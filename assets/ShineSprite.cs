using Godot;

/// <summary>
/// Shine Sprite: idle float + pickup detection + the collect cutscene, all
/// driven by the model's own baked animations now (ShineSprite.glb ships with
/// real textures/materials and three animations — shine_float, shine_demo_shine_get,
/// shine_demo_shine_get_yo — so no procedural materials or hand-rolled motion
/// are needed here anymore).
///
/// One thing the export lost: color. Both textures are grayscale intensity
/// maps and there's no vertex-color attribute on any primitive, so the model
/// renders white/silver out of the box. ApplyColorTint() multiplies the real
/// imported materials (by name — "_starglow1"/"_mat_shine_body") with a gold
/// color, keeping their actual textures/alpha intact. [Tool] so this previews
/// in the editor; it's idempotent (safe to rerun every load).
///
/// A "PickupZone" Area3D child reports Mario contact to Mario.TryCollectShine().
/// That claims the shine (disables  pickup zone) but doesn't necessarily
/// start the cutscene right away — if Mario is airborne, Mario waits until he
/// lands before calling BeginCollectSequence() here. Once it does start, this
/// plays shine_demo_shine_get (its length matches Mario's own ma_demo_shine_get
/// exactly — both ripped from the same source cutscene) and frees itself when
/// that animation finishes; no further callback needed for that part.
/// </summary>
[Tool]
public partial class ShineSprite : Node3D
{
    [Signal] public delegate void SpawnEntranceFinishedEventHandler();

    /// <summary>How long a newly awarded Shine takes to appear.</summary>
    [Export] public float SpawnEntranceDuration = 1.15f;

    /// <summary>Vertical distance the Shine rises while appearing.</summary>
    [Export] public float SpawnEntranceRise = 1.5f;

    /// <summary>Full turns made while the Shine appears.</summary>
    [Export] public float SpawnEntranceTurns = 2f;

    [Export]
    public string FloatAnimName = "shine_float";

    [Export]
    public string CollectAnimName = "shine_demo_shine_get";

    [Export]
    public Color GlowTint = new Color(1.0f, 0.85f, 0.4f);

    [Export]
    public Color BodyTint = new Color(1.0f, 0.8f, 0.2f);

    // NO hand-socket attach. The star geometry hangs off a bone with a ~71
    // raw-unit rest offset (≈1.1m at 0.016 scale) that shine_demo_shine_get
    // rotates around the ROOT — the baked animation IS the star's whole
    // choreography (reach height, low swing, overhead lift), authored as the
    // synced pair of ma_demo_shine_get with the root pinned at Mario's feet.
    // Attaching the root to his moving hand double-applies motion and sweeps
    // the star up to a meter away from the hand (at the final pose it ended
    // up under the floor — took a scale/visibility log to find). Pinning the
    // root at Mario's origin and letting the animation do the rest is both
    // simpler and the actual 1:1 behavior.
    /// <summary>Extra yaw (degrees) added to Mario's facing when orienting the
    /// shine for the cutscene — flip 180 if its baked routine plays out
    /// mirrored/backwards relative to him.</summary>
    [Export]
    public float CutsceneFaceYawOffsetDeg = 180f;

    // Reference video of the real cutscene: when it starts, the shine is HIGH
    // in the sky in front of Mario and descends to him over the camera's
    // behind-the-shoulder beat (~2.4s), reaching his hand right at the grab
    // moment in ma_demo_shine_get. None of that motion is baked into
    // shine_demo_shine_get (its translation tracks are static — verified in
    // the glb), so the descent is scripted here. Offsets are Mario-local
    // world-units, -Z = in front of him (same convention as the camera rail).
    // Kept so the scene's existing overrides still resolve (harmless if unused).
    [Export]
    public float FlyDownDuration = 2.4f;

    [Export]
    public Vector3 FlyDownStartOffset = new(-0.5f, 5.5f, -3.0f);

    /// <summary>Where the shine sits relative to Mario during the cutscene
    /// (his-local: +X right, -Z in front, +Y up). Tracked live off his
    /// position + facing every frame.</summary>
    [Export]
    public Vector3 FlyDownGrabOffset = new(0f, 0f, 0f);

    /// <summary>Idle spin speed while floating in the level (radians/sec).</summary>
    [Export]
    public float IdleSpinSpeed = 2.4f;

