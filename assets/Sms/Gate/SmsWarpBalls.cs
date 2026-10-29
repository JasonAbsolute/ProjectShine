using Godot;
using System.Collections.Generic;

/// <summary>
/// The warp-in's light balls -- TMario::warpInEffect (MarioParticle.cpp 423-486) and TWarpInCallBack (388-411); SMS pass 127b. Mario
/// dissolves into ten one-frame bursts of ms_mariowp_* balls off his joints (the .jpa files under /scene/map/map/gate, jpa_dump.py):
/// body 80 off the centre joint (a 20 u sphere x (1.12, 1.2, 1.02), 6 up), head 140 (a 22 u half shell, 5 / -16 off, tilted 65 deg),
/// cap 140 (26 u, 6 / -26, -125 deg), each hand 40 (a 5 x 20 u cylinder at 8 / 1), each leg 30 (a 7 x 10 u ring, hollow 75 %),
/// each foot 20 (a 10 u sphere x 2 tall), the FLUDD 80 (a 17 u shell x (0.5, 2.3, 0.5) at 35 / -44) -- about 600 balls. Each flies
/// off at omni 4-6 u/f (+-2-3 random) on its base velocity, slowed 5 % a frame (airResist 0.95), and carries the Vortex field's
/// velocity (JPAVortexField::affect: tangential about the joint's axis -- Y for most, X for the head and cap -- at mag x (1 - d^2 /
/// 1000^2), 5-8 u/f, set every frame and never damped: the cloud of each part spins about its joint), lives 90 frames (+-62 %), is a
/// 20 u billboard (0.4 x 50) of P_msm_mizutama1 x (1 +- 25 %) lerp(env -> prm) by the texture, alpha-tested hard (GEQUAL 222), shrinking
/// to nothing over its last quarter. Colours per part: body a random one of cap-red / cream / blue (the 14-frame random table), head
/// skin, cap red, hands white, legs blue, feet brown, FLUDD lilac. Then the callback (every frame while warpIn is in state 2): the ball's
/// position (its global offset unk14) +=
/// dir x unk468 x mStatusTimer x (1 + ((ptr >> 2) & 63) / 16) -- each ball its own 1 .. 5 factor, the push growing with the timer --
/// so the cloud streams into the gate and through it. A .jpa frame is 1/60 s. Drawn by one MultiMesh of unlit alpha-tested billboards.
/// Mario's warp-in calls SmsWarpInStart(mario, gate) at the burst and SmsWarpInPull(strength, centre) every tick of the pull (strength =
/// unk468 x timer). The dust and the flash live beside this node in WarpBalls.tscn as GpuParticles3D (one-shots).
/// </summary>
[GlobalClass]
public partial class SmsWarpBalls : Node3D
{
    private struct Ball
    {
        public Vector3 Pos, Vel, Axis, Centre;   // Pos / Centre world metres; Vel units a frame (world axes); Axis the vortex axis (world)
        public float Life, Age, Size, Vortex, Factor, Weight;   // frames; Size metres; Vortex units a frame; Factor the callback's 1 .. 5; Weight the dynamics weight 0.625 .. 1
        public Color Col, Env;   // 127c: prm (the core) and env (the rim): the TEV's lerp(env -> prm, I) runs in WarpBall.gdshader
    }

    /// <summary>The ball texture (P_msm_mizutama1: a water-drop highlight, alpha = coverage).</summary>
    [Export] public Texture2D BallTexture;
    /// <summary>GX alpha compare GEQUAL 222 / 255: the balls are hard-edged.</summary>
    [Export] public float AlphaCut = 0.8706f;
    /// <summary>127c: the ball shader (WarpBall.gdshader: lerp(env -> prm, I) per texel, the hard cut, billboarded per instance); none = the file.</summary>
    [Export] public Shader BallShader;
    /// <summary>x the counts (600 balls at 1); his GPU decides.</summary>
    [Export] public float Density = 1f;
    /// <summary>Skip the FLUDD burst when he has no FLUDD (the game's MARIO_FLAG_HAS_FLUDD test).</summary>
    [Export] public bool FluddBurst = true;
    /// <summary>127g: x the pull into the gate (TWarpInCallBack's dir x 0.6 x timer x 1..5 a frame, as a POSITION shift). 1 = the game: after the
    /// 90-tick hang the slowest balls take 20 frames (a third of a second) to cover the 3.4 m from the hold point, the fastest 9. Lower = a slower
    /// stream; Mario's own SmsWarpInBallsTicks (90) sets how long the cloud hangs first.</summary>
    [Export(PropertyHint.Range, "0.1,3")] public float PullScale = 1f;
    /// <summary>127g: the joint-spin (the Vortex field) fades to nothing by this fraction of each ball's life -- the disc's 0.625 (FLD1 fadeOut 0 -> 0.625, cutoff).</summary>
    [Export(PropertyHint.Range, "0.05,1")] public float VortexFadeEnd = 0.625f;
    [Export] public bool DebugPrint;

