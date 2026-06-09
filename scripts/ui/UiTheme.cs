using Godot;

// Shared menu styling so every screen matches the in-game arcade look instead of plain
// gray controls: rounded team-colored buttons with hover/press states and an outlined
// font, on top of the stadium backdrop dimmed for readability.
public static class UiTheme
{
	// Builds a Theme that styles Buttons, PanelContainers and Labels with the arcade palette.
	public static Theme Build()
	{
		var theme = new Theme();

		theme.SetStylebox("normal",   "Button", Button(Palette.TeamBlue, Palette.Darken(Palette.TeamBlue, 0.32f)));
		theme.SetStylebox("hover",    "Button", Button(Palette.Lighten(Palette.TeamBlue, 0.14f), Palette.ArcadeYellow));
		theme.SetStylebox("pressed",  "Button", Button(Palette.Darken(Palette.TeamBlue, 0.20f), Palette.Darken(Palette.TeamBlue, 0.40f)));
		theme.SetStylebox("disabled", "Button", Button(Palette.UiDisabled, Palette.Darken(Palette.UiDisabled, 0.30f)));
		theme.SetStylebox("focus",    "Button", Button(Palette.TeamBlue, Palette.ArcadeYellow));

		theme.SetColor("font_color",          "Button", Palette.UiText);
		theme.SetColor("font_hover_color",    "Button", Palette.UiText);
		theme.SetColor("font_pressed_color",  "Button", Palette.ArcadeYellow);
		theme.SetColor("font_disabled_color", "Button", Palette.UiTextMuted);
		theme.SetColor("font_outline_color",  "Button", new Color(0f, 0f, 0f, 0.9f));
		theme.SetConstant("outline_size",     "Button", 5);
		theme.SetFontSize("font_size",        "Button", 26);

		theme.SetStylebox("panel", "PanelContainer", Palette.CreateHudPanelStyle());

		theme.SetColor("font_color",         "Label", Palette.UiText);
		theme.SetColor("font_outline_color", "Label", new Color(0f, 0f, 0f, 0.85f));
		theme.SetConstant("outline_size",    "Label", 4);

		return theme;
	}

	private static StyleBoxFlat Button(Color bg, Color border)
	{
		var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
		s.SetBorderWidthAll(4);
		s.SetCornerRadiusAll(14);
		s.SetContentMarginAll(12);
		s.ContentMarginLeft = 22;
		s.ContentMarginRight = 22;
		s.ShadowColor = Palette.PanelShadow;
		s.ShadowSize = 6;
		s.ShadowOffset = new Vector2(0f, 4f);
		return s;
	}

	// Applies the theme to a menu root and (optionally) drops the dimmed stadium backdrop
	// behind it. Overlays shown on top of gameplay (e.g. pause) should pass withBackdrop:false.
	public static void Apply(Control root, bool withBackdrop = true, float dimAlpha = 0.45f)
	{
		if (root == null)
			return;

		root.Theme = Build();
		if (withBackdrop)
			AddBackdrop(root, dimAlpha);
	}

	private static void AddBackdrop(Control root, float dimAlpha)
	{
		if (root.HasNode("MenuBackdrop"))
			return;

		var tex = ResourceLoader.Load<Texture2D>("res://Assets/Backgrounds/Stadium.png");
		int index = 0;

		if (tex != null)
		{
			var bg = new TextureRect
			{
				Name = "MenuBackdrop",
				Texture = tex,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			root.AddChild(bg);
			root.MoveChild(bg, index++);
		}

		var dim = new ColorRect
		{
			Name = "MenuDim",
			Color = new Color(0.04f, 0.08f, 0.14f, dimAlpha),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);
		root.MoveChild(dim, index);
	}
}
