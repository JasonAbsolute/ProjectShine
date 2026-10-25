using Godot;
using System.Collections.Generic;

/// <summary>
/// The card save -- TCardSave (GC2D/CardSave.cpp) on save.blo (game_6.szs) with savemessage.bmg's strings: the "Save?" windows the director
/// runs in STATE_CARD_SAVE, the game stopped (MarDirectorDirect.cpp 770: init(purpose), the pads' MENU_INPUT; 378: getNextState -- 0 the game
/// goes on, 1 the title). Three of its purposes are here:
///  * PURPOSE_SHINE_GET (6): fireGetStar (854) sets CARD_SAVE_PENDING with the shine; when the demo ends (866) the dialog opens over the faded
///    screen -- 'w_5' (373 x 300), the heading 'sm6a', the file's Mario-text + letter ('t_4', 't_5'..'t_7'), the saved shine count ('f_sj'
///    icon x 2 digits / 'f_sk' 3 digits; 'm_8a' "New" for a file never saved) and three options 'sm3a'..'sm5a' (bmg 28 "Save and Continue",
///    30 "Save and Quit", 29 "Continue") with the pointer hands 'sc3a'..'sc5a' (cursor.bti 29 x 41 turned 270, a black twin at alpha 125 four
///    px down-right) -- waitForSelect3 (1447): A = SELECT_COMMON and the close, up / down move the hand (E3_MENU_CURSOR), no wrap. 0 / 1 save
///    (1 quits after), 2 just closes (PROGRESS_UNK1).
///  * PURPOSE_BLUE_COIN_GET (1): fireGetBlueCoin (MarDirectorEvent.cpp 107) -- 'w_4' (373 x 239), bmg 36 in 'sm0a', two options 'sm1a' /
///    'sm2a' (28 / 29), hands 'sc1a' / 'sc2a' -- waitForSelect2 (1321). Every blue coin, as the game.
///  * PURPOSE_SAVE (0): the pause menu's SAVE -- 'w_1' (402 x 202): bmg 9 "Save?" in the yellow 'm_4a', the file bookmark ('t_0' + letter,
///    'f_sh' / 'f_si' count or 'm_3a' "New"), YES 's_6' / NO 's_7' -- waitForChoiceBM (788): left / right pick, the pick green (0,255,0) the
///    other white, and from its 4th frame the pick breathes: 1.5 x over 40 frames and back over 40 (TExPane::setCenteredSize) while its
///    colour runs (t, 255, 0) with t 0 -> 255 -> 0 over 80 (green to yellow), the cycle restarting on a move; A = SELECT_COMMON (yes) /
///    CANCEL_COMMON (no). Yes saves; then "Save complete.\nContinue playing?" (bmg 35) with the live count: YES closes the pause menu (UNK1
///    -> TPauseMenu2::setDrawEnd), NO quits (UNK19). No at "Save?" also closes the menu (UNK1).
/// Every window opens as TExPane::setCenteredSize(20, w, h, 0, 0): 20 frames of linear growth about its centre (TCoord2D, (target -
/// start) / 20 a frame), its contents hidden until open, SY_2D_IN at the end of the growth; it closes the same way shrinking, SY_2D_OUT at the
/// start (2372). THE SAVING (2123): the write at once (saveSuccess), the 'w_1' window with bmg 26 "Saving file..." and the bookmark (the SAVED
/// count, or "New") for 300 frames: at frame 200 SY_DECIDE_COMMON and the count becomes the live MSF_SHINE_COUNT ("New" goes); past 300 the
/// option block is written and the window shrinks (endWaitForChoice). The blue coin's saving shows the Mario-text and letter but no count.
/// The frame: standard_window.bti at the four corners (mirrored), the window colour 00bbb988 (tools/projectshine/npc82/save124/
/// gen_save_assets.py -> save/standard_window.png, a NinePatch with 20 px margins). The texts: <see cref="SmsTextBox"/> with the system font
/// (TalkFont.tres), 20 x 24, line space 20, each with its black shadow box.
/// DEVIATIONS (logged): the window's own mask never shows (the game shows it for GAME_OVER / ALL_SHINES / EXPLANATION only, none here);
/// no memory-card states (no card, format, corrupt -- the file is user://save_data.json); the YES / NO sparks (emitter 0x1FA) are not ported;
/// <see cref="HeadingMessage"/>: the game's shine-get heading is savemessage.bmg entry 0x21 (CardSave.cpp 1460) -- on the US disc that
/// entry reads "File created." (the Japanese order's "SHINE! Save data?" moved to 41 and the index was never updated); 33 = as shipped,
/// 41 = the intended line. One save file: <see cref="FileLetter"/> 0 = "Mario A". 124b (Jaysonn's playtest, SaveRef.png): <see cref="WindowColor"/>
/// (the frame split into a tinted body and the white rim; his default darker and denser than the game's 00bbb9 at 0x88), <see cref="CenterContents"/>
/// (everything centred on the window, the hand following each option -- the blo is left-weighted), <see cref="DigitPitch"/> and <see cref="CountShown"/>.
/// </summary>
[GlobalClass]
public partial class SmsCardSave : CanvasLayer
{
    public enum Purpose { Save = 0, BlueCoinGet = 1, ShineGet = 6 }
    /// <summary>getNextState: 0 = the game goes on, 1 = the title (Save and Quit / "Continue playing?" No).</summary>
    [Signal] public delegate void FinishedEventHandler(int next);

    public static SmsCardSave Current { get; private set; }

