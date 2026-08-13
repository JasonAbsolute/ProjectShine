using Godot;

/// <summary>
/// SMS-style follow camera. Polar coordinates around Mario (yaw, pitch, radius)
/// chased toward per-mode targets each frame. Mirrors the architecture of
/// CPolarSubCamera in the SMS decomp: mode-based dispatch, separate chase rates
/// per axis, height pan as its own subsystem, L-button as a sustained mode.
///
/// Scene hierarchy expected:
///   SunshineCamera (Node3D, TopLevel = true)  ← this script
///     SpringArmPivot (Node3D)                  ← rotated for yaw
///       SpringArm3D                            ← handles wall collision
///         Camera3D                             ← the actual viewport camera
///
/// Mario is referenced by NodePath. The Armature child is used to read his
/// facing direction for the auto-behind logic.
/// </summary>
public partial class SunshineCamera : Node3D
{
    // ============================================================
    //                       Modes
    // ============================================================
    public enum CamMode
    {
        Normal,        // default behind-Mario follower; player has control
        LButton,       // holding L: strong lock behind Mario, no manual orbit
        OverShoulder,  // Y button: Mario frozen, tight OTS view, free 360° look
        Free,          // no auto-behind ever; player owns the camera fully (e.g. cutscene staging)
        ShineGet,      // shine sprite collect cutscene: fixed low-angle hero shot, no player input
    }

    [Export] public CamMode Mode { get; set; } = CamMode.Normal;

    // ============================================================
    //                       Wiring
    // ============================================================
    [Export] public NodePath TargetPath = new("..");                    // Mario
    [Export] public NodePath ArmaturePath = new("../Armature");         // his facing
    [Export] public NodePath PivotPath = new("SpringArmPivot");
    [Export] public NodePath SpringArmPath = new("SpringArmPivot/SpringArm3D");
    [Export] public NodePath CameraPath = new("SpringArmPivot/SpringArm3D/Camera3D");

    // ============================================================
    //                       Input actions
    // ============================================================
    [Export] public string RStickLeft  = "Rstick_left";
    [Export] public string RStickRight = "Rstick_right";
    [Export] public string RStickUp    = "Rstick_up";
    [Export] public string RStickDown  = "Rstick_down";
    [Export] public string LStickLeft  = "Lstick_left";
    [Export] public string LStickRight = "Lstick_right";
    [Export] public string LStickUp    = "Lstick_up";
    [Export] public string LStickDown  = "Lstick_down";
    [Export] public string LButtonAction = "button_l";   // L (sustained press) → LButton mode
    [Export] public string CenterAction  = "button_l";   // L tap → also re-centers (SMS dual-purpose)
    [Export] public string OverShoulderAction = "button_y"; // Y (sustained press) → over-the-shoulder mode

    // ============================================================
    //                       Follow / Offset
    // ============================================================
    [Export] public Vector3 TargetOffset = new(0f, 1.8f, 0f);   // look-at point above feet (Mario's chest)
    [Export] public float FollowSmooth = 8f;                     // how fast we lerp toward Mario's world position

    // ============================================================
    //                       Polar limits
    // ============================================================
    [Export] public float MinRadius = 3.5f;
    [Export] public float MaxRadius = 11.0f;
    [Export] public float BaseRadius = 6.0f;

    [Export] public float MinPitchDeg = -55f;     // looking down at Mario (camera high)
    /// <summary>Positive = camera below pivot. SMS-style max zoom-in puts the camera at Mario's chest, not below — keep this ~10° to prevent under-Mario framing.</summary>
    [Export] public float MaxPitchDeg =  10f;
    [Export] public float DefaultPitchDeg = -18f; // SMS resting angle

    // OverShoulder-only: wider pitch range and tighter radius
    [Export] public float OverShoulderRadius = 1.4f;
    [Export] public float OverShoulderMinPitchDeg = -75f;
    [Export] public float OverShoulderMaxPitchDeg =  70f;
    /// <summary>Starting pitch when entering OS. Slightly positive = camera at shoulder height looking slightly up. Negative would tilt it down like Normal mode.</summary>
    [Export] public float OverShoulderDefaultPitchDeg = 5f;
    /// <summary>Offset relative to Mario's VISUAL local frame: +X = his right, +Y = up, +Z = behind. This keeps the framing consistent as he rotates.</summary>
    [Export] public Vector3 OverShoulderLocalOffset = new(-0.55f, 1.75f, 0f); // negative X = over LEFT shoulder, Y at shoulder height

    /// <summary>L-stick X rotation speed for turning Mario in place during OS mode (rad/sec at full deflection).</summary>
    [Export] public float OverShoulderTurnSpeed = 2.4f;
    /// <summary>L-stick Y pitch speed for OS look up/down.</summary>
    [Export] public float OverShoulderPitchSpeed = 2.0f;

