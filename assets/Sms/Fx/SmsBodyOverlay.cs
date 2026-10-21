using Godot;
using System.Collections.Generic;

/// <summary>
/// The body's two draw-time effects (assets/Sms/Fx/BodyOverlay.tscn), each in the place the game draws it.
/// <see cref="SetDamageFog"/> -- TMario::addDamageFog (MarioDraw.cpp 2185): while mInvincibilityFrames > 0 and he is not talking, the
/// body's, cap's and hands' materials carry a fog of colour (0xFF, 0x00, 0x80) whose range SMS_AddDamageFogEffect (DrawUtil.cpp) sets
/// about his view depth so the linear factor at his depth is 7/12 - sin(moveTick x 0x888) / 4: a 0.33..0.83 pink pulse with a period
/// of 30 ticks = a QUARTER second (pass 95b: gpMarDirector->mMoveTickCount is a 120 Hz counter -- MarDirectorDirect.cpp 104 adds one per
/// simulation tick, four a drawn frame), free-running on the director's tick count. It is a fog on the body's OWN materials, so since
/// pass 97 each surface's StandardMaterial3D is swapped for a copy of itself with the fog (MarioFog.gdshader*, the values copied in) while
/// the fog is on: the fogged body is then part of Godot's opaque pass and of hint_screen_texture, which the goop ripple ring and balls and
/// the Gooble's body refract -- the pass-56 screen-reading overlay kept the fog in the transparent pass, and every refraction showed an
/// unfogged Mario (his navy overalls smeared round his magenta legs in the goop ripple).
/// <see cref="Negative"/> -- TMario::drawSpecial -> drawLogic (MarioDraw.cpp 2058-2108) during MARIO_STATUS_ELECTRIC_DAMAGE: the body
/// model drawn again with GX_BM_LOGIC and mBlendLogicOp 10 = GX_LO_INV, every covered pixel its own inverse, every frame -- a second pass
/// over the finished body, kept as the screen-reading <see cref="Overlay"/> (BodyOverlay.gdshader). The water gun is its own actor in the
/// game and keeps its colours, so its subtree is skipped. Off, each surface and mesh gets back exactly what it had.
/// </summary>
public partial class SmsBodyOverlay : Node
{
    /// <summary>The negative's overlay: BodyOverlay.gdshader (the screen's inverse at the body's pixels).</summary>
    [Export] public ShaderMaterial Overlay;

    /// <summary>The fog shaders (pass 97) -- MarioFog.gdshaderinc with the static part each surface's StandardMaterial3D needs: transparency
    /// DISABLED or ALPHA, cull back or none. A surface whose material wants anything else keeps its own material, unfogged, with a warning.</summary>
    [Export] public Shader FogShader;                 // opaque, cull back: the body
    [Export] public Shader FogShaderTwoSided;         // opaque, cull disabled: the hands
    [Export] public Shader FogShaderAlpha;            // transparency ALPHA, cull back
    [Export] public Shader FogShaderAlphaTwoSided;    // transparency ALPHA, cull disabled

    /// <summary>Node names under the Armature whose subtrees are not the body model (TWaterGun draws its own model, untouched).</summary>
    [Export] public string[] ExcludedSubtrees = { "SmsFluddAttach" };

    /// <summary>The fog's pulse: 30 ticks of the director's 120 Hz move tick = 0.25 s (65536 / 0x888 = 30 ticks a cycle).</summary>
    [Export] public float FogPeriodSeconds = 0.25f;
    [ExportGroup("Goop skin (pass 111)")]
    /// <summary>Mario's own pollution masks from ma_mdl1.bmd / ma_hnd2l.bmd (H_ma_polmask1_i4, H_ma_polmask_hand_i4): the intensity decides
    /// which texels goop first (dark) and last (bright). Assigned per surface by name: the hand surfaces take the hand mask.</summary>
    [Export] public Texture2D BodyPollutionMask;
    [Export] public Texture2D HandPollutionMask;
    /// <summary>The stage's goop skin (the NPCs' H_ma_rak): what a dirty Mario wears where the mask goops -- unless a layer dresses him (pass 108).</summary>
    [Export] public Texture2D GoopTexture;
    /// <summary>A surface takes the HAND mask when its MeshInstance3D (or an ancestor) is named with one of these prefixes, or sits under a *HandBone
    /// node -- the five hand glbs (ma_hnd2l / 3l / 2r / 3r / 4r); their material names vary with the export, so the mesh decides.</summary>
    [Export] public string[] HandMeshPrefixes = { "ma_hnd" };
    /// <summary>0: the mask and goop through UV; 1: through UV2 (the bmd's TEX1 / TEX2). Set from the glb at bind: UV2 when the body mesh carries a second set.</summary>
    [Export] public int PollutionUv = -1;
    [ExportGroup("")]
    private float _dirty;                                   // mDirty / 255
    private Texture2D _dirtyTex;                            // the goop texture in use (the layer's or GoopTexture)
    public float Dirty => _dirty;
    public Texture2D GoopTextureNow => _dirtyTex ?? GoopTexture;
    public int PollutionUvNow => _polUv;
    private int _polUv;

