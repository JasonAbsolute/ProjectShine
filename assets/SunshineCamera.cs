using System;
using Godot;
using Godot.Collections;

/// <summary>
/// The game's camera: CPolarSubCamera's NORMAL follow camera (pass 53), driven by the disc's kind records (assets/Sms/SmsCameraKinds.cs:
/// /Camera/kind*.prm and camera_normal.prm). Sources, doldecomp/sms: cameragc.cpp ctrlGameCamera_ 868 / calcPosAndAt_ 464 /
/// calcFinalPosAndAt_ 828 / calcExternalData_ 857 / rotateX_ByStickY_ 333 / rotateY_ByStickX_ 346 / isMarioReadyGun_ 372 /
/// calcSlopeAngleX_ 408 / loadAfter 134; CameraNormal.cpp ctrlNormalOrTowerCamera_ 68; CameraHeightPan.cpp; CameraBGCheck.cpp;
/// CameraInbetween.cpp; CameraKindParam.cpp; CameraSecureView.cpp; CameraChange.cpp execCameraModeChangeProc_ 568 / changeCamModeSub_ 272 /
/// execFrontRotate_ 447; CameraMarioData.cpp; cameralib.cpp. The decomp's calcPosAndAt_ is NonMatching: against the original asm
/// (QbeRoot/sms asm/Camera/cameragc.s) the polar solve's stores go to mCurrentTarget.unk18 (the DESIRED eye), mCurrentTarget.mPosition
/// is the height-PANNED eye whose x / z are copied from unk18 just before execHeightPan_, finalAt.y is mCurrentTarget.mTarget.y
/// (0x90, after the pan offset -- not unk18.y), and the ratio restored after a wall push is mPreviousTarget.unk28 (0xDC), not unk30.
///
/// THE CAMERA RUNS AT 120 Hz (PerformList Movement, CUE_MOVE every simulation tick): every "a frame" rate here is a tick. Mario moves
/// once a 30 fps frame (CUE_MOVEMENT_GATE_A | B), so the move distances the camera reads are his per-FRAME displacement squared,
/// clamped 100 (CameraMarioData.cpp 23-45) -- SmsCamMarioData carries them so. Angles are s16 BAM (CLBPolarToCross: the eye at
/// origin + r (cos v sin h, sin v, cos v cos h); the yaw is the bearing of the eye about the look-at, +Z toward +X, the same convention
/// MarioYCam.cs validated), distances game units (74.44 a metre) until the apply step.
///
/// The polar rig about the look-at: each tick the look point is Mario's origin + AtOffsetY (+ ratio x XRotRatioAtOffsetY; no head joint);
/// the pitch ratio unk28 (0..1, the C-stick's Y at 0.01 a tick) picks the pitch, the distance and the look height along the kind's min..max;
/// the yaw is the C-stick's X at YAngleManualSpeedX a tick, or chases "behind him" (his face - 0x8000) at FollowSpeedX x how far off x the
/// obstruct / L magnifiers x his move distance; the desired eye is solved about the look point with the cushion rule (a Follow kind keeps
/// its exact distance; a cushioned kind lets him approach), pulled in by the squat aim's HoldAddDistXZ, panned in Y (the eye rides a slower
/// Y chase while he is airborne and rising), pushed out of walls (150 u) and up off the ground (144 u, 30 u when squat-aiming), then the
/// actual eye and look-at chase it at the kind's PosChaseRate / AtChaseRate a tick, the look-at's pitch clamped to +-15872. A kind change
/// (status -> kind, CameraChange.cpp 656-826) blends the record and sweeps eye and look-at in polar coordinates over the current kind's
/// Inbet<new> ticks (16 mostly). Not ported, logged: the in-house (OOB) magnifier, the roof check (Monte village only: a dummy roof
/// everywhere else), the camera cubes / fixed cameras (pass 54: an area's kind by a tag), the notice camera (no bosses), the 0x8000
/// camera-clip surface flag (every static body clips the camera; the metadata `sms_camera_pass` on a body opts it out), SE 0x4826.
///
/// THE L (Y) CAMERA, CAMERA_MODE_L_NORMAL, is a mode of this same camera (pass 68 folded assets/Sms/MarioYCam.cs's second Camera3D into
/// the rig, as CPolarSubCamera has it): a Y in a normal kind starts execFrontRotate_'s swing behind him (the gap halved a tick,
/// YButtonRotateChase 2; the L press's variant closes a tenth, LButtonRotateChase 10); when it settles (CAMERA_FLAG_UNK8 -> UNK10)
/// doLButtonCameraOn_ changes the kind to LNormal with the current kind's Inbet (16 ticks), unk282 = 60 and the pitch ratio where the
/// camera is level with its target (setUpToLButtonCamera_). In the mode ctrlLButtonCamera_ (CameraNotice.cpp 208) runs instead of
/// ctrlNormalOrTowerCamera_: the look target is the nozzle top (his look point without the FLUDD), the MAIN stick turns the yaw (300 BAM
/// a tick) and tilts the ratio (0.01 a tick), then the SAME calcPosAndAt_ -- the kind record's 200 u, OffsetLookatXZ -72 over the
/// shoulder, no cushion rule and no MinCushionXZ push (not a normal kind -- the push would put the eye at 300 u; the game keeps the record's
/// 200, see CalcPosAndAt), no slope aim, no wall check, no
/// ground check, the inbetween, the chase -- and the record's fov 60 / near 10 blend in with the rest. Y / A / B (the pad's
/// MEANING_Y | MEANING_CAM_AB, once unk282 is out and the inbetween over) sweep back over the L record's Inbet (16) into the kind the
/// L camera came from (changeCamMode_(-1) pops it; the saved ratio comes back); a knockdown, the water, the hover / rocket, the dash, a
/// climb or a demo's hold cut it in one tick (the hover's cut sweeps by Jaysonn's logged deviation, SmsParams.LCamera.RocketExitSweeps).
/// Mario's half of the mode stays in MarioYCam.cs and reads IsLCamera / FinalYawBam / CurPitchBam here: his stick is the camera's, A / B / Y
/// its buttons, his face the eye's bearing (playerControl 2449), the gun angle its pitch (WaterGun.cpp 219). This node sits ON the
/// look-at (mTarget) and the pivot's yaw is the eye's bearing (Mario's stick reads both). The scene rig (CamController > SpringArmPivot >
/// SpringArm3D > Camera3D) is kept; the SpringArm3D no longer processes (it would re-place the camera each physics frame) and the
/// Camera3D is posed directly. Mode TalkingPOV is Jaysonn's sign-dialogue framing (RedCoinSwitch.cs) and stays his.
/// </summary>
public partial class SunshineCamera : Node3D
{
    public enum CamMode
    {
        Normal,      // the game's camera
        TalkingPOV,  // reading a sign / switch dialogue: Mario frozen, held side-on medium shot (his)
    }

    [Export] public CamMode Mode { get; set; } = CamMode.Normal;

    // ============================================================ wiring (Player.tscn's paths)
    [Export] public NodePath TargetPath = new("..");                    // Mario
    [Export] public NodePath ArmaturePath = new("../Armature");         // his facing, for the TalkingPOV shot
    [Export] public NodePath PivotPath = new("SpringArmPivot");
    [Export] public NodePath SpringArmPath = new("SpringArmPivot/SpringArm3D");
    [Export] public NodePath CameraPath = new("SpringArmPivot/SpringArm3D/Camera3D");

    // ============================================================ inputs
    [Export] public string RStickLeft = "Rstick_left";
    [Export] public string RStickRight = "Rstick_right";
    [Export] public string RStickUp = "Rstick_up";
    [Export] public string RStickDown = "Rstick_down";
    [Export] public string LButtonAction = "button_l";   // MEANING_CAM_L: the press swings the camera behind him (execFrontRotate_)

    // ============================================================ the game's camera
    /// <summary>The stage area's kind (what a camera cube would assign; CameraCodeControl.cpp). FOLLOW is the game's default when no cube
    /// applies (CameraChange.cpp 823); a Delfino Plaza stage uses DELFINO. A later pass will let an Area3D tag override it per area.</summary>
    [Export] public SmsCamKind DefaultKind = SmsCamKind.Follow;
    /// <summary>Delfino Plaza (map 7) rules: no SWIMMING / AQUATIC_TURBO kinds there (CameraChange.cpp 675-683).</summary>
    [Export] public bool DelfinoPlazaRules = false;
    // ============================================================ TalkingPOV (sign / switch dialogue) -- his shot
    [Export] public Vector3 TalkingPOVLocalOffset = new(-0.60f, 1.20f, -0.10f);
    [Export] public float TalkingPOVYawOffsetDeg = -32f;
    [Export] public float TalkingPOVPitchDeg = 0f;
    [Export] public float TalkingPOVRadius = 1.0f;
    [Export] public float TalkingPOVEntryDuration = 0.45f;
    [Export] public float FollowSmooth = 8f;        // the shot's look-at follow (per second)
    [Export] public float YawChaseRate = 6f;        // the shot's settled chase rates (per second)
    [Export] public float PitchChaseRate = 5f;
    [Export] public float RadiusChaseRate = 6f;
    [Export] public float YawSnapRate = 18f;        // the shot's entry rates
    [Export] public float PitchSnapRate = 14f;
    [Export] public bool AutoScaleToTargetHeight = true;
    [Export] public float ReferenceTargetHeight = 1.80f;

    // ============================================================ nodes
    private Node3D _target;
    private Mario _mario;
    private Node3D _armature;
    private Node3D _pivot;
    private SpringArm3D _springArm;
    private Camera3D _camera;
    private float _rigScale = 1f;
    private uint _mask = 1;
    private Rid _marioRid;
    private bool _hasMarioRid = false;
    private readonly CylinderShape3D _wallShape = new CylinderShape3D();

    // ============================================================ the game's state (units, BAM)
    /// <summary>TTargetCamera (Camera.hpp 26): Pos = mPosition (the panned eye), At = mTarget (the look point), Desired = unk18 (the polar
    /// solve's eye), Pitch / Yaw = mPitch / mYaw, Ratio = unk28, SavedRatio = unk30.</summary>
    private struct TargetCam
    {
        public Vector3 Pos, At, Desired;
        public int Pitch, Yaw;
        public float Ratio, SavedRatio;
    }

