using Godot;

public partial class CamController : Node3D
{
    // --- Target / Rig ---
    [Export]
    public NodePath TargetPath = new NodePath(".."); // Mario

    [Export]
    public NodePath FacingPath = new NodePath("../Armature"); // optional

    [Export]
    public NodePath PivotPath = new NodePath("SpringArmPivot");

    [Export]
    public NodePath SpringArmPath = new NodePath("SpringArmPivot/SpringArm3D");

    // Preset A (Sunshine-ish, meters scale)
    [Export]
    public Vector3 FollowOffset = new Vector3(0, 1.8f, 0); // Higher — Mario's chest area

    [Export]
    public float FollowSmooth = 5f; // Reduced for more lag/smoothness

    // --- Right stick actions ---
    [Export]
    public string RStickLeft = "Rstick_left";

    [Export]
    public string RStickRight = "Rstick_right";

    [Export]
    public string RStickUp = "Rstick_up";

    [Export]
    public string RStickDown = "Rstick_down";

    // --- Orbit tuning ---
    [Export]
    public float ManualYawSpeed = 3.2f;

    [Export]
    public float ManualPitchSpeed = 2.6f;

    [Export]
    public float MinPitchDeg = -55f;   // Can look further down when zooming out

    [Export]
    public float MaxPitchDeg = 30f;    // Can look slightly further up

    [Export]
    public float DefaultPitchDeg = -18f; // SMS rests at ~-18° (slightly above and behind)

    [Export]
    public float ManualDeadzone = 0.18f;

    // --- Auto-behind tuning ---
    [Export]
    public float AutoBehindDelay = 0.55f;

    [Export]
    public float AutoBehindStrength = 0.8f; // Much gentler auto-behind pull

    [Export]
    public float AutoBehindMinSpeed = 0.6f;

    // NEW: don’t auto-behind immediately when left stick goes neutral
    [Export]
    public float MoveReleaseDelay = 0.55f;

    // NEW: stop auto-behind while airborne (feels much more Sunshine)
    [Export]
    public bool AutoBehindOnlyOnFloor = true;

    // NEW: "orbital" assist while moving (slow pivot around Mario)
    [Export]
    public float OrbitAssistStrength = 0.2f; // very gentle — Sunshine barely assists

    [Export]
    public float OrbitAssistMinSpeed = 1.5f; // only kick in at a proper running speed

    [Export]
    public NodePath CameraPath = new NodePath("SpringArmPivot/SpringArm3D/Camera3D");
    private Camera3D _camera;

    // --- Zoom tuning (meters-scale sane defaults) ---
    [Export]
    public float MinArmLength = 3.5f;   // Closest camera can get

    [Export]
    public float MaxArmLength = 11.0f;  // Furthest when holding stick down

    [Export]
    public float BaseArmLength = 6.0f;  // Neutral distance — further back like SMS

    [Export]
    public float SpeedZoomMaxOut = 0.5f; // Small speed zoom, SMS doesn't zoom much

    [Export]
    public float StickZoomOutSpeed = 4.0f;

    [Export]
    public float ZoomSmooth = 8f;       // Slightly slower zoom for smoothness

    [Export]
    public float SpeedForMaxZoomOut = 14f;

    [Export]
    public float StickZoomDeadzone = 0.35f;

    // Mouse (optional)
    [Export]
    public bool EnableMouse = true;

    [Export]
    public float MouseYawSpeed = 0.0035f;

    [Export]
    public float MousePitchSpeed = 0.0035f;

    [Export]
    public float AutoYawMaxDegPerSec = 25f; // Slow lazy auto-behind rotation

    [Export]
    public float OrbitYawMaxDegPerSec = 6f; // Very slow orbit — barely noticeable

    // --- Movement actions ---
    // change from ui_* to Lstick_*
    [Export]
    public string MoveLeft = "Lstick_left";

    [Export]
    public string MoveRight = "Lstick_right";

    [Export]
    public string MoveForward = "Lstick_up";

    [Export]
    public string MoveBack = "Lstick_down";

    [Export]
    public float MoveDeadzone = 0.18f;

    // --- Orbit assist gating (NEW) ---
    [Export]
    public float ForwardSuppressDot = 0.92f; // >0.92 = mostly forward/back (skip orbit). 0.92 ~ 23°

    [Export]
    public float MinStrafeForOrbit = 0.12f; // tiny strafe should do almost nothing

    [Export]
    public float StrafePower = 1.6f; // higher = much slower at small strafes

