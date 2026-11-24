using System;
using Godot;

/// <summary>
/// SMS 256: the game's lift on a SPLINE rail (TNormalLift on an "S_" rail: MapObjRailBlock.cpp, graph.cpp, spline.cpp) -- the Hillside Cave Secret's two
/// star platforms before the goal (normalvariant0 on S_block00 / S_block01). TGraphWeb gives every rail whose name starts with "S_" a TSplineRail (graph.cpp
/// 145): TSplinePath's natural cubic spline through the rail's nodes on a chord-length parametrization (spline.cpp makeTable / calcSpline), closed when the
/// rail's start is its end -- the points then run last node, every node, the first node, the second node (N + 3), and t wraps between the first node's t
/// and its repeat (TSplineRail::wrapT). On a spline the lift does not walk node to node: each tick the tracer advances t by speed x (t of the node ahead - t
/// of the node behind) / the chord between them (TGraphTracer::calcSplineSpeed, traceSpline), the lift's position is the spline at t and its ROTATION is
/// MsGetRotFromZaxis(spline(t + 0.01) - spline(t)) (TRailMapObj::moveToNextNode, the spline branch: mRotation = local_28) -- the lift faces along the curve,
/// so a star on a closed loop turns once a lap while it circles. Reaching a node (t passing its t) re-reads the speed from the node's yaw x 0.01 u a tick
/// (TNormalLift::control) and moves on to the shortest next node (TGraphWeb::getShortestNextIndex: on a closed loop, the one it did not come from). Until
/// the first node is reached the speed is TNormalLift's own 1.0 (unk144). The movement runs at the director's 120 ticks a second (SmsUnits.TicksFor).
/// Knobs: RailNodes (game units, the parent's frame), RailSpeeds (u a tick per node: the node's yaw x 0.01), ClosedLoop, StartNode, FirstSpeed, LookAheadT,
/// FaceAlongRail (off = the game's position with the placed rotation kept), TicksPerSecond, SpeedScale, Active. The start: the game's tracer begins with
/// t = 0, which wraps onto the LAST node's stretch, so its lift jumps there on the first tick; here t starts at the node the lift is placed on (StartNode).
/// </summary>
public partial class SmsSplineLift : AnimatableBody3D
{
    public const string BuildMarker = "SMS256_SPLINE_LIFT_A";

    [ExportGroup("Rail (the game's units: 74.44 to the metre)")]
    /// <summary>The rail's nodes (scene.ral), game units, in this node's parent's frame.</summary>
    [Export] public Vector3[] RailNodes = Array.Empty<Vector3>();
    /// <summary>Per node: u per movement tick once that node has been reached (TRailNode mYaw x 0.01; the game's lifts carry 40 = 0.4). Empty = FirstSpeed throughout.</summary>
    [Export] public float[] RailSpeeds = Array.Empty<float>();
    /// <summary>The rail's start is its end (TGraphWeb::startIsEnd): the spline closes and the lift goes round and round.</summary>
    [Export] public bool ClosedLoop = true;
    /// <summary>The node the lift is placed on (the game: the node nearest its placement).</summary>
    [Export] public int StartNode = 0;
    /// <summary>TNormalLift's speed before the first node is reached (unk144 = 1.0 u a tick).</summary>
    [Export] public float FirstSpeed = 1f;
    /// <summary>Game units to the metre (74.44, the game's; 1 = author the nodes in metres).</summary>
    [Export] public float UnitsPerMetre = 74.44f;

    [ExportGroup("Facing")]
    /// <summary>The lift turns to face along the curve (the game's mRotation = MsGetRotFromZaxis(the tangent)); off = it keeps its placed rotation.</summary>
    [Export] public bool FaceAlongRail = true;
    /// <summary>How far ahead along t the tangent is read (TSplineRail::getPosAndRot: dt = 0.01 of the whole path).</summary>
    [Export] public float LookAheadT = 0.01f;

    [ExportGroup("Pace")]
    /// <summary>The game's movement ticks a second (120: four ticks a 30 fps frame, none skipped).</summary>
    [Export] public float TicksPerSecond = 120f;
    /// <summary>1 = the game's pace.</summary>
    [Export] public float SpeedScale = 1f;
    [Export] public bool Active = true;

