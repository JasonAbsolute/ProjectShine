using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// ELECTROKOOPA ON THE NET -- TAmiNoko (Enemy/amiNoko.cpp; JP アミノコ "net Koopa", the aminoko archive) on <see cref="SmsSmallEnemy"/> with the shipped
/// /enemy/aminoko.prm (SMS pass 144, feature/enemy-pinna). Pinna Park's crackling Koopa that crawls the Ferris wheel's nets (and the ceilings and
/// floors around them) from node to node of its path, pausing to turn at each one. Its body and the charged air 100 u out from its back shock
/// Mario on the same side of the net (ELECTRIC_SHOCK: the shock pose, -1 HP); a punch from the OTHER side of the net, or a ground pound on a floor
/// one, knocks it off: it flies away along its back and vanishes in a puff of smoke. The hose stuns it for a while.
///
/// THE GAME. TAmiNoko is a TWalkerEnemy (ACTOR_TYPE_AMI_NOKO, unk150 0x11) with LIVE_FLAG_UNK10 from init: no bind while it walks (TLiveActor::bind
/// is skipped; the nerves write mPositionDelta); only the death flight (bck 0) binds: gravity (0 in Die), the ground under the next point, the
/// airborne flag. /enemy/aminoko.prm: HeadHeight 120, BodyRadius 5, WallRadius 400, 1 hit point, the damage and attack cylinders 120 x 150,
/// FreezeWait 300, MarchSpeed 15 (unused: creepToCurPathNode's 3 u a tick is a constant), ElecRange 200 (unused), MtxRotSpeed 0.05. The bck table
/// is the archive's 16 clips in order: 0 flying1_loop (40), 1 flying1_start (9), 2 hit1 (194), 3 run1_end (19), 4 run1_loop (25), 5 run1_start
/// (79), 6-8 run2 end / loop / start, 9 turn1_end (19), 10 turn1_loop (40), 11 turn1_start (19), 12-14 turn2, 15 wait1 (120, loop); unk210 picks
/// set 1 (every stage but 8) or set 2. The PLANE (unk194, unk198: PLANE 0 a floor, 1 a roof, 2 a wall): every frame (calcRootMatrix) the nearest
/// of the walls within 10 u (isTouchedWallsAndMoveXZ, radius 10), the ground under the head height (signed distance >= 0) and the roof above.
/// The ORIENTATION: unk19C (the up) eases to the plane's normal and unk1A8 (the heading) to the direction of the current path node, each component
/// by MtxRotSpeed a frame (the heading turned 90 deg about the normal first when it points the other way, so the turn is never through zero);
/// the root matrix = (axis = up x heading, up, heading) re-orthogonalised (unk1B4 / unk1C0 keep the last good pair) at mPosition - 30 x up, scale
/// mScaling. The nerves: WALKONFENCE (the default): tick 0 setWalkAnm (run1_start); its end -> run1_loop; the target within 1.5 u with the clip
/// at its end: run1_loop -> run1_end; run1_end's end -> goToRandomNextGraphNode + TURN. creepToCurPathNode(3): during a loop clip (run / turn)
/// mPositionDelta += the direction to the node x 3 (the US build does not clamp it: the body dithers about the node until the loop ends). TURN:
/// turn1_start -> turn1_loop; the heading within 0.8 of the node's direction and the clip at its end: turn1_loop -> turn1_end -> wait1 (120
/// frames) -> WALKONFENCE; creep(0): no movement. The ELECTRICITY: TAmiHit, a hit actor (120 x 240, HIT_CATEGORY_PLAYER) kept at mPosition + 100 x
/// the up with its base 120 down (the cylinder centred 100 u out from its back); Mario in it, or in the body's own 120 x 150 attack cylinder ->
/// attackToMario: PLANE 0 (a floor): Mario under the body with a roof over him -> nothing; PLANE 1 (a roof): Mario standing with his feet + 5 over
/// the body -> nothing; PLANE 2 (a wall): Mario's wall plane facing the other way (his fence normal . the plane's normal < 0: the other side of
/// the net) -> nothing; else SMS_SendMessageToMario(ELECTRIC_SHOCK) and, taken, FREEZE. behaveToWater -> FREEZE (not already), the spray cooldown
/// cleared. isHitValid: PUNCH with Mario behind the net ((body - Mario) . up > 0) or any HIP_DROP -> DIE; TRAMPLE is never valid (he bounces, no
/// kill); only UNKB returns true. DIE: flying1_start, no collision; its end: the velocity = the model's up x 20, +10 u, airborne, flying1_loop;
/// KIL_SMOKE bound to the body every frame; past 30 ticks a wall within 3 x BodyRadius (isDeadByWall: the WALLHIT puffs, the velocity 0), the
/// ground (not airborne) or 10000 u from Mario -> DEAD + TNerveSmallEnemyDie, genRandomItem. FREEZE: hit1 + DNK_SHIBIRE_B x 2 at the body; its end
/// -> wait1; a wait1 loop end past FreezeWait -> back to what it was doing. Every frame: MSD_SE_EN_AMINOKO_SPARK and the sparks -- AMN_BIRI at
/// joints 11, 10, 9 (lkejnt1, rkejnt1, effnull1: the two antennae and the back), AMN_SPARK R / L / M at joint 11; in Freeze's first 46 ticks
/// DNK_SHIBIRE_A x 2 at joint 0 and DNK_HIBANA x 2 at joint 6 (the head). getGravityY: 0 in Die. isCollidMove false.
///
/// THE PORT. <c>_noBind</c> + <c>FlyBind</c>: the species owns the step -- the creep writes <c>_lin</c>, the death flight adds its own velocity, a floor
/// / wall probe ends it. The plane: a ray along the last up (+30 u .. -30 u) tracks the surface it rides; none -> the game's search (eight 10-u wall
/// rays, the floor under the head height, the roof above; 30 / 200 u at the first attach) picks the nearest. The orientation is the game's eased
/// pair in a Godot Basis; the model's feet are kept ON the plane (<see cref="FeetOnPlane"/>: the offset is the measured gap, the game's -30 u
/// otherwise). The shock is Mario's own TakeShock (MARIO_STATUS_ELECTRIC_DAMAGE from pass 50); the TAmiHit cylinder is a second CheckDistance in
/// HitCheckMario; the fence punch is Mario's SmsFencePunchActive window with TMario's 250 x 200 cylinder (as the Klamber's). The path is the
/// base's Graph (its children): a random OTHER node each time (DEVIATION: the game walks the graph's edges; the port's graph has none). The creep
/// is clamped to the distance left (the PAL build's fix: no dithering at a node). Effects: the sparks are Mario's ShockSparks scene bound to the
/// two antennae (<see cref="SparkFxScene"/>), the freeze's numbness the Cataquack's dizzy stars (<see cref="FreezeFxScene"/>), the death flight's
/// smoke and the wall crash's puffs slots with the enemy wall-hit puff as the default; the sounds are slots.
/// </summary>
public partial class SmsAmiNoko : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ aminoko.prm
    public class AmiNokoParams : Params { public float ElecRange = 200f, MtxRotSpeed = 0.05f; }
    /// <summary>The shipped /enemy/aminoko.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly AmiNokoParams Prm = new AmiNokoParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 400f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 100, ActiveEnemyNum = 20, JumpForce = 10f,
        SearchLength = 800f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 2000f, GiveUpHeight = 650f, AttackWait = 120, FreezeWait = 300,
        DamageRadius = 120f, DamageHeight = 150f, AttackRadius = 120f, AttackHeight = 150f, TurnSpeedLow = 5f, TurnSpeedHigh = 5f, BodyScaleLow = 1f, BodyScaleHigh = 1f,
        GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100, MarchSpeedLow = 15f, MarchSpeedHigh = 15f, ElecRange = 200f, MtxRotSpeed = 0.05f,
    };
    protected override Params P => Prm;
    /// <summary>TAmiHit's cylinder (initHitActor: attack 120 x 240, damage 120 x 240) and its place: 100 u out along the up, the base 120 down.</summary>
    public const float ElecRadius = 120f, ElecHeight = 240f, ElecOutUnits = 100f;
    /// <summary>TMario's attack cylinder during FENCE_PUNCH (mAttackParamsFencePunch: radius 250, height 200).</summary>
    public const float FencePunchRadius = 250f, FencePunchHeight = 200f;
    /// <summary>creepToCurPathNode's speed (3 u a tick), the death flight's (20 u a tick) and its wall reach (3 x BodyRadius).</summary>
    public const float CreepSpeed = 3f, FlySpeed = 20f, ReachUnits = 1.5f;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the crawl (3 u a tick in the game: 2.4 m/s) and the turn's easing.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    /// <summary>1 = the Pinna Park clips (run1 / turn1), 2 = the Pianta Village clips (run2 / turn2: the game's stage 8).</summary>
    [Export(PropertyHint.Range, "1,2,1")] public int ClipSet = 1;
    /// <summary>On: the model's feet sit on the surface it rides wherever the path's nodes are placed (the gap measured each tick). Off: the game's
    /// own placement -- the model 30 u into the surface from the node (its nodes sit 30 u out).</summary>
    [Export] public bool FeetOnPlane = true;
    /// <summary>The game's rule: it shocks a Mario on the ground behind the net too when he is within its charged air (only a Mario CLINGING to the
    /// other side is spared). Off: nothing through the net.</summary>
    [Export] public bool AttacksAcrossTheNet = true;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_amn_biri + ms_amn_spark: the sparks crackling on its two antennae while it lives (an SmsShockSparks scene: Active while alive). Empty =
    /// assets/Sms/Fx/ShockSparks.tscn (Mario's shock sparks stand in).</summary>
    [Export] public PackedScene SparkFxScene;
    /// <summary>ms_dnk_shibire_a + ms_dnk_hibana: the numbness over a stunned body (a looping GPUParticles3D scene as a child, x 2). Empty =
    /// assets/Sms/Enemy/Fx131/PoiDizzy.tscn (the Cataquack's dizzy stars stand in).</summary>
    [Export] public PackedScene FreezeFxScene;
    /// <summary>ms_kil_smoke: the smoke trailing the body knocked off the net (a looping GPUParticles3D scene as a child of the model). Empty = none.</summary>
    [Export] public PackedScene FlySmokeFxScene;
    /// <summary>ms_enm_wallhit (+ _o): the puff where the knocked-off body smacks a wall (an SmsFxBurst / one-shot scene). Empty = assets/Sms/Fx/EnemyWallHit.tscn.</summary>
    [Export] public PackedScene WallHitFxScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_AMINOKO_SPARK (the crackle, every frame while it lives: give it a looping stream), the bas tables' run / turn / hit / fly starts.</summary>
    [Export] public AudioStreamPlayer3D SparkSound, RunSound, TurnSound, HitSound, FlySound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's 16 bcks; Godot strips "_loop")
    private static readonly string[] Names = { "aminoko_flying1", "aminoko_flying1_start", "aminoko_hit1", "aminoko_run1_end", "aminoko_run1", "aminoko_run1_start",
        "aminoko_run2_end", "aminoko_run2", "aminoko_run2_start", "aminoko_turn1_end", "aminoko_turn1", "aminoko_turn1_start", "aminoko_turn2_end", "aminoko_turn2",
        "aminoko_turn2_start", "aminoko_wait1" };
    private static readonly int[] Frames = { 40, 9, 194, 19, 25, 79, 19, 25, 79, 19, 40, 19, 19, 40, 19, 120 };
    private static readonly bool[] Loops = { true, false, false, false, true, false, false, true, false, false, true, false, false, true, false, true };
    protected const int BckFlyLoop = 0, BckFlyStart = 1, BckHit = 2, BckRun1End = 3, BckRun1Loop = 4, BckRun1Start = 5, BckRun2End = 6, BckRun2Loop = 7, BckRun2Start = 8,
        BckTurn1End = 9, BckTurn1Loop = 10, BckTurn1Start = 11, BckTurn2End = 12, BckTurn2Loop = 13, BckTurn2Start = 14, BckWait = 15;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    private int RunStart => ClipSet == 2 ? BckRun2Start : BckRun1Start;
    private int RunLoop => ClipSet == 2 ? BckRun2Loop : BckRun1Loop;
    private int RunEnd => ClipSet == 2 ? BckRun2End : BckRun1End;
    private int TurnStart => ClipSet == 2 ? BckTurn2Start : BckTurn1Start;
    private int TurnLoop => ClipSet == 2 ? BckTurn2Loop : BckTurn1Loop;
    private int TurnEnd => ClipSet == 2 ? BckTurn2End : BckTurn1End;
    private bool InLoopClip => IsBck(BckRun1Loop) || IsBck(BckRun2Loop) || IsBck(BckTurn1Loop) || IsBck(BckTurn2Loop);

    // ------------------------------------------------------------------------------------------------------------------ nerves (TNerveAmiNoko*)
    protected const int NerveWalkOnFence = NerveSpecies, NerveATurn = NerveSpecies + 1, NerveADie = NerveSpecies + 2, NerveAFreeze = NerveSpecies + 3;
    private static readonly string[] AmiNerveNames = { "WalkOnFence", "Turn", "Die", "Freeze" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + AmiNerveNames.Length ? AmiNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveWalkOnFence;

    // ------------------------------------------------------------------------------------------------------------------ state
    public enum PlaneKind { Floor = 0, Roof = 1, Wall = 2 }
    private bool _hasPlane; private Vector3 _planeN = Vector3.Up; private float _planeGap; private PlaneKind _planeKind = PlaneKind.Floor;   // unk194 / unk198
    private Vector3 _up = Vector3.Up, _heading = Vector3.Back;                 // unk19C / unk1A8 (eased)
    private Vector3 _upSaved = Vector3.Up, _headingSaved = Vector3.Back;       // unk1B4 / unk1C0
    private Vector3 _posU;                                                     // mPosition as the ticks advance it, units
    private Vector3 _flyVel;                                                   // mVelocity (the death flight)
    private Vector3 _dieUp;                                                    // the model's up as the flight began (the matrix's column 1)
    private bool _needAttach = true; private bool _flyLanded, _flyWall;
    private uint _planeMask = 5;
    private CollisionShape3D _shape; private Vector3 _shapeLocal = new Vector3(0f, 1.2f, 0f);
    private Node3D _sparkL, _sparkR, _freezeFx, _smokeFx;
    private Godot.Collections.Array<Rid> _excl;
    /// <summary>For the drives.</summary>
    public string CurrentNerveName => NerveName(_cur);
    public bool HasPlane => _hasPlane;
    public PlaneKind Plane => _planeKind;
    public Vector3 PlaneNormal => _hasPlane ? _planeN : Vector3.Zero;
    public float PlaneGapUnits => _planeGap;
    public Vector3 Up => _up;
    public Vector3 Heading => _heading;
    public Vector3 BodyUnits => _posU;
    public Vector3 ElecCentreUnits => _posU + _up.Normalized() * ElecOutUnits;
    public bool Dying => IsNerve(NerveADie);
    public bool Frozen => IsNerve(NerveAFreeze);
    public bool Flying => IsNerve(NerveADie) && IsBck(BckFlyLoop);
    public Vector3 FlyVelocity => _flyVel;
    public bool Sparking => _sparkL != null && IsInstanceValid(_sparkL) && _sparkL is SmsShockSparks s && s.Active;
    public int Shocks { get; private set; }
    public int NodesReached { get; private set; }
    public int Turns { get; private set; }
    public string DeathCause { get; private set; } = "";
    public Vector3 GoalUnits => Goal();

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _planeMask = CollisionMask;                                             // the scene's Collision > Mask names the layers it rides (the map); the body collides with nothing
        CollisionMask = 0;
        _shape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (_shape != null) _shapeLocal = _shape.Position;
        _flags150 = 0x11;
        if (SparkFxScene == null && ResourceLoader.Exists("res://assets/Sms/Fx/ShockSparks.tscn")) SparkFxScene = GD.Load<PackedScene>("res://assets/Sms/Fx/ShockSparks.tscn");
        if (FreezeFxScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Fx131/PoiDizzy.tscn")) FreezeFxScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Fx131/PoiDizzy.tscn");
        if (WallHitFxScene == null && ResourceLoader.Exists("res://assets/Sms/Fx/EnemyWallHit.tscn")) WallHitFxScene = GD.Load<PackedScene>("res://assets/Sms/Fx/EnemyWallHit.tscn");
        if (DebugPrint) GD.Print($"[SMS aminoko] {Name}: plane mask {_planeMask}; graph {(Graph != null ? Graph.GetChildCount() : 0)} nodes");
    }
    /// <summary>TAmiNoko::reset = TWalkerEnemy::reset; init's LIVE_FLAG_UNK10 (no bind) and the fresh frame (up +Y, heading +Z).</summary>
    protected override void Reset()
    {
        base.Reset();
        _bodyScale = 1f; _scale = 1f;
        _noBind = true;
        _hasPlane = false; _planeN = Vector3.Up; _planeGap = 0f; _planeKind = PlaneKind.Floor; _needAttach = true;
        _up = Vector3.Up; _heading = PolarXZ(_yaw, 1f); _upSaved = _up; _headingSaved = _heading;
        _flyVel = Vector3.Zero; _dieUp = Vector3.Up; _flyLanded = false; _flyWall = false; DeathCause = "";
        _posU = Pos();
        InitialGraphNode();
        SetBck(BckWait);
        StopFx(ref _smokeFx); StopFx(ref _freezeFx);
        ApplyLook();
    }
    /// <summary>initialGraphNode: the path's nearest node is the first target; no graph -> a random point (the base's).</summary>
    private void InitialGraphNode()
    {
        if (Graph != null && Graph.GetChildCount() > 0)
        {
            Node3D best = null; float bestD = float.MaxValue;
            foreach (Node k in Graph.GetChildren())
                if (k is Node3D n3) { float d = (n3.GlobalPosition * U - _posU).LengthSquared(); if (d < bestD) { bestD = d; best = n3; } }
            if (best != null) { SetGoalPoint(best.GlobalPosition * U); return; }
        }
        GoToRandomNextGraphNode();
    }
    protected override Vector3 LookScale => Vector3.One;                         // mScaling 1: the body scale is 1
    protected override bool IsCollidMove(Node3D other) => false;
    protected override bool WallHopAllowed() => false;
    protected override bool FlyBind => true;
    protected override float GravityY() => IsNerve(NerveADie) ? 0f : P.Gravity;   // getGravityY (the flight never binds gravity: 0 in Die)

    public override void _PhysicsProcess(double delta)
    {
        if (!Engine.IsEditorHint()) _posU = Pos();
        base._PhysicsProcess(delta);
        if (_dead) { StopSparks(); return; }
        bool alive = !IsNerve(NerveADie);
        SetSparks(alive);
        BoundFx(ref _freezeFx, FreezeFxScene, IsNerve(NerveAFreeze), 2f);
        BoundFx(ref _smokeFx, FlySmokeFxScene, Flying, 1f);
        if (SparkSound != null && SparkSound.Stream != null) { if (alive && !SparkSound.Playing) SparkSound.Play(); else if (!alive && SparkSound.Playing) SparkSound.Stop(); }
    }

    // ------------------------------------------------------------------------------------------------------------------ the tick hooks
    /// <summary>The frame's head: the plane and the orientation (the US build's calcRootMatrix runs them every frame; here every tick).</summary>
    protected override void MoveObjectPre()
    {
        TrackClip();
        if (!IsNerve(NerveADie)) { FindPlane(); CalcDirection(); }
    }
    /// <summary>bind(): nothing while it walks (UNK10); the death flight (bck 0) moves by its velocity -- the ground under the next point lands it.</summary>
    protected override void SetBehavior()
    {
        if (_dead || !IsNerve(NerveADie) || !IsBck(BckFlyLoop)) return;
        Vector3 next = _posU + _flyVel;                                             // getNextFramePosition: mPosition + mPositionDelta (already in _posU) + mVelocity
        float g = GroundAt(next, out bool hasGround);
        if (hasGround && next.Y <= g + 1.05f)
        {
            next.Y = g + 1f;
            _flyVel = Vector3.Zero; _flyLanded = true;
        }
        _lin += next - _posU;
        _posU = next;
        _airborne = !_flyLanded;
    }

    // ------------------------------------------------------------------------------------------------------------------ the plane (unk194 / unk198)
    private PhysicsDirectSpaceState3D Space => GetWorld3D().DirectSpaceState;
    private Godot.Collections.Array<Rid> Excl()
    {
        if (_excl == null) { _excl = new Godot.Collections.Array<Rid> { GetRid() }; if (_mario != null) _excl.Add(_mario.GetRid()); }
        return _excl;
    }
    private bool Ray(Vector3 fromU, Vector3 toU, out Vector3 posU, out Vector3 n)
    {
        posU = Vector3.Zero; n = Vector3.Up;
        var q = PhysicsRayQueryParameters3D.Create(fromU / U, toU / U, _planeMask, Excl());
        var hit = Space.IntersectRay(q);
        if (hit.Count == 0) return false;
        posU = (Vector3)hit["position"] * U; n = ((Vector3)hit["normal"]).Normalized();
        return true;
    }
    private static readonly Vector3[] Dirs8 = { new(1, 0, 0), new(-1, 0, 0), new(0, 0, 1), new(0, 0, -1), new(0.7071f, 0, 0.7071f), new(-0.7071f, 0, 0.7071f), new(0.7071f, 0, -0.7071f), new(-0.7071f, 0, -0.7071f) };
    private static PlaneKind KindOf(Vector3 n) => n.Y > 0.7f ? PlaneKind.Floor : n.Y < -0.7f ? PlaneKind.Roof : PlaneKind.Wall;
    /// <summary>calcRootMatrix's plane pick: the walls within 10 u (radius 10), the ground under the head height, the roof above -- the nearest wins. The port
    /// first follows the plane it rides (a ray along the last up); the search runs when that misses, and wider at the first attach.</summary>
    private void FindPlane()
    {
        Vector3 p = _posU;
        if (_hasPlane && !_needAttach)
        {
            if (Ray(p + _planeN * 30f, p - _planeN * 30f, out Vector3 hp, out Vector3 hn) && hn.Dot(_planeN) > 0.3f)
            {
                _planeN = hn; _planeGap = (p - hp).Dot(hn); _planeKind = KindOf(hn);
                return;
            }
        }
        float wallR = _needAttach ? 30f : 10f, vertR = _needAttach ? 200f : 120f;
        float best = -1f; bool found = false; Vector3 bestN = Vector3.Up, bestP = p; PlaneKind bestKind = PlaneKind.Wall;
        foreach (Vector3 d in Dirs8)
            if (Ray(p - d * 20f, p + d * wallR, out Vector3 hp, out Vector3 hn) && hn.Dot(d) < -0.3f)
            {
                float dist = MathF.Abs((hp - p).Dot(hn));
                if (!found || dist < best) { best = dist; found = true; bestN = hn; bestP = hp; bestKind = PlaneKind.Wall; }
            }
        if (Ray(p + Vector3.Up * P.HeadHeight, p - Vector3.Up * vertR, out Vector3 gp, out Vector3 gn) && gn.Y > 0.3f)
        {
            float dist = (p - gp).Dot(gn);
            if (dist >= -1f && (!found || dist < best)) { best = dist; found = true; bestN = gn; bestP = gp; bestKind = PlaneKind.Floor; }
        }
        if (Ray(p, p + Vector3.Up * vertR, out Vector3 rp, out Vector3 rn) && rn.Y < -0.3f)
        {
            float dist = (p - rp).Dot(rn);
            if (dist >= -1f && (!found || dist < best)) { best = dist; found = true; bestN = rn; bestP = rp; bestKind = PlaneKind.Roof; }
        }
        if (found)
        {
            _hasPlane = true; _planeN = bestN; _planeGap = (p - bestP).Dot(bestN); _planeKind = bestKind;
            if (_needAttach) { _up = bestN; _upSaved = _up; if (DebugPrint) GD.Print($"[SMS aminoko] {Name}: attached to a {bestKind} (n {bestN}) {_planeGap:F1} u off"); }
        }
        _needAttach = false;
    }
    private float GroundAt(Vector3 pU, out bool has)
    {
        has = Ray(pU + Vector3.Up * P.HeadHeight, pU - Vector3.Up * 60f, out Vector3 hp, out Vector3 hn) && hn.Y > 0.3f;
        return has ? hp.Y : 0f;
    }
    /// <summary>The game eases each COMPONENT by MtxRotSpeed and renormalises; that has fixed points (a heading on a diagonal with the target in the opposite
    /// quadrant steps straight inward and the normalisation undoes it -- a body turning from +X to a node up and behind it never finished its turn in the
    /// port's first run). The port turns the vector about their common axis by MtxRotSpeed radians a tick instead (DEVIATION: the same speed in the small
    /// angles, never stuck; an anti-parallel pair snaps, as the PAL build's <= -1 test does).</summary>
    private static Vector3 RotateToward(Vector3 from, Vector3 to, float maxRad)
    {
        if (to.LengthSquared() < 1e-8f) return from;
        if (from.LengthSquared() < 1e-8f) return to.Normalized();
        Vector3 f = from.Normalized(), t = to.Normalized();
        float c = Mathf.Clamp(f.Dot(t), -1f, 1f);
        if (c > 0.99999f) return t;
        Vector3 axis = f.Cross(t);
        if (axis.LengthSquared() < 1e-8f) return t;
        float ang = MathF.Acos(c);
        return f.Rotated(axis.Normalized(), MathF.Min(ang, maxRad)).Normalized();
    }
    /// <summary>calcRootMatrix (US) / calcDirection (PAL): the up eased to the plane's normal, the heading to the node's direction (turned 90 deg about the
    /// normal first when it points the other way, so the turn goes the long way round and never through zero), MtxRotSpeed a tick; the last good
    /// (up, heading) pair kept.</summary>
    private void CalcDirection()
    {
        Vector3 toNode = Goal() - _posU;
        Vector3 dir = toNode.LengthSquared() < 1e-8f ? Vector3.Right : toNode.Normalized();
        float step = Prm.MtxRotSpeed * SpeedScale;
        Vector3 normal = _hasPlane ? _planeN : Vector3.Up;
        _up = RotateToward(_up, normal, step);
        if (_heading.Dot(dir) < -0.1f) dir = dir.Rotated(normal.Normalized(), Mathf.Pi * 0.5f);
        _heading = RotateToward(_heading, dir, step);
        if (_up.Cross(_heading).LengthSquared() > 1e-8f) { _upSaved = _up; _headingSaved = _heading; }
    }
    /// <summary>The root matrix's frame: (axis = up x heading, up, heading) re-orthogonalised.</summary>
    private Basis Frame()
    {
        Vector3 up = _up, hd = _heading;
        Vector3 axis = up.Cross(hd);
        if (axis.LengthSquared() < 1e-8f) { up = _upSaved; hd = _headingSaved; axis = up.Cross(hd); if (axis.LengthSquared() < 1e-8f) axis = Vector3.Right; }
        axis = axis.Normalized();
        Vector3 fwd = axis.Cross(up); fwd = fwd.LengthSquared() < 1e-8f ? Vector3.Back : fwd.Normalized();
        Vector3 nrm = fwd.Cross(axis); nrm = nrm.LengthSquared() < 1e-8f ? Vector3.Up : nrm.Normalized();
        return new Basis(axis, nrm, fwd);
    }
    /// <summary>calcRootMatrix: the frame at mPosition - 30 x up (the model's feet 30 u into the surface from the node), scale 1; <see cref="FeetOnPlane"/> uses
    /// the measured gap instead. The collision shape (the hose's target) rides the same frame. The death flight keeps the frame it had.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Basis f = Frame();
        float back = FeetOnPlane && _hasPlane && !IsNerve(NerveADie) ? _planeGap : 30f;
        Vector3 off = -_up.Normalized() * (back / U);
        Basis b = f * Basis.FromScale(Vector3.One * _modelScale);
        if (IsInsideTree())
        {
            _model.GlobalTransform = new Transform3D(b, GlobalPosition + off);
            if (_shape != null) _shape.GlobalTransform = new Transform3D(f, GlobalPosition + off + f * _shapeLocal);
        }
        else _model.Transform = new Transform3D(b, off);
    }

    // ------------------------------------------------------------------------------------------------------------------ effects
    private void SetSparks(bool on)
    {
        if (on && SparkFxScene != null && _sparkL == null)
        {
            _sparkL = SpawnSpark("lkejnt1"); _sparkR = SpawnSpark("rkejnt1");
        }
        SetSpark(_sparkL, on); SetSpark(_sparkR, on);
    }
    /// <summary>Mario's shock sparks stand in for ms_amn_biri / ms_amn_spark: only their flashes and flying sparks (the body-wide streaks and arcs are his
    /// whole-body shock, far too much on an antenna tip), at half size.</summary>
    private static void SetSpark(Node3D n, bool on)
    {
        if (n is not SmsShockSparks s) return;
        if (s.Active == on) return;
        s.Active = on;
        if (on) { if (s.Streaks != null) s.Streaks.Emitting = false; if (s.Arcs != null) s.Arcs.Emitting = false; }
    }
    private Node3D SpawnSpark(string bone)
    {
        Node3D n = SparkFxScene.Instantiate<Node3D>();
        if (n is SmsFollowEffect fe) { fe.BoneName = bone; AddChild(n); fe.Bind(this); }
        else AddChild(n);
        n.Scale = Vector3.One * 0.45f;
        return n;
    }
    private void StopSparks()
    {
        if (_sparkL is SmsShockSparks l) l.Active = false;
        if (_sparkR is SmsShockSparks r) r.Active = false;
    }
    private void BoundFx(ref Node3D slot, PackedScene scene, bool on, float scale)
    {
        if (on && slot == null && scene != null)
        {
            slot = scene.Instantiate<Node3D>();
            if (_model != null) _model.AddChild(slot); else AddChild(slot);
            slot.Position = Vector3.Up * (P.DamageHeight * 0.6f);                   // the model's own units
            slot.Scale = Vector3.One * (scale / Mathf.Max(_modelScale, 1e-4f));
        }
        else if (!on && slot != null) StopFx(ref slot);
    }
    private void StopFx(ref Node3D slot) { if (slot != null && IsInstanceValid(slot)) slot.QueueFree(); slot = null; }
    private Vector3 MarioFeetU() => _mario != null ? _mario.SmsFeetWorld() * U : _posU;

    // ------------------------------------------------------------------------------------------------------------------ the path
    /// <summary>goToRandomNextGraphNode: a random OTHER node of the path (the one within 50 u in 3D is the current); no graph -> the base's random point.</summary>
    private void NextGraphNode()
    {
        if (Graph != null && Graph.GetChildCount() > 1)
        {
            var kids = Graph.GetChildren();
            List<Node3D> others = new();
            foreach (Node k in kids) if (k is Node3D n3 && (n3.GlobalPosition * U - _posU).Length() > 50f) others.Add(n3);
            if (others.Count > 0) { SetGoalPoint(others[_rng.Next(others.Count)].GlobalPosition * U); return; }
        }
        GoToRandomNextGraphNode();
    }
    /// <summary>creepToCurPathNode(speed): during a loop clip the delta toward the node -- clamped to the distance left (the PAL build's fix).</summary>
    private void CreepToCurPathNode(float speed)
    {
        if (!InLoopClip || speed <= 0f) return;
        Vector3 d = Goal() - _posU;
        float len = d.Length();
        if (len < 1e-5f) return;
        Vector3 step = d * (MathF.Min(speed * SpeedScale, len) / len);
        _lin += step; _posU += step;
    }
    private void ChangeBck(int i, AudioStreamPlayer3D sound = null) { SetBck(i); sound?.Play(); _prevFrame = -1f; }
    private float _prevFrame = -1f; private bool _wrapped;
    /// <summary>checkCurAnmEnd for this species: the base's last-1.1-frames window, or the looping clip seen to WRAP since the last tick (the window is
    /// narrower than an uneven render step: a missed loop end would hold the Freeze a whole wait1 loop longer).</summary>
    private bool ClipEnded() => BckEnded() || _wrapped;
    private void TrackClip()
    {
        float f = BckFrame();
        _wrapped = _bck >= 0 && _bck < Loops.Length && Loops[_bck] && _prevFrame >= 0f && f < _prevFrame - 5f;
        _prevFrame = f;
    }

    // ------------------------------------------------------------------------------------------------------------------ hits
    /// <summary>The collision lists for ACTOR_TYPE_AMI_NOKO: his HIP_DROP (any plane) and FENCE_PUNCH from behind the net kill it, a TRAMPLE only bounces him
    /// (isHitValid false), the hose freezes it; the other way its 120 x 150 attack cylinder OR TAmiHit's 120 x 240 charged air 100 u out from its back
    /// on his damage cylinder -> attackToMario. Mario's cylinder stands on his feet.</summary>
    protected override void HitCheckMario()
    {
        if (_mario == null || IsNerve(NerveADie) || !GetCollisionLayerValue(1)) return;
        Vector3 g = _posU, m = MarioFeetU();
        bool onGround = _mario.IsOnFloor();
        bool rising = !onGround && _mario.Velocity.Y > 0f;
        bool fencePunch = _mario.SmsFencePunchActive;
        float ar = fencePunch ? FencePunchRadius : onGround ? SmsParams.Mario.HoldRadius : (rising ? SmsParams.Mario.PushupRadius : SmsParams.Mario.TrampleRadius);
        float ah = fencePunch ? FencePunchHeight : onGround ? SmsParams.Mario.AttackHeight : (rising ? SmsParams.Mario.PushupHeight : SmsParams.Mario.AttackHeight);
        bool marioHits = CheckDistance(m, ar, ah, g, Prm.DamageRadius, Prm.DamageHeight);
        if (marioHits)
        {
            bool falling = !onGround && _mario.Velocity.Y < 0f && g.Y < m.Y;
            var st = _mario.stateOfMario;
            bool noTrample = st == Mario.MarioState.diving || st == Mario.MarioState.singleJumpDive || st == Mario.MarioState.doubleJumpDive || st == Mario.MarioState.tripleJumpDive
                || st == Mario.MarioState.bellySlidingFromDive || st == Mario.MarioState.swimming || st == Mario.MarioState.ledgeHang || st == Mario.MarioState.ledgeClimb
                || st == Mario.MarioState.treeGrab || st == Mario.MarioState.treeClimb || st == Mario.MarioState.hurt || st == Mario.MarioState.dead
                || st == Mario.MarioState.fence || st == Mario.MarioState.roofHang;
            if (falling && st == Mario.MarioState.groundPoundFalling) { Die("a ground pound"); return; }                        // hitNormal: HIP_DROP -> isHitValid -> Die
            if (st == Mario.MarioState.groundPoundLanding && g.Y < m.Y + 2f) { Die("a ground pound"); return; }                  // hitHipDrop: the landing frames with the body under him
            if (falling && !noTrample) { _mario.StompBounce(); return; }                                                        // TRAMPLE: taken (he bounces), never valid: no kill
            if (_mario.SmsHoseSpraying) SmsSprayedByWater(m + Vector3.Up * (80f / U));
            if (_dead || IsNerve(NerveADie)) return;
            if (fencePunch && (g - m).Dot(_up) > 0f) { Die("a punch from behind the net"); return; }                            // isHitValid(PUNCH): Mario behind the net
        }
        Vector3 e = ElecCentreUnits; e.Y -= 0.5f * ElecHeight;                                                                  // TAmiHit::perform: 100 u out, the base half the height down
        bool weHit = CheckDistance(g, Prm.AttackRadius, Prm.AttackHeight, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight)
                  || CheckDistance(e, ElecRadius, ElecHeight, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight);
        if (weHit) AttackToMario();
    }
    /// <summary>attackToMario: the plane rule, then ELECTRIC_SHOCK (Mario's TakeShock); taken -> FREEZE.</summary>
    protected override void AttackToMario()
    {
        if (_mario == null || !_hasPlane || IsNerve(NerveADie)) return;
        Vector3 g = _posU, m = MarioFeetU();
        bool check = true;
        switch (_planeKind)
        {
            case PlaneKind.Floor: if (m.Y < g.Y - 5f) check = false; break;                                                 // Mario under the floor it stands on (his roof plane over him)
            case PlaneKind.Roof: if (m.Y + 5f > g.Y) check = false; break;                                                  // Mario over the ceiling it hangs from (his ground plane is always some floor)
            case PlaneKind.Wall:
                if (_mario.SmsIsFencing && _mario.SmsFenceNormal.Dot(_planeN) < 0f) check = false;                           // clinging to the OTHER side of the net
                if (!AttacksAcrossTheNet && (m - g).Dot(_planeN) < -5f) check = false;
                break;
        }
        if (!check) return;
        bool wasHurt = _mario.stateOfMario == Mario.MarioState.hurt;
        _mario.TakeShock();
        if (!wasHurt && _mario.stateOfMario == Mario.MarioState.hurt)
        {
            Shocks++;
            if (DebugPrint) GD.Print($"[SMS aminoko] {Name}: ELECTRIC_SHOCK on Mario ({_planeKind}) -> Freeze");
            if (!IsNerve(NerveAFreeze)) PushNerve(NerveAFreeze);
        }
    }
    /// <summary>isHitValid: the base's TRAMPLE / HIP_DROP paths never kill through it (the deaths are the species' own above).</summary>
    protected override bool IsHitValid(HitMessage msg) => false;
    /// <summary>behaveToWater: FREEZE unless frozen; the spray cooldown cleared (the next drop counts again).</summary>
    protected override void BehaveToWater()
    {
        if (_dead || IsNerve(NerveADie)) return;
        if (!IsNerve(NerveAFreeze)) PushNerve(NerveAFreeze);
        _sprayCooldown = 0;
    }
    /// <summary>A kill from outside (a thrown fruit, a drive): the species' flight-off death.</summary>
    public override void Kill() { if (!_dead) Die("a kill"); }
    private void Die(string cause)
    {
        if (_dead || IsNerve(NerveADie)) return;
        DeathCause = cause;
        ResetSpine(); SetNext(NerveADie);
        if (DebugPrint) GD.Print($"[SMS aminoko] {Name}: {cause} -> Die");
    }
    /// <summary>isDeadByWall: a wall within 3 x BodyRadius of the body -> the WALLHIT puffs, the velocity 0.</summary>
    private bool IsDeadByWall()
    {
        Vector3 d = _flyVel.LengthSquared() > 1e-6f ? _flyVel.Normalized() : _dieUp;
        if (!Ray(_posU, _posU + d * (3f * P.BodyRadius), out Vector3 hp, out _)) return false;
        _flyVel = Vector3.Zero; _flyWall = true;
        SpawnBurst(WallHitFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), hp / U));
        return true;
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveWalkOnFence:                              // TNerveAmiNokoWalkOnFence
                if (_time == 0) ChangeBck(RunStart, RunSound);
                if (IsBck(RunStart) && ClipEnded()) ChangeBck(RunLoop);
                if ((Goal() - _posU).Length() < ReachUnits && ClipEnded())
                {
                    if (IsBck(RunEnd))
                    {
                        NodesReached++;
                        NextGraphNode();
                        PushAfterCurrent(NerveATurn);
                        return true;
                    }
                    if (IsBck(RunLoop)) ChangeBck(RunEnd);
                }
                CreepToCurPathNode(CreepSpeed);
                return false;

            case NerveATurn:                                    // TNerveAmiNokoTurn
                if (_time == 0) { ChangeBck(TurnStart, TurnSound); Turns++; }
                if (IsBck(TurnStart) && ClipEnded()) ChangeBck(TurnLoop);
                {
                    Vector3 d = Goal() - _posU;
                    d = d.LengthSquared() < 1e-8f ? Vector3.Right : d.Normalized();
                    if (d.Dot(_heading) > 0.8f && ClipEnded())
                    {
                        if (IsBck(TurnEnd)) ChangeBck(BckWait);
                        else if (IsBck(BckWait)) { PushAfterCurrent(NerveWalkOnFence); return true; }
                        else ChangeBck(TurnEnd);
                    }
                }
                CreepToCurPathNode(0f);
                return false;

            case NerveADie:                                     // TNerveAmiNokoDie
                if (_time == 0)
                {
                    ChangeBck(BckFlyStart, FlySound);
                    SetCollisionLayerValue(1, false);
                    _hp = 0;
                    _dieUp = Frame().Y;
                    _flyVel = Vector3.Zero; _flyLanded = false; _flyWall = false;
                }
                if (IsBck(BckFlyStart) && ClipEnded())
                {
                    _flyVel = _dieUp * FlySpeed;                                                                            // the model's up (the matrix's column 1) x 20
                    _posU += Vector3.Up * 10f; _lin += Vector3.Up * 10f;
                    _airborne = true;
                    ChangeBck(BckFlyLoop);
                    if (DebugPrint) GD.Print($"[SMS aminoko] {Name}: flies off at {_flyVel} u/tick");
                }
                if (IsBck(BckFlyLoop) && _time > 30)
                {
                    bool far = (_posU - MarioFeetU()).Length() > 10000f;
                    if (IsDeadByWall() || _flyLanded || far)
                    {
                        DeathCause += _flyWall ? " (a wall)" : _flyLanded ? " (the ground)" : " (far away)";
                        SetCollisionLayerValue(1, false);
                        _noReward = false;
                        GenRandomItem();
                        Dead();
                        return true;
                    }
                }
                return false;

            case NerveAFreeze:                                  // TNerveAmiNokoFreeze
                if (_time == 0) ChangeBck(BckHit, HitSound);
                if (ClipEnded())
                {
                    if (IsBck(BckHit)) ChangeBck(BckWait);
                    else if (_time > P.FreezeWait) return true;
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
