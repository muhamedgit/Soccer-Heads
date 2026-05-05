using Godot;

public partial class MainMenu : Control
{
	private Button _playButton;
	private Button _instructionsButton;
	private Button _quitButton;

	public override void _Ready()
	{
		GD.Print("MainMenu script loaded.");

		_playButton = GetNode<Button>("CenterContainer/VBoxContainer/PlayButton");
		_instructionsButton = GetNode<Button>("CenterContainer/VBoxContainer/InstructionsButton");
		_quitButton = GetNode<Button>("CenterContainer/VBoxContainer/QuitButton");

		_playButton.Pressed += OnPlayPressed;
		_instructionsButton.Pressed += OnInstructionsPressed;
		_quitButton.Pressed += OnQuitPressed;
	}

	private void OnPlayPressed()
	{
		GD.Print("Play button pressed.");

		GetNode<GameState>("/root/GameState").ResetMatch();
		GetNode<SceneManager>("/root/SceneManager").GoToMatch();
	}

	private void OnInstructionsPressed()
	{
		GD.Print("Instructions button pressed.");

		GetNode<SceneManager>("/root/SceneManager").GoToInstructions();
	}

	private void OnQuitPressed()
	{
		GD.Print("Quit button pressed.");

		GetTree().Quit();
	}
}