    private TargetCam _cur, _prev;                       // mCurrentTarget / mPreviousTarget
    private Vector3 _posU, _atU;                         // mPosition / mTarget: the chased camera
    private Vector3 _viewPosU, _viewAtU;                 // unk124 / unk148: the view after calcFinalPosAndAt_
    private Vector3 _lookPoint;                          // gpCameraMario->unk0: his origin + the look height
    private readonly SmsCamKindParam _p = new SmsCamKindParam(); // mCurrentParams
    private SmsCamKind _mode = SmsCamKind.Follow, _prevMode = SmsCamKind.Follow;
    private SmsCamKind? _areaKind;                       // controlByCameraCode_: the camera cube Mario + 75 u stands in, if any (pass 57)
    private PhysicsPointQueryParameters3D _areaQuery;
    private int _flags = 0;                              // unk64
    private const int FlagPanA = 0x1, FlagPanB = 0x2, FlagFrontRotate = 0x4, FlagFrontY = 0x8, FlagFrontDone = 0x10, FlagFreeze = 0x40, FlagStickYaw = 0x80, FlagCushionFar = 0x100;
    private int _frontYaw = 0, _frontChase = 0;          // unk274 / unk276
    private int _lLock = 0;                              // unk282: ticks after an L camera change before Y / A / B act or a Y re-enters (60)
    private SmsCamKind _lModeBefore = SmsCamKind.Follow; // unk60's stack: the kind the L camera came from (changeCamMode_(-1) pops it)
    private int _groundSkip = 0, _roofSkip = 0, _autoChaseOff = 0; // unk278 / unk27A / unk284
    private float _autoChase = 1f;                       // unk288
    private int _slopeX = 0, _addY = 0;                  // unk28C / unk28E
    private float _secureX = 0f, _secureZ = 0f;          // unk294 / unk298
    private int _holdAngle = 0;                          // unk2AC->unk0: the squat aim's pitch offset
    private float _holdDist = 0f, _holdRatio = 0f, _holdTarget = 0f; // unk2AC->unk4 / unk8 / unkC
    private float _followSpeed = 0f;                     // unk250
    private int _finalPitch = 0, _finalYaw = 0;          // unk256 / unk258: the actual camera's polar angles
    private float _finalRatio = 0f;                      // unk270
    private float _panOffset = 0f;                       // mHeightPanOffset
    private int _posFreeze = 0, _targetFreeze = 0;       // mPosFreezeFrames / mTargetFreezeFrames
    private bool _occluded = false;                      // MARIO_FLAG_OCCLUDED: the game peeks the framebuffer at his screen point; here an eye -> Mario ray
    // TCameraInbetween
    private int _ibFrames = 0;                           // mFramesRemaining
    private float _ibPosR = 0f, _ibAtR = 0f;             // unk8 / unk10
    private int _ibPosPitch = 0, _ibPosYaw = 0, _ibAtPitch = 0, _ibAtYaw = 0; // unkC / unkE / unk14 / unk16
    private Vector3 _ibPos, _ibAt, _ibMario;             // mPos / mAt / unk30
    private float _ibApproach = 0f;                      // unk44: the move-approach distance (FIX_G only; always 0 here)
    // the frame
    private float _tickAccum = 0f;
    private float _cx = 0f, _cy = 0f;                    // the C-stick as the game reads it (mCompSPos[6], [7]: right and UP positive)
    private bool _started = false;
    private bool _warpRequested = false;
    private SmsCamMarioData _md;
    private float _dbgTimer = 0f;
    // TalkingPOV
    private float _tkYaw, _tkPitch, _tkRadius, _tkCurYaw, _tkCurPitch, _tkCurRadius, _tkTransition;
    private Vector3 _tkLookAt;

    private static float U(float m) => m * SmsUnits.UnitsPerMetre;
    private static Vector3 U(Vector3 m) => m * SmsUnits.UnitsPerMetre;
    private static float M(float u) => u / SmsUnits.UnitsPerMetre;
    private static Vector3 M(Vector3 u) => u / SmsUnits.UnitsPerMetre;

    /// <summary>Mario's visual facing yaw (the armature's): the shine demo and the TalkingPOV shot use it.</summary>
    public float CurrentVisualYaw => GetVisualYaw();

    // ============================================================ setup
    public override void _Ready()
    {
        TopLevel = true;
        _target = GetNodeOrNull<Node3D>(TargetPath);
        _mario = _target as Mario;
        _armature = GetNodeOrNull<Node3D>(ArmaturePath);
        _pivot = GetNodeOrNull<Node3D>(PivotPath);
        _springArm = GetNodeOrNull<SpringArm3D>(SpringArmPath);
        _camera = GetNodeOrNull<Camera3D>(CameraPath);
        if (_target == null) GD.PushError("[SMS camera] Target not found.");
        if (_mario == null) GD.PushError("[SMS camera] The target is not Mario: the game's camera reads his status.");
        if (_pivot == null) GD.PushError("[SMS camera] Pivot not found.");
        if (_springArm == null) GD.PushError("[SMS camera] SpringArm not found.");
        if (_camera == null) GD.PushError("[SMS camera] Camera3D not found.");
        if (_springArm != null)
        {
            // the arm's own physics step re-placed its children every frame (process_spring); the game's camera is posed here instead
            _springArm.SetPhysicsProcessInternal(false);
            _mask = _springArm.CollisionMask;
            _springArm.Position = Vector3.Zero;
            _springArm.Rotation = Vector3.Zero;
        }
        if (_pivot != null) _pivot.Position = Vector3.Zero;
        if (_camera != null) { _camera.Position = Vector3.Zero; _camera.Rotation = Vector3.Zero; }
        if (_target is CollisionObject3D co) { _marioRid = co.GetRid(); _hasMarioRid = true; }
        if (AutoScaleToTargetHeight) RecomputeRigScale();
        _wallShape.Radius = M(SmsCamNormal.WallCheckRadius);
        _wallShape.Height = M(20f); // the game's wall record is a horizontal probe at the eye's XZ: a thin disc keeps the floor (144 u down) out of it
        // the initial pose (loadAfter) is taken on the first physics frame: Mario's own _Ready runs AFTER his children's
    }

    // ============================================================ the frame
    public override void _PhysicsProcess(double delta)
    {
        if (_target == null || _camera == null || _pivot == null || _mario == null) return;
        float dt = (float)delta;
        _md = _mario.SmsCameraData(delta);
        if (!_started)
        {
            LoadAfter();
            _started = true;
        }
        if (Mode == CamMode.TalkingPOV)
        {
            TickTalking(dt);
            return;
        }
        if (_warpRequested || _md.Teleported)
            WarpBehind();
        // the C-stick through the pad library (PADClamp's sub-stick region 59 / 31 / 15 and JUTGamePad's 42): mCompSPos[6] = x, [7] = y (up +)
        Vector2 sub = SmsStick.PadSubStick(Input.GetVector(RStickLeft, RStickRight, RStickUp, RStickDown));
        _cx = sub.X;
        _cy = -sub.Y;
        bool lEdge = InputMap.HasAction(LButtonAction) && Input.IsActionJustPressed(LButtonAction);
        int n = SmsUnits.TicksFor(delta, ref _tickAccum);
        for (int i = 0; i < n; i++)
            Tick(lEdge && i == 0);
        Apply();
        if (_md.Debug)
        {
            _dbgTimer += dt;
            if (_dbgTimer >= 1f)
            {
                _dbgTimer = 0f;
                GD.Print($"[SMS camera] {_mode}{(_ibFrames > 0 ? $" (blend {_ibFrames})" : "")}: yaw {SmsMath.WrapAngle(_finalYaw * SmsUnits.BamToRad) * 180f / MathF.PI:F0} deg, pitch {_finalPitch * 180f / 32768f:F1} deg (ratio {_cur.Ratio:F2}), {(_posU - _atU).Length():F0} u off the look-at, look {_atU.Y - _md.PosU.Y:F0} u over his feet, eye {_posU.Y - _md.PosU.Y:F0} u; fov {_p.Fovy:F0}; stick ({_cx:F2}, {_cy:F2}){(_areaKind != null ? $", area {_areaKind}" : "")}{(_occluded ? ", occluded" : "")}{((_flags & FlagFrontRotate) != 0 ? ((_flags & FlagFrontY) != 0 ? ", the Y swing" : ", L swing") : "")}{(_lLock > 0 ? $", L lock {_lLock}" : "")}{(_md.ReadyGun ? $", squat aim {_md.AimRatio:F2} hold {_holdDist:F0} u" : "")}");
            }
        }
    }

    /// <summary>CPolarSubCamera::loadAfter (cameragc.cpp 134): the kind record, the ratio at XRotStart, the yaw behind him, the eye at
    /// the kind's near distance about the look point, everything else parked there.</summary>
    private void LoadAfter()
    {
        _mode = _prevMode = DefaultKind == SmsCamKind.LNormal ? SmsCamKind.Follow : DefaultKind;
        _p.CopyFrom(SmsCamKinds.Get(_mode));
        _cur = default;
        _cur.Ratio = Mathf.Clamp(SmsCamNormal.XRotStart, 0f, 1f);
        _cur.SavedRatio = _cur.Ratio;
        _cur.Yaw = (short)(_md.AngleY - 0x8000);
        PlaceBehind();
        if (_md.Debug)
            GD.Print($"[SMS camera] the game's normal camera: kind {_mode} (/Camera/kind{_mode.ToString().ToLower()}.prm), {SmsCamKinds.Count} kinds carried; {_p.DistMin:F0}..{_p.DistMax:F0} u off the look-at, pitch {_p.XAngleMin * 180f / 32768f:F1}..{_p.XAngleMax * 180f / 32768f:F1} deg, look {_p.AtOffsetY:F0}+{_p.XRotRatioAtOffsetY:F0} u over his feet, fov {_p.Fovy:F0}; the stick turns {_p.YAngleManualSpeedXMin * SmsUnits.TicksPerSecond * 360f / 65536f:F0} deg/s, tilts the range in {1f / (_p.XRotRatioManualSpeed * SmsUnits.TicksPerSecond):F2} s; eye and look-at chase at {_p.PosChaseRateXZ:F2} / {_p.AtChaseRateXZ:F2} a tick");
    }

    /// <summary>The eye parked at the kind's polar position about the look point, no lag anywhere (loadAfter's tail; a stage load).</summary>
    private void PlaceBehind()
    {
        _finalRatio = _cur.Ratio;
        _cur.Pitch = LerpS(_p.XAngleMin, _p.XAngleMax, _cur.Ratio);
        float yOff = _cur.Ratio * _p.XRotRatioAtOffsetY + _p.AtOffsetY;
        _lookPoint = _md.PosU + new Vector3(0f, yOff, 0f);
        _cur.At = _lookPoint;
        float dist = LerpF(_p.DistMin, _p.DistMax, _cur.Ratio);
        Vector3 pos = PolarToCross(_cur.At, dist, _cur.Pitch, _cur.Yaw);
        CalcSecureViewTarget(_cur.Yaw, out _secureX, out _secureZ);
        pos.X += _secureX; pos.Z += _secureZ;
        _cur.At.X += _secureX; _cur.At.Z += _secureZ;
        _cur.Pos = _cur.Desired = pos;
        _posU = _viewPosU = pos;
        _atU = _viewAtU = _cur.At;
        _finalPitch = _cur.Pitch;
        _finalYaw = _cur.Yaw;
        _prev = _cur;
        _ibFrames = 0;
        _ibApproach = 0f;
        _ibPos = pos;
        _ibAt = _cur.At;
        _ibMario = _md.PosU;
        IbCalcPolar();
        _flags = 0;
        _groundSkip = _roofSkip = _autoChaseOff = 0;
        _autoChase = 1f;
        _slopeX = _addY = 0;
        _panOffset = 0f;
        _posFreeze = _targetFreeze = 0;
        HoldReset();
        _occluded = false;
        Apply();
    }