    // ------------------------------------------------------------------------------------------------------------------------ the look
    [Export] public SmsTalkFont Font;
    /// <summary>TalkGlyph.gdshader: the x4 sheet sharpened to a one-pixel edge (pass 87); empty = plain bilinear.</summary>
    [Export] public Shader GlyphShader;
    [Export] public Texture2D WindowTexture;            // save/standard_window_body.png (NinePatch, margins 20): the body, tinted by WindowColor
    /// <summary>save/standard_window_rim.png: the white inner line and the corner dots, drawn over the body untinted.</summary>
    [Export] public Texture2D WindowRimTexture;
    /// <summary>The window colour (J2DWindow's contents 00bbb988 and the frame's "black" -- the game's teal at alpha 0x88 = (0, 0.733, 0.725, 0.533)).
    /// 124b, Jaysonn: "the blue should be darker ... make it so I can set it to any color" -- his default a darker teal, denser.</summary>
    [Export] public Color WindowColor { get => _windowColor; set { _windowColor = value; ApplyWindowColor(); } }
    private Color _windowColor = new Color(0f, 0.5f, 0.5f, 0.85f);
    /// <summary>124b (Jaysonn: "have everything middle aligned"), a DEVIATION: the heading, the file name, the count and the options are
    /// centred on the window and the hand follows each option's left edge at the blo's distance; off = the blo's left-weighted layout.</summary>
    [Export] public bool CenterContents = true;
    /// <summary>The count's digit pitch in blo px (the blo's panes sit 17 apart with 25-wide cells -- the game's tight look; the HUD's ratio is 27 / 36).</summary>
    [Export(PropertyHint.Range, "12,30")] public float DigitPitch = 17f;
    public enum CountMode { Saved, Live }
    /// <summary>What the windows show before the write: Saved = the game (the file's count, TCardBookmarkInfo; the saving window ticks it to
    /// the live one at frame 200), Live = the count with this pickup already in it.</summary>
    [Export] public CountMode CountShown = CountMode.Saved;
    [Export] public Texture2D CursorTexture;            // save/cursor.png: the pointer hand
    [Export] public Texture2D MarioTexture;             // save/file_name_mario.png
    [Export] public Texture2D[] LetterTextures = new Texture2D[3];   // save/file_name_a/b/c.png
    [Export] public Texture2D YesTexture, NoTexture;    // save/select_yes.png / select_no.png
    [Export] public Texture2D ShineIcon;                // hud/shine_icon.png
    [Export] public Texture2D TimesTexture;             // hud/coin_number_x.png
    [Export] public Texture2D[] DigitTextures = new Texture2D[10];   // hud/coin_number_0..9.png (the blo's coin_number_%d.bti)
    /// <summary>TApplication::mSaveFile: the file the bookmark names -- 0 Mario A, 1 B, 2 C.</summary>
    [Export(PropertyHint.Range, "0,2")] public int FileLetter = 0;
    /// <summary>Where "Save and Quit" / "Continue playing? -- No" go (the game's title screen).</summary>
    [Export(PropertyHint.File, "*.tscn")] public string TitleScene = "res://assets/CharacterSelect.tscn";
    /// <summary>1 = the blo's 480 lines on the window's height.</summary>
    [Export] public float Size = 1.0f;
    /// <summary>The shine-get window's heading, a savemessage.bmg entry: 33 = the game's 0x21 as the US disc reads it ("File created."),
    /// 41 = "SHINE!\nSave data?".</summary>
    [Export] public int HeadingMessage = 33;
    /// <summary>The saving window's frames: the DECIDE + the live count at <see cref="SavingDecideFrame"/>, the close past <see cref="SavingFrames"/>
    /// (the game: 200 / 300 at 60 Hz).</summary>
    [Export] public int SavingFrames = 300, SavingDecideFrame = 200;
    /// <summary>The stick as a menu button (JUTGamePad CStick::getButton at 0.5; his 0.3 / 0.15 as the pause menu's).</summary>
    [Export(PropertyHint.Range, "0.05,1")] public float MenuStickOn = 0.3f;
    [Export(PropertyHint.Range, "0,1")] public float MenuStickOff = 0.15f;
    /// <summary>MSD_SE_SY_2D_IN 0x4819 / 2D_OUT 0x481A / DECIDE_COMMON 0x481B / SELECT_COMMON 0x481C / CANCEL_COMMON 0x481D / E3_MENU_CURSOR 0x481E.</summary>
    [Export] public AudioStreamPlayer WindowIn, WindowOut, Decide, Select, Cancel, CursorMove;
    [Export] public bool DebugPrint = false;

    // ------------------------------------------------------------------------------------------------------------------- the strings
    /// <summary>savemessage.bmg (/data/common.szs 2d/, the US disc), the entries the three purposes read.</summary>
    public static readonly Dictionary<int, string> Messages = new()
    {
        [9] = "Save?",
        [25] = "New",
        [26] = "Saving file...\nPlease do not touch\nthe Memory Card or the\nPOWER Button.",
        [28] = "Save and Continue",
        [29] = "Continue",
        [30] = "Save and Quit",
        [33] = "File created.",
        [35] = "Save complete.\nContinue playing?",
        [36] = "You got a Blue Coin!\n\nCollect ten of them\nthen visit the boathouse\nin Delfino Plaza.",
        [41] = "SHINE!\nSave data?",
    };
    public static string Message(int id) => Messages.TryGetValue(id, out string s) ? s : "";

    // ----------------------------------------------------------------------------------------------------------------- the machine
    private enum Progress { None, Begin, Confirm, Event, ShineGetSel, Saving, SaveDone, Done, Quit }   // UNK0/2, UNK15, UNK2A, UNK2B, SAVING, SAVE_DONE, UNK1, UNK19
    private enum Step { Open, Opening, Shown, Close, Closing, Done }                                   // WindowStep

