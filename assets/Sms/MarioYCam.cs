using System;
using Godot;

/// <summary>
/// The Y camera -- the game's CAMERA_MODE_L_NORMAL, its "L button camera" (CameraChange.cpp: execCameraModeChangeProc_ 580-630,
/// execFrontRotate_ 449, doLButtonCameraOn_ 469 / Off_ 479, setUpToLButtonCamera_ 256, changeCamModeSub_ 272; CameraNotice.cpp
/// ctrlLButtonCamera_ 208; cameragc.cpp rotateX/Y_ByStick 333/346, calcPosAndAt_ 465, ctrlGameCamera_ 861; CameraNormal.cpp 79-86;
/// CameraInbetween.cpp execCameraInbetween 57; cameralib.cpp CLBChaseAngleDecrease 61), with the shipped /Camera/kindLNormal.prm and
/// camera_normal.prm (SmsParams.LCamera).
///
/// THE CAMERA RUNS AT 120 Hz. "camera 1" (the CPolarSubCamera) sits on "PerformList Movement" (data/PerformLists.bin), which the
/// director performs with CUE_MOVE on every simulation tick -- four a 30 fps frame, like Mario -- and CPolarSubCamera::perform reads
/// the first- and last-tick flags of TGraphics::unk0. So every "a frame" rate in the camera code is a TICK: the stick turns the L camera
/// 300 BAM a tick (198 deg/s at full stick), the pitch ratio moves 0.01 a tick (the whole range in 0.83 s), the chase 0.1 / 0.08 a
/// tick, the 60-tick lock is 0.5 s and the 16-tick inbetween 0.13 s. (Pass 16 had all of these at 30 Hz: four times too slow, a 2 s
/// lock -- "no cam turning and its slow", Jaysonn 2026-09-28 03:00.)
///
/// THE Y PRESS is not a mode change. In a normal camera, Y with nothing to lock on to (execNoticeOnOffProc_(2) finds no boss) calls
/// execFrontRotate_(): the normal camera's yaw chases "behind Mario" (unk274 = his face - 0x8000) halving its gap every tick
/// (YButtonRotateChase 2: 180 deg in 16 ticks), the position following at once (chaseXZ = 1 under CAMERA_FLAG_UNK4), and a flag
/// (UNK8 -> UNK10) turns the L camera on when the swing has settled: doLButtonCameraOn_ -> changeCamMode_(L_NORMAL) with the 16-tick
/// inbetween (TCameraInbetween: the look-at sweeps around him and the camera around the look-at in polar coordinates, then the
/// chase follows). The L camera's yaw is the one the swing left, so Mario -- who faces the camera's look in the mode -- keeps his facing.
/// Mario is still his own during the swing (a 30 fps frame of the game); the port lets this frame run too.
///
/// IN THE MODE (MarioGamePad.cpp 70-88): the MAIN stick is the camera's (mCompSPos[4],[5]) and Mario's is zero; A and B are
/// MEANING_CAM_AB, Y is Y, R sprays, X switches, L is MEANING_L. Y / A / B leave (doLButtonCameraOff_(false), a 16-tick sweep back)
/// once unk282 == 0 and the inbetween is over; a knockdown (status bit 0x20000), the hover / rocket (a logged deviation keeps the
/// mode on through them: SmsParams.LCamera.StaysOnRocketing), the dash, the surf, sleeping, climbing cut it in one tick
/// (Off_(true)). Mario faces the chased camera's bearing (MarioMove.cpp 2449: unk258 + 0x8000 -
/// OffsetAngleY), holds the pump (MarioUpper.cpp 17), and the gun angle IS mCurrentTarget.mPitch (WaterGun.cpp 219): looking down
/// bends the upper body forward (MarioWaistCtrl 476), looking up lifts the spray nozzle and the head. On the way out the normal camera
/// resumes with the L camera's yaw -- behind the way he now faces (the rig is told so: SunshineCamera.SmsYCamExit).
///
/// Port shape: a second Camera3D ("SmsYCamera", TopLevel) made current from the Y press; the rig's SunshineCamera keeps following
/// underneath and gets the view back after the sweep out. Its own Y-button mode is off under SMS (it froze Mario with CameraLocked
/// underneath this one: no stick, no FLUDD, no way out -- the whole of the pass-16 report).
/// A Y IN THE AIR works as in the game (execFrontRotate_ runs in any status but the hip drop; only sleeping, the surf, the dash and
/// climbing refuse -- and the hover / rocket, unless StaysOnRocketing): the mode keeps his status, his stick is zero, A / B are eaten, and his facing is forced to
/// the camera's look every tick -- mForwardVel runs along mFaceAngle, so the flight bends with the look (the port projects the
/// velocity onto the forced face each frame: the same thing for a smooth turn). The landing inside the mode goes straight to WAIT.
/// Not ported: the notice lock-on (no bosses), the SEs (0x4824 / 0x4825), the squat inside the mode (R clicked / L: canSquat holds
/// in the mode too). The upper body holds the grips in the mode as the game's does (checkPumping(), MarioUpper.cpp 17: HOLDING_PUMP
/// at the pump clip's frame 0); the pass-17b idle-arms deviation was withdrawn in pass 17c.
/// </summary>
public partial class Mario
{
    private enum YCamPhase { Off, FrontRotate, LNormal, BlendOut }

