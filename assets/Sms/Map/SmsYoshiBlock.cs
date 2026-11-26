using System;
using Godot;

/// <summary>
/// The moving spotted cube used by Super Mario Sunshine's secret courses. YoshiBlock maps to TWoodBlock in
/// MarNameRefGen_MapObj.cpp, so it follows TNormalLift's rail at 120 movement ticks per second, multiplies its
/// texture by the RGB tint loaded after the rail name, waits where a rail node has flag 0x800, and uses
/// TWoodBlock::calcRecycle when a node has flag 0x1000: fade for 180 ticks, reset, wait the node's roll ticks
/// while hidden, then reappear. Rail positions remain in game units so generated scenes preserve scene.ral.
/// The same script can drive TNormalLift instances; those use TRailMapObj's flashing recycle instead of the
/// TWoodBlock colour fade and honor the rail's one-shot stop flag 0x100 after an arrival or recycle reset.
/// </summary>
public partial class SmsYoshiBlock : AnimatableBody3D
{
    /// <summary>Rail nodes from scene.ral, in game units and in this node's parent frame.</summary>
    [Export] public Vector3[] RailNodes = Array.Empty<Vector3>();

    /// <summary>Speed for a segment arriving at each node, in game units per 120 Hz movement tick. Zero uses one.</summary>
    [Export] public float[] NodeSpeeds = Array.Empty<float>();

    /// <summary>Wait at each node, in game movement ticks. TNormalLift reads this from pitch when flag 0x800 is set.</summary>
    [Export] public int[] NodeWaitTicks = Array.Empty<int>();

    /// <summary>Raw scene.ral flags. Flag 0x100 stops after arrival; 0x1000 starts the recycle cycle.</summary>
    [Export] public int[] NodeFlags = Array.Empty<int>();

    /// <summary>Hidden delay after a 0x1000 reset, in movement ticks. TNormalLift reads this from the node's roll.</summary>
    [Export] public int[] NodeHiddenTicks = Array.Empty<int>();

    /// <summary>The rail closes from its last node to its first. Otherwise the platform reverses at each end.</summary>
    [Export] public bool ClosedLoop = false;

    /// <summary>The rail node nearest the scene.bin placement, which is the game's starting node.</summary>
    [Export] public int StartNode = 0;

    /// <summary>The first destination node. Minus one makes the game-style nearest-connected choice.</summary>
    [Export] public int StartTowards = -1;

    /// <summary>The scene.bin RGB values divided by 255, handed to the Yoshi block's TEV shader.</summary>
    [Export] public Color TintColor = Colors.White;

    /// <summary>Game units per metre used by ProjectShine.</summary>
    [Export] public float UnitsPerMetre = 74.44f;

    /// <summary>The game's movement clock. TMarDirector performs map-object control 120 times per second.</summary>
    [Export] public float TicksPerSecond = 120f;

    /// <summary>The game uses 180 movement ticks for either recycle presentation.</summary>
    [Export] public int RecycleFadeTicks = 180;

    /// <summary>One preserves the game's speed; this multiplier is exposed for play-test tuning.</summary>
    [Export] public float SpeedScale = 1f;

    /// <summary>Turns rail movement on or off without changing the authored placement.</summary>
    [Export] public bool Active = true;

    private int _current;
    private int _next;
    private int _direction = 1;
    private int _initialNode;
    private int _initialTowards;
    private float _waitSeconds;
    private float _fadeSeconds;
    private float _hiddenSeconds;
    private float _movementTicks;
    private bool _fadePending;
    private bool _stopped;
    private bool _woodBlockRecycle;
    private int _pendingHiddenTicks;
    private bool _firstSegment = true;
    private uint _savedCollisionLayer;
    private uint _savedCollisionMask;

