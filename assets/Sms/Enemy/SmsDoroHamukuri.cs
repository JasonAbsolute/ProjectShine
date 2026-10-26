using System;
using Godot;

/// <summary>
/// The GOOP STU -- TDoroHamuKuri (Enemy/hamukuri.cpp 2314-2440; JP ドロハムクリ, "mud Stu": the dark one with the painted face) on
/// <see cref="SmsHamukuri"/>, with the shipped /enemy/dorohamukuri.prm (pass 125f, feature/enemy-base). The game's class is the Stu with a different
/// model, different numbers and the CAP ROBBING on top: its attackToMario is selectCapHolder (the attack message, then -- Mario wearing his cap -- the
/// cap appears as an item and a pooled Stu becomes its holder), the others run at the holder (TNerveDoroHamuKuriRobCap) and take the cap off it by
/// jumping over it (isCollidMove's pass), the holder flies to a graph node, a stomped holder puts the cap down (releaseCap). That needs Mario's cap as
/// an item and a capless Mario, so it is NOT PORTED here (logged): this kind attacks like the Stu but WITHOUT the hop and the PostAttack wait
/// (TDoroHamuKuri::attackToMario never calls THamuKuri's), keeps the Stu's kick of a flailing one (with the rumble), and wears the disguise the game
/// gives a cap holder -- dorokuriHige.bmd (TDoroHige, TSharedParts on joint 5 'body': Mario's cap + the moustache) -- only when <see cref="Disguise"/> is
/// set, for his eye. Its .prm: 1 HP, jump 6, turn 5..8 deg a tick, march 1..2, attack 60 x 60, damage 100 x 90, 180 deg of sight, give-up 1000 / 1100,
/// AttackWait 100, GenItemRate 50 + 10 (the DropTable decides here), BoundNum 2, VelocityRate 0.8, FirstVelocityY 10, JitabataTimer 200.
///
/// THE MODEL: dorokuri_model.bmd (his rip, models/Eniemes/Dorokuri/Dorokuri.glb) has the Stu's skeleton (root_2 / center / jnt_head / null_cap / nose
/// / body / jnt_footR / Rfoot / jnt_footL / Lfoot) and NO clips of its own -- the game plays /scene/hamukurianm's 15 bcks on it (createMActor(..., 3)
/// with THamuKuriManager's anm data). Here <see cref="Init"/> borrows the Stu's AnimationPlayer library from <see cref="ClipSource"/> (Hamukuri.glb)
/// after renaming the root bone root_2 -> root, so the bck table is the Stu's, slot for slot. The materials are the bmd's TEV (bmd_mat_dump.py):
/// feet GX_TEVREG0 (10, 10, 350) on K_doro_dot; the bottom the two-register dot mask (230, 230, 230) over (10, 90, 10) GREEN; the top the two-register
/// FACE mask -- K_dorokuri_face (32 x 64) mixing (10, 90, 10) green and (250, 133, 12) ORANGE, two-sided; the nose (500, 350, 225) on K_doro_dot; the
/// eye the Stu's pupil decal. GenerateOnlyDead 0.
/// </summary>
[GlobalClass]
public partial class SmsDoroHamukuri : SmsHamukuri
{
    // ------------------------------------------------------------------------------------------------------------------ dorohamukuri.prm
    /// <summary>The shipped /enemy/dorohamukuri.prm (params.szs on his disc): the Stu's numbers where the file does not differ.</summary>
    public static readonly HamukuriParams DoroPrm = new HamukuriParams
    {
        HeadHeight = 120f, BodyRadius = 6f, WallRadius = 50f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 100, ActiveEnemyNum = 3,
        JumpForce = 6f, SearchLength = 800f, SearchHeight = 1000f, SearchAware = 10f, SearchAngle = 180f, GiveUpLength = 1000f, GiveUpHeight = 1100f,
        AttackWait = 100, FreezeWait = 300, DamageRadius = 100f, DamageHeight = 90f, AttackRadius = 60f, AttackHeight = 60f,
        TurnSpeedLow = 5f, TurnSpeedHigh = 8f, BodyScaleLow = 1f, BodyScaleHigh = 1f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 15f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 1f, MarchSpeedHigh = 2f,
        WaterCoeff = 30f, WaterAttackCoeff = 30f, FirstVelocityY = 10f, VelocityRate = 0.8f, FirstKickVelocityY = 2f, CanSearchDist = 800f,
        BoundNum = 2, SearchActorTimer = 500, JitabataTimer = 200, FlyTimer = 500, TrampleBonusNum = 3, CrashBonusNum = 3, SerialCrashFrame = 15, KyoroTimer = 500,
    };
    protected override HamukuriParams HPrm => DoroPrm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>The scene whose AnimationPlayer holds the Stu's 15 clips (THamuKuriManager's hamukurianm): models/Eniemes/Hamukuri/Hamukuri.glb unless another is set.</summary>
    [Export] public PackedScene ClipSource;
    public const string ClipSourcePath = "res://models/Eniemes/Hamukuri/Hamukuri.glb";
    /// <summary>TDoroHige: Mario's cap + the moustache (dorokuriHige.bmd) on joint 5 'body' -- the game shows it on the Stu holding his cap. The cap robbing is not
    /// ported, so the parts show only with <see cref="Disguise"/> on.</summary>
    [Export] public PackedScene DisguiseScene;
    public const string DisguiseScenePath = "res://models/Eniemes/Dorokuri/DorokuriHige.glb";
    public const string DisguiseBone = "body";
    [Export] public bool Disguise;

    private Node3D _disguise;
    public Node3D DisguiseNode => _disguise;

    // ------------------------------------------------------------------------------------------------------------------ setup
    /// <summary>TDoroHamuKuri::init: THamuKuri::init (unk150 17), the model's clips borrowed from the Stu's glb, the disguise parts on the body joint.</summary>
    protected override void Init()
    {
        base.Init();
        BorrowClips();
        AttachDisguise();
    }

    /// <summary>The Stu's clips onto this model (the base's BorrowClips: the root track lands on root_2).</summary>
    private void BorrowClips()
    {
        PackedScene src = ClipSource ?? GD.Load<PackedScene>(ClipSourcePath);
        if (src == null) { GD.PushWarning($"[SMS dorokuri] {GetPath()}: {ClipSourcePath} did not load: no clips"); return; }
        BorrowClips(src, "dorokuri");
    }

    private void AttachDisguise()
    {
        if (_model == null) return;
        var skel = FindSkeleton(_model);
        if (skel == null || skel.FindBone(DisguiseBone) < 0) return;
        PackedScene scene = DisguiseScene ?? GD.Load<PackedScene>(DisguiseScenePath);
        if (scene == null) return;
        var att = new BoneAttachment3D { Name = "DisguiseAttach" };
        skel.AddChild(att);
        att.BoneName = DisguiseBone;
        _disguise = scene.Instantiate<Node3D>();
        _disguise.Name = "Disguise";
        att.AddChild(_disguise);
        _disguise.Visible = Disguise;
    }

    private static Skeleton3D FindSkeleton(Node n)
    {
        if (n is Skeleton3D s) return s;
        foreach (Node c in n.GetChildren()) { var r = FindSkeleton(c); if (r != null) return r; }
        return null;
    }

    protected override void ApplyLook()
    {
        base.ApplyLook();
        if (_disguise != null && _disguise.Visible != Disguise) _disguise.Visible = Disguise;
    }

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    /// <summary>TDoroHamuKuri::attackToMario: a flailing one he touches below his feet is kicked (forceRoll + the rumble); else selectCapHolder = the attack
    /// message (the cap part not ported) -- NO hop and no PostAttack: it keeps coming.</summary>
    protected override void AttackToMario()
    {
        if (IsNerve(NerveJitabata))
        {
            if (Pos().Y + 10f > MarioPos().Y) ForceRoll(MarioPos(), false);
            return;
        }
        SendAttackMsgToMario();
    }

    public new string DebugState => $"{base.DebugState} disguise {(_disguise != null ? _disguise.Visible.ToString() : "-")}";
}
