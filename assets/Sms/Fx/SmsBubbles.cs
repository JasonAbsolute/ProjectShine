using Godot;
using System;

/// <summary>
/// Mario's bubbles under water (MarioParticle.cpp 239-292, MarioMove.cpp 2097): three of the disc's emitters, simulated here particle for
/// particle -- the game's TBubbleCallBack kills a bubble the frame it rises past the water surface and puts the ripple there, which a GPU
/// particle cannot do -- and drawn as two MultiMeshes (billboard quads, additive, the I8 textures). The .jpa fields are the exports.
///   ms_m_awa    swimmingBubbleEffect(): while the head is BubbleDepth (10 u) under the surface, one emitter in the head joint's frame
///               (setGlobalRTMatrix(mHeadMtx)): a point at (22, 5, 0) -- the mouth -- rate 0.1 a frame (x 1 +- 1.0), life 130, 2 u/f along
///               (1, 0.25, 0) in a 45 deg cone (spread 0.25), airResist 0.98, moment 1 +- 0.25, a Gravity field +0.06 u/f^2 (the rise:
///               terminal 3 u/f = 2.4 m/s), a Random field +-4 u/f on the frame every 2 frames (the shimmer), another +-1 u/f into the
///               velocity every 8 (the drift); quad 0.2 x 50 = 10 u, scale X 0.6 -> 1 -> 1.4 and Y 1.4 -> 1 -> 0.6, BOTH ping-ponged on an
///               8-frame cycle (anm type 1 on X and Y: the wobble; pass 78 -- the values are x10 and the old dumper's flag bits were one high),
///               x (1 +- 0.4) a bubble; alpha 0.625 fading from 92.5 %; colour lerp((135,183,231) -> white, tex), additive.
///   ms_m_awa_s  bubbleFromBody(): while the 'center' joint is 10 u under, one emitter in that joint's frame: a shell 25..50 u around it
///               (sphere 50, minRad 0.5, the emitter scaled (0.75, 1, 1)), its rate = lerp(0, 0.5, fwd / 40) a frame (mBodyBubble*),
///               life 90 (-25 %), no launch, Gravity +0.04, Random +-2.5 / 2 frames and +-0.5 / 8, quad 6.5 u, scale X 0.7 -> 1 -> 1.3 /
///               Y 1.3 -> 1 -> 0.7, the same 8-frame ping-pong, x (1 +- 0.4).
///   ms_m_awahamon  the pop: a flat ring 45 u wide at the surface (+3 u), 30 frames, alpha 0.5 -> 0, the texture matrix 1.5 shrinking
///               0.0099 a frame AND the quad's ESP1 scale 0.3 -> 1 by 37.33 %, -> 1.8 at the end, x (1 +- 0.3) (pass 78: the ring grows both
///               ways), one per bubble that crosses the surface (mBubbleToRipple 0.3 != 0).
///   decHP() under water: ten bubbleFromMouth emitters for one frame -- each a new emitter whose first frame always puts out a particle
///               (JPABaseEmitter's FIRST_EMISSION rule), so a gulp of ten; the continuous emitters start with one the same way.
/// A frame is 1/60 s (the particle clock); Mario steps this once a physics frame. Emitters not called a frame stop (the manager's
/// becomeInvalidEmitter): their bubbles live on. Not ported: the per-particle spin (speed +- 0.25 turns a frame: the texture is round).
/// </summary>
public partial class SmsBubbles : Node3D
{
    [Export] public MultiMeshInstance3D Bubbles;
    [Export] public MultiMeshInstance3D Rings;
    [Export] public int MaxBubbles = 160;
    [Export] public int MaxRings = 40;

    [ExportGroup("ms_m_awa (the mouth)")]
    [Export] public float MouthRate = 0.1f;
    [Export] public float MouthRateRndm = 1f;
    [Export] public int MouthLife = 130;
    [Export] public Vector3 MouthOffsetUnits = new(22f, 5f, 0f);
    [Export] public Vector3 MouthDir = new(1f, 0.25f, 0f);
    [Export] public float MouthSpeed = 2f;
    [Export] public float MouthSpread = 0.25f;
    [Export] public float MouthGravity = 0.06f;
    [Export] public float MouthJitter = 8f;
    [Export] public float MouthWalk = 2f;
    [Export] public float MouthSize = 0.2f;
    [Export] public float MouthScaleInX = 0.6f, MouthScaleOutX = 1.4f, MouthScaleInY = 1.4f, MouthScaleOutY = 0.6f; // pass 78: x10 (JPAExtraShape.cpp 32-39)

