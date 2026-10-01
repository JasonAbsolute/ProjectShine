using Godot;

/// <summary>
/// The super hip drop's landing: MarioJump.cpp 1188-1191 -- emitParticle(MS_M_SPHIPD_HIT_A / B / C / D) at mPosition (his feet;
/// unbound, world space) the tick the super fall (state 3) meets the ground, in place of the normal landing's three. The scene
/// assets/Sms/Fx/SuperHipDropLanding.tscn (gen_fx_super.py from the .jpa fields): A -- one flat 250-unit P_msm_shwave1 ring 7 up for 22
/// frames, white going pink; B -- 30 smoke puffs (P_msm_kemuball3, 175 units) from an 18-unit ring 100 up, out at 34 units a frame
/// under a 0.94 air resistance and a 0.92 drag, two seconds; C -- 16 flame streaks (P_msm_glow3, 300 x 75 units, additive) out at 26 and
/// up at 22 units a frame for 35 frames; D -- spinning stars (P_msm_hoshi, 60 units, yellow / pink / pale blue) out at 32 and up at 35
/// units a frame under a 0.94 air resistance for 140 frames. This script places the scene at the landing spot and restarts them, and
/// applies the knobs below at ready.
/// </summary>
public partial class SmsSuperHipDropLanding : Node3D
{
    /// <summary>ms_m_sphipd_hit_a: the flat shockwave ring.</summary>
    [Export] public GpuParticles3D Shockwave;

    /// <summary>ms_m_sphipd_hit_b: the smoke puffs.</summary>
    [Export] public GpuParticles3D Smoke;

    /// <summary>ms_m_sphipd_hit_c: the flame streaks.</summary>
    [Export] public GpuParticles3D Flames;

    /// <summary>ms_m_sphipd_hit_d: the stars.</summary>
    [Export] public GpuParticles3D Stars;

    /// <summary>The landing's sound, his to fill.</summary>
    [Export] public AudioStreamPlayer3D Sound;

    /// <summary>How many stars the landing throws. DEVIATION (Jaysonn, 2026-09-30: "not as many stars"): 16; the game's ms_m_sphipd_hit_d
    /// emits 8 (rate 8, one frame).</summary>
    [Export(PropertyHint.Range, "1,64,1")] public int StarCount = 16;

    /// <summary>The flame streaks' speed away from the landing point, game units a frame (60 Hz). The game: ms_m_sphipd_hit_c velAxis 26.</summary>
    [Export(PropertyHint.Range, "0,80,0.5")] public float FlameOutSpeed = 26f;

    /// <summary>The flame streaks' upward speed, game units a frame. The game: ms_m_sphipd_hit_c velDir 22 (about 40 degrees of elevation
    /// with the 26 out); lower it for flatter streaks.</summary>
    [Export(PropertyHint.Range, "0,80,0.5")] public float FlameUpSpeed = 22f;

    private const int FlameDirections = 96;        // the ring of directed emission points baked in the scene (gen_fx_super.py)
    private const float FlameSpeedRatio = 0.2249f; // velRatio: each streak's speed x (1 +- 0.2249)

    public override void _Ready()
    {
        if (Shockwave == null || Smoke == null || Flames == null || Stars == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Shockwave / Smoke / Flames / Stars exports are not all set (see assets/Sms/Fx/SuperHipDropLanding.tscn)");
        foreach (var p in new[] { Shockwave, Smoke, Flames, Stars })
            if (p != null)
            {
                p.OneShot = true;
                p.Emitting = false;
            }
        if (Stars != null)
            Stars.Amount = Mathf.Max(1, StarCount);
        ApplyFlameSpeeds();
    }

    /// <summary>The flames' emission normals (the ring's directed points) and speed from FlameOutSpeed / FlameUpSpeed: the JPA velAxis
    /// (out, level) + velDir (up) sum, x (1 +- 0.2249), in the port's metres a second.</summary>
    private void ApplyFlameSpeeds()
    {
        if (Flames?.ProcessMaterial is not ParticleProcessMaterial mat)
            return;
        var img = Image.CreateEmpty(FlameDirections, 1, false, Image.Format.Rgbf);
        for (int i = 0; i < FlameDirections; i++)
        {
            float ang = Mathf.Tau * i / FlameDirections;
            var d = new Vector3(FlameOutSpeed * Mathf.Sin(ang), FlameUpSpeed, FlameOutSpeed * Mathf.Cos(ang));
            d = d.LengthSquared() < 1e-6f ? Vector3.Up : d.Normalized();
            img.SetPixel(i, 0, new Color(d.X, d.Y, d.Z));
        }
        mat.EmissionNormalTexture = ImageTexture.CreateFromImage(img);
        float speed = SmsUnits.SpeedToMps(Mathf.Sqrt(FlameOutSpeed * FlameOutSpeed + FlameUpSpeed * FlameUpSpeed));
        mat.InitialVelocityMin = speed * (1f - FlameSpeedRatio);
        mat.InitialVelocityMax = speed * (1f + FlameSpeedRatio);
    }

    /// <summary>The landing: the bursts at his feet, world-aligned like the game's unbound emitters.</summary>
    public void Play(Vector3 feet)
    {
        GlobalPosition = feet;
        GlobalRotation = Vector3.Zero;
        foreach (var p in new[] { Shockwave, Smoke, Flames, Stars })
            p?.Restart();
        if (Sound != null && Sound.Stream != null)
            Sound.Play();
    }
}
