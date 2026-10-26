using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The game's SMALL-ENEMY BASE (SMS pass 125, feature/enemy-base): TSmallEnemy (Enemy/smallEnemy.cpp) + TWalkerEnemy (walkerEnemy.cpp) on
/// TSpineEnemy's walk / sight helpers (enemy.cpp) and TWalker's bind (walker.cpp), with the Spine nerve stack (Strategic/Spine.hpp). Most of
/// the game's ground enemies -- the Stus (hamukuri.cpp, every kind), the Cataquack, the Pokey's head, the Bob-omb, the Klamber, the Gooble
/// (namekuri.cpp; Gooble.cs, a species here since pass 125e) -- are a thin species on top: their .prm numbers, their bck table and the
/// anim setters, a few nerves of their own. ONE TICK = one 120 Hz movement tick (TMarDirector::direct runs every live actor's CUE_MOVE each
/// tick; the .prm speeds are UNITS A TICK, SmsUnits.TickSpeedToMps); the physics step is the ticks' displacement through MoveAndSlide
/// (the world's floors and walls as TWalker's bind: gravity 0.15 a tick, a terminal 40, the velocity ZEROED on landing, no drag).
///
/// THE SHARED NERVES (walkerEnemy.cpp / smallEnemy.cpp): Generate (the set anim, then Wander) -> GraphWander (walk the graph "main"; no
/// graph here: random points within WanderRadius -- DEVIATION, his levels carry no graph; a random next node on arrival) -> isFindMario
/// (not airborne, Mario not in water, within SearchHeight and MsIsInSight(SearchLength / Angle / Aware)) -> behaveToFindMario: Attack
/// (the run anim toward him at walkBehavior(2, x3): march x 3, turn x 3; gives up past GiveUpLength, past GiveUpHeight, or when he goes
/// into water) then Wander, OR with unk150 & 2 the Escape (away from him at x2 until past GiveUpLength). Touching Mario = attackToMario:
/// SMS_SendMessageToMario(ATTACK) (his damage) and, unless jumping / dying / generating, the hop (SmallEnemyJump: vy = JumpForce x scale)
/// then PostAttack (the wait anim for AttackWait ticks). A wall while walking = the hop forward at MarchSpeedHigh (TWalkerEnemy::moveObject).
/// Mario's attack cylinder on the damage cylinder with him falling = TRAMPLE (his StompBounce) / HIP_DROP in a ground pound -> isHitValid ->
/// kill -> Die: decHitPoints; at 0 the dead anim + the dead effect, the body gone once the anim ended + the manager's 60 ticks (or at 360),
/// genRandomItem (the DISAP puffs; the event coin only for a placed coin id; generateItem's ACTOR_TYPE_ITEM_UNK8 is a type nothing in the
/// shipped game carries -- the real game drops NOTHING, the DropTable is the deviation knob). SPRAYED_BY_WATER: the WATHIT mark every hit; once
/// per 30-tick cooldown decHpByWater (the hit's attack, at least 1) + behaveToWater (the base: Freeze -- the freeze anim for FreezeWait).
/// Other small enemies on the ground push each other apart at march x 3 (isCollidMove). The game stopped (a talk, the pause, a dialog):
/// the tree's pause.
///
/// A species overrides: <see cref="P"/> (its .prm), the bck table (<see cref="BckNames"/>, <see cref="BckFrames"/>, <see cref="BckLoops"/>),
/// the anim setters, <see cref="ExecuteNerve"/> for its own nerves (ids from <see cref="NerveSpecies"/> up), and the hooks named after the
/// game's virtuals (SetBehavior, BehaveToWater, AttackToMario, IsFindMario, WalkBehavior, IsHitValid, IsCollidMove, GravityY, ...).
/// </summary>
public abstract partial class SmsSmallEnemy : CharacterBody3D, ISmsSprayTarget
{
    // ------------------------------------------------------------------------------------------------------------------ the .prm
    /// <summary>TSmallEnemyParams + TWalkerEnemyParams (the defaults are the decomp's PARAM_INIT values; a species loads its .prm over them).</summary>
    public class Params
    {
        public float HeadHeight = 120f, BodyRadius = 5f, WallRadius = 80f, ClipRadius = 100f;
        public int HitPointMax = 1, InstanceNum = 6, ActiveEnemyNum = 6;
        public float Gravity = 0.15f;                            // TLiveActor's mGravity (LiveActor.cpp 52)
        public float JumpForce = 10f;
        public float SearchLength = 800f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f;
        public float GiveUpLength = 2000f, GiveUpHeight = 650f;
        public int AttackWait = 120, FreezeWait = 300;
        public float DamageRadius = 80f, DamageHeight = 60f, AttackRadius = 80f, AttackHeight = 60f;
        public float TurnSpeedLow = 3f, TurnSpeedHigh = 5f;
        public float BodyScaleLow = 0.108f, BodyScaleHigh = 0.11f;
        public float GenItemRate = 50f, GenEggRate = 10f;
        public int WaitTime = 100;
        public float StampRange = 5f;
        public float ZigzagCycle = 1000f, ZigzagAngle = 30f;
        public float MarchSpeedLow = 0.8f, MarchSpeedHigh = 1.5f;
    }
    protected abstract Params P { get; }

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>An optional wander graph (gpConductor->getGraphByName("main")): its children's positions are the nodes. None: random points
    /// within <see cref="WanderRadiusUnits"/> of the placed point (DEVIATION: his levels carry no graph).</summary>
    [Export] public Node3D Graph;
    [Export] public float WanderRadiusUnits = 600f;
    /// <summary>TLauncher's period (launcher.prm mSLLaunchPeriod 200): a dead one re-launches from its placed point after this many ticks
    /// (DEVIATION: the game pools them behind launchers / the manager; a placed one here is its own). Below 0 = freed when dead.</summary>
    [Export] public int RelaunchTicks = 200;
    /// <summary>Set before the body enters the tree by a spawner (SmsEnemySpawner / SmsEnemyOneShotSpawner / SmsEnemyLauncher, pass 126): the body is
    /// one of a pool -- TEnemyManager's objects. Dead, it stays dead and hidden until the pool launches it again (getFarOutEnemy -> resetSRTV); it is
    /// never its own launcher and never freed.</summary>
    public bool Pooled;
    /// <summary>What this enemy drops when it dies (pass 125d; a DEVIATION -- the retail game's generateItem asks for an item type no stage carries, so
    /// it drops nothing): an SmsDropTable (New in the inspector, or a .tres such as assets/Sms/Enemy/Drops/StuDrops.tres), rolled once per death
    /// at the game's hook (genRandomItem: never for a wall crash or a body already dead), spawned at (x, mGroundHeight, z) with the game's coin pop.
    /// Unset = nothing drops.</summary>
    [Export] public SmsDropTable DropTable;
    /// <summary>TSmallEnemy::setAfterDeadEffect: PARTICLE_MS_ENM_DISAP_A + _B at the body (scaled) -- assets/Sms/Fx/EnemyDisap.tscn (an SmsFxBurst: the
    /// blue-gray smoke + the mint glow streaks; pass 125c), any one-shot GPUParticles3D, or nothing.</summary>
    [Export] public PackedScene DeathFxScene;
    /// <summary>MSD_SE_EN_COMMON_SMOKE (the death puff) and the species' own slots below it. Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D DeathSound;
    /// <summary>The Model node's scale AS PLACED in the species scene (the glb's units -> metres: 1 / 74.44 = 0.013434 for a raw FinModelUtility glb in the
    /// game's units, 1 for a glb exported in metres). Captured in _Ready and multiplied into the body scale -- the editor shows the real size (pass 125b).</summary>
    protected float _modelScale = 1f;
    [Export] public bool DebugPrint;

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveNone = 0, NerveGenerate = 1, NerveGraphWander = 2, NerveAttack = 3, NervePostAttack = 4, NerveEscape = 5,
        NerveTraceMario = 6, NerveDie = 7, NerveFreeze = 8, NerveJump = 9, NerveHitWaterJump = 10, NerveWait = 11;
    /// <summary>A species' own nerves start here.</summary>
    protected const int NerveSpecies = 100;
    private static readonly string[] BaseNerveNames = { "None", "Generate", "GraphWander", "Attack", "PostAttack", "Escape", "TraceMario", "Die", "Freeze", "Jump", "HitWaterJump", "Wait" };
    protected virtual string NerveName(int n) => n >= 0 && n < BaseNerveNames.Length ? BaseNerveNames[n] : $"Nerve{n}";

