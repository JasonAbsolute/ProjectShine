using Godot;

/// <summary>
/// The pickup sparkle: TMario::emitGetEffect (MarioParticle.cpp 120) -- emitAndBindToPosPtr(MS_ITEMGET1_A, &amp;unk160) and the chime
/// (SE 0x1989); fired when a nozzle or the FLUDD is taken (MarioReceiveMsg.cpp 33 / 44). The particle is the disc's ms_itemget1_a.jpa
/// (read with tools jpa_dump.py): an emitter that lives 120 game frames (30 Hz: 4 s), bound to the 'center' joint 15 units above it,
/// FollowEmitter (the stars ride along with Mario); 0.8 stars a frame from a cylinder r 15..60 x h +-60 units, each 35 frames +-12.5 %,
/// sliding outward from the axis at 2 units a frame, lifted by a 0.2 unit/frame^2 field, swirled by a vortex of 7.5 units a frame;
/// a billboard 125 x 140 units (P_msm_star4s, additive), prm pink (255,183,207) -> yellow (255,255,183) over its first 16 frames,
/// alpha 0.4 -> 0.875 by 12.5 %, gone by 100 % from 62.5 %; each star drops one P_msm_stardust1 child every 20 frames (life 20, a
/// 0.28 unit/frame^2 fall). All of that is the scene, assets/Sms/Fx/GetSparkle.tscn: this script binds it to Mario, stops the
/// emitter after EmitFrames and removes the scene once the last star is gone. Sound is his to fill.
/// </summary>
public partial class SmsGetSparkle : SmsFollowEffect
{
    [Export] public GpuParticles3D Particles;
    [Export] public AudioStreamPlayer3D Sound;

    /// <summary>BEM1 maxFrame: how long the emitter emits, in game frames (30 Hz). ms_itemget1_a: 120.</summary>
    [Export] public float EmitFrames = 120f;

    private float _t, _end;

    public override void _Ready()
    {
        if (Particles == null)
            GD.PushWarning($"[SMS get effect] {GetPath()}: the scene's Particles export is not set (see assets/Sms/Fx/GetSparkle.tscn)");
        else
        {
            Particles.OneShot = false;
            Particles.Emitting = true;
            _end = EmitFrames / 30f + (float)Particles.Lifetime + 0.5f;
        }
        if (Sound != null && Sound.Stream != null)
        {
            Sound.Play();
            _end = Mathf.Max(_end, (float)Sound.Stream.GetLength() + 0.1f);
        }
        if (_end <= 0f)
            _end = 1f;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        _t += (float)delta;
        if (Particles != null && Particles.Emitting && _t >= EmitFrames / 30f)
            Particles.Emitting = false;
        if (_t >= _end)
            QueueFree();
    }
}
