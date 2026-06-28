using Godot;
using Godot.Collections;

public partial class SaveSystem : Node
{
	public static SaveSystem Instance { get; private set; }

	private const string SavePath   = "user://save_data.json";
	private const string BackupPath = "user://save_data.bak";
	private const string Version    = "1.0";

	public override void _Ready() => Instance = this;

	public void SaveGame()
	{
		var upgrades = new Dictionary();

		foreach (var kv in UpgradeManager.Instance.Upgrades)
		{
			upgrades[kv.Key] = kv.Value.Level;
		}

		var inventory = new Dictionary();

		foreach (var kv in EconomyManager.Instance.Inventory)
		{
			inventory[kv.Key] = kv.Value;
		}
		var data = new Dictionary
		{
			["version"]   = Version,
			["timestamp"] = Time.GetUnixTimeFromSystem(),
			["player"] = new Dictionary
			{
				["coins"]       = EconomyManager.Instance.Coins,
				["totalEarned"] = EconomyManager.Instance.TotalEarned,
				["totalDives"]  = GameManager.Instance.TotalDives
			},
			["upgrades"]  = upgrades,
			["inventory"] = inventory
		};

		string tmpPath = SavePath + ".tmp";
		using var file = FileAccess.Open(tmpPath, FileAccess.ModeFlags.Write);
		file.StoreString(Json.Stringify(data, "\t"));
		file.Close();

		if (FileAccess.FileExists(SavePath))
			DirAccess.CopyAbsolute(SavePath, BackupPath);

		DirAccess.RenameAbsolute(tmpPath, SavePath);
		EventBus.Instance.EmitGameSaved();
	}

	public bool LoadGame()
	{
		if (!FileAccess.FileExists(SavePath)) return false;
		var data = ReadJson(SavePath);
		if (!Validate(data)) return TryRestoreBackup();
		Apply(data);
		return true;
	}

	private bool Validate(Dictionary data) =>
		data.ContainsKey("version") &&
		data.ContainsKey("player") &&
		data.ContainsKey("upgrades");

	private void Apply(Dictionary data)
	{
		var player  = data["player"].AsGodotDictionary();
		var upgDict = data["upgrades"].AsGodotDictionary();
		var invDict = data["inventory"].AsGodotDictionary();

		EconomyManager.Instance.AddCoins((int)player["coins"]);
		GameManager.Instance.TotalDives = (int)player["totalDives"];

		foreach (var key in upgDict.Keys)
			UpgradeManager.Instance.SetUpgradeLevel(key.AsString(), upgDict[key].AsInt32());

		foreach (var key in invDict.Keys)
			EconomyManager.Instance.AddToInventory(key.AsString(), invDict[key].AsInt32());
	}

	private bool TryRestoreBackup()
	{
		if (!FileAccess.FileExists(BackupPath)) return false;
		var data = ReadJson(BackupPath);
		if (!Validate(data)) return false;
		Apply(data);
		return true;
	}

	private static Dictionary ReadJson(string path)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		return Json.ParseString(file.GetAsText()).AsGodotDictionary();
	}
}
