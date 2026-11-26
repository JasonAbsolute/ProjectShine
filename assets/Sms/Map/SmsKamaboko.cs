using Godot;

/// <summary>
/// The Shell's Secret sliding board. TNormalLift first approaches the nearest rail node at its
/// default 1 unit/tick, then follows the chain at each destination node's yaw speed, waits at
/// endpoint flag 0x800, and reverses at dead ends.
/// </summary>
public partial class SmsKamaboko : AnimatableBody3D
{
    /// <summary>Stops after the nearest rail node, as offsets from the authored placement in metres.</summary>
    [Export] public Vector3[] Waypoints = System.Array.Empty<Vector3>();
    /// <summary>Travel speed after reaching the rail. This course uses 3.5 units/tick.</summary>
    [Export] public float MoveSpeed = 5.642128f;
    /// <summary>Waits for each Waypoints stop in 120 Hz movement ticks.</summary>
    [Export] public int[] WaitTicks = System.Array.Empty<int>();
    /// <summary>Wait at the nearest rail node in 120 Hz movement ticks.</summary>
    [Export] public int HomeWaitTicks = 20;
    /// <summary>Cycles the listed route. Used for the board that starts at the middle node.</summary>
    [Export] public bool Loop = false;
    /// <summary>The game's movement tick rate.</summary>
    [Export] public float TicksPerSecond = 120f;
    /// <summary>Disables movement while retaining the authored placement.</summary>
    [Export] public bool Active = true;

    // All three rico_ex1 boards are authored 50 game units above their nearest rail node.
    // TRailMapObj starts with unk144 = 1.0, so this first approach is slower than rail travel.
    [Export] public Vector3 InitialRailOffset = new Vector3(0f, -50f / 74.44f, 0f);
    [Export] public float InitialMoveSpeed = 120f / 74.44f;

    private Vector3 _authoredPosition;
    private Vector3 _railStart;
    private int _target;
    private int _direction = 1;
    private float _wait;
    private bool _initialApproach = true;

    public override void _Ready()
    {
        _authoredPosition = Position;
        _railStart = _authoredPosition + InitialRailOffset;
        SyncToPhysics = true;
    }

    private Vector3 Stop(int index) => index == 0 ? _railStart : _authoredPosition + Waypoints[index - 1];

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Active || Waypoints.Length == 0)
            return;

        float dt = (float)delta;
        if (_wait > 0f)
        {
            _wait = Mathf.Max(0f, _wait - dt);
            return;
        }

        if (_initialApproach)
        {
            if (MoveTo(Stop(0), InitialMoveSpeed, dt))
            {
                _initialApproach = false;
                _target = 0;
                SetWait(HomeWaitTicks);
            }
            return;
        }

        int stopCount = Waypoints.Length + 1;
        int next;
        if (Loop)
        {
            next = (_target + 1) % stopCount;
        }
        else
        {
            next = _target + _direction;
            if (next < 0 || next >= stopCount)
            {
                _direction = -_direction;
                next = _target + _direction;
            }
        }

        if (MoveTo(Stop(next), MoveSpeed, dt))
        {
            _target = next;
            int waitTicks = _target == 0
                ? HomeWaitTicks
                : (_target - 1 < WaitTicks.Length ? WaitTicks[_target - 1] : 0);
            SetWait(waitTicks);
        }
    }

    private bool MoveTo(Vector3 target, float speed, float delta)
    {
        Vector3 travel = target - Position;
        float step = Mathf.Max(0f, speed) * delta;
        if (travel.Length() > step)
        {
            Position += travel.Normalized() * step;
            return false;
        }
        Position = target;
        return true;
    }

    private void SetWait(int ticks)
    {
        if (ticks > 0)
            _wait = ticks / Mathf.Max(1f, TicksPerSecond);
    }
}
