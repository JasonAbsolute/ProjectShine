using System;
using Godot;

/// <summary>
/// CATAQUACK -- TPoiHana (Enemy/poihana.cpp; JP ポイハナ, "fling flower") on <see cref="SmsSmallEnemy"/>, with the shipped /enemy/poihana.prm
/// (SMS pass 128, feature/enemy-pokey-cataquack). Gelato Beach's blue duck-flower that runs at Mario and FLINGS him into the air.
///
/// THE GAME. A walker (TWalkerEnemy's GraphWander / Attack) that starts awake and wandering (no set anim: its default nerve is the wander);
/// march 1.8 u/tick, turn 3..5 deg a tick, body scale 1.2..1.5 (the model is 191 units tall: 3.1 .. 3.8 m). FINDING MARIO: 800 u ahead within
/// 180 deg (300 u aware), 200 u of height -> the DASH (bck 0) at march x3 = 5.4 u/tick until he is 2000 u away / 300 u higher / in water.
/// THE FLING (TNervePoihanaThrow): on contact in Attack the throw clip (8) starts and FREEZES at frame 4 for 16 ticks (the bill under him),
/// at tick 4 the rumble and the two bursts at the mouth joint, at tick 16 SMS_SendMessageToMario(THROWN) + SMS_ThrowMario((0, 1,
/// -BackThrowVal 0) in the body's frame, ThrowSpeed 140 u/tick) -- straight up; the clip then plays out. The RED kind (TPoiHanaRed) also sends
/// ATTACK on that contact (dmgpoihana.prm: 1 HP, knock-down type 1, min speed 50) before the fling. Contact while NOT attacking (wandering,
/// sleeping) = plain ATTACK damage -- except asleep, where a touch WAKES it. WATER (behaveToWater, in wander / attack / jump): FREEZE
/// (TNervePoihanaFreeze): on SAND the waterhit (12) -> the pikupiku twitch loop (4) for FreezeWait 3000 ticks (25 s) -> the getup (2); on
/// hard ground the waterhit2 (13, 2.6 s: it staggers and rights itself) and that is all. A stomp / ground pound on a frozen one is NOT a kill:
/// isHitValid plays the humi squash (3) and genRandomItem -> the DISAP puff + its EVENT COIN (genEventCoin: the stage's attached coin pops
/// away from Mario at (10, 18, 10) u/tick, once) -- nothing else: TPoiHana::generateItem drops no random item. The only message that kills a
/// Cataquack is HIT_MESSAGE_UNKB = Yoshi EATING it (Yoshi.cpp 403); Mario alone cannot kill one -- faithful here (<see cref="StompKills"/> is the
/// deviation knob). SLEEP (mSleepVersion 1): after WakeFrame 6000 ticks (+ 100 x the instance index) of wandering, at a walk clip's end, the
/// sleep_start (6, 3 s) -> the sleep loop (5, with the ZZZ) for SleepFrame 200 ticks past the timer -> the waken (10) -> wander; a touch or a
/// spray wakes it at once (the waken from frame 148); the instance index staggers the first nap (index x -250). The SLEEPING kind
/// (TSleepPoiHana) starts asleep and cannot wake by itself (only a touch or the hose) -- and stands rooted (LIVE_FLAG_UNK10). The red kind
/// never sleeps. Gravity 0.15 a tick (TLiveActor's). Body colour: GX_TEVREG0 per kind on the '_body' material -- blue (-191, 8, 303), red
/// (283, -53, -122).
/// NOT PORTED (logged): the sand-bomb trap (TNervePoihanaTrapped: Gelato's rising sand pillars fling it -- no such object here), the
/// map-object stun (isCollidMove: a charging one hitting a MOVABLE map object knocks itself out -- his levels' walls are the bg, which never
/// stuns), Yoshi (the eat, the juice platform), the sounds (slots), doKeepDistance.
/// </summary>
[Tool]   // pass 128b: the body paint previews in the editor (SmsSmallEnemy._Ready / _PhysicsProcess return there after EditorPreview)
[GlobalClass]
public partial class SmsPoihana : SmsSmallEnemy
{
    public enum PoihanaKind { Blue, Red, Sleeping }

