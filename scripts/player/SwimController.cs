using Godot;

public partial class SwimController : CharacterBody3D
{
	[Export] public float SwimSpeed        = 6.0f;
	[Export] public float Drag             = 0.08f;
	[Export] public float Inertia          = 0.92f;
	[Export] public float MouseSensitivity = 0.002f;
	[Export] public float TiltAngleMax     = 15.0f;
	[Export] public float GravityStrength = 2.0f;

	[ExportGroup("Near Visibility")]
	// Bán kính nhìn rõ quanh Player = UpgradeManager.Instance.GetLightRange() (Value
	// của cấp đèn hiện tại — dùng trực tiếp làm RadiusByLevel, không qua resource
	// riêng nào khác) - DepthFalloffPerBand mỗi DepthBandSize mét độ sâu (tối thiểu 0).
	[Export] public float DepthBandSize       = 10.0f; // mỗi band độ sâu (m)
	[Export] public float DepthFalloffPerBand = 5.0f;  // trừ bán kính mỗi band (m)
	[Export] public float NearVisibilityEnergy = 10f; // tăng từ 3.5 — vẫn còn yếu ở bán kính 5-25m
	[Export] public float NearVisibilityAttenuation = 1.0f; // giảm từ 1.4 — sáng đều hơn, không tối nhanh ở rìa range
	private Color NearVisibilityColor = new Color(0.65f, 0.85f, 1.0f);



	private const float BaseFogDensity = 0.25f;   // density cho tầm nhìn ~5-7m — tinh chỉnh tay trong editor
	private const float DeepFogBonus   = 0.10f;   // murky thêm khi xuống rất sâu (0-50m)
	// Seabed dốc theo Z (Utils.GetFloorY: -10 - |z|*0.1405), nên ở rìa vùng spawn prop
	// (z≈±400, xem EnvironmentSpawner) floorY ≈ -66m. MaxDepthInvisible PHẢI ≥ độ sâu
	// lớn nhất có thể chạm tới, nếu không player sẽ bị tối đen từ rất sớm (chỉ ~140m
	// theo Z) dù vẫn còn rất nhiều nội dung (cá, coral, prop) phía trước chưa lặn tới.
	private const float MaxDepthInvisible = 75.0f; // Độ sâu visibility = 0 (khớp đáy sâu nhất ~66m)
	private Color NearColor = new Color(0.2f, 0.65f, 0.90f);
	private Color FarColor  = new Color(0.15f,  0.42f, 0.58f);

	private Node3D       _cameraRig;
	private Camera3D     _camera;
	private SpotLight3D  _divingLight;
	private OmniLight3D  _nearVisibilityLight;

	private Vector3 _swimVelocity = Vector3.Zero;
	private float   _cameraPitch  = 0f;
	private bool    _isUnderwater = false;

	private OxygenSystem _oxygenSystem;
	private StorageBag   _storageBag;
	private Environment _worldEnv;
	private Sky _defaultSky;
	private ShaderMaterial _waterMaterial;
	private GpuParticles3D _bubbleTrail;
	public override void _Ready()
	{
		_cameraRig    = GetNode<Node3D>("CameraRig");
		_camera       = GetNode<Camera3D>("CameraRig/Camera3D");
		_divingLight  = GetNode<SpotLight3D>("CameraRig/DivingLight");
		_oxygenSystem = GetNode<OxygenSystem>("OxygenSystem");
		_storageBag   = GetNode<StorageBag>("StorageBag");
		_bubbleTrail = GetNode<GpuParticles3D>("CameraRig/BubbleTrail");

		Input.MouseMode   = Input.MouseModeEnum.Visible;
		_divingLight.Visible = false;
		_bubbleTrail.Emitting = false;

		InitNearVisibilityLight();

		// Bắt đầu ở surface state
		// GameManager.Instance.ChangeState(GameManager.GameState.Surface);
		var worldEnv = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
		if (worldEnv != null)
		{
			_worldEnv = worldEnv.Environment;
			_defaultSky = _worldEnv.Sky;
		}

		var waterPlane = GetTree().Root.FindChild("WaterPlane", true, false) as MeshInstance3D;
		if (waterPlane != null)
			_waterMaterial = waterPlane.GetSurfaceOverrideMaterial(0) as ShaderMaterial;
	}

