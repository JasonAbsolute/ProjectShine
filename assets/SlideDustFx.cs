using Godot;

/// <summary>
/// Continuous dust trail for sliding (wall slide / belly slide) — small camera-facing
/// puffs spawn at the contact point at a steady rate, drift straight up, and fade.
/// Unlike <see cref="LandingDustFx"/>'s one-shot radial burst, this node stays
/// "emitting" every frame while the caller keeps calling <see cref="Emit"/>, and each
/// puff freezes at its own spawn position — so as Mario moves (down the wall, along
/// the ground) the puffs are left behind, reading as a trail rather than a burst.
///
/// Same puff texture/billboard technique as LandingDustFx (soft radial-falloff quad,
/// BillboardMode.Enabled so it always faces the camera).
///
/// Usage: attach to a Node3D child of Mario (e.g. "SlideDustFx"). Each physics tick
/// while sliding, call <see cref="Emit"/> with the current contact point; call
/// <see cref="Stop"/> the moment the slide ends (or just stop calling Emit — puffs
/// already spawned keep animating and the trail simply stops growing).
/// </summary>
public partial class SlideDustFx : Node3D
{
    [Export] public float SpawnInterval = 0.045f;
    [Export] public float PuffLifetime = 0.4f;
    [Export] public float PuffStartSize = 0.16f;
    [Export] public float PuffEndSize = 0.45f;
    [Export] public float RiseHeight = 0.5f;
    /// <summary>Small random horizontal wobble so the trail doesn't look like a
    /// perfectly straight line of identical puffs.</summary>
    [Export] public float HorizontalJitter = 0.06f;
    [Export(PropertyHint.Range, "0,1")] public float MaxAlpha = 0.8f;
    [Export] public Color DustColor = new Color(1f, 1f, 1f);
    [Export] public int PoolSize = 16;

    private struct Puff
    {
        public MeshInstance3D Node;
        public StandardMaterial3D Mat;
        public float Timer; // < 0 = inactive
        public Vector3 BasePos;
        public Vector3 Up;
        public Vector3 Jitter;
        public float SizeMul;
    }

    private Puff[] _pool;
    private int _nextSlot;
    private float _spawnAccum;
    private bool _emitting;
    private Vector3 _emitPos;
    private Vector3 _emitUp = Vector3.Up;
    private RandomNumberGenerator _rng;
    private static Texture2D _tex;

    public override void _Ready()
    {
        // Individual puffs carry their own world position (see SpawnPuff) — freeze
        // this container's own transform at identity so it never moves them.
        TopLevel = true;
        GlobalTransform = Transform3D.Identity;

        if (_tex == null)
            _tex = BuildPuffTexture();

        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        Build();
    }

    /// <summary>Call every tick while sliding, with the current contact point.</summary>
    public void Emit(Vector3 pos, Vector3? up = null)
    {
        _emitting = true;
        _emitPos = pos;
        _emitUp = (up ?? Vector3.Up).Normalized();
    }

    /// <summary>Call once when the slide ends. Already-spawned puffs keep animating.</summary>
    public void Stop()
    {
        _emitting = false;
        _spawnAccum = 0f;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_emitting)
        {
            _spawnAccum += dt;
            while (_spawnAccum >= SpawnInterval)
            {
                _spawnAccum -= SpawnInterval;
                SpawnPuff();
            }
        }

        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i].Timer < 0f)
                continue;

            _pool[i].Timer += dt;
            float t = _pool[i].Timer / PuffLifetime;
            if (t >= 1f)
            {
                _pool[i].Node.Visible = false;
                _pool[i].Timer = -1f;
                continue;
            }

            float eased = 1f - (1f - t) * (1f - t);
            float size = Mathf.Lerp(PuffStartSize, PuffEndSize, eased) * _pool[i].SizeMul;
            _pool[i].Node.Position = _pool[i].BasePos + _pool[i].Up * (RiseHeight * eased) + _pool[i].Jitter * eased;
            _pool[i].Node.Scale = new Vector3(size, size, size);

            float a = MaxAlpha * (1f - Mathf.SmoothStep(0.3f, 1f, t));
            var c = _pool[i].Mat.AlbedoColor;
            c.A = a;
            _pool[i].Mat.AlbedoColor = c;
        }
    }

    private void SpawnPuff()
    {
        int i = _nextSlot;
        _nextSlot = (_nextSlot + 1) % _pool.Length;

        Vector3 jitterDir = new Vector3(_rng.RandfRange(-1f, 1f), 0f, _rng.RandfRange(-1f, 1f));
        if (jitterDir.LengthSquared() > 0.0001f)
            jitterDir = jitterDir.Normalized() * _rng.RandfRange(0f, HorizontalJitter);

        _pool[i].Timer = 0f;
        _pool[i].BasePos = _emitPos;
        _pool[i].Up = _emitUp;
        _pool[i].Jitter = jitterDir;
        _pool[i].SizeMul = _rng.RandfRange(0.8f, 1.15f);

        _pool[i].Node.Visible = true;
        _pool[i].Node.Position = _pool[i].BasePos;
        _pool[i].Node.Scale = new Vector3(PuffStartSize, PuffStartSize, PuffStartSize) * _pool[i].SizeMul;

        var c = _pool[i].Mat.AlbedoColor;
        c.A = MaxAlpha;
        _pool[i].Mat.AlbedoColor = c;
    }

    private void Build()
    {
        int count = Mathf.Max(1, PoolSize);
        _pool = new Puff[count];

        for (int i = 0; i < count; i++)
        {
            var quad = new QuadMesh { Size = new Vector2(1f, 1f) };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(DustColor.R, DustColor.G, DustColor.B, MaxAlpha),
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

            _pool[i].Node = node;
            _pool[i].Mat = mat;
            _pool[i].Timer = -1f;
        }
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
