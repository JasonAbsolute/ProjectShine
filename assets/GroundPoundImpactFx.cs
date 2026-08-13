using Godot;

/// <summary>
/// One-shot impact FX for the ground pound: a flat shockwave ring that expands
/// outward across the floor, plus a radial cone of blue streaks shooting up-and-out
/// at ~45° from the impact point.
///
/// Usage: attach to a Node3D child of Mario (e.g. "GroundPoundImpactFx").
/// Mario.cs calls <see cref="Trigger"/> at the moment of ground-pound landing.
/// </summary>
public partial class GroundPoundImpactFx : Node3D
{
    // Shockwave starburst
    [Export] public float ShockwaveStartRadius = 0.4f;
    [Export] public float ShockwaveEndRadius = 4.5f;
    [Export] public float ShockwaveDuration = 0.28f;
    [Export(PropertyHint.Range, "0,1")] public float ShockwaveMaxAlpha = 0.85f;
    // Tan/cream dust color (kicked-up sand). Pure yellow reads cartoony; tan reads "dust".
    [Export] public Color ShockwaveColor = new Color(0.88f, 0.78f, 0.52f);
    /// <summary>Number of dust lobes/puffs around the cloud's edge.</summary>
    [Export(PropertyHint.Range, "4,16")] public int ShockwaveSpikeCount = 7;
    /// <summary>How pronounced the lobes are (1 = nearly circular, 8 = very lumpy).</summary>
    [Export(PropertyHint.Range, "1,8")] public float ShockwaveSpikeSharpness = 3f;
    /// <summary>Distance above Mario's foot position to place the ring. Bump if the ring clips through the floor.</summary>
    [Export] public float ShockwaveHeightAboveFloor = 0.25f;

    // Blue radial burst — long thin glowy beams fanning out near-horizontally from
    // the impact point. Matches the SMS reference: many directions, almost flat.
    [Export(PropertyHint.Range, "4,24")] public int BurstDirections = 12;
    /// <summary>0 = straight up, 90 = horizontal. SMS-style is ~35° (beams shoot UP and out at ~55° above horizontal).</summary>
    [Export(PropertyHint.Range, "0,89")] public float BurstTiltFromVerticalDeg = 35f;
    [Export] public int StreaksPerDirection = 1;
    [Export] public float BlueStreakSpeed = 14f;
    [Export] public float BlueStreakLifetime = 0.30f;
    /// <summary>X = beam radius (thickness), Y = beam length.</summary>
    [Export] public Vector2 BlueStreakSize = new Vector2(0.05f, 1.20f);
    [Export] public Color BlueStreakColor = new Color(0.55f, 0.85f, 1.00f);

    private MeshInstance3D _ring;
    private float _shockTimer = -1f;
    private StandardMaterial3D _ringMat;
    private static Texture2D _ringTex;
    private static Texture2D _streakTex;

    private sealed class Streak
    {
        public MeshInstance3D Node;
        public StandardMaterial3D Mat;
        public Vector3 Velocity;
        public float Timer;
    }
    private readonly System.Collections.Generic.List<Streak> _streaks = new();

    public override void _Ready()
    {
        // Don't follow Mario — once we're triggered, stay where the pound landed.
        TopLevel = true;

        // Rebuild the starburst texture every Ready in case spike count/sharpness was tweaked.
        _ringTex = BuildStarburstTexture(ShockwaveSpikeCount, ShockwaveSpikeSharpness);
        if (_streakTex == null) _streakTex = BuildSoftStreakTexture();

        BuildShockwave();
    }

