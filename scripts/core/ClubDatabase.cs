using Godot;

// Static catalogue of selectable clubs. Each club maps to a real European side and carries
// its authentic colours, a CrestPath pointing to an SVG asset, three player variants with
// club-themed names/looks, and small balanced stat modifiers.
public static class ClubDatabase
{
	public const int PlayersPerClub = 3;

	public readonly struct PlayerVariant
	{
		public readonly string Name;
		public readonly Color SkinColor;
		public readonly Color HairColor;

		public PlayerVariant(string name, Color skin, Color hair)
		{
			Name = name;
			SkinColor = skin;
			HairColor = hair;
		}
	}

	public readonly struct Club
	{
		public readonly string Name;
		public readonly Color PrimaryColor;
		public readonly Color AccentColor;
		public readonly string CrestPath; // res:// path to SVG crest asset (loaded after editor import)

		public readonly float SpeedMultiplier;
		public readonly float JumpMultiplier;
		public readonly float KickMultiplier;

		public readonly PlayerVariant[] Players;

		public Club(string name, Color primary, Color accent, string crestPath,
			float speed, float jump, float kick, PlayerVariant[] players)
		{
			Name = name;
			PrimaryColor = primary;
			AccentColor = accent;
			CrestPath = crestPath;
			SpeedMultiplier = speed;
			JumpMultiplier = jump;
			KickMultiplier = kick;
			Players = players;
		}
	}

	private static readonly Color SkinLight  = new Color(0.99f, 0.83f, 0.69f);
	private static readonly Color SkinMid    = new Color(0.85f, 0.63f, 0.45f);
	private static readonly Color SkinDark   = new Color(0.55f, 0.38f, 0.26f);

	private static readonly Color HairBlack  = new Color(0.10f, 0.08f, 0.08f);
	private static readonly Color HairBrown  = new Color(0.36f, 0.22f, 0.12f);
	private static readonly Color HairBlond  = new Color(0.93f, 0.80f, 0.36f);
	private static readonly Color HairGinger = new Color(0.80f, 0.40f, 0.10f);

	public static readonly Club[] Clubs =
	{
		// Real Madrid — white/gold, speed-focused
		new Club(
			"Real Madrid",
			Palette.FromHex("#FFFFFF"), Palette.FromHex("#F7D300"),
			"res://Assets/Clubs/real_madrid.svg",
			speed: 1.08f, jump: 1.00f, kick: 0.96f,
			new[]
			{
				new PlayerVariant("Bellingham", SkinLight, HairBrown),
				new PlayerVariant("Vinicius",   SkinDark,  HairBlack),
				new PlayerVariant("Modric",     SkinLight, HairBlack),
			}
		),

		// Arsenal — red/white, powerful kick
		new Club(
			"Arsenal",
			Palette.FromHex("#EF0107"), Palette.FromHex("#FFFFFF"),
			"res://Assets/Clubs/arsenal.svg",
			speed: 0.97f, jump: 1.02f, kick: 1.10f,
			new[]
			{
				new PlayerVariant("Saka",       SkinDark,  HairBlack),
				new PlayerVariant("Havertz",    SkinLight, HairBlond),
				new PlayerVariant("Martinelli", SkinDark,  HairBlack),
			}
		),

		// Man City — sky blue/white, high jump
		new Club(
			"Man City",
			Palette.FromHex("#6CABDD"), Palette.FromHex("#FFFFFF"),
			"res://Assets/Clubs/man_city.svg",
			speed: 1.00f, jump: 1.12f, kick: 0.94f,
			new[]
			{
				new PlayerVariant("Haaland",   SkinLight, HairBlond),
				new PlayerVariant("De Bruyne", SkinLight, HairGinger),
				new PlayerVariant("Doku",      SkinDark,  HairBlack),
			}
		),

		// Juventus — black/white, solid kick power
		new Club(
			"Juventus",
			Palette.FromHex("#000000"), Palette.FromHex("#FFFFFF"),
			"res://Assets/Clubs/juventus.svg",
			speed: 1.00f, jump: 1.00f, kick: 1.08f,
			new[]
			{
				new PlayerVariant("Vlahovic", SkinMid,  HairBlack),
				new PlayerVariant("Yildiz",   SkinMid,  HairBlack),
				new PlayerVariant("Bremer",   SkinDark, HairBlack),
			}
		),

		// Liverpool — red/gold, explosive pace
		new Club(
			"Liverpool",
			Palette.FromHex("#C8102E"), Palette.FromHex("#F6EB61"),
			"res://Assets/Clubs/liverpool.svg",
			speed: 1.10f, jump: 0.96f, kick: 1.00f,
			new[]
			{
				new PlayerVariant("Salah",             SkinMid,   HairBlack),
				new PlayerVariant("Nunez",             SkinMid,   HairBlack),
				new PlayerVariant("Alexander-Arnold",  SkinLight, HairBlond),
			}
		),
	};

	public static int ClubCount => Clubs.Length;

	public static Club GetClub(int index)
	{
		return Clubs[Mathf.PosMod(index, Clubs.Length)];
	}

	public static PlayerVariant GetPlayer(int clubIndex, int variant)
	{
		Club club = GetClub(clubIndex);
		return club.Players[Mathf.PosMod(variant, club.Players.Length)];
	}
}
