using Godot;
using System.Collections.Generic;

/// <summary>
/// ThankYouScreen — màn hình "Lời tri ân" hiển thị dạng scroll crawl giống intro phim
/// (text trôi từ dưới lên trên, giữa màn hình, tốc độ chậm để người chơi đọc kịp).
///
/// Nội dung gồm: đoạn giới thiệu dự án + danh sách ekip (Ý tưởng/Phát triển — hardcode)
/// + danh sách Mô hình 3D và Âm thanh & Nhạc được đọc trực tiếp từ res://thank-you.txt
/// (định dạng "Tên: Tác giả" theo từng dòng, có 2 section "Model:" và "Music & Effect:").
/// Đọc từ file ngoài để cập nhật credit (thêm tác giả mới) không cần sửa code/scene.
/// </summary>
public partial class ThankYouScreen : CanvasLayer
{
	private const string CreditsFilePath    = "res://thank-you.txt";
	private const float  ScrollSpeedPxPerSec = 65f;   // tốc độ trôi chữ — chậm, dễ đọc
	private const float  TopPaddingPx        = 220f;  // khoảng trống sau khi cuộn hết để đọc xong dòng cuối

	private const string CreatorName = "Evil Artist";

	// TODO: thay bằng link donate thật (Ko-fi / Buy Me a Coffee / Patreon / PayPal.me / ...).
	private const string DonateUrl = "https://your-donate-link.example.com";

	// ── Node refs ────────────────────────────────────────────────────────────
	private Control       _crawlViewport;
	private VBoxContainer _crawlContent;
	private Button        _supportButton;
	private Button        _skipButton;

	private Tween _crawlTween;
	private Tween _supportPulseTween;

	// ── Màu chữ — cùng tinh thần các screen khác (trắng mờ theo độ quan trọng) ──
	private static readonly Color ColorTitle      = new Color(1f, 0.8392157f, 0.34901962f, 1f); // vàng cam, giống LoadingBar fill
	private static readonly Color ColorBody       = new Color(1f, 1f, 1f, 0.85f);
	private static readonly Color ColorHeader     = new Color(1f, 1f, 1f, 0.95f);
	private static readonly Color ColorRole       = new Color(0.5f, 0.8f, 0.75f, 1f);
	private static readonly Color ColorName       = new Color(1f, 1f, 1f, 0.85f);
	private static readonly Color ColorEnd        = new Color(1f, 1f, 1f, 0.4f);

	private static readonly Color SupportBg        = new Color(1f, 1f, 1f, 1f);          // nền trắng
	private static readonly Color SupportBgHover   = new Color(1f, 0.93f, 0.93f, 1f);     // trắng hơi ánh hồng khi hover
	private static readonly Color SupportText      = new Color(0.82f, 0.1f, 0.12f, 1f);   // chữ đỏ
	private static readonly Color SupportTextHover = new Color(0.95f, 0.05f, 0.08f, 1f);  // đỏ sáng hơn khi hover

	public override void _Ready()
	{
		_crawlViewport = GetNode<Control>       ("CrawlViewport");
		_crawlContent  = GetNode<VBoxContainer>  ("CrawlViewport/CrawlContent");
		_supportButton = GetNode<Button>         ("BottomBar/SupportButton");
		_skipButton    = GetNode<Button>         ("BottomBar/SkipButton");

		_supportButton.Pressed += OnSupport;
		_skipButton.Pressed    += OnBack;

		StyleSupportButton();
		CallDeferred(nameof(StartSupportPulse)); // chờ layout xong để pivot scale đúng tâm nút

		Visible = false;
	}

