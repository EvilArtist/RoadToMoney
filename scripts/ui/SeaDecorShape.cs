using Godot;
using System.Collections.Generic;

/// <summary>
/// Vẽ 1 trong 3 shape biển bằng code, không cần asset ảnh.
/// Dùng làm decor góc màn hình cho MenuScreen / các panel khác.
/// </summary>
public partial class SeaDecorShape : Control
{
	public enum Shape { Starfish, Shell, Coral }

	[Export] public Shape ShapeType = Shape.Starfish;
	[Export] public Color ShapeColor = new Color(0.96f, 0.78f, 0.45f, 0.85f);
	[Export] public float DrawSize = 48f;

	public override void _Ready()
	{
		QueueRedraw();
	}

	public override void _Draw()
	{
		switch (ShapeType)
		{
			case Shape.Starfish: DrawStarfish(); break;
			case Shape.Shell:    DrawShell();    break;
			case Shape.Coral:    DrawCoral();    break;
		}
	}

	private void DrawStarfish()
	{
		Vector2 center  = new Vector2(DrawSize, DrawSize);
		float   outerR  = DrawSize * 0.9f;
		float   innerR  = DrawSize * 0.38f;

		var points = new Vector2[10];
		for (int i = 0; i < 10; i++)
		{
			float angle = Mathf.DegToRad(-90f + i * 36f);
			float r     = (i % 2 == 0) ? outerR : innerR;
			points[i]   = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
		}
		DrawColoredPolygon(points, ShapeColor);

		for (int i = 0; i < 5; i++)
		{
			float angle = Mathf.DegToRad(-90f + i * 72f);
			Vector2 dot = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerR * 0.55f;
			DrawCircle(dot, DrawSize * 0.05f, ShapeColor.Darkened(0.25f));
		}
	}

	private void DrawShell()
	{
		Vector2 baseCenter = new Vector2(DrawSize, DrawSize * 1.6f);
		float   radius     = DrawSize * 0.95f;
		int     ribCount   = 7;

		var fanPoints = new List<Vector2> { baseCenter };
		for (int i = 0; i <= ribCount; i++)
		{
			float t     = (float)i / ribCount;
			float angle = Mathf.Lerp(Mathf.DegToRad(200f), Mathf.DegToRad(-20f), t);
			fanPoints.Add(baseCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
		}
		DrawColoredPolygon(fanPoints.ToArray(), ShapeColor);

		for (int i = 1; i < ribCount; i++)
		{
			float t     = (float)i / ribCount;
			float angle = Mathf.Lerp(Mathf.DegToRad(200f), Mathf.DegToRad(-20f), t);
			Vector2 tip = baseCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
			DrawLine(baseCenter, tip, ShapeColor.Darkened(0.3f), 1.5f);
		}
	}

	private void DrawCoral()
	{
		Vector2 root = new Vector2(DrawSize, DrawSize * 1.8f);
		DrawBranch(root, -90f, DrawSize * 0.9f, 3);
	}

	private void DrawBranch(Vector2 start, float angleDeg, float length, int depth)
	{
		if (depth <= 0 || length < 6f) return;

		Vector2 dir = new Vector2(Mathf.Cos(Mathf.DegToRad(angleDeg)), Mathf.Sin(Mathf.DegToRad(angleDeg)));
		Vector2 end = start + dir * length;
		DrawLine(start, end, ShapeColor, Mathf.Max(2f, depth * 1.5f));

		DrawBranch(end, angleDeg - 25f, length * 0.65f, depth - 1);
		DrawBranch(end, angleDeg + 25f, length * 0.65f, depth - 1);
	}
}
