using Godot;

/// <summary>
/// MenuScreen — Main menu chỉ còn: Play/Resume, Credits, Setting, Exit.
/// Shop / Upgrade / Book được chuyển sang icon buttons ở HUD (BottomIconBar).
/// </summary>
public partial class MenuScreen : CanvasLayer
{
	private Button _playButton;
	private Button _creditsButton;
	private Button _settingButton;
	private Button _exitButton;

	private bool _isFirstLaunchContext = true;

	public override void _Ready()
	{
		_playButton    = GetNode<Button>("MenuCenter/ButtonList/PlayButton");
		_creditsButton = GetNode<Button>("MenuCenter/ButtonList/CreditsButton");
		_settingButton = GetNode<Button>("MenuCenter/ButtonList/SettingButton");
		_exitButton    = GetNode<Button>("MenuCenter/ButtonList/ExitButton");

		_playButton.Pressed    += OnPlay;
		_creditsButton.Pressed += OnCredits;
		_settingButton.Pressed += OnSetting;
		_exitButton.Pressed    += OnExit;

		EventBus.Instance.PlayerSurfaced += OnPlayerSurfaced;

		Visible = false;

		if (GameManager.Instance.CurrentState == GameManager.GameState.MainMenu)
			Show(isFirstLaunch: true);
	}

	private void OnPlayerSurfaced()
	{
		Show(isFirstLaunch: false);
	}

	// ── Public entry point ──────────────────────────────────────────────────

	public void Show(bool isFirstLaunch)
	{
		_isFirstLaunchContext = isFirstLaunch;
		bool diving = GameManager.Instance.IsDiving();
		_playButton.Text = diving ? Tr("RESUME") : Tr("PLAY");

		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public new void Hide()
	{
		Visible = false;
	}

	public void ShowPrevious()
	{
		Show(_isFirstLaunchContext);
	}

	// ── Button handlers ───────────────────────────────────────────────────────

	private void OnPlay()
	{
		Hide();

		if (_isFirstLaunchContext)
		{
			FloatingMessage.Show(GetTree(), Tr("SPACE_TO_SWIM"));
			GameManager.Instance.ChangeState(GameManager.GameState.Surface);
		}

		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void OnCredits()
	{
		Hide();
		var thankYouScreen = GetTree().Root.FindChild("ThankYouScreen", true, false) as ThankYouScreen;
		thankYouScreen?.Show();
	}

	private void OnSetting()
	{
		Hide();
		var settingsMenu = GetTree().Root.FindChild("SettingsMenu", true, false) as SettingsMenu;
		settingsMenu?.Display();
	}

	private void OnExit()
	{
		GetTree().Quit();
	}
}
