using Godot;

// Lightweight "brain" for an AI-controlled player. Plain class (no node lifecycle) owned by a
// PlayerController: each physics frame the controller hands it a world snapshot and gets back a
// horizontal move axis and a jump request, exactly the shape a human's input produces.
//
// It runs a small ATTACK / DEFEND / RECOVER state machine, aims the (automatic) contact-kick at
// the opponent goal mouth, times headers by predicting the ball's arc, is aware of perks (chase
// advantages while in possession, avoid disadvantages), and returns toward a guard spot when idle.
// Difficulty only changes the tuning constants in Configure.
public class AiController
{
	public enum AiState { Attack, Defend, Recover }

	// World snapshot handed in by the owning PlayerController each frame (passed by `in`, no GC).
	public struct AiContext
	{
		public Vector2 SelfPos;
		public Vector2 BallPos;
		public Vector2 BallVel;
		public Vector2 TargetGoal;   // goal the AI attacks
		public Vector2 OwnGoal;      // goal the AI defends
		public Vector2 PerkPos;
		public int AttackDir;        // +1 = attacks right goal, -1 = attacks left goal
		public bool OnFloor;
		public bool IsLastToucher;   // AI was the last to touch the ball (perk would apply to it)
		public bool HasPerk;
		public bool PerkIsAdvantage;
		public double Delta;
	}

	// Difficulty tuning.
	private float _reactionTime;     // perception lag: how often it refreshes its view + decisions
	private float _deadZone;         // px tolerance before it bothers to move
	private float _aimNoise;         // px random offset on its stand point (degrades aim)
	private float _speedFactor;      // 0..1 movement-speed scale
	private float _jumpReach;        // how far above the head the ball may be and still header it
	private float _jumpRange;        // horizontal distance at the predicted contact to allow a jump
	private float _jumpReliability;  // 0..1 chance it actually takes an available header (Beginner whiffs)
	private float _jumpTimingError;  // s jitter on the jump lead window
	private float _standOffset;      // px it stands off the ball to aim the contact
	private float _perkStrength;     // 0..1 how strongly it chases/avoids perks

	// Tuning constants shared across difficulties.
	private const float BallGravity = 1960f;   // ball gravity_scale 2 * ~980
	private const float HeaderOffset = 150f;    // header contact height above the body origin
	private const float LeadMin = 0.10f;        // jump only if the ball arrives within this window
	private const float LeadMax = 0.50f;
	private const float JumpHorizon = 0.6f;     // ignore predictions farther out than this
	private const float Hysteresis = 120f;      // midfield dead band so the state doesn't dither
	private const float HomeInset = 250f;       // guard spot, this far in front of the own goal
	private const float AvoidRadius = 350f;     // disadvantage perk avoidance kicks in within this
	private const float AvoidPush = 220f;       // px nudge away from a disadvantage perk

	private AiState _state = AiState.Defend;
	private Vector2 _perceivedBallPos;
	private Vector2 _perceivedBallVel;
	private float _perceptionTimer;
	private float _aimOffsetX;
	private float _jumpCooldown;

	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	public void Configure(GameState.AiDifficulty difficulty)
	{
		_rng.Randomize();
		_state = AiState.Defend;

		switch (difficulty)
		{
			case GameState.AiDifficulty.Beginner:
				_reactionTime = 0.40f;
				_deadZone = 90f;
				_aimNoise = 150f;
				_speedFactor = 0.62f;
				_jumpReach = 220f;
				_jumpRange = 150f;
				_jumpReliability = 0.45f;
				_jumpTimingError = 0.18f;
				_standOffset = 90f;
				_perkStrength = 0f;
				break;

			case GameState.AiDifficulty.Intermediate:
				_reactionTime = 0.06f;
				_deadZone = 14f;
				_aimNoise = 14f;
				_speedFactor = 1.0f;
				_jumpReach = 360f;
				_jumpRange = 250f;
				_jumpReliability = 1.0f;
				_jumpTimingError = 0.02f;
				_standOffset = 60f;
				_perkStrength = 1.0f;
				break;

			default: // Normal
				_reactionTime = 0.18f;
				_deadZone = 40f;
				_aimNoise = 60f;
				_speedFactor = 0.86f;
				_jumpReach = 300f;
				_jumpRange = 200f;
				_jumpReliability = 0.80f;
				_jumpTimingError = 0.08f;
				_standOffset = 72f;
				_perkStrength = 0.5f;
				break;
		}
	}

