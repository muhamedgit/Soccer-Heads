using Godot;
using System;

// Static description of one perk. Apply mutates the holder (the last player to touch the ball)
// and returns a PerkRestore that undoes exactly what it changed.
public class PerkDefinition
{
	public PerkType Type;
	public string Label;
	public bool IsAdvantage;
	public Color Color;
	public float Duration;
	public Func<PlayerController, PerkRestore> Apply;
	public string IconPath; // res:// path to SVG icon (loaded after editor import)
}
