using Godot;

/// <summary>
/// An NPC effect the way TMarioParticleManager runs one (EmitterViewObj.cpp): the scene is instanced under a BoneAttachment3D of the joint the
/// game binds the emitter to (emitAndBindToMtxPtr / SMS_EasyEmitParticle with a MtxPtr -- the emitter's RT matrix is the joint's every frame),
/// <see cref="Start"/> opens its emitters for the .jpa's emitter life (BEM1 maxFrame, in 60 Hz frames: 1 = one burst, 80 = the sparkle's,
/// 0 = until <see cref="Stop"/> -- the wash, kept alive by its clip) and the node frees itself once the last particle is gone
/// (<see cref="DrainSeconds"/> = the longest particle life). The scale (setGlobalScale(getEffectScale_()) x mSLCleanEffectScale on the happy
/// sparkle) goes on the node AND on the quads: Godot's world-space particles keep their own size, so each GPUParticles3D gets a per-instance
/// copy of its process material with scale_min / max multiplied. Particles are world space (local_coords off), so a burst stays where the
/// joint was when it fired, as the game's do. The scenes: assets/Sms/Fx/Npc*.tscn (gen_fx_npc.py).
/// Pass 98: an effect bound to a POSITION pointer (emitAndBindToPosPtr: the burning NPC's fire at mFireParticlePos) keeps the world axes --
/// <see cref="FollowRotation"/> off -- and the emitters in the node group <see cref="StrengthGroup"/> take a live <see cref="SetStrength"/>
/// (the game's setGlobalScale(scale x mBurnStrength) every frame: the quads and the emitter offset shrink as the fire is hosed out).
/// Pass 108: <see cref="SetGoopColors"/> recolours the goop-ball materials of THIS instance (SMSSetEmitterPolColor by the layer the NPC sank
/// in, SmsNpcProfile.GoopFromLayer): the scene's material is shared by every spawn, so the emitter gets a per-instance copy as its MaterialOverride.
/// </summary>
public partial class SmsNpcEffect : Node3D
{
    /// <summary>BEM1 maxFrame: how long the emitter emits, in 60 Hz frames (1 = a single burst, 0 = until Stop()).</summary>
    [Export] public float EmitFrames = 1f;
    /// <summary>The longest particle life (lifetime x (1 + randomness)): the wait after the emitter closes before the node is freed.</summary>
    [Export] public float DrainSeconds = 0.6f;
    /// <summary>On: the node takes the joint's rotation too (emitAndBindToMtxPtr). Off: its position only, the world axes kept (emitAndBindToPosPtr --
    /// the burning NPC's fire rises world-up whatever the head does).</summary>
    [Export] public bool FollowRotation = true;

    /// <summary>GPUParticles3D children in this node group scale with <see cref="SetStrength"/> (the three fire emitters; the smoke does not).</summary>
    public const string StrengthGroup = "sms_burn_strength";

    private double _emitLeft = -1;
    private double _drainLeft = -1;
    private bool _started;
    private float _scale = 1f;
    private float _strength = 1f;
    private Node3D _follow;                // the BoneAttachment3D (its frame carries the model's 1/74.44 scale: followed orthonormalised, top-level)

    private sealed class StrengthSlot
    {
        public GpuParticles3D P; public ParticleProcessMaterial Pm; public float BaseMin, BaseMax; public Vector3 BasePos; public float BaseSphere;
    }
    private readonly System.Collections.Generic.List<StrengthSlot> _strengthSlots = new();

    /// <summary>The joint frame without the model's scale, the effect scale on top -- the game's emitter RT matrix is the joint's, its scale its own.</summary>
    private void Follow()
    {
        if (_follow == null || !IsInsideTree() || !GodotObject.IsInstanceValid(_follow)) return;
        Basis b = FollowRotation ? _follow.GlobalBasis.Orthonormalized() : Basis.Identity;
        GlobalTransform = new Transform3D(b.Scaled(Vector3.One * _scale), _follow.GlobalPosition);
    }

    /// <summary>Open the emitters. `scale` = the game's emitter scale (1 for a Toad, the NPC's initial scale for a Pianta, x 1.65 on the sparkle).</summary>
    public void Start(float scale = 1f)
    {
        if (_started) return;
        _started = true;
        _scale = scale;
        foreach (var p in Emitters())
        {
            bool strength = p.IsInGroup(StrengthGroup);
            if ((strength || !Mathf.IsEqualApprox(scale, 1f)) && p.ProcessMaterial is ParticleProcessMaterial pm)
            {
                var copy = (ParticleProcessMaterial)pm.Duplicate();
                copy.ScaleMin *= scale; copy.ScaleMax *= scale;
                p.ProcessMaterial = copy;
                if (strength)
                    _strengthSlots.Add(new StrengthSlot { P = p, Pm = copy, BaseMin = copy.ScaleMin, BaseMax = copy.ScaleMax, BasePos = p.Position, BaseSphere = copy.EmissionSphereRadius });
            }
        }
        Follow();
        foreach (var p in Emitters())
        {
            if (p.OneShot) p.Restart(); else p.Emitting = true;
        }
        _emitLeft = EmitFrames > 0f ? EmitFrames / 60.0 : -1;
        if (EmitFrames > 0f && EmitFrames <= 1f) { _emitLeft = -1; _drainLeft = DrainSeconds; }   // a burst: nothing to close, just drain
    }

