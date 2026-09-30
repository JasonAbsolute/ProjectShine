using Godot;

/// <summary>
/// The super hip drop's landing: MarioJump.cpp 1188-1191 -- emitParticle(MS_M_SPHIPD_HIT_A / B / C / D) at mPosition (his feet;
/// unbound, world space) the tick the super fall (state 3) meets the ground, in place of the normal landing's three. The scene
/// assets/Sms/Fx/SuperHipDropLanding.tscn (gen_fx_super.py from the .jpa fields): A -- one flat 250-unit P_msm_shwave1 ring 7 up for 22
/// frames, white going pink; B -- 30 smoke puffs (P_msm_kemuball3, 175 units) from an 18-unit ring 100 up, out at 34 units a frame
/// under a 0.94 air resistance and a 0.92 drag, two seconds; C -- 16 flame streaks (P_msm_glow3, 300 x 75 units, additive) out at 26 and
/// up at 22 units a frame for 35 frames; D -- 8 spinning stars (P_msm_hoshi, 60 units, yellow / pink / pale blue) out at 32 and up at
/// 35 units a frame under a 0.94 air resistance for 140 frames. This script places the scene at the landing spot and restarts them.
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
