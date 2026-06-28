using Godot;
using System;

/// <summary>
/// DayNightManager — đồng bộ giờ trong game với giờ thực của máy người chơi.
/// Không còn là bộ đếm tích lũy, không cần pause khi lặn (vì đây là phép map
/// trực tiếp từ DateTime.Now, không phải counter — pause/resume không có nghĩa).
/// </summary>
public partial class DayNightManager : Node
{
	public static DayNightManager Instance { get; private set; }

	public enum Period { Morning, Noon, Afternoon, Night }

	[ExportGroup("Period Boundaries (giờ 24h, theo giờ thực)")]
	[Export] public float MorningStartHour   = 5f;
	[Export] public float NoonStartHour      = 11f;
	[Export] public float AfternoonStartHour = 13f;
	[Export] public float NightStartHour     = 18f;

	public float  CurrentHour   { get; private set; }
	public Period CurrentPeriod { get; private set; } = Period.Morning;

	// "Ngày" giờ là số ngày thực đã trôi qua kể từ lần đầu chơi — dùng cho Book #12
	// (hiển thị "bắt lần đầu Ngày N" có ý nghĩa), persist qua SaveSystem.
	public int CurrentDay => (DateTime.Now.Date - _epoch.Date).Days + 1;

	private DateTime _epoch;

	public override void _Ready()
	{
		Instance    = this;
		ProcessMode = ProcessModeEnum.Always;
		_epoch      = DateTime.Now; // mặc định lần đầu chạy — SaveSystem sẽ override qua LoadEpoch()
		RefreshFromSystemClock();
	}

	public override void _Process(double delta) => RefreshFromSystemClock();

	private void RefreshFromSystemClock()
	{
		var now = DateTime.Now;
		CurrentHour = now.Hour + now.Minute / 60f + now.Second / 3600f;

		EventBus.Instance.EmitDayTimeChanged(CurrentHour);

		var period = GetPeriodForHour(CurrentHour);
		if (period != CurrentPeriod)
		{
			CurrentPeriod = period;
			EventBus.Instance.EmitDayPeriodChanged(period);
		}
	}

	public Period GetPeriodForHour(float hour)
	{
		if (hour >= MorningStartHour   && hour < NoonStartHour)      return Period.Morning;
		if (hour >= NoonStartHour      && hour < AfternoonStartHour) return Period.Noon;
		if (hour >= AfternoonStartHour && hour < NightStartHour)     return Period.Afternoon;
		return Period.Night;
	}

	// ── Lighting "nền" theo giờ — KHÔNG đổi gì, SwimController/SkyController
	// vẫn gọi 2 hàm này như cũ, không cần sửa lại 2 file đó ───────────────────
	public float GetSunEnergyForTime()
	{
		float h = CurrentHour;
		return CurrentPeriod switch
		{
			Period.Morning   => Mathf.Lerp(0.3f, 1.0f, SubProgress(h, MorningStartHour, NoonStartHour)),
			Period.Noon      => 1.2f,
			Period.Afternoon => Mathf.Lerp(1.2f, 0.5f, SubProgress(h, AfternoonStartHour, NightStartHour)),
			_                => 0.05f,
		};
	}

	public float GetAmbientEnergyForTime()
	{
		float h = CurrentHour;
		return CurrentPeriod switch
		{
			Period.Morning   => Mathf.Lerp(0.45f, 0.85f, SubProgress(h, MorningStartHour, NoonStartHour)),
			Period.Noon      => 0.85f,
			Period.Afternoon => Mathf.Lerp(0.85f, 0.55f, SubProgress(h, AfternoonStartHour, NightStartHour)),
			_                => 0.25f,
		};
	}

	private float SubProgress(float hour, float start, float end)
		=> Mathf.Clamp((hour - start) / Mathf.Max(0.01f, end - start), 0f, 1f);

	// ── SaveSystem: lưu/khôi phục epoch để CurrentDay tính đúng qua nhiều session ──
	public void LoadEpoch(DateTime epoch) => _epoch = epoch;
	public DateTime GetEpoch() => _epoch;

	public string GetPeriodLabelKey(Period p) => p switch
	{
		Period.Morning   => "PERIOD_MORNING",
		Period.Noon      => "PERIOD_NOON",
		Period.Afternoon => "PERIOD_AFTERNOON",
		_                => "PERIOD_NIGHT",
	};
}
