using System;
using Godot;

// ============================================================================
//  SMS slope handling + ground pound (hip drop), ported from doldecomp/sms:
//    MarioRun.cpp   slopeProcess(), slideProcess(), doSliding(), getSlopeNormalAccele(),
//                   getSlopeSlideAccele()
//    MarioMove.cpp  isSlipStart(), mSlopeAngle = matan(n.z, n.x)  (yaw of the floor
//                   normal's horizontal part = the DOWNHILL direction)
//    MarioJump.cpp  hipAttacking()      MarioWait.cpp hipAttackEnd(), slipEnd()
//  Shipped values from params.szs (slipparam*.prm, mario.prm, jump.prm).
//
//  Companion to SmsPhysics.cs (same units: SMS units, 120 Hz ticks, 74.44 u/m).
// ============================================================================

/// <summary>Slope rules. The headline: on NORMAL ground the shipped SlopeAccele values are 0/0, so
/// running speed is not changed by slopes at all in Sunshine; only special surfaces accelerate.</summary>
public static class SmsSlope
{
    /// <summary>TBGCheckData surface classes that pick a slipparam*.prm set.</summary>
    public enum Surface
    {
        Normal,       // slipparamnormal.prm   (everything ProjectShine has today)
        FortyFive,    // slipparam45.prm       (isUnk2: the sandy/steep "45" slopes)
        ForceSlip,    // slipparamall.prm      (isForceSlip / isUnk1: goop, always slide)
        Slider,       // slipparamall_slider.prm (the secret-course slides)
        WaterGround,  // slipparamwaterground.prm (wet, nearly flat: n.y > 0.99)
        WaterSlope,   // slipparamwaterslope.prm  (wet, sloped)
    }

    /// <summary>One slipparam*.prm set (shipped values).</summary>
    public readonly struct Params
    {
        public readonly float SlipFriction, SlopeAccelUp, SlopeAccelDown, SlideAccelUp, SlideAccelDown, SlideStop, StickSlideMult;
        public readonly int SlideAngleYSp; // BAM per tick the face turns toward the slide direction
        public readonly bool JumpEnable;
        public Params(float fr, float su, float sd, float lu, float ld, float stop, float stick, int yaw, bool jump)
        { SlipFriction = fr; SlopeAccelUp = su; SlopeAccelDown = sd; SlideAccelUp = lu; SlideAccelDown = ld; SlideStop = stop; StickSlideMult = stick; SlideAngleYSp = yaw; JumpEnable = jump; }
    }

    //                                   friction  slopeUp slopeDn slideUp slideDn stop  stick  yawSp  jump
    public static readonly Params Normal      = new(0.97f,  0f,    0f,     0f,     1.5f,   1f,   0.005f, 100, true);
    public static readonly Params FortyFive   = new(0.93f,  1f,    0.022f, 0f,     0f,     0f,   0f,     0,   true);  // SlideAccele not in the prm -> defaults 0/0
    public static readonly Params ForceSlip   = new(0.95f,  4.5f,  2f,     4f,     5f,     1f,   0f,     0,   false);
    public static readonly Params Slider      = new(0.975f, 0f,    0f,     4.5f,   2f,     3f,   0.025f, 512, true);
    public static readonly Params WaterGround = new(0.985f, 0f,    0f,     5f,     2f,     1f,   0.005f, 100, true);
    public static readonly Params WaterSlope  = new(0.98f,  0f,    0f,     5f,     2f,     1f,   0.005f, 100, true);

    public static Params For(Surface s) => s switch
    {
        Surface.FortyFive => FortyFive,
        Surface.ForceSlip => ForceSlip,
        Surface.Slider => Slider,
        Surface.WaterGround => WaterGround,
        Surface.WaterSlope => WaterSlope,
        _ => Normal,
    };

    /// <summary>doSliding(): on isUnk1 (force slip) and isSlider ground the slide never stops on its own, however slow.</summary>
    public static bool NeverStops(Surface s) => s == Surface.ForceSlip || s == Surface.Slider;

    /// <summary>mDeParams.mRunningMax (mario.prm): the clamp slopeProcess applies after adding slope accel.</summary>
    public const float RunningMax = 80f;

    /// <summary>Horizontal length of the floor normal = sin(slope angle). 0 on flat ground.</summary>
    public static float SlopeMag(Vector3 floorNormal) => MathF.Sqrt(floorNormal.X * floorNormal.X + floorNormal.Z * floorNormal.Z);

