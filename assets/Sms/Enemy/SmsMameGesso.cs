using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// SMALL BLOOPER -- TMameGesso (Enemy/mameGesso.cpp; JP まめゲッソー, "bean Blooper") on <see cref="SmsSmallEnemy"/> with the shipped /enemy/MameGesso.prm
/// (SMS pass 140, feature/enemy-ricco). Ricco Harbor's hopping squid: it bounds along the docks and over the water, stuns under the hose or a
/// stomp (then Mario can pick it up and throw it), flattens into a floating PLATFORM when sprayed on the water, and only a ground pound kills it.
///
/// THE GAME. A TWalkerEnemy (unk150 0x11, ACTOR_TYPE_MAME_GESSO): march 3..5 u/tick, turn 1.3..1.5 deg/tick, body scale 1, one hit point, attack 100 x
/// 90 / damage 100 x 120 cylinders, FreezeWait 500; its own prm: JumpWanderSpeed 10 (the hop's vy: 333 u up, 133 ticks in the air at gravity 0.15),
/// ReturnJumpDist 200 / ReturnJumpSp 10, WaitTimeOnGround 100 / InWater 200 (+ 10 x the instance index), PickUpTime 500, ThrownVY 12 at ThrownRateXZ 1 x
/// Mario's throw power under ThrownGravityY 0.15, ObjectRecoverTime 3000, CollisionScale 2, GenerateInterval 300 (a dead placed one is reset -- its
/// placed point + 20 u -- after that many ticks). reset() puts it 20 u over its placed point. The nerves: GRAPHJUMPWANDER (the default): run_start1 (on
/// a water surface run_start2) and the walk toward the goal (walkBehavior(2) on the ground, (3) = turn only in the air); past frame 26 on the ground the
/// HOP: goToShortestNextGraphNode and calcVelocityToJumpToY(goal, JumpWanderSpeed, gravity) (SMSCalcJumpVelocityY: vy = the speed, the XZ speed = the
/// distance over the ticks the parabola takes to come back down to the goal's height; on a water surface the water column effect), +10 u, airborne; or,
/// off its graph (unk1EC 0, after a throw landed over 300 u from the goal), a ReturnJumpDist hop toward the goal at ReturnJumpSp until the goal is
/// reached; the start clip ends -> run_loop1; landed -> run_end1 (run_end2 and the column on water); that clip's end -> WAIT: wait1 (wait4 on water) for
/// WaitTimeOnGround / InWater (+ 10 x the index) past the clip's end -> GraphJumpWander. DAMAGE (the hose: behaveToWater while wandering or waiting; a
/// stomp: kill): the XZ velocity dropped, turn1; landed on water -> OBJECT; the clip ended: airborne -> fall1; landed -> on water OBJECT else JITABATA:
/// setFreezeAnm (unk164 = 1 -- the stun: no attack, Mario may take it; wait2, the flail) until FreezeWait, then return1 and (the goal over 300 u away ->
/// unk1EC = 0) WAIT with the stun off. kill(): wandering or waiting -> Damage then TRAMPLE pushed (Trample first: the hit clip -- a hip drop press_upside1,
/// anything else turn1, press_downside1 if already flailing -- then Damage); flailing -> Trample alone. isHitValid: a HIP_DROP while wandering -> the Die
/// nerve pushed and FALSE (no bounce; setDeadAnm only sets the stun: the running clip plays out, then the body goes); anything else true. PICKUP
/// (behaveToTaken, in his hands): past PickUpTime with the clip ended the stun is off and it ATTACKS him in his hands. THROWN (behaveToRelease, only while
/// stunned): (ThrownRateXZ x his throw power along his yaw, ThrownVY), +2 u, airborne; landed on water -> Object (stun off); on land land1 then the stun
/// off. OBJECT (the platform): land2 then wait3; onHitFilter(NO_COLLISION) unless Mario is swimming (then the cylinders x 2.5 and doKeepDistance pushes
/// him off); a two-triangle TMapCollisionMove quad, 2 x CollisionScale x AttackRadius square at joint 1 + 90 u, re-entered every tick he is not swimming;
/// mPosition.y = the ground (the surface) - 80 u while the model is drawn at the surface + unk1E0 (sinking 0 -> -20 u); past ObjectRecoverTime: water_return1
/// (unk1E0 back to 0) then the quad re-entered, the collision on, the body back at the surface -> Wait. attackToMario: never while stunned or a
/// platform. isCollidMove false only while wandering on the ground. getGravityY: JumpWanderGravityY as a platform, ThrownGravityY thrown, else mGravity.
///
/// THE PORT. The hops ride the base's bind (gravity, the floor through MoveAndSlide). A WATER SURFACE is a floor here too (the game's water triangles are
/// ground; his water is an sms_water Area3D): the surface -- the area's sms_water_surface_y or its box top, as Mario reads it -- found within 0.5 m under
/// the body and above the real floor is landed on and rested on by the species (SetBehavior pins the vertical velocity each tick; MoveObjectPre keeps
/// the airborne flag down across the frame boundary the base flips it at). The platform is the scene's Platform StaticBody3D (a 400 x 20 x 400 u box, its
/// top at the body joint + <see cref="PlatformHeightUnits"/>), enabled and placed every tick of Object, the body excepted from it. The water column
/// (TEffectColumWater, an effect actor with its own model) is the small splash burst by default (<see cref="WaterColumnFxScene"/>). The goal: a random
/// other node of <see cref="SmsSmallEnemy.Graph"/>, or (no graph) a random point within WanderRadiusUnits of the placed point (the base's GraphWander rule;
/// the game walks its graph's shortest next edge). DEVIATIONS (logged): <see cref="ObjectRecoverTicks"/>; the hit-react clips re-start once (the game sets
/// the freeze clip on three ticks); a water column only where a splash scene is; the instance index is a hash (the manager's slot); clipped-out return
/// hops do not die; Yoshi and the sounds are slots.
/// </summary>
public partial class SmsMameGesso : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ MameGesso.prm
    public class GessoParams : Params
    {
        public float JumpWanderGravityY = 0.5f, JumpWanderSpeed = 15f, ThrownGravityY = 0.5f, ThrownVY = 50f, ThrownRateXZ = 0.5f, ObjectScale = 2f, CollisionScale = 1f, ReturnJumpDist = 200f, ReturnJumpSp = 10f;
        public int PickUpTime = 100, GenerateInterval = 300, WaitTimeOnGround = 50, WaitTimeInWater = 200, ObjectRecoverTime = 1000;
    }
    /// <summary>The shipped /enemy/MameGesso.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly GessoParams Prm = new GessoParams
    {
        HeadHeight = 120f, BodyRadius = 5f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 1, InstanceNum = 9, ActiveEnemyNum = 9,
        JumpForce = 10f, SearchLength = 800f, SearchHeight = 600f, SearchAware = 400f, SearchAngle = 90f, GiveUpLength = 2000f, GiveUpHeight = 650f,
        AttackWait = 120, FreezeWait = 500, DamageRadius = 100f, DamageHeight = 120f, AttackRadius = 100f, AttackHeight = 90f,
        TurnSpeedLow = 1.3f, TurnSpeedHigh = 1.5f, BodyScaleLow = 1f, BodyScaleHigh = 1f, GenItemRate = 50f, GenEggRate = 10f, WaitTime = 100,
        StampRange = 5f, ZigzagCycle = 1000f, ZigzagAngle = 30f, MarchSpeedLow = 3f, MarchSpeedHigh = 5f,
        JumpWanderGravityY = 0.1f, JumpWanderSpeed = 10f, PickUpTime = 500, ThrownGravityY = 0.15f, ThrownVY = 12f, ThrownRateXZ = 1f, GenerateInterval = 300,
        WaitTimeOnGround = 100, WaitTimeInWater = 200, ObjectRecoverTime = 3000, ObjectScale = 2f, CollisionScale = 2f, ReturnJumpDist = 200f, ReturnJumpSp = 10f,
    };
    protected override Params P => Prm;
    /// <summary>TMameGesso::mBaseHoseiTransY: how far the flattened body sinks (the model), units.</summary>
    public const float BaseHoseiTransY = -20f;
    /// <summary>The Object nerve's unk1E8: the body point this far under the surface while it is a platform.</summary>
    public const float ObjectSinkUnits = 80f;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>How long it stays a platform after flattening on the water, in ticks (120 a second; the game's ObjectRecoverTime 3000 = 25 s).</summary>
    [Export(PropertyHint.Range, "60,6000,10")] public int ObjectRecoverTicks = 3000;
    /// <summary>The standing surface's height over the flattened body's joint, units (the game's quad sits 90 u = 1.2 m up).</summary>
    [Export(PropertyHint.Range, "0,200,1")] public float PlatformHeightUnits = 90f;

    [ExportGroup("Effects (empty = the default)")]
    /// <summary>TEffectColumWater (the water column actor) at a hop or a landing on the water and at the flattening: any SmsFxBurst / one-shot GPUParticles3D
    /// scene; empty = assets/Sms/Fx/WaterSplashSmall.tscn (Mario's small splash, the stand-in).</summary>
    [Export] public PackedScene WaterColumnFxScene;
    public const string DefaultColumnScene = "res://assets/Sms/Fx/WaterSplashSmall.tscn";

    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>The bas table's sounds: run_start2 (the hop off the water), run_end2 / land2 (the landing and the flop onto the water), turn1 (the hit),
    /// water_return1 (the recovery).</summary>
    [Export] public AudioStreamPlayer3D WaterHopSound, WaterLandSound, HitSound, RecoverSound;

    // ------------------------------------------------------------------------------------------------------------------ the clips (default.bmd's 19 bcks, the archive's order)
    // Godot's glTF import drops a "_loop" from a clip's name (and loops it): mamegeso_run_loop1 would collide with mamegeso_run1, so the glb's clip was
    // renamed mamegeso_runloop1 before import (the bck is run_loop1)
    private static readonly string[] Names = { "mamegeso_fall1", "mamegeso_land1", "mamegeso_land2", "mamegeso_press_downside1", "mamegeso_press_upside1", "mamegeso_return1",
        "mamegeso_run1", "mamegeso_run2", "mamegeso_run_end1", "mamegeso_run_end2", "mamegeso_runloop1", "mamegeso_run_start1", "mamegeso_run_start2", "mamegeso_turn1",
        "mamegeso_wait1", "mamegeso_wait2", "mamegeso_wait3", "mamegeso_wait4", "mamegeso_water_return1" };
    private static readonly int[] Frames = { 20, 9, 49, 29, 39, 59, 90, 100, 31, 31, 30, 67, 67, 17, 100, 60, 60, 60, 70 };
    private static readonly bool[] Loops = { true, false, false, false, false, false, true, true, false, false, true, false, false, false, true, true, true, true, false };
    protected const int BckFall = 0, BckLand1 = 1, BckLand2 = 2, BckPressDown = 3, BckPressUp = 4, BckReturn = 5, BckRun1 = 6, BckRun2 = 7, BckRunEnd1 = 8, BckRunEnd2 = 9,
        BckRunLoop = 10, BckRunStart1 = 11, BckRunStart2 = 12, BckTurn = 13, BckWait1 = 14, BckWait2 = 15, BckWait3 = 16, BckWait4 = 17, BckWaterReturn = 18;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;
    /// <summary>The bmd's joints: root_1_2, bodyjnt1 (joint 1: the platform's anchor), legnull2, legnull1, bodyjnt2.</summary>
    public const string BodyJoint = "bodyjnt1";

    // ------------------------------------------------------------------------------------------------------------------ nerves
    protected const int NerveGraphJumpWander = NerveSpecies, NerveGDamage = NerveSpecies + 1, NerveJitabata = NerveSpecies + 2, NerveGTrample = NerveSpecies + 3,
        NerveGPickUp = NerveSpecies + 4, NerveGThrown = NerveSpecies + 5, NerveObject = NerveSpecies + 6, NerveGWait = NerveSpecies + 7;
    private static readonly string[] GessoNerveNames = { "GraphJumpWander", "Damage", "Jitabata", "Trample", "PickUp", "Thrown", "Object", "Wait" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + GessoNerveNames.Length ? GessoNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveGraphJumpWander;

    // ------------------------------------------------------------------------------------------------------------------ state
    private bool _stunned;                     // unk164: the freeze -- no attack, Mario may take it
    private bool _hipDropped;                  // unk1D0
    private bool _notHipDrop = true;           // unk1D1: the Trample nerve's clip choice
    private bool _onGraph = true;              // unk1EC: following the graph (0 = the return hops after a throw)
    private float _objHosei, _objBaseY, _objSink;   // unk1E0 / unk1E4 / unk1E8
    private int _instanceIdx;                  // the manager's slot (the wait's + 10 x index)
    private float _throwYaw, _throwPower;      // SMS_GetMarioAngleY / SMS_GetMarioThrowPower at the release
    private bool _waterUnder; private float _waterSurfU;   // mGroundPlane->isWaterSurface(): a water surface at or just under the body, over the real floor
    private bool _onWater, _landedWater;       // resting on the surface; the landing tick (the velocity zeroed next tick, as the bind's)
    private float _dispY;                      // the ticks' vertical displacement so far this physics frame, units
    private Skeleton3D _skel; private StaticBody3D _platform; private CollisionShape3D _platformShape;
    /// <summary>For the drives.</summary>
    public bool Stunned => _stunned;
    public bool OnGraph => _onGraph;
    public bool OnWaterSurface => _waterUnder;
    public bool RestingOnWater => _onWater;
    public float WaterSurfaceY => _waterUnder ? _waterSurfU / U : float.NaN;
    public bool IsPlatform => IsNerve(NerveObject);
    public bool PlatformEnabled => _platformShape != null && !_platformShape.Disabled;
    public float PlatformTopY => _platform != null ? _platform.GlobalPosition.Y + 0.135f : float.NaN;
    public float ObjectHosei => _objHosei;
    public string CurrentNerveName => NerveName(_cur);
    public int ColumnBursts { get; private set; }
    public bool Dying => IsNerve(NerveDie);

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override void Init()
    {
        _flags150 = 0x11;
        _instanceIdx = (int)(GetInstanceId() % 9);
        if (_model != null) foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; if (_skel != null) break; }
        _platform = GetNodeOrNull<StaticBody3D>("Platform");
        _platformShape = _platform?.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (_platform != null) { AddCollisionExceptionWith(_platform); _platform.TopLevel = true; }
        if (WaterColumnFxScene == null && ResourceLoader.Exists(DefaultColumnScene)) WaterColumnFxScene = GD.Load<PackedScene>(DefaultColumnScene);
        if (DebugPrint) GD.Print($"[SMS gesso] {Name}: skel {_skel?.Name}; platform {_platformShape != null}; column fx {WaterColumnFxScene?.ResourcePath}; index {_instanceIdx}");
    }

    /// <summary>TMameGesso::reset: TWalkerEnemy::reset, the flags, the body 20 u over its placed point.</summary>
    protected override void Reset()
    {
        base.Reset();
        _stunned = false; _hipDropped = false; _notHipDrop = true; _onGraph = true;
        _objHosei = 0f; _objBaseY = 0f; _objSink = 0f;
        _waterUnder = false; _onWater = false; _landedWater = false; _dispY = 0f;
        PlatformOff();
        GlobalPosition += Vector3.Up * (20f / U);
        _airborne = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Engine.IsEditorHint()) _dispY = 0f;
        base._PhysicsProcess(delta);
        if (_onWater && !_dead && _holder == null) _airborne = false;      // the base's step raised it (no floor under a floating body): resting on the surface is grounded
    }

    // ------------------------------------------------------------------------------------------------------------------ the water surface as a floor
    /// <summary>Mario's SmsWaterSurfaceOf: the area's sms_water_surface_y, else its first BoxShape3D's top, else its origin (metres).</summary>
    private static float WaterSurfaceOf(Area3D area)
    {
        if (area.HasMeta("sms_water_surface_y")) return (float)area.GetMeta("sms_water_surface_y").AsDouble();
        foreach (Node child in area.GetChildren())
            if (child is CollisionShape3D cs && cs.Shape is BoxShape3D box) return (cs.GlobalTransform * new Vector3(0f, box.Size.Y * 0.5f, 0f)).Y;
        return area.GlobalPosition.Y;
    }
    /// <summary>The highest sms_water surface at the body's XZ within 0.5 m under <paramref name="posM"/> (a point query half a metre down), in metres.</summary>
    private bool WaterSurfaceAt(Vector3 posM, out float surfM)
    {
        surfM = float.NegativeInfinity;
        var space = GetWorld3D().DirectSpaceState;
        var q = new PhysicsPointQueryParameters3D { Position = posM + Vector3.Down * 0.5f, CollideWithAreas = true, CollideWithBodies = false, CollisionMask = uint.MaxValue };
        bool any = false;
        foreach (var hit in space.IntersectPoint(q, 8))
        {
            if (hit["collider"].As<GodotObject>() is not Area3D area) continue;
            if (!area.IsInGroup("sms_water") && !area.HasMeta("sms_water")) continue;
            float s = WaterSurfaceOf(area);
            if (!any || s > surfM) { surfM = s; any = true; }
        }
        return any;
    }
    /// <summary>mGroundHeight: the water surface when the body is over one, else the floor (units).</summary>
    private float GroundHeightU() => _waterUnder ? _waterSurfU : _groundY;

    /// <summary>TMameGesso::moveObject's head: the water under the body this tick (the ground plane's isWaterSurface), the airborne flag held down while
    /// resting on it (the base's step raised it at the frame's end: there is no floor under a floating body), the landing tick's velocity zeroed.</summary>
    protected override void MoveObjectPre()
    {
        Vector3 pM = GlobalPosition + Vector3.Up * (_dispY / U);
        float surfM = float.NegativeInfinity;
        _waterUnder = !Carried && WaterSurfaceAt(pM, out surfM) && surfM * U > _groundY + 1f;
        if (_waterUnder) _waterSurfU = surfM * U; else _onWater = false;
        if (_onWater) _airborne = false;
        if (_landedWater) { _landedWater = false; _vel = Vector3.Zero; OnLanded(); }
    }

    /// <summary>TMameGesso::moveObject's tail: a platform's body point pinned at the surface - 80 u; else the water floor: a fall meeting the surface lands on
    /// it this tick (the vertical velocity = the gap), a resting body is held on it (the bind's velocity zero and ground snap), a rise leaves it.</summary>
    protected override void SetBehavior()
    {
        if (Carried) return;
        if (IsNerve(NerveObject))
        {
            _vel = Vector3.Zero; _lin = Vector3.Zero; _airborne = false; _onWater = true;
            GlobalPosition = new Vector3(GlobalPosition.X, (_objBaseY - _objSink) / U, GlobalPosition.Z);
            _dispY = 0f;
            UpdatePlatform();
            return;
        }
        if (_waterUnder)
        {
            float yU = GlobalPosition.Y * U + _dispY;
            float nextY = yU + _lin.Y + _vel.Y;
            if (_onWater)
            {
                if (_vel.Y > 0f) _onWater = false;                               // the hop: off the surface
                else { _vel.Y = _waterSurfU - yU - _lin.Y; _vel.X = 0f; _vel.Z = 0f; _airborne = false; }
            }
            else if (_vel.Y <= 0f && nextY <= _waterSurfU + 0.05f)
            {
                _vel.Y = _waterSurfU - yU - _lin.Y;                              // onto the surface this tick (bind: nextPos.y = mGroundHeight)
                _onWater = true; _landedWater = true; _airborne = false;
            }
        }
        _dispY += _lin.Y + _vel.Y;
    }

    // ------------------------------------------------------------------------------------------------------------------ the platform (TMapCollisionMove)
    /// <summary>calcObjCollision + entryObjCollision: the quad at joint 1 + 90 u, 2 x CollisionScale x AttackRadius x the body scale square -- the scene's box
    /// placed with its top there and enabled.</summary>
    private void UpdatePlatform()
    {
        if (_platform == null || _platformShape == null) return;
        Vector3 at = JointWorld(BodyJoint);
        float top = at.Y + PlatformHeightUnits / U;
        float half = Prm.CollisionScale * Prm.AttackRadius * _scale / U;
        if (_platformShape.Shape is BoxShape3D box && MathF.Abs(box.Size.X - half * 2f) > 0.01f) box.Size = new Vector3(half * 2f, 0.27f, half * 2f);
        _platform.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(at.X, top - 0.135f, at.Z));
        if (_platformShape.Disabled) _platformShape.Disabled = false;
    }
    private void PlatformOff() { if (_platformShape != null && !_platformShape.Disabled) _platformShape.Disabled = true; }
    private Vector3 JointWorld(string joint)
    {
        if (_skel != null) { int b = _skel.FindBone(joint); if (b >= 0) return (_skel.GlobalTransform * _skel.GetBoneGlobalPose(b)).Origin; }
        return _model != null ? _model.GlobalPosition : GlobalPosition;
    }

    /// <summary>calcRootMatrix: as a platform the model sits at the surface + unk1E0 (the body point is 80 u down); else TSpineEnemy's.</summary>
    protected override void ApplyLook()
    {
        base.ApplyLook();
        if (_model != null && IsNerve(NerveObject) && IsInsideTree() && _holder == null)
            _model.GlobalPosition = new Vector3(GlobalPosition.X, (_objBaseY + _objHosei) / U, GlobalPosition.Z);
    }

    // ------------------------------------------------------------------------------------------------------------------ the hop
    /// <summary>SMSCalcJumpVelocityY(goal, mPosition, speed, gravity, -40): vy = the speed; the XZ speed = the offset over the ticks the parabola takes to come
    /// back down to the goal's height (the terminal -40).</summary>
    private Vector3 CalcVelocityToJumpToY(Vector3 goalU, float speedY, float gravity)
    {
        Vector3 pos = Pos();
        float currY = pos.Y, vy = speedY;
        int ticks = 1;
        for (int guard = 0; guard < 100000; guard++)
        {
            currY += vy;
            if (vy < 0f && currY <= goalU.Y) break;
            vy -= gravity;
            if (vy < -40f) vy = -40f;
            ticks++;
        }
        return new Vector3((goalU.X - pos.X) / ticks, speedY, (goalU.Z - pos.Z) / ticks);
    }
    /// <summary>goToShortestNextGraphNode: the next hop's goal -- a random other node of the Graph, or a random point within WanderRadiusUnits (the base's rule).</summary>
    private void GoToNextHopGoal() => GoToRandomNextGraphNode();
    /// <summary>generateEffectColumWater: the column at the body (the scaled burst).</summary>
    private void ColumnWater()
    {
        Vector3 at = new Vector3(GlobalPosition.X, GroundHeightU() / U, GlobalPosition.Z);
        if (SpawnBurst(WaterColumnFxScene, new Transform3D(Basis.FromScale(Vector3.One * _scale), at)) != null) ColumnBursts++;
    }

    // ------------------------------------------------------------------------------------------------------------------ the game's virtuals
    protected override void SetWaitAnm() => SetBck(_waterUnder ? BckWait4 : BckWait1);
    protected override void SetFreezeAnm() { _stunned = true; SetBck(BckWait2); }
    protected override void SetDeadAnm() { _stunned = true; }                       // the running clip plays out
    protected override void SetDeadEffect() { if (_waterUnder && !_airborne) ColumnWater(); }
    /// <summary>TMameGesso::attackToMario: nothing while stunned or a platform; else the ATTACK (no PostAttack hop).</summary>
    protected override void AttackToMario() { if (_stunned || IsNerve(NerveObject)) return; SendAttackMsgToMario(); }
    /// <summary>isCollidMove: not while wandering on the ground.</summary>
    protected override bool IsCollidMove(Node3D other) => !(IsNerve(NerveGraphJumpWander) && !_airborne);
    /// <summary>getGravityY: JumpWanderGravityY as a platform, ThrownGravityY thrown, else mGravity.</summary>
    protected override float GravityY() => IsNerve(NerveObject) ? Prm.JumpWanderGravityY : IsNerve(NerveGThrown) ? Prm.ThrownGravityY : base.GravityY();
    /// <summary>behaveToWater: wandering or waiting -> Damage.</summary>
    protected override void BehaveToWater()
    {
        if (!IsNerve(NerveGraphJumpWander) && !IsNerve(NerveGWait)) return;
        PushNerve(NerveGDamage);
    }
    /// <summary>isHitValid: a HIP_DROP while wandering -> the Die nerve pushed, false (no bounce, no kill()); else true.</summary>
    protected override bool IsHitValid(HitMessage msg)
    {
        _notHipDrop = msg != HitMessage.HipDrop;
        if (IsNerve(NerveGraphJumpWander) && msg == HitMessage.HipDrop)
        {
            _hipDropped = true;
            PushNerve(NerveDie);
            if (DebugPrint) GD.Print($"[SMS gesso] {Name}: HIP_DROP while wandering -> Die");
            return false;
        }
        return true;
    }
    /// <summary>TMameGesso::kill: no death -- wandering or waiting: Damage then Trample pushed (Trample runs first); flailing: Trample alone.</summary>
    public override void Kill()
    {
        if (_dead) return;
        if (_attackerKilled && IsNerve(NerveGraphJumpWander)) { PushNerve(NerveDie); return; }
        if (IsNerve(NerveGraphJumpWander) || IsNerve(NerveGWait)) { PushNerve(NerveGDamage); PushNerve(NerveGTrample); HitSound?.Play(); }
        else if (IsNerve(NerveJitabata)) PushNerve(NerveGTrample);
    }
    /// <summary>doKeepDistance: a platform at rest (wait3).</summary>
    private bool DoKeepDistance() => IsBck(BckWait3);
    /// <summary>MarioCheckCol.cpp 366: doKeepDistance -> keepDistance (a swimming Mario pushed off the platform's axis), else hitPickUpEnemy (the base's
    /// hit check; his side takes a stunned one).</summary>
    protected override void HitCheckMario()
    {
        base.HitCheckMario();
        if (_mario == null || !DoKeepDistance() || !GetCollisionLayerValue(1)) return;
        Vector3 g = Pos(), m = _mario.SmsFeetWorld() * U;
        if (CheckDistance(m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight, g, Prm.DamageRadius * _bodyScale, Prm.DamageHeight * _bodyScale))
            _mario.SmsKeepDistance(g, Prm.DamageRadius * _bodyScale);
    }
    /// <summary>genRandomItem at the ground height -- the surface for a floating one.</summary>
    protected override void GenRandomItem() { _groundY = GroundHeightU(); base.GenRandomItem(); }

    // ------------------------------------------------------------------------------------------------------------------ taken (TMario::hitPickUpEnemy with unk164)
    /// <summary>His touch takes a stunned one (not a platform, not dead, not held).</summary>
    public override bool SmsCanBeTaken() => _stunned && !_dead && _holder == null && !IsNerve(NerveObject) && GetCollisionLayerValue(1);
    /// <summary>behaveToTaken: PickUp pushed unless already in it.</summary>
    protected override void BehaveToTaken(Mario holder) { if (!IsNerve(NerveGPickUp)) PushNerve(NerveGPickUp); }
    /// <summary>behaveToRelease: only while stunned and not already thrown -- Thrown pushed with his yaw and throw power read now.</summary>
    protected override void BehaveToRelease(Mario holder, bool put)
    {
        if (!_stunned || IsNerve(NerveGThrown)) return;
        Vector3 f = holder != null && IsInstanceValid(holder) ? holder.SmsFacingDir() : PolarXZ(_yaw, 1f);
        f.Y = 0f;
        _throwYaw = f.LengthSquared() > 1e-6f ? Mathf.RadToDeg(Mathf.Atan2(f.X, f.Z)) : _yaw;
        _throwPower = SmsParams.Take.ThrowPower;
        PushNerve(NerveGThrown);
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveGraphJumpWander:                           // TNerveMameGessoGraphJumpWander
                if (_time == 0) SetBck(_waterUnder ? BckRunStart2 : BckRunStart1);
                if (IsBck(BckRunStart1) || IsBck(BckRunStart2))
                {
                    if (BckEnded()) SetBck(BckRunLoop);
                    if (!_airborne && BckFrame() > 26f)
                    {
                        if (_waterUnder) { ColumnWater(); WaterHopSound?.Play(); }
                        if (_onGraph)
                        {
                            GoToNextHopGoal();
                            Vector3 v = CalcVelocityToJumpToY(Goal(), Prm.JumpWanderSpeed, GravityY());
                            GlobalPosition += Vector3.Up * (10f / U);
                            _vel = v; _airborne = true; _onWater = false;
                        }
                        else
                        {
                            Vector3 d = new Vector3(Goal().X - Pos().X, 0f, Goal().Z - Pos().Z);
                            if (d == Vector3.Zero) d.X += 1f;
                            d = d.Normalized();
                            Vector3 target = new Vector3(d.X * Prm.ReturnJumpDist + Pos().X, Pos().Y, d.Z * Prm.ReturnJumpDist + Pos().Z);
                            Vector3 v = CalcVelocityToJumpToY(target, Prm.ReturnJumpSp, GravityY());
                            GlobalPosition += Vector3.Up * (2f / U);
                            _vel = v; _airborne = true; _onWater = false;
                        }
                        if (DebugPrint) GD.Print($"[SMS gesso] {Name}: the hop ({(_onGraph ? "to the goal" : "the return")}) v {_vel} from {(_waterUnder ? "the water" : "the ground")}");
                    }
                }
                if (_onGraph)
                {
                    if (!IsReachedToGoal() || _airborne) WalkBehavior(_airborne ? 3 : 2, 1f);
                }
                else
                {
                    if (IsReachedToGoal()) _onGraph = true;
                    WalkBehavior(3, 1f);
                }
                if (!_airborne)
                {
                    if (IsBck(BckRunLoop))
                    {
                        if (_waterUnder) { SetBck(BckRunEnd2); ColumnWater(); WaterLandSound?.Play(); }
                        else SetBck(BckRunEnd1);
                    }
                    else if (BckEnded()) { PushAfterCurrent(NerveGWait); return true; }
                }
                return false;

            case NerveGDamage:                                   // TNerveMameGessoDamage
                if (_time == 0) { _vel.X = 0f; _vel.Z = 0f; SetBck(BckTurn); }
                if (!_airborne && _waterUnder) { PushAfterCurrent(NerveObject); return true; }
                if (BckEnded())
                {
                    if (IsBck(BckTurn) && _airborne) SetBck(BckFall);
                    if (!_airborne) { PushAfterCurrent(_waterUnder ? NerveObject : NerveJitabata); return true; }
                }
                return false;

            case NerveJitabata:                                  // TNerveMameGessoJitabata
                if (_time == 0) SetFreezeAnm();
                else if (BckEnded())
                {
                    if (IsBck(BckWait2)) { if (_time > Prm.FreezeWait) { SetBck(BckReturn); RecoverSound?.Play(); } }
                    else if (IsBck(BckReturn))
                    {
                        if ((Goal() - Pos()).Length() > 300f) _onGraph = false;
                        PushAfterCurrent(NerveGWait);
                        _stunned = false;
                        return true;
                    }
                }
                return false;

            case NerveGTrample:                                  // TNerveMameGessoTrample
                if (_time < 1)
                {
                    if (_notHipDrop) SetBck(IsBck(BckWait2) ? BckPressDown : BckTurn);
                    else SetBck(IsBck(BckWait2) ? BckPressDown : BckPressUp);
                }
                else if (BckEnded()) return true;
                return false;

            case NerveGPickUp:                                   // TNerveMameGessoPickUp
                if (_time == 0 && !_stunned) return true;
                if (BckEnded() && _time > Prm.PickUpTime)
                {
                    _stunned = false;
                    AttackToMario();                             // it comes to in his hands and bites
                    return true;
                }
                return false;

            case NerveGThrown:                                   // TNerveMameGessoThrown
                if (_time == 0)
                {
                    Vector3 f = PolarXZ(_throwYaw, 1f);
                    _vel = new Vector3(Prm.ThrownRateXZ * _throwPower * f.X, Prm.ThrownVY, Prm.ThrownRateXZ * _throwPower * f.Z);
                    GlobalPosition += Vector3.Up * (2f / U);
                    _airborne = true; _onWater = false; _onWall = false;
                    if (DebugPrint) GD.Print($"[SMS gesso] {Name}: thrown at {_vel} toward {_throwYaw:F0} deg");
                }
                if (!_airborne && _time > 0)
                {
                    if (_waterUnder) { PushAfterCurrent(NerveObject); _stunned = false; return true; }
                    if (!IsBck(BckLand1)) SetBck(BckLand1);
                    if (BckEnded() && IsBck(BckLand1)) { _stunned = false; return true; }
                }
                return false;

            case NerveObject:                                    // TNerveMameGessoObject
                {
                    bool swimming = _mario != null && _mario.stateOfMario == Mario.MarioState.swimming;
                    if (swimming) { SetCollisionLayerValue(1, true); _bodyScale = _scale * 2.5f; }       // offHitFilter(NO_COLLISION); unk190 = unk154 x 2.5; expandCollision
                    else { SetCollisionLayerValue(1, false); _bodyScale = _scale; }
                    if (_time == 0)
                    {
                        SetBck(BckLand2);
                        SetCollisionLayerValue(1, false);
                        _objHosei = 0f;
                        _objBaseY = GroundHeightU();
                        _objSink = MathF.Min(ObjectSinkUnits, MathF.Max(0f, _objBaseY - _groundY - 5f));   // (clamped over the real floor: a shallow pool)
                        UpdatePlatform();
                        ColumnWater(); WaterLandSound?.Play();
                        if (DebugPrint) GD.Print($"[SMS gesso] {Name}: a PLATFORM on the water at y {_objBaseY / U:F2} m for {ObjectRecoverTicks} ticks");
                    }
                    if (BckEnded() && IsBck(BckLand2)) SetBck(BckWait3);
                    if (_time > ObjectRecoverTicks)
                    {
                        if (BckEnded())
                        {
                            if (IsBck(BckWait3)) { SetBck(BckWaterReturn); RecoverSound?.Play(); }
                            else if (IsBck(BckWaterReturn))
                            {
                                PlatformOff();
                                SetCollisionLayerValue(1, true); _bodyScale = _scale;
                                GlobalPosition = new Vector3(GlobalPosition.X, _objBaseY / U, GlobalPosition.Z);
                                _objSink = 0f;
                                PushAfterCurrent(NerveGWait);
                                return true;
                            }
                        }
                        if (_objHosei < 0f) _objHosei += 1f;
                    }
                    else if (_objHosei > BaseHoseiTransY) _objHosei -= 1f;
                    return false;
                }

            case NerveGWait:                                     // TNerveMameGessoWait
                if (_time == 0) SetWaitAnm();
                if (BckEnded())
                {
                    int wait = _waterUnder ? Prm.WaitTimeInWater : Prm.WaitTimeOnGround;
                    if (_time > wait + _instanceIdx * 10) { PushAfterCurrent(NerveGraphJumpWander); return true; }
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }

    protected override void Dead()
    {
        PlatformOff();
        base.Dead();
    }
}
