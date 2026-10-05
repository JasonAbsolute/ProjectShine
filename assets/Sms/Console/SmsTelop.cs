using Godot;

/// <summary>
/// The console's telop -- the Dolpic News ticker (TGCConsole2, GCConsole2.cpp updateTelopState 1313, startAppearTelop 2396,
/// processDrawTelop 3323, the draw 4016): standard_1.blo's 'te_0' group with the 'te_w' window at (37, 400), 443 x 42 on the 640 x 448
/// frame (contents (8, 8) 427 x 26; telop_window_1..4.bti, black at alpha 136 with a soft shadow to the bottom right -- hud/telop_window.png)
/// and the 'tet2' text (standard_font.bfn 20 x 26, white) over its 'tet1' copy two pixels right and down (black). While Mario stands in
/// WAIT (mStatus == MARIO_STATUS_WAIT) with no camera demo and no timer out, the console counts frames; at the first wait (unk562: 5 s)
/// and then every wait (unk560: 60 s, the count restarts when a news item ends) it picks the next message of the stage's list
/// (checkChangeTelopArray: Delfino Plaza's per-episode arrays of infomess.bmg's D.E.B.S. ALERT lines; the index steps BEFORE the
/// pick, so a two-item list starts on its second), plays MSD_SE_SY_NEWS, and the group rises from 465 (off the bottom) to its home over
/// 80 frames (setPaneOffset). Then the text scrolls right to left at 0.7 frame pixels a frame (unk564), scissored to the window's
/// contents, until it has left the window; the group drops back over 80 frames. The console runs on the simulation list -- its frames
/// are the 120 Hz ticks (SmsHud.Fps). The scene is assets/Sms/Console/Telop.tscn, instanced in SmsHud.tscn (SmsHudLayer.Telop): this
/// Control is the window's group, the Frame a NinePatchRect, the Clip the contents (clip_contents), the Text a Label whose
/// LabelSettings (TelopText.tres) are the font, size, colour and shadow -- his to swap; the Sound an AudioStreamPlayer slot for the
/// news jingle. The rise and the drop follow SmsHud.AnimSpeed like every other pane; the waits and the scroll are the game's.
/// The game's scissor is 16/15 wider than the contents and 16 px higher (a J2DPrint origin detail not resolved); the port clips the
/// text to the contents band and centres it there. DEVIATION (Jaysonn, pass 43): the bar is centred on the canvas (Centered) -- the
/// game's x 37 is a 4:3 frame's margin, and left-anchored on the 16:9 canvas the bar parked at the left; off = the game's place.
/// </summary>
public partial class SmsTelop : Control
{
    [Export] public NinePatchRect Frame;
    [Export] public Control Clip;
    [Export] public Label Text;
    /// <summary>MSD_SE_SY_NEWS at each appearance; an empty stream is silent.</summary>
    [Export] public AudioStreamPlayer Sound;

    /// <summary>The stage's news lines (checkChangeTelopArray's array): the Level's News export, read by Mario at his setup. None = no
    /// telop in this stage, as every stage but Delfino Plaza has it. The 20 D.E.B.S. ALERT texts are in assets/Sms/Console/dolpic_news.txt.</summary>
    [Export] public string[] Messages = System.Array.Empty<string>();

    /// <summary>unk562: seconds of standing before the stage's FIRST news (unk56C is set until the first one has shown).</summary>
    [Export] public float FirstWaitSeconds = 5f;
    /// <summary>unk560: seconds of standing between news items (the count restarts when one ends).</summary>
    [Export] public float WaitSeconds = 60f;
    /// <summary>setPaneOffset / updatePaneOffset(80, ...): the rise from 465 (off the bottom) and the drop back, console frames.</summary>
    [Export] public float SlideFrames = 80f;
    /// <summary>unk564: the text's speed, frame pixels a console frame (0.7 at 120 Hz = 84 px/s). Jaysonn's knob too.</summary>
    [Export] public float ScrollPerFrame = 0.7f;
    /// <summary>The window's width in frame pixels (te_w: 443; the game leaves 53 px to the gauge). Widen it on a wide canvas.</summary>
    [Export] public float WidthFrame = 443f;
    /// <summary>The bar's size on top of the console's scale. Jaysonn's knob; the text scales with the bar.</summary>
    [Export] public float SizeScale = 1f;
    /// <summary>Moves the bar from its place, frame pixels. Jaysonn's knob.</summary>
    [Export] public Vector2 ShiftFrame = Vector2.Zero;
    /// <summary>DEVIATION (Jaysonn, pass 43): the bar centred on the canvas, the game's bottom margin kept. The game's te_w sits at x 37 of
    /// its 640-wide frame -- a 4:3 margin that parks the bar at the left of the 16:9 canvas (his HudRef.png). Off = the game's place.</summary>
    [Export] public bool Centered = true;

    // the game's layout, frame pixels
    private const float HomeX = 37f, HomeY = 400f, HeightFrame = 42f, Inset = 8f, ContentH = 26f;
    private const float OffBottomY = 465f;   // setPaneOffset(80, 0, 0, 0, 465 - initial y1): the group's y while hidden

    /// <summary>mStatus == MARIO_STATUS_WAIT: standing, no demo (unk50), no timer out (unk44C visible). Fed by the console each frame.</summary>
    public bool MarioWaiting;
    /// <summary>The pause menu / unk124: the count holds.</summary>
    public bool Paused;

