using Godot;
using System;
using System.Collections.Generic;

/// <summary>TTalk2D2 (GC2D/Talk2D2.cpp), the NPC talk balloon -- the "normal window" path, 1:1 with the game and its message_2.blo layout
/// (game_6/scrn): a 10 x 10 root pane 'me_0' the code parks at (388, 115) turned -18 deg (TALK_A) or (160, 135) turned 20 (TALK_B, a small
/// NPC) on the 640 x 448 frame, its (5, 5) centre the pivot; three line panes 'me_1..3' at y -33 / 0 / 33, each with its blue band 'bacN'
/// (message_back_0.bti, 292 x 58 at (-141, -25), turned 1 deg, tint 0000ff alpha 160), its text curve (the marker boxes f_1 (-128, 4) /
/// f_2 (-5, -16) / f_3 (118, 4) -> the quadratic Bezier (-128, 4) (0, -42) (128, 4) every 20 x 20 glyph box of the line is laid along --
/// makeBoxLine), and its mark group 'cuN' at (159, 24): the blue dot 'cr' (26 x 26 at (-8, -8), alpha 160) under the white arrow 'cc'
/// (18 x 18 at (-2, -3), turned 70: more pages) or the white dot 'cs' (16 x 16 at (-3, -3): the last page).
///
/// The flow (perform: CUE_MOVE every 120 Hz tick, CUE_CALC_ANIM once a 30 fps frame; the director's unk124 == 2): openTalkWindow (the talk
/// camera, the HUD's telop / help balloon / lives away) -> state 2 until the camera's inbetween is over (isThing) -> state 3, 20 frames ->
/// state 4 openNormalWindow: line i's band parameter runs i + 1 -> -0.109 at 0.04 a tick (openWindow slides the ramp texture across the
/// band: it wipes in left to right), held at 1 until the line above has shown its last glyph; the moment it passes 1 the band sound plays and
/// the line's first glyph shows; glyphs fade in one after another, 0x40 alpha a tick (A held past the third glyph: 0x80 and no waits);
/// every band landed -> state 5 moveTalkWindow: the remaining glyphs finish (one step a line a tick), then the last line's mark group fades
/// in (0x10 a tick) and its arrow / dot pulses 255 -> 60 by 4, back by 2; checkControler: A or B -> state 7 eraseNormalWindow (the root
/// fades 0x10 a tick and 0x10 more once a frame, then the next page is laid out and state 4 rolls it out) or, on the last page,
/// closeTalkWindow: the camera goes back (makeMtxForPrevTalk), every NPC's npcTalkOut, the telop may return, state 6 closeNormalWindow
/// fades the root 0x10 a tick -> state 0, and the director frees Mario (readBillboard -> WAIT).
///
/// Text: the game's pages come from the stage's .bmg with hand-broken lines; here an NPC's Lines are its pages, '\n' breaks a line, and
/// AutoWrap (DEVIATION, on) breaks a long line by words where the game would truncate it (the glyph laid past w = 1.1 is the line's last)
/// and spills a fourth line into a new page. Tags as the .bmg's escapes: &lt;c0&gt;..&lt;c5&gt; = cColorTable (white, white, FFB48C, 6EE6FF, FFFF00,
/// AAFF50), &lt;w=N&gt; = the text speed from here on -- N ticks between one glyph's fade and the next's appearance (unk280, kept until the next
/// tag; a new message resets it).
///
/// The choice window (pass 102, 'me_4'): the .bmg's option tags (group 1, tag 0 / 1: a 16-char string each, the first met is the one
/// highlighted) are &lt;s0=Yes&gt; / &lt;s1=No&gt; here, or &lt;yesno&gt; for both; a page carrying them shows, once complete, the 10 x 10 pane the code
/// never moves -- (362, 187) turned -18 whatever the camera mode -- instead of the continue mark: its window 'bac4' (-48, -32) 106 x 74 (a
/// J2DWindow of message_window.bti corners, tint 0000ff alpha 160), the text box 'slct' (35, 15) 56 x 46 in it (the system font at 20 x 24,
/// char space -2, line space 26, bound left / bottom, the box does NOT inherit the pane's alpha) and the marks 'sc_1' (-35, -18) / 'sc_2'
/// (-35, 8), 20 x 20 message_cursor arrows. The pane fades in 0x10 a step, the highlighted mark pulses like the continue mark and the text is
/// rewritten with it: the highlighted option white at the pulse's alpha, the other at 0x60. Up / down (the main stick past 0.5 or the D-pad,
/// on the press edge) move the highlight (MSD_SE_SY_E3_MENU_CURSOR), A or B confirm it (B does not cancel) and go on as the continue mark
/// would; the answer (getSelectedValue) reaches the NPC at npcTalkOut (the game's stage script reads it).
///
/// The board window (pass 103, a sign's text: unk28 = the NPC is ACTOR_TYPE_NPC_BOARD): message_board_1.blo's 'mb_0' pane (296, 223) turned
/// 10 (J2D), its window 'back' (-155, -118) 320 x 246 (a J2DWindow of message_board_1 / _2's corners, blue at alpha 160), the text box 'text'
/// (-124, -88) 257 x 186 (the system font at 20 x 26, char space -1, line space 32, bound centre / top: up to six lines a page, the tags
/// skipped), seven ruled lines 'li_1..7' (256 x 3 at alpha 60) and the mark group 'cu_1' at (0, 142). openBoardWindow: the pane slides in
/// from 80 below over 25 ticks (TBoundPane, a quadratic Bezier with the control at the start), then the mark group shows at alpha 0;
/// moveBoardWindow: the group fades in 4 a tick, then the arrow (more pages) or the dot (last page) pulses 4 a tick between 0 and 255;
/// checkBoardControler: A or B on the last page -> closeTalkWindow at once (the camera back, every NPC's npcTalkOut) while the pane slides
/// 600 up over 60 ticks; A or B earlier -> eraseBoardWindow (the text fades 4 a tick to 0, the next page laid) and appearBoardBoxWindow
/// (back to 255). The game's CUE_CALC_ANIM erase step runs eraseNormalWindow on the board too (perform 1005) -- harmless, the board erase
/// finishes first on its 64th tick -- and is not mirrored here.</summary>
public partial class SmsTalkBalloon : CanvasLayer
{
    public enum TalkState { Closed = 0, WaitCamera = 2, Pause = 3, Opening = 4, Open = 5, Closing = 6, Erasing = 7, Appearing = 8 }

    [Export] public SmsTalkFont Font;
    /// <summary>The frame's 448 lines on the window's height, times this (1 = the game's size; the console's own Size knob is separate).</summary>
    [Export] public float Size = 1f;
    /// <summary>DEVIATION knob (pass 87, his "might need to be shifted up a bit" for three lines): frame units added to the game's park --
    /// (388, 115) for TALK_A, (160, 135) for TALK_B (openTalkWindow). (0, 0) = the game; (0, -25) lifts the balloon 25 of the frame's 448.</summary>
    [Export] public Vector2 ParkOffset = Vector2.Zero;
    [Export] public bool AutoWrap = true;                   // DEVIATION: the game's lines are pre-broken in the message data
    [Export] public string AdvanceActionA = "button_a", AdvanceActionB = "button_b";
    /// <summary>The choice window's up / down (MEANING_SELECT_UP / DOWN: the main stick's direction bit -- JUTGamePad::CStick::getButton sets it
    /// at |y| >= 0.5 -- or the D-pad, on the press edge, no repeat): the stick's axis actions read as strengths, the D-pad / key actions as
    /// presses. An action the project lacks is ignored.</summary>
    [Export] public string StickUpAction = "Lstick_up", StickDownAction = "Lstick_down", DpadUpAction = "ui_up", DpadDownAction = "ui_down";
    [Export] public bool DebugPrint = false;

