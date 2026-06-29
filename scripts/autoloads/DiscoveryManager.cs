using Godot;
using System.Collections.Generic;

/// <summary>
/// DiscoveryEntry — trạng thái "đã khám phá" cho 1 loại SeaResource.
///
/// Lưu thời điểm bắt lần đầu dưới dạng Unix timestamp (giờ THỰC của máy người chơi,
/// lấy từ Time.GetUnixTimeFromSystem() — giống cách SaveSystem đang lưu "timestamp"),
/// vì Godot/C# không export được kiểu System.DateTime trực tiếp trên Resource.
/// UI (BookScreen) sẽ convert lại thành DateTime để hiển thị theo định dạng ngôn ngữ.
/// </summary>
[GlobalClass]
public partial class DiscoveryEntry : Resource
{
	[Export] public bool Discovered { get; set; } = false;
	[Export] public long FirstCaughtUnixTime { get; set; } = 0;
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
			entry.Discovered           = true;
			entry.FirstCaughtUnixTime  = (long)Time.GetUnixTimeFromSystem();
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
