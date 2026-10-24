using System;
using Godot;

/// <summary>
/// SMS pass 120: ms_m_gout_gate.jpa, the paint-water stream that carries Mario out of an M gate on the return (TMario::warpOutEffect kind 0,
/// MarioParticle.cpp 494: emitWithRotate at his feet, the emitter turned to the return spot's rotation). Read with jpa_dump.py and run here
/// as the engine runs it (JPAField.cpp / JPAParticle.cpp), one particle frame a 60th of a second:
///   emitter   volume Sphere 60 u (minRad 0.625), 10 a frame for 30 frames, life 60, at (0, -70, -330) in the emitter's frame (+Z = the
///             spot's forward: the gate 3.3 m behind the spot), base velocity 40 u/f along (0, 1, 1), airResist 0.96 a frame, moment 0.75
///             +- 0.125 on the whole velocity (mDynamicsWeight).
///   Newton    toward (0, 120, 350), mag 0.3 x (10 within 100 u, else 10 x 100^2 / d^2), faded in over the first half of life, added to the
///             field ACCELERATION (it accumulates).
///   Magnet    toward (0, 110, 350), 20 u/f, from half life faded in to the end, a field VELOCITY (this frame only).
///   Vortex    enabled from progress 1.0: never acts (left as in the data).
///   shape     Billboard along the velocity, 25 u (x 1 +- 0.125), alpha 0 -> 1 by 25 %, -> 0 from 75 %; colour = lerp(env, prm, texture
///             intensity) with prm AND env keyed over a 23-frame cycle (red, yellow, blue held, salmon, white, brown), each bead at its own
///             phase (anmRndm 24).
///   children  from 87.5 % of the parent's life one every 2 frames: 55 u shrinking to nothing over 16 frames, the parent's colour, +- 20 u.
/// The texture is the game's P_msm_mizutama1_ia (jpa_bake 'raw': intensity in RGB, alpha in A); a soft bead stands in until the editor
/// imports it. Spawned by Mario.SmsGateOutStart; frees itself when the last child is gone (~1.8 s).
/// </summary>
public partial class SmsGateStream : Node3D
{
    private const float Fps = 60f;
    private const int Rate = 10, EmitFrames = 30, Life = 60, ChildLife = 16, CycleFrames = 23;
    private const float AirResist = 0.96f, BaseSpeed = 40f, BeadUnits = 25f, ChildUnits = 55f;
    private static readonly Vector3 EmitterTrs = new(0f, -70f, -330f);
    private static readonly Vector3 EmitDir = new Vector3(0f, 1f, 1f).Normalized();
    private static readonly Vector3 NewtonPos = new(0f, 120f, 350f), MagnetPos = new(0f, 110f, 350f);
    private const float NewtonMag = 0.3f, NewtonRefSq = 100f * 100f, MagnetMag = 20f;
    // the BSP1 colour keys (frame, rgb): prm then env; held between keys as the engine lerps (calc type 4 = each bead at a random phase)
    private static readonly (int f, Color c)[] Prm = { (0, C(255, 201, 168)), (5, C(255, 87, 63)), (6, C(255, 255, 231)), (7, C(115, 134, 255)), (15, C(115, 134, 255)), (16, C(255, 201, 168)), (18, C(255, 201, 168)), (19, C(255, 255, 255)), (21, C(255, 255, 255)), (22, C(183, 111, 87)) };
    private static readonly (int f, Color c)[] Env = { (0, C(126, 61, 2)), (5, C(184, 24, 0)), (6, C(255, 213, 0)), (7, C(28, 0, 162)), (15, C(28, 0, 162)), (16, C(243, 140, 105)), (18, C(243, 140, 105)), (19, C(207, 231, 231)), (21, C(207, 231, 231)), (22, C(126, 61, 2)) };
    private static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

    private struct P { public Vector3 Pos, BaseVel, FieldAcc, Vel; public int Age, Life, Phase; public float Moment, Size; public bool Child; public Color Prm, Env; public int NextChild; }
    private readonly P[] _p = new P[Rate * EmitFrames + Rate * EmitFrames * 4];
    private int _count, _frame;
    private double _acc;
    private readonly Random _rng = new Random(0x60A7);
    private MultiMesh _mm; private MultiMeshInstance3D _mmi;
    private Basis _frame3;   // the emitter's frame: X = the spot's left, Y up, Z = the spot's forward
    private Vector3 _origin;
    public int Count => _count;
    public bool Done => _frame >= EmitFrames && _count == 0;

