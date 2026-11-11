using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 167: GHOST BREATH (KINGBOO_REMATCH_gdoc.txt, attack 4: "Crown glows frosty blue. He blows cold breath in a sweep across the arena, left to
/// right or right to left. A hit freezes Mario for one second"). King Boo makes one (his Breath move) at his mouth and it runs on its own: a STREAM of
/// cold -- puffs of pale blue mist (the port's smoke ball, mix-blended), ice glints in it, a cold light at the mouth -- blown out at BreathSpeed, aimed
/// DownDeg under level so it reaches the floor and ROLLS along it (the puffs sink at Sink and collide with a slab under the floor), each puff growing from
/// PuffSizeStart to PuffSizeEnd over its PuffSeconds (its reach: BreathSpeed x PuffSeconds, 33 m), the mouth SWEEPING through SweepDeg about the way to
/// Mario over SweepSeconds -- from his left to his right as he faces King Boo (SweepWay LeftToRight), the other way, or a coin (the doc). THE COLD is the
/// stream itself: every frame of blowing is a ray from the mouth, and the puff that left along it is where its age puts it (BreathSpeed out, the sink,
/// the floor); a live puff (age under PuffSeconds) whose ball -- HitRadiusK of its size -- meets Mario's damage cylinder (42 u across, 130 u tall from
/// his feet) is THE HIT, unless his feet are HitHeight or more over the floor: the cold hugs the floor, a jump clears it (the fight's dodge; the sweep
/// crosses a spot in a third of a second). The hit is the FREEZE -- Mario.SmsFreezeFor(FreezeSeconds): the doc's one second, "the one reaction Sunshine
/// does not have" -- nothing moves him, his pose is held, his body carries the frost fog, no HP; and the ICE about him (<see cref="SmsIceBlock"/>), which
/// shatters as he thaws. Once per attack unless OneFreezePerAttack is off (the stream passes once anyway). MSD_SE_BS_TELESA_BREATHE as the blow begins (and
/// every BreathSeEvery), the bottle's wind under it. <see cref="Cancel"/> (King Boo hit through the attack) cuts the breath off: no more cold, the puffs
/// out fade. Signals Swept(hit) as the sweep ends, Finished as the last puff goes.
/// Pass 172 (Jaysonn: "the breath attack should be thicker"): the puffs bigger (3 -> 8 m), more of them (520), more solid (PuffAlpha 0.72), scattered wider
/// (9 deg), and a second, DENSE CORE down the middle of the stream (CorePuffs: smaller, whiter, nearly solid puffs, hugging the ray) so the breath reads as a
/// thick rolling bank of cold rather than a line of puffs. The cold that hits is HitRadiusK of a puff's size as before (0.35 of the bigger puffs: 1.05 -> 2.8 m).
/// </summary>
public partial class SmsGhostBreath : Node3D
{
    public enum Way { Random, LeftToRight, RightToLeft }
    public enum Phase { Idle, Blow, Tail, Done }

    [ExportGroup("The sweep")]
    /// <summary>The whole arc the mouth sweeps (degrees, centred on the way to Mario), how long it takes, which way (as Mario sees it facing King Boo; Random:
    /// a coin), how far under level the breath is aimed (degrees; it meets the floor about 10 m out and rolls on).</summary>
    [Export] public float SweepDeg = 150f, SweepSeconds = 1.6f;
    [Export] public Way SweepWay = Way.Random;
    [Export] public float DownDeg = 10f;
    /// <summary>How fast the cold blows (m/s), how long a puff of it lives (seconds; its reach is the two multiplied), how hard it sinks (m/s^2), how wide
    /// the stream scatters (degrees either side).</summary>
    [Export] public float BreathSpeed = 22f, PuffSeconds = 1.5f, Sink = 4f, SpreadDeg = 9f;
    /// <summary>199 (Jaysonn: "the breath coming out of his mouth a bit wider -- King Boo has a wide mouth, one point of contact looks off"): the cold leaves a MOUTH
    /// MouthWidth across and MouthHeight tall (a box at the mouth point, across the stream; 157-198: a point 0.7 m across) -- the puffs, the dense core and the
    /// glints all born across it, the mouth's light stretched to its width -- and the cold that freezes runs in HitLanes lanes across it (each frame a ray from
    /// each lane: the left edge, the middle, the right edge), so the hit is as wide as the stream from the first metre.</summary>
    [Export(PropertyHint.Range, "0.5,9,0.1")] public float MouthWidth = 4.5f;
    [Export(PropertyHint.Range, "0.2,4,0.1")] public float MouthHeight = 1.4f;
    [Export(PropertyHint.Range, "1,7,1")] public int HitLanes = 3;
    [ExportGroup("The hit (187: the cold that freezes is the DENSE cold)")]
    /// <summary>187 (Jaysonn: "the freeze breath still feels like I'm unfairly getting hit"): a puff freezes only while it is SOLID -- its ball is HitRadiusK of its
    /// size scaled by its own fade (the puffs fade in over the first tenth of their life and out over the last third: the thin blue edge and the fading tail
    /// no longer freeze), and never past HitLifeK of its life (0.75: the freeze's reach is 25 m, the mist rolls on harmless to 33); HitRadiusK 0.35 -> 0.3 (the
    /// dense white core's own size, 4.5 m across at the end); HitHeight 1.2 -> 1.0 (a jump clears the cold a little earlier and later: his feet are over 1 m for
    /// 0.55 s of a held jump). 167-186: the full ball to the end of the puff's life, invisible mist freezing him.</summary>
    [Export(PropertyHint.Range, "0.3,1,0.05")] public float HitLifeK = 0.75f;
    [Export] public bool HitFollowsFade = true;
    /// <summary>A puff's size as it leaves the mouth and at its end (metres across), what part of that is the cold that hits (0.4: the smoke ball's solid
    /// middle), how high over the floor the cold reaches -- a Mario whose feet are higher is over it -- and how long the freeze holds him.</summary>
    [Export] public float PuffSizeStart = 3f, PuffSizeEnd = 8f;
    [Export(PropertyHint.Range, "0.1,0.6,0.01")] public float HitRadiusK = 0.3f;
    [Export] public float HitHeight = 1f, FreezeSeconds = 1f;
    [Export] public bool OneFreezePerAttack = true;
    /// <summary>192 (Jaysonn: "the hit boxes for the freeze breath are still off from the visuals"): the cold that freezes DRAWN -- a red ball on every live puff's
    /// hit (its HitRadius, where the hit maths puts it), so the cold you see and the cold that counts can be compared in play (King Boo's ShowHitBoxes; his
    /// cylinder is drawn there too). The same pass pins the dense white core to the hit maths: its puffs now leave at BreathSpeed (0.97-1.03 of it; 0.92-1.02
    /// before) and sink at Sink (1.1 x before), so the solid white front IS the hit front -- the pale outer mist (0.85-1 of the speed) trails behind it, harmless.
    /// And the REAL mismatch, found with the balls drawn: the puffs' floor collision carried a FRICTION of 0.15 -- Godot takes that much of a particle's speed
    /// EVERY FRAME it touches the floor, so a puff that rolled onto the stone 9 m out stopped dead within 2 m more while the cold that freezes ran on to 25 m:
    /// Mario was frozen with the visible mist 5-10 m behind him. FloorDrag is that friction, 0 now: the mist slides out with the hit maths (the hit maths never
    /// slowed a puff on the floor); 0.15 brings the old stall back (and the hit back out of step with it).</summary>
    [Export] public bool ShowHitBox;
    [Export(PropertyHint.Range, "0,1,0.01")] public float FloorDrag = 0f;
    /// <summary>178 (Jaysonn: "it looks like mario doesn't get hit but still is frozen"): the camera's damage shake on the freeze (x the game's; 0 = none) --
    /// the hit's own look is the ice's (SmsIceBlock.HitFrom: the splash, the flash, the ice climbing him).</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float HitShake = 0.8f;
    [ExportGroup("The look")]
    /// <summary>The mist's colour and how solid it is, the glints' colour, how many puffs and glints the stream carries, the cold light at the mouth (metres
    /// across; 0 = none).</summary>
    [Export] public Color BreathColor = new Color(0.8f, 0.93f, 1f), GlintColor = new Color(0.6f, 0.95f, 1f);
    [Export(PropertyHint.Range, "0,1,0.01")] public float PuffAlpha = 0.72f;
    [Export(PropertyHint.Range, "0,800,1")] public int Puffs = 520;
    /// <summary>172: the dense CORE down the middle of the stream -- how many puffs (0 = none), their size as they leave and at their end (metres across), how
    /// solid they are, how far they scatter (degrees either side; the stream's own is SpreadDeg).</summary>
    [Export(PropertyHint.Range, "0,400,1")] public int CorePuffs = 180;
    [Export] public float CoreSizeStart = 1.6f, CoreSizeEnd = 4.5f, CoreSpreadDeg = 3f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float CoreAlpha = 0.9f;
    [Export(PropertyHint.Range, "0,200,1")] public int Glints = 90;
    [Export] public float MouthGlowSize = 3f;
    /// <summary>A glint's size (metres; the quad -- the process material's scale range did not take here).</summary>
    [Export] public float GlintSize = 0.45f;
    [ExportGroup("The sounds")]
    /// <summary>211: the breath is Jaysonn's Freezzard blow (BREATH_BLOW, 3.4 s; played again every BreathSeEvery while he blows), the game's bottle wind under it at -6 dB (empty WindSe for the blow alone).</summary>
    [Export] public string BreathSe = "BREATH_BLOW", WindSe = "MSD_SE_MA_BOTTLE_WIND";
    /// <summary>The breathe sound again this often through the blow (0 = once).</summary>
    [Export] public float BreathSeEvery = 3.2f;
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void SweptEventHandler(bool hit);
    [Signal] public delegate void FinishedEventHandler();

    public Phase State { get; private set; } = Phase.Idle;
    public int Hits { get; private set; }
    public bool HitMario => _hit;
    /// <summary>The sweep: 0 at its start, 1 at its end; the mouth's yaw now and the way it blows; where it started and where it ends; which way (+1: from
    /// Mario's left); the floor; the mouth now; whether it is blowing; the yaw it was centred on.</summary>
    public float SweepK => Mathf.Clamp(_t / Mathf.Max(0.01f, SweepSeconds), 0f, 1f);
    public float YawNow => _yawNow;
    public Vector3 DirNow => _dirNow;
    public float StartYaw => _yawCentre - _side * Mathf.DegToRad(SweepDeg) * 0.5f;
    public float EndYaw => _yawCentre + _side * Mathf.DegToRad(SweepDeg) * 0.5f;
    public float CentreYaw => _yawCentre;
    public int Side => _side;
    public float FloorY { get; private set; }
    public Vector3 MouthPoint => _mouthNow;
    public bool Emitting => _puffs != null && _puffs.Emitting;
    public bool CoreEmitting => _core != null && _core.Emitting;
    public int CorePuffCount => _core != null ? _core.Amount : 0;
    public int RaysBlown => _rays.Count;
    /// <summary>199, for the drives: the frames the blow ran (one HitLanes set of rays each) -- RaysBlown is HitLanes x this.</summary>
    public int BlowFrames { get; private set; }
    public float Age => _age;
    public Vector3 FreezePoint { get; private set; }
    public float FreezeAt { get; private set; } = -1f;
    public static readonly List<SmsGhostBreath> All = new();
    public static int FreezeCount;

    private const float U = 74.44f;
    private Mario _mario; private Func<Vector3> _mouth; private SmsActorSound _sound; private readonly Random _rng = new(167);
    private GpuParticles3D _puffs, _glints, _core; private MeshInstance3D _glow; private StandardMaterial3D _glowMat; private GpuParticlesCollisionBox3D _slab;
    private MultiMeshInstance3D _hitBalls;                                                                                                   // 192: the hit, drawn
    /// <summary>192, for the drives: the hit balls drawn this frame (0 when ShowHitBox is off); the balls shown or hidden for a frame (the shots compare the mist with and without them).</summary>
    public int HitBallsDrawn { get; private set; }
    public bool HitBallsShown { get => _hitBalls != null && _hitBalls.Visible; set { if (_hitBalls != null) _hitBalls.Visible = value; } }
    private readonly List<(float t, Vector3 o, Vector3 d)> _rays = new();
    private float _t, _age, _yawCentre, _yawNow, _seT, _tailT; private int _side = 1; private Vector3 _dirNow = Vector3.Forward, _mouthNow; private bool _hit;

    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>Which side the sweep starts from for a way: +1 is Mario's LEFT as he faces King Boo (the start direction, yawCentre less half the arc, points
    /// to his left), -1 his right; Random a coin from the caller's dice.</summary>
    public static int SideFor(Way way, float yawCentre, Random rng)
    {
        if (way == Way.Random) return rng.NextDouble() < 0.5 ? 1 : -1;
        // King Boo looks along yawCentre at Mario; Mario looks back: his left is up x (-that way). The start with side +1 is at yawCentre - half.
        Vector3 toMario = new Vector3(MathF.Sin(yawCentre), 0f, MathF.Cos(yawCentre));
        Vector3 marioLeft = Vector3.Up.Cross(-toMario);
        float ys = yawCentre - Mathf.DegToRad(60f);
        bool plusIsLeft = new Vector3(MathF.Sin(ys), 0f, MathF.Cos(ys)).Dot(marioLeft) > 0f;
        return (way == Way.LeftToRight) == plusIsLeft ? 1 : -1;
    }

    /// <summary>The breath begins: from the mouth (read every frame), sweeping about yawCentre (the way to Mario) from the side given, over the floor at floorY.</summary>
    public void Begin(Mario mario, float floorY, Func<Vector3> mouth, float yawCentre, int side)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY; _mouth = mouth; _yawCentre = yawCentre; _side = side >= 0 ? 1 : -1;
        _sound = SmsActorSound.Attach(this);
        _mouthNow = mouth();
        GlobalPosition = _mouthNow;
        _puffs = MakePuffs(); _core = MakeCore(); _glints = MakeGlints(); MakeGlow(); MakeSlab();
        if (ShowHitBox) MakeHitBalls();                                                                                                      // 192
        State = Phase.Blow; _t = 0f; _age = 0f; _seT = 0f;
        Aim(0f);
        if (_puffs != null) { _puffs.Visible = true; _puffs.Emitting = true; }
        if (_core != null) { _core.Visible = true; _core.Emitting = true; }
        if (_glints != null) { _glints.Visible = true; _glints.Emitting = true; }
        if (_glow != null) _glow.Visible = true;
        if (_sound != null) { if (!string.IsNullOrEmpty(BreathSe)) _sound.Play(BreathSe); if (!string.IsNullOrEmpty(WindSe)) _sound.Play(WindSe, -6f); }
        if (DebugPrint) GD.Print($"[SMS breath] {Name}: BLOWS from {_mouthNow} ({_mouthNow.Y - FloorY:F2} m over the floor), {SweepDeg} deg over {SweepSeconds} s from Mario's {(_side > 0 ? "left" : "right")} (yaw {Mathf.RadToDeg(StartYaw):F0} -> {Mathf.RadToDeg(EndYaw):F0}), {BreathSpeed} m/s for {PuffSeconds} s ({BreathSpeed * PuffSeconds:F0} m), {DownDeg} deg down");
    }

    public float PuffSize(float age) => Mathf.Lerp(PuffSizeStart, PuffSizeEnd, Mathf.Clamp(age / Mathf.Max(0.01f, PuffSeconds), 0f, 1f));
    /// <summary>199: the mouth's emission box about the mouth point, in the emitters' frame (x across the stream, y up, z along it).</summary>
    public Vector3 MouthExtents => new Vector3(Mathf.Max(0.1f, MouthWidth * 0.5f), Mathf.Max(0.05f, MouthHeight * 0.5f), 0.2f);
    /// <summary>199, for the drives: the mist puffs' emission shape and box, as made.</summary>
    public string PuffEmission => _puffs?.ProcessMaterial is ParticleProcessMaterial pm ? $"{pm.EmissionShape} {pm.EmissionBoxExtents}" : "none";
    public Vector3 PuffEmissionExtents => _puffs?.ProcessMaterial is ParticleProcessMaterial pm ? pm.EmissionBoxExtents : Vector3.Zero;
    /// <summary>187: a puff's own fade at this age (the ColorRamp Fade draws: in over the first tenth, 0.75 of full by two thirds, out by the end), 0..1.</summary>
    private float PuffFade(float age)
    {
        float k = Mathf.Clamp(age / Mathf.Max(0.01f, PuffSeconds), 0f, 1f);
        if (k < 0.1f) return k / 0.1f;
        if (k < 0.65f) return Mathf.Lerp(1f, 0.75f, (k - 0.1f) / 0.55f);
        return Mathf.Lerp(0.75f, 0f, (k - 0.65f) / 0.35f);
    }
    /// <summary>187: the cold's ball at this age -- HitRadiusK of the puff's size, by its fade (HitFollowsFade), none past HitLifeK of its life.</summary>
    public float HitRadius(float age)
    {
        if (age < 0f || age > PuffSeconds * HitLifeK) return 0f;
        return PuffSize(age) * HitRadiusK * (HitFollowsFade ? PuffFade(age) : 1f);
    }
    /// <summary>Where the puff blown along a ray is at this age: out at BreathSpeed, sunk, no lower than its ball on the floor.</summary>
    private Vector3 PuffCentre((float t, Vector3 o, Vector3 d) ray, float age)
    {
        Vector3 c = ray.o + ray.d * (BreathSpeed * age);
        c.Y -= 0.5f * Sink * age * age;
        float r = PuffSize(age) * HitRadiusK;
        if (c.Y < FloorY + r) c.Y = FloorY + r;
        return c;
    }
    /// <summary>The nearest live cold to a point on the floor: the smallest (flat distance to a puff's middle less its ball), or a big number.</summary>
    public float NearestCold(Vector3 feet)
    {
        float best = 999f;
        foreach (var ray in _rays)
        {
            float age = _age - ray.t; float r = HitRadius(age); if (r <= 0f) continue;
            Vector3 c = PuffCentre(ray, age);
            best = Mathf.Min(best, new Vector2(c.X - feet.X, c.Z - feet.Z).Length() - r);
        }
        return best;
    }
    /// <summary>A live puff's ball meeting Mario's damage cylinder, his feet under HitHeight over the floor.</summary>
    private bool ColdOn(Vector3 feet, out Vector3 at)
    {
        at = feet;
        if (feet.Y - FloorY >= HitHeight) return false;
        float mr = SmsParams.Mario.DamageRadius / U, mh = SmsParams.Mario.DamageHeight / U;
        foreach (var ray in _rays)
        {
            float age = _age - ray.t; float r = HitRadius(age); if (r <= 0f) continue;                                   // 187: the dense cold only
            Vector3 c = PuffCentre(ray, age);
            if (c.Y + r < feet.Y || c.Y - r > feet.Y + mh) continue;
            if (new Vector2(c.X - feet.X, c.Z - feet.Z).Length() <= r + mr) { at = c; return true; }
        }
        return false;
    }

    private void Aim(float k)
    {
        _yawNow = _yawCentre + _side * Mathf.DegToRad(SweepDeg) * (k - 0.5f);
        float d = Mathf.DegToRad(DownDeg);
        _dirNow = new Vector3(MathF.Sin(_yawNow) * MathF.Cos(d), -MathF.Sin(d), MathF.Cos(_yawNow) * MathF.Cos(d));
        _mouthNow = _mouth != null ? _mouth() : GlobalPosition;
        GlobalPosition = _mouthNow;
        var basis = Basis.LookingAt(_dirNow, Vector3.Up);
        foreach (var p in new[] { _puffs, _core, _glints }) if (p != null) { p.GlobalPosition = _mouthNow; p.GlobalBasis = basis; }
        if (_glow != null) _glow.GlobalPosition = _mouthNow + _dirNow * 0.6f;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (State == Phase.Idle || State == Phase.Done) return;
        _age += dt;
        if (State == Phase.Blow)
        {
            _t += dt; _seT += dt;
            Aim(SweepK);
            int lanes = Mathf.Max(1, HitLanes); Vector3 across = Vector3.Up.Cross(_dirNow); across = across.LengthSquared() > 1e-6f ? across.Normalized() : Vector3.Right;
            BlowFrames++;
            for (int i = 0; i < lanes; i++)                                                                                                   // 199: a ray from each lane across the mouth
            {
                float k = lanes > 1 ? (i / (float)(lanes - 1)) * 2f - 1f : 0f;
                _rays.Add((_age, _mouthNow + across * (k * MouthWidth * 0.5f), _dirNow));
            }
            if (_glowMat != null) _glowMat.AlbedoColor = new Color(GlintColor.R, GlintColor.G, GlintColor.B, 0.45f + 0.2f * MathF.Sin(_age * 22f));
            if (BreathSeEvery > 0f && _seT >= BreathSeEvery && _sound != null && !string.IsNullOrEmpty(BreathSe)) { _seT = 0f; _sound.Play(BreathSe); }
            if (_t >= SweepSeconds)
            {
                State = Phase.Tail; _tailT = 0f;
                if (_puffs != null) _puffs.Emitting = false;
                if (_core != null) _core.Emitting = false;
                if (_glints != null) _glints.Emitting = false;
                if (DebugPrint) GD.Print($"[SMS breath] {Name}: the sweep is over ({_rays.Count} rays) -- {(_hit ? "it FROZE Mario" : "a miss so far")}; the cold rolls on {PuffSeconds:F1} s");
                EmitSignal(SignalName.Swept, _hit);
            }
        }
        else if (State == Phase.Tail)
        {
            _tailT += dt;
            if (_glowMat != null) _glowMat.AlbedoColor = new Color(GlintColor.R, GlintColor.G, GlintColor.B, Mathf.Max(0f, 0.5f - _tailT * 2f));
            if (_glow != null && _tailT > 0.3f) _glow.Visible = false;
            if (_tailT >= PuffSeconds + 0.2f) { Finish(); return; }
        }
        HitCheck();
        if (_hitBalls != null) HitBallsTick();                                                                                               // 192
    }

    /// <summary>192: the cold that counts, drawn -- a translucent red ball per live puff, at its hit radius, where the hit maths puts it.</summary>
    private void MakeHitBalls()
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 }, InstanceCount = 320, VisibleInstanceCount = 0 };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoColor = new Color(1f, 0.15f, 0.15f, 0.3f), RenderPriority = 2 };
        _hitBalls = new MultiMeshInstance3D { Name = "HitBalls", TopLevel = true, Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_hitBalls);
        _hitBalls.GlobalTransform = Transform3D.Identity;
    }
    private void HitBallsTick()
    {
        var mm = _hitBalls.Multimesh; int n = 0;
        foreach (var ray in _rays)
        {
            float age = _age - ray.t; float r = HitRadius(age); if (r <= 0f) continue;
            if (n >= mm.InstanceCount) break;
            mm.SetInstanceTransform(n++, new Transform3D(Basis.Identity.Scaled(Vector3.One * r), PuffCentre(ray, age)));
        }
        mm.VisibleInstanceCount = n; HitBallsDrawn = n;
    }

    private void HitCheck()
    {
        if (_mario == null || !IsInstanceValid(_mario)) return;
        if (OneFreezePerAttack && Hits > 0) return;
        if (_mario.SmsFrozen) return;
        Vector3 feet = _mario.GlobalPosition;
        if (!ColdOn(feet, out Vector3 at)) return;
        if (!_mario.SmsFreezeFor(FreezeSeconds)) return;                    // refused: invincible, down, a demo...
        _hit = true; Hits++; FreezeCount++; FreezePoint = at; FreezeAt = _age;
        var ice = SmsIceBlock.Freeze(_mario, GetParent() ?? this, DebugPrint);
        if (ice != null) ice.HitFrom(_dirNow);                               // 178: the IMPACT -- the cold splashing on him along the stream, the flash, the shake
        if (HitShake > 0f) SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, HitShake);
        if (DebugPrint) GD.Print($"[SMS breath] {Name}: the cold GETS Mario at {feet} ({new Vector2(at.X - _mouthNow.X, at.Z - _mouthNow.Z).Length():F1} m from the mouth, {_age:F2} s in, sweep {SweepK:F2}) -- FROZEN {FreezeSeconds:F1} s, hp {_mario.SmsHP}");
    }

    private GpuParticles3D MakePuffs()
    {
        if (Puffs <= 0) return null;
        var tex = Tex("kemuball3_raw");
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = MouthExtents,                                   // 199: born across the mouth
            Direction = new Vector3(0f, 0f, -1f), Spread = Mathf.Max(0.5f, SpreadDeg), InitialVelocityMin = BreathSpeed * 0.85f, InitialVelocityMax = BreathSpeed,
            Gravity = new Vector3(0f, -Sink, 0f), ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = Ramp(PuffSizeStart, PuffSizeEnd),
            AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -40f, AngularVelocityMax = 40f,
            Color = new Color(BreathColor.R, BreathColor.G, BreathColor.B, 1f),
            ColorRamp = Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, PuffAlpha), new Color(0.92f, 0.96f, 1f, PuffAlpha * 0.75f), new Color(0.9f, 0.95f, 1f, 0f)),
            CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Rigid, CollisionFriction = Mathf.Clamp(FloorDrag, 0f, 1f), CollisionBounce = 0f, CollisionUseScale = true   // 192: FloorDrag (0.15 before: the mist stalled 10 m out)
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = "ColdPuffs", TopLevel = true, CollisionBaseSize = HitRadiusK, Amount = Puffs, Lifetime = PuffSeconds, Emitting = false, Visible = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 40f, 120f)) };
        AddChild(p);
        return p;
    }
    /// <summary>172: the dense core of the stream -- smaller, whiter, nearly solid puffs hugging the ray, sinking and rolling with the rest.</summary>
    private GpuParticles3D MakeCore()
    {
        if (CorePuffs <= 0) return null;
        var tex = Tex("kemuball3_raw");
        Color c = BreathColor.Lerp(new Color(1f, 1f, 1f), 0.2f);                   // a shade whiter than the stream, still the cold's blue
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = MouthExtents * new Vector3(0.9f, 0.8f, 1f),       // 199: the dense core across the mouth, a shade inside the mist
            Direction = new Vector3(0f, 0f, -1f), Spread = Mathf.Max(0.5f, CoreSpreadDeg), InitialVelocityMin = BreathSpeed * 0.97f, InitialVelocityMax = BreathSpeed * 1.03f,   // 192: the core rides WITH the hit maths (0.92-1.02 and 1.1 x the sink before: the hit led the white front)
            Gravity = new Vector3(0f, -Sink, 0f), ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = Ramp(CoreSizeStart, CoreSizeEnd),
            AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -60f, AngularVelocityMax = 60f,
            Color = new Color(c.R, c.G, c.B, 1f),
            ColorRamp = Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, CoreAlpha), new Color(0.95f, 0.98f, 1f, CoreAlpha * 0.7f), new Color(0.9f, 0.95f, 1f, 0f)),
            CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Rigid, CollisionFriction = Mathf.Clamp(FloorDrag, 0f, 1f), CollisionBounce = 0f, CollisionUseScale = true   // 192: FloorDrag
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, RenderPriority = 1 };
        var p = new GpuParticles3D { Name = "ColdCore", TopLevel = true, CollisionBaseSize = HitRadiusK, Amount = CorePuffs, Lifetime = PuffSeconds * 0.9, Emitting = false, Visible = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 40f, 120f)) };
        AddChild(p);
        return p;
    }
    private GpuParticles3D MakeGlints()
    {
        if (Glints <= 0) return null;
        var tex = Tex("star4s_raw");
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = MouthExtents,                                   // 199: the glints across the mouth too
            Direction = new Vector3(0f, 0f, -1f), Spread = Mathf.Max(0.5f, SpreadDeg) * 1.4f, InitialVelocityMin = BreathSpeed * 0.9f, InitialVelocityMax = BreathSpeed * 1.1f,
            Gravity = new Vector3(0f, -Sink * 0.8f, 0f), ScaleMin = 0.7f, ScaleMax = 1.2f, AngularVelocityMin = -300f, AngularVelocityMax = 300f,
            Color = new Color(GlintColor.R, GlintColor.G, GlintColor.B, 1f),
            ColorRamp = Fade(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0f)),
            CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Rigid, CollisionFriction = 0.3f, CollisionBounce = 0.2f
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = "IceGlints", TopLevel = true, CollisionBaseSize = 0.1f, Amount = Glints, Lifetime = PuffSeconds * 0.8, Emitting = false, Visible = false, LocalCoords = false, FixedFps = 60, Interpolate = true, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One * GlintSize, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-60f, -10f, -60f), new Vector3(120f, 40f, 120f)) };
        AddChild(p);
        return p;
    }
    private void MakeGlow()
    {
        if (MouthGlowSize <= 0f) return;
        _glowMat = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = true,
            AlbedoTexture = Tex("radgrad_raw"), AlbedoColor = new Color(GlintColor.R, GlintColor.G, GlintColor.B, 0.5f), RenderPriority = 3
        };
        _glow = new MeshInstance3D { Name = "ColdLight", TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = new Vector2(Mathf.Max(MouthGlowSize, MouthWidth * 0.9f), MouthGlowSize) }, MaterialOverride = _glowMat };   // 199: the light stretched across the mouth
        AddChild(_glow);
    }
    /// <summary>A slab under the floor for the puffs to roll along (GPU particles collide with these, not with the world).</summary>
    private void MakeSlab()
    {
        _slab = new GpuParticlesCollisionBox3D { Name = "FloorSlab", TopLevel = true, Size = new Vector3(160f, 2f, 160f) };
        AddChild(_slab);
        _slab.GlobalPosition = new Vector3(_mouthNow.X, FloorY - 1f, _mouthNow.Z);
    }
    private static Texture2D Tex(string name) { string p = $"res://assets/Sms/Fx/tex/{name}.png"; return ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null; }
    private static CurveTexture Ramp(float a, float b)
    {
        var c = new Curve { MinValue = 0f, MaxValue = Mathf.Max(1f, Mathf.Max(a, b) * 1.1f) };
        c.AddPoint(new Vector2(0f, a)); c.AddPoint(new Vector2(1f, b));
        return new CurveTexture { Curve = c };
    }
    private static GradientTexture1D Fade(Color c0, Color c1, Color c2, Color c3)
    {
        var g = new Gradient { Offsets = new float[] { 0f, 0.1f, 0.65f, 1f }, Colors = new Color[] { c0, c1, c2, c3 } };
        return new GradientTexture1D { Gradient = g };
    }

    private void Finish()
    {
        State = Phase.Done; All.Remove(this);
        if (DebugPrint) GD.Print($"[SMS breath] {Name}: finished -- {Hits} freeze{(Hits == 1 ? "" : "s")}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through the attack: the breath cuts off -- no more cold; the puffs already out fade on their own, then the node goes.</summary>
    /// <summary>203: gone at once with the field (the split's cut): the mist, the core, the glints, the glow and the balls hidden with the node, which is freed; true = it went.</summary>
    public bool VanishNow()
    {
        if (IsQueuedForDeletion()) return false;
        State = Phase.Done; Visible = false;
        if (_puffs != null) _puffs.Emitting = false;
        if (_core != null) _core.Emitting = false;
        if (_glints != null) _glints.Emitting = false;
        QueueFree();
        return true;
    }
    public void Cancel()
    {
        if (State == Phase.Done) return;
        State = Phase.Done;                                                                                     // 203: it stays in All till it is freed (the field clear reaches a cancelled breath's hanging mist through it)
        if (_puffs != null) _puffs.Emitting = false;
        if (_core != null) _core.Emitting = false;
        if (_glints != null) _glints.Emitting = false;
        if (_glow != null) _glow.Visible = false;
        if (_hitBalls != null) _hitBalls.Visible = false;
        if (DebugPrint) GD.Print($"[SMS breath] {Name}: cut off at sweep {SweepK:F2}");
        EmitSignal(SignalName.Finished);
        GetTree().CreateTimer(PuffSeconds + 0.2f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
}