    private readonly List<Ball> _balls = new();
    private MultiMeshInstance3D _mmi;
    private MultiMesh _mm;
    private Vector3 _dir, _gateCentre;
    private float _pull;         // unk468 x timer
    private bool _running;
    private double _acc;
    private readonly RandomNumberGenerator _rng = new();
    private static readonly Color[] BodyTable = { new Color(1f, 135f / 255f, 111f / 255f), new Color(1f, 1f, 231f / 255f), new Color(87f / 255f, 135f / 255f, 1f) };
    private static readonly Color[] BodyEnv = { new Color(1f, 24f / 255f, 0f), new Color(1f, 213f / 255f, 0f), new Color(0f, 39f / 255f, 159f / 255f) };

    public int Alive => _balls.Count;
    public bool Running => _running;
    public float LastPull => _pull;

    /// <summary>The cloud's mean position (the drive).</summary>
    public Vector3 Mean()
    {
        if (_balls.Count == 0) return GlobalPosition;
        Vector3 s = Vector3.Zero; foreach (var b in _balls) s += b.Pos; return s / _balls.Count;
    }

    /// <summary>warpInEffect: the ten bursts at Mario's joints as they stand this frame; dir = the pull direction (mWarpInDir).</summary>
    public void SmsWarpInStart(Mario mario, SmsModelGate gate)
    {
        _dir = gate != null ? (gate.CentreWorld - gate.HoldPointWorld).Normalized() : Vector3.Forward;
        _gateCentre = gate != null ? gate.CentreWorld : GlobalPosition + _dir * 100f;
        var skel = mario?.FindChild("Skeleton3D", true, false) as Skeleton3D;
        if (skel == null) { if (DebugPrint) GD.Print("[SMS warp balls] no skeleton: nothing to burst"); return; }
        if (DebugPrint) GD.Print($"[SMS warp balls] skeleton at {skel.GlobalTransform.Origin} (armature {(skel.GetParent() as Node3D)?.Position}), Mario at {mario.GlobalPosition}, root bone pose {skel.GetBoneGlobalPose(0).Origin}, center pose {skel.GetBoneGlobalPose(Mathf.Max(skel.FindBone("center"), 0)).Origin}");
        Transform3D J(string name, string alt = null)
        {
            int i = skel.FindBone(name); if (i < 0 && alt != null) i = skel.FindBone(alt);
            if (i < 0) return skel.GlobalTransform;
            return skel.GlobalTransform * skel.GetBoneGlobalPose(i);
        }
        Color White = Colors.White;
        Color Skin = new Color(1f, 231f / 255f, 207f / 255f), SkinEnv = new Color(1f, 183f / 255f, 135f / 255f);
        Color CapRed = new Color(1f, 135f / 255f, 111f / 255f), CapEnv = new Color(1f, 24f / 255f, 0f);
        Color HandEnv = new Color(207f / 255f, 231f / 255f, 231f / 255f);
        Color Blue = new Color(87f / 255f, 137f / 255f, 1f), BlueEnv = new Color(0f, 39f / 255f, 159f / 255f);
        Color Brown = new Color(183f / 255f, 111f / 255f, 87f / 255f), BrownEnv = new Color(126f / 255f, 61f / 255f, 2f / 255f);
        Color Lilac = new Color(183f / 255f, 183f / 255f, 1f), LilacEnv = new Color(72f / 255f, 72f / 255f, 135f / 255f);
        // (joint, count, volume 0 sphere / 1 cylinder, size u, minRad, sweep, scale, trs u, rot z deg, omni, axis, rndm, vortex mag, prm, env, the body's random table, the vortex axis)
        Burst(J("center", "jnt_waist"), 80, 0, 20f, 0.625f, 1f, new Vector3(1.12f, 1.2f, 1.02f), new Vector3(0f, 6f, 0f), 0f, 5f, 5f, 3f, 8f, White, White, true, Vector3.Up);
        Burst(J("jnt_head", "M_head"), 140, 0, 22f, 0.875f, 0.5f, Vector3.One, new Vector3(5f, -16.1f, 0f), 65f, 4f, 1f, 2f, -8f, Skin, SkinEnv, false, Vector3.Right);
        Burst(J("jnt_head", "M_head"), 140, 0, 26f, 0.875f, 0.5f, Vector3.One, new Vector3(6f, -26f, 0f), -125f, 5f, 1f, 2f, 8f, CapRed, CapEnv, false, Vector3.Right);
        Burst(J("jnt_hand_R", "M_hand1_R"), 40, 1, 5f, 0f, 1f, new Vector3(1f, 4f, 1f), new Vector3(8f, 1f, 0f), -76f, 6f, 0f, 3f, 5f, White, HandEnv, false, Vector3.Up);
        Burst(J("jnt_hand_L", "M_hand1_L"), 40, 1, 5f, 0f, 1f, new Vector3(1f, 4f, 1f), new Vector3(8f, 1f, 0f), -76f, 6f, 0f, 3f, -5f, White, HandEnv, false, Vector3.Up);
        Burst(J("chn_foot_R", "jnt_leg_R2"), 30, 1, 7f, 0.75f, 1f, new Vector3(1f, 1.5f, 1f), new Vector3(0.5f, -0.5f, 0f), 90f, 5f, 0f, 2f, 5f, Blue, BlueEnv, false, Vector3.Up);
        Burst(J("jnt_foot_R", "M_foot_R"), 20, 0, 10f, 0.5f, 1f, new Vector3(1f, 2f, 1f), new Vector3(5f, 2.4f, 0f), 95f, 5f, 0f, 2f, 5f, Brown, BrownEnv, false, Vector3.Up);
        Burst(J("chn_foot_L", "jnt_leg_L2"), 30, 1, 7f, 0.75f, 1f, new Vector3(1f, 1.5f, 1f), new Vector3(0.5f, -0.5f, 0f), 90f, 5f, 0f, 2f, -5f, Blue, BlueEnv, false, Vector3.Up);
        Burst(J("jnt_foot_L", "M_foot_L"), 20, 0, 10f, 0.5f, 1f, new Vector3(1f, 2f, 1f), new Vector3(5f, 2.4f, 0f), 95f, 5f, 0f, 2f, -5f, Brown, BrownEnv, false, Vector3.Up);
        if (FluddBurst && mario.HasFludd)
            Burst(J("center", "jnt_waist"), 80, 0, 17f, 1f, 1f, new Vector3(0.5f, 2.3f, 0.5f), new Vector3(0f, 35f, -43.9f), 0f, 5f, 2f, 3f, 6f, Lilac, LilacEnv, false, Vector3.Up);
        _running = true;
        BuildMesh();
        // ms_mariowp_dust at his centre joint: the rings and their streaks, one shot
        GlobalPosition = J("center", "jnt_waist").Origin;
        Fire("DustRings"); Fire("DustStreaks");
        if (DebugPrint) GD.Print($"[SMS warp balls] {_balls.Count} balls off {skel.GetBoneCount()} bones, pull dir {_dir}");
    }

