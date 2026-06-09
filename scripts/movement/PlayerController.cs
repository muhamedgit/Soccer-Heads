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

	// Foot-kick runtime state.
	private float _kickActiveLeft;   // > 0 while the kick boost window is open
	private float _kickCooldownLeft; // > 0 while a new kick is blocked
	private int   _kickDir = 1;      // +1 = P1 kicks right, -1 = P2 kicks left (set in CreateFoot)
	private Sprite2D    _footSprite;
	private Area2D      _footArea;
	private RigidBody2D _footKickAppliedTo; // prevents double-kick within one kick window
	private float _footAngle;               // current arc angle in radians (π/2 = 6-o'clock rest)

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
		CreateFoot();
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

		bool kickedThisFrame = false;
		if (InputEnabled && kickPressed && _kickCooldownLeft <= 0f)
		{
			_kickActiveLeft      = KickActiveSeconds;
			_kickCooldownLeft    = KickCooldownSeconds;
			_footKickAppliedTo   = null; // fresh kick window
			kickedThisFrame      = true;
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

		// Foot arc: snap to kick side while button held/AI active, sweep back to rest on release
		bool kickIsHeld = Control == ControlMode.Ai
			? _kickActiveLeft > 0f
			: InputEnabled && InputMap.HasAction(KickAction) && Input.IsActionPressed(KickAction);
		float restAngle = Mathf.Pi * 0.5f;          // 6 o'clock (bottom of head)
		float kickAngle = _kickDir > 0 ? 0f : Mathf.Pi; // 3 o'clock (P1) or 9 o'clock (P2)
		_footAngle = kickIsHeld
			? kickAngle
			: Mathf.MoveToward(_footAngle, restAngle, Mathf.DegToRad(380f) * (float)delta);
		UpdateFootTransform();

		// Poll foot-ball overlap every frame — reliable even when the foot teleports.
		if (_footArea != null)
		{
			if (_kickActiveLeft <= 0f)
				_footKickAppliedTo = null;

			foreach (var overlapBody in _footArea.GetOverlappingBodies())
			{
				if (overlapBody is not RigidBody2D footBall || !footBall.IsInGroup("ball"))
					continue;

				// Hard physical barrier: cancel all ball velocity into the foot + spring push.
				// This runs every frame regardless of kick state so the foot never clips the ball.
				Vector2 footWorld = ToGlobal(_footSprite.Position);
				Vector2 awayDir   = footBall.GlobalPosition - footWorld;
				awayDir = awayDir.LengthSquared() > 0.01f
					? awayDir.Normalized()
					: (_kickDir > 0 ? Vector2.Right : Vector2.Left);

				float intoFoot = Mathf.Max(0f, -footBall.LinearVelocity.Dot(awayDir));
				footBall.ApplyCentralImpulse(awayDir * (intoFoot + 1200f * (float)delta) * footBall.Mass);

				// Kick boost: once per kick window on top of the barrier impulse.
				if (_kickActiveLeft > 0f && footBall != _footKickAppliedTo)
				{
					ApplyKickImpulse(footBall);
					_footKickAppliedTo = footBall;
				}

				break;
			}
		}

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
			dir = (contactNormal + new Vector2(_kickDir, -0.35f)).Normalized();

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

	// Foot sprite that orbits the head center on a circular arc.
	// P1: rest = 6 o'clock (90°), kick = 3 o'clock (0°, right side).
	// P2: rest = 6 o'clock (90°), kick = 9 o'clock (180°, left side).
	private void CreateFoot()
	{
		GetNodeOrNull<Sprite2D>("FootSprite")?.QueueFree();
		GetNodeOrNull<Area2D>("FootArea")?.QueueFree();

		int footW = Math.Max(48, PlaceholderWidth  / 3);
		int footH = Math.Max(20, PlaceholderHeight / 9); // taller = thicker shoe

		_footSprite = new Sprite2D
		{
			Name     = "FootSprite",
			Texture  = PlayerSpriteFactory.BuildFootTexture(footW, footH, OutlineColor, OutlineThickness),
			Centered = true,
			ZIndex   = 11
		};
		AddChild(_footSprite);

		// Collision area follows the foot sprite so the ball can be kicked by the foot.
		_footArea = new Area2D { Name = "FootArea" };
		_footArea.CollisionLayer = 0;
		_footArea.CollisionMask  = 1u << 1; // layer 2 = ball
		// Detection shape is slightly larger than the visual foot so the barrier
		// impulse fires before the ball visually overlaps the sprite.
		_footArea.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(footW + 10, footH + 10) }
		});
		AddChild(_footArea);

		_kickDir   = (_playerIndex == 0) ? 1 : -1;
		_footAngle = Mathf.Pi * 0.5f; // start at rest
		UpdateFootTransform();
	}


	// Reposition the foot sprite (and its collision area) on the head-boundary arc.
	// Rotation tracks the sweep so the toe is horizontal at rest and points up at full kick.
	private void UpdateFootTransform()
	{
		if (_footSprite == null) return;

		// Head center in node-local space.
		// Matches PlayerSpriteFactory: hy=8, hh=PlaceholderHeight*0.58 → cy_image=8+hh/2
		// Node Y = cy_image - PlaceholderHeight/2 = 8 + PlaceholderHeight*0.29 - PlaceholderHeight*0.5
		float headCenterLocalY = 8f - PlaceholderHeight * 0.21f;
		float armRadius        = PlaceholderHeight * 0.46f; // outside head boundary
		// Shift orbit center toward the kick side so the foot hangs in front of the player.
		float orbitOffsetX     = _kickDir * PlaceholderWidth * 0.10f;

		var pos = new Vector2(
			orbitOffsetX + armRadius * Mathf.Cos(_footAngle),
			headCenterLocalY + armRadius * Mathf.Sin(_footAngle)
		);

		// Toe points right at texture 0°.
		// P1: flat (0°) at rest (π/2), toe down (+π/2) at kick (0). Formula: π/2 - angle.
		// P2: flat (π) at rest (π/2), toe down (+π/2) at kick (π). Formula: 3π/2 - angle.
		float rot = _kickDir > 0
			? Mathf.Pi * 0.5f - _footAngle
			: Mathf.Pi * 1.5f - _footAngle;

		_footSprite.Position = pos;
		_footSprite.Rotation = rot;

		if (_footArea != null)
		{
			_footArea.Position = pos;
			_footArea.Rotation = rot;
		}
	}

	private int DetectPlayerIndex()
	{
		string nodeName = Name.ToString();

		if (LeftAction.Contains("Player2") || nodeName.Contains("2"))
			return 2;

		return 1;
	}

}
