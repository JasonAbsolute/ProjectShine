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
/// Pass 108 (DEVIATION, his ask): the three take their colours from the LAYER that dirtied him (SetGoopLayer, called from thinkDirty every tick he
/// stands on paint) when GoopFromLayer is on -- a MaterialOverride copy of each emitter's material with its colour table rewritten; the game
/// colours them by the stage's one row (SMSSetEmitterPolColor) and loads the footprint jpa per stage.
/// </summary>
public partial class SmsGoopMario : Node3D
{
    /// <summary>ms_m_ashios: the footprint template (one-shot).</summary>
    [Export] public GpuParticles3D FootPrint;
    /// <summary>ms_m_tokeos: the melt-in-water blobs (continuous).</summary>
    [Export] public GpuParticles3D Melt;
    /// <summary>ms_m_spinos: the spin-jump droplets (continuous).</summary>
    [Export] public GpuParticles3D Fling;
    /// <summary>Pass 108 (DEVIATION, his ask "it should be whatever goop they are in"): on, the footprints, the melt and the fling take their light /
    /// dark colours from the layer that dirtied him (SmsPollutionLayer.CharacterGoop: a brown layer with nothing of its own keeps the baked stage
    /// colours; Fire / Electric layers and layers with a GoopTexture or goop colours set give theirs). Off = the game: the stage's one colour row
    /// (SMSSetEmitterPolColor, EmitterViewObj.cpp 597) and footprint file whatever he walked through -- Delfino's brown.</summary>
    [Export] public bool GoopFromLayer = true;
    private SmsPollutionLayer _goopLayer;
    /// <summary>The layer his goop colours come from (null = the stage default, the baked colours).</summary>
    public SmsPollutionLayer GoopLayer => _goopLayer;
    /// <summary>True while the three emitters wear a layer's colours (a MaterialOverride on each).</summary>
    public bool GoopDressed => FootPrint != null && FootPrint.MaterialOverride != null;

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

    /// <summary>Pass 108: the layer he is getting dirty on (thinkDirty: SmsPollutionMap.PollutedLayerAt his feet, every tick on paint). A change
    /// rewrites the three emitters' colour tables: prm0 / env0 = the layer's light / dark, prm1 / env1 half-way to the pale top entry, which the
    /// melt and the fling keep (SMSSetEmitterPolColor writes the goop entries of a 6-row table; the top rows stay the jpa's white / pale); the
    /// footprint's table is all goop (ms_m_ashios' own browns), so it becomes the pair. Null, a layer that keeps the stage default, or the knob
    /// off = the baked materials (the overrides dropped).</summary>
    public void SetGoopLayer(SmsPollutionLayer layer)
    {
        Color light = default, dark = default;
        if (!GoopFromLayer || layer == null || !layer.CharacterGoop(out _, out light, out dark)) layer = null;
        if (layer == _goopLayer) return;
        _goopLayer = layer;
        if (layer == null)
        {
            if (FootPrint != null) FootPrint.MaterialOverride = null;
            if (Melt != null) Melt.MaterialOverride = null;
            if (Fling != null) Fling.MaterialOverride = null;
            return;
        }
        Dress(FootPrint, light, dark, false);
        Dress(Melt, light, dark, true);
        Dress(Fling, light, dark, true);
    }

    private static void Dress(GpuParticles3D p, Color light, Color dark, bool keepPale)
    {
        var src = p?.DrawPass1?.SurfaceGetMaterial(0) as ShaderMaterial;       // the baked (stage) table, never edited
        if (src == null) return;
        var copy = (ShaderMaterial)src.Duplicate();
        Vector3 l = new Vector3(light.R, light.G, light.B), d = new Vector3(dark.R, dark.G, dark.B);
        Vector3 top = keepPale ? src.GetShaderParameter("prm2").AsVector3() : l, topEnv = keepPale ? src.GetShaderParameter("env2").AsVector3() : d;
        copy.SetShaderParameter("prm0", l); copy.SetShaderParameter("env0", d);
        copy.SetShaderParameter("prm1", l.Lerp(top, 0.5f)); copy.SetShaderParameter("env1", d.Lerp(topEnv, 0.5f));
        copy.SetShaderParameter("prm2", top); copy.SetShaderParameter("env2", topEnv);
        p.MaterialOverride = copy;
    }

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