    // message_2.blo + Talk2D2.cpp constants
    public const int MaxLines = 3, MaxChars = 30;                                   // unk9C[90]: 3 x 30 one-glyph text boxes
    public const float LineSpacing = 33f;                                            // 'me_1' (0, -33), 'me_2' (0, 0), 'me_3' (0, 33)
    public static readonly Vector2 CurveP0 = new(-128f, 4f), CurveP1 = new(0f, -42f), CurveP2 = new(128f, 4f); // f_1.x1/y1, (0, f_2.y1 - 26), (f_3.x2 - 10, f_3.y1)
    public const float AdvanceScale = 0.7f, AdvancePad = 4f, OverflowW = 1.1f;       // makeBoxLine: w += unk94 * (0.7 * width + 4); the glyph laid past 1.1 is the last
    public const float GlyphBoxYBase = -37f;                                          // move(ix, -0x25 - iy)
    public const float BandRollStep = 0.04f, BandSoundAt = 1.0f, BandDone = -0.109f; // unk338, unk33C, openNormalWindow's floor
    public const int FadeStep = 0x40, FadeStepSkip = 0x80, RootFadeStep = 0x10, MarkFadeStep = 0x10, PulseUp = 2, PulseDown = 4, PulseMin = 60;
    public const int PauseFrames = 20;                                               // unk251 = 0x14, counted in 30 fps frames (CUE_CALC_ANIM)
    public const int SkipAfterGlyphs = 2;                                             // unk2DE > 2
    public static readonly Vector2 RootPosTalkA = new(388f, 115f), RootPosTalkB = new(160f, 135f);   // openTalkWindow by gpCamera->mMode
    public const float RootRotTalkA = -18f, RootRotTalkB = 20f;                      // J2D degrees (J2DPane::makeMatrix turns by -mRotation)
    public static readonly Vector2 RootPivot = new(5f, 5f);                          // the 10 x 10 pane's base position 4 (its centre)
    // 'me_4' (pass 102): the pane at (362, 187) ROT 342, 'bac4' (-48, -32) 106 x 74, 'slct' (35, 15) 56 x 46 in it, font 20 x 24, 'sc_1' (-35, -18), 'sc_2' (-35, 8)
    public static readonly Vector2 ChoicePos = new(362f, 187f);
    // the board (pass 103): 'mb_0' (296, 223) ROT 10; 'text' (-124, -88) 257 x 186, font 20 x 26, char space -1, line space 32; the slide-in / out
    public static readonly Vector2 BoardPos = new(296f, 223f);
    public const float BoardRotJ2D = 10f;
    public static readonly Vector2 BoardTextBox = new(-124f, -88f);
    public const float BoardTextBoxW = 257f, BoardTextBoxH = 186f, BoardFontW = 20f, BoardFontH = 26f, BoardCharSpace = -1f, BoardLineSpace = 32f;
    public const int BoardMaxLines = 6;                                                // setupBoardTextBox: lineIdx < 6
    public const int BoardOpenTicks = 25, BoardCloseTicks = 60, BoardFadeStep = 4;    // setPanePosition(0x19 ..) / (60 ..); the 4-a-tick fades
    /// <summary>DEVIATION (Jaysonn, 10-19: "speed up the page fade"): the text's fade on a page flip, per 120 Hz tick. The game's
    /// eraseBoardWindow / appearBoardBoxWindow step 4 (64 ticks = 0.53 s each way); 8 halves that. The mark group's fade-in and the arrow /
    /// dot pulse keep the game's 4 (BoardFadeStep).</summary>
    [Export(PropertyHint.Range, "1,64,1")] public int BoardPageFadeStep = 8;
    public static readonly Vector2 BoardOpenFrom = new(0f, 80f), BoardCloseTo = new(0f, -600f);
    public const float ChoiceRotJ2D = -18f;
    public static readonly Vector2 ChoiceTextBox = new(-13f, -17f);                  // bac4 + slct, in the pane
    public const float ChoiceTextBoxH = 46f, ChoiceFontW = 20f, ChoiceFontH = 24f, ChoiceCharSpace = -2f, ChoiceLineSpace = 26f;
    public const int ChoiceDimAlpha = 0x60, ChoiceMaxChars = 16;                    // the other option's CC/GC alpha; unk218's 0x11 buffer
    public const float StickSelectAt = 0.5f;                                         // CStick::getButton: MAINSTICK_UP / DOWN at |y| >= 0.5
    public static readonly Color[] ColorTable =                                       // cColorTable[6] (RGBA8)
    {
        new(1f, 1f, 1f), new(1f, 1f, 1f), new(1f, 0xB4 / 255f, 0x8C / 255f), new(0x6E / 255f, 0xE6 / 255f, 1f), new(1f, 1f, 0f), new(0xAA / 255f, 1f, 0x50 / 255f),
    };

    public TalkState State { get; private set; } = TalkState.Closed;
    public bool IsOpen => State != TalkState.Closed;
    public bool IsOpenOn(SmsNpc npc) => IsOpen && _npc == npc;
    public bool LastPage => _lastPage;
    public int Page => _page;
    public int PageCount => _pages.Count;
    /// <summary>The page's glyphs are all in and the mark group is up: A / B would act (checkControler's gate).</summary>
    public bool PageComplete => State == TalkState.Open && ((_marks[_lines] != null && _marks[_lines].Visible) || ChoiceVisible || (_board && BoardMarkVisible));
    /// <summary>getSelectedValue as the NPC reads it: the option confirmed with A / B this talk (0 = the first, 1 = the second), -1 = none -- no
    /// choice in the message, or the talk ended before one was confirmed (the game keeps the highlighted one there; nothing reads it).</summary>
    public int SelectedValue => _choiceConfirmed ? _choice : -1;
    public int ChoiceHighlighted => _choice;                                          // unk214 (-1: no choice window in this message)
    public bool ChoiceVisible => _choicePane != null && _choicePane.Visible;
    public int ChoiceAlpha => _choiceAlpha;
    public int PulseAlpha => _pulseAlpha;
    public string ChoiceOption(int i) => i >= 0 && i < 2 ? _opt[i] ?? "" : "";
    public float BandRoll(int line) => _band[Math.Clamp(line, 0, MaxLines - 1)];
    public int GlyphsShown(int line) { int n = 0; for (int k = 0; k <= _last[line] && k < MaxChars; k++) if (_g[line][k].Shown) n++; return n; }
    public int GlyphCount(int line) => _last[line] + 1;
    public int LinesUsed => _lines + 1;
    public int RootAlpha => _rootAlpha;
    public Vector2 RootPark => _rootPos;
    public float RootRotJ2D => _rootRot;
    /// <summary>For the drives: a laid glyph's box (in its line pane's frame), its J2D rotation, colour and fade state.</summary>
    public (char c, Vector2 box, float rotJ2D, Color col, bool shown, int alpha) GlyphInfo(int line, int k)
    {
        ref Glyph g = ref _g[Math.Clamp(line, 0, MaxLines - 1)][Math.Clamp(k, 0, MaxChars - 1)];
        return (g.C, g.Box, g.RotJ2D, g.Col, g.Shown, g.Alpha);
    }

    private struct Glyph
    {
        public char C; public Vector2 Box; public float RotJ2D; public Color Col; public int WaitAfter;
        public bool Shown; public int Alpha;
    }

    private Control _root, _glyphs;
    private readonly TextureRect[] _bands = new TextureRect[MaxLines];
    private readonly ShaderMaterial[] _bandMats = new ShaderMaterial[MaxLines];
    private readonly Control[] _marks = new Control[MaxLines];
    private readonly TextureRect[] _markArrow = new TextureRect[MaxLines], _markDot = new TextureRect[MaxLines];
    private AudioStreamPlayer _sfxObi, _sfxSelect, _sfxCursor;
    private bool _nodesReady;
    private Control _choicePane, _choiceText; private NinePatchRect _choiceWindow;   // 'me_4', 'slct' (a sibling: no alpha inheritance), 'bac4'
    private readonly TextureRect[] _choiceMarks = new TextureRect[2];                // 'sc_1', 'sc_2'
    private readonly string[] _opt = new string[2];                                  // unk218
    private int _choice = -1;                                                        // unk214
    private bool _choiceConfirmed;
    private int _choiceAlpha;                                                        // 'me_4' alpha
    private bool _edgeUp, _edgeDown; private int _stickBits;                         // MEANING_SELECT_UP / DOWN this tick; MAINSTICK bits (8 up, 4 down)

