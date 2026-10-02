using Godot;
using System.Collections.Generic;

/// <summary>
/// The body's overlay (assets/Sms/Fx/BodyOverlay.tscn, BodyOverlay.gdshader): what the game draws over Mario's own pixels.
/// <see cref="DamageFog"/> -- TMario::addDamageFog (MarioDraw.cpp 2185): while mInvincibilityFrames > 0 and he is not talking, the
/// body's, cap's and hands' materials carry a fog of colour (0xFF, 0x00, 0x80) whose range SMS_AddDamageFogEffect (DrawUtil.cpp) sets
/// about his view depth so the linear factor at his depth is 7/12 - sin(moveTick x 0x888) / 4: a 0.33..0.83 pink pulse with a period
/// of 30 ticks, one second, free-running on the director's tick count. <see cref="Negative"/> -- TMario::drawSpecial -> drawLogic
/// (MarioDraw.cpp 2058-2108) during MARIO_STATUS_ELECTRIC_DAMAGE: the body model drawn again with GX_BM_LOGIC and mBlendLogicOp 10 =
/// GX_LO_INV, every covered pixel its own inverse, every frame. The water gun is its own actor in the game and keeps its colours, so
/// its subtree is skipped. The overlay sits on the meshes only while something is on; off, each mesh gets back what it had.
/// </summary>
public partial class SmsBodyOverlay : Node
{
    /// <summary>The overlay: BodyOverlay.gdshader (fog, fog_color, negative).</summary>
    [Export] public ShaderMaterial Overlay;

    /// <summary>Node names under the Armature whose subtrees are not the body model (TWaterGun draws its own model, untouched).</summary>
    [Export] public string[] ExcludedSubtrees = { "SmsFluddAttach" };

    /// <summary>The fog's pulse: 30 ticks of the game's 30 Hz move tick = 1 s.</summary>
    [Export] public float FogPeriodSeconds = 1.0f;

    private readonly List<MeshInstance3D> _meshes = new();
    private readonly List<Material> _previous = new();
    private bool _applied;
    private bool _negative;
    private bool _fogOn;
    private float _fog;
    private double _clock;

    public void Bind(Node3D owner)
    {
        _meshes.Clear();
        Node3D armature = owner.GetNodeOrNull<Node3D>("Armature");
        if (armature == null)
        {
            GD.PushWarning($"[SMS fx] {GetPath()}: {owner.GetPath()} has no Armature -- the body overlay has nothing to draw on");
            return;
        }
        Collect(armature);
        if (Overlay == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Overlay export is not set (see assets/Sms/Fx/BodyOverlay.tscn)");
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

    /// <summary>drawLogic() runs while the status is ELECTRIC_DAMAGE.</summary>
    public bool Negative
    {
        get => _negative;
        set
        {
            if (value == _negative)
                return;
            _negative = value;
            Overlay?.SetShaderParameter("negative", value);
            Refresh();
        }
    }

    /// <summary>addDamageFog()'s condition, told each tick: invincible and not talking. The factor follows in _Process.</summary>
    public void SetDamageFog(bool on)
    {
        if (on == _fogOn)
            return;
        _fogOn = on;
        if (!on)
        {
            _fog = 0f;
            Overlay?.SetShaderParameter("fog", 0f);
        }
        Refresh();
    }

    /// <summary>The fog factor now (0 when off): the headless drive reads it.</summary>
    public float Fog => _fog;

    /// <summary>How many meshes carry the overlay now.</summary>
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

    public override void _Process(double delta)
    {
        _clock += delta;
        if (!_fogOn || Overlay == null)
            return;
        // SMS_AddDamageFogEffect: s = sin(moveTick x 0x888), the factor at his depth (700 - 300 s) / 1200
        float s = Mathf.Sin((float)(_clock / FogPeriodSeconds) * Mathf.Tau);
        _fog = 7f / 12f - 0.25f * s;
        Overlay.SetShaderParameter("fog", _fog);
    }

    private void Refresh()
    {
        bool want = _negative || _fogOn;
        if (want == _applied)
            return;
        _applied = want;
        if (want)
        {
            _previous.Clear();
            foreach (MeshInstance3D m in _meshes)
            {
                _previous.Add(m.MaterialOverlay);
                m.MaterialOverlay = Overlay;
            }
        }
        else
        {
            for (int i = 0; i < _meshes.Count; i++)
                _meshes[i].MaterialOverlay = i < _previous.Count ? _previous[i] : null;
            _previous.Clear();
        }
    }
}
