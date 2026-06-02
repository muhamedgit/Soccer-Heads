using Godot;

public partial class GameState : Node
{
	public enum GameMode
	{
		PlayerVsPlayer,
		PlayerVsAi
	}

	public enum AiDifficulty
	{
		Beginner,
		Normal,
		Intermediate
	}

	[Signal]
	public delegate void ScoreChangedEventHandler(int playerOneScore, int playerTwoScore);

	[Signal]
	public delegate void TimeChangedEventHandler(float secondsLeft);

	public int PlayerOneScore { get; private set; }
	public int PlayerTwoScore { get; private set; }
	public float MatchTimeLeft { get; private set; }

	// Chosen on the main menu and kept across scene changes (this is an autoload).
	// ResetMatch deliberately leaves these untouched.
	public GameMode Mode { get; set; } = GameMode.PlayerVsPlayer;
	public AiDifficulty Difficulty { get; set; } = AiDifficulty.Normal;

	public const float DefaultMatchDuration = 90f;
	public const int ScoreToWin = 5;

	public void ResetMatch()
	{
		PlayerOneScore = 0;
		PlayerTwoScore = 0;
		MatchTimeLeft = DefaultMatchDuration;
		EmitSignal(SignalName.ScoreChanged, PlayerOneScore, PlayerTwoScore);
		EmitSignal(SignalName.TimeChanged, MatchTimeLeft);
	}

	public void AddGoalForPlayerOne()
	{
		PlayerOneScore++;
		PlayGoalSound();
		EmitSignal(SignalName.ScoreChanged, PlayerOneScore, PlayerTwoScore);
	}

	public void AddGoalForPlayerTwo()
	{
		PlayerTwoScore++;
		PlayGoalSound();
		EmitSignal(SignalName.ScoreChanged, PlayerOneScore, PlayerTwoScore);
	}

	private void PlayGoalSound()
	{
		GetNodeOrNull<AudioManager>("/root/AudioManager")?.PlayGoalScoredSound();
	}

	public void SetTimeLeft(float timeLeft)
	{
		MatchTimeLeft = Mathf.Max(0f, timeLeft);
		EmitSignal(SignalName.TimeChanged, MatchTimeLeft);
	}

	public bool IsMatchOver()
	{
		return PlayerOneScore >= ScoreToWin ||
			   PlayerTwoScore >= ScoreToWin ||
			   MatchTimeLeft <= 0f;
	}

	public string GetFinalScoreText()
	{
		return $"{PlayerOneScore} - {PlayerTwoScore}";
	}

	public string GetWinnerText()
	{
		if (PlayerOneScore > PlayerTwoScore)
			return "Player 1 Wins";

		if (PlayerTwoScore > PlayerOneScore)
			return "Player 2 Wins";

		return "Draw";
	}
}
