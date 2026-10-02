using Godot;

/// <summary>
/// The wall slide's dust: TMario::wallSlipEffect (MarioParticle.cpp 309) -- emitAndBindToPosPtr(PARTICLE_MS_MARIWALLSL1, &amp;mPosition, 1)
/// every WALL_SLIDE tick that falls faster than 10 u/f (MarioJump.cpp 623): one emitter per status (bind 1), live while the calls come.
/// ms_mariwallsl1.jpa: 0.8 puffs a frame off a 20-unit shell at his feet, 60 frames +- 35 %, up at 2 u/f gaining 0.7 a frame from a
/// quarter of their life under a Drag of 0.88, 60 units wide, fading from half-life. assets/Sms/Fx/WallSlideDust.tscn (gen_fx_dust.py).
/// </summary>
public partial class SmsWallSlideDust : SmsFollowEffect
{
    [Export] public GpuParticles3D Dust;
    private bool _active;

    public override void _Ready()
    {
        if (Dust == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Dust export is not set (see assets/Sms/Fx/WallSlideDust.tscn)");
        else
            Dust.Emitting = false;
    }

    /// <summary>wallSlipEffect() called this tick (on) or not (off: the emitter is invalidated, its puffs finish).</summary>
    public bool Active
    {
        get => _active;
        set
        {
            if (value == _active)
                return;
            _active = value;
            if (Dust != null)
                Dust.Emitting = value;
        }
    }
}
