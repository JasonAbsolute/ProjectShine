using Godot;

/// <summary>
/// TBGKMtxCalc::calc (Enemy/gatekeeper.cpp 243-290; pass 126c): the gatekeeper's root joint carries the tracking yaw (joint 0's matrix x RotY(unk180)), but joint
/// 11 -- centnull1, the parent of the six bodynull rings that skin the goop MOUND -- is overwritten every frame with the actor's own frame
/// (MsMtxSetXYZRPH(mPosition, mRotation)): the stalk and head swing round toward Mario, the mound never turns. The stalk's chain (bodyjnt1..5, headnull1,
/// kutijnt1/2) and the mound's rings share no joints (gk_skin.py), and neither joint 0 nor joint 11 is animated by any clip.
/// Here the Model node carries the yaw (SmsGateKeeper.TrackYaw) and this modifier sets centnull1's LOCAL pose ABSOLUTELY to inverse(parent's global) x
/// RotY(-yaw): the base's world frame = the actor's, whatever the clips do above it. Absolute, so it cannot stack across frames (the lesson of pass 9).
/// </summary>
[GlobalClass]
public partial class SmsGateKeeperBasePin : SkeletonModifier3D
{
    /// <summary>The joint pinned to the actor frame (the game's joint 11).</summary>
    [Export] public string BaseBoneName = "centnull1";
    /// <summary>The tracking yaw the Model node carries (degrees); the base is turned back by it.</summary>
    public float YawDeg;
    /// <summary>The base joint's global pose (skeleton space) as this modifier left it, and how many times it has run. Since Godot 4.3 the skeleton RESTORES the
    /// unmodified poses after the skin update, so GetBoneGlobalPose from outside returns the animation's pose, not the pinned one -- read these instead.</summary>
    public Transform3D LastBaseGlobal = Transform3D.Identity;
    public int Runs;

    private int _bone = -2;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null) return;
        if (_bone == -2) _bone = skel.FindBone(BaseBoneName);
        if (_bone < 0) return;
        int parent = skel.GetBoneParent(_bone);
        Transform3D parentGlobal = parent >= 0 ? skel.GetBoneGlobalPose(parent) : Transform3D.Identity;
        Transform3D want = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(-YawDeg)), Vector3.Zero);   // the actor frame, in skeleton space
        Transform3D local = parentGlobal.AffineInverse() * want;
        skel.SetBonePosePosition(_bone, local.Origin);
        skel.SetBonePoseRotation(_bone, local.Basis.GetRotationQuaternion());
        skel.SetBonePoseScale(_bone, local.Basis.Scale);
        LastBaseGlobal = skel.GetBoneGlobalPose(_bone);
        Runs++;
    }
}
