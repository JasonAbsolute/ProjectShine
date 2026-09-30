using Godot;

/// <summary>
/// The pickup sparkle: TMario::emitGetEffect (MarioDraw.cpp) -- the item-get particle (ms_itemget1_a, a burst of stars around Mario's
/// centre) with its chime (SE 0x1989), fired when a nozzle or the FLUDD is taken (MarioReceiveMsg.cpp 18-33); the boxes' items also
/// play MSD_SE_SY_GET_NOZZLE (TItemNozzle::touchPlayer). The game's particle data lives in its JPA archives and is not decoded: the
/// scene assets/Sms/Fx/GetSparkle.tscn stands in with a one-shot GPUParticles3D (its process material, draw material and star texture
/// all the scene's) and an AudioStreamPlayer3D whose stream is his to fill. Instanced by the item scenes (SmsNozzleItem.GetEffect) at
/// Mario's chest; this script only starts what the scene holds and removes the scene when the burst is over.
/// </summary>
public partial class SmsGetSparkle : Node3D
{
    [Export] public GpuParticles3D Particles;
    [Export] public AudioStreamPlayer3D Sound;

    private float _life;

    public override void _Ready()
    {
        if (Particles == null)
            GD.PushWarning($"[SMS get effect] {GetPath()}: the scene's Particles export is not set (see assets/Sms/Fx/GetSparkle.tscn)");
        else
        {
            Particles.OneShot = true;
            Particles.Emitting = true;
            _life = (float)(Particles.Lifetime * (1.0 + Particles.Randomness)) + 0.5f;
        }
        if (Sound != null && Sound.Stream != null)
        {
            Sound.Play();
            _life = Mathf.Max(_life, (float)Sound.Stream.GetLength() + 0.1f);
        }
        if (_life <= 0f)
            _life = 1f;
    }

    public override void _Process(double delta)
    {
        _life -= (float)delta;
        if (_life <= 0f)
            QueueFree();
    }
}