    /// <summary>A respawn / the intro's spawn / a teleport: the game reloads the stage and the camera starts behind him (loadAfter); the
    /// kind and the ratio are kept.</summary>
    private void WarpBehind()
    {
        _warpRequested = false;
        DropLCamera(); // a stage load's camera is the normal one, no lock
        _cur.Yaw = (short)(_md.AngleY - 0x8000);
        PlaceBehind();
        if (_md.Debug)
            GD.Print("[SMS camera] warped behind him (a stage load's start)");
    }

    /// <summary>For Mario: Respawn / RevealFromIntro -- the camera starts behind him as a stage load's does.</summary>
    public void SmsWarpBehind() => _warpRequested = true;

    /// <summary>The scene takes the frame: this node on the look-at, the pivot's yaw the eye's bearing (Mario's stick direction), the
    /// Camera3D posed on the view with the kind's fov and near clip.</summary>
    private void Apply()
    {
        Vector3 eyeM = M(_viewPosU), atM = M(_viewAtU);
        GlobalPosition = M(_atU);
        _pivot.GlobalRotation = new Vector3(0f, _finalYaw * SmsUnits.BamToRad, 0f); // global: Mario.cs rotates the stick by the body's basis and then by this yaw
        Vector3 dir = atM - eyeM;
        if (dir.LengthSquared() > 1e-10f)
        {
            Vector3 up = MathF.Abs(dir.Normalized().Y) > 0.999f ? _camera.GlobalBasis.Y : Vector3.Up;
            _camera.GlobalTransform = new Transform3D(Basis.LookingAt(dir, up), eyeM);
        }
        _camera.Fov = _p.Fovy;
        _camera.Near = M(_p.NearClip);
    }

    // ============================================================ cameralib
    private static float Sin(int bam) => MathF.Sin(bam * SmsUnits.BamToRad);
    private static float Cos(int bam) => MathF.Cos(bam * SmsUnits.BamToRad);
    /// <summary>matan(a, b): the s16 BAM angle whose adjacent is a and opposite b (the decomp's matan(dz, dx) is the bearing from +Z toward +X).</summary>
    private static int Matan(float a, float b) => (short)(int)MathF.Round(MathF.Atan2(b, a) / SmsUnits.BamToRad);
    private static int RoundS16(float v) => (short)(int)(v + (v > 0f ? 0.5f : -0.5f)); // CLBRoundf<s16>
    private static float LerpF(float a, float b, float f) => a + f * (b - a);        // CLBLinearInbetween<f32>
    private static int LerpS(int a, int b, float f) => (short)(int)(a + f * (b - a)); // CLBLinearInbetween<s16>: the cast truncates
    private static float CalcRatio(float a, float b, float c) => a != b ? (c - a) / (b - a) : 0f;
    private static float EaseIn(float a, float b, float t) => a + (b - a) * t * t;   // CLBEaseInInbetween = CLBTwoDegreeGeneralInbetween(a, b, t, b - a)

    /// <summary>CLBPolarToCross: origin + r (cos v sin h, sin v, cos v cos h).</summary>
    private static Vector3 PolarToCross(Vector3 o, float r, int v, int h)
        => o + new Vector3(r * Cos(v) * Sin(h), r * Sin(v), r * Cos(v) * Cos(h));

    /// <summary>CLBCrossToPolar: the radius, the elevation and the bearing of p about o.</summary>
    private static void CrossToPolar(Vector3 o, Vector3 p, out float r, out int pitch, out int yaw)
    {
        Vector3 d = p - o;
        r = d.Length();
        pitch = Matan(MathF.Sqrt(d.X * d.X + d.Z * d.Z), d.Y);
        yaw = Matan(d.Z, d.X);
    }

    /// <summary>CLBChaseDecrease: value closes ratio (at most 1) of its gap; true while more than threshold remains.</summary>
    private static bool ChaseDecrease(ref float v, float desired, float ratio, float threshold = 0f)
    {
        if (ratio > 1f) ratio = 1f;
        v += ratio * (desired - v);
        return MathF.Abs(v - desired) > MathF.Abs(threshold);
    }

    /// <summary>CLBChaseGeneralConstantSpecifySpeed&lt;f32&gt;: a constant step, landing exactly; true while more is needed.</summary>
    private static bool ChaseConstSpeed(ref float v, float desired, float speed)
    {
        float delta = desired - v;
        if (speed < 0f) speed = -speed;
        if (delta > 0f) { delta -= speed; v = delta > 0f ? desired - delta : desired; }
        else { delta += speed; v = delta < 0f ? desired - delta : desired; }
        return v != desired;
    }

    /// <summary>CLBChaseGeneralConstantSpecifySpeed&lt;s16&gt;: the same in s16 arithmetic -- the gap wraps, so an angle takes the short way round.</summary>
    private static bool ChaseConstSpeed(ref int v, int desired, int speed)
    {
        int delta = (short)(desired - v);
        if (speed < 0) speed = -speed;
        if (delta > 0) { delta -= speed; v = delta > 0 ? (short)(desired - delta) : (short)desired; }
        else { delta += speed; v = delta < 0 ? (short)(desired - delta) : (short)desired; }
        return v != (short)desired;
    }

    /// <summary>CLBChaseSpecialDecrease: a proportional step, never slower than speed.</summary>
    private static bool ChaseSpecialDecrease(ref float v, float desired, float ratio, float speed)
    {
        if (ratio > 1f) ratio = 1f;
        float actual = ratio * (desired - v);
        if (MathF.Abs(actual) < MathF.Abs(speed)) actual = speed;
        return ChaseConstSpeed(ref v, desired, actual);
    }

    /// <summary>CLBChaseConstantSpecifyFrame&lt;f32&gt;: 1/frames of the gap a call, the last call landing.</summary>
    private static void ChaseFrame(ref float v, float desired, float frames)
    {
        if (frames < 0.001f) { v = desired; return; }
        v += (1f / frames) * (desired - v);
    }

    /// <summary>CLBChaseConstantSpecifyFrame(s16*): the same on an angle, rounded, the gap wrapped.</summary>
    private static void ChaseFrame(ref int v, int desired, float frames)
    {
        if (frames < 0.001f) { v = (short)desired; return; }
        int delta = (short)(desired - v);
        v = (short)(v + RoundS16((1f / frames) * delta));
    }

    /// <summary>CLBRevisionLookatByAngleX: the look-at moved so its elevation from the eye stays within the limits.</summary>
    private static void RevisionLookatByAngleX(int vMin, int vMax, Vector3 origin, ref Vector3 inOut)
    {
        CrossToPolar(origin, inOut, out float r, out int v, out int h);
        v = Mathf.Clamp(v, vMin, vMax);
        inOut = PolarToCross(origin, r, v, h);
    }

    private float GetVisualYaw()
    {
        if (_armature == null) return _finalYaw * SmsUnits.BamToRad;
        return _armature.GlobalRotation.Y; // the model faces the armature's -Z (pass 54)
    }

    private void RecomputeRigScale()
    {
        _rigScale = 1f;
        if (_target == null) return;
        var cs = _target.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        float total = 0f;
        if (cs?.Shape is CapsuleShape3D cap) total = cap.Height + 2f * cap.Radius;
        else if (cs?.Shape is CylinderShape3D cyl) total = cyl.Height;
        if (total > 0.01f)
        {
            float sy = _target.GlobalTransform.Basis.Scale.Y;
            _rigScale = total * Mathf.Max(0.01f, sy) / Mathf.Max(0.01f, ReferenceTargetHeight);
        }
    }

    // ============================================================ one 120 Hz tick: ctrlGameCamera_ + calcFinalPosAndAt_ + calcExternalData_
    private void Tick(bool lEdge)
    {
        if ((_flags & FlagStickYaw) == 0 && _autoChaseOff > 0) _autoChaseOff--;
        if (_groundSkip != 0) _groundSkip--;
        if (_roofSkip != 0) _roofSkip--;
        if (_lLock != 0) _lLock--;             // unk282
        // setMarioLookat_ (cameragc.cpp 269): his origin + the kind's look height (+260 on KICK_ROOF_ROLL_UP)
        float yOff = _cur.Ratio * _p.XRotRatioAtOffsetY + _p.AtOffsetY;
        if (_md.KickRoofRollUp) yOff += 260f;
        _lookPoint = _md.PosU + new Vector3(0f, yOff, 0f);
        _prev = _cur;
        // controlByCameraCode_ + execCameraModeChangeProc_: only while the game is in play (director state 4), never under a demo's hold
        _areaKind = SampleAreaKind();
        if (_md.Locked)
        {
            // a demo's hold (the talk, the shine, a death, CameraLocked): the game never reaches execCameraModeChangeProc_ under one and
            // its demo camera takes the view; here the one camera keeps the frame, so an open L camera is cut and a Y swing dropped
            if (_mode == SmsCamKind.LNormal) LCameraOff(true, "a demo hold", force: true);
            _flags &= ~(FlagFrontRotate | FlagFrontY | FlagFrontDone);
        }
        else ModeChangeProc(lEdge);
        // the kind record: the mode's, blended over the inbetween (inbetweenData with the frames remaining)
        SmsCamKindParam target = SmsCamKinds.Get(_mode);
        if (_ibFrames > 0) _p.Inbetween(target, _ibFrames);
        else _p.CopyFrom(target);
        if (_autoChaseOff > _p.AutoChaseCompleteFrame) _autoChaseOff = _p.AutoChaseCompleteFrame;
        // occlusion (MARIO_FLAG_OCCLUDED): the game reads the framebuffer at his screen point; a ray from the eye to his waist stands in
        _occluded = RayHits(M(_posU), M(_md.PosU + new Vector3(0f, 80f, 0f)));
        if (_mode == SmsCamKind.LNormal) CtrlL();
        else CtrlNormal();
        // calcFinalPosAndAt_ (828): the shake is SmsCameraShake's own pass; the look-at's elevation from the eye clamped to the limits
        _viewPosU = _posU;
        _viewAtU = _atU;
        RevisionLookatByAngleX(SmsCamNormal.LimitMinAngleX, SmsCamNormal.LimitMaxAngleX, _viewPosU, ref _viewAtU);
        // calcExternalData_ (857): the actual camera's polar angles (unk256 / unk258) and ratio (unk270)
        CrossToPolar(_atU, _posU, out _, out _finalPitch, out _finalYaw);
        _finalRatio = Mathf.Clamp(CalcRatio(_p.XAngleMin, _p.XAngleMax, _finalPitch), 0f, 1f);
    }

