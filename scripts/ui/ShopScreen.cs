using Godot;
using System.Collections.Generic;

/// <summary>
/// ShopScreen — Clean minimal style.
/// Node paths khớp với ShopScreen.tscn mới.
/// Logic giữ nguyên, chỉ cập nhật:
///  - Node paths
///  - Item rows được styled (icon + màu giá)
///  - Total row nổi bật hơn
/// </summary>
public partial class ShopScreen : CanvasLayer
{
	// ── Node refs ────────────────────────────────────────────────────────────
	private Label         _coinsLabel;
	private VBoxContainer _inventoryList;
	private Button        _sellAllButton;
	private Button        _closeButton;

	// ── Sell price multipliers theo rarity ───────────────────────────────────
	private static readonly float[] RarityMultiplier = { 1.0f, 1.5f, 2.5f, 4.0f };

	// ── Colors ───────────────────────────────────────────────────────────────
	private static readonly Color ColorItemName  = new Color(1f, 1f, 1f, 0.75f);
	private static readonly Color ColorItemPrice = new Color(0.5f, 0.8f, 0.75f, 1f);
	private static readonly Color ColorTotalVal  = new Color(1f, 1f, 1f, 1f);
	private static readonly Color ColorEmpty     = new Color(1f, 1f, 1f, 0.3f);
	private static readonly Color ColorCoins     = new Color(1f, 1f, 1f, 0.45f);

	public override void _Ready()
	{
		_coinsLabel = GetNode<Label>("MenuCenter/Stack/CoinsRow/CoinsLabel");
		_inventoryList = GetNode<VBoxContainer>("MenuCenter/Stack/BarList/ListPanel/ScrollContainer/InventoryList");
		_sellAllButton = GetNode<Button>       ("MenuCenter/Stack/BarList/SellAllButton");
		_closeButton   = GetNode<Button>       ("MenuCenter/Stack/BarList/CloseButton");

		_sellAllButton.Pressed += OnSellAll;
		_closeButton.Pressed   += OnBack;

		var overlay = GetNode<Control>("Overlay");
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		overlay.GuiInput   += OnOverlayInput;

		// EventBus.Instance.PlayerSurfaced += OnPlayerSurfaced;

		Visible = false;
	}

	// ── Show / hide ───────────────────────────────────────────────────────────

	public void OpenSell()
	{
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		CallDeferred(nameof(RefreshInventory));
	}

	// ── Refresh ───────────────────────────────────────────────────────────────

	private void RefreshInventory()
	{
		foreach (var child in _inventoryList.GetChildren())
			child.QueueFree();

		_coinsLabel.Text = $"{EconomyManager.Instance.Coins}₫";

		var inventory = EconomyManager.Instance.Inventory;

		if (inventory.Count == 0)
		{
			var empty = new Label();
			empty.Text     = Tr("BAG_EMPTY");
			empty.Modulate = ColorEmpty;
			_inventoryList.AddChild(empty);
			_sellAllButton.Disabled = true;
			return;
		}

		_sellAllButton.Disabled = false;
		int totalValue = 0;

		foreach (var kvp in inventory)
		{
			var res = GD.Load<SeaResource>($"res://resources/items/{kvp.Key}.tres");
			if (res == null) continue;

			int sellPrice = CalculateSellPrice(res);
			int lineTotal = sellPrice * kvp.Value;
			totalValue   += lineTotal;

			_inventoryList.AddChild(MakeItemRow(Tr(res.DisplayName), kvp.Value, sellPrice, lineTotal));
		}

		// Divider trước total
		var sep = new HSeparator();
		sep.AddThemeColorOverride("separator_color", new Color(1, 1, 1, 0.07f));
		_inventoryList.AddChild(sep);

		// Total row
		_inventoryList.AddChild(MakeTotalRow(totalValue));
	}

	// ── Row builders ──────────────────────────────────────────────────────────

	private Control MakeItemRow(string displayName, int qty, int unitPrice, int lineTotal)
	{
		// Outer margin container for padding
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left",   12);
		margin.AddThemeConstantOverride("margin_right",  12);
		margin.AddThemeConstantOverride("margin_top",    6);
		margin.AddThemeConstantOverride("margin_bottom", 6);

		// Background panel
		var bg = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = new Color(1f, 1f, 1f, 0.04f);
		style.CornerRadiusTopLeft = style.CornerRadiusTopRight =
		style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 20;
		style.ContentMarginLeft = style.ContentMarginRight = 12;
		style.ContentMarginTop  = style.ContentMarginBottom = 7;
		bg.AddThemeStyleboxOverride("panel", style);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		// Resource name + qty
		var nameLabel = new Label();
		nameLabel.Text     = $"{displayName}  ×{qty}";
		nameLabel.Modulate = ColorItemName;
		nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand;

		// Unit price (mờ)
		var unitLabel = new Label();
		unitLabel.Text     = $"{unitPrice}₫";
		unitLabel.Modulate = new Color(1f, 1f, 1f, 0.28f);
		unitLabel.VerticalAlignment = VerticalAlignment.Center;

		// Line total (nổi bật)
		var totalLabel = new Label();
		totalLabel.Text     = $"{lineTotal}₫";
		totalLabel.Modulate = ColorItemPrice;
		totalLabel.VerticalAlignment = VerticalAlignment.Center;

		row.AddChild(nameLabel);
		row.AddChild(unitLabel);
		row.AddChild(totalLabel);
		bg.AddChild(row);
		margin.AddChild(bg);
		return margin;
	}

	private Control MakeTotalRow(int total)
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left",   12);
		margin.AddThemeConstantOverride("margin_right",  12);
		margin.AddThemeConstantOverride("margin_top",    4);
		margin.AddThemeConstantOverride("margin_bottom", 8);

		var row = new HBoxContainer();

		var label = new Label();
		label.Text     = Tr("TOTAL");
		label.Modulate = new Color(1f, 1f, 1f, 0.4f);
		label.SizeFlagsHorizontal = Control.SizeFlags.Expand;

		var valLabel = new Label();
		valLabel.Text     = $"{total}₫";
		valLabel.Modulate = ColorTotalVal;
		valLabel.AddThemeFontSizeOverride("font_size", 20);
		valLabel.HorizontalAlignment = HorizontalAlignment.Right;

		row.AddChild(label);
		row.AddChild(valLabel);
		margin.AddChild(row);
		return margin;
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private int CalculateSellPrice(SeaResource res)
	{
		float multiplier = RarityMultiplier[(int)res.ResourceRarity];
		return (int)(res.BaseValue * multiplier);
	}

	// ── Button handlers ───────────────────────────────────────────────────────

	private void OnSellAll()
	{
		int total = 0;
		foreach (var kvp in EconomyManager.Instance.Inventory)
		{
			var res = GD.Load<SeaResource>($"res://resources/items/{kvp.Key}.tres");
			if (res == null) continue;
			total += CalculateSellPrice(res) * kvp.Value;
		}
		EconomyManager.Instance.AddCoins(total);
		EconomyManager.Instance.ClearInventory();
		RefreshInventory();
	}

	private void OnBack()
	{
		Visible = false;
		var hud = GetTree().Root.FindChild("Hud", true, false) as HUD;
		hud?.ReturnToIdle();
	}

	private void OnOverlayInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			OnBack();
	}
}
