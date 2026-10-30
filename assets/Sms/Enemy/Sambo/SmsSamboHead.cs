using Godot;
using System;

/// <summary>
/// SMS pass 129 -- the Pokey Head, TSamboHead (doldecomp/sms src/Enemy/hanasambo.cpp 1044-1544, include/Enemy/HanaSambo.hpp): a
/// TWalkerEnemy that lives buried under a sunflower bud, pops out when Mario comes into sight and hops at him; water knocks it back, a
/// wall ends it in a crash that pays three coins, a stomp squashes it.
///
/// THE GAME (per tick, 120 Hz; sambohead.prm: body scale 1.5 -- 120 u = 2.4 m tall; turn 0.3..0.5; damage 60 x 100; attack 40 x 80):
/// - Hide (the default nerve): samboHead_Fhide (44), no collision, MS_SMB_HD_ROCK + HD_SMOKE; at the clip's end LIVE_FLAG_HIDDEN,
///   samboHead_wait, initFlower (the bud at the ground); turning toward Mario. isFindMario(1.0) (in sight: SearchLength 600 / 90 deg /
///   aware 300, within SearchHeight) AND within AppearDist (800 u) -> Appear.
/// - Appear: shown, collision on, samboHead_set (44: the rise) with MS_SMB_AP_ROCK + AP_SMOKE, the bud hidden (a bud the hose had
///   opened pops its pollen once more); at tick 20 the flower manager's dropLeaf: three leaves fly off (<see cref="SmsSamboLeaf"/>); at the
///   end samboHead_wait -> Attack.
/// - Attack: on the ground, once JumpPrepareTime (300 ticks) has passed and the clip is at its end: a hop MoveDist (300 u) toward Mario
///   at JumpSp (8 u a tick up) under MoveGravity (0.3): samboHead_jump_start; falling -> samboHead_jump_end held at its first frame;
///   landed -> the clip runs; a finished jump_end -> samboHead_wait. In the air the roll (unk1AC) = the velocity's pitch, clamped to
///   JumpAngY (30), decaying x 0.8 a tick near the ground: the body joint leans into the hop (SamboHeadRollCallback, <see cref="SmsBoneRoll"/>).
///   Turning toward Mario at the turn speed (5 a tick in the air).
/// - Contact (attackToMario): ATTACK (dmgenemycommon); in the air a bounce away ((pos - Mario, +10) normalised x 8); on the ground in
///   Attack -> the base's Freeze (FreezeWait 120 ticks, the clip kept: TSmallEnemy::setFreezeAnm is empty) -- the window for a stomp.
/// - Water (behaveToWater, not while hidden / rising / crashing / dying): a hop away from Mario (HitJumpSpXZ 12, HitJumpSpY 10, lifted 2 u,
///   airborne) -> HitWater (a second drop adds to the flight): samboHead_hit (30, loop) in the air; landed -> samboHead_jump_end for a tick,
///   then samboHead_hit_end (36) while it SKIDS on: each tick back at ground + 1 with the saved velocity; at hit_end's end the velocity x
///   HitJumpSpRateXZ (0.6), grounded, samboHead_wait -> RecoverWater (the roll decays x 0.99 to under 1) -> back to Attack. The roll
///   while hit: -3 a tick for the first 100 ticks of hit_end, +3 after (a somersault), clamped to JumpAngY. Gravity HitJumpGravity (0.4).
///   A wall met while hit (isHitWallInBound) -> HitWall: samboHead_crash (5) with MS_ENM_WALLHIT (+ _O in the goop colour); 60 ticks past
///   its end: dead with UNK20000 (no drop table) and genRandomItem -> genEventCoin in the crash pose = THREE coins fanned at -60 / 0 / +60
///   degrees 100 u out, velocity (dir x 4, rand(8, 16), dir x 4) (the middle one is the stage's attached coin when there is one).
/// - A stomp (TRAMPLE / HIP_DROP: TSmallEnemy::isHitValid) -> kill -> the base's Die: samboHead_down (14), the puffs (DISAP_A_W after a
///   crash, else DISAP_A + B, scale 1.5), the drop table.
/// DEVIATIONS (logged): <see cref="StartBuried"/>; the particles and sounds are slots (empty = none); the manager's crash wait is 60 ticks (the
/// Stu's); the stage's attached coin is <see cref="EventCoinScene"/> for all three; Yoshi's eat.
/// </summary>
[GlobalClass]
public partial class SmsSamboHead : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ sambohead.prm
    public class HeadParams : Params
    {
        public float AppearDist = 800f, HideDist = 1000f, MoveDist = 100f, MoveGravity = 0.1f, JumpSp = 10f, HitJumpSpXZ = 12f, HitJumpSpY = 10f, HitJumpGravity = 1f, HitJumpSpRateXZ = 0.3f, HitJumpSpRateY = 0.3f, JumpAngY = 30f;
        public int JumpPrepareTime = 20;
    }
    /// <summary>The shipped /enemy/sambohead.prm (params.szs on his disc, read with prm_file.py).</summary>
    public static readonly HeadParams Prm = new HeadParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 20, ActiveEnemyNum = 20,
        JumpForce = 10f, SearchLength = 600f, SearchHeight = 1000f, SearchAware = 300f, SearchAngle = 90f, GiveUpLength = 4000f, GiveUpHeight = 1000f,
        AttackWait = 60, FreezeWait = 120, DamageRadius = 60f, DamageHeight = 100f, AttackRadius = 40f, AttackHeight = 80f,
        TurnSpeedLow = 0.3f, TurnSpeedHigh = 0.5f, BodyScaleLow = 1.5f, BodyScaleHigh = 1.5f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 5f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 0.8f, MarchSpeedHigh = 1.5f,
        AppearDist = 800f, HideDist = 1000f, MoveDist = 300f, MoveGravity = 0.3f, JumpSp = 8f, JumpPrepareTime = 300,
        HitJumpSpXZ = 12f, HitJumpSpY = 10f, HitJumpGravity = 0.4f, HitJumpSpRateXZ = 0.6f, HitJumpSpRateY = 0.5f, JumpAngY = 30f,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (off = the game)")]
    /// <summary>On: a placed head starts already buried (the bud on the spot) instead of standing and sinking for 0.7 s when the level loads.
    /// A re-launch after a death plays the game's sink.</summary>
    [Export] public bool StartBuried = true;

    [ExportGroup("The flower, the leaves and the coins")]
    /// <summary>The sunflower bud that marks a buried head (TSamboFlower; the hose opens it). Default: assets/Sms/Enemy/Sambo/SamboFlower.tscn.</summary>
    [Export] public PackedScene FlowerScene;
    public const string FlowerScenePath = "res://assets/Sms/Enemy/Sambo/SamboFlower.tscn";
    /// <summary>The leaf that flies off as it rises (three per rise). Default: assets/Sms/Enemy/Sambo/SamboLeaf.tscn; empty = none.</summary>
    [Export] public PackedScene LeafScene;
    public const string LeafScenePath = "res://assets/Sms/Enemy/Sambo/SamboLeaf.tscn";
    /// <summary>genEventCoin's coin: the three the wall crash fans out -- his yellow_coin.tscn by default. Empty = no coins.</summary>
    [Export] public PackedScene EventCoinScene;
    public const string EventCoinScenePath = "res://yellow_coin.tscn";

    [ExportGroup("Effects and sounds (empty = none)")]
    /// <summary>PARTICLE_MS_SMB_AP_ROCK + AP_SMOKE at the base when it rises.</summary>
    [Export] public PackedScene AppearFxScene;
    /// <summary>PARTICLE_MS_SMB_HD_ROCK + HD_SMOKE at the base when it sinks.</summary>
    [Export] public PackedScene HideFxScene;
    /// <summary>PARTICLE_MS_ENM_WALLHIT (+ _O, the goop colour) at the crash, scale 1.5, facing the heading -- EnemyWallHit.tscn-like; empty = none.</summary>
    [Export] public PackedScene WallHitFxScene;
    /// <summary>PARTICLE_MS_ENM_DISAP_A_W: the puff of a death in the crash pose (the wall). Unset = DeathFxScene.</summary>
    [Export] public PackedScene WetDeathFxScene;

    // ------------------------------------------------------------------------------------------------------------------ the bck table
    // /scene/sambohead (bianco0.szs): the files in order = setBckAnm's indices (sambohead_bastable); frames + loops read off the disc (bck.py).
    // flower_shoot (0) is the 3-joint flower clip of another actor -- the head's glb has no such clip (an empty slot).
    private static readonly string[] Names = { "", "sambohead_crash", "sambohead_dance", "sambohead_down", "sambohead_fhide", "sambohead_hit", "sambohead_hit_end", "sambohead_jump_end", "sambohead_jump_start", "sambohead_land", "sambohead_set", "sambohead_turn", "sambohead_wait" };
    private static readonly int[] Frames = { 23, 5, 70, 14, 44, 30, 36, 29, 10, 12, 44, 150, 30 };
    private static readonly bool[] Loops = { true, false, true, false, false, true, false, false, false, false, false, true, true };
    protected const int BckCrash = 1, BckDance = 2, BckDown = 3, BckFhide = 4, BckHit = 5, BckHitEnd = 6, BckJumpEnd = 7, BckJumpStart = 8, BckLand = 9, BckSet = 10, BckTurn = 11, BckWait = 12;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    /// <summary>mBodyJntIndex: 'jnt_body' (JNT1: 0 root, 1 center, 2 jnt_body, 3 null_up, 4 null_down, 5 body) -- the roll callback's joint.</summary>
    public const string BodyJoint = "jnt_body";

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveHHide = NerveSpecies + 0, NerveHAppear = NerveSpecies + 1, NerveHAttack = NerveSpecies + 2, NerveHHitWater = NerveSpecies + 3, NerveHRecover = NerveSpecies + 4, NerveHHitWall = NerveSpecies + 5;
    protected override string NerveName(int n) => n switch { NerveHHide => "HeadHide", NerveHAppear => "HeadAppear", NerveHAttack => "HeadAttack", NerveHHitWater => "HeadHitWater", NerveHRecover => "HeadRecoverWater", NerveHHitWall => "HeadHitWall", _ => base.NerveName(n) };
    protected override int DefaultNerve => NerveHHide;

    // ------------------------------------------------------------------------------------------------------------------ state
    private bool _hidden;             // LIVE_FLAG_HIDDEN
    private int _prepare;             // unk19C: ticks on the ground before the next hop
    private Vector3 _hitVel;          // unk1A0: the water knock's velocity, kept for the skid
    private float _roll;              // unk1AC: the lean, degrees
    private bool _skipSink, _crashed;
    private SmsSamboFlower _flower;
    private SmsBoneRoll _rollMod;
    private readonly SmsSamboLeaf[] _leaves = new SmsSamboLeaf[3];

    public bool Hidden => _hidden;
    public float Roll => _roll;
    public int PrepareTicks => _prepare;
    public SmsSamboFlower Flower => _flower;

    /// <summary>A pass-131 effect scene by name (assets/Sms/Enemy/Fx131/), or null when missing.</summary>
    internal static PackedScene Fx131(string name) => ResourceLoader.Exists("res://assets/Sms/Enemy/Fx131/" + name + ".tscn") ? GD.Load<PackedScene>("res://assets/Sms/Enemy/Fx131/" + name + ".tscn") : null;
    protected override void Init()
    {
        _flags150 = 17;                                   // unk150 = 0x11
        SetGoalMario();
        FlowerScene ??= ResourceLoader.Exists(FlowerScenePath) ? GD.Load<PackedScene>(FlowerScenePath) : null;
        LeafScene ??= ResourceLoader.Exists(LeafScenePath) ? GD.Load<PackedScene>(LeafScenePath) : null;
        AppearFxScene ??= Fx131("SmbAppear"); HideFxScene ??= Fx131("SmbHide");   // pass 131: the disc's jpa
        EventCoinScene ??= ResourceLoader.Exists(EventCoinScenePath) ? GD.Load<PackedScene>(EventCoinScenePath) : null;
        if (_model != null)
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false))
                if (n is Skeleton3D skel) { _rollMod = new SmsBoneRoll { Name = "Roll", BoneName = BodyJoint }; skel.AddChild(_rollMod); break; }
        _skipSink = StartBuried;
    }

    protected override void Reset()
    {
        base.Reset();
        _prepare = 0; _hitVel = Vector3.Zero; _roll = 0f; _crashed = false;
        SetHidden(false);
        _flower?.ResetBud();
    }
    protected override void Dead() { base.Dead(); _skipSink = false; }   // a re-launch after a death plays the game's sink

    // ------------------------------------------------------------------------------------------------------------------ anims and hooks
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetDeadAnm() => SetBck(BckDown);
    /// <summary>TSamboHead::getGravityY: MoveGravity while attacking, HitJumpGravity while knocked by water, else mGravity.</summary>
    protected override float GravityY() => _cur == NerveHAttack ? Prm.MoveGravity : (_cur == NerveHHitWater ? Prm.HitJumpGravity : base.GravityY());
    protected override bool WallHopAllowed() => false;
    /// <summary>The hop lands with its velocity zeroed (bind); the skid of HitWater sets its own each tick.</summary>
    protected override bool KeepVelocityOnGround() => false;

    /// <summary>TSamboHead::attackToMario: the message; in the air a bounce away; on the ground while attacking -> the base's Freeze (a stomp window).</summary>
    protected override void AttackToMario()
    {
        SendAttackMsgToMario();
        if (_airborne)
        {
            Vector3 v = Pos() - MarioPos(); v.Y = 10f;
            if (v.LengthSquared() < 1e-6f) v = Vector3.Up;
            _vel = v.Normalized() * 8f;
        }
        else if (_cur == NerveHAttack) PushNerve(NerveFreeze);
    }

    /// <summary>TSamboHead::behaveToWater: not while hidden, rising, crashing or dying -- a hop away from Mario (HitJumpSpXZ / SpY), lifted 2 u,
    /// airborne, -> HitWater pushed over the current nerve (already there: the hop adds to the flight).</summary>
    protected override void BehaveToWater()
    {
        if (_cur == NerveHHide || _cur == NerveHAppear || _cur == NerveHHitWall || _cur == NerveDie || _dead) return;
        Vector3 flat = _vel; flat.Y = 0f;
        Vector3 away = Pos() - MarioPos(); away.Y = 0f;
        if (away.LengthSquared() < 1e-6f) away = PolarXZ(_yaw, 1f);
        Vector3 jump = away.Normalized() * Prm.HitJumpSpXZ; jump.Y = Prm.HitJumpSpY;
        if (_cur != NerveHHitWater) PushNerve(NerveHHitWater);
        else jump += flat;
        _vel = jump;
        GlobalPosition += Vector3.Up * (2f / U);
        _airborne = true;
    }

    /// <summary>genRandomItem's genEventCoin in the crash pose: three coins fanned at -60 / 0 / +60 degrees of the heading, 100 u out, popped
    /// (dir x 4, rand(8, 16), dir x 4) from the body's point; else TSmallEnemy's (nothing without a placed coin). The crash death is flagged
    /// UNK20000 (no drop table) -- the coins still come, as in the game.</summary>
    protected override void GenRandomItem()
    {
        if (IsBck(BckCrash) && !_dead)
        {
            SetAfterDeadEffect();
            if (EventCoinScene != null)
                for (int i = 0; i < 3; i++)
                {
                    Vector3 off = PolarXZ(_yaw - 60f + 60f * i, 100f);
                    Vector3 at = (Pos() + off) / U; at.Y = GlobalPosition.Y;
                    Vector3 dir = off.Normalized();
                    DropSpawner.SpawnItem(EventCoinScene, GetParent(), at, new Vector3(dir.X * 4f, Lerp(8f, 16f, Rand()), dir.Z * 4f), 0, this);
                }
            return;
        }
        base.GenRandomItem();
    }
    /// <summary>setAfterDeadEffect: DISAP_A_W in the crash pose else DISAP_A, + DISAP_B, scale 1.5 (mScaling).</summary>
    protected override void SetAfterDeadEffect()
    {
        PackedScene fx = IsBck(BckCrash) && WetDeathFxScene != null ? WetDeathFxScene : DeathFxScene;
        SpawnBurst(fx, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
        if (DeathSound != null && DeathSound.Stream != null) DeathSound.Play();
    }
    /// <summary>setCrashAnm: samboHead_crash + MS_ENM_WALLHIT (+ _O) facing the heading at scale 1.5.</summary>
    private void SetCrashAnm()
    {
        SetBck(BckCrash);
        _crashed = true;
        SpawnBurst(WallHitFxScene, new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(_yaw)).Scaled(Vector3.One * 1.5f), GlobalPosition));
    }

    private void InitFlower()
    {
        if (_flower == null && FlowerScene != null)
        {
            _flower = FlowerScene.Instantiate<SmsSamboFlower>();
            _flower.StartsShown = false;
            GetParent().AddChild(_flower);
        }
        _flower?.Show(new Vector3(GlobalPosition.X, GroundYBelow() / U, GlobalPosition.Z));
    }
    private void SetHidden(bool hidden)
    {
        _hidden = hidden;
        if (_model != null) _model.Visible = !hidden;
        SetCollisionLayerValue(1, !hidden);
    }
    private void Bury()
    {
        SetHidden(true);
        SetBck(BckWait);
        InitFlower();
    }
    /// <summary>TSamboFlowerManager::dropLeaf(pos, scale): three leaves at 0 / 120 / 240 degrees, (0, rand(4, 4.8), rand(3, 3.6)) turned by the angle,
    /// facing angle - 90, gravity 0.15 (samboflower.prm).</summary>
    private void DropLeaves()
    {
        if (LeafScene == null) return;
        for (int i = 0; i < 3; i++)
        {
            if (_leaves[i] == null || !IsInstanceValid(_leaves[i]))
            {
                _leaves[i] = LeafScene.Instantiate<SmsSamboLeaf>();
                GetParent().AddChild(_leaves[i]);
            }
            float rotY = 120f * i;
            float vxz = Lerp(3f, 3.6f, Rand()), vy = Lerp(4f, 4.8f, Rand());
            Vector3 v = PolarXZ(rotY, vxz); v.Y = vy;
            _leaves[i].Fly(Pos(), v, rotY - 90f, Vector3.One * _scale, 0.15f);
        }
    }
    private float Dist2ToMario() { Vector3 d = MarioPos() - Pos(); return d.LengthSquared(); }

    /// <summary>calcVelocityToJumpToY(goal, jumpSp, g): the velocity that lands on `goal` (units) with the vertical speed jumpSp under the per-tick
    /// gravity g -- the flight's ticks t = (jumpSp + sqrt(jumpSp^2 + 2 g dy)) / g (dy = the drop to the goal), the horizontal speed the distance over t.</summary>
    private Vector3 VelocityToJumpTo(Vector3 goal, float jumpSp, float g)
    {
        Vector3 p = Pos();
        float dy = p.Y - goal.Y;
        float disc = jumpSp * jumpSp + 2f * g * dy;
        float t = g > 1e-6f ? (jumpSp + MathF.Sqrt(MathF.Max(disc, 0f))) / g : 1f;
        if (t < 1f) t = 1f;
        return new Vector3((goal.X - p.X) / t, jumpSp, (goal.Z - p.Z) / t);
    }
    private static float PitchOf(Vector3 v) => Mathf.RadToDeg(MathF.Atan2(-v.Y, new Vector2(v.X, v.Z).Length()));   // MsGetRotFromZaxis(v).x

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveHHide:                                     // TNerveSamboHeadHide
                if (_time == 0)
                {
                    if (_skipSink) { _skipSink = false; Bury(); }
                    else
                    {
                        SetBck(BckFhide);
                        SetCollisionLayerValue(1, false);
                        SpawnBurst(HideFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
                    }
                }
                else if (!_hidden && BckEnded()) Bury();
                else if (IsFindMario(1f) && Dist2ToMario() < Prm.AppearDist * Prm.AppearDist) { PushAfterCurrent(NerveHAppear); return true; }
                WalkToCurPathNode(0f, _turnSpeed, 0f);
                return false;

            case NerveHAppear:                                   // TNerveSamboHeadAppear
                if (_time == 0)
                {
                    SetHidden(false);
                    if (_flower != null && _flower.Bloomed) _flower.Bloom();
                    SetBck(BckSet);
                    SpawnBurst(AppearFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
                    _flower?.Hide();
                }
                if (_time == 20) DropLeaves();
                if (BckEnded()) { SetBck(BckWait); PushAfterCurrent(NerveHAttack); return true; }
                return false;

            case NerveHAttack:                                   // TNerveSamboHeadAttack
                if (!_airborne)
                {
                    if (_prepare > Prm.JumpPrepareTime && BckEnded())
                    {
                        _prepare = 0;
                        Vector3 to = MarioPos() - Pos(); to.Y = 0f;
                        if (to.LengthSquared() < 1e-6f) to.X += 1f;
                        Vector3 goal = Pos() + to.Normalized() * Prm.MoveDist;
                        _vel = VelocityToJumpTo(goal, Prm.JumpSp, GravityY());
                        GlobalPosition += Vector3.Up * (2f / U);
                        _airborne = true;
                        SetBck(BckJumpStart);
                    }
                    else _prepare++;
                    if (BckEnded() && IsBck(BckJumpEnd)) SetBck(BckWait);
                    SetBckPaused(false);
                }
                else if (_vel.Y < 0f && IsBck(BckJumpStart)) { SetBck(BckJumpEnd); SetBckPaused(true); }
                if (Pos().Y > 30f + _groundY) _roll = Mathf.Clamp(PitchOf(_vel), -Prm.JumpAngY, Prm.JumpAngY);
                else _roll *= 0.8f;
                WalkToCurPathNode(0f, _airborne ? 5f : _turnSpeed, 0f);
                return false;

            case NerveHHitWater:                                 // TNerveSamboHeadHitWater
                if (_time == 0) { SetBck(BckHit); _hitVel = _vel; }
                if (_onWall)
                {
                    _roll = 0f;
                    _stack.Clear(); SetNext(NerveHHitWall); PushAfterCurrent(NerveHHitWall);
                    return true;
                }
                {
                    float lim = Prm.JumpAngY;
                    if (IsBck(BckHitEnd)) _roll = _time < 100 ? Mathf.Clamp(_roll - 3f, -lim, lim) : Mathf.Clamp(_roll + 3f, -lim, 0f);
                    else _roll = Mathf.Clamp(_roll + 3f, -lim, lim);
                }
                if (IsBck(BckHit) && !_airborne) SetBck(BckJumpEnd);
                if (IsBck(BckHitEnd))
                {
                    // the skid: the game puts it back at ground + 1 with the saved velocity (y 10 included) and AIRBORNE every tick -- a slide that
                    // never leaves the floor. Here: grounded, the saved horizontal velocity re-set each tick (the landing's zeroing is per frame).
                    _airborne = false;
                    _vel = new Vector3(_hitVel.X, 0f, _hitVel.Z);
                }
                if (!_airborne && !IsBck(BckHitEnd)) SetBck(BckHitEnd);   // setBckAnm(6) each tick: MActor keeps a clip already set
                if (BckEnded() && IsBck(BckHitEnd))
                {
                    _hitVel.X *= Prm.HitJumpSpRateXZ; _hitVel.Z *= Prm.HitJumpSpRateXZ; _hitVel.Y = 0f;
                    _vel = _hitVel;
                    GlobalPosition = new Vector3(GlobalPosition.X, _groundY / U, GlobalPosition.Z);
                    _airborne = false;
                    SetBck(BckWait);
                    PushAfterCurrent(NerveHRecover);              // setNext(Attack) + pushAfterCurrent(RecoverWater) + done: the stack's Recover runs, then what was under it
                    return true;
                }
                WalkToCurPathNode(0f, _turnSpeed, 0f);
                return false;

            case NerveHRecover:                                  // TNerveSamboHeadRecoverWater
                if (_time == 0) SetBck(BckWait);
                _roll *= 0.99f;
                return BckEnded() && _roll < 1f;

            case NerveHHitWall:                                  // TNerveSamboHeadHitWall
                if (_time == 0) SetCrashAnm();
                if (BckEnded() && _time > 60 + BckEndFrame)
                {
                    _noReward = true;                            // LIVE_FLAG_UNK20000: no drop table (the three coins are genEventCoin's)
                    GenRandomItem();
                    Dead();
                    return true;
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }

    /// <summary>calcRootMatrix + the roll callback: the lean about up x velocity while attacking / hit / recovering (isUseCallBack), else none.</summary>
    protected override void ApplyLook()
    {
        base.ApplyLook();
        if (_rollMod == null) return;
        bool use = _cur == NerveHAttack || _cur == NerveHHitWater || _cur == NerveHRecover;
        _rollMod.Degrees = use ? _roll : 0f;
        Vector3 v = _vel; if (v.X == 0f && v.Z == 0f) v.X = 0.001f;
        _rollMod.WorldAxis = Vector3.Up.Cross(new Vector3(v.X, 0f, v.Z)).Normalized();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_flower != null && IsInstanceValid(_flower) && !_flower.IsQueuedForDeletion()) _flower.QueueFree();
        foreach (var l in _leaves) if (l != null && IsInstanceValid(l) && !l.IsQueuedForDeletion()) l.QueueFree();
    }
}
