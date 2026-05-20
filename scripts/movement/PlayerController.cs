using Godot;
using System;

public partial class PlayerController : CharacterBody2D
{
	[Export] public float Speed = 420f;
	[Export] public float GravityUp = 2000f;
	[Export] public float GravityDown = 3800f;
	[Export] public float MaxFallSpeed = 1400f;
	[Export] public float JumpVelocity = -950f;

	[Export] public string LeftAction = "Player1_Left";
	[Export] public string RightAction = "Player1_Right";
	[Export] public string JumpAction = "Player1_Jump";

	[Export] public float MinX = 50f;
	[Export] public float MaxX = 2250f;

	[Export] public float BaseKickImpulse = 120f;
	[Export] public float SpeedToImpulse = 0.25f;
	[Export] public float MinKickImpulse = 90f;
	[Export] public float MaxKickImpulse = 220f;

	[Export] public Color Player1BodyColor = new Color(0.20f, 0.45f, 0.95f, 1.0f);
	[Export] public Color Player2BodyColor = new Color(0.90f, 0.22f, 0.22f, 1.0f);
	[Export] public Color OutlineColor = new Color(0f, 0f, 0f, 1f);
	[Export] public int PlaceholderWidth = 192;
	[Export] public int PlaceholderHeight = 256;
	[Export] public int OutlineThickness = 5;

	public bool InputEnabled { get; set; } = true;

	private bool _wasOnFloor;
	private RigidBody2D _ballInContact;

	public override void _Ready()
	{
		UpDirection = Vector2.Up;
		FloorMaxAngle = Mathf.DegToRad(50f);
		FloorSnapLength = 8f;

		SetupPlaceholderSprite();
	}

	public override void _PhysicsProcess(double delta)
	{
		var v = Velocity;
		v.X = InputEnabled ? Input.GetAxis(LeftAction, RightAction) * Speed : 0f;

		if (!IsOnFloor())
		{
			float g = v.Y < 0f ? GravityUp : GravityDown;
			v.Y += g * (float)delta;
			v.Y = Mathf.Min(v.Y, MaxFallSpeed);
		}
		else if (v.Y > 0f)
		{
			v.Y = 0f;
		}

		if (InputEnabled && Input.IsActionJustPressed(JumpAction) && IsOnFloor())
			v.Y = JumpVelocity;

		Velocity = v;
		MoveAndSlide();

		RigidBody2D touchedBall = null;
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var collision = GetSlideCollision(i);
			if (collision.GetCollider() is RigidBody2D rb && rb.IsInGroup("ball"))
			{
				touchedBall = rb;
				break;
			}
		}

		// Only kick on fresh contact to prevent repeated impulses while overlapping
		if (touchedBall != null && touchedBall != _ballInContact)
			ApplyKickImpulse(touchedBall);

		_ballInContact = touchedBall;

		var p = GlobalPosition;
		p.X = Mathf.Clamp(p.X, MinX, MaxX);
		GlobalPosition = p;

