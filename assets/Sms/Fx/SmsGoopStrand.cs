using System.Collections.Generic;
using Godot;

/// <summary>
/// A JPA STRIPE emitter (ms_gkpa_yodare_l / _s, pass 126e/f): the gatekeeper's drool. The game emits a point particle at the mouth joint every
/// <see cref="EmitEveryFrames"/> frames (rate 0.5 / 1.0), each falling under the Gravity field (1.2 u/f^2 as BaseVelocity) for <see cref="LifeFrames"/>
/// frames (13 / 10), and JPADrawExecStripeCross threads ONE ribbon through the live particles in emission order (JPADrawVisitor.cpp 1205): the newest at the
/// mouth, the oldest hanging 0.6 x life^2 units below where the mouth was when it was born; two ribbons at right angles, <see cref="WidthUnits"/> wide
/// (base size 1.0 / 0.8 x 50 u), texcoord T running 0 at the mouth to 1 at the tip -- P_msm_yodare (a single teardrop, rounded at both ends, a
/// highlight down one side; BTI wrap CLAMP) mapped ONCE along the strand, coloured lerp(env -> prm, I) with the stage's pollution colours
/// (SMSSetEmitterPolColor 6), alpha compare GEQUAL 192, depth written. The game's texture scroll (tex inc trs (0, 0.0137) a frame on a CLAMPED texture)
/// slides the drop off the strand within 1.2 s and leaves the edge row: a thin plain string -- <see cref="ScrollPerFrame"/> is 0 by default (DEVIATION
/// knob: the game's 0.0137 brings that back). A MeshInstance3D rebuilt every frame from the parent <see cref="SmsNpcEffect"/>'s position (it follows the
/// joint, rotation too), the chain smoothed (Catmull-Rom, <see cref="Subdivisions"/> a segment) and the last <see cref="TipTaper"/> of it narrowed on a
/// quarter circle to a rounded point (Jaysonn: "rounded tips"); <see cref="LengthScale"/> stretches the hang (Jaysonn: "not long enough"; 1 = the game's).
/// New points only while the effect <see cref="SmsNpcEffect.IsEmitting"/>, so a Stop lets the strand run out and the effect's drain frees it. Scale
/// (InheritScale: the actor's) comes from the parent effect's EffectScaleApplied.
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
    /// <summary>How far the strand hangs, as a multiple of the game's (0.6 x life^2 u = 1.36 m for the long one). Set by SmsGateKeeper.DroolLength.</summary>
    [Export] public float LengthScale = 1f;
    /// <summary>The ribbon's width as a multiple of the game's. Set by SmsGateKeeper.DroolWidth.</summary>
    [Export] public float WidthScale = 1f;
    /// <summary>The texture's V scroll a frame (the game: 0.0137 on a clamped texture -- the drop slides off within 1.2 s and a thin plain string is left; 0 keeps
    /// the drop mapped once along the strand).</summary>
    [Export] public float ScrollPerFrame = 0f;
    /// <summary>The last part of the strand (0..1) that narrows on a quarter circle to a rounded point (0 = the game's flat cut).</summary>
    [Export(PropertyHint.Range, "0,0.8,0.01")] public float TipTaper = 0.35f;
    /// <summary>Ribbon vertices a segment between two particles (the chain smoothed with Catmull-Rom; 1 = the game's straight segments).</summary>
    [Export(PropertyHint.Range, "1,8,1")] public int Subdivisions = 3;
    /// <summary>Two ribbons at right angles (StripeCross) or one.</summary>
    [Export] public bool Cross = true;
    /// <summary>The ribbon's material (JpaStripe.gdshader: tex, prm, env, alpha_ref).</summary>
    [Export] public Material StrandMaterial;

    private readonly struct Pt { public readonly Vector3 Born; public readonly float Age; public Pt(Vector3 b, float a) { Born = b; Age = a; } }
    private readonly List<Pt> _pts = new();
    private readonly List<Vector3> _chain = new();
    private float _emitAccum, _scroll;
    private ImmediateMesh _mesh;
    private SmsNpcEffect _effect;
    public int PointCount => _pts.Count;
    public int Rebuilds { get; private set; }
    /// <summary>The tip's distance below the mouth, metres, as last built.</summary>
    public float HangMetres { get; private set; }
    /// <summary>The oldest point's own fall, metres (0.5 x gravity x age^2 x LengthScale x the actor scale): the strand's length by the field alone, whatever
    /// the mouth did meanwhile.</summary>
    public float FallMetres { get; private set; }
    /// <summary>The ribbon's half width at its last vertex pair over the full half width (0 = a point).</summary>
    public float TipWidthFraction { get; private set; } = 1f;

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
        // the chain: the newest point rides the mouth; each older one hangs 0.6 x age^2 units (x LengthScale) under where the mouth was at its birth
        _chain.Clear();
        _chain.Add(mouth);
        for (int i = _pts.Count - 1; i >= 0; i--)
        {
            var p = _pts[i];
            _chain.Add(p.Born + Vector3.Down * (0.5f * GravityUnits * p.Age * p.Age * LengthScale * scale / u));
        }
        HangMetres = mouth.Y - _chain[_chain.Count - 1].Y;
        FallMetres = _pts[0].Born.Y - _chain[_chain.Count - 1].Y;
        // the ribbon's spine: the chain smoothed, Subdivisions vertices a segment
        var spine = new List<Vector3>();
        int segs = _chain.Count - 1, sub = Mathf.Max(1, Subdivisions);
        for (int s = 0; s < segs; s++)
        {
            Vector3 p0 = _chain[Mathf.Max(0, s - 1)], p1 = _chain[s], p2 = _chain[s + 1], p3 = _chain[Mathf.Min(_chain.Count - 1, s + 2)];
            for (int k = 0; k < sub; k++) spine.Add(CatmullRom(p0, p1, p2, p3, (float)k / sub));
        }
        spine.Add(_chain[_chain.Count - 1]);
        Basis frame = anchor.GlobalBasis.Orthonormalized();
        float half = 0.5f * WidthUnits * WidthScale * scale / u;
        int strips = Cross ? 2 : 1;
        int last = spine.Count - 1;
        for (int st = 0; st < strips; st++)
        {
            Vector3 right = (st == 0 ? frame.X : frame.Z) * half;
            _mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int i = 0; i <= last; i++)
            {
                float t = (float)i / last;                                        // T: 0 at the mouth, 1 at the tip (JPADrawExecStripeCross's fVar2)
                float w = WidthAt(t);
                float v = t - _scroll;
                _mesh.SurfaceSetUV(new Vector2(0f, v)); _mesh.SurfaceAddVertex(spine[i] - right * w);
                _mesh.SurfaceSetUV(new Vector2(1f, v)); _mesh.SurfaceAddVertex(spine[i] + right * w);
            }
            _mesh.SurfaceEnd();
        }
        TipWidthFraction = WidthAt(1f);
        Rebuilds++;
    }

    /// <summary>The half-width factor along the strand: 1, then over the last TipTaper a quarter circle down to a point (0.02: a sliver, not a degenerate strip).</summary>
    private float WidthAt(float t)
    {
        if (TipTaper <= 0f || t < 1f - TipTaper) return 1f;
        float s = Mathf.Clamp((t - (1f - TipTaper)) / TipTaper, 0f, 1f);
        return Mathf.Max(0.02f, Mathf.Sqrt(Mathf.Max(0f, 1f - s * s)));
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
