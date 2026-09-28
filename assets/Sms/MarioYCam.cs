using System;
using Godot;

/// <summary>
/// The Y camera -- the game's CAMERA_MODE_L_NORMAL, its "L button camera" (CPolarSubCamera: doLButtonCameraOn_ / Off_,
/// CameraChange.cpp 469 / 479; ctrlLButtonCamera_, CameraNotice.cpp 207; rotateY_ByStickX_ / rotateX_ByStickY_, cameragc.cpp 332 /
/// 344; calcPosAndAt_, cameragc.cpp 464), with the shipped /Camera/kindLNormal.prm (params.szs). Y from a normal camera on the
/// ground enters it; Y, A or B leave it once the 60-frame lock has run out; leaving the ground, the water, a hit or the dash
/// throw him out silently. The stick drives the camera, not Mario: yaw at 300 BAM a frame, the pitch as a 0..1 ratio at 0.01 a
/// frame (stick up = the camera rises = it looks DOWN, the game's own inversion). The camera pivots about the nozzle top (30 units
/// up the nozzle_center joint's Y; his origin + 150 without the FLUDD), 200 units from a look-at shifted 72 units to the side
/// (the over-the-shoulder framing), its elevation the pitch + 12.7 deg, its bearing the yaw - 2.7 deg, and chases those at
/// 0.1 / 0.08 a frame. Mario faces the look (mFaceAngle.y = yaw + 0x8000 - OffsetAngleY, MarioMove.cpp 2449), stands, holds the
/// pump, sprays with R, and the gun angle IS the pitch (calcGunAngle's first branch): looking down bends the chest forward
/// (MarioWaistCtrl), looking up lifts the nozzle and the head. The camera runs at the game's 30 fps.
/// </summary>
public partial class Mario
{
    // /Camera/kindLNormal.prm as shipped (params.szs, params/camera/kindlnormal.prm) and camera_normal.prm
    private const float YCamFovy = 60f;
    private const float YCamNearClip = 10f;                             // units
    private const float YCamDist = 200f;                                // DistMin = DistMax: units from the look-at
    private const int YCamXAngleMin = -12800, YCamXAngleMax = 10000;   // the pitch (BAM): the camera 70.3 deg under the target .. 54.9 over
    private const float YCamXRotRatioManualSpeed = 0.01f;              // the ratio a frame at full stick
    private const int YCamYAngleManualSpeed = 300;                     // BAM a frame at full stick (XMin = XMax)
    private const float YCamAtOffsetY = 150f;                          // no FLUDD: the target this far over his origin
    private const int YCamOffsetAngleX = 2306, YCamOffsetAngleY = -489;   // 12.7 deg over the pitch; -2.7 deg on the bearing
    private const float YCamOffsetLookatXZ = -72.021484f;              // the look-at's sideways shift (units)
    private const float YCamPosChaseRateXZ = 0.1f, YCamPosChaseRateY = 0.08f; // PosChaseRate / AtChaseRate, a frame
    private const int YCamInbetFrames = 16;                             // mSLInbetLNormal: the blend in and out
    private const int YCamLockFrames = 60;                              // unk282: Y / A / B do nothing for 60 frames after a change
    private const float YCamFps = 30f;                                  // the camera runs once a rendered frame

    private bool _ycamOn = false;             // isLButtonCamera(): Mario's side of the mode
    private float _ycamYaw = 0f;              // mCurrentTarget.mYaw (BAM): the camera's bearing from the target; unbounded
    private float _ycamRatio = 0f;            // mCurrentTarget.unk28: the pitch as a 0..1 ratio
    private int _ycamLock = 0;                // unk282 (30 fps frames)
    private int _ycamInbet = 0;               // frames left of the blend in (physics side)
    private float _ycamBlend = 1f;            // the render side's blend factor 0..1 (in or out)
    private bool _ycamBlendOut = false;       // the camera is blending back to the normal camera
    private float _ycamAccum = 0f;
    private Camera3D _ycamCamera;
    private Vector3 _ycamPos, _ycamAt;        // the chased position and look-at (metres)
    private Transform3D _ycamFrom;            // where a blend started
    private bool _ycamPoseValid = false;

    private float YCamPitchBam => Mathf.Lerp(YCamXAngleMin, YCamXAngleMax, _ycamRatio);

    /// <summary>The Y press that enters: a normal camera, the ground, not in water, not the dash, none of the frozen states, the
    /// lock run out (unk282 == 0). The game keeps his status; here he stands (the stick starves him anyway).</summary>
    private bool SmsYCamCanEnter()
    {
        if (!UseSmsPhysics || _ycamOn || _ycamBlendOut || _ycamLock > 0 || camera == null || !IsOnFloor() || _smsInWater || _fluddDashing)
            return false;
        switch (stateOfMario)
        {
            case MarioState.idle:
            case MarioState.walking:
            case MarioState.running:
            case MarioState.sprinting:
            case MarioState.pivot:
            case MarioState.landing:
                return true;
        }
        return false;
    }

