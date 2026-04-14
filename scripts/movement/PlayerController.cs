 using Godot;

 public partial class PlayerController : CharacterBody2D
 {
	 [Export] public float Speed = 420f;
	 [Export] public float Gravity = 2200f;
	 [Export] public float JumpVelocity = -900f;

	 [Export] public string LeftAction = "Player1_Left";
	 [Export] public string RightAction = "Player1_Right";
	 [Export] public string JumpAction = "Player1_Jump";

	 [Export] public float MinX = 50f;
	 [Export] public float MaxX = 2250f;

	 private bool _wasOnFloor;

	 public override void _Ready()
	 {
		 UpDirection = Vector2.Up;
		 FloorMaxAngle = Mathf.DegToRad(50f);
		 FloorSnapLength = 8f;
	 }

	 public override void _PhysicsProcess(double delta)
	 {
		 var v = Velocity;
		 v.X = Input.GetAxis(LeftAction, RightAction) * Speed;

		 if (!IsOnFloor()) v.Y += Gravity * (float)delta;
		 else if (v.Y > 0f) v.Y = 0f;

		 if (Input.IsActionJustPressed(JumpAction) && IsOnFloor())
			 v.Y = JumpVelocity;

		 Velocity = v;
		 MoveAndSlide();

		 var p = GlobalPosition;
		 p.X = Mathf.Clamp(p.X, MinX, MaxX);
		 GlobalPosition = p;

		 bool onFloor = IsOnFloor();
		 if (onFloor != _wasOnFloor)
		 {
			 GD.Print($"{Name} OnFloor: {onFloor}");
			 _wasOnFloor = onFloor;
		 }
	 }
 }