    private readonly Glyph[][] _g = new Glyph[MaxLines][];
    private readonly int[] _last = new int[MaxLines];        // unk228: the line's last glyph index (-1: no glyph)
    private readonly int[] _next = new int[MaxLines];        // unk224: 0 = the band has not started; else the next glyph to fade
    private readonly float[] _band = new float[MaxLines];    // unk234
    private int _lines;                                      // unk274: the index of the last line used
    private int _wait;                                       // unk2DC
    private int _latest;                                     // unk2DE: the latest glyph reached (flat index)
    private bool _skip;                                      // unk26C
    private int _fadeStep = FadeStep;                        // unk340
    private bool _lastPage;                                  // unk26A
    private int _rootAlpha = 255;                            // unk90's alpha
    private int _pauseFrames; private float _frameAccum;     // unk251 (30 fps frames)
    private bool _pulseUp;                                   // unk26B
    private int _markAlpha, _pulseAlpha;
    private int _textWait;                                   // unk280: the speed tag in force while laying out

    private List<string[]> _pages = new();
    private int _page;
    private float _invCurveLen;                              // unk94
    private float _tickAccum;
    private bool _edgeA, _edgeB;
    private Vector2 _rootPos = RootPosTalkA; private float _rootRot = RootRotTalkA;

    private SmsNpc _npc; private Mario _mario; private SunshineCamera _camera;

    // ---- the board window (pass 103): unk10 / unk14 (TBoundPane) / unk18 (text) / unk1C (the mark group) / unk20 (arrow) / unk24 (dot)
    private bool _board;                                      // unk28
    private int _boardStep;                                   // unk29: openBoardWindow's case
    private Control _boardPane, _boardText, _boardMark; private TextureRect _boardArrow, _boardDot;
    private int _boardTextAlpha = 255;                        // unk18's alpha: 255 from the blo, faded 4 a tick on a page flip and back
    private int _boardMarkAlpha, _boardArrowAlpha = 255, _boardDotAlpha = 255;   // unk1C / unk20 / unk24: the children keep the blo's 255 until the group has faded in
    private bool _boardPulseUp;                               // unk26B
    private string[][] _boardPages = System.Array.Empty<string[]>();
    private Vector2 _boundOffset, _boundStart, _boundCtrl, _boundEnd; private float _boundT, _boundStep; private bool _boundMoving;   // TBoundPane
    public bool IsBoardWindow => _board;
    public Vector2 BoardOffset => _boundOffset;
    /// <summary>The balloon's 120 Hz ticks since it opened (for tests: the fades and slides count in these, not in physics frames).</summary>
    public int TickCount { get; private set; }
    public int BoardTextAlpha => _boardTextAlpha;
    public int BoardMarkAlpha => _boardMarkAlpha;
    public bool BoardMarkVisible => _boardMark != null && _boardMark.Visible;
    public int BoardArrowAlpha => _boardArrowAlpha;
    public int BoardDotAlpha => _boardDotAlpha;
    public int BoardLineCount => _page < _boardPages.Length ? _boardPages[_page].Length : 0;
    public string BoardLine(int i) => _page < _boardPages.Length && i < _boardPages[_page].Length ? _boardPages[_page][i] : "";

    public static SmsTalkBalloon Instance { get; private set; }
    public const string ScenePath = "res://assets/Sms/Talk/TalkBalloon.tscn";

    /// <summary>The stage's one balloon (the game's "会話表示" lives in the common 2D group): made on the first ask -- an NPC's _Ready, so it
    /// loads with the level and its shader compiles there, not at the first talk -- and kept on the tree's root.</summary>
    public static SmsTalkBalloon Ensure(Node from)
    {
        if (Instance != null && GodotObject.IsInstanceValid(Instance)) return Instance;
        var scene = GD.Load<PackedScene>(ScenePath);
        if (scene == null) return null;
        Instance = scene.Instantiate<SmsTalkBalloon>();
        from.GetTree().Root.CallDeferred(Node.MethodName.AddChild, Instance);
        return Instance;
    }

    public override void _Ready()
    {
        EnsureNodes();
        Visible = false;
    }

    private void EnsureNodes()
    {
        if (_nodesReady) return;
        _nodesReady = true;
        _root = GetNodeOrNull<Control>("Root");
        _glyphs = GetNodeOrNull<Control>("Root/Glyphs");
        for (int i = 0; i < MaxLines; i++)
        {
            _g[i] = new Glyph[MaxChars];
            _bands[i] = GetNodeOrNull<TextureRect>($"Root/Line{i + 1}/Band");
            _bandMats[i] = _bands[i]?.Material as ShaderMaterial;
            _marks[i] = GetNodeOrNull<Control>($"Root/Line{i + 1}/Mark");
            _markArrow[i] = GetNodeOrNull<TextureRect>($"Root/Line{i + 1}/Mark/Arrow");
            _markDot[i] = GetNodeOrNull<TextureRect>($"Root/Line{i + 1}/Mark/Dot");
            _band[i] = i + 1f;
            _last[i] = -1;
        }
        _sfxObi = GetNodeOrNull<AudioStreamPlayer>("Sfx/Obi");
        _sfxSelect = GetNodeOrNull<AudioStreamPlayer>("Sfx/Select");
        _sfxCursor = GetNodeOrNull<AudioStreamPlayer>("Sfx/Cursor");
        _choicePane = GetNodeOrNull<Control>("Choice");
        _choiceWindow = GetNodeOrNull<NinePatchRect>("Choice/Window");
        _choiceMarks[0] = GetNodeOrNull<TextureRect>("Choice/Mark1");
        _choiceMarks[1] = GetNodeOrNull<TextureRect>("Choice/Mark2");
        _choiceText = GetNodeOrNull<Control>("ChoiceText");
        _boardPane = GetNodeOrNull<Control>("Board");
        _boardText = GetNodeOrNull<Control>("Board/Text");
        _boardMark = GetNodeOrNull<Control>("Board/Mark");
        _boardArrow = GetNodeOrNull<TextureRect>("Board/Mark/Arrow");
        _boardDot = GetNodeOrNull<TextureRect>("Board/Mark/Dot");
        if (_glyphs != null) _glyphs.Draw += DrawGlyphs;
        if (_choiceText != null) _choiceText.Draw += DrawChoice;
        if (_boardText != null) _boardText.Draw += DrawBoardText;
        if (_boardPane != null) _boardPane.Visible = false;
        _invCurveLen = 1f / CurveLength();
        HideAll();
        HideChoice();
    }

    /// <summary>loadAfter: the curve's length in 100 steps, 1 / that is the glyph advance's unit.</summary>
    private static float CurveLength()
    {
        Vector2 prev = Curve(0f); float len = 0f;
        for (float t = 0.01f; t <= 1.0f; t += 0.01f) { Vector2 p = Curve(t); len += p.DistanceTo(prev); prev = p; }
        return len;
    }

    /// <summary>makeLine: the quadratic Bezier through the line's three marker boxes.</summary>
    private static Vector2 Curve(float t)
    {
        float inv = 1f - t;
        return CurveP0 * (inv * inv) + CurveP1 * (2f * t * inv) + CurveP2 * (t * t);
    }