    /// <summary>doLButtonCameraOn_(): the lock, the blend, the mode. setUpToLButtonCamera_(): the pitch ratio starts where the
    /// pitch is -OffsetAngleX (the camera level with the target); the yaw is the camera's current bearing (mCurrentTarget.mYaw
    /// carries over). SE 0x4824.</summary>
    private void SmsYCamEnter()
    {
        _ycamOn = true;
        _ycamBlendOut = false;
        _ycamLock = YCamLockFrames;
        _ycamInbet = YCamInbetFrames;
        _ycamBlend = 0f;
        _ycamAccum = 0f;
        Vector3 d = camera.GlobalPosition - GlobalPosition;
        _ycamYaw = MathF.Atan2(d.X, d.Z) / SmsUnits.BamToRad;
        _ycamRatio = Mathf.Clamp((float)(-YCamOffsetAngleX - YCamXAngleMin) / (YCamXAngleMax - YCamXAngleMin), 0f, 1f);
        if (_ycamCamera == null)
        {
            _ycamCamera = new Camera3D { Name = "SmsYCamera", Fov = YCamFovy, Near = SmsUnits.UnitsToMetres(YCamNearClip), Far = camera.Far };
            AddChild(_ycamCamera);
        }
        _ycamFrom = camera.GlobalTransform;
        _ycamCamera.GlobalTransform = _ycamFrom;
        SmsYCamTargets(out _ycamPos, out _ycamAt);
        _ycamPoseValid = true;
        _ycamCamera.MakeCurrent();
        velocity.X = 0f;
        velocity.Z = 0f;
        if (stateOfMario != MarioState.idle)
        {
            stateOfMario = MarioState.idle;
            SetMarioState(stateOfMario);
        }
        if (SmsDebugPrint)
            GD.Print($"[SMS ycam] on: bearing {_ycamYaw * SmsUnits.BamToRad * 180f / MathF.PI:F0} deg, pitch {YCamPitchBam * 180f / 32768f:F1} deg (ratio {_ycamRatio:F2}), target {(_fluddReady ? "the nozzle top" : "his origin + 150")}; stick: left/right turns, up = look down (the game's); Y / A / B leave after {YCamLockFrames} frames");
    }

    /// <summary>doLButtonCameraOff_(): the lock again, the blend back to the normal camera (which kept running); Mario is his own at
    /// once. SE 0x4825 on a cancel, none when a state threw him out.</summary>
    private void SmsYCamLeave(string why)
    {
        _ycamOn = false;
        _ycamLock = YCamLockFrames;
        _ycamBlendOut = true;
        _ycamBlend = 0f;
        _ycamFrom = _ycamCamera != null ? _ycamCamera.GlobalTransform : camera.GlobalTransform;
        if (SmsDebugPrint)
            GD.Print($"[SMS ycam] off ({why})");
    }

    /// <summary>One physics frame in the mode (Mario's side; the camera's 30 fps steps). He stands where he is, faces the look,
    /// and the stick turns the camera. True while the frame is his (the caller ends it).</summary>
    private bool SmsYCamFrame(double delta)
    {
        // the camera's frames: unk282 and the blend count down, the stick turns the target angles (rotateY_ByStickX_ /
        // rotateX_ByStickY_: stickX = -mCompSPos[4], stickY = -mCompSPos[5])
        _ycamAccum += (float)(delta * YCamFps);
        int n = (int)MathF.Floor(_ycamAccum + 1e-4f);
        _ycamAccum -= n;
        for (int i = 0; i < n; i++)
        {
            if (_ycamLock > 0)
                _ycamLock--;
            if (_ycamInbet > 0)
                _ycamInbet--;
            float stickX = -_smsStickH, stickY = -_smsStickV;
            _ycamYaw += stickX * YCamYAngleManualSpeed;
            _ycamRatio = Mathf.Clamp(_ycamRatio - stickY * YCamXRotRatioManualSpeed, 0f, 1f);
        }
        // waitProcess(): he stands
        velocity.X = 0f;
        velocity.Z = 0f;
        velocity.Y = -2f;
        // execCameraModeChangeProc_: the states that end it silently
        bool thrown = !IsOnFloor() || _smsInWater || _fluddDashing || stateOfMario == MarioState.hurt || stateOfMario == MarioState.dead
            || stateOfMario == MarioState.talking || stateOfMario == MarioState.shineGet || stateOfMario == MarioState.swimming;
        if (thrown)
        {
            SmsYCamLeave("the state");
            return false;
        }
        // Y again, or A / B (MEANING_CAM_AB), once the lock is out and the blend in is done
        bool cancel = Input.IsActionJustPressed("button_y") || Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("button_b")
            || Input.IsActionJustPressed("key_space");
        if (cancel && _ycamLock == 0 && _ycamInbet == 0)
        {
            SmsYCamLeave("cancelled");
            return true; // this frame's A / B is the camera's, not a jump
        }
        // mFaceAngle.y = unk258 + 0x8000 - OffsetAngleY: he faces the look, 2.7 deg off the camera's axis
        _smsFaceYaw = SmsMath.WrapAngle((_ycamYaw + YCamOffsetAngleY) * SmsUnits.BamToRad);
        _smsFaceValid = true;
        _smsFaceSource = 1;
        Vector3 face = SmsMath.DirFromYaw(_smsFaceYaw);
        lastFacingDirection = face;
        _smsLatchedFacing = face;
        armature.Rotation = armature.Rotation with { Y = BaseYawFromDir(face) };
        return true;
    }

