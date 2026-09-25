using System;
using Godot;

// ============================================================================
//  Super Mario Sunshine player physics — ported from the doldecomp/sms decomp
//  (src/Player/MarioMove.cpp, MarioJump.cpp, MarioRun.cpp, MarioPhysics.cpp)
//  with the SHIPPED tuning values decoded from the disc's params.szs
//  (/mario/jump.prm, run.prm, mario.prm, controller.prm, slipparamnormal.prm).
//  Those .prm values override the PARAM_INIT defaults compiled into the game,
//  and differ a lot from them (gravity 0.8 -> 1.0, air steer 0.5 -> 0.008 ...).
//
//  UNITS. The game keeps velocity in "SMS units". TMarDirector::direct runs
//  FOUR simulation ticks per 30 fps frame (dt = 600/30 = 20, tick = 5 -> 120 Hz)
//  and every tick moves Mario by vel/4 (jumpProcess: next = pos + 0.25*vel).
//  Hence:   speed [units/s]        = v * 30
//           accel [units/s^2]      = (delta-v per tick) * 3600
//  Mario's collision height is 160 units (roof check "pos.y + 160").
//  ma_mdl1.glb is placed in Player.tscn at scale 0.0134334 => 1 m = 74.44 units.
//
//  Angles: SMS uses s16 BAM (0x8000 = 180 deg). We use radians and convert the
//  angular-rate constants (BAM per tick) once.
// ============================================================================

public static class SmsUnits
{
    /// <summary>Armature scale of ma_mdl1.glb in Player.tscn — fixes the unit/metre ratio.</summary>
    public const float ArmatureScale = 0.0134334f;
    public const float UnitsPerMetre = 1f / ArmatureScale; // 74.44

    public const float TicksPerSecond = 120f;
    public const float TickDt = 1f / TicksPerSecond;
    public const float MarioHeightUnits = 160f;

    /// <summary>SMS velocity value -> metres/second.</summary>
    public static float SpeedToMps(float v) => v * 30f / UnitsPerMetre;

    /// <summary>metres/second -> SMS velocity value.</summary>
    public static float MpsToSpeed(float mps) => mps * UnitsPerMetre / 30f;

    /// <summary>SMS per-tick delta-v -> metres/second^2.</summary>
    public static float AccelToMps2(float dvPerTick) => dvPerTick * 3600f / UnitsPerMetre;

    public static float UnitsToMetres(float u) => u / UnitsPerMetre;
    public static float MetresToUnits(float m) => m * UnitsPerMetre;

    public const float BamToRad = MathF.PI / 32768f;

    /// <summary>
    /// How many 120 Hz sub-ticks to run for a Godot physics frame of <paramref name="delta"/>
    /// seconds, carrying the remainder in <paramref name="accum"/>. At 60 Hz this is exactly 2.
    /// </summary>
    public static int TicksFor(double delta, ref float accum)
    {
        accum += (float)(delta * TicksPerSecond);
        int n = (int)MathF.Floor(accum + 1e-4f);
        if (n < 1) n = 1; // never skip a frame entirely
        accum -= n;
        if (accum < 0f) accum = 0f;
        return n;
    }
}

/// <summary>Shipped tuning values (params.szs). Code defaults noted where they differ.</summary>
public static class SmsParams
{
    public static class Jump // /mario/jump.prm
    {
        public const float Gravity = 1.0f;              // default 0.8
        public const float SpinJumpGravity = 0.35f;     // default 0.6  (only while falling in a spin jump)
        public const float JumpingMax = 80f;            // hard cap on horizontal air speed
        public const float JumpSpeedBrake = 0.999f;     // fwd *= this every tick in the air
        public const float JumpAccelControl = 0.008f;   // default 0.5  (!)
        public const float JumpSlideControl = 0.3f;     // default 0.5
        public const float TurnJumpForce = 70f;         // default 62   (sideflip / U-turn jump)
        public const float BackJumpForce = -18f;        // default -16  (backflip horizontal, backwards)
        public const float BackJumpForceY = 70f;        // default 62
        public const float RotateJumpForceY = 70f;      // default 62   (spin jump)
        public const float HipAttackSpeedY = -80f;      // default -50  (ground pound fall speed)
        public const float SuperHipAttackSpeedY = -80f;
        public const int SuperHipAttackCt = 80;         // ticks of falling before "super" hip drop
        public const float SecJumpEnableSp = 0f;        // default 20   (min fwd for double jump)
        public const float SecJumpForce = 52f;
        public const float SecJumpSpeedMult = 0.25f;    // default 0    (vy = fwd*mult + force)
        public const float SecJumpXZMult = 0.8f;
        public const float TriJumpEnableSp = 25f;       // default 20   (min fwd for triple jump)
        public const float UltraJumpForce = 75f;        // default 70
        public const float UltraJumpSpeedMult = 0.15f;  // default 0.25
        public const float UltraJumpXZMult = 1.0f;      // default 0.8
        public const float RotBroadJumpForce = 55f;     // default 60   (rollout out of a belly slide)
        public const float RotBroadJumpForceY = 38f;    // default 20
        public const float RotBroadEnableV = 15f;
        public const float JumpJumpCatchSp = 80f;       // fwd set when diving from the air (then capped to 48)
        public const float FenceSpeed = 1.3f;
        public const float ValleyDepth = 500f;
        public const float ClashAngle = 12000f;         // BAM
        public const int JumpCatchRotXSp = 150;         // BAM per tick, dive body pitch rate
        public const int JumpCatchRotXMax = 10000;      // BAM, dive body pitch cap (~55 deg)
    }

