using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A tightrope: TMapWire with its TMapWireActor (src/Map/MapWire.cpp, MapWireManager.cpp), pass 66. The game calls it a WIRE; the clips
/// say rope.
/// GEOMETRY (TMapWire::init 369-443): a wire is a TCubeGeneralInfo cube -- centre, Euler rotation, a size vector. This node IS that cube:
/// its origin the cube's bottom centre (CLBIsPointInCube 165-213: x and z are half extents about the centre, y runs 0 .. size.y UP from
/// it), local +Z along the wire. The tips sit at the cube's TOP, half the length each way (initTipPoints 339-366: centre +- halfWire, + size.y
/// in Y); the idle curve is the chord minus a half-sine sag of size.y / 2 (getPointPosDefault: y -= mWireSag * sin(pos * pi)); the
/// render points are one per 50 units, minus the two tips (floor(length / 50 + 1) - 2).
/// MARIO ON IT: the manager (TMapWireActorManager::doActorToWire 115-148, perform 183-199) finds the cube Mario's position is in; while he is
/// held the wire is HANGING with its hang point at Mario's model root (setFootPointsAtHanged(gpMarioOriginal->getTakenMtx()), 255-273)
/// every frame -- so the drawn rope is a V pinned at his FEET when he stands on it and at his HANDS when he hangs (the hang clips are
/// authored with the hands at the root), the flat bit mFootLength 26 units each way, straight to the tips outside. When he leaves
/// (receiveMessage 8, or his position leaving the cube) release(): the whole rope bounces as a damped cosine (updateMovePointAtReleased
/// 159-174) -- amplitude mHeightRate 1.3 x his launch speed (mReleaseHeight 100 if he left going down), the decay 0.003 x 2|hangPos - 0.5|
/// (a release from the exact middle never decays: the game's arithmetic), the phase 0.03 a frame, ending under 0.001; the render points,
/// re-seeded half each side of the hang point, drift back to their default spots at (default - pos) / 1000 a frame.
/// DRAW (drawUpper / drawLower 40-128): two flat faces through the points, mDrawWidth 5 each side of the chord's perpendicular, the lower
/// one mDrawHeight 6 below, unlit flat colours. DrawSides adds the two side faces (a visual deviation: the open gap reads wrong edge-on).
/// The fittings (WireFitting.bmd / .col) are solid posts at the tips: Posts stands two cylinders from the node's base to the tips.
/// </summary>
[GlobalClass]
public partial class SmsWire : Node3D
{
    public enum WireState { Idle, Hanging, Released }

    [Export] public float LengthUnits = 1500f;     // size.z: the chord's length (the game's mWireLength)
    [Export] public float CubeHeightUnits = 320f;  // size.y: the tips this far above the node; the idle sag is half of it; the grab box's height
    [Export] public float WidthUnits = 100f;       // size.x: the grab box's width (the level data's; not in the decomp)
    [Export] public bool Posts = true;
    [Export] public float PostRadiusUnits = 12f;
    [Export] public Color UpperColor = new Color(0.32f, 0.25f, 0.17f);
    [Export] public Color LowerColor = new Color(0.17f, 0.13f, 0.08f);
    [Export] public bool DrawSides = true;         // DEVIATION (visual): the game draws the upper and lower faces only
    [Export] public bool DebugPrint = false;

    public WireState State { get; private set; } = WireState.Idle;
    /// <summary>The tips (world, metres): mStartPoint / mEndPoint.</summary>
    public Vector3 StartPoint { get; private set; }
    public Vector3 EndPoint { get; private set; }
    public Vector3 Span => EndPoint - StartPoint;
    /// <summary>mWireLength (units): the authored length, which is the chord's.</summary>
    public float LengthUnitsNow => LengthUnits;
    public float HangPos { get; private set; }     // mHangPos: where he is, 0..1
    public bool Built { get; private set; }

    private struct WirePoint
    {
        public float PosOnWire, DefaultPosOnWire, ReturnRate;
        public Vector3 Position, DefaultPosition;
    }
    private WirePoint[] _points = Array.Empty<WirePoint>();
    private int _active;                           // mNumActiveMapWirePoints (2 while hanging)
    private Vector3 _hangOrBounce;                 // mHangOrBouncePoint (world metres; while RELEASED only .y matters)
    private float _hangRef1, _hangRef2;            // mHangReferencePos1 / 2
    private float _bounceAmp, _bounceDecay, _bouncePower, _moveTimer; // units / per frame / 0..1 / 0..2
    private float _tickAccum;
    private MeshInstance3D _ribbon;
    private ImmediateMesh _mesh;
    private bool _dirty = true;
    private float _sagM => SmsUnits.UnitsToMetres(CubeHeightUnits * 0.5f);

