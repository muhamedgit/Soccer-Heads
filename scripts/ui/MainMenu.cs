using Godot;
using System;

public partial class MainMenu : Control
{
	private Button _playButton;
	private Button _modeButton;
	private Button _difficultyButton;
	private Button _scoreLimitButton;
	private Button _matchLengthButton;
	private Button _instructionsButton;
	private Button _settingsButton;
	private Button _quitButton;

	private bool _isStartingMatch = false;

	private GameState.GameMode _mode = GameState.GameMode.PlayerVsPlayer;
	private GameState.AiDifficulty _difficulty = GameState.AiDifficulty.Normal;

	// Match-rule presets cycled by the two buttons. 0 means "Off" (no score limit / untimed).
	private static readonly int[] ScorePresets = { 3, 5, 10, 0 };
	private static readonly float[] TimePresets = { 60f, 90f, 120f, 0f };
	private int _scoreIndex;
	private int _timeIndex;

	public override void _Ready()
	{
		GD.Print("MainMenu script loaded.");

		_playButton = GetNode<Button>("CenterContainer/VBoxContainer/PlayButton");
		_modeButton = GetNode<Button>("CenterContainer/VBoxContainer/ModeButton");
		_difficultyButton = GetNode<Button>("CenterContainer/VBoxContainer/DifficultyButton");
		_scoreLimitButton = GetNode<Button>("CenterContainer/VBoxContainer/ScoreLimitButton");
		_matchLengthButton = GetNode<Button>("CenterContainer/VBoxContainer/MatchLengthButton");
		_instructionsButton = GetNode<Button>("CenterContainer/VBoxContainer/InstructionsButton");
		_settingsButton = GetNode<Button>("CenterContainer/VBoxContainer/SettingsButton");
		_quitButton = GetNode<Button>("CenterContainer/VBoxContainer/QuitButton");

		// Restore the last chosen mode/difficulty/match settings so the menu reflects them.
		var gameState = GetNode<GameState>("/root/GameState");
		_mode = gameState.Mode;
		_difficulty = gameState.Difficulty;
		_scoreIndex = IndexOfOrDefault(ScorePresets, gameState.ScoreLimit, GameState.DefaultScoreToWin);
		_timeIndex = IndexOfOrDefault(TimePresets, gameState.MatchDurationSeconds, GameState.DefaultMatchDuration);

		_playButton.Pressed += OnPlayPressed;
		_modeButton.Pressed += OnModePressed;
		_difficultyButton.Pressed += OnDifficultyPressed;
		_scoreLimitButton.Pressed += OnScoreLimitPressed;
		_matchLengthButton.Pressed += OnMatchLengthPressed;
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

	private void OnScoreLimitPressed()
	{
		_scoreIndex = (_scoreIndex + 1) % ScorePresets.Length;
		UpdateModeUi();
	}

	private void OnMatchLengthPressed()
	{
		_timeIndex = (_timeIndex + 1) % TimePresets.Length;
		UpdateModeUi();
	}

	private void UpdateModeUi()
	{
		bool vsAi = _mode == GameState.GameMode.PlayerVsAi;

		_modeButton.Text = vsAi ? "Mode: vs Computer" : "Mode: 2 Players";
		_difficultyButton.Disabled = !vsAi;
		_difficultyButton.Text = $"AI: {DifficultyName(_difficulty)}";

		int score = ScorePresets[_scoreIndex];
		_scoreLimitButton.Text = score <= 0 ? "Score: Off" : $"Score: {score}";

		float time = TimePresets[_timeIndex];
		_matchLengthButton.Text = time <= 0f ? "Time: Off" : $"Time: {FormatTimeLabel(time)}";
	}

	private static string FormatTimeLabel(float seconds)
	{
		int total = Mathf.RoundToInt(seconds);
		return $"{total / 60}:{total % 60:00}";
	}

	private static int IndexOfOrDefault<T>(T[] presets, T value, T defaultValue)
	{
		int idx = Array.IndexOf(presets, value);
		if (idx >= 0)
			return idx;

		idx = Array.IndexOf(presets, defaultValue);
		return idx >= 0 ? idx : 0;
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

		int chosenScore = ScorePresets[_scoreIndex];
		float chosenTime = TimePresets[_timeIndex];

		// Both-Off guard: never allow an unbounded match — fall back to the default length.
		if (chosenScore <= 0 && chosenTime <= 0f)
			chosenTime = GameState.DefaultMatchDuration;

		var gameState = GetNode<GameState>("/root/GameState");
		gameState.Mode = _mode;
		gameState.Difficulty = _difficulty;
		gameState.ScoreLimit = chosenScore;
		gameState.MatchDurationSeconds = chosenTime;
		gameState.SaveMatchSettings(chosenScore, chosenTime);
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
