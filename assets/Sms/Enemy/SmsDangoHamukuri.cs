using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The STACKED STUS -- TDangoHamuKuri + TBossDangoHamuKuri (Enemy/hamukuri.cpp 1710-2126; JP だんごハムクリ, "dumpling Stus": the Stu tower of Pianta
/// Village) on <see cref="SmsHamukuri"/>, with the shipped /enemy/bossdangohamukuri.prm for the one at the bottom (the BOSS: body scale 2.0..2.1, 1 HP,
/// turn 1, sight 10 u / aware 600 (it notices him 600 u all round), give-up 2000 / 200, AttackWait 240, damage 80 x 60, attack 50 x 50, Jitabata 500,
/// KyoroTimer 2000, StampRange 20, NumArray 10) and /enemy/dangohamukuri.prm (= the Stu's numbers) for the BODIES it carries (pass 125g).
///
/// THE TOWER: at the boss's Generate anim (setGenerateAnm) it makes NumArray bodies (makeOneEnemyAppear from the dango pool -- here <see cref="BodyScene"/>
/// instances) and TAKES them into a chain (mPrev / mNext / mHeldObject): each body rides the one below at its joint 3 'null_cap' (the top of the head;
/// +40 u on the boss), turned to the boss's yaw, swaying on its own slow circle (swingBody: 10 u at a 0.01-a-tick phase, 16 while the boss attacks),
/// in TNerveDangoHamuKuriWait (the wait anim from a random frame). The boss walks the Stu's life (Wander / Attack -- its run anim is the WALK while it
/// carries; a lone one runs) and the bodies come along. HITS: a body in the chain cannot be stomped (isHitValid: only a lone one), but its attack
/// cylinder hurts Mario like a Stu's (attackToMario: a held body or one he is not standing above -> THamuKuri's hop and hit; else only the message while
/// the boss attacks). WATER: the TOP body (no next) is knocked OFF -- released (PUT), thrown 5 u/tick away from him with vy 1, +20 u, into
/// GraphWander as a free Stu (its boss link stays: it dies with the boss); a middle body spins once around (unk230 / unk210: +40 deg a tick, settling
/// 10..20 deg askew, MSD_SE_EN_DANGOKURI_TURN); the boss hops (TNerveSmallEnemyHitWaterJump: vy 2, +10 u) with the tower on it. DEATH: the boss dead
/// (Die or WallDie) -> every body of its tower, held or freed, dies with LIVE_FLAG_UNK20000 (forceKill); a freed body stomped dies as a Stu. The bodies
/// are freed when dead (no re-launch of their own: the boss's re-launch rebuilds the tower). Drops: the boss's DropTable at its death (GenItemRate 50 + 10);
/// the game's event coin (genEventCoin) is a placed coin, none here.
/// NOT PORTED (logged): mAttackSw's attack wobble (a static the shipped game leaves off), changeByJuice (Yoshi), the pool (TDangoHamuKuriManager), the
/// sound slots (DANGOKURI_TURN).
/// </summary>
[GlobalClass]
public partial class SmsDangoHamukuri : SmsHamukuri
{
    // ------------------------------------------------------------------------------------------------------------------ the .prm
    public class BossParams : HamukuriParams { public int NumArray = 3; }
    /// <summary>The shipped /enemy/bossdangohamukuri.prm (params.szs on his disc): the Stu's numbers where the file does not differ.</summary>
    public static readonly BossParams BossPrm = new BossParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 50f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 1, ActiveEnemyNum = 1,
        JumpForce = 3f, SearchLength = 10f, SearchHeight = 200f, SearchAware = 600f, SearchAngle = 10f, GiveUpLength = 2000f, GiveUpHeight = 200f,
        AttackWait = 240, FreezeWait = 300, DamageRadius = 80f, DamageHeight = 60f, AttackRadius = 50f, AttackHeight = 50f,
        TurnSpeedLow = 1f, TurnSpeedHigh = 1f, BodyScaleLow = 2f, BodyScaleHigh = 2.1f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 20f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 1f, MarchSpeedHigh = 1f,
        WaterCoeff = 30f, WaterAttackCoeff = 30f, FirstVelocityY = 5f, VelocityRate = 1f, FirstKickVelocityY = 2f, CanSearchDist = 800f,
        BoundNum = 3, SearchActorTimer = 500, JitabataTimer = 500, FlyTimer = 500, TrampleBonusNum = 3, CrashBonusNum = 3, SerialCrashFrame = 15, KyoroTimer = 2000,
        NumArray = 10,
    };
    /// <summary>The shipped /enemy/dangohamukuri.prm: the Stu's numbers (only the pool sizes differ).</summary>
    public static readonly HamukuriParams BodyPrm = Prm;
    protected override HamukuriParams HPrm => IsBody ? BodyPrm : BossPrm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>How many Stus the boss carries (bossdangohamukuri.prm mSLNumArray 10). Set below 10 for a shorter tower.</summary>
    [Export] public int StackCount = 10;
    /// <summary>The scene of a carried body: this scene itself unless another is set (a Stu kind on SmsDangoHamukuri).</summary>
    [Export] public PackedScene BodyScene;
    public const string BodyScenePath = "res://assets/Sms/Enemy/Dango.tscn";
    /// <summary>Set before the body enters the tree: this instance is a carried body, not the boss (the spawner sets it; a placed one is a boss).</summary>
    public bool IsBody;
    public const string RideBone = "null_cap";                  // unk1AC = 3
    /// <summary>MSD_SE_EN_DANGOKURI_TURN (a middle body's spin). Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D TurnSound;

    // ------------------------------------------------------------------------------------------------------------------ the chain
    private SmsDangoHamukuri _prev, _next, _boss;             // mPrev (the holder), mNext (the held), mBoss
    private readonly List<SmsDangoHamukuri> _bodies = new();  // the boss's: every body it made this life (held or freed)
    private int _generated;                                   // unk238
    private float _swingPhase;                                // unk20C
    private float _spin, _pitch, _spinPhase;                  // unk210 (yaw), unk214 (pitch), unk218
    private bool _turning;                                    // unk230
    private Vector3 _swing;                                   // (unk21C, unk224)
    private Skeleton3D _skel; private int _rideBone = -1;
    private const int NerveDangoWait = NerveSpecies + 30;
    protected override string NerveName(int n) => n == NerveDangoWait ? "DangoWait" : base.NerveName(n);

    public bool Held => _prev != null;
    public bool Carrying => _next != null;
    public bool IsBoss => _boss == this;
    public int TowerSize { get { int n = 0; for (var b = _next; b != null; b = b._next) n++; return n; } }
    public SmsDangoHamukuri Top { get { var b = this; while (b._next != null) b = b._next; return b; } }
    public SmsDangoHamukuri Below => _prev;
    public IReadOnlyList<SmsDangoHamukuri> Bodies => _bodies;
    public new string DebugState => $"{base.DebugState} boss {IsBoss} held {Held} tower {TowerSize} spin {_spin:F0} pitch {_pitch:F0}";

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override int DefaultNerve => IsBody ? NerveDangoWait : NerveGenerate;   // TDangoHamuKuri::init initWith(DangoWait); the boss: Generate
    protected override void Init()
    {
        base.Init();
        if (!IsBody) _boss = this;
        if (_model != null)
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        if (_skel != null) _rideBone = _skel.FindBone(RideBone);
    }
    protected override void Reset()
    {
        base.Reset();
        if (!IsBody) { _boss = this; _generated = 0; _bodies.Clear(); }
        _swingPhase = Rand();
        _spin = 0f; _pitch = 0f; _spinPhase = 0f; _turning = false;
    }

    // the tower: the boss makes its bodies at the set anim
    protected override void SetGenerateAnm()
    {
        base.SetGenerateAnm();
        if (!IsBody) for (int i = 0; i < StackCount; i++) GenerateBody();
    }
    /// <summary>TBossDangoHamuKuri::generateBody: one more body on top of the chain, taken.</summary>
    private void GenerateBody()
    {
        if (_generated >= StackCount) return;
        PackedScene scene = BodyScene ?? GD.Load<PackedScene>(BodyScenePath);
        if (scene == null) return;
        var body = scene.Instantiate<SmsDangoHamukuri>();
        body.IsBody = true; body.RelaunchTicks = -1; body.DebugPrint = DebugPrint;
        GetParent().AddChild(body);
        body.SmsPlace(GlobalPosition, _yaw);
        body._boss = this;
        body.Graph = Graph;
        var cur = this; while (cur._next != null) cur = cur._next;
        cur._next = body; body._prev = cur;
        // a tower never shoves itself: the new body ignores the boss and every body, and they ignore it (the first frame it sits inside the boss's capsule)
        body.AddCollisionExceptionWith(this); AddCollisionExceptionWith(body);
        foreach (var b in _bodies) if (IsInstanceValid(b)) { body.AddCollisionExceptionWith(b); b.AddCollisionExceptionWith(body); }
        body.GlobalPosition = cur.HeadPoint();
        _bodies.Add(body);
        _generated++;
    }
    public SmsDangoHamukuri NextInChain => _next;

    // ------------------------------------------------------------------------------------------------------------------ riding
    protected override bool FlyBind => _prev != null;           // a held body: the step neither drops nor snaps it; its place is the holder's head
    /// <summary>TDangoHamuKuri::calcRootMatrix for a held body: the holder's null_cap joint (+40 u on the boss) turned to the boss's yaw, the sway added,
    /// then its own spin and pitch; the body's position is that point. The sway and the spin (swingBody, the water turn) advance here each tick.</summary>
    protected override void MoveObjectPre()
    {
        base.MoveObjectPre();
        if (_prev != null) { _noBind = true; _airborne = false; _vel = Vector3.Zero; }
        // forceKill: the boss dead -> the body dies with it, no reward
        if (_boss != null && _boss != this && IsInstanceValid(_boss) && (_boss.IsNerve(NerveDie) || _boss.IsNerve(NerveWallDie) || _boss.IsDead) && !IsNerve(NerveDie) && !_dead)
        {
            _noReward = true; ResetSpine(); SetNext(NerveDie);
        }
        // swingBody
        if (_swingPhase > 1f) _swingPhase -= 1f;
        _swingPhase += 0.01f;
        float r = _boss != null && IsInstanceValid(_boss) && _boss.IsNerve(NerveAttack) ? 16f : 10f;
        _swing = new Vector3(r * Mathf.Cos(_swingPhase * Mathf.Tau), 0f, r * Mathf.Sin(_swingPhase * Mathf.Tau));
        if (_prev != null)
        {
            if (_turning)
            {
                _spin += 40f;
                if (_spin > 360f) { _spin = -Lerp(10f, 20f, Rand()); _turning = false; }
            }
            if (_prev._turning) _spin = -_prev._spin;
            Vector3 head = _prev.HeadPoint() + _swing / U;
            GlobalPosition = head;
            _yaw = _boss != null && IsInstanceValid(_boss) ? _boss._yaw + _spin : _prev._yaw + _spin;
        }
    }
    /// <summary>getTakingMtx's point: the null_cap joint in the world (+40 u for the boss).</summary>
    private Vector3 HeadPoint()
    {
        Vector3 p = _skel != null && _rideBone >= 0 ? (_skel.GlobalTransform * _skel.GetBoneGlobalPose(_rideBone)).Origin : GlobalPosition + Vector3.Up * (117f * _bodyScale / U);
        if (IsBoss) p += Vector3.Up * (40f / U);
        return p;
    }
    protected override void ApplyLook()
    {
        base.ApplyLook();
        if (_model != null && _pitch != 0f) _model.Transform = new Transform3D(_model.Transform.Basis * Basis.FromEuler(new Vector3(Mathf.DegToRad(_pitch), 0f, 0f)), _model.Transform.Origin);
    }
    protected override void SetRunAnm() => SetBck(_next == null ? BckRun : BckWalk);   // a carrying boss walks its run
    protected override void KyoroTick() { if (_prev == null) base.KyoroTick(); }

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    /// <summary>TDangoHamuKuri::isHitValid: only a lone one (no holder, nothing held) can be stomped.</summary>
    protected override bool IsHitValid(HitMessage msg) => _prev == null && _next == null && base.IsHitValid(msg);
    /// <summary>TDangoHamuKuri::attackToMario: a held body, or one he is not standing above -> THamuKuri's; else (he is on the tower) the message only while the boss attacks.</summary>
    protected override void AttackToMario()
    {
        if (_prev != null || Pos().Y + 20f > MarioPos().Y) { base.AttackToMario(); return; }
        if (_boss != null && IsInstanceValid(_boss) && _boss.IsNerve(NerveAttack)) SendAttackMsgToMario();
    }
    protected override bool IsCollidMove(Node3D other) => _prev == null && other is SmsSmallEnemy e && !e.Airborne && !_airborne;   // TSmallEnemy's; a held body pushes nothing
    /// <summary>TDangoHamuKuri::behaveToWater: the top of a chain is knocked off into a free Stu; the boss hops with the tower; a middle one turns.</summary>
    protected override void BehaveToWater()
    {
        if (_next == null)
        {
            if (_prev == null) { base.BehaveToWater(); return; }
            if (_sprayCooldown <= 1)
            {
                var holder = _prev;
                holder._next = null; holder._sprayCooldown = 1;
                _prev = null;
                ResetSpine(); SetNext(NerveGraphWander);
                _spin = 0f; _pitch = 0f; _turning = false;
                _noBind = false; MotionMode = MotionModeEnum.Grounded;
                Vector3 d = (Pos() - MarioPos()).Normalized();
                _vel = new Vector3(d.X * 5f, 1f, d.Z * 5f);
                GlobalPosition += Vector3.Up * (20f / U);
                _airborne = true;
            }
            return;
        }
        if (_prev == null)
        {
            if (!_airborne)
            {
                GlobalPosition += Vector3.Up * (10f / U);
                _airborne = true; _vel = new Vector3(0f, 2f, 0f);
                PushNerve(NerveHitWaterJump);
            }
            return;
        }
        if (!_prev._turning)
        {
            _turning = true; _spin = 1f;
            if (TurnSound != null && TurnSound.Stream != null) TurnSound.Play();
        }
    }
    protected override void Dead()
    {
        base.Dead();
        if (_prev != null) { _prev._next = null; _prev = null; }   // a dead body leaves the chain
    }

    protected override bool ExecuteNerve(int n)
    {
        if (n == NerveDangoWait)                                 // TNerveDangoHamuKuriWait: the wait anim from a random frame, for ever
        {
            if (_time < 2 && _anim != null) { SetWaitAnm(); if (_anim.IsPlaying()) _anim.Seek(Lerp(0f, 30f, Rand()) / 60f, true); }
            return false;
        }
        return base.ExecuteNerve(n);
    }
}
