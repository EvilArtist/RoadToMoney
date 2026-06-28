using Godot;

public partial class OceanCurrentSystem : Node3D
{
	public static OceanCurrentSystem Instance { get; private set; }

	[Export]
	public Vector3 CurrentDirection = Vector3.Right;

	[Export]
	public float CurrentStrength = 0.2f;

	public override void _Ready()
	{
		Instance = this;

		CurrentDirection = CurrentDirection.Normalized();
	}

	public Vector3 GetCurrentVelocity()
	{
		return CurrentDirection * CurrentStrength;
	}
}
