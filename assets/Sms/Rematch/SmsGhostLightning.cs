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
/// OneShockPerAttack is off; a jump did not clear 166's 5-m wall (185: 2.2 m, a held jump clears it; Mario's feet over the wall's top as the face passes = no
/// shock). <see cref="Cancel"/> (King Boo hit through the attack) takes the walls away. Signals Squeezed(hit) per squeeze, Finished.
/// Pass 172 (Jaysonn: "particle effects for electric need to be better"): each wall carries the SHOCK'S OWN electricity (ms_m_biri_a / b / c, the port's
/// ShockSparks, scaled to the wall) over its whole face -- bolt STREAKS twisting about their own axis (ShockStreak.gdshader, biri_l1), ARCS popping (biri_r),
/// FLASHES (hitmrk1) with white spark CROSSES raining down the wall, sparks pouring off each glowing POST, bolts CRAWLING along the floor at its foot; dimmer
/// through the warning, full as it closes -- and once the gap is under ZapGap, ZAPS jump it: bolts flickering between the two walls at random spots. The meet
/// bursts with crosses, streaks and flashes along the seam, over 170's shockwave ring and shake.
/// Pass 173 (Jaysonn: "make the electricity move more yellow"): the colours go YELLOW (BoltColorA a warm white-yellow, BoltColorB an amber -- the shader,
/// the particles, the posts, the lights, the zaps and the ring all take them) and everything moves MORE: the streaks dart (2-6 m/s, were 0.4-1.6), the arcs
/// drift faster, the floor bolts race (5-10 m/s), the zaps flicker quicker (ZapChance 0.5 a frame, 1-3 frames on), the wall's arcs re-roll JitterHz 26 times a
/// second (18) and wave harder (Wave 0.28).
/// </summary>
public partial class SmsGhostLightning : Node3D
{
    [ExportGroup("The walls")]
    /// <summary>Each wall's length (the open ends are this far apart to run through), its height, how far apart they start (both sides of Mario), how fast
    /// each closes (both close: the gap shuts at twice this), and how deep its face hits. 185 (Jaysonn: "jump-over-able more easily, but longer"): 18 m long
    /// and 2.2 m tall (10 and 5 before: a held jump, apex 3.03 m, clears 2.2 with 0.37 s to spare; 5 m took a spin or triple jump) -- the dodge is the jump,
    /// not the run round the end. King Boo's own knobs of the same names set these on each attack.</summary>
    [Export] public float WallLength = 18f, WallHeight = 2.2f, WallGap = 12f, WallSpeed = 8f, WallThickness = 0.5f;
    /// <summary>192 (Jaysonn: "the hit boxes for the electric wall are still off from the visuals"): the shock reaches this far up the wall -- 0.85 of WallHeight
    /// (1.87 m): the shader fades the bolts out over the quad's top quarter, so feet already over the bolts were shocked at 1. And a wall is a SLAB now --
    /// WallThickness about its drawn face, plus Mario's 42 u -- so a Mario past a wall (he jumped it and came down outside) is clear; before, everything beyond
    /// a face counted and a clean jump was shocked on the way down. 193: the slab is the ONLY test -- 192 had kept a "gap shut" test that zapped a Mario outside
    /// the walls as they met. ShowHitBox draws the two slabs in red while the walls stand (King Boo's ShowHitBoxes).</summary>
    [Export(PropertyHint.Range, "0.3,1.2,0.05")] public float WallHitTopK = 0.85f;
    [Export] public bool ShowHitBox;
    /// <summary>The squeezes: how many, the tell before each closes, the pause after each meets, and how far the axis turns from one to the next.</summary>
    [Export(PropertyHint.Range, "1,6,1")] public int SqueezeCount = 3;
    [Export] public float WarnSeconds = 0.45f, SqueezePause = 0.2f, SqueezeTurnDeg = 60f;
    /// <summary>A Mario shocked by one squeeze is left alone by the rest of this attack (off: every squeeze can shock him -- the shock's lock and grace are
    /// 1.5 s and the squeezes come 1.55 s apart, so a Mario who stands still would take all three).</summary>
    [Export] public bool OneShockPerAttack = true;
    [ExportGroup("The look")]
    /// <summary>The bolts' colours (173: a warm white-yellow toward an amber -- the shock's cyan (0.435, 1, 1) toward its blue (0.25, 0.34, 1) before), their
    /// brightness, how many bolt columns a wall carries; the wall shader's re-roll rate (arcs a second) and how hard its arcs wave along the wall.</summary>
    [Export] public Color BoltColorA = new Color(1f, 0.95f, 0.5f), BoltColorB = new Color(1f, 0.62f, 0.1f);
    [Export] public float Intensity = 1.2f, BoltsPerMetre = 1f, JitterHz = 26f, Wave = 0.28f;
    /// <summary>170: the arcs run along the wall -- this many stacked up it; the glowing POSTS at the walls' ends they jump between (metres wide; 0 = none);
    /// a cyan light on each wall (energy; 0 = none); the meet's shockwave ring on the floor (metres across at its widest; 0 = none) and camera shake (0 = none).</summary>
    [Export(PropertyHint.Range, "1,16,1")] public int Arcs = 6;
    [Export] public float PostWidth = 0.9f, LightEnergy = 3f, MeetRing = 14f, MeetShake = 0.6f;
    [ExportGroup("The electricity (172)")]
    /// <summary>The shock's own particles over each wall's face: how many bolt STREAKS live at once (twisting, 0.2 s each), ARCS (biri_r, a tenth of a second),
    /// FLASHES (hitmrk1, a frame or two), white spark CROSSES raining down the wall; sparks pouring off each POST; bolts CRAWLING along the floor at the wall's
    /// foot; how much of all that runs through the warning (0..1); the ZAPS jumping the gap between the walls (how many at once; 0 = none) once it is under
    /// ZapGap metres, each this wide and this bright (over 1: the additive bolt punches through the wall's own light); the meet's burst: crosses, streaks
    /// and flashes along the seam.</summary>
    [Export(PropertyHint.Range, "0,300,1")] public int WallStreaks = 90;
    [Export(PropertyHint.Range, "0,60,1")] public int WallArcs = 14;
    [Export(PropertyHint.Range, "0,30,1")] public int WallFlashes = 6;
    [Export(PropertyHint.Range, "0,200,1")] public int WallSparks = 60;
    [Export(PropertyHint.Range, "0,100,1")] public int PostSparks = 24;
    [Export(PropertyHint.Range, "0,100,1")] public int FloorCrawl = 30;
    [Export(PropertyHint.Range, "0,1,0.05")] public float WarnFx = 0.35f;
    [Export(PropertyHint.Range, "0,8,1")] public int GapZaps = 4;
    [Export] public float ZapGap = 7f, ZapWidth = 1f, ZapBrightness = 1.8f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float ZapChance = 0.5f;
    [Export(PropertyHint.Range, "0,300,1")] public int MeetSparks = 120;
    [Export(PropertyHint.Range, "0,200,1")] public int MeetStreaks = 60;
    [Export(PropertyHint.Range, "0,40,1")] public int MeetFlashes = 12;
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
    public Vector3 Side => _side;
    public float Gap => _gap;
    public float FloorY { get; private set; }
    public bool HitThisSqueeze => _hit;
    public Vector3 WallAPoint => _centre + _axis * (_gap * 0.5f);
    public Vector3 WallBPoint => _centre - _axis * (_gap * 0.5f);
    public float WallIntensityShown => _warnK;
    public int SqueezesDone { get; private set; }
    /// <summary>172, for the drives: the electricity emitters per wall and how many are emitting; the zaps and how many show now.</summary>
    public int WallFxCount => _wallFx.Count;
    public int WallFxEmitting { get { int n = 0; foreach (var p in _wallFx) if (p.Emitting) n++; return n; } }
    public int ZapsShown { get { int n = 0; foreach (var z in _zaps) if (z.node.Visible) n++; return n; } }
    public int ZapsFlashed { get; private set; }
    public static readonly List<SmsGhostLightning> All = new();
    public static int MeetBursts;

