using Godot;
using System;

public partial class PlayerController : CharacterBody2D
{
	[Export] public float Speed = 420f;
	[Export] public float GravityUp = 2000f;
	[Export] public float GravityDown = 3800f;
	[Export] public float MaxFallSpeed = 1400f;
	[Export] public float JumpVelocity = -950f;

	[Export] public string LeftAction = "Player1_Left";
	[Export] public string RightAction = "Player1_Right";
	[Export] public string JumpAction = "Player1_Jump";

	[Export] public float MinX = 50f;
	[Export] public float MaxX = 2250f;

	[Export] public float BaseKickImpulse = 120f;
	[Export] public float SpeedToImpulse = 0.25f;
	[Export] public float MinKickImpulse = 90f;
	[Export] public float MaxKickImpulse = 220f;
	[Export] public float KickCooldownSeconds = 0.08f;

	private bool _wasOnFloor;
	private double _kickCooldown;

	public override void _Ready()
	{
		UpDirection = Vector2.Up;
		FloorMaxAngle = Mathf.DegToRad(50f);
		FloorSnapLength = 8f;
	}

	public override void _PhysicsProcess(double delta)
	{
		_kickCooldown = Math.Max(0.0, _kickCooldown - delta);

		var v = Velocity;
		v.X = Input.GetAxis(LeftAction, RightAction) * Speed;

		if (!IsOnFloor())
		{
			float g = v.Y < 0f ? GravityUp : GravityDown;
			v.Y += g * (float)delta;
			v.Y = Mathf.Min(v.Y, MaxFallSpeed);
		}
		else if (v.Y > 0f)
			v.Y = 0f;

		if (Input.IsActionJustPressed(JumpAction) && IsOnFloor())
			v.Y = JumpVelocity;

		Velocity = v;
		MoveAndSlide();

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var collision = GetSlideCollision(i);
			var collider = collision.GetCollider();

			if (collider is RigidBody2D ball && ball.IsInGroup("ball"))
			{
				TryKick(ball);
			}
		}

		var p = GlobalPosition;
		p.X = Mathf.Clamp(p.X, MinX, MaxX);
		GlobalPosition = p;

		bool onFloor = IsOnFloor();
		if (onFloor != _wasOnFloor)
		{
#if DEBUG
			GD.Print($"{Name} OnFloor: {onFloor}");
#endif
			_wasOnFloor = onFloor;
		}
	}

	private void TryKick(RigidBody2D ball)
	{
		if (_kickCooldown > 0)
			return;

		_kickCooldown = KickCooldownSeconds;

		Vector2 dir = (ball.GlobalPosition - GlobalPosition).Normalized();

		float strength = BaseKickImpulse + Velocity.Length() * SpeedToImpulse;
		strength = Mathf.Clamp(strength, MinKickImpulse, MaxKickImpulse);

		ball.ApplyCentralImpulse(dir * strength);
	}
}