    /// <summary>execCameraModeChangeProc_ (CameraChange.cpp 568) for the kinds carried. The refusing states (asleep, the water, the hover /
    /// rocket, the dash, a climb; 588) cut an open L camera in a tick and let only the L press swing; in the L camera a knockdown (status bit
    /// 0x20000) cuts it and Y / A / B (MEANING_Y | MEANING_CAM_AB, unk282 out, the inbetween over) sweep out of it; in a normal kind a settled
    /// Y swing (CAMERA_FLAG_UNK10) turns the L camera on, and a Y (unk282 out, his status allowing) or an L press starts execFrontRotate_'s
    /// swing (never in the hip drop). Then, unless the kind changed here or the L camera holds, the status ladder (656-826) names the
    /// kind and changeCamMode_ blends into it. The notice camera (execNoticeOnOffProc_: a boss to lock on to) is not ported.</summary>
    private void ModeChangeProc(bool lEdge)
    {
        SmsCamKind before = _mode;
        bool inL = _mode == SmsCamKind.LNormal;
        bool refuses = _md.InWater || _md.Swimming || _md.Rocketing || _md.Dashing || _md.Climbing;
        if (refuses)
        {
            if (inL)
                LCameraOff(true, _md.Rocketing ? "the hover / rocket" : "the water / the dash / a climb", sweepAnyway: _md.Rocketing && SmsParams.LCamera.RocketExitSweeps);
            if (lEdge && !_md.HipDrop && (_md.TouchGround || _md.WallJumpOrSlide))
                ExecFrontRotate(false);
        }
        else if (inL)
        {
            if (_md.Downed)
                LCameraOff(true, "knocked down");
            else if (!IsLInbetween() && _md.CamCancelPressed && _lLock == 0)
                LCameraOff(false, "Y / A / B");
        }
        else if (SmsCamKinds.IsNormalClass(_mode))
        {
            if ((_flags & FlagFrontDone) != 0)
            {
                _flags &= ~FlagFrontDone;
                LCameraOn(); // the Y swing has settled behind him
            }
            else if (_md.YPressed || lEdge)
            {
                bool doCheck = true;
                if (_md.YPressed && (_lLock != 0 || !_md.YCamCanEnter))
                    doCheck = false; // unk282 running, or a status of his the port keeps the Y out of (MarioYCam.SmsYCamCanEnter)
                if (doCheck && !IsLInbetween() && !_md.HipDrop)
                {
                    // execFrontRotate_ 447: refused in the hip drop; an air L press is the hip drop's input (pass 17's rule), a Y in the air is the game's
                    if (_md.YPressed)
                        ExecFrontRotate(true);
                    else if (_md.TouchGround || _md.WallJumpOrSlide)
                        ExecFrontRotate(false);
                }
            }
        }
        if (_mode != before || _mode == SmsCamKind.LNormal)
            return; // prevMode != mMode -> return; isLButtonCameraSpecifyMode(mMode) -> return: the L camera's own exits, the kind holds
        ChangeMode(SelectKind());
    }

    /// <summary>controlByCameraCode_ (CameraCodeControl.cpp 9): Mario's position + 75 units against the stage's camera cubes, the first hit's
    /// code. Here the Area3Ds at that point: an SmsCameraArea's Kind (the highest Priority where they overlap), else any Area3D's metadata
    /// `sms_camera_kind` (a kind's name). A cube's camera map tool (a fixed / definite camera) is not ported.</summary>
    private SmsCamKind? SampleAreaKind()
    {
        _areaQuery ??= new PhysicsPointQueryParameters3D { CollideWithAreas = true, CollideWithBodies = false, CollisionMask = uint.MaxValue };
        _areaQuery.Position = M(_md.PosU + new Vector3(0f, 75f, 0f));
        var hits = GetWorld3D().DirectSpaceState.IntersectPoint(_areaQuery, 8);
        SmsCameraArea best = null;
        SmsCamKind? tagged = null;
        foreach (Dictionary h in hits)
        {
            if (h["collider"].AsGodotObject() is not Node n)
                continue;
            if (n is SmsCameraArea a)
            {
                if (best == null || a.Priority > best.Priority)
                    best = a;
            }
            else if (tagged == null && n.HasMeta("sms_camera_kind") && System.Enum.TryParse(n.GetMeta("sms_camera_kind").AsString(), true, out SmsCamKind k))
                tagged = k;
        }
        return best != null ? best.Kind : tagged;
    }

    /// <summary>The ladder (CameraChange.cpp 656-826) with the stage's area kind as param_1. Of the game's branches, the port has no
    /// helmet / underground maps, surf, rails, slider / mirror / indoor ground types, bars (CLIMB), held objects, platforms or
    /// force jumps; those fall through as they would with their flags clear. Wires (kindWire / kindWireHang) ARE ported (pass 66y).</summary>
    private SmsCamKind SelectKind()
    {
        bool delfino = DelfinoPlazaRules; // currentMap == 7
        SmsCamKind area = _areaKind ?? DefaultKind; // the cube's code, else the stage's own kind (code -1 -> mInitialMode)
        if (area == SmsCamKind.LNormal)
            area = SmsCamKind.Follow;
        if (_md.OnWireTop)
            return SmsCamKind.Wire;      // on a wire overrides the rest: he cannot dash/swim/fence/etc. while on it, and a stage camera cube must not steal the wire view
        if (_md.OnWireHang)
            return SmsCamKind.WireHang;
        if (_md.Dashing)
            return !delfino && (_md.Swimming || _md.SwimJump) ? SmsCamKind.AquaticTurbo : SmsCamKind.Turbo;
        if (!delfino && _md.Swimming)
            return SmsCamKind.Swimming;
        if (_md.FenceFamily)
            return SmsCamKind.Fence;
        if (_md.Rocketing)
            return _md.HoverNozzle ? SmsCamKind.Hovering : SmsCamKind.RocketJump;
        if (_md.RoofFamily && !_md.KickRoofRollUp)
            return SmsCamKind.Hang;
        if (_md.HipDrop)
            return IsOverHipAttackKind(area) ? area : SmsCamKind.HipAttack;
        if (area != SmsCamKind.Follow)
            return area;   // a valid non-follow area kind beats the wall jump and the climb (785-787)
        if (_md.WallJumpOrSlide)
            return SmsCamKind.WallJump;
        return SmsCamKind.Follow;
    }

    /// <summary>isOverHipAttackSpecifyMode: the area kinds that keep showing through a hip drop (of the kinds carried: Indoor, Delfino, LookDown).</summary>
    private static bool IsOverHipAttackKind(SmsCamKind k) => k == SmsCamKind.Indoor || k == SmsCamKind.Delfino || k == SmsCamKind.LookDown;

    /// <summary>changeCamMode_ -> changeCamModeSpecifyFrame_ -> changeCamModeSub_ (CameraChange.cpp 272): the blend's length is the CURRENT
    /// kind's Inbet&lt;new&gt; (0 -> 1), the inbetween starts from the current pose, the freeze counters and the move approach clear, the height
    /// pan dies for the kinds without one (and leaving HANG), the squat-aim hold resets. frames &lt; 0 = the table's (changeCamMode_);
    /// given = changeCamModeSpecifyFrame_'s.</summary>
    private void ChangeMode(SmsCamKind to, int frames = -1)
    {
        if (to == _mode) return;
        if (frames < 0) frames = SmsCamKinds.InbetFrames(_mode, to);
        if (frames < 0) return;
        if (frames == 0) frames = 1;
        _prevMode = _mode;
        IbStart(frames);
        _mode = to;
        _posFreeze = _targetFreeze = 0;
        _ibApproach = 0f;   // offMoveApproach_ (the FIX_G exception has no fixed cameras to come from here)
        KillHeightPanWhenChangeCamMode();
        HoldReset();
        if (_md.Debug)
            GD.Print($"[SMS camera] kind {_prevMode} -> {_mode} over {frames} ticks");
    }

    /// <summary>execFrontRotate_ (CameraChange.cpp 447): the yaw chases behind his face (unk274 = mFaceAngle.y - 0x8000) with the eye
    /// following at once (chaseXZ 1 under CAMERA_FLAG_UNK4), the stick and the auto chase skipped meanwhile. MEANING_Y: YButtonRotateChase
    /// (2: the gap halved a tick, 180 deg in 16) and CAMERA_FLAG_UNK8, so the settled swing turns the L camera on; MEANING_CAM_L:
    /// LButtonRotateChase (10: a tenth a tick) and no more. SE 0x4826 not ported.</summary>
    private void ExecFrontRotate(bool yPress)
    {
        bool fresh = (_flags & FlagFrontRotate) == 0; // the pad's frame meaning holds for every tick of its frame (the game re-arms the swing each; one line is enough)
        _flags &= ~FlagFrontDone;
        _flags |= FlagFrontRotate;
        _frontYaw = (short)(_md.AngleY - 0x8000);
        if (yPress)
        {
            _frontChase = SmsCamNormal.YButtonRotateChase;
            _flags |= FlagFrontY;
            if (_md.Debug && fresh)
                GD.Print($"[SMS camera] Y: the camera swings behind him ({MathF.Abs((short)(_frontYaw - _cur.Yaw)) * 360f / 65536f:F0} deg to go, the gap halved each tick), then the L camera");
        }
        else
        {
            _frontChase = SmsCamNormal.LButtonRotateChase;
            _flags &= ~FlagFrontY;
        }
    }

    // ============================================================ the L (Y) camera: CAMERA_MODE_L_NORMAL as a mode of this camera (pass 68)
    /// <summary>isLButtonCameraInbetween: the blend into or out of the L camera is running.</summary>
    private bool IsLInbetween() => _ibFrames > 0 && (_mode == SmsCamKind.LNormal || _prevMode == SmsCamKind.LNormal);

    /// <summary>doLButtonCameraOn_ (CameraChange.cpp 469) -> changeCamMode_(L_NORMAL) -> changeCamModeSub_ (272) with setUpToLButtonCamera_
    /// (256): unk282 = 60; the kind is pushed (unk60); the ratio is saved (unk30) and set where the pitch is -OffsetAngleX, the camera
    /// level with its target; the current kind's Inbet&lt;LNormal&gt; (16) sweeps from the swing's pose. SE 0x4824 not ported. Mario's half
    /// sees IsLCamera from this tick (onNeutralMarioKey is his: MarioYCam.cs).</summary>
    private void LCameraOn()
    {
        if (_mode == SmsCamKind.LNormal || IsLInbetween()) return;
        _lLock = SmsParams.LCamera.LockTicks;
        _lModeBefore = _mode;
        SmsCamKindParam lp = SmsCamKinds.Get(SmsCamKind.LNormal);
        _cur.SavedRatio = _cur.Ratio;
        _cur.Ratio = _prev.Ratio = Mathf.Clamp(CalcRatio(lp.XAngleMin, lp.XAngleMax, -lp.OffsetAngleX), 0f, 1f);
        ChangeMode(SmsCamKind.LNormal);
        if (_md.Debug)
            GD.Print($"[SMS camera] the L camera on: bearing {SmsMath.WrapAngle(_cur.Yaw * SmsUnits.BamToRad) * 180f / MathF.PI:F0} deg (behind him), pitch {LerpS(lp.XAngleMin, lp.XAngleMax, _cur.Ratio) * 180f / 32768f:F1} deg (ratio {_cur.Ratio:F2}), target {(_md.HasFludd ? "the nozzle top" : "his look point")}; the main stick turns {lp.YAngleManualSpeedXMin * SmsUnits.TicksPerSecond * 360f / 65536f:F0} deg/s, tilts the range in {1f / (lp.XRotRatioManualSpeed * SmsUnits.TicksPerSecond):F2} s; Y / A / B leave after {SmsParams.LCamera.LockTicks / SmsUnits.TicksPerSecond:F1} s");
    }

