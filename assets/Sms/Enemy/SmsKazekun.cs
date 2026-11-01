using System;
using Godot;

/// <summary>
/// WIND SPIRIT -- TKazekun (Enemy/Kazekun.cpp; JP かぜくん, the kazekun archive) on <see cref="SmsSmallEnemy"/> with the shipped /enemy/kazekun.prm (SMS
/// pass 143, feature/enemy-ricco). Ricco Harbor's wisp of wind: invisible at its place until Mario comes within AppearDist, it puffs into view, circles
/// over his head for a while, tumbles in place to wind up, then dashes through him and vanishes, to come back from its place a few seconds later. A
/// splash of water while it is out knocks it down and keeps it away far longer.
///
/// THE GAME. TKazekun is a TSmallEnemy (ACTOR_TYPE_KAZEKUN) with LIVE_FLAG_UNK10 from its constructor: NO bind -- its own bind() is mPositionDelta +=
/// mVelocity, nothing else: no gravity, no ground, no walls (it passes through everything). init() does not call TSmallEnemy::init and reset() does not
/// call TSmallEnemy::reset: mBodyScale stays 1, the hit points 1, mHeadHeight 40 and mBodyRadius 50 from initCollision; the manager sets the attack and
/// damage cylinders 50 x 40 (not scaled). /enemy/kazekun.prm: AppearDist 1000, AroundDist 800, AroundSpeed 10, AroundTime 524, AttackSpeed 60, AirFric
/// 0.97, ResetTime 300, ResetTimeHitting 1500 (the default: not in the shipped prm), PoseTime 90, DicideTiming 0.1, TurnOffsetY 200, LostOffsetYUp 2000,
/// LostOffsetYDown 600, PoseSpeed 13.887, PoseOmegaRate 0.12. The clips (set by name): kazekun_appear (59), kazekun_attack (20, loop), kazekun_hit (19),
/// kazekun_vanish (59), kazekun_wait (70, loop). The body's orientation is a QUATERNION (unk1A0), not a yaw: calcRootMatrix = the quaternion + mPosition
/// (no scale); while it has wind (Turn / PreAttack / Attack / HitWater) ms_kaze_wind + ms_kaze_blur are bound to the model's matrix. getAroundQuat(dir,
/// rate): the frame with Z along dir (X = up x Z, Y = Z x X), then turned about its Y by (2 - rate) x 90 deg -- rate 2 faces dir, 1 is broadside, 0 faces
/// away. The nerves: SEARCH (the default): tick 0 reset (the orientation identity, the body at its place, INVISIBLE); Mario within AppearDist (3D) ->
/// APPEAR: visible, the appear puff, the appear clip; its end -> TURN: the wait clip, the collision on; each tick flyAroundMario: toMario = Mario +
/// (0, TurnOffsetY, 0) - the body; climb = clamp(toMario.y, +-400) x 0.0025; the horizontal toMario gives the around-quat at rate |toMario| / AroundDist
/// (clamped 0..2: far away it heads in, at AroundDist it circles broadside, close up it turns away); the delta = the quat's forward with y = climb, x
/// (1 + |climb|) x AroundSpeed. isGiveUpAround (Mario under the PLACE by LostOffsetYDown or over it by LostOffsetYUp) -> DISAPPEAR; past AroundTime ->
/// PREATTACK: doAttackPose(start): the around-quat at rate 1 (broadside), the velocity = its forward x PoseSpeed; KAZEKUN_READY; each tick the
/// orientation spins about the body's own -X by pi x PoseOmegaRate (21.6 deg: a somersault every 17 ticks) and the velocity = the new forward x |v|
/// (small vertical loops, 37 u across: it tumbles in place); past PoseTime x DicideTiming the goal = Mario's position (set every tick: his spot at the
/// dash's start); past PoseTime -> ATTACK: the attack clip, the velocity = toward the goal at AttackSpeed; each tick the orientation slerps 0.1 toward
/// the velocity's frame (rate 2: nose along the dash) and the velocity x AirFric; under 1 u/tick -> DISAPPEAR with unk1B0 = ResetTime. DISAPPEAR: the
/// vanish clip, the appear puff, the velocity 0, the collision off; its end -> WAIT: invisible; past unk1B0 -> SEARCH. behaveToWater (Turn / PreAttack
/// / Attack only): the spine reset -> HITWATER: the hit clip, KAZEKUN_DOWN, the velocity 0 each tick; its end -> DISAPPEAR with unk1B0 =
/// ResetTimeHitting. attackToMario: the ATTACK only in Turn / PreAttack / Attack. isCollidMove false. setDeadAnm (a stomp: TSmallEnemy::kill -> the Die
/// nerve): the clip frozen at frame 0 as a one-frame clip -> dead after the nerve's 60 ticks, the drop table.
///
/// THE PORT. <c>_noBind</c> with the COLLISION MASK 0 (through everything, as the game's); the delta is the base's <c>_lin</c>: the orbit / pose write it,
/// SetBehavior adds <c>_vel</c> (the game's bind). The orientation is a Godot Quaternion with the game's formulas (the same right-handed Y-up frame, the
/// model's +Z forward); ApplyLook sets the model's basis from it with no body scale (LookScale 1). Hidden = the model off + the hit layer off. The hit
/// layer is on only in Turn / PreAttack / Attack. The Die nerve is the species' (the clip frozen, 60 ticks). DEVIATIONS (logged): the appear puff
/// defaults to the enemy death puff (ms_kaze_appear is a slot); the wind and blur bound effects are a slot; the sounds are slots; the clipped-out rules
/// are not ported.
/// </summary>
public partial class SmsKazekun : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ kazekun.prm
    public class KazekunParams : Params
    {
        public float AppearDist = 1000f, AroundDist = 400f, AroundSpeed = 30f, AttackSpeed = 30f, AirFric = 0.97f, DicideTiming = 0.1f, TurnOffsetY = 200f,
            LostOffsetYUp = 500f, LostOffsetYDown = 500f, PoseSpeed = 7.6f, PoseOmegaRate = 0.04f;
        public int AroundTime = 600, ResetTime = 300, ResetTimeHitting = 1500, PoseTime = 120;
    }
    /// <summary>The shipped /enemy/kazekun.prm (params.szs on his disc, sms_shipped_params.json) with TKazekun's own collision (initCollision + the manager's cylinders).</summary>
    public static readonly KazekunParams Prm = new KazekunParams
    {
        HeadHeight = 40f, BodyRadius = 50f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 100, ActiveEnemyNum = 10,
        DamageRadius = 50f, DamageHeight = 40f, AttackRadius = 50f, AttackHeight = 40f, BodyScaleLow = 1f, BodyScaleHigh = 1f, TurnSpeedLow = 5f, TurnSpeedHigh = 5f,
        MarchSpeedLow = 1f, MarchSpeedHigh = 1f, GenItemRate = 50f, GenEggRate = 10f,
        AppearDist = 1000f, AroundDist = 800f, AroundSpeed = 10f, AroundTime = 524, AttackSpeed = 60f, AirFric = 0.97f, ResetTime = 300, ResetTimeHitting = 1500,
        PoseTime = 90, DicideTiming = 0.1f, TurnOffsetY = 200f, LostOffsetYUp = 2000f, LostOffsetYDown = 600f, PoseSpeed = 13.88671875f, PoseOmegaRate = 0.12f,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the circling, the tumble and the dash speeds (the game: 10 / 13.9 / 60 u a tick).</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_kaze_appear: the puff where it appears and where it vanishes (an SmsFxBurst / one-shot GPUParticles3D scene). Empty = assets/Sms/Fx/EnemyDisap.tscn (the enemy death puff stands in).</summary>
    [Export] public PackedScene AppearFxScene;
    /// <summary>ms_kaze_wind + ms_kaze_blur: the wind streaks bound to the body while it is out (a looping GPUParticles3D scene as a child of the model). Empty = none.</summary>
    [Export] public PackedScene WindFxScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_KAZEKUN_READY (the wind-up), _DOWN (knocked down by water), and the bas tables' appear / attack / vanish / wait.</summary>
    [Export] public AudioStreamPlayer3D ReadySound, DownSound, AppearSound, AttackSound, VanishSound, WaitSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's 5 bcks; the bas table's order)
    private static readonly string[] Names = { "kazekun_appear", "kazekun_attack", "kazekun_hit", "kazekun_vanish", "kazekun_wait" };
    private static readonly int[] Frames = { 59, 20, 19, 59, 70 };
    private static readonly bool[] Loops = { false, true, false, false, true };
    protected const int BckAppear = 0, BckAttack = 1, BckHit = 2, BckVanish = 3, BckWait = 4;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves (TNerveKazekun*)
    protected const int NerveSearch = NerveSpecies, NerveAppear = NerveSpecies + 1, NerveTurn = NerveSpecies + 2, NervePreAttack = NerveSpecies + 3, NerveKAttack = NerveSpecies + 4,
        NerveDisappear = NerveSpecies + 5, NerveKWait = NerveSpecies + 6, NerveHitWater = NerveSpecies + 7;
    private static readonly string[] KazekunNerveNames = { "Search", "Appear", "Turn", "PreAttack", "Attack", "Disappear", "Wait", "HitWater" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + KazekunNerveNames.Length ? KazekunNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveSearch;

    // ------------------------------------------------------------------------------------------------------------------ state
    private Quaternion _quat = Quaternion.Identity;   // unk1A0: the body's orientation
    private int _waitTicks;                            // unk1B0: the Wait nerve's length (ResetTime after a dash, ResetTimeHitting after a hit)
    private Node3D _windFx;
    private static readonly Vector3 ModelForward = new Vector3(0f, 0f, 1f);   // the game's +Z
    /// <summary>For the drives.</summary>
    public string CurrentNerveName => NerveName(_cur);
    public int LatestNerve => _cur != NerveNone ? _cur : _prev;
    public string LatestNerveName => NerveName(LatestNerve);
    public bool Hidden => _model != null && !_model.Visible;
    public bool HasWind => LatestNerve == NerveTurn || LatestNerve == NervePreAttack || LatestNerve == NerveKAttack || LatestNerve == NerveHitWater;
    public bool CanBeHurt => LatestNerve == NerveTurn || LatestNerve == NervePreAttack || LatestNerve == NerveKAttack;   // isHitWater / isDamage
    public int WaitTicks => _waitTicks;
    public bool Dying => IsNerve(NerveDie);
    public Quaternion Orientation => _quat;
    public Vector3 Forward => _quat * ModelForward;
    public Vector3 HomeUnits => _spawnPos;
    public int Appearances { get; private set; }
    public int Puffs { get; private set; }

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        CollisionMask = 0;                                                               // bind(): mPositionDelta += mVelocity, nothing else -- through everything
        if (AppearFxScene == null && ResourceLoader.Exists("res://assets/Sms/Fx/EnemyDisap.tscn")) AppearFxScene = GD.Load<PackedScene>("res://assets/Sms/Fx/EnemyDisap.tscn");
        if (DebugPrint) GD.Print($"[SMS kazekun] {Name}: home {_spawnPos}");
    }
    /// <summary>TKazekun::reset: the orientation identity, the body at its place, invisible; the game's flag UNK10 (no bind) stays on.</summary>
    protected override void Reset()
    {
        base.Reset();
        _noBind = true; CollisionMask = 0;
        _scale = 1f;                                                                     // calcRootMatrix draws the model at scale 1 (the body scale is 1 too)
        _waitTicks = 0;
        HomeReset();
        SetBck(BckWait);
        ApplyLook();
    }
    private void HomeReset()
    {
        _quat = Quaternion.Identity;
        GlobalPosition = _spawnPos / U;
        _vel = Vector3.Zero; _lin = Vector3.Zero; _airborne = true;
        SetShown(false);
    }
    /// <summary>setVisible: LIVE_FLAG_HIDDEN + UNK8 (the model off, nothing performs or collides).</summary>
    private void SetShown(bool on)
    {
        if (_model != null) _model.Visible = on;
        if (!on) { SetCollisionLayerValue(1, false); BindWind(false); }
    }
    protected override Vector3 LookScale => Vector3.One;
    protected override bool IsCollidMove(Node3D other) => false;
    protected override bool WallHopAllowed() => false;

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (!_dead) BindWind(HasWind && !Hidden);
    }
    /// <summary>bind(): the velocity onto the position delta (after the nerve: TSmallEnemy::moveObject's control -> bind order).</summary>
    protected override void SetBehavior() => _lin += _vel;
    protected override void MoveObjectPre() { _airborne = true; }
    protected override float GravityY() => 0f;

    /// <summary>calcRootMatrix: the model's base matrix = the quaternion + mPosition, no scale.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Basis b = new Basis(_quat) * Basis.FromScale(Vector3.One * _modelScale);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, GlobalPosition); else _model.Transform = new Transform3D(b, Vector3.Zero);
    }
    private void BindWind(bool on)
    {
        if (on && _windFx == null && WindFxScene != null && _model != null) { _windFx = WindFxScene.Instantiate<Node3D>(); _model.AddChild(_windFx); _windFx.Position = Vector3.Zero; }
        else if (!on && _windFx != null) { _windFx.QueueFree(); _windFx = null; }
    }
    private void EmitAppearEffect()
    {
        if (SpawnBurst(AppearFxScene, new Transform3D(Basis.Identity, GlobalPosition)) != null) Puffs++;
    }
    private Vector3 MarioFeetU() => _mario != null ? _mario.SmsFeetWorld() * U : Pos();

    // ------------------------------------------------------------------------------------------------------------------ the game's math
    /// <summary>SMS_CalcToDirMatrix(dir, up) as a quaternion, then turned about its own Y by (2 - rate) x 90 deg (getAroundQuat).</summary>
    private static Quaternion AroundQuat(Vector3 dir, float rate)
    {
        Vector3 z = dir.LengthSquared() < 1e-10f ? ModelForward : dir.Normalized();
        Vector3 x = Vector3.Up.Cross(z);
        x = x.LengthSquared() < 1e-10f ? Vector3.Right : x.Normalized();
        Vector3 y = z.Cross(x).Normalized();
        Quaternion q = new Basis(x, y, z).GetRotationQuaternion();
        Quaternion r = new Quaternion(y, (2f - rate) * Mathf.Pi * 0.5f);
        return (q * r).Normalized();
    }
    private float AroundRate(Vector3 dir) => Mathf.Clamp(dir.Length() / Prm.AroundDist, 0f, 2f);
    /// <summary>flyAroundMario: the orbit over his head -- in when far, broadside at AroundDist, away when close; the climb toward his head + TurnOffsetY.</summary>
    private void FlyAroundMario()
    {
        Vector3 toMario = MarioFeetU() + new Vector3(0f, Prm.TurnOffsetY, 0f) - Pos();
        float climb = Mathf.Clamp(toMario.Y, -400f, 400f) * 0.0025f;
        toMario.Y = 0f;
        _quat = AroundQuat(toMario, AroundRate(toMario));
        Vector3 v = _quat * ModelForward;
        v.Y = climb;
        v *= (1f + MathF.Abs(climb)) * Prm.AroundSpeed * SpeedScale;
        _lin = v;                                                                        // mPositionDelta = velocity
    }
    /// <summary>doAttackPose: the wind-up -- broadside, then a somersault about the body's own -X every tick, the velocity along the turning forward.</summary>
    private void DoAttackPose(bool start)
    {
        Vector3 toMario = MarioFeetU() - Pos(); toMario.Y = 0f;
        if (start)
        {
            _quat = AroundQuat(toMario, 1f);
            _vel = _quat * new Vector3(0f, 0f, Prm.PoseSpeed * SpeedScale);
        }
        Vector3 spinAxis = _quat * new Vector3(-1f, 0f, 0f);
        Quaternion spin = new Quaternion(spinAxis.Normalized(), Mathf.Pi * Prm.PoseOmegaRate);
        _quat = (spin * _quat).Normalized();
        _vel = _quat * new Vector3(0f, 0f, _vel.Length());
    }
    /// <summary>doAttack: the dash at AttackSpeed toward the goal; the orientation easing (slerp 0.1) onto the velocity's frame.</summary>
    private void DoAttack(bool start)
    {
        if (start)
        {
            Vector3 d = Goal() - Pos();
            _vel = (d.LengthSquared() > 1e-10f ? d.Normalized() : _quat * ModelForward) * Prm.AttackSpeed * SpeedScale;
        }
        Quaternion target = AroundQuat(_vel, 2f);
        _quat = _quat.Slerp(target, 0.1f).Normalized();
    }
    private bool IsGiveUpAround()
    {
        float offsetY = MarioFeetU().Y - _spawnPos.Y;
        return offsetY < -Prm.LostOffsetYDown || Prm.LostOffsetYUp < offsetY;
    }
    private void ChangeBck(int i, AudioStreamPlayer3D sound) { SetBck(i); sound?.Play(); }

    // ------------------------------------------------------------------------------------------------------------------ the game's virtuals
    /// <summary>attackToMario: the ATTACK only while out (isDamage: Turn / PreAttack / Attack).</summary>
    protected override void AttackToMario() { if (CanBeHurt) SendAttackMsgToMario(); }
    /// <summary>behaveToWater: out (isHitWater) -> the spine reset to HitWater.</summary>
    protected override void BehaveToWater()
    {
        if (!CanBeHurt) return;
        ResetSpine();
        SetNext(NerveHitWater);
    }
    /// <summary>setDeadAnm: the frame controller made a one-frame clip at frame 0 -- the body frozen where it is.</summary>
    protected override void SetDeadAnm() { SeekBck(0f); SetBckPaused(true); }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveSearch:                                    // TNerveKazekunSearch
                if (_time == 0) HomeReset();
                {
                    Vector3 d = MarioFeetU() - Pos();
                    if (_mario != null && d.LengthSquared() <= Prm.AppearDist * Prm.AppearDist) { PushAfterCurrent(NerveAppear); return true; }
                }
                return false;

            case NerveAppear:                                    // TNerveKazekunAppear
                if (_time == 0) { SetShown(true); EmitAppearEffect(); ChangeBck(BckAppear, AppearSound); Appearances++; if (DebugPrint) GD.Print($"[SMS kazekun] {Name}: appears at {Pos()}"); }
                if (BckEnded()) { PushAfterCurrent(NerveTurn); return true; }
                return false;

            case NerveTurn:                                      // TNerveKazekunTurn
                if (_time == 0) { ChangeBck(BckWait, WaitSound); SetCollisionLayerValue(1, true); }
                FlyAroundMario();
                if (IsGiveUpAround()) { PushAfterCurrent(NerveDisappear); return true; }
                if (Prm.AroundTime < _time) { PushAfterCurrent(NervePreAttack); return true; }
                return false;

            case NervePreAttack:                                 // TNerveKazekunPreAttack
                if (_time == 0) { DoAttackPose(true); ReadySound?.Play(); }
                if (Prm.PoseTime * Prm.DicideTiming < _time) SetGoalPoint(MarioFeetU());
                DoAttackPose(false);
                if (Prm.PoseTime < _time) { PushAfterCurrent(NerveKAttack); return true; }
                return false;

            case NerveKAttack:                                   // TNerveKazekunAttack
                if (_time == 0) { ChangeBck(BckAttack, AttackSound); DoAttack(true); if (DebugPrint) GD.Print($"[SMS kazekun] {Name}: dashes at {_vel} u/tick toward {Goal()}"); }
                DoAttack(false);
                _vel *= Prm.AirFric;
                if (_vel.LengthSquared() < 1f) { PushAfterCurrent(NerveDisappear); _waitTicks = Prm.ResetTime; return true; }
                return false;

            case NerveDisappear:                                 // TNerveKazekunDisappear
                if (_time == 0) { ChangeBck(BckVanish, VanishSound); EmitAppearEffect(); _vel = Vector3.Zero; SetCollisionLayerValue(1, false); }
                if (BckEnded()) { PushAfterCurrent(NerveKWait); return true; }
                return false;

            case NerveKWait:                                     // TNerveKazekunWait
                if (_time == 0) SetShown(false);
                if (_waitTicks < _time) { PushAfterCurrent(NerveSearch); return true; }
                return false;

            case NerveHitWater:                                  // TNerveKazekunHitWater
                if (_time == 0) { ChangeBck(BckHit, DownSound); if (DebugPrint) GD.Print($"[SMS kazekun] {Name}: knocked down by water"); }
                if (BckEnded()) { PushAfterCurrent(NerveDisappear); _waitTicks = Prm.ResetTimeHitting; return true; }
                _vel = Vector3.Zero;
                return false;

            case NerveDie:                                       // TNerveSmallEnemyDie with setDeadAnm's one-frame clip: dead after 60 ticks
                if (_time == 0) { _hp = 0; SetCollisionLayerValue(1, false); SetDeadAnm(); SetDeadEffect(); _vel = Vector3.Zero; _lin = Vector3.Zero; }
                if (_time > 60) { GenRandomItem(); Dead(); return true; }
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