		bool onFloor = IsOnFloor();
		if (onFloor != _wasOnFloor)
		{
#if DEBUG
			GD.Print($"{Name} OnFloor: {onFloor}");
#endif
			_wasOnFloor = onFloor;
		}
	}

	private void ApplyKickImpulse(RigidBody2D ball)
	{
		Vector2 contactNormal = (ball.GlobalPosition - GlobalPosition).Normalized();
		float speed = Velocity.Length();

		// Blend contact normal with player movement direction so fast runs send the ball forward
		Vector2 dir = speed > 10f
			? (contactNormal + Velocity.Normalized()).Normalized()
			: contactNormal;

		float strength = BaseKickImpulse + speed * SpeedToImpulse;
		strength = Mathf.Clamp(strength, MinKickImpulse, MaxKickImpulse);

		ball.ApplyCentralImpulse(dir * strength);
	}

	private void SetupPlaceholderSprite()
	{
		var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (sprite == null)
		{
			GD.PushWarning($"{Name} is missing Sprite2D.");
			return;
		}

		var collision = GetNodeOrNull<CollisionShape2D>("PlayerColisionArea");

		sprite.Centered = true;
		sprite.Position = Vector2.Zero;
		sprite.ZIndex = 10;

		if (collision != null)
			collision.Position = Vector2.Zero;

		int playerIndex = DetectPlayerIndex();

		Color bodyColor = playerIndex == 2 ? Player2BodyColor : Player1BodyColor;
		Color accentColor = playerIndex == 2
			? new Color(1.0f, 0.93f, 0.20f, 1.0f)
			: Colors.White;

		sprite.Texture = BuildPlaceholderTexture(
			Math.Max(PlaceholderWidth, 48),
			Math.Max(PlaceholderHeight, 64),
			playerIndex,
			bodyColor,
			accentColor,
			OutlineColor,
			Math.Max(OutlineThickness, 2)
		);
	}

	private int DetectPlayerIndex()
	{
		string nodeName = Name.ToString();

		if (LeftAction.Contains("Player2") || nodeName.Contains("2"))
			return 2;

		return 1;
	}

	private Texture2D BuildPlaceholderTexture(
		int width,
		int height,
		int playerIndex,
		Color bodyColor,
		Color accentColor,
		Color outlineColor,
		int outline)
	{
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color headColor = LightenColor(bodyColor, 0.18f);
		Color shortsColor = DarkenColor(bodyColor, 0.25f);

		int torsoX = width / 2 - width / 5;
		int torsoY = height / 3;
		int torsoW = width / 5 * 2;
		int torsoH = height / 3;

		int legW = width / 7;
		int legH = height / 5;
		int leftLegX = width / 2 - legW - width / 18;
		int rightLegX = width / 2 + width / 18;
		int legY = torsoY + torsoH - outline;

		int armW = width / 10;
		int armH = height / 4;
		int leftArmX = torsoX - armW + outline;
		int rightArmX = torsoX + torsoW - outline;
		int armY = torsoY + height / 18;

		Vector2 headCenter = new Vector2(width / 2.0f, height * 0.20f);
		float headRadius = width * 0.17f;

		DrawFilledCircle(image, headCenter, headRadius + outline, outlineColor);
		DrawFilledCircle(image, headCenter, headRadius, headColor);

		DrawRect(image, leftArmX - outline, armY - outline, armW + outline * 2, armH + outline * 2, outlineColor);
		DrawRect(image, rightArmX - outline, armY - outline, armW + outline * 2, armH + outline * 2, outlineColor);
		DrawRect(image, leftArmX, armY, armW, armH, bodyColor);
		DrawRect(image, rightArmX, armY, armW, armH, bodyColor);

		DrawRect(image, torsoX - outline, torsoY - outline, torsoW + outline * 2, torsoH + outline * 2, outlineColor);
		DrawRect(image, torsoX, torsoY, torsoW, torsoH, bodyColor);

		DrawRect(image, torsoX, torsoY + torsoH - height / 12, torsoW, height / 12, shortsColor);

		if (playerIndex == 1)
		{
			int stripeH = Math.Max(6, height / 16);
			DrawRect(image, torsoX + outline, torsoY + torsoH / 4, torsoW - outline * 2, stripeH, accentColor);
		}
		else
		{
			int stripeW = Math.Max(6, width / 10);
			DrawRect(image, width / 2 - stripeW / 2, torsoY + outline, stripeW, torsoH - outline * 2, accentColor);
		}

		DrawRect(image, leftLegX - outline, legY - outline, legW + outline * 2, legH + outline * 2, outlineColor);
		DrawRect(image, rightLegX - outline, legY - outline, legW + outline * 2, legH + outline * 2, outlineColor);
		DrawRect(image, leftLegX, legY, legW, legH, shortsColor);
		DrawRect(image, rightLegX, legY, legW, legH, shortsColor);

		return ImageTexture.CreateFromImage(image);
	}

	private void DrawRect(Image image, int x, int y, int width, int height, Color color)
	{
		int startX = Math.Max(0, x);
		int startY = Math.Max(0, y);
		int endX = Math.Min(image.GetWidth(), x + width);
		int endY = Math.Min(image.GetHeight(), y + height);

		for (int py = startY; py < endY; py++)
		{
			for (int px = startX; px < endX; px++)
			{
				image.SetPixel(px, py, color);
			}
		}
	}

	private void DrawFilledCircle(Image image, Vector2 center, float radius, Color color)
	{
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Math.Min(image.GetWidth() - 1, Mathf.CeilToInt(center.X + radius));
		int minY = Math.Max(0, Mathf.FloorToInt(center.Y - radius));
		int maxY = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(center.Y + radius));

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

	private Color LightenColor(Color color, float amount)
	{
		return new Color(
			Mathf.Lerp(color.R, 1.0f, amount),
			Mathf.Lerp(color.G, 1.0f, amount),
			Mathf.Lerp(color.B, 1.0f, amount),
			color.A
		);
	}

	private Color DarkenColor(Color color, float amount)
	{
		return new Color(
			color.R * (1.0f - amount),
			color.G * (1.0f - amount),
			color.B * (1.0f - amount),
			color.A
		);
	}
}
