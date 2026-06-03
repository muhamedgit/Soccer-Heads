using Godot;

public partial class Ball : RigidBody2D
{
	public enum BallVisualStyle
	{
		Simple,
		HighContrast
	}

	[Export] public BallVisualStyle VisualStyle = BallVisualStyle.HighContrast;
	[Export] public int SpriteDiameter = 64;
	[Export] public int OutlineThickness = 4;

	[Export] public float MinHitSpeed = 140f;
	[Export] public float StrongHitSpeed = 900f;
	[Export] public double HitSoundCooldownSeconds = 0.08;
	[Export] public float MinHitVolumeDb = -18f;
	[Export] public float MaxHitVolumeDb = -6f;

	private const string BallHitSoundPath = "res://Assets/Audio/ball_hit.wav";
	private const string HitSoundBus = "Master";

	private AudioStreamPlayer _hitPlayer;
	private double _hitCooldown;
	private Vector2 _lastLinearVelocity;

	private bool _teleportQueued;
	private Vector2 _teleportTarget;

	private const uint PlayerLayer = 1 << 0;
	private const uint GroundLayer = 1 << 2;
	private const uint WallsLayer = 1 << 3;

	public override void _Ready()
	{
		AddToGroup("ball");

		ContactMonitor = true;
		MaxContactsReported = 8;

		SetupSprite();
		SetupHitSound();

		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_hitCooldown > 0)
			_hitCooldown -= delta;

		_lastLinearVelocity = LinearVelocity;
	}

	// Teleport the ball reliably. Setting GlobalPosition directly on a RigidBody2D is
	// overwritten by the physics server, so the move is applied inside _IntegrateForces.
	public void ResetTo(Vector2 position)
	{
		_teleportTarget = position;
		_teleportQueued = true;
	}

	public override void _IntegrateForces(PhysicsDirectBodyState2D state)
	{
		if (!_teleportQueued)
			return;

		Transform2D transform = state.Transform;
		transform.Origin = _teleportTarget;
		state.Transform = transform;
		state.LinearVelocity = Vector2.Zero;
		state.AngularVelocity = 0f;

		_lastLinearVelocity = Vector2.Zero;
		_teleportQueued = false;
	}

	public void Kick(Vector2 direction, float strength)
	{
		ApplyCentralImpulse(direction.Normalized() * strength);
	}

	private void SetupHitSound()
	{
		_hitPlayer = new AudioStreamPlayer();
		_hitPlayer.Name = "BallHitPlayer";
		_hitPlayer.Bus = HitSoundBus;

		AudioStream hitStream = ResourceLoader.Load<AudioStream>(BallHitSoundPath);

		if (hitStream == null)
		{
			GD.PushWarning($"Ball hit sound was not found at: {BallHitSoundPath}");
			return;
		}

		_hitPlayer.Stream = hitStream;
		AddChild(_hitPlayer);
	}

	private void OnBodyEntered(Node body)
	{
		if (_hitCooldown > 0)
			return;

		if (!IsRelevantCollision(body))
			return;

		float impactSpeed = _lastLinearVelocity.Length();

		if (impactSpeed < MinHitSpeed)
			return;

		PlayHitSound(impactSpeed);
	}

	private bool IsRelevantCollision(Node body)
	{
		if (body is CollisionObject2D collisionObject)
		{
			uint relevantLayers = PlayerLayer | GroundLayer | WallsLayer;

			if ((collisionObject.CollisionLayer & relevantLayers) != 0)
				return true;
		}

		if (body is CharacterBody2D)
			return true;

		if (body is StaticBody2D)
			return true;

		if (body.IsInGroup("player"))
			return true;

		if (body.IsInGroup("wall"))
			return true;

		if (body.IsInGroup("ground"))
			return true;

		return false;
	}

	private void PlayHitSound(float impactSpeed)
	{
		if (_hitPlayer == null || _hitPlayer.Stream == null)
			return;

		float volumeRatio = Mathf.InverseLerp(MinHitSpeed, StrongHitSpeed, impactSpeed);
		volumeRatio = Mathf.Clamp(volumeRatio, 0f, 1f);

		_hitPlayer.VolumeDb = Mathf.Lerp(MinHitVolumeDb, MaxHitVolumeDb, volumeRatio);

		_hitPlayer.Stop();
		_hitPlayer.Play();

		_hitCooldown = HitSoundCooldownSeconds;
	}

	private void SetupSprite()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite == null)
		{
			GD.PushWarning("Ball is missing a Sprite2D child.");
			return;
		}

		var collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

		sprite.Centered = true;
		sprite.Position = Vector2.Zero;
		sprite.ZIndex = 10;

		if (collision != null)
			collision.Position = Vector2.Zero;

		int diameter = SpriteDiameter;

		if (collision?.Shape is CircleShape2D circle)
		{
			diameter = Mathf.RoundToInt(circle.Radius * 2.0f);
			if (diameter < 16)
				diameter = 16;
		}

		sprite.Texture = BuildBallTexture(diameter, VisualStyle, OutlineThickness);
	}

	private Texture2D BuildBallTexture(int diameter, BallVisualStyle style, int outlineThickness)
	{
		int size = Mathf.Max(diameter, 16);

		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Vector2 center = new Vector2((size - 1) / 2.0f, (size - 1) / 2.0f);
		float radius = size * 0.5f - 1.0f;

		Color fillColor = style == BallVisualStyle.HighContrast
			? new Color(1.0f, 0.92f, 0.2f, 1.0f)
			: Colors.White;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				Vector2 p = new Vector2(x, y);
				float dist = p.DistanceTo(center);

				if (dist <= radius)
				{
					image.SetPixel(x, y, fillColor);

					if (dist >= radius - outlineThickness)
						image.SetPixel(x, y, Colors.Black);
				}
			}
		}

		if (style == BallVisualStyle.HighContrast)
			DrawHighContrastPattern(image, center, radius);

		return ImageTexture.CreateFromImage(image);
	}

	private void DrawHighContrastPattern(Image image, Vector2 center, float radius)
	{
		DrawFilledCircle(image, center, radius * 0.18f, Colors.Black);

		float ringDistance = radius * 0.48f;
		float patchRadius = radius * 0.12f;

		float[] angles = { -90f, -18f, 54f, 126f, 198f };

		foreach (float angleDeg in angles)
		{
			float angle = Mathf.DegToRad(angleDeg);
			Vector2 patchCenter = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringDistance;
			DrawFilledCircle(image, patchCenter, patchRadius, Colors.Black);
		}
	}

	private void DrawFilledCircle(Image image, Vector2 center, float radius, Color color)
	{
		int minX = Mathf.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Mathf.Min(image.GetWidth() - 1, Mathf.CeilToInt(center.X + radius));
		int minY = Mathf.Max(0, Mathf.FloorToInt(center.Y - radius));
		int maxY = Mathf.Min(image.GetHeight() - 1, Mathf.CeilToInt(center.Y + radius));

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				Vector2 p = new Vector2(x, y);
				if (p.DistanceTo(center) <= radius)
					image.SetPixel(x, y, color);
			}
		}
	}
}
