using Godot;

// Static catalogue for the "Create Clubs" feature. A Club is a cosmetic team identity - a name,
// an emblem (drawn procedurally from its two colours) and exactly 3 selectable human-head player
// avatars - plus small, balanced stat modifiers so clubs feel a little different to play without
// any of them being strictly best. The avatars themselves are generated in code (the same approach
// as the original code-drawn placeholder) so the feature ships without binary art assets.
public static class ClubDatabase
{
	public const int PlayersPerClub = 3;

	// One selectable human head. Only the face (skin) and hair colour change between a club's three
	// players; the kit/body colour always comes from the club so a side reads as one team.
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
		public readonly Color PrimaryColor;   // kit / body colour and emblem base
		public readonly Color AccentColor;    // shirt stripe and emblem detail

		// Stat modifiers are multipliers centred on 1.0 and kept inside a narrow band so no club
		// dominates: each is a little better at one thing and a little worse at another.
		public readonly float SpeedMultiplier;
		public readonly float JumpMultiplier;
		public readonly float KickMultiplier;

		public readonly PlayerVariant[] Players;

		public Club(string name, Color primary, Color accent,
			float speed, float jump, float kick, PlayerVariant[] players)
		{
			Name = name;
			PrimaryColor = primary;
			AccentColor = accent;
			SpeedMultiplier = speed;
			JumpMultiplier = jump;
			KickMultiplier = kick;
			Players = players;
		}
	}

	// Shared human skin / hair tones reused across clubs so the three avatars per club stay
	// visually distinct from one another.
	private static readonly Color SkinLight = new Color(0.99f, 0.83f, 0.69f);
	private static readonly Color SkinMid = new Color(0.85f, 0.63f, 0.45f);
	private static readonly Color SkinDark = new Color(0.55f, 0.38f, 0.26f);

	private static readonly Color HairBlack = new Color(0.12f, 0.10f, 0.10f);
	private static readonly Color HairBrown = new Color(0.36f, 0.22f, 0.12f);
	private static readonly Color HairBlond = new Color(0.93f, 0.80f, 0.36f);

	private static PlayerVariant[] HumanHeads()
	{
		return new[]
		{
			new PlayerVariant("Striker", SkinLight, HairBrown),
			new PlayerVariant("Captain", SkinMid, HairBlack),
			new PlayerVariant("Winger", SkinDark, HairBlond),
		};
	}

	public static readonly Club[] Clubs =
	{
		new Club("Falcons", Palette.FromHex("#2F6FE0"), Palette.FromHex("#FFD23F"),
			speed: 1.10f, jump: 1.00f, kick: 0.94f, HumanHeads()),

		new Club("Bulls", Palette.FromHex("#C0392B"), Palette.FromHex("#F2F2F2"),
			speed: 0.95f, jump: 1.00f, kick: 1.12f, HumanHeads()),

		new Club("Sharks", Palette.FromHex("#1ABC9C"), Palette.FromHex("#0B3D33"),
			speed: 1.00f, jump: 1.10f, kick: 0.96f, HumanHeads()),

		new Club("Dragons", Palette.FromHex("#8E44AD"), Palette.FromHex("#FFC857"),
			speed: 1.05f, jump: 1.05f, kick: 0.93f, HumanHeads()),

		new Club("Wolves", Palette.FromHex("#5D6D7E"), Palette.FromHex("#E74C3C"),
			speed: 0.97f, jump: 0.98f, kick: 1.08f, HumanHeads()),
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
