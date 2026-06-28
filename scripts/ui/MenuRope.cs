using Godot;

/// <summary>
/// Vẽ 2 đoạn dây ngắn (rope tick) ở khoảng trống giữa mỗi cặp nút liên tiếp
/// trong ButtonList — theo đúng style ảnh mẫu (dây chỉ hiện ở phần gap).
/// </summary>
public partial class MenuRope : Node2D
{
	[Export] public NodePath ButtonListPath;
	[Export] public Color    RopeColor = new Color(0.29f, 0.20f, 0.15f);
	[Export] public float    RopeWidth = 3f;
	[Export] public float    EdgeInset = 22f;

	private VBoxContainer _buttonList;

	public override void _Ready()
	{
		_buttonList = GetNode<VBoxContainer>(ButtonListPath);
		_buttonList.Resized += () => QueueRedraw();
		CallDeferred("queue_redraw");
	}

	public override void _Draw()
	{
		if (_buttonList == null || _buttonList.GetChildCount() < 2) return;

		for (int i = 0; i < _buttonList.GetChildCount() - 1; i++)
		{
			var top    = _buttonList.GetChild<Control>(i);
			var bottom = _buttonList.GetChild<Control>(i + 1);

			Vector2 topGlobalPos    = top.GetGlobalRect().Position;
			Vector2 bottomGlobalPos = bottom.GetGlobalRect().Position;

			float topBottomY = topGlobalPos.Y + top.Size.Y;
			float bottomTopY = bottomGlobalPos.Y;
			float leftX      = topGlobalPos.X + EdgeInset;
			float rightX     = topGlobalPos.X + top.Size.X - EdgeInset;

			Vector2 leftTop     = ToLocal(new Vector2(leftX, topBottomY));
			Vector2 leftBottom  = ToLocal(new Vector2(leftX, bottomTopY));
			Vector2 rightTop    = ToLocal(new Vector2(rightX, topBottomY));
			Vector2 rightBottom = ToLocal(new Vector2(rightX, bottomTopY));

			DrawLine(leftTop, leftBottom, RopeColor, RopeWidth);
			DrawLine(rightTop, rightBottom, RopeColor, RopeWidth);
		}
	}
}
