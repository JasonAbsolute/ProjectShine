using Godot;

/// <summary>
/// Sunshine's joint "jiggle": TMtxTimeLag (MarioUtil/MtxUtil.cpp), the matrix effect TMultiMtxEffect hangs on Mario's model
/// (MarioDraw.cpp initModel: three bones, all TIME_LAG -- the chest joint and both upper arms; /Mario/MtxEffect14.prm, 16, 22).
/// Each tick the joint's WORLD matrix is followed by a lagging copy: a position with velocity, acceleration toward the
/// target, a brake, and a clamp to +-PosLimit around the target on each axis; and a quaternion accumulated the same way
/// (component-wise, no renormalising, the shorter arc chosen), rebuilt into a rotation. While enabled the lagging copy replaces
/// the matrix the CHILDREN inherit (J3D stores the joint's own matrix before its callback, so the joint's own skin keeps the clip):
/// on the chest joint the shoulders and head trail the chest by up to 5 units and bob after it; on the upper arms the forearms
/// and hands trail the swing. The state integrates whether or not the write is enabled, so enabling never pops.
/// Enabled by Mario per the game's rule: the body animation carries the flag (gMarioAnimeData bit 2: the locomotion set, not
/// hanging / climbing / squatting / demos), the upper body is idle (not pumping), and the level is 1200 ticks old.
/// Child of Mario's Skeleton3D, after SmsWaistLean (the game's chn_chest callback runs before the chest's in tree order).
/// </summary>
public partial class SmsTimeLag : SkeletonModifier3D
{
    public sealed class Entry
    {
        public string BoneName = "";
        public float PosAccel, PosBrake, PosLimitUnits, QuatAccel, QuatBrake; // the shipped MtxEffectN.prm fields
        public int Bone = -1;
        public int[] Children = System.Array.Empty<int>();
        public bool Init = true;                 // flag 2: take the current matrix as the lag state
        public Vector3 LagPos, PosVel;           // world metres
        public Quaternion LagQ = Quaternion.Identity;
        public Quaternion QVel = new Quaternion(0f, 0f, 0f, 0f); // component-wise velocity (not a rotation)
        public float DbgLagUnits, DbgLagDeg;     // what the children were moved by, this frame
    }

    public Entry[] Entries = System.Array.Empty<Entry>();
    public bool Enabled;        // flag 1: write the lag into the children (the state always runs)
    public float UnitsPerMetre = 74.44f;
    private float _tickAccum;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        if (skel == null)
            return;
        foreach (var e in Entries)
        {
            e.Bone = skel.FindBone(e.BoneName);
            e.Children = e.Bone >= 0 ? skel.GetBoneChildren(e.Bone) : System.Array.Empty<int>();
        }
    }

    /// <summary>flag 2 for every bone: the next update takes the animated matrix as the lag (a respawn, a teleport).</summary>
    public void Reset()
    {
        foreach (var e in Entries)
            e.Init = true;
    }

    private static Transform3D GlobalOf(Skeleton3D skel, int bone)
    {
        // the bone's skeleton-space transform from the CURRENT local poses (earlier modifiers included), root first
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
        if (skel == null || Entries.Length == 0)
            return;
        _tickAccum += (float)delta * 120f;                 // the game's callback runs on every 120 Hz tick
        int ticks = (int)_tickAccum;
        _tickAccum -= ticks;
        Transform3D skelW = skel.GlobalTransform;
        Basis skelRot = skelW.Basis.Orthonormalized();
        float limit = 0f;
        foreach (var e in Entries)
        {
            if (e.Bone < 0 || e.Children.Length == 0)
                continue;
            Transform3D g = GlobalOf(skel, e.Bone);
            Basis gRot = g.Basis.Orthonormalized();
            Vector3 tpos = skelW * g.Origin;                                   // world metres
            Quaternion tq = (skelRot * gRot).GetRotationQuaternion();
            limit = e.PosLimitUnits / UnitsPerMetre;
            if (e.Init)
            {
                e.Init = false;
                e.LagPos = tpos; e.PosVel = Vector3.Zero;
                e.LagQ = tq; e.QVel = new Quaternion(0f, 0f, 0f, 0f);
            }
            else
            {
                for (int i = 0; i < ticks; i++)
                {
                    // TMtxTimeLag::calc: unk08 += accel x (target - unk14); unk08 *= brake; unk14 += unk08; clamp to +-limit per axis
                    e.PosVel += e.PosAccel * (tpos - e.LagPos);
                    e.PosVel *= e.PosBrake;
                    e.LagPos += e.PosVel;
                    e.LagPos = new Vector3(
                        Mathf.Clamp(e.LagPos.X, tpos.X - limit, tpos.X + limit),
                        Mathf.Clamp(e.LagPos.Y, tpos.Y - limit, tpos.Y + limit),
                        Mathf.Clamp(e.LagPos.Z, tpos.Z - limit, tpos.Z + limit));
                    // the quaternion the same way: shorter arc, component-wise accumulation, no renormalising
                    Quaternion q = tq;
                    if (e.LagQ.X * q.X + e.LagQ.Y * q.Y + e.LagQ.Z * q.Z + e.LagQ.W * q.W < 0f)
                        q = new Quaternion(-q.X, -q.Y, -q.Z, -q.W);
                    e.QVel = new Quaternion(
                        (e.QVel.X + e.QuatAccel * (q.X - e.LagQ.X)) * e.QuatBrake,
                        (e.QVel.Y + e.QuatAccel * (q.Y - e.LagQ.Y)) * e.QuatBrake,
                        (e.QVel.Z + e.QuatAccel * (q.Z - e.LagQ.Z)) * e.QuatBrake,
                        (e.QVel.W + e.QuatAccel * (q.W - e.LagQ.W)) * e.QuatBrake);
                    e.LagQ = new Quaternion(e.LagQ.X + e.QVel.X, e.LagQ.Y + e.QVel.Y, e.LagQ.Z + e.QVel.Z, e.LagQ.W + e.QVel.W);
                }
            }
            float len = Mathf.Sqrt(e.LagQ.X * e.LagQ.X + e.LagQ.Y * e.LagQ.Y + e.LagQ.Z * e.LagQ.Z + e.LagQ.W * e.LagQ.W);
            if (len < 1e-6f)
            {
                e.Init = true;
                continue;
            }
            Quaternion lq = new Quaternion(e.LagQ.X / len, e.LagQ.Y / len, e.LagQ.Z / len, e.LagQ.W / len); // MTXQuat normalises
            e.DbgLagUnits = (e.LagPos - tpos).Length() * UnitsPerMetre;
            e.DbgLagDeg = Mathf.RadToDeg(tq.AngleTo(lq));
            if (!Enabled)
                continue;
            // the lagging world matrix, back in the bone's own frame: children take D x pose (rotation and origin), the bone's skin stays
            Basis lRotSkel = skelRot.Inverse() * new Basis(lq);
            Vector3 lPosSkel = skelW.AffineInverse() * e.LagPos;
            Transform3D gO = new Transform3D(gRot, g.Origin);
            Transform3D d = gO.AffineInverse() * new Transform3D(lRotSkel, lPosSkel);
            foreach (int c in e.Children)
            {
                Transform3D pc = skel.GetBonePose(c);
                Transform3D npc = d * pc;
                skel.SetBonePosePosition(c, npc.Origin);
                skel.SetBonePoseRotation(c, npc.Basis.Orthonormalized().GetRotationQuaternion());
            }
        }
    }
}
