using Godot;

public partial class ScoreHud : CanvasLayer
{
	private Label _playerOneScoreLabel;
	private Label _playerTwoScoreLabel;
	private GameState _gameState;

	public override void _Ready()
	{
		_playerOneScoreLabel = GetNode<Label>("HudRoot/ScorePanel/ScoreRow/PlayerOneScore");
		_playerTwoScoreLabel = GetNode<Label>("HudRoot/ScorePanel/ScoreRow/PlayerTwoScore");
		_gameState = GetNode<GameState>("/root/GameState");

		_gameState.ScoreChanged += UpdateScore;
		UpdateScore(_gameState.PlayerOneScore, _gameState.PlayerTwoScore);
	}

	public override void _ExitTree()
	{
		if (_gameState != null)
		{
			_gameState.ScoreChanged -= UpdateScore;
		}
	}

	private void UpdateScore(int playerOneScore, int playerTwoScore)
	{
		_playerOneScoreLabel.Text = playerOneScore.ToString();
		_playerTwoScoreLabel.Text = playerTwoScore.ToString();
	}
}