    /// <summary>One body surface: its mesh, surface index, the fog copy of its material, and the override it had before the fog.</summary>
    private sealed class FogSlot
    {
        public MeshInstance3D Mesh;
        public int Surface;
        public ShaderMaterial Material;
        public Material Restore;
    }

    private readonly List<MeshInstance3D> _meshes = new();
    private readonly List<Material> _previousOverlays = new();
    private readonly List<FogSlot> _fogSlots = new();
    private readonly List<string> _fogSkipped = new();
    private bool _overlayApplied;
    private bool _fogApplied;
    private bool _negative;
    private bool _fogOn;
    private float _fog;
    private double _clock;

    private static readonly StringName FogParam = "fog";
    private static readonly StringName FogColorParam = "fog_color";

    public void Bind(Node3D owner)
    {
        _meshes.Clear();
        _fogSlots.Clear();
        _fogSkipped.Clear();
        Node3D armature = owner.GetNodeOrNull<Node3D>("Armature");
        if (armature == null)
        {
            GD.PushWarning($"[SMS fx] {GetPath()}: {owner.GetPath()} has no Armature -- the body overlay has nothing to draw on");
            return;
        }
        Collect(armature);
        // pass 111: the bmd reads its masks through TEX1 / TEX2; the glb carries them as UV2 when it carries them at all
        _polUv = PollutionUv >= 0 ? PollutionUv : (MeshHasUv2(_meshes) ? 1 : 0);
        if (Overlay == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Overlay export is not set (see assets/Sms/Fx/BodyOverlay.tscn)");
        if (FogShader == null || FogShaderTwoSided == null || FogShaderAlpha == null || FogShaderAlphaTwoSided == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: a FogShader export is not set (see assets/Sms/Fx/BodyOverlay.tscn) -- the surfaces needing it stay unfogged");
        foreach (MeshInstance3D mesh in _meshes)
        {
            int n = mesh.Mesh != null ? mesh.Mesh.GetSurfaceCount() : 0;
            for (int s = 0; s < n; s++)
            {
                Material active = mesh.GetActiveMaterial(s);
                string label = $"{mesh.Name}#{s} '{(active != null ? active.ResourceName : "null")}'";
                ShaderMaterial fogMat = active is StandardMaterial3D std ? MakeFogMaterial(std, mesh, out string why) : Refuse(active, out why);
                if (fogMat == null)
                {
                    _fogSkipped.Add(label);
                    GD.PushWarning($"[SMS fx] {GetPath()}: {label} keeps its own material through the damage fog -- {why}");
                    continue;
                }
                fogMat.ResourceName = $"damage fog: {label}";
                _fogSlots.Add(new FogSlot { Mesh = mesh, Surface = s, Material = fogMat });
            }
        }
    }

    private static bool MeshHasUv2(List<MeshInstance3D> meshes)
    {
        foreach (var mi in meshes)
        {
            if (mi.Mesh is not ArrayMesh am) continue;
            for (int s = 0; s < am.GetSurfaceCount(); s++)
                if ((am.SurfaceGetFormat(s) & Mesh.ArrayFormat.FormatTexUV2) != 0) return true;
        }
        return false;
    }

    private void Collect(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (System.Array.IndexOf(ExcludedSubtrees, child.Name.ToString()) >= 0)
                continue;
            if (child is MeshInstance3D mesh)
                _meshes.Add(mesh);
            Collect(child);
        }
    }

    private static ShaderMaterial Refuse(Material m, out string why)
    {
        why = m == null ? "no material" : $"a {m.GetClass()}, not a StandardMaterial3D";
        return null;
    }

    /// <summary>The surface's StandardMaterial3D as a MarioFog material: the static part picks the shader, the rest is copied in. Null, with the
    /// reason, for a feature MarioFog.gdshaderinc does not state (so the body never silently changes look under the fog).</summary>
    private ShaderMaterial MakeFogMaterial(StandardMaterial3D b, MeshInstance3D meshForMask, out string why)
    {
        why = null;
        Shader shader = null;
        bool alpha;
        switch (b.Transparency)
        {
            case BaseMaterial3D.TransparencyEnum.Disabled: alpha = false; break;
            case BaseMaterial3D.TransparencyEnum.Alpha: alpha = true; break;
            default: why = $"transparency {b.Transparency}"; return null;
        }
        switch (b.CullMode)
        {
            case BaseMaterial3D.CullModeEnum.Back: shader = alpha ? FogShaderAlpha : FogShader; break;
            case BaseMaterial3D.CullModeEnum.Disabled: shader = alpha ? FogShaderAlphaTwoSided : FogShaderTwoSided; break;
            default: why = $"cull mode {b.CullMode}"; return null;
        }
        if (shader == null) { why = "its fog shader export is not set"; return null; }
        if (b.ShadingMode != BaseMaterial3D.ShadingModeEnum.PerPixel) { why = $"shading mode {b.ShadingMode}"; return null; }
        if (b.DiffuseMode != BaseMaterial3D.DiffuseModeEnum.Burley) { why = $"diffuse mode {b.DiffuseMode}"; return null; }
        if (b.SpecularMode != BaseMaterial3D.SpecularModeEnum.SchlickGgx) { why = $"specular mode {b.SpecularMode}"; return null; }
        if (b.TextureFilter != BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps) { why = $"texture filter {b.TextureFilter}"; return null; }
        if (b.DepthDrawMode != BaseMaterial3D.DepthDrawModeEnum.OpaqueOnly || b.NoDepthTest) { why = "its depth mode"; return null; }
        if (b.EmissionEnabled || b.NormalEnabled || b.RimEnabled || b.ClearcoatEnabled || b.AnisotropyEnabled || b.Get("ao_enabled").AsBool()
            || b.HeightmapEnabled || b.RefractionEnabled || b.DetailEnabled || b.BacklightEnabled || b.SubsurfScatterEnabled)
        { why = "a lighting feature (emission / normal / rim / clearcoat / anisotropy / AO / height / refraction / detail / backlight / sss)"; return null; }
        if (b.MetallicTexture != null || b.RoughnessTexture != null) { why = "a metallic / roughness texture"; return null; }
        if (b.Uv1Triplanar || b.Grow || b.BillboardMode != BaseMaterial3D.BillboardModeEnum.Disabled || b.FixedSize || b.UsePointSize
            || b.AlbedoTextureMsdf || b.AlbedoTextureForceSrgb || b.DisableAmbientLight || b.DisableReceiveShadows || b.ProximityFadeEnabled
            || b.DistanceFadeMode != BaseMaterial3D.DistanceFadeModeEnum.Disabled)
        { why = "a vertex / fade / texture flag (triplanar, grow, billboard, fixed size, point size, msdf, force sRGB, no ambient, no shadows, fades)"; return null; }
        if (b.NextPass != null) { why = "a next_pass"; return null; }

        var m = new ShaderMaterial { Shader = shader, RenderPriority = b.RenderPriority };
        m.SetShaderParameter("albedo", b.AlbedoColor);
        if (b.AlbedoTexture != null) m.SetShaderParameter("texture_albedo", b.AlbedoTexture);
        m.SetShaderParameter("tex_repeat", b.TextureRepeat);
        m.SetShaderParameter("roughness", b.Roughness);
        m.SetShaderParameter("specular", b.MetallicSpecular);
        m.SetShaderParameter("metallic", b.Metallic);
        m.SetShaderParameter("use_vertex_color", b.VertexColorUseAsAlbedo);
        m.SetShaderParameter("vertex_color_srgb", b.VertexColorIsSrgb);
        m.SetShaderParameter("uv1_scale", b.Uv1Scale);
        m.SetShaderParameter("uv1_offset", b.Uv1Offset);
        m.SetShaderParameter(FogParam, 0f);
        m.SetShaderParameter(FogColorParam, new Vector3(_fogColor.R, _fogColor.G, _fogColor.B));
        m.SetShaderParameter("dirty", 0f);
        // the five hand models are their own meshes (ma_hnd2l / 3l / 2r / 3r / 4r under Left/RightHandBone); their material names vary with the export
        bool hand = false;
        for (Node q = meshForMask; q != null && !hand; q = q.GetParent())
        {
            string nm = q.Name.ToString();
            foreach (string pre in HandMeshPrefixes ?? System.Array.Empty<string>())
                if (!string.IsNullOrEmpty(pre) && nm.StartsWith(pre, System.StringComparison.OrdinalIgnoreCase)) hand = true;
            if (nm.EndsWith("HandBone")) hand = true;
        }
        var mask = hand ? HandPollutionMask : BodyPollutionMask;
        if (mask != null) m.SetShaderParameter("pol_mask_tex", RawLinearMask(mask));
        if (GoopTexture != null) m.SetShaderParameter("pol_tex", GoopTexture);
        m.SetShaderParameter("pol_uv", _polUv);
        return m;
    }

    private static readonly System.Collections.Generic.Dictionary<ulong, Texture2D> _rawMaskCache = new System.Collections.Generic.Dictionary<ulong, Texture2D>();

    /// <summary>Pass 113b: the pollution mask is intensity DATA (the game's I4 threshold per texel), but a colour PNG is sampled sRGB-DECODED on
    /// the GPU, which drops its values (147 -> ~75) so almost the whole body reads &lt;= mDirty and goops -- Mario turned uniformly dark instead of
    /// the ~20 % the mask intends. (The headless drive read the mask IMAGE on the CPU, where the values are already raw, so it could not see
    /// this; the NPC skin's gradient mask tolerates the shift, Mario's splatter does not.) Re-wrap the mask as a linear R8 texture so
    /// `texture(pol_mask_tex).r` returns the raw 8-bit value the shader's `floor(.r * 255)` expects. Cached per source texture.</summary>
    private static Texture2D RawLinearMask(Texture2D src)
    {
        if (src == null) return null;
        ulong key = src.GetInstanceId();
        if (_rawMaskCache.TryGetValue(key, out var hit) && hit != null) return hit;
        Image img = null;
        try { img = src.GetImage(); } catch { img = null; }
        if (img == null) { _rawMaskCache[key] = src; return src; }
        if (img.IsCompressed()) img.Decompress();
        if (img.GetFormat() != Image.Format.R8) img.Convert(Image.Format.R8);  // R8 = R8_UNORM: sampled raw, never sRGB-decoded
        var tex = ImageTexture.CreateFromImage(img);
        _rawMaskCache[key] = tex;
        return tex;
    }

    /// <summary>addDirty (MarioDraw.cpp 2139): mDirty into every surface's konst alpha -- here the `dirty` uniform (mDirty / 255) and the goop
    /// texture (the layer's when pass 108 dressed him, else GoopTexture). The shader copies are installed while dirty > 0 (as while fogged).</summary>
    public void SetDirty(float amount255, Texture2D goop)
    {
        float d = Mathf.Clamp(amount255 / 255f, 0f, 1f);
        Texture2D tex = goop ?? GoopTexture;
        if (d == _dirty && tex == _dirtyTex) { RefreshFog(); return; }
        _dirty = d; _dirtyTex = tex;
        foreach (FogSlot slot in _fogSlots)
        {
            slot.Material.SetShaderParameter("dirty", d);
            if (tex != null) slot.Material.SetShaderParameter("pol_tex", tex);
        }
        RefreshFog();
    }
    /// <summary>Surfaces wearing the goop swap right now (dirty > 0 and installed), for the drives.</summary>
    public int DirtyCount
    {
        get
        {
            if (_dirty <= 0f) return 0;
            int n = 0;
            foreach (FogSlot slot in _fogSlots) if (slot.Mesh.GetSurfaceOverrideMaterial(slot.Surface) == slot.Material) n++;
            return n;
        }
    }

    /// <summary>drawLogic() runs while the status is ELECTRIC_DAMAGE.</summary>
    public bool Negative
    {
        get => _negative;
        set
        {
            if (value == _negative)
                return;
            _negative = value;
            RefreshOverlay();
        }
    }

    /// <summary>The fog's two colours (addDamageFog, MarioDraw.cpp 2188 / 2195): the damage pink (0xFF, 0x00, 0x80) and the goop's yellow-green
    /// (0x82, 0x96, 0x00) -- framebuffer (gamma-space) values, as the shader expects.</summary>
    public static readonly Color DamagePink = new Color(1f, 0f, 0.5f);
    public static readonly Color GoopYellow = new Color(0x82 / 255f, 0x96 / 255f, 0f);
    private Color _fogColor = new Color(1f, 0f, 0.5f);
    public Color FogColorNow => _fogColor;

    /// <summary>The fog's colour (fog_color): the pink, or the goop's yellow-green while he stands dirty on SLIP paint.</summary>
    public void SetFogColor(Color c)
    {
        if (c == _fogColor)
            return;
        _fogColor = c;
        var v = new Vector3(c.R, c.G, c.B);
        foreach (FogSlot slot in _fogSlots)
            slot.Material.SetShaderParameter(FogColorParam, v);
    }

    /// <summary>addDamageFog()'s condition, told each tick: invincible and not talking. The factor follows in _Process.</summary>
    public void SetDamageFog(bool on)
    {
        if (on == _fogOn)
            return;
        _fogOn = on;
        if (!on)
            SetFogFactor(0f);
        RefreshFog();
    }

    /// <summary>The fog factor now (0 when off): the headless drive reads it.</summary>
    public float Fog => _fog;

    /// <summary>How many meshes carry the negative's overlay now.</summary>
    public int OverlaidCount
    {
        get
        {
            int n = 0;
            foreach (MeshInstance3D m in _meshes)
                if (Overlay != null && m.MaterialOverlay == Overlay)
                    n++;
            return n;
        }
    }

    /// <summary>How many body surfaces carry their fog material now (0 while the fog is off).</summary>
    public int FoggedCount
    {
        get
        {
            int n = 0;
            foreach (FogSlot slot in _fogSlots)
                if (slot.Mesh.GetSurfaceOverrideMaterial(slot.Surface) == slot.Material)
                    n++;
            return n;
        }
    }

    /// <summary>How many body surfaces the fog cannot state (they keep their own material; see the warnings at Bind).</summary>
    public int FogSkippedCount => _fogSkipped.Count;

    /// <summary>The fog material of a surface (null if that surface is skipped or unknown) -- the drive compares its values to the base.</summary>
    public ShaderMaterial FogMaterialOf(MeshInstance3D mesh, int surface)
    {
        foreach (FogSlot slot in _fogSlots)
            if (slot.Mesh == mesh && slot.Surface == surface)
                return slot.Material;
        return null;
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (!_fogOn)
            return;
        // SMS_AddDamageFogEffect: s = sin(moveTick x 0x888), the factor at his depth (700 - 300 s) / 1200
        float s = Mathf.Sin((float)(_clock / FogPeriodSeconds) * Mathf.Tau);
        SetFogFactor(7f / 12f - 0.25f * s);
    }

    private void SetFogFactor(float f)
    {
        _fog = f;
        foreach (FogSlot slot in _fogSlots)
            slot.Material.SetShaderParameter(FogParam, f);
    }

    /// <summary>The fog on: each surface's override becomes its fog material (what it had is kept); off: put back.</summary>
    private void RefreshFog()
    {
        bool want = _fogOn || _dirty > 0f;                      // pass 111: the shader copies carry the goop skin too
        if (want == _fogApplied)
            return;
        _fogApplied = want;
        if (want)
        {
            foreach (FogSlot slot in _fogSlots)
            {
                slot.Restore = slot.Mesh.GetSurfaceOverrideMaterial(slot.Surface);
                slot.Mesh.SetSurfaceOverrideMaterial(slot.Surface, slot.Material);
            }
        }
        else
        {
            foreach (FogSlot slot in _fogSlots)
            {
                if (slot.Mesh.GetSurfaceOverrideMaterial(slot.Surface) == slot.Material)
                    slot.Mesh.SetSurfaceOverrideMaterial(slot.Surface, slot.Restore);
                slot.Restore = null;
            }
        }
    }

    /// <summary>The negative on: the overlay goes on every body mesh (what it had is kept); off: put back.</summary>
    private void RefreshOverlay()
    {
        if (_negative == _overlayApplied)
            return;
        _overlayApplied = _negative;
        if (_negative)
        {
            _previousOverlays.Clear();
            foreach (MeshInstance3D m in _meshes)
            {
                _previousOverlays.Add(m.MaterialOverlay);
                m.MaterialOverlay = Overlay;
            }
        }
        else
        {
            for (int i = 0; i < _meshes.Count; i++)
                _meshes[i].MaterialOverlay = i < _previousOverlays.Count ? _previousOverlays[i] : null;
            _previousOverlays.Clear();
        }
    }
}