    private YCamPhase _ycamPhase = YCamPhase.Off;
    private bool _ycamOn = false;                  // isLButtonCamera(): the L mode proper (not the swing before it): Mario's side of the mode
    private int _ycamYawBam = 0;                   // mCurrentTarget.mYaw (s16): the camera's bearing from the look-at, the game's (sin, cos) convention
    private int _ycamBehindBam = 0;                // unk274: behind him at the Y press
    private float _ycamRatio = 0f;                 // mCurrentTarget.unk28: the pitch as a 0..1 ratio
    private int _ycamLock = 0;                     // unk282 (ticks): Y / A / B do nothing, and Y cannot re-enter, for 60 ticks after a change
    private int _ycamInbet = 0;                    // mInbetween->mFramesRemaining (ticks)
    private float _ycamTickAccum = 0f;
    private Vector2 _ycamStick = Vector2.Zero;     // the pad's main stick as the camera reads it (right / down positive here)
    private bool _ycamCancelPressed = false;       // MEANING_Y | MEANING_CAM_AB this physics frame
    private Camera3D _ycamCamera;
    private Vector3 _ycamPos, _ycamAt;             // mPosition / mTarget: the chased camera (metres)
    private Vector3 _ycamInbetPos, _ycamInbetAt;   // mInbetween->mPos / mAt
    private float _ycamInbetAtR, _ycamInbetAtPitch, _ycamInbetAtYaw;    // TCameraInbetween unk10 / unk14 / unk16: the look-at about him
    private float _ycamInbetPosR, _ycamInbetPosPitch, _ycamInbetPosYaw; // unk8 / unkC / unkE: the camera about the look-at
    private float _ycamFrontR, _ycamFrontPitch;    // the rig's polar frame about its look-at at the Y press (the swing keeps them)
    private Vector3 _ycamFrontAtOffset;            // the rig's look-at, relative to his origin
    private float _ycamFov = 60f;
    private Transform3D _ycamXform = Transform3D.Identity;
    private float _ycamDbgTimer = 0f;
    private int _ycamNeutralTicks = 0;              // mNeutralKeyTimer (in ticks): his stick is dead, then fades in, after either end of the mode
    private bool _ycamHoldThisFrame = false;       // the frame whose Y / A / B ended the mode: that press was the camera's (MEANING_CAM_AB), not a jump or a dive

    private float YCamPitchBam => Mathf.Lerp(SmsParams.LCamera.XAngleMin, SmsParams.LCamera.XAngleMax, _ycamRatio);

    // CLBPolarToCross / CLBCrossToPolar: bearing from +Z toward +X, elevation up
    private static Vector3 YCamPolar(float r, float pitch, float yaw)
        => new Vector3(r * MathF.Cos(pitch) * MathF.Sin(yaw), r * MathF.Sin(pitch), r * MathF.Cos(pitch) * MathF.Cos(yaw));

    private static void YCamToPolar(Vector3 v, out float r, out float pitch, out float yaw)
    {
        r = v.Length();
        pitch = r > 1e-6f ? MathF.Asin(Mathf.Clamp(v.Y / r, -1f, 1f)) : 0f;
        yaw = MathF.Atan2(v.X, v.Z);
    }

    /// <summary>CLBChaseConstantSpecifyFrame: a constant-step chase that lands on the target at the last frame.</summary>
    private static float YCamChaseConst(float value, float target, int rem) => rem <= 1 ? target : value + (target - value) / rem;
    private static float YCamChaseConstAngle(float value, float target, int rem) => rem <= 1 ? target : value + SmsMath.WrapAngle(target - value) / rem;

