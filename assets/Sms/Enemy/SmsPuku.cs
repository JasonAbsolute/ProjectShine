using System;
using Godot;

/// <summary>
/// CHEEP CHEEP -- TTobiPuku / TTobiPukuLaunchPad / TPukuPuku (Enemy/tobiPuku.cpp; JP とびプク "leaping Puku", プクプク; the pukupuku archive) on
/// <see cref="SmsSmallEnemy"/> with the shipped /enemy/tobipuku.prm (SMS pass 142, feature/enemy-ricco). Ricco Harbor's fish: the LEAPER is shot out of
/// the water by its launch pad in a high arc, flies pitched along its path, splashes down, loops over under the surface, swims back to the pad and leaps
/// again, forever -- unless it lands on a dock (it bounces, flops about hurting Mario, and dies after a while), hits Mario in flight (the attack, then a
/// fall), or is hosed in the air (flung away from him, then a fall into the water: dead). The SWIMMER swims a graph of points under the surface, bobbing,
/// and hurts a swimming Mario it touches.
///
/// THE GAME. TTobiPuku is a TWalkerEnemy (unk150 0x31, ACTOR_TYPE_PUKU_PUKU) with /enemy/tobipuku.prm: HeadHeight 50, WallRadius 10, 1 hit point,
/// SearchLength 800 / Height 600 / Aware 400 / Angle 90, GiveUp 2000 / 650, AttackWait 120, FreezeWait 300, damage 100 x 80 / attack 105 x 60, turn 3..5,
/// body scale 1, march 2.5..3.5 u/tick, zigzag 1000 / 30; its own: BoundNum 3, BoundVal 0.5, LifeTimer 800, FlyGravityY 0.25, PowerFromWater 30. The
/// statics: mLandAngle 90, mBoundSw true, mBoundVelocityY 0.8, mReturnLaunchSw true. The archive's 11 bcks in order are the clip table: 0 attack,
/// 1 death, 2 down_air, 3 down_land, 4 fall, 5 fall_end_land, 6 jump, 7 jump_start, 8 pitipiti, 9 swim, 10 wait. The LAUNCH PAD (TTobiPukuLaunchPad,
/// a tiny TSmallEnemy of /enemy/tobipukulaunch.prm, placed in the water with an extra s32 launchPower) perform(): with mReturnLaunchSw it launches a
/// fish when it has none or its fish is dead (else every LaunchInterval 300 ticks); forceLaunch: the pad's yaw and X rotation (the pitch) give the
/// velocity (sin yaw, 1, cos yaw) x launchPower, x = (cos pitch, sin pitch, cos pitch); the fish reset at the pad with unk1D0 = that velocity, unk1B0 = the
/// pad's height, unk1DC = the pad (the manager's unk60 mode -- calcVelocityToJumpToY to FlyDist along the facing at LaunchVelocityY -- is never set).
/// The nerves: GENERATE (the default): tick 0 LIVE_FLAG_UNK10 (no bind), mPosition.y -= 300, the pitch of the velocity, the jump clip; each tick y +=
/// unk1D0.y until over unk1B0 -> flyStart (unk194 = 1: FlyGravityY, mVelocity = unk1D0, unk1B4 = its pitch, the water column + TOBIPUKU_FRWATER,
/// airborne, UNK10 off) -> FLY: setJumpStartAnm (a no-op unless already jump_start: the jump clip plays on), the pitch follows the velocity
/// (MsGetRotFromZaxis), unk1E4 = vy each tick; landed -> LAND. LAND: ticks 0-1: on a WATER surface: y -= 10, UNK10, the column + TOBIPUKU_TOWATER;
/// with the return switch unk1E4 x 0.8 and unk1E8 = (180 - pitch) / |600 / unk1E4| (the pitch step that reaches 180 over the 600 u dive); on the
/// GROUND: mBoundSw and canBound (unk198 &lt; BoundNum) -> BOUND, else unk1B8[0] = the position, fall_end_land, pitch 0. From tick 2: fall_end_land
/// playing -> (a 20-tick position blend whose second vector the decomp never sets) -> its end -> PITIPITI; else with the return switch the DIVE by hand:
/// unk1D0.xz x 0.5, the velocity = unk1D0.xz x cos(pitch) with vy = unk1E4, pitch = clamp(pitch + unk1E8, 0, 180), past 120 u deep the roll
/// (unk1EC, +3 a tick to 180: TobiPukuRollCallback turns joint 1 'center' about its Z), past 600 u deep -> RETURNLAUNCH; without the switch y -= 12 a
/// tick, the pitch +1.2 to mLandAngle 90 while the jump clip plays, dead after 100 ticks. BOUND: bound(): unk1AE = 1, unk198++, the velocity = unk1D0.xz
/// x BoundVal, vy = mBoundVelocityY x BoundVal x (unk1B0 - the ground height) / 30, airborne; rising -> unk1B0 = y; landed -> LAND. PITIPITI: the
/// pitipiti clip (PUKU_PICHI at joint 1 each frame), its end past LifeTimer -> unk1AD = 0 -> DIE. RETURNLAUNCH: the goal = the pad, the swim clip,
/// unk1E0 = y, PUKU_AWA bubbles at joint 6 (the lifetime from the depth); within 200 u of the pad in XZ -> PREPAREFLY; else toward the pad at the march
/// speed with unk1D0.xz decaying x 0.99 subtracted, the roll +1 a tick. PREPAREFLY: unk1F0 = (wrap(unk1B4) - pitch) / 60; each tick the position
/// eases 1/60 toward the pad, the roll -3, the pitch += unk1F0; tick 50 setJumpStartAnm; past 60 -> the pad's forceLaunch + reset -> GENERATE.
/// attackToMario: ATTACK; not attacking, not bouncing (unk1AE), Mario not in water -> unk1D0 = 0, ATTACK nerve: the attack clip; airborne: frame 6
/// fallStart (unk194 = 0, the XZ velocity zeroed, +2 u, airborne), its end -> FALL; on the ground done at once. behaveToWater -> EN_COMMON_FLY +
/// HITWATER (unless in it): tick 0 airborne over 50 u -> the attack clip + hitWater (the velocity = the direction from Mario x PowerFromWater, y x 2;
/// unk1D0 = it, unk1B0 = y, yaw = 180 - Mario's angle); on the ground and bouncing -> pitipiti; each tick on the ground the knock (away x 5, vy 5,
/// +5 u, airborne); the clip's end: the attack clip -> FALL; done. FALL: pitch 0, the fall clip; landed: water -> the column, DIE with UNK20000 (no
/// reward); ground -> DIE with UNK20000. DIE: airborne -> no collision, the XZ velocity zeroed, down_air; else unk1AD -> no collision, down_land; else
/// UNK20000 + death; the clip's end -> DEAD, TNerveSmallEnemyDie, genRandomItem. kill (a stomp): hit points 1, not already dying from a kill -> the
/// spine reset to DIE. moveObject: the turn speed = TurnSpeedLow; airborne with mBoundSw -> hitWall: a wall within 1.1 x the wall radius at the head
/// reflects the velocity (vy x 0.5, unk1D0 = it, unk1B0 = y); a roof over the head caps vy at 0. getGravityY: FlyGravityY while unk194. calcRootMatrix:
/// pitch != 0 -> the model at (x, y + 70 x pitch / mLandAngle, z) with the full rotation. TPukuPuku: the default nerve SWIMWANDER: unk1E0 = y, the swim
/// clip, initialGraphNode, UNK10; at the goal (200 u in XZ) goToRandomNextGraphNode; walkBehavior(0, 1.5): the walk x 1.5, then y = unk1E0 + 10 sin(2 x
/// time), the pitch from the position delta. TMoePuku (the flaming one) is another level's.
///
/// THE PORT. <see cref="Kind"/> = Leaper (the fish AND its pad: the node's place is the pad, in the water at the surface; <see cref="LaunchSpeedUnits"/> and
/// <see cref="LaunchPitchDegrees"/> the pad's launchPower and X rotation, the node's yaw the direction) or Swimmer. UNK10 = the base's <c>_noBind</c> with
/// the collision mask off (the game's bind-less fish passes through the map: a rising one under a pool's floor, a diving one through it); the flight,
/// the bounces and the falls ride the base's bind. A WATER SURFACE is a floor (the game's water triangles are ground, his water an sms_water area):
/// SetBehavior lands a falling body meeting the surface (the base's WaterSurfaceAt) and the nerves read <c>_onWater</c> as mGroundPlane->isWaterSurface().
/// The pitch is the model's X turn in ApplyLook (with the 70 u lift), the roll an SmsBoneRoll on 'center' about the body axis. DEVIATIONS (logged): the
/// landing's 20-tick position blend is not ported (its second vector is never set in the decomp); the dive ends early at a pool's floor
/// (<see cref="DiveDepthUnits"/>, the game dives 600 u); the swimmer's goal rule is the base's (a random Graph node or a random point within
/// WanderRadiusUnits); the water column, the bubbles and the flop drops are slots; the sounds are slots; the clipped-out rules are not ported.
/// </summary>
public partial class SmsPuku : SmsSmallEnemy
{
    public enum Kinds { Leaper, Swimmer }

