using Godot;

/// <summary>
/// The FLUDD's seat on Mario's jnt_chest bone, for Player.tscn: a BoneAttachment3D named SmsFluddAttach under Armature/Skeleton3D
/// with this script. In the game Mario.cs adopts the node (SmsFluddSetup) and hangs the pack on it exactly as it would on one it
/// made itself -- the node carries nothing of its own. In the editor this script hangs the same pack on it (SmsFluddModel.Build),
/// housed with the spray nozzle on, as children the scene does not save: the editor shows the pack on his back without owning a
/// copy of it, and Mario.tscn / the levels that instance Player.tscn show it too. A preview only; nothing here runs in the game.
/// </summary>
[Tool]
public partial class SmsFluddAttach : BoneAttachment3D
{
    public const string BoneOfThePack = "jnt_chest";
    private const string PreviewName = "EditorPreview";

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint())
            return;
        if (string.IsNullOrEmpty(BoneName))
            BoneName = BoneOfThePack;
    }

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
            return;
        EnsurePreview();
    }

    /// <summary>The preview once: a rebuild of the assembly re-creates this script over the same children, so an existing
    /// preview is kept, never doubled.</summary>
    private void EnsurePreview()
    {
        if (GetNodeOrNull<Node3D>(PreviewName) != null)
            return;
        var root = new Node3D { Name = PreviewName };
        AddChild(root);
        var parts = SmsFluddModel.Build(root, 0);
        if (parts == null)
            return;
        // Mario starts with the pack housed and the spray nozzle on (SmsFluddPoseTank / SmsFluddSprayNozzleAnim at stow 1)
        SmsFluddModel.Scrub(parts.TankAnim, "wg_house", 1f);
        SmsFluddModel.Scrub(parts.NozzleAnim[0], "normal_wg_house", 1f);
    }
}
