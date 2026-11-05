using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// SMS pass 157: KING BOO as the REMATCH fights him (KINGBOO_REMATCH_gdoc.txt) -- the arena's own actor, apart from the casino's
/// SmsBossTelesa (the roulette fight stays as it is). This pass is his ENTRANCE: hidden until <see cref="Spawn"/>, then the appear clip, the
/// laugh clip with the long laugh (MSD_SE_BS_TELESA_V_LAUGH1), and the wait loop -- hovering (HoverBob), turning to face Mario (FaceMario),
/// the soft glow and the twinkle at his body (the casino boss's GlowFx / ChikaFx, "a slight glow on him throughout"), the blink loop on his
/// eyes (btelesa_twait's 150-frame table), the ghost's screen mix at GhostOpacity. Pass 158: HIS HIT POINTS -- a thrown Bomb Boo bursting on him
/// (SmsBombBoo; 159: SWUNG into his body's hit cylinder, HitRadius / HitBelow / HitAbove) is <see cref="TakeHit"/>: a point off, the hit clip
/// with a full turn about himself (the doc's hurt flip, until its own animation comes), a flash, more solid (OpacityPerHit); at none left the
/// down clip and he is gone (Defeated).
/// Pass 162: THE TELEPORTS AND THE LICK (the doc: "quick and light... teleports between spots... does not walk"; "he teleports directly in front of
/// Mario and licks, with half a second of wind-up before it lands. The wind-up is the whole tell and a jump clears it"). While he waits a gap runs
/// down (GapAtStart at the first hit point to GapAtLastHit at the last, +- GapJitter: the doc's pace, shorter per hit); at its end a MOVE: he
/// VANISHES (the hide clip -- its hop up -- under a fade of TeleportOutSeconds, MSD_SE_BS_TELESA_DISAPPEAR), and ARRIVES somewhere else (a fade in
/// of TeleportInSeconds, settling down TeleportInDrop, the appear voice): usually LickDistance in front of Mario, facing him, at LickHeight over the
/// floor, for the LICK; by RepositionChance just another spot of the arena (SpotRadius about its middle, SpotMinFromMario to SpotMaxFromMario from
/// Mario) and a half gap. The lick is a casino clip (LickClip: btelesa_roll, the roulette spin, at LickClipSpeed): he rears up with the tongue
/// raised overhead (the tell, MSD_SE_BS_TELESA_V_ROLL), then plunges as the tongue WHIPS across the front of him, left to right, low -- the wet whip
/// (163: Jaysonn; 162 had re-aimed the tongue onto Mario, which looked wrong) -- with the tongue a bit longer than the clips have it
/// (<see cref="SmsTongueStretch"/>, TongueStretch). The HIT: over the whip's front crossing (clip frames LickHitFromFrame..LickHitToFrame,
/// MSD_SE_BS_TELESA_LICK at its start) any tongue joint, or the point between two, inside Mario's damage cylinder (42 u across, 130 u tall from his
/// feet) grown by LickThickness is the plain hit -- Mario.TakeDamage with the common enemy profile, the knockdown -- once per lick; the whip comes
/// through at Mario's legs from LickHeight (the tip skimming the floor), so a jump at the tell clears it. The clip is cut at LickEndFrame for the wait; AfterLickSeconds later he
/// vanishes to another spot and the gap runs again. He can be hit by a swung Bomb Boo while he waits AND through the lick (the hit breaks the lick
/// off: the hurt as before); not while he is between spots.
/// Pass 164: THE BOOLOSSUS SMASH (the doc's attack 2: "Crown glows white. A Boolossus falls from above Mario and tries to slam down on him. On landing it
/// splits into 15 Boos that spread out as a shockwave, then vanish after one second"). A move is now one of three (the doc's near-random pick):
/// a hop to another spot (RepositionChance), else an attack -- a SMASH by SmashChance, else the lick. The smash is made where he stands: the laugh clip
/// (SmashClip, the second laugh voice) with his CROWN GLOWING the attack's colour (the doc: white for the smash; 165, Jaysonn: "king boo's crown should
/// glow the color listed in the doc to show that's the attack he's using") -- a light at the crown joint (CrownGlowSize, an additive quad of the port's
/// radial gradient), the crown shader's own glow term (crown_glow / crown_glow_k) and its colour register, pulsing at CrownPulseHz through
/// SmashTellSeconds, the tell, then held -- then the Boolossi: 165, SmashCount of them (three), SmashStagger apart, each the Boolossus scene (SmashScene:
/// <see cref="SmsBoolossus"/>, its own knobs) DIVING in at Mario from its own direction (SmashSpreadDeg apart about a random first; from where King
/// Boo stands when SmashFromKingBoo) and running on its own (the dive, the slam, the wave of Boos); he watches SmashWatchSeconds past the last, the
/// crown dark again, and the wait and the gap run again. A swung Bomb Boo hits him through the tell (the smash is called off; Boolossi already out
/// carry on). The lick has no crown colour (the doc). The faces and the other attacks are
/// later passes; the animations another hand's. The model is the casino boss's (btelesa.glb at 1 / 74.44) with its shader materials
/// (BossTelesaBody/Crown/Ha/Mouth.tres: opacity, tint, mask_k, base_tex), copied per instance. Signals: Spawned (the appear starts), Laughed
/// (the laugh starts), ReadyForFight (the wait begins -- the fight may start), Hit, Defeated; 162: Teleported (he has arrived somewhere), LickStarted
/// (the wind-up), Licked(hit) (the whip has passed, with whether it got him); 164: SmashStarted (the tell), Smashed(hit) (the Boolossus has landed).
/// </summary>
public partial class SmsKingBooRematch : Node3D
{
    /// <summary>The ghost's screen mix (the casino boss's GhostOpacity; the game's ghost is 0.588).</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float GhostOpacity = 0.72f;
    /// <summary>The glow quad's strength (x his opacity) and whether the twinkle stars show.</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float Glow = 0.9f;
    [Export] public bool Twinkle = true;
    /// <summary>The hover: he bobs this many metres up and down over this many seconds (0 = still).</summary>
    [Export] public float HoverBob = 0.35f, HoverBobSeconds = 2.4f;
    /// <summary>He turns to face Mario while he waits, at this many radians a second.</summary>
    [Export] public bool FaceMario = true;
    [Export] public float TurnSpeed = 3f;
    /// <summary>The long laugh at the laugh clip (through SmsActorSound: his sound node) and its gain.</summary>
    [Export] public string LaughSe = "MSD_SE_BS_TELESA_V_LAUGH1";
    [Export] public float LaughDb = 0f;
    [Export] public bool Blink = true;
    [Export] public Texture2D EyeOpen, EyeHalf, EyeClose;
    [ExportGroup("The fight (158)")]
    /// <summary>The doc's three hits; each Bomb Boo burst on him takes one.</summary>
    [Export(PropertyHint.Range, "1,9,1")] public int HitPoints = 3;
    /// <summary>The hurt: the hit clip with this many full turns about himself (the flip, until its animation comes), the flash's colour, and how
    /// much more solid he gets per hit (the casino boss's +30 / 255 is 0.12).</summary>
    [Export] public float HurtTurns = 1f;
    [Export] public Color HurtFlash = new Color(1f, 0.35f, 0.35f);
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float OpacityPerHit = 0.1f;
    /// <summary>The hurt's sound (the casino boss's damage cry) and the down's.</summary>
    [Export] public string HurtSe = "MSD_SE_BS_TELESA_DAMAGE", DownSe = "MSD_SE_BS_TELESA_DOWN";
    /// <summary>159: his BODY for the swung Bomb Boo -- a cylinder about jnt_body: this radius, this far under the joint to this far over it (metres; the
    /// game's body hit cylinder is 350 u across, 550 tall from 200 under the joint: 4.7 / 2.7 / 4.7). A little inside the visible body.</summary>
    [Export] public float HitRadius = 4f, HitBelow = 2.5f, HitAbove = 4.5f;
    [ExportGroup("The fight (162): the teleports and the Lick")]
    /// <summary>The moves run at all (off: he waits as before, for the earlier passes' tests).</summary>
    [Export] public bool AttacksOn = true;
    /// <summary>Seconds from the fight's start (the wait begins) to his first move.</summary>
    [Export] public float FirstAttackDelay = 3f;
    /// <summary>The gap between moves: this long with all his hit points, this long at his last one (the doc's pace: shorter per hit), give or take the jitter.</summary>
    [Export] public float GapAtStart = 3.5f, GapAtLastHit = 1.8f, GapJitter = 0.6f;
    /// <summary>The chance a move is only a hop to another spot of the arena (no lick); the gap after one is half.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float RepositionChance = 0.3f;
    /// <summary>The teleport: the fade out (the hide clip plays under it: its hop up; longer than 0.5 s shows its dive down too), the fade in, how far he
    /// settles down as he fades in, and the clip for the vanish ("" = none).</summary>
    [Export] public float TeleportOutSeconds = 0.4f, TeleportInSeconds = 0.3f, TeleportInDrop = 1.2f;
    [Export] public string TeleportClip = "btelesa_hide";
    [Export] public string VanishSe = "MSD_SE_BS_TELESA_DISAPPEAR", ArriveSe = "MSD_SE_BS_TELESA_V_APPEAR";
    /// <summary>The spots: at least this far from Mario, at most this far (0 = no cap; the arena is 26 m across), within this radius of the arena's middle
    /// (0 = three quarters of the floor's own).</summary>
    [Export] public float SpotMinFromMario = 7f, SpotMaxFromMario = 16f, SpotRadius = 0f;
    /// <summary>The lick's spot: this far in front of Mario along his face, his body joint this high over the floor (0 = his hover height). The roll's
    /// whip crosses the front 8.7 m out from his body joint, its tip 5.7 m under the body origin as it passes straight ahead and 6.4 m under at its lowest
    /// (frame 62, to his right): at 8.7 / 6.4 the TIP skims the floor through Mario's legs, 0.7 m up, and nothing of the tongue goes into the floor; the
    /// shaft behind it is 2.8 m up, over a jumping Mario. Lower, and the tongue comes through higher up him but its tip digs into the floor.</summary>
    [Export] public float LickDistance = 8.7f, LickHeight = 6.4f;
    /// <summary>The clip for the lick and its speed (btelesa_roll, 139 frames: he rears up to frame 30 with the tongue overhead, the tongue whips across the
    /// front over frames 50-66 as he plunges, the body is back at its height by 80; at 1.5 the crossing starts 0.56 s in). The hit window: the clip
    /// frames the tongue is live (the front crossing). The frame the clip is cut at for the wait.</summary>
    [Export] public string LickClip = "btelesa_roll";
    [Export] public float LickClipSpeed = 1.5f;
    [Export] public int LickHitFromFrame = 50, LickHitToFrame = 66, LickEndFrame = 80;
    /// <summary>How long he stays in the wait after the lick before he vanishes.</summary>
    [Export] public float AfterLickSeconds = 0.4f;
    /// <summary>The tongue's thickness for the hit (added to Mario's damage cylinder, 0.56 m across), and how much longer than the clips' the tongue is (163:
    /// "stick out more, not much but a bit"; 1 = as the clips have it).</summary>
    [Export] public float LickThickness = 0.35f;
    [Export(PropertyHint.Range, "1,1.6,0.01")] public float TongueStretch = 1.15f;
    /// <summary>The body joint never dips closer than this to the floor through the lick clip's plunge (the node lifts).</summary>
    [Export] public float BodyMinOverFloor = 2.6f;
    [Export] public string LickWindSe = "MSD_SE_BS_TELESA_V_ROLL", LickSe = "MSD_SE_BS_TELESA_LICK";
    [ExportGroup("The fight (164): the Boolossus Smash")]
    /// <summary>The smash runs at all; of his attacks this share are smashes (the rest licks).</summary>
    [Export] public bool SmashOn = true;
    [Export(PropertyHint.Range, "0,1,0.05")] public float SmashChance = 0.5f;
    /// <summary>The Boolossus (Boolossus.tscn carries its own knobs: the height, the fall, the wave).</summary>
    [Export] public PackedScene SmashScene;
    /// <summary>The tell: his crown glows this colour (the doc: white) pulsing at this rate for this long before the first Boolossus; the clip he plays
    /// through the smash and its voice; how long he watches after the last Boolossus is out before the wait and the gap run again.</summary>
    [Export] public Color SmashCrownColor = new Color(1f, 1f, 1f);
    [Export] public float SmashTellSeconds = 0.6f, CrownPulseHz = 6f, SmashWatchSeconds = 1f;
    [Export] public string SmashClip = "btelesa_laugh";
    [Export] public string SmashSe = "MSD_SE_BS_TELESA_V_LAUGH2";
    /// <summary>165: how many Boolossi a smash makes, how far apart in time, and their approach directions (the first at random, the rest this many
    /// degrees on; SmashFromKingBoo: the first from where he stands). The crown's light: its size (metres across; 0 = the shader's glow alone) and
    /// strength.</summary>
    [Export(PropertyHint.Range, "1,6,1")] public int SmashCount = 3;
    [Export] public float SmashStagger = 0.5f, SmashSpreadDeg = 120f;
    [Export] public bool SmashFromKingBoo = true;
    [Export] public float CrownGlowSize = 3.2f, CrownGlowStrength = 1.2f;
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void SpawnedEventHandler();
    [Signal] public delegate void LaughedEventHandler();
    [Signal] public delegate void ReadyForFightEventHandler();
    /// <summary>158: a Bomb Boo burst on him (the hit points left) / the last one took him down.</summary>
    [Signal] public delegate void HitEventHandler(int hpLeft);
    [Signal] public delegate void DefeatedEventHandler();
    /// <summary>162: he has arrived at a spot; the lick's wind-up begins; the tongue is pulled back (whether it got Mario).</summary>
    [Signal] public delegate void TeleportedEventHandler();
    [Signal] public delegate void LickStartedEventHandler();
    [Signal] public delegate void LickedEventHandler(bool hit);
    /// <summary>164: the smash's tell begins; the Boolossus has landed (whether its body got Mario).</summary>
    [Signal] public delegate void SmashStartedEventHandler();
    [Signal] public delegate void SmashedEventHandler(bool hitMario);

