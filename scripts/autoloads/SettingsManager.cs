using Godot;
using Godot.Collections;

public partial class SettingsManager : Node
{
	public static SettingsManager Instance { get; private set; }

	private const string SettingsPath = "user://settings.json";

	public float MusicVolume { get; private set; } = 0.8f;
	public float SfxVolume   { get; private set; } = 1.0f;
	public string Language   { get; private set; } = "en"; // "en" | "vi"
	public override void _Ready()
	{
		Instance = this;

		// Lần đầu mở game (chưa có file settings) → đoán ngôn ngữ theo máy
		Language = DetectDefaultLanguage();

		Load();
		Apply();
	}

	// ── Public API — gọi từ SettingsMenu khi kéo slider / đổi OptionButton ──

	public void SetMusicVolume(float value)
	{
		MusicVolume = value;
		ApplyBusVolume("Music", MusicVolume);
		Save();
	}

	public void SetSfxVolume(float value)
	{
		SfxVolume = value;
		ApplyBusVolume("SFX", SfxVolume);
		Save();
	}

	public void SetLanguage(string languageCode)
	{
		Language = languageCode;
		TranslationServer.SetLocale(Language);
		Save();
	}

	// ── Internal ──────────────────────────────────────────────────

	private string DetectDefaultLanguage()
	{
		string osLocale = OS.GetLocaleLanguage(); // ví dụ: "vi", "en"
		return osLocale == "vi" ? "vi" : "en";
	}

	private void Apply()
	{
		ApplyBusVolume("Music", MusicVolume);
		ApplyBusVolume("SFX", SfxVolume);
		TranslationServer.SetLocale(Language);
	}

	// Gọi trực tiếp AudioServer (không qua AudioManager.Instance) để không phụ thuộc
	// thứ tự Autoload — bus Music/SFX đã tồn tại sẵn trong default_bus_layout.tres
	private void ApplyBusVolume(string busName, float linear01)
	{
		int busIndex = AudioServer.GetBusIndex(busName);
		if (busIndex == -1) return;
		AudioServer.SetBusVolumeDb(busIndex, linear01 <= 0.0001f ? -80f : Mathf.LinearToDb(linear01));
		AudioServer.SetBusMute(busIndex, linear01 <= 0.0001f);
	}

	private void Save()
	{
		var data = new Dictionary
		{
			["musicVolume"] = MusicVolume,
			["sfxVolume"]   = SfxVolume,
			["language"]    = Language
		};

		using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
		file.StoreString(Json.Stringify(data, "\t"));
	}

	private void Load()
	{
		if (!FileAccess.FileExists(SettingsPath)) return;

		using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
		var parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary) return;

		var data = parsed.AsGodotDictionary();
		if (data.ContainsKey("musicVolume")) MusicVolume = (float)data["musicVolume"];
		if (data.ContainsKey("sfxVolume"))   SfxVolume   = (float)data["sfxVolume"];
		if (data.ContainsKey("language"))    Language    = (string)data["language"];
	}
}
