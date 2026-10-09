using Godot;

/// <summary>
/// PARTICLE_MS_ENM_WATHIT -- the mark FLUDD's water leaves on an actor it hits (ms_enm_wathit.jpa, Particles.hpp 0xE7: one 125-unit cartoon
/// splat per hit, white-yellow to orange over 12 frames, growing to 2.25 x, a random angle). The game emits it unbound, one per hit
/// (gpMarioParticleManager->emit(id, pos, 0, nullptr)): TSmallEnemy::receiveMessage(HIT_MESSAGE_SPRAYED_BY_WATER) at the water hit actor's
/// position -- the drop that hit (Gooble.cs) -- and TBaseNPC::behaveToHitObject_ 351 while BURNING at the NPC's own mPosition (SmsNpc.cs; a plain
/// or washing NPC emits nothing, the game's TSplashManager splash there is not ported). One scene (assets/Sms/Fx/WaterHitMark.tscn, gen_fx_npc.py
/// wathit()) holds a POOL of one-particle one-shot emitters; <see cref="Emit"/> restarts the next one at the hit -- the spray lands tens of drops
/// a second, a node spawned per hit would churn the renderer. The scene lives once under the tree root, made by <see cref="Ensure"/> from an
/// owner's _Ready so its shader compiles with the level, not at the first hit (the pass-77 rule).
/// </summary>
public partial class SmsWaterHitMarks : Node3D
{
    public const string ScenePath = "res://assets/Sms/Fx/WaterHitMark.tscn";

    private static SmsWaterHitMarks _live;
    private static PackedScene _scene;
    private readonly System.Collections.Generic.List<GpuParticles3D> _pool = new();
    private int _next;

    /// <summary>Every Emit so far and where the last one went (a headless drive reads them; GPU particles do not run headless).</summary>
    public static int EmitCount;
    public static Vector3 LastAt;

    public static SmsWaterHitMarks Live => GodotObject.IsInstanceValid(_live) ? _live : null;
    public int PoolSize => _pool.Count;
    public int NextIndex => _next;

    public override void _Ready()
    {
        TopLevel = true;                      // world space whatever it hangs under
        GlobalTransform = Transform3D.Identity;
        _pool.Clear();
        foreach (var c in GetChildren())
            if (c is GpuParticles3D p) { p.Emitting = false; p.OneShot = true; _pool.Add(p); }
    }

    /// <summary>The pool, made once and hung under the tree root (deferred: an owner's _Ready runs while its parent is still adding children).</summary>
    public static SmsWaterHitMarks Ensure(Node from)
    {
        if (GodotObject.IsInstanceValid(_live)) return _live;
        if (from == null || !from.IsInsideTree()) return null;
        _scene ??= GD.Load<PackedScene>(ScenePath);
        if (_scene == null) { GD.PushWarning($"[SMS fx] {ScenePath} is missing -- no water-hit marks"); return null; }
        _live = _scene.Instantiate<SmsWaterHitMarks>();
        from.GetTree().Root.CallDeferred(Node.MethodName.AddChild, _live);
        return _live;
    }

    /// <summary>One mark at <paramref name="at"/> (world metres) -- the game's one emit per water hit.</summary>
    public static void Emit(Node from, Vector3 at)
    {
        var live = Ensure(from);
        EmitCount++; LastAt = at;
        if (live == null || !live.IsInsideTree()) return;      // counted, not drawn: the pool is still on its way into the tree
        live.Fire(at);
    }

    private void Fire(Vector3 at)
    {
        if (_pool.Count == 0) return;
        var p = _pool[_next];
        _next = (_next + 1) % _pool.Count;
        p.GlobalPosition = at;
        p.Restart();                            // a one-shot restarted = one new particle at the new spot (the previous one of this slot is cut)
    }
}
