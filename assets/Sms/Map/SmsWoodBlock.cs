using System.Collections.Generic;
using Godot;

/// <summary>
/// A Beach Cannon wood platform. TWoodBlock (MapObjRailBlock.cpp) rides its two-node rail,
/// waits at the upper node, fades its TEV tint over 180 movement ticks, stays absent for the
/// upper node's roll value, then returns and repeats. The game advances this at 120 ticks per second.
/// </summary>
public partial class SmsWoodBlock : AnimatableBody3D
{
    private enum Phase { MoveLow, MoveHigh, WaitVisible, Fade, WaitHidden }

    /// <summary>The RGB values read by TWoodBlock::load, multiplied into the model texture.</summary>
    [Export(PropertyHint.ColorNoAlpha)] public Color TintColor = Colors.White;
    /// <summary>The lower rail node relative to the placed upper node, in project metres.</summary>
    [Export] public Vector3 LowOffsetMeters = Vector3.Zero;
    /// <summary>The rail yaw value times 0.01 units per tick, converted to metres per second.</summary>
    [Export] public float MoveSpeedMetersPerSecond = 9.672219f;
    /// <summary>The upper rail node's pitch value: fully visible movement ticks before fading.</summary>
    [Export] public int VisibleWaitTicks;
    /// <summary>The upper rail node's roll value: movement ticks spent absent after recycling.</summary>
    [Export] public int HiddenWaitTicks;
    /// <summary>TWoodBlock::calcRecycle fades the tint over 180 movement ticks.</summary>
    [Export] public int FadeTicks = 180;
    /// <summary>Enable the two-node disappear-and-return cycle. Blocks without a rail remain static.</summary>
    [Export] public bool Recycles;
    /// <summary>Print phase changes for a focused Godot verification run.</summary>
    [Export] public bool DebugPrint;

    private readonly List<StandardMaterial3D> _materials = new();
    private readonly List<CollisionShape3D> _collisions = new();
    private Node3D _model;
    private Vector3 _home;
    private Phase _phase;
    private int _ticks;
    private float _accumulator;

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
        _phase = Recycles ? Phase.MoveLow : Phase.WaitVisible;
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
            case Phase.MoveLow:
                if (MoveTo(_home + LowOffsetMeters)) Change(Phase.MoveHigh, 0);
                break;
            case Phase.MoveHigh:
                if (MoveTo(_home)) Change(Phase.WaitVisible, VisibleWaitTicks);
                break;
            case Phase.WaitVisible:
                if (_ticks-- <= 0) Change(Phase.Fade, Mathf.Max(1, FadeTicks));
                break;
            case Phase.Fade:
                _ticks--;
                ApplyTint(Mathf.Clamp((float)_ticks / Mathf.Max(1, FadeTicks), 0f, 1f));
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
                    Change(Phase.MoveLow, 0);
                }
                break;
        }
    }

    private bool MoveTo(Vector3 target)
    {
        float step = Mathf.Max(0f, MoveSpeedMetersPerSecond) / 120f;
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
                var material = (StandardMaterial3D)source.Duplicate();
                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mesh.SetSurfaceOverrideMaterial(i, material);
                _materials.Add(material);
            }
        }
        foreach (Node child in node.GetChildren()) CollectMaterials(child);
    }

    private void ApplyTint(float alpha)
    {
        var color = new Color(TintColor.R, TintColor.G, TintColor.B, Mathf.Clamp(alpha, 0f, 1f));
        foreach (StandardMaterial3D material in _materials) material.AlbedoColor = color;
    }
}
