using System;
using Godot;

/// <summary>
/// SMS 237: the game's rail block (TRailBlock, MapObjRailBlock.cpp) -- the secret courses' tumbling blocks and rolling cubes. The block walks its rail
/// node to node; each node carries the speed of the segment that reaches it (TRailNode mSpeed * 0.01 u per movement tick) and a turn (mPitch / mYaw /
/// mRoll, degrees) the block performs OVER that segment, spread evenly along it (TRailBlock::control: the angle difference over the segment's ticks). The
/// turn is about the WORLD axes: calcRootMatrix is RotRPH(the step so far) * (the turns already made), so a rail that says pitch 90 at every node rolls
/// the block over and over about world X however it is standing (coro_ex2's railvariant1: 90 per 600 u on R1_1 and R1_2 -- R1_2's "270" is -90 the
/// short way, MsAngleDiff -- and 100 per 800 u on R1_3, which leaves it 10 degrees off square each pass). coro_ex1's EXRollCube rails are closed
/// rectangles: EXCube1 turns 34 then 56 at each corner, EXCube2 yaws 60 / 90 while it goes round, EXCube0 only slides.
/// The route: a dead end sends it back the way it came and a closed loop carries it on (TGraphWeb::getShortestNextIndex: one connection = that one; two
/// = the one it did not come from; at the start, with nowhere it came from, the NEAREST connected node, the lower index on a tie).
/// The game's units are the knobs: nodes in u (74.44 to the metre) in the parent's frame, speeds in u per movement tick, turns in degrees. The tick
/// (241): TMarDirector::direct runs four 5 ms simulation ticks a 30 fps frame (dt = 600 / 30 = 20) and performs the movement list on every one, and
/// nothing gates a map object out of any of them (TViewObj::testPerform strips CUE_MOVE only for the CUE_MOVEMENT_GATE bits, which no object sets),
/// so control() -- and the block's step -- runs 120 times a second. 237-240 stepped it 30 times a second: a quarter of the game's pace.
/// 243c: RollWithTravel, a deviation Jaysonn asked for -- a block whose rail gives it no turn (the Casino's field cubes) rolls like a die as it
/// travels, a quarter turn per side length, so it still lands flat at every node. Off by default: the game's.
/// 244: authored like MovingPlatform -- drag the block in the editor to where the next stop is and click "Add Node At Current Location" (the node is
/// written in game units in the parent's frame and the block snaps back home), "Set Origin Here" moves the whole rail with node 0, "Remove Last Node";
/// MoveSpeedMetresPerSecond for a custom rail with one speed; the rail drawn in the editor as a line (nodes as crosses, the start node brighter), an
/// unowned child that is never saved and never exists at run time.
/// </summary>
[Tool]
public partial class SmsRailBlock : AnimatableBody3D
{
    [ExportGroup("Authoring (drag the block, then click)")]
    /// <summary>Makes the block's current spot node 0 and moves the whole rail with it (the nodes keep their offsets); creates the rail if there is none.</summary>
    [ExportToolButton("Set Origin Here")] public Callable SetOriginHereButton => Callable.From(SetOriginHere);
    /// <summary>Adds the block's current spot as the next node (a zero turn, the rail's default speed) and snaps the block back to its start node.</summary>
    [ExportToolButton("Add Node At Current Location")] public Callable AddNodeHereButton => Callable.From(AddNodeHere);
    [ExportToolButton("Remove Last Node")] public Callable RemoveLastNodeButton => Callable.From(RemoveLastNode);

    /// <summary>0 = the rail's own speeds (RailSpeeds, else the game's 2 u a tick = 3.22 m/s); above 0 = this many metres a second on every segment.</summary>
    [Export] public float MoveSpeedMetresPerSecond = 0f;

    [ExportGroup("Rail (the game's units: 74.44 to the metre)")]
    /// <summary>The rail's nodes (scene.ral), game units, in this node's parent's frame -- the block is set ON node StartNode at _Ready.</summary>
    [Export] public Vector3[] RailNodes = Array.Empty<Vector3>();

    /// <summary>Per node: the pitch (x), yaw (y) and roll (z) in degrees turned over the segment that ARRIVES at that node (TRailNode mPitch / mYaw / mRoll).</summary>
    [Export] public Vector3[] RailTurns = Array.Empty<Vector3>();

    /// <summary>Per node: u per movement tick for the segment that arrives at that node (TRailNode mSpeed * 0.01). 0 = the game's default 2.0.</summary>
    [Export] public float[] RailSpeeds = Array.Empty<float>();

    /// <summary>The last node connects back to the first (the EXCube rails); off = a chain whose ends turn the block round (the R1 rails).</summary>
    [Export] public bool ClosedLoop = false;

    /// <summary>The node the block stands on at load (the game: the node nearest its placement).</summary>
    [Export] public int StartNode = 0;

    /// <summary>The first node it walks to; -1 = the game's pick (the nearest connected node, the lower index on a tie).</summary>
    [Export] public int StartTowards = -1;

    /// <summary>Game units to the metre for RailNodes and RailSpeeds (74.44, the game's; 1 = author the nodes in metres).</summary>
    [Export] public float UnitsPerMetre = 74.44f;

