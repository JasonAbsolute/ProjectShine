using Godot;

/// <summary>
/// Two-bone foot IK so Mario's feet plant ON a slope instead of both sitting on
/// one flat plane (downhill foot floating, uphill foot buried).
///
/// Runs as a <see cref="SkeletonModifier3D"/> — the same stage HeadForwardLock
/// and JiggleBone use — so it writes AFTER the AnimationTree and actually
/// sticks. Deliberately NOT SkeletonIK3D: that class is deprecated in 4.7 (it's
/// one of the build warnings on Mario.cs) and can't do the hip-drop step.
///
/// Order of operations each frame:
///   1. Raycast down under each foot for surface height + normal.
///   2. Drop the ROOT bone to whichever foot needs to reach lowest, so the far
///      leg isn't asked to over-extend. Root, not jnt_waist — see RootBone.
///   3. Two-bone solve per leg to put each ankle on its hit point.
///   4. Rotate each foot to lie along the surface normal.
///
/// <see cref="Weight"/> is driven externally by Mario (see UpdateFootIK) and is
/// eased in only for idle/pivot — matching SMS, which doesn't apply it while
/// walking or running. That also keeps it clear of the running waist IK, which
/// owns jnt_waist; the two must never be active at once.
/// </summary>
[GlobalClass]
public partial class FootIK : SkeletonModifier3D
{
    /// <summary>Bone the hip-drop is applied to. MUST be the skeleton ROOT
    /// (mdl1), not jnt_waist.
    ///
    /// Setting a bone's global-pose origin does not translate the character —
    /// it repositions that bone relative to its parent, i.e. it changes the
    /// bone's LENGTH. On a mid-chain bone like jnt_waist (mdl1 -> center ->
    /// jnt_waist) the skinned mesh stretches with it, which shows up as a
    /// visibly elongated torso. The root has no parent to stretch away from, so
    /// moving it translates the whole hierarchy rigidly — feet included, which
    /// is exactly what the hip drop wants: everything sinks, then each leg IK
    /// lifts its own foot back onto its (absolute, unmoved) target.</summary>
    [Export]
    public string RootBone = "mdl1";

    [Export]
    public string HipBoneR = "jnt_leg_R1";

    [Export]
    public string KneeBoneR = "jnt_leg_R2";

    [Export]
    public string AnkleBoneR = "chn_foot_R";

    [Export]
    public string FootBoneR = "jnt_foot_R";

    [Export]
    public string HipBoneL = "jnt_leg_L1";

    [Export]
    public string KneeBoneL = "jnt_leg_L2";

    [Export]
    public string AnkleBoneL = "chn_foot_L";

    [Export]
    public string FootBoneL = "jnt_foot_L";

    /// <summary>Driven by Mario. 0 = pure animation, 1 = fully IK'd.</summary>
    public float Weight;

    /// <summary>How far above the ankle the downward probe starts (metres).</summary>
    [Export]
    public float RayUp = 0.5f;

    /// <summary>How far below the ankle the probe reaches. Anything further is
    /// treated as "no ground" and that foot is left animated.</summary>
    [Export]
    public float RayDown = 0.9f;

    /// <summary>Ankle-to-sole distance — the ankle is placed this far above the
    /// surface hit so the foot rests on it rather than through it.</summary>
    [Export]
    public float FootOffset = 0.177706f;

    /// <summary>Most the hips are allowed to drop to let the low foot reach.
    /// Prevents a deep crouch when one foot finds a big hole.</summary>
    [Export]
    public float MaxHipDrop = 0.25f;

    /// <summary>How the correction is shared between the two legs.
    /// 0 = drop only to the AVERAGE of the two feet, so each leg takes half
    ///     (one extends, one folds) — neither foot lands exactly, but the pose
    ///     stays symmetric and natural.
    /// 1 = drop all the way to the LOWEST foot, so it plants perfectly and the
    ///     other leg has to fold up by the whole difference.
    /// Measured on a 33.8° slope: feet wanted -0.343m and -0.032m, so 1.0 means
    /// dropping a third of a metre and folding the near leg 0.31m — geometrically
    /// correct but it reads as a deep lopsided crouch.</summary>
    [Export(PropertyHint.Range, "0,1")]
    public float HipDropToLowest = 0.5f;