    public static class Run // /mario/run.prm
    {
        public const float MaxSpeed = 32f;
        public const float VelMinusBrake = 1.1f;        // fwd <= 0: fwd += this
        public const float AddBase = 1.1f;              // fwd += AddBase - fwd*AddVelDiv  (SM64's 1.1 - v/43)
        public const float AddVelDiv = 0.0232558f;      // 1/43
        public const float DecStartNrmY = 0.95f;        // only brake to target on floors this flat
        public const float DecBrake = 5f;               // default 1.0
        public const float Soft2Walk = 13f;             // default 8   (anim: soft step -> walk above this speed)
        public const float Walk2Soft = 8f;              // default 5   (anim: walk -> soft step below this speed)
        public const float RunAnmSpeedBase = 1.0f;
        public const float RunAnmSpeedMult = 0.055f;    // run anim rate = base + fwd*mult
        public const float MotBlendWalkSp = 1.5f;
        public const float MotBlendRunSp = 2.5f;
        public const float DoJumpCatchSp = 15f;         // min fwd to dive from a run
        public const float TurnNeedSp = 10f;            // min fwd for the skid U-turn
        public const int DashRotSp = 60;                // BAM per tick
    }

    public static class Mario // /mario/mario.prm  (TDeParams)
    {
        public const int WaitingRotSp = 220;            // BAM/tick, turning in place
        public const int RunningRotSpMin = 1500;        // BAM/tick (shipped min == max, so turning is speed independent)
        public const int RunningRotSpMax = 1500;
        public const float ClashSpeed = 40f;            // run into a wall faster than this -> bonk (SHORT_BACK_DOWN)
        public const float DamageFallHeight = 1500f;    // fall further than this at vy <= -70 -> hard landing
        public const float FeelDeep = 800f;
        public const float TramplePowStep1 = 35f;       // stomp bounce vy (1st), + 0.25*fwd
        public const float TramplePowStep2 = 45f;
        public const float TramplePowStep3 = 70f;
        public const float JumpWallHeight = 500f;
        public const float SlipStart = 0.4f;            // floor normal.y below which you slip
        public const float ForceSlipAngle = 0.4f;
        public const float HoldRadius = 80f;
        public const float DamageRadius = 42f;
        public const float DamageHeight = 130f;
        public const float AttackHeight = 50f;
        public const float TrampleRadius = 100f;
        public const float HipdropRadius = 100f;
        public const int InvincibleTime = 300;          // ticks
        public const int RecoverTimer = 60;
    }

    public static class SlipNormal // /mario/slipparamnormal.prm
    {
        public const float SlipFriction = 0.97f;        // default 0.9   (belly slide: fwd *= friction per tick)
        public const float SlideStopNormal = 1f;        // default 15
        public const float SlideStopCatch = 1f;         // default 15    (belly slide stops below this speed)
        public const int SlideAngleYSp = 100;           // BAM
        public const float StickSlideMult = 0.005f;
    }

    public static class Controller // /mario/controller.prm
    {
        public const int StickRotateTime = 40;          // ticks of yaw history for spin-jump detection
        public const int LengthMultTimes = 2;
        public const float LengthMult = 0.78f;          // stick length *= 0.78^2 = 0.6084
    }

    public static class Hanging // /mario/hanging.prm + the constants in hangonCheck / hanging / checkDescent
    {
        public const float MoveSp = 0.06f;              // shimmy: units per tick per unit of stick magnitude (x32 max = 1.92/tick, 3.1 m/s)
        public const float AnmRate = 0.8f;              // ANIM_HMOV rate = units moved per frame * this
        public const int RapidTime = 2000;              // ticks (16.7 s): shimmy stops, ANIM_HANG plays at AnmRapid
        public const int LimitTime = 2400;              // ticks (20 s): he lets go (LAND_SAFE_DOWN)
        public const float AnmRapid = 8f;
        public const float DescentSp = 10f;             // checkDescent: max fwd to climb down over an edge; also its wall radius
        public const float WallMovableAngle = 0.3f;     // mDeParams.mHangWallMovableAngle: min dot of old/new wall normals while shimmying
        public const float WallLowUnits = 30f;          // the wall must be solid here (feet + 30)
        public const float WallHighUnits = 150f;        // ... and clear here (feet + 150), else it is a wall hit
        public const float TopMinUnits = 100f;          // the ledge top must be more than this above the feet
        public const float TopSearchUnits = 160f;       // the top is looked up from feet + 160 down
        public const float InsideUnits = 60f;           // ... 60 units inside the wall
        public const float HeadroomUnits = 160f;        // roof clearance above the top needed to grab, climb or vault
        public const float DropUnits = 100f;            // startHangLanding: down 100 unless the ground is nearer
        public const float AutoVaultBelowUnits = 100f;  // ground within this below (30 units behind) -> HANG_JUMPING by itself
        public const int StickDelayTicks = 40;          // mStatusTimer >= 0x28 before the stick does anything
        public const int StickClimbBam = 0x400;         // stick within 5.6 deg of the face: climb
        public const int StickDropBam = 0x71c7;         // stick beyond 160 deg from the face: let go
        public const float DescentEdgeUnits = 10f;      // checkDescent: wall search radius at feet - 10
        public const float DescentDropUnits = 160f;     // ... and the ground beyond must be more than this below
    }

