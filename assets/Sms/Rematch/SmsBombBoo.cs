using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 158 / 159 / 160: a BOMB BOO (KINGBOO_REMATCH_gdoc.txt, HOW MARIO DEALS DAMAGE) -- Galaxy's black Boo, Mario's only weapon in the rematch.
/// It comes out of a black foggy spot (SmsBombBooSpawner), rises and fades in, then FLOATS AS SUNSHINE'S BOOS DO (160, TTelesa's numbers from
/// /enemy/telesa.prm as SmsTelesa carries them): it drifts between random points within WanderRadiusUnits of where it rose (walkToCurPathNode:
/// turning TurnSpeed degrees a tick toward the goal, MarchSpeed units a tick along its face; reached, it waits WaitTicks, then the next), its
/// height the base's FlyHeight (50..100 u, rising 1 u a tick to it) plus the bob (FlyAmplitude 50..75 u over FlyFrequency 200..400 ticks), its
/// nose tilting 10 - 35 cos(phase) degrees with the bob; and it HURTS Mario on touch unless he is above it (TTelesa::attackToMario; TouchHurts,
/// the common enemy damage). SeeksMario makes it drift toward him instead (158's). Mario's SPIN (X without the FLUDD) within his BombGrabRadius
/// GRABS it BY THE TONGUE (159, as Galaxy has it): he holds the tongue up in one hand (SmsLassoArm) and the Boo SWINGS ROUND HIM on it --
/// OrbitRadius out, OrbitOverHand over his hand, OrbitDegPerSec round, leaning outward (OrbitLeanDeg), facing in, the tongue stretched from its
/// mouth to his hand; his spin jump makes it swing faster (SpinJumpBoost); a DIVE drops it (released, it floats again after DropGraceSeconds). It
/// is NOT thrown: swung into King Boo's body (SmsKingBooRematch.InHitCylinder) it BURSTS on him and he TAKES A HIT -- Mario walks it into him.
/// HoldSeconds (0 = never) and it bursts in his hand for nothing. Expires on its own after ExpireSeconds floating (0 = never); King Boo's hit
/// clears every Boo on the field (the spawner). Model: models/Sms/Rematch/bomb_boo.glb + bomb_boo_tongue.glb (Jaysonn's .dae pair through
/// Blender, their vertex colours kept) at ModelScale, on BombBoo.gdshader -- the maroon body texture with its yellow eyes x the dark vertex
/// colours x Lift, Opacity 1 (159: 158's was purple and see-through), the eyes glowing a little (FaceGlow), a faint violet rim (Glow).
/// Signals: Grabbed, Dropped, Burst(hitKingBoo), Touched.
/// </summary>
public partial class SmsBombBoo : Node3D
{
    public enum Phase { Rising, Drifting, Carried, Gone }
    /// <summary>The tongue glb's length along its +Z (its root at the origin), metres at scale 1 (76.6 cm in the .dae).</summary>
    public const float TongueModelLength = 0.766f;
    private const float U = SmsUnits.UnitsPerMetre;

