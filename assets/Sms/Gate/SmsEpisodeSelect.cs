using Godot;
using System.Collections.Generic;

/// <summary>
/// The episode select -- TSelectDir (System/SelectDir.cpp) with TSelectMenu and TSelectGrad (GC2D/SelectMenu.cpp); SMS pass 127. The
/// screen a main stage's gate goes through (TMarDirector::moveStage -> APP_STATE_TITLE): the stage's gradient behind, the 3D ring of
/// its shines (<see cref="SmsSelectShineRing"/>), and scenario_select_1.blo's panes laid out as the disc has them on the game's 600 x 448
/// frame (the blo is 600 x 480 with rows 16 .. 464 on screen). Its states and frames (perform, at 30 a second):
/// <list type="bullet">
/// <item>LETTERBOX (30 frames): the two bars grow in from the top and bottom edges; the banner slides in from the right (20 frames)
/// and squashes (15), stretches (20) and settles (15) -- TBoundPane's three-point curves.</item>
/// <item>APPEAR: the stage name fades in 6 a frame, then the EPISODE pane, the marks row and the score 6 a frame.</item>
/// <item>INPUT: left / right step through the open episodes (the ring turns a slot in 10 frames, the EPISODE pane slides out as its
/// twin slides in from the side over 10 frames, the arrows show at the ends there are more past); A picks (the bars close to the middle
/// over 30 frames while everything fades 16 a frame, the ring fades out, the burst -- a slot) then 10 frames of wait; the selected
/// mark pulses 64 .. 255 by 6, the arrows bob 5 px.</item>
/// <item>CLOSE: MSF_EPISODE = the pick; a 1 s fade to WHITE; then the next scene -- his character select (the stage's
/// CharacterSelectScene) or the level.</item>
/// </list>
/// What may be picked is the game's rule (initData): one past the last cleared episode; the newest open one starts selected. The disc's
/// movie behind each episode is a still here (<see cref="SmsEpisodeInfo.Preview"/>) -- DEVIATION; the stage's coin record (the SCORE row)
/// has no counter in the port and is hidden unless <see cref="ShowScore"/>. The stage comes from GameData.PendingStage (an M gate), else
/// <see cref="TestStage"/> (running the scene by itself).
/// </summary>
[Tool, GlobalClass]
public partial class SmsEpisodeSelect : Control
{
    public enum MenuState { CloseMenu = 0, LetterboxAnimation = 1, StageBannerSlide = 2, StageBannerSquash = 3, StageBannerStretch = 4, AppearMenu = 5, MenuInputLoop = 6, MenuAnimLoop = 7, DisappearMenu = 8, WaitBeforeClose = 9 }

    public const float FrameW = 600f, FrameH = 448f, FrameTop = 16f, FrameRate = 30f;

    [ExportGroup("Where it goes")]
    /// <summary>The stage shown when no gate set one (the scene run on its own).</summary>
    [Export] public SmsStageInfo TestStage;

    [ExportGroup("Art (the disc's select/timg)")]
    [Export] public Texture2D EpisodeWord;      // sc_story.bti: EPISODE
    [Export] public Texture2D[] Numbers = System.Array.Empty<Texture2D>();   // sc_number_1 .. 8
    [Export] public Texture2D MarkGot;          // sc_mark_1: the sun (a cleared episode)
    [Export] public Texture2D MarkOpen;         // sc_mark_0: the dot (an open one)
    [Export] public Texture2D Arrow;            // sc_arrow (points left; flipped for the right)
    [Export] public Texture2D ScoreWord;        // sc_myscore
    [Export] public Texture2D CoinIcon;         // coin_icon
    [Export] public Texture2D CoinX;            // coin_number_x
    [Export] public Texture2D[] CoinDigits = System.Array.Empty<Texture2D>();  // coin_number_0 .. 9
    [Export] public Texture2D ShineGetIcon;     // shine_get_icon (the 100-coin shine)
    [Export] public Texture2D PreviewFrame;     // DEVIATION: a frame around the episode's still (none = the still alone)
    /// <summary>The episode name's font (the game: the system font); none = Font/Delfino.ttf when it exists, else the theme's.</summary>
    [Export] public Font NameFont;
    [Export] public int NameFontSize = 20;
    /// <summary>The stage name in the big letters when the stage has no banner: the big_tx set's format (SmsBigText).</summary>
    [Export] public string BigLetterFormat = "res://assets/Sms/hud/big_tx_{0}.png";

    [ExportGroup("Knobs")]
    /// <summary>The letterbox bars and the gradient span the whole window (the game's 4:3 frame leaves the 16:9 sides bare).</summary>
    [Export] public bool StretchBars = true;
    /// <summary>Show the SCORE row (the stage's coin record): the port keeps no record, so off by default.</summary>
    [Export] public bool ShowScore = false;
    [Export] public int ScoreCoins = 0;
    /// <summary>Where the episode's still sits (frame px, the game's 3D band) and its size; the ring shows through where it is transparent.</summary>
    [Export] public Vector2 PreviewCentre = new Vector2(300f, 150f);
    [Export] public Vector2 PreviewSize = new Vector2(256f, 144f);
    [Export] public bool DebugPrint;

    [ExportGroup("Sounds (slots)")]
    [Export] public AudioStreamPlayer Music;        // MSD_BGM_FANFARE_CASINO
    [Export] public AudioStreamPlayer CursorSound;  // MSD_SE_SY_SHINE_CURSOR
    [Export] public AudioStreamPlayer SelectSound;  // MSD_SE_SY_SHINE_SELECT
    /// <summary>127b: ms_2d_scsel_on_a / a2 (a2 for a cleared episode) / b / c / d at the screen's centre on the pick (a Node2D scene).</summary>
    [Export] public PackedScene PickBurstScene;

    /// <summary>The pick is made (the episode, 0-based) -- before the fade.</summary>
    [Signal] public delegate void PickedEventHandler(int episode);

    // -- state ------------------------------------------------------------------------------------------------------------------------------
    private SmsStageInfo _stage;
    private MenuState _state = MenuState.CloseMenu;
    private readonly int[] _unlock = new int[8];   // 0 locked, 2 open, 3 cleared
    private int _num, _selected;
    private int _letterboxFrame, _closeWaitFrame;
    private bool _closeMenu, _done, _moved;
    private double _acc;
    private readonly Dictionary<string, bool> _held = new();
    private bool _selectNext;
    private bool _markPulseUp = true, _arrowDirUp = true; private int _arrowPos;
    private float _fadeT; private bool _fadeStarted;

