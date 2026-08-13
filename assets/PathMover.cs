using Godot;

[Tool]
public partial class PathMover : PathFollow3D
{
    public enum LoopMode
    {
        PingPong,
        Loop,
        OneShot,
    }

    [Export]
    public NodePath PlatformPath;

    [Export]
    public float Speed = 5.0f;

    [Export]
    public LoopMode Mode = LoopMode.PingPong;

    [Export]
    public float WaitTimeAtEnds = 0.0f;

    [Export]
    public bool Active = true;

    private Color _tintColor = new Color(1, 1, 1, 1);

    [Export(PropertyHint.ColorNoAlpha)]
    public Color TintColor
    {
        get => _tintColor;
        set
        {
            _tintColor = value;
            if (IsInsideTree() && _platform != null) ApplyTint(_platform);
        }
    }

    private AnimatableBody3D _platform;
    private int _direction = 1;
    private float _waitTimer = 0f;
    private bool _stopped = false;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;

        if (PlatformPath == null || PlatformPath.IsEmpty)
        {
            GD.PrintErr("PathMover: PlatformPath is not set!");
            return;
        }

        _platform = GetNode<AnimatableBody3D>(PlatformPath);
        _platform.SyncToPhysics = false;
        _platform.GlobalPosition = GlobalPosition;
        _platform.GlobalRotation = GlobalRotation;
        ApplyTint(_platform);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Active || _stopped || _platform == null) return;
        float dt = (float)delta;

        if (_waitTimer > 0f)
        {
            _waitTimer -= dt;
            return;
        }

        Progress += Speed * dt * _direction;

        switch (Mode)
        {
            case LoopMode.PingPong:
            {
                if (ProgressRatio >= 1.0f)
                {
                    ProgressRatio = 1.0f;
                    _direction = -1;
                    _waitTimer = WaitTimeAtEnds;
                }
                else if (ProgressRatio <= 0.0f)
                {
                    ProgressRatio = 0.0f;
                    _direction = 1;
                    _waitTimer = WaitTimeAtEnds;
                }
                break;
            }
            case LoopMode.Loop:
            {
                break;
            }
            case LoopMode.OneShot:
            {
                if (ProgressRatio >= 1.0f)
                {
                    ProgressRatio = 1.0f;
                    _stopped = true;
                }
                break;
            }
        }

        // Push position/rotation to the platform, but preserve its scale
        _platform.GlobalPosition = GlobalPosition;
        _platform.GlobalRotation = GlobalRotation;
    }

    private void ApplyTint(Node node)
    {
        if (TintColor == new Color(1, 1, 1, 1)) return;

        if (node is MeshInstance3D mesh)
        {
            for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
            {
                var mat = mesh.GetActiveMaterial(i);
                if (mat is StandardMaterial3D stdMat)
                {
                    var unique = (StandardMaterial3D)stdMat.Duplicate();
                    unique.AlbedoColor = TintColor;
                    mesh.SetSurfaceOverrideMaterial(i, unique);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            ApplyTint(child);
        }
    }
}
