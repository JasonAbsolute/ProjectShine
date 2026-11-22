using System.Collections.Generic;
using Godot;

/// <summary>
/// Drives the two sliding triangle blocks in the Dirty Lake secret course. This follows
/// TNormalLift::control and TWoodBlock::calcRecycle in MapObjRailBlock.cpp at 120 movement
/// ticks per second: wait at the start, move once to the far rail node, fade there for 180
/// ticks, teleport to the start at full tint, wait for the reset delay, and repeat.
/// </summary>
public partial class SmsDirtyLakeWoodBlock : AnimatableBody3D
{
    private enum Phase
    {
        HomeWait,
        MoveOut,
        FadeAtEnd,
        ResetDelay,
    }

    /// <summary>The RGB values read by TWoodBlock::load and multiplied into the model texture.</summary>
    [Export(PropertyHint.ColorNoAlpha)] public Color TintColor = Colors.White;

    /// <summary>The far rail node relative to the scene.bin placement, in ProjectShine metres.</summary>
    [Export] public Vector3 MoveOffsetMeters = Vector3.Zero;

    /// <summary>The rail node yaw times 0.01 units per tick, converted to metres per second.</summary>
    [Export] public float MoveSpeedMetersPerSecond = 32.24073f;

    /// <summary>The start node's pitch value: movement ticks spent at the start before moving.</summary>
    [Export] public int StartWaitTicks = 30;

    /// <summary>TWoodBlock::calcRecycle fades the tint at the far node for 180 movement ticks.</summary>
    [Export] public int FadeTicks = 180;

    /// <summary>The far node's roll value: visible movement ticks held at home after a reset.</summary>
    [Export] public int ResetDelayTicks = 45;

    /// <summary>The game's movement tick rate from TMarDirector::direct.</summary>
    [Export] public float TicksPerSecond = 120f;

    /// <summary>Print phase changes during a focused verification run.</summary>
    [Export] public bool DebugPrint;

    private readonly List<StandardMaterial3D> _materials = new();
    private Node3D _model;
    private Vector3 _homePosition;
    private Phase _phase;
    private int _ticksRemaining;
    private float _tickRemainder;

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>("Model");
        if (_model == null)
        {
            GD.PushError($"{Name}: SmsDirtyLakeWoodBlock cannot find its Model child.");
            return;
        }

        _homePosition = Position;
        SyncToPhysics = true;
        CollectMaterials(_model);
        ApplyTint(1f);
        ChangePhase(Phase.HomeWait, StartWaitTicks);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || _model == null || TicksPerSecond <= 0f)
            return;

        _tickRemainder += (float)delta * TicksPerSecond;
        int owed = Mathf.FloorToInt(_tickRemainder);
        _tickRemainder -= owed;
        int steps = owed > 240 ? 240 : owed;
        for (int i = 0; i < steps; i++)
            Tick();
    }

    /// <summary>Runs one TNormalLift/TWoodBlock movement tick.</summary>
    private void Tick()
    {
        switch (_phase)
        {
            case Phase.HomeWait:
                if (_ticksRemaining > 0)
                {
                    _ticksRemaining--;
                    return;
                }
                ChangePhase(Phase.MoveOut, 0);
                break;

            case Phase.MoveOut:
                float step = Mathf.Max(0f, MoveSpeedMetersPerSecond) / TicksPerSecond;
                Position = Position.MoveToward(_homePosition + MoveOffsetMeters, step);
                if (Position.IsEqualApprox(_homePosition + MoveOffsetMeters))
                    ChangePhase(Phase.FadeAtEnd, Mathf.Max(1, FadeTicks));
                break;

            case Phase.FadeAtEnd:
                _ticksRemaining--;
                ApplyTint(Mathf.Clamp((float)_ticksRemaining / Mathf.Max(1, FadeTicks), 0f, 1f));
                if (_ticksRemaining <= 0)
                {
                    Position = _homePosition;
                    ApplyTint(1f);
                    ChangePhase(Phase.ResetDelay, ResetDelayTicks);
                }
                break;

            case Phase.ResetDelay:
                if (_ticksRemaining > 0)
                {
                    _ticksRemaining--;
                    return;
                }
                ChangePhase(Phase.HomeWait, StartWaitTicks);
                break;
        }
    }

    private void ChangePhase(Phase next, int ticks)
    {
        _phase = next;
        _ticksRemaining = Mathf.Max(0, ticks);
        if (DebugPrint)
            GD.Print($"[SmsDirtyLakeWoodBlock] {Name}: {_phase}, {_ticksRemaining} ticks");
    }

    private void CollectMaterials(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                if (mesh.GetActiveMaterial(i) is not StandardMaterial3D source)
                    continue;
                var material = (StandardMaterial3D)source.Duplicate();
                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mesh.SetSurfaceOverrideMaterial(i, material);
                _materials.Add(material);
            }
        }

        foreach (Node child in node.GetChildren())
            CollectMaterials(child);
    }

    private void ApplyTint(float alpha)
    {
        var color = new Color(TintColor.R, TintColor.G, TintColor.B, Mathf.Clamp(alpha, 0f, 1f));
        foreach (StandardMaterial3D material in _materials)
            material.AlbedoColor = color;
    }
}