    private Vector3 _scale = Vector3.One;      // the authored scale, kept out of the rotation
    private Basis _placedRot = Basis.Identity; // the authored rotation (FaceAlongRail off)
    private float[] _tx, _ty, _tz, _param, _mx, _my, _mz; // TSplinePath: the points, the chord parametrization, the three tables
    private int _n;                             // the rail's node count
    private int _cur, _prev;                    // TGraphTracer mCurrIdx / mPrevIdx
    private float _t;                           // TGraphTracer unk14: where on the spline
    private float _speed;                       // TRailMapObj unk144: u a tick
    private float _tickAccum;
    private bool _ok;

    /// <summary>Where the lift is on the curve (0..1 of the whole path), for drives.</summary>
    public float T => _t;
    public int CurrentNode => _cur;
    public float SpeedUnits => _speed;

    // ---- TSplinePath ------------------------------------------------------------------------------------------------------------------------------
    /// <summary>spline.cpp makeTable: the natural cubic spline's table for one coordinate (the game's own elimination, kept as written).</summary>
    private static float[] MakeTable(float[] p, float[] c)
    {
        int n = p.Length;
        var m = new float[n];
        var d = new float[n];     // mTmpParamDeltas
        var tmp = new float[n];   // mTmp
        m[0] = 0f; m[n - 1] = 0f;
        for (int i = 0; i < n - 1; i++)
        {
            d[i] = p[i + 1] - p[i];
            tmp[i + 1] = MathF.Abs(d[i]) < 0.0001f ? 0f : (c[i + 1] - c[i]) / d[i];
        }
        m[1] = -(d[0] * m[0] - (tmp[2] - tmp[1]));
        tmp[1] = (p[2] - p[0]) * 2f;
        for (int i = 1; i < n - 2; i++)
        {
            if (MathF.Abs(tmp[i]) < 0.0001f) tmp[i] = 0.0001f;
            float f = d[i] / tmp[i];
            m[i + 1] = -(f * m[i] - (tmp[i + 2] - tmp[i + 1]));
            tmp[i + 1] = (p[i + 2] - p[i]) * 2f - f * d[i];
        }
        m[n - 2] -= d[n - 2] * m[n - 1];
        for (int i = n - 2; i > 0; i--)
            m[i] = (m[i] - d[i] * m[i + 1]) / tmp[i];
        return m;
    }

    /// <summary>spline.cpp calcSpline: one coordinate at t.</summary>
    private static float CalcSpline(float t, float[] p, float[] c, float[] m)
    {
        int n = p.Length;
        if (n == 1) return c[0];
        if (n == 2) return c[0] * (1f - t) + c[1] * t;
        int l = 0, r = n - 1;
        while (l < r)
        {
            int mid = (l + r) / 2;
            if (p[mid] < t) l = mid + 1; else r = mid;
        }
        if (l > 0) l--;
        float dt = t - p[l];
        float dp = p[l + 1] - p[l];
        float a = m[l] * 3f + dt * (m[l + 1] - m[l]) / dp;
        float dd = (c[l + 1] - c[l]) / dp - dp * (m[l] * 2f + m[l + 1]);
        float cc = dt * a + dd;
        return dt * cc + c[l];
    }

    /// <summary>TSplineRail: the spline's points from the rail (closed: last, all, first, second), the chord-length parametrization, the tables.</summary>
    private void BuildSpline()
    {
        _n = RailNodes.Length;
        Vector3[] pts;
        if (ClosedLoop)
        {
            pts = new Vector3[_n + 3];
            pts[0] = RailNodes[_n - 1];
            for (int i = 0; i < _n; i++) pts[i + 1] = RailNodes[i];
            pts[_n + 1] = RailNodes[0];
            pts[_n + 2] = RailNodes[1 % _n];
        }
        else
            pts = (Vector3[])RailNodes.Clone();
        int k = pts.Length;
        _tx = new float[k]; _ty = new float[k]; _tz = new float[k]; _param = new float[k];
        for (int i = 0; i < k; i++) { _tx[i] = pts[i].X; _ty[i] = pts[i].Y; _tz[i] = pts[i].Z; }
        _param[0] = 0f;
        for (int i = 1; i < k; i++) _param[i] = _param[i - 1] + (pts[i] - pts[i - 1]).Length();
        float total = _param[k - 1];
        if (total > 0f) for (int i = 1; i < k; i++) _param[i] /= total;
        if (k > 2) { _mx = MakeTable(_param, _tx); _my = MakeTable(_param, _ty); _mz = MakeTable(_param, _tz); }
    }

