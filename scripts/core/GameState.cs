using Godot;

public partial class GameState : Node
{
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
	}

	public void AddGoalForPlayerOne()
	{
		PlayerOneScore++;
	}

	public void AddGoalForPlayerTwo()
	{
		PlayerTwoScore++;
	}

	public void SetTimeLeft(float timeLeft)
	{
		MatchTimeLeft = Mathf.Max(0f, timeLeft);
	}

	public bool IsMatchOver()
	{
		return PlayerOneScore >= ScoreToWin ||
			   PlayerTwoScore >= ScoreToWin ||
			   MatchTimeLeft <= 0f;
	}
}
