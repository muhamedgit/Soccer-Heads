using Godot;

public partial class SettingsMenu : Control
{
	private HSlider _volumeSlider;
	private Label _volumeValue;
	private CheckButton _fullscreenToggle;
	private Button _backButton;
	private AudioManager _audioManager;

	public override void _Ready()
	{
		GD.Print("SettingsMenu script loaded.");

		_audioManager = GetNode<AudioManager>("/root/AudioManager");
		_volumeSlider = GetNode<HSlider>("CenterContainer/VBoxContainer/VolumeRow/VolumeSlider");
		_volumeValue = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/VolumeRow/VolumeValue");
		_fullscreenToggle = GetNodeOrNull<CheckButton>("CenterContainer/VBoxContainer/FullscreenRow/FullscreenToggle");
		_backButton = GetNode<Button>("CenterContainer/VBoxContainer/BackButton");

		_volumeSlider.MinValue = 0;
		_volumeSlider.MaxValue = 1;
		_volumeSlider.Step = 0.05;
		_volumeSlider.Value = _audioManager.GetMasterVolume();
		UpdateValueLabel(_volumeSlider.Value);

		if (_fullscreenToggle != null)
		{
			_fullscreenToggle.ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen
				|| DisplayServer.WindowGetMode() == DisplayServer.WindowMode.ExclusiveFullscreen;
			_fullscreenToggle.Toggled += OnFullscreenToggled;
		}

		_volumeSlider.ValueChanged += OnVolumeChanged;
		_backButton.Pressed += OnBackPressed;
	}

	private void OnVolumeChanged(double value)
	{
		_audioManager.SetMasterVolume((float)value);
		UpdateValueLabel(value);
	}

	private void UpdateValueLabel(double value)
	{
		if (_volumeValue != null)
			_volumeValue.Text = $"{Mathf.RoundToInt(value * 100)}%";
	}

	private void OnFullscreenToggled(bool pressed)
	{
		DisplayServer.WindowSetMode(pressed
			? DisplayServer.WindowMode.Fullscreen
			: DisplayServer.WindowMode.Windowed);
	}

	private void OnBackPressed()
	{
		GD.Print("Settings back pressed.");
		GetNode<SceneManager>("/root/SceneManager").GoToMainMenu();
	}
}
