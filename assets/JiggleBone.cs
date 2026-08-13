using Godot;

/// <summary>
/// Procedural spring-bone "jiggle" — recreates SMS's chain-bone (chn_*) secondary
/// motion. A virtual tip on the bone lags behind the bone's real (world) motion
/// via a spring + damper, then springs back and overshoots; the bone is rotated
/// to point at that lagging tip. Runs as a Skeleton modifier (AFTER the animation)
/// so it isn't overwritten each frame.
///
/// The sim runs in WORLD space, so it reacts to Mario actually moving/stopping/
/// turning in the level — not just the baked animation. All feel params are set
/// externally (by Mario) so they're trivial to fine-tune.
/// </summary>
public partial class JiggleBone : SkeletonModifier3D
{
    public string BoneName = "chn_chest";
    public string TipBoneName = ""; // descendant used as the swing "tip" (empty = first child)
    public float Stiffness = 120f; // spring return — higher = settles faster / tighter
    public float Damping = 12f; // wobble decay — higher = fewer bounces
    public float Response = 0.06f; // how strongly acceleration (start/stop/turn) kicks it
    public float MaxAngleDeg = 25f; // clamp on the swing
    public float Gain = 1.5f; // amplify the swing angle (visibility)
    public float Weight = 1f; // overall strength (0 = off)
    public float Dt = 1f / 60f; // frame delta, pushed by the owner
    // Hard ceiling on visible travel (metres) — the ONE dial that bounds how far
    // this can move no matter what Stiffness/Gain/Response end up set to. A loose
    // (low) Stiffness lets the spring travel further before it pulls back, so
    // turning Gain down alone doesn't reliably cap "how far" — this does.
    public float MaxShiftMeters = 0.05f;

    private int _bone = -1;
    private int _parent = -1;
    private Vector3 _tipLocal; // tip offset in the bone's local frame (skeleton units)
    private Transform3D _restLocal = Transform3D.Identity; // bone's rest local pose (the un-jiggled base)
    private Quaternion _restLocalRot = Quaternion.Identity;
    // Mario's HORIZONTAL body acceleration, computed on the PHYSICS tick and pushed
    // in each frame. Must NOT be differenced here: this modifier runs at render rate
    // (variable, faster than physics), so differencing velocity here would read 0 on
    // most frames and a divide-by-tiny-dt spike right after each physics tick.
    public Vector3 DriveAccel;
    public bool Debug;

    private Vector3 _simRel; // sim tip offset from the target (0 at rest)
    private Vector3 _simRelVel;
    private int _dbg;

    /// <summary>Kick the spring directly (world-space velocity) — for one-shot events
    /// like a landing impact that the continuous horizontal drive can't see.</summary>
    public void AddImpulse(Vector3 worldVel) => _simRelVel += worldVel;

    public override void _Ready()
    {
        var skel = GetSkeleton();
        if (skel == null)
            return;
        _bone = skel.FindBone(BoneName);
        if (_bone < 0)
            return;
        _parent = skel.GetBoneParent(_bone);
        _restLocal = skel.GetBoneRest(_bone);
        _restLocalRot = _restLocal.Basis.GetRotationQuaternion();

        // Tip = a descendant's rest position in this bone's local frame — the
        // "stick" we swing. Prefer TipBoneName (e.g. jnt_head), else the first
        // child; and if that lands ~on top of us (co-located chain root, like
        // chn_chest/jnt_chest), walk down until we find a real offset.
        int tip = TipBoneName != "" ? skel.FindBone(TipBoneName) : -1;
        if (tip < 0)
            for (int i = 0; i < skel.GetBoneCount(); i++)
                if (skel.GetBoneParent(i) == _bone)
                {
                    tip = i;
                    break;
                }

        Transform3D restG = skel.GetBoneGlobalRest(_bone);
        _tipLocal = new Vector3(0f, 10f, 0f);
        while (tip >= 0)
        {
            Vector3 cand = restG.AffineInverse() * skel.GetBoneGlobalRest(tip).Origin;
            if (cand.LengthSquared() > 1e-4f)
            {
                _tipLocal = cand;
                break;
            }
            // co-located with us — step down to this bone's first child.
            int next = -1;
            for (int i = 0; i < skel.GetBoneCount(); i++)
                if (skel.GetBoneParent(i) == tip)
                {
                    next = i;
                    break;
                }
            tip = next;
        }
    }

    public override void _ProcessModification()
    {
        if (_bone < 0 || Weight <= 0.0001f)
            return;
        var skel = GetSkeleton();
        if (skel == null)
            return;

        Transform3D skelG = skel.GlobalTransform;
        // Parent's live (animated) pose in skeleton space — we shift this bone
        // relative to it. Never read this bone's own current pose: chain bones have
        // no animation track, so reading it back would fold last frame's jiggle in.
        Transform3D parentGlobal =
            _parent >= 0 ? skel.GetBoneGlobalPose(_parent) : Transform3D.Identity;

        float dt = Mathf.Clamp(Dt, 0.0001f, 1f / 30f);

        // Drive off MARIO'S locomotion acceleration (computed on the physics tick,
        // horizontal only), NOT the tip's world motion — the tip is swamped by the
        // run animation (hip sway / bob), and his velocity is clean: ~0 at constant
        // speed, a real spike on stop / start / turn. The tip offset is a spring-
        // mass kicked opposite that acceleration (inertia), so only those events
        // move it. Held constant across the render frames of one physics tick, so
        // integrating acc*dt at render rate delivers exactly one tick's impulse.
        Vector3 acc = DriveAccel;
        const float maxAcc = 250f;
        if (acc.LengthSquared() > maxAcc * maxAcc)
            acc = acc.Normalized() * maxAcc;

        _simRelVel += (-_simRel * Stiffness - acc * Response) * dt;
        _simRelVel /= 1f + Damping * dt;
        _simRel += _simRelVel * dt;
        const float maxOff = 0.6f; // hard safety clamp (metres)
        if (_simRel.LengthSquared() > maxOff * maxOff)
            _simRel = _simRel.Normalized() * maxOff;

        // Apply the lag as a POSITIONAL shift of the bone — the whole upper body
        // sways/bobbles as a rigid unit (the SMS "shift around"), NOT a rotation of
        // the chest, which pivots the spine and reads as folding at the waist.
        // Convert the world-space lag offset into the bone's parent-local frame:
        //   world metres --(skelG⁻¹)--> skeleton units --(parentGlobal⁻¹)--> parent-local.
        Vector3 shiftWorld = _simRel * Gain;
        if (shiftWorld.LengthSquared() > MaxShiftMeters * MaxShiftMeters)
            shiftWorld = shiftWorld.Normalized() * MaxShiftMeters;
        Vector3 shiftSkel = skelG.Basis.Inverse() * shiftWorld;
        Vector3 shiftLocal = parentGlobal.Basis.Inverse() * shiftSkel;
        Vector3 newLocalPos = _restLocal.Origin + shiftLocal * Mathf.Clamp(Weight, 0f, 1f);

        // Purely positional — we never touch rotation, so the bone doesn't bend
        // (no waist-fold) and a rotation-owning modifier on the same bone (e.g.
        // HeadForwardLock on jnt_head) is free to coexist.
        skel.SetBonePosePosition(_bone, newLocalPos);

        if (Debug && ++_dbg % 12 == 0)
            GD.Print(
                $"[Jiggle:{BoneName}] acc={acc.Length():0.0} rel={_simRel.Length():0.000} shift_cm={shiftWorld.Length() * 100f:0.0}"
            );
    }
}
