using Godot;

/// <summary>
/// Small, quick white dust puffs for ORDINARY landings (every jump, not just ground
/// pound) — a ring of camera-facing puffs (<see cref="PuffCount"/>) that pop up and
/// drift outward from Mario's feet. Reference: SMS landing dust (JumpEffects.mp4) —
/// a brief puff that appears on touchdown and dissipates within roughly a third of
/// a second.
///
/// BillboardMode.Enabled always faces the camera, so these read regardless of view
/// angle (unlike a flat ground decal, which collapses to a sliver when viewed
/// edge-on from a level chase camera).
///
/// Usage: attach to a Node3D child of Mario (e.g. "LandingDustFx"). Mario.cs calls
/// <see cref="Trigger"/> at the moment of a normal landing.
/// </summary>
public partial class LandingDustFx : Node3D
{
    [Export] public float Duration = 0.4f;
    [Export] public Color DustColor = new Color(1f, 1f, 1f);

    /// <summary>Distance above the floor to place the puffs. Bump if they clip through the ground.</summary>
    [Export] public float HeightAboveFloor = 0.03f;

    /// <summary>How many puffs spawn around Mario's feet.</summary>
    [Export(PropertyHint.Range, "1,16")] public int PuffCount = 5;

    // They start nearly together and drift outward as they grow, so the puff visibly
    // "reaches out" instead of just swelling in place.
    [Export] public float BillboardStartSideOffset = 0.07f;
    [Export] public float BillboardEndSideOffset = 0.85f;
    [Export] public float BillboardStartSize = 0.3f;
    [Export] public float BillboardEndSize = 1.5f;
    [Export] public float BillboardRise = 0.45f;
    [Export(PropertyHint.Range, "0,1")] public float BillboardMaxAlpha = 0.85f;

    private MeshInstance3D[] _billboards;
    private StandardMaterial3D[] _billboardMats;
    // Per-puff variance so a ring of puffs doesn't look like a mechanical flower —
    // random angle jitter plus size/rise/timing multipliers, fixed at Build() so
    // each puff is consistent across its own lifetime.
    private float[] _puffAngle;
    private float[] _puffSizeMul;
    private float[] _puffRiseMul;
    private float[] _puffDelay; // fraction of Duration before this puff starts growing
    private float _timer = -1f;
    private static Texture2D _tex;

    public override void _Ready()
    {
        // Don't follow Mario after triggering — stay put at the landing spot.
        TopLevel = true;

        if (_tex == null)
            _tex = BuildPuffTexture();

        Build();
    }

    /// <summary>Fire the puffs at the given feet position, oriented to the floor normal.</summary>
    public void Trigger(Vector3 feetPos, Vector3? upNormal = null)
    {
        Vector3 up = (upNormal ?? Vector3.Up).Normalized();
        GlobalTransform = new Transform3D(BasisFromUp(up), feetPos + up * HeightAboveFloor);
        _timer = 0f;
        foreach (var b in _billboards)
            b.Visible = true;
    }

    public override void _Process(double delta)
    {
        if (_timer < 0f)
            return;

        _timer += (float)delta;
        float t = _timer / Duration;
        if (t >= 1f)
        {
            foreach (var b in _billboards)
                b.Visible = false;
            _timer = -1f;
            return;
        }

        for (int i = 0; i < _billboards.Length; i++)
        {
            // Each puff runs its own eased pop, staggered by _puffDelay so they
            // don't all peak in lockstep.
            float localT = Mathf.Clamp((t - _puffDelay[i]) / (1f - _puffDelay[i]), 0f, 1f);
            float localEased = 1f - (1f - localT) * (1f - localT);

            float size = Mathf.Lerp(BillboardStartSize, BillboardEndSize, localEased) * _puffSizeMul[i];
            float sideOffset = Mathf.Lerp(BillboardStartSideOffset, BillboardEndSideOffset, localEased);
            float rise = Mathf.Lerp(0f, BillboardRise, localEased) * _puffRiseMul[i];
            float bA = BillboardMaxAlpha * (1f - Mathf.SmoothStep(0.3f, 1f, localT));

            Vector3 dir = new Vector3(Mathf.Cos(_puffAngle[i]), 0f, Mathf.Sin(_puffAngle[i]));
            _billboards[i].Position = dir * sideOffset + Vector3.Up * rise;
            _billboards[i].Scale = new Vector3(size, size, size);
            var bc = _billboardMats[i].AlbedoColor;
            bc.A = bA;
            _billboardMats[i].AlbedoColor = bc;
        }
    }

    private void Build()
    {
        // Puffs arranged radially around Mario's feet, matching the reference's
        // look of dust splitting off in multiple directions rather than a single
        // clean blob.
        int count = Mathf.Max(1, PuffCount);
        _billboards = new MeshInstance3D[count];
        _billboardMats = new StandardMaterial3D[count];
        _puffAngle = new float[count];
        _puffSizeMul = new float[count];
        _puffRiseMul = new float[count];
        _puffDelay = new float[count];

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < count; i++)
        {
            _puffAngle[i] = (Mathf.Tau * i / count) + rng.RandfRange(-0.35f, 0.35f);
            _puffSizeMul[i] = rng.RandfRange(0.75f, 1.2f);
            _puffRiseMul[i] = rng.RandfRange(0.7f, 1.3f);
            _puffDelay[i] = rng.RandfRange(0f, 0.15f);

            var quad = new QuadMesh { Size = new Vector2(1f, 1f) };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(DustColor.R, DustColor.G, DustColor.B, BillboardMaxAlpha),
                AlbedoTexture = _tex,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
                DisableReceiveShadows = true,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            };
            quad.Material = mat;

            var node = new MeshInstance3D
            {
                Mesh = quad,
                Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(node);

            _billboards[i] = node;
            _billboardMats[i] = mat;
        }
    }

    private static Basis BasisFromUp(Vector3 up)
    {
        Vector3 arbitrary = Mathf.Abs(up.Dot(Vector3.Forward)) < 0.99f ? Vector3.Forward : Vector3.Right;
        Vector3 x = up.Cross(arbitrary).Normalized();
        Vector3 z = x.Cross(up).Normalized();
        return new Basis(x, up, z);
    }

    /// <summary>Soft puffy blob, wispy radial falloff, a few gentle lobes so it doesn't
    /// read as a perfect circle — same technique as the ground-pound shockwave texture.</summary>
    private static Texture2D BuildPuffTexture()
    {
        const int size = 128;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float cx = size * 0.5f,
            cy = size * 0.5f;
        float maxR = size * 0.48f;
        const float lobeAmp = 0.12f;
        const int lobes = 5;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / maxR;
                float dy = (y - cy) / maxR;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                float edge = 1f + lobeAmp * Mathf.Cos(angle * lobes);
                float t = Mathf.Clamp(d / edge, 0f, 1f);
                float a = 1f - t * t;
                a = a * a;

                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}
