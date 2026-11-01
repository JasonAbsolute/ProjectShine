using Godot;
using System;

/// <summary>
/// SMS pass 150 -- the Chain Chomp (TBossWanwan : TSpineEnemy, Enemy/bosswanwan.cpp, BossWanwan.hpp), Pianta Village's house-sized red-hot
/// Chain Chomp on its leash (<see cref="SmsBossWanwanLeash"/>): the one Mario cools with the hose and drags by its stake to the hot spring.
/// The game ships no bosswanwan.prm: the numbers are TBWParams' own defaults.
///
/// THE GAME. init: bwanwan_body.bmd (7 bcks: 0 bark 214, 1 down 105, 2 shake 55, 3 sleep 3 L, 4 wait 25 L, 5 wait2 25 L, 6 wakeup 135; the brks
/// 0 body, 1 body_gold, 2 chain, 3 chain_gold, 4 root; the btps 0 tdown, 1 tsleep, 2 twakeup), the graph "bwanwan" with its goal node at the bath
/// (BW_BATH_POS), GRAPHWANDER, the march 6 and turn 1, the body at BW_HEAD_START, the leash of 15 nodes and the picket at BW_PICKET_START,
/// STAKED (mIsNearMario = 1), the two body hits (TBWHit: at joint 3 the attack 500 x 500 / receive 450 x 500; 500 u under it 300 x 500 / 270 x
/// 500), the body radius 500, the wait clip, brk 0 at the hit points' frame, 255 hit points, the bath's collision model (bwanwan_ofuro_col.col)
/// made and removed. perform: CUE_CALC_ANIM -> the brk frame = hp / max x 299 (hot at the max, cold at 0), node 0's temperature at 0 and at the
/// max, emitEffects; the hits at joint 3 and 500 under; the balloons (not ported); hot and staked and pulled once -> 600 ticks -> SHAKE pushed
/// (it pulls its stake out); at 0 and not barking -> 2400 ticks -> BARK; else every 20 ticks a hit point back; staked wandering with the picket
/// taken -> the drag sound; the leash and the picket performed; the motion blend stepped (changeBck blends 10 frames). control: staked, or the
/// picket taken and the head pulled (|mPicketPullDelta|^2 >= PullLimit 1) -> mPositionDelta += the pull, the turn to put its back to the chain's
/// node 3 (4 x the turn speed); the pull cleared. TBWBinder::bind: airborne -> gravity (0.15) and the terminal; JumpToBath / Die -> no ground;
/// airborne -> checkGround from the head height -> landed; on the ground the movement is projected onto the graph's rail (prev -> cur, at least
/// 3 u a tick along it) and pulled back onto the line (10 u a tick at most); the roll (unk168) += the distance / (2 pi 500) x 360 x 2 (reset past
/// 360); staked -> kept within 860 u of the leash's node 3. receiveMessage: Mario -> nothing (no stomp); WATER -> the WATHIT mark, the sound (1
/// left -> TO_COOL + the DOWNYUGE steam; 0 -> COOL_MORE; else COOL), a hit point off, the steam flag; a WOOD_BARREL -> hit points 0 at once.
/// takeBath (Die): invincible, the body at BW_BATH_POS, the bath collision up (1.1 x), the hits and the leash's collision off, the picket off.
/// emitEffects: wait / wait2 passing frame 8 or shake passing 38 -> JUMP_ROCK + JUMP_SMOKE at the feet + the chain's rattle (CAL cold / ANG hot)
/// at the head and the picket; bark passing 72 / wait passing 6 or 12 / shake passing 4 -> HIBANA sparks at joint 1; wait passing 10 -> the
/// camera shake 0x16 (shakebowaland1: 20 ticks, pitch 100 at 5000); bark passing 60 / 127 -> 0x16 + rock + smoke, passing 202 -> 0x17
/// (shakebowaland2: 80 ticks, pitch 150 at 4000, roll 100 at 2000) + rock + smoke; shake passing 40 -> 0x16; hot -> YUGAMI (the heat haze) at
/// joint 1; sprayed and hot -> HITYUGE steam. shakeCamera: Mario on the ground; the reach ShakeLengthMax 3000 x r + ShakeLengthMaxHP0 2000 x
/// (1 - r) (r = 1 barking, else hp / max); the power (reach - his distance) / reach, x r. GRAPHWANDER: tick 0 wait (wait2 at 0 hp), the btp
/// tdown held at 0 (the eyes open); at 0 hp the wait clip's end -> wait2; LOOSE, the picket taken and the head pulled -> within 400 u of the
/// previous node: the goal node -> JUMPTOBATH after, else the rail reversed toward Mario; slideToCurPathNode(3 x hp / max x the march, the
/// turn) (0 at 0 hp: he drags it); the goal reached -> jumpToNextGraphNode (a node flagged 1 -> JUMP to its flagged neighbour) or
/// rollNextGraphNode (the next node within 100 deg of its facing, the one most away from Mario); the picket taken -> the slide at hp / max x the
/// march x (1 - 0.75 x his stick x (his stick . the way to him)) clamped 0..1.5; else hp / max x the march + 0.2. JUMP: the velocity to the
/// goal (calcVelocityToJumpToY at the node's speed), airborne; reached -> GRAPHWANDER. BARK: bark; pulled once and staked -> the stake out (the
/// smoke, ESCAPE1); tick 280 -> the hit points back to the max; the clip's end -> GRAPHWANDER. SHAKE: shake; the end -> releasePicket, loose,
/// ESCAPE2. JUMPTOBATH: the velocity to the bath (y speed 10), the goal the bath; past 120 ticks under the bath's y + 10 -> the water column (x 5)
/// + DIVE; within 100 u at or under the bath's y -> the body at the bath, DIE after. DIE: the velocity 0, no bind, DEADYUGE; hit points left ->
/// one off a tick; then the down camera, takeBath, the down clip, the btp tdown (the blinks, then the X eyes), the GOLD brk (0 -> 300, held at
/// its end), past tick 60 the shine (makeShineAppearWithDemo), the sparkles (KIRA) past half the gold. STUN / WAKEUP / ROLL / JUMPAWAY / FALL
/// are defined but never entered by the shipped code (the sleep and wakeup clips, the tsleep / twakeup btps are theirs).
///
/// THE PORT. SmsSmallEnemy's spine, tick and physics with the boss's six nerves (NerveSpecies + 0..5: GraphWander, Jump, Bark, Shake, JumpToBath,
/// Die; the base's Die unused); its own HitCheckMario (the two body cylinders: the BURN when hot; the leash's nodes: the BURN under node 8 when
/// hot and Mario kept out; the hose on the body: <see cref="SmsBossWanwanBody"/>, a StaticBody3D child of the game's two receive cylinders the
/// drops' rays hit and Mario walks against -- it stands in for the game's keepDistance pushes, and in the bath for the collision model he
/// climbs); the floor probe is the body's own small foot collider (the game reads no walls: it follows its rail). The graph: <see cref="SmsSmallEnemy.Graph"/>'s children IN
/// ORDER are the rail (node i joins i - 1 and i + 1; a child with meta sms_jump = the game's jump flag: the Chomp hops to the next node); the
/// goal node is the one nearest <see cref="BathPoint"/> (the last child without one); no graph -> random points within WanderRadiusUnits
/// (DEVIATION, as the base's) and no rail projection. The stake stands at <see cref="PicketPoint"/> (default: 1000 u behind the placed body).
/// The heat is the brk's keys (bwanwan_body.brk / _gold.brk) written to c1 / c2 of the body, root and mouth materials (own copies) and to each
/// chain link by its temperature; the eyes' btp swaps the eye material's picture. The camera shakes are SmsCameraShake's StartMode with two
/// SmsCameraShakeMode knobs (the game's shakebowaland1 / 2). Knobs (grouped, plain English): Deviations (SpeedScale, RollSw, JumpSpeedY), the
/// level's points (BathPoint, PicketPoint, ShineScene, ShineSpawnPoint), Effects (StompDustFxScene = Fx/HipDropLanding, SteamFxScene =
/// Fx/GkKemuriW, SplashFxScene = Fx/WaterSplash, the rocks / sparks / heat haze / sparkles slots), Camera (the two shake modes), Sounds (slots).
/// DEVIATIONS (logged): the demo cameras (the dive, the shine), the balloons and the rumble are not ported; the bath collision model is the body
/// sphere x 1.1 (not bwanwan_ofuro_col.col); the 10-frame clip blend is not ported; Mario's clip while a hot one drags him stays the hold.
/// </summary>
public partial class SmsBossWanwan : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ TBWParams (no prm shipped: the defaults)
    public class BossParams : Params
    {
        public float SLMarchSpeed = 6f, SLTurnSpeed = 1f, LeashNodeLen = 120f, PicketHeight = 100f, PicketRadius = 100f, ChainHitHeight = 100f, ChainHitRadius = 100f, ChainGroundRadius = 60f;
        public float PullLimit = 1f, AttackSpeed = 10f, SLSearchLength = 10000f, SLSearchAngle = 60f, HeadGap = 150f, ShakeLengthMax = 3000f, ShakeLengthMaxHP0 = 2000f;
        public int StunTimer = 4000, BWHitPointMax = 255;
    }
    public static readonly BossParams Prm = new BossParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 80f, ClipRadius = 100f, HitPointMax = 255, InstanceNum = 1, ActiveEnemyNum = 1, Gravity = 0.15f, JumpForce = 10f,
        SearchLength = 10000f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 60f, GiveUpLength = 2000f, GiveUpHeight = 650f, AttackWait = 120, FreezeWait = 300,
        DamageRadius = 450f, DamageHeight = 500f, AttackRadius = 500f, AttackHeight = 500f, TurnSpeedLow = 1f, TurnSpeedHigh = 1f, BodyScaleLow = 1f, BodyScaleHigh = 1f,
        GenItemRate = 0f, GenEggRate = 0f, WaitTime = 100, StampRange = 0f, MarchSpeedLow = 6f, MarchSpeedHigh = 6f,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("The level's points (empty = the defaults)")]
    /// <summary>The hot spring (BW_BATH_POS): where a cooled Chomp dragged to the rail's goal node leaps and is beaten. Empty: the Chomp is beaten where it is dragged over water.</summary>
    [Export] public Node3D BathPoint;
    /// <summary>Where its stake stands (BW_PICKET_START). Empty: 1000 u straight behind the placed body.</summary>
    [Export] public Node3D PicketPoint;
    /// <summary>The Shine that appears 60 ticks into its bath (makeShineAppearWithDemo "the boss shine"): a ShineSprite scene (assets/ShineSprite.tscn or his own). Empty = none.</summary>
    [Export] public PackedScene ShineScene;
    /// <summary>Where that Shine appears. Empty: over the Chomp in the bath.</summary>
    [Export] public Node3D ShineSpawnPoint;
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the slide, the turn and the drag speeds.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    /// <summary>The body tumble-rolls as it slides. OFF by default (pass 150b): the game rolls only an inner joint (unk168 on the 'roll' node), but the
    /// port's rig turned the whole body, which read as the Chomp somersaulting instead of walking -- turn it back on to try the tumble.</summary>
    [Export] public bool RollSw = false;
    /// <summary>The upward speed of a hop between rail nodes (the game's is each node's own; units a tick).</summary>
    [Export(PropertyHint.Range, "5,60,1")] public float JumpSpeedY = 20f;
    /// <summary>How long a cooled Chomp stays cold before it barks and reheats (the game's 2400 counts; a tick here is 1/120 s, so 2400 = 20 s --
    /// raise it (9600 = 80 s) when the drag to your bath is long).</summary>
    [Export(PropertyHint.Range, "120,36000,10")] public int ColdTicks = 2400;
    /// <summary>How long a hot Chomp stays staked after it was once pulled before it shakes its stake out (the game's 600 counts = 5 s here).</summary>
    [Export(PropertyHint.Range, "60,36000,10")] public int ShakeTicks = 600;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_bwan_jump_smoke: the dust where it slams down (the wait clip's stomp, the bark, the shake). Empty = assets/Sms/Fx/HipDropLanding.tscn, scaled to the body.</summary>
    [Export] public PackedScene StompDustFxScene;
    /// <summary>ms_bwan_hityuge (the steam of a hose hit), ms_bwan_downyuge (the great steam as it cools), ms_bwan_deadyuge (in the bath). Empty = assets/Sms/Fx/GkKemuriW.tscn.</summary>
    [Export] public PackedScene SteamFxScene;
    /// <summary>The water column where it lands in the bath (x 5). Empty = assets/Sms/Fx/WaterSplash.tscn.</summary>
    [Export] public PackedScene SplashFxScene;
    /// <summary>ms_bwan_jump_rock (the stones its slams kick up), ms_bwan_hibana (the sparks from its hinge), ms_bwan_yugami (the heat haze over a hot one), ms_bwan_kira (the sparkles of the gold one). Slots.</summary>
    [Export] public PackedScene RockFxScene, SparkFxScene, HeatHazeFxScene, SparkleFxScene;
    [ExportGroup("Camera (the game's shakebowaland1 / 2; empty = those)")]
    /// <summary>startShake(0x16): the slam of the wait clip, the shake clip, the bark's first two slams -- 20 ticks, pitch 100 at 5000.</summary>
    [Export] public SmsCameraShakeMode ShakeLand1;
    /// <summary>startShake(0x17): the bark's last slam -- 80 ticks, pitch 150 at 4000, roll 100 at 2000.</summary>
    [Export] public SmsCameraShakeMode ShakeLand2;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>The bas tables (bark / shake / wait / wait2) are the clips'; these are the code's: the hose's cooling / almost-cooled / cooled-more, the stake locked by his ground
    /// pound, the stake pulled out (the bark's / the shake's), the dive into the bath, the chain's rattle hot / cold at a slam, the drag of a staked one.</summary>
    [Export] public AudioStreamPlayer3D CoolSound, ToCoolSound, CoolMoreSound, LockSound, Escape1Sound, Escape2Sound, DiveSound, ChainHotSound, ChainColdSound, DragSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's order)
    private static readonly string[] Names = { "bwanwan_bark", "bwanwan_down", "bwanwan_shake", "bwanwan_sleep", "bwanwan_wait", "bwanwan_wait2", "bwanwan_wakeup" };
    private static readonly int[] Frames = { 214, 105, 55, 3, 25, 25, 135 };
    private static readonly bool[] Loops = { false, false, false, true, true, true, false };
    protected const int BckBark = 0, BckDown = 1, BckShake = 2, BckSleep = 3, BckWait = 4, BckWait2 = 5, BckWakeup = 6;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected const int NerveWanderB = NerveSpecies + 0, NerveJumpB = NerveSpecies + 1, NerveBark = NerveSpecies + 2, NerveShake = NerveSpecies + 3, NerveJumpToBath = NerveSpecies + 4, NerveDieB = NerveSpecies + 5;
    private static readonly string[] BossNerveNames = { "GraphWander", "Jump", "Bark", "Shake", "JumpToBath", "Die" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + BossNerveNames.Length ? BossNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveWanderB;

    // ------------------------------------------------------------------------------------------------------------------ state
    private SmsBossWanwanLeash _leash; private bool _leashReady;
    private ShaderMaterial _linkMatSrc, _picketMatSrc; private PackedScene _linkScene, _picketScene;
    private SmsBossWanwanBody _bodySphere;
    private ShaderMaterial _bodyMat, _rootMat, _mouthMat, _eyeMat;
    private Texture2D _eyeOpen, _eyeClose, _eyeCross; private int _eyeTex = -1;
    private Skeleton3D _skel; private int _chainBone = -1, _rollBone = -1, _upBone = -1;
    private bool _staked = true, _neverPulled = true, _invincible, _shineDone, _waterSpawned, _dieEntered, _dieStarted, _barrelCooled;
    private Node3D _stompFx;
    private int _shakeTimer, _coldTimer, _steamPending, _bathHops;
    private float _rollDeg, _prevBckFrame, _goldFrame = -1f;
    private Vector3 _pullDelta, _bathPos;
    private bool _yanked;
    private int _pathPrev = -1, _pathCur = -1;
    public int Burns { get; private set; }
    public int Stomps { get; private set; }
    public int Shakes { get; private set; }
    public int Barks { get; private set; }
    public int Yanks { get; private set; }
    public int HpRegens { get; private set; }
    public int Jumps { get; private set; }
    public int Reverses { get; private set; }
    public bool Staked => _staked;
    public bool BarrelCooled => _barrelCooled;
    public int BathHops => _bathHops;
    public bool PulledOnce => !_neverPulled;
    public bool Invincible => _invincible;
    public bool ShineSpawned => _shineDone;
    public float RollDeg => _rollDeg;
    public float GoldFrame => _goldFrame;
    public float HeatFrame => HeatRatioNow * 299f;
    public float HpRatio => _hp / (float)Prm.BWHitPointMax;
    /// <summary>The heat ratio the LOOK uses (the body's brk colour and the chain links): during the bark it ramps 0 -> 1 over the roar up to tick 280,
    /// where the hit points actually restore, so the Chomp GLOWS back from cold blue to red-hot as it reignites instead of snapping at that one tick
    /// (pass 150b). Everywhere else it is the plain hit-point ratio.</summary>
    public float HeatRatioNow => IsBarking ? MathF.Max(HpRatio, Mathf.Clamp(_time / 280f, 0f, 1f)) : HpRatio;
    public int ShakeTimer => _shakeTimer;
    public int ColdTimer => _coldTimer;
    public int EyeTexture => _eyeTex;
    public Vector3 PullDeltaNow => _pullDelta;
    public bool Yanked => _yanked;
    public SmsBossWanwanLeash Leash => _leash;
    public SmsBossWanwanPicket Picket => _leash?.Picket;
    public SmsBossWanwanBody BodySphere => _bodySphere;
    public Vector3 BodyColorC1 => _bodyMat != null ? _bodyMat.GetShaderParameter("c1").AsVector3() : Vector3.Zero;
    public Vector3 BodyColorC2 => _bodyMat != null ? _bodyMat.GetShaderParameter("c2").AsVector3() : Vector3.Zero;
    public int PathPrev => _pathPrev;
    public int PathCur => _pathCur;
    public int GoalIndex => GoalNodeIndex();

    // the state tests (the game's)
    public bool IsDefeat => IsNerve(NerveDieB) || (_cur == NerveNone && _prev == NerveDieB);
    public bool IsJumpingToBath => IsNerve(NerveJumpToBath);
    public bool IsBarking => IsNerve(NerveBark);
    public bool IsShaking => IsNerve(NerveShake);
    public bool IsWandering => IsNerve(NerveWanderB);
    public bool IsPicketTaken => _leash != null && _leash.Picket != null && _leash.Picket.IsHeld;
    /// <summary>isHeadPulled: this tick's pull on the head is at least PullLimit.</summary>
    public bool IsHeadPulled => _pullDelta.LengthSquared() >= Prm.PullLimit;

    protected override void Init()
    {
        StompDustFxScene ??= LoadIf("res://assets/Sms/Fx/HipDropLanding.tscn");
        SteamFxScene ??= LoadIf("res://assets/Sms/Fx/GkKemuriW.tscn");
        SplashFxScene ??= LoadIf("res://assets/Sms/Fx/WaterSplash.tscn");
        SparkFxScene ??= LoadIf("res://assets/Sms/Fx/HitBurst.tscn");   // the HIBANA sparks off its hot mouth (pass 150c): an orange-streak burst, not the cyan ShockSparks
        ShakeLand1 ??= new SmsCameraShakeMode { TimeTicks = 20, AmpX = 100f, VelX = 5000 };
        ShakeLand2 ??= new SmsCameraShakeMode { TimeTicks = 80, AmpX = 150f, VelX = 4000, AmpZ = 100f, VelZ = 2000 };
        if (_model != null)
        {
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    if (mi.GetActiveMaterial(i) is not ShaderMaterial src) continue;
                    string rn = src.ResourceName ?? "";
                    var own = (ShaderMaterial)src.Duplicate();
                    mi.SetSurfaceOverrideMaterial(i, own);
                    if (rn.StartsWith("_mat_body")) _bodyMat = own;
                    else if (rn.StartsWith("_mat_root")) _rootMat = own;
                    else if (rn.StartsWith("_mat_kuti")) _mouthMat = own;
                    else if (rn.StartsWith("_mat_me")) { _eyeMat = own; _eyeOpen = own.GetShaderParameter("base_tex").As<Texture2D>(); }
                }
            }
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null) { _chainBone = _skel.FindBone("chain_null"); _rollBone = _skel.FindBone("roll"); _upBone = _skel.FindBone("jnt_up"); }
        }
        _eyeClose = LoadTex("res://models/Eniemes/BossWanwan/N_bwanwan_me_close.png");
        _eyeCross = LoadTex("res://models/Eniemes/BossWanwan/N_bwanwan_me_cross.png");
        _linkMatSrc = LoadMat("res://assets/Sms/Enemy/BossWanwan/BossWanwanChain.tres");
        _picketMatSrc = LoadMat("res://assets/Sms/Enemy/BossWanwan/BossWanwanPicket.tres");
        _linkScene = LoadIf("res://models/Eniemes/BossWanwan/bwanwan_chain.glb");
        _picketScene = LoadIf("res://models/Eniemes/BossWanwan/bwanwan_picket.glb");
        // the body's cylinders: the drops' rays and Mario's body meet them (the game's receive cylinders and keepDistance), the bath's platform later
        _bodySphere = new SmsBossWanwanBody { Name = "BodyCylinders" };
        AddChild(_bodySphere);
        _bodySphere.Setup(this);
        CallDeferred(nameof(ExceptBodySphere));
        // the leash: a sibling (TopLevel links), set up on the first tick in the tree
        _leash = new SmsBossWanwanLeash { Name = Name + "Leash" };
        Node parent = GetParent() ?? this;
        parent.CallDeferred(Node.MethodName.AddChild, _leash);
        if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: the leash made; the chain joint {(_chainBone >= 0 ? "chain_null" : "MISSING")}, the body material {(_bodyMat != null ? "own" : "none")}");
    }
    private void ExceptBodySphere() { if (_bodySphere != null) AddCollisionExceptionWith(_bodySphere); }
    private static PackedScene LoadIf(string path) => ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
    private static Texture2D LoadTex(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    private static ShaderMaterial LoadMat(string path) => ResourceLoader.Exists(path) ? GD.Load<ShaderMaterial>(path) : null;
    public override void _ExitTree()
    {
        if (_leash != null && IsInstanceValid(_leash) && !_leash.IsQueuedForDeletion()) _leash.QueueFree();
        base._ExitTree();
    }

    protected override void Reset()
    {
        base.Reset();
        _marchSpeed = Prm.SLMarchSpeed * SpeedScale; _turnSpeed = Prm.SLTurnSpeed * SpeedScale;
        _hp = Prm.BWHitPointMax;
        _staked = true; _neverPulled = true; _invincible = false; _shineDone = false; _waterSpawned = false; _dieEntered = false; _dieStarted = false; _barrelCooled = false;
        _shakeTimer = 0; _coldTimer = 0; _steamPending = 0; _bathHops = 0; _rollDeg = 0f; _goldFrame = -1f; _pullDelta = Vector3.Zero; _yanked = false;
        _pathPrev = -1; _pathCur = -1;
        _noBind = false;
        SetEyeTexture(0);
        ApplyHeat();
        if (_bodySphere != null) { _bodySphere.SetScale(1f); _bodySphere.Enabled = true; }
        GoToShortestNextGraphNode();
        if (_leash != null && IsInstanceValid(_leash) && _leashReady)
        {
            Vector3 head = ChainJointUnits();
            _leash.MoveHead(head);
            _leash.StakeUnits = StakeUnits();
            _leash.LayStraight(_leash.StakeUnits);
            _leash.Picket.Disabled = false;
            _leash.PlaceLinks();
        }
    }
    /// <summary>BW_PICKET_START: the PicketPoint, or 1000 u behind the placed body; 60 u up (the rope rests ChainGroundRadius over the ground).</summary>
    private Vector3 StakeUnits()
    {
        Vector3 p = PicketPoint != null ? PicketPoint.GlobalPosition * U : _spawnPos + PolarXZ(_spawnYaw + 180f, 1000f);
        return p + Vector3.Up * Prm.ChainGroundRadius;
    }

    // ------------------------------------------------------------------------------------------------------------------ the joints
    private Vector3 BoneUnits(int bone, Vector3 fallbackU)
    {
        if (_skel != null && bone >= 0) return (_skel.GlobalTransform * _skel.GetBoneGlobalPose(bone).Origin) * U;
        return fallbackU;
    }
    /// <summary>Joint 5 chain_null: the chain's root at the back of the body.</summary>
    public Vector3 ChainJointUnits() => BoneUnits(_chainBone, Pos() + PolarXZ(_yaw + 180f, 500f) + Vector3.Up * 500f);
    /// <summary>Joint 1 roll: the body's centre (the model's origin, 500 u over the feet).</summary>
    public Vector3 CenterUnits() => Pos() + Vector3.Up * 500f * _scale;
    /// <summary>Joint 3 jnt_up: the upper jaw's hinge -- the first hit's point.</summary>
    public Vector3 HitUpUnits() => BoneUnits(_upBone, CenterUnits());

    // ------------------------------------------------------------------------------------------------------------------ the brk keys (bwanwan_body.brk, _gold.brk; the chain's are the same)
    private static Vector3 Key3(float f, Vector3 a, Vector3 b, Vector3 c)
    {
        f = Mathf.Clamp(f, 0f, 300f);
        return (f <= 149f ? a.Lerp(b, f / 149f) : b.Lerp(c, (f - 149f) / 151f)) / 255f;
    }
    private static Vector3 Key2(float f, Vector3 a, Vector3 b) => a.Lerp(b, Mathf.Clamp(f, 0f, 300f) / 300f) / 255f;
    /// <summary>_mat_body colour id 1 (C1, the glint): (0, 30, 80) cold, (20, 0, 0) at 149, (250, 120, 40) hot.</summary>
    public static Vector3 BrkBodyC1(float f) => Key3(f, new Vector3(0, 30, 80), new Vector3(20, 0, 0), new Vector3(250, 120, 40));
    /// <summary>_mat_body colour id 2 (C2, the body): (0, 0, 60) cold, (0, 0, 0) at 149, (250, 0, 0) hot.</summary>
    public static Vector3 BrkBodyC2(float f) => Key3(f, new Vector3(0, 0, 60), new Vector3(0, 0, 0), new Vector3(250, 0, 0));
    /// <summary>_mat_kuti colour id 2: 150 cold, 50 at 149, 250 hot (red only).</summary>
    public static Vector3 BrkMouthC2(float f) => Key3(f, new Vector3(150, 0, 0), new Vector3(50, 0, 0), new Vector3(250, 0, 0));
    public static Vector3 BrkGoldC1(float f) => Key2(f, new Vector3(0, 30, 80), new Vector3(120, 100, 0));
    public static Vector3 BrkGoldC2(float f) => Key2(f, new Vector3(0, 0, 60), new Vector3(220, 160, 0));
    public static Vector3 BrkGoldMouthC2(float f) => Key2(f, new Vector3(150, 0, 0), new Vector3(0, 0, 0));
    /// <summary>The body's brk at the hit points' frame (perform's CUE_CALC_ANIM), or the gold brk's frame in the bath.</summary>
    private void ApplyHeat()
    {
        if (_goldFrame >= 0f)
        {
            Vector3 c1 = BrkGoldC1(_goldFrame), c2 = BrkGoldC2(_goldFrame);
            _bodyMat?.SetShaderParameter("c1", c1); _bodyMat?.SetShaderParameter("c2", c2);
            _rootMat?.SetShaderParameter("c1", c1); _rootMat?.SetShaderParameter("c2", c2);
            _mouthMat?.SetShaderParameter("c2", BrkGoldMouthC2(_goldFrame));
            return;
        }
        float f = HeatFrame;
        Vector3 b1 = BrkBodyC1(f), b2 = BrkBodyC2(f);
        _bodyMat?.SetShaderParameter("c1", b1); _bodyMat?.SetShaderParameter("c2", b2);
        _rootMat?.SetShaderParameter("c1", b1); _rootMat?.SetShaderParameter("c2", b2);
        _mouthMat?.SetShaderParameter("c2", BrkMouthC2(f));
    }
    /// <summary>The btps' pictures: 0 open, 1 close, 2 cross.</summary>
    private void SetEyeTexture(int which)
    {
        if (_eyeTex == which || _eyeMat == null) return;
        Texture2D t = which == 1 ? _eyeClose : which == 2 ? _eyeCross : _eyeOpen;
        if (t == null) return;
        _eyeMat.SetShaderParameter("base_tex", t);
        _eyeTex = which;
    }
    /// <summary>bwanwan_tdown.btp (105 frames, once): frames 0-71 two open / four closed, 72-96 open, 97-104 the X eyes (held).</summary>
    private static int TdownTexture(float frame)
    {
        if (frame >= 97f) return 2;
        if (frame >= 72f) return 0;
        return (frame % 6f) < 2f ? 0 : 1;
    }

    // ------------------------------------------------------------------------------------------------------------------ the rail (the graph's children in order)
    private int NodeCountOnRail => Graph != null ? Graph.GetChildCount() : 0;
    private Vector3 NodeUnits(int i) => Graph.GetChild(i) is Node3D n ? n.GlobalPosition * U : Pos();
    private bool NodeIsJump(int i) => Graph.GetChild(i) is Node n && n.HasMeta("sms_jump") && n.GetMeta("sms_jump").AsBool();
    /// <summary>initGoalIndex(BW_BATH_POS): the node nearest the bath (the last without a BathPoint); -1 without a rail.</summary>
    private int GoalNodeIndex()
    {
        int n = NodeCountOnRail; if (n == 0) return -1;
        if (BathPoint == null) return n - 1;
        Vector3 b = BathPoint.GlobalPosition * U; int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < n; i++) { float d = (NodeUnits(i) - b).LengthSquared(); if (d < bd) { bd = d; best = i; } }
        return best;
    }
    private int NearestNodeIndex()
    {
        int n = NodeCountOnRail; int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < n; i++) { float d = (NodeUnits(i) - Pos()).LengthSquared(); if (d < bd) { bd = d; best = i; } }
        return best;
    }
    private void MoveTo(int next) { _pathPrev = _pathCur; _pathCur = next; if (_pathCur >= 0) SetGoalPoint(NodeUnits(_pathCur)); }
    /// <summary>goToShortestNextGraphNode: the nearest node (none placed: the placed point).</summary>
    private void GoToShortestNextGraphNode()
    {
        if (NodeCountOnRail == 0) { _pathPrev = -1; _pathCur = -1; SetGoalPoint(_spawnPos); return; }
        _pathPrev = -1; _pathCur = NearestNodeIndex(); SetGoalPoint(NodeUnits(_pathCur));
    }
    /// <summary>The rail's neighbours of a node (i - 1 and i + 1).</summary>
    private int NeighbourCount(int i) { int n = NodeCountOnRail; int c = 0; if (i > 0) c++; if (i < n - 1) c++; return c; }
    /// <summary>rollNextGraphNode (getEscapeDirLimited(cur, prev, the facing, 100 deg)): of the neighbours but the previous within 100 degrees of the
    /// facing, the one most AWAY from Mario; none within -> the other neighbour.</summary>
    private void RollNextGraphNode()
    {
        if (NodeCountOnRail == 0) { GoToRandomNextGraphNode(); return; }
        int cur = _pathCur < 0 ? NearestNodeIndex() : _pathCur;
        Vector3 facing = PolarXZ(_yaw, 1f), toMario = MarioPos() - Pos(); toMario.Y = 0f; toMario = toMario.LengthSquared() > 0f ? toMario.Normalized() : facing;
        int best = -1; float bestCos = float.MaxValue; int fallback = -1;
        foreach (int c in new[] { cur - 1, cur + 1 })
        {
            if (c < 0 || c >= NodeCountOnRail || c == _pathPrev) continue;
            fallback = c;
            Vector3 d = NodeUnits(c) - Pos(); d.Y = 0f; if (d.LengthSquared() < 1e-6f) continue; d = d.Normalized();
            float ang = Mathf.RadToDeg(MathF.Acos(Mathf.Clamp(facing.Dot(d), -1f, 1f)));
            if (ang >= 100f) continue;
            float cos = d.Dot(toMario);
            if (best < 0 || cos < bestCos) { best = c; bestCos = cos; }
        }
        if (best < 0) best = fallback >= 0 ? fallback : (_pathPrev >= 0 ? _pathPrev : cur);
        MoveTo(best);
    }
    /// <summary>jumpToNextGraphNode: the current node flagged (meta sms_jump) -> the neighbour but the previous is the hop's goal; else -1.</summary>
    private int JumpToNextGraphNode()
    {
        if (NodeCountOnRail == 0 || _pathCur < 0 || !NodeIsJump(_pathCur)) return -1;
        int cur = _pathCur;
        foreach (int c in new[] { cur + 1, cur - 1 })
            if (c >= 0 && c < NodeCountOnRail && c != _pathPrev) { MoveTo(c); return c; }
        return -1;
    }
    /// <summary>reverseNextGraphNode: dragged back past the previous node -- the rail flips toward Mario: the previous node is the goal again and its
    /// neighbour nearest Mario's way becomes the previous.</summary>
    private void ReverseNextGraphNode()
    {
        if (NodeCountOnRail == 0 || _pathPrev < 0) return;
        int prev = _pathPrev, cur = _pathCur;
        Vector3 toMario = MarioPos() - Pos(); toMario.Y = 0f; toMario = toMario.LengthSquared() > 0f ? toMario.Normalized() : Vector3.Back;
        int next = -1; float bestCos = float.MinValue;
        foreach (int c in new[] { prev - 1, prev + 1 })
        {
            if (c < 0 || c >= NodeCountOnRail || c == cur) continue;
            Vector3 d = NodeUnits(c) - Pos(); d.Y = 0f; float cos = d.LengthSquared() > 0f ? d.Normalized().Dot(toMario) : -1f;
            if (cos > bestCos) { bestCos = cos; next = c; }
        }
        _pathPrev = next; _pathCur = prev; SetGoalPoint(NodeUnits(prev));
        Reverses++;
    }
    /// <summary>slideToCurPathNode(speed, turn): the turn toward the goal clamped at turn a tick, the step of speed along the way to it (the way itself,
    /// not the heading).</summary>
    private void SlideToCurPathNode(float speed, float turn)
    {
        Vector3 d = Goal() - Pos(); float dist = d.Length();
        float target = Wrap360(YawOf(d));
        float diff = Mathf.Clamp(AngleDiff(target, _yaw), -turn, turn);
        _yaw = Wrap360(_yaw + diff);
        if (dist > 0f) _lin += d * (speed / dist);
    }

    // ------------------------------------------------------------------------------------------------------------------ the messages
    /// <summary>TBWPicket::receiveMessage(TAKE): releasePicket (the smoke if staked), pulled once, loose.</summary>
    public void OnPicketTaken()
    {
        ReleasePicket();
        _neverPulled = false;
        _staked = false;
        if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: the stake TAKEN -- loose");
    }
    public void OnPicketReleased() { if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: the stake let go ({(_staked ? "staked" : "loose")})"); }
    /// <summary>TBWPicket::receiveMessage(HIP_DROP): staked again where the tail lies, the shake timer 0, the LOCK sound.</summary>
    public void OnPicketHipDrop()
    {
        _staked = true; _shakeTimer = 0;
        if (_leash != null) _leash.StakeUnits = _leash.TailUnits;
        LockSound?.Play();
        if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: the stake LOCKED by his ground pound");
    }
    /// <summary>releasePicket: while staked, the smoke at the stake (JUMP_SMOKE scaled (0.3, 0.5, 0.3)).</summary>
    private void ReleasePicket()
    {
        if (!_staked || _leash == null) return;
        StompDust(_leash.TailUnits / U, new Vector3(0.3f, 0.5f, 0.3f) * 4f);
    }
    /// <summary>JUMP_SMOKE (+ the rocks' slot) at a point: the StompDustFxScene -- Mario's HipDropLanding / TrampleDust are template holders whose bursts
    /// SmsUnboundBurst copies out (their Play), an SmsFxBurst scene is fired as one, a bare GPUParticles3D as a one-shot.</summary>
    private void StompDust(Vector3 atM, Vector3 scale)
    {
        if (StompDustFxScene == null) return;
        if (_stompFx == null || !IsInstanceValid(_stompFx))
        {
            _stompFx = StompDustFxScene.Instantiate<Node3D>();
            _stompFx.Name = "StompDustTemplates"; _stompFx.TopLevel = true;
            AddChild(_stompFx);
            if (_stompFx is SmsFxBurst || _stompFx is GpuParticles3D) { _stompFx.QueueFree(); _stompFx = null; }   // a one-shot kind: fired fresh each time below
        }
        var at = new Transform3D(Basis.FromScale(scale), atM);
        if (_stompFx is SmsHipDropLanding h) { foreach (var p in new[] { h.Sparks, h.Smoke, h.Shockwave }) if (p != null) SmsUnboundBurst.Spawn(p, GetParent(), at); }
        else if (_stompFx is SmsTrampleDust t) { foreach (var p in new[] { t.Ring, t.Puff, t.Marks, t.Spokes }) if (p != null) SmsUnboundBurst.Spawn(p, GetParent(), at); }
        else if (_stompFx != null) { foreach (Node n in _stompFx.FindChildren("*", "GPUParticles3D", true, false)) SmsUnboundBurst.Spawn((GpuParticles3D)n, GetParent(), at); }
        else SpawnBurst(StompDustFxScene, at);
    }
    /// <summary>The leash's pullTail hands the head's displacement here (mPicketPullDelta).</summary>
    public void AddPullDelta(Vector3 deltaU) { _pullDelta += deltaU; }
    /// <summary>For the drives: the long counters set near their ends (the bark's ColdTicks, the shake's ShakeTicks).</summary>
    public void SmsDriveSetTimers(int cold = -1, int shake = -1) { if (cold >= 0) _coldTimer = cold; if (shake >= 0) _shakeTimer = shake; }
    /// <summary>receiveMessage(WATER): the mark, the sound by the hit points left, one off, the steam flag (every drop; no cooldown gate).</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead || _invincible) return _invincible;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (_hp == 0) CoolMoreSound?.Play();
        else if (_hp == 1) { ToCoolSound?.Play(); if (SteamFxScene != null) SpawnBurst(SteamFxScene, new Transform3D(Basis.FromScale(Vector3.One * 3f), CenterUnits() / U)); }
        else if (_sprayCooldown == 0) CoolSound?.Play();
        _hp = Math.Max(0, _hp - 1);
        _steamPending++;
        if (_sprayCooldown == 0) _sprayCooldown = 1;
        return true;
    }
    /// <summary>A wooden barrel (ACTOR_TYPE_WOOD_BARREL, Pianta Village's water barrels) against the body: the hit points 0 at once, the great steam, TO_COOL.</summary>
    public void SmsHitByWoodBarrel()
    {
        if (_dead || _invincible) return;
        _hp = 0; _steamPending++; _barrelCooled = true;
        if (SteamFxScene != null) SpawnBurst(SteamFxScene, new Transform3D(Basis.FromScale(Vector3.One * 3f), CenterUnits() / U));
        ToCoolSound?.Play();
    }
    protected override void OnBodyContact(Node3D other) { if (other.IsInGroup("sms_wood_barrel")) SmsHitByWoodBarrel(); }
    protected override bool IsHitValid(HitMessage msg) => false;
    protected override bool IsCollidMove(Node3D other) => false;
    protected override bool WallHopAllowed() => false;
    protected override bool KeepVelocityOnGround() => false;
    protected override float GravityY() => P.Gravity;
    public override void Kill() { }                                               // TBossWanwan::kill() { }

    /// <summary>The two body hits (TBWHit at joint 3, and 500 under it) and the leash's nodes against Mario: the BURN while hot (nodes under 8), the
    /// stake's ground pound; the pushes are the body sphere's.</summary>
    protected override void HitCheckMario()
    {
        if (_mario == null || _invincible || _dead) return;
        Vector3 m = MarioPos();
        Vector3 up = HitUpUnits(), low = new Vector3(up.X, up.Y - 500f, up.Z);
        bool hot = _hp != 0;
        bool hit0 = CheckDistance(up, 500f, 500f, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight);
        bool hit1 = CheckDistance(low, 300f, 500f, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight);
        if (hot && (hit0 || hit1)) Burn();
        if (_leash != null && _leashReady)
        {
            for (int i = 0; i < SmsBossWanwanLeash.NodeCount - 2; i++)
            {
                Vector3 p = _leash.NodeUnits(i);
                if (hot && i < 8 && CheckDistance(p, Prm.ChainHitRadius * 2f, Prm.ChainHitHeight * 2f, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight)) Burn();
                if (CheckDistance(m, SmsParams.Mario.HoldRadius, SmsParams.Mario.AttackHeight, p, Prm.ChainHitRadius, Prm.ChainHitHeight)) _mario.SmsKeepDistance(p, Prm.ChainHitRadius);
            }
            var pk = _leash.Picket;
            var ms = _mario.stateOfMario;
            if (pk != null && !pk.Disabled && !pk.IsHeld && (ms == Mario.MarioState.groundPoundFalling || ms == Mario.MarioState.groundPoundLanding) && _mario.Velocity.Y <= 0f)
            {
                Vector3 t = _leash.TailUnits;
                if (CheckDistance(m, SmsParams.Mario.TrampleRadius, SmsParams.Mario.AttackHeight, t, Prm.PicketRadius, Prm.PicketHeight) && m.Y >= t.Y - Prm.ChainGroundRadius) pk.OnHipDrop();
            }
        }
    }
    private void Burn()
    {
        _mario.TakeDamage(GlobalPosition, Mario.SmsDamageProfile.Fire);
        Burns++;
        if (DebugPrint && (Burns & 15) == 1) GD.Print($"[SMS bwanwan] {Name}: BURN on Mario");
    }

    // ------------------------------------------------------------------------------------------------------------------ moveObject
    protected override void MoveObjectPre()
    {
        if (!_leashReady && _leash != null && IsInstanceValid(_leash) && _leash.IsInsideTree())
        {
            Vector3 head = ChainJointUnits();
            _leash.Setup(this, head, StakeUnits(), _linkScene, _linkMatSrc, _picketScene, _picketMatSrc);
            _leashReady = true;
            if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: the leash set up -- the head {head}, the stake {_leash.StakeUnits}");
        }
        _yanked = false;
        // the hit points: at 0 and not barking the cold timer counts to the BARK; else the regen (every 20 ticks one back) and the timer 0
        if (!IsDefeat && !IsJumpingToBath)
        {
            if (_hp != 0)
            {
                if (_staked && !_neverPulled) { if (++_shakeTimer > ShakeTicks && !IsShaking) { PushNerve(NerveShake); _shakeTimer = 0; } }
            }
            else _shakeTimer = 0;
            if (_hp == 0 && !IsBarking)
            {
                if (++_coldTimer > ColdTicks) { _coldTimer = 0; SetNext(NerveBark); }
            }
            else
            {
                if ((_tickCount % 20) == 0 && _hp < Prm.BWHitPointMax) { _hp++; HpRegens++; }
                _coldTimer = 0;
            }
        }
        // the leash: perform(CUE_MOVE) before the body's control, as the game's order has the body's control() first then the leash's perform --
        // the port steps it here with last tick's body so the pull delta it hands over is consumed by this tick's control
        if (_leashReady)
        {
            _leash.MoveHead(ChainJointUnits());
            if (_staked)
            {
                _leash.PullTail(_leash.StakeUnits);
                Vector3 diff = _leash.HeadUnits - CenterUnits();
                if (diff.Length() > 500f + Prm.HeadGap)
                {
                    Vector3 pull = diff.Normalized() * 20f; pull.Y = 0f;
                    _lin += pull; _yanked = true; Yanks++;
                }
                float target = Wrap360(YawOf(diff) + 180f);
                float d = AngleDiff(target, _yaw);
                float max = 1.5f * _turnSpeed;
                _yaw = Wrap360(_yaw + Mathf.Clamp(d, -max, max));
            }
            _leash.KeepOutOfBody(CenterUnits());
        }
    }
    /// <summary>control()'s tail: staked, or the picket taken and the head pulled -> the pull on the body and the turn to put its back to node 3.</summary>
    private void ControlPull()
    {
        if (_staked || (IsPicketTaken && IsHeadPulled))
        {
            _lin += _pullDelta;
            if (_leashReady)
            {
                Vector3 d = Pos() - _leash.NodeUnits(3); d.Y = 0f;
                if (d.LengthSquared() > 1e-6f)
                {
                    float target = Wrap360(YawOf(d));
                    float diff = AngleDiff(target, _yaw);
                    float step = _turnSpeed * 4f;
                    _yaw = Wrap360(_yaw + Mathf.Clamp(diff, -step, step));
                }
            }
        }
        _pullDelta = Vector3.Zero;
    }
    protected override void SetBehavior()
    {
        // TBWBinder::bind's ground part: on the rail the movement is projected onto the current segment (at least 3 u a tick along it) and pulled
        // back toward the line (10 u at most); staked -> kept within 860 u of the leash's node 3; the roll from the distance moved
        if (!_airborne && !IsDefeat && !IsJumpingToBath)
        {
            Vector3 vel = _lin; vel.Y = 0f;                                               // grounded, the game's body rides its rail: no vertical step (the pull's y would lift it off the floor)
            if (NodeCountOnRail > 0 && _pathCur >= 0 && _pathPrev >= 0 && _pathCur != _pathPrev)
            {
                Vector3 a = NodeUnits(_pathPrev), b = NodeUnits(_pathCur), rail = b - a;
                if (rail.LengthSquared() > 0f)
                {
                    Vector3 dir = rail.Normalized();
                    float proj = vel.Dot(dir);
                    if (proj < 0f && proj > -3f) proj = -3f; else if (proj > 0f && proj < 3f) proj = 3f;
                    vel = dir * proj;
                    Vector3 foot = a + dir * Mathf.Clamp((Pos() - a).Dot(dir), 0f, rail.Length());
                    Vector3 pull = foot - Pos(); pull.Y = 0f;
                    float dist = MathF.Min(pull.Length(), 10f);
                    if (dist > 0.0001f) vel += pull.Normalized() * dist;
                }
            }
            if (_staked && _leashReady)
            {
                Vector3 anchor = _leash.NodeUnits(3);
                Vector3 next = Pos() + vel - anchor;
                if (next.Length() > 860f) vel = anchor + next.Normalized() * 860f - Pos();
            }
            if (RollSw)
            {
                float speed = vel.Length();
                if (speed > 0f)
                {
                    float deg = speed / 3141.5928f * 360f;
                    if (PolarXZ(_yaw, 1f).Dot(vel) < 0f) deg = -deg;
                    _rollDeg += deg * 2f;
                    if (_rollDeg > 360f || _rollDeg < -360f) _rollDeg = 0f;                  // the game resets past 360 (the same angle; here either way)
                }
            }
            _lin = vel;
        }
        // the leash's calc part and the picket after the body: the temperatures, the colours, the links, the picket
        if (_leashReady)
        {
            _leash.TemperatureTick(HpRatio, _hp, Prm.BWHitPointMax);
            if ((_tickCount & 3) == 0) { if (_goldFrame >= 0f) _leash.ApplyGold(_goldFrame); else _leash.ApplyColors(HeatRatioNow); }   // the links' 28 uniforms at 30 Hz (HeatRatioNow: they reheat with the body over the bark)
            _leash.Picket.Step();
            _leash.PlaceLinks();
            if (IsWandering && IsPicketTaken && _staked && DragSound != null && !DragSound.Playing && _pullDelta.LengthSquared() > 0f) DragSound.Play();
        }
        if ((_tickCount & 1) == 0) ApplyHeat();
        if (_goldFrame >= 0f && _goldFrame < 300f) _goldFrame = MathF.Min(300f, _goldFrame + 0.5f);   // the gold brk at the anim rate (2 ticks a frame), held at its end
        if (IsDefeat && _dieStarted) SetEyeTexture(TdownTexture(BckFrame()));
        EmitEffects();
        _prevBckFrame = BckFrame();
    }
    /// <summary>checkBckPass(f): the clip passed frame f this tick.</summary>
    private bool BckPassed(int clip, float f) => IsBck(clip) && _prevBckFrame < f && BckFrame() >= f;
    /// <summary>shakeCamera(type): Mario on the ground; the reach by the heat; the power by his distance within it.</summary>
    private void ShakeCamera(SmsCameraShakeMode mode)
    {
        if (_mario == null || !_mario.IsOnFloor() || mode == null) return;
        float dist = (MarioPos() - Pos()).Length();
        float ratio = IsBck(BckBark) ? 1f : HpRatio;
        float reach = Prm.ShakeLengthMax * ratio + Prm.ShakeLengthMaxHP0 * (1f - ratio);
        float delta = reach - dist;
        if (delta < 0f) return;
        float power = MathF.Min(delta / reach, 1f);
        SmsCameraShake.Current?.StartMode(mode, power * ratio);
    }
    private void StompEffects()
    {
        Stomps++;
        StompDust(GlobalPosition, new Vector3(4f, 1.4f, 4f));   // wide but low: the dust and the impact streaks hug the ground instead of rising up the body (pass 150c)
        if (RockFxScene != null) SpawnBurst(RockFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
        if (_hp == 0) ChainColdSound?.Play(); else ChainHotSound?.Play();
    }
    private void EmitEffects()
    {
        bool waiting = IsBck(BckWait) || IsBck(BckWait2);
        if ((waiting && BckPassed(_bck, 8f)) || BckPassed(BckShake, 38f)) StompEffects();
        bool spark = BckPassed(BckBark, 72f) || (waiting && (BckPassed(_bck, 6f) || BckPassed(_bck, 12f))) || BckPassed(BckShake, 4f);
        if (spark && SparkFxScene != null) SpawnBurst(SparkFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), (HitUpUnits() + PolarXZ(_yaw, 200f)) / U));   // the HIBANA at the mouth / teeth, not the belly (pass 150c)
        if (waiting && BckPassed(_bck, 10f)) ShakeCamera(ShakeLand1);
        if (BckPassed(BckBark, 60f) || BckPassed(BckBark, 127f)) { ShakeCamera(ShakeLand1); StompEffects(); }
        if (BckPassed(BckBark, 202f)) { ShakeCamera(ShakeLand2); StompEffects(); }
        if (BckPassed(BckShake, 40f)) ShakeCamera(ShakeLand1);
        if ((_hp != 0 || IsBarking) && HeatHazeFxScene != null && (_tickCount & 15) == 0) SpawnBurst(HeatHazeFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale * 3f * MathF.Max(0.3f, HeatRatioNow)), CenterUnits() / U));
        if (_steamPending != 0 && (_tickCount & 3) == 0)   // a steam puff every 4 ticks while it is hosed; clears the flag only when it actually fires, so a spray is never silently dropped (pass 150c)
        {
            if (_hp != 0 && SteamFxScene != null) SpawnBurst(SteamFxScene, new Transform3D(Basis.FromScale(Vector3.One * 2.5f), CenterUnits() / U + Vector3.Up * 2f));
            _steamPending = 0;
        }
        if (_invincible && _goldFrame > 150f && SparkleFxScene != null && (_tickCount & 31) == 0) SpawnBurst(SparkleFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale * 3f), CenterUnits() / U));
    }
    /// <summary>calcRootMatrix: the model's origin (the body's centre) 500 u over the feet, the yaw, the roll about the body's X (the roll joint's).</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Vector3 fwd = PolarXZ(_yaw, 1f), x = Vector3.Up.Cross(fwd).Normalized();
        Basis b = new Basis(x, Vector3.Up, fwd);
        if (RollSw && _rollDeg != 0f) b = b * new Basis(Vector3.Right, Mathf.DegToRad(_rollDeg));
        Vector3 at = GlobalPosition + Vector3.Up * (500f * _scale / U);
        b = b * Basis.FromScale(LookScale * _modelScale);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, at);
        else _model.Transform = new Transform3D(b, Vector3.Zero);
        if (_bodySphere != null && IsInsideTree()) _bodySphere.GlobalTransform = new Transform3D(new Basis(x, Vector3.Up, fwd), GlobalPosition);
    }
    /// <summary>SMSCalcJumpVelocityY from the body point: the ticks to come down to the goal's y at speedY, the XZ spread over them.</summary>
    private Vector3 CalcVelocityToJumpToY(Vector3 goalU, float speedY, float gravity)
    {
        Vector3 pos = Pos();
        float currY = pos.Y, vy = speedY; int ticks = 1;
        for (int guard = 0; guard < 100000; guard++) { currY += vy; if (vy < 0f && currY <= goalU.Y) break; vy -= gravity; if (vy < -40f) vy = -40f; ticks++; }
        return new Vector3((goalU.X - pos.X) / ticks, speedY, (goalU.Z - pos.Z) / ticks);
    }
    /// <summary>The bath's point: BathPoint, else where the Chomp stands (its drop into the water under it).</summary>
    private Vector3 BathUnits() => BathPoint != null ? BathPoint.GlobalPosition * U : Pos();
    /// <summary>Without a BathPoint: dragged (held and pulled) with water under its feet -> the bath is here.</summary>
    private bool OverWater() => WaterSurfaceAt(GlobalPosition + Vector3.Up * 0.3f, out float surf, 0.5f) && (surf - GlobalPosition.Y) * U > -Prm.HeadHeight;

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveWanderB:                                   // TNerveBWGraphWander
                if (_time == 0)
                {
                    if (!IsBck(BckWait) && !IsBck(BckWait2)) SetBck(_hp == 0 ? BckWait2 : BckWait);
                    if (!IsDefeat) SetEyeTexture(0);
                }
                if (_hp == 0 && !IsBck(BckWait2) && BckEnded()) SetBck(BckWait2);
                if (!_staked && IsPicketTaken && IsHeadPulled)
                {
                    // dragged: back past the previous node -> the bath (the goal node), or the rail reversed toward Mario; the slide at 3 x the heat
                    if (NodeCountOnRail > 0 && _pathPrev >= 0)
                    {
                        float dPrev = (NodeUnits(_pathPrev) - Pos()).Length();
                        if (dPrev < 400f && _pathPrev == GoalNodeIndex()) { _bathPos = BathUnits(); PushAfterCurrent(NerveJumpToBath); ControlPull(); return true; }
                        if (dPrev < 400f) ReverseNextGraphNode();
                    }
                    else if (BathPoint != null && (BathPoint.GlobalPosition * U - Pos()).Length() < 400f) { _bathPos = BathUnits(); PushAfterCurrent(NerveJumpToBath); ControlPull(); return true; }
                    else if (BathPoint == null && OverWater()) { _bathPos = Pos(); PushAfterCurrent(NerveJumpToBath); ControlPull(); return true; }
                    SlideToCurPathNode(3f * HpRatio * _marchSpeed, _turnSpeed);
                    ControlPull();
                    return false;
                }
                if (IsReachedToGoal())
                {
                    if (JumpToNextGraphNode() >= 0) { PushAfterCurrent(NerveWanderB); PushAfterCurrent(NerveJumpB); }
                    else { PushAfterCurrent(NerveWanderB); RollNextGraphNode(); }
                    ControlPull();
                    return true;
                }
                if (IsPicketTaken)
                {
                    // his stick against the way from him to it slows the slide (a pull back) or speeds it (a push): 1 - 0.75 x mag x (-diff . his way), 0..1.5
                    Vector3 diff = Pos() - MarioPos(); diff = diff.LengthSquared() > 0f ? diff.Normalized() : Vector3.Zero;
                    Vector3 marioDir = _mario != null ? _mario.SmsPullStickDir : Vector3.Zero;
                    float mag = _mario != null ? _mario.SmsIntendedMag / 32f : 0f;
                    float pullFactor = Mathf.Clamp(1f - mag * 0.75f * -diff.Dot(marioDir), 0f, 1.5f);
                    SlideToCurPathNode(pullFactor * HpRatio * _marchSpeed, _turnSpeed);
                }
                else SlideToCurPathNode(HpRatio * _marchSpeed + 0.2f * SpeedScale, _turnSpeed);
                ControlPull();
                return false;

            case NerveJumpB:                                     // TNerveBWJump
                if (_time == 0)
                {
                    _vel = CalcVelocityToJumpToY(Goal(), JumpSpeedY, GravityY());
                    _airborne = true; Jumps++;
                    if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: JUMP to {Goal()} at {_vel}");
                }
                if (IsReachedToGoal() || (!_airborne && _time > 4)) { PushAfterCurrent(NerveWanderB); ControlPull(); return true; }
                WalkToCurPathNode(0f, _turnSpeed, 0f);
                ControlPull();
                return false;

            case NerveBark:                                      // TNerveBWBark
                if (_time == 0)
                {
                    SetBck(BckBark); _rollDeg = 0f; Barks++;
                    if (!_neverPulled && _staked)
                    {
                        ReleasePicket();
                        _staked = false;
                        Escape1Sound?.Play();
                        if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: BARK -- the stake pulled out");
                    }
                }
                if (_time == 280) { _hp = Prm.BWHitPointMax; if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: BARK -- hot again"); }
                if (BckEnded()) { PushAfterCurrent(NerveWanderB); ControlPull(); return true; }
                ControlPull();
                return false;

            case NerveShake:                                     // TNerveBWShake
                if (_time == 0) { SetBck(BckShake); Shakes++; }
                if (BckEnded())
                {
                    ReleasePicket();
                    _neverPulled = false; _staked = false;
                    Escape2Sound?.Play();
                    if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: SHAKE -- the stake pulled out");
                    ControlPull();
                    return true;
                }
                ControlPull();
                return false;

            case NerveJumpToBath:                                // TNerveBWJumpToBath
                if (_time == 0 && _bathHops == 0)
                {
                    _vel = CalcVelocityToJumpToY(_bathPos, 10f, GravityY());
                    SetGoalPoint(_bathPos);
                    _airborne = true; _waterSpawned = false;
                    if (_leashReady && _leash.Picket.IsHeld) _leash.Picket.BehaveApart();
                    if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: JUMPTOBATH from {Pos()} to {_bathPos} at {_vel}");
                }
                if (_time > 120 && !_waterSpawned && Pos().Y < _bathPos.Y + 10f)
                {
                    if (SplashFxScene != null) SpawnBurst(SplashFxScene, new Transform3D(Basis.FromScale(Vector3.One * 5f), (Pos() + Vector3.Up * 470f) / U));
                    DiveSound?.Play();
                    _waterSpawned = true;
                }
                if (DebugPrint && (_time == 1 || _time % 40 == 0)) GD.Print($"[SMS bwanwan] {Name}: JUMPTOBATH t{_time} at {Pos()} vel {_vel} lin {_lin} air {_airborne} floor {IsOnFloor()} hops {_bathHops}");
                {
                    Vector3 diff = _bathPos - Pos();
                    float dxz = new Vector2(diff.X, diff.Z).Length();
                    bool there = (diff.LengthSquared() < 10000f && Pos().Y <= _bathPos.Y) || (!_airborne && _time > 10 && dxz < 300f);
                    if (there)
                    {
                        if (diff.LengthSquared() < 10000f) GlobalPosition = _bathPos / U; else GlobalPosition = new Vector3(_bathPos.X / U, GlobalPosition.Y, _bathPos.Z / U);
                        if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: at the bath after {_time} ticks and {_bathHops + 1} hop(s): {Pos()}");
                        PushAfterCurrent(NerveDieB); _pullDelta = Vector3.Zero; return true;
                    }
                    if (!_airborne && _time > 10 && _bathHops < 3)
                    {
                        // DEVIATION: the game's bath is a pit the one leap always ends in; the port's is a point on whatever floor the level has, so a
                        // leap that lands short (a slope, a ledge, a landing slide) hops again from where it came down, up to three times
                        _bathHops++;
                        _vel = CalcVelocityToJumpToY(_bathPos, 10f, GravityY()); _airborne = true;
                        if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: landed {dxz:F0} u short of the bath after {_time} ticks -- hop {_bathHops} again at {_vel}");
                        _time = 0;
                    }
                    else if (!_airborne && _time > 10)
                    {
                        if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: still {dxz:F0} u from the bath after three hops: the bath is here");
                        _bathPos = Pos();
                        PushAfterCurrent(NerveDieB); _pullDelta = Vector3.Zero; return true;
                    }
                }
                WalkToCurPathNode(0f, _turnSpeed, 0f);
                _pullDelta = Vector3.Zero;
                return false;

            case NerveDieB:                                      // TNerveBWDie
                if (!_dieEntered)
                {
                    _dieEntered = true;
                    _vel = Vector3.Zero; _lin = Vector3.Zero; _noBind = true; _airborne = false;
                    if (SteamFxScene != null) SpawnBurst(SteamFxScene, new Transform3D(Basis.FromScale(Vector3.One * 5f), GlobalPosition));
                }
                if (_hp != 0 && !_dieStarted) { _hp--; _time = -1; return false; }       // one hit point off a tick (the game pushes Die again)
                if (!_dieStarted)
                {
                    _dieStarted = true; _rollDeg = 0f; _staked = false; _pullDelta = Vector3.Zero;
                    // takeBath: invincible, the body at the bath, the collision model up (x 1.1), the hits off, the leash's and the picket's collision off
                    _invincible = true;
                    if (_bodySphere != null) _bodySphere.SetScale(1.1f);
                    if (_leashReady) { _leash.Picket.Disabled = true; if (_leash.Picket.IsHeld) _leash.Picket.BehaveApart(); }
                    SetBck(BckDown); SetEyeTexture(0);
                    _goldFrame = 0f;
                    if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: DIE -- in the bath, the gold brk");
                }
                if (_time > 60 && !_shineDone) SpawnShine();
                _lin = Vector3.Zero; _vel = Vector3.Zero;
                return false;                                    // for good: isDefeat
        }
        return base.ExecuteNerve(n);
    }
    /// <summary>makeShineAppearWithDemo (the boss shine): the ShineScene at the ShineSpawnPoint (default: 1000 u over the bath), its spawn entrance.</summary>
    private void SpawnShine()
    {
        _shineDone = true;
        if (ShineScene == null) return;
        var shine = ShineScene.Instantiate<Node3D>();
        GetParent().AddChild(shine);
        shine.GlobalPosition = ShineSpawnPoint != null ? ShineSpawnPoint.GlobalPosition : GlobalPosition + Vector3.Up * (1000f / U);
        if (shine is ShineSprite s) s.PlaySpawnEntrance();
        if (DebugPrint) GD.Print($"[SMS bwanwan] {Name}: the Shine at {shine.GlobalPosition}");
    }
}

