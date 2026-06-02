using Godot;

public partial class ScoreHud : CanvasLayer
{
	private Label _playerOneScoreLabel;
	private Label _playerTwoScoreLabel;
	private Label _timerLabel;
	private GameState _gameState;

	public override void _Ready()
	{
		_playerOneScoreLabel = GetNode<Label>("HudRoot/ScorePanel/ScoreRow/PlayerOneScore");
		_playerTwoScoreLabel = GetNode<Label>("HudRoot/ScorePanel/ScoreRow/PlayerTwoScore");
		_timerLabel = GetNode<Label>("HudRoot/TimerPanel/TimerLabel");
		_gameState = GetNode<GameState>("/root/GameState");

		if (_gameState.Mode == GameState.GameMode.PlayerVsAi)
		{
			var playerTwoName = GetNodeOrNull<Label>(
				"HudRoot/ScorePanel/ScoreRow/PlayerTwoChip/PlayerTwoText/PlayerTwoName");
			if (playerTwoName != null)
				playerTwoName.Text = "COMPUTER";
		}

		_gameState.ScoreChanged += UpdateScore;
		_gameState.TimeChanged += UpdateTime;
		UpdateScore(_gameState.PlayerOneScore, _gameState.PlayerTwoScore);
		UpdateTime(_gameState.MatchTimeLeft);
	}

	public override void _ExitTree()
	{
		if (_gameState != null)
		{
			_gameState.ScoreChanged -= UpdateScore;
			_gameState.TimeChanged -= UpdateTime;
		}
	}

	private void UpdateScore(int playerOneScore, int playerTwoScore)
	{
		_playerOneScoreLabel.Text = playerOneScore.ToString();
		_playerTwoScoreLabel.Text = playerTwoScore.ToString();
	}

	private void UpdateTime(float secondsLeft)
	{
		_timerLabel.Text = FormatTime(secondsLeft);
	}

	private static string FormatTime(float seconds)
	{
		seconds = Mathf.Max(0f, seconds);
		int totalSeconds = Mathf.FloorToInt(seconds);
		int minutes = totalSeconds / 60;
		int remainingSeconds = totalSeconds % 60;

		return $"{minutes:00}:{remainingSeconds:00}";
	}
}
