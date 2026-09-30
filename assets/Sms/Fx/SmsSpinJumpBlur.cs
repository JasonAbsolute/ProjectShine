using Godot;

/// <summary>
/// The spin jump's rings: TMario::emitBlurSpinJump (MarioParticle.cpp 564), called every frame of the spin (MarioJump.cpp 1022,
/// MarioRun.cpp 780) -- emitAndBindToMtxPtr(MS_M_BLUR2 and MS_M_BLUR2SP, getCenterAnmMtx(), 1, this): the 1 keeps one emitter of
/// each alive per owner while the calls keep coming (EmitterViewObj.cpp 352), so the rings run while he spins and stop when he stops.
/// The particles (ms_m_blur2 / ms_m_blur2sp.jpa, tools jpa_dump.py): FollowEmitter, 3 rings a frame each living 6 frames, born on a
/// vertical line through the centre joint 15 units up (a cylinder r 50 scaled 0.01 x 0.5 x 0.01: +-25 units; the small rings +-50),
/// flat Direction quads (the quad's up is the emitter's Z, its width the emitter's X -> the plane is the ground's) of 100 units
/// (P_msm_hamonring3 lerp (183,231,255) -> white, alpha 159) and 55 units (flat prm (63,87,255), its first frame (255,87,72,183)),
/// the ring texture turning -0.3 turns a frame, alpha 0 -> 1 by 37.5 %, 1 -> 0 from 62.5 %. The scene assets/Sms/Fx/SpinJumpBlur.tscn
/// holds all of it; this script binds it to Mario and switches the two emitters with the spin. Mario.cs turns the legacy
/// SpinJumpEffects rings off when this scene is present.
/// </summary>
public partial class SmsSpinJumpBlur : SmsFollowEffect
{
    /// <summary>ms_m_blur2: the large soft rings.</summary>
    [Export] public GpuParticles3D Rings;

    /// <summary>ms_m_blur2sp: the small blue rings.</summary>
    [Export] public GpuParticles3D InnerRings;

    private bool _active;

    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value)
                return;
            _active = value;
            if (Rings != null) Rings.Emitting = value;
            if (InnerRings != null) InnerRings.Emitting = value;
        }
    }

    public override void _Ready()
    {
        if (Rings == null || InnerRings == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Rings / InnerRings exports are not both set (see assets/Sms/Fx/SpinJumpBlur.tscn)");
        if (Rings != null) Rings.Emitting = false;
        if (InnerRings != null) InnerRings.Emitting = false;
    }
}