    // ------------------------------------------------------------------------------------------------------------------ poihana.prm
    public class PoihanaParams : Params
    {
        public float ThrowSpeed = 100f, BackThrowVal = 0.5f;
        public int SleepFrame = 1000, WakeFrame = 2000;
        public float TrapJumpMaxSpY = 10f, TrapJumpMinSpY = 10f, TrapJumpMaxSpXZ = 8f, TrapJumpMinSpXZ = 8f, TrapJumpGravity = 1f;
    }
    /// <summary>The shipped /enemy/poihana.prm (params.szs on his disc, read with prm_file.py).</summary>
    public static readonly PoihanaParams Prm = new PoihanaParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 12, ActiveEnemyNum = 12,
        JumpForce = 1f, SearchLength = 800f, SearchHeight = 200f, SearchAware = 300f, SearchAngle = 180f, GiveUpLength = 2000f, GiveUpHeight = 300f,
        AttackWait = 120, FreezeWait = 3000, DamageRadius = 100f, DamageHeight = 100f, AttackRadius = 120f, AttackHeight = 80f,
        TurnSpeedLow = 3f, TurnSpeedHigh = 5f, BodyScaleLow = 1.2f, BodyScaleHigh = 1.5f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 5f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 1.8f, MarchSpeedHigh = 1.8f,
        ThrowSpeed = 140f, BackThrowVal = 0f, SleepFrame = 200, WakeFrame = 6000,
        TrapJumpMaxSpY = 20f, TrapJumpMinSpY = 15f, TrapJumpMaxSpXZ = 10f, TrapJumpMinSpXZ = 8f, TrapJumpGravity = 0.2f,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Kind")]
    /// <summary>Blue = TPoiHana: flings Mario, no damage. Red = TPoiHanaRed: the fling hurts too (1 HP), and it never sleeps. Sleeping =
    /// TSleepPoiHana: a blue one born asleep that only a touch or the hose wakes. The kind also picks the game's paint (blue or red) unless
    /// <see cref="UseBodyColor"/> is on. Behaviour is fixed at birth; the paint follows a change live (and in the editor).</summary>
    [Export] public PoihanaKind Kind { get => _kind; set { _kind = value; if (IsInsideTree()) ApplyBodyColor(); } }
    private PoihanaKind _kind = PoihanaKind.Blue;

    [ExportGroup("Paint (off = the kind's game colour)")]
    /// <summary>On: the body is painted <see cref="BodyColor"/> instead of the kind's game paint -- any colour, behaviour unchanged. Shows in the
    /// editor too.</summary>
    [Export] public bool UseBodyColor { get => _useBodyColor; set { _useBodyColor = value; if (IsInsideTree()) ApplyBodyColor(); } }
    private bool _useBodyColor;
    /// <summary>The body's colour where the sun hits it full on; the game's toon shading darkens it in the shade and adds the sun's white glint on
    /// top. The pick is exact: the material's register is solved from it (c0 = 2 x colour - 1/2, the game's constant blue tint dropped), so a
    /// yellow is a yellow. The default is the blue kind's own lit colour (0, 68, 247). Only the body: the bill stays yellow and the eye its own.</summary>
    [Export(PropertyHint.ColorNoAlpha)] public Color BodyColor { get => _bodyColor; set { _bodyColor = value; if (IsInsideTree()) ApplyBodyColor(); } }
    private Color _bodyColor = new Color(0f, 0.2657f, 0.9696f);

    [ExportGroup("Ground")]
    /// <summary>What it stands on decides the flip: Auto = sand when the floor it touches is in the "sms_sand" group (or its name contains "sand"),
    /// else hard ground; Sand / Hard force it. SAND: the hose flips it onto its back for FreezeWait (25 s) and stomps pay the coin; HARD: it only
    /// staggers (waterhit2) and gets straight back up -- the game's rule (mGroundPlane->isSand()).</summary>
    [Export] public GroundKindEnum GroundKind = GroundKindEnum.Auto;
    public enum GroundKindEnum { Auto, Sand, Hard }

