using Godot;

/// <summary>
/// SettingsMenu — mở từ MenuScreen (nút Setting). Đổi Music/SFX volume và Language ngay khi
/// kéo slider/chọn option (preview tức thì qua SettingsManager). Nút Back / click ra ngoài
/// đóng menu này và quay lại MenuScreen (Setting được mở lồng bên trong Menu).
/// </summary>
public partial class SettingsMenu : CanvasLayer
{
	private HSlider _musicSlider;
	private HSlider _sfxSlider;
	private OptionButton _languageOption;
	private Button _backButton;
	private Control _dimBackground;

	public override void _Ready()
	{
		_musicSlider    = GetNode<HSlider>("MenuCenter/RowList/MusicRow/Row/MusicSlider");
		_sfxSlider      = GetNode<HSlider>("MenuCenter/RowList/SfxRow/Row/SfxSlider");
		_languageOption = GetNode<OptionButton>("MenuCenter/RowList/LanguageRow/Row/LanguageOption");
		_backButton     = GetNode<Button>("MenuCenter/RowList/BackButton");
		_dimBackground  = GetNode<Control>("DimBackground");

		_musicSlider.ValueChanged    += (double v) => SettingsManager.Instance.SetMusicVolume((float)v);
		_sfxSlider.ValueChanged      += (double v) => SettingsManager.Instance.SetSfxVolume((float)v);
		_languageOption.ItemSelected += (long idx) => SettingsManager.Instance.SetLanguage(idx == 1 ? "vi" : "en");
		_backButton.Pressed         += OnBackPressed;

		_dimBackground.MouseFilter = Control.MouseFilterEnum.Stop;
		_dimBackground.GuiInput   += OnDimBackgroundInput;

		Visible = false;
	}

	// Gọi từ MenuScreen.OnSetting() — đồng bộ lại giá trị hiển thị trước khi hiện ra,
	// tránh trường hợp slider lệch với giá trị thật nếu SettingsManager đổi từ nơi khác
	public void Display()
	{
		var settings = SettingsManager.Instance;

		_musicSlider.Value = settings.MusicVolume;
		_sfxSlider.Value   = settings.SfxVolume;
		_languageOption.Select(settings.Language == "vi" ? 1 : 0);

		Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public new void Hide()
	{
		Visible = false;
	}

	private void OnBackPressed()
	{
		Hide();
		var menuScreen = GetTree().Root.FindChild("MenuScreen", true, false) as MenuScreen;
		menuScreen?.Show();
	}

	private void OnDimBackgroundInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			OnBackPressed();
	}
}
