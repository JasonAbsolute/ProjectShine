using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 157 (the King Boo REMATCH, KINGBOO_REMATCH_gdoc.txt): the casino table CRACKS like glass and SHATTERS. <see cref="Crack"/> takes the
/// roulette discs round this node (the `sms_roulette` group within SearchRadius), hides each disc's model and puts a copy of its own
/// triangles in its place (FloorShatter.gdshader, the disc's own textures: nothing changes to the eye), cut into Voronoi cells about random
/// seeds spread over the disc's area (one per CellAreaM2). Over CrackSeconds the cell borders light up as cracks spreading from the table's
/// centre to the rim; HoldSeconds later the table breaks: the discs' collision goes (Mario falls through), every cell flies as a rigid shard --
/// kicked outward and up, tumbling, falling -- and fades out; the glass-break sound (MSD_SE_OBJ_GLASS_BREAK) plays. The shards are freed
/// after ShardSeconds; the discs stay gone. Signals: Cracked (the first crack), Shattered (the break), Finished (the shards gone).
/// DEVIATION: the game has nothing of this -- the Shine appears over the table. Knobs: the timing (CrackSeconds, HoldSeconds, ShardSeconds),
/// the look (CrackWidth, CrackColor, CrackGlow, CellAreaM2), the shards' flight (Kick, Pop, Spin, Gravity, RimDelay, FadeStart) and the sound.
/// </summary>
public partial class SmsFloorShatter : Node3D
{
    private const float U = SmsUnits.UnitsPerMetre;

    /// <summary>The discs taken: the `sms_roulette` bodies within this many metres across and half as many up or down.</summary>
    [Export] public float SearchRadius = 60f;
    [ExportGroup("Timing")]
    /// <summary>The cracks' spread from the centre to the rim, in seconds.</summary>
    [Export(PropertyHint.Range, "0.1,10,0.05")] public float CrackSeconds = 1.4f;
    /// <summary>The cracked table held whole before it breaks.</summary>
    [Export(PropertyHint.Range, "0,10,0.05")] public float HoldSeconds = 0.5f;
    /// <summary>The shards' life after the break (freed then).</summary>
    [Export(PropertyHint.Range, "0.2,10,0.1")] public float ShardSeconds = 2.4f;
    /// <summary>The shards start fading this many seconds after the break (gone at ShardSeconds).</summary>
    [Export(PropertyHint.Range, "0,10,0.1")] public float FadeStart = 1.4f;
    [ExportGroup("Look")]
    /// <summary>One shard per this much table area (square metres): fewer = bigger pieces. the cap is 128 cells a disc (the outer ring reaches it at 8).</summary>
    [Export(PropertyHint.Range, "1,100,0.5")] public float CellAreaM2 = 8f;
    /// <summary>The crack line's width as a fraction of the table's radius.</summary>
    [Export(PropertyHint.Range, "0.001,0.1,0.001")] public float CrackWidth = 0.012f;
    [Export] public Color CrackColor = new Color(0.85f, 0.97f, 1f);
    /// <summary>The cracks' glow (emission).</summary>
    [Export(PropertyHint.Range, "0,8,0.1")] public float CrackGlow = 1.6f;
    [ExportGroup("Flight")]
    /// <summary>Outward speed of a shard (m/s) and its upward pop; its tumble (rad/s); its fall (m/s^2); how much later the rim's cells break than the centre's (s).</summary>
    [Export] public float Kick = 2.5f, Pop = 1.5f, Spin = 2.5f, Gravity = 14f, RimDelay = 0.18f;
    [ExportGroup("Sound")]
    /// <summary>MSD_SE_OBJ_GLASS_BREAK at the break (through SmsActorSound's files).</summary>
    [Export] public bool ShatterSound = true;
    [Export] public string ShatterSe = "MSD_SE_OBJ_GLASS_BREAK";
    [Export] public float ShatterSoundDb = 0f;
    [ExportGroup("")]
    [Export] public int Seed = 157;
    [Export] public bool DebugPrint;

    [Signal] public delegate void CrackedEventHandler();
    [Signal] public delegate void ShatteredEventHandler();
    [Signal] public delegate void FinishedEventHandler();

    public enum Phase { Idle, Cracking, Held, Shattered, Done }
    public Phase State { get; private set; } = Phase.Idle;
    /// <summary>The cracks' reach (0..1) and the seconds since the break -- for the drives.</summary>
    public float CrackProgress { get; private set; }
    public float ShatterTime { get; private set; }
    public int DiscsTaken { get; private set; }
    public int CellsTotal { get; private set; }
    public int TrianglesTotal { get; private set; }

    private readonly List<SmsRoulette> _discs = new();
    private readonly List<MeshInstance3D> _shards = new();
    private readonly List<ShaderMaterial> _mats = new();
    private Shader _shader;
    private float _t;