    /// <summary>Turn OFF to isolate the leg solve from the foot alignment.
    /// FootUpAxisLocal is a rig-specific guess, and if it's wrong the foot
    /// rotates toward a meaningless axis — which looks like the whole IK is
    /// broken when the legs may in fact be fine.</summary>
    [Export]
    public bool AlignFeetToSurface = true;

    /// <summary>Max degrees the foot is tilted to match the surface.</summary>
    [Export]
    public float MaxFootTiltDegrees = 45f;

    /// <summary>Bone-local axis pointing out of the sole. Leave
    /// AutoDeriveFootAxis on and this is filled in from the rig at startup;
    /// it's only an override for when that guess is wrong.</summary>
    [Export]
    public Vector3 FootUpAxisLocal = new(0f, 1f, 0f);

    /// <summary>Derive FootUpAxisLocal from the skeleton's REST pose instead of
    /// trusting the value above.
    ///
    /// In the rest pose the character stands upright with his feet flat, so the
    /// sole's outward normal is simply skeleton-up. The foot bone's global rest
    /// basis maps bone-local -> skeleton, so the local sole axis is exactly
    /// restBasis.Inverse() * Up. That's a lookup against the actual rig rather
    /// than a guess — which matters on a BMD -> Blender -> GLB rig where bone
    /// axes land wherever the exporter put them, and where a wrong axis rotates
    /// the foot toward something meaningless and snaps the ankle.</summary>
    [Export]
    public bool AutoDeriveFootAxis = true;

    /// <summary>Collision mask for the ground probe. Defaults to layer 1.</summary>
    [Export(PropertyHint.Layers3DPhysics)]
    public uint GroundMask = 1;

    /// <summary>Bodies to ignore — Mario himself, set by him at startup.</summary>
    public Godot.Collections.Array<Rid> ExcludeBodies = new();

    private int _root = -1;
    private int[] _hip = { -1, -1 };
    private int[] _knee = { -1, -1 };
    private int[] _ankle = { -1, -1 };
    private int[] _foot = { -1, -1 };