	// Fill light luôn bật quanh Player — đảm bảo nhìn rõ trong NearVisibilityRadius
	// bất kể độ sâu hay đã nâng cấp đèn hay chưa. Tạo bằng code để không cần
	// chỉnh tay trong scene .tscn.
	private void InitNearVisibilityLight()
	{
		_nearVisibilityLight = new OmniLight3D();
		_nearVisibilityLight.Name          = "NearVisibilityLight";
		_nearVisibilityLight.LightColor    = NearVisibilityColor;
		_nearVisibilityLight.LightEnergy   = NearVisibilityEnergy;
		float initialRadius = UpgradeManager.Instance?.GetLightRange() ?? 5.0f; // fallback nếu Autoload chưa kịp _Ready
		_nearVisibilityLight.OmniRange = initialRadius; // sẽ được tính lại mỗi frame trong UpdateDepthEffects
		_nearVisibilityLight.OmniAttenuation = NearVisibilityAttenuation; // fade tự nhiên, không cắt cụt
		_nearVisibilityLight.Visible       = true;
		_cameraRig.AddChild(_nearVisibilityLight);
		_nearVisibilityLight.Position = Vector3.Zero; // đặt ngay tại vị trí camera
	}

	public override void _Input(InputEvent @event)
	{
		// Xoay camera theo chuột — luôn hoạt động
		if (@event is InputEventMouseMotion motion && GameManager.Instance.CurrentState == GameManager.GameState.Diving)
		{
			RotateY(-motion.Relative.X * MouseSensitivity);

			_cameraPitch -= motion.Relative.Y * MouseSensitivity;
			_cameraPitch  = Mathf.Clamp(
				_cameraPitch,
				Mathf.DegToRad(-80f),
				Mathf.DegToRad(80f)
			);
			_cameraRig.Rotation = new Vector3(
				_cameraPitch, 0f, _cameraRig.Rotation.Z
			);
		}

	}
	private bool _initialDive = false;
	private float _initialDiveTarget = -2f;

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		var state = GameManager.Instance.CurrentState;

		if (state == GameManager.GameState.Surface)
		{
			 var pos = GlobalPosition;
			pos.Y = 0.5f;
			GlobalPosition = pos;

			_swimVelocity = Vector3.Zero;
			Velocity      = Vector3.Zero;
			// MoveAndSlide();
			UpdateDepthEffects(); 
			if (Input.IsActionJustPressed("swim_up"))
			{
				_initialDive = true;  // bắt đầu phase chìm xuống
				GameManager.Instance.ChangeState(GameManager.GameState.Diving);
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			return;
		}

		if (state != GameManager.GameState.Diving) return;

		// ── Phase 1: Tự động chìm xuống 2m ──────────────────────────
		if (_initialDive)
		{
			_swimVelocity = new Vector3(0f, -4f, 0f);  // chìm thẳng xuống

			if (GlobalPosition.Y <= _initialDiveTarget)
				_initialDive = false;  // xong phase 1, bắt đầu bơi tự do
		}
		// ── Phase 2: Bơi theo hướng camera ───────────────────────────
		else if (Input.IsActionPressed("swim_up"))
		{
			var forward = -_cameraRig.GlobalTransform.Basis.Z;
			_swimVelocity = _swimVelocity.Lerp(forward * SwimSpeed, Drag);
		}
		else
		{
			_swimVelocity = _swimVelocity.Lerp(Vector3.Zero, 1f - Inertia);
		}

		// Camera tilt
		float tiltTarget = 0f;
		var   right      = GlobalTransform.Basis.X;
		float lateralDot = _swimVelocity.Dot(right);
		tiltTarget = Mathf.Clamp(-lateralDot * 0.05f,
			-Mathf.DegToRad(TiltAngleMax),
			 Mathf.DegToRad(TiltAngleMax));
		float newZ = Mathf.Lerp(_cameraRig.Rotation.Z, tiltTarget, dt * 3f);
		_cameraRig.Rotation = new Vector3(_cameraPitch, 0f, newZ);

		Velocity = _swimVelocity;
		// Gravity nhẹ kéo xuống khi không bơi
		// if (!_initialDive)
		// 	_swimVelocity.Y -= GravityStrength * (float)delta;

		// Clamp — không cho vượt quá mặt nước
		if (GlobalPosition.Y > -0.3f && _swimVelocity.Y > 0f)
			_swimVelocity.Y = 0f;
		float seabedY = Utils.GetFloorY(GlobalPosition.X, GlobalPosition.Z);
		if (GlobalPosition.Y <= seabedY)
		{
			var pos = GlobalPosition;
			pos.Y = seabedY;
			GlobalPosition = pos;
			_swimVelocity.Y = 0f;
		}

		Velocity = _swimVelocity;
		MoveAndSlide();

		if (IsOnFloor())
			_swimVelocity.Y = 0f;
		MoveAndSlide();

		CheckSurface();
		UpdateDepthEffects();
		// UpdateBubble();
	}
	
