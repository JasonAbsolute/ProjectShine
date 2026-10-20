using Godot;

/// <summary>
/// Pass 107: Mario's own goop particles, the three the game loads per stage from /scene/&lt;stage&gt;/map/pollution/ (MarioParticle.cpp 31-54):
/// the dirty FOOTPRINT (ms_m_ashios: emitDirtyFootPrint, one flat multiply-blended print per footfall while the footprint window is open),
/// the goop MELTING off him in water (ms_m_tokeos: meltInWaterEffect, flat blobs drifting out from a ring on the water surface at his x / z,
/// rate mDirty x MeltInWaterMax / 255, x 0.6 in shallow water) and the goop FLUNG OFF in a spin jump (ms_m_spinos: emitBlurSpinJump, droplets
/// out of a ring around the centre joint, rate 1.75 x mDirty / 255). The footprint is a one-shot template spawned as an unbound burst at the
/// ground frame (the game binds a COPY of the matrix). The melt and the fling are continuous emitters the owner moves every tick to the game's
/// matrix (emitAndBindToMtxPtr: the pointer is re-read each frame) and opens with a rate; their particles live in the world.
/// Scene: assets/Sms/Fx/GoopMario.tscn (gen_fx_goop.py).
/// </summary>
public partial class SmsGoopMario : Node3D
{
    /// <summary>ms_m_ashios: the footprint template (one-shot).</summary>
    [Export] public GpuParticles3D FootPrint;
    /// <summary>ms_m_tokeos: the melt-in-water blobs (continuous).</summary>
    [Export] public GpuParticles3D Melt;
    /// <summary>ms_m_spinos: the spin-jump droplets (continuous).</summary>
    [Export] public GpuParticles3D Fling;

    /// <summary>The particle clock the scenes were baked at (gen_fx.FPS): a game rate in particles a frame times this is particles a second.</summary>
    public const float ParticleFps = 60f;
    /// <summary>ms_m_ashios BEM1 trs (0, -11, 0) in calcGroundMtx's frame, whose origin sits 18 u over the ground: the print lies 7 u up.</summary>
    public const float FootPrintRiseUnits = 18f - 11f;
    /// <summary>meltInWaterEffect: setGlobalScale(0.6) in shallow water (MARIO_FLAG_IN_SHALLOW_WATER).</summary>
    public const float ShallowMeltScale = 0.6f;

    public override void _Ready()
    {
        if (FootPrint == null || Melt == null || Fling == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: FootPrint / Melt / Fling are not all set (see assets/Sms/Fx/GoopMario.tscn)");
        if (FootPrint != null) { FootPrint.Emitting = false; FootPrint.OneShot = true; }
        if (Melt != null) Melt.Emitting = false;
        if (Fling != null) Fling.Emitting = false;
    }

    /// <summary>One footprint. <paramref name="ground"/> = calcGroundMtx's frame at the ground point under the foot joint: X across (his right),
    /// Y the ground normal, Z his forward along the ground. The print is placed FootPrintRiseUnits up the normal and stays there.</summary>
    public void SpawnFootPrint(Transform3D ground)
    {
        if (FootPrint == null) return;
        var at = new Transform3D(ground.Basis, ground.Origin + ground.Basis.Y.Normalized() * SmsUnits.UnitsToMetres(FootPrintRiseUnits));
        SmsUnboundBurst.Spawn(FootPrint, GetTree().CurrentScene ?? GetParent(), at);
    }

    /// <summary>Open / move the melt: <paramref name="surface"/> = the game's unk220 (his x / z at the water surface, his model yaw),
    /// <paramref name="ratePerFrame"/> = mDirty x MeltInWaterMax / 255, <paramref name="shallow"/> = IN_SHALLOW_WATER (the 0.6 scale).</summary>
    public void MeltSet(Transform3D surface, float ratePerFrame, bool shallow)
    {
        if (Melt == null) return;
        float s = shallow ? ShallowMeltScale : 1f;
        Melt.GlobalTransform = new Transform3D(surface.Basis.Orthonormalized().Scaled(Vector3.One * s), surface.Origin);
        Melt.AmountRatio = RateToAmountRatio(Melt, ratePerFrame);
        if (!Melt.Emitting) Melt.Emitting = true;
    }

    public void MeltOff() { if (Melt != null && Melt.Emitting) Melt.Emitting = false; }

    /// <summary>Open / move the fling: <paramref name="centre"/> = getCenterAnmMtx(), <paramref name="ratePerFrame"/> = 1.75 x mDirty / 255.</summary>
    public void FlingSet(Transform3D centre, float ratePerFrame)
    {
        if (Fling == null) return;
        Fling.GlobalTransform = new Transform3D(centre.Basis.Orthonormalized(), centre.Origin);
        Fling.AmountRatio = RateToAmountRatio(Fling, ratePerFrame);
        if (!Fling.Emitting) Fling.Emitting = true;
    }

    public void FlingOff() { if (Fling != null && Fling.Emitting) Fling.Emitting = false; }

    public bool MeltOn => Melt != null && Melt.Emitting;
    public bool FlingOn => Fling != null && Fling.Emitting;

    /// <summary>A game emission rate (particles a particle frame) as the emitter's AmountRatio: the emitter streams Amount particles per
    /// Lifetime seconds at ratio 1.</summary>
    public static float RateToAmountRatio(GpuParticles3D p, float ratePerFrame)
    {
        float full = p.Amount / (float)p.Lifetime;                       // particles a second at ratio 1
        return Mathf.Clamp(ratePerFrame * ParticleFps / full, 0f, 1f);
    }
}
