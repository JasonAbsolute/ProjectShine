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
/// tag; a new message resets it). The choice window ('me_4', the game's scripted yes / no) and the board window are not ported.</summary>
public partial class SmsTalkBalloon : CanvasLayer
{
    public enum TalkState { Closed = 0, WaitCamera = 2, Pause = 3, Opening = 4, Open = 5, Closing = 6, Erasing = 7 }

    [Export] public SmsTalkFont Font;
    /// <summary>The frame's 448 lines on the window's height, times this (1 = the game's size; the console's own Size knob is separate).</summary>
    [Export] public float Size = 1f;
    [Export] public bool AutoWrap = true;                   // DEVIATION: the game's lines are pre-broken in the message data
    [Export] public string AdvanceActionA = "button_a", AdvanceActionB = "button_b";
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
    public bool PageComplete => State == TalkState.Open && _marks[_lines] != null && _marks[_lines].Visible;
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
    private AudioStreamPlayer _sfxObi, _sfxSelect;
    private bool _nodesReady;

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
        if (_glyphs != null) _glyphs.Draw += DrawGlyphs;
        _invCurveLen = 1f / CurveLength();
        HideAll();
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
        _pages = LayoutPages(pages);
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
        _tickAccum = 0f; _frameAccum = 0f;
        SetupPage();                                                        // setMessageID's setupTextBox: the layout is ready before the roll-out
        State = TalkState.WaitCamera;
        UpdateRootTransform();
        Visible = true;
        if (DebugPrint) GD.Print($"[SMS talk2d] open on {npc?.Name}: {_pages.Count} page(s), {(talkB ? "TALK_B" : "TALK_A")} park ({_rootPos.X}, {_rootPos.Y}) rot {_rootRot}");
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
        int n = SmsUnits.TicksFor(delta, ref _tickAccum);
        _frameAccum += (float)delta * 30f;              // CUE_CALC_ANIM: once a 30 fps frame
        bool frameEdge = false;
        if (_frameAccum >= 1f) { _frameAccum -= 1f; frameEdge = true; }
        for (int i = 0; i < n; i++)
        {
            Tick(i == n - 1 && frameEdge);
            _edgeA = _edgeB = false;
            if (!IsOpen) break;
        }
        if (n == 0 && frameEdge && IsOpen) CalcAnim();
        _glyphs?.QueueRedraw();
    }

    private void Tick(bool frameLast)
    {
        switch (State)
        {
            case TalkState.WaitCamera:
                if (_camera == null || _camera.TalkCameraSettled) { _pauseFrames = PauseFrames; State = TalkState.Pause; }
                break;
            case TalkState.Opening:
                if (OpenNormalWindow()) State = TalkState.Open;
                break;
            case TalkState.Open:
                MoveTalkWindow();
                CheckControler();
                break;
            case TalkState.Closing:
                if (CloseNormalWindow()) { State = TalkState.Closed; Finish(); return; }
                break;
            case TalkState.Erasing:
                if (EraseNormalWindow()) State = TalkState.Opening;
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
                if (EraseNormalWindow()) State = TalkState.Opening;
                break;
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
        Control group = _marks[_lines];
        if (group == null || !group.Visible) return;
        if (!(_edgeA || _edgeB)) return;
        if (_lastPage) CloseTalkWindow();                              // MSD_SE_SY_SELECT_COMMON + closeTalkWindow
        else { _sfxSelect?.Play(); State = TalkState.Erasing; }
    }

    /// <summary>closeNormalWindow: the root fades 0x10 a tick; gone, everything resets.</summary>
    private bool CloseNormalWindow()
    {
        _rootAlpha -= RootFadeStep;
        if (_rootAlpha >= 0) return false;
        _rootAlpha = 0;
        HideAll();
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
    }

    // ------------------------------------------------------------------ the layout (setupTextBox + makeBoxLine)
    /// <summary>The root pane on the viewport: the game's 640 x 448 frame scaled by Size on the window's height and centred; the pane's (5, 5)
    /// base position is the pivot of its -18 deg (J2DPane::makeMatrix: T(pos + pivot) R(-rot) T(-pivot)).</summary>
    private void UpdateRootTransform()
    {
        if (_root == null) return;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float s = vp.Y / SmsHud.ScreenH * Size;
        _root.Scale = new Vector2(s, s);
        _root.PivotOffset = RootPivot;
        Vector2 pivotOnScreen = new(SmsHud.CenterX(_rootPos.X + RootPivot.X, s, vp), (_rootPos.Y + RootPivot.Y) * s);
        _root.Position = pivotOnScreen - RootPivot;      // Godot puts the pivot at Position + PivotOffset (unscaled)
        _root.Rotation = -Mathf.DegToRad(_rootRot);
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
