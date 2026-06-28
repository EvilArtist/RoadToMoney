using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	public enum GameState { MainMenu, Surface, Diving, Shop, Paused }

	public GameState CurrentState { get; private set; } = GameState.MainMenu;
	public int   TotalDives   { get; set; } = 0;
	public float SessionStart { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
	}

	public void ChangeState(GameState newState)
	{
		CurrentState = newState;
		switch (newState)
		{
			case GameState.Diving:
				SessionStart = Time.GetTicksMsec() / 1000f;
				TotalDives++;
				EventBus.Instance.EmitDiveStarted();
				break;
			case GameState.Surface:
				EventBus.Instance.EmitDiveEnded();
				break;
		}
	}

	public bool IsDiving() => CurrentState == GameState.Diving;
}
