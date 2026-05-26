using Godot;
using System;

public static class Palette
{
	// ============================================================
	// Primary arcade colors
	// ============================================================

	public const string TeamBlueHex = "#2F80ED";
	public const string TeamRedHex = "#FF3B30";
	public const string ArcadeYellowHex = "#FFD23F";
	public const string GrassGreenHex = "#7AC943";
	public const string SkyBlueHex = "#34A4E9";

	public static readonly Color TeamBlue = FromHex(TeamBlueHex);
	public static readonly Color TeamRed = FromHex(TeamRedHex);
	public static readonly Color ArcadeYellow = FromHex(ArcadeYellowHex);
	public static readonly Color GrassGreen = FromHex(GrassGreenHex);
	public static readonly Color SkyBlue = FromHex(SkyBlueHex);

	// ============================================================
	// Neutral UI colors
	// ============================================================

	public const string UiPanelDarkHex = "#162846";
	public const string UiPanelDarkerHex = "#0B1424";
	public const string UiBorderHex = "#314A6E";
	public const string UiTextHex = "#FFFFFF";
	public const string UiTextMutedHex = "#AFC3D8";
	public const string UiDisabledHex = "#6F7D8C";

	public static readonly Color UiPanelDark = FromHex(UiPanelDarkHex);
	public static readonly Color UiPanelDarker = FromHex(UiPanelDarkerHex);
	public static readonly Color UiBorder = FromHex(UiBorderHex);
	public static readonly Color UiText = FromHex(UiTextHex);
	public static readonly Color UiTextMuted = FromHex(UiTextMutedHex);
	public static readonly Color UiDisabled = FromHex(UiDisabledHex);

	// ============================================================
	// Feedback colors
	// ============================================================

	public const string SuccessHex = "#34C759";
	public const string GoalHex = "#FFD23F";
	public const string WarningHex = "#FF9F0A";
	public const string DangerHex = "#FF3B30";

	public static readonly Color Success = FromHex(SuccessHex);
	public static readonly Color Goal = FromHex(GoalHex);
	public static readonly Color Warning = FromHex(WarningHex);
	public static readonly Color Danger = FromHex(DangerHex);

	// ============================================================
	// Transparent overlay colors
	// ============================================================

	public static readonly Color TeamBlueGlow = WithAlpha(TeamBlue, 0.30f);
	public static readonly Color TeamRedGlow = WithAlpha(TeamRed, 0.30f);
	public static readonly Color WhiteGlow = new Color(1f, 1f, 1f, 0.22f);
	public static readonly Color PanelShadow = new Color(0f, 0f, 0f, 0.35f);

	// ============================================================
	// Shared style helpers
	// ============================================================

	public static StyleBoxFlat CreateHudPanelStyle()
	{
		var style = new StyleBoxFlat();
		style.BgColor = UiPanelDarker;
		style.BorderColor = UiBorder;
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(8);
		style.ShadowColor = PanelShadow;
		style.ShadowSize = 5;
		return style;
	}

	public static StyleBoxFlat CreateBlueButtonStyle()
	{
		return CreateButtonStyle(TeamBlue, Darken(TeamBlue, 0.22f));
	}

	public static StyleBoxFlat CreateRedButtonStyle()
	{
		return CreateButtonStyle(TeamRed, Darken(TeamRed, 0.22f));
	}

	public static StyleBoxFlat CreateYellowButtonStyle()
	{
		return CreateButtonStyle(ArcadeYellow, Darken(ArcadeYellow, 0.24f));
	}

	public static StyleBoxFlat CreateButtonStyle(Color background, Color border)
	{
		var style = new StyleBoxFlat();
		style.BgColor = background;
		style.BorderColor = border;
		style.SetBorderWidthAll(3);
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(10);
		style.ShadowColor = PanelShadow;
		style.ShadowSize = 4;
		return style;
	}

	public static void ApplyReadableLabel(Label label, int fontSize = 24)
	{
		if (label == null)
			return;

		label.AddThemeColorOverride("font_color", UiText);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.65f));
		label.AddThemeConstantOverride("shadow_offset_x", 2);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		label.AddThemeFontSizeOverride("font_size", fontSize);
	}

	public static void ApplyMutedLabel(Label label, int fontSize = 18)
	{
		if (label == null)
			return;

		label.AddThemeColorOverride("font_color", UiTextMuted);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.55f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		label.AddThemeFontSizeOverride("font_size", fontSize);
	}

	// ============================================================
	// Utility methods
	// ============================================================

	public static Color FromHex(string hex)
	{
		string cleanHex = hex.Replace("#", "");

		if (cleanHex.Length != 6)
			throw new ArgumentException($"Invalid hex color: {hex}");

		int r = Convert.ToInt32(cleanHex.Substring(0, 2), 16);
		int g = Convert.ToInt32(cleanHex.Substring(2, 2), 16);
		int b = Convert.ToInt32(cleanHex.Substring(4, 2), 16);

		return new Color(r / 255f, g / 255f, b / 255f, 1f);
	}

	public static Color WithAlpha(Color color, float alpha)
	{
		return new Color(color.R, color.G, color.B, alpha);
	}

	public static Color Darken(Color color, float amount)
	{
		return new Color(
			color.R * (1f - amount),
			color.G * (1f - amount),
			color.B * (1f - amount),
			color.A
		);
	}

	public static Color Lighten(Color color, float amount)
	{
		return new Color(
			Mathf.Lerp(color.R, 1f, amount),
			Mathf.Lerp(color.G, 1f, amount),
			Mathf.Lerp(color.B, 1f, amount),
			color.A
		);
	}
}
