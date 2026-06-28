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

		SpawnProps(CoralScene,   CoralGroupCount,   null,        0f,    false);
		SpawnProps(RockScene,    RockGroupCount,    _rockNoise,    -0.05f, true);
		SpawnProps(SeaweedScene, SeaweedGroupCount, _seaweedNoise,  0.05f, false);
		SpawnProps(GrassScene,        GrassGroupCount,        _grassNoise,   0.05f, false);
		SpawnProps(SeabedDetailScene, SeabedDetailGroupCount, null,         0f,    false);
	}

	private void SpawnProps(string scenePath, int count, FastNoiseLite noiseField, float threshold, bool invert)
	{
		var scene = GD.Load<PackedScene>(scenePath);
		if (scene == null)
		{
			return;
		}

		int spawned = 0;
		int totalAttempts = 0;
		int maxTotalAttempts = count * MaxAttemptsPerProp;

		while (spawned < count && totalAttempts < maxTotalAttempts)
		{
			totalAttempts++;

			float x = _rng.RandfRange(-400f, 400f);
			float z = _rng.RandfRange(-400f, 400f);

			if (noiseField != null)
			{
				float n = noiseField.GetNoise2D(x, z);
				bool reject = invert ? (n > threshold) : (n < threshold);
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

			var prop = scene.Instantiate<Node3D>();
			prop.Position = new Vector3(x, floorY, z);
			prop.RotateY(_rng.RandfRange(0f, Mathf.Pi * 2f));

			float scale = _rng.RandfRange(0.8f, 1.3f);
			prop.Scale   = Vector3.One * scale;

			AddChild(prop);
			CreatureSpawner.PlayFirstAnimation(prop);

			_occupied.Add(pos2D);
			spawned++;
		}
	}
}
