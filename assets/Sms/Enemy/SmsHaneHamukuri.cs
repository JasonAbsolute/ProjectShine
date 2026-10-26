using System;
using Godot;

/// <summary>
/// SOARIN' STU -- THaneHamuKuri (Enemy/hamukuri.cpp 1291-1555; JP ハネハムクリ, the winged Stu of Pinna) on <see cref="SmsHamukuri"/>, with the
/// shipped /enemy/hanehamukuri.prm (pass 125f, feature/enemy-base). It is the Stu's code with the body in the AIR: THaneHamuKuri::walkBehavior and
/// ::bind replace TWalker's ground bind.
///
/// THE FLIGHT (walkBehavior, every tick a nerve walks): the body's height = BASE (unk230: the ground under it, ramped toward a new ground height
/// at (dh / 120) u a tick, at most 10) + RISE (unk234: climbs 1 u a tick to FlyBaseHeight 600 while wandering / attacking / preparing, decays 1 a
/// tick elsewhere) + BOB (unk210: FlyBaseAmplitude 100 x sin of a FlyBaseFrequency 300-tick cycle). It wanders (GraphWander, the wait anim = the
/// flap, march 3 u/tick, turn 0.5 deg) and finds Mario 1000 u around / 1200 u of height (180 deg); TWalkerEnemy's Jump -> Attack flies at him
/// (march x3 = 9 u/tick); isReachedToGoal is on the GROUND PLANE: within 10 u (MsVECMag2 &lt; 100) over him with the rise within 10 of its height
/// -> the DIVE (unk21C): TNerveDoroHanePrepareAttack pushed -- 50 ticks climbing 4 u a tick, 50 ticks plunging 15 -- then back in Attack / Wander
/// the plunge goes on at 15 u a tick (the body stretching tall above 200 u of rise, squashing to 0.8 x below) until the ground: PARTICLE_MS_HIPDROP_C,
/// the rumble, MSD_SE_MA_HIP_ATTACK (<see cref="DiveHitFxScene"/> / <see cref="DiveHitSound"/> slots), then TNerveHaneHamuKuriUpWait: 400 ticks of the
/// wait anim rising 2 u a tick, turning toward him, the squash recovering (x/z x0.9 a tick, y x1.3) -- then Attack / Wander again. The HIT is the
/// body's attack cylinder (100 x 60 x 0.7) on his damage cylinder on the way down: attackToMario = MSD_SE_EN_HANEKURI_ATTACK + his damage, NO hop
/// and no PostAttack (unlike the Stu). bind: the base point (height - rise - bob) carries the velocity under gravity 0.15 and lands on the ground
/// + 1.05 (the hop of a wall or the Jump nerve is a real hop; the hover itself is NOT airborne, so it finds Mario and pushes other enemies apart).
/// WATER (behaveToWater): forceRoll(Mario, water) ALWAYS -- vy FirstVelocityY 10, 30 u/tick away from him, the roll anim; TNerveHamuKuriBoundFreeze
/// runs with TWalkerEnemy::walkBehavior (the flight math idle, the height frozen) so the knock is a true arc at hover height until the base point
/// comes back down to its start (~134 ticks, ~3900 u of travel -- as the game's numbers give it); BoundNum 0 -> the first landing is straight back
/// to Wander; a wall on the way = isHitWallInBound -> WallDie (the crash anim, the splat on the wall). STOMP: isHitValid = not hidden -> Die with
/// the down anim (14 frames), nothing dropped (GenItemRate 0: the DropTable slot is still honoured if set). The 4-clip table (crash / down / roll /
/// wait) sits in the Stu's slots 1 / 2 / 7 / 13; wait doubles as walk and run. THaneHamuKuri::mBoundFly (a static the game never sets; <see
/// cref="BoundFly"/>): the hopping flight -- a NormalJumpVy 15 (AttackJumpVy 10 attacking) hop whenever grounded instead of the hover.
/// NOT PORTED (logged): the bank (mRotation.z from the heading), THaneHamuKuri2 (TNerveHaneHamuKuriMoveOnGraph: the Pinna graph flyer), the
/// cap-stealing TDoroHaneKuri (dorohane.bmd), the long-fall Land of the Stu (no clip), the fruit flip (no flail clip), the kick.
/// </summary>
[GlobalClass]
public partial class SmsHaneHamukuri : SmsHamukuri
{
    // ------------------------------------------------------------------------------------------------------------------ hanehamukuri.prm
    public class HaneParams : HamukuriParams
    {
        public float NormalJumpVy = 8f, AttackJumpVy = 4f, FlyBaseHeight = 100f, FlyBaseAmplitude = 50f, FlyBaseFrequency = 100f;
    }
    /// <summary>The shipped /enemy/hanehamukuri.prm (params.szs on his disc): the Stu's numbers where the file does not differ.</summary>
    public static readonly HaneParams HanePrm = new HaneParams
    {
        HeadHeight = 200f, BodyRadius = 5f, WallRadius = 100f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 15, ActiveEnemyNum = 15,   // mHeadHeight = 200 in THaneHamuKuri::reset
        JumpForce = 5f, SearchLength = 1000f, SearchHeight = 1200f, SearchAware = 10f, SearchAngle = 180f, GiveUpLength = 2000f, GiveUpHeight = 1500f,
        AttackWait = 0, FreezeWait = 300, DamageRadius = 120f, DamageHeight = 100f, AttackRadius = 100f, AttackHeight = 60f,
        TurnSpeedLow = 0.5f, TurnSpeedHigh = 0.5f, BodyScaleLow = 0.7f, BodyScaleHigh = 0.7f, GenItemRate = 0f, GenEggRate = 0f, WaitTime = 100,
        StampRange = 0f, ZigzagCycle = 200f, ZigzagAngle = 5f, MarchSpeedLow = 3f, MarchSpeedHigh = 3f,
        WaterCoeff = 30f, WaterAttackCoeff = 30f, FirstVelocityY = 10f, VelocityRate = 1.5f, FirstKickVelocityY = 2f, CanSearchDist = 800f,
        BoundNum = 0, SearchActorTimer = 500, JitabataTimer = 200, FlyTimer = 500, TrampleBonusNum = 3, CrashBonusNum = 3, SerialCrashFrame = 15, KyoroTimer = 500,
        NormalJumpVy = 15f, AttackJumpVy = 10f, FlyBaseHeight = 600f, FlyBaseAmplitude = 100f, FlyBaseFrequency = 300f,
    };
    protected override HamukuriParams HPrm => HanePrm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>THaneHamuKuri::mBoundFly: the hopping flight instead of the hover (a static the shipped game leaves off).</summary>
    [Export] public bool BoundFly;
    /// <summary>The dive's landing: PARTICLE_MS_HIPDROP_C at the body + SMSRumbleMgr 0x15 + MSD_SE_MA_HIP_ATTACK -- a one-shot scene (an SmsFxBurst or a GPUParticles3D), or nothing.</summary>
    [Export] public PackedScene DiveHitFxScene;
    [Export] public AudioStreamPlayer3D DiveHitSound;