    /// <summary>Unit horizontal vector pointing DOWNHILL (mSlopeAngle direction). Zero on flat ground.</summary>
    public static Vector3 Downhill(Vector3 floorNormal)
    {
        var h = new Vector3(floorNormal.X, 0f, floorNormal.Z);
        float m = h.Length();
        return m > 1e-6f ? h / m : Vector3.Zero;
    }

    /// <summary>angDiff = mSlopeAngle - mFaceAngle.y in (-90, 90): Mario faces within 90 deg of downhill.</summary>
    public static bool FacingDownhill(Vector3 floorNormal, Vector3 faceDir)
        => floorNormal.X * faceDir.X + floorNormal.Z * faceDir.Z > 0f;

    /// <summary>
    /// slopeProcess(): the per-tick slope term for RUNNING/WALKING, applied after doRunning's accel.
    /// Facing downhill: fwd += SlopeAccelUp * sin; facing uphill: fwd -= SlopeAccelDown * sin; clamp RunningMax.
    /// With the shipped normal-ground values this is a no-op except for the clamp. The velocity it writes is
    /// HORIZONTAL (mVel.y = 0) with magnitude fwd; walkProcess() then scales the step by normal.y and snaps to the
    /// floor, see <see cref="GroundStepScale"/>.
    /// </summary>
    public static float RunSlopeTick(float fwd, Vector3 floorNormal, Vector3 faceDir, Surface s = Surface.Normal)
    {
        float mag = SlopeMag(floorNormal);
        if (mag <= 0f) return MathF.Min(fwd, RunningMax);
        var p = For(s);
        fwd += FacingDownhill(floorNormal, faceDir) ? p.SlopeAccelUp * mag : -p.SlopeAccelDown * mag;
        return MathF.Min(fwd, RunningMax);
    }

    /// <summary>
    /// slideProcess() slope term for the belly slide / slip: acceleration (units per tick) added along the DOWNHILL
    /// direction. Normal ground: 0 when facing downhill (a downhill belly slide is friction only, no gravity assist),
    /// 1.5 * sin when facing uphill (the slide is braked and would be pulled back; in practice it drops below
    /// SlideStop = 1 and he gets up). Positive = toward downhill.
    /// </summary>
    public static float SlideSlopeAccel(Vector3 floorNormal, Vector3 faceDir, Surface s = Surface.Normal)
    {
        float mag = SlopeMag(floorNormal);
        if (mag <= 0f) return 0f;
        var p = For(s);
        return FacingDownhill(floorNormal, faceDir) ? p.SlideAccelUp * mag : p.SlideAccelDown * mag;
    }

    /// <summary>
    /// Scalar form for a slide modelled as speed-along-face (ProjectShine's belly slide): returns the change in fwd
    /// this tick from the slope. Facing uphill on normal ground: -1.5 * sin. Facing downhill: +SlideAccelUp * sin (0).
    /// Apply BEFORE the friction multiply (slideProcess adds acc, then multiplies by friction).
    /// </summary>
    public static float SlideSlopeDeltaFwd(Vector3 floorNormal, Vector3 faceDir, Surface s = Surface.Normal)
    {
        float acc = SlideSlopeAccel(floorNormal, faceDir, s);
        return FacingDownhill(floorNormal, faceDir) ? acc : -acc;
    }

    /// <summary>isSlipStart(): the floor is too steep to stand on (shipped mSlipStart = 0.4 -> steeper than 66.4 deg).
    /// FortyFive surfaces slip from 30 deg (n.y < cos 30); ForceSlip/Slider always slip.</summary>
    public static bool SlipStarts(Vector3 floorNormal, Surface s = Surface.Normal)
    {
        if (s == Surface.ForceSlip || s == Surface.Slider) return true;
        if (s == Surface.FortyFive && floorNormal.Y < 0.8660254f) return true;
        return floorNormal.Y < SmsParams.Mario.SlipStart; // 0.4
    }

