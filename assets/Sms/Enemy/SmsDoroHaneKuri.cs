using System;
using Godot;

/// <summary>
/// SWIPIN' STU -- TDoroHaneKuri (Enemy/hamukuri.cpp 1557-1645 and the nerves at 2699 / 2724 / 2751; JP ドロハネクリ, the winged cap-thief) on
/// <see cref="SmsHaneHamukuri"/>, with the shipped /enemy/dorohanekuri.prm (pass 125g): body scale 1.5, march 4, turn 2, FlyBaseHeight 500, bob 100 over
/// 200 ticks, FirstVelocityY 20, BoundNum 2, VelocityRate 1.5, 1 HP, no drop odds of its own. The model dorohane.bmd (the winged mesh with the goop
/// Stu's face and green-dotted bottom; wing / eye / nose as the winged one) plays hanekuri's 4 clips (borrowed from Hanekuri.glb, as the game's manager
/// shares the anm data).
///
/// THE THEFT (attackToMario): Mario not wearing his cap -> the attack message and the cry; wearing it -> the message, then onHaveCap: Mario's cap comes
/// OFF (<see cref="Mario.SmsCapStolen"/>: the cap surface hidden, the hair dome on), the disguise -- Mario's cap + a moustache (dorokuriHige.bmd,
/// TDoroHige, a TSharedParts on joint 7 = 'body' in the bmd's depth-first joint order -- the hige model's vertices are in that joint's frame; the Dorokuri's
/// is joint 5, its 'body' too) -- shows on the thief, PARTICLE_MS_TLS_CHANGE at joint 5 = 'null_cap' (unk1AC; <see cref="StealFxScene"/> slot), and TNerveDoroHaneRise:
/// it climbs toward 800 u over the ground (faster each tick, up to 5 u), x/z easing back from a squash, turning to Mario at 2 deg a tick -- and stays
/// up there WHILE IT HOLDS THE CAP (the nerve ends only when the cap is gone). WATER (behaveToWater): the dive flag set + TNerveDoroHaneHitWater for 300
/// ticks: the rise drops 4 u a tick while it is over 50 u up (the bind's gravity brings the body down to ~40 u), spinning 10 deg a tick, the clips at
/// 1.5x, TWalkerEnemy's slow wander toward where he stood -- its window on the ground; then the flight math takes it back (the dive plunges it to the
/// ground, UpWait, and Rise again if it still holds). STOMP: Die -> setBehavior's releaseCap: the cap PUT on the ground under it as a cap item
/// (<see cref="CapItemScene"/>: Mario walks into it and it is back on his head); the disguise off; its DropTable (DoroHaneDrops: the game's unk18C = 5
/// event coins) at the death. The cap also comes back if the thief is killed any other way. isCollidMove: a hide object (a hidden-coin spot) is
/// ignored; a map object while attacking throws it back (not ported: it has no map-object category here).
/// NOT PORTED (logged): the capless Mario's sun damage, the sounds (slots), TDoroHamuKuri's RobCap exchange between ground Stus (the ground goop Stu
/// here keeps the plain hit).
/// </summary>
[GlobalClass]
public partial class SmsDoroHaneKuri : SmsHaneHamukuri
{
    // ------------------------------------------------------------------------------------------------------------------ dorohanekuri.prm
    public static readonly HaneParams DoroHanePrm = new HaneParams
    {
        HeadHeight = 200f, BodyRadius = 5f, WallRadius = 100f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 10, ActiveEnemyNum = 10,
        JumpForce = 5f, SearchLength = 1000f, SearchHeight = 1200f, SearchAware = 10f, SearchAngle = 180f, GiveUpLength = 2000f, GiveUpHeight = 1500f,
        AttackWait = 0, FreezeWait = 300, DamageRadius = 120f, DamageHeight = 100f, AttackRadius = 100f, AttackHeight = 60f,
        TurnSpeedLow = 2f, TurnSpeedHigh = 2f, BodyScaleLow = 1.5f, BodyScaleHigh = 1.5f, GenItemRate = 0f, GenEggRate = 0f, WaitTime = 100,
        StampRange = 0f, ZigzagCycle = 200f, ZigzagAngle = 5f, MarchSpeedLow = 4f, MarchSpeedHigh = 4f,
        WaterCoeff = 30f, WaterAttackCoeff = 30f, FirstVelocityY = 20f, VelocityRate = 1.5f, FirstKickVelocityY = 2f, CanSearchDist = 800f,
        BoundNum = 2, SearchActorTimer = 500, JitabataTimer = 200, FlyTimer = 500, TrampleBonusNum = 3, CrashBonusNum = 3, SerialCrashFrame = 15, KyoroTimer = 500,
        NormalJumpVy = 15f, AttackJumpVy = 10f, FlyBaseHeight = 500f, FlyBaseAmplitude = 100f, FlyBaseFrequency = 200f,
    };
    protected override HamukuriParams HPrm => DoroHanePrm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>The scene whose AnimationPlayer holds hanekuri's 4 clips: models/Eniemes/Hanekuri/Hanekuri.glb unless another is set.</summary>
    [Export] public PackedScene ClipSource;
    public const string ClipSourcePath = "res://models/Eniemes/Hanekuri/Hanekuri.glb";
    /// <summary>TDoroHige: Mario's cap + the moustache (dorokuriHige.bmd) on joint 7 'body' (createHige's TDoroHige(.., 7, ..); the exporter names the
    /// bone body_3 here -- matched without the suffix), shown while it holds his cap. Pass 125h: it sat on null_cap, a joint with its own 1.5 scale
    /// and a 150 deg turn -- the cap came out big and tilted.</summary>
    [Export] public PackedScene DisguiseScene;
    public const string DisguiseScenePath = "res://models/Eniemes/Dorokuri/DorokuriHige.glb";
    public const string DisguiseBone = "body";
    /// <summary>unk1AC = 5 'null_cap': where the TLS_CHANGE puff fires as the cap is taken.</summary>
    public const string StealBone = "null_cap";
    public string DisguiseBoneName { get; private set; } = "";
    /// <summary>The cap on the ground after the thief's death (releaseCap -> the TMarioCap item): assets/Sms/Items/MarioCap.tscn unless another is set.</summary>
    [Export] public PackedScene CapItemScene;
    public const string CapItemScenePath = "res://assets/Sms/Items/MarioCap.tscn";
    /// <summary>PARTICLE_MS_TLS_CHANGE at the joint when the cap is taken: a one-shot scene, or nothing.</summary>
    [Export] public PackedScene StealFxScene;
    /// <summary>Steal the cap at all (off = the plain winged hit; the game always steals).</summary>
    [Export] public bool StealsCap = true;

