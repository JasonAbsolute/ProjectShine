using Godot;
using System;

/// <summary>
/// SMS pass 130 -- the Bob-omb, TBombHei (doldecomp/sms src/Enemy/bombhei.cpp, include/Enemy/BombHei.hpp; "nejibomb" = the wind-up bomb):
/// a TWalkerEnemy that marches at Mario and blows up when its fuse runs out; water douses it (it sits, dizzy) until the fuse runs out anyway.
///
/// THE GAME (per tick, 120 Hz; bombhei.prm: march 2 u/tick = 3.2 m/s, turn 4, scale 1 (130 u = 1.75 m), damage 70 x 150, attack 60 x 140,
/// BombTime 1000 ticks, BombRange 200, ThrownVY 22 / RateXZ 3 / GravityY 0.3):
/// - Generate (the default): nejibomb_model1.bmd, nejibomb_fling1 (the falling pose, looping), the btp nejibomb_stop1 held at frame 0 (the
///   normal face); landed -> nejibomb_land1 (44); at its end -> Attack. (Held by Mario the clip freezes; dropped in the air -> fling again.)
/// - Attack: nejibomb_run1 (the walk), the goal Mario, walkBehavior(2, 1.0) for ever (no give-up, no sight test) -- MSD_SE_EN_BOMBHEI_ZENMAI
///   (the wind-up) every tick of the walk. A walking Bob-omb does NOT hurt on touch: attackToMario sends nothing outside the explosion (and
///   isHitValid takes nothing but Yoshi's eat: a stomp does nothing either).
/// - The fuse (moveObject, every tick outside Thrown / Explosion / Generate): past BombTime (8.3 s) -> the doused flag off and, unless already
///   in WaitExplosion, WalkExplosion pushed: nejibomb_run_down1 (119) with the btp nejibomb_count1 (the LED display counts 3 / 2 / 1, 40
///   frames each), walking at 0.6 x, MSD_SE_EN_BOMBHEI_COUNT every 40 btp frames, PARTICLE_MS_BOMB_LIMIT bound to the body joint; at the
///   clip's end -> Explosion.
/// - Water (behaveToWater while walking or landing, with the hit points gone -- decHpByWater): WaitExplosion, cooldown 20: nejibomb_stop_down1
///   (179), doused: the clip freezes at frame 60 (it sits), the btp stop1 runs to frame 1 at frame 10 (the dizzy blue face). The fuse keeps
///   counting; when it runs out the doused flag drops and the clip runs on with the count btp (3 / 2 / 1 again), and at its end past 150 ticks
///   -> Explosion.
/// - Explosion: the model swapped for downnejibomb_model1.bmd (the pieces) at a random yaw, downnejibomb_down1 (80), the camera shake and the
///   rumble; the attack cylinder grows x 1.2 a tick until BombRange x scale / AttackRadius (200 / 60 = 3.33: a 200 x 467 u blast); Mario in it
///   -> HIT_MESSAGE_BURN (dmgfire: 1 HP + FIRE_DOWN); frame 2 of the clip spawns TEffectExplosion (the blast) at the body's scale; at the
///   clip's end dead (no drops: setAfterDeadEffect is empty, genRandomItem never runs; a coin only for a bomb Mario carried). Another Bob-omb
///   touching an exploding one explodes too (isCollidMove, mSerialBomb). Water surface / sand columns, death planes (forceKill): not here.
/// - PickUp / Thrown (pass 130b): Mario's touch takes a DOUSED Bob-omb (TMario::hitPickUpEnemy, MarioCheckCol.cpp 211: unk164 set and he is not JUMPING;
///   B held, isTakeSituation) -- HIT_MESSAGE_TAKE: the hit filter NO_COLLISION, mHolder = Mario, behaveToTaken pushes PickUp (unk1A4 = 1: a coin at
///   the end) and the body rides his taking matrix with the sit clip frozen (calcRootMatrix). The fuse keeps counting in his hands: run out -> the
///   doused flag drops and WalkExplosion is pushed OVER PickUp (the legs run and the LED counts in his hands) -> Explosion there: sendAttackMsgToMario
///   (the common ATTACK, not BURN) and the end of the blast empties his hands (mHolder = nullptr). B (isThrowStart: an enemy is always PITCHED) ->
///   HIT_MESSAGE_THROWN -> behaveToRelease (doused or counting; never from the Explosion) pushes Thrown: velocity = (ThrownRateXZ x his mThrowPower 2.8
///   x sin / cos of his angle, ThrownVY) = (8.4, 22, 8.4) u/tick, mPosition.y += 2, AIRBORNE, ThrownGravityY 0.3 a tick (getGravityY); landed ->
///   genEventCoin (a coin toward Mario at 20 u/tick, 20 up -- the manager allows 20 a stage, canMakeDeadCoin) and Explosion. A walking or counting
///   Bob-omb cannot be picked up; a doused one only on the ground.
/// DEVIATIONS (logged): the particles and sounds are slots (empty = none); the camera shake is the port's Damage shake (the game's mode 6 is not ported);
/// the blast's visual <see cref="ExplosionFxScene"/> defaults to the disc's TEffectExplosion port (BombExplosion.tscn: the 10_bomb fireball with its
/// bck / brk / btk and the four jpa emitters) at the body's scale x <see cref="BlastScale"/> (the game scales only the hit cylinder by BombRange);
/// the fuse spark <see cref="FuseFxScene"/> defaults to ms_bomb_limit's port (BombFuse.tscn).
/// </summary>
[GlobalClass]
public partial class SmsBombHei : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ bombhei.prm
    public class BombParams : Params
    {
        public int BombTime = 1000;
        public float BombRange = 300f, ThrownVY = 50f, ThrownRateXZ = 0.5f, ThrownGravityY = 1.5f, ShootVelocity = 12f;
    }
    /// <summary>The shipped /enemy/bombhei.prm (params.szs on his disc, read with prm_file.py).</summary>
    public static readonly BombParams Prm = new BombParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 5, ActiveEnemyNum = 5,
        JumpForce = 10f, SearchLength = 800f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 2000f, GiveUpHeight = 650f,
        AttackWait = 120, FreezeWait = 300, DamageRadius = 70f, DamageHeight = 150f, AttackRadius = 60f, AttackHeight = 140f,
        TurnSpeedLow = 4f, TurnSpeedHigh = 4f, BodyScaleLow = 1f, BodyScaleHigh = 1f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 5f, MarchSpeedLow = 2f, MarchSpeedHigh = 2f,
        BombTime = 1000, BombRange = 200f, ThrownVY = 22f, ThrownRateXZ = 3f, ThrownGravityY = 0.3f, ShootVelocity = 8f,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (off = the game)")]
    /// <summary>x BombTime (the game's 1000 ticks = 8.3 s of marching before the countdown). 1 = the game.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float FuseScale = 1f;
    /// <summary>x BombRange (the game's 200 u = 2.7 m blast radius; the blast is also 467 u tall). 1 = the game.</summary>
    [Export(PropertyHint.Range, "0.25,3,0.05")] public float BlastScale = 1f;
    /// <summary>mSerialBomb: a Bob-omb touching an exploding one goes off too (the game's default on).</summary>
    [Export] public bool ChainReaction = true;
    /// <summary>TBombHeiManager::canMakeDeadCoin: how many coins the bombs Mario carried may drop in one stage (the game's 20). 0 = none.</summary>
    [Export(PropertyHint.Range, "0,100,1")] public int MaxCarryCoins = 20;

    [ExportGroup("Effects and sounds (empty = none)")]
    /// <summary>TEffectExplosion: the blast at frame 2 of the death clip, at the body's scale. Default: BombExplosion.tscn (the disc's 10_bomb fireball + the
    /// four jpa emitters, pass 130b); any one-shot scene.</summary>
    [Export] public PackedScene ExplosionFxScene;
    public const string ExplosionFxPath = "res://assets/Sms/Enemy/Bomb/BombExplosion.tscn";
    /// <summary>PARTICLE_MS_BOMB_LIMIT: the fuse smoke bound to the body while it counts down. Default: BombFuse.tscn (ms_bomb_limit's port); any looping
    /// GPUParticles3D scene.</summary>
    [Export] public PackedScene FuseFxScene;
    public const string FuseFxPath = "res://assets/Sms/Enemy/Bomb/BombFuse.tscn";
    /// <summary>genEventCoin: the coin a bomb Mario carried drops where it lands (gpItemManager ACTOR_TYPE_COIN). Default his yellow_coin.tscn.</summary>
    [Export] public PackedScene EventCoinScene;
    public const string EventCoinScenePath = "res://yellow_coin.tscn";
    /// <summary>MSD_SE_EN_BOMBHEI_ZENMAI: the wind-up whir while it walks (looping: played while walking, stopped otherwise). Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D WindUpSound;
    /// <summary>MSD_SE_EN_BOMBHEI_COUNT: the beep at each count digit (3, 2, 1). Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D CountSound;
    /// <summary>The blast's sound (the game's is TEffectExplosion's). Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D ExplosionSound;

    // ------------------------------------------------------------------------------------------------------------------ the bck table
    // /scene/bombhei (delfinoBoss / pinnaBeach1 / sirena2 .szs): the files in order = setBckAnm's indices (bombhei_bastable); frames + loops read off
    // the disc (bck.py). Index 0 is the DEAD model's clip (downnejibomb_down1, 9 joints): the ModelDead glb's own player plays it.
    private static readonly string[] Names = { "", "nejibomb_damage1", "nejibomb_fling1", "nejibomb_land1", "nejibomb_run1", "nejibomb_run_down1", "nejibomb_stop_down1" };
    private static readonly int[] Frames = { 80, 59, 20, 44, 30, 119, 179 };
    private static readonly bool[] Loops = { false, false, true, false, true, false, false };
    protected const int BckDown = 0, BckDamage = 1, BckFling = 2, BckLand = 3, BckRun = 4, BckRunDown = 5, BckStopDown = 6;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    public const string DeadClip = "downnejibomb_down1";
    /// <summary>The btp texture indices into the bmd's TEX1 (btp_dump.py): 0 Q_nejibomb (the face / the "0" display), 2 Q_stop_nejibomb (the dizzy
    /// blue face), 3 / 4 / 5 Q_nejibomb_txanm1 / 2 / 3 (the "1" / "2" / "3"). nejibomb_count1.btp (119 frames, once): _body1 0 throughout; _led1
    /// 5 for frames 0-39, 4 for 40-79, 3 for 80-118. nejibomb_stop1.btp (2 frames): frame 0 -> 0, frame 1 -> 2 on both.</summary>
    public const int TexFace = 0, TexStop = 2, TexOne = 3, TexTwo = 4, TexThree = 5;
    public static readonly string[] TexPaths = { "res://models/Eniemes/Bomb/Q_nejibomb_s3tc.png", "", "res://models/Eniemes/Bomb/Q_stop_nejibomb_s3tc.png", "res://models/Eniemes/Bomb/Q_nejibomb_txanm1_s3tc.png", "res://models/Eniemes/Bomb/Q_nejibomb_txanm2_s3tc.png", "res://models/Eniemes/Bomb/Q_nejibomb_txanm3_s3tc.png" };

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveBGenerate = NerveSpecies + 0, NerveBAttack = NerveSpecies + 1, NerveBWalkExplosion = NerveSpecies + 2, NerveBWaitExplosion = NerveSpecies + 3, NerveBExplosion = NerveSpecies + 4,
        NerveBPickUp = NerveSpecies + 5, NerveBThrown = NerveSpecies + 6;
    protected override string NerveName(int n) => n switch { NerveBGenerate => "BombGenerate", NerveBAttack => "BombAttack", NerveBWalkExplosion => "WalkExplosion", NerveBWaitExplosion => "WaitExplosion", NerveBExplosion => "Explosion", NerveBPickUp => "PickUp", NerveBThrown => "Thrown", _ => base.NerveName(n) };
    protected override int DefaultNerve => NerveBGenerate;

    // ------------------------------------------------------------------------------------------------------------------ state
    private int _fuse;                 // unk198: the ticks toward BombTime
    private bool _doused;              // unk164: sitting doused (the thing Mario may pick up in the game)
    private int _waterCooldown;        // mSprayedByWaterCooldown
    private float _growTarget, _grow = 1f;   // unk1A0 / unk190: the blast's cylinder growth
    private int _bodyTex = TexFace, _ledTex = TexFace;
    private int _countBeeps;
    private Node3D _modelDead; private Skeleton3D _skelDead; private AnimationPlayer _deadAnim;
    private ShaderMaterial _bodyMat, _ledMat;
    private Node3D _fuseFx;
    private bool _blastSpawned;
    private bool _takenByMario;        // unk1A4: Mario carried it -> a coin where it blows (genEventCoin)
    private float _throwYaw, _throwPower;   // SMS_GetMarioAngleY / SMS_GetMarioThrowPower read at the release, for the Thrown nerve's first tick
    private static int _deadCoins; private static ulong _deadCoinsScene;   // TBombHeiManager::unk60: the carry coins made this stage

    public int FuseTicks => _fuse;
    public bool Doused => _doused;
    public float Grow => _grow;
    public int BodyTex => _bodyTex;
    public int LedTex => _ledTex;
    public bool Exploding => _cur == NerveBExplosion;
    public bool Thrown => _cur == NerveBThrown;
    public bool TakenByMario => _takenByMario;
    public Node3D FuseFxNow => _fuseFx;
    public Node3D LastBlastFx { get; private set; }
    public string DeadClipName => _deadAnim != null && _deadAnim.IsPlaying() ? _deadAnim.CurrentAnimation : "";
    public float DeadClipFrame => _deadAnim != null && _deadAnim.IsPlaying() ? (float)_deadAnim.CurrentAnimationPosition * 60f : -1f;
    /// <summary>The blast's reach right now, metres (the attack radius x the growth).</summary>
    public float BlastRadiusMetres => Prm.AttackRadius * _bodyScale * _grow / U;

    protected override void Init()
    {
        _flags150 = 17;                                   // unk150 = 0x11
        SetGoalMario();
        ExplosionFxScene ??= ResourceLoader.Exists(ExplosionFxPath) ? GD.Load<PackedScene>(ExplosionFxPath) : null;
        FuseFxScene ??= ResourceLoader.Exists(FuseFxPath) ? GD.Load<PackedScene>(FuseFxPath) : null;
        EventCoinScene ??= ResourceLoader.Exists(EventCoinScenePath) ? GD.Load<PackedScene>(EventCoinScenePath) : null;
        _modelDead = GetNodeOrNull<Node3D>("ModelDead");
        if (_modelDead != null)
        {
            foreach (Node n in _modelDead.FindChildren("*", "Skeleton3D", true, false)) { _skelDead = n as Skeleton3D; if (_skelDead != null) break; }
            _deadAnim = _modelDead.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            if (_deadAnim == null) foreach (Node n in _modelDead.FindChildren("*", "AnimationPlayer", true, false)) { _deadAnim = n as AnimationPlayer; if (_deadAnim != null) break; }
            if (_deadAnim != null && _deadAnim.HasAnimation(DeadClip)) _deadAnim.GetAnimation(DeadClip).LoopMode = Animation.LoopModeEnum.None;
            _modelDead.Visible = false;
        }
        DressBody();
    }

    /// <summary>Per-instance copies of the two btp-driven materials ('_body1', '_led1'), found by their resource names on the live model.</summary>
    private void DressBody()
    {
        if (_model == null) return;
        foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (n is not MeshInstance3D mi || mi.Mesh == null) continue;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var m = mi.GetActiveMaterial(i) as ShaderMaterial;
                if (m == null) continue;
                string name = (m.ResourceName ?? "").Split(' ')[0];
                if (name.StartsWith("_body1")) { _bodyMat = (ShaderMaterial)m.Duplicate(); mi.SetSurfaceOverrideMaterial(i, _bodyMat); }
                else if (name.StartsWith("_led1")) { _ledMat = (ShaderMaterial)m.Duplicate(); mi.SetSurfaceOverrideMaterial(i, _ledMat); }
            }
        }
        SetTextures(TexFace, TexFace);
    }
    private static Texture2D _texCache0, _texCache2, _texCache3, _texCache4, _texCache5;
    private static Texture2D Tex(int index)
    {
        ref Texture2D slot = ref _texCache0;
        switch (index) { case 2: slot = ref _texCache2; break; case 3: slot = ref _texCache3; break; case 4: slot = ref _texCache4; break; case 5: slot = ref _texCache5; break; }
        if (slot == null && index >= 0 && index < TexPaths.Length && TexPaths[index] != "" && ResourceLoader.Exists(TexPaths[index])) slot = GD.Load<Texture2D>(TexPaths[index]);
        return slot;
    }
    /// <summary>The btp's work: the body's and the LED's texture (indices into the bmd's TEX1).</summary>
    private void SetTextures(int body, int led)
    {
        _bodyTex = body; _ledTex = led;
        var tb = Tex(body); var tl = Tex(led);
        if (_bodyMat != null && tb != null) _bodyMat.SetShaderParameter("base_tex", tb);
        if (_ledMat != null && tl != null) _ledMat.SetShaderParameter("base_tex", tl);
    }
    /// <summary>nejibomb_count1.btp at `frame`: the LED shows 3 (0-39), 2 (40-79), 1 (80-118); the body keeps its face.</summary>
    private void CountFace(float frame)
    {
        int led = frame < 40f ? TexThree : (frame < 80f ? TexTwo : TexOne);
        if (led != _ledTex || _bodyTex != TexFace)
        {
            SetTextures(TexFace, led);
            if (CountSound != null && CountSound.Stream != null) CountSound.Play();    // MSD_SE_EN_BOMBHEI_COUNT every 40 btp frames
            _countBeeps++;
        }
    }

    protected override void Reset()
    {
        base.Reset();
        _fuse = 0; _doused = false; _waterCooldown = 0; _grow = 1f; _growTarget = 1f; _countBeeps = 0; _blastSpawned = false;
        _takenByMario = false; _throwYaw = 0f; _throwPower = 0f;   // TBombHei::reset: unk1A4 = 0, unk164 = 0
        _sitHeld = false; _stopFaceOn = false; _resumeFrame = -1f;
        if (_model != null) _model.Visible = true;
        if (_modelDead != null) _modelDead.Visible = false;
        if (_deadAnim != null) _deadAnim.Stop();
        SetTextures(TexFace, TexFace);
        BoundFuse(false);
    }

    // ------------------------------------------------------------------------------------------------------------------ anims and hooks
    protected override void SetWalkAnm() => SetBck(BckRun);
    protected override void SetFreezeAnm() => SetBck(BckDamage);
    /// <summary>setDeadAnm: the pieces model at a random yaw, downnejibomb_down1, the camera shake (+ the rumble, not ported).</summary>
    protected override void SetDeadAnm()
    {
        if (_model != null) _model.Visible = false;
        if (_anim != null) _anim.Stop();
        _yaw = Rand() * 360f;
        if (_modelDead != null) _modelDead.Visible = true;
        if (_deadAnim != null && _deadAnim.HasAnimation(DeadClip)) { _deadAnim.Play(DeadClip); _deadAnim.Seek(0f, true); _deadAnim.SpeedScale = 1f; }
        SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage);
        if (ExplosionSound != null && ExplosionSound.Stream != null) ExplosionSound.Play();
    }
    private bool DeadClipEnded => _deadAnim == null || !_deadAnim.IsPlaying();

    /// <summary>TBombHei::isHitValid: nothing but Yoshi's eat -- a stomp does nothing to a Bob-omb.</summary>
    protected override bool IsHitValid(HitMessage msg) => false;
    /// <summary>TBombHei::attackToMario: only the explosion hurts -- HIT_MESSAGE_BURN (dmgfire + FIRE_DOWN).</summary>
    protected override void AttackToMario() { if (_cur == NerveBExplosion) _mario?.TakeDamage(GlobalPosition, Mario.SmsDamageProfile.Fire); }
    /// <summary>TBombHei::behaveToWater: walking or landing with the hit points gone -> WaitExplosion (the dousing); a 20-tick cooldown.</summary>
    protected override void BehaveToWater()
    {
        if (_waterCooldown > 0) return;
        if (IsBck(BckRun) || IsBck(BckLand))
        {
            if (_hp == 0) PushNerve(NerveBWaitExplosion);
            _waterCooldown = 20;
        }
    }
    /// <summary>TBombHei::isCollidMove: an exploding Bob-omb sets off the one touching it (mSerialBomb); the push goes on.</summary>
    protected override bool IsCollidMove(Node3D other)
    {
        if (ChainReaction && other is SmsBombHei b && b.Exploding) ChainIn();
        return true;
    }
    /// <summary>The pair test the game runs on every live actor (gpObjHitCheck: the damage cylinders overlapping) -> isCollidMove on both: from the
    /// exploding one's side, every other Bob-omb whose cylinder meets its own goes off.</summary>
    private void ChainNeighbours()
    {
        float s = _bodyScale;
        foreach (Node n in GetTree().GetNodesInGroup("sms_enemy"))
            if (n is SmsBombHei o && o != this && IsInstanceValid(o) && !o._dead && o._cur != NerveBExplosion
                && CheckDistance(Pos(), Prm.DamageRadius * s, Prm.DamageHeight * s, o.Pos(), Prm.DamageRadius * o._bodyScale, Prm.DamageHeight * o._bodyScale))
                o.ChainIn();
    }
    private void ChainIn() { if (_cur != NerveBExplosion && !_dead) PushNerve(NerveBExplosion); }
    /// <summary>TBombHei::kill (a demo's killEnemiesWithin aside): hp 1, the Explosion now.</summary>
    public override void Kill()
    {
        if (_dead) return;
        _hp = 1;
        if (_cur != NerveBExplosion) { _stack.Clear(); SetNext(NerveBExplosion); }
    }
    /// <summary>TBombHei::getGravityY: ThrownGravityY (0.3 a tick) while thrown, else mGravity.</summary>
    protected override float GravityY() => _cur == NerveBThrown ? Prm.ThrownGravityY : base.GravityY();
    protected override bool WallHopAllowed() => _cur == NerveBAttack || _cur == NerveBWalkExplosion;

    // ------------------------------------------------------------------------------------------------------------------ taken (pass 130b)
    /// <summary>TMario::hitPickUpEnemy: his touch takes it while unk164 (doused) is set -- a sitting dizzy Bob-omb; never a walking, counting or exploding one.</summary>
    public override bool SmsCanBeTaken() => _doused && !_dead && _holder == null && _cur != NerveBExplosion && GetCollisionLayerValue(1);
    /// <summary>TBombHei::behaveToTaken: unk1A4 = 1 for Mario (the coin at the end), PickUp pushed unless already in it.</summary>
    protected override void BehaveToTaken(Mario holder)
    {
        if (_cur == NerveBPickUp) return;
        _takenByMario = true;
        PushNerve(NerveBPickUp);
    }
    /// <summary>TBombHei::behaveToRelease: nothing unless doused or counting (the Explosion keeps its place); not twice; else Thrown pushed with his angle and
    /// throw power read now (SMS_GetMarioAngleY / SMS_GetMarioThrowPower in the nerve's first tick, which runs on the next tick).</summary>
    protected override void BehaveToRelease(Mario holder, bool put)
    {
        if (!_doused && _cur != NerveBWalkExplosion) return;
        if (_cur == NerveBThrown) return;
        Vector3 f = holder != null && IsInstanceValid(holder) ? holder.SmsFacingDir() : PolarXZ(_yaw, 1f);
        f.Y = 0f;
        _throwYaw = f.LengthSquared() > 1e-6f ? Mathf.RadToDeg(Mathf.Atan2(f.X, f.Z)) : _yaw;
        _throwPower = SmsParams.Take.ThrowPower;
        PushNerve(NerveBThrown);
    }
    /// <summary>TBombHei::genEventCoin: a bomb Mario carried (unk1A4) makes a coin at its point flying toward him (20 u/tick each way, 20 up) -- while the
    /// manager's budget (20 a stage) lasts.</summary>
    protected virtual void GenEventCoin()
    {
        if (!_takenByMario || EventCoinScene == null || !CanMakeDeadCoin()) return;
        Vector3 d = MarioPos() - Pos(); d.Y = 0f;
        d = d.LengthSquared() > 1e-6f ? d.Normalized() : PolarXZ(_yaw, 1f);
        DropSpawner.SpawnItem(EventCoinScene, GetParent(), GlobalPosition, new Vector3(d.X * 20f, 20f, d.Z * 20f), 0, this);
    }
    private bool CanMakeDeadCoin()
    {
        ulong scene = GetTree()?.CurrentScene?.GetInstanceId() ?? 0;
        if (scene != _deadCoinsScene) { _deadCoinsScene = scene; _deadCoins = 0; }   // TBombHeiManager lives with the stage: a new one, a new count
        if (_deadCoins >= MaxCarryCoins) return false;
        _deadCoins++;
        return true;
    }

    /// <summary>moveObject's fuse: every tick outside Thrown / Explosion / Generate; past BombTime the doused flag drops and (unless sitting in
    /// WaitExplosion, whose own clock takes over) WalkExplosion is pushed.</summary>
    protected override void MoveObjectPre()
    {
        if (_waterCooldown > 0) _waterCooldown--;
        if (_cur == NerveBExplosion || _cur == NerveBGenerate || _cur == NerveBThrown || _cur == NerveNone) return;   // moveObject: not in Thrown / Explosion / Generate / Change (it DOES count in PickUp: a carried bomb goes off in his hands)
        _fuse++;
        if (_fuse > Prm.BombTime * FuseScale)
        {
            _doused = false; _fuse = 0;
            if (_cur != NerveBWaitExplosion && _cur != NerveBWalkExplosion) PushNerve(NerveBWalkExplosion);   // (the game re-pushes in WalkExplosion too, but its 1000-tick fuse can never run out again inside the 238-tick count; a short FuseScale could)
        }
    }

    /// <summary>The blast's lists: the attack cylinder grown by <see cref="_grow"/> (expandCollision) on Mario's damage cylinder -> BURN; the base's
    /// own tests otherwise (a stomp is refused by isHitValid; the hose reaches SmsSprayedByWater).</summary>
    protected override void HitCheckMario()
    {
        if (_cur != NerveBExplosion) { base.HitCheckMario(); return; }
        if (_mario == null || _dead || !GetCollisionLayerValue(1)) return;   // HIT_FILTER_NO_COLLISION (a blast still in his hands reaches him by sendAttackMsgToMario instead)
        float s = _bodyScale * _grow;
        if (CheckDistance(Pos(), Prm.AttackRadius * s, Prm.AttackHeight * s, MarioPos(), SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight))
            AttackToMario();
    }

    private void BoundFuse(bool on)
    {
        if (on && _fuseFx == null && FuseFxScene != null)
        {
            _fuseFx = FuseFxScene.Instantiate<Node3D>();
            AddChild(_fuseFx);
            _fuseFx.Position = Vector3.Up * (90.5f * _scale / U);     // joint 1 'body1' (0, 90.5, 0)
            _fuseFx.Scale = Vector3.One * _scale;
        }
        else if (!on && _fuseFx != null) { _fuseFx.QueueFree(); _fuseFx = null; }
    }
    private void WindUp(bool on)
    {
        if (WindUpSound == null || WindUpSound.Stream == null) return;
        if (on && !WindUpSound.Playing) WindUpSound.Play();
        else if (!on && WindUpSound.Playing) WindUpSound.Stop();
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveBGenerate:                                 // TNerveBombHeiGenerate
                if (_time == 0) { if (_model != null) _model.Visible = true; SetBck(BckFling); SetTextures(TexFace, TexFace); }
                if (!_airborne)
                {
                    if (IsBck(BckFling)) SetBck(BckLand);
                    else if (BckEnded()) { PushAfterCurrent(NerveBAttack); return true; }
                }
                else if (!IsBck(BckFling)) { SetBck(BckFling); SetTextures(TexFace, TexFace); }
                return false;

            case NerveBAttack:                                   // TNerveBombHeiAttack
                if (_time == 0) { SetWalkAnm(); _doused = false; SetGoalMario(); }
                WindUp(true);
                WalkBehaviorBase(2, 1f);
                return false;

            case NerveBWalkExplosion:                            // TNerveBombHeiWalkExplosion
                if (_time == 0) { SetBck(BckRunDown); CountFace(0f); BoundFuse(true); }
                else if (BckEnded()) { PushAfterCurrent(NerveBExplosion); return true; }
                CountFace(BckFrame());                           // the count btp runs with the clip (119 frames each)
                WindUp(true);
                WalkBehaviorBase(2, 0.6f);
                return false;

            case NerveBWaitExplosion:                            // TNerveBombHeiWaitExplosion
                if (_time == 0) { SetBck(BckStopDown); _doused = true; _stopFaceOn = false; _resumeFrame = -1f; WindUp(false); }
                if (_doused)
                {
                    float f = BckFrame();
                    if (f >= 60f && !_sitHeld) { SeekBck(60f); SetBckPaused(true); _sitHeld = true; }        // checkPass(60): the clip held -- it sits
                    if (f >= 10f && !_stopFaceOn) { SetTextures(TexStop, TexStop); _stopFaceOn = true; }   // checkPass(10): the stop btp to frame 1 -- the dizzy face
                }
                else
                {
                    if (_sitHeld) { SetBckPaused(false); _sitHeld = false; _resumeFrame = BckFrame(); BoundFuse(true); }   // the fuse ran out while it sat: the clip runs on
                    float f = BckFrame() - (_resumeFrame < 0f ? 0f : _resumeFrame);
                    CountFace(MathF.Max(f, 0f));                 // the count btp from its start (3 / 2 / 1)
                    if (BckEnded() && _time > 150) { PushAfterCurrent(NerveBExplosion); return true; }
                }
                return false;

            case NerveBExplosion:                                // TNerveBombHeiExplosion
                if (_time == 0)
                {
                    _growTarget = Prm.BombRange * BlastScale * _bodyScale / Prm.AttackRadius;
                    _grow = 1f;
                    SetDeadAnm();
                    if (_holder != null) SendAttackMsgToMario();   // getHolder() == gpMarioAddress -> sendAttackMsgToMario: it went off in his hands (the common ATTACK, not the blast's BURN)
                    _doused = false;
                    WindUp(false); BoundFuse(false);
                    // the collision stays on through the blast (the game turns it off at the end): the body lists are the blast's own (HitCheckMario),
                    // a stomp is refused by isHitValid, the hose finds nothing to do -- and the contact is what sets off a neighbour (isCollidMove)
                }
                if (!_blastSpawned && DeadClipFrame >= 2f)      // calcRootMatrix: frame 2 of down1 -> TEffectExplosion::generate(mPosition, mScaling)
                {
                    _blastSpawned = true;
                    float s = _bodyScale * BlastScale;           // the game: mScaling (the fireball model is its own 200 u -> 240 u); BlastScale folded in so the knob scales the look with the reach
                    LastBlastFx = SpawnBurst(ExplosionFxScene, new Transform3D(Basis.FromScale(Vector3.One * s), GlobalPosition));
                }
                if (_grow < _growTarget) _grow *= 1.2f;
                else if (DeadClipEnded || _time > 400)
                {
                    Dead();
                    return true;
                }
                if (ChainReaction && (_tickCount & 3) == 0 && GetCollisionLayerValue(1)) ChainNeighbours();   // isCollidMove on the pair: the hit lists meet every fourth tick (none under NO_COLLISION: a blast in his hands)
                return false;

            case NerveBPickUp:                                   // TNerveBombHeiPickUp: nothing happens in his hands (the sit clip stays frozen, the dizzy face on); a bomb no longer doused when it starts falls straight back
                if (_time == 0 && !_doused) return true;
                return false;

            case NerveBThrown:                                   // TNerveBombHeiThrown
                if (_time == 0)
                {
                    float rate = Prm.ThrownRateXZ;
                    Vector3 f = PolarXZ(_throwYaw, 1f);
                    _vel = new Vector3(rate * _throwPower * f.X, Prm.ThrownVY, rate * _throwPower * f.Z);   // (sin, cos of SMS_GetMarioAngleY) x rate x SMS_GetMarioThrowPower; VY
                    GlobalPosition += Vector3.Up * (2f / U);     // mPosition.y += 2
                    _airborne = true;
                    _onWall = false;
                    if (DebugPrint) GD.Print($"[SMS bomb] {Name}: thrown at ({_vel.X:F2}, {_vel.Y:F1}, {_vel.Z:F2}) u/tick toward {_throwYaw:F0} deg, gravity {Prm.ThrownGravityY} a tick");
                }
                if (!_airborne && _time > 0)                     // landed (bind: nextPos.y <= ground -> AIRBORNE off): the coin, the blast
                {
                    GenEventCoin();
                    PushAfterCurrent(NerveBExplosion);
                    return true;
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }
    private bool _sitHeld, _stopFaceOn; private float _resumeFrame = -1f;

    protected override void Dead()
    {
        base.Dead();
        WindUp(false); BoundFuse(false);
        if (_modelDead != null) _modelDead.Visible = false;
        if (_deadAnim != null) _deadAnim.Stop();
    }

    /// <summary>The pieces ride the live model's frame (the random yaw of the death included).</summary>
    protected override void ApplyLook()
    {
        base.ApplyLook();
        if (_modelDead != null && _model != null) _modelDead.Transform = _model.Transform;
    }
}
