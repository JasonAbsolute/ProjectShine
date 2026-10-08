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
///  * the talk (changeNerveProc_ here, TMarDirector::movement_game in Mario.SmsDirectorTalkCheck): within TalkAcceptDist / Height,
///    inside the NPC's TalkAcceptDegree cone and Mario facing it within MarioTalkAcceptDegree -> LIVE_FLAG_UNK20000 "talkable"; the
///    DIRECTOR (Mario's tick, before any actor moves) finds the nearest talkable NPC while he stands (MARIO_STATUS_WAIT), not holding,
///    not in the L camera -> the cursor over its head, B's meaning TALK_B -> StartTalk: the NPC turns to him (isTurnToMarioWhenTalk,
///    the talk clip), Mario is set 100 units off and turns at ReadRotSp; when he faces it the director opens TTalk2D2 (SmsTalkBalloon,
///    pass 86: the talk camera, the blue bands rolling out, the glyphs fading in along the curve) on Lines; A pages; closing =
///    makeMtxForPrevTalk's npcTalkOut on EVERY NPC: talk forbidden 60 ticks, the talking one's walking 120 and its nerve resumed
///    (pass 85: pass 82 read B in the NPC's own tick, so an NPC after him in the tree only saw his B after waiting() had made it a
///    TAKE_POSE -- it never talked);
///  * the neck (NPCNeckCallBack): the head follows Mario within NeckTurnSearchDist -- SmsNpcNeck on the skeleton;
///  * the water (behaveToHitObject_): a spray -> TNerveNPCWet: the wet clip (50 % the alternate) to its end, then a Pianta in
///    MadSearchRange goes Mad (step to face him at MadTurnSpeed, the mad clip), else back to what it was doing;
///  * the hit (TMario::hitNpc, pass 85): his attack cylinder (HoldRadius 80 standing, TrampleRadius 100 falling, PushupRadius 60 rising)
///    against our damage cylinder (DamageRadius, shrunk to DamageRadiusSmall 10 while he is airborne above us within 3 R --
///    setVariableDamageRadius_) -> a jumping status falling with his feet above ours = trampleExec: MARIO_STATUS_TRAMPLE for him
///    (StompBounce), behaveToBeTrampled_ + TNpcTrample for us: a 40-tick squash wobble (scale.y x (1 - amp sin), amp 0.75 -> 0), then
///    after 60 ticks the Pianta goes Mad if Mario is in range; any other overlap = keepDistance: he is pushed out to DamageRadius + 50
///    (his wall radius) at most 50 units a tick, knocked down above ClashSpeed. The capsule excepts his body: an NPC is no floor (it was
///    one through pass 84 -- he stood on heads), its collision is this cylinder maths, as in the game;
///  * the look (NpcInitData.cpp colour tables + the bmd TEV, NpcSkin / NpcEye.gdshader): ColorIndex picks the register colour
///    (-1 = random), the model's materials are replaced per instance, the scene's DirectionalLight3D is the sun.
/// NOT ported (logged): the pollution (dirty / sink / wash), the Pianta that throws Mario (TNpcThrow), the balloon messages, sounds
/// (slots below), the talk camera, trampleExec's PARTICLE_MS_FUMI_B / C at half scale (no stomp dust is baked yet).</summary>
[Tool]
public partial class SmsNpc : CharacterBody3D, ISmsSprayTarget
{
    private const float U = SmsUnits.UnitsPerMetre;

    public enum IdleStyle { Wait, WaitA, WaitB, Talking, Angry, Dance, Sit, SitTalk }   // the placement's NPC_ACTION flags -> getNpcWaitAnmBase_ / UNK1 sit
    public enum Nerve { GraphWander, UTurn, GraphWait, WaitMarioApproach, TurnToMario, Talk, Wet, Mad, Taken, Sink, RecoverFromSink, RecoverAfter, Throw }