    /// <summary>A Y press that starts the sequence: a normal camera (the rig's Normal or L-hold mode), unk282 run out, and none of the
    /// states the mode change refuses (execCameraModeChangeProc_ 588: asleep, the surf / water, the hover or rocket, the dash,
    /// climbing; execFrontRotate_ 453: not the hip drop). On the ground a standing / running / landing status; in the air any
    /// launched or falling state that doJumping() steers (the game takes a Y in a jump too).</summary>
    private bool SmsYCamCanEnter()
    {
        if (!UseSmsPhysics || _ycamPhase != YCamPhase.Off || _ycamLock > 0 || camera == null || _camera == null || armature == null)
            return false;
        if (_camera.Mode != SunshineCamera.CamMode.Normal && _camera.Mode != SunshineCamera.CamMode.LButton)
            return false;
        if (_smsInWater || _fluddDashing || CameraLocked)
            return false;
        if (!IsOnFloor())
            return SmsAirSteers(stateOfMario) // the jumps, the flips, the dives, the rolls, a fall off an edge; not the hip drop, a wall slide, a hang
                || (SmsParams.LCamera.StaysOnRocketing && stateOfMario == MarioState.fluddRocket); // the deviation: a Y while he hovers
        switch (stateOfMario)
        {
            case MarioState.idle:
            case MarioState.walking:
            case MarioState.running:
            case MarioState.sprinting:
            case MarioState.sneak:
            case MarioState.pivot:
            case MarioState.landing:
            case MarioState.singleJumpLanding:
            case MarioState.doubleJumpLanding:
            case MarioState.tripleJumpLanding:
            case MarioState.rolloutRun:
                return true;
        }
        return false;
    }

    /// <summary>execFrontRotate_(): the Y press. This camera takes the rig's place (same pose, so nothing cuts) and its yaw about the
    /// rig's look-at starts halving its gap to "behind him" every tick (YButtonRotateChase 2); the L camera follows when it settles.
    /// This frame is still Mario's (the game's mode is still the normal one during the swing).</summary>
    private void SmsYCamEnter()
    {
        if (_ycamCamera == null)
        {
            _ycamCamera = new Camera3D { Name = "SmsYCamera", TopLevel = true };
            AddChild(_ycamCamera);
        }
        _ycamCamera.Far = camera.Far;
        _ycamCamera.KeepAspect = camera.KeepAspect;
        Vector3 at0 = _camera.GlobalPosition;   // the rig's smoothed look-at (SunshineCamera sits on it: UpdateFollow)
        Vector3 camPos = camera.GlobalPosition;
        YCamToPolar(camPos - at0, out _ycamFrontR, out _ycamFrontPitch, out float yaw);
        if (_ycamFrontR < 0.05f)
            _ycamFrontR = 0.05f;
        _ycamFrontAtOffset = at0 - GlobalPosition;
        _ycamYawBam = (short)(int)MathF.Round(yaw / SmsUnits.BamToRad);
        Vector3 face = _smsFaceValid ? SmsMath.DirFromYaw(_smsFaceYaw)
            : (lastFacingDirection.LengthSquared() > 1e-4f ? lastFacingDirection.Normalized() : SmsMath.DirFromYaw(0f));
        _ycamBehindBam = (short)(int)MathF.Round(MathF.Atan2(-face.X, -face.Z) / SmsUnits.BamToRad); // unk274 = mFaceAngle.y - 0x8000
        _ycamPos = camPos;
        _ycamAt = at0;
        _ycamFov = camera.Fov;
        _ycamXform = camera.GlobalTransform;
        _ycamPhase = YCamPhase.FrontRotate;
        _ycamOn = false;
        SmsYCamApplyTransform();
        _ycamCamera.MakeCurrent();
        if (SmsDebugPrint)
        {
            int gap = (short)(_ycamBehindBam - _ycamYawBam);
            GD.Print($"[SMS ycam] Y: the camera swings behind him ({MathF.Abs(gap) * SmsUnits.BamToRad * 180f / MathF.PI:F0} deg to go, the gap halved each tick), then the L camera");
        }
    }

    /// <summary>doLButtonCameraOn_ + setUpToLButtonCamera_ + changeCamModeSub_: unk282 = 60 ticks, the pitch ratio where the pitch is
    /// -OffsetAngleX (the camera level with its target), the 16-tick inbetween from where the swing left the camera, and Mario's side
    /// of the mode from this tick.</summary>
    private void SmsYCamModeOn()
    {
        _ycamPhase = YCamPhase.LNormal;
        _ycamOn = true;
        _ycamLock = SmsParams.LCamera.LockTicks;
        _ycamRatio = Mathf.Clamp((float)(-SmsParams.LCamera.OffsetAngleX - SmsParams.LCamera.XAngleMin) / (SmsParams.LCamera.XAngleMax - SmsParams.LCamera.XAngleMin), 0f, 1f);
        _ycamNeutralTicks = SmsParams.LCamera.NeutralKeyTicks; // onNeutralMarioKey()
        SmsYCamInbetStart(SmsParams.LCamera.InbetLNormal);
        _ycamDbgTimer = 0f;
        if (SmsDebugPrint)
            GD.Print($"[SMS ycam] on: bearing {_ycamYawBam * SmsUnits.BamToRad * 180f / MathF.PI:F0} deg (behind him), pitch {YCamPitchBam * 180f / 32768f:F1} deg (ratio {_ycamRatio:F2}), target {(_fluddReady ? "the nozzle top" : "his origin + 150")}; the stick turns {SmsParams.LCamera.YAngleManualSpeed * SmsUnits.TicksPerSecond * 360f / 65536f:F0} deg/s, tilts the range in {1f / (SmsParams.LCamera.XRotRatioManualSpeed * SmsUnits.TicksPerSecond):F2} s, up = look down (the game's); Y / A / B leave after {SmsParams.LCamera.LockTicks / SmsUnits.TicksPerSecond:F1} s; R sprays at the pitch");
    }

