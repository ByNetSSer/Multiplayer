using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Combate autoritativo: el cliente solicita un ataque y el servidor decide el resultado.
/// La vida es estado replicado; los impactos son eventos independientes.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class PlayerCombat : NetworkBehaviour
{
    [Header("Estado")]
    [SerializeField] private int maxHealth = 100;

    [Header("Ataque")]
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackCooldown = 0.55f;

    public readonly NetworkVariable<int> Health = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Estado derivado: no se replica otro booleano que pueda contradecir a Health.
    public bool IsDead => Health.Value <= 0;
    public int MaxHealth => maxHealth;

    private Renderer playerRenderer;
    private Material runtimeMaterial;
    private Color aliveColor;
    private Transform healthFill;
    private TextMesh label;
    private float nextLocalAttackTime;
    private double nextServerAttackTime;

    private static readonly Color[] PlayerColors =
    {
        new(0.18f, 0.68f, 1f),
        new(1f, 0.38f, 0.24f),
        new(0.35f, 0.9f, 0.45f),
        new(0.8f, 0.42f, 1f)
    };

    private void Awake()
    {
        playerRenderer = GetComponent<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        aliveColor = PlayerColors[(int)(OwnerClientId % (ulong)PlayerColors.Length)];
        runtimeMaterial = playerRenderer.material;
        CreateWorldHealthBar();

        Health.OnValueChanged += OnHealthChanged;
        ApplyHealthVisual(Health.Value);

        if (IsServer)
            Health.Value = maxHealth;
    }

    public override void OnNetworkDespawn()
    {
        Health.OnValueChanged -= OnHealthChanged;
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned || IsDead)
            return;

        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextLocalAttackTime)
        {
            PlayerCombat target = FindNearestTarget();
            if (target == null)
                return;

            nextLocalAttackTime = Time.time + attackCooldown;
            RequestAttackRpc(target.NetworkObjectId);
        }
    }

    private PlayerCombat FindNearestTarget()
    {
        PlayerCombat nearest = null;
        float nearestDistance = attackRange;

        foreach (PlayerCombat candidate in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
        {
            if (candidate == this || !candidate.IsSpawned || candidate.IsDead)
                continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance <= nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    // En NGO 2.13, InvokePermission.Owner reemplaza a RequireOwnership = true.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestAttackRpc(ulong targetNetworkObjectId, RpcParams rpcParams = default)
    {
        // Nunca confiamos en el resultado enviado por el cliente: validamos su intención.
        if (rpcParams.Receive.SenderClientId != OwnerClientId || IsDead)
            return;

        double now = NetworkManager.ServerTime.Time;
        if (now < nextServerAttackTime)
            return;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId, out NetworkObject targetObject))
            return;

        PlayerCombat target = targetObject.GetComponent<PlayerCombat>();
        if (target == null || target == this || target.IsDead)
            return;

        if (Vector3.Distance(transform.position, target.transform.position) > attackRange)
            return;

        nextServerAttackTime = now + attackCooldown;
        int appliedDamage = Mathf.Min(attackDamage, target.Health.Value);
        target.Health.Value -= appliedDamage;

        // Evento fiable: confirma cada golpe y transporta sus datos, sin leer el estado al llegar.
        PlayHitFeedbackRpc(targetNetworkObjectId, appliedDamage, target.Health.Value);

        // Evento cosmético: si se pierde, el estado correcto permanece en Health.
        PlayImpactParticlesRpc(targetNetworkObjectId);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayHitFeedbackRpc(ulong targetNetworkObjectId, int damage, int remainingHealth)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId, out NetworkObject targetObject))
            return;

        PlayerCombat target = targetObject.GetComponent<PlayerCombat>();
        if (target == null)
            return;

        target.ShowFloatingDamage(damage, remainingHealth);
        target.PlaySyntheticHitSound(remainingHealth <= 0);
    }

    [Rpc(SendTo.Everyone, Delivery = RpcDelivery.Unreliable)]
    private void PlayImpactParticlesRpc(ulong targetNetworkObjectId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId, out NetworkObject targetObject))
            return;

        PlayerCombat target = targetObject.GetComponent<PlayerCombat>();
        if (target != null)
            target.SpawnImpactParticles();
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        ApplyHealthVisual(newValue);
    }

    private void ApplyHealthVisual(int value)
    {
        float normalized = Mathf.Clamp01(value / (float)maxHealth);
        if (healthFill != null)
        {
            healthFill.localScale = new Vector3(normalized, 1f, 1f);
            healthFill.localPosition = new Vector3((normalized - 1f) * 0.85f, 0f, -0.02f);
            healthFill.GetComponent<Renderer>().material.color =
                Color.Lerp(new Color(0.95f, 0.15f, 0.12f), new Color(0.2f, 0.95f, 0.35f), normalized);
        }

        if (label != null)
            label.text = $"J{OwnerClientId}  {value}/{maxHealth}";

        if (runtimeMaterial != null)
            runtimeMaterial.color = IsDead ? new Color(0.24f, 0.26f, 0.3f) : aliveColor;
    }

    private void CreateWorldHealthBar()
    {
        GameObject root = new("HealthBar");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        root.AddComponent<BillboardToCamera>();

        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
        background.name = "Background";
        background.transform.SetParent(root.transform, false);
        background.transform.localScale = new Vector3(1.8f, 0.18f, 0.08f);
        background.GetComponent<Renderer>().material.color = new Color(0.035f, 0.045f, 0.065f);
        Destroy(background.GetComponent<Collider>());

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "Fill";
        fill.transform.SetParent(root.transform, false);
        fill.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        fill.transform.localScale = new Vector3(1.7f, 0.11f, 0.09f);
        Destroy(fill.GetComponent<Collider>());
        healthFill = fill.transform;

        GameObject textObject = new("HealthLabel");
        textObject.transform.SetParent(root.transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0.28f, -0.03f);
        label = textObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 42;
        label.characterSize = 0.045f;
        label.color = Color.white;
    }

    private void ShowFloatingDamage(int damage, int remainingHealth)
    {
        GameObject damageObject = new("FloatingDamage");
        damageObject.transform.position = transform.position + Vector3.up * 2.3f;
        TextMesh text = damageObject.AddComponent<TextMesh>();
        text.text = remainingHealth <= 0 ? $"-{damage}  KO" : $"-{damage}";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 64;
        text.characterSize = 0.06f;
        text.color = remainingHealth <= 0 ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.85f, 0.2f);
        damageObject.AddComponent<BillboardToCamera>();
        StartCoroutine(AnimateFloatingDamage(damageObject.transform, text));
    }

    private IEnumerator AnimateFloatingDamage(Transform floating, TextMesh text)
    {
        float elapsed = 0f;
        Color startColor = text.color;
        while (elapsed < 0.9f)
        {
            elapsed += Time.deltaTime;
            floating.position += Vector3.up * (Time.deltaTime * 1.1f);
            text.color = new Color(startColor.r, startColor.g, startColor.b, 1f - elapsed / 0.9f);
            yield return null;
        }
        Destroy(floating.gameObject);
    }

    private void SpawnImpactParticles()
    {
        GameObject burst = new("ImpactBurst");
        burst.transform.position = transform.position + Vector3.up;
        ParticleSystem particles = burst.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        //main.duration = 0.25f;
        main.startLifetime = 0.35f;
        main.startSpeed = 4f;
        main.startSize = 0.12f;
        main.startColor = new Color(1f, 0.45f, 0.12f);
        main.loop = false;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        particles.Play();
        Destroy(burst, 1.2f);
    }

    private void PlaySyntheticHitSound(bool lethal)
    {
        AudioSource source = gameObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.65f;
            source.volume = 0.45f;
        }

        const int sampleRate = 22050;
        const float duration = 0.11f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float frequency = lethal ? 105f : 165f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - t / duration;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.55f;
        }

        AudioClip clip = AudioClip.Create("NetworkHit", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        source.PlayOneShot(clip);
        Destroy(clip, duration + 0.1f);
    }
}

public class BillboardToCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }
}
