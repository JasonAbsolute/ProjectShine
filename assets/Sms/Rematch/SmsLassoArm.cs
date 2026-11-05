using Godot;

/// <summary>
/// SMS pass 159: the LASSO ARM -- one of Mario's arms held up in the air while he swings a Bomb Boo round by its tongue (KINGBOO_REMATCH_gdoc.txt, NEW
/// WORK: "an animation blend that holds one of Mario's arms in the air, for the carry"). A SkeletonModifier3D under his Skeleton3D, as the port's
/// other bone systems are (SmsWaistLean, SmsBoneRotZ, SmsUpperPose): after the clip has posed him, the upper arm (jnt_arm_R1 / L1) is turned by the
/// shortest arc so it points up -- <see cref="Tilt"/> outward (the side read off the chest-to-shoulder vector, so it holds through every clip) and
/// <see cref="Forward"/> ahead -- and the forearm (jnt_arm_R2 / L2) up and <see cref="In"/> over the head; the hand is left as the clip has it. So the
/// arm stays up through the run, the jumps and the landings, with the clip's own twist, until its real animation comes. <see cref="Weight"/> blends in
/// over BlendSeconds when <see cref="On"/> and out again. <see cref="HandPoint"/> is the hand's place in the world AFTER the pose, read in the same pass
/// (a Skeleton3D restores the clip's poses once the frame is drawn, so GetBoneGlobalPose from outside would give the arm down): the Bomb Boo's
/// tongue ends there. The directions are taken in the WORLD and carried into the skeleton's frame (the armature turns with him).
/// </summary>
public partial class SmsLassoArm : SkeletonModifier3D
{
    /// <summary>R or L: which arm.</summary>
    public string Side = "R";
    public bool On;
    public float BlendSeconds = 0.15f;
    /// <summary>The upper arm's lean outward and forward, the forearm's lean in over the head -- fractions of straight up.</summary>
    public float Tilt = 0.35f, Forward = 0.2f, In = 0.2f;
    public bool Debug;
    public float Weight { get; private set; }
    public Vector3 HandPoint { get; private set; }
    public bool HandValid { get; private set; }
    public bool Bound => _r1 >= 0 && _r2 >= 0 && _hand >= 0;
    /// <summary>For the drives: how far the hand moved up this pass against the clip's pose (world metres), the shoulder's place, and the arm's direction
    /// (shoulder to wrist, world) against straight up after the pose (1 = straight up).</summary>
    public float HandLift { get; private set; }
    public Vector3 ShoulderPoint { get; private set; }
    public float ArmUpDot { get; private set; }

    private int _chest = -1, _r1 = -1, _r2 = -1, _hand = -1; private int _dbg;