    [ExportGroup("Pace")]
    /// <summary>The game's movement ticks a second: a speed of 1 u a tick covers this many u a second (120: four ticks a 30 fps frame, none skipped).</summary>
    [Export] public float TicksPerSecond = 120f;

    /// <summary>1 = the game's pace; a multiplier on every segment's speed.</summary>
    [Export] public float SpeedScale = 1f;

    [Export] public bool Active = true;

    [ExportGroup("Roll")]
    /// <summary>243c, Jaysonn's call over the game's: the block also ROLLS as it travels, like a die across a floor -- about the level axis across
    /// its path, a quarter turn per side length (RollSideUnits), so it lands flat at every node whose segment is a whole number of sides (the Casino's
    /// are: 800 u per field segment, 1600 across the start). The game turns a rail block only by its rail's node angles, and the Casino's exlift00,
    /// 01 and 03-08 carry none (they slide, MarioWiki: "push" and "squish"). Off = the game. Added on top of any rail turn, so leave it off where the
    /// rail already turns the block (exlift02, 09, 10).</summary>
    [Export] public bool RollWithTravel = false;

    /// <summary>The rolling block's side in game units as placed (EXRollCube 800; 1600 at scale 2): one quarter turn per this much travel.</summary>
    [Export] public float RollSideUnits = 800f;

    private Basis _accum = Basis.Identity;   // the turns already made (the game's unk174), the placement's rotation inside it
    private Vector3 _scale = Vector3.One;    // the authored scale, kept out of the rotation
    private int _cur, _next, _dir = 1;
    private Vector3 _segStart, _segEnd, _turn;
    private float _segLen, _speed, _travelled;
    private Vector3 _rollAxis = Vector3.Zero; // RollWithTravel: the level axis across this segment's path (up x direction: a forward roll)
    private float _rollAngle;                 // RollWithTravel: the radians rolled over the whole segment

    private Vector3 NodeMetres(int i) => RailNodes[i] / UnitsPerMetre;

    public override void _Ready()
    {
        _accum = Transform.Basis.Orthonormalized();
        _scale = Transform.Basis.Scale;
        SyncToPhysics = true;
        if (!Engine.IsEditorHint()) SetProcess(false);   // _Process only draws the editor's rail line
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
        _speed = MoveSpeedMetresPerSecond > 0f ? MoveSpeedMetresPerSecond * SpeedScale : u * TicksPerSecond / UnitsPerMetre * SpeedScale;
        Vector3 t = _next < RailTurns.Length ? RailTurns[_next] : Vector3.Zero;
        _turn = new Vector3(ShortWay(t.X), ShortWay(t.Y), ShortWay(t.Z));
        _travelled = 0f;
        _rollAxis = Vector3.Zero; _rollAngle = 0f;
        if (RollWithTravel && RollSideUnits > 0f)
        {
            Vector3 flat = new Vector3(_segEnd.X - _segStart.X, 0f, _segEnd.Z - _segStart.Z);   // the path across the level, the parent's frame
            if (flat.LengthSquared() > 1e-8f)
            {
                _rollAxis = Vector3.Up.Cross(flat.Normalized());                                   // the level axis across the path: the top rolls forward
                _rollAngle = _segLen / (RollSideUnits / UnitsPerMetre) * (Mathf.Pi / 2f);         // a quarter turn per side length
            }
        }
    }

    /// <summary>RollWithTravel: the roll made over the fraction f of this segment (identity when the roll is off or the segment is vertical).</summary>
    private Basis Roll(float f) => _rollAxis == Vector3.Zero ? Basis.Identity : new Basis(_rollAxis, _rollAngle * f);

