using Godot;
using System.Collections.Generic;

// EnvironmentSpawner rai prop trong map.
//
// Coral / Rock / Grass / SeabedDetail: dung MultiMeshInstance3D - moi MODEL rieng
// (vd aurora_reef_coral, brown_coral_l, rock_1...) co 1 node MultiMeshInstance3D
// DUY NHAT trong toan scene, chua nhieu instance ben trong qua
// MultiMesh.SetInstanceTransform(). Day la cach MultiMesh thuc su giam draw call
// (1 draw call / model bat ke so luong instance). Cac file *_mm.tscn duoc tao boi
// scripts/tools/ExtractMultiMeshProps.cs (chay 1 lan trong Editor).
//
// Seaweed: giu nguyen Instantiate() Node3D nhu cu, vi SeaweedGroup.cs co script
// dieu khien sway animation rieng tung instance moi frame - MultiMesh khong ho tro
// dieu nay (chi co transform tinh, set 1 lan).
//
// Vi tri duoc tinh bang WORLD-SPACE tuyet doi: Utils.GetFloorY(x, z) da bao gom
// san slope nghieng cua seabed (cung cong thuc SwimController dung) nen "bam day
// bien" dung ngay ca o vung OceanRightFloor/OceanLeftFloor nghieng, khong can
// parent rieng vao 2 floor node do.
public partial class EnvironmentSpawner : Node3D
{
	public enum PropTier { Large, Medium, Small }

	private struct MultiMeshPropDef
	{
		public string Name;
		public string ScenePath;
		public PropTier Tier;
	}

	[ExportGroup("Coral")]
	// So luong RIENG cho moi tier - Large (landmark, to) phai it, Small (mushroom...)
	// co the nhieu hon vi nho va re hon ve hieu nang.
	[Export] public int CoralLargeCountPerModel  = 2;  // x4 model Large = 8 landmark coral toan map
	[Export] public int CoralMediumCountPerModel = 12; // x10 model Medium
	[Export] public int CoralSmallCountPerModel  = 20; // x1 model Small (mushroom_coral)

	[ExportGroup("Rock")]
	[Export] public int RockCountPerModel = 16; // x3 model

	[ExportGroup("Grass")]
	[Export] public int GrassCountPerModel = 60; // x3 model

	[ExportGroup("SeabedDetail")]
	[Export] public int SeabedDetailCountPerModel = 25; // x3 model

	[ExportGroup("Seaweed (giu Instantiate cu)")]
	[Export] public int SeaweedGroupCount = 200;

	[ExportGroup("Distribution")]
	[Export] public float MinSpacingSmall  = 4.0f;  // Grass/SeabedDetail/coral nho
	[Export] public float MinSpacingMedium = 8.0f;  // coral/rock vua
	[Export] public float MinSpacingLarge  = 30.0f; // coral landmark to
	[Export] public float NoiseFrequency     = 0.015f;
	[Export] public int   MaxAttemptsPerProp = 30;

	private Vector2 PlayerSpawnXZ = Vector2.Zero;
	[Export] public float SafeZoneRadius = 25.0f;

	[ExportGroup("World Bounds")]
	[Export] public float WorldHalfSizeX = 400f;
	[Export] public float WorldHalfSizeZ = 400f;

	private RandomNumberGenerator _rng = new();
	private FastNoiseLite _seaweedNoise = new();
	private FastNoiseLite _rockNoise    = new();
	private FastNoiseLite _grassNoise   = new();

	// Theo doi vi tri da chiem theo world-space (x, z) de check chong lap giua
	// MOI loai prop, ke ca khac MultiMesh hay Instantiate thuong.
	private List<(Vector2 pos, float radius)> _occupied = new();

	private const string MultiMeshDir = "res://scenes/props/multimesh/";