    // ============================================================
    //                       ShineGet rail (Path3D)
    // ============================================================
    // Choreography mapped frame-by-frame from a full video capture of the
    // real cutscene (GMSE04_2026-07-21_21-11-35_0.avi, ~10s @60fps):
    //   1. On grab the camera CUTS to a low shot in front of Mario, roughly
    //      chest height, close — he reaches out and takes the shine.
    //   2. It then CRANES UP continuously for the rest of the celebration:
    //      rising in front of him as he swings the shine and lifts it
    //      overhead, ending high above, looking down steeply (~40°) with
    //      Mario small in frame, his shadow below him.
    //   3. Big "SHINE!" letters slide in from the lower-left ~1.2s after the
    //      grab and fade before the sequence ends (handled by ShineGetPopup).
    // Built on Path3D + PathFollow3D: the camera rides a PathFollow3D along
    // a Curve3D re-authored each grab in Mario-local space, so the same arc
    // plays out relative to wherever he's standing/facing. The camera is
    // physically detached from the SpringArm chain for the duration (see
    // EnterShineGetShot) so nothing fights the rail.
    //
    // Frame-by-frame of the video shows the shot STARTS BEHIND Mario:
    // camera low behind his shoulder, aimed UP over his head at the sky
    // (Mario is just a corner element in frame), then it swings around his
    // side to the front — lowering its aim from the sky down to his chest —
    // landing in the front-low grab shot, and only THEN cranes up.
    /// <summary>How long the opening behind-the-shoulder shot holds before the swing to front.</summary>
    [Export] public float ShineGetBehindHoldDuration = 1.2f;
    /// <summary>How long the swing from behind Mario around to his front takes.</summary>
    [Export] public float ShineGetSwingDuration = 1.2f;
    /// <summary>How long the camera holds at the front-low point (the grab beat) before the crane-up.</summary>
    [Export] public float ShineGetFrontHoldDuration = 0.5f;
    /// <summary>How long the crane-up from the low front shot to the elevated hero shot takes.</summary>
    [Export] public float ShineGetRiseDuration = 2.0f;
    // Rail points in Mario-local space (+X his right, -Z in FRONT of him —
    // see EnterShineGetShot for why -Z), scaled by _rigScale at build time.
    // Behind → side → front is the swing; front → mid → end is the crane.
    // The front/crane points deliberately STAY ~30° off his facing axis on
    // his left — the reference never squares up dead-front: the grab and the
    // whole celebration are shot at an angle (Mario three-quarter view,
    // slightly right of frame center), continuing the direction the swing
    // came around. He reaches ACROSS with his right hand toward the
    // camera-side shine, which is why the grab reads so clearly in f0270.
    // Heights sit at head level for the low beats (user feedback: the low
    // shots were reading too low/floor-hugging), rising to the overhead end.
    // Retuned to the video hero pose (f0348/f0420): the camera holds an
    // ELEVATED FRONT shot looking DOWN ~20° at Mario, moderate distance —
    // Mario centered ~a third of frame height, floating above the platform
    // with his shadow, shine up-left. NOT a steep overhead crane.
    [Export] public Vector3 ShineGetCamBehind = new(-0.35f, 1.05f, 1.9f);
    [Export] public Vector3 ShineGetCamSide   = new(-1.4f, 1.05f, -0.6f);
    [Export] public Vector3 ShineGetCamStart  = new(-0.58f, 1.6f, -1.4f);
    [Export] public Vector3 ShineGetCamMid    = new(-0.5f, 2.15f, -1.5f);
    [Export] public Vector3 ShineGetCamEnd    = new(-0.45f, 2.7f, -1.6f);
    /// <summary>Aim point once the camera is in front (and for the whole crane): Mario's chest/upper body, biased slightly to his left so he sits right-of-center in frame (the SHINE! banner owns the lower-left) like the reference.</summary>
    [Export] public Vector3 ShineGetLookAtOffset = new(-0.15f, 1.3f, 0f);
    /// <summary>Aim point for the opening behind shot: high, forward, and off to his left so Mario sits in the bottom-RIGHT corner of frame (matches the video) while the camera looks up over his head.</summary>
    [Export] public Vector3 ShineGetLookAtHighOffset = new(-0.7f, 2.5f, -2.0f);
    [Export] public float ShineGetEntryDuration = 0.4f;
    /// <summary>Dutch-tilt roll (degrees) held during the hero pose — the SMS
    /// shot slopes the horizon up to the right. Ramps in over the crane.</summary>
    [Export] public float ShineGetRollDeg = 9f;

    private Path3D _shineRail;
    private PathFollow3D _shineRailFollow;
    private Vector3 _railLookLow, _railLookHigh;
    private float _railFrontRatio;
    private float _shineShotTime;
    private Node3D _railShineTrack; // the descending shine — aimed at during the behind/swing beats

    /// <summary>Mario's visual facing yaw, exposed so gameplay code (the shine's
    /// scripted descent) can share the rail's exact facing convention.</summary>
    public float CurrentVisualYaw => GetVisualYaw();

    /// <summary>How far (radians, signed) the settled front shot sits off Mario's
    /// ORIGINAL facing — i.e. how much he must turn during the grab beat to end
    /// up looking straight into the camera for the pose (the reference has him
    /// facing the lens for the whole celebration). Derived from the rail's
    /// front point so tuning ShineGetCamStart keeps the turn in sync.</summary>
    public float ShineGetFrontYawOffset => Mathf.Atan2(-ShineGetCamStart.X, -ShineGetCamStart.Z);

    // Mario body / waist bend while looking down at him (SMS: when the camera goes overhead, Mario tilts his upper body back to look up at it)
    [Export] public bool   BodyTrackingEnabled = true;
    [Export] public string BodyBoneName = "chn_chest";     // matches this project's Mario skeleton
    [Export] public float  BodyBendMaxDeg = 35f;           // how far back he tilts at max look-down
    /// <summary>Pitch (in degrees, negative = looking down) at which the waist bend STARTS. Below this, Mario bends progressively.</summary>
    [Export] public float  BodyBendStartPitchDeg = -25f;
    /// <summary>Pitch at which the bend reaches its maximum. Should be more negative than BodyBendStartPitchDeg.</summary>
    [Export] public float  BodyBendFullPitchDeg = -65f;
    [Export] public float  BodyTrackResponse = 10f;

    // ============================================================
    //                       Chase rates (lerp ratios per second)
    // ============================================================
    // current = lerp(current, target, 1 - exp(-rate * dt))
    [Export] public float YawChaseRate     = 6f;
    [Export] public float PitchChaseRate   = 5f;
    [Export] public float RadiusChaseRate  = 6f;
    [Export] public float YawSnapRate      = 18f; // L-button mode uses a faster chase
    [Export] public float PitchSnapRate    = 14f;
    // Over-shoulder mode steady-state rates (faster than Normal so L-stick feels responsive)
    [Export] public float OverShoulderYawRate    = 8f;
    [Export] public float OverShoulderPitchRate  = 7f;
    [Export] public float OverShoulderRadiusRate = 8f;
    /// <summary>Duration of the entry "swing behind" transition. Snap rates are used during this window.</summary>
    [Export] public float OverShoulderEntryDuration = 0.35f;

    // ============================================================
    //                       Input mapping
    // ============================================================
    [Export] public float CStickYawSpeed    = 3.2f;   // rad/sec at full deflection
    [Export] public float CStickPitchSpeed  = 2.4f;   // rad/sec at full deflection (only used when LinkPitchToRadius is OFF)
    [Export] public float CStickZoomSpeed   = 4.0f;   // radius/sec at full deflection (only used when LinkPitchToRadius is OFF)
    [Export] public float CStickDeadzone    = 0.18f;
    /// <summary>SMS-style: stick UP = closer + camera lower (toward Mario's level). Default true matches SMS.</summary>
    [Export] public bool  CStickYInvertedPitch = true;
    /// <summary>SMS-style linked zoom: pitch and radius share one "zoom level" curve. Stick Y moves both together. Disable for independent pitch/radius control.</summary>
    [Export] public bool  LinkPitchToRadius = true;
    /// <summary>Speed at which the zoom level (0=closest, 1=farthest) changes with full stick Y deflection.</summary>
    [Export] public float CStickLinkedZoomSpeed = 0.6f;

