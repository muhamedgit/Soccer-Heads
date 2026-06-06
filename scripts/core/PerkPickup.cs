using Godot;
using System;

// A pickup that sits on the pitch. When the BALL touches it, it emits PickedUp and frees itself
// (the effect goes to whoever last touched the ball). Mirrors GoalTrigger's Area2D + lock pattern.
public partial class PerkPickup : Area2D
{
	[Signal]
	public delegate void PickedUpEventHandler(int perkType);

	private const int Outline = 4;

	private PerkType _type;
	private float _radius = 40f;
	private Color _color = Colors.White;
	private bool _locked;

	public void Configure(PerkType type, Color color, float radius)
	{
		_type = type;
		_color = color;
		_radius = radius;
	}

	public override void _Ready()
	{
		Monitoring = true;
		CollisionLayer = 1u << 5; // layer 6 (perks)
		CollisionMask = 1u << 1;  // detect the ball (layer 2)

		var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = _radius } };
		AddChild(shape);

		var sprite = new Sprite2D { Texture = BuildTexture(), ZIndex = 5 };
		AddChild(sprite);

		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node body)
	{
		if (_locked)
			return;

		if (body.IsInGroup("ball"))
		{
			_locked = true;
			EmitSignal(SignalName.PickedUp, (int)_type);
			QueueFree();
		}
	}

	private Texture2D BuildTexture()
	{
		int r = Mathf.CeilToInt(_radius);
		int size = (r + Outline) * 2;
		Vector2 center = new Vector2(size / 2f, size / 2f);

		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		DrawFilledCircle(image, center, r + Outline, new Color(0f, 0f, 0f, 1f)); // outline
		DrawFilledCircle(image, center, r, _color);                              // body
		DrawFilledCircle(image, center - new Vector2(r * 0.3f, r * 0.3f), r * 0.28f,
			new Color(1f, 1f, 1f, 0.55f));                                       // highlight

		return ImageTexture.CreateFromImage(image);
	}

	private static void DrawFilledCircle(Image image, Vector2 center, float radius, Color color)
	{
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Math.Min(image.GetWidth() - 1, Mathf.CeilToInt(center.X + radius));
		int minY = Math.Max(0, Mathf.FloorToInt(center.Y - radius));
		int maxY = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(center.Y + radius));

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				if (new Vector2(x, y).DistanceTo(center) <= radius)
					image.SetPixel(x, y, color);
			}
		}
	}
}