    public enum Phase { Hidden, Appear, Laugh, Wait, Hurt, Down, Gone, Vanish, Arrive, Lick, Smash }
    public enum Move { None, Hop, Lick, Smash }
    public Phase State { get; private set; } = Phase.Hidden;
    public string Clip => _anim != null ? (string)_anim.CurrentAnimation : "";
    public bool IsSpawned => State != Phase.Hidden;
    public bool LaughPlayed { get; private set; }
    public int Hp { get; private set; } = -1;
    public int HitsTaken { get; private set; }
    /// <summary>A Bomb Boo bursts on him while he waits or licks (not between spots, through the hurt, the down, the entrance).</summary>
    public bool CanBeHit => State == Phase.Wait || State == Phase.Lick || State == Phase.Smash;
    public bool IsAlive => State != Phase.Down && State != Phase.Gone;
    public int MaterialsCopied => _mats.Count;
    /// <summary>162, for the drives: the lick's stage (-1 none, 0 wind-up, 1 the whip live, 2 the clip's tail, 3 the wait after), the counts, the gap left,
    /// the last spot, his hover height over the floor, the arena's middle and radius as he read them, the tongue.</summary>
    public int LickStage => State == Phase.Lick ? _lickStage : -1;
    public int MovesMade { get; private set; }
    public int TeleportsMade { get; private set; }
    public int LicksMade { get; private set; }
    public int LickHits { get; private set; }
    public int LickMisses { get; private set; }
    public float GapNow => _gapT;
    public float LastGap { get; private set; }
    public bool LastMoveWasLick => LastMove == Move.Lick;
    public Move LastMove { get; private set; }
    /// <summary>164: the smashes made, how many landed ON Mario, the last Boolossus (while it lives), the crown's colour register now (yellow at rest).</summary>
    public int SmashesMade { get; private set; }
    public int SmashHits { get; private set; }
    public SmsBoolossus Boolossus => _boolossus != null && IsInstanceValid(_boolossus) ? _boolossus : null;
    public Vector3 CrownColorNow => _crownNow;
    public bool SmashLaunched => _smashLaunched;
    /// <summary>165: the crown's glow now (0 = dark, its colour), how many Boolossi this smash has launched, the last one's approach direction.</summary>
    public float CrownGlowK => _crownGlowK;
    public Color CrownGlowColor => _crownGlowColor;
    public bool CrownGlowShown => _crownGlow != null && _crownGlow.Visible;
    public int SmashLaunchedCount => _smashLaunchedCount;
    public Vector3 LastApproachDir => _lastApproach;
    public Vector3 LastSpot { get; private set; }
    public float HoverOverFloor => _hoverOverFloor;
    public Vector3 ArenaCentre => _arenaCentre;
    public float ArenaRadius => _arenaRadius;
    public SmsTongueStretch Tongue => _tongue;
    public float OpacityShown => _opacityShown;
    /// <summary>jnt_body's place in the world (where the glow sits).</summary>
    public Vector3 BodyPoint => _skel != null && _bBody >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(_bBody).Origin : GlobalPosition;
    /// <summary>159: a point (a swung Bomb Boo, with its own reach) is in his body's hit cylinder (HitRadius, HitBelow, HitAbove about jnt_body).</summary>
    public bool InHitCylinder(Vector3 p, float extra = 0f)
    {
        Vector3 b = BodyPoint;
        if (p.Y < b.Y - HitBelow - extra || p.Y > b.Y + HitAbove + extra) return false;
        return new Vector2(p.X - b.X, p.Z - b.Z).Length() <= HitRadius + extra;
    }
    /// <summary>The cylinder's lowest point over the world's floor, for the drives.</summary>
    public float HitBottomY => BodyPoint.Y - HitBelow;

