using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// LAND BLOOPER -- TGesso / TLandGesso / TSurfGesso (Enemy/gesso.cpp; JP ゲッソー, the rikuGesso archive) on <see cref="SmsSmallEnemy"/> with the shipped
/// /enemy/gesso.prm (SMS pass 141, feature/enemy-ricco). Ricco Harbor's big goop-spitting squid: the REGULAR kind sits on the edge of a walkway, leans
/// after Mario, spits goop balls that splat the ground, rolls over the edge to hang underneath when he is below it, and once the hose knocks it off it
/// walks the ground as the LAND kind does -- chasing, punching, spitting, paralysed by water; the SURF kind is a walker with the surfing death clip.
///
/// THE GAME. A TWalkerEnemy (unk150 17, ACTOR_TYPE_GESSO): march 0.8..1.2 u/tick, turn 0.7..1.2, body scale 1.1, 1 hit point, attack 70 x 120 / damage 60 x
/// 160, SearchLength 1600 / Angle 360 / Aware 400 / Height 1200, GiveUp 2500 / 2000, FreezeWait 600, AttackWait 120; its own: SearchLengthOnObj 2000 /
/// AngleOnObj 180 / AwareOnObj 500, PollutionLength 1000, PollutionInterval 200, PolluteObjGravity 0.14 / Speed 10 (the linear mode 15 / 0.04 is never
/// chosen), DropGravityY 0.4, BodyAngMax 40, TurnLength 1000, HitWaterSpXZ 18 / SpY 5. The archive's 23 bcks in order are the clip table (0-1 the goop
/// ball's own, 22 the stamp's): 2 180turn1, 3 down1, 4 fall1, 5 gero1, 6 hit1, 7 land1, 8 mahi_end1, 9 mahi_loop1, 10 mahi_start1, 11 punch1, 12 run1, 13
/// search1, 14 stairland1, 15 surf1, 16 turn_down1, 17 turn_gero1, 18 turn_hit1, 19 turn_up1, 20 turn_wait1, 21 wait1. load(): the placed yaw picks the
/// facing (mStayYaw 0 / 90 / 180 / 270, the body's yaw then zeroed); reset(): the regular kind STATE_BEAM_CHILLING with the Stay nerve, the others
/// STATE_WANDERING with GraphWander; wait1 started at a frame offset by the slot. The state machine: BEAM_CHILLING -- bind pins mPosition.y = unk1A4 (the
/// placed height) - unk1D0 (0 upright; mDamageHeight hanging: the body point under the walkway while the model, drawn at mPosition.y + unk1D0 with
/// yaw mRotation.y + mStayYaw, hangs there in turn_wait1); the body leans after Mario (bind: mBodyTrackingAngle eases 0.5 deg/tick toward the angle
/// between the facing's side axis and his direction; GessoBodyCallback turns joint 12 bodyjnt1 about X by clamp(angle - 90, +-BodyAngMax)). STAY:
/// setWaitAnm (wait1 / turn_wait1), rollCheck, polluteBehavior. rollCheck: no attack cooldown, Mario in sight along the facing (2000 u, 180 deg, aware
/// 500) and under the body point by 10 u upright (over it hanging) -> no collision, STATE_ROLLING, ROLLING (turn_down1 / turn_up1; its end rollEnd:
/// collision on, BEAM_CHILLING, upright flipped); within TurnLength and upright and NOT in sight -> TURN (180turn1, no collision; 7.2 deg/tick to 180,
/// then its end: turnOut flips the facing). polluteBehavior (Stay, and walkBehavior mode 2): the timer past PollutionInterval with the clip at its end,
/// Mario in sight (PollutionLength, 180 deg) -> POLLUTE: setPolluteGoal (Mario's position +-100 u, calcVelocityToJumpToY at PolluteObjSpeed -- 0 hanging
/// -- under PolluteObjGravity); gero1 (turn_gero1 hanging), the spit particle at joint 13 kutijnt1, the ball appears at the mouth; frame 50 -> pollute():
/// the ball flies (vy -3 when hanging), its yaw along the flight, from the mouth + 100 u along the facing. The BALL (TGessoPolluteObj, a
/// TEnemyAttachment, 10 x 10 hit actor): bind under PolluteObjGravity; the ground -> behaveToHitGround -> rebirth: tick 0 GESO_OSENHIT_A / B + the land
/// sound, tick 10 vy -15 and gpPollution->pollute(pos, 8 x 32 x 0.5 = 128 u) + the stamp model (stamp_gero_model1), past 20 gone; a wall -> rebirth;
/// touching Mario -> ATTACK and gone; KISEKI bound while it flies. behaveToWater (every hit: the cooldown reset): not in Die / Fall / Freeze and not
/// rolling: BEAM_CHILLING -> (upright: the knock away from Mario at HitWaterSpXZ, at least 5 per axis, +2 u, airborne) FALL; wandering -> FREEZE.
/// FALL: wandering: away from Mario at 4, hit1; its end: away at 6 with vy 5 and fall1; landed on water -> the column and Die; fall1's end -> land1 ->
/// GraphWander; the beam kind: hit1 (upright) or turn_hit1 with the body 200 u down (hanging), STATE_FALLING (bind: pos += vel, gravity DropGravityY /
/// 2 upright, until 100 u under the placed height or turn_hit1 -> the normal bind); unk1D0 x 0.8 a tick; the clip's end -> fall1; landed: water ->
/// Die, else land1 (modifyRotate: mStayYaw and mRotation x 0.8 a tick) -> fallEnd: WANDERING, upright, the rotation 0 -> GraphWander; the XZ velocity x
/// 0.99 a tick. FREEZE: the knock away from Mario (x 4, vy 5), +2 u, airborne; checkDropInWater (50 ticks of the parabola: a water surface at the
/// landing -> Fall instead); mahi_start1 -> mahi_loop1 (POI_KIZETSU while down) -> past FreezeWait and no new hit (unk165) mahi_end1 -> done; the
/// knock bounces (vy x 0.3 each landing until under 1.5); airborne it turns toward Mario. attackToMario: not wandering -> ATTACK, the cooldown; wandering:
/// PostAttack + PUNCH pushed (punch1: frame 10 -> ATTACK); in the Freeze / Die nothing but the mahi_loop case. doKeepDistance: mahi_loop with Mario not
/// 50 u above (he is pushed off the paralysed body). setBehavior: the attack cooldown (200), mNeedsLanding when airborne 250 u over the ground -> LAND
/// (stairland1) on landing in GraphWander / Attack. behaveToFindMario: the goal Mario, GraphWander + Attack, and FINDMARIO (search1) once per 300 ticks.
/// walkBehavior runs only past clip frame 20. kill: never while hanging; the ball gone. setDeadAnm: a water surface -> hidden; surf1 for the surf kind,
/// else down1. genRandomItem: surf1 -> a surfboard (ACTOR_TYPE_GESO_SURF_BOARD). TSurfGesso never finds Mario.
///
/// THE PORT. <see cref="Kind"/> picks the game's three classes. The walker side is the base's (GraphWander / Attack / PostAttack / Die with the overrides
/// named after the game's virtuals). The beam kind collides with NOTHING while it sits (its scene mask is read into <c>_planeMask</c> and zeroed; SetBehavior
/// pins the body point each tick and MoveObjectPre keeps the airborne flag down); the mask comes back when it falls and it rides the base's bind from
/// then on. The lean is an SmsBoneRoll on bodyjnt1 about the model's X. The goop ball is the scene's GoopBall child (gero_model1.glb, TopLevel) stepped by
/// the species: a ray for the ground and the step, a cylinder test on Mario, the splat = <see cref="GoopSplatFxScene"/> + SmsPollutionMap.Pollute 128 u
/// (the stamp model is the pollution stamp, as the Gorogoro's). A water surface is a floor for the drown (the base's WaterSurfaceAt). DEVIATIONS
/// (logged): the facing is the placed yaw as is (the game rounds it to 0 / 90 / 180 / 270 and the land kinds start facing +Z); a wandering one spits in
/// front of ITSELF (the game passes a direction of 0 = +Z for a wanderer); the surfboard, the stamp model, the trail and spit particles, the sounds are
/// slots; the fall's own integration is the base's bind; the clipped-out rules are not ported.
/// </summary>
public partial class SmsGesso : SmsSmallEnemy
{
    public enum Kinds { Regular, Land, Surf }
    public enum States { BeamChilling = 0, Wandering = 1, Rolling = 2, Falling = 3, FreeFall = 4 }