    /// <summary>doLButtonCameraOff_ (479), only with the L camera on and its inbetween over (isThing2): unk282 = 60; a cancel is
    /// changeCamMode_(-1) -- the pushed kind pops back with the L record's Inbet (16) -- a cut changeCamModeSpecifyFrame_(-1, 1);
    /// setUpFromLButtonCamera_ restores the saved ratio. The yaw stays the L camera's, which IS behind the way he now faces. SE 0x4825 not
    /// ported. force: a demo's hold cuts it whatever the inbetween (the one camera must not stay in the mode under a demo).</summary>
    private void LCameraOff(bool cut, string why, bool sweepAnyway = false, bool force = false)
    {
        if (_mode != SmsCamKind.LNormal) return;
        if (_ibFrames > 0 && !force) return;
        _lLock = SmsParams.LCamera.LockTicks;
        _cur.Ratio = _prev.Ratio = _cur.SavedRatio;
        SmsCamKind to = _lModeBefore;
        int frames = (!cut || sweepAnyway) ? SmsCamKinds.InbetFrames(SmsCamKind.LNormal, to) : 1; // sweepAnyway: the hover's cut sweeps by a logged deviation
        ChangeMode(to, frames);
        if (_md.Debug)
            GD.Print($"[SMS camera] the L camera off ({why}): {(frames > 1 ? $"the {frames}-tick sweep back" : "a cut")} into {to}, behind him; Y again after {SmsParams.LCamera.LockTicks / SmsUnits.TicksPerSecond:F1} s");
    }

    /// <summary>A stage load (the warp) or the talk shot taking the view: the L camera is simply gone, no blend, no lock.</summary>
    private void DropLCamera()
    {
        _lLock = 0;
        _flags &= ~(FlagFrontRotate | FlagFrontY | FlagFrontDone);
        if (_mode != SmsCamKind.LNormal) return;
        _prevMode = SmsCamKind.LNormal;
        _mode = _lModeBefore;
        _cur.Ratio = _prev.Ratio = _cur.SavedRatio;
        _ibFrames = 0;
    }

    /// <summary>ctrlLButtonCamera_ (CameraNotice.cpp 208), the L camera's tick: the look target is the nozzle top (getNozzleTopPos_: the
    /// nozzle matrix's origin + 30 units up its Y column) or, without the FLUDD, his look point (gpCameraMario->unk0); the MAIN stick turns
    /// the yaw (rotateY_ByStickX_ with -mCompSPos[4]) and tilts the ratio (rotateX_ByStickY_ with -mCompSPos[5]: up = the camera rises and
    /// looks down); then the same calcPosAndAt_ as the normal camera (the eye at the record's 200 u off the shifted look-at: no MinCushionXZ
    /// push for this kind, see there). The notice camera's branch (a boss) is not ported.</summary>
    private void CtrlL()
    {
        _followSpeed = 0f;
        if (_targetFreeze == 0)
            _cur.At = _md.HasFludd ? _md.NozzleTopU : _lookPoint;
        if (_posFreeze == 0)
        {
            RotateYByStickX(-_md.MainStick.X); // stickX = -mCompSPos[4] (the pad's main stick, right positive)
            RotateXByStickY(_md.MainStick.Y);  // stickY = -mCompSPos[5] (up positive in the pad; the port's vector is down positive)
        }
        CalcPosAndAt();
    }

    // ============================================================ what Mario's half of the L camera reads (MarioYCam.cs)
    /// <summary>isLButtonCamera(): the mode proper, from the tick it turns on (through its inbetween) to the tick it leaves.</summary>
    public bool IsLCamera => _mode == SmsCamKind.LNormal;
    /// <summary>unk258: the actual eye's bearing about the look-at (calcExternalData_), what playerControl faces him by (MarioMove.cpp 2449).</summary>
    public int FinalYawBam => _finalYaw;
    /// <summary>mCurrentTarget.mPitch: the L camera's pitch, the FLUDD gun angle in the mode (WaterGun.cpp 219).</summary>
    public int CurPitchBam => _cur.Pitch;
    /// <summary>getOffsetAngleY(): the current (blended) record's.</summary>
    public int OffsetAngleYBam => _p.OffsetAngleY;

    private void HoldReset()
    {
        _holdAngle = 0;
        _holdDist = _holdRatio = _holdTarget = 0f;
    }

    // ============================================================ TCameraInbetween (CameraInbetween.cpp)
    private void IbCalcPolar()
    {
        CrossToPolar(_ibAt, _ibPos, out _ibPosR, out _ibPosPitch, out _ibPosYaw);
        CrossToPolar(_ibMario, _ibAt, out _ibAtR, out _ibAtPitch, out _ibAtYaw);
    }

    /// <summary>startCameraInbetween: the sweep starts from the current inbetween pose.</summary>
    private void IbStart(int frames)
    {
        _ibFrames = frames;
        IbCalcPolar();
    }

    /// <summary>warpPosAndAt.</summary>
    private void IbWarp(Vector3 pos, Vector3 at)
    {
        _ibPos = pos;
        _ibAt = at;
        IbCalcPolar();
    }

    /// <summary>execCameraInbetween: the pose IS the target; while frames remain, the look-at's polar coordinates about Mario and the eye's
    /// about the look-at each close 1/remaining of their gap a tick (the look-at's height is the target's at once).</summary>
    private void IbExec(Vector3 pos, Vector3 at, Vector3 mario)
    {
        _ibPos = pos;
        _ibAt = at;
        if (_ibFrames > 0)
        {
            float rem = _ibFrames;
            if (_ibApproach != 0f)
                ChaseFrame(ref _ibApproach, 0f, rem);
            CrossToPolar(mario, at, out float ar, out int ap, out int ay);
            ChaseFrame(ref _ibAtR, ar, rem);
            ChaseFrame(ref _ibAtPitch, ap, rem);
            ChaseFrame(ref _ibAtYaw, ay, rem);
            if (MathF.Abs(mario.X - at.X) > 0.1f || MathF.Abs(mario.Z - at.Z) > 0.1f)
            {
                Vector3 tmp = PolarToCross(mario, _ibAtR, _ibAtPitch, _ibAtYaw);
                _ibAt.X = tmp.X;
                _ibAt.Z = tmp.Z;
            }
            CrossToPolar(_ibAt, pos, out float pr, out int pp, out int py);
            ChaseFrame(ref _ibPosR, pr, rem);
            ChaseFrame(ref _ibPosPitch, pp, rem);
            ChaseFrame(ref _ibPosYaw, py, rem);
            _ibPos = PolarToCross(_ibAt, _ibPosR, _ibPosPitch, _ibPosYaw);
            _ibFrames--;
        }
        _ibMario = mario;
    }

    private bool IsNormalCompletely() => SmsCamKinds.IsNormalClass(_mode) && (_ibFrames == 0 || SmsCamKinds.IsNormalClass(_prevMode));

    /// <summary>isNotHeightPanCamMode_ (Camera.hpp 215) for the kinds carried.</summary>
    private static bool IsNotHeightPanKind(SmsCamKind k)
        => k == SmsCamKind.LNormal || k == SmsCamKind.Hang || k == SmsCamKind.Hovering || k == SmsCamKind.Swimming || k == SmsCamKind.LookDown || k == SmsCamKind.WireHang;

    /// <summary>isSlopeCameraMode (CameraMode.cpp 307) for the kinds carried.</summary>
    private static bool IsSlopeKind(SmsCamKind k)
        => k == SmsCamKind.Follow || k == SmsCamKind.HipAttack || k == SmsCamKind.Indoor || k == SmsCamKind.Delfino || k == SmsCamKind.Turbo;

    // ============================================================ ctrlNormalOrTowerCamera_ (CameraNormal.cpp 68)
    private void CtrlNormal()
    {
        float cx = _cx, cy = _cy;
        _followSpeed = 0f;
        if (_targetFreeze == 0)
            _cur.At = _lookPoint;
        if (_posFreeze == 0)
        {
            if ((_flags & FlagFrontRotate) != 0)
            {
                // the L (or Y) swing: the yaw closes 1/chase of its gap to behind him each tick; done -> the Y path arms the L camera (not here)
                if (!SmsMath.ChaseAngleDecreaseS16(ref _cur.Yaw, _frontYaw, _frontChase))
                {
                    _flags &= ~FlagFrontRotate;
                    if ((_flags & FlagFrontY) != 0) { _flags &= ~FlagFrontY; _flags |= FlagFrontDone; }
                }
            }
            else if (cx != 0f)
            {
                RotateYByStickX(cx);
                _autoChaseOff = _p.AutoChaseCompleteFrame; // execInvalidAutoChase_
            }
            else
            {
                _followSpeed = _md.TouchGround
                    ? LerpF(_p.FollowSpeedXmin, _p.FollowSpeedXmax, _cur.Ratio)
                    : LerpF(_p.JumpFollowSpeedXmin, _p.JumpFollowSpeedXmax, _cur.Ratio);
                if (IsMomentDefinite())
                {
                    // a cushioned kind while he approaches: the eye holds its place, the yaw is wherever it already sits about the new look point
                    _cur.Yaw = Matan(_prev.Pos.Z - _cur.At.Z, _prev.Pos.X - _cur.At.X);
                }
                else if ((_flags & FlagStickYaw) == 0 && !(_md.ReadyGun && _md.CamL)) // isMarioCrabWalk_: the squat's L sidestep holds the yaw
                {
                    int behind = (short)(_md.AngleY - 0x8000);
                    if (_md.ReadyGun && _md.AimR)
                    {
                        // isMarioAimWithGun_: the squat aim chases behind him at a rate eased from AimAngleYChaseMin (60) at ratio 0
                        if (_autoChase != 0f)
                        {
                            float f = EaseIn(0f, 1f, _cur.Ratio);
                            int speed = (short)(int)EaseIn(SmsCamNormal.AimAngleYChaseMin, SmsCamNormal.AimAngleYChaseMax, f);
                            SmsMath.ChaseAngleDecreaseS16(ref _cur.Yaw, behind, speed);
                        }
                    }
                    else
                    {
                        // the auto follow: speed x how far off (HOVERING: linear; else (1 - cos 2d) / 2) x the magnifiers x his move x the ramp
                        float f30 = _mode == SmsCamKind.Hovering
                            ? MathF.Abs((short)(behind - _finalYaw)) * (2f / 65536f)
                            : (1f - Cos((short)((behind - _finalYaw) * 2))) * 0.5f;
                        float f29 = 1f; // the in-house (OOB) magnifier is not modelled: unk2CA stays -1
                        if (_occluded)
                            f29 = LerpF(_p.ObstructMaginfXmin, _p.ObstructMaginfXmax, _cur.Ratio);
                        if (_md.CamL)
                            f29 *= LerpF(_p.LFollowMaginfXmin, _p.LFollowMaginfXmax, _cur.Ratio);
                        float f4 = _mode == SmsCamKind.Hovering ? 100f : _md.MoveH;
                        float kek = _followSpeed * f30 * f29 * f4 * _autoChase;
                        if (kek > 32766.998f) kek = 32766.998f;
                        ChaseConstSpeed(ref _cur.Yaw, behind, RoundS16(kek));
                    }
                }
            }
            RotateXByStickY(cy);
        }
        CalcPosAndAt();
    }