    public static class Timing
    {
        public const int JumpSlipWindowTicks = 16;      // JumpSlipRecord.mMaxTimer: landing window for the jump chain (0.133 s)
        public const int HipDropHangTicks = 60;         // hipAttacking state 1 duration
        public const int HipDropLiftTicks = 40;         // lift = 0.5*(40 - t), applied *0.25
        public const int WallSlideStickTicks = 10;      // stayWall: mStatusTimer counts TWICE per pinned tick (+1 at the top, +1 in the < 20 branch), so mVel = 0 for 10 ticks (83 ms)
        public const int WallSlideTimerOffset = 10;     // after the pin the timer is ticks + 10: -10.5 on the first sliding tick
        public const int WallSlideMaxTicks = 60;        // vy = -0.5 * timer, timer capped at 60 -> -30 (-12.1 m/s) from tick 50
        public const float FallCancelMinVy = 20f;       // isFallCancel: A released && vy > 20 -> vy *= 0.75
        public const float FallCancelMult = 0.75f;
        public const float TerminalVy = -75f;
        public const float StrongLandingVy = -70f;      // vy <= this (and fall > DamageFallHeight) -> hard landing
        public const float BrakeDecel = 4f;             // FConverge(fwd, 0, 4, 4) while braking / turning / landing skid
        public const float WalkEndDecel = 1f;           // FConverge(fwd, 0, 1, 1) when the stick is released
        public const float LandSlipKeepMult = 0.98f;    // jumpSlipCommon: stick held -> fwd *= 0.98 per tick
        public const float LandSlipSkidMin = 16f;       // jumpSlipCommon: neutral & fwd >= 16 -> brake 4/tick, else stop
        public const float AirWallSlideMinFwd = 16f;    // jumpingBasic case 2: fwd > 16 -> wall slide (if wall)…
        public const float AirWallBonkMinFwd = 38f;     // … >= 38 -> bonk (JUMP_SHORT_BACK_DOWN), else LAND_SAFE_DOWN (fwd -8)
        public const float TurnAngleRad = 0x471C * SmsUnits.BamToRad;   // 100.1 deg: reversal that triggers the skid turn
        public const float TurnSkidAnimMinFwd = 18f;    // turnning: fwd >= 18 uses the skid anim, else the quick turn
        public const float SpinModelDegPerTick = 22.5f; // rotateJumping: mModelFaceAngle += 4096 BAM per tick (2700 deg/s)
        public const int JumpSlipWindowTicksUltra = 4;   // ultraJumpSlip: the triple jump's landing window is only 4 ticks
        public const float BonkBackSpeed = 16f;          // jumpDownCommon(..., -16): knocked back at 16/tick until he lands
        public const float BonkDownFriction = 0.96f;     // downingCommon: the backward slide decays 0.96 per tick while ma_sdown plays
    }
}

/// <summary>checkController: GameCube stick -> intended magnitude (0..32) with SMS's deadzone and response curve.</summary>
public static class SmsStick
{
    /// <summary>
    /// Returns the SMS intended magnitude (0..32). <paramref name="axis"/> is a -1..1 stick vector
    /// (Godot's Input.GetVector output). Curve: raw = 128*axis (truncated to s16); deadzone +-7 with 6
    /// subtracted; length *= 0.78^2; clamp 64; mag = 32 * (len/64)^2. Full magnitude at ~82% tilt.
    /// </summary>
    public static float IntendedMag(Vector2 axis)
    {
        int rawH = (int)(128f * axis.X);
        int rawV = (int)(128f * axis.Y);
        float h = 0f, v = 0f;
        if (rawH < -7) h = rawH + 6; else if (rawH > 7) h = rawH - 6;
        if (rawV < -7) v = rawV + 6; else if (rawV > 7) v = rawV - 6;
        float dist = MathF.Sqrt(h * h + v * v);
        for (int i = 0; i < SmsParams.Controller.LengthMultTimes; i++)
            dist *= SmsParams.Controller.LengthMult;
        if (dist > 64f) dist = 64f;
        float norm = dist / 64f;
        return 32f * norm * norm;
    }

