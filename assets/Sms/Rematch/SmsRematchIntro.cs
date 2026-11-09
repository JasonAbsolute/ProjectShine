using Godot;
using System;

/// <summary>
/// SMS pass 157: the REMATCH arena's INTRO (KINGBOO_REMATCH_gdoc.txt, INTRO 3-6). The scene starts black (the fader set black); Mario is held
/// (SuppressForIntro) and King Boo hidden; the two-line card shows over the black (SmsRematchCard: Line1, then Line2 under it, Delfino,
/// the timings below); then Mario is revealed FallHeight over the floor at the MarioSpawn marker (he falls) and the screen fades in on him
/// falling; BooSpawnDelay after he lands (BooSpawnTimeout after the reveal at the latest) King Boo spawns at BooHeight over the floor at the
/// BooSpawn marker and laughs; the fight may start (FightReady). A (the jump button) skips the card. No FLUDD here: the Level's HasFludd is
/// off. After a miss (GameData.CameFromMiss) the card is skipped: he falls straight in and King Boo comes.
/// Knobs: the lines and their timing, the heights, the fade, SkipAllowed, the card's font (LabelSettings). PlayWithoutGameData: the intro
/// runs when the scene is opened on its own too (F6), as it does when the casino sends him here.
/// Pass 174 (Jaysonn: "mario lands then a cutscene starts where mario looks around looking from where the laughing is coming from and the king boo
/// appears just like the casino fight laughs and then goes underground and the fight starts"): with Cutscene on, CutsceneDelay after Mario lands
/// his controls lock (CameraLocked) and a cutscene camera takes over -- a LAUGH from nowhere (King Boo's long laugh), Mario LOOKS ABOUT for it
/// (176, Jaysonn: "a new animation ... more like a 'where am i' look around": the LookClip, a real clip of his -- the custom ma_lookabout, baked from
/// assets/Mario/Anims/ma_lookabout.pose.json by tools/anim/bake_anim.gd and edited like any clip in the Animation panel -- played outside the
/// AnimationTree (Mario.SmsPlayDemoClip) for its length (LookSeconds 0 = the clip's), his body standing; no clip = 175's head look-about), then
/// King Boo SURFACES: SurfaceDistance in front of him toward the BooSpawn marker, rising out of the floor as the casino boss rises from his table
/// (the appear clip and voice), LAUGHING for IntroLaughSeconds (his knob) -- Mario in his BATTLE STANCE (176: StanceInClip then the StanceClip loop,
/// the custom ma_battle_stance_in / ma_battle_stance), his head on King Boo, his body turned to him -- with the camera off to the side on the two of
/// them (SurfaceCamSide / Height), then SINKING back under the floor; AfterSinkSeconds on the empty spot, the game camera is back,
/// Mario is free (his head back to the clip; 176b: he HOLDS the stance until his first input, StanceHoldUntilInput), and King Boo rises at his BooSpawn spot for the fight (SpawnRising: no second laugh) -- FightReady as
/// before. A skips the cutscene too (the fight starts at once). After a miss the cutscene is skipped like the card. Cutscene off = the 157 intro.
/// </summary>
public partial class SmsRematchIntro : Node
{
    [ExportGroup("Who and where")]
    [Export] public NodePath MarioPath = "../Mario";
    [Export] public NodePath MarioSpawnPath = "../MarioSpawn";
    [Export] public NodePath BooPath = "../KingBoo";
    [Export] public NodePath BooSpawnPath = "../BooSpawn";
    [Export] public NodePath FloorPath = "../Arena";
    /// <summary>Mario is revealed this high over the floor under his marker (falls in); King Boo hovers this high over the floor under his (159: 4 --
    /// his body's hit cylinder reaches down to 1.5 m, where a Bomb Boo swung at Mario's arm height, about 2.3 m, reaches it).</summary>
    [Export] public float FallHeight = 22f, BooHeight = 4f;
    [ExportGroup("The card")]
    [Export] public string Line1 = "You call that a fight fit for a king?";
    [Export] public string Line2 = "I'll show you a real fight...";
    /// <summary>Black before the first line; each line's fade in; the first line held alone; the gap before the second comes; the second held (both up); both lines' fade out; black after.</summary>
    [Export] public float BlackBefore = 0.6f, LineFadeIn = 0.45f, Line1Hold = 1.9f, LineGap = 0.3f, Line2Hold = 2.3f, LinesFadeOut = 0.5f, BlackAfter = 0.35f;
    /// <summary>The card's text style (empty = Delfino at 46 px, white, a black shadow).</summary>
    [Export] public LabelSettings LabelSettings;
    [Export] public int FontSize = 46;
    /// <summary>A (the jump button) skips the card.</summary>
    [Export] public bool SkipAllowed = true;
    [ExportGroup("The fall and the spawn")]
    /// <summary>The fade in once he is revealed falling.</summary>
    [Export] public float FadeInSeconds = 0.7f;
    /// <summary>King Boo comes this long after Mario lands -- or this long after the reveal if he never lands (a fall off the edge).</summary>
    [Export] public float BooSpawnDelay = 0.5f, BooSpawnTimeout = 5f;
    [Export] public bool PlayWithoutGameData = true;
    [ExportGroup("The cutscene (174)")]
    /// <summary>The cutscene runs at all; the beat after Mario lands before it starts; how long he looks about before King Boo surfaces (the laugh
    /// comes at its start); where King Boo surfaces (metres in front of Mario toward the BooSpawn marker; 0 = at the marker); the beat on the
    /// empty spot after he sinks before the fight starts; the cutscene camera's field of view.</summary>
    [Export] public bool Cutscene = true;
    [Export] public float CutsceneDelay = 0.4f, LookSeconds = 0f, SurfaceDistance = 12f, AfterSinkSeconds = 0.3f, CutsceneFov = 50f;   // 176: LookSeconds 0 = the clip's length; 175: 12 m (7 stood them too close); 197: AfterSinkSeconds 0.6 -> 0.3 (room for the music's cue)
    /// <summary>197 (Jaysonn: "the music starts right when Mario looks around, and the fight needs to start within 7.5 s of the song starting to cue the proper music
    /// timing"): the FIGHT STARTS (the cutscene over, Mario free, King Boo's rise for the fight) this long after the music's start on the look-about's first frame. The
    /// cutscene's fixed beats -- the rise tell, the appear clip's rise, the dive clip, AfterSinkSeconds -- are what they are; the look-about and King Boo's laugh are
    /// TRIMMED in proportion to fit the rest (the look-about clip played quicker, the laugh shorter: IntroLaughOverride), never under LookMinScale of themselves; when
    /// there is room to spare the empty-spot pause after the dive takes it (up to CueSlackMax) so the fight lands ON the cue. 0 = no cue: the beats as set.</summary>
    [Export(PropertyHint.Range, "0,20,0.1")] public float FightCueSeconds = 7.5f;
    [Export(PropertyHint.Range, "0.3,1,0.05")] public float LookMinScale = 0.5f;
    [Export(PropertyHint.Range, "0,4,0.1")] public float CueSlackMax = 1.5f;
    /// <summary>197, for the drives: the cutscene's clock when the music was cued (the look-about's first frame), when the fight started (the cutscene's end), the planned
    /// fight time from the cue, the trim applied to the look-about and the laugh (1 = none), the look-about's natural and played lengths.</summary>
    public float MusicCueAt { get; private set; } = -1f;
    public float FightStartAt { get; private set; } = -1f;
    public float PlannedFightAt { get; private set; } = -1f;
    public float CueTrim { get; private set; } = 1f;
    public float LookNaturalSeconds { get; private set; }
    public float LookPlayedSeconds => _lookLen;
    private float _afterSink, _sinceCue;
    /// <summary>The surface shot's camera: how far off the line between Mario and King Boo (metres, a side two-shot) and how high.</summary>
    [Export] public float SurfaceCamSide = 22f, SurfaceCamHeight = 4.6f;
    /// <summary>Where the surface shot's camera looks: 0 = at Mario's head, 1 = at King Boo's body, between for both (the crown needs room above).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float SurfaceCamAim = 0.7f;
    /// <summary>176, the look-about: the clip Mario plays while he looks about (his own AnimationPlayer's; the custom ma_lookabout -- edit it in the
    /// Animation panel, or re-bake it from assets/Mario/Anims/ma_lookabout.pose.json), at LookClipSpeed; "" or a missing clip = 175's head look-about
    /// (a modifier turning his head 60 degrees each way). LookTurnDegPerSec: how quickly his BODY turns to King Boo once he has surfaced.</summary>
    [Export] public string LookClip = "ma_lookabout";
    [Export] public float LookClipSpeed = 1f, LookTurnDegPerSec = 240f;
    /// <summary>176 (Jaysonn: "when it cuts to the side view of the stand off i want mario to do this battle stance"): on the cut to the two-shot
    /// Mario DROPS INTO a battle stance (StanceInClip, one-shot) and HOLDS it (StanceClip, a loop) until the fight starts -- the custom clips
    /// ma_battle_stance_in / ma_battle_stance (assets/Mario/Anims). "" or a missing clip = his idle.</summary>
    [Export] public string StanceInClip = "ma_battle_stance_in", StanceClip = "ma_battle_stance";
    /// <summary>176b (Jaysonn: "have him hold the fight pose until input from the player happens then he goes back to normal"): when the fight starts
    /// Mario keeps the stance until the player's first input (the stick or a button; a hit ends it too); off = the idle as soon as the fight starts.</summary>
    [Export] public bool StanceHoldUntilInput = true;
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void CardShownEventHandler();
    [Signal] public delegate void RevealedEventHandler();
    [Signal] public delegate void FightReadyEventHandler();

