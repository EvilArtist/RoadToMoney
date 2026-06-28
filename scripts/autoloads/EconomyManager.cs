using Godot;
using System.Collections.Generic;

public partial class EconomyManager : Node
{
	public static EconomyManager Instance { get; private set; }

	public int   Coins            { get; private set; } = 0;
	public int   TotalEarned      { get; private set; } = 0;
	public float BagWeightCurrent { get; private set; } = 0f;

	private Dictionary<string, int> _inventory = new();
	public IReadOnlyDictionary<string, int> Inventory => _inventory;

	public override void _Ready()
	{
		Instance = this;
		EventBus.Instance.ResourceCollected += OnResourceCollected;
	}

	public void AddCoins(int amount)
	{
		Coins       += amount;
		TotalEarned += amount;
		EventBus.Instance.EmitCoinsChanged(Coins);
	}

	public bool SpendCoins(int amount)
	{
		if (Coins < amount) return false;
		Coins -= amount;
		EventBus.Instance.EmitCoinsChanged(Coins);
		return true;
	}

	public void AddToInventory(string resourceId, float weight, int quantity = 1)
	{
		_inventory.TryGetValue(resourceId, out int current);
		_inventory[resourceId] = current + quantity;
		EventBus.Instance.EmitInventoryChanged(weight);
	}

	public void ResetCoins() {
		AddCoins(0 - Coins);
	}

	public void ClearInventory()
	{
		_inventory.Clear();
		BagWeightCurrent = 0f;
		EventBus.Instance.EmitInventoryChanged(0f);
	}

	private void OnResourceCollected(string id, float weight, int qty) => AddToInventory(id, weight, qty);
}
