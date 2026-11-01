using System;
using Godot;

/// <summary>
/// SNOOZA KOOPA -- TTamaNoko (Enemy/tamaNoko.cpp; JP タマノコ, the tamaNoko archive) on <see cref="SmsSmallEnemy"/> with the shipped /enemy/tamanoko.prm
/// (SMS pass 145, feature/enemy-pinna). Pinna Park's big Koopa asleep in its shell on the sand: a long enough hose wakes it (it shakes, then
/// stretches out), it wanders, and when it sees Mario it runs at him and leaps -- a somersault at the top of its arc, a hang in the air, then a
/// straight drop. Landing on SAND it is stuck fast, struggling, and a ground pound then kills it: a sunflower grows from the spot and bursts
/// five coins. Landing on hard ground it lies flattened for a while -- Mario can pick it up and throw it -- then gets up and wanders on. Far from
/// Mario it goes back to sleep.
///
/// THE GAME. TTamaNoko is a TWalkerEnemy (ACTOR_TYPE_TAMA_NOKO, unk150 0x11) with LIVE_FLAG_UNK1000 (the ground check ignores water surfaces),
/// body scale 2 (BodyScaleLow / High 2.0: the 227-u model stands 454 u), mScaledBodyRadius 280 (the shadow). /enemy/tamanoko.prm: HeadHeight 120,
/// BodyRadius 7, WallRadius 100, 1 hit point, SearchLength 1500 / Height 1000 / Aware 500 / Angle 180, GiveUp 4000 / 1200, AttackWait 240, FreezeWait
/// 200, damage 90 x 120, attack 80 x 100, turn 1.2 / 2.5, WaitTime 60, march 4 / 4.5; its own: PickUpTime 120, JumpAttackDist 800, ThrownVY 10,
/// ThrownRateXZ 2, ThrownGravityY 0.2, JumpAttackSp 25, AttackGravityY 0.4, SinkTime 400, WakeUpTimer 200; the statics mStopOnAirTimeMax 40,
/// mRollOnAirTimeMax 20, mRollOnAirAngle 360, mDropVelocityY -30. The bck table is the archive's 17 clips in order: 0 tamaflower_grow1 (the
/// flower's, 244), 1 appear1 (249), 2 attack1 (99), 3 down1 (74), 4 land1 (59), 5 mogaki1 (30, loop), 6 mudland1 (14), 7 return1 (169), 8 run1_end
/// (14), 9 run1_loop (60, loop), 10 run1_start (14), 11 sleep1 (159), 12 wait1_end (14), 13 wait1_loop (60, loop), 14 wait1_start (14), 15
/// waterhit1 (9), 16 waterhit1_loop (30, loop). SLEEP (the default): the current clip's end -> waterhit1 held at frame 0 (the shell at rest),
/// POI_ZZZ every frame, walkBehavior(5, 0) (no step, no turn); doKeepDistance (Mario pushed off the shell); isCollidMove hands another actor a
/// TRAMPLE. behaveToWater (every drop: the cooldown 0, unk165 = 1): asleep -> HITWATER pushed; wandering -> ATTACK pushed. HITWATER: waterhit1
/// then waterhit1_loop; at a loop's end: past WakeUpTimer (or woken once before: unk1B9) -> appear1 (the puffs at frames 0 and 46; frame 25 on
/// sand landEffect, else the SMB_AP rock + smoke and a shake); else sprayed since the last loop (unsetUnk165) -> the loop again; else back to
/// SLEEP. appear1's end -> WAIT then GRAPHWANDER. WAIT: wait1_start -> wait1_loop; past WaitTime -> wait1_end -> GRAPHWANDER. GRAPHWANDER (the
/// walker's): run1_start -> run1_loop (moveObject), the nodes, isFindMario -> behaveToFindMario = ATTACK after it; walkBehavior: the turn at
/// TurnSpeedLow x k toward the target, the march at MarchSpeedLow x k (not in modes 3 / 5); every 200 ticks of wandering forceSleep: Mario past
/// GiveUpLength -> sleep1, the spine reset to SLEEP. ATTACK: run (run1_start -> run1_loop, the goal Mario, walkBehavior(2, 1)); a loop's end with
/// Mario past SearchLength -> unk1B8 + run1_end -> GRAPHWANDER; within JumpAttackDist -> run1_end -> (Mario in water -> GRAPHWANDER; Mario
/// invincible -> WAIT pushed) attack1 with walkBehavior(3, 0.5) (the turn only); frame 63: the jump = calcVelocityToJumpToY(Mario, JumpAttackSp,
/// AttackGravityY) with XZ x 2, +30 u, airborne, mIsDoingJumpAttack. In the air: vy < -1 -> TAMA_BLUR at joint 1; vy < 0 -> M_BLUR2 200 u under
/// joint 1 and, for StopOnAirTimeMax ticks, UNK10 (no bind: it HANGS) with the yaw + 18 deg a tick for the first RollOnAirTimeMax (a full turn), then
/// the velocity (0, DropVelocityY, 0) and the bind back; landed -> landEffect, DOWN. DOWN: on sand -> SINK + the sand column; else DROPOK, unk164
/// (down: Mario may take it), a shake, land1; the clip's end past PickUpTime -> unk164 off, WAIT. PICKUP (taken): the clip's end past PickUpTime
/// -> unk164 off, attackToMario (it wakes in his hands and hurts him). THROWN (released while down): the velocity (ThrownRateXZ x his throw power
/// along his facing, ThrownVY), +2 u, airborne, ThrownGravityY; landed -> unk164 off. SINK: mIsSunkInGround, mudland1 -> mogaki1 (loop); a loop's
/// end past SinkTime x (the live fraction of the stage's Snooza Koopas) -> return1 (landEffect at frame 110, the puff at 152); its end -> unsunk,
/// WAIT. isHitValid: UNKB false; not sunk, or return1 past frame 80 -> false; HIP_DROP -> true (TRAMPLE never: he bounces). attackToMario: not
/// down, not asleep, not (sunk with Mario over it) -> ATTACK. getGravityY: AttackGravityY in ATTACK, ThrownGravityY in THROWN. setDeadAnm down1.
/// setAfterDeadEffect: the puffs + the TTamaNokoFlower (tamaflower_model1, grow1 at the body's point; its end: reviveOneSunflower, then FIVE coins
/// 350 u out at 72-deg steps with the velocity (dir x 4, 20, dir x 4)), TAMA_FLOWER, COMMON_SMOKE. landEffect: sand -> HIPDROP_C + POI_SAND (x 0.8);
/// water -> the column; else HIPDROP_C + HIPDROP_B; a shake and a rumble.
///
/// THE PORT. The base's walker (GraphWander, the Die nerve, the hold proxy) with the species' nerves; sand = the floor collider in the "sms_sand"
/// group or named with "sand" (the Cataquack's rule); the live fraction = this kind alive among this kind in the level. The jump's hang is
/// <c>_noBind</c> for the 40 ticks (the base keeps the velocity while it is on). Mario's invincibility is read as his hurt state (DEVIATION: no
/// getter). The flower is a scene node of its own (<see cref="SmsTamaFlower"/>) so it outlives the freed body. Effects: PoiZzz / PoiSand / JumpEd /
/// SmbAppear from Fx131 stand in for POI_ZZZ / POI_SAND + HIPDROP_C / JUMP_ED_B / SMB_AP; the blurs, the sand column and the flower puff are slots;
/// the sounds are slots; the shakes and rumbles are not ported.
/// </summary>
public partial class SmsTamaNoko : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ tamanoko.prm
    public class TamaNokoParams : Params
    {
        public int PickUpTime = 60, SinkTime = 500, WakeUpTimer = 100;
        public float JumpAttackDist = 200f, ThrownVY = 50f, ThrownRateXZ = 0.5f, ThrownGravityY = 1.5f, JumpAttackSp = 10f, AttackGravityY = 0.2f;
    }
    /// <summary>The shipped /enemy/tamanoko.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly TamaNokoParams Prm = new TamaNokoParams
    {
        HeadHeight = 120f, BodyRadius = 7f, WallRadius = 100f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 5, ActiveEnemyNum = 5, JumpForce = 2.5f,
        SearchLength = 1500f, SearchHeight = 1000f, SearchAware = 500f, SearchAngle = 180f, GiveUpLength = 4000f, GiveUpHeight = 1200f, AttackWait = 240, FreezeWait = 200,
        DamageRadius = 90f, DamageHeight = 120f, AttackRadius = 80f, AttackHeight = 100f, TurnSpeedLow = 1.2f, TurnSpeedHigh = 2.5f, BodyScaleLow = 2f, BodyScaleHigh = 2f,
        GenItemRate = 50f, GenEggRate = 100f, WaitTime = 60, MarchSpeedLow = 4f, MarchSpeedHigh = 4.5f,
        PickUpTime = 120, JumpAttackDist = 800f, ThrownVY = 10f, ThrownRateXZ = 2f, ThrownGravityY = 0.2f, JumpAttackSp = 25f, AttackGravityY = 0.4f, SinkTime = 400, WakeUpTimer = 200,
    };
    protected override Params P => Prm;
    /// <summary>TTamaNoko's statics: the hang at the top of the leap, the roll's length and angle, the drop.</summary>
    public const int StopOnAirTimeMax = 40, RollOnAirTimeMax = 20;
    public const float RollOnAirAngle = 360f, DropVelocityY = -30f, ShadowRadius = 280f;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the walk, the run and the leap (the game: march 4 u a tick, the leap at 25 u a tick up).</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpeedScale = 1f;
    /// <summary>What it lands on: Auto = sand when the floor is in the "sms_sand" group or its name has "sand" in it (the Cataquack's rule); Sand / Hard force it.</summary>
    [Export] public GroundKind Ground = GroundKind.Auto;
    public enum GroundKind { Auto, Sand, Hard }
    /// <summary>The seconds of hosing that wake a sleeper (WakeUpTimer 200 ticks = 1.7 s in the game; once woken, a later hosing wakes it at once).</summary>
    [Export(PropertyHint.Range, "0,10,0.1")] public float WakeSeconds = 200f / 120f;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_poi_zzz: the Zs over the sleeping shell (a looping scene as a child). Empty = assets/Sms/Enemy/Fx131/PoiZzz.tscn.</summary>
    [Export] public PackedScene SleepFxScene;
    /// <summary>ms_poi_sand + ms_hipdrop_c: the landing's sand puff (an SmsFxBurst / one-shot scene). Empty = assets/Sms/Enemy/Fx131/PoiSand.tscn.</summary>
    [Export] public PackedScene LandSandFxScene;
    /// <summary>ms_hipdrop_c + ms_hipdrop_b: the landing's puff on hard ground. Empty = assets/Sms/Fx/HipDropLanding.tscn.</summary>
    [Export] public PackedScene LandHardFxScene;
    /// <summary>ms_jump_ed_b: the small puffs as it stretches out of its shell and comes out of the sand. Empty = assets/Sms/Enemy/Fx131/JumpEd.tscn.</summary>
    [Export] public PackedScene PuffFxScene;
    /// <summary>ms_smb_ap_rock + _smoke (x 2): the burst as it stretches out on hard ground. Empty = assets/Sms/Enemy/Fx131/SmbAppear.tscn.</summary>
    [Export] public PackedScene AppearHardFxScene;
    /// <summary>The sand column where it plunges into the sand (TEffectColumSand), ms_tama_blur / ms_m_blur2 (the fall's streaks), ms_tama_flower (the
    /// sunflower's puff), ms_tama_hit (the shake's sparkle). Empty = none.</summary>
    [Export] public PackedScene SandColumnFxScene, FallBlurFxScene, FlowerPuffFxScene, ShakeFxScene;
    /// <summary>The sunflower that grows where it died (models/Eniemes/TamaNoko/TamaFlower.tscn: tamaflower_model1 with grow1, then the coins). Empty = none.</summary>
    [Export] public PackedScene FlowerScene;
    /// <summary>The coin the flower bursts (five of them). Empty = res://yellow_coin.tscn.</summary>
    [Export] public PackedScene FlowerCoinScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_TAMANOKO_DROPOK (the flat landing), the bas tables' appear / attack / down / land / sleep / waterhit, the flower's grow.</summary>
    [Export] public AudioStreamPlayer3D DropSound, AppearSound, AttackSound, DownSound, LandSound, SleepSound, WaterHitSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's 17 bcks; Godot strips "_loop"; waterhit1_loop renamed in the glb)
    private static readonly string[] Names = { "tamaflower_grow1", "tamanoko_appear1", "tamanoko_attack1", "tamanoko_down1", "tamanoko_land1", "tamanoko_mogaki1", "tamanoko_mudland1",
        "tamanoko_return1", "tamanoko_run1_end", "tamanoko_run1", "tamanoko_run1_start", "tamanoko_sleep1", "tamanoko_wait1_end", "tamanoko_wait1", "tamanoko_wait1_start",
        "tamanoko_waterhit1", "tamanoko_waterhit1_cycl" };
    private static readonly int[] Frames = { 244, 249, 99, 74, 59, 30, 14, 169, 14, 60, 14, 159, 14, 60, 14, 9, 30 };
    private static readonly bool[] Loops = { false, false, false, false, false, true, false, false, false, true, false, false, false, true, false, false, true };
    protected const int BckFlower = 0, BckAppear = 1, BckAttack = 2, BckDown = 3, BckLand = 4, BckMogaki = 5, BckMudLand = 6, BckReturn = 7, BckRunEnd = 8, BckRunLoop = 9,
        BckRunStart = 10, BckSleep = 11, BckWaitEnd = 12, BckWaitLoop = 13, BckWaitStart = 14, BckWaterHit = 15, BckWaterHitLoop = 16;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves (TNerveTamaNoko*)
    protected const int NerveSleep = NerveSpecies, NerveTAttack = NerveSpecies + 1, NerveDown = NerveSpecies + 2, NervePickUp = NerveSpecies + 3, NerveThrown = NerveSpecies + 4,
        NerveSink = NerveSpecies + 5, NerveHitWater = NerveSpecies + 6, NerveTWait = NerveSpecies + 7;
    private static readonly string[] TamaNerveNames = { "Sleep", "Attack", "Down", "PickUp", "Thrown", "Sink", "HitWater", "Wait" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + TamaNerveNames.Length ? TamaNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveSleep;

    // ------------------------------------------------------------------------------------------------------------------ state
    private bool _jumpAttack, _sunk, _down, _giveUp, _firstWake = true, _sprayedSince;   // mIsDoingJumpAttack / mIsSunkInGround / unk164 / unk1B8 / unk1B9 / unk165
    private int _airTimer;                                                                // mAirAttackTimer
    private Vector3 _jumpVel;                                                             // unk1A0
    private float _throwYaw, _throwPower;
    private Node3D _sleepFx, _blurFx, _shakeFx;
    private float _prevFrame = -1f, _lastFrame = -1f; private bool _wrapped;      // the clip frame at this tick's start and at the last tick's
    /// <summary>For the drives.</summary>
    public string CurrentNerveName => NerveName(_cur);
    public bool Sleeping => IsNerve(NerveSleep);
    public bool Sunk => _sunk;
    public bool Down => _down;
    public bool JumpAttacking => _jumpAttack;
    public bool Hanging => _jumpAttack && _noBind;
    public int AirTimer => _airTimer;
    public bool WokenOnce => !_firstWake;
    public Vector3 JumpVelocity => _jumpVel;
    public int Leaps { get; private set; }
    public int Landings { get; private set; }
    public int Wakes { get; private set; }
    public int Sleeps { get; private set; }
    public int Flowers { get; private set; }
    public string LastLanding { get; private set; } = "";

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _flags150 = 0x11;
        if (SleepFxScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Fx131/PoiZzz.tscn")) SleepFxScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Fx131/PoiZzz.tscn");
        if (LandSandFxScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Fx131/PoiSand.tscn")) LandSandFxScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Fx131/PoiSand.tscn");
        if (LandHardFxScene == null && ResourceLoader.Exists("res://assets/Sms/Fx/HipDropLanding.tscn")) LandHardFxScene = GD.Load<PackedScene>("res://assets/Sms/Fx/HipDropLanding.tscn");
        if (PuffFxScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Fx131/JumpEd.tscn")) PuffFxScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Fx131/JumpEd.tscn");
        if (AppearHardFxScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/Fx131/SmbAppear.tscn")) AppearHardFxScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/Fx131/SmbAppear.tscn");
        if (FlowerScene == null && ResourceLoader.Exists("res://assets/Sms/Enemy/TamaNoko/TamaFlower.tscn")) FlowerScene = GD.Load<PackedScene>("res://assets/Sms/Enemy/TamaNoko/TamaFlower.tscn");
        if (FlowerCoinScene == null && ResourceLoader.Exists("res://yellow_coin.tscn")) FlowerCoinScene = GD.Load<PackedScene>("res://yellow_coin.tscn");
        if (DebugPrint) GD.Print($"[SMS tamanoko] {Name}: scale {_scale}; sand rule {Ground}");
    }
    /// <summary>TTamaNoko::reset: TWalkerEnemy::reset, the flags cleared (unk1B9 back to true), mScaledBodyRadius 280, LIVE_FLAG_UNK1000.</summary>
    protected override void Reset()
    {
        base.Reset();
        _jumpAttack = false; _sunk = false; _down = false; _giveUp = false; _firstWake = true; _sprayedSince = false; _airTimer = 0; _jumpVel = Vector3.Zero;
        _prevFrame = -1f; _wrapped = false;
        StopFx(ref _blurFx); StopFx(ref _shakeFx);
        SetBck(BckWaterHit); SetBckPaused(true);
    }
    protected override void SetWalkAnm() => SetBck(BckRunStart);
    protected override void SetRunAnm() { if (!IsBck(BckRunLoop)) SetBck(BckRunStart); }
    protected override void SetDeadAnm() { SetBck(BckDown); DownSound?.Play(); }
    protected override float GravityY() => IsNerve(NerveTAttack) ? Prm.AttackGravityY : IsNerve(NerveThrown) ? Prm.ThrownGravityY : P.Gravity;
    protected override bool IsReachedToGoal() { Vector3 d = Goal() - Pos(); d.Y = 0f; return d.LengthSquared() < 200f * 200f; }
    protected override bool WallHopAllowed() => false;
    /// <summary>isCollidMove: its own kind pushes; while asleep another actor gets a TRAMPLE (a walker into the shell) and nothing moves it.</summary>
    protected override bool IsCollidMove(Node3D other) => other is SmsTamaNoko || IsNerve(NerveSleep);

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (_dead) return;
        BoundFx(ref _sleepFx, SleepFxScene, IsNerve(NerveSleep), 1.6f);
        BoundFx(ref _shakeFx, ShakeFxScene, IsBck(BckWaterHitLoop) || (IsBck(BckSleep) && BckFrame() > 90f), 1f);
        BoundFx(ref _blurFx, FallBlurFxScene, _jumpAttack && _vel.Y < -1f, 1f);
    }
    protected override void MoveObjectPre()
    {
        float f = BckFrame();
        _wrapped = _bck >= 0 && _bck < Loops.Length && Loops[_bck] && _prevFrame >= 0f && f < _prevFrame - 5f;
        _lastFrame = _prevFrame; _prevFrame = f;
        if (IsNerve(NerveGraphWander) && IsBck(BckRunStart) && ClipEnded()) SetBck(BckRunLoop);   // moveObject: the walk start into the loop
    }
    private bool ClipEnded() => BckEnded() || _wrapped;
    private void ChangeBck(int i, AudioStreamPlayer3D sound = null) { SetBck(i); sound?.Play(); _prevFrame = -1f; _lastFrame = -1f; }
    private bool PassedFrame(float frame) => _lastFrame >= 0f && _lastFrame < frame && _prevFrame >= frame;   // checkPass(frame): crossed since the last tick

    // ------------------------------------------------------------------------------------------------------------------ the ground, the effects
    /// <summary>mGroundPlane->isSand(): the floor under the body (the sms_sand group, or "sand" in the collider's name), or the knob.</summary>
    private bool OnSand()
    {
        if (Ground != GroundKind.Auto) return Ground == GroundKind.Sand;
        var q = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * 0.3f, GlobalPosition + Vector3.Down * 1.5f, CollisionMask, new Godot.Collections.Array<Rid> { GetRid() });
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count == 0) return false;
        for (Node n = hit["collider"].As<Node>(); n != null; n = n.GetParent())
            if (n.IsInGroup("sms_sand") || n.Name.ToString().Contains("sand", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    private Transform3D At(float scale = 1f) => new Transform3D(Basis.FromScale(Vector3.One * _scale * scale), GlobalPosition);
    /// <summary>landEffect: sand -> HIPDROP_C + POI_SAND (x 0.8); water -> the column; else HIPDROP_C + HIPDROP_B.</summary>
    private void LandEffect()
    {
        bool sand = OnSand();
        if (sand) SpawnBurst(LandSandFxScene, At(0.8f));
        else if (WaterSurfaceAt(GlobalPosition, out _)) SpawnBurst(LandSandFxScene, At(0.8f));
        else SpawnBurst(LandHardFxScene, At(0.8f));
        LandSound?.Play();
        Landings++; LastLanding = sand ? "sand" : "hard";
    }
    private void BoundFx(ref Node3D slot, PackedScene scene, bool on, float scale)
    {
        if (on && slot == null && scene != null)
        {
            slot = scene.Instantiate<Node3D>();
            AddChild(slot);
            slot.Position = Vector3.Up * (P.DamageHeight * _scale * 0.9f / U);
            slot.Scale = Vector3.One * _scale * scale;
        }
        else if (!on && slot != null) StopFx(ref slot);
    }
    private void StopFx(ref Node3D slot) { if (slot != null && IsInstanceValid(slot)) slot.QueueFree(); slot = null; }
    /// <summary>calcRootMatrix's clip-driven effects: the shake's sparkle, appear1's puffs (frames 0 / 46) and its frame-25 landing, return1's landing
    /// (110) and puff (152).</summary>
    private void ClipEffects()
    {
        if (IsBck(BckAppear))
        {
            if (PassedFrame(0.5f) || PassedFrame(46f)) SpawnBurst(PuffFxScene, At());
            if (PassedFrame(25f)) { if (OnSand()) LandEffect(); else SpawnBurst(AppearHardFxScene, At(2f)); }
        }
        if (IsBck(BckReturn))
        {
            if (PassedFrame(110f)) LandEffect();
            if (PassedFrame(152f) && !WaterSurfaceAt(GlobalPosition, out _)) SpawnBurst(PuffFxScene, At());
        }
    }
    /// <summary>SMSCalcJumpVelocityY from the body point: the ticks to come down to the goal's height at speedY, the XZ speed to arrive then.</summary>
    private Vector3 CalcVelocityToJumpToY(Vector3 goalU, float speedY, float gravity)
    {
        Vector3 pos = Pos();
        float currY = pos.Y, vy = speedY; int ticks = 1;
        for (int guard = 0; guard < 100000; guard++) { currY += vy; if (vy < 0f && currY <= goalU.Y) break; vy -= gravity; if (vy < -40f) vy = -40f; ticks++; }
        return new Vector3((goalU.X - pos.X) / ticks, speedY, (goalU.Z - pos.Z) / ticks);
    }
    /// <summary>The live fraction of this kind in the level (the manager's objects): the fewer remain, the sooner the sunk ones get up.</summary>
    private float LiveFraction()
    {
        int all = 0, live = 0;
        foreach (Node n in GetTree().GetNodesInGroup("sms_enemy")) if (n is SmsTamaNoko t && IsInstanceValid(t)) { all++; if (!t.IsDead) live++; }
        return all > 0 ? live / (float)all : 1f;
    }

    // ------------------------------------------------------------------------------------------------------------------ the game's virtuals
    /// <summary>walkBehavior: the turn toward the target at TurnSpeedLow x k, the march at MarchSpeedLow x k (none in modes 3 / 5); every 200 ticks of
    /// wandering forceSleep (Mario past GiveUpLength: sleep1, the spine reset to SLEEP).</summary>
    protected override void WalkBehavior(int mode, float k)
    {
        _turnSpeed = Prm.TurnSpeedLow; _marchSpeed = Prm.MarchSpeedLow * SpeedScale;
        Vector3 to = Goal() - Pos();
        float rot = Wrap360(YawOf(to)), diff = AngleDiff(rot, _yaw);
        float step = diff > 0f ? MathF.Min(diff, _turnSpeed * k) : MathF.Max(diff, -_turnSpeed * k);
        _yaw = Wrap360(_yaw + step);
        if (mode != 5 && mode != 3) _lin += PolarXZ(_yaw, _marchSpeed * k);
        if (IsNerve(NerveGraphWander) && _time % 200 == 1) ForceSleep();
    }
    /// <summary>forceSleep: Mario past GiveUpLength -> sleep1, back to SLEEP.</summary>
    private void ForceSleep()
    {
        Vector3 d = MarioPos() - Pos();
        if (d.LengthSquared() > Prm.GiveUpLength * Prm.GiveUpLength)
        {
            ChangeBck(BckSleep, SleepSound); Sleeps++;
            ResetSpine(); SetNext(NerveSleep);
            if (DebugPrint) GD.Print($"[SMS tamanoko] {Name}: Mario {d.Length():F0} u off -> back to sleep");
        }
    }
    /// <summary>behaveToFindMario: ATTACK after the wander.</summary>
    protected override void BehaveToFindMario() => PushAfterCurrent(NerveTAttack);
    /// <summary>behaveToWater: the cooldown 0 (every drop counts), unk165; asleep -> HITWATER; wandering -> ATTACK.</summary>
    protected override void BehaveToWater()
    {
        _sprayCooldown = 0; _sprayedSince = true;
        if (IsNerve(NerveSleep)) { PushNerve(NerveHitWater); return; }
        if (IsNerve(NerveGraphWander)) PushNerve(NerveTAttack);
    }
    /// <summary>isHitValid: only a HIP_DROP on a sunk body (not yet out of the sand: return1 under frame 80) kills; a TRAMPLE never (he bounces).</summary>
    protected override bool IsHitValid(HitMessage msg)
    {
        if (!_sunk || (IsBck(BckReturn) && BckFrame() > 80f)) return false;
        return msg == HitMessage.HipDrop;
    }
    /// <summary>attackToMario: not while down, asleep, or sunk with Mario over it.</summary>
    protected override void AttackToMario()
    {
        if (_down || IsNerve(NerveSleep)) return;
        if (_sunk && MarioPos().Y > Pos().Y + 10f) return;
        SendAttackMsgToMario();
    }
    /// <summary>The base's lists plus doKeepDistance: a sleeping shell pushes Mario out of its damage cylinder (keepDistance).</summary>
    protected override void HitCheckMario()
    {
        base.HitCheckMario();
        if (_mario == null || _dead || !IsNerve(NerveSleep)) return;
        Vector3 g = Pos(), m = MarioPos();
        if (CheckDistance(m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight, g, P.DamageRadius * _bodyScale, P.DamageHeight * _bodyScale))
            _mario.SmsKeepDistance(g, P.DamageRadius * _bodyScale);
    }
    /// <summary>TMario::hitPickUpEnemy: his touch takes it while it lies flat (unk164).</summary>
    public override bool SmsCanBeTaken() => _down && !_dead && _holder == null && GetCollisionLayerValue(1);
    /// <summary>behaveToTaken: PICKUP pushed unless already.</summary>
    protected override void BehaveToTaken(Mario holder) { if (!IsNerve(NervePickUp)) PushNerve(NervePickUp); }
    /// <summary>behaveToRelease: while down, THROWN pushed with his facing and throw power read now.</summary>
    protected override void BehaveToRelease(Mario holder, bool put)
    {
        _airborne = true;                                                                                  // out of his hands: the bind finds the ground (or not) from here
        if (!_down || IsNerve(NerveThrown)) return;
        Vector3 f = holder != null && IsInstanceValid(holder) ? holder.SmsFacingDir() : PolarXZ(_yaw, 1f);
        f.Y = 0f;
        _throwYaw = f.LengthSquared() > 1e-6f ? Mathf.RadToDeg(Mathf.Atan2(f.X, f.Z)) : _yaw;
        _throwPower = SmsParams.Take.ThrowPower;
        PushNerve(NerveThrown);
    }
    /// <summary>setAfterDeadEffect: the puffs, then the sunflower at the body's point (its growth and its coins are its own), TAMA_FLOWER.</summary>
    protected override void SetAfterDeadEffect()
    {
        base.SetAfterDeadEffect();
        if (FlowerScene != null)
        {
            var flower = FlowerScene.Instantiate<Node3D>();
            GetParent().AddChild(flower);
            flower.GlobalPosition = new Vector3(GlobalPosition.X, _groundY / U, GlobalPosition.Z);
            if (flower is SmsTamaFlower tf) tf.CoinScene = FlowerCoinScene;
            Flowers++;
        }
        if (FlowerPuffFxScene != null) SpawnBurst(FlowerPuffFxScene, At());
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        ClipEffects();
        switch (n)
        {
            case NerveSleep:                                     // TNerveTamaNokoSleep: the shell at rest (waterhit1's first frame), no step
                if (!IsBck(BckWaterHit) && ClipEnded()) { ChangeBck(BckWaterHit); SetBckPaused(true); }
                WalkBehavior(5, 0f);
                return false;

            case NerveHitWater:                                  // TNerveTamaNokoHitWater: the shake; long enough -> the stretch out of the shell
                if (_time < 2) { ChangeBck(BckWaterHit, WaterHitSound); SetBckPaused(false); }
                if (ClipEnded())
                {
                    if (IsBck(BckWaterHit)) ChangeBck(BckWaterHitLoop);
                    else if (IsBck(BckWaterHitLoop))
                    {
                        int wake = Mathf.RoundToInt(WakeSeconds * 120f);
                        if (_time > wake || !_firstWake)
                        {
                            _firstWake = false;
                            if (!IsBck(BckAppear)) { ChangeBck(BckAppear, AppearSound); Wakes++; if (DebugPrint) GD.Print($"[SMS tamanoko] {Name}: wakes after {_time} ticks"); }
                        }
                        else if (_sprayedSince) { _sprayedSince = false; ChangeBck(BckWaterHitLoop); }
                        else { ResetSpine(); SetNext(NerveSleep); PushAfterCurrent(NerveSleep); return true; }
                    }
                    else if (IsBck(BckAppear)) { PushAfterCurrent(NerveGraphWander); PushAfterCurrent(NerveTWait); return true; }
                }
                return false;

            case NerveTWait:                                     // TNerveTamaNokoWait: wait1 start / loop (WaitTime) / end -> the wander
                if (_time < 2) { ChangeBck(BckWaitStart); SetBckPaused(false); }
                if (ClipEnded())
                {
                    if (IsBck(BckWaitStart)) ChangeBck(BckWaitLoop);
                    else if (IsBck(BckWaitLoop)) { if (_time > P.WaitTime) ChangeBck(BckWaitEnd); }
                    else if (IsBck(BckWaitEnd)) { PushAfterCurrent(NerveGraphWander); return true; }
                }
                return false;

            case NerveTAttack:                                   // TNerveTamaNokoAttack: the chase, the leap, the hang, the drop
                if (_time == 0) { if (!IsBck(BckRunLoop)) ChangeBck(BckRunStart); SetGoalMario(); }
                if (_jumpAttack)
                {
                    if (!_airborne && !_noBind)
                    {
                        LandEffect();
                        _jumpAttack = false;
                        PushAfterCurrent(NerveDown);
                        return true;
                    }
                    if (_vel.Y < 0f)
                    {
                        if (_airTimer < StopOnAirTimeMax)
                        {
                            if (_airTimer == 0 && DebugPrint) GD.Print($"[SMS tamanoko] {Name}: hangs at {Pos()} (vy {_vel.Y:F2})");
                            _noBind = true;                                                                  // UNK10: it hangs at the top of its arc
                            if (_airTimer < RollOnAirTimeMax) _yaw = Wrap360(_yaw + RollOnAirAngle / RollOnAirTimeMax);
                            _airTimer++;
                        }
                        else
                        {
                            if (_noBind && DebugPrint) GD.Print($"[SMS tamanoko] {Name}: drops from {Pos()}");
                            _vel = new Vector3(0f, DropVelocityY * SpeedScale, 0f); _noBind = false; _airborne = true;
                        }
                    }
                    return false;
                }
                if (ClipEnded())
                {
                    if (IsBck(BckRunStart)) ChangeBck(BckRunLoop);
                    else if (IsBck(BckRunLoop))
                    {
                        float d2 = (MarioPos() - Pos()).LengthSquared();
                        if (d2 > Prm.SearchLength * Prm.SearchLength) { _giveUp = true; ChangeBck(BckRunEnd); }
                        else if (d2 < Prm.JumpAttackDist * Prm.JumpAttackDist) ChangeBck(BckRunEnd);
                    }
                    else if (IsBck(BckRunEnd))
                    {
                        if (_giveUp) { _giveUp = false; PushAfterCurrent(NerveGraphWander); return true; }
                        if (MarioInWater || (_mario != null && _mario.SmsInShallowWater)) { PushAfterCurrent(NerveGraphWander); return true; }
                        if (_mario != null && _mario.stateOfMario == Mario.MarioState.hurt) PushNerve(NerveTWait);   // isInvincible (DEVIATION: his hurt state)
                        ChangeBck(BckAttack, AttackSound);
                    }
                }
                if (IsBck(BckAttack))
                {
                    if (PassedFrame(63f))
                    {
                        _jumpVel = CalcVelocityToJumpToY(MarioPos(), Prm.JumpAttackSp * SpeedScale, GravityY());
                        _jumpVel.X *= 2f; _jumpVel.Z *= 2f;
                        _lin += Vector3.Up * 30f;
                        _vel = _jumpVel; _airborne = true; _jumpAttack = true; _airTimer = 0; Leaps++;
                        if (DebugPrint) GD.Print($"[SMS tamanoko] {Name}: leaps at {_jumpVel} u/tick toward {MarioPos()}");
                    }
                    WalkBehavior(3, 0.5f);
                }
                else WalkBehavior(2, 1f);
                return false;

            case NerveDown:                                      // TNerveTamaNokoDown: on sand stuck (SINK); else flat for PickUpTime, then WAIT
                if (_time == 0)
                {
                    if (OnSand())
                    {
                        PushAfterCurrent(NerveSink);
                        if (SandColumnFxScene != null) SpawnBurst(SandColumnFxScene, new Transform3D(Basis.FromScale(new Vector3(1.2f, 0.5f, 1.2f) * _scale), GlobalPosition));
                        return true;
                    }
                    DropSound?.Play();
                    _down = true;
                    ChangeBck(BckLand);
                }
                if (ClipEnded() && _time > Prm.PickUpTime) { _down = false; PushAfterCurrent(NerveTWait); return true; }
                return false;

            case NervePickUp:                                    // TNerveTamaNokoPickUp: in his hands; the clip's end past PickUpTime -> awake in his hands: it hurts him
                if (_time == 0 && !_down) return true;
                if (ClipEnded() && _time > Prm.PickUpTime) { _down = false; SendAttackMsgToMario(); return true; }
                return false;

            case NerveThrown:                                    // TNerveTamaNokoThrown
                if (_time == 0)
                {
                    Vector3 f = PolarXZ(_throwYaw, 1f);
                    _vel = new Vector3(f.X * _throwPower * Prm.ThrownRateXZ, Prm.ThrownVY, f.Z * _throwPower * Prm.ThrownRateXZ);
                    _lin += Vector3.Up * 2f;
                    _airborne = true;
                }
                if (!_airborne && _time > 1) { _down = false; return true; }
                return false;

            case NerveSink:                                      // TNerveTamaNokoSink: stuck in the sand, struggling; out past SinkTime x the live fraction
                if (_time == 0) { _sunk = true; ChangeBck(BckMudLand); if (DebugPrint) GD.Print($"[SMS tamanoko] {Name}: stuck in the sand"); }
                if (ClipEnded())
                {
                    if (IsBck(BckMudLand)) ChangeBck(BckMogaki);
                    float sinkTime = Prm.SinkTime * LiveFraction();
                    if (_time > sinkTime)
                    {
                        if (IsBck(BckReturn)) { _sunk = false; PushAfterCurrent(NerveTWait); return true; }
                        ChangeBck(BckReturn);
                    }
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