    /// <summary>rotateY_ByStickX_ (cameragc.cpp 346): the stick times the kind's manual yaw speed, as an s16.</summary>
    private void RotateYByStickX(float stickX)
    {
        int thing = (short)(int)(stickX * LerpS(_p.YAngleManualSpeedXMin, _p.YAngleManualSpeedXMax, _cur.Ratio));
        _cur.Yaw = (short)(_cur.Yaw + thing);
    }

    /// <summary>rotateX_ByStickY_ (333): the ratio moves against the stick (up = a lower, nearer camera), clamped 0..1.</summary>
    private void RotateXByStickY(float stickY)
    {
        _cur.Ratio = Mathf.Clamp(_cur.Ratio - stickY * _p.XRotRatioManualSpeed, 0f, 1f);
    }

    /// <summary>isMomentDefinite_ (396): a cushioned kind, auto-following, with the eye not already beyond its distance.</summary>
    private bool IsMomentDefinite()
        => (_flags & FlagCushionFar) == 0 && IsNormalCompletely() && _followSpeed > 0.001f
           && LerpF(_p.CushionMin, _p.CushionMax, _cur.Ratio) > 0.001f;

    // ============================================================ calcPosAndAt_ (cameragc.cpp 464), the normal-class branch
    private void CalcPosAndAt()
    {
        if (_md.MoveH >= 0.05f) _flags &= ~FlagStickYaw;
        if ((_flags & FlagStickYaw) == 0)
        {
            if (_autoChaseOff > 0)
            {
                int acf = _p.AutoChaseCompleteFrame, acs = _p.AutoChaseStartFrame;
                int delta = acf - _autoChaseOff + 1;
                _autoChase = delta <= acs ? 0f : CalcRatio(acs, acf, delta);
            }
            else _autoChase = 1f;
        }
        // the squat aim's hold: a pitch offset (unk0) and an extra distance (unk4) both chased toward the aim ratio's share of the kind's
        // manual yaw speed and HoldAddDistXZ; unk8 is the smoothed 0..1 "how much aim" the ground check reads
        int yawDelta = 0;
        float distDelta = 0f;
        int yawSpeed = LerpS(_p.YAngleManualSpeedXMin, _p.YAngleManualSpeedXMax, _cur.Ratio);
        float distSpeed = LerpF(_p.HoldAddDistXZMin, _p.HoldAddDistXZMax, _cur.Ratio);
        if (SmsCamKinds.IsNormalClass(_mode) && _md.ReadyGun)
        {
            float mag = _md.AimRatio;
            distDelta = mag * distSpeed;
            yawDelta = (short)(int)(mag * yawSpeed);
        }
        SmsMath.ChaseAngleDecreaseS16(ref _holdAngle, yawDelta, SmsCamNormal.AimAngleYChaseMin);
        ChaseDecrease(ref _holdDist, distDelta, SmsCamNormal.HoldDistChase);
        if (distSpeed < 0.001f)
        {
            if (yawSpeed != 0)
                _holdTarget = Mathf.Clamp(MathF.Abs(yawDelta) / (float)MathF.Abs(yawSpeed), 0f, 1f);
        }
        else _holdTarget = Mathf.Clamp(distDelta / distSpeed, 0f, 1f);
        ChaseDecrease(ref _holdRatio, _holdTarget, SmsCamNormal.HoldDistChase);

        _flags &= ~FlagCushionFar;
        if (_posFreeze != 0) _posFreeze--;
        if (_targetFreeze != 0) _targetFreeze--;
        bool moving = _md.MoveH >= 0.05f || _md.MoveV >= 0.05f;
        bool inputActive = true;
        if (!_md.ReadyGun && (_flags & FlagFrontRotate) == 0)
            inputActive = _cx != 0f || _cy != 0f;
        if ((_flags & FlagFreeze) != 0)
        {
            if (_posFreeze == 0) _posFreeze = 1;
            if (_targetFreeze == 0) _targetFreeze = 1;
        }
        else if (moving || inputActive)
        {
            _targetFreeze = 0;
            _posFreeze = 0;
        }

        if (_posFreeze == 0 || _targetFreeze == 0)
        {
            if (_posFreeze == 0 && _targetFreeze == 0) _flags &= ~FlagFreeze;
            if (_ibApproach == 0f || moving || inputActive)
            {
                _cur.Pitch = LerpS(_p.XAngleMin, _p.XAngleMax, _cur.Ratio);
                int xAngle = (short)(_cur.Pitch + _p.OffsetAngleX + _holdAngle);
                int yAngle = (short)(_cur.Yaw + _p.OffsetAngleY);
                if (_md.MoveH >= 0.05f)
                {
                    // MaxAddAngleY: the extra swing toward his facing while he moves (0 in every kind carried)
                    int mAngle = (short)(_md.AngleY - 0x8000);
                    float m = Mathf.Clamp(_p.MaxAddAngleY * (0.5f * (1f - Cos((short)((mAngle - _finalYaw) * 2)))), -32766.998f, 32766.998f);
                    if ((short)(mAngle - yAngle) < 0) m = -m;
                    ChaseConstSpeed(ref _addY, RoundS16(m), SmsCamNormal.AddAngleYSpeed);
                }
                yAngle = (short)(yAngle + _addY);
                CalcSlopeAngleX(ref xAngle);
                Vector3 finalAt = new Vector3(
                    -(_p.OffsetLookatXZ * Sin((short)(yAngle - 0x4000)) - _cur.At.X),
                    _cur.At.Y,
                    -(_p.OffsetLookatXZ * Cos((short)(yAngle - 0x4000)) - _cur.At.Z));
                ExecSecureView(yAngle, ref finalAt);
                Vector3 saveAt = finalAt;
                float dist = LerpF(_p.DistMin, _p.DistMax, _cur.Ratio);
                int cushAng = LerpS(_p.XAngleMin, _p.XAngleMax, _cur.Ratio);
                float cushion = Cos(cushAng);
                Vector3 newPos = PolarToCross(saveAt, dist + _ibApproach, xAngle, yAngle);
                if (IsNormalCompletely())
                {
                    // the cushion rule about the previous (panned) eye with the previous DESIRED height
                    Vector3 bas = _prev.Pos;
                    bas.Y = _prev.Desired.Y;
                    CrossToPolar(saveAt, bas, out float nDist, out int nVA, out int nHA);
                    float cushDist = cushion * nDist;
                    if (cushDist > cushion * dist)
                    {
                        _flags |= FlagCushionFar;   // farther than the kind's distance: snap to it
                        _cur.Desired = newPos;
                    }
                    else
                    {
                        float minDist = cushion * (dist - LerpF(_p.CushionMin, _p.CushionMax, _cur.Ratio));
                        if (minDist < SmsCamNormal.MinCushionXZ) minDist = SmsCamNormal.MinCushionXZ;
                        if (cushDist < minDist)
                        {
                            float diff = minDist - cushDist;
                            bas.X += diff * Sin(nHA);
                            bas.Z += diff * Cos(nHA);
                            CrossToPolar(saveAt, bas, out nDist, out nVA, out nHA);
                        }
                        bool useMid = (_flags & FlagCushionFar) == 0 && IsNormalCompletely() && _followSpeed > 0.001f
                                      && LerpF(_p.CushionMin, _p.CushionMax, _cur.Ratio) > 0.001f;
                        _cur.Desired = useMid ? bas : PolarToCross(saveAt, nDist, nVA, yAngle);
                        _cur.Desired.Y = newPos.Y;
                    }
                }
                else _cur.Desired = newPos;
                float sY = Sin(yAngle), cY = Cos(yAngle);
                if (SmsCamKinds.IsNormalClass(_mode))
                {
                    // never nearer his origin (horizontally) than the kind's distance, nor MinCushionXZ (300). NOT for the L camera: the
                    // decomp's guard (cameragc.cpp 704, fabricatedInline3 = "not UNDER_GROUND") reads as every kind, which would push the
                    // L eye from its record's 200 u out to 300 -- but the game as played keeps it at 200 (Jaysonn's Ref/yCAMrEF.png vs
                    // Dolphin: the nozzle top at the frame's centre 72 u off the look-at measures the eye ~205 u out; pass 68b). The asm
                    // of that NonMatching function has not been read for this branch; the game's own framing wins until it is.
                    float dx = _md.PosU.X - _cur.Desired.X, dz = _md.PosU.Z - _cur.Desired.Z;
                    float d = MathF.Sqrt(dx * dx + dz * dz);
                    float mD2 = SmsCamNormal.MinCushionXZ < dist * cushion ? dist * cushion : SmsCamNormal.MinCushionXZ;
                    if (d < mD2)
                    {
                        float add = mD2 - d;
                        _cur.Desired.X += sY * add;
                        _cur.Desired.Z += cY * add;
                    }
                }
                // the squat aim's hold distance pulls the eye in along the bearing (not under ground)
                _cur.Desired.X = -(_holdDist * sY - _cur.Desired.X);
                _cur.Desired.Z = -(_holdDist * cY - _cur.Desired.Z);
                if (_ibApproach != 0f && inputActive)
                    IbWarp(PolarToCross(_ibAt, LerpF(_p.DistMin, _p.DistMax, _cur.Ratio) + _ibApproach, xAngle, yAngle), _ibAt);
                // mCurrentTarget.mPosition.xz = unk18.xz (asm: 0x98 -> 0x80, 0xA0 -> 0x88); its y is the height pan's
                _cur.Pos.X = _cur.Desired.X;
                _cur.Pos.Z = _cur.Desired.Z;
                ExecHeightPan();
                finalAt.Y = _cur.At.Y;   // asm: lfs 0x90 = mCurrentTarget.mTarget.y after the pan offset (the decomp's unk18.y is a mislabel)
                Vector3 posCpy = _cur.Pos;
                if (NeedWallCheck() && ExecWallCheck(ref posCpy))
                {
                    CrossToPolar(_cur.At, _cur.Pos, out _, out _cur.Pitch, out _cur.Yaw);
                    _cur.Ratio = _prev.Ratio;   // asm: 0xDC = mPreviousTarget.unk28 (the decomp's unk30 is a mislabel)
                }
                // isNeedRoofCheck_ / execRoofCheck_: Monte village only -- everywhere else the game uses a dummy roof and skips; not ported
                if (NeedGroundCheck())
                    ExecGroundCheck(posCpy);
                IbExec(_cur.Pos, finalAt, _md.PosU);
            }
        }

        // the chase tail: the actual camera toward the inbetween's pose
        if (_posFreeze == 0)
        {
            float chaseXZ = _p.PosChaseRateXZ, chaseY = _p.PosChaseRateY;
            if ((_flags & FlagFrontRotate) != 0)
                chaseXZ = 1f;
            else if (_cx != 0f || _cy != 0f)
            {
                float v = MathF.Min(_md.MoveH, 20f);
                chaseXZ = EaseIn(_p.PosChaseRateXZ, _p.PosChaseRateXZ_C, CalcRatio(20f, 0f, v));
            }
            if (_cx != 0f)
            {
                float v = MathF.Min(_md.MoveV, 20f);
                chaseY = EaseIn(_p.PosChaseRateY, _p.PosChaseRateY_C, CalcRatio(20f, 0f, v));
            }
            ChaseDecrease(ref _posU.X, _ibPos.X, chaseXZ);
            ChaseDecrease(ref _posU.Y, _ibPos.Y, chaseY);
            ChaseDecrease(ref _posU.Z, _ibPos.Z, chaseXZ);
        }
        if (_targetFreeze == 0)
        {
            ChaseDecrease(ref _atU.X, _ibAt.X, _p.AtChaseRateXZ);
            ChaseDecrease(ref _atU.Y, _ibAt.Y, _p.AtChaseRateY);
            ChaseDecrease(ref _atU.Z, _ibAt.Z, _p.AtChaseRateXZ);
        }
    }