    [ExportGroup("ms_m_awa_s (the body)")]
    [Export] public float BodyRadiusUnits = 50f;
    [Export] public float BodyMinRad = 0.5f;
    [Export] public Vector3 BodyEmitterScale = new(0.75f, 1f, 1f);
    [Export] public int BodyLife = 90;
    [Export] public float BodyLifeRndm = 0.25f;
    [Export] public float BodyGravity = 0.04f;
    [Export] public float BodyJitter = 5f;
    [Export] public float BodyWalk = 1f;
    [Export] public float BodySize = 0.13f;
    [Export] public float BodyScaleInX = 0.7f, BodyScaleOutX = 1.3f, BodyScaleInY = 1.3f, BodyScaleOutY = 0.7f; // pass 78: x10
    [Export] public float BodySpeedMin = 0f, BodySpeedMax = 40f;    // mBodyBubbleSpMin / Max (units a tick)
    [Export] public float BodyEmitMin = 0f, BodyEmitMax = 0.5f;     // mBodyBubbleEmitMin / Max (particles a frame)

    [ExportGroup("shared")]
    [Export] public float AirResist = 0.98f;
    [Export] public float MomentRndm = 0.25f;
    [Export] public float Alpha = 0.625f;
    [Export] public float AlphaOutTiming = 0.925f;
    [Export] public float ScaleInTiming = 0.375f, ScaleOutTiming = 0.625f, ScaleOutRndm = 0.4f; // ScaleOutRndm: JPADraw.cpp 242 -- the WHOLE scale x (1 +- 0.4), not the out value (pass 78)
    [Export] public int JitterCycle = 2, WalkCycle = 8, WobbleFrames = 8;
    [Export] public float BubbleDepthUnits = 10f;                    // mBubbleDepth: the head / the centre this far under before they bubble

    [ExportGroup("ms_m_awahamon (the pop)")]
    [Export] public int RippleLife = 30;
    [Export] public float RippleSize = 0.9f;
    [Export] public float RippleAlpha = 0.5f;
    /// <summary>ms_m_awahamon ESP1 scale (pass 78): 0.3 -> 1 by 37.33 %, -> 1.8 at the end, x (1 +- 0.3) a ring -- on top of the texture matrix's shrink.</summary>
    [Export] public float RippleScaleIn = 0.3f, RippleScaleOut = 1.8f, RippleScaleTiming = 0.3733f, RippleScaleRndm = 0.3f;
    [Export] public float RippleUvScale = 1.4999f, RippleUvStep = -0.0099f;
    [Export] public float RippleUpUnits = 3f;

    private const float Quad = 50f; // units per baseSize

    private struct Bubble
    {
        public bool Alive;
        public Vector3 Pos, BaseVel;     // metres, metres a frame
        public float Moment, Gravity, Jitter, Walk;   // gravity u/f^2, the random fields' mags (u/f)
        public float Size, ScaleInX, ScaleOutX, ScaleInY, ScaleOutY, ScaleRnd;   // ScaleRnd: JPADraw.cpp 242, 1 + RF x rndm on the whole scale
        public int Age, Life;
    }

    private struct Ring
    {
        public bool Alive;
        public Vector3 Pos;
        public int Age;
        public float ScaleRnd;
    }

    private Bubble[] _b = Array.Empty<Bubble>();
    private Ring[] _r = Array.Empty<Ring>();
    private float _mouthCount, _bodyCount;
    private bool _mouthWas, _bodyWas;
    private readonly Random _rng = new();
    public int AliveBubbles { get; private set; }
    public int AliveRings { get; private set; }
    public int Spawned { get; private set; }
    public int Popped { get; private set; }

