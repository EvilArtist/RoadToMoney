using Godot;
using System.Collections.Generic;

public partial class StorageBag : Node3D
{
	public float MaxCapacity
{
	get{
		if (UpgradeManager.Instance == null) return 10f;
		return UpgradeManager.Instance.GetBagCapacity();
	}
}
	public float UsedCapacity   { get; private set; } = 0f;
	public float FreeCapacity   => MaxCapacity - UsedCapacity;
	public bool  IsFull         => UsedCapacity >= MaxCapacity;

	public override void _Ready()
	{
		EventBus.Instance.InventoryChanged += OnInventoryChanged;
		EventBus.Instance.DiveStarted      += OnDiveStarted;
	}

	private void OnDiveStarted()
	{
		UsedCapacity = 0f;
	}

	private void OnInventoryChanged(float weight)
	{
		UsedCapacity = UsedCapacity + weight;
		if (EconomyManager.Instance.Inventory.Count == 0)
			UsedCapacity = 0;
	}

	public bool CanAdd(float itemWeight) => UsedCapacity + itemWeight <= MaxCapacity;

	// Thêm vào StorageBag.cs
	public void ClearInventory()
	{
		EconomyManager.Instance.ClearInventory();
		UsedCapacity = 0f;
		// EmitInventoryChanged đã được gọi bên trong EconomyManager.ClearInventory()
	}
}
