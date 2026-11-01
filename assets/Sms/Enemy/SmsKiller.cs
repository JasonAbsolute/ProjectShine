using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 147 -- the Bullet Bill (TKiller : TFlyEnemy : TWalkerEnemy, Enemy/killer.cpp, Killer.hpp) on the small-enemy base, with the shipped
/// /enemy/killer.prm: the shots of the Monty Mole cannon's three barrels (pass 146's <see cref="SmsCannon.KillerScene"/> slot).
///
/// THE GAME. TFlyEnemy: reset = the fly timer 0, the mode NEUTRAL, mChaseGravityY = NormalFlyGravityY, homing off. getGravityY = mChaseGravityY in
/// CHASEFLY, else NormalFlyGravityY. bind: in CHASEFLY or under mInvalidTime 200 ticks -> fly(): pos += mVelocity (never slowed: no gravity on it) +
/// Mario's speed / mTestMarioSpMax 12 (it drifts with him) and pos.y += mChaseGravityY (a drift a tick, up or down); the ground check (the water
/// surface is ground); at or under it past 200 ticks -> landed (airborne off, the velocity 0); else TLiveActor::bind (gravity on the velocity, the
/// landing). TKiller::bind: the timer++; not exploding: landed past 200 ticks -> EXPLOSION; past 200 ticks touching a wall (2 x BodyRadius) ->
/// EXPLOSION; airborne: KILLER_FLY, the pitch clamped -25 .. 90, KIL_SMOKE bound to the body (the trail). NORMALFLY (the default): tick 0
/// setNormalFlyAnm (killer_model1, run1, the roll angle 0, the timer 0); force-chase past 500 ticks (never set for a cannon's) or, not gold, isFindMario
/// (|dy| under SearchHeight 2000 and in sight: 3000 u, 90 deg, aware 10 -- no airborne test) past 100 ticks, within ChaseDist 10000 -> CHASEFLY after;
/// the pitch = the velocity's; the scale x 1.05 a tick up to the body scale (the cannon fires it at 0.1: it pops in). CHASEFLY: tick 0 mLaunchVelocity =
/// the velocity, calcChaseParam, setChaseFlyAnm (search1); the speed k = not homing: |launch velocity with y 0.1| / march^2 x mTestSp 2.5; homing in
/// DIVE: 2; else 1; walkBehavior(2, k) (the turn and the march toward the goal); homing and the goal reached in XZ, or DIVE under the ground + 100 ->
/// calcChaseParam again; DIVE / NEUTRAL -> walkBehavior(3, 1) (the turn), HOMING -> flyMove (the velocity x 0.9, the turn at the turn speed, the
/// march along the heading: a second march); not HOMING over the ground + 200 -> the pitch of this tick's delta with y = the gravity, else the
/// pitch x 0.99; flyBehavior: the turn speed TurnSpeedLow 0.4, past ChaseTimer 3000 ticks mChaseGravityY -= WaterAddGravityY 10 a tick (it sinks and
/// blows), search1 done -> chase1, the roll angle + 2.5 (mRollSw is off in the shipped game: the body never rolls); the scale x 1.1. calcChaseParam:
/// diff = Mario - pos with XZ x 1.1, the goal = pos + diff (set only when homing); Mario over it by more than 100 or within 100: homing -> HOMING (the
/// gravity ChaseFlyGravityY 0.1 unless just sprayed), else pos.y -= 1; otherwise (Mario under it by 100+): the gravity NormalFlyGravityY, the velocity
/// zeroed; not HOMING (or diff.y over 150) -> DIVE with the gravity ForceGravityY -3.5 (the NormalFlySpeed 40 XZ velocity the game computes here is
/// zeroed two lines on); else pos.y -= 3. attackToMario (Mario's feet under its origin only): not exploding -> EXPLOSION + the ATTACK (dmgkiller:
/// 3 HP, FIRE_DOWN); exploding -> BURN. isHitValid: UNKB -> dead, coins; else mTrampleDie -> mChaseGravityY -= 12 (the stomp knocks it down); never
/// valid (Mario's hits do not kill it). isCollidMove: mSerialBomb and chasing -> EXPLOSION (any actor it touches). behaveToWater: flying -> genEventCoin
/// (2 coins; 8 gold: 30 u out at even angles, (3 dir, 20, 3 dir)), then EXPLOSION with NO_COLLISION (a harmless burst). EXPLOSION: tick 0 the bomb
/// scale max = BombRange 300 x the body scale / AttackRadius 120, the pitch 0, setDeadAnm (downkiller_model1 down1, TEffectExplosion at the body's
/// scale, a shake, a rumble); landed on water -> the bomb water column x 2, on sand -> the sand column (0.6, 0.9, 0.6); then the collision scale x 1.3 a
/// tick up to the max (expandCollision: the blast's reach), then NO_COLLISION and, the clip done, dead (y -= 200), DIE queued (it drops nothing: the
/// dead flag is already on). Colours (calcRootMatrix, one GX_TEVREG0 per material): plain black; gold (5 % at reset: the body (200, 185, 0), the pieces
/// (255, 225, 70), run1 the nose and eyes (170, 140, 0), search1 the nose flashing (170, 140, 0) / (180, 140, 150) every 5 ticks, chase1 the body
/// (170, 140, 0) and the nose waving); homing (setColorType: the body and the pieces (70, 20, 70); search1 the nose and eyes flashing red (200) /
/// purple, chase1 the nose waving red to purple; gold is never homing). The pitch clamped -25 .. 90 (nose up a little, nose down to straight).
///
/// THE PORT. <c>_noBind</c> with <see cref="FlyBind"/>: the base moves the body by this tick's <c>_lin</c> only; the species' bind adds the flight
/// (Fly / LiveBind) each tick, reads the floor with the base's ground ray (the water surface through WaterSurfaceAt) and the step's wall and body
/// contacts (OnWallContact / OnBodyContact) for the explosion past the invalid time. The pieces are a second glb (<c>Down</c>) swapped in at the
/// explosion; the clips run through the base's bck table (slot 0 = the pieces' down1 on the Down player). The four materials are the species' shader
/// (KillerBody.gdshader: TEX x the light + C0 + the sphere-map glint; the pieces' vertex colour x the light + C0), duplicated per body at Init so
/// each Bill writes its own C0 colours. A placed Bill (no cannon) flies off the way it faces at <see cref="LaunchSpeedUnits"/> toward a point far
/// ahead (the cannon gives its own led goal); <see cref="Kind"/> forces plain / homing / gold (Auto = the game's roll: 5 % gold, homing only from the
/// cannon's 1 in 5). Knobs (grouped, plain English): Kind, Launch, Deviations (SpeedScale, RollSw, TrampleDie, SerialBomb), Effects (the blast =
/// Bomb/BombExplosion.tscn, the smoke trail / water column / sand column slots, the coin), Sounds (the fly loop, the explosion). DEVIATIONS (logged):
/// the trail, the columns and the sounds are slots; the shake and the rumble are not ported; the roll is off (the game's static) but offered; the
/// force-chase (a subclass's) is not ported; UNKB (the juice / Yoshi) and changeOut are not ported.
/// </summary>
public partial class SmsKiller : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ killer.prm
    public class KillerParams : Params
    {
        public float NormalFlyGravityY = 0.2f, NormalFlySpeed = 10f, ChaseFlyGravityY = 0.1f, ChaseDist = 2000f, ForceGravityY = 0.1f, WaterAddGravityY = 1f, BombRange = 300f;
        public int ChaseTimer = 1000;
    }
    /// <summary>The shipped /enemy/killer.prm (params.szs on his disc, sms_shipped_params.json). Gravity = NormalFlyGravityY: what a launcher's
    /// calcVelocityToJumpToY reads through GravityUnits (the game passes killer->getGravityY()).</summary>
    public static readonly KillerParams Prm = new KillerParams
    {
        HeadHeight = 120f, BodyRadius = 6f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 10, ActiveEnemyNum = 10, JumpForce = 10f, Gravity = 0.03f,
        SearchLength = 3000f, SearchHeight = 2000f, SearchAware = 10f, SearchAngle = 90f, GiveUpLength = 3000f, GiveUpHeight = 1200f, AttackWait = 120, FreezeWait = 300,
        DamageRadius = 130f, DamageHeight = 100f, AttackRadius = 120f, AttackHeight = 120f, TurnSpeedLow = 0.4f, TurnSpeedHigh = 0.4f, BodyScaleLow = 1f, BodyScaleHigh = 1f,
        GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100, StampRange = 5f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 3.8f, MarchSpeedHigh = 3.8f,
        NormalFlyGravityY = 0.03f, NormalFlySpeed = 40f, ChaseFlyGravityY = 0.1f, ChaseDist = 10000f, ForceGravityY = -3.5f, WaterAddGravityY = 10f, ChaseTimer = 3000, BombRange = 300f,
    };
    protected override Params P => Prm;
    /// <summary>TFlyEnemy's statics: mTestSp 2.5, mInvalidTime 200, mTestMarioSpMax 12.</summary>
    public const float TestSp = 2.5f, TestMarioSpMax = 12f;
    public const int InvalidTime = 200;
    public enum FlyMode { Dive = 0, Neutral = 1, Homing = 2 }
    public enum KillerKind { Auto, Plain, Homing, Gold }

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Kind")]
    /// <summary>Auto = the game's roll (1 in 20 gold; homing only when a cannon says so, 1 in 5 of its shots). Plain / Homing / Gold force one.</summary>
    [Export] public KillerKind Kind = KillerKind.Auto;
    [ExportGroup("Launch (a placed one; a cannon gives its own)")]
    /// <summary>How fast a placed Bill flies off the way it faces, in units a tick (the cannon's shots leave at 4 to 6).</summary>
    [Export(PropertyHint.Range, "0,40,0.1")] public float LaunchSpeedUnits = 6f;
    /// <summary>Its climb at the launch, in units a tick (0 = level).</summary>
    [Export(PropertyHint.Range, "-10,10,0.1")] public float LaunchRiseUnits = 0f;
    [ExportGroup("Deviations (1 / off = the game)")]
    /// <summary>x the chase's turn (TurnSpeedLow 0.4 deg a tick) and march (3.8 u a tick).</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    /// <summary>mRollSw: the body rolling about its length in the chase (2.5 deg a tick). Off in the shipped game.</summary>
    [Export] public bool RollSw = false;
    /// <summary>mTrampleDie: a stomp knocks it down (its drift -12 u a tick: it hits the ground and blows). On in the game.</summary>
    [Export] public bool TrampleDie = true;
    /// <summary>mSerialBomb: a chasing one blows up on any enemy it touches. On in the game.</summary>
    [Export] public bool SerialBomb = true;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>TEffectExplosion at the body's scale when it blows. Empty = assets/Sms/Enemy/Bomb/BombExplosion.tscn.</summary>
    [Export] public PackedScene BlastFxScene;
    /// <summary>ms_kil_smoke: the smoke trail bound to the body while it flies (a looping effect node, kept as a child). Slot.</summary>
    [Export] public PackedScene SmokeTrailFxScene;
    /// <summary>The bomb water column (x 2) when it lands on water, the sand column (0.6, 0.9, 0.6) on sand. Slots.</summary>
    [Export] public PackedScene WaterColumnFxScene, SandColumnFxScene;
    /// <summary>The coins the hose knocks out of it (two; eight from a gold one). Empty = res://yellow_coin.tscn.</summary>
    [Export] public PackedScene CoinScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_KILLER_FLY (looped while it flies) and the blast (the game's is TEffectExplosion's).</summary>
    [Export] public AudioStreamPlayer3D FlySound, ExplodeSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's order; slot 0 is the pieces' clip on the Down player)
    private static readonly string[] Names = { "", "killer_chase1", "killer_run1", "killer_search1", "killer_zrot1" };
    private static readonly int[] Frames = { 89, 90, 60, 29, 45 };
    private static readonly bool[] Loops = { false, true, true, false, true };
    protected const int BckDown = 0, BckChase = 1, BckRun = 2, BckSearch = 3, BckZrot = 4;
    public const string DownClip = "downkiller_down1";
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveNormalFly = NerveSpecies, NerveChaseFly = NerveSpecies + 1, NerveExplosion = NerveSpecies + 2;
    protected override string NerveName(int n) => n == NerveNormalFly ? "NormalFly" : n == NerveChaseFly ? "ChaseFly" : n == NerveExplosion ? "Explosion" : base.NerveName(n);
    protected override int DefaultNerve => NerveNormalFly;

    // ------------------------------------------------------------------------------------------------------------------ state
    private Node3D _down; private AnimationPlayer _downAnim; private float _downScale = 1f; private bool _downPlaying;
    private int _flyTimer; private FlyMode _flyMode; private float _chaseGravityY; private bool _forceChase, _isHoming, _isGold; private Vector3 _launchVel;
    private float _rollAngle, _pitch, _bombScaleMax, _collScale = 1f;
    private Vector3 _body, _eyes, _nose, _downBody;                                              // the four GX_TEVREG0 colours, 0..255
    private readonly List<ShaderMaterial>[] _mats = { new(), new(), new(), new() };
    private readonly Vector3[] _matLast = { new(-1, 0, 0), new(-1, 0, 0), new(-1, 0, 0), new(-1, 0, 0) };
    private ulong _contactFrame; private bool _landedOnWater;
    private Node3D _trail;
    /// <summary>For the drives.</summary>
    public string CurrentNerveName => NerveName(_cur);
    public string FlyModeName => _flyMode.ToString();
    public int FlyTimer => _flyTimer;
    public bool Homing => _isHoming;
    public bool Gold => _isGold;
    public float ChaseGravityY => _chaseGravityY;
    public float PitchDeg => _pitch;
    public float RollDeg => _rollAngle;
    public float ScaleNow => _scale;
    public float CollScale => _collScale;
    public bool Exploding => IsNerve(NerveExplosion);
    public bool PiecesShown => _down != null && _down.Visible;
    public int CoinsMade { get; private set; }
    public Vector3 BodyColor => _body;
    public Vector3 NoseColor => _nose;
    public Vector3 GoalUnits => Goal();

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _flags150 = 0x11;
        _down = GetNodeOrNull<Node3D>("Down");
        if (_down != null) { _downScale = _down.Scale.X; foreach (Node n in _down.FindChildren("*", "AnimationPlayer", true, false)) { _downAnim = n as AnimationPlayer; if (_downAnim != null) break; } }
        if (BlastFxScene == null && ResourceLoader.Exists(SmsBombHei.ExplosionFxPath)) BlastFxScene = GD.Load<PackedScene>(SmsBombHei.ExplosionFxPath);
        if (CoinScene == null && ResourceLoader.Exists("res://yellow_coin.tscn")) CoinScene = GD.Load<PackedScene>("res://yellow_coin.tscn");
        OwnMaterials(_model); OwnMaterials(_down);
        if (SmokeTrailFxScene != null) { _trail = SmokeTrailFxScene.Instantiate<Node3D>(); AddChild(_trail); _trail.Position = Vector3.Up * (60f / U); SetTrail(false); }
        if (DebugPrint) GD.Print($"[SMS killer] {Name}: materials body {_mats[0].Count} eyes {_mats[1].Count} nose {_mats[2].Count} pieces {_mats[3].Count}; down {_down?.Name} anim {_downAnim?.Name}");
    }
    /// <summary>SMS_InitPacket_OneTevColor x 4: each Bill owns its materials (a duplicate per surface) so its GX_TEVREG0 colours are its own.</summary>
    private void OwnMaterials(Node root)
    {
        if (root == null) return;
        foreach (Node n in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (n is not MeshInstance3D mi || mi.Mesh == null) continue;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                if (mi.GetActiveMaterial(i) is not ShaderMaterial m || m.Shader == null || !m.Shader.ResourcePath.EndsWith("KillerBody.gdshader")) continue;
                var d = (ShaderMaterial)m.Duplicate();
                mi.SetSurfaceOverrideMaterial(i, d);
                int part = d.GetShaderParameter("part").AsInt32();
                if (part >= 0 && part < 4) _mats[part].Add(d);
            }
        }
    }
    /// <summary>TKiller::reset: TFlyEnemy's (the timer, NEUTRAL, the drift NormalFlyGravityY, homing off), the colours black, 5 % gold.</summary>
    protected override void Reset()
    {
        base.Reset();
        _noBind = true; _airborne = true;
        _flyTimer = 0; _flyMode = FlyMode.Neutral; _forceChase = false; _chaseGravityY = Prm.NormalFlyGravityY; _isHoming = false;
        _rollAngle = 0f; _pitch = 0f; _collScale = 1f; _bombScaleMax = 1f; _downPlaying = false; _landedOnWater = false;
        _turnSpeed = Prm.TurnSpeedLow * SpeedScale; _marchSpeed = Prm.MarchSpeedLow * SpeedScale;
        _body = Vector3.Zero; _downBody = Vector3.Zero; _eyes = Vector3.Zero; _nose = Vector3.Zero;
        _isGold = Kind == KillerKind.Gold || (Kind == KillerKind.Auto && Rand() < 0.05f);
        if (_isGold) { _body = new Vector3(200, 185, 0); _downBody = new Vector3(255, 225, 70); }
        if (Kind == KillerKind.Homing) _isHoming = true;
        SmsSetColorType();
        if (_model != null) _model.Visible = true;
        if (_down != null) _down.Visible = false;
        _downAnim?.Stop();
        SetGoalPoint(Pos() + PolarXZ(_yaw, 3000f));                                              // a placed one flies at a point far ahead (the cannon sets its own goal)
        _vel = PolarXZ(_yaw, LaunchSpeedUnits) + Vector3.Up * LaunchRiseUnits;                  // (a launcher's SmsLaunch overwrites it)
        ApplyColors();
        ApplyLook();
    }
    protected override void LaunchVelocity() { }                                                // (Reset set it)

    // ------------------------------------------------------------------------------------------------------------------ for the cannon
    /// <summary>setHoming (the cannon's 1 in 5 before setColorType).</summary>
    public void SmsSetHoming(bool homing) { if (Kind == KillerKind.Auto || Kind == KillerKind.Homing) _isHoming = homing; }
    /// <summary>TKiller::setColorType: gold is never homing; a homing one wears purple (70, 20, 70) on the body and the pieces.</summary>
    public void SmsSetColorType()
    {
        if (_isGold) _isHoming = false;
        if (_isHoming) { _body = new Vector3(70, 20, 70); _downBody = new Vector3(70, 20, 70); }
        ApplyColors();
    }
    /// <summary>mScaling at the launch (the cannon's 0.1; a demo's 0.6): it grows back to the body scale in flight.</summary>
    public void SmsSetScale(float s) { _scale = s; ApplyLook(); }
    /// <summary>setGoal: the point it heads for (the cannon's led point near Mario), units.</summary>
    public void SmsSetGoalPoint(Vector3 units) => SetGoalPoint(units);

    // ------------------------------------------------------------------------------------------------------------------ the look
    protected override bool FlyBind => true;
    protected override bool WallHopAllowed() => false;
    protected override Vector3 LookScale => Vector3.One * _scale;
    protected override float GravityY() => IsNerve(NerveChaseFly) ? _chaseGravityY : Prm.NormalFlyGravityY;   // TFlyEnemy::getGravityY
    /// <summary>calcRootMatrix: the heading, the pitch (nose down positive, -25 .. 90) and, with RollSw in the chase, the roll about the length.</summary>
    protected override void ApplyLook()
    {
        float roll = RollSw && IsRollFly() ? _rollAngle : 0f;
        Basis b = Basis.FromEuler(new Vector3(Mathf.DegToRad(_pitch), Mathf.DegToRad(_yaw), Mathf.DegToRad(roll)));
        if (_model != null)
        {
            var t = new Transform3D(b * Basis.FromScale(LookScale * _modelScale), IsInsideTree() ? GlobalPosition : Vector3.Zero);
            if (IsInsideTree()) _model.GlobalTransform = t; else _model.Transform = t;
        }
        if (_down != null)
        {
            var t = new Transform3D(b * Basis.FromScale(LookScale * _downScale), IsInsideTree() ? GlobalPosition : Vector3.Zero);
            if (IsInsideTree()) _down.GlobalTransform = t; else _down.Transform = t;
        }
    }
    /// <summary>TKiller::isRollFly: chasing with the chase1 clip.</summary>
    private bool IsRollFly() => IsNerve(NerveChaseFly) && IsBck(BckChase);
    /// <summary>MsGetRotFromZaxis(v).x: the pitch that turns +Z onto v (up = negative), degrees.</summary>
    private static float PitchOf(Vector3 v) { float h = MathF.Sqrt(v.X * v.X + v.Z * v.Z); return h < 1e-6f && MathF.Abs(v.Y) < 1e-6f ? 0f : Mathf.RadToDeg(MathF.Atan2(-v.Y, h)); }

    // ------------------------------------------------------------------------------------------------------------------ the tick hooks
    protected override void OnWallContact(KinematicCollision3D col) { _contactFrame = Engine.GetPhysicsFrames(); }
    protected override void OnBodyContact(Node3D other) { _contactFrame = Engine.GetPhysicsFrames(); }
    private bool TouchedThisFrame => Engine.GetPhysicsFrames() - _contactFrame <= 1;
    /// <summary>TKiller::bind (after the spine): the flight, the timer, the explosion triggers, the fly sound and the trail.</summary>
    protected override void SetBehavior()
    {
        if (_dead) return;
        if (!IsNerve(NerveExplosion))
        {
            if (IsNerve(NerveChaseFly) || _flyTimer < InvalidTime) Fly(); else LiveBind();
            _flyTimer++;
            if (!_airborne && _flyTimer > InvalidTime) PushNerve(NerveExplosion);                                      // landed
            else if (_flyTimer > InvalidTime && TouchedThisFrame) PushNerve(NerveExplosion);                            // isTouchedWallsAndMoveXZ (the floor too)
            else if (SerialBomb && IsNerve(NerveChaseFly) && _touching.Count > 0) PushNerve(NerveExplosion);           // isCollidMove while chasing
            if (_airborne) _pitch = Mathf.Clamp(_pitch, -25f, 90f);
        }
        else { _vel = Vector3.Zero; _airborne = false; }
        UpdateColors();
        _pitch = Mathf.Clamp(_pitch, -25f, 90f);
        bool flying = _airborne && !IsNerve(NerveExplosion);
        if (FlySound != null && FlySound.Stream != null) { if (flying && !FlySound.Playing) FlySound.Play(); else if (!flying && FlySound.Playing) FlySound.Stop(); }
        SetTrail(flying);
    }
    private bool _trailOn = true;
    private void SetTrail(bool on)
    {
        if (_trail == null || on == _trailOn) return;
        _trailOn = on;
        if (_trail is GpuParticles3D gp) gp.Emitting = on; else _trail.Visible = on;
    }
    /// <summary>mPositionDelta / Mario's speed in units a tick (two game ticks a physics frame).</summary>
    private Vector3 MarioSpeedUnits() => _mario != null && IsInstanceValid(_mario) ? _mario.Velocity * U / 120f : Vector3.Zero;
    /// <summary>The ground under it, units: the base's floor ray, or the water surface when that is higher (mGroundPlane->isWaterSurface()).</summary>
    private float GroundUnits(out bool water)
    {
        water = false;
        float gy = _groundY;
        if (WaterSurfaceAt(GlobalPosition, out float s, 1.5f) && s * U > gy) { gy = s * U; water = true; }
        return gy;
    }
    /// <summary>TFlyEnemy::fly: the step = the velocity + Mario's speed / 12 + the drift; at or under the ground past the invalid time -> landed.</summary>
    private void Fly()
    {
        Vector3 step = _vel;
        Vector3 ms = MarioSpeedUnits();
        step.X += ms.X / TestMarioSpMax; step.Z += ms.Z / TestMarioSpMax;
        step.Y += _chaseGravityY;
        float gy = GroundUnits(out bool water) + 1f;
        if (Pos().Y + step.Y <= gy)
        {
            if (_flyTimer > InvalidTime) { _airborne = false; _vel = Vector3.Zero; step.Y = gy - Pos().Y; _landedOnWater = water; }
        }
        else _airborne = true;
        _lin += step;
    }
    /// <summary>TLiveActor::bind (NORMALFLY past the invalid time): gravity on the velocity (terminal -40), the landing.</summary>
    private void LiveBind()
    {
        if (_airborne) { _vel.Y -= GravityY(); if (_vel.Y < -40f) _vel.Y = -40f; }
        Vector3 step = _vel;
        float gy = GroundUnits(out bool water) + 1f;
        if (Pos().Y + step.Y <= gy) { _airborne = false; _vel = Vector3.Zero; step.Y = gy - Pos().Y; _landedOnWater = water; }
        else _airborne = true;
        _lin += step;
    }

    // ------------------------------------------------------------------------------------------------------------------ TFlyEnemy
    /// <summary>TKiller::isFindMario: |dy| under SearchHeight and in sight (k x length / angle / aware) -- no airborne, water or demo test.</summary>
    protected override bool IsFindMario(float k)
    {
        if (_mario == null) return false;
        Vector3 mp = MarioPos();
        if (MathF.Abs(mp.Y - Pos().Y) >= P.SearchHeight) return false;
        return IsInSight(mp, P.SearchLength * k, P.SearchAngle * k, P.SearchAware * k);
    }
    private bool WithinChaseDist() { Vector3 d = MarioPos() - Pos(); return d.LengthSquared() < Prm.ChaseDist * Prm.ChaseDist; }
    private bool IsReachedToGoalXZ() { Vector3 d = Goal() - Pos(); d.Y = 0f; return d.Length() < 100f; }
    /// <summary>TFlyEnemy::calcChaseParam.</summary>
    private void CalcChaseParam()
    {
        Vector3 p = Pos(), diff = MarioPos() - p;
        diff.X *= 1.1f; diff.Z *= 1.1f;
        if (_isHoming) SetGoalPoint(p + diff);
        if (diff.Y > 100f || MathF.Abs(diff.Y) < 100f)
        {
            if (_isHoming)
            {
                if (_flyMode != FlyMode.Homing && _sprayCooldown == 0) _chaseGravityY = Prm.ChaseFlyGravityY;
                _flyMode = FlyMode.Homing;
            }
            else _lin.Y -= 1f;                                                                   // mPosition.y -= 1
            return;
        }
        _chaseGravityY = Prm.NormalFlyGravityY;
        if (_flyMode != FlyMode.Homing || diff.Y > 150f) { _flyMode = FlyMode.Dive; _chaseGravityY = Prm.ForceGravityY; }   // (the NormalFlySpeed velocity: zeroed below, as the game's)
        else _lin.Y -= 3f;                                                                       // mPosition.y -= 3
        _vel = Vector3.Zero;
    }
    /// <summary>TFlyEnemy::flyMove: the velocity x 0.9, the turn toward the goal at the turn speed, the march along the heading.</summary>
    private void FlyMove()
    {
        _vel *= 0.9f;
        Vector3 d = Goal() - Pos();
        float delta = AngleDiff(Wrap360(YawOf(d)), _yaw);
        delta = delta > 0f ? MathF.Min(delta, _turnSpeed) : MathF.Max(delta, -_turnSpeed);
        _yaw = Wrap360(_yaw + delta);
        _lin += PolarXZ(_yaw, _marchSpeed);
    }
    /// <summary>TKiller::flyBehavior: the turn speed, the chase timer's sink, search1 -> chase1, the roll.</summary>
    private void FlyBehavior()
    {
        _turnSpeed = Prm.TurnSpeedLow * SpeedScale;
        if (_time > Prm.ChaseTimer) _chaseGravityY -= Prm.WaterAddGravityY;
        if (BckEnded() && IsBck(BckSearch)) SetBck(BckChase);
        _rollAngle += 2.5f;
    }
    /// <summary>setNormalFlyAnm: the live model, run1, the roll 0, the timer 0.</summary>
    private void SetNormalFlyAnm() { if (_model != null) _model.Visible = true; if (_down != null) _down.Visible = false; SetBck(BckRun); _rollAngle = 0f; _flyTimer = 0; }
    private void SetChaseFlyAnm() => SetBck(BckSearch);

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveNormalFly:                                 // TNerveFlyEnemyNormalFly
                if (_time == 0) SetNormalFlyAnm();
                if (_forceChase && _flyTimer > 500) { if (WithinChaseDist()) { PushAfterCurrent(NerveChaseFly); return true; } }
                else if (!_isGold && IsFindMario(1f) && _flyTimer > 100) { if (WithinChaseDist()) { PushAfterCurrent(NerveChaseFly); return true; } }
                _pitch = PitchOf(_vel);
                _scale = Mathf.Clamp(1.05f * _scale, 0f, _bodyScale);
                return false;

            case NerveChaseFly:                                  // TNerveFlyEnemyChaseFly
                if (_time == 0) { _launchVel = _vel; CalcChaseParam(); SetChaseFlyAnm(); }
                {
                    float speed = 1f;
                    if (!_isHoming) { float m2 = _marchSpeed * _marchSpeed; _launchVel.Y = 0.1f; speed = m2 > 0f ? _launchVel.Length() / m2 * TestSp : 1f; }
                    else if (_flyMode == FlyMode.Dive) speed = 2f;
                    WalkBehavior(2, speed);
                    if ((_isHoming && IsReachedToGoalXZ()) || (_flyMode == FlyMode.Dive && Pos().Y < 100f + _groundY)) CalcChaseParam();
                    switch (_flyMode)
                    {
                        case FlyMode.Dive: case FlyMode.Neutral: WalkBehavior(3, 1f); break;
                        case FlyMode.Homing: FlyMove(); break;
                    }
                    if (_flyMode != FlyMode.Homing && Pos().Y > 200f + _groundY) { Vector3 delta = _lin; delta.Y = GravityY(); _pitch = PitchOf(delta); }
                    else _pitch *= 0.99f;
                    FlyBehavior();
                    _scale = Mathf.Clamp(1.1f * _scale, 0f, _bodyScale);
                }
                return false;

            case NerveExplosion:                                 // TNerveKillerExplosion
                if (_time == 0)
                {
                    _bombScaleMax = Prm.BombRange * _bodyScale / Prm.AttackRadius;
                    _pitch = 0f;
                    SetDeadAnm();
                    if (!_airborne)
                    {
                        if (_landedOnWater && WaterColumnFxScene != null) SpawnBurst(WaterColumnFxScene, new Transform3D(Basis.FromScale(Vector3.One * 2f), GlobalPosition));
                        else if (!_landedOnWater && OnSand() && SandColumnFxScene != null) SpawnBurst(SandColumnFxScene, new Transform3D(Basis.FromScale(new Vector3(0.6f, 0.9f, 0.6f)), GlobalPosition));
                    }
                    _vel = Vector3.Zero; _lin = Vector3.Zero;
                }
                if (_collScale < _bombScaleMax) _collScale *= 1.3f;                              // expandCollision: the blast's reach grows
                else
                {
                    SetCollisionLayerValue(1, false);                                            // onHitFilter(NO_COLLISION)
                    if (DownClipEnded() || _time > 400) { Dead(); return true; }                 // dead (y -= 200), DIE queued: nothing dropped
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }
    /// <summary>TKiller::setDeadAnm: the pieces (downkiller_model1 down1) in place of the body, TEffectExplosion at the body's scale (a shake and a
    /// rumble in the game).</summary>
    protected override void SetDeadAnm()
    {
        if (_model != null) _model.Visible = false;
        if (_down != null) _down.Visible = true;
        if (_downAnim != null && _downAnim.HasAnimation(DownClip))
        {
            var clip = _downAnim.GetAnimation(DownClip);
            clip.LoopMode = Animation.LoopModeEnum.None;
            float rate = clip.Length > 0f ? Frames[BckDown] / (float)clip.Length : 60f;
            _downAnim.Play(DownClip); _downAnim.Seek(0f, true); _downAnim.SpeedScale = 60f / rate;
            _downPlaying = true;
        }
        _bck = BckDown;
        SpawnBurst(BlastFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
        if (ExplodeSound != null && ExplodeSound.Stream != null) ExplodeSound.Play();
    }
    private bool DownClipEnded() => !_downPlaying || _downAnim == null || !_downAnim.IsPlaying();
    /// <summary>mGroundPlane->isSand(): the floor collider in the sms_sand group or named with "sand" (the Cataquack's rule).</summary>
    private bool OnSand()
    {
        var q = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * 0.3f, GlobalPosition + Vector3.Down * 1.5f, CollisionMask, new Godot.Collections.Array<Rid> { GetRid() });
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count == 0) return false;
        for (Node n = hit["collider"].As<Node>(); n != null; n = n.GetParent())
            if (n.IsInGroup("sms_sand") || n.Name.ToString().Contains("sand", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ------------------------------------------------------------------------------------------------------------------ hits
    protected override Mario.SmsDamageProfile DamageProfile => Mario.SmsDamageProfile.Killer;
    /// <summary>The blast: the attack cylinder at the collision scale (expandCollision) while the lists are on; else the base's pair test.</summary>
    protected override void HitCheckMario()
    {
        if (IsNerve(NerveExplosion))
        {
            if (_mario == null || !GetCollisionLayerValue(1)) return;
            float s = _bodyScale * _collScale;
            if (CheckDistance(Pos(), P.AttackRadius * s, P.AttackHeight * s, MarioPos(), SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight)) AttackToMario();
            return;
        }
        base.HitCheckMario();
    }
    /// <summary>TKiller::attackToMario: his feet under its origin only -- not exploding: EXPLOSION and the ATTACK (3 HP); exploding: BURN.</summary>
    protected override void AttackToMario()
    {
        if (_mario == null || MarioPos().Y >= Pos().Y) return;
        if (!IsNerve(NerveExplosion)) { PushNerve(NerveExplosion); SendAttackMsgToMario(); }
        else _mario.TakeDamage(GlobalPosition, Mario.SmsDamageProfile.Fire);
    }
    /// <summary>TKiller::isHitValid: never (nothing of Mario's kills it); mTrampleDie -> the drift -12 (a stomp knocks a chasing one down).</summary>
    protected override bool IsHitValid(HitMessage msg) { if (TrampleDie) _chaseGravityY -= 12f; return false; }
    protected override bool IsCollidMove(Node3D other) => true;
    /// <summary>TKiller::behaveToWater: flying -> the coins; then EXPLOSION with NO_COLLISION (a harmless burst), the velocity 0.</summary>
    protected override void BehaveToWater()
    {
        if (IsNerve(NerveNormalFly) || IsNerve(NerveChaseFly)) GenEventCoin();
        if (!IsNerve(NerveExplosion)) { PushNerve(NerveExplosion); SetCollisionLayerValue(1, false); _vel = Vector3.Zero; }
    }
    /// <summary>TKiller::genEventCoin: 2 coins (8 gold) 30 u out at even angles, each flying (3 dir, 20, 3 dir).</summary>
    private void GenEventCoin()
    {
        if (CoinScene == null || !IsInsideTree()) return;
        int count = _isGold ? 8 : 2;
        var table = new SmsDropTable();
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.DegToRad(360f / count * (i + 1));
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 at = GlobalPosition + dir * (30f / U);
            if (table.SpawnItem(CoinScene, GetParent(), at, new Vector3(3f * dir.X, 20f, 3f * dir.Z), 0, this) != null) CoinsMade++;
        }
    }
    /// <summary>kill (a demo's killEnemiesWithin aside): the explosion.</summary>
    public override void Kill() { if (!_dead && !IsNerve(NerveExplosion)) PushNerve(NerveExplosion); }
    protected override void Dead()
    {
        base.Dead();
        if (_down != null) _down.Visible = false;
        _downAnim?.Stop(); _downPlaying = false;
        SetTrail(false);
        if (FlySound != null && FlySound.Playing) FlySound.Stop();
    }

    // ------------------------------------------------------------------------------------------------------------------ the colours (calcRootMatrix)
    private void UpdateColors()
    {
        if (IsBck(BckRun))
        {
            if (_isGold) { _nose = new Vector3(170, 140, 0); _eyes = new Vector3(170, 140, 0); }
            else { _eyes = Vector3.Zero; _nose = Vector3.Zero; }
        }
        if (IsBck(BckSearch))
        {
            _nose.Y = 0f; _nose.Z = 0f; _eyes.X = 0f;
            bool a = _time % 10 < 5;
            if (a) { _nose.X = 0f; _eyes.Y = 0f; _eyes.Z = 0f; }
            if (_isGold) _nose = a ? new Vector3(170, 140, 0) : new Vector3(180, 140, 150);
            if (_isHoming) { if (a) { _nose.X = 200f; _eyes.X = 200f; _nose.Y = 0f; _nose.Z = 0f; } else _nose = new Vector3(70, 20, 70); }
        }
        if (IsBck(BckChase))
        {
            _eyes = Vector3.Zero; _nose.Y = 0f; _nose.Z = 0f;
            float wave = MathF.Abs(MathF.Sin(MathF.Tau * _time / 120f));
            if (_isGold) { _body = new Vector3(170, 140, 0); _nose = new Vector3(10f * wave + 160f, 30f * wave + 140f, 150f * wave); }
            if (_isHoming) { _body = new Vector3(70, 20, 70); _downBody = new Vector3(70, 20, 70); _nose = new Vector3(130f * wave + 70f, 20f - 20f * wave, 70f - 70f * wave); }
        }
        ApplyColors();
    }
    private void ApplyColors()
    {
        ApplyTev(0, _body); ApplyTev(1, _eyes); ApplyTev(2, _nose); ApplyTev(3, _downBody);
    }
    private void ApplyTev(int part, Vector3 c)
    {
        if (_matLast[part] == c) return;
        _matLast[part] = c;
        Vector3 v = c / 255f;
        foreach (var m in _mats[part]) m.SetShaderParameter("tev_color", v);
    }
}
