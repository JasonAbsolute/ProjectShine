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

    /// <summary>DEVIATION knob: the ring quads times this; 1 = the game (the disc's 100 and 55 unit quads, each ring then x 0..2 at random as
    /// ms_m_blur2's ESP1 says -- pass 78). Pass 30 set 1.5 at Jaysonn's word ("too close to mario ... only showing white") when the rings were
    /// still a FIXED 100 units (radius 45, inside his torso); the random size has carried that since 78, so pass 91 returns the default to the
    /// game's and leaves the knob (Player.tscn's SpinJumpBlur instance). Pass 91 also found the old `q.Size *= SizeScale` compounding: the quad
    /// meshes were shared through the cached PackedScene, so every scene reload (a death's ReloadCurrentScene) grew the rings another 1.5x
    /// (1.5, 2.25, 3.4, 5.1 ... measured). The meshes are now local to the scene and the size is set from a base captured once.</summary>
    [Export] public float SizeScale = 1f;

    private readonly System.Collections.Generic.Dictionary<QuadMesh, Vector2> _baseSize = new();

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

    /// <summary>The scene's quad (the disc's size) times SizeScale. The base is captured the first time a mesh is seen, so running this
    /// again (or on a mesh another instance already scaled) cannot compound; the emission volume is untouched.</summary>
    private void ScaleQuads(GpuParticles3D p)
    {
        if (p == null)
            return;
        for (int i = 0; i < p.DrawPasses; i++)
            if (p.GetDrawPassMesh(i) is QuadMesh q)
            {
                if (!_baseSize.TryGetValue(q, out Vector2 b)) { b = q.Size; _baseSize[q] = b; }
                Vector2 want = b * SizeScale;
                if (!q.Size.IsEqualApprox(want)) q.Size = want;
            }
    }
}
