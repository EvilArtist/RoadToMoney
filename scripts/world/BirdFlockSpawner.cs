using Godot;

public partial class BirdFlockSpawner : Node3D
{
	[Export] public PackedScene BirdScene; // tạo BirdScene.tscn: Node3D + script Bird.cs
	[Export] public int    FlockSize        = 7;
	[Export] public float  FlightHeight     = 14f;
	[Export] public float  FlightSpeed      = 8f;
	[Export] public float  SpawnDistance    = 40; // bán kính spawn quanh player
	[Export] public float  VSpacing         = 7f;
	[Export] public float  MinSpawnInterval = 25f;
	[Export] public float  MaxSpawnInterval = 50f;

	[Export] public float TravelDistance   = 600f; // đủ xa để vượt FadeEndDistance(500) trước khi despawn
	[Export] public float OverheadLateral  = 15f;  // mode "trên đỉnh đầu" — lệch ngang
	[Export] public float OverheadZRange   = 20f;  // mode "trên đỉnh đầu" — lệch dọc Z
	[Export] public float SideFixedZMin    = 250f; // mode "bên màn hình" — Z cố định
	[Export] public float SideFixedZMax    = 300f;
	[Export] public float SideStartXOffset = 60f;  // mode "bên màn hình" — điểm bắt đầu từ mép

	private float _spawnTimer;
	private bool  _activePeriod;
	private Node3D _player;

	public override void _Ready()
	{
		EventBus.Instance.DayPeriodChanged += OnPeriodChanged;
		_activePeriod = IsActivePeriod(DayNightManager.Instance.CurrentPeriod);
		_player = GetTree().Root.FindChild("Player", true, false) as Node3D;
		ResetTimer();

		SpawnFlock();
	}

	private bool IsActivePeriod(DayNightManager.Period p) =>
		p == DayNightManager.Period.Morning || p == DayNightManager.Period.Afternoon;

	private void OnPeriodChanged(DayNightManager.Period p) => _activePeriod = IsActivePeriod(p);

	public override void _Process(double delta)
	{
		if (!_activePeriod || BirdScene == null) return;
		if (GameManager.Instance.CurrentState == GameManager.GameState.Diving) return;

		_spawnTimer -= (float)delta;
		if (_spawnTimer <= 0f)
		{
			SpawnFlock();
			ResetTimer();
		}
	}

	private void ResetTimer() => _spawnTimer = (float)GD.RandRange(MinSpawnInterval, MaxSpawnInterval);

	private void SpawnFlock()
	{
		Vector3 playerPos = _player != null ? _player.GlobalPosition : Vector3.Zero;
		Vector3 origin = new Vector3(playerPos.X, 0f, playerPos.Z);

		bool alongZ = GD.Randf() < 0.5f;
		Vector3 startPos, dir;

		if (alongZ)
		{
			// Xuất hiện gần trên đỉnh đầu, bay dọc theo Z (ra xa/lại gần dần)
			float lateral = (float)GD.RandRange(-OverheadLateral, OverheadLateral);
			float startZ  = (float)GD.RandRange(-OverheadZRange, OverheadZRange);
			float dirZ    = GD.Randf() < 0.5f ? 1f : -1f;

			startPos = origin + new Vector3(lateral, FlightHeight, startZ);
			dir = new Vector3(0f, 0f, dirZ);
		}
		else
		{
			// Xuất hiện từ 1 bên màn hình, Z cố định 150-250, bay ngang theo X
			float fixedZ = (float)GD.RandRange(SideFixedZMin, SideFixedZMax);
			float zSign  = GD.Randf() < 0.5f ? 1f : -1f;
			float dirX   = GD.Randf() < 0.5f ? 1f : -1f;
			float startX = -dirX * SideStartXOffset;

			startPos = origin + new Vector3(startX, FlightHeight, zSign * fixedZ);
			dir = new Vector3(dirX, 0f, 0f);
		}

		var formation = new Node3D { Name = "BirdFormation" };
		GetTree().Root.AddChild(formation);
		formation.GlobalPosition = startPos;

		Vector3 right = dir.Cross(Vector3.Up).Normalized();
		for (int i = 0; i < FlockSize; i++)
		{
			var bird = BirdScene.Instantiate<Bird>();
			formation.AddChild(bird);

			int row = (i + 1) / 2;
			float side = (i % 2 == 0) ? 1f : -1f;
			Vector3 offset = i == 0
				? Vector3.Zero
				: right * side * row * VSpacing - dir * row * VSpacing * 0.8f;

			bird.Position = offset;
			bird.LookAt(bird.GlobalPosition + dir, Vector3.Up);
		}

		var tween = formation.CreateTween();
		float duration = TravelDistance / FlightSpeed;
		tween.TweenProperty(formation, "global_position", startPos + dir * TravelDistance, duration);
		tween.TweenCallback(Callable.From(formation.QueueFree));
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
			EventBus.Instance.DayPeriodChanged -= OnPeriodChanged;
	}
}
