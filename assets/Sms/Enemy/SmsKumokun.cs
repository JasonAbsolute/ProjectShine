using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// KLAMBER -- TKumokun (Enemy/Kumokun.cpp; JP くもくん, "little spider") on <see cref="SmsSmallEnemy"/> (SMS pass 139, feature/enemy-ricco). Ricco
/// Harbor's wall-walking spider: the one enemy that lives on the harbour's climbing nets, its roofs and its decks, and the first of Ricco's family.
///
/// THE GAME. No /enemy/kumokun.prm shipped on the disc (none in params.szs nor in any stage's map/params), so the retail Klamber runs on the
/// decomp's PARAM_INIT defaults: TorqueY 0.01 (x 3 when attacking: a turn is 100 ticks, 33 attacking), MarchSpeed 1.0 and AttackSpeed 1.5 u/tick,
/// MarchTimer 600, WaitTimer 180, SearchRange 1000 u, FlySpeed 30 u/tick. TKumokun::init never calls TSmallEnemy::init and its reset() is empty, so the
/// small-enemy scaling never runs: the body scale is 1 and the hit cylinders are initCollision's own -- attack 60 x 50, damage 65 x 70 u; head height
/// 75, body / wall radius 100 u. A PLANE (unk198: a wall, a roof or a floor triangle) is attached at the placement (initAttachPlane: a wall within
/// 100 u at head height first, else a roof within head height above, else the floor) and the body's whole orientation is a quaternion (unk19C)
/// whose local +Y is the plane normal and local +Z the heading; on a wall the model is drawn 30 u INTO the plane (calcRootMatrix). isOnFloor = normal.y
/// over 0.7, isOnRoof under -0.7, a wall otherwise.
///   The nerves: SEARCH (decideTarget: Mario found on the same plane -> turn toward him with unk1D4 = attacking, the turn1 loop; else a random turn
/// of 90..270 degrees about the normal through turn1_start -> loop; the slerp at TorqueY a tick, then turn1_end) -> PREWALK (run1_start) -> WALK
/// (run1_loop: forward x MarchSpeed, or AttackSpeed when attacking, until the bind's hit-plane counter passes 30 or MarchTimer) -> POSTWALK (run1_end)
/// -> WAIT (wait1 for WaitTimer, attacking off; Mario found -> Search at once). The bind (TKumokun::bind) re-projects the body onto its plane each tick:
/// checkOnMovingWall / Floor / Roof for the body AND for a look-ahead point (50 u ahead on a floor or roof, 37.5 u on a wall), the two deltas SUMMED
/// (so the clear-path speed is twice MarchSpeed: 2 u/tick = 3.2 m/s, 3 u/tick attacking) and the counter stepping whenever either is blocked --
/// walking off a plane's edge, down into a floor, up a step over 30 u, into a wall -- the look-ahead stops the body a few units short of an edge.
/// Mario is found (isFindOutMario) only ON THE SAME PLANE: fencing a wall of the same normal (SMS_IsMarioFencing / SMS_GetMarioWlPlane), hanging a
/// roof of the same normal, or standing a floor of the same normal -- within SearchRange and within head height (x 3 under a roof) of the plane.
/// WATER (behaveHitWater): a 30-tick timer; FREEZE (hit_start -> hit_loop, curled up) until it runs out, then POSTFREEZE (hit_end, 149 frames) and
/// Search; a new hit while curled is ignored, one during the uncurl re-curls. HIT_MESSAGE_PUNCH (TMario::fencePunch's frames 5..9 at attack radius
/// 250 / height 200, or the roof kick's frames 9..13): from a fence or roof of ANOTHER normal (the other side of the net) -> PREFLY (hit_start at the
/// double rate) -> FLY (flying1_loop; the body 75 u off the plane, flying along the normal at FlySpeed with no gravity, KIL_SMOKE behind it) until a
/// plane is met (checkRoof / Floor / WallPlane; the counter) or 1800 ticks -> Die with the flyingdown1 clip. TRAMPLE on a floor -> Die (down1, Mario's
/// bounce); a HIP_DROP on a floor -> Die, under a roof -> PreFly (knocked off the ceiling). Contact (attackToMario) hurts Mario only while attacking
/// (isAttack: the Search / Walk / Wait nerves, and not when Mario is above a roof-hanging one); doKeepDistance (on the same plane and NOT attacking:
/// frozen, flying, dying) makes it a solid Mario is pushed off (TMario::keepDistance). Other actors never push it (isCollidMove false). The bck rate is
/// half speed everywhere, full speed while attacking or in PreFly. A TWallAtGraph: the placement's graph, a closed loop, becomes fences 100 u inside
/// which the body is kept (checkWalls).
///
/// THE PORT. The map queries are rays (checkGround / checkRoof straight down / up; checkWallPlane as eight rays in XZ plus one into the plane, the
/// nearest wall -- |normal.y| under 0.7 -- within the radius, the point pushed out to the radius along the wall's XZ normal). The body collides with
/// NOTHING (its mask is read into the plane mask and zeroed: the bind is the game's, not Godot's) and keeps the base's layer so the hose finds it.
/// <see cref="SmsSmallEnemy.Graph"/> is the closed fence loop (TWallAtGraph), not a wander path. A placed one re-attaches from its placed point after
/// RelaunchTicks (1800 in the scene; the game's Klambers are the manager's). DEVIATIONS (logged): the placement snaps onto the plane found (the game
/// leaves the point as placed and a point off the wall never moves); a plane not found at the first tick is looked for again every 30 ticks;
/// <see cref="AttacksAcrossTheNet"/>; the bas sound tables are slots; KIL_SMOKE is a slot (<see cref="FlySmokeFxScene"/>); moving floors / the
/// ride momentum are not ported.
/// </summary>
public partial class SmsKumokun : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ kumokun.prm (none shipped: the PARAM_INIT defaults ARE the retail values)
    public class KumoParams : Params
    {
        public float TorqueY = 0.01f, MarchSpeed = 1f, AttackSpeed = 1.5f, SearchRange = 1000f, FlySpeed = 30f;
        public int MarchTimer = 600, WaitTimer = 180;
    }
    public static readonly KumoParams Prm = new KumoParams
    {
        HeadHeight = 75f, BodyRadius = 100f, WallRadius = 100f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 6, ActiveEnemyNum = 6,
        AttackRadius = 60f, AttackHeight = 50f, DamageRadius = 65f, DamageHeight = 70f,                  // TKumokunManager::load + initCollision
        BodyScaleLow = 1f, BodyScaleHigh = 1f, MarchSpeedLow = 1f, MarchSpeedHigh = 1f, TurnSpeedLow = 3f, TurnSpeedHigh = 5f,
        TorqueY = 0.01f, MarchSpeed = 1f, AttackSpeed = 1.5f, MarchTimer = 600, WaitTimer = 180, SearchRange = 1000f, FlySpeed = 30f,
    };
    protected override Params P => Prm;
    /// <summary>mHeadHeight (initCollision): the plane probes' reach.</summary>
    public const float HH = 75f;
    /// <summary>TMario's attack cylinder during FENCE_PUNCH (mAttackParamsFencePunch: radius 250, height 200) and KICK_ROOF (200 / 250).</summary>
    public const float FencePunchRadius = 250f, FencePunchHeight = 200f, RoofKickRadius = 200f, RoofKickHeight = 250f;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the march and the attack run (the game: 1.0 / 1.5 u/tick, doubled by the bind's look-ahead on a clear plane = 3.2 / 4.8 m/s).</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    /// <summary>How far along its plane it notices Mario, in the game's units (SearchRange 1000 = 13.4 m).</summary>
    [Export(PropertyHint.Range, "100,4000,10")] public float SearchRangeUnits = 1000f;
    /// <summary>How fast it flies off when knocked loose, units a tick (FlySpeed 30 = 48 m/s, straight along the plane normal, no gravity).</summary>
    [Export(PropertyHint.Range, "1,100,1")] public float FlySpeedUnits = 30f;
    /// <summary>The game: its touch hurts Mario even from the OTHER side of a thin net (its attack cylinder, 60 u, reaches through). Off = a Klamber on the
    /// far side of the plane cannot hurt him.</summary>
    [Export] public bool AttacksAcrossTheNet = true;

    [ExportGroup("Effects (empty = none)")]
    /// <summary>PARTICLE_MS_KIL_SMOKE: the smoke bound to the model while it flies (scale 1.5, alpha 128) -- an SmsNpcEffect scene on the body joint, kept alive
    /// through the flight. Empty = no smoke (the slot; ms_kil_smoke.jpa is not generated yet).</summary>
    [Export] public PackedScene FlySmokeFxScene;

    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>The bas tables' sounds: the run loop's steps (looping while it walks), the curl (hit_start), the knock-off (flying1), the squash (down1).</summary>
    [Export] public AudioStreamPlayer3D WalkSound, FreezeSound, FlySound, DownSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (kumo_model1.bmd's bcks)
    // Godot's glTF import strips a "_loop" suffix (and sets the clip looping): kumo_run1_loop is the player's kumo_run1, and so on
    private static readonly string[] Names = { "kumo_wait1", "kumo_run1_start", "kumo_run1", "kumo_run1_end", "kumo_turn1_start", "kumo_turn1", "kumo_turn1_end",
        "kumo_hit_start", "kumo_hit", "kumo_hit_end", "kumo_flying1", "kumo_flyingdown1", "kumo_down1" };
    private static readonly int[] Frames = { 60, 24, 30, 34, 9, 20, 49, 9, 20, 149, 20, 34, 29 };
    private static readonly bool[] Loops = { true, false, true, false, false, true, false, false, true, false, true, false, false };
    protected const int BckWait = 0, BckRunStart = 1, BckRunLoop = 2, BckRunEnd = 3, BckTurnStart = 4, BckTurnLoop = 5, BckTurnEnd = 6,
        BckHitStart = 7, BckHitLoop = 8, BckHitEnd = 9, BckFlyLoop = 10, BckFlyDown = 11, BckDown = 12;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    /// <summary>The bmd's joints: bodyjnt1 (the root), r/l leg/arm jnt1-2, eyesjnt1.</summary>
    public const string BodyJoint = "bodyjnt1";

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveSearch = NerveSpecies, NervePreWalk = NerveSpecies + 1, NerveWalk = NerveSpecies + 2, NervePostWalk = NerveSpecies + 3, NerveKumoWait = NerveSpecies + 4,
        NerveKumoFreeze = NerveSpecies + 5, NervePostFreeze = NerveSpecies + 6, NervePreFly = NerveSpecies + 7, NerveFly = NerveSpecies + 8;
    private static readonly string[] KumoNerveNames = { "Search", "PreWalk", "Walk", "PostWalk", "Wait", "Freeze", "PostFreeze", "PreFly", "Fly" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + KumoNerveNames.Length ? KumoNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveSearch;                 // mSpine->initWith(TNerveKumokunSearch)

    // ------------------------------------------------------------------------------------------------------------------ state
    private bool _hasPlane; private Vector3 _planeN = Vector3.Up; private GodotObject _planeBody;          // unk198: the attached plane (its normal, its body)
    private Quaternion _q = Quaternion.Identity, _qFrom = Quaternion.Identity, _qTo = Quaternion.Identity; private float _slerpT;   // unk19C / unk1AC / unk1BC / unk1CC
    private int _hitPlaneCounter;                                   // mHitPlaneCounter
    private int _waterTimer;                                        // unk1D0
    private bool _attacking;                                        // unk1D4
    private readonly List<int> _anmQueue = new();                   // unk1D8: the clips to follow the current one (a stack: the last is next)
    private int _dieKind = 2;                                       // unk1EC: 0 down1 (a stomp), 1 flyingdown1 (a flight's end), 2 the clip held
    private Vector3 _walkDelta;                                     // mPositionDelta as the nerve set it this tick, units
    private Vector3 _flyVel;                                        // mVelocity: the flight (the game's velocity is otherwise 0 -- its bind has no gravity)
    private Vector3 _posU;                                          // mPosition as the ticks advance it within one physics frame, units (the body moves at the frame's end)
    private bool _needAttach; private int _flyTicks;
    private struct WallPlane { public Vector3 N; public float D; }
    private WallPlane[] _graphWalls;                                // TWallAtGraph (unk1E8)
    private uint _planeMask = 5;
    private CollisionShape3D _shape; private Vector3 _shapeLocal = new Vector3(0f, 0.6f, 0f);
    private Skeleton3D _skel; private SmsNpcEffect _flyFx; private readonly Dictionary<string, BoneAttachment3D> _fxCache = new();
    private Godot.Collections.Array<Rid> _excl;
    private ulong _marioGroundFrame = ulong.MaxValue; private Vector3 _marioGroundN;
    /// <summary>For the drives.</summary>
    public bool HasPlane => _hasPlane;
    public Vector3 PlaneNormal => _hasPlane ? _planeN : Vector3.Zero;      // getPlaneNormal: zero without a plane
    public bool OnFloor => _hasPlane && _planeN.Dot(Vector3.Up) > 0.7f;    // isOnFloor
    public bool OnRoof => _hasPlane && _planeN.Dot(Vector3.Up) < -0.7f;    // isOnRoof
    public bool OnWall => _hasPlane && !OnFloor && !OnRoof;                 // isOnWall (fabricated)
    public bool Attacking => _attacking;
    public int HitPlaneCounter => _hitPlaneCounter;
    public int WaterTimer => _waterTimer;
    public int DieKind => _dieKind;
    public Quaternion Orientation => _q;
    public Vector3 Forward => _q * Vector3.Back;                            // unk19C.getZDir: the heading along the plane
    public bool Flying => IsNerve(NerveFly);
    public bool Dying => IsNerve(NerveDie);
    public string CurrentNerveName => NerveName(_cur);
    public int GraphWallCount => _graphWalls?.Length ?? 0;
    public bool SamePlaneAsMario => IsOnSamePlaneWithMario();
    public bool FindsMario => IsFindOutMario(out _);
    public bool FlySmokeEmitting => _flyFx != null && IsInstanceValid(_flyFx) && _flyFx.IsEmitting;

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _planeMask = CollisionMask;                                 // the scene's Collision > Mask names the layers it walks (the map); the body itself collides with nothing
        CollisionMask = 0;
        _shape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (_shape != null) _shapeLocal = _shape.Position;
        if (_model != null) foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        _flags150 = 0;
        if (DebugPrint) GD.Print($"[SMS kumokun] {Name}: plane mask {_planeMask}; skel {_skel?.Name}; clips {(_anim != null ? _anim.GetAnimationList().Length : 0)}");
    }

    protected override void Reset()
    {
        base.Reset();
        _bodyScale = 1f; _scale = 1f;                               // TKumokun::reset is empty: the small-enemy scaling never runs, mScaling stays 1
        _noBind = true;                                             // the bind is the species' own: no gravity, no velocity through the base
        _hasPlane = false; _planeN = Vector3.Up; _planeBody = null; _needAttach = true; _graphWalls = null;
        _q = Quaternion.Identity; _qFrom = _q; _qTo = _q; _slerpT = 0f;
        _hitPlaneCounter = 0; _waterTimer = 0; _attacking = false; _anmQueue.Clear(); _dieKind = 2; _flyTicks = 0;
        _walkDelta = Vector3.Zero; _flyVel = Vector3.Zero;
        _posU = Pos();
        StopFlyFx();
        ApplyLook();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Engine.IsEditorHint()) { _posU = Pos(); _marioGroundFrame = ulong.MaxValue; }
        base._PhysicsProcess(delta);
    }

    // ------------------------------------------------------------------------------------------------------------------ the plane
    /// <summary>initAttachPlane: a wall within 100 u at head height -> the orientation up-to-normal; else a roof within head height above -> upside down
    /// (setEulerZ(pi)); else the floor. The placed yaw turns the heading about the normal (DEVIATION: the game's heading on a fresh plane is the
    /// rotation's own; the body snaps onto the plane found -- the game leaves the placed point as it is).</summary>
    private void InitAttachPlane()
    {
        _needAttach = false;
        _hasPlane = false;
        Vector3 pos = _posU;
        float yaw = Mathf.DegToRad(_spawnYaw);
        if (CheckWallPlane(ref pos, HH, 100f, out Vector3 wn, out GodotObject wb))
        {
            _hasPlane = true; _planeN = wn; _planeBody = wb;
            _q = ArcTo(Vector3.Up, _planeN) * new Quaternion(Vector3.Up, yaw);
            pos -= _planeN * 100f;                                  // onto the plane (the probe left the point 100 u out)
            _posU = pos; GlobalPosition = pos / U;
        }
        else
        {
            pos = _posU;
            if (CheckRoofPlane(ref pos, HH, out Vector3 rn, out GodotObject rb))
            {
                _hasPlane = true; _planeN = rn; _planeBody = rb;
                _q = new Quaternion(new Vector3(0f, 0f, 1f), Mathf.Pi) * new Quaternion(Vector3.Up, yaw);
                _posU = pos; GlobalPosition = pos / U;
            }
            else
            {
                // checkFloorPlane takes a point AT or under the floor; a body placed a little above it (the editor's drag) is set down onto it when the
                // floor is within a head height (DEVIATION: the game's point never moves)
                pos = _posU;
                float gy = CheckGround(new Vector3(pos.X, pos.Y + HH, pos.Z), out Vector3 fn, out GodotObject fb) + 1f;
                if (gy > -1e8f && pos.Y - gy < HH)
                {
                    pos.Y = gy;
                    _hasPlane = true; _planeN = fn; _planeBody = fb;
                    _q = ArcTo(Vector3.Up, _planeN) * new Quaternion(Vector3.Up, yaw);
                    _posU = pos; GlobalPosition = pos / U;
                }
            }
        }
        _qFrom = _q; _qTo = _q; _slerpT = 1f;
        if (_hasPlane) InitWallAtGraph();
        if (DebugPrint) GD.Print($"[SMS kumokun] {Name}: attach -> {(OnWall ? "WALL" : OnRoof ? "ROOF" : OnFloor ? "FLOOR" : "NONE")} n {_planeN} at {_posU}; graph walls {GraphWallCount}");
    }

    /// <summary>TWallAtGraph::init(graph, mPosition, planeNormal): each edge of the closed loop becomes a plane through its first node, its normal the cross
    /// of the plane normal and the edge, turned toward the body.</summary>
    private void InitWallAtGraph()
    {
        _graphWalls = null;
        if (Graph == null) return;
        var pts = new List<Vector3>();
        foreach (Node c in Graph.GetChildren()) if (c is Node3D n3) pts.Add(n3.GlobalPosition * U);
        if (pts.Count < 3) return;
        var walls = new WallPlane[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 diff = pts[(i + 1) % pts.Count] - pts[i];
            Vector3 n = _planeN.Cross(diff);
            if (n.LengthSquared() < 1e-8f) { n = Vector3.Up; }
            n = n.Normalized();
            if (n.Dot(_posU - pts[i]) < 0f) n = -n;
            walls[i] = new WallPlane { N = n, D = n.Dot(pts[i]) };
        }
        _graphWalls = walls;
    }
    /// <summary>TWallAtGraph::checkWalls: the point kept at least r inside every fence plane.</summary>
    private bool CheckGraphWalls(ref Vector3 p, float r)
    {
        if (_graphWalls == null) return false;
        bool res = false;
        foreach (var w in _graphWalls)
        {
            float dist = w.N.Dot(p) - w.D;
            if (r - dist > 0f) { p += w.N * (r - dist); res = true; }
        }
        return res;
    }

    // ------------------------------------------------------------------------------------------------------------------ the map queries (gpMap's checks as rays)
    private bool Ray(Vector3 fromU, Vector3 toU, out Vector3 posU, out Vector3 n, out GodotObject body)
    {
        posU = toU; n = Vector3.Up; body = null;
        var space = GetWorld3D().DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(fromU / U, toU / U, _planeMask);
        bool marioKnown = _mario != null && IsInstanceValid(_mario);
        if (_excl == null || (marioKnown && _excl.Count < 2)) { _excl = new Godot.Collections.Array<Rid> { GetRid() }; if (marioKnown) _excl.Add(_mario.GetRid()); }
        q.Exclude = _excl;
        var hit = space.IntersectRay(q);
        if (hit.Count == 0) return false;
        body = hit["collider"].AsGodotObject();
        if (body is CharacterBody3D || body is RigidBody3D) return false;   // a hit actor, not the map (the game's checks see only the BG)
        posU = (Vector3)hit["position"] * U; n = (Vector3)hit["normal"];
        return true;
    }
    /// <summary>gpMap->checkGround(x, y, z): the floor at or under the point (a ray 80 m down). None: -1e9.</summary>
    private float CheckGround(Vector3 pU, out Vector3 n, out GodotObject body)
    {
        if (Ray(pU, pU + Vector3.Down * 6000f, out Vector3 hp, out n, out body)) return hp.Y;
        return -1e9f;
    }
    /// <summary>gpMap->checkRoof(x, y, z): the roof over the point (a ray 80 m up). None: 1e9.</summary>
    private float CheckRoof(Vector3 pU, out Vector3 n, out GodotObject body)
    {
        if (Ray(pU, pU + Vector3.Up * 6000f, out Vector3 hp, out n, out body)) return hp.Y;
        return 1e9f;
    }
    private static readonly Vector3[] Dirs8 = { new(1, 0, 0), new(-1, 0, 0), new(0, 0, 1), new(0, 0, -1), new(0.7071f, 0, 0.7071f), new(-0.7071f, 0, 0.7071f), new(0.7071f, 0, -0.7071f), new(-0.7071f, 0, -0.7071f) };
    /// <summary>TKumokun::checkWallPlane(pos, yOff, radius): TBGWallCheckRecord at (x, y + yOff, z) with isTouchedWallsAndMoveXZ -- the nearest wall (|normal.y|
    /// under 0.7) within radius, the point moved out to radius from it along its XZ normal. Rays in eight XZ directions and into the plane.</summary>
    private bool CheckWallPlane(ref Vector3 pU, float yOff, float radius, out Vector3 wallN, out GodotObject wallBody)
    {
        wallN = Vector3.Zero; wallBody = null;
        Vector3 c = new Vector3(pU.X, pU.Y + yOff, pU.Z);
        float bestD = float.MaxValue; Vector3 bestP = Vector3.Zero, bestN = Vector3.Zero; GodotObject bestB = null;
        int first = _hasPlane && MathF.Abs(_planeN.Y) < 0.7f ? -1 : 0;
        for (int i = first; i < Dirs8.Length; i++)
        {
            Vector3 dir = i < 0 ? -_planeN : Dirs8[i];
            if (!Ray(c, c + dir * radius, out Vector3 hp, out Vector3 hn, out GodotObject hb)) continue;
            if (MathF.Abs(hn.Y) >= 0.7f) continue;
            float d = (hp - c).Length();
            if (d < bestD) { bestD = d; bestP = hp; bestN = hn; bestB = hb; }
        }
        if (bestB == null) return false;
        wallN = bestN; wallBody = bestB;
        Vector3 nxz = new Vector3(wallN.X, 0f, wallN.Z);
        if (nxz.LengthSquared() < 1e-6f) return false;
        nxz = nxz.Normalized();
        float dist = new Vector3(pU.X - bestP.X, 0f, pU.Z - bestP.Z).Dot(nxz);
        if (dist < radius) pU += nxz * (radius - dist);
        return true;
    }
    /// <summary>TKumokun::checkFloorPlane(pos, yOff): the ground under (x, y + yOff, z) + 1; the point at or under it is set onto it.</summary>
    private bool CheckFloorPlane(ref Vector3 pU, float yOff, out Vector3 n, out GodotObject body)
    {
        float gy = CheckGround(new Vector3(pU.X, pU.Y + yOff, pU.Z), out n, out body) + 1f;
        if (pU.Y <= gy + 0.05f && gy > -1e8f) { pU.Y = gy; return true; }
        body = null; return false;
    }
    /// <summary>TKumokun::checkRoofPlane(pos, yOff): the roof over the point - 1; one 0..yOff above the point sets it onto it.</summary>
    private bool CheckRoofPlane(ref Vector3 pU, float yOff, out Vector3 n, out GodotObject body)
    {
        float ry = CheckRoof(pU, out n, out body) - 1f;
        float d = ry - pU.Y;
        if (d >= 0f && d < yOff) { pU.Y = ry; return true; }
        body = null; return false;
    }

    // ------------------------------------------------------------------------------------------------------------------ the bind
    private static Vector3 SetLength(Vector3 v, float len) { float l = v.Length(); return l > 1e-6f ? v * (len / l) : Vector3.Zero; }

    /// <summary>TKumokun::bind: the walk's delta (+ the velocity) through the plane's check for the body and for a look-ahead point, the two deltas summed; the
    /// graph's fences; the hit-plane counter. Runs after the nerve (setBehavior's slot), the result into the base's displacement.</summary>
    private void Bind()
    {
        if (IsNerve(NerveFly)) { BindOnFlying(); return; }
        if (!_hasPlane) { _hitPlaneCounter++; _lin = Vector3.Zero; return; }
        Vector3 move = _walkDelta + _flyVel;
        bool hit; Vector3 dAhead, dSelf;
        if (OnFloor)
        {
            hit = CheckOnMovingFloor(out dAhead, _posU + SetLength(move, 50f), move);
            hit |= CheckOnMovingFloor(out dSelf, _posU, move);
        }
        else if (OnRoof)
        {
            hit = CheckOnMovingRoof(out dAhead, _posU + SetLength(move, 50f), move);
            hit |= CheckOnMovingRoof(out dSelf, _posU, move);
        }
        else
        {
            hit = CheckOnMovingWall(out dAhead, _posU + SetLength(move, 0.5f * HH), move);
            hit |= CheckOnMovingWall(out dSelf, _posU, move);
        }
        // DEVIATION: the look-ahead's delta keeps only its part ALONG the plane. The game sums both deltas whole, which doubles the plane snap as well as
        // the walk -- a body off its rest offset (a roof one sits at roof - 1 u after the attach, its rest is roof - 38.5 u) overshoots by the same
        // amount and oscillates 75 u a tick for ever. The snap comes from the body's own delta alone here; the walk's doubling and the edge stops stay.
        dAhead -= _planeN * dAhead.Dot(_planeN);
        if (_graphWalls != null)
        {
            Vector3 p = _posU + dSelf + dAhead, p2 = p;
            hit |= CheckGraphWalls(ref p2, Prm.WallRadius);
            dSelf += p2 - p;
        }
        if (hit) _hitPlaneCounter++; else _hitPlaneCounter = 0;
        _lin = dSelf + dAhead;
        _posU += _lin;
    }

    /// <summary>checkOnMovingWall: the point 100 u off the wall, moved; kept above the ground by head height; a wall within 100 u of the point pulled 10 u in
    /// (none: the edge -- no move, a hit); back onto the plane.</summary>
    private bool CheckOnMovingWall(out Vector3 delta, Vector3 pos, Vector3 move)
    {
        bool result = false;
        Vector3 n = _planeN;
        Vector3 p0 = pos + n * 100f;
        Vector3 p1 = p0 + move;
        float gy = CheckGround(new Vector3(p1.X, p1.Y + HH, p1.Z), out _, out _);
        if (gy > -1e8f) { gy += 1f + HH; if (p1.Y <= gy + 0.05f) { result = true; p1.Y = gy; } }
        Vector3 q = p1 - n * 10f;
        if (!CheckWallPlane(ref q, HH, 100f, out _, out _)) { result = true; p1 = p0; }
        else { p1.X = q.X; p1.Z = q.Z; }
        p1 -= n * 100f;
        delta = p1 - pos;
        return result;
    }
    /// <summary>checkOnMovingFloor: the point moved and pulled 10 u into the floor; the ground under it: a step up over 30 u or a drop away = no move, a hit;
    /// a wall within 100 u = pushed out, a hit.</summary>
    private bool CheckOnMovingFloor(out Vector3 delta, Vector3 pos, Vector3 move)
    {
        bool result = false;
        Vector3 p1 = pos + move - _planeN * 10f;
        float gy = CheckGround(new Vector3(p1.X, p1.Y + HH, p1.Z), out _, out _);
        if (gy > -1e8f) gy += 1f;
        if (gy > -1e8f && p1.Y <= gy + 0.05f)
        {
            if (gy - p1.Y > 30f) { result = true; p1 = pos; }
            else p1.Y = gy;
        }
        else { result = true; p1 = pos; }
        Vector3 w = p1;
        if (CheckWallPlane(ref w, HH, 100f, out _, out _)) { result = true; p1.X = w.X; p1.Z = w.Z; }
        delta = p1 - pos;
        return result;
    }
    /// <summary>checkOnMovingRoof: the point half a head height toward the roof, moved, pulled 10 u in; the roof over it within head height = onto it, none =
    /// no move, a hit; a wall = pushed out, a hit; back down.</summary>
    private bool CheckOnMovingRoof(out Vector3 delta, Vector3 pos, Vector3 move)
    {
        bool result = false;
        Vector3 c = _planeN * (-HH / 2f);
        Vector3 b = pos + c;
        Vector3 p1 = b + move - _planeN * 10f;
        float ry = CheckRoof(new Vector3(p1.X, p1.Y - HH, p1.Z), out _, out _);
        if (ry < 1e8f)
        {
            ry -= 1f;
            if (p1.Y > ry - 0.05f) p1.Y = ry;
            else if (ry - p1.Y < HH) p1.Y = ry;
            else { result = true; p1 = b; }
        }
        else { result = true; p1 = b; }
        Vector3 w = p1;
        if (CheckWallPlane(ref w, HH, 100f, out _, out _)) { result = true; p1.X = w.X; p1.Z = w.Z; }
        p1 -= c;
        delta = p1 - pos;
        return result;
    }
    /// <summary>bindOnFlying: the point ahead against a roof, the floor (either: the velocity gone) and a wall within the body radius; a hit counts, over 30 the new
    /// plane is the one met (roof, floor, wall in that order).</summary>
    private void BindOnFlying()
    {
        bool hit = false;
        Vector3 p = _posU + _walkDelta + _flyVel;
        bool roof = CheckRoofPlane(ref p, HH, out Vector3 rn, out GodotObject rb);
        if (roof) { hit = true; _flyVel = Vector3.Zero; _airborne = false; }
        bool floor = CheckFloorPlane(ref p, HH, out Vector3 fn, out GodotObject fb);
        if (floor) { hit = true; _flyVel = Vector3.Zero; _airborne = false; } else _airborne = true;
        bool wall = CheckWallPlane(ref p, HH, Prm.BodyRadius, out Vector3 wn, out GodotObject wb);
        if (wall) hit = true;
        if (hit)
        {
            _hitPlaneCounter++;
            if (_hitPlaneCounter > 30)
            {
                if (roof) { _planeN = rn; _planeBody = rb; } else if (floor) { _planeN = fn; _planeBody = fb; } else { _planeN = wn; _planeBody = wb; }
                _hasPlane = true;
            }
        }
        else _hitPlaneCounter = 0;
        _lin = p - _posU;
        _posU = p;
    }

    // ------------------------------------------------------------------------------------------------------------------ the tick hooks
    /// <summary>TKumokun::moveObject's head: the water timer, updateAnimation (the clip queue), the attach; mPositionDelta.zero().</summary>
    protected override void MoveObjectPre()
    {
        if (_waterTimer > 0) _waterTimer--;
        UpdateAnimation();
        if (_needAttach || (!_hasPlane && (_tickCount % 30) == 0)) InitAttachPlane();
        _walkDelta = Vector3.Zero;
    }
    /// <summary>The bind in setBehavior's slot (after the nerve and the hit lists); the effects' upkeep.</summary>
    protected override void SetBehavior()
    {
        if (_dead) return;
        Bind();
        if (IsNerve(NerveFly))
        {
            _flyTicks++;
            if (FlySmokeFxScene != null && (_flyFx == null || !IsInstanceValid(_flyFx)) && _skel != null && (_tickCount & 3) == 0)
                _flyFx = SmsNpcEffect.Spawn(FlySmokeFxScene, _skel, BodyJoint, 1.5f, _fxCache, Name);
        }
        else if (_flyFx != null) StopFlyFx();
        if (WalkSound != null && WalkSound.Stream != null)
        {
            bool walking = IsNerve(NerveWalk) && _hitPlaneCounter == 0;
            if (walking && !WalkSound.Playing) WalkSound.Play();
            else if (!walking && WalkSound.Playing) WalkSound.Stop();
        }
    }
    private void StopFlyFx() { if (_flyFx != null && IsInstanceValid(_flyFx)) _flyFx.Stop(); _flyFx = null; }
    protected override bool IsCollidMove(Node3D other) => false;        // TKumokun::isCollidMove: nothing pushes it
    protected override bool WallHopAllowed() => false;                  // no TWalkerEnemy wall hop: the bind is its own
    protected override bool FlyBind => true;                            // the base's step: the displacement through MoveAndSlide (against nothing), no floor snap, no airborne flag

    /// <summary>calcRootMatrix: the model at the body point, 30 u into the plane on a wall or a roof, the orientation quaternion, scale 1. The collision shape
    /// (the hose's target) rides the same frame.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Vector3 off = (OnWall || OnRoof) ? -_planeN * (30f / U) : Vector3.Zero;
        Basis b = new Basis(_q) * Basis.FromScale(LookScale * _modelScale);
        if (IsInsideTree())
        {
            _model.GlobalTransform = new Transform3D(b, GlobalPosition + off);
            if (_shape != null) _shape.GlobalTransform = new Transform3D(new Basis(_q), GlobalPosition + off + _q * _shapeLocal);
        }
        else _model.Transform = new Transform3D(b, off);
    }

    // ------------------------------------------------------------------------------------------------------------------ the clips (changeBck / the stack)
    /// <summary>changeBck: the clip at SMSGetAnmFrameRate x 0.5 -- doubled (the full rate) while attacking or in PreFly, never in Die.</summary>
    private void ChangeBck(int bck)
    {
        SetBck(bck);
        bool fast = !IsNerve(NerveDie) && (_attacking || IsNerve(NervePreFly));
        if (_anim != null) _anim.SpeedScale *= 0.5f * (fast ? 2f : 1f);
    }
    /// <summary>pushNextAnm(name, start): onto the queue; start = also now.</summary>
    private void PushNextAnm(int bck, bool start) { _anmQueue.Add(bck); if (start) ChangeBck(bck); }
    /// <summary>updateAnimation: the current clip ended with a queue -> the top popped; it was the current one and more remain -> the next starts; else the queue
    /// is dropped.</summary>
    private void UpdateAnimation()
    {
        if (_anmQueue.Count == 0 || !BckEnded()) return;
        int top = _anmQueue[^1]; _anmQueue.RemoveAt(_anmQueue.Count - 1);
        if (IsBck(top) && _anmQueue.Count >= 1) ChangeBck(_anmQueue[^1]);
        else _anmQueue.Clear();
    }
    private bool CheckSerialAnmEnd() => _anmQueue.Count == 0 && BckEnded();

    // ------------------------------------------------------------------------------------------------------------------ the heading (the quaternion)
    /// <summary>TQuat4::setRotate(from, to): the shortest arc; antiparallel = half a turn about Z (the roof's setEulerZ(pi)).</summary>
    private static Quaternion ArcTo(Vector3 from, Vector3 to)
    {
        from = from.Normalized(); to = to.Normalized();
        float d = from.Dot(to);
        if (d > 0.99999f) return Quaternion.Identity;
        if (d < -0.99999f) { Vector3 axis = from.Cross(Vector3.Back); if (axis.LengthSquared() < 1e-6f) axis = from.Cross(Vector3.Right); return new Quaternion(axis.Normalized(), Mathf.Pi); }
        return new Quaternion(from, to).Normalized();
    }
    /// <summary>rotateGoalDirToLocal: the point into the body's frame (the inverse quaternion on the offset).</summary>
    private Vector3 RotateGoalDirToLocal(Vector3 worldU) => _q.Inverse() * (worldU - _posU);
    /// <summary>decideTarget: Mario found on the plane -> toward him (attacking), else a random turn.</summary>
    private void DecideTarget()
    {
        if (IsFindOutMario(out Vector3 local)) { DecideTargetAtDir(local); _attacking = true; }
        else DecideTargetAtRandom();
    }
    /// <summary>decideTargetAtDir: the local direction flattened onto the plane; the target orientation = the current turned from +Z to it.</summary>
    private void DecideTargetAtDir(Vector3 local)
    {
        Vector3 d = new Vector3(local.X, 0f, local.Z);
        if (d.LengthSquared() < 1e-8f) d = Vector3.Back; else d = d.Normalized();
        Quaternion p = d.Dot(Vector3.Back) < -0.99999f ? new Quaternion(Vector3.Up, Mathf.Pi) : ArcTo(Vector3.Back, d);
        _qFrom = _q; _qTo = (_q * p).Normalized(); _slerpT = 0f;
    }
    /// <summary>decideTargetAtRandom: a turn of (rand + 0.5) x pi about the normal; attacking off.</summary>
    private void DecideTargetAtRandom()
    {
        Quaternion p = new Quaternion(Vector3.Up, (Rand() + 0.5f) * Mathf.Pi);
        _qFrom = _q; _qTo = (_q * p).Normalized(); _slerpT = 0f;
        _attacking = false;
    }
    /// <summary>doAdjustTarget: the slerp advanced by TorqueY (x 3 attacking) a tick; true once it reaches the target.</summary>
    private bool DoAdjustTarget()
    {
        bool done = false;
        _slerpT += Prm.TorqueY * (_attacking ? 3f : 1f);
        if (_slerpT >= 1f) { _slerpT = 1f; done = true; }
        _q = _qFrom.Slerp(_qTo, _slerpT).Normalized();
        return done;
    }
    /// <summary>doWalk: forward (the quaternion's Z) x MarchSpeed, or AttackSpeed when attacking, as this tick's delta; true once the counter passed 30.</summary>
    private bool DoWalk()
    {
        float speed = (_attacking ? Prm.AttackSpeed : Prm.MarchSpeed) * SpeedScale;
        _walkDelta = Forward * speed;
        return _hitPlaneCounter > 30;
    }
    /// <summary>prepareFly: the velocity along the normal at FlySpeed, the body a head height off the plane, the counter reset.</summary>
    private void PrepareFly()
    {
        _flyVel = SetLength(_planeN, FlySpeedUnits);
        _hitPlaneCounter = 0;
        Vector3 inc = _planeN * HH;
        _posU += inc; GlobalPosition += inc / U;
        _flyTicks = 0;
        FlySound?.Play();
        DoFly();
    }
    /// <summary>doFly: the counter over 30, or any plane met after the first 30 ticks -> the orientation up-to-the-plane; true (the flight ends).</summary>
    private bool DoFly()
    {
        if (_hitPlaneCounter > 30 || (_hitPlaneCounter > 0 && _time > 30))
        {
            if (_hasPlane) _q = ArcTo(Vector3.Up, _planeN);
            _qFrom = _q; _qTo = _q; _slerpT = 1f;
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------------------------------------------------------ Mario on the plane
    private static bool IsSameNormal(Vector3 a, Vector3 b) => a.LengthSquared() > 0.5f && b.LengthSquared() > 0.5f && a.Dot(b) >= 0.99f;
    /// <summary>SMS_GetMarioRfPlane()->getNormal(): the roof he hangs (its normal.y is what his roof code keeps).</summary>
    private Vector3 MarioRoofNormal() => _mario != null && _mario.SmsRoofNormalY < 0f ? new Vector3(0f, _mario.SmsRoofNormalY, 0f).Normalized() : Vector3.Zero;
    /// <summary>SMS_GetMarioGrPlane()->getNormal(): the floor under his feet (a ray 60 m down, once a physics frame).</summary>
    private Vector3 MarioGroundNormal()
    {
        if (_mario == null) return Vector3.Zero;
        ulong f = Engine.GetPhysicsFrames();
        if (_marioGroundFrame == f) return _marioGroundN;
        _marioGroundFrame = f;
        Vector3 feet = _mario.SmsFeetWorld() * U;
        _marioGroundN = Ray(feet + Vector3.Up * 30f, feet + Vector3.Down * 4500f, out _, out Vector3 n, out _) ? n : Vector3.Zero;
        return _marioGroundN;
    }
    /// <summary>SMS_GetMarioPos(): his mPosition, the feet.</summary>
    private Vector3 MarioFeetU() => _mario != null ? _mario.SmsFeetWorld() * U : _posU;
    /// <summary>isOnSamePlaneWithMario: on a wall he is fencing of the same normal; under a roof he is hanging of the same normal; on a floor of the same normal
    /// as the one under him.</summary>
    private bool IsOnSamePlaneWithMario()
    {
        if (!_hasPlane || _mario == null) return false;
        if (OnWall) return _mario.SmsIsFencing && IsSameNormal(_mario.SmsFenceNormal, _planeN);
        if (OnRoof) return _mario.SmsIsRoofing && IsSameNormal(MarioRoofNormal(), _planeN);
        return IsSameNormal(MarioGroundNormal(), _planeN);
    }
    /// <summary>isFindOutMario: on the same plane, within SearchRange, within head height of the plane (x 3 under a roof); his direction in the body's frame.</summary>
    private bool IsFindOutMario(out Vector3 local)
    {
        local = Vector3.Zero;
        if (!IsOnSamePlaneWithMario()) return false;
        if (_mario.SmsShineDemoRunning) return false;
        local = RotateGoalDirToLocal(MarioFeetU());
        float h = HH * (OnRoof ? 3f : 1f);
        return MathF.Abs(local.Y) < h && local.LengthSquared() < SearchRangeUnits * SearchRangeUnits;
    }
    /// <summary>isAttack: the Search / Walk / Wait / PreWalk / PostWalk nerves -- and never when Mario is above a roof-hanging one.</summary>
    private bool IsAttack()
    {
        if (OnRoof && _mario != null && MarioFeetU().Y + 5f > _posU.Y) return false;
        return IsNerve(NerveWalk) || IsNerve(NerveSearch) || IsNerve(NerveKumoWait) || IsNerve(NervePreWalk) || IsNerve(NervePostWalk);
    }
    /// <summary>doKeepDistance: on Mario's plane and not attacking (curled, flying, dying) -> his keepDistance pushes him off the body.</summary>
    private bool DoKeepDistance() => IsOnSamePlaneWithMario() && !IsAttack();

    // ------------------------------------------------------------------------------------------------------------------ hits
    /// <summary>TKumokun::attackToMario: the ATTACK message only while attacking (no PostAttack hop).</summary>
    protected override void AttackToMario() { if (IsAttack()) SendAttackMsgToMario(); }

    /// <summary>The collision lists for ACTOR_TYPE_KUMOKUN (MarioCheckCol.cpp 341): hitNormal (a HIP_DROP / TRAMPLE while he falls onto it; the hose), doKeepDistance
    /// -> keepDistance, hitHipDrop, then the FENCE_PUNCH (frames 5..9) / KICK_ROOF (frames 9..13) PUNCH; the other way our attack cylinder on his damage
    /// cylinder -> attackToMario. Mario's cylinder stands on his feet (the game's mPosition).</summary>
    protected override void HitCheckMario()
    {
        if (_mario == null || _cur == NerveDie || !GetCollisionLayerValue(1)) return;
        Vector3 g = _posU, m = MarioFeetU();
        bool onGround = _mario.IsOnFloor();
        bool rising = !onGround && _mario.Velocity.Y > 0f;
        bool fencePunch = _mario.SmsFencePunchActive, roofKick = _mario.SmsRoofKickActive;
        float ar = fencePunch ? FencePunchRadius : roofKick ? RoofKickRadius : onGround ? SmsParams.Mario.HoldRadius : (rising ? SmsParams.Mario.PushupRadius : SmsParams.Mario.TrampleRadius);
        float ah = fencePunch ? FencePunchHeight : roofKick ? RoofKickHeight : onGround ? SmsParams.Mario.AttackHeight : (rising ? SmsParams.Mario.PushupHeight : SmsParams.Mario.AttackHeight);
        bool marioHits = CheckDistance(m, ar, ah, g, Prm.DamageRadius, Prm.DamageHeight);
        if (marioHits)
        {
            bool falling = !onGround && _mario.Velocity.Y < 0f && g.Y < m.Y;
            var st = _mario.stateOfMario;
            bool noTrample = st == Mario.MarioState.diving || st == Mario.MarioState.singleJumpDive || st == Mario.MarioState.doubleJumpDive || st == Mario.MarioState.tripleJumpDive
                || st == Mario.MarioState.bellySlidingFromDive || st == Mario.MarioState.swimming || st == Mario.MarioState.ledgeHang || st == Mario.MarioState.ledgeClimb
                || st == Mario.MarioState.treeGrab || st == Mario.MarioState.treeClimb || st == Mario.MarioState.hurt || st == Mario.MarioState.dead
                || st == Mario.MarioState.fence || st == Mario.MarioState.roofHang;
            if (falling && st == Mario.MarioState.groundPoundFalling) { BehaveHitHipdrop(); return; }               // hitNormal: HIP_DROP, then out either way
            if (st == Mario.MarioState.groundPoundLanding && g.Y < m.Y + 2f) { BehaveHitHipdrop(); return; }         // hitHipDrop: the landing frames (mStatusState 2) with the body under him (+2 u: a floor one perches 1 u up)
            if (falling && !noTrample && BehaveHitTrample()) { _mario.StompBounce(); return; }                       // trampleExec: the bounce only when the message is taken
            if (_mario.SmsHoseSpraying) SmsSprayedByWater(m + Vector3.Up * (80f / U));
            if (_dead || _cur == NerveDie) return;
            if ((fencePunch || roofKick) && BehaveHitPunch()) return;
        }
        if (DoKeepDistance() && CheckDistance(m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight, g, Prm.DamageRadius, Prm.DamageHeight))
            _mario.SmsKeepDistance(g, Prm.DamageRadius);                                                            // keepDistance(*this, 0)
        bool weHit = CheckDistance(g, Prm.AttackRadius, Prm.AttackHeight, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight);
        if (!weHit) return;
        bool otherSide = OnWall && (m - g).Dot(_planeN) < -5f;
        if (AttacksAcrossTheNet || !otherSide) AttackToMario();
    }
    /// <summary>behaveHitPunch: from a fence or a roof of ANOTHER normal than this body's plane -> PreFly; the same plane (or neither) -> nothing.</summary>
    private bool BehaveHitPunch()
    {
        bool fencing = _mario.SmsIsFencing, roofing = _mario.SmsIsRoofing;
        if ((!fencing || IsSameNormal(_mario.SmsFenceNormal, PlaneNormal)) && (!roofing || IsSameNormal(MarioRoofNormal(), PlaneNormal))) return false;
        ResetSpine(); SetNext(NervePreFly);
        if (DebugPrint) GD.Print($"[SMS kumokun] {Name}: PUNCH from the other side -> PreFly");
        return true;
    }
    /// <summary>behaveHitTrample: on a floor -> Die with down1.</summary>
    private bool BehaveHitTrample()
    {
        if (!OnFloor) return false;
        _dieKind = 0; Kill();
        if (DebugPrint) GD.Print($"[SMS kumokun] {Name}: TRAMPLE on the floor -> Die (down1)");
        return true;
    }
    /// <summary>behaveHitHipdrop: on a floor -> Die with down1; under a roof -> PreFly (knocked off the ceiling).</summary>
    private bool BehaveHitHipdrop()
    {
        if (IsNerve(NervePreFly) || IsNerve(NerveFly)) return true;   // his landing frames send HIP_DROP on every list; one knock-off is enough (the game restarts hit_start each time)
        if (OnFloor) { _dieKind = 0; Kill(); if (DebugPrint) GD.Print($"[SMS kumokun] {Name}: HIP_DROP on the floor -> Die (down1)"); return true; }
        if (OnRoof) { ResetSpine(); SetNext(NervePreFly); if (DebugPrint) GD.Print($"[SMS kumokun] {Name}: HIP_DROP under the roof -> PreFly"); return true; }
        return false;
    }
    /// <summary>behaveToWater -> behaveHitWater: with the 30-tick timer down, the timer, the spine reset into Freeze.</summary>
    protected override void BehaveToWater()
    {
        if (_waterTimer > 0 || _dead || IsNerve(NerveDie) || IsNerve(NerveFly) || IsNerve(NervePreFly)) return;   // (the game freezes a flying one too: it would hang in the air -- not here)
        _waterTimer = 30;
        ResetSpine(); SetNext(NerveKumoFreeze);
    }
    /// <summary>TSmallEnemy::kill from outside the species' own hits (a thrown fruit, Yoshi, a drive): the game would hold the current clip (unk1EC's initial 2);
    /// here it plays the squash -- a frozen pose for three seconds reads as a bug (DEVIATION, logged).</summary>
    public override void Kill()
    {
        if (_dieKind == 2) _dieKind = 0;
        base.Kill();
    }
    /// <summary>setDeadAnm per unk1EC: down1 (a stomp), flyingdown1 (a flight's end), or the current clip held at frame 0.</summary>
    protected override void SetDeadAnm()
    {
        switch (_dieKind)
        {
            case 0: ChangeBck(BckDown); DownSound?.Play(); break;
            case 1: ChangeBck(BckFlyDown); break;
            default: SeekBck(0f); SetBckPaused(true); break;
        }
        StopFlyFx();
    }
    /// <summary>genRandomItem's ground: the floor under the body (the base's ground ray sees nothing with the mask zeroed).</summary>
    protected override void GenRandomItem()
    {
        float gy = CheckGround(_posU, out _, out _);
        _groundY = gy > -1e8f ? gy : _posU.Y;
        base.GenRandomItem();
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveSearch:                                    // TNerveKumokunSearch
                if (_time == 0)
                {
                    DecideTarget();
                    if (_attacking) PushNextAnm(BckTurnLoop, true);
                    else { PushNextAnm(BckTurnLoop, false); PushNextAnm(BckTurnStart, true); }
                    if (DebugPrint) GD.Print($"[SMS kumokun] {Name}: Search {(_attacking ? "toward Mario" : "at random")}");
                }
                if (DoAdjustTarget() && BckEnded())
                {
                    if (IsBck(BckTurnLoop)) ChangeBck(BckTurnEnd);
                    else { PushAfterCurrent(NervePreWalk); return true; }
                }
                return false;

            case NervePreWalk:                                   // TNerveKumokunPreWalk
                if (_time == 0) ChangeBck(BckRunStart);
                if (CheckSerialAnmEnd()) { PushAfterCurrent(NerveWalk); return true; }
                return false;

            case NerveWalk:                                      // TNerveKumokunWalk
                if (_time == 0) { _hitPlaneCounter = 0; ChangeBck(BckRunLoop); }
                if ((DoWalk() || _time > Prm.MarchTimer) && BckEnded()) { PushAfterCurrent(NervePostWalk); return true; }
                return false;

            case NervePostWalk:                                  // TNerveKumokunPostWalk
                if (_time == 0) ChangeBck(BckRunEnd);
                if (BckEnded()) { PushAfterCurrent(NerveKumoWait); return true; }
                return false;

            case NerveKumoWait:                                  // TNerveKumokunWait
                if (_time == 0) { _attacking = false; ChangeBck(BckWait); }
                if (IsFindOutMario(out _)) { PushAfterCurrent(NerveSearch); return true; }
                if (BckEnded() && _time > Prm.WaitTimer) { PushAfterCurrent(NerveSearch); return true; }
                return false;

            case NerveKumoFreeze:                                // TNerveKumokunFreeze
                if (_time == 0)
                {
                    _anmQueue.Clear();
                    if (IsBck(BckHitEnd) || IsBck(BckHitStart)) ChangeBck(BckHitLoop);
                    else if (!IsBck(BckHitLoop)) { PushNextAnm(BckHitLoop, false); PushNextAnm(BckHitStart, true); FreezeSound?.Play(); }
                }
                if (_waterTimer <= 0) { PushAfterCurrent(NervePostFreeze); return true; }
                return false;

            case NervePostFreeze:                                // TNerveKumokunPostFreeze
                if (_time == 0) ChangeBck(BckHitEnd);
                if (BckEnded()) { PushAfterCurrent(NerveSearch); return true; }
                return false;

            case NervePreFly:                                    // TNerveKumokunPreFly
                if (_time == 0) { _anmQueue.Clear(); PushNextAnm(BckHitStart, true); }
                if (CheckSerialAnmEnd()) { PushAfterCurrent(NerveFly); return true; }
                return false;

            case NerveFly:                                       // TNerveKumokunFly
                if (_time == 0) { ChangeBck(BckFlyLoop); PrepareFly(); }
                if (DoFly() || _time > 1800)
                {
                    PushAfterCurrent(NerveDie); _dieKind = 1;
                    if (DebugPrint) GD.Print($"[SMS kumokun] {Name}: the flight ends after {_time} ticks (counter {_hitPlaneCounter}) at {_posU / U} (body {GlobalPosition}, vel {_flyVel}) -> Die (flyingdown1)");
                    return true;
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
