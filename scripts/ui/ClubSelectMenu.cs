using Godot;

// Club + player selection screen for the "Create Clubs" feature. Reached from the Main Menu Play
// flow. Each side first cycles through the clubs (emblem, name and stat modifiers shown), then picks
// one of that club's three human-head players. In 2-player mode both sides choose; in vs-Computer
// mode only Player 1 chooses and the AI gets a random club + player. Confirming starts the match.
// The whole UI is built in code so the scene file stays minimal.
public partial class ClubSelectMenu : Control
{
	private const int EmblemSize = 200;
	private const int ThumbSize = 150;

	private GameState _gameState;
	private SceneManager _sceneManager;

	private bool _twoPlayers;
	private int _side;                       // 0 = Player 1 choosing, 1 = Player 2 choosing
	private readonly int[] _club = { 0, 1 }; // chosen club per side
	private readonly int[] _player = { 0, 0 };
	private bool _starting;

	private Label _header;
	private TextureRect _emblem;
	private Label _clubName;
	private Label _stats;
	private readonly Button[] _thumbButtons = new Button[ClubDatabase.PlayersPerClub];
	private Button _confirmButton;
	private Button _backButton;

	public override void _Ready()
	{
		_gameState = GetNode<GameState>("/root/GameState");
		_sceneManager = GetNode<SceneManager>("/root/SceneManager");
		_twoPlayers = _gameState.Mode == GameState.GameMode.PlayerVsPlayer;

		// Seed from whatever was last chosen so a rematch reopens on the same picks.
		_club[0] = _gameState.ClubOneIndex;
		_player[0] = _gameState.PlayerOneVariant;
		_club[1] = _gameState.ClubTwoIndex;
		_player[1] = _gameState.PlayerTwoVariant;

		BuildUi();
		RefreshUi();
	}