    // ------------------------------------------------------------------------------------------------------------------ the bck table
    // /scene/hanekuri: crash / down / roll / wait (the archive's order = hanekuri_bastable), on the Stu's slots the shared code uses (crash 1, down 2,
    // roll 7, wait 13); the empty slots are clips this kind has not. Frames + loops read off the disc (bck.py): 5 / 14 / 12 (loop) / 30 (loop).
    private static readonly string[] Names = { null, "hanekuri_crash", "hanekuri_down", null, null, null, null, "hanekuri_roll", null, null, null, null, null, "hanekuri_wait", null };
    private static readonly int[] Frames = { 0, 5, 14, 0, 0, 0, 0, 12, 0, 0, 0, 0, 0, 30, 0 };
    private static readonly bool[] Loops = { false, false, false, false, false, false, false, true, false, false, false, false, false, true, false };
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves
    private const int NerveUpWait = NerveSpecies + 10, NervePrepareAttack = NerveSpecies + 11;
    protected override string NerveName(int n) => n switch { NerveUpWait => "UpWait", NervePrepareAttack => "PrepareAttack", _ => base.NerveName(n) };
    /// <summary>THaneHamuKuri::init: mSpine->initWith(TNerveWalkerGraphWander) -- no Generate.</summary>
    protected override int DefaultNerve => NerveGraphWander;

