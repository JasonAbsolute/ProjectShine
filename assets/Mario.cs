using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;

public partial class Mario : CharacterBody3D
{
    public enum MarioState
    {
        idle,
        pivot,
        sneak,
        singleJump,
        doubleJump,
        tripleJump,
        walking,
        running,
        sprinting,
        singleJumpLanding,
        doubleJumpLanding,
        tripleJumpLanding,
        diving,
        singleJumpDive,
        doubleJumpDive,
        tripleJumpDive,
        bellySlidingFromDive,
        singleRollout,
        gettingUpFromSliding,
        bellyRollout,
        sideFlip,
        sideFlipTurning,
        SpinJump,
        groundSpin,
        crouch,
        backFlip,
        bonk,
        landing,
        rolloutRun,
        wallSlide,
        slipping,
        wallJump,
        ledgeHang,
        ledgeClimb,
        ledgeHopUp,
        pickupFail,
        pickupRaise,
        carrying,
        putDown,
        wallPush,
        wallShuffle,
        ledgeFall,
        hurt,
        stomping,
        groundPoundStartup,
        groundPoundFalling,
        groundPoundLanding,
        treeGrab,
        treeWait,
        treeClimb,
        treeMoveL,
        treeMoveR,
        treeTopReach,
        shineGet,
        dead,
        talking,
    }

    private struct AirControlProfile
    {
        public float maxSpeed; // desired max XZ speed while airborne
        public float accel; // how fast we steer toward desired velocity
        public float brake; // extra strength when pushing opposite current velocity
        public float drag; // how fast we slow down when stick neutral
        public float reverseMax; // optional: how much you can "pull back" (0 = none)
        public float stopSnap; // snap-to-zero threshold when braking/dragging

        public AirControlProfile(
            float maxSpeed,
            float accel,
            float brake,
            float drag,
            float reverseMax,
            float stopSnap
        )
        {
            this.maxSpeed = maxSpeed;
            this.accel = accel;
            this.brake = brake;
            this.drag = drag;
            this.reverseMax = reverseMax;
            this.stopSnap = stopSnap;
        }
    }

    /// <summary>Fires the instant Mario grabs a shine (start of the collect
    /// cutscene, StartShineGet) — not after the celebration plays out. Anything
    /// that should stop the moment the goal is reached, not after watching him
    /// pose with it — e.g. a ShineTimer, speedrun or countdown alike — should
    /// hook this instead of AddShine() (which only fires later, mid-cutscene,
    /// timed for the HUD count-up flourish).</summary>
    [Signal] public delegate void ShineCollectedEventHandler();

    private bool fPrev = false;
    private float pickupFailT = 0f;
    private float pickupFailDur = 0.45f; // fallback

    // Health
    [Export]
    public int StartingHealth = 8;
    private int _health;
    private LifeMeter _lifeMeter;

    // Coins
    private int _coins;
    private YellowCoinHud _yellowCoinHud;
    private int _redCoins;
    private RedCoinHud _redCoinHud;
    private int _blueCoins;
    private BlueCoinHud _blueCoinHud;
    private int _shines;
    private ShineHud _shineHud;
    private SunshineCamera _camera;

    // Hand-authored shine-get cutscene camera (a Camera3D + AnimationPlayer
    // under a rig, keyframed in the editor). Played instead of the rail camera
    // when Mario collects a shine.
    private Node3D _shineGetCamRig;
    private Camera3D _shineGetCam;
    private AnimationPlayer _shineGetCamAP;

    /// <summary>Mario's live visual facing yaw — used by the shine to track his
    /// position/facing during the collect cutscene.</summary>
    public float CurrentFacingYaw => _camera?.CurrentVisualYaw ?? GlobalRotation.Y;

    // Lives
    [Export]
    public int StartingLives = 4;
    private int _lives;
    private LifeCounterHud _lifeCounterHud;
    private float _idleTimer = 0f;
    private bool _lifeCounterShowing = false;
    private float _blueCoinDisplayTimer = 0f;
    private const float IdleShowDelay = 3f;
    private const float BlueCoinDisplayDuration = 3f;

    // On a shine grab the HUD appears immediately but the count doesn't tick up
    // until this delay (mid-cutscene), per the reference footage.
    private const float ShineCountDelay = 2f;

    // Hurt state
    private float _hurtTimer = 0f;
    private const float HURT_DURATION = 0.5f;
    private const float HURT_KNOCKBACK = 2.0f;
    private bool _hitFromFront = false;
    private bool _hurtAirborne = false; // true if hit while not on floor

    // Stomp bounce
    private int _stompChain = 0;
    private const float STOMP_BOUNCE_VELOCITY = 24.0f;
    private const float STOMP_BOUNCE_VELOCITY_TRIPLE = 48.0f;

    // Ground pound
    private const float GROUND_POUND_FALL_SPEED = -40.0f; // fast downward velocity during fall

    [Export]
    private float groundPoundAirStallDuration = 0.25f; // stall after startup anim before falling
    private float _groundPoundStartupTimer = 0f;
    private bool _groundPoundStalling = false;
    private float _groundPoundStallTimer = 0f;
    private float _groundPoundLandingTimer = 0f;
    private bool _groundPoundSlpedPlaying = false;
    private float _groundPoundSlpedTimer = 0f;

    // Movement / jump (scaled)
    public const float RUN_SPEED = 11.550872f; // 26 * SCALE_FIX
    public const float ROLL_SPEED = 9.773815f; // 22 * SCALE_FIX

    public const float BASE_JUMP_VELOCITY = 8.885286f; // 20 * SCALE_FIX
    public const float MAX_JUMP_VELOCITY_SINGLE = 12.883665f; // 29 * SCALE_FIX
    public const float MAX_JUMP_VELOCITY_DOUBLE = 17.770573f; // 40 * SCALE_FIX

    public const float MAX_ROLLOUT_HOLD_TIME = 0.2f; // time (DO NOT scale)

    public const float BASE_ROLLOUT_VELOCITY = 7.5f; // 25 * SCALE_FIX
    public const float MAX_ROLLOUT_VELOCITY = 10.327930f; // 30 * SCALE_FIX

    public const float TRIPLE_JUMP_VELOCITY = 39.983789f; // 90 * SCALE_FIX

    // Side flip was launching at TRIPLE_JUMP_VELOCITY itself (same height as an
    // actual triple jump) — too high. Slightly less than triple jump instead.
    public const float SIDEFLIP_JUMP_VELOCITY = TRIPLE_JUMP_VELOCITY * 0.9f;
    public const float GETTING_UP_FROM_SLIDE_ROLL = 13.327930f; // 30 * SCALE_FIX
    public const float TERMINAL_VELOCITY = 13.327930f; // 30 * SCALE_FIX

    // Spin tuning (scaled where it is a speed)
    private const float SPIN_FORWARD_BONUS = 1.777057f; // 4.0 * SCALE_FIX
    private const float SPIN_TERMINAL_FALL = 10.662344f; // 24 * SCALE_FIX

    // Spin gravity multipliers are dimensionless (DO NOT scale)
    private const float SPIN_GRAVITY_UP_SCALE = 1f;
    private const float SPIN_GRAVITY_DOWN_SCALE = 0.45f;

    // Gravity itself is world accel -> scale it
    [Export]
    public Vector3 GRAVITY = new Vector3(0, -62.197005f, 0); // -140 * SCALE_FIX

    // --- WALL SLIDE / WALL KICK ---
    private const float WALL_SLIDE_MAX_FALL = -11.106608f; // -25 * SCALE_FIX
    private const float WALL_SLIDE_GRAVITY_SCALE = 0.20f; // dimensionless (DO NOT scale)
    private const float WALL_STICK_SPEED = 0.710823f; // 1.6 * SCALE_FIX

    private const float WALL_KICK_UP_VEL = 14.660723f; // 33 * SCALE_FIX
    private const float WALL_KICK_OUT_SPEED = 9.773814f; // 22 * SCALE_FIX — pulled back down, less shoot-off distance on a quick tap

    // If you still use these older ones anywhere:
    private const float WALL_MIN_APPROACH_SPEED = 1.999189f; // 4.5 * SCALE_FIX

    // Variable wall jump height (scaled)
    private const float WALL_JUMP_BASE_UP_VEL = 11.995136f; // 27 * SCALE_FIX — more pop on a quick tap
    private const float WALL_JUMP_MAX_UP_VEL = 22.213216f; // 50 * SCALE_FIX
    private const float MAX_WALL_JUMP_HOLD_TIME = 0.25f;

    // Wall jump XZ steering is split into two axes (see ApplyWallJumpAirControl) so
    // "extend/brake along the kick direction" and "redirect left/right" can be tuned
    // independently — AIR_WALLJUMP.accel (8.885) governs the former, this governs
    // the latter. Kept at half that rate as a starting point for "still steerable,
    // just less than forward."
    private const float WALL_JUMP_LATERAL_ACCEL = 4.442643f;

    private const float DIVE_POP_Y = 8f;
    private const float AIR_DIVE_Y_BOOST = 4.5f; // added on top of whatever Y velocity the jump-hold ramp had at the moment of diving, so a jump-dive always arcs instead of reading as a flat lunge
    private const float DIVE_SPEED_XZ = 16f;
    private const float ROLLOUT_SPEED_XZ = 20.216458f; // 32 * SCALE_FIX (tune 12–16)

    // --- GROUND WALL PUSH / SHUFFLE ---
    private const float WALL_PUSH_MAX_ANGLE = 30f;
    private const float WALL_SHUFFLE_MAX_ANGLE = 60f;
    private const float WALL_PUSH_SLIDE_SPEED = 1.5f;
    private const float WALL_SHUFFLE_MIN_ANIM_SPEED = 0.5f;
    private const float WALL_SHUFFLE_MAX_ANIM_SPEED = 2.0f;
    private Vector3 groundWallNormal = Vector3.Zero;

    private int suppressLandingFrames = 0;
    private int wallKickLock = 0;
    private int wallRegrabCooldown = 0;
    private Vector3 wallKickDir = Vector3.Zero;
    private Vector3 lastWallNormal = Vector3.Zero; // for stability

    // SideFlip animations are authored facing the opposite direction.
    // Add 180° to make them visually face the movement direction.
    private const float SIDEFLIP_YAW_OFFSET = Mathf.Pi;

    public bool initalJumpHold = true;

    public MarioState stateOfMario;
    private readonly CircularBuffer<MarioState> stateHistory = new CircularBuffer<MarioState>(7);

    private bool wasOnFloor = true;
    private MarioState _lastDbgState = MarioState.idle;
    private bool _lastDbgOnFloor = true;
    private int _jumpDbgFrames = 0; // when >0, [LAND_DBG] prints every frame instead of only on change

    // --- Jump buffer (press A slightly before landing) ---
    private const int JUMP_BUFFER_FRAMES = 6; // ~0.1s at 60fps
    private int jumpBuffer = 0;

    private bool queuedTouchdownJump = false;

    private readonly AirControlProfile AIR_SINGLE = new AirControlProfile(
        8.885286f, // 20 * SCALE_FIX
        8.885286f, // 20 * SCALE_FIX
        15.549251f, // 35 * SCALE_FIX
        8.885286f, // 20 * SCALE_FIX
        0f,
        0.155492f // 0.35 * SCALE_FIX (stopSnap is a speed threshold)
    );

    // All of the profiles below are now IDENTICAL to AIR_SINGLE, on purpose, per
    // the actual SM64 decomp (checked against a source-accurate Godot recreation's
    // air-state scripts): single jump, double jump, triple jump, side flip,
    // backflip, and wall kick ALL funnel through one shared air-steering function
    // (update_air_without_turn) with zero per-move parameter differences. The ONLY
    // move with custom horizontal handling at all is the long jump (a one-time
    // ×1.5 forward-speed multiplier on entry, capped at 48) — which this project
    // doesn't currently implement as a separate state.
    //
    // What actually differentiates each jump's feel in the real game is NOT air
    // control — it's the entry velocity: single/double/triple keep 80% of your
    // existing forward speed (`forward_velocity *= 0.8`) and launch at increasing
    // heights (42/52/69 raw Y), while side flip and backflip IGNORE your current
    // speed and force a fixed entry value instead — side flip forces forward speed
    // to +8 (a small forward push), backflip forces it to -16 (an actual backward
    // drift while ascending). That forced-entry-velocity behavior isn't
    // implemented here yet; it lives outside AirControlProfile (in each state's
    // jump-entry code) if it's ever wanted for full fidelity.
    private readonly AirControlProfile AIR_DOUBLE = new AirControlProfile(
        8.885286f,
        8.885286f,
        15.549251f,
        8.885286f,
        0f,
        0.155492f
    );

    private readonly AirControlProfile AIR_TRIPLE = new AirControlProfile(
        8.885286f,
        8.885286f,
        15.549251f,
        8.885286f,
        0f,
        0.155492f
    );

    // More real air control than a normal jump: accel is what actually governs how
    // fast he can REDIRECT mid-air (ApplyAirControl steers the whole velocity
    // vector toward wherever the stick points at this rate) — raising maxSpeed
    // alone doesn't help turning, only top speed in whatever direction he's
    // already committed to. Brake raised to match so full reversal still stays
    // snappier than a same-direction turn. reverseMax matches maxSpeed so holding
    // back hard enough can fully retrace the arc back toward the takeoff point,
    // not just weakly drift backward (also needed ApplyAirControl's reverse-target
    // sign fixed — it was pointing back at the original direction, not desiredDir).
    private readonly AirControlProfile AIR_SIDEFLIP = new AirControlProfile(
        11.550872f, // 26 * SCALE_FIX (== RUN_SPEED)
        17.770573f, // 40 * SCALE_FIX
        22.213216f, // 50 * SCALE_FIX
        8.885286f,
        11.550872f, // 26 * SCALE_FIX (== maxSpeed)
        0.155492f
    );

    // No decomp equivalent — Spin Jump isn't an SM64 move (Sunshine/custom
    // addition), so there's no source-accurate reference for it. Matched to
    // AIR_SINGLE for consistency with every move that DOES have a reference; its
    // floaty character still comes through via SPIN_FORWARD_BONUS and
    // SPIN_TERMINAL_FALL (its own forward carry + slower fall), which are separate
    // from air-steering entirely.
    private readonly AirControlProfile AIR_SPIN = new AirControlProfile(
        8.885286f,
        8.885286f,
        15.549251f,
        8.885286f,
        0f,
        0.155492f
    );

    // Tree jump-off reuses MarioState.wallJump for its physics/animation/locks (all
    // of which are correct for it — same "kick off a vertical surface" shape), but
    // it should feel like a normal jump in the air, not a wall-kick. Kept as its own
    // profile (rather than switching on state) via _isTreeJumpAirborne below, so
    // none of the existing wallJump-specific behavior has to change.
    private readonly AirControlProfile AIR_TREE = new AirControlProfile(
        8.885286f,
        8.885286f,
        15.549251f,
        8.885286f,
        0f,
        0.155492f
    );

    private readonly AirControlProfile AIR_WALLJUMP = new AirControlProfile(
        13.328f, // 30 * SCALE_FIX — raised above WALL_KICK_OUT_SPEED (9.77) so holding toward
        // his facing direction can actually extend/carry the kick instead of reining it back in
        8.885286f, // accel restored — strong control when pointing where he's already facing
        11.106608f, // 25 * SCALE_FIX — brake strengthened, backward input now has real bite again
        8.885286f,
        0f, // no pull-back reversal allowed
        0.155492f
    );

    // Backflip launches him moving BACKWARD (opposite his facing — see
    // StartBackflip's `back = lastFacingDirection` velocity). So "backward
    // control" here is the normal maxSpeed/accel path (continuing to hold back =
    // continuing his existing momentum direction — kept high, matches
    // AIR_SIDEFLIP), while "forward control" is the brake/reverseMax path
    // (fighting against that momentum toward his original facing) — reverseMax
    // cut way down so pushing forward only lets him drift slightly that way,
    // never fully reverse the flip's momentum.
    private readonly AirControlProfile AIR_BACKFLIP = new AirControlProfile(
        4.442643f, // 10 * SCALE_FIX — halved again, still too strong
        5.331172f, // 12 * SCALE_FIX — halved again, still too strong
        22.213216f, // 50 * SCALE_FIX
        8.885286f,
        0.888529f, // 2 * SCALE_FIX — only a slight forward drift allowed
        0.155492f
    );

    // Ground-pound jump (the jump-cancel out of a ground pound landing) shares
    // backflip/side flip's air control — same "acrobatic recovery move" tier, even
    // though its height (GroundPoundJumpVelocity) is tuned independently. Used via
    // _isGroundPoundJump in TryGetAirProfile since this move reuses MarioState.
    // singleJump rather than having its own state (same pattern as
    // _isTreeJumpAirborne reusing wallJump).
    private readonly AirControlProfile AIR_GROUND_POUND_JUMP = new AirControlProfile(
        11.550872f,
        17.770573f,
        22.213216f,
        8.885286f,
        11.550872f,
        0.155492f
    );

    // Dive is a committed lunge, not a steerable jump — so unlike every profile
    // above, maxSpeed matches DIVE_SPEED_XZ itself rather than a jump's slower
    // cap. That's the difference between "some air control" and "full air
    // control": with a jump-speed maxSpeed, ApplyAirControl would immediately
    // brake the dive from 16 down toward ~9 the instant it started steering,
    // gutting the lunge's distance even when held perfectly straight. Matching
    // maxSpeed to DIVE_SPEED_XZ means holding forward preserves the full dive,
    // while accel (kept low, well under AIR_SINGLE's) still lets the stick bend
    // the trajectory left/right over the course of the dive. reverseMax is 0 —
    // you can curve a dive, not abort one.
    // accel bumped 1.5x (4.442643 -> 6.663965) per playtest feedback: the curve
    // was there but too subtle to feel. Bumped again to 2x base (-> 8.885286)
    // per follow-up feedback.
    private readonly AirControlProfile AIR_DIVE = new AirControlProfile(
        DIVE_SPEED_XZ, // 16 - don't bleed off the lunge's distance
        8.885286f, // 4.442643 * 2
        8.885286f, // 20 * SCALE_FIX
        2.221321f, // 5 * SCALE_FIX - slow drag; neutral stick shouldn't stall the dive early
        0f,
        0.155492f
    );

    // Rollout gets its own profile rather than reusing AIR_SINGLE — bumping
    // AIR_SINGLE's accel would also strengthen single/double/triple jump's air
    // control, which nobody asked for. Same shape as AIR_SINGLE otherwise;
    // accel bumped 1.5x (8.885286 -> 13.327929) per playtest feedback, same as
    // AIR_DIVE above. Bumped again to 2x base (-> 17.770572) per follow-up
    // feedback.
    private readonly AirControlProfile AIR_ROLLOUT = new AirControlProfile(
        8.885286f,
        17.770572f, // 8.885286 * 2
        15.549251f,
        8.885286f,
        0f,
        0.155492f
    );

    public float rotation_angle = 0.0f;

    private SpinJumpEffects spinRingFx;

    public double mouseSensitivity = 0.001;
    public double twistInput = 0.0;
    public double pitchInput = 0.0;

    Vector3 velocity;
    private Node3D armature;
    public Node3D gameCam;

    private Node3D springArmPivot;
    private SpringArm3D springArm;
    private Camera3D camera;

    private LandingDustFx landingDustFx;
    private SlideDustFx slideDustFx;

    // --- Air takeoff dir lock (used to restrict wall-slide to a cone) ---
    private Vector3 airTakeoffDirXZ = Vector3.Zero; // normalized XZ
    private bool airTakeoffDirValid = false;

    // Allowed deviation from takeoff direction to still be eligible to wall-slide
    private const float WALL_TAKEOFF_CONE_DEG_DEFAULT = 55f;

    // --- Wall slide entry tuning (per-state) ---
    private const float WALL_MIN_APPROACH_SPEED_DEFAULT = 8.0f; // <-- was effectively 4.5
    private const float WALL_APPROACH_DOT_DEFAULT = 0.5f; // cos(60deg) — <-- was 0.62 (~51.7deg)

    private const float WALL_MIN_HORIZONTAL_RATIO = 0.18f;

    // if horizontal speed is less than ~18% of vertical fall speed, don't grab


    private MarioState lastRecordedState;
    // ===================== ROLLOUT SLOPE BOOST =====================
    // Rolling out onto a ramp and driving UP it lets him exceed RUN_SPEED for
    // as long as he's on the slope. Deliberately uphill-only and cut dead the
    // moment he reaches flat ground.
    //
    // Note this runs against the grain of the physics (and of SMS's own
    // slopeProcess(), whose arithmetic adds speed facing DOWNhill and subtracts
    // it facing uphill) — it's a chosen game-feel behaviour, not a simulation.

    /// <summary>Extra m/s on top of RUN_SPEED at full steepness driving
    /// straight uphill. Scaled down by both the slope's steepness and how
    /// directly he's heading up it, so a gentle ramp or a diagonal line gives
    /// proportionally less. RUN_SPEED is 11.55 for scale.</summary>
    [Export]
    public float RolloutSlopeMaxBoost = 9.0f;

    /// <summary>How fast the boost ramps in/out toward its target (m/s²). At
    /// the default it reaches full boost in roughly a third of a second.</summary>
    [Export]
    public float RolloutSlopeBoostAccel = 20f;

    /// <summary>Steepness (sin of the slope angle) below which the ground counts
    /// as flat and the boost is cut. 0.1 ≈ 5.7°, enough to ignore trivial
    /// unevenness without eating a real ramp.</summary>
    [Export]
    public float RolloutSlopeFlatThreshold = 0.1f;

    private float _rolloutSlopeBoost = 0f; // current extra speed on top of RUN_SPEED
    private bool _rolloutBoostArmed = false; // set on rollout landing, cleared at flat ground / idle

    /// <summary>Upward velocity below which a rollout counts as "done rising" and
    /// is allowed to land. The roll does NOT need to finish its arc or its
    /// animation — once he's touching the ground and no longer meaningfully
    /// going up, he hands straight off to rolloutRun (and the slope boost).
    /// Measured before this existed: 4-5 frames of singleRollout skimming a
    /// 33.8° ramp at velY=+0.15 with onFloor=True, waiting for velY to go
    /// negative. Still comfortably above the ~8 m/s a fresh roll launches at,
    /// so it can't re-open the ground-jump hijack.</summary>
    private const float ROLLOUT_LAND_MAX_RISE = 2f;

    private const float ROLLOUT_RUN_DECEL = 0.08f; // stick neutral slow-down
    private const float ROLLOUT_RUN_ACCEL = 0.25f; // responsiveness when stick held
    private const float ROLLOUT_STOP_SPEED = 1.2f; // below this, snap to idle

    // InverseK stuff
    SkeletonIK3D skeletonIK3DWaist;
    Node3D targetWaist;

    // Jump chain tracking (replaces stateHistory.Contains for chaining)
    private int jumpChainStage = 0; // 0=none, 1=next jump is double, 2=next jump is triple
    private int jumpChainTimer = 0;
    private const int JUMP_CHAIN_WINDOW = 18; // frames you allow the next jump after landing

    private Vector3 aimDirThisFrame = Vector3.Zero;

    [Export]
    public float SideFlipTurningYawSpeed = 18f; // tune (bigger = snappier)

    // SpinJump facing (separate from armature spin rotation)
    // SpinJump takeoff facing (LOCKED at start of SpinJump)
    private Vector3 spinJumpTakeoffDir = Vector3.Forward;
    private bool spinJumpDirLocked = false;
    private float spinJumpLockedYaw = 0f;

    // SideFlip facing lock (takeoff dir)
    private bool sideFlipDirLocked = false;
    private Vector3 sideFlipTakeoffDir = Vector3.Forward;
    private float sideFlipLockedYaw = 0f;
    private Vector3 sideFlipStoredDir = Vector3.Zero; // direction we should flip/jump toward
    private bool _sideFlipLanding = false; // keep PI offset on armature during sideflip landing

    private const float SPIN_TRIPLE_MIN_SPEED = 18f; // speed required at landing (XZ)
    private const float SPIN_TRIPLE_MIN_INPUT = 0.5f; // stick must be held (not neutral)

    // Facing/movement dir captured at the START of a jump chain (first single jump,
    // stage 0) — triple jump locks to THIS instead of re-reading current input, so
    // it keeps facing the direction the chain started in rather than snapping to
    // wherever the stick happens to be pointing by the third jump.
    private Vector3 _jumpChainStartDir = Vector3.Forward;

    // Minimum carried XZ speed required to actually get a triple jump out of the
    // chain; below this it falls back to a plain single jump instead (can't triple
    // jump standing still).
    private const float TRIPLE_JUMP_MIN_SPEED = 5f;
    private const float SPIN_TRIPLE_MAX_ANGLE = 35f; // allowed deviation from locked dir
    private bool spinTripleQueued = false;
    private Vector3 spinTripleDir = Vector3.Forward;

    private const int SPIN_COOLDOWN_FRAMES = 8;
    private int spinCooldown = 0;
    private int spinDropFrames = 0;

    // Grounded spin: spin the stick + tap R while grounded to spin in place;
    // press A during it to spin-jump in any direction (or straight up if neutral).
    [Export]
    public float GroundSpinDuration = 2.0f;
    private float groundSpinTimer = 0f;

    [Export]
    public float GroundSpinMoveSpeed = 11.55f; // ~RUN_SPEED; steer while spinning

    [Export]
    public float GroundSpinDegPerSec = 2000f; // visual body-spin rate (matches SpinJump)

    // Backflip: hold Z to squat (ma_sqwat), press A to backflip (ma_jump_rolling).
    // Toned down slightly below SIDEFLIP_JUMP_VELOCITY — was matching it exactly
    // but that jumped a bit too high.
    [Export]
    public float BackflipJumpVelocity = SIDEFLIP_JUMP_VELOCITY * 0.9f;

    [Export]
    public float BackflipBackSpeed = 4.0f;
    private bool _backflipFalling = false;
    private float _backflipTimer = 0f;
    private Vector3 _backflipLaunchDir = Vector3.Forward;

    // Dive bonk: diving into a wall -> bounce off (ma_bkdwn in air), land and get
    // up (ma_sdown) before control returns, with a star burst at the impact.
    [Export]
    public float BonkBackSpeed = 8.0f; // pushback off the wall

    [Export]
    public float BonkUpSpeed = 9.0f; // upward pop on impact

    [Export]
    public float BonkMinDiveSpeed = 4.0f; // min into-wall speed to trigger a bonk

    [Export]
    public float BonkLandingSlideSpeed = 2.5f; // short backward skid on touchdown

    [Export]
    public float BonkLandingSlideDeceleration = 8.0f;
    private bool _bonkAirborne = false;
    private float _bonkTimer = 0f;
    private Vector3 _bonkAwayDirection = Vector3.Zero;
    private GpuParticles3D bonkStars;

    // You already have these, keep them:
    private bool spinTracking = false;
    private float lastStickAngle = 0f;
    private float spinAngleAccum = 0f;
    private int spinFrames = 0;

    // spinInput should be treated as a 1-frame event
    private bool spinInput = false;

    private int spinBuffer = 0;

    // Quadrant-only spin detection
    private const int SPIN_BUFFER_FRAMES = 20; // buffer duration after a valid spin
    private const int WINDOW_FRAMES = 20; // frames allowed to hit all 4 quadrants (you already have this)
    private const float AXIS_EPS = 0.08f; // ignore samples too close to X==0 or Y==0 (prevents free jitter)

    private bool spinQuadTracking = false;
    private int spinFramesLeft = 0;
    private int lastQuadrant = -1;

    // Pivot tuning
    private const float PIVOT_MIN_INPUT = 0.06f; // stick must be at least this to pivot
    private const float PIVOT_MAX_INPUT = DEADZONE; // <= DEADZONE so it won't move
    private const float PIVOT_STOP_DECEL = 0.35f; // how quickly XZ velocity dies
    private const float PIVOT_TURN_SPEED = 14f; // higher = snappier turn

    // Sneak tuning (ma_sstep) — sits between pivot and walking
    private const float SNEAK_MAX_INPUT = 0.35f; // stick above this → walking/running
    private const float SNEAK_SPEED = 2.888218f; // ~6.5 * SCALE_FIX — slow tiptoe
    private const float SNEAK_MIN_ANIM_SPEED = 0.4f; // anim speed at lowest sneak stick
    private const float SNEAK_MAX_ANIM_SPEED = 1.0f; // anim speed at highest sneak stick

    // --- LEDGE GRAB ---
    private Node3D ledgeSensors;
    private RayCast3D chestRay;
    private RayCast3D headRay;
    private RayCast3D topDownRay;

    private Vector3 ledgeTopPoint = Vector3.Zero; // where the top surface is
    private Vector3 ledgeWallNormal = Vector3.Back; // wall normal at grab
    private Vector3 ledgeHangPos = Vector3.Zero; // exact position we pin Mario to while hanging
    private Vector3 ledgeStandPos = Vector3.Zero; // where we place him after climbing

    private bool ledgePinned = false;

    // timers for climb/hopup
    private float ledgeClimbT = 0f;
    private float ledgeClimbDur = 0.55f; // will auto-fill from animation if found

    // TUNING (these are "world units" — adjust once and you're done)
    // --- LEDGE GRAB (scaled) ---
    private const float LEDGE_MIN_FALL_SPEED = -0.888529f; // -2.0 * SCALE_FIX

    private const float LEDGE_HANG_BACK = 0.199919f; // 0.45 * SCALE_FIX
    private const float LEDGE_HANG_DOWN = 0.111066f; // 0.25 * SCALE_FIX
    private const float LEDGE_STAND_UP = 0.004443f; // 0.01 * SCALE_FIX
    private const float LEDGE_STAND_FORWARD = 0.444264f; // 1.0 * SCALE_FIX

    private const float LEDGE_HOP_UP_VEL = 17.770573f; // 40 * SCALE_FIX
    private const float LEDGE_HOP_FWD_SPEED = 7.996758f; // 18 * SCALE_FIX

    private const int LEDGE_REGRAB_COOLDOWN_FRAMES = 10;
    private int ledgeRegrabCooldown = 0;

    // --- TREE CLIMB ---
    private Node3D _treeNode; // root node of grabbed PalmTree (for instance validity)
    private Vector3 _treeCenter; // world trunk axis pivot (XZ); Y is unused
    private float _treeRadiusBottom = 1.4f; // distance Mario stands from axis at base
    private float _treeRadiusTop = 0.6f; // distance at the very top (trunk tapers)
    private float _treeBottomY = 0f; // world Y where Mario should release at the base
    private float _treeTopY = 0f; // world Y at which the top-reach pop fires
    private float _treeLeafLandY = 0f; // world Y where Mario teleports onto the leaves at top
    private float _treeAngle = 0f; // current azimuth around trunk (atan2(dx, dz))
    private float _treeY = 0f; // current Mario Y on trunk
    private bool _treePinned = false;
    private float _treeGrabAnimT = 0f;
    private float _treeGrabAnimDur = 0.4f; // overwritten from ma_tree_catch length on grab

    private const float TREE_CLIMB_SPEED_Y = 3.5f; // world units / sec up/down
    private const float TREE_ORBIT_SPEED = 1.7f; // rad / sec around trunk
    private const float TREE_GRAB_RADIUS_PAD = 0.08f; // gap between Mario center and trunk surface
    private const float TREE_JUMP_OFF_UP = 16.0f; // wall-kick up impulse
    private const float TREE_JUMP_OFF_OUT = 11.0f; // wall-kick outward impulse
    private const float TREE_TOP_POP_UP = 9.0f; // upward hop when reaching top (unused after top-reach teleport rework)
    private const float TREE_GROUND_CLEAR = 0.05f; // min clearance between Mario's feet and the floor while on trunk
    private const float TREE_GROUND_RAY = 3.0f; // how far down to look for the ground
    private const int TREE_REGRAB_COOLDOWN_FRAMES = 18;
    private int _treeRegrabCooldown = 0;

    // --- GROUND POUND JUMP ---
    // Special jump from the "slped" stand-up phase of a ground pound. Fixed height,
    // body spins 360° from launch to apex, uses the normal single-jump animation.
    // Slightly higher than MAX_JUMP_VELOCITY_DOUBLE (17.77) — caps out a touch above a
    // full-press double jump as a reward for landing the ground-pound and chaining.
    [Export]
    public float GroundPoundJumpVelocity = 19.5f; // fixed launch speed (height = v²/(2g))

    [Export]
    public float GroundPoundJumpSpinTurns = 1.0f; // full 360° rotations during the rise
    private bool _isGroundPoundJump = false;
    private float _gpJumpStartYaw = 0f;
    private float _gpJumpTimer = 0f;
    private float _gpJumpSpinDuration = 0.3f; // computed at launch from velocity / gravity

    // True if the most recent tree jump-off used ma_tjmp1 (which is authored facing
    // backwards). On landing, we have to undo the +Pi armature yaw so Mario faces forward.
    private bool _treeJumpOffTjmp1 = false;
    private Vector3 _treeJumpOffDir = Vector3.Forward;

    // True while airborne from a tree jump-off — gives it the AIR_TREE control
    // profile instead of AIR_WALLJUMP (state stays wallJump for everything else:
    // locks, animation, wall-slide timing are all correct as-is for it). Cleared
    // on landing.
    private bool _isTreeJumpAirborne = false;

    // Platform tracking for ledge grab (so Mario moves with moving platforms)
    private Node3D _ledgePlatformBody = null;
    private Vector3 _ledgeLocalHangPos; // hang position in platform's local space
    private Vector3 _ledgeLocalStandPos; // stand position in platform's local space
    private Vector3 _ledgeLocalWallNormal; // wall normal in platform's local space

    private float _baseFloorSnapLength; // captured in _Ready — see jump-launch snap override

    private CollisionShape3D bodyCol;
    private float capRadiusWorld;
    private float capHalfHeightWorld; // (cap.Height*0.5 + cap.Radius) in WORLD units
    private float bottomOffsetFromBody; // bottomY = bodyY + bottomOffsetFromBody  (negative)

    [Export]
    private float ikRaycastHeight = 0.222132f; // 0.5 * SCALE_FIX

    [Export]
    private float footOffset = 0.177706f; // 0.4 * SCALE_FIX

    [Export]
    private Vector2 minMaxInterpolation = new Vector2(0f, 2.221322f); // 5 * SCALE_FIX

    private Skeleton3D skeleton;

    private AnimationPlayer animPlayer;
    private float currentJumpVelocity = BASE_JUMP_VELOCITY;
    private float jumpHoldTime = 0.0f;
    private const float MAX_JUMP_HOLD_TIME_SINGLE = 0.2f;
    private const float MAX_JUMP_HOLD_TIME_DOUBLE = 0.4f;
    private bool isJumping = false;

    private Vector3 lastFacingDirection = Vector3.Zero;

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    private Vector3 wallIncomingDirXZ = Vector3.Zero;

    public int gettingUpFromSlidingTimer = 53;
    public int bellyRolloutTimer = 20;
    public int sittingAnimationTimer = 58;
    public int sleepingTimer = 28;
    public int idleTimer = 580;
    public int sittingTimerWait = 180;
    public int sleeptimer = 400;
    public int sideFlipTurningTimer = 33;
    public int landingTimer = 7;
    public int newLandingTimer = 7;

    // Wall slide / wall jump animations authored facing opposite direction
    // Wall slide anim is authored backward (needs +PI)
    private const float WALL_SLIDE_YAW_OFFSET = Mathf.Pi;

    // Wall jump anim: start with 0 (if still backwards, change to Mathf.Pi)
    private bool wallJumpAnimPlayed = false;
    private bool wallJumpAnimFrozen = false;

    private const float WALL_JUMP_YAW_OFFSET = 0f;

    private float WallSlideYawFromDir(Vector3 dir)
    {
        return WrapAnglePi(YawFromDir(dir) + WALL_SLIDE_YAW_OFFSET);
    }

    private float WallJumpYawFromDir(Vector3 dir)
    {
        return WrapAnglePi(YawFromDir(dir) + WALL_JUMP_YAW_OFFSET);
    }

    // Timings (assuming 60fps physics; Sunshine is ~30fps so these feel similar)
    private const int WALL_KICK_LOCK_FRAMES = 8; // committed kick
    private const int WALL_REGRAB_COOLDOWN_FRAMES = 14; // prevents instant re-stick
    private const int WALL_SLIDE_LOST_WALL_GRACE = 6; // keep as-is (feels good)

    // --- WALL SLIDE FACING ---
    private const float WALL_SLIDE_FACE_SPEED = 18f; // how quickly armature turns while sliding

    private bool wallJumpHoldActive = false;
    private float wallJumpHoldTime = 0f;

    // precomputed “this is the direction we’ll kick to”
    private Vector3 wallPlannedKickDir = Vector3.Zero;

    private int wallSlideLostWallFrames = 0;

    // --- SLIP (steep ground too shallow to count as a real wall) ---
    private const float WALL_TRUE_VERTICAL_MIN_ANGLE_DEG = 75f; // surfaces steeper than this (angle from Up) are a real wall, eligible for wall-slide/wall-jump; anything between floor_max_angle and this is just steep ground he can't stand on — slip instead
    /// <summary>Top slide speed along the slope's downhill tangent at FULL
    /// (vertical) steepness; the actual cap is this * the steepness curve, so
    /// on a 66° face it's ~91% of this. For scale: RUN_SPEED is 11.55 and
    /// ROLLOUT_SPEED_XZ is 20.2, so anything under ~12 here makes slipping
    /// slower than walking — which is what the old 6.5 was doing (6.5 *
    /// sin(66°) = 5.9, about half walking pace).</summary>
    [Export]
    public float SlipMaxSpeed = 22f;

    /// <summary>Fraction of REAL gravity that pulls him down the fall line.
    /// 1.0 is true physics: accel = GRAVITY (62.2) * sin(angle), i.e. 56.8 m/s²
    /// on a 66° face. The old hand-picked constant was 14, ~4.4x weaker than
    /// gravity, which is why the slide crawled instead of dropping him. Lower
    /// this for a floatier, more forgiving slip.</summary>
    [Export]
    public float SlipGravityScale = 1.0f;

    /// <summary>Exponent applied to sin(angle) before it scales speed/accel.
    /// 1.0 = true physics. Worth knowing: across the whole slip band sin only
    /// runs 0.906 (65°) to 0.966 (75°) — barely 7% — so at 1.0 every slippable
    /// slope is effectively "max speed" and the angle is almost unfelt. Raising
    /// this exaggerates the difference between a just-too-steep face and a
    /// near-vertical one (at 4.0 the same band spreads 0.67 to 0.87), at the
    /// cost of making the shallow end slower.</summary>
    [Export]
    public float SlipSteepnessCurve = 1.0f;

    /// <summary>Minimum horizontal speed (m/s) on reaching walkable ground for
    /// a slip to continue as a belly slide. Below this he just stands up, so a
    /// slip that trickled to a stop doesn't flop onto his belly for one frame
    /// and immediately play the get-up. For scale, a full-speed 66° slip
    /// arrives at roughly 8 m/s horizontal.</summary>
    [Export]
    public float SlipExitSlideMinSpeed = 3.0f;

    /// <summary>Multiplier on the horizontal speed carried from a slip into the
    /// belly slide. 1.0 is the honest physical carry — the floor absorbs the
    /// slip's vertical component (~91% of it on a 66° face), so he arrives much
    /// slower than his slide speed suggests. Raise above 1.0 if that reads too
    /// tame and you want the slope to fling him further.</summary>
    [Export]
    public float SlipExitSlideBoost = 1.0f;

    /// <summary>Hard floor on downward velocity while slipping — a safety net
    /// for pathological geometry, NOT a speed control. It is additionally
    /// clamped at runtime so it can never bind tighter than the slide's own
    /// cap: on a steep face the slide is almost entirely vertical (91% of it at
    /// 66°), so a fall limit set above the slide speed would silently throttle
    /// the slide instead. The old -13 was doing exactly that.</summary>
    [Export]
    public float SlipMaxFallSpeed = -26f;
    private const float SLIP_HOP_UP_VEL = 6f; // weak escape hop — not a directional wall-kick, this isn't a real wall
    private const float SLIP_LAND_DURATION = 0.458333f; // ma_slpla's real clip length
    private int slipLostSurfaceFrames = 0;
    private Vector3 lastSlipNormal = Vector3.Zero;
    private float slipAirLaunchTimer = 0f; // >0 while ma_slpla should override ma_jump right after leaving a slip for airborne (sliding off the edge, or the escape hop)
    private Vector3 slipSlideVel = Vector3.Zero; // slide velocity IN the slope's tangent plane; builds from rest each slip episode and is steered by the stick

    /// <summary>How hard the stick can push the slide while slipping (m/s²).
    /// Modelled on SMS's `doSliding()`, which does NOT let the stick set the
    /// slide direction — it adds a small acceleration to the existing slide
    /// velocity each frame (scaled there by stick magnitude * 0.03125), so you
    /// carve gradually instead of turning on a dime. Keep this well BELOW the
    /// fall-line pull (GRAVITY * SlipGravityScale * slopeFactor, ~57 on a 66°
    /// face) so gravity always dominates the descent. The downhill component is
    /// additionally hard-clamped to never go negative, so no amount of tuning
    /// here lets him climb ground he can't stand on. Note this is now a much
    /// smaller share of total accel than it was against the old constant 14, so
    /// steering will feel weaker — raise it if carving is too subtle.</summary>
    [Export]
    public float SlipSteerAccel = 9.0f;
    private bool _slipFacingUphill = false; // locked at entry: ma_slpbk (facing uphill, slides backward) vs ma_slip (facing downhill, slides forward)

    //Sleeping effects
    private ZEffectSpawner zEffectSpawner;

    //EyeEffect
    private Texture2D awakeEyeTexture;
    private Texture2D sleepingEyeTexture;
    private StandardMaterial3D leftEyeMaterial;
    private StandardMaterial3D rightEyeMaterial;
    private MeshInstance3D marioMesh;

    //HandEffects
    private StandardMaterial3D RightHandMaterial;
    private StandardMaterial3D RightHandClosedMaterial;
    private StandardMaterial3D LeftHandMaterial;
    private Node3D RightClosedHand;
    private Node3D LeftClosedHand;

    // Third variant. Present in the scene but was never referenced by any code,
    // so it rendered permanently on top of whichever hand was actually selected
    // — the "all the hands are showing at once" problem. Unused by the pose
    // rules below for now; wired up so it can be given a state later.
    private Node3D RightSlightlyOpenHand;
    private Node3D LeftSlightlyOpenHand;

    private enum HandPose
    {
        Open,
        Closed,
        SlightlyOpen,
    }

    // Deliberately starts at an invalid value so the first ApplyHandPose always
    // writes, rather than assuming the scene's default visibility is correct.
    private HandPose _handPose = (HandPose)(-1);

    // Captured at setup so ShowOpenHands() can restore the open-hand materials
    // exactly (the closed-hand swap sets their albedo to transparent).
    private Color _rightHandOpenAlbedo = Colors.White;
    private Color _leftHandOpenAlbedo = Colors.White;
    Color currentColor;
    Vector3 direction = new Vector3(0, 0, 0);

    //Changing Direction Sharply
    private readonly CircularBuffer<Vector3> Last4FacingDirections = new CircularBuffer<Vector3>(4);

    //SpinJump stuff
    // Deadzone to avoid accidental inputs from a nearly centered stick.
    private const float DEADZONE = 0.2f;

    // Track which quadrants have been visited.
    private bool[] quadrantVisited = new bool[4] { false, false, false, false };
    private int frameCounter = 0;

    private float walkingStrength;

    private Node3D SpinJumpEffects;
    private GpuParticles3D blueSpinEffects;
    private GpuParticles3D redSpinEffects;
    private GpuParticles3D whiteSpinEffects;
    private GpuParticles3D footSparks;
    private GroundPoundEffects groundPoundFx;
    private GroundPoundImpactFx groundPoundImpactFx;
    private Node3D camControl;
    private Node3D hangAnchor;

    // --- Visual root lock (prevents ma_hgup root/armature translation offset) ---
    private Vector3 armatureBaseLocalPos;
    private Vector3 armatureBaseLocalScale;

    // --- Run bob (procedural body bounce; the baked run clips are flat) ---
    // Lifts the whole visual rig between footfalls so running doesn't glide.
    // Phase-locked to the run clip so the low point lands on each footplant, and
    // biased upward-only so the planted foot never sinks below the ground (a
    // downward dip needs foot IK — Phase 2).
    [Export]
    public float RunBobAmplitude = 0.07f; // metres the rig rises at mid-stride

    [Export]
    public int RunBobDipsPerLoop = 2; // footfalls per run-clip loop

    [Export]
    public float RunBobPhaseOffset = 0f; // align the low point with footplant (radians)

    [Export]
    public float RunBobEaseSpeed = 7f; // how fast the bob fades in/out
    private float _runBobWeight;
    private float _runBobPhase;

    // --- Head-forward lock (cancels the baked side-to-side head turn while moving) ---
    [Export]
    public bool HeadLockEnabled = true;

    [Export]
    public float HeadLockEaseSpeed = 8f;

    // Slight upward head tilt while moving so he looks ahead, not at the ground.
    // If the tilt goes the wrong way, flip the sign or change the axis component.
    [Export]
    public float HeadLookUpDegrees = -12f;

    [Export]
    public Vector3 HeadLookUpAxis = new(0f, 0f, 1f);

    private HeadForwardLock _headLock;
    private FootIK _footIK;
    private float _footIKWeight = 0f;

    /// <summary>Master switch for slope foot IK.</summary>
    [Export]
    public bool FootIKEnabled = true;

    /// <summary>Ground steeper than this (degrees from flat) stops Mario dozing
    /// off while idle. The sit/sleep clips lay him out flat, so on a ramp he'd
    /// sit straight through it.</summary>
    [Export]
    public float SleepMaxSlopeDegrees = 8f;

    /// <summary>How fast the foot IK eases in/out (weight per second). Fast
    /// enough to be planted by the time he settles, slow enough that entering
    /// idle doesn't snap the legs.</summary>
    [Export]
    public float FootIKEaseSpeed = 6f;
    private float _headLockWeight;

    // --- Turn lean (leans the WAIST left/right into a turn, same IK target the
    // forward run-lean already uses — harder/faster turn = more lean). Driven
    // by the RATE his visual facing (armature yaw) is currently rotating, not a
    // spring — this is a sustained lean for as long as he's actively turning,
    // not a bounce-and-settle reaction. If it leans the wrong way, flip the
    // sign on TurnLeanSensitivity. ---
    [Export]
    public bool TurnLeanEnabled = true;

    [Export]
    public float TurnLeanMaxDegrees = 30f; // clamp on how far he can lean — only a

    // genuinely sharp turn should get anywhere near this
    [Export]
    public float TurnLeanSensitivity = -0.15f; // how much yaw-rate becomes lean angle

    // (negative flips which way he leans — was leaning away from the turn)
    [Export]
    public float TurnLeanEaseSpeed = 22f; // how fast the lean eases toward target —

    // needs to be fast: the actual reorientation completes in under ~150ms, so a
    // slow ease never catches up to the peak before the turn's already over.
    private float _lastArmatureYaw;
    private float _turnLeanCurrentDeg;
    private float _turnYawRateDeg; // sampled on the physics tick — see _PhysicsProcess

    // --- Head jiggle (jnt_head bobble — the most visible SMS jiggle) ---
    [Export]
    public bool HeadJiggleEnabled = true;

    [Export]
    public float HeadJiggleWeight = 1f;

    [Export]
    public float HeadJiggleStiffness = 110f; // wobble frequency — how FAST it bounces

    [Export]
    public float HeadJiggleDamping = 3.5f; // bounce decay — LOW = keeps wobbling, HIGH = one snap and done

    [Export]
    public float HeadJiggleResponse = 0.6f;

    [Export]
    public float HeadJiggleGain = 0.8f;

    [Export]
    public float HeadJiggleMaxShiftCm = 7f;

    [Export]
    public float HeadJiggleLandingKick = 0.55f;

    [Export]
    public float HeadJiggleJumpKick = 0.4f;
    private JiggleBone _headJiggle;

    // --- Arm jiggle (jnt_sldr_R/L → jnt_hand_R/L — the sleeves/arms swing as a
    // unit, mirrored on both sides) ---
    [Export]
    public bool ArmJiggleEnabled = true;

    [Export]
    public float ArmJiggleWeight = 1f;

    [Export]
    public float ArmJiggleStiffness = 110f;

    [Export]
    public float ArmJiggleDamping = 4f;

    [Export]
    public float ArmJiggleResponse = 0.55f;

    [Export]
    public float ArmJiggleGain = 0.7f;

    [Export]
    public float ArmJiggleMaxShiftCm = 7f;

    [Export]
    public float ArmJiggleLandingKick = 0.4f;

    [Export]
    public float ArmJiggleJumpKick = 0.3f;
    private JiggleBone _armJiggleR;
    private JiggleBone _armJiggleL;

    // Mario's horizontal acceleration, sampled on the physics tick (fixed dt) so the
    // jiggle gets a clean drive signal instead of differencing velocity at render rate.
    private Vector3 _lastPhysHorizVel;
    private Vector3 _marioHorizAccel;

    private ShapeCast3D pickupCast;
    private Vector3 pickupCastInitialLocalPos; // Store initial offset for rotation
    private Node3D carrySocket;

    private RigidBody3D heldBody;
    public bool IsCarrying => heldBody != null;
    public bool SpinInput => spinInput;
    private uint heldLayer;
    private uint heldMask;
    private RigidBody3D pendingPickup;

    private AnimationTree animTree;

    // Godot 4 Blend2 usually exposes this as blend_amount.
    // If this exact path doesn’t work, use “Copy Property Path” (see note below).
    private const string CARRY_BLEND_PATH = "parameters/CarryBlend/blend_amount";

    // AnimationTree TimeScale node for run speed while carrying.
    // Create a TimeScale node in the tree named "RunTimeScale" and copy its property path if needed.
    private const string RUN_TIMESCALE_PATH = "parameters/RunTimeScale/scale";

    // Run-clip playback speed mapped from movement speed (low speed -> full speed).
    // Widen Max to make his legs clearly cycle faster at full tilt.
    [Export]
    public float RunAnimSpeedMin = 0.9f;

    [Export]
    public float RunAnimSpeedMax = 1.4f;
    private float carryBlend = 0f; // current value (smoothed)
    private float carryTarget = 0f; // 0 = normal run, 1 = carry upper-body
    private const float CARRY_BLEND_SPEED = 12f; // higher = faster snap

    private AnimationTree _tree;
    private AnimationNodeStateMachinePlayback _sm;

    /// <summary>
    /// Assign a PlayerProfile .tres asset here to apply custom colors.
    /// Leave null to keep the default Mario appearance.
    /// </summary>
    [Export]
    public PlayerProfile Profile = null;

    public override void _Ready()
    {
        base._Ready();
        CacheCapsuleWorldMetrics();
        _baseFloorSnapLength = FloorSnapLength;

        // Health / Lives setup
        _health = StartingHealth;
        _lives = StartingLives;
        _lifeMeter = GetNodeOrNull<LifeMeter>("LifeMeter");
        _lifeMeter?.SetHealth(_health);
        _lifeCounterHud = GetNodeOrNull<LifeCounterHud>("LifeCounterHud");

        _coins = 0;
        _yellowCoinHud = GetNodeOrNull<YellowCoinHud>("YellowCoinHud");
        _yellowCoinHud?.ShowHud();

        _redCoins = 0;
        _redCoinHud = GetNodeOrNull<RedCoinHud>("RedCoinHud");
        _redCoinHud?.SetCount(0);

        _blueCoins = GameData.Instance.BlueCoinCount;
        _blueCoinHud = GetNodeOrNull<BlueCoinHud>("BlueCoinHud");
        _blueCoinHud?.SetCount(_blueCoins);

        _shines = 0;
        _shineHud = GetNodeOrNull<ShineHud>("ShineHud");
        _shineHud?.SetCount(0);
        _camera = GetNodeOrNull<SunshineCamera>("CamController");

        _shineGetCamRig = GetNodeOrNull<Node3D>("ShineGetCamRig");
        _shineGetCam = GetNodeOrNull<Camera3D>("ShineGetCamRig/ShineGetCam");
        // The AnimationPlayer may sit directly under the rig or under the
        // camera, and its name can vary — find the first one anywhere under
        // the rig rather than hard-coding the path.
        _shineGetCamAP = GetNodeOrNull<AnimationPlayer>("ShineGetCamAP");
        if (_shineGetCamAP == null && _shineGetCamRig != null)
        {
            foreach (
                Node n in _shineGetCamRig.FindChildren("*", nameof(AnimationPlayer), true, false)
            )
            {
                _shineGetCamAP = n as AnimationPlayer;
                if (_shineGetCamAP != null)
                    break;
            }
        }
        string animList =
            _shineGetCamAP != null ? string.Join(",", _shineGetCamAP.GetAnimationList()) : "(none)";
        GD.Print(
            $"[ShineGet] NEW BUILD ACTIVE — rig={_shineGetCamRig != null}, cam={_shineGetCam != null}, ap={_shineGetCamAP != null} (path={_shineGetCamAP?.GetPath()}), anims=[{animList}]"
        );

        stateOfMario = MarioState.idle;
        lastRecordedState = stateOfMario;
        wasOnFloor = IsOnFloor();

        chestRay = GetNodeOrNull<RayCast3D>("LedgeSensors/ChestRay");
        headRay = GetNodeOrNull<RayCast3D>("LedgeSensors/HeadRay");
        topDownRay = GetNodeOrNull<RayCast3D>("LedgeSensors/TopDownRay");

        if (chestRay != null)
            chestRay.Rotation = Vector3.Zero;
        if (headRay != null)
            headRay.Rotation = Vector3.Zero;
        if (topDownRay != null)
            topDownRay.Rotation = Vector3.Zero;

        if (chestRay == null || headRay == null || topDownRay == null)
        {
            GD.PushError(
                "Missing ledge rays. Expected: LedgeSensors/ChestRay, HeadRay, TopDownRay"
            );
        }
        else
        {
            chestRay.Enabled = true;
            headRay.Enabled = true;
            topDownRay.Enabled = true;
        }
        // Make rays match Mario’s collision mask (so they "see" what Mario can collide with)
        uint m = CollisionMask; // CharacterBody3D's mask

        chestRay.CollisionMask = m;
        headRay.CollisionMask = m;
        topDownRay.CollisionMask = m;

        // optional but recommended:
        chestRay.CollideWithAreas = false;
        headRay.CollideWithAreas = false;
        topDownRay.CollideWithAreas = false;

        // avoid self-hits
        chestRay.AddException(this);
        headRay.AddException(this);
        topDownRay.AddException(this);

        skeleton = GetNode<Node3D>("Armature").GetNode<Skeleton3D>("Skeleton3D");
        // Holds the head forward while moving (cancels the baked head-turn); runs
        // as a skeleton modifier so it applies after the animation.
        if (skeleton != null)
        {
            _headLock = new HeadForwardLock { Name = "HeadForwardLock" };
            skeleton.AddChild(_headLock);

            // Foot IK — plants each foot on a slope independently.
            //
            // PREFER a node placed in Player.tscn: created-in-code nodes never
            // show up in the inspector, so none of FootIK's tunables
            // (MaxHipDrop, HipDropToLowest, AlignFeetToSurface, FootOffset...)
            // would be reachable for tuning. Only fall back to constructing one
            // if the scene doesn't have it, so an older scene still works.
            _footIK = skeleton.GetNodeOrNull<FootIK>("FootIK");
            if (_footIK == null)
            {
                _footIK = new FootIK
                {
                    Name = "FootIK",
                    FootOffset = footOffset,
                    RayUp = ikRaycastHeight,
                };
                skeleton.AddChild(_footIK);
                GD.PushWarning(
                    "Mario: no FootIK node in the scene — created one at runtime. "
                        + "Its exports won't appear in the inspector; add a FootIK "
                        + "child under Armature/Skeleton3D to tune it."
                );
            }

            // Set regardless of which path made it — Mario's own RID isn't
            // knowable from the scene file.
            _footIK.ExcludeBodies = new Godot.Collections.Array<Rid> { GetRid() };

            // Head bobble (jnt_head) — the most visible SMS jiggle. Positional
            // only, so HeadForwardLock keeps owning the head's rotation
            // (look-forward/up).
            _headJiggle = new JiggleBone
            {
                Name = "HeadJiggle",
                BoneName = "jnt_head",
                Debug = false,
            };
            skeleton.AddChild(_headJiggle);

            // Arm jiggle — root at the shoulder, tip at the hand, so the whole
            // arm/sleeve swings as a unit (same positional-shift technique as the
            // head). Arm rotation tracks carry the actual swing animation;
            // position tracks are single-keyframe/static in these clips, so
            // shifting position here doesn't fight the baked animation.
            _armJiggleR = new JiggleBone
            {
                Name = "ArmJiggleR",
                BoneName = "jnt_sldr_R",
                TipBoneName = "jnt_hand_R",
                Debug = false,
            };
            skeleton.AddChild(_armJiggleR);

            _armJiggleL = new JiggleBone
            {
                Name = "ArmJiggleL",
                BoneName = "jnt_sldr_L",
                TipBoneName = "jnt_hand_L",
                Debug = false,
            };
            skeleton.AddChild(_armJiggleL);
        }
        armature = GetNode<Node3D>("Armature");
        _lastArmatureYaw = armature.Rotation.Y; // avoid a false turn-rate spike on the first frame
        animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        hangAnchor = GetNodeOrNull<Node3D>("Armature/HangAnchor");
        groundPoundFx = GetNodeOrNull<GroundPoundEffects>("GroundPoundEffects");
        groundPoundImpactFx = GetNodeOrNull<GroundPoundImpactFx>("GroundPoundImpactFx");

        // Tree-climb anims that should loop while in-state.
        foreach (
            string a in new[]
            {
                "ma_tree_wait",
                "ma_tree_climb",
                "ma_tree_move_l",
                "ma_tree_move_r",
            }
        )
        {
            if (animPlayer.HasAnimation(a))
                animPlayer.GetAnimation(a).LoopMode = Animation.LoopModeEnum.Linear;
        }

        //camControl.Position = Position;
        //camControl.Scale = Scale;
        Console.WriteLine(Scale + " " + Position);

        // Camera rig (paths assume: Mario/CamController/SpringArmPivot/SpringArm3D/Camera3D)
        camControl = GetNodeOrNull<Node3D>("CamController");
        springArmPivot = GetNodeOrNull<Node3D>("CamController/SpringArmPivot");
        springArm = GetNodeOrNull<SpringArm3D>("CamController/SpringArmPivot/SpringArm3D");
        camera = GetNodeOrNull<Camera3D>("CamController/SpringArmPivot/SpringArm3D/Camera3D");

        if (springArmPivot == null)
            GD.PushError(
                "Mario: springArmPivot is NULL. Check node path: CamController/SpringArmPivot"
            );
        if (springArm == null)
            GD.PushError(
                "Mario: springArm is NULL. Check node path: CamController/SpringArmPivot/SpringArm3D"
            );

        // Waist IK
        skeletonIK3DWaist = GetNode<Node3D>("Armature")
            .GetNode<Skeleton3D>("Skeleton3D")
            .GetNode<SkeletonIK3D>("SkeletonIK3D");
        targetWaist = GetNode<Node3D>("Armature/WaistTarget");

        // Start IK
        skeletonIK3DWaist.Stop();

        // Swap in the character body FIRST. SetupSleeping (and anything else that
        // caches a surface material off Mesh_0) must run against the final mesh,
        // or it caches Mario's surfaces and ApplyProfile then replaces the mesh
        // underneath them - which is why Luigi's eyes never closed.
        if (Profile != null)
            ApplyProfile(Profile);

        //Setting up anything related to sleeping
        SetupSleeping();

        //Setting up Hands
        setupHandSwaping();

        setupSpinJumpEffects();
        //ParticleEffects

        // --- LEDGE SENSORS (add this block in _Ready) ---
        ledgeSensors = GetNodeOrNull<Node3D>("LedgeSensors");

        if (ledgeSensors == null)
        {
            GD.PushError("LedgeSensors node NOT found. Check: Mario/LedgeSensors");
        }
        else
        {
            chestRay = GetNodeOrNull<RayCast3D>("LedgeSensors/ChestRay");
            headRay = GetNodeOrNull<RayCast3D>("LedgeSensors/HeadRay");
            topDownRay = GetNodeOrNull<RayCast3D>("LedgeSensors/TopDownRay");

            GD.Print(
                $"[Ledge] chest={(chestRay != null)} head={(headRay != null)} top={(topDownRay != null)}"
            );

            if (chestRay == null)
                GD.PushError("Missing LedgeSensors/ChestRay (RayCast3D)");
            if (headRay == null)
                GD.PushError("Missing LedgeSensors/HeadRay (RayCast3D)");
            if (topDownRay == null)
                GD.PushError("Missing LedgeSensors/TopDownRay (RayCast3D)");
        }

        InitLedgeRays();
        // if you have the animations, grab a reasonable duration
        var hgup = animPlayer.GetAnimation("ma_hgup");
        if (hgup != null)
            ledgeClimbDur = (float)hgup.Length;

        if (hangAnchor == null)
            GD.PushError(
                "Missing Armature/HangAnchor (Node3D). Add it and position it at hands in ma_hang pose."
            );

        armatureBaseLocalPos = armature.Position;
        armatureBaseLocalScale = armature.Scale;

        landingDustFx = GetNodeOrNull<LandingDustFx>("LandingDustFx");
        if (landingDustFx == null)
            GD.PushError("Missing LandingDustFx (Node3D) as a child of Mario.");

        slideDustFx = GetNodeOrNull<SlideDustFx>("SlideDustFx");
        if (slideDustFx == null)
            GD.PushError("Missing SlideDustFx (Node3D) as a child of Mario.");
        GD.Print(Scale);
        pickupCast = GetNodeOrNull<ShapeCast3D>("pickupCast");
        if (pickupCast == null)
            GD.PushError("Missing PickupCast (ShapeCast3D) under Mario.");
        else
            pickupCastInitialLocalPos = pickupCast.Position; // Store initial local position

        carrySocket = GetNodeOrNull<Node3D>("Armature/Skeleton3D/RightHandBone/CarrySocket");
        if (carrySocket == null)
            GD.PushError("Missing CarrySocket under RightHandBone. Move it there.");

        if (pickupCast != null)
        {
            pickupCast.Enabled = true;
            pickupCast.CollideWithBodies = true;
            pickupCast.CollideWithAreas = false;
            pickupCast.AddException(this);
        }
        animPlayer.AnimationFinished += OnAnimationFinished;

        animTree = GetNodeOrNull<AnimationTree>("AnimationTree");
        if (animTree == null)
        {
            GD.PushError("Missing AnimationTree node. Expected path: Mario/AnimationTree");
        }
        else
        {
            // AnimationTree starts inactive - SetMarioState will activate it
            animTree.Active = false;
            animTree.Set(CARRY_BLEND_PATH, 0.0f);
        }
        ForceLoop("ma_run1");
        ForceLoop("ma_run2");
        _tree = GetNode<AnimationTree>("AnimationTree");

        ForceLoop("ma_pivot");
        ForceLoop("ma_sstep");
        ForceLoop("ma_wait");
        ForceLoop("ma_t_wait");
        ForceLoop("ma_sleep_wait");
        ForceLoop("ma_demo_gate_out_rolling_get"); // Triple jump loops

        // Get the StateMachine playback from the AnimationTree
        _sm = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/BaseSM/playback");

        if (_sm == null)
        {
            GD.PushError("❌ AnimationTree StateMachine not found at 'parameters/BaseSM/playback'!");
        }
        else
        {
            GD.Print("✅ AnimationTree successfully initialized!");

            // Enable animation blending for smoother transitions
            if (animTree != null && animTree.TreeRoot is AnimationNodeStateMachine stateMachine)
            {
                // Get all transitions and set blend times
                var transitionCount = stateMachine.GetTransitionCount();
                float blendTime = 0.1f; // 0.1 second blend (adjust to taste)

                for (int i = 0; i < transitionCount; i++)
                {
                    var transition = stateMachine.GetTransition(i);
                    if (transition != null)
                    {
                        // Set crossfade time for smooth blending
                        transition.XfadeTime = blendTime;
                        // Enable crossfade mode (blend between animations)
                        transition.XfadeCurve = null; // Linear blend (or set a curve for custom easing)
                    }
                }

                GD.Print(
                    $"📊 AnimationTree blending configured: {transitionCount} transitions with {blendTime}s blend time"
                );
            }
        }

    }

    /// <summary>
    /// When true, Mario ignores ALL player input. Used by Y-cam over-the-shoulder
    /// mode, NPC talk, and cutscenes (Shine Get). Crucially does NOT zero his
    /// velocity — on the ground he stands still, but in the air he keeps his
    /// trajectory (gravity + momentum) so the SMS Y-turn/Kenny-kick mechanic works.
    /// </summary>
    public bool CameraLocked { get; set; } = false;

    // Prevent the A press that closes dialogue from also becoming a jump/action
    // on the first unlocked physics frame.
    private int _talkingReleaseLockFrames = 0;

    /// <summary>
    /// Snaps Mario to a standing idle pose/animation regardless of what he was
    /// doing. Retained for non-dialogue callers; sign dialogue uses the dedicated
    /// talking state below.
    /// </summary>
    public void ForceStandingIdle()
    {
        stateOfMario = MarioState.idle;
        SetMarioState(MarioState.idle);
    }

    /// <summary>Enter non-controllable sign/NPC dialogue and loop ma_t_wait.</summary>
    public void EnterTalkingState()
    {
        _talkingReleaseLockFrames = 0;
        velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
        stateOfMario = MarioState.talking;
        SetMarioState(stateOfMario);
    }

    /// <summary>Return from dialogue to normal idle control.</summary>
    public void ExitTalkingState()
    {
        if (stateOfMario != MarioState.talking)
            return;

        velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
        stateOfMario = MarioState.idle;
        SetMarioState(stateOfMario);
        _talkingReleaseLockFrames = 2;
    }

    /// <summary>
    /// Fully hides and freezes Mario — used by Level's intro-pan cutscene, where
    /// he hasn't "spawned" yet and shouldn't be visible, processing physics, or
    /// holding the camera. Unlike CameraLocked (which keeps him ticking so he can
    /// still fall/land during a brief shine-get freeze), this stops _Process and
    /// _PhysicsProcess outright on both Mario and his camera rig.
    /// </summary>
    public void SuppressForIntro()
    {
        Visible = false;
        SetProcess(false);
        SetPhysicsProcess(false);
        _camera?.SetProcess(false);
        _camera?.SetPhysicsProcess(false);
    }

    /// <summary>
    /// Reverses SuppressForIntro: optionally places him at the intro's spawn
    /// point, makes him visible and ticking again, and reclaims the camera.
    /// </summary>
    public void RevealFromIntro(Transform3D? spawnTransform = null)
    {
        if (spawnTransform.HasValue)
            GlobalTransform = spawnTransform.Value;

        Visible = true;
        SetProcess(true);
        SetPhysicsProcess(true);
        _camera?.SetProcess(true);
        _camera?.SetPhysicsProcess(true);

        camera?.MakeCurrent();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (slipAirLaunchTimer > 0f)
            slipAirLaunchTimer -= (float)delta;

        // A fresh jump launch vs floor-snap: this is the real cause of "jump
        // gets stuck gliding on a steep ramp" — Godot's floor_snap_length
        // re-glues Mario to the ramp within the SAME MoveAndSlide call before
        // velocity.Y has had one tick to actually separate him from it, so
        // IsOnFloor() never goes false and he just slides along the surface
        // forever in the jump pose. Zero the snap for exactly the frames
        // where he's in a launched-jump state and still (spuriously) reading
        // as grounded; restore it the instant either resolves, so normal
        // ground-sticking (ramps, edges) is unaffected everywhere else.
        bool inJumpLaunchState =
            stateOfMario == MarioState.singleJump
            || stateOfMario == MarioState.doubleJump
            || stateOfMario == MarioState.tripleJump
            || stateOfMario == MarioState.SpinJump
            || stateOfMario == MarioState.backFlip
            || stateOfMario == MarioState.sideFlip;
        FloorSnapLength = (inJumpLaunchState && IsOnFloor()) ? 0f : _baseFloorSnapLength;

        // Sample horizontal acceleration on the fixed physics step for the chest
        // jiggle. Uses last frame's resolved Velocity (post-MoveAndSlide) — clean,
        // and correctly zero when he's cruising at a steady speed.
        {
            Vector3 hv = new Vector3(Velocity.X, 0f, Velocity.Z);
            _marioHorizAccel = (hv - _lastPhysHorizVel) / Mathf.Max((float)delta, 0.0001f);
            _lastPhysHorizVel = hv;
        }

        // Same fix, same reason: armature.Rotation.Y only changes on a physics
        // tick, so its rate must be measured with the FIXED physics delta here,
        // not from _Process's variable render delta (which produces huge spikes
        // on frames that happen to land right after a tick, and exactly zero on
        // frames that don't — the alternating pattern that was masking the lean).
        if (armature != null)
        {
            float yaw = armature.Rotation.Y;
            float yawDelta = Mathf.Wrap(yaw - _lastArmatureYaw, -Mathf.Pi, Mathf.Pi);
            _lastArmatureYaw = yaw;
            _turnYawRateDeg = Mathf.RadToDeg(yawDelta) / Mathf.Max((float)delta, 0.0001f);
        }
        UpdateTurnLean((float)delta);

        if (_talkingReleaseLockFrames > 0)
        {
            _talkingReleaseLockFrames--;
            velocity = Vector3.Zero;
            Velocity = Vector3.Zero;
            wasOnFloor = IsOnFloor();
            return;
        }

        if (CameraLocked)
        {
            bool wasAirPrev = !wasOnFloor;

            if (IsOnFloor())
            {
                // Standing: full freeze, no slide.
                velocity = Vector3.Zero;
                Velocity = Vector3.Zero;
            }
            else
            {
                // While a shine is pending, keep him PURELY vertical — kill any
                // XZ so he drops straight down (he was already snapped to the
                // shine's XZ on contact). Otherwise (normal Y-cam lock) his arc.
                if (_pendingShine != null)
                {
                    velocity.X = 0f;
                    velocity.Z = 0f;
                }
                velocity += GRAVITY * (float)delta;
                if (velocity.Y < -TERMINAL_VELOCITY)
                    velocity.Y = -TERMINAL_VELOCITY;
                Velocity = velocity;
            }
            MoveAndSlide();

            // Pending shine + just landed → start the cutscene now (before the
            // landing-state logic below, which would otherwise claim the frame).
            if (_pendingShine != null && IsOnFloor())
            {
                var pending = _pendingShine;
                _pendingShine = null;
                StartShineGet(pending);
                wasOnFloor = IsOnFloor();
                return;
            }

            // Physics-driven state transition: if we were airborne and now we're on
            // the floor, trigger the landing animation. Player input is locked, but
            // state changes from physical events still happen so the animation
            // system stays in sync with Mario's actual situation.
            bool justLandedFromYCam = IsOnFloor() && wasAirPrev;
            if (
                justLandedFromYCam
                && stateOfMario != MarioState.landing
                && stateOfMario != MarioState.idle
                && stateOfMario != MarioState.singleJumpLanding
                && stateOfMario != MarioState.doubleJumpLanding
                && stateOfMario != MarioState.tripleJumpLanding
                && stateOfMario != MarioState.groundPoundLanding
                && stateOfMario != MarioState.shineGet
                && stateOfMario != MarioState.bonk
                && stateOfMario != MarioState.talking
            )
            {
                stateOfMario = MarioState.landing;
                SetMarioState(MarioState.landing);
            }
            wasOnFloor = IsOnFloor();
            return;
        }

        // A shine touched mid-air waits here, fully controllable, until Mario
        // actually lands — the cutscene shouldn't freeze him out of the sky.
        // Must return immediately: letting the rest of this frame's normal
        // state machine keep running after StartShineGet() would immediately
        // reprocess/overwrite the shineGet state it just set (the same class
        // of bug as the CameraLocked landing-transition stomp fixed earlier).
        if (_pendingShine != null && IsOnFloor())
        {
            var shine = _pendingShine;
            _pendingShine = null;
            StartShineGet(shine);
            return;
        }

        if (heldBody != null && carrySocket != null)
        {
            var gt = carrySocket.GlobalTransform;

            // IMPORTANT: remove socket scale from the transform we apply to the held object
            gt.Basis = gt.Basis.Orthonormalized();

            heldBody.GlobalTransform = gt;

            // optional: enforce local scale too (helps if anything else touches it)
            heldBody.Scale = Vector3.One;
        }
        UpdateCarryRunBlend((float)delta);
        UpdateRunAnimationSpeed();
        if (Input.IsActionJustPressed("button_z") || Input.IsActionJustPressed("key_z"))
            GD.Print(
                $"[DBG] key_z JustPressed | state={stateOfMario} | onFloor={IsOnFloor()} | canCrouch={CanEnterCrouch()}"
            );

        // Backflip: play the roll, then swap to the ledge-fall anim (ma_land) when the
        // roll animation finishes (mirrors SMS backJumping -> ANIM_LAND on last frame).
        if (stateOfMario == MarioState.backFlip)
        {
            _backflipTimer += (float)delta;
            if (!_backflipFalling)
            {
                float rollLen = (float)_sm.GetCurrentLength();
                float rollPos = (float)_sm.GetCurrentPlayPosition();
                bool animDone = (rollLen > 0.001f && rollPos >= rollLen - 0.03f);
                bool safety = (_backflipTimer > 0.8f); // never get stuck on the roll
                if (animDone || safety)
                {
                    _backflipFalling = true;
                    _sm.Travel("ma_land");
                }
            }
        }
        else
        {
            _backflipFalling = false;
            _backflipTimer = 0f;
        }

        // preserve previous-frame floor latch immediately so all early gotos don't affect it
        bool wasOnFloorPrev = wasOnFloor;
        Vector2 LstickVec = Input.GetVector(
            "Lstick_left",
            "Lstick_right",
            "Lstick_up",
            "Lstick_down"
        );
        float stickStrength = LstickVec.Length();

        Vector3 inputDirWorld = Vector3.Zero;
        float camYaw = (springArmPivot != null) ? springArmPivot.Rotation.Y : 0f;
        if (stickStrength > DEADZONE)
        {
            // Build direction in local space, rotate by camera yaw, and scale by strength
            inputDirWorld = (
                Transform.Basis * new Vector3(LstickVec.X, 0, LstickVec.Y)
            ).Normalized();
            inputDirWorld = inputDirWorld.Rotated(Vector3.Up, camYaw);
            inputDirWorld *= stickStrength;
        }
        // Aim direction (works even when stick is under DEADZONE)
        Vector3 aimDirWorld = Vector3.Zero;
        if (stickStrength > 0.001f)
        {
            aimDirWorld = (Transform.Basis * new Vector3(LstickVec.X, 0, LstickVec.Y)).Normalized();
            aimDirWorld = aimDirWorld.Rotated(Vector3.Up, camYaw);
        }

        aimDirThisFrame = aimDirWorld;
        walkingStrength = stickStrength;

        if (ledgeRegrabCooldown > 0)
            ledgeRegrabCooldown--;

        if (_treeRegrabCooldown > 0)
            _treeRegrabCooldown--;

        // Update jump buffer (works in air + on ground)
        if (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))
        {
            jumpBuffer = JUMP_BUFFER_FRAMES;
        }
        else if (jumpBuffer > 0)
        {
            jumpBuffer--;
        }

        if (stateOfMario == MarioState.pickupRaise)
        {
            if (animTree != null)
                animTree.Active = false;
            velocity = Vector3.Zero;

            if (animPlayer.CurrentAnimation != "ma_raise")
                animPlayer.Play("ma_raise");

            goto EndFrame;
        }

        // SMS quirk: while belly-sliding ON THE GROUND from a dive with B HELD,
        // hitting a carryable fruit picks it up instead of bonking. Durian's
        // CanBePickedUp is false so it's automatically excluded. Airborne dive
        // does NOT trigger this — Mario must be on the floor.
        if (
            heldBody == null
            && (
                stateOfMario == MarioState.diving || stateOfMario == MarioState.bellySlidingFromDive
            )
            && IsOnFloor()
            && Input.IsActionPressed("button_b")
            && pickupCast != null
        )
        {
            UpdatePickupCastFacing();
            if (TryFindPickable(out var diveFruit) && diveFruit is Fruit f && f.CanBePickedUp)
            {
                velocity = Vector3.Zero;
                Velocity = Vector3.Zero;
                StartPickupRaise(diveFruit);
                goto EndFrame;
            }
        }

        if (stateOfMario == MarioState.putDown)
        {
            if (animTree != null)
                animTree.Active = false;
            velocity = Vector3.Zero;

            // Keep whichever animation StartThrow / StartPutDown started — ma_throw
            // when throwing, ma_put when setting down. Don't overwrite it.
            string desiredAnim = _isThrowQueued ? "ma_throw" : "ma_put";
            if (!animPlayer.HasAnimation(desiredAnim))
                desiredAnim = "ma_put";
            if (animPlayer.CurrentAnimation != desiredAnim)
                animPlayer.Play(desiredAnim);

            goto EndFrame;
        }

        // Tree climb states are handled before the on-floor/airborne split because
        // Mario can grab a trunk from either condition; standard physics is skipped.
        if (stateOfMario == MarioState.treeGrab)
        {
            TickTreeGrab(delta);
            // Only re-zero if we're STILL grabbing — TickTreeGrab can itself call
            // DoTreeJumpOff() (jump pressed during the catch animation), which sets
            // a real launch velocity and moves state to wallJump.
            if (stateOfMario == MarioState.treeGrab)
            {
                Velocity = Vector3.Zero;
                wasOnFloor = false;
            }
            else
            {
                // We just left the tree. Tree ticks never call MoveAndSlide(), so
                // IsOnFloor() is still stale-true from before the grab. Applying the
                // exit velocity via MoveAndSlide() right NOW (instead of waiting for
                // next tick) makes IsOnFloor() correct immediately — otherwise the
                // grounded-only logic elsewhere runs on the stale flag for one tick,
                // resets isJumping straight back to false, and the "walked off a
                // ledge" detector then misclassifies this jump as an accidental
                // ledge-fall the tick after.
                MoveAndSlide();
                wasOnFloor = false;
            }
            return;
        }
        if (
            stateOfMario == MarioState.treeWait
            || stateOfMario == MarioState.treeClimb
            || stateOfMario == MarioState.treeMoveL
            || stateOfMario == MarioState.treeMoveR
        )
        {
            TickTreeOnTrunk(delta, LstickVec);
            // Same reasoning as above: TickTreeOnTrunk can exit via DoTreeJumpOff(),
            // DoTreeTopReach(), or ExitTreeToFall() (ground/bottom release, or the
            // tree-was-freed case) — all of which set their own correct velocity for
            // leaving the tree. Only zero if we're still in one of the on-trunk
            // states (a normal climb/orbit tick), so we don't clobber a real exit.
            bool stillOnTrunk =
                stateOfMario == MarioState.treeWait
                || stateOfMario == MarioState.treeClimb
                || stateOfMario == MarioState.treeMoveL
                || stateOfMario == MarioState.treeMoveR;
            if (stillOnTrunk)
            {
                Velocity = Vector3.Zero;
                wasOnFloor = false;
            }
            else
            {
                MoveAndSlide(); // establish correct IsOnFloor() this same tick — see above
                wasOnFloor = false;
            }
            return;
        }

        // Debug: press + to heal one segment
        if (Input.IsKeyPressed(Key.Equal) || Input.IsKeyPressed(Key.KpAdd))
        {
            if (_health < StartingHealth)
            {
                _health = Mathf.Min(_health + 1, StartingHealth);
                _lifeMeter?.SetHealth(_health);
                GD.Print($"[DebugHeal] Health: {_health}/{StartingHealth}");
                if (_health == StartingHealth)
                    _lifeMeter?.HideForFullHealth();
            }
        }

        if (stateOfMario == MarioState.dead)
        {
            if (!IsOnFloor())
                velocity += GRAVITY * (float)delta;

            // Press R to respawn
            if (Input.IsKeyPressed(Key.R))
                Respawn();

            goto EndFrame;
        }

        if (stateOfMario == MarioState.hurt)
        {
            if (!IsOnFloor())
                velocity += GRAVITY * (float)delta;

            if (_hurtAirborne && IsOnFloor())
            {
                // Just landed — play ground hit animation, timer = animation length
                _hurtAirborne = false;
                string landAnim = _hitFromFront ? "ma_sldwn" : "ma_sdwnf";
                var anim = animPlayer.GetAnimation(landAnim);
                _hurtTimer = anim != null ? (float)anim.Length : HURT_DURATION;
                velocity.X = 0;
                velocity.Z = 0;
                if (_sm != null)
                    _sm.Travel(landAnim);
            }

            if (!_hurtAirborne)
                _hurtTimer -= (float)delta;

            if (!_hurtAirborne && _hurtTimer <= 0f)
            {
                stateOfMario = MarioState.idle;
                SetMarioState(MarioState.idle);
            }
            goto EndFrame;
        }

        if (stateOfMario == MarioState.stomping)
        {
            if (!IsOnFloor())
                velocity += GRAVITY * (float)delta;

            // Run spin input detection so it works during stomp
            UpdateSpinInput(
                Input.GetVector("Lstick_left", "Lstick_right", "Lstick_up", "Lstick_down")
            );
            if (spinBuffer > 0)
                spinBuffer--;
            spinInput = (spinBuffer > 0);

            // Spin jump cancels stomp and resets chain
            if (spinInput && CanStartSpinInAir())
            {
                _stompChain = 0;
                StartSpinJumpFromAir();
                spinInput = false;
                goto EndFrame;
            }

            // Air control while stomping
            if (TryGetAirProfile(MarioState.stomping, out var stompProf))
                ApplyAirControl(delta, stompProf);

            if (IsOnFloor() && velocity.Y <= 0)
            {
                // Landed on the ground — chain resets, back to landing
                _stompChain = 0;
                stateOfMario = MarioState.landing;
                SetMarioState(MarioState.landing);
            }
            goto EndFrame;
        }

        // --- GROUND POUND ---
        if (stateOfMario == MarioState.groundPoundStartup)
        {
            // Freeze in the air — no gravity, no movement
            velocity = Vector3.Zero;

            // Wait for AnimationPlayer to finish ma_hipsr
            bool animDone = !animPlayer.IsPlaying() || animPlayer.CurrentAnimation != "ma_hipsr";

            if (animDone)
            {
                // Startup done — transition to falling (stall happens there)
                _groundPoundStalling = true;
                _groundPoundStallTimer = groundPoundAirStallDuration;
                stateOfMario = MarioState.groundPoundFalling;
                SetMarioState(MarioState.groundPoundFalling);
            }
            goto EndFrame;
        }

        if (stateOfMario == MarioState.groundPoundFalling)
        {
            velocity.X = 0f;
            velocity.Z = 0f;

            if (_groundPoundStalling)
            {
                // Air stall: frozen in place showing first frame of ma_hipat
                velocity.Y = 0f;
                _groundPoundStallTimer -= (float)delta;
                if (_groundPoundStallTimer <= 0f)
                    _groundPoundStalling = false;
                goto EndFrame;
            }

            // Falling phase — straight down at fixed speed, no air control
            velocity.Y = GROUND_POUND_FALL_SPEED;

            if (IsOnFloor())
            {
                // Landed — enter ground pound landing
                velocity = Vector3.Zero;
                stateOfMario = MarioState.groundPoundLanding;
                SetMarioState(MarioState.groundPoundLanding);

                // Get animation length for the landing timer
                var hipedAnim = animPlayer.GetAnimation("ma_hiped");
                _groundPoundLandingTimer = hipedAnim != null ? (float)hipedAnim.Length : 0.5f;

                TriggerLandingDust();

                // GP impact effects: the FX itself raycasts down to find the real floor.
                groundPoundImpactFx?.Trigger(GlobalPosition);
            }
            goto EndFrame;
        }

        if (stateOfMario == MarioState.groundPoundLanding)
        {
            velocity = Vector3.Zero;

            // GP-jump can fire at ANY time during groundPoundLanding (both the impact
            // ma_hiped phase and the get-up ma_slped phase). Press A → launch.
            if (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))
            {
                _groundPoundSlpedPlaying = false;
                jumpChainStage = 0;
                jumpChainTimer = 0;
                isJumping = true;
                initalJumpHold = false;
                jumpHoldTime = 0f;
                currentJumpVelocity = GroundPoundJumpVelocity;
                stateOfMario = MarioState.singleJump;
                velocity.Y = GroundPoundJumpVelocity;

                _isGroundPoundJump = true;
                _gpJumpStartYaw = armature.Rotation.Y;
                _gpJumpTimer = 0f;
                _gpJumpSpinDuration = Mathf.Max(
                    0.05f,
                    GroundPoundJumpVelocity / Mathf.Abs(GRAVITY.Y)
                );

                SetMarioState(MarioState.singleJump);
                goto EndFrame;
            }

            // Sub-phase: ma_slped is playing after hiped finished with neutral stick
            if (_groundPoundSlpedPlaying)
            {
                // Any input interrupts ma_slped
                bool wantsMove = walkingStrength > DEADZONE;

                _groundPoundSlpedTimer -= (float)delta;
                if (wantsMove || _groundPoundSlpedTimer <= 0f)
                {
                    _groundPoundSlpedPlaying = false;
                    if (wantsMove)
                    {
                        if (walkingStrength > 0.5f)
                        {
                            stateOfMario = MarioState.sprinting;
                            SetMarioState(MarioState.sprinting);
                        }
                        else if (walkingStrength <= SNEAK_MAX_INPUT)
                        {
                            stateOfMario = MarioState.sneak;
                            SetMarioState(MarioState.sneak);
                        }
                        else
                        {
                            stateOfMario = MarioState.running;
                            SetMarioState(MarioState.running);
                        }
                    }
                    else
                    {
                        stateOfMario = MarioState.idle;
                        SetMarioState(MarioState.idle);
                    }
                }
                goto EndFrame;
            }

            _groundPoundLandingTimer -= (float)delta;
            if (_groundPoundLandingTimer <= 0f)
            {
                // Landing animation done — check stick input
                if (walkingStrength <= DEADZONE)
                {
                    // Neutral stick: play ma_slped and stay locked until it finishes
                    _groundPoundSlpedPlaying = true;
                    var slpedAnim = animPlayer.GetAnimation("ma_slped");
                    _groundPoundSlpedTimer = slpedAnim != null ? (float)slpedAnim.Length : 0.5f;
                    if (_sm != null)
                        _sm.Travel("ma_slped");
                }
                else
                {
                    // Stick held: let player interrupt into movement
                    if (walkingStrength > 0.5f)
                    {
                        stateOfMario = MarioState.sprinting;
                        SetMarioState(MarioState.sprinting);
                    }
                    else if (walkingStrength <= SNEAK_MAX_INPUT)
                    {
                        stateOfMario = MarioState.sneak;
                        SetMarioState(MarioState.sneak);
                    }
                    else
                    {
                        stateOfMario = MarioState.running;
                        SetMarioState(MarioState.running);
                    }
                }
            }
            goto EndFrame;
        }

        if (wallKickLock > 0)
            wallKickLock--;
        if (wallRegrabCooldown > 0)
            wallRegrabCooldown--;

        if (!IsOnFloor())
        {
            if (
                stateOfMario != MarioState.wallSlide
                && stateOfMario != MarioState.ledgeHang
                && stateOfMario != MarioState.ledgeClimb
            )
            {
                // Detect running/walking off a ledge without jumping.
                // If the state is still a grounded locomotion state while airborne and Mario
                // didn't jump, he must have walked off an edge.
                bool isGroundedLocoState =
                    stateOfMario == MarioState.idle
                    || stateOfMario == MarioState.sneak
                    || stateOfMario == MarioState.walking
                    || stateOfMario == MarioState.running
                    || stateOfMario == MarioState.sprinting
                    || stateOfMario == MarioState.pivot
                    || stateOfMario == MarioState.sideFlipTurning;
                if (!isJumping && isGroundedLocoState)
                {
                    // If he walks off the edge mid-sideFlipTurning, RotateArmature
                    // never gets called again this tick (it's gated on still being
                    // in that state) — so without this, he'd freeze facing
                    // wherever the yaw-lerp had gotten to (potentially still the
                    // pre-turn direction if he stepped off right as the turn
                    // started). Snap to the turn's target direction immediately,
                    // same as RotateArmature's own "turn finished normally" branch.
                    if (stateOfMario == MarioState.sideFlipTurning)
                    {
                        Vector3 turnDir =
                            (sideFlipStoredDir != Vector3.Zero) ? sideFlipStoredDir
                            : (lastFacingDirection != Vector3.Zero) ? lastFacingDirection
                            : Vector3.Forward;
                        turnDir = turnDir.Normalized();

                        armature.Rotation = new Vector3(0f, YawFromDir(turnDir), 0f);
                        lastFacingDirection = turnDir;
                    }

                    stateOfMario = MarioState.ledgeFall;
                    SetMarioState(MarioState.ledgeFall);
                }
                else if (!isJumping && stateOfMario == MarioState.rolloutRun)
                {
                    // Rolled off a ledge while coasting (rolloutRun isn't a
                    // "normal" locomotion state, so it's deliberately not in
                    // isGroundedLocoState above — routing it to ledgeFall like
                    // the rest would strip his air control entirely). Hand off
                    // to singleRollout instead, which the universal air-control
                    // block already knows how to steer (AIR_SINGLE profile),
                    // so the speed he carried into the roll isn't wasted.
                    stateOfMario = MarioState.singleRollout;
                    SetMarioState(MarioState.singleRollout);
                }

                if (stateOfMario == MarioState.SpinJump)
                {
                    float gScale =
                        (velocity.Y > 0f) ? SPIN_GRAVITY_UP_SCALE : SPIN_GRAVITY_DOWN_SCALE;
                    velocity += GRAVITY * (float)delta * gScale;

                    // Clamp SpinJump fall speed so it never becomes a brick drop
                    if (velocity.Y < -SPIN_TERMINAL_FALL)
                        velocity.Y = -SPIN_TERMINAL_FALL;
                }
                else
                {
                    // Normal gravity for everything else
                    velocity += GRAVITY * (float)delta;

                    // (Optional) global terminal velocity if you want:
                    // if (velocity.Y < -TERMINAL_VELOCITY) velocity.Y = -TERMINAL_VELOCITY;
                }

                // Continuously update double jump animation based on velocity
                if (stateOfMario == MarioState.doubleJump && _aerialThrowLockTimer <= 0f)
                {
                    SetMarioState(stateOfMario);
                }
            }
            var gf = animPlayer.GetAnimation("ma_get_fail");
            if (gf != null)
                pickupFailDur = (float)gf.Length;
        }

        // Waist tilt: lean forward based on speed, disabled while carrying
        bool isRunning = stateOfMario == MarioState.running || stateOfMario == MarioState.sprinting;
        bool isCarrying = heldBody != null;

        if (isRunning && !isCarrying)
        {
            float xzSpeed = new Vector2(velocity.X, velocity.Z).Length();
            float tilt = Mathf.Lerp(90f, 120f, Mathf.Clamp(xzSpeed / RUN_SPEED, 0f, 1f));
            skeletonIK3DWaist.Start();
            // X was always 0 here — that's the one axis this target doesn't
            // already use (Y is a fixed reference, Z is the forward-lean
            // above), so it's where the turn lean (left/right, from
            // UpdateTurnLean) goes. If it leans the wrong way, flip the sign
            // on TurnLeanSensitivity.
            targetWaist.RotationDegrees = new Vector3(_turnLeanCurrentDeg, 90f, tilt);
        }
        else
        {
            skeletonIK3DWaist.Stop();
        }

        if (stateOfMario != MarioState.idle)
        {
            SleepStatus(false);
        }

        // If landing is playing, buffer input instead of applying it
        if (IsLandingState(stateOfMario))
        {
            pendingMoveDir = inputDirWorld;
            pendingMoveStrength = stickStrength;
        }
        else
        {
            direction = inputDirWorld;
        }
        // Only process input if it exceeds the deadzone.
        // --- SPIN INPUT DETECTION (replace the quadrant logic block with this) ---
        // SpinInput should be a ONE-FRAME pulse.
        // Reset it at the start of this frame and let UpdateSpinInput set it.
        UpdateSpinInput(LstickVec);

        if (spinBuffer > 0)
            spinBuffer--;

        spinInput = (spinBuffer > 0);
        if (queuedTouchdownJump)
        {
            queuedTouchdownJump = false;
            StartGroundJumpPreserveXZ(); // consumes jumpBuffer, sets state, sets velocity.Y, etc.
            goto EndFrame; // IMPORTANT: skip ground logic this frame
        }
        // === DIVE BONK ===
        // While bonked: run the bounce/getup sequence and own the frame.
        if (stateOfMario == MarioState.bonk)
        {
            TickBonk(delta);
            goto EndFrame;
        }
        // Dive bonks are detected immediately after MoveAndSlide(), where this
        // frame's wall collisions and the pre-impact velocity are both available.

        // singleRollout / bellyRollout are AIR states that launch from a grounded
        // frame. On a ramp they clip the surface while still RISING — measured
        // live: singleRollout, onFloor=True, velY=+7.97 on a 33.8° ramp, one
        // frame after the roll began. Letting the grounded branch run on that
        // frame spends the still-live jumpBuffer (the very same A press that
        // STARTED the roll, jumpBuffer=4 of 6 remaining) as a generic ground
        // jump, so the rollout turns into a singleJump almost immediately.
        // While he's still going up, he is not grounded in any meaningful sense.
        bool rollingUpwardOffGround =
            (stateOfMario == MarioState.singleRollout || stateOfMario == MarioState.bellyRollout)
            && velocity.Y > ROLLOUT_LAND_MAX_RISE;

        if (IsOnFloor() && !rollingUpwardOffGround) //This is the main logic loop for Player being on the ground
        {
            // === GROUNDED SPIN ===
            // Already grounded-spinning: keep spinning in place, damp motion,
            // and allow a spin-jump (A) in any direction at any time.
            if (stateOfMario == MarioState.groundSpin)
            {
                groundSpinTimer -= (float)delta;

                // Visual spin (same rate as the aerial SpinJump).
                float gsDeg = GroundSpinDegPerSec * (float)delta * (Mathf.Pi / 180f);
                armature.RotateObjectLocal(Vector3.Up, gsDeg);

                // Steerable while spinning: move in the stick direction.
                Vector2 gsCur = new Vector2(velocity.X, velocity.Z);
                Vector2 gsTarget = Vector2.Zero;
                if (stickStrength > 0.1f && inputDirWorld != Vector3.Zero)
                {
                    Vector3 gsMd = inputDirWorld.Normalized();
                    gsTarget = new Vector2(gsMd.X, gsMd.Z) * GroundSpinMoveSpeed * stickStrength;
                }
                Vector2 gsNew = gsCur.MoveToward(gsTarget, 80f * (float)delta);
                velocity.X = gsNew.X;
                velocity.Z = gsNew.Y;
                velocity.Y = -2f;

                if (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))
                {
                    // Held direction -> jump that way; neutral -> straight up.
                    if (stickStrength > 0.2f && inputDirWorld != Vector3.Zero)
                    {
                        Vector3 gd = inputDirWorld.Normalized();
                        velocity.X = gd.X * RUN_SPEED;
                        velocity.Z = gd.Z * RUN_SPEED;
                    }
                    else
                    {
                        velocity.X = 0f;
                        velocity.Z = 0f;
                    }
                    StartSpinJump();
                    goto EndFrame;
                }

                if (groundSpinTimer <= 0f)
                {
                    isSpining(false);
                    if (footSparks != null)
                        footSparks.Emitting = false;
                    stateOfMario = MarioState.idle;
                    SetMarioState(stateOfMario);
                }
                goto EndFrame;
            }

            // Enter grounded spin: an active spin input + tap R.
            if (spinBuffer > 0 && Input.IsActionJustPressed("button_r") && CanEnterGroundSpin())
            {
                StartGroundSpin();
                goto EndFrame;
            }

            // === CROUCH / BACKFLIP ===
            // Holding Z on the ground puts Mario in a squat; A launches a backflip.
            if (stateOfMario == MarioState.crouch)
            {
                velocity.X = Mathf.MoveToward(velocity.X, 0f, 60f * (float)delta);
                velocity.Z = Mathf.MoveToward(velocity.Z, 0f, 60f * (float)delta);
                velocity.Y = -2f;

                if (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))
                {
                    StartBackflip();
                    goto EndFrame;
                }
                if (!Input.IsActionPressed("button_z") && !Input.IsActionPressed("key_z"))
                {
                    stateOfMario = MarioState.idle;
                    SetMarioState(stateOfMario);
                }
                goto EndFrame;
            }

            // Enter crouch stance: hold Z on the ground.
            if (
                (Input.IsActionPressed("button_z") || Input.IsActionPressed("key_z"))
                && CanEnterCrouch()
            )
            {
                StartCrouch();
                goto EndFrame;
            }

            if (jumpChainTimer > 0)
            {
                jumpChainTimer--;
                if (jumpChainTimer == 0)
                    jumpChainStage = 0;
            }
            if (stateOfMario == MarioState.rolloutRun)
            {
                // If this returns true, we want rolloutRun to fully own this frame.
                // If it returns false, we transitioned out -> let normal ground logic run.
                if (HandleRolloutRun(delta, inputDirWorld, stickStrength))
                    goto EndFrame;
            }

            // If we're currently doing pickupFail, let it fully own the frame.
            if (stateOfMario == MarioState.pickupFail)
            {
                if (TickPickupFail(delta, inputDirWorld, stickStrength))
                    goto EndFrame;
            }

            // --- WALL PUSH / WALL SHUFFLE (ground) ---
            if (stateOfMario == MarioState.wallPush || stateOfMario == MarioState.wallShuffle)
            {
                if (TickGroundWall(delta, inputDirWorld, stickStrength))
                    goto EndFrame;
            }

            if (Input.IsActionJustPressed("button_b"))
            {
                Console.WriteLine(
                    stickStrength + " " + new Vector2(velocity.X, velocity.Z).Length()
                );
                bool wantsDive =
                    (stickStrength > 0.75f)
                    || (new Vector2(velocity.X, velocity.Z).Length() > 4.5f);

                if (
                    wantsDive
                    && heldBody == null // can't dive while carrying — falls through to throw/putdown below
                    && stateOfMario != MarioState.sideFlipTurning
                    && stateOfMario != MarioState.wallPush
                    && stateOfMario != MarioState.wallShuffle
                )
                {
                    // DIVE (ground)
                    stateOfMario = MarioState.diving;
                    velocity.Y = DIVE_POP_Y;
                    SetMarioState(stateOfMario);

                    Vector3 diveDir =
                        (lastFacingDirection != Vector3.Zero)
                            ? lastFacingDirection.Normalized()
                            : Vector3.Forward;
                    velocity.X = diveDir.X * DIVE_SPEED_XZ;
                    velocity.Z = diveDir.Z * DIVE_SPEED_XZ;
                    goto EndFrame;
                }

                // otherwise: pickup / putdown (not during sideFlipTurning or wall states)
                if (
                    stateOfMario != MarioState.sideFlipTurning
                    && stateOfMario != MarioState.wallPush
                    && stateOfMario != MarioState.wallShuffle
                )
                {
                    if (heldBody != null && stateOfMario != MarioState.putDown)
                    {
                        // SMS-style: running/intending-to-move B = instant throw (fruit
                        // leaves hand at 45°, Mario keeps running unaffected);
                        // standing-still B = gentle put-down.
                        float horizSpeed = new Vector2(velocity.X, velocity.Z).Length();
                        bool wantsThrow = horizSpeed > 3.0f || stickStrength > 0.4f;
                        if (wantsThrow)
                            StartThrow(inputDirWorld);
                        else
                            StartPutDown();
                        goto EndFrame;
                    }

                    if (heldBody == null && stateOfMario != MarioState.pickupRaise)
                    {
                        UpdatePickupCastFacing(); // Rotate pickup cast with Mario
                        if (TryFindPickable(out var rb))
                            StartPickupRaise(rb);
                        else
                            EnterPickupFail();
                        goto EndFrame;
                    }
                }
            }

            // Pivot: tiny stick input -> face direction, don't move
            if (HandlePivot(delta, aimDirWorld, stickStrength))
                goto EndFrame;

            //This keeps it so player is not able to do double jump when pressing A twice in the air
            //when player lets go of A this will be false untill the ground is hit again
            initalJumpHold = true;

            //This will not be here when grounded spin is implemented
            isSpining(false);
            isJumping = false;
            jumpHoldTime = 0.0f;
            currentJumpVelocity = BASE_JUMP_VELOCITY;
            //This first check is locking mario into the path he is commiting to when jumping
            if (
                stateOfMario == MarioState.landing
                || stateOfMario == MarioState.singleJumpLanding
                || stateOfMario == MarioState.doubleJumpLanding
                || stateOfMario == MarioState.tripleJumpLanding
            )
            {
                if (IsLandingState(stateOfMario))
                {
                    // Keep Mario grounded + prevent re-land jitter
                    velocity.Y = 0f;

                    // Keep moving in the direction you landed with (decays with friction)
                    landingCarryVelXZ = landingCarryVelXZ.Lerp(Vector2.Zero, LANDING_FRICTION);
                    velocity.X = landingCarryVelXZ.X;
                    velocity.Z = landingCarryVelXZ.Y;

                    // Allow jump-cancel out of landing (continues jump chain). Uses
                    // the jump buffer instead of a same-frame IsActionJustPressed
                    // check — a press landing on the exact frame EnterLanding() was
                    // called (or a frame either side of it) could otherwise be
                    // missed entirely, eating the input and reading as "jump doesn't
                    // work" right when he's most likely to be pressing it (landing
                    // from a bounce/launch while already trying to keep moving).
                    if (!landingJumpConsumed && jumpBuffer > 0)
                    {
                        landingJumpConsumed = true;
                        jumpBuffer = 0;
                        StartJumpFromLanding();
                        goto EndFrame;
                    }
                    // Landing is still “locked” for a few frames
                    if (landingTimer > 0)
                    {
                        landingTimer--;

                        // If you want: allow jump/dive cancels during landing lock, put them here.
                        // Otherwise do nothing and let buffering keep updating pendingMoveDir.
                    }
                    else
                    {
                        if (_sideFlipLanding)
                        {
                            _sideFlipLanding = false;

                            // landingFacingDir is already locked in EnterLanding()
                            Vector3 face =
                                (landingFacingDir != Vector3.Zero)
                                    ? landingFacingDir
                                    : lastFacingDirection;
                            if (face == Vector3.Zero)
                                face = Vector3.Forward;

                            armature.Rotation = new Vector3(0f, YawFromDir(face), 0f);
                            sideFlipLockedYaw = 0f; // optional, keeps things clean
                        }
                        // Landing finished -> consume buffered input once and pick next state
                        Vector3 bufferedDir = pendingMoveDir;
                        float bufferedStrength = pendingMoveStrength;

                        // Clear buffer now that we used it
                        pendingMoveDir = Vector3.Zero;
                        pendingMoveStrength = 0f;

                        // If player is holding strong opposite direction after landing -> sideFlipTurning
                        if (bufferedStrength > 0.7f && bufferedDir != Vector3.Zero)
                        {
                            Vector3 desiredFacing = bufferedDir.Normalized();
                            if (TurnExceedsAngle(landingFacingDir, desiredFacing, 120f))
                            {
                                EnterSideFlipTurning(desiredFacing);
                                lastFacingDirection = desiredFacing;
                                velocity.X = desiredFacing.X * RUN_SPEED;
                                velocity.Z = desiredFacing.Z * RUN_SPEED;

                                goto EndFrame;
                            }
                        }

                        // Otherwise transition normally based on buffered input
                        direction = bufferedDir;
                        walkingStrength = bufferedStrength;
                        bool usingCarryTree = ShouldUseCarryRunTree();
                        if (
                            stateOfMario == MarioState.wallPush
                            || stateOfMario == MarioState.wallShuffle
                        )
                        {
                            // wall states own their animation/state — don't override
                        }
                        else if (direction == Vector3.Zero)
                        {
                            stateOfMario = MarioState.idle;
                            animPlayer.Play("ma_wait");
                            SetMarioState(stateOfMario);
                        }
                        else if (walkingStrength > .5f)
                        {
                            stateOfMario = MarioState.sprinting;
                            SetMarioState(stateOfMario);
                        }
                        else if (walkingStrength <= SNEAK_MAX_INPUT)
                        {
                            stateOfMario = MarioState.sneak;
                            SetMarioState(stateOfMario);
                        }
                        else
                        {
                            stateOfMario = MarioState.running;
                            SetMarioState(stateOfMario);
                        }

                        goto EndFrame;
                        ; // IMPORTANT: don't continue into the rest of your ground logic this frame
                    }

                    goto EndFrame;
                    ; // IMPORTANT: landing state handled entirely here
                }
                else if (landingTimer != 0)
                {
                    // IMPORTANT: never use stateHistory.Contains for chaining anymore
                    if (Input.IsActionJustPressed("button_a"))
                    {
                        // Use the current chain (only if timer is active)
                        int stage = (jumpChainTimer > 0) ? jumpChainStage : 0;

                        // consume chain on jump
                        jumpChainStage = 0;
                        jumpChainTimer = 0;

                        isJumping = true;

                        if (spinBuffer > 0)
                        {
                            spinBuffer = 0;
                            StartSpinJump();
                        }
                        else if (stage == 2)
                        {
                            spinBuffer = 0;
                            spinInput = false;
                            spinTracking = false;
                            stateOfMario = MarioState.tripleJump;
                            velocity.Y = TRIPLE_JUMP_VELOCITY;
                            SetMarioState(stateOfMario);
                        }
                        else if (stage == 1)
                        {
                            stateOfMario = MarioState.doubleJump;
                            velocity.Y = BASE_JUMP_VELOCITY;
                            SetMarioState(stateOfMario);
                        }
                        else
                        {
                            stateOfMario = MarioState.singleJump;
                            velocity.Y = BASE_JUMP_VELOCITY;
                            SetMarioState(stateOfMario);
                        }

                        goto EndFrame;
                    }
                    if (Input.IsActionJustPressed("button_b"))
                    {
                        velocity.Y = 0;
                        velocity.Y += DIVE_POP_Y;
                        stateOfMario = MarioState.diving;
                        SetMarioState(stateOfMario);

                        // Set a fixed dive speed instead of interpolating
                        Vector3 diveDirection = lastFacingDirection.Normalized();

                        velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                        velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;
                    }
                }

                landingTimer--;
            }
            else
            {
                if (stateOfMario == MarioState.gettingUpFromSliding)
                {
                    gettingUpFromSlidingTimer--;
                    //Play this animation but if the player does any input then let other animations override.
                    //maybe use timer or learn about timmers and how to handle?
                    // Lost animation - keeping AnimationPlayer for special animation
                    SetMarioState(MarioState.gettingUpFromSliding);
                    if (gettingUpFromSlidingTimer == 0)
                    {
                        gettingUpFromSlidingTimer = 53;
                        stateOfMario = MarioState.idle;
                        animPlayer.Play("ma_wait");
                        TriggerLandingDust();
                    }
                }
                else
                {
                    if (stateOfMario == MarioState.bellySlidingFromDive)
                    {
                        bellyRolloutTimer = 20;

                        // Deliberately terrain-blind — belly slide ignores slope
                        // direction/steepness entirely, same flat friction regardless
                        // of what he's sliding over. Staying attached to steep ground
                        // is handled globally via floor_max_angle/floor_snap_length,
                        // not by redirecting his velocity here.
                        velocity.X = Mathf.Lerp(velocity.X, 0, .05f);
                        velocity.Z = Mathf.Lerp(velocity.Z, 0, .05f);

                        // Dust trail behind him as he slides along the ground.
                        Vector3 floorN = IsOnFloor() ? GetFloorNormal().Normalized() : Vector3.Up;
                        slideDustFx?.Emit(ComputeFeetPosition(), floorN);
                        if (
                            velocity.X < 0.1
                            && velocity.Z < 0.1
                            && velocity.X > -0.1
                            && velocity.Z > -0.1
                        )
                        {
                            velocity.X = 0;
                            velocity.Z = 0;
                            stateOfMario = MarioState.gettingUpFromSliding;
                        }
                        if (Input.IsActionPressed("button_a") && checkSpeedForBellyRoll(velocity))
                        {
                            stateOfMario = MarioState.bellyRollout;
                            velocity.Y = GETTING_UP_FROM_SLIDE_ROLL;
                            SetMarioState(stateOfMario);
                        }
                        else if (Input.IsActionPressed("button_a"))
                        {
                            stateOfMario = MarioState.singleRollout;
                            // Roll jump - special animation, keeping AnimationPlayer
                            SetMarioState(stateOfMario);
                            StartRolloutXZ();
                            rolloutAction(delta);
                            // velocity.X = direction.X DIVE_SPEED_XZ;
                            // velocity.Z = direction.Z DIVE_SPEED_XZ;
                        }
                        else if (Input.IsActionJustPressed("button_b"))
                        {
                            stateOfMario = MarioState.diving;
                            RotateArmature();
                            velocity.Y = 0;
                            velocity.Y += DIVE_POP_Y;
                            SetMarioState(stateOfMario);
                            Vector3 diveDirection = lastFacingDirection.Normalized();
                            velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                            velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;
                        }
                    }
                    else
                    {
                        if (
                            stateOfMario == MarioState.diving
                            || stateOfMario == MarioState.singleJumpDive
                            || stateOfMario == MarioState.doubleJumpDive
                            || stateOfMario == MarioState.tripleJumpDive
                        )
                        {
                            SetMarioState(stateOfMario);
                            stateOfMario = MarioState.bellySlidingFromDive;
                        }
                        else
                        {
                            // Setting Mario to be idle
                            if (
                                stateOfMario != MarioState.sideFlipTurning
                                && direction == Vector3.Zero
                                && !isJumping
                                && sideFlipTurningTimer > 32
                            )
                            {
                                stateOfMario = MarioState.idle;
                            }

                            // Rollout slope boost. Must run before groundSpeed is
                            // read — the Lerp below is 0.5/frame, so the cap it
                            // targets IS his speed within about two frames and
                            // there's no surplus that could survive on its own.
                            UpdateRolloutSlopeBoost(delta);

                            //this is the running need to apply the strength of the stick
                            float groundSpeed =
                                (stateOfMario == MarioState.sneak)
                                    ? SNEAK_SPEED
                                        * Mathf.Clamp(
                                            (walkingStrength - DEADZONE)
                                                / (SNEAK_MAX_INPUT - DEADZONE),
                                            0f,
                                            1f
                                        )
                                    // Sneak deliberately excluded — a boosted tiptoe
                                    // would look wrong and reads as a bug.
                                    : RUN_SPEED + _rolloutSlopeBoost;
                            velocity.X = Mathf.Lerp(velocity.X, direction.X * groundSpeed, .5f);
                            velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * groundSpeed, .5f);

                            if (direction != Vector3.Zero)
                            {
                                idleTimer = 66800;

                                if (
                                    stateOfMario != MarioState.sideFlipTurning
                                    && stateOfMario != MarioState.sideFlip
                                )
                                    lastFacingDirection = direction.Normalized();
                                // Don't try to detect a new sideFlipTurning while already turning/flipping
                                if (
                                    stateOfMario == MarioState.sideFlipTurning
                                    || stateOfMario == MarioState.sideFlip
                                )
                                    goto AfterSideFlipDetect;
                                foreach (var lastFacing in Last4FacingDirections.list)
                                {
                                    // Make sure we don't compare zero-length vectors (if that’s possible in your code)
                                    if (
                                        lastFacing != Vector3.Zero
                                        && lastFacingDirection != Vector3.Zero
                                    )
                                    {
                                        // Dot product of two normalized vectors = cos(angle).
                                        // If dot < 0, angle > 90°.
                                        float dot =
                                            (lastFacing.X * lastFacingDirection.X)
                                            + (lastFacing.Y * lastFacingDirection.Y)
                                            + (lastFacing.Z * lastFacingDirection.Z);

                                        if (dot < -0.5f)
                                        {
                                            if (spinBuffer > 0 || spinInput)
                                                continue;

                                            if (
                                                walkingStrength > 0.7
                                                && stateOfMario != MarioState.singleJump
                                            )
                                            {
                                                GD.Print("ENTER sideFlipTurning");

                                                // NEW DIR = the direction the player is pushing RIGHT NOW (this is what we want to face)
                                                Vector3 newDir = direction;
                                                if (newDir == Vector3.Zero)
                                                    newDir = lastFacingDirection;
                                                if (newDir == Vector3.Zero)
                                                    newDir = Vector3.Forward;

                                                newDir = newDir.Normalized();

                                                // Enter turning state once
                                                stateOfMario = MarioState.sideFlipTurning;
                                                sideFlipTurningTimer = 33;

                                                // Store + lock to NEW direction
                                                sideFlipStoredDir = newDir;
                                                sideFlipLockedYaw =
                                                    YawFromDir(newDir) + SIDEFLIP_YAW_OFFSET;
                                                sideFlipDirLocked = true;

                                                // Snap facing immediately
                                                armature.Rotation = new Vector3(
                                                    0f,
                                                    sideFlipLockedYaw,
                                                    0f
                                                );

                                                lastFacingDirection = newDir;

                                                // Play once (don’t restart every frame)
                                                if (animPlayer.CurrentAnimation != "ma_trned")
                                                    SetMarioState(MarioState.sideFlipTurning);
                                            }
                                        }
                                    }
                                }
                                AfterSideFlipDetect:
                                ;
                                if (
                                    stateOfMario == MarioState.landing
                                    || stateOfMario == MarioState.singleJumpLanding
                                    || stateOfMario == MarioState.doubleJumpLanding
                                    || stateOfMario == MarioState.tripleJumpLanding
                                )
                                {
                                    //do not rotate mario landing animation
                                }
                                else
                                {
                                    //only want to rotate if the sideflip turning is happening NOTHING ELSE
                                    if (stateOfMario == MarioState.sideFlipTurning)
                                    {
                                        RotateArmature();
                                    }
                                }

                                if (
                                    Input.IsActionJustPressed("button_b")
                                    && stateOfMario != MarioState.sideFlipTurning
                                )
                                {
                                    stateOfMario = MarioState.diving;
                                    velocity.Y = 0;
                                    velocity.Y += DIVE_POP_Y;
                                    SetMarioState(stateOfMario);
                                    Vector3 diveDirection = lastFacingDirection.Normalized();
                                    velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                                    velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;

                                    goto EndFrame;
                                }
                                if (stateOfMario == MarioState.diving)
                                {
                                    // do nothing - diving owns state/anim
                                }
                                else if (stateOfMario == MarioState.sideFlipTurning)
                                {
                                    if (animPlayer.CurrentAnimation != "ma_trned")
                                        SetMarioState(MarioState.sideFlipTurning);
                                }
                                else if (
                                    stateOfMario == MarioState.wallPush
                                    || stateOfMario == MarioState.wallShuffle
                                )
                                {
                                    // wall states own their animation/state — don't override
                                }
                                else if (
                                    stateOfMario == MarioState.singleJump
                                    || stateOfMario == MarioState.doubleJump
                                    || stateOfMario == MarioState.tripleJump
                                    || stateOfMario == MarioState.SpinJump
                                    || stateOfMario == MarioState.backFlip
                                    || stateOfMario == MarioState.sideFlip
                                )
                                {
                                    // Just launched — IsOnFloor() can stay true for a tick or
                                    // two after a jump trigger (steep terrain, floor snap,
                                    // corner of a rotating platform), which re-enters this
                                    // grounded classification block before he's visibly left
                                    // the ground. Without this guard it unconditionally
                                    // stomped stateOfMario back to sprinting/running/sneak
                                    // based on current stick input, which read as "jump
                                    // instantly reverts to running" even though the jump
                                    // itself triggered correctly. Let gravity/air logic own
                                    // the state from here instead.
                                }
                                else if (walkingStrength > .5f)
                                {
                                    stateOfMario = MarioState.sprinting;
                                    SetMarioState(stateOfMario);
                                    // Hands are driven by UpdateHandPose() now.
                                    // This used to swap them to closed inline and
                                    // nothing ever swapped them back, so they
                                    // stayed clenched for the rest of the run.
                                }
                                else if (walkingStrength <= SNEAK_MAX_INPUT)
                                {
                                    stateOfMario = MarioState.sneak;
                                    SetMarioState(stateOfMario);
                                }
                                else
                                {
                                    stateOfMario = MarioState.running;
                                    SetMarioState(stateOfMario);
                                }

                                if (
                                    stateOfMario == MarioState.sneak
                                    || stateOfMario == MarioState.running
                                    || stateOfMario == MarioState.sprinting
                                )
                                {
                                    RotateArmature();
                                }
                            }
                            else
                            {
                                if (stateOfMario == MarioState.sideFlipTurning)
                                {
                                    // keep anim alive
                                    if (animPlayer.CurrentAnimation != "ma_trned")
                                        SetMarioState(MarioState.sideFlipTurning);

                                    // IMPORTANT: still tick down the turning timer and allow exit
                                    RotateArmature();

                                    // optional: kill drift while turning with neutral stick
                                    velocity.X = Mathf.Lerp(velocity.X, 0f, 0.35f);
                                    velocity.Z = Mathf.Lerp(velocity.Z, 0f, 0.35f);

                                    goto EndFrame; // prevent other idle/landing logic from fighting it
                                }
                                else
                                {
                                    if (suppressLandingFrames > 0)
                                    {
                                        suppressLandingFrames--;
                                    }
                                    else
                                    {
                                        if (stateOfMario == MarioState.idle)
                                        {
                                            // Don't sleep if carrying an object
                                            if (heldBody != null)
                                            {
                                                // Just play normal idle with carry blend
                                                SetMarioState(stateOfMario);
                                            }
                                            // Don't doze off on a slope — the sit/sleep
                                            // clips lay him down flat, so on an incline he
                                            // sits straight through the surface. Same shape
                                            // as the carrying guard above: hold normal idle
                                            // and reset the wind-down so it starts fresh
                                            // once he's back on level ground.
                                            else if (OnSleepBlockingSlope())
                                            {
                                                SleepStatus(false); // also resets every sleep timer
                                                SetMarioState(stateOfMario);
                                            }
                                            else if (idleTimer == 0)
                                            {
                                                if (sittingAnimationTimer == 0)
                                                {
                                                    if (sittingTimerWait == 0)
                                                    {
                                                        if (sleepingTimer == 0)
                                                        {
                                                            // sleep_wait - use AnimationPlayer, not AnimationTree
                                                            if (animTree != null)
                                                                animTree.Active = false;

                                                            if (
                                                                animPlayer.CurrentAnimation
                                                                != "ma_sleep_wait"
                                                            )
                                                            {
                                                                animPlayer.Play("ma_sleep_wait");
                                                                Console.WriteLine(
                                                                    animPlayer.CurrentAnimation
                                                                        + " as;lkd"
                                                                        + animPlayer.CurrentAnimationPosition
                                                                );
                                                            }
                                                            if (sleeptimer == 0)
                                                            {
                                                                //spawn in z every few seconds
                                                                zEffectSpawner.StartZEffect();
                                                                // Apply the updated material back to Surface 7

                                                                sleeptimer = 150;
                                                            }
                                                            else
                                                            {
                                                                sleeptimer--;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // ma_sleep - use AnimationPlayer, not AnimationTree
                                                            if (animTree != null)
                                                                animTree.Active = false;

                                                            sleepingTimer--;
                                                            animPlayer.Play("ma_sleep");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Sitting animations - use AnimationPlayer
                                                        if (animTree != null)
                                                            animTree.Active = false;

                                                        sittingTimerWait--;
                                                        animPlayer.Play("ma_sit_wait");
                                                        SleepStatus(true);
                                                    }
                                                }
                                                else
                                                {
                                                    // Sitting animation - use AnimationPlayer
                                                    if (animTree != null)
                                                        animTree.Active = false;

                                                    sittingAnimationTimer--;
                                                    animPlayer.Play("ma_sit");
                                                }
                                            }
                                            else
                                            {
                                                idleTimer--;
                                                if (animPlayer.CurrentAnimation != ("ma_laend"))
                                                {
                                                    SetMarioState(stateOfMario);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (jumpBuffer > 0)
                            {
                                // Buffered instead of IsActionJustPressed — a brief
                                // IsOnFloor() flicker (steep terrain, corner of a
                                // rotating platform) can route the exact frame of the
                                // press into the airborne branch instead of this one;
                                // IsActionJustPressed never fires again once back on
                                // floor, silently eating the jump. jumpBuffer is set
                                // from a top-of-frame check that runs unconditionally
                                // every tick regardless of branch, and persists for a
                                // few frames, so the next grounded frame still catches it.
                                jumpBuffer = 0;
                                jumpChainStage = 0;
                                jumpChainTimer = 0;
                                isJumping = true;
                                _jumpDbgFrames = 40;

                                // 1) SPIN WINS
                                if (spinBuffer > 0)
                                {
                                    spinBuffer = 0;
                                    StartSpinJump();
                                    goto EndFrame;
                                }
                                // 2) then sideflip if you're in the turning setup
                                else if (stateOfMario == MarioState.sideFlipTurning)
                                {
                                    // Use the STORED dir you captured at reversal time
                                    Vector3 lockDir =
                                        (sideFlipStoredDir != Vector3.Zero)
                                            ? sideFlipStoredDir
                                            : GetMoveDirOrFallback();
                                    lockDir = lockDir.Normalized();

                                    sideFlipTakeoffDir = lockDir;
                                    sideFlipLockedYaw = YawFromDir(lockDir) + SIDEFLIP_YAW_OFFSET;
                                    sideFlipLockedYaw = Mathf.Wrap(
                                        sideFlipLockedYaw,
                                        -Mathf.Pi,
                                        Mathf.Pi
                                    );
                                    sideFlipDirLocked = true;

                                    armature.Rotation = new Vector3(0f, sideFlipLockedYaw, 0f);

                                    lastFacingDirection = lockDir;

                                    // optional: align momentum with flip dir
                                    float carry = new Vector2(velocity.X, velocity.Z).Length();
                                    float spd = Mathf.Max(carry, RUN_SPEED * 0.85f);
                                    velocity.X = lockDir.X * spd;
                                    velocity.Z = lockDir.Z * spd;

                                    stateOfMario = MarioState.sideFlip;
                                    velocity.Y = SIDEFLIP_JUMP_VELOCITY;
                                    SetMarioState(stateOfMario);

                                    goto EndFrame;
                                }
                                // 3) otherwise normal jump
                                else
                                {
                                    stateOfMario = MarioState.singleJump;
                                    velocity.Y = BASE_JUMP_VELOCITY;
                                    SetMarioState(stateOfMario);
                                    goto EndFrame;
                                }
                            }
                        }
                    }
                }
            }
        }
        else // Airborne
        {
            // Tick down the post-aerial-throw dive-block while still in air.
            if (_aerialThrowLockTimer > 0f)
                _aerialThrowLockTimer -= (float)delta;

            // Carrying intercept: B in air must NOT dive — instead throw the held
            // object. This runs before any dive triggers downstream check button_b.
            if (heldBody != null && Input.IsActionJustPressed("button_b"))
            {
                GD.Print(
                    $"[Throw] Airborne intercept fired — state={stateOfMario}, vel={velocity}"
                );
                ThrowHeldInstant(inputDirWorld);
                goto EndFrame;
            }

            // While the aerial throw anim is still playing, swallow any B input so
            // the dive blocks below don't replace ma_throw with ma_sldct.
            if (_aerialThrowLockTimer > 0f && Input.IsActionPressed("button_b"))
            {
                goto EndFrame;
            }

            // If we somehow reach here with B just pressed (intercept didn't fire),
            // log it so we can find the leak.
            if (Input.IsActionJustPressed("button_b"))
            {
                GD.Print(
                    $"[Throw] WARNING: airborne B press got past intercept (heldBody={(heldBody == null ? "null" : "set")}, state={stateOfMario})"
                );
            }

            if (stateOfMario == MarioState.ledgeHang)
            {
                TickLedgeHang(delta, LstickVec);
                Velocity = Vector3.Zero;

                // IMPORTANT: we skipped MoveAndSlide, so keep floor latch accurate
                wasOnFloor = false;

                return;
            }

            if (stateOfMario == MarioState.ledgeClimb)
            {
                bool finished = TickLedgeClimb(delta);
                Velocity = Vector3.Zero;

                // Key: if finished, we are already "on floor" for latch purposes
                wasOnFloor = finished;

                return;
            }

            if (stateOfMario == MarioState.ledgeHopUp)
            {
                // hop-up behaves like airborne after the initial impulse
                // (gravity already applies because we don't skip it for ledgeHopUp)
            }

            // try to START a ledge grab if we're not already doing ledge stuff
            if (TryStartLedgeGrab())
            {
                // snap + freeze immediately, and skip MoveAndSlide this frame
                velocity = Vector3.Zero;
                Velocity = Vector3.Zero;
                return;
            }

            // --- WALL SLIDE / WALL JUMP ---
            if ((stateOfMario != MarioState.wallJump || wallKickLock <= 0) && CanStartWallSlide())
            {
                if (stateOfMario != MarioState.wallSlide)
                    EnterWallSlide(velocity); // <-- pass current velocity
            }

            if (stateOfMario == MarioState.wallSlide)
            {
                TickWallSlide(delta);
                goto EndFrame;
            }

            // --- SLIP (steep ground too shallow to be a real wall) ---
            if (CanStartSlip())
            {
                if (stateOfMario != MarioState.slipping)
                    EnterSlip(velocity);
            }

            if (stateOfMario == MarioState.slipping)
            {
                TickSlip(delta, inputDirWorld, stickStrength);
                goto EndFrame;
            }

            if (stateOfMario == MarioState.wallJump)
            {
                // Allow dive out of wall jump (like in Super Mario Sunshine)
                if (Input.IsActionJustPressed("button_b"))
                {
                    stateOfMario = MarioState.diving;
                    SetMarioState(stateOfMario);

                    // Dive in the direction Mario is facing
                    Vector3 diveDirection = lastFacingDirection.Normalized();
                    velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                    velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;

                    // Reset wall jump flags
                    wallJumpAnimFrozen = false;
                    wallKickLock = 0;
                    goto EndFrame;
                }
            }

            if (stateOfMario == MarioState.wallJump)
            {
                // DO NOT replay every frame.
                // Just freeze at end once it finishes.
                if (!wallJumpAnimFrozen && animPlayer.CurrentAnimation == "ma_wjmp")
                {
                    double len = animPlayer.CurrentAnimationLength;
                    double pos = animPlayer.CurrentAnimationPosition;

                    // when the animation reaches the end, snap to last frame and hold it
                    if (pos >= len - 0.01f)
                    {
                        animPlayer.Seek(len, true); // ensure exact last frame pose
                        animPlayer.Stop(true); // stop playback but KEEP pose
                        wallJumpAnimFrozen = true;
                    }
                }

                // lock the kick for a few frames so it feels committed
                if (wallKickLock > 0)
                {
                    velocity.X = wallKickDir.X * WALL_KICK_OUT_SPEED;
                    velocity.Z = wallKickDir.Z * WALL_KICK_OUT_SPEED;
                }
            }

            if (stateOfMario == MarioState.ledgeFall)
            {
                if (Input.IsActionJustPressed("button_b"))
                {
                    stateOfMario = MarioState.diving;
                    SetMarioState(stateOfMario);
                    Vector3 diveDir = lastFacingDirection.Normalized();
                    velocity.X = diveDir.X * DIVE_SPEED_XZ;
                    velocity.Z = diveDir.Z * DIVE_SPEED_XZ;
                    goto EndFrame;
                }
            }

            // --- VARIABLE WALL JUMP HOLD ---
            if (wallJumpHoldActive)
            {
                if (Input.IsActionPressed("button_a"))
                {
                    wallJumpHoldTime += (float)delta;

                    if (wallJumpHoldTime < MAX_WALL_JUMP_HOLD_TIME)
                    {
                        float a = wallJumpHoldTime / MAX_WALL_JUMP_HOLD_TIME;
                        float targetY = Mathf.Lerp(WALL_JUMP_BASE_UP_VEL, WALL_JUMP_MAX_UP_VEL, a);

                        // Match your normal jump behavior: while holding, keep Y at the computed target
                        // (prevents gravity from shortening the hold)
                        if (velocity.Y < targetY)
                            velocity.Y = targetY;
                    }
                    else
                    {
                        wallJumpHoldActive = false;
                    }
                }
                else
                {
                    wallJumpHoldActive = false;
                }
            }

            //this is the velocity of being in the air
            //keeping the same direction your face but being able to airdrift

            // Air-spin: no extra height, no state change
            // If player does the stick spin while airborne, enter SpinJump state (NO height boost)
            if (spinInput && CanStartSpinInAir())
            {
                StartSpinJumpFromAir();
                spinInput = false; // consume
            }

            // --- GROUND POUND from air (R button) ---
            if (
                Input.IsActionJustPressed("button_r")
                && stateOfMario != MarioState.groundPoundStartup
                && stateOfMario != MarioState.groundPoundFalling
                && stateOfMario != MarioState.wallSlide
                && stateOfMario != MarioState.ledgeHang
                && stateOfMario != MarioState.ledgeClimb
                && stateOfMario != MarioState.ledgeHopUp
            )
            {
                // Enter ground pound startup — freeze in air and play startup anim
                stateOfMario = MarioState.groundPoundStartup;
                // Play directly via AnimationPlayer (bypass SetMarioState to avoid
                // the animTree.Active=true / animPlayer.Stop at the top of that method)
                if (animTree != null)
                    animTree.Active = false;
                animPlayer.Play("ma_hipsr");
                velocity = Vector3.Zero;
                isJumping = false;
                isSpining(false);

                // Fix facing — sideflip and spin jump both have offset/spinning armature
                // Always snap to the stored takeoff direction from whichever state we came from
                Vector3 gpFaceDir = Vector3.Zero;
                if (spinJumpDirLocked && spinJumpTakeoffDir != Vector3.Zero)
                    gpFaceDir = spinJumpTakeoffDir;
                else if (sideFlipDirLocked && sideFlipTakeoffDir != Vector3.Zero)
                    gpFaceDir = sideFlipTakeoffDir;
                else if (lastFacingDirection != Vector3.Zero)
                    gpFaceDir = lastFacingDirection;
                else
                    gpFaceDir = Vector3.Forward;

                gpFaceDir = gpFaceDir.Normalized();
                lastFacingDirection = gpFaceDir;
                armature.Rotation = new Vector3(0f, BaseYawFromDir(gpFaceDir), 0f);

                sideFlipDirLocked = false;
                sideFlipLockedYaw = 0f;
                _sideFlipLanding = false;
                spinJumpDirLocked = false;
                _groundPoundStalling = false;
                _groundPoundSlpedPlaying = false;

                goto EndFrame;
            }

            // SpinJump state spins the whole way down until landing or B dive-out
            if (stateOfMario == MarioState.SpinJump)
            {
                RotateSpinJump(delta);
                isSpining(true);
            }
            else
            {
                isSpining(false);
            }

            sideFlipTurningTimer = 33;
            Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
            if (stateOfMario == MarioState.bellyRollout)
            {
                // Belly rollout has air control (removed velocity lock)
                bellyRolloutTimer--;
                if (bellyRolloutTimer == 0)
                {
                    // After rollout animation, play ma_slpla
                    if (_sm != null)
                        _sm.Travel("ma_slpla");
                }
            }
            if (Input.IsActionJustReleased("button_a"))
            {
                initalJumpHold = false;
            }

            if (
                (isJumping || stateOfMario == MarioState.singleJumpDive)
                && (Input.IsActionPressed("key_space") || Input.IsActionPressed("button_a"))
                && stateOfMario != MarioState.sideFlip
            )
            {
                jumpHoldTime += (float)delta;
                if (initalJumpHold)
                {
                    if (
                        stateOfMario == MarioState.singleJump
                        || stateOfMario == MarioState.singleJumpDive
                    )
                    {
                        if (initalJumpHold)
                        {
                            if (jumpHoldTime < MAX_JUMP_HOLD_TIME_SINGLE)
                            {
                                // Front-loaded (sqrt) instead of linear: a reflexive
                                // dive-cancel only ever captures the first few physics
                                // frames of this ramp, so most of the height gain needs
                                // to land early to be felt at all. Still reaches the
                                // same MAX_JUMP_VELOCITY_SINGLE ceiling at the same
                                // MAX_JUMP_HOLD_TIME_SINGLE cutoff as before.
                                velocity.Y =
                                    BASE_JUMP_VELOCITY
                                    + (MAX_JUMP_VELOCITY_SINGLE - BASE_JUMP_VELOCITY)
                                        * Mathf.Sqrt(jumpHoldTime / MAX_JUMP_HOLD_TIME_SINGLE)
                                    + (walkingStrength * 7);
                            }
                        }

                        if (
                            Input.IsActionJustPressed("button_b")
                            && Input.IsActionPressed("button_a")
                        )
                        {
                            stateOfMario = MarioState.diving;
                            SetMarioState(stateOfMario);
                            // Store the facing direction at the moment of the dive
                            Vector3 diveDirection = lastFacingDirection.Normalized();
                            velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                            velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;
                            velocity.Y += AIR_DIVE_Y_BOOST;
                        }
                    }
                    else if (
                        stateOfMario == MarioState.doubleJump
                        || stateOfMario == MarioState.doubleJumpDive
                    )
                    {
                        jumpHoldTime += (float)delta;
                        if (jumpHoldTime < MAX_JUMP_HOLD_TIME_DOUBLE)
                        {
                            // Front-loaded (sqrt) for the same reason as the single-jump
                            // ramp above — a reflexive dive-cancel only ever captures the
                            // first few physics frames of this window.
                            velocity.Y =
                                BASE_JUMP_VELOCITY
                                + (MAX_JUMP_VELOCITY_DOUBLE - BASE_JUMP_VELOCITY)
                                    * Mathf.Sqrt(jumpHoldTime / MAX_JUMP_HOLD_TIME_DOUBLE)
                                + (walkingStrength * 7);
                        }
                        if (
                            Input.IsActionJustPressed("button_b")
                            && Input.IsActionPressed("button_a")
                        )
                        {
                            stateOfMario = MarioState.diving;
                            SetMarioState(stateOfMario);
                            Vector3 diveDirection = lastFacingDirection.Normalized();

                            velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                            velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;
                            velocity.Y += AIR_DIVE_Y_BOOST;
                        }
                    }
                }
            }
            if (Input.IsActionJustPressed("button_b") && stateOfMario == MarioState.tripleJump)
            {
                //RotateArmature();
                stateOfMario = MarioState.diving;
                SetMarioState(stateOfMario);
                Vector3 diveDirection = lastFacingDirection.Normalized();

                velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;
                goto EndFrame; // <-- ADD THIS
            }

            if (stateOfMario == MarioState.singleRollout && Input.IsActionPressed("button_a"))
            {
                rolloutAction(delta);
            }
            else if (
                (stateOfMario == MarioState.sideFlip || stateOfMario == MarioState.SpinJump)
                && Input.IsActionJustPressed("button_b")
            )
            {
                // Consume spin
                spinInput = false;

                // IMPORTANT: clear any visual locks/offset modes BEFORE we dive
                sideFlipDirLocked = false;
                _sideFlipLanding = false;
                spinJumpDirLocked = false;

                // Pick a REAL direction to dive toward (NOT the sideflip visual yaw)
                Vector3 diveDir =
                    (stateOfMario == MarioState.sideFlip && sideFlipTakeoffDir != Vector3.Zero)
                        ? sideFlipTakeoffDir
                        : GetMoveDirOrFallback();

                if (diveDir == Vector3.Zero)
                    diveDir = Vector3.Forward;

                diveDir = diveDir.Normalized();

                // Make dive direction authoritative
                lastFacingDirection = diveDir;

                // Enter dive
                stateOfMario = MarioState.diving;
                SetMarioState(stateOfMario);

                // Snap to BASE yaw (NO SIDEFLIP_YAW_OFFSET)
                armature.Rotation = new Vector3(0f, BaseYawFromDir(diveDir), 0f);

                // Apply dive velocity
                velocity.X = diveDir.X * DIVE_SPEED_XZ;
                velocity.Z = diveDir.Z * DIVE_SPEED_XZ;

                goto EndFrame; // prevent later air-control / other B blocks from fighting this frame
            }
            else if (Input.IsActionJustPressed("button_b"))
            {
                if (stateOfMario != MarioState.diving)
                {
                    if (direction.IsZeroApprox())
                    {
                        stateOfMario = MarioState.diving;
                        SetMarioState(stateOfMario);
                        Vector3 diveDirection = lastFacingDirection.Normalized();
                        velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                        velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;
                    }
                    else if (
                        stateOfMario == MarioState.singleJump
                        || stateOfMario == MarioState.doubleJump
                        || stateOfMario == MarioState.tripleJump
                    )
                    {
                        stateOfMario = MarioState.diving;
                        SetMarioState(stateOfMario);
                        Vector3 diveDirection = lastFacingDirection.Normalized();

                        velocity.X = diveDirection.X * DIVE_SPEED_XZ;
                        velocity.Z = diveDirection.Z * DIVE_SPEED_XZ;
                    }
                }
            }
            // --- UNIVERSAL AIR CONTROL (profiles per state) ---
            if (TryGetAirProfile(stateOfMario, out var prof))
            {
                // Don't fight states that *own* XZ explicitly. Diving states used
                // to be listed here too — now handled instead by AIR_DIVE (a
                // profile tuned to curve the dive without slowing it down), so
                // TryGetAirProfile's diving cases above can actually run.
                bool ownsXZ =
                    stateOfMario == MarioState.wallSlide
                    || stateOfMario == MarioState.ledgeFall;

                if (!ownsXZ)
                {
                    if (stateOfMario == MarioState.backFlip)
                        ApplyBackflipAirControl(delta, prof);
                    else if (stateOfMario == MarioState.wallJump && !_isTreeJumpAirborne)
                        ApplyWallJumpAirControl(delta, prof, WALL_JUMP_LATERAL_ACCEL);
                    else
                        // Speed preservation is scoped to the plain single jump
                        // for now — the flips/dives own their arcs deliberately.
                        ApplyAirControl(
                            delta,
                            prof,
                            preserveTakeoffSpeed: stateOfMario == MarioState.singleJump
                        );
                }
            }
        }

        EndFrame:
        if (
            stateOfMario != MarioState.wallSlide
            && stateOfMario != MarioState.bellySlidingFromDive
            && stateOfMario != MarioState.slipping
        )
            slideDustFx?.Stop();

        if (stateOfMario != lastRecordedState)
        {
            stateHistory.Add(stateOfMario);
            lastRecordedState = stateOfMario;
        }
        Last4FacingDirections.Add(lastFacingDirection);
        Vector3 preSlideVelocity = velocity;
        MarioState preMoveState = stateOfMario;

        // A jump launching on a steep slope needs an explicit gap, not just
        // upward velocity — floor_max_angle=83° is so permissive that
        // MoveAndSlide's slide-along-floor classification treats a launch
        // that doesn't clear the surface fast enough as more floor-sliding
        // instead of leaving it: onFloor stays true, gravity code never
        // runs (it's gated on being airborne), and velocity.Y just sits
        // frozen at the launch value while he climbs the ramp like a rail
        // (confirmed via [LAND_DBG]: postSlideVelY == preSlideVelY, constant,
        // for 30+ frames straight). Nudging him off the surface along the
        // floor normal the instant a launch fires gives this frame's
        // MoveAndSlide a real gap to resolve against instead of a surface
        // it can re-glue to via slide projection.
        // The > 2f threshold only catches strong launches. An AIR state resting
        // on the floor with a small positive Y is a deadlock: gravity is gated
        // on being airborne so Y never decays, and the landing test requires
        // Y <= 0 so it can never fire either. Measured live: singleJump,
        // onFloor=True, velY frozen at exactly +1.38 for ~20 frames while posY
        // climbed -20.3 to -18.0 — he railed up the ramp in the jump animation.
        // That's the "gliding". Any upward velocity in an air state earns the
        // clearance; TryGetAirProfile is the existing "is this an air state"
        // test, so grounded locomotion (which sits at Y=0 anyway) is unaffected.
        bool inAirState = TryGetAirProfile(stateOfMario, out _);
        if (IsOnFloor() && (velocity.Y > 2f || (inAirState && velocity.Y > 0.01f)))
        {
            GlobalPosition += GetFloorNormal() * 0.12f;
        }

        // One-way leaf platform: pass through when moving upward
        SetCollisionMaskValue(3, velocity.Y <= 0.1f);
        Velocity = velocity;
        MoveAndSlide();
        velocity = Velocity;

        if (stateOfMario != _lastDbgState || IsOnFloor() != _lastDbgOnFloor || _jumpDbgFrames > 0)
        {
            float ang = IsOnFloor() ? Mathf.RadToDeg(GetFloorNormal().AngleTo(Vector3.Up)) : -1f;
            GD.Print(
                $"[LAND_DBG] onFloor={IsOnFloor()} state={stateOfMario} floorAngle={ang:F1} preSlideVelY={preSlideVelocity.Y:F2} postSlideVelY={velocity.Y:F2} posY={GlobalPosition.Y:F3} jumpBuffer={jumpBuffer} isJumping={isJumping}"
            );
            _lastDbgState = stateOfMario;
            _lastDbgOnFloor = IsOnFloor();
            if (_jumpDbgFrames > 0)
                _jumpDbgFrames--;
        }

        // Collision data is valid only after MoveAndSlide. Preserve the incoming
        // dive speed above because MoveAndSlide removes the into-wall component.
        TryEnterDiveBonkAfterMove(preSlideVelocity, preMoveState);

        if (stateOfMario == MarioState.wallSlide)
        {
            if (IsOnWall())
            {
                wallSlideLostWallFrames = 0;
                lastWallNormal = GetWallNormal();
            }
            else
            {
                wallSlideLostWallFrames++;
                if (wallSlideLostWallFrames > WALL_SLIDE_LOST_WALL_GRACE)
                {
                    wallSlideLostWallFrames = 0;
                    stateOfMario = MarioState.singleJump; // or whatever air state

                    // When falling off wall naturally (not jumping), face movement direction
                    if (inputDirWorld != Vector3.Zero)
                    {
                        Vector3 moveDir = new Vector3(inputDirWorld.X, 0, inputDirWorld.Z);
                        if (moveDir.Length() > 0.001f)
                        {
                            lastFacingDirection = moveDir.Normalized();
                            armature.Rotation = new Vector3(
                                0f,
                                YawFromDir(lastFacingDirection),
                                0f
                            );
                        }
                    }
                }
            }
        }

        if (stateOfMario == MarioState.slipping)
        {
            if (IsOnFloor())
            {
                slipLostSurfaceFrames = 0;

                // Slid down onto walkable ground. Don't snap him upright —
                // carry the momentum into the BELLY SLIDE, which already owns
                // everything that should happen next and needs no duplication:
                // the friction ramp-down, the ma_lost get-up once he runs out
                // of speed with no input, and A -> bellyRollout / singleRollout
                // picked by speed (checkSpeedForBellyRoll). B still re-dives.
                //
                // Note the slide arrives with only the HORIZONTAL part of the
                // slip: on a 66° face the slide is ~91% vertical, so a 20 m/s
                // slip lands as roughly 8 m/s along the ground once the floor
                // absorbs the vertical. That's the physically honest carry —
                // raise SlipExitSlideBoost if it reads too tame.
                float exitSpeed = new Vector2(velocity.X, velocity.Z).Length();
                if (exitSpeed >= SlipExitSlideMinSpeed)
                {
                    if (SlipExitSlideBoost != 1.0f)
                    {
                        velocity.X *= SlipExitSlideBoost;
                        velocity.Z *= SlipExitSlideBoost;
                    }
                    stateOfMario = MarioState.bellySlidingFromDive;
                }
                else
                {
                    // Trickled to a stop on the way down — nothing to slide
                    // with, so stand up as before rather than flopping onto
                    // his belly for one frame and immediately getting back up.
                    stateOfMario = MarioState.idle;
                }
                SetMarioState(stateOfMario);
            }
            else if (IsOnWall())
            {
                slipLostSurfaceFrames = 0;
                lastSlipNormal = GetWallNormal();
            }
            else
            {
                slipLostSurfaceFrames++;
                if (slipLostSurfaceFrames > WALL_SLIDE_LOST_WALL_GRACE)
                {
                    // Slid off the edge — fall normally. ma_slpla plays
                    // instead of ma_jump for this leave-slip beat.
                    slipLostSurfaceFrames = 0;
                    stateOfMario = MarioState.singleJump;
                    slipAirLaunchTimer = SLIP_LAND_DURATION;
                    SetMarioState(stateOfMario);
                }
            }
        }

        PostMoveWallCheck(preSlideVelocity, preMoveState);
        CheckGroundWallEntry(preMoveState, inputDirWorld, stickStrength);

        bool onFloorNow = IsOnFloor();
        // A single-frame IsOnFloor() flicker (more likely to happen at speed on a
        // steep incline) shouldn't count as "landing" if he was already a grounded
        // locomotion state a moment ago — he never actually left the ground long
        // enough to be airborne. Without this, running up a steep slope could
        // repeatedly trip EnterLanding() below, which forces velocity.Y = 0 and
        // locks him into landing-recovery, eating jump input until it happens to
        // land in the narrow !landingJumpConsumed window — i.e. "can't jump at
        // that angle" even though he was never really airborne to begin with. A
        // genuine walk-off-a-ledge fall reclassifies stateOfMario to ledgeFall (see
        // the !IsOnFloor() branch above) before this can fire, so this only
        // suppresses the false-positive flicker case, not real landings.
        bool wasGroundedLocoAtImpact =
            preMoveState == MarioState.idle
            || preMoveState == MarioState.sneak
            || preMoveState == MarioState.walking
            || preMoveState == MarioState.running
            || preMoveState == MarioState.sprinting
            || preMoveState == MarioState.pivot
            || preMoveState == MarioState.sideFlipTurning;
        // Second, stronger signal on top of the state check above: a longer
        // flicker (2-3 frames instead of 1, more likely on steeper/faster cases)
        // gives the walk-off-ledge detector in the !IsOnFloor() branch above just
        // enough time to already reclassify preMoveState to ledgeFall before he
        // touches back down — which defeats the grounded-state check by itself.
        // Real falls build up real downward speed under gravity; a brief flicker
        // doesn't have time to. Require actual fall speed at touchdown too.
        bool hadRealFallSpeed = preSlideVelocity.Y < -3f;

        // ...but ONLY demand that proof for the case it was written for. The
        // flicker it guards against reclassifies to ledgeFall before touchdown
        // (that's the whole reason the grounded-state check above isn't enough),
        // so ledgeFall is the only state that needs to prove it fell for real.
        //
        // Requiring it unconditionally broke landings that are genuinely gentle:
        // coming down onto an UPHILL ramp, the ramp rises to meet him, so
        // relative vertical speed at contact is tiny. Confirmed live —
        // touchdown at preSlideVelY = -1.85 on a 33.8° ramp failed the > 3 gate,
        // so justLanded stayed false, the landing block never ran, and he was
        // left stuck in singleJump ON the ground: gravity gated off, Y frozen at
        // 0, sliding up the slope in the jump animation until he stopped.
        bool needsFallSpeedProof = preMoveState == MarioState.ledgeFall;

        // The old `< -3` test was quietly doing TWO jobs, and dropping it
        // wholesale broke the second one. Direction is the real invariant: you
        // cannot be landing while still travelling UP. A dive launches at
        // velocity.Y = +8 (DIVE_POP_Y) and, going into a rising ramp, clips the
        // surface within a frame or two while still moving upward — without
        // this it registers as touchdown, dumps him straight into
        // bellySlidingFromDive and out to running, i.e. "can't dive on ramps".
        //
        // Magnitude stays gated to ledgeFall only (see above), so a genuinely
        // gentle uphill landing (-1.85 measured) still counts.
        bool movingDownward = preSlideVelocity.Y <= 0f;

        // A rollout is allowed to land while still drifting up a little — see
        // ROLLOUT_LAND_MAX_RISE. Without this he keeps rolling for several
        // frames after he's visibly back on the ramp, waiting for velY to cross
        // zero, which reads as the roll animation having to play itself out
        // before sprinting (and the boost) can start.
        bool rolloutSettling =
            (preMoveState == MarioState.singleRollout || preMoveState == MarioState.bellyRollout)
            && preSlideVelocity.Y <= ROLLOUT_LAND_MAX_RISE;
        movingDownward = movingDownward || rolloutSettling;

        bool justLanded =
            onFloorNow
            && !wasOnFloorPrev
            && !wasGroundedLocoAtImpact
            && movingDownward
            && (!needsFallSpeedProof || hadRealFallSpeed);

        if (justLanded)
        {
            // Landing is a purely VERTICAL event — invisible to the head/arm
            // jiggle's continuous horizontal-acceleration drive — so it needs a
            // direct one-shot kick, scaled by how hard he hit (fall speed).
            float fallSpeed = Mathf.Max(0f, -preSlideVelocity.Y);
            if (fallSpeed > 0.5f)
            {
                _headJiggle?.AddImpulse(Vector3.Down * fallSpeed * HeadJiggleLandingKick);
                _armJiggleR?.AddImpulse(Vector3.Down * fallSpeed * ArmJiggleLandingKick);
                _armJiggleL?.AddImpulse(Vector3.Down * fallSpeed * ArmJiggleLandingKick);
            }
        }

        // Symmetric case: LEAVING the ground on a jump is a sudden UPWARD
        // acceleration — just as invisible to the horizontal drive as landing is.
        // Loose parts lag the same direction (down, relative to the now-rising
        // body) as they do on landing, just kicked by launch speed instead of
        // impact speed. Only fires on an actual jump launch (upward velocity),
        // not walking off a ledge (which has ~0 vertical velocity at departure).
        bool justJumped = wasOnFloorPrev && !onFloorNow && preSlideVelocity.Y > 0.5f;
        if (justJumped)
        {
            float launchSpeed = preSlideVelocity.Y;
            _headJiggle?.AddImpulse(Vector3.Down * launchSpeed * HeadJiggleJumpKick);
            _armJiggleR?.AddImpulse(Vector3.Down * launchSpeed * ArmJiggleJumpKick);
            _armJiggleL?.AddImpulse(Vector3.Down * launchSpeed * ArmJiggleJumpKick);
        }

        // If we just landed on a palm leaf, trigger its bounce.
        if (justLanded)
        {
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                var col = GetSlideCollision(i);
                if (col.GetNormal().Y < 0.5f)
                    continue; // not floor-ish
                if (col.GetCollider() is Node leafBody && leafBody.IsInGroup("palm_leaf_body"))
                {
                    if (leafBody.GetParent() is ILeafBouncer bouncer)
                        bouncer.Bounce(GlobalPosition);
                    break;
                }
            }

            // If the last airborne action was a tree jump-off that used ma_tjmp1, the
            // armature still has the +Pi backwards-anim correction. Undo it on landing.
            if (_treeJumpOffTjmp1)
            {
                armature.Rotation = new Vector3(0f, BaseYawFromDir(_treeJumpOffDir), 0f);
                lastFacingDirection = _treeJumpOffDir;
                _treeJumpOffTjmp1 = false;
            }
            _isTreeJumpAirborne = false; // outside the tjmp1 check — must clear for BOTH 50/50 paths
        }

        if (onFloorNow && stateOfMario == MarioState.ledgeHopUp)
        {
            stateOfMario = MarioState.idle;
            animPlayer.Play("ma_wait");
            SetMarioState(stateOfMario);
            suppressLandingFrames = 2;
            landingEnteredThisContact = true;
        }

        MarioState airStateAtImpact = preMoveState; // what we were doing when we touched ground

        if (!onFloorNow)
        {
            landingEnteredThisContact = false; // reset latch once airborne
        }

        if (
            stateOfMario == MarioState.pickupRaise
            || stateOfMario == MarioState.putDown
            || stateOfMario == MarioState.carrying
            || stateOfMario == MarioState.pickupFail
            || stateOfMario == MarioState.hurt
            || stateOfMario == MarioState.bonk
            || stateOfMario == MarioState.stomping
            || stateOfMario == MarioState.groundPoundStartup
            || stateOfMario == MarioState.groundPoundFalling
            || stateOfMario == MarioState.groundPoundLanding
        )
        {
            wasOnFloor = IsOnFloor();
            return;
        }

        if (justLanded)
        {
            ClearAirTakeoffDir();

            // If we landed from a ledge get-up, skip landing dust and ma_laend.
            if (
                airStateAtImpact == MarioState.ledgeHopUp
                || airStateAtImpact == MarioState.ledgeClimb
            )
            {
                velocity.Y = 0f;
                // pick your preferred "finish" anim
                stateOfMario = MarioState.idle;
                animPlayer.Play("ma_wait");
                SetMarioState(stateOfMario);

                // prevent re-trigger spam on the same contact
                landingEnteredThisContact = true;

                wasOnFloor = onFloorNow;
                return;
            }

            if (suppressLandingFrames <= 0)
                TriggerLandingDust();

            if (airStateAtImpact == MarioState.sideFlip)
            {
                Vector3 lockDir =
                    (sideFlipTakeoffDir != Vector3.Zero)
                        ? sideFlipTakeoffDir
                        : GetMoveDirOrFallback();

                lockDir = lockDir.Normalized();

                lastFacingDirection = lockDir;
                landingFacingDir = lockDir;

                // The reused single-jump landing animation is authored facing forward,
                // so clear the side-flip PI offset as soon as Mario touches down.
                float landingYaw = BaseYawFromDir(lockDir);
                sideFlipLockedYaw = 0f;
                armature.Rotation = new Vector3(0f, landingYaw, 0f);

                _sideFlipLanding = false;
                sideFlipDirLocked = false;
            }

            // If we landed from a dive, go into belly slide instead of normal landing
            if (
                airStateAtImpact == MarioState.diving
                || airStateAtImpact == MarioState.singleJumpDive
                || airStateAtImpact == MarioState.doubleJumpDive
                || airStateAtImpact == MarioState.tripleJumpDive
            )
            {
                stateOfMario = MarioState.bellySlidingFromDive;
                SetMarioState(stateOfMario);

                wasOnFloor = onFloorNow; // keep your floor latch correct
                return; // IMPORTANT: don’t goto EndFrame here
            }

            // Slipping already picked his landing state up in the post-move slip
            // block (belly slide if he carried speed off the face, idle if he
            // trickled to a stop) — that block runs on IsOnFloor() directly, so
            // it fires even on a slow slip where justLanded is false for want of
            // fall speed. Bail out before the generic EnterLanding below can
            // overwrite that with ma_laend.
            if (airStateAtImpact == MarioState.slipping)
            {
                wasOnFloor = onFloorNow;
                return;
            }

            // If we landed from a rollout, go straight into runout (no landing anim)
            if (
                airStateAtImpact == MarioState.bellyRollout
                || airStateAtImpact == MarioState.singleRollout
            )
            {
                EnterRolloutRun(inputDirWorld, stickStrength);
                // If jump was buffered while airborne, queue it for NEXT frame (pre-ground-logic)
                if (jumpBuffer > 0)
                    queuedTouchdownJump = true;
                wasOnFloor = onFloorNow;
                return;
            }
            // --- Jump chain window setup ---
            if (airStateAtImpact == MarioState.SpinJump)
            {
                // Spin jump always sets up for triple jump (like double jump)
                jumpChainStage = 2;
                jumpChainTimer = JUMP_CHAIN_WINDOW;
            }
            else if (airStateAtImpact == MarioState.ledgeFall)
            {
                // Landing from ledge fall allows a double jump next
                jumpChainStage = 1;
                jumpChainTimer = JUMP_CHAIN_WINDOW;
            }
            else if (airStateAtImpact == MarioState.singleJump)
            {
                jumpChainStage = 1;
                jumpChainTimer = JUMP_CHAIN_WINDOW;
            }
            else if (airStateAtImpact == MarioState.doubleJump)
            {
                jumpChainStage = 2;
                jumpChainTimer = JUMP_CHAIN_WINDOW;
            }
            else
            {
                jumpChainStage = 0;
                jumpChainTimer = 0;
            }

            // --- SpinJump facing snap on landing (the thing that fixes wrong direction) ---
            if (airStateAtImpact == MarioState.SpinJump)
            {
                Vector3 lockDir = spinJumpTakeoffDir;

                if (lockDir == Vector3.Zero)
                    lockDir = GetMoveDirOrFallback();

                lockDir = lockDir.Normalized();

                lastFacingDirection = lockDir;
                landingFacingDir = lockDir;

                armature.Rotation = new Vector3(0f, YawFromDir(lockDir), 0f);
                spinJumpDirLocked = false;
            }

            // Enter landing (guarded internally)
            if (!IsLandingState(stateOfMario))
                if (suppressLandingFrames > 0)
                {
                    suppressLandingFrames--;
                    landingEnteredThisContact = true; // prevent EnterLanding re-trigger spam
                    wasOnFloor = onFloorNow;
                    return;
                }

            // Reuse the normal single-jump landing animation and short lock.
            if (airStateAtImpact == MarioState.sideFlip)
            {
                EnterLanding(MarioState.singleJumpLanding, "ma_laend");
            }
            else
            {
                EnterLanding(MarioState.landing, "ma_laend");
            }
        }

        wasOnFloor = onFloorNow;

        UpdatePlatformTilt(delta);
    }

    public override void _Process(double delta)
    {
        UpdateRunBob((float)delta);
        UpdateHeadLock((float)delta);
        UpdateFootIK((float)delta);
        UpdateHandPose();
        UpdateJiggle((float)delta);

        // Ground-pound streak effect: on only during the actual fall (not the air-stall).
        if (groundPoundFx != null)
        {
            bool fxOn = stateOfMario == MarioState.groundPoundFalling && !_groundPoundStalling;
            groundPoundFx.SetActive(fxOn);
        }

        // Ground-pound jump 360° spin while rising. Sweep from 0 → 2π * spinTurns.
        if (_isGroundPoundJump)
        {
            bool ended =
                stateOfMario != MarioState.singleJump
                || IsOnFloor()
                || _gpJumpTimer >= _gpJumpSpinDuration;

            if (ended)
            {
                armature.Rotation = new Vector3(0f, _gpJumpStartYaw, 0f);
                _isGroundPoundJump = false;
            }
            else
            {
                _gpJumpTimer += (float)delta;
                float t = Mathf.Clamp(_gpJumpTimer / _gpJumpSpinDuration, 0f, 1f);
                float spin = t * Mathf.Tau * GroundPoundJumpSpinTurns;
                armature.Rotation = new Vector3(0f, _gpJumpStartYaw + spin, 0f);
            }
        }

        //handle movement influence
        if (Velocity.Length() > .1)
        {
            //this will handle any influce the cam needs
        }

        // Blue coin collect display timer
        if (_blueCoinDisplayTimer > 0f)
        {
            _blueCoinDisplayTimer -= (float)delta;
            if (
                _blueCoinDisplayTimer <= 0f
                && _lifeCounterShowing
                && stateOfMario != MarioState.idle
            )
            {
                _lifeCounterShowing = false;
                _lifeCounterHud?.HideLives();
                _blueCoinHud?.HideHud();
                _shineHud?.HideHud();
                _yellowCoinHud?.ReturnToNormal();
            }
        }

        // Life counter idle display
        if (stateOfMario == MarioState.idle)
        {
            _idleTimer += (float)delta;
            if (!_lifeCounterShowing && _idleTimer >= IdleShowDelay)
            {
                _lifeCounterShowing = true;
                _lifeCounterHud?.ShowLives(_lives);
                _shineHud?.ShowHud();
                _blueCoinHud?.ShowHud();
                _yellowCoinHud?.NudgeDown();
            }
        }
        else
        {
            _idleTimer = 0f;
            if (_lifeCounterShowing && _blueCoinDisplayTimer <= 0f)
            {
                _lifeCounterShowing = false;
                _lifeCounterHud?.HideLives();
                _blueCoinHud?.HideHud();
                _shineHud?.HideHud();
                _yellowCoinHud?.ReturnToNormal();
            }
        }
    }

    private Vector2 playerAimPoint()
    {
        float width = DisplayServer.WindowGetSize().X;
        float height = DisplayServer.WindowGetSize().Y;
        return new Vector2(width / 2, height * 1f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.IsActionJustPressed("key_esc"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
                Input.SetMouseMode(Input.MouseModeEnum.Visible);
            else
                Input.SetMouseMode(Input.MouseModeEnum.Captured);
        }
    }

    private void SleepStatus(bool isSleeping)
    {
        if (isSleeping)
        {
            leftEyeMaterial.AlbedoTexture = sleepingEyeTexture;
            rightEyeMaterial.AlbedoTexture = sleepingEyeTexture;
        }
        else
        {
            leftEyeMaterial.AlbedoTexture = awakeEyeTexture;
            rightEyeMaterial.AlbedoTexture = awakeEyeTexture;
            //Usally when this is called all timers should be reset for sleeping
            sittingAnimationTimer = 58;
            sleepingTimer = 28;
            idleTimer = 480;
            sittingTimerWait = 180;
            sleeptimer = 100;
            zEffectSpawner.StopZEffect();
        }
    }

    private void rolloutAction(double delta)
    {
        // Hold-to-boost rollout jump height (Y). This alone is meant to keep
        // running every frame button_a is held (it's the same charge-up shape
        // as the single/double jump hold-height logic) — it does NOT touch
        // X/Z, so it no longer fights the universal air-control block below.
        if (initalJumpHold)
        {
            jumpHoldTime += (float)delta;

            if (jumpHoldTime < MAX_ROLLOUT_HOLD_TIME)
            {
                float a = jumpHoldTime / MAX_ROLLOUT_HOLD_TIME;
                velocity.Y = Mathf.Lerp(BASE_ROLLOUT_VELOCITY, MAX_ROLLOUT_VELOCITY, a);
            }
        }
    }

    // Launch the rollout's horizontal speed. Deliberately called ONCE, only on
    // the frame singleRollout is entered — this used to live inside
    // rolloutAction() and re-run every frame button_a was held, which pinned
    // X/Z back to pure lastFacingDirection every tick and made the universal
    // air-control block's steering (which runs later the same frame, and
    // every frame after) invisible: it could never out-fight a full XZ reset
    // happening right before it. Now it only sets the initial launch vector;
    // ApplyAirControl (AIR_SINGLE) owns steering from then on.
    private void StartRolloutXZ()
    {
        Vector3 dir = lastFacingDirection;
        if (dir == Vector3.Zero)
        {
            Vector2 v = new Vector2(velocity.X, velocity.Z);
            if (v.Length() > 0.1f)
                dir = new Vector3(velocity.X, 0, velocity.Z).Normalized();
            else
                dir = Vector3.Forward;
        }
        dir = dir.Normalized();

        velocity.X = dir.X * ROLLOUT_SPEED_XZ;
        velocity.Z = dir.Z * ROLLOUT_SPEED_XZ;
    }

    /**
        This method checks to see if Player is in withen a certain speed to do a roll out from belly slide
    **/
    private bool checkSpeedForBellyRoll(Vector3 velocity)
    {
        if ((velocity.X > -3) && (velocity.X < 3) && (velocity.Z < 3) && (velocity.Z > -3))
        {
            return true;
        }
        return false;
    }

    private void setupHandSwaping()
    {
        //setting up the nodes for hands
        RightClosedHand = GetNode<Node3D>("Armature/Skeleton3D/RightHandBone/RightHandClosed");
        LeftClosedHand = GetNode<Node3D>("Armature/Skeleton3D/LeftHandBone/LeftHandClosed");

        // NOTE the scene spells these "Slighty", not "Slightly".
        RightSlightlyOpenHand = GetNodeOrNull<Node3D>(
            "Armature/Skeleton3D/RightHandBone/RightHandSlightyOpen"
        );
        LeftSlightlyOpenHand = GetNodeOrNull<Node3D>(
            "Armature/Skeleton3D/LeftHandBone/LeftHandSlightyOpen"
        );
        marioMesh = GetNode<MeshInstance3D>("Armature/Skeleton3D/Mesh_0");
        // Find the mesh inside the closed right hand rather than hard-coding a path
        // through its internals - a different character's hand model will not have
        // the same node names as Mario's ma_hnd3r_armature.
        MeshInstance3D RightHandMeshClosed = FindFirstMeshInstance(RightClosedHand);
        if (RightHandMeshClosed == null)
        {
            GD.PushWarning("setupHandSwaping: no MeshInstance3D inside RightHandClosed.");
            return;
        }

        // The wrist stubs on the BODY mesh, located by material name. Surface order
        // differs per character - these are 4/5 on Mario and Koopa, but on Luigi
        // index 5 is _mat_eye_L, which had this tinting his eye instead of his hand
        // (and fighting SetupSleeping over the same surface).
        int RightHandSurface = FindSurfaceByMaterialName(marioMesh, "_mat_head(5)", 4);
        int LeftHandSurface = FindSurfaceByMaterialName(marioMesh, "_mat_head(6)", 5);

        Material originalLeftMaterial =
            marioMesh.GetSurfaceOverrideMaterial(LeftHandSurface)
            ?? marioMesh.Mesh.SurfaceGetMaterial(LeftHandSurface);

        if (originalLeftMaterial is StandardMaterial3D)
        {
            LeftHandMaterial = (StandardMaterial3D)originalLeftMaterial.Duplicate();
            marioMesh.SetSurfaceOverrideMaterial(LeftHandSurface, LeftHandMaterial);
        }
        else
        {
            GD.PrintErr("ERROR: Left Hand material not found or invalid!");
        }

        // Right Hand Material
        Material originalRightMaterial =
            marioMesh.GetSurfaceOverrideMaterial(RightHandSurface)
            ?? marioMesh.Mesh.SurfaceGetMaterial(RightHandSurface);

        if (originalRightMaterial is StandardMaterial3D)
        {
            RightHandMaterial = (StandardMaterial3D)originalRightMaterial.Duplicate();
            marioMesh.SetSurfaceOverrideMaterial(RightHandSurface, RightHandMaterial);
        }
        else
        {
            GD.PrintErr("ERROR: Right Hand material not found or invalid!");
        }

        if (originalRightMaterial is StandardMaterial3D)
        {
            RightHandClosedMaterial = (StandardMaterial3D)originalRightMaterial.Duplicate();
            RightHandMeshClosed.SetSurfaceOverrideMaterial(0, RightHandClosedMaterial);
        }

        if (RightHandMaterial != null)
            RightHandMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        if (LeftHandMaterial != null)
            LeftHandMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        // Remember the open-hand albedo so ShowOpenHands() can restore it after
        // the closed-hand swap zeroes it out.
        if (RightHandMaterial != null)
            _rightHandOpenAlbedo = RightHandMaterial.AlbedoColor;
        if (LeftHandMaterial != null)
            _leftHandOpenAlbedo = LeftHandMaterial.AlbedoColor;

        GD.Print("Hand materials successfully duplicated and assigned.");
    }

    /// <summary>
    /// Index of the first surface whose material resource_name starts with
    /// <paramref name="namePrefix"/>, or <paramref name="fallback"/> if none
    /// matches. Lets per-character features find their surface without every
    /// character having to hardcode an index.
    /// </summary>
    private static int FindSurfaceByMaterialName(
        MeshInstance3D meshInstance,
        string namePrefix,
        int fallback
    )
    {
        if (meshInstance?.Mesh == null)
            return fallback;

        for (int i = 0; i < meshInstance.Mesh.GetSurfaceCount(); i++)
        {
            var mat =
                meshInstance.GetSurfaceOverrideMaterial(i)
                ?? meshInstance.Mesh.SurfaceGetMaterial(i);

            if (mat != null && mat.ResourceName.StartsWith(namePrefix))
                return i;
        }

        GD.PushWarning(
            $"FindSurfaceByMaterialName: no surface named '{namePrefix}*', "
                + $"falling back to index {fallback}."
        );
        return fallback;
    }

    /**
        This Sets up anything that involved with sleeping
        also helps setup any replaceing textures involving the eyes
    **/
    private void SetupSleeping()
    {
        //sleeping effects
        zEffectSpawner = GetNode<ZEffectSpawner>("ZEffectSpawner");
        // Load the textures from your files (update the paths)
        // Per-character if the profile supplies them, else Mario's originals.
        awakeEyeTexture =
            Profile?.AwakeEyeTexture
            ?? (Texture2D)
                ResourceLoader.Load("res://models/Mario/mario/bmd/ma_mdl1/H_ma_eye1_s3tc.png");
        sleepingEyeTexture =
            Profile?.SleepingEyeTexture
            ?? (Texture2D)
                ResourceLoader.Load(
                    "res://models/Mario/mario/bmd/ma_mdl1/H_ma_eye1_s3tc_shut.png"
                );

        // Get Mario's main mesh
        marioMesh = GetNode<MeshInstance3D>("Armature/Skeleton3D/Mesh_0");
        if (marioMesh == null)
        {
            GD.PrintErr("ERROR: Mario mesh not found! Check the node path.");
            return;
        }

        // Locate the eyes by MATERIAL NAME rather than a hardcoded index. Surface
        // order differs per character (Mario's eyes are 7/8, Luigi's are 5/6),
        // but both rigs name the materials _mat_eye_L / _mat_eye_R - Luigi's just
        // carry a ".001" suffix from the Blender round-trip, so match on prefix.
        int leftEyeSurface = FindSurfaceByMaterialName(marioMesh, "_mat_eye_L", 7);
        int rightEyeSurface = FindSurfaceByMaterialName(marioMesh, "_mat_eye_R", 8);

        // Left Eye Material
        Material originalLeftMaterial =
            marioMesh.GetSurfaceOverrideMaterial(leftEyeSurface)
            ?? marioMesh.Mesh.SurfaceGetMaterial(leftEyeSurface);

        if (originalLeftMaterial is StandardMaterial3D)
        {
            leftEyeMaterial = (StandardMaterial3D)originalLeftMaterial.Duplicate();
            marioMesh.SetSurfaceOverrideMaterial(leftEyeSurface, leftEyeMaterial);
        }
        else
        {
            GD.PrintErr("ERROR: Left eye material not found or invalid!");
        }

        // Right Eye Material
        Material originalRightMaterial =
            marioMesh.GetSurfaceOverrideMaterial(rightEyeSurface)
            ?? marioMesh.Mesh.SurfaceGetMaterial(rightEyeSurface);

        if (originalRightMaterial is StandardMaterial3D)
        {
            rightEyeMaterial = (StandardMaterial3D)originalRightMaterial.Duplicate();
            marioMesh.SetSurfaceOverrideMaterial(rightEyeSurface, rightEyeMaterial);
        }
        else
        {
            GD.PrintErr("ERROR: Right eye material not found or invalid!");
        }

        GD.Print("Eye materials successfully duplicated and assigned.");
    }

    private void RotateSpinJump(double delta)
    {
        if (stateOfMario == MarioState.SpinJump)
        {
            float degreesPerSecond = 2000f;
            float angleThisFrame = degreesPerSecond * (float)delta * (Mathf.Pi / 180.0f);
            armature.RotateObjectLocal(Vector3.Up, angleThisFrame);
        }
    }

    private void RotateArmature()
    {
        if (stateOfMario == MarioState.sideFlipTurning)
        {
            sideFlipTurningTimer--;

            // If player is aiming a direction this frame, let it update the turn target
            if (aimDirThisFrame != Vector3.Zero)
            {
                sideFlipStoredDir = aimDirThisFrame.Normalized();
            }

            // If we still don't have a dir, fall back
            Vector3 turnDir =
                (sideFlipStoredDir != Vector3.Zero) ? sideFlipStoredDir
                : (lastFacingDirection != Vector3.Zero) ? lastFacingDirection
                : Vector3.Forward;

            turnDir = turnDir.Normalized();

            // IMPORTANT: turning anim is authored backward, so apply the visual offset WHILE turning
            float targetYaw = YawFromDir(turnDir) + SIDEFLIP_YAW_OFFSET;

            // Smoothly rotate toward the target yaw (instead of hard lock)
            float t = Mathf.Clamp(
                (float)(SideFlipTurningYawSpeed * GetPhysicsProcessDeltaTime()),
                0f,
                1f
            );
            float newYaw = Mathf.LerpAngle(armature.Rotation.Y, targetYaw, t);
            armature.Rotation = new Vector3(0f, newYaw, 0f);

            // Keep facing direction in sync so takeoff uses the latest direction
            lastFacingDirection = turnDir;

            if (sideFlipTurningTimer <= 0)
            {
                sideFlipTurningTimer = 33;

                // When turning ends, snap to NORMAL yaw (no offset) for idle/run
                armature.Rotation = new Vector3(0f, YawFromDir(turnDir), 0f);
                lastFacingDirection = turnDir;

                bool stickNeutral = (walkingStrength <= DEADZONE) || direction == Vector3.Zero;
                stateOfMario =
                    stickNeutral ? MarioState.idle
                    : (walkingStrength > 0.5f) ? MarioState.sprinting
                    : (walkingStrength <= SNEAK_MAX_INPUT) ? MarioState.sneak
                    : MarioState.running;

                if (stickNeutral)
                    SetMarioState(stateOfMario);

                // clear turning data
                sideFlipDirLocked = false;
                sideFlipStoredDir = Vector3.Zero;
            }

            return;
        }
        else if (stateOfMario == MarioState.sideFlip)
        {
            if (sideFlipDirLocked)
            {
                armature.Rotation = new Vector3(0f, sideFlipLockedYaw, 0f);
            }
            else
            {
                // fallback = face velocity direction, but using the same base yaw convention
                armature.Rotation = armature.Rotation with
                {
                    Y = BaseYawFromVelXZ(),
                };
            }
        }
        else if (stateOfMario == MarioState.diving)
        {
            armature.Rotation = armature.Rotation with { Y = BaseYawFromVelXZ() };
        }
        else if (_sideFlipLanding)
        {
            if (!IsLandingState(stateOfMario))
            {
                // Landing finished — snap armature to normal facing and clear flag
                _sideFlipLanding = false;
                armature.Rotation = new Vector3(0f, YawFromDir(lastFacingDirection), 0f);
            }
            else
            {
                // Keep armature locked at sideflip yaw during landing animation
                armature.Rotation = new Vector3(0f, sideFlipLockedYaw, 0f);
            }
        }
        else
        {
            float speedSq = velocity.X * velocity.X + velocity.Z * velocity.Z;
            if (speedSq > 0.001f)
            {
                armature.Rotation = armature.Rotation with { Y = BaseYawFromVelXZ() };
            }
        }
    }

    // Determine quadrant based on the sign of x and y.
    // Quadrant 0: Top-Right (x >= 0, y >= 0)
    // Quadrant 1: Top-Left  (x <  0, y >= 0)
    // Quadrant 2: Bottom-Left (x <  0, y <  0)
    // Quadrant 3: Bottom-Right(x >= 0, y <  0)
    private int GetQuadrant(Vector2 input)
    {
        if (input.X >= 0 && input.Y >= 0)
            return 0;
        else if (input.X < 0 && input.Y >= 0)
            return 1;
        else if (input.X < 0 && input.Y < 0)
            return 2;
        else if (input.X >= 0 && input.Y < 0)
            return 3;
        return -1; // Should never happen.
    }

    // Check if all quadrants have been visited.
    private bool AllQuadrantsVisited()
    {
        foreach (bool visited in quadrantVisited)
        {
            if (!visited)
                return false;
        }
        return true;
    }

    // Reset the 10-frame window and quadrant flags.
    private void ResetWindow()
    {
        frameCounter = 0;
        for (int i = 0; i < quadrantVisited.Length; i++)
        {
            quadrantVisited[i] = false;
        }
    }

    private void setupSpinJumpEffects()
    {
        // Get the child node that has the SpinJumpEffects script
        spinRingFx = GetNodeOrNull<SpinJumpEffects>("SpinJumpEffects");
        if (spinRingFx == null)
        {
            GD.PushError(
                "SpinJumpEffects node is missing OR does not have SpinJumpEffects.cs attached."
            );
            return;
        }

        // No Target / FollowYaw anymore (child of Mario handles positioning)
        spinRingFx.SetSpinActive(false);

        SetupFootSparks();
        SetupBonkStars();
        GD.Print("SpinJumpEffects hooked up OK.");
    }

    private void SetupFootSparks()
    {
        footSparks = new GpuParticles3D();
        footSparks.Name = "FootSparks";
        footSparks.Amount = 12;
        footSparks.Lifetime = 0.19;
        footSparks.Emitting = false;
        footSparks.LocalCoords = false; // world space so streaks fly out independently
        footSparks.Explosiveness = 0.0f;

        var pm = new ParticleProcessMaterial();
        pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        pm.EmissionSphereRadius = 0.12f;
        pm.Direction = new Vector3(0f, 1f, 0f); // straight up...
        pm.Spread = 130f; // ...within a ~30-45 degree cone
        pm.Flatness = 0f;
        pm.InitialVelocityMin = 12f; // faster
        pm.InitialVelocityMax = 18f;
        pm.Gravity = new Vector3(0f, 0f, 0f); // no gravity
        pm.ScaleMin = 0.7f;
        pm.ScaleMax = 1.3f;
        pm.Color = new Color(1f, 1f, 1f, 1f);
        // Elongate each particle along its travel direction -> streaks.
        pm.SetParticleFlag(ParticleProcessMaterial.ParticleFlags.AlignYToVelocity, true);

        // Alpha fade only (colour comes from the streak texture): hold, then fade.
        var grad = new Gradient();
        grad.Offsets = new float[] { 0f, 0.6f, 1f };
        grad.Colors = new Color[]
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 1f, 1f, 0f),
        };
        var ramp = new GradientTexture1D();
        ramp.Gradient = grad;
        pm.ColorRamp = ramp;

        footSparks.ProcessMaterial = pm;

        // Elongated glowing quad; its long (Y) axis aligns to velocity.
        var quad = new QuadMesh();
        quad.Size = new Vector2(0.07f, 0.62f);

        // Red -> white -> red across the streak's WIDTH (white core, red edges).
        var stripe = new Gradient();
        stripe.Offsets = new float[] { 0f, 0.5f, 1f };
        stripe.Colors = new Color[]
        {
            new Color(1f, 0.5f, 0.08f, 1f),
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 0.5f, 0.08f, 1f),
        };
        var stripeTex = new GradientTexture2D();
        stripeTex.Gradient = stripe;
        stripeTex.Width = 32;
        stripeTex.Height = 4;
        stripeTex.Fill = GradientTexture2D.FillEnum.Linear;
        stripeTex.FillFrom = new Vector2(0f, 0.5f);
        stripeTex.FillTo = new Vector2(1f, 0.5f);

        var mat = new StandardMaterial3D();
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        mat.VertexColorUseAsAlbedo = true; // particle alpha fade
        mat.AlbedoColor = new Color(1f, 1f, 1f, 1f);
        mat.AlbedoTexture = stripeTex;
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 1f, 1f);
        mat.EmissionEnergyMultiplier = 5f;
        mat.SetTexture(BaseMaterial3D.TextureParam.Emission, stripeTex);
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        quad.Material = mat;

        footSparks.DrawPass1 = quad;

        AddChild(footSparks);
        footSparks.Position = Vector3.Zero; // feet position in the animation
    }

    private void isSpining(bool isSpining)
    {
        if (spinRingFx != null)
            spinRingFx.SetSpinActive(isSpining);

        // keep your old particles if you still want them
        if (SpinJumpEffects != null)
            SpinJumpEffects.Visible = isSpining;

        if (blueSpinEffects != null)
            blueSpinEffects.Emitting = isSpining;
        if (whiteSpinEffects != null)
            whiteSpinEffects.Emitting = isSpining;
        if (redSpinEffects != null)
            redSpinEffects.Emitting = isSpining;
    }

    private static bool TurnExceedsAngle(Vector3 from, Vector3 to, float angleDeg)
    {
        if (from == Vector3.Zero || to == Vector3.Zero)
            return false;
        float dot = from.Normalized().Dot(to.Normalized());
        float dotThreshold = MathF.Cos(angleDeg * (MathF.PI / 180f));
        return dot < dotThreshold;
    }

    // Landing input buffering
    private Vector3 landingFacingDir = Vector3.Zero; // locked facing used for jump/landing anim
    private Vector3 pendingMoveDir = Vector3.Zero; // stick direction captured during landing
    private float pendingMoveStrength = 0f;

    // Landing carry / latch
    private Vector2 landingCarryVelXZ = Vector2.Zero;
    private bool landingEnteredThisContact = false;
    private bool landingJumpConsumed = false;

    // Tuning
    private const float LANDING_FRICTION = 0.03f; // higher = stops quicker

    private bool IsLandingState(MarioState s) =>
        s == MarioState.landing
        || s == MarioState.singleJumpLanding
        || s == MarioState.doubleJumpLanding
        || s == MarioState.tripleJumpLanding;

    private void EnterLanding(MarioState landingState, string animName)
    {
        // If we already entered landing for this same ground contact, don’t restart the anim.
        if (landingEnteredThisContact && IsLandingState(stateOfMario))
            return;

        landingEnteredThisContact = true;

        stateOfMario = landingState;
        SetMarioState(landingState);
        // Lock facing & carry
        landingFacingDir = lastFacingDirection;
        landingCarryVelXZ = new Vector2(velocity.X, velocity.Z);

        // Kill vertical to prevent micro-bounces / re-landing spam
        velocity.Y = 0f;

        // Clear buffer so we capture what player holds DURING landing
        pendingMoveDir = Vector3.Zero;
        pendingMoveStrength = 0f;
        // NEW: allow one buffered/held jump during this landing
        landingJumpConsumed = false;

        // Sideflip landing (ma_tjmp2) is a longer animation — give it more lock time
        landingTimer = (landingState == MarioState.tripleJumpLanding) ? 20 : 7;
    }

    private void StartJumpFromLanding()
    {
        isJumping = true;
        initalJumpHold = true;
        jumpHoldTime = 0f;

        // Once we actually jump, consume the chain window
        int stage = (jumpChainTimer > 0) ? jumpChainStage : 0;
        jumpChainStage = 0;
        jumpChainTimer = 0;

        if (spinBuffer > 0)
        {
            spinBuffer = 0;
            StartSpinJump();
            return;
        }

        if (stage == 2)
        {
            // Not fast enough carried in -> no triple jump standing still; fall
            // back to a plain single jump instead (chain already consumed above).
            if (!spinTripleQueued && landingCarryVelXZ.Length() < TRIPLE_JUMP_MIN_SPEED)
            {
                _jumpChainStartDir = GetMoveDirOrFallback();
                stateOfMario = MarioState.singleJump;
                velocity.Y = BASE_JUMP_VELOCITY;
                SetMarioState(stateOfMario);
                return;
            }

            // Locks to the direction the CHAIN started facing, not wherever the
            // stick/current movement happens to be pointing right now.
            Vector3 dirLock = spinTripleQueued ? spinTripleDir : _jumpChainStartDir;
            spinTripleQueued = false;

            lastFacingDirection = dirLock;
            armature.Rotation = new Vector3(0, YawFromDir(dirLock), 0);

            // Optional but recommended: keep speed but align it with the locked dir
            float speedCarry = landingCarryVelXZ.Length();
            velocity.X = dirLock.X * speedCarry;
            velocity.Z = dirLock.Z * speedCarry;
            spinBuffer = 0;
            spinInput = false;
            spinTracking = false;
            stateOfMario = MarioState.tripleJump;
            velocity.Y = TRIPLE_JUMP_VELOCITY;
            SetMarioState(stateOfMario);
            return;
        }

        if (stage == 1)
        {
            stateOfMario = MarioState.doubleJump;
            velocity.Y = BASE_JUMP_VELOCITY;
            SetMarioState(stateOfMario);
            return;
        }

        // Stage 0: a fresh chain starts here.
        _jumpChainStartDir = GetMoveDirOrFallback();
        stateOfMario = MarioState.singleJump;
        velocity.Y = BASE_JUMP_VELOCITY;
        SetMarioState(stateOfMario);
    }

    private bool CanEnterGroundSpin()
    {
        return stateOfMario == MarioState.idle
            || stateOfMario == MarioState.walking
            || stateOfMario == MarioState.running
            || stateOfMario == MarioState.sprinting
            || stateOfMario == MarioState.pivot
            || stateOfMario == MarioState.sneak
            || stateOfMario == MarioState.landing
            || stateOfMario == MarioState.singleJumpLanding
            || stateOfMario == MarioState.doubleJumpLanding
            || stateOfMario == MarioState.tripleJumpLanding;
    }

    private bool CanEnterCrouch()
    {
        return stateOfMario == MarioState.idle
            || stateOfMario == MarioState.walking
            || stateOfMario == MarioState.running
            || stateOfMario == MarioState.sprinting
            || stateOfMario == MarioState.pivot
            || stateOfMario == MarioState.sneak
            || stateOfMario == MarioState.landing;
    }

    private void StartCrouch()
    {
        GD.Print("[DBG] StartCrouch entered");
        velocity.X = 0f;
        velocity.Z = 0f;
        stateOfMario = MarioState.crouch;
        SetMarioState(stateOfMario);
    }

    private void StartBackflip()
    {
        GD.Print("[DBG] StartBackflip launched");
        stateOfMario = MarioState.backFlip;
        _backflipFalling = false;
        _backflipTimer = 0f;
        velocity.Y = BackflipJumpVelocity;

        // Small backward hop relative to current facing; the flip keeps facing forward.
        Vector3 back = lastFacingDirection;
        if (back == Vector3.Zero)
            back = Vector3.Forward;
        back = -back.Normalized();
        velocity.X = back.X * BackflipBackSpeed;
        velocity.Z = back.Z * BackflipBackSpeed;

        // Fixed reference for ApplyBackflipAirControl — captured once here so
        // "backward = good control, forward = limited" stays consistent for the
        // whole flight, regardless of how his velocity gets steered afterward.
        _backflipLaunchDir = back;

        SetMarioState(stateOfMario);
    }

    private static bool IsDiveState(MarioState s)
    {
        return s == MarioState.diving
            || s == MarioState.singleJumpDive
            || s == MarioState.doubleJumpDive
            || s == MarioState.tripleJumpDive
            || s == MarioState.bellySlidingFromDive;
    }

    private bool TryEnterDiveBonkAfterMove(Vector3 impactVelocity, MarioState impactState)
    {
        if (!IsDiveState(impactState) || stateOfMario == MarioState.bonk)
            return false;

        Vector3 velocityXZ = new Vector3(impactVelocity.X, 0f, impactVelocity.Z);
        float speed = velocityXZ.Length();
        if (speed < BonkMinDiveSpeed)
            return false;

        Vector3 travelDirection = velocityXZ / speed;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            Vector3 normal = GetSlideCollision(i).GetNormal();

            // Ignore floors, ceilings, and slopes too shallow to be a real
            // wall — same 75° cutoff as wall-slide/slip (WALL_TRUE_VERTICAL_MIN_ANGLE_DEG),
            // so diving into a too-steep-to-stand slope doesn't bonk, it's
            // just ground he can't stand on.
            if (Mathf.Abs(normal.Y) > Mathf.Cos(Mathf.DegToRad(WALL_TRUE_VERTICAL_MIN_ANGLE_DEG)))
                continue;

            Vector3 normalXZ = new Vector3(normal.X, 0f, normal.Z);
            if (normalXZ.LengthSquared() < 0.000001f)
                continue;

            normalXZ = normalXZ.Normalized();
            if (travelDirection.Dot(-normalXZ) <= 0.25f)
                continue;

            EnterBonk(normalXZ);
            return true;
        }

        return false;
    }

    private void EnterBonk(Vector3 wallNormalXZ)
    {
        StopWaistIKHard();
        stateOfMario = MarioState.bonk;
        _bonkAirborne = true;
        _bonkTimer = 0f;
        _bonkAwayDirection = wallNormalXZ.Normalized();

        // Bounce back off the wall and pop up.
        velocity.X = wallNormalXZ.X * BonkBackSpeed;
        velocity.Z = wallNormalXZ.Z * BonkBackSpeed;
        velocity.Y = BonkUpSpeed;

        // Face the wall he hit (he falls backward away from it).
        Vector3 faceWall = -wallNormalXZ;
        if (faceWall != Vector3.Zero)
        {
            lastFacingDirection = faceWall;
            armature.Rotation = new Vector3(0f, YawFromDir(faceWall), 0f);
        }

        SetMarioState(stateOfMario); // -> ma_bkdwn
        SpawnBonkStars(wallNormalXZ);
    }

    private void TickBonk(double delta)
    {
        if (_bonkAirborne)
        {
            velocity += GRAVITY * (float)delta;
            if (IsOnFloor())
            {
                // Landed: play the get-up animation to completion before control.
                _bonkAirborne = false;
                _bonkTimer = 0f;
                velocity.X = _bonkAwayDirection.X * BonkLandingSlideSpeed;
                velocity.Z = _bonkAwayDirection.Z * BonkLandingSlideSpeed;
                _sm.Travel("ma_sdown");
                TriggerLandingDust();
            }
            return;
        }

        // Get-up phase: hold controls while the short backward skid eases to a stop.
        velocity.X = Mathf.MoveToward(velocity.X, 0f, BonkLandingSlideDeceleration * (float)delta);
        velocity.Z = Mathf.MoveToward(velocity.Z, 0f, BonkLandingSlideDeceleration * (float)delta);
        velocity.Y = -2f;

        _bonkTimer += (float)delta;
        float len = (float)_sm.GetCurrentLength();
        float pos = (float)_sm.GetCurrentPlayPosition();
        bool done = (len > 0.001f && pos >= len - 0.03f) || _bonkTimer > 1.5f;
        if (done)
        {
            velocity.X = 0f;
            velocity.Z = 0f;
            stateOfMario = MarioState.idle;
            SetMarioState(stateOfMario);
        }
    }

    /// <summary>Creates a soft-edged five-point star texture for bonk particles.</summary>
    private static Texture2D CreateBonkStarTexture()
    {
        const int size = 64;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        const float outer = 0.48f;
        const float inner = 0.21f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 d = (new Vector2(x, y) - center) / size;
                float radius = d.Length();
                float angle = Mathf.Atan2(d.Y, d.X) - Mathf.Pi * 0.5f;
                while (angle < 0f)
                    angle += Mathf.Tau;

                float spoke = angle / (Mathf.Tau / 10f);
                int segment = Mathf.FloorToInt(spoke) % 10;
                float frac = spoke - Mathf.Floor(spoke);
                float r0 = (segment % 2 == 0) ? outer : inner;
                float r1 = (segment % 2 == 0) ? inner : outer;
                float edge = Mathf.Lerp(r0, r1, frac);
                float alpha = Mathf.Clamp((edge - radius) * size * 0.75f, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Builds the one-shot star burst used when Mario bonks a wall.</summary>
    private void SetupBonkStars()
    {
        bonkStars = new GpuParticles3D();
        bonkStars.Name = "BonkStars";
        bonkStars.Amount = 10;
        bonkStars.Lifetime = 0.6;
        bonkStars.OneShot = true;
        bonkStars.Explosiveness = 1.0f; // pop all at once
        bonkStars.Emitting = false;
        bonkStars.LocalCoords = false;

        var pm = new ParticleProcessMaterial();
        pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        pm.EmissionSphereRadius = 0.15f;
        pm.Direction = new Vector3(0f, 1f, 0f);
        pm.Spread = 180f;
        pm.InitialVelocityMin = 3.0f;
        pm.InitialVelocityMax = 6.0f;
        pm.Gravity = new Vector3(0f, -12f, 0f);
        pm.ScaleMin = 0.8f;
        pm.ScaleMax = 1.4f;
        pm.AngularVelocityMin = -720f;
        pm.AngularVelocityMax = 720f;
        pm.Color = new Color(1f, 0.9f, 0.2f, 1f); // bright yellow "stars"

        var grad = new Gradient();
        grad.Offsets = new float[] { 0f, 0.7f, 1f };
        grad.Colors = new Color[]
        {
            new Color(1f, 1f, 0.5f, 1f),
            new Color(1f, 0.85f, 0.2f, 1f),
            new Color(1f, 0.8f, 0.15f, 0f),
        };
        var ramp = new GradientTexture1D();
        ramp.Gradient = grad;
        pm.ColorRamp = ramp;

        bonkStars.ProcessMaterial = pm;

        var quad = new QuadMesh();
        quad.Size = new Vector2(0.28f, 0.28f);
        var mat = new StandardMaterial3D();
        var starTexture = CreateBonkStarTexture();
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        mat.VertexColorUseAsAlbedo = true;
        mat.AlbedoColor = new Color(1f, 0.9f, 0.3f, 1f);
        mat.AlbedoTexture = starTexture;
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.85f, 0.25f);
        mat.EmissionEnergyMultiplier = 5f;
        mat.SetTexture(BaseMaterial3D.TextureParam.Emission, starTexture);
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        quad.Material = mat;
        bonkStars.DrawPass1 = quad;

        AddChild(bonkStars);
        bonkStars.Position = new Vector3(0f, 1.2f, 0f);
    }

    private void SpawnBonkStars(Vector3 wallNormalXZ)
    {
        if (bonkStars == null)
            return;
        // Emit from around Mario's head, nudged toward the wall he hit.
        Vector3 toward = -wallNormalXZ * 0.4f;
        bonkStars.Position = new Vector3(toward.X, 1.2f, toward.Z);
        bonkStars.Restart();
        bonkStars.Emitting = true;
    }

    private void StartGroundSpin()
    {
        StopWaistIKHard();
        if (animPlayer != null)
            animPlayer.SpeedScale = 1f; // match SpinJump animation playback rate
        spinBuffer = 0;
        spinInput = false;
        spinTracking = false;
        groundSpinTimer = GroundSpinDuration;
        velocity.X = 0f;
        velocity.Z = 0f;
        stateOfMario = MarioState.groundSpin;
        SetMarioState(stateOfMario);
        isSpining(true);
        if (footSparks != null)
            footSparks.Emitting = true;
    }

    private void StartSpinJump()
    {
        StopWaistIKHard(); // <-- add this
        if (footSparks != null)
            footSparks.Emitting = false; // grounded-spin sparks only
        stateOfMario = MarioState.SpinJump;
        velocity.Y = TRIPLE_JUMP_VELOCITY;
        SetMarioState(stateOfMario);

        spinJumpTakeoffDir = GetSpinLockDir();
        spinJumpDirLocked = true;
        LockAirTakeoffDir(spinJumpTakeoffDir);

        LockAirTakeoffDir(spinJumpTakeoffDir);

        lastFacingDirection = spinJumpTakeoffDir;

        spinJumpLockedYaw = BaseYawFromDir(spinJumpTakeoffDir);
        armature.Rotation = new Vector3(0f, spinJumpLockedYaw, 0f);

        isSpining(true);
    }

    private bool CanStartSpinInAir()
    {
        return stateOfMario != MarioState.SpinJump
            && stateOfMario != MarioState.singleRollout
            && stateOfMario != MarioState.tripleJump
            && stateOfMario != MarioState.tripleJumpDive
            && stateOfMario != MarioState.singleJumpDive
            && stateOfMario != MarioState.doubleJumpDive
            && stateOfMario != MarioState.diving
            && stateOfMario != MarioState.ledgeFall
            && stateOfMario != MarioState.groundPoundStartup
            && stateOfMario != MarioState.groundPoundFalling
            && stateOfMario != MarioState.backFlip
            && stateOfMario != MarioState.bonk;
    }

    private void StartSpinJumpFromAir()
    {
        StopWaistIKHard(); // <-- add this
        stateOfMario = MarioState.SpinJump;
        SetMarioState(stateOfMario);

        spinJumpTakeoffDir = GetMoveDirOrFallback();
        spinJumpDirLocked = true;
        LockAirTakeoffDir(spinJumpTakeoffDir);

        lastFacingDirection = spinJumpTakeoffDir;

        // IMPORTANT: snap base yaw so the spin starts from a known facing
        armature.Rotation = new Vector3(0f, BaseYawFromDir(spinJumpTakeoffDir), 0f);

        isSpining(true);
    }

    private Vector3 GetSpinLockDir()
    {
        // Prefer actual movement direction in air
        Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
        if (velXZ.Length() > 0.05f)
        {
            Vector3 v = new Vector3(velocity.X, 0, velocity.Z).Normalized();
            return v;
        }

        // Otherwise fall back to stick direction or last facing
        Vector3 d = (direction != Vector3.Zero) ? direction : lastFacingDirection;
        if (d == Vector3.Zero)
            d = Vector3.Forward;
        return d.Normalized();
    }

    private bool QualifiesForSpinTripleOnLanding(Vector3 inputDirWorld, float inputStrength)
    {
        // Must be holding a direction (no neutral)
        if (inputStrength < SPIN_TRIPLE_MIN_INPUT || inputDirWorld == Vector3.Zero)
            return false;

        // Must be going fast enough
        float speedXZ = new Vector2(velocity.X, velocity.Z).Length();
        if (speedXZ < SPIN_TRIPLE_MIN_SPEED)
            return false;

        // Must still be generally same direction as the spin locked direction
        Vector3 desired = inputDirWorld.Normalized();
        Vector3 locked = spinJumpTakeoffDir; // set when SpinJump started
        if (locked == Vector3.Zero)
            return false;

        float dot = desired.Dot(locked.Normalized());
        float dotThreshold = Mathf.Cos(Mathf.DegToRad(SPIN_TRIPLE_MAX_ANGLE));
        return dot >= dotThreshold;
    }

    private Vector3 GetMoveDirOrFallback()
    {
        // Prefer actual movement (works for sideFlip, slopes, etc.)
        Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
        if (velXZ.Length() > 0.1f)
            return new Vector3(velocity.X, 0, velocity.Z).Normalized();

        // Fall back to stick direction
        if (direction != Vector3.Zero)
            return direction.Normalized();

        // Fall back to last known facing
        if (lastFacingDirection != Vector3.Zero)
            return lastFacingDirection.Normalized();

        return Vector3.Forward;
    }

    private float YawFromDir(Vector3 dir)
    {
        return Mathf.Atan2(-dir.X, -dir.Z);
    }

    private static float WrapAnglePi(float a)
    {
        // Wrap to [-PI, PI]
        return Mathf.PosMod(a + Mathf.Pi, Mathf.Tau) - Mathf.Pi; // Tau = 2*PI
    }

    private void UpdateSpinInput(Vector2 stick)
    {
        // cooldown prevents double-trigger from one spin
        if (spinCooldown > 0)
        {
            spinCooldown--;
            return;
        }

        // Start tracking when the player gives ANY directional intent (no radius),
        // but ignore ambiguous "on-axis" input.
        bool validForQuadrant = Mathf.Abs(stick.X) > AXIS_EPS && Mathf.Abs(stick.Y) > AXIS_EPS;

        if (!spinQuadTracking)
        {
            if (!validForQuadrant)
                return;

            spinQuadTracking = true;
            spinFramesLeft = WINDOW_FRAMES;
            lastQuadrant = -1;
            for (int i = 0; i < quadrantVisited.Length; i++)
                quadrantVisited[i] = false;
        }

        // Tracking: always consume time (even if stick goes to center), so you can’t “pause” forever
        spinFramesLeft--;
        if (spinFramesLeft <= 0)
        {
            spinQuadTracking = false;
            return;
        }

        if (!validForQuadrant)
            return; // don't mark anything while on-axis/center-ish

        int q = GetQuadrant(stick);

        // Optional: only mark when quadrant changes (prevents spamming same quadrant)
        if (q != lastQuadrant)
        {
            quadrantVisited[q] = true;
            lastQuadrant = q;
        }

        if (AllQuadrantsVisited())
        {
            spinBuffer = SPIN_BUFFER_FRAMES; // <-- this is your "spinInput lasts 20 frames"
            spinCooldown = SPIN_COOLDOWN_FRAMES; // optional, avoids immediate retrigger
            spinQuadTracking = false;
        }
    }

    private void EnterRolloutRun(Vector3 inputDirWorld, float stickStrength)
    {
        // Grounded: kill vertical
        velocity.Y = 0f;

        stateOfMario = MarioState.rolloutRun;

        // Arm the uphill slope boost for this rollout. Starts at zero and is
        // fed by UpdateRolloutSlopeBoost only while he's actually driving up a
        // ramp; survives the handoff into running/sprinting.
        _rolloutBoostArmed = true;
        _rolloutSlopeBoost = 0f;
        GD.Print( // TEMP DIAGNOSTIC
            $"[BOOST] ARMED via EnterRolloutRun  speed={new Vector2(velocity.X, velocity.Z).Length():F2}"
        );

        // Decide initial facing (prefer stick if held, else keep momentum direction)
        Vector3 faceDir = Vector3.Zero;

        if (stickStrength > DEADZONE && inputDirWorld != Vector3.Zero)
        {
            faceDir = inputDirWorld.Normalized();
        }
        else
        {
            Vector2 v = new Vector2(velocity.X, velocity.Z);
            if (v.Length() > 0.1f)
                faceDir = new Vector3(velocity.X, 0, velocity.Z).Normalized();
            else if (lastFacingDirection != Vector3.Zero)
                faceDir = lastFacingDirection.Normalized();
            else
                faceDir = Vector3.Forward;
        }

        lastFacingDirection = faceDir;
        armature.Rotation = new Vector3(0f, YawFromDir(faceDir), 0f);

        // Play run immediately (no landing anim)
        SetMarioState(stateOfMario);
    }

    /// <summary>
    /// Feeds <see cref="_rolloutSlopeBoost"/>, the surplus added to RUN_SPEED
    /// while a rollout is driving UP a ramp.
    ///
    /// The boost can't live inside the rolloutRun state itself: HandleRolloutRun
    /// hands off to running/sprinting the instant the stick is held, which is
    /// exactly when you'd be steering up the ramp. So it's armed on rollout
    /// landing and survives into normal running, gating the ground speed cap
    /// until the ground goes flat.
    ///
    /// Uphill-only by design. The floor normal's horizontal component points
    /// DOWNhill, so uphill is its negation; the dot against his heading means a
    /// diagonal line up the ramp earns proportionally less than a straight one,
    /// and heading downhill earns nothing (clamped at 0) rather than draining it.
    /// </summary>


    private void UpdateRolloutSlopeBoost(double delta)
    {
        if (!_rolloutBoostArmed)
        {
            _rolloutSlopeBoost = 0f;
            // Deliberately silent: this is the steady state for the whole game.
            // Logging it every 6 frames floods the 2000-line ring buffer and
            // evicts the rollout frames we're actually trying to read.
            return;
        }

        // Airborne (jumped off the ramp, launched off a lip): hold whatever he
        // built rather than feeding or cutting it, so landing back on the same
        // ramp continues instead of restarting from zero.
        if (!IsOnFloor())
        {
            return;
        }

        if (stateOfMario == MarioState.idle)
        {
            _rolloutSlopeBoost = 0f;
            _rolloutBoostArmed = false;
            return;
        }

        Vector3 nXZ = new Vector3(GetFloorNormal().X, 0f, GetFloorNormal().Z);
        float steepness = nXZ.Length(); // == sin(slope angle)

        if (steepness < RolloutSlopeFlatThreshold)
        {
            // Back on flat ground — cut immediately, not a fade.
            _rolloutSlopeBoost = 0f;
            _rolloutBoostArmed = false;
            return;
        }

        Vector3 heading = new Vector3(direction.X, 0f, direction.Z);
        if (heading.LengthSquared() < 0.0001f)
        {
            // Standing still on the ramp: hold what he earned rather than
            // draining it. Only flat ground takes the boost away.
            return;
        }

        Vector3 uphill = -nXZ.Normalized();
        float uphillness = Mathf.Max(0f, heading.Normalized().Dot(uphill));

        // RATCHET — the boost only ever climbs while he's on a slope. Turning
        // round and running back DOWN, or cutting across, holds whatever he
        // built instead of bleeding it off; the single thing that clears it is
        // reaching flat ground (the DISARM flat branch above).
        //
        // Without this, uphillness collapses to 0 the instant he turns and the
        // speed evaporated under him — visible in the log as
        // "up=0.00 boost=0.00" while still standing on a 33.8° face.
        float target = RolloutSlopeMaxBoost * steepness * uphillness;
        if (target > _rolloutSlopeBoost)
        {
            _rolloutSlopeBoost = Mathf.MoveToward(
                _rolloutSlopeBoost,
                target,
                RolloutSlopeBoostAccel * (float)delta
            );
        }
    }

    private bool HandleRolloutRun(double delta, Vector3 inputDirWorld, float stickStrength)
    {
        // Jump out of rolloutRun immediately (before we decelerate XZ)
        if (jumpBuffer > 0)
        {
            StartGroundJumpPreserveXZ();
            return true; // rolloutRun owns this frame, but we changed state to air
        }

        // If stick held -> hand off to normal running/sprinting logic
        if (stickStrength > DEADZONE && inputDirWorld != Vector3.Zero)
        {
            // make sure your normal ground logic sees the input
            direction = inputDirWorld;
            walkingStrength = stickStrength;

            // IMPORTANT: exit rolloutRun state so you don't get stuck
            stateOfMario = (stickStrength > 0.5f) ? MarioState.sprinting : MarioState.running;

            // Don't fully own this frame anymore; let the rest of ground code run now
            return false;
        }

        // Stick neutral -> rolloutRun owns movement + anim while coasting to stop
        velocity.X = Mathf.Lerp(velocity.X, 0f, ROLLOUT_RUN_DECEL);
        velocity.Z = Mathf.Lerp(velocity.Z, 0f, ROLLOUT_RUN_DECEL);

        float speedXZ = new Vector2(velocity.X, velocity.Z).Length();

        if (speedXZ > ROLLOUT_STOP_SPEED)
        {
            SetMarioState(stateOfMario);
            RotateArmature();
            return true; // keep owning this frame (prevents landing/idle logic fighting you)
        }

        // Fully stopped -> go idle
        velocity.X = 0f;
        velocity.Z = 0f;
        stateOfMario = MarioState.idle;
        animPlayer.Play("ma_wait");
        SetMarioState(stateOfMario);
        return true; // we already decided everything; skip the rest this frame
    }

    private bool HandlePivot(double delta, Vector3 aimDirWorld, float stickStrength)
    {
        // If big input, let normal run logic take over
        if (stickStrength >= DEADZONE)
            return false;

        // If stick basically neutral, exit pivot (if we were pivoting) and let normal idle logic run
        if (stickStrength < PIVOT_MIN_INPUT || aimDirWorld == Vector3.Zero)
        {
            if (stateOfMario == MarioState.pivot)
            {
                stateOfMario = MarioState.idle;
                animPlayer.Play("ma_wait");
                SetMarioState(stateOfMario);
            }
            return false;
        }

        // Only pivot in the small-input band
        if (stickStrength > PIVOT_MAX_INPUT)
            return false;

        // Optional: only allow pivot when basically stopped
        float speedXZ = new Vector2(velocity.X, velocity.Z).Length();
        if (speedXZ > 2.0f && stateOfMario != MarioState.pivot)
            return false;

        // Jump from ground
        if (Input.IsActionJustPressed("key_space") || Input.IsActionJustPressed("button_a"))
        {
            jumpChainStage = 0;
            jumpChainTimer = 0;
            isJumping = true;

            // 1) SPIN JUMP if buffered
            if (spinBuffer > 0)
            {
                spinBuffer = 0;
                StartSpinJump();
                return true;
            }
            // 2) SIDEFLIP if you're in the turning setup
            else if (stateOfMario == MarioState.sideFlipTurning)
            {
                Vector3 lockDir =
                    (sideFlipStoredDir != Vector3.Zero)
                        ? sideFlipStoredDir
                        : GetMoveDirOrFallback();
                lockDir = lockDir.Normalized();

                sideFlipTakeoffDir = lockDir;
                sideFlipLockedYaw = YawFromDir(lockDir) + SIDEFLIP_YAW_OFFSET;
                sideFlipLockedYaw = Mathf.Wrap(sideFlipLockedYaw, -Mathf.Pi, Mathf.Pi);
                sideFlipDirLocked = true;

                armature.Rotation = new Vector3(0f, sideFlipLockedYaw, 0f);
                lastFacingDirection = lockDir;

                stateOfMario = MarioState.sideFlip;
                velocity.Y = SIDEFLIP_JUMP_VELOCITY;
                SetMarioState(stateOfMario);
                return true;
            }
            // 3) otherwise normal jump
            else
            {
                stateOfMario = MarioState.singleJump;
                velocity.Y = BASE_JUMP_VELOCITY;
                SetMarioState(stateOfMario);
                return true;
            }
        }

        // Enter/maintain pivot
        if (stateOfMario != MarioState.pivot)
        {
            stateOfMario = MarioState.pivot;
            SetMarioState(stateOfMario);
        }
        else
        {
            if (animPlayer.CurrentAnimation != "ma_pivot")
                SetMarioState(stateOfMario);
        }

        // NO movement: kill XZ
        velocity.X = Mathf.Lerp(velocity.X, 0f, PIVOT_STOP_DECEL);
        velocity.Z = Mathf.Lerp(velocity.Z, 0f, PIVOT_STOP_DECEL);

        // Face-only: rotate toward aim direction
        Vector3 dir = aimDirWorld.Normalized();
        lastFacingDirection = dir;

        float targetYaw = YawFromDir(dir);
        float t = Mathf.Clamp((float)(PIVOT_TURN_SPEED * delta), 0f, 1f);
        float newYaw = Mathf.LerpAngle(armature.Rotation.Y, targetYaw, t);
        armature.Rotation = new Vector3(0f, newYaw, 0f);

        return true; // pivot owns this frame (prevents idle/run code overriding it)
    }

    private void EnterSideFlipTurning(Vector3 newDir)
    {
        if (newDir == Vector3.Zero)
            newDir = lastFacingDirection;
        if (newDir == Vector3.Zero)
            newDir = Vector3.Forward;
        newDir = newDir.Normalized();

        stateOfMario = MarioState.sideFlipTurning;
        sideFlipTurningTimer = 33;

        sideFlipStoredDir = newDir;
        sideFlipLockedYaw = YawFromDir(newDir) + SIDEFLIP_YAW_OFFSET; // turning anim authored backward
        sideFlipDirLocked = true;

        armature.Rotation = new Vector3(0f, sideFlipLockedYaw, 0f);
        lastFacingDirection = newDir;

        if (animPlayer.CurrentAnimation != "ma_trned")
            SetMarioState(MarioState.sideFlipTurning);
    }

    private void StopWaistIKHard()
    {
        if (skeletonIK3DWaist != null)
            skeletonIK3DWaist.Stop();

        // IMPORTANT: clear any pose overrides left by IK this frame
        if (skeleton != null)
            skeleton.ClearBonesGlobalPoseOverride();
    }

    /// <summary>Restore Mario's OPEN hands — undoes the closed-hand swap the
    /// sprint state does (transparent open-hand material + visible closed
    /// meshes). Used by the shine-get so he holds the shine with an open hand.</summary>
    private void ShowOpenHands() => ApplyHandPose(HandPose.Open);

    /// <summary>Picks the hand mesh from Mario's current state. Open by default,
    /// clenched while sprinting or airborne off a jump.
    ///
    /// Single owner for hand visibility. It used to be swapped inline when
    /// sprinting started, with nothing swapping it back, so his hands stayed
    /// clenched forever after the first sprint — and the third "slightly open"
    /// variant was never touched by anything, leaving it rendering on top of
    /// whichever hand was actually selected.</summary>
    private void UpdateHandPose()
    {
        bool clenched =
            stateOfMario == MarioState.sprinting
            || stateOfMario == MarioState.singleJump
            || stateOfMario == MarioState.doubleJump
            || stateOfMario == MarioState.tripleJump
            || stateOfMario == MarioState.sideFlip
            || stateOfMario == MarioState.backFlip
            || stateOfMario == MarioState.SpinJump
            || stateOfMario == MarioState.wallJump;

        ApplyHandPose(clenched ? HandPose.Closed : HandPose.Open);
    }

    /// <summary>Makes exactly one hand variant visible. Idempotent — early-outs
    /// unless the pose actually changed, so it's safe to call every frame.
    ///
    /// The open hands are SURFACES on Mario's main mesh rather than separate
    /// nodes, so they're hidden by zeroing their albedo alpha instead of a
    /// Visible flag; the other two are instanced scenes on the hand bones.</summary>
    private void ApplyHandPose(HandPose pose)
    {
        if (_handPose == pose)
            return;
        _handPose = pose;

        bool openVisible = pose == HandPose.Open;
        Color openAlbedoR = openVisible ? _rightHandOpenAlbedo : new Color(0, 0, 0, 0);
        Color openAlbedoL = openVisible ? _leftHandOpenAlbedo : new Color(0, 0, 0, 0);

        if (RightHandMaterial != null)
            RightHandMaterial.AlbedoColor = openAlbedoR;
        if (LeftHandMaterial != null)
            LeftHandMaterial.AlbedoColor = openAlbedoL;

        bool closed = pose == HandPose.Closed;
        if (RightClosedHand != null)
            RightClosedHand.Visible = closed;
        if (LeftClosedHand != null)
            LeftClosedHand.Visible = closed;

        bool slightly = pose == HandPose.SlightlyOpen;
        if (RightSlightlyOpenHand != null)
            RightSlightlyOpenHand.Visible = slightly;
        if (LeftSlightlyOpenHand != null)
            LeftSlightlyOpenHand.Visible = slightly;
    }

    private bool CanStartWallSlide()
    {
        if (IsOnFloor())
            return false;
        if (!IsOnWall())
            return false;
        if (wallRegrabCooldown > 0)
            return false;

        // Don't allow from these states
        if (stateOfMario == MarioState.bellySlidingFromDive)
            return false;
        if (
            stateOfMario == MarioState.diving
            || stateOfMario == MarioState.singleJumpDive
            || stateOfMario == MarioState.doubleJumpDive
            || stateOfMario == MarioState.tripleJumpDive
        )
            return false;

        Vector3 n = GetWallNormal();
        if (n == Vector3.Zero)
            return false;

        // Only a genuinely near-vertical surface counts as a real wall to
        // kick off of. Godot's own floor/wall split is a single threshold
        // (floor_max_angle), so a merely-steep ramp that fails the floor
        // check still comes through here as "wall" — that's CanStartSlip's
        // job, not a real wall-jump.
        if (Mathf.RadToDeg(n.AngleTo(Vector3.Up)) < WALL_TRUE_VERTICAL_MIN_ANGLE_DEG)
            return false;

        // Per-state entry requirements
        GetWallSlideEntryParams(stateOfMario, out float minSpeed, out float minApproachDot);

        Vector3 nXZ = new Vector3(n.X, 0, n.Z);
        if (nXZ.Length() < 0.001f)
            return false;
        nXZ = nXZ.Normalized();

        Vector3 vXZ = new Vector3(velocity.X, 0, velocity.Z);
        float speed = vXZ.Length();
        if (speed < minSpeed)
            return false;

        // Extra anti-cling: if you're mostly just falling and barely moving into the wall, don't grab
        float fall = Mathf.Abs(velocity.Y);
        if (fall > 1.0f && (speed / fall) < WALL_MIN_HORIZONTAL_RATIO)
            return false;

        Vector3 vDir = vXZ / speed;
        // --- Takeoff cone restriction ---
        // Prevent wall-slide if we hit a wall from a direction too far from our takeoff direction.
        // Skip cone check for wallJump — player is intentionally chaining wall jumps.
        if (airTakeoffDirValid && stateOfMario != MarioState.wallJump)
        {
            float coneDeg = WALL_TAKEOFF_CONE_DEG_DEFAULT;

            if (!WithinConeXZ(vDir, airTakeoffDirXZ, coneDeg))
                return false;
        }

        // approaching INTO wall (align with -normal)
        float approach = vDir.Dot(-nXZ);

        return approach >= minApproachDot;
    }

    private bool CanStartSlip()
    {
        if (IsOnFloor())
            return false;
        if (!IsOnWall())
            return false;

        if (stateOfMario == MarioState.bellySlidingFromDive)
            return false;
        if (
            stateOfMario == MarioState.diving
            || stateOfMario == MarioState.singleJumpDive
            || stateOfMario == MarioState.doubleJumpDive
            || stateOfMario == MarioState.tripleJumpDive
        )
            return false;

        Vector3 n = GetWallNormal();
        if (n == Vector3.Zero)
            return false;

        // Steep ground, not a real wall: Godot already rejected this as
        // floor (angle > floor_max_angle), but it's still short of true-wall
        // steepness — that's the slip band.
        return Mathf.RadToDeg(n.AngleTo(Vector3.Up)) < WALL_TRUE_VERTICAL_MIN_ANGLE_DEG;
    }

    private void EnterSlip(Vector3 incomingVel)
    {
        isSpining(false);

        stateOfMario = MarioState.slipping;
        slipLostSurfaceFrames = 0;
        slipSlideVel = Vector3.Zero;

        lastSlipNormal = GetWallNormal();
        if (lastSlipNormal == Vector3.Zero)
            lastSlipNormal = -lastFacingDirection;

        Vector3 nXZ = new Vector3(lastSlipNormal.X, 0, lastSlipNormal.Z);
        Vector3 downhillXZ = (nXZ.Length() > 0.001f) ? nXZ.Normalized() : -lastFacingDirection;
        Vector3 uphillXZ = -downhillXZ;

        // ma_slpbk (slip back) if he was facing/moving UP the slope when he
        // hit the too-steep patch — slides backward, keeps facing uphill.
        // ma_slip (forward) otherwise — reorients to face downhill and
        // slides forward. Locked for the whole episode so the animation
        // doesn't flip mid-slide.
        Vector3 approachXZ = new Vector3(incomingVel.X, 0, incomingVel.Z);
        Vector3 refDir =
            (approachXZ.Length() > 0.5f) ? approachXZ.Normalized() : lastFacingDirection;
        _slipFacingUphill = refDir.Dot(downhillXZ) < -0.3f;

        Vector3 faceDir = _slipFacingUphill ? uphillXZ : downhillXZ;
        if (faceDir != Vector3.Zero)
        {
            lastFacingDirection = faceDir;
            armature.Rotation = new Vector3(0f, YawFromDir(faceDir), 0f);
        }

        // Straight into the slide loop — ma_slpla is not an entry/landing
        // animation, it's what plays when LEAVING slip for airborne (sliding
        // off the edge, or the escape hop), see TickSlip's exit paths.
        SetMarioState(MarioState.slipping);

        if (velocity.Y > 0f)
            velocity.Y = 0f;
    }

    private void TickSlip(double delta, Vector3 inputDirWorld, float stickStrength)
    {

        Vector3 n = IsOnWall() ? GetWallNormal() : lastSlipNormal;
        if (n == Vector3.Zero)
            n = lastSlipNormal;

        Vector3 nXZ = new Vector3(n.X, 0, n.Z);
        if (nXZ.Length() < 0.001f)
            nXZ = new Vector3(lastSlipNormal.X, 0, lastSlipNormal.Z);
        Vector3 downhillXZ = (nXZ.Length() > 0.001f) ? nXZ.Normalized() : Vector3.Zero;

        slideDustFx?.Emit(GlobalPosition - n.Normalized() * capRadiusWorld, Vector3.Up);

        // Slide DOWN the slope, along its actual surface — a fixed
        // horizontal speed plus separately-clamped vertical fall doesn't
        // point along the real surface (especially at steeper angles), so
        // MoveAndSlide read the mismatch as him moving away from it and
        // ejected him instead of sliding him along it. Project straight-down
        // onto the slope's tangent plane instead: for a near-vertical
        // surface this is mostly vertical, for a shallow one mostly
        // horizontal, always parallel to the actual face.
        Vector3 fullNormal = (n.LengthSquared() > 0.0001f) ? n.Normalized() : Vector3.Up;
        Vector3 slopeDownDir = Vector3.Down - Vector3.Down.Dot(fullNormal) * fullNormal;
        if (slopeDownDir.LengthSquared() > 0.0001f)
            slopeDownDir = slopeDownDir.Normalized();

        // Speed accelerates from rest toward a steepness-scaled top speed —
        // nXZ's length (before normalizing to downhillXZ) is sin(angle from
        // Up): 0 at flat, 1 at a vertical wall. Both the ceiling AND the
        // ramp-up rate scale with it, so a steeper slope both slides faster
        // AND gets there quicker, rather than snapping straight to a target
        // speed. This isn't just for feel: a future "slip zone" mechanic
        // (forcing a slip regardless of real geometry, as a "you shouldn't
        // be standing here" catch-all) will hand this an arbitrary angle, so
        // the speed needs to genuinely respond to steepness rather than
        // being a flat constant that only happened to look right for the
        // narrow 65-75 band real slopes currently use.
        float steepness = nXZ.Length();

        // The slide is a VECTOR in the slope's tangent plane, not a scalar
        // speed pinned to the fall line — that's what makes steering possible.
        //
        // Curved surfaces (and moving platforms) turn the normal under him, so
        // re-flatten last frame's slide onto the CURRENT tangent plane before
        // integrating, or the carried velocity slowly drifts out of the face.
        slipSlideVel -= fullNormal * slipSlideVel.Dot(fullNormal);

        // Steepness curve — see SlipSteepnessCurve. At 1.0 this is just
        // sin(angle), i.e. true physics.
        float slopeFactor =
            SlipSteepnessCurve == 1.0f
                ? steepness
                : Mathf.Pow(steepness, SlipSteepnessCurve);

        // Gravity down the fall line, now derived from ACTUAL gravity rather
        // than a hand-picked constant, so the pull genuinely tracks the slope.
        Vector3 accel = slopeDownDir * (GRAVITY.Length() * SlipGravityScale * slopeFactor);

        // Stick steering, modelled on SMS's doSliding(): the stick contributes
        // an ACCELERATION to the existing slide rather than overriding its
        // direction, so he carves round gradually instead of snapping to the
        // held direction. Flattened onto the tangent plane too, so steering can
        // never push him into the surface or peel him off it.
        if (stickStrength > DEADZONE && inputDirWorld != Vector3.Zero)
        {
            Vector3 steer = inputDirWorld.Normalized();
            steer -= fullNormal * steer.Dot(fullNormal);
            if (steer.LengthSquared() > 0.0001f)
                accel += steer.Normalized() * (SlipSteerAccel * stickStrength);
        }

        slipSlideVel += accel * (float)delta;

        // Hard guarantee: steering can slow the descent but never reverse it.
        // He's on ground he physically can't stand on, so uphill progress has to
        // stay impossible however SlipSteerAccel is tuned — otherwise a steep
        // face becomes climbable by just holding up.
        float alongDown = slipSlideVel.Dot(slopeDownDir);
        if (alongDown < 0f)
            slipSlideVel -= slopeDownDir * alongDown;

        // Same steepness-scaled ceiling as before, now capping the whole 2D
        // slide instead of a scalar — so steering REDIRECTS speed rather than
        // stacking on top of it, and a diagonal carve can't outrun a straight
        // fall-line drop.
        float maxSpeed = SlipMaxSpeed * slopeFactor;
        if (slipSlideVel.Length() > maxSpeed)
            slipSlideVel = slipSlideVel.Normalized() * maxSpeed;

        velocity = slipSlideVel;
        // Small adhesion push into the surface, same idea as wall-slide's,
        // so MoveAndSlide keeps recognizing contact frame to frame instead
        // of drifting off it.
        velocity -= fullNormal * WALL_STICK_SPEED;

        // Safety net only. Clamped against the slide's own cap so it can never
        // bind tighter than maxSpeed — on a steep face the slide is nearly all
        // vertical, so a fall limit above the slide speed would throttle the
        // slide itself rather than acting as a backstop (the old -13 did).
        // Mathf.Min on two negatives picks the more permissive one.
        float fallFloor = Mathf.Min(SlipMaxFallSpeed, -(maxSpeed + WALL_STICK_SPEED + 1f));
        if (velocity.Y < fallFloor)
            velocity.Y = fallFloor;

        // Face the way he's ACTUALLY sliding, not the raw fall line, so the
        // steering reads on the model. Which of ma_slip / ma_slpbk is playing
        // stays locked from entry (_slipFacingUphill), so this only rotates
        // him within that choice rather than flipping the animation mid-slide.
        Vector3 headingXZ = new Vector3(slipSlideVel.X, 0, slipSlideVel.Z);
        headingXZ =
            headingXZ.LengthSquared() > 0.01f ? headingXZ.Normalized() : downhillXZ;
        Vector3 faceDir = _slipFacingUphill ? -headingXZ : headingXZ;
        if (faceDir != Vector3.Zero)
        {
            float targetYaw = YawFromDir(faceDir);
            float t = Mathf.Clamp((float)(WALL_SLIDE_FACE_SPEED * delta), 0f, 1f);
            float newYaw = Mathf.LerpAngle(armature.Rotation.Y, targetYaw, t);
            armature.Rotation = new Vector3(0f, newYaw, 0f);
            lastFacingDirection = faceDir;
        }

        // Escape hop only — a plain vertical pop, not a directional
        // wall-kick, since this was never a real wall to push off of.
        if (Input.IsActionJustPressed("button_a"))
        {
            velocity.Y = SLIP_HOP_UP_VEL;
            stateOfMario = MarioState.singleJump;
            slipAirLaunchTimer = SLIP_LAND_DURATION; // ma_slpla plays instead of ma_jump for this leave-slip beat
            SetMarioState(stateOfMario);
        }
    }

    /// <summary>
    /// Purely visual — while diving, belly-sliding, or slipping on ANY
    /// slope (static ramp or moving platform alike), tilts the armature so
    /// his belly lies flush against the actual surface instead of always
    /// staying world-upright while the ground underneath is tilted. Aligns
    /// the armature's up axis to the current contact normal each frame,
    /// keeping the facing direction that state's own logic already set.
    /// Never touches GlobalPosition, velocity, or collision. Runs last in
    /// _PhysicsProcess so it layers on top instead of being overwritten by
    /// a later yaw-set call the same frame. Leaving one of these states
    /// explicitly snaps back to pure yaw immediately — some destinations
    /// (rollout) don't do their own armature.Rotation overwrite, so the
    /// tilt would otherwise persist incorrectly.
    /// </summary>
    private bool _wasPlatformTilting = false;
    private Basis _tiltCurrentRotation = Basis.Identity; // smoothed PURE rotation only — mirror/scale applied fresh each frame, never fed back in

    /// <summary>Flat sink toward the surface (metres) for the PRONE states
    /// (dive / belly slide), on top of the shared slope term. Covers the prone
    /// animation's own belly-above-origin height — the part that does NOT vary
    /// with slope. Tune on FLAT ground, where the slope term is zero and this
    /// is the only thing acting. Purely visual: the collider is untouched, so
    /// it can't push him through geometry.</summary>
    [Export]
    public float BellyTiltSurfaceOffset = 0.15f;

    /// <summary>Same idea as BellyTiltSurfaceOffset but for slipping, where
    /// he's upright on his feet rather than prone, so the pose sits differently
    /// against the surface and wants its own flat term. Defaults to 0 — slip
    /// only ever happens on steep ground, so the shared slope term below is
    /// doing nearly all the work there and this is just trim.</summary>
    [Export]
    public float SlipTiltSurfaceOffset = 0f;

    /// <summary>Scales the geometric slope compensation, which is
    /// capRadiusWorld * sin(slope angle). This one is shared by ALL the tilt
    /// states because it comes from the COLLIDER, not the pose: a vertical
    /// cylinder resting against an inclined plane always contacts on its bottom
    /// rim, whatever the angle, leaving the body origin exactly that far clear
    /// of the surface. 1.0 is the true geometric value; 0 disables it. Only
    /// worth moving off 1.0 if a pose's own shape makes the exact value read
    /// wrong at steep angles.</summary>
    [Export]
    public float TiltSlopeCompensation = 1.0f;

    private void UpdatePlatformTilt(double delta)
    {
        bool wantsTilt =
            stateOfMario == MarioState.diving
            || stateOfMario == MarioState.bellySlidingFromDive
            || stateOfMario == MarioState.slipping;

        if (!wantsTilt)
        {
            if (_wasPlatformTilting)
            {
                armature.Rotation = new Vector3(0f, YawFromDir(lastFacingDirection), 0f);
                armature.Position = armatureBaseLocalPos; // undo the belly sink
            }
            _wasPlatformTilting = false;
            return;
        }

        Vector3 normal;
        if (IsOnFloor())
            normal = GetFloorNormal();
        else if (IsOnWall())
            normal = GetWallNormal();
        else
            return; // airborne mid-dive — no surface to lie against

        if (normal == Vector3.Zero)
            return;
        if (lastFacingDirection == Vector3.Zero)
            return;

        // Normalize once up front — the sink below relies on |normal.xz| being
        // exactly sin(slope angle), which only holds for a unit normal.
        normal = normal.Normalized();

        // Start from the SAME yaw-only basis every other state already uses
        // (proven correct-handedness for this mesh), then lay him onto the
        // surface with a PITCH followed by a ROLL, rather than one shortest-arc
        // rotation from Up to the normal. Shortest-arc rotates about Up × normal,
        // an axis with no relationship to his heading, so it drags the heading
        // sideways as it tilts. Pitch-then-roll reaches the same final up axis
        // while leaving the heading exactly where the state machine put it.
        Basis yawBasis = new Basis(Vector3.Up, YawFromDir(lastFacingDirection));
        Vector3 right = yawBasis.X;

        Vector3 normalInPitchPlane = normal - normal.Dot(right) * right;
        if (normalInPitchPlane.LengthSquared() < 0.0001f)
            return; // normal is (near-)parallel to right — no well-defined pitch

        // PITCH — lean along his facing axis, like a plane pitching with level
        // wings. This alone handles a slope he's sliding straight down.
        float pitchAngle = Vector3.Up.SignedAngleTo(normalInPitchPlane.Normalized(), right);

        // ROLL — the rest of the alignment. Pitch only tilts him forward/back,
        // so sliding diagonally across a slope, or riding a platform that tips
        // sideways, still left one side lifted clear of the surface.
        //
        // After the pitch his up is normalInPitchPlane and {right, upPitched,
        // rollAxis} is orthonormal. The full normal has NO component along
        // rollAxis (it is normalInPitchPlane plus a pure `right` term, and both
        // of those are perpendicular to rollAxis), so it lies in the plane the
        // roll sweeps through — one roll about his own forward axis therefore
        // carries upPitched exactly onto the normal, giving belly-flush contact.
        // Rolling about his forward axis is also precisely what protects the
        // heading: a rotation fixes its own axis, and the pitch before it only
        // tilted the facing inside its own vertical plane, so the horizontal
        // heading survives both steps untouched.
        Vector3 upPitched = normalInPitchPlane.Normalized();
        Vector3 rollAxis = right.Cross(upPitched);
        if (rollAxis.LengthSquared() < 0.0001f)
            return; // degenerate — no well-defined forward axis to roll about
        rollAxis = rollAxis.Normalized();
        float rollAngle = upPitched.SignedAngleTo(normal, rollAxis);

        // `normal`, `right`, `rollAxis` and Vector3.Up are all WORLD space, so
        // both Basis(axis, angle) rotations above are WORLD rotations — but
        // armature.Transform is a LOCAL transform, and every level places the
        // Mario body with its own yaw (~180°, plus ~2° of roll in Level.tscn).
        // Writing a world rotation straight into the local transform composes
        // the body's yaw on top of it, which mirrors the horizontal part of the
        // resulting up axis: the tilt comes out leaning the opposite way and
        // drives his face into the slope instead of laying his belly along it.
        // Conjugating just the AXES into the body's space applies the identical
        // world rotations from inside the local transform
        // (P⁻¹·Rot(v,θ)·P == Rot(P⁻¹v, θ)), and is an exact no-op when the body
        // is unrotated.
        //
        // The yaw half deliberately stays in the existing local convention: the
        // mesh is authored backwards and the body's 180° is precisely what
        // cancels it, so converting that too would snap him 180° the instant
        // the tilt engages.
        Basis toBodySpace = GlobalBasis.Orthonormalized().Inverse();
        Vector3 pitchAxisLocal = (toBodySpace * right).Normalized();
        Vector3 rollAxisLocal = (toBodySpace * rollAxis).Normalized();

        // Roll is applied AFTER the pitch, so it left-multiplies it.
        Basis targetRotation =
            new Basis(rollAxisLocal, rollAngle)
            * new Basis(pitchAxisLocal, pitchAngle)
            * yawBasis;

        // Smooth toward the target — GetWallNormal()/GetFloorNormal() can
        // flicker frame to frame right at a floor/wall classification
        // boundary (the same contact-flicker class of bug fought all
        // session elsewhere). Snap instantly on the very first tilting
        // frame (no previous target to blend from) instead of blending
        // from a stale pose.
        if (!_wasPlatformTilting)
            _tiltCurrentRotation = targetRotation;
        else
        {
            float t = Mathf.Clamp((float)(WALL_SLIDE_FACE_SPEED * delta), 0f, 1f);
            _tiltCurrentRotation = _tiltCurrentRotation.Slerp(targetRotation, t).Orthonormalized();
        }
        _wasPlatformTilting = true;

        // Sink him toward the surface along his OWN down axis (the tilted rig's
        // local -Y, which the tilt above just aligned with -normal), NOT world
        // down — on a ramp those differ, and world-down would slide the belly
        // along the slope instead of straight into it. Rebuilt from
        // armatureBaseLocalPos every frame rather than accumulated, so it can't
        // creep. Origin lives in the Mario body's space, which is unit-scaled,
        // so these are plain metres.
        //
        // Two terms:
        //
        // 1. The GEOMETRIC gap, shared by every tilt state because it comes from
        //    the COLLIDER, not the pose. His collider is a cylinder with a FLAT
        //    bottom face, and that face never tilts. Resting it against an
        //    inclined plane, the support point in the -normal direction
        //    maximises (-t*cos + -r*(n·u)) over axis height t and radial
        //    direction u — which lands at t=0 for EVERY angle, i.e. always on
        //    the bottom rim, on the uphill side. That leaves the body origin
        //    (the centre of the bottom face) hanging capRadiusWorld*sin(angle)
        //    clear of the surface, measured perpendicular. We sink along the
        //    perpendicular, so sin is the right factor — and sin is exactly the
        //    horizontal magnitude of a unit normal, so no trig call, and it's
        //    the same "steepness" idiom TickSlip already uses. It degrades
        //    correctly at both ends: 0 on flat ground, and exactly
        //    capRadiusWorld against a vertical wall (pure side contact). Being
        //    bounded by 1.0, unlike tan, it also can't blow up when steep.
        //
        //    This is why slip floated: it's on 65-75° ground, where this term is
        //    ~0.34 — nearly all of the gap — and slip used to be excluded from
        //    it on the mistaken belief that a steep contact was a side contact
        //    rather than a bottom-rim one.
        //
        // 2. A flat artistic fudge for the pose's own height above the origin.
        //    This one DOES differ per state, since prone-on-his-belly and
        //    upright-on-his-feet sit against the surface differently.
        float steepness = new Vector2(normal.X, normal.Z).Length(); // == sin(slope angle)
        float sink = capRadiusWorld * steepness * TiltSlopeCompensation;
        sink +=
            stateOfMario == MarioState.slipping
                ? SlipTiltSurfaceOffset
                : BellyTiltSurfaceOffset;

        Vector3 tiltedOrigin = armatureBaseLocalPos - _tiltCurrentRotation.Y * sink;

        Vector3 origScale = armature.Transform.Basis.Scale;
        armature.Transform = new Transform3D(
            _tiltCurrentRotation.Scaled(origScale),
            tiltedOrigin
        );
    }

    // ===================== GROUND WALL PUSH / SHUFFLE =====================

    private void CheckGroundWallEntry(
        MarioState preMoveState,
        Vector3 inputDirWorld,
        float stickStrength
    )
    {
        if (!IsOnFloor() || !IsOnWall())
        {
            // Lost the wall — exit back to running/idle
            if (stateOfMario == MarioState.wallPush || stateOfMario == MarioState.wallShuffle)
            {
                ExitGroundWall(stickStrength, inputDirWorld);
            }
            return;
        }

        // Only enter from running/sprinting (or stay in if already wall-pushing/shuffling)
        bool isRunState =
            preMoveState == MarioState.running || preMoveState == MarioState.sprinting;
        bool isWallState =
            preMoveState == MarioState.wallPush || preMoveState == MarioState.wallShuffle;
        if (!isRunState && !isWallState)
            return;

        if (heldBody != null) // don't wall push while carrying
            return;

        if (stickStrength <= DEADZONE)
        {
            if (isWallState)
                ExitGroundWall(stickStrength, inputDirWorld);
            return;
        }

        Vector3 wallN = GetWallNormal();
        if (wallN == Vector3.Zero)
            return;

        Vector3 wallNXZ = new Vector3(wallN.X, 0, wallN.Z);
        if (wallNXZ.Length() < 0.001f)
            return;
        wallNXZ = wallNXZ.Normalized();

        // Movement direction (normalized, XZ only)
        Vector3 moveDir = new Vector3(inputDirWorld.X, 0, inputDirWorld.Z);
        if (moveDir.Length() < 0.001f)
        {
            if (isWallState)
                ExitGroundWall(stickStrength, inputDirWorld);
            return;
        }
        moveDir = moveDir.Normalized();

        // Angle between movement direction and the wall's inward direction (-normal)
        float dot = moveDir.Dot(-wallNXZ);
        float angleDeg = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)));

        groundWallNormal = wallNXZ;

        // Hysteresis: if already in a wall state, widen the threshold by a few degrees
        // to prevent rapid flipping at boundaries
        const float HYSTERESIS = 5f;
        float pushMax =
            WALL_PUSH_MAX_ANGLE + (stateOfMario == MarioState.wallPush ? HYSTERESIS : 0f);
        float shuffleMax =
            WALL_SHUFFLE_MAX_ANGLE + (stateOfMario == MarioState.wallShuffle ? HYSTERESIS : 0f);

        if (angleDeg <= pushMax)
        {
            if (stateOfMario != MarioState.wallPush)
            {
                stateOfMario = MarioState.wallPush;
                SetMarioState(stateOfMario);
            }
        }
        else if (angleDeg <= shuffleMax)
        {
            if (stateOfMario != MarioState.wallShuffle)
            {
                stateOfMario = MarioState.wallShuffle;
                SetMarioState(stateOfMario);
            }
            // Scale animation speed: closer to 60° = faster
            float t = Mathf.Clamp(
                (angleDeg - WALL_PUSH_MAX_ANGLE) / (WALL_SHUFFLE_MAX_ANGLE - WALL_PUSH_MAX_ANGLE),
                0f,
                1f
            );
            float animSpeed = Mathf.Lerp(
                WALL_SHUFFLE_MIN_ANIM_SPEED,
                WALL_SHUFFLE_MAX_ANIM_SPEED,
                t
            );
            animPlayer.SpeedScale = animSpeed;
        }
        else
        {
            // Beyond shuffle angle — exit to normal running
            if (isWallState)
                ExitGroundWall(stickStrength, inputDirWorld);
        }
    }

    private bool TickGroundWall(double delta, Vector3 inputDirWorld, float stickStrength)
    {
        // Exit if stick released
        if (stickStrength <= DEADZONE)
        {
            ExitGroundWall(stickStrength, inputDirWorld);
            return false; // let normal ground logic take over
        }

        // Exit if no longer on wall
        if (!IsOnWall())
        {
            ExitGroundWall(stickStrength, inputDirWorld);
            return false;
        }

        // Allow jump to exit
        if (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))
        {
            ExitGroundWall(stickStrength, inputDirWorld);
            return false; // fall through so jump logic can pick it up
        }

        Vector3 wallNXZ = groundWallNormal;
        if (wallNXZ == Vector3.Zero)
        {
            ExitGroundWall(stickStrength, inputDirWorld);
            return false;
        }

        if (stateOfMario == MarioState.wallPush)
        {
            // Kill velocity into wall, allow tiny lateral slide
            Vector3 moveDir = new Vector3(inputDirWorld.X, 0, inputDirWorld.Z);
            if (moveDir.Length() > 0.001f)
            {
                moveDir = moveDir.Normalized();
                // Project onto wall plane (remove the into-wall component)
                Vector3 slideDir = (moveDir - wallNXZ * moveDir.Dot(wallNXZ)).Normalized();
                velocity.X = slideDir.X * WALL_PUSH_SLIDE_SPEED;
                velocity.Z = slideDir.Z * WALL_PUSH_SLIDE_SPEED;
            }
            else
            {
                velocity.X = 0f;
                velocity.Z = 0f;
            }

            if (animPlayer.CurrentAnimation != "ma_push")
                SetMarioState(MarioState.wallPush);

            // Face roughly into wall but allow stick to steer a bit
            Vector3 pushDir = moveDir.Length() > 0.001f ? moveDir : -wallNXZ;
            float pushYaw = BaseYawFromDir(pushDir);
            armature.Rotation = new Vector3(0f, pushYaw, 0f);
        }
        else if (stateOfMario == MarioState.wallShuffle)
        {
            // Slide along wall surface
            Vector3 moveDir = new Vector3(inputDirWorld.X, 0, inputDirWorld.Z);
            Vector3 slideDir = Vector3.Zero;
            if (moveDir.Length() > 0.001f)
            {
                moveDir = moveDir.Normalized();
                slideDir = (moveDir - wallNXZ * moveDir.Dot(wallNXZ)).Normalized();
                float slideSpeed = RUN_SPEED * 0.3f;
                velocity.X = slideDir.X * slideSpeed;
                velocity.Z = slideDir.Z * slideSpeed;
            }

            // Pick left or right shuffle animation based on slide direction
            Vector3 wallRight = Vector3.Up.Cross(wallNXZ).Normalized();
            float sideDot = slideDir.Dot(wallRight);
            string shuffleAnim = (sideDot >= 0f) ? "ma_swlkr" : "ma_swlkl";

            if (animPlayer.CurrentAnimation != shuffleAnim)
                animPlayer.Play(shuffleAnim);

            // Face flush against wall
            armature.Rotation = new Vector3(0f, BaseYawFromDir(-wallNXZ), 0f);
        }

        // Small push into wall so MoveAndSlide maintains wall contact
        velocity += (-wallNXZ) * 0.5f;

        return true; // owns the frame
    }

    private void ExitGroundWall(float stickStrength, Vector3 inputDirWorld)
    {
        animPlayer.SpeedScale = 1f; // reset any shuffle speed scaling

        // Set facing to the movement direction when exiting, not the wall direction
        // This prevents false sideflip triggers
        Vector3 exitDir = Vector3.Zero;
        if (stickStrength > DEADZONE && inputDirWorld != Vector3.Zero)
        {
            exitDir = new Vector3(inputDirWorld.X, 0, inputDirWorld.Z).Normalized();
            lastFacingDirection = exitDir;
            armature.Rotation = new Vector3(0f, YawFromDir(exitDir), 0f);
        }

        // Clear facing history with the exit direction to prevent sideflip
        Vector3 clearDir = (exitDir != Vector3.Zero) ? exitDir : lastFacingDirection;
        for (int i = 0; i < 4; i++)
            Last4FacingDirections.Add(clearDir);

        if (stickStrength > DEADZONE)
        {
            stateOfMario = (stickStrength > 0.5f) ? MarioState.sprinting : MarioState.running;
        }
        else
        {
            stateOfMario = MarioState.idle;
            animPlayer.Play("ma_wait");
            SetMarioState(stateOfMario);
        }
    }

    private void EnterWallSlide(Vector3 incomingVel)
    {
        // Wall sliding takes over from every airborne move; spin visuals must not
        // keep emitting after the SpinJump state has ended.
        isSpining(false);

        stateOfMario = MarioState.wallSlide;
        wallSlideLostWallFrames = 0;

        if (animPlayer.CurrentAnimation != "ma_wsld")
            SetMarioState(MarioState.wallSlide);

        lastWallNormal = GetWallNormal();
        if (lastWallNormal == Vector3.Zero)
            lastWallNormal = -lastFacingDirection;

        // Face the wall
        // Store incoming horizontal direction for angled wall-kicks
        Vector3 vXZ = new Vector3(incomingVel.X, 0, incomingVel.Z);
        wallIncomingDirXZ = (vXZ.Length() > 0.001f) ? vXZ.Normalized() : Vector3.Zero;

        // Plan kick dir now (used for facing + jump)
        Vector3 nNow = GetWallNormal();
        if (nNow == Vector3.Zero)
            nNow = lastWallNormal;
        wallPlannedKickDir = ComputeWallKickDir(wallIncomingDirXZ, nNow);

        // Face the PLANNED jump direction (not just away-from-wall)
        if (wallPlannedKickDir.Length() > 0.001f)
        {
            lastFacingDirection = wallPlannedKickDir;
            armature.Rotation = new Vector3(0f, WallSlideYawFromDir(wallPlannedKickDir), 0f);
        }

        // SMS: once you're in wallSlide, kill sideways motion
        velocity.X = 0f;
        velocity.Z = 0f;

        if (velocity.Y > 0f)
            velocity.Y = 0f;
    }

    private void TickWallSlide(double delta)
    {
        // keep anim alive
        if (animPlayer.CurrentAnimation != "ma_wsld")
            SetMarioState(MarioState.wallSlide);

        // Prefer current wall normal, else fall back to last known.
        Vector3 n = IsOnWall() ? GetWallNormal() : lastWallNormal;
        if (n == Vector3.Zero)
            n = lastWallNormal;

        Vector3 nXZ = new Vector3(n.X, 0, n.Z);
        if (nXZ.Length() < 0.001f)
            nXZ = new Vector3(lastWallNormal.X, 0, lastWallNormal.Z);

        if (nXZ.Length() > 0.001f)
            nXZ = nXZ.Normalized();

        // Dust trail at the wall contact point, kicked up as he scrapes down.
        slideDustFx?.Emit(GlobalPosition - n.Normalized() * capRadiusWorld, Vector3.Up);

        // Keep planned kick dir updated in case normal changes slightly
        Vector3 nFull = (IsOnWall() ? GetWallNormal() : lastWallNormal);
        if (nFull == Vector3.Zero)
            nFull = lastWallNormal;

        wallPlannedKickDir = ComputeWallKickDir(wallIncomingDirXZ, nFull);

        // Smoothly face planned kick dir while sliding
        if (wallPlannedKickDir != Vector3.Zero)
        {
            float targetYaw = WallSlideYawFromDir(wallPlannedKickDir);

            float t = Mathf.Clamp((float)(WALL_SLIDE_FACE_SPEED * delta), 0f, 1f);
            float newYaw = Mathf.LerpAngle(armature.Rotation.Y, targetYaw, t);
            armature.Rotation = new Vector3(0f, newYaw, 0f);

            lastFacingDirection = wallPlannedKickDir;
        }

        // --- SMS STYLE: NO lateral control, NO tangential slide ---
        // Only keep a tiny "adhesion" push into the wall so contact persists.
        velocity.X = -nXZ.X * WALL_STICK_SPEED;
        velocity.Z = -nXZ.Z * WALL_STICK_SPEED;

        // --- Down only (never climb / never gain Y) ---
        if (velocity.Y > 0f)
            velocity.Y = 0f;

        // Apply scaled gravity (only here; your outer gravity code already skips wallSlide)
        velocity += GRAVITY * (float)delta * WALL_SLIDE_GRAVITY_SCALE;

        // Clamp fall speed
        if (velocity.Y < WALL_SLIDE_MAX_FALL)
            velocity.Y = WALL_SLIDE_MAX_FALL;

        // Only allowed action: wall kick
        if (Input.IsActionJustPressed("button_a"))
            DoWallJump();
    }

    private void DoWallJump()
    {
        // Hard rule: no wall jump from dives (rollout is ok)
        if (
            stateOfMario == MarioState.diving
            || stateOfMario == MarioState.singleJumpDive
            || stateOfMario == MarioState.doubleJumpDive
            || stateOfMario == MarioState.tripleJumpDive
            || stateOfMario == MarioState.bellySlidingFromDive
        )
            return;

        Vector3 n = GetWallNormal();
        if (n == Vector3.Zero)
            n = lastWallNormal;
        if (n == Vector3.Zero)
            n = Vector3.Back;

        // Use the precomputed planned dir if valid; otherwise compute now.
        Vector3 outDir =
            (wallPlannedKickDir != Vector3.Zero)
                ? wallPlannedKickDir
                : ComputeWallKickDir(wallIncomingDirXZ, n);
        LockAirTakeoffDir(outDir);

        // Horizontal kick
        velocity.X = outDir.X * WALL_KICK_OUT_SPEED;
        velocity.Z = outDir.Z * WALL_KICK_OUT_SPEED;

        // Variable jump start (tap = base, hold = higher)
        velocity.Y = WALL_JUMP_BASE_UP_VEL;

        stateOfMario = MarioState.wallJump;

        // play once
        wallJumpAnimPlayed = true;
        wallJumpAnimFrozen = false;
        SetMarioState(MarioState.wallJump);

        // Face kick dir immediately
        lastFacingDirection = outDir;
        armature.Rotation = new Vector3(0f, WallJumpYawFromDir(outDir), 0f);

        // Commit / lock stuff
        wallKickDir = outDir;
        wallKickLock = WALL_KICK_LOCK_FRAMES;
        wallRegrabCooldown = WALL_REGRAB_COOLDOWN_FRAMES;

        // Enable variable-hold window
        wallJumpHoldActive = true;
        wallJumpHoldTime = 0f;

        // Keep your existing jump flags consistent with normal jumps
        isJumping = true;
        initalJumpHold = true;
        jumpHoldTime = 0f;
    }

    private void PostMoveWallCheck(Vector3 preSlideVelocity, MarioState airStateAtContact)
    {
        // Never auto-enter wall slide from dive states (rollouts are ok)
        if (
            airStateAtContact == MarioState.diving
            || airStateAtContact == MarioState.singleJumpDive
            || airStateAtContact == MarioState.doubleJumpDive
            || airStateAtContact == MarioState.tripleJumpDive
            || airStateAtContact == MarioState.bellySlidingFromDive
        )
            return;

        if (IsOnFloor())
            return;
        if (stateOfMario == MarioState.wallJump && wallKickLock > 0)
            return;
        if (stateOfMario == MarioState.wallSlide || stateOfMario == MarioState.slipping)
            return;
        if (!IsOnWall())
            return;

        Vector3 n = GetWallNormal();
        if (n == Vector3.Zero)
            return;

        // This is the FIRST-CONTACT trigger — fires the same frame
        // MoveAndSlide resolves the hit, before CanStartWallSlide's
        // sustaining check even runs — so the real-wall-vs-steep-slope angle
        // gate has to live here, not just there. Every slope contact was
        // routing straight into wallSlide regardless of angle because this
        // path never checked it.
        if (Mathf.RadToDeg(n.AngleTo(Vector3.Up)) < WALL_TRUE_VERTICAL_MIN_ANGLE_DEG)
        {
            EnterSlip(preSlideVelocity);
            return;
        }

        if (wallRegrabCooldown > 0)
            return;

        // Per-state entry requirements based on what you were doing when you struck the wall
        GetWallSlideEntryParams(airStateAtContact, out float minSpeed, out float minApproachDot);

        Vector3 nXZ = new Vector3(n.X, 0, n.Z);
        if (nXZ.Length() < 0.001f)
            return;
        nXZ = nXZ.Normalized();

        Vector3 vXZ = new Vector3(preSlideVelocity.X, 0, preSlideVelocity.Z);
        float speed = vXZ.Length();
        if (speed < minSpeed)
            return;

        float fall = Mathf.Abs(preSlideVelocity.Y);
        if (fall > 1.0f && (speed / fall) < WALL_MIN_HORIZONTAL_RATIO)
            return;

        Vector3 vDir = vXZ / speed;
        // --- Takeoff cone restriction (use airStateAtContact for spin tightening) ---
        // Skip cone for wallJump — player is intentionally chaining wall jumps
        if (airTakeoffDirValid && airStateAtContact != MarioState.wallJump)
        {
            float coneDeg = WALL_TAKEOFF_CONE_DEG_DEFAULT;

            if (!WithinConeXZ(vDir, airTakeoffDirXZ, coneDeg))
                return;
        }

        float approach = vDir.Dot(-nXZ);

        if (approach < minApproachDot)
            return;

        EnterWallSlide(preSlideVelocity);
    }

    private Vector3 ComputeWallKickDir(Vector3 inDir, Vector3 wallNormal)
    {
        Vector3 nXZ = new Vector3(wallNormal.X, 0, wallNormal.Z);
        if (nXZ.Length() < 0.001f)
            nXZ = new Vector3(lastWallNormal.X, 0, lastWallNormal.Z);

        if (nXZ.Length() < 0.001f)
            nXZ = Vector3.Back;

        nXZ = nXZ.Normalized();

        Vector3 dir = inDir;
        if (dir == Vector3.Zero)
            dir = -nXZ;

        // mirror reflection (same angle in/out)
        Vector3 outDir = dir.Bounce(nXZ).Normalized();

        // ensure it actually goes away from the wall
        if (outDir.Dot(nXZ) < 0.05f)
            outDir = (outDir - nXZ * outDir.Dot(nXZ) + nXZ * 0.25f).Normalized();

        return outDir;
    }

    // If Mario faces BACKWARDS relative to movement, set this to Mathf.Pi.
    // If he faces correctly, set it to 0.
    // (This is the main "flip 180°" knob.)
    private const float MODEL_YAW_OFFSET = 0f;

    private float BaseYawFromDir(Vector3 dir)
    {
        if (dir == Vector3.Zero)
            return armature.Rotation.Y;

        // Your current convention (forward = -Z)
        float yaw = Mathf.Atan2(-dir.X, -dir.Z);

        // Apply model correction
        yaw += MODEL_YAW_OFFSET;

        return WrapAnglePi(yaw);
    }

    private float BaseYawFromVelXZ()
    {
        Vector3 v = new Vector3(velocity.X, 0f, velocity.Z);
        if (v.LengthSquared() < 0.0001f)
            return armature.Rotation.Y;
        return BaseYawFromDir(v.Normalized());
    }

    private bool TryGetAirProfile(MarioState s, out AirControlProfile p)
    {
        switch (s)
        {
            case MarioState.singleJump:
            case MarioState.singleJumpLanding: // (if you ever treat as airborne; probably not)
                // Ground-pound jump reuses this state rather than having its own
                // (same pattern as tree jump-off reusing wallJump below).
                p = _isGroundPoundJump ? AIR_GROUND_POUND_JUMP : AIR_SINGLE;
                return true;

            case MarioState.doubleJump:
                p = AIR_DOUBLE;
                return true;

            case MarioState.tripleJump:
                p = AIR_TRIPLE;
                return true;

            case MarioState.sideFlip:
                p = AIR_SIDEFLIP;
                return true;

            case MarioState.SpinJump:
                p = AIR_SPIN;
                return true;

            case MarioState.backFlip:
                p = AIR_BACKFLIP;
                return true;

            case MarioState.wallJump:
                // Tree jump-off reuses this state for its physics/animation/locks
                // (all correct as-is for it), but should feel like a normal jump in
                // the air, not a wall-kick.
                p = _isTreeJumpAirborne ? AIR_TREE : AIR_WALLJUMP;
                return true;

            case MarioState.bellyRollout:
            case MarioState.singleRollout:
                p = AIR_ROLLOUT;
                return true;

            case MarioState.diving:
            case MarioState.singleJumpDive:
            case MarioState.doubleJumpDive:
            case MarioState.tripleJumpDive:
                p = AIR_DIVE;
                return true;

            case MarioState.stomping:
                p = AIR_SINGLE;
                return true;
        }

        p = default;
        return false;
    }

    private void ApplyAirControl(
        double delta,
        AirControlProfile prof,
        bool preserveTakeoffSpeed = false
    )
    {
        float dt = (float)delta;

        Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
        float speed = velXZ.Length();

        bool hasInput = walkingStrength > DEADZONE && direction != Vector3.Zero;

        if (!hasInput)
        {
            // Neutral stick: apply drag
            velXZ = velXZ.MoveToward(Vector2.Zero, prof.drag * dt);

            if (velXZ.Length() < prof.stopSnap)
                velXZ = Vector2.Zero;

            velocity.X = velXZ.X;
            velocity.Z = velXZ.Y;
            return;
        }

        // Desired direction (camera-relative) already in `direction`
        Vector3 desired3 = direction.Normalized();
        Vector2 desiredDir = new Vector2(desired3.X, desired3.Z);
        if (desiredDir.Length() < 0.001f)
            return;
        desiredDir = desiredDir.Normalized();

        Vector2 velDir = (speed > 0.001f) ? (velXZ / speed) : desiredDir;
        float dot = velDir.Dot(desiredDir);
        bool braking = dot < 0.0f;

        // Is the stick opposing the direction he LAUNCHED in? Measured against
        // the fixed takeoff vector, not current velocity — same reasoning as
        // ApplyBackflipAirControl: current velocity gets steered, so classifying
        // against it makes the meaning of "opposite" drift mid-flight.
        bool opposingTakeoff = false;
        if (preserveTakeoffSpeed && airTakeoffDirValid)
        {
            Vector2 takeoffXZ = new Vector2(airTakeoffDirXZ.X, airTakeoffDirXZ.Z);
            if (takeoffXZ.LengthSquared() > 0.0001f)
                opposingTakeoff = desiredDir.Dot(takeoffXZ.Normalized()) < -0.35f;
        }

        float targetSpeed;
        if (!braking)
        {
            targetSpeed = prof.maxSpeed * walkingStrength;

            // Don't let steering SCRUB speed he took off with. MoveToward pulls
            // both ways, so with AIR_SINGLE.maxSpeed = 8.885 — already below
            // RUN_SPEED = 11.55 — every jump from a sprint was being hauled back
            // down mid-flight (a boosted 14.55 bled off in ~0.64s, about one
            // jump's length). Raising the target to whatever he's actually doing
            // keeps full steering authority while making the cap a floor he can
            // exceed, so a faster run genuinely jumps farther. Holding AGAINST
            // the takeoff direction opts out and brakes normally.
            if (preserveTakeoffSpeed && !opposingTakeoff)
                targetSpeed = Mathf.Max(targetSpeed, speed);
        }
        else
        {
            // Mostly stop. Optionally allow reversing back toward desiredDir.
            // NOTE: targetSpeed must be POSITIVE here — desiredVel = desiredDir *
            // targetSpeed below, so a negative value would point desiredVel back
            // toward the ORIGINAL direction (the one being braked away from)
            // instead of toward where the stick is actually being held.
            bool allowReverse =
                (prof.reverseMax > 0f) && (walkingStrength > 0.65f) && (dot < -0.35f);
            targetSpeed = allowReverse ? (prof.reverseMax * walkingStrength) : 0f;
        }

        Vector2 desiredVel = desiredDir * targetSpeed;

        float rate = braking ? prof.brake : prof.accel;
        velXZ = velXZ.MoveToward(desiredVel, rate * dt);

        // Snap when braking and basically stopped
        if (braking && velXZ.Length() < prof.stopSnap)
            velXZ = Vector2.Zero;

        velocity.X = velXZ.X;
        velocity.Z = velXZ.Y;
    }

    // Same shape as ApplyAirControl, but classifies "forward vs backward" against
    // a FIXED reference (_backflipLaunchDir, captured once in StartBackflip) rather
    // than his current, ever-changing velocity direction. Backflip needs "backward
    // control good, forward control limited" to stay consistent for the WHOLE
    // flight — using current velocity for that (like the generic function does)
    // means the classification silently flips as soon as his velocity gets steered
    // even a little, which is what made reverseMax feel like it wasn't doing
    // anything: forward pushes were landing in the full-power branch instead of
    // the capped one as soon as velocity direction drifted off the launch axis.
    private void ApplyBackflipAirControl(double delta, AirControlProfile prof)
    {
        float dt = (float)delta;

        Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
        bool hasInput = walkingStrength > DEADZONE && direction != Vector3.Zero;

        if (!hasInput)
        {
            velXZ = velXZ.MoveToward(Vector2.Zero, prof.drag * dt);
            if (velXZ.Length() < prof.stopSnap)
                velXZ = Vector2.Zero;
            velocity.X = velXZ.X;
            velocity.Z = velXZ.Y;
            return;
        }

        Vector3 desired3 = direction.Normalized();
        Vector2 desiredDir = new Vector2(desired3.X, desired3.Z);
        if (desiredDir.Length() < 0.001f)
            return;
        desiredDir = desiredDir.Normalized();

        Vector2 launchDir = new Vector2(_backflipLaunchDir.X, _backflipLaunchDir.Z).Normalized();
        float dot = desiredDir.Dot(launchDir);
        // Pushing AGAINST the launch direction = toward where he jumped FROM.
        bool pushingForward = dot < 0.0f;

        float targetSpeed;
        if (!pushingForward)
        {
            targetSpeed = prof.maxSpeed * walkingStrength;
        }
        else
        {
            bool allowReverse =
                (prof.reverseMax > 0f) && (walkingStrength > 0.65f) && (dot < -0.35f);
            targetSpeed = allowReverse ? (prof.reverseMax * walkingStrength) : 0f;
        }

        Vector2 desiredVel = desiredDir * targetSpeed;

        float rate = pushingForward ? prof.brake : prof.accel;
        velXZ = velXZ.MoveToward(desiredVel, rate * dt);

        if (pushingForward && velXZ.Length() < prof.stopSnap)
            velXZ = Vector2.Zero;

        velocity.X = velXZ.X;
        velocity.Z = velXZ.Y;
    }

    // Same shape as ApplyAirControl, but steers the forward (along wallKickDir) and
    // lateral (perpendicular to it) components of velocity at independent rates.
    // Regular ApplyAirControl only has one accel knob shared by "extend the kick"
    // and "redirect left/right" — raising it enough to feel responsive pushing
    // toward his facing direction also made side-to-side redirection too strong,
    // since both go through the same rate. Decomposing onto the kick axis lets
    // forward stay snappy (prof.accel/prof.brake, same braking/reverse behavior as
    // the generic version) while lateral gets its own, weaker rate.
    private void ApplyWallJumpAirControl(double delta, AirControlProfile prof, float lateralAccel)
    {
        float dt = (float)delta;
        Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
        bool hasInput = walkingStrength > DEADZONE && direction != Vector3.Zero;

        if (!hasInput)
        {
            velXZ = velXZ.MoveToward(Vector2.Zero, prof.drag * dt);
            if (velXZ.Length() < prof.stopSnap)
                velXZ = Vector2.Zero;
            velocity.X = velXZ.X;
            velocity.Z = velXZ.Y;
            return;
        }

        Vector2 kickDirXZ = new Vector2(wallKickDir.X, wallKickDir.Z);
        if (kickDirXZ.Length() < 0.001f)
            kickDirXZ = (velXZ.Length() > 0.001f) ? velXZ.Normalized() : Vector2.Down;
        kickDirXZ = kickDirXZ.Normalized();
        Vector2 lateralAxis = new Vector2(-kickDirXZ.Y, kickDirXZ.X);

        Vector3 desired3 = direction.Normalized();
        Vector2 desiredDir = new Vector2(desired3.X, desired3.Z);
        if (desiredDir.Length() < 0.001f)
            return;
        desiredDir = desiredDir.Normalized();

        // Same target velocity the generic function would use (desiredDir * maxSpeed),
        // just decomposed onto the kick-relative axes instead of steered as one vector.
        float desiredForward = desiredDir.Dot(kickDirXZ);
        float desiredLateral = desiredDir.Dot(lateralAxis);

        float curForward = velXZ.Dot(kickDirXZ);
        float curLateral = velXZ.Dot(lateralAxis);

        bool brakingForward = desiredForward < 0f;
        float targetForward;
        if (!brakingForward)
        {
            targetForward = prof.maxSpeed * walkingStrength * desiredForward;
        }
        else
        {
            bool allowReverse =
                (prof.reverseMax > 0f) && (walkingStrength > 0.65f) && (desiredForward < -0.35f);
            targetForward = allowReverse ? (prof.reverseMax * walkingStrength * desiredForward) : 0f;
        }
        float forwardRate = brakingForward ? prof.brake : prof.accel;
        curForward = Mathf.MoveToward(curForward, targetForward, forwardRate * dt);
        if (brakingForward && Mathf.Abs(curForward) < prof.stopSnap)
            curForward = 0f;

        float targetLateral = prof.maxSpeed * walkingStrength * desiredLateral;
        curLateral = Mathf.MoveToward(curLateral, targetLateral, lateralAccel * dt);
        // Only snap to exact zero when actually settling toward zero (near-zero
        // target too) — snapping whenever the CURRENT value is merely small also
        // caught the very first ramp-up step away from zero every frame, since a
        // single frame's step (lateralAccel * dt) is smaller than stopSnap. That
        // reset it right back to 0 every tick, permanently, regardless of accel.
        if (Mathf.Abs(targetLateral) < prof.stopSnap && Mathf.Abs(curLateral) < prof.stopSnap)
            curLateral = 0f;

        velXZ = kickDirXZ * curForward + lateralAxis * curLateral;
        velocity.X = velXZ.X;
        velocity.Z = velXZ.Y;
    }

    private void StartGroundJumpPreserveXZ()
    {
        // consume buffer
        jumpBuffer = 0;

        // basic jump bookkeeping
        isJumping = true;
        initalJumpHold = true;
        jumpHoldTime = 0f;

        landingEnteredThisContact = false;

        // SPIN wins if available
        if (spinBuffer > 0)
        {
            spinBuffer = 0;
            StartSpinJumpPreserveXZ(); // <-- new function below
            return;
        }

        // Otherwise: normal single jump — this starts a fresh chain.
        _jumpChainStartDir = GetMoveDirOrFallback();
        stateOfMario = MarioState.singleJump;
        velocity.Y = BASE_JUMP_VELOCITY;
        SetMarioState(stateOfMario);
    }

    private void StartSpinJumpPreserveXZ()
    {
        StopWaistIKHard();

        stateOfMario = MarioState.SpinJump;
        velocity.Y = TRIPLE_JUMP_VELOCITY; // your current spin height
        SetMarioState(stateOfMario);

        // lock facing based on current movement/stick
        spinJumpTakeoffDir = GetSpinLockDir();
        Vector2 velXZ = new Vector2(velocity.X, velocity.Z);
        float speed = velXZ.Length();

        Vector3 fwd = spinJumpTakeoffDir; // locked dir
        velocity.X += fwd.X * SPIN_FORWARD_BONUS;
        velocity.Z += fwd.Z * SPIN_FORWARD_BONUS;

        spinJumpDirLocked = true;
        lastFacingDirection = spinJumpTakeoffDir;

        spinJumpLockedYaw = BaseYawFromDir(spinJumpTakeoffDir);
        armature.Rotation = new Vector3(0f, spinJumpLockedYaw, 0f);

        // IMPORTANT: do NOT change velocity.X/Z here.
        // That’s the whole “carry speed from anything” rule.

        isSpining(true);
    }

    private void GetWallSlideEntryParams(MarioState s, out float minSpeed, out float minApproachDot)
    {
        // SpinJump: same as everything else
        // (remove special-case)

        // default for everything else
        minSpeed = WALL_MIN_APPROACH_SPEED_DEFAULT;
        minApproachDot = WALL_APPROACH_DOT_DEFAULT;
    }

    private static Vector3 NormalizeXZ(Vector3 v)
    {
        v.Y = 0f;
        float len = v.Length();
        return (len > 0.0001f) ? (v / len) : Vector3.Zero;
    }

    private static bool WithinConeXZ(Vector3 dirXZ, Vector3 axisXZ, float coneDeg)
    {
        if (dirXZ == Vector3.Zero || axisXZ == Vector3.Zero)
            return false;
        float dot = NormalizeXZ(dirXZ).Dot(NormalizeXZ(axisXZ));
        float minDot = Mathf.Cos(Mathf.DegToRad(coneDeg));
        return dot >= minDot;
    }

    private void LockAirTakeoffDir(Vector3 dirWorld)
    {
        airTakeoffDirXZ = NormalizeXZ(dirWorld);
        airTakeoffDirValid = (airTakeoffDirXZ != Vector3.Zero);
    }

    private void ClearAirTakeoffDir()
    {
        airTakeoffDirValid = false;
        airTakeoffDirXZ = Vector3.Zero;
    }

    private bool TryStartLedgeGrab()
    {
        UpdateLedgeSensorFacing();
        if (ledgeRegrabCooldown > 0)
            return false;
        if (IsOnFloor())
            return false;

        // don't grab during these
        if (stateOfMario == MarioState.wallSlide || stateOfMario == MarioState.wallJump)
            return false;
        if (
            stateOfMario == MarioState.diving
            || stateOfMario == MarioState.singleJumpDive
            || stateOfMario == MarioState.doubleJumpDive
            || stateOfMario == MarioState.tripleJumpDive
            || stateOfMario == MarioState.bellySlidingFromDive
        )
            return false;

        if (velocity.Y > LEDGE_MIN_FALL_SPEED)
            return false; // i.e. if Y > -2, don't grab

        if (chestRay == null || headRay == null || topDownRay == null)
            return false;

        chestRay.ForceRaycastUpdate();
        headRay.ForceRaycastUpdate();
        topDownRay.ForceRaycastUpdate();

        bool fNow = Input.IsKeyPressed(Key.F);
        if (fNow && !fPrev)
        {
            GD.Print(
                $"[LedgeCheck] velY={velocity.Y:F2} "
                    + $"chest={chestRay.IsColliding()} head={headRay.IsColliding()} top={topDownRay.IsColliding()} "
                    + $"chLen={chestRay.TargetPosition.Length():F2} hLen={headRay.TargetPosition.Length():F2} tLen={topDownRay.TargetPosition.Length():F2} "
                    + $"maskC={chestRay.CollisionMask} maskH={headRay.CollisionMask} maskT={topDownRay.CollisionMask}"
            );
            DebugRayHit(chestRay, "Chest");
            DebugRayHit(headRay, "Head");
            DebugRayHit(topDownRay, "TopDown");

            if (chestRay.IsColliding())
                GD.Print(
                    $"  chest hit={chestRay.GetCollider()} n={chestRay.GetCollisionNormal()} p={chestRay.GetCollisionPoint()}"
                );

            if (topDownRay.IsColliding())
                GD.Print(
                    $"  top hit={topDownRay.GetCollider()} n={topDownRay.GetCollisionNormal()} p={topDownRay.GetCollisionPoint()}"
                );
        }
        fPrev = fNow;

        // core rule:
        // 1) chest hits a wall
        // 2) head does NOT hit (space above)
        // 3) top-down hits a walkable top surface
        if (!chestRay.IsColliding())
            return false;
        // Only ledge grab static environment, not enemies or characters
        // (AnimatableBody3D inherits StaticBody3D, so moving platforms pass this check)
        if (chestRay.GetCollider() is not StaticBody3D)
            return false;
        if (headRay.IsColliding())
            return false;

        // do NOT require topDownRay.IsColliding() anymore
        // top surface will be validated by the robust IntersectRay probe below

        Vector3 wallN = chestRay.GetCollisionNormal();
        Vector3 chestP = chestRay.GetCollisionPoint();

        if (wallN == Vector3.Zero)
            return false;

        // horizontal wall normal
        Vector3 wallNXZ = new Vector3(wallN.X, 0, wallN.Z);
        if (wallNXZ.Length() < 0.001f)
            return false;
        wallNXZ = wallNXZ.Normalized();

        // ---- TOP PROBE (robust): cast down from above + slightly over the ledge ----
        float probeUp = capHalfHeightWorld + capRadiusWorld * 0.25f; // start above Mario
        float probeFwd = capRadiusWorld * 0.70f; // move toward the platform
        float probeDownLen = capHalfHeightWorld * 3.0f; // enough to find top

        Vector3 probeFrom = chestP + Vector3.Up * probeUp + (-wallNXZ) * probeFwd;
        Vector3 probeTo = probeFrom + Vector3.Down * probeDownLen;

        var space = GetWorld3D().DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(probeFrom, probeTo);
        q.CollisionMask = CollisionMask;
        q.CollideWithBodies = true;
        q.CollideWithAreas = false;
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = space.IntersectRay(q);
        if (hit.Count == 0)
            return false;

        Vector3 topP = (Vector3)hit["position"];
        Vector3 topN = (Vector3)hit["normal"];

        // Reject bogus top hits that are far away (prevents "zoom across map")
        Vector3 d = topP - chestP;
        d.Y = 0f;
        float maxXZ = capRadiusWorld * 3.5f; // tune 3–5x radius
        if (d.Length() > maxXZ)
            return false;

        // must be a reasonably flat top
        if (topN.Dot(Vector3.Up) < 0.7f)
            return false;

        // must be above the wall hit a bit (prevents “ground under you”)
        if (topP.Y < chestP.Y + capRadiusWorld * 0.25f)
            return false;

        ledgeTopPoint = topP;
        ledgeWallNormal = wallNXZ;
        // --- COMPUTE STAND POSITION (where Mario ends up after climb) ---
        // Move onto the platform (away from wall)


        // Put capsule bottom on the ledge top surface.
        // bottomOffsetFromBody is negative, so: bodyY = topY - bottomOffsetFromBody
        float standBodyY = BodyYForCapsuleBottomY(ledgeTopPoint.Y) + LEDGE_STAND_UP;
        // Use chest contact as the XZ anchor, then push onto the ledge
        Vector3 ontoLedge = -ledgeWallNormal; // onto platform
        Vector3 standXZ = chestP + ontoLedge * LEDGE_STAND_FORWARD;
        ledgeStandPos = new Vector3(standXZ.X, standBodyY, standXZ.Z);

        // --------- COMPUTE HANG POSITION FROM GEOMETRY ---------

        // ENTER hang state + play hang anim
        stateOfMario = MarioState.ledgeHang;
        SetMarioState(stateOfMario);
        LockArmatureVisualRoot();

        // XZ: anchor at chest hit point, push away from wall just enough to avoid clipping
        float hangBackDist = capRadiusWorld * 0.1f;
        float hangX = chestP.X + ledgeWallNormal.X * hangBackDist;
        float hangZ = chestP.Z + ledgeWallNormal.Z * hangBackDist;

        // Y: no offset — hang animation handles the visual positioning
        float hangBodyY = ledgeTopPoint.Y;

        ledgeHangPos = new Vector3(hangX, hangBodyY, hangZ);

        // Snap there
        GlobalPosition = ledgeHangPos;

        // Freeze
        velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
        ledgePinned = true;

        // Track platform so Mario moves with it (moving/spinning platforms)
        var collider = chestRay.GetCollider();
        if (collider is AnimatableBody3D abody)
        {
            _ledgePlatformBody = abody;
            Transform3D inv = abody.GlobalTransform.AffineInverse();
            _ledgeLocalHangPos = inv * ledgeHangPos;
            _ledgeLocalStandPos = inv * ledgeStandPos;
            _ledgeLocalWallNormal = inv.Basis * ledgeWallNormal;
        }
        else
        {
            _ledgePlatformBody = null;
        }

        // Face wall while hanging
        armature.Rotation = new Vector3(0f, BaseYawFromDir(-ledgeWallNormal), 0f);

        return true;
    }

    private void TickLedgeHang(double delta, Vector2 LstickVec)
    {
        // Follow moving/spinning platform
        if (!UpdateLedgePlatformPositions())
        {
            ExitLedgeHangToFall();
            return;
        }

        // pin Mario exactly
        if (ledgePinned)
        {
            velocity = Vector3.Zero;
            GlobalPosition = ledgeHangPos;
            armature.Rotation = new Vector3(0f, BaseYawFromDir(-ledgeWallNormal), 0f);
        }

        if (animPlayer.CurrentAnimation != "ma_hang")
            SetMarioState(MarioState.ledgeHang);

        LockArmatureVisualRoot();
        // Drop (optional): press B to let go
        if (Input.IsActionJustPressed("button_b"))
        {
            ExitLedgeHangToFall();
            return;
        }

        // UP on stick = negative Y in GetVector (because it's down-up)
        bool stickUp = (LstickVec.Length() > DEADZONE) && (LstickVec.Y < -0.55f);

        if (stickUp)
        {
            // start climb
            stateOfMario = MarioState.ledgeClimb;
            SetMarioState(stateOfMario);
            ledgeClimbT = 0f;
            return;
        }

        // A = quick get up / hop
        if (Input.IsActionJustPressed("button_a"))
        {
            // play hop anim and give impulse
            stateOfMario = MarioState.ledgeHopUp;
            SetMarioState(stateOfMario);

            // hop upward + forward onto ledge (uses current rotated wall normal)
            Vector3 fwdOntoLedge = -ledgeWallNormal; // onto platform
            velocity = Vector3.Zero;
            velocity.Y = LEDGE_HOP_UP_VEL;
            velocity.X = fwdOntoLedge.X * LEDGE_HOP_FWD_SPEED;
            velocity.Z = fwdOntoLedge.Z * LEDGE_HOP_FWD_SPEED;

            ledgePinned = false;
            _ledgePlatformBody = null;
            ledgeRegrabCooldown = LEDGE_REGRAB_COOLDOWN_FRAMES;
            return;
        }
    }

    private bool TickLedgeClimb(double delta)
    {
        // Follow moving/spinning platform
        if (!UpdateLedgePlatformPositions())
        {
            ExitLedgeHangToFall();
            return false;
        }

        velocity = Vector3.Zero;

        if (animPlayer.CurrentAnimation != "ma_hgup")
            SetMarioState(MarioState.ledgeClimb);

        LockArmatureVisualRoot();
        ledgeClimbT += (float)delta;

        float t = (ledgeClimbDur > 0.01f) ? (ledgeClimbT / ledgeClimbDur) : (ledgeClimbT / 0.55f);
        t = Mathf.Clamp(t, 0f, 1f);

        float s = t * t * (3f - 2f * t);

        // Only lerp XZ toward stand position; Y stays pinned — the climb animation handles vertical
        Vector3 lerpedPos = ledgeHangPos.Lerp(ledgeStandPos, s);
        GlobalPosition = new Vector3(lerpedPos.X, ledgeHangPos.Y, lerpedPos.Z);

        armature.Rotation = new Vector3(0f, BaseYawFromDir(-ledgeWallNormal), 0f);

        if (t < 1f)
            return false;

        // FINISH
        ledgePinned = false;
        _ledgePlatformBody = null;
        ledgeRegrabCooldown = LEDGE_REGRAB_COOLDOWN_FRAMES;

        stateOfMario = MarioState.idle;
        animPlayer.Play("ma_wait");
        SetMarioState(stateOfMario);
        LockArmatureVisualRoot();

        //GlobalPosition = ledgeStandPos;

        // IMPORTANT: do NOT give a downward velocity here
        velocity = Vector3.Zero;
        Velocity = Vector3.Zero;

        // Optional: also mark landing latch so even if something calls EnterLanding, it won't restart
        landingEnteredThisContact = true;

        suppressLandingFrames = 2; // 1–3 frames is enough
        landingEnteredThisContact = true; // belt-and-suspenders against EnterLanding

        return true;
    }

    /// <summary>
    /// If Mario is hanging on a moving/spinning platform, update ledge positions
    /// from the platform's current transform. Returns false if the platform was
    /// destroyed (caller should drop Mario).
    /// </summary>
    private bool UpdateLedgePlatformPositions()
    {
        if (_ledgePlatformBody == null)
            return true; // static geometry, nothing to update

        if (!IsInstanceValid(_ledgePlatformBody))
        {
            _ledgePlatformBody = null;
            return false; // platform was freed
        }

        // Ledge trump: if the platform starts a waypoint rotation, kick Mario off
        if (_ledgePlatformBody is MovingPlatform mp && mp.IsWaypointRotating)
            return false;

        Transform3D xform = _ledgePlatformBody.GlobalTransform;
        ledgeHangPos = xform * _ledgeLocalHangPos;
        ledgeStandPos = xform * _ledgeLocalStandPos;

        // Recompute wall normal from platform's current rotation
        Vector3 rotatedNormal = xform.Basis * _ledgeLocalWallNormal;
        // Flatten to XZ (keep it horizontal)
        rotatedNormal.Y = 0f;
        if (rotatedNormal.LengthSquared() > 0.0001f)
            ledgeWallNormal = rotatedNormal.Normalized();

        return true;
    }

    private void ExitLedgeHangToFall()
    {
        ledgePinned = false;
        _ledgePlatformBody = null;
        ledgeRegrabCooldown = LEDGE_REGRAB_COOLDOWN_FRAMES;
        stateOfMario = MarioState.singleJump; // any "air" state you prefer
        velocity.Y = -5f; // start falling
    }

    // ============================================================
    //   TREE CLIMB
    // ============================================================

    /// <summary>
    /// Called externally (e.g. by a PalmTree's GrabZone Area3D) when Mario enters
    /// the trunk grab volume. Returns true if Mario grabbed.
    /// </summary>
    public bool TryGrabTree(
        Node3D treeRoot,
        Vector3 trunkCenterXZ,
        float trunkBottomY,
        float trunkTopY,
        float leafLandY,
        float trunkRadiusBottom,
        float trunkRadiusTop
    )
    {
        if (_treeRegrabCooldown > 0)
            return false;
        if (heldBody != null)
            return false;
        switch (stateOfMario)
        {
            case MarioState.treeGrab:
            case MarioState.treeWait:
            case MarioState.treeClimb:
            case MarioState.treeMoveL:
            case MarioState.treeMoveR:
            case MarioState.treeTopReach:
            case MarioState.ledgeHang:
            case MarioState.ledgeClimb:
            case MarioState.ledgeHopUp:
            case MarioState.groundPoundStartup:
            case MarioState.groundPoundFalling:
            case MarioState.groundPoundLanding:
            case MarioState.hurt:
            case MarioState.dead:
                return false;
        }

        // If Mario was wall-sliding (likely on the trunk capsule), cancel the slide cleanly.
        if (stateOfMario == MarioState.wallSlide || stateOfMario == MarioState.wallJump)
        {
            wallJumpAnimFrozen = false;
            wallKickLock = 0;
        }

        // Tree grab is not a spin state — make sure the spin particles are off.
        isSpining(false);

        _treeNode = treeRoot;
        _treeCenter = new Vector3(trunkCenterXZ.X, 0f, trunkCenterXZ.Z);
        _treeRadiusBottom = trunkRadiusBottom + TREE_GRAB_RADIUS_PAD;
        _treeRadiusTop = trunkRadiusTop + TREE_GRAB_RADIUS_PAD;
        _treeBottomY = trunkBottomY;
        _treeTopY = trunkTopY;
        _treeLeafLandY = leafLandY;

        // Initial angle = direction from trunk to Mario (radial-out)
        Vector3 outDir = GlobalPosition - _treeCenter;
        outDir.Y = 0f;
        if (outDir.LengthSquared() < 0.0001f)
            outDir = Vector3.Forward;
        outDir = outDir.Normalized();
        _treeAngle = Mathf.Atan2(outDir.X, outDir.Z);

        // Preserve Mario's current Y on grab. Bottom release uses strict `<` so this won't
        // immediately eject as long as _treeBottomY is set BELOW where Mario stands.
        _treeY = Mathf.Min(GlobalPosition.Y, _treeTopY - 0.05f);

        SnapToTree();

        velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
        _treePinned = true;

        _treeGrabAnimT = 0f;
        if (animPlayer != null && animPlayer.HasAnimation("ma_tree_catch"))
            _treeGrabAnimDur = (float)animPlayer.GetAnimation("ma_tree_catch").Length;

        stateOfMario = MarioState.treeGrab;
        SetMarioState(stateOfMario);
        LockArmatureVisualRoot();
        return true;
    }

    private float SampleGroundUnderMario(out bool hit)
    {
        hit = false;
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return 0f;

        Vector3 from = GlobalPosition + new Vector3(0f, 0.1f, 0f);
        Vector3 to = from + new Vector3(0f, -TREE_GROUND_RAY, 0f);
        var q = PhysicsRayQueryParameters3D.Create(from, to);
        q.CollideWithAreas = false;
        q.CollideWithBodies = true;
        // Skip Mario himself + the trunk we're hugging
        var excludes = new Godot.Collections.Array<Godot.Rid> { GetRid() };
        if (_treeNode != null)
        {
            foreach (var child in _treeNode.GetChildren())
                if (child is StaticBody3D sb)
                    excludes.Add(sb.GetRid());
        }
        q.Exclude = excludes;
        var result = space.IntersectRay(q);
        if (result.Count == 0)
            return 0f;
        hit = true;
        return ((Vector3)result["position"]).Y;
    }

    private float CurrentTreeRadius()
    {
        if (Mathf.IsEqualApprox(_treeTopY, _treeBottomY))
            return _treeRadiusBottom;
        float t = Mathf.Clamp((_treeY - _treeBottomY) / (_treeTopY - _treeBottomY), 0f, 1f);
        return Mathf.Lerp(_treeRadiusBottom, _treeRadiusTop, t);
    }

    private void SnapToTree()
    {
        Vector3 outDir = new Vector3(Mathf.Sin(_treeAngle), 0f, Mathf.Cos(_treeAngle));
        Vector3 pos = _treeCenter + outDir * CurrentTreeRadius();
        pos.Y = _treeY;
        GlobalPosition = pos;
        // Face inward toward trunk
        armature.Rotation = new Vector3(0f, BaseYawFromDir(-outDir), 0f);
    }

    private void TickTreeGrab(double delta)
    {
        velocity = Vector3.Zero;
        SnapToTree();
        LockArmatureVisualRoot();

        // A jump pressed during the brief catch animation was silently dropped —
        // this function never checked for it, unlike TickTreeOnTrunk (which only
        // runs once the catch finishes). A quick, reflexive jump-off right after
        // grabbing (no time spent aiming a direction first) easily lands in this
        // window, since IsActionJustPressed only fires on the single frame of the
        // press — once that frame passes unchecked, the input is just gone.
        if (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))
        {
            DoTreeJumpOff();
            return;
        }

        _treeGrabAnimT += (float)delta;
        if (_treeGrabAnimT >= _treeGrabAnimDur)
        {
            stateOfMario = MarioState.treeWait;
            SetMarioState(stateOfMario);
        }
    }

    private void TickTreeOnTrunk(double delta, Vector2 stick)
    {
        // Tree was freed during play — drop Mario
        if (_treeNode != null && !IsInstanceValid(_treeNode))
        {
            ExitTreeToFall();
            return;
        }

        velocity = Vector3.Zero;

        // Jump off
        if (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))
        {
            DoTreeJumpOff();
            return;
        }

        // Stick reads (Lstick.Y: up = negative)
        bool stickUp = stick.Y < -0.55f;
        bool stickDown = stick.Y > 0.55f;
        bool stickLeft = stick.X < -0.55f;
        bool stickRight = stick.X > 0.55f;

        MarioState desired = MarioState.treeWait;

        if (stickUp)
        {
            _treeY += TREE_CLIMB_SPEED_Y * (float)delta;
            desired = MarioState.treeClimb;
        }
        else if (stickDown)
        {
            _treeY -= TREE_CLIMB_SPEED_Y * (float)delta;
            desired = MarioState.treeClimb;
        }
        else if (stickLeft)
        {
            _treeAngle -= TREE_ORBIT_SPEED * (float)delta;
            desired = MarioState.treeMoveL;
        }
        else if (stickRight)
        {
            _treeAngle += TREE_ORBIT_SPEED * (float)delta;
            desired = MarioState.treeMoveR;
        }

        // Reached top -> pop up onto leaves
        if (_treeY >= _treeTopY)
        {
            GD.Print($"[Tree] top reach: _treeY={_treeY:F2} _treeTopY={_treeTopY:F2}");
            DoTreeTopReach();
            return;
        }

        // Ground detection only fires while actively sliding DOWN, so a grab at ground
        // level doesn't immediately self-release. Handles trees with their root sunk
        // below the actual floor.
        if (stickDown)
        {
            float groundY = SampleGroundUnderMario(out bool groundHit);
            if (groundHit && _treeY <= groundY + TREE_GROUND_CLEAR)
            {
                GD.Print($"[Tree] ground release: _treeY={_treeY:F2} groundY={groundY:F2}");
                _treeY = groundY + TREE_GROUND_CLEAR;
                SnapToTree();
                ExitTreeToFall();
                return;
            }
        }

        // Slid past bottom -> release (strict < so a grab at the bottom doesn't auto-eject)
        if (_treeY < _treeBottomY)
        {
            GD.Print($"[Tree] bottom release: _treeY={_treeY:F2} _treeBottomY={_treeBottomY:F2}");
            ExitTreeToFall();
            return;
        }

        if (desired != stateOfMario)
        {
            stateOfMario = desired;
            SetMarioState(stateOfMario);
        }

        SnapToTree();
        LockArmatureVisualRoot();
    }

    private void DoTreeJumpOff()
    {
        Vector3 outDir = new Vector3(Mathf.Sin(_treeAngle), 0f, Mathf.Cos(_treeAngle));
        velocity = Vector3.Zero;
        velocity.X = outDir.X * TREE_JUMP_OFF_OUT;
        velocity.Z = outDir.Z * TREE_JUMP_OFF_OUT;
        velocity.Y = TREE_JUMP_OFF_UP;
        Velocity = velocity;

        _treePinned = false;
        _treeNode = null;
        _treeRegrabCooldown = TREE_REGRAB_COOLDOWN_FRAMES;
        _isTreeJumpAirborne = true;

        // Keep jump flags consistent with normal jumps (same reasoning as the real
        // wall-jump's own init) — without isJumping=true, the "walked off a ledge
        // without jumping" detector elsewhere sees a grounded-loco state (which he
        // can briefly, spuriously appear to be in for one tick right after this,
        // since IsOnFloor() is still stale-true until the next MoveAndSlide) and
        // !isJumping, and misclassifies this deliberate jump as an accidental
        // ledge walk-off — forcing stateOfMario to ledgeFall instead of leaving him
        // in wallJump for the rest of the arc.
        isJumping = true;
        initalJumpHold = true;
        jumpHoldTime = 0f;

        stateOfMario = MarioState.wallJump;
        SetMarioState(stateOfMario);

        // 50/50: half the time play ma_tjmp1 instead of the default ma_wjmp.
        // We stay in wallJump state for physics; just override the animation.
        // ma_tjmp1 is authored facing the opposite direction (same as sideflip),
        // so we add 180° to the armature yaw to make it face the jump direction.
        bool useTjmp1 = GD.Randf() < 0.5f;
        float yaw = BaseYawFromDir(outDir);
        if (useTjmp1)
            yaw += Mathf.Pi;
        armature.Rotation = new Vector3(0f, yaw, 0f);

        _treeJumpOffDir = outDir;
        _treeJumpOffTjmp1 = useTjmp1;

        // Always play directly via AnimationPlayer rather than relying on the state
        // machine's Travel("ma_wjmp") for the non-tjmp1 half: Travel() needs a valid
        // transition PATH through the graph from wherever the machine's "current
        // node" was frozen. The tree states (treeGrab/treeWait/...) freeze animTree
        // while grabbing, and if Mario ran INTO the tree at speed first, the
        // existing wall-push detection had already set the current node to
        // "ma_push" just before that freeze — Travel("ma_wjmp") from "ma_push" was
        // getting stuck and never actually transitioning, silently leaving him
        // frozen on ma_push for the whole jump. A direct Play() has no such
        // reachability requirement, so it works regardless of what state we were
        // frozen in. (SetMarioState already reactivates animTree.Active=true above
        // for every call, so the next real state change after landing correctly
        // hands control back to the tree either way.)
        if (animTree != null)
            animTree.Active = false;
        animPlayer.Play(useTjmp1 ? "ma_tjmp1" : "ma_wjmp");
    }

    private void DoTreeTopReach()
    {
        // Teleport Mario directly above the leaves at the trunk axis, then let
        // gravity drop him onto the leaf collision so he lands on top of the leaves.
        Vector3 outDir = new Vector3(Mathf.Sin(_treeAngle), 0f, Mathf.Cos(_treeAngle));
        GlobalPosition = new Vector3(_treeCenter.X, _treeLeafLandY, _treeCenter.Z);

        velocity = Vector3.Zero;
        velocity.Y = 0f; // no jump; the leaf collision will catch him
        Velocity = velocity;

        _treePinned = false;
        _treeNode = null;
        _treeRegrabCooldown = TREE_REGRAB_COOLDOWN_FRAMES;

        stateOfMario = MarioState.treeTopReach;
        SetMarioState(stateOfMario);
        // Face outward (same direction he was hugging the trunk from) so he doesn't snap-rotate
        armature.Rotation = new Vector3(0f, BaseYawFromDir(outDir), 0f);
    }

    private void ExitTreeToFall()
    {
        // Push Mario radially away from the trunk so he doesn't get stuck against
        // the trunk's solid collision (especially when the trunk has a trimesh hull
        // that hugs the visible mesh tightly).
        Vector3 outDir = new Vector3(Mathf.Sin(_treeAngle), 0f, Mathf.Cos(_treeAngle));
        const float TREE_RELEASE_OUT_SPEED = 4.5f;

        _treePinned = false;
        _treeNode = null;
        _treeRegrabCooldown = TREE_REGRAB_COOLDOWN_FRAMES;
        stateOfMario = MarioState.singleJump;
        SetMarioState(stateOfMario);

        velocity = Vector3.Zero;
        velocity.X = outDir.X * TREE_RELEASE_OUT_SPEED;
        velocity.Z = outDir.Z * TREE_RELEASE_OUT_SPEED;
        velocity.Y = -2f;
        Velocity = velocity;

        // Snap Mario a little further out so he starts the fall clear of the trunk.
        GlobalPosition += outDir * 0.15f;
    }

    private void AutoSetupLedgeRays()
    {
        if (chestRay == null || headRay == null || topDownRay == null)
            return;

        float radius = 0.7f;
        float height = 2.0f;

        var colShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (colShape?.Shape is CapsuleShape3D cap)
        {
            radius = cap.Radius;
            height = cap.Height; // capsule cylinder height (not total incl hemispheres)
        }
        else if (colShape?.Shape is CylinderShape3D cyl)
        {
            radius = cyl.Radius;
            height = cyl.Height; // cylinder height is already the full extent (flat caps)
        }

        float forwardLenWorld = 0.977382f; // 2.2 * SCALE_FIX
        float chestYWorld = 0.577543f; // 1.3 * SCALE_FIX
        float headYWorld = 1.043025f; // 2.35 * SCALE_FIX
        float topStartYWorld = 1.288366f; // 2.9 * SCALE_FIX
        float topDownLenWorld = 1.110661f; // 2.5 * SCALE_FIX
        float topForwardWorld = 0.888529f; // 2.0 * SCALE_FIX

        float invScale = GetLedgeSensorsInvScale();

        float forwardLenLocal = forwardLenWorld * invScale;
        float chestYLocal = chestYWorld * invScale;
        float headYLocal = headYWorld * invScale;
        float topStartYLocal = topStartYWorld * invScale;
        float topDownLenLocal = topDownLenWorld * invScale;
        float topForwardLocal = topForwardWorld * invScale;

        chestRay.Position = new Vector3(0f, chestYLocal, 0f);
        chestRay.TargetPosition = new Vector3(0f, 0f, -forwardLenLocal);

        headRay.Position = new Vector3(0f, headYLocal, 0f);
        headRay.TargetPosition = new Vector3(0f, 0f, -forwardLenLocal);

        topDownRay.Position = new Vector3(0f, topStartYLocal, -topForwardLocal);
        topDownRay.TargetPosition = new Vector3(0f, -topDownLenLocal, 0f);

        chestRay.Enabled = true;
        headRay.Enabled = true;
        topDownRay.Enabled = true;

        chestRay.ForceRaycastUpdate();
        headRay.ForceRaycastUpdate();
        topDownRay.ForceRaycastUpdate();

        GD.Print($"[Ledge] cap r={radius:F2} h={height:F2} invScale={invScale:F2}");
    }

    private float GetLedgeSensorsInvScale()
    {
        // Use LedgeSensors scale if present, otherwise Mario scale.
        // Assuming uniform scale (your (0.03,0.03,0.03) is uniform).
        float s = 1f;

        var ls = GetNodeOrNull<Node3D>("LedgeSensors");
        if (ls != null)
        {
            Vector3 sc = ls.GlobalTransform.Basis.Scale;
            s = (Mathf.Abs(sc.X) + Mathf.Abs(sc.Y) + Mathf.Abs(sc.Z)) / 3f;
        }
        else
        {
            Vector3 sc = GlobalTransform.Basis.Scale;
            s = (Mathf.Abs(sc.X) + Mathf.Abs(sc.Y) + Mathf.Abs(sc.Z)) / 3f;
        }

        if (s < 0.0001f)
            s = 0.0001f;
        return 1f / s;
    }

    private void InitLedgeRays()
    {
        if (chestRay == null || headRay == null || topDownRay == null)
            return;

        chestRay.Enabled = true;
        headRay.Enabled = true;
        topDownRay.Enabled = true;

        // IMPORTANT: ensure they actually hit physics bodies
        chestRay.CollideWithBodies = true;
        headRay.CollideWithBodies = true;
        topDownRay.CollideWithBodies = true;

        chestRay.CollideWithAreas = false;
        headRay.CollideWithAreas = false;
        topDownRay.CollideWithAreas = false;

        // TEMP: collide with EVERYTHING to prove the rays work
        const uint ALL = 0xFFFFFFFF;
        chestRay.CollisionMask = ALL;
        headRay.CollisionMask = ALL;
        topDownRay.CollisionMask = ALL;

        // avoid self hits
        chestRay.AddException(this);
        headRay.AddException(this);
        topDownRay.AddException(this);
    }

    private void DebugRayHit(RayCast3D rc, string name)
    {
        if (rc == null)
        {
            GD.Print($"[{name}] rc == null");
            return;
        }

        // Compute the ray in WORLD space:
        Vector3 from = rc.GlobalTransform.Origin;
        Vector3 to = from + rc.GlobalTransform.Basis * rc.TargetPosition;

        var space = GetWorld3D().DirectSpaceState;

        var q = PhysicsRayQueryParameters3D.Create(from, to);
        q.CollisionMask = rc.CollisionMask;
        q.CollideWithBodies = true;
        q.CollideWithAreas = false;
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() }; // exclude Mario body RID

        var hit = space.IntersectRay(q);

        if (hit.Count > 0)
        {
            var collider = hit["collider"];
            var pos = (Vector3)hit["position"];
            var normal = (Vector3)hit["normal"];
            GD.Print($"[{name}] HIT collider={collider} pos={pos} n={normal} from={from} to={to}");
        }
        else
        {
            GD.Print($"[{name}] NO HIT from={from} to={to} (len={(to - from).Length():F2})");
        }
    }

    private void UpdateLedgeSensorFacing()
    {
        if (ledgeSensors == null || armature == null)
            return;

        // Match the yaw you're actually using visually, but flip 180° to face forward
        float yaw =
            BaseYawFromDir(
                lastFacingDirection != Vector3.Zero ? lastFacingDirection : Vector3.Forward
            ) + Mathf.Pi; // Add PI to flip sensors to face Mario's forward direction
        var r = ledgeSensors.GlobalRotation;
        r.Y = yaw;
        ledgeSensors.GlobalRotation = r;
    }

    private void UpdatePickupCastFacing()
    {
        if (pickupCast == null || armature == null)
            return;

        // Get Mario's facing direction
        Vector3 facing =
            lastFacingDirection != Vector3.Zero ? lastFacingDirection : Vector3.Forward;
        facing = facing.Normalized();

        // Calculate yaw rotation
        float yaw = YawFromDir(facing);

        // Rotate the pickup cast's initial local position around Mario
        // This makes it orbit around Mario as he rotates
        Vector3 rotatedLocalPos = pickupCastInitialLocalPos.Rotated(Vector3.Up, yaw);

        // Set the rotated local position (relative to Mario)
        pickupCast.Position = rotatedLocalPos;

        // Also rotate the cast itself to face the same direction as Mario
        pickupCast.Rotation = new Vector3(0, yaw, 0);
    }

    private void CacheCapsuleWorldMetrics()
    {
        bodyCol = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        float radius;
        float halfHeight;
        if (bodyCol?.Shape is CapsuleShape3D cap)
        {
            radius = cap.Radius;
            halfHeight = cap.Height * 0.5f + cap.Radius; // Height excludes the hemispherical caps
        }
        else if (bodyCol?.Shape is CylinderShape3D cyl)
        {
            radius = cyl.Radius;
            halfHeight = cyl.Height * 0.5f; // flat caps — Height is already the full extent
        }
        else
        {
            GD.PushError("Expected CollisionShape3D with CapsuleShape3D or CylinderShape3D.");
            return;
        }

        // WORLD scale of the collision shape (assume uniform-ish)
        Vector3 sc = bodyCol.GlobalTransform.Basis.Scale;
        float s = (Mathf.Abs(sc.X) + Mathf.Abs(sc.Y) + Mathf.Abs(sc.Z)) / 3f;
        if (s < 0.0001f)
            s = 0.0001f;

        capRadiusWorld = radius * s;
        capHalfHeightWorld = halfHeight * s;

        // bottom of capsule in WORLD Y
        float bottomY = bodyCol.GlobalPosition.Y - capHalfHeightWorld;

        // store offset from Mario body origin to capsule bottom
        bottomOffsetFromBody = bottomY - GlobalPosition.Y; // usually negative
    }

    private float BodyToCollisionCenterOffsetY()
    {
        if (bodyCol == null)
            return 0f;
        return bodyCol.GlobalPosition.Y - GlobalPosition.Y;
    }

    // Given a desired capsule-bottom world Y, compute the Mario body world Y that achieves it.
    private float BodyYForCapsuleBottomY(float bottomWorldY)
    {
        float bodyToCol = BodyToCollisionCenterOffsetY();
        // capsule center should be at bottom + capHalfHeightWorld
        float desiredColCenterY = bottomWorldY + capHalfHeightWorld;
        // therefore Mario body should be centerY - (body->col offset)
        return desiredColCenterY - bodyToCol;
    }

    private void SnapBodyToFloorUnderSelf()
    {
        var space = GetWorld3D().DirectSpaceState;

        // cast from slightly above our current position straight down
        Vector3 from = GlobalPosition + Vector3.Up * (capHalfHeightWorld * 0.6f);
        Vector3 to = GlobalPosition + Vector3.Down * (capHalfHeightWorld * 2.5f);

        var q = PhysicsRayQueryParameters3D.Create(from, to);
        q.CollisionMask = CollisionMask;
        q.CollideWithBodies = true;
        q.CollideWithAreas = false;
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = space.IntersectRay(q);
        if (hit.Count == 0)
            return;

        Vector3 p = (Vector3)hit["position"];
        Vector3 n = (Vector3)hit["normal"];

        // only snap to reasonably walkable surfaces
        if (n.Dot(Vector3.Up) < 0.65f)
            return;

        float desiredBodyY = BodyYForCapsuleBottomY(p.Y) + LEDGE_STAND_UP;
        GlobalPosition = new Vector3(GlobalPosition.X, desiredBodyY, GlobalPosition.Z);
    }

    /// <summary>
    /// Procedural running bob. The baked run clips are flat (no vertical root
    /// motion), so Mario glides. This lifts the whole visual rig (the Armature
    /// node — physics body is untouched) between footfalls, phase-locked to the
    /// run clip's playback so the low point lands on each footplant. It's biased
    /// upward-only: 0 at footplant (feet stay grounded) rising to +amplitude at
    /// mid-stride. A proper downward compression dip needs foot IK (Phase 2).
    /// </summary>
    private void UpdateRunBob(float delta)
    {
        if (armature == null)
            return;

        bool running =
            (stateOfMario == MarioState.running || stateOfMario == MarioState.sprinting)
            && IsOnFloor();
        _runBobWeight = Mathf.MoveToward(_runBobWeight, running ? 1f : 0f, RunBobEaseSpeed * delta);

        // Drive the phase off the run clip's own playback so the bob tracks the
        // visible legs; freeze it when we're not on a run clip (e.g. blending out).
        if (_sm != null)
        {
            float len = _sm.GetCurrentLength();
            if (len > 0.0001f && _sm.GetCurrentNode().ToString().Contains("run"))
                _runBobPhase = _sm.GetCurrentPlayPosition() / len * Mathf.Tau * RunBobDipsPerLoop;
        }

        if (_runBobWeight <= 0.0001f)
            return;

        // While the surface tilt owns the rig it also owns armature.Position (it
        // sinks him along his own down axis to keep the belly on the slope), and
        // this runs in _Process — AFTER the tilt's _PhysicsProcess write. Bailing
        // out here stops the bob's tail-off from stomping the sink for the few
        // frames its weight takes to ease back to zero after a dive starts. The
        // weight/phase updates above still run, so it eases out normally.
        if (_wasPlatformTilting)
            return;

        float speedFactor = Mathf.Clamp(
            new Vector2(velocity.X, velocity.Z).Length() / RUN_SPEED,
            0.5f,
            1f
        );
        float bob =
            RunBobAmplitude
            * speedFactor
            * _runBobWeight
            * (0.5f - 0.5f * Mathf.Cos(_runBobPhase + RunBobPhaseOffset));

        armature.Position = new Vector3(
            armatureBaseLocalPos.X,
            armatureBaseLocalPos.Y + bob,
            armatureBaseLocalPos.Z
        );
    }

    /// <summary>Eases the head-forward lock in while Mario is moving (walk/run/
    /// sprint) and out otherwise, cancelling the side-to-side head turn baked into
    /// the locomotion clips. The actual pose override runs in HeadForwardLock (a
    /// skeleton modifier), after the animation.</summary>
    /// <summary>Eases the slope foot IK in for IDLE and PIVOT only, and out
    /// everywhere else.
    ///
    /// Scoped that tightly on purpose: SMS doesn't plant feet while walking or
    /// running, and it also keeps foot IK off jnt_waist while the running waist
    /// IK (skeletonIK3DWaist, root_bone = jnt_waist) owns that same bone. Both
    /// writing the waist in one frame would fight. The two state sets are
    /// disjoint — running/sprinting for the waist, idle/pivot here — so they can
    /// never overlap, and that's load-bearing rather than coincidence.
    ///
    /// Also requires IsOnFloor(): mid-air the probe would either miss entirely
    /// or, worse, find ground far below and stretch his legs toward it.</summary>
    /// <summary>Slope steeper than <see cref="SleepMaxSlopeDegrees"/> (measured
    /// from flat) blocks the idle sit/sleep wind-down. Those clips lay him out
    /// flat, so on an incline he'd sit and lie straight through the surface.
    /// Small by design — this is meant to catch genuine ramps, not to stop him
    /// sleeping on ground that's a fraction off level.</summary>
    private bool OnSleepBlockingSlope()
    {
        if (!IsOnFloor())
            return false;
        return Mathf.RadToDeg(GetFloorNormal().AngleTo(Vector3.Up)) > SleepMaxSlopeDegrees;
    }

    private void UpdateFootIK(float delta)
    {
        if (_footIK == null)
            return;

        // Sitting/sleeping is an idle VARIANT, not its own MarioState — the
        // timer chain in the idle branch plays ma_sit -> ma_sit_wait ->
        // ma_sleep -> ma_sleep_wait straight through animPlayer while
        // stateOfMario stays idle. So it can't be gated on state. His feet
        // leave the standing pose entirely in those clips (he's sat down),
        // and planting them to the ground fights the animation.
        bool sittingOrAsleep = false;
        if (animPlayer != null)
        {
            string clip = animPlayer.CurrentAnimation;
            sittingOrAsleep =
                !string.IsNullOrEmpty(clip)
                && (clip.StartsWith("ma_sit") || clip.StartsWith("ma_sleep"));
        }

        // Idle AND pivot. Pivot was briefly excluded because the IK was planting
        // the foot the turn picks up — but that was a flaw in how the targets
        // were built (forcing each ankle onto its hit point), not something
        // inherent to pivot. FootIK now measures each foot's ANIMATED lift
        // against the ground under the body and re-references it to the ground
        // under that foot, so a lifted foot stays lifted and only the terrain
        // difference is applied. See the probe section in FootIK.
        bool wants =
            FootIKEnabled
            && IsOnFloor()
            && !sittingOrAsleep
            && (stateOfMario == MarioState.idle || stateOfMario == MarioState.pivot);

        _footIKWeight = Mathf.MoveToward(_footIKWeight, wants ? 1f : 0f, FootIKEaseSpeed * delta);
        _footIK.Weight = _footIKWeight;
    }

    private void UpdateHeadLock(float delta)
    {
        if (_headLock == null)
            return;

        bool loco =
            stateOfMario == MarioState.walking
            || stateOfMario == MarioState.running
            || stateOfMario == MarioState.sprinting;
        _headLockWeight = Mathf.MoveToward(
            _headLockWeight,
            (HeadLockEnabled && loco) ? 1f : 0f,
            HeadLockEaseSpeed * delta
        );
        _headLock.Weight = _headLockWeight;
        _headLock.LookUpDegrees = HeadLookUpDegrees;
        _headLock.LookUpAxisLocal = HeadLookUpAxis;
    }

    /// <summary>Updates _turnLeanCurrentDeg — how far the WAIST should lean
    /// left/right right now, from how fast his visual facing is turning (NOT
    /// the jiggle spring — a sustained lean for the duration of the turn,
    /// proportional to sharpness, not a bounce-and-settle reaction). Runs on
    /// the physics tick using the fixed delta throughout (both the rate sample
    /// in _PhysicsProcess and this ease), since the underlying signal only
    /// changes on physics ticks — mixing in render-rate delta here previously
    /// produced huge spikes on frames right after a tick and exactly zero
    /// otherwise. The actual bone application happens at the waist-tilt site
    /// (near "Waist tilt: lean forward"), not here — this only tracks the value.</summary>
    private void UpdateTurnLean(float delta)
    {
        float targetLeanDeg = TurnLeanEnabled
            ? Mathf.Clamp(
                -_turnYawRateDeg * TurnLeanSensitivity,
                -TurnLeanMaxDegrees,
                TurnLeanMaxDegrees
            )
            : 0f;

        // Frame-rate-independent exponential ease toward the target — smooth
        // lean-in/lean-out instead of snapping straight to it every tick.
        float t = 1f - Mathf.Exp(-TurnLeanEaseSpeed * delta);
        _turnLeanCurrentDeg = Mathf.Lerp(_turnLeanCurrentDeg, targetLeanDeg, t);
    }

    /// <summary>Pushes the tunable jiggle params + frame delta to the head and
    /// arm spring-bone modifiers (which do the actual sim after the animation).</summary>
    private void UpdateJiggle(float delta)
    {
        if (_headJiggle != null)
        {
            _headJiggle.Weight = HeadJiggleEnabled ? HeadJiggleWeight : 0f;
            _headJiggle.Stiffness = HeadJiggleStiffness;
            _headJiggle.Damping = HeadJiggleDamping;
            _headJiggle.Response = HeadJiggleResponse;
            _headJiggle.Gain = HeadJiggleGain;
            _headJiggle.MaxShiftMeters = HeadJiggleMaxShiftCm / 100f;
            _headJiggle.Dt = delta;
            _headJiggle.DriveAccel = _marioHorizAccel;
        }
        foreach (var arm in new[] { _armJiggleR, _armJiggleL })
        {
            if (arm == null)
                continue;
            arm.Weight = ArmJiggleEnabled ? ArmJiggleWeight : 0f;
            arm.Stiffness = ArmJiggleStiffness;
            arm.Damping = ArmJiggleDamping;
            arm.Response = ArmJiggleResponse;
            arm.Gain = ArmJiggleGain;
            arm.MaxShiftMeters = ArmJiggleMaxShiftCm / 100f;
            arm.Dt = delta;
            arm.DriveAccel = _marioHorizAccel;
        }
    }

    private void LockArmatureVisualRoot()
    {
        if (armature == null)
            return;

        // Force the visual rig to stay at its authored rest offset.
        // This cancels any AnimationPlayer tracks like "Armature:position".
        armature.Position = armatureBaseLocalPos;
        armature.Scale = armatureBaseLocalScale;
    }

    // capHalfHeightWorld bakes in an extra +radius probe margin (see
    // CacheCapsuleWorldMetrics), so the true center-to-bottom distance is that minus
    // capRadiusWorld.
    private Vector3 ComputeFeetPosition()
    {
        float bottomY =
            (bodyCol != null ? bodyCol.GlobalPosition.Y : GlobalPosition.Y)
            - (capHalfHeightWorld - capRadiusWorld);
        return new Vector3(GlobalPosition.X, bottomY, GlobalPosition.Z);
    }

    private void TriggerLandingDust()
    {
        if (landingDustFx == null)
            return;

        Vector3 n = Vector3.Up;
        if (IsOnFloor())
            n = GetFloorNormal().Normalized();

        landingDustFx.Trigger(ComputeFeetPosition(), n);
    }

    private void EnterPickupFail()
    {
        // only on floor use-case
        velocity.Y = 0f;
        velocity.X = 0f;
        velocity.Z = 0f;

        pickupFailT = 0f;
        stateOfMario = MarioState.pickupFail;

        SetMarioState(stateOfMario);
    }

    private bool TickPickupFail(double delta, Vector3 inputDirWorld, float stickStrength)
    {
        // Own the frame while playing
        velocity.Y = 0f;
        velocity.X = 0f;
        velocity.Z = 0f;

        if (animPlayer.CurrentAnimation != "ma_get_fail")
            SetMarioState(stateOfMario);

        pickupFailT += (float)delta;

        // finished?
        if (pickupFailT >= pickupFailDur - 0.01f)
        {
            // decide next state based on input
            if (stickStrength > DEADZONE && inputDirWorld != Vector3.Zero)
            {
                direction = inputDirWorld;
                walkingStrength = stickStrength;

                stateOfMario = (stickStrength > 0.5f) ? MarioState.sprinting : MarioState.running;
                animPlayer.Play((stateOfMario == MarioState.sprinting) ? "ma_run2" : "ma_run1");
            }
            else
            {
                stateOfMario = MarioState.idle;
                animPlayer.Play("ma_wait");
                SetMarioState(stateOfMario);
            }

            return false; // no longer owns the frame
        }

        return true; // still owning the frame
    }

    private bool TryFindPickable(out RigidBody3D body)
    {
        body = null;
        if (pickupCast == null)
            return false;

        pickupCast.ForceShapecastUpdate();
        if (!pickupCast.IsColliding())
            return false;

        int count = pickupCast.GetCollisionCount();
        for (int i = 0; i < count; i++)
        {
            var obj = pickupCast.GetCollider(i);
            if (obj is RigidBody3D rb && rb.IsInGroup("pickable"))
            {
                body = rb;
                return true;
            }
        }
        return false;
    }

    public void Anim_AttachPickup()
    {
        GD.Print($"carrySocket gscale = {carrySocket.GlobalTransform.Basis.Scale}");
        GD.Print($"held pre-attach gscale = {pendingPickup.GlobalTransform.Basis.Scale}");

        if (pendingPickup == null || heldBody != null || carrySocket == null)
            return;

        heldBody = pendingPickup;
        pendingPickup = null;

        heldLayer = heldBody.CollisionLayer;
        heldMask = heldBody.CollisionMask;

        heldBody.LinearVelocity = Vector3.Zero;
        heldBody.AngularVelocity = Vector3.Zero;
        heldBody.Freeze = true;

        heldBody.CollisionLayer = 0;
        heldBody.CollisionMask = 0;

        heldBody.TopLevel = true; // <- key
        var gt = carrySocket.GlobalTransform;
        gt.Basis = gt.Basis.Orthonormalized();
        heldBody.GlobalTransform = gt;

        // force world scale to 1 so it’s visible (since TopLevel ignores parent)
        var t = heldBody.GlobalTransform;
        t.Basis = t.Basis.Orthonormalized(); // keeps rotation clean
        heldBody.GlobalTransform = t;

        heldBody.Scale = Vector3.One; // optional but usually fine

        heldBody.Visible = true;
        GD.Print($"held post-attach gscale = {heldBody.GlobalTransform.Basis.Scale}");
        GD.Print($"held local scale = {heldBody.Scale}");
    }

    public void Anim_DetachPickup()
    {
        if (heldBody == null)
            return;

        var rb = heldBody;
        heldBody = null;

        rb.TopLevel = false;
        rb.Freeze = false;

        rb.CollisionLayer = heldLayer;
        rb.CollisionMask = heldMask;

        // If a throw was queued (StartThrow), use the captured throw facing — even
        // if the ma_throw animation didn't play and ma_put fired instead, we still
        // honor the throw arc.
        if (_isThrowQueued)
        {
            rb.LinearVelocity =
                _queuedThrowFacing * THROW_FORWARD_BOOST + Vector3.Up * THROW_UP_BOOST;
            _isThrowQueued = false;
            GD.Print(
                $"[Throw] Detached with arc — facing={_queuedThrowFacing}, vel={rb.LinearVelocity}"
            );
            return;
        }

        // Regular put-down or pickup-cancel: apply Mario's current velocity (slight
        // throw if he's moving, zero if stationary).
        Vector3 horizVel = new Vector3(velocity.X, 0f, velocity.Z);
        if (horizVel.LengthSquared() > 1.0f)
        {
            Vector3 facing = horizVel.Normalized();
            rb.LinearVelocity =
                horizVel + facing * THROW_FORWARD_BOOST + Vector3.Up * THROW_UP_BOOST;
        }
        else
        {
            rb.LinearVelocity = Vector3.Zero;
        }
    }

    // Throw state — Mario stops mid-action, plays ma_throw, then releases fruit on anim finish.
    private bool _isThrowQueued = false;
    private Vector3 _queuedThrowFacing = Vector3.Forward;

    // Blocks airborne dive triggers while the aerial throw anim is playing — otherwise
    // a B-press release (IsActionPressed stays true the frame after JustPressed fires)
    // would tip Mario into a dive and replace ma_throw with ma_sldct.
    private float _aerialThrowLockTimer = 0f;
    private const float AERIAL_THROW_LOCK_DURATION = 0.45f;

    /// <summary>
    /// Ground throw: Mario stops + plays ma_throw, and the fruit is released
    /// IMMEDIATELY (this frame) at a 45° launch — not on animation finish.
    /// Mario returns to idle when ma_throw completes; if the player is still
    /// holding the stick he re-accelerates from there.
    /// </summary>
    private void StartThrow(Vector3 stickDirWorld)
    {
        if (heldBody == null)
            return;

        // Pick the throw direction from Mario's intent: stick > running velocity >
        // last-facing > armature forward.
        Vector3 facing;
        Vector3 stickHoriz = new Vector3(stickDirWorld.X, 0f, stickDirWorld.Z);
        Vector3 horizVel = new Vector3(velocity.X, 0f, velocity.Z);
        if (stickHoriz.LengthSquared() > 0.04f)
            facing = stickHoriz.Normalized();
        else if (horizVel.LengthSquared() > 0.25f)
            facing = horizVel.Normalized();
        else if (lastFacingDirection.LengthSquared() > 0.01f)
            facing = new Vector3(lastFacingDirection.X, 0f, lastFacingDirection.Z).Normalized();
        else
            facing = -armature.GlobalTransform.Basis.Z.Normalized();

        // Snap Mario's facing toward the throw direction (visual cohesion with the anim)
        lastFacingDirection = facing;
        armature.Rotation = new Vector3(0f, BaseYawFromDir(facing), 0f);

        // Stop Mario for the throw stance.
        velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
        stateOfMario = MarioState.putDown; // reuse the state-lock semantics of putDown
        // Flag stays TRUE for the duration of the anim so the per-frame putDown
        // handler at the top of _PhysicsProcess picks ma_throw (not ma_put).
        // Anim_DetachPickup is a no-op since heldBody is already null below.
        _isThrowQueued = true;
        if (animTree != null)
            animTree.Active = false;

        // Detach the fruit immediately at 45°.
        var rb = heldBody;
        heldBody = null;
        rb.TopLevel = false;
        rb.Freeze = false;
        rb.CollisionLayer = heldLayer;
        rb.CollisionMask = heldMask;

        // Per-fruit throw-speed multiplier (heavy fruits don't fly as far).
        float speedMul = (rb is Fruit fruitGround) ? fruitGround.ThrowSpeedMultiplier : 1.0f;
        Vector3 throwVel =
            facing * (THROW_FORWARD_VELOCITY * speedMul)
            + Vector3.Up * (THROW_UP_VELOCITY * speedMul);
        // Snap the fruit to a point in FRONT of Mario's center (not relative to
        // its current carry-socket position, which is at his right hand and
        // would leave the fruit brushing his right side and pushing him left
        // via MoveAndSlide ejection).
        rb.GlobalPosition = GlobalPosition + facing * 1.2f + Vector3.Up * 1.0f;
        rb.Sleeping = false;
        rb.SetDeferred("linear_velocity", throwVel);
        rb.SetDeferred("angular_velocity", Vector3.Zero);

        // Hard guarantee Mario can't be shoved by this fruit ever — physics
        // server-level exclusion. Cleared shortly after via a timer so the
        // thrown fruit can hit Mario later if it comes back around.
        rb.AddCollisionExceptionWith(this);
        var clearExc = GetTree().CreateTimer(0.6);
        clearExc.Timeout += () =>
        {
            if (IsInstanceValid(rb))
                rb.RemoveCollisionExceptionWith(this);
        };

        // VERY slight backwards recoil — one-shot position offset (velocity gets
        // zeroed every frame while in putDown state, so we can't use velocity).
        GlobalPosition -= facing * 0.08f;

        // Clear carry blend so Mario instantly drops the holding pose.
        if (animTree != null && animTree.Active)
            animTree.Set(CARRY_BLEND_PATH, 0.0f);

        string animName = animPlayer.HasAnimation("ma_throw") ? "ma_throw" : "ma_put";
        animPlayer.Play(animName);
        GD.Print(
            $"[Throw] StartThrow — playing '{animName}', facing={facing}, throwVel={throwVel}"
        );
    }

    /// <summary>
    /// Instant throw — both ground and air paths. Fruit leaves Mario's hand at
    /// a 45° launch angle in his facing direction with NO velocity inheritance
    /// and NO effect on Mario himself. Mario continues whatever he was doing
    /// (running, jumping, etc) without state lock or velocity zero.
    /// </summary>
    private void ThrowHeldInstant(Vector3 stickDirWorld = default)
    {
        if (heldBody == null)
        {
            GD.Print("[Throw] ThrowHeldInstant: heldBody is null, aborting");
            return;
        }

        var rb = heldBody;
        heldBody = null;
        rb.TopLevel = false;
        rb.Freeze = false;
        rb.CollisionLayer = heldLayer;
        rb.CollisionMask = heldMask;

        // Pick the throw direction from Mario's intent: stick > running velocity >
        // last-facing > armature forward. This is purely the horizontal direction;
        // we do NOT add Mario's current velocity into the throw (the user wants the
        // throw to be 45° regardless of how fast Mario is moving).
        Vector3 facing;
        Vector3 stickHoriz = new Vector3(stickDirWorld.X, 0f, stickDirWorld.Z);
        Vector3 horizVel = new Vector3(velocity.X, 0f, velocity.Z);
        if (stickHoriz.LengthSquared() > 0.04f)
            facing = stickHoriz.Normalized();
        else if (horizVel.LengthSquared() > 0.25f)
            facing = horizVel.Normalized();
        else if (lastFacingDirection.LengthSquared() > 0.01f)
            facing = new Vector3(lastFacingDirection.X, 0f, lastFacingDirection.Z).Normalized();
        else
            facing = -armature.GlobalTransform.Basis.Z.Normalized();

        // SMS steep-arc launch — same as ground throw. Per-fruit multiplier scales
        // both axes (heavy fruits don't fly as far OR as high).
        float speedMul = (rb is Fruit fruitAir) ? fruitAir.ThrowSpeedMultiplier : 1.0f;
        Vector3 throwVel =
            facing * (THROW_FORWARD_VELOCITY * speedMul)
            + Vector3.Up * (THROW_UP_VELOCITY * speedMul);

        // Snap the fruit to Mario's center + forward, same as ground throw, so
        // it can't brush his right side (carrySocket is on RightHandBone).
        rb.GlobalPosition = GlobalPosition + facing * 1.2f + Vector3.Up * 1.0f;

        // Physics-server exclusion so the thrown fruit cannot push Mario at all,
        // regardless of overlap. Cleared after 0.6s so the fruit can hit him later.
        rb.AddCollisionExceptionWith(this);
        var clearExc = GetTree().CreateTimer(0.6);
        clearExc.Timeout += () =>
        {
            if (IsInstanceValid(rb))
                rb.RemoveCollisionExceptionWith(this);
        };

        // Apply velocity DEFERRED — setting LinearVelocity during _PhysicsProcess
        // on a body just unfrozen this same frame gets clobbered by the engine's
        // first integration. Deferring lets the new velocity stick cleanly.
        rb.Sleeping = false;
        rb.SetDeferred("linear_velocity", throwVel);
        rb.SetDeferred("angular_velocity", Vector3.Zero);
        GD.Print($"[Throw] Instant detach — rb={rb.Name}, facing={facing}, throwVel={throwVel}");

        // Clear the carry blend so Mario instantly drops the holding pose.
        if (animTree != null && animTree.Active)
            animTree.Set(CARRY_BLEND_PATH, 0.0f);

        // Only swap to ma_throw if Mario is airborne — on the ground he should
        // keep running without an animation hiccup. The dive-lock prevents the
        // airborne B-press from being re-read as a dive on the next frame.
        if (!IsOnFloor() && animPlayer != null && animPlayer.HasAnimation("ma_throw"))
        {
            if (animTree != null)
                animTree.Active = false;
            animPlayer.Play("ma_throw");
            _aerialThrowLockTimer = AERIAL_THROW_LOCK_DURATION;
        }
    }

    /// <summary>Called from OnAnimationFinished when ma_throw completes — actually releases the held body with the throw arc.</summary>
    private void Anim_FinishThrow()
    {
        if (heldBody == null)
            return;
        var rb = heldBody;
        heldBody = null;
        rb.TopLevel = false;
        rb.Freeze = false;
        rb.CollisionLayer = heldLayer;
        rb.CollisionMask = heldMask;
        rb.LinearVelocity = _queuedThrowFacing * THROW_FORWARD_BOOST + Vector3.Up * THROW_UP_BOOST;
    }

    private const float THROW_FORWARD_BOOST = 9f;
    private const float THROW_UP_BOOST = 3f;

    // SMS-style steep arc — fruit lobs up far more than it travels forward.
    // Up:forward ratio ≈ 2.2:1, launch angle ≈ 65° from horizontal.
    private const float THROW_UP_VELOCITY = 39f;
    private const float THROW_FORWARD_VELOCITY = 28f;

    private void StartPickupRaise(RigidBody3D target)
    {
        pendingPickup = target;

        // stop Mario movement during raise
        velocity.X = 0f;
        velocity.Z = 0f;
        velocity.Y = 0f;

        stateOfMario = MarioState.pickupRaise;
        animPlayer.Play("ma_raise");
    }

    /// <summary>
    /// Public entry for auto-pickup from items like fruit. Called by Fruit.cs when
    /// Mario enters the fruit's PickupZone Area3D. Returns true if pickup started.
    /// </summary>
    public bool TryStartFruitPickup(RigidBody3D target)
    {
        if (target == null)
            return false;
        if (heldBody != null)
            return false;
        if (pendingPickup != null)
            return false;
        if (CameraLocked)
            return false;
        if (!IsOnFloor())
            return false; // SMS lets you pick up only on ground

        // Block during inappropriate states
        switch (stateOfMario)
        {
            case MarioState.pickupRaise:
            case MarioState.putDown:
            case MarioState.carrying:
            case MarioState.pickupFail:
            case MarioState.diving:
            case MarioState.bellySlidingFromDive:
            case MarioState.groundPoundStartup:
            case MarioState.groundPoundFalling:
            case MarioState.groundPoundLanding:
            case MarioState.hurt:
            case MarioState.dead:
            case MarioState.ledgeHang:
            case MarioState.ledgeClimb:
            case MarioState.ledgeHopUp:
            case MarioState.treeGrab:
            case MarioState.treeWait:
            case MarioState.treeClimb:
            case MarioState.treeMoveL:
            case MarioState.treeMoveR:
                return false;
        }

        StartPickupRaise(target);
        return true;
    }

    private ShineSprite _pendingShine;
    private ShineSprite _activeShine; // the shine mid-cutscene; told to free itself when ma_demo_shine_get ends

    /// <summary>
    /// Public entry for shine sprite collection. Called by ShineSprite.cs when Mario
    /// enters its PickupZone Area3D. Returns true if the shine has been claimed — the
    /// shine should disable its pickup zone as soon as this returns true, but the
    /// actual cutscene (freeze, animation, camera) doesn't necessarily start yet: if
    /// Mario is airborne when he touches it, it waits until he lands (see the
    /// _pendingShine check in _PhysicsProcess) rather than freezing him mid-air.
    /// </summary>
    public bool TryCollectShine(ShineSprite shine)
    {
        if (CameraLocked)
            return false;

        switch (stateOfMario)
        {
            case MarioState.shineGet:
            case MarioState.pickupRaise:
            case MarioState.carrying:
            case MarioState.putDown:
            case MarioState.hurt:
            case MarioState.dead:
                return false;
        }

        // Teleport-snap Mario to the shine's X and Z (its origin) so he's
        // directly under it — instant, keeping his Y so he then falls to the
        // ground from wherever he grabbed it.
        Vector3 sp = shine.GlobalPosition;
        GlobalPosition = new Vector3(sp.X, GlobalPosition.Y, sp.Z);

        if (IsOnFloor())
        {
            StartShineGet(shine);
        }
        else
        {
            // Touched mid-air: lock him NOW so he can't keep jumping/steering.
            // XZ is already snapped to the shine; kill horizontal so he drops
            // straight down in the normal fall animation until the cutscene starts.
            _pendingShine = shine;
            CameraLocked = true;
            isSpining(false);
            stateOfMario = MarioState.ledgeFall;
            SetMarioState(stateOfMario);
            velocity.X = 0f;
            velocity.Z = 0f;
        }

        return true;
    }

    private void StartShineGet(ShineSprite shine)
    {
        // Shine collection owns Mario's presentation from here on; no previous
        // airborne move may leave its visual effects running into the cutscene.
        isSpining(false);

        EmitSignal(SignalName.ShineCollected);

        // Kill ALL momentum and plant him — no slide, no residual fall/arc.
        // (StartShineGet only fires once he's grounded, so zeroing Y here just
        // stops him dead; the CameraLocked freeze keeps him there.)
        velocity = Vector3.Zero;
        Velocity = Vector3.Zero;

        CameraLocked = true;
        stateOfMario = MarioState.shineGet;
        SetMarioState(stateOfMario);

        // If he ran/sprinted into the shine: the sprint state left his hands
        // swapped to the CLOSED meshes and the waist IK running. Neither gets
        // reset by the CameraLocked path, so they bleed into ma_demo_shine_get —
        // undo both here so he holds the shine with an OPEN hand and a clean pose.
        ShowOpenHands();
        StopWaistIKHard();

        // Order matters: BeginCollectSequence teleports the shine to its sky
        // start point (the scripted fly-down), and EnterShineGetShot's first
        // LookAt aims at wherever the shine already is — so the shine must
        // move first or the camera's opening frame aims at the ground.
        float visualYaw = _camera?.CurrentVisualYaw ?? GlobalRotation.Y;
        _activeShine = shine;
        shine.BeginCollectSequence(this, visualYaw);

        // Hand the render camera to the hand-authored cutscene camera. The rig
        // is snapped to Mario's position + facing so the keyframed move plays
        // out relative to him no matter where in the level he collects the
        // shine, then Play() runs the animation in lockstep with ma_demo_shine_get.
        if (_shineGetCam != null && _shineGetCamAP != null)
        {
            if (_shineGetCamRig != null)
                _shineGetCamRig.GlobalTransform = new Transform3D(
                    new Basis(Vector3.Up, visualYaw + Mathf.Pi),
                    GlobalPosition
                );

            // Play once and hold on the last frame — never loop the cutscene.
            var camAnim = _shineGetCamAP.GetAnimation("ShineGet");
            if (camAnim != null)
                camAnim.LoopMode = Animation.LoopModeEnum.None;

            _shineGetCam.MakeCurrent();
            _shineGetCamAP.Play("ShineGet");
            GD.Print(
                $"[ShineGet] switched to ShineGetCam (current={_shineGetCam.Current}), playing '{_shineGetCamAP.CurrentAnimation}'"
            );
        }
        else
        {
            GD.PushWarning(
                $"[ShineGet] camera nodes not found under Mario — rig={(_shineGetCamRig != null)}, cam={(_shineGetCam != null)}, ap={(_shineGetCamAP != null)}. Check the node paths 'ShineGetCamRig/ShineGetCam' and 'ShineGetCamRig/ShineGetCamAP'."
            );
        }

        // Show the collectibles HUD right away (at the current count), then tick
        // the shine count up a couple seconds later (mid-cutscene) with a flourish.
        ShowCollectibleHud();
        ScheduleShineCountUp();
        SpawnShineGetPopup();
    }

    /// Colorful "SHINE!" banner seen in reference footage of the real
    /// cutscene — spawned fresh each time, frees itself when its animation
    /// finishes (see ShineGetPopup.cs), not a persistent HUD element.
    private void SpawnShineGetPopup(string popupText = "Wonderful!")
    {
        var popupScene = GD.Load<PackedScene>("res://Font/HudElements/ShineGetPopup.tscn");
        if (popupScene == null)
            return;

        var popup = popupScene.Instantiate<ShineGetPopup>();
        popup.PopupText = popupText; // dynamic word; set before entering the tree

        // Wrap in a CanvasLayer so the banner renders in screen space, centered
        // and on top of everything, instead of as a raw Control under a Node3D.
        var layer = new CanvasLayer { Layer = 100 };
        layer.AddChild(popup);
        AddChild(layer);
    }

    private void StartPutDown()
    {
        velocity = Vector3.Zero;
        stateOfMario = MarioState.putDown;
        animPlayer.Play("ma_put");
    }

    private void OnAnimationFinished(StringName animName)
    {
        // PICKUP RAISE finished
        if (stateOfMario == MarioState.pickupRaise && animName == "ma_raise")
        {
            // If you attach via a call track, heldBody will already be set.
            // If not, fallback attach here.
            if (heldBody == null)
            {
                if (pendingPickup != null && carrySocket != null)
                    Anim_AttachPickup();
            }

            if (heldBody == null)
            {
                pendingPickup = null;
                EnterPickupFail();
                return;
            }

            stateOfMario = MarioState.carrying;
            SetMarioState(stateOfMario);

            return;
        }

        // PUT DOWN / THROW (when using ma_put as fallback for ma_throw) finished
        if (stateOfMario == MarioState.putDown && (animName == "ma_put" || animName == "ma_throw"))
        {
            GD.Print($"[Throw] Anim finished '{animName}', _isThrowQueued={_isThrowQueued}");
            Anim_DetachPickup(); // applies throw arc if _isThrowQueued, else gentle drop
            _isThrowQueued = false; // clear so the next put-down plays ma_put correctly
            stateOfMario = MarioState.idle;
            animPlayer.Play("ma_wait");
            SetMarioState(stateOfMario);
            return;
        }

        // SHINE GET cutscene finished — hand control back to the player.
        if (stateOfMario == MarioState.shineGet && animName == "ma_demo_shine_get")
        {
            CameraLocked = false;
            stateOfMario = MarioState.idle;
            animPlayer.Play("ma_wait");
            SetMarioState(stateOfMario);

            // Return rendering to the normal gameplay camera.
            camera?.MakeCurrent();

            // The shine rides Mario's hand and spins on a looping animation, so
            // it can't signal its own end — tell it to free itself now.
            if (_activeShine != null && IsInstanceValid(_activeShine))
                _activeShine.OnCollectSequenceEnd();
            _activeShine = null;

            return;
        }

        // Aerial instant-throw path — body already detached, just resume anim tree.
        if (animName == "ma_throw" && !_isThrowQueued)
        {
            if (animTree != null)
                animTree.Active = true;
            SetMarioState(stateOfMario);
            return;
        }
    }

    private bool ShouldUseCarryRunTree()
    {
        // only while holding something and doing ground locomotion
        if (heldBody == null)
            return false;
        if (!IsOnFloor())
            return false;

        return stateOfMario == MarioState.running
            || stateOfMario == MarioState.sprinting
            || stateOfMario == MarioState.walking
            || stateOfMario == MarioState.sneak
            || stateOfMario == MarioState.idle
            || stateOfMario == MarioState.pivot;
    }

    /// <summary>
    /// Hang a character's headgear off the <c>M_head_cap1</c> bone, creating the
    /// BoneAttachment3D on first use. Doing it here rather than in Player.tscn
    /// keeps the base scene free of an empty node that only one character needs,
    /// and means adding headgear to a future character is a profile change only.
    /// </summary>
    private void ApplyHeadGear(PlayerProfile profile)
    {
        var skeleton = GetNodeOrNull<Skeleton3D>("Armature/Skeleton3D");
        if (skeleton == null)
            return;

        var attach = skeleton.GetNodeOrNull<BoneAttachment3D>(HEAD_GEAR_NODE);

        if (profile.HeadGear == null)
        {
            // Nothing to wear - clear anything a previous profile left behind.
            if (attach != null)
            {
                skeleton.RemoveChild(attach);
                attach.QueueFree();
            }
            return;
        }

        if (skeleton.FindBone("M_head_cap1") < 0)
        {
            GD.PushWarning("ApplyHeadGear: skeleton has no 'M_head_cap1' bone.");
            return;
        }

        if (attach == null)
        {
            attach = new BoneAttachment3D { Name = HEAD_GEAR_NODE };
            skeleton.AddChild(attach);
            attach.BoneName = "M_head_cap1";
        }

        foreach (var child in attach.GetChildren())
        {
            attach.RemoveChild(child);
            child.QueueFree();
        }

        if (profile.HeadGear.Instantiate() is not Node3D gear)
        {
            GD.PushWarning("ApplyHeadGear: HeadGear scene is not a Node3D.");
            return;
        }

        attach.AddChild(gear);
        gear.Scale = Vector3.One * Mathf.Max(0.001f, profile.HeadGearScale);
    }

    /// <summary>
    /// Swap one hand attachment for a character-specific model. Frees the old
    /// child and instances the replacement under the same parent with the same
    /// name, transform and visibility - so hardcoded node paths elsewhere keep
    /// resolving. No-op when <paramref name="replacement"/> is null.
    ///
    /// parentPath is the hand SLOT (the container the swap code toggles) and
    /// childName is "Model" - replacing the child keeps the slot's own
    /// visibility, which ShowOpenHands/ShowClosedHands drive.
    ///
    /// NOTE the scene spells the open variants "Slighty", not "Slightly".
    /// </summary>
    private void ReplaceHand(string parentPath, string childName, PackedScene replacement)
    {
        if (replacement == null)
            return;

        var parent = GetNodeOrNull<Node3D>(parentPath);
        if (parent == null)
        {
            GD.PushWarning($"ReplaceHand: missing '{parentPath}'.");
            return;
        }

        var old = parent.GetNodeOrNull<Node3D>(childName);
        var xform = old?.Transform ?? Transform3D.Identity;
        bool wasVisible = old?.Visible ?? true;

        if (old != null)
        {
            parent.RemoveChild(old);
            old.QueueFree();
        }

        if (replacement.Instantiate() is not Node3D fresh)
        {
            GD.PushWarning($"ReplaceHand: '{childName}' replacement is not a Node3D.");
            return;
        }

        fresh.Name = childName;
        parent.AddChild(fresh);
        fresh.Transform = xform;
        fresh.Visible = wasVisible;
    }

    /// <summary>
    /// Depth-first search for the first MeshInstance3D in an instantiated model,
    /// used to lift a character body out of PlayerProfile.BodyScene. Imported
    /// glTF nests it under Armature/Skeleton3D, but the depth is not guaranteed
    /// across exporters, so this does not hard-code a path.
    /// </summary>
    private static MeshInstance3D FindFirstMeshInstance(Node node)
    {
        if (node is MeshInstance3D mi)
            return mi;

        foreach (var child in node.GetChildren())
        {
            var found = FindFirstMeshInstance(child);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// The Armature scale as authored in the scene (~0.0134334, which cancels the
    /// 74.4414 bind-pose convention). Captured on the first ApplyProfile call so
    /// PlayerProfile.BodyScale can multiply it instead of overwriting it.
    /// </summary>
    private Vector3 _armatureBaseScale = Vector3.Zero;

    /// <summary>Name of the runtime-created headgear BoneAttachment3D.</summary>
    private const string HEAD_GEAR_NODE = "HeadCapBone";

    public void ApplyProfile(PlayerProfile profile)
    {
        if (profile == null)
            return;

        var mesh = GetNodeOrNull<MeshInstance3D>("Armature/Skeleton3D/Mesh_0");
        if (mesh == null)
        {
            GD.PushWarning("ApplyProfile: Could not find Armature/Skeleton3D/Mesh_0");
            return;
        }

        // --- body swap: mesh and skin travel as a matched pair ---
        // Every character rides the SAME Skeleton3D and the same ma_*.res
        // animations (they bind by bone name), so swapping a character is just
        // swapping the geometry and its bind poses.
        Mesh newMesh = profile.BodyMesh;
        Skin newSkin = profile.BodySkin;

        // BodyScene is the ergonomic path: lift the mesh and its own Skin out of
        // the imported model together so the two can never be mismatched.
        // Explicit BodyMesh/BodySkin win if the user set them.
        if (newMesh == null && profile.BodyScene != null)
        {
            var probe = profile.BodyScene.Instantiate();
            var src = FindFirstMeshInstance(probe);

            if (src == null)
            {
                GD.PushWarning(
                    $"ApplyProfile: '{profile.ProfileName}' BodyScene contains no "
                        + "MeshInstance3D - keeping the current body."
                );
            }
            else
            {
                newMesh = src.Mesh;
                newSkin = src.Skin;
            }

            probe.QueueFree();
        }

        if (newMesh != null)
        {
            if (newSkin == null)
            {
                GD.PushWarning(
                    $"ApplyProfile: '{profile.ProfileName}' supplies a body mesh but no "
                        + "Skin. They are a matched pair - the mesh will deform "
                        + "incorrectly against the shared skeleton."
                );
            }

            // Drop overrides left by a previously applied profile BEFORE swapping.
            // Surface counts differ per character (Mario has 11, Luigi 10), so a
            // stale override would otherwise land on the wrong surface.
            for (int i = mesh.GetSurfaceOverrideMaterialCount() - 1; i >= 0; i--)
                mesh.SetSurfaceOverrideMaterial(i, null);

            mesh.Mesh = newMesh;

            if (newSkin != null)
                mesh.Skin = newSkin;
        }

        // --- hands ---
        // Swap the attachment models in place, keeping each node's NAME and
        // TRANSFORM, so every existing GetNode path and the hand-swap logic keep
        // working untouched.
        // Each slot is a Node3D container whose NAME the hand-swap code toggles;
        // the model sits inside it as "Model". Swapping the child leaves the
        // container (and its visibility state) alone.
        const string LH = "Armature/Skeleton3D/LeftHandBone";
        const string RH = "Armature/Skeleton3D/RightHandBone";
        ReplaceHand($"{LH}/LeftHandClosed",       "Model", profile.LeftHandClosed);
        ReplaceHand($"{LH}/LeftHandSlightyOpen",  "Model", profile.LeftHandSlightlyOpen);
        ReplaceHand($"{RH}/RightHandClosed",      "Model", profile.RightHandClosed);
        ReplaceHand($"{RH}/RightHandSlightyOpen", "Model", profile.RightHandSlightlyOpen);
        ReplaceHand($"{RH}/RightHandWithHat",     "Model", profile.RightHandWithHat);

        ApplyHeadGear(profile);

        // --- per-character scale ---
        // Applied to the Armature, not the mesh node: a skinned MeshInstance3D
        // follows its skeleton, so scaling that node is unreliable. Scaling the
        // Armature takes the skeleton and everything bone-attached (hands, cap)
        // with it, and deliberately leaves CollisionShape3D alone so the tuned
        // physics stays shared across characters.
        var armature = GetNodeOrNull<Node3D>("Armature");
        if (armature != null)
        {
            if (_armatureBaseScale == Vector3.Zero)
                _armatureBaseScale = armature.Scale;

            armature.Scale = _armatureBaseScale * Mathf.Max(0.001f, profile.BodyScale);
        }

        int surfaceCount = mesh.Mesh.GetSurfaceCount();
        for (int i = 0; i < surfaceCount; i++)
        {
            var origMat = mesh.Mesh.SurfaceGetMaterial(i) as StandardMaterial3D;
            if (origMat?.AlbedoTexture == null)
                continue;

            // Pick replacement based on which atlas this surface uses
            string path = origMat.AlbedoTexture.ResourcePath;
            Texture2D replacement = null;

            // Which atlas is "body" and which is "eyes" differs per character
            // (Mario's eyes are on ma_mdl1_1, Luigi's on ma_mdl1_2), so the
            // profile carries its own match strings instead of hardcoding them.
            if (
                profile.BodyTexture != null
                && !string.IsNullOrEmpty(profile.BodyTextureMatch)
                && path.Contains(profile.BodyTextureMatch)
            )
                replacement = profile.BodyTexture;
            else if (
                profile.EyesTexture != null
                && !string.IsNullOrEmpty(profile.EyesTextureMatch)
                && path.Contains(profile.EyesTextureMatch)
            )
                replacement = profile.EyesTexture;

            if (replacement == null)
                continue;

            // Duplicate so we don't modify the shared mesh resource
            var mat = (StandardMaterial3D)origMat.Duplicate();
            mat.AlbedoTexture = replacement;
            mesh.SetSurfaceOverrideMaterial(i, mat);
        }
    }

    private void UpdateCarryRunBlend(float dt)
    {
        // NOTE: AnimationTree activation is now handled by SetMarioState()
        // This function only updates the blend parameter for smooth transitions

        bool isCarrying = (heldBody != null);

        // Smooth blend transition
        float targetBlend = isCarrying ? 1.0f : 0.0f;
        carryBlend = Mathf.MoveToward(carryBlend, targetBlend, dt * CARRY_BLEND_SPEED);

        // Update the carry blend parameter if the tree is active
        if (animTree != null && animTree.Active)
        {
            animTree.Set(CARRY_BLEND_PATH, carryBlend);
        }
    }

    private void ForceLoop(string name)
    {
        var a = animPlayer.GetAnimation(name);
        if (a != null)
            a.LoopMode = Animation.LoopModeEnum.Linear;
    }

    private float GetRunAnimSpeedScale()
    {
        float speedXZ = new Vector2(velocity.X, velocity.Z).Length();

        // Decide what "full speed" means for your authored cycle.
        // If ma_run1 is your normal run and ma_run2 is sprint, you can tune these.
        float full = RUN_SPEED; // or use (stateOfMario==sprinting ? RUN_SPEED : RUN_SPEED*0.7f)

        // Ceiling has to leave room for the rollout slope boost, or the anim
        // saturates and a boosted run cycles at exactly the same rate as a
        // normal one — the speed is there but reads as nothing happening.
        // Derived from the boost so raising RolloutSlopeMaxBoost widens this
        // automatically, and floored at the original 1.25 so nothing regresses.
        float maxRatio = Mathf.Max(
            1.25f,
            1f + Mathf.Max(0f, RolloutSlopeMaxBoost) / Mathf.Max(RUN_SPEED, 0.001f)
        );

        float t = Mathf.Clamp(speedXZ / Mathf.Max(full, 0.001f), 0f, maxRatio);

        // Map speed -> playback speed (widen RunAnimSpeedMax for a faster full-tilt cycle).
        return Mathf.Lerp(RunAnimSpeedMin, RunAnimSpeedMax, t);
    }

    private void UpdateRunAnimationSpeed()
    {
        // Sneak gets its own speed scaling based on stick strength
        if (stateOfMario == MarioState.sneak)
        {
            float sneakT = Mathf.Clamp(
                (walkingStrength - DEADZONE) / (SNEAK_MAX_INPUT - DEADZONE),
                0f,
                1f
            );
            float sneakSpeed = Mathf.Lerp(SNEAK_MIN_ANIM_SPEED, SNEAK_MAX_ANIM_SPEED, sneakT);

            if (animTree != null && animTree.Active)
                animTree.Set(RUN_TIMESCALE_PATH, sneakSpeed);
            else
                animPlayer.SpeedScale = sneakSpeed;
            return;
        }

        // Only apply speed scaling to running/walking states
        bool isRunningState = (
            stateOfMario == MarioState.running
            || stateOfMario == MarioState.walking
            || stateOfMario == MarioState.sprinting
        );

        if (!isRunningState)
        {
            if (animTree != null && animTree.Active)
                animTree.Set(RUN_TIMESCALE_PATH, 1.0f);
            else
                animPlayer.SpeedScale = 1.0f;
            return;
        }

        float s = GetRunAnimSpeedScale();
        // If the carry AnimationTree is active, drive the TimeScale node.
        if (animTree != null && animTree.Active)
        {
            animTree.Set(RUN_TIMESCALE_PATH, s * 1.5f);
            return;
        }
        else
        {
            animPlayer.SpeedScale = s;
        }
    }

    public void StompBounce()
    {
        _stompChain++;
        if (_stompChain > 3)
            _stompChain = 3;

        stateOfMario = MarioState.stomping;
        isJumping = true;
        isSpining(false);

        // Third stomp is ~2x height
        float bounceVel = _stompChain >= 3 ? STOMP_BOUNCE_VELOCITY_TRIPLE : STOMP_BOUNCE_VELOCITY;
        velocity.Y = bounceVel;

        // Play the step animation
        string stepAnim = _stompChain switch
        {
            1 => "ma_step1",
            2 => "ma_step2",
            _ => "ma_step3",
        };

        if (animTree != null)
        {
            animPlayer.Stop(false);
            animTree.Active = true;
        }
        if (_sm != null)
            _sm.Travel(stepAnim);
    }

    public void TakeDamage(Vector3 hitSourcePosition)
    {
        // Don't interrupt if already hurt or dead
        if (stateOfMario == MarioState.hurt || stateOfMario == MarioState.dead)
            return;

        // Reduce health and update life meter
        bool wasFullHealth = _health == StartingHealth;
        _health = Mathf.Max(_health - 1, 0);
        GD.Print(
            $"[TakeDamage] Health: {_health}/{StartingHealth} | LifeMeter found: {_lifeMeter != null}"
        );
        _lifeMeter?.SetHealth(_health);

        // First hit from full health: spawn meter near Mario then fly to HUD corner
        if (wasFullHealth && _lifeMeter != null && camera != null)
        {
            Vector2 screenPos = camera.UnprojectPosition(
                GlobalPosition + new Vector3(-3.1f, 2.5f, 0)
            );
            _lifeMeter.SpawnAtScreenPosition(screenPos);
        }

        // Die if health is 0
        if (_health <= 0)
        {
            stateOfMario = MarioState.dead;
            velocity = Vector3.Zero;
            SleepStatus(true); // Close eyes
            if (animTree != null)
            {
                animPlayer.Stop(false);
                animTree.Active = true;
            }
            if (_sm != null)
                _sm.Travel("ma_die");
            return;
        }

        // Determine front or back hit using dot product with Mario's facing direction
        Vector3 toHitSource = hitSourcePosition - GlobalPosition;
        toHitSource.Y = 0;
        toHitSource = toHitSource.Normalized();

        // Mario's forward is -Z in local space, rotated by armature yaw
        float yaw = armature.Rotation.Y;
        Vector3 forward = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));

        _hitFromFront = forward.Dot(toHitSource) > 0f;

        // Apply knockback away from hit source
        Vector3 knockbackDir = -toHitSource;
        velocity = knockbackDir * HURT_KNOCKBACK;

        _hurtAirborne = !IsOnFloor();
        stateOfMario = MarioState.hurt;

        // Play directional hurt animation
        if (animTree != null)
        {
            animPlayer.Stop(false);
            animTree.Active = true;
        }

        if (_hurtAirborne)
        {
            // Airborne: play air hurt anim, no timer yet (waits for landing)
            velocity.Y = 0f;
            if (_sm != null)
                _sm.Travel(_hitFromFront ? "ma_bkdwn" : "ma_shfdn");
        }
        else
        {
            // Grounded: play ground hurt anim with timer
            _hurtTimer = HURT_DURATION;
            velocity.Y = 0f;
            if (_sm != null)
                _sm.Travel(_hitFromFront ? "ma_sfbdn" : "ma_sffdn");
        }
    }

    public void GainExtraLife()
    {
        _lives++;
    }

    public void AddCoin()
    {
        _coins++;
        if (_coins >= 50)
        {
            _coins = 0;
            GainExtraLife();
        }
        _yellowCoinHud?.TickUp(_coins);
    }

    public void HealOneHP()
    {
        if (_health >= StartingHealth)
            return;
        _health = Mathf.Min(_health + 1, StartingHealth);
        _lifeMeter?.SetHealth(_health);
        if (_health == StartingHealth)
            _lifeMeter?.HideForFullHealth();
    }

    public int AddRedCoin()
    {
        _redCoins++;
        if (_redCoins == 1)
            _redCoinHud?.ShowHud();
        _redCoinHud?.TickUp(_redCoins);
        GD.Print($"[RedCoin] {_redCoins} collected");
        return _redCoins;
    }

    public void AddBlueCoin()
    {
        _blueCoins++;
        _blueCoinHud?.TickUp(_blueCoins);

        _blueCoinDisplayTimer = BlueCoinDisplayDuration;
        if (!_lifeCounterShowing)
        {
            _lifeCounterShowing = true;
            _shineHud?.ShowHud();
            _blueCoinHud?.ShowHud();
            _yellowCoinHud?.NudgeDown();
        }
    }

    /// <summary>Brings up the collectibles HUD (shine + blue coins + coins, like
    /// the idle display but without lives) and keeps it up. Idempotent — only
    /// slides in if it isn't already showing.</summary>
    private void ShowCollectibleHud()
    {
        _blueCoinDisplayTimer = BlueCoinDisplayDuration;
        if (!_lifeCounterShowing)
        {
            _lifeCounterShowing = true;
            _shineHud?.ShowHud();
            _blueCoinHud?.ShowHud();
            _yellowCoinHud?.NudgeDown();
        }
    }

    /// <summary>Waits ShineCountDelay (so the count ticks up mid-cutscene), then
    /// increments. A tween on Mario so it's auto-cancelled if he's freed.</summary>
    private void ScheduleShineCountUp()
    {
        var t = CreateTween();
        t.TweenInterval(ShineCountDelay);
        t.TweenCallback(Callable.From(AddShine));
    }

    public void AddShine()
    {
        _shines++;
        _shineHud?.TickUp(_shines); // sets the count + flare-stars on the changed digit(s)
        ShowCollectibleHud();
    }

    private void Respawn()
    {
        _health = StartingHealth;
        _lifeMeter?.SetHealth(_health);
        _lifeMeter?.HideForFullHealth();
        velocity = Vector3.Zero;
        SleepStatus(false); // Open eyes
        stateOfMario = MarioState.idle;
        SetMarioState(MarioState.idle);
    }

    private void SetMarioState(MarioState state)
    {
        // Safety check - if AnimationTree isn't set up, fall back to AnimationPlayer
        if (_sm == null || animTree == null)
        {
            // Fallback to AnimationPlayer for basic animations
            string fallbackAnim = state switch
            {
                MarioState.idle => "ma_wait",
                MarioState.talking => "ma_t_wait",
                MarioState.sneak => "ma_sstep",
                MarioState.running => "ma_run1",
                MarioState.walking => "ma_run1",
                MarioState.sprinting => "ma_run2",
                MarioState.pivot => "ma_pivot",
                _ => "ma_wait",
            };
            if (animPlayer.CurrentAnimation != fallbackAnim)
                animPlayer.Play(fallbackAnim);
            return;
        }

        // Diving would throw the object (not implemented yet), so no carry blend
        bool isDiving = (
            state == MarioState.diving
            || state == MarioState.singleJumpDive
            || state == MarioState.doubleJumpDive
            || state == MarioState.tripleJumpDive
        );

        // Always activate AnimationTree when SetMarioState is called.
        // Sleep animations bypass SetMarioState entirely (they use animPlayer directly),
        // so this is safe — it also ensures we wake up correctly when movement starts.
        if (animTree != null)
        {
            animPlayer.Stop(false); // stop animPlayer so it doesn't fight animTree
            animTree.Active = true;
        }

        // Apply carry blend to ALL animations when holding an object (except diving)
        if (animTree != null && animTree.Active)
        {
            if (heldBody != null && !isDiving)
            {
                animTree.Set(CARRY_BLEND_PATH, 1.0f); // Full carry blend
            }
            else
            {
                animTree.Set(CARRY_BLEND_PATH, 0.0f); // No carry blend
            }
        }

        // Travel to the appropriate animation in the state machine
        switch (state)
        {
            case MarioState.idle:
                _sm.Travel("ma_wait");
                break;
            case MarioState.talking:
                // ma_t_wait is present in the AnimationPlayer library but not as
                // an AnimationTree state, so play its forced loop directly.
                if (animTree != null)
                    animTree.Active = false;
                if (animPlayer.CurrentAnimation != "ma_t_wait")
                    animPlayer.Play("ma_t_wait");
                break;
            case MarioState.pivot:
                _sm.Travel("ma_pivot");
                break;
            case MarioState.sneak:
                _sm.Travel("ma_sstep");
                break;
            case MarioState.walking:
                _sm.Travel("ma_run1");
                break;
            case MarioState.running:
                _sm.Travel("ma_run1");
                break;
            case MarioState.sprinting:
                _sm.Travel("ma_run2");
                break;
            case MarioState.singleJump:
                // Just left a slip for airborne (slid off the edge, or the
                // escape hop) — ma_slpla plays for that beat instead of the
                // normal jump pose.
                _sm.Start(slipAirLaunchTimer > 0f ? "ma_slpla" : "ma_jump");
                break;
            case MarioState.doubleJump:
                // Double jump: ma_2jmp1 while rising, ma_2jmp2 while falling
                if (Velocity.Y > 0)
                    _sm.Travel("ma_2jmp1");
                else
                    _sm.Travel("ma_2jmp2");
                break;
            case MarioState.tripleJump:
                _sm.Travel("ma_demo_gate_out_rolling_get");
                break;
            case MarioState.singleJumpLanding:
            case MarioState.doubleJumpLanding:
            case MarioState.landing:
                _sm.Travel("ma_laend");
                break;
            case MarioState.tripleJumpLanding:
                _sm.Travel("ma_tjmp2");
                break;
            case MarioState.diving:
            case MarioState.singleJumpDive:
                _sm.Travel("ma_sldct");
                break;
            case MarioState.doubleJumpDive:
            case MarioState.tripleJumpDive:
                _sm.Travel("ma_sldct");
                break;
            case MarioState.singleRollout:
                _sm.Travel("ma_roll_jump");
                break;
            case MarioState.bellySlidingFromDive:
                _sm.Travel("ma_slpbk");
                break;
            case MarioState.bellyRollout:
                _sm.Travel("ma_roll");
                break;
            case MarioState.gettingUpFromSliding:
                _sm.Travel("ma_lost");
                break;
            case MarioState.sideFlip:
                _sm.Travel("ma_tjmp1");
                break;
            case MarioState.sideFlipTurning:
                _sm.Travel("ma_trned");
                break;
            case MarioState.SpinJump:
            case MarioState.groundSpin:
                _sm.Travel("ma_spin_p");
                break;
            case MarioState.crouch:
                _sm.Travel("ma_sqwat");
                break;
            case MarioState.bonk:
                _sm.Travel("ma_bkdwn");
                break;
            case MarioState.backFlip:
                _sm.Travel("ma_jump_rolling");
                break;
            case MarioState.rolloutRun:
                _sm.Travel("ma_run2");
                break;
            case MarioState.wallSlide:
                _sm.Travel("ma_wsld");
                break;

            case MarioState.slipping:
                // Start, not Travel — ma_slpla/ma_slpbk/ma_slip are only
                // graph-connected in the ground-pound/belly-rollout
                // recovery neighborhood (they're reused for that too).
                // Travel() pathfinds through the graph to reach them, which
                // from a jump/wall-slide state routes through whatever
                // unrelated recovery animation is on that path — the "weird
                // animation" flash. Start() jumps straight there.
                _sm.Start(_slipFacingUphill ? "ma_slpbk" : "ma_slip");
                break;
            case MarioState.wallJump:
                _sm.Travel("ma_wjmp");
                break;
            case MarioState.ledgeFall:
                _sm.Travel("ma_land");
                break;
            case MarioState.ledgeHang:
                _sm.Travel("ma_hang");
                break;
            case MarioState.ledgeClimb:
                _sm.Travel("ma_hgup");
                break;
            case MarioState.ledgeHopUp:
                _sm.Travel("ma_hgjmp");
                break;
            case MarioState.pickupFail:
                _sm.Travel("ma_get_fail");
                break;
            case MarioState.pickupRaise:
                // Keep AnimationPlayer for pickup animations
                if (animTree != null)
                    animTree.Active = false;
                animPlayer.Play("ma_raise");
                break;
            case MarioState.carrying:
                // Carrying uses AnimationTree with carry blend
                if (animTree != null)
                {
                    animTree.Active = true;
                    animTree.Set(CARRY_BLEND_PATH, 1.0f);
                }
                _sm.Travel("ma_wait");
                break;
            case MarioState.putDown:
                // Keep AnimationPlayer for putdown animations
                if (animTree != null)
                    animTree.Active = false;
                animPlayer.Play("ma_put");
                break;
            case MarioState.shineGet:
                // Keep AnimationPlayer for the shine-get cutscene animation
                if (animTree != null)
                    animTree.Active = false;
                animPlayer.Play("ma_demo_shine_get");
                break;
            case MarioState.wallPush:
                _sm.Travel("ma_push");
                break;
            case MarioState.wallShuffle:
                // Wall shuffle direction is handled dynamically in TickGroundWall
                // Don't override it here - keep using AnimationPlayer
                if (animTree != null)
                    animTree.Active = false;
                break;
            case MarioState.groundPoundStartup:
                if (animTree != null)
                    animTree.Active = false;
                animPlayer.Play("ma_hipsr");
                break;
            case MarioState.groundPoundFalling:
                _sm.Travel("ma_hipat");
                break;
            case MarioState.groundPoundLanding:
                _sm.Travel("ma_hiped");
                break;
            case MarioState.treeGrab:
                if (animTree != null)
                    animTree.Active = false;
                if (animPlayer.CurrentAnimation != "ma_tree_catch")
                    animPlayer.Play("ma_tree_catch");
                break;
            case MarioState.treeWait:
                if (animTree != null)
                    animTree.Active = false;
                if (animPlayer.CurrentAnimation != "ma_tree_wait")
                    animPlayer.Play("ma_tree_wait");
                break;
            case MarioState.treeClimb:
                if (animTree != null)
                    animTree.Active = false;
                if (animPlayer.CurrentAnimation != "ma_tree_climb")
                    animPlayer.Play("ma_tree_climb");
                break;
            case MarioState.treeMoveL:
                if (animTree != null)
                    animTree.Active = false;
                if (animPlayer.CurrentAnimation != "ma_tree_move_l")
                    animPlayer.Play("ma_tree_move_l");
                break;
            case MarioState.treeMoveR:
                if (animTree != null)
                    animTree.Active = false;
                if (animPlayer.CurrentAnimation != "ma_tree_move_r")
                    animPlayer.Play("ma_tree_move_r");
                break;
            case MarioState.treeTopReach:
                if (animTree != null)
                    animTree.Active = false;
                if (animPlayer.CurrentAnimation != "ma_tjmp2")
                    animPlayer.Play("ma_tjmp2");
                break;
        }
    }
}