    private Purpose _purpose;
    private Progress _progress = Progress.None;
    private Step _step = Step.Open;
    private int _sel;                 // unk2E9: the cursor
    private int _frame;               // unk2E0: the frames in the state
    private int _pulse;               // unk2FC: the YES / NO breathing clock
    private bool _wrote;              // unk2DC gone: the file written this saving
    private bool _optionWritten;      // unk2DD
    private bool _quit;               // unk2DE: quit after the save
    private bool _savedExists; private int _savedCount;   // the bookmark: TCardBookmarkInfo unk18 / unk1C (the file as on disk)
    private bool _pausedTree;
    private float _acc;
    private int _stickV, _stickH;
    private readonly Dictionary<string, bool> _held = new();
    private int _log;

    // ---------------------------------------------------------------------------------------------------------------------- the panes
    private Control _root;
    private ShaderMaterial _glyphMat;
    private Win _confirm, _event, _shine;   // 'w_1', 'w_4', 'w_5'

    /// <summary>A WIN1 pane with TExPane's size animation (setCenteredSize + update: linear over N frames, about the initial bounds' centre).</summary>
    private sealed class Win
    {
        public NinePatchRect Frame, Rim; public Rect2 Initial;
        public Vector2 Cur, Target, Vel; public bool Animating;
        public readonly List<Control> Contents = new();
        public SmsTextBox Msg, NewText; public TextureRect MarioText; public TextureRect[] Letters = new TextureRect[3];
        public Control CountUnder, CountOver; public TextureRect[] UnderDigits = new TextureRect[2], OverDigits = new TextureRect[3];
        public SmsTextBox[] Options = System.Array.Empty<SmsTextBox>(); public TextureRect[] Hands = System.Array.Empty<TextureRect>(), HandShadows = System.Array.Empty<TextureRect>();
        public TextureRect Yes, No; public Rect2 YesInitial, NoInitial;
        public Vector2 YesCur, YesTarget, YesVel, NoCur, NoTarget, NoVel; public bool YesAnim, NoAnim;

        public void SetCenteredSize(int frames, float toW, float toH, float fromW, float fromH)
        {
            Cur = new Vector2(fromW, fromH); Target = new Vector2(toW, toH);
            Vel = frames > 0 ? (Target - Cur) / frames : Vector2.Zero;
            Animating = true;
            Apply();
        }
        /// <summary>TExPane::update: one step; true once the size has reached its target (isThing2).</summary>
        public bool Update()
        {
            if (Animating)
            {
                Cur = Chase(Cur, Target, Vel);
                if (Cur == Target) Animating = false;
                Apply();
            }
            return !Animating;
        }
        private void Apply()
        {
            Frame.Size = new Vector2(Mathf.Round(Cur.X), Mathf.Round(Cur.Y));
            Frame.Position = Initial.Position + (Initial.Size - Frame.Size) * 0.5f;
            if (Rim != null) Rim.Size = Frame.Size;
        }
        public void HideContents() { foreach (Control c in Contents) c.Visible = false; }

        /// <summary>The YES / NO panes' own setCenteredSize (the breathing).</summary>
        public void PickSize(bool yes, int frames, float toScale, float fromScale)
        {
            Rect2 init = yes ? YesInitial : NoInitial;
            Vector2 from = init.Size * fromScale, to = init.Size * toScale;
            if (yes) { YesCur = from; YesTarget = to; YesVel = frames > 0 ? (to - from) / frames : Vector2.Zero; YesAnim = true; ApplyPick(true); }
            else { NoCur = from; NoTarget = to; NoVel = frames > 0 ? (to - from) / frames : Vector2.Zero; NoAnim = true; ApplyPick(false); }
        }
        public void PickUpdate(bool yes)
        {
            if (yes) { if (!YesAnim) return; YesCur = Chase(YesCur, YesTarget, YesVel); if (YesCur == YesTarget) YesAnim = false; ApplyPick(true); }
            else { if (!NoAnim) return; NoCur = Chase(NoCur, NoTarget, NoVel); if (NoCur == NoTarget) NoAnim = false; ApplyPick(false); }
        }
        private void ApplyPick(bool yes)
        {
            TextureRect p = yes ? Yes : No; Rect2 init = yes ? YesInitial : NoInitial; Vector2 cur = yes ? YesCur : NoCur;
            p.Size = new Vector2(Mathf.Round(cur.X), Mathf.Round(cur.Y));
            p.Position = init.Position + (init.Size - p.Size) * 0.5f;
        }
        /// <summary>CLBChaseGeneralConstantSpecifySpeed on both axes.</summary>
        private static Vector2 Chase(Vector2 cur, Vector2 target, Vector2 vel)
        {
            for (int i = 0; i < 2; i++)
            {
                float c = cur[i], t = target[i], v = vel[i];
                if (c == t) continue;
                float n = c + v;
                if ((v >= 0f && n >= t) || (v < 0f && n <= t) || v == 0f) n = t;
                cur[i] = n;
            }
            return cur;
        }
    }

    public bool IsOpen => _progress != Progress.None;
    /// <summary>For the drives: the window step and the cursor.</summary>
    public string DebugState => $"{_purpose}/{_progress}/{_step} sel {_sel} frame {_frame} pulse {_pulse}";
    public int Selected => _sel;
    public bool SavingWindowShowsLiveCount => _progress == Progress.Saving && _frame > SavingDecideFrame;
    /// <summary>For the drives: which frame is up and its size, the heading, the hands and the count shown.</summary>
    public string DebugPanes()
    {
        string W(Win w, string n) => w.Frame.Visible ? $"{n} {w.Frame.Size.X:F0}x{w.Frame.Size.Y:F0} msg '{(w.Msg.Visible ? w.Msg.Text.Replace("\n", "/") : "")}'" : $"{n} hidden";
        int hands = 0; foreach (Win w in new[] { _event, _shine }) foreach (TextureRect h in w.Hands) if (h.Visible) hands++;
        string count = _confirm.CountUnder.Visible ? "confirm:under" : _confirm.CountOver.Visible ? "confirm:over" : _confirm.NewText.Visible ? "confirm:NEW" : _shine.CountUnder.Visible ? "shine:under" : _shine.CountOver.Visible ? "shine:over" : _shine.NewText.Visible ? "shine:NEW" : "none";
        return $"{W(_confirm, "w_1")}; {W(_event, "w_4")}; {W(_shine, "w_5")}; hands {hands}; count {count}; yes {(_confirm.Yes.Visible ? _confirm.Yes.Modulate.ToHtml(false) + " " + _confirm.Yes.Size.X.ToString("F0") : "-")}";
    }

