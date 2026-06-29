using Godot;

/// <summary>
/// SkyController — xoay Sun/Moon, đổi màu sky + mặt biển theo giờ.
/// KHÔNG đụng Sun.LightEnergy / AmbientLightEnergy (SwimController vẫn làm chủ 2 cái
/// đó theo độ sâu) và KHÔNG đụng view_depth của water shader (SwimController cũng giữ).
/// Chỉ chỉnh: rotation Sun/Moon, SkyMode (ẩn disc khi quá tối), màu sky, màu nước.
/// </summary>
public partial class SkyController : Node3D
{
	[Export] public NodePath SunPath  = "../Sun";
	[Export] public NodePath MoonPath = "../Moon";

	[Export] public float MoonLightEnergy = 0.35f;
	private static readonly Color MoonLightColor = new Color(0.55f, 0.65f, 0.85f);

	private DirectionalLight3D _sun;
	private DirectionalLight3D _moon;
	private ShaderMaterial _skyMat; 
	private ShaderMaterial _waterMaterial;

	public override void _Ready()
	{
		_sun  = GetNodeOrNull<DirectionalLight3D>(SunPath);
		_moon = GetNodeOrNull<DirectionalLight3D>(MoonPath);


		var worldEnv = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
		if (worldEnv != null)
			_skyMat = worldEnv.Environment.Sky.SkyMaterial as ShaderMaterial;


		var waterPlane = GetTree().Root.FindChild("WaterPlane", true, false) as MeshInstance3D;
		if (waterPlane != null)
			_waterMaterial = waterPlane.GetSurfaceOverrideMaterial(0) as ShaderMaterial;


		if (_sun == null)
		{
			return; // tránh NullReferenceException ở OnDayTimeChanged
		}

		if (_moon != null)
		{
			_moon.LightColor    = MoonLightColor;
			_moon.LightEnergy   = 0f;
			_moon.ShadowEnabled = false;
		}

		EventBus.Instance.DayTimeChanged += OnDayTimeChanged;
		OnDayTimeChanged(DayNightManager.Instance.CurrentHour);
	}

	private void OnDayTimeChanged(float hour)
	{
		var period = DayNightManager.Instance.CurrentPeriod;

		// ── Rotation ──────────────────────────────────────────────────────────
		float sunAngleDeg = (hour / 24f) * 360f - 90f;
		_sun.RotationDegrees = new Vector3(sunAngleDeg, _sun.RotationDegrees.Y, 0f);

		float dayBaseSunEnergy = DayNightManager.Instance.GetSunEnergyForTime();

		// ── Ẩn disc khi quá tối (tránh "quả cầu đen") — light vẫn chiếu sáng scene
		// bình thường qua LightOnly, chỉ không vẽ disc lên ProceduralSkyMaterial ──
		_sun.SkyMode = dayBaseSunEnergy > 0.15f
			? DirectionalLight3D.SkyModeEnum.LightAndSky
			: DirectionalLight3D.SkyModeEnum.LightOnly;

		if (_moon != null)
		{
			_moon.RotationDegrees = new Vector3(sunAngleDeg + 180f, _moon.RotationDegrees.Y, 0f);

			float targetMoonEnergy = period == DayNightManager.Period.Night ? MoonLightEnergy : 0f;
			_moon.LightEnergy = Mathf.Lerp(_moon.LightEnergy, targetMoonEnergy, 0.08f);

			_moon.SkyMode = period == DayNightManager.Period.Night
				? DirectionalLight3D.SkyModeEnum.LightAndSky
				: DirectionalLight3D.SkyModeEnum.LightOnly;
		}

		// ── Màu sky ───────────────────────────────────────────────────────────
		if (_skyMat != null)
		{
			float curCloud     = _skyMat.GetShaderParameter("cloud_coverage").AsSingle();
			float curWhiteMix  = _skyMat.GetShaderParameter("cloud_white_mix").AsSingle();

			float targetCloud    = GetCloudCoverageForPeriod(period);
			float targetWhiteMix = period == DayNightManager.Period.Noon ? 1f : 0f;

			_skyMat.SetShaderParameter("cloud_coverage", Mathf.Lerp(curCloud, targetCloud, 0.05f));
			_skyMat.SetShaderParameter("cloud_white_mix", Mathf.Lerp(curWhiteMix, targetWhiteMix, 0.05f));
		}
		// ── Màu mặt biển ──────────────────────────────────────────────────────
		// CHỈ đổi shallow_color/deep_color/sky_tint — KHÔNG đụng view_depth
		// (SwimController vẫn set view_depth riêng theo độ sâu mỗi physics frame).
		if (_waterMaterial != null)
		{
			var (shallow, deep, tint) = GetWaterColorsForHour(period);

			Color curShallow = _waterMaterial.GetShaderParameter("shallow_color").AsColor();
			Color curDeep    = _waterMaterial.GetShaderParameter("deep_color").AsColor();
			Vector3 curTint  = _waterMaterial.GetShaderParameter("sky_tint").AsVector3();

			_waterMaterial.SetShaderParameter("shallow_color", curShallow.Lerp(shallow, 0.05f));
			_waterMaterial.SetShaderParameter("deep_color",    curDeep.Lerp(deep, 0.05f));
			_waterMaterial.SetShaderParameter("sky_tint",
				curTint.Lerp(new Vector3(tint.R, tint.G, tint.B), 0.05f));
		}
	}
	// Method GetCloudCoverageForPeriod — tăng hẳn lên, đặc biệt Sáng/Chiều
	private float GetCloudCoverageForPeriod(DayNightManager.Period period) => period switch
	{
		DayNightManager.Period.Morning   => 0.75f,
		DayNightManager.Period.Noon      => 0.55f, // mây trắng, để nhẹ hơn cho trời quang
		DayNightManager.Period.Afternoon => 0.80f, // chiều mây dày nhất, nhuốm hoàng hôn rõ
		_                                  => 0f,
	};