    private const float U = 74.44f;
    // the clips used, their bck frame counts (one bck frame = 1 / 60 s) and loops -- SmsBossTelesa's table
    private const string BckAppear = "btelesa_appear", BckLaugh = "btelesa_laugh", BckWait = "btelesa_wait", BckHit = "btelesa_hit", BckDown = "btelesa_down";
    private static readonly Dictionary<string, (int frames, bool loop)> Clips = new()
    {
        [BckAppear] = (59, false), [BckLaugh] = (89, false), [BckWait] = (60, true), [BckHit] = (42, false), [BckDown] = (199, false),
        ["btelesa_hide"] = (44, false), ["btelesa_roll"] = (139, false), ["btelesa_lick"] = (140, false), ["btelesa_spit"] = (99, false), ["btelesa_bero_hit"] = (59, false), ["btelesa_damage"] = (219, false),
    };
    private static readonly int[] BlinkEnds = { 19, 21, 24, 26, 28, 30, 33, 35, 85, 87, 90, 92, 150 };   // the twait runs' ends (frames)
    private static readonly int[] BlinkEyes = { 0, 1, 2, 1, 0, 1, 2, 1, 0, 1, 2, 1, 0 };                   // open / half / close

    private Node3D _model; private AnimationPlayer _anim; private Skeleton3D _skel; private int _bBody = -1;
    private readonly List<ShaderMaterial> _mats = new(); private ShaderMaterial _bodyMat, _crownMat; private Vector3 _crownRest = new(1f, 1f, 0f), _crownNow = new(1f, 1f, 0f);
    private SmsBoolossus _boolossus; private float _smashT; private bool _smashLaunched, _smashClipEnded; private int _smashLaunchedCount, _bCrown = -1; private float _smashNextAt, _smashBaseAngle; private Vector3 _lastApproach;
    private MeshInstance3D _crownGlow; private StandardMaterial3D _crownGlowMat; private float _crownGlowK; private Color _crownGlowColor = new(1f, 1f, 1f);
    private GpuParticles3D _chika; private Node3D _glow; private SmsActorSound _sound; private SmsTongueStretch _tongue;
    private Mario _mario; private float _yaw, _hoverT, _blinkT; private int _eyeNow = -1; private Vector3 _home; private bool _laughSounded;
    private float _hurtT, _hurtLen, _hurtYaw0; private bool _flashOn;
    // 162
    private readonly Random _rng = new(162);
    private float _gapT, _fadeT, _opacitySolid, _opacityShown, _hoverOverFloor = 4f, _arenaRadius = 12f, _floorAtSpot = float.NaN;
    private Vector3 _arenaCentre, _dest; private bool _moveIsLick, _lickHit, _lickClipEnded, _arenaRead;
    private int _lickStage = -1; private float _lickT, _stageT;

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>("Model");
        _anim = GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_model != null)
        {
            if (_anim == null) foreach (Node n in _model.FindChildren("*", "AnimationPlayer", true, false)) { _anim = n as AnimationPlayer; if (_anim != null) break; }
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null)
            {
                _bBody = _skel.FindBone("jnt_body"); if (_bBody < 0 && _skel.GetBoneCount() > 1) _bBody = 1;
                _bCrown = _skel.FindBone("jnt_crown");
                if (!Engine.IsEditorHint()) { _tongue = new SmsTongueStretch { Name = "TongueStretch", Debug = DebugPrint, Stretch = TongueStretch }; _skel.AddChild(_tongue); }
            }
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    if (mi.GetActiveMaterial(i) is not ShaderMaterial sm) continue;
                    var copy = (ShaderMaterial)sm.Duplicate();
                    mi.SetSurfaceOverrideMaterial(i, copy);
                    _mats.Add(copy);
                    if ((sm.ResourceName ?? "").StartsWith("_mat_body") || _bodyMat == null) _bodyMat = copy;
                    if ((sm.ResourceName ?? "").StartsWith("_mat_crown")) { _crownMat = copy; var c0 = copy.GetShaderParameter("crown_c0"); if (c0.VariantType == Variant.Type.Vector3) _crownRest = _crownNow = c0.AsVector3(); }
                }
            }
        }
        else GD.PushWarning($"[SMS kingboo] {GetPath()}: no Model child (btelesa.glb)");
        if (_anim != null) _anim.AnimationFinished += OnClipEnd;
        EyeOpen ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_open.png");
        EyeHalf ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_half.png");
        EyeClose ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_close.png");
        _chika = GetNodeOrNull<GpuParticles3D>("ChikaFx"); _glow = GetNodeOrNull<Node3D>("GlowFx");
        foreach (Node3D fx in new Node3D[] { _chika, _glow }) if (fx != null) { fx.TopLevel = true; fx.Visible = false; }
        if (!Engine.IsEditorHint() && CrownGlowSize > 0f)                                 // 165: the crown's light -- a camera-facing additive quad of the port's radial gradient
        {
            var tex = ResourceLoader.Exists("res://assets/Sms/Fx/tex/radgrad_raw.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/radgrad_raw.png") : null;
            _crownGlowMat = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = true,
                AlbedoTexture = tex, AlbedoColor = new Color(1f, 1f, 1f, 0f), RenderPriority = 3
            };
            _crownGlow = new MeshInstance3D { Name = "CrownGlowFx", TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One * CrownGlowSize }, MaterialOverride = _crownGlowMat };
            AddChild(_crownGlow);
        }
        _opacitySolid = GhostOpacity;
        foreach (var m in _mats) { m.SetShaderParameter("tint", new Vector3(1f, 1f, 1f)); m.SetShaderParameter("mask_k", 0f); }
        SetOpacity(GhostOpacity);
        ApplyEye(0);
        _sound = SmsActorSound.Attach(this);
        SmashScene ??= ResourceLoader.Exists("res://assets/Sms/Rematch/Boolossus.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Rematch/Boolossus.tscn") : null;
        _yaw = Rotation.Y; _home = Position;
        if (_model != null) _model.Visible = false;
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: {_mats.Count} materials, clips {(_anim != null ? string.Join(",", _anim.GetAnimationList()) : "none")}, body joint {_bBody}, tongue {(_tongue != null && _tongue.Bound ? "bound" : "none")}, crown material {(_crownMat != null ? "found" : "NONE")}, crown joint {_bCrown}, Boolossus scene {(SmashScene != null ? "loaded" : "NONE")}");
        SmsKingBooDriveHook();
    }
    partial void SmsKingBooDriveHook();
    private static Texture2D LoadTex(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

    /// <summary>He appears: the appear clip, then the laugh with the long laugh, then the wait. Nothing happens twice.</summary>
    public void Spawn()
    {
        if (State != Phase.Hidden || Engine.IsEditorHint()) return;
        State = Phase.Appear; _home = Position; _hoverT = 0f;   // the hover bobs about where he was put
        Hp = Math.Max(1, HitPoints); HitsTaken = 0;
        if (_model != null) _model.Visible = true;
        if (_tongue != null) { _tongue.Stretch = TongueStretch; _tongue.On = TongueStretch > 1.001f; }
        SetCrownGlow(_crownGlowColor, 0f);
        PlayClip(BckAppear);
        ReadArena();
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: appears at {GlobalPosition} ({_hoverOverFloor:F2} m over the floor; the arena's middle {_arenaCentre}, radius {_arenaRadius:F1})");
        EmitSignal(SignalName.Spawned);
    }

    /// <summary>162: his hover height over the floor from where he was put, the arena's middle and radius from its floor (SmsArenaFloor), for the spots.</summary>
    private void ReadArena()
    {
        float fy = FloorUnder(GlobalPosition);
        _hoverOverFloor = float.IsNaN(fy) ? 4f : Mathf.Max(0.5f, GlobalPosition.Y - fy);
        _arenaCentre = GlobalPosition; _arenaRadius = 12f;
        var floor = GetTree().CurrentScene?.FindChildren("*", "StaticBody3D", true, false).OfType<SmsArenaFloor>().FirstOrDefault();
        if (floor != null && floor.Radius > 1f) { _arenaCentre = floor.Centre; _arenaRadius = floor.Radius; }
        _arenaRead = true;
    }
    /// <summary>The ground under a point (a ray from 100 m over it, 400 m down, Mario left out): its world y, or NaN.</summary>
    private float FloorUnder(Vector3 p)
    {
        var space = GetWorld3D()?.DirectSpaceState; if (space == null) return float.NaN;
        var q = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 100f, p + Vector3.Down * 300f);
        q.CollideWithAreas = false; q.CollideWithBodies = true;
        var m = M(); if (m != null) q.Exclude = new Godot.Collections.Array<Rid> { m.GetRid() };
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].AsVector3().Y : float.NaN;
    }
    private Mario M()
    {
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        return _mario;
    }
    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private void PlayClip(string name)
    {
        if (_anim == null || !_anim.HasAnimation(name)) { if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: no clip {name}"); CallDeferred(nameof(OnClipEnd), new StringName(name)); return; }
        var clip = _anim.GetAnimation(name);
        var (frames, loop) = Clips.TryGetValue(name, out var c) ? c : (60, false);
        clip.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        float rate = clip.Length > 0f ? frames / (float)clip.Length : 60f;
        _anim.Play(name);
        _anim.Seek(0f, true);
        _anim.SpeedScale = 60f / rate;
    }
    /// <summary>The bck frame the clip named is at now (a big number when it is not the one playing).</summary>
    private float ClipFrameNow(string name)
    {
        if (_anim == null || !_anim.IsPlaying()) return 9999f;
        string cur = _anim.CurrentAnimation;
        if (string.IsNullOrEmpty(cur) || cur != name) return 9999f;
        var clip = _anim.GetAnimation(cur);
        int frames = Clips.TryGetValue(cur, out var c) ? c.frames : 60;
        return clip.Length > 0f ? (float)(_anim.CurrentAnimationPosition / clip.Length) * frames : 9999f;
    }

    private void OnClipEnd(StringName name)
    {
        string n = name;
        if (State == Phase.Appear && n == BckAppear)
        {
            State = Phase.Laugh; _laughSounded = false;
            PlayClip(BckLaugh);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the laugh");
            EmitSignal(SignalName.Laughed);
        }
        else if (State == Phase.Laugh && n == BckLaugh)
        {
            EnterWait(FirstAttackDelay);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: waits -- the fight may start (the first move in {FirstAttackDelay:F1} s)");
            EmitSignal(SignalName.ReadyForFight);
        }
        else if (State == Phase.Hurt && n == BckHit)
        {
            SetFlash(false); Rotation = new Vector3(0f, _yaw, 0f);
            if (Hp > 0) { EnterWait(NextGap()); if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait, {Hp} hit point(s) left, the next move in {_gapT:F1} s"); }
            else
            {
                State = Phase.Down; PlayClip(BckDown);
                if (_sound != null && !string.IsNullOrEmpty(DownSe)) _sound.Play(DownSe);
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: DOWN");
            }
        }
        else if (State == Phase.Down && n == BckDown)
        {
            State = Phase.Gone;
            if (_model != null) _model.Visible = false;
            PlaceFx();
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: gone -- defeated (the fight's end is a later pass)");
            EmitSignal(SignalName.Defeated);
        }
        else if (State == Phase.Lick && n == LickClip) _lickClipEnded = true;
        else if (State == Phase.Smash && n == SmashClip) { if (_smashLaunchedCount < SmashCount) PlayClip(SmashClip); else _smashClipEnded = true; }
    }

    private void EnterWait(float gap)
    {
        State = Phase.Wait; _hoverT = 0f; Position = _home; _gapT = gap; LastGap = gap;
        PlayClip(BckWait);
    }
    /// <summary>The gap before the next move: GapAtStart with every hit point, GapAtLastHit at his last one, give or take GapJitter.</summary>
    private float NextGap()
    {
        float k = HitPoints > 1 ? Mathf.Clamp(HitsTaken / (float)(HitPoints - 1), 0f, 1f) : 1f;
        float g = Mathf.Lerp(GapAtStart, GapAtLastHit, k) + ((float)_rng.NextDouble() * 2f - 1f) * GapJitter;
        return Mathf.Max(0.3f, g);
    }

    /// <summary>158: a Bomb Boo burst on him. A point off, the hurt (the hit clip, HurtTurns about himself, the flash), the Hit signal; the last takes him
    /// down. False when he cannot be hit now. 162: through a lick it breaks the lick off.</summary>
    public bool TakeHit(SmsBombBoo by)
    {
        if (!CanBeHit) return false;
        bool midLick = State == Phase.Lick, midSmash = State == Phase.Smash;
        AbortLick(); AbortSmash();
        Hp = Math.Max(0, Hp - 1); HitsTaken++;
        State = Phase.Hurt; _hurtT = 0f; _hurtYaw0 = _yaw; Position = _home;
        PlayClip(BckHit);
        _hurtLen = _anim != null && _anim.HasAnimation(BckHit) ? (float)(_anim.GetAnimation(BckHit).Length / Mathf.Max(0.01f, _anim.SpeedScale)) : 0.7f;
        SetFlash(true);
        _opacitySolid = Mathf.Clamp(GhostOpacity + OpacityPerHit * HitsTaken, 0.1f, 1f);
        SetOpacity(_opacitySolid);
        if (_sound != null && !string.IsNullOrEmpty(HurtSe)) _sound.Play(HurtSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: HIT by {by?.Name ?? "?"}{(midLick ? " mid-lick" : midSmash ? " mid-smash (the tell called off)" : "")} -> {Hp} left (the hit clip, {_hurtLen:F2} s)");
        EmitSignal(SignalName.Hit, Hp);
        return true;
    }
    private void AbortLick() { _lickStage = -1; _lickClipEnded = false; }
    private void AbortSmash() { SetCrownGlow(_crownGlowColor, 0f); _smashClipEnded = false; }
    private void SetCrown(Vector3 c) { _crownNow = c; _crownMat?.SetShaderParameter("crown_c0", c); }
    /// <summary>165: the crown GLOWS <paramref name="color"/> by <paramref name="k"/> (0 = its own yellow, dark): the light at the crown joint, the shader's glow
    /// term, the colour register toward the colour.</summary>
    private void SetCrownGlow(Color color, float k)
    {
        _crownGlowColor = color; _crownGlowK = Mathf.Clamp(k, 0f, 1f);
        var c = new Vector3(color.R, color.G, color.B);
        SetCrown(_crownRest.Lerp(c, _crownGlowK));
        _crownMat?.SetShaderParameter("crown_glow", c); _crownMat?.SetShaderParameter("crown_glow_k", _crownGlowK * 0.9f);
        if (_crownGlow != null)
        {
            _crownGlow.Visible = _crownGlowK > 0.02f && State != Phase.Hidden && State != Phase.Gone;
            if (_crownGlowMat != null) _crownGlowMat.AlbedoColor = new Color(color.R, color.G, color.B, Mathf.Clamp(_crownGlowK * CrownGlowStrength, 0f, 1f));
        }
    }
    /// <summary>jnt_crown's place in the world (the crown's light sits there), else a little over the body joint.</summary>
    public Vector3 CrownPoint => _skel != null && _bCrown >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(_bCrown).Origin : BodyPoint + Vector3.Up * 3f;
    private void SetFlash(bool on)
    {
        _flashOn = on;
        foreach (var m in _mats)
        {
            m.SetShaderParameter("tint", on ? new Vector3(HurtFlash.R, HurtFlash.G, HurtFlash.B) : new Vector3(1f, 1f, 1f));
            m.SetShaderParameter("mask_k", on ? 1f : 0f);
        }
    }
    private void SetOpacity(float o)
    {
        _opacityShown = o;
        foreach (var m in _mats) m.SetShaderParameter("opacity", o);
    }

    // ------------------------------------------------------------------------------------------------------------------ 162: the moves
    /// <summary>A move: he vanishes (the hide clip under the fade), to arrive in front of Mario for the lick, or (by RepositionChance, or when told) at another spot.</summary>
    private void StartMove(bool? lick = null)
    {
        if (M() == null) { _gapT = 0.5f; return; }
        Move pick = lick.HasValue ? (lick.Value ? Move.Lick : Move.Hop) : PickMove();
        LastMove = pick;
        if (pick == Move.Smash) { BeginSmash(); return; }
        _moveIsLick = pick == Move.Lick;
        AbortLick();
        State = Phase.Vanish; _fadeT = 0f; MovesMade++;
        if (!string.IsNullOrEmpty(TeleportClip)) PlayClip(TeleportClip);
        if (_sound != null && !string.IsNullOrEmpty(VanishSe)) _sound.Play(VanishSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: vanishes from {GlobalPosition} ({(_moveIsLick ? "for the LICK" : "to another spot")})");
    }
    /// <summary>The doc's near-random pick: a hop by RepositionChance, else an attack -- a smash by SmashChance (when on and its scene is there), else the lick.</summary>
    private Move PickMove()
    {
        if (_rng.NextDouble() < RepositionChance) return Move.Hop;
        if (SmashOn && SmashScene != null && _rng.NextDouble() < SmashChance) return Move.Smash;
        return Move.Lick;
    }
    /// <summary>164: the smash, where he stands: the laugh clip and its voice, the crown pulsing white through the tell, then the Boolossus over Mario.</summary>
    private void BeginSmash()
    {
        State = Phase.Smash; _smashT = 0f; _smashLaunched = false; _smashClipEnded = false; _smashLaunchedCount = 0; _smashNextAt = SmashTellSeconds; MovesMade++; SmashesMade++; Position = _home;
        var m0 = M();
        _smashBaseAngle = SmashFromKingBoo && m0 != null ? MathF.Atan2(GlobalPosition.X - m0.GlobalPosition.X, GlobalPosition.Z - m0.GlobalPosition.Z) : (float)(_rng.NextDouble() * Math.Tau);
        PlayClip(SmashClip);
        if (_sound != null && !string.IsNullOrEmpty(SmashSe)) _sound.Play(SmashSe);
        SetCrownGlow(SmashCrownColor, 0f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: SMASH {SmashesMade} -- the crown glows {SmashCrownColor} ({SmashCount} Boolossi from {SmashTellSeconds:F2} s, {SmashStagger:F2} s apart)");
        EmitSignal(SignalName.SmashStarted);
    }
    private void SmashTick(float dt)
    {
        _smashT += dt;
        FaceTick(dt);
        if (_smashLaunchedCount < SmashCount)
        {
            if (!_smashLaunched)                                      // the tell: the crown pulsing up to the colour
            {
                float k = 0.5f + 0.5f * MathF.Sin(_smashT * MathF.Tau * CrownPulseHz - MathF.PI / 2f);
                SetCrownGlow(SmashCrownColor, Mathf.Lerp(0.35f, 1f, k) * Mathf.Clamp(_smashT / 0.15f, 0f, 1f));
            }
            if (_smashT >= _smashNextAt)
            {
                var m = M();
                if (m == null || SmashScene == null) { EndSmash(); return; }
                int i = _smashLaunchedCount;
                float ang = _smashBaseAngle + Mathf.DegToRad(SmashSpreadDeg) * i;
                _lastApproach = new Vector3(MathF.Sin(ang), 0f, MathF.Cos(ang));
                var b = SmashScene.Instantiate<SmsBoolossus>(); b.Name = $"Boolossus{SmashesMade}_{i + 1}"; b.DebugPrint = DebugPrint;
                (GetTree().CurrentScene ?? GetParent()).AddChild(b);
                float fy = FloorUnder(m.GlobalPosition); if (float.IsNaN(fy)) fy = m.GlobalPosition.Y;
                b.Landed += hit => { if (hit) SmashHits++; if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: a Boolossus has landed -- {(hit ? "ON Mario" : "a miss")}"); EmitSignal(SignalName.Smashed, hit); };
                b.Begin(m, fy, _lastApproach);
                _boolossus = b; _smashLaunched = true; _smashLaunchedCount++; _smashNextAt += SmashStagger;
                SetCrownGlow(SmashCrownColor, 1f);
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: Boolossus {_smashLaunchedCount} of {SmashCount} dives in from {_lastApproach} at Mario {m.GlobalPosition}");
            }
        }
        else if (_smashT >= _smashNextAt - SmashStagger + SmashWatchSeconds || _smashClipEnded) EndSmash();
    }
    private void EndSmash()
    {
        SetCrownGlow(_crownGlowColor, 0f); _smashClipEnded = false;
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait after the smash, the next move in {_gapT:F1} s");
    }
    /// <summary>Another spot of the arena: within SpotRadius of its middle (three quarters of the floor's radius), SpotMinFromMario to SpotMaxFromMario from Mario, over floor.</summary>
    private Vector3 RandomSpot()
    {
        var m = M();
        float radius = SpotRadius > 0f ? SpotRadius : _arenaRadius * 0.75f;
        for (int i = 0; i < 16; i++)
        {
            float a = (float)(_rng.NextDouble() * Math.Tau), r = radius * Mathf.Sqrt((float)_rng.NextDouble());
            var p = new Vector3(_arenaCentre.X + Mathf.Cos(a) * r, _arenaCentre.Y, _arenaCentre.Z + Mathf.Sin(a) * r);
            if (m != null && (Flat(p, m.GlobalPosition) < SpotMinFromMario || (SpotMaxFromMario > 0f && Flat(p, m.GlobalPosition) > SpotMaxFromMario))) continue;
            float fy = FloorUnder(p); if (float.IsNaN(fy)) continue;
            _floorAtSpot = fy; p.Y = fy + _hoverOverFloor; return p;
        }
        _floorAtSpot = FloorUnder(_home); return _home;
    }
    /// <summary>The lick's spot: LickDistance in front of Mario along his face (behind him, or beside him, when there is no floor there), LickHeight over the floor.</summary>
    private Vector3 LickSpot()
    {
        var m = M(); Vector3 mp = m.GlobalPosition;
        Vector3 f = m.SmsFacingDir(); f.Y = 0f; f = f.LengthSquared() > 1e-6f ? f.Normalized() : Vector3.Forward;
        float h = LickHeight > 0f ? LickHeight : _hoverOverFloor;
        foreach (Vector3 dir in new[] { f, -f, f.Cross(Vector3.Up), Vector3.Up.Cross(f) })
        {
            Vector3 p = mp + dir * LickDistance;
            float fy = FloorUnder(p); if (float.IsNaN(fy)) continue;
            _floorAtSpot = fy; p.Y = fy + h; return p;
        }
        float under = FloorUnder(mp); _floorAtSpot = float.IsNaN(under) ? mp.Y : under;
        return mp + f * LickDistance + Vector3.Up * (_floorAtSpot - mp.Y + h);
    }
    private void ArriveAt(Vector3 dest)
    {
        _dest = dest; _home = dest; LastSpot = dest; TeleportsMade++;
        Position = dest + Vector3.Up * TeleportInDrop;
        FaceNow();
        State = Phase.Arrive; _fadeT = 0f;
        PlayClip(BckWait);
        SetOpacity(0f);
        if (_sound != null && !string.IsNullOrEmpty(ArriveSe)) _sound.Play(ArriveSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: arrives at {dest} (floor {_floorAtSpot:F2}; Mario {(M() != null ? Flat(dest, M().GlobalPosition).ToString("F1") : "?")} m off)");
        EmitSignal(SignalName.Teleported);
    }
    private void BeginLick()
    {
        State = Phase.Lick; _lickStage = 0; _lickT = 0f; _stageT = 0f; _lickHit = false; _lickClipEnded = false; LicksMade++;
        PlayClip(LickClip);
        if (_anim != null) _anim.SpeedScale *= Mathf.Max(0.1f, LickClipSpeed);
        if (_sound != null && !string.IsNullOrEmpty(LickWindSe)) _sound.Play(LickWindSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: LICK {LicksMade}: the wind-up ({LickClip} x{LickClipSpeed}; the whip live over frames {LickHitFromFrame}-{LickHitToFrame}: from {LickHitFromFrame / 60f / Mathf.Max(0.1f, LickClipSpeed):F2} s)");
        EmitSignal(SignalName.LickStarted);
    }
    private void LickTick(float dt)
    {
        _lickT += dt; _stageT += dt;
        var m = M();
        float frame = ClipFrameNow(LickClip);
        switch (_lickStage)
        {
            case 0:                                                   // the wind-up: he rears up, the tongue overhead; still turning to him
                FaceTick(dt);
                if (frame >= LickHitFromFrame)
                {
                    _lickStage = 1; _stageT = 0f;
                    if (_sound != null && !string.IsNullOrEmpty(LickSe)) _sound.Play(LickSe);
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the whip is LIVE (frame {frame:F0}, {_lickT:F2} s in; Mario {(m != null ? (m.IsOnFloor() ? "on the floor" : $"in the air, {m.GlobalPosition.Y - _floorAtSpot:F2} m over the floor") : "?")})");
                }
                break;
            case 1:                                                   // the whip across the front: the hit window
                LickHitCheck(m);
                if (_lickClipEnded || frame > LickHitToFrame)
                {
                    _lickStage = 2; _stageT = 0f;
                    if (!_lickHit) LickMisses++;
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the whip has passed -- {(_lickHit ? "it GOT him" : "a miss")}");
                    EmitSignal(SignalName.Licked, _lickHit);
                }
                break;
            case 2:                                                   // the clip's tail, cut at LickEndFrame
                if (_lickClipEnded || frame >= LickEndFrame) { _lickStage = 3; _stageT = 0f; PlayClip(BckWait); }
                break;
            case 3:                                                   // a breath in the wait, then off to another spot
                if (_stageT >= AfterLickSeconds) StartMove(false);
                break;
        }
        // the body kept out of the floor through the clip's plunge
        if (!float.IsNaN(_floorAtSpot) && BodyMinOverFloor > 0f)
        {
            float bodyLocal = BodyPoint.Y - GlobalPosition.Y;
            float lift = Mathf.Max(0f, _floorAtSpot + BodyMinOverFloor - (_home.Y + bodyLocal));
            Position = _home + Vector3.Up * lift;
        }
    }
    /// <summary>The whip on Mario, once per lick: any tongue joint, or the point between two, inside his damage cylinder (42 u across, 130 u tall from his
    /// feet) grown by LickThickness -- the plain hit: Mario.TakeDamage with the common enemy profile, the knockdown, from the tongue point that got him.</summary>
    private void LickHitCheck(Mario m)
    {
        if (_lickHit || m == null || _tongue == null || !_tongue.PointsValid) return;
        Vector3 feet = m.GlobalPosition;
        float r = SmsParams.Mario.DamageRadius / U + LickThickness, bottom = feet.Y - LickThickness, top = feet.Y + SmsParams.Mario.DamageHeight / U + LickThickness;
        var pts = _tongue.Points;
        for (int i = 0; i < pts.Length; i++)
        {
            if (In(pts[i])) { Land(pts[i]); return; }
            if (i + 1 < pts.Length) { Vector3 mid = (pts[i] + pts[i + 1]) * 0.5f; if (In(mid)) { Land(mid); return; } }
        }
        bool In(Vector3 p) => p.Y >= bottom && p.Y <= top && Flat(p, feet) <= r;
        void Land(Vector3 p)
        {
            _lickHit = true; LickHits++;
            m.TakeDamage(p, Mario.SmsDamageProfile.EnemyCommon);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the whip GETS Mario at {p} (Mario at {feet}; {p.Y - feet.Y:F2} m over his feet; hp {m.SmsHP})");
        }
    }
    private void FaceNow()
    {
        var m = M(); if (m == null) return;
        Vector3 d = m.GlobalPosition - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() > 1e-4f) { _yaw = MathF.Atan2(d.X, d.Z); Rotation = new Vector3(0f, _yaw, 0f); }
    }
    private void FaceTick(float dt)
    {
        if (!FaceMario) return;
        var m = M(); if (m == null) return;
        Vector3 d = m.GlobalPosition - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() > 1e-4f)
        {
            float want = MathF.Atan2(d.X, d.Z);     // +Z forward: the model faces +Z at yaw 0 (the casino places him at rotation 180 to face its camera)
            _yaw = Mathf.LerpAngle(_yaw, want, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
            Rotation = new Vector3(0f, _yaw, 0f);
        }
    }

    public override void _Process(double delta)
    {
        if (State == Phase.Hidden || Engine.IsEditorHint()) return;
        float dt = (float)delta;
        // the long laugh with the laugh clip (the casino boss plays it two bck frames in: a thirtieth of a second, dropped here)
        if (State == Phase.Laugh && !_laughSounded)
        {
            _laughSounded = true; LaughPlayed = true;
            if (_sound != null && !string.IsNullOrEmpty(LaughSe)) _sound.Play(LaughSe, LaughDb);
            else if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: no sound node for the laugh");
        }
        if (State == Phase.Hurt)
        {
            _hurtT += dt;
            float k = _hurtLen > 0f ? Mathf.Clamp(_hurtT / _hurtLen, 0f, 1f) : 1f;
            Rotation = new Vector3(0f, _hurtYaw0 + HurtTurns * MathF.Tau * Mathf.SmoothStep(0f, 1f, k), 0f);   // the flip about himself, until its animation comes
            bool flash = ((int)(_hurtT * 15f) & 1) == 0 && k < 0.8f;
            if (flash != _flashOn) SetFlash(flash);
        }
        if (State == Phase.Wait)
        {
            _hoverT += dt;
            if (HoverBob > 0f && HoverBobSeconds > 0f) Position = _home + Vector3.Up * (MathF.Sin(_hoverT / HoverBobSeconds * MathF.Tau) * HoverBob);
            FaceTick(dt);
            if (AttacksOn && _gapT > 0f) { _gapT -= dt; if (_gapT <= 0f) StartMove(); }
        }
        else if (State == Phase.Vanish)
        {
            _fadeT += dt;
            float k = TeleportOutSeconds > 0f ? Mathf.Clamp(_fadeT / TeleportOutSeconds, 0f, 1f) : 1f;
            SetOpacity(_opacitySolid * (1f - k));
            if (k >= 1f) ArriveAt(_moveIsLick ? LickSpot() : RandomSpot());
        }
        else if (State == Phase.Arrive)
        {
            _fadeT += dt;
            float k = TeleportInSeconds > 0f ? Mathf.Clamp(_fadeT / TeleportInSeconds, 0f, 1f) : 1f;
            SetOpacity(_opacitySolid * k);
            Position = _dest + Vector3.Up * (TeleportInDrop * (1f - k) * (1f - k));
            FaceTick(dt);
            if (k >= 1f)
            {
                Position = _dest;
                if (_moveIsLick) BeginLick();
                else { EnterWait(NextGap() * 0.5f); if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: waits at the new spot, the next move in {_gapT:F1} s"); }
            }
        }
        else if (State == Phase.Lick) LickTick(dt);
        else if (State == Phase.Smash) SmashTick(dt);
        if (Blink) BlinkTick(dt);
        PlaceFx();
    }

    private void BlinkTick(float dt)
    {
        _blinkT += dt * 30f;                                     // the btp clock: 30 frames a second
        int f = (int)_blinkT % 150; int eye = 0;
        for (int i = 0; i < BlinkEnds.Length; i++) if (f < BlinkEnds[i]) { eye = BlinkEyes[i]; break; }
        ApplyEye(eye);
    }
    private void ApplyEye(int eye)
    {
        if (eye == _eyeNow || _bodyMat == null) return;
        Texture2D t = eye switch { 1 => EyeHalf, 2 => EyeClose, _ => EyeOpen } ?? EyeOpen;
        if (t == null) return;
        _eyeNow = eye;
        _bodyMat.SetShaderParameter("base_tex", t);
    }
    private void PlaceFx()
    {
        bool show = State != Phase.Hidden && State != Phase.Gone && _model != null && _model.Visible && _opacityShown > 0.02f;
        Vector3 at = BodyPoint;
        if (_chika != null) { bool on = show && Twinkle; _chika.Visible = on; _chika.Emitting = on; if (on) _chika.GlobalPosition = at; }
        if (_crownGlow != null && _crownGlow.Visible) { if (!show) _crownGlow.Visible = false; else _crownGlow.GlobalPosition = CrownPoint; }
        if (_glow != null)
        {
            _glow.Visible = show && Glow > 0f;
            if (_glow.Visible)
            {
                _glow.GlobalPosition = at;
                if (_glow is MeshInstance3D gm && gm.GetActiveMaterial(0) is ShaderMaterial gsm) gsm.SetShaderParameter("strength", Glow * _opacityShown);
            }
        }
    }
}