    // -- nodes -------------------------------------------------------------------------------------------------------------------------------
    private ColorRect _backdrop; private ShaderMaterial _gradMat;
    private Control _frame; private ColorRect _barTop, _barBottom;
    private SubViewportContainer _ringView; private SubViewport _ringVp; private Camera3D _ringCam; private SmsSelectShineRing _ring;
    private TextureRect _preview, _previewFrame;
    private Control _bannerGroup; private TextureRect _banner, _bannerShadow; private List<TextureRect> _bannerLetters = new();
    private Control _pane1, _pane2; private TextureRect _num1, _num2, _numS1, _numS2, _word1, _word2, _wordS1, _wordS2; private Label _name1, _name2;
    private Control _marksGroup; private readonly TextureRect[] _marks = new TextureRect[8]; private TextureRect _arrowL, _arrowR;
    private Control _scoreGroup; private Label _stageNameText;
    private SmsFader _fader;
    private Vector2 _arrowLHome, _arrowRHome;
    private float _barTopH, _barBottomH;   // the bars' blo heights (166)
    private readonly Color _markCol = Colors.White, _selectedMarkCol = new Color(1f, 1f, 0f);
    private const float MarkAlpha = 120f / 255f, SelectedMarkAlpha = 1f;

    // -- the pane animations (TExPane / TBoundPane) --------------------------------------------------------------------------------------
    private class ExAnim
    {
        public Control Node; public int Frames, T; public Vector2 BasePos, BaseSize;
        public bool HasOff, HasSize, HasAlpha; public Vector2 OffFrom, OffTo, SizeFrom, SizeTo; public float AlphaFrom, AlphaTo;
        /// <summary>TExPane::update: one frame; true once finished (the final values set).</summary>
        public bool Update()
        {
            if (Node == null) return true;
            if (T >= Frames) return true;
            T++;
            float t = Frames > 0 ? Mathf.Clamp((float)T / Frames, 0f, 1f) : 1f;
            Vector2 off = HasOff ? OffFrom.Lerp(OffTo, t) : Vector2.Zero;
            Vector2 size = HasSize ? SizeFrom.Lerp(SizeTo, t) : BaseSize;
            Node.Position = BasePos + off;
            if (HasSize) Node.Size = size;
            if (HasAlpha) { Color c = Node.Modulate; c.A = Mathf.Lerp(AlphaFrom, AlphaTo, t); Node.Modulate = c; }
            return T >= Frames;
        }
    }
    private class BoundAnim
    {
        public Control Node; public int Frames, T; public Vector2 BasePos, BaseSize; public Vector2 P0, P1, P2, S0, S1, S2; public bool Active;
        private static float B(float t, float a, float b, float c) { float u = 1f - t; return a * u * u + b * 2f * u * t + c * t * t; }
        /// <summary>TBoundPane::update: the position delta and the size delta each on a three-point curve, the size about the centre (base position 4).</summary>
        public bool Update()
        {
            if (Node == null || !Active) return true;
            if (T >= Frames) { Active = false; return true; }
            T++;
            float t = Frames > 0 ? Mathf.Clamp((float)T / Frames, 0f, 1f) : 1f;
            Vector2 dp = new Vector2(B(t, P0.X, P1.X, P2.X), B(t, P0.Y, P1.Y, P2.Y));
            Vector2 ds = new Vector2(B(t, S0.X, S1.X, S2.X), B(t, S0.Y, S1.Y, S2.Y));
            Node.Size = BaseSize + ds;
            Node.Position = BasePos + dp - ds * 0.5f;
            if (T >= Frames) Active = false;
            return T >= Frames;
        }
    }
    private readonly ExAnim _topAnim = new(), _bottomAnim = new(), _bannerAnim = new(), _pane1Anim = new(), _pane2Anim = new();
    private readonly BoundAnim _bannerText = new(), _bannerShadowText = new();

    // -- public (the drive) ---------------------------------------------------------------------------------------------------------------
    public MenuState State => _state;
    public int Selected => _selected;
    public int UnlockedCount => _num;
    public int UnlockState(int i) => i >= 0 && i < 8 ? _unlock[i] : 0;
    public SmsStageInfo Stage => _stage;
    public SmsSelectShineRing Ring => _ring;
    public bool CloseRequested => _closeMenu;
    public float BarTopHeight => _barTop != null ? _barTop.Size.Y : 0f;
    public float BannerX => _bannerGroup != null ? _bannerGroup.Position.X : 0f;
    public float MenuAlpha => _pane1 != null ? (_pane1.Visible ? _pane1.Modulate.A : _pane2.Modulate.A) : 0f;
    public bool ArrowLeftVisible => _arrowL != null && _arrowL.Visible && _arrowL.Modulate.A > 0.01f;
    public bool ArrowRightVisible => _arrowR != null && _arrowR.Visible && _arrowR.Modulate.A > 0.01f;
    /// <summary>The pane showing the selection: pane1 until a step has run, then pane2 (the game hides pane1 after each slide).</summary>
    private bool Pane2Current => _pane2 != null && _pane2.Visible && (_pane1 == null || !_pane1.Visible);
    public Texture2D ShownNumber => Pane2Current ? _num2?.Texture : _num1?.Texture;
    public string ShownName => Pane2Current ? _name2?.Text : _name1?.Text;
    public SmsFader Fader => _fader;

    public override void _Ready()
    {
        _stage = GameData.Instance?.PendingStage ?? TestStage;
        if (_stage == null && !Engine.IsEditorHint()) GD.PushWarning("[SMS select] no stage (GameData.PendingStage / TestStage): a stand-in with eight episodes");
        _stage ??= StandIn();
        Build();
        if (Engine.IsEditorHint()) { LayoutStatic(); return; }
        InitData();
        StartOpenWindow();
        // the fader: startWipe(0xe, 0.4) from black (white for stage 9)
        var faderScene = GD.Load<PackedScene>("res://assets/Sms/Console/Fader.tscn");
        if (faderScene != null) { _fader = faderScene.Instantiate<SmsFader>(); AddChild(_fader); _fader.SetColor(Colors.Black); _fader.SetBlack(); _fader.FadeIn(0.4f); }
        Music?.Play();
        if (DebugPrint) GD.Print($"[SMS select] {_stage.StageName}: {_stage.Episodes.Length} episodes, {_num} open, selected {_selected} ({string.Join("", System.Array.ConvertAll(_unlock, u => u.ToString()))})");
    }

