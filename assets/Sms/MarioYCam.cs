using System;
using Godot;

/// <summary>
/// Mario's half of the L (Y) camera -- the game's CAMERA_MODE_L_NORMAL, its "L button camera" (CameraChange.cpp execCameraModeChangeProc_
/// 568-636, execFrontRotate_ 447, doLButtonCameraOn_ 469 / Off_ 479, setUpToLButtonCamera_ 256, changeCamModeSub_ 272; CameraNotice.cpp
/// ctrlLButtonCamera_ 208; MarioGamePad.cpp 70-88 / 135-154; MarioMove.cpp playerControl 2449; MarioUpper.cpp checkPumping 17; WaterGun.cpp
/// 219; MarioWaistCtrl MarioDraw.cpp 476).
///
/// THE CAMERA ITSELF IS THE RIG'S (assets/SunshineCamera.cs, pass 68): CAMERA_MODE_L_NORMAL is a mode of the one Camera3D, as CPolarSubCamera
/// has it -- the Y press's swing behind him (execFrontRotate_, the gap halved a tick), the kind change into the LNormal record with its 16-tick
/// inbetween, ctrlLButtonCamera_ (the nozzle top as the target, the MAIN stick turning and tilting it) through the same calcPosAndAt_ as the
/// normal camera, the Y / A / B sweep back and the state cuts, unk282 (the 60-tick lock). Before pass 68 this file ran all of that on a
/// second TopLevel Camera3D made current from the Y press; that camera, its polar inbetween and its hand-back to the rig are gone.
///
/// WHAT THE GAME KEEPS ON MARIO'S SIDE, and this file does:
///  - the pad (MarioGamePad.cpp 70-88): in the mode the MAIN stick is the camera's and his is zero (mCompSPos[0],[1] are never written), A and
///    B are MEANING_CAM_AB (nothing of his sees them: no jump, no dive, the buffered jump dropped), Y is the camera's; onNeutralMarioKey() at
///    EITHER end of the mode (changeCamModeSub_ 309 / 314): mNeutralKeyTimer = 0x3c, counted down by considerMarioStick on the pad's three
///    updateMeaning calls a 30 fps frame -> 80 ticks, his stick dead for the first 0x28 counts (53 ticks, 0.44 s) and fading back in over
///    the rest (0.22 s) -- the stick that aimed the camera does not launch him;
///  - his face (playerControl 2449): mFaceAngle.y = unk258 + 0x8000 - OffsetAngleY every tick of the mode, unk258 being the ACTUAL eye's
///    bearing about the look-at (the rig's FinalYawBam) -- he faces where the camera looks, 2.7 deg off its axis, and turns with it as it
///    swings, on the ground and in the air alike, in every status but the hang family (2452); the MODEL turns with it (walkProcess /
///    jumpProcess end every tick with mModelFaceAngle = mFaceAngle.y, MarioPhysics.cpp 230 / 512), the spin jump keeping its own absolute
///    angle (rotateJumping, MarioJump.cpp 1027) and the side flip its 0x8000 (uTurnJumping 400);
///  - the ground: he stands where the L camera found him (the game's WAIT with a zero stick); canSquat holds in the mode (MarioMove.cpp 44),
///    so R clicked / L squats him and R sprays where the camera looks; a landing inside the mode goes straight to WAIT;
///  - FLUDD: checkPumping() holds the grips (MarioUpper.cpp 17: HOLDING_PUMP at the pump clip's frame 0; the pass-17b idle-arms deviation
///    stays withdrawn, 17c), and the gun angle IS the camera's pitch (WaterGun.cpp 219; Mario.cs SmsFluddCalcGunAngle and the waist bend read
///    YCamPitchBam): looking down bends the upper body forward, looking up lifts the nozzle and the head;
///  - the Y press's status gate the port keeps (SmsYCamCanEnter): the game refuses only sleeping, the water, the hover / rocket, the dash, a
///    climb and the hip drop; the port also keeps the Y out of the ledge, the wire, the fence, the roof and the knockdowns. The rig asks for
///    it, and for the frame's Y / A / B edges, through SmsCamMarioData -- the edges are captured here BEFORE the mode eats A / B (the rig is
///    his child and ticks after him; a released action is no longer "just pressed").
/// A Y IN THE AIR works as in the game: the mode keeps his status, his stick is zero, A / B are eaten, his face is forced to the look every
/// tick -- mForwardVel runs along mFaceAngle, so the flight bends with the look (SmsAirTick projects the velocity onto the forced face).
/// Not ported: the notice lock-on (no bosses), the SEs (0x4824 / 0x4825 / 0x4826).
/// </summary>
public partial class Mario
{
    private int _ycamNeutralTicks = 0;                 // mNeutralKeyTimer (in ticks): his stick is dead, then fades in, after either end of the mode
    private float _ycamTickAccum = 0f;
    private bool _ycamWasOn = false;                   // isLButtonCamera() last frame: its edges are onNeutralMarioKey()'s
    private bool _camYPressed = false;                 // MEANING_Y this frame (a frame meaning: an edge), read before the mode eats anything
    private bool _camCancelPressed = false;            // MEANING_Y | MEANING_CAM_AB this frame
    private Vector2 _camMainStick = Vector2.Zero;      // the pad's main stick as the camera reads it (mCompSPos[4],[5]; right / down positive here)
    private float _ycamDbgTimer = 0f;