	private void BuildUi()
	{
		var background = new ColorRect { Color = Palette.UiPanelDarker };
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(background);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.AddThemeConstantOverride("separation", 18);
		center.AddChild(column);

		_header = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		Palette.ApplyReadableLabel(_header, 40);
		column.AddChild(_header);

		// Club row: ◀ arrow | emblem + name + stats | ▶ arrow.
		var clubRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		clubRow.AddThemeConstantOverride("separation", 24);
		column.AddChild(clubRow);

		var leftArrow = MakeButton("◀", 36, 70, 70);
		leftArrow.Pressed += () => CycleClub(-1);
		clubRow.AddChild(leftArrow);

		var clubBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		clubBox.AddThemeConstantOverride("separation", 6);
		clubBox.CustomMinimumSize = new Vector2(360, 0);
		clubRow.AddChild(clubBox);

		_emblem = new TextureRect
		{
			CustomMinimumSize = new Vector2(EmblemSize, EmblemSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		clubBox.AddChild(_emblem);

		_clubName = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		Palette.ApplyReadableLabel(_clubName, 32);
		clubBox.AddChild(_clubName);

		_stats = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		Palette.ApplyMutedLabel(_stats, 20);
		clubBox.AddChild(_stats);

		var rightArrow = MakeButton("▶", 36, 70, 70);
		rightArrow.Pressed += () => CycleClub(1);
		clubRow.AddChild(rightArrow);

		// Player thumbnails (the club's three human heads).
		var pickLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, Text = "Choose your player" };
		Palette.ApplyMutedLabel(pickLabel, 22);
		column.AddChild(pickLabel);

		var thumbRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		thumbRow.AddThemeConstantOverride("separation", 20);
		column.AddChild(thumbRow);

		for (int i = 0; i < _thumbButtons.Length; i++)
		{
			int index = i;
			var button = new Button
			{
				CustomMinimumSize = new Vector2(160, 190),
				ExpandIcon = true,
				IconAlignment = HorizontalAlignment.Center,
				VerticalIconAlignment = VerticalAlignment.Top
			};
			button.AddThemeFontSizeOverride("font_size", 20);
			button.Pressed += () => SelectPlayer(index);
			_thumbButtons[i] = button;
			thumbRow.AddChild(button);
		}

		// Footer: Back | Confirm.
		var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		footer.AddThemeConstantOverride("separation", 40);
		column.AddChild(footer);

		_backButton = MakeButton("Back", 24, 200, 54);
		_backButton.Pressed += OnBackPressed;
		footer.AddChild(_backButton);

		_confirmButton = MakeButton("Confirm", 24, 240, 54);
		_confirmButton.Pressed += OnConfirmPressed;
		footer.AddChild(_confirmButton);
	}

	private static Button MakeButton(string text, int fontSize, int minWidth, int minHeight)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(minWidth, minHeight)
		};
		button.AddThemeFontSizeOverride("font_size", fontSize);
		return button;
	}

	private void CycleClub(int direction)
	{
		_club[_side] = Mathf.PosMod(_club[_side] + direction, ClubDatabase.ClubCount);
		RefreshUi();
	}

	private void SelectPlayer(int index)
	{
		_player[_side] = index;
		RefreshUi();
	}

	private void RefreshUi()
	{
		ClubDatabase.Club club = ClubDatabase.GetClub(_club[_side]);

		string who = _side == 0 ? "PLAYER 1" : "PLAYER 2";
		_header.Text = $"{who} — CHOOSE YOUR CLUB";

		_emblem.Texture = PlayerSpriteFactory.BuildEmblem(EmblemSize, club);
		_clubName.Text = club.Name.ToUpper();
		_stats.Text = FormatStats(club);

		for (int i = 0; i < _thumbButtons.Length; i++)
		{
			ClubDatabase.PlayerVariant variant = club.Players[i];
			_thumbButtons[i].Icon = PlayerSpriteFactory.BuildHeadThumbnail(ThumbSize, club, variant);
			_thumbButtons[i].Text = variant.Name;

			bool selected = i == _player[_side];
			_thumbButtons[i].Modulate = selected ? Colors.White : new Color(0.55f, 0.55f, 0.6f);
			_thumbButtons[i].AddThemeColorOverride(
				"font_color", selected ? Palette.ArcadeYellow : Palette.UiTextMuted);
		}

		bool lastStep = _side == 1 || !_twoPlayers;
		_confirmButton.Text = lastStep ? "Start Match" : "Next: Player 2";
	}

	private static string FormatStats(in ClubDatabase.Club club)
	{
		return $"Speed {Bar(club.SpeedMultiplier)}   Jump {Bar(club.JumpMultiplier)}   Kick {Bar(club.KickMultiplier)}";
	}

	// Map a ~0.90..1.14 multiplier onto a 5-pip bar so clubs are easy to compare at a glance.
	private static string Bar(float multiplier)
	{
		int pips = Mathf.Clamp(Mathf.RoundToInt((multiplier - 0.88f) / 0.065f), 1, 5);
		return new string('■', pips) + new string('□', 5 - pips);
	}

	private void OnConfirmPressed()
	{
		if (_starting)
			return;

		// In 2-player mode, the first confirm advances to Player 2's selection.
		if (_side == 0 && _twoPlayers)
		{
			_side = 1;
			RefreshUi();
			return;
		}

		_starting = true;
		_confirmButton.Disabled = true;

		_gameState.ClubOneIndex = _club[0];
		_gameState.PlayerOneVariant = _player[0];

		if (_twoPlayers)
		{
			_gameState.ClubTwoIndex = _club[1];
			_gameState.PlayerTwoVariant = _player[1];
		}
		else
		{
			// vs-Computer: give the AI a random club + player.
			_gameState.RandomizeClubTwo();
		}

		_sceneManager.GoToMatch();
	}

	private void OnBackPressed()
	{
		if (_starting)
			return;

		// Step back to Player 1's selection, or leave to the main menu from the first step.
		if (_side == 1)
		{
			_side = 0;
			RefreshUi();
			return;
		}

		_sceneManager.GoToMainMenu();
	}
}
