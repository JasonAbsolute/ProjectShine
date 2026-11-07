using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 167: THE ICE about a frozen Mario -- the Ghost Breath's freeze made visible (KINGBOO_REMATCH_gdoc.txt: "a hit freezes Mario for one second";
/// the freeze itself is Mario.SmsFreezeFor: his move skipped, his pose held, the frost fog on his body). A six-sided BLOCK of ice (<see cref="IceBlock.gdshader"/>:
/// translucent ice blue, a fresnel rim, a frost mottle, a highlight sliding down it) snapped about his body -- BlockWidth across, BlockHeight tall from
/// his feet -- popping in over PopSeconds from small and white-hot with a burst of frost about him and Mario's fright (FreezeSe), ice GLINTS drifting off
/// it while it holds, following him (he does not move; a Mario frozen in the air hangs there, and the ice with him). While Mario.SmsFrosted holds. When he
/// THAWS -- or another hit breaks him out of it -- the block SHATTERS: gone in a frame, Shards of ice flung out (the port's square spark, ice blue,
/// tumbling, under gravity, fading) with the glass break (ShatterSe: the casino floor's; the port has no ice). One per Mario at a time (<see cref="Freeze"/>
/// returns the one already on him).
/// </summary>
public partial class SmsIceBlock : Node3D
{
    /// <summary>The block's size (metres across, tall from his feet), its pop-in, how long the shards last.</summary>
    [Export] public float BlockWidth = 1.9f, BlockHeight = 2.05f, PopSeconds = 0.22f, ShardSeconds = 0.9f;   // 178: the pop 0.1 -> 0.22, the ice CLIMBING him from the feet
    /// <summary>178 (Jaysonn: "it looks like mario doesn't get hit but still is frozen"): THE IMPACT as the cold gets him -- a flash of white-blue at his body
    /// (FlashSize m, FlashSeconds), a SPLASH of frost puffs flung on along the stream's way and up (SplashPuffs), a RING of mist spreading from his feet along
    /// the floor (RingPuffs), ice crystals flung out (HitShards) -- and the block grows from his FEET up over PopSeconds instead of popping about his middle.</summary>
    [Export(PropertyHint.Range, "0,80,1")] public int SplashPuffs = 36, RingPuffs = 24, HitShards = 22;
    [Export] public float FlashSize = 3.2f, FlashSeconds = 0.28f;
    [Export] public Color FlashColor = new Color(0.85f, 0.95f, 1f);
    /// <summary>170: the ice is a jagged CHUNK (Jaysonn: "a chunk of ice, not just a smooth block"): how far its facets are pushed in and out (0 = a smooth
    /// egg), how many spikes stick out of it and how far, the facets about and up it.</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float Jag = 0.22f;
    [Export(PropertyHint.Range, "0,12,1")] public int Spikes = 5;
    [Export] public float SpikeOut = 0.45f;
    [Export(PropertyHint.Range, "5,16,1")] public int FacetsAround = 9, FacetsUp = 6;
    /// <summary>A glint's and a shard's size (metres; the quads -- the process material's scale range did not take here).</summary>
    [Export] public float GlintSize = 0.18f, ShardSize = 0.14f;
    [Export] public Color IceColor = new Color(0.7f, 0.88f, 1f, 0.15f), GlintColor = new Color(0.75f, 0.95f, 1f);
    [Export(PropertyHint.Range, "0,64,1")] public int Glints = 20;
    [Export(PropertyHint.Range, "0,120,1")] public int Shards = 36;
    [Export(PropertyHint.Range, "0,40,1")] public int FrostPuffs = 14;
    [Export] public string FreezeSe = "MSD_SE_MV30_FRIGHT_01", ShatterSe = "MSD_SE_OBJ_GLASS_BREAK";
    [Export] public bool DebugPrint;

    public Mario Target { get; private set; }
    public bool Shattered { get; private set; }
    public float Age => _age;
    public float ScaleShown => _mesh != null ? _mesh.Scale.Y : 0f;
    public static readonly List<SmsIceBlock> All = new();
    public static int Made, Shatters;

    private MeshInstance3D _mesh; private ShaderMaterial _mat; private GpuParticles3D _glints; private SmsActorSound _sound; private float _age, _popT;