    [ExportGroup("Who")]
    [Export] public SmsNpcProfile Profile;
    /// <summary>Which entry of the profile's colour table; -1 = a random one per instance (the game numbers them from the stage data).</summary>
    [Export] public int ColorIndex = -1;
    [Export] public IdleStyle Idle = IdleStyle.Wait;
    [ExportGroup("Outfit")]
    /// <summary>The species' wardrobe (pass 88): its slots appear below as dropdowns -- None / Random / a part -- and are saved per instance
    /// (TNpcParts: the game's placement word picks the slots the same way). Profiles/Outfit*.tres; swap or extend it for your own parts.
    /// In the editor a chosen part shows on the model at once (a preview, not saved into the scene).</summary>
    [Export] public SmsNpcOutfitCatalog Outfit { get => _outfit; set { _outfit = value; NotifyPropertyListChanged(); if (Engine.IsEditorHint() && IsInsideTree()) CallDeferred(nameof(EditorPreviewOutfit)); } }
    private SmsNpcOutfitCatalog _outfit;
    /// <summary>Any extra part scenes (an SmsNpcPart root with its Joint: 'kubi' head, 'body_jnt' chest, 'handR_jnt' the right hand ...):
    /// things held, badges, your own models -- always worn, on top of the slots.</summary>
    [Export] public Godot.Collections.Array<PackedScene> ExtraParts = new();
    /// <summary>DEVIATION (his ask, pass 85): every slot left at None is rolled at _Ready like a Random one. A part placed by hand under this
    /// node keeps its slot out of the roll. Off = the game: only what the slots and the scene carry.</summary>
    [Export] public bool RandomOutfit = false;
    /// <summary>Scales every slot's odds: 0.5 = the catalog's (a Pianta's hat 60 %, moustache 35 %, glasses 25 %, lapel or tie 30 %, luggage 10 %).</summary>
    [Export(PropertyHint.Range, "0,1")] public float RandomOutfitDensity = 0.5f;
    /// <summary>The placement's three PART colour indices (setIndividualDifference_: the second colour triple): x = hats, y = moustache /
    /// belts / lapel / the W's flower centre, z = glasses (TNpcModelData's colour channel). -1 = random. Every part -- a slot's, an extra, or
    /// an SmsNpcPart scene dropped under this node by hand -- is hung on its joint and coloured from NpcInitData.cpp's tables.</summary>
    [Export] public Vector3I PartColorIndices = new Vector3I(-1, -1, -1);
    private readonly Dictionary<string, int> _outfitChoice = new();            // OutfitHat -> None 0 / Random 1 / a part 2+ (the dynamic properties)
    private readonly List<Node> _outfitPreview = new();                        // the editor's preview nodes (no owner: never saved)
    [ExportGroup("Talk")]
    /// <summary>The pages the window shows, one entry each; a page is up to three lines -- Enter in the inspector breaks a line (the game's
    /// .bmg pages are hand-broken the same way), and a long line wraps by words (the balloon's AutoWrap). Empty = this NPC cannot be
    /// talked to (no cursor). Pass 87: the hint moved onto the ELEMENTS (TypeString "4/18:") -- the array-level MultilineText of 82 left
    /// every page a single-line field, so no three-line page could be typed.</summary>
    [Export(PropertyHint.TypeString, "4/18:")] public string[] Lines = { "Welcome to Isle Delfino!", "Enjoy your stay." };
    [Export] public bool CanTalk = true;
    [ExportGroup("Take")]
    /// <summary>LIVE_FLAG_UNK100000 -- in the game the stage script sets it per NPC (evSetFlagNPCCanTaken): Mario touching this NPC with B
    /// lifts it (TMario::hitNpc -> canTake, the same isTakeSituation geometry as a fruit), carries it on his hands (its feet at his taking
    /// matrix, its facing offset to his kept), and a throw or a put-down both end as the NPC's own releaseTaken_: ThrowSpeedXZ 3 / Y 2.5
    /// units a tick along his facing, 15 frames of no collision, then it stands (WaitMarioApproach). findNearestTalkNPC skips a liftable
    /// NPC: B lifts, it never talks (no cursor). Off = the game's default.</summary>
    [Export] public bool CanBeTaken = false;
    [ExportGroup("Throw")]
    /// <summary>TNpcThrow (the placement's flag 0x1 with its speed and angle): when the talk with this NPC ends it runs TNerveNPCThrow -- the THROW
    /// clip (mom_throw, 119 frames), its collision off from tick ThrowStartFrame - 20 for 30 frames, and at tick ThrowStartFrame (40) Mario is
    /// sent HIT_MESSAGE_THROWN and thrown (TMario::throwMario): along (0, sin angle, -cos angle) turned by THIS NPC's yaw -- i.e. up and
    /// BEHIND the NPC, which faced him for the talk -- at ThrowSpeed units a tick, Mario facing back the way he came (ANIM_THROWN, the
    /// THROWN_DOWN status: no inputs but a little steering, two bounces, THROWN_END). A throw NPC accepts the talk only within
    /// ThrowTalkAcceptDist 200 / Height 150 (npcnormal.prm) instead of 400 / 500. The Pianta Village launchers.</summary>
    [Export] public bool ThrowsMario = false;
    /// <summary>TNpcThrow's speed: units a tick along the throw vector (Mario's mForwardVel / mVel.y take speed x the vector's parts).</summary>
    [Export] public float ThrowSpeed = 60f;
    /// <summary>TNpcThrow's angle in degrees above the ground: 0 = flat, 90 or more = straight up (CLBDegToShortAngle; the placement's value).</summary>
    [Export(PropertyHint.Range, "0,90")] public float ThrowAngleDeg = 45f;
    [ExportGroup("Pollution")]
    /// <summary>mPollutionAmount at the start (the placement's second colour triple's B / 255, NpcInitPrg.cpp 379): 1 = placed already goop-covered
    /// -- the DIRTY wait, no talk, no turning, the head straight -- until the hose washes it (PollutionCleanSpeed a hit). An NPC standing on
    /// painted goop sinks on its own (TNerveNPCSink) whatever this says; needs Profile.SinksInPollution.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Pollution = 0f;
    /// <summary>TNpcCoin (the placement's event id): when a HAPPY NPC -- just washed clean -- is talked to, a coin pops out at its cursor point
    /// 40 frames after the talk (requestAppearCoin: 15 units a tick at 75 deg up along its facing). Empty = no coin (the game's default too).</summary>
    [Export] public PackedScene ThankYouCoin;
    [ExportGroup("Wander")]
    /// <summary>Walk between points (the game's graph "main"). Off = stands where placed (an NPC without a graph).</summary>
    [Export] public bool Wander = false;
    /// <summary>A Node3D whose children (Marker3Ds) are the route; empty with Wander on = random points within WanderRadiusUnits.</summary>
    [Export] public Node3D Graph;
    [Export] public float WanderRadiusUnits = 600f;
    [ExportGroup("Scene")]
    [Export] public NodePath ModelPath = "Model";
    [Export] public PackedScene CursorScene;
    [Export] public Shader SkinShader;
    [Export] public Shader EyeShader;
    [Export] public Texture2D ToonTexture;
    /// <summary>H_ma_polmask_sample_i4 (every NPC bmd carries the same 32 x 64 gradient) and the stage's /map/pollution/h_ma_rak.bti (Delfino
    /// Plaza's here): the skin shader's pollution mask and goop. Empty = the defaults under assets/Sms/Npc/tex.</summary>
    [Export] public Texture2D PollutionMaskTexture, GoopTexture;
    /// <summary>Sound slots (the sound bank pass fills them): nothing assigned = silent.</summary>
    [Export] public AudioStreamPlayer3D TalkVoice, WetVoice, TrampleVoice, MadVoice;
    [Export] public bool DebugPrint = false;

    public const string SkinShaderPath = "res://assets/Sms/Npc/NpcSkin.gdshader";
    public const string EyeShaderPath = "res://assets/Sms/Npc/NpcEye.gdshader";
    public const string ToonPath = "res://assets/Sms/models/toon_2_4i.png";
    public const string PollutionMaskPath = "res://assets/Sms/Npc/tex/H_ma_polmask_sample.png";
    public const string GoopPath = "res://assets/Sms/Npc/tex/H_ma_rak.png";
    public const string CursorPath = "res://assets/Sms/Npc/TalkCursor.tscn";

    // ---- state ----
    private static readonly List<SmsNpc> All = new();
    public static IReadOnlyList<SmsNpc> Everyone => All;     // Mario's take search (hitNpc) reads the stage's NPCs here
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
    private bool _marioExcepted;                         // his body is excepted from the capsule: the hit check below is our collision with him
    private float _damageRadiusNow;                      // mDamageRadius after setVariableDamageRadius_ (units)
    private int _colorIdx;
    private readonly int[] _partColorIdx = { -1, -1, -1 };     // the three part colour indices once drawn
    private readonly List<SmsNpcPart> _parts = new();
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
    // ---- pollution (pass 93) ----
    private float _pollution;                            // mPollutionAmount 0..1 (isClean: == 0)
    private float _sinkGroundY;                          // unk1C4: the ground he stood on when the sink began (metres)
    private bool _sinking;                               // LIVE_FLAG_UNK400000: sinking, sunk or coming back out (no bind, the sink effect)
    private bool _sunk;                                  // LIVE_FLAG_SINK_BOTTOM: at the bottom (no collision, the WAIT_FALL clip)
    private bool _recoverJumped; private float _sinkVy;  // LIVE_FLAG_UNK8000000 + mVelocity.y while the APPEAR clip's jump is in the air
    private bool _thanks;                                // UNK1DA_FLAG_UNK2: washed clean this wet -- the happy pose, the talk forbid
    private bool _happy; private int _coinTimer;         // NPC_ACTION_HAPPY (until the next talk ends: npcTalkOut) + TNpcCoin's 40-frame count
    private int _sinkPoll;                               // TNpcSink: changeNerveProc_ polls the goop once every SinkPollFrames (staggered per NPC)
    private static int _sinkPollStagger;                 // sCheckPollutedStartCounter
    private readonly List<ShaderMaterial> _skinMats = new();   // the body / cloth materials carrying the `pollution` uniform
    private bool _polDirty = true;
    public float PollutionAmount => _pollution;
    public bool IsClean => _pollution <= 0f;
    public bool IsSinking => _sinking;
    public bool IsSunk => _sunk;
    public bool IsHappy => _happy;
    private bool DirtyOrHappy => _pollution > 0f || _happy;

