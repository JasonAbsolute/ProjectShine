using Godot;

/// <summary>
/// The game's ms_dmg_c stars: emitParticle(PARTICLE_MS_DMG_C) at mPosition (his feet; unbound, world space) when a jump or a dive hits a
/// wall above ClashSpeed and knocks him into JUMP_SHORT_BACK_DOWN (MarioJump.cpp 164, 464 -- the bonk), and as one of the three damage
/// effects (dmg_a / b / c). The scene assets/Sms/Fx/BonkStars.tscn (gen_fx_super.py from the .jpa fields): 6 spinning P_msm_hoshi stars
/// (65 units; yellow / pink / peach, one colour per star) from a 10-unit half shell 5 up, out at 10 and up at 18 units a frame under a
/// 0.96 air resistance, flipping from a tall sliver to a flat one over 75 frames (+-46 %), faded in by 12.5 % and out from 80 %. This
/// script places the scene and restarts the burst; Mario.cs calls it where the game emits dmg_c.
/// </summary>
public partial class SmsBonkStars : Node3D
{
    /// <summary>ms_dmg_c: the stars.</summary>
    [Export] public GpuParticles3D Stars;

    /// <summary>The bonk's sound, his to fill.</summary>
    [Export] public AudioStreamPlayer3D Sound;

    public override void _Ready()
    {
        if (Stars == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Stars export is not set (see assets/Sms/Fx/BonkStars.tscn)");
        if (Stars != null)
        {
            Stars.OneShot = true;
            Stars.Emitting = false;
        }
    }

    /// <summary>The burst at <paramref name="feet"/> (the game's mPosition), world-aligned.</summary>
    public void Play(Vector3 feet)
    {
        GlobalPosition = feet;
        GlobalRotation = Vector3.Zero;
        Stars?.Restart();
        if (Sound != null && Sound.Stream != null)
            Sound.Play();
    }
}