    public override void _EnterTree() => SmsWireMap.Wires.Add(this);
    public override void _ExitTree() => SmsWireMap.Wires.Remove(this);

    public override void _Ready()
    {
        Build();
    }

    /// <summary>TMapWire::init: the tips, the sag, the render points at their default spots; the posts and the ribbon.</summary>
    public void Build()
    {
        Basis b = GlobalBasis.Orthonormalized();
        Vector3 half = b * new Vector3(0f, 0f, SmsUnits.UnitsToMetres(LengthUnits) * 0.5f);
        Vector3 raise = Vector3.Up * SmsUnits.UnitsToMetres(CubeHeightUnits);
        StartPoint = GlobalPosition - half + raise;
        EndPoint = GlobalPosition + half + raise;
        int n = Math.Max(0, (int)(LengthUnits / 50f + 1f - 2f));
        _points = new WirePoint[n];
        for (int i = 0; i < n; i++)
        {
            float pos = (i + 1) / (float)n;
            _points[i].PosOnWire = _points[i].DefaultPosOnWire = pos;
            _points[i].DefaultPosition = PointDefault(pos);
            _points[i].Position = _points[i].DefaultPosition;
        }
        _active = n;
        State = WireState.Idle;
        _bouncePower = 0f;
        _hangOrBounce = Vector3.Zero;
        if (_ribbon == null)
        {
            _mesh = new ImmediateMesh();
            _ribbon = new MeshInstance3D
            {
                Name = "Ribbon", Mesh = _mesh, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled },
            };
            AddChild(_ribbon);
            _ribbon.GlobalTransform = Transform3D.Identity;
        }
        if (Posts && GetNodeOrNull("PostStart") == null)
        {
            MakePost("PostStart", StartPoint);
            MakePost("PostEnd", EndPoint);
        }
        _dirty = true;
        Built = true;
        if (DebugPrint)
            GD.Print($"[SMS wire] {Name}: length {LengthUnits} units, {n} points, tips {StartPoint} -> {EndPoint}, sag {CubeHeightUnits * 0.5f} units");
    }

    private void MakePost(string name, Vector3 tip)
    {
        float h = SmsUnits.UnitsToMetres(CubeHeightUnits);
        float r = SmsUnits.UnitsToMetres(PostRadiusUnits);
        var body = new StaticBody3D { Name = name, TopLevel = true };
        AddChild(body);
        body.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(tip.X, tip.Y - h * 0.5f, tip.Z));
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = r, Height = h } });
        body.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h, RadialSegments = 10 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.22f, 0.2f) },
        });
    }

    // ------------------------------------------------------------------------------------------------------- the cube and the ratio
    /// <summary>CLBIsPointInCube on this wire's cube: |x| within half the width, 0 < y < the height, |z| within half the length, in the
    /// node's frame -- the game's gpCubeWire->getInCubeNo(marioPos).</summary>
    public bool InCube(Vector3 world)
    {
        Vector3 l = GlobalBasis.Orthonormalized().Inverse() * (world - GlobalPosition);
        float hx = SmsUnits.UnitsToMetres(WidthUnits) * 0.5f, hz = SmsUnits.UnitsToMetres(LengthUnits) * 0.5f, hy = SmsUnits.UnitsToMetres(CubeHeightUnits);
        return -hx < l.X && l.X < hx && 0f < l.Y && l.Y < hy && -hz < l.Z && l.Z < hz;
    }

    /// <summary>getPosInWire (275-289): the point projected onto the chord in the XZ plane, as 0..1 from the start.</summary>
    public float PosInWire(Vector3 world)
    {
        Vector2 s = new Vector2(StartPoint.X, StartPoint.Z), e = new Vector2(EndPoint.X, EndPoint.Z), p = new Vector2(world.X, world.Z);
        Vector2 d = e - s;
        float len2 = d.LengthSquared();
        if (len2 < 1e-8f)
            return 0f;
        float t = (p - s).Dot(d) / len2;
        return Mathf.Clamp(t, 0f, 1f); // the perpendicular foot's distance from the start over the chord's length
    }

    // ---------------------------------------------------------------------------------------------------------------- sampling
    public Vector3 PointOnLine(float pos) => StartPoint + Span * pos;

    public Vector3 PointDefault(float pos)
    {
        Vector3 p = PointOnLine(pos);
        p.Y -= _sagM * MathF.Sin(pos * MathF.PI);
        return p;
    }

    private float PointPowerAtReleased(float pos)
    {
        float rel = pos >= HangPos ? (HangPos >= 1f ? 0f : (pos - HangPos) / (1f - HangPos)) : (HangPos <= 0f ? 0f : 1f - pos / HangPos);
        return 1f - rel * rel;
    }

    private Vector3 PointAtReleased(float pos)
    {
        Vector3 line = PointOnLine(pos);
        Vector3 def = PointDefault(pos);
        float power = PointPowerAtReleased(pos);
        return new Vector3(line.X, line.Y + (1f - _bouncePower) * (def.Y - line.Y) + power * _hangOrBounce.Y, line.Z);
    }

    private Vector3 PointAtHanged(float pos)
    {
        float off = pos - HangPos;
        Vector3 o = new Vector3(Span.X * off + _hangOrBounce.X, 0f, Span.Z * off + _hangOrBounce.Z);
        if (pos <= _hangRef1)
            o.Y = _hangOrBounce.Y + (StartPoint.Y - _hangOrBounce.Y) * (_hangRef1 - pos) / MathF.Max(_hangRef1, 1e-6f);
        else if (pos >= _hangRef2)
            o.Y = _hangOrBounce.Y + (EndPoint.Y - _hangOrBounce.Y) * (pos - _hangRef2) / MathF.Max(1f - _hangRef2, 1e-6f);
        else
            o.Y = _hangOrBounce.Y;
        return o;
    }

    /// <summary>getPointPosOnWire (294-314): where the rope is at 0..1 right now (the V while held, the bounce after, the sag at rest).</summary>
    public Vector3 PointOnWire(float pos)
    {
        pos = Mathf.Clamp(pos, 0f, 1f);
        return State == WireState.Hanging ? PointAtHanged(pos) : PointAtReleased(pos);
    }

    // ------------------------------------------------------------------------------------------------------------ hang / release
    /// <summary>setFootPointsAtHanged(Mario's model root), every frame he is held: HANGING, the hang point there, the two foot refs
    /// mFootLength 26 units each way (or the hang point itself when the tip is nearer).</summary>
    public void SetHang(Vector3 marioRoot)
    {
        State = WireState.Hanging;
        _hangOrBounce = marioRoot;
        HangPos = PosInWire(marioRoot);
        float footFrac = SmsParams.Wire.FootLength / MathF.Max(LengthUnits, 1f);
        _hangRef1 = HangPos - footFrac;
        _hangRef2 = HangPos + footFrac;
        _active = 2;
        if (_points.Length < 2)
            _points = new WirePoint[2];
        if (SmsParams.Wire.FootLength < HangPos * LengthUnits)
        {
            _points[0].PosOnWire = _hangRef1;
            _points[0].Position = PointAtHanged(_hangRef1);
        }
        else
        {
            _points[0].PosOnWire = HangPos;
            _points[0].Position = _hangOrBounce;
        }
        if (SmsParams.Wire.FootLength < (1f - HangPos) * LengthUnits)
        {
            _points[1].PosOnWire = _hangRef2;
            _points[1].Position = PointAtHanged(_hangRef2);
        }
        else
        {
            _points[1].PosOnWire = HangPos;
            _points[1].Position = _hangOrBounce;
        }
        _dirty = true;
    }

    /// <summary>release() (177-212): all the points back, re-seeded half each side of the hang point; the bounce's amplitude from his
    /// launch (mHeightRate 1.3 x |v| going up, else mReleaseHeight 100), its decay 0.003 x 2|hangPos - 0.5| a frame, power 1, phase 1
    /// (the cosine starts at -1: the rope recoils down first). <paramref name="marioVelUnitsPerFrame"/> is his velocity in SMS units.</summary>
    public void Release(Vector3 marioVelUnitsPerFrame)
    {
        if (State == WireState.Released)
            return;
        State = WireState.Released;
        _hangOrBounce = Vector3.Zero;
        int n = _points.Length;
        _active = n;
        int half = n / 2;
        if (half != 0)
        {
            float adv = HangPos / half;
            for (int i = 0; i < half; i++)
                InitPointAtJustReleased(adv * (i + 1), i);
        }
        if (n - half != 0)
        {
            float adv = (1f - HangPos) / (n - half);
            for (int i = half; i < n; i++)
                InitPointAtJustReleased(adv * (i - half + 1) + HangPos, i);
        }
        float stretch = SmsParams.Wire.StretchRate * MathF.Abs(HangPos - 0.5f);
        _bounceAmp = marioVelUnitsPerFrame.Y > 0f ? SmsParams.Wire.HeightRate * marioVelUnitsPerFrame.Length() : SmsParams.Wire.ReleaseHeight;
        _bounceDecay = SmsParams.Wire.DownRateMax * stretch;
        _bouncePower = 1f;
        _moveTimer = 1f;
        _dirty = true;
        if (DebugPrint)
            GD.Print($"[SMS wire] {Name}: released at {HangPos:F2}: amplitude {_bounceAmp:F0} units, decay {_bounceDecay:F5} a frame");
    }

    private void InitPointAtJustReleased(float pos, int i)
    {
        _points[i].PosOnWire = pos;
        _points[i].Position = PointAtReleased(pos);
        _points[i].ReturnRate = (_points[i].DefaultPosOnWire - pos) / 1000f;
    }

    /// <summary>The game's per-frame move() while RELEASED, run per 120 Hz tick at a quarter: the power fades, the phase advances, the
    /// bounce point's Y is the cosine; the points drift back to their default spots and are re-sampled. IDLE / HANGING do nothing.</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (!Built)
            return;
        int n = SmsUnits.TicksFor(delta, ref _tickAccum);
        if (State == WireState.Released)
        {
            for (int t = 0; t < n; t++)
            {
                _bouncePower -= _bounceDecay / SmsUnits.TicksPerFrame;
                if (_bouncePower < SmsParams.Wire.EndRate)
                {
                    for (int i = 0; i < _points.Length; i++)
                    {
                        _points[i].PosOnWire = _points[i].DefaultPosOnWire;
                        _points[i].Position = _points[i].DefaultPosition;
                    }
                    _active = _points.Length;
                    _bouncePower = 0f;
                    _hangOrBounce = Vector3.Zero;
                    State = WireState.Idle;
                    if (DebugPrint)
                        GD.Print($"[SMS wire] {Name}: the bounce ended");
                    break;
                }
                _moveTimer += SmsParams.Wire.MoveTimerSpeed / SmsUnits.TicksPerFrame;
                if (_moveTimer >= 2f)
                    _moveTimer -= 2f;
                _hangOrBounce.Y = SmsUnits.UnitsToMetres(MathF.Cos(_moveTimer * MathF.PI) * _bounceAmp * _bouncePower);
                for (int i = 0; i < _active; i++)
                {
                    float pos = _points[i].DefaultPosOnWire;
                    float step = _points[i].ReturnRate / SmsUnits.TicksPerFrame;
                    if (MathF.Abs(pos - _points[i].PosOnWire) > MathF.Abs(step))
                        pos = _points[i].PosOnWire + step;
                    _points[i].PosOnWire = pos;
                    _points[i].Position = PointAtReleased(pos);
                }
            }
            _dirty = true;
        }
        if (_dirty)
        {
            Redraw();
            _dirty = false;
        }
    }

    /// <summary>The ribbon through the tips and the active points: the upper face at the points, the lower mDrawHeight below, each
    /// mDrawWidth to both sides of the chord's XZ perpendicular; the sides optional.</summary>
    private void Redraw()
    {
        if (_mesh == null)
            return;
        Vector3 sp = Span;
        Vector3 axes = new Vector3(sp.Z, 0f, -sp.X);
        axes = axes.LengthSquared() > 1e-8f ? axes.Normalized() : Vector3.Right;
        Vector3 o = axes * SmsUnits.UnitsToMetres(SmsParams.Wire.DrawWidth);
        Vector3 down = Vector3.Down * SmsUnits.UnitsToMetres(SmsParams.Wire.DrawHeight);
        var pts = new List<Vector3>(_active + 2) { StartPoint };
        for (int i = 0; i < _active && i < _points.Length; i++)
            pts.Add(_points[i].Position);
        pts.Add(EndPoint);
        _mesh.ClearSurfaces();
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            Quad(a + o, a - o, b + o, b - o, UpperColor);                      // the upper face
            Quad(a - o + down, a + o + down, b - o + down, b + o + down, LowerColor); // the lower face
            if (DrawSides)
            {
                Quad(a + o + down, a + o, b + o + down, b + o, LowerColor);
                Quad(a - o, a - o + down, b - o, b - o + down, LowerColor);
            }
        }
        _mesh.SurfaceEnd();
    }

    private void Quad(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Color c)
    {
        _mesh.SurfaceSetColor(c);
        _mesh.SurfaceAddVertex(a0); _mesh.SurfaceAddVertex(b0); _mesh.SurfaceAddVertex(a1);
        _mesh.SurfaceAddVertex(a1); _mesh.SurfaceAddVertex(b0); _mesh.SurfaceAddVertex(b1);
    }
}

/// <summary>TMapWireManager's cube lookup: the wires in the scene, and the one whose cube a world point is in.</summary>
public static class SmsWireMap
{
    public static readonly List<SmsWire> Wires = new();

    public static SmsWire InCube(Vector3 world)
    {
        foreach (SmsWire w in Wires)
            if (w != null && GodotObject.IsInstanceValid(w) && w.Built && w.InCube(world))
                return w;
        return null;
    }
}
