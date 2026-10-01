using Godot;

/// <summary>
/// The game's ms_dmg_c stars: emitParticle(PARTICLE_MS_DMG_C) at mPosition (his feet; unbound, world space, the emitter's axis UP) when a
/// jump or a dive hits a wall above ClashSpeed and knocks him into JUMP_SHORT_BACK_DOWN (MarioJump.cpp 164, 464 -- the bonk), and as one
/// of the three damage effects (dmg_a / b / c, at calcDamagePos). The scene assets/Sms/Fx/BonkStars.tscn (gen_fx_super.py from the .jpa
/// fields): spinning P_msm_hoshi stars (65 units; yellow / pink / peach, one colour per star) from a 10-unit half shell 5 units along the
/// axis, out from the axis at 10 and along it at 18 units a frame under a 0.96 air resistance, flipping from a tall sliver to a flat one
/// over 75 frames (+-46 %), faded in by 12.5 % and out from 80 %. This script places the scene, aims its axis and restarts the burst;
/// Mario.cs calls it where the game emits dmg_c.
/// </summary>
public partial class SmsBonkStars : Node3D
{
    /// <summary>ms_dmg_c: the stars.</summary>
    [Export] public GpuParticles3D Stars;

    /// <summary>The bonk's sound, his to fill.</summary>
    [Export] public AudioStreamPlayer3D Sound;

    /// <summary>How many stars a burst throws. DEVIATION (Jaysonn, 2026-09-30: "add a few more stars"): 10; the game's ms_dmg_c emits 6
    /// (rate 6, one frame).</summary>
    [Export(PropertyHint.Range, "1,64,1")] public int Count = 10;

    /// <summary>A bonk's stars leave along the impact (the emitter's axis turned from up to the way he is knocked back, away from the wall)
    /// so the burst reads as a hit. DEVIATION (Jaysonn, 2026-09-30: "like an impact when bonking"): the game emits dmg_c with the axis up
    /// for a bonk too -- false = the game's rising spray. A hit's stars (damage) always use the game's up.</summary>
    [Export] public bool AimAlongImpact = true;

    public override void _Ready()
    {
        if (Stars == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Stars export is not set (see assets/Sms/Fx/BonkStars.tscn)");
        if (Stars != null)
        {
            Stars.OneShot = true;
            Stars.Emitting = false;
            Stars.Amount = Mathf.Max(1, Count);
        }
    }

    /// <summary>The burst at <paramref name="at"/> (the game's mPosition / calcDamagePos). <paramref name="impact"/> is the way he is
    /// thrown by a bonk (the wall's normal) -- with AimAlongImpact the emitter's axis follows it; Vector3.Up (or zero) keeps the game's
    /// upward spray.</summary>
    public void Play(Vector3 at, Vector3 impact)
    {
        Vector3 axis = AimAlongImpact && impact.LengthSquared() > 1e-6f ? impact.Normalized() : Vector3.Up;
        Vector3 x = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.999f ? Vector3.Right : Vector3.Up.Cross(axis).Normalized();
        Vector3 z = x.Cross(axis);
        GlobalTransform = new Transform3D(new Basis(x, axis, z), at);
        Stars?.Restart();
        if (Sound != null && Sound.Stream != null)
            Sound.Play();
    }
}
