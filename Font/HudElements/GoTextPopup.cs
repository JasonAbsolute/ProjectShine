using System.Collections.Generic;
using Godot;

/// <summary>
/// The "GO!" banner shown when Mario spawns into a level — same glyph-texture
/// approach as <see cref="ShineGetPopup"/> (one AtlasTexture .tres per character,
/// looked up dynamically via <see cref="GlyphPathFormat"/>), but a different
/// entrance/exit tuned to match the reference clip (GOTest.mp4):
///
///   * Letters pop in LEFT-TO-RIGHT, staggered, each rising up from BELOW its
///     rest position (mirrors the drop-from-above wave in LifeCounterHud.cs)
///     with a springy Back-ease overshoot on both position and scale.
///   * After a hold, the letters separate and shoot UP AND OUTWARD in a fan —
///     the leftmost letter drifts up-left, the rightmost up-right, spread
///     scaling with letter count (a 2-letter word fans less than a 5-letter
///     one). Each letter shrinks and fades as it flies.
///   * Each flying letter drags a comet-tail of ITSELF: faded, shrinking ghost
///     copies of that letter's own glyph spawned along its flight path — not a
///     generic glow streak like Shine Get's swoosh. A few star sparkles drift
///     alongside for extra sparkle.
///
/// Spawned fresh per use (see Level.SpawnGoTextPopup). Set PopupText BEFORE it
/// enters the tree (right after Instantiate) to change the word.
/// </summary>
public partial class GoTextPopup : Control
{
    /// <summary>The word to display. Rendered upper-cased using the glyph .tres set.</summary>
    [Export]
    public string PopupText = "GO!";

    // {0} is replaced with a per-character token (upper-case letter, or "Excl"/…).
    [Export]
    public string GlyphPathFormat = "res://Font/HudElements/ShineLetter{0}.tres";

    [Export]
    public float GlyphHeight = 124f;

    [Export]
    public float LetterGap = 4f;

    // Per-letter colours, cycled across the word. Defaults matched to the
    // reference clip: G orange-red, O yellow, ! pink.
    [Export]
    public Color[] Palette =
    {
        new Color(0.95f, 0.34f, 0.14f), // orange-red
        new Color(1f, 0.82f, 0.15f), // yellow
        new Color(0.95f, 0.25f, 0.55f), // pink
    };

    // Entrance: each letter starts this far BELOW its rest position (and
    // scaled down), then springs up into place.
    [Export]
    public float RiseOffset = 220f;

    [Export]
    public float StartScale = 0.4f;

    [Export]
    public float LetterPopDuration = 0.35f;

    [Export]
    public float LetterStagger = 0.1f;

    // How long the settled word holds still before shooting away. Matched to
    // the reference clip (letters settle ~t1.2s, separate ~t2.1s).
    [Export]
    public float HoldDuration = 0.8f;

    // Exit: letters fan out from straight-up. Total spread widens with more
    // letters, capped at MaxSpreadDegrees.
    [Export]
    public float SpreadDegreesPerLetter = 18f;

    [Export]
    public float MaxSpreadDegrees = 70f;

    [Export]
    public float ExitDuration = 0.7f;

    [Export]
    public float ExitTravelDistance = 320f;

    [Export]
    public float ExitShrinkTo = 0.5f;

    // Letter afterimage trail.
    [Export]
    public int TrailGhostCount = 5;

    [Export]
    public float TrailGhostFadeDuration = 0.28f;

    // Sparkles spawn continuously (one every SparkleSpawnInterval) from each
    // letter's live position for as long as it's flying, instead of a one-shot
    // burst — a steady stream "shooting out" of the text until it's gone.
    [Export]
    public float SparkleSpawnInterval = 0.039f;

    [Export]
    public Vector2 SparkleScaleRange = new Vector2(0.5f, 1.1f);

    // Each individual sparkle's own travel distance/lifetime — short, since a
    // continuous stream reads better with quick little flicks than a few long
    // journeys. It fades out exactly as its movement ends, never sitting still.
    [Export]
    public float SparkleTravelDistance = 90f;

    [Export]
    public Vector2 SparkleLifetimeRange = new Vector2(0.3f, 0.5f);

    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    private Control _banner;

