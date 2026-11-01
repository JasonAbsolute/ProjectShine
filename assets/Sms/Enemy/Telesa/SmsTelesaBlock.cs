using Godot;

/// <summary>
/// THE BOO BLOCK -- TJuiceBlock made as ACTOR_TYPE_TELESA_BLOCK (telesablock.bmd; MapObj/JuiceBlock.cpp) for the box Boo (SMS pass 151): the pink
/// Boo-shaped slab a sprayed box Boo turns into, a platform Mario stands on. TBoxTelesa::changeByJuice makes it at the Boo's point with the Boo's
/// yaw, the scale rate (unk140) starting at 0.1 and the Boo's scalingChangeActor growing it 0.02 a tick in X / Z to TSmallEnemyManager::mBlockXZScale
/// 1.5 and 0.01 a tick in Y to mBlockYScale 0.6 (a wide, flat slab); changeMove spins it three turns over the first 80 ticks (BlockWaitTime 400 x 0.2;
/// its Y bob is x unk188 = 0 for the Boo) and blinks it (10 ticks hidden in 20) through the last 200 of ChangeBlockTime 3000; changeOut kills it.
/// The port: a StaticBody3D (layer 1, the floor Mario walks) whose BoxShape3D is the glb's own bounds at the live scale, re-sized as it grows.
/// </summary>
public partial class SmsTelesaBlock : StaticBody3D
{
    private const float U = SmsUnits.UnitsPerMetre;
    private Node3D _model; private CollisionShape3D _col; private BoxShape3D _box; private Aabb _aabb = new Aabb(Vector3.Zero, Vector3.One);
    private float _modelScale = 1f;
    /// <summary>unk140: the live scale rate, (0.1, 0.1, 0.1) at birth, grown by the Boo.</summary>
    public float ScaleXZ = 0.1f, ScaleY = 0.1f;
    public float YawDeg;
    private bool _ready;

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>("Model");
        _col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (_model != null)
        {
            _modelScale = _model.Scale.X;
            var mi = _model.FindChild("*", true, false) as MeshInstance3D ?? FirstMesh(_model);
            if (mi != null && mi.Mesh != null) _aabb = mi.Mesh.GetAabb();   // the glb's bounds in model units (x _modelScale -> metres)
        }
        if (_col != null) { _box = new BoxShape3D(); _col.Shape = _box; }
        AddToGroup("sms_telesa_block");
        _ready = true;
        Apply();
    }
    private static MeshInstance3D FirstMesh(Node n)
    {
        foreach (Node c in n.FindChildren("*", "MeshInstance3D", true, false)) return (MeshInstance3D)c;
        return null;
    }
    /// <summary>The Boo's scalingChangeActor / changeMove hand the live scale and yaw here each tick.</summary>
    public void Apply()
    {
        if (!_ready) return;
        Vector3 sc = new Vector3(ScaleXZ, ScaleY, ScaleXZ);
        Basis b = new Basis(Vector3.Up, Mathf.DegToRad(YawDeg));
        if (_model != null) _model.Transform = new Transform3D(b * Basis.FromScale(sc * _modelScale), Vector3.Zero);
        if (_box != null && _col != null)
        {
            Vector3 size = _aabb.Size * _modelScale * sc; size.X = Mathf.Max(size.X, 0.05f); size.Y = Mathf.Max(size.Y, 0.05f); size.Z = Mathf.Max(size.Z, 0.05f);
            _box.Size = size;
            _col.Transform = new Transform3D(b, b * (_aabb.GetCenter() * _modelScale * sc));
        }
    }
    /// <summary>For the drives: the slab's top in metres (what Mario stands on).</summary>
    public float TopY => GlobalPosition.Y + (_aabb.Position.Y + _aabb.Size.Y) * _modelScale * ScaleY;
    public Vector3 SizeNow => _aabb.Size * _modelScale * new Vector3(ScaleXZ, ScaleY, ScaleXZ);
}
