using Godot;
using System;

/// <summary>
/// SMS pass 129 -- the Pokey, THanaSambo (doldecomp/sms src/Enemy/hanasambo.cpp, include/Enemy/HanaSambo.hpp), a TSmallEnemy that never
/// walks: it lives buried where it was placed, rises when Mario comes near, leans its head at him, and sinks back when he leaves.
///
/// THE GAME (per tick, 120 Hz; the clips at 60 bck frames a second):
/// - Hide (the default nerve): sambo_fhide (79) with MS_SMB_HD_ROCK + HD_SMOKE; at tick 75 and at the clip's end initFlower: a sunflower bud
///   (<see cref="SmsSamboFlower"/>) marks the spot at the ground; then no collision, sambo_fset held at frame 0, LIVE_FLAG_HIDDEN. Every tick:
///   Mario within AppearDist (1000 u) -> Appear (even mid-sink).
/// - Appear: shown, collision on, sambo_fset (79: the rise) with MS_SMB_AP_ROCK + AP_SMOKE, the bud hidden; turning at 3 x the turn speed
///   toward Mario; at the end -> Wait, and straight on to Attack when Mario is within AttackDist (500 u) and below the base + 100.
/// - Wait: sambo_fwait (40, loop) turning toward Mario; each pass of frame 16 createPollen (MS_SMB_KAFUN_OW 200 u over the pollen joint);
///   past AttackInterval (150) with Mario within AttackDist and below +100 -> Attack; Mario beyond HideDist (2000 u) -> Hide.
/// - Attack: sambo_fattack_start (34); at its end createPollen (MS_SMB_KAFUN_OA 200 u ahead of the pollen joint) + MSD_SE_EN_SAMBO_ATTACK and
///   sambo_fattack (20, loop: the lean); each loop's end past AttackingTime (250) with no water since the last look -> sambo_fattack_end (14),
///   then -> Wait (water on the head keeps it in the lean).
/// - The head hit actor (THanaSamboHead at jnt_head, attack 60 x 20, damage 120 x 40, x the body scale): Mario in its attack cylinder ->
///   ATTACK (dmgenemycommon); his TRAMPLE / HIP_DROP on it -> kill (the only way: the body's isHitValid takes only Yoshi's eat, and its
///   own damage cylinder is 5 x 5); the hose on it -> waterDamage: the wet flag, and in Wait -> Freeze: sambo_fhit_start (4) then sambo_hit
///   (20, loop) for as long as water keeps coming, then back to Wait. The body's attack cylinder (40 x 60 at the base) hurts on touch.
/// - Die (kill: a stomp on the head): no collision, the model swapped for samboD.bmd -- the stack as four loose pieces -- playing sambo_ydown
///   when it was leaning (36) else sambo_down (36); at the clip's end (or tick 300) MS_ENM_DISAP_A + _B at each piece (Dhead, Dbody3, Dbody2,
///   Dbody1), LIVE_FLAG_DEAD, genRandomItem (the drop table). Rooted: mPosition.x / z are pinned to the placed point every tick; isCollidMove
///   false (nothing pushes it).
/// hanasambo.prm: the TSmallEnemy fields + AttackDist 500, AttackInterval 150, AppearDist 1000, HideDist 2000, AttackingTime 250,
/// HeadAttackRadius 60 / Height 20, HeadDamageRadius 120 / Height 40; body scale 1 (the stack 360 u = 4.8 m tall).
/// DEVIATIONS (logged): <see cref="StartBuried"/> (the game plays the sink at the stage's load, in view or not); a Pokey's re-shown bud takes
/// the hose (the game's initFlower leaves its collision off after a hide -- a slip); a Freeze whose 4-frame start ends with no water left ends
/// (the game holds the pose until the next drop); the particles and sounds are slots (empty = none); Yoshi's eat and the juice (no Yoshi).
/// </summary>
[GlobalClass]
public partial class SmsHanaSambo : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ hanasambo.prm
    public class SamboParams : Params
    {
        public float AttackDist = 200f, AppearDist = 800f, HideDist = 1000f, HeadAttackRadius = 60f, HeadAttackHeight = 20f, HeadDamageRadius = 80f, HeadDamageHeight = 40f;
        public int AttackInterval = 200, AttackingTime = 30;
    }
    /// <summary>The shipped /enemy/hanasambo.prm (params.szs on his disc, read with prm_file.py).</summary>
    public static readonly SamboParams Prm = new SamboParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 100, ActiveEnemyNum = 30,
        JumpForce = 10f, SearchLength = 1500f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 2000f, GiveUpHeight = 650f,
        AttackWait = 120, FreezeWait = 300, DamageRadius = 5f, DamageHeight = 5f, AttackRadius = 40f, AttackHeight = 60f,
        TurnSpeedLow = 3f, TurnSpeedHigh = 5f, BodyScaleLow = 1f, BodyScaleHigh = 1f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100, StampRange = 5f,
        AttackDist = 500f, AttackInterval = 150, AppearDist = 1000f, HideDist = 2000f, AttackingTime = 250,
        HeadAttackRadius = 60f, HeadAttackHeight = 20f, HeadDamageRadius = 120f, HeadDamageHeight = 40f,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (off = the game)")]
    /// <summary>On: a placed Pokey starts already buried (the bud on the spot) instead of standing and sinking for 1.3 s when the level loads --
    /// the game plays that sink at the stage's load whether or not it is in view. A re-launch after a death plays the game's sink.</summary>
    [Export] public bool StartBuried = true;

    [ExportGroup("The flower and the dead stack")]
    /// <summary>The sunflower bud that marks a buried Pokey (TSamboFlower; the hose opens it). Default: assets/Sms/Enemy/Sambo/SamboFlower.tscn.</summary>
    [Export] public PackedScene FlowerScene;
    public const string FlowerScenePath = "res://assets/Sms/Enemy/Sambo/SamboFlower.tscn";
    /// <summary>The death clips for the ModelDead glb (samboD.bmd carries no clips of its own in the rip): sambo_down and sambo_ydown baked from the
    /// disc's bck (bck_to_anim.py). Defaults: assets/Sms/Enemy/Sambo/SamboDown.tres / SamboYdown.tres.</summary>
    [Export] public Animation DeadDownClip, DeadYdownClip;

    [ExportGroup("Effects and sounds (empty = none)")]
    /// <summary>PARTICLE_MS_SMB_AP_ROCK + AP_SMOKE at the base when it rises (one scene for both, or either).</summary>
    [Export] public PackedScene AppearFxScene;
    /// <summary>PARTICLE_MS_SMB_HD_ROCK + HD_SMOKE at the base when it sinks.</summary>
    [Export] public PackedScene HideFxScene;
    /// <summary>PARTICLE_MS_SMB_KAFUN_OW: the pollen drifting off the crown each wait loop (200 u over the pollen joint).</summary>
    [Export] public PackedScene PollenWaitFxScene;
    /// <summary>PARTICLE_MS_SMB_KAFUN_OA: the pollen puffed at Mario as the lean begins (200 u ahead of the pollen joint).</summary>
    [Export] public PackedScene PollenAttackFxScene;
    /// <summary>MSD_SE_EN_SAMBO_ATTACK as the lean begins. Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D AttackSound;

    // ------------------------------------------------------------------------------------------------------------------ the bck table
    // /scene/sambo (bianco3.szs): the files in order = setBckAnm's indices (sambo_bastable); frames + loop attributes read off the disc (bck.py).
    // sambo_down / sambo_ydown / sambo_ydamage are the DEAD model's clips (6 / 7 joints): never played on Model, see the ModelDead player.
    private static readonly string[] Names = { "sambo_down", "sambo_fattack", "sambo_fattack_end", "sambo_fattack_start", "sambo_fhide", "sambo_fhit_start", "sambo_fset", "sambo_fwait", "sambo_hit", "sambo_ydamage", "sambo_ydown" };
    private static readonly int[] Frames = { 36, 20, 14, 34, 79, 4, 79, 40, 20, 44, 36 };
    private static readonly bool[] Loops = { false, true, false, false, false, false, false, true, true, false, false };
    protected const int BckDown = 0, BckFattack = 1, BckFattackEnd = 2, BckFattackStart = 3, BckFhide = 4, BckFhitStart = 5, BckFset = 6, BckFwait = 7, BckHit = 8, BckYdamage = 9, BckYdown = 10;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    /// <summary>mHeadJntIndex 3 = 'jnt_head' (JNT1: 0 jnt1_1, 1 jnt1_2, 2 jnt1_3, 3 jnt_head, 4 null4, 5 null2, 6 head, 7 body3, 8 body2, 9 body1); mPollenJntIndex 6 = 'head'.</summary>
    public const string HeadJoint = "jnt_head", PollenJoint = "head";
    /// <summary>TNerveHanaSamboDie's jIndexTable {1, 3, 4, 5} on samboD.bmd (JNT1: 0 null5_2, 1 Dhead, 2 Dflower2, 3 Dbody3, 4 Dbody2, 5 Dbody1).</summary>
    private static readonly string[] DeadPieces = { "Dhead", "Dbody3", "Dbody2", "Dbody1" };

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveHide = NerveSpecies + 0, NerveAppear = NerveSpecies + 1, NerveSWait = NerveSpecies + 2, NerveSAttack = NerveSpecies + 3, NerveSFreeze = NerveSpecies + 4, NerveSDie = NerveSpecies + 5;
    protected override string NerveName(int n) => n switch { NerveHide => "Hide", NerveAppear => "Appear", NerveSWait => "SamboWait", NerveSAttack => "SamboAttack", NerveSFreeze => "SamboFreeze", NerveSDie => "SamboDie", _ => base.NerveName(n) };
    protected override int DefaultNerve => NerveHide;

    // ------------------------------------------------------------------------------------------------------------------ state
    private bool _wet;                // unk165: water on the head since the last look (unsetUnk165)
    private bool _attackPose;         // unk1B0: the lean is on (the death plays ydown)
    private bool _hidden;             // LIVE_FLAG_HIDDEN
    private bool _headDead;           // THanaSamboHead::kill: the head actor off
    private bool _skipSink;           // StartBuried: the first Hide goes straight to the buried state (cleared by a death: the re-launch sinks)
    private Vector3 _root;            // unk19C: the placed point, units
    private float _prevFrame;
    private Skeleton3D _skel, _skelDead;
    private Node3D _modelDead;
    private AnimationPlayer _deadAnim;
    private CollisionShape3D _headShape;
    private SmsSamboFlower _flower;
    private int _deadStart;

    public bool Hidden => _hidden;
    public bool Wet => _wet;
    public bool Leaning => _attackPose;
    public SmsSamboFlower Flower => _flower;
    public string DeadClipName => _deadAnim != null && _deadAnim.IsPlaying() ? _deadAnim.CurrentAnimation : "";
    /// <summary>The puffs the last death spawned (4 at the pieces; a drive reads it).</summary>
    public int DeathPuffs { get; private set; }

    protected override void Init()
    {
        _flags150 = 17;                                   // unk150 = 0x11
        _root = Pos();
        SetGoalMario();                                   // setGoalPath(TPathNode(gpMarioAddress))
        if (_model != null)
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        _modelDead = GetNodeOrNull<Node3D>("ModelDead");
        if (_modelDead != null)
        {
            foreach (Node n in _modelDead.FindChildren("*", "Skeleton3D", true, false)) { _skelDead = n as Skeleton3D; if (_skelDead != null) break; }
            _modelDead.Visible = false;
            BuildDeadPlayer();
        }
        _headShape = GetNodeOrNull<CollisionShape3D>("HeadShape");
        FlowerScene ??= ResourceLoader.Exists(FlowerScenePath) ? GD.Load<PackedScene>(FlowerScenePath) : null;
        _skipSink = StartBuried;
    }

    /// <summary>samboD.bmd's clips on the ModelDead glb: an AnimationPlayer under it with the two baked Animations, every track re-pointed at its
    /// skeleton (the .tres names the bones; the node path is this instance's).</summary>
    private void BuildDeadPlayer()
    {
        DeadDownClip ??= ResourceLoader.Exists("res://assets/Sms/Enemy/Sambo/SamboDown.tres") ? GD.Load<Animation>("res://assets/Sms/Enemy/Sambo/SamboDown.tres") : null;
        DeadYdownClip ??= ResourceLoader.Exists("res://assets/Sms/Enemy/Sambo/SamboYdown.tres") ? GD.Load<Animation>("res://assets/Sms/Enemy/Sambo/SamboYdown.tres") : null;
        _deadAnim = _modelDead.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (_deadAnim == null)
        {
            _deadAnim = new AnimationPlayer { Name = "AnimationPlayer" };
            _modelDead.AddChild(_deadAnim);
            _deadAnim.RootNode = _deadAnim.GetPathTo(_modelDead);
        }
        var lib = new AnimationLibrary();
        foreach (var (name, src) in new[] { ("sambo_down", DeadDownClip), ("sambo_ydown", DeadYdownClip) })
        {
            if (src == null) continue;
            var a = (Animation)src.Duplicate();
            a.LoopMode = Animation.LoopModeEnum.None;
            if (_skelDead != null)
                for (int t = 0; t < a.GetTrackCount(); t++)
                {
                    NodePath tp = a.TrackGetPath(t);
                    if (tp.GetSubNameCount() == 0) continue;
                    a.TrackSetPath(t, new NodePath($"{_modelDead.GetPathTo(_skelDead)}:{MatchBone(_skelDead, tp.GetSubName(0))}"));
                }
            lib.AddAnimation(name, a);
        }
        if (_deadAnim.HasAnimationLibrary("")) _deadAnim.RemoveAnimationLibrary("");
        _deadAnim.AddAnimationLibrary("", lib);
    }

    protected override void Reset()
    {
        _root = Pos();                                    // unk19C: before the base's ApplyLook pins to it
        base.Reset();
        _wet = false; _attackPose = false; _headDead = false; _deadStart = 0; _prevFrame = 0f;
        SetHidden(false);
        if (_modelDead != null) _modelDead.Visible = false;
        _flower?.ResetBud();
    }

    // ------------------------------------------------------------------------------------------------------------------ anims
    protected override void SetWaitAnm() { SetBck(BckFwait); _attackPose = false; }
    /// <summary>setAttackAnm: the lean's start; unk1B0 on (the death plays ydown).</summary>
    private void SetAttackAnm() { SetBck(BckFattackStart); _attackPose = true; }
    /// <summary>setDeadAnm: mMActor = samboD.bmd, bck ydown when leaning else down; the head actor off; LIVE_FLAG_UNK8.</summary>
    protected override void SetDeadAnm()
    {
        if (_model != null) _model.Visible = false;
        if (_anim != null) _anim.Stop();
        if (_modelDead != null) _modelDead.Visible = true;
        string clip = _attackPose ? "sambo_ydown" : "sambo_down";
        if (_deadAnim != null && _deadAnim.HasAnimation(clip)) { _deadAnim.Play(clip); _deadAnim.Seek(0f, true); _deadAnim.SpeedScale = 1f; }
        _headDead = true;
        _attackPose = false;
        _deadStart = _time;
    }
    private bool DeadClipEnded => _deadAnim == null || !_deadAnim.IsPlaying();

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    /// <summary>THanaSambo::isCollidMove: false -- nothing pushes a rooted Pokey.</summary>
    protected override bool IsCollidMove(Node3D other) => false;
    protected override bool WallHopAllowed() => false;
    /// <summary>THanaSambo::isHitValid: only HIT_MESSAGE_UNKB (Yoshi's eat) -- the BODY takes no stomp; the head actor does (<see cref="HitCheckMario"/>).</summary>
    protected override bool IsHitValid(HitMessage msg) => false;
    /// <summary>TSmallEnemy::attackToMario (not TWalkerEnemy's): the message only -- no hop, no PostAttack.</summary>
    protected override void AttackToMario() => SendAttackMsgToMario();
    /// <summary>THanaSambo::behaveToWater is empty: water on the stalk does nothing (the head's is <see cref="WaterDamage"/>).</summary>
    protected override void BehaveToWater() { }

    /// <summary>TSmallEnemy::kill -> THanaSambo::kill: hp 1, the Die nerve now (the stack dropped), the head actor off, LIVE_FLAG_UNK40.</summary>
    public override void Kill()
    {
        if (_dead) return;
        _hp = 1;
        if (_cur != NerveSDie) { _stack.Clear(); SetNext(NerveSDie); }
        _headDead = true;
    }

    /// <summary>THanaSambo::waterDamage (the head actor's SPRAYED_BY_WATER): the wet flag; in Wait -> Freeze pushed over it.</summary>
    public void WaterDamage()
    {
        _wet = true;
        if (_cur == NerveSWait) PushNerve(NerveSFreeze);
    }
    private bool UnsetWet() { bool w = _wet; _wet = false; return w; }

    /// <summary>A drop (or the hose at close range) on the body: the mark; within the head's damage cylinder (120 x 40 x scale at jnt_head) it is
    /// the head actor's message -> waterDamage; on the stalk nothing (behaveToWater is empty). Taken either way (the drop dies).</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead || _hidden || _cur == NerveSDie) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        float s = _bodyScale;
        if (CheckDistance(hitPos * U, 50f, 80f, HeadPos(), Prm.HeadDamageRadius * s, Prm.HeadDamageHeight * s)) WaterDamage();   // the drop's own 50 x 80 cylinder (checkWater)
        return true;
    }

    /// <summary>The body's lists (the base: the stalk's 40 x 60 attack cylinder hurts on touch; its 5 x 5 damage cylinder takes nothing) PLUS the head
    /// actor's (THanaSamboHead): Mario's attack cylinder on the head's damage cylinder -> TRAMPLE / HIP_DROP -> kill, the hose -> waterDamage;
    /// the head's attack cylinder on Mario's damage cylinder -> checkHit -> ATTACK.</summary>
    protected override void HitCheckMario()
    {
        if (_cur == NerveSDie) return;
        base.HitCheckMario();
        if (_mario == null || _dead || _hidden || _headDead || _cur == NerveSDie || !GetCollisionLayerValue(1)) return;
        Vector3 h = HeadPos(), m = MarioPos();
        float s = _bodyScale;
        bool onGround = _mario.IsOnFloor();
        bool rising = !onGround && _mario.Velocity.Y > 0f;
        float ar = onGround ? SmsParams.Mario.HoldRadius : (rising ? SmsParams.Mario.PushupRadius : SmsParams.Mario.TrampleRadius);
        float ah = onGround ? SmsParams.Mario.AttackHeight : (rising ? SmsParams.Mario.PushupHeight : SmsParams.Mario.AttackHeight);
        if (CheckDistance(m, ar, ah, h, Prm.HeadDamageRadius * s, Prm.HeadDamageHeight * s))
        {
            bool falling = !onGround && _mario.Velocity.Y < 0f && h.Y < m.Y;
            var st = _mario.stateOfMario;
            bool noTrample = st == Mario.MarioState.diving || st == Mario.MarioState.singleJumpDive || st == Mario.MarioState.doubleJumpDive || st == Mario.MarioState.tripleJumpDive
                || st == Mario.MarioState.bellySlidingFromDive || st == Mario.MarioState.swimming || st == Mario.MarioState.ledgeHang || st == Mario.MarioState.ledgeClimb
                || st == Mario.MarioState.treeGrab || st == Mario.MarioState.treeClimb || st == Mario.MarioState.hurt || st == Mario.MarioState.dead;
            if (falling && st == Mario.MarioState.groundPoundFalling) { Kill(); return; }                 // HIP_DROP -> unk68->kill()
            if (falling && !noTrample) { Kill(); _mario.StompBounce(); return; }                        // TRAMPLE -> kill; trampleExec bounces him
            if (_mario.SmsHoseSpraying) WaterDamage();
            if (_cur == NerveSDie) return;
        }
        if (CheckDistance(h, Prm.HeadAttackRadius * s, Prm.HeadAttackHeight * s, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight))
            SendAttackMsgToMario();                                                                   // THanaSamboHead::checkHit
    }

    /// <summary>The head joint's world point, units (the head actor rides getAnmMtx(mHeadJntIndex)).</summary>
    public Vector3 HeadPos() => BoneWorld(HeadJoint, 246.6f).Origin * U;
    /// <summary>A live-model bone's world transform; the fallback stands `restY` units over the base at the body scale.</summary>
    private Transform3D BoneWorld(string bone, float restY)
    {
        if (_skel != null)
        {
            int b = _skel.FindBone(bone);
            if (b >= 0) return _skel.GlobalTransform * _skel.GetBoneGlobalPose(b);
        }
        return new Transform3D(Basis.Identity, GlobalPosition + Vector3.Up * (restY * _scale / U));
    }

    /// <summary>createPollen: the KAFUN emitter at the pollen joint, its frame the joint's turned -90 about Z; in Wait the OW one lifted 200 u,
    /// else the OA one pushed 200 u along the facing.</summary>
    private void CreatePollen()
    {
        Transform3D j = BoneWorld(PollenJoint, 246.6f);
        Basis b = (j.Basis.Orthonormalized() * new Basis(Vector3.Forward, Mathf.DegToRad(-90f))).Scaled(Vector3.One * _scale);
        if (_cur == NerveSWait)
            SpawnBurst(PollenWaitFxScene, new Transform3D(b, j.Origin + Vector3.Up * (200f / U)));
        else
            SpawnBurst(PollenAttackFxScene, new Transform3D(b, j.Origin + PolarXZ(_yaw, 200f) / U));
    }

    /// <summary>initFlower: the bud made once (makeOneEnemyAppear) and shown at the ground under the base.</summary>
    private void InitFlower()
    {
        if (_flower == null && FlowerScene != null)
        {
            _flower = FlowerScene.Instantiate<SmsSamboFlower>();
            _flower.StartsShown = false;
            GetParent().AddChild(_flower);
        }
        _flower?.Show(new Vector3(GlobalPosition.X, GroundYBelow() / U, GlobalPosition.Z));   // mGroundHeight: a fresh ray (the cached one is last frame's, none on the first tick)
    }

    private void SetHidden(bool hidden)
    {
        _hidden = hidden;
        if (_model != null) _model.Visible = !hidden;
        SetCollisionLayerValue(1, !hidden);
    }
    /// <summary>The buried state (the end of the sink): the bud up, no collision, sambo_fset held at frame 0, hidden.</summary>
    private void Bury()
    {
        InitFlower();
        SetBck(BckFset); SetBckPaused(true);
        SetHidden(true);
    }

    private float Dist2ToMario() { Vector3 d = MarioPos() - Pos(); return d.LengthSquared(); }
    private bool MarioLow() => MarioPos().Y < 100f + Pos().Y;

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveHide:                                      // TNerveHanaSamboHide
                if (_time == 0)
                {
                    if (_skipSink) { _skipSink = false; Bury(); }
                    else
                    {
                        SetBck(BckFhide);
                        SpawnBurst(HideFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
                    }
                }
                if (_time == 75) InitFlower();
                if (!_hidden && BckEnded()) Bury();
                if (Dist2ToMario() < Prm.AppearDist * Prm.AppearDist) { PushAfterCurrent(NerveAppear); return true; }
                return false;

            case NerveAppear:                                    // TNerveHanaSamboAppear
                if (_time == 0)
                {
                    SetHidden(false);
                    SetBckPaused(false);
                    SetBck(BckFset);
                    SpawnBurst(AppearFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), GlobalPosition));
                    _flower?.Hide();
                }
                if (BckEnded())
                {
                    PushAfterCurrent(NerveSWait);
                    SetWaitAnm();
                    if (MarioLow() && Dist2ToMario() < Prm.AttackDist * Prm.AttackDist) PushAfterCurrent(NerveSAttack);
                    return true;
                }
                WalkToCurPathNode(0f, _turnSpeed * 3f, 0f);
                return false;

            case NerveSWait:                                     // TNerveHanaSamboWait
                if (_time == 0) { SetWaitAnm(); _prevFrame = 0f; }
                {
                    float f = BckFrame();
                    if (_prevFrame < 16f && f >= 16f) CreatePollen();         // getFrameCtrl()->checkPass(16)
                    _prevFrame = f;
                }
                if (_time > Prm.AttackInterval && MarioLow() && Dist2ToMario() < Prm.AttackDist * Prm.AttackDist) { PushAfterCurrent(NerveSAttack); return true; }
                if (Dist2ToMario() > Prm.HideDist * Prm.HideDist) { PushAfterCurrent(NerveHide); return true; }
                WalkToCurPathNode(0f, _turnSpeed, 0f);
                return false;

            case NerveSAttack:                                   // TNerveHanaSamboAttack
                if (_time == 0) SetAttackAnm();
                else if (BckEnded())
                {
                    if (IsBck(BckFattackStart))
                    {
                        CreatePollen();
                        if (AttackSound != null && AttackSound.Stream != null) AttackSound.Play();
                        SetBck(BckFattack);
                    }
                    else if (IsBck(BckFattack))
                    {
                        if (_time > Prm.AttackingTime && !UnsetWet()) SetBck(BckFattackEnd);
                    }
                    else { PushAfterCurrent(NerveSWait); return true; }
                }
                return false;

            case NerveSFreeze:                                   // TNerveHanaSamboFreeze
                if (_time == 0) SetBck(BckFhitStart);
                if (BckEnded())
                {
                    if (IsBck(BckFhitStart)) { if (UnsetWet()) SetBck(BckHit); else return true; }   // DEVIATION: the game holds the start pose until the next drop
                    else if (UnsetWet()) { }                                                        // the hit loop runs on while water keeps coming
                    else return true;
                }
                return false;

            case NerveSDie:                                      // TNerveHanaSamboDie
                if (_time == 0)
                {
                    SetCollisionLayerValue(1, false);
                    SetDeadAnm();
                }
                else if (DeadClipEnded || _time > 300)
                {
                    GenRandomItem();                             // setAfterDeadEffect (the four puffs) + the drop table
                    Dead();
                    return true;
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }

    /// <summary>The four MS_ENM_DISAP_A + _B at the fallen pieces (jIndexTable on samboD), scaled -- in place of the base's one puff at the body.</summary>
    protected override void SetAfterDeadEffect()
    {
        if (_skelDead == null || _modelDead == null || !_modelDead.Visible) { base.SetAfterDeadEffect(); DeathPuffs = 1; return; }
        DeathPuffs = 0;
        foreach (string piece in DeadPieces)
        {
            int b = _skelDead.FindBone(piece);
            Vector3 at = b >= 0 ? (_skelDead.GlobalTransform * _skelDead.GetBoneGlobalPose(b)).Origin : GlobalPosition;
            var fx = SpawnBurst(DeathFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), at));
            if (fx != null) DeathPuffs++;
            if (DebugPrint) GD.Print($"[SMS sambo] death puff at {piece} (bone {b}) {at} -> {fx?.Name} ({fx?.GetType().Name})");
        }
        if (DeathSound != null && DeathSound.Stream != null) DeathSound.Play();
    }

    protected override void Dead()
    {
        base.Dead();
        _skipSink = false;                                // a re-launch after a death plays the game's sink
        if (_modelDead != null) _modelDead.Visible = false;
        if (_deadAnim != null) _deadAnim.Stop();
    }

    /// <summary>calcRootMatrix + moveObject's pin: the body stays on the placed point (mPosition.x / z = unk19C), the dead stack rides the live
    /// model's frame, the head collision sphere follows jnt_head.</summary>
    protected override void ApplyLook()
    {
        Vector3 p = GlobalPosition;
        GlobalPosition = new Vector3(_root.X / U, p.Y, _root.Z / U);
        base.ApplyLook();
        if (_modelDead != null && _model != null) _modelDead.Transform = _model.Transform;
        if (_headShape != null && !_hidden) _headShape.GlobalPosition = HeadPos() / U;
    }

    /// <summary>The level's shine-get kill, a pool's death: the bud goes too.</summary>
    public override void _ExitTree()
    {
        base._ExitTree();
        if (_flower != null && IsInstanceValid(_flower) && !_flower.IsQueuedForDeletion()) _flower.QueueFree();
    }
}