	private static readonly MultiMeshPropDef[] CoralDefs = new[]
	{
		// -- tu CoralGroup.tscn cu --
		new MultiMeshPropDef { Name = "coral",         ScenePath = MultiMeshDir + "coral_mm.tscn",         Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "coral_1",       ScenePath = MultiMeshDir + "coral_1_mm.tscn",       Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "coral_2",       ScenePath = MultiMeshDir + "coral_2_mm.tscn",       Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "brain_coral",    ScenePath = MultiMeshDir + "brain_coral_mm.tscn",   Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "brain_coral_2",  ScenePath = MultiMeshDir + "brain_coral_2_mm.tscn", Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "mushroom_coral", ScenePath = MultiMeshDir + "mushroom_coral_mm.tscn",Tier = PropTier.Small },

		// -- tu BigCoralGroup.tscn moi --
		new MultiMeshPropDef { Name = "aurora_reef_coral",         ScenePath = MultiMeshDir + "aurora_reef_coral_mm.tscn",         Tier = PropTier.Large },
		new MultiMeshPropDef { Name = "lazulight_coral",            ScenePath = MultiMeshDir + "lazulight_coral_mm.tscn",           Tier = PropTier.Large },
		new MultiMeshPropDef { Name = "rainbow_haven_reef_coral",    ScenePath = MultiMeshDir + "rainbow_haven_reef_coral_mm.tscn",  Tier = PropTier.Large },
		new MultiMeshPropDef { Name = "long_ledges_reef_community", ScenePath = MultiMeshDir + "long_ledges_reef_community_mm.tscn",Tier = PropTier.Large },
		new MultiMeshPropDef { Name = "brown_coral_l",               ScenePath = MultiMeshDir + "brown_coral_l_mm.tscn",             Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "porites_lutea_fresh",         ScenePath = MultiMeshDir + "porites_lutea_fresh_mm.tscn",       Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "pocillopora_meandrina",       ScenePath = MultiMeshDir + "pocillopora_meandrina_mm.tscn",     Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "staghorn_coral",              ScenePath = MultiMeshDir + "staghorn_coral_mm.tscn",            Tier = PropTier.Medium },
	};

	private static readonly MultiMeshPropDef[] RockDefs = new[]
	{
		new MultiMeshPropDef { Name = "rock",   ScenePath = MultiMeshDir + "rock_mm.tscn",   Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "rock_1", ScenePath = MultiMeshDir + "rock_1_mm.tscn", Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "rock_2", ScenePath = MultiMeshDir + "rock_2_mm.tscn", Tier = PropTier.Medium },
	};

	private static readonly MultiMeshPropDef[] GrassDefs = new[]
	{
		new MultiMeshPropDef { Name = "grass",        ScenePath = MultiMeshDir + "grass_mm.tscn",        Tier = PropTier.Small },
		new MultiMeshPropDef { Name = "grass_clover", ScenePath = MultiMeshDir + "grass_clover_mm.tscn", Tier = PropTier.Small },
		new MultiMeshPropDef { Name = "sea_sponge",   ScenePath = MultiMeshDir + "sea_sponge_mm.tscn",   Tier = PropTier.Small },
	};

	private static readonly MultiMeshPropDef[] SeabedDetailDefs = new[]
	{
		new MultiMeshPropDef { Name = "starfish", ScenePath = MultiMeshDir + "starfish_mm.tscn", Tier = PropTier.Small },
		new MultiMeshPropDef { Name = "urchin",   ScenePath = MultiMeshDir + "urchin_mm.tscn",   Tier = PropTier.Small },
		new MultiMeshPropDef { Name = "urchin2",  ScenePath = MultiMeshDir + "urchin2_mm.tscn",  Tier = PropTier.Small },
	};

	private static readonly string SeaweedScene = "res://scenes/props/SeaweedGroup.tscn";

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

		// Dang ky safe zone quanh player nhu 1 diem "occupied" ao ngay tu dau, de
		// logic tranh-chong-lap tu dong loai tru khu vuc nay cho moi loai prop.
		_occupied.Add((PlayerSpawnXZ, SafeZoneRadius));

		// Coral spawn TRUOC (Large tier chiem dien tich lon nhat) de cac loai
		// nho/dong hon tu nhien ne ra xung quanh thay vi nguoc lai.
		foreach (var def in CoralDefs)
		{
			int count = def.Tier switch
			{
				PropTier.Large  => CoralLargeCountPerModel,
				PropTier.Medium => CoralMediumCountPerModel,
				_                => CoralSmallCountPerModel,
			};
			SpawnMultiMeshProps(def, count, noise: null, threshold: 0f, invert: false);
		}

		foreach (var def in RockDefs)
		{
			SpawnMultiMeshProps(def, RockCountPerModel, _rockNoise, threshold: -0.05f, invert: true);
		}

		foreach (var def in GrassDefs)
		{
			SpawnMultiMeshProps(def, GrassCountPerModel, _grassNoise, threshold: 0.05f, invert: false);
		}

		foreach (var def in SeabedDetailDefs)
		{
			SpawnMultiMeshProps(def, SeabedDetailCountPerModel, noise: null, threshold: 0f, invert: false);
		}