    /// <summary>Spin speed while held during the cutscene (radians/sec).</summary>
    [Export]
    public float CutsceneSpinSpeed = 3.2f;

    /// <summary>How long the end float-up + shrink-away takes (seconds).</summary>
    [Export]
    public float EndShrinkDuration = 1.0f;

    /// <summary>How far the shine floats upward (world metres) as it shrinks away
    /// after the cutscene.</summary>
    [Export]
    public float EndFloatUpHeight = 2.0f;

    private bool _collected;
    private Area3D _pickupZone;
    private AnimationPlayer _animPlayer;

    private bool _spawnEntrancePlaying;
    private bool _pausedTreeForSpawnEntrance;
    private ProcessModeEnum _processModeBeforeSpawnEntrance;
    private Tween _spawnEntranceTween;

    // Cutscene tracking / end state.
    private Mario _mario;
    private bool _inCutscene;
    private bool _ending;

    // --- Glow / "shiny" VFX (built in code, like GroundPoundEffects/SpinJumpEffects) ---
    // All parented under a single counter-scale node so the effects can be
    // authored in real metres despite the shine root's tiny 0.006 scale, and so
    // they follow the shine automatically (idle spin, cutscene tracking, and the
    // end shrink all just work because they're children of this node/root).
    [Export]
    public bool EnableShineEffects = true;

    [Export]
    public Color ShineGlowColor = new Color(1.0f, 0.85f, 0.42f);

    /// <summary>Fine vertical nudge (world metres) if the glow package sits
    /// slightly off the star's visual center after auto-calibration.</summary>
    [Export]
    public float ShineFxYNudge = 0f;

    private Node3D _fx;
    private MeshInstance3D _glow;
    private MeshInstance3D _ring;
    private MeshInstance3D _rays;
    private OmniLight3D _light;
    private GpuParticles3D _sparkles;
    private float _fxTime;
    private float _baseLightEnergy;

    // Follows the star mesh through the baked collect animation: an invisible
    // marker pinned to the star's skeleton bone. _fx is driven to its world
    // position each frame so the whole glow package rides the star's reach/swing.
    // The star body mesh is rigidly skinned (100%) to the "body" bone, but the
    // bone's origin sits near the TOP of the star — so the FX emits from the
    // body-surface CENTROID instead (a marker pinned into the bone at the mesh's
    // center of mass), which rides the collect animation exactly.
    private Skeleton3D _starSkel;
    private int _starBoneIdx = -1;
    private Marker3D _meshCenter;
    private MeshInstance3D _starMeshInst;
    private Node3D _pickupShapeDbg;
    private bool _fxCenterLogged;

    // Neutral (idle) bone pose captured at startup, restored at the end so the
    // shine drops the baked grab animation's final pose and just spins as it rises.
    private Vector3[] _neutralBonePos;
    private Quaternion[] _neutralBoneRot;
    private Vector3[] _neutralBoneScale;

    public override void _Ready()
    {
        // Blender fast64-addon export artifact (a material-preview plane), not
        // part of the actual model — hide it rather than leaving it floating
        // in the scene.
        GetNodeOrNull<MeshInstance3D>("fast64_f3d_material_library_PlaneObject")
            ?.Hide();

        ApplyColorTint();

        if (Engine.IsEditorHint())
            return;

        // Stop any baked animation — the shine is spun/positioned in code now
        // (idle spin, cutscene tracking, end shrink), not by the glb animations.
        _animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        _animPlayer?.Stop();

        _pickupZone = GetNodeOrNull<Area3D>("PickupZone");
        if (_pickupZone != null)
            _pickupZone.BodyEntered += OnPickupBodyEntered;
        else
            GD.PushWarning(
                "ShineSprite: no PickupZone Area3D child found — this shine can't be collected."
            );

        if (EnableShineEffects)
            BuildShineEffects();

        CaptureNeutralPose();
    }