    public enum Phase { Waiting, Card, Falling, Spawned, Done, LookAbout, Surfaced, Sunk }
    public Phase State { get; private set; } = Phase.Waiting;
    public bool Skipped { get; private set; }
    public bool CameFromMiss { get; private set; }
    public float? FloorY { get; private set; }
    public SmsRematchCard Card { get; private set; }

    private Mario _mario; private Node3D _marioSpawn, _booSpawn; private SmsKingBooRematch _boo; private SmsArenaFloor _floor;
    private float _t, _sinceReveal, _landedAt = -1f; private bool _booSpawned;
    // 174: the cutscene
    private Camera3D _cam, _gameCam; private float _cut, _sunkAt = -1f, _faceYaw, _lookLen; private Vector3 _surfaceSpot, _camFrom, _camAt, _camTo; private bool _cutsceneRan, _cutsceneSkipped, _lookClipUsed, _stanceUsed; private int _looks;
    /// <summary>176, for the drives: the look-about ran on the LookClip (else the head modifier), how long it runs; the stance clips played on the cut.</summary>
    public bool LookClipUsed => _lookClipUsed;
    public float LookLength => _lookLen;
    public bool StanceUsed => _stanceUsed;
    /// <summary>174, for the drives: the cutscene ran, was skipped; where King Boo surfaced; Mario's facing yaw now (radians) and the number of look turns made; the cutscene camera (while it runs).</summary>
    public bool CutsceneRan => _cutsceneRan;
    public bool CutsceneSkipped => _cutsceneSkipped;
    public Vector3 SurfaceSpot => _surfaceSpot;
    public float FaceYawNow => _faceYaw;
    public int LooksMade => _looks;
    public Camera3D CutsceneCamera => _cam;

