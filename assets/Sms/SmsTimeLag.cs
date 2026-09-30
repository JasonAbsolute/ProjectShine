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
/// TIMING. The game runs the callback on every 120 Hz move tick, in lockstep with Mario's movement. Here Mario moves in the
/// physics frame and the skeleton renders in the process frame; a lag stepped per render frame against a world target that only
/// moves on physics frames popped the children back and forth by the clamp on every frame the two did not line up. So
/// <see cref="Step"/> runs from Mario's physics frame with that frame's tick count, reads the bones' current global poses as the
/// targets, and stores each bone's offset in ITS OWN frame; the modifier pass only applies the stored offset to the children --
/// an offset that rides the animated bone smoothly between physics frames.
/// Child of Mario's Skeleton3D, after SmsWaistLean (the game's chn_chest callback runs before the chest's in tree order).
/// THE SCENE: assets/Sms/BodyLag.tscn is this node with its <see cref="Bones"/> -- one SmsTimeLagEntry per bone, the shipped values --
/// to instance under Armature/Skeleton3D in Player.tscn; Mario adopts it there (and builds one with a warning when it is missing).
/// </summary>
public partial class SmsTimeLag : SkeletonModifier3D
{
    /// <summary>The bones and their MtxEffect fields (the scene's list; the editor edits them).</summary>
    [Export] public SmsTimeLagEntry[] Bones = System.Array.Empty<SmsTimeLagEntry>();

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
        public Transform3D D = Transform3D.Identity; // the lagging frame relative to the bone's own, in the bone's frame
        public bool Ready;                       // D holds a stepped state
        public float DbgLagUnits, DbgLagDeg;     // how far the children sit from where the clip puts them
    }

    /// <summary>The running state, one per entry of <see cref="Bones"/> (built in _Ready).</summary>
    public Entry[] Entries = System.Array.Empty<Entry>();
    public bool Enabled;        // flag 1: write the lag into the children (the state always runs)
    public float UnitsPerMetre = 74.44f;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        if (skel == null)
            return;
        if (Entries.Length == 0)
        {
            var list = new System.Collections.Generic.List<Entry>();
            foreach (var b in Bones)
                if (b != null && !string.IsNullOrEmpty(b.BoneName))
                    list.Add(new Entry { BoneName = b.BoneName, PosAccel = b.PosAccel, PosBrake = b.PosBrake, PosLimitUnits = b.PosLimitUnits, QuatAccel = b.QuatAccel, QuatBrake = b.QuatBrake });
            Entries = list.ToArray();
        }
        foreach (var e in Entries)
        {
            e.Bone = skel.FindBone(e.BoneName);
            e.Children = e.Bone >= 0 ? skel.GetBoneChildren(e.Bone) : System.Array.Empty<int>();
        }
    }

    /// <summary>flag 2 for every bone: the next step takes the animated matrix as the lag (a respawn, a teleport).</summary>
    public void Reset()
    {
        foreach (var e in Entries)
        {
            e.Init = true;
            e.Ready = false;
        }
    }

    /// <summary>TMtxTimeLag::calc for <paramref name="ticks"/> move ticks, called from Mario's physics frame after the movement.
    /// The targets are the bones' global poses as the skeleton last computed them (the waist lean and the previous frame's lags
    /// included, as the game's recursion has them).</summary>
    public void Step(int ticks)
    {
        var skel = GetSkeleton();
        if (skel == null || Entries.Length == 0)
            return;
        Transform3D skelW = skel.GlobalTransform;
        Basis skelRot = skelW.Basis.Orthonormalized();
        foreach (var e in Entries)
        {
            if (e.Bone < 0 || e.Children.Length == 0)
                continue;
            Transform3D g = skel.GetBoneGlobalPose(e.Bone);
            Basis gRot = g.Basis.Orthonormalized();
            Vector3 tpos = skelW * g.Origin;                                   // world metres
            Quaternion tq = (skelRot * gRot).GetRotationQuaternion();
            float limit = e.PosLimitUnits / UnitsPerMetre;
            if (e.Init)
            {
                e.Init = false;
                e.LagPos = tpos; e.PosVel = Vector3.Zero;
                e.LagQ = tq; e.QVel = new Quaternion(0f, 0f, 0f, 0f);
                e.D = Transform3D.Identity; e.Ready = false;
                e.DbgLagUnits = 0f; e.DbgLagDeg = 0f;
                continue;
            }
            for (int i = 0; i < ticks; i++)
            {
                // unk08 += accel x (target - unk14); unk08 *= brake; unk14 += unk08; clamp to +-limit per axis
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
            float len = Mathf.Sqrt(e.LagQ.X * e.LagQ.X + e.LagQ.Y * e.LagQ.Y + e.LagQ.Z * e.LagQ.Z + e.LagQ.W * e.LagQ.W);
            if (len < 1e-6f)
            {
                e.Init = true;
                e.Ready = false;
                continue;
            }
            Quaternion lq = new Quaternion(e.LagQ.X / len, e.LagQ.Y / len, e.LagQ.Z / len, e.LagQ.W / len); // MTXQuat normalises
            e.DbgLagUnits = (e.LagPos - tpos).Length() * UnitsPerMetre;
            e.DbgLagDeg = Mathf.RadToDeg(tq.AngleTo(lq));
            // the lagging world frame, relative to the bone's own frame: the children take D x pose; the bone's skin keeps the clip
            Basis lRotSkel = skelRot.Inverse() * new Basis(lq);
            Vector3 lPosSkel = skelW.AffineInverse() * e.LagPos;
            Transform3D gO = new Transform3D(gRot, g.Origin);
            e.D = gO.AffineInverse() * new Transform3D(lRotSkel, lPosSkel);
            e.Ready = true;
        }
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        if (!Enabled)
            return;
        var skel = GetSkeleton();
        if (skel == null)
            return;
        foreach (var e in Entries)
        {
            if (!e.Ready || e.Bone < 0)
                continue;
            foreach (int c in e.Children)
            {
                Transform3D pc = skel.GetBonePose(c);
                Transform3D npc = e.D * pc;
                skel.SetBonePosePosition(c, npc.Origin);
                skel.SetBonePoseRotation(c, npc.Basis.Orthonormalized().GetRotationQuaternion());
            }
        }
    }
}
