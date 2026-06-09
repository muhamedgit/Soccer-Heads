using Godot;

public partial class SceneManager : Node
{
	public const string MainMenuScene = "res://scenes/MainMenu.tscn";
	public const string InstructionsScene = "res://scenes/Instructions.tscn";
	public const string SettingsScene = "res://scenes/Settings.tscn";
	public const string ClubSelectScene = "res://scenes/ClubSelect.tscn";
	public const string MatchScene = "res://scenes/Game.tscn";
	public const string EndScreenScene = "res://scenes/EndScreen.tscn";

	private bool _isChangingScene = false;

	public async void GoTo(string scenePath)
	{
		if (_isChangingScene)
			return;

		_isChangingScene = true;

		UpdateMusicForScene(scenePath);

		Error error = GetTree().ChangeSceneToFile(scenePath);

		if (error != Error.Ok)
		{
			GD.PushError($"Failed to change scene to: {scenePath}. Error: {error}");
			_isChangingScene = false;
			return;
		}

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		_isChangingScene = false;
	}

	public void GoToMainMenu()
	{
		GoTo(MainMenuScene);
	}

	public void GoToInstructions()
	{
		GoTo(InstructionsScene);
	}

	public void GoToSettings()
	{
		GoTo(SettingsScene);
	}

	public void GoToClubSelect()
	{
		GoTo(ClubSelectScene);
	}

	public void GoToMatch()
	{
		GoTo(MatchScene);
	}

	public void GoToEndScreen()
	{
		GoTo(EndScreenScene);
	}

	private void UpdateMusicForScene(string scenePath)
	{
		var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");

		if (audioManager == null)
			return;

		if (scenePath == MatchScene)
			audioManager.PlayGameplayMusic();
		else
			audioManager.StopMusic();
	}
}