	// Nút "Support me" nổi bật: nền trắng, chữ đỏ đậm — tương phản mạnh với toàn bộ
	// theme tối của game để không ai lướt qua mà không thấy.
	private void StyleSupportButton()
	{
		var normal = new StyleBoxFlat
		{
			BgColor = SupportBg,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			ContentMarginLeft = 16, ContentMarginRight = 16,
			ContentMarginTop = 8, ContentMarginBottom = 8,
			ShadowSize = 6,
			ShadowColor = new Color(0.82f, 0.1f, 0.12f, 0.45f),
		};

		var hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = SupportBgHover;

		var pressed = (StyleBoxFlat)normal.Duplicate();
		pressed.BgColor = SupportBgHover;
		pressed.ShadowSize = 2;

		_supportButton.AddThemeStyleboxOverride("normal", normal);
		_supportButton.AddThemeStyleboxOverride("hover", hover);
		_supportButton.AddThemeStyleboxOverride("pressed", pressed);
		_supportButton.AddThemeStyleboxOverride("focus", normal);

		_supportButton.AddThemeColorOverride("font_color", SupportText);
		_supportButton.AddThemeColorOverride("font_hover_color", SupportTextHover);
		_supportButton.AddThemeColorOverride("font_pressed_color", SupportTextHover);
		_supportButton.AddThemeColorOverride("font_focus_color", SupportText);
	}

