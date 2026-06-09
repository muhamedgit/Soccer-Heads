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
	[Export] public string KickAction = "Player1_Kick";

	// Leg kick: a swing of the foot that launches the ball harder than a passive body touch.
	// While the swing is "active" the contact impulse is multiplied by KickBoost.
	[Export] public float KickBoost = 1.9f;
	[Export] public float KickActiveSeconds = 0.18f;   // window during which a touch counts as a real kick
	[Export] public float KickCooldownSeconds = 0.45f; // can't spam-kick
	[Export] public float KickSwingDegrees = 100f;     // how far the leg swings forward
	[Export] public float KickSwingSeconds = 0.16f;    // forward-swing duration (return is the same)

	[Export] public float MinX = 50f;
	[Export] public float MaxX = 2250f;

	// Bounce off the player: the faster the player moves into the ball, the harder it
	// launches. Outgoing speed = baseline + reflected incoming speed + a share of player speed.
	[Export] public float BaseKickSpeed = 250f;    // launch even from a standing touch
	[Export] public float Bounciness = 1.4f;       // how much of the ball's incoming speed is reflected
	[Export] public float KickSpeedFactor = 1.3f;  // how much of the player's speed adds to the launch
	[Export] public float MaxBallSpeed = 2200f;    // cap on the resulting launch speed
	[Export] public float KickSpinFactor = 0.35f;  // torque fraction of kick impulse for ball spin

	// Continuous push lets the player dribble/shove the ball while staying in contact,
	// instead of only getting a single impulse on first touch.
	[Export] public float MinPushSpeed = 5f;         // ignore pushes slower than this
	[Export] public float PushResponsiveness = 0.8f; // how quickly the ball matches the push speed (0..1)

	// Body / kit colour now comes from the chosen club (see ClubDatabase); only the avatar
	// dimensions and outline remain tunable here.
	[Export] public Color OutlineColor = new Color(0f, 0f, 0f, 1f);
	[Export] public int PlaceholderWidth = 192;
	[Export] public int PlaceholderHeight = 256;
	[Export] public int OutlineThickness = 5;

	public enum ControlMode { Human, Ai }

	[Export] public ControlMode Control = ControlMode.Human;

	public bool InputEnabled { get; set; } = true;

	private bool _wasOnFloor;
	private RigidBody2D _ballInContact;

	// Leg-kick runtime state.
	private float _kickActiveLeft;     // > 0 while a swing counts as a real kick
	private float _kickCooldownLeft;   // > 0 while a new kick is blocked
	private int _facing = 1;           // +1 facing right, -1 facing left (drives the swing direction)
	private Sprite2D _legSprite;
	private Tween _legTween;

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

		ApplyClubModifiers();
		SetupPlayerSprite();
		CreateLeg();
	}

	// Apply the chosen club's small, balanced stat modifiers to this player's movement and kick.
	// Defaults (no GameState / no clubs) leave the tuned exports untouched.
	private void ApplyClubModifiers()
	{
		if (_gameState == null)
			return;

		int clubIndex = _playerIndex == 1 ? _gameState.ClubTwoIndex : _gameState.ClubOneIndex;
		ClubDatabase.Club club = ClubDatabase.GetClub(clubIndex);

		Speed *= club.SpeedMultiplier;
		JumpVelocity *= club.JumpMultiplier;     // JumpVelocity is negative; scaling magnitude still works
		BaseKickSpeed *= club.KickMultiplier;
		KickSpeedFactor *= club.KickMultiplier;
	}

	public override void _PhysicsProcess(double delta)
	{
		ReadControls(delta, out float moveAxis, out bool jumpPressed, out bool kickPressed);

		_kickActiveLeft = Mathf.Max(0f, _kickActiveLeft - (float)delta);
		_kickCooldownLeft = Mathf.Max(0f, _kickCooldownLeft - (float)delta);

		// Face the way the player is moving so the kick swings forward.
		if (InputEnabled && Mathf.Abs(moveAxis) > 0.1f)
			_facing = moveAxis > 0f ? 1 : -1;

		bool kickedThisFrame = false;
		if (InputEnabled && kickPressed && _kickCooldownLeft <= 0f)
		{
			StartKick();
			kickedThisFrame = true;
		}

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
		// contact so the player can dribble/shove rather than the ball sticking. Pressing Kick
		// while already overlapping the ball also fires a (boosted) impulse this frame.
		bool freshContact = touchedBall != null && touchedBall != _ballInContact;
		if (freshContact || (kickedThisFrame && touchedBall != null))
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
	private void ReadControls(double delta, out float moveAxis, out bool jumpPressed, out bool kickPressed)
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

			(moveAxis, jumpPressed, kickPressed) = _aiBrain.Think(in ctx);
			return;
		}

		moveAxis = Input.GetAxis(LeftAction, RightAction);
		jumpPressed = Input.IsActionJustPressed(JumpAction);
		kickPressed = InputMap.HasAction(KickAction) && Input.IsActionJustPressed(KickAction);
	}

	private void ApplyKickImpulse(RigidBody2D ball)
	{
		Vector2 contactNormal = (ball.GlobalPosition - GlobalPosition).Normalized();
		float playerSpeed = Velocity.Length();
		bool kicking = _kickActiveLeft > 0f;

		// Blend contact normal with player movement direction so fast runs send the ball forward.
		// Mid-swing, bias the launch toward the facing direction so a timed kick "shoots" forward.
		Vector2 dir = playerSpeed > 10f
			? (contactNormal + Velocity.Normalized()).Normalized()
			: contactNormal;
		if (kicking)
			dir = (contactNormal + new Vector2(_facing, -0.35f)).Normalized();

		float ballAlong = ball.LinearVelocity.Dot(dir);
		float playerInto = Mathf.Max(0f, Velocity.Dot(dir));  // player speed heading into the ball
		float incoming = Mathf.Max(0f, -ballAlong);           // ball speed heading into the player

		// Bouncier the faster the player moves: launch = baseline + reflected incoming
		// speed (bounciness) + a share of the player's speed. A live leg-kick multiplies the
		// result so a well-timed swing clearly out-powers a passive bump.
		float targetOut = BaseKickSpeed + incoming * Bounciness + playerInto * KickSpeedFactor;
		if (kicking)
			targetOut *= KickBoost;
		targetOut = Mathf.Min(targetOut, MaxBallSpeed);

		float currentOut = Mathf.Max(0f, ballAlong);
		float deltaV = targetOut - currentOut;
		if (deltaV > 0f)
		{
			ball.ApplyCentralImpulse(dir * deltaV * ball.Mass);
			// Impart spin proportional to the horizontal kick component so headers and
			// drives look dynamic. Positive X kick → clockwise (positive angular vel).
			ball.ApplyTorqueImpulse(dir.X * targetOut * ball.Mass * KickSpinFactor);
		}
	}

	// Start a leg swing: open the "real kick" window, set the cooldown and animate the foot.
	private void StartKick()
	{
		_kickActiveLeft = KickActiveSeconds;
		_kickCooldownLeft = KickCooldownSeconds;
		AnimateLegSwing();
	}

	private void AnimateLegSwing()
	{
		if (_legSprite == null)
			return;

		if (_legTween != null && _legTween.IsValid())
			_legTween.Kill();

		// The leg hangs down at rest; a swing rotates it forward (toward facing) and back.
		_legSprite.Scale = new Vector2(_facing, 1f);
		float swing = Mathf.DegToRad(KickSwingDegrees) * _facing;

		_legSprite.Rotation = 0f;
		_legTween = CreateTween();
		_legTween.TweenProperty(_legSprite, "rotation", swing, KickSwingSeconds)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_legTween.TweenProperty(_legSprite, "rotation", 0f, KickSwingSeconds)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
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

	private void SetupPlayerSprite()
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

		// Use the club + player chosen on the selection screen; fall back to the first club/player
		// when there is no GameState (e.g. running the match scene on its own).
		int clubIndex = 0;
		int variantIndex = 0;
		if (_gameState != null)
		{
			clubIndex = playerIndex == 2 ? _gameState.ClubTwoIndex : _gameState.ClubOneIndex;
			variantIndex = playerIndex == 2 ? _gameState.PlayerTwoVariant : _gameState.PlayerOneVariant;
		}

		ClubDatabase.Club club = ClubDatabase.GetClub(clubIndex);
		ClubDatabase.PlayerVariant variant = ClubDatabase.GetPlayer(clubIndex, variantIndex);

		sprite.Texture = PlayerSpriteFactory.BuildPlayerTexture(
			Math.Max(PlaceholderWidth, 48),
			Math.Max(PlaceholderHeight, 64),
			playerIndex,
			club,
			variant,
			OutlineColor,
			Math.Max(OutlineThickness, 2)
		);
	}

	// Build the swinging leg as a child Sprite2D pivoted at the hip so a swing rotates the foot
	// forward. Sits low on the body, behind the main sprite.
	private void CreateLeg()
	{
		GetNodeOrNull<Sprite2D>("Leg")?.QueueFree();

		int legLength = Math.Max(40, PlaceholderHeight / 4);
		int legWidth = Math.Max(14, PlaceholderWidth / 12);

		_legSprite = new Sprite2D
		{
			Name = "Leg",
			Texture = BuildLegTexture(legWidth, legLength),
			Centered = false,
			// Pivot at the top of the bar (the hip): origin sits at the sprite's top-centre.
			Offset = new Vector2(-legWidth / 2f, 0f),
			Position = new Vector2(0f, PlaceholderHeight * 0.18f),
			ZIndex = 11 // in front of the body sprite (ZIndex 10) so the kick is visible
		};
		AddChild(_legSprite);
	}

	private Texture2D BuildLegTexture(int width, int length)
	{
		Color legColor = new Color(0.96f, 0.85f, 0.70f); // generic skin-tone limb
		Color bootColor = new Color(0.12f, 0.12f, 0.14f);
		Color outline = OutlineColor;

		Image image = Image.CreateEmpty(width, length, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		for (int y = 0; y < length; y++)
		{
			for (int x = 0; x < width; x++)
			{
				bool edge = x == 0 || x == width - 1 || y == 0 || y == length - 1;
				Color c = edge ? outline : (y > length * 0.74f ? bootColor : legColor);
				image.SetPixel(x, y, c);
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private int DetectPlayerIndex()
	{
		string nodeName = Name.ToString();

		if (LeftAction.Contains("Player2") || nodeName.Contains("2"))
			return 2;

		return 1;
	}

}
