using System;
using Godot;

/// <summary>
/// The rideable cloud from Super Mario Sunshine's TRideCloud (MapObjCloud.cpp). It compresses by a Hermite curve while Mario stands on it,
/// expands across X and Z when water hits it, and follows a rail when one is supplied. The game's control() runs at 120 movement ticks a second;
/// this script accumulates those ticks inside Godot's physics frames. The cloud_wait model animation is authored separately at 60 frames a second.
/// </summary>
public partial class SmsRideCloud : AnimatableBody3D, ISmsSprayTarget
{
    /// <summary>The Area3D just above the cloud that detects a grounded player.</summary>
    [Export] public NodePath RiderAreaPath = "BckScale/RiderDetector";

    /// <summary>The imported cloud model. Its cloud_wait animation is started when an AnimationPlayer is available.</summary>
    [Export] public NodePath ModelPath = "BckScale/ModelScale/Visual";

    /// <summary>Movement ticks per second. TMarDirector runs the movement list four times per 30 Hz frame.</summary>
    [Export] public float TicksPerSecond = 120f;

    /// <summary>Game units in one ProjectShine metre.</summary>
    [Export] public float UnitsPerMetre = 74.44f;

    /// <summary>TRideCloud::mMoveSpeedDefault, in game units per movement tick.</summary>
    [Export] public float MoveSpeedDefault = 2f;

    /// <summary>TRideCloud::mCushionLen, the full downward compression in game units.</summary>
    [Export] public float CushionLength = 80f;

    /// <summary>TRideCloud::mCushionSpeed, added to the 0..1 compression amount each movement tick while ridden.</summary>
    [Export] public float CushionSpeedPerTick = 0.015f;

    /// <summary>TRideCloud::mScaleSpeed, added to X and Z scale each movement tick while the water counter is positive.</summary>
    [Export] public float WaterScaleSpeedPerTick = 0.005f;

    /// <summary>touchWater's ten movement ticks added by one water drop.</summary>
    [Export] public int WaterTicksPerDrop = 10;

    /// <summary>touchWater's cap for the water counter: 600 movement ticks, or five seconds.</summary>
    [Export] public int MaximumWaterTicks = 600;

    /// <summary>The largest X/Z multiplier allowed by TRideCloud::control.</summary>
    [Export] public float MaximumWaterScale = 3f;

    /// <summary>The first TEV register colour loaded after the rail name. coro_ex0 stores white.</summary>
    [Export] public Color TevColor0 = Colors.White;

    /// <summary>The second TEV register colour loaded after the rail name. coro_ex0 stores 160 gray.</summary>
    [Export] public Color TevColor1 = new(160f / 255f, 160f / 255f, 160f / 255f, 1f);

    /// <summary>Optional scene.ral nodes in game units. Empty is the stage's actual setting for all four Dirty Lake clouds.</summary>
    [Export] public Vector3[] RailNodes = Array.Empty<Vector3>();

    /// <summary>Optional speed for the segment arriving at each rail node, in game units per movement tick. Zero uses MoveSpeedDefault.</summary>
    [Export] public float[] RailSpeeds = Array.Empty<float>();

    /// <summary>Optional wait at each rail node in movement ticks, from a node carrying flag 0x800.</summary>
    [Export] public int[] RailWaitTicks = Array.Empty<int>();

    /// <summary>Whether the optional rail joins its last node back to its first.</summary>
    [Export] public bool ClosedLoop = false;

    /// <summary>The optional rail node nearest the authored placement.</summary>
    [Export] public int StartNode = 0;

    private Area3D _riderArea;
    private Vector3 _authoredScale;
    private Vector3 _railPosition;
    private float _tickRemainder;
    private float _cushion;
    private float _waterScale = 1f;
    private int _waterTicks;
    private int _railWait;
    private int _railCurrent;
    private int _railNext;
    private int _railDirection = 1;

