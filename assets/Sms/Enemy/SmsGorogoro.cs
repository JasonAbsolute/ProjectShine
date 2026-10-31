using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// GOROGORO -- TGorogoro (Enemy/igaiga.cpp; JP ゴロゴロ, "rumbling") on <see cref="SmsSmallEnemy"/>, with the shipped /enemy/gorogoro.prm (SMS pass
/// 136). Bianco Hills' rolling goop head: Petey Piranha's head (/scene/gorogoro/bosspaku_head.bmd) under a coat of the stage's goop, spat by the
/// Polluted Piranha at the gate (TBiancoGateKeeper::launchGorogoro -> generateByGateKeeper) and rolling down the paths toward Mario.
///
/// THE GAME. A roll enemy (TRollEnemy on TWalkerEnemy): march 7..8 u/tick (11..13 m/s), turn 0.03 deg a tick (TurnSpeedLow: it barely steers),
/// body scale 0.8 (the head is 470 u across: 5 m), hit points 255. Its one nerve, TNerveGorogoroRollOnGraph: the move clip (bck 2) and the walk
/// toward the next graph node (goToShortestNextGraphNode), the node reached within 200 u (XZ) -> the next; walkBehavior(2, 1): the roll angle
/// grows 0.4 x march a tick (RollEnemyBodyCallback rotates joint 1 'center' about its X), the goop THROWN OFF the head (PKH_DORO on the root)
/// while within 30 u of the ground, and the BOUNCE: while airborne over 20 u up the fall height / 80 is remembered (at most BoundVYMax 20); on
/// landing (not on water) above 1.0 it bounces straight up at that speed, SPLATS the ground (requestPolluteModel: the goop splat model
/// bosspaku_head_stamp) and rumbles the pad -- so the gate's lob (vy 15 u/tick: 750 u up) lands, bounces 9.4, 3.7, then rolls. STUCK against a wall
/// (the distance to the goal unchanged by 100 u for 300 ticks) it dies. The TRAIL: setBehavior stamps the pollution map every tick (PolluteInterval
/// 1) on the ground with a 32 x PolluteRange 5 = 160 u stamp 10 ticks behind the body (unk1AC -10 x mPositionDelta). WATER: behaveToWater resets the
/// spray cooldown (every hit counts), the hit points fall (floored at 1) and the body's konst alpha K0.a = hp / 255 WASHES the goop off along the
/// dirt sheet (yogore2) to show Petey's skin; PKH_WASH drops fly; it NEVER dies of water. Rolling onto a WATER SURFACE: kill with the melt flags
/// (UNK10000 | UNK20000: no reward) -> the melt clip (bck 1), PKH_TOKE goop bubbles at the ground, half-speed rolling into the water. A STOMP /
/// ground pound (hitNormal, no isHitValid override): kill -> TNerveGorogoroDie: no collision, the splat model, the humi squash (bck 0) with
/// PKH_FUMI at joint 1, the dead effect; at the clip's end genRandomItem (the drop table) and LIVE_FLAG_DEAD; then TNerveSmallEnemyDie's 0x20
/// stamp: a 32 x StampRange 60 = 1920 u goop stamp under the body (unk150 = 0x31). Contact with Mario: ATTACK (the common damage). Other
/// actors: isCollidMove ATTACKs them and never pushes. Mario's spit targets: MsIsInSight(2000 u) -> toward Mario, else the nearest graph node;
/// the manager re-spawns dead ones every GenerateInterval 2500 ticks at the graph's node 0 while Mario is in the area.
///
/// THE PORT. <see cref="Graph"/> (a Node3D whose children are the nodes, in order) = the game's gorogoro0 / 1 graph: the nodes are followed in
/// order, the last one on past the end; with none, the head rolls straight on (the goal 2000 u ahead, re-set when reached). Graph JUMP edges
/// (flagJump: a leap between two nodes) are not ported. The splat model is a pollution stamp of its size (280 u x the scale). The trail stamps
/// every FOURTH tick (30 u apart at the march: still one line; a quarter of the texel work). The expansion by water (ExpandRate / ExpandMax: 1.0 in
/// the shipped prm = none) is not ported. A placed one re-rolls from its point after <see cref="SmsSmallEnemy.RelaunchTicks"/> (2500 in the scene =
/// GenerateInterval). NOT PORTED (logged): the Bianco watermill turning, STOP_ROCK, the mirror actor, the sounds (slots), Yoshi.
/// </summary>
[Tool]
[GlobalClass]
public partial class SmsGorogoro : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ gorogoro.prm
    public class RollParams : Params
    {
        public int GenerateInterval = 300;
        public float ExpandRate = 1f, ExpandMax = 1.5f, BoundVYMax = 15f, GroundOffsetY = 150f;
        public float PolluteRange = 5f;
        public int PolluteInterval = 1;
    }
    /// <summary>The shipped /enemy/gorogoro.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly RollParams Prm = new RollParams
    {
        HeadHeight = 500f, BodyRadius = 20f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 255, InstanceNum = 10, ActiveEnemyNum = 10,
        JumpForce = 60f, SearchLength = 800f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 2000f, GiveUpHeight = 650f,
        AttackWait = 120, FreezeWait = 300, DamageRadius = 220f, DamageHeight = 220f, AttackRadius = 180f, AttackHeight = 180f,
        TurnSpeedLow = 0.03f, TurnSpeedHigh = 0.05f, BodyScaleLow = 0.8f, BodyScaleHigh = 0.8f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 60f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 7f, MarchSpeedHigh = 8f,
        GenerateInterval = 2500, ExpandRate = 1f, ExpandMax = 1f, BoundVYMax = 20f, GroundOffsetY = -50f, PolluteRange = 5f, PolluteInterval = 1,
    };
    protected override Params P => Prm;
    /// <summary>TRollEnemy::mBoundVal: the fall height per unit of bounce speed.</summary>
    public const float BoundVal = 80f;
    /// <summary>The body's splat (the pollute model bosspaku_head_stamp.bmd, 280 u across), as a pollution stamp of that width x the scale.</summary>
    public const float SplatUnits = 280f;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Goop (the gate's knobs)")]
    /// <summary>The goop sheet the head wears -- the game: the STAGE's pollution mark (goop_mark_bianco.png = Bianco's h_ma_rak.bti). Unset = the material's own.
    /// The effects take the sheet's colours (SmsPollutionLayer.MeasureGoopColors), as the gatekeeper's do.</summary>
    [Export] public Texture2D GoopTexture { get => _goopTexture; set { _goopTexture = value; if (IsInsideTree()) ApplyGoopKnobs(); } }
    private Texture2D _goopTexture;
    /// <summary>Texture (the game) / Tint (x GoopColor) / Recolour (GoopColor's hue over the sheet's pattern) -- the gatekeeper's modes.</summary>
    [Export] public SmsGateKeeper.GoopColorModes GoopColorMode { get => _goopMode; set { _goopMode = value; if (IsInsideTree()) ApplyGoopKnobs(); } }
    private SmsGateKeeper.GoopColorModes _goopMode = SmsGateKeeper.GoopColorModes.FromMaterial;
    [Export(PropertyHint.ColorNoAlpha)] public Color GoopColor { get => _goopColor; set { _goopColor = value; if (IsInsideTree()) ApplyGoopKnobs(); } }
    private Color _goopColor = Colors.White;

    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the death stamp's width (the game: StampRange 60 x 32 = 1920 u = 25.8 m of goop under a stomped head). 0 = no stamp.</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float DeathStampScale = 1f;
    /// <summary>The goop trail behind the rolling head (setBehavior's stampGround every tick; mIsPolluter is the placement's). Off = no trail.</summary>
    [Export] public bool LeavesTrail = true;
    /// <summary>x the trail stamp's width (the game: 32 x PolluteRange 5 = 160 u).</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float TrailScale = 1f;
    /// <summary>x the march (the game: 7..8 u/tick = 11..13 m/s).</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float SpeedScale = 1f;

    [ExportGroup("Effects (empty = the defaults under Gorogoro/)")]
    /// <summary>PARTICLE_MS_PKH_DORO: the goop thrown off the head while it rolls on the ground (an SmsNpcEffect on the root, EmitFrames 0).</summary>
    [Export] public PackedScene DoroFxScene;
    /// <summary>PARTICLE_MS_PKH_WASH: the goop washing off under the hose (an SmsNpcEffect on the root, kept alive while hits come).</summary>
    [Export] public PackedScene WashFxScene;
    /// <summary>PARTICLE_MS_PKH_FUMI: the squash puffs at joint 1 when stomped (an SmsFxBurst).</summary>
    [Export] public PackedScene FumiFxScene;
    /// <summary>PARTICLE_MS_PKH_TOKE: the goop bubbles at the ground while it melts into water (an SmsNpcEffect, EmitFrames 100).</summary>
    [Export] public PackedScene TokeFxScene;
    public const string FxDir = "res://assets/Sms/Enemy/Gorogoro/";
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_BS_KRPAKU_ROLL (looping while on the ground) / _GND (a bounce) / _DOWN (the stomp) / _SINK (the melt).</summary>
    [Export] public AudioStreamPlayer3D RollSound, BounceSound, DownSound, SinkSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips
    private static readonly string[] Names = { "bosspaku_head_humi", "bosspaku_head_melt", "bosspaku_head_move" };
    private static readonly int[] Frames = { 99, 119, 241 };
    private static readonly bool[] Loops = { false, false, true };
    protected const int BckHumi = 0, BckMelt = 1, BckMove = 2;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    /// <summary>The bmd's joints: 0 scale, 1 center (the roll), 2 head.</summary>
    public const string RootJoint = "scale", RollJoint = "center";

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveRollOnGraph = NerveSpecies, NerveGoroDie = NerveSpecies + 1;
    protected override string NerveName(int n) => n == NerveRollOnGraph ? "RollOnGraph" : n == NerveGoroDie ? "GoroDie" : base.NerveName(n);
    protected override int DefaultNerve => NerveRollOnGraph;

    // ------------------------------------------------------------------------------------------------------------------ state
    private float _rollAngle;                 // unk194: the roll about the center joint, degrees
    private float _bound;                     // unk1A0: the bounce speed remembered from the fall
    private bool _inFlight;                   // unk1A8: lobbed / leaping -- no walking until the landing
    private float _trailBack = -10f;          // unk1AC: the trail stamp this many position deltas behind
    private float _boundMin = 1f;             // unk1B0: the smallest fall that bounces
    private float _expand = 1f;               // unk158
    private int _node;                        // the graph tracer's index
    private int _stuck; private float _lastDist = float.NaN;   // unk128 / unk12C
    private int _washTicks;                   // ticks since the last water hit kept the wash emitter open
    private bool _dieStarted;                 // TNerveGorogoroDie's first pass done
    private Vector3 _lastDelta;               // mPositionDelta of the last tick, units
    private Skeleton3D _skel; private SmsBoneRoll _roll; private ShaderMaterial _bodyMat;
    private SmsNpcEffect _doroFx, _washFx;
    private readonly Dictionary<string, BoneAttachment3D> _fxCache = new();
    /// <summary>For the drives.</summary>
    public float RollAngle => _rollAngle;
    public bool InFlight => _inFlight;
    public float Wash => _bodyMat != null ? _bodyMat.GetShaderParameter("wash").AsSingle() : 1f;
    public int HitPoints => _hp;
    public int Bounces { get; private set; }
    public int TrailStamps { get; private set; }
    public bool Melting => _melt && IsNerve(NerveGoroDie);
    public bool Dying => IsNerve(NerveGoroDie);
    public string CurrentNerveName => NerveName(_cur);
    public bool DoroEmitting => _doroFx != null && IsInstanceValid(_doroFx) && _doroFx.IsEmitting;
    public bool WashEmitting => _washFx != null && IsInstanceValid(_washFx) && _washFx.IsEmitting;

    protected override void Init()
    {
        _flags150 = 0x31;
        DoroFxScene ??= LoadFx("GoroDoro"); WashFxScene ??= LoadFx("GoroWash"); FumiFxScene ??= LoadFx("GoroFumi"); TokeFxScene ??= LoadFx("GoroToke");
        if (_model != null)
        {
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (n is not MeshInstance3D mi || mi.Mesh == null) continue;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                    if (mi.GetActiveMaterial(i) is ShaderMaterial sm && sm.Shader != null && sm.Shader.ResourcePath.EndsWith("GorogoroBody.gdshader"))
                    { _bodyMat = (ShaderMaterial)sm.Duplicate(); mi.SetSurfaceOverrideMaterial(i, _bodyMat); }
            }
            if (_skel != null && !Engine.IsEditorHint())
            {
                _roll = new SmsBoneRoll { Name = "Roll", BoneName = RollJoint };
                _skel.AddChild(_roll);
            }
        }
        ApplyGoopKnobs();
        if (DebugPrint) GD.Print($"[SMS gorogoro] {Name}: fx doro {DoroFxScene != null} wash {WashFxScene != null} fumi {FumiFxScene != null} toke {TokeFxScene != null}; skel {_skel?.Name}; body mat {_bodyMat != null}");
    }
    private static PackedScene LoadFx(string name) => ResourceLoader.Exists(FxDir + name + ".tscn") ? GD.Load<PackedScene>(FxDir + name + ".tscn") : null;

    protected override void EditorPreview() => ApplyGoopKnobs();

    /// <summary>The goop sheet and the colour knobs onto the body's material (the gatekeeper's names).</summary>
    private void ApplyGoopKnobs()
    {
        if (_bodyMat == null) return;
        if (_goopTexture != null) _bodyMat.SetShaderParameter("goop_texture", _goopTexture);
        if (_goopMode != SmsGateKeeper.GoopColorModes.FromMaterial) { _bodyMat.SetShaderParameter("goop_colour_mode", (int)_goopMode); _bodyMat.SetShaderParameter("goop_colour", _goopColor); }
    }
    /// <summary>SMSSetEmitterPolColor: the sheet's light / dark pair for the goop effects (SmsGateKeeper.GoopPair's rule).</summary>
    private bool GoopPair(out Color light, out Color dark)
    {
        Color prm = SmsPollutionLayer.GoopLightDefault, env = SmsPollutionLayer.GoopDarkDefault;
        Texture2D sheet = _goopTexture;
        if (sheet == null && _bodyMat != null) { var v = _bodyMat.GetShaderParameter("goop_texture"); if (v.VariantType == Variant.Type.Object) sheet = v.As<Texture2D>(); }
        bool bianco = sheet == null || sheet.ResourcePath.EndsWith("goop_mark_bianco.png");
        if (!bianco) SmsPollutionLayer.MeasureGoopColors(sheet, out prm, out env);
        switch (_goopMode)
        {
            case SmsGateKeeper.GoopColorModes.Tint: light = prm * _goopColor; dark = env * _goopColor; light.A = 1f; dark.A = 1f; return true;
            case SmsGateKeeper.GoopColorModes.Recolour: light = _goopColor; dark = _goopColor * 0.4f; light.A = 1f; dark.A = 1f; return true;
            default: light = prm; dark = env; light.A = 1f; dark.A = 1f; return !bianco;
        }
    }

    protected override void Reset()
    {
        base.Reset();
        _rollAngle = Rand() * 360f;           // TRollEnemy::reset
        _expand = 1f; _bound = 0f; _inFlight = false; _trailBack = -10f; _boundMin = 1f;
        _node = 0; _stuck = 0; _lastDist = float.NaN; _washTicks = 0; _lastDelta = Vector3.Zero; Bounces = 0; TrailStamps = 0; _dieStarted = false;
        _marchSpeed *= SpeedScale;
        SetWash(1f);
        StopFx(ref _doroFx); StopFx(ref _washFx);
    }
    private void SetWash(float w) => _bodyMat?.SetShaderParameter("wash", Mathf.Clamp(w, 0f, 1f));
    private static void StopFx(ref SmsNpcEffect fx) { if (fx != null && IsInstanceValid(fx)) fx.Stop(); fx = null; }

    // ------------------------------------------------------------------------------------------------------------------ the look
    /// <summary>calcRootMatrix: the body at (x, y + GroundOffsetY -50 x unk158, z) facing the heading at the scale; the roll on joint 1 while
    /// rolling (RollEnemyBodyCallback: isRolling = the RollOnGraph nerve or the melt clip).</summary>
    protected override void ApplyLook()
    {
        base.ApplyLook();
        if (_model == null) return;
        // MsMtxSetXYZRPH(base, x, y + GroundOffsetY (-50) x unk158, z): the model origin 50 u under the body point -- except in the die nerve, which
        // takes TSpineEnemy::calcRootMatrix (the plain body point), so the squash and the melt play ON the ground, not under it
        if (!IsNerve(NerveGoroDie))
        {
            if (IsInsideTree()) _model.GlobalPosition += Vector3.Up * (Prm.GroundOffsetY * _expand / U);
            else _model.Position += Vector3.Up * (Prm.GroundOffsetY * _expand / U);
        }
        if (_roll != null)
        {
            bool rolling = IsNerve(NerveRollOnGraph) || IsBck(BckMelt);
            _roll.WorldAxis = _model.GlobalBasis.X.Normalized();
            _roll.Degrees = rolling ? _rollAngle : 0f;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ the goal
    /// <summary>goToShortestNextGraphNode: the graph's next node in order (the last one left behind: straight on); no graph: 2000 u ahead.</summary>
    private void PickNextGoal()
    {
        if (Graph != null && Graph.GetChildCount() > 0 && _node < Graph.GetChildCount())
        {
            if (Graph.GetChild(_node) is Node3D n3) { SetGoalPoint(n3.GlobalPosition * U); _node++; return; }
            _node++;
        }
        SetGoalPoint(Pos() + PolarXZ(_yaw, 2000f));
    }
    /// <summary>TRollEnemy::isReachedToGoalXZ: within 200 u, on the ground plane unless in flight.</summary>
    private bool ReachedGoalXZ()
    {
        Vector3 d = Goal() - Pos();
        if (!_inFlight) d.Y = 0f;
        return d.Length() < 200f;
    }

    // ------------------------------------------------------------------------------------------------------------------ the walk
    /// <summary>TGorogoro::walkBehavior -> TRollEnemy::walkBehavior: the low turn, the walk (not in flight), the bounce bookkeeping, the stuck
    /// kill, the roll angle, the water-surface melt.</summary>
    private void WalkBehaviorGoro(int mode, float k)
    {
        _turnSpeed = Prm.TurnSpeedLow;
        if (!_inFlight)
        {
            float before = (Goal() - Pos()).Length();
            WalkBehaviorBase(mode, k);
            // unk128 / unk12C (walkToCurPathNode): the distance to the goal unchanged by 100 u for 300 ticks = stuck
            if (float.IsNaN(_lastDist) || MathF.Abs(before - _lastDist) >= 100f) { _stuck = 0; _lastDist = before; } else _stuck++;
        }
        float h = Pos().Y - _groundY;
        if (_airborne && h > 20f)
        {
            float bound = Mathf.Clamp(h / BoundVal, 0f, Prm.BoundVYMax);
            if (_bound < bound) _bound = bound;
        }
        else if (!_airborne)
        {
            if (!OverWaterSurface())
            {
                _inFlight = false;
                if (_bound > _boundMin)
                {
                    Bound();
                    _vel = new Vector3(0f, _bound, 0f);
                    _airborne = true;
                    GlobalPosition += Vector3.Up * (5f / U);
                    _bound = 0f;
                    BounceSound?.Play();
                }
            }
        }
        if (h < 30f && RollSound != null && RollSound.Stream != null && !RollSound.Playing && !_airborne) RollSound.Play();
        if (_stuck > 300 && !IsNerve(NerveGoroDie))
        {
            if (DebugPrint) GD.Print($"[SMS gorogoro] {Name}: stuck 300 ticks -> dead");
            Kill();
            return;
        }
        _rollAngle = Wrap360(_rollAngle + 0.4f * _marchSpeed * k);
        if (!IsNerve(NerveGoroDie) && h < 10f && OverWaterSurface())
        {
            _melt = true; _noReward = true;                     // LIVE_FLAG_UNK10000 | UNK20000 -> kill: the melt
            if (DebugPrint) GD.Print($"[SMS gorogoro] {Name}: onto a water surface -> the melt");
            Kill();
        }
    }
    /// <summary>TRollEnemy::bound: requestPolluteModel (the splat model at the body, scaled) + the rumble when Mario stands near.</summary>
    private void Bound()
    {
        Bounces++;
        SmsPollutionMap.Pollute(new Vector3(GlobalPosition.X, _groundY / U, GlobalPosition.Z), SplatUnits * _scale);
        if (_mario != null && IsInstanceValid(_mario) && _mario.IsOnFloor() && (_mario.GlobalPosition - GlobalPosition).Length() < 15f) Input.StartJoyVibration(0, 0.4f, 0.7f, 0.17f);   // SMSRumbleMgr->start(0x15, 10) when Mario stands near
    }
    /// <summary>mGroundPlane->isWaterSurface(): the body stands in an sms_water area whose surface is above its feet (the lake).</summary>
    private bool OverWaterSurface()
    {
        var space = GetWorld3D().DirectSpaceState;
        var q = new PhysicsPointQueryParameters3D { Position = GlobalPosition + Vector3.Up * 0.05f, CollideWithAreas = true, CollideWithBodies = false, CollisionMask = uint.MaxValue };
        foreach (var hit in space.IntersectPoint(q, 8))
        {
            if (hit["collider"].As<GodotObject>() is not Area3D area) continue;
            if (!area.IsInGroup("sms_water") && !area.HasMeta("sms_water")) continue;
            float surface = area.HasMeta("sms_water_surface_y") ? (float)area.GetMeta("sms_water_surface_y").AsDouble() : area.GlobalPosition.Y;
            if (surface >= GlobalPosition.Y) return true;
        }
        return false;
    }

    /// <summary>TRollEnemy::setBehavior: the trail -- within 50 u of the ground, alive, a polluter: stampGround(1, the point 10 deltas back, 32 x
    /// PolluteRange) every PolluteInterval ticks (here every fourth: see the header). Also the effects' upkeep.</summary>
    protected override void SetBehavior()
    {
        _lastDelta = _lin + _vel;
        float h = Pos().Y - _groundY;
        bool dying = IsNerve(NerveGoroDie) || _dead;
        if (LeavesTrail && !dying && h <= 50f && (_tickCount & 3) == 0 && TrailScale > 0f)
        {
            Vector3 back = _lastDelta * _trailBack; back.Y = 0f;
            Vector3 at = new Vector3(GlobalPosition.X + back.X / U, _groundY / U, GlobalPosition.Z + back.Z / U);
            SmsPollutionMap.Pollute(at, 32f * Prm.PolluteRange * TrailScale);
            TrailStamps++;
        }
        // PKH_DORO: bound to the root every frame the head is within 30 u of the ground and not dying (calcRootMatrix)
        bool wantDoro = !dying && h < 30f && _model != null && _model.Visible;
        if (wantDoro && (_doroFx == null || !IsInstanceValid(_doroFx)) && (_tickCount & 3) == 0) _doroFx = SpawnBound(DoroFxScene);
        if (!wantDoro && _doroFx != null) StopFx(ref _doroFx);
        // PKH_WASH: kept alive while hits come
        if (_washTicks > 0) { _washTicks--; if (_washFx == null || !IsInstanceValid(_washFx)) _washFx = SpawnBound(WashFxScene); }
        else if (_washFx != null) StopFx(ref _washFx);
    }
    private SmsNpcEffect SpawnBound(PackedScene scene)
    {
        if (scene == null || _skel == null) return null;
        var fx = SmsNpcEffect.Spawn(scene, _skel, RootJoint, _scale, _fxCache, Name);
        if (fx != null && GoopPair(out Color light, out Color dark)) fx.SetGoopColors(light, dark);
        return fx;
    }

    /// <summary>TRollEnemy::isCollidMove: a STOP_ROCK kills it (none here); anything else is ATTACKED and never pushes the head.</summary>
    protected override bool IsCollidMove(Node3D other) => false;
    /// <summary>TRollEnemy::attackToMario: the ATTACK message only -- no PostAttack hop, the head rolls on through him.</summary>
    protected override void AttackToMario() => SendAttackMsgToMario();
    /// <summary>No wall hop (TWalkerEnemy::moveObject's TNerveSmallEnemyJump at JumpForce 60 x 0.8 = 48 u/tick would leap the head 100 m; the game's
    /// jump nerve is guarded by its once-flag and the roll enemy's wall answer is the stuck rule). DEVIATION, logged.</summary>
    protected override bool WallHopAllowed() => false;
    /// <summary>For the drives: the effect spawns.</summary>
    public int FumiBursts { get; private set; }
    public int TokeSpawns { get; private set; }

    // ------------------------------------------------------------------------------------------------------------------ water
    /// <summary>receiveMessage(SPRAYED_BY_WATER) with TGorogoro::behaveToWater: the mark; the cooldown reset (every hit counts); decHpByWater (1 for the hose)
    /// floored at 1 -- it never dies of water; K0.a = hp / max onto the body (the wash); PKH_WASH kept alive.</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead || IsNerve(NerveGoroDie)) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        _sprayCooldown = 0;
        _hp = Math.Max(1, _hp - 1);
        SetWash(_hp / (float)Prm.HitPointMax);
        _washTicks = 8;
        return true;
    }
    protected override void BehaveToWater() { }

    // ------------------------------------------------------------------------------------------------------------------ death
    /// <summary>TGorogoro::kill: the roll angle zeroed; already dying -> nothing; else the spine reset into GorogoroDie (then the default).</summary>
    public override void Kill()
    {
        if (_dead || IsNerve(NerveGoroDie)) return;
        _rollAngle = 0f;
        ResetSpine();
        SetNext(NerveGoroDie);
        PushAfterCurrent(DefaultNerve);
    }
    protected override void SetDeadAnm()
    {
        if (_skel != null && FumiFxScene != null)
        {
            int b = _skel.FindBone(RollJoint);
            Vector3 at = b >= 0 ? (_skel.GlobalTransform * _skel.GetBoneGlobalPose(b)).Origin : GlobalPosition;
            if (SpawnBurst(FumiFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), at)) != null) FumiBursts++;
            if (DebugPrint) GD.Print($"[SMS gorogoro] {Name}: the squash -- PKH_FUMI at the center joint {at}");
        }
        SetBck(BckHumi);
        DownSound?.Play();
    }
    protected override void SetMeltAnm()
    {
        SetBck(BckMelt);
        if (DebugPrint) GD.Print($"[SMS gorogoro] {Name}: the melt clip; TOKE scene {(TokeFxScene == null ? "NULL" : TokeFxScene.ResourcePath)}; parent {GetParent()?.Name}");
        if (TokeFxScene != null && TokeFxScene.Instantiate() is SmsNpcEffect fx)
        {
            fx.TopLevel = true;
            GetParent().AddChild(fx);
            Vector3 fwd = PolarXZ(_yaw, 1f);
            fx.GlobalTransform = new Transform3D(Basis.LookingAt(fwd, Vector3.Up).Scaled(Vector3.One * _scale), new Vector3(GlobalPosition.X, _groundY / U, GlobalPosition.Z));
            if (GoopPair(out Color light, out Color dark)) fx.SetGoopColors(light, dark);
            fx.Start(_scale);
            TokeSpawns++;
            if (DebugPrint) GD.Print($"[SMS gorogoro] {Name}: the melt -- PKH_TOKE at the ground {fx.GlobalPosition}");
        }
        SinkSound?.Play();
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveRollOnGraph:                               // TNerveGorogoroRollOnGraph
                if (_time == 0)
                {
                    if (_airborne && _vel.LengthSquared() > 0f)
                    {
                        // generateByGateKeeper's lob: in flight until the landing, the goal where it lands (toward Mario or the "graph")
                        _inFlight = true;
                        float ticks = _vel.Y > 0f ? 2f * _vel.Y / Math.Max(GravityY(), 1e-4f) : 0f;
                        SetGoalPoint(Pos() + new Vector3(_vel.X, 0f, _vel.Z) * ticks);
                    }
                    else PickNextGoal();
                    SetBck(BckMove);
                }
                if (ReachedGoalXZ()) PickNextGoal();           // jumpToNextGraphNode -> flagJump: the jump edges are not ported
                WalkBehaviorGoro(2, 1f);
                return false;

            case NerveGoroDie:                                   // TNerveGorogoroDie
                if (_time < 2)
                {
                    // the game tests getTime() < 2, not == 0: a kill from inside the running nerve (the water surface, the stuck rule) lands here with the
                    // time already 1 -- the spine's increment ran after the switch
                    if (!_dieStarted)
                    {
                        _dieStarted = true;
                        SetCollisionLayerValue(1, false);        // onHitFilter(NO_COLLISION)
                        StopFx(ref _doroFx); StopFx(ref _washFx);
                        if (_melt) SetMeltAnm();
                        else
                        {
                            SmsPollutionMap.Pollute(new Vector3(GlobalPosition.X, _groundY / U, GlobalPosition.Z), SplatUnits * _scale);   // requestPolluteModel
                            SetDeadAnm();
                            SetDeadEffect();
                        }
                    }
                }
                else if (BckEnded() || _time > 360)
                {
                    GenRandomItem();
                    // TNerveSmallEnemyDie's first tick, right after: the 0x20 stamp (unk150 & 0x20) for a death that is not a melt
                    if (!_melt && DeathStampScale > 0f)
                        SmsPollutionMap.Pollute(new Vector3(GlobalPosition.X, _groundY / U, GlobalPosition.Z), P.StampRange * 32f * _expand * DeathStampScale);
                    Dead();
                    return true;
                }
                if (_melt) WalkBehaviorGoro(2, 0.5f);
                return false;
        }
        return base.ExecuteNerve(n);
    }

    protected override void Dead()
    {
        StopFx(ref _doroFx); StopFx(ref _washFx);
        if (RollSound != null && RollSound.Playing) RollSound.Stop();
        base.Dead();
    }
}