    protected readonly List<int> _stack = new();
    protected int _cur = NerveNone, _prev = NerveNone;
    protected int _time;                                        // TSpineBase::mTime
    protected void PushNerve(int n) { if (_cur != NerveNone) { _prev = _cur; _stack.Add(_cur); } _time = 0; _cur = n; }
    protected void PushAfterCurrent(int n) => _stack.Add(n);
    protected void SetNext(int n) { if (_cur != NerveNone) _prev = _cur; _time = 0; _cur = n; }
    protected void ResetSpine() { _stack.Clear(); }
    protected bool IsNerve(int n) => _cur == n;
    private int PopNerve() { if (_stack.Count == 0) return NerveNone; int n = _stack[^1]; _stack.RemoveAt(_stack.Count - 1); return n; }
    private void SpineUpdate()
    {
        if (_cur == NerveNone) { int n = PopNerve(); if (n != NerveNone) { _cur = n; _time = 0; } }
        if (_cur == NerveNone) return;
        bool done = ExecuteNerve(_cur);
        _time++;
        if (done) { _prev = _cur; _cur = NerveNone; }
    }
    /// <summary>The default nerve a reset lands in (mSpine->getDefault()): Generate.</summary>
    protected virtual int DefaultNerve => NerveGenerate;

    // ------------------------------------------------------------------------------------------------------------------ state
    protected const float U = SmsUnits.UnitsPerMetre;
    protected Node3D _model; protected AnimationPlayer _anim; protected Mario _mario;
    protected int _bck = -1; private float _bckRate = 60f; private float _accum;
    protected Vector3 _vel;                                     // mVelocity, units a tick (the jump / fall / a launch)
    protected Vector3 _lin;                                     // mPositionDelta's walk part, units a tick, rebuilt each tick
    private Vector3 _disp;                                      // the ticks' displacement this physics frame, units
    protected float _yaw;                                       // mRotation.y, degrees: 0 = +Z, growing toward +X (MsGetRotFromZaxisY)
    protected bool _airborne, _onWall, _noBind, _dead, _melt, _noReward, _hitValid, _attackerKilled;   // LIVE_FLAG_AIRBORNE / TWalker unk1C / UNK10 / DEAD / MELT_ON_DEATH / UNK20000 / unk198-like / unk184
    protected Vector3 _floorNormal = Vector3.Up;
    protected readonly List<Node3D> _touching = new();          // the other live actors in contact this frame (isCollidMove's push)
    protected float _bodyScale = 1f, _scale = 1f, _marchSpeed = 1f, _turnSpeed = 1f;
    protected int _hp = 1, _sprayCooldown, _relaunch, _wanderTicks, _tickCount;
    protected bool _goalIsMario; protected Vector3 _goalPoint;  // TPathNode: the actor, or a point (units)
    protected Vector3 _spawnPos; protected float _spawnYaw;
    protected Random _rng;
    protected float _groundY;                                   // mGroundHeight, units
    protected uint _flags150;                                   // unk150 (the species' behaviour bits: 2 = escapes, 0x20 = stamps goop at the death)
    protected int _wallHopCooldown;

    // ------------------------------------------------------------------------------------------------------------------ the bck table
    protected abstract string[] BckNames { get; }
    protected abstract int[] BckFrames { get; }
    protected abstract bool[] BckLoops { get; }

    // ------------------------------------------------------------------------------------------------------------------ for the drives
    public string NerveNameNow => _cur == NerveNone ? NerveName(_prev) + "(done)" : NerveName(_cur);
    /// <summary>The nerve before the current one (mSpine's previous): a drive's way to see a nerve that lasted less than a physics frame.</summary>
    public string PrevNerveName => NerveName(_prev);
    public int NerveTime => _time;
    public string BckName => _bck >= 0 && _bck < BckNames.Length ? BckNames[_bck] : "-";
    public int BckIndex => _bck;
    public bool IsDead => _dead;
    public bool Airborne => _airborne;
    public float YawDeg => _yaw;
    public Vector3 VelUnits => _vel;
    public int Hp => _hp;
    public float BodyScale => _bodyScale;
    public uint Flags150 => _flags150;

