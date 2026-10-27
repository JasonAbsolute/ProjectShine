using System.Collections.Generic;
using Godot;

/// <summary>
/// A JPA STRIPE emitter (ms_gkpa_yodare_l / _s, pass 126e): the gatekeeper's drool. The game emits a point particle at the mouth joint every
/// <see cref="EmitEveryFrames"/> frames (rate 0.5 / 1.0), each falling under the Gravity field (1.2 u/f^2 as BaseVelocity) for <see cref="LifeFrames"/>
/// frames (13 / 10), and JPAStripe draws ONE ribbon threaded through the live particles in emission order -- the newest at the mouth, the oldest
/// hanging 0.6 x life^2 units below where the mouth was when it was born; StripeCross = two such ribbons at right angles (the joint's X and Z),
/// <see cref="WidthUnits"/> wide (base size 1.0 / 0.8 x 50 u), P_msm_yodare_ia along them with the texture scrolling <see cref="ScrollPerFrame"/>
/// a frame (tex inc trs (0, 0.0137)), coloured lerp(env -> prm, I) with the stage's pollution colours (SMSSetEmitterPolColor 6), alpha compare GEQUAL
/// 192, depth written. A MeshInstance3D rebuilt every frame from the parent <see cref="SmsNpcEffect"/>'s position (it follows the joint, rotation
/// too); new points only while the effect <see cref="SmsNpcEffect.IsEmitting"/>, so a Stop lets the strand run out and the effect's drain frees it.
/// Scale (InheritScale: the actor's) comes from the parent effect's EffectScaleApplied.
/// </summary>
[GlobalClass]
public partial class SmsGoopStrand : MeshInstance3D
{
    /// <summary>The ribbon's width in game units (yodare_l 50, yodare_s 40).</summary>
    [Export] public float WidthUnits = 50f;
    /// <summary>A particle's life in frames (13 / 10): the strand's length in time.</summary>
    [Export] public int LifeFrames = 13;
    /// <summary>Frames between particles (yodare_l 2, yodare_s 1).</summary>
    [Export] public float EmitEveryFrames = 2f;
    /// <summary>The Gravity field, units a frame squared, added to the base velocity each frame (1.2).</summary>
    [Export] public float GravityUnits = 1.2f;
    /// <summary>The texture's V scroll a frame (0.0137).</summary>
    [Export] public float ScrollPerFrame = 0.0137f;
    /// <summary>Two ribbons at right angles (StripeCross) or one.</summary>
    [Export] public bool Cross = true;
    /// <summary>The ribbon's material (JpaQuad.gdshader: tex, prm, env, alpha_ref).</summary>
    [Export] public Material StrandMaterial;

    private readonly struct Pt { public readonly Vector3 Born; public readonly float Age; public Pt(Vector3 b, float a) { Born = b; Age = a; } }
    private readonly List<Pt> _pts = new();
    private float _emitAccum, _scroll;
    private ImmediateMesh _mesh;
    private SmsNpcEffect _effect;
    public int PointCount => _pts.Count;
    public int Rebuilds { get; private set; }

    public override void _Ready()
    {
        _mesh = new ImmediateMesh();
        Mesh = _mesh;
        if (StrandMaterial != null) MaterialOverride = StrandMaterial;
        _effect = GetParent() as SmsNpcEffect;
        TopLevel = true;                                  // the ribbon is built in WORLD space (the old points stay where the mouth was)
        ProcessPriority = 101;                            // after the effect's Follow (100)
        CastShadow = ShadowCastingSetting.Off;
    }

    public override void _Process(double delta)
    {
        float frames = (float)(delta * 60.0);
        float u = SmsUnits.UnitsPerMetre;
        float scale = _effect != null ? _effect.EffectScaleApplied : 1f;
        Node3D anchor = _effect ?? GetParent() as Node3D;
        if (anchor == null) return;
        Vector3 mouth = anchor.GlobalPosition;
        bool emitting = _effect == null || _effect.IsEmitting;
        for (int i = 0; i < _pts.Count; i++) _pts[i] = new Pt(_pts[i].Born, _pts[i].Age + frames);
        _pts.RemoveAll(p => p.Age > LifeFrames);
        if (emitting)
        {
            _emitAccum += frames;
            while (_emitAccum >= EmitEveryFrames) { _emitAccum -= EmitEveryFrames; _pts.Add(new Pt(mouth, 0f)); }
        }
        _scroll += ScrollPerFrame * frames;
        GlobalTransform = Transform3D.Identity;
        _mesh.ClearSurfaces();
        if (_pts.Count < 2) { Visible = false; return; }
        Visible = true;
        // the newest point rides the mouth; each older one hangs 0.6 x age^2 units under where the mouth was at its birth
        var pos = new Vector3[_pts.Count + 1];
        pos[0] = mouth;
        for (int i = 0; i < _pts.Count; i++)
        {
            var p = _pts[_pts.Count - 1 - i];                                 // newest first
            pos[i + 1] = p.Born + Vector3.Down * (0.5f * GravityUnits * p.Age * p.Age * scale / u);
        }
        Basis frame = anchor.GlobalBasis.Orthonormalized();
        float half = 0.5f * WidthUnits * scale / u;
        int strips = Cross ? 2 : 1;
        for (int s = 0; s < strips; s++)
        {
            Vector3 right = (s == 0 ? frame.X : frame.Z) * half;
            _mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int i = 0; i < pos.Length; i++)
            {
                float v = (float)i / (pos.Length - 1) - _scroll;
                _mesh.SurfaceSetUV(new Vector2(0f, v)); _mesh.SurfaceAddVertex(pos[i] - right);
                _mesh.SurfaceSetUV(new Vector2(1f, v)); _mesh.SurfaceAddVertex(pos[i] + right);
            }
            _mesh.SurfaceEnd();
        }
        Rebuilds++;
    }
}
