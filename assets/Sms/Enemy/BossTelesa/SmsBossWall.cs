using System;
using Godot;

/// <summary>
/// The casino boss room's WALL and CEILING -- TSirenabossWall (MoveBG/MapObjSirena.cpp; the mapobj boss_wall.bmd, scene.bin's SirenabossWall placed at
/// the origin, scale 1, where the three roulettes and the slot machine are placed too) and the room's own map (delfinoBoss/map: map.bmd, map.col) --
/// for SMS pass 153k: the OUTER RIM of King Boo's arena, the walls Jaysonn asked for.
/// The game: map.col is 48 wall triangles -- a 24-gon of corner radius 2450 u from -1500 to 4000 u -- and 44 ceiling triangles at 4000 u (no floor:
/// the discs' own .col files); boss_wall.bmd paints the same 24-gon (corners at 2464) from -500 to 4050, seen from inside (cull back); map.bmd is the
/// ceiling: the marble ring _B2f_ten (1183..2464 at 4000) and the blue cap _00ura_3 (a 1183 cylinder from 4000 up to 4800, closed). The outer disc's
/// rim (2511) pokes 61 u into the wall, so nothing falls off the table. The wall's TEV and its three btks (TMultiBtk, the drifting coloured spots) are
/// BossWall.gdshader's; the ceiling draws as the game's (texture x vertex colour, unlit). The game lifts its roulette discs 500 u over their placed point
/// (TRoulette::initMapObj) and the arena's discs stand at the arena node instead, so this room sits TableLiftUnits under the node it is dropped on.
/// The port: a StaticBody3D (layer 1) that BUILDS itself at start -- the painted wall (an ArrayMesh, the shader), its collision (a concave polygon of the
/// game's 48 triangles, both sides), the ceiling (the imported map.glb, its materials set unlit / sRGB vertex colours, a trimesh of its faces) -- in the
/// editor too ([Tool]), owner-less, so the scene file stays small and a knob change rebuilds it.
/// </summary>
[Tool]
public partial class SmsBossWall : StaticBody3D
{
    private const float U = SmsUnits.UnitsPerMetre;
    private const string BuiltMeta = "sms_built";

    /// <summary>The painted wall and its collision.</summary>
    [ExportGroup("Room")] [Export] public bool WallOn { get => _wallOn; set { _wallOn = value; Rebuild(); } }
    /// <summary>The ceiling (map.glb: the marble ring and the blue cap) and its collision. Off: the room is open to the sky.</summary>
    [Export] public bool CeilingOn { get => _ceilingOn; set { _ceilingOn = value; Rebuild(); } }
    /// <summary>The game stands its roulette discs 500 u over the point it places them AND this room at; KingBooArena's discs stand at the arena node, so
    /// the room sits this far UNDER the node it is dropped on. 0 if you place the room by hand at the game's own point.</summary>
    [Export(PropertyHint.Range, "0,1500,10")] public float TableLiftUnits { get => _tableLift; set { _tableLift = value; Rebuild(); } }
    /// <summary>The wall and ceiling throw the sun's shadow over the table (a dark indoor room). Off (the game has no shadows): the table stays lit as before.</summary>
    [Export] public bool RoomCastsShadows { get => _shadows; set { _shadows = value; Rebuild(); } }

    /// <summary>The collision 24-gon's corner radius (map.col: 2450 u). The faces' middles are 21 u nearer.</summary>
    [ExportGroup("Wall (the game's map.col and boss_wall.bmd)")] [Export(PropertyHint.Range, "500,6000,1")] public float WallRadiusUnits { get => _radius; set { _radius = value; Rebuild(); } }
    /// <summary>How far outside the collision the painted wall stands (boss_wall.bmd's corners at 2464: 14 u).</summary>
    [Export(PropertyHint.Range, "0,100,1")] public float WallPaintOutsetUnits { get => _paintOutset; set { _paintOutset = value; Rebuild(); } }
    /// <summary>The polygon's columns (24: one star tile each).</summary>
    [Export(PropertyHint.Range, "3,96,1")] public int WallColumns { get => _columns; set { _columns = Math.Max(3, value); Rebuild(); } }
    /// <summary>The painted wall's bottom and top over the game's placing point (-500 / 4050 u).</summary>
    [Export] public float WallBottomUnits { get => _wallBottom; set { _wallBottom = value; Rebuild(); } }
    [Export] public float WallTopUnits { get => _wallTop; set { _wallTop = value; Rebuild(); } }
    /// <summary>The collision's bottom and top (map.col: -1500 / 4000 u -- the wall runs on under the table).</summary>
    [Export] public float WallCollisionBottomUnits { get => _colBottom; set { _colBottom = value; Rebuild(); } }
    [Export] public float WallCollisionTopUnits { get => _colTop; set { _colTop = value; Rebuild(); } }
    /// <summary>The star texture's tiles round the wall and up it (the bmd's uv: 24 x 8).</summary>
    [Export(PropertyHint.Range, "1,96,1")] public int StarTilesAround { get => _tilesAround; set { _tilesAround = Math.Max(1, value); Rebuild(); } }
    [Export(PropertyHint.Range, "1,32,1")] public int StarTilesUp { get => _tilesUp; set { _tilesUp = Math.Max(1, value); Rebuild(); } }