    public override void _Ready()
    {
        Current = this;
        ProcessMode = ProcessModeEnum.Always;
        if (GlyphShader != null) _glyphMat = new ShaderMaterial { Shader = GlyphShader };
        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _root.Size = new Vector2(600f, 480f);
        AddChild(_root);
        BuildWindows();
        Layout();
        GetViewport().SizeChanged += Layout;
    }

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
        if (_pausedTree && GetTree() != null) GetTree().Paused = false;
    }

    private void Layout()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float s = vp.Y / 480f * Size;
        _root.Scale = new Vector2(s, s);
        _root.Position = vp * 0.5f - new Vector2(300f, 240f) * s;
    }

    // ------------------------------------------------------------------------------------------------------------- building the panes
    private SmsTextBox TextBox(Control parent, string name, Vector2 bPos, Vector2 size, int hbind, int vbind, Color? color = null)
    {
        // the blo's pair: the shadow box 'xb' at bPos, the text box 'xa' its child at (-3, -3); here one node at the text's place drawing both
        var t = new SmsTextBox { Name = name, Font = Font, Position = bPos - new Vector2(3f, 3f), Size = size, HBind = hbind, VBind = vbind, Shadow = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (color.HasValue) t.TextColor = color.Value;
        if (_glyphMat != null) t.Material = _glyphMat;
        parent.AddChild(t);
        return t;
    }

    private TextureRect Pic(Control parent, string name, Texture2D tex, Vector2 pos, Vector2 size, float rotDeg = 0f, Color? modulate = null)
    {
        var p = new TextureRect { Name = name, Texture = tex, Position = pos, Size = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (rotDeg != 0f) { p.PivotOffset = size * 0.5f; p.Rotation = -Mathf.DegToRad(rotDeg); }   // J2D's ROT about the pane's centre, the screen's other sense
        if (modulate.HasValue) p.Modulate = modulate.Value;
        parent.AddChild(p);
        return p;
    }

    private Win Frame(string name, Vector2 pos, Vector2 size)
    {
        var w = new Win();
        w.Frame = new NinePatchRect { Name = name, Texture = WindowTexture, Position = pos, Size = size, PatchMarginLeft = 20, PatchMarginTop = 20, PatchMarginRight = 20, PatchMarginBottom = 20, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        w.Frame.SelfModulate = WindowColor;   // the body only: the rim and the contents keep their own colours
        w.Initial = new Rect2(pos, size);
        _root.AddChild(w.Frame);
        if (WindowRimTexture != null)
        {
            w.Rim = new NinePatchRect { Name = "Rim", Texture = WindowRimTexture, Position = Vector2.Zero, Size = size, PatchMarginLeft = 20, PatchMarginTop = 20, PatchMarginRight = 20, PatchMarginBottom = 20, MouseFilter = Control.MouseFilterEnum.Ignore };
            w.Frame.AddChild(w.Rim);          // the first child: under the contents
        }
        return w;
    }

    /// <summary>Live: the colour knob on the three frames (the editor's inspector or a script at run time).</summary>
    public void ApplyWindowColor()
    {
        foreach (Win w in new[] { _confirm, _event, _shine }) if (w?.Frame != null) w.Frame.SelfModulate = WindowColor;
    }

    /// <summary>The file bookmark: the Mario-text, the three letters (one shown), the "New" box and the two count groups (icon, x, digits).</summary>
    private void Bookmark(Win w, Vector2 marioPos, Vector2 newBPos, Vector2 underPos, Vector2 overPos)
    {
        w.MarioText = Pic(w.Frame, "MarioText", MarioTexture, marioPos, new Vector2(90f, 27f)); w.Contents.Add(w.MarioText);
        for (int i = 0; i < 3; i++)
        {
            Vector2 off = i == 0 ? new Vector2(89f, -4f) : new Vector2(89f, -7f), sz = i == 0 ? new Vector2(31f, 31f) : new Vector2(31f, 34f);
            w.Letters[i] = Pic(w.Frame, $"Letter{i}", LetterTextures != null && i < LetterTextures.Length ? LetterTextures[i] : null, marioPos + off, sz);
            w.Contents.Add(w.Letters[i]);
        }
        w.NewText = TextBox(w.Frame, "NewText", newBPos, new Vector2(130f, 24f), 0, 0); w.Contents.Add(w.NewText);
        // the blo's digit panes: x at +31, the digits at +50 / +67 (two) and +50 / +67 / +87 (three); DigitPitch scales the digit steps
        float p1 = DigitPitch, p2 = DigitPitch * 37f / 17f;
        w.CountUnder = new Control { Name = "CountUnder", Position = underPos, Size = new Vector2(50f + p1 + 25f, 41f), MouseFilter = Control.MouseFilterEnum.Ignore }; w.Frame.AddChild(w.CountUnder); w.Contents.Add(w.CountUnder);
        Pic(w.CountUnder, "Icon", ShineIcon, Vector2.Zero, new Vector2(35f, 41f));
        Pic(w.CountUnder, "Times", TimesTexture, new Vector2(31f, 7f), new Vector2(25f, 25f));
        w.UnderDigits[0] = Pic(w.CountUnder, "Tens", null, new Vector2(50f, 7f), new Vector2(25f, 25f));
        w.UnderDigits[1] = Pic(w.CountUnder, "Ones", null, new Vector2(50f + p1, 7f), new Vector2(25f, 25f));
        w.CountOver = new Control { Name = "CountOver", Position = overPos, Size = new Vector2(50f + p2 + 25f, 41f), MouseFilter = Control.MouseFilterEnum.Ignore }; w.Frame.AddChild(w.CountOver); w.Contents.Add(w.CountOver);
        Pic(w.CountOver, "Icon", ShineIcon, Vector2.Zero, new Vector2(35f, 41f));
        Pic(w.CountOver, "Times", TimesTexture, new Vector2(31f, 7f), new Vector2(25f, 25f));
        w.OverDigits[0] = Pic(w.CountOver, "Hundreds", null, new Vector2(50f, 7f), new Vector2(25f, 25f));
        w.OverDigits[1] = Pic(w.CountOver, "Tens", null, new Vector2(50f + p1, 7f), new Vector2(25f, 25f));
        w.OverDigits[2] = Pic(w.CountOver, "Ones", null, new Vector2(50f + p2, 7f), new Vector2(25f, 25f));
    }

    /// <summary>124b, CenterContents: the heading / message box, the file name group (90 + the letter's 30), the "New" box, the count groups and
    /// the option boxes centred on the window's width; the hands follow each option's text at the blo's distance (text left 97 -> hand centre 63.5).</summary>
    private void CenterWindow(Win w)
    {
        float W = w.Initial.Size.X, c = W * 0.5f;
        if (w.Msg != null) w.Msg.Position = new Vector2(c - w.Msg.Size.X * 0.5f - 3f, w.Msg.Position.Y);
        if (w.MarioText != null)
        {
            float x = Mathf.Round(c - 60f);
            w.MarioText.Position = new Vector2(x, w.MarioText.Position.Y);
            for (int i = 0; i < 3; i++) if (w.Letters[i] != null) w.Letters[i].Position = new Vector2(x + 89f, w.Letters[i].Position.Y);
        }
        if (w.NewText != null) w.NewText.Position = new Vector2(c - 65f - 3f, w.NewText.Position.Y);
        if (w.CountUnder != null) w.CountUnder.Position = new Vector2(Mathf.Round(c - w.CountUnder.Size.X * 0.5f), w.CountUnder.Position.Y);
        if (w.CountOver != null) w.CountOver.Position = new Vector2(Mathf.Round(c - w.CountOver.Size.X * 0.5f), w.CountOver.Position.Y);
        for (int i = 0; i < w.Options.Length; i++)
        {
            SmsTextBox o = w.Options[i];
            o.HBind = 0;
            o.Position = new Vector2(c - 125f - 3f, o.Position.Y);
            int lineW = (int)(o.LineWidth(o.Text) + 0.5f);
            float textLeft = o.Position.X + (250 - lineW) / 2;             // J2DPrint's integer centring inside the 250 box
            float handCentre = textLeft - 33.5f;
            w.Hands[i].Position = new Vector2(handCentre - 14.5f, w.Hands[i].Position.Y);
            w.HandShadows[i].Position = new Vector2(handCentre - 13.5f, w.HandShadows[i].Position.Y);
        }
    }

    /// <summary>The option list: the texts (left-bound 250 x 24) and the hands ('scNa' 29 x 41 turned 270 at (49, y - 5); the shadow 'scNb' at (50, y - 1), black, alpha 125).</summary>
    private void OptionList(Win w, float[] ys)
    {
        w.Options = new SmsTextBox[ys.Length]; w.Hands = new TextureRect[ys.Length]; w.HandShadows = new TextureRect[ys.Length];
        for (int i = 0; i < ys.Length; i++)
        {
            w.HandShadows[i] = Pic(w.Frame, $"HandShadow{i}", CursorTexture, new Vector2(50f, ys[i] - 1f), new Vector2(29f, 41f), 270f, new Color(0f, 0f, 0f, 125f / 255f));
            w.Hands[i] = Pic(w.Frame, $"Hand{i}", CursorTexture, new Vector2(49f, ys[i] - 5f), new Vector2(29f, 41f), 270f);
            w.Options[i] = TextBox(w.Frame, $"Option{i}", new Vector2(100f, ys[i]), new Vector2(250f, 24f), 2, 0);
            w.Contents.Add(w.HandShadows[i]); w.Contents.Add(w.Hands[i]); w.Contents.Add(w.Options[i]);
        }
    }

    private void BuildWindows()
    {
        // 'w_1': the confirm / saving window
        _confirm = Frame("Confirm", new Vector2(99f, 174f), new Vector2(402f, 202f));
        _confirm.Msg = TextBox(_confirm.Frame, "Msg", new Vector2(13f, 87f), new Vector2(380f, 98f), 0, 2, new Color(1f, 1f, 0f)); _confirm.Contents.Add(_confirm.Msg);
        Bookmark(_confirm, new Vector2(129f, 17f), new Vector2(129f, 54f), new Vector2(142f, 43f), new Vector2(133f, 43f));
        _confirm.Yes = Pic(_confirm.Frame, "Yes", YesTexture, new Vector2(117f, 154f), new Vector2(64f, 29f)); _confirm.YesInitial = new Rect2(117f, 154f, 64f, 29f); _confirm.Contents.Add(_confirm.Yes);
        _confirm.No = Pic(_confirm.Frame, "No", NoTexture, new Vector2(240f, 155f), new Vector2(44f, 29f)); _confirm.NoInitial = new Rect2(240f, 155f, 44f, 29f); _confirm.Contents.Add(_confirm.No);
        // 'w_4': the event save window (the blue coin)
        _event = Frame("Event", new Vector2(114f, 166f), new Vector2(373f, 239f));
        _event.Msg = TextBox(_event.Frame, "Msg", new Vector2(25f, 31f), new Vector2(330f, 98f), 0, 0); _event.Contents.Add(_event.Msg);
        OptionList(_event, new[] { 149f, 185f });
        _event.Options[0].SetText(Message(28)); _event.Options[1].SetText(Message(29));
        // 'w_5': the shine-get window
        _shine = Frame("ShineGet", new Vector2(114f, 138f), new Vector2(373f, 300f));
        _shine.Msg = TextBox(_shine.Frame, "Msg", new Vector2(25f, 32f), new Vector2(330f, 48f), 0, 0); _shine.Contents.Add(_shine.Msg);
        Bookmark(_shine, new Vector2(114f, 96f), new Vector2(114f, 129f), new Vector2(127f, 122f), new Vector2(118f, 122f));
        OptionList(_shine, new[] { 174f, 210f, 246f });
        _shine.Options[0].SetText(Message(28)); _shine.Options[1].SetText(Message(30)); _shine.Options[2].SetText(Message(29));
        foreach (Win w in new[] { _confirm, _event, _shine }) { if (CenterContents) CenterWindow(w); w.HideContents(); }
    }

    // ------------------------------------------------------------------------------------------------------------------ open / finish
    /// <summary>TCardSave::init(purpose) + the director's STATE_CARD_SAVE entry. pauseTree: stop the game under the window (the blue coin, a
    /// shine with the level still live); the pause menu's SAVE keeps its own pause. screenFaded is informational (a log line).</summary>
    public void Open(Purpose purpose, bool pauseTree, bool screenFaded = false)
    {
        if (IsOpen) return;
        _purpose = purpose; _quit = false; _sel = 0; _acc = 0f; _log = 0;
        _pausedTree = pauseTree;
        if (pauseTree) GetTree().Paused = true;
        _root.Visible = true;
        foreach (string act in new[] { "button_a", "button_b" }) Pressed(act);
        StickEdge(true, ref _stickV); StickEdge(false, ref _stickH);   // the stick's state as it is: a stick held through the opening is no move
        SetProgress(Progress.Begin);
        if (DebugPrint) GD.Print($"[SMS cardsave] open: {purpose} (tree paused {pauseTree}, screen faded {screenFaded}); the file {(GameData.Instance?.HasSaveFile == true ? $"saved, {GameData.Instance.SavedShineCount} shines" : "NEW")}, live count {GameData.Instance?.ShineCount ?? 0}");
    }

    private void Finish(int next)
    {
        _progress = Progress.None;
        _root.Visible = false;
        foreach (Win w in new[] { _confirm, _event, _shine }) { w.HideContents(); w.Frame.Visible = false; }
        if (_pausedTree) { GetTree().Paused = false; _pausedTree = false; }
        if (DebugPrint) GD.Print($"[SMS cardsave] done: {_purpose} -> next {next} ({(next == 1 ? "the title" : "the game goes on")})");
        EmitSignal(SignalName.Finished, next);
    }

    // ------------------------------------------------------------------------------------------------------------------------ input
    private bool Pressed(string action)
    {
        bool now = InputMap.HasAction(action) && Input.IsActionPressed(action);
        _held.TryGetValue(action, out bool was);
        _held[action] = now;
        return now && !was;
    }

    /// <summary>The stick as a button, one move a press (mRepeat with no repeat mask = the trigger): +1 down / right, -1 up / left on the edge.</summary>
    private int StickEdge(bool vertical, ref int state)
    {
        string plus = vertical ? "Lstick_down" : "Lstick_right", minus = vertical ? "Lstick_up" : "Lstick_left";
        float v = (InputMap.HasAction(plus) ? Input.GetActionRawStrength(plus) : 0f) - (InputMap.HasAction(minus) ? Input.GetActionRawStrength(minus) : 0f);
        int now = state;
        if (now == 0) { if (v >= MenuStickOn) now = 1; else if (v <= -MenuStickOn) now = -1; }
        else if (Mathf.Abs(v) < MenuStickOff || Mathf.Sign(v) != now) now = 0;
        int edge = now != 0 && now != state ? now : 0;
        state = now;
        return edge;
    }

    // ------------------------------------------------------------------------------------------------------------------------- ticks
    public override void _Process(double delta)
    {
        if (!IsOpen) return;
        _acc += (float)delta * 60f;
        int n = 0;
        while (_acc >= 1f && n < 4 && IsOpen) { _acc -= 1f; Tick(); n++; }
    }

    private void SetProgress(Progress p)
    {
        _progress = p; _step = Step.Open; _frame = 0; _pulse = 0; _wrote = false; _optionWritten = false;
    }

    private void Sound(AudioStreamPlayer p) { if (p != null && p.Stream != null) p.Play(); }

    /// <summary>execMovement_ at 60 Hz: one perform.</summary>
    private void Tick()
    {
        bool a = Pressed("button_a"); Pressed("button_b");
        int dv = StickEdge(true, ref _stickV), dh = StickEdge(false, ref _stickH);
        Progress prev = _progress; Step prevStep = _step;
        switch (_progress)
        {
            case Progress.Begin:
                // UNK0 + UNK2: the bookmark read (gpCardManager->getBookmarkInfos): the file as on disk
                _savedExists = GameData.Instance?.HasSaveFile ?? false;
                _savedCount = GameData.Instance?.SavedShineCount ?? 0;
                SetProgress(_purpose == Purpose.Save ? Progress.Confirm : _purpose == Purpose.ShineGet ? Progress.ShineGetSel : Progress.Event);
                break;
            case Progress.Confirm:
            {   // UNK15: waitForChoiceBM(SAVING, UNK1, 0)
                int r = WaitForChoiceBM(a, dh, Message(9), live: false);
                if (r == 0) SetProgress(Progress.Saving); else if (r == 1) SetProgress(Progress.Done);
                break;
            }
            case Progress.Event:
            {   // UNK2A: waitForSelect2(SAVING, UNK1)
                int r = WaitForSelect(_event, a, dv, Message(36), bookmark: false);
                if (r == 0) SetProgress(Progress.Saving); else if (r == 1) SetProgress(Progress.Done);
                break;
            }
            case Progress.ShineGetSel:
            {   // UNK2B: waitForSelect3(SAVING, SAVING, UNK1); 1 = quit after the save (unk2DE)
                int r = WaitForSelect(_shine, a, dv, Message(HeadingMessage), bookmark: true);
                if (r == 0 || r == 1) { _quit = r == 1; SetProgress(Progress.Saving); }
                else if (r == 2) SetProgress(Progress.Done);
                break;
            }
            case Progress.Saving:
            {
                if (!_wrote) { _wrote = true; GameData.Instance?.SaveNow(); }           // unk2DC: saveSuccess -- the write
                int r = DrawMessageBM(Message(26));
                if (r != -1) { SetProgress(_quit ? Progress.Quit : Progress.SaveDone); break; }
                if (_frame > SavingFrames)
                {
                    if (_optionWritten) EndWaitForChoice(_confirm);                        // the window shrinks
                    else _optionWritten = true;                                          // the option block's write (saveOption + writeOptionBlock)
                }
                else
                {
                    if (!_optionWritten && _frame == SavingDecideFrame)
                    {
                        Sound(Decide);                                                   // MSD_SE_SY_DECIDE_COMMON
                        if (_purpose == Purpose.Save || _purpose == Purpose.ShineGet) ShowCount(_confirm, GameData.Instance?.ShineCount ?? 0, newFile: false);   // the live MSF_SHINE_COUNT
                    }
                    _frame++;
                }
                break;
            }
            case Progress.SaveDone:
            {
                if (_purpose != Purpose.Save) { SetProgress(Progress.Done); break; }
                int r = WaitForChoiceBM(a, dh, Message(35), live: true);                 // waitForChoiceBM(UNK1, UNK19, 0)
                if (r == 0) SetProgress(Progress.Done); else if (r == 1) SetProgress(Progress.Quit);
                break;
            }
            case Progress.Done: Finish(0); return;
            case Progress.Quit: Finish(1); return;
        }
        if (prev == _progress && prevStep != _step)
        {
            if (prevStep == Step.Opening) Sound(WindowIn);     // MSD_SE_SY_2D_IN
            if (_step == Step.Closing) Sound(WindowOut);       // MSD_SE_SY_2D_OUT
        }
        if (DebugPrint && _log++ % 30 == 0) GD.Print($"[SMS cardsave] {DebugState}");
    }

    // ---------------------------------------------------------------------------------------------------------- the window routines
    private void ShowBookmark(Win w, bool withCount, bool newFileText)
    {
        w.MarioText.Visible = true;
        for (int i = 0; i < 3; i++) w.Letters[i].Visible = i == Mathf.Clamp(FileLetter, 0, 2);
        if (!withCount) { w.NewText.Visible = false; w.CountUnder.Visible = false; w.CountOver.Visible = false; return; }
        if (CountShown == CountMode.Live) { ShowCount(w, GameData.Instance?.ShineCount ?? 0, newFile: false); return; }   // 124b knob: the live count, always a number
        if (newFileText) { w.NewText.SetText(Message(25)); w.NewText.Visible = true; w.CountUnder.Visible = false; w.CountOver.Visible = false; }
        else ShowCount(w, _savedCount, newFile: false);
    }

    /// <summary>The count group: under 100 'f_sh' (two digits), from 100 'f_si' (three); the digits coin_number_%d (score - 100 x hundreds).</summary>
    private void ShowCount(Win w, int score, bool newFile)
    {
        w.NewText.Visible = false;
        score = Mathf.Clamp(score, 0, 999);
        if (score < 100)
        {
            w.UnderDigits[0].Texture = Digit(score / 10); w.UnderDigits[1].Texture = Digit(score % 10);
            w.CountUnder.Visible = true; w.CountOver.Visible = false;
        }
        else
        {
            w.OverDigits[0].Texture = Digit(score / 100); int rest = score - score / 100 * 100;
            w.OverDigits[1].Texture = Digit(rest / 10); w.OverDigits[2].Texture = Digit(rest % 10);
            w.CountOver.Visible = true; w.CountUnder.Visible = false;
        }
    }
    private Texture2D Digit(int d) => DigitTextures != null && d >= 0 && d < DigitTextures.Length ? DigitTextures[d] : null;

    /// <summary>waitForSelect2 / waitForSelect3 (1321 / 1447): the list window. -1 while up; else the picked option.</summary>
    private int WaitForSelect(Win w, bool a, int dv, string heading, bool bookmark)
    {
        switch (_step)
        {
            case Step.Open:
                w.Frame.Visible = true;
                w.SetCenteredSize(20, w.Initial.Size.X, w.Initial.Size.Y, 0f, 0f);
                w.Msg.SetText(heading);
                w.HideContents();
                _step = Step.Opening;
                break;
            case Step.Opening:
                if (w.Update())
                {
                    _step = Step.Shown; _sel = 0;
                    w.Msg.Visible = true;
                    for (int i = 0; i < w.Options.Length; i++) { w.Options[i].Visible = true; w.Hands[i].Visible = w.HandShadows[i].Visible = i == 0; }
                    if (bookmark) ShowBookmark(w, withCount: true, newFileText: !_savedExists);
                }
                break;
            case Step.Shown:
            {
                int old = _sel;
                if (a) { Sound(Select); _step = Step.Close; }                                  // 0x481C
                else if (dv < 0) { if (old > 0) _sel = old - 1; }
                else if (dv > 0) { if (old < w.Options.Length - 1) _sel = old + 1; }
                if (old != _sel)
                {
                    Sound(CursorMove);                                                           // 0x481E
                    w.Hands[old].Visible = w.HandShadows[old].Visible = false;
                    w.Hands[_sel].Visible = w.HandShadows[_sel].Visible = true;
                }
                break;
            }
            case Step.Close:
                w.SetCenteredSize(20, 0f, 0f, w.Initial.Size.X, w.Initial.Size.Y);
                w.HideContents();
                _step = Step.Closing;
                break;
            case Step.Closing:
                if (w.Update()) { w.Frame.Visible = false; _step = Step.Done; }
                break;
            case Step.Done:
                return _sel;
        }
        return -1;
    }

    /// <summary>waitForChoiceBM (788): the 'w_1' YES / NO window with the bookmark (live: the live count, as after the save). -1 while up; 0 yes, 1 no.</summary>
    private int WaitForChoiceBM(bool a, int dh, string message, bool live)
    {
        Win w = _confirm;
        switch (_step)
        {
            case Step.Open:
                w.Msg.SetText(message);
                w.HideContents();
                w.Frame.Visible = true;
                w.SetCenteredSize(20, w.Initial.Size.X, w.Initial.Size.Y, 0f, 0f);
                _step = Step.Opening;
                break;
            case Step.Opening:
                if (w.Update())
                {
                    w.Msg.Visible = true;
                    if (live) { ShowBookmark(w, withCount: false, newFileText: false); ShowCount(w, GameData.Instance?.ShineCount ?? 0, newFile: false); }   // unk310 == SAVE_DONE: MSF_SHINE_COUNT
                    else ShowBookmark(w, withCount: true, newFileText: !_savedExists);
                    w.Yes.Visible = w.No.Visible = true;
                    w.PickSize(true, 1, 1f, 1f); w.PickSize(false, 1, 1f, 1f); w.PickUpdate(true); w.PickUpdate(false);
                    _sel = 0;
                    PickColors(w);
                    _step = Step.Shown;
                }
                break;
            case Step.Shown:
            {
                int old = _sel;
                if (a)
                {
                    Sound(_sel == 0 ? Select : Cancel);                                           // 0x481C / 0x481D
                    w.HideContents();
                    _step = Step.Closing;
                    w.SetCenteredSize(20, 0f, 0f, w.Initial.Size.X, w.Initial.Size.Y);
                    break;
                }
                else if (dh < 0) _sel = 0;
                else if (dh > 0) _sel = 1;
                bool yes = _sel == 0;
                if (_pulse == 4) w.PickSize(yes, 40, 1.5f, 1f);
                else if (_pulse == 44) w.PickSize(yes, 40, 1f, 1.5f);
                int t = _pulse < 40 ? _pulse : 80 - _pulse;
                t = (int)(t * 255f / 40f);
                (yes ? w.Yes : w.No).Modulate = new Color(t / 255f, 1f, 0f, 1f);              // setWhite((t << 24) + 0xFF00FF): green to yellow
                w.PickUpdate(yes);
                _pulse++;
                if (_pulse > 80) _pulse = 0;
                if (old != _sel)
                {
                    _pulse = 0;
                    Sound(CursorMove);                                                            // 0x481E
                    w.PickSize(true, 1, 1f, 1f); w.PickSize(false, 1, 1f, 1f); w.PickUpdate(true); w.PickUpdate(false);
                    PickColors(w);
                }
                break;
            }
            case Step.Closing:
                if (w.Update()) { w.Frame.Visible = false; _step = Step.Done; }
                break;
            case Step.Done:
                return _sel;
        }
        return -1;
    }

    private void PickColors(Win w)
    {
        w.Yes.Modulate = _sel == 0 ? new Color(0f, 1f, 0f) : Colors.White;
        w.No.Modulate = _sel == 1 ? new Color(0f, 1f, 0f) : Colors.White;
    }

    /// <summary>drawMessageBM (1091): the 'w_1' window with a message and the bookmark, no choice. -1 while up; 0 once closed.</summary>
    private int DrawMessageBM(string message)
    {
        Win w = _confirm;
        switch (_step)
        {
            case Step.Open:
                w.Msg.SetText(message);
                w.HideContents();
                w.Frame.Visible = true;
                w.SetCenteredSize(20, w.Initial.Size.X, w.Initial.Size.Y, 0f, 0f);
                _step = Step.Opening;
                break;
            case Step.Opening:
                if (w.Update())
                {
                    w.Msg.Visible = true;
                    bool count = _purpose == Purpose.Save || _purpose == Purpose.ShineGet;    // the blue coin's saving: the name, no count
                    ShowBookmark(w, withCount: count, newFileText: !_savedExists);
                    _step = Step.Shown;
                }
                break;
            case Step.Closing:
                if (w.Update()) { w.Frame.Visible = false; _step = Step.Done; }
                break;
            case Step.Done:
                return 0;
        }
        return -1;
    }

    /// <summary>endWaitForChoice (575): the shown 'w_1' window's contents away and the shrink.</summary>
    private void EndWaitForChoice(Win w)
    {
        if (_step != Step.Shown) return;
        w.HideContents();
        _step = Step.Closing;
        w.SetCenteredSize(20, 0f, 0f, w.Initial.Size.X, w.Initial.Size.Y);
    }
}
