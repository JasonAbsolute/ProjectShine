using Godot;

/// <summary>
/// One "WANTED" poster on the Player Select screen.
///
/// The layout lives in CharacterPoster.tscn so the frame, name and head can be
/// positioned by hand in the editor. This script only swaps in the textures for
/// whichever character it's given — it never moves anything, so your placement
/// is what ships.
/// </summary>
public partial class CharacterPoster : Button
{
    [Export] public NodePath FramePath = "Frame";
    [Export] public NodePath NameArtPath = "NameArt";
    [Export] public NodePath HeadArtPath = "HeadArt";
    [Export] public NodePath PaintPath = "Paint";

    /// <summary>Tint used while this poster isn't the highlighted one.</summary>
    [Export] public Color DimmedTint = new(0.62f, 0.60f, 0.58f, 1f);

    /// <summary>How much the highlighted poster grows. 1.0 disables the effect.</summary>
    [Export] public float SelectedScale = 1.06f;

    /// <summary>258: the colour strip (the colour's name over a row of swatch dots) for a character with alternate colours -- built in code under the
    /// poster when its entry has Colors, so the scene's layout stays yours. The strip's box (poster space) and type size.</summary>
    [Export] public Rect2 ColorStripRect = new(58f, 261f, 152f, 28f);   // the WANTED frame's photo panel runs x 58..210, y 70..289; the head art ends at y 260
    [Export] public int ColorNameSize = 12;
    [Export] public float ColorDot = 7f;
    [Export] public float ColorDotGap = 6f;
    /// <summary>The hint shown either side of the name while the poster is highlighted ("Y" and "X": the buttons that cycle).</summary>
    [Export] public bool ColorShowButtons = true;

    private TextureRect _nameArt;
    private TextureRect _headArt;
    private PaintReveal _paint;
    private Control _colorStrip;
    private Label _colorName;
    private HBoxContainer _colorDots;
    private bool _highlighted;

    public CharacterEntry Entry { get; private set; }

    /// <summary>258: the colour this poster shows (an index into Entry.Colors; 0 = the default look).</summary>
    public int ColorIndex { get; private set; }
    public int ColorCount => Entry?.Colors != null ? Entry.Colors.Count : 0;
    public CharacterColor CurrentColor => ColorCount > 0 && ColorIndex >= 0 && ColorIndex < ColorCount ? Entry.Colors[ColorIndex] : null;

    public override void _Ready()
    {
        _nameArt = GetNodeOrNull<TextureRect>(NameArtPath);
        _headArt = GetNodeOrNull<TextureRect>(HeadArtPath);
        _paint = GetNodeOrNull<PaintReveal>(PaintPath);
        Apply();
    }

    /// <summary>Show colour <paramref name="index"/> (wrapped into the entry's list). Safe before _Ready.</summary>
    public void SetColor(int index)
    {
        int n = ColorCount;
        ColorIndex = n > 0 ? ((index % n) + n) % n : 0;
        if (IsNodeReady())
            RefreshColorStrip();
    }

    /// <summary>The next (+1) or previous (-1) colour; false when the character has no colours to cycle.</summary>
    public bool CycleColor(int dir)
    {
        if (ColorCount < 2)
            return false;
        SetColor(ColorIndex + dir);
        return true;
    }

