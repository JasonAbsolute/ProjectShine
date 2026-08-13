using Godot;

/// <summary>
/// A celebratory word banner shown during the collect cutscene — "SHINE!" by
/// default, but any text via <see cref="PopupText"/> (e.g. "AMAZING!").
///
/// The letters use the real SMS bubbly glyph graphics: one AtlasTexture .tres per
/// character, looked up DYNAMICALLY at runtime by name via <see cref="GlyphPathFormat"/>.
/// For each character in the text we load res://Font/HudElements/ShineLetter{TOKEN}.tres
/// (TOKEN = the upper-cased letter, or "Excl" for '!', etc.), and build a
/// TextureRect for it — sized from the glyph's own aspect ratio, coloured from the
/// cycling palette, arched, tilted and animated. Nothing about the word is baked
/// into the scene, so adding new ShineLetter*.tres files is all it takes to spell
/// new words. Characters with no matching .tres are skipped.
///
/// Behaviour:
///   * The banner sits low-and-left of screen center with an ascending-right tilt
///     and 0.8 scale (see the Banner node transform in the .tscn). Letters are
///     laid out centered around the Banner origin with a gentle arch and measured
///     spacing so glyphs never overlap.
///   * Letters appear FIRST-TO-LAST (left-to-right), each popping in with a springy
///     scale bounce and a quick fade.
///   * As each letter appears, a soft fluffy light streak (blue glow halo + white
///     core) swooshes out of it to the right — so the streaks cascade with the
///     letters rather than all at once.
///   * The instant a letter settles, a burst of stars erupts from its center.
///   * After the last letter settles and a short hold, the whole banner fades out
///     and frees itself (along with the CanvasLayer wrapper Mario spawns it in).
///
/// Spawned fresh per shine (see Mario.SpawnShineGetPopup). Set PopupText BEFORE it
/// enters the tree (right after Instantiate) to change the word.
/// </summary>
public partial class ShineGetPopup : Control
{
    /// <summary>The word to display. Rendered upper-cased using the glyph .tres set.</summary>
    [Export]
    public string PopupText = "SHINE!";

    // {0} is replaced with a per-character token (upper-case letter, or "Excl"/…).
    [Export]
    public string GlyphPathFormat = "res://Font/HudElements/ShineLetter{0}.tres";

    [Export]
    public float GlyphHeight = 124f;

    [Export]
    public float LetterGap = 4f;

    [Export]
    public float ArchHeight = 20f;

    // Per-letter colours, cycled across the word. Defaults to the SMS palette.
    [Export]
    public Color[] Palette =
    {
        new Color(0.16f, 0.71f, 0.24f), // green
        new Color(0.96f, 0.36f, 0.62f), // pink
        new Color(1f, 0.83f, 0.16f), // yellow
        new Color(0.18f, 0.76f, 0.72f), // teal
        new Color(0.98f, 0.52f, 0.13f), // orange
        new Color(0.96f, 0.28f, 0.4f), // pink-red
    };

    // Entrance / hold / fade timing.
    [Export]
    public float AppearDelay = 3.5f;

    [Export]
    public float LetterSlideDuration = 0.34f;

    [Export]
    public float LetterStagger = 0.09f;

    [Export]
    public float HoldDuration = 2.0f;

    [Export]
    public float FadeOutDuration = 0.4f;

    // Star-burst tuning.
    [Export]
    public int StarsPerBurst = 9;

    [Export]
    public float BurstMinDistance = 80f;

    [Export]
    public float BurstMaxDistance = 160f;

    // Fluffy "swoosh" glow streaks (blue halo + white core) — one (or more) shoots
    // out of each letter as it appears, sweeping right.
    [Export]
    public int SwooshPerLetter = 3;

    [Export]
    public Color SwooshGlowColor = new Color(0.45f, 0.72f, 1f, 1f);

    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    private Control _banner;
    private Texture2D _glowTex;