    /// <summary>The ice about a Mario just frozen (the one already there when he is): the block, the pop, the fright.</summary>
    public static SmsIceBlock Freeze(Mario mario, Node parent, bool debug = false)
    {
        if (mario == null || !GodotObject.IsInstanceValid(mario)) return null;
        foreach (var b in All) if (b.Target == mario && !b.Shattered) return b;
        var block = new SmsIceBlock { Name = $"IceBlock{Made + 1}", DebugPrint = debug, Target = mario, TopLevel = true };
        (parent ?? mario.GetParent()).AddChild(block);
        return block;
    }

    public override void _Ready()
    {
        All.Add(this); Made++;
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/IceBlock.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/IceBlock.gdshader") : null;
        if (shader != null)
        {
            _mat = new ShaderMaterial { Shader = shader, RenderPriority = 2 };
            _mat.SetShaderParameter("ice_color", IceColor); _mat.SetShaderParameter("rim_color", new Color(0.92f, 0.98f, 1f)); _mat.SetShaderParameter("flash", 1f);
            var frost = Tex("stardust1"); if (frost != null) _mat.SetShaderParameter("frost_tex", frost);
        }
        _mesh = new MeshInstance3D
        {
            Name = "Block", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = ChunkMesh(Made), MaterialOverride = _mat, Position = Vector3.Up * (BlockHeight * 0.5f), Rotation = new Vector3(0f, Mathf.DegToRad(30f + 47f * (Made % 7)), 0f), Scale = Vector3.One * 0.25f
        };
        AddChild(_mesh);
        if (Glints > 0) MakeGlints();
        Follow();
        if (FrostPuffs > 0) FrostBurst();
        _sound = SmsActorSound.Attach(this);
        if (_sound != null && !string.IsNullOrEmpty(FreezeSe)) _sound.Play(FreezeSe);
        if (DebugPrint) GD.Print($"[SMS ice] {Name}: the ice snaps about Mario at {GlobalPosition} ({(Target != null && Target.IsOnFloor() ? "on the floor" : "in the air")})");
    }
    public override void _ExitTree() { All.Remove(this); }