    // ------------------------------------------------------------------ the director's calls
    /// <summary>openTalkWindow (the director's unk126 = 2, from readBillboard facing the NPC): makeMtxForTalk, the root parked for the camera's
    /// mode, alpha 255, the telop / help balloon / lives away, state 2. The message was set by the NPC's talk start (setMessageID): the text
    /// speed and the colour reset, the first page laid out.</summary>
    public void Open(SmsNpc npc, Mario mario, string[] pages)
    {
        EnsureNodes();
        if (IsOpen) ForceClose();
        _npc = npc; _mario = mario; _camera = mario?.SmsCamera;
        _textWait = 0;                                                      // unk280 = 0
        _skip = false; _fadeStep = FadeStep; _wait = 0; _latest = 0;        // unk26C / unk340 / unk2DC / unk2DE
        _choice = -1; _choiceConfirmed = false; _opt[0] = _opt[1] = null;  // unk214 = -1 (setMessageID)
        HideChoice();
        _board = npc != null && npc.Profile != null && npc.Profile.IsBoard;   // unk28 = getActorType() == ACTOR_TYPE_NPC_BOARD
        _boardStep = 0; _boundMoving = false; _boundOffset = Vector2.Zero;
        if (_board) { _boardPages = LayoutBoardPages(pages); _pages = new List<string[]>(_boardPages); }
        else _pages = LayoutPages(pages);
        _page = 0;
        _camera?.MakeMtxForTalk(npc != null && npc.IsSmallNpc, mario != null ? mario.SmsCamFaceAngleBam() : 0);
        bool talkB = _camera != null ? _camera.IsTalkCameraSmall : npc != null && npc.IsSmallNpc;
        _rootPos = talkB ? RootPosTalkB : RootPosTalkA;
        _rootRot = talkB ? RootRotTalkB : RootRotTalkA;
        _rootAlpha = 255;
        var hud = SmsHudLayer.Current;
        hud?.Telop?.Disappear();                                            // startDisappearTelop
        hud?.Balloon?.Hide();                                               // startDisappearBalloon(.., true)
        hud?.Standard?.TalkIn();                                            // startDisappearMario
        _tickAccum = 0f; _frameAccum = 0f; TickCount = 0;
        if (_board) SetupBoardPage(); else SetupPage();                     // setMessageID's setupTextBox: the layout is ready before the roll-out
        if (_root != null) _root.Visible = !_board;                         // unk28: the draw is unk10 (the board screen) or unk2C (the balloon's)
        if (_boardPane != null) _boardPane.Visible = false;                 // pass 104: unk10 is drawn from state 4 (Opening) on (perform's CUE_DRAW switch, states 4-7) -- nothing through WaitCamera / Pause; pass 103 showed it PARKED there, then it jumped 80 below and slid ("it just appears, then slides in")
        if (_board && _boardMark != null) { _boardMark.Visible = false; _boardMark.Modulate = new Color(1f, 1f, 1f, 0f); }
        State = TalkState.WaitCamera;
        UpdateRootTransform();
        Visible = true;
        if (DebugPrint) GD.Print($"[SMS talk2d] open on {npc?.Name}: {_pages.Count} page(s), {(_board ? "BOARD, " : "")}{(talkB ? "TALK_B" : "TALK_A")} park ({_rootPos.X}, {_rootPos.Y}) rot {_rootRot}");
    }

    /// <summary>closeTalkWindow (unk270 & 1): MSD_SE_SY_SELECT_COMMON, makeMtxForPrevTalk (the camera back, onNeutralMarioKey, every NPC's
    /// npcTalkOut), startAppearTelop(false) (the port's telop returns on its own clock), then state 6's fade.</summary>
    private void CloseTalkWindow()
    {
        _sfxSelect?.Play();
        _camera?.MakeMtxForPrevTalk();
        SmsNpc.NpcTalkOutAll();
        State = TalkState.Closing;
    }

    /// <summary>forceCloseTalk (an event ends the talk: the NPC hurt, taken, sprayed, gone): makeMtxForPrevTalk, the telop back, state 6 -- or
    /// straight to 0 before the window was up.</summary>
    public void ForceClose()
    {
        if (!IsOpen) return;
        _camera?.MakeMtxForPrevTalk();
        SmsNpc.NpcTalkOutAll();
        if (State == TalkState.WaitCamera || State == TalkState.Pause) { State = TalkState.Closed; Finish(); return; }
        State = TalkState.Closing;
    }

    /// <summary>unk248 back to 0: the director's unk126 = 0 -> PAD_FLAG_TALK_SELECT off, readBillboard -> WAIT.</summary>
    private void Finish()
    {
        HideAll();
        HideChoice();
        if (_boardPane != null) _boardPane.Visible = false;
        if (_root != null) _root.Visible = true;
        Visible = false;
        var mario = _mario;
        _npc = null; _mario = null; _camera = null;
        mario?.SmsTalkEnd();
    }

    // ------------------------------------------------------------------ the frame
    public override void _PhysicsProcess(double delta)
    {
        if (!IsOpen) return;
        _edgeA = Input.IsActionJustPressed(AdvanceActionA);
        _edgeB = Input.IsActionJustPressed(AdvanceActionB);
        ReadSelectEdges();
        int n = SmsUnits.TicksFor(delta, ref _tickAccum);
        _frameAccum += (float)delta * 30f;              // CUE_CALC_ANIM: once a 30 fps frame
        bool frameEdge = false;
        if (_frameAccum >= 1f) { _frameAccum -= 1f; frameEdge = true; }
        for (int i = 0; i < n; i++)
        {
            Tick(i == n - 1 && frameEdge);
            _edgeA = _edgeB = _edgeUp = _edgeDown = false;
            if (!IsOpen) break;
        }
        if (n == 0 && frameEdge && IsOpen) CalcAnim();
        _glyphs?.QueueRedraw();
    }

    private void Tick(bool frameLast)
    {
        TickCount++;
        switch (State)
        {
            case TalkState.WaitCamera:
                if (_camera == null || _camera.TalkCameraSettled) { _pauseFrames = PauseFrames; State = TalkState.Pause; }
                break;
            case TalkState.Opening:
                if (_board ? OpenBoardWindow() : OpenNormalWindow()) State = TalkState.Open;
                break;
            case TalkState.Open:
                if (_board) { MoveBoardWindow(); CheckBoardControler(); }
                else { MoveTalkWindow(); CheckControler(); }
                break;
            case TalkState.Closing:
                if (_board ? CloseBoardWindow() : CloseNormalWindow()) { State = TalkState.Closed; Finish(); return; }
                break;
            case TalkState.Erasing:
                if (_board) { if (EraseBoardWindow()) State = TalkState.Appearing; }
                else if (EraseNormalWindow()) State = TalkState.Opening;
                break;
            case TalkState.Appearing:
                if (AppearBoardBoxWindow()) State = TalkState.Open;
                break;
        }
        if (frameLast) CalcAnim();
        ApplyAlpha();
    }

    /// <summary>The once-a-frame half of perform: the 20-frame pause after the camera lands, and the erase's extra fade step.</summary>
    private void CalcAnim()
    {
        switch (State)
        {
            case TalkState.Pause:
                if (--_pauseFrames < 0) { _latest = 0; State = TalkState.Opening; }
                break;
            case TalkState.Erasing:
                if (!_board && EraseNormalWindow()) State = TalkState.Opening;   // the game runs this step on the board too (harmless: see the header)
                break;
        }
    }

    // ------------------------------------------------------------------ the board window (pass 103: TTalk2D2's unk28 branch)
    /// <summary>TBoundPane::setPanePosition: the offset runs start -> end along a quadratic Bezier (control) in `ticks` updates.</summary>
    private void BoundSet(int ticks, Vector2 start, Vector2 control, Vector2 end)
    {
        _boundT = 0f; _boundStep = 1f / ticks; _boundStart = start; _boundCtrl = control; _boundEnd = end; _boundMoving = true;
    }

    /// <summary>TBoundPane::update: true once nothing moves. The step past 1 clamps and ends the motion; the offset is rounded half away.</summary>
    private bool BoundUpdate()
    {
        if (_boundMoving)
        {
            if (_boundT > 1f) { _boundT = 1f; _boundMoving = false; }
            float t = _boundT, u = 1f - t;
            Vector2 p = _boundEnd * (t * t) + (_boundStart * (u * u) + _boundCtrl * (2f * u * t));
            _boundOffset = new Vector2(RoundHalfAway(p.X), RoundHalfAway(p.Y));
            _boundT += _boundStep;
        }
        return !_boundMoving;
    }

