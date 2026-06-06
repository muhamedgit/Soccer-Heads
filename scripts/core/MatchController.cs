using Godot;
using System;
using System.Threading.Tasks;

public partial class MatchController : Node2D
{
	private PlayerController _playerOne;
	private PlayerController _playerTwo;
	private Ball _ball;
	private GameState _gameState;
	private SceneManager _sceneManager;
	private bool _matchEnded;
	private bool _isResetting;
	private bool _countingDown;

	private CanvasLayer _countdownLayer;
	private Label _countdownLabel;

	// Goal celebration: screen shake (via a code-built Camera2D) + a centered "GOAL!" banner.
	private Camera2D _camera;
	private readonly RandomNumberGenerator _shakeRng = new RandomNumberGenerator();
	private float _shakeTimeLeft;
	private float _shakeDuration;
	private float _shakeMagnitude;

	private CanvasLayer _goalBannerLayer;
	private Control _goalBannerRoot;
	private Label _goalBannerTitle;
	private Label _goalBannerScorer;
	private Label _goalBannerScore;
	private Tween _goalBannerTween;

	private GoalTrigger _leftGoal;
	private GoalTrigger _rightGoal;

	// Goal celebration shown on every goal: screen shake + centered banner, then fade before kickoff.
	[Export] public float ShakeMagnitude = 18f;            // peak camera offset in px on the 2304x1536 viewport
	[Export] public float ShakeDuration = 0.4f;            // ~0.3-0.5s per spec
	[Export] public float GoalCelebrationDuration = 1.0f;  // opaque banner hold before it fades
	[Export] public float BannerFadeSeconds = 0.3f;        // banner modulate:a 1 -> 0
	[Export] public int CelebrationCameraCenterX = 1152;   // viewport_width / 2
	[Export] public int CelebrationCameraCenterY = 768;    // viewport_height / 2
	[Export] public int BannerTitleFontSize = 220;
	[Export] public int BannerScorerFontSize = 110;
	[Export] public int BannerScoreFontSize = 180;
	[Export] public Color BannerTitleColor = Palette.ArcadeYellow;
	[Export] public Color BannerScorerOneColor = Palette.TeamBlue;   // Player 1 scored
	[Export] public Color BannerScorerTwoColor = Palette.TeamRed;    // Player 2 / Computer scored
	[Export] public Color BannerScoreColor = Palette.UiText;
	[Export] public int BannerCanvasLayer = 3;             // above HUD (1) and countdown (2)

	// Kickoff countdown shown at match start and after every goal.
	[Export] public int CountdownStartNumber = 3;
	[Export] public float CountdownStepSeconds = 0.7f;   // time each of "3","2","1" is shown
	[Export] public float GoFlashSeconds = 0.5f;          // time "GO!" is shown before play resumes
	[Export] public int CountdownFontSize = 320;          // large text on the 2304x1536 viewport
	[Export] public string CountdownGoText = "GO!";
	[Export] public Color CountdownNumberColor = Palette.UiText;
	[Export] public Color CountdownGoColor = Palette.ArcadeYellow;
	[Export] public int CountdownCanvasLayer = 2;         // above the HUD (ScoreHud/PauseMenu default to layer 1)

	private Vector2 _playerOneStartPosition;
	private Vector2 _playerTwoStartPosition;
	private Vector2 _ballStartPosition;

	[Export] public Vector2 LeftGoalHighlightPosition = new Vector2(145f, 955f);
	[Export] public Vector2 RightGoalHighlightPosition = new Vector2(2160f, 955f);

	[Export] public int HighlightWidth = 330;
	[Export] public int HighlightHeight = 450;
	[Export] public int HighlightZIndex = 1;

	[Export] public Color LeftGoalColor = new Color(0.18f, 0.48f, 1.0f, 0.30f);
	[Export] public Color RightGoalColor = new Color(1.0f, 0.22f, 0.22f, 0.30f);
	[Export] public Color WhiteShineColor = new Color(1f, 1f, 1f, 0.20f);
	[Export] public Color SoftWhiteColor = new Color(1f, 1f, 1f, 0.10f);