    // ============================================================
    //                       Auto-behind
    // ============================================================
    [Export] public float AutoBehindDelay         = 0.55f; // seconds of no manual input before auto-behind kicks in
    [Export] public float AutoBehindMinSpeed      = 0.6f;  // target XZ speed required
    [Export] public float AutoBehindRate          = 0.8f;  // chase rate toward behind-yaw (gentle)
    [Export] public float AutoBehindMaxDegPerSec  = 25f;   // hard cap on auto-rotation speed
    [Export] public bool  AutoBehindOnlyOnFloor   = true;

    /// <summary>Suppress auto-behind when Mario is moving toward the camera. Prevents the camera from flipping around to "follow" backward motion.</summary>
    [Export] public bool  SuppressWhenApproaching = true;
    /// <summary>Cone half-angle (degrees) around the camera direction that counts as "approaching".</summary>
    [Export] public float ApproachConeDeg         = 25f;

    /// <summary>If your Mario model is authored with a 180° forward offset, set true so behind-Mario reads correctly from the armature fallback.</summary>
    [Export] public bool  ArmatureForwardIsFlipped = true;

    // ============================================================
    //                       Height pan (separate Y subsystem)
    // ============================================================
    [Export] public float HeightPanRate           = 7f;    // how fast the look-at point's Y follows Mario
    [Export] public float HeightPanLeashUp        = 1.2f;  // tolerated Y offset above before correction speeds up
    [Export] public float HeightPanLeashDown      = 2.5f;  // tolerated Y offset below

    // ============================================================
    //                       Recenter (tap L)
    // ============================================================
    [Export] public float TapMaxDuration          = 0.18f; // <= this many seconds counts as a tap
    [Export] public float RecenterRate            = 14f;   // snap-behind speed when tap fires

    // ============================================================
    //                       Auto-scale to target height
    // ============================================================
    [Export] public bool  AutoScaleToTargetHeight = true;
    [Export] public float ReferenceTargetHeight   = 1.80f;

    // ============================================================
    //                       Runtime state
    // ============================================================
    private Node3D _target;
    private Node3D _armature;
    private Node3D _pivot;
    private SpringArm3D _springArm;
    private Camera3D _camera;

    // Polar state (yaw is around Y, pitch is local X on the arm)
    private float _curYaw, _tgtYaw;
    private float _curPitch, _tgtPitch;
    private float _curRadius, _tgtRadius;

    // Look-at world position (lerped follow target). Height is panned separately.
    private Vector3 _lookAtXZ;
    private float _lookAtY;

    // Mode bookkeeping
    private float _camManualTimer = 999f;   // time since last camera (R-stick) input
    private float _moveNeutralTimer = 999f; // time since last L-stick (movement) input

    // L-button tap detection
    private float _lButtonHeldTime = -1f;   // -1 = not pressed

    private float _rigScale = 1f;

    // ============================================================
    //                       Setup
    // ============================================================
    public override void _Ready()
    {
        TopLevel = true;

        _target    = GetNodeOrNull<Node3D>(TargetPath);
        _armature  = GetNodeOrNull<Node3D>(ArmaturePath);
        _pivot     = GetNodeOrNull<Node3D>(PivotPath);
        _springArm = GetNodeOrNull<SpringArm3D>(SpringArmPath);
        _camera    = GetNodeOrNull<Camera3D>(CameraPath);

        if (_target == null)    GD.PushError("[SunshineCamera] Target not found.");
        if (_pivot == null)     GD.PushError("[SunshineCamera] Pivot not found.");
        if (_springArm == null) GD.PushError("[SunshineCamera] SpringArm not found.");

        if (_springArm != null && _target is CollisionObject3D co)
            _springArm.AddExcludedObject(co.GetRid());

        // Zero out any stale local transforms on the child rig — they'd offset the
        // computed camera position weirdly. Pivot/SpringArm/Camera all need to be at
        // local origin so the polar math is in the same frame.
        if (_pivot != null)     _pivot.Position = Vector3.Zero;
        if (_springArm != null) _springArm.Position = Vector3.Zero;
        if (_camera != null)    _camera.Position = Vector3.Zero;

        if (AutoScaleToTargetHeight) RecomputeRigScale();

        GD.Print($"[SunshineCamera] rigScale={_rigScale:F2}  base={BaseRadius * _rigScale:F2}  min={MinRadius * _rigScale:F2}  max={MaxRadius * _rigScale:F2}  targetOffsetY={TargetOffset.Y * _rigScale:F2}");

        // Initialize polar state
        _curYaw = _tgtYaw = (_pivot != null) ? _pivot.GlobalRotation.Y : 0f;
        // When linking is on, BaseRadius is the authoritative default — derive pitch from it.
        // When linking is off, use DefaultPitchDeg + BaseRadius independently.
        _zoomT = DefaultZoomT;
        if (LinkPitchToRadius)
        {
            _curPitch  = _tgtPitch  = ZoomTtoPitch(_zoomT);
            _curRadius = _tgtRadius = ZoomTtoRadius(_zoomT);
        }
        else
        {
            _curPitch  = _tgtPitch  = Mathf.DegToRad(DefaultPitchDeg);
            _curRadius = _tgtRadius = BaseRadius * _rigScale;
        }

        // Initialize look-at to Mario's current position so we don't snap in
        if (_target != null)
        {
            Vector3 mp = _target.GlobalPosition + TargetOffset * _rigScale;
            _lookAtXZ = new Vector3(mp.X, 0f, mp.Z);
            _lookAtY = mp.Y;
        }

        ApplyTransform();
        if (_springArm != null) _springArm.SpringLength = _curRadius;
    }

