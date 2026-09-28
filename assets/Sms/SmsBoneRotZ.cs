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

    /// <summary>The bone's global pose as DRAWN: the clip's pose with this modifier's rotations re-applied down the chain. For
    /// readers outside the modifier pass -- Godot 4.3+ puts the unmodified poses back after the skin update, so there
    /// <c>GetBoneGlobalPose()</c> is the clip's alone, while the mesh, and every BoneAttachment3D, show the modified one.</summary>
    public Transform3D DrawnGlobalPose(Skeleton3D skel, int bone)
    {
        if (skel == null || bone < 0)
            return Transform3D.Identity;
        var chain = new System.Collections.Generic.List<int>();
        for (int b = bone; b >= 0; b = skel.GetBoneParent(b))
            chain.Add(b);
        Transform3D drawn = Transform3D.Identity, parentClip = Transform3D.Identity;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            int b = chain[i];
            Transform3D clip = skel.GetBoneGlobalPose(b);
            Transform3D local = parentClip.AffineInverse() * clip;
            int k = _bones != null ? System.Array.IndexOf(_bones, b) : -1;
            if (k >= 0 && k < Angles.Length && Mathf.Abs(Angles[k]) >= 1e-5f)
                local = new Transform3D(local.Basis * new Basis(new Quaternion(new Vector3(0f, 0f, 1f), Angles[k])), local.Origin);
            drawn *= local;
            parentClip = clip;
        }
        return drawn;
    }
}