    // ------------------------------------------------------------------------------------------------------------------ the flight's state
    protected float _flyBase;                 // unk230: the ground the flight stands on, units
    protected float _flyRise;                 // unk234: the climb over it
    protected float _flyBob;                  // unk210: the sine
    protected float _flyAdjust;                 // unk214: the base's ramp toward a new ground height, u a tick
    protected float _flyPhase;                 // unk20C: the sine's tick counter
    protected bool _dive;                     // unk21C
    protected float _scaleXZ, _scaleY;         // mScaling.x/z, mScaling.y (the body scale, squashed by the dive)

    public float FlyRise => _flyRise;
    public float FlyBase => _flyBase;
    public float FlyBob => _flyBob;
    public bool Diving => _dive;
    public float ScaleY => _scaleY;
    public float HeightOverGround => Pos().Y - _groundY;
    public new string DebugState => $"{NerveNameNow} t{NerveTime} bck {BckName} air {_airborne} hp {_hp} dead {_dead} base {_flyBase:F0} rise {_flyRise:F0} bob {_flyBob:F0} dive {_dive} sy {_scaleY:F2} y {Pos().Y:F0} ground {_groundY:F0} vel {_vel}";

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    protected override bool FlyBind => true;
    protected override Vector3 LookScale => new Vector3(_scaleXZ, _scaleY, _scaleXZ);

    /// <summary>THaneHamuKuri::reset: TWalkerEnemy::reset, the head height 200, the base = the ground under the body, the flight's numbers cleared.</summary>
    protected override void Reset()
    {
        base.Reset();
        _noBind = true;                                          // the base's gravity and velocity are this species' own (its bind below)
        MotionMode = MotionModeEnum.Floating;
        _groundY = GroundYBelow();
        _flyBase = _groundY;
        ResetFlyParam();
        _scaleXZ = _scaleY = _bodyScale;
        ApplyLook();                                             // the base's Reset drew the look before the squash scale existed
    }
    private void ResetFlyParam() { _flyAdjust = 0f; _flyBob = 0f; _flyRise = 0f; _flyPhase = 0f; _dive = false; }

    protected override void KyoroTick() { }                      // no look-around clip: the flap is the walk
    protected override void MoveObjectPre()
    {
        _noBind = true;
        base.MoveObjectPre();
    }
    /// <summary>THaneHamuKuri::isReachedToGoal: on the ground plane, MsVECMag2 &lt; 100 -> within 10 u.</summary>
    protected override bool IsReachedToGoal()
    {
        Vector3 d = Goal() - Pos(); d.Y = 0f;
        return d.LengthSquared() < 100f;
    }
    protected override void LaunchVelocity() { _launching = false; }   // no launcher arc: it rises from its point
    protected override void OnBodyContact(Node3D other) { }     // no flail clip: a fruit does nothing to it

