using Godot;
using System.Collections.Generic;

/// <summary>
/// SMS pass 163: KING BOO'S TONGUE A BIT LONGER -- Jaysonn: the re-aimed tongue (162's FABRIK onto Mario) "doesn't look very good ... if anything we just
/// make the tongue stick out more in general, not much but a bit". So the tongue is the clips' own again (the roll's whirl IS the wet whip), stretched:
/// a SkeletonModifier3D under his Skeleton3D, as Mario's lasso arm is, that after the clip has posed him scales each tongue joint's pose position
/// (jnt_sita_1 -> 2 -> 3 -> 4 -> eff_sita) by <see cref="Stretch"/> -- every segment that much longer, the root that much further out of the mouth, the
/// mesh following its bones. Weight blends it in over BlendSeconds when <see cref="On"/>. The joints' places in the world AFTER the pose are
/// <see cref="Points"/>, read in the same pass (a Skeleton3D restores the clip's poses once the frame is drawn): the lick's hit on Mario is tested on them.
/// </summary>
public partial class SmsTongueStretch : SkeletonModifier3D
{
    public bool On;
    public float BlendSeconds = 0.3f;
    /// <summary>The tongue's length as a multiple of the clips' (1 = as they are; 1.15 = a bit longer).</summary>
    public float Stretch = 1.15f;
    public bool Debug;
    public float Weight { get; private set; }
    public bool Bound => _bones.Count >= 2;
    /// <summary>The tongue joints after the pose (world), root first, tip last; the chain's length in the world (metres); the root and the tip.</summary>
    public Vector3[] Points { get; private set; } = System.Array.Empty<Vector3>();
    public bool PointsValid { get; private set; }
    public float Reach { get; private set; }
    public Vector3 RootPoint { get; private set; }
    public Vector3 TipPoint { get; private set; }
    public int Passes { get; private set; }

    private readonly List<int> _bones = new(); private int _dbg;

    public override void _Ready()
    {
        Active = true; Influence = 1f;
        var skel = GetSkeleton();
        if (skel == null) return;
        foreach (string name in new[] { "jnt_sita_1", "jnt_sita_2", "jnt_sita_3", "jnt_sita_4", "eff_sita" }) { int b = skel.FindBone(name); if (b >= 0) _bones.Add(b); }
        if (!Bound) GD.PushWarning($"[SMS tongue] {GetPath()}: no jnt_sita_1..4 / eff_sita on the skeleton -- the tongue stays as the clips have it");
        else if (Debug) GD.Print($"[SMS tongue] {_bones.Count} joints: {string.Join(" -> ", _bones.ConvertAll(b => skel.GetBoneName(b)))}");
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null || !Bound) return;
        Weight = Mathf.MoveToward(Weight, On ? 1f : 0f, (float)delta / Mathf.Max(0.01f, BlendSeconds));
        float k = 1f + (Stretch - 1f) * Weight;
        if (Mathf.Abs(k - 1f) > 1e-4f)
            foreach (int b in _bones) skel.SetBonePosePosition(b, skel.GetBonePosePosition(b) * k);
        Transform3D sg = skel.GlobalTransform;
        int n = _bones.Count;
        var pts = new Vector3[n];
        for (int i = 0; i < n; i++) pts[i] = sg * skel.GetBoneGlobalPose(_bones[i]).Origin;
        float reach = 0f; for (int i = 0; i < n - 1; i++) reach += pts[i].DistanceTo(pts[i + 1]);
        Points = pts; RootPoint = pts[0]; TipPoint = pts[n - 1]; Reach = reach; PointsValid = true; Passes++;
        if (Debug && _dbg < 2 && Weight >= 1f) { _dbg++; GD.Print($"[SMS tongue] pass: x{k:F2}, root {RootPoint}, tip {TipPoint}, {reach:F2} m along the chain"); }
    }
}