    /// <summary>
    /// Reveals a newly awarded Shine while gameplay is frozen. This node switches
    /// to Always processing so its tween and effects continue while SceneTree is
    /// paused, then restores both its prior process mode and the tree pause state.
    /// </summary>
    public void PlaySpawnEntrance()
    {
        if (_spawnEntrancePlaying || _collected)
            return;

        var tree = GetTree();
        if (tree == null)
            return;

        _spawnEntrancePlaying = true;
        _processModeBeforeSpawnEntrance = ProcessMode;
        ProcessMode = ProcessModeEnum.Always;

        if (_pickupZone != null)
            _pickupZone.Monitoring = false;

        _pausedTreeForSpawnEntrance = !tree.Paused;
        if (_pausedTreeForSpawnEntrance)
            tree.Paused = true;

        Vector3 targetScale = Scale;
        Vector3 targetPosition = Position;
        Vector3 targetRotation = Rotation;
        float duration = Mathf.Max(SpawnEntranceDuration, 0.01f);

        // Keep the basis invertible while visually starting near zero. The
        // Shine's FX code uses child global transforms during this tween.
        Scale = targetScale * 0.05f;
        Position = targetPosition - Vector3.Up * SpawnEntranceRise;
        Rotation = new Vector3(
            targetRotation.X,
            targetRotation.Y - Mathf.Pi * 2f * SpawnEntranceTurns,
            targetRotation.Z
        );

        _spawnEntranceTween = CreateTween();
        _spawnEntranceTween.SetPauseMode(Tween.TweenPauseMode.Process);
        _spawnEntranceTween.SetParallel(true);
        _spawnEntranceTween.TweenProperty(this, "scale", targetScale, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        _spawnEntranceTween.TweenProperty(this, "position", targetPosition, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        _spawnEntranceTween.TweenProperty(this, "rotation", targetRotation, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        _spawnEntranceTween.Finished += FinishSpawnEntrance;

        GD.Print($"[ShineSprite] spawn entrance started; world_paused={tree.Paused}");
    }

    private void FinishSpawnEntrance()
    {
        if (!_spawnEntrancePlaying)
            return;

        _spawnEntrancePlaying = false;
        _spawnEntranceTween = null;

        if (_pickupZone != null)
            _pickupZone.Monitoring = true;

        ProcessMode = _processModeBeforeSpawnEntrance;

        var tree = GetTree();
        if (_pausedTreeForSpawnEntrance && tree != null)
            tree.Paused = false;
        _pausedTreeForSpawnEntrance = false;

        EmitSignal(SignalName.SpawnEntranceFinished);
        GD.Print($"[ShineSprite] spawn entrance finished; world_paused={tree?.Paused}");
    }

    public override void _ExitTree()
    {
        // Never strand the game paused if a scene transition removes the Shine
        // before its entrance tween completes.
        if (_spawnEntrancePlaying && _pausedTreeForSpawnEntrance)
        {
            var tree = GetTree();
            if (tree != null)
                tree.Paused = false;
        }
    }

    /// <summary>Snapshots the skeleton's idle bone pose (after the AnimationPlayer
    /// is stopped, so it reflects the scene's authored/override pose) so the end
    /// sequence can restore it — dropping the baked grab animation's final pose.</summary>
    private void CaptureNeutralPose()
    {
        var skel = _starSkel ?? GetNodeOrNull<Skeleton3D>("Armature/Skeleton3D");
        if (skel == null)
            return;
        _starSkel = skel;

        int n = skel.GetBoneCount();
        _neutralBonePos = new Vector3[n];
        _neutralBoneRot = new Quaternion[n];
        _neutralBoneScale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            _neutralBonePos[i] = skel.GetBonePosePosition(i);
            _neutralBoneRot[i] = skel.GetBonePoseRotation(i);
            _neutralBoneScale[i] = skel.GetBonePoseScale(i);
        }
    }

    /// <summary>Stops the baked animation and restores the captured idle pose so
    /// the star reads as the normal floating shine (centered on its spin axis)
    /// instead of frozen in the overhead grab pose.</summary>
    private void RestoreNeutralPose()
    {
        _animPlayer?.Stop();
        if (_starSkel == null || _neutralBonePos == null)
            return;
        for (int i = 0; i < _neutralBonePos.Length; i++)
        {
            _starSkel.SetBonePosePosition(i, _neutralBonePos[i]);
            _starSkel.SetBonePoseRotation(i, _neutralBoneRot[i]);
            _starSkel.SetBonePoseScale(i, _neutralBoneScale[i]);
        }
        _starSkel.ForceUpdateAllBoneTransforms(); // so GetBoneGlobalPose reflects the reset now
    }

    /// <summary>The star's mesh-center world position computed straight from the
    /// live bone global pose (so it's valid immediately after a pose change,
    /// unlike the BoneAttachment marker which syncs a frame later).</summary>
    private Vector3 MeshCenterWorldDirect()
    {
        if (_starSkel == null || _starBoneIdx < 0 || _meshCenter == null)
            return GlobalPosition;
        Vector3 c = _starSkel.GlobalTransform * _starSkel.GetBoneGlobalPose(_starBoneIdx) * _meshCenter.Position;
        if (_starMeshInst != null)
            c += _starSkel.GlobalTransform.Basis * _starMeshInst.Position;
        return c;
    }

    /// <summary>
    /// Tints the real imported materials by name rather than replacing them —
    /// keeps their actual texture/alpha/blend-mode, just multiplies in color
    /// the export lost. "_mat_shine_eyes" is left alone; its texture already
    /// reads correctly as a dark eye with a highlight.
    /// </summary>
    private void ApplyColorTint()
    {
        TintMaterialsRecursive(this);
    }

    private void TintMaterialsRecursive(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            int count = mesh.Mesh.GetSurfaceCount();
            for (int i = 0; i < count; i++)
            {
                if (mesh.GetActiveMaterial(i) is not BaseMaterial3D mat)
                    continue;

                // The glow dome ("_starglow1", now "_starglow_hidden" in
                // shine_sprite.tscn) is deliberately NOT tinted — it's been
                // hidden entirely (alpha 0) per reference comparison: the real
                // shine has no soft dome around it, just the solid star.
                // Re-tinting it here would set its albedo alpha back to 1 and
                // bring it back.
                Color? tint = mat.ResourceName switch
                {
                    "_mat_shine_body" => BodyTint,
                    _ => null,
                };
                if (tint == null)
                    continue;

                var dup = (BaseMaterial3D)mat.Duplicate();
                dup.AlbedoColor = tint.Value;
                mesh.SetSurfaceOverrideMaterial(i, dup);
            }
        }

        foreach (Node child in node.GetChildren())
            TintMaterialsRecursive(child);
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        float d = (float)delta;

        // Glow/rays/light breathe in every state, and the star-follow marker
        // calibrates here (the actual FX pin is applied at the end, below).
        AnimateShineEffects(d);

        if (_spawnEntrancePlaying)
        {
            // The entrance tween owns position, rotation, and scale while the
            // rest of the tree is paused.
        }
        else if (_ending)
        {
            // Idle spin while it floats up and shrinks away.
            RotateY(IdleSpinSpeed * d);
        }
        else if (_inCutscene && _mario != null && IsInstanceValid(_mario))
        {
            // Cutscene: track Mario's live position + facing so the shine stays put
            // relative to him. The baked shine_demo_shine_get animation drives the
            // visual motion (spin/swing), so we DON'T code-spin here — just keep
            // the root positioned + oriented to Mario.
            float yaw = _mario.CurrentFacingYaw;
            GlobalPosition = _mario.GlobalPosition + FlyDownGrabOffset.Rotated(Vector3.Up, yaw);
            Rotation = new Vector3(0f, yaw + Mathf.DegToRad(CutsceneFaceYawOffsetDeg), 0f);
        }
        else if (!_collected)
        {
            // Idle: spin in place while floating in the level.
            RotateY(IdleSpinSpeed * d);
        }

        // Pin the FX to the star LAST, after this frame's root transform is set,
        // so root motion during the cutscene can't shear the effects off-center.
        UpdateShineFxPosition();
    }

    private void OnPickupBodyEntered(Node3D body)
    {
        if (_collected)
            return;
        if (body is not Mario mario)
            return;

        if (mario.TryCollectShine(this))
        {
            _collected = true;
            _pickupZone.SetDeferred(Area3D.PropertyName.Monitoring, false);
        }
    }

    /// <summary>Called by Mario once the cutscene should actually start — either
    /// immediately (he was already grounded) or after he lands (see
    /// Mario._pendingShine). visualYaw is Mario's facing in the same convention
    /// as the camera rail (-Z rotated by it = in front of him), so the descent
    /// happens where the behind-the-shoulder camera is looking.
    ///
    /// Flow: the shine spins (shine_float) through a scripted sky→hand descent
    /// over FlyDownDuration, then PARENTS to Mario's raised-hand socket so his
    /// ma_demo_shine_get hand animation carries it up beside his head for the
    /// hero pose. It frees itself when Mario's animation ends (Mario tells it,
    /// via OnCollectSequenceEnd) — the shine's own animations loop, so they
    /// can't signal completion.</summary>
    /// <summary>Called by Mario once the cutscene starts. The shine snaps to his
    /// hand and, from here on, tracks his live position + facing (see _Process),
    /// spinning in place — no baked animation.</summary>
    public void BeginCollectSequence(Mario mario, float visualYaw)
    {
        _mario = mario;
        _inCutscene = true;

        // Orient + snap to Mario, then play the baked collect routine.
        Rotation = new Vector3(0f, visualYaw + Mathf.DegToRad(CutsceneFaceYawOffsetDeg), 0f);
        GlobalPosition = mario.GlobalPosition + FlyDownGrabOffset.Rotated(Vector3.Up, visualYaw);

        if (_animPlayer != null)
        {
            var anim = _animPlayer.GetAnimation(CollectAnimName);
            if (anim != null)
                anim.LoopMode = Animation.LoopModeEnum.None;
            _animPlayer.Play(CollectAnimName);
        }
    }

    /// <summary>Called by Mario when his ma_demo_shine_get finishes. Floats
    /// straight up while spinning forward and shrinking away, then frees itself.</summary>
    public void OnCollectSequenceEnd()
    {
        if (_ending)
            return;
        _ending = true;
        _inCutscene = false;

        // Drop the baked grab animation's final pose so the star reads as the
        // normal floating shine — but compensate the root so the star doesn't
        // visibly jump/drop from the overhead grab pose to its neutral center.
        Vector3 starBefore = MeshCenterWorldDirect();
        RestoreNeutralPose();
        Vector3 starAfter = MeshCenterWorldDirect();
        GlobalPosition += starBefore - starAfter; // reset to idle pose without the star jumping

        // As it shrinks, the star collapses toward the root origin (which sits well
        // below the star), so target the root's final Y at the star's CURRENT Y +
        // the rise, and use the SAME easing for position and scale. The star then
        // rises by exactly EndFloatUpHeight with no dip as it shrinks away.
        float targetY = starBefore.Y + EndFloatUpHeight;
        var t = CreateTween();
        t.SetParallel(true);
        t.TweenProperty(this, "global_position:y", targetY, EndShrinkDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        t.TweenProperty(this, "scale", Vector3.Zero, EndShrinkDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        t.Chain().TweenCallback(Callable.From(QueueFree));
    }

    // ---------------------------------------------------------------------
    //  Glow / "shiny" VFX
    // ---------------------------------------------------------------------

    /// <summary>
    /// Builds the shine's glow package in code: a warm point light, a soft
    /// additive halo, camera-facing rotating rays (shine_rays.gdshader), and a
    /// steady stream of twinkling sparkles. Everything hangs off one
    /// counter-scale node so it's authored in real metres (the shine root is
    /// scaled ~0.006) and centered on the star geometry (~200 local units up),
    /// and so it inherits the shine's motion for free.
    /// </summary>
    private void BuildShineEffects()
    {
        // Undo the root's tiny scale so children below are authored in metres.
        float rootScale = Mathf.Abs(Scale.X) > 0.0001f ? Scale.X : 0.006f;
        float inv = 1f / rootScale;

        _fx = new Node3D { Name = "ShineFX" };
        AddChild(_fx);
        // Star geometry sits ~200 local units above the node (same space the
        // PickupZone shape uses); park the effects there so they wrap the star.
        _fx.Position = new Vector3(0f, 200f, 0f);
        _fx.Scale = new Vector3(inv, inv, inv);

        // Warm light so the shine actually casts a glow onto nearby surfaces.
        _light = new OmniLight3D
        {
            Name = "ShineLight",
            LightColor = ShineGlowColor,
            LightEnergy = 1.6f,
            OmniRange = 4.5f,
            OmniAttenuation = 1.4f,
            ShadowEnabled = false,
        };
        _baseLightEnergy = _light.LightEnergy;
        _fx.AddChild(_light);

        // Soft round halo (additive billboard) — the "bloom" around the star.
        // Kept dim (~half) so it reads as a translucent glow, not a solid disc
        // that hides the star behind it.
        _glow = MakeBillboardQuad(
            "Glow",
            "res://Font/T_VFX_simple_1.png",
            new Color(
                ShineGlowColor.R * 0.5f,
                ShineGlowColor.G * 0.5f,
                ShineGlowColor.B * 0.5f,
                1f
            ),
            1.7f
        );
        _fx.AddChild(_glow);

        // Faint iridescent "sun glare" ring encircling the shine — the subtle
        // rainbow lens-flare halo from the reference footage. Kept dim so it's a
        // hint, not a hoop.
        _ring = MakeBillboardQuad(
            "Ring",
            "res://assets/shine_lens_ring.png",
            new Color(0.15f, 0.15f, 0.15f, 1f),
            2.4f
        );
        _fx.AddChild(_ring);

        // Camera-facing rotating sun-rays (custom shader; spins + pulses itself).
        _rays = new MeshInstance3D
        {
            Name = "Rays",
            Mesh = new QuadMesh { Size = new Vector2(3.4f, 3.4f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        var raysShader = GD.Load<Shader>("res://assets/shine_rays.gdshader");
        if (raysShader != null)
        {
            var raysMat = new ShaderMaterial { Shader = raysShader };
            raysMat.SetShaderParameter(
                "tint",
                new Color(ShineGlowColor.R, ShineGlowColor.G, ShineGlowColor.B, 1f)
            );
            raysMat.SetShaderParameter("intensity", 0.6f);
            raysMat.SetShaderParameter("speed", 1.0f);
            raysMat.SetShaderParameter("ray_life", 0.35f);
            _rays.MaterialOverride = raysMat;
        }
        _fx.AddChild(_rays);

        // Twinkling sparkles drifting off the star.
        _sparkles = BuildSparkles();
        _fx.AddChild(_sparkles);

        SetupStarFollow();
    }

    /// <summary>
    /// Finds the bone the FX should emit from. The star body mesh is 100% rigidly
    /// skinned to the "body" bone (verified from the glb weights), and Blender
    /// shows that bone sits at the middle of the shine — so its world origin is
    /// the mesh center, and it tracks the collect animation exactly with no
    /// calibration, markers, or drift. Falls back to starglow/shine, then to the
    /// fixed local spot if there's no skeleton at all.
    /// </summary>
    private void SetupStarFollow()
    {
        _starSkel = GetNodeOrNull<Skeleton3D>("Armature/Skeleton3D");
        if (_starSkel == null)
            return;

        _starBoneIdx = _starSkel.FindBone("body");
        if (_starBoneIdx < 0)
            _starBoneIdx = _starSkel.FindBone("starglow");
        if (_starBoneIdx < 0)
            _starBoneIdx = _starSkel.FindBone("shine");
        if (_starBoneIdx < 0)
            return;

        // Pin a marker into the body bone at the star-body centroid (the mesh's
        // center of mass, offset from the bone origin), so the FX emits from the
        // middle of the shine and rides the animation with it.
        Vector3 localCentroid = ComputeBodyCentroidLocal();
        var attach = new BoneAttachment3D { Name = "ShineFXAttach" };
        _starSkel.AddChild(attach);
        attach.BoneName = _starSkel.GetBoneName(_starBoneIdx);
        _meshCenter = new Marker3D { Name = "ShineFXCenter", Position = localCentroid };
        attach.AddChild(_meshCenter);
        _pickupShapeDbg = GetNodeOrNull<Node3D>("PickupZone/CollisionShape3D");
    }

    /// <summary>Centroid of the star-body surface expressed in the body bone's
    /// local space (bindPose × mean-vertex), so a marker placed there sits at the
    /// mesh's center. Reads the largest skinned surface; falls back to the value
    /// measured from the glb if the mesh can't be read.</summary>
    private Vector3 ComputeBodyCentroidLocal()
    {
        var fallback = new Vector3(0.009f, 18.99f, 0f);
        var mi = GetNodeOrNull<MeshInstance3D>("Armature/Skeleton3D/Mesh_0");
        _starMeshInst = mi;
        if (mi?.Mesh == null || mi.Skin == null)
            return fallback;

        Mesh mesh = mi.Mesh;
        int bestSurf = -1,
            bestCount = -1;
        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            Vector3[] vs = mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (vs.Length > bestCount)
            {
                bestCount = vs.Length;
                bestSurf = s;
            }
        }
        if (bestSurf < 0)
            return fallback;

        var arr = mesh.SurfaceGetArrays(bestSurf);
        Vector3[] verts = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        int[] bones = arr[(int)Mesh.ArrayType.Bones].AsInt32Array();
        if (verts.Length == 0)
            return fallback;

        Vector3 mean = Vector3.Zero;
        foreach (Vector3 v in verts)
            mean += v;
        mean /= verts.Length;

        int bindIdx = bones.Length > 0 ? bones[0] : 0;
        int skelBone = mi.Skin.GetBindBone(bindIdx);
        if (skelBone >= 0)
            _starBoneIdx = skelBone; // emit from the bone this surface actually rides
        return mi.Skin.GetBindPose(bindIdx) * mean;
    }

    /// <summary>Additive, unshaded, camera-facing quad from a black-background
    /// glow texture — black adds nothing, the bright center adds warm light.</summary>
    private static MeshInstance3D MakeBillboardQuad(
        string name,
        string texPath,
        Color tint,
        float size
    )
    {
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            AlbedoColor = tint,
            AlbedoTexture = GD.Load<Texture2D>(texPath),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DisableReceiveShadows = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        };
        return new MeshInstance3D
        {
            Name = name,
            Mesh = new QuadMesh { Size = new Vector2(size, size) },
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    /// <summary>A gentle upward drift of small additive star-sparkles that fade
    /// in and back out (twinkle) over their short life.</summary>
    private GpuParticles3D BuildSparkles()
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.6f,
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 180f,
            Gravity = new Vector3(0f, 0.55f, 0f),
            InitialVelocityMin = 0.35f,
            InitialVelocityMax = 0.95f,
            ScaleMin = 0.06f,
            ScaleMax = 0.18f,
            Color = Colors.White,
        };

        // Fade in then out (transparent → bright → transparent) so each sparkle
        // twinkles rather than pops. Modulates the additive quad via vertex color.
        var grad = new Gradient();
        grad.SetOffset(0, 0f);
        grad.SetColor(0, new Color(ShineGlowColor.R, ShineGlowColor.G, ShineGlowColor.B, 0f));
        grad.SetOffset(1, 1f);
        grad.SetColor(1, new Color(ShineGlowColor.R, ShineGlowColor.G, ShineGlowColor.B, 0f));
        grad.AddPoint(0.3f, new Color(1f, 0.96f, 0.7f, 0.7f));
        pm.ColorRamp = new GradientTexture1D { Gradient = grad };

        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = GD.Load<Texture2D>("res://assets/shine_sparkle.png"),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DisableReceiveShadows = true,
        };
        var mesh = new QuadMesh { Size = new Vector2(0.3f, 0.3f), Material = mat };

        return new GpuParticles3D
        {
            Name = "Sparkles",
            Amount = 32,
            Lifetime = 1.0,
            Preprocess = 1.0,
            Explosiveness = 0f,
            LocalCoords = false,
            ProcessMaterial = pm,
            DrawPass1 = mesh,
            Emitting = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    /// <summary>Breathes the halo scale and light energy each frame (the rays
    /// pulse/shoot themselves in the shader).</summary>
    private void AnimateShineEffects(float d)
    {
        if (_fx == null)
            return;

        _fxTime += d;

        if (_light != null)
            _light.LightEnergy = _baseLightEnergy * (1f + 0.16f * Mathf.Sin(_fxTime * 2.1f));

        if (_glow != null)
        {
            float s = 1f + 0.12f * Mathf.Sin(_fxTime * 1.7f);
            _glow.Scale = new Vector3(s, s, s);
        }
    }

    /// <summary>Pins the FX package to the star-body centroid marker (the middle
    /// of the mesh). Called at the END of _Process, after the root's own transform
    /// for the frame is finalized, so cutscene root motion can't shear it off.</summary>
    private void UpdateShineFxPosition()
    {
        if (_fx == null || _meshCenter == null || !IsInstanceValid(_meshCenter))
            return;

        // The Mesh_0 node carries a constant -Y offset (relative to the skeleton)
        // that shifts the rendered star down; add it (in world space, so it rotates
        // with the skeleton during the cutscene) to land on the true mesh center.
        Vector3 emit = _meshCenter.GlobalPosition;
        if (_starMeshInst != null)
            emit += _starSkel.GlobalTransform.Basis * _starMeshInst.Position;

        _fx.GlobalPosition = emit + Vector3.Up * ShineFxYNudge;

        if (!_fxCenterLogged)
        {
            _fxCenterLogged = true;
            GD.Print($"[ShineFX] emitting from mesh center {emit}");
        }
    }
}
