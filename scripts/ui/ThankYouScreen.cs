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

	// ── Node refs ────────────────────────────────────────────────────────────
	private Control       _crawlViewport;
	private VBoxContainer _crawlContent;
	private Button        _skipButton;

	private Tween _crawlTween;

	// ── Màu chữ — cùng tinh thần các screen khác (trắng mờ theo độ quan trọng) ──
	private static readonly Color ColorTitle      = new Color(1f, 0.8392157f, 0.34901962f, 1f); // vàng cam, giống LoadingBar fill
	private static readonly Color ColorBody       = new Color(1f, 1f, 1f, 0.85f);
	private static readonly Color ColorHeader     = new Color(1f, 1f, 1f, 0.95f);
	private static readonly Color ColorRole       = new Color(0.5f, 0.8f, 0.75f, 1f);
	private static readonly Color ColorName       = new Color(1f, 1f, 1f, 0.85f);
	private static readonly Color ColorEnd        = new Color(1f, 1f, 1f, 0.4f);

	public override void _Ready()
	{
		_crawlViewport = GetNode<Control>       ("CrawlViewport");
		_crawlContent  = GetNode<VBoxContainer>  ("CrawlViewport/CrawlContent");
		_skipButton    = GetNode<Button>         ("SkipButton");

		_skipButton.Pressed += OnBack;

		Visible = false;
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
