using Godot;
using System.Collections.Generic;

// EnvironmentSpawner rai prop trong map.
//
// Fix v2:
// - Spawn ASYNC: trai ra nhieu frame qua _Process() thay vi block _Ready()
//   (500+ node trong 1 frame gay freeze/hitch ro ret khi load map)
// - Bo random scale cho MultiMesh: scale da embed vao Mesh transform khi
//   ExtractMultiMeshProps chay trong editor, khong nhan them 0.8-1.3 nua
//   (truoc do: scale 8 trong scene x 1.3 = 10.4, coral to bat thuong)
// - Seaweed giu scale nho (0.3-0.6) vi SeaweedGroup.tscn chua nhieu cay
//   da duoc dat scale hop ly trong scene goc
//
// Coral / Rock / Grass / SeabedDetail: MultiMeshInstance3D (1 node / model,
// N instance qua SetInstanceTransform) -> 1 draw call / model.
// Seaweed: Instantiate() Node3D giu nguyen vi co per-instance sway script.
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
	[Export] public int CoralLargeCountPerModel  = 2;
	[Export] public int CoralMediumCountPerModel = 12;
	[Export] public int CoralSmallCountPerModel  = 20;

	[ExportGroup("Rock")]
	[Export] public int RockCountPerModel = 16;

	[ExportGroup("Grass")]
	[Export] public int GrassCountPerModel = 60;

	[ExportGroup("SeabedDetail")]
	[Export] public int SeabedDetailCountPerModel = 25;

	[ExportGroup("Seaweed (giu Instantiate cu)")]
	[Export] public int SeaweedGroupCount = 200;

	[ExportGroup("Async Spawn")]
	// So luong prop spawn toi da moi frame. Tang = load nhanh hon nhung giat hon.
	// Giam = load mua dan nhung FPS on dinh hon luc dau game.
	[Export] public int SpawnBatchPerFrame = 8;

	[ExportGroup("Distribution")]
	[Export] public float MinSpacingSmall  = 4.0f;
	[Export] public float MinSpacingMedium = 8.0f;
	[Export] public float MinSpacingLarge  = 30.0f;
	[Export] public float NoiseFrequency     = 0.015f;
	[Export] public int   MaxAttemptsPerProp = 30;

	private Vector2 PlayerSpawnXZ = Vector2.Zero;
	[Export] public float SafeZoneRadius = 25.0f;

	[ExportGroup("Visibility Culling")]
	[Export] public float CullDistanceLarge   = 150.0f;
	[Export] public float CullDistanceMedium  = 80.0f;
	[Export] public float CullDistanceSmall   = 40.0f;
	[Export] public float CullDistanceSeaweed = 30.0f;

	[ExportGroup("World Bounds")]
	[Export] public float WorldHalfSizeX = 400f;
	[Export] public float WorldHalfSizeZ = 400f;

	private RandomNumberGenerator _rng = new();
	private FastNoiseLite _seaweedNoise = new();
	private FastNoiseLite _rockNoise    = new();
	private FastNoiseLite _grassNoise   = new();

	private List<(Vector2 pos, float radius)> _occupied = new();

	private const string MultiMeshDir = "res://scenes/props/multimesh/";

	private static readonly MultiMeshPropDef[] CoralDefs = new[]
	{
		new MultiMeshPropDef { Name = "coral",                        ScenePath = MultiMeshDir + "coral_mm.tscn",                         Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "coral_1",                      ScenePath = MultiMeshDir + "coral_1_mm.tscn",                       Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "coral_2",                      ScenePath = MultiMeshDir + "coral_2_mm.tscn",                       Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "brain_coral",                   ScenePath = MultiMeshDir + "brain_coral_mm.tscn",                   Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "brain_coral_2",                 ScenePath = MultiMeshDir + "brain_coral_2_mm.tscn",                 Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "mushroom_coral",                ScenePath = MultiMeshDir + "mushroom_coral_mm.tscn",                Tier = PropTier.Small  },
		new MultiMeshPropDef { Name = "aurora_reef_coral",             ScenePath = MultiMeshDir + "aurora_reef_coral_mm.tscn",             Tier = PropTier.Large  },
		new MultiMeshPropDef { Name = "lazulight_coral",               ScenePath = MultiMeshDir + "lazulight_coral_mm.tscn",               Tier = PropTier.Large  },
		new MultiMeshPropDef { Name = "rainbow_haven_reef_coral",      ScenePath = MultiMeshDir + "rainbow_haven_reef_coral_mm.tscn",      Tier = PropTier.Large  },
		new MultiMeshPropDef { Name = "long_ledges_reef_community",    ScenePath = MultiMeshDir + "long_ledges_reef_community_mm.tscn",    Tier = PropTier.Large  },
		new MultiMeshPropDef { Name = "brown_coral_l",                 ScenePath = MultiMeshDir + "brown_coral_l_mm.tscn",                 Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "porites_lutea_fresh",           ScenePath = MultiMeshDir + "porites_lutea_fresh_mm.tscn",           Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "pocillopora_meandrina",         ScenePath = MultiMeshDir + "pocillopora_meandrina_mm.tscn",         Tier = PropTier.Medium },
		new MultiMeshPropDef { Name = "staghorn_coral",                ScenePath = MultiMeshDir + "staghorn_coral_mm.tscn",                Tier = PropTier.Medium },
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

	// --- Async spawn state ---
	// Moi "job" la 1 PropDef can spawn voi so luong + noise config cu the.
	// _pendingJobs duoc nap het trong _Ready(), sau do _Process() giai quyet
	// tung job theo batch SpawnBatchPerFrame moi frame cho den het.
	private struct SpawnJob
	{
		public bool IsMultiMesh;
		// MultiMesh fields
		public MultiMeshPropDef Def;
		public int Count;
		public FastNoiseLite Noise;
		public float Threshold;
		public bool Invert;
		// Seaweed fields (IsMultiMesh = false)
		// dung Noise, Threshold tu job nay
		public int SeaweedCount;

		// Runtime state (khoi tao khi job bat dau chay)
		public MultiMeshInstance3D MmInstance;   // chi dung khi IsMultiMesh = true
		public MultiMesh MultiMesh;
		public List<Transform3D> Transforms;
		public int Spawned;
		public int Attempts;
		public int MaxAttempts;
		public bool Started;
	}

	private Queue<SpawnJob> _pendingJobs = new();
	private SpawnJob _currentJob;
	private bool _hasCurrentJob = false;
	private bool _spawnDone = false;

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

		_occupied.Add((PlayerSpawnXZ, SafeZoneRadius));

		// Nap job theo thu tu uu tien: Large coral truoc (chiem dien tich lon nhat)
		foreach (var def in CoralDefs)
		{
			int count = def.Tier switch
			{
				PropTier.Large  => CoralLargeCountPerModel,
				PropTier.Medium => CoralMediumCountPerModel,
				_                => CoralSmallCountPerModel,
			};
			_pendingJobs.Enqueue(new SpawnJob { IsMultiMesh = true, Def = def, Count = count });
		}

		foreach (var def in RockDefs)
			_pendingJobs.Enqueue(new SpawnJob { IsMultiMesh = true, Def = def, Count = RockCountPerModel, Noise = _rockNoise, Threshold = -0.05f, Invert = true });

		foreach (var def in GrassDefs)
			_pendingJobs.Enqueue(new SpawnJob { IsMultiMesh = true, Def = def, Count = GrassCountPerModel, Noise = _grassNoise, Threshold = 0.05f });

		foreach (var def in SeabedDetailDefs)
			_pendingJobs.Enqueue(new SpawnJob { IsMultiMesh = true, Def = def, Count = SeabedDetailCountPerModel });

		_pendingJobs.Enqueue(new SpawnJob { IsMultiMesh = false, SeaweedCount = SeaweedGroupCount, Noise = _seaweedNoise, Threshold = 0.05f });
	}

	public override void _Process(double delta)
	{
		if (_spawnDone) return;

		int budget = SpawnBatchPerFrame;

		while (budget > 0)
		{
			// Lay job tiep theo neu job hien tai xong hoac chua co
			if (!_hasCurrentJob)
			{
				if (_pendingJobs.Count == 0)
				{
					// Tat ca job xong — finalize MultiMesh va tat _Process
					FinalizeCurrentJobIfAny();
					_spawnDone = true;
					SetProcess(false);
					return;
				}

				FinalizeCurrentJobIfAny();
				_currentJob = _pendingJobs.Dequeue();
				_hasCurrentJob = true;
				StartJob(ref _currentJob);
			}

			// Xu ly 1 attempt (co the thanh cong hoac fail do noise/overlap)
			bool jobDone = _currentJob.IsMultiMesh
				? StepMultiMeshJob(ref _currentJob)
				: StepSeaweedJob(ref _currentJob);

			if (jobDone)
			{
				FinalizeCurrentJobIfAny();
				_hasCurrentJob = false;
			}

			budget--;
		}
	}

	private void StartJob(ref SpawnJob job)
	{
		if (!job.IsMultiMesh) { job.MaxAttempts = job.SeaweedCount * MaxAttemptsPerProp; return; }

		var scene = GD.Load<PackedScene>(job.Def.ScenePath);
		if (scene == null)
		{
			GD.PrintErr($"[EnvironmentSpawner] Khong load duoc: {job.Def.ScenePath}");
			job.Spawned = job.Count; // skip
			return;
		}

		job.MmInstance = scene.Instantiate<MultiMeshInstance3D>();
		AddChild(job.MmInstance);

		if (job.MmInstance.Multimesh == null)
		{
			GD.PrintErr($"[EnvironmentSpawner] Khong co Multimesh: {job.Def.ScenePath}");
			job.MmInstance.QueueFree();
			job.Spawned = job.Count;
			return;
		}

		job.MultiMesh = (MultiMesh)job.MmInstance.Multimesh.Duplicate();
		job.MmInstance.Multimesh = job.MultiMesh;
		job.Transforms = new List<Transform3D>(job.Count);
		job.MaxAttempts = job.Count * MaxAttemptsPerProp;

		float cullDist = CullDistanceForTier(job.Def.Tier);
		job.MmInstance.VisibilityRangeEnd = cullDist;
		job.MmInstance.VisibilityRangeEndMargin = cullDist * 0.1f;
	}

	// Tra ve true khi job hoan thanh (het so luong hoac het luot thu)
	private bool StepMultiMeshJob(ref SpawnJob job)
	{
		if (job.Spawned >= job.Count || job.Attempts >= job.MaxAttempts) return true;

		job.Attempts++;

		float x = _rng.RandfRange(-WorldHalfSizeX, WorldHalfSizeX);
		float z = _rng.RandfRange(-WorldHalfSizeZ, WorldHalfSizeZ);

		if (job.Noise != null)
		{
			float n = job.Noise.GetNoise2D(x, z);
			bool reject = job.Invert ? (n > job.Threshold) : (n < job.Threshold);
			if (reject) return false;
		}

		Vector2 pos2D = new Vector2(x, z);
		float spacing = SpacingForTier(job.Def.Tier);

		foreach (var occ in _occupied)
		{
			if (occ.pos.DistanceSquaredTo(pos2D) < Mathf.Max(spacing, occ.radius) * Mathf.Max(spacing, occ.radius))
				return false;
		}

		float worldY = Utils.GetFloorY(x, z);
		// Khong random scale: scale da embed vao Mesh transform khi ExtractMultiMeshProps chay.
		// Random scale them se nhan doi kich thuoc (coral scale 8 trong scene x 1.3 = 10.4).
		float rotY = _rng.RandfRange(0f, Mathf.Pi * 2f);
		var basis = Basis.FromEuler(new Vector3(0f, rotY, 0f));
		job.Transforms.Add(new Transform3D(basis, new Vector3(x, worldY, z)));

		_occupied.Add((pos2D, spacing * 0.5f));
		job.Spawned++;
		return job.Spawned >= job.Count;
	}

	private bool StepSeaweedJob(ref SpawnJob job)
	{
		if (job.Spawned >= job.SeaweedCount || job.Attempts >= job.MaxAttempts) return true;

		job.Attempts++;

		float x = _rng.RandfRange(-WorldHalfSizeX, WorldHalfSizeX);
		float z = _rng.RandfRange(-WorldHalfSizeZ, WorldHalfSizeZ);

		if (job.Noise != null && job.Noise.GetNoise2D(x, z) < job.Threshold) return false;

		Vector2 pos2D = new Vector2(x, z);
		foreach (var occ in _occupied)
		{
			if (occ.pos.DistanceSquaredTo(pos2D) < Mathf.Max(MinSpacingSmall, occ.radius) * Mathf.Max(MinSpacingSmall, occ.radius))
				return false;
		}

		var scene = GD.Load<PackedScene>(SeaweedScene);
		if (scene == null) return true;

		var prop = scene.Instantiate<Node3D>();
		prop.Position = new Vector3(x, Utils.GetFloorY(x, z), z);
		prop.RotateY(_rng.RandfRange(0f, Mathf.Pi * 2f));
		// Scale nho: SeaweedGroup.tscn chua nhieu cay rong con, scale 0.3-0.5 vua mat
		prop.Scale = Vector3.One * _rng.RandfRange(0.3f, 0.5f);
		AddChild(prop);
		CreatureSpawner.PlayFirstAnimation(prop);
		SetVisibilityRangeOnChildren(prop, CullDistanceSeaweed);

		_occupied.Add((pos2D, MinSpacingSmall * 0.5f));
		job.Spawned++;
		return job.Spawned >= job.SeaweedCount;
	}

	private void FinalizeCurrentJobIfAny()
	{
		if (!_hasCurrentJob) return;
		if (!_currentJob.IsMultiMesh) return;
		if (_currentJob.MultiMesh == null || _currentJob.Transforms == null) return;

		_currentJob.MultiMesh.InstanceCount = _currentJob.Transforms.Count;
		for (int i = 0; i < _currentJob.Transforms.Count; i++)
			_currentJob.MultiMesh.SetInstanceTransform(i, _currentJob.Transforms[i]);

		if (_currentJob.Spawned < _currentJob.Count)
			GD.Print($"[EnvironmentSpawner] {_currentJob.Def.Name}: {_currentJob.Spawned}/{_currentJob.Count} instance.");
	}

	private float SpacingForTier(PropTier tier) => tier switch
	{
		PropTier.Large  => MinSpacingLarge,
		PropTier.Medium => MinSpacingMedium,
		_                => MinSpacingSmall,
	};

	private float CullDistanceForTier(PropTier tier) => tier switch
	{
		PropTier.Large  => CullDistanceLarge,
		PropTier.Medium => CullDistanceMedium,
		_                => CullDistanceSmall,
	};

	private static void SetVisibilityRangeOnChildren(Node root, float cullDist)
	{
		if (root is GeometryInstance3D geo)
		{
			geo.VisibilityRangeEnd = cullDist;
			geo.VisibilityRangeEndMargin = cullDist * 0.1f;
		}
		foreach (Node child in root.GetChildren())
			SetVisibilityRangeOnChildren(child, cullDist);
	}
}
