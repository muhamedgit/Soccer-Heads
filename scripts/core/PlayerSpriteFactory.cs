using Godot;
using System;

// Procedural art factory for the clubs feature.
// Builds the in-match player avatar, head thumbnail and club emblem.
// For emblems: tries the SVG crest asset first (available after editor import),
// then falls back to a procedural disc so the game works before first import.
public static class PlayerSpriteFactory
{
	// Full in-match avatar: head-soccer style — big cartoon head with a club collar below.
	// No torso or arms; the kicking leg is a separate Sprite2D child on the PlayerController.
	public static Texture2D BuildPlayerTexture(
		int width,
		int height,
		int playerIndex,
		in ClubDatabase.Club club,
		in ClubDatabase.PlayerVariant variant,
		Color outlineColor,
		int outline)
	{
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color bodyColor   = club.PrimaryColor;
		Color accentColor = club.AccentColor;

		// Big head — nearly fills the canvas width
		Vector2 headCenter = new Vector2(width / 2.0f, height * 0.38f);
		float headRadius   = width * 0.43f;

		// Club shirt collar strip peeking below the chin
		int collarH = Mathf.RoundToInt(height * 0.14f);
		int collarW = Mathf.RoundToInt(width  * 0.54f);
		int collarX = width / 2 - collarW / 2;
		int collarY = Mathf.RoundToInt(headCenter.Y + headRadius * 0.77f);
		DrawRect(image, collarX - outline, collarY - outline, collarW + outline * 2, collarH + outline * 2, outlineColor);
		DrawRect(image, collarX, collarY, collarW, collarH, bodyColor);

		// Accent stripe on collar (horizontal for P1, vertical for P2)
		if (playerIndex == 1)
		{
			int stripeH = Math.Max(4, collarH / 4);
			DrawRect(image, collarX + outline, collarY + collarH / 3, collarW - outline * 2, stripeH, accentColor);
		}
		else
		{
			int stripeW = Math.Max(4, collarW / 7);
			DrawRect(image, width / 2 - stripeW / 2, collarY + outline, stripeW, collarH - outline * 2, accentColor);
		}

		// Head drawn on top so it overlaps and hides the collar top edge
		DrawHumanHead(image, headCenter, headRadius, variant.SkinColor, variant.HairColor, outlineColor, outline);

		return ImageTexture.CreateFromImage(image);
	}

	// Square head thumbnail for the selection grid — matches the in-game head-soccer style.
	public static Texture2D BuildHeadThumbnail(
		int size,
		in ClubDatabase.Club club,
		in ClubDatabase.PlayerVariant variant)
	{
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color outlineColor = new Color(0f, 0f, 0f, 1f);
		int outline = Math.Max(2, size / 28);

		// Collar strip at the bottom edge
		int collarH = (int)(size * 0.22f);
		DrawRect(image, 0, size - collarH - outline, size, collarH + outline + 1, outlineColor);
		DrawRect(image, outline, size - collarH, size - outline * 2, collarH, club.PrimaryColor);

		// Big head overlapping the collar
		Vector2 headCenter = new Vector2(size * 0.5f, size * 0.45f);
		float headRadius   = size * 0.38f;
		DrawHumanHead(image, headCenter, headRadius, variant.SkinColor, variant.HairColor, outlineColor, outline);

		return ImageTexture.CreateFromImage(image);
	}

	// Club emblem: tries loading the SVG crest from CrestPath first.
	// Falls back to a procedural coloured disc if the asset isn't imported yet.
	public static Texture2D BuildEmblem(int size, in ClubDatabase.Club club)
	{
		if (!string.IsNullOrEmpty(club.CrestPath))
		{
			try
			{
				var svgTex = ResourceLoader.Load<Texture2D>(club.CrestPath);
				if (svgTex != null)
					return svgTex;
			}
			catch { }
		}

		return BuildProceduralEmblem(size, club);
	}