    private const string ShaderCode = @"shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_opaque, shadows_disabled;
uniform sampler2D bead : source_color, filter_linear;
varying vec4 v_env;
void vertex() { v_env = INSTANCE_CUSTOM; }
void fragment() {
    vec4 t = texture(bead, UV);
    ALBEDO = mix(v_env.rgb, COLOR.rgb, t.r);   // the JPA TEV: lerp(env -> prm, texture intensity)
    ALPHA = t.a * COLOR.a;
    ALPHA_SCISSOR_THRESHOLD = 0.03;
}";

    /// <summary>At <paramref name="feet"/> (his position), the emitter's +Z along <paramref name="forward"/> (the spot's forward, flattened).</summary>
    public static SmsGateStream Spawn(Node parent, Vector3 feet, Vector3 forward)
    {
        var s = new SmsGateStream { Name = "SmsGateStream" };
        parent.AddChild(s);
        s.GlobalTransform = Transform3D.Identity;
        forward.Y = 0f;
        if (forward.LengthSquared() < 1e-6f) forward = Vector3.Forward;
        forward = forward.Normalized();
        s._frame3 = new Basis(Vector3.Up.Cross(forward), Vector3.Up, forward);   // SMS: +X left, +Y up, +Z forward
        s._origin = feet;
        return s;
    }