    /// <summary>doLButtonCameraOff_(): unk282 = 60 again; a cancel (Y / A / B) sweeps back over 16 ticks, a state cut takes one. The
    /// normal camera resumes with this camera's yaw -- behind the way he faces now -- so the rig snaps its yaw there first and this
    /// camera sweeps onto the rig's live pose. Mario is his own from this tick.</summary>
    private void SmsYCamModeOff(bool cut, string why)
    {
        _ycamOn = false;
        _ycamLock = SmsParams.LCamera.LockTicks;
        _ycamPhase = YCamPhase.BlendOut;
        _ycamInbet = cut ? 1 : SmsParams.LCamera.InbetLNormal;
        _ycamNeutralTicks = SmsParams.LCamera.NeutralKeyTicks; // onNeutralMarioKey(): the stick that aimed the camera does not launch him
        if (!cut)
            _ycamHoldThisFrame = true; // the press that ended the mode was MEANING_CAM_AB / MEANING_Y: it does not reach his jump or dive this frame
        _camera?.SmsYCamExit();
        if (SmsDebugPrint)
            GD.Print($"[SMS ycam] off ({why}): {(cut ? "a cut" : $"the {SmsParams.LCamera.InbetLNormal}-tick sweep back")} to the normal camera, behind him; Y again after {SmsParams.LCamera.LockTicks / SmsUnits.TicksPerSecond:F1} s");
    }

    /// <summary>The sweep is over (or the cut is done): the rig's camera is current again.</summary>
    private void SmsYCamFinish()
    {
        _ycamPhase = YCamPhase.Off;
        _ycamOn = false;
        if (camera != null)
            camera.MakeCurrent();
    }

    /// <summary>Respawn / a hard reset: the rig's camera back at once, no lock.</summary>
    private void SmsYCamReset()
    {
        bool wasCurrent = _ycamCamera != null && _ycamCamera.Current;
        _ycamPhase = YCamPhase.Off;
        _ycamOn = false;
        _ycamLock = 0;
        _ycamInbet = 0;
        if (wasCurrent && camera != null)
            camera.MakeCurrent();
    }