    // anims: hanekuri_bastable -- wait (3) is the walk and the run too, roll (2), crash (0), down (1)
    protected override void SetGenerateAnm() => SetBck(BckWait);
    protected override void SetWalkAnm() => SetBck(BckWait);
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetRunAnm() => SetBck(BckWait);
    protected override void SetFreezeAnm() => SetBck(BckWait);
    protected override void SetMeltAnm() => SetBck(BckDown);
    protected override void SetRollAnm() => SetBck(BckRoll);
    protected override void SetCrashAnm() { _fly = false; SetBck(BckCrash); }
    protected override void SetDeadAnm() => SetBck(BckDown);     // (releaseCap when it held his cap: the cap kinds are not ported)

    /// <summary>THaneHamuKuri::attackToMario: MSD_SE_EN_HANEKURI_ATTACK + sendAttackMsgToMario -- no hop, no PostAttack.</summary>
    protected override void AttackToMario()
    {
        if (AttackVoice != null && AttackVoice.Stream != null) AttackVoice.Play();
        SendAttackMsgToMario();
    }
    /// <summary>THaneHamuKuri::behaveToWater: forceRoll(Mario's position, water), always.</summary>
    protected override void BehaveToWater() => ForceRoll(MarioPos(), true);
    /// <summary>THaneHamuKuri::isHitValid: a hidden one takes nothing; no serial kill.</summary>
    protected override bool IsHitValid(HitMessage msg) => _model == null || _model.Visible;
    /// <summary>THaneHamuKuri::isCollidMove = TSmallEnemy's: both on the ground (a hovering one counts as grounded).</summary>
    protected override bool IsCollidMove(Node3D other) => other is SmsSmallEnemy e && !e.Airborne && !_airborne;

    /// <summary>THaneHamuKuri::walkBehavior: the flight math, then (not diving) TWalkerEnemy's walk.</summary>
    protected override void WalkBehavior(int mode, float k)
    {
        float H = HanePrm.FlyBaseHeight, F = HanePrm.FlyBaseFrequency, A = HanePrm.FlyBaseAmplitude;
        float groundPlus = _groundY + 1f;                        // mGroundHeight as bind leaves it (checkGround + 1)
        if (BoundFly)
        {
            if (!_airborne)
            {
                float vy = mode == 2 ? HanePrm.AttackJumpVy : HanePrm.NormalJumpVy;
                SetY(Pos().Y + 10f);
                _airborne = true;
                _vel = new Vector3(0f, vy, 0f);
            }
        }
        else
        {
            if (!_dive)
            {
                if (_flyAdjust == 0f && MathF.Abs(_flyBase - groundPlus) > 5f)
                    _flyAdjust = Mathf.Clamp((groundPlus - _flyBase) / 120f, -10f, 10f);
                if (_flyAdjust > 0f) { _flyBase += _flyAdjust; if (_flyBase > groundPlus) _flyAdjust = 0f; }
                if (_flyAdjust < 0f) { _flyBase += _flyAdjust; if (_flyBase < groundPlus) { _flyAdjust = 0f; _flyBase = 1f + groundPlus; } }
            }
            bool flying = IsNerve(NerveGraphWander) || IsNerve(NerveAttack) || IsNerve(NervePrepareAttack);
            if (flying)
            {
                if (!_dive)
                {
                    _flyPhase += 1f;
                    if (_flyPhase > F) _flyPhase = 0f;
                    if (_flyRise < H) _flyRise += 1f;
                    if (_flyRise > H) _flyRise -= 1f;
                }
                if (IsNerve(NerveAttack) || IsNerve(NerveGraphWander))
                {
                    if (_dive)
                    {
                        float ground = GroundYBelow();           // checkGround(pos + 2 x head): the raw ground
                        _groundY = ground;
                        if (_flyBase + _flyRise + _flyBob > ground)
                        {
                            _flyRise -= 15f;
                            _flyBase = ground; _flyBob = 0f; _flyAdjust = 0f; _flyPhase = 0f;
                            if (_flyRise + _flyBob > 200f) _scaleY = Mathf.Clamp(1.1f * _scaleY, 0f, 1.3f * _bodyScale);
                            else _scaleY = Mathf.Clamp(0.8f * _scaleXZ, 0.5f * _bodyScale, 1.3f * _bodyScale);
                        }
                        else
                        {
                            _dive = false; _flyAdjust = 0f; _flyBob = 0f; _flyRise = 0f; _flyPhase = 0f;
                            SpawnBurst(DiveHitFxScene, new Transform3D(Basis.FromScale(Vector3.One * _bodyScale), GlobalPosition));   // PARTICLE_MS_HIPDROP_C (+ the rumble, MSD_SE_MA_HIP_ATTACK)
                            if (DiveHitSound != null && DiveHitSound.Stream != null) DiveHitSound.Play();
                            SetGoalMario();
                            PushNerve(NerveUpWait);
                            _diveHits++;
                        }
                    }
                    else if (IsReachedToGoal() && _flyRise > H - 10f)
                    {
                        _dive = true;
                        PushNerve(NervePrepareAttack);
                    }
                }
            }
            else if (_flyRise > 0f) _flyRise -= 1f;
            if (!_dive) _flyBob = A * Mathf.Sin(Mathf.DegToRad(_flyPhase * 360f / F));
            SetY(_flyBase + _flyRise + _flyBob);
        }
        if (IsNerve(NerveUpWait) && _flyRise < H) _flyRise += 1f;
        // (mRotation.z = MsGetRotFromZaxis(the heading).z, the bank, and UpWait's mRotation.x x 0.8: not ported)
        if (!_dive && !IsBck(BckKyoro)) base.WalkBehavior(mode, k);
        if (_dive && Pos().Y < _groundY) SetY(_groundY);
    }
    private int _diveHits;
    /// <summary>For the drives: the dives that reached the ground this life.</summary>
    public int DiveHits => _diveHits;