    private void Burst(Transform3D joint, int count, int volume, float sizeU, float minRad, float sweep, Vector3 scl, Vector3 trsU, float rotZDeg,
        float omni, float axisV, float rndm, float vortex, Color prm, Color env, bool bodyTable, Vector3 fieldAxis)
    {
        float u = SmsUnits.UnitsPerMetre;
        int n = Mathf.Max(1, Mathf.RoundToInt(count * Density));
        Basis rot = Basis.FromEuler(new Vector3(0f, 0f, Mathf.DegToRad(rotZDeg)));
        Basis frame = joint.Basis.Orthonormalized() * rot;
        Vector3 centre = joint.Origin + joint.Basis.Orthonormalized() * (trsU / u);
        if (DebugPrint) GD.Print($"[SMS warp balls] burst {n} at {centre} (joint {joint.Origin}, trs {trsU} u)");
        for (int i = 0; i < n; i++)
        {
            Vector3 p, outward;
            if (volume == 0)
            {   // sphere: a shell between minRad and 1, the sweep a fraction of the azimuth
                float r = Mathf.Lerp(minRad, 1f, _rng.Randf());
                float theta = _rng.Randf() * Mathf.Tau * sweep, phi = Mathf.Acos(1f - 2f * _rng.Randf());
                outward = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                p = outward * r;
            }
            else
            {   // cylinder: a ring in XZ between minRad and 1, height +-1 along Y
                float r = Mathf.Lerp(minRad, 1f, _rng.Randf());
                float theta = _rng.Randf() * Mathf.Tau * sweep;
                outward = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
                p = outward * r + Vector3.Up * (_rng.Randf() * 2f - 1f);
            }
            Vector3 local = p * scl * (sizeU / u);
            Vector3 pos = centre + frame * local;
            // the base velocity, units a frame: omni outward from the centre, 'axis' = RADIAL from the volume's Y axis (JPAEmitter.cpp
            // calcCreateParticle: setLength((pos.x, 0, pos.z), velAxis) -- 127g; 127b had it along Y), random per axis, x (1 +- 12.5 %: velRatio)
            Vector3 radial = new Vector3(p.X, 0f, p.Z);
            radial = radial.LengthSquared() > 1e-8f ? radial.Normalized() : Vector3.Zero;
            Vector3 v = (outward * omni + radial * axisV + new Vector3(_rng.Randf() - 0.5f, _rng.Randf() - 0.5f, _rng.Randf() - 0.5f) * 2f * rndm) * (1f + 0.125f * (_rng.Randf() * 2f - 1f));
            var b = new Ball
            {
                Pos = pos, Vel = frame * v, Axis = (frame * fieldAxis).Normalized(), Centre = centre,
                Life = 90f * (1f + (_rng.Randf() * 2f - 1f) * 0.625f), Age = 0f,
                Size = 20f / u * (1f + (_rng.Randf() * 2f - 1f) * 0.25f), Vortex = vortex, Factor = 1f + _rng.RandiRange(0, 63) / 16f,
                Weight = 1f - 0.375f * _rng.Randf(),   // mDynamicsWeight = moment x (1 - 0.375 x rnd): the move is scaled 0.625 .. 1 per ball
            };
            if (bodyTable) { int k = _rng.RandiRange(0, 2); b.Col = BodyTable[k]; b.Env = BodyEnv[k]; }
            else { b.Col = prm; b.Env = env; }   // lerp(env -> prm, TEXC) in the shader: the texture's bright core shows prm, its rim env
            _balls.Add(b);
        }
    }