    public override void _Ready()
    {
        _b = new Bubble[Math.Max(1, MaxBubbles)];
        _r = new Ring[Math.Max(1, MaxRings)];
        if (Bubbles?.Multimesh != null)
            Bubbles.Multimesh.InstanceCount = _b.Length;
        if (Rings?.Multimesh != null)
            Rings.Multimesh.InstanceCount = _r.Length;
        TopLevel = true; // world-anchored: the bubbles are unbound once born
        GlobalTransform = Transform3D.Identity;
        Draw();
    }

    private float Rnd() => (float)_rng.NextDouble();              // 0..1
    private float RndZh() => (float)_rng.NextDouble() - 0.5f;      // -0.5..0.5 (get_r_zh)
    private float RndPm() => (float)_rng.NextDouble() * 2f - 1f;   // -1..1

    private static float U2M(float units) => SmsUnits.UnitsToMetres(units);

    /// <summary>One particle frame (1/60 s). <paramref name="head"/> / <paramref name="center"/>: the joints' world transforms;
    /// <paramref name="mouthEmit"/> / <paramref name="bodyEmit"/>: swimmingBubbleEffect()'s depth tests passed this frame;
    /// <paramref name="fwdUnits"/>: mForwardVel for the body's rate; <paramref name="surfaceY"/>: the water surface (TBubbleCallBack).</summary>
    public void Step(Transform3D head, bool mouthEmit, Transform3D center, float fwdUnits, bool bodyEmit, float surfaceY)
    {
        // the emitters: called a frame -> emitCount += rate (1 +- rateRndm); not called -> the emitter is gone (a new one later starts at 0)
        if (mouthEmit)
        {
            bool first = !_mouthWas; // a new emitter (JPABaseEmitter FIRST_EMISSION): its first frame always puts out a particle
            if (first) _mouthCount = 0f;
            _mouthCount += MouthRate * (1f + MouthRateRndm * RndPm());
            if (first && MouthRate > 0f && _mouthCount < 1f) _mouthCount = 1f;
            while (_mouthCount >= 1f) { _mouthCount -= 1f; SpawnMouth(head); }
        }
        _mouthWas = mouthEmit;
        if (bodyEmit)
        {
            bool first = !_bodyWas;
            if (first) _bodyCount = 0f;
            float t = fwdUnits > BodySpeedMax ? 1f : fwdUnits > BodySpeedMin ? (fwdUnits - BodySpeedMin) / (BodySpeedMax - BodySpeedMin) : 0f;
            float rate = BodyEmitMin + t * (BodyEmitMax - BodyEmitMin);
            _bodyCount += rate * (1f + 1f * RndPm()); // rate +-1.0 as the file has it
            if (first && rate > 0f && _bodyCount < 1f) _bodyCount = 1f;
            while (_bodyCount >= 1f) { _bodyCount -= 1f; SpawnBody(center); }
        }
        _bodyWas = bodyEmit;
        Advance(surfaceY);
        Draw();
    }

    /// <summary>decHP() under water: bubbleFromMouth(i) for i in 0..9 -- ten new emitters, each putting out its first-frame particle (the
    /// FIRST_EMISSION rule) before the manager stops it: a gulp of ten.</summary>
    public void BurstMouth(Transform3D head, int emitters = 10)
    {
        for (int i = 0; i < emitters; i++)
        {
            float count = MouthRate * (1f + MouthRateRndm * RndPm());
            if (MouthRate > 0f && count < 1f) count = 1f;
            while (count >= 1f) { count -= 1f; SpawnMouth(head); }
        }
    }

    private int Slot()
    {
        for (int i = 0; i < _b.Length; i++)
            if (!_b[i].Alive) return i;
        return -1;
    }

    private void SpawnMouth(Transform3D head)
    {
        int i = Slot();
        if (i < 0) return;
        Basis rot = head.Basis.Orthonormalized();
        Vector3 pos = head.Origin + rot * (MouthOffsetUnits * U2M(1f));
        // the launch: initialVelDir along the emitter's dir, tilted from it by spread x rnd(-1..1) x 180 deg about a random perpendicular
        Vector3 axis = rot * MouthDir.Normalized();
        Vector3 vel = Tilt(axis, MouthSpread * RndPm() * MathF.PI) * MouthSpeed;
        _b[i] = new Bubble
        {
            Alive = true, Pos = pos, BaseVel = vel * U2M(1f), Moment = 1f + MomentRndm * RndPm(),
            Gravity = MouthGravity, Jitter = MouthJitter, Walk = MouthWalk, Size = MouthSize,
            ScaleInX = MouthScaleInX, ScaleOutX = MouthScaleOutX, ScaleInY = MouthScaleInY, ScaleOutY = MouthScaleOutY,
            ScaleRnd = 1f + ScaleOutRndm * RndPm(),
            Age = -1, Life = MouthLife
        };
        Spawned++;
    }

