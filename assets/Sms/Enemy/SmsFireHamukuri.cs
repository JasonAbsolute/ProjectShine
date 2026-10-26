using System;
using Godot;

/// <summary>
/// SMOLDERIN' STU -- TFireHamuKuri (Enemy/hamukuri.cpp 2120-2312; JP ファイアハムクリ, the burning Stu of Corona Mountain) on <see cref="SmsHamukuri"/>,
/// with the shipped /enemy/firehamukuri.prm (pass 125g, feature/enemy-base). The Stu's model (default.bmd) and clips; what changes is the FIRE.
///
/// BURNING (unk210): lit on the first walk tick after a reset / recover (walkBehavior: unk214 == 0 and not burning -> burning). While burning:
/// its hit is HIT_MESSAGE_BURN (Mario catches fire -- <see cref="SmsBurnMethod"/> on Mario if he has one, else his plain damage), it cannot be stomped
/// (isHitValid false: Mario bounces off and his feet take the burn through its attack cylinder), and PARTICLE_MS_MOE_FIRE_A/B/C/D ride joint 3
/// 'null_cap' (<see cref="FireFxScene"/>: a looping scene shown while burning; a slot). WATER while burning (behaveToWater): knocked 8 u/tick away from
/// Mario on the ground plane, +5 u, airborne, the spray cooldown set to 20 (so the next hit lands after 10 ticks, not 30); when the hit points reach 0
/// (HitPointMax 4: four drops) -> dieFire: the fire OUT (MSD_SE_EN_MOEKURI_COOL, PARTICLE_MS_MOE_FIRE_OFF -- <see cref="FireOffFxScene"/>), the
/// recover clock starts, unk150's bit 2 set / bit 1 cleared: it now ESCAPES Mario (TWalkerEnemy's Escape instead of Attack) and gives up the attack
/// (unk1A2). Out: a normal Stu -- water flips it (jitabata), a stomp kills it. RECOVER: after RecoverTimer 1000 ticks of walking (the clock counts in
/// walkBehavior) the spine resets to Wander with TNerveFireHamuKuriRecover pushed: the wait anim (13), then the RETURN anim (6), then recoverFire --
/// full hit points, the attack bit back, burning again on the next walk tick. THE COLOURS (changeTevColor): the nose's GX_TEVREG0 ramps between
/// (500, 200, 120) burning and (-20, -90, -140) out, the top's and feet's between (500, -40, -70) and (80, 5, -50), over 30 ticks (unk218) each way;
/// the trousers are fixed (200, 200, 200) / (0, 90, 0) / (200, 160, 130) -- GREEN dots -- and the top has no goop stage (KColor alpha 0). The
/// materials are per-instance copies of the Stu's four, re-registered each tick the ramp moves. .prm: 4 HP, jump 5, turn 3..5, march 1..1.5,
/// sight 1000 u / 200 of height / 120 deg / aware 300, give-up height 400, AttackWait 300, damage 80 x 90, attack 70 x 70, GenItemRate 50 + 10,
/// BoundNum 3, VelocityRate 0.5, WaitTime 300, StampRange 5.
/// NOT PORTED (logged): the MOE_FIRE flames themselves (the four JPA emitters -- the slot takes any looping scene; the same JPA pipeline as the
/// puffs makes them), the sounds (slots), HIT_MESSAGE_UNKB.
/// </summary>
[GlobalClass]
public partial class SmsFireHamukuri : SmsHamukuri
{
    // ------------------------------------------------------------------------------------------------------------------ firehamukuri.prm
    public class FireParams : HamukuriParams { public int RecoverTimer = 500; }
    /// <summary>The shipped /enemy/firehamukuri.prm (params.szs on his disc): the Stu's numbers where the file does not differ.</summary>
    public static readonly FireParams FirePrm = new FireParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 50f, ClipRadius = 100f, HitPointMax = 4, InstanceNum = 100, ActiveEnemyNum = 10,
        JumpForce = 5f, SearchLength = 1000f, SearchHeight = 200f, SearchAware = 300f, SearchAngle = 120f, GiveUpLength = 1500f, GiveUpHeight = 400f,
        AttackWait = 300, FreezeWait = 300, DamageRadius = 80f, DamageHeight = 90f, AttackRadius = 70f, AttackHeight = 70f,
        TurnSpeedLow = 3f, TurnSpeedHigh = 5f, BodyScaleLow = 1f, BodyScaleHigh = 1f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 300,
        StampRange = 5f, ZigzagCycle = 100f, ZigzagAngle = 10f, MarchSpeedLow = 1f, MarchSpeedHigh = 1.5f,
        WaterCoeff = 30f, WaterAttackCoeff = 30f, FirstVelocityY = 5f, VelocityRate = 0.5f, FirstKickVelocityY = 2f, CanSearchDist = 800f,
        BoundNum = 3, SearchActorTimer = 500, JitabataTimer = 200, FlyTimer = 500, TrampleBonusNum = 3, CrashBonusNum = 3, SerialCrashFrame = 15, KyoroTimer = 500,
        RecoverTimer = 1000,
    };
    protected override HamukuriParams HPrm => FirePrm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>The flames while burning: PARTICLE_MS_MOE_FIRE_A/B/C/D, emitAndBindToMtxPtr on joint 'null_cap' every frame while burning (the manager
    /// keeps one of each alive as long as it is asked for), setGlobalScale(the body scale) -- StuFire.tscn (gen_fx125h.py from the disc's .jpa), an
    /// SmsNpcEffect spawned on the joint when the fire lights (Spawn: the joint's rotation + translation, as setGlobalRTMatrix's normalised matrix)
    /// and stopped (drained) when it goes out. Any other scene is kept as a plain child of the joint and shown while burning. Unset = no flames (the
    /// colours still ramp). The scene also sits in FireHamukuri.tscn under Model/Node/Skeleton3D/FirePreview as the EDITOR's preview; the runtime frees it.</summary>
    [Export] public PackedScene FireFxScene;
    /// <summary>The fire going out (PARTICLE_MS_MOE_FIRE_OFF bound to the joint for its 60 frames): StuFireOff.tscn, an SmsNpcEffect spawned on the joint;
    /// any other scene fires as a one-shot burst at the joint's point. Unset = nothing.</summary>
    [Export] public PackedScene FireOffFxScene;
    public const string FirePreviewNode = "FirePreview";
    /// <summary>MSD_SE_EN_MOEKURI_COOL (the fire out); MSD_SE_EN_MOEKURI_FLAME loops while burning. Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D CoolSound, FlameSound;
    /// <summary>HIT_MESSAGE_BURN: the method on Mario that sets him on fire (called with the attacker's position) when it exists; else his plain damage.</summary>
    [Export] public string SmsBurnMethod = "SmsBurnByEnemy";
    public const string FireBone = "null_cap";                  // unk1AC = 3

    // the game's registers (GXColorS10): the nose and the others, burning (Start) and out (End)
    private static readonly Vector3 NoseStart = new Vector3(500f, 200f, 120f) / 255f, NoseEnd = new Vector3(-20f, -90f, -140f) / 255f;
    private static readonly Vector3 OtherStart = new Vector3(500f, -40f, -70f) / 255f, OtherEnd = new Vector3(80f, 5f, -50f) / 255f;
    private static readonly Vector3 TrouserC0 = new Vector3(200f, 200f, 200f) / 255f, TrouserC1 = new Vector3(0f, 90f, 0f) / 255f, TrouserC2 = new Vector3(200f, 160f, 130f) / 255f;

    // ------------------------------------------------------------------------------------------------------------------ state
    private bool _burning;                    // unk210
    private int _recover;                     // unk214: 0 = burning; counts up from 1 while out
    private int _ramp = 30, _rampApplied = -1;// unk218: 0 burning .. 30 out
    private ShaderMaterial _noseMat, _topMat;
    private Node3D _fireFx;                   // a non-SmsNpcEffect FireFxScene: a child of the joint, shown while burning
    private SmsNpcEffect _fire;               // the live StuFire effect (null when out)
    private Skeleton3D _skel;
    private readonly System.Collections.Generic.Dictionary<string, BoneAttachment3D> _fxCache = new();
    /// <summary>For the drives: the flames are live; the editor's preview node was in the scene.</summary>
    public bool FireFxEmitting => _fire != null && IsInstanceValid(_fire) && _fire.IsEmitting;
    public bool FirePreviewSeen { get; private set; }
    private const int NerveRecover = NerveSpecies + 20;
    protected override string NerveName(int n) => n == NerveRecover ? "Recover" : base.NerveName(n);

    public bool Burning => _burning;
    public int RecoverTicks => _recover;
    public int Ramp => _ramp;
    public Vector3 NoseColor => _noseMat != null ? _noseMat.GetShaderParameter("c0").As<Vector3>() : Vector3.Zero;
    public new string DebugState => $"{base.DebugState} burning {_burning} recover {_recover} ramp {_ramp} flags {_flags150}";

    // ------------------------------------------------------------------------------------------------------------------ setup
    /// <summary>TFireHamuKuri::init + the manager's initSetEnemies: the four body materials become this instance's own with the fire registers; the flames parked on the joint.</summary>
    protected override void Init()
    {
        base.Init();
        if (_model == null) return;
        foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (n is not MeshInstance3D mi || mi.Mesh == null) continue;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                string sn = (mi.Mesh as ArrayMesh)?.SurfaceGetName(i) ?? "";
                if (mi.GetActiveMaterial(i) is not ShaderMaterial src) continue;
                var own = (ShaderMaterial)src.Duplicate();
                mi.SetSurfaceOverrideMaterial(i, own);
                switch (sn)
                {
                    case "_mat_nose1": _noseMat = own; break;
                    case "_mat_body_top1": _topMat = own; own.SetShaderParameter("dirt", 0f); break;
                    case "_mat_Rfoot1": case "_mat_Lfoot1": own.SetShaderParameter("c0", OtherStart); break;   // both feet share the register; collected below
                    case "_mat_body_bottom1": own.SetShaderParameter("c0", TrouserC0); own.SetShaderParameter("c1", TrouserC1); own.SetShaderParameter("c2", TrouserC2); break;
                }
            }
            break;
        }
        _feet.Clear();
        foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            if (n is MeshInstance3D mi && mi.Mesh != null)
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    string sn = (mi.Mesh as ArrayMesh)?.SurfaceGetName(i) ?? "";
                    if ((sn == "_mat_Rfoot1" || sn == "_mat_Lfoot1") && mi.GetActiveMaterial(i) is ShaderMaterial f) _feet.Add(f);
                }
        foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        // the editor's preview of the flame (FireHamukuri.tscn): the game's emitters are spawned on the joint as the fire lights
        foreach (Node n in _model.FindChildren(FirePreviewNode, "", true, false)) { FirePreviewSeen = true; n.QueueFree(); }
        if (FireFxScene != null && _skel != null && FireFxScene.Instantiate() is Node3D probe)
        {
            if (probe is not SmsNpcEffect && _skel.FindBone(FireBone) >= 0)
            {   // a plain scene on the slot: a child of the joint, shown while burning
                var att = new BoneAttachment3D { Name = "FireAttach", BoneName = FireBone };
                _skel.AddChild(att); att.BoneName = FireBone;
                _fireFx = probe; _fireFx.Name = "Fire";
                att.AddChild(_fireFx);
                _fireFx.Visible = false;
            }
            else probe.Free();                                  // an SmsNpcEffect: spawned on the joint as the fire lights
        }
    }
    private readonly System.Collections.Generic.List<ShaderMaterial> _feet = new();

    /// <summary>TFireHamuKuri::reset: THamuKuri::reset + recoverFire.</summary>
    protected override void Reset()
    {
        base.Reset();
        RecoverFire();
    }
    /// <summary>recoverFire: full hit points, the attack bit (unk150 |= 1, &amp;= ~2), the clock and the fire cleared (the next walk tick lights it).</summary>
    private void RecoverFire()
    {
        _hp = HPrm.HitPointMax;
        _flags150 = (_flags150 & ~2u) | 1u;
        _recover = 0;
        SetBurning(false);
    }
    /// <summary>dieFire: out -- the give-up (unk1A2), the escape bit (unk150 |= 2, &amp;= ~1), the clock at 1, the sound and the OFF puff.</summary>
    private void DieFire()
    {
        SetBurning(false);
        _giveUpAttack = true;
        _flags150 = (_flags150 | 2u) & ~1u;
        _recover = 1;
        if (CoolSound != null && CoolSound.Stream != null) CoolSound.Play();
        if (FireOffFxScene == null) return;
        if (_skel != null && FireOffFxScene.Instantiate() is Node3D probe)
        {   // an SmsNpcEffect: bound to the joint for its 60 frames, as the game's emitAndBindToMtxPtr
            bool npcFx = probe is SmsNpcEffect;
            probe.Free();
            if (npcFx && SmsNpcEffect.Spawn(FireOffFxScene, _skel, FireBone, _bodyScale, _fxCache, Name) != null) return;
        }
        Vector3 at = JointPoint();
        SpawnBurst(FireOffFxScene, new Transform3D(Basis.FromScale(Vector3.One * _bodyScale), at));
    }
    /// <summary>The null_cap joint's point in the world (the emitters' bind), else the head's height.</summary>
    private Vector3 JointPoint()
    {
        int b = _skel != null ? _skel.FindBone(FireBone) : -1;
        return b >= 0 ? (_skel.GlobalTransform * _skel.GetBoneGlobalPose(b)).Origin : GlobalPosition + Vector3.Up * (100f * _bodyScale / U);
    }
    /// <summary>LIVE_FLAG_DEAD: a dead body is not performing, so the game stops asking for its emitters -- the flames drain (the state keeps unk210; reset
    /// settles it).</summary>
    protected override void Dead()
    {
        base.Dead();
        if (_fire != null) { if (IsInstanceValid(_fire)) _fire.Stop(); _fire = null; }
    }
    /// <summary>The fire on or off: the StuFire effect spawned on the joint (the game re-asks for its four emitters every burning frame) or stopped
    /// (its live particles drain, as the invalidated emitters' do); a plain slot scene shown / hidden.</summary>
    private void SetBurning(bool on)
    {
        _burning = on;
        if (_fireFx != null) _fireFx.Visible = on;
        if (on)
        {
            if ((_fire == null || !IsInstanceValid(_fire) || !_fire.IsEmitting) && FireFxScene != null && _fireFx == null && _skel != null && IsInsideTree())
                _fire = SmsNpcEffect.Spawn(FireFxScene, _skel, FireBone, _bodyScale, _fxCache, Name);
        }
        else if (_fire != null)
        {
            if (IsInstanceValid(_fire)) _fire.Stop();
            _fire = null;
        }
        if (FlameSound != null && FlameSound.Stream != null) { if (on && !FlameSound.Playing) FlameSound.Play(); else if (!on && FlameSound.Playing) FlameSound.Stop(); }
    }

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    /// <summary>TFireHamuKuri::moveObject's tail: changeTevColor -- the ramp a step toward 0 (burning) or 30 (out), the registers re-set when it moved.</summary>
    protected override void MoveObjectPre()
    {
        base.MoveObjectPre();
        if (!_burning) { if (_ramp < 30) _ramp++; }
        else if (_ramp > 0) _ramp--;
        if (_ramp != _rampApplied)
        {
            _rampApplied = _ramp;
            float t = _ramp / 30f;
            Vector3 nose = NoseStart + (NoseEnd - NoseStart) * t, other = OtherStart + (OtherEnd - OtherStart) * t;
            _noseMat?.SetShaderParameter("c0", nose);
            _topMat?.SetShaderParameter("c0", other);
            foreach (var f in _feet) f.SetShaderParameter("c0", other);
        }
    }

    /// <summary>TFireHamuKuri::walkBehavior: the walk (not during the look-around), the recover clock, the lighting, the recover at RecoverTimer.</summary>
    protected override void WalkBehavior(int mode, float k)
    {
        base.WalkBehavior(mode, k);                              // the Stu's (which stands still through the look-around)
        if (_recover > 0) _recover++;
        if (_recover == 0 && !_burning) SetBurning(true);
        if (_recover > FirePrm.RecoverTimer)
        {
            ResetSpine();
            SetNext(NerveGraphWander);
            PushNerve(NerveRecover);
            SetBck(BckWait);
        }
    }

    /// <summary>TFireHamuKuri::behaveToWater: burning -> knocked 8 u/tick away on the ground plane (+5 u, airborne), out at 0 HP, the cooldown 20; out -> the Stu's.</summary>
    protected override void BehaveToWater()
    {
        if (_burning)
        {
            Vector3 d = new Vector3(Pos().X - MarioPos().X, 0f, Pos().Z - MarioPos().Z);
            if (d == Vector3.Zero) d.X += 1f;
            _vel = d.Normalized() * 8f;
            _airborne = true;
            GlobalPosition += Vector3.Up * (5f / U);
            if (_hp == 0) DieFire();
            _sprayCooldown = 20;
            return;
        }
        base.BehaveToWater();
    }
    /// <summary>TFireHamuKuri::isHitValid: burning -> no (he bounces, his feet burn); out -> the Stu's.</summary>
    protected override bool IsHitValid(HitMessage msg) => !_burning && base.IsHitValid(msg);
    /// <summary>TFireHamuKuri::sendAttackMsgToMario: HIT_MESSAGE_BURN while burning (Mario's burn method if he has one), else ATTACK.</summary>
    protected override void SendAttackMsgToMario()
    {
        if (_burning && _mario != null && !string.IsNullOrEmpty(SmsBurnMethod) && _mario.HasMethod(SmsBurnMethod)) { _mario.Call(SmsBurnMethod, GlobalPosition); return; }
        base.SendAttackMsgToMario();
    }

    protected override bool ExecuteNerve(int n)
    {
        if (n == NerveRecover)                                   // TNerveFireHamuKuriRecover
        {
            if (BckEnded())
            {
                if (IsBck(BckReturn)) { RecoverFire(); return true; }
                SetBck(BckReturn);
            }
            return false;
        }
        return base.ExecuteNerve(n);
    }
}
