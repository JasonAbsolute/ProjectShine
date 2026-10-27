using System.Collections.Generic;
using Godot;

/// <summary>
/// The LAUNCHER -- TLauncher / TCommonLauncher (Enemy/launcher.cpp; pass 126): the goop pile ("コモンランチャー", /scene/clauncher/generator_model1.bmd,
/// its 4 bcks and 2 bas) that spits enemies out of itself, on /enemy/launcher.prm (HeadHeight 120, BodyRadius 30, WallRadius 50, ClipRadius 300,
/// FarClip 10000, InstanceNum 100, ActiveEnemyNum 10, LaunchPeriod 200 -- the launchers' own manager; the common one takes its period from the
/// placement: mLaunchPeriod, Bianco's 768). A TSpineEnemy with a nerve that waits forever: its life is a 5-state machine ticked by control():
///   NORMAL  (bck 3 wait1, looping): the cooldown counts down; at 0 the enemy manager named in the placement (here <see cref="EnemyScene"/> with
///           <see cref="PoolSize"/> bodies) is asked for a far-out body -- none: it stays, asking every tick; one: -> LAUNCH.
///   LAUNCH  (bck 0 appear1, 89 frames): at tick 62 getProperEnemy -> resetSRTV at the launcher's point, rotation 0, the body's scale, velocity
///           (0, 4, 0) turned by the PLACED rotation (mRotation.x - 270: see below), on the launcher's graph; the clip ending -> cooldown = period, NORMAL.
///   HIT     (bck 1 damage1, 39 frames): tick 0 decHitPoints; the clip ending -> DIE at 0 HP, else NORMAL. MS_GENE_HIT (the goop drops) is asked for
///           every frame the damage clip is current, bound to joint 0 with the stage's pollution colour (SMSSetEmitterPolColor 6) at the body's scale.
///   DIE     (bck 2 dead1, 64 frames): the clip ending -> MS_GENE_DEAD (pol colour 6) + MS_ENM_DISAP_A + _B at joint 0, scaled; kill(), no collision.
///   WATER:  HIT_MESSAGE_SPRAYED_BY_WATER (dead / dying: refused) -> the WATHIT mark, MSD_SE_EN_COMMON_W_HIT_OK; already HIT: taken, nothing; else -> HIT.
///   REGEN:  in NORMAL below 5 HP, +1 HP every 1200 ticks (mRegenTimer). HP 5 at init (resetHitPoints then mHitPoints = 5; launcher.prm's 255 unused).
///   TOUCH:  every tick Mario in the hit cylinder (150 u x 100 u at the body's scale) gets HIT_MESSAGE_ATTACK (his TakeDamage, the common profile).
///   LOOK:   initHitActor 150 x 100 (the attack and the damage cylinders); the model LIGHT_TYPE_OBJECT; the dummy texture Q_gene_dummy_I4 swapped for
///           the stage's /scene/map/pollution/H_ma_rak.bti (LauncherBody.tres: goop_tex = the stage's goop mark, the Gooble brain's); mRotation.x += 270:
///           the clips pose the root joint +90 deg about X (the bind pose lies flat), the actor's 270 lays it back down -- Launcher.tscn's Model carries
///           that -90 deg; the launch direction uses the placed rotation (this node's) alone.
/// The hit body: a StaticBody3D on <see cref="SmsHiddenCoin.HideLayer"/> (the drops' ray reaches it; nothing collides -- the game's hit actor blocks
/// nothing either, Mario walks into the pile and is hurt). Knobs: the period, the pool, the hit points, the regen, the fx slots, the sound slots
/// (generator_damage1.bas / generator_dead1.bas: MSD_SE_EN_COMMON_W_HIT_OK and the two clip cues, empty), the launch velocity.
/// NOT PORTED (logged): the manager's clipping (LIVE_FLAG_CLIPPED_OUT: the far-out rule is a distance), the TLauncher base's bare 12-tick states (no
/// model: no stage uses it alone), the sounds.
/// </summary>
[GlobalClass]
public partial class SmsEnemyLauncher : StaticBody3D, ISmsSprayTarget
{
    public enum State { Initial, HitByWater, Normal, Launch, Die }
    // launcher.prm
    public const float HeadHeight = 120f, BodyRadius = 30f, WallRadius = 50f, ClipRadius = 300f, FarClip = 10000f;
    public const int PrmLaunchPeriod = 200, PrmInstanceNum = 100, PrmActiveEnemyNum = 10;
    // TCommonLauncher
    public const float HitRadiusUnits = 150f, HitHeightUnits = 100f;
    public const int LaunchFrame = 0x3E, RegenPeriod = 1200, MaxHitPoints = 5;   // the spit at frame 62 of the 89-frame appear clip (mTicksSpentInCurState == 0x3E: the clip at 1 frame a game frame)
    public static readonly string[] BckNames = { "generator_appear1", "generator_damage1", "generator_dead1", "generator_wait1" };
    public static readonly int[] BckFrames = { 89, 39, 64, 60 };
    public static readonly bool[] BckLoops = { false, false, false, true };
    public const int BckLaunch = 0, BckDamage = 1, BckDead = 2, BckWait = 3;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>The kind this launcher spits (a scene whose root is an SmsSmallEnemy: the Stu in Bianco).</summary>
    [Export] public PackedScene EnemyScene;
    /// <summary>That kind's manager pool (mSLActiveEnemyNum: the Stu's 6 in hamukuri.prm) -- how many can be out at once.</summary>
    [Export] public int PoolSize = 6;
    /// <summary>mLaunchPeriod from the placement: ticks between launches (TCommonLauncher's default 120; Bianco's pile 768).</summary>
    [Export] public int LaunchPeriodTicks = 120;
    /// <summary>The walk graph the launched bodies take (getProperEnemy: the launcher's own graph); none = they wander free.</summary>
    [Export] public Node3D Graph;
    /// <summary>The launch velocity in units a tick before the node's rotation (the game's (0, 4, 0)).</summary>
    [Export] public Vector3 LaunchVelocityUnits = new Vector3(0f, 4f, 0f);
    /// <summary>A live body farther than this (metres) from Mario counts as clipped out and may be taken back.</summary>
    [Export] public float RecycleDistance = 60f;
    /// <summary>The hit points at birth (TCommonLauncher::init: 5) and the ceiling the regen fills to.</summary>
    [Export] public int HitPoints = MaxHitPoints;
    /// <summary>Ticks of NORMAL per regained hit point (mRegenTimer > 1200).</summary>
    [Export] public int RegenTicks = RegenPeriod;
    /// <summary>MS_GENE_HIT: the goop drops while the damage clip plays (an SmsNpcEffect on the root joint; GeneHit.tscn). Unset = none.</summary>
    [Export] public PackedScene HitFxScene;
    /// <summary>MS_GENE_DEAD: the goop burst at death (an SmsFxBurst at the body; GeneDead.tscn). Unset = none.</summary>
    [Export] public PackedScene DeadFxScene;
    /// <summary>MS_ENM_DISAP_A + _B at death (EnemyDisap.tscn, the small enemies' own). Unset = none.</summary>
    [Export] public PackedScene DisapFxScene;
    /// <summary>MSD_SE_EN_COMMON_W_HIT_OK on each water hit; generator_damage1.bas's cue; generator_dead1.bas's two cues. Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D WaterHitSound, DamageSound, DeadSound;
    /// <summary>On: the body goes on the spray-only layer (the hidden coins' HideLayer) with no mask at _Ready, whatever the scene says.</summary>
    [Export] public bool AutoLayer = true;
    [Export] public bool DebugPrint;

