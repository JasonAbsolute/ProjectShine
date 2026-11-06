using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 169: GHOST WIND (KINGBOO_REMATCH_gdoc.txt, attack 6: "Crown glows pink, for now. Tornados come at Mario and fling him around the arena"; the hit:
/// "knocked back and flung ... the fling is new work either way" -- the port has one since pass 94: Mario.SmsThrowMario, the Cataquack's fling (THROWN_DOWN:
/// he flails backwards along the flight, bounces twice, THROWN_END), which the tornado uses). King Boo makes one and it runs on its own: Count TORNADOS
/// (<see cref="SmsTornado"/>) rise out of the floor SpawnRadius (x the arena's) from its middle, the first at a random bearing and the rest SpawnSpreadDeg on,
/// none nearer Mario than SpawnMinFromMario, over RiseSeconds; then each COMES AT Mario at Speed, turning no faster than TurnDegPerSec (a sidestep or a run
/// outpaces it), wobbling a little, for LifeSeconds, then dissipates. A tornado: a spinning funnel (<see cref="GhostWind.gdshader"/> on a cone Height tall,
/// TopRadius / BaseRadius across, turning SpinDegPerSec), dust whirling at its foot, ghost wisps rising in it, a ring on the floor, the bottle's wind.
/// THE HIT: Mario's feet within Radius of a tornado's axis and under its height is the FLING -- Mario.SmsThrowMario(up and out from the axis by FlingUpK,
/// FlingSpeed) -- no HP, the thrown state's own landing; not while he is down, flung, frozen or within FlingGraceSeconds of the last. <see cref="Cancel"/>
/// (King Boo hit through the attack) dissipates every tornado. Signals Spawned(count), Flung, Finished (the last tornado gone).
/// </summary>
public partial class SmsGhostWind : Node3D
{
    public enum Phase { Idle, Rising, Hunting, Done }
    [ExportGroup("The tornados")]
    /// <summary>How many, how far out from the arena's middle they rise (x its radius), the bearings between them, how far from Mario at least, how long they
    /// take to rise, how long they live.</summary>
    [Export(PropertyHint.Range, "1,6,1")] public int Count = 2;
    [Export] public float SpawnRadius = 0.7f, SpawnSpreadDeg = 150f, SpawnMinFromMario = 8f, RiseSeconds = 0.6f, LifeSeconds = 7f;
    /// <summary>How fast one comes at Mario (m/s), how quickly it can turn toward him (degrees a second), its wobble (metres either side).</summary>
    [Export] public float Speed = 6f, TurnDegPerSec = 70f, Wobble = 0.6f;
    [ExportGroup("The funnel")]
    [Export] public float Height = 8f, TopRadius = 2.8f, BaseRadius = 0.8f, SpinDegPerSec = 420f;
    [Export] public Color Tint = new Color(0.82f, 0.66f, 1f);
    [Export(PropertyHint.Range, "0,1,0.01")] public float Alpha = 0.8f;
    [Export(PropertyHint.Range, "0,64,1")] public int Dust = 36;
    [Export(PropertyHint.Range, "0,64,1")] public int Wisps = 16;
    [ExportGroup("The fling")]
    /// <summary>The reach of the wind about the axis (metres), the fling's speed (units a frame, the game's throw units: the Cataquack's 140 lifts Mario 33 m),
    /// how much of it is up (the rest out from the axis), the grace after a fling.</summary>
    [Export] public float Radius = 1.7f, FlingSpeed = 85f;
    [Export(PropertyHint.Range, "0.3,1,0.01")] public float FlingUpK = 0.75f;
    [Export] public float FlingGraceSeconds = 2f;
    [ExportGroup("The sounds")]
    [Export] public string WindSe = "MSD_SE_MA_BOTTLE_WIND";
    [Export] public float WindSeEvery = 1.2f;
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void SpawnedEventHandler(int count);
    [Signal] public delegate void FlungEventHandler();
    [Signal] public delegate void FinishedEventHandler();

    public Phase State { get; private set; } = Phase.Idle;
    public int Flings { get; private set; }
    public IReadOnlyList<SmsTornado> Tornados => _tornados;
    public int Alive { get { int n = 0; foreach (var t in _tornados) if (IsInstanceValid(t) && !t.Gone) n++; return n; } }
    public float FloorY { get; private set; }
    public Vector3 ArenaCentre => _centre;
    public float ArenaRadius => _radius;
    public float Age => _age;
    public float LastFlingAt { get; private set; } = -99f;
    public Vector3 LastFlingPoint { get; private set; }
    public static readonly List<SmsGhostWind> All = new();
    public static int FlingCount;

    private const float U = 74.44f;
    private Mario _mario; private SmsActorSound _sound; private readonly Random _rng = new(169);
    private readonly List<SmsTornado> _tornados = new();
    private Vector3 _centre; private float _radius = 26f, _age, _seT;

    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>The tornados rise: about the arena given, over the floor at floorY, away from Mario.</summary>
    public void Begin(Mario mario, float floorY, Vector3 arenaCentre, float arenaRadius)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY; _centre = arenaCentre; _radius = Mathf.Max(5f, arenaRadius);
        _sound = SmsActorSound.Attach(this);
        GlobalPosition = new Vector3(_centre.X, floorY, _centre.Z);
        float a0 = (float)(_rng.NextDouble() * Math.Tau);
        Vector3 mp = mario != null && IsInstanceValid(mario) ? mario.GlobalPosition : _centre;
        for (int i = 0; i < Mathf.Max(1, Count); i++)
        {
            Vector3 p = Vector3.Zero; bool ok = false;
            for (int tries = 0; tries < 12 && !ok; tries++)
            {
                float a = a0 + Mathf.DegToRad(SpawnSpreadDeg) * i + (tries > 0 ? (float)(_rng.NextDouble() - 0.5) * 1.2f : 0f);
                p = new Vector3(_centre.X + MathF.Cos(a) * _radius * SpawnRadius, floorY, _centre.Z + MathF.Sin(a) * _radius * SpawnRadius);
                ok = new Vector2(p.X - mp.X, p.Z - mp.Z).Length() >= SpawnMinFromMario;
            }
            var t = new SmsTornado { Name = $"Tornado{i + 1}" };
            (GetParent() ?? this).AddChild(t);
            t.Setup(this, p, (float)(_rng.NextDouble() * Math.Tau));
            _tornados.Add(t);
        }
        State = Phase.Rising; _age = 0f; _seT = 0f;
        if (_sound != null && !string.IsNullOrEmpty(WindSe)) _sound.Play(WindSe);
        if (DebugPrint) GD.Print($"[SMS wind] {Name}: {_tornados.Count} tornado{(_tornados.Count == 1 ? "" : "s")} rise {SpawnRadius * _radius:F1} m out, {Speed} m/s at Mario for {LifeSeconds} s; the fling {FlingSpeed} ({FlingUpK:F2} up)");
        EmitSignal(SignalName.Spawned, _tornados.Count);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (State == Phase.Idle) return;
        _age += dt; _seT += dt;
        if (State == Phase.Rising && _age >= RiseSeconds) State = Phase.Hunting;
        if (State != Phase.Done && WindSeEvery > 0f && _seT >= WindSeEvery && _sound != null && !string.IsNullOrEmpty(WindSe)) { _seT = 0f; _sound.Play(WindSe, -6f, 0.9f + 0.2f * (float)_rng.NextDouble()); }
        Vector3 mp = _mario != null && IsInstanceValid(_mario) ? _mario.GlobalPosition : _centre;
        bool canFling = State == Phase.Hunting && _mario != null && IsInstanceValid(_mario) && _age - LastFlingAt >= FlingGraceSeconds && _mario.stateOfMario != Mario.MarioState.hurt && !_mario.SmsFrozen;
        int alive = 0;
        foreach (var t in _tornados)
        {
            if (!IsInstanceValid(t) || t.Gone) continue;
            alive++;
            t.Tick(dt, mp, _age, State == Phase.Hunting, _age >= LifeSeconds);
            if (canFling && t.Reaches(mp)) { canFling = false; Fling(t, mp); }
        }
        if (alive == 0 && State != Phase.Done) Finish();
    }
    private void Fling(SmsTornado t, Vector3 mp)
    {
        Vector3 away = mp - t.GlobalPosition; away.Y = 0f;
        away = away.LengthSquared() > 1e-4f ? away.Normalized() : new Vector3(MathF.Sin(t.Spin), 0f, MathF.Cos(t.Spin));
        Vector3 dir = (away * (1f - FlingUpK) + Vector3.Up * FlingUpK).Normalized();
        var was = _mario.stateOfMario;
        _mario.SmsThrowMario(dir, FlingSpeed, this);
        if (_mario.stateOfMario == Mario.MarioState.hurt)
        {
            Flings++; FlingCount++; LastFlingAt = _age; LastFlingPoint = mp; t.Flung();
            if (DebugPrint) GD.Print($"[SMS wind] {Name}: {t.Name} FLINGS Mario at {mp} along {dir} at {FlingSpeed} ({was} -> {_mario.stateOfMario}, {_mario.SmsDownKindName}); hp {_mario.SmsHP}");
            EmitSignal(SignalName.Flung);
        }
    }
    private void Finish()
    {
        State = Phase.Done; All.Remove(this);
        if (DebugPrint) GD.Print($"[SMS wind] {Name}: finished -- {Flings} fling{(Flings == 1 ? "" : "s")}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through the attack: every tornado dissipates.</summary>
    public void Cancel()
    {
        if (State == Phase.Done) return;
        State = Phase.Done; All.Remove(this);
        foreach (var t in _tornados) if (IsInstanceValid(t)) t.Dissipate();
        if (DebugPrint) GD.Print($"[SMS wind] {Name}: cut off at {_age:F2} s");
        EmitSignal(SignalName.Finished);
        GetTree().CreateTimer(1.2f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
    public static Texture2D Tex(string name) { string p = $"res://assets/Sms/Fx/tex/{name}.png"; return ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null; }
    public static CurveTexture Ramp(float a, float b)
    {
        var c = new Curve { MinValue = 0f, MaxValue = Mathf.Max(1f, Mathf.Max(a, b) * 1.1f) };
        c.AddPoint(new Vector2(0f, a)); c.AddPoint(new Vector2(1f, b));
        return new CurveTexture { Curve = c };
    }
    public static GradientTexture1D Fade(Color c0, Color c1, Color c2, Color c3)
    {
        var g = new Gradient { Offsets = new float[] { 0f, 0.15f, 0.6f, 1f }, Colors = new Color[] { c0, c1, c2, c3 } };
        return new GradientTexture1D { Gradient = g };
    }
}

/// <summary>One ghost tornado (169): the funnel, its rise, its hunt of Mario, its dissipation.</summary>
public partial class SmsTornado : Node3D
{
    public bool Gone { get; private set; }
    public bool Dissipating { get; private set; }
    public float Spin { get; private set; }
    public Vector3 Heading { get; private set; } = Vector3.Forward;
    public float RiseK { get; private set; }
    public float SpeedNow { get; private set; }
    public int FlingsMade { get; private set; }
    public float FunnelYaw => _funnel != null ? _funnel.Rotation.Y : 0f;

    private SmsGhostWind _owner; private MeshInstance3D _funnel, _ring; private ShaderMaterial _mat; private StandardMaterial3D _ringMat; private GpuParticles3D _dust, _wisps;
    private float _dissT, _wobblePhase; private bool _started;

    public void Setup(SmsGhostWind owner, Vector3 at, float heading0)
    {
        _owner = owner; GlobalPosition = at; Spin = heading0; Heading = new Vector3(MathF.Sin(heading0), 0f, MathF.Cos(heading0)); _wobblePhase = heading0;
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/GhostWind.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/GhostWind.gdshader") : null;
        if (shader != null)
        {
            _mat = new ShaderMaterial { Shader = shader, RenderPriority = 1 };
            var tex = SmsGhostWind.Tex("kemuri1_raw") ?? SmsGhostWind.Tex("kemuball3_raw"); if (tex != null) _mat.SetShaderParameter("wind_tex", tex);
            _mat.SetShaderParameter("tint", new Vector3(owner.Tint.R, owner.Tint.G, owner.Tint.B)); _mat.SetShaderParameter("alpha", owner.Alpha);
        }
        _funnel = new MeshInstance3D { Name = "Funnel", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new CylinderMesh { TopRadius = owner.TopRadius, BottomRadius = owner.BaseRadius, Height = owner.Height, RadialSegments = 24, Rings = 8, CapTop = false, CapBottom = false }, MaterialOverride = _mat, Position = Vector3.Up * (owner.Height * 0.5f), Scale = new Vector3(1f, 0.02f, 1f) };
        AddChild(_funnel);
        _ringMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = SmsGhostWind.Tex("hamonring3_soft"), AlbedoColor = new Color(owner.Tint.R, owner.Tint.G, owner.Tint.B, 0.5f) };
        _ring = new MeshInstance3D { Name = "Ring", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One * (owner.BaseRadius * 6f) }, MaterialOverride = _ringMat, Position = Vector3.Up * 0.08f, Rotation = new Vector3(-MathF.PI / 2f, 0f, 0f) };
        AddChild(_ring);
        if (owner.Dust > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring, EmissionRingAxis = Vector3.Up, EmissionRingRadius = owner.BaseRadius * 1.6f, EmissionRingInnerRadius = owner.BaseRadius * 0.8f, EmissionRingHeight = 0.2f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 60f, InitialVelocityMin = 1.5f, InitialVelocityMax = 3.5f, Gravity = new Vector3(0f, 1.5f, 0f), DampingMin = 1f, DampingMax = 2f,
                ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsGhostWind.Ramp(1.2f, 2.8f), AngularVelocityMin = -120f, AngularVelocityMax = 120f,
                Color = new Color(0.66f, 0.58f, 0.82f, 1f), ColorRamp = SmsGhostWind.Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0.4f), new Color(1f, 1f, 1f, 0f))
            };
            var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsGhostWind.Tex("kemuball3_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
            _dust = new GpuParticles3D { Name = "Dust", TopLevel = true, Amount = owner.Dust, Lifetime = 0.9, Emitting = true, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 40f, 120f)) };
            AddChild(_dust);
        }
        if (owner.Wisps > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring, EmissionRingAxis = Vector3.Up, EmissionRingRadius = owner.BaseRadius * 1.5f, EmissionRingInnerRadius = owner.BaseRadius, EmissionRingHeight = owner.Height * 0.6f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 25f, InitialVelocityMin = 2f, InitialVelocityMax = 4f, Gravity = new Vector3(0f, 1f, 0f),
                ScaleMin = 1f, ScaleMax = 1f, AngularVelocityMin = -200f, AngularVelocityMax = 200f,
                Color = new Color(owner.Tint.R, owner.Tint.G, owner.Tint.B, 1f), ColorRamp = SmsGhostWind.Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0f))
            };
            var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsGhostWind.Tex("star4s_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
            _wisps = new GpuParticles3D { Name = "Wisps", Amount = owner.Wisps, Lifetime = 1.2, Emitting = true, LocalCoords = true, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * 0.5f, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-10f, -2f, -10f), new Vector3(20f, 16f, 20f)) };
            AddChild(_wisps);
        }
        _started = true;
    }

    /// <summary>A frame: the rise, the hunt (turning toward Mario no faster than TurnDegPerSec, the wobble), the spin, the dissipation past the life.</summary>
    public void Tick(float dt, Vector3 marioPos, float age, bool hunting, bool pastLife)
    {
        if (Gone || !_started) return;
        if (pastLife && !Dissipating) Dissipate();
        float riseK = Mathf.Clamp(age / Mathf.Max(0.05f, _owner.RiseSeconds), 0f, 1f); RiseK = riseK;
        Spin += Mathf.DegToRad(_owner.SpinDegPerSec) * dt;
        if (_funnel != null) { _funnel.Rotation = new Vector3(0f, Spin, 0f); _funnel.Scale = new Vector3(Mathf.Lerp(0.3f, 1f, riseK), Mathf.Max(0.02f, riseK), Mathf.Lerp(0.3f, 1f, riseK)); }
        if (_ring != null) { _ring.Rotation = new Vector3(-MathF.PI / 2f, 0f, -Spin * 0.3f); }
        if (hunting && !Dissipating)
        {
            Vector3 to = marioPos - GlobalPosition; to.Y = 0f;
            if (to.LengthSquared() > 1e-4f)
            {
                float want = MathF.Atan2(to.X, to.Z), have = MathF.Atan2(Heading.X, Heading.Z);
                float step = Mathf.DegToRad(_owner.TurnDegPerSec) * dt, d = Mathf.AngleDifference(have, want);
                float yaw = have + Mathf.Clamp(d, -step, step);
                Heading = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
            }
            _wobblePhase += dt * 2.2f;
            Vector3 side = new Vector3(-Heading.Z, 0f, Heading.X);
            Vector3 vel = Heading * _owner.Speed + side * (MathF.Cos(_wobblePhase) * _owner.Wobble * 2.2f);
            SpeedNow = _owner.Speed;
            Vector3 p = GlobalPosition + vel * dt;
            Vector3 d2 = new Vector3(p.X - _owner.ArenaCentre.X, 0f, p.Z - _owner.ArenaCentre.Z); float dist = d2.Length(), wall = _owner.ArenaRadius - 1f;
            if (dist > wall) p -= d2 / dist * (dist - wall);
            p.Y = _owner.FloorY;
            GlobalPosition = p;
        }
        if (Dissipating)
        {
            _dissT += dt; float k = Mathf.Clamp(_dissT / 0.8f, 0f, 1f);
            _mat?.SetShaderParameter("fade", k);
            if (_ringMat != null) _ringMat.AlbedoColor = new Color(_owner.Tint.R, _owner.Tint.G, _owner.Tint.B, 0.5f * (1f - k));
            if (_funnel != null) _funnel.Scale = new Vector3(1f + k * 0.6f, Mathf.Max(0.02f, 1f - k), 1f + k * 0.6f);
            if (k >= 1f) { Gone = true; Visible = false; if (_dust != null) _dust.Emitting = false; GetTree().CreateTimer(1f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); }; }
        }
        if (_dust != null) _dust.GlobalPosition = GlobalPosition + Vector3.Up * 0.2f;
    }
    /// <summary>Mario's feet within Radius of the axis and under the funnel's height.</summary>
    public bool Reaches(Vector3 feet)
    {
        if (Gone || Dissipating || RiseK < 0.99f) return false;
        if (feet.Y - GlobalPosition.Y > _owner.Height || feet.Y - GlobalPosition.Y < -1f) return false;
        return new Vector2(feet.X - GlobalPosition.X, feet.Z - GlobalPosition.Z).Length() <= _owner.Radius + 42f / 74.44f;
    }
    public void Flung() { FlingsMade++; }
    public void Dissipate() { if (!Dissipating && !Gone) { Dissipating = true; _dissT = 0f; if (_wisps != null) _wisps.Emitting = false; } }
}
