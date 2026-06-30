#if TOOLS
using Godot;

// Chay 1 lan trong Godot Editor (chuot phai file -> Run) sau khi asset .glb that
// da co tren dia.
//
// CoralGroup.tscn / BigCoralGroup.tscn / RockGroup.tscn / GrassGroup.tscn /
// SeabedDetailGroup.tscn deu gop nhieu model .glb khac nhau trong 1 scene.
// MultiMeshInstance3D chi render duoc 1 mesh/material duy nhat, nen moi model
// duoc tach thanh 1 file MultiMesh .tres + 1 scene wrapper *_mm.tscn rieng.
// EnvironmentSpawner se load toan bo cac file nay va rai theo PropCategory +
// PropTier (xem EnvironmentSpawner.cs).
//
// LUU Y: SeaweedGroup KHONG nam trong danh sach nay - no co script rieng
// (SeaweedGroup.cs) dieu khien sway animation theo dong nuoc moi frame, MultiMesh
// khong ho tro animation rieng tung instance nen giu nguyen cach Instantiate() cu.
[Tool]
public partial class ExtractMultiMeshProps : EditorScript
{
	public enum PropCategory { Coral, Rock, Grass, SeabedDetail }
	public enum PropTier { Large, Medium, Small }

	private class SourceEntry
	{
		public string Name;
		public string GlbPath;
		public PropCategory Category;
		public PropTier Tier;
	}

	private static readonly SourceEntry[] Sources = new[]
	{
		// ---- Coral: tu CoralGroup.tscn (cu) ----
		new SourceEntry { Name = "coral",        GlbPath = "res://assets/models/creatures/coral.glb",        Category = PropCategory.Coral, Tier = PropTier.Medium },
		new SourceEntry { Name = "coral_1",      GlbPath = "res://assets/models/creatures/coral_1.glb",      Category = PropCategory.Coral, Tier = PropTier.Medium },
		new SourceEntry { Name = "coral_2",      GlbPath = "res://assets/models/creatures/coral_2.glb",      Category = PropCategory.Coral, Tier = PropTier.Medium },
		new SourceEntry { Name = "brain_coral",   GlbPath = "res://assets/models/creatures/brain_coral.glb",   Category = PropCategory.Coral, Tier = PropTier.Medium },
		new SourceEntry { Name = "brain_coral_2", GlbPath = "res://assets/models/creatures/brain_coral_2.glb", Category = PropCategory.Coral, Tier = PropTier.Medium },
		new SourceEntry { Name = "mushroom_coral",GlbPath = "res://assets/models/creatures/mushroom_coral.glb",Category = PropCategory.Coral, Tier = PropTier.Small },

		// ---- Coral: tu BigCoralGroup.tscn (moi, da nen) ----
		new SourceEntry { Name = "aurora_reef_coral",          GlbPath = "res://assets/models/coral/aurora_reef_coral.glb",                Category = PropCategory.Coral, Tier = PropTier.Large },
		new SourceEntry { Name = "lazulight_coral",             GlbPath = "res://assets/models/coral/lazulight-coral.glb",                  Category = PropCategory.Coral, Tier = PropTier.Large },
		new SourceEntry { Name = "rainbow_haven_reef_coral",     GlbPath = "res://assets/models/coral/rainbow_haven_reef_coral.glb",         Category = PropCategory.Coral, Tier = PropTier.Large },
		new SourceEntry { Name = "long_ledges_reef_community",  GlbPath = "res://assets/models/decor/long_ledges_reef_community.glb",       Category = PropCategory.Coral, Tier = PropTier.Large },
		new SourceEntry { Name = "brown_coral_l",                GlbPath = "res://assets/models/coral/brown_coral_l.glb",                    Category = PropCategory.Coral, Tier = PropTier.Medium },
		new SourceEntry { Name = "porites_lutea_fresh",          GlbPath = "res://assets/models/coral/porites_lutea_fresh_sample.glb",       Category = PropCategory.Coral, Tier = PropTier.Medium },
		new SourceEntry { Name = "pocillopora_meandrina",        GlbPath = "res://assets/models/coral/pocillopora_meandrina.glb",            Category = PropCategory.Coral, Tier = PropTier.Medium },
		new SourceEntry { Name = "staghorn_coral",               GlbPath = "res://assets/models/decor/staghorn_coral.glb",                   Category = PropCategory.Coral, Tier = PropTier.Medium },

		// ---- Rock: tu RockGroup.tscn ----
		new SourceEntry { Name = "rock",   GlbPath = "res://assets/models/creatures/rock.glb",   Category = PropCategory.Rock, Tier = PropTier.Medium },
		new SourceEntry { Name = "rock_1", GlbPath = "res://assets/models/creatures/rock_1.glb", Category = PropCategory.Rock, Tier = PropTier.Medium },
		new SourceEntry { Name = "rock_2", GlbPath = "res://assets/models/creatures/rock_2.glb", Category = PropCategory.Rock, Tier = PropTier.Medium },

		// ---- Grass: tu GrassGroup.tscn (qua GrassPatch / GrassCloverPatch) ----
		new SourceEntry { Name = "grass",        GlbPath = "res://assets/models/decor/grass.glb",        Category = PropCategory.Grass, Tier = PropTier.Small },
		new SourceEntry { Name = "grass_clover", GlbPath = "res://assets/models/decor/grass_clover.glb", Category = PropCategory.Grass, Tier = PropTier.Small },
		new SourceEntry { Name = "sea_sponge",   GlbPath = "res://assets/models/decor/sea_sponge.glb",   Category = PropCategory.Grass, Tier = PropTier.Small },

		// ---- SeabedDetail: tu SeabedDetailGroup.tscn ----
		new SourceEntry { Name = "starfish", GlbPath = "res://assets/models/decor/starfish.glb", Category = PropCategory.SeabedDetail, Tier = PropTier.Small },
		new SourceEntry { Name = "urchin",   GlbPath = "res://assets/models/decor/urchin.glb",   Category = PropCategory.SeabedDetail, Tier = PropTier.Small },
		new SourceEntry { Name = "urchin2",  GlbPath = "res://assets/models/decor/urchin2.glb",  Category = PropCategory.SeabedDetail, Tier = PropTier.Small },
	};

