using Godot;

public partial class MainMenu : Control
{
	[Export] private Button _playButton;
	[Export] private Button _instructionsButton;
	[Export] private Button _quitButton;

	public override void _Ready()
	{
		_playButton.Pressed += OnPlayPressed;
		_instructionsButton.Pressed += OnInstructionsPressed;

		if (_quitButton != null)
			_quitButton.Pressed += OnQuitPressed;
	}

	private void OnPlayPressed()
	{
		GetNode<GameState>("/root/GameState").ResetMatch();
		GetNode<SceneManager>("/root/SceneManager").GoToMatch();
	}

	private void OnInstructionsPressed()
	{
		GetNode<SceneManager>("/root/SceneManager").GoToInstructions();
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
