using System;
using Godot;

/// <summary>
/// SLEEPY BOO -- TDebuTelesa (Enemy/DebuTelesa.cpp; JP デブテルサ "the fat Boo", the debutelesa archive) on <see cref="SmsSmallEnemy"/> (SMS pass 152,
/// feature/enemy-sirena). Hotel Delfino's huge Boo asleep across a doorway: it hangs where it was placed snoring (the Zs off its hand, a string of
/// drool), hurts Mario if he walks into it and shoves him back out, and NOTHING of Mario's moves it -- no jump, no ground pound, no punch, and the
/// hose does nothing to it. In the game only YOSHI can clear it: he eats it (HIT_MESSAGE_UNKB, the EATEN sound). Yoshi's eating is not in this port,
/// so a Sleepy Boo here can also be beaten Jaysonn's way (<see cref="BeatenBy"/>, ON by default): the hose WAKES it (the Zs stop, it stirs) and a
/// ground pound on the woken one beats it; left alone it dozes off again.
///
/// THE GAME. TDebuTelesa is a TSmallEnemy (ACTOR_TYPE_DEBU_TELESA) with LIVE_FLAG_UNK10 -- no bind: it never moves, falls or turns. The manager
/// loads /enemy/debuTelesa.prm (not shipped: TSmallEnemyParams' defaults) and then forces the hits -- attack 240 x 330, damage 220 x 300, body
/// scale 1 -- and a 350-u near clip. init: WAIT, the hit actor (ACTOR_TYPE_DEBU_TELESA, HIT_CATEGORY_PLAYER, 10 x 10), the joints null_yodare and
/// jnt_Rhand. The one nerve, WAIT: debutelesa_wait (94 frames, loop) and its bas. receiveMessage: TRAMPLE / HIP_DROP / PUNCH -> false (nothing
/// happens); UNKB (Yoshi's eat) -> MSD_SE_EN_DB_TELSA_EATEN, then TSmallEnemy's (isHitValid -> kill -> DIE, setDeadAnm = the clip from frame 1 -- there
/// is no death clip). behaveToWater: nothing (the base's decHpByWater still takes the hit point, to no effect). attackToMario: sendAttackMsgToMario (the
/// touch hurts). isCollidMove: false (nothing pushes it). doKeepDistance: true (Mario is pushed out of its damage cylinder). emitEffects (every frame,
/// not held, not dying): POI_ZZZ (the Zs, x 1.5) at jnt_Rhand, TLS_YODARE_L (the drool, x 2.3) at null_yodare. The manager's clipEnemies: the view
/// frustum at a 350 near / FarClip far.
///
/// THE PORT. The base with no bind (<c>_noBind</c>, FlyBind: the body never steps or falls), one nerve (DebuWait) and no walker; its own HitCheckMario
/// (no jump or pound finds it unless woken under <see cref="BeatenBy"/>; the hose; the touch attack; doKeepDistance's shove); SmsSprayedByWater
/// wakes it (the deviation) or does nothing (the game). The Zs are the PoiZzz scene bound to the right hand (jnt_Rhand), the drool a slot bound to
/// null_yodare. Beaten: the base's DIE with no clip -- it shrinks away over the death's ticks (the game has no clip either: Yoshi swallows it),
/// the puffs and the drops. Knobs (grouped, plain English): Deviations (BeatenBy, AwakeSeconds), Effects (the Zs, the drool slot), Sounds (two slots).
/// </summary>
public partial class SmsDebuTelesa : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ debuTelesa.prm (not shipped) + the manager's forced hits
    /// <summary>TSmallEnemyParams' PARAM_INIT defaults with TDebuTelesaManager::load's attack 240 x 330, damage 220 x 300, body scale 1.</summary>
    public static readonly Params Prm = new Params
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 80f, ClipRadius = 100f, HitPointMax = 1, InstanceNum = 6, ActiveEnemyNum = 6,
        AttackRadius = 240f, AttackHeight = 330f, DamageRadius = 220f, DamageHeight = 300f, BodyScaleLow = 1f, BodyScaleHigh = 1f,
        TurnSpeedLow = 0f, TurnSpeedHigh = 0f, MarchSpeedLow = 0f, MarchSpeedHigh = 0f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    public enum Beaten { YoshiOnly, SprayThenGroundPound }
    [ExportGroup("Deviations (the game = YoshiOnly)")]
    /// <summary>YoshiOnly = the game: nothing Mario does moves it (Yoshi eats it, and Yoshi's eating is not in this port -- a wall for good).
    /// SprayThenGroundPound (the default here): the hose wakes it for AwakeSeconds (the Zs stop, it stirs), and a ground pound on the woken one beats it.</summary>
    [Export] public Beaten BeatenBy = Beaten.SprayThenGroundPound;
    /// <summary>How long a hosed one stays awake (and poundable) before it dozes off again.</summary>
    [Export(PropertyHint.Range, "1,60,0.5")] public float AwakeSeconds = 8f;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_poi_zzz: the Zs off its right hand while it sleeps (a looping scene bound to the hand). Empty = assets/Sms/Enemy/Fx131/PoiZzz.tscn.</summary>
    [Export] public PackedScene SleepFxScene;
    /// <summary>ms_tls_yodare_l: the drool from its mouth (null_yodare; a looping scene). Slot.</summary>
    [Export] public PackedScene DroolFxScene;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_EN_DB_TELSA_EATEN (the beat), the bas table's wait (the snore).</summary>
    [Export] public AudioStreamPlayer3D EatenSound, SnoreSound;

    // ------------------------------------------------------------------------------------------------------------------ the clip (the archive's one bck)
    private static readonly string[] Names = { "debutelesa_wait" };
    private static readonly int[] Frames = { 94 };
    private static readonly bool[] Loops = { true };
    protected const int BckWait = 0;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveDWait = NerveSpecies;
    protected override string NerveName(int n) => n == NerveDWait ? "DebuWait" : base.NerveName(n);
    protected override int DefaultNerve => NerveDWait;

    // ------------------------------------------------------------------------------------------------------------------ state
    private Skeleton3D _skel; private int _handBone = -1, _droolBone = -1;
    private Node3D _zzz, _drool;
    private int _awakeTicks;                                    // > 0: woken by the hose (the deviation)
    private float _stir;
    /// <summary>For the drives.</summary>
    public bool IsAwake => _awakeTicks > 0;
    public bool HasZzz => _zzz != null && IsInstanceValid(_zzz) && _zzz.Visible;
    public bool IsDying => IsNerve(NerveDie);
    public new string DebugState => $"{NerveNameNow} t{NerveTime} bck {BckName} awake {_awakeTicks} zzz {HasZzz} nobind {_noBind} hp {_hp} dead {_dead} y {Pos().Y:F0}";

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    protected override bool FlyBind => true;                    // LIVE_FLAG_UNK10: no bind at all -- the species keeps the body where it was placed

    protected override void Init()
    {
        SleepFxScene ??= LoadIf("res://assets/Sms/Enemy/Fx131/PoiZzz.tscn");
        if (_model != null)
        {
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null) { _handBone = _skel.FindBone("jnt_Rhand"); _droolBone = _skel.FindBone("null_yodare"); }
        }
    }
    private static PackedScene LoadIf(string path) => ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;

    protected override void Reset()
    {
        base.Reset();
        _noBind = true;
        MotionMode = MotionModeEnum.Floating;
        _vel = Vector3.Zero; _airborne = false; _awakeTicks = 0; _stir = 0f;
        _yaw = _spawnYaw;
        ApplyLook();
    }
    protected override void MoveObjectPre() { _noBind = true; _lin = Vector3.Zero; }
    protected override bool IsFindMario(float k) => false;
    protected override bool IsCollidMove(Node3D other) => false;   // TDebuTelesa::isCollidMove: nothing pushes it
    protected override void LaunchVelocity() { }
    protected override float GravityY() => 0f;
    protected override bool WallHopAllowed() => false;

    protected override void SetGenerateAnm() => SetBck(BckWait);
    protected override void SetWaitAnm() => SetBck(BckWait);
    protected override void SetWalkAnm() => SetBck(BckWait);
    protected override void SetRunAnm() => SetBck(BckWait);
    protected override void SetFreezeAnm() => SetBck(BckWait);
    /// <summary>TDebuTelesa::setDeadAnm: the one clip from frame 1 -- there is no death clip (Yoshi swallows it); the port shrinks it away in the Die nerve.</summary>
    protected override void SetDeadAnm() { SetBck(BckWait); if (EatenSound != null && EatenSound.Stream != null) EatenSound.Play(); }
    protected override void SetMeltAnm() => SetBck(BckWait);

    /// <summary>TDebuTelesa::receiveMessage: TRAMPLE / HIP_DROP / PUNCH -> false; only UNKB (Yoshi) kills -- or, under the deviation, a ground pound on a woken one.</summary>
    protected override bool IsHitValid(HitMessage msg) => BeatenBy == Beaten.SprayThenGroundPound && _awakeTicks > 0 && msg == HitMessage.HipDrop;
    /// <summary>attackToMario: sendAttackMsgToMario -- the touch hurts (not a woken one being pounded).</summary>
    protected override void AttackToMario() => SendAttackMsgToMario();

    /// <summary>The base's lists plus doKeepDistance: Mario is pushed out of its damage cylinder.</summary>
    protected override void HitCheckMario()
    {
        base.HitCheckMario();
        if (_mario == null || _dead || IsNerve(NerveDie)) return;
        Vector3 g = Pos(), m = MarioPos();
        if (CheckDistance(m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight, g, P.DamageRadius * _bodyScale, P.DamageHeight * _bodyScale))
            _mario.SmsKeepDistance(g, P.DamageRadius * _bodyScale);
    }
    /// <summary>HIT_MESSAGE_SPRAYED_BY_WATER: the mark; the game's behaveToWater is NOTHING (decHpByWater takes the point to no effect) -- under the deviation the hose wakes it.</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (_sprayCooldown != 0) return true;
        _sprayCooldown = 1;
        if (BeatenBy == Beaten.SprayThenGroundPound && !IsNerve(NerveDie)) _awakeTicks = (int)(AwakeSeconds * 120f);
        return true;
    }
    protected override void BehaveToWater() { }

    /// <summary>emitEffects (every frame, not held, not dying): the Zs at the right hand, the drool at null_yodare -- here bound scenes shown while asleep;
    /// a woken one stirs (a small squash and stretch) until its awake ticks run out.</summary>
    protected override void SetBehavior()
    {
        if (_dead) return;
        _airborne = false; _vel = Vector3.Zero;
        bool dying = IsNerve(NerveDie);
        bool asleep = !dying && _awakeTicks == 0;
        if (_awakeTicks > 0) { _awakeTicks--; _stir += 1f; }
        Bound(ref _zzz, SleepFxScene, asleep, _handBone, Vector3.Up * 230f, 1.5f);
        Bound(ref _drool, DroolFxScene, asleep, _droolBone, Vector3.Up * 160f + PolarXZ(_yaw, 120f), 2.3f);
        if (SnoreSound != null && SnoreSound.Stream != null && asleep && !SnoreSound.Playing) SnoreSound.Play();
    }
    private void Bound(ref Node3D slot, PackedScene scene, bool on, int bone, Vector3 fallbackU, float scale)
    {
        if (on && scene != null)
        {
            if (slot == null || !IsInstanceValid(slot)) { slot = scene.Instantiate<Node3D>(); slot.TopLevel = true; AddChild(slot); slot.Scale = Vector3.One * _scale * scale; }
            Vector3 at = _skel != null && bone >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(bone).Origin : (Pos() + fallbackU) / U;
            slot.GlobalPosition = at;
            slot.Visible = true;
        }
        else if (slot != null && IsInstanceValid(slot)) slot.Visible = false;
    }
    /// <summary>The body where it was placed, facing its yaw; a woken one stirs; a dying one shrinks away (no death clip: the port's stand-in for the swallow).</summary>
    protected override Vector3 LookScale
    {
        get
        {
            float s = _scale;
            if (IsNerve(NerveDie)) s *= MathF.Max(0.02f, 1f - NerveTime / 40f);
            else if (_awakeTicks > 0) s *= 1f + 0.04f * MathF.Sin(_stir * 0.35f);
            return new Vector3(s, s, s);
        }
    }

    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveDWait:                                     // TNerveDebuTelesaWait: the wait clip, for ever
                if (_time == 0) SetBck(BckWait);
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