    // ============================================================ the height pan (CameraHeightPan.cpp)
    /// <summary>fabricatedInline (Camera.hpp 245): pan while airborne and rising in a kind that pans (not on a wire, not the roof roll-up).</summary>
    private bool PanMode()
        => !IsNotHeightPanKind(_mode) && !_md.TouchGround && !_md.GoDown && !_md.KickRoofRollUp;

    /// <summary>execHeightPan_ (26): the look-at gets the jump offset (chased at AtJumpOffsetSpeed), and the eye's height either pans
    /// (chases the desired height at HeightPanChaseRateY from last tick's) or, settling after a pan, closes in at PanAfterMagnif but at
    /// least PanAfterMinHeight a tick; a JUMP_CODE / ROCKET_JUMP eye below PanWarpAngleX warps. Otherwise the eye is at its desired height.</summary>
    private void ExecHeightPan()
    {
        bool airborne = !_md.TouchGround;
        float offset = airborne ? _p.TargetAtJumpOffsetY : 0f;
        ChaseConstSpeed(ref _panOffset, offset, _p.AtJumpOffsetSpeed);
        _cur.At.Y += _panOffset;
        float deltaDesiredY = _cur.Desired.Y - _prev.Desired.Y;
        if (PanMode())
        {
            _flags |= FlagPanA;
            _flags &= ~FlagPanB;
            _cur.Pos.Y = _prev.Pos.Y;
            ChaseDecrease(ref _cur.Pos.Y, _cur.Desired.Y, _p.HeightPanChaseRateY);
            if (_cur.Ratio != _prev.Ratio)
                _cur.Pos.Y += deltaDesiredY;
        }
        else if ((_flags & (FlagPanA | FlagPanB)) != 0)
        {
            if (_cur.Ratio != _prev.Ratio)
                _cur.Pos.Y += deltaDesiredY;
            if ((_flags & FlagPanA) != 0)
            {
                _flags &= ~FlagPanA;
                bool warp = _mode == SmsCamKind.RocketJump && _finalPitch < SmsCamNormal.PanWarpAngleX;
                if (warp)
                {
                    _posU.Y = _cur.Pos.Y = _cur.Desired.Y;
                    _flags &= ~FlagPanB;
                }
                else _flags |= FlagPanB;
            }
            if (_cur.Pos.Y != _cur.Desired.Y)
            {
                bool still = ChaseSpecialDecrease(ref _cur.Pos.Y, _cur.Desired.Y, SmsCamNormal.PanAfterMagnif, SmsCamNormal.PanAfterMinHeight);
                if (!still && !airborne) _flags &= ~FlagPanB;
            }
            else if (!airborne) _flags &= ~FlagPanB;
        }
        else _cur.Pos.Y = _cur.Desired.Y;
    }

    private void KillHeightPan()
    {
        _flags &= ~(FlagPanA | FlagPanB);
        _cur.Pos.Y = _cur.Desired.Y;
        _panOffset = 0f;
    }

    /// <summary>killHeightPanWhenChangeCamMode_ (8): into a kind without a pan, or out of HANG.</summary>
    private void KillHeightPanWhenChangeCamMode()
    {
        if (IsNotHeightPanKind(_mode) || _prevMode == SmsCamKind.Hang)
            KillHeightPan();
    }

    // ============================================================ the slope aim (cameragc.cpp 408)
    /// <summary>calcSlopeAngleX_: in a slope kind, standing on camera-solid ground and not squat-aiming, the ground SlopeForwardDistXZ (300 u)
    /// ahead of him (away from the eye) raises an angle up to SlopeMaxAngleX (45 deg; steeper = none) that the pitch chases at
    /// SlopeSpeedAngleX a tick and the pitch is lowered by, then the pitch is clamped to the limits.</summary>
    private void CalcSlopeAngleX(ref int xAngle)
    {
        int result = 0;
        if (!_md.ReadyGun && _md.TouchGround && IsSlopeKind(_mode))
        {
            Vector3 diff = new Vector3(_md.PosU.X - _posU.X, 0f, _md.PosU.Z - _posU.Z);
            if (diff.LengthSquared() > 1e-6f)
            {
                float grLevel = _md.PosU.Y; // SMS_GetMarioGrLevel: his ground height (the feet, standing)
                int maxAng = SmsCamNormal.SlopeMaxAngleX;
                float fwd = SmsCamNormal.SlopeForwardDistXZ;
                Vector3 sample = _md.PosU + diff.Normalized() * fwd;
                float probeY = fwd * MathF.Tan(maxAng * SmsUnits.BamToRad) + 10f + _md.PosU.Y;
                if (GroundBelow(sample.X, probeY, sample.Z, out float gy))
                {
                    float height = gy - grLevel;
                    int angle = 0;
                    if (height > 0f) angle = Matan(fwd, height);
                    result = angle > maxAng ? 0 : angle;
                }
            }
        }
        ChaseConstSpeed(ref _slopeX, result, SmsCamNormal.SlopeSpeedAngleX);
        xAngle = (short)(xAngle - _slopeX);
        xAngle = Mathf.Clamp(xAngle, SmsCamNormal.LimitMinAngleX, SmsCamNormal.LimitMaxAngleX);
    }

    // ============================================================ the secure view (CameraSecureView.cpp)
    /// <summary>calcSecureViewTarget_: a sideways look-at shift from the kind's SecureViewDist fields -- 0 in every kind carried.</summary>
    private void CalcSecureViewTarget(int angle, out float outX, out float outZ)
    {
        int bas = (short)(_md.AngleY - 0x8000);
        int diff = (short)(angle - bas);
        float first = LerpF(_p.SecureViewDistXMin, _p.SecureViewDistXMax, _cur.Ratio);
        float cosD = Cos(diff);
        float second = cosD >= 0f ? 0f : LerpF(_p.SecureViewDistZMin, _p.SecureViewDistZMax, _cur.Ratio);
        float mag = -MathF.Abs(first * Sin(diff) + second * cosD);
        outX = mag * Sin(bas);
        outZ = mag * Cos(bas);
    }

    /// <summary>execSecureView_: the shift chased at SecureViewChase (the game slows it by his turn rate, unk9C; a no-op at 0 anyway).</summary>
    private void ExecSecureView(int angle, ref Vector3 at)
    {
        CalcSecureViewTarget(angle, out float px, out float pz);
        float v = Mathf.Clamp(_p.SecureViewChase, 0f, 1f);
        ChaseDecrease(ref _secureX, px, v);
        ChaseDecrease(ref _secureZ, pz, v);
        at.X += _secureX;
        at.Z += _secureZ;
    }

    // ============================================================ the BG checks (CameraBGCheck.cpp) in Godot physics
    /// <summary>isNeedWallCheck_ (118): not in the L camera nor its inbetween, not the talk camera (Mode TalkingPOV takes the frame), never
    /// under the front-rotate swing (CAMERA_FLAG_UNK4).</summary>
    private bool NeedWallCheck() => (_flags & FlagFrontRotate) == 0 && _mode != SmsCamKind.LNormal && !IsLInbetween();

    /// <summary>isNeedGroundCheck_ (78): not in the L camera once its inbetween is over; skipped for 120 ticks once the eye sits more than
    /// 1.25 x the kind's highest reach over the look-at.</summary>
    private bool NeedGroundCheck()
    {
        if (_groundSkip != 0) return false;
        if (_mode == SmsCamKind.LNormal && _ibFrames == 0) return false;
        if (SmsCamKinds.IsNormalClass(_mode))
        {
            float a = _p.DistMin * Sin(_p.XAngleMin);
            float b = _p.DistMax * Sin(_p.XAngleMax);
            if (a > b) b = a;
            float distY = _posU.Y - _atU.Y;
            if (distY > 1.25f * b)
            {
                if (_groundSkip < 120) _groundSkip = 120;
                return false;
            }
        }
        return true;
    }

