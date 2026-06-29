using Godot;

/// <summary>
/// HUD — Clean minimal style.
/// Node paths khớp với HUD.tscn mới.
///
/// Features:
///  - O2 bar: fill xanh → đỏ khi < 25%, pulse animation
///  - Bag bar: fill teal, label "x.x/yy kg"
///  - Depth: số lớn, ẩn khi trên mặt nước
///  - Coins: góc phải cùng row với depth
///  - BottomIconBar: 4 icon buttons (Shop, Upgrade, Book, Menu)
///    chỉ hiển thị khi đang trên mặt nước (Surface state)
/// </summary>
public partial class HUD : CanvasLayer
{
	// ── Node refs ────────────────────────────────────────────────────────────
	private ProgressBar _o2Bar;
	private Label       _o2ValLabel;
	private ProgressBar _bagBar;
	private Label       _bagValLabel;
	private Label       _depthLabel;
	private Label       _coinsLabel;

	// Bottom icon bar
	private Control        _bottomIconBar;
	private TextureButton  _shopIconBtn;
	private TextureButton  _upgradeIconBtn;
	private TextureButton  _bookIconBtn;
	private TextureButton  _menuIconBtn;

	// Death overlay nodes (tạo dynamic trong _Ready)
	private ColorRect _deathOverlay;
	private Label     _deathLabel;

	// O2 bar fill styles
	private StyleBoxFlat _o2FillNormal;
	private StyleBoxFlat _o2FillWarn;

	// ── State ────────────────────────────────────────────────────────────────
	private float _o2Current = 1f;
	private float _o2Max     = 1f;
	private float _pulseTime = 0f;
	private bool  _isWarn    = false;
	private bool  _isDead    = false;
	private bool _drownWarningShown = false;

	// ── Colors ───────────────────────────────────────────────────────────────
	private static readonly Color ColorO2Normal  = new Color(0.31f, 0.76f, 0.97f, 1f);
	private static readonly Color ColorO2Warn    = new Color(0.92f, 0.30f, 0.28f, 1f);
	private static readonly Color ColorValNormal = new Color(1f, 1f, 1f, 0.45f);
	private static readonly Color ColorValWarn   = new Color(0.92f, 0.30f, 0.28f, 0.9f);
	private static readonly Color ColorCoins     = new Color(1.0f, 0.85f, 0.35f, 0.85f);
	private static readonly Color ColorPillBg  = new Color(1f, 1f, 1f, 0.0f);
	private static readonly Color ColorPillQty = new Color(1f, 1f, 1f, 0.85f);
	private HBoxContainer _inventoryRow;

