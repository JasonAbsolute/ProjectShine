using Godot;
using System;
using System.Collections.Generic;

/// <summary>TBaseNPC (NPC/NpcBase.cpp + NpcNerve / NpcChange / NpcWalkTurn / NpcAnm / NpcCallback), the one class behind every
/// Pianta, Noki and Toad: the island natives. One tick = one 120 Hz physics step (a TLiveActor adds its full velocity every
/// CUE_MOVE; its params are per tick). What is here, each against the source:
///  * the nerve stack (NpcNerve.cpp): GraphWander -> GraphWait on a random timer (300..3000 ticks of walking, 100..1200 of
///    standing) between the points of a Graph (any Node3D's children) or random points in WanderRadius -- or, with Wander off,
///    WaitMarioApproach standing where placed; TurnToMario when Mario is inside BodyTurnSearchDist / Height / Degree (the Toads:
///    isTurnToMarioWhenApproach is true only for 0x4000016..18; a Pianta turns back to its placed facing at FirstStateTurnSpeed);
///  * the talk (TMarDirector::movement_game + changeNerveProc_ + readBillboard): Mario standing (MARIO_STATUS_WAIT), not holding,
///    not in the L camera, within TalkAcceptDist / Height, inside the NPC's TalkAcceptDegree cone and facing it within
///    MarioTalkAcceptDegree -> the cursor over its head -> B: the NPC turns to him (isTurnToMarioWhenTalk, the talk clip), Mario
///    is set 100 units off and turns at ReadRotSp; when he faces it the window opens on Lines; A pages; closing = npcTalkOut:
///    talk forbidden 60 ticks, walking 120;
///  * the neck (NPCNeckCallBack): the head follows Mario within NeckTurnSearchDist -- SmsNpcNeck on the skeleton;
///  * the water (behaveToHitObject_): a spray -> TNerveNPCWet: the wet clip (50 % the alternate) to its end, then a Pianta in
///    MadSearchRange goes Mad (step to face him at MadTurnSpeed, the mad clip), else back to what it was doing;
///  * the trample (behaveToBeTrampled_ + TNpcTrample): Mario landing on it -> StompBounce for him, a 40-tick squash wobble on it
///    (scale.y x (1 - amp sin), amp 0.75 -> 0), then after 60 ticks the Pianta goes Mad if Mario is in range;
///  * the look (NpcInitData.cpp colour tables + the bmd TEV, NpcSkin / NpcEye.gdshader): ColorIndex picks the register colour
///    (-1 = random), the model's materials are replaced per instance, the scene's DirectionalLight3D is the sun.
/// NOT ported (logged): being carried / thrown by Mario (LIVE_FLAG_UNK100000 Piantas: the take system is RigidBody-only), the
/// pollution (dirty / sink / wash), the Pianta that throws Mario (TNpcThrow), the balloon messages, sounds (slots below), the
/// talk camera, the accessory parts (hats / glasses / moustaches: TNpcParts) and their colour tables.</summary>
public partial class SmsNpc : CharacterBody3D, ISmsSprayTarget
{
    private const float U = SmsUnits.UnitsPerMetre;

    public enum IdleStyle { Wait, WaitA, WaitB, Talking, Angry, Dance, Sit, SitTalk }   // the placement's NPC_ACTION flags -> getNpcWaitAnmBase_ / UNK1 sit
    public enum Nerve { GraphWander, UTurn, GraphWait, WaitMarioApproach, TurnToMario, Talk, Wet, Mad, Taken }

    [ExportGroup("Who")]
    [Export] public SmsNpcProfile Profile;
    /// <summary>Which entry of the profile's colour table; -1 = a random one per instance (the game numbers them from the stage data).</summary>
    [Export] public int ColorIndex = -1;
    [Export] public IdleStyle Idle = IdleStyle.Wait;
    [ExportGroup("Talk")]
    /// <summary>The pages the window shows, one string each (\n breaks a line). Empty = this NPC cannot be talked to (no cursor).</summary>
    [Export(PropertyHint.MultilineText)] public string[] Lines = { "Welcome to Isle Delfino!", "Enjoy your stay." };
    [Export] public bool CanTalk = true;
    [ExportGroup("Take")]
    /// <summary>LIVE_FLAG_UNK100000 -- in the game the stage script sets it per NPC (evSetFlagNPCCanTaken): Mario touching this NPC with B
    /// lifts it (TMario::hitNpc -> canTake, the same isTakeSituation geometry as a fruit), carries it on his hands (its feet at his taking
    /// matrix, its facing offset to his kept), and a throw or a put-down both end as the NPC's own releaseTaken_: ThrowSpeedXZ 3 / Y 2.5
    /// units a tick along his facing, 15 frames of no collision, then it stands (WaitMarioApproach). findNearestTalkNPC skips a liftable
    /// NPC: B lifts, it never talks (no cursor). Off = the game's default.</summary>
    [Export] public bool CanBeTaken = false;
    [ExportGroup("Wander")]
    /// <summary>Walk between points (the game's graph "main"). Off = stands where placed (an NPC without a graph).</summary>
    [Export] public bool Wander = false;
    /// <summary>A Node3D whose children (Marker3Ds) are the route; empty with Wander on = random points within WanderRadiusUnits.</summary>
    [Export] public Node3D Graph;
    [Export] public float WanderRadiusUnits = 600f;
    [ExportGroup("Scene")]
    [Export] public NodePath ModelPath = "Model";
    [Export] public PackedScene TalkWindowScene;
    [Export] public PackedScene CursorScene;
    [Export] public Shader SkinShader;
    [Export] public Shader EyeShader;
    [Export] public Texture2D ToonTexture;
    /// <summary>Sound slots (the sound bank pass fills them): nothing assigned = silent.</summary>
    [Export] public AudioStreamPlayer3D TalkVoice, WetVoice, TrampleVoice, MadVoice;
    [Export] public bool DebugPrint = false;

    public const string SkinShaderPath = "res://assets/Sms/Npc/NpcSkin.gdshader";
    public const string EyeShaderPath = "res://assets/Sms/Npc/NpcEye.gdshader";
    public const string ToonPath = "res://assets/Sms/models/toon_2_4i.png";
    public const string TalkWindowPath = "res://assets/Sms/Npc/TalkWindow.tscn";
    public const string CursorPath = "res://assets/Sms/Npc/TalkCursor.tscn";