    /// <summary>Once a physics frame, at the top of the SMS block (so it runs whatever handler owns the frame): the camera's ticks, the
    /// lock, the exits, the pose, and -- in the mode -- Mario's facing. The stick is the pad's main stick as the camera reads it
    /// (mMainStick.mPosX / mPosY, -1..1); PADClamp's dead zone (15 of 72) is approximated by GetVector's 0.2 circular one.</summary>
    private void SmsYCamFrame(double delta)
    {
        if (!UseSmsPhysics)
            return;
        _ycamHoldThisFrame = false;
        // execCameraModeChangeProc_: a Y in a normal camera starts the swing, whatever status he is in (execFrontRotate_)
        if (_ycamPhase == YCamPhase.Off && Input.IsActionJustPressed("button_y") && SmsYCamCanEnter())
            SmsYCamEnter();
        if (_ycamPhase == YCamPhase.Off && _ycamLock == 0 && _ycamNeutralTicks == 0)
            return;
        _ycamStick = Input.GetVector("Lstick_left", "Lstick_right", "Lstick_up", "Lstick_down", 0.2f);
        if (_ycamPhase == YCamPhase.LNormal)
        {
            // MEANING_Y | MEANING_CAM_AB, frame meanings: an edge this frame; the ticks below may act on it, then it is gone
            _ycamCancelPressed = Input.IsActionJustPressed("button_y") || Input.IsActionJustPressed("button_a")
                || Input.IsActionJustPressed("key_space") || Input.IsActionJustPressed("button_b");
        }
        int n = SmsUnits.TicksFor(delta, ref _ycamTickAccum);
        for (int i = 0; i < n; i++)
            SmsYCamTick();
        _ycamCancelPressed = false;
        if (_ycamOn || _ycamHoldThisFrame)
        {
            // MEANING_CAM_AB: in the mode A and B are the camera's, not MEANING_A / MEANING_B -- nothing of Mario's sees them this frame
            // (no jump, no dive, no A-held lift), and the frame whose press ENDED the mode is the camera's too; on the ground the hold
            // below covers it, in the air this does. The input block already buffered that press as a jump: drop it.
            Input.ActionRelease("button_a");
            Input.ActionRelease("key_space");
            Input.ActionRelease("button_b");
            jumpBuffer = 0;
        }
        if (_ycamPhase != YCamPhase.Off)
            SmsYCamApplyTransform();
        if (_ycamOn)
        {
            SmsYCamFaceLook(); // in every status but the hang family (playerControl 2451): on the ground and through a jump
            if (SmsDebugPrint && _ycamOn)
            {
                _ycamDbgTimer += (float)delta;
                if (_ycamDbgTimer >= 1f)
                {
                    _ycamDbgTimer = 0f;
                    Vector3 d = _ycamPos - _ycamAt;
                    GD.Print($"[SMS ycam] yaw {SmsMath.WrapAngle(_ycamYawBam * SmsUnits.BamToRad) * 180f / MathF.PI:F0} deg, pitch {YCamPitchBam * 180f / 32768f:F1} deg (ratio {_ycamRatio:F2}), camera {SmsUnits.MetresToUnits(d.Length()):F0} u from the look-at, {MathF.Asin(Mathf.Clamp(d.Y / MathF.Max(d.Length(), 1e-4f), -1f, 1f)) * 180f / MathF.PI:F0} deg over it; stick ({_ycamStick.X:F2}, {-_ycamStick.Y:F2}); gun angle {_fluddGunAngle:F0} BAM, upper {_fluddUpper}, R {_fluddR:F2}; lock {_ycamLock} inbet {_ycamInbet}; face {_smsFaceYaw * 180f / MathF.PI:F0} deg");
                }
            }
        }
    }

