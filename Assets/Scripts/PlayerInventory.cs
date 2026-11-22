using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum ItemType : byte { Wood = 1, Cube = 2, Coin = 3 }

public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
{
    public ItemType Type;
    public ushort Amount;

    public InventorySlot(ItemType type, ushort amount) { Type = type; Amount = amount; }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref Amount);
    }

    public bool Equals(InventorySlot other) => Type == other.Type && Amount == other.Amount;
    public override bool Equals(object obj) => obj is InventorySlot other && Equals(other);
    public override int GetHashCode() => HashCode.Combine((byte)Type, Amount);
}

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] private ushort maxStack = 99;
    public NetworkList<InventorySlot> Slots { get; private set; }
    private string inventoryText = "INVENTARIO\nVacío";

    private void Awake()
    {
        Slots = new NetworkList<InventorySlot>(null, NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        Slots.OnListChanged += OnListChanged;
        RebuildOwnerUI();
        if (IsOwner)
            Lab09CloudBackpack.RegisterLocalInventory(this);
    }

    public override void OnNetworkDespawn()
    {
        List<InventorySlot> snapshot = CopySlots();
        Lab09ReconnectCoordinator.RecordInventoryDespawn(OwnerClientId, snapshot);
        if (IsOwner)
            Lab09CloudBackpack.SaveOwnedInventory(snapshot);
        Slots.OnListChanged -= OnListChanged;
        inventoryText = "INVENTARIO\nVacío";
    }

    public bool AddItem(ItemType type, ushort amount)
    {
        if (!IsServer || amount == 0) return false;

        for (int i = 0; i < Slots.Count; i++)
        {
            InventorySlot slot = Slots[i];
            if (slot.Type != type || slot.Amount >= maxStack) continue;
            ushort accepted = (ushort)Mathf.Min(amount, maxStack - slot.Amount);
            slot.Amount += accepted;
            Slots[i] = slot;
            amount -= accepted;
            if (amount == 0) return true;
        }

        while (amount > 0)
        {
            ushort accepted = (ushort)Mathf.Min(amount, maxStack);
            Slots.Add(new InventorySlot(type, accepted));
            amount -= accepted;
        }
        return true;
    }

    // Compatibilidad con el recurso de tronco de laboratorios anteriores.
    public void AddWood(int amount)
    {
        if (amount > 0)
            AddItem(ItemType.Wood, (ushort)Mathf.Min(amount, ushort.MaxValue));
    }

    public List<InventorySlot> CopySlots()
    {
        List<InventorySlot> copy = new(Slots.Count);
        for (int index = 0; index < Slots.Count; index++)
            copy.Add(Slots[index]);
        return copy;
    }

    public void ReplaceSlotsFromServer(IReadOnlyList<InventorySlot> restoredSlots)
    {
        if (!IsServer || restoredSlots == null) return;
        Slots.Clear();
        for (int index = 0; index < restoredSlots.Count; index++)
            Slots.Add(restoredSlots[index]);
    }

    public void RequestCloudRestore(string serializedBackpack)
    {
        if (!IsOwner || string.IsNullOrWhiteSpace(serializedBackpack)) return;
        RestoreCloudBackpackRpc(new FixedString4096Bytes(serializedBackpack));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RestoreCloudBackpackRpc(FixedString4096Bytes serializedBackpack)
    {
        if (!Lab09ReconnectCoordinator.TryConsumeCloudRestore(OwnerClientId)) return;
        List<InventorySlot> restoredSlots = new();
        if (!Lab09BackpackSerializer.TryDeserialize(serializedBackpack.ToString(), restoredSlots)) return;
        ReplaceSlotsFromServer(restoredSlots);
        Debug.Log($"[Lab 9] Cloud Save restauró {restoredSlots.Count} casilla(s) para ClientId {OwnerClientId}.");
    }

    private void OnListChanged(NetworkListEvent<InventorySlot> change)
    {
        if (IsOwner)
            Debug.Log($"[Inventario] {change.Type}: índice {change.Index}, {change.Value.Type} x{change.Value.Amount}");
        RebuildOwnerUI();
    }

    private void RebuildOwnerUI()
    {
        if (!IsOwner) return;
        if (Slots.Count == 0) { inventoryText = "INVENTARIO\nVacío"; return; }
        inventoryText = "INVENTARIO";
        for (int i = 0; i < Slots.Count; i++)
            inventoryText += $"\n{Slots[i].Type}: {Slots[i].Amount}";
    }

    private void OnGUI()
    {
        if (!IsOwner || !IsSpawned) return;
        GUIStyle style = new(GUI.skin.box)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(12, 12, 10, 10),
            normal = { textColor = Color.white }
        };
        GUI.Box(new Rect(18f, 310f, 250f, 82f + Slots.Count * 24f), inventoryText, style);
    }
}