    /// <summary>Intended magnitude normalised to 0..1 (mag / 32).</summary>
    public static float Strength01(Vector2 axis) => IntendedMag(axis) / 32f;
}

/// <summary>
/// Spin-jump detection (TMario::makeHistory + checkStickRotate). While the stick is off-centre SMS records
/// the intended (world) yaw every tick into a StickRotateTime (40 tick = 0.333 s) sliding window and clears
/// it the moment the stick is released; a spin is recognised when the window contains all four 90-degree
/// sectors centred on the cardinal directions. The check runs when A is pressed on the ground
/// (considerRotateJumpStart, before a normal / double / triple jump) and every tick of a single, double,
/// U-turn, wall or trample jump (considerJumpRotate: the jump becomes a spin jump, velocity untouched).
/// Godot samples once per 60 Hz physics frame, so the window is 20 samples.
/// </summary>
public sealed class SmsStickRotate
{
    public const int Window = SmsParams.Controller.StickRotateTime / 2;
    private readonly float[] _yaw = new float[Window];
    private int _count;

    /// <summary>Feed one physics frame. <paramref name="stickHeld"/> = intended magnitude &gt; 0.</summary>
    public void Push(bool stickHeld, float intendedYaw)
    {
        if (!stickHeld) { _count = 0; return; }
        if (_count >= Window)
        {
            Array.Copy(_yaw, 1, _yaw, 0, Window - 1);
            _count = Window - 1;
        }
        _yaw[_count++] = intendedYaw;
    }

    public void Clear() => _count = 0;

    /// <summary>
    /// True when the recorded yaws cover all four sectors. <paramref name="spinSign"/> is +1 when the yaw
    /// mostly increased (counter-clockwise seen from above, SMS RIGHT_ROTATE_JUMP) and -1 otherwise
    /// (clockwise, LEFT_ROTATE_JUMP); the model spins the same way the stick was spun.
    /// </summary>
    public bool Check(out int spinSign)
    {
        bool q0 = false, q1 = false, q2 = false, q3 = false;
        int inc = 0, dec = 0;
        const float q = MathF.PI * 0.25f, h = MathF.PI * 0.75f;
        for (int i = 0; i < _count - 1; i++)
        {
            float a = SmsMath.WrapAngle(_yaw[i]);
            if (a < -h || a > h) q0 = true;
            if (-h <= a && a <= -q) q1 = true;
            if (-q < a && a < q) q2 = true;
            if (q <= a && a <= h) q3 = true;
            if (SmsMath.WrapAngle(_yaw[i + 1] - _yaw[i]) > 0f) inc++; else dec++;
        }
        spinSign = inc > dec ? 1 : -1;
        return q0 && q1 && q2 && q3;
    }
}

public static class SmsMath
{
    public static float WrapAngle(float a)
    {
        a = MathF.IEEERemainder(a, MathF.PI * 2f);
        return a;
    }

    /// <summary>IConverge / FConverge: move <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="step"/>.</summary>
    public static float Converge(float current, float target, float step)
    {
        float d = target - current;
        if (MathF.Abs(d) <= step) return target;
        return current + MathF.Sign(d) * step;
    }

    /// <summary>Angular converge along the shortest arc (SMS s16 wraparound semantics).</summary>
    public static float ConvergeAngle(float current, float target, float step)
    {
        float d = WrapAngle(target - current);
        if (MathF.Abs(d) <= step) return target;
        return current + MathF.Sign(d) * step;
    }

    /// <summary>Godot yaw (rotation about +Y) of an XZ direction, matching Mario.cs's convention: forward = -Z.</summary>
    public static float YawFromDir(Vector3 dir) => MathF.Atan2(-dir.X, -dir.Z);

    public static Vector3 DirFromYaw(float yaw) => new Vector3(-MathF.Sin(yaw), 0f, -MathF.Cos(yaw));
}

/// <summary>Ground locomotion (MarioRun.cpp). All speeds in SMS units, one call = one 120 Hz tick.</summary>
public static class SmsRun
{
    /// <summary>
    /// doRunning(): accelerate toward the target speed. The decomp (NonMatching MarioRun.cpp) prints the
    /// target as max(MaxSpeed, mag); that contradicts SM64 (this code's ancestor, min()) and the shipped
    /// Soft2Walk/Walk2Soft animation thresholds only make sense if stick tilt can hold a low speed, so
    /// min() is used here: tilt controls speed, quadratic via <see cref="SmsStick.IntendedMag"/>.
    /// </summary>
    public static void AccelTick(ref float fwd, float intendedMag, float floorNormalY)
    {
        float sp = MathF.Min(SmsParams.Run.MaxSpeed, intendedMag);
        if (fwd <= 0f)
            fwd += SmsParams.Run.VelMinusBrake;
        else if (fwd <= sp)
            fwd += SmsParams.Run.AddBase - fwd * SmsParams.Run.AddVelDiv;
        else if (floorNormalY >= SmsParams.Run.DecStartNrmY)
            fwd -= SmsParams.Run.DecBrake;
        if (fwd < 0f) fwd = 0f;
    }