    /// <summary>mBurnStrength (0..1): the StrengthGroup emitters' quad size, emitter offset and emission volume x this (setGlobalScale each frame).</summary>
    public void SetStrength(float s)
    {
        s = Mathf.Clamp(s, 0f, 1f);
        if (Mathf.IsEqualApprox(s, _strength)) return;
        _strength = s;
        foreach (var slot in _strengthSlots)
        {
            slot.Pm.ScaleMin = slot.BaseMin * s; slot.Pm.ScaleMax = slot.BaseMax * s;
            slot.P.Position = slot.BasePos * s;
            if (slot.BaseSphere > 0f) slot.Pm.EmissionSphereRadius = Mathf.Max(slot.BaseSphere * s, 0.001f);
        }
    }

    /// <summary>Pass 108: SMSSetEmitterPolColor by the layer -- every emitter whose draw-pass material carries the goop ball's `prm_color` / `env_color`
    /// uniforms (NpcDrip* / NpcWash*: the ball is lerp(env -> prm, I)) gets a copy with `light` as prm and `dark` as env as its MaterialOverride.
    /// Display-space colours, as the uniforms are. The sparkle, the burn and the sink ripple carry no such uniform and are left alone.</summary>
    public void SetGoopColors(Color light, Color dark)
    {
        var l = new Vector3(light.R, light.G, light.B); var d = new Vector3(dark.R, dark.G, dark.B);
        foreach (var p in Emitters())
        {
            var src = (p.MaterialOverride ?? p.DrawPass1?.SurfaceGetMaterial(0)) as ShaderMaterial;
            if (src == null || src.Shader == null || !HasUniform(src.Shader, "prm_color") || !HasUniform(src.Shader, "env_color")) continue;
            var copy = (ShaderMaterial)src.Duplicate();
            copy.SetShaderParameter("prm_color", l);
            copy.SetShaderParameter("env_color", d);
            p.MaterialOverride = copy;
            _goopColored++;
        }
    }
    private int _goopColored;
    /// <summary>How many emitters SetGoopColors recoloured (the drive reads it; 0 = the baked stage colours).</summary>
    public int GoopColoredEmitters => _goopColored;

    private static bool HasUniform(Shader shader, string name)
    {
        foreach (var u in shader.GetShaderUniformList())
            if (u.AsGodotDictionary()["name"].AsString() == name) return true;
        return false;
    }

    public float StrengthApplied => _strength;
    public int StrengthEmitterCount => _strengthSlots.Count;

    /// <summary>Close the emitters (the wash clip ended); the node frees itself when the live particles are gone.</summary>
    public void Stop()
    {
        if (_drainLeft >= 0) return;
        foreach (var p in Emitters()) p.Emitting = false;
        _emitLeft = -1;
        _drainLeft = DrainSeconds;
    }

    public bool IsEmitting => _started && _drainLeft < 0;
    public float EffectScaleApplied => _scale;

    /// <summary>Every Spawn so far (a headless drive counts the emits) and the scene file of the last one.</summary>
    public static int SpawnCount;
    public static string LastSpawned = "";

    public override void _Process(double delta)
    {
        Follow();
        if (_emitLeft >= 0)
        {
            _emitLeft -= delta;
            if (_emitLeft <= 0) Stop();
        }
        if (_drainLeft >= 0)
        {
            _drainLeft -= delta;
            if (_drainLeft <= 0) QueueFree();
        }
    }

    private System.Collections.Generic.IEnumerable<GpuParticles3D> Emitters()
    {
        foreach (var c in GetChildren()) if (c is GpuParticles3D p) yield return p;
    }

    /// <summary>Instance `scene` under a BoneAttachment3D of `joint` on `skel` (made once per joint and kept) and start it; null when the
    /// scene or the joint is missing (one warning per joint). The attachment follows the joint's full pose, as the game's MtxPtr bind does.</summary>
    public static SmsNpcEffect Spawn(PackedScene scene, Skeleton3D skel, string joint, float scale, System.Collections.Generic.Dictionary<string, BoneAttachment3D> cache, string who)
    {
        if (scene == null || skel == null) return null;
        string key = skel.GetInstanceId() + ":" + joint;        // pass 98: a part's skeleton (the luggage's ef_null) shares the cache with the body's
        if (cache.TryGetValue(key, out var att) && att == null) return null;   // warned once already
        if (att == null || !GodotObject.IsInstanceValid(att))
        {
            int b = string.IsNullOrEmpty(joint) ? -1 : skel.FindBone(joint);
            if (b < 0)
            {
                GD.PushWarning($"[SMS npc fx] {who}: no joint '{joint}' on {skel.Name} for {scene.ResourcePath.GetFile()} -- the effect is skipped");
                cache[key] = null;
                return null;
            }
            att = new BoneAttachment3D { Name = "Fx_" + joint, BoneName = joint };
            skel.AddChild(att);
            cache[key] = att;
        }
        if (scene.Instantiate() is not SmsNpcEffect fx)
        {
            GD.PushWarning($"[SMS npc fx] {who}: {scene.ResourcePath.GetFile()} is not an SmsNpcEffect scene");
            return null;
        }
        fx.TopLevel = true;                 // the attachment's frame carries the model's 1/74.44 scale -- followed without it (Follow)
        fx._follow = att;
        fx.ProcessPriority = 100;           // after the AnimationPlayer and the skeleton modifiers
        att.AddChild(fx);
        fx.Start(scale);
        SpawnCount++; LastSpawned = scene.ResourcePath.GetFile();
        return fx;
    }
}