    [ExportGroup("Deviations (off = the game)")]
    /// <summary>DEVIATION: in the game only Yoshi eating it kills a Cataquack (HIT_MESSAGE_UNKB); a stomp on a flipped one pays its coin and it twitches
    /// on. On: a GROUND POUND on a flipped one (the twitch loop) kills it (the death clip, the puffs, the drop table).</summary>
    [Export] public bool StompKills = false;
    /// <summary>Ticks of wandering before the first nap (the game: WakeFrame 6000 + 100 x the instance index, ~50 s). -1 = the game's.</summary>
    [Export] public int WakeTicksOverride = -1;
    /// <summary>x the fling's ThrowSpeed (the game's 140: Mario rises 2450 units = 33 m -- the Gelato tower-top launch). 1 = the game.</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float ThrowSpeedScale = 1f;

    [ExportGroup("Coin and effects (empty = none)")]
    /// <summary>genEventCoin: the coin the stage attaches to this Cataquack, popped once when a stomp lands on it flipped -- his yellow_coin.tscn by
    /// default (DEVIATION: the game's placement data decides which ones carry a coin; here every one does unless this is cleared).</summary>
    [Export] public PackedScene EventCoinScene;
    public const string EventCoinScenePath = "res://yellow_coin.tscn";
    /// <summary>PARTICLE_MS_POI_KIZETSU: the dizzy stars while squashed / twitching (bck 3 / 4), bound to the body -- a looping GPUParticles3D scene. Default PoiDizzy.tscn (pass 131).</summary>
    [Export] public PackedScene DizzyFxScene;
    /// <summary>PARTICLE_MS_POI_ZZZ: the sleep bubbles while in the sleep loop (bck 5), bound to the body -- a looping GPUParticles3D scene. Default PoiZzz.tscn.</summary>
    [Export] public PackedScene ZzzFxScene;
    /// <summary>PARTICLE_MS_JUMP_ED_A + _B: the landing dust when it hits the ground flipping (waterhit frame 18), on hard ground. Default JumpEd.tscn.</summary>
    [Export] public PackedScene LandDustFxScene;
    /// <summary>PARTICLE_MS_POI_SAND (+ JUMP_ED_A): the sand spray of the flip (frame 18) and the getup (frame 34), on sand. Default PoiSand.tscn.</summary>
    [Export] public PackedScene SandFxScene;
    /// <summary>PARTICLE_MS_POI_DEAD: the splash of coloured water drops at the death clip's start (setDeadAnm: Yoshi's eat, or StompKills). Default PoiDead.tscn.</summary>
    [Export] public PackedScene DeadFxScene;
    public const string FxDir = "res://assets/Sms/Enemy/Fx131/";
    /// <summary>PARTICLE_MS_DMG_B + MS_M_AMIATTACK at the mouth joint, 4 ticks into the fling (with the rumble).</summary>
    [Export] public PackedScene ThrowFxScene;
    /// <summary>poihana_throw.bas / MSD_SE_EN_KOHANA_WAKEUP3 (the waken). Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D ThrowSound, WakeSound;

