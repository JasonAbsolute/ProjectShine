using Godot;

/// <summary>
/// The game's UNBOUND emitters -- emitParticle(id, &amp;pos) with no bind: a burst that stays where it was emitted while Mario moves on.
/// The Fx scenes are instanced under Mario, so a GpuParticles3D that emits in place rides along with him: its particles are world space,
/// but ParticleProcessMaterial reads the node's CURRENT transform every frame for the radial / orbit pivot and the directional frame
/// (process_radial_displacement: normalize(pos - emission_transform * velocity_pivot)), so a knock-back or a run-off after a landing
/// steers the whole burst after him ("when mario gets hit from an enemy the stars spawn too far away from him", Jaysonn 2026-09-30:
/// the stars were being pushed away from his receding position). <see cref="Spawn"/> copies the template as a top-level one-shot at
/// the given transform and frees it when its particles are gone; the template itself never emits in the game, so a second burst
/// (a bonk's landing, a ground hit's second one) never cuts the first short either.
/// </summary>
public static class SmsUnboundBurst
{
    /// <summary><paramref name="configure"/> runs on the copy before it emits -- for a variant of the template (fewer stars, a shorter life).</summary>
    public static GpuParticles3D Spawn(GpuParticles3D template, Node parent, Transform3D at, System.Action<GpuParticles3D> configure = null)
    {
        if (template == null || parent == null)
            return null;
        var burst = (GpuParticles3D)template.Duplicate(); // the material (and any ready-time edits to it) is shared, not copied
        burst.TopLevel = true;
        burst.OneShot = true;
        burst.Emitting = false;
        configure?.Invoke(burst);
        parent.AddChild(burst);
        burst.GlobalTransform = at;
        burst.Finished += burst.QueueFree;
        burst.Restart();
        return burst;
    }

    /// <summary>A world-aligned burst at <paramref name="at"/> (the usual case: the game's emitters here are unrotated).</summary>
    public static GpuParticles3D Spawn(GpuParticles3D template, Node parent, Vector3 at, System.Action<GpuParticles3D> configure = null) => Spawn(template, parent, new Transform3D(Basis.Identity, at), configure);
}
