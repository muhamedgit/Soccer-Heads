using Godot;

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
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_matchEnded)
			return;

		_gameState.SetTimeLeft(_gameState.MatchTimeLeft - (float)delta);

		if (_gameState.IsMatchOver())
		{
			EndMatch();
		}
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
}
