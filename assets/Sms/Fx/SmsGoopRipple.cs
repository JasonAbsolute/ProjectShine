using Godot;

/// <summary>
/// SMS_EmitSinkInPollutionEffect (MarioUtil/EffectUtil.cpp 56): every 20 of the director's 120 Hz move ticks, at the sink point with the
/// ground normal as Y (setXYZDir(C, normal, B) with B = X x normal), an unbound ms_mari_rakuhamon emitter (one goop ripple ring, flag 2 = the
/// screen texture) and, while the actor is not at the bottom, an ms_mari_rakuball one (the goop balls). Called for an NPC while it sinks and
/// climbs out (NpcBase.cpp 554: LIVE_FLAG_UNK400000, SINK_BOTTOM -> no balls) and for Mario while he stands on goop (MarioMove.cpp 1102: the
/// ring only) or sinks in it (1221: ring + balls). The Godot emitters run continuously at the game's steady rates (6 rings, 48 balls a second --
/// GoopSinkRipple.tscn, gen_fx_npc.py); the owner calls <see cref="Set"/> every tick and the particles stay in the world where they were born.
/// </summary>
public partial class SmsGoopRipple : Node3D
{
    private GpuParticles3D _ring, _balls;

    public override void _Ready()
    {
        _ring = GetNodeOrNull<GpuParticles3D>("Ring");
        _balls = GetNodeOrNull<GpuParticles3D>("Balls");
    }

    /// <summary>Place the emitter frame (Y = the ground normal) at `pos` and open / close the two emitters.</summary>
    public void Set(Vector3 pos, Vector3 normal, bool ring, bool balls)
    {
        if (normal.LengthSquared() < 1e-6f) normal = Vector3.Up;
        normal = normal.Normalized();
        Vector3 fwd = Vector3.Right;                                     // the game's fixed (1, 0, 0)
        Vector3 b = fwd.Cross(normal);
        if (b.LengthSquared() < 1e-6f) b = Vector3.Forward.Cross(normal);
        b = b.Normalized();
        Vector3 c = normal.Cross(b).Normalized();
        GlobalTransform = new Transform3D(new Basis(c, normal, b), pos);
        if (_ring != null && _ring.Emitting != ring) _ring.Emitting = ring;
        if (_balls != null && _balls.Emitting != balls) _balls.Emitting = balls;
    }

    /// <summary>Close both emitters (the live particles finish on their own).</summary>
    public void Off()
    {
        if (_ring != null && _ring.Emitting) _ring.Emitting = false;
        if (_balls != null && _balls.Emitting) _balls.Emitting = false;
    }

    public bool RingOn => _ring != null && _ring.Emitting;
    public bool BallsOn => _balls != null && _balls.Emitting;
}
