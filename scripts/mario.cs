using Godot;
using System;

public partial class mario : CharacterBody3D
{
	public const float Speed = 5.0f;
	public const float JumpVelocity = 4.5f;

	public double mouseSensitivity = 0.001;
	public double twistInput = 0.0;
	public double pitchInput = 0.0;
	

	AnimationTree animationTree;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        base._Ready();
		animationTree = GetNode<AnimationTree>("AnimationTree");
		Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
			velocity.Y -= gravity * (float)delta;

		// Handle Jump.
		if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
			velocity.Y = JumpVelocity;

		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.
		Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

		Velocity = velocity;
		animationTree.Set("parameters/conditions/jump", IsOnFloor() == false);
		animationTree.Set("parameters/conditions/idle", (IsOnFloor() && velocity.X == 0));
		animationTree.Set("parameters/conditions/moving", (IsOnFloor() && velocity.X != 0));

		if(Input.IsActionJustPressed("ui_cancel")){
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
		
		MoveAndSlide();
	}
}