    public override void _Ready()
    {
        _mario = GetNodeOrNull<Mario>(MarioPath); _marioSpawn = GetNodeOrNull<Node3D>(MarioSpawnPath);
        _boo = GetNodeOrNull<SmsKingBooRematch>(BooPath); _booSpawn = GetNodeOrNull<Node3D>(BooSpawnPath);
        _floor = GetNodeOrNull<SmsArenaFloor>(FloorPath);
        if (Engine.IsEditorHint()) return;
        if (_mario == null) { GD.PushError($"[SMS rematch intro] {GetPath()}: MarioPath {MarioPath} is not a Mario"); return; }
        CameFromMiss = GameData.Instance != null && GameData.Instance.CameFromMiss;   // read before the Level clears it (a child's _Ready runs first)
        // held and dark from the first frame: his own _Ready has run (he is before this node in the scene), the fader is his
        _mario.SuppressForIntro();
        if (SmsFader.Current != null) SmsFader.Current.SetBlack();
        SmsRematchIntroDriveHook();
        CallDeferred(nameof(Start));
    }
    partial void SmsRematchIntroDriveHook();

    private void Start()
    {
        if (SmsFader.Current != null) SmsFader.Current.SetBlack();   // the Level's own miss / return fades, if any, overruled: the card runs over black
        _mario.SuppressForIntro();                                    // again, after every _Ready has run
        if (CameFromMiss)
        {
            if (DebugPrint) GD.Print("[SMS rematch intro] after a miss: no card, the fall at once");
            Reveal();
            return;
        }
        Card = new SmsRematchCard { Name = "RematchCard", Line1Text = Line1, Line2Text = Line2, Settings = LabelSettings, FontSize = FontSize };
        Card.BlackBefore = BlackBefore; Card.LineFadeIn = LineFadeIn; Card.Line1Hold = Line1Hold; Card.LineGap = LineGap; Card.Line2Hold = Line2Hold; Card.LinesFadeOut = LinesFadeOut; Card.BlackAfter = BlackAfter;
        Card.Finished += Reveal;
        AddChild(Card);
        State = Phase.Card; _t = 0f;
        if (DebugPrint) GD.Print($"[SMS rematch intro] the card: \"{Line1}\" / \"{Line2}\" ({Card.TotalSeconds:F1} s)");
        EmitSignal(SignalName.CardShown);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (State == Phase.Card && SkipAllowed && Card != null && !Skipped && (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space")))
        {
            Skipped = true; Card.Skip();
            if (DebugPrint) GD.Print("[SMS rematch intro] the card skipped");
        }
        if (State == Phase.Falling || State == Phase.Spawned)
        {
            _sinceReveal += dt;
            if (_landedAt < 0f && _mario != null && IsInstanceValid(_mario) && _mario.IsOnFloor()) _landedAt = _sinceReveal;
            bool cutscene = Cutscene && !CameFromMiss && _boo != null && !_cutsceneRan;
            if (cutscene && State == Phase.Falling && _landedAt >= 0f && _sinceReveal - _landedAt >= CutsceneDelay) { BeginCutscene(); return; }
            if (cutscene && State == Phase.Falling && _sinceReveal >= BooSpawnTimeout) { SpawnBoo(); return; }   // he never landed (off the edge): no cutscene -- the 157 spawn, with the laugh
            if (!cutscene && !_booSpawned && ((_landedAt >= 0f && _sinceReveal - _landedAt >= BooSpawnDelay) || _sinceReveal >= BooSpawnTimeout)) SpawnBoo();
        }
        if (State == Phase.LookAbout || State == Phase.Surfaced || State == Phase.Sunk) CutsceneTick(dt);
    }

    // ---- 174: the cutscene ---------------------------------------------------------------------------------------------------------------------
    private static Vector3 Flat(Vector3 v) { v.Y = 0f; return v.LengthSquared() > 1e-6f ? v.Normalized() : Vector3.Forward; }
    private static float Yaw(Vector3 dir) => MathF.Atan2(dir.X, dir.Z);
    private static Vector3 Dir(float yaw) => new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
    /// <summary>Mario has landed: his controls lock, the cutscene camera takes over, the laugh sounds from nowhere, he looks about for it.</summary>
    private void BeginCutscene()
    {
        _cutsceneRan = true; State = Phase.LookAbout; _cut = 0f; _looks = 0;
        _mario.CameraLocked = true;
        Vector3 mp = _mario.GlobalPosition, landed = Flat(_mario.SmsFacingDir());
        // where he will surface: toward the BooSpawn marker, SurfaceDistance out (or at the marker), over the floor there
        Vector3 toBoo = _booSpawn != null ? Flat(_booSpawn.GlobalPosition - mp) : landed;
        Vector3 face = toBoo;                                                                                // the look-about is about the way the laugh comes from
        _faceYaw = Yaw(face);
        _mario.SmsDemoFace(face);                                                                            // squared up to it from the first frame (his landing facing is the marker's)
        Vector3 spot = SurfaceDistance > 0f ? mp + toBoo * SurfaceDistance : (_booSpawn != null ? _booSpawn.GlobalPosition : mp + face * 7f);
        float? fy = _floor?.FloorYUnder(spot.X, spot.Z, spot.Y + 100f, 400f, Exclude()) ?? FloorY;
        if (!fy.HasValue && _booSpawn != null) { spot = _booSpawn.GlobalPosition; fy = _floor?.FloorYUnder(spot.X, spot.Z, spot.Y + 100f, 400f, Exclude()) ?? FloorY; }
        _surfaceSpot = new Vector3(spot.X, (fy ?? mp.Y) + BooHeight, spot.Z);
        // the camera: in front of him, a little to one side, low, on his face as he looks about (nearly head-on: his face shows through both turns)
        _gameCam = GetViewport().GetCamera3D();
        _cam = new Camera3D { Name = "IntroCutsceneCamera", Fov = CutsceneFov };
        (GetTree().CurrentScene ?? GetParent()).AddChild(_cam);
        Vector3 side = new Vector3(-face.Z, 0f, face.X);                                                  // in front of the way he landed facing: his turns are about it
        _camFrom = mp + face * 3.6f + side * 0.4f + Vector3.Up * 1.45f; _camAt = mp + Vector3.Up * 1.15f;
        _camTo = mp + face * 4.1f + side * 0.5f + Vector3.Up * 1.55f;
        PlaceCam(0f); _cam.Current = true;
        MusicCueAt = 0f; _sinceCue = 0f; _boo.MusicCue(SmsKingBooRematch.BossMusicCues.LookAbout, "the look-about's first frame");   // 197: the music's cue; its own clock runs from here
        _boo.PlayVoice(_boo.LaughSe, _boo.LaughDb);                                                        // the laugh, from nowhere
        // 176: the look-about is a CLIP of his (the custom ma_lookabout: a glance left, right, then up with a shrug), played outside the tree for its
        // length, his body standing where he landed; no such clip = 175's head look-about (a modifier turning his head: a beat, left, right, ahead)
        float clipLenRaw = _mario.SmsClipLength(LookClip);
        float clipLen = clipLenRaw / Mathf.Max(0.05f, LookClipSpeed);
        float lookNatural = LookSeconds > 0f ? LookSeconds : clipLenRaw > 0f ? clipLen : 2.6f;
        LookNaturalSeconds = lookNatural;
        // 197: the music's cue -- the fixed beats after the look-about (King Boo's rise tell, rise and dive; the pause after) against FightCueSeconds; the look-about and
        // the laugh share what is left, trimmed in proportion when it is less than they want, the pause taking the slack (up to CueSlackMax) when it is more
        float laughNatural = _boo.IntroLaughSeconds, trim = 1f; _afterSink = AfterSinkSeconds; _boo.IntroLaughOverride = float.NaN;
        if (FightCueSeconds > 0f)
        {
            float fixedT = _boo.IntroFixedSeconds + AfterSinkSeconds, left = FightCueSeconds - fixedT, want = lookNatural + laughNatural;
            if (left < want) trim = Mathf.Clamp(left / Mathf.Max(0.1f, want), LookMinScale, 1f);
            else _afterSink = AfterSinkSeconds + Mathf.Min(left - want, CueSlackMax);
            _boo.IntroLaughOverride = laughNatural * trim;
            PlannedFightAt = fixedT - AfterSinkSeconds + _afterSink + want * trim;
        }
        else PlannedFightAt = -1f;
        CueTrim = trim;
        float lookSpeed = LookClipSpeed / trim;
        _lookClipUsed = clipLenRaw > 0f && _mario.SmsPlayDemoClip(LookClip, lookSpeed);
        _lookLen = lookNatural * trim;
        var look = _mario.SmsHeadLook;
        if (!_lookClipUsed && look != null && look.HasBones)
        {
            float L = Mathf.Max(0.5f, _lookLen), T = Mathf.DegToRad(60f), U = -Mathf.DegToRad(10f);   // negative pitch = up
            look.ChestShare = 0.3f;
            look.Play(new[] {
                new SmsHeadLook.Look(0f, 0f, 0f, 0.08f * L),              // the beat: the laugh registers
                new SmsHeadLook.Look(T, U, 0.12f * L, 0.18f * L),         // left, up a little
                new SmsHeadLook.Look(-T, U, 0.16f * L, 0.18f * L),        // right
                new SmsHeadLook.Look(0f, 0.5f * U, 0.12f * L, 0.16f * L), // ahead: where he will come from
            });
        }
        if (DebugPrint) GD.Print($"[SMS rematch intro] CUTSCENE: Mario landed at {mp} facing {landed} (turned to {face}), the laugh; he looks about for {_lookLen:F2} s ({(_lookClipUsed ? $"the clip {LookClip} at x{LookClipSpeed / trim:F2}" : look != null && look.HasBones ? $"no clip {LookClip}: the head modifier" : "NO clip and no head modifier")}); King Boo will surface at {_surfaceSpot} ({(spot - mp).Length():F1} m away); the camera at {_camFrom}; the music's cue: the fight at {PlannedFightAt:F2} s of {FightCueSeconds} (fixed beats {_boo.IntroFixedSeconds:F2} + the pause {_afterSink:F2}; the look-about {lookNatural:F2} and the laugh {laughNatural:F2} trimmed x{trim:F2})");
    }
    private void PlaceCam(float k)
    {
        if (_cam == null) return;
        _cam.GlobalPosition = _camFrom.Lerp(_camTo, k);
        _cam.LookAt(_camAt, Vector3.Up);
        if (!_cam.Current) _cam.Current = true;
    }
    private void CutsceneTick(float dt)
    {
        _cut += dt; if (MusicCueAt >= 0f) _sinceCue += dt;                                                   // 197: the cue's clock (the cut's restarts at the surface)
        if (SkipAllowed && !_cutsceneSkipped && (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))) { SkipCutscene(); return; }
        if (State == Phase.LookAbout)
        {
            // 176: the look about is the clip's (or 175's head modifier's, started in BeginCutscene); the body stands square to where the laugh comes from
            var look = _mario.SmsHeadLook;
            _looks = !_lookClipUsed && look != null ? Mathf.Max(0, look.Step - 1) : 0;                   // the head modifier's looks made (the beat is step 0)
            TurnMario(Yaw(Flat(_surfaceSpot - _mario.GlobalPosition)), dt);
            PlaceCam(Mathf.Clamp(_cut / Mathf.Max(0.1f, _lookLen), 0f, 1f));
            if (_cut >= _lookLen) Surface();
        }
        else if (State == Phase.Surfaced)
        {
            TurnMario(Yaw(Flat(_boo.GlobalPosition - _mario.GlobalPosition)), dt);
            _mario.SmsHeadLook?.Track(_boo.BodyPoint, Dir(_faceYaw));                                      // 175: his head on King Boo as he rises and laughs
            float total = _boo.IntroRiseSeconds + _boo.IntroLaughSeconds + _boo.IntroSinkSeconds;
            // the side two-shot's aim: between Mario's head and King Boo's body, following the body (eased)
            Vector3 body = _boo.BodyPoint; body.Y = Mathf.Max(body.Y, _boo.IntroFloorY + 1.5f);   // not under the floor while he is
            Vector3 want = (_mario.GlobalPosition + Vector3.Up * 1.2f).Lerp(body, SurfaceCamAim);
            _camAt = _camAt.Lerp(want, 1f - MathF.Exp(-5f * dt));
            PlaceCam(Mathf.Clamp(_cut / Mathf.Max(0.1f, total), 0f, 1f));
            if (_sunkAt >= 0f && _cut - _sunkAt >= _afterSink) EndCutscene();                                 // 197: the pause takes the cue's slack
        }
    }
    private void TurnMario(float wantYaw, float dt)
    {
        float step = Mathf.DegToRad(LookTurnDegPerSec) * dt;
        float d = Mathf.Wrap(wantYaw - _faceYaw, -MathF.PI, MathF.PI);
        _faceYaw += Mathf.Clamp(d, -step, step);
        _mario.SmsDemoFace(Dir(_faceYaw));
    }
    /// <summary>The look about is over: King Boo surfaces at the spot, the camera off to the side on the two of them, pushing in a little through the laugh.</summary>
    private void Surface()
    {
        State = Phase.Surfaced; _cut = 0f; _sunkAt = -1f;
        // 176: the look-about clip is over; under the camera cut he drops into the BATTLE STANCE and holds it (the drop-in, then the loop) -- or the tree's idle
        _mario.SmsEndDemoClip();
        _stanceUsed = _mario.SmsPlayDemoClip(StanceInClip, 1f, StanceClip) || _mario.SmsPlayDemoClip(StanceClip);
        if (_mario.SmsHeadLook != null) _mario.SmsHeadLook.ChestShare = _stanceUsed ? 0f : 0.3f;        // 176b: the stance owns the chest; the head alone finds King Boo
        Vector3 mp = _mario.GlobalPosition, toSpot = Flat(_surfaceSpot - mp), side = new Vector3(-toSpot.Z, 0f, toSpot.X);
        // a side two-shot (he is far too big to frame from behind Mario 7 m away): from SurfaceCamSide metres off the line between them, a little behind
        // Mario, SurfaceCamHeight up, looking between Mario's head and King Boo's body; pushing in a little through the laugh
        Vector3 mid = (mp + _surfaceSpot) * 0.5f; mid.Y = mp.Y;
        _camFrom = mid + side * SurfaceCamSide + toSpot * -2f + Vector3.Up * SurfaceCamHeight; _camAt = (mp + Vector3.Up * 1.2f).Lerp(new Vector3(_surfaceSpot.X, _surfaceSpot.Y - BooHeight + 2f, _surfaceSpot.Z), 0.5f);
        _camTo = mid + side * (SurfaceCamSide * 0.85f) + toSpot * -1.5f + Vector3.Up * (SurfaceCamHeight + 0.6f);
        PlaceCam(0f);
        _boo.IntroSunk += OnIntroSunk;
        _boo.IntroSurface(_surfaceSpot, Yaw(Flat(mp - _surfaceSpot)));
        if (DebugPrint) GD.Print($"[SMS rematch intro] CUTSCENE: King Boo surfaces at {_surfaceSpot}, facing Mario; Mario {(_stanceUsed ? $"drops into the stance ({StanceInClip} -> {StanceClip})" : "stands (no stance clip)")}");
    }
    private void OnIntroSunk() { _sunkAt = _cut; if (_boo != null) _boo.IntroSunk -= OnIntroSunk; if (DebugPrint) GD.Print($"[SMS rematch intro] CUTSCENE: he has sunk; the fight in {AfterSinkSeconds:F1} s"); }
    /// <summary>A skips the cutscene: King Boo under the floor at once, the fight starts.</summary>
    private void SkipCutscene()
    {
        _cutsceneSkipped = true;
        if (_boo != null) { _boo.IntroSunk -= OnIntroSunk; _boo.IntroAbort(); }
        if (DebugPrint) GD.Print("[SMS rematch intro] the cutscene skipped");
        EndCutscene();
    }
    /// <summary>The cutscene is over: the game camera back, Mario free, King Boo rising at his spot for the fight.</summary>
    private void EndCutscene()
    {
        State = Phase.Sunk; FightStartAt = _sinceCue;                                                                                          // 197: on the cue's own clock (_cut restarts at the surface)
        if (DebugPrint && MusicCueAt >= 0f) GD.Print($"[SMS rematch intro] CUTSCENE over -- the fight starts {_sinceCue:F2} s after the music's cue (planned {PlannedFightAt:F2}, FightCueSeconds {FightCueSeconds}; the look-about and the laugh trimmed x{CueTrim:F2}; the pause after the dive {_afterSink:F2} s)");
        if (_gameCam != null && IsInstanceValid(_gameCam)) _gameCam.Current = true;
        if (_cam != null && IsInstanceValid(_cam)) { _cam.QueueFree(); _cam = null; }
        _mario.CameraLocked = false;
        if (_stanceUsed && StanceHoldUntilInput && !_cutsceneSkipped) _mario.SmsHoldDemoClipUntilInput();   // 176b: the stance held until the player moves
        else _mario.SmsEndDemoClip();                                                                      // 176: the stance (or a skipped look-about) ends; the tree's idle as the fight starts
        _mario.SmsHeadLook?.Stop();                                                                        // 175: his head back to the clip
        SpawnBoo();
    }

    /// <summary>The card is over: Mario revealed FallHeight over the floor (falling), the screen fading in on him.</summary>
    private void Reveal()
    {
        if (State == Phase.Falling || State == Phase.Spawned || State == Phase.Done) return;
        Transform3D spawn = _marioSpawn != null ? _marioSpawn.GlobalTransform : _mario.GlobalTransform;
        FloorY = _floor?.FloorYUnder(spawn.Origin.X, spawn.Origin.Z, spawn.Origin.Y + 100f, 400f, Exclude());   // not Mario himself (he waits at his marker)
        if (!FloorY.HasValue && _floor != null && !float.IsNaN(_floor.TopY)) FloorY = _floor.TopY;
        if (FloorY.HasValue) spawn.Origin = new Vector3(spawn.Origin.X, FloorY.Value + FallHeight, spawn.Origin.Z);
        _mario.RevealFromIntro(spawn);
        _mario.Velocity = Vector3.Zero;
        if (SmsFader.Current != null) { SmsFader.Current.SetBlack(); SmsFader.Current.FadeIn(FadeInSeconds); }
        State = Phase.Falling; _sinceReveal = 0f; _landedAt = -1f;
        if (Card != null && IsInstanceValid(Card)) { Card.QueueFree(); Card = null; }
        if (DebugPrint) GD.Print($"[SMS rematch intro] revealed at {spawn.Origin} (floor {(FloorY.HasValue ? FloorY.Value.ToString("F2") : "?")}), fading in over {FadeInSeconds} s");
        EmitSignal(SignalName.Revealed);
    }

    private Godot.Collections.Array<Rid> Exclude() => _mario != null && IsInstanceValid(_mario) ? new Godot.Collections.Array<Rid> { _mario.GetRid() } : null;

    private void SpawnBoo()
    {
        _booSpawned = true;
        if (_boo != null)
        {
            Vector3 at = _booSpawn != null ? _booSpawn.GlobalPosition : _boo.GlobalPosition;
            float? fy = _floor?.FloorYUnder(at.X, at.Z, at.Y + 100f, 400f, Exclude()) ?? FloorY;
            if (fy.HasValue) at.Y = fy.Value + BooHeight;
            _boo.GlobalPosition = at;
            if (_booSpawn != null) _boo.GlobalRotation = new Vector3(0f, _booSpawn.GlobalRotation.Y, 0f);
            _boo.ReadyForFight += () => { State = Phase.Done; if (DebugPrint) GD.Print("[SMS rematch intro] the fight may start"); EmitSignal(SignalName.FightReady); };
            if (_cutsceneRan) _boo.SpawnRising(); else _boo.Spawn();                                    // 174: after the cutscene he rises at his spot, no second laugh
        }
        else
        {
            GD.PushWarning($"[SMS rematch intro] {GetPath()}: BooPath {BooPath} is not an SmsKingBooRematch -- no King Boo");
            State = Phase.Done; EmitSignal(SignalName.FightReady); return;
        }
        State = Phase.Spawned;
        if (DebugPrint) GD.Print($"[SMS rematch intro] King Boo spawns at {_boo.GlobalPosition} ({(_landedAt >= 0f ? "Mario landed" : "timeout")})");
    }
}

/// <summary>
/// The two-line card over black (SMS pass 157): Line1 fades in and holds, Line2 comes under it, both fade out; above the fader (layer 110) so it
/// reads over the black. Delfino (Font/Delfino.ttf) at FontSize, white with a black shadow, unless a LabelSettings is given. Skip() runs the
/// fade out at once. Finished fires when the black after is done.
/// </summary>
public partial class SmsRematchCard : CanvasLayer
{
    public string Line1Text = "", Line2Text = "";
    public LabelSettings Settings; public int FontSize = 46;
    public float BlackBefore = 0.6f, LineFadeIn = 0.45f, Line1Hold = 1.9f, LineGap = 0.3f, Line2Hold = 2.3f, LinesFadeOut = 0.5f, BlackAfter = 0.35f;
    [Signal] public delegate void FinishedEventHandler();