    /// <summary>
    /// Ground-move model, from walkProcess():
    ///     next.x = pos.x + (vel.x / 4) * normal.y;   next.z = pos.z + (vel.z / 4) * normal.y;   then snap y to the floor.
    /// So on a walkable slope the HORIZONTAL speed is fwd * ny in every direction. Along the gradient that is a surface
    /// speed of exactly fwd; across the slope the surface speed is fwd * ny (SMS scales every direction by ny).
    /// Godot's CharacterBody3D with floor_constant_speed keeps the SURFACE speed equal to the horizontal speed handed to
    /// MoveAndSlide, uphill (collision, slide along the floor, rescale) and downhill (snap within floor_snap_length, then
    /// redirect along the previous floor plane). Along the gradient it therefore already matches the game; across a slope
    /// it is 1/ny too fast. This returns the factor to multiply the horizontal speed by so Godot's surface distance equals
    /// Sunshine's: sqrt(ny^2 + (n_h . dir)^2), which is 1 along the gradient (dir parallel to the horizontal part of the
    /// normal) and ny across it (dir perpendicular). Flat ground is 1. The caller divides X/Z by it after MoveAndSlide.
    /// NEVER hand MoveAndSlide a velocity with an upward component while on the ground: Godot reads velocity.dot(up) > 0
    /// as leaving the floor and skips the floor snap, which launched Mario off every uphill lip (c173ccf).
    /// </summary>
    public static float GroundStepScale(Vector3 horizontalVelocity, Vector3 floorNormal)
    {
        float ny = floorNormal.Y;
        if (ny <= 1e-4f || ny >= 1f - 1e-6f) return 1f;
        var h = new Vector3(horizontalVelocity.X, 0f, horizontalVelocity.Z);
        float len = h.Length();
        if (len <= 1e-6f) return 1f;
        float g = (floorNormal.X * h.X + floorNormal.Z * h.Z) / len; // n_h . dir: +-sin(slope) along the gradient, 0 across
        return MathF.Sqrt(ny * ny + g * g);
    }
}

/// <summary>
/// hipAttacking() / hipAttackEnd(): the ground pound.
///   Hang  (state 1): 60 ticks (0.5 s). ALL velocity zero (setPlayerVelocity(0), mVel.y = 0) — a pound from a run
///                    drops straight down. For ticks 0..39 he drifts UP by (40 - t) * 0.125 units per tick
///                    (102.5 units = 1.38 m total, rate 5 -> 0 u/tick), skipped if his head (160 above) would hit a roof.
///                    Ground contact during the hang = ordinary landing (JUMP_END), wall = bonk (SHORT_BACK_DOWN -16).
///   Fall  (state 2/3): mVel.y = -80 EVERY tick (constant 32.2 m/s, no gravity, no air control, fwd stays 0).
///                    After 80 falling ticks it becomes the "super" drop: same speed, different VFX/damage.
///   Land: HIP_ATTACK_END -> ma_hiped plays to its last frame with velocity zeroed every tick, no inputs honoured
///                    -> SLIP_END: ma_slped, interrupted by any stick/A/B input (checkAllMotions) -> WAIT.
///                    (If the floor is >100 units below him at that moment -> LANDING; steep floor -> SLIP_FORE.)
/// </summary>
public sealed class SmsHipDrop
{
    public enum Phase { Hang, Fall, Landed }

    public Phase Current { get; private set; } = Phase.Hang;
    /// <summary>Ticks spent in the current phase.</summary>
    public int Tick { get; private set; }
    /// <summary>True once 80 falling ticks have passed (hipAttacking state 3).</summary>
    public bool IsSuper => Current == Phase.Fall && Tick > SmsParams.Jump.SuperHipAttackCt;

    public void Reset() { Current = Phase.Hang; Tick = 0; }

    /// <summary>
    /// Advance one 120 Hz tick. Returns the vertical velocity to use this tick in SMS units per tick
    /// (0 during the hang, -80 while falling) and the extra upward position nudge in SMS units (the hang lift,
    /// already reduced to 0 by the caller if a roof is within 160 units + lift above his feet).
    /// Horizontal velocity must be zero for the whole pound.
    /// </summary>
    public void Step(bool roofBlocksLift, out float vyUnitsPerTick, out float liftUnits)
    {
        liftUnits = 0f;
        switch (Current)
        {
            case Phase.Hang:
                if (Tick < SmsParams.Timing.HipDropLiftTicks && !roofBlocksLift)
                    liftUnits = SmsAir.HipDropLiftUnits(Tick);
                vyUnitsPerTick = 0f;
                Tick++;
                if (Tick >= SmsParams.Timing.HipDropHangTicks) { Current = Phase.Fall; Tick = 0; }
                return;
            case Phase.Fall:
                Tick++;
                vyUnitsPerTick = IsSuper ? SmsParams.Jump.SuperHipAttackSpeedY : SmsParams.Jump.HipAttackSpeedY; // both -80 shipped
                return;
            default:
                vyUnitsPerTick = 0f;
                return;
        }
    }