    /// <summary>Facing rotation rate while running, radians per tick (RunningRotSpMin..Max are equal when shipped).</summary>
    public static float TurnRateRad(float fwd)
    {
        float min = SmsParams.Mario.RunningRotSpMin, max = SmsParams.Mario.RunningRotSpMax;
        float bam = 0.03125f * (fwd * (max - min)) + min;
        return bam * SmsUnits.BamToRad;
    }

    /// <summary>Turning in place (WaitingRotSp), radians per tick.</summary>
    public static float WaitTurnRateRad() => SmsParams.Mario.WaitingRotSp * SmsUnits.BamToRad;

    /// <summary>walkEnd(): stick released -> fwd converges to 0 by 1 per tick.</summary>
    public static void WalkEndTick(ref float fwd) => fwd = SmsMath.Converge(fwd, 0f, SmsParams.Timing.WalkEndDecel);

    /// <summary>braking()/turnning(): fwd converges to 0 by 4 per tick.</summary>
    public static void BrakeTick(ref float fwd) => fwd = SmsMath.Converge(fwd, 0f, SmsParams.Timing.BrakeDecel);

    /// <summary>isRunningTurnning(): stick reversed by more than ~100 deg relative to facing.</summary>
    public static bool IsTurnReversal(float faceYaw, float intendedYaw)
        => MathF.Abs(SmsMath.WrapAngle(intendedYaw - faceYaw)) > SmsParams.Timing.TurnAngleRad;

    /// <summary>
    /// jumpSlipCommon(): horizontal handling during the 16-tick landing window.
    /// Stick held: keep running speed (fwd *= 0.98). Neutral: skid (4/tick) while fwd >= 16, else stop dead.
    /// </summary>
    public static void LandSlipTick(ref float fwd, bool stickHeld)
    {
        if (stickHeld)
        {
            fwd *= SmsParams.Timing.LandSlipKeepMult;
            if (fwd * fwd < 1f) fwd = 0f;
        }
        else if (fwd >= SmsParams.Timing.LandSlipSkidMin)
        {
            fwd = SmsMath.Converge(fwd, 0f, SmsParams.Timing.BrakeDecel);
        }
        // else: below 16 with the stick neutral jumpSlipCommon only zeroes mVel.y; he coasts until *_JUMP_END stops him.
    }

    /// <summary>doRunningAnimation thresholds: 0 = soft step, 1 = walk, 2 = run (hysteresis Walk2Soft / Soft2Walk).</summary>
    public static int GaitBand(float fwd, int currentBand)
    {
        if (currentBand <= 0)
            return fwd > SmsParams.Run.Soft2Walk ? 1 : 0;
        return fwd < SmsParams.Run.Walk2Soft ? 0 : 1;
    }
}

/// <summary>Belly slide after a dive (catching()/doSliding()).</summary>
public static class SmsSlide
{
    /// <summary>
    /// One tick of belly-slide friction. mult = SlipFriction + 0.02 * (mag/32) * cos(stick vs slide dir):
    /// holding forward stretches the slide (up to 0.99/tick), holding back shortens it. Returns true when
    /// the slide has stopped (|fwd| below SlideStopCatch).
    /// </summary>
    public static bool Tick(ref float fwd, float intendedMag, float stickVsSlideCos)
        => Tick(ref fwd, intendedMag, stickVsSlideCos, SmsSlope.Normal, false);

    /// <summary>Same, with the surface's slipparam set (friction, SlideStopCatch) and doSliding()'s never-stop rule for
    /// isUnk1 / isSlider ground.</summary>
    public static bool Tick(ref float fwd, float intendedMag, float stickVsSlideCos, SmsSlope.Params p, bool neverStops)
    {
        float cs = stickVsSlideCos;
        if (cs < 0f && fwd >= 0f)
            cs *= 0.01f * (0.5f * fwd) + 0.5f;
        float mult = 0.02f * (intendedMag * 0.03125f * cs) + p.SlipFriction;
        fwd *= mult;
        if (!neverStops && fwd * fwd < p.SlideStop * p.SlideStop)
        {
            fwd = 0f;
            return true;
        }
        return false;
    }

    /// <summary>catching(): A pressed during the slide. Fast (fwd > ClashSpeed) -> rollout jump, else just get up.</summary>
    public static bool CanRollout(float fwd) => fwd > SmsParams.Mario.ClashSpeed;
}

