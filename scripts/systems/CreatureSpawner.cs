using Godot;
using System.Collections.Generic;

public partial class CreatureSpawner : Node3D
{
	[Export] public int FishCountPerZone = 6;

	private List<Node3D> _spawnedCreatures = new();
	private RandomNumberGenerator _rng = new();
	private Node3D _fishSchool;

	// Danh sách scene paths
	private static readonly string[] FishScenes =
	{
		"res://scenes/creatures/Clownfish.tscn",
		"res://scenes/creatures/Angelfish.tscn",
		"res://scenes/creatures/YellowTangFish.tscn",
	};

	private static readonly string[] AmbientScenes =
	{
		"res://scenes/creatures/SeaTurtle.tscn",
	};

	public override void _Ready()
	{
		_rng.Randomize();
		_fishSchool = GetNode<Node3D>("FishSchool");

		SpawnFish();
		SpawnAmbient();
	}

	private void SpawnFish()
	{
		var groupCenters = new Vector3[]
		{
			new(0f,   -5f,  -20f),
			new(20f,  -15f, -40f),
			new(-15f, -25f, -60f),
			new(10f,  -35f, -80f),
		};

		foreach (var center in groupCenters)
		{
			// Clamp center Y không thấp hơn seabed + 3m
			float floorY = Utils.GetFloorY(center.X, center.Z);
			var safeCenter = new Vector3(
				center.X,
				Mathf.Max(center.Y, floorY + 3f), // cá bơi cách đáy ít nhất 3m
				center.Z
			);

			string scenePath = FishScenes[_rng.RandiRange(0, FishScenes.Length - 1)];
			var scene = GD.Load<PackedScene>(scenePath);
			if (scene == null) continue;

			var school = new SchoolController();
			_fishSchool.AddChild(school);
			school.GlobalPosition = safeCenter;

			for (int i = 0; i < FishCountPerZone; i++)
			{
				var offset = new Vector3(
					_rng.RandfRange(-4f, 4f),
					_rng.RandfRange(-1f, 1f),
					_rng.RandfRange(-4f, 4f)
				);
				var pos = safeCenter + offset;

				// Clamp từng con cá
				float fishFloorY = Utils.GetFloorY(pos.X, pos.Z);
				pos.Y = Mathf.Max(pos.Y, fishFloorY + 1.5f);

				SpawnCreature(scene, pos, school);
			}

			school.RegisterMembers();
		}
	}

	private void SpawnAmbient()
	{
		var turtleScene = GD.Load<PackedScene>(AmbientScenes[0]);
		if (turtleScene == null) return;

		for (int i = 0; i < 3; i++)
		{
			float x = _rng.RandfRange(-40f, 40f);
			float z = _rng.RandfRange(-20f, -80f);
			float y = Mathf.Max(_rng.RandfRange(-5f, -15f), Utils.GetFloorY(x, z) + 2f);

			SpawnCreature(turtleScene, new Vector3(x, y, z), this);
		}
	}

	private void SpawnCreature(PackedScene scene, Vector3 position, Node3D parent)
	{
		var creature = scene.Instantiate<Node3D>();
		creature.Position = position;
		parent.AddChild(creature);
		_spawnedCreatures.Add(creature);

		// Auto-play animation nếu có
		PlayFirstAnimation(creature);
	}

	public static void PlayFirstAnimation(Node3D root)
	{
		var animPlayer = FindAnimationPlayer(root);
		if (animPlayer == null) return;

		var anims = animPlayer.GetAnimationList();
		if (anims.Length == 0) return;

		// Ưu tiên "swim" hoặc "idle", fallback animation đầu tiên
		string toPlay = anims[0];
		animPlayer.Play(toPlay);
		animPlayer.SpeedScale = 1.0f;
	}

	private static AnimationPlayer FindAnimationPlayer(Node node)
	{
		if (node is AnimationPlayer ap) return ap;
		foreach (var child in node.GetChildren())
		{
			var found = FindAnimationPlayer(child);
			if (found != null) return found;
		}
		return null;
	}
}
