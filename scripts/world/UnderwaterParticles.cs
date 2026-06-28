using Godot;

public partial class UnderwaterParticles : Node3D
{
	[Export]
	public Node3D Player;
	private GpuParticles3D _particles;
	private ParticleProcessMaterial _material;
	private GpuParticles3D _planktoms;


	public override void _Ready()
	{
		_particles = GetNode<GpuParticles3D>("DustParticles");
		_planktoms = GetNode<GpuParticles3D>("PlanktonParticles");
		_material =
			_particles.ProcessMaterial
			as ParticleProcessMaterial;
	}
	public override void _Process(double delta)
	{
		if (Player == null)
			return;

		GlobalPosition = Player.GlobalPosition;

		float depth = -Player.GlobalPosition.Y;

		// Trên mặt nước
		if (depth <= 0)
		{
			_particles.AmountRatio = 0f;
			_planktoms.AmountRatio = 0f;
			return;
		}
		
		// Tăng dần từ 0m -> 20m
		float density =
			Mathf.Clamp(depth / 20f, 0f, 1f);
		_particles.AmountRatio = density;
		_planktoms.AmountRatio = density;
		
		Vector3 current =
			OceanCurrentSystem.Instance
			?.GetCurrentVelocity()
			?? Vector3.Zero;
		if (current.Length() > 0.001f)
		{
			_material.Direction =
				current.Normalized();
		}
		float strength =
			OceanCurrentSystem.Instance
			?.CurrentStrength
			?? 0f;
		_material.InitialVelocityMin = strength * 0.5f;
		_material.InitialVelocityMax = strength;
	}
}
