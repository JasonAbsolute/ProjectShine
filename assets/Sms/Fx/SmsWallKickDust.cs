using Godot;

/// <summary>
/// The wall kick's dust: TMario::startJumpWall (MarioJump.cpp 16) -- emitParticle(PARTICLE_MS_WALLKICK_A, angle) and (_B, angle) at
/// mPosition, angle the wall normal's bearing + 0x8000 (facing into the wall). A (ms_wallkick_a.jpa): one 90-unit puff off a 15-unit
/// circle, flung any way at 13 u/f and braked 10 % a frame, 35 frames +- 50 %, fading over its last eighth. B (ms_wallkick_b.jpa):
/// eight hit marks on a 20-unit circle, 6 frames, drawn out along their length and spinning. The scene assets/Sms/Fx/WallKickDust.tscn
/// (gen_fx_dust.py) holds both; <see cref="Play"/> places the node and restarts them.
/// </summary>
public partial class SmsWallKickDust : Node3D
{
    [Export] public GpuParticles3D Puff;
    [Export] public GpuParticles3D Marks;

    public override void _Ready()
    {
        TopLevel = true; // emitted into the world at the kick, not carried along by Mario afterwards
        foreach (GpuParticles3D p in new[] { Puff, Marks })
        {
            if (p == null)
                continue;
            p.OneShot = true;
            p.Emitting = false;
        }
        if (Puff == null || Marks == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Puff / Marks exports are not both set (see assets/Sms/Fx/WallKickDust.tscn)");
    }

    /// <summary>emitWithRotate at the feet with the kick's bearing (the wall normal's, + 180 degrees).</summary>
    public void Play(Vector3 feetM, float yawRad)
    {
        GlobalPosition = feetM;
        GlobalRotation = new Vector3(0f, yawRad, 0f);
        Puff?.Restart();
        Marks?.Restart();
    }
}
