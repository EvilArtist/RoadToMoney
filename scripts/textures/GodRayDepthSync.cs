using Godot;

/// <summary>
/// GodRayDepthSync — attaches to the same node as GodRaySystem.
/// Dynamically fades ray_alpha on the ShaderMaterial based on player depth,
/// so rays dim naturally as you dive deeper (less light penetration).
///
/// Curve:
///   Surface (Y=0)          → alpha = AlphaAtSurface  (default 0.18)
///   ShallowDepth (Y=-8)    → alpha peak               (default 0.22)
///   MaxDepth     (Y=-30)   → alpha = 0 (fully faded)
///
/// This matches real ocean light penetration — rays are most visible in
/// the top 5–15m, then fade rapidly.
/// </summary>
public partial class GodRayDepthSync : Node3D
{
    [Export] public NodePath       PlayerPath;
    [Export] public ShaderMaterial GodRayMaterial;

    [ExportGroup("Depth Curve")]
    [Export] public float SurfaceY      =  0.0f;
    [Export] public float PeakDepth     =  8.0f;
    [Export] public float FadeDepth     = 32.0f;
    [Export] public float AlphaAtSurface = 0.12f;  // giảm từ 0.22 — đang overexpose
    [Export] public float AlphaAtPeak    = 0.25f;  // giảm từ 0.45 — đang overexpose

    [ExportGroup("Time of Day (optional)")]
    [Export] public bool  UseTimeOfDay  = false;
    [Export] public float TimeOfDayAlphaScale = 1.0f;  // set from GameManager each frame

    private Node3D _player;

    public override void _Ready()
    {
        if (PlayerPath != null)
            _player = GetNode<Node3D>(PlayerPath);
    }

    public override void _Process(double delta)
    {
        if (_player == null || GodRayMaterial == null) return;

        float depth = SurfaceY - _player.GlobalPosition.Y;  // positive = below surface
        depth = Mathf.Max(0f, depth);

        float alpha;

        if (depth <= PeakDepth)
        {
            // Ramp UP from surface to peak
            float t = depth / PeakDepth;
            alpha = Mathf.Lerp(AlphaAtSurface, AlphaAtPeak, t);
        }
        else
        {
            // Fade OUT from peak to zero
            float t = (depth - PeakDepth) / (FadeDepth - PeakDepth);
            t     = Mathf.Clamp(t, 0f, 1f);
            alpha = Mathf.Lerp(AlphaAtPeak, 0f, t);
        }

        if (UseTimeOfDay)
            alpha *= TimeOfDayAlphaScale;

        GodRayMaterial.SetShaderParameter("ray_alpha", alpha);
    }
}