    // ---- state ----
    private static readonly List<SmsNpc> All = new();
    public static IReadOnlyList<SmsNpc> Everyone => All;     // Mario's take search (hitNpc) reads the stage's NPCs here
    private static SmsTalkWindow _window;                // one window for the stage (the director's)
    private Mario _mario;
    private Node3D _model; private AnimationPlayer _anim; private Skeleton3D _skel; private SmsNpcNeck _neck;
    private SmsTalkCursor _cursor;
    private readonly Stack<Nerve> _stack = new();
    private Nerve _cur = Nerve.WaitMarioApproach;
    private int _nerveTime;
    private float _marchSpeed, _turnSpeed;               // units/tick, deg/tick
    private float _firstYaw;                             // unk1A0.y: the placed facing (radians)
    private Vector3 _target; private bool _hasTarget;    // unkF4: the current path node
    private int _wanderTimer, _wanderGoal, _waitGoal;    // unk22C
    private int _talkForbid, _walkForbid;                // mTalkForbidCount / mWalkForbidCount
    private int _graphIndex = -1;
    private string _clip = ""; private bool _clipLoops;
    private float _trampleAmp; private int _trampleShake, _trampleToMad; private float _trampleDec; // TNpcTrample
    private bool _marioStompLatch;
    private int _colorIdx;
    private bool _talkable;                              // LIVE_FLAG_UNK20000
    private bool _talking, _windowOpen;
    private Mario _holder; private SmsNpcHoldProxy _holdProxy;      // mHolder / unk158 (TTakeActor)
    private float _angleDiffWhenTaken;                              // mAngleYDiffWhenTaken (radians): his yaw - ours at the take
    private int _posBlendTicks; private Vector3 _posBlendFrom;      // TNpcInbetween::startPosBlend: PosInbetweenFrame frames (x4 ticks) into his hands
    private int _noCollisionTicks;                                  // unk1DC: HIT_FILTER_NO_COLLISION for 15 frames after the release
    private bool _collisionOff; private uint _layerSaved;           // the hit filter: the capsule's layer off (he is not pushed), his body excepted
    private bool _thrown; private Vector3 _thrownVel; private int _thrownMoves;   // LIVE_FLAG_UNK10000000 + mVelocity from releaseTaken_, until the landing zeroes it
    private Vector3 _modelBaseScale = Vector3.One;
    private int _needTurnToFirst;
    private readonly Random _rng = new();

    public Nerve CurrentNerve => _cur;
    public bool Talkable => _talkable;
    public bool IsTalking => _talking;
    public bool IsHeld => _holder != null;
    public bool IsThrown => _thrown;
    /// <summary>The receive cylinder Mario's take test reads (isTakeSituation: his DamageRadius against the object's).</summary>
    public float HitRadiusUnits => Profile != null ? Profile.DamageRadius : 100f;
    public float HitHeightUnits => Profile != null ? Profile.DamageHeight : 200f;
    /// <summary>isNowCanTaken: the flag, no holder, collision on (not the 15 frames after a release), not talking, a nerve the take may
    /// interrupt (isNerveCanGoToTaken: the wander / wait / approach / turn / wet family -- not Talk, not Mad).</summary>
    public bool IsNowCanTaken() => CanBeTaken && Profile != null && _holder == null && !_collisionOff && !_talking && IsInsideTree()
        && (_cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario || _cur == Nerve.Wet);
    public string CurrentClip => _clip;
    public float MarchSpeed => _marchSpeed;
    public int ColorIdx => _colorIdx;
    public SmsNpcNeck Neck => _neck;
    public bool NeckAllowed => true;   // isNeedNeckStraight: only the throw / sunflower / board NPCs hold the head straight