    public override void _Ready()
    {
        var skel = GetSkeleton();
        if (skel == null)
            return;

        _root = skel.FindBone(RootBone);
        _hip[0] = skel.FindBone(HipBoneR);
        _knee[0] = skel.FindBone(KneeBoneR);
        _ankle[0] = skel.FindBone(AnkleBoneR);
        _foot[0] = skel.FindBone(FootBoneR);
        _hip[1] = skel.FindBone(HipBoneL);
        _knee[1] = skel.FindBone(KneeBoneL);
        _ankle[1] = skel.FindBone(AnkleBoneL);
        _foot[1] = skel.FindBone(FootBoneL);

        if (AutoDeriveFootAxis && _foot[0] >= 0)
        {
            Basis restBasis = skel.GetBoneGlobalRest(_foot[0]).Basis;
            Vector3 derived = restBasis.Inverse() * Vector3.Up;
            if (derived.LengthSquared() > 1e-8f)
            {
                FootUpAxisLocal = derived.Normalized();
            }
        }

        for (int s = 0; s < 2; s++)
        {
            if (_hip[s] < 0 || _knee[s] < 0 || _ankle[s] < 0)
            {
                GD.PushError(
                    $"FootIK: leg chain {s} not found — check the bone name exports "
                        + "against the rig (expected jnt_leg_*1 / jnt_leg_*2 / chn_foot_*)."
                );
            }
        }
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        if (Weight <= 0.0001f)
            return;

        var skel = GetSkeleton();
        if (skel == null || _root < 0)
            return;

        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return;

        float w = Mathf.Clamp(Weight, 0f, 1f);

        Transform3D skelToWorld = skel.GlobalTransform;
        Transform3D worldToSkel = skelToWorld.AffineInverse();

        // UNITS. The armature carries a ~0.0134 scale, so skeleton space is
        // about 74x LARGER than world space — bone positions in this rig are
        // values like 52.758, not metres. Every tunable below (FootOffset,
        // MaxHipDrop) is authored in world metres because that's the only unit
        // that means anything to a human, so each has to be converted before
        // being used against bone positions. Getting this wrong doesn't fail
        // loudly: it just scales every correction down by 74x, which reads as
        // the IK doing nothing at all.
        //
        // The length of the transformed up vector IS the conversion factor, so
        // this self-adjusts if the rig scale ever changes.
        Vector3 upSkelRaw = worldToSkel.Basis * Vector3.Up;
        float metresToSkel = upSkelRaw.Length();
        if (metresToSkel < 1e-6f)
            return;
        Vector3 upSkel = upSkelRaw / metresToSkel;

        float footOffsetSkel = FootOffset * metresToSkel;
        float maxHipDropSkel = MaxHipDrop * metresToSkel;

        // ---- 1. probe under each foot --------------------------------------
        // The animation's own foot lift is measured against the REST POSE, not
        // against the world. Referencing the world was wrong: on a slope the
        // collision cylinder rests on its uphill rim and floats the body ~0.2m,
        // so both ankles read as high off the ground and that float got
        // preserved as if the animation had lifted them — Mario standing on air.
        // Rest-relative lift is pure animation and carries no terrain in it.

        Vector3[] target = new Vector3[2];
        Vector3[] normal = new Vector3[2];
        bool[] hit = new bool[2];
        float[] drop = { 0f, 0f };

        // Every bone pose is snapshotted HERE, before a single write happens.
        // Re-reading after the root drop below is not safe: the skeleton's
        // global-pose cache is not guaranteed to refresh mid-modification, so a
        // later GetBoneGlobalPose can hand back the PRE-drop position.
        Transform3D[] hipG = new Transform3D[2];
        Transform3D[] kneeG = new Transform3D[2];
        Transform3D[] ankleG = new Transform3D[2];
        Transform3D[] hipParentG = new Transform3D[2];
        for (int s = 0; s < 2; s++)
        {
            if (_hip[s] < 0 || _knee[s] < 0 || _ankle[s] < 0)
                continue;
            hipG[s] = skel.GetBoneGlobalPose(_hip[s]);
            kneeG[s] = skel.GetBoneGlobalPose(_knee[s]);
            ankleG[s] = skel.GetBoneGlobalPose(_ankle[s]);
            int par = skel.GetBoneParent(_hip[s]);
            hipParentG[s] = par >= 0 ? skel.GetBoneGlobalPose(par) : Transform3D.Identity;
        }

        for (int s = 0; s < 2; s++)
        {
            if (_ankle[s] < 0)
                continue;

            Vector3 ankleSkel = ankleG[s].Origin;
            Vector3 ankleWorld = skelToWorld * ankleSkel;

            var q = PhysicsRayQueryParameters3D.Create(
                ankleWorld + Vector3.Up * RayUp,
                ankleWorld + Vector3.Down * RayDown,
                GroundMask
            );
            q.Exclude = ExcludeBodies;

            var res = space.IntersectRay(q);
            if (res.Count == 0)
                continue;

            hit[s] = true;
            Vector3 hitSkel = worldToSkel * (Vector3)res["position"];
            normal[s] = (worldToSkel.Basis * ((Vector3)res["normal"]).Normalized()).Normalized();

            // How far the ANIMATION has lifted this foot relative to its rest
            // height. Clamped at 0 so a pose that sinks a foot below its rest
            // stance still gets planted rather than pushed further down.
            float restH = skel.GetBoneGlobalRest(_ankle[s]).Origin.Y;
            float lift = Mathf.Max(0f, ankleSkel.Y - restH);

            // Same lift, but re-referenced to the ground under THIS foot.
            target[s] = hitSkel + upSkel * (footOffsetSkel + lift);
            drop[s] = (target[s] - ankleSkel).Dot(upSkel);
        }

        // NOTE: there is deliberately no "is this foot being lifted on purpose"
        // heuristic here. The obvious one — release a foot that sits far above
        // its target — cannot work on this rig: measured slope corrections reach
        // 0.35m while a deliberate step lifts maybe 0.15-0.25m, so the two ranges
        // overlap and any threshold either eats the slope planting or fails to
        // release a step. Foot IK is instead gated to IDLE only (see Mario's
        // UpdateFootIK), where every foot genuinely is planted.
        float[] footW = { w, w };

        if (!hit[0] && !hit[1])
            return;

        // ---- 2. drop the whole rig to the foot that has to reach lowest ----
        // Only ever DOWN: raising the hips would lift the planted foot off the
        // ground it's standing on. Both legs hang off the same jnt_waist, so
        // this moves them together before either leg solves.
        float lowest = 0f;
        float sum = 0f;
        int n = 0;
        for (int s = 0; s < 2; s++)
        {
            if (!hit[s])
                continue;
            lowest = Mathf.Min(lowest, drop[s]);
            sum += drop[s];
            n++;
        }
        float average = n > 0 ? sum / n : 0f;

        // Share the correction (see HipDropToLowest) rather than always sinking
        // to the lowest foot.
        float chosen = Mathf.Lerp(Mathf.Min(average, 0f), lowest, HipDropToLowest);
        float hipDrop = Mathf.Clamp(chosen, -maxHipDropSkel, 0f) * w;
        if (Mathf.Abs(hipDrop) > 0.00001f)
        {
            Transform3D rootG = skel.GetBoneGlobalPose(_root);
            rootG.Origin += upSkel * hipDrop;
            skel.SetBoneGlobalPose(_root, rootG);
        }

        // ---- 3 + 4. solve each leg, then lay the foot on the surface ------
        for (int s = 0; s < 2; s++)
        {
            if (!hit[s] || _hip[s] < 0 || _knee[s] < 0 || _ankle[s] < 0)
                continue;

            // The root moved by hipDrop, so every cached leg position moved
            // with it. Apply that offset explicitly rather than re-reading.
            Vector3 shift = upSkel * hipDrop;
            Transform3D hipParentShifted = hipParentG[s];
            hipParentShifted.Origin += shift;

            SolveLeg(
                skel,
                _hip[s],
                _knee[s],
                hipParentShifted,
                hipG[s].Origin + shift,
                kneeG[s].Origin + shift,
                ankleG[s].Origin + shift,
                hipG[s].Basis,
                kneeG[s].Basis,
                target[s],
                footW[s]
            );

            if (AlignFeetToSurface && _foot[s] >= 0)
                AlignFoot(skel, _foot[s], normal[s], footW[s]);
        }
    }

