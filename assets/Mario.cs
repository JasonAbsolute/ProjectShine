using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
        singleJumpDive,
        doubleJumpDive,
        tripleJumpDive,
        bellySlidingFromDive,
        singleRollout,
        gettingUpFromSliding,
        bellyRollout,
        sideFlip,
        sideFlipTurning,
        SpinJump,
        landing,
    }

    public const float RUN_SPEED = 26;
    public const int thisistest = 0;
    public const float ROLL_SPEED = 22;
    public const float BASE_JUMP_VELOCITY = 20;
    public const float MAX_JUMP_VELOCITY_SINGLE = 29;
    public const float MAX_JUMP_VELOCITY_DOUBLE = 40;
    public const float MAX_ROLLOUT_HOLD_TIME = 0.2f;
    public const float BASE_ROLLOUT_VELOCITY = 25;
    public const float MAX_ROLLOUT_VELOCITY = 30;
    public const float TRIPLE_JUMP_VELOCITY = 90;
    public const float GETTING_UP_FROM_SLIDE_ROLL = 30;
    public const float TERMINAL_VELOCITY = 30;

    public bool initalJumpHold = true;

    public MarioState stateOfMario;
    private readonly CircularBuffer<MarioState> stateHistory = new CircularBuffer<MarioState>(7);

    [Export]
    public Vector3 GRAVITY = new Vector3(0, -140, 0);
    public float rotation_angle = 0.0f;

    public double mouseSensitivity = 0.001;
    public double twistInput = 0.0;
    public double pitchInput = 0.0;

    Vector3 velocity;
    private Node3D armature;
    public Node3D gameCam;

    private Node3D springArmPivot;
    private SpringArm3D springArm;
    private Camera3D camera;

    // InverseK stuff
    SkeletonIK3D skeletonIK3DWaist;
    Node3D targetWaist;

    [Export]
    private float ikRaycastHeight = 0.5f;

    [Export]
    private float footOffset = .4f;

    [Export]
    private Vector2 minMaxInterpolation = new Vector2(0f, 5.0f);

    private AnimationPlayer animPlayer;
    private float currentJumpVelocity = BASE_JUMP_VELOCITY;
    private float jumpHoldTime = 0.0f;
    private const float MAX_JUMP_HOLD_TIME_SINGLE = 0.2f;
    private const float MAX_JUMP_HOLD_TIME_DOUBLE = 0.4f;
    private bool isJumping = false;

    private Vector3 lastFacingDirection = Vector3.Zero;

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public int gettingUpFromSlidingTimer = 53;
    public int bellyRolloutTimer = 20;
    public int sittingAnimationTimer = 58;
    public int sleepingTimer = 28;
    public int idleTimer = 180;
    public int sittingTimerWait = 180;
    public int sleeptimer = 100;
    public int sideFlipTurningTimer = 33;
    public int landingTimer = 7;

    //Sleeping effects
    private ZEffectSpawner zEffectSpawner;

    //EyeEffect
    private Texture2D awakeEyeTexture;
    private Texture2D sleepingEyeTexture;
    private StandardMaterial3D leftEyeMaterial;
    private StandardMaterial3D rightEyeMaterial;
    private MeshInstance3D marioMesh;

    //HandEffects
    private StandardMaterial3D RightHandMaterial;
    private StandardMaterial3D RightHandClosedMaterial;
    private StandardMaterial3D LeftHandMaterial;
    private Node3D RightClosedHand;
    private Node3D LeftClosedHand;
    Color currentColor;
    Vector3 direction = new Vector3(0, 0, 0);

    //Changing Direction Sharply
    private readonly CircularBuffer<Vector3> Last4FacingDirections = new CircularBuffer<Vector3>(4);

    //SpinJump stuff
    // Number of frames allowed to complete the quadrant sweep.
    private const int WINDOW_FRAMES = 20;

    // Deadzone to avoid accidental inputs from a nearly centered stick.
    private const float DEADZONE = 0.2f;

    // Track which quadrants have been visited.
    private bool[] quadrantVisited = new bool[4] { false, false, false, false };
    private int frameCounter = 0;
    private bool spinInput = false;

    private float walkingStrength;

    //LandingParticles
    private GpuParticles3D landingParticles;
    private Node3D SpinJumpEffects;
    private GpuParticles3D blueSpinEffects;
    private GpuParticles3D redSpinEffects;
    private GpuParticles3D whiteSpinEffects;
    private Node3D camControl;

    //walljump stuff
    private bool wallJumpInitial = false;

    public override void _Ready()
    {
        base._Ready();
        stateOfMario = MarioState.idle;

        camControl = GetNode<Node3D>("CamController");
        //camControl.Position = Position;
        //camControl.Scale = Scale;
        Console.WriteLine(Scale + " " + Position);

        camera = GetNode<Camera3D>("CamController/SpringArmPivot/SpringArm3D/Camera3D");
        springArmPivot = GetNode<Node3D>("CamController/SpringArmPivot");
        springArm = GetNode<SpringArm3D>("CamController/SpringArmPivot/SpringArm3D");
        armature = GetNode<Node3D>("Armature");

        // Waist IK
        skeletonIK3DWaist = GetNode<Node3D>("Armature")
            .GetNode<Skeleton3D>("Skeleton3D")
            .GetNode<SkeletonIK3D>("SkeletonIK3D");
        targetWaist = GetNode<Node3D>("Armature/WaistTarget");

        // Start IK
        skeletonIK3DWaist.Stop();

        // Animation player
        animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");

        //Setting up anything related to sleeping
        SetupSleeping();

        //Setting up Hands
        setupHandSwaping();

        //Landing Particles
        landingParticles = GetNode<GpuParticles3D>("LandingParticles");
        landingParticles.LocalCoords = false;
        setupSpinJumpEffects();
        //ParticleEffects
    }

    public override void _PhysicsProcess(double delta)
    {
        // Add the gravity.
        if (!IsOnFloor())
        {
            if (velocity.Y < -50)
            {
                //do nothing
            }
            else
            {
                if (stateOfMario == MarioState.SpinJump)
                {
                    if (velocity.Y > 0)
                    {
                        velocity += GRAVITY * (float)delta;
                    }
                    else
                    {
                        velocity += GRAVITY * (float)delta * .5f;
                    }
                }
                else
                {
                    velocity += GRAVITY * (float)delta;
                }
            }
        }

        if (stateOfMario == MarioState.sprinting)
        {
            skeletonIK3DWaist.Start();
            targetWaist.RotationDegrees = new Vector3(0, 90f, 120.0f);
        }
        else
        {
            skeletonIK3DWaist.Stop();
        }

        if (stateOfMario != MarioState.idle)
        {
            SleepStatus(false);
        }
        Vector2 LstickVec = Input.GetVector(
            "Lstick_left",
            "Lstick_right",
            "Lstick_up",
            "Lstick_down"
        );
        // Only process input if it exceeds the deadzone.
        if (LstickVec.Length() > DEADZONE)
        {
            //doing spin jump checks here
            frameCounter++;
            int quadrant = GetQuadrant(LstickVec);
            if (quadrant != -1)
            {
                quadrantVisited[quadrant] = true;
            }

            // Check if all quadrants have been visited.
            if (AllQuadrantsVisited())
            {
                spinInput = true;
                Console.WriteLine("Spin jump is true");
                ResetWindow();
            }

            if (frameCounter >= WINDOW_FRAMES)
            {
                spinInput = false;
                Console.WriteLine("Spin jump is fasle");
                ResetWindow();
            }
        }

        if (
            stateOfMario != MarioState.landing
            && stateOfMario != MarioState.singleJump
            && stateOfMario != MarioState.doubleJump
        )
        {
            // Get the input direction and handle the movement

            if (LstickVec.X == 0 && LstickVec.Y == 0) // No WASD input
            {
                LstickVec = Input.GetVector(
                    "Lstick_left",
                    "Lstick_right",
                    "Lstick_up",
                    "Lstick_down"
                );
                direction = (
                    Transform.Basis * new Vector3(LstickVec.X, 0, LstickVec.Y)
                ).Normalized();
                direction = direction * LstickVec.Length();
            }
            else
            {
                direction = (
                    Transform.Basis * new Vector3(LstickVec.X, 0, LstickVec.Y)
                ).Normalized();
            }

            //this is what changes the players direction based on cam
            direction = direction.Rotated(Vector3.Up, springArmPivot.Rotation.Y);
        }

        walkingStrength = LstickVec.Length();
        if (IsOnWallOnly())
        {
            if (wallJumpInitial)
            {
                //Sets wall jump to still
                velocity.X = 0;
                velocity.Z = 0;
                velocity.Y = 0;
                wallJumpInitial = false;
            }
            else
            {
                animPlayer.Play("ma_wsld");
                Console.WriteLine("Wall jump: " + GetWallNormal());

                if (Input.IsActionJustPressed("button_a"))
                {
                    animPlayer.Play("ma_wjmp");
                    jumpHoldTime += (float)delta;
                    if (initalJumpHold)
                    {
                        if (
                            stateOfMario == MarioState.singleJump
                            || stateOfMario == MarioState.singleJumpDive
                        )
                        {
                            if (initalJumpHold)
                            {
                                if (jumpHoldTime < MAX_JUMP_HOLD_TIME_SINGLE)
                                {
                                    velocity.Y =
                                        BASE_JUMP_VELOCITY
                                        + (MAX_JUMP_VELOCITY_SINGLE - BASE_JUMP_VELOCITY)
                                            * (jumpHoldTime / MAX_JUMP_HOLD_TIME_SINGLE)
                                        + (walkingStrength * 7);
                                }
                            }

                            if (
                                Input.IsActionJustPressed("button_b")
                                && Input.IsActionPressed("button_a")
                            )
                            {
                                animPlayer.Play("ma_sldct");
                                stateOfMario = MarioState.singleJumpDive;
                                // Store the facing direction at the moment of the dive
                                Vector3 diveDirection = lastFacingDirection.Normalized();

                                velocity.X = diveDirection.X * 50;
                                velocity.Z = diveDirection.Z * 50;
                            }
                        }
                    }
                }
            }
        }
        else if (IsOnFloor()) //This is the main logic loop for Player being on the ground
        {
            //This keeps it so player is not able to do double jump when pressing A twice in the air
            //when player lets go of A this will be false untill the ground is hit again
            initalJumpHold = true;
            //This will not be here when grounded spin is implemented
            isSpining(false);
            isJumping = false;
            wallJumpInitial = true;
            jumpHoldTime = 0.0f;
            currentJumpVelocity = BASE_JUMP_VELOCITY;
            //This first check is locking mario into the path he is commiting to when jumping
            if (stateOfMario == MarioState.landing)
            {
                if (landingTimer == 0)
                {
                    Console.WriteLine("HERE" + landingTimer);
                    landingTimer = 6;
                    if (direction != Vector3.Zero)
                    {
                        idleTimer = 1800;

                        lastFacingDirection = direction.Normalized();
                        foreach (var lastFacing in Last4FacingDirections.list)
                        {
                            // Make sure we don't compare zero-length vectors (if that’s possible in your code)
                            if (lastFacing != Vector3.Zero && lastFacingDirection != Vector3.Zero)
                            {
                                // Dot product of two normalized vectors = cos(angle).
                                // If dot < 0, angle > 90°.
                                float dot =
                                    (lastFacing.X * lastFacingDirection.X)
                                    + (lastFacing.Y * lastFacingDirection.Y)
                                    + (lastFacing.Z * lastFacingDirection.Z);
                                // Angle threshold in degrees
                                float angleThreshold = 120f;

                                // Convert degrees to radians
                                float angleThresholdRadians = angleThreshold * (MathF.PI / 180f);

                                // Calculate the dot threshold
                                float dotThreshold = MathF.Cos(angleThresholdRadians);

                                if (dot < dotThreshold)
                                {
                                    if (walkingStrength > 0.7)
                                    {
                                        stateOfMario = MarioState.sideFlipTurning;
                                    }
                                    // side flip logic.
                                }
                            }
                        }

                        RotateArmature();

                        if (
                            Input.IsActionJustPressed("button_b")
                            && stateOfMario != MarioState.sideFlipTurning
                        )
                        {
                            velocity.Y = 0;
                            velocity.Y += 20;
                            animPlayer.Play("ma_sldct");
                            stateOfMario = MarioState.diving;

                            // Set a fixed dive speed instead of interpolating
                            Vector3 diveDirection = lastFacingDirection.Normalized();

                            velocity.X = diveDirection.X * 50;
                            velocity.Z = diveDirection.Z * 50;
                        }
                        else if (stateOfMario == MarioState.sideFlipTurning)
                        {
                            animPlayer.Play("ma_trned");
                            stateOfMario = MarioState.sideFlipTurning;
                        }
                        else if (walkingStrength > .5 && stateOfMario != MarioState.sideFlipTurning)
                        {
                            animPlayer.Play("ma_run2", -1, 2f);
                            stateOfMario = MarioState.sprinting;

                            RightHandMaterial.AlbedoColor = new Color(0, 0, 0, 0);
                            LeftHandMaterial.AlbedoColor = new Color(0, 0, 0, 0);
                            LeftClosedHand.Visible = true;
                            RightClosedHand.Visible = true;
                        }
                        else
                        {
                            animPlayer.Play("ma_run1");
                            stateOfMario = MarioState.running;
                        }
                        // if (stateHistory.Contains(MarioState.singleJump))
                        // {
                        //     animPlayer.Play("ma_laend");
                        //     stateOfMario = MarioState.landing;
                        // }
                    }
                    else
                    {
                        RotateArmature();

                        if (stateOfMario == MarioState.sideFlipTurning)
                        {
                            //play Animation
                        }
                        else
                        {
                            stateOfMario = MarioState.idle;
                            if (
                                (
                                    stateHistory.Contains(MarioState.singleJump)
                                    || stateHistory.Contains(MarioState.tripleJump)
                                    || stateHistory.Contains(MarioState.bellyRollout)
                                    || stateHistory.Contains(MarioState.sideFlip)
                                    || stateHistory.Contains(MarioState.SpinJump)
                                )
                                && direction == Vector3.Zero
                            )
                            {
                                animPlayer.Play("ma_laend");
                                stateOfMario = MarioState.landing;
                                landingParticles.Emitting = true;
                            }
                            else if (stateHistory.Contains(MarioState.doubleJump))
                            {
                                animPlayer.Play("ma_2jmed");
                            }
                            if (stateOfMario == MarioState.idle)
                            {
                                if (idleTimer == 0)
                                {
                                    if (sittingAnimationTimer == 0)
                                    {
                                        if (sittingTimerWait == 0)
                                        {
                                            if (sleepingTimer == 0)
                                            {
                                                animPlayer.Play("ma_sleep_wait");
                                                if (sleeptimer == 0)
                                                {
                                                    //spawn in z every few seconds
                                                    zEffectSpawner.StartZEffect();
                                                    // Apply the updated material back to Surface 7

                                                    sleeptimer = 150;
                                                }
                                                else
                                                {
                                                    sleeptimer--;
                                                }
                                            }
                                            else
                                            {
                                                sleepingTimer--;
                                                animPlayer.Play("ma_sleep");
                                            }
                                        }
                                        else
                                        {
                                            sittingTimerWait--;
                                            animPlayer.Play("ma_sit_wait");
                                            SleepStatus(true);
                                        }
                                    }
                                    else
                                    {
                                        sittingAnimationTimer--;
                                        animPlayer.Play("ma_sit");
                                    }
                                }
                                else
                                {
                                    idleTimer--;
                                    if (animPlayer.CurrentAnimation != ("ma_laend"))
                                    {
                                        animPlayer.Play("ma_wait");
                                    }
                                }
                            }
                        }
                    }
                }
                else if (landingTimer != 0)
                {
                    if (Input.IsActionPressed("button_a"))
                    {
                        isJumping = true;
                        if (spinInput)
                        {
                            stateOfMario = MarioState.SpinJump;
                            velocity.Y = TRIPLE_JUMP_VELOCITY;
                            animPlayer.Play("ma_spin_p");
                        }
                        else if (stateOfMario == MarioState.sideFlipTurning)
                        {
                            //need somthing that update last control stick direction
                            RotateArmature();
                            stateOfMario = MarioState.sideFlip;
                            velocity.Y = TRIPLE_JUMP_VELOCITY;
                            animPlayer.Play("ma_tjmp1");
                        }
                        else if (
                            stateHistory.Contains(MarioState.doubleJump)
                            || stateHistory.Contains(MarioState.SpinJump)
                        )
                        {
                            velocity.Y = TRIPLE_JUMP_VELOCITY;
                            stateOfMario = MarioState.tripleJump;
                            animPlayer.Play("ma_demo_gate_out_rolling_get");
                        }
                        else if (stateHistory.Contains(MarioState.singleJump))
                        {
                            velocity.Y = BASE_JUMP_VELOCITY;
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
                    if (Input.IsActionPressed("button_b"))
                    {
                        velocity.Y = 0;
                        velocity.Y += 20;
                        animPlayer.Play("ma_sldct");
                        stateOfMario = MarioState.diving;

                        // Set a fixed dive speed instead of interpolating
                        Vector3 diveDirection = lastFacingDirection.Normalized();

                        velocity.X = diveDirection.X * 50;
                        velocity.Z = diveDirection.Z * 50;
                    }
                }

                landingTimer--;
            }
            else
            {
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
                        landingParticles.Emitting = true;
                    }
                }
                else
                {
                    if (stateOfMario == MarioState.bellySlidingFromDive)
                    {
                        bellyRolloutTimer = 20;
                        velocity.X = Mathf.Lerp(velocity.X, 0, .05f);
                        velocity.Z = Mathf.Lerp(velocity.Z, 0, .05f);
                        if (
                            velocity.X < 0.1
                            && velocity.Z < 0.1
                            && velocity.X > -0.1
                            && velocity.Z > -0.1
                        )
                        {
                            velocity.X = 0;
                            velocity.Z = 0;
                            stateOfMario = MarioState.gettingUpFromSliding;
                        }
                        if (Input.IsActionPressed("button_a") && checkSpeedForBellyRoll(velocity))
                        {
                            stateOfMario = MarioState.bellyRollout;
                            velocity.Y = GETTING_UP_FROM_SLIDE_ROLL;
                            animPlayer.Play("ma_roll");
                        }
                        else if (Input.IsActionPressed("button_a"))
                        {
                            stateOfMario = MarioState.singleRollout;
                            animPlayer.Play("ma_roll_jump");
                            rolloutAction(delta);
                            // velocity.X = direction.X * 50;
                            // velocity.Z = direction.Z * 50;
                        }
                        else if (Input.IsActionPressed("button_b"))
                        {
                            stateOfMario = MarioState.diving;
                            RotateArmature();
                            velocity.Y = 0;
                            velocity.Y += 20;
                            animPlayer.Play("ma_sldct", 20);
                            Vector3 diveDirection = lastFacingDirection.Normalized();
                            velocity.X = diveDirection.X * 50;
                            velocity.Z = diveDirection.Z * 50;
                        }
                    }
                    else
                    {
                        if (
                            stateOfMario == MarioState.diving
                            || stateOfMario == MarioState.singleJumpDive
                            || stateOfMario == MarioState.doubleJumpDive
                            || stateOfMario == MarioState.tripleJumpDive
                        )
                        {
                            animPlayer.Play("ma_slpbk");
                            stateOfMario = MarioState.bellySlidingFromDive;
                        }
                        else
                        {
                            // Setting Mario to be idle
                            if (
                                direction == Vector3.Zero
                                && !isJumping
                                && sideFlipTurningTimer > 32
                            )
                            {
                                stateOfMario = MarioState.idle;
                            }

                            velocity.X = Mathf.Lerp(velocity.X, direction.X * RUN_SPEED, .5f);
                            velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * RUN_SPEED, .5f);

                            if (direction != Vector3.Zero)
                            {
                                idleTimer = 1800;

                                lastFacingDirection = direction.Normalized();
                                foreach (var lastFacing in Last4FacingDirections.list)
                                {
                                    // Make sure we don't compare zero-length vectors (if that’s possible in your code)
                                    if (
                                        lastFacing != Vector3.Zero
                                        && lastFacingDirection != Vector3.Zero
                                    )
                                    {
                                        // Dot product of two normalized vectors = cos(angle).
                                        // If dot < 0, angle > 90°.
                                        float dot =
                                            (lastFacing.X * lastFacingDirection.X)
                                            + (lastFacing.Y * lastFacingDirection.Y)
                                            + (lastFacing.Z * lastFacingDirection.Z);

                                        if (dot < 0f)
                                        {
                                            if (walkingStrength > 0.7)
                                            {
                                                stateOfMario = MarioState.sideFlipTurning;
                                            }
                                            // side flip logic.
                                        }
                                    }
                                }

                                RotateArmature();

                                if (
                                    Input.IsActionJustPressed("button_b")
                                    && stateOfMario != MarioState.sideFlipTurning
                                )
                                {
                                    velocity.Y = 0;
                                    velocity.Y += 20;
                                    animPlayer.Play("ma_sldct");
                                    stateOfMario = MarioState.diving;

                                    // Set a fixed dive speed instead of interpolating
                                    Vector3 diveDirection = lastFacingDirection.Normalized();

                                    velocity.X = diveDirection.X * 50;
                                    velocity.Z = diveDirection.Z * 50;
                                }
                                else if (stateOfMario == MarioState.sideFlipTurning)
                                {
                                    animPlayer.Play("ma_trned");
                                    stateOfMario = MarioState.sideFlipTurning;
                                }
                                else if (
                                    walkingStrength > .5
                                    && stateOfMario != MarioState.sideFlipTurning
                                )
                                {
                                    animPlayer.Play("ma_run2", -1, 2f);
                                    stateOfMario = MarioState.sprinting;
                                    RotateArmature();
                                    RightHandMaterial.AlbedoColor = new Color(0, 0, 0, 0);
                                    LeftHandMaterial.AlbedoColor = new Color(0, 0, 0, 0);
                                    LeftClosedHand.Visible = true;
                                    RightClosedHand.Visible = true;
                                }
                                else
                                {
                                    animPlayer.Play("ma_run1");
                                    stateOfMario = MarioState.running;
                                    RotateArmature();
                                }
                                if (
                                    stateHistory.Contains(MarioState.singleJump)
                                    || stateHistory.Contains(MarioState.doubleJump)
                                    || stateHistory.Contains(MarioState.sideFlip)
                                    || stateHistory.Contains(MarioState.SpinJump)
                                )
                                {
                                    animPlayer.Play("ma_laend");
                                    stateOfMario = MarioState.landing;
                                    landingParticles.Emitting = true;
                                }
                            }
                            else
                            {
                                RotateArmature();

                                if (stateOfMario == MarioState.sideFlipTurning)
                                {
                                    //play Animation
                                }
                                else
                                {
                                    if (
                                        (
                                            stateHistory.Contains(MarioState.singleJump)
                                            || stateHistory.Contains(MarioState.tripleJump)
                                            || stateHistory.Contains(MarioState.bellyRollout)
                                            || stateHistory.Contains(MarioState.sideFlip)
                                            || stateHistory.Contains(MarioState.SpinJump)
                                        )
                                        && direction == Vector3.Zero
                                    )
                                    {
                                        animPlayer.Play("ma_laend");
                                        stateOfMario = MarioState.landing;
                                        landingParticles.Emitting = true;
                                    }
                                    else if (stateHistory.Contains(MarioState.doubleJump))
                                    {
                                        animPlayer.Play("ma_2jmed");
                                        stateOfMario = MarioState.landing;
                                    }
                                    else if (direction == Vector3.Zero)
                                    {
                                        stateOfMario = MarioState.idle;
                                    }
                                    if (stateOfMario == MarioState.idle)
                                    {
                                        if (idleTimer == 0)
                                        {
                                            if (sittingAnimationTimer == 0)
                                            {
                                                if (sittingTimerWait == 0)
                                                {
                                                    if (sleepingTimer == 0)
                                                    {
                                                        animPlayer.Play("ma_sleep_wait");
                                                        if (sleeptimer == 0)
                                                        {
                                                            //spawn in z every few seconds
                                                            zEffectSpawner.StartZEffect();
                                                            // Apply the updated material back to Surface 7

                                                            sleeptimer = 150;
                                                        }
                                                        else
                                                        {
                                                            sleeptimer--;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        sleepingTimer--;
                                                        animPlayer.Play("ma_sleep");
                                                    }
                                                }
                                                else
                                                {
                                                    sittingTimerWait--;
                                                    animPlayer.Play("ma_sit_wait");
                                                    SleepStatus(true);
                                                }
                                            }
                                            else
                                            {
                                                sittingAnimationTimer--;
                                                animPlayer.Play("ma_sit");
                                            }
                                        }
                                        else
                                        {
                                            idleTimer--;
                                            if (animPlayer.CurrentAnimation != ("ma_laend"))
                                            {
                                                animPlayer.Play("ma_wait");
                                            }
                                        }
                                    }
                                }
                            }

                            if (
                                Input.IsActionPressed("key_space")
                                || Input.IsActionPressed("button_a")
                            )
                            {
                                isJumping = true;
                                if (spinInput)
                                {
                                    stateOfMario = MarioState.SpinJump;
                                    velocity.Y = TRIPLE_JUMP_VELOCITY;
                                    animPlayer.Play("ma_spin_p");
                                }
                                else if (stateOfMario == MarioState.sideFlipTurning)
                                {
                                    //need somthing that update last control stick direction
                                    RotateArmature();
                                    stateOfMario = MarioState.sideFlip;
                                    velocity.Y = TRIPLE_JUMP_VELOCITY;
                                    animPlayer.Play("ma_tjmp1");
                                }
                                else if (
                                    stateHistory.Contains(MarioState.doubleJump)
                                    || stateHistory.Contains(MarioState.SpinJump)
                                )
                                {
                                    velocity.Y = TRIPLE_JUMP_VELOCITY;
                                    stateOfMario = MarioState.tripleJump;
                                    animPlayer.Play("ma_demo_gate_out_rolling_get");
                                }
                                else if (stateHistory.Contains(MarioState.singleJump))
                                {
                                    //velocity.Y = BASE_JUMP_VELOCITY;
                                    stateOfMario = MarioState.landing;
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
        }
        else // Airborne
        {
            if (
                spinInput
                && (
                    stateOfMario != MarioState.singleRollout
                    && stateOfMario != MarioState.tripleJump
                    && stateOfMario != MarioState.tripleJumpDive
                    && stateOfMario != MarioState.singleJumpDive
                    && stateOfMario != MarioState.doubleJumpDive
                    && stateOfMario != MarioState.singleRollout
                    && stateOfMario != MarioState.diving
                )
            )
            {
                stateOfMario = MarioState.SpinJump;
                animPlayer.Play("ma_spin_p");
                isSpining(true);
            }
            else
            {
                isSpining(false);
            }
            if (stateOfMario == MarioState.SpinJump)
            {
                RotateSpinJump(delta);
                spinInput = false;
                isSpining(true);
            }
            sideFlipTurningTimer = 33;
            Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
            if (stateOfMario == MarioState.bellyRollout)
            {
                bellyRolloutTimer--;
                if (bellyRolloutTimer == 0)
                {
                    animPlayer.Play("ma_land");
                }
            }
            if (Input.IsActionJustReleased("button_a"))
            {
                initalJumpHold = false;
            }

            if (
                isJumping
                && (Input.IsActionPressed("key_space") || Input.IsActionPressed("button_a"))
                && stateOfMario != MarioState.sideFlip
            )
            {
                jumpHoldTime += (float)delta;
                if (initalJumpHold)
                {
                    if (
                        stateOfMario == MarioState.singleJump
                        || stateOfMario == MarioState.singleJumpDive
                    )
                    {
                        if (initalJumpHold)
                        {
                            if (jumpHoldTime < MAX_JUMP_HOLD_TIME_SINGLE)
                            {
                                velocity.Y =
                                    BASE_JUMP_VELOCITY
                                    + (MAX_JUMP_VELOCITY_SINGLE - BASE_JUMP_VELOCITY)
                                        * (jumpHoldTime / MAX_JUMP_HOLD_TIME_SINGLE)
                                    + (walkingStrength * 7);
                            }
                        }

                        if (
                            Input.IsActionJustPressed("button_b")
                            && Input.IsActionPressed("button_a")
                        )
                        {
                            animPlayer.Play("ma_sldct");
                            stateOfMario = MarioState.singleJumpDive;
                            // Store the facing direction at the moment of the dive
                            Vector3 diveDirection = lastFacingDirection.Normalized();

                            velocity.X = diveDirection.X * 50;
                            velocity.Z = diveDirection.Z * 50;
                        }
                    }
                    else if (
                        stateOfMario == MarioState.doubleJump
                        || stateOfMario == MarioState.doubleJumpDive
                    )
                    {
                        jumpHoldTime += (float)delta;
                        if (jumpHoldTime < MAX_JUMP_HOLD_TIME_DOUBLE)
                        {
                            velocity.Y =
                                BASE_JUMP_VELOCITY
                                + (MAX_JUMP_VELOCITY_DOUBLE - BASE_JUMP_VELOCITY)
                                    * (jumpHoldTime / MAX_JUMP_HOLD_TIME_DOUBLE)
                                + (walkingStrength * 7);
                        }
                        if (
                            Input.IsActionJustPressed("button_b")
                            && Input.IsActionPressed("button_a")
                        )
                        {
                            animPlayer.Play("ma_sldct");
                            stateOfMario = MarioState.doubleJumpDive;
                            Vector3 diveDirection = lastFacingDirection.Normalized();

                            velocity.X = diveDirection.X * 50;
                            velocity.Z = diveDirection.Z * 50;
                        }
                    }
                }
            }
            if (Input.IsActionPressed("button_b") && stateOfMario == MarioState.tripleJump)
            {
                //RotateArmature();
                animPlayer.Play("ma_sldct");
                stateOfMario = MarioState.tripleJumpDive;
                Vector3 diveDirection = lastFacingDirection.Normalized();

                velocity.X = diveDirection.X * 50;
                velocity.Z = diveDirection.Z * 50;
            }

            if (stateOfMario == MarioState.singleRollout && Input.IsActionPressed("button_a"))
            {
                rolloutAction(delta);
            }
            else if (
                (stateOfMario == MarioState.sideFlip || stateOfMario == MarioState.SpinJump)
                && Input.IsActionPressed("button_b")
            )
            {
                spinInput = false;
                stateOfMario = MarioState.diving;
                RotateArmature();
                animPlayer.Play("ma_sldct");
                Vector3 diveDirection = lastFacingDirection.Normalized();
                velocity.X = diveDirection.X * 50;
                velocity.Z = diveDirection.Z * 50;
            }
            else if (Input.IsActionPressed("button_b"))
            {
                if (stateOfMario != MarioState.diving)
                {
                    if (direction.IsZeroApprox())
                    {
                        stateOfMario = MarioState.diving;
                        animPlayer.Play("ma_sldct");
                        Vector3 diveDirection = lastFacingDirection.Normalized();
                        velocity.X = diveDirection.X * 50;
                        velocity.Z = diveDirection.Z * 50;
                    }
                    else if (
                        stateOfMario == MarioState.singleJump
                        || stateOfMario == MarioState.doubleJump
                        || stateOfMario == MarioState.tripleJump
                    )
                    {
                        animPlayer.Play("ma_sldct");
                        stateOfMario = MarioState.diving;
                        Vector3 diveDirection = lastFacingDirection.Normalized();

                        velocity.X = diveDirection.X * 50;
                        velocity.Z = diveDirection.Z * 50;
                    }
                }
            }
            else if (velXZ.Length() > RUN_SPEED)
            {
                velocity.X = Mathf.Lerp(velocity.X, direction.X * ROLL_SPEED, .01f);
                velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * ROLL_SPEED, .01f);
            }
            else
            {
                velocity.X = Mathf.Lerp(velocity.X, direction.X * RUN_SPEED, .01f);
                velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * RUN_SPEED, .01f);
            }
        }

        // Camera controller match the position of Mario
        stateHistory.Add(stateOfMario);
        Last4FacingDirections.Add(lastFacingDirection);
        Velocity = velocity;
        MoveAndSlide();
        velocity = Velocity;
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

        camControl.Position = camControl.Position with
        {
            X = (float)Mathf.Lerp(camControl.Position.X, Position.X * 0.03f, delta * 12),
        };
        camControl.Position = camControl.Position with
        {
            Y = (float)Mathf.Lerp(camControl.Position.Y, Position.Y * 0.03f, delta * 4),
        };
        camControl.Position = camControl.Position with
        {
            Z = (float)Mathf.Lerp(camControl.Position.Z, Position.Z * 0.03f, delta * 12),
        };

        springArmPivot.RotateY(-RstickVec.X * (float)delta * 5); //left-right HAPPENS ON THE PARENT NODE
        springArm.RotateX(-RstickVec.Y * (float)delta * 5); //up-down
        //handle movement influence
        if (Velocity.Length() > .1)
        {
            //this will handle any influce the cam needs
        }
        springArm.Rotation = springArm.Rotation with
        {
            X = Mathf.Clamp(
                springArm.Rotation.X,
                float.DegreesToRadians(-75),
                float.DegreesToRadians(45)
            ),
        }; //downward limit, upward limit
    }

    private Vector2 playerAimPoint()
    {
        float width = DisplayServer.WindowGetSize().X;
        float height = DisplayServer.WindowGetSize().Y;
        return new Vector2(width / 2, height * 1f);
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

    private void SleepStatus(bool isSleeping)
    {
        if (isSleeping)
        {
            leftEyeMaterial.AlbedoTexture = sleepingEyeTexture;
            rightEyeMaterial.AlbedoTexture = sleepingEyeTexture;
        }
        else
        {
            leftEyeMaterial.AlbedoTexture = awakeEyeTexture;
            rightEyeMaterial.AlbedoTexture = awakeEyeTexture;
            //Usally when this is called all timers should be reset for sleeping
            sittingAnimationTimer = 58;
            sleepingTimer = 28;
            idleTimer = 180;
            sittingTimerWait = 180;
            sleeptimer = 100;
            zEffectSpawner.StopZEffect();
        }
    }

    private void rolloutAction(double delta)
    {
        if (initalJumpHold)
        {
            jumpHoldTime += (float)delta;
            if (jumpHoldTime < MAX_ROLLOUT_HOLD_TIME)
            {
                //this will take in the velocity the roll out uses
                velocity.Y =
                    BASE_ROLLOUT_VELOCITY
                    + (MAX_ROLLOUT_VELOCITY - BASE_ROLLOUT_VELOCITY)
                        * (jumpHoldTime / MAX_ROLLOUT_VELOCITY);
            }
            Vector3 diveDirection = lastFacingDirection.Normalized();

            velocity.X = diveDirection.X * 50;
            velocity.Z = diveDirection.Z * 50;
        }
    }

    /**
        This method checks to see if Player is in withen a certain speed to do a roll out from belly slide
    **/
    private bool checkSpeedForBellyRoll(Vector3 velocity)
    {
        if ((velocity.X > -10) && (velocity.X < 10) && (velocity.Z < 10) && (velocity.Z > -10))
        {
            return true;
        }
        return false;
    }

    private void setupHandSwaping()
    {
        //setting up the nodes for hands
        RightClosedHand = GetNode<Node3D>("Armature/Skeleton3D/RightHandBone/RightHandClosed");
        LeftClosedHand = GetNode<Node3D>("Armature/Skeleton3D/LeftHandBone/LeftHandClosed");
        marioMesh = GetNode<MeshInstance3D>("Armature/Skeleton3D/Mesh_0");
        MeshInstance3D RightHandMeshClosed = GetNode<MeshInstance3D>(
            "Armature/Skeleton3D/RightHandBone/RightHandClosed/ma_hnd3r_armature/Skeleton3D/ma_hnd3r"
        );

        int RightHandSurface = 4; // Adjust based on debug output
        int LeftHandSurface = 5; // Adjust based on debug output

        int HandSwapSurface = 0;

        Material originalLeftMaterial =
            marioMesh.GetSurfaceOverrideMaterial(LeftHandSurface)
            ?? marioMesh.Mesh.SurfaceGetMaterial(LeftHandSurface);

        if (originalLeftMaterial is StandardMaterial3D)
        {
            LeftHandMaterial = (StandardMaterial3D)originalLeftMaterial.Duplicate();
            marioMesh.SetSurfaceOverrideMaterial(LeftHandSurface, LeftHandMaterial);
        }
        else
        {
            GD.PrintErr("ERROR: Left Hand material not found or invalid!");
        }

        // Right Hand Material
        Material originalRightMaterial =
            marioMesh.GetSurfaceOverrideMaterial(RightHandSurface)
            ?? marioMesh.Mesh.SurfaceGetMaterial(RightHandSurface);

        if (originalRightMaterial is StandardMaterial3D)
        {
            RightHandMaterial = (StandardMaterial3D)originalRightMaterial.Duplicate();
            marioMesh.SetSurfaceOverrideMaterial(RightHandSurface, RightHandMaterial);
        }
        else
        {
            GD.PrintErr("ERROR: Right Hand material not found or invalid!");
        }

        RightHandClosedMaterial = (StandardMaterial3D)originalRightMaterial.Duplicate();

        RightHandMeshClosed.SetSurfaceOverrideMaterial(0, RightHandClosedMaterial);

        RightHandMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        LeftHandMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        GD.Print("Hand materials successfully duplicated and assigned.");
    }

    /**
        This Sets up anything that involved with sleeping
        also helps setup any replaceing textures involving the eyes
    **/
    private void SetupSleeping()
    {
        //sleeping effects
        zEffectSpawner = GetNode<ZEffectSpawner>("ZEffectSpawner");
        // Load the textures from your files (update the paths)
        awakeEyeTexture = (Texture2D)
            ResourceLoader.Load("res://models/Mario/mario/bmd/ma_mdl1/H_ma_eye1_s3tc.png");
        sleepingEyeTexture = (Texture2D)
            ResourceLoader.Load("res://models/Mario/mario/bmd/ma_mdl1/H_ma_eye1_s3tc_shut.png");

        // Get Mario's main mesh
        marioMesh = GetNode<MeshInstance3D>("Armature/Skeleton3D/Mesh_0");
        if (marioMesh == null)
        {
            GD.PrintErr("ERROR: Mario mesh not found! Check the node path.");
            return;
        }

        int leftEyeSurface = 7; // Adjust based on debug output
        int rightEyeSurface = 8; // Adjust based on debug output

        // Left Eye Material
        Material originalLeftMaterial =
            marioMesh.GetSurfaceOverrideMaterial(leftEyeSurface)
            ?? marioMesh.Mesh.SurfaceGetMaterial(leftEyeSurface);

        if (originalLeftMaterial is StandardMaterial3D)
        {
            leftEyeMaterial = (StandardMaterial3D)originalLeftMaterial.Duplicate();
            marioMesh.SetSurfaceOverrideMaterial(leftEyeSurface, leftEyeMaterial);
        }
        else
        {
            GD.PrintErr("ERROR: Left eye material not found or invalid!");
        }

        // Right Eye Material
        Material originalRightMaterial =
            marioMesh.GetSurfaceOverrideMaterial(rightEyeSurface)
            ?? marioMesh.Mesh.SurfaceGetMaterial(rightEyeSurface);

        if (originalRightMaterial is StandardMaterial3D)
        {
            rightEyeMaterial = (StandardMaterial3D)originalRightMaterial.Duplicate();
            marioMesh.SetSurfaceOverrideMaterial(rightEyeSurface, rightEyeMaterial);
        }
        else
        {
            GD.PrintErr("ERROR: Right eye material not found or invalid!");
        }

        GD.Print("Eye materials successfully duplicated and assigned.");
    }

    private void RotateSpinJump(double delta)
    {
        if (stateOfMario == MarioState.SpinJump)
        {
            float degreesPerSecond = 2000f;
            float angleThisFrame = degreesPerSecond * (float)delta * (Mathf.Pi / 180.0f);
            armature.RotateObjectLocal(Vector3.Up, angleThisFrame);
        }
    }

    private void RotateArmature()
    {
        if (stateOfMario == MarioState.sideFlipTurning)
        {
            sideFlipTurningTimer--;
            //Start side flipping timer to then get him out of this state
            if (sideFlipTurningTimer == 0)
            {
                sideFlipTurningTimer = 33;
                if (walkingStrength > .5)
                {
                    stateOfMario = MarioState.sprinting;
                    armature.Rotation = armature.Rotation with
                    {
                        Y = (float)(Mathf.Atan2(-velocity.X, -velocity.Z) + Math.PI),
                    };
                }
                else
                {
                    stateOfMario = MarioState.walking;
                    armature.Rotation = armature.Rotation with
                    {
                        Y = (float)(Mathf.Atan2(-velocity.X, -velocity.Z) + Math.PI),
                    };
                }
            }
            if (sideFlipTurningTimer != 0)
            {
                armature.Rotation = armature.Rotation with
                {
                    Y = (float)(Mathf.Atan2(-velocity.X, -velocity.Z) + Math.PI),
                };
            }
        }
        else if (stateOfMario == MarioState.sideFlip)
        {
            armature.Rotation = armature.Rotation with
            {
                Y = (float)(Mathf.Atan2(-velocity.X, -velocity.Z) + Math.PI),
            };
        }
        else if (stateOfMario == MarioState.diving)
        {
            armature.Rotation = armature.Rotation with
            {
                Y = Mathf.Atan2(-velocity.X, -velocity.Z),
            };
        }
        else
        {
            float speedSq = velocity.X * velocity.X + velocity.Z * velocity.Z;
            float minSpeedSq = 0.001f;
            if (speedSq > minSpeedSq)
            {
                armature.Rotation = armature.Rotation with
                {
                    Y = Mathf.Atan2(-velocity.X, -velocity.Z),
                };
            }
        }
    }

    // Determine quadrant based on the sign of x and y.
    // Quadrant 0: Top-Right (x >= 0, y >= 0)
    // Quadrant 1: Top-Left  (x <  0, y >= 0)
    // Quadrant 2: Bottom-Left (x <  0, y <  0)
    // Quadrant 3: Bottom-Right(x >= 0, y <  0)
    private int GetQuadrant(Vector2 input)
    {
        if (input.X >= 0 && input.Y >= 0)
            return 0;
        else if (input.X < 0 && input.Y >= 0)
            return 1;
        else if (input.X < 0 && input.Y < 0)
            return 2;
        else if (input.X >= 0 && input.Y < 0)
            return 3;
        return -1; // Should never happen.
    }

    // Check if all quadrants have been visited.
    private bool AllQuadrantsVisited()
    {
        foreach (bool visited in quadrantVisited)
        {
            if (!visited)
                return false;
        }
        return true;
    }

    // Reset the 10-frame window and quadrant flags.
    private void ResetWindow()
    {
        frameCounter = 0;
        for (int i = 0; i < quadrantVisited.Length; i++)
        {
            quadrantVisited[i] = false;
        }
    }

    private void setupSpinJumpEffects()
    {
        SpinJumpEffects = GetNode<Node3D>("SpinJumpEffects");
        blueSpinEffects = GetNode<Node3D>("SpinJumpEffects")
            .GetNode<GpuParticles3D>("BlueTrailEffect");
        whiteSpinEffects = GetNode<Node3D>("SpinJumpEffects")
            .GetNode<GpuParticles3D>("WhiteTrailEffect");
        redSpinEffects = GetNode<Node3D>("SpinJumpEffects")
            .GetNode<GpuParticles3D>("RedTrailEffect");
    }

    private void isSpining(bool isSpining)
    {
        if (isSpining)
        {
            SpinJumpEffects.Visible = true;
            blueSpinEffects.Emitting = true;
            whiteSpinEffects.Emitting = true;
            redSpinEffects.Emitting = true;
        }
        else
        {
            SpinJumpEffects.Visible = false;
            blueSpinEffects.Emitting = false;
            whiteSpinEffects.Emitting = false;
            redSpinEffects.Emitting = false;
        }
    }
}
