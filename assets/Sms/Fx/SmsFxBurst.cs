using Godot;

/// <summary>
/// A one-shot particle burst scene with any number of GPUParticles3D children (the small enemies' death puffs, the wall crash): the game's
/// emit(id, &amp;pos) with no bind -- the burst stays where it was fired. _Ready pins the node to the world (TopLevel), restarts every child
/// one-shot and frees the node once the longest child has finished. The spawner sets the transform (position, the body's yaw, the body's
/// scale = the game's setGlobalScale) before AddChild, or calls <see cref="Play"/> after it to place and restart in one.
/// </summary>
public partial class SmsFxBurst : Node3D
{
    private int _running;
    private bool _wired;

    public override void _Ready()
    {
        TopLevel = true;
        Fire();
    }

    /// <summary>At <paramref name="at"/> (its basis = the emitter's rotation x the body's scale), restarted.</summary>
    public void Play(Transform3D at)
    {
        GlobalTransform = at;
        Fire();
    }

    /// <summary>A shader parameter on every child's draw-pass material that declares it (the splat's goop colour).</summary>
    public void SetShaderParam(string name, Variant value)
    {
        foreach (Node n in GetChildren())
        {
            if (n is not GpuParticles3D p || p.DrawPass1 is not Mesh mesh) continue;
            for (int i = 0; i < mesh.GetSurfaceCount(); i++)
                if (mesh.SurfaceGetMaterial(i) is ShaderMaterial sm && sm.Shader != null)
                    foreach (Variant u in sm.Shader.GetShaderUniformList())
                        if (u.AsGodotDictionary()["name"].AsString() == name) { sm.SetShaderParameter(name, value); break; }
        }
    }

    private void Fire()
    {
        _running = 0;
        foreach (Node n in GetChildren())
        {
            if (n is ISmsFxBurstPart part)                      // pass 130b: an animated model riding the burst (the Bob-omb's fireball), restarted and counted like an emitter
            {
                _running++;
                if (!_wired) part.Finished += () => { if (--_running <= 0) Done(); };
                part.Restart();
                continue;
            }
            if (n is not GpuParticles3D p) continue;
            p.OneShot = true;
            p.Emitting = false;
            _running++;
            if (!_wired) p.Finished += () => { if (--_running <= 0) Done(); };
            // pass 131: a JPA emitter's start frame (the Pokey's sink rocks come 40 frames into the sink) -- `metadata/sms_delay_frames` on the child
            int delay = p.HasMeta("sms_delay_frames") ? p.GetMeta("sms_delay_frames").AsInt32() : 0;
            if (delay > 0 && IsInsideTree())
            {
                var pp = p;
                GetTree().CreateTimer(delay / 60.0).Timeout += () => { if (IsInstanceValid(pp)) pp.Restart(); };
            }
            else p.Restart();
        }
        _wired = true;
        if (_running == 0) Done();
    }

    /// <summary>The last counted child finished: freed now -- or, when a child drives a sub-emitter (the Bob-omb scraps' smoke trail, a nested
    /// GPUParticles3D the burst neither restarts nor counts), after that target's lifetime, so its last puffs are not cut short (pass 130b).</summary>
    private void Done()
    {
        double tail = 0.0;
        foreach (Node n in GetChildren())
            if (n is GpuParticles3D p && !p.SubEmitter.IsEmpty && GetNodeOrNull<GpuParticles3D>(p.SubEmitter) is GpuParticles3D target)
                tail = System.Math.Max(tail, target.Lifetime);
        if (tail > 0.0 && IsInsideTree()) GetTree().CreateTimer(tail).Timeout += QueueFree;
        else QueueFree();
    }

    /// <summary>For the drives: the children still emitting.</summary>
    public int Running => _running;
}

/// <summary>A burst child that is not a GPUParticles3D but plays along with the emitters (pass 130b: the Bob-omb's 10_bomb fireball model): Fire restarts it
/// and keeps the burst alive until it raises <see cref="Finished"/>.</summary>
public interface ISmsFxBurstPart
{
    void Restart();
    event System.Action Finished;
}