    // ------------------------------------------------------------------------------------------------------------------ gesso.prm
    public class GessoParams : Params
    {
        public float SearchLengthOnObj = 500f, SearchAngleOnObj = 180f, SearchAwareOnObj = 500f, PollutionLength = 200f, PolluteObjGravity = 0.01f, PolluteObjSpeed = 5f,
            PolluteObjLinerSp = 5f, PolluteObjLinerG = 0.01f, DropGravityY = 0.1f, BodyAngMax = 40f, PolluteModelScale = 2f, TurnLength = 300f, HitWaterSpXZ = 12f, HitWaterSpY = 5f;
        public int PollutionInterval = 300;
    }
    /// <summary>The shipped /enemy/gesso.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly GessoParams Prm = new GessoParams
    {
        HeadHeight = 80f, BodyRadius = 5f, WallRadius = 60f, ClipRadius = 500f, HitPointMax = 1, InstanceNum = 16, ActiveEnemyNum = 16,
        JumpForce = 6f, SearchLength = 1600f, SearchHeight = 1200f, SearchAware = 400f, SearchAngle = 360f, GiveUpLength = 2500f, GiveUpHeight = 2000f,
        AttackWait = 120, FreezeWait = 600, DamageRadius = 60f, DamageHeight = 160f, AttackRadius = 70f, AttackHeight = 120f,
        TurnSpeedLow = 0.7f, TurnSpeedHigh = 1.2f, BodyScaleLow = 1.1f, BodyScaleHigh = 1.1f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 5f, ZigzagCycle = 500f, ZigzagAngle = 30f, MarchSpeedLow = 0.8f, MarchSpeedHigh = 1.2f,
        SearchLengthOnObj = 2000f, SearchAngleOnObj = 180f, SearchAwareOnObj = 500f, PollutionLength = 1000f, PollutionInterval = 200, PolluteObjGravity = 0.14f,
        PolluteObjSpeed = 10f, PolluteObjLinerSp = 15f, PolluteObjLinerG = 0.04f, DropGravityY = 0.4f, BodyAngMax = 40f, PolluteModelScale = 2f, TurnLength = 1000f,
        HitWaterSpXZ = 18f, HitWaterSpY = 5f,
    };
    protected override Params P => Prm;
    public const float BodyRotSpeed = 0.5f, PollRange = 8f, ThroughHoseiDistY = 200f;          // TGesso's statics
    public const float BallScale = 1.4f;                                                      // TGessoPolluteObj's unk164
    public const string BodyJoint = "bodyjnt1", MouthJoint = "kutijnt1";                     // mBodyJntIndex 12, mMouthJntIndex 13

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>Regular = the walkway-edge sitter (TGesso: STATE_BEAM_CHILLING, placed ON the edge facing out over it); Land = the ground walker (TLandGesso);
    /// Surf = the walker with the surfing death clip (TSurfGesso; the surfboard it drops is not ported).</summary>
    [Export] public Kinds Kind = Kinds.Regular;
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the walk (the game: 0.8..1.2 u/tick, x 3 chasing).</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>PARTICLE_MS_GESO_OSENHIT_A + _B where a goop ball lands (an SmsFxBurst / one-shot GPUParticles3D scene). Empty = none (the pollution stamp still lands).</summary>
    [Export] public PackedScene GoopSplatFxScene;
    /// <summary>The spit's particle (0xBB) at the mouth when a ball is coughed up. Empty = none.</summary>
    [Export] public PackedScene SpitFxScene;
    /// <summary>PARTICLE_MS_POI_KIZETSU: the dizzy stars over a paralysed body (a looping GPUParticles3D scene as a child). Empty = assets/Sms/Enemy/Fx131/PoiDizzy.tscn.</summary>
    [Export] public PackedScene DizzyFxScene;
    /// <summary>TEffectColumWater where it drowns. Empty = assets/Sms/Fx/WaterSplashSmall.tscn.</summary>
    [Export] public PackedScene WaterColumnFxScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>The bas tables' sounds: the spit (gero1), the punch, the hit (hit1), the roll (turn_down1 / up1), the ball's landing (GESO_GERO_LAND).</summary>
    [Export] public AudioStreamPlayer3D SpitSound, PunchSound, HitSound, RollSound, GoopLandSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's 23 bcks, the game's indices)
    private static readonly string[] Names = { "", "", "geso_180turn1", "geso_down1", "geso_fall1", "geso_gero1", "geso_hit1", "geso_land1", "geso_mahi_end1", "geso_mahiloop1", "geso_mahi_start1",
        "geso_punch1", "geso_run1", "geso_search1", "geso_stairland1", "geso_surf1", "geso_turn_down1", "geso_turn_gero1", "geso_turn_hit1", "geso_turn_up1", "geso_turn_wait1", "geso_wait1", "" };
    private static readonly int[] Frames = { 0, 0, 29, 29, 15, 69, 29, 34, 29, 40, 19, 29, 60, 29, 29, 129, 59, 69, 29, 69, 60, 60, 0 };
    private static readonly bool[] Loops = { false, false, false, false, true, false, false, false, false, true, false, false, true, false, false, false, false, false, false, false, true, true, false };
    protected const int BckTurn180 = 2, BckDown = 3, BckFall = 4, BckGero = 5, BckHit = 6, BckLand = 7, BckMahiEnd = 8, BckMahiLoop = 9, BckMahiStart = 10, BckPunch = 11, BckRun = 12,
        BckSearch = 13, BckStairLand = 14, BckSurf = 15, BckTurnDown = 16, BckTurnGero = 17, BckTurnHit = 18, BckTurnUp = 19, BckTurnWait = 20, BckWait = 21;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    // (Godot's glTF import drops a "_loop" from a clip's name: geso_mahi_loop1 was renamed geso_mahiloop1 in the glb, the ball's gero_run_loop1 gero_runloop1)
    public const string BallClipStart = "gero_run1", BallClipLoop = "gero_runloop1";

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveStay = NerveSpecies, NerveGFreeze = NerveSpecies + 1, NervePunch = NerveSpecies + 2, NervePollute = NerveSpecies + 3, NerveFall = NerveSpecies + 4,
        NerveRolling = NerveSpecies + 5, NerveFindMario = NerveSpecies + 6, NerveLand = NerveSpecies + 7, NerveTurn = NerveSpecies + 8;
    private static readonly string[] GessoNerveNames = { "Stay", "Freeze", "Punch", "Pollute", "Fall", "Rolling", "FindMario", "Land", "Turn" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + GessoNerveNames.Length ? GessoNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => Kind == Kinds.Regular ? NerveStay : NerveGraphWander;

    // ------------------------------------------------------------------------------------------------------------------ state
    private States _state = States.BeamChilling;   // mState
    private bool _rightSideUp = true;              // mIsRightSideUp
    private float _stayYaw;                        // mStayYaw: the facing (the placed yaw)
    private float _beamY;                          // unk1A4: the placed height, units
    private float _hosei;                          // unk1D0: the model over the body point (mDamageHeight while hanging)
    private float _bodyTrack = 90f;                // mBodyTrackingAngle
    private int _attackCooldown, _pollTimer, _findCooldown;   // mAttackCooldown / mPollutionTimer / unk1B4
    private float _turnAngle;                      // mTurnAngle
    private bool _needsLanding, _freezeHitAgain, _punchSent, _spat;   // mNeedsLanding / unk165 / checkPass(10) / checkPass(50)
    private Vector3 _knock;                        // unk1DC: the freeze's knock
    private uint _planeMask = 5;
    private float _dispY;
    private bool _waterUnder, _onWater, _landedWater; private float _waterSurfU;
    private Skeleton3D _skel; private SmsBoneRoll _lean; private Node3D _dizzyFx;
    // the goop ball (TGessoPolluteObj)
    private enum BallStates { Idle, Held, Flying, Splat }
    private BallStates _ball = BallStates.Idle; private Node3D _ballNode, _ballModel; private AnimationPlayer _ballAnim;
    private Vector3 _ballPos, _ballVel, _ballDir; private int _ballCount; private float _ballGroundY; private bool _ballGrounded;
    /// <summary>For the drives.</summary>
    public States State => _state;
    public bool RightSideUp => _rightSideUp;
    public float StayYaw => _stayYaw;
    public float BodyTrackingAngle => _bodyTrack;
    public float LeanDegrees => _lean != null ? _lean.Degrees : 0f;
    public int AttackCooldown => _attackCooldown;
    public bool NeedsLanding => _needsLanding;
    public string CurrentNerveName => NerveName(_cur);
    public bool Dying => IsNerve(NerveDie);
    public bool BallFlying => _ball == BallStates.Flying;
    public bool BallVisible => _ballNode != null && _ballNode.Visible;
    public Vector3 BallPosition => _ballPos / U;
    public int Spits { get; private set; }
    public int Splats { get; private set; }
    public int BallHits { get; private set; }
    public bool OnWaterSurface => _waterUnder;
    public int ColumnBursts { get; private set; }

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _flags150 = 17;
        _planeMask = CollisionMask;
        if (_model != null) foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        if (_skel != null && _skel.FindBone(BodyJoint) >= 0 && !Engine.IsEditorHint()) { _lean = new SmsBoneRoll { Name = "Lean", BoneName = BodyJoint }; _skel.AddChild(_lean); }
        _ballNode = GetNodeOrNull<Node3D>("GoopBall");
        if (_ballNode != null)
        {
            _ballNode.TopLevel = true; _ballNode.Visible = false;
            _ballModel = _ballNode.GetNodeOrNull<Node3D>("Model");
            _ballAnim = _ballModel?.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            if (_ballModel != null) _ballModel.Scale = Vector3.One * (BallScale / U);
        }
        if (DizzyFxScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Fx131/PoiDizzy.tscn")) DizzyFxScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Fx131/PoiDizzy.tscn");
        if (WaterColumnFxScene == null && ResourceLoader.Exists("res://assets/Sms/Fx/WaterSplashSmall.tscn")) WaterColumnFxScene = GD.Load<PackedScene>("res://assets/Sms/Fx/WaterSplashSmall.tscn");
        if (DebugPrint) GD.Print($"[SMS gesso] {Name}: kind {Kind}; skel {_skel?.Name} lean {_lean != null}; ball {_ballNode != null} clips {(_ballAnim != null ? string.Join(",", _ballAnim.GetAnimationList()) : "-")}");
    }

    /// <summary>TGesso::load + reset: the facing from the placed yaw, the state by the kind, the body's own yaw zeroed for the beam kind, wait1 at a frame offset
    /// by the slot (the game: index / capacity).</summary>
    protected override void Reset()
    {
        base.Reset();
        _stayYaw = _spawnYaw;
        _state = Kind == Kinds.Regular ? States.BeamChilling : States.Wandering;
        if (Kind == Kinds.Regular) _yaw = 0f;
        _rightSideUp = true; _needsLanding = false; _freezeHitAgain = false; _punchSent = false; _spat = false;
        _beamY = Pos().Y; _hosei = 0f; _bodyTrack = 90f; _turnAngle = 0f;
        _attackCooldown = 0; _pollTimer = 0; _findCooldown = 0; _knock = Vector3.Zero;
        _waterUnder = false; _onWater = false; _landedWater = false; _dispY = 0f;
        _marchSpeed *= SpeedScale;
        CollisionMask = _state == States.BeamChilling ? 0 : _planeMask;
        BallKill();
        BoundFx(ref _dizzyFx, null, false);
        SetBck(BckWait);
        SeekBck(Frames[BckWait] * ((GetInstanceId() % 16) / 16f));
        ApplyLook();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Engine.IsEditorHint()) _dispY = 0f;
        base._PhysicsProcess(delta);
        if (!_dead && (_state == States.BeamChilling || _state == States.Rolling || _onWater)) _airborne = false;
    }

    // ------------------------------------------------------------------------------------------------------------------ the tick hooks
    protected override void MoveObjectPre()
    {
        Vector3 pM = GlobalPosition + Vector3.Up * (_dispY / U);
        float surfM = float.NegativeInfinity;
        _waterUnder = _state != States.BeamChilling && _state != States.Rolling && WaterSurfaceAt(pM, out surfM) && surfM * U > _groundY + 1f;
        if (_waterUnder) _waterSurfU = surfM * U; else _onWater = false;
        if (_onWater || _state == States.BeamChilling || _state == States.Rolling) _airborne = false;
        if (_landedWater) { _landedWater = false; _vel = Vector3.Zero; }
    }

    /// <summary>TGesso::setBehavior + bind: the attack cooldown, the landing flag, the beam states' pin and the lean, the state 3 -> 4 hand-over, the water
    /// floor, the ball's step and the effects' upkeep.</summary>
    protected override void SetBehavior()
    {
        if (_attackCooldown > 0) _attackCooldown++;
        if (_attackCooldown > 200) _attackCooldown = 0;
        if (_airborne && Pos().Y > _groundY + 250f && _cur != NerveGenerate) _needsLanding = true;
        if (!_airborne && _needsLanding && (_cur == NerveGraphWander || _cur == NerveAttack)) PushNerve(NerveLand);
        if (!_airborne) _needsLanding = false;
        // bind
        switch (_state)
        {
            case States.Falling:
                if (IsBck(BckTurnHit) || ThroughHoseiDistY * 0.5f + Pos().Y + _dispY < _beamY) { _state = States.FreeFall; CollisionMask = _planeMask; }
                break;
            case States.Rolling:
                _hosei = 0f;
                PinBeam();
                break;
            case States.BeamChilling:
                PinBeam();
                break;
            default:
                WaterFloor();
                break;
        }
        if (_state != States.Wandering) TrackBody();
        if (_lean != null)
        {
            bool use = _state != States.Wandering && _model != null;
            _lean.WorldAxis = _model != null ? _model.GlobalBasis.X.Normalized() : Vector3.Right;
            _lean.Degrees = use ? Mathf.Clamp(_bodyTrack - 90f, -Prm.BodyAngMax, Prm.BodyAngMax) : 0f;
        }
        BallTick();
        BoundFx(ref _dizzyFx, DizzyFxScene, IsNerve(NerveGFreeze) && IsBck(BckMahiLoop) && !_airborne && !_dead);
    }
    /// <summary>bind for BEAM_CHILLING / ROLLING: mPosition.y = unk1A4 - unk1D0; no velocity, no gravity.</summary>
    private void PinBeam()
    {
        _vel = Vector3.Zero; _lin = Vector3.Zero; _airborne = false;
        GlobalPosition = new Vector3(GlobalPosition.X, (_beamY - _hosei) / U, GlobalPosition.Z);
        _dispY = 0f;
    }
    /// <summary>A water surface as the floor (the drown): a fall meeting it lands on it; a resting body is held there.</summary>
    private void WaterFloor()
    {
        if (!_waterUnder) { _dispY += _lin.Y + _vel.Y; return; }
        float yU = GlobalPosition.Y * U + _dispY;
        float nextY = yU + _lin.Y + _vel.Y;
        if (_onWater)
        {
            if (_vel.Y > 0f) _onWater = false;
            else { _vel.Y = _waterSurfU - yU - _lin.Y; _vel.X = 0f; _vel.Z = 0f; _airborne = false; }
        }
        else if (_vel.Y <= 0f && nextY <= _waterSurfU + 0.05f)
        {
            _vel.Y = _waterSurfU - yU - _lin.Y;
            _onWater = true; _landedWater = true; _airborne = false;
        }
        _dispY += _lin.Y + _vel.Y;
    }
    /// <summary>bind's tail (not wandering): the body tracking angle eases toward the angle between the facing's side axis and Mario's direction.</summary>
    private void TrackBody()
    {
        if (_mario == null) return;
        float f1 = _rightSideUp ? 1f : -1f;
        Vector3 side = -PolarXZ(SightDirection() + 90f, 1f) * f1;
        Vector3 to = MarioFeetU() - Pos(); to.Y = 0f;
        float angle = MathF.Abs(Mathf.RadToDeg(MathF.Atan2(side.Cross(to).Length(), side.Dot(to))));
        if (_bodyTrack < angle) { _bodyTrack += BodyRotSpeed; if (_bodyTrack > angle) _bodyTrack = angle; }
        else if (_bodyTrack > angle) { _bodyTrack -= BodyRotSpeed; if (_bodyTrack < angle) _bodyTrack = angle; }
    }
    private Vector3 MarioFeetU() => _mario != null ? _mario.SmsFeetWorld() * U : Pos();
    /// <summary>getSightDirection: the facing yaw -- mStayYaw + the turns for the beam kind; the body's yaw wandering (DEVIATION: the game passes 0 there).</summary>
    private float SightDirection() => _state == States.Wandering ? _yaw : _stayYaw + _yaw;
    /// <summary>MsIsInSight(eye, direction, target, length, angle, aware) with an explicit direction.</summary>
    private bool InSightFrom(float dirYaw, Vector3 target, float length, float angle, float aware)
    {
        Vector3 d = target - Pos();
        float d2 = d.LengthSquared();
        if (d2 < aware * aware) return true;
        return d2 < length * length && MathF.Abs(AngleDiff(YawOf(d), dirYaw)) < angle * 0.5f;
    }
    private void BoundFx(ref Node3D slot, PackedScene scene, bool on)
    {
        if (on && slot == null && scene != null)
        {
            slot = scene.Instantiate<Node3D>();
            AddChild(slot);
            slot.Position = Vector3.Up * (Prm.DamageHeight * 0.6f / U);
            slot.Scale = Vector3.One * _scale;
        }
        else if (!on && slot != null) { slot.QueueFree(); slot = null; }
    }

    /// <summary>calcRootMatrix: wandering, TSpineEnemy's; else the model at (x, mPosition.y + unk1D0, z) with yaw mRotation.y + mStayYaw.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        if (_state == States.Wandering) { base.ApplyLook(); return; }
        Basis b = new Basis(Vector3.Up, Mathf.DegToRad(_yaw + _stayYaw)) * Basis.FromScale(LookScale * _modelScale);
        Vector3 at = new Vector3(GlobalPosition.X, GlobalPosition.Y + _hosei / U, GlobalPosition.Z);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, at); else _model.Transform = new Transform3D(b, Vector3.Up * (_hosei / U));
    }

    // ------------------------------------------------------------------------------------------------------------------ the game's virtuals
    protected override void SetWalkAnm() => SetBck(BckRun);
    protected override void SetRunAnm() => SetBck(BckRun);
    protected override void SetWaitAnm()
    {
        if (_state == States.Wandering || _rightSideUp) { SetBck(BckWait); _hosei = 0f; }
        else { SetBck(BckTurnWait); _hosei = Prm.DamageHeight * _bodyScale; }
    }
    protected override void SetDeadAnm()
    {
        if (_waterUnder) { if (_model != null) _model.Visible = false; }           // LIVE_FLAG_HIDDEN on a water surface
        else if (Kind == Kinds.Surf) SetBck(BckSurf);
        else SetBck(BckDown);
    }
    protected override void SetAfterDeadEffect() { BallKill(); base.SetAfterDeadEffect(); }
    /// <summary>genRandomItem: the surf death would drop a surfboard (not ported); else the base's.</summary>
    protected override void GenRandomItem() { if (IsBck(BckSurf)) return; base.GenRandomItem(); }
    protected override float GravityY() => _state != States.Wandering ? (_rightSideUp ? Prm.DropGravityY * 0.5f : Prm.DropGravityY) : base.GravityY();
    /// <summary>TSurfGesso::isFindMario false; else the base's.</summary>
    protected override bool IsFindMario(float k) => Kind != Kinds.Surf && base.IsFindMario(k);
    /// <summary>TGesso::walkBehavior: the walk only past clip frame 20; mode 2 spits; the find cooldown counts.</summary>
    protected override void WalkBehavior(int mode, float k)
    {
        if (BckFrame() > 20f) base.WalkBehavior(mode, k);
        if (mode == 2) PolluteBehavior();
        if (_findCooldown > 0 && _findCooldown < 300) _findCooldown++; else _findCooldown = 0;
    }
    /// <summary>behaveToFindMario: the goal Mario; GraphWander, Attack, and FindMario (search1) once per 300 ticks.</summary>
    protected override void BehaveToFindMario()
    {
        if ((_flags150 & 2) != 0) { base.BehaveToFindMario(); return; }
        SetGoalMario();
        PushAfterCurrent(NerveGraphWander);
        PushAfterCurrent(NerveAttack);
        if (_findCooldown == 0) { PushAfterCurrent(NerveFindMario); _findCooldown = 1; }
    }
    /// <summary>TGesso::attackToMario: sitting, the ATTACK and the cooldown; wandering, PostAttack + Punch pushed, the punch landing at frame 10; frozen / dying nothing
    /// unless out of the mahi loop.</summary>
    protected override void AttackToMario()
    {
        if (_state != States.Wandering) { SendAttackMsgToMario(); _attackCooldown = 1; return; }
        if (_cur != NervePunch && _cur != NerveGFreeze && _cur != NerveDie) { _attackCooldown = 1; PushNerve(NervePostAttack); PushNerve(NervePunch); _punchSent = false; PunchSound?.Play(); }
        if (_cur == NervePunch)
        {
            if (IsBck(BckPunch) && BckFrame() >= 10f && !_punchSent) { _punchSent = true; SendAttackMsgToMario(); }   // checkPass(10) on the punch clip (the nerve's first tick sets it)
            return;
        }
        if (!IsBck(BckMahiLoop)) SendAttackMsgToMario();
    }
    /// <summary>doKeepDistance: paralysed (mahi_loop) with Mario not 50 u above the body point.</summary>
    private bool DoKeepDistance() => IsBck(BckMahiLoop) && _mario != null && MarioFeetU().Y <= Pos().Y + 50f;
    protected override void HitCheckMario()
    {
        base.HitCheckMario();
        if (_mario == null || !DoKeepDistance() || !GetCollisionLayerValue(1)) return;
        Vector3 g = Pos(), m = MarioFeetU();
        if (CheckDistance(m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight, g, Prm.DamageRadius * _bodyScale, Prm.DamageHeight * _bodyScale))
            _mario.SmsKeepDistance(g, Prm.DamageRadius * _bodyScale);
    }
    /// <summary>TGesso::kill: never while hanging; the ball gone.</summary>
    public override void Kill()
    {
        if (!_rightSideUp) return;
        base.Kill();
        BallKill();
    }
    /// <summary>receiveMessage(SPRAYED_BY_WATER) with TGesso::behaveToWater: every hit counts (the cooldown reset); a hit while frozen is remembered (unk165); not
    /// dying / falling / frozen / rolling: the sitter is knocked off its edge (upright: away from Mario at HitWaterSpXZ, at least 5 per axis) into Fall, the
    /// walker into Freeze.</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        _sprayCooldown = 0;
        _hp = Math.Max(0, _hp - 1);
        _freezeHitAgain = IsNerve(NerveGFreeze);
        if (IsNerve(NerveDie) || IsNerve(NerveFall) || IsNerve(NerveGFreeze) || _state == States.Rolling) return true;
        if (_state == States.BeamChilling)
        {
            if (_rightSideUp)
            {
                Vector3 d = new Vector3(Pos().X - MarioFeetU().X, 0f, Pos().Z - MarioFeetU().Z);
                if (d.X == 0f && d.Z == 0f) d.X = 1f;
                d = d.Normalized();
                d.X *= Prm.HitWaterSpXZ; if (MathF.Abs(d.X) < 5f) d.X = d.X > 0f ? 5f : -5f;
                d.Z *= Prm.HitWaterSpXZ; if (MathF.Abs(d.Z) < 5f) d.Z = d.Z > 0f ? 5f : -5f;
                d.Y *= Prm.HitWaterSpY;
                _vel = d;
                GlobalPosition += Vector3.Up * (2f / U);
                _airborne = true;
            }
            PushNerve(NerveFall);
        }
        else PushNerve(NerveGFreeze);
        return true;
    }
    protected override void BehaveToWater() { }
    protected override bool WallHopAllowed() => _state == States.Wandering;

    // ------------------------------------------------------------------------------------------------------------------ the sitter (rollCheck, the turn, polluteBehavior)
    /// <summary>rollCheck: Mario in sight along the facing and under the body point (over it hanging) -> no collision, ROLLING; within TurnLength, upright and out of
    /// sight -> TURN.</summary>
    private void RollCheck()
    {
        if (_mario == null) return;
        Vector3 m = MarioFeetU();
        if (_attackCooldown == 0 && InSightFrom(SightDirection(), m, Prm.SearchLengthOnObj, Prm.SearchAngleOnObj, Prm.SearchAwareOnObj))
        {
            if ((_rightSideUp && Pos().Y > m.Y + 10f) || (!_rightSideUp && Pos().Y < m.Y - 10f))
            {
                SetCollisionLayerValue(1, false);
                _state = States.Rolling;
                PushNerve(NerveRolling);
                _attackCooldown = 0;
                RollSound?.Play();
                if (DebugPrint) GD.Print($"[SMS gesso] {Name}: rolling {(_rightSideUp ? "under" : "up onto")} the edge (Mario {(m.Y < Pos().Y ? "below" : "above")})");
            }
        }
        Vector3 d = m - Pos(); d.Y = 0f;
        if (d.LengthSquared() < Prm.TurnLength * Prm.TurnLength && _rightSideUp && !InSightFrom(SightDirection(), m, Prm.SearchLengthOnObj, Prm.SearchAngleOnObj, 0f))
            PushNerve(NerveTurn);
    }
    private void RollEnd()
    {
        SetCollisionLayerValue(1, true);
        _state = States.BeamChilling;
        _rightSideUp = !_rightSideUp;
    }
    private void TurnIn() { SetBck(BckTurn180); _turnAngle = 0f; SetCollisionLayerValue(1, false); }
    private bool Turning()
    {
        const float step = 7.2f;
        if (_turnAngle + step <= 180f) { _bodyTrack = 90f; _yaw += step; _turnAngle += step; return false; }
        _turnAngle = 180f;
        return BckEnded();
    }
    private void TurnOut() { _turnAngle = 0f; SetCollisionLayerValue(1, true); }
    private void ModifyRotate() { _stayYaw *= 0.8f; _yaw *= 0.8f; }
    private void FallEnd()
    {
        _state = States.Wandering;
        CollisionMask = _planeMask;
        _yaw = 0f; _stayYaw = 0f;
        _rightSideUp = true;
        _hosei = 0f;
    }
    /// <summary>polluteBehavior: the timer past PollutionInterval, the clip at its end, not already spitting, Mario in sight within PollutionLength -> Pollute.</summary>
    private void PolluteBehavior()
    {
        _pollTimer++;
        if (!BckEnded() || IsNerve(NervePollute) || _pollTimer <= Prm.PollutionInterval || _mario == null) return;
        if (!InSightFrom(SightDirection(), MarioFeetU(), Prm.PollutionLength, Prm.SearchAngleOnObj, 0f)) return;
        _pollTimer = 0;
        BallKill();
        PushNerve(NervePollute);
    }
    /// <summary>checkDropInWater: 50 ticks of the knock's parabola; a water surface at the landing.</summary>
    private bool CheckDropInWater()
    {
        Vector3 p = Pos(), v = _vel;
        for (int i = 0; i < 50; i++) { p += v; v.Y -= GravityY(); if (p.Y < _groundY) break; }
        float floorU = GroundAt(p);                                         // gpMap->checkGround at the landing point (the pool's floor, not the dock's)
        return WaterSurfaceAt(p / U, out float s, 1.5f) && s * U > floorU + 1f;
    }
    /// <summary>gpMap->checkGround(x, y + 2 x head height, z) at a point: the floor under it, units (-1e9 none).</summary>
    private float GroundAt(Vector3 pU)
    {
        var space = GetWorld3D().DirectSpaceState;
        Vector3 from = (pU + Vector3.Up * (Prm.HeadHeight * 2f)) / U;
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 80f, _planeMask);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? ((Vector3)hit["position"]).Y * U : -1e9f;
    }
    private void ColumnWater()
    {
        Vector3 at = new Vector3(GlobalPosition.X, (_waterUnder ? _waterSurfU : _groundY) / U, GlobalPosition.Z);
        if (SpawnBurst(WaterColumnFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), at)) != null) ColumnBursts++;
    }

    // ------------------------------------------------------------------------------------------------------------------ the goop ball (TGessoPolluteObj)
    private Vector3 MouthWorldU()
    {
        if (_skel != null) { int b = _skel.FindBone(MouthJoint); if (b >= 0) return (_skel.GlobalTransform * _skel.GetBoneGlobalPose(b)).Origin * U; }
        return Pos() + Vector3.Up * (Prm.DamageHeight * 0.7f);
    }
    /// <summary>setPolluteGoal: Mario's position +-100 u, the parabola at PolluteObjSpeed (0 hanging) under PolluteObjGravity.</summary>
    private void SetPolluteGoal()
    {
        float speed = _rightSideUp ? Prm.PolluteObjSpeed : 0f;
        Vector3 goal = MarioFeetU() + new Vector3(Lerp(-100f, 100f, Rand()), 0f, Lerp(-100f, 100f, Rand()));
        _ballVel = CalcVelocityToJumpToY(goal, speed, Prm.PolluteObjGravity);
    }
    /// <summary>SMSCalcJumpVelocityY from the body point.</summary>
    private Vector3 CalcVelocityToJumpToY(Vector3 goalU, float speedY, float gravity)
    {
        Vector3 pos = Pos();
        float currY = pos.Y, vy = speedY; int ticks = 1;
        for (int guard = 0; guard < 100000; guard++) { currY += vy; if (vy < 0f && currY <= goalU.Y) break; vy -= gravity; if (vy < -40f) vy = -40f; ticks++; }
        return new Vector3((goalU.X - pos.X) / ticks, speedY, (goalU.Z - pos.Z) / ticks);
    }
    /// <summary>appear: the ball at the mouth, in the loop clip, held until the spit.</summary>
    private void BallAppear()
    {
        if (_ballNode == null) return;
        _ball = BallStates.Held; _ballGrounded = false;
        _ballDir = PolarXZ(_state == States.Wandering ? _yaw : _yaw + _stayYaw, 1f);
        _ballNode.Visible = true;
        if (_ballAnim != null && _ballAnim.HasAnimation(BallClipLoop)) { var a = _ballAnim.GetAnimation(BallClipLoop); a.LoopMode = Animation.LoopModeEnum.Linear; _ballAnim.Play(BallClipLoop); }
        BallSet();
    }
    /// <summary>set(): the ball at the mouth + 100 u along its flight direction (the facing until it flies).</summary>
    private void BallSet()
    {
        Vector3 mouth = MouthWorldU();
        _ballPos = new Vector3(mouth.X + _ballDir.X * 100f, mouth.Y, mouth.Z + _ballDir.Z * 100f);
        BallPlace();
    }
    private void BallPlace()
    {
        if (_ballNode == null) return;
        _ballNode.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(YawOf(_ballDir))), _ballPos / U);
    }
    /// <summary>pollute(): the ball flies -- vy -3 when spat hanging; its yaw along the flight; from the mouth + 100 u along the facing.</summary>
    private void Pollute()
    {
        if (_state != States.Wandering && !_rightSideUp) _ballVel.Y = -3f;
        Vector3 flat = new Vector3(_ballVel.X, 0f, _ballVel.Z);
        _ballDir = flat.LengthSquared() > 1e-6f ? flat.Normalized() : PolarXZ(_state == States.Wandering ? _yaw : _yaw + _stayYaw, 1f);
        Vector3 mouth = MouthWorldU();
        Vector3 face = PolarXZ(_state == States.Wandering ? _yaw : _yaw + _stayYaw, 100f);
        _ballPos = new Vector3(mouth.X + face.X, mouth.Y, mouth.Z + face.Z);
        _ball = BallStates.Flying; _ballGrounded = false; _ballCount = 0;
        if (_ballNode != null) _ballNode.Visible = true;
        if (_ballAnim != null && _ballAnim.HasAnimation(BallClipLoop)) _ballAnim.Play(BallClipLoop);
        BallPlace();
        Spits++;
        SpitSound?.Play();
        if (DebugPrint) GD.Print($"[SMS gesso] {Name}: spits the goop ball at {_ballVel} u/tick from {_ballPos / U}");
    }
    private void BallKill() { _ball = BallStates.Idle; if (_ballNode != null) _ballNode.Visible = false; }
    /// <summary>TEnemyAttachment::moveObject for the ball: held -> set(); flying -> the bind (PolluteObjGravity, the ground, a wall) and the ATTACK on Mario;
    /// splatting -> rebirth's count (the burst, the pollution at 10, gone past 20, sinking).</summary>
    private void BallTick()
    {
        switch (_ball)
        {
            case BallStates.Held:
                BallSet();
                return;
            case BallStates.Flying:
            {
                Vector3 next = _ballPos + _ballVel;
                _ballVel.Y -= Prm.PolluteObjGravity;
                if (_ballVel.Y < -40f) _ballVel.Y = -40f;
                var space = GetWorld3D().DirectSpaceState;
                var q = PhysicsRayQueryParameters3D.Create(_ballPos / U, next / U, _planeMask);
                q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
                var hit = space.IntersectRay(q);
                bool hitMap = hit.Count > 0 && hit["collider"].AsGodotObject() is not CharacterBody3D;
                var gq = PhysicsRayQueryParameters3D.Create(next / U + Vector3.Up * 0.2f, next / U + Vector3.Down * 60f, _planeMask);
                gq.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
                var g = space.IntersectRay(gq);
                _ballGroundY = g.Count > 0 ? ((Vector3)g["position"]).Y * U + 1f : -1e9f;
                if (hitMap) { next = (Vector3)hit["position"] * U; BallHitGround(next); return; }
                if (next.Y + _ballVel.Y <= _ballGroundY) { next.Y = _ballGroundY; BallHitGround(next); return; }
                _ballPos = next;
                BallPlace();
                // sendMessage: the 10 x 10 hit actor on Mario's damage cylinder -> ATTACK, the ball gone
                if (_mario != null && IsInstanceValid(_mario) && CheckDistance(_ballPos, 10f, 10f, MarioFeetU(), SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight))
                {
                    _mario.TakeDamage(_ballPos / U, DamageProfile);
                    BallHits++;
                    BallKill();
                    if (DebugPrint) GD.Print($"[SMS gesso] {Name}: the goop ball hit Mario");
                }
                return;
            }
            case BallStates.Splat:
            {
                if (_ballCount == 0)
                {
                    if (GoopSplatFxScene != null) SpawnBurst(GoopSplatFxScene, new Transform3D(Basis.FromScale(Vector3.One * BallScale), _ballPos / U));
                    GoopLandSound?.Play();
                }
                _ballCount++;
                if (_ballCount == 10)
                {
                    _ballVel.Y = -15f;
                    SmsPollutionMap.Pollute(_ballPos / U, PollRange * 32f * 0.5f);
                    Splats++;
                    if (DebugPrint) GD.Print($"[SMS gesso] {Name}: the goop ball splats at {_ballPos / U}: a {PollRange * 16f:F0} u stamp");
                }
                if (_ballCount > 20) { BallKill(); return; }
                _ballPos += _ballVel;
                if (_ballPos.Y < _ballGroundY - 30f) _ballVel.Y = 0f;
                BallPlace();
                return;
            }
        }
    }
    private void BallHitGround(Vector3 at)
    {
        _ballPos = at; _ballVel = new Vector3(0f, -0.3f, 0f); _ballGrounded = true;
        _ball = BallStates.Splat; _ballCount = 0;
        BallPlace();
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveStay:                                      // TNerveGessoStay
                if (_time == 0) SetWaitAnm();
                RollCheck();
                PolluteBehavior();
                return false;

            case NerveGFreeze:                                   // TNerveGessoFreeze
                if (_time == 0)
                {
                    Vector3 k = Pos() - MarioFeetU();
                    k = k.LengthSquared() > 1e-6f ? k.Normalized() : Vector3.Back;
                    k.Y = 5f; k.X *= 4f; k.Z *= 4f;
                    _vel = k; GlobalPosition += Vector3.Up * (2f / U); _airborne = true; _knock = k;
                    if (CheckDropInWater()) { PushAfterCurrent(NerveFall); return true; }
                    SetBck(BckMahiStart);
                    HitSound?.Play();
                }
                if (BckEnded())
                {
                    if (IsBck(BckMahiStart)) SetBck(BckMahiLoop);
                    else if (IsBck(BckMahiLoop))
                    {
                        bool again = _freezeHitAgain; _freezeHitAgain = false;        // unsetUnk165
                        if (_time > Prm.FreezeWait && !again) SetBck(BckMahiEnd); else SeekBck(0f);
                    }
                    else if (IsBck(BckMahiEnd)) return true;
                }
                if (!_airborne)
                {
                    if (_knock.Y >= 1.5f) { _knock.Y *= 0.3f; GlobalPosition += Vector3.Up * (2f / U); _airborne = true; _vel = _knock; }
                }
                else if (_goalIsMario) WalkToCurPathNode(0f, _turnSpeed, 0f);
                return false;

            case NervePunch:                                     // TNerveGessoPunch
                if (_time == 0) { SetBck(BckPunch); _punchSent = false; }
                return BckEnded();

            case NervePollute:                                   // TNerveGessoPollute
                if (_time < 2) SetPolluteGoal();
                if (IsBck(BckGero) || IsBck(BckTurnGero))
                {
                    if (BckFrame() >= 50f && !_spat) { _spat = true; Pollute(); }
                    if (BckEnded()) return true;
                }
                else
                {
                    SetBck(_state == States.Wandering || _rightSideUp ? BckGero : BckTurnGero);
                    _spat = false;
                    if (SpitFxScene != null) SpawnBurst(SpitFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), MouthWorldU() / U));
                    BallAppear();
                }
                return false;

            case NerveFall:                                      // TNerveGessoFall
                if (_time == 0)
                {
                    SetGoalPoint(MarioFeetU());
                    if (_state == States.Wandering)
                    {
                        Vector3 d = Pos() - MarioFeetU(); d.Y = 0f;
                        if (d.X == 0f && d.Z == 0f) d.Z = 1f;
                        d = d.Normalized() * 4f;
                        _vel = d; _airborne = true;
                        SetBck(BckHit);
                    }
                    else
                    {
                        // STATE_FALLING: the game's bind integrates the knock with no ground check until the body is 100 u under the placed height (or the
                        // hanging clip) -- the knocked sitter DROPS THROUGH its walkway; the mask stays off until SetBehavior hands over to the normal bind
                        if (_rightSideUp) SetBck(BckHit);
                        else { SetBck(BckTurnHit); GlobalPosition += Vector3.Down * (ThroughHoseiDistY / U); }
                        _state = States.Falling;
                        CollisionMask = 0;
                        _airborne = true;
                    }
                    HitSound?.Play();
                }
                else if (_state == States.Wandering)
                {
                    if (BckEnded())
                    {
                        if (IsBck(BckHit))
                        {
                            Vector3 d = Pos() - MarioFeetU(); d.Y = 0f;
                            if (d.X == 0f && d.Z == 0f) d.Z = 1f;
                            d = d.Normalized();
                            _vel = new Vector3(d.X * 6f, 5f, d.Z * 6f); _airborne = true;
                            SetBck(BckFall);
                        }
                        else if (!_airborne)
                        {
                            if (_waterUnder) { ColumnWater(); PushAfterCurrent(NerveDie); return true; }
                            if (IsBck(BckFall)) SetBck(BckLand);
                            else if (IsBck(BckLand)) { PushAfterCurrent(NerveGraphWander); return true; }
                        }
                    }
                }
                else
                {
                    if (IsBck(BckHit) || IsBck(BckTurnHit))
                    {
                        _hosei *= 0.8f;
                        if (BckEnded()) { _state = States.FreeFall; CollisionMask = _planeMask; SetBck(BckFall); }
                    }
                    else if (IsBck(BckFall))
                    {
                        _hosei *= 0.8f;
                        if (!_airborne)
                        {
                            if (_waterUnder) { ColumnWater(); PushAfterCurrent(NerveDie); return true; }
                            if (BckEnded()) SetBck(BckLand);
                        }
                    }
                    else if (IsBck(BckLand))
                    {
                        ModifyRotate();
                        if (BckEnded()) { FallEnd(); PushAfterCurrent(NerveGraphWander); return true; }
                    }
                }
                _vel.X *= 0.99f; _vel.Z *= 0.99f;
                return false;

            case NerveRolling:                                   // TNerveGessoRolling
                if (_time < 2) SetBck(_rightSideUp ? BckTurnDown : BckTurnUp);
                else if (BckEnded()) { RollEnd(); return true; }
                return false;

            case NerveFindMario:                                 // TNerveGessoFindMario
                if (_time == 0) SetBck(BckSearch);
                return BckEnded();

            case NerveLand:                                      // TNerveGessoLand
                if (_time == 0) SetBck(BckStairLand);
                if (BckEnded()) { _needsLanding = false; return true; }
                return false;

            case NerveTurn:                                      // TNerveGessoTurn
                if (_time < 2) TurnIn();
                else if (Turning()) { TurnOut(); return true; }
                return false;
        }
        return base.ExecuteNerve(n);
    }

    protected override void Dead()
    {
        BallKill();
        BoundFx(ref _dizzyFx, null, false);
        base.Dead();
    }
}
