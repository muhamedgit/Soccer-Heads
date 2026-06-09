using Godot;

public partial class EndScreen : Control
{
	private Label _winnerLabel;
	private Label _scoreLabel;
	private Button _restartButton;
	private Button _mainMenuButton;
	private GameState _gameState;
	private SceneManager _sceneManager;
	private bool _isNavigating;

	public override void _Ready()
	{
		GD.Print("EndScreen script loaded.");

		UiTheme.Apply(this);

		_winnerLabel = GetNode<Label>("MarginContainer/VBoxContainer/Panel/Content/WinnerLabel");
		_scoreLabel = GetNode<Label>("MarginContainer/VBoxContainer/Panel/Content/ScoreLabel");
		_restartButton = GetNode<Button>("MarginContainer/VBoxContainer/ButtonRow/RestartButton");
		_mainMenuButton = GetNode<Button>("MarginContainer/VBoxContainer/ButtonRow/MainMenuButton");
		_gameState = GetNode<GameState>("/root/GameState");
		_sceneManager = GetNode<SceneManager>("/root/SceneManager");

		_winnerLabel.Text = _gameState.GetWinnerText();
		_scoreLabel.Text = _gameState.GetFinalScoreText();

		_restartButton.Pressed += OnRestartPressed;
		_mainMenuButton.Pressed += OnMainMenuPressed;
	}

	private void OnRestartPressed()
	{
		if (_isNavigating)
			return;

		_isNavigating = true;
		_restartButton.Disabled = true;
		_mainMenuButton.Disabled = true;

		_gameState.ResetMatch();
		_sceneManager.GoToMatch();
	}

	private void OnMainMenuPressed()
	{
		if (_isNavigating)
			return;

		_isNavigating = true;
		_restartButton.Disabled = true;
		_mainMenuButton.Disabled = true;

		_sceneManager.GoToMainMenu();
	}
}