    public override void _Ready()
    {
        SyncToPhysics = true;
        _savedCollisionLayer = CollisionLayer;
        _savedCollisionMask = CollisionMask;
        ApplyTint(this);
        _woodBlockRecycle = HasYoshiTintShader(this);
        SetTransparency(this, 0f);

        if (Engine.IsEditorHint() || RailNodes.Length < 2)
            return;

        _current = Mathf.Clamp(StartNode, 0, RailNodes.Length - 1);
        _initialNode = _current;
        Position = NodeMetres(_current);
        _next = ValidDestination(StartTowards) ? StartTowards : NearestConnected(_current);
        _initialTowards = _next;
        SetDirection(_current, _next);
    }

    private Vector3 NodeMetres(int index) => RailNodes[index] / UnitsPerMetre;

    private bool ValidDestination(int index) => index >= 0 && index < RailNodes.Length && index != _current;

    private int NearestConnected(int index)
    {
        int count = RailNodes.Length;
        int before = index - 1;
        int after = index + 1;
        if (ClosedLoop)
        {
            if (before < 0) before = count - 1;
            if (after >= count) after = 0;
        }
        if (before < 0) return after;
        if (after >= count) return before;
        float beforeDistance = RailNodes[index].DistanceSquaredTo(RailNodes[before]);
        float afterDistance = RailNodes[index].DistanceSquaredTo(RailNodes[after]);
        return beforeDistance <= afterDistance ? before : after;
    }

    private void SetDirection(int from, int to)
    {
        _direction = to > from ? 1 : -1;
        if (ClosedLoop && Math.Abs(to - from) > 1)
            _direction = -_direction;
    }

    private float SegmentSpeed()
    {
        // TNormalLift begins its first segment with constructor speed 1.0. After a node arrival it reads the
        // next destination node's yaw and multiplies by 0.01 game units per tick.
        float unitsPerTick = 1f;
        if (!_firstSegment && _next < NodeSpeeds.Length && NodeSpeeds[_next] > 0f)
            unitsPerTick = NodeSpeeds[_next];
        return unitsPerTick * TicksPerSecond / UnitsPerMetre * SpeedScale;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Active || RailNodes.Length < 2)
            return;

        float dt = (float)delta;
        _movementTicks += dt * TicksPerSecond;
        if (_fadeSeconds > 0f)
        {
            _fadeSeconds = Mathf.Max(0f, _fadeSeconds - dt);
            float duration = Mathf.Max(1, RecycleFadeTicks) / TicksPerSecond;
            if (_woodBlockRecycle)
            {
                SetTransparency(this, 1f - _fadeSeconds / duration);
            }
            else
            {
                // TRailMapObj::calcRecycle displays three of every four 4-tick groups for the first half,
                // then alternating 4-tick groups for the second half. Rendering changes; collision remains.
                float ticksRemaining = _fadeSeconds * TicksPerSecond;
                int group = (int)MathF.Floor(_movementTicks / 4f);
                bool visible = ticksRemaining < RecycleFadeTicks / 2f ? group % 2 != 0 : group % 4 != 0;
                SetTransparency(this, visible ? 0f : 1f);
            }
            if (_fadeSeconds <= 0f)
                ResetAfterFade();
            return;
        }
        if (_hiddenSeconds > 0f)
        {
            _hiddenSeconds = Mathf.Max(0f, _hiddenSeconds - dt);
            if (_hiddenSeconds <= 0f)
                ShowAfterReset();
            return;
        }
        if (_waitSeconds > 0f)
        {
            _waitSeconds = Mathf.Max(0f, _waitSeconds - dt);
            return;
        }
        if (_fadePending)
        {
            _fadePending = false;
            _fadeSeconds = Mathf.Max(1, RecycleFadeTicks) / TicksPerSecond;
            return;
        }
        if (_stopped)
            return;