    public override void _Ready()
    {
        Active = true; Influence = 1f;
        var skel = GetSkeleton();
        if (skel == null) return;
        _chest = skel.FindBone("jnt_chest");
        _r1 = skel.FindBone("jnt_arm_" + Side + "1"); _r2 = skel.FindBone("jnt_arm_" + Side + "2"); _hand = skel.FindBone("jnt_hand_" + Side);
        if (!Bound) GD.PushWarning($"[SMS lasso] {GetPath()}: no jnt_arm_{Side}1 / jnt_arm_{Side}2 / jnt_hand_{Side} on the skeleton -- the arm stays as the clips have it");
        else if (Debug) GD.Print($"[SMS lasso] bones: chest {_chest} arm1 {_r1} (parent {skel.GetBoneParent(_r1)} {(skel.GetBoneParent(_r1) >= 0 ? skel.GetBoneName(skel.GetBoneParent(_r1)) : "-")}) arm2 {_r2} (parent {skel.GetBoneParent(_r2)}) hand {_hand} (parent {skel.GetBoneParent(_hand)}); skeleton basis scale {skel.GlobalTransform.Basis.Scale}");
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null || !Bound) return;
        Weight = Mathf.MoveToward(Weight, On ? 1f : 0f, (float)delta / Mathf.Max(0.01f, BlendSeconds));
        Transform3D sg = skel.GlobalTransform;
        Vector3 hand0 = sg * skel.GetBoneGlobalPose(_hand).Origin;
        if (Weight > 0f)
        {
            // the arm's side from the pose itself: chest -> shoulder is outward; up x outward is forward for the right arm (outward x up for the left),
            // the right-hand rule, whatever way he faces
            Basis toSkel = sg.Basis.Inverse();
            Vector3 side = _chest >= 0 ? sg * skel.GetBoneGlobalPose(_r1).Origin - sg * skel.GetBoneGlobalPose(_chest).Origin : sg.Basis.X * (Side == "R" ? 1f : -1f);
            side.Y = 0f;
            side = side.LengthSquared() > 1e-8f ? side.Normalized() : (sg.Basis.X * (Side == "R" ? 1f : -1f)).Normalized();
            Vector3 fwd = Side == "R" ? Vector3.Up.Cross(side) : side.Cross(Vector3.Up);
            Vector3 upper = (Vector3.Up + fwd * Forward + side * Tilt).Normalized();
            Vector3 lower = (Vector3.Up + fwd * (Forward * 0.5f) - side * In).Normalized();
            bool dbg = Debug && _dbg < 3 && Weight >= 1f;
            if (dbg) GD.Print($"[SMS lasso] pass: hand before {hand0} side {side} fwd {fwd}; arm1 global {skel.GetBoneGlobalPose(_r1).Origin} arm2 {skel.GetBoneGlobalPose(_r2).Origin} hand {skel.GetBoneGlobalPose(_hand).Origin} (skeleton space)");
            Aim(skel, _r1, _r2, (toSkel * upper).Normalized(), dbg);
            if (dbg) GD.Print($"[SMS lasso]   after arm1: arm2 {skel.GetBoneGlobalPose(_r2).Origin} hand {skel.GetBoneGlobalPose(_hand).Origin}");
            Aim(skel, _r2, _hand, (toSkel * lower).Normalized(), dbg);
            if (dbg) { GD.Print($"[SMS lasso]   after arm2: hand {skel.GetBoneGlobalPose(_hand).Origin} -> world {sg * skel.GetBoneGlobalPose(_hand).Origin}"); _dbg++; }
        }
        HandPoint = sg * skel.GetBoneGlobalPose(_hand).Origin;
        ShoulderPoint = sg * skel.GetBoneGlobalPose(_r1).Origin;
        Vector3 arm = HandPoint - ShoulderPoint;
        ArmUpDot = arm.LengthSquared() > 1e-8f ? arm.Normalized().Dot(Vector3.Up) : 0f;
        HandLift = HandPoint.Y - hand0.Y;
        HandValid = true;
    }

    /// <summary>The bone turned about its own origin so the direction to its child points along <paramref name="want"/> (skeleton space), by Weight: the
    /// turn taken in the skeleton's frame, carried into the bone's local pose (the parent's global pose undone) and set as its pose rotation -- the
    /// port's other modifiers write poses this way.</summary>
    private void Aim(Skeleton3D skel, int bone, int child, Vector3 want, bool dbg = false)
    {
        Transform3D g = skel.GetBoneGlobalPose(bone);
        Vector3 have = skel.GetBoneGlobalPose(child).Origin - g.Origin;
        if (have.LengthSquared() < 1e-10f || want.LengthSquared() < 1e-10f) { if (dbg) GD.Print($"[SMS lasso]   bone {bone}: no direction to its child"); return; }
        Quaternion q = ShortestArc(have.Normalized(), want);
        if (Weight < 1f) q = Quaternion.Identity.Slerp(q, Weight);
        int parent = skel.GetBoneParent(bone);
        Basis parentB = parent >= 0 ? skel.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        Basis newGlobal = new Basis(q) * g.Basis;
        Basis newLocal = parentB.Inverse() * newGlobal;
        Quaternion before = skel.GetBonePoseRotation(bone);
        skel.SetBonePoseRotation(bone, newLocal.Orthonormalized().GetRotationQuaternion());
        if (dbg) GD.Print($"[SMS lasso]   bone {bone} ({skel.GetBoneName(bone)}): have {have.Normalized()} want {want} q {q} local rot {before} -> {skel.GetBonePoseRotation(bone)}");
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
