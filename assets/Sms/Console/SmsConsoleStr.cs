using Godot;

/// <summary>
/// The console's big text -- TConsoleStr (GC2D/ConsoleStr.cpp) over the HUD: the "TOO BAD!" of a miss. In the game the letters are
/// big_tx_1.blo's panes ms00..ms06 (78 x 78 at y 338 on its 600 x 480 screen, textures big_tx_t/o/o/b/a/d/ex, white); here they are
/// built from <see cref="MissText"/> by <see cref="SmsBigText"/> about the blo's word centre and tinted from the palette (DEVIATION,
/// Jaysonn's: the game's word is fixed and white; a Level's MissText overrides the text). startAppearMiss (315) then processMiss (554)
/// choreograph them, one frame per console frame (120 Hz ticks in this port, pass 38; the game's TConsoleStr counts 0.5 a frame and
/// doubles it): letter i starts at frame i x 10 -- a quadratic drop from 270 above to 30 below its place over 60 frames (TBoundPane's
/// bezier with the control point at the start), one full turn on the way (rotation (start - frame) x 6 degrees), alpha +12 a frame --
/// then at +60 a bounce 110 up over 40 (the control point at the end), at +100 back to its place over 40, at +300 down and out over
/// 30, hidden past +360; the stage has long reloaded by then. The sparkle each letter makes as it appears (emitter 0x1F9) is not
/// ported. The director starts this the tick after MARIO_FLAG_GAME_OVER.
/// </summary>
public partial class SmsConsoleStr : CanvasLayer
{
    /// <summary>The TOO BAD! group: the blo's 600 x 480 screen; the letters are built under it.</summary>
    [Export] public Control Miss;
    /// <summary>The blo's screen is 600 x 480; the group is placed about its centre on the viewport.</summary>
    [Export] public Vector2 LayoutCenter = new Vector2(300f, 240f);
    /// <summary>1 = the blo's 480 lines on the window's height.</summary>
    [Export] public float Size = 1.0f;

    /// <summary>What a miss says (the game: TOO BAD!). A Level's MissText, when set, wins. Letters without a glyph are skipped.</summary>
    [Export] public string MissText = "TOO BAD!";
    /// <summary>Where the letters come from; {0} = the letter (lower-case, "ex" for !) -- or a ShineLetter{0}.tres style set.</summary>
    [Export] public string GlyphPathFormat = "res://assets/Sms/hud/big_tx_{0}.png";
    /// <summary>Tint the letters from <see cref="Palette"/> in turn (off = the game's white).</summary>
    [Export] public bool Colored = true;
    [Export] public Color[] Palette = SmsBigText.DefaultPalette();
    /// <summary>The blo's panes are 78 x 78; the word is centred where its T..! sit (x 76..541, y 338..416).</summary>
    [Export] public float GlyphHeight = 78f;
    [Export] public float LetterGap = -8f;
    [Export] public float SpaceWidth = 28f;
    [Export] public Vector2 MissCenter = new Vector2(308.5f, 377f);
    /// <summary>A longer word is built smaller to fit this many blo px (the screen is 600 wide; 760 fills a 16:9 window). 0 = never.</summary>
    [Export] public float MaxWidth = 760f;
    /// <summary>Pass 116: TConsoleStr::startAppearShineGet -- the US big_tx_1.blo's sg00..sg05 spell SHINE! (JP / PAL: SHINE GET!).</summary>
    [Export] public string ShineText = "SHINE!";
    /// <summary>The panes' centres in the blo's 600 x 480 (sg00 (59, 364) .. sg05 (341, 287), 70 x 70; the I's pane reads 417 x 51 in this blo and
    /// is interpolated between H and N).</summary>
    [Export] public Vector2[] ShineHomes = { new Vector2(94f, 399f), new Vector2(153f, 384f), new Vector2(206f, 369f), new Vector2(259f, 355f), new Vector2(318f, 336f), new Vector2(376f, 322f) };
    [Export] public float ShineTiltDeg = 12f;      // the panes' ROT 12 (the I untilted)
    [Export] public float ShineGlyphHeight = 70f;
    /// <summary>116f: the game's letters are white glyphs x each pane's four CORNER COLOURS (big_tx_1.blo): a vertical gradient, light at the
    /// top and saturated at the bottom. false = the flat tint from Palette (116).</summary>
    [Export] public bool SmsShineGetGradient = true;
    /// <summary>sg00..sg05's corner colours, top then bottom per letter (the blo's second pair is the top on screen).</summary>
    [Export] public Color[] ShineGradientTop = { new Color("00ff00"), new Color("ffdcdc"), new Color("ffff50"), new Color("00ffff"), new Color("ffd264"), new Color("ffdcdc") };
    [Export] public Color[] ShineGradientBottom = { new Color("006400"), new Color("ff0064"), new Color("e68c00"), new Color("001eff"), new Color("ff0000"), new Color("ff0064") };
    private static Shader _shineGradShader;
    private const string ShineGradCode = "shader_type canvas_item;\nuniform vec4 top_color : source_color = vec4(1.0);\nuniform vec4 bottom_color : source_color = vec4(1.0);\nuniform float rect_h = 1.0;\nvarying float v_t;\nvoid vertex() { v_t = clamp(VERTEX.y / max(rect_h, 1.0), 0.0, 1.0); }\nvoid fragment() { vec4 t = texture(TEXTURE, UV); COLOR = t * mix(top_color, bottom_color, v_t); }\n";

