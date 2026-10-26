using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The Gooble -- TNameKuri (Enemy/namekuri.cpp, the Swoopin' Stu: a goop blob that stalks Mario and leaps at him) as a SPECIES on
/// <see cref="SmsSmallEnemy"/> (SMS pass 125e, feature/enemy-base). Its stand-alone port of the whole stack (passes 56-99) is gone: the base is
/// the one body of TSmallEnemy / TWalkerEnemy / Spine code every small enemy shares, and this file is only what namekuri.cpp adds to it, with
/// the shipped /enemy/namekuri.prm (SmsParams.NameKuri -> <see cref="Prm"/>). ONE TICK = one 120 Hz movement tick; the .prm speeds are units a tick.
///
/// WHAT THE BASE GIVES IT (TWalkerEnemy / TSmallEnemy): Generate (the set anim) -> GraphWander (the walk at 0.2 deg a tick; no graph here: random
/// points within WanderRadiusUnits) -> isFindMario (800 u all round, 300 u of height, he is not in water) -> the hop over a wall while walking
/// (JumpForce 2.2 x scale, +5 deg), the collision lists every FOURTH tick (TMarDirector::direct), Mario's TRAMPLE / HIP_DROP -> isHitValid -> kill
/// -> Die (the down anim; the death STAMPS goop -- unk150 & 0x20, StampRange 15 x 32 u; the body gone once the anim ended + the manager's 60 ticks
/// (unk5C) or at 360), the other small enemies' push-apart (isCollidMove), LIVE_FLAG_DEAD -> the re-launch after RelaunchTicks (launcher.prm's
/// mSLLaunchPeriod 200), genRandomItem -> the DropTable (pass 125d) unless the death carries LIVE_FLAG_UNK20000.
///
/// WHAT IS THE GOOBLE'S OWN (TNameKuri's virtuals and nerves, namekuri.cpp): behaveToFindMario = NKFollowMario (march toward him turning 3 deg a
/// tick) -> within JumpAttackRadius 350 u in the front 180 deg -> JumpAttackPrepare (the wait anim, turning JumpAttackTurnSp 1 deg a tick until he is
/// within 30 deg) -> JumpAttack (the jump_start anim; the body pulses white (|sin(ColorChangeRate 5 x t)| x 255 on GX_TEVREG1) until frame 62, the
/// LEAP: SMSCalcJumpVelocityY to his position with vy JumpAttackSp 6 under JumpAttackGravity 0.135, +2 u, turning 6 deg a tick, the flash fading 5
/// a tick; landed and the anim done -> jump_end -> dies with LIVE_FLAG_UNK20000: no reward). attackToMario: SMS_SendMessageToMario(ATTACK) with
/// dmgnamekuri.prm (1 HP, 300 i-ticks) and a LEAPING Gooble stops and dies, no reward. behaveToWater (not in HitWaterJump / Die): Die -- melting
/// (LIVE_FLAG_MELT_ON_DEATH: the hit anim, gravity 0.01) when on the ground -- no reward. moveObject's tail: a fall past LandHeight 100 u while
/// wandering / escaping -> Land (the land anim on touchdown); MSD_SE_EN_NAMEKURI_WALK while the walk anim plays on the ground; the ALIVE clock --
/// AliveTime 2000 ticks, the body inflating from 1900 (x 1.01 a tick to twice its scale), then kill() (no reward here unless
/// <see cref="DropOnTimeOut"/>: the table is for his kills; the retail game's generateItem drops nothing in any case). getGravityY: 0.135 in the leap.
/// isHitValid: unk198 = 1 for any hit message but water -- the gate of setAfterDeadEffect: no smoke (TSmallEnemy's DISAP is overridden away);
/// killed by a hit with FLUDD's tank under half -> the stage's bottle_short (<see cref="BottleScene"/>, SmsWaterBottle) on the ground under the body.
/// reset: scale 0.6..1 (mBodyScale), the next of nameKuriTevColorData's 7 colours (GX_TEVREG0). TNameKuriLauncher::stateLaunch: resetSRTV(the
/// launcher's point, rotation (0, 0, 0), the scale, (0, 4, 0)) -- the re-launch pops up at yaw 0; Generate's set anim zeroes the velocity at once.
///
/// THE LOOK (name.bmd / brain.bmd read with tools/projectshine/bmd_mat_dump.py; unchanged from the stand-alone port): the glb's _mat_body surface
/// gets GoobleBody.gdshader (BodyMaterial, duplicated per instance: the body is 69 % the screen behind it -- TScreenTexture through the camera
/// projection -- and 31 % goop coloured by GX_TEVREG0, doubled by the stage-2 scale along K_new_name's alpha ramp, the skirt and the tufts full, the
/// dome half, plus the sun's specular, with GX_TEVREG1, the charge flash, added on top); _mat_eye gets GoobleEye.tres (K_new_name_eye cut out, lit);
/// the brain (TSharedParts on joint 2 = jnt_brain: brain.bmd with the stage's goop mark H_ma_rak swapped into K_name_dummy) hangs on the bone as
/// BrainScene with BrainMaterial; a shadow-only copy of the mesh casts the shadow the game's 影 draw buffer gives it. The lights are the materials'
/// (bianco2 scene.bin: the sun (255,255,255) from (200000, 500000, 200000), ambient (128,128,128), spec shininess 50); a DirectionalLight3D in the
/// scene replaces the sun's direction and colour. THE DEATH: setDeadAnm / setMeltAnm emit ms_deadnamekli_o (16 goop blobs) and _n (28 rainbow
/// drops) bound to joint 2's matrix at the body scale -- DeathFxScene is the SmsGoobleDeath scene (assets/Sms/Fx/GoobleDeath.tscn, gen_fx_gooble.py),
/// kept as a child and played at the brain joint in the body's upright frame (DeathFxUpright) -- NOT the base's one-shot puff (which this species'
/// setAfterDeadEffect overrides away, so the base's DeathSound slot is inert here).
///
/// THE SCENE (assets/Gooble.tscn): Model (the glb, scale 1: it is exported in metres) with Model/AnimationPlayer, the CollisionShape3D for the
/// world, and the exports set to their default resources (the bottle, the death splash, the three materials, the brain, GoobleDrops.tres) so they
/// load WITH the level (a scene loaded for the first time compiles its shaders then and there, and that belongs in the load, not in play -- pass 77).
/// The game's hit cylinders (attack 50 x 30, damage 40 x 90, x the body scale) and sight cones are the base's code.
/// NOT PORTED: the sounds without players (the exports), the oil-slip / catch kill (PUNCH), TWalker's slope blend (the Gooble leaps from any floor),
/// the pollution launchers, the Explosion / Diffuse nerves (the diffusion kind, diffusionnamekuri.prm), isCollidMove's kill of a leaping Gooble on a
/// map object, the HitWaterJump's 0.4 x 4 x 0.4 squash (NameKuriScaleCallback).
/// </summary>
[GlobalClass]
public partial class Gooble : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ namekuri.prm
    /// <summary>TNameKuriSaveLoadParams on TWalkerEnemyParams on TSmallEnemyParams; the shipped /enemy/namekuri.prm (sms_shipped_params.json), the code
    /// defaults in SmsPhysics.cs's comments where they differ.</summary>
    public class NameKuriParams : Params
    {
        public int AliveTime = 2000;
        public float LandHeight = 500f, JumpAttackGravity = 0.1f, JumpAttackSp = 20f, JumpAttackTurnSp = 2f;
        public int JumpPrepareFrame = 100;
        public float JumpMaxFrame = 30f, JumpMaxAngle = 30f, JumpMaxScale = 0.2f;
        public int ColorChangeRate = 25;
        public float JumpAttackRadius = 400f, JumpAttackAngle = 180f;
    }
    public static readonly NameKuriParams Prm = new NameKuriParams
    {
        HeadHeight = SmsParams.NameKuri.HeadHeight, BodyRadius = SmsParams.NameKuri.BodyRadius, WallRadius = SmsParams.NameKuri.WallRadius, ClipRadius = 100f,
        HitPointMax = SmsParams.NameKuri.HitPointMax, InstanceNum = 2, ActiveEnemyNum = 2,
        Gravity = SmsParams.NameKuri.Gravity, JumpForce = SmsParams.NameKuri.JumpForce,
        SearchLength = SmsParams.NameKuri.SearchLength, SearchHeight = SmsParams.NameKuri.SearchHeight, SearchAware = SmsParams.NameKuri.SearchAware, SearchAngle = SmsParams.NameKuri.SearchAngle,
        GiveUpLength = SmsParams.NameKuri.GiveUpLength, GiveUpHeight = SmsParams.NameKuri.GiveUpHeight,
        AttackWait = SmsParams.NameKuri.AttackWait, FreezeWait = SmsParams.NameKuri.FreezeWait,
        DamageRadius = SmsParams.NameKuri.DamageRadius, DamageHeight = SmsParams.NameKuri.DamageHeight, AttackRadius = SmsParams.NameKuri.AttackRadius, AttackHeight = SmsParams.NameKuri.AttackHeight,
        TurnSpeedLow = SmsParams.NameKuri.TurnSpeedLow, TurnSpeedHigh = SmsParams.NameKuri.TurnSpeedHigh,
        BodyScaleLow = SmsParams.NameKuri.BodyScaleLow, BodyScaleHigh = SmsParams.NameKuri.BodyScaleHigh,
        GenItemRate = SmsParams.NameKuri.GenItemRate, GenEggRate = SmsParams.NameKuri.GenEggRate,
        WaitTime = SmsParams.NameKuri.WaitTime, StampRange = SmsParams.NameKuri.StampRange,
        ZigzagCycle = SmsParams.NameKuri.ZigzagCycle, ZigzagAngle = SmsParams.NameKuri.ZigzagAngle,
        MarchSpeedLow = SmsParams.NameKuri.MarchSpeedLow, MarchSpeedHigh = SmsParams.NameKuri.MarchSpeedHigh,
        AliveTime = SmsParams.NameKuri.AliveTime, LandHeight = SmsParams.NameKuri.LandHeight,
        JumpAttackGravity = SmsParams.NameKuri.JumpAttackGravity, JumpAttackSp = SmsParams.NameKuri.JumpAttackSp, JumpAttackTurnSp = SmsParams.NameKuri.JumpAttackTurnSp,
        JumpPrepareFrame = SmsParams.NameKuri.JumpPrepareFrame, JumpMaxFrame = SmsParams.NameKuri.JumpMaxFrame, JumpMaxAngle = SmsParams.NameKuri.JumpMaxAngle, JumpMaxScale = SmsParams.NameKuri.JumpMaxScale,
        ColorChangeRate = SmsParams.NameKuri.ColorChangeRate, JumpAttackRadius = SmsParams.NameKuri.JumpAttackRadius, JumpAttackAngle = SmsParams.NameKuri.JumpAttackAngle,
    };
    protected override Params P => Prm;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    /// <summary>TNameKuri::setAfterDeadEffect (namekuri.cpp 511): the water bottle a stomped Gooble leaves when Mario's tank is under half --
    /// assets/Sms/Items/WaterBottle.tscn (SmsWaterBottle: bottle_short, half a tank) unless another is set. Resolved at _Ready (not at the death): the
    /// scene brings GetWater.tscn and its shaders in with the level (see the header).</summary>
    [Export] public PackedScene BottleScene;
    public const string BottleScenePath = "res://assets/Sms/Items/WaterBottle.tscn";
    /// <summary>The ALIVE clock's death (2000 ticks) also rolls the DropTable. Off (default): a Gooble that times out drops nothing -- the table is for
    /// his kills (the game's kill() there carries no UNK20000, so its generateItem would roll too; the retail game drops nothing from it anyway).</summary>
    [Export] public bool DropOnTimeOut;
    /// <summary>MSD_SE_EN_NAMEKURI_WALK (looping, while it walks), _DOWN (the death), _DOWN_WT (melting). Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D WalkSound, DownSound, MeltSound;
    /// <summary>_mat_body's material: assets/Sms/GoobleBody.tres (GoobleBody.gdshader) unless another is set; duplicated per instance,
    /// body_color (GX_TEVREG0) / flash_color (GX_TEVREG1) / sun_dir / sun_color fed each frame.</summary>
    [Export] public ShaderMaterial BodyMaterial;
    public const string BodyMaterialPath = "res://assets/Sms/GoobleBody.tres";
    /// <summary>_mat_eye's material: assets/Sms/GoobleEye.tres unless another is set (shared: nothing per instance in it).</summary>
    [Export] public Material EyeMaterial;
    public const string EyeMaterialPath = "res://assets/Sms/GoobleEye.tres";
    /// <summary>The brain (brain.bmd as a glb) hung on the jnt_brain bone, its surface in BrainMaterial (the goop mark, unlit).</summary>
    [Export] public PackedScene BrainScene;
    [Export] public Material BrainMaterial;
    public const string BrainScenePath = "res://assets/Sms/models/gooble_brain.glb";
    public const string BrainMaterialPath = "res://assets/Sms/GoobleBrain.tres";
    public const string BrainBone = "jnt_brain";
    /// <summary>The base's DeathFxScene export is, for the Gooble, the SmsGoobleDeath scene: assets/Sms/Fx/GoobleDeath.tscn unless another is set.</summary>
    public const string DeathFxScenePath = "res://assets/Sms/Fx/GoobleDeath.tscn";

    // ------------------------------------------------------------------------------------------------------------------ the game's data
    private static readonly Color[] BodyColors =
    {
        // nameKuriTevColorData (GXColorS10, 0..255; alpha 169): red, blue, yellow, green, purple, cyan, orange
        new Color(0x64 / 255f, 0x00 / 255f, 0x0A / 255f), new Color(0x00 / 255f, 0x00 / 255f, 0x64 / 255f),
        new Color(0x5D / 255f, 0x54 / 255f, 0x00 / 255f), new Color(0x00 / 255f, 0x4C / 255f, 0x10 / 255f),
        new Color(0x63 / 255f, 0x04 / 255f, 0x60 / 255f), new Color(0x00 / 255f, 0x44 / 255f, 0x75 / 255f),
        new Color(0x64 / 255f, 0x28 / 255f, 0x00 / 255f),
    };
    private static int _nextColor; // TNameKuriManager::getNextColorIdx

    // ------------------------------------------------------------------------------------------------------------------ the bck table
    // the archive's files in order = setBckAnm's indices; the .bck ANK1 durations (60 Hz) and loop attributes (wait / walk 2, the rest 0)
    private static readonly string[] Names = { "name_down", "name_hit", "name_jump_end", "name_jump_start", "name_land", "name_set", "name_wait", "name_walk" };
    private static readonly int[] Frames = { 9, 21, 5, 89, 19, 59, 120, 60 };
    private static readonly bool[] Loops = { false, false, false, false, false, false, true, true };
    private const int BckDown = 0, BckHit = 1, BckJumpEnd = 2, BckJumpStart = 3, BckLand = 4, BckSet = 5, BckWait = 6, BckWalk = 7;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves
    private const int NerveFollowMario = NerveSpecies + 0, NerveJumpAttackPrepare = NerveSpecies + 1, NerveJumpAttack = NerveSpecies + 2, NerveLand = NerveSpecies + 3;
    protected override string NerveName(int n) => n switch { NerveFollowMario => "FollowMario", NerveJumpAttackPrepare => "JumpAttackPrepare", NerveJumpAttack => "JumpAttack", NerveLand => "Land", _ => base.NerveName(n) };

    // ------------------------------------------------------------------------------------------------------------------ state
    private int _alive;                        // unk194: the alive clock
    private float _flash;                      // unk1BC.r = g = b: the charge's white pulse, 0..255
    private float _scaleKeep;                  // unk1B0
    private int _colorIdx;                     // unk1C4's index
    private bool _passed62; private float _lastFrame;
    private ShaderMaterial _bodyMat;
    private Material _eyeMat;
    private Node3D _brain;
    private SmsGoobleDeath _deathFx;
    private static Material _shadowMat;        // the shadow-only copy's opaque stand-in (shared)

    /// <summary>For the drives / logs.</summary>
    public float Flash => _flash;
    public int ColorIndex => _colorIdx;
    public int AliveTicks => _alive;
    public bool IsMelting => _melt;
    public bool HitValid => _hitValid;
    public bool NoReward => _noReward;
    public float ScaleNow => _scale;
    public SmsGoobleDeath DeathFx => _deathFx;
    public string DebugState => $"{NerveNameNow} t{NerveTime} bck {BckName} air {_airborne} hp {_hp} dead {_dead} melt {_melt} alive {_alive} scale {_scale:F3}/{_bodyScale:F3} flash {_flash:F0} yaw {_yaw:F0} vel {_vel}";

    // ------------------------------------------------------------------------------------------------------------------ setup
    /// <summary>TNameKuri::init: unk150 = 0x3A (0x20: the death stamps goop; the escape bit 2 is moot -- behaveToFindMario is its own), the look, the
    /// death splash as a child, the bottle scene resolved now so its shaders compile with the level.</summary>
    protected override void Init()
    {
        _flags150 = 0x3A;
        SetupLook();
        SetupDeathFx();
        BottleScene ??= GD.Load<PackedScene>(BottleScenePath);
        if (BottleScene == null)
            GD.PushWarning($"[SMS gooble] {GetPath()}: {BottleScenePath} did not load: no water bottle drop");
    }

    /// <summary>TNameKuri::reset over TWalkerEnemy::reset: the alive clock, the flash and the scale keep cleared, the next colour (TNameKuriManager::
    /// getNextColorIdx) before the base resets the body and applies the look.</summary>
    protected override void Reset()
    {
        _alive = 0; _flash = 0f; _scaleKeep = 1f; _passed62 = false; _lastFrame = 0f; _leapHit = false;
        _colorIdx = _nextColor; _nextColor = (_nextColor + 1) % BodyColors.Length;
        base.Reset();
    }

    private static DirectionalLight3D FindSun(Node n)
    {
        if (n == null) return null;
        if (n is DirectionalLight3D d) return d;
        foreach (Node c in n.GetChildren())
        {
            var r = FindSun(c);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>The model's surfaces by the glb's material names: "_mat_body" (name.bmd's material 0) takes a per-instance copy of
    /// GoobleBody.tres, "_mat_eye" (material 1) the shared GoobleEye.tres; the brain scene goes on the jnt_brain bone (TSharedParts(this,
    /// 2, brainModel, 3)); a shadow-only copy of the mesh casts the shadow; the scene's DirectionalLight3D is the sun of both
    /// materials (set once: it does not move).</summary>
    private void SetupLook()
    {
        if (_model == null)
            return;
        MeshInstance3D mesh = null; int bodySurf = -1, eyeSurf = -1;
        foreach (MeshInstance3D mi in FindMeshes(_model))
        {
            if (mi.Mesh == null || mi.Mesh.GetSurfaceCount() == 0) continue;
            mesh = mi;
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                string name = mi.GetActiveMaterial(s)?.ResourceName.ToLowerInvariant() ?? "";
                if (name.Contains("body")) bodySurf = s;
                else if (name.Contains("eye")) eyeSurf = s;
            }
            break;
        }
        if (mesh == null)
        {
            GD.PushWarning($"[SMS gooble] {GetPath()}: no mesh under Model: no goop look");
            return;
        }
        if (bodySurf < 0) bodySurf = 0;                               // one unnamed surface: the body
        var sun = FindSun(GetTree().CurrentScene);
        ShaderMaterial body = BodyMaterial ?? GD.Load<ShaderMaterial>(BodyMaterialPath);
        if (body == null)
            GD.PushWarning($"[SMS gooble] {GetPath()}: {BodyMaterialPath} did not load: no goop body");
        else
        {
            _bodyMat = (ShaderMaterial)body.Duplicate();
            SetSun(_bodyMat, sun);
            mesh.SetSurfaceOverrideMaterial(bodySurf, _bodyMat);
        }
        if (eyeSurf >= 0)
        {
            _eyeMat = EyeMaterial ?? GD.Load<Material>(EyeMaterialPath);
            if (_eyeMat == null)
                GD.PushWarning($"[SMS gooble] {GetPath()}: {EyeMaterialPath} did not load: the glb's eye material stays");
            else
            {
                if (_eyeMat is ShaderMaterial em) SetSun(em, sun);
                mesh.SetSurfaceOverrideMaterial(eyeSurf, _eyeMat);
            }
        }
        // the shadow: the body reads the screen (the transparent pass casts nothing), so a copy of the mesh casts it (the game draws the
        // Gooble into the インダイレクト影 buffer)
        _shadowMat ??= new StandardMaterial3D { ResourceName = "Gooble shadow caster" };
        var caster = new MeshInstance3D { Name = "ShadowCaster", Mesh = mesh.Mesh, Skin = mesh.Skin, Skeleton = mesh.Skeleton,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly, Transform = mesh.Transform };
        for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
            caster.SetSurfaceOverrideMaterial(s, _shadowMat);
        mesh.AddSibling(caster);
        // the brain on the jnt_brain bone
        var skel = FindSkeleton(_model);
        if (skel == null || skel.FindBone(BrainBone) < 0)
        {
            GD.PushWarning($"[SMS gooble] {GetPath()}: no Skeleton3D with the bone {BrainBone} under Model: no brain");
            return;
        }
        PackedScene brainScene = BrainScene ?? GD.Load<PackedScene>(BrainScenePath);
        Material brainMat = BrainMaterial ?? GD.Load<Material>(BrainMaterialPath);
        if (brainScene == null || brainMat == null)
        {
            GD.PushWarning($"[SMS gooble] {GetPath()}: {BrainScenePath} / {BrainMaterialPath} did not load: no brain");
            return;
        }
        var att = new BoneAttachment3D { Name = "BrainAttach" };
        skel.AddChild(att);
        att.BoneName = BrainBone;
        _brain = brainScene.Instantiate<Node3D>();
        _brain.Name = "Brain";
        att.AddChild(_brain);
        foreach (MeshInstance3D mi in FindMeshes(_brain))
            for (int s = 0; s < (mi.Mesh?.GetSurfaceCount() ?? 0); s++)
                mi.SetSurfaceOverrideMaterial(s, brainMat);
    }

    /// <summary>GoobleDeath.tscn as a child (top-level: the splash stays where the body died).</summary>
    private void SetupDeathFx()
    {
        PackedScene scene = DeathFxScene ?? GD.Load<PackedScene>(DeathFxScenePath);
        if (scene == null || scene.Instantiate() is not SmsGoobleDeath fx)
        {
            GD.PushWarning($"[SMS gooble] {GetPath()}: {DeathFxScenePath} did not load as SmsGoobleDeath: no death splash");
            return;
        }
        fx.Name = "DeathFx";
        _deathFx = fx;
        AddChild(fx);
    }

    /// <summary>emitAndBindToMtxPtr(PARTICLE_MS_DEADNAMEKLI_O / _N, getAnmMtx(2)) + setGlobalScale(mScaling): the splash at the brain
    /// joint, at the body scale, in the body's upright frame (joint 1 'center': the heading and the ground tilt, as TNameIndParCallback
    /// binds the Gooble's other emitter) -- DEVIATION DeathFxUpright: the code binds to joint 2, whose matrix is rotated (90, 0.5, 90)
    /// (X up, Y forward), which would fan the goop sideways along the heading; he knows the game's splash goes up toward the sky.</summary>
    public const bool DeathFxUpright = true;
    private void PlayDeathFx()
    {
        if (_deathFx == null) return;
        Transform3D model = _model != null ? _model.GlobalTransform : GlobalTransform;
        Vector3 origin = _brain != null && IsInstanceValid(_brain) ? _brain.GlobalPosition : model.Origin + model.Basis.Y.Normalized() * (BrainRestHeightUnits * _scale / U);
        Transform3D frame = DeathFxUpright || _brain == null || !IsInstanceValid(_brain) ? new Transform3D(model.Basis, origin) : _brain.GlobalTransform;
        _deathFx.Play(frame, _scale);
    }
    private const float BrainRestHeightUnits = 62f - 5.92f;     // JNT1: center (0, 62, 0) + jnt_brain (0, -5.92, 0.43)

    /// <summary>The materials' sun (their default is the game's "太陽（オブジェクト）") replaced by the scene's DirectionalLight3D: its
    /// +Z basis points toward the light, its colour x energy is the light colour.</summary>
    private static void SetSun(ShaderMaterial m, DirectionalLight3D sun)
    {
        if (sun == null) return;
        m.SetShaderParameter("sun_dir", sun.GlobalTransform.Basis.Z.Normalized());
        Color c = sun.LightColor * sun.LightEnergy;
        m.SetShaderParameter("sun_color", new Vector3(c.R, c.G, c.B));
    }

    private static Skeleton3D FindSkeleton(Node n)
    {
        if (n is Skeleton3D s) return s;
        foreach (Node c in n.GetChildren())
        {
            var r = FindSkeleton(c);
            if (r != null) return r;
        }
        return null;
    }

    private static IEnumerable<MeshInstance3D> FindMeshes(Node n)
    {
        if (n is MeshInstance3D mi) yield return mi;
        foreach (Node c in n.GetChildren())
            foreach (MeshInstance3D m in FindMeshes(c))
                yield return m;
    }

    // ------------------------------------------------------------------------------------------------------------------ the look
    /// <summary>TNameKuri::calcRootMatrix is the base's (the floor normal up, the heading, the body scale); the anim's colour registers on the material.</summary>
    protected override void ApplyLook()
    {
        base.ApplyLook();
        if (_bodyMat != null)
        {
            Color c = BodyColors[_colorIdx];
            _bodyMat.SetShaderParameter("body_color", new Vector3(c.R, c.G, c.B));            // GX_TEVREG0, the raw register value
            float f = _flash / 255f;
            _bodyMat.SetShaderParameter("flash_color", new Vector3(f, f, f));                 // GX_TEVREG1
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ anims (TNameKuri's setters)
    protected override void SetGenerateAnm() { SetBck(BckSet); _vel = Vector3.Zero; }
    protected override void SetWalkAnm() => SetBck(BckWalk);
    protected override void SetWaitAnm() => SetBck(BckWait);
    /// <summary>setDeadAnm: the down anim, MSD_SE_EN_NAMEKURI_DOWN, ms_deadnamekli_o / n on joint 2, still, LIVE_FLAG_UNK10 (no bind).</summary>
    protected override void SetDeadAnm()
    {
        SetBck(BckDown);
        if (DownSound != null && DownSound.Stream != null) DownSound.Play();
        PlayDeathFx();
        _vel = Vector3.Zero; _lin = Vector3.Zero; _noBind = true;
    }
    /// <summary>setMeltAnm: the hit anim, ms_deadnamekli_o / n on joint 2, still, LIVE_FLAG_UNK10, MSD_SE_EN_NAMEKURI_DOWN_WT.</summary>
    protected override void SetMeltAnm()
    {
        SetBck(BckHit);
        PlayDeathFx();
        _vel = Vector3.Zero; _lin = Vector3.Zero; _noBind = true;
        if (MeltSound != null && MeltSound.Stream != null) MeltSound.Play();
    }

    /// <summary>getFrameCtrl(BCK)->checkPass(f): the clip crossed frame f this tick.</summary>
    private bool BckPassed(float f)
    {
        float now = BckFrame();
        bool pass = _lastFrame < f && now >= f;
        _lastFrame = now;
        return pass;
    }

    // ------------------------------------------------------------------------------------------------------------------ the hooks
    /// <summary>TNameKuri::getGravityY: 0.01 melting, JumpAttackGravity in the leap, else TLiveActor's mGravity.</summary>
    protected override float GravityY()
    {
        if (_melt) return 0.01f;
        if (IsNerve(NerveJumpAttack)) return Prm.JumpAttackGravity;
        return Prm.Gravity;
    }
    protected override bool WallHopAllowed() => !IsNerve(NerveJumpAttack) && !IsNerve(NerveJumpAttackPrepare);

    /// <summary>TNameKuri::moveObject's tail (after TWalkerEnemy::moveObject): a fall past LandHeight while wandering / escaping -> Land; the walk
    /// sound; the alive clock -- the inflation over the last 100 ticks, then kill() (TNameKuri::setBehavior itself is empty).</summary>
    protected override void SetBehavior()
    {
        if (_vel.Y < 0f && _airborne && (IsNerve(NerveGraphWander) || IsNerve(NerveEscape)) && Pos().Y - _groundY > Prm.LandHeight)
            PushNerve(NerveLand);
        if (WalkSound != null)
        {
            bool walking = !_airborne && IsBck(BckWalk);
            if (walking && !WalkSound.Playing) WalkSound.Play();
            else if (!walking && WalkSound.Playing) WalkSound.Stop();
        }
        if (_model == null || _model.Visible)                    // !LIVE_FLAG_HIDDEN
        {
            _alive++;
            if (_alive + 100 > Prm.AliveTime && _scale < _bodyScale * 2f) _scale *= 1.01f;
            if (_alive > Prm.AliveTime)
            {
                if (!DropOnTimeOut) _noReward = true;
                Kill();
            }
        }
    }

    /// <summary>TNameKuri::behaveToFindMario: Wander after FollowMario -- no hop, no Attack run.</summary>
    protected override void BehaveToFindMario()
    {
        PushAfterCurrent(NerveGraphWander);
        PushAfterCurrent(NerveFollowMario);
    }
    protected override Mario.SmsDamageProfile DamageProfile => Mario.SmsDamageProfile.Namekuri;
    /// <summary>TNameKuri::attackToMario: SMS_SendMessageToMario(ATTACK) (DmgNamekuri: 1 HP, 300 i-ticks; his TakeDamage keeps the invincibility),
    /// and a leaping Gooble stops and dies on the hit, no reward.</summary>
    protected override void AttackToMario()
    {
        SendAttackMsgToMario();
        if (IsNerve(NerveJumpAttack))
        {
            _leapHit = true;
            _vel = Vector3.Zero;
            _noReward = true;                                    // LIVE_FLAG_UNK20000
            Kill();
        }
    }
    private bool _leapHit;
    /// <summary>For the drives: this life's leap hit him (attackToMario in JumpAttack).</summary>
    public bool LeapHitMario => _leapHit;
    /// <summary>TNameKuri::behaveToWater: not in HitWaterJump / Die (/ Explosion) -> Die, melting when on the ground, no reward.</summary>
    protected override void BehaveToWater()
    {
        if (IsNerve(NerveHitWaterJump) || IsNerve(NerveDie)) return;
        PushNerve(NerveDie);
        if (!_airborne) _melt = true;                            // LIVE_FLAG_MELT_ON_DEATH
        _noReward = true;                                        // LIVE_FLAG_UNK20000
    }
    /// <summary>TNameKuri::isHitValid: unk198 = 1 for any hit message but SPRAYED_BY_WATER (which never asks) -- the bottle's gate; a hidden one takes nothing.</summary>
    protected override bool IsHitValid(HitMessage msg)
    {
        _hitValid = true;
        return base.IsHitValid(msg);
    }

    /// <summary>TNameKuriLauncher::stateLaunch: resetSRTV(the launcher's point, rotation (0, 0, 0), the scale, (0, 4, 0) in the launcher's upright frame)
    /// -- the body pops up out of the goop into Generate, whose set anim zeroes the velocity at once (as the game's does); yaw 0 is the reset rotation.</summary>
    protected override void LaunchVelocity()
    {
        _vel = new Vector3(0f, 4f, 0f);
        _airborne = true;                                        // LIVE_FLAG_AIRBORNE: bind drops it onto the floor
        _yaw = 0f;
        if (DebugPrint) GD.Print($"[SMS gooble] {Name}: re-launched (colour {_colorIdx}, scale {_bodyScale:F2}, march {_marchSpeed:F2})");
    }

    /// <summary>LIVE_FLAG_DEAD: the base's, and the walk sound stops.</summary>
    protected override void Dead()
    {
        base.Dead();
        if (WalkSound != null && WalkSound.Playing) WalkSound.Stop();
    }

    /// <summary>TNameKuri::setAfterDeadEffect (namekuri.cpp 511; no disappearance smoke -- TSmallEnemy's is overridden away): killed by a
    /// hit (unk198) with SMS_GetMarioWaterGun()->getCurrentWater() x 2 &lt; getMaxWater() -> gpItemManager->makeObjAppear(mPosition,
    /// ACTOR_TYPE_BOTTLE_SHORT, true): the stage's one bottle_short put on the ground found from 5 units over the body (checkGround; illegal ground or
    /// the bottle already out -> nothing).</summary>
    protected override void SetAfterDeadEffect()
    {
        if (!_hitValid || _mario == null) return;
        if (_mario.SmsWaterNow * 2 >= _mario.SmsWaterMax) return;
        if (!SmsWaterBottle.PoolFree) return;
        var space = GetWorld3D().DirectSpaceState;
        Vector3 from = GlobalPosition + Vector3.Up * (5f / U);
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 200f, CollisionMask);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = space.IntersectRay(q);
        if (hit.Count == 0) return;                               // BG_CHECK_FLAG_ILLEGAL: no bottle
        var bottle = SmsWaterBottle.Spawn(BottleScene, GetParent(), (Vector3)hit["position"]);  // loaded at _Ready
        if (DebugPrint) GD.Print($"[SMS gooble] {Name}: the water bottle {(bottle != null ? "dropped at " + bottle.GlobalPosition : "did not load")} (water {_mario.SmsWaterNow}/{_mario.SmsWaterMax})");
    }

    /// <summary>SMSCalcJumpVelocityY(target, pos, vy, gravity, min -40): count the ticks until a body thrown up at vy under gravity falls
    /// back to the target's height, then the horizontal velocity that crosses the gap in that many ticks.</summary>
    private Vector3 CalcVelocityToJumpToY(Vector3 target, float vy, float gravity)
    {
        float y = Pos().Y, ySpeed = vy;
        int ticks = 1;
        for (int guard = 0; guard < 100000; guard++)
        {
            y += ySpeed;
            if (ySpeed < 0f && y <= target.Y) break;
            ySpeed -= gravity;
            if (ySpeed < -40f) ySpeed = -40f;
            ticks++;
        }
        float f = 1f / ticks;
        return new Vector3(f * (target.X - Pos().X), vy, f * (target.Z - Pos().Z));
    }

    // ------------------------------------------------------------------------------------------------------------------ the species' nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveFollowMario:                               // TNerveNKFollowMario
                if (_time == 0) SetGoalMario();
                WalkToCurPathNode(_marchSpeed, 3f, 0f);
                if (IsInSight(MarioPos(), Prm.JumpAttackRadius, Prm.JumpAttackAngle, 0f))
                {
                    PushAfterCurrent(NerveJumpAttackPrepare);
                    return true;
                }
                return false;

            case NerveJumpAttackPrepare:                         // TNerveNameKuriJumpAttackPrepare
                if (_time == 1) { SetGoalMario(); SetWaitAnm(); }
                else if (IsBck(BckWait))
                {
                    if (IsInSight(MarioPos(), 100000f, 30f, 0f))
                    {
                        if (!_airborne) { PushAfterCurrent(NerveJumpAttack); return true; } // (TWalker's wall-attach / slope blend not ported)
                    }
                    else
                        WalkToCurPathNode(0f, Prm.JumpAttackTurnSp, 0f);
                }
                return false;

            case NerveJumpAttack:                                // TNerveNameKuriJumpAttack
                if (_time <= 1)
                {
                    SetGoalMario();
                    _scaleKeep = _scale; _passed62 = false; _lastFrame = 0f;
                }
                else if (IsBck(BckWait))
                    SetBck(BckJumpStart);
                else if (IsBck(BckJumpStart))
                {
                    if (!_passed62 && BckPassed(62f))
                    {
                        _passed62 = true;
                        Vector3 mp = MarioPos();
                        _vel = CalcVelocityToJumpToY(mp, Prm.JumpAttackSp, Prm.JumpAttackGravity);
                        GlobalPosition += Vector3.Up * (2f / U);
                        _airborne = true;
                        SetGoalPoint(mp);                        // setGoalPath(SMS_GetMarioPos()): the point he stood on
                    }
                    WalkToCurPathNode(0f, 6f, 0f);
                    if (BckFrame() > 62f)
                        _flash = Mathf.Clamp(_flash - Prm.ColorChangeRate, 0f, 255f);
                    else
                        _flash = MathF.Abs(Mathf.Sin(Mathf.DegToRad(Prm.ColorChangeRate * _time))) * 255f;
                    if (!_airborne && BckEnded())
                        SetBck(BckJumpEnd);
                }
                else if (IsBck(BckJumpEnd))
                {
                    if (BckEnded())
                    {
                        _noReward = true;                        // LIVE_FLAG_UNK20000
                        PushAfterCurrent(NerveDie);
                        return true;
                    }
                }
                return false;

            case NerveLand:                                      // TNerveNameKuriLand
                if (IsBck(BckLand) && BckEnded()) return true;
                if (!_airborne && !IsBck(BckLand)) SetBck(BckLand);
                return false;
        }
        return base.ExecuteNerve(n);
    }
}