	private static Texture2D BuildProceduralEmblem(int size, in ClubDatabase.Club club)
	{
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color outlineColor = new Color(0f, 0f, 0f, 1f);
		Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
		float radius = size * 0.46f;

		DrawFilledCircle(image, center, radius, outlineColor);
		DrawFilledCircle(image, center, radius - Math.Max(2, size / 24f), club.AccentColor);
		DrawFilledCircle(image, center, radius * 0.78f, club.PrimaryColor);

		float half = radius * 0.42f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				if (Math.Abs(x - center.X) + Math.Abs(y - center.Y) <= half)
					image.SetPixel(x, y, club.AccentColor);
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private static void DrawHumanHead(
		Image image,
		Vector2 center,
		float radius,
		Color skin,
		Color hair,
		Color outlineColor,
		int outline)
	{
		// Ears — drawn BEFORE the head circle so the head naturally covers their inner edge,
		// making them look like they're attached at the sides.
		float earY = center.Y + radius * 0.06f;
		float earR = radius * 0.16f;
		DrawFilledCircle(image, new Vector2(center.X - radius, earY), earR + outline, outlineColor);
		DrawFilledCircle(image, new Vector2(center.X + radius, earY), earR + outline, outlineColor);
		DrawFilledCircle(image, new Vector2(center.X - radius, earY), earR, skin);
		DrawFilledCircle(image, new Vector2(center.X + radius, earY), earR, skin);

		// 1. Black outline
		DrawFilledCircle(image, center, radius + outline, outlineColor);
		// 2. Skin
		DrawFilledCircle(image, center, radius, skin);

		// 3. Hair cap — everything above hairLine is hair-coloured
		float hairLine = center.Y - radius * 0.20f;
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Math.Min(image.GetWidth()  - 1, Mathf.CeilToInt(center.X + radius));
		int minY = Math.Max(0, Mathf.FloorToInt(center.Y - radius));
		int maxY = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(hairLine));

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				if (new Vector2(x, y).DistanceTo(center) <= radius && y <= hairLine)
					image.SetPixel(x, y, hair);
			}
		}

		// 4. Facial features drawn on top of skin + hair
		DrawFaceFeatures(image, center, radius, hairLine, outlineColor);
	}

	// Draws cartoon eyes, eyebrows, nose dots and a small mouth.
	private static void DrawFaceFeatures(
		Image image, Vector2 center, float radius, float hairLine, Color outlineColor)
	{
		float eyeSpacing = radius * 0.36f;
		float eyeRX      = radius * 0.19f;
		float eyeRY      = radius * 0.14f;

		// Place brows just below the hairline, eyes below brows
		float browY = hairLine + radius * 0.10f;
		float eyeY  = browY   + radius * 0.22f;

		var leftEye  = new Vector2(center.X - eyeSpacing, eyeY);
		var rightEye = new Vector2(center.X + eyeSpacing, eyeY);

		// Eye whites
		DrawEllipse(image, leftEye,  eyeRX * 1.15f, eyeRY * 1.15f, Colors.White);
		DrawEllipse(image, rightEye, eyeRX * 1.15f, eyeRY * 1.15f, Colors.White);

		// Irises
		Color irisColor = new Color(0.22f, 0.14f, 0.08f);
		DrawFilledCircle(image, leftEye,  eyeRY * 0.85f, irisColor);
		DrawFilledCircle(image, rightEye, eyeRY * 0.85f, irisColor);

		// Pupils (offset slightly up-right for life)
		var pupilOff = new Vector2(eyeRY * 0.15f, -eyeRY * 0.12f);
		DrawFilledCircle(image, leftEye  + pupilOff, eyeRY * 0.42f, Colors.Black);
		DrawFilledCircle(image, rightEye + pupilOff, eyeRY * 0.42f, Colors.Black);

		// Tiny specular highlights
		var hlOff = new Vector2(-eyeRY * 0.28f, -eyeRY * 0.28f);
		DrawFilledCircle(image, leftEye  + hlOff, eyeRY * 0.18f, Colors.White);
		DrawFilledCircle(image, rightEye + hlOff, eyeRY * 0.18f, Colors.White);

		// Eyebrows (thin filled rectangles)
		int browW = Mathf.RoundToInt(eyeRX * 2.0f);
		int browH = Math.Max(2, Mathf.RoundToInt(eyeRY * 0.55f));
		DrawRect(image, Mathf.RoundToInt(leftEye.X  - browW / 2f), Mathf.RoundToInt(browY - browH - 1), browW, browH, outlineColor);
		DrawRect(image, Mathf.RoundToInt(rightEye.X - browW / 2f), Mathf.RoundToInt(browY - browH - 1), browW, browH, outlineColor);

		// Nose (two subtle nostril dots)
		float noseY  = center.Y + radius * 0.22f;
		float noseDot = Math.Max(1f, radius * 0.065f);
		var   noseAlpha = new Color(0f, 0f, 0f, 0.28f);
		DrawFilledCircle(image, new Vector2(center.X - radius * 0.10f, noseY), noseDot, noseAlpha);
		DrawFilledCircle(image, new Vector2(center.X + radius * 0.10f, noseY), noseDot, noseAlpha);

		// Mouth (small rounded rectangle)
		float mouthY = center.Y + radius * 0.42f;
		int   mouthW = Mathf.RoundToInt(radius * 0.50f);
		int   mouthH = Math.Max(2, Mathf.RoundToInt(radius * 0.13f));
		DrawRect(image,
			Mathf.RoundToInt(center.X - mouthW / 2f), Mathf.RoundToInt(mouthY),
			mouthW, mouthH,
			new Color(0.50f, 0.12f, 0.12f, 0.85f));
	}

	private static void DrawEllipse(Image image, Vector2 center, float radiusX, float radiusY, Color color)
	{
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radiusX));
		int maxX = Math.Min(image.GetWidth()  - 1, Mathf.CeilToInt(center.X + radiusX));
		int minY = Math.Max(0, Mathf.FloorToInt(center.Y - radiusY));
		int maxY = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(center.Y + radiusY));

		for (int y = minY; y <= maxY; y++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				float dx = (x - center.X) / radiusX;
				float dy = (y - center.Y) / radiusY;
				if (dx * dx + dy * dy <= 1.0f)
					image.SetPixel(x, y, color);
			}
		}
	}

	private static void DrawRect(Image image, int x, int y, int width, int height, Color color)
	{
		int startX = Math.Max(0, x);
		int startY = Math.Max(0, y);
		int endX   = Math.Min(image.GetWidth(),  x + width);
		int endY   = Math.Min(image.GetHeight(), y + height);

		for (int py = startY; py < endY; py++)
		{
			for (int px = startX; px < endX; px++)
				image.SetPixel(px, py, color);
		}
	}

	private static void DrawFilledCircle(Image image, Vector2 center, float radius, Color color)
	{
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Math.Min(image.GetWidth()  - 1, Mathf.CeilToInt(center.X + radius));
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

	private static Color Darken(Color color, float amount)
	{
		return new Color(color.R * (1f - amount), color.G * (1f - amount), color.B * (1f - amount), color.A);
	}
}
