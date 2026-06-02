using Godot;

public partial class MainMenu : Control
{
	private Button _playButton;
	private Button _modeButton;
	private Button _difficultyButton;
	private Button _instructionsButton;
	private Button _settingsButton;
	private Button _quitButton;

	private bool _isStartingMatch = false;

	private GameState.GameMode _mode = GameState.GameMode.PlayerVsPlayer;
	private GameState.AiDifficulty _difficulty = GameState.AiDifficulty.Normal;

	public override void _Ready()
	{
		GD.Print("MainMenu script loaded.");

		_playButton = GetNode<Button>("CenterContainer/VBoxContainer/PlayButton");
		_modeButton = GetNode<Button>("CenterContainer/VBoxContainer/ModeButton");
		_difficultyButton = GetNode<Button>("CenterContainer/VBoxContainer/DifficultyButton");
		_instructionsButton = GetNode<Button>("CenterContainer/VBoxContainer/InstructionsButton");
		_settingsButton = GetNode<Button>("CenterContainer/VBoxContainer/SettingsButton");
		_quitButton = GetNode<Button>("CenterContainer/VBoxContainer/QuitButton");

		// Restore the last chosen mode/difficulty so the menu reflects the current setting.
		var gameState = GetNode<GameState>("/root/GameState");
		_mode = gameState.Mode;
		_difficulty = gameState.Difficulty;

		_playButton.Pressed += OnPlayPressed;
		_modeButton.Pressed += OnModePressed;
		_difficultyButton.Pressed += OnDifficultyPressed;
		_instructionsButton.Pressed += OnInstructionsPressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_quitButton.Pressed += OnQuitPressed;

		UpdateModeUi();
	}

	private void OnModePressed()
	{
		_mode = _mode == GameState.GameMode.PlayerVsPlayer
			? GameState.GameMode.PlayerVsAi
			: GameState.GameMode.PlayerVsPlayer;

		UpdateModeUi();
	}

	private void OnDifficultyPressed()
	{
		// Cycle Beginner -> Normal -> Intermediate -> Beginner.
		_difficulty = (GameState.AiDifficulty)(((int)_difficulty + 1) % 3);
		UpdateModeUi();
	}

	private void UpdateModeUi()
	{
		bool vsAi = _mode == GameState.GameMode.PlayerVsAi;

		_modeButton.Text = vsAi ? "Mode: vs Computer" : "Mode: 2 Players";
		_difficultyButton.Disabled = !vsAi;
		_difficultyButton.Text = $"AI: {DifficultyName(_difficulty)}";
	}

	private static string DifficultyName(GameState.AiDifficulty difficulty)
	{
		return difficulty switch
		{
			GameState.AiDifficulty.Beginner => "Beginner",
			GameState.AiDifficulty.Intermediate => "Intermediate",
			_ => "Normal",
		};
	}

	private void OnPlayPressed()
	{
		if (_isStartingMatch)
			return;

		_isStartingMatch = true;
		_playButton.Disabled = true;

		GD.Print($"Play button pressed. Mode={_mode}, Difficulty={_difficulty}");

		var gameState = GetNode<GameState>("/root/GameState");
		gameState.Mode = _mode;
		gameState.Difficulty = _difficulty;
		gameState.ResetMatch();

		GetNode<SceneManager>("/root/SceneManager").GoToMatch();
	}

	private void OnInstructionsPressed()
	{
		GD.Print("Instructions button pressed.");

		GetNode<SceneManager>("/root/SceneManager").GoToInstructions();
	}

	private void OnSettingsPressed()
	{
		GD.Print("Settings button pressed.");

		GetNode<SceneManager>("/root/SceneManager").GoToSettings();
	}

	private void OnQuitPressed()
	{
		GD.Print("Quit button pressed.");

		GetTree().Quit();
	}
}
