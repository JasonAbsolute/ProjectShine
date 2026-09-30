using Godot;

/// <summary>
/// The spin jump's rings: TMario::emitBlurSpinJump (MarioParticle.cpp 564), called every frame of the spin (MarioJump.cpp 1022,
/// MarioRun.cpp 780) -- emitAndBindToMtxPtr(MS_M_BLUR2 and MS_M_BLUR2SP, getCenterAnmMtx(), 1, this): the 1 keeps one emitter of
/// each alive per owner while the calls keep coming (EmitterViewObj.cpp 352), so the rings run while he spins and stop when he stops.
/// The particles (ms_m_blur2 / ms_m_blur2sp.jpa, tools jpa_dump.py): FollowEmitter, 3 rings a 60 Hz particle frame (180 a second) each living 6 frames (0.1 s), born on a
/// vertical line through the centre joint 15 units up (a cylinder r 50 scaled 0.01 x 0.5 x 0.01: +-25 units; the small rings +-50),
/// flat Direction quads (the quad's up is the emitter's Z, its width the emitter's X -> the plane is the ground's) of 100 units
/// (P_msm_hamonring3 lerp (183,231,255) -> white, alpha 159) and 55 units (flat prm, RED (255,87,72,183) or BLUE (63,87,255) picked per ring: a 2-entry table, calc type Random),
/// the ring texture turning -9814/65536 turns a frame (-9 turns/s), alpha 0 -> 1 by 37.5 %, 1 -> 0 from 62.5 %. The scene assets/Sms/Fx/SpinJumpBlur.tscn
/// holds all of it; this script binds it to Mario and switches the two emitters with the spin. Mario.cs turns the legacy
/// SpinJumpEffects rings off when this scene is present.
/// </summary>
public partial class SmsSpinJumpBlur : SmsFollowEffect
{
    /// <summary>ms_m_blur2: the large soft rings.</summary>
    [Export] public GpuParticles3D Rings;

    /// <summary>ms_m_blur2sp: the small blue rings.</summary>
    [Export] public GpuParticles3D InnerRings;

    /// <summary>DEVIATION (Jaysonn, pass 30: "slightly bigger (like they are too close to mario) and still are only showing white"): the
    /// ring quads times this. 1 = the disc's 100 and 55 units (the ring in P_msm_hamonring3 fills 90 % of its quad: radii 45 and 25 units
    /// -- the small red / blue rings at 25 units sat inside his torso, which is why he saw white only). At 1.5 the white rings are 150
    /// units (2 m, about his height), the coloured ones 82 (1.1 m), clear of the body. Only the quads scale; the line the rings are born
    /// on (+-25 / +-50 units through the centre joint) stays the game's.</summary>
    [Export] public float SizeScale = 1.5f;

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
        ScaleQuads(Rings);
        ScaleQuads(InnerRings);
    }

    /// <summary>The scene's quad (the disc's size) times SizeScale -- the mesh is this scene's own sub-resource, so the change is local
    /// to it; the emission volume is untouched.</summary>
    private void ScaleQuads(GpuParticles3D p)
    {
        if (p == null || Mathf.IsEqualApprox(SizeScale, 1f))
            return;
        for (int i = 0; i < p.DrawPasses; i++)
            if (p.GetDrawPassMesh(i) is QuadMesh q)
                q.Size *= SizeScale;
    }
}