/// <summary>Airborne physics (MarioJump.cpp / MarioPhysics.cpp / setStatusToJumping).</summary>
public static class SmsAir
{
    public enum Launch
    {
        Jump,             // MARIO_STATUS_JUMP:            vy = 0.25*fwd + 42, fwd *= 0.8
        SecondJump,       // MARIO_STATUS_SECOND_JUMP:     vy = 0.25*fwd + 52, fwd *= 0.8
        UltraJump,        // MARIO_STATUS_ULTRA_JUMP:      vy = 0.15*fwd + 75, fwd *= 1.0   (triple; needs fwd >= 25)
        RotateJump,       // LEFT/RIGHT_ROTATE_JUMP:       vy = 0.25*fwd + 70, fwd *= 0.8   (spin jump)
        UTurnJump,        // MARIO_STATUS_U_TURN_JUMP:     vy = 70, fwd = 8 (facing the new direction) (sideflip)
        BackJump,         // MARIO_STATUS_BACK_JUMP:       vy = 70, fwd = -18 (backflip)
        WallJump,         // MARIO_STATUS_WALL_JUMP:       vy = 62, fwd = 24 (facing away from the wall)
        JumpCatchAir,     // MARIO_STATUS_JUMP_CATCH (B in air):  vy unchanged, fwd = min(15 + fwd, 48)
        JumpCatchGround,  // MARIO_STATUS_JUMP_CATCH (B on ground, fwd > 15): vy = 20, fwd = min(15 + fwd, 48)
        HipDrop,          // MARIO_STATUS_HIP_DROP:        hang, then vy = -80 constant
        RotateBroadJump,  // MARIO_STATUS_ROTATE_BROAD_JUMP: vy = 38, fwd = 55 (rollout from a belly slide)
        CatchStop,        // MARIO_STATUS_CATCH_STOP (0x8A6): A during a belly slide at fwd <= ClashSpeed: vy = 30, fwd kept, roll to the feet
        Trample1,         // stomp bounce: vy = 0.25*fwd + 35, fwd *= 0.8
        Trample2,         // vy = 0.25*fwd + 45
        Trample3,         // vy = 0.25*fwd + 70
        MissJump,         // vy = 0.25*fwd + 42 (jump attempted while slipping)
    }

    /// <summary>Applies setStatusToJumping's launch table to (fwd, vy). Both in SMS units.</summary>
    public static void ApplyLaunch(Launch kind, ref float fwd, ref float vy)
    {
        switch (kind)
        {
            case Launch.Jump:
            case Launch.MissJump:
                vy = fwd * 0.25f + 42f; fwd *= 0.8f; break;
            case Launch.SecondJump:
                vy = fwd * SmsParams.Jump.SecJumpSpeedMult + SmsParams.Jump.SecJumpForce;
                fwd *= SmsParams.Jump.SecJumpXZMult; break;
            case Launch.UltraJump:
                vy = fwd * SmsParams.Jump.UltraJumpSpeedMult + SmsParams.Jump.UltraJumpForce;
                fwd *= SmsParams.Jump.UltraJumpXZMult; break;
            case Launch.RotateJump:
                vy = fwd * 0.25f + SmsParams.Jump.RotateJumpForceY; fwd *= 0.8f; break;
            case Launch.UTurnJump:
                vy = SmsParams.Jump.TurnJumpForce; fwd = 8f; break;
            case Launch.BackJump:
                vy = SmsParams.Jump.BackJumpForceY; fwd = SmsParams.Jump.BackJumpForce; break;
            case Launch.WallJump:
                vy = 62f; fwd = 24f; break;
            case Launch.JumpCatchAir:
                // checkBackTrig(): setPlayerVelocity(JumpJumpCatchSp = 80) first, then the JUMP_CATCH case
                // does min(15 + fwd, 48) -> an air dive ALWAYS leaves at 48, whatever the speed before it.
                fwd = MathF.Min(15f + SmsParams.Jump.JumpJumpCatchSp, 48f); break;
            case Launch.JumpCatchGround:
                vy = 20f; fwd = MathF.Min(15f + fwd, 48f); break;
            case Launch.HipDrop:
                vy = 0f; fwd = 0f; break;
            case Launch.RotateBroadJump:
                vy = SmsParams.Jump.RotBroadJumpForceY; fwd = SmsParams.Jump.RotBroadJumpForce; break;
            case Launch.CatchStop:
                vy = 30f; break;  // catchStop(): fwd is kept
            case Launch.Trample1:
                vy = fwd * 0.25f + SmsParams.Mario.TramplePowStep1; fwd *= 0.8f; break;
            case Launch.Trample2:
                vy = fwd * 0.25f + SmsParams.Mario.TramplePowStep2; fwd *= 0.8f; break;
            case Launch.Trample3:
                vy = fwd * 0.25f + SmsParams.Mario.TramplePowStep3; fwd *= 0.8f; break;
        }
    }

    /// <summary>
    /// Statuses carrying MARIO_STATUS_FLAG_UNK2000000 get the "fall cancel": releasing A while vy > 20
    /// multiplies vy by 0.75 every tick (variable jump height). Triple, spin, sideflip, backflip, dive
    /// and ground pound are NOT cancellable.
    /// </summary>
    public static bool FallCancelEligible(Launch kind) =>
        kind == Launch.Jump || kind == Launch.SecondJump || kind == Launch.WallJump
        || kind == Launch.MissJump || kind == Launch.RotateBroadJump
        || kind == Launch.Trample1 || kind == Launch.Trample2 || kind == Launch.Trample3;

