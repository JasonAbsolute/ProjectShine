using Godot;
using System;
using System.Collections.Generic;

/// <summary>EPollutionType (PollutionLayer.hpp 14-26). 8 and 10 are "no layer here".</summary>
public enum SmsPollutionType
{
    Sink = 0,        // POLLUTION_TYPE_SINK: the mud he sinks in (checkSink)
    Fire = 1,        // POLLUTION_TYPE_FIRE: burns (checkGraffitoFire)
    Slip = 2,        // POLLUTION_TYPE_SLIP: the ordinary goop (checkGraffitoSlip)
    GlassWall = 3,   // POLLUTION_TYPE_GLASS_WALL: a wall he cannot cross
    Electric = 4,    // POLLUTION_TYPE_ELECTRIC: the shock (checkGraffitoElec)
    Instakill = 5,   // POLLUTION_TYPE_INSTAKILL: loserExec on contact
    Safe = 6,        // POLLUTION_TYPE_SAFE: dirty and footprints only
    FireRegrow = 7,  // POLLUTION_TYPE_UNK7: a fire layer whose paint above 160 grows 8 a frame (threshold 200)
    None = 10,       // POLLUTION_TYPE_UNK10: no layer under him
}

/// <summary>
/// A goop layer, after TPollutionLayer (Map/PollutionLayer.cpp, PollutionPos.cpp, PollutionAction.cpp, PollutionCount.cpp): a byte
/// map (0 clean .. 255) over a grid of TexelSize-unit texels covering this mesh's XZ extent, with a height map (one depth unit = 40
/// units of height, 0xFF = no surface) so a sample only counts within +-2 depth units (80 units) of the surface. The game's I8 texture
/// of the layer's model IS the map; here the map is uploaded to the goop material's pollution_map uniform (assets/Sms/Fx/Goop.gdshader
/// reads it by world XZ). Stamps (pollute = toward 255, clean = toward 0, a soft disc of the given size, through the height mask), the
/// 30 Hz settle pass (paint under 50 fades 8 a frame; a FireRegrow layer grows paint above 160; a Decay layer loses 2 everywhere), the
/// spread (a layer flagged CanSpread paints 128-unit stamps near Mario every 4th movement tick), the degree (texels above 0) and the
/// Cleaned signal (TPollutionLayer::cleaned: the erase sound + the cleaned-goop particle, your hooks) are the port's.
/// Author one as a MeshInstance3D over the floor (a PlaneMesh 1 cm above a flat floor, or a copy of the terrain mesh): the mesh is
/// both the surface the height map is baked from and what the goop is drawn on. A StaticBody3D / CSG tagged metadata sms_pollution
/// gets an implicit layer over its collision (SmsPollutionMap.EnsureImplicitLayers), painted full, drawn by nothing.
/// Not ported: the wall layers (TPollutionLayerWallPlusX / PlusZ: the paint projected along X / Z), the layer's own particles (the fire /
/// thunder / wave effects, PARTICLE_MS_RAKU_INDAWA), the stamp textures (the default clean / pollute stamps are soft discs here), the
/// joint-object and model stamps (enemies painting with their models), the prohibit areas and the revival stamps.
/// </summary>
[GlobalClass]
public partial class SmsPollutionLayer : MeshInstance3D
{
    public enum Paint { Empty = 0, Full = 1, Texture = 2 }

    [Export] public SmsPollutionType Type = SmsPollutionType.Slip;
    /// <summary>info->mTexelSize: units of floor per texel (the sampling ring is 32 units; a 20 m pond at 32 is a 64 x 64 map).</summary>
    [Export] public float TexelSize = 32f;
    [Export] public Paint InitialPaint = Paint.Full;
    /// <summary>The layer's /scene/map/pollution/&lt;name&gt;.bmp: red = the paint (0 clean .. 255), pixel (x, y) = texel (x, y) from the layer's
    /// -X / -Z corner; the edge rule (-50 a neighbouring empty texel) is applied as the game's initTexImage does.</summary>
    [Export] public Texture2D InitialPaintTexture;
    /// <summary>FLAG_DECAY: every texel loses 2 a frame (a layer that dies on its own).</summary>
    [Export] public bool Decay = false;
    /// <summary>FLAG_CAN_SPREAD: while Mario is near its paint, the layer grows (TPollutionLayer::spread).</summary>
    [Export] public bool CanSpread = false;
    /// <summary>The goop look when the mesh has no material override of its own (default res://assets/Sms/Fx/Goop.gdshader).</summary>
    [Export] public Shader GoopShader;
    /// <summary>The goop's surface texture (goop_albedo). Empty = the type's default from assets/Sms/Fx/Goop: FireGoop for Fire / FireRegrow,
    /// ElectricGoop for Electric, BrownGoop for everything else; OilGoop, PinkGoop and BetaGoop are there to pick.</summary>
    [Export] public Texture2D GoopTexture;
    [Export] public bool DebugPrint = false;