    private TextureRect[] _letters = System.Array.Empty<TextureRect>();
    private Vector2[] _home = System.Array.Empty<Vector2>();
    private string _built;
    private float _frame = -1f;

    public bool MissRunning => _frame >= 0f;
    private Control _shineRoot; private TextureRect[] _shineLetters = System.Array.Empty<TextureRect>(); private Vector2[] _shineHome = System.Array.Empty<Vector2>();
    private float _shineFrame = -1f;
    public bool ShineGetRunning => _shineFrame >= 0f;
    public float ShineGetFrame => _shineFrame;
    public int ShineLettersVisible { get { int n = 0; foreach (var t in _shineLetters) if (t.Visible) n++; return n; } }
    public Vector2 ShineLetterOffset(int i) => i < _shineLetters.Length ? _shineLetters[i].Position - _shineHome[i] : Vector2.Zero;
    public int LetterCount => _letters.Length;

    public override void _Ready()
    {
        if (Miss == null)
        {
            GD.PushWarning($"[SMS console] {GetPath()}: the scene's Miss export is not set (see assets/Sms/Console/ConsoleStr.tscn)");
            return;
        }
        Build(CurrentText());
        Miss.Visible = false;
    }

    private string CurrentText()
    {
        var level = GetTree()?.CurrentScene as Level;
        return !string.IsNullOrEmpty(level?.MissText) ? level.MissText : MissText;
    }

    /// <summary>(Re)builds the letters from <paramref name="text"/> under <see cref="Miss"/>; the old ones are freed.</summary>
    private void Build(string text)
    {
        foreach (TextureRect t in _letters)
            if (IsInstanceValid(t))
            {
                t.GetParent()?.RemoveChild(t); // out of the tree now: the new letters may take the names
                t.QueueFree();
            }
        var list = SmsBigText.Build(Miss, text, GlyphPathFormat, MissCenter, GlyphHeight, LetterGap, SpaceWidth, Palette, Colored, null, 0f, MaxWidth);
        _letters = list.ToArray();
        _home = new Vector2[_letters.Length];
        for (int i = 0; i < _letters.Length; i++)
        {
            _home[i] = _letters[i].Position;
            _letters[i].Visible = false;
        }
        _built = text;
    }

    /// <summary>TConsoleStr::startAppearMiss: the letters hidden and clear, the choreography from frame 0.</summary>
    public void StartAppearMiss()
    {
        if (Miss == null)
            return;
        string text = CurrentText();
        if (text != _built)
            Build(text);
        _frame = 0f;
        foreach (TextureRect t in _letters)
        {
            t.Visible = false;
            t.Modulate = new Color(1f, 1f, 1f, 0f);
            t.Rotation = 0f;
        }
        Miss.Visible = true;
    }

    /// <summary>TConsoleStr::startAppearShineGet (ConsoleStr.cpp 364): the letters from the start, after the 0xB4 half-tick wait.</summary>
    public void StartAppearShineGet()
    {
        if (_shineRoot == null)
        {
            _shineRoot = new Control { Name = "ShineGet", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_shineRoot);
        }
        if (_shineLetters.Length != ShineText.Length)
        {
            foreach (var t in _shineLetters) if (IsInstanceValid(t)) t.QueueFree();
            var letters = new System.Collections.Generic.List<TextureRect>();
            for (int i = 0; i < ShineText.Length; i++)
            {
                Vector2 home = i < ShineHomes.Length ? ShineHomes[i] : new Vector2(94f + 56f * i, 399f - 15f * i);
                char ch = ShineText[i];
                float tilt = ch == 'I' ? 0f : ShineTiltDeg;
                var pal = Palette.Length > 0 ? new[] { Palette[i % Palette.Length] } : Palette;
                var one = SmsBigText.Build(_shineRoot, ch.ToString(), GlyphPathFormat, home, ShineGlyphHeight, LetterGap, SpaceWidth, pal, Colored && !SmsShineGetGradient, null, tilt, 0f);
                if (SmsShineGetGradient)
                {
                    // 116f: the pane's corner colours as a top -> bottom gradient over the white glyph (the outline stays black: it multiplies)
                    _shineGradShader ??= new Shader { Code = ShineGradCode };
                    foreach (var rect in one)
                    {
                        var mat = new ShaderMaterial { Shader = _shineGradShader };
                        mat.SetShaderParameter("top_color", i < ShineGradientTop.Length ? ShineGradientTop[i] : Colors.White);
                        mat.SetShaderParameter("bottom_color", i < ShineGradientBottom.Length ? ShineGradientBottom[i] : Colors.White);
                        mat.SetShaderParameter("rect_h", Mathf.Max(rect.Size.Y, 1f));
                        rect.Material = mat; rect.SelfModulate = Colors.White;
                    }
                }
                letters.AddRange(one);
            }
            _shineLetters = letters.ToArray();
            _shineHome = new Vector2[_shineLetters.Length];
            for (int i = 0; i < _shineLetters.Length; i++) _shineHome[i] = _shineLetters[i].Position;
        }
        foreach (var t in _shineLetters) { t.Visible = false; t.Modulate = new Color(1f, 1f, 1f, 0f); }
        _shineFrame = 0f;
        _shineRoot.Visible = true;
    }