    /// <summary>
    /// Analytic two-bone IK. Everything is in SKELETON space.
    ///
    /// The bend plane is taken from wherever the animation already had the knee,
    /// rather than from a pole target — that keeps the knee bending the way the
    /// clip intended and avoids a rig-specific pole vector that would need
    /// re-tuning per animation.
    /// </summary>
    private static void SolveLeg(
        Skeleton3D skel,
        int hip,
        int knee,
        Transform3D hipParentGlobal,
        Vector3 H,
        Vector3 K,
        Vector3 A,
        Basis hipBasis,
        Basis kneeBasis,
        Vector3 targetPos,
        float w
    )
    {

        float l1 = (K - H).Length();
        float l2 = (A - K).Length();
        if (l1 < 1e-6f || l2 < 1e-6f)
            return;

        // Blend the goal in by weight so easing Weight in/out doesn't pop.
        Vector3 goal = A.Lerp(targetPos, w);

        Vector3 toGoal = goal - H;
        float dist = toGoal.Length();
        if (dist < 1e-6f)
            return;

        // Clamp inside the reachable annulus, leaving a sliver so the leg never
        // locks perfectly straight (which makes the knee direction undefined).
        float minReach = Mathf.Abs(l1 - l2) + 1e-4f;
        float maxReach = l1 + l2 - 1e-4f;
        float d = Mathf.Clamp(dist, minReach, maxReach);
        Vector3 axis = toGoal / dist;

        // Bend direction, derived from the PLANE of the animated leg rather than
        // from the perpendicular component of hip->knee.
        //
        // The obvious version — (K-H) minus its projection on the new axis —
        // degenerates exactly when the leg has to STRAIGHTEN: that perpendicular
        // component shrinks toward zero, so normalizing it amplifies noise and
        // the knee direction becomes unstable or flips. That's why the downhill
        // foot (the leg reaching furthest, so the straightest) misbehaved while
        // the uphill one, which stays bent, looked fine.
        //
        // The animated pose ALWAYS has a bent knee, so its plane normal
        // (K-H) x (A-H) is well conditioned no matter what the new axis is.
        // Take the bend as perpendicular-to-axis within that plane, then flip it
        // to whichever side the original knee was on.
        Vector3 planeN = (K - H).Cross(A - H);
        Vector3 bend;
        if (planeN.LengthSquared() > 1e-10f)
        {
            bend = planeN.Normalized().Cross(axis);
            if (bend.Dot(K - H) < 0f)
                bend = -bend;
        }
        else
        {
            // Animated leg is perfectly straight too — no plane to recover.
            bend = (K - H) - (K - H).Dot(axis) * axis;
        }

        if (bend.LengthSquared() < 1e-8f)
        {
            bend = axis.Cross(Vector3.Right);
            if (bend.LengthSquared() < 1e-8f)
                bend = axis.Cross(Vector3.Up);
        }
        bend = bend.Normalized();

        // Law of cosines for the hip angle, then place the knee.
        float cosHip = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
        float hipAngle = Mathf.Acos(cosHip);
        Vector3 newK = H + axis * (l1 * Mathf.Cos(hipAngle)) + bend * (l1 * Mathf.Sin(hipAngle));
        Vector3 newA = H + axis * d;

        // Rotate the hip so hip->knee lands on hip->newKnee...
        Quaternion qHip = FromTo((K - H).Normalized(), (newK - H).Normalized());

        // ...then the knee, measured AFTER the hip has turned. Computing the
        // post-hip ankle analytically avoids depending on the skeleton cache
        // refreshing between the two SetBoneGlobalPose calls below.
        Vector3 aAfterHip = H + qHip * (A - H);
        Quaternion qKnee = FromTo(
            (aAfterHip - newK).Normalized(),
            (newA - newK).Normalized()
        );

        // Write LOCAL poses, converted using OUR OWN parent globals.
        //
        // SetBoneGlobalPose would convert global->local using the skeleton's
        // cached parent global, which is stale here: the root drop above shifted
        // every descendant, but that cache is not guaranteed to have caught up
        // mid-modification. The hip would then land short by exactly the shift —
        // body sinks, legs don't follow, feet read as detached. Doing the
        // conversion ourselves depends on nothing the skeleton has cached.
        Transform3D hipGlobal = new Transform3D(new Basis(qHip) * hipBasis, H);
        Transform3D kneeGlobal = new Transform3D(
            new Basis(qKnee) * new Basis(qHip) * kneeBasis,
            newK
        );

        SetLocalPose(skel, hip, hipParentGlobal.AffineInverse() * hipGlobal);
        SetLocalPose(skel, knee, hipGlobal.AffineInverse() * kneeGlobal);
    }