/// <summary>
/// The Chain Chomp's body (pass 150): a StaticBody3D of the game's two receive cylinders -- 270 u by 500 from the feet (TBWHit 1) and 450 u by 500
/// from the centre up (TBWHit 0) -- that the hose's drops hit (<see cref="ISmsSprayTarget"/>, forwarded to the Chomp) and Mario's body meets (the
/// game's keepDistance: he can stand under the overhang up to 270 u from the axis, where the 300-u attack cylinder burns him); in the bath both
/// are scaled 1.1 (the game's bwanwan_ofuro_col.col) and he climbs them. Carried by the Chomp at its feet; the Chomp itself ignores it.
/// </summary>
public partial class SmsBossWanwanBody : StaticBody3D, ISmsSprayTarget
{
    private const float U = SmsUnits.UnitsPerMetre;
    private SmsBossWanwan _owner; private CollisionShape3D _low, _up; private CylinderShape3D _lowC, _upC;
    private float _k = 1f;
    public bool Enabled { get => _low != null && !_low.Disabled; set { if (_low != null) _low.Disabled = !value; if (_up != null) _up.Disabled = !value; } }
    public void Setup(SmsBossWanwan owner)
    {
        _owner = owner;
        TopLevel = true;
        CollisionLayer = 1; CollisionMask = 0;
        _lowC = new CylinderShape3D(); _upC = new CylinderShape3D();
        _low = new CollisionShape3D { Shape = _lowC, Name = "LowCylinder" }; _up = new CollisionShape3D { Shape = _upC, Name = "UpCylinder" };
        AddChild(_low); AddChild(_up);
        SetScale(1f);
    }
    /// <summary>The cylinders at k x the game's: 270 x 500 from the feet, 450 x 500 from the centre (takeBath: 1.1).</summary>
    public void SetScale(float k)
    {
        _k = k;
        _lowC.Radius = 270f * k / U; _lowC.Height = 500f * k / U; _low.Position = new Vector3(0f, 250f * k / U, 0f);
        _upC.Radius = 450f * k / U; _upC.Height = 500f * k / U; _up.Position = new Vector3(0f, 750f * k / U, 0f);
    }
    public float ScaleNow => _k;
    public bool SmsSprayedByWater(Vector3 hitPos) => _owner != null && IsInstanceValid(_owner) && _owner.SmsSprayedByWater(hitPos);
}
