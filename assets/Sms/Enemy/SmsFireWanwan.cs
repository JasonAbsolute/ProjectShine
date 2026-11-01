using Godot;
using System;

/// <summary>
/// SMS pass 149 -- the Chain Chomplet (TFireWanwan : TSmallEnemy, Enemy/fireWanwan.cpp, FireWanwan.hpp) on the small-enemy base, with the
/// shipped /enemy/fireWanwan.prm: Pianta Village's red-hot little Chain Chomp on its chain (<see cref="SmsFireWanwanChain"/>), the one the hose
/// cools, Mario drags by the chain and flings into the hot spring.
///
/// THE GAME. init: ACTOR_TYPE_FIRE_WANWAN, unk150 1, the default GRAPHWANDER, the tail hit (the chain) made, the body colour's TLerpControl on
/// `_mat_body`'s GX_TEVREG0, the placed point kept. reset: there again, on fire, the tracer reset, the shortest graph node, the march speed, red at
/// once. The bcks (the archive's eight): 0 damage, 1 hit, 2 return, 3 run, 4 run_end, 5 search, 6 wait, 7 walk; the btk wanwan_eye (the pupils'
/// 5-frame squash and stretch) runs in the cooled states, frozen at 0 in RECOVER. doWalk: sprayed this half-second -> bck 1 (the flinch), a clip
/// done -> walk; walkToCurPathNode(march, turn). doEscape: the same with bck 0 (damage) at march x EscapeSpeedRate. doAttack: the same at march x
/// AttackSpeedRate (run), done = isMissMario (Mario hidden, in water or on its surface; |dy| > GiveUpHeight; the goal past GiveUpLength). decideTarget
/// / doAdjustTarget: a slerp of the facing from the current yaw to the target over the clip's length. isFindMario: not while mStopSearchTimer runs,
/// not with Mario in water, else the param search (1300 u, 120 deg, aware 10, |dy| < 1000). receiveMessage: TRAMPLE / HIP_DROP -> false (a stomp does
/// nothing); SPRAYED_BY_WATER -> WATHIT, the first-water sound at full hp, decHpByWater EVERY drop (no cooldown gate), behaveToWater, the cooldown
/// started. behaveToWater: cooled -> the cool-more sound and the sweat (not RECOVER); hp left -> the to-cool sound, wandering -> FINDMARIO (woken);
/// hp 0 -> the cool sound, ESCAPE, the chain's fire out, cooled, the cooldown 20. behaveHitComrades (another Chomplet touched while walking, the
/// no-collide timer out) -> the timer NoCollideTimerMax, TURN. kill (the pond): not already defeated -> DIE. isHitValid false. moveObject: the
/// timers; updateCollisionFromParam (the prm's hit cylinders, unscaled); the camera shake within CamShakeRange walking / attacking and the rumble
/// (not ported); updatePollute: on fire, every PolluteTimerMax ticks a goop stamp at a random point along the body's X axis within AttackRadius,
/// radius 375 x PolluteNormalRate (x PolluteAttackRate attacking); updateHitPoint: on fire the hp climbs back RecoverRate a tick (1 every 10) to the
/// max; checkHungTail: cooled (ESCAPE / FREEZE) and the chain taken -> HUNGTAIL; control; the hit actors (Mario -> attackToMario; another Chomplet ->
/// the push + behaveHitComrades both ways; others -> the push); bind; the ground; checkInPond (on the ground, a pool or water surface under it, the
/// body within HeadHeight of it -> kill); setBehavior. bind: vy -= the gravity (FlyGravityY 5 flying / defeated, else mGravity), the terminal, |v| <=
/// 100, v x AirFric; the step in 25-u pieces through 8 wall probes (bindBody / bindPoint: the ground, the walls at WallRadius x 0.277); flying into
/// walls -> the velocity reflected on the summed normal, the yaw from it, WALLKICK puffs, the shake, the reflect sound; flying on the ground -> v x
/// FloorFric, vy x -0.05. attackToMario: on fire and not FLY / ESCAPE / RECOVER / FREEZE -> BURN to Mario, mStopSearchTimer = StopSearchTimerMax.
/// emitEffects (the 'center' joint): on fire with hp -> CAN_YUGAMI (the heat haze); sprayed -> CAN_HITYUGE (steam); FREEZE under mFreezeWait ->
/// POI_KIZETSU (the dizzy stars); flying or held -> M_SLIPSMOKE (150 u ahead when held) at alpha 179; RECOVER -> HIKAGE1_A x 1.8; defeated -> the
/// steam for 600 ticks, BWAN_KIRA sparkles x 0.22, HAMON_A / B ripples at the water. calcRootMatrix in DIE: the model 160 u down its own Y axis.
/// GRAPHWANDER: walk; the goal reached -> a random next node; isFindMario(1) -> FINDMARIO after; doWalk. TURN: prepareTurn (the target = yaw +
/// 180), search; the clip done -> walk; doTurn (RotateY a tick) -> GRAPHWANDER after, the previous node again. FINDMARIO: search, decideTarget
/// (Mario); doAdjustTarget; the clip done -> ATTACK after. ATTACK: run, the goal Mario; doAttack -> RECOVERGRAPH after; past AttackTimerMax ->
/// TIRED after. TIRED: run_end (looped); past AttackWait -> FINDMARIO (found) or RECOVERGRAPH after. RECOVERGRAPH: search, the tracer reset, the
/// nearest node, decideTarget (the goal); in walk doAdjustTarget; the clip done -> walk; done -> GRAPHWANDER after. RECOVER: return, the btk frozen at
/// 0, the manager's count, the sound, red over the clip; the clip done -> recoverFire (hp max, on fire), the default after. DIE: damage, the fire
/// out, the pitch 90, the manager's counts (all dead -> the balloon), the sounds, silver over 40, the water column, UNK1000, not airborne, hp 0, the
/// damage height - 250; every tick the velocity's XZ x 0.9 and the after sound. HUNGTAIL: damage + the btk; the yaw = the way from Mario to it (its
/// back to him); isReadyToFly (not taken and the chain within TailLengthToFly) -> FLY after. FLY: damage + the btk, the velocity = polar(yaw, the
/// throw power), FUMI_C at the tail, mFreezeWait = FreezeWait (|v| > FreezeTimeThreshold) or FreezeWaitShort; every tick v x AirFric; the XZ speed
/// under 1 -> FREEZE after. FREEZE: damage + the btk, the twinkle sound, the velocity 0; past mFreezeWait + 75 -> RECOVER after. ESCAPE: the node away
/// from Mario, damage + the btk, black over 40; past EscapeRecoverTime -> RECOVER after; the goal reached -> a random escape node; doEscape; the bark.
///
/// THE PORT. The base's walker and Mario hooks with the species' twelve nerves (NerveSpecies + 0..11; the base's Die and Freeze unused), its own
/// SmsSprayedByWater (every drop counts), HitCheckMario (the prm's unscaled cylinders, no stomp bounce), KeepVelocityOnGround while flying /
/// defeated, the wall bounce from the base's wall contacts, the pond from the base's water areas. The chain is an <see cref="SmsFireWanwanChain"/>
/// made at Init beside the body and stepped after its move; Mario's grab is MarioPull.cs (PULLING, pass 149). The heat colour is the shared
/// EnemyToon shader's c0 on the body and the five links, lerped as the game's TLerpControl (OwnMaterials). The pupils' btk is the eye material's
/// uv_scale (FireWanwanEye.gdshader) from the five keys. Knobs (grouped, plain English): Deviations (SpeedScale), Effects (FireFxScene = Fx/StuFire,
/// FireOffFxScene = Fx/StuFireOff, SlipSmokeFxScene = Fx/SlipSmoke, WallKickFxScene = Fx/WallKickDust, LaunchFxScene = Fx/TrampleDust, DizzyFxScene
/// = Fx/BonkStars, SteamFxScene = Fx/GkKemuriW, SplashFxScene = Fx/WaterSplash; the heat haze, the sweat, the recover shimmer, the sparkles and the
/// ripples are slots), Sounds (slots). DEVIATIONS (logged): the camera shake and the rumble are not ported; the manager (the help balloons, the
/// shine when all are beaten, the reset off the map) is not ported; the chain's nodes read no walls; the 8-point wall probe is the body's collider.
/// </summary>
public partial class SmsFireWanwan : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ fireWanwan.prm
    public class FireParams : Params
    {
        public float AttackSpeedRate = 2.6f, EscapeSpeedRate = 1f, FlySpeed = 30f, FlyGravityY = 5f, AirFric = 0.99f, FloorFric = 0.9f, RotateY = 1f;
        public int AttackTimerMax = 600, EscapeRecoverTime = 600, NoCollideTimerMax = 300, StopSearchTimerMax = 600, PolluteTimerMax = 20, FreezeWaitShort = 120;
        public float TailLengthToFly = 100f, CamShakeRange = 1000f, ContShakeRange = 750f, FreezeTimeThreshold = 50f, RecoverRate = 0.1f;
        public float PolluteNormalRate = 0.7f, PolluteAttackRate = 1.5f, TailEndPowRate = 0.06f, RubberBoundRate = 0.25f, RubberDecay = 0.5f;
        public float RubberBoundRateHitting = 0.7f, RubberDecayHitting = 0.6f, TailEndColRange = 100f, TailApartPowRate = 0.1f, TailMaxLength = 300f;
    }
    /// <summary>The shipped /enemy/fireWanwan.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly FireParams Prm = new FireParams
    {
        HeadHeight = 50f, BodyRadius = 6f, WallRadius = 260f, ClipRadius = 300f, HitPointMax = 60, InstanceNum = 100, ActiveEnemyNum = 4, JumpForce = 0f,
        SearchLength = 1300f, SearchHeight = 1000f, SearchAware = 10f, SearchAngle = 120f, GiveUpLength = 2000f, GiveUpHeight = 300f, AttackWait = 120, FreezeWait = 300,
        DamageRadius = 180f, DamageHeight = 400f, AttackRadius = 200f, AttackHeight = 420f, TurnSpeedLow = 1f, TurnSpeedHigh = 1f, BodyScaleLow = 2f, BodyScaleHigh = 2f,
        GenItemRate = 0f, GenEggRate = 0f, WaitTime = 0, StampRange = 0f, MarchSpeedLow = 3f, MarchSpeedHigh = 3f,
        AttackSpeedRate = 2.3f, AttackTimerMax = 360, EscapeSpeedRate = 1f, EscapeRecoverTime = 1500, FlySpeed = 100f, FlyGravityY = 5f, AirFric = 0.993f, FloorFric = 1f,
        RotateY = 1f, NoCollideTimerMax = 300, StopSearchTimerMax = 600, TailLengthToFly = 50f, CamShakeRange = 1000f, ContShakeRange = 750f, PolluteTimerMax = 20,
        FreezeTimeThreshold = 50f, FreezeWaitShort = 120, RecoverRate = 0.1f, PolluteNormalRate = 0.7f, PolluteAttackRate = 1.5f, TailEndPowRate = 0.2f,
        RubberBoundRate = 0.3f, RubberDecay = 0.55f, RubberBoundRateHitting = 0.7f, RubberDecayHitting = 0.6f, TailEndColRange = 150f, TailApartPowRate = 0.25f, TailMaxLength = 300f,
    };
    protected override Params P => Prm;

    // the body colours (GX_TEVREG0, S10: beyond the 8-bit range) and their lerp (TLerpControl)
    private static readonly Vector3 ColorOnFire = new Vector3(400f, -50f, -100f) / 255f, ColorOnCool = new Vector3(-50f, -50f, 50f) / 255f, ColorOnSilver = new Vector3(260f, 260f, 270f) / 255f;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the walk, the run, the escape and the turn.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_moe_fire_a/b/c/d at the chain's ring while it is hot (shrinking as the hose cools it). Empty = assets/Sms/Fx/StuFire.tscn.</summary>
    [Export] public PackedScene FireFxScene;
    /// <summary>ms_moe_fire_off: the puff when the fire goes out. Empty = assets/Sms/Fx/StuFireOff.tscn.</summary>
    [Export] public PackedScene FireOffFxScene;
    /// <summary>ms_m_slipsmoke under it while it slides (flying) or is held. Empty = assets/Sms/Fx/SlipSmoke.tscn.</summary>
    [Export] public PackedScene SlipSmokeFxScene;
    /// <summary>ms_wallkick_a/b where a flying one bounces off a wall. Empty = assets/Sms/Fx/WallKickDust.tscn.</summary>
    [Export] public PackedScene WallKickFxScene;
    /// <summary>ms_fumi_c at the tail as it launches. Empty = assets/Sms/Fx/TrampleDust.tscn.</summary>
    [Export] public PackedScene LaunchFxScene;
    /// <summary>ms_poi_kizetsu: the dizzy stars over it after a flight. Empty = assets/Sms/Fx/BonkStars.tscn.</summary>
    [Export] public PackedScene DizzyFxScene;
    /// <summary>ms_can_hityuge: the steam when the hose hits it (and off the pond for 600 ticks after). Empty = assets/Sms/Fx/GkKemuriW.tscn.</summary>
    [Export] public PackedScene SteamFxScene;
    /// <summary>The water column where it drops into the pond. Empty = assets/Sms/Fx/WaterSplash.tscn.</summary>
    [Export] public PackedScene SplashFxScene;
    /// <summary>ms_can_yugami (the heat haze over a hot one), ms_cho_ase (the sweat of a hosed cool one), ms_hikage1_a (the shimmer as it reheats),
    /// ms_bwan_kira (the sparkles on a beaten one), ms_npc_hamon_a/b (the ripples around it in the pond). Slots.</summary>
    [Export] public PackedScene HeatHazeFxScene, SweatFxScene, ReheatFxScene, SparkleFxScene, RippleFxScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>The bas tables (wait / walk / run / search / return) are the clips'; these are the code's: the barks while it flees, the hose's first
    /// hit / cooling / cooled / cool-more, the recovery, the thrown flight, the wall bounce, the drop into the pond and the bubbling after, the twinkle.</summary>
    [Export] public AudioStreamPlayer3D BarkSound, FirstWaterSound, ToCoolSound, CoolSound, CoolMoreSound, RecoverSound, ThrownSound, ReflectSound, DownSound, AfterSound, TwinkleSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's order)
    private static readonly string[] Names = { "wanwan_damage", "wanwan_hit", "wanwan_return", "wanwan_run", "wanwan_run_end", "wanwan_search", "wanwan_wait", "wanwan_walk" };
    private static readonly int[] Frames = { 20, 20, 39, 20, 39, 39, 40, 40 };
    private static readonly bool[] Loops = { true, true, false, true, false, false, true, true };
    protected const int BckDamage = 0, BckHit = 1, BckReturn = 2, BckRun = 3, BckRunEnd = 4, BckSearch = 5, BckWait = 6, BckWalk = 7;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected const int NerveWanderF = NerveSpecies + 0, NerveTurn = NerveSpecies + 1, NerveFindMario = NerveSpecies + 2, NerveAttackF = NerveSpecies + 3, NerveTired = NerveSpecies + 4,
        NerveRecoverGraph = NerveSpecies + 5, NerveRecover = NerveSpecies + 6, NerveDieF = NerveSpecies + 7, NerveHungTail = NerveSpecies + 8, NerveFly = NerveSpecies + 9,
        NerveFreezeF = NerveSpecies + 10, NerveEscapeF = NerveSpecies + 11;
    private static readonly string[] FireNerveNames = { "GraphWander", "Turn", "FindMario", "Attack", "Tired", "RecoverGraph", "Recover", "Die", "HungTail", "Fly", "Freeze", "Escape" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + FireNerveNames.Length ? FireNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveWanderF;

    // ------------------------------------------------------------------------------------------------------------------ state
    private SmsFireWanwanChain _chain;
    private int _noCollideTimer, _stopSearchTimer, _polluteTimer, _freezeWait;
    private float _recoverTimer, _turnTargetAngle, _distToMarioSq;
    private float _slerpFrom, _slerpTo;                                                   // decideTarget's two quaternions, as yaws
    private Vector3 _prevGoal; private bool _prevGoalValid;                             // the tracer's previous node (initTurnNextGraphNode's popCurr)
    private ShaderMaterial _bodyMat, _eyeMat;
    private Vector3 _colorCur = ColorOnFire, _colorFrom = ColorOnFire, _colorTo = ColorOnFire; private float _colorT, _colorLen;   // TLerpControl
    private bool _eyeWobble; private float _eyeFrame;
    private Node3D _slipSmoke; private bool _wallHit; private Vector3 _wallNormalSum; private int _wallHits;
    private Skeleton3D _skel; private int _tailBone = -1, _centerBone = -1;
    private float _pitch; private float _defeatDamageHeight;
    private Vector3 _lastStampAt;
    private bool _onFire = true;
    public int Stamps { get; private set; }
    public int Burns { get; private set; }
    public int Bounces { get; private set; }
    public int Comrades { get; private set; }
    public int Recovers { get; private set; }
    public float ScaleNow => _scale;
    public SmsFireWanwanChain Chain => _chain;
    public bool IsOnFire => _onFire;
    public float RecoverTimer => _recoverTimer;
    public int StopSearchTimer => _stopSearchTimer;
    public int FreezeWaitNow => _freezeWait;
    public Vector3 BodyColor => _colorCur;
    public bool EyeWobble => _eyeWobble;
    public float Pitch => _pitch;

    // the state tests (the game's is*)
    public bool IsDefeat => IsNerve(NerveDieF) || (_cur == NerveNone && _prev == NerveDieF);
    public bool CanTakenByMario => IsNerve(NerveEscapeF) || IsNerve(NerveFreezeF);
    public bool IsTurning => IsNerve(NerveTurn);
    public bool IsFlying => IsNerve(NerveFly);
    public bool IsWalking => IsNerve(NerveWanderF);
    public bool IsWandering => IsNerve(NerveRecoverGraph) || IsNerve(NerveTurn) || IsWalking;
    public bool IsAttacking => IsNerve(NerveAttackF);
    public bool IsFreezeF => IsNerve(NerveFreezeF);
    public bool IsReadyToFly => _chain != null && !_chain.IsTaken && _chain.Length() <= Prm.TailLengthToFly;
    public bool IsRecovering => IsNerve(NerveRecover);
    public bool IsHungTailNerve => IsNerve(NerveHungTail);
    public bool IsEscaping => IsNerve(NerveEscapeF);
    public bool IsTailTaken => _chain != null && _chain.IsTaken;

    protected override void Init()
    {
        _flags150 = 1;
        FireFxScene ??= LoadIf("res://assets/Sms/Fx/StuFire.tscn");
        FireOffFxScene ??= LoadIf("res://assets/Sms/Fx/StuFireOff.tscn");
        SlipSmokeFxScene ??= LoadIf("res://assets/Sms/Fx/SlipSmoke.tscn");
        WallKickFxScene ??= LoadIf("res://assets/Sms/Fx/WallKickDust.tscn");
        LaunchFxScene ??= LoadIf("res://assets/Sms/Fx/TrampleDust.tscn");
        DizzyFxScene ??= LoadIf("res://assets/Sms/Fx/BonkStars.tscn");
        SteamFxScene ??= LoadIf("res://assets/Sms/Fx/GkKemuriW.tscn");
        SplashFxScene ??= LoadIf("res://assets/Sms/Fx/WaterSplash.tscn");
        // the materials: the body (its c0 = the heat), the eye (its pupils' squash), own copies so each Chomplet writes its own
        ShaderMaterial linkMat = null;
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
                    else if (rn.StartsWith("_mat_eye")) _eyeMat = own;
                }
            }
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null) { _tailBone = _skel.FindBone("null_tail"); _centerBone = _skel.FindBone("center"); }
        }
        if (ResourceLoader.Exists("res://assets/Sms/Enemy/FireWanwan/FireWanwanTail.tres")) linkMat = GD.Load<ShaderMaterial>("res://assets/Sms/Enemy/FireWanwan/FireWanwanTail.tres");
        _chain = new SmsFireWanwanChain { Name = Name + "Chain" };
        Node parent = GetParent() ?? this;
        parent.CallDeferred(Node.MethodName.AddChild, _chain);
        _chainSetupPending = (linkMat);
        if (DebugPrint) GD.Print($"[SMS wanwan] {Name}: the chain made; the tail joint {(_tailBone >= 0 ? "null_tail" : "MISSING")}, the body material {(_bodyMat != null ? "own" : "none")}");
    }
    private ShaderMaterial _chainSetupPending; private bool _chainReady;
    private static PackedScene LoadIf(string path) => ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
    /// <summary>The chain is a sibling: it goes when the body goes.</summary>
    public override void _ExitTree()
    {
        if (_chain != null && IsInstanceValid(_chain) && !_chain.IsQueuedForDeletion()) _chain.QueueFree();
        base._ExitTree();
    }

    protected override void Reset()
    {
        base.Reset();
        _onFire = true;
        _noCollideTimer = 0; _stopSearchTimer = 0; _polluteTimer = 0; _recoverTimer = 0f; _freezeWait = Prm.FreezeWait;
        _turnSpeed = Prm.TurnSpeedLow * SpeedScale; _marchSpeed = Prm.MarchSpeedLow * SpeedScale;
        _pitch = 0f; _defeatDamageHeight = 0f; _wallHit = false; _wallHits = 0; _wallNormalSum = Vector3.Zero;
        _eyeWobble = false; _eyeFrame = 0f;
        _colorCur = _colorFrom = _colorTo = ColorOnFire; _colorT = _colorLen = 1f;
        ApplyColor();
        _prevGoalValid = false;
        GoToShortestNextGraphNode();
        if (_chain != null && IsInstanceValid(_chain) && _chainReady)
        {
            Vector3 tj = TailJointUnits();
            _chain.ResetRubber(tj, tj); _chain.IsOnFire = true; _chain.SetColor(_colorCur); _chain.PlaceLinks();
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ the anims
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetWalkAnm() => SetBck(BckWalk);
    protected override void SetRunAnm() => SetBck(BckRun);
    protected override void SetDeadAnm() => SetBck(BckDamage);
    private bool Sprayed => _sprayCooldown != 0;
    /// <summary>doWalk / doEscape / doAttack's head: sprayed this half-second -> the flinch (bck 1) over whatever plays.</summary>
    private void FlinchIfSprayed() { if (Sprayed && !IsBck(BckHit)) SetBck(BckHit); }
    private void DoWalk()
    {
        FlinchIfSprayed();
        if (BckEnded()) SetBck(BckWalk);
        WalkToCurPathNode(_marchSpeed, _turnSpeed, 0f);
    }
    private void DoEscape()
    {
        FlinchIfSprayed();
        if (BckEnded()) SetBck(BckDamage);
        WalkToCurPathNode(_marchSpeed * Prm.EscapeSpeedRate, _turnSpeed, 0f);
    }
    private bool DoAttack()
    {
        if (Sprayed) FlinchIfSprayed();
        else WalkToCurPathNode(_marchSpeed * Prm.AttackSpeedRate, _turnSpeed, 0f);
        if (BckEnded()) SetBck(BckRun);
        return IsMissMario();
    }
    /// <summary>decideTarget: the yaw to turn to (the target's XZ way; straight behind -> +180) and the yaw now, slerped over the clip (doAdjustTarget).</summary>
    private void DecideTarget(Vector3 targetU)
    {
        Vector3 d = targetU - Pos(); d.Y = 0f;
        _slerpTo = d.LengthSquared() < 1e-6f ? Wrap360(_yaw + 180f) : Wrap360(YawOf(d));
        _slerpFrom = Wrap360(_yaw);
    }
    private void DoAdjustTarget()
    {
        float t = Mathf.Clamp(BckFrame() / MathF.Max(1f, BckFrames[_bck]), 0f, 1f);
        _yaw = Wrap360(_slerpFrom + AngleDiff(_slerpTo, _slerpFrom) * t);
    }
    private int FreezeTime => _freezeWait + 75;
    protected override bool IsFindMario(float k)
    {
        if (_stopSearchTimer > 0) return false;
        if (MarioInWater) return false;
        return base.IsFindMario(k);
    }
    /// <summary>isMissMario: Mario in water (his flags / his ground a water surface), |dy| > GiveUpHeight, the goal past GiveUpLength.</summary>
    private bool IsMissMario()
    {
        if (_mario == null || MarioInWater) return true;
        if (MathF.Abs(MarioPos().Y - Pos().Y) > Prm.GiveUpHeight) return true;
        return (Goal() - Pos()).Length() > Prm.GiveUpLength;
    }
    private void PrepareTurn() => _turnTargetAngle = Wrap360(_yaw + 180f);
    private bool DoTurn()
    {
        float d = AngleDiff(_turnTargetAngle, _yaw);
        _yaw = Wrap360(_yaw + Mathf.Clamp(d, -Prm.RotateY * SpeedScale, Prm.RotateY * SpeedScale));
        return MathF.Abs(AngleDiff(_yaw, _turnTargetAngle)) < 1f;
    }
    /// <summary>goToShortestNextGraphNode: the nearest graph node (none: the placed point) as the goal.</summary>
    private void GoToShortestNextGraphNode()
    {
        Vector3 best = _spawnPos; float bd = float.MaxValue; bool any = false;
        if (Graph != null)
            foreach (Node c in Graph.GetChildren())
                if (c is Node3D n) { float d = (n.GlobalPosition * U - Pos()).LengthSquared(); if (d < bd) { bd = d; best = n.GlobalPosition * U; any = true; } }
        if (!any) best = _spawnPos;
        SetGoalPoint(best);
    }
    /// <summary>initTurnNextGraphNode (popCurr): back to the node it came from.</summary>
    private void InitTurnNextGraphNode()
    {
        if (_prevGoalValid) { Vector3 cur = Goal(); SetGoalPoint(_prevGoal); _prevGoal = cur; }
        else GoToRandomNextGraphNode();
    }
    private void NextGraphNode()
    {
        _prevGoal = Goal(); _prevGoalValid = true;
        GoToRandomNextGraphNode();
    }
    /// <summary>initEscapeNextGraphNode: the node away from Mario (getEscapeFromMarioIndex) when the nearest one lies toward him, else the nearest.</summary>
    private void InitEscapeNextGraphNode()
    {
        Vector3 nearest = _spawnPos, away = _spawnPos; float bd = float.MaxValue, bdot = float.MaxValue; bool any = false;
        Vector3 toMario = MarioPos() - Pos(); toMario.Y = 0f;
        if (Graph != null)
            foreach (Node c in Graph.GetChildren())
                if (c is Node3D n)
                {
                    Vector3 p = n.GlobalPosition * U, d = p - Pos(); d.Y = 0f;
                    float l2 = d.LengthSquared(); if (l2 < bd) { bd = l2; nearest = p; }
                    float dot = toMario.LengthSquared() > 0f && l2 > 0f ? toMario.Normalized().Dot(d.Normalized()) : 0f;
                    if (dot < bdot) { bdot = dot; away = p; }
                    any = true;
                }
        if (!any) { GoToRandomEscapeGraphNode(); return; }
        Vector3 dn = nearest - Pos(), da = away - Pos();
        SetGoalPoint(toMario.Dot(dn) >= 0f && toMario.Dot(da) < 0f ? away : nearest);
    }
    private void RecoverFire() { _hp = Prm.HitPointMax; _onFire = true; if (_chain != null) _chain.IsOnFire = true; }

    // ------------------------------------------------------------------------------------------------------------------ the colours (TLerpControl)
    private void ChangeBody(Vector3 to, float frames) { _colorFrom = _colorCur; _colorTo = to; _colorT = 0f; _colorLen = MathF.Max(frames, 1f); }
    public void ChangeBodyToRed(float frames) => ChangeBody(ColorOnFire, frames);
    public void ChangeBodyToBlack(float frames) => ChangeBody(ColorOnCool, frames);
    public void ChangeBodyToSilver(float frames) => ChangeBody(ColorOnSilver, frames);
    private void ColorTick()
    {
        if (_colorT >= _colorLen) return;
        _colorT = MathF.Min(_colorLen, _colorT + 0.5f);                                 // the lerp's frame control runs at the anim rate: 2 ticks a frame
        _colorCur = _colorFrom.Lerp(_colorTo, _colorT / _colorLen);
        ApplyColor();
    }
    private void ApplyColor()
    {
        _bodyMat?.SetShaderParameter("c0", _colorCur);
        if (_chain != null && IsInstanceValid(_chain)) _chain.SetColor(_colorCur);
    }
    // the pupils (wanwan_eye.btk: 5 frames looping, the texture scaled about its centre -- X 1 / 1.3 / 1 / - / 0.9 / 1, Y 1 / 0.9 / 1 / - / 1.3 / 1)
    private static readonly float[] EyeKeyF = { 0f, 1f, 2f, 4f, 5f }, EyeKeyX = { 1f, 1.3f, 1f, 0.9f, 1f }, EyeKeyY = { 1f, 0.9f, 1f, 1.3f, 1f };
    private void EyeTick()
    {
        if (_eyeMat == null) return;
        if (!_eyeWobble) { if (_eyeFrame != 0f) { _eyeFrame = 0f; _eyeMat.SetShaderParameter("uv_scale", Vector2.One); } return; }
        _eyeFrame += 0.5f; if (_eyeFrame >= 5f) _eyeFrame -= 5f;                     // the btk at the anim frame rate (2 ticks a frame), looping
        int k = 0; while (k < EyeKeyF.Length - 2 && _eyeFrame > EyeKeyF[k + 1]) k++;
        float t = (_eyeFrame - EyeKeyF[k]) / (EyeKeyF[k + 1] - EyeKeyF[k]);
        _eyeMat.SetShaderParameter("uv_scale", new Vector2(Mathf.Lerp(EyeKeyX[k], EyeKeyX[k + 1], t), Mathf.Lerp(EyeKeyY[k], EyeKeyY[k + 1], t)));
    }
    private void SetEyeWobble(bool on) => _eyeWobble = on;

    // ------------------------------------------------------------------------------------------------------------------ the sounds / effects
    public void StartThrownSound() => ThrownSound?.Play();
    public void StopTriggerSound() { if (ThrownSound != null && ThrownSound.Playing) ThrownSound.Stop(); }
    private Vector3 TailJointUnits()
    {
        if (_skel != null && _tailBone >= 0) return (_skel.GlobalTransform * _skel.GetBoneGlobalPose(_tailBone).Origin) * U;
        return Pos() + PolarXZ(_yaw + 180f, 100f * _scale) + Vector3.Up * 40f * _scale;
    }
    private Vector3 CenterUnits()
    {
        if (_skel != null && _centerBone >= 0) return (_skel.GlobalTransform * _skel.GetBoneGlobalPose(_centerBone).Origin) * U;
        return Pos() + Vector3.Up * 70f * _scale;
    }
    private void SlipSmoke(bool on, Vector3 atU)
    {
        if (on && _slipSmoke == null && SlipSmokeFxScene != null) { _slipSmoke = SlipSmokeFxScene.Instantiate<Node3D>(); _slipSmoke.TopLevel = true; AddChild(_slipSmoke); _slipSmoke.Scale = Vector3.One * _scale; }
        if (_slipSmoke == null) return;
        _slipSmoke.GlobalPosition = atU / U;
        if (_slipSmoke is SmsSlipSmoke s) s.Active = on;
        else foreach (Node n in _slipSmoke.FindChildren("*", "GPUParticles3D", true, false)) ((GpuParticles3D)n).Emitting = on;
    }

    // ------------------------------------------------------------------------------------------------------------------ moveObject
    protected override void MoveObjectPre()
    {
        if (!_chainReady && _chain != null && IsInstanceValid(_chain) && _chain.IsInsideTree())
        {
            Vector3 tj = TailJointUnits();
            _chain.Setup(this, tj, _bodyScale, _chainSetupPending);
            _chain.IsOnFire = true; _chain.SetColor(_colorCur);
            _chainReady = true;
        }
        if (_noCollideTimer > 0) _noCollideTimer--;
        if (_stopSearchTimer > 0) _stopSearchTimer--;
        _distToMarioSq = _mario != null ? (MarioPos() - Pos()).LengthSquared() : float.MaxValue;
        // bind's head: the velocity's gravity is the base's; the clamp and the air friction are the species'
        _vel.X = Mathf.Clamp(_vel.X, -100f, 100f); _vel.Y = Mathf.Clamp(_vel.Y, -100f, 100f); _vel.Z = Mathf.Clamp(_vel.Z, -100f, 100f);
        _vel *= Prm.AirFric;
        // a wall met while flying (the base's contacts last frame): the velocity reflected on the summed normal, the yaw from it
        if (_wallHit)
        {
            if (IsFlying && _wallNormalSum.LengthSquared() > 1e-6f)
            {
                Vector3 n = _wallNormalSum.Normalized();
                float d = _vel.Dot(n);
                if (d < 0f) _vel += n * MathF.Abs(d * 2f);
                _yaw = Wrap360(YawOf(_vel));
                Bounces++;
                if (WallKickFxScene != null) SpawnBurst(WallKickFxScene, new Transform3D(Basis.LookingAt(-n, Vector3.Up) * Basis.FromScale(Vector3.One * _scale), GlobalPosition + Vector3.Up * (100f * _scale / U)));
                StopTriggerSound(); ReflectSound?.Play();
                if (DebugPrint) GD.Print($"[SMS wanwan] {Name}: off a wall, the velocity {_vel} (normal {n})");
            }
            _wallHit = false; _wallNormalSum = Vector3.Zero;
        }
        UpdatePollute();
        UpdateHitPoint();
        CheckHungTail();
    }
    protected override void OnWallContact(KinematicCollision3D col) { _wallHit = true; _wallNormalSum += col.GetNormal(); _wallHits++; }
    protected override bool WallHopAllowed() => false;
    protected override bool KeepVelocityOnGround() => IsFlying || IsDefeat;
    protected override float GravityY() => IsFlying || IsDefeat ? Prm.FlyGravityY : P.Gravity;
    /// <summary>updatePollute: on fire, every PolluteTimerMax ticks a goop stamp at a random point along the body's X axis within AttackRadius.</summary>
    private void UpdatePollute()
    {
        if (!_onFire) return;
        if (_polluteTimer != 0) { _polluteTimer--; return; }
        _polluteTimer = Prm.PolluteTimerMax;
        Vector3 x = PolarXZ(_yaw + 90f, 1f);
        Vector3 at = Pos() + x * ((Rand() - 0.5f) * 2f * Prm.AttackRadius);
        float radius = 375f * (IsAttacking ? Prm.PolluteAttackRate : Prm.PolluteNormalRate);
        SmsPollutionMap.Pollute(at / U, radius);
        _lastStampAt = at; Stamps++;
    }
    /// <summary>updateHitPoint: on fire the hp climbs back RecoverRate a tick to the max.</summary>
    private void UpdateHitPoint()
    {
        if (!_onFire) return;
        _recoverTimer += Prm.RecoverRate;
        if (_recoverTimer >= 1f) { _recoverTimer -= 1f; _hp = Math.Min(_hp + 1, Prm.HitPointMax); }
    }
    /// <summary>checkHungTail: cooled (ESCAPE / FREEZE) with the chain taken -> HUNGTAIL.</summary>
    private void CheckHungTail()
    {
        if (CanTakenByMario && IsTailTaken) { _stack.Clear(); SetNext(NerveHungTail); }
    }
    protected override void SetBehavior()
    {
        // isFlying && !airborne: v x FloorFric, vy x -0.05 (the slide along the ground)
        if (IsFlying && !_airborne) { _vel.X *= Prm.FloorFric; _vel.Z *= Prm.FloorFric; _vel.Y *= -0.05f; }
        if (IsDefeat) { _vel.X *= 0.9f; _vel.Z *= 0.9f; }
        CheckInPond();
        ColorTick();
        EyeTick();
        // the chain after the body's tick, then the effects
        if (_chainReady && _chain != null && IsInstanceValid(_chain))
        {
            _chain.Step(TailJointUnits());
            // the ring flame: lit while hot, and ALSO building through RECOVER so the reignition SHOWS fire growing back (pass 150b -- the game lit it
            // only at the clip's end, which read as a pop and, with ReheatFxScene an empty slot, left the reheat with no fire at all). It grows from a
            // small flame to full over the return clip, in step with the body's black -> red lerp; RecoverFire then keeps it at full.
            bool fireOn = (_onFire || IsRecovering) && !_dead;
            float fireScale = IsRecovering
                ? _bodyScale * Mathf.Clamp(BckFrame() / MathF.Max(1f, BckFrames[BckReturn]), 0.25f, 1f)
                : _bodyScale * _hp / MathF.Max(1, Prm.HitPointMax);
            _chain.FireEffect(fireOn, fireScale, FireFxScene, FireOffFxScene);
        }
        EmitEffects();
    }
    /// <summary>checkInPond: on the ground with a pool / water surface under the body within HeadHeight -> killed.</summary>
    private void CheckInPond()
    {
        if (_airborne || IsDefeat || _dead) return;
        if (WaterSurfaceAt(GlobalPosition + Vector3.Up * 0.3f, out float surf, 0.35f) && (surf - GlobalPosition.Y) * U > -Prm.HeadHeight)
        {
            if (DebugPrint) GD.Print($"[SMS wanwan] {Name}: in the pond (the surface {(surf - GlobalPosition.Y) * U:F0} u over its feet)");
            Kill();
        }
    }
    public override void Kill()
    {
        if (_dead || IsDefeat) return;
        _stack.Clear(); SetNext(NerveDieF);
    }
    protected override bool IsHitValid(HitMessage msg) => false;
    /// <summary>The hit check with the prm's unscaled cylinders (updateCollisionFromParam) and no stomp: TRAMPLE / HIP_DROP are refused, so a Mario
    /// landing on it is only in its attack cylinder (the burn, when hot); the hose on the body still counts.</summary>
    protected override void HitCheckMario()
    {
        if (_mario == null || !GetCollisionLayerValue(1)) return;
        Vector3 g = Pos(), m = MarioPos();
        float dh = IsDefeat ? MathF.Max(Prm.DamageHeight - 250f, 50f) : Prm.DamageHeight;
        bool marioHits = CheckDistance(m, SmsParams.Mario.HoldRadius, SmsParams.Mario.AttackHeight, g, Prm.DamageRadius, dh);
        if (marioHits && _mario.SmsHoseSpraying) SmsSprayedByWater(m + Vector3.Up * 80f / U);
        if (IsDefeat) return;
        bool weHit = CheckDistance(g, Prm.AttackRadius, Prm.AttackHeight, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight);
        if (weHit)
        {
            AttackToMario();
            if (!_onFire) _mario.SmsKeepDistance(g, Prm.DamageRadius);                 // doKeepDistance: a cooled one is a thing he walks into, not through
        }
    }
    /// <summary>attackToMario: on fire and not FLY / ESCAPE / RECOVER / FREEZE -> BURN (the fire knock), the search stopped for StopSearchTimerMax.</summary>
    protected override void AttackToMario()
    {
        if (!_onFire) return;
        if (IsFlying || IsEscaping || IsRecovering || IsFreezeF) return;
        _mario.TakeDamage(GlobalPosition, Mario.SmsDamageProfile.Fire);
        Burns++; if (DebugPrint && (Burns & 7) == 1) GD.Print($"[SMS wanwan] {Name}: BURN on Mario");
        _stopSearchTimer = Prm.StopSearchTimerMax;
    }
    /// <summary>checkHitActors: another Chomplet touched -> the push (true) and behaveHitComrades on both.</summary>
    protected override bool IsCollidMove(Node3D other) { if (other is SmsFireWanwan o) { BehaveHitComrades(); o.BehaveHitComrades(); } return true; }
    private void BehaveHitComrades()
    {
        if (!IsWalking || _noCollideTimer > 0) return;
        _noCollideTimer = Prm.NoCollideTimerMax;
        Comrades++;
        _stack.Clear(); SetNext(NerveTurn);
    }
    /// <summary>receiveMessage(SPRAYED_BY_WATER): the mark, the first-water sound at full hp, the hp down EVERY drop, behaveToWater, the cooldown started.</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead || IsDefeat) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (_hp == Prm.HitPointMax) FirstWaterSound?.Play();
        _hp = Math.Max(0, _hp - 1);
        BehaveToWater();
        if (_sprayCooldown == 0) _sprayCooldown = 1;
        return true;
    }
    protected override void BehaveToWater()
    {
        if (!_onFire)
        {
            if (_sprayCooldown == 0)
            {
                CoolMoreSound?.Play();
                if (!IsRecovering && SweatFxScene != null) SpawnBurst(SweatFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale * 0.75f), GlobalPosition));
            }
            return;
        }
        if (_hp != 0)
        {
            if (_sprayCooldown == 0) ToCoolSound?.Play();
            if (IsWandering) { _stack.Clear(); SetNext(NerveFindMario); }
            return;
        }
        CoolSound?.Play();
        _stack.Clear(); SetNext(NerveEscapeF);
        _onFire = false;
        if (_chain != null) { _chain.IsOnFire = false; _chain.FireEffect(false, 0f, FireFxScene, FireOffFxScene); }
        _sprayCooldown = 20;
        if (DebugPrint) GD.Print($"[SMS wanwan] {Name}: cooled -- ESCAPE");
    }
    /// <summary>emitEffects, once a tick: the steam while sprayed, the slip smoke flying / held, the dizzy stars in FREEZE, the pond's steam and sparkles.</summary>
    private void EmitEffects()
    {
        Vector3 center = CenterUnits();
        if (_hp != 0 && Sprayed && SteamFxScene != null && (_tickCount & 15) == 0) SpawnBurst(SteamFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), center / U));
        if (((_hp != 0 && _onFire) || IsRecovering) && HeatHazeFxScene != null && (_tickCount & 15) == 0) SpawnBurst(HeatHazeFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), center / U));
        if (IsFreezeF && _time < _freezeWait && DizzyFxScene != null && (_tickCount & 63) == 0) SpawnBurst(DizzyFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), center / U + Vector3.Up * (0.6f * _scale)));
        bool slip = IsFlying || IsTailTaken;
        SlipSmoke(slip, IsFlying ? Pos() : Pos() + PolarXZ(_yaw, 150f));
        if (IsRecovering && ReheatFxScene != null && (_tickCount & 15) == 0) SpawnBurst(ReheatFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale * 1.8f), GlobalPosition));
        if (IsDefeat)
        {
            if (_time < 600 && SteamFxScene != null && (_tickCount & 31) == 0) SpawnBurst(SteamFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
            if (SparkleFxScene != null && (_tickCount & 63) == 0) SpawnBurst(SparkleFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale * 0.22f), center / U));
            if (RippleFxScene != null && (_tickCount & 63) == 0) SpawnBurst(RippleFxScene, new Transform3D(Basis.FromScale(Vector3.One * 1.5f), GlobalPosition));
        }
    }
    /// <summary>The look: the body's yaw and, defeated, its 90-degree pitch with the model 160 u down its own Y axis (calcRootMatrix).</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Vector3 fwd = PolarXZ(_yaw, 1f), x = Vector3.Up.Cross(fwd).Normalized();
        Basis b = new Basis(x, Vector3.Up, fwd);
        if (_pitch != 0f) b = b * new Basis(Vector3.Right, Mathf.DegToRad(_pitch));
        Vector3 at = GlobalPosition;
        if (IsDefeat) at += b.Y * (-160f / U);
        b = b * Basis.FromScale(LookScale * _modelScale);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, at);
        else _model.Transform = new Transform3D(b, Vector3.Zero);
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveWanderF:                                   // TNerveFireWanwanGraphWander
                if (_time == 0) SetBck(BckWalk);
                if (IsReachedToGoal()) NextGraphNode();
                if (IsFindMario(1f)) { PushAfterCurrent(NerveFindMario); return true; }
                DoWalk();
                return false;

            case NerveTurn:                                      // TNerveFireWanwanTurn
                if (_time == 0) { PrepareTurn(); SetBck(BckSearch); }
                if (BckEnded()) SetBck(BckWalk);
                if (DoTurn()) { PushAfterCurrent(NerveWanderF); InitTurnNextGraphNode(); return true; }
                return false;

            case NerveFindMario:                                 // TNerveFireWanwanFindMario
                if (_time == 0) { SetBck(BckSearch); DecideTarget(MarioPos()); }
                DoAdjustTarget();
                if (BckEnded()) { PushAfterCurrent(NerveAttackF); return true; }
                return false;

            case NerveAttackF:                                   // TNerveFireWanwanAttack
                if (_time == 0) { SetBck(BckRun); SetGoalMario(); }
                if (DoAttack()) { PushAfterCurrent(NerveRecoverGraph); return true; }
                if (_time > Prm.AttackTimerMax) { PushAfterCurrent(NerveTired); return true; }
                return false;

            case NerveTired:                                     // TNerveFireWanwanTired
                if (_time == 0) SetBck(BckRunEnd);
                if (BckEnded())
                {
                    SetBck(BckRunEnd);
                    if (_time > Prm.AttackWait) { PushAfterCurrent(IsFindMario(1f) ? NerveFindMario : NerveRecoverGraph); return true; }
                }
                return false;

            case NerveRecoverGraph:                              // TNerveFireWanwanRecoverGraph
                if (_time == 0) { SetBck(BckSearch); _prevGoalValid = false; GoToShortestNextGraphNode(); DecideTarget(Goal()); }
                if (IsBck(BckWalk)) DoAdjustTarget();
                else if (BckEnded()) SetBck(BckWalk);
                if (BckEnded()) { PushAfterCurrent(NerveWanderF); return true; }
                return false;

            case NerveRecover:                                   // TNerveFireWanwanRecover
                if (_time == 0)
                {
                    SetBck(BckReturn); SetEyeWobble(false);
                    Recovers++; RecoverSound?.Play();
                    ChangeBodyToRed(BckFrames[BckReturn]);
                    if (DebugPrint) GD.Print($"[SMS wanwan] {Name}: RECOVER -- red again over the return");
                }
                if (BckEnded()) { RecoverFire(); PushAfterCurrent(DefaultNerve); return true; }
                return false;

            case NerveDieF:                                      // TNerveFireWanwanDie
                if (_time == 0)
                {
                    SetBck(BckDamage); SetEyeWobble(false);
                    _onFire = false; if (_chain != null) { _chain.IsOnFire = false; _chain.FireEffect(false, 0f, FireFxScene, FireOffFxScene); }
                    _pitch = 90f;
                    StopTriggerSound(); DownSound?.Play();
                    ChangeBodyToSilver(40f);
                    if (SplashFxScene != null) SpawnBurst(SplashFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
                    _airborne = false;
                    _hp = 0;
                    if (DebugPrint) GD.Print($"[SMS wanwan] {Name}: DIE -- beaten in the pond");
                }
                if ((_tickCount & 127) == 0) AfterSound?.Play();
                return false;                                    // for good: isDefeat

            case NerveHungTail:                                  // TNerveFireWanwanHungTail
                if (_time == 0) { SetBck(BckDamage); SetEyeWobble(true); if (DebugPrint) GD.Print($"[SMS wanwan] {Name}: HUNGTAIL -- the chain in his hands"); }
                { Vector3 v = Pos() - MarioPos(); v.Y = 0f; if (v.LengthSquared() > 1e-6f) _yaw = Wrap360(YawOf(v)); }
                if (IsReadyToFly) { PushAfterCurrent(NerveFly); return true; }
                return false;

            case NerveFly:                                       // TNerveFireWanwanFly
                if (_time == 0)
                {
                    SetBck(BckDamage); SetEyeWobble(true);
                    float pow = _chain != null ? _chain.ThrowPow : 0f;
                    Vector3 vel = PolarXZ(_yaw, pow);
                    _vel = vel; _airborne = true;
                    if (LaunchFxScene != null) SpawnBurst(LaunchFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), TailJointUnits() / U));
                    _freezeWait = vel.LengthSquared() > Prm.FreezeTimeThreshold * Prm.FreezeTimeThreshold ? Prm.FreezeWait : Prm.FreezeWaitShort;
                    if (DebugPrint) GD.Print($"[SMS wanwan] {Name}: FLY at {pow:F1} u a tick toward {_yaw:F0} deg");
                }
                _vel *= Prm.AirFric;
                { Vector3 v = _vel; v.Y = 0f; if (v.LengthSquared() < 1f) { PushAfterCurrent(NerveFreezeF); return true; } }
                return false;

            case NerveFreezeF:                                   // TNerveFireWanwanFreeze
                if (_time == 0) { SetBck(BckDamage); SetEyeWobble(true); TwinkleSound?.Play(); }
                _vel = Vector3.Zero;
                if (_time > FreezeTime) { PushAfterCurrent(NerveRecover); return true; }
                return false;

            case NerveEscapeF:                                   // TNerveFireWanwanEscape
                if (_time == 0) { InitEscapeNextGraphNode(); SetBck(BckDamage); SetEyeWobble(true); ChangeBodyToBlack(40f); }
                if (_time > Prm.EscapeRecoverTime) { PushAfterCurrent(NerveRecover); return true; }
                if (IsReachedToGoal()) GoToRandomEscapeGraphNode();
                DoEscape();
                if ((_tickCount & 63) == 0) BarkSound?.Play();
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
