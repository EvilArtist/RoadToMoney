using Godot;

/// <summary>
/// HUD — Clean minimal style.
/// Node paths khớp với HUD.tscn mới.
///
/// Features:
///  - O2 bar: fill xanh → đỏ khi &lt; 25%, pulse animation
///  - Bag bar: fill teal, label "x.x/yy kg"
///  - Depth: số lớn, ẩn khi trên mặt nước
///  - Coins: góc phải cùng row với depth
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


		// ── Build death overlay ──────────────────────────────────────────────
		_deathOverlay              = new ColorRect();
		_deathOverlay.Color        = new Color(0f, 0f, 0f, 0f); // bắt đầu trong suốt
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
		_deathLabel.Modulate           = new Color(1f, 1f, 1f, 0f); // ẩn ban đầu
		_deathLabel.AddThemeFontSizeOverride("font_size", 48);
		_deathOverlay.AddChild(_deathLabel);

		// ── Fill styles ──────────────────────────────────────────────────────
		_o2FillNormal = MakeFill(ColorO2Normal);
		_o2FillWarn   = MakeFill(ColorO2Warn);
		_o2Bar.AddThemeStyleboxOverride("fill", _o2FillNormal);

		// ── Subscribe events ─────────────────────────────────────────────────
		EventBus.Instance.OxygenChanged  += OnOxygenChanged;
		EventBus.Instance.OxygenCritical += OnOxygenCritical;
		EventBus.Instance.InventoryChanged += OnInventoryChanged;
		EventBus.Instance.CoinsChanged   += OnCoinsChanged;
		EventBus.Instance.PlayerDrowned  += OnPlayerDrowned;
		EventBus.Instance.ResourceCaughtFx += OnResourceCaughtFx;

		// Init
		UpdateCoins(EconomyManager.Instance.Coins);
		OnInventoryChanged(0f);
		GetNode<Control>("HudPanel/VBox/O2Row").Visible = false;
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

	// ── Death sequence ────────────────────────────────────────────────────────

	private void OnPlayerDrowned()
	{
		_isDead = true;
		// Tắt O2 row ngay
		GetNode<Control>("HudPanel/VBox/O2Row").Visible = false;
		// Bắt đầu coroutine
		StartDeathSequence();
	}

	private async void StartDeathSequence()
	{
		_deathOverlay.Visible = true;

		// Phase 1: Fade to black (0.8s)
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

		// Phase 2: Hiện text "You drowned."
		_deathLabel.Modulate = new Color(1f, 1f, 1f, 1f);

		// Phase 3: Chờ 2s
		await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);

		// Phase 4: Respawn
		DoRespawn();

		// Phase 5: Fade out overlay (0.5s)
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
		// 1. Xóa inventory (mất hải sản, giữ coins)
		var bag = GetTree().Root.FindChild("StorageBag", true, false) as StorageBag;
		bag?.ClearInventory();

		// 2. Teleport player về origin, reset velocity
		var player = GetTree().Root.FindChild("Player", true, false) as SwimController;
		if (player != null)
		{
			player.GlobalPosition = new Vector3(0f, 0.5f, 0f);
			player.Velocity       = Vector3.Zero;
		}

		// 3. Reset O2
		var o2 = GetTree().Root.FindChild("OxygenSystem", true, false) as OxygenSystem;
		o2?.RefreshFromUpgrade();

		// 4 Reset Coins
		EconomyManager.Instance.ResetCoins();

		// 6 Reset Tools
		UpgradeManager.Instance.ResetTools();

		// 4. Respawn resources
		var spawner = GetTree().Root.FindChild("ResourceSpawner", true, false) as ResourceSpawner;
		spawner?.RespawnAll();

		// 5. GameManager về Surface
		GameManager.Instance.ChangeState(GameManager.GameState.Surface);
	}

	// ── Heartbeat trigger ─────────────────────────────────────────────────────

	private void OnOxygenCritical()
	{
		// Chỉ trigger heartbeat khi đang dive, không trigger liên tục
		// AudioManager throttle bằng cách check IsPlaying
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

		// Reset warning flag khi O2 phục hồi (sau khi respawn)
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

		// Đích: tâm InventoryRow — đơn giản và ổn định, tránh phụ thuộc
		// vào pill cụ thể (vì pill được rebuild toàn bộ mỗi khi InventoryChanged bắn)
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