    private static SmsStageInfo StandIn()
    {
        var s = new SmsStageInfo { StageName = "STAGE" };
        var eps = new SmsEpisodeInfo[8];
        for (int i = 0; i < 8; i++) eps[i] = new SmsEpisodeInfo { Name = $"Episode {i + 1}" };
        s.Episodes = eps;
        return s;
    }

    // ===================================================================================================================================
    //  initData: the unlock states, the selection, the panes' contents
    // ===================================================================================================================================
    private void InitData()
    {
        int n = Mathf.Min(_stage.Episodes.Length, 8);
        for (int i = 0; i < 8; i++) _unlock[i] = i < n ? 2 : 0;
        _num = 0;
        for (int i = 0; i < n; i++)
            if (_stage.IsCleared(i)) { _unlock[i] = 3; _num = i + 2; }
        if (_num > n) _num = n;
        if (_num == 0) _num = Mathf.Min(1, n);
        _selected = Mathf.Max(_num - 1, 0);
        for (int i = _num; i < 8; i++) _unlock[i] = 0;
        // the marks row: odd counts on the i_o slots, even on the i_e ones (centred either way)
        for (int i = 0; i < 8; i++) _marks[i].Visible = false;
        float x0 = (_num & 1) == 1 ? -105f + 32f * ((7 - _num) / 2) : -121f + 32f * ((8 - _num) / 2);
        for (int i = 0; i < _num; i++)
        {
            var m = _marks[i];
            m.Position = new Vector2(x0 + 32f * i, -10f);
            m.Texture = _unlock[i] == 3 ? MarkGot : MarkOpen;
            m.Visible = true;
            SetMark(i, false);
        }
        SetMark(_selected, true);
        // the arrows for this count (a_lN / a_rN: 16 px further out per extra episode)
        _arrowL.Visible = _arrowR.Visible = false;
        if (_num > 1)
        {
            _arrowLHome = new Vector2(-59f - 16f * (_num - 2), -7f);
            _arrowRHome = new Vector2(45f + 16f * (_num - 2), -7f);
            _arrowL.Position = _arrowLHome; _arrowR.Position = _arrowRHome;
            _arrowL.Modulate = new Color(1f, 1f, 1f, MarkAlpha); _arrowR.Modulate = new Color(1f, 1f, 1f, MarkAlpha);
            if (_selected != 0) _arrowL.Visible = true;
            if (_selected != _num - 1) _arrowR.Visible = true;
        }
        SetPane(_pane1, _num1, _numS1, _name1, _selected);
        SetPreview(_selected);
        // the score row
        _scoreGroup.Visible = ShowScore;
        if (ShowScore) SetScore(ScoreCoins);
        // the ring
        _ring.Init(_unlock, _num, _selected);
    }

    private void SetMark(int i, bool selected)
    {
        var m = _marks[i]; if (m == null) return;
        Color c = selected ? _selectedMarkCol : _markCol; c.A = selected ? SelectedMarkAlpha : MarkAlpha;
        m.Modulate = c;
    }

    private void SetPane(Control pane, TextureRect num, TextureRect numShadow, Label name, int episode)
    {
        var tex = episode >= 0 && episode < Numbers.Length ? Numbers[episode] : null;
        num.Texture = tex; numShadow.Texture = tex;
        name.Text = _stage.EpisodeName(episode);
    }

    private void SetPreview(int episode)
    {
        var tex = episode >= 0 && episode < _stage.Episodes.Length && _stage.Episodes[episode] != null ? _stage.Episodes[episode].Preview : null;
        _preview.Texture = tex;
        _preview.Visible = tex != null;
        if (_previewFrame != null) _previewFrame.Visible = tex != null && PreviewFrame != null;
    }

    private void SetScore(int coins)
    {
        coins = Mathf.Clamp(coins, 0, 999);
        var digits = new[] { GetNodeOrNull<TextureRect>("Frame/Score/Digit0"), GetNodeOrNull<TextureRect>("Frame/Score/Digit1"), GetNodeOrNull<TextureRect>("Frame/Score/Digit2") };
        if (CoinDigits.Length < 10) return;
        if (coins < 100) { digits[0].Visible = false; digits[1].Texture = CoinDigits[coins / 10]; digits[2].Texture = CoinDigits[coins % 10]; }
        else { digits[0].Visible = true; digits[0].Texture = CoinDigits[coins / 100]; digits[1].Texture = CoinDigits[coins / 10 % 10]; digits[2].Texture = CoinDigits[coins % 10]; }
    }

