using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Mario : CharacterBody3D
{
    public enum MarioState
    {
        idle,
        singleJump,
        doubleJump,
        tripleJump,
        walking,
        running,
        sprinting,
        singleJumpLanding,
        doubleJumpLanding,
        tripleJumpLanding,
    }

    [Export] public float SPEED = 5f;
    public float SPEED_CAP = 25f;
    public float SPPED_ACL = 1f;
    [Export] public float JUMPVELOCITY = 8f;
    public MarioState stateOfMario;
    private readonly CircularBuffer<MarioState> stateHistory = new CircularBuffer<MarioState>(20);

    [Export] public float GRAVITY = 8.19f;
    public float rotation_angle = 0.0f;

    public double mouseSensitivity = 0.001;
    public double twistInput = 0.0;
    public double pitchInput = 0.0;

    Vector3 velocity;

    AnimationTree animationTree;
    public Node3D gameCam;

    //inverseK stuff
    SkeletonIK3D skeletonIK3DLeft;
    SkeletonIK3D skeletonIK3DRight;
    RayCast3D rayCast3DLeft;    
    RayCast3D rayCast3DRight;
    Node3D interpolationLeft;
    Node3D interpolationRight;

    Node3D targetLeft;
    Node3D targetRight;
    
    Node3D noRayCastTargetLeft;
     Node3D noRayCastTargetRight;
    [Export] private float ikRaycastHeight = 0.5f;
    [Export] private float footOffset = .4f;
    [Export] private Vector2 minMaxInterpolation = new Vector2(0f, 5.0f);


    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        base._Ready();
        animationTree = GetNode<AnimationTree>("AnimationTree");
        gameCam = GetNode<Node3D>("CameraController");
        stateOfMario = MarioState.idle;
        //left leg
        skeletonIK3DLeft = GetNode<Node3D>("Armature").GetNode<Skeleton3D>("Skeleton3D").GetNode<SkeletonIK3D>("SkeletonIK3DLeft");
        interpolationLeft = GetNode<Node3D>("InterpolationLeft");
        rayCast3DLeft = GetNode<RayCast3D>("RayCast3DLeft");
        noRayCastTargetLeft = GetNode<Node3D>("NoRayCastTargetLeft");
        targetLeft = GetNode<Node3D>("TargetLeft");
        //right leg
        skeletonIK3DRight = GetNode<Node3D>("Armature").GetNode<Skeleton3D>("Skeleton3D").GetNode<SkeletonIK3D>("SkeletonIK3DRight");
        interpolationRight = GetNode<Node3D>("InterpolationRight");
        rayCast3DRight = GetNode<RayCast3D>("RayCast3DRight");
        noRayCastTargetRight = GetNode<Node3D>("NoRayCastTargetRight");
        targetRight = GetNode<Node3D>("TargetRight");
        skeletonIK3DRight.Start();
        skeletonIK3DLeft.Start();
    }

    public override void _PhysicsProcess(double delta)
    {
        velocity.Y -= GRAVITY * (float)delta;
        if(stateOfMario != MarioState.idle){
            skeletonIK3DRight.Stop();
            skeletonIK3DLeft.Stop();
        }else{
            skeletonIK3DRight.Start();
            skeletonIK3DLeft.Start();
            UpdateIkTargetPos(targetLeft, rayCast3DLeft, noRayCastTargetLeft, footOffset);
            UpdateIkTargetPos(targetRight, rayCast3DRight, noRayCastTargetRight, footOffset);

            skeletonIK3DRight.Interpolation = Mathf.Clamp(interpolationRight.GlobalTransform.Origin.Y, minMaxInterpolation.X, minMaxInterpolation.Y);
        
            skeletonIK3DLeft.Interpolation = Mathf.Clamp(interpolationLeft.GlobalTransform.Origin.Y, minMaxInterpolation.X, minMaxInterpolation.Y);
        }
        
        if (Input.IsActionPressed("CamRight"))
        {
            gameCam.RotateY(Mathf.DegToRad(5));
        }
        if (Input.IsActionPressed("CamLeft"))
        {
            gameCam.RotateY(Mathf.DegToRad(-5));
        }


        // Get the Vector3 Velocity because XY and Z cant be edited individually
        velocity = Velocity;

        Vector2 input = Input.GetVector("left", "right", "up", "down");
       // Check if there is any input for movement
      
        if (input != Vector2.Zero)
        {
            // Accelerate the speed gradually until it reaches the speed cap
            SPEED = Mathf.Min(SPEED + SPPED_ACL * (float)delta, SPEED_CAP);
            // Get the camera basis to move Mario in the direction relative to the camera
            Basis camBasis = gameCam.Transform.Basis;
            Vector3 direction = camBasis * new Vector3(-input.X, 0, -input.Y).Normalized();
        
            // Apply the direction and speed to the velocity
            velocity.X = direction.X * SPEED;
            velocity.Z = direction.Z * SPEED;
            if(IsOnFloor()){
                if(SPEED < 10){
                    stateOfMario = MarioState.walking;
                }else if(SPEED < 15){
                    stateOfMario = MarioState.running;
                }else if (SPEED >= 15){
                    stateOfMario = MarioState.sprinting;
                }
            }
        }else{
        // Decelerate the speed when there's no input
        SPEED = Mathf.Max(SPEED - SPPED_ACL , 5f);  // Return to walking speed gradually
        velocity.X = Mathf.Lerp(velocity.X, 0, (float)delta * 5); // Gradually stop the X movement
        velocity.Z = Mathf.Lerp(velocity.Z, 0, (float)delta * 5); // Gradually stop the Z movement
        }
        // If not on floor add gravity else check if jump was pressed to apply jump velocity
        velocity.Y -= GRAVITY * (float)delta;
        if (!IsOnFloor())
        {
            if(velocity.Y < 1 && stateOfMario == MarioState.doubleJump){
                animationTree.Set("parameters/conditions/doubleJumpFalling", (!IsOnFloor()));
            }
            velocity.Y -= GRAVITY * (float)delta;
        }
        else
        {
            if (velocity.X == 0 && velocity.Z == 0 && IsOnFloor()){
                stateOfMario = MarioState.idle;
                animationTree.Set("parameters/conditions/idle", (IsOnFloor() && velocity.X == 0 && velocity.Z == 0));
        
            }
            if(stateHistory.Contains(MarioState.singleJump) || stateHistory.Contains(MarioState.doubleJump)){
                if(stateHistory.Contains(MarioState.singleJump)){
                    stateOfMario = MarioState.singleJumpLanding;
                }else if( stateHistory.Contains(MarioState.doubleJump)){
                    stateOfMario = MarioState.doubleJumpLanding;
                }else if( stateHistory.Contains(MarioState.tripleJump)){
                    stateOfMario = MarioState.tripleJumpLanding;
                }
            }
            if (Input.IsActionJustPressed("jump")){
                if(stateHistory.Contains(MarioState.doubleJumpLanding)){
                    stateOfMario = MarioState.tripleJump;
                     velocity.Y = JUMPVELOCITY*3;
                     Console.WriteLine("triple jump");
                }
                if(stateHistory.Contains(MarioState.singleJumpLanding)){
                     stateOfMario = MarioState.doubleJump;
                     velocity.Y = JUMPVELOCITY*2;
                     Console.WriteLine("doubleJump");
                }
                if(stateOfMario == MarioState.idle || stateOfMario == MarioState.walking || stateOfMario == MarioState.running || stateOfMario == MarioState.sprinting ){
                    velocity.Y = JUMPVELOCITY;
                    Console.WriteLine("singleJump");
                    stateOfMario = MarioState.singleJump;
                }
            }
        }

        // Apply the velocity back to the built-in velocity variable
        Velocity = velocity;

        // Rotate to moving direction if Velocity.X and Z are not 0
        if (new Vector2(Velocity.X, Velocity.Z).Length() > 0)
        {
            rotation_angle = new Vector2(Velocity.Z, Velocity.X).Angle();
            Vector3 rot = Rotation;
            rot.Y = (float)(Mathf.LerpAngle(Rotation.Y, rotation_angle, 10 * delta));
            Rotation = rot;
        }
       
        animationTree.Set("parameters/conditions/jump", !IsOnFloor() && stateOfMario == MarioState.singleJump);
        
        //animationTree.Set("parameters/conditions/movingWalkingSpeed", (IsOnFloor() && (velocity.X != 0 || velocity.Z != 0)));
            
        animationTree.Set("parameters/conditions/landingFromSingleJump", (IsOnFloor() && (velocity.X == 0 && velocity.Z == 0) && (stateOfMario == MarioState.singleJump || stateOfMario == MarioState.singleJumpLanding)));
        animationTree.Set("parameters/conditions/landingFromDoubleJump", (IsOnFloor() && (velocity.X == 0 && velocity.Z == 0) && stateOfMario == MarioState.doubleJump || stateOfMario == MarioState.doubleJumpLanding));
        animationTree.Set("parameters/conditions/landingFromTripleJump", (IsOnFloor() && (velocity.X == 0 && velocity.Z == 0) && stateOfMario == MarioState.tripleJump || stateOfMario == MarioState.tripleJumpLanding));
        animationTree.Set("parameters/conditions/movingWalkingSpeed", IsOnFloor() && (velocity.X != 0 || velocity.Z != 0) && stateOfMario == MarioState.walking || stateOfMario == MarioState.singleJumpLanding|| stateOfMario == MarioState.doubleJumpLanding);
        animationTree.Set("parameters/conditions/movingRunningSpeed", IsOnFloor() && (velocity.X != 0 || velocity.Z != 0) && stateOfMario == MarioState.running);
        animationTree.Set("parameters/conditions/movingRunningMaxSpeed", IsOnFloor() && (velocity.X != 0 || velocity.Z != 0) && stateOfMario == MarioState.sprinting);
            
        animationTree.Set("parameters/conditions/doubleJumpRising", (!IsOnFloor() && stateOfMario == MarioState.doubleJump));
        animationTree.Set("parameters/conditions/tripleJump", (!IsOnFloor() && stateOfMario == MarioState.tripleJump));
        animationTree.Set("parameters/conditions/idle", (IsOnFloor() && velocity.X == 0 && velocity.Z == 0) && stateOfMario == MarioState.idle);
        if(!IsOnFloor() && stateOfMario == MarioState.tripleJump){
            Console.WriteLine("Triple jump animation");
        }

        // Camera controller match the position of Mario
        gameCam.Position = gameCam.Position.Lerp(Position, 0.08f);
        stateHistory.Add(stateOfMario);
        MoveAndSlide();
    
    }
   private void UpdateIkTargetPos(Node3D target, RayCast3D raycast, Node3D noRaycastPos, float footHeightOffset)
    {
        if (raycast.IsColliding())
        {
            float hitPoint = raycast.GetCollisionPoint().Y + footHeightOffset;
            Vector3 newOrigin = target.GlobalTransform.Origin;
            newOrigin.Y = hitPoint;
            target.GlobalTransform = new Transform3D(target.GlobalTransform.Basis, newOrigin);
        }
        else
        {
            Vector3 noRaycastOrigin = noRaycastPos.GlobalTransform.Origin;
            Vector3 targetOrigin = target.GlobalTransform.Origin;
            targetOrigin.Y = noRaycastOrigin.Y;
            target.GlobalTransform = new Transform3D(target.GlobalTransform.Basis, targetOrigin);
        }
    }
 
}