    public Vector3 PosU() => GlobalPosition * U;
    public Vector3 MarioPositionMetres() => _mario != null ? _mario.GlobalPosition : Vector3.Inf;
    private Vector3 MarioU() => _mario != null ? _mario.GlobalPosition * U : PosU();
    private float YawRad => Rotation.Y;
    private static float YawOf(Vector3 d) => Mathf.Atan2(-d.X, -d.Z);              // DirFromYaw(y) = (-sin y, 0, -cos y): forward = -Z
    private static Vector3 DirOf(float yaw) => new(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));

    public override void _Ready()
    {
        All.Add(this);
        _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        Profile ??= new SmsNpcProfile();
        _firstYaw = Rotation.Y;
        _model = GetNodeOrNull<Node3D>(ModelPath);
        if (_model != null)
        {
            _modelBaseScale = _model.Scale;
            _anim = FindChild<AnimationPlayer>(_model);
            _skel = FindChild<Skeleton3D>(_model);
        }
        _colorIdx = ColorIndex >= 0 ? ColorIndex % Profile.ColorCount : _rng.Next(Profile.ColorCount);
        SetupLook();
        SetupNeck();
        SetupCursor();
        MarkLoops();
        _stack.Clear();
        if (Wander) { ChooseNextTarget(); Enter(Nerve.GraphWander); }
        else Enter(Nerve.WaitMarioApproach);
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: {Profile.Kind} colour {_colorIdx}, clips {(_anim != null ? _anim.GetAnimationList().Length : 0)}, neck {(_neck != null ? "on" : "off")}");
    }

    public override void _ExitTree() { All.Remove(this); }

    private static T FindChild<T>(Node n) where T : Node
    {
        if (n is T t) return t;
        foreach (var c in n.GetChildren()) { var r = FindChild<T>(c); if (r != null) return r; }
        return null;
    }

    // ------------------------------------------------------------------ look
    /// <summary>SMS_InitChangeNpcColor (NpcColor.cpp) on the materials the init data names, with the bmd's own TEV as the shaders:
    /// every MeshInstance3D surface named like a profile material gets a per-instance ShaderMaterial; the eyes their decal
    /// shader; the leaves keep the import's material.</summary>
    private void SetupLook()
    {
        if (_model == null) return;
        var skin = SkinShader ?? GD.Load<Shader>(SkinShaderPath);
        var eye = EyeShader ?? GD.Load<Shader>(EyeShaderPath);
        var toon = ToonTexture ?? GD.Load<Texture2D>(ToonPath);
        var sun = FindSun(GetTree().CurrentScene);
        bool toad = Profile.Kind == SmsNpcProfile.NpcKind.Toad;
        foreach (var mi in AllMeshes(_model))
        {
            int n = mi.GetSurfaceOverrideMaterialCount();
            for (int s = 0; s < n; s++)
            {
                var src = mi.Mesh?.SurfaceGetMaterial(s);
                string name = src?.ResourceName ?? "";
                Texture2D tex = (src as BaseMaterial3D)?.AlbedoTexture;
                ShaderMaterial m = null;
                if (name == Profile.BodyMaterial)
                {
                    m = new ShaderMaterial { Shader = skin };
                    m.SetShaderParameter("mode", toad ? 2 : 0);
                    m.SetShaderParameter("color_a", Vec(Profile.BodyColors, _colorIdx, new Vector3(255, 255, 255)));
                    m.SetShaderParameter("color_b", Vec(Profile.BodyColors2, _colorIdx, new Vector3(230, 230, 230)));
                    m.SetShaderParameter("spec_gain", 1.0f);
                    m.SetShaderParameter("use_vertex_color", !toad);
                    if (tex != null) m.SetShaderParameter("base_tex", tex);
                }
                else if (Profile.ClothMaterial != "" && name == Profile.ClothMaterial)
                {
                    m = new ShaderMaterial { Shader = skin };
                    m.SetShaderParameter("mode", 2);
                    m.SetShaderParameter("color_a", Vec(Profile.ClothColors, _colorIdx, new Vector3(250, 150, 0)));
                    m.SetShaderParameter("color_b", Vec(Profile.ClothColors2, _colorIdx, new Vector3(255, 255, 255)));
                    m.SetShaderParameter("spec_gain", 0.5f);
                    m.SetShaderParameter("use_vertex_color", false);
                    if (tex != null) m.SetShaderParameter("base_tex", tex);
                }
                else if (toad && (name == "_mat_body" || name == "_mat_mouth"))
                {
                    m = new ShaderMaterial { Shader = skin };
                    m.SetShaderParameter("mode", 1);
                    m.SetShaderParameter("use_vertex_color", false);
                    if (tex != null) m.SetShaderParameter("base_tex", tex);
                }
                else if (name == "_eye_mat")
                {
                    m = new ShaderMaterial { Shader = eye, RenderPriority = 1 };
                    m.SetShaderParameter("eye_base", 0.5f);
                    m.SetShaderParameter("lit_mix", toad ? 0.625f : 0.5f);
                    m.SetShaderParameter("toon_mix", toad ? 0.375f : 0f);
                    if (tex != null) m.SetShaderParameter("eye_tex", tex);
                }
                if (m == null) continue;
                m.SetShaderParameter("toon_tex", toon);
                SetSun(m, sun);
                mi.SetSurfaceOverrideMaterial(s, m);
            }
        }
    }

    private static Vector3 Vec(Vector3[] table, int i, Vector3 fallback)
    {
        if (table == null || table.Length == 0) return fallback / 255f;
        return table[i % table.Length] / 255f;
    }

    private static IEnumerable<MeshInstance3D> AllMeshes(Node n)
    {
        if (n is MeshInstance3D mi) yield return mi;
        foreach (var c in n.GetChildren()) foreach (var r in AllMeshes(c)) yield return r;
    }

    private static DirectionalLight3D FindSun(Node n)
    {
        if (n == null) return null;
        if (n is DirectionalLight3D d) return d;
        foreach (var c in n.GetChildren()) { var r = FindSun(c); if (r != null) return r; }
        return null;
    }

    /// <summary>The scene's DirectionalLight3D stands in for the stage's object sun (its default is scene.bin's).</summary>
    private static void SetSun(ShaderMaterial m, DirectionalLight3D sun)
    {
        if (sun == null) return;
        m.SetShaderParameter("sun_dir", -sun.GlobalTransform.Basis.Z.Normalized());
        m.SetShaderParameter("sun_color", new Vector3(sun.LightColor.R, sun.LightColor.G, sun.LightColor.B) * sun.LightEnergy);
    }

    private void SetupNeck()
    {
        if (_skel == null || string.IsNullOrEmpty(Profile.NeckBone) || _skel.FindBone(Profile.NeckBone) < 0) return;
        _neck = new SmsNpcNeck { Name = "SmsNpcNeck", Owner = this, BoneName = Profile.NeckBone };
        _skel.AddChild(_neck);
    }

    private void SetupCursor()
    {
        var scene = CursorScene ?? GD.Load<PackedScene>(CursorPath);
        if (scene == null) return;
        _cursor = scene.Instantiate<SmsTalkCursor>();
        AddChild(_cursor);
        _cursor.SetRest(Vector3.Up * ((Profile.BodyHeight + Profile.CursorHeight) / U));
        _cursor.Visible = false;
    }

    /// <summary>The clips the game loops (every "wait" family, the walk / run, the talk, the mad loop, the dance) vs the one-shots.</summary>
    private void MarkLoops()
    {
        if (_anim == null) return;
        var p = Profile;
        string[] loops = { p.ClipWalk, p.ClipRun, p.ClipWait, p.ClipWaitA, p.ClipWaitB, p.ClipTalk, p.ClipSit, p.ClipSitTalk, p.ClipMadLoop, p.ClipDance, p.ClipDirty, p.ClipWaitFall, p.ClipTaken };
        foreach (var name in _anim.GetAnimationList())
        {
            var a = _anim.GetAnimation(name);
            if (a == null) continue;
            a.LoopMode = Array.IndexOf(loops, (string)name) >= 0 ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        }
    }

    // ------------------------------------------------------------------ animation
    /// <summary>requestNpcAnm_ with NPC_STOP_MOTION_BLEND_ON = a MotionBlendFrame (20 frame = 0.33 s) blend; OFF = a cut.</summary>
    private void Play(string clip, bool blend = true, float rate = 1f)
    {
        if (_anim == null || string.IsNullOrEmpty(clip) || !_anim.HasAnimation(clip)) { _clip = clip ?? ""; return; }
        if (_clip == clip && _anim.IsPlaying()) { _anim.SpeedScale = rate; return; }
        _clip = clip;
        _anim.Play(clip, blend ? SmsParams.Npc.MotionBlendFrame / 60f : 0f, rate);
        _clipLoops = _anim.GetAnimation(clip).LoopMode != Animation.LoopModeEnum.None;
    }

    private bool ClipEnded() => _anim == null || string.IsNullOrEmpty(_clip) || !_anim.HasAnimation(_clip) || (!_clipLoops && !_anim.IsPlaying());

    private string WaitClip()
    {
        var p = Profile;
        switch (Idle)
        {
            case IdleStyle.WaitA: return p.ClipWaitA;
            case IdleStyle.WaitB: return p.ClipWaitB;
            case IdleStyle.Talking: return p.ClipTalk;
            case IdleStyle.Angry: return p.ClipMadLoop;
            case IdleStyle.Dance: return p.ClipDance;
            case IdleStyle.Sit: return p.ClipSit;
            case IdleStyle.SitTalk: return p.ClipSitTalk;
            default: return p.ClipWait;
        }
    }
    private bool Sitting => Idle == IdleStyle.Sit || Idle == IdleStyle.SitTalk;

    private void WaitIn() { Play(WaitClip()); ResetToWait(); }           // npcWaitIn
    private void StepIn() { Play(Sitting ? WaitClip() : Profile.ClipStep); ResetToWait(); }  // npcStepIn (a sitting NPC does not step)
    private void ResetToWait() { _marchSpeed = 0f; _turnSpeed = Profile.WaitTurnSpeed; }

    // ------------------------------------------------------------------ nerves
    private void Enter(Nerve n)
    {
        _cur = n; _nerveTime = 0;
        switch (n)
        {
            case Nerve.GraphWander: _wanderTimer = 0; _wanderGoal = _rng.Next(SmsParams.Npc.GraphWanderMinFrame, SmsParams.Npc.GraphWanderMaxFrame + 1); break;
            case Nerve.GraphWait: _wanderTimer = 0; _waitGoal = _rng.Next(SmsParams.Npc.GraphWaitMinFrame, SmsParams.Npc.GraphWaitMaxFrame + 1); break;
            case Nerve.WaitMarioApproach: break;
            case Nerve.TurnToMario: break;
            case Nerve.Talk: TalkIn(); break;
            case Nerve.Wet: WetIn(); break;
            case Nerve.Mad: MadIn(); break;
            case Nerve.Taken: TakenIn(); break;      // TNerveNPCWaitContinue at time 0 with a holder: npcTakenIn
        }
    }

    /// <summary>pushNerve: the interrupting nerve runs, then the one it interrupted resumes.</summary>
    private void Push(Nerve n) { _stack.Push(_cur); Enter(n); }
    private void Pop()
    {
        Nerve back = _stack.Count > 0 ? _stack.Pop() : (Wander ? Nerve.GraphWander : Nerve.WaitMarioApproach);
        if (back == Nerve.GraphWander && !_hasTarget) ChooseNextTarget();
        Enter(back);
        if (back == Nerve.WaitMarioApproach || back == Nerve.GraphWait || back == Nerve.TurnToMario) WaitIn();
    }

    private float _tickAccum;                                       // SmsUnits.TicksFor: two 120 Hz ticks a 60 Hz physics frame

    public override void _PhysicsProcess(double delta)
    {
        if (Profile == null) return;
        if (_mario == null) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        int n = SmsUnits.TicksFor(delta, ref _tickAccum);              // TLiveActor: every 120 Hz tick of the frame (pass 83; 82 ran one a frame)
        if (_holder != null) { for (int i = 0; i < n; i++) TickHeldClocks(); TickHeld(); return; }   // perform with mHolder: no bind
        for (int i = 0; i < n; i++)
        {
            Tick();
            if (_holder != null) { TickHeld(); return; }               // taken inside the frame: the held path owns the rest of it
        }
        Move();
        if (_cursor != null) _cursor.Visible = _talkable && !_talking && NearestTalkable() == this;
    }

    /// <summary>One 120 Hz tick: moveObject (updateForbidCount_, the trample, the hit checks, changeNerveProc_'s talk accept) and the spine.</summary>
    private void Tick()
    {
        if (_talkForbid > 0) _talkForbid--;
        if (_walkForbid > 0) _walkForbid--;
        if (_noCollisionTicks > 0 && --_noCollisionTicks == 0) SetCollisionOff(false);   // unk1DC ran out: the hit filter back on
        UpdateTrample();
        if (!_collisionOff) HitCheckMario();                           // HIT_FILTER_NO_COLLISION: his hit checks skip us
        TalkCheck();
        _nerveTime++;
        switch (_cur)
        {
            case Nerve.GraphWander: TickGraphWander(); break;
            case Nerve.UTurn: TickUTurn(); break;
            case Nerve.GraphWait: TickGraphWait(); break;
            case Nerve.WaitMarioApproach: TickWaitMarioApproach(); break;
            case Nerve.TurnToMario: TickTurnToMario(); break;
            case Nerve.Talk: TickTalk(); break;
            case Nerve.Wet: TickWet(); break;
            case Nerve.Mad: TickMad(); break;
            case Nerve.Taken: break;
        }
    }

    /// <summary>TLiveActor::bind for an NPC: the march along the facing (units/tick -> m/s), gravity when airborne, the floor otherwise.</summary>
    private void Move()
    {
        Vector3 v = Velocity;
        if (_thrown && _thrownMoves > 0 && IsOnFloor()) { _thrown = false; v = Vector3.Zero; _thrownVel = Vector3.Zero; }   // bind: on the ground mVelocity.set(0,0,0)
        if (_thrown) _thrownMoves++;
        Vector3 f = _thrown ? _thrownVel : DirOf(YawRad) * SmsUnits.TickSpeedToMps(_marchSpeed);
        v.X = f.X; v.Z = f.Z;
        if (!IsOnFloor()) v.Y -= SmsUnits.TickAccelToMps2(SmsParams.Npc.GravityY) * (float)GetPhysicsProcessDeltaTime();
        else v.Y = Mathf.Max(v.Y, -0.01f);
        Velocity = v;
        MoveAndSlide();
    }

    // ---- GraphWander / GraphWait / UTurn (TNerveNPCGraphWander, execWalk) ----
    private void ChooseNextTarget()
    {
        Vector3 p = GlobalPosition;
        if (Graph != null && Graph.GetChildCount() > 0)
        {
            int n = Graph.GetChildCount();
            int next = _rng.Next(n);
            if (n > 1 && next == _graphIndex) next = (next + 1) % n;
            _graphIndex = next;
            var c = Graph.GetChild(next) as Node3D;
            _target = c != null ? c.GlobalPosition : p; _hasTarget = c != null;
        }
        else
        {
            float r = WanderRadiusUnits / U;
            float a = (float)(_rng.NextDouble() * Mathf.Tau), d = (float)(_rng.NextDouble()) * r;
            Vector3 home = _homeSet ? _home : (_home = p);
            _homeSet = true;
            _target = home + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d); _hasTarget = true;
        }
    }
    private Vector3 _home; private bool _homeSet;

    /// <summary>execWalk(true): the march speed chases MaxMarchSpeed at MarchAccel a tick (CLBChaseGeneralConstantSpecifySpeed),
    /// the body turns toward the node at WalkTurnSpeed (WaitTurnSpeed while stopped) and walks along its facing.</summary>
    private void ExecWalk(bool go)
    {
        if (_walkForbid > 0 || _talking) { _marchSpeed = 0f; _turnSpeed = 0f; return; }
        if (go)
        {
            bool run = _clip == Profile.ClipRun;
            float target = run ? Profile.MaxRunSpeed : Profile.MaxMarchSpeed, accel = run ? Profile.RunAccel : Profile.MarchAccel;
            _marchSpeed = Chase(_marchSpeed, target, accel);
        }
        else _marchSpeed = Chase(_marchSpeed, 0f, Profile.MarchDecrease);
        _turnSpeed = _marchSpeed < 0.001f ? Profile.WaitTurnSpeed : Profile.WalkTurnSpeed;
        if (_hasTarget && (_target - GlobalPosition).LengthSquared() * U * U >= 2.5625f)
            TurnToward(_target, _turnSpeed);
        WalkAnmRate();
    }

    /// <summary>walkAnmRateChange_: the walk clip's rate rides the speed between MinWalkAnmRate and MaxWalkAnmRate.</summary>
    private void WalkAnmRate()
    {
        if (_anim == null || (_clip != Profile.ClipWalk && _clip != Profile.ClipRun)) return;
        bool run = _clip == Profile.ClipRun;
        float max = run ? Profile.MaxRunSpeed : Profile.MaxMarchSpeed;
        float t = Mathf.Clamp((_marchSpeed - Profile.MinMarchSpeed) / Mathf.Max(max - Profile.MinMarchSpeed, 1e-4f), 0f, 1f);
        _anim.SpeedScale = Mathf.Lerp(Profile.MinWalkAnmRate, run ? Profile.MaxRunAnmRate : Profile.MaxWalkAnmRate, t);
    }

    private void TickGraphWander()
    {
        if (!_hasTarget) ChooseNextTarget();
        if (_walkForbid > 0 || _talking) { ExecWalk(false); if (_marchSpeed < 0.001f && _clip != WaitClip()) WaitIn(); return; } // mWalkForbidCount: standing until it runs out
        if (_clip != Profile.ClipWalk) Play(Profile.ClipWalk);
        ExecWalk(true);
        _wanderTimer++;
        Vector3 d = _target - GlobalPosition; d.Y = 0f;
        float arrive = SmsParams.Npc.ArriveDist / U;
        if (d.LengthSquared() < arrive * arrive)
        {
            if (_wanderTimer >= _wanderGoal) { Enter(Nerve.GraphWait); return; }     // the leg's time is up: a wait
            ChooseNextTarget();                                                    // goToShortestNextGraphNode
        }
    }

    private void TickUTurn()
    {
        if (!_hasTarget) { Enter(Nerve.GraphWander); return; }
        if (TurnToward(_target, Profile.UTurnSpeed)) { _marchSpeed = 0f; Enter(Nerve.GraphWander); }
    }

    private void TickGraphWait()
    {
        if (_marchSpeed < 0.001f)
        {
            if (_clip == Profile.ClipWalk || _clip == Profile.ClipRun) WaitIn();
            _wanderTimer++;
            if (_wanderTimer >= _waitGoal) { ChooseNextTarget(); Enter(Nerve.GraphWander); }
        }
        else ExecWalk(false);
    }

    // ---- WaitMarioApproach / TurnToMario ----
    /// <summary>isInBodyTurnSearchRange: Mario inside BodyTurnSearchDist / Height and the BodyTurnSearchDegree cone around the facing.</summary>
    private bool InBodyTurnRange()
    {
        if (_mario == null) return false;
        return InSight(MarioU(), Profile.BodyTurnSearchDist, Profile.BodyTurnSearchHeight, Profile.BodyTurnSearchDegree);
    }

    private bool InMadRange() => _mario != null && InSight(MarioU(), Profile.MadSearchDist, Profile.MadSearchHeight, Profile.MadSearchDegree);

    /// <summary>isInSight(pos, dist, degree): within the distance, the height, and the half-cone around the facing.</summary>
    private bool InSight(Vector3 targetU, float dist, float height, float degree)
    {
        Vector3 p = PosU();
        float dx = targetU.X - p.X, dz = targetU.Z - p.Z, dy = targetU.Y - p.Y;
        if (Mathf.Abs(dy) >= height) return false;
        float d2 = dx * dx + dz * dz;
        if (d2 >= dist * dist) return false;
        if (degree >= 360f || d2 < 1e-6f) return true;
        Vector3 f = DirOf(YawRad);
        float cos = (f.X * dx + f.Z * dz) / Mathf.Sqrt(d2);
        return cos >= Mathf.Cos(Mathf.DegToRad(degree * 0.5f));
    }

    private void TickWaitMarioApproach()
    {
        if (InBodyTurnRange()) { Enter(Nerve.TurnToMario); return; }
        if (IsNeedTurnToFirstState())
        {
            if (TurnToYaw(_firstYaw, Profile.FirstStateTurnSpeed)) { if (_clip != WaitClip()) WaitIn(); }
            else if (_clip != Profile.ClipStep) StepIn();
        }
        else if (_nerveTime == 1) WaitIn();
    }

    /// <summary>TNerveNPCTurnToMario: a Toad turns to him at the wait turn speed, stepping until it faces him; a Pianta
    /// (isTurnToMarioWhenApproach false) turns back to its placed facing instead.</summary>
    private void TickTurnToMario()
    {
        if (!InBodyTurnRange()) { Enter(Nerve.WaitMarioApproach); return; }
        if (Profile.TurnsToMarioOnApproach && !Sitting)
        {
            bool faced = TurnToward(_mario.GlobalPosition, _turnSpeed > 0f ? _turnSpeed : Profile.WaitTurnSpeed);
            if (faced) { if (_clip != WaitClip()) WaitIn(); }
            else if (_clip != Profile.ClipStep) StepIn();
        }
        else if (IsNeedTurnToFirstState())
        {
            if (TurnToYaw(_firstYaw, Profile.FirstStateTurnSpeed)) { if (_clip != WaitClip()) WaitIn(); }
            else if (_clip != Profile.ClipStep) StepIn();
        }
        else if (_nerveTime == 1) WaitIn();
    }

    /// <summary>isNeedTurnToFirstState: in the approach nerves, not sitting (UNK1) and not a special idle, the NPC drifts back to its placed yaw.</summary>
    private bool IsNeedTurnToFirstState() => !Sitting && Idle != IdleStyle.Angry && (_cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario);

    // ---- turning ----
    private static float Chase(float cur, float target, float step)
    {
        float d = target - cur;
        if (Mathf.Abs(d) <= step) return target;
        return cur + Mathf.Sign(d) * step;
    }

    /// <summary>SMS_GoRotate: the yaw moves toward the point at degPerTick; true when it faces it.</summary>
    private bool TurnToward(Vector3 worldPoint, float degPerTick)
    {
        Vector3 d = worldPoint - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() < 1e-8f) return true;
        return TurnToYaw(YawOf(d.Normalized()), degPerTick);
    }

    private bool TurnToYaw(float targetYaw, float degPerTick)
    {
        float cur = Rotation.Y;
        float diff = Mathf.Wrap(targetYaw - cur, -Mathf.Pi, Mathf.Pi);
        float step = Mathf.DegToRad(degPerTick);
        if (Mathf.Abs(diff) <= step) { Rotation = Rotation with { Y = targetYaw }; return true; }
        Rotation = Rotation with { Y = cur + Mathf.Sign(diff) * step };
        return false;
    }

    // ------------------------------------------------------------------ talk
    /// <summary>changeNerveProc_'s accept test (+ findNearestTalkNPC's Mario conditions): sets LIVE_FLAG_UNK20000 "talkable".</summary>
    private void TalkCheck()
    {
        bool can = CanTalk && !CanBeTaken && Lines != null && Lines.Length > 0 && _talkForbid == 0 && !_talking && _mario != null
            && (_cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.TurnToMario || _cur == Nerve.Wet || _cur == Nerve.Mad || _cur == Nerve.WaitMarioApproach);
        if (can)
        {
            Vector3 m = MarioU(), p = PosU();
            float degree = Sitting ? SmsParams.Npc.SitTalkAcceptDegree : SmsParams.Npc.TalkAcceptDegree;
            can = Mathf.Abs(m.Y - p.Y) < SmsParams.Npc.TalkAcceptHeight
                && InSight(m, SmsParams.Npc.TalkAcceptDist, SmsParams.Npc.TalkAcceptHeight, degree)
                && MarioFacesUs(SmsParams.Npc.TalkAcceptDist, SmsParams.Npc.MarioTalkAcceptDegree);
        }
        bool bNow = Input.IsActionPressed("button_b");
        bool bJust = bNow && !_bLast;                 // edge-detected here: Mario's tick may run before or after ours (pass 59)
        _bLast = bNow;
        _talkable = can;
        if (!can) return;
        // TMarDirector::movement_game: MARIO_STATUS_WAIT, not holding, not the L camera, the nearest such NPC, MEANING_TALK_B
        if (_mario.SmsCanStartTalk && NearestTalkable() == this && bJust)
            StartTalk();
    }
    private bool _bLast;

    /// <summary>MsIsInSight(marioPos, marioAngleY, npcPos, dist, MarioTalkAcceptDegree): Mario's facing cone holds the NPC.</summary>
    private bool MarioFacesUs(float dist, float degree)
    {
        Vector3 d = GlobalPosition - _mario.GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() * U * U >= dist * dist) return false;
        if (d.LengthSquared() < 1e-8f) return true;
        Vector3 f = _mario.SmsFacingDir();
        return f.Dot(d.Normalized()) >= Mathf.Cos(Mathf.DegToRad(degree * 0.5f));
    }

    private static SmsNpc NearestTalkable()
    {
        SmsNpc best = null; float bestD = float.MaxValue;
        foreach (var n in All)
        {
            if (!n._talkable || n._mario == null) continue;
            float d = n.GlobalPosition.DistanceSquaredTo(n._mario.GlobalPosition);
            if (d < bestD) { bestD = d; best = n; }
        }
        return best;
    }

    public void StartTalk()
    {
        if (_talking || _mario == null) return;
        _talking = true; _windowOpen = false;
        _mario.SmsTalkStart(this);
        if (_cur == Nerve.Wet || _cur == Nerve.Mad) Enter(Nerve.Talk); else Push(Nerve.Talk);
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: talk start ({Lines.Length} page(s))");
    }

    private void TalkIn()
    {
        // npcTalkIn -> requestTalkAnm_: the sit-talk when sitting, else the talk clip
        Play(Sitting ? Profile.ClipSitTalk : Profile.ClipTalk);
        ResetToWait();
        if (TalkVoice != null && TalkVoice.Stream != null) TalkVoice.Play();
    }

    private void TickTalk()
    {
        _marchSpeed = 0f;
        if (Profile.TurnsToMarioWhenTalking && !Sitting && _mario != null)
            TurnToward(_mario.GlobalPosition, Profile.WaitTurnSpeed);      // npcTalking: SMS_GoRotate at the turn speed
        if (!_talking) TalkOut();
    }

    /// <summary>readBillboard reached its facing (gpMarDirector->unk126 = 2): the window opens.</summary>
    public void MarioFacesYou()
    {
        if (!_talking || _windowOpen) return;
        _windowOpen = true;
        var w = Window();
        if (w == null) { EndTalk(); return; }
        w.Open(Lines, this, (Profile.BodyHeight + Profile.CursorHeight) / U, EndTalk);
    }

    private SmsTalkWindow Window()
    {
        if (_window != null && IsInstanceValid(_window)) return _window;
        var scene = TalkWindowScene ?? GD.Load<PackedScene>(TalkWindowPath);
        if (scene == null) return null;
        _window = scene.Instantiate<SmsTalkWindow>();
        GetTree().Root.AddChild(_window);
        return _window;
    }

    private void EndTalk()
    {
        if (!_talking) return;
        _talking = false; _windowOpen = false;
        if (_mario != null) _mario.SmsTalkEnd();
    }

    /// <summary>npcTalkOut: talk forbidden 60 ticks, walking 120; changeNerveFromTalk_: back to what it was doing.</summary>
    private void TalkOut()
    {
        _talkForbid = SmsParams.Npc.TalkForbidTicksAfterTalk;
        _walkForbid = SmsParams.Npc.WalkForbidTicksAfterTalk;
        Pop();
    }

    // ------------------------------------------------------------------ taken (TTakeActor: Mario's HIT_MESSAGE_TAKE)
    /// <summary>behaveToBeTaken_ (NpcChange.cpp 240): the yaw difference to him is kept (mAngleYDiffWhenTaken), the position blends into his
    /// hands (startPosBlend, PosInbetweenFrame 6 frames), the hit filter goes NO_COLLISION (his body is not pushed by the capsule), the
    /// walk / talk flags clear, the nerve is set to WaitContinue -> with a holder: npcTakenIn (the TAKEN clip, resetToWait).</summary>
    public void SmsTaken(Mario holder, SmsNpcHoldProxy proxy)
    {
        if (holder == null || _holder != null) return;
        if (_talking) EndTalk();
        _mario = holder; _holder = holder; _holdProxy = proxy;
        _angleDiffWhenTaken = Mathf.Wrap(YawOf(holder.SmsFacingDir()) - YawRad, -Mathf.Pi, Mathf.Pi);
        _posBlendFrom = GlobalPosition; _posBlendTicks = SmsParams.Npc.PosInbetweenFrame * SmsUnits.TicksPerFrame;
        _thrown = false; _thrownVel = Vector3.Zero; Velocity = Vector3.Zero; _marchSpeed = 0f;
        _trampleAmp = 0f; _trampleShake = 0; _trampleToMad = 0; if (_model != null) _model.Scale = _modelBaseScale;
        _noCollisionTicks = 0;
        SetCollisionOff(true);
        _stack.Clear();
        Enter(Nerve.Taken);
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: taken (yaw diff {Mathf.RadToDeg(_angleDiffWhenTaken):F0} deg)");
    }

    private void TakenIn()
    {
        Play(Profile.ClipTaken);
        ResetToWait();
    }

    /// <summary>TBaseNPC::perform with mHolder: mPosition = the taking matrix's translation, mRotation.y = his yaw - the difference at the
    /// take; execPosInbetween (once a frame) eases the first 6 frames from the spot he lifted us at. No bind, no hit checks.</summary>
    private void TickHeldClocks()
    {
        _nerveTime++;
        if (_posBlendTicks > 0) _posBlendTicks--;
    }

    private void TickHeld()
    {
        if (!IsInstanceValid(_holder) || _holdProxy == null || !IsInstanceValid(_holdProxy)) { ReleaseTaken(); return; }
        Vector3 hand = _holdProxy.GlobalPosition;
        Vector3 pos = hand;
        int frames = _posBlendTicks / SmsUnits.TicksPerFrame;         // the frame the game's blend is on (it steps on the frame's last tick)
        if (frames >= 1) pos = hand + (_posBlendFrom - hand) * (frames / (float)SmsParams.Npc.PosInbetweenFrame);
        GlobalPosition = pos;
        Rotation = new Vector3(0f, Mathf.Wrap(YawOf(_holder.SmsFacingDir()) - _angleDiffWhenTaken, -Mathf.Pi, Mathf.Pi), 0f);
        Velocity = Vector3.Zero;
        if (_cursor != null) _cursor.Visible = false;
    }

    /// <summary>His hold ended (ensureTakeSituation / the proxy's release): releaseTaken_.</summary>
    public void SmsReleased() => ReleaseTaken();

    /// <summary>releaseTaken_ (NpcChange.cpp 216): mVelocity = (ThrowSpeedXZ sin, ThrowSpeedY, ThrowSpeedXZ cos) of HIS yaw -- the same for a
    /// throw and a put-down -- LIVE_FLAG_UNK10000000 on, unk1DC = 15 frames of no collision, the nerve set to WaitMarioApproach (its time 0:
    /// npcWaitIn). The flight is bind's: gravity a tick, the velocity kept whole until the ground zeroes it.</summary>
    private void ReleaseTaken()
    {
        Mario holder = _holder;
        _holder = null; _holdProxy = null; _posBlendTicks = 0;
        Vector3 f = holder != null && IsInstanceValid(holder) ? holder.SmsFacingDir() : DirOf(YawRad);
        f.Y = 0f; f = f.LengthSquared() > 1e-6f ? f.Normalized() : DirOf(YawRad);
        _thrownVel = f * SmsUnits.TickSpeedToMps(SmsParams.Npc.ThrowSpeedXZ) + Vector3.Up * SmsUnits.TickSpeedToMps(SmsParams.Npc.ThrowSpeedY);
        _thrown = true; _thrownMoves = 0;
        Velocity = _thrownVel;
        _noCollisionTicks = SmsParams.Npc.ReleaseNoCollisionFrames * SmsUnits.TicksPerFrame;
        _stack.Clear();
        Enter(Nerve.WaitMarioApproach);                               // its time 0 turns a Pianta back to its placed yaw, in the air as the game does
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: released -- flies {SmsParams.Npc.ThrowSpeedXZ} / {SmsParams.Npc.ThrowSpeedY} u/tick along his facing");
    }

    /// <summary>HIT_FILTER_NO_COLLISION in Godot terms: the capsule leaves its layer (nothing is pushed by it, Mario's hit checks skip it) and
    /// Mario's body is excepted from its own MoveAndSlide; the mask stays so it still stands on the map.</summary>
    private void SetCollisionOff(bool off)
    {
        if (off == _collisionOff) return;
        _collisionOff = off;
        if (off) { _layerSaved = CollisionLayer; CollisionLayer = 0; if (_mario != null) AddCollisionExceptionWith(_mario); }
        else { CollisionLayer = _layerSaved; if (_mario != null) RemoveCollisionExceptionWith(_mario); }
    }

    // ------------------------------------------------------------------ water
    public void SmsSprayedByWater(Vector3 hitPos) { if (!_collisionOff) Sprayed(); }   // HIT_FILTER_NO_COLLISION: no hit pairs while held / just released

    /// <summary>behaveToHitObject_(WATER_SPRAY): isNerveCanGoToWet (not already wet, not in a talk's first 4 ticks) -> pushNerve(Wet).</summary>
    private void Sprayed()
    {
        if (!Profile.ReactsToWater) return;
        bool can = _cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario || (_cur == Nerve.Talk && _nerveTime >= 4);
        if (!can) return;
        if (_cur == Nerve.Talk)
        {
            if (_window != null && IsInstanceValid(_window) && _window.IsOpen) _window.Close(); else EndTalk();   // the close callback ends the talk
            _talkForbid = SmsParams.Npc.TalkForbidTicksAfterTalk;
            Enter(Nerve.Wet);
            return;
        }
        Push(Nerve.Wet);
    }

    private void WetIn()
    {
        // npcWetIn: the wet clip, half the time the alternate (a sitting NPC: the sit-wet)
        string clip = Sitting ? Profile.ClipSitWet : (_rng.NextDouble() < 0.5 ? Profile.ClipWetAlt : Profile.ClipWet);
        Play(clip, blend: Profile.Kind == SmsNpcProfile.NpcKind.Toad); // a Pianta's wet is a cut (NPC_STOP_MOTION_BLEND_OFF)
        _marchSpeed = 0f; _turnSpeed = Profile.WaitTurnSpeed;
        if (WetVoice != null && WetVoice.Stream != null) WetVoice.Play();
    }

    private void TickWet()
    {
        _marchSpeed = 0f;
        if (!ClipEnded()) return;
        if (Profile.GetsMad && InMadRange()) { Enter(Nerve.Mad); return; }   // the wet Pianta: isStateGoToMad_ -> Mad
        Pop();                                                                 // npcWetOut
    }

    // ------------------------------------------------------------------ trample + mad
    /// <summary>TMario::hitNormal on the NPC's damage cylinder: falling onto it (not a dive) = HIT_MESSAGE_TRAMPLE -> behaveToBeTrampled_;
    /// the hose at close range = SPRAYED_BY_WATER (the Gooble's shortcut, the drops' ISmsSprayTarget hits also land here).</summary>
    private void HitCheckMario()
    {
        if (_mario == null) return;
        Vector3 g = PosU(), m = MarioU();
        bool hits = CheckDistance(m, SmsParams.Mario.TrampleRadius, SmsParams.Mario.AttackHeight, g, Profile.DamageRadius, Profile.DamageHeight);
        if (!hits) { _marioStompLatch = false; return; }
        // the velocity BEFORE his MoveAndSlide: landing on the NPC's own capsule zeroes Velocity.Y and sets the floor in the same tick (pass 64);
        // his feet above half the NPC's height: a grounded Mario beside it carries a floor-stick velocity that is not a landing
        bool falling = (_mario.SmsPreMoveVelocity.Y < -0.5f || (!_mario.IsOnFloor() && _mario.Velocity.Y < 0f)) && m.Y > g.Y + Profile.DamageHeight * 0.5f;
        var st = _mario.stateOfMario;
        bool dive = st == Mario.MarioState.diving || st == Mario.MarioState.singleJumpDive || st == Mario.MarioState.doubleJumpDive || st == Mario.MarioState.tripleJumpDive
            || st == Mario.MarioState.bellySlidingFromDive || st == Mario.MarioState.swimming || st == Mario.MarioState.hurt || st == Mario.MarioState.dead;
        if (falling && !dive && !_marioStompLatch && Profile.Trampled)
        {
            _marioStompLatch = true;
            Trampled();
            _mario.StompBounce();
        }
        if (_mario.SmsHoseSpraying) Sprayed();
    }

    private static bool CheckDistance(Vector3 a, float ar, float ah, Vector3 s, float sr, float sh)
    {
        if (a.Y > s.Y + sh) return false;
        if (a.Y + ah < s.Y) return false;
        float r = ar + sr, dx = a.X - s.X, dz = a.Z - s.Z;
        return r * r > dx * dx + dz * dz;
    }

    /// <summary>TNpcTrample::startTrample: amplitude 0.75 over 40 ticks, then the 60-tick count to Mad.</summary>
    private void Trampled()
    {
        _trampleAmp = SmsParams.Npc.TrampleAmplitude;
        _trampleShake = SmsParams.Npc.TrampleShakeFrames;
        _trampleToMad = 0;
        _trampleDec = _trampleAmp / _trampleShake;
        if (TrampleVoice != null && TrampleVoice.Stream != null) TrampleVoice.Play();
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: trampled");
    }

    /// <summary>TNpcTrample::updateTrample: scale.y = base x (1 + amp x -sin(phase)), phase += TrampleVelocity BAM a tick, amp -> 0; at the end
    /// the mad count; when that runs out a Pianta in range goes Mad.</summary>
    private void UpdateTrample()
    {
        if (_model == null) return;
        if (_trampleToMad > 0)
        {
            _trampleToMad--;
            _model.Scale = _modelBaseScale;
            if (_trampleToMad == 0 && Profile.GetsMad && InMadRange() && CanGoToMad()) GoMad();
        }
        else if (_trampleShake > 0)
        {
            _trampleShake--;
            if (_trampleShake == 0) { _model.Scale = _modelBaseScale; _trampleToMad = SmsParams.Npc.TrampleToMadFrames; return; }
            _trampleAmp -= _trampleDec;
            float phase = SmsParams.Npc.TrampleVelocity * (SmsParams.Npc.TrampleShakeFrames - _trampleShake) * SmsUnits.BamToRad;
            float amp = _trampleAmp * -Mathf.Sin(phase);
            _model.Scale = new Vector3(_modelBaseScale.X, _modelBaseScale.Y * (amp + 1f), _modelBaseScale.Z);
        }
    }

    private bool CanGoToMad() => _cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario || _cur == Nerve.Wet;
    private void GoMad() { if (_cur == Nerve.Wet) Enter(Nerve.Mad); else Push(Nerve.Mad); }

    private void MadIn()
    {
        _madFacing = false;
        if (Sitting) { Play(Profile.ClipMad); _madFacing = true; }   // npcMadIn: a sitting one goes straight to the mad clip
        else StepIn();
        if (MadVoice != null && MadVoice.Stream != null) MadVoice.Play();
    }
    private bool _madFacing;

    /// <summary>npcMadding: step to face Mario at MadTurnSpeed, then the mad clip once.</summary>
    private void TickMad()
    {
        _marchSpeed = 0f;
        if (!_madFacing)
        {
            if (_mario == null || TurnToward(_mario.GlobalPosition, Profile.MadTurnSpeed)) { _madFacing = true; Play(Profile.ClipMad, blend: false); }
            return;
        }
        if (ClipEnded()) Pop();
    }

    // ------------------------------------------------------------------ helpers for the drives
    public Vector3 NeckHeadWorld()
    {
        if (_skel == null || _neck == null) return GlobalPosition;
        int b = _skel.FindBone(Profile.NeckBone);
        return b < 0 ? GlobalPosition : _skel.GlobalTransform * _skel.GetBoneGlobalPose(b).Origin;
    }
}
