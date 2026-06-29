using Godot;

/// <summary>
/// MenuScreen — dùng chung 3 context: MainMenu (start), mở giữa game qua Esc,
/// và khi player nổi lên mặt nước (thay cho auto-ShopScreen).
/// Wiring đầy đủ với ShopScreen/UpgradeScreen sẽ hoàn thiện ở Group 4.
/// </summary>
public partial class MenuScreen : CanvasLayer
{
	private Button _playButton;
	private Button _shopButton;
	private Button _upgradeButton;
	private Button _bookButton;
	private Button _settingButton;
	private Button _exitButton;

	private bool _isFirstLaunchContext = true;

	public override void _Ready()
	{
		_playButton    = GetNode<Button>("MenuCenter/ButtonList/PlayButton");
		_shopButton    = GetNode<Button>("MenuCenter/ButtonList/ShopButton");
		_upgradeButton = GetNode<Button>("MenuCenter/ButtonList/UpgradeButton");
		_bookButton    = GetNode<Button>("MenuCenter/ButtonList/BookButton");
		_settingButton = GetNode<Button>("MenuCenter/ButtonList/SettingButton");
		_exitButton    = GetNode<Button>("MenuCenter/ButtonList/ExitButton");

		_playButton.Pressed    += OnPlay;
		_shopButton.Pressed    += OnShop;
		_upgradeButton.Pressed += OnUpgrade;
		_bookButton.Pressed    += OnBook;
		_settingButton.Pressed += OnSetting;
		_exitButton.Pressed    += OnExit;

		EventBus.Instance.PlayerSurfaced += OnPlayerSurfaced;

		Visible = false;

		// Lúc OceanWorld vừa load, nếu state vẫn là MainMenu (chưa Play lần nào) → tự hiện
		if (GameManager.Instance.CurrentState == GameManager.GameState.MainMenu)
			Show(isFirstLaunch: true);
	}

	private void OnPlayerSurfaced()
	{
		Show(isFirstLaunch: false);
	}

	// ── Public entry point ──────────────────────────────────────────────────

	/// <param name="isFirstLaunch">
	/// true  = mở từ GameState.MainMenu lúc start game (nút Play = "Play", Exit khả dụng)
	/// false = mở giữa game qua Esc hoặc khi nổi lên mặt nước (nút Play = "Resume", Exit ẩn)
	/// </param>
	public void Show(bool isFirstLaunch)
	{
		_isFirstLaunchContext = isFirstLaunch;
		bool diving = GameManager.Instance.IsDiving();
		_playButton.Text  = diving ? Tr("RESUME") : Tr("PLAY");
		// _exitButton.Visible = isFirstLaunch;

		RefreshButtonStates();

		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public new void Hide()
	{
		Visible = false;
	}

	// Gọi từ SettingsMenu.OnBackPressed() — quay lại đúng context cũ (MainMenu/Pause/Surface)
	// mà không cần SettingsMenu phải tự nhớ context, tránh trùng state ở 2 nơi
	public void ShowPrevious()
	{
		Show(_isFirstLaunchContext);
	}

	// ── State-aware enable/disable ───────────────────────────────────────────

	private void RefreshButtonStates()
	{
		bool diving = GameManager.Instance.IsDiving();
		_shopButton.Disabled    = diving;
		_upgradeButton.Disabled = diving;
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

	private void OnShop()
	{
		Hide();
		var shopScreen = GetTree().Root.FindChild("ShopScreen", true, false) as ShopScreen;
		shopScreen?.OpenSell();
	}

	private void OnUpgrade()
	{
		Hide();
		var upgradeScreen = GetTree().Root.FindChild("UpgradeScreen", true, false) as UpgradeScreen;
		upgradeScreen?.Show();
	}

	private void OnBook()
	{
		Hide();
		var bookScreen = GetTree().Root.FindChild("BookScreen", true, false) as BookScreen;
		bookScreen?.Show();
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