	private const string MultiMeshOutDir = "res://resources/multimesh/";
	private const string SceneOutDir     = "res://scenes/props/multimesh/";

	public override void _Run()
	{
		EnsureDir(MultiMeshOutDir);
		EnsureDir(SceneOutDir);

		int ok = 0, failed = 0;

		foreach (var src in Sources)
		{
			if (!ResourceLoader.Exists(src.GlbPath))
			{
				GD.PrintErr($"[ExtractMultiMeshProps] BO QUA (khong tim thay file): {src.GlbPath}");
				failed++;
				continue;
			}

			var packed = GD.Load<PackedScene>(src.GlbPath);
			if (packed == null)
			{
				GD.PrintErr($"[ExtractMultiMeshProps] Load that bai: {src.GlbPath}");
				failed++;
				continue;
			}

			var instance = packed.Instantiate<Node3D>();
			var meshInstance = FindFirstMeshInstance(instance);

			if (meshInstance == null || meshInstance.Mesh == null)
			{
				GD.PrintErr($"[ExtractMultiMeshProps] Khong tim thay MeshInstance3D hop le trong: {src.GlbPath}");
				instance.QueueFree();
				failed++;
				continue;
			}

			var mesh = meshInstance.Mesh;
			Material materialOverride = meshInstance.GetSurfaceOverrideMaterial(0);

			var multiMesh = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				Mesh = mesh,
				InstanceCount = 0, // EnvironmentSpawner set lai luc runtime
			};

			string mmPath = $"{MultiMeshOutDir}{src.Name}_multimesh.tres";
			var saveErr = ResourceSaver.Save(multiMesh, mmPath);
			if (saveErr != Error.Ok)
			{
				GD.PrintErr($"[ExtractMultiMeshProps] Luu MultiMesh that bai ({saveErr}): {mmPath}");
				instance.QueueFree();
				failed++;
				continue;
			}

			var mmInstance = new MultiMeshInstance3D
			{
				Name = $"{src.Name}_MM",
				Multimesh = GD.Load<MultiMesh>(mmPath),
			};
			if (materialOverride != null)
			{
				mmInstance.MaterialOverride = materialOverride;
			}

			var wrapperScene = new PackedScene();
			wrapperScene.Pack(mmInstance);

			string scenePath = $"{SceneOutDir}{src.Name}_mm.tscn";
			saveErr = ResourceSaver.Save(wrapperScene, scenePath);
			if (saveErr != Error.Ok)
			{
				GD.PrintErr($"[ExtractMultiMeshProps] Luu scene wrapper that bai ({saveErr}): {scenePath}");
				failed++;
			}
			else
			{
				GD.Print($"[ExtractMultiMeshProps] OK ({src.Category}/{src.Tier}): {src.Name} -> {scenePath}");
				ok++;
			}

			mmInstance.QueueFree();
			instance.QueueFree();
		}

		GD.Print($"[ExtractMultiMeshProps] Hoan tat. Thanh cong: {ok}, Loi/bo qua: {failed}");
		GD.Print("[ExtractMultiMeshProps] Danh sach scene path duoc EnvironmentSpawner.cs doc cung " +
			"thu muc nay (res://scenes/props/multimesh/) - khong can sua gi them neu ten file khop.");
	}

	private static MeshInstance3D FindFirstMeshInstance(Node node)
	{
		if (node is MeshInstance3D mi && mi.Mesh != null)
		{
			return mi;
		}

		foreach (Node child in node.GetChildren())
		{
			var found = FindFirstMeshInstance(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private static void EnsureDir(string resPath)
	{
		string absPath = ProjectSettings.GlobalizePath(resPath);
		if (!System.IO.Directory.Exists(absPath))
		{
			System.IO.Directory.CreateDirectory(absPath);
		}
	}
}
#endif