    // ============================================================
    //                       Main update
    // ============================================================
    // The ShineGet rail runs at RENDER rate — a hand-authored camera pan
    // stepped at 60Hz physics reads as stutter on a high-refresh display,
    // and nothing about the rail needs physics (Mario is frozen and the
    // descent/turn tweens are idle-processed, so everything advances on the
    // same clock).
    public override void _Process(double delta)
    {
        if (Mode == CamMode.ShineGet)
            TickShineGetRail((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target == null || _pivot == null || _springArm == null) return;

        float dt = (float)delta;

        // ShineGet is a rail, not a polar orbit — the camera is physically
        // detached from the SpringArm chain for this mode (see
        // EnterShineGetShot), so none of the normal follow/chase/apply
        // machinery below applies. It's ticked from _Process (render rate)
        // instead of here: the 60Hz physics step made the authored pans
        // visibly stutter on high-refresh displays ("rough"), and nothing
        // about the rail is physics-coupled — Mario is frozen for the
        // duration. Just bail out so the normal machinery stays off.
        if (Mode == CamMode.ShineGet)
            return;

        // 1) Read inputs and update timers
        Vector2 rstick = ApplyDeadzone(Input.GetVector(RStickLeft, RStickRight, RStickUp, RStickDown), CStickDeadzone);
        Vector2 lstick = ApplyDeadzone(Input.GetVector(LStickLeft, LStickRight, LStickUp, LStickDown), 0.18f);
        bool camManual = rstick != Vector2.Zero;
        bool moving = lstick != Vector2.Zero;

        if (camManual)  _camManualTimer = 0f;   else _camManualTimer  += dt;
        if (moving)     _moveNeutralTimer = 0f; else _moveNeutralTimer += dt;

        // 2) L-button: detect press/release + tap vs hold
        UpdateLButtonState(dt);

        // 3) Follow Mario (smoothed look-at)
        UpdateFollow(dt);

        // 2.5) Y button: enter/exit over-the-shoulder mode (held)
        UpdateOverShoulderState();

        // 4) Mode dispatch — sets _targetYaw/Pitch/Radius
        switch (Mode)
        {
            case CamMode.Normal:       TickNormal(dt, rstick, lstick, camManual, moving); break;
            case CamMode.LButton:      TickLButton(dt, lstick, moving);                   break;
            case CamMode.OverShoulder: TickOverShoulder(dt, rstick, camManual);           break;
            case CamMode.Free:         TickFree(dt, rstick, camManual);                   break;
            // ShineGet handled earlier via early-return — never reached here.
        }

        // 5) Chase: advance current toward target
        ChaseToTargets(dt);

        // 6) Apply
        ApplyTransform();

        // 7) Mario waist bend (only while looking down at him in OverShoulder)
        UpdateBodyTracking(dt);
    }

    // ============================================================
    //                       Mode: Normal
    // ============================================================
    private void TickNormal(float dt, Vector2 rstick, Vector2 lstick, bool camManual, bool moving)
    {
        if (camManual)
        {
            _tgtYaw -= rstick.X * CStickYawSpeed * dt;

            if (LinkPitchToRadius)
            {
                // SMS-linked zoom: ONE input controls a single zoom level (0..1) that
                // drives BOTH pitch and radius along a fixed curve. Each pitch has a
                // matching radius, no drift possible.
                // Stick UP (rstick.Y < 0) zooms IN → t decreases.
                _zoomT = Mathf.Clamp(_zoomT + rstick.Y * CStickLinkedZoomSpeed * dt, 0f, 1f);
                _tgtPitch  = ZoomTtoPitch(_zoomT);
                _tgtRadius = ZoomTtoRadius(_zoomT);
            }
            else
            {
                // Independent control (legacy / advanced).
                float pitchDelta = (CStickYInvertedPitch ? -rstick.Y : rstick.Y);
                _tgtPitch += pitchDelta * CStickPitchSpeed * dt;
                _tgtPitch = Mathf.Clamp(_tgtPitch, Mathf.DegToRad(MinPitchDeg), Mathf.DegToRad(MaxPitchDeg));

                _tgtRadius += rstick.Y * CStickZoomSpeed * dt;
                _tgtRadius = Mathf.Clamp(_tgtRadius, MinRadius * _rigScale, MaxRadius * _rigScale);
            }
        }

        // Auto-behind: only while actively moving AND no manual input recently
        if (moving
            && _camManualTimer >= AutoBehindDelay
            && GetTargetSpeedXZ() >= AutoBehindMinSpeed
            && (!AutoBehindOnlyOnFloor || IsTargetOnFloor())
            && (!SuppressWhenApproaching || !IsApproachingCamera(lstick)))
        {
            float behindYaw = GetBehindYaw();
            float a = 1f - Mathf.Exp(-AutoBehindRate * dt);
            float blended = Mathf.LerpAngle(_tgtYaw, behindYaw, a);
            float maxStep = Mathf.DegToRad(AutoBehindMaxDegPerSec) * dt;
            _tgtYaw = StepAngle(_tgtYaw, blended, maxStep);
        }
    }

    // ============================================================
    //                       Mode: LButton (sustained L hold)
    // ============================================================
    private void TickLButton(float dt, Vector2 lstick, bool moving)
    {
        // While L is held, the camera locks behind Mario aggressively.
        // No manual orbit input is read here — releasing L returns to Normal.
        float behindYaw = GetBehindYaw();
        _tgtYaw = behindYaw;

        // Pitch eases back to default and radius eases back to base.
        _tgtPitch  = Mathf.DegToRad(DefaultPitchDeg);
        _tgtRadius = BaseRadius * _rigScale;
    }

    // ============================================================
    //                       Mode: OverShoulder (Y toggled)
    // ============================================================
    /// <summary>
    /// Mario's VISUAL facing yaw (his model's facing direction in world). Accounts
    /// for the 180° offset some imported rigs have. Use this everywhere instead of
    /// reading armature.Rotation.Y directly.
    /// </summary>
    private float GetVisualYaw()
    {
        if (_armature == null) return _curYaw;
        float y = _armature.GlobalRotation.Y;
        if (ArmatureForwardIsFlipped) y += Mathf.Pi;
        return y;
    }

    private void TickOverShoulder(float dt, Vector2 rstick, bool camManual)
    {
        // OS mode reads the L-stick (movement stick), NOT the R-stick.
        Vector2 lstick = ApplyDeadzone(
            Input.GetVector(LStickLeft, LStickRight, LStickUp, LStickDown),
            0.18f);

        // L-stick X turns Mario's body in place. Direction depends on rig flip:
        // increasing armature.Y rotates the model one way for an unflipped rig and
        // the opposite for a flipped one — so the input sign flips too.
        if (_armature != null && Mathf.Abs(lstick.X) > 0.0001f)
        {
            float sign = ArmatureForwardIsFlipped ? +1f : -1f;
            float dYaw = sign * lstick.X * OverShoulderTurnSpeed * dt;
            Vector3 r = _armature.Rotation;
            r.Y += dYaw;
            _armature.Rotation = r;
        }

        // L-stick Y tilts the camera pitch (up = look up, down = look down).
        if (Mathf.Abs(lstick.Y) > 0.0001f)
        {
            float pitchDelta = -lstick.Y * OverShoulderPitchSpeed * dt;
            _tgtPitch += pitchDelta;
            _tgtPitch = Mathf.Clamp(_tgtPitch,
                Mathf.DegToRad(OverShoulderMinPitchDeg),
                Mathf.DegToRad(OverShoulderMaxPitchDeg));
        }

        // Camera is always locked behind Mario (his current visual facing).
        _tgtYaw = GetVisualYaw();
        _tgtRadius = OverShoulderRadius * _rigScale;
    }

    // ============================================================
    //                       Mode: ShineGet
    // ============================================================
    /// <summary>
    /// Rides the PathFollow3D along the rail authored in EnterShineGetShot(),
    /// in four beats matched to the reference video:
    ///   1. hold BEHIND Mario, aimed high over his head (behind beat);
    ///   2. swing around his side to the front, aim easing down to his chest;
    ///   3. hold at the front-low point (grab beat);
    ///   4. crane up to the high look-down and hold there until the cutscene
    ///      ends.
    /// _railFrontRatio marks where the front-low point sits along the curve,
    /// so the swing sweeps [0.._railFrontRatio] and the crane sweeps the rest.
    /// </summary>
    private void TickShineGetRail(float dt)
    {
        if (_camera == null || _shineRailFollow == null) return;

        _shineShotTime += dt;
        float t = _shineShotTime;

        float progress;
        float aimT; // 0 = high sky aim (behind beat), 1 = chest aim
        if (t < ShineGetBehindHoldDuration)
        {
            progress = 0f;
            aimT = 0f;
        }
        else if (t < ShineGetBehindHoldDuration + ShineGetSwingDuration)
        {
            float u = (t - ShineGetBehindHoldDuration) / ShineGetSwingDuration;
            float e = u * u * (3f - 2f * u);
            progress = _railFrontRatio * e;
            aimT = e;
        }
        else if (t < ShineGetBehindHoldDuration + ShineGetSwingDuration + ShineGetFrontHoldDuration)
        {
            progress = _railFrontRatio;
            aimT = 1f;
        }
        else
        {
            float u = Mathf.Clamp(
                (t - ShineGetBehindHoldDuration - ShineGetSwingDuration - ShineGetFrontHoldDuration) / ShineGetRiseDuration,
                0f, 1f);
            float e = u * u * (3f - 2f * u);
            progress = _railFrontRatio + (1f - _railFrontRatio) * e;
            aimT = 1f;
        }

        _shineRailFollow.ProgressRatio = progress;
        _camera.Position = Vector3.Zero; // stay glued to the follower

        // While the shine is descending (behind + swing beats), aim at IT —
        // the video's camera visibly tracks the shine down the sky. The lerp
        // toward the chest aim converges naturally: the shine's descent ends
        // at his hand right as aimT reaches 1. Falls back to the fixed high
        // aim if there's no shine to track (or it's already been freed).
        Vector3 highAim = (_railShineTrack != null && IsInstanceValid(_railShineTrack))
            ? _railShineTrack.GlobalPosition
            : _railLookHigh;
        _camera.LookAt(highAim.Lerp(_railLookLow, aimT), Vector3.Up);

        // Dutch tilt (camera roll) — the SMS shine-get holds a slight roll so
        // the platform/horizon slopes up to the right, giving it the dynamic
        // hero feel. Ramp it in with aimT so the behind/swing beats stay level
        // and the roll is full by the time we're in the front hero shot.
        if (Mathf.Abs(ShineGetRollDeg) > 0.01f)
            _camera.RotateObjectLocal(Vector3.Forward, Mathf.DegToRad(ShineGetRollDeg) * aimT);
    }

    // ============================================================
    //                       Mode: Free
    // ============================================================
    private void TickFree(float dt, Vector2 rstick, bool camManual)
    {
        if (camManual)
        {
            _tgtYaw   -= rstick.X * CStickYawSpeed * dt;
            float pitchDelta = (CStickYInvertedPitch ? -rstick.Y : rstick.Y);
            _tgtPitch += pitchDelta * CStickPitchSpeed * dt;
            _tgtPitch  = Mathf.Clamp(_tgtPitch, Mathf.DegToRad(MinPitchDeg), Mathf.DegToRad(MaxPitchDeg));
            _tgtRadius += rstick.Y * CStickZoomSpeed * dt;
            _tgtRadius  = Mathf.Clamp(_tgtRadius, MinRadius * _rigScale, MaxRadius * _rigScale);
        }
    }

    // ============================================================
    //                       Follow (look-at point)
    // ============================================================
    private void UpdateFollow(float dt)
    {
        Vector3 offset;
        if (Mode == CamMode.OverShoulder)
        {
            // Mario-LOCAL offset rotated by his VISUAL facing so the shoulder framing
            // tracks him cleanly as he turns. Uses GetVisualYaw() to handle flipped rigs.
            offset = OverShoulderLocalOffset.Rotated(Vector3.Up, GetVisualYaw()) * _rigScale;
        }
        else
        {
            offset = TargetOffset * _rigScale;
        }
        Vector3 marioP = _target.GlobalPosition + offset;

        // XZ tracking — smooth follow (Mario can lead briefly when sprinting)
        float aXZ = 1f - Mathf.Exp(-FollowSmooth * dt);
        _lookAtXZ = _lookAtXZ.Lerp(new Vector3(marioP.X, 0f, marioP.Z), aXZ);

        // Height pan — separate subsystem with leash-based rate.
        // When Mario is within the leash, follow gently. When outside, ramp up fast.
        float dy = marioP.Y - _lookAtY;
        float leash = dy >= 0f ? HeightPanLeashUp * _rigScale : HeightPanLeashDown * _rigScale;
        float urgency = Mathf.Clamp(Mathf.Abs(dy) / Mathf.Max(0.01f, leash), 0f, 4f);
        float effRate = HeightPanRate * (0.5f + 0.5f * urgency);  // double rate at full leash
        float aY = 1f - Mathf.Exp(-effRate * dt);
        _lookAtY = Mathf.Lerp(_lookAtY, marioP.Y, aY);

        GlobalPosition = new Vector3(_lookAtXZ.X, _lookAtY, _lookAtXZ.Z);
    }

    // ============================================================
    //                       Chase (toward targets)
    // ============================================================
    private void ChaseToTargets(float dt)
    {
        // Mode transitions (entering OR exiting OS) get a brief snap-rate window
        // for a graceful sweep into the new framing.
        bool transitioning = _modeTransitionTimer > 0f;
        if (transitioning) _modeTransitionTimer -= dt;

        float yawRate, pitchRate, radiusRate;
        if (transitioning)
        {
            yawRate    = YawSnapRate;
            pitchRate  = PitchSnapRate;
            radiusRate = PitchSnapRate;
        }
        else if (Mode == CamMode.LButton)
        {
            yawRate    = YawSnapRate;
            pitchRate  = PitchSnapRate;
            radiusRate = RadiusChaseRate;
        }
        else if (Mode == CamMode.OverShoulder)
        {
            yawRate    = OverShoulderYawRate;
            pitchRate  = OverShoulderPitchRate;
            radiusRate = OverShoulderRadiusRate;
        }
        else
        {
            yawRate    = YawChaseRate;
            pitchRate  = PitchChaseRate;
            radiusRate = RadiusChaseRate;
        }

        _curYaw    = Mathf.LerpAngle(_curYaw, _tgtYaw,   1f - Mathf.Exp(-yawRate    * dt));
        _curPitch  = Mathf.Lerp     (_curPitch, _tgtPitch, 1f - Mathf.Exp(-pitchRate * dt));
        _curRadius = Mathf.Lerp     (_curRadius, _tgtRadius, 1f - Mathf.Exp(-radiusRate * dt));
    }

    // ============================================================
    //                       Transform application
    // ============================================================
    private void ApplyTransform()
    {
        _pivot.GlobalRotation = new Vector3(0f, _curYaw, 0f);
        _springArm.Rotation   = new Vector3(_curPitch, 0f, 0f);
        _springArm.SpringLength = _curRadius;
    }

    // ============================================================
    //                       Y-button over-the-shoulder mode
    // ============================================================
    private Skeleton3D _marioSkeleton;
    private int _bodyBoneIdx = -1;
    private Quaternion _bodyBonePoseTarget = Quaternion.Identity;
    private Quaternion _bodyBonePoseCurrent = Quaternion.Identity;

    // Counts down each frame after entering or leaving OS. While > 0, chase uses
    // snap rates for a graceful sweep. Once 0, the active mode's normal rates resume.
    private float _modeTransitionTimer = 0f;

    // Normalized 0..1 zoom level — 0 = closest (MinRadius, MaxPitchDeg = level/slight up),
    // 1 = farthest (MaxRadius, MinPitchDeg = overhead). Used when LinkPitchToRadius is on.
    private float _zoomT = 0.4f;

    private float ZoomTtoPitch(float t)  => Mathf.Lerp(Mathf.DegToRad(MaxPitchDeg), Mathf.DegToRad(MinPitchDeg), t);
    private float ZoomTtoRadius(float t) => Mathf.Lerp(MinRadius, MaxRadius, t) * _rigScale;
    private float DefaultZoomT => Mathf.Clamp(Mathf.InverseLerp(MinRadius, MaxRadius, BaseRadius), 0f, 1f);

    private void UpdateOverShoulderState()
    {
        if (string.IsNullOrEmpty(OverShoulderAction) || !InputMap.HasAction(OverShoulderAction))
            return;

        // Toggle: press Y to enter, press Y again to exit.
        if (Input.IsActionJustPressed(OverShoulderAction))
        {
            if (Mode == CamMode.OverShoulder) ExitOverShoulder();
            else                              EnterOverShoulder();
        }
    }

    private void EnterOverShoulder()
    {
        Mode = CamMode.OverShoulder;
        SetMarioLocked(true);

        // Set targets but DO NOT snap _cur values — the chase functions sweep the
        // camera smoothly behind Mario over the entry duration.
        _tgtYaw    = GetVisualYaw();
        _tgtPitch  = Mathf.DegToRad(OverShoulderDefaultPitchDeg);
        _tgtRadius = OverShoulderRadius * _rigScale;
        _modeTransitionTimer = OverShoulderEntryDuration;

        // Lazy-resolve the skeleton/waist bone the first time we enter OS mode.
        if (BodyTrackingEnabled && _marioSkeleton == null && _armature != null)
        {
            _marioSkeleton = _armature.GetNodeOrNull<Skeleton3D>("Skeleton3D");
            if (_marioSkeleton != null)
            {
                _bodyBoneIdx = _marioSkeleton.FindBone(BodyBoneName);
                if (_bodyBoneIdx < 0)
                    GD.PushWarning($"[SunshineCamera] Waist bone '{BodyBoneName}' not found on Mario skeleton — body tracking disabled. Adjust BodyBoneName in the Inspector.");
            }
        }
    }

    private void ExitOverShoulder()
    {
        Mode = CamMode.Normal;
        SetMarioLocked(false);

        // Reset targets to Normal-mode defaults so the camera sweeps back to a
        // proper third-person framing (behind Mario, default zoom level).
        _tgtYaw = GetBehindYaw();
        if (LinkPitchToRadius)
        {
            _zoomT     = DefaultZoomT;
            _tgtPitch  = ZoomTtoPitch(_zoomT);
            _tgtRadius = ZoomTtoRadius(_zoomT);
        }
        else
        {
            _tgtPitch  = Mathf.DegToRad(DefaultPitchDeg);
            _tgtRadius = BaseRadius * _rigScale;
        }
        _modeTransitionTimer = OverShoulderEntryDuration;

        if (_marioSkeleton != null && _bodyBoneIdx >= 0)
        {
            _bodyBonePoseTarget  = Quaternion.Identity;
            _bodyBonePoseCurrent = Quaternion.Identity;
            _marioSkeleton.SetBonePoseRotation(_bodyBoneIdx, Quaternion.Identity);
        }
    }

    private void SetMarioLocked(bool locked)
    {
        if (_target is Mario mario)
            mario.CameraLocked = locked;
    }

    // ============================================================
    //                       Shine Get hero shot
    // ============================================================
    // Triggered by Mario.cs (StartShineGet/OnAnimationFinished), not by input
    // detected here — unlike OverShoulder, this mode is driven by a gameplay
    // event, not a button. Mario.cs owns CameraLocked for this sequence; this
    // class only owns the camera's own framing/mode.
    /// <param name="shineToTrack">The descending shine sprite — the behind/swing
    /// beats aim at it as it falls (the video's camera visibly follows the
    /// shine down the sky). Null-safe: falls back to a fixed high aim.</param>
    public void EnterShineGetShot(Node3D shineToTrack = null)
    {
        Mode = CamMode.ShineGet;
        _shineShotTime = 0f;
        _railShineTrack = shineToTrack;

        if (_target == null || _camera == null) return;

        // Lazily build the Path3D + PathFollow3D rail nodes. RotationMode is
        // None because the camera aims itself via LookAt every tick — the
        // follower only supplies position along the curve.
        if (_shineRail == null)
        {
            _shineRail = new Path3D { Name = "ShineGetRail" };
            AddChild(_shineRail);
            _shineRailFollow = new PathFollow3D
            {
                Name = "ShineGetRailFollow",
                RotationMode = PathFollow3D.RotationModeEnum.None,
                Loop = false,
            };
            _shineRail.AddChild(_shineRailFollow);
        }

        // Pin the rail at Mario's feet, rotated to his visual facing at this
        // instant — the curve points below are authored in that local space,
        // so the node's transform does the local→world work for us.
        // NOTE: -Z is "in front of Mario" in this space, not +Z — empirically
        // verified: this rig's SpringArm3D pushes the normal follow camera to
        // local +Z, which GetBehindYaw() confirms sits BEHIND Mario. Positive
        // Z rail points end up clipped into the back of his head (this bit us
        // once already — caught via rendered screenshot, not reasoning).
        float yaw = GetVisualYaw();
        _shineRail.GlobalTransform = new Transform3D(new Basis(Vector3.Up, yaw), _target.GlobalPosition);

        // Author the full rail fresh each grab: behind → side → front (the
        // swing around Mario), then front → mid → end (the crane-up). Gentle
        // in/out handles on the interior points keep the corners smooth as
        // the follower sweeps through them.
        Vector3 pBehind = ShineGetCamBehind * _rigScale;
        Vector3 pSide   = ShineGetCamSide * _rigScale;
        Vector3 pFront  = ShineGetCamStart * _rigScale;
        Vector3 pMid    = ShineGetCamMid * _rigScale;
        Vector3 pEnd    = ShineGetCamEnd * _rigScale;
        var curve = new Curve3D();
        curve.AddPoint(pBehind);
        curve.AddPoint(pSide, (pBehind - pFront) * 0.2f, (pFront - pBehind) * 0.2f);
        curve.AddPoint(pFront);
        curve.AddPoint(pMid, (pFront - pEnd) * 0.15f, (pEnd - pFront) * 0.15f);
        curve.AddPoint(pEnd);
        _shineRail.Curve = curve;

        // Where along the curve the front-low point sits (0..1) — the swing
        // phase sweeps up to here, the crane phase covers the rest.
        float baked = curve.GetBakedLength();
        _railFrontRatio = baked > 0.001f ? curve.GetClosestOffset(pFront) / baked : 0.5f;

        // Rotated by yaw — the offset has a lateral component now (Mario sits
        // right-of-center in frame), which is only correct in his local space.
        _railLookLow  = _target.GlobalPosition + (ShineGetLookAtOffset * _rigScale).Rotated(Vector3.Up, yaw);
        Vector3 highLocal = new Vector3(ShineGetLookAtHighOffset.X, ShineGetLookAtHighOffset.Y, ShineGetLookAtHighOffset.Z) * _rigScale;
        _railLookHigh = _target.GlobalPosition + highLocal.Rotated(Vector3.Up, yaw);

        // Detach the camera from the SpringArm chain and put it on the rail.
        // SpringArm3D does its own internal positioning of what's beneath it
        // every physics step; leaving the camera parented there while the
        // rail drives it would fight itself. Reparent(keepGlobal: false)
        // keeps the OLD local transform, so explicitly zero it to sit
        // exactly on the follower.
        _shineRailFollow.ProgressRatio = 0f;
        _camera.Reparent(_shineRailFollow, false);
        _camera.Position = Vector3.Zero;
        // Behind beat aims at the descending shine (already teleported to its
        // sky start by BeginCollectSequence, which runs before this).
        Vector3 initialAim = (_railShineTrack != null && IsInstanceValid(_railShineTrack))
            ? _railShineTrack.GlobalPosition
            : _railLookHigh;
        _camera.LookAt(initialAim, Vector3.Up);
    }

    public void ExitShineGetShot()
    {
        if (Mode != CamMode.ShineGet) return;
        Mode = CamMode.Normal;

        // Re-attach to the SpringArm chain and reset local transform — the
        // normal polar update resumes driving it from here.
        if (_camera != null && _springArm != null)
        {
            _camera.Reparent(_springArm, false);
            _camera.Position = Vector3.Zero;
            _camera.Rotation = Vector3.Zero;
        }

        // Same "return to Normal" reset as ExitOverShoulder — sweep back to a
        // proper third-person framing behind Mario at the default zoom level.
        _tgtYaw = GetBehindYaw();
        if (LinkPitchToRadius)
        {
            _zoomT     = DefaultZoomT;
            _tgtPitch  = ZoomTtoPitch(_zoomT);
            _tgtRadius = ZoomTtoRadius(_zoomT);
        }
        else
        {
            _tgtPitch  = Mathf.DegToRad(DefaultPitchDeg);
            _tgtRadius = BaseRadius * _rigScale;
        }
        _modeTransitionTimer = ShineGetEntryDuration;
    }

    /// <summary>
    /// While in OverShoulder mode AND looking down at Mario, bend his waist
    /// backward so he tilts to look up at the camera. No effect when looking
    /// forward or up.
    /// </summary>
    private void UpdateBodyTracking(float dt)
    {
        if (!BodyTrackingEnabled || _marioSkeleton == null || _bodyBoneIdx < 0) return;

        bool osActive = (Mode == CamMode.OverShoulder);

        if (osActive)
        {
            // _curPitch < 0 means the camera is ABOVE Mario looking DOWN.
            float pitchDeg = Mathf.RadToDeg(_curPitch);
            float t = Mathf.Clamp(
                Mathf.InverseLerp(BodyBendStartPitchDeg, BodyBendFullPitchDeg, pitchDeg),
                0f, 1f);
            float bendDeg = t * BodyBendMaxDeg;
            // Negative X rotation tilts the upper body BACKWARD (looking up).
            _bodyBonePoseTarget = new Quaternion(Vector3.Right, Mathf.DegToRad(-bendDeg));
        }
        else
        {
            _bodyBonePoseTarget = Quaternion.Identity;
        }

        float a = 1f - Mathf.Exp(-BodyTrackResponse * dt);
        _bodyBonePoseCurrent = _bodyBonePoseCurrent.Slerp(_bodyBonePoseTarget, a);
        _marioSkeleton.SetBonePoseRotation(_bodyBoneIdx, _bodyBonePoseCurrent);
    }

    // ============================================================
    //                       L-button tap vs hold
    // ============================================================
    private void UpdateLButtonState(float dt)
    {
        if (string.IsNullOrEmpty(LButtonAction) || !InputMap.HasAction(LButtonAction)) return;

        if (Input.IsActionPressed(LButtonAction))
        {
            // Pressed this frame? Start the held timer
            if (_lButtonHeldTime < 0f) _lButtonHeldTime = 0f;
            else _lButtonHeldTime += dt;

            // If held past tap window, enter LButton mode
            if (Mode == CamMode.Normal && _lButtonHeldTime > TapMaxDuration)
                Mode = CamMode.LButton;
        }
        else
        {
            // Released
            if (_lButtonHeldTime >= 0f && _lButtonHeldTime <= TapMaxDuration)
            {
                // It was a TAP — instant recenter (snap behind Mario)
                DoRecenter();
            }
            _lButtonHeldTime = -1f;

            // Always return to Normal once L is released
            if (Mode == CamMode.LButton) Mode = CamMode.Normal;
        }
    }

    private void DoRecenter()
    {
        _tgtYaw = GetBehindYaw();
        if (LinkPitchToRadius)
        {
            _zoomT     = DefaultZoomT;
            _tgtPitch  = ZoomTtoPitch(_zoomT);
            _tgtRadius = ZoomTtoRadius(_zoomT);
        }
        else
        {
            _tgtPitch  = Mathf.DegToRad(DefaultPitchDeg);
            _tgtRadius = BaseRadius * _rigScale;
        }
        // Force a fast chase for one frame by overwriting current toward target.
        _curYaw    = Mathf.LerpAngle(_curYaw,    _tgtYaw,    0.6f);
        _curPitch  = Mathf.Lerp     (_curPitch,  _tgtPitch,  0.6f);
        _curRadius = Mathf.Lerp     (_curRadius, _tgtRadius, 0.4f);
    }

    // ============================================================
    //                       Helpers
    // ============================================================
    private float GetBehindYaw()
    {
        // Prefer VELOCITY (world coords — immune to model facing conventions).
        if (_target is CharacterBody3D cb)
        {
            Vector3 v = cb.Velocity;
            v.Y = 0f;
            if (v.LengthSquared() > 0.04f)
            {
                v = v.Normalized();
                // Camera should sit OPPOSITE to where Mario is moving.
                // Pivot yaw such that SpringArm extends toward -v in world.
                return Mathf.Atan2(-v.X, -v.Z);
            }
        }
        // Stationary fallback: read armature facing.
        if (_armature != null)
        {
            Vector3 fwd = -_armature.GlobalTransform.Basis.Z;
            fwd.Y = 0f;
            if (fwd.LengthSquared() > 0.0001f)
            {
                fwd = fwd.Normalized();
                // Apply 180° correction if the rig was authored with opposite forward.
                float yaw = Mathf.Atan2(-fwd.X, -fwd.Z);
                if (ArmatureForwardIsFlipped) yaw += Mathf.Pi;
                return yaw;
            }
        }
        return _curYaw;
    }

    /// <summary>
    /// True if Mario's L-stick input is pushing roughly toward the camera (within
    /// ApproachConeDeg of the camera-to-Mario direction). Auto-behind is suppressed
    /// in this case so the camera doesn't spin to chase backward motion.
    /// </summary>
    private bool IsApproachingCamera(Vector2 stick)
    {
        if (_camera == null || _target == null) return false;
        if (stick == Vector2.Zero) return false;

        // Convert camera-relative stick to world dir using current yaw.
        Vector3 stickDirWorld = new Vector3(stick.X, 0f, stick.Y).Rotated(Vector3.Up, _curYaw);
        stickDirWorld.Y = 0f;
        if (stickDirWorld.LengthSquared() < 1e-4f) return false;
        stickDirWorld = stickDirWorld.Normalized();

        // Direction from Mario toward the camera.
        Vector3 toCam = _camera.GlobalPosition - _target.GlobalPosition;
        toCam.Y = 0f;
        if (toCam.LengthSquared() < 1e-4f) return false;
        toCam = toCam.Normalized();

        return stickDirWorld.Dot(toCam) >= Mathf.Cos(Mathf.DegToRad(ApproachConeDeg));
    }

    private float GetTargetSpeedXZ()
    {
        if (_target is CharacterBody3D cb)
        {
            Vector3 v = cb.Velocity;
            return new Vector2(v.X, v.Z).Length();
        }
        return 0f;
    }

    private bool IsTargetOnFloor()
        => _target is CharacterBody3D cb && cb.IsOnFloor();

    private static Vector2 ApplyDeadzone(Vector2 v, float dz)
    {
        float len = v.Length();
        if (len <= dz) return Vector2.Zero;
        float scaled = (len - dz) / (1f - dz);
        return (v / len) * scaled;
    }

    private static float StepAngle(float current, float target, float maxRadStep)
    {
        float diff = Mathf.AngleDifference(current, target);
        diff = Mathf.Clamp(diff, -maxRadStep, maxRadStep);
        return current + diff;
    }

    private void RecomputeRigScale()
    {
        _rigScale = 1f;
        if (_target == null) return;

        var cs = _target.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (cs?.Shape is CapsuleShape3D cap)
        {
            float total = cap.Height + 2f * cap.Radius;
            if (total > 0.01f)
            {
                float sy = _target.GlobalTransform.Basis.Scale.Y;
                float h = total * Mathf.Max(0.01f, sy);
                _rigScale = h / Mathf.Max(0.01f, ReferenceTargetHeight);
            }
        }
    }
}