    /// <summary>TPollutionLayer::cleaned: a spot over 100 was being erased -- the game plays MSD_SE_ERASE_SCRAWL and PARTICLE_MS_RAKU_KIE here.</summary>
    [Signal] public delegate void CleanedEventHandler(Vector3 position);

    public int Width { get; private set; }
    public int Height { get; private set; }
    public float MinX { get; private set; }   // the layer's XZ extent (world metres) = TPollutionLayerInfo mLeft / mTop / mRight / mBottom
    public float MinZ { get; private set; }
    public float MaxX { get; private set; }
    public float MaxZ { get; private set; }
    public float VerticalOffsetUnits { get; private set; } // info->mVerticalOffset: depth 0 (units)
    public bool Built { get; private set; }
    /// <summary>Texels above 0 at the last settle pass (the counter the GPU pass counts; TPollutionManager::getPollutionDegree sums them).</summary>
    public int Degree { get; private set; }
    /// <summary>Texels above 0 right after the build (the whole paint).</summary>
    public int InitialDegree { get; private set; }
    /// <summary>A layer SmsPollutionMap made for a tagged body: no mesh, the height map from rays onto that body.</summary>
    public bool Implicit;
    public Node3D ImplicitBody;

    public int PollutedThreshold => Type == SmsPollutionType.FireRegrow ? SmsParams.Pollution.ThresholdRegrow
                                  : Type == SmsPollutionType.Fire ? SmsParams.Pollution.ThresholdFire : SmsParams.Pollution.ThresholdNormal;
    public int PerFrameThreshold => Type == SmsPollutionType.FireRegrow ? SmsParams.Pollution.PerFrameThresholdRegrow
                                  : Type == SmsPollutionType.Fire ? SmsParams.Pollution.PerFrameThresholdFire : SmsParams.Pollution.PerFrameThresholdNormal;

    private byte[] _map;          // mPollutionMap
    private byte[] _height;       // TPollutionPos::mHeightMap (0xFF = no surface)
    private float _texelM;        // metres per texel
    private float _invTexelM;
    private Image _image;
    private ImageTexture _tex;
    private bool _texDirty;
    private ShaderMaterial _mat;
    private bool _ownMaterial;    // the layer made the material (it may set the look); a material of yours only gets the map uniforms
    private double _settleAccum;
    private float _tickAccum;
    private int _spreadTimer;
    private int _effectTimer;
    private int _cleanEffectCounter = 1;
    private int _cleanPosNo;
    private int _cleanCounterX, _cleanCounterZ;
    private static readonly int[] CleanOffX = { -1, 0, 2, 4, 1, -1, -2, 0, 3, -3 };
    private static readonly int[] CleanOffZ = { -1, -1, 0, 2, -2, -3, 0, 3, 0, 1 };
    private static readonly Random Rng = new Random(0x600D);

    public override void _Ready()
    {
        SmsPollutionMap.Register(this);
        if (!Implicit && Mesh != null)
            Build();
    }

    public override void _ExitTree() => SmsPollutionMap.Unregister(this);

