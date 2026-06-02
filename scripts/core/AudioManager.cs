using Godot;

public partial class AudioManager : Node
{
	private const string GameplayMusicPath = "res://Assets/Audio/gameplay_loop.wav";
	private const string MusicBusName = "Music";

	[Export] public float MusicVolumeDb = -24f;
	[Export] public float FadeOutSeconds = 0.35f;

	private AudioStreamPlayer _musicPlayer;
	private Tween _fadeTween;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_musicPlayer = new AudioStreamPlayer();
		_musicPlayer.Name = "MusicPlayer";
		_musicPlayer.Bus = MusicBusName;
		_musicPlayer.VolumeDb = MusicVolumeDb;

		AddChild(_musicPlayer);
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
}