    // ------------------------------------------------------------------------------------------------------------------ the bck table
    // /scene/poihana (mamma0.szs): the files in order = setBckAnm's indices (poihana_bastable); frames + loop attributes read off the disc (bck.py)
    private static readonly string[] Names = { "poihana_dash", "poihana_death", "poihana_getup", "poihana_humi", "poihana_pikupiku", "poihana_sleep", "poihana_sleep_start", "poihana_spin", "poihana_throw", "poihana_wait", "poihana_waken", "poihana_walk", "poihana_waterhit", "poihana_waterhit2" };
    private static readonly int[] Frames = { 20, 89, 89, 67, 90, 416, 184, 90, 49, 80, 201, 40, 79, 156 };
    private static readonly bool[] Loops = { true, false, false, false, true, true, false, true, false, true, false, true, false, false };
    protected const int BckDash = 0, BckDeath = 1, BckGetup = 2, BckHumi = 3, BckPikupiku = 4, BckSleep = 5, BckSleepStart = 6, BckSpin = 7, BckThrow = 8, BckWait = 9, BckWaken = 10, BckWalk = 11, BckWaterhit = 12, BckWaterhit2 = 13;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    /// <summary>mMouthJntIndex 6 = the bmd's 'mouth1_2' (JNT1 order: 0 jnt_body, 1 eff_body, 2 upperbody, 3 mouth2_1, 4 mouth2_2, 5 mouth1_1, 6 mouth1_2, 7 Reye,
    /// 8 Leye, 9 jnt_tail, 10-12 the left leg, 13-15 the right), mBodyJntIndex 0 (jnt_body).</summary>
    public const int MouthJoint = 6, BodyJoint = 0;

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveSleep = NerveSpecies + 0, NervePoiFreeze = NerveSpecies + 1, NerveThrow = NerveSpecies + 2;
    protected override string NerveName(int n) => n switch { NerveSleep => "Sleep", NervePoiFreeze => "PoiFreeze", NerveThrow => "Throw", _ => base.NerveName(n) };
    /// <summary>TPoiHana::init: mSpine->initWith(Sleep) for the sleeping kind, else the wander -- no set anim.</summary>
    protected override int DefaultNerve => Kind == PoihanaKind.Sleeping && !_awakeKind ? NerveSleep : NerveGraphWander;

    // ------------------------------------------------------------------------------------------------------------------ state
    private bool _wakeNow;            // unk194: a touch while asleep -> the waken at once
    private bool _slept;              // unk195
    private bool _awakeKind = true;   // unk1A0: true = wakes by itself (the blue / red kinds; a touched sleeper becomes one)
    private int _goToSleep;           // mGoToSleepTimer
    private bool _wet;                // unk165: sprayed since last looked at (unsetUnk165)
    private int _throwTimer;          // mThrowTimer
    private bool _canSleep = true;    // unk1C8: false for the red kind
    private int _instanceIndex;       // mInstanceIndex: the nth Cataquack born in this level
    private bool _coinPaid;           // mCoin == nullptr after genEventCoin
    private bool _bckPausedForThrow;
    private Node3D _dizzyFx, _zzzFx;
    private ShaderMaterial _bodyMat;
    private Skeleton3D _skel;
    private static int _born;
    private int _freezeTime;          // the Freeze nerve's own clock (spine->getTime() while in it)

    public bool Sleeping => IsNerve(NerveSleep);
    public bool Frozen => IsNerve(NervePoiFreeze);
    public bool Throwing => IsNerve(NerveThrow);
    public int ThrowTimer => _throwTimer;
    public bool WakesBySelf => _awakeKind;
    public int SleepTimer => _goToSleep;
    public bool CoinPaid => _coinPaid;
    public int InstanceIndex => _instanceIndex;

    protected override void Init()
    {
        _flags150 = 17;                                   // unk150 = 17
        _instanceIndex = _born++;
        _awakeKind = Kind != PoihanaKind.Sleeping;         // TSleepPoiHana::load: unk1A0 = false
        _canSleep = Kind != PoihanaKind.Red;              // TPoiHanaRed::init: unk1C8 = false
        EventCoinScene ??= ResourceLoader.Exists(EventCoinScenePath) ? GD.Load<PackedScene>(EventCoinScenePath) : null;
        DizzyFxScene ??= LoadFx("PoiDizzy"); ZzzFxScene ??= LoadFx("PoiZzz"); LandDustFxScene ??= LoadFx("JumpEd"); SandFxScene ??= LoadFx("PoiSand"); DeadFxScene ??= LoadFx("PoiDead");   // pass 131
        DressBody();
    }
    private static PackedScene LoadFx(string name) => ResourceLoader.Exists(FxDir + name + ".tscn") ? GD.Load<PackedScene>(FxDir + name + ".tscn") : null;

