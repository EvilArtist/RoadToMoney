using Godot;
using Godot.Collections;
using System;

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

		var discoveries = new Dictionary();

		foreach (var kv in DiscoveryManager.Instance.GetAllEntries())
		{
			discoveries[kv.Key] = new Dictionary
			{
				["discovered"]   = kv.Value.Discovered,
				["day"]          = kv.Value.FirstCaughtDay,
				["period"]       = (int)kv.Value.FirstCaughtPeriod,
				["timesCaught"]  = kv.Value.TimesCaught
			};
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
			["world"] = new Dictionary
			{
				["epoch"] = DayNightManager.Instance.GetEpoch().ToString("o") // ISO 8601
			},
			["upgrades"]  = upgrades,
			["inventory"] = inventory,
			["discoveries"] = discoveries
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
		
		if (data.ContainsKey("world"))
		{
			var world = data["world"].AsGodotDictionary();
			if (DateTime.TryParse(world["epoch"].AsString(), out var epoch))
				DayNightManager.Instance.LoadEpoch(epoch);
		}

		// Book #12 — backward-compatible: save cũ chưa có key "discoveries" thì bỏ qua,
		// sổ tay sẽ bắt đầu trống (không crash, không mất save cũ).
		if (data.ContainsKey("discoveries"))
		{
			var discDict = data["discoveries"].AsGodotDictionary();
			var entries  = new System.Collections.Generic.Dictionary<string, DiscoveryEntry>();

			foreach (var key in discDict.Keys)
			{
				var entryDict = discDict[key].AsGodotDictionary();
				entries[key.AsString()] = new DiscoveryEntry
				{
					Discovered        = entryDict["discovered"].AsBool(),
					FirstCaughtDay    = entryDict["day"].AsInt32(),
					FirstCaughtPeriod = (DayNightManager.Period)entryDict["period"].AsInt32(),
					TimesCaught       = entryDict["timesCaught"].AsInt32()
				};
			}

			DiscoveryManager.Instance.LoadEntries(entries);
		}
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
