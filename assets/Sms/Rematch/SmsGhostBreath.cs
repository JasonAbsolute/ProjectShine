using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 167: GHOST BREATH (KINGBOO_REMATCH_gdoc.txt, attack 4: "Crown glows frosty blue. He blows cold breath in a sweep across the arena, left to
/// right or right to left. A hit freezes Mario for one second"). King Boo makes one (his Breath move) at his mouth and it runs on its own: a STREAM of
/// cold -- puffs of pale blue mist (the port's smoke ball, mix-blended), ice glints in it, a cold light at the mouth -- blown out at BreathSpeed, aimed
/// DownDeg under level so it reaches the floor and ROLLS along it (the puffs sink at Sink and collide with a slab under the floor), each puff growing from
/// PuffSizeStart to PuffSizeEnd over its PuffSeconds (its reach: BreathSpeed x PuffSeconds, 33 m), the mouth SWEEPING through SweepDeg about the way to
/// Mario over SweepSeconds -- from his left to his right as he faces King Boo (SweepWay LeftToRight), the other way, or a coin (the doc). THE COLD is the
/// stream itself: every frame of blowing is a ray from the mouth, and the puff that left along it is where its age puts it (BreathSpeed out, the sink,
/// the floor); a live puff (age under PuffSeconds) whose ball -- HitRadiusK of its size -- meets Mario's damage cylinder (42 u across, 130 u tall from
/// his feet) is THE HIT, unless his feet are HitHeight or more over the floor: the cold hugs the floor, a jump clears it (the fight's dodge; the sweep
/// crosses a spot in a third of a second). The hit is the FREEZE -- Mario.SmsFreezeFor(FreezeSeconds): the doc's one second, "the one reaction Sunshine
/// does not have" -- nothing moves him, his pose is held, his body carries the frost fog, no HP; and the ICE about him (<see cref="SmsIceBlock"/>), which
/// shatters as he thaws. Once per attack unless OneFreezePerAttack is off (the stream passes once anyway). MSD_SE_BS_TELESA_BREATHE as the blow begins (and
/// every BreathSeEvery), the bottle's wind under it. <see cref="Cancel"/> (King Boo hit through the attack) cuts the breath off: no more cold, the puffs
/// out fade. Signals Swept(hit) as the sweep ends, Finished as the last puff goes.
/// </summary>
public partial class SmsGhostBreath : Node3D
{
    public enum Way { Random, LeftToRight, RightToLeft }
    public enum Phase { Idle, Blow, Tail, Done }

    [ExportGroup("The sweep")]
    /// <summary>The whole arc the mouth sweeps (degrees, centred on the way to Mario), how long it takes, which way (as Mario sees it facing King Boo; Random:
    /// a coin), how far under level the breath is aimed (degrees; it meets the floor about 10 m out and rolls on).</summary>
    [Export] public float SweepDeg = 150f, SweepSeconds = 1.6f;
    [Export] public Way SweepWay = Way.Random;
    [Export] public float DownDeg = 10f;
    /// <summary>How fast the cold blows (m/s), how long a puff of it lives (seconds; its reach is the two multiplied), how hard it sinks (m/s^2), how wide
    /// the stream scatters (degrees either side).</summary>
    [Export] public float BreathSpeed = 22f, PuffSeconds = 1.5f, Sink = 4f, SpreadDeg = 6f;
    [ExportGroup("The hit")]
    /// <summary>A puff's size as it leaves the mouth and at its end (metres across), what part of that is the cold that hits (0.4: the smoke ball's solid
    /// middle), how high over the floor the cold reaches -- a Mario whose feet are higher is over it -- and how long the freeze holds him.</summary>
    [Export] public float PuffSizeStart = 2f, PuffSizeEnd = 6f;
    [Export(PropertyHint.Range, "0.1,0.6,0.01")] public float HitRadiusK = 0.4f;
    [Export] public float HitHeight = 1.2f, FreezeSeconds = 1f;
    [Export] public bool OneFreezePerAttack = true;
    [ExportGroup("The look")]
    /// <summary>The mist's colour and how solid it is, the glints' colour, how many puffs and glints the stream carries, the cold light at the mouth (metres
    /// across; 0 = none).</summary>
    [Export] public Color BreathColor = new Color(0.8f, 0.93f, 1f), GlintColor = new Color(0.6f, 0.95f, 1f);
    [Export(PropertyHint.Range, "0,1,0.01")] public float PuffAlpha = 0.55f;
    [Export(PropertyHint.Range, "0,600,1")] public int Puffs = 320;
    [Export(PropertyHint.Range, "0,200,1")] public int Glints = 90;
    [Export] public float MouthGlowSize = 3f;
    /// <summary>A glint's size (metres; the quad -- the process material's scale range did not take here).</summary>
    [Export] public float GlintSize = 0.45f;
    [ExportGroup("The sounds")]
    [Export] public string BreathSe = "MSD_SE_BS_TELESA_BREATHE", WindSe = "MSD_SE_MA_BOTTLE_WIND";
    /// <summary>The breathe sound again this often through the blow (0 = once).</summary>
    [Export] public float BreathSeEvery = 0.8f;
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void SweptEventHandler(bool hit);
    [Signal] public delegate void FinishedEventHandler();

    public Phase State { get; private set; } = Phase.Idle;
    public int Hits { get; private set; }
    public bool HitMario => _hit;
    /// <summary>The sweep: 0 at its start, 1 at its end; the mouth's yaw now and the way it blows; where it started and where it ends; which way (+1: from
    /// Mario's left); the floor; the mouth now; whether it is blowing; the yaw it was centred on.</summary>
    public float SweepK => Mathf.Clamp(_t / Mathf.Max(0.01f, SweepSeconds), 0f, 1f);
    public float YawNow => _yawNow;
    public Vector3 DirNow => _dirNow;
    public float StartYaw => _yawCentre - _side * Mathf.DegToRad(SweepDeg) * 0.5f;
    public float EndYaw => _yawCentre + _side * Mathf.DegToRad(SweepDeg) * 0.5f;
    public float CentreYaw => _yawCentre;
    public int Side => _side;
    public float FloorY { get; private set; }
    public Vector3 MouthPoint => _mouthNow;
    public bool Emitting => _puffs != null && _puffs.Emitting;
    public int RaysBlown => _rays.Count;
    public float Age => _age;
    public Vector3 FreezePoint { get; private set; }
    public float FreezeAt { get; private set; } = -1f;
    public static readonly List<SmsGhostBreath> All = new();
    public static int FreezeCount;

    private const float U = 74.44f;
    private Mario _mario; private Func<Vector3> _mouth; private SmsActorSound _sound; private readonly Random _rng = new(167);
    private GpuParticles3D _puffs, _glints; private MeshInstance3D _glow; private StandardMaterial3D _glowMat; private GpuParticlesCollisionBox3D _slab;
    private readonly List<(float t, Vector3 o, Vector3 d)> _rays = new();
    private float _t, _age, _yawCentre, _yawNow, _seT, _tailT; private int _side = 1; private Vector3 _dirNow = Vector3.Forward, _mouthNow; private bool _hit;

    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>Which side the sweep starts from for a way: +1 is Mario's LEFT as he faces King Boo (the start direction, yawCentre less half the arc, points
    /// to his left), -1 his right; Random a coin from the caller's dice.</summary>
    public static int SideFor(Way way, float yawCentre, Random rng)
    {
        if (way == Way.Random) return rng.NextDouble() < 0.5 ? 1 : -1;
        // King Boo looks along yawCentre at Mario; Mario looks back: his left is up x (-that way). The start with side +1 is at yawCentre - half.
        Vector3 toMario = new Vector3(MathF.Sin(yawCentre), 0f, MathF.Cos(yawCentre));
        Vector3 marioLeft = Vector3.Up.Cross(-toMario);
        float ys = yawCentre - Mathf.DegToRad(60f);
        bool plusIsLeft = new Vector3(MathF.Sin(ys), 0f, MathF.Cos(ys)).Dot(marioLeft) > 0f;
        return (way == Way.LeftToRight) == plusIsLeft ? 1 : -1;
    }

    /// <summary>The breath begins: from the mouth (read every frame), sweeping about yawCentre (the way to Mario) from the side given, over the floor at floorY.</summary>
    public void Begin(Mario mario, float floorY, Func<Vector3> mouth, float yawCentre, int side)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY; _mouth = mouth; _yawCentre = yawCentre; _side = side >= 0 ? 1 : -1;
        _sound = SmsActorSound.Attach(this);
        _mouthNow = mouth();
        GlobalPosition = _mouthNow;
        _puffs = MakePuffs(); _glints = MakeGlints(); MakeGlow(); MakeSlab();
        State = Phase.Blow; _t = 0f; _age = 0f; _seT = 0f;
        Aim(0f);
        if (_puffs != null) { _puffs.Visible = true; _puffs.Emitting = true; }
        if (_glints != null) { _glints.Visible = true; _glints.Emitting = true; }
        if (_glow != null) _glow.Visible = true;
        if (_sound != null) { if (!string.IsNullOrEmpty(BreathSe)) _sound.Play(BreathSe); if (!string.IsNullOrEmpty(WindSe)) _sound.Play(WindSe, -6f); }
        if (DebugPrint) GD.Print($"[SMS breath] {Name}: BLOWS from {_mouthNow} ({_mouthNow.Y - FloorY:F2} m over the floor), {SweepDeg} deg over {SweepSeconds} s from Mario's {(_side > 0 ? "left" : "right")} (yaw {Mathf.RadToDeg(StartYaw):F0} -> {Mathf.RadToDeg(EndYaw):F0}), {BreathSpeed} m/s for {PuffSeconds} s ({BreathSpeed * PuffSeconds:F0} m), {DownDeg} deg down");
    }

    private float PuffSize(float age) => Mathf.Lerp(PuffSizeStart, PuffSizeEnd, Mathf.Clamp(age / Mathf.Max(0.01f, PuffSeconds), 0f, 1f));
    /// <summary>Where the puff blown along a ray is at this age: out at BreathSpeed, sunk, no lower than its ball on the floor.</summary>
    private Vector3 PuffCentre((float t, Vector3 o, Vector3 d) ray, float age)
    {
        Vector3 c = ray.o + ray.d * (BreathSpeed * age);
        c.Y -= 0.5f * Sink * age * age;
        float r = PuffSize(age) * HitRadiusK;
        if (c.Y < FloorY + r) c.Y = FloorY + r;
        return c;
    }
    /// <summary>The nearest live cold to a point on the floor: the smallest (flat distance to a puff's middle less its ball), or a big number.</summary>
    public float NearestCold(Vector3 feet)
    {
        float best = 999f;
        foreach (var ray in _rays)
        {
            float age = _age - ray.t; if (age < 0f || age > PuffSeconds) continue;
            Vector3 c = PuffCentre(ray, age);
            best = Mathf.Min(best, new Vector2(c.X - feet.X, c.Z - feet.Z).Length() - PuffSize(age) * HitRadiusK);
        }
        return best;
    }
    /// <summary>A live puff's ball meeting Mario's damage cylinder, his feet under HitHeight over the floor.</summary>
    private bool ColdOn(Vector3 feet, out Vector3 at)
    {
        at = feet;
        if (feet.Y - FloorY >= HitHeight) return false;
        float mr = SmsParams.Mario.DamageRadius / U, mh = SmsParams.Mario.DamageHeight / U;
        foreach (var ray in _rays)
        {
            float age = _age - ray.t; if (age < 0f || age > PuffSeconds) continue;
            Vector3 c = PuffCentre(ray, age); float r = PuffSize(age) * HitRadiusK;
            if (c.Y + r < feet.Y || c.Y - r > feet.Y + mh) continue;
            if (new Vector2(c.X - feet.X, c.Z - feet.Z).Length() <= r + mr) { at = c; return true; }
        }
        return false;
    }

    private void Aim(float k)
    {
        _yawNow = _yawCentre + _side * Mathf.DegToRad(SweepDeg) * (k - 0.5f);
        float d = Mathf.DegToRad(DownDeg);
        _dirNow = new Vector3(MathF.Sin(_yawNow) * MathF.Cos(d), -MathF.Sin(d), MathF.Cos(_yawNow) * MathF.Cos(d));
        _mouthNow = _mouth != null ? _mouth() : GlobalPosition;
        GlobalPosition = _mouthNow;
        var basis = Basis.LookingAt(_dirNow, Vector3.Up);
        foreach (var p in new[] { _puffs, _glints }) if (p != null) { p.GlobalPosition = _mouthNow; p.GlobalBasis = basis; }
        if (_glow != null) _glow.GlobalPosition = _mouthNow + _dirNow * 0.6f;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (State == Phase.Idle || State == Phase.Done) return;
        _age += dt;
        if (State == Phase.Blow)
        {
            _t += dt; _seT += dt;
            Aim(SweepK);
            _rays.Add((_age, _mouthNow, _dirNow));
            if (_glowMat != null) _glowMat.AlbedoColor = new Color(GlintColor.R, GlintColor.G, GlintColor.B, 0.45f + 0.2f * MathF.Sin(_age * 22f));
            if (BreathSeEvery > 0f && _seT >= BreathSeEvery && _sound != null && !string.IsNullOrEmpty(BreathSe)) { _seT = 0f; _sound.Play(BreathSe); }
            if (_t >= SweepSeconds)
            {
                State = Phase.Tail; _tailT = 0f;
                if (_puffs != null) _puffs.Emitting = false;
                if (_glints != null) _glints.Emitting = false;
                if (DebugPrint) GD.Print($"[SMS breath] {Name}: the sweep is over ({_rays.Count} rays) -- {(_hit ? "it FROZE Mario" : "a miss so far")}; the cold rolls on {PuffSeconds:F1} s");
                EmitSignal(SignalName.Swept, _hit);
            }
        }
        else if (State == Phase.Tail)
        {
            _tailT += dt;
            if (_glowMat != null) _glowMat.AlbedoColor = new Color(GlintColor.R, GlintColor.G, GlintColor.B, Mathf.Max(0f, 0.5f - _tailT * 2f));
            if (_glow != null && _tailT > 0.3f) _glow.Visible = false;
            if (_tailT >= PuffSeconds + 0.2f) { Finish(); return; }
        }
        HitCheck();
    }

    private void HitCheck()
    {
        if (_mario == null || !IsInstanceValid(_mario)) return;
        if (OneFreezePerAttack && Hits > 0) return;
        if (_mario.SmsFrozen) return;
        Vector3 feet = _mario.GlobalPosition;
        if (!ColdOn(feet, out Vector3 at)) return;
        if (!_mario.SmsFreezeFor(FreezeSeconds)) return;                    // refused: invincible, down, a demo...
        _hit = true; Hits++; FreezeCount++; FreezePoint = at; FreezeAt = _age;
        SmsIceBlock.Freeze(_mario, GetParent() ?? this, DebugPrint);
        if (DebugPrint) GD.Print($"[SMS breath] {Name}: the cold GETS Mario at {feet} ({new Vector2(at.X - _mouthNow.X, at.Z - _mouthNow.Z).Length():F1} m from the mouth, {_age:F2} s in, sweep {SweepK:F2}) -- FROZEN {FreezeSeconds:F1} s, hp {_mario.SmsHP}");
    }

    private GpuParticles3D MakePuffs()
    {
        if (Puffs <= 0) return null;
        var tex = Tex("kemuball3_raw");
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.35f,
            Direction = new Vector3(0f, 0f, -1f), Spread = Mathf.Max(0.5f, SpreadDeg), InitialVelocityMin = BreathSpeed * 0.85f, InitialVelocityMax = BreathSpeed,
            Gravity = new Vector3(0f, -Sink, 0f), ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = Ramp(PuffSizeStart, PuffSizeEnd),
            AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -40f, AngularVelocityMax = 40f,
            Color = new Color(BreathColor.R, BreathColor.G, BreathColor.B, 1f),
            ColorRamp = Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, PuffAlpha), new Color(0.92f, 0.96f, 1f, PuffAlpha * 0.75f), new Color(0.9f, 0.95f, 1f, 0f)),
            CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Rigid, CollisionFriction = 0.15f, CollisionBounce = 0f, CollisionUseScale = true
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = "ColdPuffs", TopLevel = true, CollisionBaseSize = HitRadiusK, Amount = Puffs, Lifetime = PuffSeconds, Emitting = false, Visible = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 40f, 120f)) };
        AddChild(p);
        return p;
    }
    private GpuParticles3D MakeGlints()
    {
        if (Glints <= 0) return null;
        var tex = Tex("star4s_raw");
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.3f,
            Direction = new Vector3(0f, 0f, -1f), Spread = Mathf.Max(0.5f, SpreadDeg) * 1.4f, InitialVelocityMin = BreathSpeed * 0.9f, InitialVelocityMax = BreathSpeed * 1.1f,
            Gravity = new Vector3(0f, -Sink * 0.8f, 0f), ScaleMin = 0.7f, ScaleMax = 1.2f, AngularVelocityMin = -300f, AngularVelocityMax = 300f,
            Color = new Color(GlintColor.R, GlintColor.G, GlintColor.B, 1f),
            ColorRamp = Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0f)),
            CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Rigid, CollisionFriction = 0.3f, CollisionBounce = 0.2f
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = "IceGlints", TopLevel = true, CollisionBaseSize = 0.1f, Amount = Glints, Lifetime = PuffSeconds * 0.8, Emitting = false, Visible = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * GlintSize, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 40f, 120f)) };
        AddChild(p);
        return p;
    }
    private void MakeGlow()
    {
        if (MouthGlowSize <= 0f) return;
        _glowMat = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = true,
            AlbedoTexture = Tex("radgrad_raw"), AlbedoColor = new Color(GlintColor.R, GlintColor.G, GlintColor.B, 0.5f), RenderPriority = 3
        };
        _glow = new MeshInstance3D { Name = "ColdLight", TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One * MouthGlowSize }, MaterialOverride = _glowMat };
        AddChild(_glow);
    }
    /// <summary>A slab under the floor for the puffs to roll along (GPU particles collide with these, not with the world).</summary>
    private void MakeSlab()
    {
        _slab = new GpuParticlesCollisionBox3D { Name = "FloorSlab", TopLevel = true, Size = new Vector3(160f, 2f, 160f) };
        AddChild(_slab);
        _slab.GlobalPosition = new Vector3(_mouthNow.X, FloorY - 1f, _mouthNow.Z);
    }
    private static Texture2D Tex(string name) { string p = $"res://assets/Sms/Fx/tex/{name}.png"; return ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null; }
    private static CurveTexture Ramp(float a, float b)
    {
        var c = new Curve { MinValue = 0f, MaxValue = Mathf.Max(1f, Mathf.Max(a, b) * 1.1f) };
        c.AddPoint(new Vector2(0f, a)); c.AddPoint(new Vector2(1f, b));
        return new CurveTexture { Curve = c };
    }
    private static GradientTexture1D Fade(Color c0, Color c1, Color c2, Color c3)
    {
        var g = new Gradient { Offsets = new float[] { 0f, 0.1f, 0.65f, 1f }, Colors = new Color[] { c0, c1, c2, c3 } };
        return new GradientTexture1D { Gradient = g };
    }

    private void Finish()
    {
        State = Phase.Done; All.Remove(this);
        if (DebugPrint) GD.Print($"[SMS breath] {Name}: finished -- {Hits} freeze{(Hits == 1 ? "" : "s")}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through the attack: the breath cuts off -- no more cold; the puffs already out fade on their own, then the node goes.</summary>
    public void Cancel()
    {
        if (State == Phase.Done) return;
        State = Phase.Done; All.Remove(this);
        if (_puffs != null) _puffs.Emitting = false;
        if (_glints != null) _glints.Emitting = false;
        if (_glow != null) _glow.Visible = false;
        if (DebugPrint) GD.Print($"[SMS breath] {Name}: cut off at sweep {SweepK:F2}");
        EmitSignal(SignalName.Finished);
        GetTree().CreateTimer(PuffSeconds + 0.2f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
}
