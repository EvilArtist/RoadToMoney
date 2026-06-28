using Godot;

public partial class SplashScreen : Control
{
	private ProgressBar _loadingBar;
	private Label       _statusLabel;

	private const string TargetScenePath = "res://scenes/world/OceanWorld.tscn";

	public override async void _Ready()
	{
		_loadingBar  = GetNode<ProgressBar>("LoadingBar");
		_statusLabel = GetNode<Label>("LoadingBar/StatusLabel");

		_statusLabel.Text = Tr("LOADING");

		var tween = CreateTween();
		tween.TweenProperty(_loadingBar, "value", 90.0, 0.8)
			 .SetTrans(Tween.TransitionType.Sine)
			 .SetEase(Tween.EaseType.Out);

		await ToSignal(tween, Tween.SignalName.Finished);

		var packedScene = GD.Load<PackedScene>(TargetScenePath);

		if (packedScene == null)
		{
			_statusLabel.Text = Tr("LOADING_FAIL");
			return;
		}

		_loadingBar.Value = 100.0;
		_statusLabel.Text = Tr("READY");

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		GetTree().ChangeSceneToPacked(packedScene);
	}
}