    /// <summary>One 120 Hz tick of CPolarSubCamera::ctrlGameCamera_ as far as this mode is concerned.</summary>
    private void SmsYCamTick()
    {
        if (_ycamLock > 0)
            _ycamLock--;
        if (_ycamNeutralTicks > 0)
            _ycamNeutralTicks--;
        switch (_ycamPhase)
        {
            case YCamPhase.Off:
                return;

            case YCamPhase.FrontRotate:
            {
                // ctrlNormalOrTowerCamera_ under CAMERA_FLAG_UNK4: the target yaw halves its gap to unk274 each tick, the position follows at once
                bool more = SmsMath.ChaseAngleDecreaseS16(ref _ycamYawBam, _ycamBehindBam, SmsParams.LCamera.YButtonRotateChase);
                _ycamAt = GlobalPosition + _ycamFrontAtOffset;
                _ycamPos = _ycamAt + YCamPolar(_ycamFrontR, _ycamFrontPitch, _ycamYawBam * SmsUnits.BamToRad);
                if (!more)
                    SmsYCamModeOn(); // UNK8 -> UNK10 -> doLButtonCameraOn_
                return;
            }

            case YCamPhase.LNormal:
            {
                // execCameraModeChangeProc_: what ends the mode by itself -- knocked down / hurt / dead (status bit 0x20000), the hover
                // or rocket (isMarioRocketing), the dash (FLUDD_EMITTING), the water / surf, a talk, the shine, a cutscene hold
                bool cut = stateOfMario == MarioState.hurt || stateOfMario == MarioState.dead || stateOfMario == MarioState.talking
                    || stateOfMario == MarioState.shineGet || stateOfMario == MarioState.swimming
                    || (!SmsParams.LCamera.StaysOnRocketing && stateOfMario == MarioState.fluddRocket) // DEVIATION: he glides in the mode
                    || _smsInWater || _fluddDashing || CameraLocked;
                if (cut)
                {
                    SmsYCamModeOff(true, "the state");
                    return;
                }
                // Y again, or A / B, once unk282 is out and the inbetween is over
                if (_ycamCancelPressed && _ycamLock == 0 && _ycamInbet == 0)
                {
                    _ycamCancelPressed = false;
                    SmsYCamModeOff(false, "Y / A / B");
                    return;
                }
                int rem = _ycamInbet;
                // the params blend over the inbetween (inbetweenData): the FOV from the rig's to 60
                _ycamFov = rem > 0 ? YCamChaseConst(_ycamFov, SmsParams.LCamera.Fovy, rem) : SmsParams.LCamera.Fovy;
                // ctrlLButtonCamera_: stickX = -mCompSPos[4], stickY = -mCompSPos[5] (the pad's main stick, up positive there)
                float stickX = -_ycamStick.X;
                float stickY = _ycamStick.Y;
                _ycamYawBam = (short)(_ycamYawBam + (short)(stickX * SmsParams.LCamera.YAngleManualSpeed)); // s16 thing = stick x speed; mYaw += thing
                _ycamRatio = Mathf.Clamp(_ycamRatio - stickY * SmsParams.LCamera.XRotRatioManualSpeed, 0f, 1f); // stick up -> the camera rises -> it looks down
                SmsYCamTargets(out Vector3 pos, out Vector3 at);
                SmsYCamInbetStep(pos, at);
                // calcPosAndAt_'s tail: mPosition / mTarget chase the inbetween's pos / at at PosChaseRate / AtChaseRate a tick
                _ycamPos = new Vector3(
                    Mathf.Lerp(_ycamPos.X, _ycamInbetPos.X, SmsParams.LCamera.PosChaseRateXZ),
                    Mathf.Lerp(_ycamPos.Y, _ycamInbetPos.Y, SmsParams.LCamera.PosChaseRateY),
                    Mathf.Lerp(_ycamPos.Z, _ycamInbetPos.Z, SmsParams.LCamera.PosChaseRateXZ));
                _ycamAt = new Vector3(
                    Mathf.Lerp(_ycamAt.X, _ycamInbetAt.X, SmsParams.LCamera.AtChaseRateXZ),
                    Mathf.Lerp(_ycamAt.Y, _ycamInbetAt.Y, SmsParams.LCamera.AtChaseRateY),
                    Mathf.Lerp(_ycamAt.Z, _ycamInbetAt.Z, SmsParams.LCamera.AtChaseRateXZ));
                return;
            }

            case YCamPhase.BlendOut:
            {
                // changeCamMode_(-1): the normal camera -- the rig, its yaw already behind him -- with the inbetween's constant steps;
                // no chase lag here, the hand-over must land on the rig's live pose
                if (camera == null)
                {
                    SmsYCamFinish();
                    return;
                }
                int rem = _ycamInbet;
                _ycamFov = YCamChaseConst(_ycamFov, camera.Fov, rem);
                _ycamXform = rem <= 1 ? camera.GlobalTransform : _ycamXform.InterpolateWith(camera.GlobalTransform, 1f / rem);
                _ycamPos = _ycamXform.Origin;
                _ycamAt = _ycamPos - _ycamXform.Basis.Z;
                _ycamInbet--;
                if (_ycamInbet <= 0)
                    SmsYCamFinish();
                return;
            }
        }
    }

    /// <summary>The L camera's targets this tick (metres): the look-at 72 units to the side of the nozzle top (getNozzleTopPos_: the
    /// nozzle matrix's origin + 30 units along its Y column; his origin + AtOffsetY 150 without the FLUDD), the position 200 units
    /// from it at the pitch + OffsetAngleX over and the yaw + OffsetAngleY around (calcPosAndAt_ 594-614).</summary>
    private void SmsYCamTargets(out Vector3 pos, out Vector3 at)
    {
        Vector3 target;
        if (_fluddReady && _fluddNozzleParent is Node3D noz)
        {
            Transform3D g = noz.GlobalTransform;
            target = g.Origin + g.Basis.Y.Normalized() * SmsUnits.UnitsToMetres(30f);
        }
        else
            target = new Vector3(GlobalPosition.X, SmsFeetY(), GlobalPosition.Z) + Vector3.Up * SmsUnits.UnitsToMetres(SmsParams.LCamera.AtOffsetY);
        float xA = (YCamPitchBam + SmsParams.LCamera.OffsetAngleX) * SmsUnits.BamToRad;
        float yA = (_ycamYawBam + SmsParams.LCamera.OffsetAngleY) * SmsUnits.BamToRad;
        // finalAt = target - OffsetLookatXZ x (sin(yA - 90), cos(yA - 90)) = target + OffsetLookatXZ x (cos yA, -sin yA)
        at = target + SmsUnits.UnitsToMetres(SmsParams.LCamera.OffsetLookatXZ) * new Vector3(MathF.Cos(yA), 0f, -MathF.Sin(yA));
        pos = at + YCamPolar(SmsUnits.UnitsToMetres(SmsParams.LCamera.Dist), xA, yA);
    }

