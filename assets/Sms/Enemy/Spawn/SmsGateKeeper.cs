using System.Collections.Generic;
using Godot;

/// <summary>
/// The GATEKEEPER -- the Polluted Piranha, TBiancoGateKeeper (Enemy/gatekeeper.cpp; pass 126b): the goop plant that guards a gate ("ビアンコゲートキーパー",
/// /scene/gatekeeper/gene_pakkun_model1.bmd, 19 bcks, 2 btks, 1 bpk, 7 jpa, 7 bas) on /enemy/gatekeeper.prm (shipped: HitPointMax 3, InstanceNum 2,
/// DiveTimer 2000 -- the code default is 480 --, Loop2Dive 5, LaunchTimerNormal 600, LaunchTimerDamage 600; HeadHeight 120, BodyRadius 30, WallRadius 50,
/// ClipRadius 600, FarClip 13000). A TSpineEnemy whose ten nerves are a clip machine:
///   SLEEP (wait1, looping; the body vulnerable): the head sunk in the goop (-136 u). Spray the mound: every vulnerable hit holds a 16-tick window in
///          which a fog counter climbs 1 a tick (it decays 1 a tick outside it) -- at 255 (2.1 s of water) -> APPEAR; a hit also plays SLEEP DAMAGE
///          (damage2, 20 frames, back to SLEEP). In Bianco (<see cref="Variant.Bianco"/>) a launch timer (0 at birth: the first spit at once, then
///          LaunchTimerNormal +-120) -> LAUNCH GORO. A goop puff (kemuri_w) each time the clip passes frame 18.
///   APPEAR (appear1, 99 frames): the first time the appear demo camera + the boss BGM (here: signals + the BossMusic knob); at tick 8 the base's smoke
///          (kemuri at joint 12) and a rumble; ends -> WAIT (the airport kind: WAIT2).
///   WAIT (the Bianco / Plaza idle): wait2_start (49) -> wait2 (200, the first appearance at full HP) or wait3 (200, looping: the head sways 5.5 .. 7.7 m
///          up, "active" at frames 50 .. 160). The HEAD is the target now (TGKHitObj, joint 10 kutijnt1, a 150 x 200 u cylinder); a hit in wait3's
///          active window (or in damage-able clips) -> -1 HP -> AWAKE DAMAGE (damage1, 120 frames) or DIE; a hit outside it, or in wait2 -> IYAIYA
///          (iyaiya1, 60: it shakes the water off; sprayed on, it keeps shaking). After DiveTimer ticks, at the clip's end -> wait2_end (49) -> DIVE.
///   WAIT2 (the airport idle): wait2_start -> wait2_open (199; the head active past frame 40) -> wait2_openloop (200) x Loop2Dive -> wait2_close (199)
///          -> wait2_end -> DIVE; ANY vulnerable hit is damage.
///   DIVE (dive1, 149): it rears to 12 m and plunges; at tick 240 the base's smoke + rumble; ends -> (Bianco: a launch) -> SLEEP.
///   DIE (dead1, 299): the finish demo + BGM off, the stalk plunging under the floor then the mound melting away (gene_pakkun_dead1.bpk: _mat2's alpha
///          255 to frame 250, eased to 0 at 300 -- DeadMoundAlpha), the death bursts (dead at joint 6,
///          deadsmoke at 0); at tick 340 the smoke, the disappear sound, a rumble; a fading rumble all along; ends -> kill (the Ricco / Gelato kinds
///          revive ONCE with 3 HP: Ricco -> APPEAR, Gelato -> LAUNCH NAME).
///   LAUNCH GORO / LAUNCH NAME (shoot1, 59): at tick 52 the spit -- a Gorogoro (<see cref="LaunchScene"/>: the rolling goop head, NOT yet ported; any
///          SmsSmallEnemy kind lobbed toward Mario by generateByGateKeeper's rule) / ten Goobles in a ring (vel (4 sin, 12, 4 cos) u/tick, 36 deg
///          apart, from 100 u up); GORO ends -> SLEEP (or APPEAR at fog 255), NAME ends -> APPEAR.
/// Every tick: Mario inside the BODY cylinder (400 x 150 u at the base) takes HIT_MESSAGE_ATTACK; in the swaying clips the root joint turns toward
/// him at 3 deg a drawn frame (TBGKMtxCalc: here the Model node, 0.75 deg a tick, with the mound's joint pinned back to the actor frame by
/// SmsGateKeeperBasePin so only the stalk and head turn); the damage fog (SMS_AddDamageFogEffect: the material fog, colour
/// (200, 0, 80), 0.33 .. 0.83 pulsing at 30 ticks) while recently hit in the awake clips; in the Plaza kinds a goop STAMP (stamp_keeper_model1.bmd,
/// a 2340 u disc x 0.8 / 1.2 / 1.4) is re-stamped into the pollution layer every frame -- the goop under it cannot be cleaned while it lives.
/// The hit bodies are StaticBody3Ds on <see cref="SmsHiddenCoin.HideLayer"/> (the drops' ray reaches them; nothing collides -- the game's hit actors
/// block nothing either). NOT PORTED (logged): the demo cameras (signals), the hint balloons, TBGKObstacle (an 800 u hit box 1000 u under the actor;
/// no receiver), the particle effects (slots: pass 126c), the btk texture scroll and the bpk colour (steady scroll / a fade; 126c), the sounds (slots),
/// the Ricco / Gelato unlock flags (the kinds are placed by hand).
/// </summary>
[Tool]
[GlobalClass]
public partial class SmsGateKeeper : StaticBody3D, ISmsSprayTarget
{
    public enum Variant { Airport, Bianco, Plaza, PlazaRicco, PlazaGelato }      // VARIANT_AIRPORT 0, GENERIC 1 (Bianco), DOLPIC_TOWN 2, RICO 3, MAMMA 4
    public enum Nerve { Sleep, Appear, Wait, Wait2, SleepDamage, AwakeDamage, Die, Dive, LaunchGoro, LaunchName, Dead }
    /// <summary>The shaders' goop_colour_mode: 0 Texture, 1 Tint, 2 Recolour; FromMaterial leaves the material's own setting.</summary>
    public enum GoopColorModes { FromMaterial = -1, Texture = 0, Tint = 1, Recolour = 2 }
    // gatekeeper.prm (the shipped file)
    public const int PrmDiveTimer = 2000, PrmLoop2Dive = 5, PrmLaunchTimerNormal = 600, PrmLaunchTimerDamage = 600, PrmHitPointMax = 3, PrmInstanceNum = 2;
    public const float HeadHeight = 120f, BodyRadius = 30f, WallRadius = 50f, ClipRadius = 600f, FarClip = 13000f;
    // the hit cylinders (initHitActor): the body 400 x 150 at the actor, the head 150 x 200 at joint 10
    public const float BodyHitRadiusUnits = 400f, BodyHitHeightUnits = 150f, HeadHitRadiusUnits = 150f, HeadHitHeightUnits = 200f;
    public const int HeadJoint = 10;
    public const string HeadJointName = "kutijnt1";
    public const int FogHoldTicks = 16, FogMax = 255, DeathSmokeTick = 0x154, DiveSmokeTick = 0xF0, AppearSmokeTick = 8, LaunchTick = 0x34;
    public const float StampModelHalfUnits = 1170f;                                   // stamp_keeper_model1.bmd: x -1168 .. 1128, z -1173 .. 1002
    public const int NamekuriCount = 10;
    // the clips, in the archive's order (the game's indices). gene_pakkun_wait2_loop is "gene_pakkun_wait2cycle" in GateKeeper.glb: Godot's importer strips a
    // _loop / -loop / _cycle suffix as a loop marker and the stripped name collided with gene_pakkun_wait2 (unused by the code; the table keeps its slot).
    public static readonly string[] BckNames = { "gene_pakkun_appear1", "gene_pakkun_appear1_end", "gene_pakkun_appear1_start", "gene_pakkun_damage1", "gene_pakkun_damage2",
        "gene_pakkun_dead1", "gene_pakkun_dive1", "gene_pakkun_iyaiya1", "gene_pakkun_shoot1", "gene_pakkun_towait2", "gene_pakkun_wait1", "gene_pakkun_wait2",
        "gene_pakkun_wait2_close", "gene_pakkun_wait2_end", "gene_pakkun_wait2cycle", "gene_pakkun_wait2_open", "gene_pakkun_wait2_openloop", "gene_pakkun_wait2_start", "gene_pakkun_wait3" };
    public static readonly int[] BckFrames = { 99, 59, 99, 120, 20, 299, 149, 60, 59, 59, 120, 200, 199, 49, 200, 199, 200, 49, 200 };
    public static readonly bool[] BckLoops = { false, false, false, true, true, false, false, true, false, false, true, true, false, false, true, false, true, false, true };
    public const int BckAppear = 0, BckDamage1 = 3, BckDamage2 = 4, BckDead = 5, BckDive = 6, BckIyaiya = 7, BckShoot = 8, BckWait1 = 10, BckWait2 = 11,
        BckWait2Close = 12, BckWait2End = 13, BckWait2Loop = 14, BckWait2Open = 15, BckWait2OpenLoop = 16, BckWait2Start = 17, BckWait3 = 18;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    // The inspector shows them in these groups. The <summary> of each is its description (shown as a hover tooltip once the C# inspector-tooltips
    // editor plugin is installed; stock Godot shows C# doc comments nowhere -- the group names and the names themselves carry the gist).
    [ExportGroup("Kind and life")]
    /// <summary>Which gatekeeper this is -- it decides the idle, the spits and the stamp:
    /// Airport = the open-mouth loop that dives after a few cycles; any head hit hurts.
    /// Bianco = the sleeper of Bianco Hills that spits an enemy (LaunchScene) on a timer, from sleep and after each dive.
    /// Plaza = Delfino Plaza's: stamps goop under itself (0.8); dies once.
    /// PlazaRicco = the Ricco gate's: stamp 1.2, REVIVES ONCE after its first death.
    /// PlazaGelato = the Gelato gate's: stamp 1.4, REVIVES ONCE and spits a ring of ten Goobles (NamekuriScene) as it comes back.</summary>
    [Export] public Variant Kind = Variant.Bianco;
    /// <summary>Head hits to kill it (the game: 3). A revived one gets them back.</summary>
    [Export] public int HitPoints = PrmHitPointMax;
    /// <summary>Ticks awake before it dives back into the goop (120 ticks = 1 s; the game's shipped 2000 = 16.7 s).</summary>
    [Export] public int DiveTimerTicks = PrmDiveTimer;
    /// <summary>Airport kind only: open-mouth loops (3.3 s each) before it closes and dives (the game: 5).</summary>
    [Export] public int Loop2Dive = PrmLoop2Dive;
    /// <summary>Bianco kind only: ticks between spits while asleep (+-120 random; the game: 600 = 5 s). The first spit comes at once.</summary>
    [Export] public int LaunchTimerNormal = PrmLaunchTimerNormal;
    /// <summary>Bianco kind only: ticks between spits while it is being sprayed asleep (the game: 600).</summary>
    [Export] public int LaunchTimerDamage = PrmLaunchTimerDamage;

