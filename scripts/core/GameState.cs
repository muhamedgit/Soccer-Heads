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

	// Index (0 = Player 1, 1 = Player 2, -1 = nobody yet) of the last player to touch the ball.
	// Drives which player a ball-collected perk is applied to.
	public int LastBallToucher { get; set; } = -1;

	// Chosen on the main menu and kept across scene changes (this is an autoload).
	// ResetMatch deliberately leaves these untouched.
	public GameMode Mode { get; set; } = GameMode.PlayerVsPlayer;
	public AiDifficulty Difficulty { get; set; } = AiDifficulty.Normal;

	// Match rules chosen on the main menu (also survive ResetMatch).
	// ScoreLimit <= 0 means "no score limit"; MatchDurationSeconds <= 0 means "untimed".
	public int ScoreLimit { get; set; } = DefaultScoreToWin;
	public float MatchDurationSeconds { get; set; } = DefaultMatchDuration;

	// Club + player chosen on the club-selection screen (Create Clubs feature). Indices into
	// ClubDatabase; kept across ResetMatch like Mode/Difficulty so a rematch keeps the picks.
	public int ClubOneIndex { get; set; }
	public int PlayerOneVariant { get; set; }
	public int ClubTwoIndex { get; set; } = 1;
	public int PlayerTwoVariant { get; set; }

	// Pick a random club + player for player two; used for the AI side in vs-Computer matches.
	public void RandomizeClubTwo()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		ClubTwoIndex = rng.RandiRange(0, ClubDatabase.ClubCount - 1);
		PlayerTwoVariant = rng.RandiRange(0, ClubDatabase.PlayersPerClub - 1);
	}

	public string GetClubName(int playerIndex)
	{
		int clubIndex = playerIndex == 1 ? ClubTwoIndex : ClubOneIndex;
		return ClubDatabase.GetClub(clubIndex).Name;
	}

	public bool HasScoreLimit => ScoreLimit > 0;
	public bool IsTimed => MatchDurationSeconds > 0f;

	public const float DefaultMatchDuration = 90f;
	public const int DefaultScoreToWin = 5;

	private const string SettingsPath = "user://settings.cfg";

	public override void _Ready()
	{
		// Restore the last-used match settings (autoload, runs once at startup).
		var config = new ConfigFile();
		if (config.Load(SettingsPath) != Error.Ok)
			return; // no file yet -> keep the defaults already set on the properties

		ScoreLimit = config.GetValue("match", "score_limit", DefaultScoreToWin).AsInt32();
		MatchDurationSeconds = config.GetValue("match", "match_duration", DefaultMatchDuration).AsSingle();
	}

	// Persist the chosen match settings (mirrors AudioManager's volume persistence).
	public void SaveMatchSettings(int scoreLimit, float matchDuration)
	{
		var config = new ConfigFile();
		config.Load(SettingsPath); // ignore error: file may not exist yet
		config.SetValue("match", "score_limit", scoreLimit);
		config.SetValue("match", "match_duration", matchDuration);
		config.Save(SettingsPath);
	}

	public void ResetMatch()
	{
		PlayerOneScore = 0;
		PlayerTwoScore = 0;
		LastBallToucher = -1;
		MatchTimeLeft = IsTimed ? MatchDurationSeconds : 0f;
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
		// Safety net: if both limits are off, never run forever (the menu coerces away from
		// this, so MatchTimeLeft was seeded to a finite value when this case slips through).
		if (!HasScoreLimit && !IsTimed)
			return MatchTimeLeft <= 0f;

		bool scoreReached = HasScoreLimit &&
			(PlayerOneScore >= ScoreLimit || PlayerTwoScore >= ScoreLimit);
		bool timeUp = IsTimed && MatchTimeLeft <= 0f;
		return scoreReached || timeUp;
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
			return Mode == GameMode.PlayerVsAi ? "Computer Wins" : "Player 2 Wins";

		return "Draw";
	}
}
