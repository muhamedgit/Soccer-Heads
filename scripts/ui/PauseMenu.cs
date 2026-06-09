using Godot;

// In-match pause overlay. Toggled with the "Pause" action (Esc). While paused the whole
// SceneTree is paused (players, ball, timer freeze); this node runs with ProcessMode.Always
// so it keeps handling input and button presses.
public partial class PauseMenu : CanvasLayer
{
	private Control _overlay;
	private Button _resumeButton;
	private Button _restartButton;
	private Button _mainMenuButton;
	private GameState _gameState;
	private SceneManager _sceneManager;
	private AudioManager _audioManager;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_overlay = GetNode<Control>("Overlay");
		UiTheme.Apply(_overlay, withBackdrop: false);
		_resumeButton = GetNode<Button>("Overlay/CenterContainer/VBoxContainer/ResumeButton");
		_restartButton = GetNode<Button>("Overlay/CenterContainer/VBoxContainer/RestartButton");
		_mainMenuButton = GetNode<Button>("Overlay/CenterContainer/VBoxContainer/MainMenuButton");

		_gameState = GetNode<GameState>("/root/GameState");
		_sceneManager = GetNode<SceneManager>("/root/SceneManager");
		_audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");

		_overlay.Visible = false;

		_resumeButton.Pressed += () => SetPaused(false);
		_restartButton.Pressed += OnRestartPressed;
		_mainMenuButton.Pressed += OnMainMenuPressed;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("Pause"))
		{
			SetPaused(!GetTree().Paused);
			GetViewport().SetInputAsHandled();
		}
	}

	private void SetPaused(bool paused)
	{
		GetTree().Paused = paused;
		_overlay.Visible = paused;
		_audioManager?.SetMusicPaused(paused);
	}

	private void OnRestartPressed()
	{
		// Always unpause before changing scene, or the next scene starts frozen.
		GetTree().Paused = false;
		_gameState.ResetMatch();
		_sceneManager.GoToMatch();
	}

	private void OnMainMenuPressed()
	{
		GetTree().Paused = false;
		_sceneManager.GoToMainMenu();
	}
}
