using Godot;
using System.Collections.Generic;

// Owns the perk lifecycle. One pickup at a time spawns at an airborne spot; when the BALL
// touches it, the effect is applied to whoever last touched the ball (risk/reward around
// possession). Effects are timed and revert exactly. Driven by MatchController: Tick() is
// called only past the freeze guard, so spawning/expiry pause during reset/countdown/pause.
// Scalar buffs change PlayerController fields, so the AI is affected the same as a human.
public partial class PerkManager : Node
{
	[Export] public float SpawnInterval = 10f;
	[Export] public float FirstSpawnDelay = 5f;
	[Export] public float PickupRadius = 40f;

	// Airborne spawn envelope (above the ground band, away from the goal mouths).
	[Export] public float SpawnMinX = 400f;
	[Export] public float SpawnMaxX = 2000f;
	[Export] public float SpawnMinY = 200f;
	[Export] public float SpawnMaxY = 750f;

	[Export] public float SpeedBoostMultiplier = 1.6f;
	[Export] public float HighJumpMultiplier = 1.25f;
	[Export] public float PowerShotMultiplier = 1.8f;
	[Export] public float BigPlayerScale = 1.3f;
	[Export] public float SlowDownMultiplier = 0.55f;
	[Export] public float LowJumpMultiplier = 0.6f;
	[Export] public float WeakKickMultiplier = 0.45f;
	[Export] public float SmallPlayerScale = 0.7f;

	[Export] public float PerkDuration = 6f;

	[Export] public int PerkHudCanvasLayer = 2;
	[Export] public int PerkHudFontSize = 28;

	private Node2D _world;
	private PlayerController _playerOne;
	private PlayerController _playerTwo;
	private Ball _ball;
	private RandomNumberGenerator _rng;
	private GameState _gameState;
	private AudioManager _audio;

	private List<PerkDefinition> _registry;
	private PerkPickup _pickup;
	private bool _pickupIsAdvantage;
	private float _spawnTimer;

	private ActivePerk _activeP1;
	private ActivePerk _activeP2;

	private Label _p1Label;
	private Label _p2Label;

	private class ActivePerk
	{
		public PerkDefinition Def;
		public PerkRestore Restore;
		public float TimeLeft;
	}

	public void Initialize(Node2D world, PlayerController playerOne, PlayerController playerTwo, Ball ball, RandomNumberGenerator rng, GameState gameState)
	{
		_world = world;
		_playerOne = playerOne;
		_playerTwo = playerTwo;
		_ball = ball;
		_rng = rng;
		_gameState = gameState;
		_audio = GetNodeOrNull<AudioManager>("/root/AudioManager");

		BuildRegistry();
		BuildHud();
	}

	public void StartSpawning()
	{
		_spawnTimer = FirstSpawnDelay;
	}

	// Called by MatchController after its freeze guard, so it pauses with the game.
	public void Tick(float delta)
	{
		if (_registry == null)
			return;

		if (!IsInstanceValid(_pickup))
		{
			_pickup = null;
			_spawnTimer -= delta;
			if (_spawnTimer <= 0f)
				SpawnPickup();
		}

		_activeP1 = TickActive(_activeP1, delta);
		_activeP2 = TickActive(_activeP2, delta);
	}

	private ActivePerk TickActive(ActivePerk active, float delta)
	{
		if (active == null)
			return null;

		active.TimeLeft -= delta;
		if (active.TimeLeft <= 0f)
		{
			active.Restore.Revert();
			return null;
		}

		return active;
	}

	// Revert every active perk and remove any on-field pickup, so effects never leak across a
	// goal reset or into the end screen.
	public void ClearAll()
	{
		_activeP1?.Restore.Revert();
		_activeP1 = null;
		_activeP2?.Restore.Revert();
		_activeP2 = null;

		if (IsInstanceValid(_pickup))
			_pickup.QueueFree();
		_pickup = null;

		_spawnTimer = FirstSpawnDelay;
	}

	public override void _Process(double delta)
	{
		UpdateHudLabel(_p1Label, _activeP1);
		UpdateHudLabel(_p2Label, _activeP2);
	}

	private void SpawnPickup()
	{
		PerkDefinition def = _registry[_rng.RandiRange(0, _registry.Count - 1)];

		var pickup = new PerkPickup();
		pickup.Configure(def.Type, def.Color, PickupRadius, def.IconPath);
		_world.AddChild(pickup);
		pickup.GlobalPosition = new Vector2(
			_rng.RandfRange(SpawnMinX, SpawnMaxX),
			_rng.RandfRange(SpawnMinY, SpawnMaxY));
		pickup.PickedUp += OnPickedUp;

		_pickup = pickup;
		_pickupIsAdvantage = def.IsAdvantage;
		_spawnTimer = SpawnInterval;
		_audio?.PlayPerkSpawnSound();
	}

	// Lets the AI see the active pickup so it can chase advantages / avoid disadvantages.
	public bool TryGetActivePerk(out Vector2 pos, out bool isAdvantage)
	{
		if (IsInstanceValid(_pickup))
		{
			pos = _pickup.GlobalPosition;
			isAdvantage = _pickupIsAdvantage;
			return true;
		}

		pos = Vector2.Zero;
		isAdvantage = false;
		return false;
	}

	private void OnPickedUp(int perkType)
	{
		_pickup = null;
		_spawnTimer = SpawnInterval;
		_audio?.PlayPerkPickupSound();

		// Apply to whoever last touched the ball; if nobody has yet, the perk is wasted.
		int toucher = _gameState.LastBallToucher;
		PlayerController holder = toucher == 0 ? _playerOne : toucher == 1 ? _playerTwo : null;
		if (holder == null)
			return;

		PerkDefinition def = _registry.Find(d => (int)d.Type == perkType);
		if (def == null)
			return;

		bool isPlayerOne = holder == _playerOne;

		// One active perk per player: revert the previous one before applying the new one.
		ActivePerk current = isPlayerOne ? _activeP1 : _activeP2;
		current?.Restore.Revert();

		var active = new ActivePerk
		{
			Def = def,
			Restore = def.Apply(holder),
			TimeLeft = def.Duration
		};

		if (isPlayerOne)
			_activeP1 = active;
		else
			_activeP2 = active;
	}