    /// <summary>Call when jumpProcess reports ground contact while falling.</summary>
    public void Land() { Current = Phase.Landed; Tick = 0; }

    /// <summary>The lift a roof check has to allow: hipAttacking tests 160 + (pos.y + lift) against the roof height
    /// using the UNSCALED lift (0.5 * (40 - t)), not the 0.25x that is actually applied.</summary>
    public static float RoofProbeUnits(int hangTick)
        => hangTick < SmsParams.Timing.HipDropLiftTicks ? (SmsParams.Timing.HipDropLiftTicks - hangTick) * 0.5f : 0f;
}

/// <summary>
/// checkGroundAtWalking() (MarioPhysics.cpp): ground contact limits while walking. The floor under the next position is
/// looked up from feet + 30 units: up to 100 units below Mario is set onto it (ledges are stepped DOWN instantly), further
/// than that he falls; a floor up to 30 units ABOVE the feet is found by the same lookup and stepped onto, because wall
/// planes are only tested at feet + 30 and feet + 60 and a lower riser is not a wall.
/// </summary>
public static class SmsGroundContact
{
    public const float LedgeDropUnits = 100f;    // v.y > floorY + 100 -> fall (return 0); else mPosition.y = floorY
    public const float StepUpUnits = 30f;        // checkGroundPlane(v.x, v.y + 30, v.z): a floor this far up is ground
    public const float WallCheckLowUnits = 30f;  // checkWallPlane(v, 30, r/2)
    public const float WallCheckHighUnits = 60f; // checkWallPlane(v, 60, r)
    public const float WallCheckHeadUnits = 150f; // checkGroundAtJumping: checkWallPlane(pos, 150, r); with the ledge bit a wall below this is no wall hit

    /// <summary>Godot floor_snap_length for the ledge-down rule: 1.343 m at this scale (Player.tscn shipped 0.6).</summary>
    public static float LedgeDropMetres => SmsUnits.UnitsToMetres(LedgeDropUnits);
    /// <summary>Highest riser walked up without breaking stride: 0.403 m.</summary>
    public static float StepUpMetres => SmsUnits.UnitsToMetres(StepUpUnits);

    /// <summary>
    /// A downward probe that came to rest <paramref name="rise"/> metres above the feet on a surface whose normal has
    /// <paramref name="normalY"/> is a step to take: a real rise, no taller than the step limit, and floor-angled (the
    /// riser itself, or a wall-steep slope, has a nearly horizontal normal and is rejected).
    /// </summary>
    public static bool IsStep(float rise, float normalY, float floorMaxAngleRad)
    {
        if (rise <= 0.01f || rise > StepUpMetres)
            return false;
        float ang = MathF.Acos(Math.Clamp(normalY, -1f, 1f));
        return ang <= floorMaxAngleRad;
    }
}

