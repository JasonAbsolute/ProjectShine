using Godot;

/// <summary>
/// The game's waist lean (MarioDraw.cpp MarioWaistCtrl, the joint callback on jnt_chest): considerWaist() chases a forward pitch by
/// speed and a twist by turn rate every tick, and the callback concatenates Rz(pitch) x Rx(roll) onto the chest's matrix in the
/// chest's own frame (X up the spine, Y the front, Z his left -- the BMD's axes, which the rig keeps).
///
/// In J3D that concatenation happens AFTER the joint's own matrix has been stored (J3DMtxCalcBasic::recursiveCalc: calcIn ->
/// setAnmMtx, then callBack(node, 0), then the children), so in the game only the CHILDREN -- jnt_head, jnt_sldr_L, jnt_sldr_R and
/// everything under them -- take the lean; the chest's own skin keeps the clip and the game's smooth chest/shoulder envelopes bend
/// between. This rig's shirt is rigid on jnt_chest (138 of 239 vertices; two to four chest/shoulder blends), so the literal version
/// (ad0ed9e..16ccd6d) tore the shoulders off the chest into a hunch ("really off-putting", Jaysonn, 2026-09-27 04:29). DELIBERATE
/// DEVIATION in skinning only: the rotation is applied to the chest BONE (cur x R), which carries head, shoulders and arms to exactly
/// the same places the game's arithmetic puts them and leans the torso skin with them as one piece.
///
/// Only while the body animation is RUN1 / RUN2 (<see cref="Apply"/>). Child of Mario's Skeleton3D, ordered last among the modifiers
/// (the JiggleBones reset jnt_head / jnt_sldr positions to rest + shift each frame; last, the lean carries whatever they left).
/// Mario writes <see cref="Pitch"/> / <see cref="Roll"/> (radians) each frame.
/// </summary>
public partial class SmsWaistLean : SkeletonModifier3D
{
    public string BoneName = "jnt_chest";
    public float Pitch;  // mWaistPitch: about the joint's local Z (lateral); positive = the forward lean of the free run
    public float Roll;   // mWaistRoll as APPLIED: about the joint's local X (up the spine); Mario negates the game's value (see SmsWaistApply)
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
        if (skel == null || _bone < 0 || !Apply)
            return;
        if (Mathf.Abs(Pitch) < 1e-5f && Mathf.Abs(Roll) < 1e-5f)
            return;
        // MsMtxSetRotRPH(roll, 0, pitch) = Rz(pitch) x Rx(roll) (MathUtil.cpp 226): the twist about the spine first, then the pitch
        // about the lateral axis, in the chest's animated frame -- post-multiplied, as MTXConcat(mCurrentMtx, transform) does.
        Quaternion r = new Quaternion(Vector3.Back, Pitch) * new Quaternion(Vector3.Right, Roll);
        skel.SetBonePoseRotation(_bone, skel.GetBonePoseRotation(_bone) * r);
    }
}