    private void BuildMesh()
    {
        if (_mmi == null)
        {
            var quad = new QuadMesh { Size = new Vector2(1f, 1f) };
            var shader = BallShader ?? (ResourceLoader.Exists("res://assets/Sms/Gate/fx/WarpBall.gdshader") ? GD.Load<Shader>("res://assets/Sms/Gate/fx/WarpBall.gdshader") : null);
            if (shader != null)
            {
                var mat = new ShaderMaterial { Shader = shader };
                mat.SetShaderParameter("tex", BallTexture); mat.SetShaderParameter("alpha_ref", AlphaCut);
                quad.Material = mat;
            }
            else
            {   // no shader file: the 127b stand-in (one flat colour x the shading)
                quad.Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = AlphaCut,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, VertexColorUseAsAlbedo = true, AlbedoTexture = BallTexture, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps, DisableReceiveShadows = true,
                };
            }
            _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = _balls.Count };
            _mmi = new MultiMeshInstance3D { Name = "Balls", Multimesh = _mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true };
            AddChild(_mmi);
            _mmi.GlobalTransform = Transform3D.Identity;
        }
        Draw();
    }

    /// <summary>TWarpInCallBack: the push per frame (dir x unk468 x mStatusTimer) toward the gate's centre.</summary>
    public void SmsWarpInPull(float strength, Vector3 centre) { _pull = strength; }

    /// <summary>warpInLight: ms_mariowp_senko at his centre as the take begins (the Flash child, 50 frames of streaks); the node follows him from Mario.</summary>
    public void SmsWarpInFlash()
    {
        _flashT = 0f; _flashed = true;
        ApplyFlashKnobs();
        Fire("Flash");
    }

    /// <summary>127e DEVIATION knobs: the flash's alpha and size over the game's (1 / 1 = the disc: 50 colour-cycling streaks of 10 x 75 units growing
    /// x 2.5 / x 4 around him for ~1.8 s -- Jaysonn: "a weird pulsing light on Mario", so dialed down).</summary>
    [Export(PropertyHint.Range, "0,1.5")] public float FlashAlpha = 0.4f;
    [Export(PropertyHint.Range, "0.25,1.5")] public float FlashSize = 0.6f;
    private bool _flashKnobsApplied;
    private void ApplyFlashKnobs()
    {
        if (_flashKnobsApplied) return;
        _flashKnobsApplied = true;
        var p = GetNodeOrNull<GpuParticles3D>("Flash");
        if (p == null || p.DrawPasses < 1 || (Mathf.IsEqualApprox(FlashAlpha, 1f) && Mathf.IsEqualApprox(FlashSize, 1f))) return;
        var mesh = p.GetDrawPassMesh(0)?.Duplicate() as Mesh;
        if (mesh == null) return;
        if (mesh.SurfaceGetMaterial(0)?.Duplicate() is ShaderMaterial sm)
        {
            var prm = sm.GetShaderParameter("prm").AsVector4(); prm.W *= FlashAlpha; sm.SetShaderParameter("prm", prm);
            mesh.SurfaceSetMaterial(0, sm);
        }
        if (mesh is QuadMesh q) q.Size *= FlashSize;
        p.SetDrawPassMesh(0, mesh);
    }

    private void Fire(string name)
    {
        var p = GetNodeOrNull<GpuParticles3D>(name);
        if (p == null) return;
        p.Restart(); p.Emitting = true;
    }

    private float _flashT; private bool _flashed;

    public override void _PhysicsProcess(double delta)
    {
        if (!_running)
        {   // only the flash so far (or nothing): gone after its 110 frames if no burst follows
            if (_flashed) { _flashT += (float)delta; if (_flashT > 6f) QueueFree(); }
            return;
        }
        _acc += delta * 60.0;   // a .jpa frame is 1/60 s
        int guard = 0;
        while (_acc >= 1.0 && guard++ < 8) { _acc -= 1.0; Frame(); }
        Draw();
        if (_balls.Count == 0) { _running = false; _flashed = true; _flashT = 5f; }
    }

    /// <summary>One .jpa frame: JPAVortexField::affect (a tangential FIELD velocity about the joint's axis of mag x (1 - d^2 / 1000^2), set each
    /// frame, undamped -- 127g: fading to nothing by 62.5 % of the ball's life, its FLD1 status 0x50 = fadeOut from 0 + cutoff at 0.625), the base
    /// velocity x airResist 0.95, the move x the ball's dynamics weight (BEM1 moment 1 - 0.375 x rnd: 0.625 .. 1), then the callback's push.
    /// 127g -- TWarpInCallBack::execute (MarioParticle.cpp 393) does `particle->unk14 += dir x unk468 x mStatusTimer x factor`, and unk14 is the
    /// particle's GLOBAL OFFSET (JPAParticle.cpp: mGlobalPosition = mLocalPosition x scale + unk14), i.e. the ball's POSITION moves by that much
    /// each frame -- a velocity that grows with the timer, not an acceleration. 127b-f added it to the VELOCITY (quadratic growth, ~1.6 x
    /// faster, and it never let the cloud sit): Jaysonn -- "they explode in, they should explode then go in".</summary>
    private void Frame()
    {
        float u = SmsUnits.UnitsPerMetre;
        for (int i = _balls.Count - 1; i >= 0; i--)
        {
            var b = _balls[i];
            b.Age += 1f;
            if (b.Age >= b.Life) { _balls.RemoveAt(i); continue; }
            float progress = b.Age / b.Life;
            Vector3 relU = (b.Pos - b.Centre) * u;
            Vector3 perp = relU - b.Axis * relU.Dot(b.Axis);
            Vector3 fieldVel = Vector3.Zero;
            float d2 = perp.LengthSquared();
            if (d2 > 1e-6f && progress < VortexFadeEnd)
            {
                float t = Mathf.Min(d2, 1e6f) / 1e6f;
                fieldVel = perp.Normalized().Cross(b.Axis) * (b.Vortex * (1f - t)) * (1f - progress / VortexFadeEnd);
            }
            b.Vel *= 0.95f;
            b.Pos += (b.Vel + fieldVel) * b.Weight / u;
            if (_pull > 0f) b.Pos += _dir * (_pull * b.Factor * PullScale / u);   // the callback: the global offset += dir x unk468 x mStatusTimer x the ball's factor
            // DEVIATION: a ball that has crossed the gate's plane is gone (the game's fly on behind the gate's wall and die there unseen)
            if (_pull > 0f && (b.Pos - _gateCentre).Dot(_dir) > 0f) { _balls.RemoveAt(i); continue; }
            _balls[i] = b;
        }
    }

    private void Draw()
    {
        if (_mm == null) return;
        if (_mm.InstanceCount != _balls.Count) _mm.InstanceCount = _balls.Count;
        for (int i = 0; i < _balls.Count; i++)
        {
            var b = _balls[i];
            float t = b.Age / b.Life;
            float s = b.Size * (t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f);   // ESP1 scale: 1 -> 0 over the last quarter
            _mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * Mathf.Max(s, 0.001f)), b.Pos));
            _mm.SetInstanceColor(i, b.Col);
            _mm.SetInstanceCustomData(i, b.Env);
        }
    }
}