    // ---------------------------------------------------------------------------------------------------------------- build
    /// <summary>The grid over this mesh: its world AABB in XZ, a power-of-two texel count (the game's log2 width / height), the height map
    /// rasterised from the mesh's triangles (the top surface where they overlap), the paint, the texture and the material.</summary>
    public void Build()
    {
        var faces = Mesh.GetFaces();
        if (faces == null || faces.Length < 3)
        {
            GD.PushWarning($"SmsPollutionLayer '{Name}': the mesh has no faces -- no layer");
            return;
        }
        Transform3D xf = GlobalTransform;
        var world = new Vector3[faces.Length];
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue, minY = float.MaxValue;
        for (int i = 0; i < faces.Length; i++)
        {
            Vector3 p = xf * faces[i];
            world[i] = p;
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Z < minZ) minZ = p.Z;
            if (p.Z > maxZ) maxZ = p.Z;
            if (p.Y < minY) minY = p.Y;
        }
        SetupGrid(minX, minZ, maxX, maxZ, minY);
        // the height map from the triangles (barycentric in XZ), the highest surface where several cover a texel
        for (int t = 0; t + 2 < world.Length; t += 3)
        {
            Vector3 a = world[t], b = world[t + 1], c = world[t + 2];
            float det = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
            if (MathF.Abs(det) < 1e-9f)
                continue; // a wall or a sliver seen from above
            int s0 = Math.Max(0, (int)MathF.Floor((MathF.Min(a.X, MathF.Min(b.X, c.X)) - MinX) * _invTexelM));
            int s1 = Math.Min(Width - 1, (int)MathF.Floor((MathF.Max(a.X, MathF.Max(b.X, c.X)) - MinX) * _invTexelM));
            int t0 = Math.Max(0, (int)MathF.Floor((MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) - MinZ) * _invTexelM));
            int t1 = Math.Min(Height - 1, (int)MathF.Floor((MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) - MinZ) * _invTexelM));
            float inv = 1f / det;
            for (int tz = t0; tz <= t1; tz++)
            {
                float pz = MinZ + (tz + 0.5f) * _texelM;
                for (int sx = s0; sx <= s1; sx++)
                {
                    float px = MinX + (sx + 0.5f) * _texelM;
                    float u = ((px - a.X) * (c.Z - a.Z) - (c.X - a.X) * (pz - a.Z)) * inv;
                    float v = ((b.X - a.X) * (pz - a.Z) - (px - a.X) * (b.Z - a.Z)) * inv;
                    if (u < -1e-4f || v < -1e-4f || u + v > 1f + 1e-4f)
                        continue;
                    float y = a.Y + u * (b.Y - a.Y) + v * (c.Y - a.Y);
                    int d = DepthOf(y);
                    int idx = tz * Width + sx;
                    if (_height[idx] == 0xFF || d > _height[idx])
                        _height[idx] = (byte)d;
                }
            }
        }
        FinishBuild();
    }

    /// <summary>The implicit layer of a tagged body: the grid over its collision shapes' world AABB, the height map from a ray a texel
    /// down onto that body (its upward faces), painted full.</summary>
    public void BuildFromBody(Node3D body)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        bool any = false;
        void Grow(Aabb box, Transform3D xf)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = xf * box.GetEndpoint(i);
                minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X);
                minZ = MathF.Min(minZ, p.Z); maxZ = MathF.Max(maxZ, p.Z);
                minY = MathF.Min(minY, p.Y); maxY = MathF.Max(maxY, p.Y);
                any = true;
            }
        }
        if (body is CsgShape3D csg)
            Grow(csg.GetAabb(), csg.GlobalTransform);
        foreach (Node n in body.FindChildren("*", "CollisionShape3D", true, false))
            if (n is CollisionShape3D cs && cs.Shape != null)
                Grow(cs.Shape.GetDebugMesh().GetAabb(), cs.GlobalTransform);
        if (!any)
        {
            GD.PushWarning($"SmsPollutionLayer: tagged body '{body.Name}' has no collision shape -- no layer");
            return;
        }
        SetupGrid(minX, minZ, maxX, maxZ, minY);
        var space = GetWorld3D().DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Zero, uint.MaxValue);
        q.CollideWithAreas = false;
        for (int tz = 0; tz < Height; tz++)
        {
            float pz = MinZ + (tz + 0.5f) * _texelM;
            if (pz > MaxZ) break;
            for (int sx = 0; sx < Width; sx++)
            {
                float px = MinX + (sx + 0.5f) * _texelM;
                if (px > MaxX) break;
                q.From = new Vector3(px, maxY + 0.5f, pz);
                q.To = new Vector3(px, minY - 0.5f, pz);
                var hit = space.IntersectRay(q);
                if (hit.Count == 0)
                    continue;
                GodotObject col = hit["collider"].As<GodotObject>();
                if (!(col == body || (col is Node cn && body.IsAncestorOf(cn))))
                    continue;
                if (((Vector3)hit["normal"]).Y <= 0.1f)
                    continue;
                _height[tz * Width + sx] = (byte)DepthOf(((Vector3)hit["position"]).Y);
            }
        }
        FinishBuild();
    }

    private void SetupGrid(float minX, float minZ, float maxX, float maxZ, float minY)
    {
        MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
        VerticalOffsetUnits = SmsUnits.MetresToUnits(minY);
        _texelM = SmsUnits.UnitsToMetres(MathF.Max(1f, TexelSize));
        _invTexelM = 1f / _texelM;
        Width = Pow2Ceil(SmsUnits.MetresToUnits(MaxX - MinX) / TexelSize);
        Height = Pow2Ceil(SmsUnits.MetresToUnits(MaxZ - MinZ) / TexelSize);
        _map = new byte[Width * Height];
        _height = new byte[Width * Height];
        Array.Fill(_height, (byte)0xFF);
    }

    private static int Pow2Ceil(float texels)
    {
        int n = Math.Max(1, (int)MathF.Ceiling(texels));
        int p = 1;
        while (p < n && p < 1024) p <<= 1;
        return p;
    }

    private void FinishBuild()
    {
        // the paint (initTexImage): a texel of the surface gets its depth value less 50 for every neighbour that is no surface (the edge),
        // clamped at 0 (the game's u8 arithmetic wraps there)
        Image src = null;
        if (InitialPaint == Paint.Texture && InitialPaintTexture != null)
            src = InitialPaintTexture.GetImage();
        for (int tz = 0; tz < Height; tz++)
            for (int sx = 0; sx < Width; sx++)
            {
                int idx = tz * Width + sx;
                if (_height[idx] == 0xFF || InitialPaint == Paint.Empty)
                {
                    _map[idx] = 0;
                    continue;
                }
                int depth = 255;
                if (src != null)
                    depth = (sx < src.GetWidth() && tz < src.GetHeight()) ? src.GetPixel(sx, tz).R8 : 0;
                if (depth == 0)
                {
                    _map[idx] = 0;
                    continue;
                }
                int degree = EdgeDegree(sx, tz);
                _map[idx] = (byte)Math.Clamp(depth - degree * SmsParams.Pollution.EdgeAlpha, 0, 255);
            }
        _image = Image.CreateEmpty(Width, Height, false, Image.Format.R8);
        _image.SetData(Width, Height, false, Image.Format.R8, _map);
        _tex = ImageTexture.CreateFromImage(_image);
        Degree = 0;
        for (int i = 0; i < _map.Length; i++)
            if (_map[i] > 0) Degree++;
        InitialDegree = Degree;
        if (Mesh != null)
        {
            _mat = MaterialOverride as ShaderMaterial;
            if (_mat == null)
            {
                Shader sh = GoopShader ?? GD.Load<Shader>("res://assets/Sms/Fx/Goop.gdshader");
                _mat = new ShaderMaterial { Shader = sh };
                MaterialOverride = _mat;
                _ownMaterial = true;
            }
            _mat.SetShaderParameter("pollution_map", _tex);
            _mat.SetShaderParameter("layer_min", new Vector2(MinX, MinZ));
            _mat.SetShaderParameter("layer_inv_size", new Vector2(1f / (Width * _texelM), 1f / (Height * _texelM)));
            _mat.SetShaderParameter("threshold", PollutedThreshold / 255f);
            if (_ownMaterial)
            {
                Texture2D goop = GoopTexture ?? DefaultGoopTexture(Type);
                if (goop != null)
                {
                    _mat.SetShaderParameter("goop_albedo", goop);
                    _mat.SetShaderParameter("blotch_mix", 0f); // the game's texture as painted; no texture -> the procedural blotches (1)
                }
            }
        }
        Built = true;
        if (DebugPrint)
            GD.Print($"[SMS goop] layer '{Name}' {Type}: {Width} x {Height} texels of {TexelSize} u over x {MinX:F2}..{MaxX:F2} z {MinZ:F2}..{MaxZ:F2} m, " +
                     $"{Degree} painted, threshold {PollutedThreshold}{(Implicit ? $" (implicit, body '{ImplicitBody?.Name}')" : "")}");
    }

    /// <summary>The game's goop textures (assets/Sms/Fx/Goop) by type: the fire layers burn orange, the electric one is the teal swirl, the rest
    /// wear the brown paint. Null when the file is not there.</summary>
    public static Texture2D DefaultGoopTexture(SmsPollutionType type)
    {
        string name = type switch
        {
            SmsPollutionType.Fire or SmsPollutionType.FireRegrow => "FireGoop",
            SmsPollutionType.Electric => "ElectricGoop",
            _ => "BrownGoop",
        };
        string path = $"res://assets/Sms/Fx/Goop/{name}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private int EdgeDegree(int x, int z)
    {
        int count = 0;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= Width || nz >= Height || _height[nz * Width + nx] == 0xFF)
                    count++;
            }
        return count;
    }

    // ------------------------------------------------------------------------------------------------------------- mapping
    /// <summary>TPollutionPos::worldToDepth: (y - offset) x 0.025 -- one depth unit is 40 units of height; 0xFF is kept for "no surface".</summary>
    private int DepthOf(float yMetres) => Math.Clamp((int)((SmsUnits.MetresToUnits(yMetres) - VerticalOffsetUnits) * SmsParams.Pollution.DepthScale), 0, 254);
    private int TexS(float x) => (int)((x - MinX) * _invTexelM); // getTexPosS: worldToTexSize(x - mMinX)
    private int TexT(float z) => (int)((z - MinZ) * _invTexelM);
    private bool InGrid(int s, int t) => s >= 0 && s < Width && t >= 0 && t < Height;
    public bool IsInArea(float x, float z) => !(x < MinX || MaxX < x || z < MinZ || MaxZ < z);
    public bool IsInAreaSize(float x, float z, float sMetres) => !(x < MinX - sMetres || MaxX + sMetres < x || z < MinZ - sMetres || MaxZ + sMetres < z);

    /// <summary>TPollutionLayer::isPolluted(x, y, z): inside the extent, the texel's surface within +-2 depth units of y, the paint above the threshold.</summary>
    public bool IsPolluted(Vector3 p)
    {
        if (!Built || !IsInArea(p.X, p.Z))
            return false;
        int s = TexS(p.X), t = TexT(p.Z);
        if (!InGrid(s, t))
            return false;
        int idx = t * Width + s;
        int h = _height[idx];
        if (h >= 0xFF)
            return false;
        int d = DepthOf(p.Y);
        if (d < h - SmsParams.Pollution.DepthTolerance || d > h + SmsParams.Pollution.DepthTolerance)
            return false; // TPollutionPos::isSame
        return _map[idx] > PollutedThreshold;
    }

    /// <summary>The paint at a world point (0..255; -1 off the layer), for tools and tests.</summary>
    public int PaintAt(Vector3 p)
    {
        if (!Built || !IsInArea(p.X, p.Z)) return -1;
        int s = TexS(p.X), t = TexT(p.Z);
        return InGrid(s, t) ? _map[t * Width + s] : -1;
    }

    /// <summary>The surface height (world metres) the layer knows at a world XZ, or NaN where it has none.</summary>
    public float SurfaceYAt(float x, float z)
    {
        if (!Built || !IsInArea(x, z)) return float.NaN;
        int s = TexS(x), t = TexT(z);
        if (!InGrid(s, t) || _height[t * Width + s] == 0xFF) return float.NaN;
        return SmsUnits.UnitsToMetres(_height[t * Width + s] / SmsParams.Pollution.DepthScale + VerticalOffsetUnits);
    }

    // -------------------------------------------------------------------------------------------------------------- stamps
    /// <summary>TPollutionLayer::stamp + the GPU stamp (PollutionCount.cpp doTask / initGXforStamp): a quad of (size / texel) texels centred
    /// on the point, each texel within +-2 depth units of the stamp's height blended toward 255 (pollute, type 1) or toward 0 (clean, type
    /// 0) by the stamp's alpha -- a soft disc standing in for the stamp textures. A stamp under two texels wide draws nothing, as the
    /// game's zero-area quad does. Clean stamps run the cleaned() effect rule.</summary>
    public void Stamp(int stampType, Vector3 p, float sizeUnits)
    {
        if (!Built)
            return;
        if (!IsInAreaSize(p.X, p.Z, SmsUnits.UnitsToMetres(sizeUnits)))
            return;
        int size = (int)(sizeUnits / TexelSize); // worldToTexSize
        int half = size / 2;
        if (half <= 0)
            return;
        int cs = TexS(p.X), ct = TexT(p.Z);
        int depth = DepthOf(p.Y);
        int lo = Math.Max(0, depth - SmsParams.Pollution.DepthTolerance), hi = Math.Min(254, depth + SmsParams.Pollution.DepthTolerance);
        int x1 = cs - half, x2 = cs + half, z1 = ct - half, z2 = ct + half;
        bool clean = stampType == 0;
        bool changed = false;
        float invSpan = 1f / (2 * half);
        for (int z = Math.Max(0, z1); z < Math.Min(Height, z2); z++)
        {
            float v = (z - z1 + 0.5f) * invSpan;
            for (int x = Math.Max(0, x1); x < Math.Min(Width, x2); x++)
            {
                int idx = z * Width + x;
                int h = _height[idx];
                if (h < lo || h > hi)
                    continue; // the height mask: another floor of the layer is not this stamp's
                float u = (x - x1 + 0.5f) * invSpan;
                float a = StampAlpha(u, v);
                if (a <= 0f)
                    continue;
                int m = _map[idx];
                int nm = clean ? (int)MathF.Round(m * (1f - a)) : (int)MathF.Round(m + (255 - m) * a);
                if (nm != m)
                {
                    _map[idx] = (byte)nm;
                    changed = true;
                }
            }
        }
        if (changed)
            _texDirty = true;
        if (clean)
            CleanedEffect(p, sizeUnits);
    }

    /// <summary>The stamp texture's alpha at (u, v) in the quad: a disc, full to 60 % of the radius, fading to the edge.</summary>
    private static float StampAlpha(float u, float v)
    {
        float r = 2f * MathF.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
        if (r <= 0.6f) return 1f;
        if (r >= 1f) return 0f;
        return (1f - r) / 0.4f;
    }

    /// <summary>TPollutionLayer::cleaned (PollutionLayer.cpp 170): every sixth clean call probes one spot from its offset tables (x 32 units); a
    /// polluted spot over 100 there is "being erased": the Cleaned signal (the game's MSD_SE_ERASE_SCRAWL + PARTICLE_MS_RAKU_KIE).</summary>
    private void CleanedEffect(Vector3 p, float sizeUnits)
    {
        _cleanEffectCounter += 1;
        if (_cleanEffectCounter <= 5)
            return;
        _cleanEffectCounter = 0;
        Vector3 pos = p + new Vector3(SmsUnits.UnitsToMetres(32f * CleanOffX[_cleanCounterX]), 0f, SmsUnits.UnitsToMetres(32f * CleanOffZ[_cleanCounterZ]));
        _cleanCounterX += 1;
        _cleanCounterZ += 3;
        if (_cleanCounterX >= 10) _cleanCounterX -= 10;
        if (_cleanCounterZ >= 10) _cleanCounterZ -= 10;
        if (IsPolluted(pos) && PaintAt(pos) > 100)
        {
            EmitSignal(SignalName.Cleaned, pos);
            _cleanPosNo += 1;
            if (_cleanPosNo > 10) _cleanPosNo = 0;
        }
    }

    // ---------------------------------------------------------------------------------------------------- per-frame passes
    public override void _Process(double delta)
    {
        if (!Built)
            return;
        // the counter pass (TPollutionCounterLayer::countTexDegree, a draw-sync pass at the 30 fps render rate): the settle and the count
        _settleAccum += delta;
        int frames = 0;
        while (_settleAccum >= 1.0 / 30.0 && frames < 4)
        {
            _settleAccum -= 1.0 / 30.0;
            frames++;
        }
        if (frames > 0)
            Settle(frames);
        if (_texDirty)
        {
            _image.SetData(Width, Height, false, Image.Format.R8, _map);
            _tex.Update(_image);
            _texDirty = false;
        }
    }

    /// <summary>initGXforPollutionLayer: a FireRegrow layer raises every texel above its per-frame threshold (160) by the step; a Decay layer
    /// lowers every texel by 2; any other layer lowers texels under its per-frame threshold (50 / 128) by 8 -- the half-cleaned rim of a
    /// splash fades out within a quarter second. The count is of texels left above 0.</summary>
    private void Settle(int frames)
    {
        int step = Decay ? SmsParams.Pollution.DecayDelta : SmsParams.Pollution.PerFrameDelta;
        int thr = PerFrameThreshold;
        bool regrow = Type == SmsPollutionType.FireRegrow;
        int count = 0;
        bool changed = false;
        for (int f = 0; f < frames; f++)
        {
            count = 0;
            for (int i = 0; i < _map.Length; i++)
            {
                int m = _map[i];
                if (m == 0)
                    continue;
                int nm = m;
                if (regrow)
                {
                    if (m > thr) nm = Math.Min(255, m + step);
                }
                else if (Decay)
                    nm = Math.Max(0, m - step);
                else if (m < thr)
                    nm = Math.Max(0, m - step);
                if (nm != m)
                {
                    _map[i] = (byte)nm;
                    changed = true;
                }
                if (nm > 0)
                    count++;
            }
        }
        Degree = count;
        if (changed)
            _texDirty = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Built)
        {
            if (Implicit && ImplicitBody != null && IsInstanceValid(ImplicitBody) && ImplicitBody.IsInsideTree())
                BuildFromBody(ImplicitBody);
            return;
        }
        // TPollutionLayer::perform(CUE_MOVE) -> action(), once a movement tick
        int n = SmsUnits.TicksFor(delta, ref _tickAccum);
        for (int i = 0; i < n; i++)
            Action();
    }

    /// <summary>TPollutionLayer::action (PollutionAction.cpp 139): the type's effects (fire / thunder / glass: particles and sounds, not
    /// ported), then with paint within mSpreadArea of Mario (getPollutedPos: five tries) the RAKU_INDAWA effect (not ported) and, flagged
    /// CanSpread, spread(): every fourth tick a 128-unit stamp at a painted spot near Mario (getPollutedPosNear).</summary>
    private void Action()
    {
        if (!GetPollutedPos(SmsParams.Pollution.SpreadArea, out _))
            return;
        _effectTimer += 1;
        if (_effectTimer > 15)
            _effectTimer = 0;
        if (CanSpread)
            Spread();
    }

    private void Spread()
    {
        if (_spreadTimer < SmsParams.Pollution.SpreadFrequency)
        {
            _spreadTimer += 1;
            return;
        }
        _spreadTimer = 0;
        if (GetPollutedPosNear(SmsParams.Pollution.SpreadArea, out Vector3 at))
            SmsPollutionMap.Pollute(at, SmsParams.Pollution.SpreadStampSize);
    }

    /// <summary>getPollutedPos: five random points within range / 2 of Mario at his height -- true at the first polluted one.</summary>
    private bool GetPollutedPos(float rangeUnits, out Vector3 pos)
    {
        float range = SmsUnits.UnitsToMetres(rangeUnits);
        Vector3 mario = SmsPollutionMap.MarioPos;
        for (int i = 0; i < 5; i++)
        {
            pos = new Vector3(range * ((float)Rng.NextDouble() - 0.5f) + mario.X, mario.Y, range * ((float)Rng.NextDouble() - 0.5f) + mario.Z);
            if (IsPolluted(pos))
                return true;
        }
        pos = default;
        return false;
    }

    /// <summary>getPollutedPosNear: five tries at (rnd - 0.5)(0.7 + rnd) x range from Mario; the texel there must be of the layer, its surface
    /// no higher than Mario (else the whole probe fails; a depth-unit comparison here), its paint non-zero. (The decomp reads the z texel
    /// with getTexPosS -- the x mapping -- a game bug; the port maps z by z.)</summary>
    private bool GetPollutedPosNear(float rangeUnits, out Vector3 pos)
    {
        float range = SmsUnits.UnitsToMetres(rangeUnits);
        Vector3 mario = SmsPollutionMap.MarioPos;
        for (int i = 0; i < 5; i++)
        {
            float x = ((float)Rng.NextDouble() - 0.5f) * (SmsParams.Pollution.AreaMinRate + (float)Rng.NextDouble());
            float z = ((float)Rng.NextDouble() - 0.5f) * (SmsParams.Pollution.AreaMinRate + (float)Rng.NextDouble());
            pos = new Vector3(x * range + mario.X, 0f, z * range + mario.Z);
            if (!IsInArea(pos.X, pos.Z))
                continue;
            int s = TexS(pos.X), t = TexT(pos.Z);
            if (!InGrid(s, t))
                continue;
            int idx = t * Width + s;
            if (_height[idx] == 0xFF)
                continue;
            pos.Y = SmsUnits.UnitsToMetres(_height[idx] / SmsParams.Pollution.DepthScale + VerticalOffsetUnits);
            if (_height[idx] > DepthOf(mario.Y))
                return false; // the game: dest->y > Mario's y with the texel's quantised height -- compared in depth units here, since a port layer sits a hair above the floor his feet are on
            if (_map[idx] != 0)
                return true;
        }
        pos = default;
        return false;
    }

    /// <summary>The sms_pollution tag's words -> the type (the port's authoring vocabulary; the game's layers carry the number).</summary>
    public static SmsPollutionType ParseTag(string tag)
    {
        switch ((tag ?? "").Trim().ToLowerInvariant())
        {
            case "fire": case "hot": return SmsPollutionType.Fire;
            case "fire2": case "unk7": case "regrow": case "fireregrow": return SmsPollutionType.FireRegrow;
            case "electric": case "elec": case "shock": return SmsPollutionType.Electric;
            case "slip": case "oil": case "goop": case "paint": case "normal": return SmsPollutionType.Slip;
            case "sink": case "mud": return SmsPollutionType.Sink;
            case "instakill": case "kill": case "lethal": return SmsPollutionType.Instakill;
            case "glasswall": case "glass": case "wall": return SmsPollutionType.GlassWall;
            case "safe": case "clean": case "harmless": return SmsPollutionType.Safe;
            default: return SmsPollutionType.None;
        }
    }
}