    /// <summary>openBoardWindow: case 0 arms the slide from 80 below (the pane's update is idle, so this is the first tick); case 1 waits for it,
    /// then shows the mark group at alpha 0. The pane itself shows on case 1's first update (pass 104): the game draws the board screen from
    /// this state on, never during the camera wait and the pause.</summary>
    private bool OpenBoardWindow()
    {
        switch (_boardStep)
        {
            case 0:
                if (BoundUpdate()) { BoundSet(BoardOpenTicks, BoardOpenFrom, BoardOpenFrom, Vector2.Zero); _boardStep = 1; }
                break;
            case 1:
            {
                bool done = BoundUpdate();                                    // the first update puts the pane at the slide's start (80 below)
                if (_boardPane != null && !_boardPane.Visible) { _boardPane.Visible = true; UpdateRootTransform(); }   // pass 104: shown here, never parked before the slide
                if (done)
                {
                    _boardMarkAlpha = 0;
                    if (_boardMark != null) { _boardMark.Visible = true; _boardMark.Modulate = new Color(1f, 1f, 1f, 0f); }
                    _boardStep = 2;
                    return true;
                }
                break;
            }
        }
        return false;
    }

    /// <summary>moveBoardWindow: the mark group fades in 4 a tick (the children still at their own alpha, 255 from the blo); at 255 the arrow and
    /// the dot are set to 0 and the one for this page pulses 4 a tick up to 255 and down to 0. The other keeps whatever alpha it had.</summary>
    private void MoveBoardWindow()
    {
        if (_boardMarkAlpha < 255)
        {
            _boardMarkAlpha += BoardFadeStep;
            if (_boardMarkAlpha > 255) { _boardDotAlpha = 0; _boardArrowAlpha = 0; _boardPulseUp = true; _boardMarkAlpha = 255; }
            return;
        }
        ref int alpha = ref (_lastPage ? ref _boardDotAlpha : ref _boardArrowAlpha);
        if (_boardPulseUp) { alpha += BoardFadeStep; if (alpha > 255) { _boardPulseUp = false; alpha = 255; } }
        else { alpha -= BoardFadeStep; if (alpha < 0) { _boardPulseUp = true; alpha = 0; } }
    }

    /// <summary>checkBoardControler: on the last page A or B (the press edge) plays MSD_SE_SY_SELECT_COMMON, arms the slide 600 up over 60 ticks and
    /// closes the talk at once (closeTalkWindow: the camera back, every NPC's npcTalkOut); earlier, A or B goes to the erase (state 7).</summary>
    private void CheckBoardControler()
    {
        if (!(_edgeA || _edgeB)) return;
        if (_lastPage) { BoundSet(BoardCloseTicks, Vector2.Zero, Vector2.Zero, BoardCloseTo); CloseTalkWindow(); }
        else { _sfxSelect?.Play(); State = TalkState.Erasing; }
    }

    /// <summary>closeBoardWindow: done when the slide-out has run (the pane is then 600 above its park).</summary>
    private bool CloseBoardWindow() => BoundUpdate();

    /// <summary>eraseBoardWindow: the text fades 4 a tick; under 0 the next page is laid (setupTextBox) with the text at 0.</summary>
    private bool EraseBoardWindow()
    {
        _boardTextAlpha -= Mathf.Max(1, BoardPageFadeStep);                   // the game: BoardFadeStep (4); pass 104's knob
        if (_boardTextAlpha >= 0) return false;
        _boardTextAlpha = 0;
        _page++;
        SetupBoardPage();
        return true;
    }

    /// <summary>appearBoardBoxWindow: the text fades back 4 a tick to 255.</summary>
    private bool AppearBoardBoxWindow()
    {
        _boardTextAlpha += Mathf.Max(1, BoardPageFadeStep);                   // the game: BoardFadeStep (4); pass 104's knob
        if (_boardTextAlpha <= 255) return false;
        _boardTextAlpha = 255;
        return true;
    }

    /// <summary>setupBoardTextBox for the current page: the lines go to the text box as one string (unk18), the last page flagged (unk26A).</summary>
    private void SetupBoardPage()
    {
        _lastPage = _page >= _boardPages.Length - 1;
        _lines = 0;
        _boardText?.QueueRedraw();
    }

    /// <summary>The NPC's Lines as the board reads them: '\n' breaks, six lines a page (setupBoardTextBox's lineIdx &lt; 6), the &lt;tags&gt; dropped (the
    /// board skips every 0x1A tag: no colours, no choice), a line past the box's 257 broken by words (J2DPrint wraps it by glyph: AutoWrap's
    /// word break is the port's).</summary>
    private string[][] LayoutBoardPages(string[] pages)
    {
        var out_ = new List<string[]>();
        if (pages == null || pages.Length == 0) pages = new[] { "..." };
        foreach (string page in pages)
        {
            var lines = new List<string>();
            foreach (string raw0 in (page ?? "").Replace("\r", "").Split('\n'))
            {
                string raw = StripTags(raw0);
                if (!AutoWrap || BoardLineWidth(raw) <= BoardTextBoxW) { lines.Add(raw); continue; }
                string cur = "";
                foreach (string word in raw.Split(' '))
                {
                    string trial = cur.Length == 0 ? word : cur + " " + word;
                    if (BoardLineWidth(trial) <= BoardTextBoxW || cur.Length == 0) cur = trial;
                    else { lines.Add(cur); cur = word; }
                }
                if (cur.Length > 0) lines.Add(cur);
            }
            for (int i = 0; i < lines.Count; i += BoardMaxLines)
                out_.Add(lines.GetRange(i, Math.Min(BoardMaxLines, lines.Count - i)).ToArray());
        }
        if (out_.Count == 0) out_.Add(new[] { "" });
        return out_.ToArray();
    }

    private static string StripTags(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        for (int p = 0; p < s.Length; p++)
        {
            if (s[p] == '<') { int e = s.IndexOf('>', p); if (e > p) { p = e; continue; } }
            sb.Append(s[p]);
        }
        return sb.ToString();
    }

    /// <summary>J2DPrint::parse's line width at 20 x 26 with char space -1: the first glyph steps kern + width, the rest width, each then the
    /// char space; the width is the cursor's reach before the last char space (f24).</summary>
    public float BoardLineWidth(string s)
    {
        if (Font == null || string.IsNullOrEmpty(s)) return 0f;
        float sx = BoardFontW / Font.Width, x = 0f, reach = 0f; bool first = true;
        foreach (char c in s)
        {
            Font.WidthEntry(c, out int kern, out int adv);
            x += (first ? adv + kern : adv) * sx;
            reach = Math.Max(reach, x);
            x += BoardCharSpace;
            first = false;
        }
        return reach;
    }

    /// <summary>'text' as J2DTextBox::draw -> J2DPrint::printReturn lays it, bound centre / top: each line's x = (257 - round(width)) / 2 (integer),
    /// the first baseline at 26 (VBIND_TOP adds nothing; mCursorV += fontSizeY), each next line 32 lower; a glyph's cell spans baseline -
    /// ascent x 1.3 .. baseline; white at the text box's alpha.</summary>
    private void DrawBoardText()
    {
        if (Font?.Page == null || _boardText == null || !_board) return;
        float sx = BoardFontW / Font.Width, sy = BoardFontH / Font.CellHeight;
        var col = new Color(1f, 1f, 1f, _boardTextAlpha / 255f);
        int n = BoardLineCount;
        for (int line = 0; line < n; line++)
        {
            string s = BoardLine(line);
            int w = (int)(BoardLineWidth(s) + 0.5f);
            float x0 = (int)((BoardTextBoxW - w) / 2f);
            float baseline = BoardFontH + line * BoardLineSpace;
            float x = 0f; bool first = true;
            foreach (char c in s)
            {
                Font.WidthEntry(c, out int kern, out int adv);
                float drawX = first ? x : x - kern * sx;
                var rect = new Rect2(BoardTextBox.X + x0 + drawX, BoardTextBox.Y + baseline - Font.Ascent * sy, Font.CellWidth * sx, Font.CellHeight * sy);
                _boardText.DrawTextureRectRegion(Font.Page, rect, Font.Cell(c), col);
                x += (first ? adv + kern : adv) * sx + BoardCharSpace;
                first = false;
            }
        }
    }

