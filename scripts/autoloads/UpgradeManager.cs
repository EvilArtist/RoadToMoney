using Godot;
using System.Collections.Generic;

public partial class UpgradeManager : Node
{
	public static UpgradeManager Instance { get; private set; }

	// ── Resource arrays — gán .tres trong Inspector. ───────────────────────────
	// Index 0 = tier baseline MIỄN PHÍ (Level=0, Cost=0 — đã sở hữu sẵn, không
	// cần mua: 10kg cho bag, Hand cho tool, v.v...). Index 1+ = các cấp phải mua,
	// mỗi cấp tăng dần. Mua upgrade = nhảy sang phần tử kế tiếp trong mảng.
	[Export] public UpgradeItemData[] BagLevels    = { UpgradeItemData.DefaultBag()    };
	[Export] public UpgradeItemData[] ToolLevels   = { UpgradeItemData.DefaultTool()   };
	[Export] public UpgradeItemData[] OxygenLevels = { UpgradeItemData.DefaultOxygen() };
	[Export] public UpgradeItemData[] LightLevels  = { UpgradeItemData.DefaultLight()  };

	// Trạng thái hiện tại của từng category — lưu TRỰC TIẾP UpgradeItemData đang
	// active, không phải số đếm cấp nữa (UpgradeItemData đã có sẵn Level/Value/...
	// nên không cần giữ thêm 1 bản trạng thái int riêng).
	public Dictionary<string, UpgradeItemData> Upgrades { get; private set; } = new();

	private Dictionary<string, UpgradeItemData[]> _levelsByCategory;

	public override void _Ready()
	{
		Instance = this;

		InitCategories();
	}

	private void InitCategories()
	{
		_levelsByCategory = new Dictionary<string, UpgradeItemData[]>
		{
			{ "bag",    BagLevels    },
			{ "tool",   ToolLevels   },
			{ "oxygen", OxygenLevels },
			{ "light",  LightLevels  },
		};

		// Mọi category bắt đầu ở tier baseline (index 0) — đã sở hữu sẵn, miễn phí.
		foreach (var kv in _levelsByCategory)
			Upgrades[kv.Key] = kv.Value.Length > 0 ? kv.Value[0] : null;
	}

	// ── Getters dùng trực tiếp UpgradeItemData hiện tại ─────────────────────────
	public float GetBagCapacity()    => Upgrades["bag"]?.Value    ?? 0f;
	public float GetOxygenDuration() => Upgrades["oxygen"]?.Value ?? 0f;
	// Value của "light" = bán kính nhìn rõ — dùng trực tiếp cho NearVisibilityLight
	// trong SwimController, không qua resource riêng nào khác.
	public float GetLightRange()     => Upgrades["light"]?.Value  ?? 0f;

	// ── Tool getters: theo yêu cầu — GetToolName→Description, GetToolRadius→Value,
	// GetToolBonus→Level * 0.25 (THAY ĐỔI CÂN BẰNG so với bảng cũ {0,0.25,0.5,0.4}:
	// giờ Vacuum Trap (level3) = 0.75 thay vì 0.40 — kiểm tra lại số liệu .tres nếu
	// muốn giữ balance cũ). ─────────────────────────────────────────────────────
	public int    GetToolLevel()  => Upgrades["tool"]?.Level ?? 0;
	public string GetToolName()   => Upgrades["tool"]?.Description ?? "Hand";
	public float  GetToolRadius() => Upgrades["tool"]?.Value        ?? 1.5f;
	public float  GetToolBonus()  => GetToolLevel() * 0.25f;

	public bool CanDiveTo(string zone)
	{
		int oxygenLv = Upgrades["oxygen"]?.Level ?? 0;
		int lightLv  = Upgrades["light"]?.Level  ?? 0;
		int bagLv    = Upgrades["bag"]?.Level     ?? 0;

		return zone switch
		{
			"Zone_Shallow" => true,
			"Zone_Reef"    => oxygenLv >= 1,
			"Zone_Deep"    => oxygenLv >= 3 && lightLv >= 1,
			"Zone_Abyss"   => oxygenLv >= 5 && lightLv >= 3 && bagLv >= 4,
			_              => false
		};
	}

	public void ApplyUpgrade(string category, UpgradeItemData item)
	{
		Upgrades[category] = item;
		EventBus.Instance.EmitUpgradePurchased(category, Upgrades[category]);
	}

	public void ResetTools()
	{
		InitCategories();
	}

	/// <summary>
	/// Set trực tiếp level theo số nguyên — dùng khi load save file (chỉ lưu int,
	/// không lưu nguyên Resource). Tìm phần tử có Level khớp trong mảng category;
	/// nếu không khớp (VD save cũ/level vượt số tier hiện có) thì fallback dùng
	/// level làm index, clamp trong giới hạn mảng.
	/// </summary>
	public void SetUpgradeLevel(string category, int level)
	{
		if (!_levelsByCategory.TryGetValue(category, out var levels) || levels.Length == 0) return;

		var match = System.Array.Find(levels, item => item != null && item.Level == level);
		Upgrades[category] = match ?? levels[Mathf.Clamp(level, 0, levels.Length - 1)];
	}

	/// <summary>Lấy level hiện tại dạng int — dùng khi SaveSystem ghi save file.</summary>
	public int GetUpgradeLevel(string category) => Upgrades.TryGetValue(category, out var item) ? (item?.Level ?? 0) : 0;
}