public static class SmsSlopeSelfTest
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

        // 30 deg slope rising toward +X: normal = (-sin30, cos30, 0); downhill = -X.
        var n30 = new Vector3(-0.5f, 0.8660254f, 0f);
        Check("slope mag (sin 30)", SmsSlope.SlopeMag(n30), 0.5f, 1e-4f);
        Check("downhill is -X", SmsSlope.Downhill(n30).X, -1f, 1e-4f);
        // Normal ground: running up or down the slope leaves fwd alone (shipped SlopeAccele 0/0).
        Check("run uphill, normal ground: no change", SmsSlope.RunSlopeTick(32f, n30, Vector3.Right), 32f, 0f);
        Check("run downhill, normal ground: no change", SmsSlope.RunSlopeTick(32f, n30, Vector3.Left), 32f, 0f);
        // Belly slide: uphill loses 1.5*sin = 0.75 per tick before friction; downhill gains nothing.
        Check("slide uphill delta", SmsSlope.SlideSlopeDeltaFwd(n30, Vector3.Right), -0.75f, 1e-4f);
        Check("slide downhill delta", SmsSlope.SlideSlopeDeltaFwd(n30, Vector3.Left), 0f, 1e-4f);
        // Ground step vs Godot floor_constant_speed: SMS horizontal speed on the slope is fwd * ny in every direction.
        // Along the gradient Godot's surface speed already equals fwd (scale 1); across it must drop to fwd * ny.
        Check("ground step scale uphill (+X)", SmsSlope.GroundStepScale(new Vector3(32f, 0f, 0f), n30), 1f, 1e-4f);
        Check("ground step scale downhill (-X)", SmsSlope.GroundStepScale(new Vector3(-32f, 0f, 0f), n30), 1f, 1e-4f);
        Check("ground step scale across (Z) = cos 30", SmsSlope.GroundStepScale(new Vector3(0f, 0f, 32f), n30), 0.8660254f, 1e-4f);
        Check("ground step scale diagonal", SmsSlope.GroundStepScale(new Vector3(1f, 0f, 1f), n30), 0.935414f, 1e-4f);
        Check("ground step scale flat is 1", SmsSlope.GroundStepScale(new Vector3(32f, 0f, 0f), Vector3.Up), 1f, 0f);
        Check("ground step scale stands still is 1", SmsSlope.GroundStepScale(Vector3.Zero, n30), 1f, 0f);
        // Slip threshold: 66.4 deg slips, 60 deg does not.
        Check("slip at n.y 0.39", SmsSlope.SlipStarts(new Vector3(0.92f, 0.39f, 0f)) ? 1f : 0f, 1f, 0f);
        Check("no slip at 60 deg", SmsSlope.SlipStarts(new Vector3(0.866f, 0.5f, 0f)) ? 1f : 0f, 0f, 0f);
        // Ground pound: 60-tick hang, lift totals 102.5 units over the first 40 ticks, then -80/tick.
        {
            var gp = new SmsHipDrop();
            float lift = 0f, maxHangVy = 0f; int hangTicks = 0; float vy;
            while (gp.Current == SmsHipDrop.Phase.Hang && hangTicks < 1000)
            {
                gp.Step(false, out vy, out float l);
                lift += l; hangTicks++; maxHangVy = MathF.Max(maxHangVy, MathF.Abs(vy));
            }
            Check("hang ticks", hangTicks, 60f, 0f);
            Check("hang vy stays zero", maxHangVy, 0f, 0f);
            Check("hang lift total (units)", lift, 102.5f, 0.01f);
            Check("hang lift total (m)", SmsUnits.UnitsToMetres(lift), 1.377f, 0.01f);
            gp.Step(false, out vy, out _);
            Check("fall vy", vy, -80f, 0f);
            Check("fall speed m/s", SmsUnits.SpeedToMps(-80f), -32.24f, 0.05f);
            for (int i = 0; i < 80; i++) gp.Step(false, out vy, out _);
            Check("super after 80 falling ticks", gp.IsSuper ? 1f : 0f, 1f, 0f);
        }
        // Ground contact (checkGroundAtWalking): ledge-down 100 units, step-up 30 units.
        Check("ledge drop metres", SmsGroundContact.LedgeDropMetres, 1.343f, 0.002f);
        Check("step up metres", SmsGroundContact.StepUpMetres, 0.403f, 0.002f);
        {
            float fma = Mathf.DegToRad(68f);
            Check("step 0.30 m on flat is a step", SmsGroundContact.IsStep(0.30f, 1f, fma) ? 1f : 0f, 1f, 0f);
            Check("step 0.40 m on flat is a step", SmsGroundContact.IsStep(0.40f, 1f, fma) ? 1f : 0f, 1f, 0f);
            Check("step 0.45 m is too tall", SmsGroundContact.IsStep(0.45f, 1f, fma) ? 1f : 0f, 0f, 0f);
            Check("riser face (n.y 0.1) is not a step", SmsGroundContact.IsStep(0.30f, 0.1f, fma) ? 1f : 0f, 0f, 0f);
            Check("no rise is not a step", SmsGroundContact.IsStep(0.0f, 1f, fma) ? 1f : 0f, 0f, 0f);
            Check("30 deg step top is a step", SmsGroundContact.IsStep(0.30f, 0.866f, fma) ? 1f : 0f, 1f, 0f);
        }
        GD.Print(ok ? "[SMS selftest] slope/hipdrop ALL OK" : "[SMS selftest] slope/hipdrop FAILURES");
        return ok;
    }
}