		SpawnSeaweedInstantiate();
	}

	private float SpacingForTier(PropTier tier) => tier switch
	{
		PropTier.Large  => MinSpacingLarge,
		PropTier.Medium => MinSpacingMedium,
		_                => MinSpacingSmall,
	};

	private void SpawnMultiMeshProps(MultiMeshPropDef def, int count, FastNoiseLite noise, float threshold, bool invert)
	{
		var scene = GD.Load<PackedScene>(def.ScenePath);
		if (scene == null)
		{
			GD.PrintErr($"[EnvironmentSpawner] Khong load duoc scene MultiMesh: {def.ScenePath} " +
				"(da chay ExtractMultiMeshProps trong Editor chua?)");
			return;
		}

		var mmInstance = scene.Instantiate<MultiMeshInstance3D>();
		AddChild(mmInstance);

		if (mmInstance.Multimesh == null)
		{
			GD.PrintErr($"[EnvironmentSpawner] Scene khong co Multimesh hop le: {def.ScenePath}");
			mmInstance.QueueFree();
			return;
		}

		// Multimesh resource co the dang duoc Godot cache/share theo path -> duplicate
		// de moi model co transform list doc lap, tranh ghi de lan nhau.
		var multiMesh = (MultiMesh)mmInstance.Multimesh.Duplicate();
		mmInstance.Multimesh = multiMesh;

		float spacing = SpacingForTier(def.Tier);
		var transforms = new List<Transform3D>(count);

		int spawned = 0;
		int totalAttempts = 0;
		int maxTotalAttempts = count * MaxAttemptsPerProp;

		while (spawned < count && totalAttempts < maxTotalAttempts)
		{
			totalAttempts++;

			float x = _rng.RandfRange(-WorldHalfSizeX, WorldHalfSizeX);
			float z = _rng.RandfRange(-WorldHalfSizeZ, WorldHalfSizeZ);

			if (noise != null)
			{
				float n = noise.GetNoise2D(x, z);
				bool reject = invert ? (n > threshold) : (n < threshold);
				if (reject) continue;
			}

			Vector2 pos2D = new Vector2(x, z);

			bool tooClose = false;
			foreach (var occ in _occupied)
			{
				float minDist = Mathf.Max(spacing, occ.radius);
				if (occ.pos.DistanceSquaredTo(pos2D) < minDist * minDist)
				{
					tooClose = true;
					break;
				}
			}
			if (tooClose) continue;

			float worldY = Utils.GetFloorY(x, z);
			Vector3 worldPos = new Vector3(x, worldY, z);

			float rotY = _rng.RandfRange(0f, Mathf.Pi * 2f);
			float scale = _rng.RandfRange(0.8f, 1.3f);
			var basis = Basis.FromEuler(new Vector3(0f, rotY, 0f)).Scaled(Vector3.One * scale);

			transforms.Add(new Transform3D(basis, worldPos));

			_occupied.Add((pos2D, spacing * 0.5f));
			spawned++;
		}

		multiMesh.InstanceCount = transforms.Count;
		for (int i = 0; i < transforms.Count; i++)
		{
			multiMesh.SetInstanceTransform(i, transforms[i]);
		}

		if (spawned < count)
		{
			GD.Print($"[EnvironmentSpawner] {def.Name}: chi spawn duoc {spawned}/{count} " +
				"(het luot thu, co the do mat do qua day hoac vung hop le qua nho).");
		}
	}

	// Seaweed giu nguyen logic cu (Instantiate Node3D), vi SeaweedGroup.cs can
	// _Process() rieng cho tung instance de sway animation - khong tuong thich MultiMesh.
	private void SpawnSeaweedInstantiate()
	{
		var scene = GD.Load<PackedScene>(SeaweedScene);
		if (scene == null)
		{
			GD.PrintErr($"[EnvironmentSpawner] Khong load duoc scene: {SeaweedScene}");
			return;
		}

		int spawned = 0;
		int totalAttempts = 0;
		int maxTotalAttempts = SeaweedGroupCount * MaxAttemptsPerProp;

		while (spawned < SeaweedGroupCount && totalAttempts < maxTotalAttempts)
		{
			totalAttempts++;

			float x = _rng.RandfRange(-WorldHalfSizeX, WorldHalfSizeX);
			float z = _rng.RandfRange(-WorldHalfSizeZ, WorldHalfSizeZ);

			float n = _seaweedNoise.GetNoise2D(x, z);
			if (n < 0.05f) continue;

			Vector2 pos2D = new Vector2(x, z);

			bool tooClose = false;
			foreach (var occ in _occupied)
			{
				float minDist = Mathf.Max(MinSpacingSmall, occ.radius);
				if (occ.pos.DistanceSquaredTo(pos2D) < minDist * minDist)
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
			prop.Scale = Vector3.One * scale;

			AddChild(prop);
			CreatureSpawner.PlayFirstAnimation(prop);

			_occupied.Add((pos2D, MinSpacingSmall * 0.5f));
			spawned++;
		}

		if (spawned < SeaweedGroupCount)
		{
			GD.Print($"[EnvironmentSpawner] Seaweed: chi spawn duoc {spawned}/{SeaweedGroupCount}.");
		}
	}
}
