using Godot;
using System.Collections.Generic;

/// <summary>
/// The console's big bubbly letters -- big_tx_*.bti, the 78 x 78 panes big_tx_1.blo / pause_1.blo lay out by hand for GO!, SHINE!,
/// TOO BAD!, READY and PAUSE -- built here from a string so a screen can say anything (DEVIATION: the game's words are fixed panes,
/// and its letters are white; the per-letter palette is the one Jaysonn's Shine Get banner uses). The disc has 17 letters
/// (a b d e g h i m n o p r s t u y and !); the pack in Font/sms_alphabet_textures adds c f j k l q v w x z, so the whole alphabet is in
/// assets/Sms/hud as big_tx_{letter}.png. Spacing follows the blo's hand layout: the game advances about 60 px per 78-px pane (TOO BAD!
/// 57 / 53 / 61 / 65 / 63, PAUSE 51 / 50 / 65 / 73) and about 28 more for a space, so a letter advances by the width of its opaque art
/// plus <c>gap</c> (-10 lands within a few px of the blo), a space by <c>space</c>. Each glyph keeps the full square pane (the art is
/// never stretched) and pivots on its centre, as the panes do (blo base position 4).
/// </summary>
public static class SmsBigText
{
    private static readonly Dictionary<string, (float x0, float w, float texW, float texH)> _trims = new();

    /// <summary>The texture for one character under <paramref name="format"/> ({0} = the token): lower-case + "ex" for the big_tx
    /// set, upper-case + "Excl" for a ShineLetter-style set; the first that exists wins. Null = no glyph (the character is skipped).</summary>
    public static Texture2D Glyph(char c, string format)
    {
        string lower, upper;
        if (char.IsLetterOrDigit(c)) { lower = char.ToLowerInvariant(c).ToString(); upper = char.ToUpperInvariant(c).ToString(); }
        else if (c == '!') { lower = "ex"; upper = "Excl"; }
        else if (c == '?') { lower = "qu"; upper = "Ques"; }
        else if (c == '.') { lower = "dot"; upper = "Dot"; }
        else if (c == ',') { lower = "comma"; upper = "Comma"; }
        else if (c == '\'') { lower = "apos"; upper = "Apos"; }
        else return null;
        foreach (string token in new[] { lower, upper })
        {
            string path = string.Format(format, token);
            if (ResourceLoader.Exists(path))
            {
                var tex = ResourceLoader.Load<Texture2D>(path);
                if (tex != null)
                    return tex;
            }
            if (FileAccess.FileExists(path) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                // not imported yet (a fresh file the editor has not scanned): the image itself
                Image img = Image.LoadFromFile(path);
                if (img != null && img.GetWidth() > 0)
                    return ImageTexture.CreateFromImage(img);
            }
        }
        return null;
    }

