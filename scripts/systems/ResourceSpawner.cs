using Godot;
using System.Collections.Generic;

public partial class ResourceSpawner : Node3D
{
	[Export] public int SpawnCountPerZone = 10;
	[Export] public SeaResource[] ResourcePool = {};

	// Preload tất cả resource từ resources/items/
	private static readonly string[] ResourcePaths =
	{
		"res://resources/items/shell.tres",
		"res://resources/items/clam.tres",
		"res://resources/items/shrimp.tres",
		"res://resources/items/crab.tres",
		"res://resources/items/squid.tres",
		"res://resources/items/octopus.tres",
		"res://resources/items/small_fish.tres",
		"res://resources/items/large_fish.tres",
		"res://resources/items/tuna_fish.tres",
		"res://resources/items/sail_fish.tres",
		"res://resources/items/shark.tres",
	};

	private List<SeaResource> _resources = new();
	private List<Node3D>      _spawnedItems = new();
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_rng.Randomize();
		// Load tất cả resource data
		foreach (var path in ResourcePaths)
		{
			// var res = GD.Load<SeaResource>(path);
			var res = ResourceLoader.Load<SeaResource>(path, "", ResourceLoader.CacheMode.Ignore);
			if (res != null) _resources.Add(res);
		}

		SpawnAll();
	}

	private void SpawnAll()
	{
		foreach (var res in _resources)
		{
			for (int i = 0; i < res.SpawnCount; i++)
			{
				float dist  = SampleGaussianDist(res.SpawnPeakDistance, res.SpawnSigma, 0f, 300f);
				//float dist  = SampleGaussianDist(_resources[0].SpawnPeakDistance, _resources[0].SpawnSigma, 0f, 300f);
				float x = _rng.RandfRange(-dist, dist);
				float z = -Mathf.Sqrt(Mathf.Max(0f, dist * dist - x * x)); // luôn âm
				float sign = _rng.RandfRange(0, 1);
				z = sign < 0.5 ? z : -z;
				z += _rng.RandfRange(-10f, 10f); // thêm noise nhỏ
				float floorY = Utils.GetFloorY(x, z);
				float offset = GaussianOffset(res.MinHeight);
				if (res.Id == "shell" || res.Id == "clam") {
					offset = 0;
				}

				float y = Mathf.Min(floorY + offset, -5f);
				var pos = new Vector3(x, floorY + offset, z);

				SpawnItem(res, pos);
			}
		}
	}
	
	private float SampleGaussianDist(float mean, float sigma, float min, float max)
	{
		for (int attempt = 0; attempt < 20; attempt++)
		{
			float u1 = Mathf.Max(_rng.Randf(), 0.0001f);
			float u2 = _rng.Randf();
			float gauss = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.Pi * u2);
			float val = mean + gauss * sigma;
			if (val >= min && val <= max) return val;
		}
		return mean; // fallback
	}
	// Phân phối Gauss: mean=0, std=0.15, clamp [0, 0.5]
	float GaussianOffset(float mean)
	{
		// Box-Muller transform
		float u1 = Mathf.Max(_rng.Randf(), 0.0001f);
		float u2 = _rng.Randf();
		float gaussian = Mathf.Sqrt(-2f * Mathf.Log(u1)) 
					   * Mathf.Cos(2f * Mathf.Pi * u2);
		
		// Mean = 0.15, std = 0.1 → hầu hết trong [0, 0.3]
		float offset = mean + gaussian * 0.1f;
		return Mathf.Max(offset, 0.1f);
	}

	private void SpawnItem(SeaResource res, Vector3 position)
	{
		Node3D item;

		if (res.ResourceScene != null)
		{
			// Spawn scene (có animation hoặc GLB)
			item = res.ResourceScene.Instantiate<Node3D>();
		}
		else
		{
			// Fallback — SphereMesh placeholder
			var mesh = new MeshInstance3D();
			var sphereMesh = new SphereMesh();
			sphereMesh.Radius = 0.15f;
			sphereMesh.Height = 0.3f;
			mesh.Mesh = sphereMesh;

			var mat = new StandardMaterial3D();
			mat.AlbedoColor = res.ResourceRarity switch
			{
				SeaResource.Rarity.Common   => new Color(0.8f, 0.8f, 0.8f),
				SeaResource.Rarity.Uncommon => new Color(0.2f, 0.5f, 1.0f),
				SeaResource.Rarity.Rare     => new Color(0.5f, 0.0f, 0.8f),
				SeaResource.Rarity.Epic     => new Color(1.0f, 0.6f, 0.0f),
				_ => new Color(1, 1, 1)
			};
			mesh.MaterialOverride = mat;
			item = mesh;
		}

		item.Position = position;
		float scale = _rng.RandfRange(
			res.MinScale,
			res.MaxScale
		);

		Vector3 scaleVector = Vector3.One * scale;
		item.Scale = res.Id switch
		{
			"shell"      => scaleVector * 0.4f,
			"clam"       => scaleVector * 1.0f,
			"shrimp"     => scaleVector * 1.0f,
			"crab"       => scaleVector * 1.0f,
			"squid"      => scaleVector * 1.0f,
			"small_fish" => scaleVector * 1.0f,
			"large_fish" => scaleVector * 1.0f,
			_            => Vector3.One
		};
		float rotate = _rng.RandfRange(0f,	360f);
		item.Rotation = new Vector3(0, rotate, 0);

		// Thêm collision nếu chưa có StaticBody3D
		bool hasBody = false;
		foreach (var child in item.GetChildren())
			if (child is StaticBody3D) { hasBody = true; break; }

		if (!hasBody)
		{
			var body   = new StaticBody3D();
			var shape  = new CollisionShape3D();
			var sphere = new SphereShape3D();
			sphere.Radius = 0.3f;
			shape.Shape   = sphere;
			body.AddChild(shape);
			item.AddChild(body);
		}

		// Metadata để CollectionInteraction detect
		item.SetMeta("resource_id",      res.Id);
		item.SetMeta("resource_value",   res.BaseValue);
		item.SetMeta("resource_weight",  res.Weight);
		item.SetMeta("can_move",         res.CanMove);
		item.SetMeta("catch_difficulty", res.CatchDifficulty);

		// Play animation nếu có
		var animPlayer = item.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		
		if (animPlayer != null)
		{
			var anims = animPlayer.GetAnimationList();
			if (anims.Length > 0) {
				var anim = animPlayer.GetAnimation(anims[0]);
				anim.LoopMode = Animation.LoopModeEnum.Linear;
				animPlayer.Play(anims[0]);
			}
		}
		if (res.CanMove)
		{
			Vector3 direction = new Vector3(
				_rng.RandfRange(-1f, 1f),
				_rng.RandfRange(-0.15f, 0.15f),
				_rng.RandfRange(-1f, 1f)
			).Normalized();

			float speed = _rng.RandfRange(
				res.MinSpeed,
				res.MaxSpeed
			);

			// var mover = item.GetNodeOrNull<ResourceMove>("ResourceMove");
			var mover = item as 	ResourceMove;
			if (mover != null)
			{
				mover.Configure(
					speed,
					direction,
					res.WanderRadius,
					res.TurnIntervalMin,
					res.TurnIntervalMax,
					res.CanFlee,
					res.FearRadius,
					res.MaxSpeed * 2f
				);
			} 
		}

		AddChild(item);
		_spawnedItems.Add(item);
	}
	

	void PrintChildren(Node node, int depth)
	{
		if (depth >= 5) return;
		foreach (var child in node.GetChildren())
		{
			PrintChildren(child, depth + 1);
		}
	}

	private SeaResource PickWeightedRandom(List<SeaResource> pool, RandomNumberGenerator rng)
	{
		// Weight ngược với rarity — Common xuất hiện nhiều hơn
		float[] weights = { 0.5f, 0.3f, 0.15f, 0.05f }; // Common/Uncommon/Rare/Epic
		float total = 0f;

		foreach (var r in pool)
			total += weights[(int)r.ResourceRarity];

		float roll = rng.RandfRange(0f, total);
		float cumulative = 0f;

		foreach (var r in pool)
		{
			cumulative += weights[(int)r.ResourceRarity];
			if (roll <= cumulative) return r;
		}

		return pool[0];
	}

	public void DespawnItem(Node3D item)
	{
		_spawnedItems.Remove(item);
		item.QueueFree();
	}

	public void RespawnAll()
	{
		// Xóa toàn bộ items hiện tại
		foreach (var item in _spawnedItems)
		{
			if (IsInstanceValid(item))
				item.QueueFree();
		}
		_spawnedItems.Clear();

		// Spawn lại
		SpawnAll();
	}
}
