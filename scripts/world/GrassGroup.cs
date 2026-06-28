using Godot;
using System.Collections.Generic;

public partial class GrassGroup : Node3D
{
	private readonly List<Node3D> _grasses = new();
	private readonly Dictionary<Node3D, float> _phaseOffsets = new();

	[Export]
	public float MaxAngle = 3f;   // nhỏ hơn seaweed (8f) — cỏ ngắn, sway ít hơn

	[Export]
	public float Speed = 1.5f;    // nhanh hơn chút cho cảm giác cỏ mảnh

	public override void _Ready()
	{
		foreach (Node child in GetChildren())
		{
			if (child is Node3D grass)
			{
				_grasses.Add(grass);
				_phaseOffsets[grass] = GD.Randf() * Mathf.Tau;
			}
		}
	}

	public override void _Process(double delta)
	{
		float strength = 1f;
		if (OceanCurrentSystem.Instance != null)
			strength = OceanCurrentSystem.Instance.CurrentStrength;

		float time = Time.GetTicksMsec() * 0.001f;

		foreach (var grass in _grasses)
		{
			float phase = _phaseOffsets[grass];
			float sway = Mathf.Sin(time * Speed + phase);
			grass.RotationDegrees = new Vector3(0, 0, sway * MaxAngle * strength);
		}
	}
}