    /// <summary>MsAngleDiff(angle, 0): the short way round, in the game's range (-180, 180] -- a node angle of exactly 180 turns +180 (the Casino's
    /// three half-turn cubes, exlift02 / 09 / 10). 237-245 used Godot's Wrap, whose range is [-180, 180), so those three turned the other way (246).</summary>
    private static float ShortWay(float deg)
    {
        float w = Mathf.Wrap(deg, -180f, 180f);
        return Mathf.IsEqualApprox(w, -180f) ? 180f : w;
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
            _accum = Roll(1f) * RotRPH(_turn) * _accum;   // the turn banked (TRailBlock::control at a node: unk174 = RotRPH(unk168) * unk174), the roll with it
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
            rot = Roll(f) * RotRPH(_turn * f) * _accum;
        }
        Basis rigid = new Basis(rot.X * _scale.X, rot.Y * _scale.Y, rot.Z * _scale.Z);
        Transform = new Transform3D(rigid, pos);
    }

    // ---- 244: authoring -- the three buttons (MovingPlatform's workflow) and the editor's rail line ----------------------------------------------

    /// <summary>The block's current spot as a rail node: its local position (the parent's frame) in game units.</summary>
    private Vector3 HereUnits() => Position * UnitsPerMetre;

    /// <summary>Keeps RailTurns and RailSpeeds as long as RailNodes (a zero turn and the default speed for any node without an entry).</summary>
    private void PadRailArrays()
    {
        int n = RailNodes.Length;
        if (RailTurns.Length < n) { int o = RailTurns.Length; Array.Resize(ref RailTurns, n); for (int i = o; i < n; i++) RailTurns[i] = Vector3.Zero; }
        if (RailSpeeds.Length < n) Array.Resize(ref RailSpeeds, n);
    }

    public void SetOriginHere()
    {
        Vector3 here = HereUnits();
        if (RailNodes.Length == 0) RailNodes = new[] { here };
        else
        {
            Vector3 shift = here - RailNodes[0];
            for (int i = 0; i < RailNodes.Length; i++) RailNodes[i] += shift;
        }
        PadRailArrays();
        StartNode = 0;
        NotifyPropertyListChanged();
        _previewStamp = null;
    }

    public void AddNodeHere()
    {
        if (RailNodes.Length == 0) { SetOriginHere(); return; }
        Array.Resize(ref RailNodes, RailNodes.Length + 1);
        RailNodes[^1] = HereUnits();
        PadRailArrays();
        Position = NodeMetres(Mathf.Clamp(StartNode, 0, RailNodes.Length - 1));   // back home: the block keeps standing where it starts
        NotifyPropertyListChanged();
        _previewStamp = null;
    }

    public void RemoveLastNode()
    {
        if (RailNodes.Length == 0) return;
        int n = RailNodes.Length - 1;
        Array.Resize(ref RailNodes, n);
        if (RailTurns.Length > n) Array.Resize(ref RailTurns, n);
        if (RailSpeeds.Length > n) Array.Resize(ref RailSpeeds, n);
        StartNode = Mathf.Clamp(StartNode, 0, Math.Max(0, n - 1));
        NotifyPropertyListChanged();
        _previewStamp = null;
    }

    private MeshInstance3D _preview;   // the editor's rail line: unowned (never saved), rebuilt when the rail changes, absent at run time
    private string _previewStamp;

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) return;
        UpdatePreview();
    }

    public override void _ExitTree()
    {
        if (_preview != null && IsInstanceValid(_preview)) _preview.QueueFree();
        _preview = null; _previewStamp = null;
    }

    /// <summary>Draws the rail in the parent's frame: the segments as lines, each node a cross, the start node brighter; a closed loop closes the line.</summary>
    public void UpdatePreview()
    {
        if (RailNodes.Length == 0)
        {
            if (_preview != null && IsInstanceValid(_preview)) _preview.QueueFree();
            _preview = null; _previewStamp = null;
            return;
        }
        if (_preview == null || !IsInstanceValid(_preview))
        {
            _preview = new MeshInstance3D { Name = "RailPreview", TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _preview.MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                NoDepthTest = true, RenderPriority = 100
            };
            AddChild(_preview);   // no owner: the editor never saves it
            _previewStamp = null;
        }
        // The nodes are in the parent's frame, so the line lives there: its own transform is the parent's (top level, so this node's does not add).
        _preview.GlobalTransform = GetParent() is Node3D p ? p.GlobalTransform : Transform3D.Identity;
        string stamp = $"{UnitsPerMetre}|{ClosedLoop}|{StartNode}|" + string.Join(";", RailNodes);
        if (stamp == _previewStamp) return;
        _previewStamp = stamp;
        var im = new ImmediateMesh();
        Color line = new Color(1f, 0.85f, 0.2f), cross = new Color(1f, 0.5f, 0.1f), start = new Color(0.3f, 1f, 0.4f);
        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        int n = RailNodes.Length;
        for (int i = 0; i + 1 < n; i++) { im.SurfaceSetColor(line); im.SurfaceAddVertex(NodeMetres(i)); im.SurfaceSetColor(line); im.SurfaceAddVertex(NodeMetres(i + 1)); }
        if (ClosedLoop && n > 2) { im.SurfaceSetColor(line); im.SurfaceAddVertex(NodeMetres(n - 1)); im.SurfaceSetColor(line); im.SurfaceAddVertex(NodeMetres(0)); }
        float h = 0.6f;
        for (int i = 0; i < n; i++)
        {
            Vector3 c = NodeMetres(i); Color k = i == Mathf.Clamp(StartNode, 0, n - 1) ? start : cross;
            im.SurfaceSetColor(k); im.SurfaceAddVertex(c + Vector3.Left * h); im.SurfaceSetColor(k); im.SurfaceAddVertex(c + Vector3.Right * h);
            im.SurfaceSetColor(k); im.SurfaceAddVertex(c + Vector3.Down * h); im.SurfaceSetColor(k); im.SurfaceAddVertex(c + Vector3.Up * h);
            im.SurfaceSetColor(k); im.SurfaceAddVertex(c + Vector3.Forward * h); im.SurfaceSetColor(k); im.SurfaceAddVertex(c + Vector3.Back * h);
        }
        im.SurfaceEnd();
        _preview.Mesh = im;
    }

    /// <summary>For a test: the preview's line vertex count (0 with no preview).</summary>
    public int PreviewVertexCount() => _preview != null && IsInstanceValid(_preview) && _preview.Mesh is ImmediateMesh m && m.GetSurfaceCount() > 0 ? m.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length : 0;
}
