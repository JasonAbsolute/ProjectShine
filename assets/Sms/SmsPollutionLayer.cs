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
/// map (0 clean .. 255) over a grid of TexelSize-unit texels covering this mesh, with a height map (one depth unit = 40 units of
/// height, 0xFF = no surface) so a sample only counts within +-2 depth units (80 units) of the surface. The game's I8 texture of the
/// layer's model IS the map; here the map is uploaded to the goop material's pollution_map uniform (assets/Sms/Fx/Goop.gdshader).
/// The grid lives in the LAYER'S OWN FRAME (the node's rotation and position; its scale is folded into the geometry), so a turned or
/// scaled quad carries its paint, its layout and its swirl with it -- the game's layers are world-axis rectangles, which never turn.
/// Stamps (pollute = toward 255, clean = toward 0, a soft disc of the given size, through the height mask), the 30 Hz settle pass
/// (paint under 50 fades 8 a frame; a FireRegrow layer grows paint above 160; a Decay layer loses 2 everywhere), the spread (a layer
/// flagged CanSpread paints 128-unit stamps near Mario every 4th movement tick), the degree (texels above 0), the Cleaned signal
/// (TPollutionLayer::cleaned: the erase sound + the cleaned-goop particle, your hooks) and the bubbles (action()'s RAKU_INDAWA) are the
/// port's. Author one as a MeshInstance3D over the floor (a PlaneMesh 1 cm above a flat floor, or a copy of the terrain mesh): the mesh
/// is both the surface the height map is baked from and what the goop is drawn on. A StaticBody3D / CSG tagged metadata sms_pollution
/// gets an implicit layer over its collision (SmsPollutionMap.EnsureImplicitLayers), painted full, drawn by nothing.
/// Not ported: the wall layers (TPollutionLayerWallPlusX / PlusZ), the layer's own particles (fire / thunder / wave), the stamp
/// textures (soft discs here), the joint-object and model stamps (enemies painting), the prohibit areas and the revival stamps.
/// </summary>
[GlobalClass]
public partial class SmsPollutionLayer : MeshInstance3D
{
    public enum Paint { Empty = 0, Full = 1, Texture = 2 }

    [Export] public SmsPollutionType Type = SmsPollutionType.Slip;
    /// <summary>info->mTexelSize: units of floor per texel (the sampling ring is 32 units; a 20 m pond at 32 is a 64 x 64 map). Lettering
    /// layouts want 16: Mario is dirty only with all nine ring points (0.86 m) on paint, so a stroke must span about three texels.</summary>
    [Export] public float TexelSize = 32f;
    [Export] public Paint InitialPaint = Paint.Full;
    /// <summary>The layer's /scene/map/pollution/&lt;name&gt;.bmp -- the SHAPE of the goop at the start: red = the paint (0 clean .. 255), the image
    /// stretched over the mesh (image x along the layer's +X, image y along its +Z) and turning with the node; sampled 3 x 3 per texel so
    /// a diagonal edge comes in graded; the edge rule (-50 a neighbouring empty texel) applies as the game's initTexImage does. Only read
    /// with InitialPaint = Texture.</summary>
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
    /// <summary>TPollutionLayer::action's PARTICLE_MS_RAKU_INDAWA: a bubble swells and pops at a random painted spot near Mario (not on fire
    /// layers). The game's particle is not here; a dome in the goop's own texture stands in.</summary>
    [Export] public bool Bubbles = true;
    /// <summary>DEVIATION knob: movement ticks between bubbles while paint is near Mario. The game's RAKU_INDAWA fires every 16; the port's
    /// stand-in reads as whack-a-mole at that rate on a big painted floor, so 48 (2.5 a second) is the default.</summary>
    [Export] public int BubbleEveryTicks = 48;
    [Export] public float BubbleSize = 0.26f;      // the dome's radius at full swell (metres)
    [Export] public float BubbleLife = 2.4f;       // seconds from forming to gone: a slow swell for 70 %, a quiver, the pop at 85 %, the fade and ripple after
    [Export] public int BubbleMax = 32;
    [Export] public bool BubblePop = true;         // the pop throws a ripple ring and a few goop droplets
    [Export] public float RingOpacity = 0.6f;      // the ripple's starting opacity (it wears the goop's own colour, a little lightened)
    [Export] public float RingBrighten = 0.25f;    // how much lighter than the paint the ripple is (0 = the paint's colour exactly)
    [Export] public Shader BubbleShader;           // default res://assets/Sms/Fx/GoopBubble.gdshader
    [Export] public Shader RingShader;             // default res://assets/Sms/Fx/GoopRing.gdshader
    /// <summary>TPollutionLayer::fire(): on a Fire / FireRegrow layer with paint within 1600 units of Mario, every 21st tick a flame (the game:
    /// MS_NEWFIRE_A + _B at x1.5 and SE_EF_FIRE) at a painted spot. A procedural flame billboard stands in (GoopFlame.gdshader).</summary>
    [Export] public bool Flames = true;
    [Export] public int FlameEveryTicks = 20;      // mFireEffectWaitTime: the timer must pass this (a flame every 21 ticks)
    [Export] public float FlameSize = 1.5f;        // the game's setGlobalScale(1.5) on both emitters: x a 0.5 x 0.9 m tongue
    [Export] public float FlameLife = 0.7f;
    [Export] public Shader FlameShader;            // default res://assets/Sms/Fx/GoopFlame.gdshader
    /// <summary>TPollutionLayer::electric(): on an Electric layer with paint within 1000 units of Mario, every 16th tick a bolt (the game:
    /// MS_THUNDER_S and SE_EF_ELEC) at a painted spot -- a jagged lightning bolt climbing up from the goop, as in the game. An upright
    /// billboard stands in (GoopSpark.gdshader).</summary>
    [Export] public bool Sparks = true;
    [Export] public int SparkEveryTicks = 16;      // the game's `mEffectTimer > 15`
    [Export] public float SparkSize = 1.6f;        // the bolt's height (metres); the quad is a quarter as wide
    [Export] public float SparkLife = 0.45f;       // seconds: it climbs for the first 45 %, flickers, fades
    [Export] public Shader SparkShader;            // default res://assets/Sms/Fx/GoopSpark.gdshader
    [Export] public int FxMax = 24;                // flames / sparks alive at once
    [Export] public bool DebugPrint = false;