    public Nerve CurrentNerve => _cur;
    public bool Talkable => _talkable;
    public bool IsTalking => _talking;
    /// <summary>TBaseNPC::isSmallNpc (NpcBase.cpp 250): a Toad (KINOPIO / KINOJII), a child, a Noki -- the talk camera's TALK_B and the balloon's other park.</summary>
    public bool IsSmallNpc => Profile != null && Profile.Kind == SmsNpcProfile.NpcKind.Toad;
    /// <summary>TBaseNPC::getFocalPoint (NpcBase.cpp 427): mPosition + scale.y x (mSLBodyHeight - mSLLookatHeight) -- what Mario's head looks at
    /// during the talk (MarioHeadCtrl) and the talk camera's look point.</summary>
    public Vector3 FocalPoint => GlobalPosition + Vector3.Up * (Scale.Y * (Profile.BodyHeight - Profile.LookatHeight) / SmsUnits.UnitsPerMetre);
    /// <summary>The talk balloon's opening on this NPC (the director's unk124 == 2 for it).</summary>
    public bool TalkWindowOpen => _windowOpen;
    public bool IsHeld => _holder != null;
    public bool IsThrown => _thrown;
    /// <summary>The receive cylinder Mario's take test reads (isTakeSituation: his DamageRadius against the object's).</summary>
    public float HitRadiusUnits => Profile != null ? Profile.DamageRadius : 100f;
    public float HitHeightUnits => Profile != null ? Profile.DamageHeight : 200f;
    /// <summary>isNowCanTaken: the flag, no holder, collision on (not the 15 frames after a release), not talking, a nerve the take may
    /// interrupt (isNerveCanGoToTaken: the wander / wait / approach / turn / wet family -- not Talk, not Mad).</summary>
    public bool IsNowCanTaken() => CanBeTaken && Profile != null && _holder == null && !_collisionOff && !_talking && !_sinking && IsInsideTree()
        && (_cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario || _cur == Nerve.Wet);
    public string CurrentClip => _clip;
    public float MarchSpeed => _marchSpeed;
    public int ColorIdx => _colorIdx;
    public int PartColorIdx(int channel) => _partColorIdx[Mathf.Clamp(channel, 0, 2)];
    public IReadOnlyList<SmsNpcPart> Parts => _parts;
    public SmsNpcNeck Neck => _neck;
    public bool NeckAllowed => _pollution <= 0f && _holder == null;   // isNeedNeckStraight (NpcBase.cpp 381): held by Mario or not clean -> the head stays straight (pass 93; 82 read only the type cases)

    public Vector3 PosU() => GlobalPosition * U;
    public Vector3 MarioPositionMetres() => _mario != null ? _mario.GlobalPosition : Vector3.Inf;
    private Vector3 MarioU() => _mario != null ? _mario.GlobalPosition * U : PosU();
    private float YawRad => Rotation.Y;
    private static float YawOf(Vector3 d) => Mathf.Atan2(-d.X, -d.Z);              // DirFromYaw(y) = (-sin y, 0, -cos y): forward = -Z
    private static Vector3 DirOf(float yaw) => new(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { CallDeferred(nameof(EditorPreviewOutfit)); return; }   // [Tool]: the outfit preview, nothing else runs here
        All.Add(this);
        _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        if (Profile == null)
        {
            // pass 91: his Level carried 'Profile = null' overrides the editor wrote while the cast failed; the default is a male Pianta's
            GD.PushError($"[SMS npc] {Name}: Profile is null (an instance override? revert it in the inspector) -- running on the default profile, whose clips are a male Pianta's");
            Profile = new SmsNpcProfile();
        }
        _firstYaw = Rotation.Y;
        _model = GetNodeOrNull<Node3D>(ModelPath);
        if (_model != null)
        {
            _modelBaseScale = _model.Scale;
            _anim = FindChild<AnimationPlayer>(_model);
            _skel = FindChild<Skeleton3D>(_model);
        }
        _colorIdx = ColorIndex >= 0 ? ColorIndex % Profile.ColorCount : _rng.Next(Profile.ColorCount);
        _damageRadiusNow = Profile.DamageRadius;
        _pollution = Profile.SinksInPollution ? Mathf.Clamp(Pollution, 0f, 1f) : 0f;      // NpcInitPrg.cpp 379: the placement's B / 255
        _sinkPoll = (_sinkPollStagger++ % SmsParams.Npc.SinkPollFrames) * SmsUnits.TicksPerFrame;   // initSinkNpc_: each NPC polls on its own frame
        ExceptMario();
        SetupLook();
        SetupOutfit();
        SetupParts();
        SetupNeck();
        SetupCursor();
        MarkLoops();
        _stack.Clear();
        if (Wander) { ChooseNextTarget(); Enter(Nerve.GraphWander); }
        else Enter(Nerve.WaitMarioApproach);
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: {Profile.Kind} colour {_colorIdx}, clips {(_anim != null ? _anim.GetAnimationList().Length : 0)}, neck {(_neck != null ? "on" : "off")}");
    }

    public override void _ExitTree() { if (Engine.IsEditorHint()) return; All.Remove(this); }

    /// <summary>The THitActor side of Mario vs an NPC: his body never collides with our capsule (an NPC is not map collision -- he cannot
    /// stand on one, he is kept off by keepDistance in HitCheckMario); the capsule still stands on the map and meets other bodies.</summary>
    private void ExceptMario()
    {
        if (_marioExcepted || _mario == null) return;
        AddCollisionExceptionWith(_mario);
        _marioExcepted = true;
    }

