using Godot;
using System;

// Procedural art for the "Create Clubs" feature. Builds the in-match player avatar (a caricatured
// big-head footballer in the club kit, with the chosen human head + hair), plus the small head
// thumbnail and club emblem used on the selection screen. Drawing everything in code keeps the
// three-players-per-club requirement satisfied without shipping binary sprite assets, and matches
// the project's existing code-drawn placeholder approach.
public static class PlayerSpriteFactory
{
	// Full in-match avatar: club kit body + the variant's human head and hair.
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

		Color bodyColor = club.PrimaryColor;
		Color accentColor = club.AccentColor;
		Color shortsColor = Darken(bodyColor, 0.25f);

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

		DrawHumanHead(image, headCenter, headRadius, variant.SkinColor, variant.HairColor, outlineColor, outline);

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

	// Square head-and-shoulders thumbnail for the selection grid.
	public static Texture2D BuildHeadThumbnail(
		int size,
		in ClubDatabase.Club club,
		in ClubDatabase.PlayerVariant variant)
	{
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color outlineColor = new Color(0f, 0f, 0f, 1f);
		int outline = Math.Max(2, size / 28);

		// Shoulders in the club kit across the bottom, then the human head above.
		int shoulderW = (int)(size * 0.62f);
		int shoulderH = (int)(size * 0.34f);
		int shoulderX = (size - shoulderW) / 2;
		int shoulderY = size - shoulderH;
		DrawRect(image, shoulderX - outline, shoulderY - outline, shoulderW + outline * 2, shoulderH + outline * 2, outlineColor);
		DrawRect(image, shoulderX, shoulderY, shoulderW, shoulderH, club.PrimaryColor);

		Vector2 headCenter = new Vector2(size * 0.5f, size * 0.42f);
		float headRadius = size * 0.30f;
		DrawHumanHead(image, headCenter, headRadius, variant.SkinColor, variant.HairColor, outlineColor, outline);

		return ImageTexture.CreateFromImage(image);
	}

	// Round club emblem: kit-coloured disc, accent ring and a centred accent diamond.
	public static Texture2D BuildEmblem(int size, in ClubDatabase.Club club)
	{
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color outlineColor = new Color(0f, 0f, 0f, 1f);
		Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
		float radius = size * 0.46f;

		DrawFilledCircle(image, center, radius, outlineColor);
		DrawFilledCircle(image, center, radius - Math.Max(2, size / 24f), club.AccentColor);
		DrawFilledCircle(image, center, radius * 0.78f, club.PrimaryColor);

		// Centred diamond in the accent colour for a bit of crest detail.
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
		// Outlined skin disc, then a hair cap over the top portion of the head.
		DrawFilledCircle(image, center, radius + outline, outlineColor);
		DrawFilledCircle(image, center, radius, skin);

		float hairLine = center.Y - radius * 0.20f; // hair covers everything above this line
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Math.Min(image.GetWidth() - 1, Mathf.CeilToInt(center.X + radius));
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
	}

	private static void DrawRect(Image image, int x, int y, int width, int height, Color color)
	{
		int startX = Math.Max(0, x);
		int startY = Math.Max(0, y);
		int endX = Math.Min(image.GetWidth(), x + width);
		int endY = Math.Min(image.GetHeight(), y + height);

		for (int py = startY; py < endY; py++)
		{
			for (int px = startX; px < endX; px++)
				image.SetPixel(px, py, color);
		}
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

	private static Color Darken(Color color, float amount)
	{
		return new Color(color.R * (1f - amount), color.G * (1f - amount), color.B * (1f - amount), color.A);
	}
}