    public override void _Ready() { SetProcess(false); }

    /// <summary>The first crack: the discs re-cut and the cracks start spreading. Nothing happens twice.</summary>
    public void Crack()
    {
        if (State != Phase.Idle || Engine.IsEditorHint()) return;
        _shader = ResourceLoader.Exists("res://assets/Sms/Rematch/FloorShatter.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/FloorShatter.gdshader") : null;
        if (_shader == null) { GD.PushWarning($"[SMS shatter] {GetPath()}: res://assets/Sms/Rematch/FloorShatter.gdshader is missing -- the table breaks with no cracks shown"); }
        FindDiscs();
        int di = 0;
        foreach (var disc in _discs) BuildShards(disc, di++);
        State = Phase.Cracking; _t = 0f; CrackProgress = 0f; ShatterTime = 0f;
        SetProcess(true);
        Push();
        if (DebugPrint) GD.Print($"[SMS shatter] {Name}: cracking {DiscsTaken} discs, {CellsTotal} cells, {TrianglesTotal} triangles");
        EmitSignal(SignalName.Cracked);
    }

    /// <summary>The break (on its own HoldSeconds after the cracks reach the rim; callable early).</summary>
    public void Shatter()
    {
        if (State == Phase.Idle) Crack();
        if (State != Phase.Cracking && State != Phase.Held) return;
        State = Phase.Shattered; _t = 0f; CrackProgress = 1f; ShatterTime = 1e-3f;
        foreach (var disc in _discs)
        {
            if (disc == null || !IsInstanceValid(disc)) continue;
            disc.SetRollSp(0f);
            disc.CollisionLayer = 0; disc.CollisionMask = 0;
            foreach (Node c in disc.GetChildren()) if (c is CollisionShape3D cs) cs.Disabled = true;
        }
        Push();
        if (ShatterSound && !string.IsNullOrEmpty(ShatterSe))
        {
            var s = SmsActorSound.Stream(ShatterSe);
            if (s != null)
            {
                var p = new AudioStreamPlayer { Name = "ShatterSound", Stream = s, Bus = SmsActorSound.SfxBus, VolumeDb = ShatterSoundDb };
                AddChild(p); p.Finished += p.QueueFree; p.Play();
            }
        }
        if (DebugPrint) GD.Print($"[SMS shatter] {Name}: the table breaks");
        EmitSignal(SignalName.Shattered);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        switch (State)
        {
            case Phase.Cracking:
                _t += dt; CrackProgress = CrackSeconds > 0f ? Mathf.Clamp(_t / CrackSeconds, 0f, 1f) : 1f;
                Push();
                if (_t >= CrackSeconds) { State = Phase.Held; _t = 0f; }
                break;
            case Phase.Held:
                _t += dt;
                if (_t >= HoldSeconds) Shatter();
                break;
            case Phase.Shattered:
                _t += dt; ShatterTime = _t;
                Push();
                if (_t >= ShardSeconds)
                {
                    foreach (var mi in _shards) if (mi != null && IsInstanceValid(mi)) mi.QueueFree();
                    _shards.Clear(); _mats.Clear();
                    State = Phase.Done; SetProcess(false);
                    if (DebugPrint) GD.Print($"[SMS shatter] {Name}: the shards are gone");
                    EmitSignal(SignalName.Finished);
                }
                break;
        }
    }

    private void Push()
    {
        foreach (var m in _mats)
        {
            m.SetShaderParameter("crack", CrackProgress);
            m.SetShaderParameter("shatter_t", ShatterTime);
        }
    }

    /// <summary>The discs round this node (SmsBossTelesa.FindArena's rule: 60 m across, 30 up or down), the small centre disc first.</summary>
    private void FindDiscs()
    {
        _discs.Clear();
        Vector3 me = GlobalPosition;
        foreach (Node n in GetTree().GetNodesInGroup("sms_roulette"))
        {
            if (n is not SmsRoulette r || !r.IsInsideTree()) continue;
            Vector3 d = r.GlobalPosition - me; float dy = MathF.Abs(d.Y); d.Y = 0f;
            if (d.Length() < SearchRadius && dy < SearchRadius * 0.5f) _discs.Add(r);
        }
        _discs.Sort((a, b) => a.RadiusUnits.CompareTo(b.RadiusUnits));
        DiscsTaken = _discs.Count;
        if (_discs.Count == 0) GD.PushWarning($"[SMS shatter] {GetPath()}: no sms_roulette discs within {SearchRadius} m -- nothing to break");
    }

    private struct Tri { public Vector3 A, B, C, Na, Nb, Nc; public Vector2 Ua, Ub, Uc; public Color Ca, Cb, Cc; public bool HasN, HasUv, HasC; public int Cell; }

