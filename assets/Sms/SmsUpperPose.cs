using Godot;

/// <summary>
/// addUpper() while the AnimationTree is off. The game grafts the upper-body pump clip (gMarioAnimeData[anim].unk2 --
/// ma_swpmp for every swim clip) onto the joints from chn_chest down (M3UModelMario's second animation root) over whatever
/// the body clip does. The tree's CarryBlend does that for the tree's clips; the swim clips run on the AnimationPlayer
/// directly, so this modifier samples the pump clip at <see cref="Time"/> and writes the chn_chest subtree after the
/// AnimationPlayer has posed the body. Child of Mario's Skeleton3D, placed first so the head jiggle / head lock modifiers add
/// their bit on top. Weight 0 = off.
/// </summary>
public partial class SmsUpperPose : SkeletonModifier3D
{
    public float Weight;
    public double Time;

    private Animation _clip;
    private int[] _bones = System.Array.Empty<int>();
    private int[] _rot = System.Array.Empty<int>();
    private int[] _pos = System.Array.Empty<int>();

    public int BoneCount => _bones.Length;

    /// <summary>Bind the clip's bone tracks for the bones at and under <paramref name="rootBone"/>.</summary>
    public void Bind(Skeleton3D skel, Animation clip, string rootBone)
    {
        _clip = clip;
        var bones = new System.Collections.Generic.List<int>();
        var rot = new System.Collections.Generic.List<int>();
        var pos = new System.Collections.Generic.List<int>();
        int root = skel.FindBone(rootBone);
        if (root < 0 || clip == null)
        {
            _bones = System.Array.Empty<int>();
            return;
        }
        for (int b = 0; b < skel.GetBoneCount(); b++)
        {
            bool under = false;
            for (int p = b; p >= 0; p = skel.GetBoneParent(p))
            {
                if (p == root)
                {
                    under = true;
                    break;
                }
            }
            if (!under)
                continue;
            string bn = skel.GetBoneName(b);
            int r = -1, t = -1;
            for (int k = 0; k < clip.GetTrackCount(); k++)
            {
                NodePath path = clip.TrackGetPath(k);
                if (path.GetSubNameCount() == 0 || (string)path.GetSubName(0) != bn)
                    continue;
                Animation.TrackType type = clip.TrackGetType(k);
                if (type == Animation.TrackType.Rotation3D)
                    r = k;
                else if (type == Animation.TrackType.Position3D)
                    t = k;
            }
            if (r < 0 && t < 0)
                continue;
            bones.Add(b);
            rot.Add(r);
            pos.Add(t);
        }
        _bones = bones.ToArray();
        _rot = rot.ToArray();
        _pos = pos.ToArray();
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        if (Weight <= 0.0001f || _clip == null)
            return;
        var skel = GetSkeleton();
        if (skel == null)
            return;
        float w = Mathf.Clamp(Weight, 0f, 1f);
        for (int i = 0; i < _bones.Length; i++)
        {
            int b = _bones[i];
            if (_rot[i] >= 0)
            {
                Quaternion q = _clip.RotationTrackInterpolate(_rot[i], Time);
                skel.SetBonePoseRotation(b, w >= 1f ? q : skel.GetBonePoseRotation(b).Slerp(q, w));
            }
            if (_pos[i] >= 0)
            {
                Vector3 p = _clip.PositionTrackInterpolate(_pos[i], Time);
                skel.SetBonePosePosition(b, w >= 1f ? p : skel.GetBonePosePosition(b).Lerp(p, w));
            }
        }
    }
}
