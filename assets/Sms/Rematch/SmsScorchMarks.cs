using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 211: SCORCH MARKS on the floor (Jaysonn: "Scorch marks where Bomb Boos blast and lava balls land. Fading decals on the floor. Right now a blast
/// leaves nothing behind."). One node in the arena keeps a pool of Decals (MaxMarks; the oldest is reused): a blast or a landing calls MarkBlast / MarkLava
/// with its floor point, and a dark splat is projected onto the stone there -- a procedural scorch (three shapes, drawn in code at start: a ragged disc, darkest
/// in the middle, grainy), turned a random way and sized BombSize x the blast's reach or LavaSize x the lava ball's radius x the splat's strength. A fresh mark
/// GLOWS orange for GlowSeconds (the decal's emission dying out), sits black for HoldSeconds, then fades over FadeSeconds. The decals project only onto the
/// floor: the Arena puts its meshes on visual layer 2 as well and the decals cull to that layer, so Mario and the Boos walking over a mark are not stained.
/// Knobs: Scorch (off = none), MaxMarks, BombSize, LavaSize, Darkness, Core, HoldSeconds, FadeSeconds, GlowSeconds, GlowEnergy, GlowColor. Readouts for the drives:
/// Made, Alive, LastAt, LastSize, LastStrength, Newest (the decal), Advance(seconds) ages every mark.
/// </summary>
[GlobalClass]
public partial class SmsScorchMarks : Node3D
{
    public static SmsScorchMarks Current { get; private set; }
    /// <summary>The visual layer the floor's meshes carry for the decals (bit 2); the Arena adds it to its meshes at start.</summary>
    public const uint FloorLayer = 2u;

    [ExportGroup("Scorch marks (211)")]
    /// <summary>Off = no marks at all.</summary>
    [Export] public bool Scorch = true;
    /// <summary>How many marks can be on the floor at once (the oldest is reused).</summary>
    [Export(PropertyHint.Range, "1,64,1")] public int MaxMarks = 24;
    /// <summary>A Bomb Boo's mark is this x the blast's reach across (BlastRadius 3.5 m -> 4.5 m); a lava ball's is this x the ball's radius x the splat (a landing 1, a bounce 0.6, the burst on Mario 1.6, its going out 0.45).</summary>
    [Export(PropertyHint.Range, "0.2,4,0.05")] public float BombSize = 1.3f;
    [Export(PropertyHint.Range, "0.5,8,0.1")] public float LavaSize = 3.2f;
    /// <summary>How black a fresh mark is (1 = the stone fully covered in the middle; runs dm / dn at 0.85 / 0.9 read as grey shadows).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float Darkness = 1f;
    /// <summary>How much of each mark, from the middle out, is solid black before the ragged edge softens (0.7 = the inner 70 %; read at start, when the shapes are drawn).</summary>
    [Export(PropertyHint.Range, "0.2,0.95,0.05")] public float Core = 0.7f;
    /// <summary>How long a mark sits before it starts to fade, and how long the fade takes.</summary>
    [Export(PropertyHint.Range, "0,60,0.5")] public float HoldSeconds = 8f;
    [Export(PropertyHint.Range, "0.5,60,0.5")] public float FadeSeconds = 10f;
    /// <summary>A fresh mark glows (orange, dying out over GlowSeconds; GlowEnergy 0 = none).</summary>
    [Export(PropertyHint.Range, "0,10,0.1")] public float GlowSeconds = 1.5f;
    [Export(PropertyHint.Range, "0,8,0.1")] public float GlowEnergy = 2.5f;
    [Export] public Color GlowColor = new(1f, 0.45f, 0.1f);
    [Export] public bool DebugPrint;

    // ---- readouts
    public int Made { get; private set; }
    public int Alive { get { int n = 0; for (int i = 0; i < _pool.Count; i++) if (_alpha[i] > 0.002f) n++; return n; } }
    public Vector3 LastAt { get; private set; }
    public float LastSize { get; private set; }
    public float LastStrength { get; private set; }
    public Decal Newest => _newest >= 0 && _newest < _pool.Count ? _pool[_newest] : null;
    public float NewestAlpha => _newest >= 0 && _newest < _pool.Count ? _alpha[_newest] : 0f;
    public float NewestAge => _newest >= 0 && _newest < _pool.Count ? _age[_newest] : -1f;
    public int Textures => _tex.Count;
    /// <summary>The last 16 marks made (where, how big, how black, on which physics frame) -- for the drives, which see several land in one tick.</summary>
    public readonly List<(Vector3 at, float size, float strength, ulong frame)> Recent = new();

    private readonly List<Decal> _pool = new(); private readonly List<float> _age = new(), _str = new(), _alpha = new();
    private readonly List<Texture2D> _tex = new(), _glow = new(); private int _newest = -1; private readonly Random _rng = new(211);

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        Current = this;
        for (int i = 0; i < 3; i++) { var (a, g) = BuildTextures(i); _tex.Add(a); _glow.Add(g); }
        if (DebugPrint) GD.Print($"[SMS scorch] {GetPath()}: ready -- {_tex.Count} shapes, up to {MaxMarks} marks, hold {HoldSeconds} s + fade {FadeSeconds} s, glow {GlowSeconds} s");
    }
    public override void _ExitTree() { if (Current == this) Current = null; }

    /// <summary>A Bomb Boo's blast on the floor: its reach (metres) sets the mark.</summary>
    public static void MarkBlast(Vector3 floorPoint, float blastRadius) { var c = Current; if (c != null) c.Add(floorPoint, blastRadius * c.BombSize, 1f); }
    /// <summary>Lava on the floor: the ball's radius and the splat's strength (a landing 1, a bounce 0.6, the burst 1.6, the end 0.45) set the mark.</summary>
    public static void MarkLava(Vector3 floorPoint, float ballRadius, float k) { var c = Current; if (c != null) c.Add(floorPoint, ballRadius * c.LavaSize * k, Mathf.Clamp(0.55f + 0.45f * k, 0.3f, 1f)); }

    /// <summary>A mark `size` metres across at a floor point, `strength` x Darkness black.</summary>
    public void Add(Vector3 at, float size, float strength)
    {
        if (!Scorch || size <= 0.05f || !IsInsideTree()) return;
        int i;
        if (_pool.Count < MaxMarks)
        {
            var d = new Decal { Name = $"Scorch{_pool.Count}", AlbedoMix = 1f, CullMask = FloorLayer, UpperFade = 0.6f, LowerFade = 0.6f, NormalFade = 0.0f, TopLevel = true };
            AddChild(d); _pool.Add(d); _age.Add(0f); _str.Add(0f); _alpha.Add(0f); i = _pool.Count - 1;
        }
        else { i = 0; for (int j = 1; j < _pool.Count; j++) if (_age[j] > _age[i]) i = j; }
        var dec = _pool[i]; int shape = _rng.Next(_tex.Count);
        dec.TextureAlbedo = _tex[shape]; dec.TextureEmission = _glow[shape];
        float s = size * (0.88f + 0.24f * (float)_rng.NextDouble());
        dec.Size = new Vector3(s, 3f, s);                                                            // the box: 1.5 m over and under the floor point, projecting down
        dec.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(0f, (float)_rng.NextDouble() * Mathf.Tau, 0f)), at);
        _age[i] = 0f; _str[i] = Mathf.Clamp(strength, 0f, 1f) * Darkness; _newest = i;
        Apply(i);
        Made++; LastAt = at; LastSize = s; LastStrength = _str[i];
        Recent.Add((at, s, _str[i], Engine.GetPhysicsFrames())); if (Recent.Count > 16) Recent.RemoveAt(0);
        if (DebugPrint) GD.Print($"[SMS scorch] mark {Made} at ({at.X:F1}, {at.Y:F2}, {at.Z:F1}), {s:F1} m across, {_str[i]:F2} black (shape {shape}, slot {i})");
    }
    /// <summary>For the drives and the shots: every mark `seconds` older.</summary>
    public void Advance(float seconds) { for (int i = 0; i < _pool.Count; i++) { _age[i] += seconds; Apply(i); } }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = 0; i < _pool.Count; i++) { if (_alpha[i] <= 0f && _age[i] > HoldSeconds + FadeSeconds) continue; _age[i] += dt; Apply(i); }
    }
    private void Apply(int i)
    {
        float age = _age[i];
        float k = age <= HoldSeconds ? 1f : 1f - Mathf.Clamp((age - HoldSeconds) / Mathf.Max(0.01f, FadeSeconds), 0f, 1f);
        float a = k * _str[i]; _alpha[i] = a;
        var d = _pool[i];
        d.Visible = a > 0.002f;
        d.Modulate = new Color(1f, 1f, 1f, a);
        float g = GlowSeconds > 0.01f ? Mathf.Clamp(1f - age / GlowSeconds, 0f, 1f) : 0f;
        d.EmissionEnergy = GlowEnergy * g * g * (_str[i] / Mathf.Max(0.01f, Darkness));
    }

    /// <summary>A scorch shape drawn in code: a ragged disc (three sine lobes of angular noise), darkest in the middle, grainy; and its glow (the same shape in GlowColor).</summary>
    private (Texture2D albedo, Texture2D glow) BuildTextures(int seed)
    {
        const int N = 128; var rnd = new Random(1000 + seed);
        float p1 = (float)rnd.NextDouble() * Mathf.Tau, p2 = (float)rnd.NextDouble() * Mathf.Tau, p3 = (float)rnd.NextDouble() * Mathf.Tau;
        var img = Image.CreateEmpty(N, N, false, Image.Format.Rgba8); var gl = Image.CreateEmpty(N, N, false, Image.Format.Rgba8);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float r = MathF.Sqrt(u * u + v * v), th = MathF.Atan2(v, u);
                float lobes = 1f + 0.16f * MathF.Sin(5f * th + p1) + 0.11f * MathF.Sin(9f * th + p2) + 0.07f * MathF.Sin(14f * th + p3);
                float edge = r / (0.9f * lobes);
                float shape = Mathf.SmoothStep(1f, Mathf.Clamp(Core, 0.2f, 0.95f), edge);           // 1 inside Core of the way out, 0 at the ragged edge (runs dm / dn: 38 % / 60 % cores read as grey smudges)
                float grain = 0.92f + 0.08f * Hash(x * 7 + seed * 131, y * 13 + seed * 17);
                float a = Mathf.Clamp(shape * grain, 0f, 1f);
                float dark = 0.03f + 0.03f * (1f - shape);                                           // near black in the middle, a touch browner at the rim
                img.SetPixel(x, y, new Color(dark, dark * 0.9f, dark * 0.8f, a));
                float ga = MathF.Pow(a, 1.5f);
                gl.SetPixel(x, y, new Color(GlowColor.R * ga, GlowColor.G * ga, GlowColor.B * ga, a));
            }
        return (ImageTexture.CreateFromImage(img), ImageTexture.CreateFromImage(gl));
    }
    private static float Hash(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xFFFF) / 65535f; }
}