    private enum Phase { Hidden, Rise, Scroll, Drop }
    private Phase _phase = Phase.Hidden;
    private float _count;        // unk55C: frames of standing since the last news
    private bool _first = true;  // unk56C
    private int _index;          // unk558
    private float _f;            // frames into the slide
    private float _x;            // unk568: the text's x in frame pixels, from the contents' right edge
    private float _textW;        // mTelopTextWidth in frame pixels
    private Vector2 _vp;
    private float _s = 1f;

    public bool IsShowing => _phase != Phase.Hidden;
    /// <summary>Hidden / Rise / Scroll / Drop (unk42 / unk44 / unk43), for logs.</summary>
    public string PhaseName => _phase.ToString();
    /// <summary>The text's x inside the contents while it scrolls, frame pixels (unk568).</summary>
    public float ScrollX => _x;
    /// <summary>The current message's width, frame pixels (mTelopTextWidth).</summary>
    public float TextWidth => _textW;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (Frame == null || Clip == null || Text == null)
            GD.PushWarning($"[SMS telop] {GetPath()}: the scene's Frame / Clip / Text exports are not all set (see assets/Sms/Console/Telop.tscn)");
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (Frame == null || Clip == null || Text == null)
            return;
        float frames = (float)delta * SmsHud.Fps;
        Vector2 vp = GetViewportRect().Size;
        if (vp != _vp || _s <= 0f)
        {
            _vp = vp;
            _s = SmsHud.Scale(vp) * SizeScale;
        }

        switch (_phase)
        {
            case Phase.Hidden:
                // updateTelopState: the count runs while not paused; the news comes when he stands and the wait is up
                if (!Paused)
                    _count += frames;
                float wait = (_first ? FirstWaitSeconds : WaitSeconds) * SmsHud.Fps;
                if (MarioWaiting && Messages != null && Messages.Length > 0 && _count >= wait)
                    Appear();
                break;
            case Phase.Rise:
                _f += frames * SmsHud.Anim;
                if (_f >= SlideFrames)
                {
                    _phase = Phase.Scroll;
                    _f = 0f;
                    _x = WidthFrame - Inset; // unk568 = unk544.x2: the contents' right edge
                }
                break;
            case Phase.Scroll:
                _x -= ScrollPerFrame * frames;        // processDrawTelop: unk568 -= unk564 every frame
                if (_x < -_textW)                     // past the window's left edge by its own width
                {
                    _count = 0f;                      // unk55C = 0: the next wait starts now
                    _phase = Phase.Drop;
                    _f = 0f;
                }
                break;
            case Phase.Drop:
                _f += frames * SmsHud.Anim;
                if (_f >= SlideFrames)
                {
                    _phase = Phase.Hidden;
                    Visible = false;
                }
                break;
        }
        Layout();
    }

    /// <summary>startDisappearTelop (GCConsole2.cpp 2434): a showing bar drops off the bottom now (the pause, a camera demo); its count
    /// restarts when the drop is done, as after a scroll.</summary>
    public void Disappear()
    {
        if (_phase == Phase.Hidden || _phase == Phase.Drop)
            return;
        _count = 0f;
        _phase = Phase.Drop;
        _f = 0f;
        Text.Visible = false;
    }

    /// <summary>startAppearTelop(true): the next message, the width, the jingle, the rise.</summary>
    private void Appear()
    {
        _first = false; // unk56C = 0
        _index = (_index + 1) % Messages.Length;            // ++unk558, wrapped at the array's end
        string msg = Messages[_index] ?? "";
        Text.Text = msg;
        _phase = Phase.Rise;
        _f = 0f;
        Visible = true;
        Text.Visible = false;                                // the text is drawn only while it scrolls (unk44 && !unk42 && !unk43)
        if (Sound != null && Sound.Stream != null)
            Sound.Play();
    }

    /// <summary>The group's place: the game's (37, 400) through the console's bottom-left anchoring -- or centred (Centered) -- the rise /
    /// drop offset, the text.
    /// Everything inside is laid out in frame pixels and the Control is scaled, as the balloon is, so his LabelSettings stay the source.</summary>
    private void Layout()
    {
        float s = _s;
        float slide = _phase == Phase.Rise ? 1f - Mathf.Clamp(_f / Mathf.Max(SlideFrames, 1f), 0f, 1f)
                    : _phase == Phase.Drop ? Mathf.Clamp(_f / Mathf.Max(SlideFrames, 1f), 0f, 1f)
                    : _phase == Phase.Hidden ? 1f : 0f;
        float dy = (OffBottomY - HomeY) * slide;             // TCoord2D: linear from +412 to 0 and back
        float x = Centered ? (_vp.X - WidthFrame * s) * 0.5f + ShiftFrame.X * s : SmsHud.LeftX(HomeX + ShiftFrame.X, s);
        Position = new Vector2(x, SmsHud.BottomY(HomeY + ShiftFrame.Y + dy, s, _vp));
        Scale = new Vector2(s, s);
        Size = new Vector2(WidthFrame, HeightFrame);
        Frame.Position = Vector2.Zero;
        Frame.Size = new Vector2(WidthFrame, HeightFrame);
        Clip.Position = new Vector2(Inset, Inset);
        Clip.Size = new Vector2(WidthFrame - 2f * Inset, ContentH);
        Clip.ClipContents = true;
        Text.Visible = _phase == Phase.Scroll;
        if (_phase == Phase.Scroll || _phase == Phase.Rise)
        {
            Vector2 ts = Text.GetMinimumSize();              // the font's own extent of the message, frame pixels
            _textW = ts.X;
            Text.Position = new Vector2(_x, (ContentH - ts.Y) * 0.5f);
            Text.Size = ts;
        }
    }
}
