using Godot;

/// <summary>
/// FloatingMessage — hiển thị text giữa màn hình, float lên rồi fade out.
/// 
/// CÁCH DÙNG:
///   // Trong bất kỳ script nào:
///   FloatingMessage.Show(GetTree(), "Bag is full!");
///   FloatingMessage.Show(GetTree(), "Bag is full!", new Color(1f, 0.4f, 0.3f));
///
/// SETUP:
///   Không cần setup gì thêm — tự tạo node vào root CanvasLayer khi cần.
/// </summary>
public static class FloatingMessage
{
	// ── Config ────────────────────────────────────────────────────────────────
	private const float FontSize      = 16f;
	private const float FloatDistance = 80f;   // pixels float lên
	private const float Duration      = 1.8f;  // giây tồn tại
	private const float FadeStart     = 0.6f;  // bắt đầu fade sau x giây

	private static readonly Color DefaultColor   = new Color(1f, 1f, 1f, 1f);
	private static readonly Color ColorBagFull   = new Color(1.0f, 0.38f, 0.28f, 1f);
	private static readonly Color OutlineColor   = new Color(0f, 0f, 0f, 0.85f);

	// ── Public API ────────────────────────────────────────────────────────────

	public static void ShowBagFull(SceneTree tree)
		=> Show(tree, TranslationServer.Translate("BAG_FULL"), ColorBagFull);

	public static void Show(SceneTree tree, string text, Color? color = null)
	{
		var node = new _FloatingLabel(text, color ?? DefaultColor);
		// Gắn vào root để luôn render trên cùng
		tree.Root.CallDeferred("add_child", node);
	}

	public static void ShowMiss(SceneTree tree)
	{
		Show(tree, TranslationServer.Translate("MISSED"), new Color(1f, 0.4f, 0.2f)); // cam đỏ
	}
}

/// <summary>
/// Internal node — tự destroy sau khi animation xong.
/// </summary>
public partial class _FloatingLabel : CanvasLayer
{
	private readonly string _text;
	private readonly Color  _color;

	private Label  _label;
	private float  _elapsed = 0f;
	private float  _startY;

	private const float FontSize      = 20f;
	private const float FloatDistance = 80f;
	private const float Duration      = 1.8f;
	private const float FadeStart     = 0.55f;

	public _FloatingLabel(string text, Color color)
	{
		_text  = text;
		_color = color;
		Layer  = 128; // render trên tất cả UI khác
	}

	public override void _Ready()
	{
		_label = new Label();
		_label.Text = _text;

		// ── Font size ─────────────────────────────────────────────────────
		_label.AddThemeFontSizeOverride("font_size", (int)FontSize);

		// ── Outline để dễ đọc trên mọi background ────────────────────────
		_label.AddThemeConstantOverride("outline_size", 3);
		_label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
		_label.AddThemeColorOverride("font_color", _color);

		// ── Căn giữa màn hình ─────────────────────────────────────────────
		_label.HorizontalAlignment = HorizontalAlignment.Center;
		_label.VerticalAlignment   = VerticalAlignment.Center;

		// Đặt vào Control để có thể position
		var anchor = new Control();
		anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		anchor.MouseFilter = Control.MouseFilterEnum.Ignore;

		// Label anchor giữa màn hình, offset lên -60px để không che HUD
		_label.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_label.AnchorLeft   = 0.5f;
		_label.AnchorRight  = 0.5f;
		_label.AnchorTop    = 0.45f;
		_label.AnchorBottom = 0.45f;
		_label.GrowHorizontal = Control.GrowDirection.Both;
		_label.MouseFilter    = Control.MouseFilterEnum.Ignore;

		// Lưu vị trí Y ban đầu để tính float
		_startY = 0f;
		_label.OffsetTop    = _startY;
		_label.OffsetBottom = _startY + 30f;

		anchor.AddChild(_label);
		AddChild(anchor);
	}

	public override void _Process(double delta)
	{
		_elapsed += (float)delta;

		// ── Float lên ────────────────────────────────────────────────────
		float progress = _elapsed / Duration;
		// Easing: nhanh lúc đầu, chậm dần
		float ease     = 1f - Mathf.Pow(1f - progress, 2f);
		float offsetY  = _startY - FloatDistance * ease;

		_label.OffsetTop    = offsetY;
		_label.OffsetBottom = offsetY + 30f;

		// ── Fade out ──────────────────────────────────────────────────────
		float alpha;
		if (_elapsed < FadeStart * Duration)
		{
			// Fade IN nhanh (0.1s đầu)
			alpha = Mathf.Min(_elapsed / 0.12f, 1f);
		}
		else
		{
			// Fade OUT
			float fadeProgress = (_elapsed - FadeStart * Duration) / ((1f - FadeStart) * Duration);
			alpha = 1f - Mathf.Clamp(fadeProgress, 0f, 1f);
		}

		_label.Modulate = new Color(1f, 1f, 1f, alpha);

		// ── Self-destroy ──────────────────────────────────────────────────
		if (_elapsed >= Duration)
			QueueFree();
	}
}