    // ===================================================================================================================================
    //  the layout: scenario_select_1.blo on the 600 x 448 frame
    // ===================================================================================================================================
    private TextureRect Pic(Control parent, string name, Texture2D tex, Vector2 pos, Vector2 size, float alpha = 1f, Color? tint = null)
    {
        var tr = new TextureRect { Name = name, Texture = tex, Position = pos, Size = size, StretchMode = TextureRect.StretchModeEnum.Scale, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        Color c = tint ?? Colors.White; c.A = alpha; tr.Modulate = c;
        parent.AddChild(tr);
        return tr;
    }

    private Control Group(Control parent, string name, Vector2 pos)
    {
        var g = new Control { Name = name, Position = pos, Size = new Vector2(10f, 10f), MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(g);
        return g;
    }

    private void Build()
    {
        foreach (Node c in GetChildren()) if (c.Name == "Backdrop" || c.Name == "Ring" || c.Name == "Frame") { RemoveChild(c); c.QueueFree(); }
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        // the gradient (TSelectGrad: a quad from the stage's top-left colour to its bottom-right one, the other corners their mean)
        _backdrop = new ColorRect { Name = "Backdrop", MouseFilter = MouseFilterEnum.Ignore };
        _backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        var shader = new Shader { Code = GradientShader };
        _gradMat = new ShaderMaterial { Shader = shader };
        _backdrop.Material = _gradMat;
        AddChild(_backdrop);
        _gradTL = _stage.GradientTopLeft; _gradBR = _stage.GradientBottomRight; _cycle = new[] { _stage.GradientCycle.X, _stage.GradientCycle.Y, _stage.GradientCycle.Z };
        ApplyGradient();
        // the ring: a 3D view the size of the frame (Screen 3D: the camera at (300, 240, 1300) looking at (300, 240, 0), fovy 30, 50 .. 10000)
        _ringView = new SubViewportContainer { Name = "Ring", Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_ringView);
        _ringVp = new SubViewport { TransparentBg = true, Size = new Vector2I(600, 448), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _ringView.AddChild(_ringVp);
        _ringCam = new Camera3D { Position = new Vector3(300f, 240f, 1300f), Fov = 30f, Near = 50f, Far = 10000f, KeepAspect = Camera3D.KeepAspectEnum.Height };
        _ringVp.AddChild(_ringCam);
        _ringCam.LookAt(new Vector3(300f, 240f, 0f), Vector3.Up);
        var light = new DirectionalLight3D { Position = new Vector3(300f, 800f, 1300f) };
        _ringVp.AddChild(light);
        light.LookAt(new Vector3(300f, 160f, 0f), Vector3.Up);
        _ring = new SmsSelectShineRing { Name = "Shines", DebugPrint = DebugPrint };
        if (ResourceLoader.Exists("res://models/ShineSprite/ShineSprite.glb")) _ring.ShineScene = GD.Load<PackedScene>("res://models/ShineSprite/ShineSprite.glb");
        _ringVp.AddChild(_ring);
        // the frame: the blo's 600 x 480 with rows 16 .. 464 on screen
        _frame = new Control { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(FrameW, FrameH) };
        AddChild(_frame);
        // the episode's still (DEVIATION: the game's movie) in the 3D band
        _previewFrame = PreviewFrame != null ? Pic(_frame, "PreviewFrame", PreviewFrame, PreviewCentre - PreviewSize * 0.5f - new Vector2(8f, 8f), PreviewSize + new Vector2(16f, 16f)) : null;
        _preview = Pic(_frame, "Preview", null, PreviewCentre - PreviewSize * 0.5f, PreviewSize);
        _preview.Visible = false;
        // msk1 / msk2: sc_mask (black) at alpha 160 -- 600 x 166 at y 0 and y 314 (blo), i.e. 150 rows each on screen
        _barTopH = 166f; _barBottomH = 166f;
        _barTop = new ColorRect { Name = "BarTop", Color = Colors.Black, Position = new Vector2(0f, 0f - FrameTop), Size = new Vector2(FrameW, _barTopH), Modulate = new Color(1f, 1f, 1f, 160f / 255f), MouseFilter = MouseFilterEnum.Ignore };
        _barBottom = new ColorRect { Name = "BarBottom", Color = Colors.Black, Position = new Vector2(0f, 314f - FrameTop), Size = new Vector2(FrameW, _barBottomH), Modulate = new Color(1f, 1f, 1f, 160f / 255f), MouseFilter = MouseFilterEnum.Ignore };
        _frame.AddChild(_barTop); _frame.AddChild(_barBottom);
        // the stage banner: xx_0 at (296, 150) with xx_a (-222, -20) and the black shadow xx_b (-218, -14) at alpha 120
        _bannerGroup = Group(_frame, "Banner", new Vector2(296f, 150f - FrameTop));
        Vector2 bsize = _stage.Banner != null ? _stage.Banner.GetSize() : new Vector2(454f, 50f);
        _bannerShadow = Pic(_bannerGroup, "Shadow", _stage.Banner, new Vector2(-bsize.X * 0.5f + 5f, -14f), bsize, 120f / 255f, Colors.Black);
        _banner = Pic(_bannerGroup, "Art", _stage.Banner, new Vector2(-bsize.X * 0.5f + 1f, -20f), bsize);
        if (_stage.Banner == null)
        {   // no art: the name in the big letters, over the banner's spot (DEVIATION: the game draws the 'map' text box only in Japanese)
            _bannerShadow.Visible = _banner.Visible = false;
            _bannerLetters = SmsBigText.Build(_bannerGroup, _stage.StageName, BigLetterFormat, new Vector2(5f, 5f), 50f, -10f, 24f, SmsBigText.DefaultPalette(), true, null, 0f, 520f);
        }
        // s_0 / 0_0: EPISODE + the number + the name, at (300, 299) and its twin 144 to the right
        _pane1 = Group(_frame, "Episode1", new Vector2(300f, 299f - FrameTop));
        _pane2 = Group(_frame, "Episode2", new Vector2(444f, 299f - FrameTop));
        BuildEpisodePane(_pane1, out _word1, out _wordS1, out _num1, out _numS1, out _name1);
        BuildEpisodePane(_pane2, out _word2, out _wordS2, out _num2, out _numS2, out _name2);
        _pane2.Visible = false;
        // i_0: the marks and arrows at (296, 384)
        _marksGroup = Group(_frame, "Marks", new Vector2(296f, 384f - FrameTop));
        for (int i = 0; i < 8; i++) { _marks[i] = Pic(_marksGroup, $"Mark{i}", MarkGot, new Vector2(-121f + 32f * i, -10f), new Vector2(28f, 28f), MarkAlpha); _marks[i].Visible = false; }
        _arrowL = Pic(_marksGroup, "ArrowL", Arrow, new Vector2(-59f, -7f), new Vector2(24f, 24f), MarkAlpha);
        _arrowR = Pic(_marksGroup, "ArrowR", Arrow, new Vector2(45f, -7f), new Vector2(24f, 24f), MarkAlpha); _arrowR.FlipH = true;
        _arrowL.Visible = _arrowR.Visible = false;
        // sc_0: SCORE x NNN (+ the 100-coin shine) at (296, 423)
        _scoreGroup = Group(_frame, "Score", new Vector2(296f, 423f - FrameTop));
        Pic(_scoreGroup, "Word", ScoreWord, new Vector2(-156f, -4f), new Vector2(80f, 20f));
        Pic(_scoreGroup, "Coin", CoinIcon, new Vector2(-68f, -9f), new Vector2(21f, 28f));
        Pic(_scoreGroup, "X", CoinX, new Vector2(-48f, -8f), new Vector2(26f, 26f));
        Pic(_scoreGroup, "Digit0", CoinDigits.Length > 0 ? CoinDigits[0] : null, new Vector2(-29f, -8f), new Vector2(26f, 26f));
        Pic(_scoreGroup, "Digit1", CoinDigits.Length > 0 ? CoinDigits[0] : null, new Vector2(-13f, -8f), new Vector2(26f, 26f));
        Pic(_scoreGroup, "Digit2", CoinDigits.Length > 0 ? CoinDigits[0] : null, new Vector2(8f, -8f), new Vector2(26f, 26f));
        Pic(_scoreGroup, "Shine", ShineGetIcon, new Vector2(36f, -16f), new Vector2(30f, 36f)).Visible = false;
        _scoreGroup.Visible = ShowScore;
        // 'map': the stage name text (the Japanese version's; empty here) -- its alpha paces APPEAR, so it exists
        _stageNameText = new Label { Name = "StageNameText", Position = new Vector2(151f, 104f - FrameTop), Size = new Vector2(300f, 20f), Text = "", MouseFilter = MouseFilterEnum.Ignore };
        _frame.AddChild(_stageNameText);
        // the start: everything but the bars hidden (startOpenWindow)
        _bannerGroup.Visible = false; _pane1.Visible = false; _marksGroup.Visible = false; _scoreGroup.Visible = false; _stageNameText.Visible = false;
        LayoutStatic();
    }

    private void BuildEpisodePane(Control pane, out TextureRect word, out TextureRect wordShadow, out TextureRect num, out TextureRect numShadow, out Label name)
    {
        wordShadow = Pic(pane, "WordShadow", EpisodeWord, new Vector2(-63f, 6f), new Vector2(114f, 28f), 80f / 255f, Colors.Black);
        word = Pic(pane, "Word", EpisodeWord, new Vector2(-67f, 0f), new Vector2(114f, 28f));
        numShadow = Pic(pane, "NumberShadow", Numbers.Length > 0 ? Numbers[0] : null, new Vector2(45f, -5f), new Vector2(40f, 40f), 80f / 255f, Colors.Black);
        num = Pic(pane, "Number", Numbers.Length > 0 ? Numbers[0] : null, new Vector2(41f, -11f), new Vector2(40f, 40f));
        name = new Label { Name = "Name", Position = new Vector2(-249f, 36f), Size = new Vector2(500f, 26f), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        Font f = NameFont ?? (ResourceLoader.Exists("res://Font/Delfino.ttf") ? GD.Load<Font>("res://Font/Delfino.ttf") : null);
        if (f != null) name.AddThemeFontOverride("font", f);
        name.AddThemeFontSizeOverride("font_size", NameFontSize);
        name.AddThemeColorOverride("font_color", Colors.White);
        name.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.6f));
        name.AddThemeConstantOverride("shadow_offset_x", 2); name.AddThemeConstantOverride("shadow_offset_y", 2);
        pane.AddChild(name);
    }

    /// <summary>The frame on the window: 448 rows on the height, centred; the gradient and (StretchBars) the bars over the full width.</summary>
    private void LayoutStatic()
    {
        Vector2 vp = GetViewportRect().Size;
        if (vp.X < 1f || vp.Y < 1f) return;
        float s = vp.Y / FrameH;
        float x0 = (vp.X - FrameW * s) * 0.5f;
        _frame.Scale = new Vector2(s, s);
        _frame.Position = new Vector2(x0, 0f);
        _ringView.Position = new Vector2(x0, 0f);
        _ringView.Size = new Vector2(FrameW * s, FrameH * s);
        if (StretchBars)
        {
            float left = -x0 / s, width = vp.X / s;
            _barTop.Position = new Vector2(left, _barTop.Position.Y); _barTop.Size = new Vector2(width, _barTop.Size.Y);
            _barBottom.Position = new Vector2(left, _barBottom.Position.Y); _barBottom.Size = new Vector2(width, _barBottom.Size.Y);
        }
    }

    // ===================================================================================================================================
    //  the gradient (TSelectGrad)
    // ===================================================================================================================================
    private const string GradientShader = @"shader_type canvas_item;
uniform vec4 tl : source_color = vec4(0.0, 1.0, 0.0, 1.0);
uniform vec4 br : source_color = vec4(0.0, 1.0, 1.0, 1.0);
void fragment() {
	vec4 mid = (tl + br) * 0.5;
	vec4 top = mix(tl, mid, UV.x);
	vec4 bottom = mix(mid, br, UV.x);
	COLOR = mix(top, bottom, UV.y);
}";
    private Color _gradTL, _gradBR; private int[] _cycle = { 4, 2, 0 };

    private void ApplyGradient()
    {
        _gradMat?.SetShaderParameter("tl", _gradTL);
        _gradMat?.SetShaderParameter("br", _gradBR);
    }

    /// <summary>TSelectGrad::perform(CUE_CALC_ANIM): each channel of the bottom-right colour climbs (cycle 0) or falls (cycle 3) by 2 a frame;
    /// the top-left one runs a step behind; when a channel hits its end every channel's step moves on (six steps round).</summary>
    private void GradientFrame()
    {
        bool next = false;
        float[] br = { _gradBR.R, _gradBR.G, _gradBR.B }, tl = { _gradTL.R, _gradTL.G, _gradTL.B };
        for (int i = 0; i < 3; i++)
        {
            switch (_cycle[i])
            {
                case 0: br[i] += 2f / 255f; if (br[i] > 1f) { br[i] = 1f; next = true; } break;
                case 3: br[i] -= 2f / 255f; if (br[i] < 0f) { br[i] = 0f; next = true; } break;
            }
            int tlc = _cycle[i] - 1; if (tlc < 0) tlc = 5;
            switch (tlc)
            {
                case 0: tl[i] = Mathf.Min(tl[i] + 2f / 255f, 1f); break;
                case 3: tl[i] = Mathf.Max(tl[i] - 2f / 255f, 0f); break;
            }
        }
        if (next) for (int i = 0; i < 3; i++) { _cycle[i]++; if (_cycle[i] >= 6) _cycle[i] = 0; }
        _gradBR = new Color(br[0], br[1], br[2]); _gradTL = new Color(tl[0], tl[1], tl[2]);
        ApplyGradient();
    }

    // ===================================================================================================================================
    //  the state machine (TSelectMenu::perform CUE_MOVE), one 30 fps frame
    // ===================================================================================================================================
    private void StartOpenWindow()
    {
        _state = MenuState.LetterboxAnimation;
        _letterboxFrame = 0;
        // the bars grow in from the edges over 30 frames: the top from height 0, the bottom from its base + height (off the bottom) and height 0
        _topAnim.Node = _barTop; _topAnim.BasePos = _barTop.Position; _topAnim.BaseSize = _barTop.Size; _topAnim.Frames = 30; _topAnim.T = 0;
        _topAnim.HasSize = true; _topAnim.SizeFrom = new Vector2(_barTop.Size.X, 0f); _topAnim.SizeTo = _barTop.Size; _topAnim.HasOff = false; _topAnim.HasAlpha = false;
        _bottomAnim.Node = _barBottom; _bottomAnim.BasePos = _barBottom.Position; _bottomAnim.BaseSize = _barBottom.Size; _bottomAnim.Frames = 30; _bottomAnim.T = 0;
        _bottomAnim.HasSize = true; _bottomAnim.SizeFrom = new Vector2(_barBottom.Size.X, 0f); _bottomAnim.SizeTo = _barBottom.Size;
        _bottomAnim.HasOff = true; _bottomAnim.OffFrom = new Vector2(0f, _barBottomH); _bottomAnim.OffTo = Vector2.Zero; _bottomAnim.HasAlpha = false;
        _barTop.Size = _topAnim.SizeFrom; _barBottom.Size = _bottomAnim.SizeFrom; _barBottom.Position = _bottomAnim.BasePos + _bottomAnim.OffFrom;
    }

    private void Frame()
    {
        PollInput();
        GradientFrame();
        switch (_state)
        {
            case MenuState.LetterboxAnimation:
            {
                bool updated = _topAnim.Update() & _bottomAnim.Update();
                if (updated || _letterboxFrame > 20)
                {   // the banner slides in from off the right edge: offset 601 - x1 -> 0 over 20 frames
                    _bannerGroup.Visible = true;
                    float x1 = _bannerGroup.Position.X + (_banner.Visible ? _banner.Position.X : -220f);
                    _bannerAnim.Node = _bannerGroup; _bannerAnim.BasePos = _bannerGroup.Position; _bannerAnim.BaseSize = _bannerGroup.Size; _bannerAnim.Frames = 20; _bannerAnim.T = 0;
                    _bannerAnim.HasOff = true; _bannerAnim.OffFrom = new Vector2(601f - x1, 0f); _bannerAnim.OffTo = Vector2.Zero; _bannerAnim.HasSize = false; _bannerAnim.HasAlpha = false;
                    _bannerGroup.Position = _bannerAnim.BasePos + _bannerAnim.OffFrom;
                    _state = MenuState.StageBannerSlide;
                }
                _letterboxFrame++;
                break;
            }
            case MenuState.StageBannerSlide:
            {
                bool updated = _topAnim.Update() & _bottomAnim.Update() & _bannerAnim.Update();
                if (updated)
                {   // the squash against the left: 15 frames, up 10 and 160 narrower / 20 taller
                    Bound(_bannerText, _banner, 15, new Vector2(0f, 0f), new Vector2(0f, -6f), new Vector2(0f, -10f), new Vector2(0f, 0f), new Vector2(-110f, 12f), new Vector2(-160f, 20f));
                    Bound(_bannerShadowText, _bannerShadow, 15, new Vector2(0f, 0f), new Vector2(0f, -6f), new Vector2(0f, -10f), new Vector2(0f, 0f), new Vector2(-110f, 12f), new Vector2(-160f, 20f));
                    _state = MenuState.StageBannerSquash;
                }
                break;
            }
            case MenuState.StageBannerSquash:
                if (_bannerText.Update() & _bannerShadowText.Update())
                {   // the stretch: 20 frames to 40 wider / 8 shorter, 4 down
                    Bound(_bannerText, _banner, 20, new Vector2(0f, -10f), new Vector2(0f, -6f), new Vector2(0f, 4f), new Vector2(-160f, 20f), new Vector2(-110f, 12f), new Vector2(40f, -8f));
                    Bound(_bannerShadowText, _bannerShadow, 20, new Vector2(0f, -10f), new Vector2(0f, -6f), new Vector2(0f, 4f), new Vector2(-160f, 20f), new Vector2(-110f, 12f), new Vector2(40f, -8f));
                    _state = MenuState.StageBannerStretch;
                }
                break;
            case MenuState.StageBannerStretch:
                if (_bannerText.Update() & _bannerShadowText.Update())
                {   // the settle: 15 frames back to its size
                    Bound(_bannerText, _banner, 15, new Vector2(0f, 4f), new Vector2(0f, 3f), new Vector2(0f, 0f), new Vector2(40f, -8f), new Vector2(15f, -6f), new Vector2(0f, 0f));
                    Bound(_bannerShadowText, _bannerShadow, 15, new Vector2(0f, 4f), new Vector2(0f, 3f), new Vector2(0f, 0f), new Vector2(40f, -8f), new Vector2(15f, -6f), new Vector2(0f, 0f));
                    _stageNameText.Visible = true; _stageNameText.Modulate = new Color(1f, 1f, 1f, 0f);
                    _pane1.Visible = true; _pane1.Modulate = new Color(1f, 1f, 1f, 0f);
                    _marksGroup.Visible = true; _marksGroup.Modulate = new Color(1f, 1f, 1f, 0f);
                    _scoreGroup.Visible = ShowScore; _scoreGroup.Modulate = new Color(1f, 1f, 1f, 0f);
                    _state = MenuState.AppearMenu;
                }
                break;
            case MenuState.AppearMenu:
            {
                bool updated = _bannerText.Update() & _bannerShadowText.Update();
                float stageAlpha = Mathf.Min(_stageNameText.Modulate.A + 6f / 255f, 1f);
                _stageNameText.Modulate = new Color(1f, 1f, 1f, stageAlpha);
                float menuAlpha = 0f;
                if (stageAlpha > 128f / 255f)
                {
                    menuAlpha = Mathf.Min(_pane1.Modulate.A + 6f / 255f, 1f);
                    _pane1.Modulate = new Color(1f, 1f, 1f, menuAlpha); _marksGroup.Modulate = new Color(1f, 1f, 1f, menuAlpha); _scoreGroup.Modulate = new Color(1f, 1f, 1f, menuAlpha);
                }
                if (updated && menuAlpha >= 1f) _state = MenuState.MenuInputLoop;
                break;
            }
            case MenuState.WaitBeforeClose:
                if (_closeWaitFrame > 10) _state = MenuState.CloseMenu;
                _closeWaitFrame++;
                break;
            case MenuState.MenuInputLoop:
                if (Pressed("button_a") || Pressed("ui_accept"))
                {
                    Pick();
                    break;
                }
                else if (Pressed("Lstick_left") || Pressed("ui_left"))
                {
                    int prev = PrevIndex();
                    if (prev != -1) { Step(prev, false); }
                    else break;
                }
                else if (Pressed("Lstick_right") || Pressed("ui_right"))
                {
                    int next = NextIndex();
                    if (next != -1) { Step(next, true); }
                    else break;
                }
                goto case MenuState.MenuAnimLoop;
            case MenuState.MenuAnimLoop:
                AnimLoop();
                break;
            case MenuState.DisappearMenu:
                Disappear();
                break;
            case MenuState.CloseMenu:
                _closeMenu = true;
                break;
        }
        _ring.Frame();
    }

    private void Bound(BoundAnim a, Control node, int frames, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 s0, Vector2 s1, Vector2 s2)
    {
        if (node == null) return;
        if (a.Node != node) { a.Node = node; a.BasePos = node.Position + node.Size * 0.5f; a.BaseSize = node.Size; a.BasePos -= node.Size * 0.5f; }
        a.Frames = frames; a.T = 0; a.P0 = p0; a.P1 = p1; a.P2 = p2; a.S0 = s0; a.S1 = s1; a.S2 = s2; a.Active = true;
    }

    private int NextIndex()
    {
        for (int i = _selected + 1; i < 8; i++) if (_unlock[i] == 2 || _unlock[i] == 3) return i;
        return -1;
    }

    private int PrevIndex()
    {
        for (int i = _selected - 1; i >= 0; i--) if (_unlock[i] == 2 || _unlock[i] == 3) return i;
        return -1;
    }

    /// <summary>A step left (right = false) or right: the ring turns, the old pane slides out as the new slides in over 10 frames.</summary>
    private void Step(int to, bool right)
    {
        CursorSound?.Play();
        if (right) _ring.StartIncrease(to - _selected); else _ring.StartDecrease(_selected - to);
        _selectNext = right;
        float dist = 144f;
        // pane2 carries the NEW episode and slides into pane1's place; pane1 (the old) slides out the other way; both over 10 frames with the alpha crossing
        _pane2.Visible = true;
        Ex(_pane2Anim, _pane2, 10, new Vector2(-dist, 0f), right ? new Vector2(-dist, 0f) + new Vector2(dist, 0f) : new Vector2(-dist * 2f, 0f), 1f, 0f);
        _pane1.Visible = true;
        Ex(_pane1Anim, _pane1, 10, right ? new Vector2(-dist, 0f) : new Vector2(dist, 0f), Vector2.Zero, 0f, 1f);
        SetPane(_pane2, _num2, _numS2, _name2, to);
        SetPane(_pane1, _num1, _numS1, _name1, _selected);
        SetMark(_selected, false);
        _selected = to;
        SetMark(_selected, true);
        SetPreview(_selected);
        if (_num > 1)
        {
            if (_selected != 0 && !_arrowL.Visible) _arrowL.Visible = true;
            if (_selected != _num - 1 && !_arrowR.Visible) _arrowR.Visible = true;
        }
        _state = MenuState.MenuAnimLoop;
        if (DebugPrint) GD.Print($"[SMS select] {(right ? "RIGHT" : "LEFT")} -> episode {_selected + 1} '{_stage.EpisodeName(_selected)}'");
    }

    /// <summary>TExPane::setPaneOffset / setPaneAlpha(time, to, from) on a pane: the offset is from its rest place (pane1 at 300, pane2 at 444).</summary>
    private void Ex(ExAnim a, Control node, int frames, Vector2 offTo, Vector2 offFrom, float alphaTo, float alphaFrom)
    {
        a.Node = node; a.BasePos = node == _pane1 ? new Vector2(300f, 299f - FrameTop) : new Vector2(444f, 299f - FrameTop); a.BaseSize = node.Size;
        a.Frames = frames; a.T = 0; a.HasOff = true; a.OffFrom = offFrom; a.OffTo = offTo; a.HasSize = false; a.HasAlpha = true; a.AlphaFrom = alphaFrom; a.AlphaTo = alphaTo;
        node.Position = a.BasePos + offFrom; node.Modulate = new Color(1f, 1f, 1f, alphaFrom);
    }

    /// <summary>MENU_ANIM_LOOP: the arrows' bob and fades, the pane slide, the selected mark's pulse.</summary>
    private void AnimLoop()
    {
        if (_num > 1)
        {
            float x = _arrowPos * 0.5f;
            if (_arrowL.Visible) _arrowL.Position = new Vector2(_arrowLHome.X - x, _arrowLHome.Y);
            if (_arrowR.Visible) _arrowR.Position = new Vector2(_arrowRHome.X + x, _arrowRHome.Y);
            if (_arrowDirUp) { _arrowPos++; if (_arrowPos > 10) _arrowDirUp = false; }
            else { _arrowPos--; if (_arrowPos == 0) _arrowDirUp = true; }
            FadeArrow(_arrowL, _selected == 0);
            FadeArrow(_arrowR, NextIndex() == -1);
        }
        if (_state != MenuState.MenuInputLoop)
        {
            bool updated = _pane1Anim.Update() & _pane2Anim.Update();
            if (updated) { _pane1.Visible = false; _state = MenuState.MenuInputLoop; }
        }
        var m = _marks[_selected];
        if (m != null)
        {
            float a = m.Modulate.A;
            if (_markPulseUp) { a += 6f / 255f; if (a > SelectedMarkAlpha) { _markPulseUp = false; a = SelectedMarkAlpha; } }
            else { a -= 6f / 255f; if (a < 64f / 255f) { _markPulseUp = true; a = 64f / 255f; } }
            Color c = m.Modulate; c.A = a; m.Modulate = c;
        }
    }

    private void FadeArrow(TextureRect arrow, bool atEnd)
    {
        float a = arrow.Modulate.A;
        if (atEnd)
        {
            if (arrow.Visible && a > 0f) { a -= 4f / 255f; if (a <= 0f) { a = 0f; arrow.Visible = false; } }
        }
        else if (a < MarkAlpha) { a = Mathf.Min(a + 4f / 255f, MarkAlpha); }
        Color c = arrow.Modulate; c.A = a; arrow.Modulate = c;
    }

    /// <summary>A: the pick -- the bars close to the middle over 30 frames, the ring closes, the burst, DISAPPEAR.</summary>
    private void Pick()
    {
        SelectSound?.Play();
        // the top bar grows to the middle (blo y 224), the bottom's top edge moves up to it
        _topAnim.Node = _barTop; _topAnim.BasePos = _barTop.Position; _topAnim.BaseSize = _barTop.Size; _topAnim.Frames = 30; _topAnim.T = 0;
        _topAnim.HasSize = true; _topAnim.SizeFrom = _barTop.Size; _topAnim.SizeTo = new Vector2(_barTop.Size.X, 224f); _topAnim.HasOff = false; _topAnim.HasAlpha = false;
        _bottomAnim.Node = _barBottom; _bottomAnim.BasePos = _barBottom.Position; _bottomAnim.BaseSize = _barBottom.Size; _bottomAnim.Frames = 30; _bottomAnim.T = 0;
        _bottomAnim.HasSize = true; _bottomAnim.SizeFrom = _barBottom.Size; _bottomAnim.SizeTo = new Vector2(_barBottom.Size.X, 480f - 224f);
        _bottomAnim.HasOff = true; _bottomAnim.OffFrom = Vector2.Zero; _bottomAnim.OffTo = new Vector2(0f, 224f - 314f); _bottomAnim.HasAlpha = false;
        _ring.StartClose();
        if (PickBurstScene != null)
        {
            var burst = PickBurstScene.Instantiate<Node2D>();
            _frame.AddChild(burst);
            burst.Position = new Vector2(300f, 244f - FrameTop);
            if (burst.HasMethod("SmsBurst")) burst.Call("SmsBurst", _unlock[_selected] == 3);
        }
        EmitSignal(SignalName.Picked, _selected);
        _state = MenuState.DisappearMenu;
        if (DebugPrint) GD.Print($"[SMS select] PICKED episode {_selected + 1} '{_stage.EpisodeName(_selected)}'");
    }

    /// <summary>DISAPPEAR_MENU: the names and panes fade 16 a frame, the bars close and go opaque (+16 a frame); when all is done, the wait.</summary>
    private void Disappear()
    {
        bool updated = true;
        float alpha = _stageNameText.Modulate.A - 16f / 255f;
        if (alpha <= 0f) alpha = 0f; else updated = false;
        _stageNameText.Modulate = new Color(1f, 1f, 1f, alpha);
        _bannerGroup.Modulate = new Color(1f, 1f, 1f, alpha);
        _pane1.Modulate = new Color(1f, 1f, 1f, alpha); _pane2.Modulate = new Color(1f, 1f, 1f, alpha);
        _marksGroup.Modulate = new Color(1f, 1f, 1f, alpha); _scoreGroup.Modulate = new Color(1f, 1f, 1f, alpha);
        _preview.Modulate = new Color(1f, 1f, 1f, alpha); if (_previewFrame != null) _previewFrame.Modulate = new Color(1f, 1f, 1f, alpha);
        updated &= _topAnim.Update(); updated &= _bottomAnim.Update();
        foreach (var bar in new[] { _barTop, _barBottom })
        {
            float a = bar.Modulate.A + 16f / 255f;
            if (a > 1f) a = 1f; else updated = false;
            bar.Modulate = new Color(1f, 1f, 1f, a);
        }
        if (updated) { _state = MenuState.WaitBeforeClose; _closeWaitFrame = 0; }
    }

    // ===================================================================================================================================
    //  the director (TSelectDir::direct) and the clock
    // ===================================================================================================================================
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) { LayoutStatic(); return; }
        LayoutStatic();
        _acc += delta * FrameRate;
        int guard = 0;
        while (_acc >= 1.0 && guard++ < 6) { _acc -= 1.0; Frame(); }
        if (_acc > 6.0) _acc = 0.0;
        if (_closeMenu && !_done)
        {
            if (_fader == null || _fader.IsFullyFadedIn || _fader.State == SmsFader.Status.FadingIn)
            {
                if (!_fadeStarted)
                {   // the pick is MSF_EPISODE; startWipe(0xf, 1.0) in WHITE; the sound fades
                    _fadeStarted = true;
                    if (GameData.Instance != null) { GameData.Instance.Episode = _selected; GameData.Instance.PendingEpisode = _selected; GameData.Instance.PendingStage = _stage; }
                    if (_fader != null) { _fader.SetColor(Colors.White); _fader.StartWipe(SmsFader.Wipe.FadeOut, 0f, 1f); }
                    if (Music != null && Music.Playing) { var tw = CreateTween(); tw.TweenProperty(Music, "volume_db", -40f, 1f); }
                    if (DebugPrint) GD.Print($"[SMS select] closing: episode {_selected + 1}, the white fade");
                }
            }
            if (_fadeStarted && (_fader == null || _fader.IsFullyFadedOut) && !_moved)
            {
                _moved = true; _done = true;
                MoveOn();
            }
        }
    }