    // ------------------------------------------------------------------ openNormalWindow / moveTalkWindow / checkControler
    private bool OpenNormalWindow()
    {
        if (_latest > SkipAfterGlyphs && Input.IsActionPressed(AdvanceActionA)) { _skip = true; _fadeStep = FadeStepSkip; }
        bool finished = true;
        for (int i = 0; i <= _lines; i++)
        {
            if (_band[i] > BandDone) { finished = false; _band[i] -= BandRollStep; }
            if (_band[i] < 1f && i != 0 && _last[i - 1] >= 0 && !_g[i - 1][_last[i - 1]].Shown)
                _band[i] = 1f;                                           // a band waits for the line above to finish appearing
            if (_next[i] == 0 && _band[i] < BandSoundAt)
            {
                _sfxObi?.Play();                                         // MSD_SE_SY_TALK_OBI
                _next[i] = 1;
                if (_last[i] >= 0) { _g[i][0].Shown = true; _g[i][0].Alpha = 255; }   // unk9C[i * 30]->show(): the box's alpha is still 255
            }
            _bandMats[i]?.SetShaderParameter("roll", _band[i]);
            if (_bands[i] != null) _bands[i].Visible = true;
        }
        if (finished)
            for (int i = 0; i <= _lines; i++) _bandMats[i]?.SetShaderParameter("roll", -1f);   // the 'bac' pane takes over
        for (int line = 0; line < MaxLines; line++)
        {
            if (_next[line] == 0) continue;
            FadeLine(line, true);
        }
        return finished;
    }

    /// <summary>The glyph fade (openNormalWindow 596 / moveTalkWindow 706): the glyph being faded gains the step; at 255 the next one is shown
    /// at alpha 0 once the wait (unk2DC) has run down -- showing glyph k arms unk281[k], the speed tag in force at glyph k + 1 (zero while the A
    /// skip is on during the roll-out). The roll-out loops through as many glyphs as the tick allows; the open window takes one step a line.</summary>
    private void FadeLine(int line, bool opening)
    {
        while (_next[line] <= _last[line] && _next[line] < MaxChars)
        {
            int idx = _next[line];
            ref Glyph g = ref _g[line][idx];
            if (g.Shown)
            {
                g.Alpha = Math.Min(255, g.Alpha + _fadeStep);
                if (opening) _latest = idx + line * MaxChars;
                if (g.Alpha < 255) break;
                _next[line]++;
                if (!opening) break;
            }
            else
            {
                if (_wait <= 0)
                {
                    _wait = opening && _skip ? 0 : g.WaitAfter;
                    g.Shown = true; g.Alpha = 0;
                }
                else _wait--;
                break;
            }
        }
    }

    private void MoveTalkWindow()
    {
        for (int line = 0; line < MaxLines; line++)
        {
            if (_next[line] == 0 || _next[line] > _last[line]) continue;
            FadeLine(line, false);
        }
        if (_next[_lines] < MaxChars && _next[_lines] <= _last[_lines]) return;   // the last line still appearing
        if (_choice >= 0) { MoveChoiceWindow(); return; }                        // unk214 != -1: 'me_4' and its mark instead of the continue mark
        Control group = _marks[_lines];
        TextureRect fade = _lastPage ? _markDot[_lines] : _markArrow[_lines];
        if (group == null) return;
        if (_markArrow[_lines] != null) _markArrow[_lines].Visible = !_lastPage;
        if (_markDot[_lines] != null) _markDot[_lines].Visible = _lastPage;
        if (group.Visible)
        {
            if (_markAlpha < 255) _markAlpha = Math.Min(255, _markAlpha + MarkFadeStep);
            if (_pulseUp) { _pulseAlpha += PulseUp; if (_pulseAlpha > 255) { _pulseUp = false; _pulseAlpha = 255; } }
            else { _pulseAlpha -= PulseDown; if (_pulseAlpha < PulseMin) { _pulseUp = true; _pulseAlpha = PulseMin; } }
        }
        else
        {
            group.Visible = true; _markAlpha = 0; _pulseAlpha = 255; _pulseUp = false;
        }
        group.Modulate = new Color(1f, 1f, 1f, _markAlpha / 255f);
        if (fade != null) fade.Modulate = new Color(1f, 1f, 1f, _pulseAlpha / 255f);
    }

    private void CheckControler()
    {
        if (_choice >= 0)
        {
            if (!ChoiceVisible) return;                                                      // unk204->isVisible()
            if (_edgeUp && _choice == 1) { _sfxCursor?.Play(); SelectChoice(0); }           // MSD_SE_SY_E3_MENU_CURSOR
            else if (_edgeDown && _choice == 0) { _sfxCursor?.Play(); SelectChoice(1); }
            else if (_edgeA || _edgeB)
            {
                _choiceConfirmed = true;
                if (_lastPage) CloseTalkWindow();                                            // MSD_SE_SY_SELECT_COMMON, state 6
                else { _sfxSelect?.Play(); State = TalkState.Erasing; }                      // state 7 (the pane stays up, as the game's does)
            }
            return;
        }
        Control group = _marks[_lines];
        if (group == null || !group.Visible) return;
        if (!(_edgeA || _edgeB)) return;
        if (_lastPage) CloseTalkWindow();                              // MSD_SE_SY_SELECT_COMMON + closeTalkWindow
        else { _sfxSelect?.Play(); State = TalkState.Erasing; }
    }

    // ------------------------------------------------------------------ the choice window (pass 102: 'me_4')
    /// <summary>MEANING_SELECT_UP / DOWN for this tick: the main stick's MAINSTICK_UP / DOWN bit (CStick::getButton: set at |y| >= 0.5) on its
    /// press edge (CButton::update: mRepeat = mTrigger with no repeat set), or the D-pad / key action just pressed.</summary>
    private void ReadSelectEdges()
    {
        float y = Strength(StickUpAction) - Strength(StickDownAction);
        int bits = y >= StickSelectAt ? 8 : y <= -StickSelectAt ? 4 : 0;
        _edgeUp = ((bits & 8) != 0 && (_stickBits & 8) == 0) || JustPressed(DpadUpAction);
        _edgeDown = ((bits & 4) != 0 && (_stickBits & 4) == 0) || JustPressed(DpadDownAction);
        _stickBits = bits;
    }

    private static float Strength(string action) => !string.IsNullOrEmpty(action) && InputMap.HasAction(action) ? Input.GetActionStrength(action) : 0f;
    private static bool JustPressed(string action) => !string.IsNullOrEmpty(action) && InputMap.HasAction(action) && Input.IsActionJustPressed(action);

    /// <summary>checkControler's up / down: the mark hidden keeps its alpha (setAlpha(0xFE)), the other shows and the pulse goes on from there.</summary>
    private void SelectChoice(int i)
    {
        _choice = i;
        for (int k = 0; k < 2; k++) if (_choiceMarks[k] != null) _choiceMarks[k].Visible = k == i;
        _pulseAlpha = 0xFE;
        _choiceText?.QueueRedraw();
    }

