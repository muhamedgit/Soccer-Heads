using Godot;

public partial class Ball : RigidBody2D
{
	public override void _Ready()
	{
		AddToGroup("ball");

		GravityScale = 0.0f;
		LockRotation = true;
		LinearDamp = 2.0f;
		AngularDamp = 10.0f;
		ContinuousCd = CCDMode.CastShape;
	}

	public void Kick(Vector2 direction, float strength)
	{
		ApplyCentralImpulse(direction.Normalized() * strength);
	}
}