    /// <summary>THaneHamuKuri::bind (in setBehavior's slot: after the spine, before the step): the base point = height - rise - bob carries the velocity;
    /// gravity; the ground re-read (checkGround + 1) while the base is not ramping; landed at the ground + 1.05 -> not airborne, the velocity zeroed;
    /// the velocity's XZ goes into the step, its Y into the height. Skipped by LIVE_FLAG_UNK10 (the wall crash).</summary>
    protected override void SetBehavior()
    {
        if (IsNerve(NerveWallDie) || _dead) return;
        float velY = _vel.Y;
        float basePoint = Pos().Y - _flyRise - _flyBob + velY;
        _vel.Y -= GravityY();
        if (_vel.Y < -40f) _vel.Y = -40f;
        float groundPlus = _groundY + 1f;
        if (basePoint <= groundPlus + 0.05f) { _airborne = false; _vel = Vector3.Zero; }
        else { _airborne = true; _lin += new Vector3(_vel.X, 0f, _vel.Z); }
        SetY(Pos().Y + velY);
        if (_dive && Pos().Y < _groundY) SetY(_groundY);
    }
    private void SetY(float yUnits) => GlobalPosition = new Vector3(GlobalPosition.X, yUnits / U, GlobalPosition.Z);

    // ------------------------------------------------------------------------------------------------------------------ the species' nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveUpWait:                                    // TNerveHaneHamuKuriUpWait
                if (_time < 1) { SetWaitAnm(); SetGoalMario(); }
                _scaleXZ = Mathf.Clamp(_scaleXZ * 0.9f, _bodyScale, _bodyScale * 2f);
                _scaleY = Mathf.Clamp(_scaleY * 1.3f, 0f, _bodyScale);
                if (_time > 400) return true;
                _flyRise += 1f;
                WalkBehavior(3, 0.5f);
                return false;

            case NervePrepareAttack:                             // TNerveDoroHanePrepareAttack (THaneHamuKuri uses it too)
                if (_time < 50) _flyRise += 4f;
                else _flyRise -= 15f;
                if (_time > 100) return true;
                WalkBehavior(0, 0.5f);
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
