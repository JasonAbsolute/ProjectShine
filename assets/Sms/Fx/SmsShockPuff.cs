using Godot;

/// <summary>
/// The shock's closing puff: TMario::elecEndEffect (MarioParticle.cpp 701) as ELECTRIC_DAMAGE ends (MarioAutodemo.cpp 466) --
/// emitAndBindToPosPtr(MS_MOE_FIRE_OFF, &amp;mCenterPos, 0, this): a simple emitter following the centre joint, its own translation 30 units
/// up. The particles (ms_moe_fire_off.jpa): 30 small dark puffs over 60 frames (one every other frame) from a 5-unit shell, each living
/// 50 frames +-30 %, up at 2.5 units a frame gaining 0.05 a frame from a quarter of their life (about 2 m of rise), 30 units wide,
/// spinning, grey 80 -> 120 over (24,24,24) (kemuball3), fading from 62.5 %. The scene assets/Sms/Fx/ShockPuff.tscn holds it; this
/// script binds the node to the joint and <see cref="Play"/> restarts the one-shot (a second puff before the first is done restarts
/// it -- the shock takes a second and the invincibility after it another half, so it never happens).
/// </summary>
public partial class SmsShockPuff : SmsFollowEffect
{
    /// <summary>ms_moe_fire_off: the puffs.</summary>
    [Export] public GpuParticles3D Smoke;

    public override void _Ready()
    {
        if (Smoke == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Smoke export is not set (see assets/Sms/Fx/ShockPuff.tscn)");
        if (Smoke != null)
        {
            Smoke.OneShot = true;
            Smoke.Emitting = false;
        }
    }

    public void Play()
    {
        if (Smoke == null)
            return;
        Smoke.Restart();
    }
}
