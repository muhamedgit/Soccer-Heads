 using Godot;

 public partial class PlayerController : CharacterBody2D
 {
	 [Export] public float Speed = 420.0f;
	 [Export] public string LeftAction = "Player1_Left";
	 [Export] public string RightAction = "Player1_Right";

	 [Export] public float MinX = 50.0f;
	 [Export] public float MaxX = 1870.0f;

	 public override void _PhysicsProcess(double delta)
	 {
		 float axis = Input.GetAxis(LeftAction, RightAction);
		 Velocity = new Vector2(axis * Speed, 0.0f);

		 MoveAndSlide();

		 Vector2 pos = GlobalPosition;
		 pos.X = Mathf.Clamp(pos.X, MinX, MaxX);
		 GlobalPosition = pos;
	 }
 }