    /// <summary>TSplineRail::getNthT: the parametrization at spline point n.</summary>
    private float NthT(int n) => _param[n];
    /// <summary>The spline point index of rail node i (the closed layout shifts every node by one).</summary>
    private int PointOf(int node) => ClosedLoop ? node + 1 : node;

    /// <summary>TSplineRail::wrapT.</summary>
    private float WrapT(float t)
    {
        if (!ClosedLoop) return t;
        float lo = NthT(1), hi = NthT(_param.Length - 2);
        if (lo >= hi) return lo;
        while (t >= hi) t -= hi - lo;
        while (t < lo) t += hi - lo;
        return t;
    }

    /// <summary>TSplinePath::getPoint at a wrapped t (game units, the parent's frame).</summary>
    private Vector3 PointAt(float t)
    {
        t = WrapT(t);
        if (_param.Length > 2)
            return new Vector3(CalcSpline(t, _param, _tx, _mx), CalcSpline(t, _param, _ty, _my), CalcSpline(t, _param, _tz, _mz));
        return _param.Length == 2 ? new Vector3(_tx[0], _ty[0], _tz[0]).Lerp(new Vector3(_tx[1], _ty[1], _tz[1]), t) : new Vector3(_tx[0], _ty[0], _tz[0]);
    }

    /// <summary>TSplineRail::getPosAndRot: the point, and the rotation (pitch x, yaw y, degrees) that puts +Z along the curve a little ahead
    /// (MsGetRotFromZaxis: yaw = atan2(x, z), pitch = -asin(y)); a flat tangent gives no rotation at all.</summary>
    private void PosAndRot(float t, out Vector3 pos, out Vector3 rotDeg)
    {
        t = WrapT(t);
        float dt = LookAheadT;
        pos = PointAt(t);
        rotDeg = Vector3.Zero;
        for (;;)
        {
            if (t + dt > 1f) t = 1f - dt;
            pos = PointAt(t);
            Vector3 dir = PointAt(t + dt) - pos;
            if (dir.LengthSquared() > 1e-12f)
            {
                dir = dir.Normalized();
                rotDeg = new Vector3(Mathf.RadToDeg(-MathF.Asin(Mathf.Clamp(dir.Y, -1f, 1f))), Mathf.RadToDeg(MathF.Atan2(dir.X, dir.Z)), 0f);
                return;
            }
            dt *= 2f;
            if (dt > 1f) return;
        }
    }

    // ---- TGraphTracer on the spline -------------------------------------------------------------------------------------------------------------
    /// <summary>TGraphWeb::getShortestNextIndex for a chain or a loop: one way out takes it; two = the one it did not come from; nowhere come from = the nearer.</summary>
    private int ShortestNext(int cur, int prev)
    {
        int n = _n;
        int a = cur - 1, b = cur + 1;
        if (ClosedLoop) { a = (a + n) % n; b %= n; }
        bool hasA = a >= 0, hasB = b < n;
        if (hasA && !hasB) return a;
        if (hasB && !hasA) return b;
        if (prev >= 0) return a == prev ? b : a;
        float da = (RailNodes[a] - RailNodes[cur]).LengthSquared(), db = (RailNodes[b] - RailNodes[cur]).LengthSquared();
        return db < da ? b : (da < db ? a : Math.Min(a, b));
    }

    /// <summary>TGraphTracer::calcSplineSpeed: a speed in u a tick as a step in t for the stretch behind -> ahead.</summary>
    private float StepT(float speed)
    {
        if (_prev < 0) return 0.001f;
        float chord = (RailNodes[_cur] - RailNodes[_prev]).Length();
        float t1, t2;
        if (ClosedLoop && _prev == _n - 1 && _cur == 0) { t1 = NthT(0); t2 = NthT(1); }
        else if (ClosedLoop && _prev == 0 && _cur == _n - 1) { t1 = NthT(_n + 1); t2 = NthT(_n); }
        else { t1 = NthT(PointOf(_prev)); t2 = NthT(PointOf(_cur)); }
        return chord > 1e-6f ? speed * (t2 - t1) / chord : 0.001f;
    }