	private (Color top, Color horizon) GetSkyColorsForHour(float hour, DayNightManager.Period period)
	{
		var morningTop = new Color(0.45f, 0.65f, 0.85f);
		var morningHor = new Color(0.85f, 0.75f, 0.55f);

		var noonTop = new Color(0.25f, 0.55f, 0.85f);
		var noonHor = new Color(0.55f, 0.72f, 0.85f); // khớp giá trị gốc của bạn

		var afternoonTop = new Color(0.35f, 0.45f, 0.65f);
		var afternoonHor = new Color(0.85f, 0.55f, 0.35f);

		var nightTop = new Color(0.02f, 0.03f, 0.08f);
		var nightHor = new Color(0.05f, 0.07f, 0.15f);

		switch (period)
		{
			case DayNightManager.Period.Morning:
				float tM = Mathf.Clamp((hour - 5f) / 6f, 0f, 1f);
				return (morningTop.Lerp(noonTop, tM), morningHor.Lerp(noonHor, tM));
			case DayNightManager.Period.Noon:
				return (noonTop, noonHor);
			case DayNightManager.Period.Afternoon:
				float tA = Mathf.Clamp((hour - 13f) / 5f, 0f, 1f);
				return (noonTop.Lerp(afternoonTop, tA), noonHor.Lerp(afternoonHor, tA));
			default:
				return (nightTop, nightHor);
		}
	}

	private (Color shallow, Color deep, Color tint) GetWaterColorsForHour(DayNightManager.Period period)
	{
		// Noon = giữ đúng giá trị gốc bạn đã tinh chỉnh tay trong ShaderMaterial_ht4wx
		switch (period)
		{
			case DayNightManager.Period.Morning:
				return (
					new Color(0.06f, 0.30f, 0.50f, 0.88f),
					new Color(0.03f, 0.13f, 0.25f, 0.95f),
					new Color(0.75f, 0.70f, 0.55f));
			case DayNightManager.Period.Noon:
				return (
					new Color(0.04f, 0.28f, 0.48f, 0.88f),
					new Color(0.02f, 0.10f, 0.22f, 0.95f),
					new Color(0.55f, 0.72f, 0.85f));
			case DayNightManager.Period.Afternoon:
				return (
					new Color(0.07f, 0.20f, 0.35f, 0.90f),
					new Color(0.035f, 0.10f, 0.20f, 0.95f),
					new Color(0.80f, 0.55f, 0.38f));
			default: // Night
				return (
					new Color(0.01f, 0.05f, 0.12f, 0.95f),
					new Color(0.005f, 0.02f, 0.06f, 0.98f),
					new Color(0.05f, 0.07f, 0.15f));
		}
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
			EventBus.Instance.DayTimeChanged -= OnDayTimeChanged;
	}
}
