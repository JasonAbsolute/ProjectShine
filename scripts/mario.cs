using Godot;
using System;

public partial class mario : CharacterBody3D
{
	[Export] public float SPEED = 5f;
	[Export] public float JUMPVELOCITY = 8f;

	[Export] public float GRAVITY = 8.19f;    public float rotation_angle = 0.0f;

	public double mouseSensitivity = 0.001;
	public double twistInput = 0.0;
	public double pitchInput = 0.0;
    
	Vector3 velocity;
	

	AnimationTree animationTree;
	public Node3D gameCam;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        base._Ready();
		animationTree = GetNode<AnimationTree>("AnimationTree");
		gameCam = GetNode<Node3D>("CameraController");
	
    }

    public override void _PhysicsProcess(double delta)
	{
		
		if(Input.IsActionPressed("CamRight")){
			gameCam.RotateY(Mathf.DegToRad(5));
		}
		if(Input.IsActionPressed("CamLeft")){
			gameCam.RotateY(Mathf.DegToRad(-5));
		}
		//Get the Vector3 Velocity because XY and Z cant be edited individually
		velocity = Velocity;

		Vector2 input = Input.GetVector("left","right","up","down");
		//Get the current camera used by the viewport
		Basis camBasis = gameCam.Transform.Basis;
		//Use the basis of the camera to determine where it should move
		Vector3 direction = camBasis * new Vector3(-input.X, 0, -input.Y).Normalized();

		//change only the X and Y of our new velocity varriable saving Y for Gravity and Jumping
		velocity.X = direction.X * SPEED;
		velocity.Z = direction.Z * SPEED;

		//if not on floor add gravity else check if jump was press to apply jump velocity
		if(!IsOnFloor()){
			
			velocity.Y -= GRAVITY * (float)delta;
		}else{
			
			if(Input.IsActionJustPressed("jump")){
				velocity.Y = JUMPVELOCITY;
						
			}
		}


		//apply the velocity back to the built-in velocity variable
		Velocity = velocity;

		//Rotate to moving direction if Velocity.X and Z are not 0
		if (new Vector2(Velocity.X, Velocity.Z).Length() > 0){
			rotation_angle = new Vector2(Velocity.Z, Velocity.X).Angle();
			Vector3 rot = Rotation;
			rot.Y = (float)(Mathf.LerpAngle(Rotation.Y, rotation_angle, 10 * delta));
			Rotation = rot;

		}
		MoveAndSlide();
		animationTree.Set("parameters/conditions/jump", IsOnFloor() == false);
		animationTree.Set("parameters/conditions/idle", (IsOnFloor() && velocity.X == 0 && velocity.Z == 0));
		animationTree.Set("parameters/conditions/moving", (IsOnFloor() && (velocity.X  != 0 || velocity.Z != 0)));

		//camera controller match the position of mario
		gameCam.Position = gameCam.Position.Lerp(Position, 0.08f);
	}

}