using Godot;

/// <summary>
/// Skeleton modifier that holds a bone (Mario's head, "jnt_head") toward its rest
/// orientation, cancelling the brisk side-to-side head turn baked into the
/// run/walk clips. It runs in the skeleton's modifier stage — AFTER the
/// AnimationTree writes the pose — so the override actually sticks instead of
/// being overwritten by the animation each frame.
///
/// <see cref="Weight"/> is driven externally by Mario (0 = fully animated head,
/// 1 = held at rest/forward), eased in only while he's in a locomotion state.
/// </summary>
public partial class HeadForwardLock : SkeletonModifier3D
{
    public string BoneName = "jnt_head";
    public float Weight;

    // Slight upward tilt so he looks ahead, not at the ground, while the lean is
    // pitching his torso forward. Axis is in the bone's local frame (rig-specific).
    public float LookUpDegrees;
    public Vector3 LookUpAxisLocal = new(1f, 0f, 0f);

    private int _bone = -1;
    private Quaternion _restRot = Quaternion.Identity;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        if (skel == null)
            return;
        _bone = skel.FindBone(BoneName);
        if (_bone >= 0)
            _restRot = skel.GetBoneRest(_bone).Basis.GetRotationQuaternion();
    }

    public override void _ProcessModification()
    {
        if (_bone < 0 || Weight <= 0.0001f)
            return;
        var skel = GetSkeleton();
        if (skel == null)
            return;

        // Rest orientation, plus a slight upward tilt.
        Quaternion target = _restRot;
        if (Mathf.Abs(LookUpDegrees) > 0.001f && LookUpAxisLocal.LengthSquared() > 0.0001f)
            target = _restRot * new Quaternion(LookUpAxisLocal.Normalized(), Mathf.DegToRad(LookUpDegrees));

        Quaternion cur = skel.GetBonePoseRotation(_bone);
        skel.SetBonePoseRotation(_bone, cur.Slerp(target, Mathf.Clamp(Weight, 0f, 1f)));
    }
}
