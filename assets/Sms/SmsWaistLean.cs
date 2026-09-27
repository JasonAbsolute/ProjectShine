using Godot;

/// <summary>
/// The game's waist lean (MarioDraw.cpp MarioWaistCtrl, the callback on jnt_chest): after the clip has posed the chest, the joint
/// is turned by MsMtxSetRotRPH(roll, 0, pitch) = Rz(pitch) x Rx(roll) in its own frame -- the forward lean by speed and the roll
/// into turns that considerWaist() chases every tick. Only while the body animation is RUN1 / RUN2 (<see cref="Apply"/>).
/// Child of Mario's Skeleton3D, first among the modifiers; Mario writes <see cref="Pitch"/> / <see cref="Roll"/> (radians) each frame.
/// </summary>
public partial class SmsWaistLean : SkeletonModifier3D
{
    public string BoneName = "jnt_chest";
    public float Pitch;  // mWaistPitch: about the joint's local Z (positive = the forward lean of the free run)
    public float Roll;   // mWaistRoll: about the joint's local X (the lean into a turn)
    public bool Apply;   // MarioWaistCtrl's ANIM_RUN1 / RUN2 condition

    private int _bone = -1;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        _bone = skel != null ? skel.FindBone(BoneName) : -1;
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null || _bone < 0 || !Apply)
            return;
        if (Mathf.Abs(Pitch) < 1e-5f && Mathf.Abs(Roll) < 1e-5f)
            return;
        Quaternion cur = skel.GetBonePoseRotation(_bone);
        skel.SetBonePoseRotation(_bone, cur * new Quaternion(Vector3.Back, Pitch) * new Quaternion(Vector3.Right, Roll));
    }
}