	public override void _Ready()
	{
		_o2Bar       = GetNode<ProgressBar>("HudPanel/VBox/O2Row/O2BarBg/O2Bar");
		_o2ValLabel  = GetNode<Label>      ("HudPanel/VBox/O2Row/O2ValLabel");
		_bagBar      = GetNode<ProgressBar>("HudPanel/VBox/BagRow/BagBarBg/BagBar");
		_bagValLabel = GetNode<Label>      ("HudPanel/VBox/BagRow/BagValLabel");
		_depthLabel  = GetNode<Label>      ("BottomRow/DepthLabel");
		_coinsLabel  = GetNode<Label>      ("BottomRow/CoinsLabel");
		_inventoryRow = GetNode<HBoxContainer>("InventoryRow");

		// ── Bottom icon bar ──────────────────────────────────────────────────
		_bottomIconBar  = GetNode<Control>       ("BottomIconBar");
		_shopIconBtn    = GetNode<TextureButton> ("BottomIconBar/ShopIconBtn");
		_upgradeIconBtn = GetNode<TextureButton> ("BottomIconBar/UpgradeIconBtn");
		_bookIconBtn    = GetNode<TextureButton> ("BottomIconBar/BookIconBtn");
		_menuIconBtn    = GetNode<TextureButton> ("BottomIconBar/MenuIconBtn");

		_shopIconBtn.Pressed    += OnShopIconPressed;
		_upgradeIconBtn.Pressed += OnUpgradeIconPressed;
		_bookIconBtn.Pressed    += OnBookIconPressed;
		_menuIconBtn.Pressed    += OnMenuIconPressed;

		// ── Build death overlay ──────────────────────────────────────────────
		_deathOverlay              = new ColorRect();
		_deathOverlay.Color        = new Color(0f, 0f, 0f, 0f);
		_deathOverlay.AnchorLeft   = 0f;
		_deathOverlay.AnchorTop    = 0f;
		_deathOverlay.AnchorRight  = 1f;
		_deathOverlay.AnchorBottom = 1f;
		_deathOverlay.MouseFilter  = Control.MouseFilterEnum.Ignore;
		_deathOverlay.Visible      = false;
		AddChild(_deathOverlay);

		_deathLabel                    = new Label();
		_deathLabel.Text               = Tr("DROWNED");
		_deathLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_deathLabel.VerticalAlignment   = VerticalAlignment.Center;
		_deathLabel.AnchorLeft         = 0f;
		_deathLabel.AnchorTop          = 0f;
		_deathLabel.AnchorRight        = 1f;
		_deathLabel.AnchorBottom       = 1f;
		_deathLabel.Modulate           = new Color(1f, 1f, 1f, 0f);
		_deathLabel.AddThemeFontSizeOverride("font_size", 48);
		_deathOverlay.AddChild(_deathLabel);

		// ── Fill styles ──────────────────────────────────────────────────────
		_o2FillNormal = MakeFill(ColorO2Normal);
		_o2FillWarn   = MakeFill(ColorO2Warn);
		_o2Bar.AddThemeStyleboxOverride("fill", _o2FillNormal);

		// ── Subscribe events ─────────────────────────────────────────────────
		EventBus.Instance.OxygenChanged    += OnOxygenChanged;
		EventBus.Instance.OxygenCritical   += OnOxygenCritical;
		EventBus.Instance.InventoryChanged += OnInventoryChanged;
		EventBus.Instance.CoinsChanged     += OnCoinsChanged;
		EventBus.Instance.PlayerDrowned    += OnPlayerDrowned;
		EventBus.Instance.ResourceCaughtFx += OnResourceCaughtFx;
		EventBus.Instance.DiveStarted      += OnDiveStarted;
		EventBus.Instance.DiveEnded        += OnDiveEnded;

		// Init
		UpdateCoins(EconomyManager.Instance.Coins);
		OnInventoryChanged(0f);
		GetNode<Control>("HudPanel/VBox/O2Row").Visible = false;

		// BottomIconBar bắt đầu ẩn (chờ Surface state)
		_bottomIconBar.Visible = false;
	}

	public override void _Process(double delta)
	{
		if (_isDead) return;

		// ── Depth ─────────────────────────────────────────────────────────
		var player = GetTree().Root.FindChild("Player", true, false) as SwimController;
		if (player != null)
		{
			float depth = player.GetDepth();
			_depthLabel.Text = depth > 0.5f ? $"📏{depth:F1}m" : "—";
		}

		// ── O2 row visibility ─────────────────────────────────────────────
		bool diving = GameManager.Instance.IsDiving();
		GetNode<Control>("HudPanel/VBox/O2Row").Visible = diving;

		// ── Pulse animation ───────────────────────────────────────────────
		if (_isWarn && diving)
		{
			_pulseTime += (float)delta * 3.5f;
			float alpha = 0.55f + 0.45f * Mathf.Sin(_pulseTime);
			_o2Bar.Modulate = new Color(1f, 1f, 1f, alpha);
		}
		else
		{
			_pulseTime = 0f;
			_o2Bar.Modulate = Colors.White;
		}
	}

	// ── BottomIconBar visibility ──────────────────────────────────────────────

