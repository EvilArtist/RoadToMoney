using Godot;
using System.Collections.Generic;

public partial class SeaweedGroup : Node3D
{
	private readonly List<Node3D> _seaweeds = new();

	private readonly Dictionary<Node3D, float> _phaseOffsets = new();

	[Export]
	public float MaxAngle = 8f;

	[Export]
	public float Speed = 1f;

	public override void _Ready()
	{
		foreach (Node child in GetChildren())
		{
			if (child is Node3D seaweed)
			{
				_seaweeds.Add(seaweed);

				_phaseOffsets[seaweed] =
					GD.Randf() * Mathf.Tau;
			}
		}
	}

	public override void _Process(double delta)
	{
		float strength = 1f;

		if (OceanCurrentSystem.Instance != null)
			strength =
				OceanCurrentSystem.Instance.CurrentStrength;

		float time =
			Time.GetTicksMsec() * 0.001f;

		foreach (var seaweed in _seaweeds)
		{
			float phase =
				_phaseOffsets[seaweed];

			float sway =
				Mathf.Sin(
					time * Speed + phase
				);

			seaweed.RotationDegrees =
				new Vector3(
					0,
					0,
					sway * MaxAngle * strength
				);
		}
	}
}