    /// <summary>execWallCheck_ (141): the walls within WallCheckRadius (150 u) of the eye's XZ at last tick's eye height + 10 push it out
    /// along their normal by the overlap (x WallRevisionRatio). The game walks the map's wall planes (up to 4); here a thin disc rests
    /// against the world and the deepest wall-ish contact is taken up to four times.</summary>
    private bool ExecWallCheck(ref Vector3 posCpy)
    {
        float radius = SmsCamNormal.WallCheckRadius;
        if (radius <= 0f) return false;
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return false;
        bool moved = false;
        var exclude = new Array<Rid>();
        if (_hasMarioRid) exclude.Add(_marioRid);
        for (int k = 0; k < 4; k++)
        {
            Vector3 centreU = new Vector3(_cur.Pos.X, _prev.Pos.Y + 10f, _cur.Pos.Z);
            var q = new PhysicsShapeQueryParameters3D
            {
                Shape = _wallShape,
                Transform = new Transform3D(Basis.Identity, M(centreU)),
                CollisionMask = _mask,
                Exclude = exclude,
            };
            Dictionary info = space.GetRestInfo(q);
            if (info.Count == 0) break;
            Vector3 n = (Vector3)info["normal"];
            Vector3 point = (Vector3)info["point"];
            Rid rid = (Rid)info["rid"];
            if (CameraPasses((ulong)info["collider_id"]))
            {
                exclude.Add(rid);
                continue;
            }
            if (MathF.Abs(n.Y) > 0.6f)
                break; // a floor or ceiling under the disc, not a wall
            Vector3 nXZ = new Vector3(n.X, 0f, n.Z);
            if (nXZ.LengthSquared() < 1e-6f) break;
            nXZ = nXZ.Normalized();
            float sd = (M(centreU) - point).Dot(nXZ) * SmsUnits.UnitsPerMetre; // the eye's signed distance off the wall plane, units
            if (sd >= radius) break;
            moved = true;
            float push = (radius - sd) * SmsCamNormal.WallRevisionRatio;
            _cur.Pos.X += push * nXZ.X;
            _cur.Pos.Z += push * nXZ.Z;
            posCpy.X += (radius - sd) * nXZ.X;
            posCpy.Z += (radius - sd) * nXZ.Z;
            exclude.Add(rid); // that body is done; another wall may still touch
        }
        return moved;
    }

    /// <summary>execGroundCheck_ (210): the ground under the eye's XZ probed from last tick's eye height + GroundChangeY (20 u); the eye kept
    /// GroundHeightNormal (144 u) over it, GroundHeightReadyGun (30 u) when squat-aiming (by the smoothed aim share).</summary>
    private void ExecGroundCheck(Vector3 posCpy)
    {
        float groundOff = LerpF(SmsCamNormal.GroundHeightNormal, SmsCamNormal.GroundHeightReadyGun, _holdRatio);
        if (GroundBelow(posCpy.X, _prev.Pos.Y + SmsCamNormal.GroundChangeY, posCpy.Z, out float groundY))
        {
            if (_cur.Pos.Y < groundY + groundOff)
                _cur.Pos.Y = groundY + groundOff;
        }
    }

    /// <summary>checkGroundIgnoreWaterSurface stand-in: the first camera-solid body straight below (x, y, z) in units, within 100000 u
    /// (water is an Area3D here and never answers a ray).</summary>
    private bool GroundBelow(float xU, float yU, float zU, out float groundYU)
    {
        groundYU = 0f;
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return false;
        Vector3 from = M(new Vector3(xU, yU, zU));
        Vector3 to = from + Vector3.Down * M(100000f);
        var exclude = new Array<Rid>();
        if (_hasMarioRid) exclude.Add(_marioRid);
        for (int k = 0; k < 4; k++)
        {
            var q = PhysicsRayQueryParameters3D.Create(from, to, _mask, exclude);
            Dictionary hit = space.IntersectRay(q);
            if (hit.Count == 0) return false;
            if (CameraPasses((ulong)hit["collider_id"]))
            {
                exclude.Add((Rid)hit["rid"]);
                continue;
            }
            groundYU = U(((Vector3)hit["position"]).Y);
            return true;
        }
        return false;
    }

    /// <summary>A ray from the eye to a point on Mario meets camera-solid world: he is occluded.</summary>
    private bool RayHits(Vector3 fromM, Vector3 toM)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return false;
        var exclude = new Array<Rid>();
        if (_hasMarioRid) exclude.Add(_marioRid);
        for (int k = 0; k < 4; k++)
        {
            var q = PhysicsRayQueryParameters3D.Create(fromM, toM, _mask, exclude);
            Dictionary hit = space.IntersectRay(q);
            if (hit.Count == 0) return false;
            if (CameraPasses((ulong)hit["collider_id"]))
            {
                exclude.Add((Rid)hit["rid"]);
                continue;
            }
            return true;
        }
        return false;
    }

    /// <summary>The game's camera checks only surfaces flagged CAMERA_WONT_CLIP (0x8000); here every static body clips, and a body (or a
    /// parent up to three levels) carrying the metadata `sms_camera_pass` opts out.</summary>
    private static bool CameraPasses(ulong colliderId)
    {
        if (GodotObject.InstanceFromId(colliderId) is not Node node) return false;
        for (int i = 0; i < 4 && node != null; i++)
        {
            if (node.HasMeta("sms_camera_pass")) return true;
            node = node.GetParent();
        }
        return false;
    }

    /// <summary>calcNowTargetFromPosAndAt_ (cameragc.cpp 310): the camera resumes its polar state from a pose it did not solve (a demo /
    /// talk camera's end): the ratio from the pose's pitch within the kind's range, the yaw its bearing, everything parked on it.</summary>
    private void ResumeFromView(Vector3 posU, Vector3 atU, int blendFrames)
    {
        CrossToPolar(atU, posU, out _, out int xAngle, out int yAngle);
        _cur.Ratio = Mathf.Clamp(CalcRatio(_p.XAngleMin, _p.XAngleMax, xAngle), 0f, 1f);
        _cur.Pitch = LerpS(_p.XAngleMin, _p.XAngleMax, _cur.Ratio);
        _cur.Yaw = yAngle;
        _cur.Pos = _cur.Desired = posU;
        _cur.At = atU;
        _prev = _cur;
        _posU = _viewPosU = posU;
        _atU = _viewAtU = atU;
        _finalPitch = xAngle;
        _finalYaw = yAngle;
        _ibPos = posU;
        _ibAt = atU;
        _ibMario = _md.PosU;
        IbStart(blendFrames);
        _ibApproach = 0f;
        _posFreeze = _targetFreeze = 0;
        _flags &= ~(FlagFrontRotate | FlagFrontY | FlagFrontDone);
        KillHeightPan();
        HoldReset();
    }

    // ============================================================ TalkingPOV (sign / switch dialogue) -- Jaysonn's shot (RedCoinSwitch.cs)
    /// <summary>The dialogue framing: the camera low on Mario's left, looking across him, Mario frozen in his talking state.</summary>
    public void EnterTalkingPOV()
    {
        if (_camera == null) return;
        DropLCamera(); // the game's talk camera pushes its own mode; the L camera does not survive it
        Mode = CamMode.TalkingPOV;
        SetMarioLocked(true);
        _mario?.EnterTalkingState();
        _tkYaw = GetVisualYaw() + Mathf.DegToRad(TalkingPOVYawOffsetDeg);
        _tkPitch = Mathf.DegToRad(TalkingPOVPitchDeg);
        _tkRadius = TalkingPOVRadius * _rigScale;
        _tkTransition = TalkingPOVEntryDuration;
        // the chase starts from the live view: the eye's bearing and elevation about the look-at in the shot's convention (pitch < 0 = above)
        _tkLookAt = M(_atU);
        Vector3 d = M(_posU) - _tkLookAt;
        float r = MathF.Max(d.Length(), 0.01f);
        _tkCurYaw = MathF.Atan2(d.X, d.Z);
        _tkCurPitch = -MathF.Asin(Mathf.Clamp(d.Y / r, -1f, 1f));
        _tkCurRadius = r;
    }

    public void ExitTalkingPOV()
    {
        if (Mode != CamMode.TalkingPOV) return;
        Mode = CamMode.Normal;
        _mario?.ExitTalkingState();
        SetMarioLocked(false);
        // the game's talk camera ends with changeCamMode_(mInitialMode): the normal camera resumes from the talk pose with its blend
        Vector3 eyeM = _camera != null ? _camera.GlobalPosition : M(_posU);
        ResumeFromView(U(eyeM), U(_tkLookAt), 16);
        Apply();
    }

    private void TickTalking(float dt)
    {
        Vector3 offset = TalkingPOVLocalOffset.Rotated(Vector3.Up, GetVisualYaw()) * _rigScale;
        Vector3 marioP = _target.GlobalPosition + offset;
        _tkLookAt = _tkLookAt.Lerp(marioP, 1f - MathF.Exp(-FollowSmooth * dt));
        _tkYaw = GetVisualYaw() + Mathf.DegToRad(TalkingPOVYawOffsetDeg);
        _tkPitch = Mathf.DegToRad(TalkingPOVPitchDeg);
        _tkRadius = TalkingPOVRadius * _rigScale;
        bool transitioning = _tkTransition > 0f;
        if (transitioning) _tkTransition -= dt;
        float yawRate = transitioning ? YawSnapRate : YawChaseRate;
        float pitchRate = transitioning ? PitchSnapRate : PitchChaseRate;
        float radiusRate = transitioning ? PitchSnapRate : RadiusChaseRate;
        _tkCurYaw = Mathf.LerpAngle(_tkCurYaw, _tkYaw, 1f - MathF.Exp(-yawRate * dt));
        _tkCurPitch = Mathf.Lerp(_tkCurPitch, _tkPitch, 1f - MathF.Exp(-pitchRate * dt));
        _tkCurRadius = Mathf.Lerp(_tkCurRadius, _tkRadius, 1f - MathF.Exp(-radiusRate * dt));
        float cp = MathF.Cos(_tkCurPitch);
        Vector3 eye = _tkLookAt + _tkCurRadius * new Vector3(MathF.Sin(_tkCurYaw) * cp, -MathF.Sin(_tkCurPitch), MathF.Cos(_tkCurYaw) * cp);
        GlobalPosition = _tkLookAt;
        _pivot.GlobalRotation = new Vector3(0f, _tkCurYaw, 0f);
        Vector3 dir = _tkLookAt - eye;
        if (dir.LengthSquared() > 1e-10f)
            _camera.GlobalTransform = new Transform3D(Basis.LookingAt(dir, Vector3.Up), eye);
        // the game's state rides along so the exit resumes from here without a jump
        _posU = _viewPosU = U(eye);
        _atU = _viewAtU = U(_tkLookAt);
    }

    private void SetMarioLocked(bool locked)
    {
        if (_mario != null)
            _mario.CameraLocked = locked;
    }

    /// <summary>For drives and tests: the camera's state this frame -- the kind, the actual yaw / pitch (unk258 / unk256), the target yaw
    /// (mYaw), the ratio, the kind's distance at that ratio and the actual one, the eye and look-at (units), the blend ticks left, the
    /// occlusion and the squat aim's hold distance.</summary>
    public (SmsCamKind kind, int yaw, int pitch, int targetYaw, float ratio, float wantDist, float dist, Vector3 eyeU, Vector3 atU, int blend, bool occluded, float holdDist, float moveH, bool ground, SmsCamKind? area) SmsProbe()
        => (_mode, _finalYaw, _finalPitch, _cur.Yaw, _cur.Ratio, LerpF(_p.DistMin, _p.DistMax, _cur.Ratio), (_posU - _atU).Length(), _posU, _atU, _ibFrames, _occluded, _holdDist, _md.MoveH, _md.TouchGround, _areaKind);
}
