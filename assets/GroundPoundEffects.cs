using Godot;

/// <summary>
/// SMS-style ground-pound streak effect: vertical blue/white/red streaks that emit
/// around Mario in a tight column and trail upward while he plummets.
///
/// Usage: attach this script to a Node3D child of Mario (e.g. "GroundPoundEffects").
/// Mario.cs calls <see cref="SetActive"/> to enable/disable when entering or leaving
/// the ground-pound falling state.
/// </summary>
public partial class GroundPoundEffects : Node3D
{
    [Export] public float RingRadius = 0.35f;
    [Export] public float RingHeight = 0.05f;
    [Export] public float StreakSpeed = 0f;          // 0 = streaks stay put; Mario falls away from them
    [Export] public float StreakLifetime = 0.10f;     // short = trail is only ~1-2 Mario heights long given fall speed
    [Export] public int AmountPerColor = 32;
    [Export] public Vector2 StreakSize = new Vector2(0.08f, 0.55f);
    [Export(PropertyHint.Range, "0,1")] public float MaxAlpha = 0.35f;

    /// <summary>Multiplier for the white emitter's particle count. Lower = less white in the mix.</summary>
    [Export(PropertyHint.Range, "0,1")] public float WhiteAmountMul = 0.45f;

    private CpuParticles3D _white, _blue, _red;
    private static Texture2D _softStreakTex;

    public override void _Ready()
    {
        if (_softStreakTex == null) _softStreakTex = BuildSoftStreakTexture();

        int whiteAmt = Mathf.Max(0, (int)(AmountPerColor * WhiteAmountMul));
        if (whiteAmt > 0)
        {
            _white = MakeEmitter(Colors.White, whiteAmt);
            AddChild(_white);
        }
        _blue = MakeEmitter(new Color(0.35f, 0.70f, 1.00f), AmountPerColor);
        _red = MakeEmitter(new Color(1.00f, 0.25f, 0.25f), AmountPerColor);

        AddChild(_blue);
        AddChild(_red);

        SetActive(false);
        GD.Print($"[GroundPoundEffects] Ready — 3 emitters built under '{GetParent()?.Name}/{Name}'.");
    }

    private bool _wasActive = false;
    public void SetActive(bool on)
    {
        if (_blue == null) return; // not initialized
        if (on != _wasActive)
        {
            GD.Print($"[GroundPoundEffects] SetActive({on}) at pos={GlobalPosition}");
            _wasActive = on;
        }
        if (_white != null) _white.Emitting = on;
        _blue.Emitting = on;
        _red.Emitting = on;
    }

    private CpuParticles3D MakeEmitter(Color color, int amount)
    {
        var p = new CpuParticles3D
        {
            Amount = amount,
            Lifetime = StreakLifetime,
            OneShot = false,
            Explosiveness = 0f,
            Randomness = 0.6f,

            EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring,
            EmissionRingRadius = RingRadius,
            EmissionRingInnerRadius = 0f, // solid disk, not a hollow ring (no funnel look)
            EmissionRingHeight = RingHeight,
            EmissionRingAxis = Vector3.Up,

            Direction = Vector3.Up,
            Spread = 0f,                  // no cone — straight column
            Gravity = Vector3.Zero,
            InitialVelocityMin = StreakSpeed,
            InitialVelocityMax = StreakSpeed,

            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.1f,

            Color = color,
            LocalCoords = false,
        };

        // Quad mesh, tall and narrow, so it reads as a vertical streak.
        var quad = new QuadMesh
        {
            Size = StreakSize,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = color,
            AlbedoTexture = _softStreakTex,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
        };
        quad.Material = mat;
        p.Mesh = quad;

        // Alpha fade-out over lifetime (MaxAlpha → 0). CpuParticles3D.ColorRamp is a Gradient.
        var ramp = new Gradient();
        ramp.SetOffsets(new[] { 0f, 1f });
        ramp.SetColors(new[]
        {
            new Color(color.R, color.G, color.B, MaxAlpha),
            new Color(color.R, color.G, color.B, 0f),
        });
        p.ColorRamp = ramp;

        return p;
    }

    /// <summary>
    /// Generate a soft elliptical streak texture in code. White core, alpha falling
    /// off smoothly to the edges so each particle reads as a fluffy streak instead
    /// of a hard-edged rectangle.
    /// </summary>
    private static Texture2D BuildSoftStreakTexture()
    {
        const int w = 32;
        const int h = 128;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float rx = w * 0.45f;
        float ry = h * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d2 = dx * dx + dy * dy;
                // smooth falloff: 1 at center, 0 at the boundary; flatten the core a bit
                float a = Mathf.Clamp(1f - d2, 0f, 1f);
                a = a * a; // soften further so edges fade gently
                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}