    /// <summary>The opaque columns of a glyph (alpha over half), as a fraction of the texture's width: x0 and width. An atlas region
    /// is taken as already trimmed; a texture whose image cannot be read counts as full-width.</summary>
    private static (float x0, float w) Trim(Texture2D tex)
    {
        string key = tex.ResourcePath != "" ? tex.ResourcePath : tex.GetInstanceId().ToString();
        if (_trims.TryGetValue(key, out var t))
            return (t.x0, t.w);
        float x0 = 0f, w = 1f;
        if (tex is not AtlasTexture)
        {
            Image img = tex.GetImage();
            if (img != null && img.GetWidth() > 0)
            {
                if (img.IsCompressed())
                    img.Decompress();
                int W = img.GetWidth(), H = img.GetHeight(), lo = W, hi = -1;
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                        if (img.GetPixel(x, y).A > 0.5f) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); break; }
                if (hi >= lo) { x0 = lo / (float)W; w = (hi - lo + 1) / (float)W; }
            }
        }
        _trims[key] = (x0, w, tex.GetWidth(), tex.GetHeight());
        return (x0, w);
    }

    /// <summary>
    /// Builds <paramref name="text"/> as TextureRect children of <paramref name="parent"/>, the word centred on <paramref name="center"/>
    /// (the parent's local frame): a glyph <paramref name="height"/> tall (its pane width from the texture's aspect), advancing by its
    /// opaque width plus <paramref name="gap"/>, a space by <paramref name="space"/>. <paramref name="wave"/> (optional) offsets letter
    /// k's centre vertically by wave[k % wave.Length], <paramref name="tiltDeg"/> turns every pane. Letters are tinted from
    /// <paramref name="palette"/> in turn (SelfModulate; Modulate is left for the caller's fades) when <paramref name="colored"/>.
    /// A word wider than <paramref name="maxWidth"/> (0 = no limit) is built smaller to fit it. Characters without a glyph are skipped
    /// with one warning. Returns the letters left to right.
    /// </summary>
    public static List<TextureRect> Build(Control parent, string text, string format, Vector2 center, float height, float gap, float space,
        Color[] palette, bool colored, float[] wave = null, float tiltDeg = 0f, float maxWidth = 0f)
    {
        var result = new List<TextureRect>();
        text ??= "";
        var glyphs = new List<(Texture2D tex, float paneW, float artX0, float artW, bool isSpace)>();
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) { glyphs.Add((null, space, 0f, space, true)); continue; }
            Texture2D tex = Glyph(c, format);
            if (tex == null)
            {
                GD.PushWarning($"[SMS console] no big letter for '{c}' ({string.Format(format, char.ToLowerInvariant(c))}) -- skipped");
                continue;
            }
            Vector2 ts = tex.GetSize();
            float paneW = ts.Y > 0f ? height * ts.X / ts.Y : height;
            (float x0, float w) = Trim(tex);
            glyphs.Add((tex, paneW, x0 * paneW, w * paneW, false));
        }
        float total = 0f;
        for (int i = 0; i < glyphs.Count; i++)
            total += glyphs[i].artW + (i < glyphs.Count - 1 && !glyphs[i].isSpace ? gap : 0f);
        if (maxWidth > 0f && total > maxWidth && total > 0f)
        {
            float fit = maxWidth / total; // too wide for the screen: the same word, smaller
            return Build(parent, text, format, center, height * fit, gap * fit, space * fit, palette, colored, wave, tiltDeg, 0f);
        }
        float cursor = center.X - total * 0.5f;
        int k = 0;
        foreach (var g in glyphs)
        {
            if (g.isSpace) { cursor += g.artW; continue; }
            var rect = new TextureRect
            {
                Texture = g.tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Name = $"big{k:00}",
            };
            rect.Size = new Vector2(g.paneW, height);
            float cy = center.Y + (wave != null && wave.Length > 0 ? wave[k % wave.Length] : 0f);
            rect.Position = new Vector2(cursor - g.artX0, cy - height * 0.5f);
            rect.PivotOffset = rect.Size * 0.5f;
            rect.Rotation = Mathf.DegToRad(tiltDeg);
            rect.SelfModulate = colored && palette != null && palette.Length > 0 ? palette[k % palette.Length] : Colors.White;
            parent.AddChild(rect);
            result.Add(rect);
            cursor += g.artW + gap;
            k++;
        }
        return result;
    }

    /// <summary>Jaysonn's Shine Get palette (Font/HudElements/ShineGetPopup.cs), cycled over the letters.</summary>
    public static Color[] DefaultPalette() => new[]
    {
        new Color(0.16f, 0.71f, 0.24f), // green
        new Color(0.96f, 0.36f, 0.62f), // pink
        new Color(1f, 0.83f, 0.16f),    // yellow
        new Color(0.18f, 0.76f, 0.72f), // teal
        new Color(0.98f, 0.52f, 0.13f), // orange
        new Color(0.96f, 0.28f, 0.4f),  // pink-red
    };
}