    // ------------------------------------------------------------------------------------------------------------------ tobipuku.prm
    public class PukuParams : Params
    {
        public int BoundNum = 3, LifeTimer = 200;
        public float BoundVal = 0.8f, FlyGravityY = 0.2f, PowerFromWater = 1f;
    }
    /// <summary>The shipped /enemy/tobipuku.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly PukuParams Prm = new PukuParams
    {
        HeadHeight = 50f, BodyRadius = 5f, WallRadius = 10f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 100, ActiveEnemyNum = 20,
        JumpForce = 10f, SearchLength = 800f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 2000f, GiveUpHeight = 650f,
        AttackWait = 120, FreezeWait = 300, DamageRadius = 100f, DamageHeight = 80f, AttackRadius = 105f, AttackHeight = 60f,
        TurnSpeedLow = 3f, TurnSpeedHigh = 5f, BodyScaleLow = 1f, BodyScaleHigh = 1f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 5f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 2.5f, MarchSpeedHigh = 3.5f,
        BoundNum = 3, BoundVal = 0.5f, LifeTimer = 800, FlyGravityY = 0.25f, PowerFromWater = 30f,
    };
    protected override Params P => Prm;
    public const float LandAngle = 90f, BoundVelocityY = 0.8f;                           // TTobiPuku::mLandAngle / mBoundVelocityY
    public const float DiveDepthGame = 600f, RollDepth = 120f, ReachXZ = 200f;           // the Land nerve's 600 and 120, isReachedToGoalXZ's 200
    public const string RollJoint = "center", BubbleJoint = "chn9";                        // joint 1 (the roll callback), joint 6 (PUKU_AWA)

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>Leaper = TTobiPuku with its launch pad: THIS node is the pad -- place it in the water, at the surface, facing the way the fish should fly
    /// (the node's yaw); the fish rises from 300 u under it and leaps, lands, swims back and leaps again. Swimmer = TPukuPuku: it swims under the surface
    /// from point to point (a Graph of Node3D children, or within WanderRadiusUnits of its place) and hurts a swimming Mario it touches.</summary>
    [Export] public Kinds Kind = Kinds.Leaper;
    [ExportGroup("Launch pad (the Leaper)")]
    /// <summary>The pad's launchPower: the leap's speed in units a tick along (yaw, up) before the pitch (the scene value; 30 u/tick clears about 5 m).</summary>
    [Export(PropertyHint.Range, "5,80,0.5")] public float LaunchSpeedUnits = 30f;
    /// <summary>The pad's X rotation: how steep the leap is, degrees up from level (90 = straight up).</summary>
    [Export(PropertyHint.Range, "10,90,1")] public float LaunchPitchDegrees = 70f;
    /// <summary>mReturnLaunchSw (on = the game): a fish that splashes down loops under, swims back to the pad and leaps again; off = it sinks and dies, and the
    /// pad launches a new one every LaunchIntervalTicks.</summary>
    [Export] public bool ReturnsToPad = true;
    /// <summary>The pad's LaunchInterval (300 = 5 s), used only with ReturnsToPad off.</summary>
    [Export(PropertyHint.Range, "30,3000,10")] public int LaunchIntervalTicks = 300;
    [ExportGroup("Deviations (the game's value = the game)")]
    /// <summary>How deep the splashed-down fish dives before it swims back, units (the game: 600 = 8 m). A pool's floor ends the dive early whatever this says.</summary>
    [Export(PropertyHint.Range, "60,1200,10")] public float DiveDepthUnits = DiveDepthGame;
    /// <summary>mBoundSw (on = the game): a fish that lands on the ground bounces (up to BoundNum 3 times) before it flops; in the air it bounces off walls.</summary>
    [Export] public bool Bounces = true;
    /// <summary>x the swim / walk speed (the game: 2.5..3.5 u/tick, x 1.5 swimming).</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>TEffectColumWater where it breaks the surface, leaving or arriving. Empty = assets/Sms/Fx/WaterSplashSmall.tscn.</summary>
    [Export] public PackedScene WaterColumnFxScene;
    /// <summary>PARTICLE_MS_PUKU_AWA: the bubbles trailing a fish swimming back to its pad (a looping GPUParticles3D scene bound at the chn9 joint). Empty = none.</summary>
    [Export] public PackedScene SwimBubbleFxScene;
    /// <summary>PARTICLE_MS_PUKU_PICHI: the drops flying off a fish flopping on the ground (a looping GPUParticles3D scene bound at the body). Empty = none.</summary>
    [Export] public PackedScene FlopFxScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_TOBIPUKU_FRWATER (the leap out), _TOWATER (the splash down), MSD_SE_EN_COMMON_FLY (hosed), the bas tables' pitipiti and swim sounds.</summary>
    [Export] public AudioStreamPlayer3D LeapSound, SplashSound, HitWaterSound, FlopSound, SwimSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's 11 bcks, the game's indices)
    private static readonly string[] Names = { "pukupuku_attack", "pukupuku_death", "pukupuku_down_air", "pukupuku_down_land", "pukupuku_fall", "pukupuku_fall_end_land",
        "pukupuku_jump", "pukupuku_jump_start", "pukupuku_pitipiti", "pukupuku_swim", "pukupuku_wait" };
    private static readonly int[] Frames = { 14, 174, 14, 20, 15, 29, 15, 9, 35, 60, 60 };
    private static readonly bool[] Loops = { false, false, false, false, true, false, true, false, true, true, true };
    protected const int BckAttack = 0, BckDeath = 1, BckDownAir = 2, BckDownLand = 3, BckFall = 4, BckFallEndLand = 5, BckJump = 6, BckJumpStart = 7, BckPitiPiti = 8, BckSwim = 9, BckWait = 10;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves (TNerveTobiPuku*)
    protected const int NervePGenerate = NerveSpecies, NerveFly = NerveSpecies + 1, NervePAttack = NerveSpecies + 2, NerveHitWater = NerveSpecies + 3, NerveFall = NerveSpecies + 4,
        NervePitiPiti = NerveSpecies + 5, NervePDie = NerveSpecies + 6, NerveLand = NerveSpecies + 7, NerveBound = NerveSpecies + 8, NervePrepareFly = NerveSpecies + 9,
        NerveReturnLaunch = NerveSpecies + 10, NerveSwimWander = NerveSpecies + 11;
    private static readonly string[] PukuNerveNames = { "Generate", "Fly", "Attack", "HitWater", "Fall", "PitiPiti", "Die", "Land", "Bound", "PrepareFly", "ReturnLaunch", "SwimWander" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + PukuNerveNames.Length ? PukuNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => Kind == Kinds.Swimmer ? NerveSwimWander : NervePGenerate;

    // ------------------------------------------------------------------------------------------------------------------ state
    private bool _flying;                          // unk194: FlyGravityY
    private int _bounds;                           // unk198: the bounces so far
    private bool _killDeath = true, _bouncing;     // unk1AD (the death from a kill / the fall, not the life timer) / unk1AE (bouncing)
    private float _apexY;                          // unk1B0: the pad's height (the water line), then the height a bounce or a hit starts from
    private float _launchPitch;                    // unk1B4: the launch velocity's pitch
    private Vector3 _landPos;                      // unk1B8[0]
    private Vector3 _launch;                       // unk1D0: the launch velocity (the pad's), then the bounce's
    private float _swimY, _vy, _pitchStep, _roll, _pitchEase;   // unk1E0 / unk1E4 / unk1E8 / unk1EC / unk1F0
    private float _diveDepth = DiveDepthGame;      // the dive this splashdown (the game's 600, or what the pool allows)
    private float _pitch;                          // mRotation.x, degrees: 0 level, positive nose down, 180 facing back (MsGetRotFromZaxis)
    private Vector3 _padPos;                       // unk1DC->mPosition: the pad (the placed point), units
    private bool _onWater, _groundIsWater;          // mGroundPlane->isWaterSurface() at the landing / this tick
    private float _waterSurfU;
    private uint _planeMask = 5;
    private Skeleton3D _skel; private SmsBoneRoll _rollBone; private Node3D _bubbleFx, _flopFx;
    /// <summary>For the drives.</summary>
    public string CurrentNerveName => NerveName(_cur);
    public bool Flying => _flying;
    public bool Bouncing => _bouncing;
    public int BounceCount => _bounds;
    public float PitchDegrees => _pitch;
    public float RollDegrees => _roll;
    public bool OnWater => _onWater;
    public float LaunchPitch => _launchPitch;
    public Vector3 LaunchVel => _launch;
    public bool Dying => IsNerve(NervePDie);
    public int ColumnBursts { get; private set; }
    public int Leaps { get; private set; }
    public int Splashdowns { get; private set; }

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _flags150 = 0x31;
        _planeMask = CollisionMask;
        if (_model != null) foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        if (_skel != null && _skel.FindBone(RollJoint) >= 0 && !Engine.IsEditorHint()) { _rollBone = new SmsBoneRoll { Name = "Roll", BoneName = RollJoint }; _skel.AddChild(_rollBone); }
        if (WaterColumnFxScene == null && ResourceLoader.Exists("res://assets/Sms/Fx/WaterSplashSmall.tscn")) WaterColumnFxScene = GD.Load<PackedScene>("res://assets/Sms/Fx/WaterSplashSmall.tscn");
        if (DebugPrint) GD.Print($"[SMS puku] {Name}: kind {Kind}; skel {_skel?.Name} roll {_rollBone != null}");
    }

    /// <summary>TTobiPuku::reset (+ the pad's forceLaunch for the leaper: the pad = the placed point, the launch velocity from the knobs; TPukuPuku::reset: SwimWander).</summary>
    protected override void Reset()
    {
        base.Reset();
        _turnSpeed = Prm.TurnSpeedLow;                                               // moveObject sets it every tick
        _marchSpeed *= SpeedScale;
        _flying = false; _bounds = 0; _killDeath = true; _bouncing = false; _roll = 0f; _pitch = 0f; _pitchStep = 0f; _pitchEase = 0f; _vy = 0f;
        _onWater = false; _groundIsWater = false;
        _padPos = _spawnPos; _apexY = _spawnPos.Y; _landPos = Pos(); _swimY = Pos().Y;
        _launch = PadVelocity(_spawnYaw);
        _launchPitch = PitchOf(_launch);
        CollisionMask = _planeMask;
        BoundFx(ref _bubbleFx, null, false, Vector3.Zero);
        BoundFx(ref _flopFx, null, false, Vector3.Zero);
        SetBck(Kind == Kinds.Swimmer ? BckSwim : BckJump);
        ApplyLook();
    }
    /// <summary>TTobiPukuLaunchPad::forceLaunch: (sin yaw, 1, cos yaw) x launchPower, then x (cos pitch, sin pitch, cos pitch).</summary>
    private Vector3 PadVelocity(float yawDeg)
    {
        float sy = Mathf.Sin(Mathf.DegToRad(yawDeg)), cy = Mathf.Cos(Mathf.DegToRad(yawDeg));
        float cp = Mathf.Cos(Mathf.DegToRad(LaunchPitchDegrees)), sp = Mathf.Sin(Mathf.DegToRad(LaunchPitchDegrees));
        return new Vector3(sy * LaunchSpeedUnits * cp, LaunchSpeedUnits * sp, cy * LaunchSpeedUnits * cp);
    }
    /// <summary>MsGetRotFromZaxis(v).x: the turn about X from +Z to v, degrees (rising negative, falling positive).</summary>
    private static float PitchOf(Vector3 v)
    {
        float h = MathF.Sqrt(v.X * v.X + v.Z * v.Z);
        if (h < 1e-6f && MathF.Abs(v.Y) < 1e-6f) return 0f;
        return Mathf.RadToDeg(MathF.Atan2(-v.Y, h));
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (!_dead && (_onWater || _noBind)) _airborne = false;                      // the step's airborne flag: no floor under a floating body (the Bloopers' rule)
    }
    protected override void MoveObjectPre()
    {
        if (_onWater || _noBind) _airborne = false;
        if (_onWater && !_noBind) _vel = Vector3.Zero;                              // at rest on the surface (the floor landing's zero, which no floor gave)
        CollisionMask = _noBind ? 0 : _planeMask;                                   // UNK10: the game's bind-less fish ignores the map
    }
    /// <summary>setBehavior (after the bind): a falling body meeting a WATER surface this tick lands on it (the game's water is ground): airborne off, the
    /// velocity the gap, <c>_onWater</c>; the fx bound to the states.</summary>
    protected override void SetBehavior()
    {
        _groundIsWater = false;
        if (!_noBind && _airborne && _vel.Y <= 0f && WaterSurfaceAt(GlobalPosition, out float surfM, 2f))
        {
            float surfU = surfM * U;
            if (surfU > GroundAt(Pos()) + 1f && Pos().Y + _vel.Y <= surfU + 0.05f)
            {
                _vel = new Vector3(0f, surfU - Pos().Y, 0f);
                _waterSurfU = surfU;
                _airborne = false; _onWater = true; _groundIsWater = true;
                OnLandedWater();
            }
        }
        BoundFx(ref _bubbleFx, SwimBubbleFxScene, IsNerve(NerveReturnLaunch) && !_dead, BubbleOffsetM());
        BoundFx(ref _flopFx, FlopFxScene, IsBck(BckPitiPiti) && !_airborne && !_dead, Vector3.Up * (Prm.DamageHeight * 0.5f / U));
    }
    private Vector3 BubbleOffsetM()
    {
        if (_skel != null) { int b = _skel.FindBone(BubbleJoint); if (b >= 0) return ToLocal((_skel.GlobalTransform * _skel.GetBoneGlobalPose(b)).Origin); }
        return Vector3.Up * (Prm.DamageHeight * 0.5f / U);
    }
    protected virtual void OnLandedWater() { }
    /// <summary>gpMap->checkGround at a point: the floor under it (the map, not the water), units (-1e9 none).</summary>
    private float GroundAt(Vector3 pU)
    {
        var space = GetWorld3D().DirectSpaceState;
        Vector3 from = (pU + Vector3.Up * (Prm.HeadHeight * 2f)) / U;
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 80f, _planeMask);
        var excl = new Godot.Collections.Array<Rid> { GetRid() };
        if (_mario != null && IsInstanceValid(_mario)) excl.Add(_mario.GetRid());
        q.Exclude = excl;
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? ((Vector3)hit["position"]).Y * U : -1e9f;
    }
    private void ColumnWater(float atU)
    {
        Vector3 at = new Vector3(GlobalPosition.X, atU / U, GlobalPosition.Z);
        if (SpawnBurst(WaterColumnFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), at)) != null) ColumnBursts++;
    }
    private void BoundFx(ref Node3D slot, PackedScene scene, bool on, Vector3 localPos)
    {
        if (on && slot == null && scene != null)
        {
            slot = scene.Instantiate<Node3D>();
            AddChild(slot);
            slot.Scale = Vector3.One * _scale;
        }
        else if (!on && slot != null) { slot.QueueFree(); slot = null; }
        if (slot != null) slot.Position = localPos;
    }

    /// <summary>calcRootMatrix: the model turned by the pitch about its side axis under the heading, lifted 70 x pitch / mLandAngle; the roll on 'center' about the body axis.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        if (_pitch == 0f) { base.ApplyLook(); }
        else
        {
            Basis b = new Basis(Vector3.Up, Mathf.DegToRad(_yaw)) * new Basis(Vector3.Right, Mathf.DegToRad(_pitch)) * Basis.FromScale(LookScale * _modelScale);
            Vector3 at = GlobalPosition + Vector3.Up * (70f * _pitch / LandAngle / U);
            if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, at); else _model.Transform = new Transform3D(b, Vector3.Up * (70f * _pitch / LandAngle / U));
        }
        if (_rollBone != null)
        {
            _rollBone.WorldAxis = _model.GlobalBasis.Z.Normalized();
            _rollBone.Degrees = _roll;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ the game's virtuals
    protected override void SetWalkAnm() => SetBck(BckSwim);
    protected override void SetRunAnm() => SetBck(BckSwim);
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetDeadAnm() => SetBck(BckDeath);
    protected override float GravityY() => _flying ? Prm.FlyGravityY : base.GravityY();
    protected override bool WallHopAllowed() => false;
    protected override bool IsCollidMove(Node3D other) => false;                       // isInhibitedForceMove while airborne; a fish pushes nothing
    /// <summary>TTobiPuku::walkBehavior: the walk, then the bob y = unk1E0 + 10 sin(2 x time) and the pitch from the position delta.</summary>
    protected override void WalkBehavior(int mode, float k)
    {
        base.WalkBehavior(mode, k);
        float prevY = Pos().Y;
        float y = _swimY + 10f * Mathf.Sin(Mathf.DegToRad(2f * _time));
        GlobalPosition = new Vector3(GlobalPosition.X, y / U, GlobalPosition.Z);
        Vector3 v = _lin; v.Y = y - prevY;
        _pitch = PitchOf(v);
    }
    /// <summary>TTobiPuku::attackToMario: the ATTACK; not attacking, not bouncing and Mario not in the water -> unk1D0 = 0, the Attack nerve.</summary>
    protected override void AttackToMario()
    {
        SendAttackMsgToMario();
        if (_cur != NervePAttack && !_bouncing && !MarioInWater) { _launch = Vector3.Zero; PushNerve(NervePAttack); }
    }
    /// <summary>TTobiPuku::behaveToWater: EN_COMMON_FLY and HitWater unless in it.</summary>
    protected override void BehaveToWater()
    {
        if (_cur == NerveHitWater) return;
        HitWaterSound?.Play();
        PushNerve(NerveHitWater);
    }
    /// <summary>TTobiPuku::kill: the hit points 1; already dying from a kill -> nothing; else unk1AD, the spine reset to Die.</summary>
    public override void Kill()
    {
        if (_dead) return;
        _hp = 1;
        if (_cur == NervePDie && _killDeath) return;
        _killDeath = true;
        ResetSpine();
        SetNext(NervePDie);
    }
    /// <summary>hitWall (moveObject, airborne with mBoundSw): the velocity reflected about the wall's normal in XZ, vy x 0.5, unk1D0 = it, unk1B0 = y.</summary>
    protected override void OnWallContact(KinematicCollision3D col)
    {
        if (!Bounces || !_airborne || _noBind) return;
        Vector3 n = col.GetNormal(); n.Y = 0f;
        if (n.LengthSquared() < 1e-6f) return;
        n = n.Normalized();
        float dot = _vel.X * n.X + _vel.Z * n.Z;
        if (dot >= 0f) return;
        _vel.X += -2f * dot * n.X; _vel.Z += -2f * dot * n.Z; _vel.Y *= 0.5f;
        _launch = _vel; _apexY = Pos().Y;
    }
    private void RoofCheck()
    {
        if (!Bounces || !_airborne || _noBind || _vel.Y <= 0f) return;
        var space = GetWorld3D().DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(GlobalPosition, GlobalPosition + Vector3.Up * (Prm.HeadHeight * 1.5f / U), _planeMask);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (space.IntersectRay(q).Count > 0) _vel.Y = 0f;
    }

    // ------------------------------------------------------------------------------------------------------------------ the pieces
    /// <summary>flyStart: unk198 = 0, FlyGravityY on, the velocity = unk1D0, unk1B4 = its pitch, the column, airborne, the bind back.</summary>
    private void FlyStart()
    {
        _bounds = 0; _flying = true; _vel = _launch; _launchPitch = PitchOf(_launch);
        ColumnWater(_apexY);
        LeapSound?.Play();
        _airborne = true; _noBind = false; _onWater = false;
        CollisionMask = _planeMask;
        Leaps++;
        if (DebugPrint) GD.Print($"[SMS puku] {Name}: leaps at {_launch} u/tick (pitch {_launchPitch:F0}) from {Pos()}");
    }
    /// <summary>fallStart: FlyGravityY off, the XZ velocity zeroed, +2 u, airborne.</summary>
    private void FallStart()
    {
        _flying = false;
        _vel = new Vector3(0f, _vel.Y, 0f);
        GlobalPosition += Vector3.Up * (2f / U);
        _airborne = true;
    }
    /// <summary>hitWater: flung from Mario at PowerFromWater (y x 2), unk1D0 = it, unk1B0 = y, the heading 180 - Mario's angle.</summary>
    private void HitWater()
    {
        Vector3 m = _mario != null ? _mario.SmsFeetWorld() * U : Pos();
        Vector3 d = Pos() - m;
        if (d.X == 0f && d.Y == 0f && d.Z == 0f) d.X = 1f;
        d = d.Normalized();
        _vel = new Vector3(d.X * Prm.PowerFromWater, 2f * d.Y * Prm.PowerFromWater, d.Z * Prm.PowerFromWater);
        _launch = _vel; _apexY = Pos().Y;
        if (_mario != null) { Vector3 f = _mario.SmsFacingDir(); f.Y = 0f; if (f.LengthSquared() > 1e-6f) _yaw = Wrap360(180f - YawOf(f)); }
    }
    /// <summary>bound: unk1AE, unk198++, the velocity = unk1D0.xz x BoundVal, vy = mBoundVelocityY x BoundVal x (unk1B0 - the ground) / 30, airborne.</summary>
    private void Bound()
    {
        _bouncing = true;
        if (_bounds < Prm.BoundNum)
        {
            _bounds++;
            Vector3 v = _launch;
            v.X *= Prm.BoundVal; v.Z *= Prm.BoundVal;
            v.Y = BoundVelocityY * Prm.BoundVal * (_apexY - _groundY) / 30f;
            _launch = v; _vel = v; _airborne = true;
        }
    }
    private bool CanBound()
    {
        if (_bounds < Prm.BoundNum) return true;
        _bouncing = false;
        return false;
    }
    private bool ReachedPadXZ()
    {
        Vector3 d = _padPos - Pos(); d.Y = 0f;
        return d.LengthSquared() < ReachXZ * ReachXZ;
    }
    /// <summary>The leaper's own default nerve after a death: the pad launches again (mReturnLaunchSw: at once; else after LaunchInterval).</summary>
    protected override void Relaunch()
    {
        if (Kind == Kinds.Leaper && !ReturnsToPad && _relaunch <= LaunchIntervalTicks) return;
        base.Relaunch();
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NervePGenerate:                                 // TNerveTobiPukuGenerate: the rise from 300 u under the pad
                if (_time == 0)
                {
                    _noBind = true; CollisionMask = 0; _onWater = false; _airborne = false;
                    GlobalPosition = new Vector3(_padPos.X, _padPos.Y - 300f, _padPos.Z) / U;
                    _pitch = PitchOf(_launch);
                    SetBck(BckJump);
                    SetCollisionLayerValue(1, false);                                  // under the water, nothing to hit
                }
                GlobalPosition += Vector3.Up * (_launch.Y / U);
                if (Pos().Y > _apexY)
                {
                    SetCollisionLayerValue(1, true);
                    FlyStart();
                    PushAfterCurrent(NerveFly);
                    return true;
                }
                return false;

            case NerveFly:                                       // TNerveTobiPukuFly
                if (_time == 0) { if (IsBck(BckJumpStart)) SetBck(BckJumpStart); _noBind = false; CollisionMask = _planeMask; }
                if (BckEnded() && IsBck(BckJumpStart)) SetBck(BckJump);
                RoofCheck();
                if (!_airborne) { PushAfterCurrent(NerveLand); return true; }
                _vy = _vel.Y;
                _pitch = PitchOf(_vel);
                return false;

            case NervePAttack:                                   // TNerveTobiPukuAttack
                if (_time == 0) SetBck(BckAttack);
                if (_airborne)
                {
                    if (BckFrame() >= 6f) FallStart();
                    if (BckEnded()) { PushAfterCurrent(NerveFall); return true; }
                    return false;
                }
                return true;

            case NerveHitWater:                                  // TNerveTobiPukuHitWater
                if (_time == 0)
                {
                    if (_airborne) { if (Pos().Y - _groundY > 50f) { SetBck(BckAttack); HitWater(); } }
                    else if (_bouncing) SetBck(BckPitiPiti);
                }
                if (!_airborne && !_noBind)
                {
                    Vector3 m = _mario != null ? _mario.SmsFeetWorld() * U : Pos();
                    Vector3 d = new Vector3(Pos().X - m.X, 0f, Pos().Z - m.Z);
                    if (d.X == 0f && d.Z == 0f) d.X = 1f;
                    d = d.Normalized();
                    _vel = new Vector3(d.X * 5f, 5f, d.Z * 5f);
                    _airborne = true; _onWater = false;
                    GlobalPosition += Vector3.Up * (5f / U);
                }
                if (BckEnded())
                {
                    if (IsBck(BckAttack)) PushAfterCurrent(NerveFall);
                    return true;
                }
                return false;

            case NerveFall:                                      // TNerveTobiPukuFall
                if (_time == 0) { _pitch = 0f; SetBck(BckFall); }
                if (!_airborne)
                {
                    if (_groundIsWater || _onWater) ColumnWater(_waterSurfU);
                    _noReward = true;                                                    // LIVE_FLAG_UNK20000
                    PushAfterCurrent(NervePDie);
                    return true;
                }
                return false;

            case NervePitiPiti:                                  // TNerveTobiPukuPitiPiti: the flop on the ground
                if (_time == 0) { SetBck(BckPitiPiti); FlopSound?.Play(); }
                if (BckEnded() && _time > Prm.LifeTimer) { _killDeath = false; PushAfterCurrent(NervePDie); return true; }
                return false;

            case NervePDie:                                      // TNerveTobiPukuDie
                if (_time == 0)
                {
                    if (_airborne) { SetCollisionLayerValue(1, false); _vel = new Vector3(0f, _vel.Y, 0f); SetBck(BckDownAir); }
                    else if (_killDeath) { SetCollisionLayerValue(1, false); SetBck(BckDownLand); }
                    else { _noReward = true; SetBck(BckDeath); }
                    BoundFx(ref _flopFx, null, false, Vector3.Zero);
                    BoundFx(ref _bubbleFx, null, false, Vector3.Zero);
                }
                if (BckEnded() || _time > 400)
                {
                    SetCollisionLayerValue(1, false);
                    GenRandomItem();
                    Dead();
                    return true;
                }
                return false;

            case NerveLand:                                      // TNerveTobiPukuLand
                if (_time < 2)
                {
                    if (_onWater)
                    {
                        if (_time == 0)
                        {
                            GlobalPosition += Vector3.Down * (10f / U);
                            _noBind = true; CollisionMask = 0; _flying = false;
                            ColumnWater(_waterSurfU); SplashSound?.Play(); Splashdowns++;
                            if (ReturnsToPad)
                            {
                                _vy *= 0.8f;
                                if (_vy >= -0.5f) _vy = -0.5f;                                      // (a flat splash: the game divides by |600 / vy|)
                                float floor0 = GroundAt(Pos());                                     // DEVIATION: the dive is as deep as the pool allows (the game's 600 u)
                                _diveDepth = floor0 > -1e8f ? Mathf.Clamp(_waterSurfU - floor0 - 30f, 40f, DiveDepthUnits) : DiveDepthUnits;
                                _pitchStep = (180f - _pitch) / MathF.Abs(_diveDepth / _vy);
                            }
                            if (DebugPrint) GD.Print($"[SMS puku] {Name}: splashes down at {Pos()} vy {_vy:F1} pitch {_pitch:F0}; the dive {_diveDepth:F0} u");
                        }
                    }
                    else
                    {
                        if (Bounces && CanBound()) { PushAfterCurrent(NerveBound); return true; }
                        if (_time == 0) { _landPos = Pos(); SetBck(BckFallEndLand); _pitch = 0f; _flying = false; }
                    }
                    return false;
                }
                if (IsBck(BckFallEndLand))
                {
                    // (the game blends the position over 20 ticks from unk1B8[0] by unk1B8[1], which the decomp never sets: not ported)
                    if (BckEnded()) { PushAfterCurrent(NervePitiPiti); return true; }
                    return false;
                }
                if (ReturnsToPad)
                {
                    // the dive by hand (UNK10): the XZ halves each tick and follows cos(pitch), the pitch steps to 180, the roll past 120 u (the dive's
                    // depth is unk1E0 - y: the water line it entered at)
                    float dy = _waterSurfU - Pos().Y;
                    _launch.X *= 0.5f; _launch.Z *= 0.5f;
                    _pitch = Mathf.Clamp(_pitch + _pitchStep, 0f, 180f);
                    float c = Mathf.Cos(Mathf.DegToRad(_pitch));
                    Vector3 v = new Vector3(_launch.X * c, _vy, _launch.Z * c);
                    if (dy + MathF.Abs(v.Y) > _diveDepth) v.Y = -(_diveDepth - dy);                              // the last step lands on the dive's floor
                    _lin += v;
                    if (dy > RollDepth * MathF.Min(1f, _diveDepth / DiveDepthGame)) _roll = Mathf.Clamp(_roll + 3f, 0f, 180f);
                    if (dy >= _diveDepth - 0.5f) { PushAfterCurrent(NerveReturnLaunch); return true; }
                    return false;
                }
                // no return: the sink
                _lin += Vector3.Down * 12f;
                if (IsBck(BckJump) && _pitch < LandAngle) _pitch += 1.2f;
                if (_time > 100) { Dead(); return true; }
                return false;

            case NerveBound:                                     // TNerveTobiPukuBound
                if (_time == 0) Bound();
                if (_vel.Y > 0f) _apexY = Pos().Y;
                RoofCheck();
                if (!_airborne) { PushAfterCurrent(NerveLand); return true; }
                return false;

            case NervePrepareFly:                                // TNerveTobiPukuPrepareFly: 60 ticks easing to the pad, the pitch to the launch's, the roll out
                if (_time == 0) _pitchEase = (Wrap360(_launchPitch) - _pitch) / 60f;
                GlobalPosition += (_padPos - Pos()) / U * (1f / 60f);
                _roll = Mathf.Clamp(_roll - 3f, 0f, 180f);
                _pitch += _pitchEase;
                if (_time == 50 && IsBck(BckJumpStart)) SetBck(BckJumpStart);
                if (_time > 60)
                {
                    // the pad's forceLaunch + reset: the fish at the pad, the velocity from the knobs -> Generate from 300 u under
                    GlobalPosition = _padPos / U; _yaw = _spawnYaw;
                    Reset();
                    return true;
                }
                return false;

            case NerveReturnLaunch:                              // TNerveTobiPukuReturnLaunch: the swim back to the pad
                if (_time == 0) { SetGoalPoint(_padPos); SetBck(BckSwim); _swimY = Pos().Y; SwimSound?.Play(); }
                if (ReachedPadXZ()) { PushAfterCurrent(NervePrepareFly); return true; }
                {
                    Vector3 diff = _padPos - Pos(); diff.Y = 0f;
                    Vector3 dir = diff.LengthSquared() > 1e-6f ? diff.Normalized() : Vector3.Forward;
                    _launch.X *= 0.99f; _launch.Z *= 0.99f;
                    _lin += new Vector3(dir.X * _marchSpeed - _launch.X, 0f, dir.Z * _marchSpeed - _launch.Z);
                    _yaw = YawOf(dir);
                    _roll = Mathf.Clamp(_roll + 1f, 0f, 180f);
                }
                return false;

            case NerveSwimWander:                                // TNerveTobiPukuSwimWander (TPukuPuku)
                if (_time == 0)
                {
                    _swimY = Pos().Y; SetBck(BckSwim); GoToRandomNextGraphNode();
                    _noBind = true; CollisionMask = 0; _onWater = false; _airborne = false;
                }
                if (IsReachedToGoal()) GoToRandomNextGraphNode();
                WalkBehavior(0, 1.5f);
                return false;
        }
        return base.ExecuteNerve(n);
    }
    /// <summary>isReachedToGoalXZ: within 200 u of the goal in XZ.</summary>
    protected override bool IsReachedToGoal()
    {
        Vector3 d = Goal() - Pos(); d.Y = 0f;
        return d.LengthSquared() < ReachXZ * ReachXZ;
    }
}