    // ---- the outfit (pass 88): the catalog's slots as inspector dropdowns, saved per instance as OutfitHat / OutfitGlasses / ... ----
    public int OutfitChoice(SmsNpcOutfitSlot slot) => slot != null && _outfitChoice.TryGetValue(SmsNpcOutfitCatalog.PropertyFor(slot), out int v) ? v : SmsNpcOutfitSlot.None;
    public void SetOutfitChoice(SmsNpcOutfitSlot slot, int choice) { if (slot != null) _outfitChoice[SmsNpcOutfitCatalog.PropertyFor(slot)] = choice; }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (_outfit == null) return list;
        // a group of its own (prefix "Outfit" stripped from the names): the dynamic properties come after every static export
        list.Add(new Godot.Collections.Dictionary { { "name", "Outfit Slots" }, { "type", (int)Variant.Type.Nil }, { "hint_string", "Outfit" }, { "usage", (int)PropertyUsageFlags.Group } });
        foreach (var slot in _outfit.Slots)
        {
            if (slot == null || string.IsNullOrEmpty(slot.Name)) continue;
            list.Add(new Godot.Collections.Dictionary
            {
                { "name", SmsNpcOutfitCatalog.PropertyFor(slot) },
                { "type", (int)Variant.Type.Int },
                { "hint", (int)PropertyHint.Enum },
                { "hint_string", slot.OptionHint() },
                { "usage", (int)PropertyUsageFlags.Default },
            });
        }
        return list;
    }

    private static bool IsOutfitProperty(string name) => name.Length > 6 && name.StartsWith("Outfit") && name != "Outfit";

    public override Variant _Get(StringName property)
    {
        string n = property;
        if (!IsOutfitProperty(n)) return default;
        return _outfitChoice.TryGetValue(n, out int v) ? v : SmsNpcOutfitSlot.None;
    }

    public override bool _Set(StringName property, Variant value)
    {
        string n = property;
        if (!IsOutfitProperty(n)) return false;
        _outfitChoice[n] = value.AsInt32();          // taken even before the catalog has loaded (a scene file lists the properties in its own order)
        if (Engine.IsEditorHint() && IsInsideTree()) CallDeferred(nameof(EditorPreviewOutfit));
        return true;
    }

    public override bool _PropertyCanRevert(StringName property) => IsOutfitProperty(property) && _outfitChoice.TryGetValue(property, out int v) && v != SmsNpcOutfitSlot.None;
    public override Variant _PropertyGetRevert(StringName property) => SmsNpcOutfitSlot.None;

    /// <summary>The part scenes this instance wears from its choices: each slot's pick (Random -- or None with RandomOutfit on -- rolled at the
    /// slot's odds unless a hand-placed part already fills the slot), then the ExtraParts. <paramref name="preview"/> leaves the rolls out.</summary>
    private List<PackedScene> OutfitScenes(bool preview)
    {
        var picks = new List<PackedScene>();
        var placed = new HashSet<string>();
        var existing = new List<SmsNpcPart>();
        CollectParts(this, existing);
        foreach (var part in existing) if (!string.IsNullOrEmpty(part.SceneFilePath)) placed.Add(part.SceneFilePath);
        if (_outfit != null)
            foreach (var slot in _outfit.Slots)
            {
                if (slot == null) continue;
                int choice = OutfitChoice(slot);
                if (choice >= SmsNpcOutfitSlot.FirstPart) { picks.AddRange(slot.ScenesFor(choice)); continue; }
                bool roll = choice == SmsNpcOutfitSlot.Random || (choice == SmsNpcOutfitSlot.None && RandomOutfit);
                if (!roll || preview) continue;
                bool filled = false;
                foreach (var path in placed) if (slot.Holds(path)) { filled = true; break; }
                if (!filled) picks.AddRange(slot.Roll(_rng, RandomOutfitDensity));
            }
        foreach (var extra in ExtraParts) if (extra != null) picks.Add(extra);
        return picks;
    }

    /// <summary>At _Ready: the outfit's scenes instanced under the Parts node (or this node); SetupParts then hangs and colours them with any
    /// part placed by hand. TNpcParts' twelve slots, chosen in the inspector instead of the placement word.</summary>
    private void SetupOutfit()
    {
        Node holder = GetNodeOrNull<Node3D>("Parts") ?? (Node)this;
        int n = 0;
        foreach (var scene in OutfitScenes(false))
        {
            var part = scene.Instantiate() as SmsNpcPart;
            if (part == null) { GD.PushWarning($"[SMS npc] {Name}: {scene.ResourcePath} is not an SmsNpcPart scene"); continue; }
            holder.AddChild(part); n++;
        }
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: outfit: {n} part(s) from the slots / extras, {CountParts()} total");
    }

    /// <summary>[Tool]: the chosen parts shown on the model in the editor -- instanced with no owner (never saved), hung on the skeleton's joints
    /// and coloured like the game's, redone on every change of a slot, the catalog or the extras. Random slots show nothing until play.</summary>
    private void EditorPreviewOutfit()
    {
        if (!Engine.IsEditorHint() || !IsInsideTree()) return;
        foreach (var n in _outfitPreview) if (IsInstanceValid(n)) { n.GetParent()?.RemoveChild(n); n.QueueFree(); }
        _outfitPreview.Clear();
        var model = GetNodeOrNull<Node3D>(ModelPath);
        var skel = model != null ? FindChild<Skeleton3D>(model) : null;
        if (model == null) return;
        var toon = ToonTexture ?? GD.Load<Texture2D>(ToonPath);
        foreach (var scene in OutfitScenes(true))
        {
            var part = scene.Instantiate() as SmsNpcPart;
            if (part == null) continue;
            Node holder = model;
            if (skel != null && part.Joint != SmsNpcPart.RootJoint && skel.FindBone(part.Joint) >= 0)
            {
                var att = new BoneAttachment3D { Name = "PreviewPart_" + part.Name };
                skel.AddChild(att);
                att.BoneName = part.Joint;
                _outfitPreview.Add(att);
                holder = att;
            }
            holder.AddChild(part);
            part.Transform = Transform3D.Identity;
            if (holder == model) _outfitPreview.Add(part);
            int ch = Mathf.Clamp(part.ColorChannel, 0, 2);
            int idx = ch == 0 ? PartColorIndices.X : ch == 1 ? PartColorIndices.Y : PartColorIndices.Z;
            part.Apply(Mathf.Max(idx, 0), toon, null);
        }
    }
    private int CountParts() { var l = new List<SmsNpcPart>(); CollectParts(this, l); return l.Count; }

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
        var polMask = PollutionMaskTexture ?? GD.Load<Texture2D>(PollutionMaskPath);
        var goop = GoopTexture ?? GD.Load<Texture2D>(GoopPath);
        var sun = FindSun(GetTree().CurrentScene);
        bool toad = Profile.Kind == SmsNpcProfile.NpcKind.Toad;
        _skinMats.Clear();
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
                if (m.Shader == skin)
                {
                    // the pollution stages (pass 93): the mask gradient, the stage's goop texture, the Toad's TEX1 when the mesh carries it
                    m.SetShaderParameter("pol_mask_tex", polMask);
                    m.SetShaderParameter("pol_tex", goop);
                    bool uv2 = false;
                    try { var arrays = mi.Mesh.SurfaceGetArrays(s); uv2 = arrays.Count > (int)Mesh.ArrayType.TexUV2 && arrays[(int)Mesh.ArrayType.TexUV2].VariantType != Variant.Type.Nil; } catch { }
                    m.SetShaderParameter("pol_uv2", uv2);
                    _skinMats.Add(m);
                }
                mi.SetSurfaceOverrideMaterial(s, m);
            }
        }
        _polDirty = true;
        UpdatePollutionUniform();
    }

    /// <summary>GX_KCOLOR0.a = mPollutionAmount x mPollutionMax (NpcBase.cpp 718, an 8-bit register): the skin shader's compare value.</summary>
    private void UpdatePollutionUniform()
    {
        if (!_polDirty) return;
        _polDirty = false;
        float k = Mathf.Floor(Mathf.Clamp(_pollution, 0f, 1f) * Profile.PollutionMax) / 255f;
        foreach (var m in _skinMats) m.SetShaderParameter("pollution", k);
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

    /// <summary>TNpcParts: every SmsNpcPart found under this node (not inside the model) is a TSharedParts on the joint its data names --
    /// a BoneAttachment3D on the NPC's skeleton carries it with no offset (the part is authored in the joint's space); "__ROOT_JOINT__" or
    /// a joint the skeleton lacks puts it on the model root. Its colour row is the NPC's index for the part's channel (PartColorIndices,
    /// -1 = drawn once per channel from the parts' table lengths), the toon ramp and the sun are the body's.</summary>
    private void SetupParts()
    {
        _parts.Clear();
        CollectParts(this, _parts);
        if (_parts.Count == 0) return;
        var toon = ToonTexture ?? GD.Load<Texture2D>(ToonPath);
        var sun = FindSun(GetTree().CurrentScene);
        for (int ch = 0; ch < 3; ch++)
        {
            int set = ch == 0 ? PartColorIndices.X : ch == 1 ? PartColorIndices.Y : PartColorIndices.Z;
            if (set >= 0) { _partColorIdx[ch] = set; continue; }
            int rows = 0;
            foreach (var part in _parts) if (part.ColorChannel == ch) rows = Mathf.Max(rows, part.ColorRows);
            _partColorIdx[ch] = rows > 0 ? _rng.Next(rows) : 0;
        }
        foreach (var part in _parts)
        {
            Node holder = _model;
            if (_skel != null && part.Joint != SmsNpcPart.RootJoint && _skel.FindBone(part.Joint) >= 0)
            {
                var att = new BoneAttachment3D { Name = "Part_" + part.Name };
                _skel.AddChild(att);
                att.BoneName = part.Joint;
                holder = att;
            }
            if (holder != null && part.GetParent() != holder)
            {
                part.GetParent()?.RemoveChild(part);
                holder.AddChild(part);
            }
            part.Transform = Transform3D.Identity;
            part.Apply(_partColorIdx[Mathf.Clamp(part.ColorChannel, 0, 2)], toon, sun);
        }
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: {_parts.Count} part(s), colours {_partColorIdx[0]} / {_partColorIdx[1]} / {_partColorIdx[2]}");
    }

    private void CollectParts(Node n, List<SmsNpcPart> into)
    {
        foreach (var c in n.GetChildren())
        {
            if (c == _model) continue;                         // the model's own meshes are not parts
            if (c is SmsNpcPart part) { into.Add(part); continue; }
            CollectParts(c, into);
        }
    }

    private void SetupNeck()
    {
        if (_skel == null || string.IsNullOrEmpty(Profile.NeckBone) || _skel.FindBone(Profile.NeckBone) < 0) return;
        _neck = new SmsNpcNeck { Name = "SmsNpcNeck", Owner = this, BoneName = Profile.NeckBone };
        _skel.AddChild(_neck);
    }

    private void SetupCursor()
    {
        SmsTalkBalloon.Ensure(this);   // the stage's balloon loads (and its shader compiles) with the level, not at the first talk
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
        string[] loops = { p.ClipWalk, p.ClipRun, p.ClipWait, p.ClipWaitA, p.ClipWaitB, p.ClipTalk, p.ClipSit, p.ClipSitTalk, p.ClipMadLoop, p.ClipDance, p.ClipDirty, p.ClipWaitFall, p.ClipTaken, p.ClipHappy, p.ClipFall };   // + happy / fall (pass 93: the .bck loop attribute is 2 on both)
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
        if (_anim == null || string.IsNullOrEmpty(clip) || !_anim.HasAnimation(clip))
        {
            if (_anim != null && !string.IsNullOrEmpty(clip) && _warnedClips.Add(clip))   // pass 91: the female on a male profile stood in her rest pose with no word
                GD.PushWarning($"[SMS npc] {Name}: no clip '{clip}' in {_model?.Name} (profile {Profile?.ResourceName}) -- the model keeps its pose");
            _clip = clip ?? ""; return;
        }
        if (_clip == clip && _anim.IsPlaying()) { _anim.SpeedScale = rate; return; }
        _clip = clip;
        _anim.Play(clip, blend ? SmsParams.Npc.MotionBlendFrame / 60f : 0f, rate);
        _clipLoops = _anim.GetAnimation(clip).LoopMode != Animation.LoopModeEnum.None;
    }

    private bool ClipEnded() => _anim == null || string.IsNullOrEmpty(_clip) || !_anim.HasAnimation(_clip) || (!_clipLoops && !_anim.IsPlaying());

    private string WaitClip()
    {
        var p = Profile;
        if (_pollution > 0f) return p.ClipDirty;         // npcWaitIn: !isClean -> NPC_ANM_KIND_DIRTY (mom_wait_yogore)
        if (_happy) return p.ClipHappy;                  // NPC_ACTION_HAPPY -> the happy pose until the next talk ends
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
            case Nerve.Sink: break;                  // npcFallIn ran at the push (changeNerveProc_)
            case Nerve.RecoverFromSink: RecoverIn(); break;
            case Nerve.RecoverAfter: Play(Profile.ClipRecover); ResetToWait(); break;   // npcRecoverAfterIn: the recover clip once
            case Nerve.Throw: Play(Profile.ClipThrow); ResetToWait(); _threw = false; break;   // npcThrowIn
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
    private readonly HashSet<string> _warnedClips = new();          // pass 91: one warning per missing clip name

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || Profile == null) return;
        if (_mario == null) { _mario = GetTree().GetFirstNodeInGroup("player") as Mario; ExceptMario(); }
        int n = SmsUnits.TicksFor(delta, ref _tickAccum);              // TLiveActor: every 120 Hz tick of the frame (pass 83; 82 ran one a frame)
        if (_holder != null) { for (int i = 0; i < n; i++) TickHeldClocks(); TickHeld(); return; }   // perform with mHolder: no bind
        for (int i = 0; i < n; i++)
        {
            Tick();
            if (_holder != null) { TickHeld(); return; }               // taken inside the frame: the held path owns the rest of it
        }
        Move();
        UpdatePollutionUniform();
        // TTalkCursor::associateNPC: the director's cursor sits on the NPC it found this frame (none while he walks: findNearestTalkNPC wants WAIT)
        if (_cursor != null) _cursor.Visible = !_talking && _mario != null && _mario.SmsTalkTargetNpc == this;
    }

    /// <summary>One 120 Hz tick: moveObject (updateForbidCount_, the trample, the hit checks, changeNerveProc_'s talk accept) and the spine.</summary>
    private void Tick()
    {
        if (_talkForbid > 0) _talkForbid--;
        if (_walkForbid > 0) _walkForbid--;
        if (_noCollisionTicks > 0 && --_noCollisionTicks == 0) SetCollisionOff(false);   // unk1DC ran out: the hit filter back on
        UpdateTrample();
        if (!_collisionOff && _mario != null && !_mario.SmsIsTalking) HitCheckMario();   // HIT_FILTER_NO_COLLISION / READ_BILLBOARD (FLAG_UNK1000): no hit pairs
        else _marioHits = false;
        TalkCheck();
        PollutionCheck();
        if (_coinTimer > 0 && _mario != null && !_mario.SmsIsTalking && --_coinTimer == 0) AppearCoin();   // TNpcCoin::updateCoin: counts outside a talk
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
            case Nerve.Sink: TickSink(); break;
            case Nerve.RecoverFromSink: TickRecover(); break;
            case Nerve.RecoverAfter: _marchSpeed = 0f; if (ClipEnded()) Pop(); break;
            case Nerve.Throw: TickThrow(); break;
        }
    }

    // ------------------------------------------------------------------ the throw (pass 94): npcThrowing + TNpcThrow::throwMario
    private bool _threw;
    /// <summary>npcThrowing (NpcAnm.cpp 768): at spine tick ThrowStartFrame - 20 (or 0 when it is under 20) the hit filter goes NO_COLLISION for
    /// 30 frames (unk1DC); at tick ThrowStartFrame TNpcThrow::throwMario; the nerve ends with the clip.</summary>
    private void TickThrow()
    {
        _marchSpeed = 0f;
        int start = SmsParams.Npc.ThrowStartFrame;
        if ((_nerveTime == 1 && start < 20) || _nerveTime == start - 20)
        {
            SetCollisionOff(true);
            _noCollisionTicks = SmsParams.Npc.ThrowNoCollisionFrames * SmsUnits.TicksPerFrame;
        }
        else if (_nerveTime == start && !_threw)
        {
            _threw = true;
            ThrowMario();
        }
        else if (ClipEnded()) Pop();
    }

    /// <summary>TNpcThrow::throwMario (NpcThrow.cpp 8): the vector (0, sin a, -cos a) -- (0, 1, 0) at 90 deg or more, (0, 0, -1) at 0 or less --
    /// turned by the NPC's yaw (x' = x cos + z sin, z' = -x sin + z cos: the game's actor forward is +Z, so -cos lands BEHIND it), then
    /// HIT_MESSAGE_THROWN to Mario and SMS_ThrowMario(vector, speed).</summary>
    private void ThrowMario()
    {
        if (_mario == null) return;
        float a = Mathf.DegToRad(Mathf.Clamp(ThrowAngleDeg, 0f, 90f));
        Vector3 v = ThrowAngleDeg >= 90f ? Vector3.Up : (ThrowAngleDeg <= 0f ? Vector3.Back : new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)));
        // the game's -Z about the NPC's yaw is its BACK (facing = +Z there); the port's facing is -Z (DirOf), so the back is -DirOf
        Vector3 back = -DirOf(YawRad);
        Vector3 dir = back * v.Z + Vector3.Up * v.Y;
        _mario.SmsThrowMario(dir, ThrowSpeed, this);
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: throwMario dir {dir} speed {ThrowSpeed} u/tick (angle {ThrowAngleDeg} deg)");
    }

    // ------------------------------------------------------------------ pollution (pass 93): changeNerveProc_ 539-590, npcSinking, npcRecoverFromSinking
    /// <summary>changeNerveProc_'s pollution half, once a frame in the game and once every SinkPollFrames per NPC (TNpcSink, staggered): a sunk NPC
    /// whose goop was cleaned comes back out (TNerveNPCRecoverFromSink); a standing one on painted goop goes under (npcFallIn + TNerveNPCSink).</summary>
    private void PollutionCheck()
    {
        if (!Profile.SinksInPollution || _talking || _happy || _cur == Nerve.Mad || _holder != null) return;   // the talk returns first; BURNING / UNK400 / HAPPY skip
        if (--_sinkPoll > 0) return;
        _sinkPoll = SmsParams.Npc.SinkPollFrames * SmsUnits.TicksPerFrame;
        if (_cur == Nerve.Sink)
        {
            if (SmsPollutionMap.IsPolluted(new Vector3(GlobalPosition.X, _sinkGroundY, GlobalPosition.Z))) return;
            Enter(Nerve.RecoverFromSink);                                   // setNext: the stack under the sink stays for the way back
            return;
        }
        if (_sinking) return;                                               // LIVE_FLAG_UNK400000: on the way out
        if (!IsOnFloor()) return;                                           // isAirborne
        if (!SmsPollutionMap.IsPolluted(GlobalPosition)) return;
        bool can = _cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario;   // isNerveCanGoToSink
        if (!can) return;
        _sinkGroundY = GlobalPosition.Y;
        Velocity = Vector3.Zero; _marchSpeed = 0f;
        _talkable = false; _sinking = true; _sunk = false;
        Play(Profile.ClipFall); ResetToWait();                              // npcFallIn: the FALL clip
        Push(Nerve.Sink);
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: on goop at y {_sinkGroundY * U:F1} u -> sinking {Profile.SinkHeight} u at {Profile.SinkSpeed} u/tick");
    }

    /// <summary>npcSinking: y chases ground - SinkHeight at SinkSpeed a tick while the amount chases 1 over the same ticks (CLBChaseConstantSpecifyFrame
    /// with frames = distance / speed); at the bottom LIVE_FLAG_SINK_BOTTOM, HIT_FILTER_NO_COLLISION and the WAIT_FALL clip.</summary>
    private void TickSink()
    {
        _marchSpeed = 0f;
        float groundU = _sinkGroundY * U, yU = GlobalPosition.Y * U, targetU = groundU - Profile.SinkHeight;
        if (yU == targetU) return;
        float frames = (1f / Profile.SinkSpeed) * (yU - targetU);
        if (frames < 0.001f) _pollution = 1f; else _pollution += (1f / frames) * (1f - _pollution);
        _polDirty = true;
        float d = targetU - yU, sp = Mathf.Abs(Profile.SinkSpeed);        // CLBChaseGeneralConstantSpecifySpeed
        if (d > 0f) { d -= sp; yU = d > 0f ? targetU - d : targetU; }
        else { d += sp; yU = d < 0f ? targetU - d : targetU; }
        SetFeetY(yU / U);
        if (yU == targetU)
        {
            _sunk = true; _pollution = 1f;
            SetCollisionOff(true);
            Play(Profile.ClipWaitFall);                                     // NPC_ANM_KIND_UNK10: stuck in the goop
            if (DebugPrint) GD.Print($"[SMS npc] {Name}: sunk ({Profile.ClipWaitFall}), pollution {_pollution:F2}");
        }
    }

    /// <summary>The way out (changeNerveProc_ 557 + npcRecoverFromSinking): collision back, the APPEAR clip; when it passes frame 32 a jump of
    /// CalcJumpVelocityY(ground - y + 150) (min 5 a tick) under the NPC's gravity; the clip's end puts him on his ground and TNerveNPCRecoverAfter
    /// plays the RECOVER clip -- he is still dirty (the amount stays 1 until the hose).</summary>
    private void RecoverIn()
    {
        _sunk = false; _recoverJumped = false; _sinkVy = 0f;
        SetCollisionOff(false);
        Play(Profile.ClipAppear); ResetToWait();
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: the goop under me is clean -> {Profile.ClipAppear}");
    }

    private void TickRecover()
    {
        _marchSpeed = 0f;
        float groundU = _sinkGroundY * U, yU = GlobalPosition.Y * U;
        if (!_recoverJumped && ClipFramesPassed(SmsParams.Npc.RecoverJumpFrame))
        {
            _recoverJumped = true;
            _sinkVy = CalcJumpVelocityY(groundU - yU + SmsParams.Npc.RecoverJumpHeight, SmsParams.Npc.GravityY);
            if (_sinkVy < 5f) _sinkVy = 5f;
            if (DebugPrint) GD.Print($"[SMS npc] {Name}: recover jump vy {_sinkVy:F2} u/tick from {yU - groundU:F1} u");
        }
        if (ClipEnded())
        {
            SetFeetY(_sinkGroundY);
            _sinking = false; _recoverJumped = false; _sinkVy = 0f; Velocity = Vector3.Zero;
            Enter(Nerve.RecoverAfter);
            return;
        }
        if (_recoverJumped)
        {
            _sinkVy -= SmsParams.Npc.GravityY;
            if (_sinkVy < SmsParams.Npc.VelocityMinY) _sinkVy = SmsParams.Npc.VelocityMinY;
            yU += _sinkVy;
            if (_sinkVy <= 0f && yU < groundU) yU = groundU;
            SetFeetY(yU / U);
        }
    }

    /// <summary>CalcJumpVelocityY (NpcAnm.cpp 27): the per-tick vy that peaks `height` above the start under `gravity` a tick a tick.</summary>
    public static float CalcJumpVelocityY(float height, float gravity)
    {
        if (gravity <= 0f) return 0f;
        return gravity * 0.5f * (Mathf.Sqrt(height * (1f / gravity) * 8f + 1f) + 1f);
    }

    private void SetFeetY(float y) { var p = GlobalPosition; p.Y = y; GlobalPosition = p; }

    /// <summary>getFrameCtrl(BCK)->checkPass(frame): the current clip's 60 fps frame counter has reached `frame`.</summary>
    private bool ClipFramesPassed(float frame) => _anim != null && _anim.IsPlaying() && _anim.CurrentAnimation == _clip && _anim.CurrentAnimationPosition * 60.0 >= frame;

    /// <summary>npcHappyIn(0): NPC_ACTION_HAPPY + the happy clip, kept until the next talk ends (npcTalkOut drops it and asks for the coin).</summary>
    private void HappyIn()
    {
        _happy = true;
        Play(Profile.ClipHappy); ResetToWait();
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: clean again -> {Profile.ClipHappy} (happy until the next talk)");
    }

    /// <summary>TNpcCoin::requestAppearCoin(getCursorPos(), mRotation.y, 40) -> execAppearCoin_: the coin scene at the cursor point, launched 15 u/tick
    /// at 75 deg up along the facing (setVelocityAndFlag10) when the 40-frame count runs out with no talk on.</summary>
    private void AppearCoin()
    {
        if (ThankYouCoin == null) return;
        var coin = ThankYouCoin.Instantiate<Node3D>();
        GetParent().AddChild(coin);
        Vector3 pos = GlobalPosition + Vector3.Up * ((Profile.BodyHeight + Profile.CursorHeight) / U);
        coin.GlobalPosition = pos;
        Vector3 v = (DirOf(YawRad) * Mathf.Cos(Mathf.DegToRad(75f)) + Vector3.Up * Mathf.Sin(Mathf.DegToRad(75f))) * SmsUnits.TickSpeedToMps(15f);
        if (coin is RigidBody3D rb) rb.LinearVelocity = v;
        if (DebugPrint) GD.Print($"[SMS npc] {Name}: the thank-you coin at {pos}, {v.Length():F1} m/s");
    }

    /// <summary>TLiveActor::bind for an NPC: the march along the facing (units/tick -> m/s), gravity when airborne, the floor otherwise.</summary>
    private void Move()
    {
        if (_sinking) { Velocity = Vector3.Zero; return; }                 // TNerveNPCSink / Recover: the nerve owns y, no bind, no floor
        Vector3 v = Velocity;
        if (_thrown && _thrownMoves > 0 && IsOnFloor()) { _thrown = false; v = Vector3.Zero; _thrownVel = Vector3.Zero; }   // bind: on the ground mVelocity.set(0,0,0)
        if (_thrown) _thrownMoves++;
        Vector3 f = _thrown ? _thrownVel : DirOf(YawRad) * SmsUnits.TickSpeedToMps(_marchSpeed);
        v.X = f.X; v.Z = f.Z;
        if (_marioHits && (_cur == Nerve.GraphWander || _cur == Nerve.UTurn) && _mario != null)
        {
            // execNpcObjCollision_ (NpcCollision.cpp 37): a WALKING NPC moves itself out of whatever it overlaps by the overlap depth
            // (mAttackRadius + the other's DamageRadius - the distance), a standing one does not -- Mario is kept off by his own keepDistance
            Vector3 d = GlobalPosition - _mario.GlobalPosition; d.Y = 0f;
            float dist = d.Length() * U;
            float depth = Mathf.Max(Profile.AttackRadius + SmsParams.Mario.DamageRadius - dist, 0.001f);
            Vector3 push = (dist > 1e-3f ? d / (dist / U) : Vector3.Right) * (depth / U);
            GlobalPosition += push;                                     // mPositionDelta += local_4C (units a tick)
        }
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
        if (_walkForbid > 0 || _talking || DirtyOrHappy) { _marchSpeed = 0f; _turnSpeed = 0f; return; }   // execWalk: !isClean / HAPPY stand still
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
        if (_walkForbid > 0 || _talking || DirtyOrHappy) { ExecWalk(false); if (_marchSpeed < 0.001f && _clip != WaitClip()) WaitIn(); return; } // mWalkForbidCount / !isClean / HAPPY: standing
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
        if (DirtyOrHappy) return;                                           // execUTurn: no turning while dirty / happy
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
        if (InBodyTurnRange() && IsClean) { Enter(Nerve.TurnToMario); return; }   // TNerveNPCWaitMarioApproach 112: isInBodyTurnSearchRange && isClean
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
        if (!InBodyTurnRange() || !IsClean) { Enter(Nerve.WaitMarioApproach); return; }   // 148: || mPollutionAmount != 0
        if (Profile.TurnsToMarioOnApproach && !Sitting && !DirtyOrHappy)   // isTurnToMarioWhenApproach 187: not while dirty / happy
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
    private bool IsNeedTurnToFirstState() => !DirtyOrHappy && !Sitting && Idle != IdleStyle.Angry && (_cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario);

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
        bool can = CanTalk && !CanBeTaken && Lines != null && Lines.Length > 0 && _talkForbid == 0 && !_talking && _mario != null && IsClean && !_sinking
            && (_cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.TurnToMario || _cur == Nerve.Wet || _cur == Nerve.Mad || _cur == Nerve.WaitMarioApproach);
        if (can)
        {
            Vector3 m = MarioU(), p = PosU();
            float degree = Sitting ? SmsParams.Npc.SitTalkAcceptDegree : SmsParams.Npc.TalkAcceptDegree;
            float dist = ThrowsMario ? SmsParams.Npc.ThrowTalkAcceptDist : SmsParams.Npc.TalkAcceptDist;        // mThrowCtrl: 200 / 150 (changeNerveProc_ 481)
            float height = ThrowsMario ? SmsParams.Npc.ThrowTalkAcceptHeight : SmsParams.Npc.TalkAcceptHeight;
            can = Mathf.Abs(m.Y - p.Y) < height
                && InSight(m, dist, height, degree)
                && MarioFacesUs(dist, SmsParams.Npc.MarioTalkAcceptDegree);
        }
        _talkable = can;                             // LIVE_FLAG_UNK20000; the director (Mario.SmsDirectorTalkCheck) reads it and B
    }

    /// <summary>MsIsInSight(marioPos, marioAngleY, npcPos, dist, MarioTalkAcceptDegree): Mario's facing cone holds the NPC.</summary>
    private bool MarioFacesUs(float dist, float degree)
    {
        Vector3 d = GlobalPosition - _mario.GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() * U * U >= dist * dist) return false;
        if (d.LengthSquared() < 1e-8f) return true;
        Vector3 f = _mario.SmsFacingDir();
        return f.Dot(d.Normalized()) >= Mathf.Cos(Mathf.DegToRad(degree * 0.5f));
    }

    /// <summary>TMarDirector::findNearestTalkNPC (MarDirectorEvent.cpp 34): the talkable NPC nearest Mario in full 3D, never a liftable one
    /// (LIVE_FLAG_UNK100000); the caller supplies MARIO_STATUS_WAIT.</summary>
    public static SmsNpc FindNearestTalk(Mario mario)
    {
        if (mario == null) return null;
        SmsNpc best = null; float bestD = float.MaxValue;
        foreach (var n in All)
        {
            if (n == null || !IsInstanceValid(n) || !n._talkable || n.CanBeTaken || n._talking) continue;
            float d = n.GlobalPosition.DistanceSquaredTo(mario.GlobalPosition);
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

    /// <summary>readBillboard reached its facing (gpMarDirector->unk126 = 2 -> TTalk2D2::openTalkWindow): the balloon opens on Lines -- the
    /// talk camera first, the bands once it has landed.</summary>
    public void MarioFacesYou()
    {
        if (!_talking || _windowOpen) return;
        _windowOpen = true;
        var balloon = SmsTalkBalloon.Ensure(this);
        if (balloon == null) { EndTalk(); return; }
        balloon.Open(this, _mario, Lines);
    }

    /// <summary>The talk ended from this side (hurt, taken, sprayed): the game's forceCloseTalk when the balloon is up -- the camera and the HUD
    /// back, every NPC's npcTalkOut, the fade, then Mario freed; before it opened, the talk is simply dropped and Mario freed now.</summary>
    private void EndTalk()
    {
        if (!_talking) return;
        var balloon = SmsTalkBalloon.Instance;
        if (_windowOpen && balloon != null && balloon.IsOpenOn(this)) { balloon.ForceClose(); return; }   // -> TalkWindowClosing on every NPC, SmsTalkEnd at the fade
        TalkWindowClosing();
        if (_mario != null) _mario.SmsTalkEnd();
    }

    /// <summary>TBaseNPC::npcTalkOut (NpcAnm.cpp 509), on EVERY NPC from makeMtxForPrevTalk (the balloon's close / forced close): talk forbidden
    /// 60 ticks for all; the one in its talk (LIVE_FLAG_UNK80000) also stops walking 120 ticks and changeNerveFromTalk_ resumes what the
    /// talk interrupted. Mario is freed later, when the balloon's fade is done (the director's unk126 = 0).</summary>
    public void TalkWindowClosing()
    {
        _talkForbid = SmsParams.Npc.TalkForbidTicksAfterTalk;
        if (!_talking) return;
        _talking = false; _windowOpen = false;
        _walkForbid = SmsParams.Npc.WalkForbidTicksAfterTalk;
        if (_happy)
        {
            _happy = false;                                                 // offActionFlag(NPC_ACTION_HAPPY)
            if (ThankYouCoin != null) { _coinTimer = SmsParams.Npc.CoinAppearFrames * SmsUnits.TicksPerFrame; _talkForbid = _walkForbid = SmsParams.Npc.CoinTalkForbidTicks; }
        }
        if (_cur == Nerve.Talk)
        {
            if (ThrowsMario && _mario != null) Enter(Nerve.Throw);          // changeNerveFromTalk_ 194: mThrowCtrl -> setNext(TNerveNPCThrow); the stack waits
            else Pop();
        }
    }

    /// <summary>makeMtxForPrevTalk's loop over gpMarDirector->unk88: npcTalkOut on every NPC of the stage.</summary>
    public static void NpcTalkOutAll()
    {
        foreach (var n in All)
            if (n != null && IsInstanceValid(n)) n.TalkWindowClosing();
    }

    /// <summary>The talk nerve's tail when the talk flag dropped without npcTalkOut (an NPC that lost its Mario): forbid, walk hold, back.</summary>
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

    /// <summary>HIT_FILTER_NO_COLLISION in Godot terms: the capsule leaves its layer (nothing is pushed by it, his hit checks skip it: Tick);
    /// the mask stays so it still stands on the map. His body is excepted from the capsule at all times (ExceptMario).</summary>
    private void SetCollisionOff(bool off)
    {
        if (off == _collisionOff) return;
        _collisionOff = off;
        if (off) { _layerSaved = CollisionLayer; CollisionLayer = 0; }
        else CollisionLayer = _layerSaved;
    }

    // ------------------------------------------------------------------ water
    public void SmsSprayedByWater(Vector3 hitPos) { if (!_collisionOff) Sprayed(); }   // HIT_FILTER_NO_COLLISION: no hit pairs while held / just released

    /// <summary>behaveToHitObject_(WATER_SPRAY): isNerveCanGoToWet (not already wet, not in a talk's first 4 ticks) -> pushNerve(Wet).</summary>
    private void Sprayed()
    {
        // behaveToHitObject_(WATER_SPRAY) 368: a dirty NPC in its Wet nerve loses PollutionCleanSpeed a hit; at 0 the talk forbid and the thanks
        if (Profile.SinksInPollution && !_talking && _cur == Nerve.Wet && _pollution > 0f && !_sinking)
        {
            _pollution -= Profile.PollutionCleanSpeed;
            _polDirty = true;
            if (_pollution <= 0f)
            {
                _pollution = 0f;
                _talkForbid = SmsParams.Npc.TalkForbidTicksAfterTalk;
                _thanks = true;
                if (DebugPrint) GD.Print($"[SMS npc] {Name}: washed clean");
            }
        }
        if (!Profile.ReactsToWater) return;
        if (_sinking) return;                                               // isNerveCanGoToWet: not from Sink / Recover
        bool can = _cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario || (_cur == Nerve.Talk && _nerveTime >= 4);
        if (!can) return;
        if (_cur == Nerve.Talk)
        {
            EndTalk();   // forceCloseTalk when the balloon is up: the camera back, every NPC's npcTalkOut (this one's Pop), Mario freed at the fade
            _talkForbid = SmsParams.Npc.TalkForbidTicksAfterTalk;
            Enter(Nerve.Wet);
            return;
        }
        Push(Nerve.Wet);
    }

    private void WetIn()
    {
        // npcWetIn: the wet clip, half the time the alternate (a sitting NPC: the sit-wet); a dirty NPC washes instead (NPC_ANM_KIND_WASH)
        string clip = _pollution > 0f ? Profile.ClipWash : (Sitting ? Profile.ClipSitWet : (_rng.NextDouble() < 0.5 ? Profile.ClipWetAlt : Profile.ClipWet));
        Play(clip, blend: _pollution > 0f || Profile.Kind == SmsNpcProfile.NpcKind.Toad); // a Pianta's wet is a cut (NPC_STOP_MOTION_BLEND_OFF); the wash blends
        _marchSpeed = 0f; _turnSpeed = Profile.WaitTurnSpeed;
        if (WetVoice != null && WetVoice.Stream != null) WetVoice.Play();
    }

    private void TickWet()
    {
        _marchSpeed = 0f;
        // npcWetting: washed clean this wet (UNK1DA_FLAG_UNK2) -> happy and out; the WASH clip ends clean -> happy and out, still dirty -> out
        if (_thanks) { _thanks = false; HappyIn(); Pop(); return; }
        if (_clip == Profile.ClipWash)
        {
            if (_pollution <= 0f) { HappyIn(); Pop(); return; }
            if (ClipEnded()) Pop();
            return;
        }
        if (!ClipEnded()) return;
        if (Profile.GetsMad && InMadRange() && !DirtyOrHappy) { Enter(Nerve.Mad); return; }   // the wet Pianta: isStateGoToMad_ -> Mad (isClean, no HAPPY)
        Pop();                                                                 // npcWetOut
    }

    // ------------------------------------------------------------------ trample + mad
    private bool _marioHits;                             // this tick's TObjHitCheck pair with him (execNpcObjCollision_ reads it)

    /// <summary>setVariableDamageRadius_ (NpcCollision.cpp 101): a trampled NPC's damage radius is DamageRadiusSmall (10) while Mario is
    /// airborne (not isTouchGround4cm) above our feet within 3 x the radius -- he lands INTO us rather than being kept off the rim.</summary>
    private float DamageRadiusNow(Vector3 m, Vector3 g)
    {
        float r = Profile.DamageRadius;
        if (Profile.Trampled && !_mario.SmsTouchGround4cm && m.Y > g.Y)
        {
            float dx = m.X - g.X, dz = m.Z - g.Z;
            if (dx * dx + dz * dz < 9f * r * r) r = SmsParams.Npc.DamageRadiusSmall;
        }
        return r;
    }

    /// <summary>TMario::hitNpc (MarioCheckCol.cpp 95) from our side, on TObjHitCheck's pair: his attack cylinder (setNormalAttackArea /
    /// setJumpingAttackArea) against our damage cylinder. A jumping status, not the hip drop, falling (mVel.y < 0), his feet above ours
    /// and isBeTrampledNpc -> trampleExec (not a dive): MARIO_STATUS_TRAMPLE for him, behaveToBeTrampled_ for us. Else keepDistance(us, 0):
    /// he is pushed out to our radius + 50. The hose at close range = SPRAYED_BY_WATER (the drops' ISmsSprayTarget hits also land here).</summary>
    private void HitCheckMario()
    {
        _marioHits = false;
        if (_mario == null || _holder != null) return;
        Vector3 g = PosU(), m = MarioU();
        _damageRadiusNow = DamageRadiusNow(m, g);
        bool onGround = _mario.IsOnFloor();
        bool rising = !onGround && _mario.Velocity.Y > 0f;
        float ar = onGround ? SmsParams.Mario.HoldRadius : (rising ? SmsParams.Mario.PushupRadius : SmsParams.Mario.TrampleRadius);
        float ah = onGround ? SmsParams.Mario.AttackHeight : (rising ? SmsParams.Mario.PushupHeight : SmsParams.Mario.AttackHeight);
        if (!CheckDistance(m, ar, ah, g, _damageRadiusNow, Profile.DamageHeight)) return;
        _marioHits = true;
        if (Profile.Trampled && _mario.SmsHitNpcTrample(g)) { Trampled(); return; }
        _mario.SmsKeepDistance(g, _damageRadiusNow);
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

    private bool CanGoToMad() => !DirtyOrHappy && (_cur == Nerve.GraphWander || _cur == Nerve.UTurn || _cur == Nerve.GraphWait || _cur == Nerve.WaitMarioApproach || _cur == Nerve.TurnToMario || _cur == Nerve.Wet);
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
