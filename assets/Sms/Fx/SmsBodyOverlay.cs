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
                ShaderMaterial fogMat = active is StandardMaterial3D std ? MakeFogMaterial(std, out string why) : Refuse(active, out why);
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
    private ShaderMaterial MakeFogMaterial(StandardMaterial3D b, out string why)
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
        return m;
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
        if (_fogOn == _fogApplied)
            return;
        _fogApplied = _fogOn;
        if (_fogOn)
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
