using Godot;

/// <summary>TTalkCursor (System/TalkCursor.cpp): the director's cursor over the one NPC Mario can talk to or lift right now (associateNPC:
/// the model's base matrix set to the NPC's getCursorPos = its position + BodyHeight x scale + CursorHeight). The model is
/// /common/cursor_b/default.bmd: a Y-billboard root (joint 0, matrix kind 2 -- this node turns to face the camera about Y), under it
/// joint 1 (rest 20 units up) with the red chevron, spun 720 deg and bobbed 20 -> 8.8 -> 20 units every 20 frames by icon_rot.bck (100
/// frames, looped, one frame a 30 fps game frame: TTalkCursor never sets SMSGetAnmFrameRate), and joint 2 (75 units up) with the
/// B-button icon quad, a full billboard whose "B" flashes red -> white by icon_flash.brk's K0 (TalkCursor.tscn's AnimationPlayer carries
/// both clips, sampled per game frame by tools/projectshine/gen_talk_cursor.py). Drawn with GX_ALWAYS over the world (the shaders'
/// depth_test_disabled). Shown / hidden by the NPC that owns it.</summary>
public partial class SmsTalkCursor : Node3D
{
    [Export] public NodePath AnimPath = "Anim";
    private AnimationPlayer _anim;

    public override void _Ready()
    {
        _anim = GetNodeOrNull<AnimationPlayer>(AnimPath);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null) return;
        // J3D's Y-billboard: the root's yaw turns its +Z to the eye, nothing else
        Vector3 to = cam.GlobalPosition - GlobalPosition;
        if (to.X * to.X + to.Z * to.Z < 1e-8f) return;
        GlobalRotation = new Vector3(0f, Mathf.Atan2(to.X, to.Z), 0f);
    }

    /// <summary>The cursor point in the NPC's frame (getCursorPos): the NPC sets it once.</summary>
    public void SetRest(Vector3 local) => Position = local;
}