	public (float axis, bool jump) Think(in AiContext ctx)
	{
		float dt = (float)ctx.Delta;
		_perceptionTimer -= dt;
		_jumpCooldown -= dt;

		// Reaction lag: easier AIs refresh their picture of the ball, their aim noise and their
		// state decision less often, so they react late.
		if (_perceptionTimer <= 0f)
		{
			_perceivedBallPos = ctx.BallPos;
			_perceivedBallVel = ctx.BallVel;
			_aimOffsetX = _rng.RandfRange(-_aimNoise, _aimNoise);
			_perceptionTimer = _reactionTime;
			_state = DecideState(in ctx);
		}

		float center = (ctx.TargetGoal.X + ctx.OwnGoal.X) * 0.5f;
		float defendSign = Mathf.Sign(ctx.OwnGoal.X - center);
		float ballX = _perceivedBallPos.X;

		// Aim at the goal mouth by default; chase an advantage perk only while attacking AND in
		// possession (otherwise the collected perk would go to the opponent).
		Vector2 aimTarget = ctx.TargetGoal;
		if (ctx.HasPerk && ctx.PerkIsAdvantage && _state == AiState.Attack && ctx.IsLastToucher && _perkStrength > 0f)
			aimTarget = ctx.TargetGoal.Lerp(ctx.PerkPos, _perkStrength);

		float desiredX;
		switch (_state)
		{
			case AiState.Defend:
				// Stand on the own-goal side of the ball so moving into it clears it upfield.
				desiredX = ballX + defendSign * _standOffset;
				break;

			case AiState.Recover:
				// Return to a guard spot just in front of the own goal.
				desiredX = ctx.OwnGoal.X - defendSign * HomeInset;
				break;

			default: // Attack
				// Stand on the far side of the ball from the aim point so contact drives it there.
				desiredX = ballX + Mathf.Sign(ballX - aimTarget.X) * _standOffset;
				break;
		}

		// Don't drive the ball toward a disadvantage perk: nudge the stand point away from it.
		if (ctx.HasPerk && !ctx.PerkIsAdvantage && _perkStrength > 0f && _state != AiState.Recover)
		{
			float toPerk = ctx.PerkPos.X - ballX;
			if (Mathf.Abs(toPerk) < AvoidRadius)
				desiredX += Mathf.Sign(toPerk) * AvoidPush * _perkStrength;
		}

		desiredX = Mathf.Clamp(desiredX, 60f, 2240f);

		float dx = desiredX + _aimOffsetX - ctx.SelfPos.X;
		float axis = Mathf.Abs(dx) > _deadZone ? Mathf.Sign(dx) * _speedFactor : 0f;
		axis = Mathf.Clamp(axis, -_speedFactor, _speedFactor);

		bool jump = DecideJump(in ctx);

		return (axis, jump);
	}

	private AiState DecideState(in AiContext ctx)
	{
		float center = (ctx.TargetGoal.X + ctx.OwnGoal.X) * 0.5f;
		float defendSign = Mathf.Sign(ctx.OwnGoal.X - center);

		float ballSide = (_perceivedBallPos.X - center) * defendSign; // > 0 = on the AI's half
		float ballSpeed = _perceivedBallVel.Length();
		bool headingOwn = _perceivedBallVel.X * defendSign > 80f;

		// Defend if the ball is on (or just past) the AI's half, or speeding toward the own goal.
		if (ballSide > -Hysteresis || (headingOwn && ballSpeed > 350f))
			return AiState.Defend;

		// Ball parked deep on the opponent half: drop back to the guard spot.
		if (ballSide < -500f && ballSpeed < 120f)
			return AiState.Recover;

		return AiState.Attack;
	}

	// Predict when the ball will fall to header height near the AI and jump to meet it, instead of
	// jumping probabilistically. Difficulty scales timing accuracy and (Beginner) whiff rate.
	private bool DecideJump(in AiContext ctx)
	{
		if (!ctx.OnFloor || _jumpCooldown > 0f)
			return false;

		float ballAbove = ctx.SelfPos.Y - _perceivedBallPos.Y;
		if (ballAbove < 60f || ballAbove > _jumpReach)
			return false;

		float a = 0.5f * BallGravity;
		if (Mathf.Abs(a) < 1e-3f)
			return false;

		float yHead = ctx.SelfPos.Y - HeaderOffset;
		float b = _perceivedBallVel.Y;
		float c = _perceivedBallPos.Y - yHead;
		float disc = b * b - 4f * a * c;
		if (disc < 0f)
			return false;

		float sq = Mathf.Sqrt(disc);
		float tHit = SmallestPositive((-b - sq) / (2f * a), (-b + sq) / (2f * a));
		if (tHit <= 0f || float.IsNaN(tHit) || float.IsInfinity(tHit) || tHit > JumpHorizon)
			return false;

		float ballXAt = _perceivedBallPos.X + _perceivedBallVel.X * tHit;
		if (Mathf.Abs(ballXAt - ctx.SelfPos.X) > _jumpRange)
			return false;

		float jitter = _rng.RandfRange(-_jumpTimingError, _jumpTimingError);
		if (tHit < LeadMin + jitter || tHit > LeadMax + jitter)
			return false;

		// Beginner sometimes fails to commit to an available header.
		if (_jumpReliability < 1f && _rng.Randf() > _jumpReliability)
			return false;

		_jumpCooldown = 0.6f;
		return true;
	}

	private static float SmallestPositive(float a, float b)
	{
		float min = float.MaxValue;
		if (a > 0f && a < min) min = a;
		if (b > 0f && b < min) min = b;
		return min == float.MaxValue ? -1f : min;
	}
}
