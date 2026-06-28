using Godot;

/// <summary>
/// Dữ liệu của 1 CẤP trong 1 category upgrade (Bag/Oxygen/Light...).
/// Value mang ý nghĩa khác nhau theo category: capacity (bag), duration (oxygen),
/// bán kính nhìn rõ (light — dùng trực tiếp cho NearVisibilityLight, không qua
/// resource riêng nào khác).
/// Tạo file .tres: chuột phải FileSystem → New Resource → UpgradeItemData.
/// </summary>
[GlobalClass]
public partial class UpgradeItemData : Resource
{
	[Export] public int    Cost        { get; set; } = 0;
	[Export] public string Description { get; set; } = "";
	[Export] public float  Value       { get; set; } = 0f;
	[Export] public int    Level       { get; set; } = 0;

	// ── Fallback an toàn khi quên gán resource trong Inspector ─────────────────
	// Chỉ dùng làm "lưới an toàn" tránh crash — số liệu thật phải lấy từ .tres.
	// Level=0, Cost=0: đại diện tier baseline miễn phí (đã sở hữu sẵn, không cần mua).
	public static UpgradeItemData DefaultBag() => new UpgradeItemData
	{
		Level       = 0,
		Cost        = 0,
		Description = "10kg",
		Value       = 10f,
	};

	public static UpgradeItemData DefaultOxygen() => new UpgradeItemData
	{
		Level       = 0,
		Cost        = 0,
		Description = "60s",
		Value       = 60f,
	};

	public static UpgradeItemData DefaultLight() => new UpgradeItemData
	{
		Level       = 0,
		Cost        = 0,
		Description = "None",
		Value       = 0f, // bán kính nhìn rõ mặc định khi chưa mua đèn — NearVisibilityLight vẫn có base riêng trong SwimController
	};

	public static UpgradeItemData DefaultTool() => new UpgradeItemData
	{
		Level       = 0,
		Cost        = 0,
		Description = "Hand",
		Value       = 1.5f, // bán kính bắt mặc định (tay không)
	};
}