	private void UpdateBubble() {
		// Bong bóng chỉ hiện khi đang bơi dưới nước
		if (_bubbleTrail != null)
			_bubbleTrail.Emitting = _isUnderwater && _swimVelocity.Length() > 0.5f;
	}

	private void CheckSurface()
	{
		bool wasUnderwater = _isUnderwater;
		_isUnderwater = GlobalPosition.Y < -0.5f;

		if (wasUnderwater && !_isUnderwater)
		{
			_swimVelocity = Vector3.Zero;
			Velocity      = Vector3.Zero;

			// 8.2: Snap XZ về (0, 0), giữ Y ở mặt nước
			var pos = GlobalPosition;
			pos.X = 0f;
			pos.Z = 0f;
			pos.Y = 0.5f;
			GlobalPosition = pos;

			// 8.2: Camera snap về forward (reset pitch & tilt)
			_cameraPitch = 0f;
			_cameraRig.Rotation = Vector3.Zero;
			RotationDegrees = new Vector3(0f, 0f, 0f); // reset body Y rotation

			GameManager.Instance.ChangeState(GameManager.GameState.Surface);
			EventBus.Instance.EmitPlayerSurfaced();
			AudioManager.Instance.SetUnderwater(false);
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
		else if (!wasUnderwater && _isUnderwater &&
				GameManager.Instance.CurrentState == GameManager.GameState.Diving)
		{
			AudioManager.Instance.SetUnderwater(true);
		}
	}
	public float GetDepth()     => Mathf.Max(0f, -GlobalPosition.Y);
	public bool  IsUnderwater() => _isUnderwater;

	public void ForceToSurface()
	{
		_swimVelocity = new Vector3(0f, 8f, 0f);
	}

	public void UpdateDivingLight()
	{
		_divingLight.Visible   = (UpgradeManager.Instance.Upgrades["light"]?.Level ?? 0) > 0;
		_divingLight.SpotRange = UpgradeManager.Instance.GetLightRange();
	}
	
	private void UpdateDepthEffects()
	{
		if (_worldEnv == null) return;

		bool  atSurface = GameManager.Instance.CurrentState == GameManager.GameState.Surface;
		float depth     = atSurface ? 0f : GetDepth();

		float lightBonus = (_divingLight != null && _divingLight.Visible)
			? UpgradeManager.Instance.GetLightRange()
			: 0f;
		float visibilityRange = MaxDepthInvisible + lightBonus;

		// ── Bán kính fill light: Value của cấp đèn hiện tại (UpgradeManager.GetLightRange())
		// dùng trực tiếp làm baseRadius, trừ dần theo độ sâu — cập nhật mỗi frame để
		// phản ứng ngay khi vừa mua upgrade hoặc đổi độ sâu ─────────────────────────
		float baseRadius        = UpgradeManager.Instance.GetLightRange();
		int   depthBand         = (int)Mathf.Floor(depth / DepthBandSize);
		float dynamicNearRadius = Mathf.Max(0f, baseRadius - DepthFalloffPerBand * depthBand);
		if (_nearVisibilityLight != null)
		{
			_nearVisibilityLight.OmniRange = dynamicNearRadius;
			_nearVisibilityLight.Visible   = dynamicNearRadius > 0.01f;
		}

		// ── Visibility fog: đạt full trong 3m đầu sau khi lặn ──────────────
		// Tách riêng khỏi "độ tối" — turbidity của nước không nên đợi tới 50m
		// mới hiện rõ, người chơi phải thấy mờ ngay khi vừa chìm xuống.
		float visibilityT   = Mathf.Clamp(depth / visibilityRange, 0f, 1f);
		float targetDensity = atSurface ? 0.05f : Mathf.Lerp(0.01f, BaseFogDensity, visibilityT);

		// ── Độ "đục" thêm khi xuống rất sâu (chuẩn bị cho Zone Deep/Abyss) ──
		float deepT = Mathf.Clamp(depth / visibilityRange, 0f, 1f);
		targetDensity += DeepFogBonus * deepT;

		_worldEnv.FogDensity    = Mathf.Lerp(_worldEnv.FogDensity, targetDensity, 0.15f);
		_worldEnv.FogLightColor = _worldEnv.FogLightColor.Lerp(
			new Color(0.02f, 0.078f, 0.149f).Lerp(new Color(0.02f, 0.07f, 0.13f), deepT), 0.08f
		);
		// Đường cong fog khác nhau giữa Surface (xa-nhẹ) và Diving (gần-mạnh).
		// QUAN TRỌNG: FogDepthBegin trước đây cố định = 1.0, nghĩa là fog luôn bắt đầu
		// mờ dần ngay từ 1m bất kể upgrade — khiến vật thể trong vùng "nhìn rõ"
		// (dynamicNearRadius) vẫn bị FogLightColor (rất tối) trộn vào, lấn át ánh sáng
		// từ NearVisibilityLight. Giờ fog CHỈ bắt đầu sau khi ra khỏi dynamicNearRadius,
		// nên vùng nhìn rõ không bị fog can thiệp dù ở mức upgrade nào.
		_worldEnv.FogDepthBegin = atSurface ? 20.0f : dynamicNearRadius;
		_worldEnv.FogDepthEnd   = atSurface ? 150.0f : dynamicNearRadius + 6.0f;
		_worldEnv.FogSunScatter = Mathf.Lerp(_worldEnv.FogSunScatter, Mathf.Lerp(0.2f, 0.0f, Mathf.Clamp(depth / 5.0f, 0f, 1f)), 0.1f);

		// ── Mặt trời tắt hẳn sau 5m độ sâu ───────────────────────────────────
		// Sun không chỉ ảnh hưởng ambient mà còn gây glow/specular phản chiếu
		// (qua Glow/Bloom, mặt nước, bubble...) — nếu giữ sàn năng lượng như
		// ambient, dưới sâu vẫn thấy quầng sáng "nắng" rất phi lý. Nên ở đây
		// dùng ngưỡng cứng riêng (5m) và cho về thẳng 0, KHÁC với ambientCurve
		// (vẫn dùng MaxDepthInvisible=75 cho việc tối dần tổng thể).
		var sun = GetTree().Root.FindChild("Sun", true, false) as DirectionalLight3D;
		float ambientCurve = Mathf.Pow(deepT, 0.6f);   // giảm nhanh hơn ở nửa đầu, mượt dần khi gần đáy
		const float SunCutoffDepth = 5.0f; // sau ngưỡng này, sun tắt hoàn toàn — không còn phản chiếu
		float sunT = Mathf.Clamp(depth / SunCutoffDepth, 0f, 1f);
		if (sun != null)
		{
			// float targetSunEnergy = Mathf.Lerp(1.2f, 0.0f, sunT); // về thẳng 0, không có sàn
			float dayBaseSunEnergy = DayNightManager.Instance?.GetSunEnergyForTime() ?? 1.2f;
			float targetSunEnergy  = Mathf.Lerp(dayBaseSunEnergy, 0.0f, sunT);
			sun.LightEnergy = Mathf.Lerp(sun.LightEnergy, targetSunEnergy, 0.08f);
		}

		// ── Ambient tối dần nhưng có sàn — đảm bảo vùng NearVisibilityRadius
		// luôn nhìn rõ được bất kể độ sâu (ambient khuếch tán, không gây glow) ──
		// QUAN TRỌNG: AmbientLightEnergy/Color CHỈ có tác dụng khi AmbientLightSource
		// = Color (hoặc Sky). Nếu resource Environment gốc đang để Source = Disabled
		// (giá trị default khi tạo mới trong Inspector), mọi dòng set Energy ở trên
		// từ đầu tới giờ đều vô tác dụng — đây nhiều khả năng là lý do "vẫn tối dù
		// đã max upgrade". Ép cứng ở đây để chắc chắn.
		_worldEnv.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		_worldEnv.AmbientLightColor  = new Color(0.55f, 0.75f, 0.9f);
		float dayBaseAmbient = DayNightManager.Instance?.GetAmbientEnergyForTime() ?? 0.85f;
		_worldEnv.AmbientLightEnergy = Mathf.Lerp(dayBaseAmbient, 0.35f, ambientCurve); 

		// ── Background: ẩn Sky procedural khi lặn xuống ─────────────────────
		// Sky không bị fog-theo-khoảng-cách chi phối, nên dù density cao,
		// trời/đường bờ vẫn hiện rõ qua mặt nước → phải đổi hẳn sang màu đặc.
		if (atSurface)
		{
			_worldEnv.BackgroundMode = Godot.Environment.BGMode.Sky;
		}
		else
		{
			_worldEnv.BackgroundMode = Godot.Environment.BGMode.Color;
			_worldEnv.BackgroundColor = NearColor.Lerp(FarColor, deepT);
		}

		// ── Water surface: ẩn sky_tint + đặc dần khi lặn sâu ────────────────
		// Đây mới là nguồn gốc thật của hiện tượng "xuyên thấu lên trời":
		// shader water_surface có sky_tint cứng + alpha tối đa 0.85, độc lập
		// hoàn toàn với Environment/BackgroundMode.
		if (_waterMaterial != null)
		{
			float surfaceT = atSurface ? 0f : Mathf.Clamp(depth / 10.0f, 0f, 1f);
			_waterMaterial.SetShaderParameter("view_depth", surfaceT);
		}
	}
}
