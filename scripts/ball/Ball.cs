using Godot;

public partial class Ball : RigidBody2D
{
	public override void _Ready()
	{
		AddToGroup("ball");
	}

	public void Kick(Vector2 direction, float strength)
	{
		ApplyCentralImpulse(direction.Normalized() * strength);
	}
}
