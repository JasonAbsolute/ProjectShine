using Godot;

/// <summary>
/// The game's waist lean: MarioWaistCtrl (MarioDraw.cpp 470), the joint callback that considerWaist() drives every tick -- a forward
/// pitch by speed and a bank by turn rate, MsMtxSetRotRPH(roll, 0, pitch) = Rz(pitch) x Rx(roll), concatenated onto the joint's matrix
/// so that its CHILDREN take it (J3D stores a joint's own matrix before its callback). The joint is chn_chest: the constant, skinless,
/// zero-offset joint above the pelvis whose only child is jnt_chest -- the whole upper body. (The decomp names the two joint-id fields
/// the other way round; the shipped data -- the FLUDD's setBaseTRMtx tilt formula, the MtxEffect14/16/22 files -- says the field it
/// calls mJointIdChnChest is jnt_chest, so the waist field is chn_chest.) chn_chest's frame is X = his back, Y = up, Z = his left, so
/// Rz(pitch) tips the upper body forward about the lateral axis and Rx(roll) banks it about the front-back axis: a positive roll --
/// a left turn -- tilts it toward Z, his left, into the turn. Because chn_chest never moves with the stride, the pitch axis is fixed:
/// no sway from the clip's shoulder twist.
/// Only while the body animation is RUN1 / RUN2 (<see cref="Apply"/>). Child of Mario's Skeleton3D, ordered after the legacy
/// modifiers and before SmsTimeLag; Mario writes <see cref="Pitch"/> / <see cref="Roll"/> (radians, the game's signs) each frame.
/// </summary>
public partial class SmsWaistLean : SkeletonModifier3D
{
    public string ParentBoneName = "chn_chest"; // the joint the callback sits on: its frame is the frame of the rotation
    public string BoneName = "jnt_chest";       // its child: the whole upper body
    public float Pitch;  // mWaistPitch: about chn_chest's local Z (his lateral axis); positive = forward
    public float Roll;   // mWaistRoll: about chn_chest's local X (his front-back axis); positive = toward his left = into a left turn
    public bool Apply;   // MarioWaistCtrl's ANIM_RUN1 / RUN2 condition

    private int _bone = -1;
    private int _parent = -1;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        _bone = skel != null ? skel.FindBone(BoneName) : -1;
        _parent = skel != null ? skel.FindBone(ParentBoneName) : -1;
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null || _bone < 0 || !Apply)
            return;
        if (Mathf.Abs(Pitch) < 1e-5f && Mathf.Abs(Roll) < 1e-5f)
            return;
        // MTXConcat(chn_chest world, Rz(pitch) x Rx(roll)) then the child's local: the child's local pose is PRE-multiplied by R in the
        // parent's frame (jnt_chest's origin is the parent's origin, so only its rotation changes).
        Quaternion r = new Quaternion(Vector3.Back, Pitch) * new Quaternion(Vector3.Right, Roll);
        if (_parent >= 0 && skel.GetBoneParent(_bone) != _parent)
        {
            // the rig's hierarchy differs from the BMD's: fall back to the child's own frame (X up, Y front, Z left)
            skel.SetBonePoseRotation(_bone, skel.GetBonePoseRotation(_bone) * new Quaternion(Vector3.Back, Pitch) * new Quaternion(Vector3.Right, Roll));
            return;
        }
        skel.SetBonePoseRotation(_bone, r * skel.GetBonePoseRotation(_bone));
        skel.SetBonePosePosition(_bone, r * skel.GetBonePosePosition(_bone));
    }
}
