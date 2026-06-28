using Godot;

public partial class BubbleEmitter : GpuParticles3D
{
	private CharacterBody3D _player;
	private float _timer;

	private RandomNumberGenerator _rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		_rng.Randomize();
 		_player = GetNode<CharacterBody3D>("../..");
		ScheduleNextBreath();
	}

	public override void _Process(double delta)
	{
		_timer -= (float)delta;

		if (_timer <= 0f)
		{
			if (IsUnderwater())
			{
				EmitBreath();
			}

			ScheduleNextBreath();
		}
	}
	private void EmitBreath()
	{
		Amount = _rng.RandiRange(10, 25);

		Restart();
	}
	
	private bool IsUnderwater()
	{
		if (_player == null)
			return false;

		return _player.GlobalPosition.Y < 0f;
	}

	private void ScheduleNextBreath()
	{
		_timer = _rng.RandfRange(2.5f, 5.0f);
	}
}