    /// <summary>processShineGet (570) on the index = ticks past the wait: letter i shows at 6i with a 40-tick Bezier from (150, -50) via (-21, 7) to
    /// (-21, 7) and alpha +7 a tick; at 6i + 40 a 20-tick settle to (0, 0); at 6i + 200 a 40-tick fly-out to (-200, 65); done when all are out.</summary>
    private void ProcessShineGet(float f)
    {
        float idx = f - 360f;   // unk20 = 0xB4 half-ticks: 3.0 s before the first letter
        if (idx < 0f) return;
        bool any = false;
        for (int i = 0; i < _shineLetters.Length; i++)
        {
            TextureRect t = _shineLetters[i];
            float since = idx - 6f * i;
            if (since < 0f || since > 240f) { t.Visible = false; continue; }
            any = true; t.Visible = true;
            Vector2 off;
            if (since < 40f) off = SmsHud.Bezier(new Vector2(150f, -50f), new Vector2(-21f, 7f), new Vector2(-21f, 7f), since / 40f);
            else if (since < 60f) off = SmsHud.Bezier(new Vector2(-21f, 7f), new Vector2(-21f, 7f), Vector2.Zero, (since - 40f) / 20f);
            else if (since < 200f) off = Vector2.Zero;
            else off = SmsHud.Bezier(Vector2.Zero, Vector2.Zero, new Vector2(-200f, 65f), (since - 200f) / 40f);
            t.Position = _shineHome[i] + off;
            float a = since < 40f ? Mathf.Min(1f, since * 7f / 255f) : 1f;
            t.Modulate = new Color(1f, 1f, 1f, a);
        }
        if (!any && idx > 240f) { _shineFrame = -1f; _shineRoot.Visible = false; }
    }

    public void Clear()
    {
        _frame = -1f;
        foreach (TextureRect t in _letters)
            t.Visible = false;
        if (Miss != null)
            Miss.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (Miss == null)
            return;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float s = vp.Y / 480f * Size;
        Miss.Scale = new Vector2(s, s);
        Miss.Position = vp * 0.5f - LayoutCenter * s;
        if (_shineRoot != null) { _shineRoot.Scale = Miss.Scale; _shineRoot.Position = Miss.Position; }
        if (_shineFrame >= 0f) { _shineFrame += (float)delta * 120f * SmsHud.Anim; ProcessShineGet(_shineFrame); }
        if (_frame < 0f)
            return;
        _frame += (float)delta * 120f * SmsHud.Anim;
        ProcessMiss(_frame);
    }

    private void ProcessMiss(float f)
    {
        bool anyVisible = false;
        for (int i = 0; i < _letters.Length; i++)
        {
            TextureRect t = _letters[i];
            float start = i * 10f;
            if (f < start || f > start + 360f)
            {
                t.Visible = false;
                continue;
            }
            anyVisible = true;
            t.Visible = true;
            float since = f - start;
            Vector2 off;
            float rotDeg = 0f;
            if (since < 60f)
            {
                off = SmsHud.Bezier(new Vector2(0f, -270f), new Vector2(0f, -270f), new Vector2(0f, 30f), since / 60f);
                rotDeg = -since * 6f; // (i * 10 - param) * 6: a full turn on the way down
            }
            else if (since < 100f)
                off = SmsHud.Bezier(new Vector2(0f, 30f), new Vector2(0f, -80f), new Vector2(0f, -80f), (since - 60f) / 40f);
            else if (since < 300f)
                off = SmsHud.Bezier(new Vector2(0f, -80f), new Vector2(0f, -80f), Vector2.Zero, (since - 100f) / 40f);
            else
                off = SmsHud.Bezier(Vector2.Zero, Vector2.Zero, new Vector2(0f, 150f), (since - 300f) / 30f);
            t.Position = _home[i] + off;
            t.Rotation = Mathf.DegToRad(rotDeg);
            float a = Mathf.Min(1f, since * 12f / 255f);
            t.Modulate = new Color(1f, 1f, 1f, a);
        }
        if (!anyVisible && f > 360f)
            Clear();
    }
}
