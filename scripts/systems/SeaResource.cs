using Godot;

[GlobalClass]
public partial class SeaResource : Resource
{
	public enum Rarity { Common, Uncommon, Rare, Epic }

	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public Texture2D Icon { get; set; }
	[Export] public Rarity ResourceRarity { get; set; } = Rarity.Common;
	[Export] public int BaseValue { get; set; } = 10;
	[Export] public float Weight { get; set; } = 0.5f;
	[Export] public string[] SpawnZones { get; set; } = {};
	[Export] public float MinDepth { get; set; } = 0f;
	[Export] public float MaxDepth { get; set; } = 20f;
	[Export] public float CatchDifficulty { get; set; } = 0.3f;
	[Export] public float RespawnTime { get; set; } = 30f;
	[Export] public PackedScene ResourceScene { get; set; }          // mesh tĩnh (fallback)
	[Export] public float MinHeight { get; set; } = 0f;
		// ---------- Instance Variation ----------

	[ExportGroup("Scale")]
	[Export] public float MinScale { get; set; } = 0.9f;
	[Export] public float MaxScale { get; set; } = 1.1f;

	[ExportGroup("Movement")]
	[Export] public bool CanMove { get; set; } = false;

	[Export] public float MinSpeed { get; set; } = 0.5f;
	[Export] public float MaxSpeed { get; set; } = 1.5f;

	[Export] public float WanderRadius { get; set; } = 10f;

	[Export] public float TurnIntervalMin { get; set; } = 3f;
	[Export] public float TurnIntervalMax { get; set; } = 8f;

	[ExportGroup("Spawn Distribution")]
	[Export] public float SpawnPeakDistance { get; set; } = 0f;   // khoảng cách peak từ origin
	[Export] public float SpawnSigma { get; set; } = 20f;          // độ rộng phân phối
	[Export] public int SpawnCount { get; set; } = 10;             // số lượng spawn

	[ExportGroup("Behavior")]
	[Export] public float FearRadius { get; set; } = 5f; // resource bỏ chạy khi player lại gần
	[Export] public bool CanFlee { get; set; } = false;  // chỉ bật cho creature-type resource
}
