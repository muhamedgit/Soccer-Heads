using Godot;
using System;

// Procedural art factory for the clubs feature.
// Builds the in-match player avatar, head thumbnail and club emblem.
// Player visuals use a rounded-rectangle head (Soccer Heads arcade style).
// For emblems: tries the SVG crest asset first (available after editor import),
// then falls back to a procedural disc so the game works before first import.
public static class PlayerSpriteFactory
{
	// Full in-match avatar: big rounded-rect head + small club-colored body stub.
	// The kicking leg is a separate Sprite2D child on PlayerController.
	public static Texture2D BuildPlayerTexture(
		int width, int height, int playerIndex,
		in ClubDatabase.Club club, in ClubDatabase.PlayerVariant variant,
		Color outlineColor, int outline)
	{
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		// Head: rounded rectangle filling ~85% of canvas width
		float hx = 14f, hy = 8f;
		float hw = width  - 28f;
		float hh = height * 0.58f;
		float hr = 22f;

		DrawRoundedHead(image, hx, hy, hw, hh, hr,
			variant.SkinColor, variant.HairColor, outlineColor, outline);

		// Club body stub — small rectangle below the chin (partially behind the head)
		float bw = width  * 0.45f;
		float bh = height * 0.17f;
		float bx = width  / 2f - bw / 2f;
		float by = hy + hh - outline * 2f;

		DrawRect(image,
			Mathf.RoundToInt(bx - outline), Mathf.RoundToInt(by - outline),
			Mathf.RoundToInt(bw + outline * 2), Mathf.RoundToInt(bh + outline * 2),
			outlineColor);
		DrawRect(image,
			Mathf.RoundToInt(bx), Mathf.RoundToInt(by),
			Mathf.RoundToInt(bw), Mathf.RoundToInt(bh),
			club.PrimaryColor);

		// Accent stripe on body (horizontal for P1, vertical for P2)
		if (playerIndex == 1)
		{
			int sh = Math.Max(4, Mathf.RoundToInt(bh / 4));
			DrawRect(image,
				Mathf.RoundToInt(bx + outline), Mathf.RoundToInt(by + bh / 3),
				Mathf.RoundToInt(bw - outline * 2), sh,
				club.AccentColor);
		}
		else
		{
			int sw = Math.Max(4, Mathf.RoundToInt(bw / 7));
			DrawRect(image,
				width / 2 - sw / 2, Mathf.RoundToInt(by + outline),
				sw, Mathf.RoundToInt(bh - outline * 2),
				club.AccentColor);
		}

		return ImageTexture.CreateFromImage(image);
	}

	// Square head thumbnail for the selection grid.
	public static Texture2D BuildHeadThumbnail(
		int size, in ClubDatabase.Club club, in ClubDatabase.PlayerVariant variant)
	{
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color outlineColor = new Color(0f, 0f, 0f, 1f);
		int outline = Math.Max(2, size / 28);

		// Club colour strip at the bottom (shows shirt)
		int collarH = (int)(size * 0.22f);
		DrawRect(image, 0, size - collarH - outline, size, collarH + outline + 1, outlineColor);
		DrawRect(image, outline, size - collarH, size - outline * 2, collarH, club.PrimaryColor);

		// Rounded rect head overlapping the collar
		float hx = size * 0.08f, hy = size * 0.05f;
		float hw = size * 0.84f, hh = size * 0.80f;
		float hr = size * 0.10f;
		DrawRoundedHead(image, hx, hy, hw, hh, hr,
			variant.SkinColor, variant.HairColor, outlineColor, outline);

		return ImageTexture.CreateFromImage(image);
	}

