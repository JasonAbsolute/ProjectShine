using Godot;

public partial class Gooble : CharacterBody3D
{
    private enum GoobleState
    {
        Wandering,
        Chasing,
        Charging,
        Jumping,
        Landing,
        Dying,
    }

    [Export]
    public float MoveSpeed = 3.0f;

    [Export]
    public float ChaseSpeed = 5.0f;

    [Export]
    public float WanderDirChangeTime = 2.0f;

    [Export]
    public float RotationSpeed = 5.0f;

    [Export]
    public float ChargeTime = 1.09f;

    [Export]
    public float JumpForwardSpeed = 12.0f;

    [Export]
    public float JumpUpForce = 10.0f;

    [Export]
    public float LandingTime = 0.5f;

    private readonly Vector3 GRAVITY = new Vector3(0, -30f, 0);

    private GoobleState _state = GoobleState.Wandering;
    private Vector3 _wanderDirection = Vector3.Zero;
    private float _wanderTimer = 0f;
    private float _stateTimer = 0f;
    private Node3D _target = null;
    private Vector3 _attackDirection = Vector3.Zero;
    private bool _marioInAttackRange = false;
    private Area3D _detectionZone;
    private Area3D _attackZone;
    private Area3D _hurtbox;
    private Area3D _stompZone;
    private AnimationPlayer _animPlayer;

