using Godot;

/// <summary>
/// A group of the console's screen (a counter's strip, icon and digits; the life meter; the timer) in SmsHud.tscn: a Control whose
/// place in the scene is its place on the game's 640 x 448 frame, with the edge of the window it keeps its distance from. The code
/// (SmsStandardHud) moves the group from there and never touches the panes' own layout.
/// </summary>
public partial class SmsHudGroup : Control
{
    public enum HudAnchor { TopLeft, TopCenter, TopRight, BottomLeft, BottomRight }

    /// <summary>The window edge (or the centre) the group keeps its frame distance from when the window is not 640 x 448.</summary>
    [Export] public HudAnchor Anchor = HudAnchor.TopLeft;

    /// <summary>Where the scene puts the group on the frame (read once, before anything moves it).</summary>
    public Vector2 Home;

    public override void _Ready() => Home = Position;

    /// <summary>The group onto the window: its frame place through its anchor, the console's scale, plus the frame-pixel offset of the
    /// moment (the slide of the whole group).</summary>
    public void Place(Vector2 offset, float s, Vector2 viewport)
    {
        Position = SmsHud.Anchored(Home, Anchor, s, viewport) + offset * s;
        Scale = new Vector2(s, s);
    }
}
