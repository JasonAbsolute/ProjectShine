using Godot;
using System.Collections.Generic;

/// <summary>
/// The shock's negative: TMario::drawSpecial -> drawLogic() (MarioDraw.cpp 2058-2108) during MARIO_STATUS_ELECTRIC_DAMAGE -- the body
/// model's two draw buffers are drawn again with GX_BM_LOGIC and mBlendLogicOp = 10 = GX_LO_INV (MarioInit.cpp 420): every pixel the
/// model covers becomes its own inverse (a colour-off depth pass first, so each pixel inverts once), every frame of the status, no
/// flicker. Here a material overlay on the body model's meshes (under the owner's Armature; the water gun is its own actor in the game
/// and keeps its colours, so its subtree is skipped): the overlay's shader (ShockNegative.gdshader) reads the screen and writes
/// 1 - screen, drawn in the transparent pass over the shaded body at the body's own depth. The scene assets/Sms/Fx/ShockNegative.tscn
/// holds the material; <see cref="Active"/> follows the status.
/// </summary>
public partial class SmsShockNegative : Node
{
    /// <summary>The overlay: ShockNegative.gdshader.</summary>
    [Export] public Material Negative;

    /// <summary>Node names under the Armature whose subtrees are not the body model (TWaterGun draws its own model, uninverted).</summary>
    [Export] public string[] ExcludedSubtrees = { "SmsFluddAttach" };

    private readonly List<MeshInstance3D> _meshes = new();
    private readonly List<Material> _previous = new();
    private bool _active;

    /// <summary>Bind to the actor: the body model's meshes are collected from its Armature.</summary>
    public void Bind(Node3D owner)
    {
        _meshes.Clear();
        Node3D armature = owner.GetNodeOrNull<Node3D>("Armature");
        if (armature == null)
        {
            GD.PushWarning($"[SMS fx] {GetPath()}: {owner.GetPath()} has no Armature -- the shock's negative has nothing to draw");
            return;
        }
        Collect(armature);
        if (Negative == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Negative export is not set (see assets/Sms/Fx/ShockNegative.tscn)");
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

    /// <summary>How many meshes carry the overlay now (the headless drive reads it).</summary>
    public int OverlaidCount
    {
        get
        {
            int n = 0;
            foreach (MeshInstance3D m in _meshes)
                if (m.MaterialOverlay == Negative && Negative != null)
                    n++;
            return n;
        }
    }

    /// <summary>drawLogic() runs while the status is ELECTRIC_DAMAGE: on sets the overlay, off restores what each mesh had.</summary>
    public bool Active
    {
        get => _active;
        set
        {
            if (value == _active)
                return;
            _active = value;
            if (value)
            {
                _previous.Clear();
                foreach (MeshInstance3D m in _meshes)
                {
                    _previous.Add(m.MaterialOverlay);
                    m.MaterialOverlay = Negative;
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
}
