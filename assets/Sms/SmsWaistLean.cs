using Godot;

/// <summary>
/// The game's waist lean (MarioDraw.cpp MarioWaistCtrl, the joint callback on jnt_chest), with J3D's semantics: a joint callback runs
/// AFTER the joint's own matrix has been computed and stored (J3DMtxCalcBasic::recursiveCalc: calcIn -> setAnmMtx, then callBack(node, 0),
/// then the children), so MTXConcat(mCurrentMtx, Rz(pitch) x Rx(roll)) changes only what the CHILDREN inherit -- jnt_head (the head meshes
/// hang on its zero-offset child M_head), jnt_sldr_L and jnt_sldr_R (the arms, and the torso vertices weighted to the shoulders) -- while
/// the vertices skinned to jnt_chest / jnt_waist keep the clip's pose. The chest joint sits at the pelvis (bind (0, 58, 0)); the
/// shoulders are 30 and the head 39 units up its X, so the lean pivots the shoulder girdle and the head about the hips: the hunched
/// sprint, not a bowed torso.
/// Ported literally: for each child bone c of jnt_chest, pose_c = R x pose_c (origin and rotation), R = Rz(pitch) x Rx(roll) in
/// jnt_chest's animated frame (X up the spine, Y the front, Z lateral -- the BMD's axes, which the rig keeps).
/// Only while the body animation is RUN1 / RUN2 (<see cref="Apply"/>). Child of Mario's Skeleton3D, first among the modifiers;
/// Mario writes <see cref="Pitch"/> / <see cref="Roll"/> (radians) each frame.
/// </summary>
public partial class SmsWaistLean : SkeletonModifier3D
{
    public string BoneName = "jnt_chest";
    public float Pitch;  // mWaistPitch: about the joint's local Z (lateral); positive = the forward lean of the free run
    public float Roll;   // mWaistRoll: about the joint's local X (up the spine); positive = counterclockwise from above = into a left turn
    public bool Apply;   // MarioWaistCtrl's ANIM_RUN1 / RUN2 condition

    private int _bone = -1;
    private int[] _children = System.Array.Empty<int>();

    public int ChildCount => _children.Length;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        _bone = skel != null ? skel.FindBone(BoneName) : -1;
        _children = skel != null && _bone >= 0 ? skel.GetBoneChildren(_bone) : System.Array.Empty<int>();
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null || _bone < 0 || !Apply || _children.Length == 0)
            return;
        if (Mathf.Abs(Pitch) < 1e-5f && Mathf.Abs(Roll) < 1e-5f)
            return;
        // MsMtxSetRotRPH(roll, 0, pitch) = Rz(pitch) x Rx(roll) (MathUtil.cpp 226), concatenated onto the chest's matrix: the children's
        // local transforms are pre-multiplied by it -- their origins swing about the chest joint as well as their axes.
        Quaternion r = new Quaternion(Vector3.Back, Pitch) * new Quaternion(Vector3.Right, Roll);
        foreach (int c in _children)
        {
            skel.SetBonePosePosition(c, r * skel.GetBonePosePosition(c));
            skel.SetBonePoseRotation(c, r * skel.GetBonePoseRotation(c));
        }
    }
}