    public override void _Ready()
    {
        _rng.Randomize();
        Modulate = new Color(1, 1, 1, 1);

        _banner = GetNodeOrNull<Control>("Banner");
        if (_banner == null)
            return;

        var letters = BuildLetters();

        float lastArrival = AppearDelay;
        int count = letters.Count;
        for (int treeIndex = 0; treeIndex < count; treeIndex++)
        {
            Control letter = letters[treeIndex];

            Vector2 rest = letter.Position;
            Vector2 letterCenter = rest + letter.Size / 2f;
            Color tint = letter.SelfModulate;

            // Start scaled-down and invisible; bounce up to full size in place.
            Vector2 restScale = letter.Scale;
            letter.Scale = restScale * 0.4f;
            letter.Modulate = new Color(1, 1, 1, 0);

            // Left-to-right cascade: the first (left-most) glyph animates first.
            float delay = AppearDelay + treeIndex * LetterStagger;

            // A streak swooshes out of this letter as it shows up.
            for (int s = 0; s < SwooshPerLetter; s++)
                SpawnSwooshStreak(letterCenter, delay + s * 0.02f);

            var t = CreateTween();
            t.TweenInterval(delay);
            t.TweenProperty(letter, "modulate:a", 1f, 0.12f);
            t.Parallel()
                .TweenProperty(letter, "scale", restScale, LetterSlideDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            t.TweenCallback(Callable.From(() => SpawnStarBurst(letterCenter, tint)));

            lastArrival = Mathf.Max(lastArrival, delay + LetterSlideDuration);
        }

        var fade = CreateTween();
        fade.TweenInterval(lastArrival + HoldDuration);
        fade.TweenProperty(this, "modulate:a", 0f, FadeOutDuration);
        fade.TweenCallback(Callable.From(FinishAndFree));
    }

    /// <summary>Maps a character to its glyph-resource token, or null to skip.</summary>
    private static string GlyphToken(char c)
    {
        if (char.IsLetter(c))
            return char.ToUpper(c).ToString();
        switch (c)
        {
            case '!':
                return "Excl";
            case '?':
                return "Ques";
            case '.':
                return "Dot";
            case ',':
                return "Comma";
            case '\'':
                return "Apos";
            default:
                return null;
        }
    }

    /// <summary>
    /// Loads a glyph .tres per visible character of <see cref="PopupText"/> and
    /// lays the TextureRects out centered around the Banner origin with measured
    /// spacing, a gentle arch, a small per-letter tilt and a cycling palette
    /// colour. Missing glyphs are skipped (whitespace inserts a gap). Returns the
    /// letters in left-to-right order.
    /// </summary>
    private System.Collections.Generic.List<Control> BuildLetters()
    {
        var result = new System.Collections.Generic.List<Control>();
        string text = (PopupText ?? "").ToUpper();
        if (text.Length == 0)
            return result;

        var textures = new Texture2D[text.Length];
        var widths = new float[text.Length];
        int placedCount = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];

            if (char.IsWhiteSpace(ch))
            {
                widths[i] = GlyphHeight * 0.4f;
                continue;
            }

            string token = GlyphToken(ch);
            if (token != null)
            {
                string path = string.Format(GlyphPathFormat, token);
                if (ResourceLoader.Exists(path))
                    textures[i] = GD.Load<Texture2D>(path);
            }

            if (textures[i] == null)
            {
                if (token != null)
                    GD.PushWarning(
                        $"ShineGetPopup: no glyph for '{ch}' at {string.Format(GlyphPathFormat, token)} — skipped"
                    );
                continue;
            }

            Vector2 ts = textures[i].GetSize();
            widths[i] = ts.Y > 0f ? GlyphHeight * (ts.X / ts.Y) : GlyphHeight;
            placedCount++;
        }

        if (placedCount == 0)
            return result;

