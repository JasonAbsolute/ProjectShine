using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The Gooble -- TNameKuri (Enemy/namekuri.cpp, the Swoopin' Stu / Gooble: a goop blob that stalks Mario and leaps at him), a
/// TWalkerEnemy (walkerEnemy.cpp) on TSmallEnemy (smallEnemy.cpp) and the game's nerve machine (Strategic/Spine.hpp), with the
/// shipped /enemy/namekuri.prm (SmsParams.NameKuri). ONE TICK = one 120 Hz movement tick (TMarDirector::direct: 4 a frame, no gate
/// on the movement list): TLiveActor::bind moves the actor by its full velocity each tick, so the .prm's units-a-tick convert with
/// SmsUnits.TickSpeedToMps (x 120 / 74.44) -- NOT Mario's SpeedToMps (his jumpProcess steps pos + vel / 4).
///
/// THE NERVES (DEFINE_NERVE): Generate (anim set, then wander) -> WalkerGraphWander (walk the graph "main" at 0.2 deg a tick; no graph
/// here: random points -- see Graph) -> isFindMario (within 800 u and 300 u of height, not in water) -> NKFollowMario (march toward him,
/// 3 deg a tick) -> within 350 u in the front half -> JumpAttackPrepare (the wait anim, turning 1 deg a tick until he is within 15 deg)
/// -> JumpAttack (the jump_start anim; the body pulses white (|sin 5t| x 255, the TEV register 1); at anim frame 62 the leap:
/// SMSCalcJumpVelocityY to his position with vy 6 under gravity 0.135, +2 u, turning 6 deg a tick; landed and the anim done -> jump_end
/// -> dies (SmallEnemyDie, no reward)). Hitting Mario mid-leap: his damage (DmgNamekuri) and the Gooble dies. A fall past 100 u while
/// wandering -> Land (the land anim). A wall while walking -> the hop (SmallEnemyJump: vy 2.2 x scale, 1.5 u a tick forward, +5 deg).
/// WATER (HIT_MESSAGE_SPRAYED_BY_WATER, from SmsWaterDrops or the hose at close range): behaveToWater -> Die, melting (the hit anim,
/// gravity 0.01) when on the ground, no reward. STOMP / GROUND POUND (TMario::hitNormal -> TRAMPLE / HIP_DROP): kill -> Die (the down
/// anim), the death stamps goop (stamp range 15 x 32 u) and, with FLUDD's tank under half, puts the stage's water bottle (bottle_short,
/// TNameKuri::setAfterDeadEffect -> assets/Sms/Items/WaterBottle.tscn, SmsWaterBottle) on the ground under the body. generateItem's
/// GenItemRate 50 % asks for an item type (0x20000008) no shipped object carries -- the game drops nothing there; DropScene is an
/// opt-in deviation. ALIVE TIME: 2000 ticks, the
/// body inflating from 1900 (x 1.01 a tick to twice its scale), then it pops. Dead: freed, or re-launched from its placed point after
/// RelaunchTicks like TNameKuriLauncher (vy 4, the set anim) -- a placed Gooble is its own launcher (DEVIATION: the game pools them
/// behind goop launchers). Body scale 0.6..1.0 per life. THE LOOK (name.bmd / brain.bmd read with tools/projectshine/bmd_mat_dump.py):
/// the glb's _mat_body surface gets GoobleBody.gdshader (BodyMaterial, duplicated per instance: the body is 69 % the screen behind it
/// -- TScreenTexture through the camera projection -- and 31 % goop coloured by GX_TEVREG0, nameKuriTevColorData's 7 colours cycling
/// per instance, doubled by the stage-2 scale along K_new_name's alpha ramp, the skirt and the tufts full, the dome half, plus the
/// sun's specular, with GX_TEVREG1, the charge flash, added on top); _mat_eye gets GoobleEye.tres (K_new_name_eye cut out, lit); the
/// brain (TSharedParts on joint 2 = jnt_brain: brain.bmd with the stage's goop mark H_ma_rak swapped into K_name_dummy) hangs on the
/// bone as BrainScene with BrainMaterial; a shadow-only copy of the mesh casts the shadow the game's 影 draw buffer gives it. The
/// lights are the materials' (bianco2 scene.bin: the sun (255,255,255) from (200000, 500000, 200000), ambient (128,128,128), spec
/// shininess 50); a DirectionalLight3D in the scene replaces the sun's direction and colour.
///
/// THE SCENE (assets/Gooble.tscn): Model (the glb: name_down / hit / jump_end / jump_start / land / set / wait / walk) with
/// Model/AnimationPlayer, the CollisionShape3D for the world, and the exports below set to their default resources (the bottle and death
/// scenes, the three materials, the brain) so they load WITH the level -- a scene loaded for the first time compiles its shaders then
/// and there (every variant through glslang, synchronous, around a second for a new one; cached in user://shader_cache after), and that
/// belongs in the load, not in play (pass 77: the bottle scene used to load at the first death, so GetWater's three shaders compiled
/// mid-play). The game's hit cylinders (attack 50 x 30, damage 40 x 90, x the body scale) and sight cones are code (the old
/// DetectionZone / AttackZone / Hurtbox / StompZone areas were deleted in pass 77). THE DEATH: setDeadAnm / setMeltAnm emit ms_deadnamekli_o (16 goop blobs) and _n (28 rainbow drops) bound to
/// joint 2's matrix at the body scale -- DeathFxScene (assets/Sms/Fx/GoobleDeath.tscn, gen_fx_gooble.py) played at the brain joint
/// in the body's upright frame (DeathFxUpright). THE BOTTLE: setAfterDeadEffect -> BottleScene (pass 73). NOT PORTED: the sounds without
/// players (the exports), the oil-slip / catch kill (PUNCH), TWalker's slope blend (the Gooble leaps from any floor), the pollution launchers.
/// </summary>
public partial class Gooble : CharacterBody3D, ISmsSprayTarget
{
    /// <summary>An optional wander graph (gpConductor->getGraphByName("main")): its children's positions are the nodes, each a random
    /// next goal. None: random points within WanderRadiusUnits of the placed point (DEVIATION: his levels carry no graph).</summary>
    [Export] public Node3D Graph;
    [Export] public float WanderRadiusUnits = 600f;
    /// <summary>TLauncher's launch period (launcher.prm default 180 ticks): a dead Gooble re-launches from its placed point after this
    /// many ticks; below 0 it is freed instead.</summary>
    [Export] public int RelaunchTicks = 180;
    /// <summary>DEVIATION, off by default: TSmallEnemy::generateItem (GenItemRate 50 %) asks the map-object manager for actor type
    /// 0x20000008 and no TMapObjData in the shipped game carries that type (MapObjInit.cpp), so the real game drops nothing here. Set a
    /// scene (his coin) to drop one at the death point half the time anyway. None: the game's nothing.</summary>
    [Export] public PackedScene DropScene;
    /// <summary>TNameKuri::setAfterDeadEffect (namekuri.cpp 511): the water bottle a stomped Gooble leaves when Mario's tank is under
    /// half -- assets/Sms/Items/WaterBottle.tscn (SmsWaterBottle: bottle_short, half a tank) unless another is set. Resolved at _Ready
    /// (not at the death): the scene brings GetWater.tscn and its shaders in with the level (see the header).</summary>
    [Export] public PackedScene BottleScene;
    public const string BottleScenePath = "res://assets/Sms/Items/WaterBottle.tscn";
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
    /// <summary>The death splash (SmsGoobleDeath): assets/Sms/Fx/GoobleDeath.tscn unless another is set; one instance per Gooble.</summary>
    [Export] public PackedScene DeathFxScene;
    public const string DeathFxScenePath = "res://assets/Sms/Fx/GoobleDeath.tscn";
    [Export] public bool DebugPrint;

    // ------------------------------------------------------------------------------------------------------------------ the game's data
    private const float U = SmsUnits.UnitsPerMetre;
    private static readonly Color[] BodyColors =
    {
        // nameKuriTevColorData (GXColorS10, 0..255; alpha 169): red, blue, yellow, green, purple, cyan, orange
        new Color(0x64 / 255f, 0x00 / 255f, 0x0A / 255f), new Color(0x00 / 255f, 0x00 / 255f, 0x64 / 255f),
        new Color(0x5D / 255f, 0x54 / 255f, 0x00 / 255f), new Color(0x00 / 255f, 0x4C / 255f, 0x10 / 255f),
        new Color(0x63 / 255f, 0x04 / 255f, 0x60 / 255f), new Color(0x00 / 255f, 0x44 / 255f, 0x75 / 255f),
        new Color(0x64 / 255f, 0x28 / 255f, 0x00 / 255f),
    };
    private static int _nextColor; // TNameKuriManager::getNextColorIdx

    // the bck table (setBckAnm indices = the archive's files in order) and their frame counts (the .bck ANK1 durations, 60 Hz)
    private static readonly string[] BckNames = { "name_down", "name_hit", "name_jump_end", "name_jump_start", "name_land", "name_set", "name_wait", "name_walk" };
    private static readonly int[] BckFrames = { 9, 21, 5, 89, 19, 59, 120, 60 };
    private const int BckDown = 0, BckHit = 1, BckJumpEnd = 2, BckJumpStart = 3, BckLand = 4, BckSet = 5, BckWait = 6, BckWalk = 7;

    private enum Nerve { None, Generate, GraphWander, FollowMario, JumpAttackPrepare, JumpAttack, Land, Jump, Die }

    // ------------------------------------------------------------------------------------------------------------------ state
    private Node3D _model;
    private AnimationPlayer _anim;
    private Mario _mario;
    private readonly List<Nerve> _stack = new();
    private Nerve _cur = Nerve.None, _prev = Nerve.None;
    private int _time;                         // TSpineBase::mTime
    private int _bck = -1;                     // the current bck index
    private float _bckRate = 60f;              // the clip's bck frames a second of clip time
    private float _accum;                      // the 120 Hz sub-tick remainder
    private Vector3 _vel;                      // mVelocity, units a tick (the jump / fall)
    private Vector3 _lin;                      // mLinearVelocity, units a tick (the walk), rebuilt each tick
    private Vector3 _disp;                     // the ticks' displacement this physics frame, units
    private float _yaw;                        // mRotation.y, degrees: 0 = +Z, growing toward +X (MsGetRotFromZaxisY)
    private bool _airborne, _onWall, _noBind, _dead, _melt, _noReward;
    private bool _hitValid;                    // unk198: TNameKuri::isHitValid -- killed by a hit message other than water (the bottle's gate)
    private Vector3 _floorNormal = Vector3.Up;
    private readonly List<Vector3> _touching = new();  // other live actors' ground points (units) this frame: isCollidMove's push
    private float _bodyScale = 1f, _scale = 1f, _marchSpeed = 1f, _turnSpeed = 0.2f;
    private int _hp = 1, _alive, _sprayCooldown, _relaunch;
    private bool _goalIsMario; private Vector3 _goalPoint; // TPathNode: the actor, or a point (units)
    private Vector3 _spawnPos; private float _spawnYaw;
    private Random _rng;
    private float _flash;                      // unk1BC.r = g = b: the charge's white pulse, 0..255
    private float _scaleKeep;                  // unk1B0
    private int _colorIdx;
    private ShaderMaterial _bodyMat;
    private Material _eyeMat;
    private Node3D _brain;
    private SmsGoobleDeath _deathFx;
    private static Material _shadowMat;        // the shadow-only copy's opaque stand-in (shared)
    private float _groundY;                    // mGroundHeight, units (the floor below while falling)

    public override void _Ready()
    {
        AddToGroup("sms_enemy");   // pass 116: gpConductor->killEnemiesWithin (the shine get)
        _model = GetNodeOrNull<Node3D>("Model");
        _anim = GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_model == null || _anim == null)
            GD.PushWarning($"[SMS gooble] {GetPath()}: the scene needs Model (the glb) with Model/AnimationPlayer (see assets/Gooble.tscn)");
        if (_anim != null)
            foreach (string n in BckNames)
                if (!_anim.HasAnimation(n))
                    GD.PushWarning($"[SMS gooble] {GetPath()}: the model has no '{n}' clip (the game's bck table: {string.Join(", ", BckNames)})");
        _rng = new Random(GetInstanceId().GetHashCode());
        _spawnPos = GlobalPosition * U;
        _spawnYaw = Mathf.RadToDeg(Rotation.Y);
        MotionMode = MotionModeEnum.Grounded;
        CallDeferred(nameof(SetupCollisionExceptions)); // the game pushes Mario and enemies apart by messages, not bodies
        SetupLook();
        SetupDeathFx();
        // the bottle scene (and GetWater.tscn inside it) loads here, with the level: its shaders' first compile is a load-time cost, not a
        // hitch at the first stomp (pass 77; TNameKuriManager::loadAfter registers the stage's bottle at load too)
        BottleScene ??= GD.Load<PackedScene>(BottleScenePath);
        if (BottleScene == null)
            GD.PushWarning($"[SMS gooble] {GetPath()}: {BottleScenePath} did not load: no water bottle drop");
        SmsWaterHitMarks.Ensure(this);                          // pass 99: the water-hit mark pool loads with the level, not at the first hit
        Reset();
    }

    private void SetupCollisionExceptions()
    {
        foreach (Node n in GetTree().GetNodesInGroup("player"))
            if (n is PhysicsBody3D body)
                AddCollisionExceptionWith(body);
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

    /// <summary>TSmallEnemy::reset + TWalkerEnemy::reset + TNameKuri::reset: a fresh body (scale 0.6..1, march 1..1.5, the next colour),
    /// the spine on its default (Generate), still.</summary>
    private void Reset()
    {
        _turnSpeed = Lerp(SmsParams.NameKuri.TurnSpeedLow, SmsParams.NameKuri.TurnSpeedHigh, Rand());
        _bodyScale = Lerp(SmsParams.NameKuri.BodyScaleLow, SmsParams.NameKuri.BodyScaleHigh, Rand());
        _marchSpeed = Lerp(SmsParams.NameKuri.MarchSpeedLow, SmsParams.NameKuri.MarchSpeedHigh, Rand());
        _scale = _bodyScale; _scaleKeep = 1f;
        _hp = SmsParams.NameKuri.HitPointMax;
        _dead = false; _noReward = false; _melt = false; _noBind = false; _hitValid = false;
        _sprayCooldown = 0; _alive = 0; _flash = 0f;
        _vel = Vector3.Zero; _lin = Vector3.Zero;
        _colorIdx = _nextColor; _nextColor = (_nextColor + 1) % BodyColors.Length;
        _stack.Clear(); _cur = Nerve.None; _prev = Nerve.None; _time = 0;
        PushAfterCurrent(Nerve.Generate);          // mSpine->pushAfterCurrent(mSpine->getDefault())
        _goalIsMario = true;                       // setGoalPath(TPathNode(gpMarioAddress))
        if (_model != null) _model.Visible = true;
        SetCollisionLayerValue(1, true);
        ApplyLook();
    }

    // ------------------------------------------------------------------------------------------------------------------ the spine
    private void PushNerve(Nerve n) { if (_cur != Nerve.None) { _prev = _cur; _stack.Add(_cur); } _time = 0; _cur = n; }
    private void PushAfterCurrent(Nerve n) => _stack.Add(n);
    private void SetNext(Nerve n) { if (_cur != Nerve.None) _prev = _cur; _time = 0; _cur = n; }
    private Nerve PopNerve() { if (_stack.Count == 0) return Nerve.None; Nerve n = _stack[^1]; _stack.RemoveAt(_stack.Count - 1); return n; }
    private void SpineUpdate()
    {
        if (_cur == Nerve.None)
        {
            Nerve n = PopNerve();
            if (n != Nerve.None) { _cur = n; _time = 0; }
        }
        if (_cur == Nerve.None)
            return;
        bool done = Execute(_cur);
        _time++;
        if (done) { _prev = _cur; _cur = Nerve.None; }
    }

    /// <summary>For the drives / logs: the nerve, its tick, the bck and the spine's depth.</summary>
    public string NerveName => (_cur == Nerve.None ? _prev + "(done)" : _cur.ToString());
    public int NerveTime => _time;
    public string BckName => _bck >= 0 ? BckNames[_bck] : "-";
    public bool IsDead => _dead;
    public bool Airborne => _airborne;
    public float Flash => _flash;
    public float BodyScale => _bodyScale;
    public float YawDeg => _yaw;
    public int ColorIndex => _colorIdx;
    public Vector3 VelUnits => _vel;

    // ------------------------------------------------------------------------------------------------------------------ the tick
    public override void _PhysicsProcess(double delta)
    {
        if (_mario == null || !IsInstanceValid(_mario))
        {
            _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
            if (_mario != null)
                AddCollisionExceptionWith(_mario); // Mario is spawned after the level: the deferred pass in _Ready may not have seen him
        }
        int n = SmsUnits.TicksFor(delta, ref _accum);
        _disp = Vector3.Zero;
        for (int i = 0; i < n; i++)
            Tick();
        if (_dead)
            return;
        // the ticks' displacement through the body: the world's floors and walls (TWalker's bind / the map check)
        FloorSnapLength = _airborne ? 0f : 0.1f;
        Velocity = _disp / U / (float)delta;
        MoveAndSlide();
        bool onFloor = IsOnFloor();
        _onWall = false;
        _touching.Clear();
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var col = GetSlideCollision(i);
            GodotObject other = col.GetCollider();
            if (other is Gooble g2)
                _touching.Add(g2.GlobalPosition * U);          // another small enemy: pushed apart, no hop
            else if (other is not Mario && MathF.Abs(col.GetNormal().Dot(UpDirection)) < 0.5f)
                _onWall = true;                                 // TWalker: a wall ahead -> the hop
        }
        if (onFloor)
        {
            _floorNormal = GetFloorNormal();
            if (_vel.Y <= 0f)
            {
                // bind: nextPos.y <= mGroundHeight + 0.05 -> offLiveFlag(AIRBORNE), mVelocity = 0
                _airborne = false;
                _vel = Vector3.Zero;
            }
        }
        else
            _airborne = true;
        ApplyLook();
    }

    /// <summary>TNameKuri::perform(CUE_MOVE) -> moveObject: TWalkerEnemy's / TSmallEnemy's with the NameKuri tail.</summary>
    private void Tick()
    {
        if (_dead)
        {
            if (RelaunchTicks >= 0 && ++_relaunch > RelaunchTicks)
                Relaunch();
            return;
        }
        _lin = Vector3.Zero;
        SpineUpdate();                                      // control()
        HitCheckMario();                                    // gpObjHitCheck: the collision list -> attackToMario / TMario::hitNormal
        if (!_airborne)
            foreach (Vector3 o in _touching)                 // TSmallEnemy::moveObject, isCollidMove (both on the ground): away at march x 3
            {
                Vector3 d = Pos() - o;
                if (d == Vector3.Zero) d.X += 1f;
                _lin = d.Normalized() * (_marchSpeed * 3f);
            }
        if (_dead)
            return;
        // TLiveActor::bind: gravity and the terminal speed (the ground is MoveAndSlide's)
        if (!_noBind)
        {
            _vel.Y -= GravityY();
            if (_vel.Y < -40f) _vel.Y = -40f;
        }
        // TWalkerEnemy::moveObject: blocked by a wall while walking -> the hop
        if (_onWall && !_airborne && _cur != Nerve.Jump && _cur != Nerve.Die && _lin.LengthSquared() > 0f && _cur != Nerve.JumpAttack && _cur != Nerve.JumpAttackPrepare)
        {
            Vector3 fwd = PolarXZ(_yaw, SmsParams.NameKuri.MarchSpeedHigh);
            _vel.X = fwd.X; _vel.Z = fwd.Z;
            PushNerve(Nerve.Jump);
            _airborne = true;
            _yaw += 5f;
            _onWall = false;
        }
        // TNameKuri::moveObject
        if (_vel.Y < 0f && _cur == Nerve.GraphWander && _airborne && Pos().Y - GroundYBelow() > SmsParams.NameKuri.LandHeight)
            PushNerve(Nerve.Land);
        if (WalkSound != null)
        {
            bool walking = !_airborne && _bck == BckWalk;
            if (walking && !WalkSound.Playing) WalkSound.Play();
            else if (!walking && WalkSound.Playing) WalkSound.Stop();
        }
        _alive++;
        if (_alive + 100 > SmsParams.NameKuri.AliveTime && _scale < _bodyScale * 2f)
            _scale *= 1.01f;
        if (_alive > SmsParams.NameKuri.AliveTime)
            Kill();
        if (_sprayCooldown > 0 && ++_sprayCooldown > 30)
            _sprayCooldown = 0;
        _disp += _lin + (_noBind ? Vector3.Zero : _vel);
    }

    private float GravityY()
    {
        if (_melt) return 0.01f;                                             // LIVE_FLAG_MELT_ON_DEATH
        if (_cur == Nerve.JumpAttack) return SmsParams.NameKuri.JumpAttackGravity;
        return SmsParams.NameKuri.Gravity;                                   // TLiveActor's mGravity
    }

    private Vector3 Pos() => GlobalPosition * U;
    private Vector3 MarioPos() => _mario != null ? _mario.GlobalPosition * U : Pos();
    private float Rand() => (float)_rng.NextDouble();
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static Vector3 PolarXZ(float yawDeg, float r) => new Vector3(r * Mathf.Sin(Mathf.DegToRad(yawDeg)), 0f, r * Mathf.Cos(Mathf.DegToRad(yawDeg)));
    private static float YawOf(Vector3 v) => Mathf.RadToDeg(Mathf.Atan2(v.X, v.Z));                    // MsGetRotFromZaxisY
    private static float Wrap360(float a) { a %= 360f; return a < 0f ? a + 360f : a; }                   // MsWrap(a, 0, 360)
    private static float AngleDiff(float a, float b) { float d = Wrap360(a - b); return d > 180f ? d - 360f : d; } // MsAngleDiff

    /// <summary>The floor under the body, units (mGroundHeight): a ray down from the feet.</summary>
    private float GroundYBelow()
    {
        var space = GetWorld3D().DirectSpaceState;
        Vector3 from = GlobalPosition + Vector3.Up * 0.05f;
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 200f, CollisionMask);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = space.IntersectRay(q);
        _groundY = hit.Count > 0 ? ((Vector3)hit["position"]).Y * U : Pos().Y - 100000f;
        return _groundY;
    }

    // ------------------------------------------------------------------------------------------------------------------ the look
    /// <summary>TNameKuri::calcRootMatrix: the base matrix from the ground normal (up) and the heading (sin / cos of mRotation.y), the
    /// body scale; the anim's colour registers on the material.</summary>
    private void ApplyLook()
    {
        if (_model != null)
        {
            Vector3 up = _floorNormal.LengthSquared() > 0f ? _floorNormal.Normalized() : Vector3.Up;
            Vector3 fwd = PolarXZ(_yaw, 1f);
            Vector3 x = up.Cross(fwd);
            if (x.LengthSquared() < 1e-6f) { up = Vector3.Up; x = up.Cross(fwd); }
            x = x.Normalized();
            Vector3 z = x.Cross(up).Normalized();
            _model.Transform = new Transform3D(new Basis(x, up, z) * Basis.FromScale(Vector3.One * _scale), Vector3.Zero);
        }
        if (_bodyMat != null)
        {
            Color c = BodyColors[_colorIdx];
            _bodyMat.SetShaderParameter("body_color", new Vector3(c.R, c.G, c.B));            // GX_TEVREG0, the raw register value
            float f = _flash / 255f;
            _bodyMat.SetShaderParameter("flash_color", new Vector3(f, f, f));                 // GX_TEVREG1
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ anims
    private void SetBck(int i)
    {
        _bck = i;
        if (_anim == null || !_anim.HasAnimation(BckNames[i]))
            return;
        var clip = _anim.GetAnimation(BckNames[i]);
        bool loop = i == BckWait || i == BckWalk;               // the .bck loop modes: wait / walk 2, the rest 0
        clip.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        _bckRate = clip.Length > 0f ? BckFrames[i] / (float)clip.Length : 60f;     // the clip's native bck frames a second
        _anim.Play(BckNames[i]);
        _anim.Seek(0f, true);
        _anim.SpeedScale = 60f / _bckRate;                      // one bck frame = 1 / 60 s (SMSGetAnmFrameRate: 2 anim steps a frame)
    }
    private bool IsBck(int i) => _bck == i;
    /// <summary>getCurAnmFrameNo: the clip's position in bck frames.</summary>
    private float BckFrame()
    {
        if (_bck < 0) return 0f;
        if (_anim == null || !_anim.HasAnimation(BckNames[_bck]) || _anim.CurrentAnimation != BckNames[_bck]) return BckFrames[_bck];
        return (float)_anim.CurrentAnimationPosition * _bckRate;
    }
    /// <summary>checkCurAnmEnd(0): a non-looping clip at (or past) its last frame.</summary>
    private bool BckEnded() => _bck >= 0 && !(IsBck(BckWait) || IsBck(BckWalk)) && (_anim == null || !_anim.HasAnimation(BckNames[_bck]) || !_anim.IsPlaying() || BckFrame() >= BckFrames[_bck] - 0.5f);
    private bool _passed62; private float _lastFrame;
    /// <summary>getFrameCtrl(BCK)->checkPass(f): the clip crossed frame f this tick.</summary>
    private bool BckPassed(float f)
    {
        float now = BckFrame();
        bool pass = _lastFrame < f && now >= f;
        _lastFrame = now;
        return pass;
    }

    // ------------------------------------------------------------------------------------------------------------------ sight and goals
    /// <summary>MsIsInSight(eye, sight, target, length, angle, aware).</summary>
    private bool IsInSight(Vector3 target, float length, float angle, float aware)
    {
        Vector3 d = target - Pos();
        float d2 = d.LengthSquared();
        if (d2 < aware * aware) return true;
        return d2 < length * length && MathF.Abs(AngleDiff(YawOf(d), _yaw)) < angle * 0.5f;
    }

    /// <summary>TSmallEnemy::isFindMario(1): not airborne, Mario not in water, within SearchHeight of height and in sight within
    /// SearchLength / SearchAngle / SearchAware.</summary>
    private bool IsFindMario()
    {
        if (_mario == null || _airborne) return false;
        if (_mario.SmsInWaterForEnemies) return false;
        Vector3 mp = MarioPos();
        if (MathF.Abs(mp.Y - Pos().Y) >= SmsParams.NameKuri.SearchHeight) return false;
        return IsInSight(mp, SmsParams.NameKuri.SearchLength, SmsParams.NameKuri.SearchAngle, SmsParams.NameKuri.SearchAware);
    }

    private Vector3 Goal() => _goalIsMario ? MarioPos() : _goalPoint;
    private void SetGoalMario() { _goalIsMario = true; }
    private void SetGoalPoint(Vector3 p) { _goalIsMario = false; _goalPoint = p; }

    /// <summary>goToRandomNextGraphNode: a random node of the graph; none -> a random point within WanderRadiusUnits of the placed point
    /// (DEVIATION), on the ground height of the body.</summary>
    private void GoToRandomNextGraphNode()
    {
        if (Graph != null && Graph.GetChildCount() > 0)
        {
            var kids = Graph.GetChildren();
            Node3D pick = kids[_rng.Next(kids.Count)] as Node3D;
            if (kids.Count > 1 && pick != null && (pick.GlobalPosition * U - Pos()).Length() < 100f)
                pick = kids[(kids.IndexOf(pick) + 1 + _rng.Next(kids.Count - 1)) % kids.Count] as Node3D;
            if (pick != null) { SetGoalPoint(pick.GlobalPosition * U); return; }
        }
        float a = Rand() * MathF.Tau, r = WanderRadiusUnits * (0.3f + 0.7f * Rand());
        SetGoalPoint(_spawnPos + new Vector3(r * MathF.Sin(a), 0f, r * MathF.Cos(a)));
    }
    /// <summary>isReachedToGoal: within 100 u of the goal.</summary>
    private bool IsReachedToGoal() => (Goal() - Pos()).Length() < 100f;

    /// <summary>TSpineEnemy::calcTurnSpeedToReach(march, half the distance): the turn a tick that reaches the goal on an arc.</summary>
    private static float CalcTurnSpeedToReach(float march, float d)
    {
        if (d == 0f) return 0f;
        float f = d * 2f * d;
        float c = Mathf.Clamp(-(march * march - f) / f, -1f, 1f);
        if (c == 1f) return 0f;
        if (c == -1f) return 180f;
        float s = MathF.Sqrt(1f - c * c);
        return 90f - Mathf.RadToDeg(MathF.Atan2(s, c));
    }

    /// <summary>TSpineEnemy::walkToCurPathNode(march, turn, offset): turn toward the goal by at most turn degrees (less within the turning
    /// circle, so the arc reaches it) and add march units a tick along the new heading to mLinearVelocity.</summary>
    private void WalkToCurPathNode(float march, float turn, float offset)
    {
        Vector3 d = Goal() - Pos();
        float dist = d.Length();
        float angle = Wrap360(offset + YawOf(d));
        float diff = AngleDiff(angle, _yaw);
        float minR;
        if (turn >= 90f) minR = 0f;
        else
        {
            float s = Mathf.Sin(Mathf.DegToRad(turn));
            minR = s == 0f ? 100000f : march * Mathf.Sin(Mathf.DegToRad(90f - turn * 0.5f)) / s;
        }
        float step;
        if (dist > minR * 2f)
            step = diff > 0f ? MathF.Min(diff, turn) : MathF.Max(diff, -turn);
        else
        {
            float t = CalcTurnSpeedToReach(march, dist * 0.5f);
            step = diff > 0f ? MathF.Min(diff, t) : MathF.Max(diff, -t);
        }
        _yaw = Wrap360(_yaw + step);
        _lin += PolarXZ(_yaw, march);
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    private bool Execute(Nerve n)
    {
        switch (n)
        {
            case Nerve.Generate:                                 // TNerveWalkerGenerate
                if (_time == 0) { SetBck(BckSet); _vel = Vector3.Zero; }    // setGenerateAnm
                else if (BckEnded()) { PushAfterCurrent(Nerve.GraphWander); return true; }
                return false;

            case Nerve.GraphWander:                              // TNerveWalkerGraphWander
                if (_time == 0) { SetBck(BckWalk); GoToRandomNextGraphNode(); } // setWalkAnm, initialGraphNode
                if (IsReachedToGoal()) GoToRandomNextGraphNode();
                if (IsFindMario())
                {
                    // TNameKuri::behaveToFindMario
                    PushAfterCurrent(Nerve.GraphWander);
                    PushAfterCurrent(Nerve.FollowMario);
                    return true;
                }
                WalkToCurPathNode(_marchSpeed, _turnSpeed, 0f); // walkBehavior(0): the zigzag with no graph factor = the plain walk
                return false;

            case Nerve.FollowMario:                              // TNerveNKFollowMario
                if (_time == 0) SetGoalMario();
                WalkToCurPathNode(_marchSpeed, 3f, 0f);
                if (IsInSight(MarioPos(), SmsParams.NameKuri.JumpAttackRadius, SmsParams.NameKuri.JumpAttackAngle, 0f))
                {
                    PushAfterCurrent(Nerve.JumpAttackPrepare);
                    return true;
                }
                return false;

            case Nerve.JumpAttackPrepare:                        // TNerveNameKuriJumpAttackPrepare
                if (_time == 1) { SetGoalMario(); SetBck(BckWait); }
                else if (IsBck(BckWait))
                {
                    if (IsInSight(MarioPos(), 100000f, 30f, 0f))
                    {
                        if (!_airborne) { PushAfterCurrent(Nerve.JumpAttack); return true; } // (TWalker's slope blend not ported)
                    }
                    else
                        WalkToCurPathNode(0f, SmsParams.NameKuri.JumpAttackTurnSp, 0f);
                }
                return false;

            case Nerve.JumpAttack:                               // TNerveNameKuriJumpAttack
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
                        _vel = CalcVelocityToJumpToY(mp, SmsParams.NameKuri.JumpAttackSp, SmsParams.NameKuri.JumpAttackGravity);
                        GlobalPosition += Vector3.Up * (2f / U);
                        _airborne = true;
                        SetGoalPoint(mp);                        // setGoalPath(SMS_GetMarioPos()): the point he stood on
                    }
                    WalkToCurPathNode(0f, 6f, 0f);
                    if (BckFrame() > 62f)
                        _flash = Mathf.Clamp(_flash - SmsParams.NameKuri.ColorChangeRate, 0f, 255f);
                    else
                        _flash = MathF.Abs(Mathf.Sin(Mathf.DegToRad(SmsParams.NameKuri.ColorChangeRate * _time))) * 255f;
                    if (!_airborne && BckEnded())
                        SetBck(BckJumpEnd);
                }
                else if (IsBck(BckJumpEnd))
                {
                    if (BckEnded())
                    {
                        _noReward = true;                        // LIVE_FLAG_UNK20000
                        PushAfterCurrent(Nerve.Die);
                        return true;
                    }
                }
                return false;

            case Nerve.Land:                                     // TNerveNameKuriLand
                if (IsBck(BckLand) && BckEnded()) return true;
                if (!_airborne && !IsBck(BckLand)) SetBck(BckLand);
                return false;

            case Nerve.Jump:                                     // TNerveSmallEnemyJump (the wall hop)
                if (_time == 0)
                {
                    _vel.Y = SmsParams.NameKuri.JumpForce * _bodyScale;
                    _airborne = true;
                }
                return _time > 0 && !_airborne;

            case Nerve.Die:                                      // TNerveSmallEnemyDie
                if (_time == 0)
                {
                    _hp = Math.Max(0, _hp - 1);                  // decHitPoints
                    if (_hp == 0) SetCollisionLayerValue(1, false); // HIT_FLAG_NO_COLLISION (the body stops being a hit actor)
                    if (_melt) SetMeltAnm();
                    else
                    {
                        // unk150 & 0x20: the death stamps goop (stampRange 15 x 32 x unk158)
                        SmsPollutionMap.Pollute(GlobalPosition, SmsParams.NameKuri.StampRange * 32f);
                        if (_hp == 0) SetDeadAnm();
                        else return true;
                    }
                }
                if (BckEnded() && _time > BckFrames[_bck] || _time > 360)
                {
                    GenRandomItem();
                    Dead();
                    return true;
                }
                return false;
        }
        return true;
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

    // ------------------------------------------------------------------------------------------------------------------ hits
    /// <summary>TObjHitCheck's checkDistance: the attacker's cylinder (radius, height above its ground point) against the sufferer's.</summary>
    private static bool CheckDistance(Vector3 a, float ar, float ah, Vector3 s, float sr, float sh)
    {
        if (a.Y > s.Y + sh) return false;
        if (a.Y + ah < s.Y) return false;
        float r = ar + sr, dx = a.X - s.X, dz = a.Z - s.Z;
        return r * r > dx * dx + dz * dz;
    }

    /// <summary>The collision lists of one tick, both ways: the Gooble's attack cylinder (50 x 30, scaled) on Mario's damage cylinder
    /// (42 x 130) -> TSmallEnemy::moveObject's attackToMario; Mario's attack cylinder (TrampleRadius 100 x AttackHeight 50) on the
    /// Gooble's damage cylinder (40 x 90, scaled) -> TMario::hitNormal: falling in a jump onto it -> TRAMPLE (HIP_DROP in a ground pound),
    /// the hose spraying at close range -> SPRAYED_BY_WATER.</summary>
    private void HitCheckMario()
    {
        if (_mario == null || _hp == 0 || _cur == Nerve.Die) return;
        Vector3 g = Pos(), m = MarioPos();
        float s = _bodyScale;
        bool marioHits = CheckDistance(m, SmsParams.Mario.TrampleRadius, SmsParams.Mario.AttackHeight, g, SmsParams.NameKuri.DamageRadius * s, SmsParams.NameKuri.DamageHeight * s);
        if (marioHits)
        {
            bool falling = !_mario.IsOnFloor() && _mario.Velocity.Y < 0f && g.Y < m.Y;
            var st = _mario.stateOfMario;
            bool dive = st == Mario.MarioState.diving || st == Mario.MarioState.singleJumpDive || st == Mario.MarioState.doubleJumpDive || st == Mario.MarioState.tripleJumpDive
                || st == Mario.MarioState.bellySlidingFromDive || st == Mario.MarioState.swimming || st == Mario.MarioState.ledgeHang || st == Mario.MarioState.ledgeClimb
                || st == Mario.MarioState.treeGrab || st == Mario.MarioState.treeClimb || st == Mario.MarioState.hurt || st == Mario.MarioState.dead;
            if (falling && st == Mario.MarioState.groundPoundFalling)
            {
                _hitValid = true;                                // isHitValid(HIP_DROP): unk198 = 1 -- the bottle may follow
                Kill(); return;                                  // HIT_MESSAGE_HIP_DROP -> kill
            }
            if (falling && !dive)
            {
                // trampleExec: HIT_MESSAGE_TRAMPLE -> isHitValid (unk198 = 1) -> kill; Mario -> MARIO_STATUS_TRAMPLE
                _hitValid = true;
                Kill();
                _mario.StompBounce();
                return;
            }
            if (_mario.SmsHoseSpraying)
                SmsSprayedByWater(m + Vector3.Up * 80f / U);    // mStaticHitActor at his position + 80
            if (_dead || _cur == Nerve.Die) return;
        }
        bool goobleHits = CheckDistance(g, SmsParams.NameKuri.AttackRadius * s, SmsParams.NameKuri.AttackHeight * s, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight);
        if (goobleHits)
            AttackToMario();
    }

    /// <summary>TNameKuri::attackToMario: SMS_SendMessageToMario(ATTACK) (DmgNamekuri: 1 HP, 300 i-ticks; his TakeDamage keeps the
    /// invincibility), and a leaping Gooble stops and dies on the hit.</summary>
    private void AttackToMario()
    {
        _mario.TakeDamage(GlobalPosition, Mario.SmsDamageProfile.Namekuri);
        if (_cur == Nerve.JumpAttack)
        {
            _vel = Vector3.Zero;
            _noReward = true;
            Kill();
        }
    }

    /// <summary>TSmallEnemy::kill: the Die nerve now, the stack dropped.</summary>
    private void Kill()
    {
        if (_dead) return;
        _hp = 1;
        if (_cur != Nerve.Die)
        {
            _stack.Clear();
            SetNext(Nerve.Die);
        }
    }

    /// <summary>HIT_MESSAGE_SPRAYED_BY_WATER (TSmallEnemy::receiveMessage 546): EVERY hit emits PARTICLE_MS_ENM_WATHIT at the water hit actor's
    /// position (the drop, or the hose's point) -- the splat that shows the water landing -- then, once per 30-tick cooldown, decHpByWater (the
    /// hit's attack, at least 1) and TNameKuri::behaveToWater: Die -- melting when on the ground -- with no reward. Returns true (the message is
    /// taken: TModelWaterManager::move() 429 kills the drop); a dead one has no collision in the game, so false (the drop flies on -- here it lands).
    /// MSD_SE_EN_COMMON_W_HIT_OK not ported.</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead || _hp == 0) return false;
        SmsWaterHitMarks.Emit(this, hitPos);                    // pass 99: before the cooldown -- the game marks every hit
        if (_sprayCooldown != 0) return true;
        _sprayCooldown = 1;
        _hp = Math.Max(0, _hp - 1);
        if (_cur != Nerve.Die)
        {
            PushNerve(Nerve.Die);
            if (!_airborne) _melt = true;                       // LIVE_FLAG_MELT_ON_DEATH
            _noReward = true;                                   // LIVE_FLAG_UNK20000
        }
        return true;
    }

    /// <summary>setDeadAnm: the down anim, MSD_SE_EN_NAMEKURI_DOWN, ms_deadnamekli_o / n on joint 2, still, LIVE_FLAG_UNK10 (no bind).</summary>
    private void SetDeadAnm()
    {
        SetBck(BckDown);
        if (DownSound != null && DownSound.Stream != null) DownSound.Play();
        PlayDeathFx();
        _vel = Vector3.Zero; _lin = Vector3.Zero; _noBind = true;
    }

    /// <summary>setMeltAnm: the hit anim, ms_deadnamekli_o / n on joint 2, still, LIVE_FLAG_UNK10, MSD_SE_EN_NAMEKURI_DOWN_WT.</summary>
    private void SetMeltAnm()
    {
        SetBck(BckHit);
        PlayDeathFx();
        _vel = Vector3.Zero; _lin = Vector3.Zero; _noBind = true;
        if (MeltSound != null && MeltSound.Stream != null) MeltSound.Play();
    }

    /// <summary>TSmallEnemy::genRandomItem (smallEnemy.cpp 334) at the Die nerve's end: setAfterDeadEffect (TNameKuri's own: the bottle),
    /// genEventCoin (no coin placed with it), then generateItem unless LIVE_FLAG_UNK20000 (its own leap) or DEAD: rand < GenItemRate +
    /// GenEggRate -> makeObjAppear(ground point, 0x20000008) -- a type no shipped TMapObjData carries, so the game drops nothing; DropScene
    /// is the opt-in deviation that drops a scene there anyway.</summary>
    private void GenRandomItem()
    {
        SetAfterDeadEffect();
        if (_noReward || DropScene == null) return;
        if (Rand() * 100f < SmsParams.NameKuri.GenItemRate + SmsParams.NameKuri.GenEggRate)
        {
            Node3D drop = DropScene.Instantiate<Node3D>();
            GetParent().AddChild(drop);
            drop.GlobalPosition = GlobalPosition;
        }
    }

    /// <summary>TNameKuri::setAfterDeadEffect (namekuri.cpp 511; no disappearance smoke -- TSmallEnemy's is overridden away): killed by a
    /// hit (unk198) with SMS_GetMarioWaterGun()->getCurrentWater() x 2 &lt; getMaxWater() -> gpItemManager->makeObjAppear(mPosition,
    /// 0x20000001, true): the stage's one bottle_short put on the ground found from 5 units over the body (checkGround; illegal ground or
    /// the bottle already out -> nothing).</summary>
    private void SetAfterDeadEffect()
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

    /// <summary>TConductor::killEnemiesWithin (TMario::winDemo, 2000 u of the shine get): this enemy dies now.</summary>
    public void SmsKillByDemo() { if (!_dead) Dead(); }
    public bool SmsIsDead => _dead;

    /// <summary>LIVE_FLAG_DEAD: out of the world until the launcher brings it back (or freed).</summary>
    private void Dead()
    {
        _dead = true; _relaunch = 0;
        if (_model != null) _model.Visible = false;              // the body goes; the splash (a top-level child) stays
        if (WalkSound != null && WalkSound.Playing) WalkSound.Stop();
        _stack.Clear(); _cur = Nerve.None;
        if (RelaunchTicks < 0)
            QueueFree();
    }

    /// <summary>TNameKuriLauncher::stateLaunch: resetSRTV(the launcher's point, rotation 0, the scale, velocity (0, 4, 0)) -- the body pops
    /// up out of the goop into its Generate nerve.</summary>
    private void Relaunch()
    {
        GlobalPosition = _spawnPos / U;
        _yaw = _spawnYaw;
        Reset();
        _vel = new Vector3(0f, 4f, 0f); // the launcher's velocity -- which control() zeroes at once in the game too (setGenerateAnm)
        _airborne = true;               // LIVE_FLAG_AIRBORNE: bind drops it onto the floor
        if (DebugPrint) GD.Print($"[SMS gooble] {Name}: re-launched (colour {_colorIdx}, scale {_bodyScale:F2}, march {_marchSpeed:F2})");
    }
}