    private void RefreshColorStrip()
    {
        if (ColorCount == 0)
        {
            if (_colorStrip != null) _colorStrip.Visible = false;
            return;
        }
        if (_colorStrip == null)
        {
            _colorStrip = new Control { Name = "ColorStrip", MouseFilter = MouseFilterEnum.Ignore };
            _colorStrip.Position = ColorStripRect.Position;
            _colorStrip.Size = ColorStripRect.Size;
            AddChild(_colorStrip);
            _colorName = new Label { Name = "ColorName", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            _colorName.Position = new Vector2(0f, ColorStripRect.Size.Y * 0.42f);   // the dots above, the name below
            _colorName.Size = new Vector2(ColorStripRect.Size.X, ColorStripRect.Size.Y * 0.58f);
            _colorName.AddThemeFontSizeOverride("font_size", ColorNameSize);
            _colorName.AddThemeColorOverride("font_color", new Color(0.30f, 0.16f, 0.08f));
            _colorName.AddThemeColorOverride("font_outline_color", new Color(1f, 0.96f, 0.86f));
            _colorName.AddThemeConstantOverride("outline_size", 3);
            _colorStrip.AddChild(_colorName);
            _colorDots = new HBoxContainer { Name = "ColorDots", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            _colorDots.Position = Vector2.Zero;
            _colorDots.Size = new Vector2(ColorStripRect.Size.X, ColorStripRect.Size.Y * 0.42f);
            _colorDots.AddThemeConstantOverride("separation", (int)ColorDotGap);
            _colorStrip.AddChild(_colorDots);
        }
        _colorStrip.Visible = true;
        var cur = CurrentColor;
        string name = cur?.DisplayName ?? "";
        _colorName.Text = ColorShowButtons && _highlighted && ColorCount > 1 ? $"Y \u25C2  {name}  \u25B8 X" : name;
        // one dot a colour, the current one full size and rimmed, the rest small
        while (_colorDots.GetChildCount() < ColorCount)
            _colorDots.AddChild(new Panel { MouseFilter = MouseFilterEnum.Ignore });
        while (_colorDots.GetChildCount() > ColorCount)
        {
            var last = _colorDots.GetChild(_colorDots.GetChildCount() - 1);
            _colorDots.RemoveChild(last);
            last.QueueFree();
        }
        for (int i = 0; i < ColorCount; i++)
        {
            var dot = (Panel)_colorDots.GetChild(i);
            bool on = i == ColorIndex;
            float d = on ? ColorDot * 1.5f : ColorDot;
            dot.CustomMinimumSize = new Vector2(d, d);
            dot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            var sb = new StyleBoxFlat { BgColor = Entry.Colors[i]?.Swatch ?? Colors.White, CornerRadiusTopLeft = (int)d, CornerRadiusTopRight = (int)d, CornerRadiusBottomLeft = (int)d, CornerRadiusBottomRight = (int)d };
            if (on) { sb.BorderColor = new Color(0.30f, 0.16f, 0.08f); sb.SetBorderWidthAll(2); }
            dot.AddThemeStyleboxOverride("panel", sb);
        }
    }

    /// <summary>Point this poster at a character. Safe to call before _Ready.</summary>
    public void SetEntry(CharacterEntry entry)
    {
        Entry = entry;
        if (IsNodeReady())
            Apply();
    }

    private void Apply()
    {
        if (Entry == null)
            return;

        Disabled = !Entry.Unlocked;

        if (_nameArt != null)
        {
            _nameArt.Texture = Entry.NameArt;
            _nameArt.Visible = Entry.NameArt != null;
        }
        if (_headArt != null)
        {
            _headArt.Texture = Entry.HeadArt;
            _headArt.Visible = Entry.HeadArt != null;
        }

        if (_paint != null)
        {
            if (Entry.StrokeArt != null)
                _paint.Texture = Entry.StrokeArt;
            _paint.PaintColor = Entry.PaintColor;
            _paint.Reset();
        }

        // Only fall back to a bare name when there's no art at all, so a
        // half-finished character still reads on the screen.
        Text = (Entry.NameArt == null && Entry.HeadArt == null) ? Entry.DisplayName : "";

        RefreshColorStrip();   // 258
    }

    public void SetHighlighted(bool on)
    {
        _highlighted = on;
        if (_colorStrip != null) RefreshColorStrip();   // 258: the Y / X hint only on the highlighted poster
        Modulate = on ? Colors.White : DimmedTint;

        // Grow from the middle rather than the top-left. Safe to set the pivot
        // here because scale is 1 at this point, so moving it can't displace the
        // node — and the HBoxContainer lays out on minimum size, so scaling is
        // purely visual and won't shove the neighbouring posters around.
        PivotOffset = Size * 0.5f;
        Scale = Vector2.One * (on ? SelectedScale : 1f);
    }

    /// <summary>Paints this character's letter on. Returns how long it takes.</summary>
    public float PlayStamp() => _paint?.Play() ?? 0f;
}