    [ExportGroup("Look")]
    /// <summary>The glb at this scale (Galaxy's centimetres read as metres make the body 2.5 m wide: 0.8 = 2 m, 1.7 m tall against Mario's 2.15).</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float ModelScale = 0.8f;
    /// <summary>1 = solid (159). Galaxy's own is about 0.6 (the material's alpha 150 / 255) -- a knob.</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float Opacity = 1f;
    /// <summary>The body's brightness: texture x (vertex colour + Ambient) x Lift (2 = the game's stage doubling; the body stays a near-black red, the eyes bright).</summary>
    [Export(PropertyHint.Range, "0.5,4,0.05")] public float Lift = 2f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Ambient = 0f;
    /// <summary>The eyes' own light (the bright texels) and the faint rim.</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float FaceGlow = 0.5f;
    [Export] public Color GlowColor = new Color(0.35f, 0.1f, 0.55f);
    [Export(PropertyHint.Range, "0,3,0.05")] public float Glow = 0.25f;
    /// <summary>The tongue's root in the MOUTH (model metres, before ModelScale; 161, measured on the model: the body is 2.5 m wide, 1.8 tall and 2.1 deep about
    /// its origin, the face on its +Z side -- the mouth's centre at (0, -0.6, 0.86), the eyes at (0, 0.15, 0.93)), how far it droops when it just hangs
    /// (degrees), and whether the face is on the +Z side (off: -Z).</summary>
    [Export] public Vector3 TongueOffset = new Vector3(0f, -0.6f, 0.85f);
    [Export(PropertyHint.Range, "-60,80,1")] public float TongueRestDroopDeg = 35f;
    [Export] public bool FacesPlusZ = true;
    [ExportGroup("The float (Sunshine's Boo, telesa.prm)")]
    /// <summary>Its base point stands this high over the floor where it rose (the game's Boo keeps the height it was placed at); the fly height and the bob
    /// ride on it. 1.2: the body (0.85 m to its bottom at scale 0.8) clears the floor at the bob's lowest.</summary>
    [Export] public float BaseLiftOverFloor = 1.2f;
    /// <summary>FlyHeight 50..100 u over the base (picked at random, risen to at 1 u a tick), FlyAmplitude 50..75 u of bob over FlyFrequency 200..400 ticks (a cycle).</summary>
    [Export] public float FlyHeightMinUnits = 50f, FlyHeightMaxUnits = 100f, FlyAmplitudeMinUnits = 50f, FlyAmplitudeMaxUnits = 75f, FlyFrequencyMin = 200f, FlyFrequencyMax = 400f;
    /// <summary>The nose: 10 - FlyAngMax x cos(phase) degrees (up as it rises, down as it sinks), eased as SmsTelesa eases it.</summary>
    [Export] public float FlyAngMax = 35f, FlyAngBase = 10f;
    /// <summary>The drift: MarchSpeed 1..1.7 u a tick and TurnSpeed 1..1.5 degrees a tick (each picked at random per Boo), the goals within WanderRadiusUnits
    /// of the base (0.3..1 of it), reached within ReachedUnits, WaitTicks of hovering between them.</summary>
    [Export] public float MarchSpeedLow = 1f, MarchSpeedHigh = 1.7f, TurnSpeedLow = 1f, TurnSpeedHigh = 1.5f;
    [Export] public float WanderRadiusUnits = 800f, ReachedUnits = 100f;
    [Export] public int WaitTicks = 100;
    /// <summary>On: it drifts toward Mario instead (158's), stopping DriftStopDistance short.</summary>
    [Export] public bool SeeksMario = false;
    [Export] public float DriftStopDistance = 1.6f;
    /// <summary>TTelesa::attackToMario: the touch hurts him (the common enemy damage, his invincibility after) unless he is ABOVE it -- his feet over the
    /// body's middle less 50 u. Its attack cylinder: TouchRadius across, TouchHeight tall about its centre. Off: a Boo he can walk through.</summary>
    [Export] public bool TouchHurts = true;
    [Export] public float TouchRadius = 0.9f, TouchHeight = 1.4f;
    /// <summary>Gone on its own after this long floating (0 = never).</summary>
    [Export] public float ExpireSeconds = 0f;
    /// <summary>Out of the fog: the rise's length (it scales up, fades in and lifts to its height over it).</summary>
    [Export] public float RiseSeconds = 0.7f;
    [ExportGroup("The swing (carried by the tongue)")]
    /// <summary>Round Mario: this far out (161: 3.6 m, "the tongue radius longer"), this far OVER his raised hand (his arms are short next to his head: the raised
    /// wrist comes to his ear, 1.3 m up on a 2.15 m body -- 0.9 puts the Boo just over his head, the tongue rising to it), this fast (degrees a second; 480 = a
    /// turn in 0.75 s), leaning this far outward.</summary>
    [Export] public float OrbitRadius = 3.6f, OrbitOverHand = 0.9f, OrbitDegPerSec = 480f, OrbitLeanDeg = 25f;
    /// <summary>From the grab to the full swing (the radius and the height ease in).</summary>
    [Export] public float OrbitSettleSeconds = 0.35f;
    [Export] public bool OrbitClockwise = false;
    /// <summary>His spin jump: the swing this much faster for this long.</summary>
    [Export] public float SpinJumpBoost = 1.7f, SpinJumpBoostSeconds = 2.5f;
    /// <summary>Dropped: no grab again for this long (he has to spin for it again anyway).</summary>
    [Export] public float DropGraceSeconds = 0.6f;
    /// <summary>Carried this long, it bursts in his hand for nothing (Galaxy's held Bomb Boo blows up on its own; 0 = never). It flashes faster over its last 2 s.</summary>
    [Export] public float HoldSeconds = 0f;
    /// <summary>The Boo's own reach when it swings into King Boo (model metres, x ModelScale: the body is 1.27 m to its side, 1.05 m to its front).</summary>
    [Export] public float OwnRadius = 1f;
    [ExportGroup("Sound (empty = silent)")]
    [Export] public string AppearSe = "MSD_SE_EN_TELESA_APPEAR", GrabSe = "MSD_SE_EN_TELESA_FIX", BurstSe = "MSD_SE_EN_TELESA_CRUSH", GoneSe = "MSD_SE_EN_TELESA_DISAPPEAR";
    [ExportGroup("")]
    [Export] public PackedScene BurstFxScene;
    [Export] public Color BurstColor = new Color(0.55f, 0.2f, 0.85f);
    [Export] public int Seed = 0;
    [Export] public bool DebugPrint;