    public override void _Ready()
    {
        _detectionZone = GetNode<Area3D>("DetectionZone");
        _detectionZone.BodyEntered += OnDetectionBodyEntered;
        _detectionZone.BodyExited += OnDetectionBodyExited;

        _attackZone = GetNode<Area3D>("AttackZone");
        _attackZone.BodyEntered += OnAttackZoneBodyEntered;
        _attackZone.BodyExited += OnAttackZoneBodyExited;

        _hurtbox = GetNode<Area3D>("Hurtbox");
        _hurtbox.BodyEntered += OnHurtboxBodyEntered;

        _stompZone = GetNode<Area3D>("StompZone");
        _stompZone.BodyEntered += OnStompZoneBodyEntered;

        _animPlayer = GetNode<AnimationPlayer>("Model/AnimationPlayer");

        // Prevent Gooble and Mario from physically pushing each other
        // (Area3D detection still works independently)
        CallDeferred(nameof(SetupCollisionExceptions));
        _animPlayer.GetAnimation("name_walk").LoopMode = Animation.LoopModeEnum.Linear;
        _animPlayer.Play("name_walk");

        PickNewWanderDirection();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector3 velocity = Velocity;

        // Apply gravity
        if (!IsOnFloor())
            velocity += GRAVITY * dt;

        switch (_state)
        {
            case GoobleState.Wandering:
                velocity = ProcessWander(velocity, dt);
                break;
            case GoobleState.Chasing:
                velocity = ProcessChase(velocity, dt);
                break;
            case GoobleState.Charging:
                velocity = ProcessCharging(velocity, dt);
                break;
            case GoobleState.Jumping:
                velocity = ProcessJumping(velocity, dt);
                break;
            case GoobleState.Landing:
                velocity = ProcessLanding(velocity, dt);
                break;
            case GoobleState.Dying:
                velocity.X = 0;
                velocity.Z = 0;
                _stateTimer -= dt;
                if (_stateTimer <= 0f)
                    QueueFree();
                break;
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    private Vector3 ProcessWander(Vector3 velocity, float dt)
    {
        _wanderTimer -= dt;
        if (_wanderTimer <= 0f)
            PickNewWanderDirection();

        velocity.X = _wanderDirection.X * MoveSpeed;
        velocity.Z = _wanderDirection.Z * MoveSpeed;

        if (_wanderDirection != Vector3.Zero)
            FaceDirection(_wanderDirection, dt);

        return velocity;
    }

    private Vector3 ProcessChase(Vector3 velocity, float dt)
    {
        if (!IsInstanceValid(_target))
        {
            EnterState(GoobleState.Wandering);
            return velocity;
        }

        // If Mario is in attack range, start charging
        if (_marioInAttackRange)
        {
            EnterState(GoobleState.Charging);
            velocity.X = 0;
            velocity.Z = 0;
            return velocity;
        }

        Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
        toTarget.Y = 0;

        if (toTarget.LengthSquared() > 0.01f)
        {
            Vector3 dir = toTarget.Normalized();
            velocity.X = dir.X * ChaseSpeed;
            velocity.Z = dir.Z * ChaseSpeed;
            FaceDirection(dir, dt);
        }

        return velocity;
    }

    private Vector3 ProcessCharging(Vector3 velocity, float dt)
    {
        // Stand still while charging
        velocity.X = 0;
        velocity.Z = 0;

        // Face Mario during charge
        if (IsInstanceValid(_target))
        {
            Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
            toTarget.Y = 0;
            if (toTarget.LengthSquared() > 0.01f)
                FaceDirection(toTarget.Normalized(), dt);
        }

        _stateTimer -= dt;
        if (_stateTimer <= 0f)
        {
            // Lock direction at the moment of jump
            if (IsInstanceValid(_target))
            {
                _attackDirection = (_target.GlobalPosition - GlobalPosition);
                _attackDirection.Y = 0;
                _attackDirection = _attackDirection.Normalized();
            }

            EnterState(GoobleState.Jumping);
            velocity.X = _attackDirection.X * JumpForwardSpeed;
            velocity.Z = _attackDirection.Z * JumpForwardSpeed;
            velocity.Y = JumpUpForce;
        }

        return velocity;
    }

    private Vector3 ProcessJumping(Vector3 velocity, float dt)
    {
        // Keep forward momentum, gravity handles the arc
        // When we hit the floor, transition to landing
        if (IsOnFloor() && _stateTimer <= 0f)
        {
            EnterState(GoobleState.Landing);
            velocity.X = 0;
            velocity.Z = 0;
        }

        // Small buffer so we don't immediately land on the same frame
        _stateTimer -= dt;

        return velocity;
    }

    private Vector3 ProcessLanding(Vector3 velocity, float dt)
    {
        velocity.X = 0;
        velocity.Z = 0;

        _stateTimer -= dt;
        if (_stateTimer <= 0f)
        {
            // Return to chasing if Mario is still in detection range, otherwise wander
            if (_target != null && IsInstanceValid(_target))
                EnterState(GoobleState.Chasing);
            else
                EnterState(GoobleState.Wandering);
        }

        return velocity;
    }

    private void EnterState(GoobleState newState)
    {
        _state = newState;

        // Hurtbox only active during the jump attack
        _hurtbox.Monitoring = newState == GoobleState.Jumping;

        switch (newState)
        {
            case GoobleState.Wandering:
                _animPlayer.Play("name_walk");
                PickNewWanderDirection();
                break;
            case GoobleState.Chasing:
                _animPlayer.Play("name_walk");
                break;
            case GoobleState.Charging:
                _animPlayer.Play("name_jump_start");
                _stateTimer = ChargeTime;
                break;
            case GoobleState.Jumping:
                // Small buffer to avoid instant floor detection
                _stateTimer = 0.1f;
                break;
            case GoobleState.Landing:
                _animPlayer.Play("name_land");
                _stateTimer = LandingTime;
                break;
            case GoobleState.Dying:
                _animPlayer.Play("name_hit");
                var deathAnim = _animPlayer.GetAnimation("name_hit");
                _stateTimer = deathAnim != null ? (float)deathAnim.Length : 0.5f;
                break;
        }
    }

    private void SetupCollisionExceptions()
    {
        foreach (var node in GetTree().GetNodesInGroup("player"))
        {
            if (node is PhysicsBody3D body)
                AddCollisionExceptionWith(body);
        }
    }

    private void FaceDirection(Vector3 direction, float dt)
    {
        float targetAngle = Mathf.Atan2(direction.X, direction.Z);
        float currentAngle = Rotation.Y;
        Rotation = new Vector3(
            Rotation.X,
            Mathf.LerpAngle(currentAngle, targetAngle, RotationSpeed * dt),
            Rotation.Z
        );
    }

    private void PickNewWanderDirection()
    {
        float angle = (float)GD.RandRange(0, Mathf.Tau);
        _wanderDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
        _wanderTimer = (float)GD.RandRange(1.0, WanderDirChangeTime);
    }

    private void OnDetectionBodyEntered(Node3D body)
    {
        if (body is Mario)
        {
            _target = body;
            if (_state == GoobleState.Wandering)
                EnterState(GoobleState.Chasing);
        }
    }

    private void OnDetectionBodyExited(Node3D body)
    {
        if (body == _target)
        {
            _target = null;
            _marioInAttackRange = false;
            if (_state == GoobleState.Chasing)
                EnterState(GoobleState.Wandering);
        }
    }

    private void OnAttackZoneBodyEntered(Node3D body)
    {
        if (body is Mario)
            _marioInAttackRange = true;
    }

    private void OnAttackZoneBodyExited(Node3D body)
    {
        if (body is Mario)
            _marioInAttackRange = false;
    }

    private void OnHurtboxBodyEntered(Node3D body)
    {
        if (body is Mario mario)
        {
            mario.TakeDamage(GlobalPosition);
        }
    }

    private void OnStompZoneBodyEntered(Node3D body)
    {
        if (_state == GoobleState.Dying)
            return;

        if (body is Mario mario && mario.Velocity.Y < 0)
        {
            mario.StompBounce();
            EnterState(GoobleState.Dying);
        }
    }
}
