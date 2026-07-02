using Godot;
using System.Collections.Generic;

public partial class EnvironmentSpawner : Node3D
{
	[Export] public int CoralGroupCount   = 50;
	[Export] public int RockGroupCount    = 50;
	[Export] public int SeaweedGroupCount = 200;
	[Export] public int GrassGroupCount       = 150;
	[Export] public int SeabedDetailGroupCount = 80;

	[ExportGroup("Distribution")]
	[Export] public float MinSpacing         = 8.0f;
	[Export] public float NoiseFrequency     = 0.015f;
	[Export] public int   MaxAttemptsPerProp = 30;

	// Seabed nghieng +-8 deg quanh truc X (xem OceanRightFloor/OceanLeftFloor trong
	// OceanWorld.tscn va cong thuc Utils.GetFloorY: 0.1405 ~= tan(8deg)).
	// Right (z > 0) nghieng -8deg, Left (z < 0) nghieng +8deg.
	[Export] public float SeabedTiltDegrees = 8.0f;

	private Vector2 PlayerSpawnXZ   = Vector2.Zero;
	// Bán kính loại trừ — phải đủ lớn để chứa cả bounding radius của RockGroup/CoralGroup
	// (vì 1 group có nhiều mesh con rải quanh tâm, không chỉ 1 điểm).
	[Export] public float   SafeZoneRadius  = 25.0f;

	private RandomNumberGenerator _rng = new();
	private FastNoiseLite _seaweedNoise = new();
	private FastNoiseLite _rockNoise    = new();
	private FastNoiseLite _grassNoise = new();

	private List<Vector2> _occupied = new();

	private static readonly string CoralScene   = "res://scenes/props/CoralGroup.tscn";
	private static readonly string RockScene    = "res://scenes/props/RockGroup.tscn";
	private static readonly string SeaweedScene = "res://scenes/props/SeaweedGroup.tscn";
	private static readonly string GrassScene        = "res://scenes/props/GrassGroup.tscn";
	private static readonly string SeabedDetailScene = "res://scenes/props/SeabedDetailGroup.tscn";

	// Cau hinh 1 loai prop can spawn: scene, so luong, va tieu chi loc theo noise.
	private struct PropSpawnConfig
	{
		public string Scene;
		public int Count;
		public FastNoiseLite Noise;    // null = khong loc theo noise
		public float Threshold;
		public bool Invert;
	}

	public override void _Ready()
	{
		_rng.Randomize();

		_seaweedNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_seaweedNoise.Frequency = NoiseFrequency;
		_seaweedNoise.Seed      = _rng.RandiRange(0, 99999);

		_rockNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_rockNoise.Frequency = NoiseFrequency;
		_rockNoise.Seed      = _rng.RandiRange(0, 99999);

		_grassNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_grassNoise.Frequency = NoiseFrequency;
		_grassNoise.Seed      = _rng.RandiRange(0, 99999);

		// Đăng ký safe zone là 1 điểm "occupied" ảo với bán kính lớn ngay từ đầu,
		// để toàn bộ logic tránh-chồng-lấp (MinSpacing) tự động loại trừ khu vực này
		// cho mọi loại prop, không cần sửa riêng từng SpawnProps call.
		_occupied.Add(PlayerSpawnXZ);

		// Khai bao toan bo cau hinh prop can spawn trong 1 mang struct duy nhat.
		PropSpawnConfig[] propConfigs = new PropSpawnConfig[]
		{
			new PropSpawnConfig { Scene = CoralScene,        Count = CoralGroupCount,        Noise = null,          Threshold = 0f,     Invert = false },
			new PropSpawnConfig { Scene = RockScene,         Count = RockGroupCount,         Noise = _rockNoise,    Threshold = -0.05f, Invert = true  },
			new PropSpawnConfig { Scene = SeaweedScene,      Count = SeaweedGroupCount,      Noise = _seaweedNoise, Threshold = 0.05f,  Invert = false },
			new PropSpawnConfig { Scene = GrassScene,        Count = GrassGroupCount,        Noise = _grassNoise,   Threshold = 0.05f,  Invert = false },
			new PropSpawnConfig { Scene = SeabedDetailScene, Count = SeabedDetailGroupCount, Noise = null,          Threshold = 0f,     Invert = false },
		};

		foreach (var config in propConfigs)
			SpawnProps(config);
	}

	private void SpawnProps(PropSpawnConfig config)
	{
		var scene = GD.Load<PackedScene>(config.Scene);
		if (scene == null)
		{
			return;
		}

		int spawned = 0;
		int totalAttempts = 0;
		int maxTotalAttempts = config.Count * MaxAttemptsPerProp;

		while (spawned < config.Count && totalAttempts < maxTotalAttempts)
		{
			totalAttempts++;

			float x = _rng.RandfRange(-400f, 400f);
			float z = _rng.RandfRange(-400f, 400f);

			if (config.Noise != null)
			{
				float n = config.Noise.GetNoise2D(x, z);
				bool reject = config.Invert ? (n > config.Threshold) : (n < config.Threshold);
				if (reject) continue;
			}

			Vector2 pos2D = new Vector2(x, z);

			// Check riêng safe zone với bán kính RIÊNG (lớn hơn MinSpacing thường)
			// vì đây không phải tránh đè giữa 2 prop, mà là chặn cả 1 vùng quanh player.
			if (pos2D.DistanceSquaredTo(PlayerSpawnXZ) < SafeZoneRadius * SafeZoneRadius)
			{
				continue;
			}

			bool tooClose = false;
			foreach (var occ in _occupied)
			{
				if (occ.DistanceSquaredTo(pos2D) < MinSpacing * MinSpacing)
				{
					tooClose = true;
					break;
				}
			}
			if (tooClose) continue;

			float floorY = Utils.GetFloorY(x, z);

			// Nghieng prop theo dung do doc cua seabed tai vi tri z:
			// Right (z > 0) -> -SeabedTiltDegrees, Left (z < 0) -> +SeabedTiltDegrees,
			// khop voi rotation cua OceanRightFloor/OceanLeftFloor trong scene.
			float tiltAngle = -Mathf.Sign(z) * Mathf.DegToRad(SeabedTiltDegrees);
			float rotY   = _rng.RandfRange(0f, Mathf.Pi * 2f);
			float scale  = _rng.RandfRange(0.8f, 1.3f);

			// Yaw ap dung truoc (xoay quanh truc len cua chinh prop), sau do tilt
			// ap dung sau cung de "up" cua prop khop voi phap tuyen mat doc —
			// neu doi thu tu nhan, tilt se bi xoay theo yaw va sai huong.
			Basis tiltBasis = new Basis(Vector3.Right, tiltAngle);
			Basis yawBasis  = new Basis(Vector3.Up, rotY);
			Basis basis = (tiltBasis * yawBasis).Scaled(Vector3.One * scale);

			var prop = scene.Instantiate<Node3D>();
			prop.Transform = new Transform3D(basis, new Vector3(x, floorY, z));

			AddChild(prop);
			CreatureSpawner.PlayFirstAnimation(prop);

			_occupied.Add(pos2D);
			spawned++;
		}
	}
}
