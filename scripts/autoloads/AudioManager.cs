using Godot;

public partial class AudioManager : Node
{
	public static AudioManager Instance { get; private set; }

	private AudioStreamPlayer   _musicPlayer;
	private AudioStreamPlayer   _ambientPlayer;
	private AudioStreamPlayer   _heartbeatPlayer; // tách riêng vì PlayHeartbeat() cần check .Playing để không bị trigger lại liên tục

	// SFX Pool — cho phép nhiều hiệu ứng (splash, bubbles, surface, catch...) phát đồng thời
	// mà không đè lẫn nhau như trước đây khi dùng 1 player chung
	[Export] private int _sfxPoolSize = 6;
	private readonly System.Collections.Generic.List<AudioStreamPlayer> _sfxPool = new();
	private int _nextSfxIndex = 0;

	private const string MasterBus     = "Master";
	private const string UnderwaterBus = "Underwater";
	private const string MusicBus = "Music";
	private const string AmbientBus = "Ambient";
	private const string SfxBus = "SFX";

	// Preload sounds
	private AudioStream _underwaterAmbient;
	private AudioStream _splashSound;
	private AudioStream _bubblesSound;
	private AudioStream _surfaceSound;
	private AudioStream _heartbeatSound;
	private AudioStream _backgroundMusic;
	// Thêm field, cạnh _heartbeatSound
	private AudioStream[] _catchSfxByRarity = new AudioStream[4]; // index khớp SeaResource.Rarity
	private AudioStream   _catchMissSfx;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		// Setup players
		_musicPlayer        = new AudioStreamPlayer();
		_ambientPlayer      = new AudioStreamPlayer();
		_heartbeatPlayer    = new AudioStreamPlayer();

		_musicPlayer.Bus    = MusicBus;
		_ambientPlayer.Bus  = AmbientBus;
		_heartbeatPlayer.Bus = SfxBus;

		AddChild(_musicPlayer);
		AddChild(_ambientPlayer);
		AddChild(_heartbeatPlayer);

		// SFX pool — round-robin, không có player nào bị "chiếm dụng" lâu dài
		for (int i = 0; i < _sfxPoolSize; i++)
		{
			var p = new AudioStreamPlayer { Bus = SfxBus };
			AddChild(p);
			_sfxPool.Add(p);
		}