    [Signal] public delegate void GrabbedEventHandler();
    [Signal] public delegate void DroppedEventHandler();
    [Signal] public delegate void BurstEventHandler(bool hitKingBoo);
    [Signal] public delegate void TouchedEventHandler();

    public static readonly List<SmsBombBoo> All = new();
    public Phase State { get; private set; } = Phase.Rising;
    public bool IsFree => State == Phase.Drifting;
    public bool IsCarried => State == Phase.Carried;
    public float OrbitSpeedNow => OrbitDegPerSec * (_boostLeft > 0f ? SpinJumpBoost : 1f);
    public float Age { get; private set; }
    /// <summary>For the drives: the float (the base point, the fly height and the bob in metres, the nose's pitch in degrees, the goal, the goals reached, the
    /// march and turn speeds picked), the swing (its radius and angle now), the tongue (its length and tip, the mouth), the materials, the touches.</summary>
    public Vector3 BasePoint => _base;
    public float FlyHeightNow => _flyCur / U;
    public float BobNow => _bob / U;
    public float PitchDeg => _rotX;
    public Vector3 GoalPoint => _goal;
    public int GoalsReached { get; private set; }
    public float MarchSpeed => _march;
    public float TurnSpeed => _turn;
    public bool Waiting => _wait > 0;
    public int Touches { get; private set; }
    public float OrbitRadiusNow { get; private set; }
    public float OrbitAngle => _orbitAngle;
    public float TongueLength { get; private set; }
    public Vector3 TongueTip { get; private set; }
    public Vector3 MouthPoint => GlobalTransform * (TongueOffset * ModelScale);
    public int MaterialCount => _mats.Count;
    public Node3D TongueNode => _tongue;

    private Node3D _model, _tongue; private readonly List<ShaderMaterial> _mats = new();
    private Mario _mario; private SmsKingBooRematch _boss; private SmsActorSound _sound; private Random _rng;
    private float _t, _yaw, _boostLeft, _grace, _floorY = float.NaN;
    private Vector3 _base, _goal; private float _flyCur, _flyTarget, _bob, _bobAmp, _bobPhase, _bobFreq = 200f, _rotX, _march = 1f, _turn = 1f; private int _wait;
    private float _orbitAngle, _settle, _r0, _y0, _held;
    private Rid _marioRid; private bool _marioRidKnown;

