using Godot;

/// <summary>NPCNeckCallBack (NpcCallback.cpp 10-110), the J3D joint callback the game hangs on every NPC's "kubi" joint: each
/// drawn frame the head turns toward Mario's position + 85 units when he is within NeckTurnSearchDist / Height, the yaw clamped
/// to +-NeckMaxAngleY and chased at NeckAngleYSpeed BAM a frame, the pitch clamped [NeckMinAngleX, NeckMaxAngleX], eased by how
/// far the yaw has gone (CLBEaseOutInbetween(tmp/2, tmp, |yaw| / max)) and chased at NeckAngleXSpeed; outside the range both chase
/// back to zero. The rotation is concatenated AFTER the animated joint matrix -- a rotation in the bone's own frame -- so here it
/// is a SkeletonModifier3D writing the bone pose after the AnimationPlayer. The bone's forward and up axes are read off its rest
/// pose at start (the Pianta's kubi has X up and Y forward; the Toad's differs), so the same code serves both models.</summary>
public partial class SmsNpcNeck : SkeletonModifier3D
{
    public SmsNpc Owner;
    public string BoneName = "kubi";

    private int _bone = -1;
    private Vector3 _fwd = Vector3.Up, _up = Vector3.Up, _side = Vector3.Right; // the bone-local axes
    private float _yaw, _pitch;             // the current head offset, radians
    public float YawDeg => Mathf.RadToDeg(_yaw);
    public float PitchDeg => Mathf.RadToDeg(_pitch);
    public bool Tracking { get; private set; }

    private static float Chase(float cur, float target, float step)
    {
        float d = target - cur;
        if (Mathf.Abs(d) <= step) return target;
        return cur + Mathf.Sign(d) * step;
    }

    private void Resolve(Skeleton3D skel)
    {
        _bone = skel.FindBone(BoneName);
        if (_bone < 0) return;
        // the bone's rest frame in NPC space: which local axis points along the NPC's forward (-Z) and which up
        Transform3D rest = skel.GetBoneGlobalRest(_bone);
        Transform3D toNpc = Owner.GlobalTransform.AffineInverse() * skel.GlobalTransform;
        Basis b = (toNpc.Basis * rest.Basis).Orthonormalized();
        Vector3[] axes = { Vector3.Right, Vector3.Up, Vector3.Back };
        float bestF = -2f, bestU = -2f;
        foreach (var a in axes)
        {
            Vector3 w = b * a;
            if (w.Dot(Vector3.Forward) > bestF) { bestF = w.Dot(Vector3.Forward); _fwd = a; }
            if (-w.Dot(Vector3.Forward) > bestF) { bestF = -w.Dot(Vector3.Forward); _fwd = -a; }
            if (w.Dot(Vector3.Up) > bestU) { bestU = w.Dot(Vector3.Up); _up = a; }
            if (-w.Dot(Vector3.Up) > bestU) { bestU = -w.Dot(Vector3.Up); _up = -a; }
        }
        _side = _fwd.Cross(_up).Normalized();
    }

    public override void _ProcessModification()
    {
        var skel = GetSkeleton();
        if (skel == null || Owner == null || Owner.Profile == null) return;
        if (_bone < 0) { Resolve(skel); if (_bone < 0) return; }
        var p = Owner.Profile;
        float dtFrames = (float)(GetProcessDeltaTime() * 30.0); // the game's chase speeds are per drawn frame (30 fps)
        if (dtFrames <= 0f) dtFrames = 1f;

        Transform3D pose = skel.GetBoneGlobalPose(_bone);                 // skeleton space, the animation's pose
        Transform3D skelWorld = skel.GlobalTransform;
        Vector3 headWorld = skelWorld * pose.Origin;
        float targetYaw = 0f, targetPitch = 0f;
        Tracking = false;
        Vector3 mario = Owner.MarioPositionMetres();
        if (Owner.NeckAllowed && mario != Vector3.Inf)
        {
            Vector3 look = mario + Vector3.Up * SmsUnits.UnitsToMetres(SmsParams.Npc.NeckTargetHeight);
            Vector3 to = look - headWorld;
            float distU = to.Length() * SmsUnits.UnitsPerMetre;
            if (distU > 0.03f && distU < p.NeckTurnSearchDist && Mathf.Abs(look.Y - headWorld.Y) * SmsUnits.UnitsPerMetre < p.NeckTurnSearchHeight)
            {
                // into the bone's frame (world -> skeleton -> bone)
                Basis boneWorld = (skelWorld.Basis * pose.Basis).Orthonormalized();
                Vector3 local = boneWorld.Inverse() * to.Normalized();
                float f = local.Dot(_fwd), s = local.Dot(_side), u = local.Dot(_up);
                targetYaw = Mathf.Atan2(s, f);
                targetPitch = Mathf.Atan2(u, Mathf.Sqrt(f * f + s * s));
                float maxY = p.NeckMaxAngleY * SmsUnits.BamToRad;
                targetYaw = Mathf.Clamp(targetYaw, -maxY, maxY);
                targetPitch = Mathf.Clamp(targetPitch, p.NeckMinAngleX * SmsUnits.BamToRad, p.NeckMaxAngleX * SmsUnits.BamToRad);
                Tracking = true;
            }
        }
        _yaw = Chase(_yaw, targetYaw, p.NeckAngleYSpeed * SmsUnits.BamToRad * dtFrames);
        if (Tracking)
        {
            float maxY = Mathf.Max(p.NeckMaxAngleY * SmsUnits.BamToRad, 1e-4f);
            float ratio = Mathf.Clamp(Mathf.Abs(_yaw) / maxY, 0f, 1f);                 // CLBCalcRatio(max, 0, |yaw|)
            float eased = Mathf.Lerp(0.5f * targetPitch, targetPitch, 1f - (1f - ratio) * (1f - ratio)); // CLBEaseOutInbetween
            targetPitch = eased;
        }
        _pitch = Chase(_pitch, targetPitch, p.NeckAngleXSpeed * SmsUnits.BamToRad * dtFrames);
        if (Mathf.Abs(_yaw) < 1e-5f && Mathf.Abs(_pitch) < 1e-5f) return;

        // right-handed: a turn about up by +a moves forward toward -side (up x fwd = -side), so the yaw toward +side is -yaw;
        // a turn about side by +a moves forward toward up (side x fwd = up)
        Basis rot = new Basis(new Quaternion(_up, -_yaw)) * new Basis(new Quaternion(_side, _pitch));
        pose.Basis = pose.Basis * rot;
        skel.SetBoneGlobalPose(_bone, pose);
    }
}
