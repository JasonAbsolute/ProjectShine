using Godot;

/// <summary>
/// The hip drop's landing: MarioJump.cpp 1184 -- emitParticle(MS_HIPDROP_C), (MS_HIPDROP_B), (MS_HIPDROP_A) at mPosition (his feet;
/// unbound, world space, the emitter unrotated) the frame the fall meets the ground (jumpProcess() == 1). The particles (ms_hipdrop_a /
/// b / c.jpa, tools jpa_dump.py): A -- 12 DirectionCross streaks (P_msm_radgrad, lerp (0,139,255) -> white) from a 5-unit circle 5 up,
/// flying out from the axis at 22 units a frame and up at 12 (+-22 %, 5 random), 200 x 33 units at full size, grown in by 29 % of their
/// 20 frames and shrunk from 67 %, alpha 1 -> 0.45 by 12.5 %, gone from 50 %; B -- 20 smoke puffs (P_msm_kemuball3, lerp (26,26,2) ->
/// (255,255,242)) from an 18-unit ring 80 up, out at 20 units a frame with a 0.88/frame drag fading in over the first 23 %, 125 units
/// at full size (0.085 -> 1 by 33 %, -> 0.2 by the end, +-35 %), alpha in by 5 %, out from 41 %, 75 frames +-47 %; C -- one flat
/// 250-unit P_msm_shwave1 ring (lerp (255,144,0) -> white) 7 units up for 18 frames, fading from 25 %. The scene
/// assets/Sms/Fx/HipDropLanding.tscn holds the three GPUParticles3D and a Sound slot; this script places the scene at the landing
/// spot and restarts them. Mario.cs uses it in place of the legacy GroundPoundImpactFx / landing dust when the scene is present.
/// </summary>
public partial class SmsHipDropLanding : Node3D
{
    /// <summary>ms_hipdrop_a: the blue-white streaks.</summary>
    [Export] public GpuParticles3D Sparks;

    /// <summary>ms_hipdrop_b: the smoke puffs.</summary>
    [Export] public GpuParticles3D Smoke;

    /// <summary>ms_hipdrop_c: the flat shockwave ring.</summary>
    [Export] public GpuParticles3D Shockwave;

    /// <summary>The landing's sound, his to fill (the game's MSD_SE_MA_HIP_ATTACK family plays from the player code).</summary>
    [Export] public AudioStreamPlayer3D Sound;

    public override void _Ready()
    {
        if (Sparks == null || Smoke == null || Shockwave == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Sparks / Smoke / Shockwave exports are not all set (see assets/Sms/Fx/HipDropLanding.tscn)");
        foreach (var p in new[] { Sparks, Smoke, Shockwave })
            if (p != null)
            {
                p.OneShot = true;
                p.Emitting = false;
            }
    }

    /// <summary>The landing: the bursts at his feet, world-aligned and world-FIXED like the game's unbound emitters -- each a one-shot copy
    /// (<see cref="SmsUnboundBurst"/>) that stays put when he runs off; the smoke's radial pivot would otherwise follow him for its two
    /// seconds. The nodes here are templates and never emit in the game.</summary>
    public void Play(Vector3 feet)
    {
        foreach (var p in new[] { Sparks, Smoke, Shockwave })
            SmsUnboundBurst.Spawn(p, this, feet);
        if (Sound != null && Sound.Stream != null)
            Sound.Play();
    }
}
