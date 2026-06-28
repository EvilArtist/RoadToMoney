using Godot;

/// <summary>
/// UpgradeScreen — Clean minimal style.
/// Node paths khớp với UpgradeScreen.tscn mới.
/// Dữ liệu category (key/tên/icon) + UpgradeItems (cost/description/value/level
/// theo từng cấp, CHUNG resource với UpgradeManager) được gán qua Resource (.tres)
/// trong Inspector — không hardcode nữa, đổi số không cần build lại code.
/// </summary>
public partial class UpgradeScreen : CanvasLayer
{
	// Singleton-style access, giống pattern UpgradeManager.Instance/EconomyManager.Instance.
	public static UpgradeScreen Instance { get; private set; }

	// ── Node refs ────────────────────────────────────────────────────────────
	private Label         _coinsLabel;
	private VBoxContainer _upgradeList;
	private Button        _backButton;

	// ── Data — gán resource .tres trong Inspector ─────────────────────────────
	// Mỗi phần tử là 1 UpgradeCategoryData (key, tên, icon, costs, descriptions).
	// Thứ tự hiển thị = thứ tự trong mảng này.
	[Export] public UpgradeCategoryData[] Categories = System.Array.Empty<UpgradeCategoryData>();

	// ── Colors (chỉ là style UI, không cần resource hóa) ──────────────────────
	private static readonly Color ColorCatName     = new Color(1f, 1f, 1f, 0.85f);
	private static readonly Color ColorCatIcon     = new Color(0.31f, 0.76f, 0.97f, 0.8f);
	private static readonly Color ColorLevel       = new Color(1f, 1f, 1f, 0.3f);
	private static readonly Color ColorNextDesc    = new Color(0.5f, 0.8f, 0.75f, 1f);
	private static readonly Color ColorMaxed       = new Color(1f, 0.85f, 0.35f, 0.8f);
	private static readonly Color ColorCantAfford  = new Color(1f, 1f, 1f, 0.25f);
	private static readonly Color ColorBuyBtn      = new Color(1f, 1f, 1f, 0.95f);
	private static readonly Color ColorBuyDisabled = new Color(1f, 1f, 1f, 0.25f);

	public override void _Ready()
	{
		Instance = this;

		_coinsLabel  = GetNode<Label>        ("MenuCenter/Stack/CoinsRow/CoinsLabel");
		_upgradeList = GetNode<VBoxContainer>("MenuCenter/Stack/BarList");
		_backButton  = GetNode<Button>       ("MenuCenter/Stack/BarList/BackButton");

		_backButton.Pressed += OnBack;

		EventBus.Instance.CoinsChanged     += _ => RefreshUpgrades();
		EventBus.Instance.UpgradePurchased += (_, _) => RefreshUpgrades();

		// Fallback an toàn nếu quên gán resource trong Inspector — tránh NullReferenceException,
		// nhưng log cảnh báo để biết mà gán cho đúng.
		if (Categories == null || Categories.Length == 0)
		{
			GD.PushWarning("UpgradeScreen.Categories chưa được gán resource trong Inspector — màn upgrade sẽ trống.");
		}

		Visible = false;
	}

	public new void Show()
	{
		Visible = true;
		CallDeferred(nameof(RefreshUpgrades));
	}

	// ── Refresh ───────────────────────────────────────────────────────────────

	private void RefreshUpgrades()
	{
		foreach (var child in _upgradeList.GetChildren()) {
			if (child != _backButton)
				child.QueueFree();
		}

		int coins = EconomyManager.Instance.Coins;
		_coinsLabel.Text = $"{coins}₫";
		int position = 0;
		foreach (var cat in Categories)
		{
			if (cat == null) continue; // bỏ qua slot trống nếu có

			int level = UpgradeManager.Instance.Upgrades[cat.CategoryKey]?.Level ?? 0;
			var categoryCard = MakeCategoryCard(cat, level, coins);
			_upgradeList.AddChild(categoryCard);
			_upgradeList.MoveChild(categoryCard, position);
			position++;
		}
	}

	// ── Card builder ──────────────────────────────────────────────────────────

	private Control MakeCategoryCard(UpgradeCategoryData cat, int level, int coins)
	{
		UpgradeItemData[] items = cat.UpgradeItems;
		// items[0] = baseline miễn phí (đã sở hữu). Số tier PHẢI MUA = items.Length - 1.
		int paidTierCount = Mathf.Max(0, items.Length - 1);

		// Card background
		var card = new PanelContainer();
		var cardStyle = new StyleBoxFlat();
		cardStyle.BgColor = new Color(0.09019608f, 0.23137255f, 0.36078432f, 1);
		cardStyle.CornerRadiusTopLeft = cardStyle.CornerRadiusTopRight =
		cardStyle.CornerRadiusBottomLeft = cardStyle.CornerRadiusBottomRight = 20;
		cardStyle.ContentMarginLeft  = cardStyle.ContentMarginRight = 14;
		cardStyle.ContentMarginTop   = cardStyle.ContentMarginBottom = 11;
		card.AddThemeStyleboxOverride("panel", cardStyle);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);

