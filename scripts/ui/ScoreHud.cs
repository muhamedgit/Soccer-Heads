using Godot;

public partial class ScoreHud : CanvasLayer
{
	private Label _playerOneScoreLabel;
	private Label _playerTwoScoreLabel;
	private Label _timerLabel;
	private Control _timerPanel;
	private GameState _gameState;
	private int _lastShownSeconds = -1;

	public override void _Ready()
	{
		_playerOneScoreLabel = GetNode<Label>("HudRoot/ScorePanel/ScoreRow/PlayerOneScore");
		_playerTwoScoreLabel = GetNode<Label>("HudRoot/ScorePanel/ScoreRow/PlayerTwoScore");
		_timerLabel = GetNode<Label>("HudRoot/TimerPanel/TimerLabel");
		_timerPanel = GetNode<PanelContainer>("HudRoot/TimerPanel");
		_gameState = GetNode<GameState>("/root/GameState");

		// Hide the timer entirely for an untimed match.
		_timerPanel.Visible = _gameState.IsTimed;

		if (_gameState.Mode == GameState.GameMode.PlayerVsAi)
		{
			var playerTwoName = GetNodeOrNull<Label>(
				"HudRoot/ScorePanel/ScoreRow/PlayerTwoChip/PlayerTwoText/PlayerTwoName");
			if (playerTwoName != null)
				playerTwoName.Text = "COMPUTER";
		}

		CreateClubCornerLabels();

		_gameState.ScoreChanged += UpdateScore;
		_gameState.TimeChanged += UpdateTime;
		UpdateScore(_gameState.PlayerOneScore, _gameState.PlayerTwoScore);
		UpdateTime(_gameState.MatchTimeLeft);
	}

	// Show each side's chosen club name in its corner: Player 1 top-left, Player 2 top-right.
	private void CreateClubCornerLabels()
	{
		GetNodeOrNull<Control>("ClubCorners")?.QueueFree();

		var root = new Control { Name = "ClubCorners", MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		AddCornerLabel(root, "ClubOneLabel", _gameState.GetClubName(0).ToUpper(),
			Palette.TeamBlue, isLeft: true);
		AddCornerLabel(root, "ClubTwoLabel", _gameState.GetClubName(1).ToUpper(),
			Palette.TeamRed, isLeft: false);
	}

	private void AddCornerLabel(Control parent, string nodeName, string text, Color color, bool isLeft)
	{
		var label = new Label
		{
			Name = nodeName,
			Text = text,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = isLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right
		};

		label.SetAnchorsPreset(isLeft ? Control.LayoutPreset.TopLeft : Control.LayoutPreset.TopRight);
		label.GrowHorizontal = isLeft
			? Control.GrowDirection.End
			: Control.GrowDirection.Begin;

		// Inset from the screen edge.
		if (isLeft)
		{
			label.OffsetLeft = 28f;
			label.OffsetTop = 24f;
		}
		else
		{
			label.OffsetRight = -28f;
			label.OffsetTop = 24f;
		}

		Palette.ApplyReadableLabel(label, 30);
		label.AddThemeColorOverride("font_color", color);
		parent.AddChild(label);
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
		// Untimed match: the panel is hidden, so ignore the stray TimeChanged emit from ResetMatch.
		if (!_gameState.IsTimed)
			return;

		// The display only changes once per second, so skip redundant per-frame updates.
		int wholeSeconds = Mathf.FloorToInt(Mathf.Max(0f, secondsLeft));
		if (wholeSeconds == _lastShownSeconds)
			return;

		_lastShownSeconds = wholeSeconds;
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
