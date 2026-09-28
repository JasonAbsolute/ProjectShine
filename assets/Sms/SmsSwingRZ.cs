using Godot;

/// <summary>
/// Sunshine's FLUDD handle swing: TMtxSwingRZ (MarioUtil/MtxUtil.cpp), the second entry of the water gun's TMultiMtxEffect
/// (WaterGun.cpp 1283: bone 0 TIME_LAG -- never enabled, see SmsParams.MtxEffect -- and "jnt_G_handle" SWING_RZ). A point mass on a
/// rope of length L hangs from the joint in WORLD space: each tick (Mario's calcAnim runs on every 120 Hz move tick, MarioMain.cpp 92)
/// vel += acc; vel *= brake; bob += vel; the bob is put back on the sphere of radius L around the joint and that correction is added to
/// its velocity. The joint's world X axis is then aimed at the bob (Y = Z x X, Z -- the hinge, Mario's lateral axis -- untouched), and
/// because J3D stores a joint's own matrix before its callback, the CHILDREN take the aimed frame: jnt_G_handle_R / L, the levers and
/// the grips swing about the hinge, the handle bar itself stays with the tank. In the bind pose the handle's X points straight down,
/// so a bob hanging straight down is the clip's pose.
/// The shipped game runs this with TMtxSwingRZ's compiled-in defaults -- acc (0, 0, 0), L 50, brake 0.9 -- because the model's
/// jnt_G_handle is joint 7 and /Mario/WaterGun/MtxEffect7.prm does not ship (10 and 12 do, written for an older rig). With no
/// gravity the grips have no rest direction: they trail his motion and stay where the last move left them (see SmsParams.MtxEffect).
/// Written while the body animation carries gMarioAnimeData bit 4 (everything but the squat, the rope, the spin and the shine demo),
/// the upper body is idle and he has the FLUDD; the state integrates regardless, so enabling never pops.
/// Child of the tank's Skeleton3D; Mario sets <see cref="Enabled"/> each frame.
/// </summary>
public partial class SmsSwingRZ : SkeletonModifier3D
{
    public string BoneName = "jnt_G_handle";
    public Vector3 AccUnits = Vector3.Zero; // mAcc: world units a tick^2 (the game's Y is up, as Godot's)
    public float LUnits = 50f;              // mL
    public float Brake = 0.9f;              // mBrake
    public bool Enabled;                    // flag 1: aim the children's frame (the state always runs)
    public float UnitsPerMetre = 74.44f;
    public float DbgAngleDeg;               // the rope's angle from straight down about the hinge: positive = swung toward his back

    private int _bone = -1;
    private int[] _children = System.Array.Empty<int>();
    private bool _init = true;              // flag 2: hang the bob straight down from the current joint position
    private Vector3 _bob, _vel;             // world units
    private float _tickAccum;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        if (skel == null)
            return;
        _bone = skel.FindBone(BoneName);
        _children = _bone >= 0 ? skel.GetBoneChildren(_bone) : System.Array.Empty<int>();
    }

    public void Reset() => _init = true;

    private static Transform3D GlobalOf(Skeleton3D skel, int bone)
    {
        var chain = new System.Collections.Generic.List<int>();
        for (int b = bone; b >= 0; b = skel.GetBoneParent(b))
            chain.Add(b);
        Transform3D t = Transform3D.Identity;
        for (int i = chain.Count - 1; i >= 0; i--)
            t *= skel.GetBonePose(chain[i]);
        return t;
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null || _bone < 0 || _children.Length == 0)
            return;
        _tickAccum += (float)delta * 120f;
        int ticks = (int)_tickAccum;
        _tickAccum -= ticks;
        Transform3D skelW = skel.GlobalTransform;
        Basis skelRot = skelW.Basis.Orthonormalized();
        Transform3D g = GlobalOf(skel, _bone);
        Basis gRot = g.Basis.Orthonormalized();
        Basis w = skelRot * gRot;                                 // the joint's world axes (columns)
        Vector3 pos = (skelW * g.Origin) * UnitsPerMetre;         // the hinge, world units
        Vector3 zAxis = w.Z.Normalized();
        if (_init)
        {
            // TMtxSwingRZ::calcLocalXY, flag 2: the bob hangs straight down, the axes are left as they are
            _init = false;
            _vel = Vector3.Zero;
            _bob = pos + new Vector3(0f, -LUnits, 0f);
            DbgAngleDeg = 0f;
            return;
        }
        for (int i = 0; i < ticks; i++)
        {
            _vel = (_vel + AccUnits) * Brake;
            _bob += _vel;
            Vector3 diff = _bob - pos;
            if (diff.LengthSquared() < 1e-8f)
                diff = new Vector3(0f, -1f, 0f); // the game would divide by zero here; keep it hanging
            Vector3 point = pos + diff.Normalized() * LUnits;
            _vel += point - _bob;                                 // the rope's pull, added to the velocity
            _bob = point;
        }
        Vector3 vecX = (_bob - pos).Normalized();
        Vector3 vecY = zAxis.Cross(vecX);
        if (vecY.LengthSquared() < 1e-8f)
            return;                                               // the rope lies along the hinge: leave the frame alone
        vecY = vecY.Normalized();
        vecX = vecY.Cross(zAxis).Normalized();
        Vector3 down = new Vector3(0f, -1f, 0f);
        DbgAngleDeg = Mathf.RadToDeg(Mathf.Atan2(down.Cross(vecX).Dot(zAxis), down.Dot(vecX)));
        if (!Enabled)
            return;
        // the aimed world frame, back in the joint's own frame: children take D x pose, the joint's own skin keeps the clip
        Basis aimedSkel = skelRot.Inverse() * new Basis(vecX, vecY, zAxis);
        Transform3D gO = new Transform3D(gRot, g.Origin);
        Transform3D d = gO.AffineInverse() * new Transform3D(aimedSkel, g.Origin);
        foreach (int c in _children)
        {
            Transform3D pc = skel.GetBonePose(c);
            Transform3D npc = d * pc;
            skel.SetBonePosePosition(c, npc.Origin);
            skel.SetBonePoseRotation(c, npc.Basis.Orthonormalized().GetRotationQuaternion());
        }
    }
}