    /// <summary>The camera's targets this frame (metres): the look-at 72 units to the side of the nozzle top, the position 200
    /// units from it at the pitch + OffsetAngleX over, the yaw + OffsetAngleY around (calcPosAndAt_).</summary>
    private void SmsYCamTargets(out Vector3 pos, out Vector3 at)
    {
        Vector3 target;
        if (_fluddReady && _fluddNozzleParent is Node3D noz)
        {
            // getNozzleTopPos_(): the nozzle_center matrix's origin, 30 units along its Y column
            Transform3D g = noz.GlobalTransform;
            target = g.Origin + g.Basis.Y.Normalized() * SmsUnits.UnitsToMetres(30f);
        }
        else
            target = new Vector3(GlobalPosition.X, SmsFeetY(), GlobalPosition.Z) + Vector3.Up * SmsUnits.UnitsToMetres(YCamAtOffsetY);
        float xA = (YCamPitchBam + YCamOffsetAngleX) * SmsUnits.BamToRad;
        float yA = (_ycamYaw + YCamOffsetAngleY) * SmsUnits.BamToRad;
        // finalAt = target - OffsetLookatXZ x (sin(yA - 90), cos(yA - 90)) = target + OffsetLookatXZ x (cos yA, -sin yA)
        at = target + SmsUnits.UnitsToMetres(YCamOffsetLookatXZ) * new Vector3(MathF.Cos(yA), 0f, -MathF.Sin(yA));
        // CLBPolarToCross(at, dist, xA, yA): (cos x sin y, sin x, cos x cos y)
        pos = at + SmsUnits.UnitsToMetres(YCamDist) * new Vector3(MathF.Cos(xA) * MathF.Sin(yA), MathF.Sin(xA), MathF.Cos(xA) * MathF.Cos(yA));
    }

    /// <summary>The render frame: chase the targets (PosChaseRate 0.1 XZ / 0.08 Y a 30 fps frame, rescaled to this frame), look at
    /// them, and blend in from -- or out to -- the normal camera over 16 frames.</summary>
    private void SmsYCamRender(double delta)
    {
        if (_ycamCamera == null || (!_ycamOn && !_ycamBlendOut))
            return;
        float frames = (float)(delta * YCamFps);
        _ycamBlend = Mathf.Min(1f, _ycamBlend + frames / YCamInbetFrames);
        if (_ycamBlendOut)
        {
            // back to the normal camera, which kept its own place
            Transform3D live = camera.GlobalTransform;
            _ycamCamera.GlobalTransform = _ycamFrom.InterpolateWith(live, _ycamBlend);
            if (_ycamBlend >= 1f)
            {
                _ycamBlendOut = false;
                camera.MakeCurrent();
            }
            return;
        }
        SmsYCamTargets(out Vector3 pos, out Vector3 at);
        float kxz = 1f - MathF.Pow(1f - YCamPosChaseRateXZ, frames);
        float ky = 1f - MathF.Pow(1f - YCamPosChaseRateY, frames);
        _ycamPos = new Vector3(Mathf.Lerp(_ycamPos.X, pos.X, kxz), Mathf.Lerp(_ycamPos.Y, pos.Y, ky), Mathf.Lerp(_ycamPos.Z, pos.Z, kxz));
        _ycamAt = new Vector3(Mathf.Lerp(_ycamAt.X, at.X, kxz), Mathf.Lerp(_ycamAt.Y, at.Y, ky), Mathf.Lerp(_ycamAt.Z, at.Z, kxz));
        Transform3D want = new Transform3D(Basis.LookingAt(_ycamAt - _ycamPos, Vector3.Up), _ycamPos);
        _ycamCamera.GlobalTransform = _ycamBlend < 1f ? _ycamFrom.InterpolateWith(want, _ycamBlend) : want;
    }
}