	// Pulse nhẹ liên tục (scale to/nhỏ) để hút mắt người chơi về nút donate
	// mà không gây chói/khó chịu — biên độ nhỏ (6%), nhịp chậm (1.2s/chu kỳ).
	private void StartSupportPulse()
	{
		_supportButton.PivotOffset = _supportButton.Size / 2f;

		_supportPulseTween?.Kill();
		_supportPulseTween = CreateTween().SetLoops();
		_supportPulseTween.TweenProperty(_supportButton, "scale", new Vector2(1.06f, 1.06f), 0.6f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_supportPulseTween.TweenProperty(_supportButton, "scale", Vector2.One, 0.6f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void OnSupport()
	{
		OS.ShellOpen(DonateUrl);
	}

	// ── Show / hide ───────────────────────────────────────────────────────────

	public new void Show()
	{
		BuildCrawl();

		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// Chờ 1 frame để layout tính xong kích thước nội dung rồi mới đo & chạy tween,
		// nếu không Size sẽ trả về giá trị cũ (0 hoặc của lần trước).
		CallDeferred(nameof(StartCrawl));
	}

	public new void Hide()
	{
		_crawlTween?.Kill();
		Visible = false;
	}

	private void OnBack()
	{
		Hide();
		var menuScreen = GetTree().Root.FindChild("MenuScreen", true, false) as MenuScreen;
		menuScreen?.Show(isFirstLaunch: false);
	}

	// ── Build nội dung crawl ───────────────────────────────────────────────────

	private void BuildCrawl()
	{
		foreach (var child in _crawlContent.GetChildren())
			child.QueueFree();

		AddTitle(Tr("THANKYOU_TITLE"));
		AddSpacer(60);

		AddParagraph(Tr("THANKYOU_INTRO"));
		AddSpacer(90);

		AddHeader(Tr("THANKYOU_CREDITS_HEADER"));
		AddSpacer(50);

		AddCreditRow(Tr("THANKYOU_IDEA"), CreatorName);
		AddSpacer(20);
		AddCreditRow(Tr("THANKYOU_DEV"), CreatorName);
		AddSpacer(70);

		var (models, sounds) = LoadCredits();

		if (models.Count > 0)
		{
			AddHeader(Tr("THANKYOU_MODEL_3D"));
			AddSpacer(30);
			foreach (var (name, author) in models)
				AddCreditRow(name, author);
			AddSpacer(70);
		}

		if (sounds.Count > 0)
		{
			AddHeader(Tr("THANKYOU_SOUND_MUSIC"));
			AddSpacer(30);
			foreach (var (name, author) in sounds)
				AddCreditRow(name, author);
			AddSpacer(70);
		}

		var endLabel = new Label
		{
			Text = Tr("THANKYOU_END"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		endLabel.Modulate = ColorEnd;
		endLabel.AddThemeFontSizeOverride("font_size", 26);
		_crawlContent.AddChild(endLabel);
	}

	private void AddTitle(string text)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.Modulate = ColorTitle;
		label.AddThemeFontSizeOverride("font_size", 64);
		_crawlContent.AddChild(label);
	}

	private void AddHeader(string text)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.Modulate = ColorHeader;
		label.AddThemeFontSizeOverride("font_size", 32);
		_crawlContent.AddChild(label);
	}

	private void AddParagraph(string text)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.Modulate = ColorBody;
		label.AddThemeFontSizeOverride("font_size", 26);
		label.AddThemeConstantOverride("line_spacing", 12);
		_crawlContent.AddChild(label);
	}

	// Một dòng credit kiểu phim: "Vai trò / Tên hạng mục" bên trái — "Tác giả" bên phải,
	// căn giữa khối lại để toàn bộ vẫn nằm trong crawl cuộn dọc theo trục giữa màn hình.
	private void AddCreditRow(string role, string name)
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		row.AddThemeConstantOverride("separation", 40);

		var roleLabel = new Label
		{
			Text = role,
			HorizontalAlignment = HorizontalAlignment.Right,
			CustomMinimumSize = new Vector2(420, 0),
		};
		roleLabel.Modulate = ColorRole;
		roleLabel.AddThemeFontSizeOverride("font_size", 22);

		var nameLabel = new Label
		{
			Text = name,
			HorizontalAlignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(420, 0),
		};
		nameLabel.Modulate = ColorName;
		nameLabel.AddThemeFontSizeOverride("font_size", 22);

		row.AddChild(roleLabel);
		row.AddChild(nameLabel);
		_crawlContent.AddChild(row);
	}

	private void AddSpacer(int height)
	{
		_crawlContent.AddChild(new Control { CustomMinimumSize = new Vector2(0, height) });
	}

	// ── Đọc thank-you.txt ────────────────────────────────────────────────────
	// Format file: 2 section "Music & Effect:" và "Model:", mỗi dòng con dạng "Tên: Tác giả".
	// Đọc trực tiếp từ file để thêm/sửa credit không cần build lại game.
	private (List<(string name, string author)> models, List<(string name, string author)> sounds) LoadCredits()
	{
		var models = new List<(string, string)>();
		var sounds = new List<(string, string)>();

		using var file = FileAccess.Open(CreditsFilePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushWarning($"ThankYouScreen: không mở được {CreditsFilePath}");
			return (models, sounds);
		}

		var section = "";
		while (!file.EofReached())
		{
			var rawLine = file.GetLine();
			var line = rawLine.Trim();

			if (line.Length == 0)
				continue;

			if (line.Equals("Model:", System.StringComparison.OrdinalIgnoreCase))
			{
				section = "model";
				continue;
			}
			if (line.StartsWith("Music & Effect", System.StringComparison.OrdinalIgnoreCase))
			{
				section = "sound";
				continue;
			}

			var sepIndex = line.IndexOf(':');
			if (sepIndex < 0)
				continue;

			var name   = line.Substring(0, sepIndex).Trim();
			var author = line.Substring(sepIndex + 1).Trim();
			if (name.Length == 0 || author.Length == 0)
				continue;

			if (section == "model")
				models.Add((name, author));
			else if (section == "sound")
				sounds.Add((name, author));
		}

		return (models, sounds);
	}

	// ── Animation cuộn chữ ───────────────────────────────────────────────────

	private void StartCrawl()
	{
		var viewportHeight = _crawlViewport.Size.Y;
		var contentHeight  = _crawlContent.Size.Y;

		float startY = viewportHeight;
		float endY   = -contentHeight - TopPaddingPx;

		_crawlContent.Position = new Vector2(_crawlContent.Position.X, startY);

		float distance = startY - endY;
		float duration = distance / ScrollSpeedPxPerSec;

		_crawlTween?.Kill();
		_crawlTween = CreateTween();
		_crawlTween.TweenProperty(_crawlContent, "position:y", endY, duration)
			.SetTrans(Tween.TransitionType.Linear);
		_crawlTween.Chain().TweenCallback(Callable.From(OnBack)); // hết crawl tự quay lại Menu
	}
}