    /// <summary>APP_STATE_GAMEPLAY with the next area set: his character select first when the stage has one, else the level.</summary>
    private void MoveOn()
    {
        string level = _stage.ScenePath;
        string select = SmsReturnSpot.ScenePath(_stage.CharacterSelectScene ?? "");
        string next = select != "" && ResourceLoader.Exists(select) ? select : level;
        if (GameData.Instance != null) GameData.Instance.FadeInFrom = Colors.White;
        if (next == "")
        {
            GD.PushWarning("[SMS select] the stage has no scene: nothing to load");
            return;
        }
        if (DebugPrint) GD.Print($"[SMS select] -> {next}{(next != level ? " then " + level : "")}");
        GetTree().ChangeSceneToFile(next);
    }

    private static readonly string[] Actions = { "button_a", "ui_accept", "Lstick_left", "ui_left", "Lstick_right", "ui_right" };
    private readonly HashSet<string> _pressed = new();

    /// <summary>Every frame: which actions went down since the last frame -- read here, not Input.IsActionJustPressed, which is stamped per
    /// phase and misses a drive's SceneTree-timed presses (pass 59). checkFrameMeaning's edge.</summary>
    private void PollInput()
    {
        _pressed.Clear();
        foreach (string action in Actions)
        {
            if (!InputMap.HasAction(action)) continue;
            bool now = Input.IsActionPressed(action);
            _held.TryGetValue(action, out bool was);
            _held[action] = now;
            if (now && !was) _pressed.Add(action);
        }
        if (DebugPrint && _pressed.Count > 0) GD.Print($"[SMS select] pressed: {string.Join(" ", _pressed)} in {_state}");
    }

    private bool Pressed(string action) => _pressed.Contains(action);
}