	public override void _Ready()
	{
		GD.Print("MatchController loaded.");

		_playerOne = GetNode<PlayerController>("Player1");
		_playerTwo = GetNode<PlayerController>("Player2");
		_ball = GetNode<Ball>("Ball");
		_gameState = GetNode<GameState>("/root/GameState");
		_sceneManager = GetNode<SceneManager>("/root/SceneManager");

		_playerOneStartPosition = _playerOne.GlobalPosition;
		_playerTwoStartPosition = _playerTwo.GlobalPosition;
		_ballStartPosition = _ball.GlobalPosition;

		ResetMatchObjects();
		CreateGoalHighlights();
		CreateMatchCamera();
		ConnectGoalTriggers();
		ConfigureAiOpponent();
		CreateCountdownOverlay();
		CreateGoalCelebrationOverlay();

		// Lock input synchronously before the first physics frame so players cannot move
		// before the countdown begins, then kick off the countdown once the tree is ready.
		SetPlayersInputEnabled(false);
		Callable.From(StartKickoffCountdown).CallDeferred();
	}

	private void ConfigureAiOpponent()
	{
		if (_gameState.Mode != GameState.GameMode.PlayerVsAi)
			return;

		// Player 2 is the computer. It attacks the goal on the opposite side of its home,
		// derived from the start positions so it works regardless of arena orientation.
		float center = (_playerOneStartPosition.X + _playerTwoStartPosition.X) * 0.5f;
		int attackDir = _playerTwoStartPosition.X >= center ? -1 : 1;

		_playerTwo.ConfigureAsAi(_gameState.Difficulty, attackDir, _ball);
		GD.Print($"AI opponent enabled for Player 2 (difficulty {_gameState.Difficulty}, attackDir {attackDir}).");
	}

	private void ConnectGoalTriggers()
	{
		_leftGoal = GetNodeOrNull<GoalTrigger>("Arena/Goals/LeftGoal");
		_rightGoal = GetNodeOrNull<GoalTrigger>("Arena/Goals/RightGoal");

		if (_leftGoal != null)
			_leftGoal.GoalScored += OnGoalScored;
		else
			GD.PushWarning("LeftGoal node not found at Arena/Goals/LeftGoal.");

		if (_rightGoal != null)
			_rightGoal.GoalScored += OnGoalScored;
		else
			GD.PushWarning("RightGoal node not found at Arena/Goals/RightGoal.");
	}

	private async void OnGoalScored(int scoringPlayerIndex)
	{
		if (_matchEnded || _isResetting || _countingDown)
			return;

		if (scoringPlayerIndex == 0)
			_gameState.AddGoalForPlayerOne();
		else
			_gameState.AddGoalForPlayerTwo();

		// Capture the match-over state after the goal is counted, but celebrate either way.
		bool matchOver = _gameState.IsMatchOver();

		// Freeze the clock and lock input for the whole celebration (winning goal included).
		_isResetting = true;
		SetPlayersInputEnabled(false);
		ResetMatchObjects();

		// Punchy feedback: shake the view and show the centered "GOAL!" banner with the new score.
		TriggerScreenShake();
		ShowGoalBanner(scoringPlayerIndex);

		if (GoalCelebrationDuration > 0f)
			await ToSignal(GetTree().CreateTimer(GoalCelebrationDuration), SceneTreeTimer.SignalName.Timeout);

		if (!IsInstanceValid(this))
			return;

		// Fade the banner fully out before anything else, so it never overlaps the countdown.
		await FadeOutGoalBannerAsync();

		if (!IsInstanceValid(this))
			return;

		// A match-winning goal celebrates first, then goes to the end screen.
		if (matchOver)
		{
			HideGoalBanner();
			EndMatch();
			return;
		}

		if (_matchEnded)
			return;

		// Re-arm the goals before play resumes; the ball is centred and motionless here.
		_leftGoal?.ResetLock();
		_rightGoal?.ResetLock();

		// Run the kickoff countdown (it re-enables input once "GO!" clears). The clock stays
		// frozen throughout: _isResetting remains true until the countdown takes over the
		// freeze via _countingDown, so at least one freeze flag is set the whole transition.
		await RunKickoffCountdownAsync();

		if (!IsInstanceValid(this))
			return;

		_isResetting = false;
	}