    private const float U = 74.44f;
    private Mario _mario; private SmsActorSound _sound; private readonly Random _rng = new(166);
    private MeshInstance3D _wallA, _wallB; private ShaderMaterial _matA, _matB;
    private OmniLight3D _lightA, _lightB; private readonly List<MeshInstance3D> _posts = new();
    private readonly List<GpuParticles3D> _wallFx = new(); private readonly List<(Node3D node, int left)> _zaps = new(); private Material _streakMat;
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
        var bolt = FxTex("biri_l1");
        _matA = MakeMat(shader, bolt, 1f); _matB = MakeMat(shader, bolt, 2f);
        _wallA = MakeWall("WallA", _matA); _wallB = MakeWall("WallB", _matB);
        MakeWallFx(_wallA); MakeWallFx(_wallB); MakeZaps();
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
        m.SetShaderParameter("jitter_hz", JitterHz); m.SetShaderParameter("wave", Wave);   // 173
        return m;
    }
    private MeshInstance3D MakeWall(string name, ShaderMaterial mat)
    {
        var mi = new MeshInstance3D { Name = name, Mesh = new QuadMesh { Size = new Vector2(WallLength, WallHeight) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(mi);
        if (ShowHitBox)                                                                // 192: the slab that shocks, drawn (its length, WallHitTopK of the height, WallThickness deep; Mario's own cylinder is King Boo's to draw)
        {
            float hh = WallHeight * WallHitTopK;
            var bm = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoColor = new Color(1f, 0.15f, 0.15f, 0.3f), RenderPriority = 2 };
            var box = new MeshInstance3D { Name = name + "HitBox", Mesh = new BoxMesh { Size = new Vector3(WallLength, hh, WallThickness) }, MaterialOverride = bm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0f, (hh - WallHeight) * 0.5f, 0f) };
            mi.AddChild(box);
        }
        if (PostWidth > 0f)                                                            // 170: the posts the arcs jump between, at the wall's ends
        {
            var tex = FxTex("glow3_raw");
            foreach (float side in new[] { -1f, 1f })
            {
                var pm = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = tex, AlbedoColor = new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, 0.9f) };
                var post = new MeshInstance3D { Name = name + (side < 0f ? "PostA" : "PostB"), Mesh = new QuadMesh { Size = new Vector2(PostWidth, WallHeight * 1.08f) }, MaterialOverride = pm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(side * WallLength * 0.5f, 0f, 0f) };
                mi.AddChild(post); _posts.Add(post);
            }
        }
        if (LightEnergy > 0f) { var l = new OmniLight3D { Name = name + "Light", LightColor = BoltColorA.Lerp(new Color(1f, 1f, 1f), 0.25f), LightEnergy = 0f, OmniRange = WallLength * 0.9f, ShadowEnabled = false, Position = new Vector3(0f, -WallHeight * 0.3f, 0f) }; mi.AddChild(l); if (name == "WallA") _lightA = l; else _lightB = l; }
        return mi;
    }

    // ---- 172: the electricity -------------------------------------------------------------------------------------------------------------------
    private static Texture2D FxTex(string name) { string p = $"res://assets/Sms/Fx/tex/{name}.png"; return ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null; }
    private static GradientTexture1D Grad(float[] at, Color[] c) => new GradientTexture1D { Gradient = new Gradient { Offsets = at, Colors = c } };
    private static StandardMaterial3D AddMat(Texture2D tex, bool billboard) => new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Particles : BaseMaterial3D.BillboardModeEnum.Disabled, BillboardKeepScale = true, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
    /// <summary>ms_m_biri_a's streak: the port's ShockStreak.gdshader (the quad's Y along the particle's way, twisting about it by INSTANCE_CUSTOM.x) with biri_l1.</summary>
    private Material StreakMat()
    {
        if (_streakMat != null) return _streakMat;
        var sh = ResourceLoader.Exists("res://assets/Sms/Fx/ShockStreak.gdshader") ? GD.Load<Shader>("res://assets/Sms/Fx/ShockStreak.gdshader") : null;
        if (sh == null) return _streakMat = AddMat(FxTex("biri_l1"), false);
        var m = new ShaderMaterial { Shader = sh }; m.SetShaderParameter("streak", FxTex("biri_l1"));
        return _streakMat = m;
    }
    private GradientTexture1D StreakRamp() => Grad(new[] { 0f, 0.2143f, 1f }, new[] { new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, 1f), new Color(BoltColorB.R, BoltColorB.G, BoltColorB.B, 1f), new Color(BoltColorB.R, BoltColorB.G, BoltColorB.B, 0f) });
    private GradientTexture1D CrossRamp() => Grad(new[] { 0f, 1f }, new[] { new Color(1f, 1f, 1f, 1f), new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, 0f) });
    private Color Pale(float k) => BoltColorA.Lerp(new Color(1f, 1f, 1f), k);
    private GpuParticles3D Emitter(Node parent, string name, int amount, double life, ParticleProcessMaterial pm, Mesh mesh, Mesh mesh2, Vector3 pos, bool oneShot = false)
    {
        var p = new GpuParticles3D { Name = name, Amount = Mathf.Max(1, amount), Lifetime = life, Emitting = false, Visible = !oneShot ? false : true, LocalCoords = !oneShot, FixedFps = 60, Interpolate = true, OneShot = oneShot, Explosiveness = oneShot ? 1f : 0f, ProcessMaterial = pm, DrawPass1 = mesh, VisibilityAabb = new Aabb(new Vector3(-20f, -8f, -20f), new Vector3(40f, 20f, 40f)), Position = pos };
        if (mesh2 != null) { p.DrawPasses = 2; p.DrawPass2 = mesh2; }
        parent.AddChild(p);
        return p;
    }
    /// <summary>The wall's electricity (children of the wall, local coordinates: they ride it as it closes). The wall quad: X along it, Y up, Z its normal.</summary>
    private void MakeWallFx(MeshInstance3D wall)
    {
        if (wall == null) return;
        float hl = WallLength * 0.5f, hh = WallHeight * 0.5f;
        if (WallStreaks > 0)
        {
            var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(hl, hh, 0.12f), ParticleFlagAlignY = true, Direction = new Vector3(1f, 0f, 0f), Spread = 180f, InitialVelocityMin = 2f, InitialVelocityMax = 6f, Gravity = Vector3.Zero, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = 540f, AngularVelocityMax = 1620f, ScaleMin = 0.6f, ScaleMax = 1.6f, LifetimeRandomness = 0.3f, ColorRamp = StreakRamp() };
            _wallFx.Add(Emitter(wall, "Streaks", WallStreaks, 0.22, pm, new QuadMesh { Size = new Vector2(0.55f, 2.2f), Material = StreakMat() }, null, Vector3.Zero));
        }
        if (WallArcs > 0)
        {
            var am = AddMat(FxTex("biri_r"), true); am.ParticlesAnimHFrames = 2; am.ParticlesAnimVFrames = 1; am.ParticlesAnimLoop = false;
            var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(hl, hh, 0.2f), Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 1f, InitialVelocityMax = 4f, Gravity = Vector3.Zero, AngleMin = 0f, AngleMax = 360f, AnimOffsetMin = 0f, AnimOffsetMax = 1f, ScaleMin = 0.6f, ScaleMax = 1.5f, ColorRamp = Grad(new[] { 0f, 0.875f, 1f }, new[] { Pale(0.3f), Pale(0.3f), new Color(Pale(0.3f), 0f) }) };
            _wallFx.Add(Emitter(wall, "Arcs", WallArcs, 0.13, pm, new QuadMesh { Size = Vector2.One * 1.6f, Material = am }, null, Vector3.Zero));
        }
        if (WallFlashes > 0)
        {
            var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(hl, hh, 0.1f), Direction = new Vector3(0f, 1f, 0f), Spread = 0f, InitialVelocityMin = 0f, InitialVelocityMax = 0f, Gravity = Vector3.Zero, Color = Pale(0.4f) };
            _wallFx.Add(Emitter(wall, "Flashes", WallFlashes, 0.05, pm, new QuadMesh { Size = Vector2.One * 0.9f, Material = AddMat(FxTex("hitmrk1"), true) }, null, Vector3.Zero));
        }
        if (WallSparks > 0)
        {
            var cm = AddMat(FxTex("shikaku"), false);
            var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(hl, hh, 0.1f), ParticleFlagAlignY = true, Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 3f, InitialVelocityMax = 6f, Gravity = new Vector3(0f, -21f, 0f), ColorRamp = CrossRamp() };
            _wallFx.Add(Emitter(wall, "Crosses", WallSparks, 0.4, pm, new QuadMesh { Size = new Vector2(0.07f, 0.3f), Material = cm, Orientation = PlaneMesh.OrientationEnum.X }, new QuadMesh { Size = new Vector2(0.07f, 0.3f), Material = cm, Orientation = PlaneMesh.OrientationEnum.Z }, Vector3.Zero));
        }
        if (PostSparks > 0 && PostWidth > 0f)
        {
            var cm = AddMat(FxTex("shikaku"), false);
            foreach (float side in new[] { -1f, 1f })
            {
                var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.15f, hh, 0.15f), ParticleFlagAlignY = true, Direction = new Vector3(side, 0.6f, 0f), Spread = 70f, InitialVelocityMin = 2f, InitialVelocityMax = 5f, Gravity = new Vector3(0f, -18f, 0f), ColorRamp = Grad(new[] { 0f, 1f }, new[] { Pale(0.5f), new Color(BoltColorB, 0f) }) };
                _wallFx.Add(Emitter(wall, side < 0f ? "PostSparksA" : "PostSparksB", PostSparks, 0.45, pm, new QuadMesh { Size = new Vector2(0.06f, 0.26f), Material = cm, Orientation = PlaneMesh.OrientationEnum.X }, new QuadMesh { Size = new Vector2(0.06f, 0.26f), Material = cm, Orientation = PlaneMesh.OrientationEnum.Z }, new Vector3(side * hl, 0f, 0f)));
            }
        }
        if (FloorCrawl > 0)
        {
            var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(hl, 0.05f, 0.3f), ParticleFlagAlignY = true, Direction = new Vector3(1f, 0f, 0f), Spread = 25f, InitialVelocityMin = 5f, InitialVelocityMax = 10f, Gravity = Vector3.Zero, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = 540f, AngularVelocityMax = 1620f, ScaleMin = 0.7f, ScaleMax = 1.3f, LifetimeRandomness = 0.3f, ColorRamp = StreakRamp() };
            var pm2 = (ParticleProcessMaterial)pm.Duplicate(); pm2.Direction = new Vector3(-1f, 0f, 0f);
            _wallFx.Add(Emitter(wall, "FloorCrawlA", Mathf.Max(1, FloorCrawl / 2), 0.25, pm, new QuadMesh { Size = new Vector2(0.4f, 1.6f), Material = StreakMat() }, null, new Vector3(0f, -hh + 0.1f, 0.15f)));
            _wallFx.Add(Emitter(wall, "FloorCrawlB", Mathf.Max(1, FloorCrawl / 2), 0.25, pm2, new QuadMesh { Size = new Vector2(0.4f, 1.6f), Material = StreakMat() }, null, new Vector3(0f, -hh + 0.1f, 0.15f)));
        }
    }
    /// <summary>The zaps: GapZaps crossed bolt quads, each spanning the gap between the walls at a spot of its own, on for a few frames and off for a few.</summary>
    private void MakeZaps()
    {
        for (int i = 0; i < GapZaps; i++)
        {
            var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = FxTex("biri_l1"), AlbedoColor = new Color(BoltColorA.R * ZapBrightness, BoltColorA.G * ZapBrightness, BoltColorA.B * ZapBrightness, 1f), TextureRepeat = true };   // over 1: the additive bolt punches through the wall's own
            var z = new Node3D { Name = $"Zap{i + 1}", TopLevel = true, Visible = false };
            AddChild(z);
            foreach (float roll in new[] { 0f, MathF.PI / 2f })
            {
                var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(ZapWidth, 1f), Material = mat }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Rotation = new Vector3(0f, roll, 0f) };
                z.AddChild(q);
            }
            _zaps.Add((z, 0));
        }
    }
    /// <summary>A frame of the zaps while the gap is under ZapGap: a shown zap counts down and hides; a hidden one may jump to a new spot (re-sized to the gap).</summary>
    private void ZapTick()
    {
        bool on = State == Phase.Close && _gap < ZapGap && _gap > 0.4f;
        for (int i = 0; i < _zaps.Count; i++)
        {
            var (z, left) = _zaps[i];
            if (!on) { if (z.Visible) z.Visible = false; _zaps[i] = (z, 0); continue; }
            if (left > 0) { left--; if (left == 0) z.Visible = false; _zaps[i] = (z, left); continue; }
            if (_rng.NextDouble() > ZapChance) continue;                                                       // off for a few frames more
            float along = ((float)_rng.NextDouble() - 0.5f) * WallLength * 0.9f, h = FloorY + WallHeight * (0.15f + 0.75f * (float)_rng.NextDouble());
            z.GlobalPosition = new Vector3(_centre.X + _side.X * along, h, _centre.Z + _side.Z * along);
            z.GlobalBasis = new Basis(_side, _axis, _side.Cross(_axis)).Orthonormalized();                     // the quads' Y (the bolt) along the gap's axis
            foreach (Node c in z.GetChildren()) if (c is MeshInstance3D q && q.Mesh is QuadMesh qm) { qm.Size = new Vector2(ZapWidth, _gap); if (qm.Material is StandardMaterial3D sm) sm.Uv1Scale = new Vector3(1f, Mathf.Max(1f, _gap / 2.7f), 1f); }
            z.Visible = true; _zaps[i] = (z, 1 + _rng.Next(0, 3)); ZapsFlashed++;
        }
    }
    private void WallFx(bool on, float ratio)
    {
        foreach (var p in _wallFx) { p.Visible = on; p.Emitting = on; p.AmountRatio = Mathf.Clamp(ratio, 0.05f, 1f); }
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
        PlaceWalls(); SetWarn(1f); SetFlash(0f); WallFx(true, WarnFx);
        if (DebugPrint) GD.Print($"[SMS lightning] {Name}: squeeze {Squeeze} of {SqueezeCount} about {_centre}, axis {_axis}, {WallGap} m apart -- the warning");
    }
    private void PlaceWalls()
    {
        float h = FloorY + WallHeight * 0.5f;
        if (_wallA != null) { _wallA.GlobalPosition = new Vector3(_centre.X + _axis.X * _gap * 0.5f, h, _centre.Z + _axis.Z * _gap * 0.5f); _wallA.GlobalBasis = Basis.LookingAt(_axis, Vector3.Up); }
        if (_wallB != null) { _wallB.GlobalPosition = new Vector3(_centre.X - _axis.X * _gap * 0.5f, h, _centre.Z - _axis.Z * _gap * 0.5f); _wallB.GlobalBasis = Basis.LookingAt(-_axis, Vector3.Up); }
    }
    private void SetWarn(float k) { _warnK = 1f - k; _matA?.SetShaderParameter("warn", k); _matB?.SetShaderParameter("warn", k); foreach (var l in new[] { _lightA, _lightB }) if (l != null) l.LightEnergy = LightEnergy * Mathf.Lerp(1f, 0.3f, k); foreach (var p in _posts) if (p.MaterialOverride is StandardMaterial3D pm) pm.AlbedoColor = new Color(BoltColorA.R, BoltColorA.G, BoltColorA.B, Mathf.Lerp(0.9f, 0.35f, k)); }
    private void SetFlash(float f) { _matA?.SetShaderParameter("flash", f); _matB?.SetShaderParameter("flash", f); }

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
                    State = Phase.Close; _t = 0f; SetWarn(0f); WallFx(true, 1f);
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
                    State = Phase.Meet; _t = 0f; SetFlash(1f); WallFx(false, 1f);
                    MeetBurst(); MeetRingAndShake();
                    SqueezesDone++;
                    if (DebugPrint) GD.Print($"[SMS lightning] {Name}: squeeze {Squeeze} MEETS -- {(_hit ? "Mario shocked" : "a miss")}; {ZapsFlashed} zaps so far");
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
        ZapTick();
    }

    /// <summary>A wall face at Mario's body (within the wall's length and height), or the gap shut on him: the shock, once per squeeze.</summary>
    private void HitCheck()
    {
        if (_hit || _mario == null || !IsInstanceValid(_mario)) return;
        if (OneShockPerAttack && Hits > 0) return;
        Vector3 p = _mario.GlobalPosition;
        if (p.Y >= FloorY + WallHeight * WallHitTopK) return;                                                  // 192: his feet over the bolts (the quad's top quarter is the fade)
        Vector3 d = p - _centre; d.Y = 0f;
        float across = Mathf.Abs(d.Dot(_axis)), along = Mathf.Abs(d.Dot(_side)), r = SmsParams.Mario.DamageRadius / U;
        if (along > WallLength * 0.5f + r) return;
        float half = _gap * 0.5f;
        bool slab = Mathf.Abs(across - half) <= WallThickness * 0.5f + r;                                       // 192: the wall's own slab about its drawn face -- past it (jumped) is clear
        // 193 (Jaysonn: "got hit by lightning from jumping over it where it spawned in"): the old "gap shut" test (half <= r) is gone -- it shocked a Mario OUTSIDE
        // the walls the moment they met, wherever he stood along them (he had jumped a wall where it stood and was clear of it). A Mario inside is always reached by a
        // face's slab first (the band is wider than his radius), so nothing is lost.
        if (slab)
        {
            _hit = true; Hits++;
            var before = _mario.stateOfMario;
            _mario.TakeShock();
            if (_sound != null && !string.IsNullOrEmpty(HitSe)) SmsActorSound.Attach(_mario)?.Play(HitSe);
            if (DebugPrint) GD.Print($"[SMS lightning] {Name}: the wall REACHES Mario at {p} (across {across:F2} of {half:F2}, along {along:F2}); {before} -> {_mario.stateOfMario}, hp {_mario.SmsHP}");
        }
    }
    /// <summary>The meet's burst along the seam: white spark crosses flung out under gravity, bolt streaks bursting from it, flashes; all freed as they finish.</summary>
    private void MeetBurst()
    {
        Node parent = GetParent() ?? this;
        Vector3 at = _centre + Vector3.Up * (WallHeight * 0.5f); Basis along = Basis.LookingAt(_side, Vector3.Up);   // the boxes' long side along the walls
        if (MeetSparks > 0)
        {
            var cm = AddMat(FxTex("shikaku"), false);
            var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.2f, WallHeight * 0.5f, WallLength * 0.5f), ParticleFlagAlignY = true, Direction = new Vector3(1f, 0f, 0f), Spread = 180f, InitialVelocityMin = 5f, InitialVelocityMax = 13f, Gravity = new Vector3(0f, -14f, 0f), DampingMin = 1f, DampingMax = 3f, ColorRamp = CrossRamp() };
            var p = Emitter(parent, "MeetSparks", MeetSparks, 0.6, pm, new QuadMesh { Size = new Vector2(0.08f, 0.34f), Material = cm, Orientation = PlaneMesh.OrientationEnum.X }, new QuadMesh { Size = new Vector2(0.08f, 0.34f), Material = cm, Orientation = PlaneMesh.OrientationEnum.Z }, Vector3.Zero, true);
            p.GlobalPosition = at; p.GlobalBasis = along; p.Finished += p.QueueFree; p.Emitting = true;
        }
        if (MeetStreaks > 0)
        {
            var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.2f, WallHeight * 0.5f, WallLength * 0.5f), ParticleFlagAlignY = true, Direction = new Vector3(1f, 0f, 0f), Spread = 180f, InitialVelocityMin = 6f, InitialVelocityMax = 16f, Gravity = Vector3.Zero, DampingMin = 6f, DampingMax = 12f, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = 540f, AngularVelocityMax = 1620f, ScaleMin = 0.8f, ScaleMax = 1.8f, ColorRamp = StreakRamp() };
            var p = Emitter(parent, "MeetStreaks", MeetStreaks, 0.3, pm, new QuadMesh { Size = new Vector2(0.6f, 2.4f), Material = StreakMat() }, null, Vector3.Zero, true);
            p.GlobalPosition = at; p.GlobalBasis = along; p.Finished += p.QueueFree; p.Emitting = true;
        }
        if (MeetFlashes > 0)
        {
            var pm = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.3f, WallHeight * 0.5f, WallLength * 0.5f), Direction = new Vector3(0f, 1f, 0f), Spread = 0f, InitialVelocityMin = 0f, InitialVelocityMax = 0f, Gravity = Vector3.Zero, ScaleMin = 0.8f, ScaleMax = 2f, Color = Pale(0.4f) };
            var p = Emitter(parent, "MeetFlashes", MeetFlashes, 0.08, pm, new QuadMesh { Size = Vector2.One * 1.4f, Material = AddMat(FxTex("hitmrk1"), true) }, null, Vector3.Zero, true);
            p.GlobalPosition = at; p.GlobalBasis = along; p.Finished += p.QueueFree; p.Emitting = true;
        }
        MeetBursts++;
    }
    /// <summary>170: the meet's shockwave -- a ring of the shock's cyan racing out along the floor from the seam -- and a kick of the camera.</summary>
    private void MeetRingAndShake()
    {
        if (MeetShake > 0f) SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, MeetShake);
        if (MeetRing <= 0f) return;
        var tex = FxTex("hamonring3_flat");
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
        if (DebugPrint) GD.Print($"[SMS lightning] {Name}: finished -- {Hits} shock{(Hits == 1 ? "" : "s")}, {ZapsFlashed} zaps");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through the attack: the walls go, nothing more comes.</summary>
    public void Cancel()
    {
        if (State == Phase.Done) return;
        State = Phase.Done; WallFx(false, 1f);
        foreach (var w in new[] { _wallA, _wallB }) if (w != null) w.Visible = false;
        foreach (var (z, _) in _zaps) z.Visible = false;
        if (DebugPrint) GD.Print($"[SMS lightning] {Name}: cancelled at squeeze {Squeeze}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
}