		// ── Row 1: Icon + Name + Level ────────────────────────────────────
		var topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 8);

		var iconLabel = new Label();
		iconLabel.Text     = cat.Icon;
		iconLabel.Modulate = ColorCatIcon;
		iconLabel.VerticalAlignment = VerticalAlignment.Center;

		var nameLabel = new Label();
		nameLabel.Text     = Tr(cat.DisplayName);
		nameLabel.Modulate = ColorCatName;
		nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand;
		nameLabel.VerticalAlignment   = VerticalAlignment.Center;

		var levelLabel = new Label();
		levelLabel.Text     = $"Lv {level}/{paidTierCount}";
		levelLabel.Modulate = ColorLevel;
		levelLabel.VerticalAlignment   = VerticalAlignment.Center;
		levelLabel.HorizontalAlignment = HorizontalAlignment.Right;

		topRow.AddChild(iconLabel);
		topRow.AddChild(nameLabel);
		topRow.AddChild(levelLabel);
		vbox.AddChild(topRow);

		// ── Progress dots ────────────────────────────────────────────────
		var dotsRow = new HBoxContainer();
		dotsRow.AddThemeConstantOverride("separation", 4);
		for (int d = 0; d < paidTierCount; d++)
		{
			var dot = new Label();
			dot.Text = d < level ? "●" : "○";
			dot.Modulate = d < level
				? new Color(0.31f, 0.76f, 0.97f, 0.9f)
				: new Color(1f, 1f, 1f, 0.15f);
			dotsRow.AddChild(dot);
		}
		vbox.AddChild(dotsRow);

		// ── Row 2: Next upgrade info + buy button ─────────────────────────
		var bottomRow = new HBoxContainer();

		if (level >= paidTierCount)
		{
			// MAXED — items[level] = tier hiện tại (đã là tier cuối)
			var maxLabel = new Label();
			maxLabel.Text     = $"MAX  —  {Tr(items[level].Description)}";
			maxLabel.Modulate = ColorMaxed;
			maxLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand;
			bottomRow.AddChild(maxLabel);
		}
		else
		{
			// Cấp kế tiếp = items[level + 1] (vì items[level] là cấp ĐANG sở hữu)
			UpgradeItemData nextItem  = items[level + 1];
			bool            canAfford = coins >= nextItem.Cost;

			var nextLabel = new Label();
			nextLabel.Text     = $"→  {Tr(nextItem.Description)}";
			nextLabel.Modulate = canAfford ? ColorNextDesc : ColorCantAfford;
			nextLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand;
			nextLabel.VerticalAlignment   = VerticalAlignment.Center;

			var buyBtn = new Button();
			buyBtn.Text     = $"{nextItem.Cost}₫";
			buyBtn.Disabled = !canAfford;

			// Styling buy button
			var btnNormal = new StyleBoxFlat();
			btnNormal.BgColor = canAfford
				? new Color(0.09019608f, 0.23137255f, 0.36078432f, 1f)
				: new Color(1f, 1f, 1f, 0.05f);
			btnNormal.CornerRadiusTopLeft = btnNormal.CornerRadiusTopRight =
			btnNormal.CornerRadiusBottomLeft = btnNormal.CornerRadiusBottomRight = 20;
			btnNormal.ContentMarginLeft  = btnNormal.ContentMarginRight = 14;
			btnNormal.ContentMarginTop   = btnNormal.ContentMarginBottom = 6;

			string capturedCat  = cat.CategoryKey;
			int    capturedCost = nextItem.Cost;
			buyBtn.Pressed += () => OnBuyUpgrade(capturedCat, capturedCost);

			bottomRow.AddChild(nextLabel);
			bottomRow.AddChild(buyBtn);
		}

		vbox.AddChild(bottomRow);
		card.AddChild(vbox);
		return card;
	}

	// ── Handlers ──────────────────────────────────────────────────────────────

	private void OnBuyUpgrade(string category, int cost)
	{
		if (!EconomyManager.Instance.SpendCoins(cost)) return;
		UpgradeItemData levelData = null;
		int currentLevel = UpgradeManager.Instance.Upgrades[category]?.Level ?? 0;
		foreach (var cat in Categories)
		{
			if (cat == null) continue; // bỏ qua slot trống nếu có
			if (cat.CategoryKey == category) {
				foreach (var upgradeItem in cat.UpgradeItems) {
					if ( upgradeItem.Level ==  currentLevel + 1) {
						levelData = upgradeItem;
					}
				}
			}
		}
		UpgradeManager.Instance.ApplyUpgrade(category, levelData);
	}

	private void OnBack()
	{
		Visible = false;
		var menuScreen = GetTree().Root.FindChild("MenuScreen", true, false) as MenuScreen;
		menuScreen?.Show(isFirstLaunch: false);
	}
}