    /// <summary>TCameraInbetween::startCameraInbetween + calcPolarData_: the sweep starts from the current pos / at.</summary>
    private void SmsYCamInbetStart(int ticks)
    {
        _ycamInbet = ticks;
        _ycamInbetPos = _ycamPos;
        _ycamInbetAt = _ycamAt;
        YCamToPolar(_ycamAt - GlobalPosition, out _ycamInbetAtR, out _ycamInbetAtPitch, out _ycamInbetAtYaw);
        YCamToPolar(_ycamPos - _ycamAt, out _ycamInbetPosR, out _ycamInbetPosPitch, out _ycamInbetPosYaw);
    }

    /// <summary>TCameraInbetween::execCameraInbetween: while frames remain, the look-at's polar coordinates about him and the
    /// camera's about the look-at each close 1/remaining of their gap a tick (the look-at's height is the target's at once); after
    /// that the inbetween IS the target.</summary>
    private void SmsYCamInbetStep(Vector3 targetPos, Vector3 targetAt)
    {
        if (_ycamInbet <= 0)
        {
            _ycamInbetPos = targetPos;
            _ycamInbetAt = targetAt;
            return;
        }
        int rem = _ycamInbet;
        Vector3 mario = GlobalPosition;
        _ycamInbetAt = targetAt;
        YCamToPolar(targetAt - mario, out float ar, out float ap, out float ay);
        _ycamInbetAtR = YCamChaseConst(_ycamInbetAtR, ar, rem);
        _ycamInbetAtPitch = YCamChaseConst(_ycamInbetAtPitch, ap, rem);
        _ycamInbetAtYaw = YCamChaseConstAngle(_ycamInbetAtYaw, ay, rem);
        float eps = SmsUnits.UnitsToMetres(0.1f);
        if (MathF.Abs(targetAt.X - mario.X) > eps || MathF.Abs(targetAt.Z - mario.Z) > eps)
        {
            Vector3 tmp = mario + YCamPolar(_ycamInbetAtR, _ycamInbetAtPitch, _ycamInbetAtYaw);
            _ycamInbetAt.X = tmp.X;
            _ycamInbetAt.Z = tmp.Z;
        }
        YCamToPolar(targetPos - _ycamInbetAt, out float pr, out float pp, out float py);
        _ycamInbetPosR = YCamChaseConst(_ycamInbetPosR, pr, rem);
        _ycamInbetPosPitch = YCamChaseConst(_ycamInbetPosPitch, pp, rem);
        _ycamInbetPosYaw = YCamChaseConstAngle(_ycamInbetPosYaw, py, rem);
        _ycamInbetPos = _ycamInbetAt + YCamPolar(_ycamInbetPosR, _ycamInbetPosPitch, _ycamInbetPosYaw);
        _ycamInbet--;
    }

    /// <summary>The camera node takes this frame's pose (on the physics clock, like Mario and the rig).</summary>
    private void SmsYCamApplyTransform()
    {
        if (_ycamCamera == null)
            return;
        if (_ycamPhase != YCamPhase.BlendOut)
        {
            Vector3 dir = _ycamAt - _ycamPos;
            if (dir.LengthSquared() > 1e-10f)
            {
                Vector3 up = MathF.Abs(dir.Normalized().Y) > 0.999f ? _ycamXform.Basis.Y : Vector3.Up;
                _ycamXform = new Transform3D(Basis.LookingAt(dir, up), _ycamPos);
            }
        }
        _ycamCamera.GlobalTransform = _ycamXform;
        _ycamCamera.Fov = _ycamFov;
        _ycamCamera.Near = _ycamPhase == YCamPhase.LNormal ? SmsUnits.UnitsToMetres(SmsParams.LCamera.NearClip) : camera.Near;
    }