    public enum GravityMode { Normal, SpinJump }

    /// <summary>
    /// One 120 Hz tick of doJumping() + jumpProcess() speed cap + fallProcess().
    /// <paramref name="fwd"/> is the signed forward speed along <paramref name="faceYaw"/>; <paramref name="sideVel"/>
    /// is the transient sideways drift for THIS tick (not persisted — SMS recomputes it every tick from the stick).
    /// <paramref name="vyBeforeGravity"/> is the vertical speed the tick moves with (gravity applies after the move).
    /// </summary>
    public static void Tick(
        ref float fwd, ref float vy, out float sideVel, out float vyBeforeGravity,
        float faceYaw, float intendedYaw, float intendedMag, bool aHeld,
        bool fallCancelEligible, GravityMode gravity)
    {
        sideVel = 0f;
        fwd *= SmsParams.Jump.JumpSpeedBrake;
        if (intendedMag > 0f)
        {
            float d = SmsMath.WrapAngle(intendedYaw - faceYaw);
            fwd += intendedMag * MathF.Cos(d) * SmsParams.Jump.JumpAccelControl;
            sideVel = intendedMag * MathF.Sin(d) * SmsParams.Jump.JumpSlideControl;
        }
        if (fwd > 32f) fwd -= 0.2f;
        if (fwd < -16f) fwd += 0.4f;

        // jumpProcess: cap the composed horizontal speed at JumpingMax (80)
        float h = MathF.Sqrt(fwd * fwd + sideVel * sideVel);
        if (h > SmsParams.Jump.JumpingMax)
        {
            float k = SmsParams.Jump.JumpingMax / h;
            fwd *= k; sideVel *= k;
        }

        vyBeforeGravity = vy;

        // fallProcess
        if (fallCancelEligible && !aHeld && vy > SmsParams.Timing.FallCancelMinVy)
            vy *= SmsParams.Timing.FallCancelMult;
        else if (gravity == GravityMode.SpinJump && vy < 0f)
            vy -= SmsParams.Jump.SpinJumpGravity;
        else
            vy -= SmsParams.Jump.Gravity;

        if (vy < SmsParams.Timing.TerminalVy) vy = SmsParams.Timing.TerminalVy;
    }

    /// <summary>Plain gravity tick for states that do not take stick input (bonk, hurt, ledge fall …).</summary>
    public static void GravityOnlyTick(ref float vy, out float vyBeforeGravity)
    {
        vyBeforeGravity = vy;
        vy -= SmsParams.Jump.Gravity;
        if (vy < SmsParams.Timing.TerminalVy) vy = SmsParams.Timing.TerminalVy;
    }

    public enum WallHit { Push, SafeDown, Bonk, Slide }

    /// <summary>jumpingBasic() case 2 — what hitting a wall in the air does, by forward speed.</summary>
    public static WallHit AirWallHit(float fwd, bool wallAllowsSlide)
    {
        if (fwd > SmsParams.Timing.AirWallSlideMinFwd)
        {
            if (wallAllowsSlide) return WallHit.Slide;
            if (fwd >= SmsParams.Timing.AirWallBonkMinFwd) return WallHit.Bonk;
            return WallHit.SafeDown;
        }
        return WallHit.Push;
    }

    /// <summary>running() case 2 — running into a wall on the ground.</summary>
    public static bool GroundWallBonks(float fwd) => fwd > SmsParams.Mario.ClashSpeed;

    /// <summary>hipAttacking() state 1: tiny upward lift during the first 40 ticks of the 60-tick hang (units per tick).</summary>
    public static float HipDropLiftUnits(int tick)
    {
        if (tick >= SmsParams.Timing.HipDropLiftTicks) return 0f;
        float lift = (SmsParams.Timing.HipDropLiftTicks - tick) * 0.5f;
        return lift * 0.25f;
    }

    /// <summary>stayWall(): vertical speed while sliding down a wall, by ticks spent on it.</summary>
    public static float WallSlideVy(int ticksOnWall) => WallSlideVy(ticksOnWall, SmsParams.Timing.WallSlideStickTicks);

    /// <summary>Same, with the pin length as a parameter (Mario.SmsWallSlidePinTicks; the game's value is 10).</summary>
    public static float WallSlideVy(int ticksOnWall, int pinTicks)
    {
        // stayWall(): timer += 1 every tick and += 1 again while < 20 (with mVel = 0), so ticks 1..10 are pinned; from
        // tick 11 the timer is ticks + 10 and vy = -0.5 * min(timer, 60): -10.5 at once, -30 from tick 50.
        if (ticksOnWall <= pinTicks) return 0f;
        int timer = Math.Min(ticksOnWall + SmsParams.Timing.WallSlideTimerOffset, SmsParams.Timing.WallSlideMaxTicks);
        return -timer * 0.5f;
    }
}