    /// <summary>Writes a bone's pose from an explicit LOCAL transform, split into
    /// the position/rotation/scale channels the pose API exposes.</summary>
    private static void SetLocalPose(Skeleton3D skel, int bone, Transform3D local)
    {
        skel.SetBonePosePosition(bone, local.Origin);
        skel.SetBonePoseRotation(bone, local.Basis.GetRotationQuaternion());
        skel.SetBonePoseScale(bone, local.Basis.Scale);
    }

    /// <summary>Tilts the foot bone so its sole axis matches the surface normal,
    /// clamped so a steep face doesn't wrench the ankle past what the rig can
    /// plausibly do.</summary>
    private void AlignFoot(Skeleton3D skel, int foot, Vector3 normalSkel, float w)
    {
        Transform3D footG = skel.GetBoneGlobalPose(foot);

        Vector3 soleAxis = FootUpAxisLocal.LengthSquared() > 1e-6f
            ? FootUpAxisLocal.Normalized()
            : Vector3.Up;

        Vector3 cur = (footG.Basis * soleAxis).Normalized();
        if (cur.LengthSquared() < 1e-8f || normalSkel.LengthSquared() < 1e-8f)
            return;

        float ang = cur.AngleTo(normalSkel);
        float maxAng = Mathf.DegToRad(MaxFootTiltDegrees);
        Vector3 goalDir = ang > maxAng ? cur.Slerp(normalSkel, maxAng / ang) : normalSkel;

        Quaternion q = FromTo(cur, goalDir);
        q = Quaternion.Identity.Slerp(q, w);
        skel.SetBoneGlobalPose(foot, new Transform3D(new Basis(q) * footG.Basis, footG.Origin));
    }

    /// <summary>Shortest-arc rotation taking unit vector <paramref name="a"/> to
    /// unit vector <paramref name="b"/>, with the antiparallel case handled
    /// explicitly (the cross product vanishes there).</summary>
    private static Quaternion FromTo(Vector3 a, Vector3 b)
    {
        float dot = Mathf.Clamp(a.Dot(b), -1f, 1f);
        if (dot > 0.999999f)
            return Quaternion.Identity;

        if (dot < -0.999999f)
        {
            Vector3 perp = a.Cross(Vector3.Up);
            if (perp.LengthSquared() < 1e-8f)
                perp = a.Cross(Vector3.Right);
            return new Quaternion(perp.Normalized(), Mathf.Pi);
        }

        Vector3 axis = a.Cross(b).Normalized();
        return new Quaternion(axis, Mathf.Acos(dot));
    }
}
