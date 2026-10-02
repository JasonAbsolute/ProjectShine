using Godot;

/// <summary>
/// The burn's cloud: particle 6, ms_firedmg_a -- gpMarioParticleManager->emitAndBindToPosPtr(6, &amp;mPosition, 0, nullptr) at every hot-floor
/// burn (checkGraffitoFire, MarioMove.cpp 869) and every fire enemy's hit (MarioReceiveMsg.cpp): a simple emitter whose position is
/// re-read from Mario's every frame (TMarioEmitterCallBackBindToPosPtr), its particles NOT FollowEmitter -- they stay where they were
/// born while the emitter rides along. 50 cloud puffs over 100 frames from a 20-unit shell at his feet, each living 100 frames +-35 %,
/// rising 1.5 units a frame under a 0.88/frame drag (so about half a metre in all), 70 units wide, spinning up to 7.5 turns a second,
/// orange-red for the first frames of their colour cycle then grey smoke (kemuball3), fading from 62.5 %. The scene
/// assets/Sms/Fx/FireDamage.tscn holds it; <see cref="Play"/> spawns a copy under this node (which Mario.cs sits at his feet), so a
/// second burn while the first cloud still hangs starts its own, as the game's createSimpleEmitterID does.
/// </summary>
public partial class SmsFireDamageFx : Node3D
{
    /// <summary>ms_firedmg_a: the template, never emitting itself.</summary>
    [Export] public GpuParticles3D Cloud;

    public override void _Ready()
    {
        if (Cloud == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Cloud export is not set (see assets/Sms/Fx/FireDamage.tscn)");
        if (Cloud != null)
        {
            Cloud.OneShot = true;
            Cloud.Emitting = false;
        }
    }

    /// <summary>A burn: a bound copy of the cloud under this node (at his feet), freed when its particles are gone.</summary>
    public void Play()
    {
        SmsUnboundBurst.SpawnBound(Cloud, this);
    }
}
