using Godot;
using System.Collections.Generic;

/// <summary>
/// SMS pass 162: KING BOO'S TONGUE AIMED -- the Lick attack's tongue (KINGBOO_REMATCH_gdoc.txt: "he teleports directly in front of Mario and licks").
/// The casino's clips have no lunge: btelesa_roll whirls the tongue about (the roulette spin) and btelesa_lick licks the lips, so the attack is a clip
/// for the body with the tongue CHANGED (Jaysonn: "use that as a base and change where the tongue is"): a SkeletonModifier3D under his Skeleton3D, as
/// Mario's lasso arm is (SmsLassoArm), that after the clip has posed him bends the tongue chain (jnt_sita_1 -> 2 -> 3 -> 4 -> eff_sita) onto
/// <see cref="Target"/> (a point in the world): a FABRIK pass over the chain with the clip's own segment lengths -- the tip lands on the target when
/// it is within reach, the tongue curls on the way; out of reach it points straight at it -- then each joint turned by the shortest arc so its child
/// lies on the chain. <see cref="Weight"/> blends in over BlendSeconds when <see cref="On"/> (the snap) and out again (the retract); the joints' places
/// in the world AFTER the pose are <see cref="Points"/>, read in the same pass (a Skeleton3D restores the clip's poses once the frame is drawn) -- the
/// lick's hit on Mario is tested on them.
/// </summary>
public partial class SmsTongueAim : SkeletonModifier3D
{
    public bool On;
    public float BlendSeconds = 0.12f;
    /// <summary>Where the tongue goes (world).</summary>
    public Vector3 Target;
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
        Transform3D sg = skel.GlobalTransform;
        int n = _bones.Count;
        if (Weight > 0f)
        {
            // the chain as the clip posed it (skeleton space) and its segment lengths
            var p = new Vector3[n]; var len = new float[n - 1]; float total = 0f;
            for (int i = 0; i < n; i++) p[i] = skel.GetBoneGlobalPose(_bones[i]).Origin;
            for (int i = 0; i < n - 1; i++) { len[i] = Mathf.Max(1e-4f, p[i].DistanceTo(p[i + 1])); total += len[i]; }
            Vector3 root = p[0];
            Vector3 target = sg.AffineInverse() * Target;
            if (root.DistanceTo(target) >= total)
            {
                Vector3 dir = (target - root).Normalized();                     // out of reach: straight at it
                for (int i = 1; i < n; i++) p[i] = p[i - 1] + dir * len[i - 1];
            }
            else
            {
                for (int it = 0; it < 6; it++)                                  // FABRIK: the tip onto the target, back to the root, and again
                {
                    p[n - 1] = target;
                    for (int i = n - 2; i >= 0; i--) { Vector3 d = p[i] - p[i + 1]; p[i] = p[i + 1] + (d.LengthSquared() > 1e-10f ? d.Normalized() : Vector3.Up) * len[i]; }
                    p[0] = root;
                    for (int i = 0; i < n - 1; i++) { Vector3 d = p[i + 1] - p[i]; p[i + 1] = p[i] + (d.LengthSquared() > 1e-10f ? d.Normalized() : Vector3.Forward) * len[i]; }
                }
            }
            bool dbg = Debug && _dbg < 2 && Weight >= 1f;
            if (dbg) GD.Print($"[SMS tongue] pass: root {sg * root} target {Target} reach {total * sg.Basis.Scale.Y:F2} m ({(root.DistanceTo(target) >= total ? "out of reach" : "in reach")})");
            for (int i = 0; i < n - 1; i++)                                     // each joint turned so its child lies on the chain (a parent's turn carries its children)
            {
                Vector3 from = skel.GetBoneGlobalPose(_bones[i]).Origin;
                Vector3 want = p[i + 1] - from;
                if (want.LengthSquared() < 1e-10f) continue;
                Aim(skel, _bones[i], _bones[i + 1], want.Normalized(), dbg);
            }
            if (dbg) _dbg++;
        }
        var pts = new Vector3[n];
        for (int i = 0; i < n; i++) pts[i] = sg * skel.GetBoneGlobalPose(_bones[i]).Origin;
        float reach = 0f; for (int i = 0; i < n - 1; i++) reach += pts[i].DistanceTo(pts[i + 1]);
        Points = pts; RootPoint = pts[0]; TipPoint = pts[n - 1]; Reach = reach; PointsValid = true; Passes++;
    }

    /// <summary>The bone turned about its own origin so the direction to its child points along <paramref name="want"/> (skeleton space), by Weight -- the
    /// lasso arm's way: the turn in the skeleton's frame, carried into the bone's local pose, set as its pose rotation.</summary>
    private void Aim(Skeleton3D skel, int bone, int child, Vector3 want, bool dbg = false)
    {
        Transform3D g = skel.GetBoneGlobalPose(bone);
        Vector3 have = skel.GetBoneGlobalPose(child).Origin - g.Origin;
        if (have.LengthSquared() < 1e-10f) return;
        Quaternion q = ShortestArc(have.Normalized(), want);
        if (Weight < 1f) q = Quaternion.Identity.Slerp(q, Weight);
        int parent = skel.GetBoneParent(bone);
        Basis parentB = parent >= 0 ? skel.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        Basis newLocal = parentB.Inverse() * (new Basis(q) * g.Basis);
        skel.SetBonePoseRotation(bone, newLocal.Orthonormalized().GetRotationQuaternion());
        if (dbg) GD.Print($"[SMS tongue]   {skel.GetBoneName(bone)}: have {have.Normalized()} want {want} -> child now {skel.GetBoneGlobalPose(child).Origin}");
    }

    private static Quaternion ShortestArc(Vector3 a, Vector3 b)
    {
        float d = a.Dot(b);
        if (d > 0.99999f) return Quaternion.Identity;
        if (d < -0.99999f)
        {
            Vector3 axis = a.Cross(Vector3.Up);
            if (axis.LengthSquared() < 1e-6f) axis = a.Cross(Vector3.Right);
            return new Quaternion(axis.Normalized(), Mathf.Pi);
        }
        Vector3 c = a.Cross(b);
        return new Quaternion(c.X, c.Y, c.Z, 1f + d).Normalized();
    }
}
