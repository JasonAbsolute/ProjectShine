using Godot;

/// <summary>
/// Pass 106: the trample dust -- TMario::trampleExec (MarioCollision.cpp 166-175). On an NPC: PARTICLE_MS_FUMI_B and FUMI_C through
/// SMS_EasyEmitParticle(.., &amp;mPosition, this, scale 0.5) = emitAndBindToPosPtr + setGlobalScale(0.5): the puff and the hit marks at half
/// size. On anything else (an enemy, a nozzle box): emitParticle(FUMI_A), (FUMI_B), (FUMI_C) = gpMarioParticleManager->emit(id, &amp;mPosition, 0,
/// nullptr), unbound, full size -- the splash ring joins in. Each is a one-shot that emits everything on its first frame (maxFrame 1), so the
/// NPC case's bind to his position pointer moves nothing visible: all four are spawned as top-level bursts at his feet (SmsUnboundBurst, the
/// pass-56 rule: a child emitter would stream the burst after a moving Mario). The game's scale is the emitter's global scale: the positions
/// and the quads of the whole system -- the burst copy's transform carries it (every emission offset and quad size is in the node's space).
/// </summary>
public partial class SmsTrampleDust : Node3D
{
    /// <summary>ms_fumi_a: the 12 splash streaks (the enemy / object trample only).</summary>
    [Export] public GpuParticles3D Ring;
    /// <summary>ms_fumi_b: the one pale puff.</summary>
    [Export] public GpuParticles3D Puff;
    /// <summary>ms_fumi_c: the 8 flickering hit marks.</summary>
    [Export] public GpuParticles3D Marks;
    /// <summary>ms_fumi_c's children: the 8 sky-blue glow spokes flying out of the point.</summary>
    [Export] public GpuParticles3D Spokes;
    /// <summary>MSD_SE_MA_KICK_ENEMY, the stomp's thud -- a slot for his WAV (the game plays it from trampleExec).</summary>
    [Export] public AudioStreamPlayer3D Sound;

    public override void _Ready()
    {
        foreach (var p in new[] { Ring, Puff, Marks, Spokes })
            if (p != null) { p.Emitting = false; p.OneShot = true; }
    }

    /// <summary>trampleExec's emits at Mario's mPosition (his feet): <paramref name="npc"/> = the NPC case (FUMI_B / C at half scale), else
    /// the enemy / object case (FUMI_A / B / C at full scale).</summary>
    public void Play(Vector3 feet, bool npc)
    {
        float scale = npc ? 0.5f : 1f;
        var at = new Transform3D(Basis.Identity.Scaled(Vector3.One * scale), feet);
        Node parent = GetTree().CurrentScene ?? GetParent();
        if (!npc) SmsUnboundBurst.Spawn(Ring, parent, at);
        SmsUnboundBurst.Spawn(Puff, parent, at);
        SmsUnboundBurst.Spawn(Marks, parent, at);
        SmsUnboundBurst.Spawn(Spokes, parent, at);
        if (Sound != null && Sound.Stream != null) { Sound.GlobalPosition = feet; Sound.Play(); }
    }
}
