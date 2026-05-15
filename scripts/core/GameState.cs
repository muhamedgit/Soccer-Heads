using Godot;

public partial class GameState : Node
{
	[Signal]
	public delegate void ScoreChangedEventHandler(int playerOneScore, int playerTwoScore);

	[Signal]
	public delegate void TimeChangedEventHandler(float secondsLeft);

	public int PlayerOneScore { get; private set; }
	public int PlayerTwoScore { get; private set; }
	public float MatchTimeLeft { get; private set; }

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
		EmitSignal(SignalName.ScoreChanged, PlayerOneScore, PlayerTwoScore);
	}

	public void AddGoalForPlayerTwo()
	{
		PlayerTwoScore++;
		EmitSignal(SignalName.ScoreChanged, PlayerOneScore, PlayerTwoScore);
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
}
