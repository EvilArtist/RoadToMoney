using Godot;

/// <summary>
/// Dữ liệu của 1 category upgrade (Bag/Tool/Oxygen/Light...).
/// Tạo file .tres từ resource này trong Editor: Right click FileSystem →
/// New Resource → UpgradeCategoryData, rồi điền các field trong Inspector.
/// </summary>
[GlobalClass]
public partial class UpgradeCategoryData : Resource
{
	// Phải khớp với key trong UpgradeManager.Upgrades ("bag", "tool", "oxygen", "light")
	[Export] public string CategoryKey { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string Icon        { get; set; } = "";

	// Index = level (0-based). Costs.Length = số cấp tối đa của category này.
	[Export] public UpgradeItemData[] UpgradeItems { get; set; } = System.Array.Empty<UpgradeItemData>();
}
