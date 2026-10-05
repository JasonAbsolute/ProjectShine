using Godot;

/// <summary>
/// The Gooble's death splash: TNameKuri::setDeadAnm / setMeltAnm (namekuri.cpp 490-545) emitAndBindToMtxPtr PARTICLE_MS_DEADNAMEKLI_O
/// (16 dark goop blobs off a ring) and _N (28 rainbow water drops off a sphere) on the jnt_brain joint's matrix, scaled by the body
/// (setGlobalScale(mScaling)). The scene assets/Sms/Fx/GoobleDeath.tscn (gen_fx_gooble.py) holds both emitters in local coordinates:
/// <see cref="Play"/> puts the node in the joint's frame at the body's scale -- the game's emitter axes are the joint's (X up, Y forward,
/// Z right) -- and turns each emitter's gravity, authored straight down, into that frame, since the game's fields are in world space.
/// </summary>
public partial class SmsGoobleDeath : Node3D
{
    [Export] public GpuParticles3D Goop;
    [Export] public GpuParticles3D Drops;

    private float _goopG, _dropsG;

    public override void _Ready()
    {
        TopLevel = true; // emitted into the world where the body died, not carried by the Gooble's re-launch
        if (Goop == null || Drops == null)
        {
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Goop / Drops exports are not both set (see assets/Sms/Fx/GoobleDeath.tscn)");
            return;
        }
        _goopG = Prepare(Goop);
        _dropsG = Prepare(Drops);
    }

    /// <summary>Its own process material (the gravity is set per Play), one-shot, quiet; returns the authored gravity's strength.</summary>
    private static float Prepare(GpuParticles3D p)
    {
        p.OneShot = true;
        p.Emitting = false;
        if (p.ProcessMaterial is ParticleProcessMaterial mat)
        {
            var own = (ParticleProcessMaterial)mat.Duplicate();
            p.ProcessMaterial = own;
            return own.Gravity.Length();
        }
        return 0f;
    }

    /// <summary>The joint's frame (rotation and position; the scale comes from the body) -- the game binds the emitter to anmMtx(2) and
    /// sets its global scale to mScaling.</summary>
    public void Play(Transform3D jointFrame, float bodyScale)
    {
        Basis rot = jointFrame.Basis.Orthonormalized();
        GlobalTransform = new Transform3D(rot * Basis.FromScale(Vector3.One * Mathf.Max(bodyScale, 0.01f)), jointFrame.Origin);
        Basis inv = rot.Inverse();
        Aim(Goop, inv, _goopG);
        Aim(Drops, inv, _dropsG);
        Goop?.Restart();
        Drops?.Restart();
    }

    private static void Aim(GpuParticles3D p, Basis inv, float g)
    {
        if (p?.ProcessMaterial is ParticleProcessMaterial mat)
            mat.Gravity = inv * new Vector3(0f, -g, 0f);
    }
}
