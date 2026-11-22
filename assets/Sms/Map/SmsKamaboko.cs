using Godot;

/// <summary>
/// The Shell's Secret sliding board. TNormalLift::control in MapObjRailBlock.cpp moves it along a three-node rail at the node's yaw times 0.01 game units per 120 Hz tick, waits for the endpoint pitch count, and reverses at a dead end.
/// </summary>
public partial class SmsKamaboko : AnimatableBody3D
{
    /// <summary>Stops after the authored start, as offsets in metres.</summary>
    [Export] public Vector3[] Waypoints = System.Array.Empty<Vector3>();
    /// <summary>Travel speed in metres per second. The course rails use 3.5 units per tick, or 5.642128 m/s.</summary>
    [Export] public float MoveSpeed = 5.642128f;
    /// <summary>Waits for each Waypoints stop in 120 Hz movement ticks; rico_ex1's rail endpoints use 20 and its middle uses 0.</summary>
    [Export] public int[] WaitTicks = System.Array.Empty<int>();
    /// <summary>Wait at the authored home stop in 120 Hz movement ticks.</summary>
    [Export] public int HomeWaitTicks = 20;
    /// <summary>Loops through the listed stop sequence. Used when a board begins at the middle node so both dead ends remain endpoints.</summary>
    [Export] public bool Loop = false;
    /// <summary>The game's movement tick rate.</summary>
    [Export] public float TicksPerSecond = 120f;
    /// <summary>Disables movement while retaining the authored placement.</summary>
    [Export] public bool Active = true;

    private Vector3 _home;
    private int _target = 0;
    private int _direction = 1;
    private float _wait;

    public override void _Ready()
    {
        _home = Position;
        SyncToPhysics = true;
    }

    private Vector3 Stop(int index) => index == 0 ? _home : _home + Waypoints[index - 1];

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Active || Waypoints.Length == 0) return;
        float dt = (float)delta;
        if (_wait > 0f) { _wait = Mathf.Max(0f, _wait - dt); return; }
        int stopCount = Waypoints.Length + 1;
        int next;
        if (Loop)
            next = (_target + 1) % stopCount;
        else
        {
            next = _target + _direction;
            if (next < 0 || next >= stopCount) { _direction = -_direction; next = _target + _direction; }
        }
        Vector3 target = Stop(next);
        Vector3 travel = target - Position;
        float step = MoveSpeed * dt;
        if (travel.Length() <= step)
        {
            Position = target;
            _target = next;
            int waitTicks = _target == 0 ? HomeWaitTicks : (_target - 1 < WaitTicks.Length ? WaitTicks[_target - 1] : 0);
            if (waitTicks > 0)
                _wait = waitTicks / Mathf.Max(1f, TicksPerSecond);
        }
        else
            Position += travel.Normalized() * step;
    }
}
