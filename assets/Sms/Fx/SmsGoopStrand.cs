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
/// Pass 126g: the chain is parametrised by AGE -- the tip is the point at exactly LifeFrames, interpolated between the two particles that bracket it, and the
/// texture's T and the taper run by age, so nothing on the strand moves in steps when a particle dies (the game's own strip jumps by a particle each death;
/// Jaysonn saw the rounded tip flicker twice a second).
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
        float keepTo = LifeFrames + EmitEveryFrames + 1f;              // one point is kept PAST the life: the tip is interpolated to age == LifeFrames between it and the last live one
        _pts.RemoveAll(p => p.Age > keepTo);
        if (emitting)
        {
            _emitAccum += frames;
            while (_emitAccum >= EmitEveryFrames) { _emitAccum -= EmitEveryFrames; _pts.Add(new Pt(mouth, 0f)); }
        }
        _scroll += ScrollPerFrame * frames;
        GlobalTransform = Transform3D.Identity;
        _mesh.ClearSurfaces();
        // the chain by AGE (newest first): the mouth (age 0) while emitting, every point up to the life, then the TIP at exactly age LifeFrames interpolated
        // between the last live point and the one kept past the life -- so the tip moves as the mouth did LifeFrames ago, not in jumps as points die
        // (the game's strip does jump, but its clamped scroll has slid the drop off the end by then; the rounded drop here would flicker)
        float fall = 0.5f * GravityUnits * LengthScale * scale / u;    // metres per frame^2
        _chain.Clear(); _ages.Clear();
        if (emitting) { _chain.Add(mouth); _ages.Add(0f); }
        int beyond = -1;
        for (int i = _pts.Count - 1; i >= 0; i--)
        {
            var p = _pts[i];
            if (p.Age > LifeFrames) { beyond = i; break; }
            _chain.Add(p.Born + Vector3.Down * (fall * p.Age * p.Age)); _ages.Add(p.Age);
        }
        bool steady = beyond >= 0 && _chain.Count >= 1;
        if (steady)
        {
            var pb = _pts[beyond];
            Vector3 beyondPos = pb.Born + Vector3.Down * (fall * pb.Age * pb.Age);
            float a1 = _ages[_ages.Count - 1];
            float w = Mathf.Clamp((LifeFrames - a1) / Mathf.Max(0.001f, pb.Age - a1), 0f, 1f);
            _chain.Add(_chain[_chain.Count - 1].Lerp(beyondPos, w)); _ages.Add(LifeFrames);
        }
        if (_chain.Count < 2) { Visible = false; _hasTip = false; return; }
        Visible = true;
        Vector3 tip = _chain[_chain.Count - 1];
        HangMetres = mouth.Y - tip.Y;
        FallMetres = fall * _ages[_ages.Count - 1] * _ages[_ages.Count - 1];
        if (_hasTip)
        {
            TipStepMetres = tip.DistanceTo(_lastTip);
            if (steady && _wasSteady && TipStepMetres > MaxTipStep)
            {
                MaxTipStep = TipStepMetres;
                MaxStepInfo = $"frames {frames:F2} pts {_pts.Count} chain {_chain.Count} a1 {_ages[_ages.Count - 2]:F2} beyond {(beyond >= 0 ? _pts[beyond].Age : -1f):F2} mouth step {mouth.DistanceTo(_lastMouth):F3} tip {tip} last {_lastTip} emitting {emitting}";
            }
        }
        _lastTip = tip; _lastMouth = mouth; _hasTip = true; _wasSteady = steady;
        // the ribbon's spine: the chain smoothed, Subdivisions vertices a segment; T = age / life (0 at the mouth, 1 at the tip) carried along
        var spine = new List<Vector3>(); var spineT = new List<float>();
        int segs = _chain.Count - 1, sub = Mathf.Max(1, Subdivisions);
        for (int s = 0; s < segs; s++)
        {
            Vector3 p0 = _chain[Mathf.Max(0, s - 1)], p1 = _chain[s], p2 = _chain[s + 1], p3 = _chain[Mathf.Min(_chain.Count - 1, s + 2)];
            for (int k = 0; k < sub; k++) { float f = (float)k / sub; spine.Add(CatmullRom(p0, p1, p2, p3, f)); spineT.Add(Mathf.Lerp(_ages[s], _ages[s + 1], f) / LifeFrames); }
        }
        spine.Add(_chain[_chain.Count - 1]); spineT.Add(_ages[_ages.Count - 1] / LifeFrames);
        Basis frame = anchor.GlobalBasis.Orthonormalized();
        float half = 0.5f * WidthUnits * WidthScale * scale / u;
        int strips = Cross ? 2 : 1;
        for (int st = 0; st < strips; st++)
        {
            Vector3 right = (st == 0 ? frame.X : frame.Z) * half;
            _mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int i = 0; i < spine.Count; i++)
            {
                float t = Mathf.Clamp(spineT[i], 0f, 1f);                         // T by age (JPADrawExecStripeCross's fVar2 runs by index; by age it is continuous)
                float w = WidthAt(t);
                float v = t - _scroll;
                _mesh.SurfaceSetUV(new Vector2(0f, v)); _mesh.SurfaceAddVertex(spine[i] - right * w);
                _mesh.SurfaceSetUV(new Vector2(1f, v)); _mesh.SurfaceAddVertex(spine[i] + right * w);
            }
            _mesh.SurfaceEnd();
        }
        TipWidthFraction = WidthAt(Mathf.Clamp(spineT[spineT.Count - 1], 0f, 1f));
        Rebuilds++;
    }
    private readonly List<float> _ages = new();
    private Vector3 _lastTip, _lastMouth; private bool _hasTip, _wasSteady;
    public string MaxStepInfo { get; private set; } = "";
    /// <summary>Restart the largest-step record (a drive measures one phase of the sway).</summary>
    public void ResetMaxTipStep() { MaxTipStep = 0f; MaxStepInfo = ""; }
    /// <summary>How far the tip moved last frame (metres), and the largest such step since the strand reached its full length -- the flicker measure.</summary>
    public float TipStepMetres { get; private set; }
    public float MaxTipStep { get; private set; }

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
