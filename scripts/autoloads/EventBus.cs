using Godot;
using System;

public partial class EventBus : Node
{
	public static EventBus Instance { get; private set; }

	public event Action<string, float, int> ResourceCollected;
	public event Action<float>       InventoryChanged;
	public event Action<int>         CoinsChanged;
	public event Action<string, UpgradeItemData> UpgradePurchased;
	public event Action              DiveStarted;
	public event Action              DiveEnded;
	public event Action<float,float> OxygenChanged;
	public event Action              OxygenCritical;
	public event Action              PlayerSurfaced;
	public event Action              GameSaved;
	public event Action PlayerDrowned;
	public event Action<string, Vector3> ResourceCaughtFx; 
	public event Action<Vector3>         CatchMissed;
	public event Action<float>                DayTimeChanged;   // giờ hiện tại (0-24)
	public event Action<int>                  DayChanged;       // số ngày mới
	public event Action<DayNightManager.Period> DayPeriodChanged; // đổi buổi (Sáng/Trưa/Chiều/Tối)  

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
	}

	public void EmitResourceCollected(string id, float weight, int qty) => ResourceCollected?.Invoke(id, weight, qty);
	public void EmitInventoryChanged(float resourceWeight)=> InventoryChanged?.Invoke(resourceWeight);
	public void EmitCoinsChanged(int amount)              => CoinsChanged?.Invoke(amount);
	public void EmitUpgradePurchased(string id, UpgradeItemData data) => UpgradePurchased?.Invoke(id, data);
	public void EmitDiveStarted()                         => DiveStarted?.Invoke();
	public void EmitDiveEnded()                           => DiveEnded?.Invoke();
	public void EmitOxygenChanged(float cur, float max)   => OxygenChanged?.Invoke(cur, max);
	public void EmitOxygenCritical()                      => OxygenCritical?.Invoke();
	public void EmitPlayerSurfaced()                      => PlayerSurfaced?.Invoke();
	public void EmitGameSaved()                           => GameSaved?.Invoke();
	public void EmitPlayerDrowned() => PlayerDrowned?.Invoke();
	public void EmitResourceCaughtFx(string id, Vector3 pos) => ResourceCaughtFx?.Invoke(id, pos);
	public void EmitCatchMissed(Vector3 pos)                 => CatchMissed?.Invoke(pos);
	public void EmitDayTimeChanged(float hour)                       => DayTimeChanged?.Invoke(hour);
	public void EmitDayChanged(int day)                              => DayChanged?.Invoke(day);
	public void EmitDayPeriodChanged(DayNightManager.Period period)  => DayPeriodChanged?.Invoke(period);
}