    /// <summary>The wall's base colour over the star texture (GxMaterialColor0 (96, 73, 96): the game's room is dark).</summary>
    [ExportGroup("Look")] [Export] public Color WallTint { get => _tint; set { _tint = value; PushLook(); } }
    /// <summary>The three drifting SPOT layers' colours (K0..K2: (111, 0, 24) red, (96, 96, 0) yellow, (24, 0, 72) violet), added over the base.</summary>
    [Export] public Color SpotColorRed { get => _k0; set { _k0 = value; PushLook(); } }
    [Export] public Color SpotColorYellow { get => _k1; set { _k1 = value; PushLook(); } }
    [Export] public Color SpotColorViolet { get => _k2; set { _k2 = value; PushLook(); } }
    /// <summary>x the spots' drift (the game's three btks at 30 frames a second; 0 = still).</summary>
    [Export(PropertyHint.Range, "0,4,0.05")] public float SpotSpeed { get => _spotSpeed; set { _spotSpeed = value; PushLook(); } }
    /// <summary>DEVIATION knobs: x the spot layers (0 = none) and x the whole wall (1 = the game's).</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float SpotStrength { get => _spotStrength; set { _spotStrength = value; PushLook(); } }
    [Export(PropertyHint.Range, "0,4,0.05")] public float Brightness { get => _brightness; set { _brightness = value; PushLook(); } }

    [ExportGroup("Resources")] [Export] public Texture2D StarsTexture { get => _stars; set { _stars = value; PushLook(); } }
    [Export] public Texture2D SpotsTexture { get => _spots; set { _spots = value; PushLook(); } }
    [Export] public Shader WallShader { get => _shader; set { _shader = value; Rebuild(); } }
    /// <summary>The ceiling model (the game's delfinoBoss map.glb).</summary>
    [Export] public PackedScene CeilingScene { get => _ceilingScene; set { _ceilingScene = value; Rebuild(); } }
    [Export] public bool DebugPrint;

    private bool _wallOn = true, _ceilingOn = true, _shadows;
    private float _tableLift = 500f, _radius = 2450f, _paintOutset = 14f, _wallBottom = -500f, _wallTop = 4050f, _colBottom = -1500f, _colTop = 4000f;
    private int _columns = 24, _tilesAround = 24, _tilesUp = 8;
    private Color _tint = new Color(96f / 255f, 73f / 255f, 96f / 255f), _k0 = new Color(111f / 255f, 0f, 24f / 255f), _k1 = new Color(96f / 255f, 96f / 255f, 0f), _k2 = new Color(24f / 255f, 0f, 72f / 255f);
    private float _spotSpeed = 1f, _spotStrength = 1f, _brightness = 1f;
    private Texture2D _stars, _spots; private Shader _shader; private PackedScene _ceilingScene;
    private ShaderMaterial _wallMat; private bool _ready;

    /// <summary>For the drives: the painted wall's triangles, the collision's, the ceiling trimesh's; the fraction of the painted triangles wound for Godot's
    /// clockwise front face seen from INSIDE (1 = all); the collision radius in metres.</summary>
    public int WallTriangles { get; private set; }
    public int WallCollisionTriangles { get; private set; }
    public int CeilingTriangles { get; private set; }
    public float WallInwardFraction { get; private set; }
    public float WallRadiusMetres => _radius / U;
    public bool Built { get; private set; }

