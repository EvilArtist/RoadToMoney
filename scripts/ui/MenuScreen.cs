using Godot;

/// <summary>
/// MenuScreen — mở từ BottomIconBar (nút Menu). Chỉ còn: Credits, Setting, Exit, Back.
/// Không còn nút Play — game bắt đầu thẳng ở Surface state, người chơi bấm Space để lặn.
/// Back / click ra ngoài → ẩn menu, quay về HUD idle (icon bar + "Press Space" hint).
/// </summary>
public partial class MenuScreen : CanvasLayer
{
	private Button  _creditsButton;
	private Button  _settingButton;
	private Button  _exitButton;
	private Button  _backButton;
	private Control _dimBackground;

	public override void _Ready()
	{
		_creditsButton = GetNode<Button> ("MenuCenter/ButtonList/CreditsButton");
		_settingButton = GetNode<Button> ("MenuCenter/ButtonList/SettingButton");
		_exitButton    = GetNode<Button> ("MenuCenter/ButtonList/ExitButton");
		_backButton    = GetNode<Button> ("MenuCenter/ButtonList/BackButton");
		_dimBackground = GetNode<Control>("DimBackground");

		_creditsButton.Pressed += OnCredits;
		_settingButton.Pressed += OnSetting;
		_exitButton.Pressed    += OnExit;
		_backButton.Pressed    += OnBack;

		// Click ra ngoài (vùng dim) để đóng menu — tương đương Back.
		_dimBackground.GuiInput += OnDimBackgroundInput;

		Visible = false;
	}

	// ── Public entry point ──────────────────────────────────────────────────

	public void Show()
	{
		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public new void Hide()
	{
		Visible = false;
	}

	// ── Click outside to close ───────────────────────────────────────────────

	private void OnDimBackgroundInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			OnBack();
	}

	// ── Button handlers ───────────────────────────────────────────────────────

	private void OnBack()
	{
		Hide();
		var hud = GetTree().Root.FindChild("Hud", true, false) as HUD;
		hud?.ReturnToIdle();
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
