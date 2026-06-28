using Godot;

public partial class ResourceMove : Node3D
{
	public float Speed;
	public Vector3 Direction;
	private Vector3 _velocity;
	private Vector3 _desiredDirection;

	[Export] public float TurnSpeed = 2.5f;
	[Export] public float SteeringStrength = 2.0f;
	public float WanderRadius = 10f;

	private float _fearRadius = 5f;
	private bool  _canFlee    = false;
	private float _fleeSpeed  = 4f;

	private Vector3 _spawnPosition;
	private float _turnTimer;
	private float _turnInterval;
	private Node3D _player;
	private bool _isFleeing = false;

	// ── Miss burst ────────────────────────────────────────────────────────────
	private bool  _isBursting    = false;
	private float _burstTimer    = 0f;
	private const float BurstDuration  = 2.0f;  // giây tăng tốc sau khi bị miss
	private const float BurstSpeedMult = 20f;  // nhân tốc độ x3

	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_rng.Randomize();
		_spawnPosition = GlobalPosition;
		_turnInterval  = _rng.RandfRange(20f, 30f);
		_turnTimer     = _turnInterval;
		_player        = GetTree().Root.FindChild("Player", true, false) as Node3D;
		_desiredDirection = Direction;
		_velocity = Direction * Speed;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// ── Tick burst timer ─────────────────────────────────────────────
		if (_isBursting)
		{
			_burstTimer -= dt;
			if (_burstTimer <= 0f)
				_isBursting = false;
		}

		// ── Fear flee (proximity) ─────────────────────────────────────────
		if (_canFlee && _player != null)
		{
			float distToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);

