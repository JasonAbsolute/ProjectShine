using Godot;

/// <summary>
/// The slide's smoke: TMario::frontSlipEffect (MarioParticle.cpp 326) on plain ground -- emitAndBindToMtxPtr(PARTICLE_MS_M_SLIPSMOKE,
/// the root joint's matrix, 1) each slipping frame (MarioRun.cpp 1102, walkProcess 1): one emitter per status, live while the calls come,
/// its frame his position and yaw. ms_m_slipsmoke.jpa: 1.5 puffs a frame off a 15-unit shell 15 up, thrown 6 u/f behind him and 2.5 up
/// (+- 30 %, 31 degrees of spread), gravity +0.7 up fading in over half their life, Drag 0.88, 50 frames +- 35 %, 60 units wide, 0.8
/// alpha from a tenth to a half. The wet ground's spray (ms_m_watslide_a/b/c) and the sand's (ms_m_slidesand_a/b) are not ported.
/// assets/Sms/Fx/SlipSmoke.tscn (gen_fx_dust.py): the node turns with Mario (TurnWithOwner) so the smoke flies behind him.
/// </summary>
public partial class SmsSlipSmoke : SmsFollowEffect
{
    [Export] public GpuParticles3D Smoke;
    private bool _active;

    public override void _Ready()
    {
        if (Smoke == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Smoke export is not set (see assets/Sms/Fx/SlipSmoke.tscn)");
        else
            Smoke.Emitting = false;
    }

    /// <summary>frontSlipEffect() called this tick (on) or not (off).</summary>
    public bool Active
    {
        get => _active;
        set
        {
            if (value == _active)
                return;
            _active = value;
            if (Smoke != null)
                Smoke.Emitting = value;
        }
    }
}
