using Godot;

public partial class DustRing : Node3D
{
    [Export]
    public float Duration = 0.25f;

    [Export]
    public float StartScale = 0.8f;

    [Export]
    public float EndScale = 2.2f;

    [Export]
    public float Rise = 0.18f;

    [Export]
    public float StartAlpha = 0.9f;

    private MeshInstance3D _ring;
    private BaseMaterial3D _mat; // StandardMaterial3D derives from this
    private float _t;
    private Vector3 _startPos;
    private Vector3 _normal = Vector3.Up;

    public override void _Ready()
    {
        _ring = GetNode<MeshInstance3D>("Ring");
        _startPos = GlobalPosition;

        _ring.RotationDegrees = new Vector3(-90f, 0f, 0f);
        _ring.Scale = Vector3.One * StartScale;

        // Get material (override preferred), duplicate so this instance is unique
        var src = _ring.MaterialOverride ?? _ring.GetActiveMaterial(0);
        if (src is BaseMaterial3D bm)
        {
            _mat = (BaseMaterial3D)bm.Duplicate(true);
            _ring.MaterialOverride = _mat;

            var c = _mat.AlbedoColor;
            _mat.AlbedoColor = new Color(c.R, c.G, c.B, StartAlpha);
        }
        else
        {
            GD.PushWarning(
                "DustRing: No BaseMaterial3D found. Assign a StandardMaterial3D to Ring."
            );
        }
    }

    public void Init(Vector3 worldPos, Vector3 floorNormal)
    {
        _normal = floorNormal.Normalized();
        GlobalPosition = worldPos + _normal * 0.02f;
        _startPos = GlobalPosition;

        GlobalTransform = new Transform3D(MakeBasisFromUp(_normal), GlobalPosition);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        float u = Mathf.Clamp(_t / Duration, 0f, 1f);
        float s = u * u * (3f - 2f * u);

        float sc = Mathf.Lerp(StartScale, EndScale, s);
        _ring.Scale = Vector3.One * sc;

        GlobalPosition = _startPos + _normal * (Rise * s);

        if (_mat != null)
        {
            float a = Mathf.Lerp(StartAlpha, 0f, s);
            var c = _mat.AlbedoColor;
            _mat.AlbedoColor = new Color(c.R, c.G, c.B, a);
        }

        if (u >= 1f)
            QueueFree();
    }

    private static Basis MakeBasisFromUp(Vector3 up)
    {
        Vector3 y = up.Normalized();
        Vector3 x = y.Cross(Vector3.Forward);
        if (x.Length() < 0.001f)
            x = y.Cross(Vector3.Right);
        x = x.Normalized();
        Vector3 z = x.Cross(y).Normalized();
        return new Basis(x, y, z);
    }
}
