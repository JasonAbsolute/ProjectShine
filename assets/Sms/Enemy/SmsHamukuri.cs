using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// STROLLIN' STU -- THamuKuri (Enemy/hamukuri.cpp; JP ハムクリ) on <see cref="SmsSmallEnemy"/>, with the shipped /enemy/hamukuri.prm (pass 125).
/// The plain kind only; the winged (HaneHamuKuri), goop / cap-stealing (DoroHamuKuri), stack (Dango), fire (FireHamuKuri) kinds share this
/// class in the game and are later passes.
///
/// WANDER (TNerveWalkerGraphWander + THamuKuri::moveObject): setWalkAnm is the KYORO look-around (bck 4, 59 frames, standing: walkBehavior does
/// nothing while it plays), then the walk (14); every KyoroTimer (500) ticks of wandering it looks around again. March 1 u/tick (1.6 m/s),
/// turn 0.9..1 deg a tick, body scale 1. FINDING MARIO: 800 u ahead within 90 deg (10 u aware), 1000 u of height; with his tank EMPTY and
/// pumping, five times as far (isFindMario x5, GiveUpLength x3) -- MSD_SE_EN_HMKRI_VO_ATTACK, then TWalkerEnemy's Jump -> Attack: the run (8)
/// at march x3 / turn x3 until he is 1500 u away, 650 u higher, or in water. The hit: his damage (dmghamukuri.prm: 1 HP, no knock-down type,
/// min speed 30, 300 i-ticks) and the hop + PostAttack's wait (13) for 200 ticks. A fall of 250+ u while wandering / attacking -> Land (5) on
/// touchdown. STOMP / GROUND POUND: kill -> Die with the down anim (2) -- or turn_down (10) when it lay flailing -- then the DISAP puffs; a Stu
/// on its back (jitabata, bck 3) stomped = requestSerialKill: every other flailing Stu dies too, and 3+ at once (TrampleBonusNum) leave a
/// 1-UP (his 1_up_mushroom scene, <see cref="OneUpScene"/>). WATER (behaveToWater): on the ground -> JITABATA: the turn_start (12) onto its
/// back, the flail (3, looping) for JitabataTimer 800 ticks, the turn_end (11) back up, then Wait; sprayed again while flailing = noted
/// (unk165); in the air and falling -> forceRoll AWAY from Mario (water: WaterCoeff 30 u/tick, vy FirstVelocityY 5). THE ROLL (BoundFreeze, the
/// roll anim 7): a wall in its way (isHitWallInBound: moving INTO the wall) -> WallDie: the crash (1), MS_ENM_WALLHIT (+ _O goop-coloured),
/// MSD_SE_EN_HAMUKURI_CRUSHED, dead after the anim + 60 ticks; else each landing is a bounce (vy 5 again, the speed x VelocityRate 1) up to
/// BoundNum 10, then back to wandering. THE KICK: Mario touching a flailing Stu whose body is below his feet -> forceRoll(kick): vy
/// FirstKickVelocityY 3 with unk1A4 = FLY: it skids along the floor (bind: vy 0, y = the ground) for FlyTimer 600 ticks -- the puck -- into a wall =
/// the crash; a rolling Stu hitting another Stu rolls it too (unk1A3: a serial crash -- 2+ crashes (CrashBonusNum) within SerialCrashFrame 60 =
/// a coin (ACTOR_TYPE_ITEM_UNK3 -> <see cref="SerialCrashScene"/>)). A thrown fruit (|vy| > 2 and |vxz| > 2) landing on it -> Jitabata. The
/// launcher (THamuKuriLauncher::stateLaunch): the Stu appears at the launcher with velocity (0, 4, 4) in the launcher's frame -- here a placed
/// Stu re-launches from its own point with that arc when <see cref="LaunchArc"/> is on (RelaunchTicks), the set anim (9) playing as it flies.
/// Gravity 0.15 a tick (TLiveActor's; mVGenerateGravityY for the vertical launcher kind, not here).
/// NOT PORTED (logged): the football chase (GoForSearchActor -- Pinna's beach ball), the cap steal (Doro), the goop-walk pollution
/// (unk150 17 = 0x11: no stamp bit), the DISAP / WALLHIT particles (scene slots), the sounds (slots), the serial-crash coin's exact item type.
/// </summary>
[GlobalClass]
public partial class SmsHamukuri : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ hamukuri.prm
    public class HamukuriParams : Params
    {
        public float WaterCoeff = 30f, WaterAttackCoeff = 30f, FirstVelocityY = 5f, VelocityRate = 1f, FirstKickVelocityY = 3f, CanSearchDist = 2000f;
        public int BoundNum = 10, SearchActorTimer = 500, JitabataTimer = 800, FlyTimer = 600, TrampleBonusNum = 3, CrashBonusNum = 2, SerialCrashFrame = 60, KyoroTimer = 500;
    }
    /// <summary>The shipped /enemy/hamukuri.prm (params.szs on his disc, pass 125).</summary>
    public static readonly HamukuriParams Prm = new HamukuriParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 80f, ClipRadius = 100f, HitPointMax = 5, InstanceNum = 6, ActiveEnemyNum = 6,
        JumpForce = 3f, SearchLength = 800f, SearchHeight = 1000f, SearchAware = 10f, SearchAngle = 90f, GiveUpLength = 1500f, GiveUpHeight = 650f,
        AttackWait = 200, FreezeWait = 100, DamageRadius = 60f, DamageHeight = 100f, AttackRadius = 90f, AttackHeight = 50f,
        TurnSpeedLow = 0.9f, TurnSpeedHigh = 1f, BodyScaleLow = 1f, BodyScaleHigh = 1f, GenItemRate = 100f, GenEggRate = 80f, WaitTime = 0,
        StampRange = 15f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 1f, MarchSpeedHigh = 1f,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>THamuKuriLauncher: the re-launch flies in with (0, 4, 4) u/tick in the placed frame (the set anim in the air). Off = it just appears.</summary>
    [Export] public bool LaunchArc = true;
    /// <summary>THamuKuriManager::requestSerialKill's 1-UP (3+ flailing Stus stomped at once): his 1_up_mushroom.tscn unless another is set.</summary>
    [Export] public PackedScene OneUpScene;
    public const string OneUpScenePath = "res://1_up_mushroom.tscn";
    /// <summary>checkSerialKill's coin (2+ crashes within 60 ticks): a scene (his yellow coin) or nothing.</summary>
    [Export] public PackedScene SerialCrashScene;
    /// <summary>PARTICLE_MS_ENM_WALLHIT (+ _O): the crash puff -- a one-shot GPUParticles3D scene, or nothing.</summary>
    [Export] public PackedScene WallHitFxScene;
    /// <summary>MSD_SE_EN_HMKRI_VO_ATTACK (the charge cry), MSD_SE_EN_HAMUKURI_CRUSHED (the wall), MSD_SE_EN_DOROKURI_JUMP. Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D AttackVoice, CrushedSound;

    // ------------------------------------------------------------------------------------------------------------------ the bck table
    // /scene/hamukurianm: the archive's files in order = setBckAnm's indices (hamukurianm_bastable); frames + loop attributes read off the disc (bck.py)
    private static readonly string[] Names = { "hamukuri_appear", "hamukuri_crash", "hamukuri_down", "hamukuri_jitabata", "hamukuri_kyoro", "hamukuri_land", "hamukuri_return", "hamukuri_roll", "hamukuri_run", "hamukuri_set", "hamukuri_turn_down", "hamukuri_turn_end", "hamukuri_turn_start", "hamukuri_wait", "hamukuri_walk" };
    private static readonly int[] Frames = { 30, 5, 14, 48, 59, 29, 24, 12, 12, 39, 14, 62, 38, 60, 30 };
    private static readonly bool[] Loops = { false, false, false, true, false, false, false, true, true, false, false, false, false, true, true };
    private const int BckAppear = 0, BckCrash = 1, BckDown = 2, BckJitabata = 3, BckKyoro = 4, BckLand = 5, BckReturn = 6, BckRoll = 7, BckRun = 8, BckSet = 9, BckTurnDown = 10, BckTurnEnd = 11, BckTurnStart = 12, BckWait = 13, BckWalk = 14;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves
    private const int NerveGoForSearchActor = NerveSpecies + 0, NerveBoundFreeze = NerveSpecies + 1, NerveWallDie = NerveSpecies + 2, NerveLand = NerveSpecies + 3, NerveJitabata = NerveSpecies + 4;
    protected override string NerveName(int n) => n switch { NerveGoForSearchActor => "GoForSearchActor", NerveBoundFreeze => "BoundFreeze", NerveWallDie => "WallDie", NerveLand => "Land", NerveJitabata => "Jitabata", _ => base.NerveName(n) };

    // ------------------------------------------------------------------------------------------------------------------ state
    private float _giveUp;                    // unk194: GiveUpLength, x3 while he pumps an empty tank
    private int _kyoro;                       // unk1A8: ticks of wandering since the last look-around
    private bool _wetWhileFlailing;           // unk165
    private bool _rolledByStu;                // unk1A3: this roll came from another Stu (the serial crash)
    private bool _fly;                        // unk1A4: the kicked puck (vy 0, y = the ground) until FlyTimer
    private bool _fellFar;                    // unk1F0: airborne 250+ u over the ground -> Land on touchdown
    private bool _giveUpAttack;               // unk1A2: endHitWaterJump found no Mario -> isResignationAttack
    private int _bounces;                     // unk1E0
    private Vector3 _rollVel;                 // unk1E4
    private Vector3 _wallNormal; private bool _wallHit;
    private static int _serialCrashClock;     // THamuKuriManager::unk6C
    private static readonly List<SmsHamukuri> All = new();   // the manager's objects (the serial kills)
    private bool _launching;                  // the arc from the launcher: the set anim with the velocity

    public bool Flailing => IsNerve(NerveJitabata);
    public bool Rolling => IsNerve(NerveBoundFreeze);
    public bool Flying => _fly;
    public int Bounces => _bounces;

    protected override void Init()
    {
        _flags150 = 17;                       // unk150 = 17 (no 0x20: a dying Stu stamps no goop; no 2: it attacks, never escapes)
        OneUpScene ??= GD.Load<PackedScene>(OneUpScenePath);
    }
    public override void _EnterTree() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    protected override void Reset()
    {
        base.Reset();
        _giveUp = Prm.GiveUpLength; _kyoro = 0; _wetWhileFlailing = false; _rolledByStu = false; _fly = false; _fellFar = false; _giveUpAttack = false;
        _bounces = 0; _wallHit = false; _launching = false;
    }

    // ------------------------------------------------------------------------------------------------------------------ anims
    protected override void SetGenerateAnm() => SetBck(BckSet);
    protected override void SetWalkAnm() => SetBck(BckKyoro);                       // the look-around first; moveObject swaps the walk in when it ends
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetRunAnm() => SetBck(BckRun);
    protected override void SetFreezeAnm() => SetBck(BckWait);                      // THamuKuri never freezes (behaveToWater is its own); the base's fallback
    protected override void SetMeltAnm() => SetBck(BckDown);
    private void SetRollAnm() => SetBck(BckRoll);
    protected override void SetDeadAnm()
    {
        if (_attackerKilled) { _noReward = true; SetAfterDeadEffect(); }          // killed by another attacker's hit: the puff at once, nothing dropped
        else SetBck(IsBck(BckJitabata) ? BckTurnDown : BckDown);
    }
    private void SetCrashAnm()
    {
        _fly = false;
        SetBck(BckCrash);
        if (_rolledByStu && _serialCrashClock == 0) _serialCrashClock = 1;
    }

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    protected override float GravityY() => _melt ? 0.01f : Prm.Gravity;

    protected override void MoveObjectPre()
    {
        if (IsNerve(NerveGraphWander))
        {
            if (++_kyoro > Prm.KyoroTimer) { _kyoro = 0; SetBck(BckKyoro); }   // the look-around every KyoroTimer ticks
            if (IsBck(BckKyoro) && BckEnded()) SetBck(BckWalk);
        }
        if (_launching && !_airborne) _launching = false;
        if (_fly)
        {
            // THamuKuri::bind with unk1A4: no fall -- vy 0, nextPos.y = the ground + 1; the fly ends at FlyTimer (or on a water surface)
            if (_time > Prm.FlyTimer) _fly = false;
            _vel.Y = 0f;
            _airborne = false;
            if (_groundY > Pos().Y - 200f) GlobalPosition = new Vector3(GlobalPosition.X, (_groundY + 1f) / U, GlobalPosition.Z);
        }
        if (_serialCrashClock != 0 && this == All[0]) CheckSerialKill();         // THamuKuriManager::perform(CUE_MOVE), once a tick
    }

    /// <summary>THamuKuri::setBehavior: a long fall (250+ u over the ground) while wandering / attacking -> Land on the ground.</summary>
    protected override void SetBehavior()
    {
        if (_airborne && Pos().Y > _groundY + 250f && !IsNerve(NerveGenerate)) _fellFar = true;
        if (!_airborne && _fellFar && (IsNerve(NerveGraphWander) || IsNerve(NerveAttack))) PushNerve(NerveLand);
    }

    protected override void WalkBehavior(int mode, float k)
    {
        if (!IsBck(BckKyoro)) base.WalkBehavior(mode, k);                          // the look-around stands still
    }
    protected override bool KeepVelocityOnGround() => _fly;
    protected override bool WallHopAllowed() => !IsNerve(NerveBoundFreeze) && !IsNerve(NerveJitabata) && !IsNerve(NerveWallDie) && !IsBck(BckKyoro);

    /// <summary>THamuKuri::isFindMario: he pumps an EMPTY tank -> five times the sight (and three times the give-up length).</summary>
    protected override bool IsFindMario(float k)
    {
        _giveUpAttack = false;
        if (_mario != null && _mario.SmsHoseSpraying && _mario.SmsWaterNow == 0) { _giveUp = Prm.GiveUpLength * 3f; return base.IsFindMario(k * 5f); }
        return base.IsFindMario(k);
    }
    protected override void BehaveToFindMario()
    {
        base.BehaveToFindMario();
        if (AttackVoice != null && AttackVoice.Stream != null) AttackVoice.Play();     // MSD_SE_EN_HMKRI_VO_ATTACK
    }
    protected override bool IsResignationAttack()
    {
        if ((Goal() - Pos()).Length() > _giveUp) { _giveUp = Prm.GiveUpLength; return true; }
        if (_giveUpAttack) { _giveUpAttack = false; return true; }
        return false;
    }
    protected override void EndHitWaterJump() { if (!IsFindMario(1.3f)) _giveUpAttack = true; }
    protected override Mario.SmsDamageProfile DamageProfile => Mario.SmsDamageProfile.Hamukuri;

    /// <summary>THamuKuri::attackToMario: a flailing Stu he touches below his feet is KICKED (forceRoll, the puck); else TWalkerEnemy's.</summary>
    protected override void AttackToMario()
    {
        if (IsNerve(NerveJitabata))
        {
            if (Pos().Y + 10f > MarioPos().Y) ForceRoll(MarioPos(), false);
            return;
        }
        base.AttackToMario();
    }

    /// <summary>THamuKuri::behaveToWater: rolling / crashing take nothing more; flailing notes it; on the ground -> Jitabata; falling -> the water roll.</summary>
    protected override void BehaveToWater()
    {
        if (IsNerve(NerveBoundFreeze) || IsNerve(NerveWallDie)) return;
        if (IsNerve(NerveJitabata)) { _wetWhileFlailing = true; return; }
        if (!_airborne) { PushNerve(NerveJitabata); return; }
        if (_vel.Y < 0f) ForceRoll(MarioPos(), true);
    }

    /// <summary>THamuKuri::isHitValid: a flailing Stu stomped = requestSerialKill (every other flailing one dies; 3+ = a 1-UP); a hidden one takes nothing.</summary>
    protected override bool IsHitValid(HitMessage msg)
    {
        if (IsBck(BckJitabata)) { RequestSerialKill(); return true; }
        return base.IsHitValid(msg);
    }

    /// <summary>THamuKuri::isCollidMove: a ROLLING Stu rolls the one it hits (the serial crash); a thrown fruit landing on it flips it; else the base push.</summary>
    protected override bool IsCollidMove(Node3D other)
    {
        if (other is SmsHamukuri h && h.IsNerve(NerveBoundFreeze) && !IsNerve(NerveDie) && !IsNerve(NerveBoundFreeze) && !IsNerve(NerveWallDie))
        {
            if (!IsHitWallInBound()) { _rolledByStu = true; ForceRoll(h.Pos(), false); return false; }
        }
        return base.IsCollidMove(other);
    }
    protected override void OnBodyContact(Node3D other)
    {
        if (other is Fruit f)
        {
            Vector3 v = f.LinearVelocity * U / 120f;                                   // the fruit's velocity in units a tick
            if (MathF.Abs(v.Y) > 2f && (MathF.Abs(v.X) > 2f || MathF.Abs(v.Z) > 2f) && !IsNerve(NerveJitabata) && !_airborne && !IsNerve(NerveDie) && !IsNerve(NerveBoundFreeze))
                PushNerve(NerveJitabata);
        }
    }
    protected override void OnWallContact(KinematicCollision3D col) { _wallHit = true; _wallNormal = col.GetNormal(); }

    /// <summary>TSmallEnemy::isHitWallInBound: a wall within WallRadius 25 x scale x 1.1 that the velocity points INTO -> turned to face it, no
    /// collision, LIVE_FLAG_UNK10 (the body stops): the crash. Here: this frame's wall contact with the roll velocity into its normal.</summary>
    private bool IsHitWallInBound()
    {
        if (!_wallHit) return false;
        _wallHit = false;
        Vector3 v = new Vector3(_vel.X + _lin.X, 0f, _vel.Z + _lin.Z);
        if (v.Dot(_wallNormal) > 0f) return false;
        _yaw = YawOf(new Vector3(_wallNormal.X, 0f, _wallNormal.Z));
        _noBind = true; _vel = Vector3.Zero; _lin = Vector3.Zero;
        SetCollisionLayerValue(1, false);
        return true;
    }

    /// <summary>THamuKuri::forceRoll(from, isWater): away from `from` (the vector with y 10 before the normalise) x WaterCoeff (WaterAttackCoeff
    /// while attacking); up 10 u; vy FirstVelocityY (water) or FirstKickVelocityY with the FLY (the kicked puck); -> BoundFreeze.</summary>
    private void ForceRoll(Vector3 from, bool water)
    {
        Vector3 d = new Vector3(Pos().X - from.X, 10f, Pos().Z - from.Z).Normalized();
        d *= IsNerve(NerveAttack) ? Prm.WaterAttackCoeff : Prm.WaterCoeff;
        _airborne = true;
        GlobalPosition += Vector3.Up * (10f / U);
        if (water) d.Y = Prm.FirstVelocityY;
        else { d.Y = Prm.FirstKickVelocityY; _fly = true; }
        _vel = d; _lin = Vector3.Zero;
        PushNerve(NerveBoundFreeze);
    }

    // ------------------------------------------------------------------------------------------------------------------ the manager's bonuses
    /// <summary>THamuKuriManager::requestSerialKill: every other flailing Stu dies with this one; TrampleBonusNum (3) or more -> a 1-UP here.</summary>
    private void RequestSerialKill()
    {
        int trampled = 1;
        foreach (SmsHamukuri h in All)
            if (h != this && IsInstanceValid(h) && h.IsBck(BckJitabata) && !h.IsDead) { h.Kill(); trampled++; }
        if (trampled >= Prm.TrampleBonusNum && OneUpScene != null)
        {
            var up = OneUpScene.Instantiate<Node3D>();
            GetParent().AddChild(up);
            up.GlobalPosition = GlobalPosition;
            if (DebugPrint) GD.Print($"[SMS hamukuri] serial kill: {trampled} flailing Stus -> a 1-UP");
        }
    }
    /// <summary>THamuKuriManager::checkSerialKill: the clock runs SerialCrashFrame (60) ticks from the first Stu-caused crash; CrashBonusNum (2) or more
    /// crashing in that window -> the coin at one of them.</summary>
    private void CheckSerialKill()
    {
        if (++_serialCrashClock > Prm.SerialCrashFrame) _serialCrashClock = 0;
        int count = 0; SmsHamukuri rep = null;
        foreach (SmsHamukuri h in All)
            if (IsInstanceValid(h) && h._rolledByStu && h.IsNerve(NerveWallDie)) { count++; rep = h; }
        if (rep != null && count >= Prm.CrashBonusNum && SerialCrashScene != null)
        {
            var coin = SerialCrashScene.Instantiate<Node3D>();
            GetParent().AddChild(coin);
            coin.GlobalPosition = rep.GlobalPosition;
            _serialCrashClock = 0;
            if (DebugPrint) GD.Print($"[SMS hamukuri] serial crash: {count} -> the coin");
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ the launcher
    protected override void LaunchVelocity()
    {
        if (!LaunchArc) return;
        // THamuKuriLauncher::stateLaunch: (0, 4, 4) rotated by the launcher's frame, the set anim in flight
        _vel = PolarXZ(_spawnYaw, 4f) + Vector3.Up * 4f;
        _airborne = true; _launching = true;
        GlobalPosition += Vector3.Up * (2f / U);
    }

    // ------------------------------------------------------------------------------------------------------------------ the species' nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveBoundFreeze:                               // TNerveHamuKuriBoundFreeze
                if (_time == 0) { SetRollAnm(); _rollVel = _vel; SetGoalMario(); _bounces = 1; }
                if (IsHitWallInBound()) { PushAfterCurrent(NerveWallDie); return true; }
                if (!_fly && !_airborne)
                {
                    if (_bounces >= Prm.BoundNum) { ResetSpine(); SetNext(NerveGraphWander); return false; }
                    _bounces++;
                    GlobalPosition += Vector3.Up * (5f / U);
                    _rollVel *= Prm.VelocityRate;
                    _vel = _rollVel; _airborne = true;
                }
                if (!IsBck(BckKyoro)) base.WalkBehavior(3, 1000f);                // the roll keeps turning toward him (turn only, x1000 = at once)
                return false;

            case NerveWallDie:                                   // TNerveHamuKuriWallDie
                if (_time == 0)
                {
                    SetCrashAnm();
                    SetWallDeadEffect();
                    if (CrushedSound != null && CrushedSound.Stream != null) CrushedSound.Play();
                    SetCollisionLayerValue(1, false);
                    _hp = 0;
                }
                else if (BckEnded() && _time > 60 + BckEndFrame)
                {
                    _noReward = true;
                    GenRandomItem();
                    Dead();
                    return true;
                }
                return false;

            case NerveLand:                                      // TNerveHamuKuriLand
                if (_time == 0) SetBck(BckLand);
                if (BckEnded()) { _fellFar = false; return true; }
                return false;

            case NerveJitabata:                                  // TNerveHamuKuriJitabata
                if (_time == 0) SetBck(BckTurnStart);
                if (BckEnded() || IsBck(BckJitabata))
                {
                    if (IsBck(BckTurnStart)) SetBck(BckJitabata);
                    else if (IsBck(BckTurnEnd)) { if (BckEnded()) { PushAfterCurrent(NerveWait); return true; } }
                    else if (IsBck(BckJitabata) && _time > Prm.JitabataTimer) SetBck(BckTurnEnd);
                }
                return false;

            case NerveGoForSearchActor:                          // not ported (the football): straight back to the wander
                ResetSpine(); SetNext(NerveGraphWander); return false;
        }
        return base.ExecuteNerve(n);
    }

    /// <summary>THamuKuri::setWallDeadEffect: MS_ENM_WALLHIT at joint 1 (+ _O in the goop colour), turned to the body's yaw, scaled.</summary>
    private void SetWallDeadEffect()
    {
        if (WallHitFxScene == null) return;
        var fx = WallHitFxScene.Instantiate<Node3D>();
        GetParent().AddChild(fx);
        fx.GlobalPosition = GlobalPosition + Vector3.Up * (55f / U); fx.Rotation = new Vector3(0f, Mathf.DegToRad(_yaw), 0f); fx.Scale = Vector3.One * _scale;
        if (fx is GpuParticles3D gp) { gp.OneShot = true; gp.Emitting = true; }
    }

    /// <summary>For the drives: the Stu's flags.</summary>
    public string DebugState => $"{NerveNameNow} t{NerveTime} bck {BckName} air {_airborne} fly {_fly} bounces {_bounces} hp {_hp} dead {_dead} vel {_vel}";
}
