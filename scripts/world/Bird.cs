using Godot;

/// <summary>
/// Bird — dùng model glb có AnimationPlayer bay sẵn, thay cho billboard sprite cũ.
/// Setup: Bird.tscn = Node3D (script này) + instance glb model làm child,
/// đảm bảo glb có animation bay (tên khớp với AnimationName export).
/// </summary>
public partial class Bird : Node3D
{
	[Export] public string AnimationName = "ArmatureAction"; // đổi khớp tên animation thật trong glb
	[Export] public float  MinAnimSpeed  = 0.85f; // lệch pha tự nhiên giữa các con
	[Export] public float  MaxAnimSpeed  = 1.15f;
	[Export] public float FadeStartDistance = 300f;
	[Export] public float FadeEndDistance   = 500f;

	private AnimationPlayer _animPlayer;

	public override void _Ready()
	{
		_animPlayer = FindAnimationPlayer(this);

		if (_animPlayer == null)
		{
			return;
		}

		var available = _animPlayer.GetAnimationList();
		if (!_animPlayer.HasAnimation(AnimationName))
		{
			if (available.Length > 0)
				AnimationName = available[0];
			else
				return;
		}
		var anim = _animPlayer.GetAnimation(AnimationName);
		if (anim != null)
			anim.LoopMode = Animation.LoopModeEnum.Linear;
		_animPlayer.SpeedScale = (float)GD.RandRange(MinAnimSpeed, MaxAnimSpeed);
		_animPlayer.Play(AnimationName);
		ApplyDistanceFade(this);
	}

	private void ApplyDistanceFade(Node node)
	{
		if (node is GeometryInstance3D gi)
		{
			gi.VisibilityRangeBegin     = 0f;
			gi.VisibilityRangeEnd       = FadeEndDistance;
			gi.VisibilityRangeEndMargin = FadeEndDistance - FadeStartDistance; // 500-300=200 → fade bắt đầu ở 300, hết hẳn ở 500
			gi.VisibilityRangeFadeMode  = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
		}
		foreach (var child in node.GetChildren())
			ApplyDistanceFade(child);
	}

	private AnimationPlayer FindAnimationPlayer(Node node)
	{
		if (node is AnimationPlayer ap) return ap;
		foreach (var child in node.GetChildren())
		{
			var found = FindAnimationPlayer(child);
			if (found != null) return found;
		}
		return null;
	}
}
