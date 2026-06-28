using Godot;
using System;

public partial class Utils : Node
{
	public static float GetFloorY(float x, float z)
	{
		// Công thức giống SwimController — nhất quán toàn game
		float wave = Mathf.Sin(x * 0.08f) * Mathf.Cos(z * 0.06f) * 0.3f
			   + Mathf.Sin(x * 0.2f + z * 0.15f) * 0.1f;
		return -10f - Mathf.Abs(z) * 0.1405f + wave; // giữ slope -12° + gợn nhẹ
	}
}