    /// <summary>TPollutionLayer::cleaned: a spot over 100 was being erased -- the game plays MSD_SE_ERASE_SCRAWL and PARTICLE_MS_RAKU_KIE here.</summary>
    [Signal] public delegate void CleanedEventHandler(Vector3 position);

    public int Width { get; private set; }
    public int Height { get; private set; }
    /// <summary>The layer's extent in ITS OWN frame (metres): TPollutionLayerInfo mLeft / mTop / mRight / mBottom. World points go through ToLocal.</summary>
    public float MinX { get; private set; }
    public float MinZ { get; private set; }
    public float MaxX { get; private set; }
    public float MaxZ { get; private set; }
    public float VerticalOffsetUnits { get; private set; } // info->mVerticalOffset: depth 0 (units, in the layer's frame)
    public bool Built { get; private set; }
    /// <summary>Texels above 0 at the last settle pass (the counter the GPU pass counts; TPollutionManager::getPollutionDegree sums them).</summary>
    public int Degree { get; private set; }
    /// <summary>Texels above 0 right after the build (the whole paint).</summary>
    public int InitialDegree { get; private set; }
    /// <summary>Bubbles alive right now (for tools and tests).</summary>
    public int ActiveBubbles { get { int n = 0; if (_bubbleAge != null) foreach (float a in _bubbleAge) if (a >= 0f) n++; return n; } }
    public int BubblesEmitted { get; private set; }
    /// <summary>A layer SmsPollutionMap made for a tagged body: no mesh, the height map from rays onto that body.</summary>
    public bool Implicit;
    public Node3D ImplicitBody;
    /// <summary>World -> the layer's frame (the node's rotation and origin; metres stay metres).</summary>
    public Transform3D ToLocal => _toLocal;
    public Transform3D ToWorld => _toWorld;

    public int PollutedThreshold => Type == SmsPollutionType.FireRegrow ? SmsParams.Pollution.ThresholdRegrow
                                  : Type == SmsPollutionType.Fire ? SmsParams.Pollution.ThresholdFire : SmsParams.Pollution.ThresholdNormal;
    public int PerFrameThreshold => Type == SmsPollutionType.FireRegrow ? SmsParams.Pollution.PerFrameThresholdRegrow
                                  : Type == SmsPollutionType.Fire ? SmsParams.Pollution.PerFrameThresholdFire : SmsParams.Pollution.PerFrameThresholdNormal;