    /// <summary>isLButtonCamera(): the rig is in the L camera -- the mode proper, from the tick it turns on (its inbetween included) to the
    /// tick it leaves; not the swing before it, not the sweep back after it.</summary>
    private bool SmsYCamOn => UseSmsPhysics && _camera != null && _camera.IsLCamera;

    /// <summary>mCurrentTarget.mPitch (BAM): the L camera's pitch -- the gun angle and the waist bend in the mode.</summary>
    private float YCamPitchBam => _camera != null ? _camera.CurPitchBam : 0f;

    /// <summary>The port's status gate for the Y press (the rig's execCameraModeChangeProc_ reads it as SmsCamMarioData.YCamCanEnter; the
    /// rig's own unk282 lock and inbetween checks are its). None of the states the mode change refuses (588: the water, the hover or
    /// rocket, the dash; 453: the hip drop) nor a demo's hold; on the ground a standing / running / landing status; in the air any launched
    /// or falling state doJumping() steers (the game takes a Y in a jump too), not the ROCKET_LANDING fall after a hover
    /// (isMarioRocketing). The goop's OIL_RUN counts as standing (the game's gate never refuses it); its belly slide does not (the hold
    /// would stop a slide the game lets run).</summary>
    private bool SmsYCamCanEnter()
    {
        if (!UseSmsPhysics || camera == null || _camera == null || armature == null)
            return false;
        if (_camera.Mode != SunshineCamera.CamMode.Normal)
            return false;
        if (_smsInWater || _fluddDashing || CameraLocked)
            return false;
        if (!IsOnFloor())
            return SmsAirSteers(stateOfMario) // the jumps, the flips, the dives, the rolls, a fall off an edge; not the hip drop, a wall slide, a hang
                && !_smsRocketLanding;
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
            case MarioState.oilRun: // standing / running on the goop: OIL_RUN is a running status the game's gate never refuses (pass 68)
                return true;
        }
        return false;
    }

    /// <summary>For the rig: the camera yaw that sits behind him -- the game's *gpMarioAngleY - 0x8000, his FACE (mFaceAngle.y), never
    /// his velocity (which points to his flank in the squat's sidestep). In the rig's convention (yaw = atan2(-d.x, -d.z) of the
    /// direction the arm points away from) that is _smsFaceYaw itself.</summary>
    public bool SmsBehindYaw(out float yaw)
    {
        yaw = _smsFaceYaw;
        return UseSmsPhysics && _smsFaceValid;
    }

    /// <summary>Once a physics frame, at the top of the SMS block (the rig, his child, ticks after him): the pad's frame meanings for the
    /// camera, captured before the mode eats A / B; the mode's edges (onNeutralMarioKey) and mNeutralKeyTimer; in the mode A / B are the
    /// camera's -- dropped before anything of his sees them, the buffered jump too -- and his face is the eye's bearing.</summary>
    private void SmsYCamFrame(double delta)
    {
        if (!UseSmsPhysics)
            return;
        bool padDead = _smsPadDisabledTicks > 0;   // pass 114: TMarioGamePad::mDisabledFrames -- no MEANING_Y / MEANING_CAM_AB while the pad is dead
        _camYPressed = !padDead && Input.IsActionJustPressed("button_y");
        _camCancelPressed = !padDead && (_camYPressed || Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space") || Input.IsActionJustPressed("button_b"));
        bool on = SmsYCamOn;
        if (on != _ycamWasOn)
        {
            _ycamWasOn = on;
            _ycamNeutralTicks = SmsParams.LCamera.NeutralKeyTicks; // onNeutralMarioKey() at both ends of the mode (changeCamModeSub_ 309 / 314)
            _ycamDbgTimer = 0f;
            if (SmsDebugPrint)
                GD.Print(on
                    ? $"[SMS ycam] on: his stick is the camera's, A / B / Y its buttons; he faces the look (gun angle = its pitch), the grips held; his stick comes back {SmsParams.LCamera.NeutralKeyDeadTicks / SmsUnits.TicksPerSecond:F2} s after the mode ends"
                    : $"[SMS ycam] off: his own again -- the stick dead {SmsParams.LCamera.NeutralKeyDeadTicks / SmsUnits.TicksPerSecond:F2} s then fading in over {(SmsParams.LCamera.NeutralKeyTicks - SmsParams.LCamera.NeutralKeyDeadTicks) / SmsUnits.TicksPerSecond:F2} s");
        }
        if (_ycamNeutralTicks > 0)
        {
            int n = SmsUnits.TicksFor(delta, ref _ycamTickAccum);
            _ycamNeutralTicks = Math.Max(0, _ycamNeutralTicks - n);
        }
        if (!on)
            return;
        // MEANING_CAM_AB: in the mode A and B are the camera's, not MEANING_A / MEANING_B -- nothing of Mario's sees them this frame (no jump,
        // no dive, no A-held lift), the frame whose press ENDS the mode included (the rig reads the edge from _camCancelPressed above). The
        // input block already buffered that press as a jump: drop it.
        Input.ActionRelease("button_a");
        Input.ActionRelease("key_space");
        Input.ActionRelease("button_b");
        jumpBuffer = 0;
        SmsYCamFaceLook(); // in every status but the hang family (playerControl 2451): on the ground and through a jump
        if (SmsDebugPrint)
        {
            _ycamDbgTimer += (float)delta;
            if (_ycamDbgTimer >= 1f)
            {
                _ycamDbgTimer = 0f;
                GD.Print($"[SMS ycam] camera bearing {SmsMath.WrapAngle(_camera.FinalYawBam * SmsUnits.BamToRad) * 180f / MathF.PI:F0} deg, pitch {YCamPitchBam * 180f / 32768f:F1} deg; main stick ({_camMainStick.X:F2}, {-_camMainStick.Y:F2}); gun angle {_fluddGunAngle:F0} BAM, upper {_fluddUpper}, R {_fluddR:F2}; face {_smsFaceYaw * 180f / MathF.PI:F0} deg; state {stateOfMario}");
            }
        }
    }

    /// <summary>playerControl() (MarioMove.cpp 2449): mFaceAngle.y = unk258 + 0x8000 - OffsetAngleY, unk258 being the ACTUAL camera's
    /// bearing from its look-at (calcExternalData_; the rig's FinalYawBam): he faces where the camera looks, 2.7 deg off its axis, and
    /// turns with it as it swings, on the ground and in the air alike -- in every status but the hang family (mStatus & 0x1FF in HANGING ..
    /// HANG_JUMPING, 2452). In the port's yaw convention (DirFromYaw = (-sin, -cos)) the + 0x8000 is absorbed. The MODEL turns with it:
    /// walkProcess() and jumpProcess() end every ground and air tick with mModelFaceAngle = mFaceAngle.y (MarioPhysics.cpp 230 / 512), so the
    /// armature follows the look through a jump too; rotateJumping() then writes the spin's own absolute angle over it (MarioJump.cpp 1027:
    /// +-timer x 4096, the port's RotateSpinJump) and uTurnJumping() adds its 0x8000 (400: the side flip's offset is kept).</summary>
    private void SmsYCamFaceLook()
    {
        if (stateOfMario == MarioState.ledgeHang || stateOfMario == MarioState.ledgeClimb || stateOfMario == MarioState.ledgeDescend
            || stateOfMario == MarioState.ledgeHopUp)
            return; // HANGING / ASCEND / DESCEND / HANG_JUMPING: playerControl leaves mFaceAngle.y alone
        float bearing = _camera.FinalYawBam * SmsUnits.BamToRad;
        bool hadFace = _smsFaceValid;
        float yawBefore = hadFace ? BaseYawFromDir(SmsMath.DirFromYaw(_smsFaceYaw)) : 0f;
        _smsFaceYaw = SmsMath.WrapAngle(bearing - _camera.OffsetAngleYBam * SmsUnits.BamToRad);
        _smsFaceValid = true;
        // in the air the launch-latched source (2): SmsAirTick then keeps this face -- and projects the velocity onto it, the game's
        // mForwardVel along mFaceAngle -- instead of re-latching it from the velocity
        _smsFaceSource = IsOnFloor() ? 1 : 2;
        Vector3 face = SmsMath.DirFromYaw(_smsFaceYaw);
        lastFacingDirection = face;
        _smsLatchedFacing = face;
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
    /// B / Y are its buttons, so he stands where the L camera found him -- the game's WAIT with a zero stick, which stops a run in a
    /// handful of ticks anyway -- and nothing that reads A, B, R-tap or the stick runs this frame. The frame whose A / B / Y ENDS the mode
    /// is still the mode's here (the rig reads that edge after him and turns the camera off for the next frame; the press itself was
    /// eaten above).</summary>
    private bool SmsYCamHoldsMario() => SmsYCamOn;

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
}