    [ExportGroup("Goop colour (this keeper)")]
    /// <summary>The goop sheet the whole plant wears -- the game uses the STAGE's pollution mark (empty = the material's own, Bianco's brown). Any of
    /// assets/Sms/Fx/Goop/*.png (Brown, Fire, Electric, Oil, Pink) fits.</summary>
    [Export] public Texture2D GoopTexture;
    /// <summary>How GoopColor is used: FromMaterial = ignore it, the material's own setting rules (the default); Texture = the sheet's own colours (the game);
    /// Tint = GoopColor multiplies the sheet (white = unchanged, darker colours darken it); Recolour = GoopColor REPLACES the sheet's colour and keeps only
    /// its light / dark pattern -- the way to get a blue or green piranha.</summary>
    [Export] public GoopColorModes GoopColorMode = GoopColorModes.FromMaterial;
    /// <summary>The colour for Tint / Recolour (see GoopColorMode).</summary>
    [Export] public Color GoopColor = new Color(1f, 1f, 1f, 1f);

    [ExportGroup("What it spits")]
    /// <summary>Bianco kind: the enemy scene it spits (the game's Gorogoro rolling head -- not yet ported; any enemy of the small-enemy base works, lobbed
    /// toward Mario). Empty = the spit animation plays with nothing coming out.</summary>
    [Export] public PackedScene LaunchScene;
    /// <summary>PlazaGelato kind: the Gooble scene for the ring of ten it spits as it revives. Empty = nothing comes out.</summary>
    [Export] public PackedScene NamekuriScene;
    /// <summary>How many of LaunchScene can be out at once (the game's manager pool: 10).</summary>
    [Export] public int LaunchPoolSize = 10;
    /// <summary>How many Goobles the ring has (the game: 10).</summary>
    [Export] public int NamekuriPoolSize = NamekuriCount;
    /// <summary>The walk graph the launched enemies follow; empty = they wander free.</summary>
    [Export] public Node3D Graph;
    /// <summary>The spit's lob: the rise in units a tick (the game: 15).</summary>
    [Export] public float LaunchJumpUnits = 15f;
    /// <summary>The spit aims at Mario when he is within this many units (2000 = 27 m), else straight ahead.</summary>
    [Export] public float LaunchSightUnits = 2000f;
    /// <summary>How far the spit may fly: a random 0 .. this many units along the aim (the game: 1500 = 20 m).</summary>
    [Export] public float LaunchSpreadUnits = 1500f;
    /// <summary>A launched enemy farther than this from Mario (metres) counts as out of view and may be spat again.</summary>
    [Export] public float RecycleDistance = 60f;

