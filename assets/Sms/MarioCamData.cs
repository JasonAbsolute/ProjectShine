using System;
using Godot;

/// <summary>What the game's camera reads off Mario each tick (TCameraMarioData, CameraMarioData.cpp; the status bits of
/// execCameraModeChangeProc_, CameraChange.cpp 568-826; isMarioReadyGun_ / isMarioAimWithGun_ / isMarioCrabWalk_, cameragc.cpp
/// 372-389; the pad's frame meanings and main stick the L camera reads, MarioGamePad.cpp / CameraNotice.cpp 208), filled once a
/// physics frame by <see cref="Mario.SmsCameraData"/> for assets/SunshineCamera.cs (pass 53; the L camera's fields pass 68).
/// Distances are game units, angles s16 BAM, in the camera's conventions.</summary>
public struct SmsCamMarioData
{
    public Vector3 PosU;                // *gpMarioPos: his origin (the feet), units
    public int AngleY;                  // *gpMarioAngleY: mFaceAngle.y; "behind him" is AngleY - 0x8000
    public float MoveH, MoveV;          // mFrameMoveDistHorizontal / Vertical: the displacement per 30 fps frame SQUARED, clamped 100 (0 in HANGING / ASCEND)
    public bool GoDown;                 // isMarioGoDown: moving, and lower than last frame
    public bool TouchGround;            // SMS_IsMarioTouchGround4cm
    public bool Rocketing;              // isMarioRocketing: ROCKET / ROCKET_LANDING
    public bool HoverNozzle;            // mCurrentNozzle == 4 (the hover): HOVERING, else ROCKET_JUMP
    public bool Dashing;                // MARIO_FLAG_FLUDD_EMITTING: the turbo dash proper
    public bool Swimming;               // status & MARIO_STATUS_FLAG_SWIMMING
    public bool SwimJump;               // (prevStatus & SWIMMING) && (status & JUMPING): the dash's leap off the water
    public bool RoofFamily;             // status & 0x200000: HANG_ROOF / WAIT_ROOF / MOVE_ROOF / KICK_ROOF (the roof hang; the ledge hang is NOT this)
    public bool KickRoofRollUp;         // MARIO_STATUS_KICK_ROOF_ROLL_UP: +260 on the look height, no HANG kind
    public bool FenceFamily;            // FENCE_MOVE / PUNCH / CATCH / JUMP_CATCH
    public bool HipDrop;                // MARIO_STATUS_HIP_DROP: the airborne hip drop (the hover + the fall), not its landing (HIP_ATTACK_END)
    public bool WallJumpOrSlide;        // WALL_JUMP / WALL_SLIDE
    public bool OnWireTop;              // MarioState.wire on TOP (WIRE_WAIT / WALK / the Wait<->SWait turns / Return): kindWire
    public bool OnWireHang;             // MarioState.wire hanging or rolling under it (WIRE_HANG family): kindWireHang
    public bool HangOrAscend;           // HANGING / ASCEND: calcAndSetMarioData zeroes the move distances
    public bool ReadyGun;               // isMarioReadyGun_: has the FLUDD and a SQUAT status (the only statuses with type bit 0x8000)
    public bool AimR;                   // MEANING_R held: with ReadyGun, isMarioAimWithGun_
    public bool CamL;                   // MEANING_CAM_L / the L trigger held: isMarioCrabWalk_ with ReadyGun, the LFollow magnifier otherwise
    public float AimRatio;              // unk1C: the nozzle's gun angle over its LAngleMin, 0..1 (how far the squat aim is lifted)
    public bool Downed;                 // status bit 0x20000: knocked down / hurt / dead -- the L camera's one-tick cut (CameraChange.cpp 599)
    public bool InWater;                // MARIO_FLAG_IN_WATER (SMS_CheckMarioFlag(2)): a refusing state for the L camera (588)
    public bool Climbing;               // isMarioClimb: the fence and the wire statuses -- a refusing state for the L camera
    public bool HasFludd;               // MARIO_FLAG_HAS_FLUDD and the pack on him: ctrlLButtonCamera_ targets the nozzle top
    public Vector3 NozzleTopU;          // getNozzleTopPos_: the nozzle matrix's origin + 30 units up its Y column, units
    public Vector2 MainStick;           // the pad's main stick (mCompSPos[4],[5] after PADClamp / JUTGamePad), right / down positive here: the L camera's stick
    public bool YPressed;               // MEANING_Y this frame (a frame meaning: the press's edge)
    public bool CamCancelPressed;       // MEANING_Y | MEANING_CAM_AB this frame: what leaves the L camera (read before Mario's half eats A / B)
    public bool YCamCanEnter;           // Mario's status gate for the Y press (MarioYCam.SmsYCamCanEnter): the rig's lock and inbetween checks are its own
    public bool Locked;                 // CameraLocked / the sign talk / the shine demo / dead: a demo holds him
    public bool Talking;                // isTalkModeNow: the NPC talk from the B press to the balloon's fade (the talk kind holds the camera meanwhile; pass 86)
    public bool Teleported;             // the frame jumped him (a respawn, the intro's spawn): the camera warps behind him as a stage load does
    public bool Debug;                  // SmsDebugPrint
}

