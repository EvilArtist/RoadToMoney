using Godot;

[GlobalClass]
public partial class CreatureData : Resource
{
	public enum CreatureType { SmallFish, LargeFish, Crab, Squid, Octopus, Turtle, Jellyfish }

	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public CreatureType Type { get; set; }
	[Export] public float MoveSpeed { get; set; } = 3f;
	[Export] public float FleeSpeed { get; set; } = 6f;
	[Export] public float DetectionRadius { get; set; } = 5f;
	[Export] public float FleeRadius { get; set; } = 8f;
	[Export] public bool  IsCatchable { get; set; } = true;
	[Export] public string DropResourceId { get; set; } = "";
	[Export] public string[] SpawnZones { get; set; } = {};
	[Export] public float MinDepth { get; set; } = 0f;
	[Export] public float MaxDepth { get; set; } = 20f;
}