    [ExportGroup("Goop stamp under it")]
    /// <summary>Game rule = only the Plaza kinds stamp (0.8 / 1.2 / 1.4 of the 31 m stamp disc); On = always, at StampScale; Off = never. The stamped goop
    /// cannot be cleaned while the plant lives.</summary>
    [Export(PropertyHint.Enum, "Game rule,On,Off")] public int StampMode = 0;
    /// <summary>The stamp disc's scale when StampMode is On (1 = the 31 m disc).</summary>
    [Export] public float StampScale = 1f;
    /// <summary>Ticks between re-stamps (the game: every drawn frame = 4).</summary>
    [Export] public int StampEveryTicks = 4;

    [ExportGroup("Turning to Mario")]
    /// <summary>The stalk turns to face Mario while it sways (the game does).</summary>
    [Export] public bool TrackMario = true;
    /// <summary>How fast it turns, degrees a tick (the game: 3 a drawn frame = 0.75).</summary>
    [Export] public float TrackDegPerTick = 0.75f;
    /// <summary>Off (the game): the goop mound stays put, only the stalk and head turn. On: the whole plant turns, mound and all.</summary>
    [Export] public bool BaseTurnsToo = false;

    [ExportGroup("Hurt look and feel")]
    /// <summary>The pink pulse over its body after a hit (the game's damage fog).</summary>
    [Export] public bool DamageFog = true;
    /// <summary>The pad shakes when it slams and all through its death (stronger the closer Mario stands).</summary>
    [Export] public bool Rumble = true;

    [ExportGroup("Effects (empty = none yet)")]
    /// <summary>The base's smoke when it appears, dives and dies (ms_gkpa_kemuri).</summary>
    [Export] public PackedScene KemuriFxScene;
    /// <summary>The goop puff while it sleeps (ms_gkpa_kemuri_w).</summary>
    [Export] public PackedScene KemuriWFxScene;
    /// <summary>The goop burst as it dies (ms_gkpa_dead).</summary>
    [Export] public PackedScene DeadFxScene;
    /// <summary>The smoke as it dies (ms_gkpa_deadsmoke).</summary>
    [Export] public PackedScene DeadSmokeFxScene;
    /// <summary>The goop dripping from the head while awake (ms_gkpa_bota).</summary>
    [Export] public PackedScene BotaFxScene;
    /// <summary>The big drool from the mouth while awake (ms_gkpa_yodare_l, two emitters).</summary>
    [Export] public PackedScene YodareLFxScene;
    /// <summary>The small drool while awake (ms_gkpa_yodare_s).</summary>
    [Export] public PackedScene YodareSFxScene;

    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>Each water hit.</summary>
    [Export] public AudioStreamPlayer3D WaterHitSound;
    /// <summary>The goop vanishing near the end of its death.</summary>
    [Export] public AudioStreamPlayer3D DisappearSound;
    /// <summary>The boss music: starts when it first appears, stops when it dies for good.</summary>
    [Export] public AudioStreamPlayer BossMusic;

    [ExportGroup("Advanced")]
    /// <summary>Puts the hit bodies on the water-only layer at start (leave on).</summary>
    [Export] public bool AutoLayer = true;
    /// <summary>Prints every state change to the output.</summary>
    [Export] public bool DebugPrint;

    /// <summary>fireStartDemoCamera("<name>出現カメラ") at the first APPEAR / ("<name>撃沈カメラ") at DIE: the demo cameras are the stage's data; here a signal
    /// each, with the actor's position.</summary>
    [Signal] public delegate void AppearDemoEventHandler(Vector3 at);
    [Signal] public delegate void FinishDemoEventHandler(Vector3 at);
    [Signal] public delegate void DamagedEventHandler(int hitPointsLeft);
    [Signal] public delegate void DiedEventHandler();

    // ------------------------------------------------------------------------------------------------------------------ state
    private readonly SmsEnemyPool _launchPool = new(), _namePool = new();
    private Nerve _nerve = Nerve.Sleep, _next = Nerve.Sleep;
    private int _nerveTicks, _hp, _hits, _fogHold, _fog, _appearCount, _loopCount, _iyaiyaCount, _revived, _launchTimer, _stampTick;
    private float _yawTrack, _blendPerTick, _accum;
    private bool _dead, _filled, _bodyVulnerable, _headVulnerable, _stampOn, _endFired, _clipRestarted, _tickLaunched, _tickSmoked;
    private Node3D _model; private AnimationPlayer _anim; private Skeleton3D _skel; private int _bck = -1, _headBone = -1; private float _bckRate = 60f, _prevFrame;
    private Mario _mario;
    private SmsGateKeeperHead _head;
    private SmsGateKeeperBasePin _basePin;
    private readonly List<ShaderMaterial> _mats = new();
    private readonly Dictionary<string, BoneAttachment3D> _fxCache = new();
    private readonly List<SmsNpcEffect> _mouthFx = new();
    private bool _mouthFxOn;
    private readonly RandomNumberGenerator _rng = new();

    public Nerve NerveNow => _nerve;
    public int NerveTicks => _nerveTicks;
    public int Hp => _hp;
    public int Fog => _fog;
    public int HitsThisTick => _hits;
    public bool IsDead => _dead;
    public bool BodyVulnerable => _bodyVulnerable;
    public bool HeadVulnerable => _headVulnerable;
    public bool StampOn => _stampOn;
    public float YawTrackDeg => _yawTrack;
    public int AppearCount => _appearCount;
    public int LoopCount => _loopCount;
    public int Launched { get; private set; }
    public int Stamps { get; private set; }
    public int Puffs { get; private set; }
    public int TicksSeen { get; private set; }
    public SmsEnemyPool LaunchPool => _launchPool;
    public SmsEnemyPool NamekuriPool => _namePool;
    public SmsGateKeeperHead Head => _head;
    public SmsGateKeeperBasePin BasePin => _basePin;
    public string BckName => _bck >= 0 ? BckNames[_bck] : "";
    public int Bck => _bck;
    public float Scale3 => GlobalTransform.Basis.Scale.Y;
    public float StampScaleNow => StampMode == 1 ? StampScale : Kind == Variant.Plaza ? 0.8f : Kind == Variant.PlazaRicco ? 1.2f : Kind == Variant.PlazaGelato ? 1.4f : 1f;
    public string DebugState => $"{_nerve} t{_nerveTicks} bck {BckName} f{BckFrame():F0} hp {_hp} fog {_fog} hold {_fogHold} hits {_hits} vuln body {_bodyVulnerable} head {_headVulnerable} headActive {HeadHitActive()} yaw {_yawTrack:F1} appear {_appearCount} dead {_dead}";

