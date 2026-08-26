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

    private TextureRect _nameArt;
    private TextureRect _headArt;
    private PaintReveal _paint;

    public CharacterEntry Entry { get; private set; }

    public override void _Ready()
    {
        _nameArt = GetNodeOrNull<TextureRect>(NameArtPath);
        _headArt = GetNodeOrNull<TextureRect>(HeadArtPath);
        _paint = GetNodeOrNull<PaintReveal>(PaintPath);
        Apply();
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
    }

    public void SetHighlighted(bool on)
    {
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
