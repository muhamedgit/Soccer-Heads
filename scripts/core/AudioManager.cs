using Godot;

public partial class AudioManager : Node
{
	private const string GameplayMusicPath = "res://Assets/Audio/gameplay_loop.wav";
	private const string GoalScoredSoundPath = "res://Assets/Audio/goal_scored.wav";

	private const string MusicBusName = "Music";
	private const string SfxBusName = "Master";

	[Export] public float MusicVolumeDb = -24f;
	[Export] public float GoalSfxVolumeDb = -5f;
	[Export] public float FadeOutSeconds = 0.35f;
	[Export] public double GoalSfxCooldownSeconds = 0.35;

	private AudioStreamPlayer _musicPlayer;
	private AudioStreamPlayer _goalSfxPlayer;

	private Tween _fadeTween;
	private double _goalSfxCooldown;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		SetupMusicPlayer();
		SetupGoalSfxPlayer();
	}

	public override void _Process(double delta)
	{
		if (_goalSfxCooldown > 0)
			_goalSfxCooldown -= delta;
	}

	private void SetupMusicPlayer()
	{
		_musicPlayer = new AudioStreamPlayer();
		_musicPlayer.Name = "MusicPlayer";
		_musicPlayer.Bus = MusicBusName;
		_musicPlayer.VolumeDb = MusicVolumeDb;
		AddChild(_musicPlayer);
	}

	private void SetupGoalSfxPlayer()
	{
		_goalSfxPlayer = new AudioStreamPlayer();
		_goalSfxPlayer.Name = "GoalSfxPlayer";
		_goalSfxPlayer.Bus = SfxBusName;
		_goalSfxPlayer.VolumeDb = GoalSfxVolumeDb;

		AudioStream goalStream = ResourceLoader.Load<AudioStream>(GoalScoredSoundPath);

		if (goalStream == null)
		{
			GD.PushWarning($"Goal scored sound was not found at: {GoalScoredSoundPath}");
			return;
		}

		_goalSfxPlayer.Stream = goalStream;
		AddChild(_goalSfxPlayer);
	}

	public void PlayGameplayMusic()
	{
		var stream = ResourceLoader.Load<AudioStream>(GameplayMusicPath);

		if (stream == null)
		{
			GD.PushWarning($"Gameplay music was not found at: {GameplayMusicPath}");
			return;
		}

		PlayMusic(stream);
	}

	public void PlayMusic(AudioStream stream)
	{
		if (stream == null)
			return;

		if (_musicPlayer.Stream != null &&
			_musicPlayer.Stream.ResourcePath == stream.ResourcePath &&
			_musicPlayer.Playing)
		{
			return;
		}

		_fadeTween?.Kill();

		_musicPlayer.Stream = stream;
		_musicPlayer.VolumeDb = MusicVolumeDb;
		_musicPlayer.Play();

		GD.Print("Gameplay music started.");
	}

	public void StopMusic()
	{
		StopMusic(FadeOutSeconds);
	}

	public void StopMusic(float fadeSeconds)
	{
		if (_musicPlayer == null || !_musicPlayer.Playing)
			return;

		_fadeTween?.Kill();

		if (fadeSeconds <= 0f)
		{
			_musicPlayer.Stop();
			_musicPlayer.VolumeDb = MusicVolumeDb;
			return;
		}

		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(_musicPlayer, "volume_db", -45f, fadeSeconds);
		_fadeTween.TweenCallback(Callable.From(() =>
		{
			_musicPlayer.Stop();
			_musicPlayer.VolumeDb = MusicVolumeDb;
			GD.Print("Gameplay music stopped.");
		}));
	}

	public void PlayGoalScoredSound()
	{
		if (_goalSfxCooldown > 0)
			return;

		if (_goalSfxPlayer == null || _goalSfxPlayer.Stream == null)
			return;

		_goalSfxPlayer.VolumeDb = GoalSfxVolumeDb;
		_goalSfxPlayer.Stop();
		_goalSfxPlayer.Play();

		_goalSfxCooldown = GoalSfxCooldownSeconds;

		GD.Print("Goal scored sound played.");
	}
}
