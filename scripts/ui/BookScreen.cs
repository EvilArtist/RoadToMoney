using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// BookScreen — Book #12: Sổ tay hải sản / Field Guide.
/// Quét toàn bộ .tres trong resources/items/ bằng DirAccess (ResourceSpawner không
/// expose list master ra ngoài nên đây là cách an toàn nhất, không cần sửa file đó).
/// Mỗi ô hiển thị silhouette + "???" nếu chưa khám phá, hoặc icon thật + tên +
/// rarity badge + ngày bắt lần đầu nếu đã khám phá — dữ liệu lấy từ DiscoveryManager.
/// </summary>
public partial class BookScreen : CanvasLayer
{
	private const string ItemsFolder = "res://resources/items/";

	// ── Node refs ────────────────────────────────────────────────────────────
	private GridContainer _grid;
	private Label          _progressLabel;
	private Button         _closeButton;

	private Shader _silhouetteShader;

	// ── Màu rarity badge — cùng tinh thần với RarityMultiplier ở ShopScreen ──
	private static readonly Color[] RarityColor =
	{
		new Color(0.7f,  0.7f,  0.7f,  1f), // Common   — xám
		new Color(0.4f,  0.85f, 0.45f, 1f), // Uncommon — xanh lá
		new Color(0.35f, 0.65f, 0.95f, 1f), // Rare     — xanh dương
		new Color(0.85f, 0.5f,  0.95f, 1f), // Epic     — tím
	};

	private static readonly Color ColorDiscoveredName = new Color(1f, 1f, 1f, 0.9f);
	private static readonly Color ColorUnknownName     = new Color(1f, 1f, 1f, 0.35f);
	private static readonly Color ColorCatchInfo       = new Color(1f, 1f, 1f, 0.45f);
	private static readonly Color ColorProgress        = new Color(1f, 1f, 1f, 0.6f);

	public override void _Ready()
	{
		_grid          = GetNode<GridContainer>("MenuCenter/Stack/BookPanel/ScrollContainer/Grid");
		_progressLabel = GetNode<Label>         ("MenuCenter/Stack/ProgressLabel");
		_closeButton   = GetNode<Button>        ("MenuCenter/Stack/CloseButton");

		_progressLabel.Modulate = ColorProgress;

		_closeButton.Pressed += OnBack;

		_silhouetteShader = GD.Load<Shader>("res://assets/shaders/silhouette_grayscale.gdshader");

		// Tự refresh khi vừa khám phá loài mới trong khi đang mở sổ tay (hiếm khi xảy ra
		// vì BookScreen chỉ mở lúc không lặn, nhưng vẫn an toàn để tương lai gắn nút HUD).
		EventBus.Instance.DiscoveryUnlocked += _ => { if (Visible) RefreshGrid(); };

		Visible = false;
	}

	// ── Show / hide ───────────────────────────────────────────────────────────

	public new void Show()
	{
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		CallDeferred(nameof(RefreshGrid));
	}

	public new void Hide()
	{
		Visible = false;
	}

	// ── Refresh ───────────────────────────────────────────────────────────────

	private void RefreshGrid()
	{
		foreach (var child in _grid.GetChildren())
			child.QueueFree();

		var resources = LoadAllSeaResources();

		int discoveredCount = 0;
		foreach (var res in resources)
		{
			var entry = DiscoveryManager.Instance.GetEntry(res.Id);
			if (entry.Discovered) discoveredCount++;
			_grid.AddChild(MakeCell(res, entry));
		}

		_progressLabel.Text = $"{Tr("FIELD_GUIDE_PROGRESS")}: {discoveredCount}/{resources.Count}";
	}

	private List<SeaResource> LoadAllSeaResources()
	{
		var result = new List<SeaResource>();
		using var dir = DirAccess.Open(ItemsFolder);
		if (dir == null)
		{
			GD.PushWarning($"BookScreen: không mở được folder {ItemsFolder}");
			return result;
		}

		dir.ListDirBegin();
		string fileName = dir.GetNext();
		while (fileName != "")
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
			{
				var res = ResourceLoader.Load<SeaResource>(ItemsFolder + fileName, "", ResourceLoader.CacheMode.Ignore);
				if (res != null) result.Add(res);
			}
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();

		// Sắp xếp theo rarity rồi theo tên — bảng hiển thị ổn định, dễ quét bằng mắt.
		result.Sort((a, b) =>
		{
			int rarityCmp = a.ResourceRarity.CompareTo(b.ResourceRarity);
			return rarityCmp != 0 ? rarityCmp : a.Id.CompareTo(b.Id);
		});
		return result;
	}