    public override void _Ready()
    {
        TopLevel = true;
        var mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        Texture2D tex = ResourceLoader.Exists("res://assets/Sms/Fx/tex/gout_drop_raw.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/gout_drop_raw.png") : null;
        mat.SetShaderParameter("bead", tex ?? SoftBead());
        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One, Material = mat },
            InstanceCount = _p.Length, VisibleInstanceCount = 0,
        };
        _mmi = new MultiMeshInstance3D { Name = "Beads", Multimesh = _mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_mmi);
    }

    /// <summary>A stand-in for the bead texture until the editor has imported the png: a shaded disc, intensity in RGB, round alpha.</summary>
    private static ImageTexture SoftBead()
    {
        var img = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float dx = (x + 0.5f - 16f) / 15f, dy = (y + 0.5f - 16f) / 15f, r = MathF.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp((1f - r) * 6f, 0f, 1f);
            float hx = (x + 0.5f - 11f) / 6f, hy = (y + 0.5f - 10f) / 6f;
            float i = Mathf.Clamp(0.35f + 0.65f * MathF.Exp(-(hx * hx + hy * hy)) - 0.25f * r, 0f, 1f);
            img.SetPixel(x, y, new Color(i, i, i, a));
        }
        return ImageTexture.CreateFromImage(img);
    }

    private float Rnd() => (float)_rng.NextDouble();
    private float Rnd11() => (float)(_rng.NextDouble() * 2.0 - 1.0);

    private static Color Key((int f, Color c)[] keys, int frame)
    {
        frame = ((frame % CycleFrames) + CycleFrames) % CycleFrames;
        (int f, Color c) a = keys[0], b = keys[^1];
        for (int i = 0; i < keys.Length; i++) { if (keys[i].f <= frame) a = keys[i]; if (keys[i].f >= frame) { b = keys[i]; break; } }
        if (b.f == a.f) return a.c;
        return a.c.Lerp(b.c, (frame - a.f) / (float)(b.f - a.f));
    }

    private void Emit()
    {
        for (int i = 0; i < Rate && _count < _p.Length; i++)
        {
            // volume Sphere: a point on a shell between 0.625 x 60 and 60 u (JPAEmitter calcVolumeSphere, sweep 1)
            float u = Rnd11(), ang = Rnd() * Mathf.Tau, r = 60f * (0.625f + 0.375f * Rnd());
            float h = MathF.Sqrt(MathF.Max(0f, 1f - u * u));
            Vector3 onShell = new Vector3(h * MathF.Cos(ang), u, h * MathF.Sin(ang)) * r;
            _p[_count++] = new P
            {
                Pos = EmitterTrs + onShell, BaseVel = EmitDir * BaseSpeed, FieldAcc = Vector3.Zero,
                Age = 0, Life = Life, Phase = _rng.Next(0, 24), Moment = 0.75f + 0.125f * Rnd11(), Size = BeadUnits * (1f + 0.125f * Rnd11()),
                Child = false, NextChild = (int)(0.875f * Life),
            };
        }
    }

    private void Step()
    {
        if (_frame < EmitFrames) Emit();
        _frame++;
        int n = _count;
        for (int i = 0; i < n; i++)
        {
            ref P q = ref _p[i];
            float progress = q.Age / (float)q.Life;
            Vector3 fieldVel = Vector3.Zero;
            if (!q.Child)
            {
                // Newton (FieldAccel, fade in 0 -> 0.5): mag x (d <= 100 u ? 10 : 10 x 100^2 / d^2) toward its point
                Vector3 d = NewtonPos - q.Pos; float lsq = d.LengthSquared();
                float fadeN = progress < 0.5f ? progress / 0.5f : 1f;
                if (lsq > 1e-6f && fadeN > 0f) q.FieldAcc += d / MathF.Sqrt(lsq) * (NewtonMag * (lsq > NewtonRefSq ? NewtonRefSq * 10f / lsq : 10f) * fadeN);
                // Magnet (FieldVelocity, cut off before 0.5, fade in 0.5 -> 1): 20 u/f toward its point
                if (progress >= 0.5f)
                {
                    Vector3 m = MagnetPos - q.Pos; float ml = m.Length();
                    if (ml > 1e-6f) fieldVel += m / ml * (MagnetMag * ((progress - 0.5f) / 0.5f));
                }
                q.BaseVel *= AirResist;
                fieldVel += q.FieldAcc;
                q.Vel = (q.BaseVel + fieldVel) * q.Moment;
            }
            q.Pos += q.Vel;
            q.Age++;
            // the sweep children: from 87.5 % of the parent's life, one every 2 frames, the parent's colour, +- 20 u, its velocity x (1 +- 0.25)
            if (!q.Child && q.Age >= q.NextChild && q.Age < q.Life && _count < _p.Length)
            {
                q.NextChild = q.Age + 2;
                int f = (q.Age + q.Phase) % CycleFrames;
                _p[_count++] = new P
                {
                    Pos = q.Pos + new Vector3(Rnd11(), Rnd11(), Rnd11()) * 20f, Vel = q.Vel * (1f + 0.25f * Rnd11()), Age = 0, Life = ChildLife,
                    Child = true, Size = ChildUnits, Moment = 1f, Prm = Key(Prm, f), Env = Key(Env, f),
                };
            }
        }
        // retire the dead (swap-remove)
        for (int i = _count - 1; i >= 0; i--) if (_p[i].Age >= _p[i].Life) { _p[i] = _p[_count - 1]; _count--; }
    }

    public override void _Process(double delta)
    {
        _acc += delta;
        int steps = 0;
        while (_acc >= 1.0 / Fps && steps < 8) { _acc -= 1.0 / Fps; Step(); steps++; }
        if (Done) { QueueFree(); return; }
        Draw();
    }

    private void Draw()
    {
        if (_mm == null) return;
        Vector3 camPos = GetViewport()?.GetCamera3D()?.GlobalPosition ?? (_origin + Vector3.Back * 5f);
        float u2m = 1f / SmsUnits.UnitsPerMetre;
        for (int i = 0; i < _count; i++)
        {
            ref P q = ref _p[i];
            float progress = q.Age / (float)q.Life;
            Vector3 world = _origin + _frame3 * (q.Pos * u2m);
            float size, alpha; Color prm, env;
            if (q.Child) { size = q.Size * (1f - progress); alpha = 1f; prm = q.Prm; env = q.Env; }
            else
            {
                size = q.Size;
                alpha = progress < 0.25f ? progress / 0.25f : progress > 0.75f ? (1f - progress) / 0.25f : 1f;
                int f = (q.Age + q.Phase) % CycleFrames; prm = Key(Prm, f); env = Key(Env, f);
            }
            // a billboard along the velocity ("dir Vel"): Y = the flight, X square to it and the eye
            Vector3 vw = _frame3 * q.Vel; float vl = vw.Length();
            Vector3 toCam = (camPos - world).Normalized();
            Vector3 yAxis = vl > 1e-4f ? vw / vl : Vector3.Up;
            if (MathF.Abs(yAxis.Dot(toCam)) > 0.98f) yAxis = Vector3.Up;
            Vector3 xAxis = yAxis.Cross(toCam).Normalized();
            Vector3 zAxis = xAxis.Cross(yAxis);
            float s = size * u2m;
            _mm.SetInstanceTransform(i, new Transform3D(new Basis(xAxis * s, yAxis * s, zAxis), world));
            _mm.SetInstanceColor(i, new Color(prm.R, prm.G, prm.B, Mathf.Clamp(alpha, 0f, 1f)));
            _mm.SetInstanceCustomData(i, new Color(env.R, env.G, env.B, 1f));
        }
        _mm.VisibleInstanceCount = _count;
    }
}