        Vector3 target = NodeMetres(_next);
        float step = SegmentSpeed() * dt;
        if (Position.DistanceTo(target) <= step)
        {
            Position = target;
            _current = _next;
            _firstSegment = false;
            ArriveAtNode();
        }
        else
        {
            Position = Position.MoveToward(target, step);
        }
    }

    private void ArriveAtNode()
    {
        int flags = _current < NodeFlags.Length ? NodeFlags[_current] : 0;
        int waitTicks = _current < NodeWaitTicks.Length ? NodeWaitTicks[_current] : 0;
        _waitSeconds = waitTicks / TicksPerSecond;
        _stopped = (flags & 0x100) != 0;

        if ((flags & 0x1000) != 0)
        {
            _pendingHiddenTicks = _current < NodeHiddenTicks.Length ? NodeHiddenTicks[_current] : 0;
            _fadePending = true;
            return;
        }
        if (!_stopped)
            AdvanceDestination();
    }

    private void AdvanceDestination()
    {
        int count = RailNodes.Length;
        if (ClosedLoop)
        {
            _next = ((_current + _direction) % count + count) % count;
        }
        else
        {
            int candidate = _current + _direction;
            if (candidate < 0 || candidate >= count)
            {
                _direction = -_direction;
                candidate = _current + _direction;
            }
            _next = candidate;
        }
    }

    private void ResetAfterFade()
    {
        CollisionLayer = 0;
        CollisionMask = 0;
        SetTransparency(this, 1f);
        _current = _initialNode;
        _next = _initialTowards;
        Position = NodeMetres(_current);
        SetDirection(_current, _next);
        _firstSegment = true;
        int initialFlags = _current < NodeFlags.Length ? NodeFlags[_current] : 0;
        _stopped = (initialFlags & 0x100) != 0;
        _waitSeconds = (initialFlags & 0x800) != 0 && _current < NodeWaitTicks.Length
            ? NodeWaitTicks[_current] / TicksPerSecond
            : 0f;
        _hiddenSeconds = _pendingHiddenTicks / TicksPerSecond;
        _pendingHiddenTicks = 0;
        if (_hiddenSeconds <= 0f)
            ShowAfterReset();
    }

    private void ShowAfterReset()
    {
        CollisionLayer = _savedCollisionLayer;
        CollisionMask = _savedCollisionMask;
        SetTransparency(this, 0f);
    }

    /// <summary>The tint goes to the block's own material. The game's material (yoshiblock.bmd _o1) does not MULTIPLY the tint into the egg
    /// sheet: its TEV stage compares the intensity sheet with the tint and draws the sheet where it is brighter, the tint where it is darker --
    /// a white block with coloured spots. YoshiBlock.tscn carries that stage as SmsYoshiBlock.gdshader on the mesh; this hands it the tint. A
    /// StandardMaterial3D (an older scene) gets the plain multiply as before.</summary>
    private void ApplyTint(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is ShaderMaterial shader)
                {
                    var unique = (ShaderMaterial)shader.Duplicate();
                    unique.SetShaderParameter("tint", TintColor);
                    mesh.SetSurfaceOverrideMaterial(surface, unique);
                }
                else if (mesh.GetActiveMaterial(surface) is StandardMaterial3D material)
                {
                    var unique = (StandardMaterial3D)material.Duplicate();
                    unique.AlbedoColor *= TintColor;
                    mesh.SetSurfaceOverrideMaterial(surface, unique);
                }
            }
        }
        foreach (Node child in node.GetChildren())
            ApplyTint(child);
    }

    private static bool HasYoshiTintShader(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is ShaderMaterial shader && shader.Shader != null
                    && shader.Shader.ResourcePath.EndsWith("/SmsYoshiBlock.gdshader", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        foreach (Node child in node.GetChildren())
        {
            if (HasYoshiTintShader(child))
                return true;
        }
        return false;
    }

    private static void SetTransparency(Node node, float amount)
    {
        if (node is GeometryInstance3D geometry)
            geometry.Transparency = Mathf.Clamp(amount, 0f, 1f);
        foreach (Node child in node.GetChildren())
            SetTransparency(child, amount);
    }
}
