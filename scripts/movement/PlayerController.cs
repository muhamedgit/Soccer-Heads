using Godot;
using System;

public partial class PlayerController : CharacterBody2D
{
	[Export] public float Speed = 420f;
	[Export] public float GravityUp = 2000f;
	[Export] public float GravityDown = 3800f;
	[Export] public float MaxFallSpeed = 1400f;
	[Export] public float JumpVelocity = -950f;

	[Export] public string LeftAction = "Player1_Left";
	[Export] public string RightAction = "Player1_Right";
	[Export] public string JumpAction = "Player1_Jump";

	[Export] public float MinX = 50f;
	[Export] public float MaxX = 2250f;

	// Bounce off the player: the faster the player moves into the ball, the harder it
	// launches. Outgoing speed = baseline + reflected incoming speed + a share of player speed.
	[Export] public float BaseKickSpeed = 250f;    // launch even from a standing touch
	[Export] public float Bounciness = 1.4f;       // how much of the ball's incoming speed is reflected
	[Export] public float KickSpeedFactor = 1.3f;  // how much of the player's speed adds to the launch
	[Export] public float MaxBallSpeed = 2200f;    // cap on the resulting launch speed

	// Continuous push lets the player dribble/shove the ball while staying in contact,
	// instead of only getting a single impulse on first touch.
	[Export] public float MinPushSpeed = 5f;         // ignore pushes slower than this
	[Export] public float PushResponsiveness = 0.8f; // how quickly the ball matches the push speed (0..1)

	[Export] public Color Player1BodyColor = new Color(0.20f, 0.45f, 0.95f, 1.0f);
	[Export] public Color Player2BodyColor = new Color(0.90f, 0.22f, 0.22f, 1.0f);
	[Export] public Color OutlineColor = new Color(0f, 0f, 0f, 1f);
	[Export] public int PlaceholderWidth = 192;
	[Export] public int PlaceholderHeight = 256;
	[Export] public int OutlineThickness = 5;

	public enum ControlMode { Human, Ai }

	[Export] public ControlMode Control = ControlMode.Human;

	public bool InputEnabled { get; set; } = true;

	private bool _wasOnFloor;
	private RigidBody2D _ballInContact;

	private GameState _gameState;
	private int _playerIndex;

	private AiController _aiBrain;
	private RigidBody2D _aiBall;
	private int _aiAttackDir = -1;
	private Vector2 _aiTargetGoal;
	private Vector2 _aiOwnGoal;
	private PerkManager _aiPerkManager;

	// Called by MatchController to turn this player into an AI opponent.
	public void ConfigureAsAi(GameState.AiDifficulty difficulty, int attackDirection, RigidBody2D ball,
		Vector2 targetGoal, Vector2 ownGoal, PerkManager perkManager)
	{
		Control = ControlMode.Ai;
		_aiAttackDir = attackDirection;
		_aiBall = ball;
		_aiTargetGoal = targetGoal;
		_aiOwnGoal = ownGoal;
		_aiPerkManager = perkManager;
		_aiBrain = new AiController();
		_aiBrain.Configure(difficulty);
	}

	public override void _Ready()
	{
		UpDirection = Vector2.Up;
		FloorMaxAngle = Mathf.DegToRad(50f);
		FloorSnapLength = 8f;

		_gameState = GetNodeOrNull<GameState>("/root/GameState");
		_playerIndex = DetectPlayerIndex() - 1; // 0 = Player 1, 1 = Player 2

		SetupPlaceholderSprite();
	}

	public override void _PhysicsProcess(double delta)
	{
		ReadControls(delta, out float moveAxis, out bool jumpPressed);

		var v = Velocity;
		v.X = InputEnabled ? moveAxis * Speed : 0f;

		if (!IsOnFloor())
		{
			float g = v.Y < 0f ? GravityUp : GravityDown;
			v.Y += g * (float)delta;
			v.Y = Mathf.Min(v.Y, MaxFallSpeed);
		}
		else if (v.Y > 0f)
		{
			v.Y = 0f;
		}

		if (InputEnabled && jumpPressed && IsOnFloor())
			v.Y = JumpVelocity;

		Velocity = v;
		MoveAndSlide();

		RigidBody2D touchedBall = null;
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var collision = GetSlideCollision(i);
			if (collision.GetCollider() is RigidBody2D rb && rb.IsInGroup("ball"))
			{
				touchedBall = rb;
				break;
			}
		}

		// Pop the ball on fresh contact (the "kick"), then keep pushing it while in
		// contact so the player can dribble/shove rather than the ball sticking.
		if (touchedBall != null && touchedBall != _ballInContact)
			ApplyKickImpulse(touchedBall);

