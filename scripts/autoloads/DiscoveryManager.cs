using Godot;
using System.Collections.Generic;

/// <summary>
/// DiscoveryEntry — trạng thái "đã khám phá" cho 1 loại SeaResource.
///
/// LƯU Ý: bản pseudocode gốc dùng `Date FirstCaughtDate`, nhưng Godot/C# không có
/// kiểu `Date` export được. Thay vào đó ta lưu `FirstCaughtDay` (số ngày, lấy từ
/// DayNightManager.CurrentDay — đã có sẵn cho Book #12) và `FirstCaughtPeriod`
/// (Sáng/Trưa/Chiều/Tối) — đúng tinh thần "Bắt lần đầu: Ngày N, {period}" mà UI cần.
/// </summary>
[GlobalClass]
public partial class DiscoveryEntry : Resource
{
	[Export] public bool Discovered { get; set; } = false;
	[Export] public int  FirstCaughtDay { get; set; } = 0;
	[Export] public DayNightManager.Period FirstCaughtPeriod { get; set; } = DayNightManager.Period.Morning;
	[Export] public int  TimesCaught { get; set; } = 0;
}

/// <summary>
/// DiscoveryManager — Book #12: theo dõi những loài hải sản người chơi đã từng bắt được.
/// Tự lắng nghe EventBus.ResourceCollected nên không cần sửa CollectionInteraction.cs.
/// </summary>
[GlobalClass]
public partial class DiscoveryManager : Node
{
	public static DiscoveryManager Instance { get; private set; }

	private Dictionary<string, DiscoveryEntry> _entries = new();

	public override void _Ready()
	{
		Instance = this;
		EventBus.Instance.ResourceCollected += OnResourceCollected;
	}

	private void OnResourceCollected(string resourceId, float weight, int qty)
	{
		bool isFirst = RecordCatch(resourceId, qty);
		if (isFirst)
		{
			var res = GD.Load<SeaResource>($"res://resources/items/{resourceId}.tres");
			string name = res != null ? Tr(res.DisplayName) : resourceId;
			FloatingMessage.Show(GetTree(), "📖 " + Tr("NEW_DISCOVERY") + ": " + name, new Color(0.55f, 0.95f, 0.8f));
			EventBus.Instance.EmitDiscoveryUnlocked(resourceId);
		}
	}

	/// <summary>
	/// Ghi nhận 1 lần bắt được resourceId. Trả về true nếu đây là lần đầu (mới khám phá).
	/// </summary>
	public bool RecordCatch(string resourceId, int qty = 1)
	{
		if (!_entries.TryGetValue(resourceId, out var entry))
		{
			entry = new DiscoveryEntry();
			_entries[resourceId] = entry;
		}

		bool isFirst = !entry.Discovered;
		if (isFirst)
		{
			entry.Discovered         = true;
			entry.FirstCaughtDay     = DayNightManager.Instance.CurrentDay;
			entry.FirstCaughtPeriod  = DayNightManager.Instance.CurrentPeriod;
		}
		entry.TimesCaught += qty;
		return isFirst;
	}

	public DiscoveryEntry GetEntry(string resourceId) =>
		_entries.TryGetValue(resourceId, out var e) ? e : new DiscoveryEntry();

	// ── Export/Import cho SaveSystem ────────────────────────────────────────
	public Dictionary<string, DiscoveryEntry> GetAllEntries() => _entries;
	public void LoadEntries(Dictionary<string, DiscoveryEntry> data) => _entries = data ?? new();
}
