using Godot;
using System;

/// <summary>
/// SMS pass 148 -- the Electrokoopa on the ground (TElecNokonoko : TWalkerEnemy, Enemy/elecNokonoko.cpp, ElecNokonoko.hpp) on the small-enemy base,
/// with the shipped /enemy/elecNokonoko.prm: Pinna Park's beach Koopa with the sparking blue shell it shoots at Mario and takes back (its shell is
/// <see cref="SmsElecCarapace"/>, a body of its own).
///
/// THE GAME. init: ACTOR_TYPE_ELEC_NOKONOKO, unk150 17, the shell (TElecCarapace, koura_model1.bmd) made and both models dressed by the archive's
/// dennoko_model1.bmt (its Q_dennoko2 texture; the bmd's own are dummies), the spine's default GRAPHWANDER, the collision on. reset: the scaled body
/// radius 140. The bck table is the archive's 18 (0 catch1 59, 1 down1 19, 2 elec_down1 110, 3 hit1 59, 4 melt1 64, 5 mogaki1_end 14, 6 mogaki1_loop
/// 20 L, 7 mogaki1_start 9, 8 nwait1 60 L, 9 run1_end 14, 10 run1_loop 40 L, 11 run1_start 9, 12 shoot1 114, 13 supply1 139, 14 turn1_end 4, 15
/// turn1_loop 20 L, 16 turn1_start 4, 17 wait1 60 L). moveObject: run1_start done -> run1_loop. The anims: wait = wait1 (and unk198 0), walk / run =
/// run1_start unless in run1_loop, dead = down1, melt = elec_down1 (LIVE_FLAG_UNK8, the shell's velocity 0, DNK_SHIBIRE_B). hasCarapace = unk1A4 == 0.
/// attackToMario: not dying: TSmallEnemy's (the message: the shell on -> ELECTRIC_SHOCK, off -> ATTACK), then, not in ATTACK / COLLECT / SHOOT and
/// with the shell, ATTACK pushed (hit1 to its end). receiveMessage: UNKD / UNKB -> dead; TAKE / PUT / THROWN branches (TSmallEnemy boilerplate: Mario's
/// checkCollision routes ACTOR_TYPE_ELEC_NOKONOKO to hitNormal, never hitPickUpEnemy, so it is never lifted); TRAMPLE: bare -> hp 1, killed; the shell on -> Mario
/// ELECTRIC_SHOCK, not valid; SPRAYED_BY_WATER -> behaveToWater (a juice change aside). isResignationAttack (the walker ATTACK's end): in view and the
/// goal within CarapaceShootRange 2000 -> SHOOT after, true. behaveToFindMario: GRAPHWANDER, ATTACK, TURN after; the goal Mario. behaveToWater: in
/// shoot1 past frame 58, dying, or in catch1 with the shell -> nothing; else unk165, the cooldown 0 and, unless frozen, FREEZE. SHOOT: tick 0 run1_end;
/// its end -> shoot1 (unk198 0); passing frame 60 the shell appears; under 62 the turn to the goal; passing 62 shootIn (the shell shot, the Koopa bare);
/// shoot1's end -> COLLECT after. COLLECT: tick 0 nwait1 (unless catch1), the goal = the shell; the clip runs only while the shell moves; in catch1
/// (catchIn, from the shell's RETURN): past frame 20 the shell's collision off, past 32 the shell back on (unk1A4 0, the shell killed = hidden), the
/// clip's end -> done; else past 800 ticks or the shell dead -> REBIRTH after; the turn to the goal. TURN: tick 0 turn1_start, the goal Mario; in
/// turn1_loop with Mario in sight (SearchLength, 60 deg) -> turn1_end; start's end -> loop, end's end -> done; the turn; past 500 -> done. FREEZE: tick 0
/// the shell on -> hit1 + DNK_SHIBIRE_B, else mogaki1_start; hit1 under frame 25 -> DNK_HIBANA at joint 8; the clips' ends: start -> loop; loop -> not
/// re-sprayed and the shell on -> mogaki1_end, else the loop again; else done; past 400 ticks the spine reset, the default next and REBIRTH after.
/// REBIRTH: tick 0 supply1, the shell back (unk1A4 0), TLS_CHANGE at the shell, the shell killed; passing 88 TLS_CHANGE at the body; the end ->
/// GRAPHWANDER after. ATTACK (the zap): hit1 to its end. calcRootMatrix: the shell on and not FREEZE / DIE / COLLECT -> DENNOKO_SPARK1 and DNK_BIRI /
/// SPARK_L / SPARK_R at joint 7 (effnul1, the shell's effect null) every frame; in elec_down1 DNK_SHIBIRE_A at the root, DNK_HIBANA at joint 8 (the head), passing 72
/// BOMB_LIMIT. genRandomItem: the shell killed, TLS_CHANGE, TSmallEnemy's, and, dead by thunder (MELT_ON_DEATH), a coin popping at the shell.
/// isDeadByThunder: in FREEZE with the shell within 200 u. forceCatchReady (the shell near home): not DIE / FREEZE / COLLECT -> COLLECT next.
///
/// THE PORT. The base's walker (GraphWander / Attack / Die) with the species' Shoot / Collect / Turn / Freeze / Rebirth / Zap nerves;
/// the shell is an <see cref="SmsElecCarapace"/> instanced beside the Koopa at Init (ElecNoko/ElecCarapace.tscn) and driven through Appear / Shoot /
/// KillAttach; the shell on the back is the body model's own koura1 part (the clips hide it when bare: nwait1, catch1, supply1). Mario's shock is
/// his TakeShock; the bare Koopa's touch his common TakeDamage; no pick-up (the base's default, as the game's collision table). The sparks are
/// Mario's ShockSparks on the effnul1 joint (the game's joint 7, animated with the shell) while the shell is on, sized to the body. Knobs (grouped, plain English): Deviations
/// (SpeedScale, ReflectSw = mReflectSw: the shell bounces off walls and other enemies), Effects (SparkFxScene = Fx/ShockSparks, StunFxScene (DNK_SHIBIRE)
/// = Fx/ShockSparks, HeadSparkFxScene / ChangeFxScene (TLS_CHANGE) slots, CoinScene = yellow_coin), Sounds (slots). DEVIATIONS (logged): the sparks and
/// the stun are Mario's shock sparks; the head sparks, the change puff and BOMB_LIMIT are slots; a ground pound on it does nothing (the game handles
/// TRAMPLE alone); the sounds are slots; the two bodies ignore each other's colliders (the game has none).
/// </summary>
public partial class SmsElecNokonoko : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ elecNokonoko.prm
    public class ElecParams : Params
    {
        public int ReadyTime = 300;
        public float CarapaceGravity = 0.01f, CarapaceSpeed = 5f, CarapaceTurnSpeed = 2f, CarapaceSpinSpeed = 2f, CarapaceShootRange = 500f, CarapaceFlyDist = 100f;
    }
    /// <summary>The shipped /enemy/elecNokonoko.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly ElecParams Prm = new ElecParams
    {
        HeadHeight = 20f, BodyRadius = 5f, WallRadius = 100f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 10, ActiveEnemyNum = 10, JumpForce = 10f,
        SearchLength = 2000f, SearchHeight = 500f, SearchAware = 600f, SearchAngle = 180f, GiveUpLength = 2500f, GiveUpHeight = 600f, AttackWait = 100, FreezeWait = 300,
        DamageRadius = 80f, DamageHeight = 80f, AttackRadius = 60f, AttackHeight = 60f, TurnSpeedLow = 2f, TurnSpeedHigh = 2f, BodyScaleLow = 1.5f, BodyScaleHigh = 1.5f,
        GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100, StampRange = 5f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 0.8f, MarchSpeedHigh = 0.8f,
        ReadyTime = 200, CarapaceGravity = 0.5f, CarapaceSpeed = 15f, CarapaceTurnSpeed = 100f, CarapaceSpinSpeed = 50f, CarapaceShootRange = 2000f, CarapaceFlyDist = 1500f,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the Koopa's turn and march and its shell's speed, turn and spin.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    /// <summary>mReflectSw: the shot shell bounces off walls and other enemies and heads home. On in the game.</summary>
    [Export] public bool ReflectSw = true;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_dnk_biri + ms_dnk_spark_l / _r: the sparks crackling on the shell (on the back and shot). Empty = assets/Sms/Fx/ShockSparks.tscn (Mario's).</summary>
    [Export] public PackedScene SparkFxScene;
    /// <summary>ms_dnk_shibire_a / _b: the shock running over the Koopa (hosed with the shell on; electrocuted by its own shell). Empty = assets/Sms/Fx/ShockSparks.tscn.</summary>
    [Export] public PackedScene StunFxScene;
    /// <summary>ms_dnk_hibana: the sparks off its head; ms_tls_change: the puff where the shell vanishes and returns. Slots.</summary>
    [Export] public PackedScene HeadSparkFxScene, ChangeFxScene;
    /// <summary>The coin a Koopa electrocuted by its own shell leaves (popping up 20 u a tick). Empty = res://yellow_coin.tscn.</summary>
    [Export] public PackedScene CoinScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_DENNOKO_SPARK1 (the shell's crackle, looped while it is on), the bas tables (catch / down / hit / shoot / supply).</summary>
    [Export] public AudioStreamPlayer3D SparkSound, ShootSound, CatchSound, HitSound, DownSound, SupplySound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's order)
    // the glb's clip names (the ripper drops the "_loop" of the three looping bcks: mogaki1_loop / run1_loop / turn1_loop import as dennoko_mogaki1 / _run1 / _turn1)
    private static readonly string[] Names = { "dennoko_catch1", "dennoko_down1", "dennoko_elec_down1", "dennoko_hit1", "dennoko_melt1", "dennoko_mogaki1_end", "dennoko_mogaki1",
        "dennoko_mogaki1_start", "dennoko_nwait1", "dennoko_run1_end", "dennoko_run1", "dennoko_run1_start", "dennoko_shoot1", "dennoko_supply1", "dennoko_turn1_end",
        "dennoko_turn1", "dennoko_turn1_start", "dennoko_wait1" };
    private static readonly int[] Frames = { 59, 19, 110, 59, 64, 14, 20, 9, 60, 14, 40, 9, 114, 139, 4, 20, 4, 60 };
    private static readonly bool[] Loops = { false, false, false, false, false, false, true, false, true, false, true, false, false, false, false, true, false, true };
    protected const int BckCatch = 0, BckDown = 1, BckElecDown = 2, BckHit = 3, BckMelt = 4, BckMogakiEnd = 5, BckMogakiLoop = 6, BckMogakiStart = 7, BckNWait = 8, BckRunEnd = 9,
        BckRunLoop = 10, BckRunStart = 11, BckShoot = 12, BckSupply = 13, BckTurnEnd = 14, BckTurnLoop = 15, BckTurnStart = 16, BckWait = 17;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves (TNerveElecNokonoko*)
    protected const int NerveFreezeE = NerveSpecies, NerveCollect = NerveSpecies + 1, NerveZap = NerveSpecies + 2, NerveRebirth = NerveSpecies + 3, NerveTurn = NerveSpecies + 4, NerveShoot = NerveSpecies + 5;
    private static readonly string[] ElecNerveNames = { "Freeze", "Collect", "Zap", "Rebirth", "Turn", "Shoot" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + ElecNerveNames.Length ? ElecNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveGraphWander;

    // ------------------------------------------------------------------------------------------------------------------ state
    private SmsElecCarapace _shell;                                                           // unk194
    private bool _bare;                                                                      // unk1A4 == 1: the shell shot
    private bool _sprayed;                                                                   // unk165
    private Node3D _spark, _stunFx;
    private float _passPrev = -1f, _passLast = -1f;                                          // the clip-frame pair for Passed()
    /// <summary>For the drives.</summary>
    public string CurrentNerveName => NerveName(_cur);
    public bool HasShell => !_bare;
    public SmsElecCarapace Shell => _shell;
    public float ScaleNow => _scale;
    public bool IsCatchReady => IsNerve(NerveCollect);
    public int Shots { get; private set; }
    public int Catches { get; private set; }
    public int Rebirths { get; private set; }
    public bool Sparking => _spark is SmsShockSparks s && s.Active;

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _flags150 = 17;
        if (SparkFxScene == null && ResourceLoader.Exists("res://assets/Sms/Fx/ShockSparks.tscn")) SparkFxScene = GD.Load<PackedScene>("res://assets/Sms/Fx/ShockSparks.tscn");
        if (StunFxScene == null && ResourceLoader.Exists("res://assets/Sms/Fx/ShockSparks.tscn")) StunFxScene = GD.Load<PackedScene>("res://assets/Sms/Fx/ShockSparks.tscn");
        if (CoinScene == null && ResourceLoader.Exists("res://yellow_coin.tscn")) CoinScene = GD.Load<PackedScene>("res://yellow_coin.tscn");
        if (ResourceLoader.Exists("res://assets/Sms/Enemy/ElecNoko/ElecCarapace.tscn"))
        {
            _shell = GD.Load<PackedScene>("res://assets/Sms/Enemy/ElecNoko/ElecCarapace.tscn").Instantiate<SmsElecCarapace>();
            _shell.Name = Name + "Shell"; _shell.RelaunchTicks = -1; _shell.DebugPrint = DebugPrint;
            Node parent = GetParent() ?? this;
            parent.CallDeferred(Node.MethodName.AddChild, _shell);
            _shell.CallDeferred(nameof(SmsElecCarapace.SetHost), this);
        }
        if (SparkFxScene != null)
        {
            _spark = SparkFxScene.Instantiate<Node3D>();
            if (_spark is SmsFollowEffect fe) { fe.BoneName = "effnul1"; AddChild(_spark); fe.Bind(this); } else AddChild(_spark);   // the bmd's joint 7
            SetSpark(_spark, false);                                                           // its size follows the body (Reset)
        }
        if (DebugPrint) GD.Print($"[SMS elecnoko] {Name}: shell {(_shell != null ? "made" : "none")}; sparks {(_spark != null ? "on the effnul1 joint" : "none")}");
    }
    protected override void Reset()
    {
        base.Reset();
        _bare = false; _sprayed = false;
        _turnSpeed = Prm.TurnSpeedLow * SpeedScale; _marchSpeed = Prm.MarchSpeedLow * SpeedScale;
        _passPrev = -1f; _passLast = -1f;
        if (_shell != null && IsInstanceValid(_shell)) _shell.KillAttach();
        if (_spark != null) _spark.Scale = Vector3.One * 0.8f * _scale;                      // setGlobalScale(mScaling): the sparks grow with the 1.5 body (0.6 sat inside the shell)
    }
    private static void SetSpark(Node3D n, bool on)
    {
        if (n is not SmsShockSparks s || s.Active == on) return;
        s.Active = on;
        if (on) { if (s.Streaks != null) s.Streaks.Emitting = false; if (s.Arcs != null) s.Arcs.Emitting = false; }
    }
    private void Stun(bool on)
    {
        if (on && _stunFx == null && StunFxScene != null)
        {
            _stunFx = StunFxScene.Instantiate<Node3D>();
            if (_stunFx is SmsFollowEffect fe) { fe.BoneName = "bodyjnt1"; AddChild(_stunFx); fe.Bind(this); } else AddChild(_stunFx);
            _stunFx.Scale = Vector3.One * _scale;
        }
        if (_stunFx is SmsShockSparks s && s.Active != on) s.Active = on;
    }

    // ------------------------------------------------------------------------------------------------------------------ the anims
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetWalkAnm() { if (!IsBck(BckRunLoop)) SetBck(BckRunStart); }
    protected override void SetRunAnm() { if (!IsBck(BckRunLoop)) SetBck(BckRunStart); }
    protected override void SetDeadAnm() { SetBck(BckDown); DownSound?.Play(); }
    /// <summary>setMeltAnm (dead by thunder): elec_down1, the shell's velocity 0, DNK_SHIBIRE_B.</summary>
    protected override void SetMeltAnm() { SetBck(BckElecDown); Stun(true); }
    private bool Passed(float frame) => _passLast >= 0f && _passLast < frame && _passPrev >= frame;   // checkPass on the current clip
    protected override void MoveObjectPre()
    {
        _passLast = _passPrev; _passPrev = BckFrame();
        if (IsBck(BckRunStart) && BckEnded()) SetBck(BckRunLoop);
    }
    /// <summary>calcRootMatrix's effects: the shell's sparks while it is on (not frozen / dying / collecting); in elec_down1 the stun and the head sparks.</summary>
    protected override void SetBehavior()
    {
        bool sparks = !_bare && !_dead && !IsNerve(NerveFreezeE) && !IsNerve(NerveDie) && !IsNerve(NerveCollect) && (_model == null || _model.Visible);
        if (_spark != null) SetSpark(_spark, sparks);
        if (sparks) { if (SparkSound != null && SparkSound.Stream != null && !SparkSound.Playing) SparkSound.Play(); }
        else if (SparkSound != null && SparkSound.Playing) SparkSound.Stop();
        if (IsBck(BckElecDown))
        {
            Stun(true);
            if (HeadSparkFxScene != null && (_tickCount & 7) == 0) SpawnBurst(HeadSparkFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), HeadPointM()));
        }
        else if (!IsNerve(NerveFreezeE) && !IsNerve(NerveDie)) Stun(false);
    }
    private Vector3 HeadPointM() => GlobalPosition + Vector3.Up * (100f * _scale / U) + PolarXZ(_yaw, 40f * _scale) / U;

    // ------------------------------------------------------------------------------------------------------------------ TElecNokonoko
    /// <summary>attackToMario: not dying: the message (the shell on -> ELECTRIC_SHOCK, bare -> ATTACK); then, with the shell and not in ZAP / COLLECT / SHOOT, ZAP.</summary>
    protected override void AttackToMario()
    {
        if (IsNerve(NerveDie)) return;
        SendAttackMsgToMario();
        if (IsNerve(NerveZap) || IsNerve(NerveCollect) || IsNerve(NerveShoot)) return;
        if (_bare) return;
        PushNerve(NerveZap);
    }
    protected override void SendAttackMsgToMario()
    {
        if (_mario == null) return;
        if (!_bare) _mario.TakeShock(); else _mario.TakeDamage(GlobalPosition, Mario.SmsDamageProfile.EnemyCommon);
    }
    /// <summary>receiveMessage(TRAMPLE): bare -> killed (hp 1); the shell on -> Mario shocked, not valid. A ground pound: nothing (the game handles TRAMPLE alone).</summary>
    protected override bool IsHitValid(HitMessage msg)
    {
        if (msg != HitMessage.Trample) return false;
        if (_bare) { _hp = 1; return true; }
        _mario?.TakeShock();
        return false;
    }
    /// <summary>isResignationAttack: the goal within CarapaceShootRange and in view -> SHOOT after.</summary>
    protected override bool IsResignationAttack()
    {
        if ((_model == null || _model.Visible) && (Goal() - Pos()).Length() < Prm.CarapaceShootRange) { PushAfterCurrent(NerveShoot); return true; }
        return false;
    }
    /// <summary>behaveToFindMario: GRAPHWANDER, ATTACK, TURN after (TURN first); the goal Mario.</summary>
    protected override void BehaveToFindMario()
    {
        PushAfterCurrent(NerveGraphWander); PushAfterCurrent(NerveAttack); PushAfterCurrent(NerveTurn);
        SetGoalMario();
    }
    /// <summary>behaveToWater: not past shoot1's frame 58, not dying, not catching with the shell; the sprayed mark, the cooldown cleared; FREEZE unless in it.</summary>
    protected override void BehaveToWater()
    {
        if (IsBck(BckShoot) && BckFrame() > 58f) return;
        if (IsNerve(NerveDie) || (IsBck(BckCatch) && !_bare)) return;
        _sprayed = true; _sprayCooldown = 0;
        if (IsNerve(NerveFreezeE)) return;
        PushNerve(NerveFreezeE);
    }
    /// <summary>genRandomItem: the shell killed, TLS_CHANGE, TSmallEnemy's drops; dead by thunder -> a coin popping up at the shell's point.</summary>
    protected override void GenRandomItem()
    {
        Vector3 shellAt = _shell != null && IsInstanceValid(_shell) && _shell.State != 0 ? _shell.GlobalPosition : GlobalPosition;
        if (_shell != null && IsInstanceValid(_shell)) _shell.KillAttach();
        if (ChangeFxScene != null) SpawnBurst(ChangeFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
        bool melt = _melt;
        base.GenRandomItem();
        if (melt && CoinScene != null && IsInsideTree() && new SmsDropTable().SpawnItem(CoinScene, GetParent(), shellAt, new Vector3(0f, 20f, 0f), 0, this) != null)
        {
            ThunderCoins++; ThunderCoinsSpawned++;
            if (DebugPrint) GD.Print($"[SMS elecnoko] {Name}: dead by its own shell: the coin popping at {shellAt}");
        }
    }
    public int ThunderCoins { get; private set; }
    /// <summary>For the drives: the thunder coins every Koopa has popped (a dying body is freed in the tick it pops its coin).</summary>
    public static int ThunderCoinsSpawned { get; private set; }
    /// <summary>catchIn (from the shell's RETURN): nwait1 -> catch1.</summary>
    public void CatchIn() { if (IsBck(BckNWait)) { SetBck(BckCatch); CatchSound?.Play(); } }
    /// <summary>shootIn: the shell shot, the Koopa bare.</summary>
    private void ShootIn() { _shell?.Shoot(); _bare = true; Shots++; ShootSound?.Play(); }
    /// <summary>forceCatchReady: not dying / frozen / collecting -> COLLECT next.</summary>
    public void ForceCatchReady() { if (!IsNerve(NerveDie) && !IsNerve(NerveFreezeE) && !IsNerve(NerveCollect)) { _stack.Clear(); SetNext(NerveCollect); } }
    /// <summary>isDeadByThunder: frozen with the shell within 200 u.</summary>
    public bool IsDeadByThunder() => IsNerve(NerveFreezeE) && _shell != null && IsInstanceValid(_shell) && (Pos() - _shell.GlobalPosition * U).Length() < 200f;
    /// <summary>LIVE_FLAG_MELT_ON_DEATH + kill: the Die nerve takes the melt anim (elec_down1) and leaves the thunder coin.</summary>
    public void MeltKill() { if (_dead || IsNerve(NerveDie)) return; _melt = true; Kill(); }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveShoot:                                     // TNerveElecNokonokoShoot
                if (_time == 0) SetBck(BckRunEnd);
                if (IsBck(BckRunEnd)) { if (BckEnded()) SetBck(BckShoot); }
                else if (IsBck(BckShoot))
                {
                    if (Passed(60f)) _shell?.Appear();
                    if (BckFrame() < 62f) WalkToCurPathNode(0f, _turnSpeed, 0f);
                    if (Passed(62f)) ShootIn();
                    if (BckEnded()) { PushAfterCurrent(NerveCollect); return true; }
                }
                return false;

            case NerveCollect:                                   // TNerveElecNokonokoCollect
                if (_time == 0) { if (!IsBck(BckCatch)) SetBck(BckNWait); }
                if (_shell != null && IsInstanceValid(_shell)) { SetGoalPoint(_shell.GlobalPosition * U); SetBckPaused(!_shell.IsMove); }
                if (IsBck(BckCatch))
                {
                    float f = BckFrame();
                    if (f > 20f && _shell != null) _shell.SetCollisionLayerValue(1, false);
                    if (f > 32f && _bare) { _bare = false; _shell?.KillAttach(); Catches++; if (DebugPrint) GD.Print($"[SMS elecnoko] {Name}: the shell back on"); }
                    if (BckEnded()) { SetBckPaused(false); return true; }
                }
                else if (_time > 800 || _shell == null || !IsInstanceValid(_shell) || (_shell.State == 0 && _bare))
                {
                    SetBckPaused(false);
                    PushAfterCurrent(NerveRebirth); return true;
                }
                WalkToCurPathNode(0f, _turnSpeed, 0f);
                return false;

            case NerveTurn:                                      // TNerveElecNokonokoTurn
                if (_time == 0) { SetBck(BckTurnStart); SetGoalMario(); }
                if (IsBck(BckTurnLoop) && _mario != null && IsInSight(MarioPos(), P.SearchLength, 60f, 0f)) SetBck(BckTurnEnd);
                if (BckEnded())
                {
                    if (IsBck(BckTurnStart)) SetBck(BckTurnLoop);
                    else if (IsBck(BckTurnEnd)) return true;
                }
                WalkToCurPathNode(0f, _turnSpeed, 0f);
                return _time > 500;

            case NerveFreezeE:                                   // TNerveElecNokonokoFreeze
                if (_time == 0)
                {
                    if (!_bare) { SetBck(BckHit); Stun(true); HitSound?.Play(); } else SetBck(BckMogakiStart);
                }
                if (IsBck(BckHit) && BckFrame() < 25f && HeadSparkFxScene != null && (_tickCount & 7) == 0) SpawnBurst(HeadSparkFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), HeadPointM()));
                if (BckEnded())
                {
                    if (IsBck(BckMogakiStart)) SetBck(BckMogakiLoop);
                    else if (IsBck(BckMogakiLoop))
                    {
                        bool sprayedSince = _sprayed; _sprayed = false;                        // unsetUnk165
                        if (!sprayedSince && !_bare) SetBck(BckMogakiEnd); else SetBck(BckMogakiLoop);
                    }
                    else { Stun(false); return true; }
                }
                if (_time > 400)
                {
                    Stun(false);
                    _stack.Clear(); PushAfterCurrent(NerveRebirth); PushAfterCurrent(DefaultNerve);   // reset, the default next, REBIRTH after it
                    return true;
                }
                return false;

            case NerveRebirth:                                   // TNerveElecNokonokoRebirth
                if (_time == 0)
                {
                    SetBck(BckSupply); SupplySound?.Play();
                    _bare = false; Rebirths++;
                    if (_shell != null && IsInstanceValid(_shell))
                    {
                        if (ChangeFxScene != null && _shell.State != 0) SpawnBurst(ChangeFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), _shell.GlobalPosition));
                        _shell.KillAttach();
                    }
                }
                if (Passed(88f) && ChangeFxScene != null) SpawnBurst(ChangeFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
                if (BckEnded()) { PushAfterCurrent(NerveGraphWander); return true; }
                return false;

            case NerveZap:                                       // TNerveElecNokonokoAttack
                if (_time == 0 || !IsBck(BckHit)) SetBck(BckHit);
                return BckEnded();
        }
        return base.ExecuteNerve(n);
    }
}