    /// <summary>playerControl() (MarioMove.cpp 2449): mFaceAngle.y = unk258 + 0x8000 - OffsetAngleY, unk258 being the CHASED camera's
    /// bearing from its look-at (Camera.hpp 167): he faces where the camera looks, 2.7 deg off its axis, and turns with it as it
    /// swings, on the ground and in the air alike -- in every status but the hang family (mStatus & 0x1FF in HANGING .. HANG_JUMPING,
    /// 2452). In the port's yaw convention (DirFromYaw = (-sin, -cos)) the + 0x8000 is absorbed. The MODEL turns with it: walkProcess()
    /// and jumpProcess() end every ground and air tick with mModelFaceAngle = mFaceAngle.y (MarioPhysics.cpp 230 / 512), so the
    /// armature follows the look through a jump too; rotateJumping() then writes the spin's own absolute angle over it (MarioJump.cpp
    /// 1027: +-timer x 4096, the port's RotateSpinJump) and uTurnJumping() adds its 0x8000 (400: the side flip's offset is kept).</summary>
    private void SmsYCamFaceLook()
    {
        if (stateOfMario == MarioState.ledgeHang || stateOfMario == MarioState.ledgeClimb || stateOfMario == MarioState.ledgeDescend
            || stateOfMario == MarioState.ledgeHopUp)
            return; // HANGING / ASCEND / DESCEND / HANG_JUMPING: playerControl leaves mFaceAngle.y alone
        Vector3 d = _ycamPos - _ycamAt;
        if (new Vector2(d.X, d.Z).LengthSquared() < 1e-8f)
            return;
        float bearing = MathF.Atan2(d.X, d.Z);
        bool hadFace = _smsFaceValid;
        float yawBefore = hadFace ? BaseYawFromDir(SmsMath.DirFromYaw(_smsFaceYaw)) : 0f;
        _smsFaceYaw = SmsMath.WrapAngle(bearing - SmsParams.LCamera.OffsetAngleY * SmsUnits.BamToRad);
        _smsFaceValid = true;
        // in the air the launch-latched source (2): SmsAirTick then keeps this face -- and projects the velocity onto it, the game's
        // mForwardVel along mFaceAngle -- instead of re-latching it from the velocity
        _smsFaceSource = IsOnFloor() ? 1 : 2;
        Vector3 face = SmsMath.DirFromYaw(_smsFaceYaw);
        lastFacingDirection = face;
        _smsLatchedFacing = face;
        // mModelFaceAngle = mFaceAngle.y: the armature turns with the look on the ground AND in the air (pass 17b wrote it on the
        // ground only -- his velocity bent with the look, his body did not). Nothing in the air path rotates the armature otherwise.
        if (stateOfMario == MarioState.SpinJump)
            return; // rotateJumping(): the spin's own absolute model angle -- RotateSpinJump owns the armature
        float yaw = BaseYawFromDir(face);
        if (stateOfMario == MarioState.sideFlip && hadFace)
            yaw += SmsMath.WrapAngle(armature.Rotation.Y - yawBefore); // uTurnJumping(): the model's 0x8000 (the port's own offset) rides on the face
        armature.Rotation = armature.Rotation with { Y = yaw };
    }

    /// <summary>considerMarioStick() while mNeutralKeyTimer runs: Mario's stick is zero for the first 0x28 counts after a mode change and
    /// ramps back to full over the remaining 0x14 (CLBCalcRatio(0x28, 0x3c, frame)).</summary>
    private float SmsYCamMarioStickScale()
    {
        if (_ycamNeutralTicks <= 0)
            return 1f;
        int elapsed = SmsParams.LCamera.NeutralKeyTicks - _ycamNeutralTicks;
        if (elapsed <= SmsParams.LCamera.NeutralKeyDeadTicks)
            return 0f;
        return Mathf.Clamp((float)(elapsed - SmsParams.LCamera.NeutralKeyDeadTicks) / (SmsParams.LCamera.NeutralKeyTicks - SmsParams.LCamera.NeutralKeyDeadTicks), 0f, 1f);
    }

    /// <summary>The ground block's side of the mode: his stick is the camera's (mCompSPos[0],[1] are never written in the mode) and A /
    /// B / Y are its buttons, so he stands where the L camera found him -- the game's WAIT with a zero stick, which stops a run in
    /// a handful of ticks anyway -- and nothing that reads A, B, R-tap or the stick runs this frame.</summary>
    /// <summary>The ground block holds him while the mode is on, and through the frame whose A / B / Y ended it.</summary>
    private bool SmsYCamHoldsMario() => _ycamOn || _ycamHoldThisFrame;

    private void SmsYCamHoldMario()
    {
        velocity.X = 0f;
        velocity.Z = 0f;
        velocity.Y = -2f;
        jumpBuffer = 0;
        spinBuffer = 0;
        if (stateOfMario != MarioState.idle)
        {
            isSpining(false);
            if (footSparks != null)
                footSparks.Emitting = false;
            StopWaistIKHard();
            stateOfMario = MarioState.idle;
            SetMarioState(stateOfMario);
        }
    }

    /// <summary>The render hook: a cutscene hold (CameraLocked), a talk, the shine or death takes the frame before the SMS block runs,
    /// so the ticks above cannot end the mode -- cut straight back to the rig's camera here, never leave it behind this one.</summary>
    private void SmsYCamRender(double delta)
    {
        if (_ycamPhase == YCamPhase.Off)
            return;
        if (CameraLocked || stateOfMario == MarioState.talking || stateOfMario == MarioState.shineGet || stateOfMario == MarioState.dead)
        {
            if (SmsDebugPrint)
                GD.Print($"[SMS ycam] off (a cutscene hold, {stateOfMario}): a cut to the rig's camera");
            SmsYCamReset();
        }
    }
}
