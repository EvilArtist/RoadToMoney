using Godot;

public partial class CreatureAI : CharacterBody3D
{
	public enum CreatureState { Idle, Wander, Flee, School, Hiding, Stunned }

	[Export] public CreatureData Data;
	[Export] public float IdleTime    = 2f;
	[Export] public float WanderRange = 8f;

	public Vector3 Velocity3D { get; private set; } = Vector3.Zero;

	protected CreatureState _state     = CreatureState.Idle;
	protected float         _stateTimer = 0f;
	protected Vector3       _wanderTarget = Vector3.Zero;
	protected Node3D        _player = null;
	public CreatureState GetState() => _state;
	
	public override void _Ready()
	{
		// Tìm player trong scene
		_player = GetTree().Root.FindChild("Player", true, false) as Node3D;
		_wanderTarget = GlobalPosition;
		SetState(CreatureState.Idle);
	}

	public override void _PhysicsProcess(double delta)
	{
		// Throttle: chỉ update AI mỗi 3 frames
		if (Engine.GetPhysicsFrames() % 3 != 0) return;

		_stateTimer -= (float)delta * 3f;

		UpdateDetection();

		switch (_state)
		{
			case CreatureState.Idle:   ProcessIdle(delta);   break;
			case CreatureState.Wander: ProcessWander(delta); break;
			case CreatureState.Flee:   ProcessFlee(delta);   break;
			case CreatureState.Hiding: ProcessHiding(delta); break;
			case CreatureState.Stunned:                      break;
		}

		// Apply velocity
		Velocity = Velocity3D;
		MoveAndSlide();
	}

	// ── Detection ────────────────────────────────────────────────
	private void UpdateDetection()
	{
		if (_player == null || Data == null) return;

		float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

		if (dist < Data.FleeRadius && _state != CreatureState.Flee)
			SetState(CreatureState.Flee);
		else if (dist > Data.FleeRadius * 1.5f && _state == CreatureState.Flee)
			SetState(CreatureState.Wander);
	}

	// ── States ───────────────────────────────────────────────────
	private void ProcessIdle(double delta)
	{
		// Drift nhẹ
		Velocity3D = Velocity3D.Lerp(Vector3.Zero, 0.05f);

		if (_stateTimer <= 0f)
			SetState(CreatureState.Wander);
	}

	private void ProcessWander(double delta)
	{
		var dir = (_wanderTarget - GlobalPosition);

		if (dir.Length() < 0.5f || _stateTimer <= 0f)
		{
			// Chọn điểm wander mới
			var rng = new RandomNumberGenerator();
			rng.Randomize();
			_wanderTarget = GlobalPosition + new Vector3(
				rng.RandfRange(-WanderRange, WanderRange),
				rng.RandfRange(-2f, 2f),
				rng.RandfRange(-WanderRange, WanderRange)
			);
			SetState(CreatureState.Idle);
			return;
		}

		float speed = Data?.MoveSpeed ?? 3f;
		Velocity3D = Velocity3D.Lerp(dir.Normalized() * speed, 0.05f);

		// Xoay về hướng di chuyển
		if (Velocity3D.Length() > 0.1f)
			LookAt(GlobalPosition + Velocity3D, Vector3.Up);
	}

	private void ProcessFlee(double delta)
	{
		if (_player == null) return;

		var fleeDir = (GlobalPosition - _player.GlobalPosition).Normalized();
		float speed = Data?.FleeSpeed ?? 6f;
		Velocity3D = Velocity3D.Lerp(fleeDir * speed, 0.1f);

		if (Velocity3D.Length() > 0.1f)
			LookAt(GlobalPosition + Velocity3D, Vector3.Up);
	}

	private void ProcessHiding(double delta)
	{
		Velocity3D = Velocity3D.Lerp(Vector3.Zero, 0.1f);
	}

	// ── Helpers ──────────────────────────────────────────────────
	public void SetState(CreatureState newState)
	{
		_state = newState;
		switch (newState)
		{
			case CreatureState.Idle:   _stateTimer = IdleTime; break;
			case CreatureState.Wander: _stateTimer = 4f;       break;
			case CreatureState.Flee:   _stateTimer = 3f;       break;
			case CreatureState.Hiding: _stateTimer = 5f;       break;
		}
	}

	public void Stun(float duration)
	{
		SetState(CreatureState.Stunned);
		_stateTimer = duration;
	}

	public void ApplyFlockSteering(Vector3 steering, float speed, float delta)
	{
		if (_state == CreatureState.Flee || _state == CreatureState.Stunned) return;
		_state = CreatureState.School;

		// Giới hạn vertical — cá bơi ngang là chính
		steering.Y *= 0.2f;

		Velocity3D = Velocity3D.Lerp(steering.Normalized() * speed, delta * 0.5f);

		// Gravity nhẹ để không trôi lên
		Velocity3D += Vector3.Down * 0.5f * delta;

		if (Velocity3D.Length() > 0.1f)
		{
			// Chỉ LookAt theo hướng ngang, giữ cá thẳng đứng
			var flatVelocity = new Vector3(Velocity3D.X, 0f, Velocity3D.Z);
			if (flatVelocity.Length() > 0.1f)
			{
				var lookTarget = GlobalPosition + flatVelocity;
				LookAt(lookTarget, Vector3.Up);
			}
		}
	}
}
