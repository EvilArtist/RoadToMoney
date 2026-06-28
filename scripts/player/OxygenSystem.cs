using Godot;

public partial class OxygenSystem : Node3D
{
	[Export] public float WarningThreshold = 0.25f;

	public float MaxOxygen     { get; private set; }
	public float CurrentOxygen { get; private set; }
	public bool  IsCritical    => CurrentOxygen / MaxOxygen <= WarningThreshold;

	private float _drainRate = 1f;
	private bool  _active    = false;

	public override void _Ready()
	{
		EventBus.Instance.DiveStarted    += OnDiveStarted;
		EventBus.Instance.PlayerSurfaced += OnSurfaced;
		RefreshFromUpgrade();
	}

	public override void _Process(double delta)
	{
		if (!_active) return;

		CurrentOxygen -= (float)delta * _drainRate;
		CurrentOxygen  = Mathf.Max(CurrentOxygen, 0f);

		EventBus.Instance.EmitOxygenChanged(CurrentOxygen, MaxOxygen);

		if (IsCritical)
			EventBus.Instance.EmitOxygenCritical();

		if (CurrentOxygen <= 0f)
			OnOxygenDepleted();
	}

	private void OnDiveStarted()
	{
		RefreshFromUpgrade();
		CurrentOxygen = MaxOxygen;
		_active = true;
	}

	private void OnSurfaced()
	{
		_active       = false;
		CurrentOxygen = MaxOxygen;
		SaveSystem.Instance.SaveGame();
	}

	public void RefreshFromUpgrade()
	{
		MaxOxygen     = UpgradeManager.Instance.GetOxygenDuration();
		CurrentOxygen = MaxOxygen;
	}

	private void OnOxygenDepleted()
	{
		_active = false;
		EventBus.Instance.EmitPlayerDrowned();
	}
}