    /// <summary>moveTalkWindow's tail with a choice (unk214 != -1, the page complete): 'me_4' shown at alpha 0 and faded 0x10 a step; the
    /// highlighted mark pulses 255 -> 60 by 4 and back by 2 (unk26B), and 'slct' is rewritten each step -- the highlighted option white at
    /// the pulse's alpha (CC / GC ffffff%02x), the other at 0x60 (the setup's 7f7f7f grey is overwritten at once).</summary>
    private void MoveChoiceWindow()
    {
        if (_choicePane == null) return;
        if (_choicePane.Visible)
        {
            if (_choiceAlpha < 255) _choiceAlpha = Math.Min(255, _choiceAlpha + MarkFadeStep);
            if (_pulseUp) { _pulseAlpha += PulseUp; if (_pulseAlpha > 255) { _pulseUp = false; _pulseAlpha = 255; } }
            else { _pulseAlpha -= PulseDown; if (_pulseAlpha < PulseMin) { _pulseUp = true; _pulseAlpha = PulseMin; } }
        }
        else
        {
            _choicePane.Visible = true; _choiceAlpha = 0; _pulseAlpha = 255;
            for (int k = 0; k < 2; k++) if (_choiceMarks[k] != null) _choiceMarks[k].Visible = k == _choice;
            if (_choiceText != null) _choiceText.Visible = true;
        }
        _choicePane.Modulate = new Color(1f, 1f, 1f, _choiceAlpha / 255f);
        var mark = _choiceMarks[_choice];
        if (mark != null) mark.Modulate = new Color(1f, 1f, 1f, _pulseAlpha / 255f);
        _choiceText?.QueueRedraw();
    }

    private void HideChoice()
    {
        if (_choicePane != null) { _choicePane.Visible = false; _choicePane.Modulate = new Color(1f, 1f, 1f, 0f); }
        if (_choiceText != null) _choiceText.Visible = false;
        _choiceAlpha = 0;
    }

    /// <summary>'slct' as J2DTextBox::draw -> J2DPrint::printReturn lays it: "%s\n%s" in the system font at 20 x 24 (the 20 x 20 cells scaled
    /// 1 x 1.2), bound left / bottom in the 56 x 46 box -- y offset = 46 - round(26 + 24) = -4, the first baseline at -4 + 24 = 20, the second
    /// 26 lower; a glyph's cell spans baseline - ascent x 1.2 .. baseline (drawChar_scale). J2DPrint::parse's advance: a line's first glyph
    /// steps kern + width, every other is drawn kern to the left and steps width; char space -2 after each.</summary>
    private void DrawChoice()
    {
        if (Font?.Page == null || _choiceText == null || _choice < 0) return;
        float sx = ChoiceFontW / Font.Width, sy = ChoiceFontH / Font.CellHeight;
        float y0 = ChoiceTextBoxH - Mathf.Round(ChoiceLineSpace + ChoiceFontH);
        for (int line = 0; line < 2; line++)
        {
            string s = _opt[line] ?? "";
            float baseline = y0 + ChoiceFontH + line * ChoiceLineSpace;
            var col = new Color(1f, 1f, 1f, (line == _choice ? _pulseAlpha : ChoiceDimAlpha) / 255f);
            float x = 0f; bool first = true;
            foreach (char c in s)
            {
                Font.WidthEntry(c, out int kern, out int adv);
                float drawX = first ? x : x - kern * sx;
                var rect = new Rect2(ChoiceTextBox.X + drawX, ChoiceTextBox.Y + baseline - Font.Ascent * sy, Font.CellWidth * sx, Font.CellHeight * sy);
                _choiceText.DrawTextureRectRegion(Font.Page, rect, Font.Cell(c), col);
                x += (first ? adv + kern : adv) * sx + ChoiceCharSpace;
                first = false;
            }
        }
    }

    /// <summary>A page's option tags (setTagParam group 1): &lt;s0=text&gt; / &lt;s1=text&gt; (16 chars kept) or &lt;yesno&gt;; the first one met while
    /// unk214 is -1 becomes the highlighted option and shows its mark.</summary>
    private bool ChoiceTag(string tag)
    {
        if (tag == "yesno") { SetOption(0, "Yes"); SetOption(1, "No"); return true; }
        if (tag.Length >= 3 && tag[0] == 's' && (tag[1] == '0' || tag[1] == '1') && tag[2] == '=') { SetOption(tag[1] - '0', tag.Substring(3)); return true; }
        return false;
    }

    private void SetOption(int i, string s)
    {
        if (_choice == -1) _choice = i;
        _opt[i] = s.Length > ChoiceMaxChars ? s.Substring(0, ChoiceMaxChars) : s;
    }

    /// <summary>closeNormalWindow: the root fades 0x10 a tick; gone, everything resets.</summary>
    private bool CloseNormalWindow()
    {
        _rootAlpha -= RootFadeStep;
        if (_rootAlpha >= 0)
        {
            if (ChoiceVisible) { _choiceAlpha = _rootAlpha; _choicePane.Modulate = new Color(1f, 1f, 1f, _rootAlpha / 255f); }   // unk204->setAlpha(alpha)
            return false;
        }
        _rootAlpha = 0;
        HideAll();
        HideChoice();                                                    // unk204->hide() (eraseNormalWindow leaves it: a choice carries over a page)
        return true;
    }

    /// <summary>eraseNormalWindow: the same fade, then the next page laid out and the root back at 255 for its roll-out (the skip, the fade
    /// step, the colour and the waits reset; the text speed is the message's and stays).</summary>
    private bool EraseNormalWindow()
    {
        _rootAlpha -= RootFadeStep;
        if (_rootAlpha >= 0) return false;
        _rootAlpha = 255;
        HideAll();
        _page++;
        _skip = false; _fadeStep = FadeStep; _latest = 0; _wait = 0;
        SetupPage();
        return true;
    }

    private void HideAll()
    {
        for (int i = 0; i < MaxLines; i++)
        {
            _band[i] = i + 1f; _next[i] = 0;
            if (_bands[i] != null) _bands[i].Visible = false;
            if (_marks[i] != null) _marks[i].Visible = false;
            _bandMats[i]?.SetShaderParameter("roll", 1f);
            if (_g[i] != null) for (int k = 0; k < MaxChars; k++) { _g[i][k].Shown = false; _g[i][k].Alpha = 0; }
        }
        _markAlpha = 0; _pulseAlpha = 255; _pulseUp = false;
    }

    private void ApplyAlpha()
    {
        if (_root != null) _root.Modulate = new Color(1f, 1f, 1f, _rootAlpha / 255f);
        if (_board)
        {
            if (_boardMark != null) _boardMark.Modulate = new Color(1f, 1f, 1f, _boardMarkAlpha / 255f);
            if (_boardArrow != null) _boardArrow.Modulate = new Color(1f, 1f, 1f, _boardArrowAlpha / 255f);
            if (_boardDot != null) _boardDot.Modulate = new Color(1f, 1f, 1f, _boardDotAlpha / 255f);
            _boardText?.QueueRedraw();
        }
    }

    // ------------------------------------------------------------------ the layout (setupTextBox + makeBoxLine)
    /// <summary>The root pane on the viewport: the game's 640 x 448 frame scaled by Size on the window's height and centred; the pane's (5, 5)
    /// base position is the pivot of its -18 deg (J2DPane::makeMatrix: T(pos + pivot) R(-rot) T(-pivot)).</summary>
    private void UpdateRootTransform()
    {
        if (_root == null) return;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float s = vp.Y / SmsHud.ScreenH * Size;
        PlacePane(_root, _rootPos + ParkOffset, _rootRot, s, vp);
        if (_choicePane != null) PlacePane(_choicePane, ChoicePos + ParkOffset, ChoiceRotJ2D, s, vp);   // 'me_4' is never moved: the blo's spot (ParkOffset lifts it with the balloon)
        if (_choiceText != null) PlacePane(_choiceText, ChoicePos + ParkOffset, ChoiceRotJ2D, s, vp);
        if (_boardPane != null && _board) PlacePane(_boardPane, BoardPos + _boundOffset, BoardRotJ2D, s, vp);   // 'mb_0' + the bound pane's offset (not lifted by ParkOffset: the game never moves it)
    }