    public override void _Ready()
    {
        _rng.Randomize();
        Modulate = new Color(1, 1, 1, 1);

        _banner = GetNodeOrNull<Control>("Banner");
        if (_banner == null)
            return;

        var letters = BuildLetters();
        int count = letters.Count;
        if (count == 0)
            return;

        float lastArrival = 0f;
        var restPositions = new Vector2[count];
        var restScales = new Vector2[count];
        var tints = new Color[count];

        for (int i = 0; i < count; i++)
        {
            Control letter = letters[i];
            Vector2 rest = letter.Position;
            Vector2 restScale = letter.Scale;

            restPositions[i] = rest;
            restScales[i] = restScale;
            tints[i] = letter.SelfModulate;

            letter.Position = rest + new Vector2(0f, RiseOffset);
            letter.Scale = restScale * StartScale;
            letter.Modulate = new Color(1, 1, 1, 0);

            float delay = i * LetterStagger;

            var t = CreateTween();
            t.TweenInterval(delay);
            t.TweenProperty(letter, "modulate:a", 1f, 0.12f);
            t.Parallel()
                .TweenProperty(letter, "position", rest, LetterPopDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            t.Parallel()
                .TweenProperty(letter, "scale", restScale, LetterPopDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);

            lastArrival = Mathf.Max(lastArrival, delay + LetterPopDuration);
        }

        var exitTimer = CreateTween();
        exitTimer.TweenInterval(lastArrival + HoldDuration);
        exitTimer.TweenCallback(Callable.From(() => StartExit(letters, restPositions, restScales, tints)));
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
    /// spacing and a cycling palette colour. Missing glyphs are skipped
    /// (whitespace inserts a gap). Returns the letters in left-to-right order.
    /// </summary>
    private List<Control> BuildLetters()
    {
        var result = new List<Control>();
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
                        $"GoTextPopup: no glyph for '{ch}' at {string.Format(GlyphPathFormat, token)} — skipped"
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
            glyph.Position = new Vector2(cursor, -GlyphHeight / 2f);
            glyph.PivotOffset = glyph.Size / 2f;
            glyph.Rotation = (colorIndex % 2 == 0 ? -1f : 1f) * 0.06f;

            _banner.AddChild(glyph);
            result.Add(glyph);

            cursor += w + LetterGap;
            colorIndex++;
        }

        return result;
    }

    /// <summary>
    /// Letters separate and shoot up-and-outward in a fan centered on straight
    /// up, spread scaling with letter count. Each drags a self-texture
    /// afterimage trail and a few drifting star sparkles. Frees the popup once
    /// every letter has finished.
    /// </summary>
    private void StartExit(
        List<Control> letters,
        Vector2[] restPositions,
        Vector2[] restScales,
        Color[] tints
    )
    {
        int count = letters.Count;
        float totalSpreadDeg = Mathf.Min(MaxSpreadDegrees, SpreadDegreesPerLetter * (count - 1));

        float maxEnd = 0f;
        for (int i = 0; i < count; i++)
        {
            Control letter = letters[i];
            Vector2 rest = restPositions[i];
            Vector2 restScale = restScales[i];

            float tCenter = count > 1 ? (i / (float)(count - 1)) * 2f - 1f : 0f;
            float angleDeg = tCenter * (totalSpreadDeg / 2f);
            float angleRad = Mathf.DegToRad(angleDeg);
            Vector2 dir = new Vector2(Mathf.Sin(angleRad), -Mathf.Cos(angleRad));
            Vector2 target = rest + dir * ExitTravelDistance;

            var t = CreateTween();
            t.SetParallel(true);
            t.TweenProperty(letter, "position", target, ExitDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            t.TweenProperty(letter, "scale", restScale * ExitShrinkTo, ExitDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            t.TweenProperty(letter, "modulate:a", 0f, ExitDuration * 0.7f)
                .SetDelay(ExitDuration * 0.3f)
                .SetEase(Tween.EaseType.In);

            SpawnTrail(letter, rest, target, restScale * ExitShrinkTo, tints[i]);
            StartSparkleStream(letter, dir, tints[i]);

            maxEnd = Mathf.Max(maxEnd, ExitDuration);
        }

        var finish = CreateTween();
        finish.TweenInterval(maxEnd + TrailGhostFadeDuration + 0.05f);
        finish.TweenCallback(Callable.From(FinishAndFree));
    }

    /// <summary>
    /// Spawns <see cref="TrailGhostCount"/> fading, shrinking copies of the
    /// letter's own texture along its flight path, one every ExitDuration /
    /// TrailGhostCount seconds — the "comet tail" seen in the reference clip.
    /// </summary>
    private void SpawnTrail(Control letter, Vector2 from, Vector2 to, Vector2 endScale, Color tint)
    {
        if (letter is not TextureRect source || _banner == null)
            return;

        float step = ExitDuration / Mathf.Max(1, TrailGhostCount);
        for (int g = 0; g < TrailGhostCount; g++)
        {
            float delay = g * step;
            var t = CreateTween();
            t.TweenInterval(delay);
            t.TweenCallback(
                Callable.From(() =>
                {
                    if (!IsInstanceValid(source))
                        return;
                    SpawnGhost(source, tint);
                })
            );
        }
    }

    private void SpawnGhost(TextureRect source, Color tint)
    {
        var ghost = new TextureRect
        {
            Texture = source.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = tint,
            ZIndex = -1,
            Size = source.Size,
            Position = source.Position,
            PivotOffset = source.PivotOffset,
            Scale = source.Scale,
            Rotation = source.Rotation,
            Modulate = new Color(1, 1, 1, 0.5f),
        };
        _banner.AddChild(ghost);

        var t = CreateTween();
        t.SetParallel(true);
        t.TweenProperty(ghost, "modulate:a", 0f, TrailGhostFadeDuration);
        t.TweenProperty(ghost, "scale", ghost.Scale * 0.8f, TrailGhostFadeDuration);
        t.Chain().TweenCallback(Callable.From(ghost.QueueFree));
    }

    /// <summary>
    /// Keeps spawning little colored star sparkles from <paramref name="letter"/>'s
    /// LIVE position (it's still being tweened when this fires) every
    /// <see cref="SparkleSpawnInterval"/> seconds — a continuous stream shooting
    /// out of the text opposite its exit direction, rather than one upfront
    /// burst — for as long as the letter itself is flying. Stops (and frees the
    /// spawner) once the letter has finished its exit, i.e. once the text is gone.
    /// </summary>
    private void StartSparkleStream(Control letter, Vector2 dir, Color tint)
    {
        if (_banner == null)
            return;

        var timer = new Timer
        {
            WaitTime = Mathf.Max(0.01f, SparkleSpawnInterval),
            OneShot = false,
            Autostart = true,
        };
        _banner.AddChild(timer);
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(letter))
                return;
            SpawnOneSparkle(letter.Position + letter.Size * 0.5f, dir, tint);
        };

        var stop = CreateTween();
        stop.TweenInterval(ExitDuration);
        stop.TweenCallback(
            Callable.From(() =>
            {
                if (IsInstanceValid(timer))
                    timer.QueueFree();
            })
        );
    }

    /// <summary>
    /// One little colored "★" — plain, no outline, no glow/halo (a flare read
    /// as unwanted in review). Moves at constant speed opposite the letter's
    /// exit direction and fades out exactly as that movement ends, so it's
    /// always still moving when it disappears, never stopping to idle first.
    /// </summary>
    private void SpawnOneSparkle(Vector2 origin, Vector2 dir, Color tint)
    {
        if (_banner == null)
            return;

        Color sparkleColor = tint.Lerp(new Color(1f, 1f, 0.75f), 0.5f);
        float scale = _rng.RandfRange(SparkleScaleRange.X, SparkleScaleRange.Y);
        int fontSize = Mathf.RoundToInt(30f * scale);

        var wrapper = new Control { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4 };

        var body = new Label { Text = "★", MouseFilter = MouseFilterEnum.Ignore };
        body.AddThemeFontSizeOverride("font_size", fontSize);
        body.AddThemeColorOverride(
            "font_color",
            new Color(sparkleColor.R, sparkleColor.G, sparkleColor.B, 0.8f)
        );
        body.Position = new Vector2(-fontSize, -fontSize) * 0.5f;
        wrapper.AddChild(body);

        _banner.AddChild(wrapper);

        Vector2 jitter = new Vector2(_rng.RandfRange(-20f, 20f), _rng.RandfRange(-14f, 14f));
        Vector2 start = origin + jitter;
        wrapper.Position = start;
        wrapper.Modulate = new Color(1, 1, 1, 0);

        float dist = _rng.RandfRange(SparkleTravelDistance * 0.7f, SparkleTravelDistance * 1.3f);
        Vector2 target = start - dir * dist;
        float life = _rng.RandfRange(SparkleLifetimeRange.X, SparkleLifetimeRange.Y);

        var st = CreateTween();
        st.TweenProperty(wrapper, "modulate:a", 1f, 0.06f);
        st.Parallel()
            .TweenProperty(wrapper, "position", target, life)
            .SetTrans(Tween.TransitionType.Linear); // constant speed — never decelerates to a stop
        st.Parallel()
            .TweenProperty(wrapper, "modulate:a", 0f, life * 0.55f)
            .SetDelay(life * 0.45f); // fade finishes exactly as the move finishes — moving, not idle
        st.Chain().TweenCallback(Callable.From(wrapper.QueueFree));
    }

    private void FinishAndFree()
    {
        if (GetParent() is CanvasLayer layer)
            layer.QueueFree();
        else
            QueueFree();
    }
}