    /// <summary>One disc's triangles, in its body's space, cut into cells and rebuilt as its Shards mesh; its model hidden.</summary>
    private void BuildShards(SmsRoulette disc, int index)
    {
        var model = disc.GetNodeOrNull<Node3D>("Model");
        if (model == null) return;
        var rng = new Random(Seed * 31 + index);
        var surfaces = new List<(Material mat, List<Tri> tris)>();
        float rMin = float.MaxValue, rMax = 0f;
        foreach (Node n in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            if (mi.Mesh == null) continue;
            Transform3D rel = disc.GlobalTransform.AffineInverse() * mi.GlobalTransform;
            Basis nrel = rel.Basis.Inverse().Transposed();
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var mat = mi.GetActiveMaterial(s);
                if (mat is StandardMaterial3D hidden && hidden.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor && hidden.AlphaScissorThreshold > 1f) continue;   // the roulette's discarded pond dome
                var arrays = mi.Mesh.SurfaceGetArrays(s);
                Vector3[] v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var nv = arrays[(int)Mesh.ArrayType.Normal]; Vector3[] nn = nv.VariantType != Variant.Type.Nil ? nv.AsVector3Array() : null;
                var uvv = arrays[(int)Mesh.ArrayType.TexUV]; Vector2[] uv = uvv.VariantType != Variant.Type.Nil ? uvv.AsVector2Array() : null;
                var cv = arrays[(int)Mesh.ArrayType.Color]; Color[] cc = cv.VariantType != Variant.Type.Nil ? cv.AsColorArray() : null;
                var iv = arrays[(int)Mesh.ArrayType.Index];
                int[] idx = iv.VariantType != Variant.Type.Nil ? iv.AsInt32Array() : null;
                int count = idx != null ? idx.Length : v.Length;
                var tris = new List<Tri>(count / 3);
                for (int t = 0; t + 2 < count; t += 3)
                {
                    int i0 = idx != null ? idx[t] : t, i1 = idx != null ? idx[t + 1] : t + 1, i2 = idx != null ? idx[t + 2] : t + 2;
                    if (i0 >= v.Length || i1 >= v.Length || i2 >= v.Length) continue;
                    var tri = new Tri { A = rel * v[i0], B = rel * v[i1], C = rel * v[i2] };
                    if (nn != null && nn.Length == v.Length) { tri.HasN = true; tri.Na = (nrel * nn[i0]).Normalized(); tri.Nb = (nrel * nn[i1]).Normalized(); tri.Nc = (nrel * nn[i2]).Normalized(); }
                    if (uv != null && uv.Length == v.Length) { tri.HasUv = true; tri.Ua = uv[i0]; tri.Ub = uv[i1]; tri.Uc = uv[i2]; }
                    if (cc != null && cc.Length == v.Length) { tri.HasC = true; tri.Ca = cc[i0]; tri.Cb = cc[i1]; tri.Cc = cc[i2]; }
                    tris.Add(tri);
                    foreach (var p in new[] { tri.A, tri.B, tri.C }) { float r = new Vector2(p.X, p.Z).Length(); rMin = MathF.Min(rMin, r); rMax = MathF.Max(rMax, r); }
                }
                if (tris.Count > 0) surfaces.Add((mat, tris));
            }
        }
        if (surfaces.Count == 0 || rMax <= 0f) return;
        if (rMin > rMax) rMin = 0f;
        // the cells: seeds spread evenly over the disc's area (an annulus for the rings), one per CellAreaM2
        float area = MathF.PI * (rMax * rMax - rMin * rMin);
        int cells = Math.Clamp((int)MathF.Round(area / MathF.Max(0.5f, CellAreaM2)), 4, 128);
        var seeds = new Vector3[cells];
        for (int i = 0; i < cells; i++)
        {
            float u = (float)rng.NextDouble(), a = (float)(rng.NextDouble() * Math.PI * 2.0);
            float r = MathF.Sqrt(Mathf.Lerp(rMin * rMin, rMax * rMax, u));
            seeds[i] = new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
        }
        var cellY = new float[cells]; var cellN = new int[cells]; int triCount = 0;
        foreach (var (_, tris) in surfaces)
            for (int t = 0; t < tris.Count; t++)
            {
                var tri = tris[t];
                Vector3 c = (tri.A + tri.B + tri.C) / 3f;
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < cells; i++) { float d = (c.X - seeds[i].X) * (c.X - seeds[i].X) + (c.Z - seeds[i].Z) * (c.Z - seeds[i].Z); if (d < bd) { bd = d; best = i; } }
                tri.Cell = best; tris[t] = tri;
                cellY[best] += c.Y; cellN[best]++; triCount++;
            }
        for (int i = 0; i < cells; i++) { seeds[i].Y = cellN[i] > 0 ? cellY[i] / cellN[i] : 0f; }
        // the mesh: a surface per source surface, every triangle on its own vertices, CUSTOM0 = the cell's pivot + index
        var mesh = new ArrayMesh();
        var shardMats = new List<ShaderMaterial>();
        foreach (var (mat, tris) in surfaces)
        {
            int n = tris.Count * 3;
            var pv = new Vector3[n]; var pn = new Vector3[n]; var puv = new Vector2[n]; var pc = new Color[n]; var custom = new float[n * 4];
            bool anyN = false, anyUv = false, anyC = false;
            for (int t = 0; t < tris.Count; t++)
            {
                var tri = tris[t]; int b = t * 3;
                pv[b] = tri.A; pv[b + 1] = tri.B; pv[b + 2] = tri.C;
                Vector3 fn = (tri.B - tri.A).Cross(tri.C - tri.A); fn = fn.LengthSquared() > 1e-12f ? fn.Normalized() : Vector3.Up;
                if (tri.HasN) { anyN = true; pn[b] = tri.Na; pn[b + 1] = tri.Nb; pn[b + 2] = tri.Nc; } else { pn[b] = pn[b + 1] = pn[b + 2] = fn; }
                if (tri.HasUv) { anyUv = true; puv[b] = tri.Ua; puv[b + 1] = tri.Ub; puv[b + 2] = tri.Uc; }
                if (tri.HasC) { anyC = true; pc[b] = tri.Ca; pc[b + 1] = tri.Cb; pc[b + 2] = tri.Cc; } else { pc[b] = pc[b + 1] = pc[b + 2] = Colors.White; }
                Vector3 pivot = seeds[tri.Cell];
                for (int k = 0; k < 3; k++) { int o = (b + k) * 4; custom[o] = pivot.X; custom[o + 1] = pivot.Y; custom[o + 2] = pivot.Z; custom[o + 3] = tri.Cell; }
            }
            var arr = new Godot.Collections.Array(); arr.Resize((int)Mesh.ArrayType.Max);
            arr[(int)Mesh.ArrayType.Vertex] = pv;
            arr[(int)Mesh.ArrayType.Normal] = pn;
            if (anyUv) arr[(int)Mesh.ArrayType.TexUV] = puv;
            if (anyC) arr[(int)Mesh.ArrayType.Color] = pc;
            arr[(int)Mesh.ArrayType.Custom0] = custom;
            // CUSTOM0 as four floats: ARRAY_CUSTOM_RGBA_FLOAT (7) at ARRAY_FORMAT_CUSTOM0_SHIFT (13)
            var flags = (Mesh.ArrayFormat)((long)Mesh.ArrayCustomFormat.RgbaFloat << 13);
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr, null, null, flags);
            var sm = new ShaderMaterial { Shader = _shader };
            Texture2D tex = null; Color albedo = Colors.White; bool vertexColor = anyC;
            if (mat is BaseMaterial3D bm)
            {
                tex = bm.AlbedoTexture; albedo = bm.AlbedoColor;
                vertexColor = anyC && bm.VertexColorUseAsAlbedo;
            }
            sm.SetShaderParameter("has_tex", tex != null);
            if (tex != null) sm.SetShaderParameter("albedo_tex", tex);
            sm.SetShaderParameter("albedo_color", albedo);
            sm.SetShaderParameter("use_vertex_color", vertexColor);
            sm.SetShaderParameter("crack_width", CrackWidth);
            sm.SetShaderParameter("crack_color", new Vector3(CrackColor.R, CrackColor.G, CrackColor.B));
            sm.SetShaderParameter("crack_glow", CrackGlow);
            sm.SetShaderParameter("gravity", Gravity); sm.SetShaderParameter("spin", Spin); sm.SetShaderParameter("kick", Kick); sm.SetShaderParameter("pop", Pop);
            sm.SetShaderParameter("rim_delay", RimDelay);
            sm.SetShaderParameter("fade_start", FadeStart); sm.SetShaderParameter("fade_end", ShardSeconds);
            sm.SetShaderParameter("centre", Vector3.Zero);
            sm.SetShaderParameter("radius", rMax);
            sm.SetShaderParameter("cell_count", cells);
            sm.SetShaderParameter("cells", seeds);
            sm.SetShaderParameter("crack", 0f); sm.SetShaderParameter("shatter_t", 0f);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, sm);
            shardMats.Add(sm);
        }
        var shards = new MeshInstance3D { Name = "Shards157", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        disc.AddChild(shards);
        model.Visible = false;
        _shards.Add(shards); _mats.AddRange(shardMats);
        CellsTotal += cells; TrianglesTotal += triCount;
        if (DebugPrint) GD.Print($"[SMS shatter] {disc.Name}: r {rMin:F1}..{rMax:F1} m, {cells} cells, {triCount} triangles in {surfaces.Count} surfaces");
    }
}
