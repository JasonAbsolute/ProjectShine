using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS 232: the HOT-SPRING BUBBLE (TBubble, bosstelesa.cpp 80-395) -- what King Boo spits on a MIXED slot result (TBossTelesa::generateSlotItem: the three
/// drums on three different faces -> result -1 -> SlotItemNum (6) x 2 = twelve of manNameTable[0], the bubble manager). Dead code in the shipped game: its slot's
/// HitCollectRate 1.0 holds every drum to the first drum's target, so the three never differ -- the bubble was built, parametered (map/params/enemy/bubble.prm
/// in delfinoBoss.szs) and never seen. Here it is the Mixed round of the port's casino fight (ForceResult Mixed, or a HitCollectRate knob under 1).
/// <para>THE GAME'S RULES, ported: twelve bubbles out of the mouth on the fan (24 degrees apart from -60: the second six go on round to his back -- the game kept
/// slotItemNum's step for the doubled count), each at 5-10 u a tick along it. A bubble FLOATS: TBubble::calcRootMatrix pins its centre FloatHeightUnits (150)
/// over the floor under it plus a rise, unk1CC -- 0 for a spat one, growing 1 u a tick to AddPosBaseUnits (200): it rises from 2 m to 4.7 m over the table in
/// its first 200 ticks -- plus the wait clip's bob. It GROWS: the body scale (BodyScaleLow / High 1.5..1.8 at birth) x RateExpand (1.001) a tick to MaxScale
/// (2.0). It never lands (the game's mGravity 0.15 a tick never brings the pinned centre down to the floor, so LIVE_FLAG_AIRBORNE stays and the spit velocity is
/// never zeroed: the game's bubble would slide at the spit speed until the room's wall -- see SpitDrag). After LiveTime (1000) ticks it SPLITS: NumDivision (3)
/// children at its point (unk1CC higher), each with a random velocity of +-2 u a tick on every axis, their own fresh body scale, no rise; a child (unk1D0) walks
/// toward Mario at 0.8 x its march for its first 40 ticks, coasts on its split velocity x 0.98 a tick to tick 80, is stopped there, and walks at Mario from then
/// on (zigzagToCurPathNode with no graph: the plain walk). The pool (the manager's ActiveEnemyNum 30, MaxAlive here) caps the split: a bubble that splits with
/// the pool full makes nothing and dies. The split itself: the split clip (2 frames: a swell to 1.3) from its tenth tick, kill() when it ends. TOUCHED by
/// Mario (the attack cylinder, AttackRadius 90 x AttackHeight 120 x the scale): sendAttackMsgToMario (the common enemy damage) and kill. HOSED while it waits
/// (TBubble::behaveToWater: the Live nerve on the wait clip): kill, and a LARGE WATER BOTTLE (ACTOR_TYPE_BOTTLE_LARGE: a full refill) appears, alive 0x4B0 =
/// 1200 ticks. Jumped on (TRAMPLE / HIP_DROP, the base's receiveMessage): kill, nothing dropped. The death: the split clip's swell, the common puff.
/// The bubble has no sounds (TBubble::getBasNameTable is King Boo's table, which has no entries for the bubble clips). NOT ported: TBossTelesaKillSmallEnemy's
/// cylinder (400 x 300 at the table's middle, every HIT_CATEGORY_ENEMY body in it killed) -- a bubble is one (TWalkerEnemy::init's categories), and the game
/// pins a spat one to its float height at the table's middle on its first frame, inside that cylinder: the game's twelve would have died where they were born.
/// SmsBossTelesa.KillSmallEnemies lets the bubbles through (the one thing that lets the Mixed round exist).</para>
/// <para>THE LOOK: btelesa_osenbubbles_ind.bmd -- a dome of 79 vertices (a half-sphere 143 u across, its flat face at z 0, the dome toward +z) on one joint scaled
/// 1.43, drawn with _mat_osenbubbles_ind (OsenBubble.gdshader: the picture behind it refracted and remapped lavender / dark red, the rim alpha, a highlight).
/// Three bck clips on the joint, replayed here from their keys (no AnimationPlayer: one joint): birth (60 frames: the dome blows up from nothing with a jelly
/// wobble -- x and y overshoot in turn, z fills in ten frames), wait (40 frames, looped: a squash-and-stretch of 0.87..1.23 against 1.23..0.87 and a bob of
/// +-10 u), split (2 frames: 1 -> 1.3). The game's bck clock: 60 frames a second here (the port's 2 ticks a frame).</para>
/// <para>DEVIATIONS, every one a knob with the game's value as its off state: SpitSinkSeconds -- the game PINS the spat bubble to its float height on its first
/// frame (from 15 m up at the mouth: a jump cut); here it sinks there over SpitSinkSeconds. SpitDrag -- the game's spat bubble never slows (airborne for ever)
/// and slides to the wall; here the spit velocity decays by SpitDrag a tick, so the twelve spread 5-9 m from under his mouth and hang there. FaceCamera -- the
/// dome turned to the camera (the game turns it with the walker's heading: a half-shell from the side). BottleOnFloor -- the bottle at the floor under the
/// bubble (the game's appears 50 u over the bubble's centre and hangs there, 2-5 m up). Refraction (the shader) -- the screen behind the bubble (the material's
/// intent; the game's texture swap misses by name and leaves the dummy's flat blue).</para>
/// Knobs: The bubble (bubble.prm's six, FloatHeightUnits, MaxAlive), Deviations, The bottle, Look. Readouts for the drives: IsChild, Generation, RiseUnits,
/// SpitVelUnits, FloatY, Alive (static), Splits, the clip.
/// </summary>
[GlobalClass]
public partial class SmsOsenBubble : SmsSmallEnemy
{
    // ---- map/params/enemy/bubble.prm (delfinoBoss.szs), read 232 (tools/projectshine/npc82/enemy128/prm_file.py)
    private static readonly Params Prm = new()
    {
        HeadHeight = 120f, BodyRadius = 30f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 100, ActiveEnemyNum = 30,
        Gravity = 0.15f, JumpForce = 10f, SearchLength = 800f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 2000f, GiveUpHeight = 650f,
        AttackWait = 120, FreezeWait = 300, DamageRadius = 80f, DamageHeight = 140f, AttackRadius = 90f, AttackHeight = 120f,
        TurnSpeedLow = 3f, TurnSpeedHigh = 5f, BodyScaleLow = 1.5f, BodyScaleHigh = 1.8f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 5f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 0.8f, MarchSpeedHigh = 1.5f,
    };
    protected override Params P => Prm;

    [ExportGroup("The bubble (bubble.prm: TBubbleSaveLoadParams)")]
    /// <summary>mSLLiveTime: ticks afloat before the split (1000 = 8.3 s).</summary>
    [Export(PropertyHint.Range, "60,6000,10")] public int LiveTime = 1000;
    /// <summary>mSLNumDivision: the children a split makes (while the pool has room).</summary>
    [Export(PropertyHint.Range, "0,8,1")] public int NumDivision = 3;
    /// <summary>mSLMaxScale: the body scale the growth stops at (from BodyScaleLow / High 1.5..1.8 at birth).</summary>
    [Export(PropertyHint.Range, "1,4,0.05")] public float MaxScale = 2f;
    /// <summary>mSLAddPosBase: a spat bubble's rise over its first float height, units (1 u a tick: 200 u in 200 ticks).</summary>
    [Export(PropertyHint.Range, "0,600,10")] public float AddPosBaseUnits = 200f;
    /// <summary>mSLRateExpand: the scale x this a tick while under MaxScale (1.001: 1.5 to 2 in 288 ticks, 2.4 s).</summary>
    [Export(PropertyHint.Range, "1,1.02,0.0005")] public float RateExpand = 1.001f;
    /// <summary>mSLDeadHeight: the game's `unk1CC > y + DeadHeight -> kill` -- never true over a table (kept for the record; 0 = off).</summary>
    [Export(PropertyHint.Range, "0,2000,10")] public float DeadHeightUnits = 200f;
    /// <summary>calcRootMatrix's 150: the bubble's centre this high over the floor under it (plus the rise, plus the wait clip's bob).</summary>
    [Export(PropertyHint.Range, "0,600,10")] public float FloatHeightUnits = 150f;
    /// <summary>The manager's pool (mSLActiveEnemyNum 30): no more than this many bubbles alive at once -- a split or a spit with the pool full makes nothing.</summary>
    [Export(PropertyHint.Range, "1,100,1")] public int MaxAlive = 30;
    [ExportGroup("Deviations (0 / off = the game)")]
    /// <summary>A spat bubble sinks from the mouth to its float height over this long (eased). 0 = the game: pinned to the float height on its first frame.</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float SpitSinkSeconds = 0.8f;
    /// <summary>The spit velocity x this a tick (0.985: 5-10 u a tick come to rest 330-670 u on, 4.5-9 m). 1 = the game: never slowed, it slides to the wall.</summary>
    [Export(PropertyHint.Range, "0.9,1,0.001")] public float SpitDrag = 0.985f;
    /// <summary>The dome turned to face the camera. Off = the game: the walker's heading (the spit's, then the walk's toward Mario).</summary>
    [Export] public bool FaceCamera = true;
    [ExportGroup("The bottle (hosed while it waits)")]
    /// <summary>ACTOR_TYPE_BOTTLE_LARGE: the full-refill water bottle. Empty = res://assets/Sms/Items/WaterBottle.tscn with FullRefill on. A scene that is not an
    /// SmsWaterBottle is placed as it is.</summary>
    [Export] public PackedScene BottleScene;
    /// <summary>killByTimer(0x4B0): the bottle's takeable window, ticks.</summary>
    [Export(PropertyHint.Range, "60,6000,10")] public int BottleLifeTicks = 1200;
    /// <summary>The bottle at the FLOOR under the bubble. Off = the game: 50 u over the bubble's centre, where it floated (2-5 m up, hanging).</summary>
    [Export] public bool BottleOnFloor = true;
    [ExportGroup("Look")]
    /// <summary>The screen behind the bubble, refracted (the material's intent). Off = the game's picture: the dummy texture's flat blue in the remap.</summary>
    [Export] public bool Refraction = true;
    [Export(PropertyHint.Range, "0,0.1,0.001")] public float RefractionUv = 0.015f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Opacity = 1f;
    [Export] public Shader BubbleShader;
    [Export] public Texture2D BubbleTex, SpeTex, IndTex;

    // ---- the clips (btelesa_osenbubbles_birth / wait / split: the joint's scale and y, per bck frame; tools/projectshine/npc82/sirena151/bubble_keys232.py)
    public enum Clip { None = -1, Birth = 0, Split = 1, Wait = 2 }
    private static readonly float[] BirthSX = { 0.000f, 0.004f, 0.015f, 0.032f, 0.058f, 0.092f, 0.135f, 0.189f, 0.253f, 0.330f, 0.419f, 0.521f, 0.636f, 0.756f, 0.874f, 0.985f, 1.084f, 1.166f, 1.225f, 1.262f, 1.281f, 1.283f, 1.270f, 1.241f, 1.198f, 1.144f, 1.090f, 1.039f, 0.994f, 0.955f, 0.925f, 0.904f, 0.894f, 0.898f, 0.916f, 0.948f, 0.984f, 1.021f, 1.058f, 1.094f, 1.126f, 1.155f, 1.178f, 1.194f, 1.202f, 1.201f, 1.191f, 1.175f, 1.154f, 1.130f, 1.105f, 1.079f, 1.056f, 1.035f, 1.020f, 1.011f, 1.005f, 1.001f, 1.000f, 0.999f, 1.000f };
    private static readonly float[] BirthSY = { 0.000f, 0.005f, 0.018f, 0.037f, 0.062f, 0.093f, 0.129f, 0.170f, 0.215f, 0.265f, 0.318f, 0.375f, 0.435f, 0.497f, 0.562f, 0.628f, 0.695f, 0.764f, 0.833f, 0.902f, 0.972f, 1.039f, 1.097f, 1.145f, 1.183f, 1.212f, 1.233f, 1.245f, 1.249f, 1.246f, 1.236f, 1.220f, 1.197f, 1.168f, 1.135f, 1.102f, 1.070f, 1.041f, 1.017f, 0.997f, 0.985f, 0.981f, 0.987f, 1.003f, 1.032f, 1.068f, 1.105f, 1.141f, 1.171f, 1.194f, 1.206f, 1.204f, 1.191f, 1.172f, 1.148f, 1.121f, 1.094f, 1.067f, 1.042f, 1.021f, 1.007f };
    private static readonly float[] BirthSZ = { 0.000f, 0.040f, 0.123f, 0.240f, 0.383f, 0.538f, 0.689f, 0.822f, 0.927f, 0.989f, 1.004f, 1.008f, 1.009f, 1.009f, 1.007f, 1.004f, 1.002f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f, 1.000f };
    private static readonly float[] SplitS = { 1.000f, 1.124f, 1.300f };
    private static readonly float[] WaitTY = { -1.29f, -2.64f, -3.99f, -5.30f, -6.53f, -7.64f, -8.58f, -9.31f, -9.80f, -9.99f, -9.86f, -9.47f, -8.84f, -8.01f, -7.00f, -5.85f, -4.58f, -3.22f, -1.79f, -0.34f, 1.13f, 2.57f, 3.97f, 5.28f, 6.49f, 7.57f, 8.48f, 9.21f, 9.71f, 9.97f, 9.95f, 9.57f, 8.88f, 7.94f, 6.80f, 5.51f, 4.14f, 2.72f, 1.32f, -0.00f, -0.63f };
    private static readonly float[] WaitSX = { 0.900f, 0.885f, 0.875f, 0.870f, 0.871f, 0.876f, 0.885f, 0.898f, 0.915f, 0.935f, 0.957f, 0.981f, 1.007f, 1.034f, 1.060f, 1.086f, 1.111f, 1.135f, 1.157f, 1.177f, 1.194f, 1.208f, 1.219f, 1.227f, 1.229f, 1.228f, 1.221f, 1.208f, 1.190f, 1.170f, 1.147f, 1.122f, 1.097f, 1.070f, 1.043f, 1.017f, 0.991f, 0.965f, 0.942f, 0.920f, 0.910f };
    private static readonly float[] WaitSY = { 1.200f, 1.214f, 1.223f, 1.227f, 1.226f, 1.221f, 1.213f, 1.201f, 1.185f, 1.167f, 1.146f, 1.122f, 1.097f, 1.071f, 1.045f, 1.019f, 0.993f, 0.968f, 0.945f, 0.925f, 0.906f, 0.891f, 0.879f, 0.872f, 0.869f, 0.870f, 0.878f, 0.891f, 0.910f, 0.932f, 0.956f, 0.981f, 1.008f, 1.035f, 1.061f, 1.088f, 1.114f, 1.138f, 1.161f, 1.182f, 1.191f };
    private static readonly int[] ClipFrames = { 60, 2, 40 };
    private static readonly bool[] ClipLoops = { false, false, true };
    protected override string[] BckNames => System.Array.Empty<string>();  // the clips are replayed from their keys (one joint)
    protected override int[] BckFrames => System.Array.Empty<int>();
    protected override bool[] BckLoops => System.Array.Empty<bool>();

    // ---- the nerves
    private const int NerveBubbleLive = NerveSpecies, NerveBubbleSplit = NerveSpecies + 1;
    protected override int DefaultNerve => NerveBubbleLive;
    protected override string NerveName(int n) => n == NerveBubbleLive ? "BubbleLive" : n == NerveBubbleSplit ? "BubbleSplit" : base.NerveName(n);

    // ---- state
    public static readonly List<SmsOsenBubble> All = new();
    /// <summary>The bubbles alive now (the pool's count).</summary>
    public static int Alive { get { int n = 0; foreach (var b in All) if (b != null && GodotObject.IsInstanceValid(b) && !b.IsDead) n++; return n; } }
    /// <summary>TEnemyManager's pool has room for one more (max = the scene's MaxAlive, read once by the spawner).</summary>
    public static bool PoolFree(int max) => Alive < max;
    private bool _child;                 // unk1D0: a split's child
    private bool _coasting = true;       // unk1D1: the child's split velocity still carries it (x 0.98 a tick past tick 40; off at 80)
    private float _rise;                 // unk1CC: the rise over the float height, units
    private Vector3 _spitVel;            // the spit's velocity (units a tick), decaying by SpitDrag
    private float _sinkFrom = float.NaN, _sinkT;   // SpitSinkSeconds: the mouth's height to sink from, the clock (ticks)
    private Clip _clip = Clip.None; private float _clipFrame;
    private Vector3 _clipScale = Vector3.One; private float _clipBob;
    private readonly List<ShaderMaterial> _mats = new();
    private bool _popped;
    private SmsWaterBottle _lastBottle;
    private float _floorY;               // mGroundHeight: the MAP under the bubble (BubbleGroundY -- the base's _groundY reads bodies too)
    private string _why;                 // the kill's reason, for the debug print
    public static int SplitsTotal { get; private set; }
    public static int ChildrenTotal { get; private set; }
    public bool IsChild => _child;
    public int Generation { get; private set; }
    public float RiseUnits => _rise;
    public Vector3 SpitVelUnits => _spitVel;
    public float FloatY => _floorY + FloatHeightUnits + _rise;
    public float FloorY => _floorY;
    public int Splits { get; private set; }
    public int ChildrenMade { get; private set; }
    public Clip ClipNow => _clip;
    public float ClipFrameNow => _clipFrame;
    public bool Sinking => !float.IsNaN(_sinkFrom);
    public Node3D LastBottle => _lastBottle != null && IsInstanceValid(_lastBottle) ? _lastBottle : null;
    public static int BottlesDropped { get; private set; }
    public string DebugState => $"{NerveNameNow} t{NerveTime} clip {_clip} f{_clipFrame:F0} child {_child} gen {Generation} scale {_scale:F2} rise {_rise:F0} y {Pos().Y:F0} floor {_floorY:F0} spit {_spitVel.Length():F1} u/t sink {Sinking} hp {_hp} dead {_dead} alive {Alive}";

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    protected override bool FlyBind => true;
    /// <summary>TBubble::init: an effect object (no shadow, the collision's hit categories the base's), unk150 0x11; the dome's material.</summary>
    protected override void Init()
    {
        _flags150 = 0x11;
        All.Add(this);
        BottleScene ??= LoadIf("res://assets/Sms/Items/WaterBottle.tscn");
        BubbleShader ??= ResourceLoader.Exists("res://assets/Sms/Enemy/BossTelesa/OsenBubble.gdshader") ? GD.Load<Shader>("res://assets/Sms/Enemy/BossTelesa/OsenBubble.gdshader") : null;
        BubbleTex ??= LoadTex("res://models/Eniemes/BossTelesa/bubble/N_btelesa_bubble.png");
        SpeTex ??= LoadTex("res://models/Eniemes/BossTelesa/bubble/N_btelesa_bubble_spe.png");
        IndTex ??= LoadTex("res://models/Eniemes/BossTelesa/bubble/N_btelesa_bubble_ind.png");
        if (_model != null && _anim == null) { _anim = new AnimationPlayer { Name = "AnimationPlayer" }; _model.AddChild(_anim); }   // the base wants one; the clips are this script's own
        // ACTOR_TYPE_EFFECT_OBJ: no body pushes between actors (the game's are messages, and the bubble answers none) -- the twelve spat together, the three
        // children born at one point and the field round his table would otherwise block one another's walk and sit on his body
        foreach (Node n in GetTree().GetNodesInGroup("sms_enemy")) if (n is PhysicsBody3D pb && pb != this) { AddCollisionExceptionWith(pb); pb.AddCollisionExceptionWith(this); }
        DressModel();
    }
    public override void _ExitTree() { All.Remove(this); }
    private static PackedScene LoadIf(string path) => ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
    private static Texture2D LoadTex(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    /// <summary>_mat_osenbubbles_ind onto every surface of the dome (one per body: the opacity is its own), cast no shadow (LIVE_FLAG_NO_SHADOW).</summary>
    private void DressModel()
    {
        _mats.Clear();
        if (_model == null || BubbleShader == null) return;
        foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            if (mi.Mesh == null) continue;
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var sm = new ShaderMaterial { Shader = BubbleShader };
                if (BubbleTex != null) sm.SetShaderParameter("bubble_tex", BubbleTex);
                if (SpeTex != null) sm.SetShaderParameter("spe_tex", SpeTex);
                if (IndTex != null) sm.SetShaderParameter("ind_tex", IndTex);
                sm.SetShaderParameter("refraction", Refraction); sm.SetShaderParameter("refraction_uv", RefractionUv); sm.SetShaderParameter("opacity", Opacity);
                mi.SetSurfaceOverrideMaterial(s, sm);
                _mats.Add(sm);
            }
        }
    }
    public int MaterialCount => _mats.Count;
    /// <summary>The Refraction knob, live (the drive's shot of the game's flat blue).</summary>
    public void SetRefraction(bool on) { Refraction = on; foreach (var m in _mats) m.SetShaderParameter("refraction", on); }

    /// <summary>TBubble::reset: no shadow, unk1CC a random 50..150 (a PLACED bubble's rise; a spat one starts at 0: SmsSpitFrom), not a child, coasting on, nothing
    /// held; the walker's reset (the scale 1.5..1.8, the turn and march speeds, the hit points) through the base.</summary>
    protected override void Reset()
    {
        base.Reset();
        _noBind = true;
        MotionMode = MotionModeEnum.Floating;
        _rise = Lerp(50f, 150f, Rand()); _child = false; _coasting = true; _spitVel = Vector3.Zero; _sinkFrom = float.NaN; _sinkT = 0f; _popped = false;
        _clip = Clip.None; _clipFrame = 0f; _clipScale = Vector3.One; _clipBob = 0f;
        _airborne = true; _vel = Vector3.Zero; _why = null;
        _floorY = IsInsideTree() ? BubbleGroundY() : Pos().Y - FloatHeightUnits;
        SetY(FloatY);
        ApplyLook();
    }
    /// <summary>TBossTelesa::generateSlotItem's bubble: at the mouth (- 250 + 10 u), the fan velocity (units a tick), LIVE_FLAG_AIRBORNE, no reset (the game's
    /// makeOneEnemyAppear leaves unk1CC at 0: the rise from the float height). The sink from the mouth's height (SpitSinkSeconds) starts here.</summary>
    public void SmsSpitFrom(Vector3 globalPosition, float yawDeg, Vector3 velocityUnitsPerTick)
    {
        SmsPlace(globalPosition, yawDeg);
        _rise = 0f; Generation = 0; _child = false;
        _spitVel = new Vector3(velocityUnitsPerTick.X, 0f, velocityUnitsPerTick.Z);
        if (SpitSinkSeconds > 0f) { _sinkFrom = globalPosition.Y * U; _sinkT = 0f; SetY(_sinkFrom); }
        else SetY(FloatY);
        if (DebugPrint) GD.Print($"[SMS bubble] {Name}: spat at {globalPosition} along {_spitVel.Length():F1} u/t (yaw {yawDeg:F0}), the float height {FloatY / U:F2} m");
    }
    /// <summary>TBubble::split's child: at the parent's point unk1CC up, a random velocity of +-2 u a tick on each axis (its y is pinned: the x / z carry), no rise,
    /// the walker's reset (a fresh body scale).</summary>
    public void SmsSplitChild(Vector3 globalPosition, Vector3 velocityUnitsPerTick, int generation)
    {
        SmsPlace(globalPosition, _yaw);
        _rise = 0f; _child = true; _coasting = true; Generation = generation;
        _spitVel = new Vector3(velocityUnitsPerTick.X, 0f, velocityUnitsPerTick.Z);
        SetY(FloatY);
    }
    private void SetY(float yUnits) => GlobalPosition = new Vector3(GlobalPosition.X, yUnits / U, GlobalPosition.Z);
    /// <summary>mGroundHeight for a bubble: the MAP under it (the game's checkGround is a bg check). The base's ray takes the first body it meets -- another
    /// bubble's (the twelve spat together, three children born at one point) or his -- and the bubbles would stack skyward on one another's tops, each
    /// tick higher; here a body that is an actor (a CharacterBody3D or a RigidBody3D: the enemies, Mario, the fruit) is cast through.</summary>
    private float BubbleGroundY()
    {
        if (!IsInsideTree()) return _floorY;
        var space = GetWorld3D().DirectSpaceState;
        Vector3 from = GlobalPosition + Vector3.Up * (P.HeadHeight / U), to = GlobalPosition + Vector3.Down * 60f;
        var excl = new Godot.Collections.Array<Rid> { GetRid() };
        if (_mario != null && IsInstanceValid(_mario)) excl.Add(_mario.GetRid());
        for (int pass = 0; pass < 12; pass++)
        {
            var q = PhysicsRayQueryParameters3D.Create(from, to, CollisionMask); q.Exclude = excl;
            var hit = space.IntersectRay(q);
            if (hit.Count == 0) return Pos().Y - 60f * U;
            if (hit["collider"].As<GodotObject>() is CollisionObject3D co && (co is CharacterBody3D || co is RigidBody3D)) { excl.Add(co.GetRid()); continue; }
            return ((Vector3)hit["position"]).Y * U;
        }
        return _floorY;
    }
    protected override void MoveObjectPre() { _noBind = true; _floorY = BubbleGroundY(); }
    protected override bool IsFindMario(float k) => false;
    protected override bool IsCollidMove(Node3D other) => false;                 // an effect object: the pushes are off (the game's EFFECT_OBJ type); the bubbles pass through one another
    protected override float GravityY() => 0f;                                     // the height is pinned (TBubble::calcRootMatrix): the game's gravity never reached the floor
    protected override bool WallHopAllowed() => false;
    protected override void LaunchVelocity() { }

    /// <summary>TBubble::attackToMario: the message (the common damage), then kill -- no post-attack hop.</summary>
    protected override void AttackToMario()
    {
        if (_cur == NerveDie || _dead) return;
        SendAttackMsgToMario();
        _why = "touched Mario (attackToMario)"; Kill();
    }
    /// <summary>TBubble::behaveToWater: in the Live nerve on the wait clip -> kill, and the large water bottle (50 u over the centre; BottleOnFloor: at the floor
    /// under it), alive BottleLifeTicks. Hosed through its birth, or while splitting: nothing but the water mark (decHpByWater still floors the hit points).</summary>
    protected override void BehaveToWater()
    {
        if (!IsNerve(NerveBubbleLive) || _clip != Clip.Wait) return;
        _why = "hosed (behaveToWater)"; Kill();
        DropBottle();
    }
    private void DropBottle()
    {
        if (BottleScene == null) { GD.PushWarning($"[SMS bubble] {GetPath()}: no BottleScene -- hosed, nothing dropped"); return; }
        Vector3 at = BottleOnFloor ? new Vector3(GlobalPosition.X, (_floorY + 1f) / U, GlobalPosition.Z) : GlobalPosition + Vector3.Up * (50f / U);
        var node = BottleScene.Instantiate<Node3D>();
        if (node is SmsWaterBottle wb) { wb.FullRefill = true; wb.Placed = false; wb.LifeTicksOverride = BottleLifeTicks; _lastBottle = wb; }
        GetParent().AddChild(node);
        node.GlobalPosition = at;
        BottlesDropped++;
        if (DebugPrint) GD.Print($"[SMS bubble] {Name}: hosed -- popped, a large water bottle at {at} for {BottleLifeTicks} ticks");
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveBubbleLive:                                // TNerveBubbleLive
                if (_time == 0)
                {
                    SetCollisionLayerValue(1, true);             // offHitFilter(NO_COLLISION)
                    if (!_child) PlayClip(Clip.Birth, Lerp(0f, 20f, Rand()));   // setBckAnm(8) from a random frame 0..20
                    else { PlayClip(Clip.Wait, Lerp(0f, 20f, Rand())); SetGoalMario(); }
                }
                else if (_clip == Clip.Birth && ClipEnded()) { SetCollisionLayerValue(1, true); PlayClip(Clip.Wait, 0f); }
                if (!_child) { if (AddPosBaseUnits > _rise) _rise += 1f; }
                else
                {
                    if (_time > 40 && _coasting) _spitVel *= 0.98f;
                    else WalkBehavior(0, 0.8f);
                    if (_time == 80) { _coasting = false; _spitVel = Vector3.Zero; }
                }
                _rise += 0.001f;
                if (DeadHeightUnits > 0f && _rise > Pos().Y + DeadHeightUnits) { _why = "fell past DeadHeight"; Kill(); return false; }
                if (_scale < MaxScale) { _scale = MathF.Min(MaxScale, _scale * RateExpand); _bodyScale = _scale; }   // mScaling grows: the hit cylinders with it
                if (_time > LiveTime) { PushAfterCurrent(NerveBubbleSplit); return true; }
                return false;

            case NerveBubbleSplit:                               // TNerveBubbleSplit
                if (_time == 0) { SetCollisionLayerValue(1, false); Split(); }
                if (_time == 10) PlayClip(Clip.Split, 0f);
                if (_clip == Clip.Split && ClipEnded()) { _why = "the split's end"; Kill(); return false; }
                return false;

            case NerveDie:                                       // TNerveSmallEnemyDie, the bubble's: the split clip's swell (setDeadAnm = clip 9), then the puff and gone
                if (_time == 0)
                {
                    _hp = 0;
                    SetCollisionLayerValue(1, false);
                    PlayClip(Clip.Split, 0f);
                }
                if (ClipEnded() || _time > 60)
                {
                    if (!_popped) { _popped = true; GenRandomItem(); }
                    Dead();
                    return true;
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }
    /// <summary>TBubble::split: NumDivision children at this point (unk1CC higher) with random velocities, while the pool has room (makeOneEnemyAppear null = stop).</summary>
    /// <summary>HIT_MESSAGE_SPRAYED_BY_WATER, logged.</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (DebugPrint && !_dead) GD.Print($"[SMS bubble] {Name}: sprayed at {hitPos} -- {DebugState}");
        return base.SmsSprayedByWater(hitPos);
    }
    /// <summary>TSmallEnemy::kill, logged: who popped it.</summary>
    public override void Kill()
    {
        if (!_dead && _cur != NerveDie && DebugPrint) GD.Print($"[SMS bubble] {Name}: popped -- {_why ?? "a stomp / the demo"}; {DebugState}");
        base.Kill();
    }
    private void Split()
    {
        Splits++; SplitsTotal++;
        int made = 0;
        var parent = GetParent();
        for (int i = 0; i < NumDivision; i++)
        {
            if (Alive >= MaxAlive) break;
            var scene = SceneFilePath != "" ? GD.Load<PackedScene>(SceneFilePath) : null;
            if (scene == null) break;
            var node = scene.Instantiate<Node3D>();
            if (node is not SmsOsenBubble b) { node.QueueFree(); break; }
            b.RelaunchTicks = -1; b.DebugPrint = DebugPrint;
            b.MaxAlive = MaxAlive; b.LiveTime = LiveTime; b.NumDivision = NumDivision; b.MaxScale = MaxScale; b.RateExpand = RateExpand; b.FloatHeightUnits = FloatHeightUnits;
            b.SpitDrag = SpitDrag; b.FaceCamera = FaceCamera; b.BottleScene = BottleScene; b.BottleLifeTicks = BottleLifeTicks; b.BottleOnFloor = BottleOnFloor;
            b.Refraction = Refraction; b.RefractionUv = RefractionUv; b.Opacity = Opacity;
            parent.AddChild(b);
            Vector3 vel = new Vector3(Lerp(-2f, 2f, Rand()), Lerp(-2f, 2f, Rand()), Lerp(-2f, 2f, Rand()));
            b.SmsSplitChild(GlobalPosition + Vector3.Up * (_rise / U), vel, Generation + 1);
            made++;
        }
        ChildrenMade += made; ChildrenTotal += made;
        if (DebugPrint) GD.Print($"[SMS bubble] {Name}: split -> {made} children (pool {Alive}/{MaxAlive})");
    }

    // ------------------------------------------------------------------------------------------------------------------ the tick's motion and look
    /// <summary>TLiveActor::moveObject for a bubble: the spit / split velocity carries the body (x SpitDrag a tick: the game's airborne body never slowed); the
    /// walk is in _lin already. The height: pinned to the floor under it + FloatHeightUnits + the rise (TBubble::calcRootMatrix), or sinking there from the
    /// mouth (SpitSinkSeconds). The dying body holds where it is.</summary>
    protected override void SetBehavior()
    {
        if (_dead) return;
        if (_spitVel.LengthSquared() > 0f)
        {
            _lin += _spitVel;
            if (!_child && SpitDrag < 1f) { _spitVel *= SpitDrag; if (_spitVel.LengthSquared() < 0.0001f) _spitVel = Vector3.Zero; }
        }
        if (!IsNerve(NerveDie))
        {
            float target = FloatY;
            if (Sinking)
            {
                _sinkT += 1f;
                float k = Mathf.Clamp(_sinkT / Mathf.Max(1f, SpitSinkSeconds * SmsUnits.TicksPerSecond), 0f, 1f);
                float e = 1f - (1f - k) * (1f - k);              // eased out: quick off the mouth, settling onto the float height
                SetY(Lerp(_sinkFrom, target, e));
                if (k >= 1f) _sinkFrom = float.NaN;
            }
            else SetY(target);
        }
        ClipTick();
    }
    private void PlayClip(Clip c, float frame) { _clip = c; _clipFrame = frame; }
    private bool ClipEnded() => _clip != Clip.None && !ClipLoops[(int)_clip] && _clipFrame >= ClipFrames[(int)_clip];
    /// <summary>The clip's clock: half a bck frame a tick (60 frames a second, the port's enemy clock); the looped wait wraps. The joint's scale and bob read off the keys.</summary>
    private void ClipTick()
    {
        if (_clip == Clip.None) { _clipScale = Vector3.One; _clipBob = 0f; return; }
        int ci = (int)_clip; int frames = ClipFrames[ci];
        _clipFrame += 0.5f;
        if (ClipLoops[ci]) { while (_clipFrame >= frames) _clipFrame -= frames; }
        else if (_clipFrame > frames) _clipFrame = frames;
        float f = _clipFrame; int i0 = Mathf.Clamp((int)MathF.Floor(f), 0, frames); int i1 = Mathf.Min(i0 + 1, frames); float t = f - i0;
        switch (_clip)
        {
            case Clip.Birth: _clipScale = new Vector3(Lerp(BirthSX[i0], BirthSX[i1], t), Lerp(BirthSY[i0], BirthSY[i1], t), Lerp(BirthSZ[i0], BirthSZ[i1], t)); _clipBob = 0f; break;
            case Clip.Wait: _clipScale = new Vector3(Lerp(WaitSX[i0], WaitSX[i1], t), Lerp(WaitSY[i0], WaitSY[i1], t), 1f); _clipBob = Lerp(WaitTY[i0], WaitTY[i1], t); break;
            case Clip.Split: { float s = Lerp(SplitS[i0], SplitS[i1], t); _clipScale = new Vector3(s, s, s); _clipBob = 0f; break; }
        }
    }
    /// <summary>calcRootMatrix + the joint's keys: the dome at the body's point (the bob on its y), scaled mScaling x the clip's per-axis scale, facing the camera
    /// (FaceCamera) or the heading. The glb's own joint scale (1.43) is in the glb.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Vector3 fwd;
        var cam = IsInsideTree() ? GetViewport()?.GetCamera3D() : null;
        if (FaceCamera && cam != null) { fwd = cam.GlobalPosition - GlobalPosition; fwd.Y = 0f; fwd = fwd.LengthSquared() > 1e-6f ? fwd.Normalized() : PolarXZ(_yaw, 1f); }
        else fwd = PolarXZ(_yaw, 1f);
        Vector3 up = Vector3.Up;
        Vector3 x = up.Cross(fwd).Normalized();
        Vector3 z = x.Cross(up).Normalized();
        Basis b = new Basis(x, up, z) * Basis.FromScale(new Vector3(_scale * _clipScale.X, _scale * _clipScale.Y, _scale * _clipScale.Z) * _modelScale);
        Vector3 at = (IsInsideTree() ? GlobalPosition : Vector3.Zero) + Vector3.Up * (_clipBob * _scale / U);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, at);
        else _model.Transform = new Transform3D(b, at);
        foreach (var m in _mats) { m.SetShaderParameter("opacity", Opacity); }
    }
    protected override Vector3 LookScale => Vector3.One * _scale;
}