/// <summary>
/// TPollutionManager (gpPollution): every layer in the scene, in tree order (the first whose extent holds a point gives its type, as
/// getPollutionType does); the stamps go to every layer; Mario's position for the spread probes. Positions are world metres, stamp
/// sizes are game units (the quad's full width).
/// </summary>
public static class SmsPollutionMap
{
    public static readonly List<SmsPollutionLayer> Layers = new List<SmsPollutionLayer>();
    /// <summary>SMS_GetMarioPos(): the spread and effect probes centre on him (set by Mario every tick).</summary>
    public static Vector3 MarioPos;
    private static Node _implicitScene;

    public static void Register(SmsPollutionLayer l) { if (!Layers.Contains(l)) Layers.Add(l); }
    public static void Unregister(SmsPollutionLayer l) => Layers.Remove(l);

    /// <summary>getPollutionType: the first layer whose XZ extent holds the point (its height is not asked).</summary>
    public static SmsPollutionType GetPollutionType(Vector3 p)
    {
        foreach (var l in Layers)
            if (l.Built && l.IsInArea(p.X, p.Z))
                return l.Type;
        return SmsPollutionType.None;
    }

    public static SmsPollutionLayer LayerAt(Vector3 p)
    {
        foreach (var l in Layers)
            if (l.Built && l.IsInArea(p.X, p.Z))
                return l;
        return null;
    }