    private byte[] _map;          // mPollutionMap
    private byte[] _height;       // TPollutionPos::mHeightMap (0xFF = no surface)
    private float _texelM;        // metres per texel
    private float _invTexelM;
    private Transform3D _toLocal = Transform3D.Identity;
    private Transform3D _toWorld = Transform3D.Identity;
    private Image _image;
    private ImageTexture _tex;
    private bool _texDirty;
    private ShaderMaterial _mat;
    private bool _ownMaterial;    // the layer made the material (it may set the look); a material of yours only gets the map uniforms
    private MultiMeshInstance3D _bubbleMmi;
    private MultiMesh _bubbleMm;
    private Vector3[] _bubblePos;
    private float[] _bubbleAge;   // seconds since forming; < 0 = free
    private float[] _bubbleScale; // a little variety in size
    private int _bubbleNext;
    private MultiMeshInstance3D _ringMmi;
    private MultiMesh _ringMm;
    private float[] _ringAge;     // < 0 = free
    private Vector3[] _ringPos;
    private float[] _ringSize;
    private int _ringNext;
    private MultiMeshInstance3D _dropMmi;
    private MultiMesh _dropMm;
    private Vector3[] _dropPos, _dropVel;
    private float[] _dropAge;     // < 0 = free
    private float[] _dropFloor;
    private int _dropNext;
    private FxPool _flames;
    private FxPool _sparks;
    private int _fireTimer;       // fire()'s share of mEffectTimer (the game's single timer is split: the port's bubbles have their own cadence)
    private int _sparkTimer;      // electric()'s
    public int FlamesEmitted => _flames?.Emitted ?? 0;
    public int SparksEmitted => _sparks?.Emitted ?? 0;
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
    private static readonly Transform3D ZeroXf = new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero);

    public override void _Ready()
    {
        SmsPollutionMap.Register(this);
        if (!Implicit && Mesh != null)
            Build();
    }

    public override void _ExitTree() => SmsPollutionMap.Unregister(this);

    // ---------------------------------------------------------------------------------------------------------------- build
    /// <summary>The grid over this mesh in the layer's frame: the node's rotation and origin make the frame (its scale goes into the geometry),
    /// the mesh's extent in that frame is the rectangle, a power-of-two texel count (the game's log2 width / height), the height map
    /// rasterised from the triangles (the top surface where they overlap), then the paint, the texture and the material.</summary>
    public void Build()
    {
        var faces = Mesh.GetFaces();
        if (faces == null || faces.Length < 3)
        {
            GD.PushWarning($"SmsPollutionLayer '{Name}': the mesh has no faces -- no layer");
            return;
        }
        Transform3D g = GlobalTransform;
        _toWorld = new Transform3D(g.Basis.Orthonormalized(), g.Origin);
        _toLocal = _toWorld.AffineInverse();
        var local = new Vector3[faces.Length];
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue, minY = float.MaxValue;
        for (int i = 0; i < faces.Length; i++)
        {
            Vector3 p = _toLocal * (g * faces[i]); // world, then into the frame: the scale survives, the rotation is taken out
            local[i] = p;
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Z < minZ) minZ = p.Z;
            if (p.Z > maxZ) maxZ = p.Z;
            if (p.Y < minY) minY = p.Y;
        }
        SetupGrid(minX, minZ, maxX, maxZ, minY);
        // the height map from the triangles (barycentric in XZ), the highest surface where several cover a texel
        for (int t = 0; t + 2 < local.Length; t += 3)
        {
            Vector3 a = local[t], b = local[t + 1], c = local[t + 2];
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

    /// <summary>The implicit layer of a tagged body: the frame is the world's, the grid over its collision shapes' AABB, the height map from a
    /// ray a texel down onto that body (its upward faces), painted full.</summary>
    public void BuildFromBody(Node3D body)
    {
        _toWorld = Transform3D.Identity;
        _toLocal = Transform3D.Identity;
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
        {
            src = InitialPaintTexture.GetImage();
            if (src != null && src.IsCompressed())
                src.Decompress(); // a VRAM-compressed import (the editor's detect_3d) cannot be read per pixel as is
        }
        float spanX = MathF.Max(1e-6f, MaxX - MinX), spanZ = MathF.Max(1e-6f, MaxZ - MinZ);
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
                {
                    // the game's bmp is one pixel per texel of its layer; here the image is stretched over the mesh's extent in the layer's frame
                    // (it fills the quad and turns with the node), nine samples a texel so a slanted edge comes in graded
                    int sum = 0;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            float u = (sx + 0.5f + ox / 3f) * _texelM / spanX;
                            float v = (tz + 0.5f + oy / 3f) * _texelM / spanZ;
                            if (u < 0f || u >= 1f || v < 0f || v >= 1f)
                                continue;
                            sum += src.GetPixel(Math.Min(src.GetWidth() - 1, (int)(u * src.GetWidth())), Math.Min(src.GetHeight() - 1, (int)(v * src.GetHeight()))).R8;
                        }
                    depth = sum / 9;
                }
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
            _mat.SetShaderParameter("layer_to_local", _toLocal);
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
        if (Bubbles && _bubbleMmi == null)
            SetupBubbles();
        if (Flames && _flames == null && (Type == SmsPollutionType.Fire || Type == SmsPollutionType.FireRegrow))
            _flames = new FxPool(this, "GoopFlames", FlameShader ?? GD.Load<Shader>("res://assets/Sms/Fx/GoopFlame.gdshader"), Math.Max(1, FxMax), 0.25f);
        if (Sparks && _sparks == null && Type == SmsPollutionType.Electric)
            _sparks = new FxPool(this, "GoopSparks", SparkShader ?? GD.Load<Shader>("res://assets/Sms/Fx/GoopSpark.gdshader"), Math.Max(1, FxMax), 0f);
        Built = true;
        if (DebugPrint)
            GD.Print($"[SMS goop] layer '{Name}' {Type}: {Width} x {Height} texels of {TexelSize} u over x {MinX:F2}..{MaxX:F2} z {MinZ:F2}..{MaxZ:F2} m (its frame), " +
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
    private float HeightMetres(int h) => SmsUnits.UnitsToMetres(h / SmsParams.Pollution.DepthScale + VerticalOffsetUnits);
    private int TexS(float x) => (int)((x - MinX) * _invTexelM); // getTexPosS: worldToTexSize(x - mMinX)
    private int TexT(float z) => (int)((z - MinZ) * _invTexelM);
    private bool InGrid(int s, int t) => s >= 0 && s < Width && t >= 0 && t < Height;
    private bool InAreaLocal(float x, float z) => !(x < MinX || MaxX < x || z < MinZ || MaxZ < z);

    /// <summary>isInArea: the world point lies over the layer's rectangle (its height is not asked -- getPollutionType's rule).</summary>
    public bool IsInArea(Vector3 world)
    {
        if (!Built) return false;
        Vector3 l = _toLocal * world;
        return InAreaLocal(l.X, l.Z);
    }

    /// <summary>isInAreaSize: within a stamp's width of the rectangle.</summary>
    public bool IsInAreaSize(Vector3 world, float sMetres)
    {
        if (!Built) return false;
        Vector3 l = _toLocal * world;
        return !(l.X < MinX - sMetres || MaxX + sMetres < l.X || l.Z < MinZ - sMetres || MaxZ + sMetres < l.Z);
    }

    /// <summary>TPollutionLayer::isPolluted(x, y, z): inside the extent, the texel's surface within +-2 depth units of y, the paint above the threshold.</summary>
    public bool IsPolluted(Vector3 world)
    {
        if (!Built)
            return false;
        Vector3 p = _toLocal * world;
        if (!InAreaLocal(p.X, p.Z))
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
    public int PaintAt(Vector3 world)
    {
        if (!Built) return -1;
        Vector3 p = _toLocal * world;
        if (!InAreaLocal(p.X, p.Z)) return -1;
        int s = TexS(p.X), t = TexT(p.Z);
        return InGrid(s, t) ? _map[t * Width + s] : -1;
    }

    /// <summary>The surface height (world metres) the layer knows under a world point, or NaN where it has none.</summary>
    public float SurfaceYAt(Vector3 world)
    {
        if (!Built) return float.NaN;
        Vector3 p = _toLocal * world;
        if (!InAreaLocal(p.X, p.Z)) return float.NaN;
        int s = TexS(p.X), t = TexT(p.Z);
        if (!InGrid(s, t) || _height[t * Width + s] == 0xFF) return float.NaN;
        return (_toWorld * new Vector3(p.X, HeightMetres(_height[t * Width + s]), p.Z)).Y;
    }

    public float SurfaceYAt(float x, float z) => SurfaceYAt(new Vector3(x, 0f, z));

    // -------------------------------------------------------------------------------------------------------------- stamps
    /// <summary>TPollutionLayer::stamp + the GPU stamp (PollutionCount.cpp doTask / initGXforStamp): a quad of (size / texel) texels centred
    /// on the point, each texel within +-2 depth units of the stamp's height blended toward 255 (pollute, type 1) or toward 0 (clean, type
    /// 0) by the stamp's alpha -- a soft disc standing in for the stamp textures. A stamp under two texels wide draws nothing, as the
    /// game's zero-area quad does. Clean stamps run the cleaned() effect rule.</summary>
    public void Stamp(int stampType, Vector3 world, float sizeUnits)
    {
        if (!Built)
            return;
        if (!IsInAreaSize(world, SmsUnits.UnitsToMetres(sizeUnits)))
            return;
        Vector3 p = _toLocal * world;
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
            CleanedEffect(world, sizeUnits);
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
    private void CleanedEffect(Vector3 world, float sizeUnits)
    {
        _cleanEffectCounter += 1;
        if (_cleanEffectCounter <= 5)
            return;
        _cleanEffectCounter = 0;
        Vector3 pos = world + new Vector3(SmsUnits.UnitsToMetres(32f * CleanOffX[_cleanCounterX]), 0f, SmsUnits.UnitsToMetres(32f * CleanOffZ[_cleanCounterZ]));
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
        if (_bubbleMmi != null)
        {
            TickBubbles(delta);
            TickPops(delta);
        }
        _flames?.Tick((float)delta);
        _sparks?.Tick((float)delta);
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
    /// ported), then with paint within mSpreadArea of Mario (getPollutedPos: five tries) the RAKU_INDAWA bubble on a non-fire layer every
    /// BubbleEveryTicks (the game: 16) and, flagged CanSpread, spread(): every fourth tick a 128-unit stamp at a painted spot near Mario.</summary>
    private void Action()
    {
        switch (Type)
        {
            case SmsPollutionType.Fire:
            case SmsPollutionType.FireRegrow:
                FireEffect();
                break;
            case SmsPollutionType.Electric:
                ElectricEffect();
                break;
            default:
                break; // glassWall(): a counter and nothing else
        }
        if (!GetPollutedPos(SmsParams.Pollution.SpreadArea, out Vector3 at))
            return;
        if (Type != SmsPollutionType.Fire && Type != SmsPollutionType.FireRegrow)
        {
            if (_effectTimer >= Math.Max(1, BubbleEveryTicks))
            {
                if (Bubbles && _bubbleMmi != null)
                    EmitBubble(at);
                _effectTimer = 0;
            }
            _effectTimer += 1;
        }
        if (CanSpread)
            Spread();
    }

    // ---------------------------------------------------------------------------------------------------- fire and electric
    /// <summary>fire() (PollutionAction.cpp 112): a painted spot near Mario (getPollutedPosNear(mFireArea 1600)) advances the timer; past
    /// mFireEffectWaitTime (20) a flame there: MS_NEWFIRE_B and _A at x1.5 with SE_EF_FIRE in the game.</summary>
    private void FireEffect()
    {
        if (!GetPollutedPosNear(SmsParams.Pollution.FireArea, out Vector3 at))
            return;
        _fireTimer += 1;
        if (_fireTimer > FlameEveryTicks)
        {
            _flames?.Spawn(at + Vector3.Up * 0.01f, 0.5f * FlameSize, 0.9f * FlameSize, FlameLife);
            _fireTimer = 0;
        }
    }

    /// <summary>electric() (79): a painted spot near Mario (getPollutedPosNear(mThunderArea 1000)) advances the timer; past 15 a spark there:
    /// MS_THUNDER_S with SE_EF_ELEC in the game.</summary>
    private void ElectricEffect()
    {
        if (!GetPollutedPosNear(SmsParams.Pollution.ThunderArea, out Vector3 at))
            return;
        _sparkTimer += 1;
        if (_sparkTimer >= Math.Max(1, SparkEveryTicks))
        {
            _sparks?.Spawn(at + Vector3.Up * 0.01f, SparkSize * 0.25f, SparkSize, SparkLife); // the bolt stands on the paint
            _sparkTimer = 0;
        }
    }

    /// <summary>A pool of billboard quads in world space (a MultiMesh under the layer): position, size and age per instance; the shader reads
    /// the instance's scale as its size and INSTANCE_CUSTOM.x as its age.</summary>
    private sealed class FxPool
    {
        private readonly MultiMesh _mm;
        private readonly Vector3[] _pos;
        private readonly float[] _age, _life, _w, _h;
        private readonly float _rise;
        private int _next;
        public int Emitted { get; private set; }

        public FxPool(Node3D parent, string name, Shader shader, int n, float rise)
        {
            _rise = rise;
            _pos = new Vector3[n];
            _age = new float[n];
            _life = new float[n];
            _w = new float[n];
            _h = new float[n];
            Array.Fill(_age, -1f);
            _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = new QuadMesh { Size = Vector2.One }, InstanceCount = n };
            for (int i = 0; i < n; i++)
                _mm.SetInstanceTransform(i, ZeroXf);
            var mmi = new MultiMeshInstance3D { Name = name, Multimesh = _mm, MaterialOverride = new ShaderMaterial { Shader = shader }, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            parent.AddChild(mmi);
            mmi.GlobalTransform = Transform3D.Identity;
        }

        public int Active { get { int c = 0; foreach (float a in _age) if (a >= 0f) c++; return c; } }

        public void Spawn(Vector3 pos, float w, float h, float life)
        {
            int i = _next;
            _next = (_next + 1) % _pos.Length;
            _pos[i] = pos;
            _w[i] = w;
            _h[i] = h;
            _life[i] = MathF.Max(0.05f, life);
            _age[i] = 0f;
            Emitted++;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _pos.Length; i++)
            {
                if (_age[i] < 0f)
                    continue;
                _age[i] += dt;
                float t = _age[i] / _life[i];
                if (t >= 1f)
                {
                    _age[i] = -1f;
                    _mm.SetInstanceTransform(i, ZeroXf);
                    continue;
                }
                _mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(_w[i], _h[i], 1f)), _pos[i] + Vector3.Up * (_rise * t)));
                _mm.SetInstanceCustomData(i, new Color(t, 0f, 0f, 0f));
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------- bubbles
    private void SetupBubbles()
    {
        int n = Math.Max(1, BubbleMax);
        _bubblePos = new Vector3[n];
        _bubbleAge = new float[n];
        _bubbleScale = new float[n];
        Array.Fill(_bubbleAge, -1f);
        _bubbleMm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 7 },
            InstanceCount = n,
        };
        for (int i = 0; i < n; i++)
            _bubbleMm.SetInstanceTransform(i, ZeroXf);
        Texture2D goop = GoopTexture ?? DefaultGoopTexture(Type);
        Variant tiles = _ownMaterial && _mat != null ? _mat.GetShaderParameter("albedo_tiles_per_metre") : 0.07f;
        var mat = new ShaderMaterial { Shader = BubbleShader ?? GD.Load<Shader>("res://assets/Sms/Fx/GoopBubble.gdshader") };
        if (goop != null)
            mat.SetShaderParameter("goop_albedo", goop);
        mat.SetShaderParameter("albedo_tiles_per_metre", tiles);
        _bubbleMmi = new MultiMeshInstance3D { Name = "GoopBubbles", Multimesh = _bubbleMm, MaterialOverride = mat, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_bubbleMmi);
        _bubbleMmi.GlobalTransform = Transform3D.Identity; // the instances are placed in world space
        if (!BubblePop)
            return;
        // the pop: a ripple ring drawn by GoopRing.gdshader (a procedural band in the goop's own colour: no texture quad to show) and up to
        // four goop droplets a bubble
        _ringAge = new float[n];
        _ringPos = new Vector3[n];
        _ringSize = new float[n];
        Array.Fill(_ringAge, -1f);
        _ringMm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = new QuadMesh { Size = Vector2.One }, InstanceCount = n };
        var ringMat = new ShaderMaterial { Shader = RingShader ?? GD.Load<Shader>("res://assets/Sms/Fx/GoopRing.gdshader") };
        if (goop != null)
            ringMat.SetShaderParameter("goop_albedo", goop);
        ringMat.SetShaderParameter("albedo_tiles_per_metre", tiles);
        ringMat.SetShaderParameter("opacity", RingOpacity);
        ringMat.SetShaderParameter("brighten", RingBrighten);
        for (int i = 0; i < n; i++)
            _ringMm.SetInstanceTransform(i, ZeroXf);
        _ringMmi = new MultiMeshInstance3D { Name = "GoopRings", Multimesh = _ringMm, MaterialOverride = ringMat, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ringMmi);
        _ringMmi.GlobalTransform = Transform3D.Identity;
        int nd = n * 4;
        _dropPos = new Vector3[nd];
        _dropVel = new Vector3[nd];
        _dropAge = new float[nd];
        _dropFloor = new float[nd];
        Array.Fill(_dropAge, -1f);
        _dropMm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 4 }, InstanceCount = nd };
        for (int i = 0; i < nd; i++)
            _dropMm.SetInstanceTransform(i, ZeroXf);
        _dropMmi = new MultiMeshInstance3D { Name = "GoopDrops", Multimesh = _dropMm, MaterialOverride = mat, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_dropMmi);
        _dropMmi.GlobalTransform = Transform3D.Identity;
    }

    /// <summary>A bubble forms at a painted spot: the dome sits on the layer's surface there.</summary>
    private void EmitBubble(Vector3 at)
    {
        float y = SurfaceYAt(at);
        if (float.IsNaN(y))
            return;
        int i = _bubbleNext;
        _bubbleNext = (_bubbleNext + 1) % _bubblePos.Length;
        _bubblePos[i] = new Vector3(at.X, y + 0.004f, at.Z);
        _bubbleAge[i] = 0f;
        _bubbleScale[i] = 0.7f + 0.6f * (float)Rng.NextDouble();
        BubblesEmitted++;
    }

    /// <summary>The pop: the ring starts at the dome's width and spreads to three times it while fading; three or four droplets go up and out
    /// and fall back onto the paint.</summary>
    private void SpawnPop(Vector3 at, float radius)
    {
        if (_ringMm == null)
            return;
        int r = _ringNext;
        _ringNext = (_ringNext + 1) % _ringAge.Length;
        _ringAge[r] = 0f;
        _ringPos[r] = at + Vector3.Up * 0.01f;
        _ringSize[r] = radius * 2f;
        int count = 3 + (Rng.NextDouble() < 0.5 ? 1 : 0);
        for (int k = 0; k < count; k++)
        {
            int i = _dropNext;
            _dropNext = (_dropNext + 1) % _dropAge.Length;
            float ang = (float)(Rng.NextDouble() * Math.PI * 2.0);
            float side = 0.6f + 0.9f * (float)Rng.NextDouble();
            _dropPos[i] = at + Vector3.Up * (radius * 0.6f);
            _dropVel[i] = new Vector3(MathF.Cos(ang) * side, 1.6f + 1.2f * (float)Rng.NextDouble(), MathF.Sin(ang) * side);
            _dropAge[i] = 0f;
            _dropFloor[i] = at.Y;
        }
    }

    private void TickPops(double delta)
    {
        if (_ringMm == null)
            return;
        float dt = (float)delta;
        const float ringLife = 0.55f;
        var flat = Basis.FromEuler(new Vector3(-Mathf.Pi / 2f, 0f, 0f)); // the quad faces +Z: lay it on the paint
        for (int i = 0; i < _ringAge.Length; i++)
        {
            if (_ringAge[i] < 0f)
                continue;
            _ringAge[i] += dt;
            float t = _ringAge[i] / ringLife;
            if (t >= 1f)
            {
                _ringAge[i] = -1f;
                _ringMm.SetInstanceTransform(i, ZeroXf);
                continue;
            }
            float size = _ringSize[i] * (1f + 2f * t);
            _ringMm.SetInstanceTransform(i, new Transform3D(flat.Scaled(new Vector3(size, size, size)), _ringPos[i]));
            _ringMm.SetInstanceCustomData(i, new Color(t, 0f, 0f, 0f)); // .x = the ring's age 0..1 (GoopRing.gdshader fades on it)
        }
        for (int i = 0; i < _dropAge.Length; i++)
        {
            if (_dropAge[i] < 0f)
                continue;
            _dropAge[i] += dt;
            _dropVel[i] += Vector3.Down * (9.8f * 1.6f) * dt;
            _dropPos[i] += _dropVel[i] * dt;
            if (_dropAge[i] > 1.2f || (_dropVel[i].Y < 0f && _dropPos[i].Y <= _dropFloor[i]))
            {
                _dropAge[i] = -1f;
                _dropMm.SetInstanceTransform(i, ZeroXf);
                continue;
            }
            float d = 0.07f + 0.03f * MathF.Min(1f, _dropAge[i] * 4f);
            _dropMm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(d, d, d)), _dropPos[i]));
            _dropMm.SetInstanceCustomData(i, new Color(0f, 0f, 0f, 0f));
        }
    }

    /// <summary>The dome swells slowly over the first 70 % of its life, quivers at full size to 85 %, then pops: a last swell while it fades to
    /// transparent, the ring and droplets starting with it (SpawnPop); the slot frees at 100 % so a popped spot stays quiet a moment.</summary>
    private void TickBubbles(double delta)
    {
        float life = MathF.Max(0.1f, BubbleLife);
        for (int i = 0; i < _bubblePos.Length; i++)
        {
            if (_bubbleAge[i] < 0f)
                continue;
            float before = _bubbleAge[i] / life;
            _bubbleAge[i] += (float)delta;
            float t = _bubbleAge[i] / life;
            if (t >= 1f)
            {
                _bubbleAge[i] = -1f;
                _bubbleMm.SetInstanceTransform(i, ZeroXf);
                continue;
            }
            float radius = BubbleSize * _bubbleScale[i];
            if (t >= 0.85f)
            {
                if (before < 0.85f && BubblePop)
                    SpawnPop(_bubblePos[i], radius);
                float k = (t - 0.85f) / 0.15f;
                float dp = radius * (1f + 0.4f * k) * 2f;
                _bubbleMm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(dp, dp * 0.75f * (1f - 0.5f * k), dp)), _bubblePos[i]));
                _bubbleMm.SetInstanceCustomData(i, new Color(1f, k, 0f, 0f)); // .y = the fade (GoopBubble.gdshader: ALPHA = 1 - y)
                continue;
            }
            float swell = t < 0.7f ? Mathf.SmoothStep(0f, 1f, t / 0.7f) : 1f + 0.06f * MathF.Sin(_bubbleAge[i] * 28f);
            float d = radius * swell * 2f; // the mesh is a unit sphere (radius 0.5): scale = diameter
            _bubbleMm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(d, d * 0.75f, d)), _bubblePos[i]));
            _bubbleMm.SetInstanceCustomData(i, new Color(t / 0.85f, 0f, 0f, 0f));
        }
    }

    // -------------------------------------------------------------------------------------------------------------- spread
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
    /// no higher than Mario (else the whole probe fails; compared in depth units, since a port layer sits a hair above the floor his feet
    /// are on), its paint non-zero. (The decomp reads the z texel with getTexPosS -- the x mapping -- a game bug; the port maps z by z.)</summary>
    private bool GetPollutedPosNear(float rangeUnits, out Vector3 pos)
    {
        float range = SmsUnits.UnitsToMetres(rangeUnits);
        Vector3 mario = SmsPollutionMap.MarioPos;
        int marioDepth = DepthOf((_toLocal * mario).Y);
        for (int i = 0; i < 5; i++)
        {
            float x = ((float)Rng.NextDouble() - 0.5f) * (SmsParams.Pollution.AreaMinRate + (float)Rng.NextDouble());
            float z = ((float)Rng.NextDouble() - 0.5f) * (SmsParams.Pollution.AreaMinRate + (float)Rng.NextDouble());
            pos = new Vector3(x * range + mario.X, mario.Y, z * range + mario.Z);
            Vector3 l = _toLocal * pos;
            if (!InAreaLocal(l.X, l.Z))
                continue;
            int s = TexS(l.X), t = TexT(l.Z);
            if (!InGrid(s, t))
                continue;
            int idx = t * Width + s;
            if (_height[idx] == 0xFF)
                continue;
            if (_height[idx] > marioDepth)
                return false;
            pos = _toWorld * new Vector3(l.X, HeightMetres(_height[idx]), l.Z);
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

    /// <summary>getPollutionType: the first layer whose rectangle holds the point (its height is not asked).</summary>
    public static SmsPollutionType GetPollutionType(Vector3 p)
    {
        foreach (var l in Layers)
            if (l.IsInArea(p))
                return l.Type;
        return SmsPollutionType.None;
    }

    public static SmsPollutionLayer LayerAt(Vector3 p)
    {
        foreach (var l in Layers)
            if (l.IsInArea(p))
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
