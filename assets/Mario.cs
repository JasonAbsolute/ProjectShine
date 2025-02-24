using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

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
        diving,
        bellySlidingFromDive,
        singleRollout,
        gettingUpFromSliding,
    }

    public const float RUN_SPEED = 26;
    public const int thisistest = 0;
    public const float ROLL_SPEED = 22;
    public const float BASE_JUMP_VELOCITY = 50;
    public const float MAX_JUMP_VELOCITY = 18;
    public MarioState stateOfMario;
    private readonly CircularBuffer<MarioState> stateHistory = new CircularBuffer<MarioState>(20);

    [Export]
    public Vector3 GRAVITY = new Vector3(0, -140, 0);
    public float rotation_angle = 0.0f;

    public double mouseSensitivity = 0.001;
    public double twistInput = 0.0;
    public double pitchInput = 0.0;

    public Vector3 lastKnownDirection;

    Vector3 velocity;
    private Node3D armature;
    public Node3D gameCam;

    private Node3D springArmPivot;
    private SpringArm3D springArm;
    private Camera3D camera;

    // InverseK stuff
    SkeletonIK3D skeletonIK3DLeft;
    SkeletonIK3D skeletonIK3DRight;

    SkeletonIK3D skeletonIK3DWaist;
    RayCast3D rayCast3DLeft;
    RayCast3D rayCast3DRight;
    Node3D interpolationLeft;
    Node3D interpolationRight;

    Node3D targetLeft;
    Node3D targetRight;

    Node3D targetWaist;

    Node3D noRayCastTargetLeft;
    Node3D noRayCastTargetRight;

    [Export]
    private float ikRaycastHeight = 0.5f;

    [Export]
    private float footOffset = .4f;

    [Export]
    private Vector2 minMaxInterpolation = new Vector2(0f, 5.0f);

    private AnimationPlayer animPlayer;
    private float currentJumpVelocity = BASE_JUMP_VELOCITY;
    private float jumpHoldTime = 0.0f;
    private const float MAX_JUMP_HOLD_TIME = 0.2f;
    private bool isJumping = false;

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public int gettingUpFromSlidingTimer = 53;

    public override void _Ready()
    {
        base._Ready();
        stateOfMario = MarioState.idle;

        springArmPivot = GetNode<Node3D>("SpringArmPivot");
        springArm = GetNode<SpringArm3D>("SpringArmPivot/SpringArm3D");
        camera = GetNode<Camera3D>("SpringArmPivot/SpringArm3D/Camera3D");
        armature = GetNode<Node3D>("Armature");

        // Left leg
        skeletonIK3DLeft = GetNode<Node3D>("Armature")
            .GetNode<Skeleton3D>("Skeleton3D")
            .GetNode<SkeletonIK3D>("SkeletonIK3DLeft");
        interpolationLeft = GetNode<Node3D>("Armature/InterpolationLeft");
        rayCast3DLeft = GetNode<RayCast3D>("Armature/RayCast3DLeft");
        noRayCastTargetLeft = GetNode<Node3D>("Armature/NoRayCastTargetLeft");
        targetLeft = GetNode<Node3D>("Armature/TargetLeft");

        // Right leg
        skeletonIK3DRight = GetNode<Node3D>("Armature")
            .GetNode<Skeleton3D>("Skeleton3D")
            .GetNode<SkeletonIK3D>("SkeletonIK3DRight");
        interpolationRight = GetNode<Node3D>("Armature/InterpolationRight");
        rayCast3DRight = GetNode<RayCast3D>("Armature/RayCast3DRight");
        noRayCastTargetRight = GetNode<Node3D>("Armature/NoRayCastTargetRight");
        targetRight = GetNode<Node3D>("Armature/TargetRight");

        // Waist IK
        skeletonIK3DWaist = GetNode<Node3D>("Armature")
            .GetNode<Skeleton3D>("Skeleton3D")
            .GetNode<SkeletonIK3D>("SkeletonIK3D");
        targetWaist = GetNode<Node3D>("Armature/WaistTarget");

        // Start IK
        skeletonIK3DWaist.Start();

        // Animation player
        animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
    }

    public override void _PhysicsProcess(double delta)
    {
        // Add the gravity.
        if (!IsOnFloor())
        {
            velocity += GRAVITY * (float)delta;
            Console.WriteLine("gravity: " + GRAVITY);
        }

        if (stateOfMario != MarioState.idle)
        {
            skeletonIK3DRight.Stop();
            skeletonIK3DLeft.Stop();
        }
        else
        {
            skeletonIK3DWaist.Stop();
            UpdateIkTargetPos(targetLeft, rayCast3DLeft, noRayCastTargetLeft, footOffset);
            UpdateIkTargetPos(targetRight, rayCast3DRight, noRayCastTargetRight, footOffset);

            skeletonIK3DRight.Interpolation = Mathf.Clamp(
                interpolationRight.GlobalTransform.Origin.Y,
                minMaxInterpolation.X,
                minMaxInterpolation.Y
            );
            skeletonIK3DLeft.Interpolation = Mathf.Clamp(
                interpolationLeft.GlobalTransform.Origin.Y,
                minMaxInterpolation.X,
                minMaxInterpolation.Y
            );
        }

        if (stateOfMario != MarioState.sprinting)
        {
            skeletonIK3DWaist.Stop();
            if (stateOfMario == MarioState.diving)
            {
                skeletonIK3DWaist.Stop();
            }
        }
        else
        {
            skeletonIK3DWaist.Start();
            Vector3 currentRotation = targetWaist.RotationDegrees;
            Console.WriteLine("" + currentRotation);
            currentRotation.Z = 120.0f;
            targetWaist.RotationDegrees = currentRotation;

            if (stateOfMario == MarioState.diving)
            {
                skeletonIK3DWaist.Stop();
            }
        }

        // Get the input direction and handle the movement
        Vector2 LstickVec = Input.GetVector("key_a", "key_d", "key_w", "key_s");
        Vector3 direction;
        if (LstickVec.X == 0 && LstickVec.Y == 0) // No WASD input
        {
            LstickVec = Input.GetVector("Lstick_left", "Lstick_right", "Lstick_up", "Lstick_down");
            direction = (Transform.Basis * new Vector3(LstickVec.X, 0, LstickVec.Y)).Normalized();
            direction = direction * LstickVec.Length();
        }
        else
        {
            direction = (Transform.Basis * new Vector3(LstickVec.X, 0, LstickVec.Y)).Normalized();
        }
        direction = direction.Rotated(Vector3.Up, springArmPivot.Rotation.Y);

        var walkingStrength = LstickVec.Length();
        //This is the main logic loop for Player being on the ground
        if (IsOnFloor())
        {
            isJumping = false;
            jumpHoldTime = 0.0f;
            currentJumpVelocity = BASE_JUMP_VELOCITY;
            if (stateOfMario == MarioState.gettingUpFromSliding)
            {
                gettingUpFromSlidingTimer--;
                //Play this animation but if the player does any input then let other animations override.
                //maybe use timer or learn about timmers and how to handle?
                animPlayer.Play("ma_lost");
                if (gettingUpFromSlidingTimer == 0)
                {
                    gettingUpFromSlidingTimer = 53;
                    stateOfMario = MarioState.idle;
                }
            }
            else
            {
                if (stateOfMario == MarioState.bellySlidingFromDive)
                {
                    velocity.X = Mathf.Lerp(velocity.X, 0, .05f);
                    velocity.Z = Mathf.Lerp(velocity.Z, 0, .05f);
                    Console.WriteLine("Vel: " + velocity);
                    if (velocity.X < 0.5 && velocity.Z < 0.5)
                    {
                        stateOfMario = MarioState.gettingUpFromSliding;
                    }
                }
                else
                {
                    if (stateOfMario == MarioState.diving)
                    {
                        animPlayer.Play("ma_slpbk");
                        stateOfMario = MarioState.bellySlidingFromDive;
                    }
                    else
                    {
                        if (Input.IsActionPressed("key_space") || Input.IsActionPressed("button_a"))
                        {
                            isJumping = true;
                            velocity.Y = BASE_JUMP_VELOCITY;
                            stateOfMario = MarioState.singleJump;
                            animPlayer.Play("ma_jump");
                        }

                        // Setting Mario to be idle
                        if (direction == Vector3.Zero && !isJumping)
                        {
                            stateOfMario = MarioState.idle;
                        }

                        velocity.X = Mathf.Lerp(velocity.X, direction.X * RUN_SPEED, .5f);
                        velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * RUN_SPEED, .5f);
                        if (direction != Vector3.Zero)
                        {
                            armature.Rotation = armature.Rotation with
                            {
                                Y = Mathf.LerpAngle(
                                    armature.Rotation.Y,
                                    Mathf.Atan2(-velocity.X, -velocity.Z),
                                    .2f
                                ),
                            };
                            if (Input.IsActionJustPressed("button_b"))
                            {
                                animPlayer.Play("ma_sldct");
                                stateOfMario = MarioState.diving;
                                velocity.X = Mathf.Lerp(
                                    velocity.X,
                                    direction.X * RUN_SPEED * 100,
                                    .01f
                                );
                                velocity.Z = Mathf.Lerp(
                                    velocity.Z,
                                    direction.Z * RUN_SPEED * 100,
                                    .01f
                                );
                                velocity.Y += 10;
                            }
                            else if (walkingStrength > .5)
                            {
                                animPlayer.Play("ma_run2", -1, 1.5f);
                                stateOfMario = MarioState.sprinting;
                            }
                            else
                            {
                                animPlayer.Play("ma_run1");
                                stateOfMario = MarioState.running;
                            }
                        }
                        else
                        {
                            if (
                                (
                                    stateHistory.Contains(MarioState.singleJump)
                                    || stateHistory.Contains(MarioState.tripleJump)
                                )
                                && direction == Vector3.Zero
                            )
                            {
                                animPlayer.Play("ma_laend");
                            }
                            else if (
                                stateHistory.Contains(MarioState.doubleJump)
                                && direction == Vector3.Zero
                            )
                            {
                                Console.WriteLine("landing double");
                                animPlayer.Play("ma_2jmed_Armature");
                            }
                            else
                            {
                                animPlayer.Play("ma_wait");
                                stateOfMario = MarioState.idle;
                            }
                        }

                        if (Input.IsActionPressed("key_space") || Input.IsActionPressed("button_a"))
                        {
                            lastKnownDirection = direction;
                            isJumping = true;
                            if (stateHistory.Contains(MarioState.doubleJump))
                            {
                                velocity.Y = MAX_JUMP_VELOCITY;
                                stateOfMario = MarioState.tripleJump;
                                animPlayer.Play("ma_demo_gate_out_rolling_get");
                            }
                            else if (stateHistory.Contains(MarioState.singleJump))
                            {
                                velocity.Y = BASE_JUMP_VELOCITY * 1.7f;
                                stateOfMario = MarioState.doubleJump;
                                animPlayer.Play("ma_2jmp1");
                            }
                            else
                            {
                                velocity.Y = BASE_JUMP_VELOCITY;
                                stateOfMario = MarioState.singleJump;
                                animPlayer.Play("ma_jump");
                            }
                        }
                    }
                }
            }
        }
        else // Airborne
        {
            if (
                isJumping
                && (Input.IsActionPressed("key_space") || Input.IsActionPressed("button_a"))
            )
            {
                jumpHoldTime += (float)delta;
                if (jumpHoldTime < MAX_JUMP_HOLD_TIME)
                {
                    velocity.Y =
                        BASE_JUMP_VELOCITY
                        + (MAX_JUMP_VELOCITY - BASE_JUMP_VELOCITY)
                            * (jumpHoldTime / MAX_JUMP_HOLD_TIME);
                }
            }

            Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
            if (velXZ.Length() > RUN_SPEED) // Airborne after roll
            {
                velocity.X = Mathf.Lerp(velocity.X, direction.X * ROLL_SPEED, .01f);
                velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * ROLL_SPEED, .01f);
            }
            else
            {
                velocity.X = Mathf.Lerp(velocity.X, direction.X * RUN_SPEED, .01f);
                velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * RUN_SPEED, .01f);
            }

            if (Input.IsActionPressed("button_b"))
            {
                if (stateOfMario != MarioState.diving)
                {
                    if (direction.IsZeroApprox())
                    {
                        Console.WriteLine("no stick");
                        stateOfMario = MarioState.diving;
                    }
                    else
                    {
                        animPlayer.Play("ma_sldct");
                        stateOfMario = MarioState.diving;
                        velocity.X = Mathf.Lerp(
                            velocity.X,
                            lastKnownDirection.X * RUN_SPEED * 200,
                            .01f
                        );
                        velocity.Z = Mathf.Lerp(
                            velocity.Z,
                            lastKnownDirection.Z * RUN_SPEED * 200,
                            .01f
                        );
                    }
                }
            }
        }

        // Camera controller match the position of Mario
        stateHistory.Add(stateOfMario);
        Velocity = velocity;
        MoveAndSlide();
        Velocity = velocity;
    }

    public override void _Process(double delta)
    {
        // Handle rstick camera
        Vector2 RstickVec = Input.GetVector(
            "Rstick_left",
            "Rstick_right",
            "Rstick_up",
            "Rstick_down"
        );
        springArmPivot.RotateY(-RstickVec.X * (float)delta * 5); // Left-right
        springArm.RotateX(-RstickVec.Y * (float)delta * 5); // Up-down
        springArm.Rotation = springArm.Rotation with
        {
            X = Mathf.Clamp(springArm.Rotation.X, -Mathf.Pi * .4f, Mathf.Pi * .3f),
        }; // Downward looking, upward looking
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion ev && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            // Handle mouse camera
            springArmPivot.RotateY(-ev.Relative.X * .005f);
            springArm.RotateX(-ev.Relative.Y * .005f);
            springArm.Rotation = springArm.Rotation with
            {
                X = Mathf.Clamp(springArm.Rotation.X, -Mathf.Pi * .4f, Mathf.Pi * .3f),
            }; // Downward looking, upward looking
        }

        if (Input.IsActionJustPressed("key_esc"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                Input.SetMouseMode(Input.MouseModeEnum.Visible);
            }
            else if (Input.MouseMode == Input.MouseModeEnum.Visible)
            {
                Input.SetMouseMode(Input.MouseModeEnum.Captured);
            }
        }
    }

    private void UpdateIkTargetPos(
        Node3D target,
        RayCast3D raycast,
        Node3D noRaycastPos,
        float footHeightOffset
    )
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