/// <summary>Numeric self-check against the values the real game produces. Prints to the Godot output.</summary>
public static class SmsSelfTest
{
    public static bool Run()
    {
        bool ok = true;
        void Check(string what, float got, float want, float tol)
        {
            bool pass = MathF.Abs(got - want) <= tol;
            ok &= pass;
            GD.Print($"[SMS selftest] {(pass ? "ok  " : "FAIL")} {what}: got {got:F3} want {want:F3}");
        }

        // Standing single jump, A held: vy 42, gravity 1/tick, pos += vy/4 per tick.
        // apex after 42 ticks (0.35 s), height = 0.25 * sum_{k=0}^{41}(42-k) = 225.75 units = 1.41 Mario heights.
        {
            float fwd = 0f, vy = 0f; SmsAir.ApplyLaunch(SmsAir.Launch.Jump, ref fwd, ref vy);
            float y = 0f; int ticks = 0;
            while (vy > 0f) { SmsAir.Tick(ref fwd, ref vy, out _, out float mv, 0f, 0f, 0f, true, true, SmsAir.GravityMode.Normal); y += 0.25f * mv; ticks++; }
            Check("single jump launch vy", 42f, 42f, 0f);
            Check("single jump apex ticks", ticks, 42f, 0f);
            Check("single jump apex height (units)", y, 225.75f, 0.01f);
            Check("single jump apex height (m at 74.44 u/m)", SmsUnits.UnitsToMetres(y), 3.033f, 0.01f);
        }
        // Tap jump: release A immediately -> 42 -> 31.5 -> 23.6 -> 17.7, then normal gravity.
        {
            float fwd = 0f, vy = 0f; SmsAir.ApplyLaunch(SmsAir.Launch.Jump, ref fwd, ref vy);
            float y = 0f; int ticks = 0;
            while (vy > 0f) { SmsAir.Tick(ref fwd, ref vy, out _, out float mv, 0f, 0f, 0f, false, true, SmsAir.GravityMode.Normal); y += 0.25f * mv; ticks++; }
            Check("tap jump apex ticks", ticks, 21f, 0f);
            Check("tap jump apex height (units)", y, 65.77f, 0.05f);
        }
        // Triple jump at full run (fwd 32): vy = 0.15*32 + 75 = 79.8, fwd stays 32.
        {
            float fwd = 32f, vy = 0f; SmsAir.ApplyLaunch(SmsAir.Launch.UltraJump, ref fwd, ref vy);
            Check("triple jump vy", vy, 79.8f, 0.001f);
            Check("triple jump fwd kept", fwd, 32f, 0.001f);
        }
        // Run-up: from rest with the stick fully tilted, SM64/SMS accel reaches 32 in 48 ticks (0.40 s).
        {
            float fwd = 0f; int ticks = 0;
            while (fwd < 31.9f && ticks < 1000) { SmsRun.AccelTick(ref fwd, 32f, 1f); ticks++; }
            Check("run-up ticks to ~32", ticks, 48f, 2f);
        }
        // Unit conversions.
        Check("32 units/tick*4 -> m/s", SmsUnits.SpeedToMps(32f), 12.896f, 0.01f);
        Check("gravity 1.0 -> m/s^2", SmsUnits.AccelToMps2(1f), 48.36f, 0.05f);
        Check("stick full tilt -> mag", SmsStick.IntendedMag(new Vector2(1f, 0f)), 32f, 0.001f);
        Check("stick half tilt -> mag", SmsStick.IntendedMag(new Vector2(0.5f, 0f)), 9.73f, 0.1f);
        Check("stick 5% tilt -> mag (deadzone)", SmsStick.IntendedMag(new Vector2(0.05f, 0f)), 0f, 0.001f);
        // Spin detection: a clockwise circle in 12 samples is a spin (sign -1), a half turn is not, release clears.
        {
            var det = new SmsStickRotate();
            for (int i = 0; i < 12; i++) det.Push(true, -i * (MathF.PI / 6f));
            bool spin = det.Check(out int sign);
            Check("stick spin detected", spin ? 1f : 0f, 1f, 0f);
            Check("stick spin sign (clockwise = -1)", sign, -1f, 0f);
            det.Clear();
            for (int i = 0; i < 6; i++) det.Push(true, i * (MathF.PI / 6f));
            Check("half turn is not a spin", det.Check(out _) ? 1f : 0f, 0f, 0f);
            det.Push(false, 0f);
            Check("release clears the spin history", det.Check(out _) ? 1f : 0f, 0f, 0f);
        }
        // Air dive from a standing jump: fwd = min(15 + 80, 48) = 48, vy untouched.
        {
            float fwd = 0f, vy = 10f;
            SmsAir.ApplyLaunch(SmsAir.Launch.JumpCatchAir, ref fwd, ref vy);
            Check("air dive fwd", fwd, 48f, 0f);
            Check("air dive keeps vy", vy, 10f, 0f);
        }
        GD.Print(ok ? "[SMS selftest] ALL OK" : "[SMS selftest] FAILURES ABOVE");
        return ok;
    }
}
