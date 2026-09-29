using Godot;

/// <summary>
/// The FLUDD's seat in Player.tscn: the BoneAttachment3D named SmsFluddAttach under Armature/Skeleton3D, on jnt_chest, with
/// assets/Sms/Fludd.tscn instanced as its child -- the pack itself is that scene (SmsFludd), every part a node to edit. Mario.cs
/// (SmsFluddSetup) adopts this node and reads the Fludd child; with no child it builds the pack from code and warns. This script
/// only keeps the seat honest: the bone defaults to jnt_chest, and the editor shows a configuration warning until the scene is in.
/// </summary>
[Tool]
public partial class SmsFluddAttach : BoneAttachment3D
{
    public const string BoneOfThePack = "jnt_chest";

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint() && string.IsNullOrEmpty(BoneName))
            BoneName = BoneOfThePack;
    }

    public override string[] _GetConfigurationWarnings()
    {
        if (SmsFluddModel.FindChild<SmsFludd>(this) == null)
            return new[] { "No FLUDD here: instance res://assets/Sms/Fludd.tscn as a child of this node. Until then the game builds the pack from code." };
        return System.Array.Empty<string>();
    }

    public override void _Notification(int what)
    {
        if (Engine.IsEditorHint() && what == NotificationChildOrderChanged)
            UpdateConfigurationWarnings();
    }
}