		// Load sounds
		_underwaterAmbient = LoadSound("res://assets/sounds/underwater_ambient.ogg");
		_splashSound       = LoadSound("res://assets/sounds/splash.ogg");
		_bubblesSound      = LoadSound("res://assets/sounds/bubbles.ogg");
		_surfaceSound      = LoadSound("res://assets/sounds/surface.ogg");
		_heartbeatSound = LoadSound("res://assets/sounds/heartbeat.ogg");
		_backgroundMusic = LoadSound("res://assets/music/underwater.ogg");
		_catchSfxByRarity[0] = LoadSound("res://assets/sounds/catch_common.ogg");
		_catchSfxByRarity[1] = LoadSound("res://assets/sounds/catch_uncommon.ogg");
		_catchSfxByRarity[2] = LoadSound("res://assets/sounds/catch_rare.ogg");
		_catchSfxByRarity[3] = LoadSound("res://assets/sounds/catch_epic.ogg");
		_catchMissSfx        = LoadSound("res://assets/sounds/catch_miss.ogg");
		// Subscribe events
		EventBus.Instance.DiveStarted    += OnDiveStarted;
		EventBus.Instance.PlayerSurfaced += OnPlayerSurfaced;
		EventBus.Instance.ResourceCaughtFx += OnResourceCaughtFx;
		EventBus.Instance.CatchMissed      += OnCatchMissed;
		PlayMusic(_backgroundMusic);
	}

	private AudioStream LoadSound(string path)
	{
		if (ResourceLoader.Exists(path))
			return GD.Load<AudioStream>(path);
		return null;
	}

	// ── Public API ────────────────────────────────────────────────
	public void PlayMusic(AudioStream stream)
	{
		if (_musicPlayer.Stream == stream) return;
		_musicPlayer.Stream = stream;
		_musicPlayer.Play();
	}

	public void PlaySfx(AudioStream stream, float volumeDb = 0f)
	{
		if (stream == null || _sfxPool.Count == 0) return;
		var player = GetFreeSfxPlayer();
		player.Stream   = stream;
		player.VolumeDb = volumeDb;
		player.Play();
	}

	// Tìm player đang rảnh trong pool theo round-robin; nếu cả pool đang bận
	// (hiếm khi xảy ra với 6 slot) thì lấy slot kế tiếp, chấp nhận cắt SFX cũ ít quan trọng nhất
	private AudioStreamPlayer GetFreeSfxPlayer()
	{
		for (int i = 0; i < _sfxPool.Count; i++)
		{
			int idx = (_nextSfxIndex + i) % _sfxPool.Count;
			if (!_sfxPool[idx].Playing)
			{
				_nextSfxIndex = (idx + 1) % _sfxPool.Count;
				return _sfxPool[idx];
			}
		}
		var fallback = _sfxPool[_nextSfxIndex];
		_nextSfxIndex = (_nextSfxIndex + 1) % _sfxPool.Count;
		return fallback;
	}

	public void SetUnderwater(bool enabled)
	{
		if (enabled)
		{
			// Bắt đầu ambient underwater loop
			if (_underwaterAmbient != null && !_ambientPlayer.Playing)
			{
				_ambientPlayer.Stream    = _underwaterAmbient;
				_ambientPlayer.VolumeDb  = -5f;
				_ambientPlayer.Autoplay  = false;
				_ambientPlayer.Play();
			}

			// Bubbles ambient
			if (_bubblesSound != null)
				PlaySfx(_bubblesSound, -10f);
		}
		else
		{
			// Tắt ambient khi nổi lên
			_ambientPlayer.Stop();
		}

		// EQ filter
		int idx = AudioServer.GetBusIndex(UnderwaterBus);
		if (idx >= 0)
			AudioServer.SetBusEffectEnabled(idx, 0, enabled);
	}

	// ── Events ────────────────────────────────────────────────────
	private void OnDiveStarted()
	{
		PlaySfx(_splashSound, -3f);
	}

	private void OnPlayerSurfaced()
	{
		_ambientPlayer.Stop();
		PlaySfx(_surfaceSound, -3f);
	}

	private void OnResourceCaughtFx(string id, Vector3 worldPos)
	{
		var res = GD.Load<SeaResource>($"res://resources/items/{id}.tres");
		if (res == null) return;

		int rarityIndex = (int)res.ResourceRarity;
		var stream = _catchSfxByRarity[Mathf.Clamp(rarityIndex, 0, _catchSfxByRarity.Length - 1)];
		PlaySfx(stream, -2f);
	}

	private void OnCatchMissed(Vector3 worldPos)
	{
		PlaySfx(_catchMissSfx, -4f);
	}

	// ── Volume helpers ────────────────────────────────────────────
	public void SetMasterVolume(float volumeDb)
		=> AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(MasterBus), volumeDb);

	// Dùng cho Settings Menu (bước sau) — nhận linear 0..1 từ Slider, tự convert sang dB và mute khi = 0
	public void SetBusVolumeLinear(string busName, float linear01)
	{
		int busIndex = AudioServer.GetBusIndex(busName);
		if (busIndex == -1) return;
		AudioServer.SetBusVolumeDb(busIndex, linear01 <= 0.0001f ? -80f : Mathf.LinearToDb(linear01));
		AudioServer.SetBusMute(busIndex, linear01 <= 0.0001f);
	}

	public void PlayHeartbeat()
	{
		if (_heartbeatSound == null || _heartbeatPlayer.Playing) return;
		_heartbeatPlayer.Stream   = _heartbeatSound;
		_heartbeatPlayer.VolumeDb = -4f;
		_heartbeatPlayer.Play();
	}
}