public partial class Mario
{
    private Vector3 _camPrevPos;
    private bool _camPrevValid = false;
    private bool _camPrevSwimming = false;

    /// <summary>The camera's Mario data for this physics frame (the game reads it every 120 Hz tick, but Mario's position only
    /// changes on his own perform -- once a 30 fps frame -- so the per-tick values are the frame's). The displacement is measured
    /// here and scaled to a 30 fps frame: the game's move distances are per frame because gpMarioOriginal->getPrevPosition()
    /// only moves when Mario performs (CUE_MOVEMENT_GATE_A | B, MarDirectorDirect.cpp 127-146), while the camera performs on
    /// every tick.</summary>
    public SmsCamMarioData SmsCameraData(double delta)
    {
        var d = new SmsCamMarioData();
        Vector3 pos = new Vector3(GlobalPosition.X, SmsFeetY(), GlobalPosition.Z);
        d.PosU = pos * SmsUnits.UnitsPerMetre;
        Vector3 disp = _camPrevValid ? pos - _camPrevPos : Vector3.Zero;
        float toFrame = delta > 1e-6 ? (1f / 30f) / (float)delta : 2f; // a physics frame's move -> a 30 fps frame's
        d.Teleported = _camPrevValid && disp.Length() > 20f;             // 20 m in one frame: nothing of his moves so; a respawn / spawn does
        _camPrevPos = pos;
        _camPrevValid = true;
        Vector3 f = disp * toFrame * SmsUnits.UnitsPerMetre;
        d.HangOrAscend = stateOfMario == MarioState.ledgeHang || stateOfMario == MarioState.ledgeClimb;
        if (d.HangOrAscend || d.Teleported)
        {
            d.MoveH = 0f;
            d.MoveV = 0f;
        }
        else
        {
            d.MoveH = MathF.Min(100f, f.X * f.X + f.Z * f.Z);
            d.MoveV = MathF.Min(100f, f.Y * f.Y);
        }
        d.GoDown = d.MoveV != 0f && disp.Y < 0f;
        d.TouchGround = IsOnFloor();
        // *gpMarioAngleY: his face as the game has it; the port's _smsFaceYaw IS "behind him" in the camera's bearing convention
        // (SmsBehindYaw: atan2 of the direction the camera sits off him), so the face is that + 0x8000
        d.AngleY = SmsCamFaceAngleBam();
        d.Rocketing = stateOfMario == MarioState.fluddRocket || _smsRocketLanding;
        d.HoverNozzle = _fluddNozzle == SmsNozzle.Hover;
        d.Dashing = _fluddDashing;
        d.Swimming = stateOfMario == MarioState.swimming;
        d.SwimJump = _camPrevSwimming && !d.Swimming && !IsOnFloor() && SmsAirSteers(stateOfMario);
        if (d.Swimming || IsOnFloor())
            _camPrevSwimming = d.Swimming;
        d.RoofFamily = stateOfMario == MarioState.roofHang;
        d.KickRoofRollUp = false; // KICK_ROOF_ROLL_UP / ROLL_DOWN (the kick's roll onto a roof) are not in the port's roof hang (pass 52)
        d.FenceFamily = stateOfMario == MarioState.fence;
        // MARIO_STATUS_HIP_DROP (0x8008A9) is the AIRBORNE hip drop only -- the hover and the fall; the landing is HIP_ATTACK_END, then SLIP_END
        // (hipAttackEnd -> jumpEndCommon(ANIM_HIPED, SLIP_END)): WAITING-type statuses the camera's ladder sees as the normal kind. The port's
        // groundPoundLanding (ma_hiped, then ma_slped) is that landing, so it is NOT the hip drop here (pass 69: it kept the HipAttack kind
        // through the landing pose -- the look-at frozen 100 u under his feet, the eye sunk to the ground check's 144 u for ~0.9 s)
        d.HipDrop = stateOfMario == MarioState.groundPoundStartup || stateOfMario == MarioState.groundPoundFalling;
        d.WallJumpOrSlide = stateOfMario == MarioState.wallJump || stateOfMario == MarioState.wallSlide;
        bool onWire = stateOfMario == MarioState.wire;
        d.OnWireHang = onWire && (_smsWire == SmsWireState.Hanging || _smsWire == SmsWireState.Rolling || _smsWire == SmsWireState.WaitToHang || _smsWire == SmsWireState.SWaitToHang);
        d.OnWireTop = onWire && !d.OnWireHang; // on the wire but not hanging/rolling = on top (WIRE_WAIT / WALK / the turns / Return)
        d.ReadyGun = _smsHasFludd && stateOfMario == MarioState.crouch;
        d.AimR = Input.IsActionPressed("button_r");
        d.CamL = Input.IsActionPressed("button_l");
        float lMin = SmsFluddCurrent.LAngleMin;
        d.AimRatio = lMin != 0f ? Mathf.Clamp(_fluddGunAngle / lMin, 0f, 1f) : 0f; // CLBCalcRatio<s16>(0, mLAngleMin, angle), clamped
        d.Downed = stateOfMario == MarioState.hurt || stateOfMario == MarioState.dead;
        d.InWater = _smsInWater;
        d.Climbing = d.FenceFamily || onWire;
        d.HasFludd = _fluddReady && _fluddNozzleParent is Node3D;
        if (d.HasFludd && _fluddNozzleParent is Node3D noz)
        {
            Transform3D g = noz.GlobalTransform;
            d.NozzleTopU = (g.Origin + g.Basis.Y.Normalized() * SmsUnits.UnitsToMetres(30f)) * SmsUnits.UnitsPerMetre;
        }
        d.MainStick = _camMainStick;
        d.YPressed = _camYPressed;
        d.CamCancelPressed = _camCancelPressed;
        d.YCamCanEnter = SmsYCamCanEnter();
        d.Locked = CameraLocked || stateOfMario == MarioState.talking || stateOfMario == MarioState.shineGet || stateOfMario == MarioState.dead;
        d.Talking = _smsTalking;
        d.Debug = SmsDebugPrint;
        return d;
    }

    /// <summary>*gpMarioAngleY as the camera reads it: his face as the game has it. The port's _smsFaceYaw IS "behind him" in the camera's bearing
    /// convention (SmsBehindYaw: atan2 of the direction the camera sits off him), so the face is that + 0x8000. The talk camera reads it fresh
    /// at makeMtxForTalk (the camera's own copy is a frame old then).</summary>
    public int SmsCamFaceAngleBam()
    {
        float behind = SmsBehindYaw(out float by) ? by : (lastFacingDirection.LengthSquared() > 1e-4f ? SmsMath.YawFromDir(lastFacingDirection.Normalized()) : 0f);
        return (short)((short)(int)MathF.Round(behind / SmsUnits.BamToRad) + 0x8000);
    }

    /// <summary>Respawn / the intro's spawn: the game reloads the stage and its camera starts behind him (CPolarSubCamera::loadAfter).</summary>
    private void SmsCameraWarpBehind()
    {
        _camPrevValid = false;
        _camera?.SmsWarpBehind();
    }
}
