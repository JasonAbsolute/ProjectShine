using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// MONTY MOLE IN ITS CANNON -- TCannon + TChorobei + TCannonDom (Enemy/cannon.cpp; JP 砲台 / チョロベー, the cannon archive) on <see cref="SmsSmallEnemy"/>
/// with the shipped /enemy/cannon.prm (SMS pass 146, feature/enemy-pinna). Pinna Beach's three-barrelled cannon with the Monty Mole on top: it turns
/// to face Mario; within BombDist the mole lobs a Bob-omb at him every BombInterval; beyond it (within KillerDist) the barrels fire Bullet Bills;
/// come within HideDist and the mole ducks into the cannon until Mario is 3 x HideDist away. A Bob-omb that blows up on the mole (Mario throwing one
/// of its own back) costs it one of its three hit points; at none the mole collapses and the cannon blows, left as a prop.
///
/// THE GAME. TCannon is a TSmallEnemy (ACTOR_TYPE_DPT_CANNON, unk150 17, LIVE_FLAG_UNK10: no bind; isCollidMove false, isInhibitedForceMove, no hits
/// valid on itself, SPRAYED_BY_WATER taken and ignored) with /enemy/cannon.prm: 3 hit points, HeadHeight 120, damage / attack 200 x 500 (never
/// used: attackToMario is TSmallEnemy's but the cannon's own cylinders stay in the enemy filter), TurnSpeedLow 0.5, HideDist 500, BombDist 3000,
/// KillerDist 20000, BombInterval 200, KillerInterval 100, ShootInterval 500, the mole's attack 200 x 300 / damage 200 x 250, KillerTransYOffset 0,
/// BombHeiGenerateRate 1.0 (every throw a Bob-omb; the other rolls were Popos and Strollin' Stus), ThrowXZSpeed 9; the statics mChorobeiJntIdx 4 (the
/// mole's hit point = the cannon's joint 4 less 100 u), mVelocityRate 0.62, mSearchRate 0.02. The mole (TChorobei, a THitActor in the enemy group)
/// is a TSharedParts of tyorobe_model1 at the cannon's root matrix + 300 u, hidden while unk70; its bck table is the archive's (12 appear1 59, 13
/// damage1 59, 14 down1 189, 15 hyde1 34, 16 throw1 74, 17 wait1_end 29, 18 wait1_loop 60, 19 wait1_start 29); checkHit each tick: Mario -> ATTACK,
/// a Bob-omb with isDamageToCannon (in its Thrown or Explosion nerve) -> hitHead -> DAMAGE and the bomb killed, a rolling Bullet Bill -> DAMAGE and
/// killed. The three domes (TCannonDom, cannon_dom.bmd) sit on the joints nullC / nullB / nullA with map collision; beyond BombDist they wobble (a
/// 1-deg-a-tick phase, -20 / -10 x sin). Open: the mole appear1 + cannonopen; both done -> SEARCH. SEARCH: tick 0 the goal Mario when within
/// BombDist; the mole wait1_start -> wait1_loop; within HideDist -> CLOSE; within BombDist past BombInterval -> SHOOT (the bomb); else within
/// KillerDist past ShootInterval -> SHOOT x 3 (the Bullet Bills), FORCEBOMBSHOOT, SEARCH queued; the yaw changing -> CANNON_MOVE; turnToGoal =
/// walkToCurPathNode(0, mTurnSpeed, 0). SHOOT (the bomb): the mole wait1_end; its end -> throw1 + bombSet (the Bob-omb made at the mole's point,
/// held -- the cannon's taking matrix is the mole's hand -- at scale 0, its clip frozen); throw1 past frame 26 bombScaleUp (+0.2 of its scale a
/// tick); passing frame 38 bombShoot: the velocity = the XZ direction to Mario x ThrowXZSpeed with vy ThrowXZSpeed (the +-30 deg yaw roll is computed
/// and never applied), +2 u, airborne, the clip running, PUT; throw1's end -> SEARCH. SHOOT (a Bullet Bill, unk290 off): setKillerGoalPoint (a point 500 u
/// from Mario at a random angle, the dome's cannondom clip); ticks < 40 turnToGoal; tick 40 killerShoot (the Bill at the dome's joint 1 + (0, -60,
/// 150), calcVelocityToJumpToY(Mario + a 300-u jitter, 5, its gravity) x mVelocityRate toward where his speed puts him, 1 in 5 homing, KILLER_FIRE +
/// _FLY); past KillerInterval -> the next dome. FORCEBOMBSHOOT: Mario within sqrt 2 x BombDist -> the bomb throw again. CLOSE: ticks 0-1 deadCannon
/// (no collision), the mole hyde1, CHO_ASE (the sweat) 300 u up; the mole's hyde1 done -> cannonclose; its frame 10 -> JUMP_ED_B x 1.5 at 290 u;
/// Mario past 3 x HideDist -> the collision back, OPEN. DAMAGE: tick 0 BOMB_SMOKE + BOMB_BOMB + BOMB_HAHEN x 2 at the cannon; decHitPoints; 0 ->
/// killShootAct (the held bomb killed), the mole damage1, a rumble, CHO_MOKU_B at the mole's joint 12 + CHO_MOKU_A, the demo camera, deadCannon;
/// else damage(): setFreezeAnm, the mole damage1 + CHO_MOKU_A at its joint 0; the cannon hops (vy 4, +10 u, airborne; moveObject brings it back down
/// to its placed height); the mole's clip done -> 0 hit points ? DAMAGEDEMO : SEARCH. DAMAGEDEMO: the mole down1; past 120 ticks with the clip done
/// -> cannon_break, CANNON_DOWN, TEffectExplosion x 2.5, MSF_MONTY_MOLE_DEFEATED -> OBJECT. OBJECT: cannon_break held at its end, the mole hidden;
/// frame 60 CANNON_LAND; past 150 gateOpen (the Mare stage's gate).
///
/// THE PORT. The cannon is the body (its yaw the base's; no bind, no step); the mole, the three domes and the thrown bomb are nodes it drives. The
/// hits: Mario's damage cylinder in the mole's 200 x 300 -> TakeDamage (the base's cylinders are off: touching the cannon hurts nothing); any
/// SmsBombHei in the mole's 200 x 250 that is Thrown or Exploding -> DAMAGE and the bomb killed, its collision layer off first (isDamageToCannon's
/// HIT_FILTER_NO_COLLISION: a bomb hits once, though its 80-frame blast outlives the 59-frame damage1). The bomb: <see cref="BombScene"/> (the Bob-omb of
/// pass 130) instanced dormant at throw1's start and launched (SmsLaunch) from the mole's hand at frame 38; it lands and lives its own life. The
/// Bullet Bill is a slot (<see cref="KillerScene"/>: any SmsSmallEnemy scene is launched from the dome at the game's velocity; empty = the long-range
/// volleys are skipped -- DEVIATION: TKiller is not ported). The cannon's own clips (cannonopen / cannonclose / cannon_break) and the domes'
/// (cannondom / cannondom_break) are not in his rip's glbs (the rip merged no bck into default.glb or cannon_dom.glb): the lid stays, the hide is
/// the mole's hyde1 + its model off, the break is the mole and the domes off with the blast (DEVIATION, logged). The damage hop and the dome wobble
/// are the model's; the Mare gate, the demos and the rumbles are not ported.
/// </summary>
public partial class SmsCannon : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ cannon.prm
    public class CannonParams : Params
    {
        public float HideDist = 300f, BombDist = 2000f, KillerDist = 10000f, ChorobeiAttackRadius = 100f, ChorobeiAttackHeight = 100f, ChorobeiDamageRadius = 100f, ChorobeiDamageHeight = 100f,
            KillerTransYOffset = -50f, BombHeiGenerateRate = 0.7f, ThrowXZSpeed = 12f;
        public int BombInterval = 100, KillerInterval = 50, ShootInterval = 100;
    }
    /// <summary>The shipped /enemy/cannon.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly CannonParams Prm = new CannonParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 50f, ClipRadius = 400f, HitPointMax = 3, InstanceNum = 1, ActiveEnemyNum = 1, JumpForce = 5f,
        SearchLength = 2000f, SearchHeight = 800f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 1100f, GiveUpHeight = 650f, AttackWait = 0, FreezeWait = 0,
        DamageRadius = 200f, DamageHeight = 500f, AttackRadius = 200f, AttackHeight = 500f, TurnSpeedLow = 0.5f, TurnSpeedHigh = 0.5f, BodyScaleLow = 1f, BodyScaleHigh = 1f,
        GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        HideDist = 500f, BombDist = 3000f, KillerDist = 20000f, BombInterval = 200, KillerInterval = 100, ShootInterval = 500,
        ChorobeiAttackRadius = 200f, ChorobeiAttackHeight = 300f, ChorobeiDamageRadius = 200f, ChorobeiDamageHeight = 250f, KillerTransYOffset = 0f, BombHeiGenerateRate = 1f, ThrowXZSpeed = 9f,
    };
    protected override Params P => Prm;
    /// <summary>TChorobei's place: the cannon's root + 300 u (its hit point 150 u under that); the statics mVelocityRate 0.62 and mSearchRate 0.02.</summary>
    public const float MoleUpUnits = 300f, MoleHitDownUnits = 150f, VelocityRate = 0.62f, SearchRate = 0.02f;
    public const int DamageDemoTicks = 120, StopOnAirTimeMax = 40;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the cannon's turn (TurnSpeedLow 0.5 deg a tick in the game) and the bomb's throw speed.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    /// <summary>How far up the cannon the mole sits (the game: the root matrix + 300 u, its hit point 150 u under).</summary>
    [Export] public float MoleHeightUnits = MoleUpUnits;
    /// <summary>The Monty Mole's hit points (3 in the game: three Bob-ombs blown up on it).</summary>
    [Export(PropertyHint.Range, "1,10,1")] public int HitPoints = 3;
    [ExportGroup("Shots (empty = none)")]
    /// <summary>The Bob-omb the mole lobs (an SmsSmallEnemy scene). Empty = assets/Sms/Enemy/Bomb/BombHei.tscn.</summary>
    [Export] public PackedScene BombScene;
    /// <summary>The Bullet Bill the barrels fire at long range (any SmsSmallEnemy scene, launched from the dome at the game's velocity). Empty = no long-range
    /// volleys (TKiller is not ported).</summary>
    [Export] public PackedScene KillerScene;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>The Bob-omb's blast where a bomb damages the mole (x 2: its Smoke, Glow and Scraps only, the game's three emitters -- the bomb's own fireball is
    /// already there) and at the cannon's end (x 2.5, the whole thing). Empty = assets/Sms/Enemy/Bomb/BombExplosion.tscn.</summary>
    [Export] public PackedScene BlastFxScene;
    /// <summary>ms_cho_moku_a / _b: the smoke off the hit mole; ms_cho_ase: the sweat as it hides; ms_jump_ed_b: the lid's puff. Slots.</summary>
    [Export] public PackedScene MoleSmokeFxScene, SweatFxScene, LidPuffFxScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_CANNON_MOVE (the turn), _DOWN (the break), _LAND, the mole's bas tables (appear / damage / down / hyde / throw).</summary>
    [Export] public AudioStreamPlayer3D MoveSound, DownSound, LandSound, AppearSound, DamageSound, HideSound, ThrowSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the cannon has none in his rip; the mole's 8 by the archive's indices)
    private static readonly string[] Names = { "cannonclose", "cannondom", "cannondom_break", "cannonopen", "cannon_break", "cannon_demo_dom", "cannon_demo_dpt", "cannon_demo_pinna",
        "hodai_mario_crush1", "hodai_mario_crush2", "hodai_mario_fly1", "hodai_mario_fly2", "tyorobe_appear1", "tyorobe_damage1", "tyorobe_down1", "tyorobe_hyde1", "tyorobe_throw1",
        "tyorobe_wait1_end", "tyorobe_wait1", "tyorobe_wait1_start" };
    private static readonly int[] Frames = { 15, 30, 99, 15, 80, 379, 379, 379, 309, 309, 379, 379, 59, 59, 189, 34, 74, 29, 60, 29 };
    private static readonly bool[] Loops = { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false };
    protected const int BckMoleAppear = 12, BckMoleDamage = 13, BckMoleDown = 14, BckMoleHide = 15, BckMoleThrow = 16, BckMoleWaitEnd = 17, BckMoleWaitLoop = 18, BckMoleWaitStart = 19;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves (TNerveCannon*)
    protected const int NerveOpen = NerveSpecies, NerveSearch = NerveSpecies + 1, NerveShoot = NerveSpecies + 2, NerveForceBombShoot = NerveSpecies + 3, NerveClose = NerveSpecies + 4,
        NerveDamage = NerveSpecies + 5, NerveDamageDemo = NerveSpecies + 6, NerveObject = NerveSpecies + 7;
    private static readonly string[] CannonNerveNames = { "Open", "Search", "Shoot", "ForceBombShoot", "Close", "Damage", "DamageDemo", "Object" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + CannonNerveNames.Length ? CannonNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveSearch;

    // ------------------------------------------------------------------------------------------------------------------ state
    private Node3D _mole; private AnimationPlayer _moleAnim; private int _moleBck = -1; private float _moleRate = 60f; private bool _moleHidden;
    private readonly List<Node3D> _domes = new(); private int _dome;                       // unk1AC / unk214
    private bool _bombMode = true;                                                           // unk290: 1 the bomb throw, 0 the Bullet Bill volley
    private SmsSmallEnemy _shot;                                                             // unk1A4: the bomb being made
    private float _hopY, _hopVy;                                                             // the damage hop (moveObject's y / mVelocity.y)
    private float _lastYaw;
    private float _molePrev = -1f, _moleLast = -1f;
    /// <summary>For the drives.</summary>
    public string CurrentNerveName => NerveName(_cur);
    public bool MoleHidden => _moleHidden;
    public string MoleClip => _moleBck >= 0 && _moleBck < Names.Length ? Names[_moleBck] : "-";
    public float MoleFrame => MoleFrameNow();
    public Vector3 MoleUnits => Pos() + Vector3.Up * (MoleHeightUnits + _hopY);
    public Vector3 MoleHitUnits => MoleUnits - Vector3.Up * MoleHitDownUnits;
    public int Hits { get; private set; }
    public int Throws { get; private set; }
    public int Volleys { get; private set; }
    public SmsSmallEnemy LastBomb { get; private set; }
    public bool Broken => IsNerve(NerveObject);
    public float HopUnits => _hopY;

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _flags150 = 17;
        CollisionMask = 0;                                                                     // nothing pushes the cannon (a Bob-omb's body inside its shape shoved it a metre a blast); the Solid child is what Mario and the bombs meet
        _mole = _model?.GetNodeOrNull<Node3D>("Mole");                                         // the mole and the domes are the Model's children, in the glb's units: they turn and hop with it
        if (_mole != null) { _mole.Position = Vector3.Up * MoleHeightUnits; foreach (Node n in _mole.FindChildren("*", "AnimationPlayer", true, false)) { _moleAnim = n as AnimationPlayer; if (_moleAnim != null) break; } }
        for (int i = 0; i < 3; i++) { var d = _model?.GetNodeOrNull<Node3D>($"Dome{i}"); if (d != null) _domes.Add(d); }
        if (BombScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Bomb/BombHei.tscn")) BombScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Bomb/BombHei.tscn");
        if (BlastFxScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Bomb/BombExplosion.tscn")) BlastFxScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Bomb/BombExplosion.tscn");
        if (LidPuffFxScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Fx131/JumpEd.tscn")) LidPuffFxScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Fx131/JumpEd.tscn");
        if (DebugPrint) GD.Print($"[SMS cannon] {Name}: mole {_mole?.Name} anim {_moleAnim?.Name} clips {(_moleAnim != null ? _moleAnim.GetAnimationList().Length : 0)}; domes {_domes.Count}");
    }
    /// <summary>TCannon::reset: the hit points, the collision off (its own cylinders never hit anything), LIVE_FLAG_UNK10, the goal.</summary>
    protected override void Reset()
    {
        base.Reset();
        _hp = HitPoints;
        _noBind = true; _airborne = false;
        _dome = 1; _bombMode = true; _hopY = 0f; _hopVy = 0f; _lastYaw = _yaw; _shot = null;
        SetCollisionLayerValue(1, false);                                                  // onHitFilter(NO_COLLISION): the cannon body is no target; the mole's cylinders are its own
        SetMoleHidden(false);
        SetMoleBck(BckMoleWaitStart);
        SetGoalMario();
        ApplyLook();
    }
    protected override Vector3 LookScale => Vector3.One;
    protected override bool IsCollidMove(Node3D other) => false;
    protected override bool WallHopAllowed() => false;
    protected override bool FlyBind => true;                                               // no step: nothing moves the cannon
    protected override void AttackToMario() { }                                            // the cannon's own cylinders never hit (the mole's checkHit does)
    protected override bool IsHitValid(HitMessage msg) => false;                           // isHitVallid false: nothing of Mario's kills it
    protected override void BehaveToWater() { }                                            // SPRAYED_BY_WATER: taken, nothing happens
    public override void Kill() { if (!_dead && !IsNerve(NerveObject)) { ResetSpine(); SetNext(NerveDamageDemo); } }
    /// <summary>The cannon's model with the damage hop; the mole at the root + MoleHeightUnits, turned with the yaw.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Basis b = Basis.FromEuler(new Vector3(0f, Mathf.DegToRad(_yaw), 0f)) * Basis.FromScale(Vector3.One * _modelScale);
        Vector3 hop = Vector3.Up * (_hopY / U);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, GlobalPosition + hop);
    }

    // ------------------------------------------------------------------------------------------------------------------ the mole's clips (TChorobei::setBckAnm on its own MActor)
    private void SetMoleBck(int i, AudioStreamPlayer3D sound = null)
    {
        _moleBck = i; _molePrev = -1f; _moleLast = -1f;
        sound?.Play();
        if (_moleAnim == null || i < 0 || i >= Names.Length || !_moleAnim.HasAnimation(Names[i])) return;
        var clip = _moleAnim.GetAnimation(Names[i]);
        clip.LoopMode = Loops[i] ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        _moleRate = clip.Length > 0f ? Frames[i] / (float)clip.Length : 60f;
        _moleAnim.Play(Names[i]); _moleAnim.Seek(0f, true); _moleAnim.SpeedScale = 60f / _moleRate;
    }
    private bool IsMoleBck(int i) => _moleBck == i;
    private float MoleFrameNow()
    {
        if (_moleBck < 0 || _moleAnim == null || !_moleAnim.HasAnimation(Names[_moleBck]) || _moleAnim.CurrentAnimation != Names[_moleBck]) return _moleBck >= 0 ? Frames[_moleBck] : 0f;
        return (float)_moleAnim.CurrentAnimationPosition * _moleRate;
    }
    /// <summary>curAnmEndsNext for the mole's clip (the last 1.1 frames, or a loop seen to wrap).</summary>
    private bool MoleClipEnded()
    {
        if (_moleBck < 0 || _moleAnim == null || !_moleAnim.HasAnimation(Names[_moleBck])) return true;
        if (!Loops[_moleBck] && !_moleAnim.IsPlaying()) return true;
        return MoleFrameNow() + 1.1f >= Frames[_moleBck] || (Loops[_moleBck] && _moleLast >= 0f && _molePrev < _moleLast - 5f);
    }
    private bool MolePassed(float frame) => _moleLast >= 0f && _moleLast < frame && _molePrev >= frame;
    private void SetMoleHidden(bool hidden) { _moleHidden = hidden; if (_mole != null) _mole.Visible = !hidden; }
    private void SetDomesVisible(bool on) { foreach (var d in _domes) d.Visible = on; }

    // ------------------------------------------------------------------------------------------------------------------ the tick hooks
    protected override void MoveObjectPre()
    {
        _moleLast = _molePrev; _molePrev = MoleFrameNow();
        // moveObject's hop: y += vy, vy -= gravity, never under the placed height
        if (_hopY > 0f || _hopVy != 0f) { _hopY += _hopVy; _hopVy -= P.Gravity; if (_hopY <= 0f) { _hopY = 0f; _hopVy = 0f; } }
        if (!IsNerve(NerveObject) && !IsNerve(NerveClose) && !_dead) MoleCheckHit();
        if (_shot != null && !IsInstanceValid(_shot)) _shot = null;                        // (the dormant bomb in the hand is dead by design: only a freed one is dropped)
    }
    /// <summary>TChorobei::checkHit: Mario in the mole's attack cylinder -> ATTACK; a Bob-omb in its damage cylinder that is Thrown or Exploding
    /// (isDamageToCannon) -> hitHead: DAMAGE and the bomb killed.</summary>
    private void MoleCheckHit()
    {
        if (_moleHidden || (_tickCount & 3) != 0) return;
        Vector3 h = MoleHitUnits;
        if (_mario != null && CheckDistance(h, Prm.ChorobeiAttackRadius, Prm.ChorobeiAttackHeight, MarioPos(), SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight))
            SendAttackMsgToMario();
        if (IsNerve(NerveDamage) || IsNerve(NerveDamageDemo)) return;
        foreach (Node n in GetTree().GetNodesInGroup("sms_enemy"))
        {
            if (n is not SmsBombHei b || !IsInstanceValid(b) || b.IsDead || b == _shot) continue;
            if (!b.GetCollisionLayerValue(1)) continue;        // HIT_FILTER_NO_COLLISION: not in the hit lists (one that already hit the mole, or one in his hands)
            if (!(b.Thrown || b.Exploding)) continue;
            if (!CheckDistance(h, Prm.ChorobeiDamageRadius, Prm.ChorobeiDamageHeight, b.GlobalPosition * U, SmsBombHei.Prm.AttackRadius * b.BodyScale, SmsBombHei.Prm.AttackHeight * b.BodyScale)) continue;
            if (DebugPrint) GD.Print($"[SMS cannon] {Name}: a Bob-omb ({(b.Thrown ? "thrown" : "exploding")}) on the mole -> Damage");
            b.SetCollisionLayerValue(1, false);                // TBombHei::isDamageToCannon: onHitFilter(HIT_FILTER_NO_COLLISION) -- one hit a bomb, however long its blast lingers in the cylinder
            PushNerve(NerveDamage);
            b.Kill();
            return;
        }
    }
    protected override void SetBehavior() { }

    // ------------------------------------------------------------------------------------------------------------------ the shots
    private Vector3 MoleHandUnits() => MoleUnits + PolarXZ(_yaw, 60f) + Vector3.Up * 60f;
    /// <summary>bombSet: the Bob-omb made at the mole's point, dormant (dead, hidden) in its hand until the throw.</summary>
    private void BombSet()
    {
        _shot = null;
        if (BombScene == null) return;
        var b = BombScene.Instantiate<SmsSmallEnemy>();
        if (b == null) return;
        b.RelaunchTicks = -1; b.Pooled = true; b.DebugPrint = DebugPrint;
        GetParent().AddChild(b);
        b.GlobalPosition = MoleHandUnits() / U;
        b.SmsSpawnDead();
        _shot = b;
    }
    /// <summary>bombShoot: the XZ direction to Mario x ThrowXZSpeed, vy ThrowXZSpeed, +2 u, airborne -- the dormant bomb launched from the hand.</summary>
    private void BombShoot()
    {
        if (_shot == null || !IsInstanceValid(_shot)) return;
        Vector3 dir = MarioPos() - Pos(); dir.Y = 0f;
        if (dir.LengthSquared() < 1e-6f) dir = Vector3.Right; else dir = dir.Normalized();
        float sp = Prm.ThrowXZSpeed * SpeedScale;
        Vector3 v = new Vector3(dir.X * sp, sp, dir.Z * sp);
        _shot.Pooled = false;
        _shot.SmsLaunch((MoleHandUnits() + Vector3.Up * 2f) / U, YawOf(dir), v);
        LastBomb = _shot; _shot = null; Throws++;
        ThrowSound?.Play();
        if (DebugPrint) GD.Print($"[SMS cannon] {Name}: the bomb thrown at {v} u/tick");
    }
    /// <summary>killerShoot: the Bullet Bill from the dome's mouth, the velocity to a point near Mario (a 300-u jitter) x mVelocityRate, led by his speed x mSearchRate.</summary>
    private void KillerShoot()
    {
        if (KillerScene == null) return;
        var k = KillerScene.Instantiate<SmsSmallEnemy>();
        if (k == null) return;
        Vector3 mouth = _domes.Count > _dome && _domes[_dome] != null ? _domes[_dome].GlobalPosition * U + Vector3.Up * 100f : MoleUnits;
        Vector3 goal = MarioPos() + new Vector3((Rand() * 2f - 1f) * 300f, 0f, (Rand() * 2f - 1f) * 300f);
        if (_dome == 0) goal.Z -= 2f * MathF.Abs((Rand() * 2f - 1f) * 300f); else if (_dome == 2) goal.Z += 2f * MathF.Abs((Rand() * 2f - 1f) * 300f);
        Vector3 v = JumpVelocityTo(mouth, goal, 5f, k.GravityUnits);
        float t = MathF.Abs((goal - Pos()).Length() / MathF.Max(1e-3f, v.X * VelocityRate));
        Vector3 ms = _mario != null ? _mario.Velocity * U / 120f : Vector3.Zero;
        Vector3 led = new Vector3(goal.X + SearchRate * ms.X * t, goal.Y, goal.Z + SearchRate * ms.Z * t);
        v = JumpVelocityTo(mouth, led, 5f, k.GravityUnits) * VelocityRate;
        k.RelaunchTicks = -1; k.DebugPrint = DebugPrint;
        GetParent().AddChild(k);
        k.SmsLaunch(mouth / U, YawOf(new Vector3(v.X, 0f, v.Z)), v);
        Volleys++;
    }
    private static Vector3 JumpVelocityTo(Vector3 from, Vector3 goal, float speedY, float gravity)
    {
        float currY = from.Y, vy = speedY; int ticks = 1;
        for (int guard = 0; guard < 100000; guard++) { currY += vy; if (vy < 0f && currY <= goal.Y) break; vy -= gravity; if (vy < -40f) vy = -40f; ticks++; }
        return new Vector3((goal.X - from.X) / ticks, speedY, (goal.Z - from.Z) / ticks);
    }
    /// <summary>turnToGoal = walkToCurPathNode(0, mTurnSpeed, 0): the yaw toward the goal at TurnSpeedLow a tick, no step; the CANNON_MOVE sound while it turns.</summary>
    private void TurnToGoal()
    {
        Vector3 to = Goal() - Pos(); to.Y = 0f;
        if (to.LengthSquared() < 1e-6f) return;
        float diff = AngleDiff(Wrap360(YawOf(to)), _yaw), step = Prm.TurnSpeedLow * SpeedScale;
        _yaw = Wrap360(_yaw + Mathf.Clamp(diff, -step, step));
        if (MathF.Abs(_yaw - _lastYaw) > 1e-4f) { _lastYaw = _yaw; if (MoveSound != null && MoveSound.Stream != null && !MoveSound.Playing) MoveSound.Play(); }
    }
    /// <summary>The blast at the cannon's middle. The Damage's is the game's three particle emitters (BOMB_SMOKE + BOMB_BOMB + BOMB_HAHEN x 2: the smoke, the glow
    /// balls, the scraps) with no fireball -- the bomb's own TEffectExplosion is already there -- so the fireball, the shockwave and the sparks of the
    /// BombExplosion scene are hidden; the break's (TEffectExplosion x 2.5) is the whole thing.</summary>
    private void Blast(float scale, bool fireball = true)
    {
        var fx = SpawnBurst(BlastFxScene, new Transform3D(Basis.FromScale(Vector3.One * scale), GlobalPosition + Vector3.Up * (MoleHeightUnits / U) * 0.5f));
        if (fx == null || fireball) return;
        foreach (string part in new[] { "Fireball", "Shockwave", "Sparks" }) fx.GetNodeOrNull<Node3D>(part)?.Hide();
    }
    private float MarioDist2() { Vector3 d = MarioPos() - Pos(); return d.LengthSquared(); }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveOpen:                                      // TNerveCannonOpen: the mole up (appear1), the lid open
                if (_time == 0) { SetMoleHidden(false); SetMoleBck(BckMoleAppear, AppearSound); }
                if (MoleClipEnded() && IsMoleBck(BckMoleAppear)) { PushAfterCurrent(NerveSearch); return true; }
                return false;

            case NerveSearch:                                    // TNerveCannonSearch
            {
                float d2 = MarioDist2();
                if (_time == 0) { if (d2 < Prm.BombDist * Prm.BombDist) SetGoalMario(); SetMoleBck(BckMoleWaitStart); }
                if (MoleClipEnded() && IsMoleBck(BckMoleWaitStart)) SetMoleBck(BckMoleWaitLoop);
                if (d2 < Prm.HideDist * Prm.HideDist) { PushAfterCurrent(NerveClose); return true; }
                if (d2 < Prm.BombDist * Prm.BombDist)
                {
                    if (_time > Prm.BombInterval) { _bombMode = true; PushAfterCurrent(NerveShoot); return true; }
                }
                else if (d2 < Prm.KillerDist * Prm.KillerDist && _time > Prm.ShootInterval && KillerScene != null)
                {
                    _bombMode = false;
                    PushAfterCurrent(NerveSearch); PushAfterCurrent(NerveForceBombShoot); PushAfterCurrent(NerveShoot); PushAfterCurrent(NerveShoot); PushAfterCurrent(NerveShoot);
                    return true;
                }
                TurnToGoal();
                return false;
            }

            case NerveShoot:                                     // TNerveCannonShoot: the bomb (unk290) or a Bullet Bill from the next dome
                if (_bombMode)
                {
                    if (_time == 0) SetMoleBck(BckMoleWaitEnd);
                    if (IsMoleBck(BckMoleWaitEnd))
                    {
                        if (MoleClipEnded()) { SetMoleBck(BckMoleThrow, ThrowSound); BombSet(); }
                        TurnToGoal();
                    }
                    else if (IsMoleBck(BckMoleThrow))
                    {
                        if (MoleClipEnded()) { PushAfterCurrent(NerveSearch); return true; }
                        if (MolePassed(38f)) BombShoot();
                        else if (_shot != null && IsInstanceValid(_shot)) _shot.GlobalPosition = MoleHandUnits() / U;      // the held bomb rides the hand (bombScaleUp's growth is not shown: it sits dormant)
                    }
                    return false;
                }
                if (_time == 0) SetGoalPoint(MarioPos() + PolarXZ(Rand() * 360f, 500f));                                 // setKillerGoalPoint
                if (_time < 40) TurnToGoal();
                if (_time == 40) KillerShoot();
                if (_time > Prm.KillerInterval) { _dome = (_dome + 1) % 3; return true; }                                // endKillerShoot
                return false;

            case NerveForceBombShoot:                            // TNerveCannonForceBombShoot: a bomb after the volley when Mario is within sqrt 2 x BombDist
                if (_time == 0) { if (MarioDist2() < 2f * Prm.BombDist * Prm.BombDist) SetMoleBck(BckMoleWaitEnd); else return true; }
                if (IsMoleBck(BckMoleWaitEnd)) { if (MoleClipEnded()) { SetMoleBck(BckMoleThrow, ThrowSound); BombSet(); } TurnToGoal(); }
                else if (IsMoleBck(BckMoleThrow))
                {
                    if (MoleClipEnded()) return true;
                    if (MolePassed(38f)) BombShoot();
                    else if (_shot != null && IsInstanceValid(_shot)) _shot.GlobalPosition = MoleHandUnits() / U;
                }
                return false;

            case NerveClose:                                     // TNerveCannonClose: the mole ducks in (hyde1), the lid shuts; Mario past 3 x HideDist -> Open
                if (_time < 2)
                {
                    if (_time == 0) { SetMoleBck(BckMoleHide, HideSound); if (SweatFxScene != null) SpawnBurst(SweatFxScene, new Transform3D(Basis.Identity, GlobalPosition + Vector3.Up * (300f / U))); }
                }
                if (!_moleHidden && IsMoleBck(BckMoleHide) && MoleClipEnded())
                {
                    SetMoleHidden(true);
                    SpawnBurst(LidPuffFxScene, new Transform3D(Basis.FromScale(Vector3.One * 1.5f), GlobalPosition + Vector3.Up * (290f / U)));
                }
                {
                    float far = Prm.HideDist * 3f;
                    if (MarioDist2() > far * far) { PushAfterCurrent(NerveOpen); return true; }
                }
                return false;

            case NerveDamage:                                    // TNerveCannonDamage: the blast, a hit point off, the mole hit, the hop
                if (_time == 0)
                {
                    Blast(2f, fireball: false);
                    _hp = Math.Max(0, _hp - 1); Hits++;
                    if (_hp == 0) { if (_shot != null && IsInstanceValid(_shot)) { _shot.SmsKillByDemo(); _shot = null; } }
                    SetMoleBck(BckMoleDamage, DamageSound);
                    if (MoleSmokeFxScene != null) SpawnBurst(MoleSmokeFxScene, new Transform3D(Basis.Identity, MoleUnits / U));
                    _hopVy = 4f; _hopY = 10f;
                    if (DebugPrint) GD.Print($"[SMS cannon] {Name}: hit -> {_hp} hit point(s) left");
                }
                if (MoleClipEnded() && IsMoleBck(BckMoleDamage)) { PushAfterCurrent(_hp == 0 ? NerveDamageDemo : NerveSearch); return true; }
                return false;

            case NerveDamageDemo:                                // TNerveCannonDamageDemo: the mole down, then the break
                if (_time == 0) { SetMoleBck(BckMoleDown); }
                if (_time > DamageDemoTicks && MoleClipEnded())
                {
                    DownSound?.Play();
                    Blast(2.5f);
                    SetDomesVisible(false); SetMoleHidden(true);
                    PushAfterCurrent(NerveObject);
                    if (DebugPrint) GD.Print($"[SMS cannon] {Name}: broken");
                    return true;
                }
                return false;

            case NerveObject:                                    // TNerveCannonObject: a prop
                if (_time == 60) LandSound?.Play();
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
