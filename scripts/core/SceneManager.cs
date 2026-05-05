using Godot;

public partial class SceneManager : Node
{
	public const string MainMenuScene = "res://Scenes/MainMenu.tscn";
	public const string InstructionsScene = "res://Scenes/Instructions.tscn";
	public const string MatchScene = "res://Scenes/Match.tscn";
	public const string EndScreenScene = "res://Scenes/EndScreen.tscn";

	private bool _isChangingScene = false;

	public void GoTo(string scenePath)
	{
		if (_isChangingScene)
			return;

		_isChangingScene = true;

		Error error = GetTree().ChangeSceneToFile(scenePath);

		if (error != Error.Ok)
		{
			GD.PushError($"Failed to change scene to: {scenePath}. Error: {error}");
			_isChangingScene = false;
		}
	}

	public void GoToMainMenu()
	{
		GoTo(MainMenuScene);
	}

	public void GoToInstructions()
	{
		GoTo(InstructionsScene);
	}

	public void GoToMatch()
	{
		GoTo(MatchScene);
	}

	public void GoToEndScreen()
	{
		GoTo(EndScreenScene);
	}
}