    [Export]
    public float OrbitYawDeadbandDeg = 2.0f; // don’t correct tiny yaw differences (prevents drift)

    private Node3D _target;
    private Node3D _facing;
    private Node3D _pivot;
    private SpringArm3D _springArm;

    private float _yaw;
    private float _pitch;

    // renamed: time since last CAMERA input (right stick / mouse)
    private float _camManualTimer = 999f;

    // NEW: time since last MOVE input (left stick)
    private float _moveNeutralTimer = 999f;

    private float _armLen;
    private float _armLenGoal;

    // --- Speed-scaled orbit assist (slow = gentle, fast = stronger) ---
    [Export]
    public float OrbitYawMaxDegPerSec_Min = 0f; // at very slow movement

    [Export]
    public float OrbitYawMaxDegPerSec_Max = 6f; // at full speed — barely noticeable

    [Export]
    public float OrbitSpeedForMaxTurn = 12f; // RUN_SPEED-ish (meters scale)

    // --- "Approaching camera" suppression cone ---
    [Export]
    public float NoAssistApproachConeDeg = 20f; // +/- 20 degrees

    [Export]
    public float NoAssistMinSpeed = 0.8f; // don't bother if basically stopped

    [Export]
    public bool SuppressAutoBehindWhenApproachingCamera = true;

    // --- R-trigger camera recenter (SMS L-trigger style) ---
    [Export]
    public string CameraResetAction = "button_l";

    [Export]
    public float ResetSwingSpeed = 8.0f; // how fast the yaw/pitch lerps during reset

    [Export]
    public float ResetDuration = 0.35f; // seconds the smooth swing lasts

    private bool _resetting = false;
    private float _resetTimer = 0f;
    private float _resetTargetYaw;
    private float _resetTargetPitch;

    // -----------------------------
    // NEW: Auto-scale camera to Mario height so re-scaling doesn't break camera
    // -----------------------------
    [Export]
    public bool AutoScaleToTargetHeight = true;

    // Height (meters) your current camera numbers were tuned for.
    // If you tuned camera when Mario was ~1.8m tall, leave this at 1.8.
    [Export]
    public float ReferenceTargetHeight = 1.80f;

    private float _rigScale = 1f;
    private Vector3 _followOffsetScaled;
    private float _minArmScaled,
        _maxArmScaled,
        _baseArmScaled,
        _speedZoomMaxOutScaled;