	private void BuildRegistry()
	{
		_registry = new List<PerkDefinition>
		{
			// --- Advantages (good for the holder) ---
			Scalar(PerkType.SpeedBoost, "SPEED+", true, Palette.SkyBlue,
				p => p.Speed, (p, v) => p.Speed = v, SpeedBoostMultiplier,
				"res://Assets/Perks/speed_boost.svg"),
			Scalar(PerkType.HighJump, "JUMP+", true, Palette.Success,
				p => p.JumpVelocity, (p, v) => p.JumpVelocity = v, HighJumpMultiplier,
				"res://Assets/Perks/high_jump.svg"),
			KickPerk(PerkType.PowerShot, "POWER+", true, Palette.ArcadeYellow, PowerShotMultiplier,
				"res://Assets/Perks/power_shot.svg"),
			ScaleNode(PerkType.BigPlayer, "BIG+", true, Palette.TeamBlue, BigPlayerScale,
				"res://Assets/Perks/big_player.svg"),

			// --- Disadvantages (bad for the holder) ---
			Scalar(PerkType.SlowDown, "SLOW-", false, Palette.Danger,
				p => p.Speed, (p, v) => p.Speed = v, SlowDownMultiplier,
				"res://Assets/Perks/slow_down.svg"),
			Scalar(PerkType.LowJump, "JUMP-", false, Palette.Warning,
				p => p.JumpVelocity, (p, v) => p.JumpVelocity = v, LowJumpMultiplier,
				"res://Assets/Perks/low_jump.svg"),
			KickPerk(PerkType.WeakKick, "KICK-", false, Palette.UiDisabled, WeakKickMultiplier,
				"res://Assets/Perks/weak_kick.svg"),
			ScaleNode(PerkType.SmallPlayer, "SMALL-", false, Palette.UiTextMuted, SmallPlayerScale,
				"res://Assets/Perks/small_player.svg"),
		};
	}

	// Multiply a single float field and restore it exactly.
	private PerkDefinition Scalar(PerkType type, string label, bool advantage, Color color,
		System.Func<PlayerController, float> get, System.Action<PlayerController, float> set,
		float multiplier, string iconPath = null)
	{
		return new PerkDefinition
		{
			Type = type,
			Label = label,
			IsAdvantage = advantage,
			Color = color,
			Duration = PerkDuration,
			IconPath = iconPath,
			Apply = holder =>
			{
				float old = get(holder);
				set(holder, old * multiplier);
				return new PerkRestore(() => set(holder, old));
			}
		};
	}

	private PerkDefinition KickPerk(PerkType type, string label, bool advantage, Color color,
		float multiplier, string iconPath = null)
	{
		return new PerkDefinition
		{
			Type = type,
			Label = label,
			IsAdvantage = advantage,
			Color = color,
			Duration = PerkDuration,
			IconPath = iconPath,
			Apply = holder =>
			{
				float oldBase = holder.BaseKickSpeed;
				float oldFactor = holder.KickSpeedFactor;
				holder.BaseKickSpeed = oldBase * multiplier;
				holder.KickSpeedFactor = oldFactor * multiplier; // MaxBallSpeed still caps it
				return new PerkRestore(() =>
				{
					holder.BaseKickSpeed = oldBase;
					holder.KickSpeedFactor = oldFactor;
				});
			}
		};
	}

	private PerkDefinition ScaleNode(PerkType type, string label, bool advantage, Color color,
		float scale, string iconPath = null)
	{
		return new PerkDefinition
		{
			Type = type,
			Label = label,
			IsAdvantage = advantage,
			Color = color,
			Duration = PerkDuration,
			IconPath = iconPath,
			Apply = holder =>
			{
				Vector2 old = holder.Scale;
				holder.Scale = old * scale;
				return new PerkRestore(() => holder.Scale = old);
			}
		};
	}

	private void BuildHud()
	{
		var hudLayer = new CanvasLayer { Name = "PerkHud", Layer = PerkHudCanvasLayer };
		AddChild(hudLayer);

		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		hudLayer.AddChild(root);

		_p1Label = MakeHudLabel(HorizontalAlignment.Left);
		_p1Label.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_p1Label.OffsetLeft = 40;
		_p1Label.OffsetRight = 380;
		_p1Label.OffsetTop = 150;
		_p1Label.OffsetBottom = 210;
		root.AddChild(_p1Label);

		_p2Label = MakeHudLabel(HorizontalAlignment.Right);
		_p2Label.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_p2Label.OffsetLeft = -380;
		_p2Label.OffsetRight = -40;
		_p2Label.OffsetTop = 150;
		_p2Label.OffsetBottom = 210;
		root.AddChild(_p2Label);
	}

	private Label MakeHudLabel(HorizontalAlignment alignment)
	{
		var label = new Label
		{
			HorizontalAlignment = alignment,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		Palette.ApplyReadableLabel(label, PerkHudFontSize);
		return label;
	}

	private static void UpdateHudLabel(Label label, ActivePerk active)
	{
		if (label == null)
			return;

		if (active == null)
		{
			label.Text = "";
			return;
		}

		label.Text = $"{active.Def.Label}  {Mathf.CeilToInt(active.TimeLeft)}s";
		label.AddThemeColorOverride("font_color", active.Def.Color);
	}
}