    public static bool IsPolluted(Vector3 p)
    {
        foreach (var l in Layers)
            if (l.IsPolluted(p))
                return true;
        return false;
    }

    public static void Stamp(int stampType, Vector3 p, float sizeUnits)
    {
        foreach (var l in Layers)
            l.Stamp(stampType, p, sizeUnits);
    }

    /// <summary>pollute(): stamp type 1.</summary>
    public static void Pollute(Vector3 p, float sizeUnits) => Stamp(1, p, sizeUnits);
    /// <summary>clean(): stamp type 0 (the game skips Delfino Plaza's sea floor below -10 here -- not ported).</summary>
    public static void Clean(Vector3 p, float sizeUnits) => Stamp(0, p, sizeUnits);

    /// <summary>getPollutionDegree: the layers' counts summed.</summary>
    public static int PollutionDegree()
    {
        int d = 0;
        foreach (var l in Layers)
            d += l.Degree;
        return d;
    }

    /// <summary>cleanedAll(): the degree under a mission's threshold (TMapEventSink::mCleanedDegree -- per stage; Mare's events use 42 / 50).</summary>
    public static bool CleanedAll(int cleanedDegree) => PollutionDegree() < cleanedDegree;

    /// <summary>A StaticBody3D / CSG shape with metadata sms_pollution (on it or a parent up to three levels up) is a layer of that type over
    /// its collision, painted full -- the port's shorthand for a floor that is goop all over. Made once per scene.</summary>
    public static void EnsureImplicitLayers(SceneTree tree)
    {
        Node scene = tree?.CurrentScene;
        if (scene == null || scene == _implicitScene)
            return;
        _implicitScene = scene;
        int made = 0;
        var stack = new Stack<Node>();
        stack.Push(scene);
        while (stack.Count > 0)
        {
            Node n = stack.Pop();
            foreach (Node c in n.GetChildren())
                stack.Push(c);
            if (!(n is StaticBody3D || n is CsgShape3D) || n is SmsPollutionLayer)
                continue;
            if (n is CsgShape3D cs && cs.GetParent() is CsgShape3D)
                continue; // a CSG operand: the root shape owns the collision
            SmsPollutionType t = SmsPollutionType.None;
            Node m = n;
            for (int depth = 0; m != null && depth < 3; depth++, m = m.GetParent())
                if (m.HasMeta("sms_pollution"))
                {
                    t = SmsPollutionLayer.ParseTag(m.GetMeta("sms_pollution").AsString());
                    break;
                }
            if (t == SmsPollutionType.None)
                continue;
            var layer = new SmsPollutionLayer
            {
                Name = "SmsGoop_" + n.Name,
                Type = t,
                Implicit = true,
                ImplicitBody = (Node3D)n,
                InitialPaint = SmsPollutionLayer.Paint.Full,
            };
            scene.AddChild(layer);
            made++;
        }
        if (made > 0)
            GD.Print($"[SMS goop] {made} implicit layer(s) for sms_pollution-tagged bodies");
    }
}