    private void SpawnBody(Transform3D center)
    {
        int i = Slot();
        if (i < 0) return;
        Basis rot = center.Basis.Orthonormalized();
        // calcVolumeSphere: a point on a shell between minRad and 1 of the size, in the emitter's frame, scaled by the emitter's scale
        float r = BodyRadiusUnits * (BodyMinRad + (1f - BodyMinRad) * Rnd());
        float theta = MathF.PI * Rnd(), phi = 2f * MathF.PI * Rnd();
        Vector3 local = new Vector3(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi)) * r;
        local *= BodyEmitterScale;
        Vector3 pos = center.Origin + rot * (local * U2M(1f));
        _b[i] = new Bubble
        {
            Alive = true, Pos = pos, BaseVel = Vector3.Zero, Moment = 1f + MomentRndm * RndPm(),
            Gravity = BodyGravity, Jitter = BodyJitter, Walk = BodyWalk, Size = BodySize,
            ScaleInX = BodyScaleInX, ScaleOutX = BodyScaleOutX, ScaleInY = BodyScaleInY, ScaleOutY = BodyScaleOutY,
            ScaleRnd = 1f + ScaleOutRndm * RndPm(),
            Age = -1, Life = Math.Max(1, (int)(BodyLife * (1f - BodyLifeRndm * Rnd())))
        };
        Spawned++;
    }

    private Vector3 Tilt(Vector3 axis, float angle)
    {
        if (MathF.Abs(angle) < 1e-5f) return axis;
        Vector3 any = MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right;
        Vector3 perp = axis.Cross(any).Normalized();
        perp = perp.Rotated(axis, 2f * MathF.PI * Rnd()); // a random direction around the axis
        return axis.Rotated(perp, angle).Normalized();
    }

    private void Advance(float surfaceY)
    {
        int alive = 0;
        for (int i = 0; i < _b.Length; i++)
        {
            ref Bubble p = ref _b[i];
            if (!p.Alive) continue;
            p.Age++;
            if (p.Age >= p.Life) { p.Alive = false; continue; }
            // the fields, fresh each frame: Gravity -> fieldAccel (into the velocity); Random (FieldVelocity) -> this frame's velocity only;
            // Random (BaseVelocity) -> into the velocity; then airResist on the velocity; pos += (vel + fieldVel) x moment
            Vector3 fieldVel = Vector3.Zero;
            bool first = p.Age == 0;
            if (first || (JitterCycle > 0 && p.Age % JitterCycle == 0))
                fieldVel += new Vector3(RndZh(), RndZh(), RndZh()) * p.Jitter * U2M(1f);
            if (first || (WalkCycle > 0 && p.Age % WalkCycle == 0))
                p.BaseVel += new Vector3(RndZh(), RndZh(), RndZh()) * p.Walk * U2M(1f);
            p.BaseVel += Vector3.Up * (p.Gravity * U2M(1f));
            p.BaseVel *= AirResist;
            p.Pos += (p.BaseVel + fieldVel) * p.Moment;
            if (p.Pos.Y > surfaceY)
            {
                // TBubbleCallBack: the bubble dies at the surface and the ring is left there
                p.Alive = false;
                Popped++;
                SpawnRing(new Vector3(p.Pos.X, surfaceY + U2M(RippleUpUnits), p.Pos.Z));
                continue;
            }
            alive++;
        }
        AliveBubbles = alive;
        int rings = 0;
        for (int i = 0; i < _r.Length; i++)
        {
            ref Ring r = ref _r[i];
            if (!r.Alive) continue;
            r.Age++;
            if (r.Age >= RippleLife) { r.Alive = false; continue; }
            rings++;
        }
        AliveRings = rings;
    }

    private void SpawnRing(Vector3 at)
    {
        for (int i = 0; i < _r.Length; i++)
        {
            if (_r[i].Alive) continue;
            _r[i] = new Ring { Alive = true, Pos = at, Age = 0, ScaleRnd = 1f + RippleScaleRndm * RndPm() };
            return;
        }
    }

    private void Draw()
    {
        var mm = Bubbles?.Multimesh;
        if (mm != null)
        {
            for (int i = 0; i < _b.Length && i < mm.InstanceCount; i++)
            {
                ref Bubble p = ref _b[i];
                if (!p.Alive || p.Age < 0)
                {
                    mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero));
                    continue;
                }
                float t = p.Age / (float)p.Life;
                float alpha = t < AlphaOutTiming ? Alpha : Alpha * (1f - (t - AlphaOutTiming) / MathF.Max(1e-4f, 1f - AlphaOutTiming));
                // JPADrawVisitor.cpp 1600-1617 (anm type 1 on X and Y, cycle 8): the curve's timer ping-pongs 0 -> 1 -> 0 over 16 frames of age
                float tw = t;
                if (WobbleFrames > 0)
                {
                    float fr = (p.Age % WobbleFrames) / (float)WobbleFrames;
                    int odd = (p.Age / WobbleFrames) & 1;
                    tw = odd + (fr - odd * 2f * fr);
                }
                float sx = p.ScaleRnd * ScaleFade(tw, p.ScaleInX, p.ScaleOutX);
                float sy = p.ScaleRnd * ScaleFade(tw, p.ScaleInY, p.ScaleOutY);
                float quad = U2M(p.Size * Quad);
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(quad * sx, quad * sy, 1f)), p.Pos));
                mm.SetInstanceColor(i, new Color(1f, 1f, 1f, alpha));
            }
        }
        var rm = Rings?.Multimesh;
        if (rm != null)
        {
            for (int i = 0; i < _r.Length && i < rm.InstanceCount; i++)
            {
                ref Ring r = ref _r[i];
                if (!r.Alive)
                {
                    rm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero));
                    continue;
                }
                float t = r.Age / (float)RippleLife;
                float grow = t < RippleScaleTiming ? Mathf.Lerp(RippleScaleIn, 1f, RippleScaleTiming > 0f ? t / RippleScaleTiming : 1f)
                    : Mathf.Lerp(1f, RippleScaleOut, RippleScaleTiming < 1f ? (t - RippleScaleTiming) / (1f - RippleScaleTiming) : 1f); // in == out: no flat part
                float size = U2M(RippleSize * Quad) * grow * r.ScaleRnd;
                rm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(size, 1f, size)), r.Pos));
                rm.SetInstanceColor(i, new Color(1f, 1f, 1f, RippleAlpha * (1f - t)));
                rm.SetInstanceCustomData(i, new Color(RippleUvScale + RippleUvStep * r.Age, 0f, 0f, 0f));
            }
        }
    }

    /// <summary>ESP1's scale envelope: inValue -> 1 over the first inTiming of the life, 1 to outTiming, 1 -> outValue at the end.</summary>
    private float ScaleFade(float t, float inValue, float outValue)
    {
        if (t < ScaleInTiming)
            return Mathf.Lerp(inValue, 1f, ScaleInTiming > 0f ? t / ScaleInTiming : 1f);
        if (t < ScaleOutTiming)
            return 1f;
        return Mathf.Lerp(1f, outValue, ScaleOutTiming < 1f ? (t - ScaleOutTiming) / (1f - ScaleOutTiming) : 1f);
    }

    public string Stats => $"bubbles {AliveBubbles} rings {AliveRings} spawned {Spawned} popped {Popped}";

    /// <summary>The simulation's view of slot <paramref name="i"/> (for a headless check: the dummy renderer keeps no MultiMesh data).</summary>
    public bool Peek(int i, out Vector3 pos, out int age, out int life)
    {
        pos = Vector3.Zero; age = 0; life = 0;
        if (i < 0 || i >= _b.Length || !_b[i].Alive) return false;
        pos = _b[i].Pos; age = _b[i].Age; life = _b[i].Life;
        return true;
    }

    public int Capacity => _b.Length;
}
