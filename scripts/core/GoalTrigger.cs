using Godot;

public partial class GoalTrigger : Area2D
{
	[Signal]
	public delegate void GoalScoredEventHandler(int scoringPlayerIndex);

	[Export] public int ScoringPlayerIndex;

	private bool _locked;

	public override void _Ready()
	{
		Monitoring = true;
		BodyEntered += OnBodyEntered;
	}

	public void ResetLock()
	{
		_locked = false;
	}

	private void OnBodyEntered(Node body)
	{
		if (_locked)
			return;

		if (!body.IsInGroup("ball"))
			return;

		_locked = true;
		EmitSignal(SignalName.GoalScored, ScoringPlayerIndex);
	}
}
