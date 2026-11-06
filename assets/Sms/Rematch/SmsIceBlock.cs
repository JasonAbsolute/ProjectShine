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
    [Export] public float BlockWidth = 1.9f, BlockHeight = 2.05f, PopSeconds = 0.1f, ShardSeconds = 0.9f;
    /// <summary>A glint's and a shard's size (metres; the quads -- the process material's scale range did not take here).</summary>
    [Export] public float GlintSize = 0.18f, ShardSize = 0.14f;
    [Export] public Color IceColor = new Color(0.7f, 0.88f, 1f, 0.22f), GlintColor = new Color(0.75f, 0.95f, 1f);
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
            Mesh = new CylinderMesh { TopRadius = BlockWidth * 0.46f, BottomRadius = BlockWidth * 0.5f, Height = BlockHeight, RadialSegments = 6, Rings = 1, CapTop = true, CapBottom = true },
            MaterialOverride = _mat, Position = Vector3.Up * (BlockHeight * 0.5f), Rotation = new Vector3(0f, Mathf.DegToRad(30f), 0f), Scale = Vector3.One * 0.25f
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
        float k = Mathf.Clamp(_popT / Mathf.Max(0.01f, PopSeconds), 0f, 1f);
        if (_mesh != null) _mesh.Scale = Vector3.One * Mathf.Lerp(0.25f, 1f, 1f - (1f - k) * (1f - k));
        _mat?.SetShaderParameter("flash", Mathf.Max(0f, 1f - _popT / (PopSeconds * 1.8f)));
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
