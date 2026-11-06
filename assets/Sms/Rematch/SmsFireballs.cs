using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 168: FIREBALLS (KINGBOO_REMATCH_gdoc.txt, attack 5: "Crown glows red. Fireballs shoot off in random directions and bounce off the arena walls.
/// They shrink as they travel and fade out"; the hit: "the burn. Set fireDown on the profile, which routes into SmsFireDownEnter and the ms_firedmg_a
/// cloud" -- Mario.SmsDamageProfile.Fire, the fire enemies' own). King Boo makes one at his mouth and it runs on its own: a VOLLEY of Count fireballs
/// spat over VolleySeconds, each along a random way within SpreadDeg about the way to Mario (the doc's random directions; the bounces scatter them further),
/// at Speed, dropping from the mouth to the floor over DropSeconds and ROLLING on along it. A fireball (<see cref="SmsFireball"/>) is a ball of fire Size
/// metres across at its birth -- a white-hot core (the port's fire smoke ball, additive), flames licking up off it (fire1_raw, yellow to red), embers trailing in
/// the world, a light on the floor -- that SHRINKS to nothing over LifeSeconds and fades over the last of them, BOUNCING off the arena's wall (the floor's
/// circle less WallInset: its flat way mirrored about the wall's normal, a flare, MSD_SE_MA_BOUND) as often as it reaches it. THE HIT: a fireball's ball
/// meeting Mario's damage cylinder is Mario.TakeDamage(the ball, Fire) -- the burn: FIRE_DOWN's launch, ma_firejmp, the fire cloud, 1 HP, 300 ticks
/// invincible (its own refusals: invincible, down, a demo) -- and the ball bursts (DiesOnHit) -- unless his feet are HitHeight over the floor: a jump clears a rolling ball, a hop does not.
/// MSD_SE_BS_TELESA_FIRE as the volley begins (the casino boss's own fire), a whoosh per ball. <see cref="Cancel"/> (King Boo hit through it) fizzles every ball
/// out. Signals Shot(count) as the last ball leaves, Burned per burn, Bounced per bounce, Finished as the last ball dies.
/// </summary>
public partial class SmsFireballs : Node3D
{
    public enum Phase { Idle, Volley, Flying, Done }
    [ExportGroup("The volley")]
    /// <summary>How many fireballs, over how long, within what arc about the way to Mario (degrees; 0 = all straight at him), how fast they fly (m/s), how long
    /// one takes to drop from the mouth to the floor.</summary>
    [Export(PropertyHint.Range, "1,24,1")] public int Count = 8;
    [Export] public float VolleySeconds = 1f, SpreadDeg = 140f, Speed = 14f, DropSeconds = 0.5f;
    [ExportGroup("The fireballs")]
    /// <summary>A fireball's size at birth (metres across its ball: the radius is half), how long it lives (it shrinks to nothing over this), where its fade
    /// begins (0..1 of the life), how far in from the floor's edge the wall is, whether the ball bursts on Mario.</summary>
    [Export] public float Size = 2f, LifeSeconds = 5f, WallInset = 0.6f;
    /// <summary>How high over the floor the fire reaches: a Mario whose feet are higher is over it (the jump; a hop is not enough).</summary>
    [Export] public float HitHeight = 1.6f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float FadeFrom = 0.65f;
    [Export] public bool DiesOnHit = true;
    [ExportGroup("The look")]
    [Export] public Color CoreColor = new Color(1f, 0.72f, 0.28f), FlameColor = new Color(1f, 0.42f, 0.06f), EmberColor = new Color(1f, 0.7f, 0.25f);
    [Export] public bool Lights = true;
    [Export] public float LightEnergy = 2.5f, LightRange = 8f;
    [Export(PropertyHint.Range, "0,64,1")] public int Flames = 26;
    [Export(PropertyHint.Range, "0,64,1")] public int Embers = 14;
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
    public int Burns { get; private set; }
    public int BouncesMade { get; private set; }
    public int Alive => _balls.Count;
    public IReadOnlyList<SmsFireball> Balls => _balls;
    public float FloorY { get; private set; }
    public Vector3 ArenaCentre => _centre;
    public float ArenaRadius => _radius;
    public float CentreYaw => _yawCentre;
    public static readonly List<SmsFireballs> All = new();
    public static int BurnCount;

    private const float U = 74.44f;
    private Mario _mario; private Func<Vector3> _mouth; private SmsActorSound _sound; private readonly Random _rng = new(168);
    private readonly List<SmsFireball> _balls = new(); private readonly List<SmsFireball> _dead = new();
    private Vector3 _centre; private float _radius = 26f, _yawCentre, _t, _nextAt;

    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>The volley begins: from the mouth (read per ball), about yawCentre (the way to Mario), inside the arena given, over the floor at floorY.</summary>
    public void Begin(Mario mario, float floorY, Func<Vector3> mouth, float yawCentre, Vector3 arenaCentre, float arenaRadius)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY; _mouth = mouth; _yawCentre = yawCentre; _centre = arenaCentre; _radius = Mathf.Max(5f, arenaRadius);
        _sound = SmsActorSound.Attach(this);
        GlobalPosition = mouth();
        State = Phase.Volley; _t = 0f; _nextAt = 0f;
        if (_sound != null && !string.IsNullOrEmpty(VolleySe)) _sound.Play(VolleySe);
        if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: the VOLLEY -- {Count} over {VolleySeconds:F2} s within {SpreadDeg} deg of yaw {Mathf.RadToDeg(yawCentre):F0}, {Speed} m/s, {Size} m across, {LifeSeconds} s; the wall at {_radius - WallInset:F1} m about {_centre}");
    }

    private void Spit()
    {
        float yaw = _yawCentre + ((float)_rng.NextDouble() - 0.5f) * Mathf.DegToRad(SpreadDeg);
        Vector3 dir = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        Vector3 from = _mouth != null ? _mouth() : GlobalPosition;
        var b = new SmsFireball { Name = $"Fireball{Spat + 1}" };
        (GetParent() ?? this).AddChild(b);
        b.Setup(this, from, dir * Speed, Size * 0.5f, LifeSeconds, FloorY, DropSeconds);
        _balls.Add(b); Spat++;
        if (_sound != null && !string.IsNullOrEmpty(SpitSe)) _sound.Play(SpitSe, -4f, 0.9f + 0.2f * (float)_rng.NextDouble());
        if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: fireball {Spat} of {Count} spat from {from} along yaw {Mathf.RadToDeg(yaw):F0}");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (State == Phase.Idle) return;
        _t += dt;
        if (State == Phase.Volley)
        {
            float step = Count > 1 ? VolleySeconds / (Count - 1) : 0f;
            while (Spat < Count && _t >= _nextAt) { Spit(); _nextAt += Mathf.Max(0.01f, step); }
            if (Spat >= Count) { State = Phase.Flying; EmitSignal(SignalName.Shot, Spat); }
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
            b.Tick(dt, _mario, _centre, _radius - WallInset, out bool bounced, out bool burned);
            if (bounced) { BouncesMade++; if (_sound != null && !string.IsNullOrEmpty(BounceSe)) _sound.Play(BounceSe, -6f, 0.8f); EmitSignal(SignalName.Bounced); }
            if (burned) { Burns++; BurnCount++; if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: {b.Name} BURNS Mario at {b.GlobalPosition} (r {b.R:F2}, age {b.Age:F2}); hp {_mario?.SmsHP}"); EmitSignal(SignalName.Burned); }
            if (b.Dead) _dead.Add(b);
        }
        foreach (var b in _dead) _balls.Remove(b);
    }
    private void Finish()
    {
        State = Phase.Done; All.Remove(this);
        if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: finished -- {Spat} spat, {BouncesMade} bounce{(BouncesMade == 1 ? "" : "s")}, {Burns} burn{(Burns == 1 ? "" : "s")}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through the attack: no more spat, every ball fizzles out.</summary>
    public void Cancel()
    {
        if (State == Phase.Done) return;
        State = Phase.Done; All.Remove(this);
        foreach (var b in _balls) if (IsInstanceValid(b)) b.Fizzle();
        if (DebugPrint) GD.Print($"[SMS fireballs] {Name}: cut off after {Spat} of {Count} ({_balls.Count} fizzling)");
        EmitSignal(SignalName.Finished);
        GetTree().CreateTimer(0.6f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
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

/// <summary>One fireball (168): the ball of fire and its flight -- the drop from the mouth to the floor, the roll at constant speed, the bounces off the arena
/// wall, the shrink to nothing over its life, the burn on Mario. The flames scale with the node (local coordinates); the embers trail in the world.</summary>
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
    public float ScaleShown => _k;

    private const float U = 74.44f;
    private SmsFireballs _owner; private MeshInstance3D _core; private StandardMaterial3D _coreMat; private GpuParticles3D _flames, _embers; private OmniLight3D _light;
    private float _vy, _drop, _k = 1f, _flare, _fizzleT; private bool _dropping;

    public void Setup(SmsFireballs owner, Vector3 from, Vector3 vel, float radius, float life, float floorY, float dropSeconds)
    {
        _owner = owner; Vel = vel; R0 = R = Mathf.Max(0.1f, radius); Life = Mathf.Max(0.5f, life); FloorY = floorY; GlobalPosition = from;
        float h = from.Y - (floorY + R0);
        _drop = Mathf.Max(0.05f, dropSeconds); _dropping = h > 0.05f;
        _vy = _dropping ? (-h / _drop + 0.5f * 20f * _drop) : 0f;                          // a lob: down to the floor in DropSeconds under 20 m/s^2
        _coreMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = SmsFireballs.Tex("kemuball3_fire"), AlbedoColor = owner.CoreColor, RenderPriority = 2 };
        _core = new MeshInstance3D { Name = "Core", Mesh = new QuadMesh { Size = Vector2.One * (R0 * 1.7f) }, MaterialOverride = _coreMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_core);
        if (owner.Flames > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.55f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 40f, InitialVelocityMin = R0 * 2f, InitialVelocityMax = R0 * 4.5f, Gravity = new Vector3(0f, R0 * 3f, 0f),
                ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsFireballs.Ramp(1f, 0.25f), AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -90f, AngularVelocityMax = 90f,
                Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(1f, 0.9f, 0.35f, 1f), owner.FlameColor, new Color(0.8f, 0.1f, 0.02f, 0.85f), new Color(0.4f, 0.03f, 0.01f, 0f))
            };
            var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsFireballs.Tex("fire1_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
            _flames = new GpuParticles3D { Name = "Flames", Amount = owner.Flames, Lifetime = 0.4, Emitting = true, LocalCoords = true, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * (R0 * 1.7f), Material = mat }, VisibilityAabb = new Aabb(new Vector3(-6f, -6f, -6f), new Vector3(12f, 12f, 12f)) };
            AddChild(_flames);
        }
        if (owner.Embers > 0)
        {
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R0 * 0.5f,
                Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 0.5f, InitialVelocityMax = 2.5f, Gravity = new Vector3(0f, -3f, 0f), DampingMin = 1f, DampingMax = 2f,
                ScaleMin = 1f, ScaleMax = 1f, Color = new Color(owner.EmberColor.R, owner.EmberColor.G, owner.EmberColor.B, 1f),
                ColorRamp = SmsFireballs.Fade(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f), new Color(1f, 0.6f, 0.3f, 0.7f), new Color(1f, 0.3f, 0.1f, 0f))
            };
            var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsFireballs.Tex("glow1_spark"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
            _embers = new GpuParticles3D { Name = "Embers", TopLevel = true, Amount = owner.Embers, Lifetime = 0.7, Emitting = true, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * 0.16f, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 30f, 120f)) };
            AddChild(_embers);
        }
        if (owner.Lights) { _light = new OmniLight3D { Name = "Light", LightColor = new Color(1f, 0.55f, 0.18f), LightEnergy = owner.LightEnergy, OmniRange = owner.LightRange, ShadowEnabled = false }; AddChild(_light); }
    }

    /// <summary>A frame: the flight (the drop, the roll, the wall), the shrink, the burn on Mario.</summary>
    public void Tick(float dt, Mario mario, Vector3 centre, float wallR, out bool bounced, out bool burned)
    {
        bounced = false; burned = false;
        if (Dead) return;
        Age += dt;
        Vector3 p = GlobalPosition;
        if (Fizzling)
        {
            _fizzleT += dt; _k = Mathf.Max(0f, 1f - _fizzleT / 0.3f); R = R0 * _k;
            Apply(); if (_k <= 0f) Die(); return;
        }
        // the size: shrinking to nothing over the life, the fade over its last part
        float life01 = Mathf.Clamp(Age / Life, 0f, 1f);
        _k = Mathf.Max(0f, 1f - life01); R = R0 * _k;
        if (_k <= 0.06f) { Die(); return; }
        // the flight
        p += Vel * dt;
        if (_dropping)
        {
            _vy -= 20f * dt; p.Y += _vy * dt;
            if (p.Y <= FloorY + R) { p.Y = FloorY + R; _dropping = false; OnFloor = true; }
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
        float s = Mathf.Max(0.01f, _k) * (_flare > 0f ? 1.35f : 1f);
        Scale = Vector3.One * s;
        float life01 = Mathf.Clamp(Age / Life, 0f, 1f), fadeFrom = _owner != null ? _owner.FadeFrom : 0.65f;
        float a = life01 > fadeFrom ? 1f - (life01 - fadeFrom) / Mathf.Max(0.05f, 1f - fadeFrom) : 1f;
        if (Fizzling) a = _k;
        if (_coreMat != null && _owner != null) _coreMat.AlbedoColor = new Color(_owner.CoreColor.R, _owner.CoreColor.G, _owner.CoreColor.B, a);
        if (_light != null && _owner != null) _light.LightEnergy = _owner.LightEnergy * a * Mathf.Clamp(_k * 1.5f, 0.2f, 1f);
        if (_embers != null) { _embers.GlobalPosition = GlobalPosition; _embers.Emitting = !Fizzling && a > 0.1f; }
    }
    /// <summary>Out in a few frames: the ball collapsing, no more hits. 170: a ball that got Mario BURSTS -- a flash of flame and a ring.</summary>
    public void Fizzle() { if (!Fizzling && !Dead) { bool burst = HitMario; Fizzling = true; _fizzleT = 0f; HitMario = true; if (_flames != null) _flames.Emitting = false; if (burst) Burst(); } }
    private void Burst()
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = R * 0.6f,
            Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 3f, InitialVelocityMax = 7f, Gravity = new Vector3(0f, 2f, 0f), DampingMin = 3f, DampingMax = 6f,
            ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = SmsFireballs.Ramp(1f, 0.2f), AngleMin = -180f, AngleMax = 180f,
            Color = new Color(1f, 1f, 1f, 1f), ColorRamp = SmsFireballs.Fade(new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.5f, 0.1f, 1f), new Color(0.8f, 0.12f, 0.02f, 0.7f), new Color(0.3f, 0.03f, 0.01f, 0f))
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = SmsFireballs.Tex("fire1_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = "Burst", TopLevel = true, Amount = 20, Lifetime = 0.5, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * (R0 * 1.6f), Material = mat }, VisibilityAabb = new Aabb(new Vector3(-10f, -5f, -10f), new Vector3(20f, 20f, 20f)) };
        (GetParent() ?? this).AddChild(p); p.GlobalPosition = GlobalPosition; p.Finished += p.QueueFree; p.Emitting = true;
        var rtex = SmsFireballs.Tex("hamonring3_flat");
        var rmat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = rtex, AlbedoColor = new Color(1f, 0.6f, 0.2f, 0.9f) };
        var ring = new MeshInstance3D { Name = "BurstRing", TopLevel = true, Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = rmat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        (GetParent() ?? this).AddChild(ring); ring.GlobalPosition = new Vector3(GlobalPosition.X, FloorY + 0.1f, GlobalPosition.Z); ring.Rotation = new Vector3(-MathF.PI / 2f, 0f, 0f);
        float size = R0 * 6f; var tw = ring.CreateTween();
        tw.TweenMethod(Callable.From<float>(k => { ring.Scale = Vector3.One * Mathf.Max(0.05f, size * k); rmat.AlbedoColor = new Color(1f, 0.6f, 0.2f, 0.9f * (1f - k)); }), 0f, 1f, 0.35f);
        tw.TweenCallback(Callable.From(ring.QueueFree));
        SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, 0.5f);
    }
    private void Die()
    {
        Dead = true; Visible = false;
        if (_light != null) _light.LightEnergy = 0f;
        if (_embers != null) _embers.Emitting = false;
        GetTree().CreateTimer(0.8f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
}