    /// <summary>
    /// Fire both effects. Caller passes Mario's approximate position; we raycast
    /// straight down from there to find the actual floor surface so the shockwave
    /// sits flush on the ground regardless of Mario's capsule offset or floor slope.
    /// </summary>
    public void Trigger(Vector3 marioPos)
    {
        Vector3 placePos = marioPos;
        var space = GetWorld3D()?.DirectSpaceState;
        if (space != null)
        {
            Vector3 from = marioPos + new Vector3(0f, 1.5f, 0f);
            Vector3 to = marioPos + new Vector3(0f, -5f, 0f);
            var q = PhysicsRayQueryParameters3D.Create(from, to);
            q.CollideWithBodies = true;
            q.CollideWithAreas = false;
            // Skip player bodies so the ray sees through Mario to the real floor.
            var excludes = new Godot.Collections.Array<Godot.Rid>();
            foreach (var n in GetTree().GetNodesInGroup("player"))
                if (n is CollisionObject3D co) excludes.Add(co.GetRid());
            if (excludes.Count > 0) q.Exclude = excludes;
            var result = space.IntersectRay(q);
            if (result.Count > 0)
                placePos = (Vector3)result["position"];
        }

        GlobalPosition = placePos;
        _shockTimer = 0f;
        if (_ring != null) _ring.Visible = true;

        SpawnAllStreaks();
    }

    private void SpawnAllStreaks()
    {
        // Clear any stragglers (e.g. if user triple-pounds in rapid succession)
        for (int i = _streaks.Count - 1; i >= 0; i--)
        {
            if (IsInstanceValid(_streaks[i].Node)) _streaks[i].Node.QueueFree();
        }
        _streaks.Clear();

        float tilt = Mathf.DegToRad(BurstTiltFromVerticalDeg);
        float sinTilt = Mathf.Sin(tilt);
        float cosTilt = Mathf.Cos(tilt);

        for (int i = 0; i < BurstDirections; i++)
        {
            float yaw = Mathf.Tau * i / BurstDirections;
            Vector3 dir = new Vector3(sinTilt * Mathf.Sin(yaw), cosTilt, sinTilt * Mathf.Cos(yaw));
            for (int j = 0; j < StreaksPerDirection; j++)
            {
                SpawnOneStreak(dir, j);
            }
        }
    }