    // ------------------------------------------------------------------------------------------------------------------ setup
    public override void _Ready()
    {
        AddToGroup("sms_enemy");                                // pass 116: gpConductor->killEnemiesWithin (the shine get)
        _model = GetNodeOrNull<Node3D>("Model");
        _anim = GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_model != null) _modelScale = _model.Scale.X;          // the scene's own scale on the glb (the editor's size is the game's)
        _rng = new Random(GetInstanceId().GetHashCode());
        Init();                                                 // before the clip check: a species may build its AnimationPlayer (the goop Stu borrows the Stu's clips) or dress the model
        _anim ??= GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_model == null || _anim == null)
            GD.PushWarning($"[SMS enemy] {GetPath()}: the scene needs Model (the glb) with Model/AnimationPlayer");
        if (_anim != null)
            foreach (string n in BckNames)
                if (!string.IsNullOrEmpty(n) && !_anim.HasAnimation(n))     // an empty slot = a clip this kind has not (the winged Stu's 4-clip table on the Stu's 15 slots)
                    GD.PushWarning($"[SMS enemy] {GetPath()}: the model has no '{n}' clip (the game's bck table: {string.Join(", ", BckNames)})");
        _spawnPos = GlobalPosition * U;
        _spawnYaw = Mathf.RadToDeg(Rotation.Y);
        _yaw = _spawnYaw;
        MotionMode = MotionModeEnum.Grounded;
        CallDeferred(nameof(SetupCollisionExceptions));         // the game pushes Mario and enemies apart by messages, not bodies
        SmsWaterHitMarks.Ensure(this);                          // pass 99: the water-hit mark pool loads with the level
        Reset();
    }

    /// <summary>TSmallEnemy::init's species tail (actor type, flags, hit filters, the model's dressing) -- after the model is found, before the clip
    /// check and the first reset. <see cref="_rng"/> is ready; <see cref="_mario"/> is not.</summary>
    protected virtual void Init() { }

    /// <summary>resetSRTV for a spawner (or a drive) that places the body AFTER adding it to the tree: the placed point and yaw become the launcher's, the body resets there.</summary>
    public void SmsPlace(Vector3 globalPosition, float yawDeg)
    {
        GlobalPosition = globalPosition;
        Rotation = new Vector3(0f, Mathf.DegToRad(yawDeg), 0f);
        _spawnPos = globalPosition * U; _spawnYaw = yawDeg; _yaw = yawDeg;
        Reset();
    }

    private void SetupCollisionExceptions()
    {
        foreach (Node n in GetTree().GetNodesInGroup("player"))
            if (n is PhysicsBody3D body) AddCollisionExceptionWith(body);
    }

    /// <summary>TWalkerEnemy::reset / TSmallEnemy::reset: the random turn speed, body scale and march speed, the hit points, the flags, the spine
    /// at its default, the goal Mario, the hit cylinders at the scale.</summary>
    protected virtual void Reset()
    {
        _turnSpeed = Lerp(P.TurnSpeedLow, P.TurnSpeedHigh, Rand());
        _bodyScale = Lerp(P.BodyScaleLow, P.BodyScaleHigh, Rand());
        _marchSpeed = Lerp(P.MarchSpeedLow, P.MarchSpeedHigh, Rand());
        _scale = _bodyScale;
        _hp = P.HitPointMax;
        _dead = false; _noReward = false; _melt = false; _noBind = false; _hitValid = false; _attackerKilled = false;
        _sprayCooldown = 0; _relaunch = 0; _wanderTicks = 0; _wallHopCooldown = 0;
        _vel = Vector3.Zero; _lin = Vector3.Zero; _airborne = false;
        _stack.Clear(); _cur = NerveNone; _prev = NerveNone; _time = 0;
        PushAfterCurrent(DefaultNerve);                         // mSpine->pushAfterCurrent(mSpine->getDefault())
        _goalIsMario = true;                                    // setGoalPath(TPathNode(gpMarioAddress))
        if (_model != null) _model.Visible = true;
        SetCollisionLayerValue(1, true);
        ApplyLook();
    }

    // ------------------------------------------------------------------------------------------------------------------ the tick
    public override void _PhysicsProcess(double delta)
    {
        if (_mario == null || !IsInstanceValid(_mario))
        {
            _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
            if (_mario != null) AddCollisionExceptionWith(_mario);
        }
        int n = SmsUnits.TicksFor(delta, ref _accum);
        _disp = Vector3.Zero;
        for (int i = 0; i < n; i++) Tick();
        if (_dead) return;
        if (FlyBind)
        {
            // THaneHamuKuri::bind is the species' own (its SetBehavior): the step moves the body by the ticks' XZ displacement, reads the walls and
            // the contacts, and leaves the airborne flag, the height and the floor normal to the species
            FloorSnapLength = 0f;
            Velocity = _disp / U / (float)delta;
            MoveAndSlide();
            _onWall = false;
            _touching.Clear();
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                var col = GetSlideCollision(i);
                GodotObject other = col.GetCollider();
                if (other is SmsSmallEnemy) _touching.Add((Node3D)other);
                else if (other is not Mario && MathF.Abs(col.GetNormal().Dot(UpDirection)) < 0.5f) { _onWall = true; OnWallContact(col); }
                else if (other is Node3D n3 && other is not Mario) OnBodyContact(n3);
            }
            _floorNormal = Vector3.Up;
            _groundY = GroundYBelow();
            ApplyLook();
            return;
        }
        // the ticks' displacement through the body: the world's floors and walls (TWalker's bind)
        FloorSnapLength = _airborne && !KeepVelocityOnGround() ? 0f : 0.3f;
        Velocity = _disp / U / (float)delta;
        MoveAndSlide();
        bool onFloor = IsOnFloor();
        _onWall = false;
        _touching.Clear();
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var col = GetSlideCollision(i);
            GodotObject other = col.GetCollider();
            if (other is SmsSmallEnemy) _touching.Add((Node3D)other);                                // another small enemy (the Gooble included): pushed apart, no hop
            else if (other is not Mario && MathF.Abs(col.GetNormal().Dot(UpDirection)) < 0.5f) { _onWall = true; OnWallContact(col); }
            else if (other is Node3D n3 && other is not Mario) OnBodyContact(n3);
        }
        if (onFloor)
        {
            _floorNormal = GetFloorNormal();
            if (_vel.Y <= 0f)
            {
                bool wasAir = _airborne;
                _airborne = false;
                if (!KeepVelocityOnGround()) _vel = Vector3.Zero;   // bind: nextPos.y <= mGroundHeight + 0.05 -> offLiveFlag(AIRBORNE), mVelocity = 0
                if (wasAir) OnLanded();
            }
        }
        else _airborne = true;
        if (KeepVelocityOnGround()) _airborne = false;        // the species' own bind rides the floor (THamuKuri's puck)
        _groundY = GroundYBelow();
        ApplyLook();
    }

    /// <summary>A wall contact this frame (the species may read the normal: the Stu's wall crash).</summary>
    protected virtual void OnWallContact(KinematicCollision3D col) { }
    /// <summary>A contact with some other body (a thrown fruit, a map object).</summary>
    protected virtual void OnBodyContact(Node3D other) { }
    /// <summary>The landing after an airborne spell.</summary>
    protected virtual void OnLanded() { }
    /// <summary>A species whose own bind keeps the velocity on the floor (THamuKuri's kicked puck): the landing does not zero it.</summary>
    protected virtual bool KeepVelocityOnGround() => false;
    /// <summary>A flying species (THaneHamuKuri::bind, pass 125f): the physics step neither snaps it to a floor nor sets its airborne flag -- the species
    /// sets <see cref="_airborne"/> and its height itself and keeps <see cref="_noBind"/> on (the base's gravity and velocity are its own).</summary>
    protected virtual bool FlyBind => false;
    /// <summary>The body's scale per axis for the look (mScaling): the one body scale, unless a species squashes (the winged Stu's dive).</summary>
    protected virtual Vector3 LookScale => Vector3.One * _scale;

    /// <summary>perform(CUE_MOVE) -> moveObject: TSmallEnemy's with TWalkerEnemy's wall hop; the species' MoveObjectPre / SetBehavior hooks.</summary>
    private void Tick()
    {
        if (_dead)
        {
            if (!Pooled && RelaunchTicks >= 0 && ++_relaunch > RelaunchTicks) Relaunch();
            return;
        }
        _lin = Vector3.Zero;
        _tickCount++;
        MoveObjectPre();
        SpineUpdate();                                          // control()
        if (_dead) return;
        // TMarDirector::direct (MarDirectorDirect.cpp 126): gpObjHitCheck->checkActorsHit() only on the ticks with mMoveTickCount's low two bits
        // clear -- the collision lists exist every FOURTH movement tick (clearHitNum on the others). A kicked Stu is 120 u away by the next list.
        if ((_tickCount & 3) == 0) HitCheckMario();             // gpObjHitCheck's pair with Mario -> attackToMario / TMario::hitNormal
        if (!_airborne)
            foreach (Node3D o in _touching)                      // isCollidMove (both on the ground): away at march x 3
            {
                if (!IsCollidMove(o)) continue;
                Vector3 d = Pos() - o.GlobalPosition * U;
                d.Y = 0f;                                        // DEVIATION (pass 126): the game's push is the full 3D direction; here a body launched onto one standing
                                                                 // at the launch point stood ON it (Godot's floor), was lifted by the push, stood on it again -- a lift
                                                                 // without end. Pushed aside instead, it slides off and gravity does the rest.
                if (d == Vector3.Zero) d.X += 1f;
                _lin += d.Normalized() * (_marchSpeed * 3f);        // v.add(local_74): the push on top of the walk (125f: it replaced the walk before)
            }
        if (_dead) return;
        // TLiveActor::bind / TWalker::bind: gravity and the terminal speed while airborne (the ground is MoveAndSlide's)
        if (!_noBind && (_airborne || _vel.Y > 0f))
        {
            _vel.Y -= GravityY();
            if (_vel.Y < -40f) _vel.Y = -40f;
        }
        // TWalkerEnemy::moveObject: blocked by a wall while walking -> the hop forward at MarchSpeedHigh (+5 deg of yaw)
        if (_onWall && !_airborne && _cur != NerveJump && _cur != NerveDie && _lin.LengthSquared() > 0f && _wallHopCooldown == 0 && WallHopAllowed())
        {
            Vector3 fwd = PolarXZ(_yaw, P.MarchSpeedHigh);
            _vel.X = fwd.X; _vel.Z = fwd.Z;
            PushNerve(NerveJump);
            _airborne = true;
            _yaw += 5f;
            _onWall = false;
            _wallHopCooldown = 10;
        }
        if (_wallHopCooldown > 0) _wallHopCooldown--;
        SetBehavior();
        if (_sprayCooldown > 0 && ++_sprayCooldown > 30) _sprayCooldown = 0;
        _disp += _lin + (_noBind ? Vector3.Zero : _vel);
    }

    /// <summary>The species' moveObject head (before control()).</summary>
    protected virtual void MoveObjectPre() { }
    /// <summary>TSmallEnemy::setBehavior (after bind).</summary>
    protected virtual void SetBehavior() { }
    protected virtual bool WallHopAllowed() => true;
    /// <summary>TLiveActor::getGravityY: mGravity (the melt: 0.01).</summary>
    protected virtual float GravityY() => _melt ? 0.01f : P.Gravity;

    protected Vector3 Pos() => GlobalPosition * U;
    protected Vector3 MarioPos() => _mario != null ? _mario.GlobalPosition * U : Pos();
    protected float Rand() => (float)_rng.NextDouble();
    protected static float Lerp(float a, float b, float t) => a + (b - a) * t;
    protected static Vector3 PolarXZ(float yawDeg, float r) => new Vector3(r * Mathf.Sin(Mathf.DegToRad(yawDeg)), 0f, r * Mathf.Cos(Mathf.DegToRad(yawDeg)));
    protected static float YawOf(Vector3 v) => Mathf.RadToDeg(Mathf.Atan2(v.X, v.Z));                    // MsGetRotFromZaxisY
    protected static float Wrap360(float a) { a %= 360f; return a < 0f ? a + 360f : a; }                   // MsWrap(a, 0, 360)
    protected static float AngleDiff(float a, float b) { float d = Wrap360(a - b); return d > 180f ? d - 360f : d; } // MsAngleDiff

    /// <summary>The floor under the body, units (mGroundHeight): a ray down from the head height -- the MAP only: Mario's body is excluded (the game's
    /// checkGround is a bg check; a diving Hane over him read his head as the ground, a Stu under him his feet).</summary>
    protected float GroundYBelow()
    {
        var space = GetWorld3D().DirectSpaceState;
        Vector3 from = GlobalPosition + Vector3.Up * (P.HeadHeight / U);
        var q = PhysicsRayQueryParameters3D.Create(from, GlobalPosition + Vector3.Down * 60f, CollisionMask);
        var excl = new Godot.Collections.Array<Rid> { GetRid() };
        if (_mario != null && IsInstanceValid(_mario)) excl.Add(_mario.GetRid());
        q.Exclude = excl;
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? ((Vector3)hit["position"]).Y * U : Pos().Y - 60f * U;
    }

    /// <summary>calcRootMatrix: the body stands on the floor normal, faces the heading, at the body scale.</summary>
    protected virtual void ApplyLook()
    {
        if (_model == null) return;
        Vector3 up = _floorNormal.LengthSquared() > 0f ? _floorNormal.Normalized() : Vector3.Up;
        Vector3 fwd = PolarXZ(_yaw, 1f);
        Vector3 x = up.Cross(fwd);
        if (x.LengthSquared() < 1e-6f) { up = Vector3.Up; x = up.Cross(fwd); }
        x = x.Normalized();
        Vector3 z = x.Cross(up).Normalized();
        _model.Transform = new Transform3D(new Basis(x, up, z) * Basis.FromScale(LookScale * _modelScale), Vector3.Zero);
    }

    // ------------------------------------------------------------------------------------------------------------------ anims
    /// <summary>setBckAnm: the clip at its bck frame count over its length (one bck frame = 1 / 60 s), looping per the .bck's attribute.</summary>
    protected void SetBck(int i)
    {
        _bck = i;
        if (_anim == null || i < 0 || i >= BckNames.Length || !_anim.HasAnimation(BckNames[i])) return;
        var clip = _anim.GetAnimation(BckNames[i]);
        clip.LoopMode = BckLoops[i] ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        _bckRate = clip.Length > 0f ? BckFrames[i] / (float)clip.Length : 60f;
        _anim.Play(BckNames[i]);
        _anim.Seek(0f, true);
        _anim.SpeedScale = 60f / _bckRate;
    }
    protected bool IsBck(int i) => _bck == i;
    /// <summary>getCurAnmFrameNo: the clip's position in bck frames.</summary>
    protected float BckFrame()
    {
        if (_bck < 0 || _bck >= BckNames.Length) return 0f;
        if (_anim == null || !_anim.HasAnimation(BckNames[_bck]) || _anim.CurrentAnimation != BckNames[_bck]) return BckFrames[_bck];
        return (float)_anim.CurrentAnimationPosition * _bckRate;
    }
    /// <summary>checkCurAnmEnd(0) = MActorAnm::endsNext: frame + rate + 0.1 >= the end -- the last ~1.1 bck frames of the clip, ONCE a cycle for a
    /// looping one (the game's nerves wait on looping waits this way), and a finished one-shot.</summary>
    protected bool BckEnded()
    {
        if (_bck < 0 || _bck >= BckNames.Length) return true;
        if (_anim == null || !_anim.HasAnimation(BckNames[_bck])) return true;
        if (!BckLoops[_bck] && !_anim.IsPlaying()) return true;
        return BckFrame() + 1.1f >= BckFrames[_bck];
    }
    protected int BckEndFrame => _bck >= 0 && _bck < BckFrames.Length ? BckFrames[_bck] : 0;

    // the anim setters (TSmallEnemy's virtuals; a species maps them onto its table)
    protected virtual void SetGenerateAnm() { }
    protected virtual void SetWalkAnm() { }
    protected virtual void SetDeadAnm() { }
    protected virtual void SetFreezeAnm() { }
    protected virtual void SetMeltAnm() { }
    protected virtual void SetWaitAnm() { }
    protected virtual void SetRunAnm() { }
    protected virtual void SetDeadEffect() { }

    // ------------------------------------------------------------------------------------------------------------------ sight and goals
    /// <summary>MsIsInSight(eye, sight, target, length, angle, aware).</summary>
    protected bool IsInSight(Vector3 target, float length, float angle, float aware)
    {
        Vector3 d = target - Pos();
        float d2 = d.LengthSquared();
        if (d2 < aware * aware) return true;
        return d2 < length * length && MathF.Abs(AngleDiff(YawOf(d), _yaw)) < angle * 0.5f;
    }

    /// <summary>TSmallEnemy::isFindMario(k): no demo, not airborne, Mario not in water, within SearchHeight and in sight within k x
    /// (SearchLength / SearchAngle / SearchAware).</summary>
    protected virtual bool IsFindMario(float k)
    {
        if (_mario == null || _airborne) return false;
        if (_mario.SmsInWaterForEnemies) return false;
        if (_mario.SmsShineDemoRunning) return false;
        Vector3 mp = MarioPos();
        if (MathF.Abs(mp.Y - Pos().Y) >= P.SearchHeight) return false;
        return IsInSight(mp, P.SearchLength * k, P.SearchAngle * k, P.SearchAware * k);
    }
    /// <summary>TWalkerEnemy / TNerveWalkerAttack's water test for Mario (isMarioInWater).</summary>
    protected bool MarioInWater => _mario != null && _mario.SmsInWaterForEnemies;

    protected Vector3 Goal() => _goalIsMario ? MarioPos() : _goalPoint;
    protected void SetGoalMario() { _goalIsMario = true; }
    protected void SetGoalPoint(Vector3 p) { _goalIsMario = false; _goalPoint = p; }

    /// <summary>goToRandomNextGraphNode: a random node of the graph (not the one within 100 u); none -> a random point within WanderRadiusUnits of
    /// the placed point (DEVIATION).</summary>
    protected void GoToRandomNextGraphNode()
    {
        if (Graph != null && Graph.GetChildCount() > 0)
        {
            var kids = Graph.GetChildren();
            Node3D pick = kids[_rng.Next(kids.Count)] as Node3D;
            Vector3 dd = pick != null ? pick.GlobalPosition * U - Pos() : Vector3.Zero; dd.Y = 0f;        // on the ground plane: a flying body's height is not a distance to a node
            if (kids.Count > 1 && pick != null && dd.Length() < 100f)
                pick = kids[(kids.IndexOf(pick) + 1 + _rng.Next(kids.Count - 1)) % kids.Count] as Node3D;
            if (pick != null) { SetGoalPoint(pick.GlobalPosition * U); return; }
        }
        float a = Rand() * MathF.Tau, r = WanderRadiusUnits * (0.3f + 0.7f * Rand());
        SetGoalPoint(_spawnPos + new Vector3(r * MathF.Sin(a), 0f, r * MathF.Cos(a)));
    }
    /// <summary>goToRandomEscapeGraphNode (getEscapeFromMarioIndex): the graph node farthest from Mario; none -> a point WanderRadius away from him.</summary>
    protected void GoToRandomEscapeGraphNode()
    {
        Vector3 mp = MarioPos();
        if (Graph != null && Graph.GetChildCount() > 0)
        {
            Node3D best = null; float bestD = -1f;
            foreach (Node k in Graph.GetChildren())
                if (k is Node3D n3) { float d = (n3.GlobalPosition * U - mp).LengthSquared(); if (d > bestD) { bestD = d; best = n3; } }
            if (best != null) { SetGoalPoint(best.GlobalPosition * U); return; }
        }
        Vector3 away = Pos() - mp; away.Y = 0f;
        if (away.LengthSquared() < 1e-3f) away = PolarXZ(_yaw, 1f);
        SetGoalPoint(Pos() + away.Normalized() * WanderRadiusUnits);
    }
    /// <summary>isReachedToGoal: within 100 u (THaneHamuKuri's: on the ground plane only).</summary>
    protected virtual bool IsReachedToGoal() => (Goal() - Pos()).Length() < 100f;

    /// <summary>TSpineEnemy::calcTurnSpeedToReach(march, half the distance): the turn a tick that reaches the goal on an arc.</summary>
    protected static float CalcTurnSpeedToReach(float march, float d)
    {
        if (d == 0f) return 0f;
        float f = d * 2f * d;
        float c = Mathf.Clamp(-(march * march - f) / f, -1f, 1f);
        if (c == 1f) return 0f;
        if (c == -1f) return 180f;
        float s = MathF.Sqrt(1f - c * c);
        return 90f - Mathf.RadToDeg(MathF.Atan2(s, c));
    }

    /// <summary>TSpineEnemy::walkToCurPathNode(march, turn, offset): turn toward the goal by at most turn degrees a tick (less within the turning
    /// circle, so the arc reaches it) and add march units a tick along the new heading.</summary>
    protected void WalkToCurPathNode(float march, float turn, float offset)
    {
        Vector3 d = Goal() - Pos();
        float dist = d.Length();
        float angle = Wrap360(offset + YawOf(d));
        float diff = AngleDiff(angle, _yaw);
        float minR;
        if (turn >= 90f) minR = 0f;
        else
        {
            float s = Mathf.Sin(Mathf.DegToRad(turn));
            minR = s == 0f ? 100000f : march * Mathf.Sin(Mathf.DegToRad(90f - turn * 0.5f)) / s;
        }
        float step;
        if (dist > minR * 2f) step = diff > 0f ? MathF.Min(diff, turn) : MathF.Max(diff, -turn);
        else { float t = CalcTurnSpeedToReach(march, dist * 0.5f); step = diff > 0f ? MathF.Min(diff, t) : MathF.Max(diff, -t); }
        _yaw = Wrap360(_yaw + step);
        _lin += PolarXZ(_yaw, march);
    }
    /// <summary>zigzagToCurPathNode: the graph tracer's zigzag factor is 0 without a graph (unk124->unk10) -> the plain walk.</summary>
    protected void ZigzagToCurPathNode(float march, float turn, float cycle, float angle) => WalkToCurPathNode(march, turn, 0f);

    /// <summary>TWalkerEnemy::walkBehavior(mode, k): 0 zigzag at march x k / turn x k; 1, 2 the walk; 3 turn only; 4 march only; 5 nothing.</summary>
    protected virtual void WalkBehavior(int mode, float k) => WalkBehaviorBase(mode, k);
    /// <summary>TWalkerEnemy::walkBehavior itself, for a nerve the game writes as the explicit base call (past a kind's flight math).</summary>
    protected void WalkBehaviorBase(int mode, float k)
    {
        switch (mode)
        {
            case 0: ZigzagToCurPathNode(_marchSpeed * k, _turnSpeed * k, P.ZigzagCycle, P.ZigzagAngle); break;
            case 1: case 2: WalkToCurPathNode(_marchSpeed * k, _turnSpeed * k, 0f); break;
            case 3: WalkToCurPathNode(0f, _turnSpeed * k, 0f); break;
            case 4: WalkToCurPathNode(_marchSpeed * k, 0f, 0f); break;
        }
    }
    /// <summary>TWalkerEnemy::isResignationAttack: the goal farther than GiveUpLength.</summary>
    protected virtual bool IsResignationAttack() => (Goal() - Pos()).Length() > P.GiveUpLength;
    /// <summary>TWalkerEnemy::behaveToFindMario: Jump -> Attack -> Wander (or, with unk150 & 2, Jump -> Escape -> Wander).</summary>
    protected virtual void BehaveToFindMario()
    {
        if ((_flags150 & 2) != 0) { PushAfterCurrent(NerveGraphWander); PushAfterCurrent(NerveEscape); PushAfterCurrent(NerveJump); }
        else { SetGoalMario(); PushAfterCurrent(NerveGraphWander); PushAfterCurrent(NerveAttack); PushAfterCurrent(NerveJump); }
    }

    // ------------------------------------------------------------------------------------------------------------------ the shared nerves
    /// <summary>One nerve's tick; true = done (the next on the stack follows). A species handles its own ids and falls back here.</summary>
    protected virtual bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveGenerate:                                  // TNerveWalkerGenerate
                if (_time == 0) SetGenerateAnm();
                else if (BckEnded()) { PushAfterCurrent(NerveGraphWander); return true; }
                return false;

            case NerveGraphWander:                               // TNerveWalkerGraphWander
                if (_time == 0) { SetWalkAnm(); GoToRandomNextGraphNode(); }
                if (IsReachedToGoal()) GoToRandomNextGraphNode();
                if (IsFindMario(1f)) { BehaveToFindMario(); return true; }
                WalkBehavior(0, 1f);
                return false;

            case NerveAttack:                                    // TNerveWalkerAttack
                if (_time == 0) SetRunAnm();
                if (_goalIsMario)
                {
                    if (MarioInWater) return true;
                    if (MathF.Abs(MarioPos().Y - Pos().Y) > P.GiveUpHeight) return true;
                }
                WalkBehavior(2, 3f);
                return IsResignationAttack();

            case NervePostAttack:                                // TNerveWalkerPostAttack
                if (_time == 0) SetWaitAnm();
                return _time >= P.AttackWait && BckEnded();

            case NerveEscape:                                    // TNerveWalkerEscape
                if (_time == 0) { GoToRandomEscapeGraphNode(); SetWalkAnm(); }
                if (IsReachedToGoal()) GoToRandomEscapeGraphNode();
                if (MarioInWater) return true;
                if ((MarioPos() - Pos()).LengthSquared() > P.GiveUpLength * P.GiveUpLength) return true;
                WalkBehavior(1, 2f);
                return false;

            case NerveTraceMario:                                // TNerveWalkerTraceMario
                if (_time == 0) { SetRunAnm(); SetGoalMario(); }
                if (MarioInWater) { PushAfterCurrent(NerveTraceMario); PushAfterCurrent(NerveWait); return true; }
                if (MathF.Abs(MarioPos().Y - Pos().Y) > P.GiveUpHeight) return true;
                WalkBehavior(2, 3f);
                return IsResignationAttack();

            case NerveDie:                                       // TNerveSmallEnemyDie
                if (_time == 0)
                {
                    _hp = Math.Max(0, _hp - 1);
                    if (_hp == 0) SetCollisionLayerValue(1, false);  // onHitFilter(NO_COLLISION)
                    if (_melt) SetMeltAnm();
                    else
                    {
                        if ((_flags150 & 0x20) != 0) SmsPollutionMap.Pollute(GlobalPosition, P.StampRange * 32f);   // the goop stamp of a dying polluter
                        if (_hp == 0) { SetDeadAnm(); SetDeadEffect(); }
                        else return true;                        // a hit point left: back to what it was doing
                    }
                }
                if ((BckEnded() && _time > 60 + BckEndFrame) || _time > 360 || _attackerKilled)
                {
                    GenRandomItem();
                    Dead();
                    return true;
                }
                return false;

            case NerveFreeze:                                    // TNerveSmallEnemyFreeze
                if (_time == 0) SetFreezeAnm();
                return BckEnded() && _time >= P.FreezeWait;

            case NerveJump:                                      // TNerveSmallEnemyJump
                if (_time == 0)
                {
                    JumpBehavior();
                    _vel.Y = P.JumpForce * _bodyScale;
                    _airborne = true;
                }
                return !_airborne;

            case NerveHitWaterJump:                              // TNerveSmallEnemyHitWaterJump
                if (_time == 0)
                {
                    SetWaitAnm(); JumpBehavior();
                    _vel.Y = P.JumpForce; _airborne = true;
                    _yaw += Lerp(30f, 100f, Rand());
                }
                _yaw += Lerp(4f, 10f, Rand());
                if (!_airborne || _time > 360) { EndHitWaterJump(); return true; }
                return false;

            case NerveWait:                                      // TNerveSmallEnemyWait
                if (_time == 0) SetWaitAnm();
                return BckEnded() && _time > P.WaitTime;
        }
        return true;
    }
    protected virtual void JumpBehavior() { }
    protected virtual void EndHitWaterJump() { }

    // ------------------------------------------------------------------------------------------------------------------ hits
    /// <summary>TObjHitCheck's checkDistance: the attacker's cylinder (radius, height above its ground point) against the sufferer's.</summary>
    protected static bool CheckDistance(Vector3 a, float ar, float ah, Vector3 s, float sr, float sh)
    {
        if (a.Y > s.Y + sh) return false;
        if (a.Y + ah < s.Y) return false;
        float r = ar + sr, dx = a.X - s.X, dz = a.Z - s.Z;
        return r * r > dx * dx + dz * dz;
    }

    /// <summary>The collision lists of one tick, both ways: our attack cylinder (AttackRadius x AttackHeight x scale) on Mario's damage cylinder
    /// -> attackToMario; Mario's attack cylinder (TMario::hitNormal: falling in a jump -> TRAMPLE, a ground pound -> HIP_DROP) on our damage
    /// cylinder -> isHitValid -> kill; the hose at close range -> SPRAYED_BY_WATER.</summary>
    protected virtual void HitCheckMario()
    {
        if (_mario == null || _cur == NerveDie || !GetCollisionLayerValue(1)) return;   // (not gated on the hit points: decHpByWater floors at 0 and the game's lists still find it -- the 1-HP kinds would be immune after one drop)
        Vector3 g = Pos(), m = MarioPos();
        float s = _bodyScale;
        bool onGround = _mario.IsOnFloor();
        bool rising = !onGround && _mario.Velocity.Y > 0f;
        float ar = onGround ? SmsParams.Mario.HoldRadius : (rising ? SmsParams.Mario.PushupRadius : SmsParams.Mario.TrampleRadius);
        float ah = onGround ? SmsParams.Mario.AttackHeight : (rising ? SmsParams.Mario.PushupHeight : SmsParams.Mario.AttackHeight);
        bool marioHits = CheckDistance(m, ar, ah, g, P.DamageRadius * s, P.DamageHeight * s);
        if (marioHits)
        {
            bool falling = !onGround && _mario.Velocity.Y < 0f && g.Y < m.Y;
            var st = _mario.stateOfMario;
            bool noTrample = st == Mario.MarioState.diving || st == Mario.MarioState.singleJumpDive || st == Mario.MarioState.doubleJumpDive || st == Mario.MarioState.tripleJumpDive
                || st == Mario.MarioState.bellySlidingFromDive || st == Mario.MarioState.swimming || st == Mario.MarioState.ledgeHang || st == Mario.MarioState.ledgeClimb
                || st == Mario.MarioState.treeGrab || st == Mario.MarioState.treeClimb || st == Mario.MarioState.hurt || st == Mario.MarioState.dead;
            if (falling && st == Mario.MarioState.groundPoundFalling)
            {
                if (IsHitValid(HitMessage.HipDrop)) { _attackerKilled = false; Kill(); }
                return;
            }
            if (falling && !noTrample)
            {
                if (IsHitValid(HitMessage.Trample)) { _attackerKilled = false; Kill(); }
                _mario.StompBounce();                            // trampleExec: MARIO_STATUS_TRAMPLE
                return;
            }
            if (_mario.SmsHoseSpraying) SmsSprayedByWater(m + Vector3.Up * 80f / U);
            if (_dead || _cur == NerveDie) return;
        }
        bool weHit = CheckDistance(g, P.AttackRadius * s, P.AttackHeight * s, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight);
        if (weHit) AttackToMario();
    }
    public enum HitMessage { Trample, HipDrop, SuperHipDrop, Punch }
    /// <summary>TSmallEnemy::isHitValid: a hidden one takes nothing.</summary>
    protected virtual bool IsHitValid(HitMessage msg) => _model == null || _model.Visible;

    /// <summary>The damage profile his TakeDamage gets (dmg*.prm): the species' (TWalkerEnemy's default EnemyCommon).</summary>
    protected virtual Mario.SmsDamageProfile DamageProfile => Mario.SmsDamageProfile.EnemyCommon;
    /// <summary>TSmallEnemy::sendAttackMsgToMario: SMS_SendMessageToMario(ATTACK).</summary>
    protected virtual void SendAttackMsgToMario() => _mario?.TakeDamage(GlobalPosition, DamageProfile);
    /// <summary>TWalkerEnemy::attackToMario: the message, then (not jumping / dying / generating) PostAttack after a hop.</summary>
    protected virtual void AttackToMario()
    {
        SendAttackMsgToMario();
        if (_cur != NerveJump && _cur != NerveDie && _cur != NerveGenerate) { PushNerve(NervePostAttack); PushNerve(NerveJump); }
    }
    /// <summary>TSmallEnemy::isCollidMove: both on the ground (map objects never push).</summary>
    protected virtual bool IsCollidMove(Node3D other)
    {
        bool otherAir = other is SmsSmallEnemy e && e._airborne;
        return !otherAir && !_airborne;
    }

    /// <summary>TSmallEnemy::kill: the Die nerve now, the stack dropped (then the default).</summary>
    public virtual void Kill()
    {
        if (_dead) return;
        _hp = 1;
        if (_cur != NerveDie) { _stack.Clear(); SetNext(NerveDie); }
    }

    /// <summary>HIT_MESSAGE_SPRAYED_BY_WATER (TSmallEnemy::receiveMessage): the WATHIT mark every hit; once per 30-tick cooldown decHpByWater
    /// (the hit's attack, at least 1) and behaveToWater. True = the drop is taken.</summary>
    public virtual bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (_sprayCooldown != 0) return true;
        _sprayCooldown = 1;
        _hp = Math.Max(0, _hp - 1);                              // decHpByWater: the particle attack (1 for the hose's drops), floored at 0 -- the body lives on until a hit message kills it
        BehaveToWater();
        return true;
    }
    /// <summary>TSmallEnemy::behaveToWater: Freeze unless frozen or dying.</summary>
    protected virtual void BehaveToWater()
    {
        if (_cur != NerveFreeze && _cur != NerveDie) PushNerve(NerveFreeze);
    }

    /// <summary>genRandomItem: setAfterDeadEffect (the puffs) + genEventCoin (nothing without a placed coin) + generateItem -> the drop table, skipped for
    /// a death flagged UNK20000 (the wall crash, a kill by another attacker) or a body already dead, as the game's flag test.</summary>
    protected virtual void GenRandomItem()
    {
        SetAfterDeadEffect();
        if (_noReward || _dead) return;
        if (DropTable != null)
            LastDrops = DropTable.Spawn(_rng, P.GenItemRate + P.GenEggRate, GetParent(), new Vector3(GlobalPosition.X, _groundY / U, GlobalPosition.Z), this);
    }
    /// <summary>For the drives: the nodes the last death dropped.</summary>
    public List<Node3D> LastDrops { get; private set; } = new();
    /// <summary>The spawner the species' own drops (the Stu's serial 1-UP and serial-crash coin) go through: the DropTable when set (its pop and hop
    /// settings), else a default table with the game's pop.</summary>
    protected SmsDropTable DropSpawner => DropTable ?? (_defaultSpawner ??= new SmsDropTable());
    private static SmsDropTable _defaultSpawner;
    /// <summary>setAfterDeadEffect: PARTICLE_MS_ENM_DISAP_A + _B at the body, scaled (setGlobalScale(mScaling)); MSD_SE_EN_COMMON_SMOKE.</summary>
    protected virtual void SetAfterDeadEffect()
    {
        SpawnBurst(DeathFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
        if (DeathSound != null && DeathSound.Stream != null) DeathSound.Play();
    }

    /// <summary>The game's emit(id, &amp;pos) with no bind: an unbound one-shot (an SmsFxBurst scene, or a bare GPUParticles3D) at <paramref name="at"/>
    /// (its basis = the emitter's rotation x the body's scale), under the level, freed when done.</summary>
    protected Node3D SpawnBurst(PackedScene scene, Transform3D at)
    {
        if (scene == null) return null;
        var fx = scene.Instantiate<Node3D>();
        fx.TopLevel = true;
        GetParent().AddChild(fx);
        fx.GlobalTransform = at;
        if (fx is SmsFxBurst burst) burst.Play(at);
        else if (fx is GpuParticles3D gp) { gp.OneShot = true; gp.Emitting = true; gp.Finished += gp.QueueFree; }
        return fx;
    }

    /// <summary>LIVE_FLAG_DEAD: hidden, no collision, waiting for the relaunch (or freed).</summary>
    protected virtual void Dead()
    {
        _dead = true; _relaunch = 0;
        _vel = Vector3.Zero; _lin = Vector3.Zero;
        if (_model != null) _model.Visible = false;
        SetCollisionLayerValue(1, false);
        if (_anim != null) _anim.Stop();
        if (RelaunchTicks < 0 && !Pooled) QueueFree();
    }
    /// <summary>A pooled body's birth: LIVE_FLAG_DEAD at once (TEnemyManager::createEnemies makes its objects dead; a generator / launcher brings them in).</summary>
    public void SmsSpawnDead() { if (!_dead) Dead(); }
    /// <summary>TSpineEnemy::resetSRTV from a generator / launcher (Enemy/enemy.cpp 187): the body at the point facing yawDeg, alive again (the flags off,
    /// reset, the hit points, the collision), with the launch velocity (units a tick) and LIVE_FLAG_AIRBORNE; on `graph` when one is given
    /// (getTracer()->setGraph / init). The point becomes the body's own launch point.</summary>
    public void SmsLaunch(Vector3 globalPosition, float yawDeg, Vector3 velocityUnitsPerTick, Node3D graph = null)
    {
        if (graph != null) Graph = graph;
        SmsPlace(globalPosition, yawDeg);
        _vel = velocityUnitsPerTick;
        _airborne = true;
    }
    /// <summary>TLauncher::stateLaunch -> resetSRTV at the placed point, the set anim.</summary>
    protected virtual void Relaunch()
    {
        GlobalPosition = _spawnPos / U; _yaw = _spawnYaw;
        Reset();
        LaunchVelocity();
    }
    /// <summary>The launcher's starting velocity for a re-launch (a placed one: none).</summary>
    protected virtual void LaunchVelocity() { }
    /// <summary>pass 116: gpConductor->killEnemiesWithin (the shine get).</summary>
    public void SmsKillByDemo() { if (!_dead) Dead(); }

    // ------------------------------------------------------------------------------------------------------------------ borrowed clips
    /// <summary>A model with no clips of its own playing another archive's (the game's createMActor(model, anm data): dorokuri_model.bmd with hamukurianm,
    /// dorohane.bmd with hanekuri's): the source scene's AnimationPlayer library copied onto an AnimationPlayer built under Model, every track re-pointed at
    /// this skeleton; a bone the exporter named differently (root / root_1 / root_2, body / body_3) is matched on its name without the _N suffix. Returns the
    /// player (also set as <see cref="_anim"/>), or null.</summary>
    protected AnimationPlayer BorrowClips(PackedScene src, string tag)
    {
        if (_model == null || src == null) return null;
        Skeleton3D skel = null;
        foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { skel = n as Skeleton3D; if (skel != null) break; }
        var donor = src.Instantiate<Node>();
        AnimationPlayer donorAnim = null;
        foreach (Node n in donor.FindChildren("*", "AnimationPlayer", true, false)) { donorAnim = n as AnimationPlayer; if (donorAnim != null) break; }
        if (donorAnim == null) { GD.PushWarning($"[SMS {tag}] {GetPath()}: the clip source has no AnimationPlayer"); donor.Free(); return null; }
        var player = _model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (player == null)
        {
            player = new AnimationPlayer { Name = "AnimationPlayer" };
            _model.AddChild(player);
            player.RootNode = player.GetPathTo(_model);
        }
        foreach (string libName in donorAnim.GetAnimationLibraryList())
        {
            var lib = donorAnim.GetAnimationLibrary(libName);
            if (lib == null) continue;
            var copy = new AnimationLibrary();
            foreach (string clip in lib.GetAnimationList())
            {
                var a = (Animation)lib.GetAnimation(clip).Duplicate();
                if (skel != null)
                    for (int t = 0; t < a.GetTrackCount(); t++)
                    {
                        NodePath tp = a.TrackGetPath(t);
                        if (tp.GetSubNameCount() == 0) continue;
                        a.TrackSetPath(t, new NodePath($"{_model.GetPathTo(skel)}:{MatchBone(skel, tp.GetSubName(0))}"));
                    }
                copy.AddAnimation(clip, a);
            }
            if (player.HasAnimationLibrary(libName)) player.RemoveAnimationLibrary(libName);
            player.AddAnimationLibrary(libName, copy);
        }
        donor.Free();
        _anim = player;
        return player;
    }
    /// <summary>The skeleton's bone for a track's bone name: exact, else the same name without the exporter's _N suffix on either side.</summary>
    protected static string MatchBone(Skeleton3D skel, string name)
    {
        if (skel.FindBone(name) >= 0) return name;
        string Strip(string s) { int i = s.LastIndexOf('_'); return i > 0 && int.TryParse(s[(i + 1)..], out _) ? s[..i] : s; }
        string want = Strip(name);
        for (int i = 0; i < skel.GetBoneCount(); i++)
            if (Strip(skel.GetBoneName(i)) == want) return skel.GetBoneName(i);
        return name;
    }
}
