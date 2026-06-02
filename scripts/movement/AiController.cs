using Godot;

// Lightweight "brain" for an AI-controlled player. It is a plain class (no node
// lifecycle) owned by a PlayerController: each physics frame the controller hands it
// the world state and gets back a horizontal move axis and a jump request, exactly the
// shape a human's input produces. Difficulty only changes the tuning constants below.
public class AiController
{
	private float _reactionTime;   // perception lag: how often the AI refreshes its view of the ball
	private float _deadZone;       // px tolerance before it bothers to move (sloppiness)
	private float _aimNoise;       // px random offset added to its target (imprecision)
	private float _jumpChance;     // 0..1 chance to take a header jump when one is available
	private float _speedFactor;    // 0..1 scales movement speed (lower = easier to beat)
	private float _jumpReach;      // how far above the AI the ball may be and still trigger a jump
	private float _jumpRange;      // horizontal distance to the ball that allows a jump

	private Vector2 _perceivedBallPos;
	private Vector2 _perceivedBallVel;
	private float _perceptionTimer;
	private float _aimOffset;
	private float _jumpCooldown;

	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	public void Configure(GameState.AiDifficulty difficulty)
	{
		_rng.Randomize();

		switch (difficulty)
		{
			case GameState.AiDifficulty.Beginner:
				_reactionTime = 0.42f;
				_deadZone = 95f;
				_aimNoise = 150f;
				_jumpChance = 0.18f;
				_speedFactor = 0.62f;
				_jumpReach = 230f;
				_jumpRange = 150f;
				break;

			case GameState.AiDifficulty.Intermediate:
				_reactionTime = 0.08f;
				_deadZone = 16f;
				_aimNoise = 22f;
				_jumpChance = 0.80f;
				_speedFactor = 1.0f;
				_jumpReach = 360f;
				_jumpRange = 240f;
				break;

			default: // Normal
				_reactionTime = 0.20f;
				_deadZone = 42f;
				_aimNoise = 70f;
				_jumpChance = 0.45f;
				_speedFactor = 0.85f;
				_jumpReach = 300f;
				_jumpRange = 200f;
				break;
		}
	}

	// attackDir is the X direction toward the goal the AI is trying to score in
	// (-1 = left goal, +1 = right goal).
	public (float axis, bool jump) Think(
		Vector2 selfPos,
		Vector2 ballPos,
		Vector2 ballVel,
		int attackDir,
		bool onFloor,
		double delta)
	{
		float dt = (float)delta;
		_perceptionTimer -= dt;
		_jumpCooldown -= dt;

		// Perception lag: easier AIs refresh their picture of the ball less often, so they
		// react late. The aim offset is re-rolled on the same tick so it doesn't jitter.
		if (_perceptionTimer <= 0f)
		{
			_perceivedBallPos = ballPos;
			_perceivedBallVel = ballVel;
			_aimOffset = _rng.RandfRange(-_aimNoise, _aimNoise);
			_perceptionTimer = _reactionTime;
		}

		// Anticipate where the ball is heading a little.
		Vector2 target = _perceivedBallPos + _perceivedBallVel * 0.12f;

		// Stand on the side of the ball away from the goal we attack, so that running
		// into it pushes the ball toward that goal (this also clears it when defending).
		const float standOffset = 70f;
		float desiredX = target.X - attackDir * standOffset + _aimOffset;

		float dx = desiredX - selfPos.X;
		float axis = 0f;
		if (Mathf.Abs(dx) > _deadZone)
			axis = Mathf.Sign(dx) * _speedFactor;

		// Jump for a header when the ball is above us (smaller Y) and horizontally close.
		bool jump = false;
		float ballAbove = selfPos.Y - ballPos.Y;
		float horizontal = Mathf.Abs(ballPos.X - selfPos.X);

		if (onFloor && _jumpCooldown <= 0f &&
			ballAbove > 60f && ballAbove < _jumpReach && horizontal < _jumpRange &&
			_rng.Randf() < _jumpChance)
		{
			jump = true;
			_jumpCooldown = 0.6f;
		}

		return (axis, jump);
	}
}