    /// <summary>TPoiHanaManager::initSetEnemies: SMS_InitPacket_OneTevColor(GX_TEVREG0) on the '_body' material -- a per-instance copy of PoihanaBody.tres
    /// with the kind's colour (the red kind's (283, -53, -122)).</summary>
    private void DressBody()
    {
        if (_model == null) return;
        foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (n is not MeshInstance3D mi || mi.Mesh == null) continue;
            if (_skel == null) _skel = mi.GetParent() as Skeleton3D;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var m = mi.GetActiveMaterial(i);
                if (m is ShaderMaterial sm && (m.ResourceName ?? "").StartsWith("_body"))
                {
                    _bodyMat = (ShaderMaterial)sm.Duplicate();
                    mi.SetSurfaceOverrideMaterial(i, _bodyMat);
                }
            }
        }
        ApplyBodyColor();
    }
    /// <summary>The body's GX_TEVREG0 + konst 2 for the look: the kind's game registers (C0 blue (-191, 8, 303) / red (283, -53, -122), K2 (0, 0, 32)),
    /// or the pick solved back through the material's stages (pass 128b). Stage 0 with no texture is s0 = 1/2 (c0 + mix(1, COLOR0, 5/8) - 1/2) --
    /// at full light COLOR0 = 1, so s0 = c0 / 2 + 1/4 -- then stage 1 is s0 x toon (1 there) + K2: the lit colour = c0 / 2 + 1/4 + K2. K2 = 0 for
    /// a pick so every channel lands exactly (the game's blue cast would floor a yellow's blue at 32), and c0 = 2 x pick - 1/2.</summary>
    private void ApplyBodyColor()
    {
        if (_bodyMat == null) return;
        Vector3 c0, k2;
        if (_useBodyColor)
        {
            c0 = new Vector3(2f * _bodyColor.R - 0.5f, 2f * _bodyColor.G - 0.5f, 2f * _bodyColor.B - 0.5f);
            k2 = Vector3.Zero;
        }
        else
        {
            c0 = _kind == PoihanaKind.Red ? new Vector3(283f, -53f, -122f) / 255f : new Vector3(-191f, 8f, 303f) / 255f;
            k2 = new Vector3(0f, 0f, 32f / 255f);
        }
        _bodyMat.SetShaderParameter("c0", c0);
        _bodyMat.SetShaderParameter("c2", k2);
    }
    /// <summary>The body's GX_TEVREG0 / konst 2 as set (a drive reads them).</summary>
    public Vector3 BodyColorNow => _bodyMat == null ? Vector3.Zero : _bodyMat.GetShaderParameter("c0").AsVector3();
    public Vector3 BodyKonstNow => _bodyMat == null ? Vector3.Zero : _bodyMat.GetShaderParameter("c2").AsVector3();
    /// <summary>[Tool]: the editor shows the kind's paint or the pick on the placed body (the override material is not saved -- the glb's nodes are the instance's own).</summary>
    protected override void EditorPreview() => DressBody();

    protected override void Reset()
    {
        base.Reset();
        _wakeNow = false; _slept = false; _goToSleep = 0; _wet = false; _throwTimer = 0; _coinPaid = false; _freezeTime = 0;
        if (!_resetOnce && _awakeKind) _goToSleep = _instanceIndex * -250;   // init runs AFTER load's reset in the game: the birth keeps mInstanceIndex x -250; a relaunch's resetSRTV zeroes it
        _resetOnce = true;
        SetBckPaused(false); _bckPausedForThrow = false;
        BoundFx(ref _dizzyFx, null, false); BoundFx(ref _zzzFx, null, false);
        _noBind = false;
    }
    private bool _resetOnce;

    // ------------------------------------------------------------------------------------------------------------------ anims
    protected override void SetWalkAnm() => SetBck(BckWalk);
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetRunAnm() => SetBck(BckDash);
    protected override void SetFreezeAnm() => SetBck(BckWaterhit);
    protected override void SetMeltAnm() => SetBck(BckDeath);
    /// <summary>TPoiHana::setDeadAnm: the collision off, the death clip (1), PARTICLE_MS_POI_DEAD + the dust (JUMP_ED_A/B, or POI_SAND + JUMP_ED_A on sand).</summary>
    protected override void SetDeadAnm()
    {
        SetBck(BckDeath);
        SpawnBurst(DeadFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));                                  // PARTICLE_MS_POI_DEAD at mPosition, mScaling
        SpawnBurst(GroundIsSand() ? SandFxScene : LandDustFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
    }

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    protected override void MoveObjectPre()
    {
        // the bound effects follow the nerves' clips (calcRootMatrix: KIZETSU while bck 3 / 4, ZZZ while bck 5)
        BoundFx(ref _dizzyFx, DizzyFxScene, !_dead && (IsBck(BckHumi) || IsBck(BckPikupiku)));
        BoundFx(ref _zzzFx, ZzzFxScene, !_dead && IsBck(BckSleep));
        // the clip-frame cues: the flip's landing dust at frame 18 (waterhit / waterhit2), the getup's sand at 34
        if ((IsBck(BckWaterhit) || IsBck(BckWaterhit2)) && PassedFrame(18f))
            SpawnBurst(GroundIsSand() ? SandFxScene : LandDustFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
        if (IsBck(BckGetup) && GroundIsSand() && PassedFrame(34f))
            SpawnBurst(SandFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
        _lastFrame = BckFrame();
    }
    private float _lastFrame;
    /// <summary>getFrameCtrl()->checkPass(f): the clip crossed frame f this tick.</summary>
    private bool PassedFrame(float f) => _lastFrame < f && BckFrame() >= f;

    /// <summary>TPoiHana::walkBehavior: the walk, and (mSleepVersion) the sleep timer while wandering: past WakeFrame + 100 x the index at a clip's end
    /// -> the Sleep nerve replaces the wander (setNext).</summary>
    protected override void WalkBehavior(int mode, float k)
    {
        base.WalkBehavior(mode, k);
        if (mode != 0 || !_canSleep) return;
        _goToSleep++;
        if (BckEnded() && _goToSleep > WakeTicks())
        {
            _goToSleep = _rng.Next(-500, 501);                // TMsRange<s32>(-500, 500).rand()
            SetNext(NerveSleep);
            _slept = true;
        }
    }
    private int WakeTicks() => WakeTicksOverride >= 0 ? WakeTicksOverride : Prm.WakeFrame + _instanceIndex * 100;

    protected override Mario.SmsDamageProfile DamageProfile => Mario.SmsDamageProfile.Poihana;

    /// <summary>TPoiHana::attackToMario (+ TPoiHanaRed's): asleep -> wake; attacking -> the Throw (the red kind hits first); otherwise (not already
    /// throwing / frozen) plain ATTACK damage.</summary>
    protected override void AttackToMario()
    {
        if (IsNerve(NerveSleep))
        {
            _awakeKind = true; _wakeNow = true; _noBind = false;
            return;
        }
        if (IsNerve(NerveAttack))
        {
            if (Kind == PoihanaKind.Red) SendAttackMsgToMario();
            PushNerve(NerveThrow);
            return;
        }
        if (!IsNerve(NerveThrow) && !IsNerve(NervePoiFreeze)) SendAttackMsgToMario();
    }

    /// <summary>TPoiHana::behaveToWater: noted (unk165); wandering / attacking / jumping -> the Freeze.</summary>
    protected override void BehaveToWater()
    {
        _wet = true;
        if (IsNerve(NerveGraphWander) || IsNerve(NerveAttack) || IsNerve(NerveJump)) PushNerve(NervePoiFreeze);
    }

    /// <summary>TPoiHana::isHitValid: only HIT_MESSAGE_UNKB (Yoshi's eat) kills; a hit on a frozen one plays the humi squash and genRandomItem (the puff +
    /// the event coin). <see cref="StompKills"/> (deviation): a ground pound on the twitch loop kills.</summary>
    protected override bool IsHitValid(HitMessage msg)
    {
        if (StompKills && msg == HitMessage.HipDrop && IsNerve(NervePoiFreeze) && (IsBck(BckPikupiku) || IsBck(BckHumi))) return true;
        if (IsNerve(NervePoiFreeze))
        {
            SetBck(BckHumi);
            SetAfterDeadEffect();                             // genRandomItem: the DISAP puff ...
            GenEventCoin();                                   // ... + genEventCoin; generateItem drops nothing (TPoiHana's override)
        }
        return false;
    }

    /// <summary>TPoiHana::genEventCoin: the attached coin appears at the body and pops away from Mario at (10, 18, 10) u/tick -- once.</summary>
    private void GenEventCoin()
    {
        if (_coinPaid || EventCoinScene == null) return;
        _coinPaid = true;
        Vector3 away = Pos() - MarioPos(); away.Y = 0f;
        away = away.LengthSquared() > 1e-6f ? away.Normalized() : PolarXZ(_yaw, 1f);
        DropSpawner.SpawnItem(EventCoinScene, GetParent(), GlobalPosition + Vector3.Up * (P.HeadHeight * 0.5f / U), new Vector3(away.X * 10f, 18f, away.Z * 10f), 0, this);
    }

    /// <summary>mGroundPlane->isSand(): the floor under the body (the sms_sand group, or "sand" in the collider's name), or the knob.</summary>
    public bool GroundIsSand()
    {
        if (GroundKind != GroundKindEnum.Auto) return GroundKind == GroundKindEnum.Sand;
        var space = GetWorld3D().DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * 0.5f, GlobalPosition + Vector3.Down * 3f, CollisionMask);
        var excl = new Godot.Collections.Array<Rid> { GetRid() };
        if (_mario != null && IsInstanceValid(_mario)) excl.Add(_mario.GetRid());
        q.Exclude = excl;
        var hit = space.IntersectRay(q);
        if (hit.Count == 0) return false;
        if (hit["collider"].AsGodotObject() is Node n)
        {
            for (Node p = n; p != null; p = p.GetParent())
            {
                if (p.IsInGroup("sms_sand") || p.Name.ToString().Contains("sand", StringComparison.OrdinalIgnoreCase)) return true;
                if (p is PhysicsBody3D && p != n) break;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------------------------------------------------------ the species' nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveSleep: return NerveSleepTick();
            case NervePoiFreeze: return NervePoiFreezeTick();
            case NerveThrow: return NerveThrowTick();
        }
        return base.ExecuteNerve(n);
    }

    /// <summary>TNervePoihanaSleep.</summary>
    private bool NerveSleepTick()
    {
        if (_time == 0)
        {
            if (!_canSleep) { _goToSleep = 0; PushAfterCurrent(NerveGraphWander); _noBind = false; return true; }
            SetBck(BckSleepStart);
            _airborne = true;                                  // onLiveFlag(AIRBORNE): it settles onto the ground as it curls up
        }
        if (!_awakeKind && !_airborne) _noBind = true;        // the sleeping kind is rooted (LIVE_FLAG_UNK10)
        _goToSleep++;
        bool wet = _wet; _wet = false;                        // unsetUnk165
        if ((_wakeNow || wet) && !IsBck(BckWaken))
        {
            if (WakeSound != null && WakeSound.Stream != null) WakeSound.Play();   // MSD_SE_EN_KOHANA_WAKEUP3
            SetBck(BckWaken);
            SeekBck(148f);                                     // the last 52 frames of the waken
        }
        if (BckEnded())
        {
            if (IsBck(BckSleepStart)) SetBck(BckSleep);
            else if (IsBck(BckSleep))
            {
                if (_awakeKind && _goToSleep > Prm.SleepFrame) SetBck(BckWaken);
            }
            else
            {
                if (IsBck(BckWaken) || !_canSleep) { _goToSleep = 0; PushAfterCurrent(NerveGraphWander); _noBind = false; return true; }
                SetBck(BckSleepStart);
            }
        }
        return false;
    }

    /// <summary>TNervePoihanaFreeze: the flip -- waterhit (sand) or waterhit2 (hard ground), the attack collision off; waterhit / humi -> the pikupiku
    /// twitch; the twitch -> the getup once FreezeWait is up; getup / waterhit2 -> the collision back, done.</summary>
    private bool NervePoiFreezeTick()
    {
        if (_time == 0)
        {
            SetBck(GroundIsSand() ? BckWaterhit : BckWaterhit2);
            _freezeTime = 0;
        }
        _freezeTime++;
        if (BckEnded())
        {
            if (IsBck(BckWaterhit) || IsBck(BckHumi)) SetBck(BckPikupiku);
            else if (IsBck(BckPikupiku)) SetBck(_freezeTime > Prm.FreezeWait ? BckGetup : BckPikupiku);
            else if (IsBck(BckGetup) || IsBck(BckWaterhit2)) return true;
        }
        return false;
    }

    /// <summary>TNervePoihanaThrow: the throw clip held at frame 4 for 16 ticks (the rumble and the mouth bursts at tick 4), then Mario is THROWN
    /// (SMS_ThrowMario((0, 1, -BackThrowVal) in the body's frame, ThrowSpeed)) and the clip plays out.</summary>
    private bool NerveThrowTick()
    {
        if (_time == 0)
        {
            SetBck(BckThrow);
            _throwTimer = 1;
            if (ThrowSound != null && ThrowSound.Stream != null) ThrowSound.Play();
        }
        if (BckFrame() >= 4f && _throwTimer > 0 && !_bckPausedForThrow) { SetBckPaused(true); _bckPausedForThrow = true; }
        if (_throwTimer > 0)
        {
            _throwTimer++;
            if (_throwTimer > 16)
            {
                Vector3 dir = new Basis(Vector3.Up, Mathf.DegToRad(_yaw)) * new Vector3(0f, 1f, -Prm.BackThrowVal);   // MsMtxSetRotRPH(rot) x (0, 1, -back)
                _mario?.SmsThrowMario(dir, Prm.ThrowSpeed * ThrowSpeedScale, this);
                _throwTimer = 0;
                SetBckPaused(false); _bckPausedForThrow = false;
            }
        }
        if (_throwTimer == 4)
        {
            Input.StartJoyVibration(0, 0.6f, 0.9f, 0.25f);    // SMSRumbleMgr->start(0x15, 0xf)
            Transform3D at = MouthTransform();
            SpawnBurst(ThrowFxScene, new Transform3D(at.Basis.Orthonormalized() * Basis.FromScale(Vector3.One * _scale), at.Origin));
        }
        return BckEnded();
    }

    /// <summary>The mouth joint's world transform (mMouthJntIndex 6 = the bmd's 'mouth1_2' -- Godot's bone order follows the bmd's, probed), or the body's
    /// head point when there is no skeleton.</summary>
    public Transform3D MouthTransform()
    {
        if (_skel != null)
        {
            int b = _skel.FindBone(MouthJointName);
            if (b < 0 && _skel.GetBoneCount() > MouthJoint) b = MouthJoint;
            if (b >= 0) return _skel.GlobalTransform * _skel.GetBoneGlobalPose(b);
        }
        return new Transform3D(Basis.Identity, GlobalPosition + Vector3.Up * (P.HeadHeight * _scale / U));
    }
    public const string MouthJointName = "mouth1_2";

    /// <summary>A looping bound effect (emitAndBindToPosPtr(&amp;mPosition)): the scene kept as a child of the body at its origin while `on` -- the jpa's own
    /// emitter translation (the stars 120 u up, the Zs 100 u) is baked into the scene -- scaled by the body scale, freed when not.</summary>
    private void BoundFx(ref Node3D slot, PackedScene scene, bool on)
    {
        if (on && slot == null && scene != null)
        {
            slot = scene.Instantiate<Node3D>();
            AddChild(slot);
            slot.Position = Vector3.Zero;
            slot.Scale = Vector3.One * _scale;
        }
        else if (!on && slot != null) { slot.QueueFree(); slot = null; }
    }
}
