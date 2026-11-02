using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// BOO -- TTelesa (Enemy/telesa.cpp; JP テレサ, the telesa archive) on <see cref="SmsSmallEnemy"/> with the shipped /enemy/telesa.prm (SMS pass 151,
/// feature/enemy-sirena). Hotel Delfino's ghost: it drifts between its graph's nodes a little way off the floor, bobbing and tilting as it goes,
/// and hurts Mario on touch (not when he is above it). Untouchable until it is HOSED: a spray knocks it back, reveals it and stuns it (the hit
/// clips) -- sunk to the floor and solid, it can be JUMPED ON (the down clip; coins), then the stun ends and it drifts off again. Four kinds in the
/// game (<see cref="Kind"/>): the LOOP Boo (the hotel's: fades in for a while, fades out, hidden a while, again), the BOX Boo (pink, always
/// visible: a spray turns it into a pink Boo BLOCK Mario stands on for 25 s, then the Boo comes back; a jump on the Boo itself only makes it
/// flinch), the NORMAL Boo (invisible until sprayed; visible for a while after) and the always-visible one.
///
/// THE GAME. TTelesa is a TWalkerEnemy (ACTOR_TYPE_TELESA, unk150 0x12; HIT_CATEGORY_ENEMY OFF and HIT_CATEGORY_BOSS on -- Mario's jump does not
/// find it), body scale 1.5. /enemy/telesa.prm: HeadHeight 120, BodyRadius 30, WallRadius 50, 1 hit point, Search 800 / 600 / 400 / 90 (unused:
/// isFindMario is false), GiveUp 2000 / 650, AttackWait 120, FreezeWait 300, damage 80 x 90, attack 70 x 80, turn 1 / 1.5, march 1 / 1.7, WaitTime
/// 100; its own: FadeInTime 240, AppearTime 240, FadeOutTime 240, FlyHeight 50..100, FlyAmplitude 50..75, FlyFrequency 200..400, TelesaGravityY
/// 0.05, GenAttackerTime 200, TelesaAttackGravityY 0.02, TelesaPowerByWater 40, LoopAppearTime 200, LoopHideTime 300; the static mFlyAngMax 35. The
/// bck table is the archive's order (0 modoki_wait -- the Mario-imitator's, 180; 1 clush 49; 2 down 14; 3 hit 15 loop; 4 hit_end 19; 5 hit_start 9;
/// 6 wait 50 loop). init: the screen texture swapped into H_ma_rak_dummy and LIGHT_TYPE_INDIRECT (a see-through ghost), the fly numbers
/// (setFlyParam: the target height, the bob's amplitude and frequency at random in their ranges), GRAPHWANDER. reset: HIDDEN, head 100, the ENEMY /
/// BOSS categories off, the base colour white (350, 350, 350). load: setTypeNormal (HIDDEN off, the fade INVISIBLE), mDampenedGroundHeight = the
/// placed Y. setBehavior (wandering / waiting / attacking): the bob phase +1 to its frequency, offsetY = amplitude x sin, rotation.x = 10 - 35 x
/// cos (the tilt; the game's `==` typo skips a jump over 5 deg). walkBehavior: TWalkerEnemy's, the fly height +1 a tick to its target; the ground
/// damping speed is (ground - ground) / 120 = 0 for a plain Boo, so the base height STAYS the placed Y; position.y = that + the fly height + the
/// bob. bind: the base point (y - fly - bob) carries the velocity under TelesaGravityY; landed at the ground + 1.05 -> not airborne, the velocity
/// 0 (dying: set on the ground). calcRootMatrix: the drool (TLS_YODARE_L / _S at joints 4 / 3, x the fade alpha); the effect matrix on texmtx 2.
/// moveObject: + changeTevKColor -- the fade: VISIBLE (alpha 255; NORMAL past AppearTime / LOOP past LoopAppearTime -> FADE_OUT, LIGHT_TYPE_INDIRECT;
/// CAN_SEE or frozen: stays), INVISIBLE (alpha 0; LOOP past LoopHideTime -> FADE_IN), FADE_IN (alpha = t x 255 / FadeInTime; at 255 VISIBLE,
/// LIGHT_TYPE_OBJECT), FADE_OUT (alpha = 255 - t x 255 / FadeOutTime; at 0 INVISIBLE). The alpha is K0_A on every material (the body also TEVREG0 =
/// the base colour). receiveMessage (TSmallEnemy's): TRAMPLE / HIP_DROP -> isHitValid (not HIDDEN) -> kill; WATER -> changeByJuice (the box Boo:
/// true) else decHpByWater (NOTHING for a Boo) + behaveToWater. behaveToWater: unk165; not frozen -> the velocity = normalize(away from Mario, y 2)
/// x 5, +2 u, AIRBORNE, FADE_IN unless visible, FREEZE pushed, unk1C8, HIT_CATEGORY_ENEMY ON (now he can jump on it). FREEZE: hit_start; a clip's
/// end: hit_end ended and not flying -> ENEMY off, done; else sprayed since (resetBaseGround) or hit_start -> hit (loop); else hit_end;
/// reduceFlyForce (the fly height -4 a tick to 0, the bob toward 0 by 4, the phase and the tilt x 0.9), walkToCurPathNode(0, turn x 0.2) (the
/// turn only). kill -> DIE: decHitPoints; at 0: FADE_IN if hidden, NO_COLLISION; the down clip (an attacker: TLS_CHANGE instead); reduceFlyForce;
/// the clip's end -> DEAD, genRandomItem (the coins), the manager's generatePetBottle (every FIFTH Boo killed drops a large water bottle), 0xCE.
/// TRAMPLE (the box Boo's isHitValid): the clush clip, false -- a jump on it only flinches it. attackToMario: HIDDEN, or Mario not above (his y <
/// y + attackHeight - 50) -> ATTACK. isReachedToGoal: XZ within sqrt(200) u. setWalkAnm: NO_COLLISION off, wait, the bob phase 0. isCollidMove:
/// everything but an effect object. TBoxTelesa (箱テレサ): CAN_SEE, unk150 0x40, the base colour PINK (600, 270, 220), a TelesaBlock registered
/// dead; changeByJuice: the block appears at the body (scale 1, rate 0.1, the Boo's yaw), the Boo HIDDEN + NO_COLLISION, CHANGE pushed with FREEZE
/// after it, MSD_SE_EN_TELESA_FIX + OBJ_AWAY_INTO_GRAF. CHANGE (TNerveSmallEnemyChange): the clip rate 0, 0xCD; scalingChangeActor (the rate +0.02
/// XZ to 1.5, +0.01 Y to 0.6); changeMove: for the first 80 ticks the block's y bob (x unk188 = 0: none) and three turns of spin; past 400 and in the
/// last 200 of getChangeBlockTime 3000 the block blinks 10 in 20; at 3000 changeOut: NO_COLLISION, MSD_SE_EN_TELSA_RECOVER, HIDDEN off, the body at
/// the block, 0xCD, the clip rate back, the block killed. TSeeTelesa: CAN_SEE. TLoopTelesa: LOOP. TTelesaManager: modoki.bmd for the imitators,
/// telesaForceKill, setFlagOutOfCube (the area cube). TMarioModokiTelesa / TKageMarioModoki (the Boos that copy Mario) and the King Boo's
/// attackers (initAttacker: reset, CAN_SEE, at his mouth - 150 with a lift of 8, ENEMY off, hidden 10 ticks; TNerveTelesaAttackMario: the wait clip, the
/// first point 1000 u behind the boss's facing, then points 0.7..1.6 of the way to Mario, walkBehavior(2, 2); the base height damped to the table's 500 at
/// (ground - 500) / 120 clamped +-2, snapped to 501 from under; TelesaAttackGravityY 0.02; the hose: y 30 and PowerByWater 40; killed: the TLS_CHANGE puff
/// and no clip, no drops) are pass 153's -- <see cref="SmsInitAttacker"/>, for <see cref="SmsBossTelesa"/>.
///
/// THE PORT. The base's walker nerves (GraphWander, Wait, Die) with the species' TelesaFreeze, Trample and Change; its own HitCheckMario (the
/// jump finds it only while <see cref="_tangible"/>, the sprayed stun -- otherwise he passes through a ghost; the hose always; the touch attack);
/// its own SmsSprayedByWater (no hit point off for water). FlyBind: the species sets its height (the base point + the fly height + the bob) and
/// airborne flag itself; the knock-back velocity rides the base point under TelesaGravityY. The fade is a `fade` uniform on per-body copies of
/// the four TelesaToon materials (<see cref="Fade"/>). The block is <see cref="SmsTelesaBlock"/> (TelesaBlock.tscn, a StaticBody3D platform sized
/// to the glb at its live scale). Knobs (grouped, plain English): Kind, Deviations (SpeedScale), Effects (the change puff, the drool slot, the
/// block scene), Sounds (eight slots). DEVIATIONS (logged): the fade is alpha (no screen-texture refraction); the ground-damping typo is kept
/// (the base height = the placed height -- place it where it should float); the fifth-kill water bottle is the DropTable's business; the
/// imitators are not here (the boss's attackers are, pass 153: pooled Telesa.tscn bodies the boss owns -- the table height they fly at is his floor); the
/// camera shakes / the area cube are not ported.
/// </summary>
public partial class SmsTelesa : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ telesa.prm
    public class TelesaParams : Params
    {
        public int FadeInTime = 60, AppearTime = 60, FadeOutTime = 60, GenAttackerTime = 500, LoopAppearTime = 100, LoopHideTime = 100;
        public float FlyHeightMax = 200f, FlyHeightMin = 100f, FlyAmplitudeMax = 100f, FlyAmplitudeMin = 100f, FlyFrequencyMax = 200f, FlyFrequencyMin = 100f;
        public float TelesaGravityY = 0.1f, TelesaAttackGravityY = 0.01f, TelesaPowerByWater = 40f;
    }
    /// <summary>The shipped /enemy/telesa.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly TelesaParams Prm = new TelesaParams
    {
        HeadHeight = 120f, BodyRadius = 30f, WallRadius = 50f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 100, ActiveEnemyNum = 50, JumpForce = 10f,
        SearchLength = 800f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 2000f, GiveUpHeight = 650f, AttackWait = 120, FreezeWait = 300,
        DamageRadius = 80f, DamageHeight = 90f, AttackRadius = 70f, AttackHeight = 80f, TurnSpeedLow = 1f, TurnSpeedHigh = 1.5f, BodyScaleLow = 1.5f, BodyScaleHigh = 1.5f,
        GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100, StampRange = 5f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 1f, MarchSpeedHigh = 1.7f,
        FadeInTime = 240, AppearTime = 240, FadeOutTime = 240, FlyHeightMax = 100f, FlyHeightMin = 50f, FlyAmplitudeMax = 75f, FlyAmplitudeMin = 50f,
        FlyFrequencyMax = 400f, FlyFrequencyMin = 200f, TelesaGravityY = 0.05f, GenAttackerTime = 200, TelesaAttackGravityY = 0.02f, TelesaPowerByWater = 40f,
        LoopAppearTime = 200, LoopHideTime = 300,
    };
    protected override Params P => Prm;
    /// <summary>TTelesa::mFlyAngMax: the tilt's swing.</summary>
    public const float FlyAngMax = 35f;
    /// <summary>TSmallEnemyManager's block statics: the block's wait, the change's length, the slab's full scale rate.</summary>
    public const int BlockWaitTime = 400, ChangeBlockTime = 3000;
    public const float BlockXZScale = 1.5f, BlockYScale = 0.6f;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    public enum BooKind { Loop, Box, Normal, CanSee }
    [ExportGroup("Kind (the game's four Boos)")]
    /// <summary>Loop = the hotel's (fades in for ~3 s, out, hidden ~5 s, again); Box = the PINK one (always visible; a spray makes it a block Mario stands
    /// on, a jump on it only flinches it); Normal = invisible until sprayed, visible ~4 s after; CanSee = always visible.</summary>
    [Export] public BooKind Kind = BooKind.Loop;
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the drift (the game: 1..1.7 u a tick) and the knock-back.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    /// <summary>How far over the FLOOR under it the Boo's base sits (units; 0 = the game: the base is the floor, and it floats its fly height 50..100 +
    /// the bob over that -- 0.7 to 2.3 m). Raise it for a Boo that should hang high in a room; a stunned one sinks to this base, a dying one to the floor.</summary>
    [Export(PropertyHint.Range, "0,600,5")] public float BaseOffsetUnits = 0f;
    /// <summary>153h (Jaysonn: "the boos should be able to be jumped on"): a jump or a ground pound on a Boo that has NOT been hosed pops it too, while it
    /// shows (a faded-out one is still a ghost) -- a real stomp: Mario's centre over its body, his feet above its middle. Off = the game: his jump passes
    /// through the ghost until the hose stuns it (HIT_CATEGORY_ENEMY off). ON for every Boo since 153i (Jaysonn: the placed ones "not jumpable still");
    /// King Boo's attackers take his AttackersJumpable.</summary>
    [Export] public bool JumpPopsUnsprayed = true;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_tls_change (0xCD): the puff where the box Boo turns into its block and where it comes back. Empty = assets/Sms/Fx/EnemyDisap.tscn.</summary>
    [Export] public PackedScene ChangeFxScene;
    /// <summary>ms_tls_yodare_l / _s: the drool at its mouth (x the fade). Slot.</summary>
    [Export] public PackedScene DroolFxScene;
    /// <summary>The pink block a sprayed box Boo turns into. Empty = assets/Sms/Enemy/Telesa/TelesaBlock.tscn.</summary>
    [Export] public PackedScene BlockScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_TELESA_FIX (the block), TELSA_RECOVER (back from the block), TELESA_APPEAR, TELESA_DISAPPEAR; the bas tables' wait / hit / down / clush.</summary>
    [Export] public AudioStreamPlayer3D FixSound, RecoverSound, AppearSound, DisappearSound, WaitSound, HitSound, DownSound, CrushSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's order; slot 0 modoki_wait is the imitator's, not in this glb)
    private static readonly string[] Names = { "", "telesa_clush", "telesa_down", "telesa_hit", "telesa_hit_end", "telesa_hit_start", "telesa_wait" };
    private static readonly int[] Frames = { 180, 49, 14, 15, 19, 9, 50 };
    private static readonly bool[] Loops = { true, false, false, true, false, false, true };
    protected const int BckModoki = 0, BckClush = 1, BckDown = 2, BckHit = 3, BckHitEnd = 4, BckHitStart = 5, BckWait = 6;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves (TNerveTelesa*)
    protected const int NerveTFreeze = NerveSpecies, NerveTrample = NerveSpecies + 1, NerveChange = NerveSpecies + 2, NerveAttackMario = NerveSpecies + 3;
    private static readonly string[] TelesaNerveNames = { "TelesaFreeze", "Trample", "Change", "AttackMario" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + TelesaNerveNames.Length ? TelesaNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveGraphWander;

    // ------------------------------------------------------------------------------------------------------------------ state
    public enum FadeState { Invisible, FadeIn, FadeOut, Visible }
    private float _flyCur, _flyTarget, _bob, _bobAmp, _bobPhase, _bobFreq = 200f, _dampGround, _rotX;   // mCurrentFlyHeight / mTargetFlyHeight / mFlyBobOffsetY / mFlyBobAmplitude / mFlyBobPhase / mFlyBobFrequency / mDampenedGroundHeight / mRotation.x
    private FadeState _fadeState = FadeState.Invisible; private int _fadeTimer, _fadeLoopTimer; private float _fadeA = -1f;   // mFadeState / mFadeTimer / mFadeLoopTimer / mTelesaFadeColor.a
    private bool _tangible, _sprayedSince;                      // HIT_CATEGORY_ENEMY on (a stunned one takes the jump) / unk165
    private bool _attacker; private float _attackBaseY, _dampSpeed;   // unk184 (King Boo's attacker, pass 153) / its base height (the game's absolute 500 = the casino table) / mGroundHeightDampeningSpeed
    private SmsTelesaBlock _block;                              // mJuiceBlock
    private readonly List<ShaderMaterial> _mats = new();
    /// <summary>For the drives.</summary>
    public FadeState Fade => _fadeState;
    public float FadeAlpha => MathF.Max(0f, _fadeA);
    public bool Tangible => _tangible;
    /// <summary>unk184: one of King Boo's attackers (pass 153) -- visible, homing on Mario at the table's height, knocked far by the hose, a puff when killed.</summary>
    public bool IsAttacker => _attacker;
    public bool IsAttacking => IsNerve(NerveAttackMario);
    public float AttackBaseY => _attackBaseY;
    /// <summary>The kind the fade follows: an attacker is CAN_SEE whatever its scene says.</summary>
    private BooKind KindNow => _attacker ? BooKind.CanSee : Kind;
    public bool IsFrozen => IsNerve(NerveTFreeze);
    public bool IsChanging => IsNerve(NerveChange);
    public bool IsDying => IsNerve(NerveDie);
    public float FlyHeight => _flyCur;
    public float Bob => _bob;
    public float TiltDeg => _rotX;
    public float BaseY => _dampGround;
    public SmsTelesaBlock Block => _block != null && IsInstanceValid(_block) ? _block : null;
    public bool IsVisibleNow => _model != null && _model.Visible && _fadeA > 0.02f;
    public float GroundNow => _groundY;
    public new string DebugState => $"{NerveNameNow} t{NerveTime} bck {BckName} fade {_fadeState}/{_fadeA:F2} tang {_tangible} fly {_flyCur:F0} bob {_bob:F0} tilt {_rotX:F0} air {_airborne} hp {_hp} dead {_dead} y {Pos().Y:F0} base {_dampGround:F0} ground {_groundY:F0} vel {_vel}";

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    protected override bool FlyBind => true;

    /// <summary>TTelesa::init: the actor type and flags, the materials copied per body so the fade is this body's own.</summary>
    protected override void Init()
    {
        _flags150 = 0x12;
        if (Kind == BooKind.Box) _flags150 |= 0x40;
        ChangeFxScene ??= LoadIf("res://assets/Sms/Fx/EnemyDisap.tscn");
        BlockScene ??= LoadIf("res://assets/Sms/Enemy/Telesa/TelesaBlock.tscn");
        _mats.Clear();
        if (_model != null)
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    if (mi.GetActiveMaterial(i) is not ShaderMaterial sm) continue;
                    var copy = (ShaderMaterial)sm.Duplicate();
                    if (Kind == BooKind.Box) copy.SetShaderParameter("c0", new Vector3(600f, 270f, 220f) / 255f);   // cTelesaColor[1]: pink and cute
                    mi.SetSurfaceOverrideMaterial(i, copy);
                    _mats.Add(copy);
                }
            }
    }
    private static PackedScene LoadIf(string path) => ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;

    /// <summary>TTelesa::reset + load: the fly numbers, the head 100, the hit categories off, the kind's fade state, the base height = the placed Y.</summary>
    protected override void Reset()
    {
        base.Reset();
        _noBind = true;                                          // the base's gravity and velocity are this species' own (its bind below)
        MotionMode = MotionModeEnum.Floating;
        if (_block != null && IsInstanceValid(_block)) _block.QueueFree(); _block = null;
        SetFlyParam(1f);
        _bob = 0f; _rotX = 0f; _tangible = false; _sprayedSince = false; _attacker = false; _dampSpeed = 0f;
        // mDampenedGroundHeight = the placed Y: the game's Boos are placed ON their floor (bind lands the base there, the velocity 0) -- here the base is
        // put on the floor under the placed point (+ BaseOffsetUnits) whatever height it was dropped at, so it never hangs airborne over its own floor
        float g = IsInsideTree() ? GroundYBelow() : Pos().Y - 100f * U;
        _dampGround = Pos().Y - g < 50f * U ? g + 1f + BaseOffsetUnits : Pos().Y;   // no floor within 50 m: keep the placed height
        SetY(_dampGround);
        _airborne = false; _vel = Vector3.Zero;
        _fadeTimer = 0; _fadeLoopTimer = 0;
        switch (KindNow)                                         // setTypeNormal / setTypeLoop / setTypeCanSee (the box Boo is CAN_SEE)
        {
            case BooKind.Normal: _fadeState = FadeState.Invisible; break;
            case BooKind.Loop: _fadeState = FadeState.FadeIn; break;
            default: _fadeState = FadeState.Visible; break;
        }
        _fadeA = -1f;
        FadeTick();
        ApplyLook();
    }
    /// <summary>setFlyParam(k): the target height, the bob's amplitude and frequency at random in their ranges; the height from 0.</summary>
    private void SetFlyParam(float k)
    {
        _flyTarget = Lerp(Prm.FlyHeightMin, Prm.FlyHeightMax, Rand()) * k;
        _flyCur = 0f;
        _bobAmp = Lerp(Prm.FlyAmplitudeMin, Prm.FlyAmplitudeMax, Rand()) * k;
        _bobPhase = 0f;
        _bobFreq = MathF.Max(1f, Lerp(Prm.FlyFrequencyMin, Prm.FlyFrequencyMax, Rand()));
    }
    protected override void MoveObjectPre() { _noBind = true; }
    protected override bool IsFindMario(float k) => false;      // TTelesa::isFindMario: never (the Boo drifts; the boss sends the attackers)
    /// <summary>TTelesa::isReachedToGoal: on the ground plane, within 200 u (MsVECMag2 is the length: pass 153 fixed 151's squared reading, 14 u).</summary>
    protected override bool IsReachedToGoal()
    {
        Vector3 d = Goal() - Pos(); d.Y = 0f;
        return d.Length() < 200f;
    }
    protected override void LaunchVelocity() { }
    protected override bool IsCollidMove(Node3D other) => other is SmsSmallEnemy;   // everything but an effect object
    /// <summary>TTelesa::getGravityY: TelesaAttackGravityY (0.02) for an attacker, TelesaGravityY (0.05) else.</summary>
    protected override float GravityY() => _attacker ? Prm.TelesaAttackGravityY : Prm.TelesaGravityY;

    // ------------------------------------------------------------------------------------------------------------------ King Boo's attacker (pass 153)
    /// <summary>TTelesa::initAttacker(from) -- the boss's genAttacker made this body his attacker: reset, visible (CAN_SEE), at his mouth - 150 y with a lift of 8 u a
    /// tick, his yaw, fresh fly numbers, HIT_CATEGORY_ENEMY off, hidden for the nerve's first 10 ticks, the AttackMario nerve. The base height the attacker
    /// settles to is the table's (the game's absolute 500 = the casino's table top; here <paramref name="tableYUnits"/> + 0, the boss's floor).</summary>
    public void SmsInitAttacker(SmsBossTelesa from, Vector3 mouthUnits, float yawDeg, float tableYUnits, float speedScale = 1f)
    {
        GlobalPosition = (mouthUnits + Vector3.Down * 150f) / U;
        _spawnPos = GlobalPosition * U; _spawnYaw = yawDeg; _yaw = yawDeg;
        Reset();
        _attacker = true;
        _attackBaseY = tableYUnits;
        _dampGround = Pos().Y; _dampSpeed = 0f;
        _vel = new Vector3(0f, 8f * speedScale, 0f); _airborne = true;
        SetFlyParam(1f);
        _tangible = false; _sprayedSince = false;
        _fadeState = FadeState.Visible; _fadeA = -1f; FadeTick();
        _stack.Clear(); _cur = NerveNone; _prev = NerveNone; _time = 0;
        PushAfterCurrent(NerveAttackMario);
        if (_model != null) _model.Visible = false;           // LIVE_FLAG_HIDDEN until the nerve's tick 10
        SetCollisionLayerValue(1, true);
        if (AppearSound != null && AppearSound.Stream != null) AppearSound.Play();
        ApplyLook();
        if (DebugPrint) GD.Print($"[SMS telesa] {Name}: attacker from {from?.Name} at {Pos()} base {_attackBaseY:F0}");
    }
    /// <summary>setFirstAttackPoint: the yaw turned about (180 - instanceIndex x 720 in the game: behind the boss's facing), the goal 1000 u ahead.</summary>
    private void SetFirstAttackPoint()
    {
        _yaw = Wrap360(_spawnYaw + 180f);
        SetGoalPoint(Pos() + PolarXZ(_yaw, 1000f));
    }
    /// <summary>setAttackPoint: the goal toward Mario, 0.7..1.6 of the way on the ground plane (it overshoots him).</summary>
    private void SetAttackPoint()
    {
        Vector3 m = MarioPos(), p = Pos();
        float r = Lerp(0.7f, 1.6f, Rand());
        SetGoalPoint(new Vector3(p.X + (m.X - p.X) * r, p.Y, p.Z + (m.Z - p.Z) * r));
    }
    /// <summary>TTelesa::walkBehavior's ground damping for an attacker: toward the table height at (ground - table) / 120 clamped +-2 a tick; under it, snapped to
    /// table + 1 (a plain Boo's speed is (ground - ground) / 120 = 0: the base stays where it was placed).</summary>
    private void DampToAttackBase()
    {
        float target = _attackBaseY;
        if (_dampSpeed == 0f && MathF.Abs(_dampGround - target) > 5f)
            _dampSpeed = Mathf.Clamp((GroundYBelow() + 1f - target) / 120f, -2f, 2f);
        if (_dampSpeed > 0f) { _dampGround += _dampSpeed; if (_dampGround > target) _dampSpeed = 0f; }
        if (_dampSpeed < 0f) { _dampGround += _dampSpeed; if (_dampGround < target) { _dampSpeed = 0f; _dampGround = target + 1f; } }
    }

    // anims (telesa_bastable: 1 clush, 2 down, 6 wait; 3 / 4 / 5 the hit set)
    protected override void SetGenerateAnm() => SetWalkAnm();
    /// <summary>TTelesa::setWalkAnm: NO_COLLISION off; the wait clip (the drift IS the wait), the bob phase from 0.</summary>
    protected override void SetWalkAnm()
    {
        SetCollisionLayerValue(1, true);
        if (!IsBck(BckWait)) { SetBck(BckWait); _bobPhase = 0f; }
    }
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetRunAnm() => SetBck(BckWait);
    protected override void SetFreezeAnm() => SetBck(BckHitStart);
    protected override void SetDeadAnm() { SetBck(BckDown); if (DownSound != null && DownSound.Stream != null) DownSound.Play(); }
    protected override void SetMeltAnm() => SetBck(BckDown);

    /// <summary>TTelesa::attackToMario: the touch hurts unless Mario is above it (his y over y + attackHeight - 50).</summary>
    protected override void AttackToMario()
    {
        if (MarioPos().Y < Pos().Y + P.AttackHeight * _bodyScale - 50f) SendAttackMsgToMario();
    }
    /// <summary>TSmallEnemy::isHitValid for a plain Boo (not hidden); TBoxTelesa::isHitValid: the clush flinch, never a kill.</summary>
    protected override bool IsHitValid(HitMessage msg)
    {
        if (Kind == BooKind.Box)
        {
            if (!IsNerve(NerveTrample)) PushNerve(NerveTrample); else SetBck(BckClush);
            return false;
        }
        return _model == null || _model.Visible;
    }
    /// <summary>TTelesa::kill: a hidden one fades in to die.</summary>
    public override void Kill()
    {
        if (_dead) return;
        if (_fadeState == FadeState.Invisible || _fadeState == FadeState.FadeOut) _fadeState = FadeState.FadeIn;
        base.Kill();
    }

    /// <summary>gpObjHitCheck's pair with Mario, the Boo's way: his jump finds it only while HIT_CATEGORY_ENEMY is on (<see cref="_tangible"/>, the
    /// sprayed stun) -- otherwise he passes through the ghost, no bounce; the hose always; the touch attack (attackToMario).</summary>
    protected override void HitCheckMario()
    {
        if (_mario == null || _cur == NerveDie || !GetCollisionLayerValue(1)) return;
        Vector3 g = Pos(), m = MarioPos();
        float s = _bodyScale;
        bool onGround = _mario.IsOnFloor();
        bool rising = !onGround && _mario.Velocity.Y > 0f;
        float ar = onGround ? SmsParams.Mario.HoldRadius : (rising ? SmsParams.Mario.PushupRadius : SmsParams.Mario.TrampleRadius);
        float ah = onGround ? SmsParams.Mario.AttackHeight : (rising ? SmsParams.Mario.PushupHeight : SmsParams.Mario.AttackHeight);
        bool marioHits = CheckDistance(m, ar, ah, g, P.DamageRadius * s, P.DamageHeight * s);
        if (marioHits)
        {
            bool stompable = _tangible;                          // the stunned one: the game's cylinders (a 2.3-m side overlap counts)
            if (!stompable && JumpPopsUnsprayed && IsVisibleNow)
            {
                // 153h/i: a shown, UNHOSED Boo takes a real stomp only -- Mario's centre over its body (within its radius, not the two cylinders' 2.3-m reach)
                // with his feet above its lower third (a Boo at his feet is a ram, not a landing). 153h used the cylinders' overlap for it too, and a
                // swooping attacker that rammed a descending Mario from the side popped a body-width before it reached him (Jaysonn: "the boos from king
                // boo are randomly disappearing"). The lower third, not the middle: his hit check runs every fourth tick and a fast fall crosses 60 u between two.
                float dx = m.X - g.X, dz = m.Z - g.Z, r = P.DamageRadius * s;
                stompable = dx * dx + dz * dz < r * r && m.Y > g.Y + P.DamageHeight * s * 0.35f;
            }
            if (stompable)
            {
                bool falling = !onGround && _mario.Velocity.Y < 0f && g.Y < m.Y;
                var st = _mario.stateOfMario;
                bool noTrample = st == Mario.MarioState.diving || st == Mario.MarioState.singleJumpDive || st == Mario.MarioState.doubleJumpDive || st == Mario.MarioState.tripleJumpDive
                    || st == Mario.MarioState.bellySlidingFromDive || st == Mario.MarioState.swimming || st == Mario.MarioState.ledgeHang || st == Mario.MarioState.ledgeClimb
                    || st == Mario.MarioState.treeGrab || st == Mario.MarioState.treeClimb || st == Mario.MarioState.hurt || st == Mario.MarioState.dead;
                if (falling && st == Mario.MarioState.groundPoundFalling)
                {
                    if (IsHitValid(HitMessage.HipDrop)) { _attackerKilled = false; Kill(); }
                    return;
                }
                if (falling && !noTrample)
                {
                    if (IsHitValid(HitMessage.Trample)) { _attackerKilled = false; Kill(); }
                    _mario.StompBounce();
                    return;
                }
            }
            if (_mario.SmsHoseSpraying) SmsSprayedByWater(m + Vector3.Up * 80f / U);
            if (_dead || _cur == NerveDie) return;
        }
        bool weHit = CheckDistance(g, P.AttackRadius * s, P.AttackHeight * s, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight);
        if (weHit) AttackToMario();
    }

    /// <summary>HIT_MESSAGE_SPRAYED_BY_WATER: the mark; once per cooldown changeByJuice (the box Boo's block) else behaveToWater -- decHpByWater is
    /// NOTHING for a Boo (water never takes its hit point).</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (_sprayCooldown != 0) return true;
        _sprayCooldown = 1;
        if (!ChangeByJuice()) BehaveToWater();
        return true;
    }
    /// <summary>TTelesa::behaveToWater: unk165; not frozen -> knocked away from Mario (normalize(away, y 2) x 5, +2 u, airborne), fading in unless
    /// visible, FREEZE pushed, HIT_CATEGORY_ENEMY on -- now his jump finds it.</summary>
    protected override void BehaveToWater()
    {
        _sprayedSince = true;
        if (IsNerve(NerveTFreeze) || IsNerve(NerveChange)) return;
        float up = _attacker ? 30f : 2f;                        // an attacker is knocked high and far: y 30, PowerByWater 40 (a Boo: y 2, 5)
        Vector3 d = new Vector3(Pos().X - MarioPos().X, up, Pos().Z - MarioPos().Z);
        SetY(Pos().Y + 2f + (_attacker ? 30f : 0f));
        if (d.LengthSquared() < 1e-6f) d = new Vector3(0f, up, 1f);
        _vel = d.Normalized() * (_attacker ? Prm.TelesaPowerByWater : 5f) * SpeedScale;
        _airborne = true;
        if (_fadeState != FadeState.Visible && _fadeState != FadeState.FadeIn) _fadeState = FadeState.FadeIn;
        PushNerve(NerveTFreeze);
        _tangible = true;
        if (HitSound != null && HitSound.Stream != null) HitSound.Play();
    }
    /// <summary>TBoxTelesa + TTelesa::changeByJuice: the box Boo (unk150 0x40) turns into its TelesaBlock -- the block at the body with its yaw, the
    /// rate 0.1, the Boo hidden and without collision, CHANGE pushed with FREEZE after it, the FIX sound.</summary>
    private bool ChangeByJuice()
    {
        if ((_flags150 & 0x40) == 0) return false;
        if (_block != null && IsInstanceValid(_block)) return true;
        if (BlockScene == null) return false;
        _block = BlockScene.Instantiate<SmsTelesaBlock>();
        _block.ScaleXZ = 0.1f; _block.ScaleY = 0.1f; _block.YawDeg = _yaw;
        GetParent().AddChild(_block);
        _block.GlobalPosition = GlobalPosition;
        _block.Apply();
        SetCollisionLayerValue(1, false);
        if (_model != null) _model.Visible = false;
        PushNerve(NerveChange);
        PushAfterCurrent(NerveTFreeze);
        if (FixSound != null && FixSound.Stream != null) FixSound.Play();
        return true;
    }
    /// <summary>TTelesa::changeOut: NO_COLLISION (until the next setWalkAnm), the RECOVER sound, shown again at the block, the puff, the block killed.</summary>
    private void ChangeOut()
    {
        SetCollisionLayerValue(1, false);
        if (RecoverSound != null && RecoverSound.Stream != null) RecoverSound.Play();
        if (_model != null) _model.Visible = true;
        if (_block != null && IsInstanceValid(_block)) { GlobalPosition = _block.GlobalPosition; _block.QueueFree(); }
        _block = null;
        SpawnBurst(ChangeFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
        SetBckPaused(false);
    }
    /// <summary>TTelesa::resetBaseGround: unk165 read and cleared (sprayed since the last look).</summary>
    private bool ResetBaseGround() { bool r = _sprayedSince; _sprayedSince = false; return r; }
    /// <summary>TTelesa::isFlying: off the ground by its fly height, its bob or the airborne flag; landed, the base height is where it is.</summary>
    private bool IsFlying()
    {
        if (_flyCur != 0f || _bob != 0f || _airborne) return true;
        _dampGround = Pos().Y;
        return false;
    }
    /// <summary>TTelesa::reduceFlyForce: the fly height -4 a tick to 0, the bob toward 0 by 4, the phase and the tilt x 0.9 -- a stunned one sinks.</summary>
    private void ReduceFlyForce()
    {
        _flyCur = _flyCur > 0f ? MathF.Max(0f, _flyCur - 4f) : 0f;
        if (_bob > 0f) _bob = MathF.Max(0f, _bob - 4f);
        else if (_bob < 0f) _bob = MathF.Min(0f, _bob + 4f);
        _bobPhase *= 0.9f;
        _rotX *= 0.9f;
    }

    /// <summary>TTelesa::walkBehavior: TWalkerEnemy's walk (x SpeedScale), the fly height +1 a tick to its target; the base height stays the placed
    /// Y (the game's damping speed is (ground - ground) / 120 = 0 for a plain Boo); position.y = base + fly + bob.</summary>
    protected override void WalkBehavior(int mode, float k)
    {
        base.WalkBehavior(mode, k * SpeedScale);
        if (!IsNerve(NerveDie) && _flyCur < _flyTarget) _flyCur += 1f;
        if (_attacker) DampToAttackBase();
        SetY(_dampGround + _flyCur + _bob);
    }

    /// <summary>TTelesa::setBehavior (the bob and the tilt while drifting / waiting) + bind (the base point under TelesaGravityY, landed at the ground
    /// + 1.05) + moveObject's changeTevKColor (the fade) + calcRootMatrix's drool.</summary>
    protected override void SetBehavior()
    {
        if (_dead) return;
        if (IsNerve(NerveDie)) ReduceFlyForce();
        Bind();                                                  // bind first: the base point is read with the bob the walk placed the body with (the game's order: control, bind, then setBehavior's new bob)
        if (IsNerve(NerveGraphWander) || IsNerve(NerveWait) || IsNerve(NerveAttackMario))
        {
            _bobPhase += 1f;
            if (_bobPhase > _bobFreq) _bobPhase = 0f;
            float ph = Mathf.DegToRad(_bobPhase * 360f / _bobFreq);
            _bob = _bobAmp * MathF.Sin(ph);
            float nx = 10f - FlyAngMax * MathF.Cos(ph);
            if (MathF.Abs(nx - _rotX) <= 5f) _rotX = nx;      // (the game's `mRotation.x == -newRotX` typo: a jump over 5 deg is skipped)
            else _rotX = Lerp(_rotX, nx, 0.2f);                // DEVIATION: eased instead of stuck (the typo would freeze the tilt for good after a stun)
        }
        FadeTick();
        if (DroolFxScene != null && _fadeA > 0.3f && (_tickCount & 31) == 0 && IsVisibleNow)
            SpawnBurst(DroolFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale * _fadeA), (Pos() + PolarXZ(_yaw, 60f * _bodyScale) + Vector3.Up * 40f * _bodyScale) / U));
    }
    /// <summary>TTelesa::bind: the base point (y - fly - bob) carries the velocity; gravity (the terminal -40); the ground + 1; landed at + 0.05 -> not
    /// airborne, the velocity 0 (dying: set on the ground); else airborne. The velocity's XZ goes into the step, its Y into the height.</summary>
    private void Bind()
    {
        float baseY = Pos().Y - _flyCur - _bob;
        Vector3 vel = _vel;
        _lin += new Vector3(vel.X, 0f, vel.Z);
        baseY += vel.Y;
        _vel.Y -= GravityY();
        if (_vel.Y < -40f) _vel.Y = -40f;
        float floorPlus = GroundYBelow() + 1f;
        float groundPlus = IsNerve(NerveDie) ? floorPlus : floorPlus + BaseOffsetUnits;   // the base's floor (a dying one drops to the real floor)
        if (baseY <= groundPlus + 0.05f)
        {
            if (_airborne && DebugPrint) GD.Print($"[SMS telesa] {Name}: LANDED base {baseY:F0} on {GroundHitName()} at {floorPlus - 1f:F0} (fly {_flyCur:F0} bob {_bob:F0} y {Pos().Y:F0})");
            _airborne = false; _vel = Vector3.Zero;
            if (IsNerve(NerveDie) || baseY < groundPlus) baseY = groundPlus;
        }
        else _airborne = true;
        SetY(baseY + _flyCur + _bob);
    }
    /// <summary>For the debug print: what the ground ray hit.</summary>
    private string GroundHitName()
    {
        var space = GetWorld3D().DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * (P.HeadHeight / U), GlobalPosition + Vector3.Down * 60f, CollisionMask);
        var excl = new Godot.Collections.Array<Rid> { GetRid() };
        if (_mario != null && IsInstanceValid(_mario)) excl.Add(_mario.GetRid());
        q.Exclude = excl;
        var hit = space.IntersectRay(q);
        return hit.Count > 0 && hit["collider"].AsGodotObject() is Node n ? $"{n.Name} ({n.GetType().Name}) y {((Vector3)hit["position"]).Y * U:F0}" : "nothing";
    }
    private void SetY(float yUnits) => GlobalPosition = new Vector3(GlobalPosition.X, yUnits / U, GlobalPosition.Z);

    /// <summary>TTelesa::changeTevKColor: the fade state machine (VISIBLE / INVISIBLE / FADE_IN / FADE_OUT by the kind's timers), the alpha onto the
    /// body's materials when it changes.</summary>
    private void FadeTick()
    {
        float a = _fadeA < 0f ? 0f : _fadeA;
        switch (_fadeState)
        {
            case FadeState.Visible:
                a = 1f; _fadeTimer = 0;
                if (KindNow != BooKind.CanSee && KindNow != BooKind.Box && !IsNerve(NerveTFreeze))
                {
                    _fadeLoopTimer++;
                    int appear = KindNow == BooKind.Normal ? Prm.AppearTime : Prm.LoopAppearTime;
                    if (_fadeLoopTimer > appear) { _fadeState = FadeState.FadeOut; _fadeLoopTimer = 0; }
                }
                else { _fadeTimer = Prm.FadeOutTime; _fadeLoopTimer = 0; }
                break;
            case FadeState.Invisible:
                _fadeTimer = 0; a = 0f;
                if (KindNow == BooKind.Loop && ++_fadeLoopTimer > Prm.LoopHideTime) { _fadeState = FadeState.FadeIn; _fadeLoopTimer = 0; }
                break;
            case FadeState.FadeIn:
            {
                _fadeTimer++;
                int p = _fadeTimer * 255 / Math.Max(1, Prm.FadeInTime);
                if (p >= 255) { _fadeState = FadeState.Visible; _fadeTimer = 0; a = 1f; if (AppearSound != null && AppearSound.Stream != null) AppearSound.Play(); }
                else a = p / 255f;
                break;
            }
            case FadeState.FadeOut:
            {
                _fadeTimer++;
                int p = 255 - _fadeTimer * 255 / Math.Max(1, Prm.FadeOutTime);
                if (a <= 0f) { _fadeState = FadeState.Invisible; _fadeTimer = 0; a = 0f; if (DisappearSound != null && DisappearSound.Stream != null) DisappearSound.Play(); }
                else a = Mathf.Clamp(p, 0, 255) / 255f;
                break;
            }
        }
        if (MathF.Abs(a - _fadeA) > 0.0005f || _fadeA < 0f)
        {
            _fadeA = a;
            foreach (var m in _mats) m.SetShaderParameter("fade", a);
        }
    }

    /// <summary>calcRootMatrix: the body at its point, the yaw, the bob's tilt about its X, the body scale.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Basis b = new Basis(Vector3.Up, Mathf.DegToRad(_yaw)) * new Basis(Vector3.Right, Mathf.DegToRad(_rotX)) * Basis.FromScale(LookScale * _modelScale);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, GlobalPosition);
        else _model.Transform = new Transform3D(b, Vector3.Zero);
    }

    // ------------------------------------------------------------------------------------------------------------------ the species' nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveAttackMario:                               // TNerveTelesaAttackMario (King Boo's attacker, pass 153)
                if (_time == 0) { SetBck(BckWait); SetFirstAttackPoint(); }
                if (_time == 10 && _model != null) _model.Visible = true;
                if (IsReachedToGoal()) SetAttackPoint();
                WalkBehavior(2, 2f);
                return false;

            case NerveDie when _attacker:                        // TNerveTelesaDie for an attacker: the TLS_CHANGE puff, no clip, dead at once, no drops
                SetCollisionLayerValue(1, false);
                if (_model != null) _model.Visible = true;
                SpawnBurst(ChangeFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
                _noReward = true;
                Dead();
                return true;

            case NerveTFreeze:                                   // TNerveTelesaFreeze
                if (_time == 0) { SetBck(BckHitStart); SetGoalMario(); }
                else if (BckEnded())
                {
                    if (IsBck(BckHitEnd))
                    {
                        if (!IsFlying()) { _tangible = false; return true; }
                    }
                    else if (ResetBaseGround() || IsBck(BckHitStart)) SetBck(BckHit);
                    else SetBck(BckHitEnd);
                }
                ReduceFlyForce();
                WalkToCurPathNode(0f, _turnSpeed * 0.2f, 0f);
                return false;

            case NerveTrample:                                   // TNerveTelesaTrample (the box Boo's flinch)
                if (_time == 0) { SetBck(BckClush); if (CrushSound != null && CrushSound.Stream != null) CrushSound.Play(); }
                else if (BckEnded()) return true;
                return false;

            case NerveChange:                                    // TNerveSmallEnemyChange with the Boo's scalingChangeActor / changeMove / changeOut
                if (_block == null || !IsInstanceValid(_block)) { ChangeOut(); return true; }
                if (_time == 0) { SetBckPaused(true); SpawnBurst(ChangeFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition)); }
                _block.ScaleXZ = Mathf.Clamp(_block.ScaleXZ + 0.02f, 0f, BlockXZScale);
                _block.ScaleY = Mathf.Clamp(_block.ScaleY + 0.01f, 0f, BlockYScale);
                {
                    float spin = BlockWaitTime * 0.2f;
                    if (_time <= spin) _block.YawDeg = _yaw + _time * 1080f / spin;                    // changeMove's three turns (the y bob is x unk188 = 0)
                    else if (_time > BlockWaitTime && _time > ChangeBlockTime - 200) _block.Visible = (_time % 20) >= 10;   // the blink through the last 200
                }
                _block.Apply();
                if (_time > ChangeBlockTime) { ChangeOut(); return true; }
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