    // ------------------------------------------------------------------------------------------------------------------ state
    private readonly SmsEnemyPool _pool = new();
    private State _state = State.Normal, _next = State.Initial;   // the game: mState 2, mNextState 0 -> the first control() jumps to Initial
    private int _ticksInState, _cooldown, _regen, _hp;
    private bool _dead, _filled, _spat;
    private float _accum;
    private Node3D _model; private AnimationPlayer _anim; private Skeleton3D _skel; private int _bck = -1; private float _bckRate = 60f;
    private Mario _mario;
    private SmsNpcEffect _hitFx;
    private readonly Dictionary<string, BoneAttachment3D> _fxCache = new();

    public SmsEnemyPool Pool => _pool;
    public State StateNow => _state;
    public int TicksInState => _ticksInState;
    public int Cooldown => _cooldown;
    public int Hp => _hp;
    public bool IsDead => _dead;
    public int Launched { get; private set; }
    public int TicksSeen { get; private set; }
    public ulong LastPhysicsFrame { get; private set; }
    public string BckName => _bck >= 0 ? BckNames[_bck] : "";
    public float Scale3 => GlobalTransform.Basis.Scale.Y;
    public string DebugState => $"{_state} t{_ticksInState} bck {BckName} f{BckFrame():F0} hp {_hp} cooldown {_cooldown} regen {_regen} dead {_dead} pool {_pool.Alive}/{_pool.Count}";

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>("Model");
        _anim = GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_model == null || _anim == null) GD.PushWarning($"[SMS launcher] {GetPath()}: the scene needs Model (the glb) with Model/AnimationPlayer");
        if (_model != null) foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        if (AutoLayer) { CollisionLayer = SmsHiddenCoin.HideLayer; CollisionMask = 0; }
        _hp = HitPoints;
        _cooldown = (int)(LaunchPeriodTicks * GD.Randf());       // mLaunchCooldown = mLaunchPeriod x MsRandF
        SmsWaterHitMarks.Ensure(this);
        CallDeferred(MethodName.FillPool);
    }
    private void FillPool()
    {
        if (_filled) return;
        _filled = true;
        int made = _pool.Fill(GetParent(), EnemyScene, PoolSize, GlobalPosition, DebugPrint, Name);
        if (DebugPrint) GD.Print($"[SMS launcher] {Name}: {made} of {(EnemyScene != null ? EnemyScene.ResourcePath.GetFile() : "nothing")} pooled; the first launch in {_cooldown} ticks");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        LastPhysicsFrame = Engine.GetPhysicsFrames();
        int n = SmsUnits.TicksFor(delta, ref _accum);
        for (int i = 0; i < n; i++) { TicksSeen++; Tick(); }
    }

    /// <summary>TLauncher::control + TCommonLauncher::perform(CUE_MOVE): the state's step, the state change, the regen, the touch.</summary>
    private void Tick()
    {
        if (_dead) return;
        switch (_state)
        {
            case State.Initial: _next = State.Normal; break;                                     // TCommonLauncher::stateInitial: at once
            case State.Normal: StateNormal(); break;
            case State.HitByWater: StateHitByWater(); break;
            case State.Launch: StateLaunch(); break;
            case State.Die: StateDie(); break;
        }
        if (_state != _next) { if (DebugPrint) GD.Print($"[SMS launcher] {Name}: {_state} -> {_next} after {_ticksInState} ticks (hp {_hp}, cooldown {_cooldown})"); _state = _next; _ticksInState = 0; }
        else _ticksInState++;
        if (_dead) return;
        if (_state == State.Normal && _hp < HitPoints && ++_regen > RegenTicks) { _regen = 0; _hp++; }
        TouchMario();
    }
    private void ChangeState(State s) => _next = s;

    private void StateNormal()
    {
        if (_ticksInState == 0) SetBck(BckWait);
        if (_cooldown > 0) _cooldown--;
        if (_cooldown > 0 || !_filled) return;
        _cooldown = LaunchPeriodTicks;
        if (FarOut() == null) return;                              // nobody to launch: ask again in a period
        ChangeState(State.Launch);
    }
    private void StateHitByWater()
    {
        if (_ticksInState == 0)
        {
            SetBck(BckDamage);
            _hp = Mathf.Max(0, _hp - 1);
            if (DamageSound != null && DamageSound.Stream != null) DamageSound.Play();
            if (HitFxScene != null && _skel != null) _hitFx = SmsNpcEffect.Spawn(HitFxScene, _skel, _skel.GetBoneName(0), Scale3, _fxCache, Name);
        }
        if (BckEnded())
        {
            if (_hitFx != null && IsInstanceValid(_hitFx)) _hitFx.Stop();
            _hitFx = null;
            ChangeState(_hp == 0 ? State.Die : State.Normal);
        }
    }
    private void StateLaunch()
    {
        if (_ticksInState == 0) { SetBck(BckLaunch); _spat = false; }
        if (!_spat && BckFrame() >= LaunchFrame)
        {
            _spat = true;
            var e = FarOut();
            if (e != null)
            {
                e.SmsLaunch(GlobalPosition, 0f, GlobalTransform.Basis.Orthonormalized() * LaunchVelocityUnits, Graph);
                Launched++;
                if (DebugPrint) GD.Print($"[SMS launcher] {Name}: launched {e.Name} ({_pool.Alive} of {_pool.Count} out)");
            }
        }
        if (BckEnded()) { _cooldown = LaunchPeriodTicks; ChangeState(State.Normal); }
    }
    private void StateDie()
    {
        if (_ticksInState == 0) { SetBck(BckDead); if (DeadSound != null && DeadSound.Stream != null) DeadSound.Play(); }
        if (BckEnded() && _bck == BckDead)
        {
            Transform3D at = new Transform3D(Basis.FromScale(Vector3.One * Scale3), GlobalPosition);
            SpawnBurst(DeadFxScene, at);
            SpawnBurst(DisapFxScene, at);
            Kill();
        }
    }
    /// <summary>TLauncher::kill (TSpineEnemy's): LIVE_FLAG_DEAD -- hidden, no collision, the clips stopped; it stays dead.</summary>
    public void Kill()
    {
        _dead = true;
        if (_model != null) _model.Visible = false;
        CollisionLayer = 0;
        if (_anim != null) _anim.Stop();
        if (_hitFx != null && IsInstanceValid(_hitFx)) { _hitFx.Stop(); _hitFx = null; }
    }

    private SmsSmallEnemy FarOut()
    {
        Vector3 mp = _mario != null && IsInstanceValid(_mario) ? _mario.GlobalPosition : GlobalPosition;
        return _pool.FarOut(mp, RecycleDistance);
    }

    /// <summary>TCommonLauncher::perform: Mario in the hit cylinder (150 x 100 u at the body's scale) -> SMS_SendMessageToMario(ATTACK) every tick (his
    /// TakeDamage keeps its own invincibility).</summary>
    private void TouchMario()
    {
        if (_mario == null || !IsInstanceValid(_mario)) return;
        float u = SmsUnits.UnitsPerMetre, s = Scale3;
        Vector3 d = _mario.GlobalPosition - GlobalPosition;
        if (new Vector2(d.X, d.Z).Length() * u > HitRadiusUnits * s) return;
        if (d.Y * u < -HitHeightUnits * s * 0.25f || d.Y * u > HitHeightUnits * s) return;
        _mario.TakeDamage(GlobalPosition, Mario.SmsDamageProfile.EnemyCommon);
    }

    /// <summary>TLauncher::receiveMessage from a water actor: dead or dying, refused; else the WATHIT mark and the sound, and HIT unless already HIT.</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead || _state == State.Die) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (WaterHitSound != null && WaterHitSound.Stream != null) WaterHitSound.Play();
        if (_state == State.HitByWater) return true;
        ChangeState(State.HitByWater);
        return true;
    }

    // ------------------------------------------------------------------------------------------------------------------ clips and fx
    private void SetBck(int i)
    {
        _bck = i;
        if (_anim == null || !_anim.HasAnimation(BckNames[i])) return;
        var clip = _anim.GetAnimation(BckNames[i]);
        clip.LoopMode = BckLoops[i] ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        _bckRate = clip.Length > 0f ? BckFrames[i] / (float)clip.Length : 60f;
        _anim.Play(BckNames[i]);
        _anim.Seek(0f, true);
        _anim.SpeedScale = 60f / _bckRate;
    }
    /// <summary>getCurAnmFrameNo: the clip's position in bck frames (the state's ticks when the clip is missing).</summary>
    public float BckFrame()
    {
        if (_bck < 0) return 0f;
        if (_anim == null || !_anim.HasAnimation(BckNames[_bck]) || _anim.CurrentAnimation != BckNames[_bck]) return _ticksInState;
        return (float)_anim.CurrentAnimationPosition * _bckRate;
    }
    /// <summary>curAnmEndsNext: the one-shot clip's last frame reached (the frame counted by the state's ticks when the clip is missing).</summary>
    private bool BckEnded()
    {
        if (_bck < 0) return true;
        if (_anim == null || !_anim.HasAnimation(BckNames[_bck])) return _ticksInState + 1 >= BckFrames[_bck] - 1;
        if (!_anim.IsPlaying() || _anim.CurrentAnimation != BckNames[_bck]) return true;
        return (float)_anim.CurrentAnimationPosition * _bckRate + 1.1f >= BckFrames[_bck] - 1;
    }
    private void SpawnBurst(PackedScene scene, Transform3D at)
    {
        if (scene == null) return;
        var fx = scene.Instantiate<Node3D>();
        fx.TopLevel = true;
        GetParent().AddChild(fx);
        fx.GlobalTransform = at;
        if (fx is SmsFxBurst burst) burst.Play(at);
        else if (fx is SmsNpcEffect npc) npc.Start(at.Basis.Scale.Y);
        else if (fx is GpuParticles3D gp) { gp.OneShot = true; gp.Emitting = true; gp.Finished += gp.QueueFree; }
    }
}
