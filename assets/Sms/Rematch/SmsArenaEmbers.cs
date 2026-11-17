using Godot;
using System.Linq;

/// <summary>
/// SMS pass 210: EMBERS IN THE AIR for the final phase (Jaysonn: "a sparse field of rising embers over the arena's rim and the void, drifting and fading in with the
/// sky, ties the floor to the fire dome"). One GPU particle field, born in a RING round the arena's edge -- from RimIn metres inside the fence to RimOut metres out
/// over the void, between Below metres under the floor and Above over it (the floor's middle, radius and height are read off the Arena at start) -- rising at
/// RiseMin..RiseMax m/s with a little lift, wandering on turbulence (Drift), each living LifeSeconds (give or take 40 %), a soft spark (the fireballs' glow1_spark)
/// Size metres across, bright yellow-orange going dim red and out, drawn additive at Brightness x (over 1: hot enough to bloom under the arena's glow).
/// It fades in WITH THE SKY: every frame the field's alpha is the sky material's final_blend (King Boo's fade to the roof fight's fire); the field starts emitting
/// as the blend leaves 0 (pre-rolled, so the air is already full) and stops when the blend is back at 0; 217: x (1 - the sky's peace_blend) -- the night after the
/// defeat puts them out. ProcessMode Always: the embers keep drifting through the
/// split cutscenes' frozen world, as the sky does. Knobs: Embers (off = none), Count, RimIn / RimOut, Below / Above, RiseMin / RiseMax, Drift, LifeSeconds,
/// Size, EmberBright / EmberDim, Brightness. Readouts for the drives: Built, Centre, Radius, RingOuter / RingInner / RingHeight, BlendRead, AlphaNow, EmittingNow,
/// FirstEmitBlend.
/// </summary>
[GlobalClass]
public partial class SmsArenaEmbers : Node3D
{
    [ExportGroup("Embers in the air (210)")]
    /// <summary>Off = no embers at all.</summary>
    [Export] public bool Embers = true;
    /// <summary>How many are in the air at once.</summary>
    [Export(PropertyHint.Range, "0,2000,10")] public int Count = 450;
    /// <summary>The ring they are born in: from this far inside the arena's edge ...</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float RimIn = 3f;
    /// <summary>... to this far out over the void.</summary>
    [Export(PropertyHint.Range, "0,40,0.5")] public float RimOut = 12f;
    /// <summary>Born between this far under the floor ...</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float Below = 4f;
    /// <summary>... and this far over it.</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float Above = 2f;
    /// <summary>How fast they rise (metres a second), and how much they wander sideways on the way up (0 = straight up).</summary>
    [Export(PropertyHint.Range, "0,5,0.1")] public float RiseMin = 0.6f;
    [Export(PropertyHint.Range, "0,5,0.1")] public float RiseMax = 2f;
    [Export(PropertyHint.Range, "0,3,0.05")] public float Drift = 1f;
    /// <summary>How long one lives (seconds; each gets 60-100 % of it) and how big it is (metres across).</summary>
    [Export(PropertyHint.Range, "1,20,0.5")] public float LifeSeconds = 7f;
    [Export(PropertyHint.Range, "0.02,1,0.01")] public float Size = 0.3f;
    /// <summary>Its colour new-born and its colour going out; Brightness over 1 makes them hot enough to bloom.</summary>
    [Export] public Color EmberBright = new(1f, 0.85f, 0.35f);
    [Export] public Color EmberDim = new(1f, 0.3f, 0.05f);
    [Export(PropertyHint.Range, "0,4,0.1")] public float Brightness = 2f;
    [Export] public bool DebugPrint;

    // ---- readouts
    public bool Built => _p != null;
    public Vector3 Centre { get; private set; }
    public float Radius { get; private set; }
    public float RingOuter { get; private set; }
    public float RingInner { get; private set; }
    public float RingHeight { get; private set; }
    public float BlendRead { get; private set; }
    public float AlphaNow { get; private set; }
    public bool EmittingNow => _p != null && _p.Emitting;
    /// <summary>The sky's blend the first time the field started emitting (-1 = not yet).</summary>
    public float FirstEmitBlend { get; private set; } = -1f;
    public int Ticks { get; private set; }
    public GpuParticles3D Particles => _p;