			if (distToPlayer < _fearRadius)
			{
				_isFleeing = true;
				Direction  = (GlobalPosition - _player.GlobalPosition).Normalized();
				Direction.Y *= 0.3f;
			}
			else if (!_isBursting)
			{
				_isFleeing = false;
			}
			// Nếu đang burst thì giữ _isFleeing = true để dùng flee direction
		}

		if (!_isFleeing && !_isBursting)
		{
			_turnTimer -= dt;

			if (_turnTimer <= 0f)
			{
				ChooseNewDirection();

				_turnInterval = _rng.RandfRange(2f, 5f);
				_turnTimer = _turnInterval;
			}

			// Wander noise liên tục
			_desiredDirection += new Vector3( 0, _rng.RandfRange(-0.05f, 0.05f), 0 ) * dt;

			_desiredDirection =
				_desiredDirection.Normalized();

			Direction =
				Direction.Lerp(
					_desiredDirection,
					SteeringStrength * dt
				).Normalized();

			_velocity =
				_velocity.Lerp(
					Direction * Speed,
					SteeringStrength * dt
				);

			GlobalPosition += _velocity * dt;

			KeepInsideArea();
		}
		else
		{
			// Flee hoặc burst — dùng fleeSpeed (x BurstSpeedMult nếu burst)
			float currentSpeed = _isBursting
				? _fleeSpeed * BurstSpeedMult
				: _fleeSpeed;

			GlobalPosition += Direction * currentSpeed * dt;
		}

		if (_velocity.LengthSquared() > 0.01f)
		{

			Basis targetBasis =
				Basis.LookingAt(
					_velocity.Normalized()
				).Orthonormalized();

			Basis currentBasis =
				Basis.Orthonormalized();

			Basis =
				currentBasis.Slerp(
					targetBasis,
					TurnSpeed * dt
				);
		}
	}

	/// <summary>
	/// Gọi từ CollectionInteraction khi player miss.
	/// Con hải sản giật mình bơi thẳng ra xa trong BurstDuration giây.
	/// </summary>
	public void OnMissed(Vector3 playerPosition)
	{
		// Hướng bơi = ra xa player + thêm chút random để không bơi thẳng ra
		Vector3 away = (GlobalPosition - playerPosition).Normalized();
		away += new Vector3(
			_rng.RandfRange(-0.3f, 0.3f),
			_rng.RandfRange(-0.1f, 0.2f), // bay nhẹ lên
			_rng.RandfRange(-0.3f, 0.3f)
		);
		Direction  = away.Normalized();
		_isBursting = true;
		_isFleeing  = true;
		_burstTimer = BurstDuration;

		// Reset wander timer để không bẻ hướng ngay sau burst
		_turnTimer = BurstDuration + _rng.RandfRange(1f, 2f);

		SpawnBubbleBurst(Direction);
	}

	/// <summary>
	/// Spawn 8 bubble MeshInstance3D tại vị trí hải sản, mỗi cái tự bay lên và fade.
	/// Dùng MeshInstance3D thay GpuParticles3D để tránh frustum-cull khi tạo bằng code.
	/// </summary>
	private void SpawnBubbleBurst(Vector3 swimDirection)
	{
		const int   BubbleCount    = 8;
		const float BubbleLifetime = 1.0f;

		// Shared material cho tất cả bubble
		var mat          = new StandardMaterial3D();
		mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.AlbedoColor  = new Color(0.75f, 0.93f, 1.0f, 0.8f);
		mat.EmissionEnabled          = true;
		mat.Emission                 = new Color(0.4f, 0.8f, 1.0f);
		mat.EmissionEnergyMultiplier = 0.25f;
		mat.BillboardMode            = BaseMaterial3D.BillboardModeEnum.Enabled;

		var mesh    = new SphereMesh();
		mesh.Radius = 0.06f;
		mesh.Height = 0.12f;
		mesh.RadialSegments = 4;
		mesh.Rings  = 2;
		mesh.Material = mat;

		var scene   = GetTree();
		var root    = scene.Root;

		for (int i = 0; i < BubbleCount; i++)
		{
			// Velocity: ngược hướng bơi + spread ngẫu nhiên + nổi lên
			Vector3 vel = -swimDirection * _rng.RandfRange(1.5f, 3.5f)
				+ new Vector3(
					_rng.RandfRange(-1.0f, 1.0f),
					_rng.RandfRange( 0.5f, 2.0f), // luôn nổi lên
					_rng.RandfRange(-1.0f, 1.0f));

			float scale    = _rng.RandfRange(0.5f, 1.4f);
			Vector3 startPos = GlobalPosition + new Vector3(
				_rng.RandfRange(-0.15f, 0.15f),
				_rng.RandfRange(-0.15f, 0.15f),
				_rng.RandfRange(-0.15f, 0.15f));

			// Mỗi bubble cần material riêng để tween alpha độc lập
			var bubbleMat          = mat.Duplicate() as StandardMaterial3D;
			var bubbleMesh         = mesh.Duplicate() as SphereMesh;
			bubbleMesh.Material    = bubbleMat;

			var bubble       = new MeshInstance3D();
			bubble.Mesh      = bubbleMesh;
			bubble.Scale     = Vector3.One * scale * 0.3f;

			// Add vào root để GlobalPosition luôn available và không bị cull theo parent
			root.AddChild(bubble);
			bubble.GlobalPosition = startPos;

			// Animate: tween position + tween albedo alpha về 0
			var tween = bubble.CreateTween();
			tween.SetParallel(true);
			tween.TweenProperty(bubble, "global_position",
				startPos + vel * BubbleLifetime,
				BubbleLifetime)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.Out);
			// Fade: tween albedo_color alpha trên material riêng của bubble này
			tween.TweenProperty(bubbleMat, "albedo_color",
				new Color(0.75f, 0.93f, 1.0f, 0f),
				BubbleLifetime)
				.SetTrans(Tween.TransitionType.Linear);
			tween.SetParallel(false);
			tween.TweenCallback(Callable.From(bubble.QueueFree));
		}
	}

	private void ChooseNewDirection()
	{
		_desiredDirection = new Vector3(
			_rng.RandfRange(-1f, 1f),
			_rng.RandfRange(-0.15f, 0.15f),
			_rng.RandfRange(-1f, 1f)
		).Normalized();
	}

	private void KeepInsideArea()
	{
		Vector3 offset = GlobalPosition - _spawnPosition;
		if (offset.Length() > WanderRadius)
			_desiredDirection = (-offset).Normalized();
	}

	public void Configure(
		float speed,
		Vector3 direction,
		float wanderRadius,
		float turnMin,
		float turnMax,
		bool  canFlee    = false,
		float fearRadius = 5f,
		float fleeSpeed  = 4f)
	{
		Speed        = speed;
		Direction    = direction;
		WanderRadius = wanderRadius;
		_turnInterval = _rng.RandfRange(turnMin, turnMax);
		_turnTimer    = _turnInterval;
		_canFlee     = canFlee;
		_fearRadius  = fearRadius;
		_fleeSpeed   = fleeSpeed;
	}
}
