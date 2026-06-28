#if TOOLS
using Godot;

[Tool]
public partial class CreateSeaResources : EditorScript
{
	public override void _Run()
	{
		var items = new[]
		{
			new { Id="shell",      Name="Shell",      Rarity=0, Value=5,   Weight=0.1f, Min=0f,  Max=8f,   Diff=0.1f, Zones=new[]{"Zone_Shallow"} },
			new { Id="clam",       Name="Clam",       Rarity=1, Value=18,  Weight=0.3f, Min=5f,  Max=15f,  Diff=0.2f, Zones=new[]{"Zone_Shallow"} },
			new { Id="shrimp",     Name="Shrimp",     Rarity=0, Value=22,  Weight=0.2f, Min=10f, Max=30f,  Diff=0.2f, Zones=new[]{"Zone_Reef"} },
			new { Id="crab",       Name="Crab",       Rarity=1, Value=45,  Weight=0.8f, Min=15f, Max=40f,  Diff=0.4f, Zones=new[]{"Zone_Reef"} },
			new { Id="squid",      Name="Squid",      Rarity=2, Value=80,  Weight=0.6f, Min=35f, Max=60f,  Diff=0.5f, Zones=new[]{"Zone_Deep"} },
			new { Id="octopus",    Name="Octopus",    Rarity=2, Value=120, Weight=1.2f, Min=40f, Max=70f,  Diff=0.7f, Zones=new[]{"Zone_Deep"} },
			new { Id="small_fish", Name="Small Fish", Rarity=0, Value=30,  Weight=0.3f, Min=0f,  Max=50f,  Diff=0.3f, Zones=new[]{"Zone_Shallow","Zone_Reef","Zone_Deep"} },
			new { Id="large_fish", Name="Large Fish", Rarity=3, Value=250, Weight=2.0f, Min=60f, Max=120f, Diff=0.8f, Zones=new[]{"Zone_Abyss"} },
		};

		foreach (var item in items)
		{
			var res = new SeaResource
			{
				Id              = item.Id,
				DisplayName     = item.Name,
				ResourceRarity  = (SeaResource.Rarity)item.Rarity,
				BaseValue       = item.Value,
				Weight          = item.Weight,
				MinDepth        = item.Min,
				MaxDepth        = item.Max,
				CatchDifficulty = item.Diff,
				RespawnTime     = 30f,
				SpawnZones      = item.Zones
			};

			string path = $"res://resources/items/{item.Id}.tres";
			ResourceSaver.Save(res, path);
		}

		GD.Print("Done! All 8 SeaResource files created.");
	}
}
#endif