    public override void _Ready()
    {
        TopLevel = true;

        _camera = GetNodeOrNull<Camera3D>(CameraPath);

        _target = !TargetPath.IsEmpty ? GetNodeOrNull<Node3D>(TargetPath) : GetParent<Node3D>();
        _facing = !FacingPath.IsEmpty ? GetNodeOrNull<Node3D>(FacingPath) : null;

        _pivot = GetNodeOrNull<Node3D>(PivotPath);
        _springArm = GetNodeOrNull<SpringArm3D>(SpringArmPath);

        if (_springArm != null && _target is CollisionObject3D co)
            _springArm.AddExcludedObject(co.GetRid());

        if (_pivot == null)
            GD.PushError($"CamController: Pivot not found. PivotPath='{PivotPath}'.");
        if (_springArm == null)
            GD.PushError($"CamController: SpringArm not found. SpringArmPath='{SpringArmPath}'.");
        if (_target == null)
            GD.PushError($"CamController: Target not found. TargetPath='{TargetPath}'.");

        // Ensure old scene local offsets don't keep camera floating above Mario
        if (_pivot != null)
            _pivot.Position = Vector3.Zero;
        if (_springArm != null)
            _springArm.Position = Vector3.Zero;

        // Compute scaled camera distances based on Mario's actual height
        RecomputeScaledRigNumbers();

        if (_pivot != null)
            _yaw = _pivot.GlobalRotation.Y;

        // Always start at the SMS-like default pitch (slightly above & behind Mario)
        _pitch = Mathf.DegToRad(DefaultPitchDeg);
        _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitchDeg), Mathf.DegToRad(MaxPitchDeg));

        _armLen = _baseArmScaled;
        _armLenGoal = _baseArmScaled;

        ApplyRot();
        if (_springArm != null)
            _springArm.SpringLength = _armLen;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pivot == null || _springArm == null)
            return;

        float dt = (float)delta;

        // Follow target with lag
        if (_target != null)
        {
            // Raise follow point when camera is nearly at ground level so player sees more ahead.
            // Boost fades in as pitch rises toward 0 (horizontal), peaks at ~0.75m when fully level.
            float pitchBoostStart = Mathf.DegToRad(-20f); // no boost above this pitch
            float pitchBoostFull  = Mathf.DegToRad(0f);   // full boost at horizontal
            float pitchT = Mathf.Clamp(Mathf.InverseLerp(pitchBoostStart, pitchBoostFull, _pitch), 0f, 1f);
            float heightBoost = pitchT * 0.75f * _rigScale;

            Vector3 targetPos = _target.GlobalPosition + _followOffsetScaled + new Vector3(0f, heightBoost, 0f);
            float a = 1f - Mathf.Exp(-FollowSmooth * dt);
            GlobalPosition = GlobalPosition.Lerp(targetPos, a);
        }

        // ---- MOVE input timer (left stick)
        Vector2 move = ApplyDeadzone(GetMoveInput(), MoveDeadzone);
        bool playerMovingInput = move != Vector2.Zero;
        if (playerMovingInput)
            _moveNeutralTimer = 0f;
        else
            _moveNeutralTimer += dt;

        // ---- CAMERA input (right stick)
        Vector2 rRaw = Input.GetVector(RStickLeft, RStickRight, RStickUp, RStickDown);

        // apply deadzone w/ remap
        Vector2 r = ApplyDeadzone(rRaw, ManualDeadzone);

        bool manualYaw = r.X != 0f;
        bool manualPitch = r.Y != 0f;

        bool camManualThisFrame = manualYaw || manualPitch;
        if (camManualThisFrame)
            _camManualTimer = 0f;
        else
            _camManualTimer += dt;

        if (manualYaw)
            _yaw += (-r.X) * ManualYawSpeed * dt;

        if (manualPitch)
        {
            // Stick UP (r.Y negative) = pitch decreases = camera goes higher, looks down at Mario (SMS feel)
            // Stick DOWN (r.Y positive) = pitch increases = camera goes lower, looks up
            _pitch += r.Y * ManualPitchSpeed * dt;
            _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitchDeg), Mathf.DegToRad(MaxPitchDeg));

            // Stick up/down also gently adjusts zoom distance like SMS C-stick
            _armLenGoal += r.Y * StickZoomOutSpeed * dt;
        }

        float speedXZ = GetTargetSpeedXZ();

        // Speed-based zoom
        float tSpeed = Mathf.Clamp(speedXZ / Mathf.Max(0.01f, SpeedForMaxZoomOut), 0f, 1f);
        float speedLen = _baseArmScaled + _speedZoomMaxOutScaled * tSpeed;

        _armLenGoal = Mathf.Max(_armLenGoal, speedLen);
        _armLenGoal = Mathf.Clamp(_armLenGoal, _minArmScaled, _maxArmScaled);

        // ---- ORBIT ASSIST: very gentle nudge toward movement direction while steering.
        // SMS has this but it's barely noticeable — a slow lazy follow.
        if (playerMovingInput && !camManualThisFrame && speedXZ >= OrbitAssistMinSpeed)
        {
            Vector2 m = move.Normalized();
            float forwardAbs = Mathf.Abs(m.Y);
            float strafeAbs = Mathf.Abs(m.X);

            // Skip when running mostly straight forward/back
            if (!(forwardAbs >= ForwardSuppressDot && strafeAbs < 0.35f)
                && strafeAbs >= MinStrafeForOrbit
                && !IsPushingTowardCameraCone(move, NoAssistApproachConeDeg))
            {
                float desiredYaw = GetDesiredYawFromMoveInput(move);
                float yawDiff = Mathf.Abs(Mathf.AngleDifference(_yaw, desiredYaw));
                if (yawDiff >= Mathf.DegToRad(OrbitYawDeadbandDeg))
                {
                    float maxStep = GetOrbitMaxStepRad(dt, speedXZ, strafeAbs);
                    float a = 1f - Mathf.Exp(-OrbitAssistStrength * dt);
                    float blendedTarget = Mathf.LerpAngle(_yaw, desiredYaw, a);
                    _yaw = StepAngle(_yaw, blendedTarget, maxStep);
                }
            }
        }

        // ---- AUTO BEHIND: only while actively steering, never on neutral stick.
        if (playerMovingInput && _camManualTimer >= AutoBehindDelay && speedXZ >= AutoBehindMinSpeed)
        {
            if (!AutoBehindOnlyOnFloor || IsTargetOnFloor())
            {
                if (!SuppressAutoBehindWhenApproachingCamera
                    || !IsPushingTowardCameraCone(move, NoAssistApproachConeDeg))
                {
                    float behindYaw = GetBehindYawFromFacingOrVelocity();
                    float a = 1f - Mathf.Exp(-AutoBehindStrength * dt);
                    float blendedTarget = Mathf.LerpAngle(_yaw, behindYaw, a);
                    float maxStep = Mathf.DegToRad(AutoYawMaxDegPerSec) * dt;
                    _yaw = StepAngle(_yaw, blendedTarget, maxStep);
                }
            }
        }

        // ---- R-TRIGGER CAMERA RECENTER (SMS style) ----
        if (InputMap.HasAction(CameraResetAction) && Input.IsActionJustPressed(CameraResetAction))
        {
            GD.Print("RESET CAMERA");
            _resetting = true;
            _resetTimer = 0f;
            _resetTargetYaw = GetFacingYawForReset();
            _resetTargetPitch = Mathf.DegToRad(DefaultPitchDeg);
        }

        if (_resetting)
        {
            // Cancel reset if the player touches the right stick
            if (camManualThisFrame)
            {
                _resetting = false;
            }
            else
            {
                _resetTimer += dt;
                float t = Mathf.Clamp(_resetTimer / ResetDuration, 0f, 1f);

                _yaw = Mathf.LerpAngle(_yaw, _resetTargetYaw, 1f - Mathf.Exp(-ResetSwingSpeed * dt));
                _pitch = Mathf.Lerp(_pitch, _resetTargetPitch, 1f - Mathf.Exp(-ResetSwingSpeed * dt));

                // Snap when close enough or time's up
                if (t >= 1f || Mathf.Abs(Mathf.AngleDifference(_yaw, _resetTargetYaw)) < 0.005f)
                {
                    _yaw = _resetTargetYaw;
                    _pitch = _resetTargetPitch;
                    _resetting = false;
                }
            }
        }

        ApplyRot();
        ApplyZoom(dt);

        // Relax zoom goal back toward speed distance
        float relax = 1f - Mathf.Exp(-2.5f * dt);
        _armLenGoal = Mathf.Lerp(_armLenGoal, speedLen, relax);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!EnableMouse || _pivot == null || _springArm == null)
            return;

        if (@event is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _camManualTimer = 0f;

            _yaw += (-mm.Relative.X) * MouseYawSpeed;
            _pitch += (-mm.Relative.Y) * MousePitchSpeed;
            _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitchDeg), Mathf.DegToRad(MaxPitchDeg));

            ApplyRot();
        }
    }

    private void ApplyRot()
    {
        _pivot.GlobalRotation = new Vector3(0f, _yaw, 0f);
        _springArm.Rotation = new Vector3(_pitch, 0f, 0f);
    }

    private void ApplyZoom(float dt)
    {
        float a = 1f - Mathf.Exp(-ZoomSmooth * dt);
        _armLen = Mathf.Lerp(_armLen, _armLenGoal, a);
        _springArm.SpringLength = _armLen;
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
    {
        return _target is CharacterBody3D cb && cb.IsOnFloor();
    }

    private Vector2 GetMoveInput()
    {
        if (
            !InputMap.HasAction(MoveLeft)
            || !InputMap.HasAction(MoveRight)
            || !InputMap.HasAction(MoveForward)
            || !InputMap.HasAction(MoveBack)
        )
            return Vector2.Zero;

        return Input.GetVector(MoveLeft, MoveRight, MoveForward, MoveBack);
    }

    private static Vector2 ApplyDeadzone(Vector2 v, float dz)
    {
        float len = v.Length();
        if (len <= dz)
            return Vector2.Zero;

        // Remap so output still reaches 1.0 at full stick
        float scaled = (len - dz) / (1f - dz);
        return (v / len) * scaled;
    }

    private float GetBehindYawFromFacingOrVelocity()
    {
        // 1) Prefer Armature facing (best if your Armature rotates with Mario)
        if (_facing != null)
        {
            Vector3 fwd = -_facing.GlobalTransform.Basis.Z;
            fwd.Y = 0f;
            if (fwd.Length() > 0.001f)
            {
                fwd = fwd.Normalized();
                return Mathf.Atan2(-fwd.X, -fwd.Z);
            }
        }

        // 2) Otherwise fall back to movement direction
        if (_target is CharacterBody3D cb)
        {
            Vector3 v = cb.Velocity;
            v.Y = 0f;
            if (v.Length() > 0.15f)
            {
                v = v.Normalized();
                return Mathf.Atan2(-v.X, -v.Z);
            }
        }

        // 3) If totally stopped, keep current yaw (don't snap)
        return _yaw;
    }

    // Used by camera reset — reads armature local rotation directly (works even when standing still)
    private float GetFacingYawForReset()
    {
        if (_target != null)
        {
            var armature = _target.GetNodeOrNull<Node3D>("Armature");
            if (armature != null)
                return armature.Rotation.Y;
        }
        return GetBehindYawFromFacingOrVelocity();
    }

    private static float StepAngle(float current, float target, float maxRadStep)
    {
        float diff = Mathf.AngleDifference(current, target);
        diff = Mathf.Clamp(diff, -maxRadStep, maxRadStep);
        return current + diff;
    }

    private float GetOrbitMaxStepRad(float dt, float speedXZ, float strafeAbs)
    {
        // Speed factor: walk vs sprint
        float tSpeed = Mathf.Clamp(speedXZ / Mathf.Max(0.01f, OrbitSpeedForMaxTurn), 0f, 1f);

        // Strafe shaping: tiny strafe = almost no turn
        float tStrafe = Mathf.Pow(Mathf.Clamp(strafeAbs, 0f, 1f), StrafePower);

        // Multiply them so BOTH matter
        float degPerSec =
            Mathf.Lerp(OrbitYawMaxDegPerSec_Min, OrbitYawMaxDegPerSec_Max, tSpeed) * tStrafe;
        return Mathf.DegToRad(degPerSec) * dt;
    }

    private bool IsPushingTowardCameraCone(Vector2 moveInput, float coneDeg)
    {
        if (_target == null || _camera == null)
            return false;

        // Must actually be pushing the stick
        if (moveInput == Vector2.Zero)
            return false;

        // Convert stick direction (camera-relative) into world direction using current camera yaw (_yaw)
        Vector3 pushDir = new Vector3(moveInput.X, 0f, moveInput.Y);
        pushDir = pushDir.Rotated(Vector3.Up, _yaw);
        pushDir.Y = 0f;
        if (pushDir.Length() < 0.0001f)
            return false;
        pushDir = pushDir.Normalized();

        // Direction from target -> camera (XZ)
        Vector3 toCam = _camera.GlobalPosition - _target.GlobalPosition;
        toCam.Y = 0f;
        if (toCam.Length() < 0.0001f)
            return false;
        toCam = toCam.Normalized();

        // within +/- coneDeg of "toward camera"
        float dot = pushDir.Dot(toCam);
        float threshold = Mathf.Cos(Mathf.DegToRad(coneDeg));
        return dot >= threshold;
    }

    private float GetDesiredYawFromMoveInput(Vector2 moveInput)
    {
        // moveInput is camera-relative: (x = right, y = forward)
        // Convert it to world dir using current camera yaw
        Vector3 dir = new Vector3(moveInput.X, 0f, moveInput.Y).Rotated(Vector3.Up, _yaw);
        dir.Y = 0f;
        if (dir.Length() < 0.0001f)
            return _yaw;

        dir = dir.Normalized();

        // We want the camera to end up behind that movement direction
        return Mathf.Atan2(-dir.X, -dir.Z);
    }

    // -----------------------------
    // Auto-scale helpers
    // -----------------------------
    private float GetApproxTargetHeightMeters()
    {
        if (_target != null)
        {
            // If your collision shape node has a different name/path, update this one line:
            var cs = _target.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (cs?.Shape is CapsuleShape3D cap)
            {
                // CapsuleShape3D: Height is cylinder height (not caps). Total ~= height + 2*radius
                float total = cap.Height + 2f * cap.Radius;
                if (total > 0.01f)
                {
                    float sy = _target.GlobalTransform.Basis.Scale.Y;
                    return total * Mathf.Max(0.01f, sy);
                }
            }

            // Fallback: scale-based estimate
            float scaleY = _target.GlobalTransform.Basis.Scale.Y;
            if (scaleY > 0.01f)
                return 1.8f * scaleY;
        }

        return 1.8f;
    }

    private void RecomputeScaledRigNumbers()
    {
        _rigScale = 1f;

        if (AutoScaleToTargetHeight)
        {
            float h = GetApproxTargetHeightMeters();
            _rigScale = h / Mathf.Max(0.01f, ReferenceTargetHeight);
        }

        _followOffsetScaled = FollowOffset * _rigScale;

        _minArmScaled = MinArmLength * _rigScale;
        _maxArmScaled = MaxArmLength * _rigScale;
        _baseArmScaled = BaseArmLength * _rigScale;
        _speedZoomMaxOutScaled = SpeedZoomMaxOut * _rigScale;
    }
}
