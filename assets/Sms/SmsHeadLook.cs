using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS 175 -- a LOOK-ABOUT for Mario's head (Jaysonn: "make a new animation for mario where he looks around not just have him rotate"): a
/// skeleton modifier that turns jnt_head on its own after the clip has posed it -- YAW about the skeleton's UP (positive = to HIS LEFT), PITCH
/// about his LEFT axis (MarioHeadCtrl's convention: NEGATIVE looks UP, as HeadForwardLock's -12) -- from the REST pose (the clip's own head turn,
/// the idle ma_wait's -37 / +70 degree glance, is taken out while this is on; slerped in and out by Weight over EaseSeconds), with a share of the yaw
/// (ChestShare) ADDED to jnt_chest about the same up so the shoulders follow. The axes are taken every frame from the bones' posed frames
/// (SkeletonUp / SkeletonForward are the model's up and forward in the skeleton's space), so the rig's joint orientations do not matter. Runs in the
/// skeleton's modifier stage like HeadForwardLock, so the AnimationTree's pose does not overwrite it. Two ways to drive it: <see cref="Play"/> a
/// SEQUENCE of looks (yaw, pitch, the turn's seconds, the hold's seconds; each turn smooth-stepped) -- the look-about animation -- or
/// <see cref="Track"/> a world point every frame (the head on King Boo as he surfaces). <see cref="Stop"/> eases back to the clip. Readouts for the
/// drives: YawNow / PitchNow (radians), Step, Playing, Tracking, Weight, Runs, and DrawnGlobalPoseWith (a bone's global pose as it would be drawn at a
/// yaw / pitch -- Godot 4.3+ puts the clip's poses back after the skin update, so readers outside the modifier pass see the clip's alone).
/// </summary>
public partial class SmsHeadLook : SkeletonModifier3D
{
    public string HeadBone = "jnt_head", ChestBone = "jnt_chest";
    /// <summary>The chest turns this share of the head's yaw (added to the clip's chest); the ease in / out of the whole thing (seconds).</summary>
    public float ChestShare = 0.3f, EaseSeconds = 0.25f;
    /// <summary>The model's up and forward in the skeleton's OWN space: Mario's skeleton faces +Z there (the 175 drive measured it -- his armature,
    /// which faces -Z, carries a half turn and the 1 / 74.44 scale), so +Z; -Z would read his pitch upside down.</summary>
    public Vector3 SkeletonUp = Vector3.Up, SkeletonForward = Vector3.Back;
    /// <summary>The limits a Track is clamped to (radians).</summary>
    public float MaxYaw = Mathf.DegToRad(75f), MaxPitch = Mathf.DegToRad(65f);   // 176b: 65 -- the pitch counts from under a leaning chest (the stance's 18)

    public float Weight { get; private set; }
    public float YawNow { get; private set; }
    public float PitchNow { get; private set; }
    public bool Playing => _seq != null && _step < _seq.Count;
    public bool Tracking { get; private set; }
    public int Step => _step;
    public int Runs { get; private set; }
    public bool HasBones => _head >= 0;
    public int HeadBoneIndex => _head;
    public int ChestBoneIndex => _chest;

    public readonly struct Look
    {
        public readonly float Yaw, Pitch, Turn, Hold;   // radians, seconds
        public Look(float yaw, float pitch, float turn, float hold) { Yaw = yaw; Pitch = pitch; Turn = turn; Hold = hold; }
    }

