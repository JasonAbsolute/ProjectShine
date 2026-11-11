using Godot;

/// <summary>
/// SMS pass 157: the rematch ARENA's ground -- Bouldergeist's planet from Super Mario Galaxy (models/Sms/Rematch/polta_planet.glb: the
/// .dae with its real vertex colours, run through Blender; Galaxy's centimetres read as the port's metres, ArenaScale on the Model child
/// re-sizes it). The collision is built from the model at start: one trimesh per mesh, the vertices carried into this body's space (the
/// glb's scale baked into the triangles, as the roulette discs do). TopY is the highest point (the drives and the intro's spawn heights read
/// it); the intro finds the floor under a point with a ray; 162: Centre / Radius (the vertices' span) place King Boo's teleport spots. 211: the meshes also carry visual layer 2, the scorch marks' decal layer. Knobs: AutoCollision (off: your own CollisionShape3D children), Brightness
/// (an albedo multiplier on every material; 1 = the file's).
/// </summary>
public partial class SmsArenaFloor : StaticBody3D
{
    /// <summary>Build the collision from the Model child's meshes at start.</summary>
    [Export] public bool AutoCollision = true;
    /// <summary>Every material's albedo x this (the Galaxy TEV darkens the textures with its ambient 100 / 255 and material 203 / 255; 1 = the glb as it is).</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float Brightness = 1f;
    [Export] public bool DebugPrint;

    public float TopY { get; private set; } = float.NaN;
    public float BottomY { get; private set; } = float.NaN;
    public int Triangles { get; private set; }
    public int Meshes { get; private set; }
    /// <summary>162: the floor's middle (x / z at TopY) and its radius (half its wider span), from the vertices -- King Boo's teleport spots.</summary>
    public Vector3 Centre { get; private set; }
    public float Radius { get; private set; }
    /// <summary>211: the floor's meshes carry visual layer 2 as well, for the scorch marks' decals.</summary>
    public bool DecalLayerSet { get; private set; }
    private float _minX = float.NaN, _maxX, _minZ, _maxZ;

    public override void _Ready()
    {
        var model = GetNodeOrNull<Node3D>("Model");
        if (model == null) { GD.PushWarning($"[SMS arena] {GetPath()}: no Model child (the arena glb)"); return; }
        foreach (Node n in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            if (mi.Mesh == null) continue;
            Meshes++;
            mi.Layers |= SmsScorchMarks.FloorLayer; DecalLayerSet = true;                           // 211: the scorch marks' decals project onto this layer only (not Mario, not the Boos)
            if (Mathf.Abs(Brightness - 1f) > 1e-3f)
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm)
                    {
                        var copy = (BaseMaterial3D)bm.Duplicate();
                        copy.AlbedoColor = new Color(bm.AlbedoColor.R * Brightness, bm.AlbedoColor.G * Brightness, bm.AlbedoColor.B * Brightness, bm.AlbedoColor.A);
                        mi.SetSurfaceOverrideMaterial(s, copy);
                    }
            Transform3D rel = GlobalTransform.AffineInverse() * mi.GlobalTransform;
            var am = new ArrayMesh();
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var arrays = mi.Mesh.SurfaceGetArrays(s);
                Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] = rel * verts[i];
                    Vector3 w = GlobalTransform * verts[i]; float y = w.Y;
                    if (float.IsNaN(_minX)) { _minX = _maxX = w.X; _minZ = _maxZ = w.Z; }
                    else { _minX = Mathf.Min(_minX, w.X); _maxX = Mathf.Max(_maxX, w.X); _minZ = Mathf.Min(_minZ, w.Z); _maxZ = Mathf.Max(_maxZ, w.Z); }
                    if (float.IsNaN(TopY) || y > TopY) TopY = y;
                    if (float.IsNaN(BottomY) || y < BottomY) BottomY = y;
                }
                var outArrays = new Godot.Collections.Array(); outArrays.Resize((int)Mesh.ArrayType.Max);
                outArrays[(int)Mesh.ArrayType.Vertex] = verts;
                var idx = arrays[(int)Mesh.ArrayType.Index];
                if (idx.VariantType != Variant.Type.Nil) { outArrays[(int)Mesh.ArrayType.Index] = idx; Triangles += idx.AsInt32Array().Length / 3; }
                else Triangles += verts.Length / 3;
                am.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, outArrays);
            }
            if (AutoCollision && !Engine.IsEditorHint() && am.GetSurfaceCount() > 0)
                AddChild(new CollisionShape3D { Name = mi.Name + "Trimesh", Shape = am.CreateTrimeshShape() });
        }
        if (!float.IsNaN(_minX)) { Centre = new Vector3((_minX + _maxX) * 0.5f, TopY, (_minZ + _maxZ) * 0.5f); Radius = Mathf.Max(_maxX - _minX, _maxZ - _minZ) * 0.5f; }
        if (DebugPrint) GD.Print($"[SMS arena] {Name}: {Meshes} meshes, {Triangles} triangles, y {BottomY:F2}..{TopY:F2}, middle {Centre}, radius {Radius:F1}");
    }

    /// <summary>The ground under (x, z) from `fromY` down (a ray on the bodies, `exclude`d ones -- Mario's -- left out): its world y, or null.</summary>
    public float? FloorYUnder(float x, float z, float fromY = 200f, float depth = 400f, Godot.Collections.Array<Rid> exclude = null)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return null;
        var q = PhysicsRayQueryParameters3D.Create(new Vector3(x, fromY, z), new Vector3(x, fromY - depth, z));
        q.CollideWithAreas = false; q.CollideWithBodies = true;
        if (exclude != null) q.Exclude = exclude;
        var hit = space.IntersectRay(q);
        if (hit.Count == 0) return null;
        return hit["position"].AsVector3().Y;
    }
}