    public override void _Ready()
    {
        _riderArea = GetNodeOrNull<Area3D>(RiderAreaPath);
        if (_riderArea == null)
            GD.PushError($"{Name}: SmsRideCloud cannot find RiderAreaPath '{RiderAreaPath}'.");
        if (GetNodeOrNull<Node3D>(ModelPath) == null)
            GD.PushError($"{Name}: SmsRideCloud cannot find ModelPath '{ModelPath}'.");

        _authoredScale = Scale;
        _railPosition = Position;
        SyncToPhysics = true;

        if (RailNodes.Length >= 2)
        {
            _railCurrent = Math.Clamp(StartNode, 0, RailNodes.Length - 1);
            _railNext = _railCurrent + 1 < RailNodes.Length ? _railCurrent + 1 : _railCurrent - 1;
            _railDirection = _railNext > _railCurrent ? 1 : -1;
            _railPosition = RailNodes[_railCurrent] / UnitsPerMetre;
            Position = _railPosition;
        }

        var player = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (player != null && player.HasAnimation("cloud_wait"))
            player.Play("cloud_wait");
    }

    /// <summary>TRideCloud::touchWater: one drop sustains growth for ten ticks, up to the 600-tick cap, and the drop is consumed.</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        _waterTicks = Math.Min(MaximumWaterTicks, _waterTicks + WaterTicksPerDrop);
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        _tickRemainder += (float)delta * TicksPerSecond;
        int owed = Mathf.FloorToInt(_tickRemainder);
        _tickRemainder -= owed;
        int ticks = Math.Min(240, owed);
        for (int i = 0; i < ticks; i++)
            Tick();
        ApplyPose();
    }

    /// <summary>TRideCloud::control, one 120 Hz movement tick.</summary>
    private void Tick()
    {
        bool ridden = false;
        if (_riderArea != null)
        {
            foreach (Node3D body in _riderArea.GetOverlappingBodies())
            {
                if (body.IsInGroup("player"))
                {
                    ridden = true;
                    break;
                }
            }
        }

        _cushion = Mathf.Clamp(_cushion + (ridden ? CushionSpeedPerTick : -CushionSpeedPerTick), 0f, 1f);
        if (_waterTicks > 0)
        {
            _waterTicks--;
            _waterScale = Mathf.Min(MaximumWaterScale, _waterScale + WaterScaleSpeedPerTick);
        }
        else
        {
            _waterScale = Mathf.Max(1f, _waterScale - WaterScaleSpeedPerTick);
        }

        TickRail();
    }

    /// <summary>TRailMapObj::moveToNextNode as used by TRideCloud::control. Dirty Lake leaves each cloud's rail name null.</summary>
    private void TickRail()
    {
        if (RailNodes.Length < 2)
            return;
        if (_railWait > 0)
        {
            _railWait--;
            return;
        }

        Vector3 target = RailNodes[_railNext] / UnitsPerMetre;
        float speedUnits = _railNext < RailSpeeds.Length && RailSpeeds[_railNext] > 0f ? RailSpeeds[_railNext] : MoveSpeedDefault;
        float step = speedUnits / UnitsPerMetre;
        Vector3 delta = target - _railPosition;
        if (delta.Length() <= step || delta.LengthSquared() < 1e-10f)
        {
            _railPosition = target;
            _railCurrent = _railNext;
            if (_railCurrent < RailWaitTicks.Length)
                _railWait = Math.Max(0, RailWaitTicks[_railCurrent]);
            if (ClosedLoop)
            {
                _railNext = (_railCurrent + _railDirection + RailNodes.Length) % RailNodes.Length;
            }
            else
            {
                int next = _railCurrent + _railDirection;
                if (next < 0 || next >= RailNodes.Length)
                {
                    _railDirection = -_railDirection;
                    next = _railCurrent + _railDirection;
                }
                _railNext = next;
            }
        }
        else
        {
            _railPosition += delta.Normalized() * step;
        }
    }

    private void ApplyPose()
    {
        // JMAHermiteInterpolation(t, 0, 0, 1, -CushionLength, 0) is -L * (3t^2 - 2t^3).
        float smooth = _cushion * _cushion * (3f - 2f * _cushion);
        Position = _railPosition + Vector3.Up * (-CushionLength * smooth / UnitsPerMetre);
        Scale = new Vector3(_authoredScale.X * _waterScale, _authoredScale.Y, _authoredScale.Z * _waterScale);
    }
}
