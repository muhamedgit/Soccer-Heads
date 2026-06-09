using Godot;
using System;

// Procedural art factory for the clubs feature.
// Builds the in-match player avatar, head thumbnail and club emblem.
// Player visuals use a rounded-rectangle head (Soccer Heads arcade style).
// For emblems: tries the SVG crest asset first (available after editor import),
// then falls back to a procedural disc so the game works before first import.
public static class PlayerSpriteFactory
{
	// Full in-match avatar: the head is an SVG asset (Assets/Heads, wired per variant in
	// ClubDatabase). The kicking foot is a separate Sprite2D child on PlayerController.
	public static Texture2D BuildPlayerTexture(in ClubDatabase.PlayerVariant variant)
	{
		return TryLoadSvg(variant.HeadPath);
	}

	// Loads an imported SVG texture from a res:// path, or null if absent/not yet imported.
	private static Texture2D TryLoadSvg(string path)
	{
		if (string.IsNullOrEmpty(path))
			return null;
		try
		{
			return ResourceLoader.Load<Texture2D>(path);
		}
		catch { return null; }
	}

	// Head thumbnail for the selection grid — the same SVG head shown in-match.
	public static Texture2D BuildHeadThumbnail(in ClubDatabase.PlayerVariant variant)
	{
		return TryLoadSvg(variant.HeadPath);
	}

	// Side-view football boot. Toe points RIGHT in its natural orientation (0° rotation),
	// the heel/ankle rises at the LEFT, and a pale sole runs along the bottom — so when the
	// foot rests flat it clearly reads as a foot, and the toe leads when it swings up.
	public static Texture2D BuildFootTexture(int width, int height, Color outlineColor, int outline)
	{
		Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		Color bootBody = new Color(0.16f, 0.13f, 0.12f);
		Color bootSole = new Color(0.93f, 0.91f, 0.85f);
		Color laces    = new Color(1f, 1f, 1f, 0.70f);

		// Foot body sits in the lower part; the ankle nub rises above it at the heel side.
		float footTop = height * 0.34f;
		float footH   = height - footTop;
		float footR   = footH * 0.48f;        // rounds the toe (right) and heel (left)

		float ankleW  = width * 0.34f;        // shin/ankle stub at the heel (left)
		float ankleH  = footTop + footH * 0.45f;
		float ankleR  = ankleW * 0.45f;

		// Outline (slightly enlarged shapes drawn underneath the fills)
		DrawRoundedRect(image, -outline, footTop - outline,
			width + outline * 2f, footH + outline * 2f, footR + outline, outlineColor);
		DrawRoundedRect(image, -outline, -outline,
			ankleW + outline * 2f, ankleH + outline * 2f, ankleR + outline, outlineColor);

		// Dark upper: foot + ankle
		DrawRoundedRect(image, 0, footTop, width, footH, footR, bootBody);
		DrawRoundedRect(image, 0, 0, ankleW, ankleH, ankleR, bootBody);

		// Pale sole along the bottom
		int soleH = Math.Max(4, Mathf.RoundToInt(footH * 0.30f));
		DrawRoundedRect(image, outline, height - soleH - outline,
			width - outline * 2f, soleH, footR * 0.5f, bootSole);

		// Lace marks on the instep (between the ankle and the toe)
		int laceH  = Math.Max(2, height / 12);
		int laceY  = Mathf.RoundToInt(footTop + footH * 0.16f);
		int laceX0 = Mathf.RoundToInt(ankleW * 0.85f);
		int laceX1 = Mathf.RoundToInt(width * 0.80f);
		for (int lx = laceX0; lx < laceX1; lx += laceH + 3)
			DrawRect(image, lx, laceY, Math.Max(2, laceH - 1), laceH, laces);

		return ImageTexture.CreateFromImage(image);
	}

	// Club emblem: tries SVG first, falls back to procedural disc.
	public static Texture2D BuildEmblem(int size, in ClubDatabase.Club club)
	{
		Texture2D svgTex = TryLoadSvg(club.CrestPath);
		if (svgTex != null)
			return svgTex;

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
