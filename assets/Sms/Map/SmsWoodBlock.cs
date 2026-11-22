using System.Collections.Generic;
using Godot;

/// <summary>
/// A Beach Cannon wood platform (TWoodBlock, MapObjRailBlock.cpp; the Village's and the Hotel Lobby's wood blocks have no rail and stay static).
/// The game's control order (TNormalLift::control) is: the hidden countdown, then the wait countdown, then the recycle, and only when none of those
/// is running does the block step along its rail. Every Beach Cannon wood block sits on its rail's node that carries BOTH the wait flag (0x800,
/// pitch = the wait) and the recycle flag (0x1000, roll = the hidden ticks), and arriving there (at load, and again after every reset) re-arms both,
/// so the step toward the rail's other node is never reached: the block STANDS STILL, waits, fades, vanishes, comes back. 243-246 walked it down
/// to the other node and back before each fade, which the game does not do (Jaysonn: "they should be still").
/// The fade (TWoodBlock::calcRecycle) scales the TEV register the texture is multiplied by -- the block goes to BLACK over 180 movement ticks,
/// opaque (the material has no blending), then disappears for the hidden ticks and returns at full tint. 243-246 faded the alpha instead.
/// The game advances all of this at 120 ticks per second.
/// </summary>
public partial class SmsWoodBlock : AnimatableBody3D
{
    private enum Phase { WaitVisible, Fade, WaitHidden }

    /// <summary>The RGB values read by TWoodBlock::load, multiplied into the model texture (GX_TEVREG0).</summary>
    [Export(PropertyHint.ColorNoAlpha)] public Color TintColor = Colors.White;
    /// <summary>The rail's other node relative to the placed one, in project metres. Kept from the scene data; the game never walks there (see above).</summary>
    [Export] public Vector3 LowOffsetMeters = Vector3.Zero;
    /// <summary>The rail's speed (yaw * 0.01 u a tick) in metres a second. Unused while the block stands still; kept for the scene data.</summary>
    [Export] public float MoveSpeedMetersPerSecond = 9.672219f;
    /// <summary>TNormalLift's constructor speed, one game unit per tick. Unused while the block stands still.</summary>
    [Export] public float InitialMoveSpeedMetersPerSecond = 120f / 74.44f;
    /// <summary>The placed node's pitch value: movement ticks fully visible before the fade.</summary>
    [Export] public int VisibleWaitTicks;
    /// <summary>The placed node's roll value: movement ticks spent absent after the fade.</summary>
    [Export] public int HiddenWaitTicks;
    /// <summary>TWoodBlock::calcRecycle fades the tint to black over 180 movement ticks.</summary>
    [Export] public int FadeTicks = 180;
    /// <summary>Enable the wait / fade / hidden cycle. Blocks without a rail remain static.</summary>
    [Export] public bool Recycles;
    /// <summary>247, a deviation knob: walk down to the rail's other node and back before each fade (243-246's behaviour). Off = the game's (still).</summary>
    [Export] public bool WalkTheRail = false;
    /// <summary>Print phase changes for a focused Godot verification run.</summary>
    [Export] public bool DebugPrint;

    private readonly List<StandardMaterial3D> _materials = new();
    private readonly List<CollisionShape3D> _collisions = new();
    private Node3D _model;
    private Vector3 _home;
    private Phase _phase;
    private int _ticks;
    private float _accumulator;
    private bool _walkingDown, _walkedDown, _firstOutbound = true;

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>("Model");
        if (_model == null)
        {
            GD.PushError($"[SmsWoodBlock] '{Name}' is missing its Model child.");
            return;
        }
        _home = Position;
        SyncToPhysics = true;
        CollectMaterials(_model);
        foreach (Node node in FindChildren("*", "CollisionShape3D", true, false))
            if (node is CollisionShape3D shape) _collisions.Add(shape);
        ApplyTint(1f);
        Change(Phase.WaitVisible, VisibleWaitTicks);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Recycles || _model == null) return;
        _accumulator += (float)delta * 120f;
        int steps = Mathf.FloorToInt(_accumulator);
        if (steps > 240) steps = 240;
        _accumulator -= steps;
        for (int i = 0; i < steps; i++) Tick();
    }

    private void Tick()
    {
        switch (_phase)
        {
            case Phase.WaitVisible:
                if (WalkTheRail && !_walkedDown)
                {
                    // the 243-246 look, on request only: down to the other node, back home, then the wait
                    if (!_walkingDown)
                    {
                        float speed = _firstOutbound ? InitialMoveSpeedMetersPerSecond : MoveSpeedMetersPerSecond;
                        if (MoveTo(_home + LowOffsetMeters, speed)) { _firstOutbound = false; _walkingDown = true; }
                    }
                    else if (MoveTo(_home, MoveSpeedMetersPerSecond)) { _walkedDown = true; _walkingDown = false; }
                    return;
                }
                if (_ticks-- <= 0) Change(Phase.Fade, Mathf.Max(1, FadeTicks));
                break;
            case Phase.Fade:
                _ticks--;
                ApplyTint(Mathf.Clamp((float)_ticks / Mathf.Max(1, FadeTicks), 0f, 1f));   // the tint x (ticks left / 180): to black
                if (_ticks <= 0)
                {
                    SetPresent(false);
                    Position = _home;
                    Change(Phase.WaitHidden, HiddenWaitTicks);
                }
                break;
            case Phase.WaitHidden:
                if (_ticks-- <= 0)
                {
                    ApplyTint(1f);
                    SetPresent(true);
                    _walkedDown = false;
                    Change(Phase.WaitVisible, VisibleWaitTicks);
                }
                break;
        }
    }

    private bool MoveTo(Vector3 target, float speedMetersPerSecond)
    {
        float step = Mathf.Max(0f, speedMetersPerSecond) / 120f;
        Position = Position.MoveToward(target, step);
        return Position.IsEqualApprox(target);
    }

    private void Change(Phase next, int ticks)
    {
        _phase = next;
        _ticks = Mathf.Max(0, ticks);
        if (DebugPrint) GD.Print($"[SmsWoodBlock] {Name}: {_phase}, {_ticks} ticks");
    }

    private void SetPresent(bool present)
    {
        _model.Visible = present;
        foreach (CollisionShape3D shape in _collisions)
            shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, !present);
    }

    private void CollectMaterials(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                if (mesh.GetActiveMaterial(i) is not StandardMaterial3D source) continue;
                var material = (StandardMaterial3D)source.Duplicate();   // opaque, as the game's material: the fade darkens, it does not see through
                mesh.SetSurfaceOverrideMaterial(i, material);
                _materials.Add(material);
            }
        }
        foreach (Node child in node.GetChildren()) CollectMaterials(child);
    }

    /// <summary>The TEV register the texture is multiplied by: the tint scaled toward black (TWoodBlock::calcRecycle, remaining / 180).</summary>
    private void ApplyTint(float multiplier)
    {
        multiplier = Mathf.Clamp(multiplier, 0f, 1f);
        var color = new Color(TintColor.R * multiplier, TintColor.G * multiplier, TintColor.B * multiplier, 1f);
        foreach (StandardMaterial3D material in _materials) material.AlbedoColor = color;
    }
}
