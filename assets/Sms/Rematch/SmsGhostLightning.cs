using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 166: GHOST LIGHTNING (KINGBOO_REMATCH_gdoc.txt, attack 3: "Crown glows red. Two walls of lightning close in on Mario from either side and try
/// to squash him. Three times in a row, fast"). King Boo makes one (his Lightning move) where Mario stands and it runs on its own: SqueezeCount squeezes,
/// each two WALLS of lightning (<see cref="GhostLightning.gdshader"/> on a quad WallLength wide and WallHeight tall, bolts jumping about; sparks along the
/// floor) that APPEAR WallGap apart about Mario's spot, across a squeeze axis (the first at random, the next turned SqueezeTurnDeg), dim and harmless for
/// WarnSeconds (the tell), then CLOSE at WallSpeed each until they meet (MSD_SE_EF_LIGHTNING as they start; a flash and a burst of sparks as they meet),
/// SqueezePause later the next, re-centred on where Mario is then. The dodge: out of the open ends, along the walls. THE HIT: through the closing, a wall
/// face reaching Mario's body (WallThickness deep, within the wall's length and height) or the gap closing on him is the SHOCK -- Mario.TakeShock(), the
/// port's ELECTRIC_DAMAGE (120 ticks locked in ma_shock with its sparks, 1 HP; MSD_SE_MA_DAMAGE_ELEC) -- once per squeeze, and once per attack unless
/// OneShockPerAttack is off; a jump does not clear a 5-m wall. <see cref="Cancel"/> (King Boo hit through the attack) takes the walls away. Signals Squeezed(hit) per squeeze, Finished.
/// </summary>
public partial class SmsGhostLightning : Node3D
{
    [ExportGroup("The walls")]
    /// <summary>Each wall's length (the open ends are this far apart to run through), its height, how far apart they start (both sides of Mario), how fast
    /// each closes (both close: the gap shuts at twice this), and how deep its face hits.</summary>
    [Export] public float WallLength = 10f, WallHeight = 5f, WallGap = 12f, WallSpeed = 8f, WallThickness = 0.5f;
    /// <summary>The squeezes: how many, the tell before each closes, the pause after each meets, and how far the axis turns from one to the next.</summary>
    [Export(PropertyHint.Range, "1,6,1")] public int SqueezeCount = 3;
    [Export] public float WarnSeconds = 0.45f, SqueezePause = 0.2f, SqueezeTurnDeg = 60f;
    /// <summary>A Mario shocked by one squeeze is left alone by the rest of this attack (off: every squeeze can shock him -- the shock's lock and grace are
    /// 1.5 s and the squeezes come 1.55 s apart, so a Mario who stands still would take all three).</summary>
    [Export] public bool OneShockPerAttack = true;
    [ExportGroup("The look")]
    /// <summary>The bolts' colours (the shock's cyan toward its blue), their brightness, how many bolt columns a wall carries, the floor sparks per wall.</summary>
    [Export] public Color BoltColorA = new Color(0.435f, 1f, 1f), BoltColorB = new Color(0.25f, 0.34f, 1f);
    [Export] public float Intensity = 1.2f, BoltsPerMetre = 1f;
    /// <summary>170: the arcs run along the wall -- this many stacked up it; the glowing POSTS at the walls' ends they jump between (metres wide; 0 = none);
    /// a cyan light on each wall (energy; 0 = none); the meet's shockwave ring on the floor (metres across at its widest; 0 = none) and camera shake (0 = none).</summary>
    [Export(PropertyHint.Range, "1,16,1")] public int Arcs = 6;
    [Export] public float PostWidth = 0.9f, LightEnergy = 3f, MeetRing = 14f, MeetShake = 0.6f;
    [Export(PropertyHint.Range, "0,120,1")] public int FloorSparks = 40;
    [Export(PropertyHint.Range, "0,120,1")] public int MeetSparks = 48;
    [ExportGroup("The sounds")]
    [Export] public string CloseSe = "MSD_SE_EF_LIGHTNING", HitSe = "MSD_SE_MA_DAMAGE_ELEC";
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void SqueezedEventHandler(bool hit);
    [Signal] public delegate void FinishedEventHandler();

    public enum Phase { Idle, Warn, Close, Meet, Pause, Done }
    public Phase State { get; private set; } = Phase.Idle;
    public int Squeeze { get; private set; }           // 1-based while running
    public int Hits { get; private set; }
    public Vector3 Centre => _centre;
    public Vector3 Axis => _axis;
    public float Gap => _gap;
    public float FloorY { get; private set; }
    public bool HitThisSqueeze => _hit;
    public Vector3 WallAPoint => _centre + _axis * (_gap * 0.5f);
    public Vector3 WallBPoint => _centre - _axis * (_gap * 0.5f);
    public float WallIntensityShown => _warnK;
    public int SqueezesDone { get; private set; }
    public static readonly List<SmsGhostLightning> All = new();
    public static int MeetBursts;

    private const float U = 74.44f;
    private Mario _mario; private SmsActorSound _sound; private readonly Random _rng = new(166);
    private MeshInstance3D _wallA, _wallB; private ShaderMaterial _matA, _matB; private GpuParticles3D _sparksA, _sparksB;
    private OmniLight3D _lightA, _lightB; private readonly List<MeshInstance3D> _posts = new();
    private Vector3 _centre, _axis = Vector3.Right, _side = Vector3.Forward; private float _gap, _t, _warnK, _baseAngle; private bool _hit, _closeSounded;

    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>Three squeezes about Mario, the first across a random axis. The floor's y is the caller's probe under him.</summary>
    public void Begin(Mario mario, float floorY)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY;
        _sound = SmsActorSound.Attach(this);
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/GhostLightning.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/GhostLightning.gdshader") : null;
        var bolt = ResourceLoader.Exists("res://assets/Sms/Fx/tex/biri_l1.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/biri_l1.png") : null;
        _matA = MakeMat(shader, bolt, 1f); _matB = MakeMat(shader, bolt, 2f);
        _wallA = MakeWall("WallA", _matA); _wallB = MakeWall("WallB", _matB);
        _sparksA = MakeSparks("SparksA"); _sparksB = MakeSparks("SparksB");
        _baseAngle = (float)(_rng.NextDouble() * Math.Tau);
        Squeeze = 0;
        NextSqueeze();
    }
    private ShaderMaterial MakeMat(Shader shader, Texture2D bolt, float seed)
    {
        if (shader == null) return null;
        var m = new ShaderMaterial { Shader = shader };
        m.SetShaderParameter("bolt_tex", bolt); m.SetShaderParameter("color_a", new Vector3(BoltColorA.R, BoltColorA.G, BoltColorA.B)); m.SetShaderParameter("color_b", new Vector3(BoltColorB.R, BoltColorB.G, BoltColorB.B));
        m.SetShaderParameter("rows", (float)Mathf.Max(1, Arcs)); m.SetShaderParameter("stretch", Mathf.Max(0.5f, WallLength * BoltsPerMetre * 0.2f)); m.SetShaderParameter("intensity", Intensity); m.SetShaderParameter("seed", seed);
        return m;
    }
    private MeshInstance3D MakeWall(string name, ShaderMaterial mat)
    {
        var mi = new MeshInstance3D { Name = name, Mesh = new QuadMesh { Size = new Vector2(WallLength, WallHeight) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(mi);
        if (PostWidth > 0f)                                                            // 170: the posts the arcs jump between, at the wall's ends
        {
            var tex = ResourceLoader.Exists("res://assets/Sms/Fx/tex/glow3_raw.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/glow3_raw.png") : null;
            foreach (float side in new[] { -1f, 1f })
            {
                var pm = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = tex, AlbedoColor = new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, 0.9f) };
                var post = new MeshInstance3D { Name = name + (side < 0f ? "PostA" : "PostB"), Mesh = new QuadMesh { Size = new Vector2(PostWidth, WallHeight * 1.08f) }, MaterialOverride = pm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(side * WallLength * 0.5f, 0f, 0f) };
                mi.AddChild(post); _posts.Add(post);
            }
        }
        if (LightEnergy > 0f) { var l = new OmniLight3D { Name = name + "Light", LightColor = new Color(0.5f, 0.9f, 1f), LightEnergy = 0f, OmniRange = WallLength * 0.9f, ShadowEnabled = false, Position = new Vector3(0f, -WallHeight * 0.3f, 0f) }; mi.AddChild(l); if (name == "WallA") _lightA = l; else _lightB = l; }
        return mi;
    }
    private GpuParticles3D MakeSparks(string name)
    {
        var tex = ResourceLoader.Exists("res://assets/Sms/Fx/tex/shikaku.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/shikaku.png") : null;
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(WallLength * 0.5f, 0.05f, 0.15f),
            Direction = new Vector3(0f, 1f, 0f), Spread = 60f, InitialVelocityMin = 1.5f, InitialVelocityMax = 4f, Gravity = new Vector3(0f, -6f, 0f), ScaleMin = 0.5f, ScaleMax = 1f,
            Color = new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, 1f)
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var p = new GpuParticles3D { Name = name, Amount = Mathf.Max(1, FloorSparks), Lifetime = 0.35, Emitting = false, Visible = false, LocalCoords = false, FixedFps = 60, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * 0.22f, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-20f, -5f, -20f), new Vector3(40f, 15f, 40f)) };
        AddChild(p);
        return p;
    }

    private void NextSqueeze()
    {
        Squeeze++;
        var m = _mario != null && IsInstanceValid(_mario) ? _mario.GlobalPosition : GlobalPosition;
        _centre = new Vector3(m.X, FloorY, m.Z);
        float ang = _baseAngle + Mathf.DegToRad(SqueezeTurnDeg) * (Squeeze - 1);
        _axis = new Vector3(MathF.Cos(ang), 0f, MathF.Sin(ang)); _side = new Vector3(-_axis.Z, 0f, _axis.X);
        _gap = WallGap; _t = 0f; _hit = false; _closeSounded = false;
        State = Phase.Warn;
        foreach (var w in new[] { _wallA, _wallB }) if (w != null) w.Visible = true;
        PlaceWalls(); SetWarn(1f); SetFlash(0f);
        if (DebugPrint) GD.Print($"[SMS lightning] {Name}: squeeze {Squeeze} of {SqueezeCount} about {_centre}, axis {_axis}, {WallGap} m apart -- the warning");
    }
    private void PlaceWalls()
    {
        float h = FloorY + WallHeight * 0.5f;
        if (_wallA != null) { _wallA.GlobalPosition = new Vector3(_centre.X + _axis.X * _gap * 0.5f, h, _centre.Z + _axis.Z * _gap * 0.5f); _wallA.GlobalBasis = Basis.LookingAt(_axis, Vector3.Up); }
        if (_wallB != null) { _wallB.GlobalPosition = new Vector3(_centre.X - _axis.X * _gap * 0.5f, h, _centre.Z - _axis.Z * _gap * 0.5f); _wallB.GlobalBasis = Basis.LookingAt(-_axis, Vector3.Up); }
        foreach (var (p, w) in new[] { (_sparksA, _wallA), (_sparksB, _wallB) })
            if (p != null && w != null) { p.GlobalPosition = new Vector3(w.GlobalPosition.X, FloorY + 0.1f, w.GlobalPosition.Z); p.GlobalBasis = w.GlobalBasis; }
    }
    private void SetWarn(float k) { _warnK = 1f - k; _matA?.SetShaderParameter("warn", k); _matB?.SetShaderParameter("warn", k); foreach (var l in new[] { _lightA, _lightB }) if (l != null) l.LightEnergy = LightEnergy * Mathf.Lerp(1f, 0.3f, k); foreach (var p in _posts) if (p.MaterialOverride is StandardMaterial3D pm) pm.AlbedoColor = new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, Mathf.Lerp(0.9f, 0.35f, k)); }
    private void SetFlash(float f) { _matA?.SetShaderParameter("flash", f); _matB?.SetShaderParameter("flash", f); }
    private void Sparks(bool on) { foreach (var p in new[] { _sparksA, _sparksB }) if (p != null) { p.Visible = on; p.Emitting = on; } }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (State == Phase.Idle || State == Phase.Done) return;
        _t += dt;
        switch (State)
        {
            case Phase.Warn:
                SetWarn(1f - 0.5f * Mathf.Clamp(_t / WarnSeconds, 0f, 1f));            // brightening toward the close
                if (_t >= WarnSeconds)
                {
                    State = Phase.Close; _t = 0f; SetWarn(0f); Sparks(true);
                    if (_sound != null && !string.IsNullOrEmpty(CloseSe)) _sound.Play(CloseSe);
                    if (DebugPrint) GD.Print($"[SMS lightning] {Name}: squeeze {Squeeze} CLOSES");
                }
                break;
            case Phase.Close:
                _gap = Mathf.Max(0f, _gap - 2f * WallSpeed * dt);
                PlaceWalls();
                HitCheck();
                if (_gap <= 0.01f)
                {
                    State = Phase.Meet; _t = 0f; SetFlash(1f); Sparks(false);
                    MeetBurst(); MeetRingAndShake();
                    SqueezesDone++;
                    if (DebugPrint) GD.Print($"[SMS lightning] {Name}: squeeze {Squeeze} MEETS -- {(_hit ? "Mario shocked" : "a miss")}");
                    EmitSignal(SignalName.Squeezed, _hit);
                }
                break;
            case Phase.Meet:
                SetFlash(Mathf.Max(0f, 1f - _t / 0.12f));
                if (_t >= 0.15f)
                {
                    foreach (var w in new[] { _wallA, _wallB }) if (w != null) w.Visible = false;
                    SetFlash(0f);
                    if (Squeeze >= SqueezeCount) { Finish(); }
                    else { State = Phase.Pause; _t = 0f; }
                }
                break;
            case Phase.Pause:
                if (_t >= SqueezePause) NextSqueeze();
                break;
        }
    }

    /// <summary>A wall face at Mario's body (within the wall's length and height), or the gap shut on him: the shock, once per squeeze.</summary>
    private void HitCheck()
    {
        if (_hit || _mario == null || !IsInstanceValid(_mario)) return;
        if (OneShockPerAttack && Hits > 0) return;
        Vector3 p = _mario.GlobalPosition;
        if (p.Y >= FloorY + WallHeight) return;
        Vector3 d = p - _centre; d.Y = 0f;
        float across = Mathf.Abs(d.Dot(_axis)), along = Mathf.Abs(d.Dot(_side)), r = SmsParams.Mario.DamageRadius / U;
        if (along > WallLength * 0.5f + r) return;
        float half = _gap * 0.5f;
        if (across >= half - WallThickness - r || half <= r)
        {
            _hit = true; Hits++;
            var before = _mario.stateOfMario;
            _mario.TakeShock();
            if (_sound != null && !string.IsNullOrEmpty(HitSe)) SmsActorSound.Attach(_mario)?.Play(HitSe);
            if (DebugPrint) GD.Print($"[SMS lightning] {Name}: the wall REACHES Mario at {p} (across {across:F2} of {half:F2}, along {along:F2}); {before} -> {_mario.stateOfMario}, hp {_mario.SmsHP}");
        }
    }
    private void MeetBurst()
    {
        var tex = ResourceLoader.Exists("res://assets/Sms/Fx/tex/shikaku.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/shikaku.png") : null;
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.2f, WallHeight * 0.5f, WallLength * 0.5f),
            Direction = new Vector3(1f, 0f, 0f), Spread = 180f, InitialVelocityMin = 4f, InitialVelocityMax = 12f, Gravity = new Vector3(0f, -9f, 0f), DampingMin = 2f, DampingMax = 5f, ScaleMin = 0.6f, ScaleMax = 1.4f,
            Color = new Color(0.8f, 0.95f, 1f, 1f)
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var p = new GpuParticles3D { Name = "MeetSparks", Amount = Mathf.Max(1, MeetSparks), Lifetime = 0.6, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * 0.3f, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-20f, -5f, -20f), new Vector3(40f, 15f, 40f)) };
        (GetParent() ?? this).AddChild(p);
        p.GlobalPosition = _centre + Vector3.Up * (WallHeight * 0.5f); p.GlobalBasis = Basis.LookingAt(_side, Vector3.Up);   // the box's long side along the walls
        p.Finished += p.QueueFree; p.Emitting = true;
        MeetBursts++;
    }
    /// <summary>170: the meet's shockwave -- a ring of the shock's cyan racing out along the floor from the seam -- and a kick of the camera.</summary>
    private void MeetRingAndShake()
    {
        if (MeetShake > 0f) SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, MeetShake);
        if (MeetRing <= 0f) return;
        var tex = ResourceLoader.Exists("res://assets/Sms/Fx/tex/hamonring3_flat.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/hamonring3_flat.png") : null;
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = tex, AlbedoColor = new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, 0.9f) };
        var ring = new MeshInstance3D { Name = "MeetRing", TopLevel = true, Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        (GetParent() ?? this).AddChild(ring);
        ring.GlobalPosition = _centre + Vector3.Up * 0.12f; ring.Rotation = new Vector3(-MathF.PI / 2f, 0f, 0f);
        float size = MeetRing; var tw = ring.CreateTween();
        tw.TweenMethod(Callable.From<float>(k => { ring.Scale = Vector3.One * Mathf.Max(0.05f, size * k); mat.AlbedoColor = new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, 0.9f * (1f - k)); }), 0f, 1f, 0.45f);
        tw.TweenCallback(Callable.From(ring.QueueFree));
    }
    private void Finish()
    {
        State = Phase.Done;
        if (DebugPrint) GD.Print($"[SMS lightning] {Name}: finished -- {Hits} shock{(Hits == 1 ? "" : "s")}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through the attack: the walls go, nothing more comes.</summary>
    public void Cancel()
    {
        if (State == Phase.Done) return;
        State = Phase.Done; Sparks(false);
        foreach (var w in new[] { _wallA, _wallB }) if (w != null) w.Visible = false;
        if (DebugPrint) GD.Print($"[SMS lightning] {Name}: cancelled at squeeze {Squeeze}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
}