    private int _head = -1, _chest = -1, _headParent = -1;
    private Basis _headRest = Basis.Identity;
    private Vector3 _headFwdLocal = Vector3.Back;   // the head's forward in its own frame (from the bind pose, where he faces the skeleton's forward)
    private List<Look> _seq; private int _step; private float _t, _fromYaw, _fromPitch; private bool _on, _holdAtEnd;
    private Vector3 _trackPoint, _trackForward; private float _trackYaw, _trackPitch;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        if (skel == null) return;
        _head = skel.FindBone(HeadBone); _chest = skel.FindBone(ChestBone);
        if (_head >= 0) { _headRest = skel.GetBoneRest(_head).Basis.Orthonormalized(); _headParent = skel.GetBoneParent(_head); _headFwdLocal = (skel.GetBoneGlobalRest(_head).Basis.Orthonormalized().Inverse() * SkeletonForward).Normalized(); }
    }

    /// <summary>The look-about: the looks in turn (each from where the head is, over its Turn seconds, then held Hold seconds). holdAtEnd: the
    /// last look is held until Stop / Track; else the head eases back to the clip when the sequence ends.</summary>
    public void Play(IList<Look> looks, bool holdAtEnd = true)
    {
        _seq = new List<Look>(looks); _step = 0; _t = 0f; _fromYaw = YawNow; _fromPitch = PitchNow; _on = true; _holdAtEnd = holdAtEnd; Tracking = false; Runs++;
    }
    /// <summary>The head on a world point: the yaw (positive = his left) and pitch (negative = up) from where the clip's pose has the head's REST
    /// looking -- under the chest as the clip turns and leans it (176: the battle stance turns the chest 45 degrees; the head still finds King Boo) --
    /// to the point, clamped. bodyForward is kept for the readouts' sake and no longer steers.</summary>
    public void Track(Vector3 worldPoint, Vector3 bodyForward = default)
    {
        _trackPoint = worldPoint; _trackForward = bodyForward; Tracking = true; _on = true; _seq = null;
    }
    /// <summary>Back to the clip (eased over EaseSeconds).</summary>
    public void Stop() { _on = false; Tracking = false; _seq = null; }

    private Vector3 Left => SkeletonUp.Cross(SkeletonForward).Normalized();   // up x forward = the model's left (for -Z forward: -X)

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skel = GetSkeleton();
        if (skel == null || _head < 0) return;
        float dt = (float)delta;
        // the targets
        float wantYaw = YawNow, wantPitch = PitchNow;
        if (_seq != null)
        {
            if (_step < _seq.Count)
            {
                Look l = _seq[_step]; _t += dt;
                float k = l.Turn > 1e-4f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_t / l.Turn, 0f, 1f)) : 1f;
                wantYaw = Mathf.Lerp(_fromYaw, l.Yaw, k); wantPitch = Mathf.Lerp(_fromPitch, l.Pitch, k);
                if (_t >= l.Turn + l.Hold) { _step++; _t = 0f; _fromYaw = l.Yaw; _fromPitch = l.Pitch; if (_step >= _seq.Count && !_holdAtEnd) _on = false; }
            }
        }
        else if (Tracking)
        {
            // from where the clip has the head's rest looking (under the posed parent: a turned, leaning chest) to the point, in the skeleton's space
            Vector3 headWorld = skel.GlobalTransform * skel.GetBoneGlobalPose(_head).Origin;
            Vector3 ds = (skel.GlobalBasis.Inverse() * (_trackPoint - headWorld));
            Basis pG = _headParent >= 0 ? skel.GetBoneGlobalPose(_headParent).Basis.Orthonormalized() : Basis.Identity;
            Vector3 rs = (pG * _headRest * _headFwdLocal).Normalized();
            Vector3 dsF = new Vector3(ds.X, 0f, ds.Z), rsF = new Vector3(rs.X, 0f, rs.Z);
            if (dsF.LengthSquared() > 1e-6f && rsF.LengthSquared() > 1e-6f)
            {
                dsF = dsF.Normalized(); rsF = rsF.Normalized(); ds = ds.Normalized();
                _trackYaw = Mathf.Clamp(MathF.Atan2(SkeletonUp.Dot(rsF.Cross(dsF)), rsF.Dot(dsF)), -MaxYaw, MaxYaw);   // + = to his left (counter-clockwise seen from above)
                _trackPitch = Mathf.Clamp(-(MathF.Asin(Mathf.Clamp(ds.Y, -1f, 1f)) - MathF.Asin(Mathf.Clamp(rs.Y, -1f, 1f))), -MaxPitch, MaxPitch);   // the game's Rz: negative = up
            }
            float k = 1f - MathF.Exp(-dt / 0.12f);   // the head catches up smoothly
            wantYaw = Mathf.Lerp(YawNow, _trackYaw, k); wantPitch = Mathf.Lerp(PitchNow, _trackPitch, k);
        }
        YawNow = wantYaw; PitchNow = wantPitch;
        Weight = Mathf.MoveToward(Weight, _on ? 1f : 0f, EaseSeconds > 1e-4f ? dt / EaseSeconds : 1f);
        if (Weight <= 1e-4f) { if (!_on) { YawNow = 0f; PitchNow = 0f; } return; }
        // the chest's share: about the skeleton's up, in the chest's posed frame (the clip's), added
        Basis parentG = _headParent >= 0 ? skel.GetBoneGlobalPose(_headParent).Basis.Orthonormalized() : Basis.Identity;   // the head's parent as the clip posed it
        if (_chest >= 0 && Mathf.Abs(ChestShare) > 1e-4f && Mathf.Abs(YawNow) > 1e-4f)
        {
            Basis chestG = skel.GetBoneGlobalPose(_chest).Basis.Orthonormalized();
            var share = new Quaternion((chestG.Inverse() * SkeletonUp).Normalized(), YawNow * ChestShare * Weight);
            skel.SetBonePoseRotation(_chest, skel.GetBonePoseRotation(_chest) * share);
            if (_headParent == _chest) parentG = chestG * new Basis(share);   // the head sits on the turned chest
        }
        // the head: from its rest under the parent as posed, yawed about the skeleton's up, pitched about his left -- replacing the clip's head
        Quaternion target = HeadTarget(parentG, YawNow, PitchNow);
        Quaternion cur = skel.GetBonePoseRotation(_head);
        skel.SetBonePoseRotation(_head, cur.Slerp(target, Weight));
    }

    /// <summary>The head's local rotation for a yaw / pitch, given its parent's global basis: rest, then the pitch about the LEFT of where the rest
    /// looks under that parent (so a pitch only raises the gaze, whichever way the chest is turned), then the yaw about the skeleton's up -- the axes
    /// mapped into the head's rest frame.</summary>
    private Quaternion HeadTarget(Basis parentG, float yaw, float pitch)
    {
        Basis inv = (parentG * _headRest).Inverse();   // skeleton space -> the head's rest frame
        Vector3 rs = parentG * _headRest * _headFwdLocal; rs.Y = 0f;
        Vector3 leftOfRest = rs.LengthSquared() > 1e-6f ? SkeletonUp.Cross(rs.Normalized()).Normalized() : Left;
        Vector3 yawAxis = (inv * SkeletonUp).Normalized(), pitchAxis = (inv * leftOfRest).Normalized();
        return _headRest.GetRotationQuaternion() * new Quaternion(yawAxis, yaw) * new Quaternion(pitchAxis, pitch);
    }

    /// <summary>For the drives: a bone's global pose (skeleton space) as it would be DRAWN with this modifier at the given yaw / pitch at full
    /// weight -- the clip's chain with the chest's share added and the head replaced from its rest.</summary>
    public Transform3D DrawnGlobalPoseWith(Skeleton3D skel, int bone, float yaw, float pitch)
    {
        if (skel == null || bone < 0) return Transform3D.Identity;
        var chain = new List<int>();
        for (int b = bone; b >= 0; b = skel.GetBoneParent(b)) chain.Add(b);
        Transform3D drawn = Transform3D.Identity, parentClip = Transform3D.Identity;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            int b = chain[i];
            Transform3D clip = skel.GetBoneGlobalPose(b);
            Transform3D local = parentClip.AffineInverse() * clip;
            if (b == _head) local = new Transform3D(new Basis(HeadTarget(drawn.Basis.Orthonormalized(), yaw, pitch)), local.Origin);   // under the parent as DRAWN (the chest's share in)
            else if (b == _chest && Mathf.Abs(ChestShare) > 1e-4f) local = new Transform3D(local.Basis * new Basis(new Quaternion((clip.Basis.Orthonormalized().Inverse() * SkeletonUp).Normalized(), yaw * ChestShare)), local.Origin);
            drawn *= local;
            parentClip = clip;
        }
        return drawn;
    }
}