    public override void _Ready()
    {
        All.Add(this);
        _rng = new Random(Seed != 0 ? Seed : (int)(GetInstanceId() & 0x7fffffff));
        _model = GetNodeOrNull<Node3D>("Model"); _tongue = GetNodeOrNull<Node3D>("Tongue");
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/BombBoo.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/BombBoo.gdshader") : null;
        foreach (var root in new[] { _model, _tongue })
        {
            if (root == null) continue;
            if (shader == null) continue;
            foreach (Node n in root.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    var sm = new ShaderMaterial { Shader = shader };
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm && bm.AlbedoTexture != null) sm.SetShaderParameter("base_tex", bm.AlbedoTexture);
                    sm.SetShaderParameter("opacity", 0f); sm.SetShaderParameter("lift", Lift); sm.SetShaderParameter("ambient", Ambient);
                    sm.SetShaderParameter("face_glow", root == _tongue ? 0f : FaceGlow);
                    sm.SetShaderParameter("glow_color", new Vector3(GlowColor.R, GlowColor.G, GlowColor.B));
                    sm.SetShaderParameter("glow", Glow);
                    mi.SetSurfaceOverrideMaterial(s, sm);
                    _mats.Add(sm);
                }
            }
        }
        BurstFxScene ??= ResourceLoader.Exists("res://assets/Sms/Fx/EnemyDisap.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Fx/EnemyDisap.tscn") : null;
        _sound = SmsActorSound.Attach(this);
        _yaw = Rotation.Y;
        // TTelesa::init's setFlyParam + TWalkerEnemy's speeds: at random in their ranges
        _flyTarget = Lerp(FlyHeightMinUnits, FlyHeightMaxUnits, Rand()); _flyCur = 0f;
        _bobAmp = Lerp(FlyAmplitudeMinUnits, FlyAmplitudeMaxUnits, Rand()); _bobPhase = 0f;
        _bobFreq = MathF.Max(1f, Lerp(FlyFrequencyMin, FlyFrequencyMax, Rand()));
        _march = Lerp(MarchSpeedLow, MarchSpeedHigh, Rand()); _turn = Lerp(TurnSpeedLow, TurnSpeedHigh, Rand());
        _rotX = FlyAngBase - FlyAngMax;
        SetScaleK(0.01f);
        if (!string.IsNullOrEmpty(AppearSe)) _sound?.Play(AppearSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: {_mats.Count} materials, at {GlobalPosition}; fly {_flyTarget:F0} u, bob {_bobAmp:F0} u / {_bobFreq:F0} ticks, march {_march:F2} u/t, turn {_turn:F2} deg/t");
    }
    public override void _ExitTree() { All.Remove(this); }
    private float Rand() => (float)_rng.NextDouble();
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>The body and the hanging tongue at the model scale x k (the rise), the fade with it.</summary>
    private void SetScaleK(float k)
    {
        if (_model != null) _model.Scale = Vector3.One * ModelScale * k;
        TongueRest(k);
        foreach (var m in _mats) m.SetShaderParameter("opacity", Opacity * Mathf.Clamp(k, 0f, 1f));
    }
    /// <summary>The tongue just hanging out of the mouth: its root at TongueOffset, drooping TongueRestDroopDeg.</summary>
    private void TongueRest(float k = 1f)
    {
        if (_tongue == null) return;
        _tongue.Transform = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(TongueRestDroopDeg)).Scaled(Vector3.One * ModelScale * k), TongueOffset * ModelScale * k);
        TongueLength = TongueModelLength * ModelScale * k; TongueTip = _tongue.GlobalTransform * new Vector3(0f, 0f, TongueModelLength);
    }
    /// <summary>The tongue stretched from the mouth to <paramref name="hand"/>: its +Z along the line, scaled to the length, its width kept.</summary>
    private void TongueTo(Vector3 hand)
    {
        if (_tongue == null) return;
        Vector3 mouth = MouthPoint;
        Vector3 d = hand - mouth; float len = d.Length();
        if (len < 0.05f) { TongueRest(); return; }
        Vector3 z = d / len;
        Vector3 x = Vector3.Up.Cross(z); if (x.LengthSquared() < 1e-6f) x = Vector3.Right.Cross(z); x = x.Normalized();
        Vector3 y = z.Cross(x);
        _tongue.GlobalTransform = new Transform3D(new Basis(x * ModelScale, y * ModelScale, z * (len / TongueModelLength)), mouth);
        TongueLength = len; TongueTip = hand;
    }
    private void SetFlash(float f) { foreach (var m in _mats) m.SetShaderParameter("flash", f); }

    private Mario MarioNow()
    {
        if (_mario == null || !IsInstanceValid(_mario)) { _mario = GetTree().GetFirstNodeInGroup("player") as Mario; _marioRidKnown = false; }
        if (_mario != null && !_marioRidKnown) { _marioRid = _mario.GetRid(); _marioRidKnown = true; }
        return _mario;
    }
    private SmsKingBooRematch BossNow()
    {
        if (_boss == null || !IsInstanceValid(_boss)) _boss = GetTree().GetFirstNodeInGroup("sms_rematch_boss") as SmsKingBooRematch;
        return _boss;
    }

    /// <summary>The floor under (x, z): a ray from 3 m over the point, Mario's body left out; NaN = none.</summary>
    private float FloorUnder(Vector3 p)
    {
        var space = GetWorld3D()?.DirectSpaceState; if (space == null) return float.NaN;
        var q = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 3f, p + Vector3.Down * 30f);
        q.CollideWithAreas = false; q.CollideWithBodies = true;
        if (_marioRidKnown) q.Exclude = new Godot.Collections.Array<Rid> { _marioRid };
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].AsVector3().Y : float.NaN;
    }

    /// <summary>The face's yaw (the model's +Z, or -Z) and the nose's pitch (positive = down) as a basis.</summary>
    private Basis FaceBasis(float yaw, float pitchDeg)
    {
        Basis b = new Basis(Vector3.Up, yaw + (FacesPlusZ ? 0f : MathF.PI));
        return b * new Basis(Vector3.Right, Mathf.DegToRad(FacesPlusZ ? pitchDeg : -pitchDeg));
    }
    /// <summary>goToRandomNextGraphNode with no graph: a random point within WanderRadiusUnits (0.3..1 of it) of the base.</summary>
    private void NextGoal()
    {
        float a = Rand() * MathF.Tau, r = WanderRadiusUnits / U * (0.3f + 0.7f * Rand());
        _goal = new Vector3(_base.X + MathF.Cos(a) * r, _base.Y, _base.Z + MathF.Sin(a) * r);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || State == Phase.Gone) return;
        float dt = (float)delta; Age += dt; _t += dt;
        if (_boostLeft > 0f) _boostLeft -= dt;
        if (_grace > 0f) _grace -= dt;
        var mario = MarioNow();
        switch (State)
        {
            case Phase.Rising:
            {
                float k = RiseSeconds > 0f ? Mathf.Clamp(_t / RiseSeconds, 0f, 1f) : 1f;
                SetScaleK(Mathf.Max(0.01f, Mathf.SmoothStep(0f, 1f, k)));
                if (float.IsNaN(_floorY)) { _floorY = FloorUnder(GlobalPosition); _base = new Vector3(GlobalPosition.X, (float.IsNaN(_floorY) ? GlobalPosition.Y : _floorY) + BaseLiftOverFloor, GlobalPosition.Z); _spawnY = GlobalPosition.Y; }
                GlobalPosition = GlobalPosition with { Y = Mathf.Lerp(_spawnY, _base.Y, Mathf.SmoothStep(0f, 1f, k)) };
                FaceMario(mario, dt);
                if (k >= 1f) { State = Phase.Drifting; _t = 0f; SetScaleK(1f); _wait = WaitTicks / 2; NextGoal(); }
                break;
            }
            case Phase.Drifting:
            {
                if (ExpireSeconds > 0f && _t >= ExpireSeconds) { Vanish(); return; }
                Vector3 p = GlobalPosition;
                Vector3 basePos = new Vector3(p.X, _base.Y, p.Z);
                if (SeeksMario)
                {
                    if (mario != null)
                    {
                        Vector3 d = mario.GlobalPosition - basePos; d.Y = 0f;
                        float dist = d.Length();
                        if (dist > DriftStopDistance && dist > 1e-3f) { basePos += d / dist * MathF.Min(_march * 60f / U * dt, dist - DriftStopDistance); _yaw = Mathf.LerpAngle(_yaw, MathF.Atan2(d.X, d.Z), Mathf.Clamp(4f * dt, 0f, 1f)); }
                    }
                }
                else
                {
                    // TNerveWalkerGraphWander for a flyer: wait at a goal, then walkToCurPathNode -- turn toward the next by TurnSpeed a tick, move MarchSpeed a tick along the face
                    if (_wait > 0) _wait--;
                    else
                    {
                        Vector3 to = _goal - basePos; to.Y = 0f;
                        if (to.Length() < ReachedUnits / U) { GoalsReached++; _wait = WaitTicks; NextGoal(); }
                        else
                        {
                            float want = MathF.Atan2(to.X, to.Z);
                            float diff = Mathf.Wrap(want - _yaw, -MathF.PI, MathF.PI);
                            float step = Mathf.DegToRad(_turn) * 60f * dt;
                            _yaw += Mathf.Clamp(diff, -step, step);
                            basePos += new Vector3(MathF.Sin(_yaw), 0f, MathF.Cos(_yaw)) * (_march * 60f / U * dt);
                        }
                    }
                }
                // setBehavior: the fly height +1 u a tick to its target, the bob phase +1 a tick round its frequency, the tilt with the bob (eased as SmsTelesa eases it)
                float ticks = 60f * dt;
                _flyCur = Mathf.MoveToward(_flyCur, _flyTarget, ticks);
                _bobPhase += ticks; if (_bobPhase > _bobFreq) _bobPhase -= _bobFreq;
                float ph = Mathf.DegToRad(_bobPhase * 360f / _bobFreq);
                _bob = _bobAmp * MathF.Sin(ph);
                float nx = FlyAngBase - FlyAngMax * MathF.Cos(ph);
                _rotX = MathF.Abs(nx - _rotX) <= 5f ? nx : Lerp(_rotX, nx, 0.2f);
                GlobalTransform = new Transform3D(FaceBasis(_yaw, _rotX), basePos + Vector3.Up * ((_flyCur + _bob) / U));
                TongueRest();
                if (TouchHurts && mario != null) TouchCheck(mario);
                break;
            }
            case Phase.Carried:
            {
                if (mario == null || !IsInstanceValid(mario)) { Drop(); return; }
                Vector3 hand = mario.SmsLassoHand;
                _settle = Mathf.Min(1f, _settle + dt / Mathf.Max(0.01f, OrbitSettleSeconds));
                float k = Mathf.SmoothStep(0f, 1f, _settle);
                _orbitAngle += Mathf.DegToRad(OrbitSpeedNow) * dt * (OrbitClockwise ? -1f : 1f);
                float r = Mathf.Lerp(_r0, OrbitRadius, k);
                float y = Mathf.Lerp(_y0, hand.Y + OrbitOverHand, k);
                Vector3 c = mario.GlobalPosition;
                Vector3 p = new Vector3(c.X + MathF.Cos(_orbitAngle) * r, y, c.Z + MathF.Sin(_orbitAngle) * r);
                OrbitRadiusNow = r;
                // facing in at Mario (the tongue in its mouth leads to his hand), leaning outward with the swing
                Vector3 outward = new Vector3(p.X - c.X, 0f, p.Z - c.Z);
                Basis b = GlobalTransform.Basis.Orthonormalized();
                if (outward.LengthSquared() > 1e-6f)
                {
                    outward = outward.Normalized();
                    _yaw = MathF.Atan2(-outward.X, -outward.Z);
                    b = new Basis(Vector3.Up, _yaw + (FacesPlusZ ? 0f : MathF.PI));
                    Vector3 tangent = Vector3.Up.Cross(outward);
                    if (tangent.LengthSquared() > 1e-6f) b = b.Rotated(tangent.Normalized(), Mathf.DegToRad(OrbitLeanDeg) * k);
                }
                GlobalTransform = new Transform3D(b, p);
                TongueTo(hand);
                // 160: no pulse on the plain swing (a tenth of white on a near-black body read as a GREY Boo, Jaysonn's screenshot); the boost and the hold's warning show
                float pulse = _boostLeft > 0f ? 0.06f + 0.04f * MathF.Sin(_t * 18f) : 0f;
                if (HoldSeconds > 0f)
                {
                    _held += dt;
                    float left = HoldSeconds - _held;
                    if (left <= 0f) { Explode(false); return; }
                    if (left < 2f) pulse = 0.12f + 0.12f * MathF.Sin(_t * (12f + (2f - left) * 14f));
                }
                SetFlash(pulse);
                var boss = BossNow();
                if (boss != null && boss.CanBeHit && boss.InHitCylinder(p, OwnRadius * ModelScale)) { Explode(true); return; }
                break;
            }
        }
    }
    private float _spawnY;

    /// <summary>TTelesa::attackToMario on its attack cylinder against his damage cylinder (42 u across, 160 tall): the hit unless he is above it -- his feet over the
    /// body's middle less 50 u. His own invincibility (the common profile's 300 ticks) spaces the hits.</summary>
    private void TouchCheck(Mario mario)
    {
        Vector3 m = mario.GlobalPosition, g = GlobalPosition;
        float marioR = SmsUnits.UnitsToMetres(42f), marioH = SmsUnits.UnitsToMetres(SmsUnits.MarioHeightUnits);
        float dxz = new Vector2(m.X - g.X, m.Z - g.Z).Length();
        if (dxz > TouchRadius + marioR) return;
        float lo = g.Y - TouchHeight * 0.5f, hi = g.Y + TouchHeight * 0.5f;
        if (m.Y + marioH < lo || m.Y > hi) return;
        if (m.Y >= g.Y - 50f / U) return;                        // above it: no hurt (the game's y + attackHeight - 50 against the body's foot; our origin is the body's middle)
        if (mario.stateOfMario == Mario.MarioState.hurt || mario.stateOfMario == Mario.MarioState.dead) return;
        int hp = mario.SmsHP;
        mario.TakeDamage(g, Mario.SmsDamageProfile.EnemyCommon);
        if (mario.SmsHP < hp || mario.stateOfMario == Mario.MarioState.hurt)
        {
            Touches++;
            if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: touched Mario -- hp {hp} -> {mario.SmsHP}");
            EmitSignal(SignalName.Touched);
        }
    }

    private void FaceMario(Mario mario, float dt)
    {
        if (mario == null) return;
        Vector3 d = mario.GlobalPosition - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() < 1e-4f) return;
        float want = MathF.Atan2(d.X, d.Z);
        _yaw = Mathf.LerpAngle(_yaw, want, Mathf.Clamp(4f * dt, 0f, 1f));
        GlobalTransform = new Transform3D(FaceBasis(_yaw, _rotX), GlobalPosition);
    }

    /// <summary>Mario's spin reached it: he has the tongue -- it swings round him from where it is (false when not free, or within the drop's grace).</summary>
    public bool Grab(Mario mario)
    {
        if (State != Phase.Drifting || _grace > 0f || mario == null) return false;
        _mario = mario; _marioRidKnown = false;
        State = Phase.Carried; _t = 0f; _boostLeft = 0f; _held = 0f; _settle = 0f;
        Vector3 rel = GlobalPosition - mario.GlobalPosition;
        _orbitAngle = MathF.Atan2(rel.Z, rel.X);
        _r0 = MathF.Max(0.3f, new Vector2(rel.X, rel.Z).Length()); _y0 = GlobalPosition.Y;
        OrbitRadiusNow = _r0;
        if (!string.IsNullOrEmpty(GrabSe)) _sound?.Play(GrabSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: grabbed by the tongue at {_r0:F2} m -- the swing");
        EmitSignal(SignalName.Grabbed);
        return true;
    }
    /// <summary>His spin jump while he swings it: faster for a while.</summary>
    public void SpinBoost() { if (State == Phase.Carried) { _boostLeft = SpinJumpBoostSeconds; if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: the swing x{SpinJumpBoost} for {SpinJumpBoostSeconds} s"); } }
    /// <summary>Let go (a dive, a hit, a death): it floats again from where the swing left it -- a new base under it, its fly height risen to again; no grab for DropGraceSeconds.</summary>
    public void Drop()
    {
        if (State != Phase.Carried) return;
        State = Phase.Drifting; _t = 0f; _grace = DropGraceSeconds; _boostLeft = 0f; SetFlash(0f);
        _floorY = FloorUnder(GlobalPosition);
        _base = new Vector3(GlobalPosition.X, (float.IsNaN(_floorY) ? _base.Y : _floorY + BaseLiftOverFloor), GlobalPosition.Z);
        _flyCur = Mathf.Clamp((GlobalPosition.Y - _base.Y) * U - _bob, 0f, _flyTarget);
        GlobalTransform = new Transform3D(FaceBasis(_yaw, _rotX), GlobalPosition);
        TongueRest(); _wait = WaitTicks; NextGoal();
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: let go");
        EmitSignal(SignalName.Dropped);
    }
    /// <summary>The burst: the puff (BurstFxScene recoloured), the sound, King Boo's hit when it was swung into him, Mario's hand emptied, and the Boo gone.</summary>
    public void Explode(bool hitKingBoo)
    {
        if (State == Phase.Gone) return;
        if (State == Phase.Carried) MarioNow()?.SmsBombGone(this);
        State = Phase.Gone;
        Puff(hitKingBoo ? 1.6f : 1.1f);
        if (!string.IsNullOrEmpty(BurstSe)) _sound?.Play(BurstSe);
        if (hitKingBoo) BossNow()?.TakeHit(this);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: burst{(hitKingBoo ? " on King Boo" : "")} at {GlobalPosition}");
        EmitSignal(SignalName.Burst, hitKingBoo);
        Gone();
    }
    /// <summary>Cleared (King Boo's hit clears the field; the expiry): a small puff and gone.</summary>
    public void Vanish()
    {
        if (State == Phase.Gone) return;
        if (State == Phase.Carried) MarioNow()?.SmsBombGone(this);
        State = Phase.Gone;
        Puff(0.9f);
        if (!string.IsNullOrEmpty(GoneSe)) _sound?.Play(GoneSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: vanishes");
        Gone();
    }
    private void Puff(float scale)
    {
        if (BurstFxScene == null || !IsInsideTree()) return;
        var fx = BurstFxScene.Instantiate<Node3D>();
        fx.Transform = new Transform3D(Basis.FromScale(Vector3.One * scale), GlobalPosition);
        GetParent().AddChild(fx);
        if (fx is SmsFxBurst b)
        {
            b.SetShaderParam("prm", new Vector4(BurstColor.R, BurstColor.G, BurstColor.B, 1f));
            b.SetShaderParam("env", new Vector4(BurstColor.R * 0.4f, BurstColor.G * 0.4f, BurstColor.B * 0.4f, 1f));
        }
    }
    private void Gone()
    {
        // the sound node plays out on its own: the Boo's visuals go now, the node a moment later
        if (_model != null) _model.Visible = false;
        if (_tongue != null) _tongue.Visible = false;
        SetPhysicsProcess(false);
        if (IsInsideTree()) GetTree().CreateTimer(2.0).Timeout += QueueFree; else QueueFree();
    }
}