	public override void _Process(double delta)
	{
		if (_camera == null)
			return;

		if (_shakeTimeLeft > 0f)
		{
			_shakeTimeLeft -= (float)delta;
			float falloff = _shakeDuration > 0f ? Mathf.Clamp(_shakeTimeLeft / _shakeDuration, 0f, 1f) : 0f;
			float amp = _shakeMagnitude * falloff;
			_camera.Offset = new Vector2(
				_shakeRng.RandfRange(-1f, 1f) * amp,
				_shakeRng.RandfRange(-1f, 1f) * amp);
		}
		else if (_camera.Offset != Vector2.Zero)
		{
			// Return to pixel-identical framing once the shake is over.
			_camera.Offset = Vector2.Zero;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Freeze the clock while the match is over, during the post-goal reset,
		// or while the kickoff countdown is running.
		if (_matchEnded || _isResetting || _countingDown)
			return;

		_gameState.SetTimeLeft(_gameState.MatchTimeLeft - (float)delta);

		if (_gameState.IsMatchOver())
			EndMatch();
	}

	private void EndMatch()
	{
		if (_matchEnded)
			return;

		_matchEnded = true;

		SetPlayersInputEnabled(false);

		if (_ball != null)
		{
			_ball.LinearVelocity = Vector2.Zero;
			_ball.AngularVelocity = 0f;
		}

		_sceneManager.GoToEndScreen();
	}

	public void ResetMatchObjects()
	{
		_playerOne.GlobalPosition = _playerOneStartPosition;
		_playerOne.Velocity = Vector2.Zero;

		_playerTwo.GlobalPosition = _playerTwoStartPosition;
		_playerTwo.Velocity = Vector2.Zero;

		_ball.ResetTo(_ballStartPosition);

		GD.Print("Match objects reset.");
	}

	private void SetPlayersInputEnabled(bool enabled)
	{
		if (_playerOne != null)
			_playerOne.InputEnabled = enabled;

		if (_playerTwo != null)
			_playerTwo.InputEnabled = enabled;
	}

	private void CreateCountdownOverlay()
	{
		// Rebuild defensively (mirrors CreateGoalHighlights) so a reused tree never stacks overlays.
		GetNodeOrNull<CanvasLayer>("CountdownLayer")?.QueueFree();

		_countdownLayer = new CanvasLayer
		{
			Name = "CountdownLayer",
			Layer = CountdownCanvasLayer,
			Visible = false
		};
		AddChild(_countdownLayer);

		// Full-rect root so the label stays centred at any window scale; ignore mouse input so
		// the pause menu buttons remain clickable while the overlay is visible.
		var root = new Control { Name = "CountdownRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_countdownLayer.AddChild(root);

		_countdownLabel = new Label
		{
			Name = "CountdownLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_countdownLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		Palette.ApplyReadableLabel(_countdownLabel, CountdownFontSize);
		root.AddChild(_countdownLabel);
	}

	// async void starter so _Ready (which cannot be async) can launch the countdown via CallDeferred.
	private async void StartKickoffCountdown()
	{
		await RunKickoffCountdownAsync();
	}

	// Shared "3-2-1-GO!" countdown used at match start and after every goal. Locks input,
	// shows the overlay, then re-enables input once "GO!" clears. The SceneTreeTimer waits
	// respect GetTree().Paused, so the countdown naturally freezes and resumes with the pause menu.
	private async Task RunKickoffCountdownAsync()
	{
		if (_countingDown)
			return;

		_countingDown = true;
		SetPlayersInputEnabled(false);

		if (_countdownLayer != null)
			_countdownLayer.Visible = true;

		for (int n = CountdownStartNumber; n >= 1; n--)
		{
			if (_countdownLabel != null)
			{
				_countdownLabel.AddThemeColorOverride("font_color", CountdownNumberColor);
				_countdownLabel.Text = n.ToString();
			}

			await ToSignal(GetTree().CreateTimer(CountdownStepSeconds), SceneTreeTimer.SignalName.Timeout);

			if (!IsInstanceValid(this))
				return;
			if (_matchEnded)
			{
				CleanupCountdown(reenableInput: false);
				return;
			}
		}

		if (_countdownLabel != null)
		{
			_countdownLabel.AddThemeColorOverride("font_color", CountdownGoColor);
			_countdownLabel.Text = CountdownGoText;
		}

		await ToSignal(GetTree().CreateTimer(GoFlashSeconds), SceneTreeTimer.SignalName.Timeout);

		if (!IsInstanceValid(this))
			return;
		if (_matchEnded)
		{
			CleanupCountdown(reenableInput: false);
			return;
		}

		CleanupCountdown(reenableInput: true);
	}

	private void CleanupCountdown(bool reenableInput)
	{
		_countingDown = false;

		if (IsInstanceValid(_countdownLayer))
			_countdownLayer.Visible = false;

		if (reenableInput)
			SetPlayersInputEnabled(true);
	}

	private void CreateMatchCamera()
	{
		// Centre the camera on the viewport so framing matches the previous camera-less view.
		// Only its Offset is shaken (render-only); Position never changes, so gameplay/goal
		// trigger world coordinates are untouched.
		GetNodeOrNull<Camera2D>("MatchCamera")?.QueueFree();

		_camera = new Camera2D
		{
			Name = "MatchCamera",
			Position = new Vector2(CelebrationCameraCenterX, CelebrationCameraCenterY),
			Zoom = Vector2.One,
			AnchorMode = Camera2D.AnchorModeEnum.DragCenter
		};
		AddChild(_camera);
		_camera.MakeCurrent();
		_shakeRng.Randomize();
	}

	private void TriggerScreenShake()
	{
		_shakeDuration = ShakeDuration;
		_shakeTimeLeft = ShakeDuration;
		_shakeMagnitude = ShakeMagnitude;
	}

	private void CreateGoalCelebrationOverlay()
	{
		GetNodeOrNull<CanvasLayer>("GoalCelebrationLayer")?.QueueFree();

		_goalBannerLayer = new CanvasLayer
		{
			Name = "GoalCelebrationLayer",
			Layer = BannerCanvasLayer,
			Visible = false
		};
		AddChild(_goalBannerLayer);

		// Full-rect root whose Modulate drives the fade; ignore mouse so pause buttons stay clickable.
		_goalBannerRoot = new Control { Name = "GoalBannerRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
		_goalBannerRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_goalBannerLayer.AddChild(_goalBannerRoot);

		var box = new VBoxContainer
		{
			Name = "GoalBannerBox",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		box.AddThemeConstantOverride("separation", 16);
		_goalBannerRoot.AddChild(box);

		_goalBannerTitle = CreateBannerLabel("GoalBannerTitle", BannerTitleFontSize, BannerTitleColor);
		_goalBannerTitle.Text = "GOAL!";
		box.AddChild(_goalBannerTitle);

		_goalBannerScorer = CreateBannerLabel("GoalBannerScorer", BannerScorerFontSize, BannerScorerOneColor);
		box.AddChild(_goalBannerScorer);

		_goalBannerScore = CreateBannerLabel("GoalBannerScore", BannerScoreFontSize, BannerScoreColor);
		box.AddChild(_goalBannerScore);
	}

	private Label CreateBannerLabel(string nodeName, int fontSize, Color color)
	{
		var label = new Label
		{
			Name = nodeName,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		Palette.ApplyReadableLabel(label, fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private void ShowGoalBanner(int scoringPlayerIndex)
	{
		if (_goalBannerLayer == null)
			return;

		if (_goalBannerTween != null && _goalBannerTween.IsValid())
			_goalBannerTween.Kill();
		_goalBannerTween = null;

		bool playerOneScored = scoringPlayerIndex == 0;
		string scorer = playerOneScored
			? "PLAYER 1"
			: (_gameState.Mode == GameState.GameMode.PlayerVsAi ? "COMPUTER" : "PLAYER 2");

		_goalBannerScorer.Text = scorer;
		_goalBannerScorer.AddThemeColorOverride(
			"font_color", playerOneScored ? BannerScorerOneColor : BannerScorerTwoColor);
		_goalBannerScore.Text = _gameState.GetFinalScoreText();

		_goalBannerRoot.Modulate = new Color(1f, 1f, 1f, 1f);
		_goalBannerLayer.Visible = true;
	}

	private async Task FadeOutGoalBannerAsync()
	{
		if (_goalBannerLayer == null || !_goalBannerLayer.Visible)
			return;

		_goalBannerTween = CreateTween();
		_goalBannerTween.TweenProperty(_goalBannerRoot, "modulate:a", 0f, BannerFadeSeconds);
		await ToSignal(_goalBannerTween, Tween.SignalName.Finished);

		if (!IsInstanceValid(this))
			return;

		HideGoalBanner();
	}

	private void HideGoalBanner()
	{
		if (_goalBannerTween != null && _goalBannerTween.IsValid())
			_goalBannerTween.Kill();
		_goalBannerTween = null;

		if (IsInstanceValid(_goalBannerLayer))
			_goalBannerLayer.Visible = false;
		if (IsInstanceValid(_goalBannerRoot))
			_goalBannerRoot.Modulate = new Color(1f, 1f, 1f, 1f);
	}

	private void CreateGoalHighlights()
	{
		var arena = GetNodeOrNull<Node2D>("Arena");
		if (arena == null)
		{
			GD.PushWarning("Arena node not found. Goal highlights were not created.");
			return;
		}

		var oldRoot = arena.GetNodeOrNull<Node2D>("GoalHighlights");
		if (oldRoot != null)
			oldRoot.QueueFree();

		var root = new Node2D();
		root.Name = "GoalHighlights";
		arena.AddChild(root);

		root.AddChild(CreateGoalHighlight(
			"LeftGoalHighlight",
			LeftGoalHighlightPosition,
			LeftGoalColor,
			true
		));

		root.AddChild(CreateGoalHighlight(
			"RightGoalHighlight",
			RightGoalHighlightPosition,
			RightGoalColor,
			false
		));
	}

	private Sprite2D CreateGoalHighlight(string nodeName, Vector2 position, Color color, bool isLeftGoal)
	{
		var sprite = new Sprite2D();
		sprite.Name = nodeName;
		sprite.Centered = true;
		sprite.Position = position;
		sprite.ZIndex = HighlightZIndex;
		sprite.Texture = BuildGoalHighlightTexture(
			Math.Max(HighlightWidth, 160),
			Math.Max(HighlightHeight, 220),
			color,
			isLeftGoal
		);

		return sprite;
	}

	private Texture2D BuildGoalHighlightTexture(int width, int height, Color goalColor, bool isLeftGoal)
	{
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		DrawMainGlow(image, goalColor);
		DrawGoalMouthShine(image, isLeftGoal);
		DrawSoftRimLight(image, goalColor, isLeftGoal);
		DrawSparkles(image, goalColor, isLeftGoal);

		return ImageTexture.CreateFromImage(image);
	}

	private void DrawMainGlow(Image image, Color color)
	{
		int width = image.GetWidth();
		int height = image.GetHeight();

		float centerX = width * 0.5f;
		float centerY = height * 0.52f;

		float radiusX = width * 0.48f;
		float radiusY = height * 0.46f;

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				float dx = (x - centerX) / radiusX;
				float dy = (y - centerY) / radiusY;
				float distance = Mathf.Sqrt(dx * dx + dy * dy);

				if (distance > 1f)
					continue;

				float fade = 1f - distance;
				float alpha = color.A * fade * fade * 0.90f;

				Color glowPixel = new Color(color.R, color.G, color.B, alpha);
				image.SetPixel(x, y, AlphaBlend(image.GetPixel(x, y), glowPixel));
			}
		}
	}

	private void DrawGoalMouthShine(Image image, bool isLeftGoal)
	{
		int width = image.GetWidth();
		int height = image.GetHeight();

		int shineWidth = Math.Max(10, width / 24);
		int shineHeight = (int)(height * 0.55f);
		int shineY = (height - shineHeight) / 2;

		int shineX = isLeftGoal
			? (int)(width * 0.64f)
			: (int)(width * 0.32f);

		DrawSoftVerticalStrip(image, shineX, shineY, shineWidth, shineHeight, WhiteShineColor);
	}

	private void DrawSoftRimLight(Image image, Color color, bool isLeftGoal)
	{
		int width = image.GetWidth();
		int height = image.GetHeight();

		int rimWidth = Math.Max(8, width / 28);
		int rimHeight = (int)(height * 0.48f);
		int rimY = (height - rimHeight) / 2;

		int rimX = isLeftGoal
			? (int)(width * 0.69f)
			: (int)(width * 0.27f);

		Color rimColor = new Color(color.R, color.G, color.B, 0.26f);
		DrawSoftVerticalStrip(image, rimX, rimY, rimWidth, rimHeight, rimColor);
	}

	private void DrawSparkles(Image image, Color color, bool isLeftGoal)
	{
		int width = image.GetWidth();
		int height = image.GetHeight();

		Vector2[] sparkles;

		if (isLeftGoal)
		{
			sparkles = new Vector2[]
			{
				new Vector2(width * 0.58f, height * 0.28f),
				new Vector2(width * 0.68f, height * 0.66f),
			};
		}
		else
		{
			sparkles = new Vector2[]
			{
				new Vector2(width * 0.38f, height * 0.28f),
				new Vector2(width * 0.28f, height * 0.66f),
			};
		}

		foreach (Vector2 sparkle in sparkles)
		{
			DrawSoftCircle(image, sparkle, 12f, new Color(1f, 1f, 1f, 0.16f));
			DrawSoftCircle(image, sparkle, 6f, new Color(color.R, color.G, color.B, 0.20f));
		}
	}

	private void DrawSoftVerticalStrip(Image image, int x, int y, int width, int height, Color color)
	{
		int startX = Math.Max(0, x);
		int startY = Math.Max(0, y);
		int endX = Math.Min(image.GetWidth(), x + width);
		int endY = Math.Min(image.GetHeight(), y + height);

		float centerX = x + width * 0.5f;

		for (int py = startY; py < endY; py++)
		{
			for (int px = startX; px < endX; px++)
			{
				float horizontalDistance = Mathf.Abs(px - centerX) / Math.Max(width * 0.5f, 1f);
				float fadeX = 1f - horizontalDistance;

				float topFade = Mathf.Clamp((py - y) / 28f, 0f, 1f);
				float bottomFade = Mathf.Clamp((y + height - py) / 28f, 0f, 1f);

				float alpha = color.A * fadeX * topFade * bottomFade;

				if (alpha <= 0f)
					continue;

				Color pixel = new Color(color.R, color.G, color.B, alpha);
				image.SetPixel(px, py, AlphaBlend(image.GetPixel(px, py), pixel));
			}
		}
	}

	private void DrawSoftCircle(Image image, Vector2 center, float radius, Color color)
	{
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Math.Min(image.GetWidth() - 1, Mathf.CeilToInt(center.X + radius));
		int minY = Math.Max(0, Mathf.FloorToInt(center.Y - radius));
		int maxY = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(center.Y + radius));

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				float distance = new Vector2(x, y).DistanceTo(center);

				if (distance > radius)
					continue;

				float fade = 1f - distance / radius;
				float alpha = color.A * fade * fade;

				Color pixel = new Color(color.R, color.G, color.B, alpha);
				image.SetPixel(x, y, AlphaBlend(image.GetPixel(x, y), pixel));
			}
		}
	}

	private Color AlphaBlend(Color background, Color foreground)
	{
		float outA = foreground.A + background.A * (1f - foreground.A);

		if (outA <= 0f)
			return Colors.Transparent;

		float outR = (foreground.R * foreground.A + background.R * background.A * (1f - foreground.A)) / outA;
		float outG = (foreground.G * foreground.A + background.G * background.A * (1f - foreground.A)) / outA;
		float outB = (foreground.B * foreground.A + background.B * background.A * (1f - foreground.A)) / outA;

		return new Color(outR, outG, outB, outA);
	}
}
