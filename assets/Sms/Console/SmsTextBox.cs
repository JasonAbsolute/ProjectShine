using Godot;

/// <summary>A J2DTextBox as the game draws one (JSystem J2DTextBox::drawSelf -> J2DPrint::printReturn), with the system font the talk balloon
/// already carries (<see cref="SmsTalkFont"/>, pass 86): the string's lines laid in the box by the bindings -- VBIND 2 top / 1 bottom / 0 centre
/// sets the block (lines - 1) x lineSpace + fontH in the box's height (integer halves, as the game's); HBIND 2 left / 1 right / 0 centre sets
/// each line by its rounded width -- the first baseline fontH under the block's top, each next lineSpace lower; a glyph is its whole 20 x 20
/// cell drawn fontW x fontH (drawChar_scale), the top at the baseline - ascent, kerned by the font's WID1 entry from the second glyph on,
/// advanced by the entry's width x fontW / 20 + charSpace. The sheet's coverage is the alpha of the box's colour (setBlackWhite). The
/// blo's twin box -- the same text in black at alpha 125, three px right and down, drawn under -- is <see cref="Shadow"/>. Built for the
/// card-save windows (pass 124); any blo text box can use it.</summary>
[GlobalClass]
public partial class SmsTextBox : Control
{
    [Export] public SmsTalkFont Font;
    [Export(PropertyHint.MultilineText)] public string Text = "";
    /// <summary>mFontSizeX / mFontSizeY: the glyph cell's drawn size (save.blo: 20 x 24).</summary>
    [Export] public float FontW = 20f, FontH = 24f;
    [Export] public float LineSpace = 20f, CharSpace = 0f;
    /// <summary>J2DTextBoxHBinding: 0 centre, 1 right, 2 left. J2DTextBoxVBinding: 0 centre, 1 bottom, 2 top.</summary>
    [Export] public int HBind = 0, VBind = 0;
    /// <summary>The box's white (its text colour; the save windows' 'Saving file...' box is yellow ffff00).</summary>
    [Export] public Color TextColor = Colors.White;
    [Export] public bool Shadow = true;
    [Export] public Vector2 ShadowOffset = new Vector2(3f, 3f);
    [Export] public Color ShadowColor = new Color(0f, 0f, 0f, 125f / 255f);
    /// <summary>mColorAlpha: the pane's alpha over the colours (255 = as set).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Alpha = 1f;

    public void SetText(string s) { if (Text != s) { Text = s ?? ""; QueueRedraw(); } }

    /// <summary>J2DPrint::parse's width of one line (the glyph widths with the kerning, charSpace between).</summary>
    public float LineWidth(string s)
    {
        if (Font == null || string.IsNullOrEmpty(s)) return 0f;
        float sx = FontW / Font.Width, x = 0f, reach = 0f; bool first = true;
        foreach (char c in s)
        {
            Font.WidthEntry(c, out int kern, out int adv);
            x += (first ? adv + kern : adv) * sx;
            reach = Mathf.Max(reach, x);
            x += CharSpace;
            first = false;
        }
        return reach;
    }

    public override void _Draw()
    {
        if (Font?.Page == null || string.IsNullOrEmpty(Text) || Alpha <= 0f) return;
        if (Shadow) DrawText(ShadowOffset, new Color(ShadowColor, ShadowColor.A * Alpha));
        DrawText(Vector2.Zero, new Color(TextColor, TextColor.A * Alpha));
    }

    private void DrawText(Vector2 offset, Color col)
    {
        string[] lines = Text.Split('\n');
        float sx = FontW / Font.Width, sy = FontH / Font.CellHeight;
        int blockH = (int)((lines.Length - 1) * LineSpace + FontH + 0.5f);
        int y0 = VBind == 2 ? 0 : VBind == 1 ? (int)Size.Y - blockH : ((int)Size.Y - blockH) / 2;
        for (int i = 0; i < lines.Length; i++)
        {
            string s = lines[i];
            int w = (int)(LineWidth(s) + 0.5f);
            int x0 = HBind == 2 ? 0 : HBind == 1 ? (int)Size.X - w : ((int)Size.X - w) / 2;
            float baseline = y0 + FontH + i * LineSpace;
            float x = 0f; bool first = true;
            foreach (char c in s)
            {
                Font.WidthEntry(c, out int kern, out int adv);
                float drawX = first ? x : x - kern * sx;
                if (c != ' ' && Font.Has(c))
                    DrawTextureRectRegion(Font.Page, new Rect2(offset.X + x0 + drawX, offset.Y + baseline - Font.Ascent * sy, Font.CellWidth * sx, Font.CellHeight * sy), Font.Cell(c), col);
                x += (first ? adv + kern : adv) * sx + CharSpace;
                first = false;
            }
        }
    }
}
