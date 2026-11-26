using Godot;

/// <summary>
/// A Super Mario Sunshine sand block, ported from TSandBlock in MapObjBlock.cpp.
/// Standing on it starts a 40-tick delay. It then shrinks vertically by 0.0075
/// scale units per 120 Hz movement tick, disappears below 0.05 scale, waits 400
/// ticks, and grows back by 0.075 per tick after the player has moved away.
/// </summary>
public partial class SmsSandBlock : AnimatableBody3D
{
    /// <summary>The visual child hidden while TSandBlock is in STATE_GONE.</summary>
    [Export] public NodePath ModelPath = "Model";

    /// <summary>The solid shape removed while TSandBlock is in STATE_GONE.</summary>
    [Export] public NodePath CollisionPath = "CollisionShape3D";

    /// <summary>The group used to find the active player whose floor contact triggers this block.</summary>
    [Export] public StringName PlayerGroup = "player";

    /// <summary>Movement ticks per second. Super Mario Sunshine runs this object's control method at 120 Hz.</summary>
    [Export(PropertyHint.Range, "1,240,1")] public float TicksPerSecond = 120f;

    /// <summary>mWaitTimeToFall: ticks between the first floor contact and the start of collapse.</summary>
    [Export(PropertyHint.Range, "0,1200,1")] public int WaitTicks = 40;

    /// <summary>mSandScaleDown: vertical scale removed on every collapse tick.</summary>
    [Export(PropertyHint.Range, "0.0001,0.1,0.0001")] public float FallScalePerTick = 0.0075f;

    /// <summary>mSandScaleMin: vertical scale below which the block disappears.</summary>
    [Export(PropertyHint.Range, "0.001,0.5,0.001")] public float MinimumScale = 0.05f;

    /// <summary>mSandWaitTime: ticks spent gone before the block may return.</summary>
    [Export(PropertyHint.Range, "0,2400,1")] public int GoneTicks = 400;

    /// <summary>mSandScaleUp: scale restored on every return tick.</summary>
    [Export(PropertyHint.Range, "0.001,0.5,0.001")] public float RestoreScalePerTick = 0.075f;

    private enum SandState
    {
        Normal,
        Touched,
        Falling,
        Gone,
        Restoring,
    }

    private Node3D _model;
    private CollisionShape3D _collision;
    private CharacterBody3D _player;
    private Vector3 _initialScale;
    private Vector3 _currentScale;
    private SandState _state = SandState.Normal;
    private int _stateTicks;
    private float _tickAccumulator;

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>(ModelPath);
        _collision = GetNodeOrNull<CollisionShape3D>(CollisionPath);
        _initialScale = Scale;
        _currentScale = _initialScale;
        SyncToPhysics = true;

        if (_model == null)
            GD.PushError($"{Name}: SmsSandBlock cannot find ModelPath '{ModelPath}'.");
        if (_collision == null)
            GD.PushError($"{Name}: SmsSandBlock cannot find CollisionPath '{CollisionPath}'.");
    }

    private CharacterBody3D FindPlayer()
    {
        if (_player != null && IsInstanceValid(_player))
            return _player;
        _player = GetTree().GetFirstNodeInGroup(PlayerGroup) as CharacterBody3D;
        return _player;
    }

    private bool PlayerIsStandingOnThisBlock()
    {
        CharacterBody3D player = FindPlayer();
        if (player == null || !player.IsOnFloor())
            return false;

        for (int i = 0; i < player.GetSlideCollisionCount(); i++)
        {
            KinematicCollision3D hit = player.GetSlideCollision(i);
            if (hit.GetNormal().Y > 0.5f && hit.GetCollider() == this)
                return true;
        }
        return false;
    }

    private void SetGone(bool gone)
    {
        if (_model != null)
            _model.Visible = !gone;
        if (_collision != null)
            _collision.Disabled = gone;
    }

    private void ApplyScale()
    {
        Scale = _currentScale;
    }

    // TSandBlock::touchPlayer and TSandBlock::control, one step per SMS movement tick.
    private void StepTick()
    {
        switch (_state)
        {
            case SandState.Normal:
                if (PlayerIsStandingOnThisBlock())
                {
                    _stateTicks = WaitTicks;
                    _state = SandState.Touched;
                }
                break;

            case SandState.Touched:
                if (_stateTicks > 0)
                    _stateTicks--;
                if (_stateTicks <= 0)
                    _state = SandState.Falling;
                break;

            case SandState.Falling:
                _currentScale.Y -= FallScalePerTick;
                ApplyScale();
                if (_currentScale.Y < MinimumScale)
                {
                    _currentScale.X = _currentScale.Y;
                    _currentScale.Z = _currentScale.Y;
                    ApplyScale();
                    SetGone(true);
                    _stateTicks = GoneTicks;
                    _state = SandState.Gone;
                }
                break;

            case SandState.Gone:
                if (_stateTicks > 0)
                    _stateTicks--;
                if (_stateTicks <= 0 && PlayerIsFarEnoughToRestore())
                {
                    SetGone(false);
                    _state = SandState.Restoring;
                }
                break;

            case SandState.Restoring:
                _currentScale += Vector3.One * RestoreScalePerTick;
                ApplyScale();
                if (_currentScale.Y >= _initialScale.X)
                {
                    _currentScale = _initialScale;
                    ApplyScale();
                    _state = SandState.Normal;
                }
                break;
        }
    }

    private bool PlayerIsFarEnoughToRestore()
    {
        CharacterBody3D player = FindPlayer();
        if (player == null)
            return true;

        // TSandBlock compares game-unit distance with mScaling.x * 100.
        float thresholdMetres = Mathf.Abs(_currentScale.X) * 100f / 74.44f;
        return GlobalPosition.DistanceTo(player.GlobalPosition) > thresholdMetres;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        float tick = 1f / Mathf.Max(TicksPerSecond, 1f);
        _tickAccumulator += (float)delta;
        while (_tickAccumulator >= tick)
        {
            _tickAccumulator -= tick;
            StepTick();
        }
    }
}
