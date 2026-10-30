using Godot;

/// <summary>
/// SMS pass 129 -- SamboHeadRollCallback (hanasambo.cpp): a J3D joint callback on the Pokey Head's body joint. After the clip has posed the joint,
/// the game turns it about the SIDE axis (up x velocity, written in the joint's own axes) by the head's roll angle (unk1AC, degrees: the lean
/// into a hop, clamped to JumpAngY) -- MTXConcat(anmMtx, rot, anmMtx), a post-multiply = a turn about an axis given in the joint's own frame.
/// Only while the head attacks, is knocked by water or recovers (isUseCallBack); the owner sets <see cref="Degrees"/> every physics frame and
/// <see cref="WorldAxis"/> to up x velocity. A SkeletonModifier3D runs after the AnimationPlayer has written the pose, every render frame.
/// </summary>
[GlobalClass]
public partial class SmsBoneRoll : SkeletonModifier3D
{
    /// <summary>The bone the turn is applied to (the head's 'jnt_body', mBodyJntIndex).</summary>
    [Export] public string BoneName = "jnt_body";
    /// <summary>The world-space axis of the turn (the owner writes up x velocity).</summary>
    public Vector3 WorldAxis = Vector3.Right;
    /// <summary>The turn, degrees (0 = the clip's pose untouched).</summary>
    public float Degrees;

    public override void _ProcessModification()
    {
        if (Mathf.Abs(Degrees) < 1e-3f) return;
        var skel = GetSkeleton();
        if (skel == null) return;
        int b = skel.FindBone(BoneName);
        if (b < 0) return;
        Vector3 axis = WorldAxis;
        if (axis.LengthSquared() < 1e-8f) return;
        // the world axis in the joint's own frame: (skeleton world basis x the bone's global pose basis)^-1 x axis
        Basis boneWorld = (skel.GlobalTransform.Basis * skel.GetBoneGlobalPose(b).Basis).Orthonormalized();
        Vector3 local = (boneWorld.Inverse() * axis).Normalized();
        Quaternion pose = skel.GetBonePoseRotation(b);
        skel.SetBonePoseRotation(b, (pose * new Quaternion(local, Mathf.DegToRad(Degrees))).Normalized());
    }
}