    public float TotalSeconds => BlackBefore + LineFadeIn + Line1Hold + LineGap + LineFadeIn + Line2Hold + LinesFadeOut + BlackAfter;
    public float Alpha1 { get; private set; }
    public float Alpha2 { get; private set; }
    public bool Done { get; private set; }

    private Label _l1, _l2; private float _t; private bool _skipped; private float _skipFrom1, _skipFrom2;

    public override void _Ready()
    {
        Layer = 110;
        var root = new Control { Name = "Card", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var settings = Settings ?? DefaultSettings(FontSize);
        _l1 = MakeLabel(Line1Text, settings, -0.5f); _l2 = MakeLabel(Line2Text, settings, 0.5f);
        root.AddChild(_l1); root.AddChild(_l2);
        _l1.Modulate = new Color(1f, 1f, 1f, 0f); _l2.Modulate = new Color(1f, 1f, 1f, 0f);
    }
    private static Label MakeLabel(string text, LabelSettings settings, float row)
    {
        var l = new Label { Text = text, LabelSettings = settings, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        l.OffsetTop = row * 80f - 40f + 10f; l.OffsetBottom = row * 80f + 40f + 10f;   // two rows about the centre, 80 px apart
        l.GrowHorizontal = Control.GrowDirection.Both; l.GrowVertical = Control.GrowDirection.Both;
        l.AnchorTop = 0.5f; l.AnchorBottom = 0.5f;
        return l;
    }
    public static LabelSettings DefaultSettings(int size)
    {
        var s = new LabelSettings { FontSize = size, FontColor = Colors.White, ShadowColor = new Color(0f, 0f, 0f, 0.9f), ShadowOffset = new Vector2(3f, 3f), ShadowSize = 2 };
        if (ResourceLoader.Exists("res://Font/Delfino.ttf")) s.Font = GD.Load<Font>("res://Font/Delfino.ttf");
        return s;
    }

    /// <summary>The lines fade out now (the rest of the timing dropped); Finished after the fade and the black after.</summary>
    public void Skip()
    {
        if (_skipped || Done) return;
        _skipped = true; _skipFrom1 = Alpha1; _skipFrom2 = Alpha2; _t = 0f;
    }

    public override void _Process(double delta)
    {
        if (Done) return;
        _t += (float)delta;
        if (_skipped)
        {
            float k = LinesFadeOut > 0f ? Mathf.Clamp(_t / LinesFadeOut, 0f, 1f) : 1f;
            Alpha1 = _skipFrom1 * (1f - k); Alpha2 = _skipFrom2 * (1f - k);
            Apply();
            if (_t >= LinesFadeOut + BlackAfter) Finish();
            return;
        }
        float t = _t;
        float a1 = 0f, a2 = 0f;
        float t1In = BlackBefore, t1Hold = t1In + LineFadeIn, t2In = t1Hold + Line1Hold + LineGap, t2Hold = t2In + LineFadeIn, tOut = t2Hold + Line2Hold, tEnd = tOut + LinesFadeOut;
        if (t < t1In) a1 = 0f;
        else if (t < t1Hold) a1 = (t - t1In) / Mathf.Max(LineFadeIn, 1e-3f);
        else if (t < tOut) a1 = 1f;
        else if (t < tEnd) a1 = 1f - (t - tOut) / Mathf.Max(LinesFadeOut, 1e-3f);
        if (t < t2In) a2 = 0f;
        else if (t < t2Hold) a2 = (t - t2In) / Mathf.Max(LineFadeIn, 1e-3f);
        else if (t < tOut) a2 = 1f;
        else if (t < tEnd) a2 = 1f - (t - tOut) / Mathf.Max(LinesFadeOut, 1e-3f);
        Alpha1 = Mathf.Clamp(a1, 0f, 1f); Alpha2 = Mathf.Clamp(a2, 0f, 1f);
        Apply();
        if (t >= tEnd + BlackAfter) Finish();
    }
    private void Apply()
    {
        if (_l1 != null) _l1.Modulate = new Color(1f, 1f, 1f, Alpha1);
        if (_l2 != null) _l2.Modulate = new Color(1f, 1f, 1f, Alpha2);
    }
    private void Finish() { Done = true; Alpha1 = Alpha2 = 0f; Apply(); EmitSignal(SignalName.Finished); }
}
