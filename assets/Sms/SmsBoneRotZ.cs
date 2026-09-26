using Godot;

/// <summary>
/// A rotation about a bone's local Z added after the clip has posed it -- the game's joint callbacks
/// (Watergun.cpp: WaterGunDivingCtrlL / R on the hover nozzle's jnt_nozzle_L / R: MTXConcat(anmMtx, RotZ(angle))).
/// Runs in the skeleton's modifier stage like HeadForwardLock, so the AnimationPlayer's pose does not overwrite it.
/// Child of the nozzle's Skeleton3D; Mario writes <see cref="Angles"/> (radians, one per bone) every physics frame.
/// </summary>
public partial class SmsBoneRotZ : SkeletonModifier3D
{
    public string[] BoneNames = System.Array.Empty<string>();
    public float[] Angles = System.Array.Empty<float>();

    private int[] _bones;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        _bones = new int[BoneNames.Length];
        for (int i = 0; i < BoneNames.Length; i++)
            _bones[i] = skel != null ? skel.FindBone(BoneNames[i]) : -1;
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null || _bones == null)
            return;
        for (int i = 0; i < _bones.Length && i < Angles.Length; i++)
        {
            if (_bones[i] < 0 || Mathf.Abs(Angles[i]) < 1e-5f)
                continue;
            Quaternion cur = skel.GetBonePoseRotation(_bones[i]);
            skel.SetBonePoseRotation(_bones[i], cur * new Quaternion(new Vector3(0f, 0f, 1f), Angles[i]));
        }
    }
}
