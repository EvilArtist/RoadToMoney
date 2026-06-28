using Godot;

/// <summary>
/// GodRaySystem — spawns N animated quad planes beneath the ocean surface.
/// 
/// HOW TO USE:
///   1. Create a Node3D in your OceanWorld scene, attach this script.
///   2. Assign GodRayMaterial (a ShaderMaterial using god_ray.gdshader).
///   3. Position this node AT ocean surface level (Y = 0 typically).
///   4. The system auto-follows the player horizontally so rays are always visible.
///
/// SCENE TREE:
///   OceanWorld
///   └── GodRaySystem  (Node3D + this script)
///       └── [RayQuad_0..N]  (MeshInstance3D, generated at runtime)
/// </summary>
public partial class GodRaySystem : Node3D
{
	// ── Inspector params ────────────────────────────────────────────────────

	[ExportGroup("Spawn")]
	[Export] public int   RayCount       = 12;
	[Export] public float SpawnRadius    = 10.0f;   // giảm từ 18 — radius lớn + tilt mạnh = tụ điểm quá xa
	[Export] public float RayLength      = 14.0f;   // giảm từ 22 — ray dài quá làm hội tụ perspective rõ
	[Export] public float RayWidth       = 1.6f;
	[Export] public float WidthVariance  = 0.8f;
	[Export] public float TiltMax        = 18.0f;    // [DEPRECATED] không còn dùng, xem TiltJitter ở "Light Sync"

	[ExportGroup("Material")]
	[Export] public ShaderMaterial GodRayMaterial;   // assign in Inspector

	[ExportGroup("Behaviour")]
	[Export] public NodePath PlayerPath;             // drag Player node here
	[Export] public float FollowSpeed    = 3.0f;    // how fast system tracks player XZ
	[Export] public float ActiveDepth    = 2.0f;    // only visible when player Y < surface - this

	[ExportGroup("Light Sync")]
	[Export] public NodePath SunPath;                // drag DirectionalLight3D (Sun) here
	[Export] public float TiltJitter     = 5.0f;     // giảm từ 8 — jitter lớn + nhiều ray = tụ điểm random trông hỗn loạn

	[ExportGroup("Animation")]
	[Export] public float SwaySpeed      = 0.4f;    // gentle lateral sway speed
	[Export] public float SwayAmplitude  = 0.6f;    // sway distance in metres

	// ── Internal state ───────────────────────────────────────────────────────
	private Node3D   _player;
	private DirectionalLight3D _sun;
	private MeshInstance3D[] _rays;
	private Vector3[]  _basePositions;   // XZ spawn offsets from system centre
	private float[]    _swayPhases;      // per-ray random phase offset
	private float[]    _swayDirs;        // per-ray sway direction (+1 / -1)

	public override void _Ready()
	{
		if (PlayerPath != null)
			_player = GetNode<Node3D>(PlayerPath);
		if (SunPath != null)
			_sun = GetNode<DirectionalLight3D>(SunPath);

		_BuildRays();
	}

	public override void _Process(double delta)
	{
		float t = (float)delta;

		// ── Follow player XZ ──────────────────────────────────────────────
		if (_player != null)
		{
			Vector3 target = new Vector3(_player.GlobalPosition.X,
										 GlobalPosition.Y,   // keep surface Y
										 _player.GlobalPosition.Z);
			GlobalPosition = GlobalPosition.Lerp(target, FollowSpeed * t);
		}

		// ── Visibility: only show when underwater ─────────────────────────
		bool underwater = _player != null &&
						  _player.GlobalPosition.Y < (GlobalPosition.Y - ActiveDepth);
		Visible = underwater;
		if (!underwater) return;

		// ── Animate each ray (gentle sway) ───────────────────────────────
		float time = (float)Time.GetTicksMsec() / 1000.0f;
		for (int i = 0; i < _rays.Length; i++)
		{
			float sway = Mathf.Sin(time * SwaySpeed + _swayPhases[i]) * SwayAmplitude;
			Vector3 offset = _basePositions[i] + new Vector3(sway * _swayDirs[i], 0f, 0f);
			_rays[i].Position = offset;
		}
	}

	// ── Private helpers ───────────────────────────────────────────────────────

	private void _BuildRays()
	{
		_rays          = new MeshInstance3D[RayCount];
		_basePositions = new Vector3[RayCount];
		_swayPhases    = new float[RayCount];
		_swayDirs      = new float[RayCount];

		var rng = new RandomNumberGenerator();
		rng.Randomize();

		// ── Tilt baseline từ hướng Sun thật ───────────────────────────
		// Ray hangs down from local -Y; ta nghiêng -Y theo đúng hướng ánh sáng
		// chiếu xuống (lightDir) để trông giống tia sáng thật, không phải
		// vệt mờ random vô hướng. Nếu nghiêng sai chiều khi test trong editor,
		// đổi dấu (-) ở baseTiltX hoặc baseTiltZ.
		Vector3 lightDir = _sun != null
			? -_sun.GlobalTransform.Basis.Z.Normalized()
			: Vector3.Down;

		float baseTiltX = 0f;
		float baseTiltZ = 0f;
		if (_sun != null)
		{
			baseTiltX = Mathf.RadToDeg(Mathf.Atan2(-lightDir.Z, -lightDir.Y));
			baseTiltZ = Mathf.RadToDeg(Mathf.Atan2(lightDir.X, -lightDir.Y));
		}

		for (int i = 0; i < RayCount; i++)
		{
			// ── Random spawn position in circle ──────────────────────────
			float angle = rng.RandfRange(0f, Mathf.Tau);
			float dist  = rng.RandfRange(0f, SpawnRadius);
			float x     = Mathf.Cos(angle) * dist;
			float z     = Mathf.Sin(angle) * dist;

			// ── Tilt = sun-aligned base + nhiễu nhẹ cho tự nhiên ──────────
			float tiltX = baseTiltX + rng.RandfRange(-TiltJitter, TiltJitter);
			float tiltZ = baseTiltZ + rng.RandfRange(-TiltJitter, TiltJitter);

			// ── Random size ───────────────────────────────────────────────
			float width = RayWidth + rng.RandfRange(-WidthVariance, WidthVariance);
			width = Mathf.Max(0.3f, width);

			// ── Build quad mesh ───────────────────────────────────────────
			var quad      = new QuadMesh();
			quad.Size     = new Vector2(width, RayLength);
			// Offset so top edge sits at Y=0 (surface), ray hangs downward
			quad.CenterOffset = new Vector3(0f, -RayLength * 0.5f, 0f);

			var mesh              = new MeshInstance3D();
			mesh.Mesh             = quad;
			mesh.MaterialOverride = GodRayMaterial;

			// Position & rotate
			mesh.Position          = new Vector3(x, 0f, z);
			mesh.RotationDegrees   = new Vector3(tiltX, rng.RandfRange(0f, 360f), tiltZ);

			// CastShadow off — these are light, not shadow-casters
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

			AddChild(mesh);

			// Store for animation
			_rays[i]          = mesh;
			_basePositions[i] = new Vector3(x, 0f, z);
			_swayPhases[i]    = rng.RandfRange(0f, Mathf.Tau);
			_swayDirs[i]      = rng.RandfRange(0f, 1f) > 0.5f ? 1f : -1f;
		}
	}

	// ── Hot-reload helper (editor only) ──────────────────────────────────────
	// Call this from a tool button or re-enter scene to rebuild rays in editor.
	public void RebuildInEditor()
	{
		foreach (Node child in GetChildren())
			child.QueueFree();
		_BuildRays();
	}
}
