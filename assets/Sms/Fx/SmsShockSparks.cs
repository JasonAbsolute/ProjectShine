using Godot;

/// <summary>
/// The shock's sparks: TMario::elecEffect (MarioParticle.cpp 691), called every tick of ELECTRIC_DAMAGE (MarioAutodemo.cpp 437) --
/// emitAndBindToPosPtr(MS_M_BIRI_A, C, B, &amp;mCenterPos, 1, this): the 1 keeps ONE emitter of each alive per owner while the calls keep
/// coming and invalidates it the first frame they stop (EmitterViewObj.cpp perform), so the sparks run for the whole shock and stop
/// with it. The particles (ms_m_biri_a / b / c.jpa, tools jpa_dump.py): a -- 120 lightning streaks a second from a 10-unit shell at
/// the centre joint, each living 14 frames, 200 x 50 units along the line from the centre (biri_l1 / l2 / l3 at random), cyan going
/// blue over their first 3 frames plus an additive blue, following him; b -- 60 arcs a second from a 50 x 72 x 50-unit shell, 8
/// frames, 100-unit billboards (biri_r1 / r2 at random), cyan + blue, following him; c -- 24 flashes a second anywhere within 45..90
/// units, one frame each (P_msm_hitmrk1, (87,207,255)), each throwing six white 20 x 5-unit spark crosses (P_msm_shikaku) out at 7
/// units a frame that fall at 0.44 units a frame squared for 20 frames -- world-fixed, not following. The scene
/// assets/Sms/Fx/ShockSparks.tscn holds the four GPUParticles3D; this script binds the node to the centre joint and switches them with
/// <see cref="Active"/>. Left out: the ESP1 sine-wave flicker of a and b, their +-1 turn/frame spin, the children spawning at their
/// own flash.
/// </summary>
public partial class SmsShockSparks : SmsFollowEffect
{
    /// <summary>ms_m_biri_a: the lightning streaks from the centre.</summary>
    [Export] public GpuParticles3D Streaks;

    /// <summary>ms_m_biri_b: the arcs around the body.</summary>
    [Export] public GpuParticles3D Arcs;

    /// <summary>ms_m_biri_c: the one-frame flashes.</summary>
    [Export] public GpuParticles3D Flashes;

    /// <summary>ms_m_biri_c's children: the sparks flying off the flashes.</summary>
    [Export] public GpuParticles3D Sparks;

    private bool _active;

    /// <summary>On while ELECTRIC_DAMAGE ticks; off ends the emission, the live particles finish on their own (becomeInvalidEmitter).</summary>
    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value)
                return;
            _active = value;
            foreach (var p in new[] { Streaks, Arcs, Flashes, Sparks })
                if (p != null)
                    p.Emitting = value;
        }
    }

    public override void _Ready()
    {
        if (Streaks == null || Arcs == null || Flashes == null || Sparks == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Streaks / Arcs / Flashes / Sparks exports are not all set (see assets/Sms/Fx/ShockSparks.tscn)");
        foreach (var p in new[] { Streaks, Arcs, Flashes, Sparks })
            if (p != null)
                p.Emitting = false;
    }
}