    private GpuParticles3D _p; private StandardMaterial3D _mat; private ShaderMaterial _sky; private bool _skyLooked;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        ProcessMode = ProcessModeEnum.Always;
        CallDeferred(nameof(Build));                                                                  // the floor measures itself in its own _Ready
    }

    private void Build()
    {
        if (!Embers || Count <= 0) return;
        var floor = GetTree()?.CurrentScene?.FindChildren("*", "", true, false).OfType<SmsArenaFloor>().FirstOrDefault();
        if (floor == null || float.IsNaN(floor.TopY) || floor.Radius <= 0.1f) { GD.PushWarning($"[SMS embers] {GetPath()}: no measured arena floor (SmsArenaFloor) -- no embers"); return; }
        Centre = floor.Centre; Radius = floor.Radius;
        RingOuter = Radius + RimOut; RingInner = Mathf.Max(0f, Radius - RimIn); RingHeight = Below + Above;
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring, EmissionRingAxis = Vector3.Up, EmissionRingRadius = RingOuter, EmissionRingInnerRadius = RingInner, EmissionRingHeight = RingHeight,
            Direction = Vector3.Up, Spread = 20f, InitialVelocityMin = RiseMin, InitialVelocityMax = RiseMax, Gravity = new Vector3(0f, 0.12f, 0f),
            TurbulenceEnabled = Drift > 0.001f, TurbulenceNoiseStrength = 1f, TurbulenceNoiseScale = 7f, TurbulenceNoiseSpeed = new Vector3(0.25f, 0.15f, 0.25f), TurbulenceNoiseSpeedRandom = 0.3f,
            TurbulenceInfluenceMin = 0.03f * Drift, TurbulenceInfluenceMax = 0.09f * Drift, LifetimeRandomness = 0.4,
            ScaleMin = 0.5f, ScaleMax = 1.3f, ScaleCurve = SmsFireballs.Ramp(1f, 0.35f),
            Color = new Color(Brightness, Brightness, Brightness, 1f),
            ColorRamp = SmsFireballs.Fade(new Color(EmberBright.R, EmberBright.G, EmberBright.B, 0f), new Color(EmberBright.R, EmberBright.G, EmberBright.B, 1f), new Color(EmberDim.R, EmberDim.G, EmberDim.B, 0.85f), new Color(EmberDim.R * 0.6f, EmberDim.G * 0.4f, EmberDim.B * 0.3f, 0f))
        };
        _mat = SmsFireballs.AddBillboard(SmsFireballs.Tex("glow1_spark")); _mat.AlbedoColor = new Color(1f, 1f, 1f, 0f);
        float reach = RingOuter + 6f, up = RingHeight + LifeSeconds * RiseMax + 6f;
        _p = new GpuParticles3D
        {
            Name = "EmberField", TopLevel = true, Amount = Count, Lifetime = LifeSeconds, Preprocess = LifeSeconds * 0.9, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true,
            ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * Size, Material = _mat }, ProcessMode = ProcessModeEnum.Always,
            VisibilityAabb = new Aabb(new Vector3(-reach, -Below - 4f, -reach), new Vector3(reach * 2f, up, reach * 2f))
        };
        AddChild(_p);
        _p.GlobalPosition = new Vector3(Centre.X, floor.TopY + (Above - Below) * 0.5f, Centre.Z);
        if (DebugPrint) GD.Print($"[SMS embers] {GetPath()}: {Count} embers in a ring {RingInner:F1}..{RingOuter:F1} m round ({Centre.X:F1}, {Centre.Z:F1}), {RingHeight:F1} m tall from {floor.TopY - Below:F1} (the floor {floor.TopY:F2}, radius {Radius:F1}); rising {RiseMin}-{RiseMax} m/s for {LifeSeconds} s");
    }

    private ShaderMaterial Sky()
    {
        if (_skyLooked) return _sky;
        _skyLooked = true;
        var we = GetTree()?.CurrentScene?.FindChildren("*", "WorldEnvironment", true, false).OfType<WorldEnvironment>().FirstOrDefault();
        _sky = we?.Environment?.Sky?.SkyMaterial as ShaderMaterial;
        return _sky;
    }

    public override void _Process(double delta)
    {
        if (_p == null) return;
        Ticks++;
        var sky = Sky(); float k = 0f, peace = 0f;
        if (sky != null)
        {
            Variant v = sky.GetShaderParameter("final_blend"); if (v.VariantType != Variant.Type.Nil) k = v.AsSingle();
            Variant pv = sky.GetShaderParameter("peace_blend"); if (pv.VariantType != Variant.Type.Nil) peace = Mathf.Clamp(pv.AsSingle(), 0f, 1f);   // 217: the night puts them out
        }
        BlendRead = Mathf.Clamp(k, 0f, 1f);
        AlphaNow = BlendRead * (1f - peace);
        _mat.AlbedoColor = new Color(1f, 1f, 1f, AlphaNow);
        bool want = AlphaNow > 0.001f;
        if (want && !_p.Emitting) { _p.Emitting = true; if (FirstEmitBlend < 0f) FirstEmitBlend = BlendRead; if (DebugPrint) GD.Print($"[SMS embers] {GetPath()}: the field starts (the sky's blend {BlendRead:F3})"); }
        else if (!want && _p.Emitting) _p.Emitting = false;
    }

    public override void _ExitTree() { if (_p != null) _p.Emitting = false; }
}