    private void SpawnOneStreak(Vector3 dir, int variant)
    {
        // Capsule mesh — long axis is +Y. Rotate so its +Y aligns with `dir`.
        var capsule = new CapsuleMesh
        {
            Radius = BlueStreakSize.X * 0.5f,
            Height = BlueStreakSize.Y,
            RadialSegments = 8,
            Rings = 1,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = BlueStreakColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,    // glowy beam look
            DisableReceiveShadows = true,
        };
        capsule.Material = mat;

        var node = new MeshInstance3D
        {
            Mesh = capsule,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        // Orient: rotate Up → dir
        Vector3 from = Vector3.Up;
        Vector3 to = dir.Normalized();
        Vector3 axis = from.Cross(to);
        if (axis.LengthSquared() < 1e-6f) axis = Vector3.Forward;
        else axis = axis.Normalized();
        float angle = from.AngleTo(to);
        node.Quaternion = new Quaternion(axis, angle);

        // Place the streak's center half-way along dir so its base sits at the impact origin.
        float startOffset = BlueStreakSize.Y * 0.5f;
        // Slight variant offset so multiple streaks per direction don't perfectly overlap.
        float jitter = (variant - (StreaksPerDirection - 1) * 0.5f) * (BlueStreakSize.X * 1.4f);
        Vector3 perp = axis * jitter;
        node.Position = dir * startOffset + perp;

        AddChild(node);

        _streaks.Add(new Streak
        {
            Node = node,
            Mat = mat,
            Velocity = dir * BlueStreakSpeed,
            Timer = 0f,
        });
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // --- Starburst shockwave ---
        if (_shockTimer >= 0f)
        {
            _shockTimer += dt;
            float t = _shockTimer / ShockwaveDuration;
            if (t >= 1f)
            {
                _ring.Visible = false;
                _shockTimer = -1f;
            }
            else
            {
                float radius = Mathf.Lerp(ShockwaveStartRadius, ShockwaveEndRadius, t);
                _ring.Scale = new Vector3(radius, 1f, radius);
                float a = ShockwaveMaxAlpha * (1f - Mathf.SmoothStep(0.5f, 1f, t));
                var c = _ringMat.AlbedoColor;
                c.A = a;
                _ringMat.AlbedoColor = c;
            }
        }

        // --- Slanted blue streaks ---
        if (_streaks.Count > 0)
        {
            for (int i = _streaks.Count - 1; i >= 0; i--)
            {
                var s = _streaks[i];
                s.Timer += dt;
                if (s.Timer >= BlueStreakLifetime || !IsInstanceValid(s.Node))
                {
                    if (IsInstanceValid(s.Node)) s.Node.QueueFree();
                    _streaks.RemoveAt(i);
                    continue;
                }

                s.Node.Position += s.Velocity * dt;
                // Fade alpha over lifetime. Hold full brightness for the first half, then fade.
                float t = s.Timer / BlueStreakLifetime;
                float alpha = (1f - Mathf.SmoothStep(0.4f, 1f, t)) * BlueStreakColor.A;
                var col = s.Mat.AlbedoColor;
                col.A = alpha;
                s.Mat.AlbedoColor = col;
            }
        }
    }

    private void BuildShockwave()
    {
        // PlaneMesh defaults to face-Y (lies flat on XZ), which is what we want.
        var plane = new PlaneMesh
        {
            Size = new Vector2(2f, 2f),       // scaled at runtime; this is the unit size
        };

        _ringMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(ShockwaveColor.R, ShockwaveColor.G, ShockwaveColor.B, ShockwaveMaxAlpha),
            AlbedoTexture = _ringTex,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BlendMode = BaseMaterial3D.BlendModeEnum.Mix,   // dust-cloud: solid tan color against the floor
            DisableReceiveShadows = true,
        };
        plane.Material = _ringMat;

        _ring = new MeshInstance3D
        {
            Mesh = plane,
            Position = new Vector3(0f, ShockwaveHeightAboveFloor, 0f),
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_ring);
    }

    // ---- Static texture builders ----

    /// <summary>
    /// Dust cloud texture: a soft puffy blob with a few low-amplitude lobes around
    /// the edge so it doesn't read as a perfect circle. Wispy radial falloff —
    /// solid in the center, fades smoothly to nothing at the rim.
    /// </summary>
    private static Texture2D BuildStarburstTexture(int spikes, float sharpness)
    {
        const int size = 256;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float cx = size * 0.5f, cy = size * 0.5f;
        float maxR = size * 0.48f;

        // Lobe amplitude scales with `sharpness` — 1 → almost circle, 8 → very lumpy.
        float lobeAmp = Mathf.Clamp((sharpness - 1f) / 7f, 0f, 1f) * 0.18f;
        // A second low-frequency wobble keeps the lobes from looking too regular.
        float wobbleAmp = lobeAmp * 0.4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / maxR;
                float dy = (y - cy) / maxR;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Edge radius varies a bit per angle so the silhouette has dust puffs
                // sticking out rather than being a clean disc.
                float edge = 1f
                    + lobeAmp   * Mathf.Cos(angle * spikes)
                    + wobbleAmp * Mathf.Cos(angle * 3f + 0.7f);

                // Smooth quadratic falloff from full opacity at center to 0 at edge.
                // No hard rim — the whole thing is wispy.
                float t = Mathf.Clamp(d / edge, 0f, 1f);
                float a = 1f - t * t;        // soft falloff
                a = a * a;                   // softer at edges, brighter at core

                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    private static Texture2D BuildSoftStreakTexture()
    {
        const int w = 32, h = 128;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        float cx = w * 0.5f, cy = h * 0.5f;
        float rx = w * 0.45f, ry = h * 0.5f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d2 = dx * dx + dy * dy;
                float a = Mathf.Clamp(1f - d2, 0f, 1f);
                a = a * a;
                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}
