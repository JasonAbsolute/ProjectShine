using Godot;

/// <summary>
/// SMS pass 157: the rematch ARENA's ground -- Bouldergeist's planet from Super Mario Galaxy (models/Sms/Rematch/polta_planet.glb: the
/// .dae with its real vertex colours, run through Blender; Galaxy's centimetres read as the port's metres, ArenaScale on the Model child
/// re-sizes it). The collision is built from the model at start: one trimesh per mesh, the vertices carried into this body's space (the
/// glb's scale baked into the triangles, as the roulette discs do). TopY is the highest point (the drives and the intro's spawn heights read
/// it); the intro finds the floor under a point with a ray; 162: Centre / Radius (the vertices' span) place King Boo's teleport spots. 211: the meshes also carry visual layer 2, the scorch marks' decal layer. Knobs: AutoCollision (off: your own CollisionShape3D children), Brightness
/// (an albedo multiplier on every material; 1 = the file's).
/// 229 (Jaysonn: "keep Bomb Boo spawns a few metres from the rim"): the STONE -- the walkable top -- measured apart from the rock under it and the kerb round it:
/// StoneY is the level with the most vertices (0.1-m bins of world y; the planet's top, 1741 of its 2255 vertices), StoneCentre the middle of the vertices within
/// StoneBand of that level and StoneRadius the widest of them about it (25.1 m here; Radius, the whole model's half-span, is 26.4 -- the rock's shoulder and the
/// broken wall stand out past the stone's edge, and a Boo kept "inside the floor's radius" could still hang over the void). The Bomb Boo supply, the Boos' wander
/// and the volley's wall read it; RimClear(point) is how far inside the stone's edge a point lies (negative: beyond it). StoneRadiusOverride sets it by hand (0 =
/// measured); Current is the arena's floor for anything that needs it without a tree walk (one arena at a time).
/// </summary>
public partial class SmsArenaFloor : StaticBody3D
{
    /// <summary>Build the collision from the Model child's meshes at start.</summary>
    [Export] public bool AutoCollision = true;
    /// <summary>Every material's albedo x this (the Galaxy TEV darkens the textures with its ambient 100 / 255 and material 203 / 255; 1 = the glb as it is).</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float Brightness = 1f;
    [Export] public bool DebugPrint;
    [ExportGroup("The stone (229): the walkable top's own edge")]
    /// <summary>229: the stone's radius set by hand, metres from StoneCentre (0 = measured from the mesh: the widest vertex within StoneBand of the fullest level).</summary>
    [Export(PropertyHint.Range, "0,60,0.1")] public float StoneRadiusOverride = 0f;
    /// <summary>229: how far off the stone's level a vertex may be and still count as the stone (0.2: the planet's top spans 0.1 m; its kerb starts 0.3 m over it).</summary>
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float StoneBand = 0.2f;

    public float TopY { get; private set; } = float.NaN;
    public float BottomY { get; private set; } = float.NaN;
    public int Triangles { get; private set; }
    public int Meshes { get; private set; }
    /// <summary>162: the floor's middle (x / z at TopY) and its radius (half its wider span), from the vertices -- King Boo's teleport spots.</summary>
    public Vector3 Centre { get; private set; }
    public float Radius { get; private set; }
    /// <summary>229: the stone -- the walkable top's level, its middle (at that level) and its radius about that middle; how many vertices made it. 0 until measured.</summary>
    public float StoneY { get; private set; } = float.NaN;
    public Vector3 StoneCentre { get; private set; }
    public float StoneRadius { get; private set; }
    public int StoneVertices { get; private set; }
    /// <summary>229: the arena's floor, for the Boos and the supply (set as it readies, cleared as it leaves the tree).</summary>
    public static SmsArenaFloor Current { get; private set; }
    /// <summary>229: how far inside the stone's edge the point lies, metres (negative: that far beyond it); NaN with no stone measured.</summary>
    public float RimClear(Vector3 p) => StoneRadius > 0f ? StoneRadius - new Vector2(p.X - StoneCentre.X, p.Z - StoneCentre.Z).Length() : float.NaN;
    /// <summary>211: the floor's meshes carry visual layer 2 as well, for the scorch marks' decals.</summary>
    public bool DecalLayerSet { get; private set; }
    private float _minX = float.NaN, _maxX, _minZ, _maxZ;
    private readonly System.Collections.Generic.List<Vector3> _worldVerts = new();

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) Current = this;
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
                    _worldVerts.Add(w);                                                                   // 229: for the stone's measure below
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
        MeasureStone();
        if (DebugPrint) GD.Print($"[SMS arena] {Name}: {Meshes} meshes, {Triangles} triangles, y {BottomY:F2}..{TopY:F2}, middle {Centre}, radius {Radius:F1}; the stone (229): level {StoneY:F2}, middle {StoneCentre}, radius {StoneRadius:F2} from {StoneVertices} vertices{(StoneRadiusOverride > 0f ? " (the override)" : "")}");
    }
    public override void _ExitTree() { if (Current == this) Current = null; }

    /// <summary>229: the STONE from the vertices gathered above -- the fullest 0.1-m level of world y is its top (the planet's flat disc holds 1741 of the model's 2255
    /// vertices; the kerb's 84 and the rock's 430 lie off it), the vertices within StoneBand of that level are its stone, their span's middle is StoneCentre and the widest
    /// of them about it is StoneRadius (25.12 m: the disc's edge; the kerb's foot at 25.3-25.6 and the rock's shoulder out to 26.4 start 0.3 m higher and stay out).
    /// Nothing gathered (no Model): nothing measured. The override replaces the radius only.</summary>
    private void MeasureStone()
    {
        if (_worldVerts.Count == 0) { if (StoneRadiusOverride > 0f) { StoneRadius = StoneRadiusOverride; StoneCentre = Centre; StoneY = TopY; } return; }
        var bins = new System.Collections.Generic.Dictionary<int, int>();
        foreach (var w in _worldVerts) { int k = Mathf.FloorToInt(w.Y / 0.1f); bins[k] = bins.TryGetValue(k, out int c) ? c + 1 : 1; }
        int bestBin = 0, bestCount = -1;
        foreach (var kv in bins) if (kv.Value > bestCount || (kv.Value == bestCount && kv.Key > bestBin)) { bestBin = kv.Key; bestCount = kv.Value; }
        StoneY = bestBin * 0.1f + 0.05f;
        float minX = float.NaN, maxX = 0f, minZ = 0f, maxZ = 0f;
        foreach (var w in _worldVerts)
        {
            if (Mathf.Abs(w.Y - StoneY) > StoneBand) continue;
            if (float.IsNaN(minX)) { minX = maxX = w.X; minZ = maxZ = w.Z; } else { minX = Mathf.Min(minX, w.X); maxX = Mathf.Max(maxX, w.X); minZ = Mathf.Min(minZ, w.Z); maxZ = Mathf.Max(maxZ, w.Z); }
        }
        if (float.IsNaN(minX)) { _worldVerts.Clear(); return; }
        StoneCentre = new Vector3((minX + maxX) * 0.5f, StoneY, (minZ + maxZ) * 0.5f);
        float r = 0f; int n = 0;
        foreach (var w in _worldVerts) { if (Mathf.Abs(w.Y - StoneY) > StoneBand) continue; n++; r = Mathf.Max(r, new Vector2(w.X - StoneCentre.X, w.Z - StoneCentre.Z).Length()); }
        StoneVertices = n; StoneRadius = StoneRadiusOverride > 0f ? StoneRadiusOverride : r;
        _worldVerts.Clear();
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
