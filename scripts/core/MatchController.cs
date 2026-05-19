using Godot;
using System;

public partial class MatchController : Node2D
{
	private CharacterBody2D _playerOne;
	private CharacterBody2D _playerTwo;
	private RigidBody2D _ball;
	private GameState _gameState;
	private SceneManager _sceneManager;
	private bool _matchEnded;

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

		_playerOne = GetNode<CharacterBody2D>("Player1");
		_playerTwo = GetNode<CharacterBody2D>("Player2");
		_ball = GetNode<RigidBody2D>("Ball");
		_gameState = GetNode<GameState>("/root/GameState");
		_sceneManager = GetNode<SceneManager>("/root/SceneManager");

		_playerOneStartPosition = _playerOne.GlobalPosition;
		_playerTwoStartPosition = _playerTwo.GlobalPosition;
		_ballStartPosition = _ball.GlobalPosition;

		ResetMatchObjects();
		CreateGoalHighlights();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_matchEnded)
			return;

		_gameState.SetTimeLeft(_gameState.MatchTimeLeft - (float)delta);

		if (_gameState.IsMatchOver())
			EndMatch();
	}

	private void EndMatch()
	{
		_matchEnded = true;
		_sceneManager.GoToEndScreen();
	}

	public void ResetMatchObjects()
	{
		_playerOne.GlobalPosition = _playerOneStartPosition;
		_playerTwo.GlobalPosition = _playerTwoStartPosition;

		_ball.GlobalPosition = _ballStartPosition;
		_ball.LinearVelocity = Vector2.Zero;
		_ball.AngularVelocity = 0f;

		GD.Print("Match objects reset.");
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