    public override void _Ready()
    {
        _rng.Randomize();
        _model = GetNodeOrNull<Node3D>("Model");
        _anim = GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_model == null || _anim == null) GD.PushWarning($"[SMS gatekeeper] {GetPath()}: the scene needs Model (the glb) with Model/AnimationPlayer");
        if (_model != null) foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        if (_skel != null) { _headBone = _skel.FindBone(HeadJointName); if (_headBone < 0) _headBone = Mathf.Min(HeadJoint, _skel.GetBoneCount() - 1); }
        // the look: this body's own copies of the two materials (the fog, the death and the goop colour are per body), the goop knobs applied. In the EDITOR too:
        // the copies live on the instanced glb's mesh, which the scene does not save (not an editable instance), so the preview costs nothing in the file.
        if (_model != null) foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            if (n is MeshInstance3D mi) for (int s = 0; s < mi.GetSurfaceOverrideMaterialCount(); s++)
                if (mi.GetActiveMaterial(s) is ShaderMaterial sm) { var own = (ShaderMaterial)sm.Duplicate(); mi.SetSurfaceOverrideMaterial(s, own); _mats.Add(own); }
        ApplyLook();
        if (Engine.IsEditorHint()) return;
        if (_skel != null)
        {
            _basePin = _skel.GetNodeOrNull<SmsGateKeeperBasePin>("BasePin");
            if (_basePin == null) { _basePin = new SmsGateKeeperBasePin { Name = "BasePin" }; _skel.AddChild(_basePin); }
        }
        if (AutoLayer) { CollisionLayer = SmsHiddenCoin.HideLayer; CollisionMask = 0; }
        _head = GetNodeOrNull<SmsGateKeeperHead>("HeadHit");
        if (_head == null)
        {
            _head = new SmsGateKeeperHead { Name = "HeadHit" };
            AddChild(_head);
            var shape = new CollisionShape3D { Name = "Shape", Shape = new CylinderShape3D { Radius = HeadHitRadiusUnits / SmsUnits.UnitsPerMetre, Height = HeadHitHeightUnits / SmsUnits.UnitsPerMetre } };
            shape.Position = new Vector3(0f, 0.5f * HeadHitHeightUnits / SmsUnits.UnitsPerMetre, 0f);
            _head.AddChild(shape);
        }
        _head.Owner3 = this;
        _head.TopLevel = true;
        if (AutoLayer) { _head.CollisionLayer = SmsHiddenCoin.HideLayer; _head.CollisionMask = 0; }
        _hp = HitPoints;
        _stampOn = StampMode == 1 || (StampMode == 0 && (Kind == Variant.Plaza || Kind == Variant.PlazaRicco || Kind == Variant.PlazaGelato));
        SmsWaterHitMarks.Ensure(this);
        CallDeferred(MethodName.FillPools);
        _nerve = Nerve.Sleep; _next = Nerve.Sleep; _nerveTicks = 0;
        SetBck(BckWait1);                                                             // the spine's init nerve: SLEEP, its clip at once (a drawn frame before the first tick)
        PlaceHead();
    }
    /// <summary>In the editor only: the goop knobs re-applied when they change, so the viewport shows the colour being picked.</summary>
    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) return;
        string sig = $"{GoopTexture?.ResourcePath}|{GoopColorMode}|{GoopColor}";
        if (sig == _lookSig) return;
        ApplyLook();
    }
    private string _lookSig = "";
    /// <summary>The goop knobs onto this body's materials: GoopTexture replaces the sheet when set; GoopColorMode / GoopColor are pushed unless FromMaterial.</summary>
    public void ApplyLook()
    {
        _lookSig = $"{GoopTexture?.ResourcePath}|{GoopColorMode}|{GoopColor}";
        foreach (var m in _mats)
        {
            if (GoopTexture != null) m.SetShaderParameter("goop_texture", GoopTexture);
            if (GoopColorMode != GoopColorModes.FromMaterial)
            {
                m.SetShaderParameter("goop_colour_mode", (int)GoopColorMode);
                m.SetShaderParameter("goop_colour", GoopColor);
            }
        }
    }
    private void FillPools()
    {
        if (_filled) return;
        _filled = true;
        if (LaunchScene != null && Kind == Variant.Bianco) _launchPool.Fill(GetParent(), LaunchScene, LaunchPoolSize, GlobalPosition, DebugPrint, Name);          // the Gorogoro manager is Bianco's
        if (NamekuriScene != null && Kind == Variant.PlazaGelato) _namePool.Fill(GetParent(), NamekuriScene, NamekuriPoolSize, GlobalPosition, DebugPrint, Name); // the Gooble ring is the Gelato gate's
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) return;
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        int n = SmsUnits.TicksFor(delta, ref _accum);
        for (int i = 0; i < n; i++) { TicksSeen++; Tick(); }
        PlaceHead();
        UpdateFog();
    }

    // ------------------------------------------------------------------------------------------------------------------ the tick
    /// <summary>TBiancoGateKeeper::perform (CUE_MOVE) + TGateKeeperBase::perform + TSpineEnemy's spine step.</summary>
    private void Tick()
    {
        if (_dead) return;
        TouchMario();
        if (_hits > 0) _fogHold = FogHoldTicks;                                       // unk154 > 0 -> unk17E = 16
        if (_fogHold > 0) { _fog = Mathf.Min(FogMax, _fog + 1); _fogHold--; }         // unk17C climbs while the window holds, decays outside it
        else _fog = Mathf.Max(0, _fog - 1);
        ControlCollision();
        int bck0 = _bck;
        float f = BckFrame();
        bool fin = BckFinishedOnce(f);
        bool endsNext = fin;                                                          // curAnmEndsNext: the same frame for the port (frame + rate + 0.1 >= end)
        switch (_nerve)
        {
            case Nerve.Sleep: NerveSleep(f); break;
            case Nerve.Appear: NerveAppear(endsNext); break;
            case Nerve.Wait: NerveWait(fin); break;
            case Nerve.Wait2: NerveWait2(fin); break;
            case Nerve.SleepDamage: NerveSleepDamage(endsNext); break;
            case Nerve.AwakeDamage: NerveAwakeDamage(fin); break;
            case Nerve.Die: NerveDie(f, endsNext); break;
            case Nerve.Dive: NerveDive(endsNext); break;
            case Nerve.LaunchGoro: NerveLaunchGoro(endsNext); break;
            case Nerve.LaunchName: NerveLaunchName(endsNext); break;
        }
        _prevFrame = _bck == bck0 && !_clipRestarted ? f : 0f;                        // a clip change or restart this tick: the new clip starts at 0 (no wrap read across clips)
        _clipRestarted = false;
        _hits = 0;                                                                    // TGateKeeperBase::perform: unk154 = 0 each move
        if (_nerve != _next) { if (DebugPrint) GD.Print($"[SMS gatekeeper] {Name}: {_nerve} -> {_next} after {_nerveTicks} ticks ({DebugState})"); _nerve = _next; _nerveTicks = 0; _tickLaunched = false; _tickSmoked = false; }
        else _nerveTicks++;
        if (_dead) return;
        TrackYaw();
        MouthFx();
        if (_stampOn && ++_stampTick >= Mathf.Max(1, StampEveryTicks)) { _stampTick = 0; StampGoop(); }
    }
    private void Push(Nerve n) => _next = n;

    private void NerveSleep(float f)
    {
        if (_nerveTicks == 0) { ChangeBck(BckWait1); CollisionOn(); }
        if (Kind == Variant.Bianco)                                                   // SMSGetMarDirector()->getCurrentStage() == 2
        {
            if (_launchTimer > 0) _launchTimer--;
            if (_launchTimer == 0) { _launchTimer = LaunchTimerNormal + (int)(240f * _rng.Randf()) - 120; Push(Nerve.LaunchGoro); return; }
        }
        if (BckPassed(18f, f)) { Puffs++; SpawnJointBurst(KemuriWFxScene, 0, 2f); }   // checkBckPass(18): MS_GKPA_KEMURI_W at joint 0, x2
        if (_fog >= FogMax) { Push(Nerve.Appear); return; }
        if (_hits > 0) { Push(Nerve.SleepDamage); return; }
    }
    private void NerveAppear(bool endsNext)
    {
        if (_nerveTicks == 0)
        {
            ChangeBck(BckAppear);
            if (_appearCount == 0) StartAppearDemo();
            if (_appearCount < 255) _appearCount++;
        }
        if (_nerveTicks == AppearSmokeTick) { SpawnJointBurst(KemuriFxScene, 12, 2f); RumblePad(); }
        if (endsNext) { Push(Kind == Variant.Airport ? Nerve.Wait2 : Nerve.Wait); }
    }
    private void NerveWait(bool fin)
    {
        if (_nerveTicks == 0) ChangeBck(BckWait2Start);
        if (_hits > 0 && _bck == BckWait3 && !HeadHitActive()) { ChangeBck(BckIyaiya); return; }
        if (_hits > 0 && _bck != BckWait2 && _bck != BckIyaiya && _bck != BckWait2Start && _bck != BckWait2End) { TakeHit(); return; }
        if (_hits > 0 && _bck == BckWait2) { ChangeBck(BckIyaiya); return; }
        if (_hits > 0 && _bck == BckIyaiya) _iyaiyaCount = 0;
        if (_nerveTicks > DiveTimerTicks && _bck != BckWait2End && fin) { ChangeBck(BckWait2End); fin = false; }
        if (!fin) return;
        if (_bck == BckWait2Start && _appearCount == 1 && _hp == 3) ChangeBck(BckWait2);
        else if (_bck == BckWait2Start) ChangeBck(BckWait3);
        else if (_bck == BckWait2) ChangeBck(BckWait3);
        else if (_bck == BckWait3) ChangeBck(BckWait3);
        else if (_bck == BckIyaiya)
        {
            if (_hits > 0) { _iyaiyaCount = 0; ChangeBck(BckIyaiya); }
            else
            {
                _fog = 0;
                if (_iyaiyaCount >= 0) { _iyaiyaCount = 0; ChangeBck(BckWait3); }
                else { _iyaiyaCount++; ChangeBck(BckIyaiya); }
            }
        }
        else if (_bck == BckWait2End) Push(Nerve.Dive);
    }
    private void NerveWait2(bool fin)
    {
        if (_nerveTicks == 0) ChangeBck(BckWait2Start);
        if (_hits > 0) { TakeHit(); return; }
        if (!fin) return;
        if (_bck == BckWait2Start) ChangeBck(BckWait2Open);
        else if (_bck == BckWait2Open) ChangeBck(BckWait2OpenLoop);
        else if (_bck == BckWait2OpenLoop)
        {
            _loopCount++;
            if (_loopCount > Loop2Dive) { ChangeBck(BckWait2Close); _loopCount = 0; }
            else ChangeBck(BckWait2OpenLoop);
        }
        else if (_bck == BckWait2Close) ChangeBck(BckWait2End);
        else if (_bck == BckWait2End) Push(Nerve.Dive);
    }
    private void NerveSleepDamage(bool endsNext)
    {
        if (_nerveTicks == 0) ChangeBck(BckDamage2);
        if (Kind == Variant.Bianco)
        {
            if (_launchTimer > 0) _launchTimer--;
            if (_launchTimer == 0) { _launchTimer = LaunchTimerDamage + (int)(240f * _rng.Randf()) - 120; LaunchGorogoro(); RumblePad(); }
        }
        if (endsNext) Push(Nerve.Sleep);
    }
    private void NerveAwakeDamage(bool fin)
    {
        if (_nerveTicks == 0) ChangeBck(BckDamage1);
        if (fin) Push(Kind == Variant.Airport ? Nerve.Wait2 : Nerve.Wait);
    }
    private void NerveDie(float f, bool endsNext)
    {
        bool revivable = (Kind == Variant.PlazaRicco || Kind == Variant.PlazaGelato) && _revived == 0;
        if (_nerveTicks == 0)
        {
            ChangeBck(BckDead);
            if (!revivable)
            {
                StartFinishDemo();
                SpawnJointBurst(DeadFxScene, 6, 1f);                                   // MS_GKPA_DEAD at joint 6 (the pollution colour)
                SpawnJointBurst(DeadSmokeFxScene, 0, 1f);                              // MS_GKPA_DEADSMOKE at joint 0
            }
        }
        if (!revivable) SetMoundAlpha(DeadMoundAlpha(f));                               // gene_pakkun_dead1.bpk: the mound's alpha, 255 to frame 250 then eased to 0 at 300
        if (_nerveTicks == DeathSmokeTick)
        {
            SpawnJointBurst(KemuriFxScene, 0, 2f);
            if (DisappearSound != null && DisappearSound.Stream != null) DisappearSound.Play();
            RumblePad();
        }
        DeathRumble(f);
        if (!endsNext) return;
        if (revivable)
        {
            _revived = 1; _hp = HitPoints;
            Push(Kind == Variant.PlazaGelato ? Nerve.LaunchName : Nerve.Appear);
            return;
        }
        _stampOn = false;
        Kill();
    }
    private void NerveDive(bool endsNext)
    {
        if (_nerveTicks == 0) ChangeBck(BckDive);
        if (_nerveTicks == DiveSmokeTick) { SpawnJointBurst(KemuriFxScene, 0, 2f); RumblePad(); }
        if (!endsNext) return;
        if (Kind == Variant.Bianco) { _launchTimer = LaunchTimerNormal + (int)(240f * _rng.Randf()) - 120; LaunchGorogoro(); RumblePad(); }
        Push(Nerve.Sleep);
    }
    private void NerveLaunchGoro(bool endsNext)
    {
        if (_nerveTicks == 0) ChangeBck(BckShoot);
        if (_nerveTicks == LaunchTick) { LaunchGorogoro(); RumblePad(); }
        if (_fog >= FogMax) { Push(Nerve.Appear); return; }
        if (endsNext) Push(Nerve.Sleep);
    }
    private void NerveLaunchName(bool endsNext)
    {
        if (_nerveTicks == 0) ChangeBck(BckShoot);
        if (_nerveTicks == LaunchTick) { LaunchNamekuri(); RumblePad(); }
        if (endsNext) Push(Nerve.Appear);
    }
    /// <summary>decHitPoints -> DIE at 0, else AWAKE DAMAGE.</summary>
    private void TakeHit()
    {
        _hp = Mathf.Max(0, _hp - 1);
        EmitSignal(SignalName.Damaged, _hp);
        if (DebugPrint) GD.Print($"[SMS gatekeeper] {Name}: hit, hp {_hp}");
        Push(_hp == 0 ? Nerve.Die : Nerve.AwakeDamage);
    }

    // ------------------------------------------------------------------------------------------------------------------ the hits
    /// <summary>controlCollision: which hit object counts a water hit, by the current clip (and wait2_open's frame).</summary>
    private void ControlCollision()
    {
        bool head = false, body = false;
        if (_bck == BckWait2 || _bck == BckIyaiya || _bck == BckWait3) head = true;
        else if (_bck == BckWait1 || _bck == BckDamage2) body = true;
        else if (_bck == BckWait2Open) head = BckFrame() > 40f;
        else if (_bck == BckWait2OpenLoop || _bck == BckWait2Close) head = true;
        _headVulnerable = head; _bodyVulnerable = body;
    }
    /// <summary>isHeadHitActive: wait3 frames 50 .. 160, wait2_open past 40, wait2_openloop / wait2_close always.</summary>
    public bool HeadHitActive()
    {
        float f = BckFrame();
        if (_bck == BckWait3) return f > 50f && f < 160f;
        if (_bck == BckWait2Open) return f > 40f;
        return _bck == BckWait2OpenLoop || _bck == BckWait2Close;
    }
    /// <summary>TGateKeeperBase::receiveMessage from a water actor (the BODY cylinder): the WATHIT mark and the sound always, a hit counted while the body is
    /// vulnerable (asleep); true (the drop dies). Dead: refused.</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (WaterHitSound != null && WaterHitSound.Stream != null) WaterHitSound.Play();
        if (_bodyVulnerable) _hits++;
        return true;
    }
    /// <summary>TGKHitObj::receiveMessage (the HEAD cylinder at joint 10): the mark and the sound, a hit counted while the head is vulnerable; true.</summary>
    public bool HeadSprayedByWater(Vector3 hitPos)
    {
        if (_dead) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (WaterHitSound != null && WaterHitSound.Stream != null) WaterHitSound.Play();
        if (_headVulnerable) _hits++;
        return true;
    }
    /// <summary>TGKHitObj::perform (CUE_CALC_ANIM): mPosition = joint 10's animated translation; the cylinder stands up from it.</summary>
    private void PlaceHead()
    {
        if (_head == null) return;
        if (_skel != null && _headBone >= 0) _head.GlobalPosition = (_skel.GlobalTransform * _skel.GetBoneGlobalPose(_headBone)).Origin;
        else _head.GlobalPosition = GlobalPosition;
        _head.GlobalBasis = Basis.Identity;
    }
    public Vector3 HeadPosition => _head != null ? _head.GlobalPosition : GlobalPosition;
    private void CollisionOn() { if (!_dead && AutoLayer) { CollisionLayer = SmsHiddenCoin.HideLayer; if (_head != null) _head.CollisionLayer = SmsHiddenCoin.HideLayer; } }
    /// <summary>perform: every collision with Mario -> SMS_SendMessageToMario(ATTACK) -- Mario in the body cylinder (400 x 150 u at the body's scale).</summary>
    private void TouchMario()
    {
        if (_mario == null || !IsInstanceValid(_mario)) return;
        float u = SmsUnits.UnitsPerMetre, s = Scale3;
        Vector3 d = _mario.GlobalPosition - GlobalPosition;
        if (new Vector2(d.X, d.Z).Length() * u > BodyHitRadiusUnits * s) return;
        if (d.Y * u < -BodyHitHeightUnits * s * 0.25f || d.Y * u > BodyHitHeightUnits * s) return;
        _mario.TakeDamage(GlobalPosition, Mario.SmsDamageProfile.EnemyCommon);
    }
    /// <summary>kill(): LIVE_FLAG_DEAD + HIT_FILTER_NO_COLLISION on the body, the head and the obstacle; hidden, the clips stopped, the stamp off.</summary>
    public void Kill()
    {
        _dead = true; _stampOn = false;
        _nerve = Nerve.Dead; _next = Nerve.Dead;
        if (_model != null) _model.Visible = false;
        CollisionLayer = 0;
        if (_head != null) _head.CollisionLayer = 0;
        if (_anim != null) _anim.Stop();
        StopMouthFx();
        EmitSignal(SignalName.Died);
    }

    // ------------------------------------------------------------------------------------------------------------------ the launches
    private Vector3 MarioPos => _mario != null && IsInstanceValid(_mario) ? _mario.GlobalPosition : GlobalPosition;
    /// <summary>launchGorogoro: gpConductor->makeOneEnemyAppear(mPosition, the Gorogoro manager) -> generateByGateKeeper(mPosition, mRotation): the body reset at
    /// the point, lobbed (calcVelocityToJumpToY, vy 15 u/tick) toward Mario if MsIsInSight(2000 u, 360 deg) else toward the graph's nearest node (here a point
    /// ahead), a random 0 .. 1500 u of the way, the heading jittered +-15 deg; the Gorogoro then rolls on its graph (its own class: not yet ported -- the slot
    /// takes any SmsSmallEnemy kind).</summary>
    private void LaunchGorogoro()
    {
        var e = _launchPool.FarOut(MarioPos, RecycleDistance);
        if (e == null) { if (DebugPrint) GD.Print($"[SMS gatekeeper] {Name}: the spit, nothing to launch ({(LaunchScene == null ? "no LaunchScene" : "the pool is out")})"); return; }
        float u = SmsUnits.UnitsPerMetre;
        Vector3 pos = GlobalPosition;
        Vector3 toMario = MarioPos - pos;
        Vector3 dir;
        if (new Vector2(toMario.X, toMario.Z).Length() * u <= LaunchSightUnits) dir = toMario;
        else dir = GlobalTransform.Basis.Z * (LaunchSpreadUnits / u);                 // toward the "graph": the node's own forward (the game: the nearest graph node)
        dir.Y = 0f;
        Vector3 target = pos;
        if (dir.Length() > 1e-4f)
        {
            dir = dir.Normalized().Rotated(Vector3.Up, Mathf.DegToRad(_rng.Randf() * 30f - 15f));
            target = pos + dir * (LaunchSpreadUnits * _rng.Randf() / u);
        }
        Vector3 vel = VelocityToJumpTo((target - pos) * u, LaunchJumpUnits, e.GravityUnits);
        e.SmsLaunch(pos, YawOf(dir), vel, Graph);
        Launched++;
        if (DebugPrint) GD.Print($"[SMS gatekeeper] {Name}: launched {e.Name} toward {target} vel {vel} u/tick");
    }
    /// <summary>launchNamekuri: ten of the Gooble manager's far-out bodies from 100 u up, 36 deg apart, vel (4 sin, 12, 4 cos) u/tick, airborne (TNerveNameKuriDiffuse:
    /// they fall and land, then live).</summary>
    private void LaunchNamekuri()
    {
        float u = SmsUnits.UnitsPerMetre;
        for (int i = 0; i < NamekuriCount; i++)
        {
            var e = _namePool.FarOut(MarioPos, RecycleDistance);
            if (e == null) break;
            float a = Mathf.DegToRad(36f * i);
            Vector3 vel = new Vector3(4f * Mathf.Sin(a), 12f, 4f * Mathf.Cos(a));
            e.SmsLaunch(GlobalPosition + Vector3.Up * (100f / u), Mathf.RadToDeg(Rotation.Y), vel, Graph, flyFirst: true);   // pushNerve(TNerveNameKuriDiffuse)
            Launched++;
        }
        if (DebugPrint) GD.Print($"[SMS gatekeeper] {Name}: the Gooble ring, {_namePool.Alive} out");
    }
    /// <summary>calcVelocityToJumpToY(dest, vy, g): the lob with the given rise that lands on dest (game units a tick).</summary>
    public static Vector3 VelocityToJumpTo(Vector3 deltaUnits, float vy, float g)
    {
        float disc = vy * vy - 2f * g * deltaUnits.Y;
        float t = disc > 0f && g > 0f ? (vy + Mathf.Sqrt(disc)) / g : (g > 0f ? 2f * vy / g : 1f);
        if (t < 1f) t = 1f;
        return new Vector3(deltaUnits.X / t, vy, deltaUnits.Z / t);
    }
    private static float YawOf(Vector3 v) => Mathf.RadToDeg(Mathf.Atan2(v.X, v.Z));

    // ------------------------------------------------------------------------------------------------------------------ the look
    /// <summary>TBGKMtxCalc::calc(0): in wait2 / wait3 / wait2_open / wait2_openloop / wait2_close (not in a demo or a talk) the root joint turns toward Mario by
    /// up to 3 deg a drawn frame (unk180, about the actor's yaw) -- here the Model node, 0.75 a tick; calc(11) pins centnull1 (the mound's rings) to the actor
    /// frame -- here <see cref="SmsGateKeeperBasePin"/> turning the base back by the same yaw, so only the stalk and head swing round.</summary>
    private void TrackYaw()
    {
        if (_model == null) return;
        bool tracking = TrackMario && (_bck == BckWait2 || _bck == BckWait3 || _bck == BckWait2Open || _bck == BckWait2OpenLoop || _bck == BckWait2Close) && _mario != null && IsInstanceValid(_mario);
        if (tracking)
        {
            Vector3 d = ToLocal(_mario.GlobalPosition);
            float target = YawOf(new Vector3(d.X, 0f, d.Z));
            float delta = Mathf.Wrap(target - _yawTrack, -180f, 180f);
            _yawTrack = Mathf.Wrap(_yawTrack + Mathf.Clamp(delta, -TrackDegPerTick, TrackDegPerTick), 0f, 360f);
        }
        _model.Rotation = new Vector3(0f, Mathf.DegToRad(_yawTrack), 0f);
        if (_basePin != null) { _basePin.Active = !BaseTurnsToo; _basePin.YawDeg = _yawTrack; }
    }
    /// <summary>isDamageFogSituation + SMS_AddDamageFogEffect: the fog on the body's materials while recently hit (unk17C > 0) in the awake clips (not in wait2 /
    /// wait2_start / wait2_end / iyaiya / dive, not outside wait3's active window), and all through AWAKE DAMAGE's first 110 ticks; its factor at the body
    /// 7/12 - sin(tick x 0x888) / 4, a 30-tick pulse.</summary>
    private void UpdateFog()
    {
        if (_mats.Count == 0) return;
        float amount = 0f;
        if (DamageFog && !_dead && FogSituation())
        {
            float s = Mathf.Sin((((long)TicksSeen * 0x888) & 0xFFFF) / 65536f * Mathf.Tau);   // JMASSin((s16)(moveTick x 0x888)): a 30-tick period
            amount = 7f / 12f - s / 4f;
        }
        foreach (var m in _mats) m.SetShaderParameter("damage_fog_amount", amount);
    }
    private bool FogSituation()
    {
        if (_nerve == Nerve.AwakeDamage && _nerveTicks <= 110) return true;
        if (_bck == BckWait3 && !(BckFrame() > 50f && BckFrame() < 160f)) return false;
        if (_bck == BckWait2 || _bck == BckWait2Start || _bck == BckWait2End || _bck == BckIyaiya || _bck == BckDive) return false;
        return _fog > 0;
    }
    private void SetMoundAlpha(float a) { foreach (var m in _mats) m.SetShaderParameter("mound_alpha", a); }
    /// <summary>gene_pakkun_dead1.bpk (bpk_dump.py): _mat2's material colour register -- R, G, B constant 255; A keyed 255 at frame 0, 255 at frame 250 and 0 at
    /// frame 300, in/out tangents -2 a frame at the last two keys: J3D's Hermite between them (the tangents scaled by the 50-frame span). The stalk's _mat1 is not
    /// animated at all -- it has plunged under the floor by frame 180 of the dead clip (gk_fk.py: the head at -3.8 m), so what the player sees is the stalk
    /// diving into the mound, then the mound melting away in the last 0.8 s. Returns 0 .. 1.</summary>
    public static float DeadMoundAlpha(float frame)
    {
        if (frame <= 250f) return 1f;
        if (frame >= 300f) return 0f;
        const float h = 50f, v0 = 255f, v1 = 0f, m0 = -2f, m1 = -2f;
        float s = (frame - 250f) / h, s2 = s * s, s3 = s2 * s;
        float v = (2f * s3 - 3f * s2 + 1f) * v0 + (s3 - 2f * s2 + s) * h * m0 + (-2f * s3 + 3f * s2) * v1 + (s3 - s2) * h * m1;
        return Mathf.Clamp(v / 255f, 0f, 1f);
    }
    /// <summary>TGateKeeperBase::perform (CUE_ENTRY): gpPollution->stampModel(the stamp model at the root matrix x mStampScale) -- the goop disc under it re-laid
    /// every frame; here the layer's Stamp (pollute) with the model's 2340 u x the scale.</summary>
    private void StampGoop()
    {
        float size = 2f * StampModelHalfUnits * StampScaleNow * Scale3;
        float sizeM = size / SmsUnits.UnitsPerMetre;
        foreach (var layer in SmsPollutionMap.Layers)
        {
            if (!IsInstanceValid(layer) || !layer.Built || !layer.IsInAreaSize(GlobalPosition, sizeM)) continue;
            layer.Stamp(1, GlobalPosition, size);
            Stamps++;
        }
    }
    /// <summary>emitParticles (CUE_CALC_ANIM, not clipped): bota on joint 6, yodare_l on 9 and 5, yodare_s on 8, every frame while not SLEEP / LAUNCH GORO -- one
    /// emitter each, bound to the joint (emitAndBindToMtxPtr with a fixed id).</summary>
    private void MouthFx()
    {
        bool on = _nerve != Nerve.Sleep && _nerve != Nerve.LaunchGoro && _nerve != Nerve.Dead;
        if (on == _mouthFxOn) return;
        _mouthFxOn = on;
        if (!on) { StopMouthFx(); return; }
        if (_skel == null) return;
        foreach (var (scene, joint) in new[] { (BotaFxScene, 6), (YodareLFxScene, 9), (YodareLFxScene, 5), (YodareSFxScene, 8) })
        {
            if (scene == null || joint >= _skel.GetBoneCount()) continue;
            var fx = SmsNpcEffect.Spawn(scene, _skel, _skel.GetBoneName(joint), Scale3, _fxCache, Name + joint);
            if (fx != null) _mouthFx.Add(fx);
        }
    }
    private void StopMouthFx() { foreach (var fx in _mouthFx) if (IsInstanceValid(fx)) fx.Stop(); _mouthFx.Clear(); _mouthFxOn = false; }
    /// <summary>A one-shot at a joint's animated position (emitAndBindToMtxPtr(id, getAnmMtx(joint), ...) for a burst), at the body's scale x the emitter's.</summary>
    private void SpawnJointBurst(PackedScene scene, int joint, float fxScale)
    {
        if (scene == null) return;
        Vector3 at = GlobalPosition;
        if (_skel != null && joint < _skel.GetBoneCount()) at = (_skel.GlobalTransform * _skel.GetBoneGlobalPose(joint)).Origin;
        var fx = scene.Instantiate<Node3D>();
        fx.TopLevel = true;
        GetParent().AddChild(fx);
        Transform3D tr = new Transform3D(Basis.FromScale(Vector3.One * Scale3 * fxScale), at);
        fx.GlobalTransform = tr;
        if (fx is SmsFxBurst burst) burst.Play(tr);
        else if (fx is SmsNpcEffect npc) npc.Start(Scale3 * fxScale);
        else if (fx is GpuParticles3D gp) { gp.OneShot = true; gp.Emitting = true; gp.Finished += gp.QueueFree; }
    }
    private float RumblePower()
    {
        float dist = (GlobalPosition - MarioPos).Length() * SmsUnits.UnitsPerMetre;
        return dist <= 0f ? 1f : Mathf.Min(1f, 2000f / dist);
    }
    /// <summary>rumblePad: SMSRumbleMgr->start(8, power) when Mario stands (SMS_IsMarioTouchGround4cm).</summary>
    private void RumblePad()
    {
        if (!Rumble || _mario == null || !IsInstanceValid(_mario) || !_mario.IsOnFloor()) return;
        float p = RumblePower();
        Input.StartJoyVibration(0, 0.6f * p, p, 0.35f);
    }
    /// <summary>deathRumble: power x (1 - frame / end) every tick of DIE.</summary>
    private void DeathRumble(float f)
    {
        if (!Rumble || _mario == null || !IsInstanceValid(_mario) || !_mario.IsOnFloor()) return;
        float p = (1f - Mathf.Clamp(f / BckFrames[BckDead], 0f, 1f)) * RumblePower();
        if (p > 0.02f) Input.StartJoyVibration(0, 0.5f * p, p, 0.05f);
    }
    private void StartAppearDemo()
    {
        EmitSignal(SignalName.AppearDemo, GlobalPosition);
        if (BossMusic != null && BossMusic.Stream != null && !BossMusic.Playing) BossMusic.Play();
    }
    private void StartFinishDemo()
    {
        EmitSignal(SignalName.FinishDemo, GlobalPosition);
        if (BossMusic != null && BossMusic.Playing) BossMusic.Stop();
    }

    // ------------------------------------------------------------------------------------------------------------------ clips
    /// <summary>changeBck: the hard cuts (setAnm: wait2_start -> wait2 / wait3, wait2 <-> wait3, iyaiya -> iyaiya / wait3, wait2_open -> wait2_openloop ->
    /// wait2_close) restart at once; every other change is joinAnm with the blend ratio ramping 1 / (0.25 x end) a frame = over a quarter of the new clip.</summary>
    private void ChangeBck(int i)
    {
        int cur = _bck;
        bool hard = (cur == BckWait2Start && (i == BckWait2 || i == BckWait3)) || (cur == BckWait2 && i == BckWait3) || (cur == BckWait3 && i == BckWait2)
            || (cur == BckIyaiya && (i == BckIyaiya || i == BckWait3)) || (cur == BckWait2Open && i == BckWait2OpenLoop) || (cur == BckWait2OpenLoop && i == BckWait2Close);
        float blend = hard ? 0f : 0.25f * BckFrames[i] / 60f;
        _blendPerTick = hard ? 0f : 1f / Mathf.Max(1f, 0.25f * BckFrames[i]);
        SetBck(i, blend);
    }
    private void SetBck(int i, float blendSec = 0f)
    {
        bool same = _bck == i;
        _bck = i;
        if (_anim == null || !_anim.HasAnimation(BckNames[i])) { _endFired = false; _prevFrame = 0f; _clipRestarted = true; return; }
        var clip = _anim.GetAnimation(BckNames[i]);
        clip.LoopMode = BckLoops[i] ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        _bckRate = clip.Length > 0f ? BckFrames[i] / (float)clip.Length : 60f;
        if (same && _anim.IsPlaying() && _anim.CurrentAnimation == BckNames[i])
        {
            if (!BckLoops[i] || blendSec <= 0f) { _anim.Seek(0f, true); _endFired = false; _prevFrame = 0f; _clipRestarted = true; }   // a restart (iyaiya again)
            // else: a looping clip joined to itself just goes on -- its cycle state stays
        }
        else { _anim.Play(BckNames[i], blendSec); _endFired = false; _prevFrame = 0f; _clipRestarted = true; }
        _anim.SpeedScale = 60f / _bckRate;
    }
    /// <summary>getFrame: the clip's position in bck frames (the nerve's ticks halved when the clip is missing).</summary>
    public float BckFrame()
    {
        if (_bck < 0) return 0f;
        if (_anim == null || !_anim.HasAnimation(BckNames[_bck]) || _anim.CurrentAnimation != BckNames[_bck]) return BckLoops[_bck] ? (_nerveTicks * 0.5f) % BckFrames[_bck] : Mathf.Min(_nerveTicks * 0.5f, BckFrames[_bck] - 1);
        return (float)_anim.CurrentAnimationPosition * _bckRate;
    }
    /// <summary>curBckFinished / curAnmEndsNext, ONCE a cycle: STATE_COMPLETED_ONCE / LOOPED_ONCE / frame + 0.1 >= end -- true in the clip's last frame, then not
    /// again until the clip wraps (a looping clip) or changes; a wrap whose last frame the ticks never sampled fires then.</summary>
    private bool BckFinishedOnce(float f)
    {
        if (_bck < 0) return false;
        int end = BckFrames[_bck] - 1;
        if (f < _prevFrame - 1f) { bool fire = !_endFired; _endFired = false; return fire; }      // the clip wrapped: a new cycle
        bool stopped = !BckLoops[_bck] && _anim != null && _anim.HasAnimation(BckNames[_bck]) && !_anim.IsPlaying() && _anim.CurrentAnimation == BckNames[_bck];
        if (f + 1.1f >= end || stopped) { if (_endFired) return false; _endFired = true; return true; }
        return false;
    }
    /// <summary>checkBckPass(frame): the clip crossed the frame this tick (a looping clip's wrap counts the crossing on either side of it).</summary>
    private bool BckPassed(float frame, float f)
    {
        if (f >= _prevFrame) return _prevFrame < frame && f >= frame;
        return _prevFrame < frame || f >= frame;
    }
}