	// ── Cell builder ──────────────────────────────────────────────────────────

	private Control MakeCell(SeaResource res, DiscoveryEntry entry)
	{
		var cardStyle = new StyleBoxFlat();
		cardStyle.BgColor = new Color(0.09019608f, 0.23137255f, 0.36078432f, entry.Discovered ? 1f : 0.5f);
		cardStyle.CornerRadiusTopLeft = cardStyle.CornerRadiusTopRight =
		cardStyle.CornerRadiusBottomLeft = cardStyle.CornerRadiusBottomRight = 18;
		cardStyle.ContentMarginLeft  = cardStyle.ContentMarginRight = 10;
		cardStyle.ContentMarginTop   = cardStyle.ContentMarginBottom = 10;

		var card = new PanelContainer();
		card.CustomMinimumSize = new Vector2(150, 170);
		card.AddThemeStyleboxOverride("panel", cardStyle);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;

		// ── Icon (thật hoặc silhouette) ────────────────────────────────────
		var iconRect = new TextureRect();
		iconRect.Texture = res.Icon;
		iconRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
		iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		iconRect.CustomMinimumSize = new Vector2(72, 72);
		iconRect.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		if (!entry.Discovered)
		{
			var mat = new ShaderMaterial();
			mat.Shader = _silhouetteShader;
			iconRect.Material = mat;
			iconRect.SelfModulate = new Color(1f, 1f, 1f, 0.55f);
		}

		vbox.AddChild(iconRect);

		// ── Rarity badge (chỉ hiện khi đã khám phá — tránh leak thông tin) ──
		if (entry.Discovered)
		{
			var badge = new Label();
			badge.Text = Tr(RarityLabelKey(res.ResourceRarity));
			badge.Modulate = RarityColor[(int)res.ResourceRarity];
			badge.HorizontalAlignment = HorizontalAlignment.Center;
			badge.AddThemeFontSizeOverride("font_size", 11);
			vbox.AddChild(badge);
		}

		// ── Tên ──────────────────────────────────────────────────────────────
		var nameLabel = new Label();
		nameLabel.Text = entry.Discovered ? Tr(res.DisplayName) : "???";
		nameLabel.Modulate = entry.Discovered ? ColorDiscoveredName : ColorUnknownName;
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AddThemeFontSizeOverride("font_size", 16);
		vbox.AddChild(nameLabel);

		// ── Thông tin bắt lần đầu ────────────────────────────────────────────
		var infoLabel = new Label();
		infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		infoLabel.HorizontalAlignment = HorizontalAlignment.Center;
		infoLabel.AddThemeFontSizeOverride("font_size", 11);
		infoLabel.Modulate = ColorCatchInfo;
		if (entry.Discovered)
		{
			infoLabel.Text = $"{Tr("FIRST_CAUGHT")}: {FormatFirstCaught(entry.FirstCaughtUnixTime)}\n×{entry.TimesCaught}";
		}
		else
		{
			infoLabel.Text = Tr("NOT_DISCOVERED_YET");
		}
		vbox.AddChild(infoLabel);

		card.AddChild(vbox);
		return card;
	}

	private static string RarityLabelKey(SeaResource.Rarity rarity) => rarity switch
	{
		SeaResource.Rarity.Uncommon => "RARITY_UNCOMMON",
		SeaResource.Rarity.Rare     => "RARITY_RARE",
		SeaResource.Rarity.Epic     => "RARITY_EPIC",
		_                           => "RARITY_COMMON",
	};

	// Ngày giờ THỰC lúc bắt lần đầu (không phải epoch trong game) — đổi định dạng
	// theo ngôn ngữ hiện tại (SettingsManager.Language: "vi" | "en").
	// vi: 29/06/2026 13:12   |   en: Jun 29, 2026 01:12 PM
	private static string FormatFirstCaught(long unixTime)
	{
		if (unixTime <= 0) return "—"; // save cũ (trước bản cập nhật real-time) không có mốc giờ thật

		var dt = DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
		bool isVi = SettingsManager.Instance.Language == "vi";
		return isVi
			? dt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
			: dt.ToString("MMM d, yyyy hh:mm tt", CultureInfo.InvariantCulture);
	}

	// ── Button handlers ───────────────────────────────────────────────────────

	private void OnBack()
	{
		Hide();
		var menuScreen = GetTree().Root.FindChild("MenuScreen", true, false) as MenuScreen;
		menuScreen?.Show(isFirstLaunch: false);
	}
}