    public override void _Ready()
    {
        _ready = true;
        Rebuild();
    }

    /// <summary>Tear down what was built and build it again (the knobs' setters call this once the node is in the tree).</summary>
    public void Rebuild()
    {
        if (!_ready || !IsInsideTree()) return;
        foreach (Node c in GetChildren())
            if (c.HasMeta(BuiltMeta)) { RemoveChild(c); c.QueueFree(); }
        WallTriangles = WallCollisionTriangles = CeilingTriangles = 0; WallInwardFraction = 0f; _wallMat = null;
        bool editor = Engine.IsEditorHint();
        float lift = -_tableLift / U;
        if (_wallOn)
        {
            BuildWallMesh(lift);
            if (!editor) BuildWallCollision(lift);
        }
        if (_ceilingOn && _ceilingScene != null) BuildCeiling(lift, editor);
        Built = true;
        if (DebugPrint && !editor) GD.Print($"[SMS boss wall] {Name}: wall {WallTriangles} tris painted (inward fraction {WallInwardFraction:F2}), {WallCollisionTriangles} collision; ceiling {CeilingTriangles} tris; r {_radius:F0} u, {_colBottom - _tableLift:F0}..{_colTop - _tableLift:F0} u over the node");
    }

    /// <summary>The painted 24-gon: one quad a column, u = the column (StarTilesAround tiles round), v 0 at the bottom to -StarTilesUp at the top (the
    /// bmd's uv: the stars upright), the normals inward, the triangles wound CLOCKWISE seen from inside (Godot's front face; cull back = the game's).</summary>
    private void BuildWallMesh(float lift)
    {
        int n = _columns;
        float r = (_radius + _paintOutset) / U, yb = _wallBottom / U + lift, yt = _wallTop / U + lift;
        var verts = new Vector3[(n + 1) * 2]; var nrm = new Vector3[(n + 1) * 2]; var uv = new Vector2[(n + 1) * 2]; var idx = new int[n * 6];
        for (int i = 0; i <= n; i++)
        {
            float a = -Mathf.Pi / 2f + Mathf.Tau * i / n;                // the bmd's u 0 at -90 degrees (toward -z); the pattern is the same all round
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            verts[2 * i] = new Vector3(r * c, yb, r * s); verts[2 * i + 1] = new Vector3(r * c, yt, r * s);
            nrm[2 * i] = nrm[2 * i + 1] = new Vector3(-c, 0f, -s);
            float u = _tilesAround * (float)i / n;
            uv[2 * i] = new Vector2(u, 0f); uv[2 * i + 1] = new Vector2(u, -_tilesUp);
        }
        int inward = 0;
        for (int i = 0; i < n; i++)
        {
            int b0 = 2 * i, t0 = 2 * i + 1, b1 = 2 * i + 2, t1 = 2 * i + 3;
            idx[6 * i] = b0; idx[6 * i + 1] = t0; idx[6 * i + 2] = b1;
            idx[6 * i + 3] = b1; idx[6 * i + 4] = t0; idx[6 * i + 5] = t1;
            for (int k = 0; k < 2; k++)
            {
                Vector3 p0 = verts[idx[6 * i + 3 * k]], p1 = verts[idx[6 * i + 3 * k + 1]], p2 = verts[idx[6 * i + 3 * k + 2]];
                Vector3 rh = (p1 - p0).Cross(p2 - p0); Vector3 mid = (p0 + p1 + p2) / 3f;
                if (rh.X * mid.X + rh.Z * mid.Z > 0f) inward++;             // the right-hand normal OUTWARD = clockwise seen from inside = the front face inside
            }
        }
        WallTriangles = n * 2; WallInwardFraction = (float)inward / (n * 2);
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts; arrays[(int)Mesh.ArrayType.Normal] = nrm; arrays[(int)Mesh.ArrayType.TexUV] = uv; arrays[(int)Mesh.ArrayType.Index] = idx;
        var mesh = new ArrayMesh(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _wallMat = new ShaderMaterial { Shader = _shader };
        PushLook();
        var mi = new MeshInstance3D { Name = "WallPaint", Mesh = mesh, MaterialOverride = _shader != null ? _wallMat : null, CastShadow = _shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
        mi.SetMeta(BuiltMeta, true);
        AddChild(mi);
    }

    /// <summary>The shader's knobs onto the wall's material.</summary>
    private void PushLook()
    {
        if (_wallMat == null) return;
        _wallMat.SetShaderParameter("stars_tex", _stars); _wallMat.SetShaderParameter("spots_tex", _spots);
        _wallMat.SetShaderParameter("mat_color", new Vector3(_tint.R, _tint.G, _tint.B));
        _wallMat.SetShaderParameter("k0", new Vector3(_k0.R, _k0.G, _k0.B)); _wallMat.SetShaderParameter("k1", new Vector3(_k1.R, _k1.G, _k1.B)); _wallMat.SetShaderParameter("k2", new Vector3(_k2.R, _k2.G, _k2.B));
        _wallMat.SetShaderParameter("spot_speed", _spotSpeed); _wallMat.SetShaderParameter("spot_strength", _spotStrength); _wallMat.SetShaderParameter("brightness", _brightness);
    }

    /// <summary>map.col's wall: the same 24-gon at the collision radius and heights, a concave polygon that collides on both sides.</summary>
    private void BuildWallCollision(float lift)
    {
        int n = _columns;
        float r = _radius / U, yb = _colBottom / U + lift, yt = _colTop / U + lift;
        var faces = new Vector3[n * 6];
        for (int i = 0; i < n; i++)
        {
            float a0 = -Mathf.Pi / 2f + Mathf.Tau * i / n, a1 = -Mathf.Pi / 2f + Mathf.Tau * (i + 1) / n;
            Vector3 b0 = new Vector3(r * Mathf.Cos(a0), yb, r * Mathf.Sin(a0)), t0 = b0 with { Y = yt };
            Vector3 b1 = new Vector3(r * Mathf.Cos(a1), yb, r * Mathf.Sin(a1)), t1 = b1 with { Y = yt };
            faces[6 * i] = b0; faces[6 * i + 1] = t0; faces[6 * i + 2] = b1;
            faces[6 * i + 3] = b1; faces[6 * i + 4] = t0; faces[6 * i + 5] = t1;
        }
        WallCollisionTriangles = n * 2;
        var cs = new CollisionShape3D { Name = "WallCollision", Shape = new ConcavePolygonShape3D { Data = faces, BackfaceCollision = true } };
        cs.SetMeta(BuiltMeta, true);
        AddChild(cs);
    }

    /// <summary>The ceiling: map.glb at the game's scale, its materials as the game draws them (unlit, texture x the bmd's vertex colours -- which are
    /// display-space values, so sRGB), its shadow as the knob says, and (not in the editor) a trimesh of its faces for the collision.</summary>
    private void BuildCeiling(float lift, bool editor)
    {
        var inst = _ceilingScene.Instantiate<Node3D>();
        inst.Name = "Ceiling"; inst.Position = new Vector3(0f, lift, 0f); inst.Scale = Vector3.One / U;
        inst.SetMeta(BuiltMeta, true);
        AddChild(inst);
        var faces = new System.Collections.Generic.List<Vector3>();
        foreach (Node node in inst.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mi || mi.Mesh == null) continue;
            mi.CastShadow = _shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
            Transform3D rel = GlobalTransform.AffineInverse() * mi.GlobalTransform;
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                if (mi.GetActiveMaterial(s) is StandardMaterial3D sm)
                {
                    var m = (StandardMaterial3D)sm.Duplicate();
                    m.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; m.VertexColorUseAsAlbedo = true; m.VertexColorIsSrgb = true;
                    mi.SetSurfaceOverrideMaterial(s, m);
                }
                if (editor) continue;
                var arrays = mi.Mesh.SurfaceGetArrays(s);
                Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var idxV = arrays[(int)Mesh.ArrayType.Index];
                if (idxV.VariantType != Variant.Type.Nil) { foreach (int k in idxV.AsInt32Array()) faces.Add(rel * verts[k]); }
                else foreach (Vector3 v in verts) faces.Add(rel * v);
            }
        }
        if (editor || faces.Count < 3) return;
        CeilingTriangles = faces.Count / 3;
        var cs = new CollisionShape3D { Name = "CeilingCollision", Shape = new ConcavePolygonShape3D { Data = faces.ToArray(), BackfaceCollision = true } };
        cs.SetMeta(BuiltMeta, true);
        AddChild(cs);
    }
}
