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
    public const int TicksPerFrame = 4;             // the game's 30 fps frame in ticks: its per-frame counters are frame counts
    public const float MarioHeightUnits = 160f;

    /// <summary>SMS velocity value -> metres/second.</summary>
    public static float SpeedToMps(float v) => v * 30f / UnitsPerMetre;

    /// <summary>metres/second -> SMS velocity value.</summary>
    public static float MpsToSpeed(float mps) => mps * UnitsPerMetre / 30f;

    /// <summary>SMS per-tick delta-v -> metres/second^2.</summary>
    public static float AccelToMps2(float dvPerTick) => dvPerTick * 3600f / UnitsPerMetre;

    /// <summary>A particle (JPA) speed, game units a frame on the 60 Hz effect clock (SMSGetAnmFrameRate = 2 steps per 30 fps frame)
    /// -> metres/second. NOT SpeedToMps: that is the 30 fps movement convention and gives half of this.</summary>
    public static float FxSpeedToMps(float unitsPerFrame) => unitsPerFrame * 60f / UnitsPerMetre;

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
        public const float SuperHipAttackSpeedY = -80f; // jump.prm (shipped): the same -80 as the normal fall (the code default was -80 against a -50 normal)
        public const int SuperHipAttackCt = 80;           // jump.prm (shipped): ticks of the fall (hipAttacking() mStatusTimer) before it is the SUPER hip drop (code default 50)
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
        public const float BroadJumpForce = 60f;        // default 80   (the turbo's BROAD_JUMP: fwd)
        public const float BroadJumpForceY = 70f;       // default 30   (vy)
        public const float RotBroadEnableV = 15f;
        public const float JumpJumpCatchSp = 80f;       // fwd set when diving from the air (then capped to 48)
        public const float FenceSpeed = 1.3f;
        public const float FireDownForce = 50f;         // jump.prm (shipped): FIRE_DOWN's launch vy (the code default 80)
        public const float FireDownControl = 0f;        // jump.prm (shipped): the stick's steer while burning, 0.03125 x mag x this -- none ships (the code default 1.0)
        public const float FireBackVelocity = 8f;       // jump.prm (shipped): FIRE_DOWN arg 0's backward kick (the code default 1.0); the live sources use arg 1
        // DELIBERATE DEVIATION (Jaysonn, 2026-10-01 21:20 "mario should not be going the direction he's facing, he should go back the way
        // he was coming from" / "the bounce he takes should be not as much, he travels too much"): the hot floor enters FIRE_DOWN with
        // the status arg no shipped caller passes -- 0: the -FireBackVelocity kick (8 a tick back along the face, facing the fire, ~1.2 m)
        // instead of 1's kept run speed (checkGraffitoFire restores mForwardVel after damageExec, MarioMove.cpp 847-854: up to 32 a tick
        // onward, ~20 m at a sprint; a landing in the fire turns the face and sends him back). The game's value is 1; fire enemies keep it.
        public const int GraffitoFireStatusArg = 0;
        public const float ValleyDepth = 500f;
        public const float ClashAngle = 12000f;         // BAM
        public const int JumpCatchRotXSp = 150;         // BAM per tick, dive body pitch rate
        public const int JumpCatchRotXMax = 10000;      // BAM, dive body pitch cap (~55 deg)
        // DELIBERATE DEVIATION (Jaysonn, 2026-09-30 01:32 "running into a wall then jumping and if you can jump higher then the ledge
        // mario will just snap to the ledge on the ground. instead of doing the full jump"): checkGroundAtJumping lands a rising jump on
        // a floor within 30 units above the feet as soon as the next position is inside it (no mVel.y test). false = the port's arc:
        // the riser stays a wall while he rises, he clears it and lands on the way down (SmsTryStepUp's air branch). true = the game.
        public const bool LedgeLandWhileRising = false;
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
        public const float SoftStepAnmMult = 0.15f;     // SSTEP rate = max(sp, 0.1) x this
        public const float DoJumpCatchSp = 15f;         // min fwd to dive from a run
        public const float TurnNeedSp = 10f;            // min fwd for the skid U-turn
        public const int DashRotSp = 60;                // BAM per tick
    }

    /// <summary>bodyanglefree.prm / bodyanglewatergun.prm (TBodyAngleParams): considerWaist()'s chest lean, applied by
    /// MarioWaistCtrl on jnt_chest while the body animation is RUN1 / RUN2. Pitch target = Pitch x fwd (BAM), roll target =
    /// Roll x (the frame's yaw change in BAM) x fwd, each clamped to its Max and chased at Rate a tick. The water-gun set drives
    /// the pitch while the upper body pumps or holds the pump, or he carries something; the roll only while pumping.</summary>
    /// <summary>TMultiMtxEffect on Mario (MarioDraw.cpp 1402): TMtxTimeLag on jnt_chest, jnt_arm_R1, jnt_arm_L1 -- the shipped
    /// /Mario/MtxEffect14.prm, 16, 22 (accel / brake per 120 Hz tick, the limit in units). addCallBack writes them while
    /// gMarioAnimeData[anim].unk6 & 2, the upper body is idle, and mMoveTickCount >= 0x4B0.</summary>
    public static class MtxEffect
    {
        public const float ChestPosAccel = 0.1f;   public const float ChestPosBrake = 0.9f;  public const float ChestPosLimit = 5f;
        public const float ChestQuatAccel = 1.0f;  public const float ChestQuatBrake = 0.5f;
        public const float ArmPosAccel = 0.1f;     public const float ArmPosBrake = 0.8f;    public const float ArmPosLimit = 5f;
        public const float ArmQuatAccel = 0.05f;   public const float ArmQuatBrake = 0.8f;
        public const int EnableAfterTicks = 0x4B0;  // 1200 ticks = 10 s into the level
        // The water gun's own table (WaterGun.cpp 1283): entry 0 TIME_LAG on the tank root -- /Mario/WaterGun/MtxEffect0.prm ships
        // (0.05 / 0.95 / 5 / 0.1 / 0.93) but its flag 1 is never set, so the tank never lags: NOT PORTED. Entry 1 SWING_RZ on
        // jnt_G_handle: the model's jnt_G_handle is joint 7 and no MtxEffect7.prm ships (MtxEffect10 and 12 do, for an older rig:
        // acc (0, -4, 0), L 50, brake 0.7), so the shipped game runs TMtxSwingRZ's compiled-in defaults -- no gravity.
        // DELIBERATE DEVIATION (Jaysonn, 2026-09-28 00:08: "floods arms look like they are doing weird things like going upside down"):
        // the compiled-in defaults (acc 0, brake 0.9) leave the grips wherever the last move put them -- backwards after a run, forwards
        // after a U-turn -- and that is what he saw. The values below are the shipped MtxEffect10 / 12.prm, the only tuning ever written
        // for this pendulum (for an older rig whose handle joints were 10 and 12): gravity 4 units a tick^2, brake 0.7. The grips hang,
        // trail back by speed (~41 deg at a full run) and settle in a few ticks. The defaults are acc (0, 0, 0), brake 0.9.
        public static readonly Vector3 HandleAcc = new Vector3(0f, -4f, 0f);
        public const float HandleL = 50f;
        public const float HandleBrake = 0.7f;
    }

    /// <summary>MarioFootPosRCtrl / LCtrl and MarioFootDirRCtrl / LCtrl (MarioDraw.cpp 524-740), the joint callbacks on chn_foot_* and
    /// jnt_foot_*: installed every frame (addCallBack) but acting only while the status is of the WAITING type (0x1C0 bits clear:
    /// WAIT, SQUAT, the *_END recoveries -- JUMP_END, SEC_JUMP_END, LAND_END, U_TURN_JUMP_END, HIP_ATTACK_END, SLIP_END ...), not
    /// BRAKE_END, not SLEEPY / SLEEP, not on Yoshi. Pos: the foot chain is moved to ground + 10 units, clamped to +-15. Dir: the
    /// foot's frame is re-aimed so its Y runs into the ground normal, its forward projected onto the ground plane.</summary>
    public static class Foot
    {
        public const float AnkleAboveGround = 10f;  // footMovement = dist - footY + 10
        public const float MaxShift = 15f;          // clamped to +-15
    }

    /// <summary>MARIO_STATUS_SQUAT (MarioWait.cpp squating(), MarioMove.cpp canSquat()): the R click or L held with the FLUDD out.</summary>
    public static class Squat
    {
        public const float RotMidAnalog = 0.7f;      // controller.prm SquatRotMidAnalog: compiled default (the shipped file has no entry)
        public const float RotMidValue = 0.05f;      // SquatRotMidValue: the turn is 5 % of WaitingRotSp up to 70 % deflection, then to 100 %
        public const float SlideSp = 0.12f;          // run.prm PumpingSlideSp: the L sidestep, units a tick per mIntendedMag x sin(diff)
        public const float SlideAnmSp = 0.5f;        // run.prm PumpingSlideAnmSp: SWLKL / SWLKR at |side| x this
        public const int EndTicks = 38;              // ma_sqend: 19 frames at 60 fps = 0.317 s (SQUAT_STANDUP ends on its last frame)
        public const float RClickPull = 0.98f;       // the GC's R click stood in for by the pad's full pull when no button_rclick exists
    }

    /// <summary>The Y camera: CAMERA_MODE_L_NORMAL's /Camera/kindLNormal.prm (SHIPPED; params.szs -> params/camera/kindlnormal.prm) and
    /// camera_normal.prm's YButtonRotateChase. The camera ("camera 1") is on PerformList Movement: it moves on every 120 Hz
    /// simulation tick like Mario, so every per-"frame" value the camera code uses is per TICK.</summary>
    public static class LCamera
    {
        public const float Fovy = 60f;
        public const float NearClip = 10f;                  // units
        public const float Dist = 200f;                     // DistMin = DistMax: units from the look-at
        public const int XAngleMin = -12800, XAngleMax = 10000; // the pitch range (BAM): the camera 70 deg under its target .. 55 over
        public const float XRotRatioManualSpeed = 0.01f;    // of the 0..1 pitch ratio a tick at full stick: the whole range in 0.83 s
        public const int YAngleManualSpeed = 300;           // BAM a tick at full stick (XMin = XMax): 198 deg/s
        public const float AtOffsetY = 150f;                // no FLUDD: the target this far over his origin (XRotRatioAtOffsetY 0)
        public const int OffsetAngleX = 2306, OffsetAngleY = -489; // the camera 12.7 deg over the pitch, -2.7 deg on the bearing
        public const float OffsetLookatXZ = -72.0215f;      // the look-at's sideways shift from the target (the over-the-shoulder framing)
        public const float PosChaseRateXZ = 0.1f, PosChaseRateY = 0.08f; // mPosition -> the inbetween's pos, a tick
        public const float AtChaseRateXZ = 0.1f, AtChaseRateY = 0.08f;   // mTarget -> the inbetween's at
        public const int InbetLNormal = 16;                 // mSLInbetLNormal: the mode change's sweep, ticks (0.13 s)
        public const int LockTicks = 60;                    // unk282: 0.5 s after a change before Y / A / B act, or Y re-enters
        public const int YButtonRotateChase = 2;            // camera_normal.prm: the swing behind him halves its gap each tick (180 deg in 16)
        public const int LButtonRotateChase = 10;           // camera_normal.prm: the L press's swing closes a tenth of its gap a tick (180 deg in ~0.4 s)
        // onNeutralMarioKey() at both ends of the mode (changeCamModeSub_): mNeutralKeyTimer = 0x3c, counted down by considerMarioStick
        // on the pad's three updateMeaning calls a 30 fps frame -> 20 frames = 80 ticks; Mario's stick reads 0 for the first 0x28 counts
        // (53 ticks, 0.44 s) and fades back in over the rest (0.22 s)
        public const int NeutralKeyTicks = 80;
        public const int NeutralKeyDeadTicks = 53;
        // DEVIATION (Jaysonn, 2026-09-28 20:45): the game cuts the L camera in ONE frame the moment he is ROCKET / ROCKET_LANDING -- the
        // hover nozzle's hover included (rocketCheck: RocketType 1 is both nozzles): execCameraModeChangeProc_ 588, isMarioRocketing ->
        // doLButtonCameraOff_(true) -> changeCamModeSpecifyFrame_(-1, 1). He wants that exit to sweep like a Y / A / B cancel's 16 ticks
        // instead of snapping. The mode still ends, a Y is still refused while he hovers. false = the game's cut.
        public const bool RocketExitSweeps = true;
    }

    public static class Waist
    {
        public const float FreePitch = 230f;        // 7360 BAM = 40.4 deg forward at the run cap 32
        public const int FreePitchMax = 12000;      // 66 deg
        public const float FreeRoll = 0.5f;         // a 1500 BAM/tick turn at 32 asks 24000: a full-rate turn saturates the cap
        public const int FreeRollMax = 8442;        // 46 deg
        public const float FreeRate = 0.07f;
        public const float GunPitch = -50f;         // -1600 at 32, capped: 11 deg BACK while pumping / holding the pump / carrying
        public const int GunPitchMax = 2000;
        public const float GunRoll = 0.5f;
        public const int GunRollMax = 4000;         // 22 deg
        public const float GunRate = 0.05f;
    }

    public static class Mario // /mario/mario.prm  (TDeParams)
    {
        public const int WaitingRotSp = 220;            // BAM/tick, turning in place
        public const int RunningRotSpMin = 1500;        // BAM/tick (shipped min == max, so turning is speed independent)
        public const int RunningRotSpMax = 1500;
        public const float ClashSpeed = 40f;            // run into a wall faster than this -> bonk (SHORT_BACK_DOWN)
        public const float DashMax = 80f;               // the turbo dash: running() / swimPaddle() force this every tick (32.2 m/s)
        public const float DashAcc = 0.5f;              // mDashSpeed climbs this a tick to 32 while the turbo pumps
        public const float DashBrake = 0.1f;            // mDashSpeed x this a tick once the pumping stops
        public const int DashStartTime = 0;             // ticks at 32 before the flag (0: at once)
        public const int PumpingRotSpMin = 300;         // BAM/tick turning while the upper body pumps or holds the pump, at fwd 0 ...
        public const int PumpingRotSpMax = 600;         // ... and at the run cap 32 (0.03125 x fwd x (max - min) + min); defaults 0x100 / 0x200
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
        public const int GraffitoNoDmgTime = 10;        // ticks of grace on electric goop before the first shock (checkGraffitoElec)
    }

    /// <summary>TTakeActor -- holding, carrying and throwing (pass 62): MarioCollision.cpp isTakeSituation / canTake / considerTake /
    /// dropObject, MarioAction.cpp taking / putting / pitching, MarioRun.cpp isThrowStart, MarioWait.cpp canPut, MarioJump.cpp
    /// checkJumpingThrowStart / jumpingThrow, MarioMove.cpp checkThrowObject; the object's side TMapObjGeneral::thrown / put
    /// (MapObjGeneral.cpp 91-134) with the compiled-in physical tables (MapObjInit.cpp) -- see SmsTakeable.</summary>
    public static class Take
    {
        public const float ThrowPower = 2.8f;           // mario.prm mThrowPower (gpMarioThrowPower): a thrown object's vx / vz = this x its ThrowRate along his face
        public const float NormalThrowSpeedRate = 0.5f; // TMapObjGeneral::mNormalThrowSpeedRate: + this x his mVel -- 0 on a ground throw (stopCommon's waitProcess zeroed it), live in the air and on a hit
        public const int ConeBam = 0x2aaa;              // isTakeSituation: the object within +-60 degrees of mFaceAngle.y
        public const float TakeFrame = 11f;             // taking(): HIT_MESSAGE_TAKE as ANIM_RAISE passes frame 11 (60 fps clip frames)
        public const float ThrowFrame = 4f;             // checkThrowObject(): dropObject() as ANIM_THROW passes frame 4 -- on the ground and in the air
        public const float PutFrame = 20f;              // putting(): HIT_MESSAGE_PUT as ANIM_PUT passes frame 20
        public const float PitchMinFwd = 16f;           // isThrowStart(): a map object is PITCHED when mForwardVel > 16, else PUT down (canPut) -- an enemy (actor type 0x10000000) is always pitched
        public const float PutProbeUnits = 100f;        // canPut(): no wall within the object's radius 100 units ahead of him (y + 10) nor at his own spot
        public const float PutProbeUp = 10f;
        public const float PutGapUnits = 10f;           // TMapObjGeneral::put(): set down objRadius + his DamageRadius + 10 ahead along his face, at his feet
        public const float FloorLossUnits = 100f;       // mInput & 4 (MarioMove.cpp 1699): the floor more than 100 units under his feet turns TAKE / PUTTING / PITCHING into a LANDING fall
    }

    public static class Graffito // /mario/graffito.prm (shipped) -- the pollution layer's hazards
    {
        public const float FireHeight = 5f;             // the hot floor burns within this height of the floor (the code default 1000)
        public const int FireInvincibleTime = 50;       // ticks after a burn (the code default 150); dmggraffitofire.prm says 50 as well
        // the SINK goop (POLLUTION_TYPE_SINK): checkSink (MarioMove.cpp 1183)
        public const float SinkTime = 240f;             // mSinkTimer ticks to the bottom; the drown (SINK_LOSER) past it -- reachable only at 0 HP (the clamp below)
        public const int SinkDmgTime = 90;              // 1 HP every this many move ticks while sunk
        public const float SinkDmgDepth = 0.6f;         // the timer is held at SinkTime x this (144) while HP > 0
        public const float SinkHeight = 150f;           // the model is drawn this far down at the bottom (x timer / SinkTime), MarioDraw.cpp 1760
        public const float SinkMoveMin = 0.3f;          // the stick x ((Max - Min)(1 - t) + Min): flat 0.3 as shipped
        public const float SinkMoveMax = 0.3f;
        public const float SinkJumpRateMin = 0.5f;      // the jump's vy and fwd x the same form: flat 0.5 as shipped
        public const float SinkJumpRateMax = 0.5f;
        public const float SinkRecover = 0.5f;          // a jump bleeds the depth by (1 - t) x SinkTime x this
        public const int FootPrintTimerMax = 520;       // mFootPrintTimerMax, the code default 0x208 (not in any .prm): the footprint window
    }

    public static class Dirty // /mario/dirty.prm (shipped) -- the oily goop (POLLUTION_TYPE_SLIP) and the dirt on him
    {
        public const float IncRunning = 0.15f;          // mDirty a tick in RUN / OIL_RUN while on paint
        public const float IncCatching = 1.5f;          // ... in CATCH / OIL_SLIP / OIL_SLOPE
        public const float IncSlipping = 0.5f;          // ... in SLIP
        public const float DecSwimming = 0.3f;          // mDirty off a tick in water
        public const float DecWaterHit = 0.2f;          // ... the tick he leaves water
        public const float DecRotJump = 0.5f;           // ... in a spin jump
        public const float BrakeStartValSlip = 1.0f;    // mOilBrake entering / in OIL_SLIP and OIL_SLOPE: no bleed on the paint
        public const float BrakeStartValRun = 0.98f;    // mOilBrake in OIL_RUN: v = 0.98 (v + 0.01 mag) -> a 15.7 u/f run at a full stick
        public const float BrakeSlipNoPollute = 0.998f; // OIL_SLIP / OIL_SLOPE briefly off the paint
        public const int DirtyTimeSlip = 60;            // the skid outlives the paint this long (OIL_SLIP / OIL_SLOPE), then the brake is 0
        public const int DirtyTimeRun = 10;             // ... OIL_RUN
        public const float PolSizeSlip = 130f;          // the paint he spreads a tick (units) in OIL_SLIP / OIL_SLOPE -- no paint map in the port
        public const float PolSizeRun = 70f;            // ... in OIL_RUN
        public const float PolSizeJump = 250f;          // ... a jump stamp
        public const float SlopeAngle = 0.95f;          // floor normal.y <= this on paint -> OIL_SLOPE
        public const float DirtyMax = 150f;             // mDirty's cap
        public const float SlipAnmSpeed = 0.25f;        // ANIM_RUN2 at 0.5 x mag x this in OIL_RUN (up to 4.0: the legs pedal)
        public const float SlipRunSp = 0.01f;           // mVel += mag x face x this a tick in OIL_RUN
        public const float SlipCatchSp = 0.0001f;       // mForwardVel += mag x cos(face - stick) x this a tick in OIL_SLIP
        public const int SlipRotate = 256;              // the face toward the stick, BAM a tick, in OIL_RUN
        public const int SlipCatchRotate = 150;         // ... in OIL_SLIP
        public const int FogTimeYellow = 240;           // mStandingOnGraffitoTimer above this: the yellow-green fog
        public const int FogTimeRed = 360;              // the fog is the damage pink below; 1 HP as the timer passes this mark; the cycle is Yellow + Red = 600
    }

    public static class Wire // /mario/wire.prm (shipped) + TMapWire's statics (MapWire.cpp 30-38) + the literals of the WIRE execs (pass 66)
    {
        public const float RotSpeed = -24f;             // DEVIATION (game mRotSpeed -8): spray's swing impulse, bumped for a snappier/more aggressive build-up (still below the 56 that snapped straight to full speed with no spin-up feel)
        public const float RotSpeedTrgHover = 24f;       // DEVIATION (game mRotSpeedTrgHover 8): hover's swing impulse, bumped for a snappier aggressive build (24 > gravity 20, so a held pump keeps winding the loop up; still spins up over several frames rather than snapping to the cap)
        public const float RotSpeedTrgTurbo = 1400f;    // mRotSpeedTrgTurbo (default 1000)
        public const float RotSpeedTrgRocket = 1400f;   // mRotSpeedTrgRocket (default 1000)
        public const float RotSpeedMax = 2800f;         // DEVIATION (game mRotSpeedMax 1400 = 0x578): the spin cap, raised for a faster/more aggressive giant swing (the faithful 1400 felt too slow). The decomp roll-jump launch = angVel x JumpRate, so this ALSO bounds the max launch -- a faster spin means a farther Super Whirl Jump (the momentum pays off). RopeSpeedScale on the Wire node scales this live
        // B held winds the swing down (x RotBrake a frame); the settle check (|angle| < SettleAngle, |speed| < RotStop) then returns him to HANGING -- he stays on the rope (Super Whirl Jump: "hold B to stop swinging before he jumps"). Without B the loop conserves (gravity is conservative) and won't stop
        public const float SwingBlurSpeed = 150f;       // the trailing streak shows once the swing is at least this fast (low, so it shows through most of a swing)
        public const int TrailLifeFrames = 32;           // how many frames a point stays in the swing streak (the hip-drop blur's own is 10; the wire runs it longer for a longer trail)
        public const float SwingRadiusCoeff = 0.032f;   // DEVIATION (game mWireSag 0.1): the loop circle is ~90 u / 1.2 m at the raised 2800 cap (0.1 would be a huge ~280 u circle there) -- the size he liked
        public const float RotStop = 100f;              // mRotStop: the settle window; 2x it is the entry kick
        public const float RotGravity = 20f;            // mRotGravity: the restoring torque x sin(angle) (BAM a frame^2)
        public const float RotBrake = 0.98f;            // mRotBrake: R held multiplies the angular speed a frame
        public const float JumpRate = 0.09f;            // mJumpRate: roll jump power = angular speed x this
        public const float SwingRate = 0.01f;           // DEVIATION (game mSwingRate 0.005): stiffer rope spring so the jump fires sooner and bounces higher (his "jumping is slow")
        public const float JumpAccelControl = 0.01f;    // mWireJumpAccelControl: the WIRE_JUMP's air steering
        public const float JumpSlideControl = 0.3f;     // mWireJumpSlideControl
        public const float JumpMult = 3.0f;             // mWireJumpMult (default 5): vy += this x -bounceVel when the rope is moving up
        public const float JumpBase = 20f;              // the game's mWireJumpBase (dropped the deviation -- he said the inflated jump was too much; plain wire jump vy 62)
        // roll-jump launch is the decomp's jumpPower = angVel x JumpRate (above), capped by RotSpeedMax via the swing clamp -- no base/bonus
        public const float SwingBrake = 0.99f;          // mWireSwingBrake: the spring's damping a frame
        public const float SwingMax = 200f;             // mWireSwingMax: the sag's clamp (units)
        public const float StandHeight = 160f;          // getOnWirePosAngle: his root this far under the chord point (the rope's give under him)
        public const float EndMargin = 100f;            // wireMove: no closer than this to a tip (units)
        public const float WalkSpeedPerMag = 0.14f;     // DEVIATION (game 0.1): snappier rope-walk traversal (units a frame per stick count)
        public const float WalkAnimRatePerMag = 0.07f;  // DEVIATION (game 0.05): clip rate kept at the game's 2:1 move:anim ratio (0.14 : 0.07) so the stride matches the snappier walk -- ~2.2x at full stick (brisk), well under the 3.2x that blurred before
        public const float AnimBlend = 0.15f;           // cross-fade between rope clips -- shorter than 0.2 for a snappier feel while still blending (not snapping)
        public const float RunMagThreshold = 16f;       // wireWait: ROPE_RUN above this stick magnitude, ROPE_WALK below
        public const float GrabBounceSeed = 0.2f;       // receiveMessage: bounceVel = this x -vy at the grab
        public const float HandMoveSpeed = 5f;          // DEVIATION (game 3): snappier hand-over-hand (units a frame)
        public const float DropBounceVel = 5f;          // the bounce charge set by the drop / the climb
        public const float ReturnHeadroom = 160f;       // wireHanging: roof - floor must be at least this for the climb back up
        public const float WireJumpVy = 42f;            // changePlayerStatus(WIRE_JUMP): setPlayerJumpSpeed(0.25, 42) with fwd 0
        public const float RollJumpAngleFwd = 0xE000;   // WIRE_ROLL_JUMP arg 0: launch angle (BAM)
        public const float RollJumpAngleBack = 0x6000;  // arg 1
        public const float RollReleaseWindow = 0x2000;  // wireRolling: the release windows about -0x2000 / 0x6000
        public const float SettleAngle = 0x200;         // wireRolling: |angle| under this and |speed| under RotStop -> hanging again
        // TMapWire statics
        public const float MoveTimerSpeed = 0.03f;      // the released bounce's phase a frame (cos(timer x pi))
        public const float DownRateMax = 0.003f;        // its decay base
        public const float EndRate = 0.001f;            // it ends under this power
        public const float StretchRate = 2.0f;          // decay = DownRateMax x this x |hangPos - 0.5|
        public const float HeightRate = 1.3f;           // amplitude = this x his launch speed (going up)
        public const float ReleaseHeight = 100f;        // amplitude when he left going down
        public const float FootLength = 26f;            // the flat bit each side of him (units)
        public const float DrawWidth = 5f;              // the ribbon's half width
        public const float DrawHeight = 6f;             // the lower face this far under the upper
        public const float PointSpacing = 50f;          // one render point per this
    }

    public static class Pollution // TPollutionLayer / TPollutionManager / TPollutionPos constants (code; no .prm) -- the goop layer (pass 65)
    {
        public const int ThresholdNormal = 30;          // mPollutedThreshold: paint above this is "polluted" (initLayerInfo)
        public const int ThresholdFire = 128;           // ... a FIRE layer
        public const int ThresholdRegrow = 200;         // ... a UNK7 (regrowing fire) layer
        public const int PerFrameThresholdNormal = 50;  // mPerFrameChangeThreshold: paint under this fades 8 a frame (the counter pass)
        public const int PerFrameThresholdFire = 128;
        public const int PerFrameThresholdRegrow = 160; // a UNK7 layer: paint ABOVE this grows 8 a frame
        public const int PerFrameDelta = 8;             // mPerFrameChangeDelta
        public const int DecayDelta = 2;                // FLAG_DECAY: every texel loses this a frame
        public const int DepthTolerance = 2;            // unk48: a sample / a stamp must be within this many depth units (x 40 units) of the surface
        public const float DepthScale = 0.025f;         // worldToDepth: (y - offset) x this -- one depth unit = 40 units of height
        public const int EdgeAlpha = 50;                // mEdgeAlpha: the initial paint loses this per neighbouring texel off the surface
        public const int FlushTime = 200;               // mFlushTime (the erase sound's hold; the sound is not ported)
        public const float SpreadArea = 2000f;          // mSpreadArea: the probes around Mario (units)
        public const int SpreadFrequency = 3;           // mSpreadFrequency: a spread stamp every 4th movement tick
        public const float SpreadStampSize = 128f;      // spread(): pollute(..., 128)
        public const float AreaMinRate = 0.7f;          // mAreaMinRate: getPollutedPosNear's radius factor
        public const float CleanGroundScale = 10f;      // splashGround: clean(mCleanSize x 10) -- the spray's 18 -> a 180-unit stamp
        public const float CleanWallScale = 32f;        // splashWall: clean(mCleanSize x 32)
        public const float FireArea = 1600f;            // mFireArea: fire()'s probe around Mario (units)
        public const int FireEffectWaitTime = 20;       // mFireEffectWaitTime: a flame when the timer passes this
        public const float ThunderArea = 1000f;         // mThunderArea: electric()'s probe around Mario (units); its timer passes 15
        public const float GlassWallArea = 1000f;       // mGlassWallArea / mGlassWallEffectTime 120: glassWall() counts and does nothing visible
    }

    public static class HangRoof // /mario/hangroof.prm (shipped) + doRoofMovingProcess's constants
    {
        public const float AnmMult = 0.3f;              // the move clip's rate = the distance a tick x this
        public const float MoveCap = 4f;                // mForwardVel + 1 a tick, capped here (units a tick)
        public const int TurnRate = 0x800;              // BAM a tick toward the stick's yaw
        public const float HeadUnits = 160f;            // the feet hang this far under the roof; the ground-to-roof gap must exceed it
        public const float ToleranceUnits = 30f;        // hangingCheckRoof: the head may be this far off the roof - 160 plane
    }

    public static class Fence // fenceMove() / fencePunch() constants; FenceSpeed is Jump.FenceSpeed (jump.prm)
    {
        public const float FrontUnits = 25f;            // 0.5 x 50: the probe point ahead and the push into the plane a tick
        public const float FootProbeUnits = 40f;        // 0.8 x 50: fenceFootCheck's probe
        public const float PunchRadius = 250f;          // attackfencepunch.prm
        public const float PunchHeight = 200f;
        public const float KickRoofRadius = 200f;       // attackkickroof.prm
        public const float KickRoofHeight = 250f;
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
        // The pad library, ahead of checkController (pass 44). PADClamp (dolphin/pad/Padclamp.c, ClampRegion {.., minStick 15,
        // maxStick 72, xyStick 40, ..}): the raw s8 stick loses a 15-count dead zone on each axis (subtracted, not just zeroed) and
        // is clamped to the 72 / 40 octagon; JUTGamePad::CStick::update: the main stick / 54 (the C stick / 42), a length past 1
        // normalised (EStickMode1). TMarioGamePad::considerMarioStick hands that mPosX / mPosY to Mario unchanged.
        public const int PadClampMin = 15;
        public const int PadClampMax = 72;
        public const int PadClampXY = 40;
        public const float PadStickDiv = 54f;
        // A modern pad's full tilt as the GameCube raw: Dolphin's GCPadStatus::MAIN_STICK_RADIUS -- every pad reaches the game
        // through it as raw 100 at the gate (a GameCube stick's own gate reads about the same). The one mapping constant in the
        // pipeline; the dead zone is then 15 % of the tilt and the run is full at 63 %.
        public const int PadGateRaw = 100;
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
        public const int StickClimbBam = 0x400;         // stick within 5.6 deg of the face: climb (the game; its hang camera puts 'up' into the wall)
        public const float StickClimbDeg = 45f;         // the port's climb cone: no hang camera, so 'toward the wall' needs slack (deviation)
        public const int StickDropBam = 0x71c7;         // stick beyond 160 deg from the face: let go
        public const float DescentEdgeUnits = 10f;      // checkDescent: wall search radius at feet - 10
        public const float DescentDropUnits = 160f;     // ... and the ground beyond must be more than this below
    }

    public static class Fludd // /mario/watergun.prm, upperbody.prm, the nozzle prms (shipped) + Watergun.cpp's constants
    {
        // DELIBERATE DEVIATIONS for the turbo dash on land (Jaysonn, 2026-09-28 01:02). The game: the face turns at RunningRotSpMin
        // (1500 BAM a tick, the run's own snap) while FLUDD_EMITTING, and doRunningAnimation paces ANIM_TURBO_DASH by
        // min(mIntendedMag, mForwardVel) = mDashSpeed, which caps at 32 -- the run's tempo (2.76) while he covers 2.5x the ground.
        // He wants a gentler wheel and legs that keep up: 600 BAM a tick, and the clip paced by mForwardVel (80 -> 5.4).
        public const int DashRotSp = 300;            // game 1500 (= Mario.RunningRotSpMin); 600 until 2026-09-28 01:40
        public const bool DashAnimByForwardVel = true; // game: by min(intended, fwd) = 32
        // DELIBERATE DEVIATION (Jaysonn, 2026-09-29 01:59 "he should do his foward walk when chagring turbo up", 03:40 "looks like hes
        // sqauting before using turbo"): doRunningAnimation picks SSTEP -- the tiptoe -- while min(mDashSpeed, fwd) is under Walk2Soft
        // (8), the first 26 ticks of a charge from a stand (back to the run above Soft2Walk 13); here the charge walks the run blend
        // from its first tick. true = the game.
        public const bool ChargeSoftStep = false;
        // DELIBERATE DEVIATION (Jaysonn, 2026-09-29 20:48, every charge in his log "from sprinting ... fwd 32.0": "when charging up he
        // should have to stand still for a frame or whatever is happening"): the game's mDashSpeed starts at 0 and mIntendedMag =
        // mDashSpeed brakes a sprint to a crawl in 6 ticks (DecBrake 5) before the 0.53 s climb -- the wind-up. Here the charge picks
        // up at the forward speed he already has along his face (a sprint keeps 32 until the trigger fills). false = the game.
        public const bool ChargeKeepsSpeed = true;
        public const float ChangeSpeed = 0.1f;          // the nozzle swap tween a tick (10 ticks); PARAM_INIT (not in the shipped prm)
        public const float DeployRate = 0.1f;           // unk1CEC -= a tick while pumping (housed 1 -> deployed 0)
        public const float StowRate = 0.005f;           // unk1CF4: unk1CEC += a tick when idle (deployed -> housed in 200 ticks)
        public const int StowHoldTicks = 360;           // unk1CF8: the pack stays out this long after the last pump
        public const int PumpWaitTime = 5;              // upperbody.prm: the pump pose is held this long after R is released
        public const float PumpAnmSpeed = 0.015f;       // upperbody.prm: the empty-tank pumping rate x mAnalogR (0..150): 2.25 at full R (pass 46)
        public const float PumpSqueakInFrame = 8f;      // addUpper(): MSD_SE_PO_POMPING1 as the empty pump's frame passes 8 forward ...
        public const float PumpSqueakOutFrame = 13f;    // ... POMPING2 as it passes 13 backward
        // gMarioAnimeData's upper-body column (unk2): the clip addUpper() grafts over the body clip while PUMPING / HOLDING_PUMP
        public const int UpperPump = 0x44;              // ma_pump (and the tank's wg_pump)
        public const int UpperHgPmp = 0x45;             // ma_hgpmp: hang, fence_move_down, rope_hgwat
        public const int UpperFePmp = 0x94;             // ma_fepmp: fence_wait, fence_move_l / r / up
        public const int UpperSwPmp = 0x95;             // ma_swpmp: the swim set
        public const int UpperNone = 0xC8;              // isPumpOK() false: no pump over this clip
        public const float HoverHeight = 230f;          // watergun.prm: the hover's target above the press height (S3)
        public const float PressureMax = 150f;          // mCurrentPressure = R x 150

        /// <summary>TNozzleBase::TEmitParams of one nozzle: the shipped prm over the PARAM_INIT defaults. Speeds are units a
        /// tick, sizes units, times ticks. The spray is TNozzleDeform (NozzleDeform.prm), the others TNozzleTrigger.</summary>
        public sealed class Nozzle
        {
            public string Name = "";
            public int Emitters = 1;                    // nozzleBmdData: muzzle joints (the hover has two)
            public int RocketType = 0;                  // mRocketType: 1 for the hover and the rocket (rocketCheck, canSquat, checkPumpEnable)
            public int AmountMax = 2100;                // the tank while this nozzle is on
            public int DecRate = 0;                     // water per drop
            public int DamageLoss = 250;                // water per hit x the attacker's WaterEmit
            public float SuckRate = 0.1f;               // refill = pressure (0..150) x this a tick in water
            public float Num = 1f, NumMin = 1f;         // drops a tick at full / zero pressure (fractions carry)
            public int Attack = 1, AttackMin = 1;
            public float EmitPow = 40f, EmitPowMin = 40f; // launch speed at full / zero pressure
            public float PowTremble = 1f;
            public float DirTremble = 0.01f, DirTrembleMin = 0.01f;
            public float Size = 40f, SizeTremble = 16f, SizeMin = 1f, SizeMinPressure = 0f, SizeMaxPressure = 1f;
            public float ReactionPow = 0f, ReactionPowMin = 0f, ReactionY = 0f;
            public int TriggerRate = 256;               // TNozzleBase::movement: the pressure ramp a tick (the spray's strength)
            public float TriggerDec = 100f, TriggerMax = 4500f; // TNozzleTrigger: the charge's bleed a tick / cap
            public int TriggerTime = 1;                 // ACTIVE ticks once charged
            public bool ManualFire = false;             // unk384: fires on A with R clicked, never at the cap (the burst)
            public int Type = 0;                        // the water particle type (WTP)
            public int LAngleBase = 4096, LAngleMin = -8192, LAngleMax = 8192; // calcGunAngle (BAM)
            public float LAngleChase = 0.1f;
            public float RButtonMult = 10000f;          // the squat aim: stick y x this a tick
        public int SideAngleMaxSide = 0, SideAngleMaxFront = 0, SideAngleMaxBack = 0; // rocketing(): the hover nozzles' bank, 0.03125 x mag x this (BAM)
            public float EmitPowScale = 10f;            // TNozzleDeform: the strength x this, clamped 1
        }

        /// <summary>nozzledeform.prm -- the spray. No recoil, a jet of 40 x strength, 0.8 drops a tick, size 50.</summary>
        public static readonly Nozzle Spray = new()
        {
            Name = "spray", Emitters = 1, AmountMax = 10000, DecRate = 1, DamageLoss = 250, SuckRate = 0.6f,
            Num = 0.8f, NumMin = 1f, Attack = 1, AttackMin = 1, EmitPow = 40f, EmitPowMin = 0f, PowTremble = 0f,
            DirTremble = 0.02f, DirTrembleMin = 0.8f, Size = 50f, SizeTremble = 0.03f, SizeMin = 1f, SizeMinPressure = 0f, SizeMaxPressure = 0.8f,
            ReactionPow = 0f, ReactionPowMin = 0f, ReactionY = 0f, TriggerRate = 50, TriggerDec = 100f, TriggerMax = 4500f, TriggerTime = 1,
            Type = 0, LAngleBase = 0, LAngleMin = -13000, LAngleMax = 8000, LAngleChase = 0.018f, RButtonMult = 10000f, EmitPowScale = 10f,
        };
        /// <summary>nozzledeformbomb.prm -- the spray's burst: A with R clicked -> 127 drops in one tick, 2 water each.</summary>
        public static readonly Nozzle Bomb = new()
        {
            Name = "burst", Emitters = 1, AmountMax = 10000, DecRate = 2, DamageLoss = 250, SuckRate = 0.1f,
            Num = 127f, NumMin = 127f, Attack = 1, AttackMin = 100, EmitPow = 35f, EmitPowMin = 35f, PowTremble = 0f,
            DirTremble = 0.6f, DirTrembleMin = 0.8f, Size = 80f, SizeTremble = 30f, SizeMin = 1f, SizeMinPressure = 0f, SizeMaxPressure = 1f,
            ReactionPow = 0f, ReactionPowMin = 0f, ReactionY = 0.03f, TriggerRate = 300, TriggerDec = 100f, TriggerMax = 100f, TriggerTime = 1,
            ManualFire = true, Type = 2, LAngleBase = 14738, LAngleMin = -8192, LAngleMax = 8192, LAngleChase = 0.1f,
        };
        /// <summary>nozzletrghover.prm -- fires the instant R is pressed, 360 ticks (3 s), two muzzles, no recoil lift (Mario's spring is the hover).</summary>
        public static readonly Nozzle Hover = new()
        {
            Name = "hover", Emitters = 2, RocketType = 1, AmountMax = 10000, DecRate = 1, DamageLoss = 0, SuckRate = 0.5f,
            Num = 1f, NumMin = 1f, Attack = 1, AttackMin = 1, EmitPow = 37.5f, EmitPowMin = 40f, PowTremble = 0f,
            DirTremble = 0f, DirTrembleMin = 0.01f, Size = 40f, SizeTremble = 10f, SizeMin = 40f, SizeMinPressure = 0f, SizeMaxPressure = 1f,
            ReactionPow = 0.005f, ReactionPowMin = 0f, ReactionY = 0f, TriggerRate = 0, TriggerDec = 0f, TriggerMax = 1f, TriggerTime = 360,
            Type = 15, LAngleBase = 0, LAngleMin = -8192, LAngleMax = 8192, LAngleChase = 0.5f,
            SideAngleMaxSide = 5000, SideAngleMaxFront = 0, SideAngleMaxBack = 8000,
        };
        /// <summary>nozzletrgrocket.prm -- 1.48 s of charge at full R (+150 - 105 a tick to 8000), then 5 ticks of 35-water drops and vy += 35 each (S4).</summary>
        public static readonly Nozzle Rocket = new()
        {
            Name = "rocket", Emitters = 1, RocketType = 1, AmountMax = 2100, DecRate = 35, DamageLoss = 250, SuckRate = 0.2f,
            Num = 1f, NumMin = 0f, Attack = 100, AttackMin = 0, EmitPow = 50f, EmitPowMin = 0f, PowTremble = 0f,
            DirTremble = 0f, DirTrembleMin = 0f, Size = 30f, SizeTremble = 20f, SizeMin = 0f, SizeMinPressure = 0f, SizeMaxPressure = 1f,
            ReactionPow = 0f, ReactionPowMin = 0f, ReactionY = 0.7f, TriggerRate = 0, TriggerDec = 105f, TriggerMax = 8000f, TriggerTime = 5,
            Type = 0, LAngleBase = 4096, LAngleMin = -8192, LAngleMax = 8192, LAngleChase = 0.1f,
            SideAngleMaxSide = 16384, SideAngleMaxFront = 16384, SideAngleMaxBack = 8192,
        };
        /// <summary>nozzletrgturbo.prm -- 0.75 s of charge (+150 - 100 a tick to 4500), then active while held; the dash is Mario's (S5).</summary>
        public static readonly Nozzle Turbo = new()
        {
            Name = "turbo", Emitters = 1, AmountMax = 2100, DecRate = 1, DamageLoss = 250, SuckRate = 0.1f,
            Num = 1f, NumMin = 1f, Attack = 1, AttackMin = 1, EmitPow = 40f, EmitPowMin = 40f, PowTremble = 1f,
            DirTremble = 0.01f, DirTrembleMin = 0.01f, Size = 40f, SizeTremble = 16f, SizeMin = 40f, SizeMinPressure = 0f, SizeMaxPressure = 1f,
            ReactionPow = 0.002f, ReactionPowMin = 0f, ReactionY = 0.002f, TriggerRate = 256, TriggerDec = 100f, TriggerMax = 4500f, TriggerTime = 32767,
            Type = 0, LAngleBase = 4096, LAngleMin = -8192, LAngleMax = 8192, LAngleChase = 0.1f,
        };

        /// <summary>By the port's nozzle index: spray, hover, rocket, turbo.</summary>
        public static Nozzle ForNozzle(int i) => i switch { 0 => Spray, 1 => Hover, 2 => Rocket, _ => Turbo };

        /// <summary>TWaterParticleType (WTPxx.prm): how a drop flies once it leaves the nozzle. Gravity units a tick^2, Alive ticks;
        /// Magnify / Extension are the game's draw scaling (the port's stand-in drop uses them the same way).</summary>
        public sealed class WaterType
        {
            public float Gravity = -0.4f, Alive = 255f, Magnify = 0.3f, Extension = 0.5f, CleanSize = 12f;
        }
        public static readonly System.Collections.Generic.Dictionary<int, WaterType> Water = new()
        {
            [0] = new() { Gravity = -0.4f, Alive = 255f, Magnify = 0.4f, Extension = 0.7f, CleanSize = 18f },   // WTP00_Normal: the spray
            [2] = new() { Gravity = -0.2f, Alive = 255f, Magnify = 0.3f, Extension = 0.8f, CleanSize = 24f },   // WTP02_Bomb: the burst
            [3] = new() { Gravity = -10f, Alive = 255f, Magnify = 0.3f, Extension = 0.6f, CleanSize = 25f },    // WTP03_TrgRocket
            [13] = new() { Gravity = 0f, Alive = 3f, Magnify = 0.3f, Extension = 1f, CleanSize = 10f },         // WTP13_Turbo
            [15] = new() { Gravity = -1f, Alive = 255f, Magnify = 0.3f, Extension = 0.5f, CleanSize = 20f },    // WTP15_Hover
        };
        public static WaterType WaterOf(int type) => Water.TryGetValue(type, out var w) ? w : Water[0];
    }

    public static class Swim // /mario/swim.prm (shipped) + run.prm's SwimDepth / InWaterBrake + doSwimming's constants
    {
        public const float StartSp = 8f;
        public const float MoveSp = 0.3f;              // fwd += mag * MoveSp / 32 a tick
        public const float MoveBrake = 0.97f;          // fwd *= a tick
        public const int RotSp = 300;                  // BAM a tick toward the stick (SwimmingRotSpMin == Max shipped)
        public const int PumpingRotSp = 200;           // ... while the upper body pumps or holds the pump (PumpingRotSpMin == Max shipped)
        public const float Gravity = 0.05f;            // vy -= a tick
        public const float PaddleAnmRate = 0.5f;       // swimPaddle(): ANIM_PADDLE_SWIM's rate
        public const float PaddleAnmRateSurf = 5f;     // ... with FLUDD_EMITTING (the turbo's surf)
        public const float WaitBuoyancy = 2f;          // vy += depthRatio * this a tick while waiting / starting
        public const float MoveBuoyancy = 1.8f;        // ... otherwise
        public const float UpDownBrake = 0.6f;         // vy *= a tick
        public const float CanJumpDepth = 100f;        // A with the feet within this of the surface can jump out
        public const float EndDepth = 80f;             // water shallower than this under him: he stands up (WAIT)
        public const float FloatHeight = 80f;          // the feet never rise above surface - this; also the buoyancy ramp
        public const float StartVMult = 0.3f;          // entering: fwd *= this
        public const float StartVYMult = 2f;           // entering: vy *= this
        public const float PaddleSpeedUp = 0.45f;      // SWIM_PADDLE_START: fwd += a tick
        public const float PaddleJumpUp = 4f;          // SWIM_PADDLE_START: vy += a tick
        public const float FloatUp = 8f;               // SWIM_UP: vy += a tick
        public const float WaterLevelCheckHeight = 10f;
        public const int WaitSinkTime = 150;           // SWIM_DIVE: ticks of sinking once the clip passes frame 16
        public const float WaitSinkSpeed = 8f;         // ... vy -= a tick
        public const float CanBreathDepth = 50f;       // isUnderWater(): the head joint this far under the surface
        public const float AirDec = 0.002f;            // mAir -= a tick under water (500 ticks = 4.2 s a unit; 8 units = 33 s)
        public const float AirDecDive = 0.00075f;      // ... with the Noki Bay helmet (not used)
        public const float AirInc = 0.03f;             // mAir += a tick out of the water (empty -> full in 2.5 s)
        public const int AirUnitFlashTicks = 50;       // unk55C: mInvincibilityFrames each time a whole unit of air goes
        public const float SwimDepth = 110f;           // run.prm: deeper than this under the feet = swimming, else wading
        public const float InWaterBrake = 0.95f;       // run.prm: the wading brake at full SwimDepth, a tick
        public const float InWaterAnmBrake = 0.8f;     // run.prm: the wading run-animation rate factor (times the brake factor)
        public const float BottomClearance = 35f;      // doSwimming(): the feet stay this far above the floor
        public const float DiveSinkStartFrame = 16f;   // swimDive(): checkPass(16) of the dive clip; SMS animation frames are
                                                       // 60ths of a second (BCKs at 60 fps), the port's clips are real time: /60
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
    /// PADClamp + JUTGamePad::CStick::update (pass 44): Godot's -1..1 stick vector -> the game's mMainStick.mPosX / mPosY, -1..1.
    /// raw = floor(axis * PadGateRaw) as the s8 the pad library sees (Dolphin's u8 cast floors); ClampStick(72, 40, 15): each axis
    /// at or under 15 is 0, past it 15 is taken off; the pair is squeezed into the octagon (xy 40 of max 72); then / 54 and a length
    /// past 1 is normalised. So a rest drift under 15 % reads 0 (his pad's 6 %: the pass-26 idle turn), the game's +-7 dead band of
    /// <see cref="IntendedMag"/> sits at 18 %, and the run is full at 63 % of the tilt (a diagonal: full at the octagon's corner).
    /// Godot's own deadzone on the Lstick_* actions must stay 0 (project.godot) -- this is the dead zone.
    /// </summary>
    public static Vector2 PadStick(Vector2 axis)
    {
        int x = Math.Clamp((int)MathF.Floor(axis.X * SmsParams.Controller.PadGateRaw), -128, 127);
        int y = Math.Clamp((int)MathF.Floor(axis.Y * SmsParams.Controller.PadGateRaw), -128, 127);
        ClampStick(ref x, ref y, SmsParams.Controller.PadClampMax, SmsParams.Controller.PadClampXY, SmsParams.Controller.PadClampMin);
        float px = x / SmsParams.Controller.PadStickDiv;
        float py = y / SmsParams.Controller.PadStickDiv;
        float len = MathF.Sqrt(px * px + py * py);
        if (len > 1f)
        {
            px /= len;
            py /= len;
        }
        return new Vector2(px, py);
    }

    /// <summary>The C-stick the same way (pass 53, the camera): PADClamp's sub-stick region -- Padclamp.c's ClampRegion { 15, 72, 40, 15, 59, 31 }:
    /// 59 max, 31 xy, 15 min -- then JUTGamePad::CStick::update's 42 (JUTGamePad.cpp 349; the main stick's is 54). Right and DOWN
    /// positive here (Godot's GetVector); the game's mSubStick.mPosY is up positive, so the camera negates Y.</summary>
    public static Vector2 PadSubStick(Vector2 axis)
    {
        int x = Math.Clamp((int)MathF.Floor(axis.X * SmsParams.Controller.PadGateRaw), -128, 127);
        int y = Math.Clamp((int)MathF.Floor(axis.Y * SmsParams.Controller.PadGateRaw), -128, 127);
        ClampStick(ref x, ref y, 59, 31, 15);
        float px = x / 42f;
        float py = y / 42f;
        float len = MathF.Sqrt(px * px + py * py);
        if (len > 1f)
        {
            px /= len;
            py /= len;
        }
        return new Vector2(px, py);
    }

    /// <summary>Padclamp.c ClampStick, integer for integer (the s8 casts truncate the divisions).</summary>
    private static void ClampStick(ref int px, ref int py, int max, int xy, int min)
    {
        int x = px, y = py, signX = 1, signY = 1;
        if (x < 0) { signX = -1; x = -x; }
        if (y < 0) { signY = -1; y = -y; }
        x = x <= min ? 0 : x - min;
        y = y <= min ? 0 : y - min;
        if (x == 0 && y == 0) { px = 0; py = 0; return; }
        if (xy * y <= xy * x)
        {
            int d = xy * x + y * (max - xy);
            if (xy * max < d) { x = xy * max * x / d; y = xy * max * y / d; }
        }
        else
        {
            int d = xy * y + x * (max - xy);
            if (xy * max < d) { x = xy * max * x / d; y = xy * max * y / d; }
        }
        px = signX * x;
        py = signY * y;
    }

    /// <summary>
    /// Returns the SMS intended magnitude (0..32). <paramref name="axis"/> is the game's mPosX / mPosY pair -- Godot's -1..1
    /// vector after <see cref="PadStick"/> (Mario.cs runs every stick read through it). Curve (checkController): raw = 128*axis
    /// (truncated to s16); deadzone +-7 with 6 subtracted; length *= 0.78^2; clamp 64; mag = 32 * (len/64)^2. Full magnitude at
    /// ~82% of mPos = 63% of a modern pad's tilt.
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

    /// <summary>The pad's stick as the game sees it for the direction: the same truncated s8 pair with the +-7 / 6 deadzone shift
    /// that <see cref="IntendedMag"/> uses (mIntendedYaw = matan(-mStickV, mStickH)). The yaw moves in ~0.6 deg steps at full
    /// tilt and a held thumb reads the same integers frame after frame -- Godot's float vector would feed the analog noise into
    /// the face. Falls back to the float vector inside the game's deadzone (the caller's own deadzone decides whether to move).</summary>
    public static Vector2 PadVector(Vector2 axis)
    {
        int rawH = (int)(128f * axis.X);
        int rawV = (int)(128f * axis.Y);
        float h = 0f, v = 0f;
        if (rawH < -7) h = rawH + 6; else if (rawH > 7) h = rawH - 6;
        if (rawV < -7) v = rawV + 6; else if (rawV > 7) v = rawV - 6;
        return h == 0f && v == 0f ? axis : new Vector2(h, v);
    }
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

    /// <summary>CLBChaseAngleDecrease (cameralib.cpp 61): an s16 angle closes 1/ratio of its (wrapped) gap a call, in integer
    /// arithmetic; false once it stops moving or has arrived. ratio 0 = arrive at once.</summary>
    public static bool ChaseAngleDecreaseS16(ref int value, int desired, int ratio)
    {
        short v = (short)value, d = (short)desired;
        if (ratio == 0)
            v = d;
        else
        {
            short nv = (short)(v - d);
            nv = (short)(nv - nv / ratio);
            nv = (short)(nv + d);
            if (nv == v)
            {
                value = v;
                return false;
            }
            v = nv;
        }
        value = v;
        return v != d;
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
            fwd = MathF.Min(fwd + SmsParams.Run.AddBase - fwd * SmsParams.Run.AddVelDiv, sp); // settle AT the target
        else if (floorNormalY >= SmsParams.Run.DecStartNrmY)
            fwd = MathF.Max(fwd - SmsParams.Run.DecBrake, sp); // brake down to it, never past it
        // Deviation from the decomp's letter: taken literally, the +0.36 accelerate step at 32 overshoots the target and the
        // shipped DecBrake of 5 yanks it back in one tick -- a 27..32 saw-tooth every ~12 ticks (10.9..12.9 m/s at ~9 Hz),
        // measured in the port (b856f6a's `tick in -> out` log fields). SM64's identical structure brakes 1 (invisible);
        // MarioRun.cpp is NonMatching, and the game's run is steady at top speed (Jaysonn), so the port settles at sp.
        if (fwd < 0f) fwd = 0f;
    }

    /// <summary>Facing rotation rate while running, radians per tick (RunningRotSpMin..Max are equal when shipped).</summary>
    public static float TurnRateRad(float fwd)
    {
        float min = SmsParams.Mario.RunningRotSpMin, max = SmsParams.Mario.RunningRotSpMax;
        float bam = 0.03125f * (fwd * (max - min)) + min;
        return bam * SmsUnits.BamToRad;
    }

    /// <summary>doRunning() while isUpperPumpingStyle(): PumpingRotSpMin..Max by speed (300 standing, 600 at 32), radians per tick.</summary>
    public static float PumpingTurnRateRad(float fwd)
    {
        float min = SmsParams.Mario.PumpingRotSpMin, max = SmsParams.Mario.PumpingRotSpMax;
        float bam = 0.03125f * (fwd * (max - min)) + min;
        return bam * SmsUnits.BamToRad;
    }

    /// <summary>doRunning() while FLUDD_EMITTING: the game turns the dash at RunningRotSpMin, 1500 BAM a tick -- the free run's own
    /// snap (MarioRun.cpp 510; run.prm's DashRotSp 60 is never read). DELIBERATE DEVIATION (Jaysonn, 2026-09-28 01:02 "the steering
    /// is still too sharp", then 01:40 at 600 "harder to steer ... bigger wider turns"): SmsParams.Fludd.DashRotSp, 300 BAM a tick -- a
    /// 90 degree turn in 0.45 s at 32 m/s, about a 9 m radius, against the game's 0.09 s / 2 m. Put RunningRotSpMin back to get the
    /// code's rate.</summary>
    public static float DashTurnRateRad() => SmsParams.Fludd.DashRotSp * SmsUnits.BamToRad;

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
        BroadJump,        // MARIO_STATUS_BROAD_JUMP: vy = 70, fwd = 60 (A during the turbo dash; from the surf too); no fall cancel (no 0x2000000 flag)
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
            case Launch.BroadJump:
                vy = SmsParams.Jump.BroadJumpForceY; fwd = SmsParams.Jump.BroadJumpForce; break;
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
        bool fallCancelEligible, GravityMode gravity) =>
        Tick(ref fwd, ref vy, out sideVel, out vyBeforeGravity, faceYaw, intendedYaw, intendedMag, aHeld, fallCancelEligible, gravity, SmsParams.Jump.JumpAccelControl);

    /// <summary>The same with getJumpAccelControl(): mWireJumpAccelControl (0.01) in WIRE_JUMP, mJumpAccelControl otherwise (MarioMove.cpp 26-30).</summary>
    public static void Tick(
        ref float fwd, ref float vy, out float sideVel, out float vyBeforeGravity,
        float faceYaw, float intendedYaw, float intendedMag, bool aHeld,
        bool fallCancelEligible, GravityMode gravity, float accelControl)
    {
        sideVel = 0f;
        fwd *= SmsParams.Jump.JumpSpeedBrake;
        if (intendedMag > 0f)
        {
            float d = SmsMath.WrapAngle(intendedYaw - faceYaw);
            fwd += intendedMag * MathF.Cos(d) * accelControl;
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


/// <summary>The hover (rocketing() with the hover nozzle, MarioJump.cpp; /mario/hhover.prm shipped; watergun.prm's HoverHeight).
/// A spring to the press height plus HoverHeight: vy = (target - y) x AccelRate is ASSIGNED every tick and the move is 0.25 x vy,
/// so the gap closes 0.5 % a tick (45 % a second; 192 of the 230 units in the 3 s window; 1.85 m/s at the start). Gravity never
/// shows: fallProcess subtracts it after the move and the next tick's assignment overwrites it.</summary>
public static class SmsHover
{
    public const int RotSp = 200;              // hhover.prm: BAM a tick the face turns toward a sideways stick (min == max)
    public const float AccelRate = 0.02f;      // hhover.prm: vy = (target - y) x this
    public const float Brake = 0.985f;         // hhover.prm: fwd x this a tick
    public const float AccelControl = 0.01f;   // /Mario/Diving.prm SHIPPED mAccelControl 0.01 (the code default is 0.02, which the port
                                               // wrongly carried): fwd += mag x cos x this a tick on a fore / aft stick (rocketing(),
                                               // MarioJump.cpp 959). Held forward it settles at 32 x 0.01 / (1 - Brake) = 21.3 units.
    public const float EntryVy = 10f;          // setStatusToJumping(ROCKET): mVel.y = 10, overwritten by the spring before the first move
    public const int FrontBandBam = 0x1555;    // a stick within 30 deg of the face pushes (no turn)
    public const int BackBandBam = 0x6AAA;     // beyond 150 deg pulls back (no turn); between the two: the turn (no push)
    public const int HeadAngleBam = 5294;      // upperbody.prm HoverHeadAngle: the head 29 deg down while he can spray

    /// <summary>One tick's mVel.y (units a tick) at <paramref name="yUnits"/> for the target <paramref name="targetUnits"/>.</summary>
    public static float SpringVy(float yUnits, float targetUnits) => (targetUnits - yUnits) * AccelRate;

    /// <summary>The height gained (units) after <paramref name="ticks"/> ticks of hovering from a standstill, no roof, no exit.</summary>
    public static float RiseAfter(int ticks)
    {
        float y = 0f;
        for (int i = 0; i < ticks; i++)
            y += 0.25f * SpringVy(y, SmsParams.Fludd.HoverHeight);
        return y;
    }
}

/// <summary>The rocket nozzle's launch (rocketing() with the rocket nozzle; TNozzleTrigger::emit; nozzletrgrocket.prm shipped).
/// The charge: +150 x R - 105 a tick to 8000 (1.48 s at full R; R at or under 0.7 never charges). Then 5 ACTIVE ticks, each
/// pushing mVel.y += 35 (pow 50 x ReactionY 0.7, the muzzle pointing down) into whatever status he is in; the ROCKET status is
/// entered the tick after the first push and its entry writes mVel.y = 10 over that push, so four pushes count.</summary>
public static class SmsRocketNozzle
{
    public const float Push = 50f * 0.7f;          // per ACTIVE tick: EmitPow x ReactionY
    public const int Pushes = 5;                   // TriggerTime
    public const float ChargeTicksFullR = 8000f / (150f - 105f); // 177.8 ticks = 1.48 s

    /// <summary>The game's launch: entry 10, then per tick move 0.25 x vy, gravity, a push -- for the four pushes that count -- then the
    /// dead tick, then the fall; returns the vy handed to ROCKET_LANDING and the total height above the launch point (units).</summary>
    public static float LaunchVy(out float apexUnits)
    {
        float vy = 10f, y = 0f;
        for (int i = 0; i < Pushes - 1; i++)
        {
            y += 0.25f * vy;
            vy -= SmsParams.Jump.Gravity;
            vy += Push;
        }
        y += 0.25f * vy;              // the dead tick: the move, the gravity, no push
        vy -= SmsParams.Jump.Gravity;
        float launch = vy;
        while (vy > 0f)               // landing(): the fall from there
        {
            y += 0.25f * vy;
            vy -= SmsParams.Jump.Gravity;
        }
        apexUnits = y;
        return launch;
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
        Check("fx 34.06 u/f at 60 Hz -> m/s", SmsUnits.FxSpeedToMps(34.06f), 27.45f, 0.05f);
        Check("stick full tilt -> mag", SmsStick.IntendedMag(new Vector2(1f, 0f)), 32f, 0.001f);
        Check("stick half tilt -> mag", SmsStick.IntendedMag(new Vector2(0.5f, 0f)), 9.73f, 0.1f);
        Check("stick 5% tilt -> mag (deadzone)", SmsStick.IntendedMag(new Vector2(0.05f, 0f)), 0f, 0.001f);
        // The pad library ahead of it (pass 44): PADClamp 15 / 72 / 40 then / 54 -- a 6 % rest drift is 0, 15 % is still 0, 16 % is
        // the first count (1/54), 72 % saturates, a full diagonal is the octagon's corner (40, 40) normalised; the run is full at 63 %.
        Check("pad 6% drift -> mPos", SmsStick.PadStick(new Vector2(0.06f, 0f)).Length(), 0f, 0f);
        Check("pad 15% -> mPos (dead zone edge)", SmsStick.PadStick(new Vector2(0.15f, 0f)).Length(), 0f, 0f);
        Check("pad 16% -> mPos (first count)", SmsStick.PadStick(new Vector2(0.16f, 0f)).X, 1f / 54f, 0.0005f);
        Check("pad 72% -> mPos (saturated)", SmsStick.PadStick(new Vector2(0.72f, 0f)).X, 1f, 0.0005f);
        Check("pad full diagonal -> mPos x", SmsStick.PadStick(new Vector2(1f, 1f)).X, 0.7071f, 0.0005f);
        Check("pad 6% drift -> mag", SmsStick.IntendedMag(SmsStick.PadStick(new Vector2(0.06f, 0f))), 0f, 0f);
        Check("pad 50% -> mag", SmsStick.IntendedMag(SmsStick.PadStick(new Vector2(0.5f, 0f))), 16.70f, 0.05f);
        Check("pad 63% -> mag (full run)", SmsStick.IntendedMag(SmsStick.PadStick(new Vector2(0.63f, 0f))), 32f, 0.001f);
        Check("pad full diagonal -> mag", SmsStick.IntendedMag(SmsStick.PadStick(new Vector2(1f, 1f))), 32f, 0.001f);
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
        // The hover: the first tick's spring vy (4.6), the 3 s window's rise (192 of 230 units), the half-way point (1.15 s).
        {
            Check("hover: first tick vy (units)", SmsHover.SpringVy(0f, SmsParams.Fludd.HoverHeight), 4.6f, 0.001f);
            Check("hover: rise after the 360-tick window (units)", SmsHover.RiseAfter(360), 192.1f, 0.5f);
            Check("hover: rise after 1 s (units)", SmsHover.RiseAfter(120), 104.0f, 0.5f);
            Check("hover: 2 water a tick -> seconds in a full tank", SmsParams.Fludd.Hover.AmountMax / (float)(SmsParams.Fludd.Hover.Emitters * SmsParams.Fludd.Hover.DecRate) / SmsUnits.TicksPerSecond, 41.67f, 0.05f);
            Check("hover: full-stick push a tick (units, Diving.prm 0.01)", 32f * SmsHover.AccelControl, 0.32f, 0.0001f);
            Check("hover: settled speed with the stick held (units)", 32f * SmsHover.AccelControl / (1f - SmsHover.Brake), 21.33f, 0.05f);
        }
        // The rocket: 10 + 4 x 35 - 5 = 145 handed to the fall; the apex 2744 units = 36.9 m = 17 Mario heights, no cap.
        {
            float vy = SmsRocketNozzle.LaunchVy(out float apex);
            Check("rocket: vy handed to ROCKET_LANDING", vy, 145f, 0.01f);
            Check("rocket: apex above the launch point (units)", apex, 2743.75f, 0.5f);
            Check("rocket: charge ticks at full R", SmsRocketNozzle.ChargeTicksFullR, 177.8f, 0.1f);
        }
        // The turbo: 32 / 0.5 = 64 ticks of ramp, 4500 / (150 - 100) = 90 ticks of charge at full R, the broad jump's apex.
        {
            Check("turbo: dash ramp ticks to 32", 32f / SmsParams.Mario.DashAcc, 64f, 0.01f);
            Check("pumping turn at fwd 32 (BAM)", SmsRun.PumpingTurnRateRad(32f) / SmsUnits.BamToRad, 600f, 0.01f);
            Check("pumping turn at fwd 0 (BAM)", SmsRun.PumpingTurnRateRad(0f) / SmsUnits.BamToRad, 300f, 0.01f);
            Check("dash turn (BAM)", SmsRun.DashTurnRateRad() / SmsUnits.BamToRad, SmsParams.Fludd.DashRotSp, 0.01f); // 300: a logged deviation (game 1500)
            Check("turbo neck: gun angle (deg)", -SmsParams.Fludd.Turbo.LAngleBase * SmsUnits.BamToRad * 180f / MathF.PI, -22.5f, 0.01f);
            // considerWaist(): the pitch at the run cap, the roll's saturation in a full-rate turn, the pumping pitch cap
            Check("waist: free pitch at fwd 32 (deg)", MathF.Min(SmsParams.Waist.FreePitch * 32f, SmsParams.Waist.FreePitchMax) * SmsUnits.BamToRad * 180f / MathF.PI, 40.43f, 0.02f);
            Check("waist: free roll, 1500 BAM/tick turn at 32 (deg)", MathF.Min(SmsParams.Waist.FreeRoll * 1500f * 32f, SmsParams.Waist.FreeRollMax) * SmsUnits.BamToRad * 180f / MathF.PI, 46.37f, 0.02f);
            Check("waist: pumping pitch at fwd 32 (deg)", MathF.Max(SmsParams.Waist.GunPitch * 32f, -SmsParams.Waist.GunPitchMax) * SmsUnits.BamToRad * 180f / MathF.PI, -8.79f, 0.02f);
            {
                float p = 0f; int ticks = 0;
                while (p < 7360f * 0.95f && ticks < 1000) { p += SmsParams.Waist.FreeRate * (7360f - p); ticks++; }
                Check("waist: ticks to 95% of the run lean", ticks, 42f, 0.5f);
            }
            Check("turbo: charge ticks at full R", SmsParams.Fludd.Turbo.TriggerMax / (150f - SmsParams.Fludd.Turbo.TriggerDec), 90f, 0.01f);
            float fwd = 80f, vy = 0f; SmsAir.ApplyLaunch(SmsAir.Launch.BroadJump, ref fwd, ref vy);
            Check("broad jump: launch fwd", fwd, 60f, 0.001f);
            Check("broad jump: launch vy", vy, 70f, 0.001f);
            float y = 0f;
            while (vy > 0f) { SmsAir.Tick(ref fwd, ref vy, out _, out float mv, 0f, 0f, 0f, true, false, SmsAir.GravityMode.Normal); y += 0.25f * mv; }
            Check("broad jump: apex (units)", y, 621.25f, 0.5f);
        }
        // The Y camera at the camera's 120 Hz: the stick's yaw rate, the pitch range's sweep, the lock, the swing behind him
        {
            Check("ycam: stick yaw at full deflection (deg/s)", SmsParams.LCamera.YAngleManualSpeed * SmsUnits.TicksPerSecond * 360f / 65536f, 197.75f, 0.05f);
            Check("ycam: pitch range end to end (s)", 1f / (SmsParams.LCamera.XRotRatioManualSpeed * SmsUnits.TicksPerSecond), 0.833f, 0.002f);
            Check("ycam: lock after a change (s)", SmsParams.LCamera.LockTicks / SmsUnits.TicksPerSecond, 0.5f, 0.001f);
            int yaw = 0, ticks = 0;
            while (SmsMath.ChaseAngleDecreaseS16(ref yaw, -32768, SmsParams.LCamera.YButtonRotateChase) && ticks < 200) ticks++;
            Check("ycam: swing behind from 180 deg (ticks)", ticks + 1, 16f, 0.5f);
            Check("ycam: swing lands within a BAM", MathF.Abs((short)(yaw - (-32768))), 1f, 1f);
        }
        GD.Print($"[SMS wire feel] walk {SmsParams.Wire.WalkSpeedPerMag} hand {SmsParams.Wire.HandMoveSpeed} jumpBase {SmsParams.Wire.JumpBase} spring {SmsParams.Wire.SwingRate} hoverSwing {SmsParams.Wire.RotSpeedTrgHover} spraySwing {SmsParams.Wire.RotSpeed} -- if these look OLD (0.1/3/25/0.005/8/-8) the editor is running STALE C#: click the editor Build (hammer) or restart it (C# does NOT hot-reload like shaders)");
        GD.Print(ok ? "[SMS selftest] ALL OK" : "[SMS selftest] FAILURES ABOVE");
        return ok;
    }
}
