using Godot;
using System.Linq;

/// <summary>
/// SMS pass 212: a DEATH PLANE (Jaysonn: "can you now implment the death zone if mario were to fall off?"). The game's: a floor polygon of the
/// death type (TBGCheckData::isDeathPlane) laid under a stage's void -- TMario::thinkSituation (MarioMove.cpp 1886) finds it under him with its height
/// over his feet, the tick he drops through it, and he is done: <see cref="Mario.SmsFallDead"/> -- game over, HP 0, MARIO_STATUS_FALL_DEAD (he falls
/// on, no control), the cry, the camera's hell dead demo (its eye stops and watches him fall away) and the miss's iris; then the miss as for any death
/// (TOO BAD!, a life down, the stage again from its start). Here: a flat plane at this node's height, everywhere (no edges), crossed when Mario's feet
/// (the bottom of his capsule) go from above it to under it between two physics frames -- as the game's check only ever fires on the tick he drops
/// through (from his last height down to this one). The red sheet under it (Preview) shows it in the editor; it is hidden in play.
/// Knobs: Active (off = no death; Mario falls on, as before 212), MarioPath. Readouts for the drives: Deaths, LastFeetY, LastFrame.
/// 261: one in each of the ten secret courses, at y 0 -- the game's own height: the Hillside Cave Secret's and the Secret of Ricco Tower's map.col
/// (coro_ex1 / coro_ex2) hold nothing but this death-plane polygon at y 0 (50000 x 30000 / 60000 x 30000 units) and a catch floor at -16000; the
/// courses' platforms are map objects placed 1700-3400 units above it. Jaysonn's other eight courses sit on the same unit grid with their lowest
/// pieces 16-88 m over y 0, so they take the same plane (their map.col were not to hand to prove it; the node is his to move).
/// </summary>
[GlobalClass]
public partial class SmsDeathPlane : Node3D
{
    /// <summary>Off = the plane does nothing.</summary>
    [Export] public bool Active = true;
    /// <summary>The Mario it watches (else the first in the scene).</summary>
    [Export] public NodePath MarioPath = "../Mario";
    [Export] public bool DebugPrint;

    /// <summary>For the drives: the deaths it dealt, Mario's feet height and the physics frame at the last.</summary>
    public int Deaths { get; private set; }
    public float LastFeetY { get; private set; } = float.NaN;
    public ulong LastFrame { get; private set; }
    public bool MarioFound => _mario != null;

    private Mario _mario;
    private float _prevFeet = float.NaN;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        var preview = GetNodeOrNull<Node3D>("Preview");
        if (preview != null) preview.Visible = false;   // the editor's red sheet only
        // 261: in a course the Mario is the PlayerSpawner's (PlayerSpawner/Mario), made in the spawner's own _Ready -- a plane declared before the
        // spawner in the scene finds nothing yet, so it looks once more once the whole scene is up (deferred), and only then gives up.
        if (!FindMario()) CallDeferred(MethodName.FindMarioLater);
    }

    private bool FindMario()
    {
        _mario = GetNodeOrNull<Mario>(MarioPath) ?? GetTree()?.CurrentScene?.FindChildren("*", "CharacterBody3D", true, false).OfType<Mario>().FirstOrDefault();
        if (_mario != null && DebugPrint) GD.Print($"[SMS death plane] {Name}: at y {GlobalPosition.Y:F2}, watching {_mario.Name}");
        return _mario != null;
    }

    private void FindMarioLater()
    {
        if (_mario == null && !FindMario())
            GD.PushWarning($"[SMS death plane] {GetPath()}: no Mario at {MarioPath} or in the scene -- the plane does nothing");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_mario == null || !IsInstanceValid(_mario)) return;
        float feet = _mario.SmsFeetWorld().Y, plane = GlobalPosition.Y, prev = _prevFeet;
        _prevFeet = feet;
        if (!Active || float.IsNaN(prev) || prev < plane || feet >= plane) return;   // only the frame he drops through it
        if (!_mario.SmsFallDead($"the death plane {Name} at y {plane:F2}")) return;   // already finished (a game over)
        Deaths++; LastFeetY = feet; LastFrame = Engine.GetPhysicsFrames();
        if (DebugPrint) GD.Print($"[SMS death plane] {Name}: Mario dropped through it (his feet {prev:F2} -> {feet:F2}, the plane {plane:F2}) -- the fall death");
    }
}
