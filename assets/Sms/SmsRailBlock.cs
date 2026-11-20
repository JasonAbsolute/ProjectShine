using System;
using Godot;

/// <summary>
/// SMS 237: the game's rail block (TRailBlock, MapObjRailBlock.cpp) -- the secret courses' tumbling blocks and rolling cubes. The block walks its rail
/// node to node; each node carries the speed of the segment that reaches it (TRailNode mSpeed * 0.01 u per frame) and a turn (mPitch / mYaw / mRoll,
/// degrees) the block performs OVER that segment, spread evenly along it (TRailBlock::control: the angle difference over the segment's frames). The
/// turn is about the WORLD axes: calcRootMatrix is RotRPH(the step so far) * (the turns already made), so a rail that says pitch 90 at every node rolls
/// the block over and over about world X however it is standing (coro_ex2's railvariant1: 90 per 600 u on R1_1 and R1_2 -- R1_2's "270" is -90 the
/// short way, MsAngleDiff -- and 100 per 800 u on R1_3, which leaves it 10 degrees off square each pass). coro_ex1's EXRollCube rails are closed
/// rectangles: EXCube1 turns 34 then 56 at each corner, EXCube2 yaws 60 / 90 while it goes round, EXCube0 only slides.
/// The route: a dead end sends it back the way it came and a closed loop carries it on (TGraphWeb::getShortestNextIndex: one connection = that one; two
/// = the one it did not come from; at the start, with nowhere it came from, the NEAREST connected node, the lower index on a tie).
/// The game's units are the knobs: nodes in u (74.44 to the metre) in the parent's frame, speeds in u per frame (30 a second), turns in degrees.
/// </summary>
[Tool]
public partial class SmsRailBlock : AnimatableBody3D
{
    /// <summary>The rail's nodes (scene.ral), game units, in this node's parent's frame -- the block is set ON node StartNode at _Ready.</summary>
    [Export] public Vector3[] RailNodes = Array.Empty<Vector3>();

    /// <summary>Per node: the pitch (x), yaw (y) and roll (z) in degrees turned over the segment that ARRIVES at that node (TRailNode mPitch / mYaw / mRoll).</summary>
    [Export] public Vector3[] RailTurns = Array.Empty<Vector3>();

    /// <summary>Per node: u per frame for the segment that arrives at that node (TRailNode mSpeed * 0.01). 0 = the game's default 2.0.</summary>
    [Export] public float[] RailSpeeds = Array.Empty<float>();

    /// <summary>The last node connects back to the first (the EXCube rails); off = a chain whose ends turn the block round (the R1 rails).</summary>
    [Export] public bool ClosedLoop = false;

    /// <summary>The node the block stands on at load (the game: the node nearest its placement).</summary>
    [Export] public int StartNode = 0;

    /// <summary>The first node it walks to; -1 = the game's pick (the nearest connected node, the lower index on a tie).</summary>
    [Export] public int StartTowards = -1;

    [Export] public float UnitsPerMetre = 74.44f;
    [Export] public float FramesPerSecond = 30f;

    /// <summary>1 = the game's pace; a multiplier on every segment's speed.</summary>
    [Export] public float SpeedScale = 1f;

    [Export] public bool Active = true;

    private Basis _accum = Basis.Identity;   // the turns already made (the game's unk174), the placement's rotation inside it
    private Vector3 _scale = Vector3.One;    // the authored scale, kept out of the rotation
    private int _cur, _next, _dir = 1;
    private Vector3 _segStart, _segEnd, _turn;
    private float _segLen, _speed, _travelled;

    private Vector3 NodeMetres(int i) => RailNodes[i] / UnitsPerMetre;

    public override void _Ready()
    {
        _accum = Transform.Basis.Orthonormalized();
        _scale = Transform.Basis.Scale;
        SyncToPhysics = true;
        if (Engine.IsEditorHint() || RailNodes.Length < 2) return;
        _cur = Mathf.Clamp(StartNode, 0, RailNodes.Length - 1);
        Position = NodeMetres(_cur);
        _next = StartTowards >= 0 && StartTowards < RailNodes.Length && StartTowards != _cur ? StartTowards : NearestConnected(_cur);
        _dir = _next > _cur ? 1 : -1;
        if (ClosedLoop && Math.Abs(_next - _cur) > 1) _dir = -_dir;   // the wrap link: 0 -> n-1 is "backwards"
        BeginSegment();
    }

    /// <summary>TGraphWeb::getShortestNextIndex with nothing come from: the nearest connected node, the lower index on a tie.</summary>
    private int NearestConnected(int i)
    {
        int n = RailNodes.Length;
        int best = -1; float bestD = 0f;
        foreach (int c in Connected(i))
        {
            float d = (RailNodes[c] - RailNodes[i]).LengthSquared();
            if (best < 0 || d < bestD) { best = c; bestD = d; }
        }
        return best < 0 ? (i + 1) % n : best;
    }

    private System.Collections.Generic.IEnumerable<int> Connected(int i)
    {
        int n = RailNodes.Length;
        if (i - 1 >= 0) yield return i - 1;
        if (i + 1 < n) yield return i + 1;
        if (ClosedLoop && n > 2)
        {
            if (i == 0) yield return n - 1;
            if (i == n - 1) yield return 0;
        }
    }

    private void BeginSegment()
    {
        _segStart = NodeMetres(_cur);
        _segEnd = NodeMetres(_next);
        _segLen = (_segEnd - _segStart).Length();
        float u = _next < RailSpeeds.Length && RailSpeeds[_next] > 0f ? RailSpeeds[_next] : 2f;
        _speed = u * FramesPerSecond / UnitsPerMetre * SpeedScale;
        Vector3 t = _next < RailTurns.Length ? RailTurns[_next] : Vector3.Zero;
        _turn = new Vector3(Mathf.Wrap(t.X, -180f, 180f), Mathf.Wrap(t.Y, -180f, 180f), Mathf.Wrap(t.Z, -180f, 180f));   // MsAngleDiff: the short way
        _travelled = 0f;
    }

    /// <summary>MsMtxSetRotRPH: Rz(roll) * Ry(yaw) * Rx(pitch), degrees.</summary>
    private static Basis RotRPH(Vector3 deg)
    {
        return new Basis(Vector3.Back, Mathf.DegToRad(deg.Z)) * new Basis(Vector3.Up, Mathf.DegToRad(deg.Y)) * new Basis(Vector3.Right, Mathf.DegToRad(deg.X));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Active || RailNodes.Length < 2) return;
        _travelled += _speed * (float)delta;
        Vector3 pos; Basis rot;
        if (_travelled >= _segLen || _segLen <= 1e-6f)
        {
            pos = _segEnd;
            _accum = RotRPH(_turn) * _accum;   // the turn banked (TRailBlock::control at a node: unk174 = RotRPH(unk168) * unk174)
            rot = _accum;
            _cur = _next;
            int n = RailNodes.Length;
            if (ClosedLoop) _next = ((_cur + _dir) % n + n) % n;
            else
            {
                int nx = _cur + _dir;
                if (nx < 0 || nx >= n) { _dir = -_dir; nx = _cur + _dir; }
                _next = nx;
            }
            BeginSegment();
        }
        else
        {
            float f = _travelled / _segLen;
            pos = _segStart.Lerp(_segEnd, f);
            rot = RotRPH(_turn * f) * _accum;
        }
        Basis rigid = new Basis(rot.X * _scale.X, rot.Y * _scale.Y, rot.Z * _scale.Z);
        Transform = new Transform3D(rigid, pos);
    }
}