	// Club emblem: tries SVG first, falls back to procedural disc.
	public static Texture2D BuildEmblem(int size, in ClubDatabase.Club club)
	{
		if (!string.IsNullOrEmpty(club.CrestPath))
		{
			try
			{
				var svgTex = ResourceLoader.Load<Texture2D>(club.CrestPath);
				if (svgTex != null) return svgTex;
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
			for (int x = 0; x < size; x++)
				if (Math.Abs(x - center.X) + Math.Abs(y - center.Y) <= half)
					image.SetPixel(x, y, club.AccentColor);

		return ImageTexture.CreateFromImage(image);
	}

	// Draws a rounded-rectangle head with ears, hair cap, and cartoon face.
	private static void DrawRoundedHead(
		Image image, float x, float y, float w, float h, float cornerR,
		Color skin, Color hair, Color outlineColor, int outline)
	{
		float cx = x + w / 2f;
		float cy = y + h / 2f;
		float featureRef = h * 0.42f; // scaling reference for face features

		// Ears: drawn BEFORE the head so the head outline covers their inner half
		float earY = cy + h * 0.04f;
		float earR = h * 0.10f;
		DrawFilledCircle(image, new Vector2(x, earY),     earR + outline, outlineColor);
		DrawFilledCircle(image, new Vector2(x + w, earY), earR + outline, outlineColor);
		DrawFilledCircle(image, new Vector2(x, earY),     earR, skin);
		DrawFilledCircle(image, new Vector2(x + w, earY), earR, skin);

		// Head: outline shell then skin fill
		DrawRoundedRect(image,
			x - outline, y - outline, w + outline * 2, h + outline * 2,
			cornerR + outline, outlineColor);
		DrawRoundedRect(image, x, y, w, h, cornerR, skin);

		// Hair cap: top 33% of head height, pixel-clipped to the rounded rect
		float hairlineY = y + h * 0.33f;
		int   pyHairMax = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(hairlineY));
		for (int py = Math.Max(0, Mathf.FloorToInt(y)); py <= pyHairMax; py++)
		{
			for (int px = Math.Max(0, Mathf.FloorToInt(x));
			     px <= Math.Min(image.GetWidth() - 1, Mathf.CeilToInt(x + w)); px++)
			{
				if (IsInsideRoundedRect(px, py, x, y, w, h, cornerR))
					image.SetPixel(px, py, hair);
			}
		}

		DrawFaceFeatures(image, new Vector2(cx, cy), featureRef, hairlineY, outlineColor);
	}

	// Soccer Heads style face: large circular eyes, brows, nostril dots, mouth line.
	private static void DrawFaceFeatures(
		Image image, Vector2 center, float radius, float hairLine, Color outlineColor)
	{
		float eyeSpacing = radius * 0.40f;
		float eyeR       = radius * 0.20f;

		float browY = hairLine + radius * 0.07f;
		float eyeY  = browY   + radius * 0.22f;

		var leftEye  = new Vector2(center.X - eyeSpacing, eyeY);
		var rightEye = new Vector2(center.X + eyeSpacing, eyeY);

		// Eye: outline ring → white → iris → pupil → specular highlight
		DrawFilledCircle(image, leftEye,  eyeR * 1.35f, outlineColor);
		DrawFilledCircle(image, rightEye, eyeR * 1.35f, outlineColor);
		DrawFilledCircle(image, leftEye,  eyeR * 1.20f, Colors.White);
		DrawFilledCircle(image, rightEye, eyeR * 1.20f, Colors.White);

		Color irisColor = new Color(0.22f, 0.14f, 0.08f);
		DrawFilledCircle(image, leftEye,  eyeR * 0.78f, irisColor);
		DrawFilledCircle(image, rightEye, eyeR * 0.78f, irisColor);

		var pupilOff = new Vector2(eyeR * 0.15f, -eyeR * 0.12f);
		DrawFilledCircle(image, leftEye  + pupilOff, eyeR * 0.42f, Colors.Black);
		DrawFilledCircle(image, rightEye + pupilOff, eyeR * 0.42f, Colors.Black);

		var hlOff = new Vector2(-eyeR * 0.30f, -eyeR * 0.30f);
		DrawFilledCircle(image, leftEye  + hlOff, eyeR * 0.20f, Colors.White);
		DrawFilledCircle(image, rightEye + hlOff, eyeR * 0.20f, Colors.White);

		// Eyebrows
		int browW = Mathf.RoundToInt(eyeR * 2.2f);
		int browH = Math.Max(2, Mathf.RoundToInt(eyeR * 0.45f));
		DrawRect(image,
			Mathf.RoundToInt(leftEye.X  - browW / 2f), Mathf.RoundToInt(browY - browH - 2),
			browW, browH, outlineColor);
		DrawRect(image,
			Mathf.RoundToInt(rightEye.X - browW / 2f), Mathf.RoundToInt(browY - browH - 2),
			browW, browH, outlineColor);

		// Nostril dots
		float noseY   = center.Y + radius * 0.22f;
		float noseDot = Math.Max(1f, radius * 0.055f);
		var   noseCol = new Color(0f, 0f, 0f, 0.30f);
		DrawFilledCircle(image, new Vector2(center.X - radius * 0.09f, noseY), noseDot, noseCol);
		DrawFilledCircle(image, new Vector2(center.X + radius * 0.09f, noseY), noseDot, noseCol);

		// Mouth
		float mouthY = center.Y + radius * 0.42f;
		int   mouthW = Mathf.RoundToInt(radius * 0.46f);
		int   mouthH = Math.Max(2, Mathf.RoundToInt(radius * 0.11f));
		DrawRect(image,
			Mathf.RoundToInt(center.X - mouthW / 2f), Mathf.RoundToInt(mouthY),
			mouthW, mouthH, new Color(0.50f, 0.12f, 0.12f, 0.90f));
	}

	private static bool IsInsideRoundedRect(float px, float py, float x, float y, float w, float h, float r)
	{
		if (px < x || px > x + w || py < y || py > y + h) return false;
		if (px < x + r     && py < y + r)     return new Vector2(px, py).DistanceTo(new Vector2(x + r,     y + r))     <= r;
		if (px > x + w - r && py < y + r)     return new Vector2(px, py).DistanceTo(new Vector2(x + w - r, y + r))     <= r;
		if (px < x + r     && py > y + h - r) return new Vector2(px, py).DistanceTo(new Vector2(x + r,     y + h - r)) <= r;
		if (px > x + w - r && py > y + h - r) return new Vector2(px, py).DistanceTo(new Vector2(x + w - r, y + h - r)) <= r;
		return true;
	}

	private static void DrawRoundedRect(Image image, float x, float y, float w, float h, float r, Color color)
	{
		int minX = Math.Max(0, Mathf.FloorToInt(x));
		int maxX = Math.Min(image.GetWidth()  - 1, Mathf.CeilToInt(x + w));
		int minY = Math.Max(0, Mathf.FloorToInt(y));
		int maxY = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(y + h));
		for (int py = minY; py <= maxY; py++)
			for (int px = minX; px <= maxX; px++)
				if (IsInsideRoundedRect(px, py, x, y, w, h, r))
					image.SetPixel(px, py, color);
	}

	private static void DrawRect(Image image, int x, int y, int width, int height, Color color)
	{
		int startX = Math.Max(0, x);
		int startY = Math.Max(0, y);
		int endX   = Math.Min(image.GetWidth(),  x + width);
		int endY   = Math.Min(image.GetHeight(), y + height);
		for (int py = startY; py < endY; py++)
			for (int px = startX; px < endX; px++)
				image.SetPixel(px, py, color);
	}

	private static void DrawFilledCircle(Image image, Vector2 center, float radius, Color color)
	{
		int minX = Math.Max(0, Mathf.FloorToInt(center.X - radius));
		int maxX = Math.Min(image.GetWidth()  - 1, Mathf.CeilToInt(center.X + radius));
		int minY = Math.Max(0, Mathf.FloorToInt(center.Y - radius));
		int maxY = Math.Min(image.GetHeight() - 1, Mathf.CeilToInt(center.Y + radius));
		for (int y = minY; y <= maxY; y++)
			for (int x = minX; x <= maxX; x++)
				if (new Vector2(x, y).DistanceTo(center) <= radius)
					image.SetPixel(x, y, color);
	}
}