		if (touchedBall != null)
		{
			ApplyContinuousPush(touchedBall);

			// Record possession so a perk the ball collects is applied to the last toucher.
			if (_gameState != null)
				_gameState.LastBallToucher = _playerIndex;
		}

		_ballInContact = touchedBall;

		var p = GlobalPosition;
		p.X = Mathf.Clamp(p.X, MinX, MaxX);
		GlobalPosition = p;

		bool onFloor = IsOnFloor();
		if (onFloor != _wasOnFloor)
		{
#if DEBUG
			GD.Print($"{Name} OnFloor: {onFloor}");
#endif
			_wasOnFloor = onFloor;
		}
	}

	// Movement intent comes from the keyboard for a human, or from the AI brain otherwise.
	private void ReadControls(double delta, out float moveAxis, out bool jumpPressed)
	{
		if (Control == ControlMode.Ai && _aiBrain != null && IsInstanceValid(_aiBall))
		{
			var ctx = new AiController.AiContext
			{
				SelfPos = GlobalPosition,
				BallPos = _aiBall.GlobalPosition,
				BallVel = _aiBall.LinearVelocity,
				TargetGoal = _aiTargetGoal,
				OwnGoal = _aiOwnGoal,
				AttackDir = _aiAttackDir,
				OnFloor = IsOnFloor(),
				IsLastToucher = _gameState != null && _gameState.LastBallToucher == _playerIndex,
				Delta = delta
			};

			if (_aiPerkManager != null &&
				_aiPerkManager.TryGetActivePerk(out Vector2 perkPos, out bool perkAdvantage))
			{
				ctx.HasPerk = true;
				ctx.PerkPos = perkPos;
				ctx.PerkIsAdvantage = perkAdvantage;
			}

			(moveAxis, jumpPressed) = _aiBrain.Think(in ctx);
			return;
		}

		moveAxis = Input.GetAxis(LeftAction, RightAction);
		jumpPressed = Input.IsActionJustPressed(JumpAction);
	}

	private void ApplyKickImpulse(RigidBody2D ball)
	{
		Vector2 contactNormal = (ball.GlobalPosition - GlobalPosition).Normalized();
		float playerSpeed = Velocity.Length();

		// Blend contact normal with player movement direction so fast runs send the ball forward
		Vector2 dir = playerSpeed > 10f
			? (contactNormal + Velocity.Normalized()).Normalized()
			: contactNormal;

		float ballAlong = ball.LinearVelocity.Dot(dir);
		float playerInto = Mathf.Max(0f, Velocity.Dot(dir));  // player speed heading into the ball
		float incoming = Mathf.Max(0f, -ballAlong);           // ball speed heading into the player

		// Bouncier the faster the player moves: launch = baseline + reflected incoming
		// speed (bounciness) + a share of the player's speed.
		float targetOut = BaseKickSpeed + incoming * Bounciness + playerInto * KickSpeedFactor;
		targetOut = Mathf.Min(targetOut, MaxBallSpeed);

		float currentOut = Mathf.Max(0f, ballAlong);
		float deltaV = targetOut - currentOut;
		if (deltaV > 0f)
			ball.ApplyCentralImpulse(dir * deltaV * ball.Mass);
	}

	// While the player overlaps the ball and moves into it, bring the ball's speed
	// along the push direction up toward the player's speed, so walking into the ball
	// dribbles it instead of the ball stalling against the body.
	private void ApplyContinuousPush(RigidBody2D ball)
	{
		Vector2 toBall = ball.GlobalPosition - GlobalPosition;
		if (toBall.LengthSquared() < 1f)
			return;

		Vector2 dir = toBall.Normalized();
		float playerInto = Velocity.Dot(dir);
		if (playerInto <= MinPushSpeed)
			return;

		float ballAlong = ball.LinearVelocity.Dot(dir);
		if (ballAlong >= playerInto)
			return;

		float deltaV = playerInto - ballAlong;
		ball.ApplyCentralImpulse(dir * deltaV * ball.Mass * PushResponsiveness);
	}

	private void SetupPlaceholderSprite()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite == null)
		{
			GD.PushWarning($"{Name} is missing Sprite2D.");
			return;
		}

		var collision = GetNodeOrNull<CollisionShape2D>("PlayerCollisionShape");

		sprite.Centered = true;
		sprite.Position = Vector2.Zero;
		sprite.ZIndex = 10;

		if (collision != null)
			collision.Position = Vector2.Zero;

		int playerIndex = DetectPlayerIndex();

		Color bodyColor = playerIndex == 2 ? Player2BodyColor : Player1BodyColor;
		Color accentColor = playerIndex == 2
			? new Color(1.0f, 0.93f, 0.20f, 1.0f)
			: Colors.White;

		sprite.Texture = BuildPlaceholderTexture(
			Math.Max(PlaceholderWidth, 48),
			Math.Max(PlaceholderHeight, 64),
			playerIndex,
			bodyColor,
			accentColor,
			OutlineColor,
			Math.Max(OutlineThickness, 2)
		);
	}

	private int DetectPlayerIndex()
	{
		string nodeName = Name.ToString();

		if (LeftAction.Contains("Player2") || nodeName.Contains("2"))
			return 2;

		return 1;
	}

	private Texture2D BuildPlaceholderTexture(
		int width,
		int height,
		int playerIndex,
		Color bodyColor,
		Color accentColor,
		Color outlineColor,
		int outline)
	{
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color headColor = LightenColor(bodyColor, 0.18f);
		Color shortsColor = DarkenColor(bodyColor, 0.25f);

		int torsoX = width / 2 - width / 5;
		int torsoY = height / 3;
		int torsoW = width / 5 * 2;
		int torsoH = height / 3;

		int legW = width / 7;
		int legH = height / 5;
		int leftLegX = width / 2 - legW - width / 18;
		int rightLegX = width / 2 + width / 18;
		int legY = torsoY + torsoH - outline;

		int armW = width / 10;
		int armH = height / 4;
		int leftArmX = torsoX - armW + outline;
		int rightArmX = torsoX + torsoW - outline;
		int armY = torsoY + height / 18;

		Vector2 headCenter = new Vector2(width / 2.0f, height * 0.20f);
		float headRadius = width * 0.17f;

		DrawFilledCircle(image, headCenter, headRadius + outline, outlineColor);
		DrawFilledCircle(image, headCenter, headRadius, headColor);

		DrawRect(image, leftArmX - outline, armY - outline, armW + outline * 2, armH + outline * 2, outlineColor);
		DrawRect(image, rightArmX - outline, armY - outline, armW + outline * 2, armH + outline * 2, outlineColor);
		DrawRect(image, leftArmX, armY, armW, armH, bodyColor);
		DrawRect(image, rightArmX, armY, armW, armH, bodyColor);

		DrawRect(image, torsoX - outline, torsoY - outline, torsoW + outline * 2, torsoH + outline * 2, outlineColor);
		DrawRect(image, torsoX, torsoY, torsoW, torsoH, bodyColor);

		DrawRect(image, torsoX, torsoY + torsoH - height / 12, torsoW, height / 12, shortsColor);

		if (playerIndex == 1)
		{
			int stripeH = Math.Max(6, height / 16);
			DrawRect(image, torsoX + outline, torsoY + torsoH / 4, torsoW - outline * 2, stripeH, accentColor);
		}
		else
		{
			int stripeW = Math.Max(6, width / 10);
			DrawRect(image, width / 2 - stripeW / 2, torsoY + outline, stripeW, torsoH - outline * 2, accentColor);
		}

		DrawRect(image, leftLegX - outline, legY - outline, legW + outline * 2, legH + outline * 2, outlineColor);
		DrawRect(image, rightLegX - outline, legY - outline, legW + outline * 2, legH + outline * 2, outlineColor);
		DrawRect(image, leftLegX, legY, legW, legH, shortsColor);
		DrawRect(image, rightLegX, legY, legW, legH, shortsColor);

		return ImageTexture.CreateFromImage(image);
	}

	private void DrawRect(Image image, int x, int y, int width, int height, Color color)
	{
		int startX = Math.Max(0, x);
		int startY = Math.Max(0, y);
		int endX = Math.Min(image.GetWidth(), x + width);
		int endY = Math.Min(image.GetHeight(), y + height);

		for (int py = startY; py < endY; py++)
		{
			for (int px = startX; px < endX; px++)
			{
				image.SetPixel(px, py, color);
			}
		}
	}

	private void DrawFilledCircle(Image image, Vector2 center, float radius, Color color)
	{
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Math.Min(image.GetWidth() - 1, Mathf.CeilToInt(center.X + radius));
		int minY = Math.Max(0, Mathf.FloorToInt(center.Y - radius));
		int maxY = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(center.Y + radius));

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				Vector2 p = new Vector2(x, y);
				if (p.DistanceTo(center) <= radius)
					image.SetPixel(x, y, color);
			}
		}
	}

	private Color LightenColor(Color color, float amount)
	{
		return new Color(
			Mathf.Lerp(color.R, 1.0f, amount),
			Mathf.Lerp(color.G, 1.0f, amount),
			Mathf.Lerp(color.B, 1.0f, amount),
			color.A
		);
	}

	private Color DarkenColor(Color color, float amount)
	{
		return new Color(
			color.R * (1.0f - amount),
			color.G * (1.0f - amount),
			color.B * (1.0f - amount),
			color.A
		);
	}
}