        float total = 0f;
        int spanning = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (textures[i] != null || char.IsWhiteSpace(text[i]))
            {
                total += widths[i];
                spanning++;
            }
        }
        total += LetterGap * Mathf.Max(0, spanning - 1);

        float cursor = -total / 2f;
        int colorIndex = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (textures[i] == null)
            {
                if (char.IsWhiteSpace(text[i]))
                    cursor += widths[i] + LetterGap;
                continue;
            }

            float w = widths[i];

            // Gentle arch: middle letters ride a touch higher (negative = up).
            float tCenter = text.Length > 1 ? (i / (float)(text.Length - 1)) * 2f - 1f : 0f;
            float archY = ArchHeight * (tCenter * tCenter - 1f);

            var glyph = new TextureRect
            {
                Texture = textures[i],
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
                SelfModulate =
                    Palette.Length > 0 ? Palette[colorIndex % Palette.Length] : Colors.White,
            };
            glyph.Size = new Vector2(w, GlyphHeight);
            glyph.Position = new Vector2(cursor, -GlyphHeight / 2f + archY);
            glyph.PivotOffset = glyph.Size / 2f;
            glyph.Rotation = (colorIndex % 2 == 0 ? -1f : 1f) * 0.06f;

            _banner.AddChild(glyph);
            result.Add(glyph);

            cursor += w + LetterGap;
            colorIndex++;
        }

        return result;
    }

    private Texture2D GlowTexture()
    {
        if (_glowTex != null)
            return _glowTex;

        var grad = new Gradient
        {
            Offsets = new float[] { 0f, 0.45f, 1f },
            Colors = new Color[]
            {
                new Color(1, 1, 1, 1),
                new Color(1, 1, 1, 0.55f),
                new Color(1, 1, 1, 0),
            },
        };
        _glowTex = new GradientTexture2D
        {
            Gradient = grad,
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1.0f, 0.5f),
        };
        return _glowTex;
    }

    private TextureRect MakeGlowRect(Vector2 size, Color color)
    {
        var tr = new TextureRect
        {
            Texture = GlowTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = color,
        };
        tr.Size = size;
        tr.Position = -size / 2f;
        return tr;
    }

    /// <summary>
    /// One fluffy light streak (blue glow halo + white core) that starts at
    /// <paramref name="origin"/> (a letter's center, in Banner-local space) and
    /// swooshes out to the right as the letter appears. Fires after
    /// <paramref name="delay"/> so it cascades with the letters.
    /// </summary>
    private void SpawnSwooshStreak(Vector2 origin, float delay)
    {
        if (_banner == null)
            return;

        var streak = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = -1,
            Modulate = new Color(1, 1, 1, 0),
        };

        float len = _rng.RandfRange(180f, 300f);
        float glowThick = _rng.RandfRange(22f, 38f);
        float coreThick = _rng.RandfRange(6f, 12f);

        streak.AddChild(
            MakeGlowRect(
                new Vector2(len, glowThick),
                new Color(SwooshGlowColor.R, SwooshGlowColor.G, SwooshGlowColor.B, 0.7f)
            )
        );
        streak.AddChild(
            MakeGlowRect(new Vector2(len * 0.82f, coreThick), new Color(1f, 1f, 1f, 1f))
        );

        Vector2 dir = Vector2.Right;
        Vector2 startPos =
            origin
            + new Vector2(
                _rng.RandfRange(-30f, 20f),
                _rng.RandfRange(-GlyphHeight * 0.5f, GlyphHeight * 0.5f)
            );
        streak.Position = startPos;
        _banner.AddChild(streak);

        float travel = _rng.RandfRange(260f, 470f);
        float dur = _rng.RandfRange(0.5f, 0.64f); // slower than before
        float peakAlpha = _rng.RandfRange(0.55f, 0.85f);

        var t = CreateTween();
        t.TweenInterval(Mathf.Max(0f, delay));
        t.TweenProperty(streak, "modulate:a", peakAlpha, 0.06f);
        t.Parallel()
            .TweenProperty(streak, "position", startPos + dir * travel, dur)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        t.Parallel().TweenProperty(streak, "modulate:a", 0f, dur * 0.45f).SetDelay(0.06f);
        t.Chain().TweenCallback(Callable.From(streak.QueueFree));
    }

    private void SpawnStarBurst(Vector2 center, Color letterTint)
    {
        if (_banner == null)
            return;

        Color burstColor = letterTint.Lerp(new Color(1f, 1f, 0.72f), 0.55f);

        for (int k = 0; k < StarsPerBurst; k++)
        {
            var star = new Label
            {
                Text = "★",
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 5,
            };
            int fontSize = _rng.RandiRange(26, 48);
            star.AddThemeFontSizeOverride("font_size", fontSize);
            star.AddThemeColorOverride("font_color", burstColor);
            _banner.AddChild(star);

            star.Position = center - new Vector2(fontSize, fontSize) * 0.5f;
            star.PivotOffset = new Vector2(fontSize, fontSize) * 0.5f;
            star.Scale = Vector2.One * 0.2f;

            float angle = (Mathf.Tau * k / StarsPerBurst) + _rng.RandfRange(-0.35f, 0.35f);
            float dist = _rng.RandfRange(BurstMinDistance, BurstMaxDistance);
            Vector2 target = star.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

            var st = CreateTween();
            st.SetParallel(true);
            st.TweenProperty(star, "position", target, 0.5f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            st.TweenProperty(star, "scale", Vector2.One * 1.15f, 0.18f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            st.TweenProperty(star, "modulate:a", 0f, 0.5f).SetEase(Tween.EaseType.In);
            st.Chain().TweenCallback(Callable.From(star.QueueFree));
        }
    }

    private void FinishAndFree()
    {
        if (GetParent() is CanvasLayer layer)
            layer.QueueFree();
        else
            QueueFree();
    }
}