    private Node3D _disguise;
    private bool _holding;                                       // unk198
    private const int NerveRise = NerveSpecies + 12, NerveHitWater = NerveSpecies + 13;
    protected override string NerveName(int n) => n switch { NerveRise => "Rise", NerveHitWater => "HitWater", _ => base.NerveName(n) };

    public bool HoldingCap => _holding;
    public Node3D DisguiseNode => _disguise;
    public new string DebugState => $"{base.DebugState} holding {_holding}";

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        base.Init();
        PackedScene src = ClipSource ?? GD.Load<PackedScene>(ClipSourcePath);
        if (src == null) GD.PushWarning($"[SMS dorohane] {GetPath()}: {ClipSourcePath} did not load: no clips");
        else BorrowClips(src, "dorohane");
        if (_model == null) return;
        Skeleton3D skel = null;
        foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { skel = n as Skeleton3D; if (skel != null) break; }
        PackedScene dis = DisguiseScene ?? GD.Load<PackedScene>(DisguiseScenePath);
        string bone = skel != null ? MatchBone(skel, DisguiseBone) : DisguiseBone;
        if (skel != null && skel.FindBone(bone) >= 0 && dis != null)
        {
            DisguiseBoneName = bone;
            var att = new BoneAttachment3D { Name = "DisguiseAttach" };
            skel.AddChild(att); att.BoneName = bone;
            _disguise = dis.Instantiate<Node3D>(); _disguise.Name = "Disguise";
            att.AddChild(_disguise);
            _disguise.Visible = false;
        }
        CapItemScene ??= GD.Load<PackedScene>(CapItemScenePath);
    }
    /// <summary>TDoroHaneKuri::reset: unk198 = 0 -- the cap comes back to Mario if a re-launch catches a thief still holding it.</summary>
    protected override void Reset()
    {
        if (_holding) ReleaseCap(false);
        base.Reset();
    }

    // ------------------------------------------------------------------------------------------------------------------ the cap
    /// <summary>TDoroHaneKuri::attackToMario: the message (+ the cry); Mario wearing his cap -> Rise, onHaveCap, the TLS_CHANGE flash.</summary>
    protected override void AttackToMario()
    {
        bool wearing = StealsCap && _mario != null && _mario.SmsWearingCap && !_holding;
        if (AttackVoice != null && AttackVoice.Stream != null) AttackVoice.Play();
        SendAttackMsgToMario();
        if (!wearing) return;
        PushNerve(NerveRise);
        _holding = true;
        _mario.SmsCapStolen(true);
        if (_disguise != null) _disguise.Visible = true;
        Vector3 at = GlobalPosition + Vector3.Up * (100f * _bodyScale / U);
        if (_disguise != null && _disguise.GetParent()?.GetParent() is Skeleton3D sk && sk.FindBone(StealBone) is int sb && sb >= 0)
            at = (sk.GlobalTransform * sk.GetBoneGlobalPose(sb)).Origin;                 // emitAndBindToPosPtr at the null_cap joint's point
        SpawnBurst(StealFxScene, new Transform3D(Basis.FromScale(Vector3.One * _bodyScale), at));
        if (DebugPrint) GD.Print($"[SMS dorohane] {Name}: took his cap");
    }
    /// <summary>THamuKuri::releaseCap: the held cap PUT on the ground under the body -- the cap item there (Mario picks it up), or straight back on his head.</summary>
    private void ReleaseCap(bool asItem)
    {
        if (!_holding) return;
        _holding = false;
        if (_disguise != null) _disguise.Visible = false;
        if (asItem && CapItemScene != null && _mario != null && IsInstanceValid(_mario))
        {
            var cap = CapItemScene.Instantiate<Node3D>();
            GetParent().AddChild(cap);
            cap.GlobalPosition = new Vector3(GlobalPosition.X, _groundY / U, GlobalPosition.Z);
            LastCapItem = cap;
            if (DebugPrint) GD.Print($"[SMS dorohane] {Name}: the cap put down at {cap.GlobalPosition}");
        }
        else if (_mario != null && IsInstanceValid(_mario)) _mario.SmsCapStolen(false);
    }
    /// <summary>For the drives: the cap item the last death put down.</summary>
    public Node3D LastCapItem { get; private set; }

    /// <summary>TDoroHaneKuri::setBehavior: dying -> releaseCap; then the winged bind.</summary>
    protected override void SetBehavior()
    {
        if (IsNerve(NerveDie) && _holding) ReleaseCap(true);
        base.SetBehavior();
    }
    protected override void Dead()
    {
        if (_holding) ReleaseCap(true);                          // a death without the Die nerve (the shine demo): the cap still comes down
        base.Dead();
    }
    /// <summary>TDoroHaneKuri::behaveToWater: the dive flag, and HitWater unless already in it.</summary>
    protected override void BehaveToWater()
    {
        _dive = true;
        if (!IsNerve(NerveHitWater)) PushNerve(NerveHitWater);
    }
    /// <summary>TDoroHaneKuri::isCollidMove: a hide object (the hidden-coin spots) is ignored; else TSmallEnemy's.</summary>
    protected override bool IsCollidMove(Node3D other) => other is not SmsHiddenCoin && base.IsCollidMove(other);

    // ------------------------------------------------------------------------------------------------------------------ the species' nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveRise:                                      // TNerveDoroHaneRise
                if (Pos().Y < _groundY + 800f) _flyRise += Mathf.Clamp(_time * 0.01f, 0.01f, 5f);
                _scaleXZ = Mathf.Clamp(_scaleXZ * 0.9f, _bodyScale, _bodyScale * 2f);
                _scaleY = Mathf.Clamp(_scaleY * 1.3f, 0f, _bodyScale);
                if (!_holding) { PushAfterCurrent(NerveGraphWander); return true; }
                GlobalPosition = new Vector3(GlobalPosition.X, (_flyBase + _flyRise) / U, GlobalPosition.Z);
                WalkToCurPathNode(0f, 2f, 0f);
                return false;

            case NerveHitWater:                                  // TNerveDoroHaneHitWater
                if (_time == 0) { SetGoalPoint(MarioPos()); if (_anim != null) _anim.SpeedScale *= 1.5f; }
                if (Pos().Y > _groundY + 50f) _flyRise -= 4f;
                _flyBob = 0f; _flyAdjust = 0f; _flyPhase = 0f;
                _flyBase = _groundY;
                if (_time > 300) { if (_anim != null) _anim.SpeedScale /= 1.5f; return true; }
                _yaw += 10f;
                if (!IsBck(BckKyoro)) WalkBehaviorBase(0, 0.5f);   // TWalkerEnemy::walkBehavior: the plain walk, no flight math
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