	/// Hiển thị icon bar khi player lên mặt nước (Surface state).
	private void OnDiveEnded()
	{
		_bottomIconBar.Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	/// Ẩn icon bar khi player lặn xuống.
	private void OnDiveStarted()
	{
		_bottomIconBar.Visible = false;
	}

	// ── Icon button handlers ──────────────────────────────────────────────────

	private void OnShopIconPressed()
	{
		var shopScreen = GetTree().Root.FindChild("ShopScreen", true, false) as ShopScreen;
		shopScreen?.OpenSell();
	}

	private void OnUpgradeIconPressed()
	{
		var upgradeScreen = GetTree().Root.FindChild("UpgradeScreen", true, false) as UpgradeScreen;
		upgradeScreen?.Show();
	}

	private void OnBookIconPressed()
	{
		var bookScreen = GetTree().Root.FindChild("BookScreen", true, false) as BookScreen;
		bookScreen?.Show();
	}

	private void OnMenuIconPressed()
	{
		// Ẩn icon bar, mở Main Menu
		_bottomIconBar.Visible = false;
		var menuScreen = GetTree().Root.FindChild("MenuScreen", true, false) as MenuScreen;
		menuScreen?.Show(isFirstLaunch: false);
	}

	// ── Death sequence ────────────────────────────────────────────────────────

	private void OnPlayerDrowned()
	{
		_isDead = true;
		_bottomIconBar.Visible = false;
		GetNode<Control>("HudPanel/VBox/O2Row").Visible = false;
		StartDeathSequence();
	}

	private async void StartDeathSequence()
	{
		_deathOverlay.Visible = true;

		float elapsed = 0f;
		const float FadeDuration = 0.8f;
		while (elapsed < FadeDuration)
		{
			elapsed += (float)GetProcessDeltaTime();
			float t = Mathf.Clamp(elapsed / FadeDuration, 0f, 1f);
			_deathOverlay.Color = new Color(0f, 0f, 0f, t);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		_deathOverlay.Color = new Color(0f, 0f, 0f, 1f);

		_deathLabel.Modulate = new Color(1f, 1f, 1f, 1f);

		await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

		DoRespawn();

		elapsed = 0f;
		const float FadeOutDuration = 0.5f;
		while (elapsed < FadeOutDuration)
		{
			elapsed += (float)GetProcessDeltaTime();
			float t = Mathf.Clamp(elapsed / FadeOutDuration, 0f, 1f);
			_deathOverlay.Color  = new Color(0f, 0f, 0f, 1f - t);
			_deathLabel.Modulate = new Color(1f, 1f, 1f, 1f - t);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		_deathOverlay.Visible = false;
		_isDead = false;
	}

	private void DoRespawn()
	{
		var bag = GetTree().Root.FindChild("StorageBag", true, false) as StorageBag;
		bag?.ClearInventory();

		var player = GetTree().Root.FindChild("Player", true, false) as SwimController;
		if (player != null)
		{
			player.GlobalPosition = new Vector3(0f, 0.5f, 0f);
			player.Velocity       = Vector3.Zero;
		}

		var o2 = GetTree().Root.FindChild("OxygenSystem", true, false) as OxygenSystem;
		o2?.RefreshFromUpgrade();

		EconomyManager.Instance.ResetCoins();

		UpgradeManager.Instance.ResetTools();

		var spawner = GetTree().Root.FindChild("ResourceSpawner", true, false) as ResourceSpawner;
		spawner?.RespawnAll();

		GameManager.Instance.ChangeState(GameManager.GameState.Surface);
	}

	// ── Heartbeat trigger ─────────────────────────────────────────────────────

	private void OnOxygenCritical()
	{
		AudioManager.Instance.PlayHeartbeat();
		if (!_drownWarningShown)
		{
			_drownWarningShown = true;
			FloatingMessage.Show(GetTree(), "⚠ " + Tr("LOW_OXYGEN"), new Color(0.92f, 0.30f, 0.28f));
		}
	}

	// ── Event handlers ────────────────────────────────────────────────────────

	private void OnOxygenChanged(float current, float max)
	{
		_o2Current = current;
		_o2Max     = max;

		float ratio = max > 0 ? current / max : 0f;
		int   pct   = (int)(ratio * 100f);
		bool  wasWarn = _isWarn;
		_isWarn     = pct < 25;

		if (!_isWarn) _drownWarningShown = false;

		_o2Bar.Value = ratio * 100f;
		_o2Bar.AddThemeStyleboxOverride("fill", _isWarn ? _o2FillWarn : _o2FillNormal);

		_o2ValLabel.Text     = $"{pct}%";
		_o2ValLabel.Modulate = _isWarn ? ColorValWarn : ColorValNormal;
	}

	private void OnInventoryChanged(float weight)
	{
		var bag = GetTree().Root.FindChild("StorageBag", true, false) as StorageBag;
		if (bag == null) return;

		float used  = bag.UsedCapacity;
		float max   = bag.MaxCapacity;
		float ratio = max > 0 ? used / max : 0f;

		_bagBar.Value     = ratio * 100f;
		_bagValLabel.Text = $"{used:F1}kg";

		_bagBar.AddThemeStyleboxOverride("fill",
			ratio > 0.85f
				? MakeFill(new Color(1.0f, 0.75f, 0.2f, 1f))
				: MakeFill(new Color(0.5f, 0.8f, 0.75f, 1f)));
		RefreshInventoryDisplay();
	}

	private void RefreshInventoryDisplay()
	{
		foreach (var child in _inventoryRow.GetChildren())
			child.QueueFree();

		foreach (var kvp in EconomyManager.Instance.Inventory)
		{
			var res = GD.Load<SeaResource>($"res://resources/items/{kvp.Key}.tres");
			if (res == null) continue;

			_inventoryRow.AddChild(MakeInventoryPill(res.Icon, kvp.Value));
		}
	}

	private Control MakeInventoryPill(Texture2D icon, int qty)
	{
		var bg = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = ColorPillBg;
		style.CornerRadiusTopLeft = style.CornerRadiusTopRight =
		style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 0;
		style.ContentMarginLeft  = style.ContentMarginRight = 8;
		style.ContentMarginTop   = style.ContentMarginBottom = 4;
		bg.AddThemeStyleboxOverride("panel", style);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);

		if (icon != null)
		{
			var iconRect = new TextureRect();
			iconRect.Texture = icon;
			iconRect.CustomMinimumSize = new Vector2(30, 30);
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			row.AddChild(iconRect);
		}

		var qtyLabel = new Label();
		qtyLabel.Text     = qty.ToString();
		qtyLabel.Modulate = ColorPillQty;
		qtyLabel.AddThemeFontSizeOverride("font_size", 14);
		qtyLabel.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(qtyLabel);

		bg.AddChild(row);
		return bg;
	}

	private void OnCoinsChanged(int amount) => UpdateCoins(amount);

	private void UpdateCoins(int amount) => _coinsLabel.Text = $"🪙{amount}₫";

	private static StyleBoxFlat MakeFill(Color color)
	{
		var sb = new StyleBoxFlat();
		sb.BgColor                 = color;
		sb.CornerRadiusTopLeft     = 3;
		sb.CornerRadiusTopRight    = 3;
		sb.CornerRadiusBottomRight = 3;
		sb.CornerRadiusBottomLeft  = 3;
		return sb;
	}

	private void OnResourceCaughtFx(string id, Vector3 worldPos)
	{
		var res = GD.Load<SeaResource>($"res://resources/items/{id}.tres");
		if (res == null || res.Icon == null) return;

		SpawnFlyingIcon(res.Icon, worldPos);
	}

	private void SpawnFlyingIcon(Texture2D icon, Vector3 worldPos)
	{
		var camera = GetViewport().GetCamera3D();
		if (camera == null) return;

		Vector2 screenStart = camera.IsPositionBehind(worldPos)
			? GetViewport().GetVisibleRect().GetCenter()
			: camera.UnprojectPosition(worldPos);

		var iconRect = new TextureRect();
		iconRect.Texture            = icon;
		iconRect.CustomMinimumSize  = new Vector2(40, 40);
		iconRect.Size               = new Vector2(40, 40);
		iconRect.ExpandMode         = TextureRect.ExpandModeEnum.IgnoreSize;
		iconRect.StretchMode        = TextureRect.StretchModeEnum.KeepAspectCentered;
		iconRect.MouseFilter        = Control.MouseFilterEnum.Ignore;
		iconRect.PivotOffset        = iconRect.Size / 2f;
		iconRect.Position           = screenStart - iconRect.Size / 2f;
		AddChild(iconRect);

		Vector2 targetPos = _inventoryRow.GetGlobalRect().GetCenter();

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(iconRect, "position", targetPos - iconRect.Size / 2f, 0.5f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.TweenProperty(iconRect, "scale", Vector2.One * 0.35f, 0.5f);
		tween.Chain().TweenProperty(iconRect, "modulate:a", 0f, 0.12f);
		tween.Chain().TweenCallback(Callable.From(() => iconRect.QueueFree()));
	}
}
