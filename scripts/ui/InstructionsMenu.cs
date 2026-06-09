using Godot;

public partial class InstructionsMenu : Control
{
	private Button _backButton;

	public override void _Ready()
	{
		GD.Print("InstructionsMenu script loaded.");

		UiTheme.Apply(this);

		_backButton = GetNode<Button>("MarginContainer/VBoxContainer/BackButton");
		_backButton.Pressed += OnBackPressed;
	}

	private void OnBackPressed()
	{
		GD.Print("Back button pressed.");

		GetNode<SceneManager>("/root/SceneManager").GoToMainMenu();
	}
}