    private void Follow()
    {
        if (Target == null || !IsInstanceValid(Target)) return;
        GlobalPosition = Target.GlobalPosition;
        if (_glints != null) _glints.GlobalPosition = GlobalPosition + Vector3.Up * (BlockHeight * 0.5f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        if (Shattered) return;
        Follow();
        _popT += dt;
        float k = Mathf.Clamp(_popT / Mathf.Max(0.01f, PopSeconds), 0f, 1f), e = 1f - (1f - k) * (1f - k);
        if (_mesh != null)   // 178: the ice climbs him -- tall from the feet up (its base stays at the floor), the width catching up behind
        {
            float sy = Mathf.Lerp(0.12f, 1f, e), sxz = Mathf.Lerp(0.55f, 1f, Mathf.Clamp(k * 1.25f, 0f, 1f));
            _mesh.Scale = new Vector3(sxz, sy, sxz); _mesh.Position = Vector3.Up * (BlockHeight * 0.5f * sy);
        }
        _mat?.SetShaderParameter("flash", Mathf.Max(0f, 1f - _popT / (PopSeconds * 1.8f)));
        if (_flash != null && IsInstanceValid(_flash))   // 178: the impact flash -- bright at once, gone over FlashSeconds, growing a little
        {
            float f = Mathf.Clamp(_popT / Mathf.Max(0.01f, FlashSeconds), 0f, 1f);
            _flash.Scale = Vector3.One * FlashSize * Mathf.Lerp(0.6f, 1.15f, f);
            if (_flashMat != null) _flashMat.AlbedoColor = new Color(FlashColor.R, FlashColor.G, FlashColor.B, (1f - f) * (1f - f) * 0.95f);
            _flash.GlobalPosition = GlobalPosition + Vector3.Up * (BlockHeight * 0.5f);
            if (f >= 1f) { _flash.QueueFree(); _flash = null; }
        }
        if (_age > 0.05f && (Target == null || !IsInstanceValid(Target) || !Target.SmsFrosted)) Shatter();
    }

    /// <summary>The block gone, the shards out, the break.</summary>
    public void Shatter()
    {
        if (Shattered) return;
        Shattered = true; Shatters++; All.Remove(this);
        if (_mesh != null) _mesh.Visible = false;
        if (_glints != null) { _glints.Emitting = false; _glints.Visible = false; }
        if (Shards > 0) ShardBurst();
        if (FrostPuffs > 0) FrostBurst(1.6f);   // the poof as it breaks
        if (_sound != null && !string.IsNullOrEmpty(ShatterSe)) _sound.Play(ShatterSe);
        if (DebugPrint) GD.Print($"[SMS ice] {Name}: SHATTERS after {_age:F2} s ({(Target != null && IsInstanceValid(Target) ? Target.stateOfMario.ToString() : "?")})");
        GetTree().CreateTimer(ShardSeconds + 0.3f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }

    private void MakeGlints()
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(BlockWidth * 0.55f, BlockHeight * 0.5f, BlockWidth * 0.55f),
            Direction = new Vector3(0f, 1f, 0f), Spread = 40f, InitialVelocityMin = 0.3f, InitialVelocityMax = 0.9f, Gravity = new Vector3(0f, 0.6f, 0f), ScaleMin = 0.7f, ScaleMax = 1.3f,
            AngularVelocityMin = -200f, AngularVelocityMax = 200f, Color = new Color(GlintColor.R, GlintColor.G, GlintColor.B, 1f),
            ColorRamp = Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f))
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = Tex("star4s_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        _glints = new GpuParticles3D { Name = "IceGlints", TopLevel = true, Amount = Glints, Lifetime = 0.7, Emitting = true, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * GlintSize, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-6f, -6f, -6f), new Vector3(12f, 12f, 12f)) };
        AddChild(_glints);
    }
    private MeshInstance3D _flash; private StandardMaterial3D _flashMat;
    /// <summary>178: the cold GETS him -- called by the breath with the stream's direction: a flash at his body, the frost SPLASHING on past him along the stream
    /// and up (the breath breaking on him), a ring of mist spreading along the floor from his feet, ice crystals flung out. Once.</summary>
    public void HitFrom(Vector3 streamDir)
    {
        if (_hitShown) return; _hitShown = true;
        Vector3 d = new Vector3(streamDir.X, 0f, streamDir.Z); d = d.LengthSquared() > 1e-6f ? d.Normalized() : Vector3.Forward;
        Vector3 body = GlobalPosition + Vector3.Up * (BlockHeight * 0.5f);
        if (FlashSize > 0f)
        {
            _flashMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = true, AlbedoTexture = Tex("radgrad_raw"), AlbedoColor = new Color(FlashColor.R, FlashColor.G, FlashColor.B, 0.95f), RenderPriority = 4 };
            _flash = new MeshInstance3D { Name = "FrostFlash", TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One, Material = _flashMat }, Scale = Vector3.One * FlashSize * 0.6f };
            AddChild(_flash); _flash.GlobalPosition = body;
        }
        if (SplashPuffs > 0) Burst("FrostSplash", SplashPuffs, body, d * 0.75f + Vector3.Up * 0.45f, 35f, 6f, 11f, 0.9f, 2.4f, new Color(0.86f, 0.95f, 1f), 0.6f, 0.75, 3f, -1.5f);
        if (RingPuffs > 0) Burst("FrostRing", RingPuffs, GlobalPosition + Vector3.Up * 0.25f, Vector3.Up * 0.05f, 180f, 4f, 7f, 0.7f, 2.2f, new Color(0.9f, 0.97f, 1f), 0.5f, 0.7, 4f, 0.2f, flat: true);
        if (HitShards > 0) ShardsFrom("HitShards", HitShards, body, d * 0.5f + Vector3.Up, 90f, 6f, 13f);
        if (DebugPrint) GD.Print($"[SMS ice] {Name}: the HIT shows -- the flash, {SplashPuffs} splash puffs on along {d}, {RingPuffs} ring puffs, {HitShards} crystals");
    }
    private bool _hitShown;
    public bool HitShown => _hitShown;
    public bool FlashUp => _flash != null && IsInstanceValid(_flash);
    /// <summary>A one-shot burst of the port's smoke ball, mix-blended: count, from a point, a direction and spread, speeds, sizes, colour, alpha, life, damping, gravity.</summary>
    private void Burst(string name, int count, Vector3 at, Vector3 dir, float spread, float v0, float v1, float s0, float s1, Color col, float alpha, double life, float damp, float gravity, bool flat = false)
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = flat ? 0.6f : 0.4f,
            Direction = dir.Normalized(), Spread = spread, Flatness = flat ? 1f : 0f, InitialVelocityMin = v0, InitialVelocityMax = v1, Gravity = new Vector3(0f, gravity, 0f), DampingMin = damp, DampingMax = damp * 1.6f,
            ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = Ramp(s0, s1), AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -70f, AngularVelocityMax = 70f,
            Color = new Color(col.R, col.G, col.B, 1f), ColorRamp = Fade(new Color(1f, 1f, 1f, alpha), new Color(1f, 1f, 1f, alpha), new Color(1f, 1f, 1f, alpha * 0.55f), new Color(1f, 1f, 1f, 0f))
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = Tex("kemuball3_raw"), DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = name, TopLevel = true, Amount = count, Lifetime = life, OneShot = true, Explosiveness = 0.9f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-12f, -4f, -12f), new Vector3(24f, 12f, 24f)) };
        AddChild(p); p.GlobalPosition = at;
        p.Finished += p.QueueFree; p.Emitting = true;
    }
    /// <summary>A one-shot of the ice shards (the port's square spark, ice blue, tumbling, under gravity) from a point, a direction and spread, speeds.</summary>
    private void ShardsFrom(string name, int count, Vector3 at, Vector3 dir, float spread, float v0, float v1)
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.5f,
            Direction = dir.Normalized(), Spread = spread, InitialVelocityMin = v0, InitialVelocityMax = v1, Gravity = new Vector3(0f, -14f, 0f), DampingMin = 0.5f, DampingMax = 1.5f,
            ScaleMin = 0.6f, ScaleMax = 1.4f, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -540f, AngularVelocityMax = 540f,
            Color = new Color(0.8f, 0.93f, 1f, 1f), ColorRamp = Fade(new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.8f), new Color(1f, 1f, 1f, 0f))
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = Tex("shikaku") ?? Tex("glow1_spark"), DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = name, TopLevel = true, Amount = count, Lifetime = ShardSeconds, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * ShardSize, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-12f, -4f, -12f), new Vector3(24f, 12f, 24f)) };
        AddChild(p); p.GlobalPosition = at;
        p.Finished += p.QueueFree; p.Emitting = true;
    }
    /// <summary>The frost about him as the ice snaps on: a one-shot ring of pale puffs.</summary>
    private void FrostBurst(float size = 1f)
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = BlockWidth * 0.5f,
            Direction = new Vector3(0f, 0.3f, 0f), Spread = 180f, InitialVelocityMin = 2f * size, InitialVelocityMax = 4.5f * size, Gravity = new Vector3(0f, -1f, 0f), DampingMin = 3f, DampingMax = 5f,
            ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = Ramp(0.6f * size, 1.8f * size), AngularVelocityMin = -60f, AngularVelocityMax = 60f,
            Color = new Color(0.82f, 0.93f, 1f, 1f), ColorRamp = Fade(new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.3f), new Color(1f, 1f, 1f, 0f))
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = Tex("kemuball3_raw"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = "FrostPuffs", TopLevel = true, Amount = FrostPuffs, Lifetime = 0.6, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-8f, -8f, -8f), new Vector3(16f, 16f, 16f)) };
        AddChild(p);
        p.GlobalPosition = GlobalPosition + Vector3.Up * (BlockHeight * 0.5f);
        p.Finished += p.QueueFree; p.Emitting = true;
    }
    /// <summary>The shards: the port's square spark, ice blue, flung out of the block's volume, tumbling and falling.</summary>
    private void ShardBurst()
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(BlockWidth * 0.45f, BlockHeight * 0.45f, BlockWidth * 0.45f),
            Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 5f, InitialVelocityMax = 12f, Gravity = new Vector3(0f, -14f, 0f), DampingMin = 0.5f, DampingMax = 1.5f,
            ScaleMin = 0.6f, ScaleMax = 1.4f, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -540f, AngularVelocityMax = 540f,
            Color = new Color(0.8f, 0.93f, 1f, 1f), ColorRamp = Fade(new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.8f), new Color(1f, 1f, 1f, 0f))
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = Tex("shikaku"), CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = "IceShards", TopLevel = true, Amount = Shards, Lifetime = ShardSeconds, OneShot = true, Explosiveness = 1f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * ShardSize, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-10f, -10f, -10f), new Vector3(20f, 20f, 20f)) };
        AddChild(p);
        p.GlobalPosition = GlobalPosition + Vector3.Up * (BlockHeight * 0.5f);
        p.Emitting = true;
    }
    /// <summary>170: the chunk -- a sphere of FacetsAround x FacetsUp facets stretched to the block's size, every vertex pushed in or out by up to Jag (a
    /// seeded dice), Spikes of them pushed out SpikeOut further, built unindexed with each facet's own normal so the rim catches every face (flat shading).</summary>
    private ArrayMesh ChunkMesh(int seed)
    {
        var rng = new Random(170 + seed * 31);
        int na = Mathf.Max(5, FacetsAround), nu = Mathf.Max(3, FacetsUp);
        var pts = new Vector3[nu + 1, na];
        var spikes = new HashSet<(int, int)>(); for (int i = 0; i < Spikes; i++) spikes.Add((1 + rng.Next(nu - 1), rng.Next(na)));
        for (int j = 0; j <= nu; j++)
        {
            float v = j / (float)nu, lat = Mathf.Lerp(-MathF.PI / 2f, MathF.PI / 2f, v);
            for (int i = 0; i < na; i++)
            {
                float lon = i / (float)na * MathF.Tau;
                float r = 1f + ((float)rng.NextDouble() * 2f - 1f) * Jag + (spikes.Contains((j, i)) ? SpikeOut : 0f);
                if (j == 0 || j == nu) r = 1f + ((float)rng.NextDouble() * 2f - 1f) * Jag * 0.5f;
                pts[j, i] = new Vector3(MathF.Cos(lat) * MathF.Cos(lon) * BlockWidth * 0.5f, MathF.Sin(lat) * BlockHeight * 0.5f, MathF.Cos(lat) * MathF.Sin(lon) * BlockWidth * 0.5f) * r;
            }
        }
        var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 n = (b - a).Cross(c - a); if (n.LengthSquared() < 1e-10f) return; n = n.Normalized();
            st.SetNormal(n); st.SetUV(ua); st.AddVertex(a); st.SetNormal(n); st.SetUV(ub); st.AddVertex(b); st.SetNormal(n); st.SetUV(uc); st.AddVertex(c);
        }
        for (int j = 0; j < nu; j++)
            for (int i = 0; i < na; i++)
            {
                int i2 = (i + 1) % na;
                Vector3 p00 = pts[j, i], p10 = pts[j, i2], p01 = pts[j + 1, i], p11 = pts[j + 1, i2];
                Vector2 u00 = new(i / (float)na, j / (float)nu), u10 = new((i + 1) / (float)na, j / (float)nu), u01 = new(i / (float)na, (j + 1) / (float)nu), u11 = new((i + 1) / (float)na, (j + 1) / (float)nu);
                Tri(p00, p01, p10, u00, u01, u10); Tri(p10, p01, p11, u10, u01, u11);
            }
        return st.Commit();
    }
    private static Texture2D Tex(string name) { string p = $"res://assets/Sms/Fx/tex/{name}.png"; return ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null; }
    private static CurveTexture Ramp(float a, float b)
    {
        var c = new Curve { MinValue = 0f, MaxValue = Mathf.Max(1f, Mathf.Max(a, b) * 1.1f) };
        c.AddPoint(new Vector2(0f, a)); c.AddPoint(new Vector2(1f, b));
        return new CurveTexture { Curve = c };
    }
    private static GradientTexture1D Fade(Color c0, Color c1, Color c2, Color c3)
    {
        var g = new Gradient { Offsets = new float[] { 0f, 0.1f, 0.65f, 1f }, Colors = new Color[] { c0, c1, c2, c3 } };
        return new GradientTexture1D { Gradient = g };
    }
}
