using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 168: FIREBALLS (KINGBOO_REMATCH_gdoc.txt, attack 5: "Crown glows red. Fireballs shoot off in random directions and bounce off the arena walls.
/// They shrink as they travel and fade out"; the hit: "the burn. Set fireDown on the profile, which routes into SmsFireDownEnter and the ms_firedmg_a
/// cloud" -- Mario.SmsDamageProfile.Fire, the fire enemies' own). Pass 172 (Jaysonn: "fireballs should be more like molten lava balls and should come from
/// the crown popping a few out at once"): King Boo makes one at his CROWN and it runs on its own -- Pops POPS of PerPop LAVA BALLS at once, PopSeconds apart,
/// each ball LOBBED out of the crown (LobHeight over it, under gravity) to come down about Mario: one of every pop within AimedWithin of him, the rest anywhere
/// out to Scatter (a ring on the floor marks where each will land, LandingMarks); a ball that lands BOUNCES (Bounce of its fall, a splat of lava each time) and
/// ROLLS on at RollSpeed along its way, bouncing off the arena's wall (the floor's circle less WallInset: its flat way mirrored about the wall's normal, a
/// flare, MSD_SE_MA_BOUND) as often as it reaches it. A ball (<see cref="SmsFireball"/>) is a MOLTEN LAVA BALL Size metres across at its birth -- a sphere
/// of dark crust with glowing cracks and molten pools (LavaBall.gdshader; its heat cooling as it ages), a glow about it, flames licking off it, embers and
/// DRIPS of lava trailing in the world, a smoke trail, a light on the floor; the crust rolls with it -- that SHRINKS to nothing over LifeSeconds and fades over
/// the last of them. THE HIT: a ball meeting Mario's damage cylinder is Mario.TakeDamage(the ball, Fire) -- the burn: FIRE_DOWN's launch, ma_firejmp, the
/// fire cloud, 1 HP, 300 ticks invincible (its own refusals: invincible, down, a demo) -- and the ball BURSTS (DiesOnHit: a splash of lava, flames, a ring,
/// smoke, a kick of the camera) -- unless his feet are HitHeight over the floor: a jump clears a rolling ball, a hop does not. MSD_SE_BS_TELESA_FIRE as the
/// first pop comes (the casino boss's own fire), a pop sound per pop. <see cref="Cancel"/> (King Boo hit through it) fizzles every ball out. Signals
/// Shot(count) as the last pop leaves, Burned per burn, Bounced per bounce (the wall's or the floor's), Finished as the last ball dies.
/// </summary>
public partial class SmsFireballs : Node3D
{
    public enum Phase { Idle, Volley, Flying, Done }
    [ExportGroup("The pops (172)")]
    /// <summary>How many POPS out of the crown and how many balls each (a few at once), the time between pops, how high over the crown they lob (metres),
    /// how far about Mario they come down (metres: one ball of every pop within AimedWithin of him, the rest anywhere out to Scatter), how fast a landed
    /// ball rolls on (m/s; 0 = at the speed it came down with), how much of its fall a landing keeps (0 = it lands and rolls; 0.35 = a bounce or two first),
    /// whether a ring on the floor marks where each ball will land.</summary>
    [Export(PropertyHint.Range, "1,8,1")] public int Pops = 3;
    [Export(PropertyHint.Range, "1,8,1")] public int PerPop = 3;
    [Export] public float PopSeconds = 0.45f, LobHeight = 6f, Scatter = 7f, AimedWithin = 1.5f, RollSpeed = 8f;
    [Export(PropertyHint.Range, "0,0.8,0.05")] public float Bounce = 0.35f;
    [Export] public bool LandingMarks = true;
    [ExportGroup("The lava balls")]
    /// <summary>A ball's size at birth (metres across: the radius is half), how long it lives (it shrinks to nothing over this), where its fade begins (0..1
    /// of the life), how far in from the floor's edge the wall is, whether the ball bursts on Mario.</summary>
    [Export] public float Size = 3f, LifeSeconds = 6f, WallInset = 0.6f;   // 173: 3 m across (Jaysonn: "make the lava balls bigger"; 2 before)
    /// <summary>How high over the floor the fire reaches: a Mario whose feet are higher is over it (the jump; a hop is not enough).</summary>
    [Export] public float HitHeight = 1.6f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float FadeFrom = 0.65f;
    [Export] public bool DiesOnHit = true;
    [ExportGroup("The look")]
    /// <summary>The glow about a ball and its light, the flames' colour, the embers' and drips' colour; how many flames, embers, drips and smoke puffs a ball
    /// carries (0 = none each); how cool a ball is at the end of its life (the crust's heat: 1 = molten, 0 = dark).</summary>
    [Export] public Color GlowColor = new Color(1f, 0.45f, 0.1f), FlameColor = new Color(1f, 0.42f, 0.06f), EmberColor = new Color(1f, 0.7f, 0.25f);
    [Export] public bool Lights = true;
    [Export] public float LightEnergy = 3f, LightRange = 9f;
    [Export(PropertyHint.Range, "0,64,1")] public int Flames = 16;
    [Export(PropertyHint.Range, "0,64,1")] public int Embers = 24;
    [Export(PropertyHint.Range, "0,32,1")] public int Drips = 10;
    [Export(PropertyHint.Range, "0,32,1")] public int Smoke = 18;
    [Export(PropertyHint.Range, "0,1,0.05")] public float HeatAtEnd = 0.3f;
    /// <summary>204 (Jaysonn: "the fireballs need the fire to trail behind them -- right now there are sparks; give them more feel that they are moving"): the FIRE TRAIL.
    /// Flame puffs are left in the WORLD behind each ball as it flies and rolls (TrailPuffs alive at once, each TrailSeconds long, born TrailSize x the ball's radius,
    /// shrinking and rising as they go from yellow through the flame colour to a dark red), drifting back along the way it came (TrailBack: their speed backward as a
    /// fraction of the ball's -- 0 = left where they were born); thin hot STREAKS along the motion (TrailStreaks of them, a quarter second each); and the ball's own
    /// flames LEAN BACK with its speed (TrailLean: 0 = straight up as before, 1 = laid flat back at the roll speed). Trail off = 168's: the flames ride on the ball
    /// and only the embers, drips and smoke are left behind.</summary>
    [Export] public bool Trail = true;
    [Export(PropertyHint.Range, "0,200,1")] public int TrailPuffs = 72;
    [Export] public float TrailSeconds = 0.55f, TrailSize = 1.15f, TrailBack = 0.25f;
    [Export(PropertyHint.Range, "0,64,1")] public int TrailStreaks = 20;
    [Export(PropertyHint.Range, "0,1.5,0.05")] public float TrailLean = 0.7f;
    /// <summary>205 (Jaysonn: "the effect could look a little better when it goes away, like a little puff"): a ball GOING OUT -- the end of its life (it has shrunk to
    /// nothing) or a fizzle that is not a burst on Mario -- ends in a small PUFF where it was: EndPuffs puffs of dark smoke rising and spreading, EndSparks embers flung
    /// out and falling, and a small ring of light, all EndPuffScale x the ball's birth size (it is tiny by then). The burst on Mario has its own effects (168). Off: 168-204's
    /// -- it just winks out.</summary>
    [Export] public bool EndPuff = true;
    [Export(PropertyHint.Range, "0,40,1")] public int EndPuffs = 10;
    [Export(PropertyHint.Range, "0,40,1")] public int EndSparks = 12;
    [Export(PropertyHint.Range, "0.2,2,0.05")] public float EndPuffScale = 0.9f;
    public int EndPuffsMade { get; set; }
    [ExportGroup("The sounds")]
    [Export] public string VolleySe = "MSD_SE_BS_TELESA_FIRE", SpitSe = "MSD_SE_BS_WANWAN_REBURN1", BounceSe = "MSD_SE_MA_BOUND";
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void ShotEventHandler(int count);
    [Signal] public delegate void BurnedEventHandler();
    [Signal] public delegate void BouncedEventHandler();
    [Signal] public delegate void FinishedEventHandler();

    public Phase State { get; private set; } = Phase.Idle;
    public int Spat { get; private set; }
    public int PopsMade { get; private set; }
    public int Burns { get; private set; }
    public int BouncesMade { get; private set; }
    public int Landings { get; private set; }
    public int Alive => _balls.Count;
    public IReadOnlyList<SmsFireball> Balls => _balls;
    public float FloorY { get; private set; }
    public Vector3 ArenaCentre => _centre;
    public float ArenaRadius => _radius;
    public float CentreYaw => _yawCentre;
    public Vector3 SpoutPoint => _spout != null ? _spout() : GlobalPosition;
    public int Count => Pops * PerPop;
    public static readonly List<SmsFireballs> All = new();
    public static int BurnCount;

    private const float U = 74.44f;
    public const float G = 20f;                                                 // the lob's gravity (m/s^2; the port's fire balls fell at this since 168)
    private Mario _mario; private Func<Vector3> _spout; private SmsActorSound _sound; private readonly Random _rng = new(168);
    private readonly List<SmsFireball> _balls = new(); private readonly List<SmsFireball> _dead = new();
    private Vector3 _centre; private float _radius = 26f, _yawCentre, _t, _nextAt;

    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>The pops begin: out of the spout (the crown's top, read per pop), about yawCentre (the way to Mario), inside the arena given, over the floor at floorY.</summary>
    public void Begin(Mario mario, float floorY, Func<Vector3> spout, float yawCentre, Vector3 arenaCentre, float arenaRadius)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY; _spout = spout; _yawCentre = yawCentre; _centre = arenaCentre; _radius = Mathf.Max(5f, arenaRadius);
        _sound = SmsActorSound.Attach(this);
        GlobalPosition = spout();
        State = Phase.Volley; _t = 0f; _nextAt = 0f;
        if (_sound != null && !string.IsNullOrEmpty(VolleySe)) _sound.Play(VolleySe);
        if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: the POPS -- {Pops} x {PerPop} lava balls {PopSeconds:F2} s apart out of the crown at {GlobalPosition}, lobbed {LobHeight} m up to come down within {Scatter} m of Mario (one within {AimedWithin} m), {Size} m across, {LifeSeconds} s, rolling at {RollSpeed} m/s; the wall at {_radius - WallInset:F1} m about {_centre}");
    }

    /// <summary>One pop: PerPop balls out of the crown at once, each lobbed to its own spot about Mario.</summary>
    private void Pop()
    {
        Vector3 from = SpoutPoint;
        Vector3 m = _mario != null && IsInstanceValid(_mario) ? _mario.GlobalPosition : _centre;
        for (int i = 0; i < PerPop; i++)
        {
            float ang = (float)(_rng.NextDouble() * Math.Tau);
            float dist = i == 0 ? AimedWithin * (float)Math.Sqrt(_rng.NextDouble()) : Mathf.Lerp(AimedWithin, Scatter, (float)Math.Sqrt(_rng.NextDouble()));
            Vector3 target = new Vector3(m.X + MathF.Sin(ang) * dist, FloorY, m.Z + MathF.Cos(ang) * dist);
            Vector3 cd = new Vector3(target.X - _centre.X, 0f, target.Z - _centre.Z); float cl = cd.Length(), wallR = _radius - WallInset - Size;
            if (cl > wallR && cl > 1e-3f) target = new Vector3(_centre.X + cd.X / cl * wallR, FloorY, _centre.Z + cd.Z / cl * wallR);   // inside the wall
            float r0 = Size * 0.5f;
            float h = Mathf.Max(0.5f, LobHeight), tUp = MathF.Sqrt(2f * h / G), apexY = from.Y + h, tDown = MathF.Sqrt(2f * Mathf.Max(0.1f, apexY - (FloorY + r0)) / G), T = tUp + tDown;
            Vector3 flat = new Vector3(target.X - from.X, 0f, target.Z - from.Z) / T;
            var b = new SmsFireball { Name = $"LavaBall{Spat + 1}" };
            (GetParent() ?? this).AddChild(b);
            b.Setup(this, from, flat, G * tUp, r0, LifeSeconds, FloorY, target, (float)_rng.NextDouble() * 100f);
            _balls.Add(b); Spat++;
            if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: lava ball {Spat} of {Count} pops out of the crown at {from} toward {target} ({dist:F1} m from Mario{(i == 0 ? ", the aimed one" : "")}; {T:F2} s in the air, up {G * tUp:F1} m/s, across {flat.Length():F1} m/s)");
        }
        PopsMade++;
        if (_sound != null && !string.IsNullOrEmpty(SpitSe)) _sound.Play(SpitSe, -3f, 0.85f + 0.2f * (float)_rng.NextDouble());
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (State == Phase.Idle) return;
        _t += dt;
        if (State == Phase.Volley)
        {
            while (PopsMade < Pops && _t >= _nextAt) { Pop(); _nextAt += Mathf.Max(0.05f, PopSeconds); }
            if (PopsMade >= Pops) { State = Phase.Flying; EmitSignal(SignalName.Shot, Spat); }
        }
        TickBalls(dt);
        if (State == Phase.Flying && _balls.Count == 0) Finish();
    }
    private void TickBalls(float dt)
    {
        _dead.Clear();
        foreach (var b in _balls)
        {
            if (!IsInstanceValid(b)) { _dead.Add(b); continue; }
            b.Tick(dt, _mario, _centre, _radius - WallInset, out bool bounced, out bool burned, out bool landed);
            if (bounced) { BouncesMade++; if (_sound != null && !string.IsNullOrEmpty(BounceSe)) _sound.Play(BounceSe, -6f, 0.8f); EmitSignal(SignalName.Bounced); }
            if (landed) { Landings++; if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: {b.Name} LANDS at {b.GlobalPosition} after {b.Bounces} bounce{(b.Bounces == 1 ? "" : "s")}, rolling at {b.Vel.Length():F1} m/s"); }
            if (burned) { Burns++; BurnCount++; if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: {b.Name} BURNS Mario at {b.GlobalPosition} (r {b.R:F2}, age {b.Age:F2}, {(b.OnFloor ? "rolling" : "in the air")}); hp {_mario?.SmsHP}"); EmitSignal(SignalName.Burned); }
            if (b.Dead) _dead.Add(b);
        }
        foreach (var b in _dead) _balls.Remove(b);
    }
    private void Finish()
    {
        State = Phase.Done; All.Remove(this);
        if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: finished -- {Spat} popped in {PopsMade} pops, {Landings} landings, {BouncesMade} bounce{(BouncesMade == 1 ? "" : "s")}, {Burns} burn{(Burns == 1 ? "" : "s")}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through the attack: no more pops, every ball fizzles out.</summary>
    public void Cancel()
    {
        if (State == Phase.Done) return;
        State = Phase.Done;                                                                                     // 203: it stays in All till it is freed (the field clear)
        foreach (var b in _balls) if (IsInstanceValid(b)) b.Fizzle();
        if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: cut off after {PopsMade} of {Pops} pops ({_balls.Count} fizzling)");
        EmitSignal(SignalName.Finished);
        GetTree().CreateTimer(0.6f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
    /// <summary>203: gone at once with the field (the split's cut) -- every ball freed outright (no fizzle), this node too; true = anything went.</summary>
    public bool VanishNow()
    {
        bool any = false;
        foreach (var b in _balls) if (IsInstanceValid(b) && b.VanishNow()) any = true;
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
    /// <summary>An additive, unshaded, vertex-coloured particle billboard of a texture.</summary>
    public static StandardMaterial3D AddBillboard(Texture2D tex) => new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
    /// <summary>The same, not a billboard (an align_y streak: the quad's Y along the particle's way).</summary>
    public static StandardMaterial3D AddStreak(Texture2D tex) => new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
}

/// <summary>One lava ball (168, molten since 172): the crust sphere and its flight -- the lob out of the crown, the bounces and the landing, the roll at
/// RollSpeed, the bounces off the arena wall, the shrink to nothing over its life (the crust cooling), the burn on Mario. The flames and the glow scale with
/// the node (local coordinates); the embers, drips and smoke trail in the world.</summary>
public partial class SmsFireball : Node3D
{
    public Vector3 Vel { get; private set; }
    public float R0 { get; private set; }
    public float R { get; private set; }
    public float Age { get; private set; }
    public float Life { get; private set; }
    public bool Dead { get; private set; }
    public bool Fizzling { get; private set; }
    public bool OnFloor { get; private set; }
    public int Bounces { get; private set; }
    public bool HitMario { get; private set; }
    public float FloorY { get; private set; }
    public Vector3 Target { get; private set; }
    public float Heat { get; private set; } = 1f;
    public float Vy => _vy;
    public float ScaleShown => _k;

    private const float U = 74.44f;
    private SmsFireballs _owner; private MeshInstance3D _core, _glow, _mark; private ShaderMaterial _crust; private StandardMaterial3D _glowMat, _markMat; private GpuParticles3D _flames, _embers, _drips, _smoke; private OmniLight3D _light;
    private float _vy, _k = 1f, _flare, _fizzleT, _seed; private bool _inAir;
    // 204: the fire trail -- the world puffs and the streaks behind the ball, and the ball's own flames' material (its direction leans back with the speed)
    private GpuParticles3D _trail, _streaks; private ParticleProcessMaterial _flamesPm, _trailPm, _streaksPm;
    /// <summary>204, for the drives: the trail emitter is on and world-space, where it emits from (it rides the ball), how many puffs it keeps, the streaks' count,
    /// and how far the ball's own flames lean back along the way it moves (the flames' direction dotted with the motion reversed: 0 = straight up, 1 = flat back).</summary>
    public bool TrailOn => _trail != null && IsInstanceValid(_trail) && _trail.Emitting && !_trail.LocalCoords;
    public Vector3 TrailEmitPoint => _trail != null && IsInstanceValid(_trail) ? _trail.GlobalPosition : GlobalPosition;
    public int TrailPuffsNow => _trail != null && IsInstanceValid(_trail) ? _trail.Amount : 0;
    public int TrailStreaksNow => _streaks != null && IsInstanceValid(_streaks) && _streaks.Emitting ? _streaks.Amount : 0;
    public float TrailLeanNow { get; private set; }
    public float TrailBackSpeedNow => _trailPm != null ? _trailPm.InitialVelocityMax : 0f;
    /// <summary>203: every ball in the world (they live in the arena, not under their maker: a cancelled maker is freed while its balls still fizzle) -- the field clear's list.</summary>
    public static readonly List<SmsFireball> All = new();
    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }
    /// <summary>203: gone at once with the field (the split's cut): no fizzle, hidden and freed with its mark; true = it went.</summary>
    public bool VanishNow()
    {
        if (Dead || IsQueuedForDeletion()) return false;
        Dead = true; Fizzling = true; Visible = false;
        if (_flames != null) _flames.Emitting = false;
        if (_mark != null && IsInstanceValid(_mark)) { _mark.QueueFree(); _mark = null; }
        QueueFree();
        return true;
    }

    public void Setup(SmsFireballs owner, Vector3 from, Vector3 flatVel, float vy0, float radius, float life, float floorY, Vector3 target, float seed)
    {
        _owner = owner; Vel = flatVel; _vy = vy0; R0 = R = Mathf.Max(0.1f, radius); Life = Mathf.Max(0.5f, life); FloorY = floorY; Target = target; _seed = seed; GlobalPosition = from; _inAir = true;
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/LavaBall.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/LavaBall.gdshader") : null;
        if (shader != null) { _crust = new ShaderMaterial { Shader = shader }; _crust.SetShaderParameter("seed", seed); _crust.SetShaderParameter("heat", 1f); }
        _core = new MeshInstance3D { Name = "Crust", Mesh = new SphereMesh { Radius = R0, Height = R0 * 2f, RadialSegments = 28, Rings = 14 }, MaterialOverride = (Material)_crust ?? new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.04f, 0.02f), EmissionEnabled = true, Emission = owner.GlowColor }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _core.Rotation = new Vector3(seed, seed * 1.7f, 0f);
        AddChild(_core);
        _glowMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = SmsFireballs.Tex("glow3_raw"), AlbedoColor = new Color(owner.GlowColor.R, owner.GlowColor.G, owner.GlowColor.B, 0.55f), RenderPriority = 1 };
        _glow = new MeshInstance3D { Name = "Glow", Mesh = new QuadMesh { Size = Vector2.One * (R0 * 2.8f) }, MaterialOverride = _glowMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_glow);
        if (owner.Flames > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.8f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 50f, InitialVelocityMin = R0 * 1.5f, InitialVelocityMax = R0 * 3.5f, Gravity = new Vector3(0f, R0 * 3f, 0f),
                ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsFireballs.Ramp(1f, 0.2f), AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -90f, AngularVelocityMax = 90f,
                Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(1f, 0.9f, 0.35f, 0.9f), owner.FlameColor, new Color(0.8f, 0.1f, 0.02f, 0.7f), new Color(0.4f, 0.03f, 0.01f, 0f))
            };
            _flames = new GpuParticles3D { Name = "Flames", Amount = owner.Flames, Lifetime = 0.35, Emitting = true, LocalCoords = true, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * (R0 * 1.1f), Material = SmsFireballs.AddBillboard(SmsFireballs.Tex("fire1_raw")) }, VisibilityAabb = new Aabb(new Vector3(-6f, -6f, -6f), new Vector3(12f, 12f, 12f)) };
            AddChild(_flames); _flamesPm = pm;
        }
        if (owner.Trail && owner.TrailPuffs > 0)   // 204: the fire left behind it -- world puffs born at the ball, drifting back the way it came, rising and dying
        {
            _trailPm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.55f,
                Direction = new Vector3(0f, 0f, 1f), Spread = 22f, InitialVelocityMin = 0.5f, InitialVelocityMax = 1.5f, Gravity = new Vector3(0f, R0 * 1.6f, 0f), DampingMin = 0.5f, DampingMax = 1f,
                ScaleMin = 0.85f, ScaleMax = 1.15f, ScaleCurve = SmsFireballs.Ramp(1f, 0.25f), AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -120f, AngularVelocityMax = 120f,
                Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(1f, 0.95f, 0.5f, 0.95f), owner.FlameColor, new Color(0.85f, 0.12f, 0.02f, 0.6f), new Color(0.35f, 0.03f, 0.01f, 0f))
            };
            _trail = new GpuParticles3D { Name = "Trail", TopLevel = true, Amount = owner.TrailPuffs, Lifetime = Mathf.Max(0.1f, owner.TrailSeconds), Emitting = true, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = _trailPm, DrawPass1 = new QuadMesh { Size = Vector2.One * (R0 * owner.TrailSize), Material = SmsFireballs.AddBillboard(SmsFireballs.Tex("fire1_raw")) }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 30f, 120f)) };
            AddChild(_trail);
        }
        if (owner.Trail && owner.TrailStreaks > 0)   // 204: hot streaks along the motion (aligned to their own velocity, which runs back the way the ball came)
        {
            _streaksPm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.5f, ParticleFlagAlignY = true,
                Direction = new Vector3(0f, 0f, 1f), Spread = 12f, InitialVelocityMin = 2f, InitialVelocityMax = 4f, Gravity = Vector3.Zero, DampingMin = 2f, DampingMax = 4f,
                ScaleMin = 0.8f, ScaleMax = 1.2f, Color = new Color(1f, 1f, 1f, 1f),
                ColorRamp = SmsFireballs.Fade(new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.6f, 0.15f, 0.9f), new Color(0.9f, 0.25f, 0.05f, 0.5f), new Color(0.4f, 0.05f, 0.01f, 0f))
            };
            _streaks = new GpuParticles3D { Name = "Streaks", TopLevel = true, Amount = owner.TrailStreaks, Lifetime = 0.28, Emitting = true, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = _streaksPm, DrawPass1 = new QuadMesh { Size = new Vector2(R0 * 0.12f, R0 * 0.9f), Material = SmsFireballs.AddStreak(SmsFireballs.Tex("glow1_spark")) }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 30f, 120f)) };
            AddChild(_streaks);
        }
        if (owner.Embers > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.7f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 0.8f, InitialVelocityMax = 3f, Gravity = new Vector3(0f, -4f, 0f), DampingMin = 0.5f, DampingMax = 1.5f,
                ScaleMin = 0.7f, ScaleMax = 1.3f, Color = new Color(owner.EmberColor.R, owner.EmberColor.G, owner.EmberColor.B, 1f),
                ColorRamp = SmsFireballs.Fade(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f), new Color(1f, 0.55f, 0.25f, 0.7f), new Color(0.8f, 0.2f, 0.05f, 0f))
            };
            _embers = new GpuParticles3D { Name = "Embers", TopLevel = true, Amount = owner.Embers, Lifetime = 0.9, Emitting = true, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * 0.16f, Material = SmsFireballs.AddBillboard(SmsFireballs.Tex("glow1_spark")) }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 30f, 120f)) };
            AddChild(_embers);
        }
        if (owner.Drips > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.75f, ParticleFlagAlignY = true,
                Direction = new Vector3(0f, -1f, 0f), Spread = 70f, InitialVelocityMin = 0.5f, InitialVelocityMax = 2f, Gravity = new Vector3(0f, -14f, 0f),
                ScaleMin = 0.7f, ScaleMax = 1.3f, Color = new Color(1f, 1f, 1f, 1f),
                ColorRamp = SmsFireballs.Fade(new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.55f, 0.1f, 1f), new Color(0.9f, 0.25f, 0.03f, 0.9f), new Color(0.4f, 0.05f, 0.01f, 0f))
            };
            _drips = new GpuParticles3D { Name = "Drips", TopLevel = true, Amount = owner.Drips, Lifetime = 0.6, Emitting = true, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = new Vector2(0.09f, 0.34f), Material = SmsFireballs.AddStreak(SmsFireballs.Tex("glow1_spark")) }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 30f, 120f)) };
            AddChild(_drips);
        }
        if (owner.Smoke > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.6f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 30f, InitialVelocityMin = 0.4f, InitialVelocityMax = 1.2f, Gravity = new Vector3(0f, 1.6f, 0f), DampingMin = 0.5f, DampingMax = 1f,
                ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsFireballs.Ramp(0.6f, 2.2f), AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -30f, AngularVelocityMax = 30f,
                Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(0.35f, 0.2f, 0.15f, 0f), new Color(0.16f, 0.12f, 0.11f, 0.5f), new Color(0.12f, 0.1f, 0.1f, 0.3f), new Color(0.1f, 0.1f, 0.1f, 0f))
            };
            var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsFireballs.Tex("kemuball3_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
            _smoke = new GpuParticles3D { Name = "Smoke", TopLevel = true, Amount = owner.Smoke, Lifetime = 1.1, Emitting = true, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * (R0 * 1.1f), Material = mat }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 30f, 120f)) };
            AddChild(_smoke);
        }
        if (owner.Lights) { _light = new OmniLight3D { Name = "Light", LightColor = new Color(1f, 0.5f, 0.15f), LightEnergy = owner.LightEnergy, OmniRange = owner.LightRange, ShadowEnabled = false }; AddChild(_light); }
        if (owner.LandingMarks)
        {
            _markMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = SmsFireballs.Tex("hamonring3_flat"), AlbedoColor = new Color(1f, 0.45f, 0.1f, 0.5f) };
            _mark = new MeshInstance3D { Name = "LandingMark", TopLevel = true, Mesh = new QuadMesh { Size = Vector2.One * (R0 * 2.6f) }, MaterialOverride = _markMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_mark); _mark.GlobalPosition = new Vector3(target.X, FloorY + 0.08f, target.Z); _mark.Rotation = new Vector3(-MathF.PI / 2f, 0f, 0f);
        }
        Apply();
    }

    /// <summary>A frame: the flight (the lob, the bounces, the roll, the wall), the shrink and the cooling, the burn on Mario.</summary>
    public void Tick(float dt, Mario mario, Vector3 centre, float wallR, out bool bounced, out bool burned, out bool landed)
    {
        bounced = false; burned = false; landed = false;
        if (Dead) return;
        Age += dt;
        Vector3 p = GlobalPosition;
        if (Fizzling)
        {
            _fizzleT += dt; _k = Mathf.Max(0f, 1f - _fizzleT / 0.3f); R = R0 * _k;
            Apply(); if (_k <= 0f) Die(); return;
        }
        // the size: shrinking to nothing over the life, the fade over its last part, the crust cooling
        float life01 = Mathf.Clamp(Age / Life, 0f, 1f);
        _k = Mathf.Max(0f, 1f - life01); R = R0 * _k;
        Heat = Mathf.Lerp(1f, _owner != null ? _owner.HeatAtEnd : 0.3f, life01);
        if (_k <= 0.06f) { Die(); return; }
        // the flight
        p += Vel * dt;
        if (_inAir)
        {
            _vy -= SmsFireballs.G * dt; p.Y += _vy * dt;
            if (p.Y <= FloorY + R && _vy < 0f)
            {
                p.Y = FloorY + R;
                float bounce = _owner != null ? _owner.Bounce : 0.35f;
                if (bounce > 0f && -_vy > 3f && Bounces < 2) { _vy = -_vy * bounce; Bounces++; bounced = true; _flare = 0.1f; Splat(p, 0.6f); }
                else
                {
                    _vy = 0f; _inAir = false; OnFloor = true; landed = true; _flare = 0.12f; Splat(p, 1f);
                    float roll = _owner != null ? _owner.RollSpeed : 0f; float sp = Vel.Length();
                    if (roll > 0f && sp > 1e-3f) Vel = Vel / sp * roll;
                    if (_mark != null) { _mark.QueueFree(); _mark = null; }
                }
            }
        }
        else p.Y = FloorY + R;
        // the wall: the flat way mirrored about the inward normal
        Vector3 d = new Vector3(p.X - centre.X, 0f, p.Z - centre.Z); float dist = d.Length();
        if (dist + R >= wallR && dist > 1e-4f)
        {
            Vector3 n = -d / dist;
            if (Vel.Dot(n) < 0f) { Vel = Vel - 2f * Vel.Dot(n) * n; Bounces++; bounced = true; _flare = 0.12f; }
            float over = dist + R - wallR; p -= d / dist * over;
        }
        GlobalPosition = p;
        // the crust rolls with the way it moves (in the air it turns as well: a tumbling bomb)
        float sp2 = Vel.Length();
        if (_core != null && sp2 > 1e-3f && R > 1e-3f) _core.Rotate(Vector3.Up.Cross(Vel / sp2).Normalized(), (OnFloor ? sp2 : sp2 * 0.5f) * dt / R);
        _flare -= dt;
        Apply();
        // the burn
        if (mario != null && GodotObject.IsInstanceValid(mario) && !HitMario)
        {
            Vector3 feet = mario.GlobalPosition; float mr = SmsParams.Mario.DamageRadius / U, mh = SmsParams.Mario.DamageHeight / U;
            if (feet.Y - FloorY < (_owner != null ? _owner.HitHeight : 1.6f) && p.Y + R >= feet.Y && p.Y - R <= feet.Y + mh && new Vector2(p.X - feet.X, p.Z - feet.Z).Length() <= R + mr)
            {
                int hp = mario.SmsHP; var was = mario.stateOfMario;
                mario.TakeDamage(p, Mario.SmsDamageProfile.Fire);
                if (mario.SmsHP < hp || (mario.stateOfMario == Mario.MarioState.hurt && was != Mario.MarioState.hurt))
                {
                    HitMario = true; burned = true;
                    if (_owner != null && _owner.DiesOnHit) Fizzle();
                }
            }
        }
    }
    private void Apply()
    {
        float s = Mathf.Max(0.01f, _k) * (_flare > 0f ? 1.25f : 1f);
        Scale = Vector3.One * s;
        float life01 = Mathf.Clamp(Age / Life, 0f, 1f), fadeFrom = _owner != null ? _owner.FadeFrom : 0.65f;
        float a = life01 > fadeFrom ? 1f - (life01 - fadeFrom) / Mathf.Max(0.05f, 1f - fadeFrom) : 1f;
        if (Fizzling) a = _k;
        _crust?.SetShaderParameter("heat", Heat * a);
        _crust?.SetShaderParameter("emissive", Fizzling ? 1f + 2f * (1f - _k) : 1f);
        if (_glowMat != null && _owner != null) _glowMat.AlbedoColor = new Color(_owner.GlowColor.R, _owner.GlowColor.G, _owner.GlowColor.B, 0.55f * a * (0.5f + 0.5f * Heat) * (_flare > 0f ? 1.5f : 1f));
        if (_light != null && _owner != null) _light.LightEnergy = _owner.LightEnergy * a * Mathf.Clamp(_k * 1.5f, 0.2f, 1f) * (0.5f + 0.5f * Heat);
        bool trailing = !Fizzling && a > 0.1f;
        foreach (var p in new[] { _embers, _drips, _smoke, _trail, _streaks }) if (p != null) { p.GlobalPosition = GlobalPosition; p.Emitting = trailing; }
        if (_drips != null) _drips.Emitting = trailing && Heat > 0.45f;                         // a cooled ball no longer drips
        // 204: the fire streams BACK the way the ball moves -- the trail's puffs and streaks are given the motion reversed as their way, their speed from the ball's; the ball's own flames lean back with it
        Vector3 v = new Vector3(Vel.X, _inAir ? _vy : 0f, Vel.Z); float speed = v.Length();
        if (speed > 0.3f)
        {
            Vector3 back = -v / speed; float roll = _owner != null && _owner.RollSpeed > 0.5f ? _owner.RollSpeed : 8f; float speedK = Mathf.Clamp(speed / roll, 0f, 1.5f);
            if (_trailPm != null) { float bk = (_owner != null ? _owner.TrailBack : 0.25f) * speed; _trailPm.Direction = back; _trailPm.InitialVelocityMin = bk * 0.6f; _trailPm.InitialVelocityMax = bk; _trailPm.ScaleMin = 0.85f * s; _trailPm.ScaleMax = 1.15f * s; }   // the puffs born as big as the ball is now (it shrinks over its life; the emitter is TopLevel, the ball's scale does not reach it)
            if (_streaksPm != null) { _streaksPm.Direction = back; _streaksPm.InitialVelocityMin = speed * 0.3f; _streaksPm.InitialVelocityMax = speed * 0.5f; }
            if (_flamesPm != null && _owner != null)
            {
                Vector3 flat = new Vector3(back.X, 0f, back.Z); float fl = flat.Length(); flat = fl > 1e-4f ? flat / fl : Vector3.Zero;
                Vector3 dir = (Vector3.Up + flat * (_owner.TrailLean * speedK)).Normalized();
                _flamesPm.Direction = dir; TrailLeanNow = dir.Dot(flat);
            }
        }
        if (_mark != null && _markMat != null) _markMat.AlbedoColor = new Color(1f, 0.45f, 0.1f, 0.3f + 0.3f * MathF.Sin(Age * 14f));
    }
    /// <summary>A splat of lava where the ball meets the floor: drops flung out (align_y streaks under gravity) and a small ring; k scales it.</summary>
    private void Splat(Vector3 at, float k)
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R * 0.5f, ParticleFlagAlignY = true,
            Direction = new Vector3(0f, 1f, 0f), Spread = 60f, InitialVelocityMin = 3f * k, InitialVelocityMax = 7f * k, Gravity = new Vector3(0f, -16f, 0f),
            ScaleMin = 0.7f, ScaleMax = 1.4f, Color = new Color(1f, 1f, 1f, 1f),
            ColorRamp = SmsFireballs.Fade(new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.55f, 0.1f, 1f), new Color(0.9f, 0.25f, 0.03f, 0.9f), new Color(0.4f, 0.05f, 0.01f, 0f))
        };
        var p = new GpuParticles3D { Name = "Splat", TopLevel = true, Amount = Mathf.Max(4, Mathf.RoundToInt(14f * k)), Lifetime = 0.55, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = new Vector2(0.1f, 0.4f), Material = SmsFireballs.AddStreak(SmsFireballs.Tex("glow1_spark")) }, VisibilityAabb = new Aabb(new Vector3(-10f, -5f, -10f), new Vector3(20f, 20f, 20f)) };
        (GetParent() ?? this).AddChild(p); p.GlobalPosition = new Vector3(at.X, FloorY + R * 0.3f, at.Z); p.Finished += p.QueueFree; p.Emitting = true;
        Ring(at, R * 4f * k, new Color(1f, 0.5f, 0.12f, 0.8f), 0.3f);
    }
    private void Ring(Vector3 at, float size, Color c, float seconds)
    {
        var rmat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = SmsFireballs.Tex("hamonring3_flat"), AlbedoColor = c };
        var ring = new MeshInstance3D { Name = "LavaRing", TopLevel = true, Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = rmat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        (GetParent() ?? this).AddChild(ring); ring.GlobalPosition = new Vector3(at.X, FloorY + 0.1f, at.Z); ring.Rotation = new Vector3(-MathF.PI / 2f, 0f, 0f);
        var tw = ring.CreateTween();
        tw.TweenMethod(Callable.From<float>(k => { ring.Scale = Vector3.One * Mathf.Max(0.05f, size * k); rmat.AlbedoColor = new Color(c.R, c.G, c.B, c.A * (1f - k)); }), 0f, 1f, seconds);
        tw.TweenCallback(Callable.From(ring.QueueFree));
    }
    /// <summary>Out in a few frames: the ball collapsing, no more hits. 170: a ball that got Mario BURSTS -- 172: a splash of lava, flames, a ring, smoke.</summary>
    public void Fizzle() { if (!Fizzling && !Dead) { bool burst = HitMario; Fizzling = true; _fizzleT = 0f; HitMario = true; if (_flames != null) _flames.Emitting = false; if (_mark != null) { _mark.QueueFree(); _mark = null; } if (burst) Burst(); } }
    private void Burst()
    {
        _burst = true;                                                                                 // 205: the burst stands in for the end puff
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R * 0.6f,
            Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 3f, InitialVelocityMax = 7f, Gravity = new Vector3(0f, 2f, 0f), DampingMin = 3f, DampingMax = 6f,
            ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsFireballs.Ramp(1f, 0.2f), AngleMin = -180f, AngleMax = 180f,
            Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.5f, 0.1f, 1f), new Color(0.8f, 0.12f, 0.02f, 0.7f), new Color(0.3f, 0.03f, 0.01f, 0f))
        };
        var p = new GpuParticles3D { Name = "Burst", TopLevel = true, Amount = 20, Lifetime = 0.5, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * (R0 * 1.6f), Material = SmsFireballs.AddBillboard(SmsFireballs.Tex("fire1_raw")) }, VisibilityAabb = new Aabb(new Vector3(-10f, -5f, -10f), new Vector3(20f, 20f, 20f)) };
        (GetParent() ?? this).AddChild(p); p.GlobalPosition = GlobalPosition; p.Finished += p.QueueFree; p.Emitting = true;
        Splat(GlobalPosition, 1.6f);                                                                   // the lava flung out
        var sm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.5f,
            Direction = new Vector3(0f, 1f, 0f), Spread = 60f, InitialVelocityMin = 1.5f, InitialVelocityMax = 3.5f, Gravity = new Vector3(0f, 2f, 0f), DampingMin = 1f, DampingMax = 2f,
            ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsFireballs.Ramp(0.6f, 1.8f), AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -40f, AngularVelocityMax = 40f,
            Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(0.3f, 0.15f, 0.1f, 0f), new Color(0.15f, 0.11f, 0.1f, 0.6f), new Color(0.12f, 0.1f, 0.1f, 0.35f), new Color(0.1f, 0.1f, 0.1f, 0f))
        };
        var smat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsFireballs.Tex("kemuball3_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        var s = new GpuParticles3D { Name = "BurstSmoke", TopLevel = true, Amount = 8, Lifetime = 1.1, OneShot = true, Explosiveness = 0.8f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = sm, DrawPass1 = new QuadMesh { Size = Vector2.One * (R0 * 1.4f), Material = smat }, VisibilityAabb = new Aabb(new Vector3(-10f, -5f, -10f), new Vector3(20f, 20f, 20f)) };
        (GetParent() ?? this).AddChild(s); s.GlobalPosition = GlobalPosition; s.Finished += s.QueueFree; s.Emitting = true;
        Ring(GlobalPosition, R0 * 6f, new Color(1f, 0.6f, 0.2f, 0.9f), 0.35f);
        SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, 0.5f);
    }
    private void Die()
    {
        if (!_burst) EndPuff();                                                                        // 205: the little puff as it goes out (the burst on Mario brought its own)
        Dead = true; Visible = false;
        if (_light != null) _light.LightEnergy = 0f;
        foreach (var p in new[] { _embers, _drips, _smoke, _trail, _streaks }) if (p != null) p.Emitting = false;
        if (_mark != null) { _mark.QueueFree(); _mark = null; }
        GetTree().CreateTimer(1.3f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
    private bool _burst;
    /// <summary>205, for the drives: the puffs this ball made going out (0 or 1).</summary>
    public int EndPuffsFired { get; private set; }
    /// <summary>205: the small puff where the ball goes out -- dark smoke rising and spreading, a few embers flung out and falling, a small ring of light; sized from the
    /// ball's BIRTH radius (EndPuffScale x it: the ball itself has shrunk to nothing by its end), left in the world (TopLevel one-shots that free themselves).</summary>
    private void EndPuff()
    {
        if (_owner == null || !_owner.EndPuff || !IsInsideTree()) return;
        float s = Mathf.Max(0.3f, R0 * _owner.EndPuffScale);
        Vector3 at = GlobalPosition;
        if (_owner.EndPuffs > 0)
        {
            var sm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = s * 0.3f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 70f, InitialVelocityMin = 0.8f, InitialVelocityMax = 2f, Gravity = new Vector3(0f, 1f, 0f), DampingMin = 1.5f, DampingMax = 3f,
                ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsFireballs.Ramp(0.6f, 1.35f), AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -60f, AngularVelocityMax = 60f,   // run cw: 0.5 -> 1.7 spread into a pale haze; tighter, and darker below
                Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(0.45f, 0.26f, 0.16f, 0.2f), new Color(0.17f, 0.12f, 0.1f, 0.9f), new Color(0.11f, 0.09f, 0.09f, 0.55f), new Color(0.08f, 0.08f, 0.08f, 0f))
            };
            var smat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsFireballs.Tex("kemuball3_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
            var p = new GpuParticles3D { Name = "EndPuff", TopLevel = true, Amount = _owner.EndPuffs, Lifetime = 0.7, OneShot = true, Explosiveness = 0.9f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = sm, DrawPass1 = new QuadMesh { Size = Vector2.One * (s * 1.1f), Material = smat }, VisibilityAabb = new Aabb(new Vector3(-8f, -4f, -8f), new Vector3(16f, 12f, 16f)) };
            (GetParent() ?? this).AddChild(p); p.GlobalPosition = at; p.Finished += p.QueueFree; p.Emitting = true;
        }
        if (_owner.EndSparks > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = s * 0.3f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 2f, InitialVelocityMax = 5f, Gravity = new Vector3(0f, -8f, 0f), DampingMin = 0.5f, DampingMax = 1.5f,
                ScaleMin = 0.7f, ScaleMax = 1.3f, Color = new Color(_owner.EmberColor.R, _owner.EmberColor.G, _owner.EmberColor.B, 1f),
                ColorRamp = SmsFireballs.Fade(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f), new Color(1f, 0.55f, 0.25f, 0.7f), new Color(0.8f, 0.2f, 0.05f, 0f))
            };
            var p = new GpuParticles3D { Name = "EndSparks", TopLevel = true, Amount = _owner.EndSparks, Lifetime = 0.55, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * 0.16f, Material = SmsFireballs.AddBillboard(SmsFireballs.Tex("glow1_spark")) }, VisibilityAabb = new Aabb(new Vector3(-8f, -4f, -8f), new Vector3(16f, 12f, 16f)) };
            (GetParent() ?? this).AddChild(p); p.GlobalPosition = at; p.Finished += p.QueueFree; p.Emitting = true;
        }
        // the flash: one glow, bright at once, swelling a little and gone in a quarter second (the "pop" of the puff; run cw had the smoke alone, a haze)
        var fm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point, Direction = Vector3.Up, Spread = 0f, InitialVelocityMin = 0f, InitialVelocityMax = 0f, Gravity = Vector3.Zero, ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsFireballs.Ramp(0.7f, 1.5f), Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.6f, 0.2f, 0.9f), new Color(1f, 0.35f, 0.08f, 0.4f), new Color(0.6f, 0.1f, 0.02f, 0f)) };
        var f = new GpuParticles3D { Name = "EndFlash", TopLevel = true, Amount = 1, Lifetime = 0.26, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = fm, DrawPass1 = new QuadMesh { Size = Vector2.One * (s * 2.4f), Material = SmsFireballs.AddBillboard(SmsFireballs.Tex("glow3_raw")) }, VisibilityAabb = new Aabb(new Vector3(-8f, -4f, -8f), new Vector3(16f, 12f, 16f)) };
        (GetParent() ?? this).AddChild(f); f.GlobalPosition = at + Vector3.Up * (s * 0.3f); f.Finished += f.QueueFree; f.Emitting = true;
        Ring(at, s * 2.8f, new Color(1f, 0.55f, 0.15f, 0.9f), 0.3f);
        EndPuffsFired++; _owner.EndPuffsMade++;
    }
}