    /// <summary>TGraphTracer::traceSpline: t advanced by step; true when the node ahead's t was crossed.</summary>
    private bool TraceSpline(float step)
    {
        float t0 = _t, t1 = t0 + step;
        float target;
        if (ClosedLoop && _prev == _n - 1 && _cur == 0) target = NthT(_n + 1);
        else if (ClosedLoop && _prev == 0 && _cur == _n - 1) target = NthT(_prev);   // the game's own read (mPrevIdx as a point index)
        else target = NthT(PointOf(_cur));
        bool reached = (step >= 0f && t0 <= target && target <= t1) || (step < 0f && t1 <= target && target <= t0);
        _t = WrapT(t1);
        return reached;
    }

    /// <summary>The node's speed (its yaw x 0.01), or -1 when the rail leaves it unset (the game keeps the speed it had: yaw 0xffff).</summary>
    private float NodeSpeed(int node) => RailSpeeds != null && node < RailSpeeds.Length && RailSpeeds[node] > 0f ? RailSpeeds[node] : -1f;

    public override void _Ready()
    {
        _placedRot = Transform.Basis.Orthonormalized();
        _scale = Transform.Basis.Scale;
        SyncToPhysics = true;
        if (Engine.IsEditorHint()) return;
        if (RailNodes == null || RailNodes.Length < 2)
        {
            GD.PushError($"SmsSplineLift '{Name}': a rail needs two nodes or more");
            return;
        }
        BuildSpline();
        _cur = Mathf.Clamp(StartNode, 0, _n - 1);
        _prev = -1;
        _t = NthT(PointOf(_cur));                    // the game: t = 0 (which wraps onto the last node's stretch); here: the node it stands on
        Position = RailNodes[_cur] / UnitsPerMetre;  // initGraphTracer: mPosition = getCurrentPos()
        _prev = _cur; _cur = ShortestNext(_cur, -1); // moveToShortestNext
        _speed = FirstSpeed;                         // unk144 until the first node is reached
        _ok = true;
        Place();
    }

    /// <summary>TNormalLift::control's spline stretch, one 120 Hz tick.</summary>
    private void Tick()
    {
        if (TraceSpline(StepT(_speed * SpeedScale)))
        {
            // moveToShortestNext, then unk144 = the new current node's yaw x 0.01
            int next = ShortestNext(_cur, _prev);
            _prev = _cur; _cur = next;
            float s = NodeSpeed(_cur);
            if (s > 0f) _speed = s;
        }
    }

    private void Place()
    {
        PosAndRot(_t, out Vector3 posU, out Vector3 rotDeg);
        Basis rot = FaceAlongRail ? RotRPH(rotDeg) : _placedRot;
        Basis rigid = new Basis(rot.X * _scale.X, rot.Y * _scale.Y, rot.Z * _scale.Z);
        Transform = new Transform3D(rigid, posU / UnitsPerMetre);
    }

    /// <summary>MsMtxSetXYZRPH's rotation: Rz(roll) * Ry(yaw) * Rx(pitch), degrees (pitch x, yaw y, roll z).</summary>
    private static Basis RotRPH(Vector3 deg) =>
        new Basis(Vector3.Back, Mathf.DegToRad(deg.Z)) * new Basis(Vector3.Up, Mathf.DegToRad(deg.Y)) * new Basis(Vector3.Right, Mathf.DegToRad(deg.X));

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !_ok || !Active) return;
        int n = TicksForRate(delta);
        for (int i = 0; i < n; i++) Tick();
        Place();
    }

    /// <summary>SmsUnits.TicksFor at this node's own rate: the 120 Hz ticks this physics frame owes, the remainder carried, never none.</summary>
    private int TicksForRate(double delta)
    {
        _tickAccum += (float)(delta * TicksPerSecond);
        int n = (int)MathF.Floor(_tickAccum + 1e-4f);
        if (n < 1) n = 1;
        _tickAccum -= n;
        if (_tickAccum < 0f) _tickAccum = 0f;
        return n;
    }
}
