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
    [Export(PropertyHint.Range, "1,8,1")] public int Count = 5;
    [Export] public float SpawnRadius = 0.7f, SpawnSpreadDeg = 150f, SpawnMinFromMario = 8f, RiseSeconds = 0.6f, LifeSeconds = 7f;
    /// <summary>195 (Jaysonn: "spawn 5 tornados for the wind attack and have the wind pattern go into the middle then back out while circling around the arena"):
    /// the PATTERN. Hunt: 169's -- each turns toward Mario and comes at him. Spiral: the Count of them rise evenly spaced on a ring SpawnRadius out (the ring
    /// turned so none is within SpawnMinFromMario of him), then the whole ring CIRCLES the arena (all one way, a random way each attack) at Speed along the
    /// ring (no faster than MaxOrbitDegPerSec about the middle, so they do not whip round at the centre) while its radius BREATHES -- in to InnerRadius (x the
    /// arena's) and back out to SpawnRadius, SpiralCycles times over the life (1: in by the middle of the life, out by its end) -- a spiral in, a spiral out.</summary>
    public enum Patterns { Hunt, Spiral }
    [Export] public Patterns Pattern = Patterns.Spiral;
    [Export(PropertyHint.Range, "0,0.6,0.01")] public float InnerRadius = 0.12f;
    [Export(PropertyHint.Range, "0.5,3,0.5")] public float SpiralCycles = 1f;
    [Export(PropertyHint.Range, "20,240,5")] public float MaxOrbitDegPerSec = 110f;
    /// <summary>How fast one comes at Mario (m/s), how quickly it can turn toward him (degrees a second), its wobble (metres either side).</summary>
    [Export] public float Speed = 6f, TurnDegPerSec = 70f, Wobble = 0.6f;
    [ExportGroup("The funnel")]
    [Export] public float Height = 8f, TopRadius = 2.8f, BaseRadius = 0.8f, SpinDegPerSec = 420f;
    [Export] public Color Tint = new Color(0.82f, 0.66f, 1f);
    [Export(PropertyHint.Range, "0,1,0.01")] public float Alpha = 0.5f;
    /// <summary>170 (Jaysonn: "more like a gust of wind"): the streaks of wind spiralling up the funnel -- how many ribbons, their width, the turns each makes
    /// from the foot to the head, how fast the set spins (degrees a second; the funnel's own spin is SpinDegPerSec) -- and the debris whirling in it.</summary>
    [Export(PropertyHint.Range, "0,8,1")] public int Ribbons = 4;
    [Export] public float RibbonWidth = 0.55f, RibbonTurns = 1.6f, RibbonSpinDegPerSec = 300f;
    [Export(PropertyHint.Range, "0,64,1")] public int Debris = 22;
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
    /// <summary>195: the way the ring circles (+1 anticlockwise seen from above, -1 clockwise); the ring's radius the spiral asks for at this age (metres from the middle).</summary>
    public int OrbitSign { get; private set; } = 1;
    public float SpiralRadiusAt(float age)
    {
        float t = MathF.Max(0f, age - RiseSeconds), active = MathF.Max(0.5f, LifeSeconds - RiseSeconds), period = active / MathF.Max(0.5f, SpiralCycles);
        float k = 0.5f - 0.5f * MathF.Cos(MathF.Tau * t / period);                                                   // 0 at the start (out), 1 half a cycle in (the middle), 0 again at the cycle's end
        return Mathf.Lerp(_radius * SpawnRadius, _radius * InnerRadius, k);
    }
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
        int n = Mathf.Max(1, Count);
        OrbitSign = _rng.NextDouble() < 0.5 ? -1 : 1;
        if (Pattern == Patterns.Spiral)
        {
            // 195: an even ring, turned so none of them rises within SpawnMinFromMario of Mario (the turn with the most room wins when none clears)
            float best = -1f, bestA = a0;
            for (int tries = 0; tries < 16; tries++)
            {
                float a = a0 + tries * MathF.Tau / (n * 16f), minD = float.MaxValue;
                for (int i = 0; i < n; i++) { float ai = a + i * MathF.Tau / n; minD = MathF.Min(minD, new Vector2(_centre.X + MathF.Cos(ai) * _radius * SpawnRadius - mp.X, _centre.Z + MathF.Sin(ai) * _radius * SpawnRadius - mp.Z).Length()); }
                if (minD > best) { best = minD; bestA = a; }
                if (minD >= SpawnMinFromMario) break;
            }
            a0 = bestA;
        }
        for (int i = 0; i < n; i++)
        {
            Vector3 p = Vector3.Zero; bool ok = false; float ang = a0;
            for (int tries = 0; tries < 12 && !ok; tries++)
            {
                ang = Pattern == Patterns.Spiral ? a0 + i * MathF.Tau / n : a0 + Mathf.DegToRad(SpawnSpreadDeg) * i + (tries > 0 ? (float)(_rng.NextDouble() - 0.5) * 1.2f : 0f);
                p = new Vector3(_centre.X + MathF.Cos(ang) * _radius * SpawnRadius, floorY, _centre.Z + MathF.Sin(ang) * _radius * SpawnRadius);
                ok = Pattern == Patterns.Spiral || new Vector2(p.X - mp.X, p.Z - mp.Z).Length() >= SpawnMinFromMario;
            }
            var t = new SmsTornado { Name = $"Tornado{i + 1}" };
            (GetParent() ?? this).AddChild(t);
            t.Setup(this, p, (float)(_rng.NextDouble() * Math.Tau));
            t.OrbitAngle = ang;
            _tornados.Add(t);
        }
        State = Phase.Rising; _age = 0f; _seT = 0f;
        if (_sound != null && !string.IsNullOrEmpty(WindSe)) _sound.Play(WindSe);
        if (DebugPrint) GD.Print($"[SMS wind] {Name}: {_tornados.Count} tornado{(_tornados.Count == 1 ? "" : "s")} rise {SpawnRadius * _radius:F1} m out, {Pattern} -- {(Pattern == Patterns.Spiral ? $"circling {(OrbitSign > 0 ? "anticlockwise" : "clockwise")} at {Speed} m/s, in to {InnerRadius * _radius:F1} m and out again {SpiralCycles}x" : $"{Speed} m/s at Mario")} for {LifeSeconds} s; the fling {FlingSpeed} ({FlingUpK:F2} up)");
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
        State = Phase.Done;                                                                                     // 203: it stays in All till it is freed (the field clear reaches a cancelled wind's dissipating tornados through it)
        foreach (var t in _tornados) if (IsInstanceValid(t)) t.Dissipate();
        if (DebugPrint) GD.Print($"[SMS wind] {Name}: cut off at {_age:F2} s");
        EmitSignal(SignalName.Finished);
        GetTree().CreateTimer(1.2f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
    /// <summary>203: gone at once with the field (the split's cut) -- every tornado freed outright (no dissipate), this node too; true = anything went.</summary>
    public bool VanishNow()
    {
        bool any = false;
        foreach (var t in _tornados) if (IsInstanceValid(t) && t.VanishNow()) any = true;
        if (State != Phase.Done) { State = Phase.Done; any = true; }
        if (!IsQueuedForDeletion()) { QueueFree(); any = true; }
        return any;
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
    /// <summary>195: its bearing about the arena's middle (radians; the spiral turns it) and its distance from the middle now.</summary>
    public float OrbitAngle { get; set; }
    public float OrbitRadiusNow { get; private set; }
    public float OrbitTurned { get; private set; }

    private SmsGhostWind _owner; private MeshInstance3D _funnel, _ring; private ShaderMaterial _mat; private StandardMaterial3D _ringMat; private GpuParticles3D _dust, _wisps, _debris;
    private Node3D _gust; private readonly List<ShaderMaterial> _ribbonMats = new(); private float _gustSpin;
    private float _dissT, _wobblePhase; private bool _started;
    /// <summary>203: every tornado standing in the world (they live in the arena, not under their wind: a cancelled wind is freed before its tornados have dissipated) -- the field clear's list.</summary>
    public static readonly List<SmsTornado> All = new();
    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }
    /// <summary>203: gone at once with the field (the split's cut): no dissipate, hidden and freed; true = it went.</summary>
    public bool VanishNow()
    {
        if (Gone || IsQueuedForDeletion()) return false;
        Gone = true; Dissipating = true; Visible = false;
        if (_dust != null) _dust.Emitting = false;
        QueueFree();
        return true;
    }

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
        if (owner.Ribbons > 0) MakeGust(owner);
        if (owner.Debris > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring, EmissionRingAxis = Vector3.Up, EmissionRingRadius = owner.BaseRadius * 2.2f, EmissionRingInnerRadius = owner.BaseRadius * 1.2f, EmissionRingHeight = owner.Height * 0.5f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 30f, InitialVelocityMin = 1.5f, InitialVelocityMax = 3.5f, Gravity = new Vector3(0f, 0.5f, 0f),
                ScaleMin = 1f, ScaleMax = 1f, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -500f, AngularVelocityMax = 500f,
                Color = new Color(0.36f, 0.3f, 0.42f, 1f), ColorRamp = SmsGhostWind.Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0f))
            };
            var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsGhostWind.Tex("shikaku"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
            _debris = new GpuParticles3D { Name = "Debris", Amount = owner.Debris, Lifetime = 1.4, Emitting = true, LocalCoords = true, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = new Vector2(0.22f, 0.14f), Material = mat }, VisibilityAabb = new Aabb(new Vector3(-10f, -2f, -10f), new Vector3(20f, 16f, 20f)) };
            (_gust ?? (Node3D)this).AddChild(_debris);                             // in the gust's frame: it whirls with the spin
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
            (_gust ?? (Node3D)this).AddChild(_wisps);                              // in the gust's frame: they whirl with it
        }
        _started = true;
    }

    /// <summary>170: the GUST -- Ribbons helix ribbons (an ArrayMesh each: RibbonTurns turns from the foot to the head, the radius growing with the funnel's,
    /// RibbonWidth across, 48 steps) under one node that spins at RibbonSpinDegPerSec, each with the ribbon shader at its own phase; the wisps and the debris
    /// ride in the same frame so they whirl.</summary>
    private void MakeGust(SmsGhostWind owner)
    {
        _gust = new Node3D { Name = "Gust" }; AddChild(_gust);
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/WindRibbon.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/WindRibbon.gdshader") : null;
        var tex = SmsGhostWind.Tex("kemuri1_raw") ?? SmsGhostWind.Tex("kemuball3_raw");
        for (int r = 0; r < owner.Ribbons; r++)
        {
            float phase0 = r / (float)owner.Ribbons * MathF.Tau, heightK = 0.85f + 0.15f * (r % 2), radiusK = 1.05f + 0.12f * (r % 3);
            var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
            const int steps = 48; var prev = new Vector3[2]; var prevUv = new Vector2[2];
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps, ang = phase0 + u * owner.RibbonTurns * MathF.Tau, y = u * owner.Height * heightK;
                float rad = Mathf.Lerp(owner.BaseRadius, owner.TopRadius, u) * radiusK;
                Vector3 c = new Vector3(MathF.Cos(ang) * rad, y, MathF.Sin(ang) * rad);
                Vector3 up = Vector3.Up * (owner.RibbonWidth * 0.5f);
                Vector3[] cur = { c - up, c + up }; Vector2[] uv = { new Vector2(u, 0f), new Vector2(u, 1f) };
                if (i > 0)
                {
                    Vector3 n = (prev[1] - prev[0]).Cross(cur[0] - prev[0]); n = n.LengthSquared() > 1e-10f ? n.Normalized() : Vector3.Up;
                    foreach (var (p, t) in new[] { (prev[0], prevUv[0]), (prev[1], prevUv[1]), (cur[0], uv[0]), (cur[0], uv[0]), (prev[1], prevUv[1]), (cur[1], uv[1]) }) { st.SetNormal(n); st.SetUV(t); st.AddVertex(p); }
                }
                prev = cur; prevUv = uv;
            }
            var mesh = st.Commit();
            ShaderMaterial mat = null;
            if (shader != null) { mat = new ShaderMaterial { Shader = shader, RenderPriority = 2 }; if (tex != null) mat.SetShaderParameter("streak_tex", tex); mat.SetShaderParameter("phase", phase0); mat.SetShaderParameter("tint", new Vector3(0.94f, 0.9f, 1f)); _ribbonMats.Add(mat); }
            var mi = new MeshInstance3D { Name = $"Ribbon{r + 1}", Mesh = mesh, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _gust.AddChild(mi);
        }
        _gust.Scale = new Vector3(0.3f, 0.02f, 0.3f);
    }

    /// <summary>A frame: the rise, the hunt (turning toward Mario no faster than TurnDegPerSec, the wobble), the spin, the dissipation past the life.</summary>
    public void Tick(float dt, Vector3 marioPos, float age, bool hunting, bool pastLife)
    {
        if (Gone || !_started) return;
        if (pastLife && !Dissipating) Dissipate();
        float riseK = Mathf.Clamp(age / Mathf.Max(0.05f, _owner.RiseSeconds), 0f, 1f); RiseK = riseK;
        Spin += Mathf.DegToRad(_owner.SpinDegPerSec) * dt; _gustSpin += Mathf.DegToRad(_owner.RibbonSpinDegPerSec) * dt;
        if (_gust != null) { _gust.Rotation = new Vector3(0f, _gustSpin, 0f); if (!Dissipating) _gust.Scale = new Vector3(Mathf.Lerp(0.3f, 1f, riseK), Mathf.Max(0.02f, riseK), Mathf.Lerp(0.3f, 1f, riseK)); }
        if (_funnel != null) { _funnel.Rotation = new Vector3(0f, Spin, 0f); _funnel.Scale = new Vector3(Mathf.Lerp(0.3f, 1f, riseK), Mathf.Max(0.02f, riseK), Mathf.Lerp(0.3f, 1f, riseK)); }
        if (_ring != null) { _ring.Rotation = new Vector3(-MathF.PI / 2f, 0f, -Spin * 0.3f); }
        if (hunting && !Dissipating && _owner.Pattern == SmsGhostWind.Patterns.Spiral)                                   // 195: the ring circles the arena, breathing in to the middle and out
        {
            float r = _owner.SpiralRadiusAt(age);
            float omega = MathF.Min(_owner.Speed / MathF.Max(1f, r), Mathf.DegToRad(_owner.MaxOrbitDegPerSec));           // Speed along the ring, capped about the middle
            OrbitAngle += _owner.OrbitSign * omega * dt; OrbitTurned += omega * dt;
            Vector3 was = GlobalPosition;
            Vector3 p = new Vector3(_owner.ArenaCentre.X + MathF.Cos(OrbitAngle) * r, _owner.FloorY, _owner.ArenaCentre.Z + MathF.Sin(OrbitAngle) * r);
            Vector3 moved = p - was; moved.Y = 0f;
            if (moved.LengthSquared() > 1e-6f) Heading = moved.Normalized();
            SpeedNow = dt > 1e-5f ? moved.Length() / dt : 0f;
            OrbitRadiusNow = r;
            GlobalPosition = p;
        }
        else if (hunting && !Dissipating)
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
            _mat?.SetShaderParameter("fade", k); foreach (var rm in _ribbonMats) rm.SetShaderParameter("fade", k);
            if (_gust != null) _gust.Scale = new Vector3(1f + k * 0.6f, Mathf.Max(0.02f, 1f - k), 1f + k * 0.6f);
            if (_debris != null && k > 0.3f) _debris.Emitting = false;
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
