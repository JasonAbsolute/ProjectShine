using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 158: the BOMB BOO SUPPLY (KINGBOO_REMATCH_gdoc.txt: "Black foggy spots appear at random through the fight and a Boo bomb comes out
/// of each one... The spawn rate, the cap on how many are out at once and whether one expires on its own are all knobs"). From the fight's
/// start (the intro's FightReady; at once without an intro, or StartAtOnce) every SpawnInterval seconds, while fewer than MaxOut Boos are out
/// and King Boo stands: a black foggy spot (FogSeconds of dark smoke at the point) and a Bomb Boo rising out of it -- which then floats as Sunshine's Boos
/// do (160: wandering within its WanderRadiusUnits of the spot, so a Centre spawn drifts about the middle of the arena). Where: SpawnMode --
/// Centre (this node's point: for the playtests, as Jaysonn asked), Random (anywhere within ArenaRadius of this node, at least MinFromMario
/// from Mario), the floor found under the point. King Boo's hit clears every Boo on the field (the doc) and the supply pauses ClearPause
/// seconds; his defeat stops it. Knobs: the timing (FirstDelay, SpawnInterval, FogSeconds, ClearPause), the cap (MaxOut), the Boo's own
/// (ExpireSeconds and the rest on BombBooScene's root), the place (SpawnMode, ArenaRadius, MinFromMario, FloorOffset).
/// Pass 179 (Jaysonn: "when spawning in they should have that mist effect with the eyes glowing before they spawn in", "keep it black not purple"): the spot is
/// Galaxy's -- a BLACK pool on the floor with low black mist rolling off it and two GLOWING YELLOW EYES in it that face Mario, FogSeconds long; then the Boo comes
/// up THROUGH the floor in black smoke (SmsBombBoo.RiseFromUnder) as the eyes go out and the pool fades. The Fog group.
/// </summary>
public partial class SmsBombBooSpawner : Node3D
{
    public enum Modes { Centre, Random }

    [Export] public PackedScene BombBooScene;
    [Export] public Modes SpawnMode = Modes.Centre;
    /// <summary>Random: within this of this node (x and z), and never nearer Mario than MinFromMario.</summary>
    [Export] public float ArenaRadius = 18f, MinFromMario = 4f;
    [ExportGroup("Supply")]
    [Export] public float FirstDelay = 1.5f, SpawnInterval = 6f, FogSeconds = 1.1f, ClearPause = 2f;
    [Export(PropertyHint.Range, "1,12,1")] public int MaxOut = 2;
    /// <summary>The fog's and the Boo's point: this far over the floor found under the spawn point.</summary>
    [Export] public float FloorOffset = 0.3f;
    /// <summary>Start without waiting for the intro's FightReady.</summary>
    [Export] public bool StartAtOnce = false;
    [Export] public bool Enabled = true;
    [ExportGroup("Fog (179: the black spot, its mist and the glowing eyes)")]
    /// <summary>179 (Jaysonn: "when spawning in they should have that mist effect with the eyes glowing before they spawn in", "keep it black not purple"; the Galaxy
    /// refs): the spot is BLACK -- a dark pool on the floor (FogDisc: FogDiscRadius, FogDiscColor), low black mist rolling off it (FogMist; FogColor, FogRadius,
    /// FogTexture empty = the game's dark smoke ball) and, EyesDelay in, two GLOWING EYES in it (EyeColor, EyeSize, EyeGap apart, EyeHeight over the floor; GlowTexture empty
    /// = a soft disc) that face Mario and pulse -- until the Boo comes up through the floor (SmsBombBoo.RiseFromUnder): the eyes go out over EyesFadeSeconds, the
    /// pool over its rise. 158-178: a violet mist and nothing else.</summary>
    [Export] public bool FogMist = true;
    [Export] public Color FogColor = new Color(0.03f, 0.03f, 0.035f, 0.7f);
    [Export] public float FogRadius = 1.1f;
    [Export] public Texture2D FogTexture;
    [Export] public bool FogDisc = true;
    [Export(PropertyHint.Range, "0.3,5,0.05")] public float FogDiscRadius = 1.7f;
    [Export] public Color FogDiscColor = new Color(0f, 0f, 0f, 0.88f);
    [Export] public bool EyesGlow = true;
    [Export(PropertyHint.Range, "0,3,0.05")] public float EyesDelay = 0.3f, EyesFadeSeconds = 0.3f;
    [Export] public Color EyeColor = new Color(1f, 0.82f, 0.08f);
    [Export] public Vector2 EyeSize = new Vector2(0.27f, 0.54f);   // 180 (Jaysonn: "the eyes a little bigger"): 179's 0.2 x 0.4
    [Export(PropertyHint.Range, "0.05,1.5,0.01")] public float EyeGap = 0.42f, EyeHeight = 0.16f;
    [Export] public Texture2D GlowTexture;
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void SpawnedEventHandler(SmsBombBoo boo);

    public bool Running { get; private set; }
    public int SpawnCount { get; private set; }
    public int OutNow { get { int n = 0; foreach (var b in SmsBombBoo.All) if (b != null && IsInstanceValid(b) && b.State != SmsBombBoo.Phase.Gone) n++; return n; } }
    public float NextIn => _timer;
    /// <summary>179, for the drives: the spot (fogging, how long), the eyes (shown, their alpha, where, how far off Mario's line they face), the pool's alpha.</summary>
    public bool Fogging => _fogging;
    public float FogTime => _fogT;
    public float EyesAlpha { get; private set; }
    public float DiscAlpha { get; private set; }
    public bool EyesShown => _eyes != null && _eyes.Visible && EyesAlpha > 0.5f;
    public Vector3 EyesPoint => _eyes != null ? _eyes.GlobalPosition : Vector3.Zero;
    public float EyesOffMarioDeg { get; private set; } = 180f;

    private float _timer; private bool _fogging; private GpuParticles3D _fog; private Vector3 _fogAt; private float _fogT;
    private Node3D _eyes; private MeshInstance3D _disc; private StandardMaterial3D _eyeMat, _discMat; private bool _fading; private float _fadeT, _riseSeconds = 0.7f;
    private SmsKingBooRematch _boss; private SmsRematchIntro _intro; private Mario _mario; private readonly Random _rng = new(158);

    public override void _Ready()
    {
        BombBooScene ??= ResourceLoader.Exists("res://assets/Sms/Rematch/BombBoo.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Rematch/BombBoo.tscn") : null;
        FogTexture ??= ResourceLoader.Exists("res://assets/Sms/Fx/tex/kemuball3_dark.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/kemuball3_dark.png") : null;
        GlowTexture ??= ResourceLoader.Exists("res://assets/Sms/Fx/tex/radgrad_raw.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/radgrad_raw.png") : null;
        if (Engine.IsEditorHint()) return;
        SmsBombSupplyDriveHook();
        CallDeferred(nameof(Wire));
    }
    partial void SmsBombSupplyDriveHook();

    private void Wire()
    {
        _boss = GetTree().GetFirstNodeInGroup("sms_rematch_boss") as SmsKingBooRematch;
        if (_boss != null) { _boss.Hit += OnBossHit; _boss.Defeated += Stop; }
        foreach (Node n in GetTree().CurrentScene?.FindChildren("*", "", true, false) ?? new Godot.Collections.Array<Node>())
            if (n is SmsRematchIntro i) { _intro = i; break; }
        if (StartAtOnce || _intro == null || _intro.State == SmsRematchIntro.Phase.Done) Start();
        else _intro.FightReady += Start;
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: mode {SpawnMode}, every {SpawnInterval} s, {MaxOut} out at most; boss {(_boss != null ? "found" : "none")}, intro {(_intro != null ? "waits" : "none")}");
    }

    public void Start()
    {
        if (Running || !Enabled) return;
        Running = true; _timer = FirstDelay;
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: the supply starts (first in {FirstDelay} s)");
    }
    public void Stop() { Running = false; StopFog(); }

    private void OnBossHit(int hpLeft)
    {
        ClearAll();
        if (hpLeft <= 0)                                                                                               // 183: the LAST hit ends the supply (the casino's forceAllItemKill) -- no Boo through the down and the Shine
        {
            Stop();
            if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: King Boo's LAST hit -- the field cleared, the supply over");
            return;
        }
        _timer = Mathf.Max(_timer, ClearPause);
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: King Boo hit ({hpLeft} left) -- the field cleared, next in {_timer:F1} s");
    }

    /// <summary>Every Boo on the field gone (the doc: King Boo's hit clears them), the fog with them.</summary>
    public void ClearAll()
    {
        foreach (var b in new List<SmsBombBoo>(SmsBombBoo.All)) if (b != null && IsInstanceValid(b)) b.Vanish();
        StopFog();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Running) return;
        float dt = (float)delta;
        if (_fogging)
        {
            _fogT += dt;
            SpotTick();
            if (_fogT >= FogSeconds)
            {
                _fogging = false; var boo = SpawnBoo(_fogAt); if (_fog != null) _fog.Emitting = false;
                _fading = true; _fadeT = 0f; _riseSeconds = boo != null ? boo.RiseSeconds : 0.7f;                 // 179: the eyes go out, the pool fades over the rise
            }
            return;
        }
        if (_fading) FadeTick(dt);
        if (_boss != null && !_boss.IsAlive) { ClearAll(); Stop(); return; }                                          // 183: the field cleared with the stop
        if (OutNow >= MaxOut) return;
        _timer -= dt;
        if (_timer > 0f) return;
        _timer = SpawnInterval;
        Vector3 at = PickPoint();
        StartFog(at);
    }

    private Vector3 PickPoint()
    {
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        Vector3 p = GlobalPosition;
        if (SpawnMode == Modes.Random)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                float a = (float)(_rng.NextDouble() * Math.PI * 2.0), r = ArenaRadius * MathF.Sqrt((float)_rng.NextDouble());
                Vector3 c = GlobalPosition + new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
                if (_mario == null || new Vector2(c.X - _mario.GlobalPosition.X, c.Z - _mario.GlobalPosition.Z).Length() >= MinFromMario) { p = c; break; }
                p = c;
            }
        }
        float? floor = FloorUnder(p);
        if (floor.HasValue) p.Y = floor.Value + FloorOffset;
        return p;
    }

    private float? FloorUnder(Vector3 p)
    {
        var space = GetWorld3D()?.DirectSpaceState; if (space == null) return null;
        var q = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 20f, p + Vector3.Down * 40f);
        q.CollideWithAreas = false; q.CollideWithBodies = true;
        if (_mario != null && IsInstanceValid(_mario)) q.Exclude = new Godot.Collections.Array<Rid> { _mario.GetRid() };
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].AsVector3().Y : null;
    }

    /// <summary>The spot: the low black mist (179: broader, slower and lower than 158's violet puffs -- it rolls off the pool), the dark pool and the eyes at the
    /// floor (the spawn point is FloorOffset over it).</summary>
    private void StartFog(Vector3 at)
    {
        _fogging = true; _fogT = 0f; _fogAt = at; _fading = false;
        float floorY = at.Y - FloorOffset;
        if (_fog == null && FogMist)
        {
            // 179: a thin, low veil of black wisps over the pool (a column of 40 big puffs hid the pool and the eyes), casting no shadow (the puffs' shadow drew a
            // hard grey disc on the floor)
            _fog = new GpuParticles3D { Name = "Fog", TopLevel = true, Amount = 18, Lifetime = 1.0, OneShot = false, Emitting = false, FixedFps = 60,
                VisibilityAabb = new Aabb(new Vector3(-4f, -2f, -4f), new Vector3(8f, 6f, 8f)) };
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = FogRadius, Direction = Vector3.Up, Spread = 85f,
                InitialVelocityMin = 0.1f, InitialVelocityMax = 0.35f, Gravity = new Vector3(0f, 0.1f, 0f), ScaleMin = 0.8f, ScaleMax = 1.4f, LifetimeRandomness = 0.4f,
                AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -40f, AngularVelocityMax = 40f
            };
            Color lin = FogColor.SrgbToLinear(); lin.A = FogColor.A;                                    // the ramp reaches the particle shader raw (no sRGB decode): given in linear
            var ramp = new Gradient();
            ramp.SetColor(0, new Color(lin.R, lin.G, lin.B, 0f)); ramp.SetColor(1, new Color(lin.R, lin.G, lin.B, 0f));
            ramp.AddPoint(0.25f, lin); ramp.AddPoint(0.7f, lin);
            pm.ColorRamp = new GradientTexture1D { Gradient = ramp };
            _fog.ProcessMaterial = pm;
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                VertexColorUseAsAlbedo = true, AlbedoTexture = FogTexture, CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false
            };
            _fog.DrawPass1 = new QuadMesh { Size = new Vector2(1.4f, 1.4f), Material = mat };
            _fog.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            AddChild(_fog);
        }
        if (_fog != null) { _fog.GlobalPosition = new Vector3(at.X, floorY + 0.15f, at.Z); _fog.Restart(); _fog.Emitting = true; }
        if (FogDisc)
        {
            if (_disc == null)
            {
                _discMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = FogDiscColor, AlbedoTexture = GlowTexture, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
                _disc = new MeshInstance3D { Name = "FogPool", TopLevel = true, Mesh = new QuadMesh { Size = new Vector2(FogDiscRadius * 2f, FogDiscRadius * 2f), Orientation = PlaneMesh.OrientationEnum.Y }, MaterialOverride = _discMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(_disc);
            }
            _disc.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(at.X, floorY + 0.03f, at.Z)); _disc.Visible = true; DiscAlpha = 0f;
            _discMat.AlbedoColor = FogDiscColor with { A = 0f };
        }
        if (EyesGlow)
        {
            if (_eyes == null)
            {
                _eyeMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = EyeColor, AlbedoTexture = GlowTexture, CullMode = BaseMaterial3D.CullModeEnum.Disabled, RenderPriority = 10 };   // drawn after the mist: the glow shows through it
                _eyes = new Node3D { Name = "FogEyes", TopLevel = true };
                var quad = new QuadMesh { Size = EyeSize };
                _eyes.AddChild(new MeshInstance3D { Name = "L", Mesh = quad, MaterialOverride = _eyeMat, Position = new Vector3(-EyeGap * 0.5f, 0f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                _eyes.AddChild(new MeshInstance3D { Name = "R", Mesh = quad, MaterialOverride = _eyeMat, Position = new Vector3(EyeGap * 0.5f, 0f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                AddChild(_eyes);
            }
            _eyes.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(at.X, floorY + EyeHeight, at.Z)); _eyes.Visible = false; EyesAlpha = 0f;
            _eyeMat.AlbedoColor = EyeColor with { A = 0f };
        }
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: a black spot at {at} (the floor {floorY:F2})");
    }
    /// <summary>179: the spot while the mist rolls -- the pool fading in over its first quarter second, the eyes coming on EyesDelay in (a fifth of a second), facing
    /// Mario (their quads' +Z at him) and pulsing.</summary>
    private void SpotTick()
    {
        if (_disc != null && _disc.Visible) { DiscAlpha = Mathf.Clamp(_fogT / 0.25f, 0f, 1f); _discMat.AlbedoColor = FogDiscColor with { A = FogDiscColor.A * DiscAlpha }; }
        if (_eyes == null || !EyesGlow) return;
        EyesAlpha = Mathf.Clamp((_fogT - EyesDelay) / 0.2f, 0f, 1f);
        _eyes.Visible = EyesAlpha > 0f;
        _eyeMat.AlbedoColor = EyeColor with { A = EyesAlpha };
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        Vector3 pos = _eyes.GlobalPosition;
        Vector3 to = _mario != null ? _mario.GlobalPosition - pos : Vector3.Back; to.Y = 0f;
        float yaw = to.LengthSquared() > 1e-4f ? MathF.Atan2(to.X, to.Z) : 0f;
        float pulse = 1f + 0.08f * MathF.Sin(_fogT * MathF.Tau * 5f);
        _eyes.GlobalTransform = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * pulse), pos);
        EyesOffMarioDeg = to.LengthSquared() > 1e-4f ? Mathf.RadToDeg(MathF.Acos(Mathf.Clamp(_eyes.GlobalBasis.Z.Normalized().Dot(to.Normalized()), -1f, 1f))) : 180f;
    }
    /// <summary>179: the Boo is coming up -- the eyes go out over EyesFadeSeconds, the pool over the Boo's rise.</summary>
    private void FadeTick(float dt)
    {
        _fadeT += dt;
        if (_eyes != null && _eyes.Visible) { EyesAlpha = EyesFadeSeconds > 0f ? Mathf.Clamp(1f - _fadeT / EyesFadeSeconds, 0f, 1f) : 0f; _eyeMat.AlbedoColor = EyeColor with { A = EyesAlpha }; if (EyesAlpha <= 0f) _eyes.Visible = false; }
        if (_disc != null && _disc.Visible) { DiscAlpha = Mathf.Clamp(1f - _fadeT / Mathf.Max(0.05f, _riseSeconds), 0f, 1f); _discMat.AlbedoColor = FogDiscColor with { A = FogDiscColor.A * DiscAlpha }; if (DiscAlpha <= 0f) _disc.Visible = false; }
        if ((_eyes == null || !_eyes.Visible) && (_disc == null || !_disc.Visible)) _fading = false;
    }
    private void StopFog()
    {
        _fogging = false; _fading = false; if (_fog != null) _fog.Emitting = false;
        if (_eyes != null) { _eyes.Visible = false; EyesAlpha = 0f; }
        if (_disc != null) { _disc.Visible = false; DiscAlpha = 0f; }
    }

    private SmsBombBoo SpawnBoo(Vector3 at)
    {
        if (BombBooScene == null) { GD.PushWarning($"[SMS bomb supply] {GetPath()}: no BombBooScene (assets/Sms/Rematch/BombBoo.tscn)"); return null; }
        var boo = BombBooScene.Instantiate<SmsBombBoo>();
        boo.Name = $"BombBoo{++SpawnCount}";
        if (DebugPrint) boo.DebugPrint = true;
        GetParent().AddChild(boo);
        boo.GlobalPosition = at;
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: {boo.Name} out of the spot at {at} ({OutNow} out)");
        EmitSignal(SignalName.Spawned, boo);
        return boo;
    }
}
