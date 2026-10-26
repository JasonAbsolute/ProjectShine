using Godot;

/// <summary>
/// The EDITOR's view of a placed Stu tower (pass 125h) -- a [Tool] child of Dango.tscn. In the editor it shows what the game will make of the
/// placed Dango: the boss (TBossDangoHamuKuri), its model at bossdangohamukuri.prm's scale (BossPrm.BodyScaleLow x the Stu glb's units), and its
/// tower of StackCount Stu models stacked the way TBossDangoHamuKuri::generateBody + getTakingMtx stack them -- the first body on the boss's null_cap
/// joint + 40 u, each next on the one below's null_cap, all at the boss's yaw (the sway left out: the game's bodies circle 10 u). The models are the
/// glb the scene's Model instances (nothing with a script), UNOWNED: the editor never saves them. StackCount is read off the parent's exported
/// property every editor frame (the parent's script is not a tool script: the editor holds its values in a placeholder) and the tower is rebuilt when
/// it changes. The boss's scale IS written on the owned Model node -- saved into Dango.tscn when that scene itself is saved -- so SmsDangoHamukuri
/// takes its unit scale from <see cref="SmsDangoHamukuri.ModelUnits"/>, never from the node. In the game the helper frees itself at _Ready.
/// Why a helper and not [Tool] on the enemy: a tool script's typed resource exports (DropTable: SmsDropTable) are set by the editor before the C#
/// assembly has typed the .tres -- an InvalidCastException on every early load (the headless editor / import showed it); the helper exports nothing.
/// </summary>
[Tool]
public partial class SmsDangoTowerPreview : Node3D
{
    public const string TowerNodeName = "EditorTower";
    private int _count = -1;
    private Node3D _tower;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) { QueueFree(); return; }
        CallDeferred(MethodName.RefreshNow);                  // the parent is still setting up its children here: an add_child on it would be refused
    }
    private void RefreshNow() => Refresh(true);

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) return;
        Refresh(false);
    }

    private void Refresh(bool force)
    {
        if (GetParent() is not Node3D owner) return;
        Variant v = owner.Get("StackCount");
        int count = v.VariantType == Variant.Type.Int ? v.AsInt32() : 10;
        if (!force && count == _count) return;
        _count = count;
        var model = owner.GetNodeOrNull<Node3D>("Model");
        if (model == null) return;
        // the boss's look: the look code's scale (BossPrm's low) on the model, in the glb's units
        model.Scale = Vector3.One * (SmsDangoHamukuri.ModelUnits * SmsDangoHamukuri.BossPrm.BodyScaleLow);
        _tower = Build(owner, model, count, _tower);
        if (_tower == null) _count = -1;                       // not built (not in the tree yet): try again next frame
        else if (OS.GetEnvironment("SMS_YCAM_SMOKE") == "1")   // the drives' headless editor runs read this
            GD.Print($"[SMS dango editor] {owner.Name}: the boss at x{SmsDangoHamukuri.BossPrm.BodyScaleLow:F1} (model scale {model.Scale.X:F4}), a tower of {_tower.GetChildCount()} (StackCount {count})");
    }

    /// <summary>The tower as the game stacks it, in Stu models (the glb `model` instances; unowned). `count` bodies: the first at the boss's null_cap
    /// joint + 40 u (the joint as the model poses it now), each next at the one below's null_cap (the glb's rest), all at the owner's yaw, at the body
    /// scale (dangohamukuri.prm = the Stu's 1). Returns the tower node (a child of `owner` named <see cref="TowerNodeName"/>), null when the glb is
    /// not there. Static for the drives, which build it at runtime and measure it.</summary>
    public static Node3D Build(Node3D owner, Node3D model, int count, Node3D previous = null)
    {
        if (previous != null && GodotObject.IsInstanceValid(previous)) previous.QueueFree();
        if (model == null || string.IsNullOrEmpty(model.SceneFilePath) || !owner.IsInsideTree()) return null;
        PackedScene glb = GD.Load<PackedScene>(model.SceneFilePath);
        if (glb == null) return null;
        var tower = new Node3D { Name = TowerNodeName };
        owner.AddChild(tower);
        if (!tower.IsInsideTree()) { tower.Free(); return null; }   // the owner refused it (busy setting up): the caller tries again
        Basis yaw = owner.GlobalTransform.Basis.Orthonormalized();
        Vector3 head = Head(model, true);
        float bodyScale = SmsDangoHamukuri.ModelUnits * SmsDangoHamukuri.BodyPrm.BodyScaleLow;
        for (int i = 0; i < count; i++)
        {
            var body = glb.Instantiate<Node3D>();
            body.Name = $"Body{i + 1}";
            tower.AddChild(body);
            body.GlobalTransform = new Transform3D(yaw * Basis.FromScale(Vector3.One * bodyScale), head);
            head = Head(body, false);
        }
        return tower;
    }

    /// <summary>getTakingMtx's point for a model: its null_cap joint in the world (+40 u on the boss); the head's height when the joint is missing.</summary>
    private static Vector3 Head(Node3D model, bool boss)
    {
        Skeleton3D skel = null;
        foreach (Node n in model.FindChildren("*", "Skeleton3D", true, false)) { skel = n as Skeleton3D; if (skel != null) break; }
        int b = skel != null ? skel.FindBone(SmsDangoHamukuri.RideBone) : -1;
        Vector3 p = b >= 0 ? (skel.GlobalTransform * skel.GetBoneGlobalPose(b)).Origin : model.GlobalPosition + Vector3.Up * (117f * model.Scale.Y / SmsDangoHamukuri.ModelUnits / SmsUnits.UnitsPerMetre);
        if (boss) p += Vector3.Up * (40f / SmsUnits.UnitsPerMetre);
        return p;
    }
}
