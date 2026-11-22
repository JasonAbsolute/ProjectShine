using System;
using Godot;

/// <summary>
/// The long sliding Uirou platform from sirena_ex0. The game creates Uirou as TNormalLift
/// (MarNameRefGen_MapObj.cpp) and TNormalLift::control follows its scene.ral chain at the
/// node yaw times 0.01 game units per 120 Hz movement tick (MapObjRailBlock.cpp).
/// </summary>
public partial class SmsUirou : AnimatableBody3D
{
    /// <summary>The complete scene.ral chain in game units, in the level root's coordinate frame.</summary>
    [Export] public Vector3[] RailNodes = Array.Empty<Vector3>();

    /// <summary>The rail node occupied when the object loads; the nearest connected node is chosen first.</summary>
    [Export] public int StartNode = 0;

    /// <summary>Movement speed in game units per movement tick. sirena_ex0 stores yaw 250, which means 2.5 units per tick.</summary>
    [Export] public float SpeedUnitsPerTick = 2.5f;

    /// <summary>Game units in one ProjectShine metre.</summary>
    [Export] public float UnitsPerMetre = 74.44f;

    /// <summary>Game movement ticks per second. TMarDirector runs map-object control at 120 Hz.</summary>
    [Export] public float TicksPerSecond = 120f;

    /// <summary>Optional rest at each rail node in game ticks. The four sirena_ex0 Uirou rails use zero.</summary>
    [Export] public int WaitTicks = 0;

    /// <summary>Allows a level author to pause this platform without changing its rail.</summary>
    [Export] public bool Active = true;

    private int _previous = -1;
    private int _current;
    private int _next;
    private float _waitSeconds;

    public override void _Ready()
    {
        SyncToPhysics = true;
        if (RailNodes.Length < 2)
        {
            GD.PushError($"{Name}: SmsUirou needs at least two rail nodes.");
            SetPhysicsProcess(false);
            return;
        }
        if (UnitsPerMetre <= 0f || TicksPerSecond <= 0f || SpeedUnitsPerTick <= 0f)
        {
            GD.PushError($"{Name}: SmsUirou rail units and speed must be positive.");
            SetPhysicsProcess(false);
            return;
        }
        _current = Mathf.Clamp(StartNode, 0, RailNodes.Length - 1);
        Position = NodeMetres(_current);
        _next = PickNext(_current, _previous);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Active || RailNodes.Length < 2)
            return;
        if (_waitSeconds > 0f)
        {
            _waitSeconds -= (float)delta;
            return;
        }

        Vector3 target = NodeMetres(_next);
        Vector3 offset = target - Position;
        float step = SpeedUnitsPerTick * TicksPerSecond / UnitsPerMetre * (float)delta;
        if (offset.Length() <= step)
        {
            Position = target;
            int old = _current;
            _current = _next;
            _previous = old;
            _next = PickNext(_current, _previous);
            _waitSeconds = WaitTicks / TicksPerSecond;
        }
        else
        {
            Position += offset.Normalized() * step;
        }
    }

    private Vector3 NodeMetres(int index) => RailNodes[index] / UnitsPerMetre;

    // These Uirou rails are ordered chains. TGraphWeb::moveToShortestNext returns
    // the other connection; at load it chooses the nearest connection, lower index on a tie.
    private int PickNext(int current, int previous)
    {
        int left = current - 1;
        int right = current + 1;
        if (left < 0)
            return right;
        if (right >= RailNodes.Length)
            return left;
        if (previous == left)
            return right;
        if (previous == right)
            return left;
        float leftDistance = (RailNodes[left] - RailNodes[current]).LengthSquared();
        float rightDistance = (RailNodes[right] - RailNodes[current]).LengthSquared();
        return leftDistance <= rightDistance ? left : right;
    }
}