    /// <summary>A 10 x 10 J2D pane on the viewport: park = its top-left on the frame, the (5, 5) base position the pivot of its rotation.</summary>
    private static void PlacePane(Control pane, Vector2 park, float rotJ2D, float s, Vector2 vp)
    {
        pane.Scale = new Vector2(s, s);
        pane.PivotOffset = RootPivot;
        Vector2 pivotOnScreen = new(SmsHud.CenterX(park.X + RootPivot.X, s, vp), (park.Y + RootPivot.Y) * s);
        pane.Position = pivotOnScreen - RootPivot;      // Godot puts the pivot at Position + PivotOffset (unscaled)
        pane.Rotation = -Mathf.DegToRad(rotJ2D);
    }

    public override void _Process(double delta)
    {
        if (IsOpen) UpdateRootTransform();
    }

    /// <summary>setupTextBox for the current page: each line's glyph boxes along the curve, the colour tags, the speed tag (unk280 -> unk281 of the
    /// glyph before each). unk26A: the last page.</summary>
    private void SetupPage()
    {
        _lastPage = _page >= _pages.Count - 1;
        string[] lines = _page < _pages.Count ? _pages[_page] : new[] { "" };
        _lines = 0;
        for (int i = 0; i < MaxLines; i++) { _last[i] = -1; _next[i] = 0; _band[i] = i + 1f; }
        Color col = ColorTable[0];                                           // unk27C = -1 at every page
        var flatWait = new List<(int line, int idx)>();
        var settingAt = new List<int>();
        for (int li = 0; li < Math.Min(MaxLines, lines.Length); li++)
        {
            var chars = new List<(char c, Color col, int wait)>();
            string text = lines[li];
            for (int p = 0; p < text.Length; p++)
            {
                char c = text[p];
                if (c == '<')
                {
                    int e = text.IndexOf('>', p);
                    if (e > p)
                    {
                        string tag = text.Substring(p + 1, e - p - 1);
                        if (tag.Length == 2 && tag[0] == 'c' && char.IsDigit(tag[1])) { col = ColorTable[Math.Clamp(tag[1] - '0', 0, 5)]; p = e; continue; }
                        if (tag.StartsWith("w=") && int.TryParse(tag.AsSpan(2), out int w)) { _textWait = Math.Max(0, w); p = e; continue; }
                        if (ChoiceTag(tag)) { p = e; continue; }
                    }
                }
                chars.Add((c, col, _textWait));
                if (chars.Count >= MaxChars) break;
            }
            if (chars.Count > 0) _lines = li;
            MakeBoxLine(li, chars);
            for (int k = 0; k <= _last[li]; k++) { flatWait.Add((li, k)); settingAt.Add(chars[k].wait); }
        }
        // unk281[k] = the speed setting read while laying out glyph k + 1 (unk281[unk2DE] = unk280 at each glyph): what showing k arms
        for (int j = 0; j < flatWait.Count; j++)
        {
            var (line, idx) = flatWait[j];
            _g[line][idx].WaitAfter = settingAt[Math.Min(j + 1, settingAt.Count - 1)];
        }
        _wait = 0;
    }

    /// <summary>makeBoxLine: the glyphs of one line laid along the curve. w advances 1 / curve length x (0.7 x the glyph's width + 4) a glyph;
    /// the box sits at the curve point of the glyph's START rotated about the pane origin by the tangent's angle (the game's own arithmetic,
    /// kept: pos = R(ang) p, box = (round(pos.x), -37 - round(pos.y))), turned by that angle about its centre. The glyph laid past w = 1.1 is
    /// the line's last (the game truncates there; AutoWrap keeps lines inside it).</summary>
    private void MakeBoxLine(int line, List<(char c, Color col, int wait)> chars)
    {
        float w = 0f;
        Vector2 pos = Curve(w);
        int idx = 0;
        _last[line] = -1;
        foreach (var (c, col, wait) in chars)
        {
            if (idx >= MaxChars) break;
            Font.WidthEntry(c, out _, out int width);
            w += _invCurveLen * (AdvanceScale * width + AdvancePad);
            Vector2 cur = Curve(w);
            float ang = MathF.Abs(MathF.Atan2(cur.Y - pos.Y, cur.X - pos.X));
            if (cur.X > 0f) ang = -ang;
            Vector2 rotated = new(pos.X * MathF.Cos(ang) - pos.Y * MathF.Sin(ang), pos.X * MathF.Sin(ang) + pos.Y * MathF.Cos(ang));
            int ix = RoundHalfAway(rotated.X), iy = RoundHalfAway(rotated.Y);
            ref Glyph g = ref _g[line][idx];
            g.C = c; g.Box = new Vector2(ix, GlyphBoxYBase - iy); g.RotJ2D = Mathf.RadToDeg(ang); g.Col = col; g.WaitAfter = 0;
            g.Shown = false; g.Alpha = 0;
            _last[line] = idx;
            if (w > OverflowW) break;
            idx++;
            pos = Curve(w);
        }
    }

    private static int RoundHalfAway(float v) => (int)(v + (v > 0f ? 0.5f : -0.5f));

    /// <summary>The NPC's Lines -> pages of up to three lines each: '\n' breaks, AutoWrap breaks by words where the curve ends.</summary>
    private List<string[]> LayoutPages(string[] pages)
    {
        var out_ = new List<string[]>();
        if (pages == null || pages.Length == 0) pages = new[] { "..." };
        foreach (string page in pages)
        {
            var lines = new List<string>();
            foreach (string raw in (page ?? "").Replace("\r", "").Split('\n'))
            {
                if (!AutoWrap || Fits(raw)) { lines.Add(raw); continue; }
                string cur = "";
                foreach (string word in raw.Split(' '))
                {
                    string trial = cur.Length == 0 ? word : cur + " " + word;
                    if (Fits(trial) || cur.Length == 0) cur = trial;
                    else { lines.Add(cur); cur = word; }
                }
                if (cur.Length > 0) lines.Add(cur);
            }
            for (int i = 0; i < lines.Count; i += MaxLines)
                out_.Add(lines.GetRange(i, Math.Min(MaxLines, lines.Count - i)).ToArray());
        }
        if (out_.Count == 0) out_.Add(new[] { "" });
        return out_;
    }

    /// <summary>Would makeBoxLine lay every glyph of the line? A glyph is laid only while the w before it is &lt;= 1.1 and fewer than 30 are down.</summary>
    private bool Fits(string text)
    {
        float w = 0f; int n = 0;
        for (int p = 0; p < text.Length; p++)
        {
            char c = text[p];
            if (c == '<') { int e = text.IndexOf('>', p); if (e > p) { p = e; continue; } }
            if (n >= MaxChars || w > OverflowW) return false;
            Font.WidthEntry(c, out _, out int width);
            w += _invCurveLen * (AdvanceScale * width + AdvancePad);
            n++;
        }
        return true;
    }

    // ------------------------------------------------------------------ the glyph quads (J2DTextBox::drawSelf -> drawChar_scale)
    private void DrawGlyphs()
    {
        if (Font?.Page == null || _glyphs == null) return;
        float cw = Font.CellWidth, ch = Font.CellHeight;
        for (int line = 0; line <= _lines && line < MaxLines; line++)
        {
            float ly = (line - 1) * LineSpacing;
            for (int k = 0; k <= _last[line] && k < MaxChars; k++)
            {
                ref Glyph g = ref _g[line][k];
                if (!g.Shown || g.Alpha <= 0) continue;
                Vector2 centre = new(g.Box.X + cw * 0.5f, ly + g.Box.Y + ch * 0.5f);
                _glyphs.DrawSetTransform(centre, -Mathf.DegToRad(g.RotJ2D), Vector2.One);
                _glyphs.DrawTextureRectRegion(Font.Page, new Rect2(-cw * 0.5f, -ch * 0.5f, cw, ch), Font.Cell(g.C), new Color(g.Col, g.Alpha / 255f));
            }
        }
        _glyphs.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
