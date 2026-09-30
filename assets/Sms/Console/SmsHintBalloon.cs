using Godot;

/// <summary>
/// The console's help balloon: TGCConsole2's 'he_w' window with the 'he_1' / 'he_2' text boxes (standard_1.blo: the window at (397, 128),
/// 194 x 216 on the 640 x 448 frame, its contents 12 px in from the left and top, 68 from the right, 40 from the bottom -- the
/// help_window_1..4 corner textures with the tail at the bottom right; the text 20 x 26 in standard_font.bfn, white over a black copy
/// two pixels right and three down; the texts in /common/2d/balloon.bmg). startAppearBalloon (GCConsole2.cpp 2630): the window shows
/// at alpha 0 and fades in 0x30 a frame, then grows 30 px a frame from its BOTTOM edge up to its full height; the text is then typed
/// two characters a frame (processBalloonTextStep); it holds strlen x 0.7 + 100 frames; startDisappearBalloon shrinks it 30 px a
/// frame and fades it out 0x30 a frame. A new message only replaces one of lower or equal priority (entry->unk4), and while one is
/// up the next is queued for after the close. Each message carries a FLUDD voice cue (scNozzleSoundList) -- the Voice player's
/// stream is his to fill.
/// The scene is assets/Sms/Console/HintBalloon.tscn, instanced in SmsHud.tscn (SmsHudLayer.Balloon): this Control is the window,
/// the Frame a NinePatchRect of hud/help_window.png (the four disc pieces composed as J2DWindow draws them, the contents blue
/// 0000FF at alpha 146 baked in), the Text a Label whose LabelSettings are the font, size, colour and shadow -- his to swap (the
/// game's bitmap font is not decoded; Jaysonn's Delfino.ttf, the fan cut of it, stands in at 20 px on a 28 px line -- HintBalloonText.tres). The appear / disappear motions follow SmsHud.AnimSpeed like every
/// other pane; the typing rate and the hold are the game's. Nothing here is drawn or laid out by code beyond moving what the scene
/// provides.
/// </summary>
public partial class SmsHintBalloon : Control
{
    /// <summary>The window edge this balloon keeps its frame distance from (the game's console: the right).</summary>
    [Export] public SmsHudGroup.HudAnchor Anchor = SmsHudGroup.HudAnchor.TopRight;
    [Export] public NinePatchRect Frame;
    [Export] public Label Text;
    /// <summary>The message's FLUDD voice (scNozzleSoundList by the message's sound index); an empty stream is silent.</summary>
    [Export] public AudioStreamPlayer Voice;
    /// <summary>unk3CC: the window's growth and shrink, frame pixels a 30 fps frame.</summary>
    [Export] public float GrowPerFrame = 30f;
    /// <summary>The fade in and out, alpha (0..255) a frame: 0x30.</summary>
    [Export] public float FadePerFrame = 48f;
    /// <summary>processBalloonTextStep: characters typed a frame.</summary>
    [Export] public float CharsPerFrame = 2f;
    /// <summary>The hold after the text is out: HoldBase + HoldPerChar x the text's length, frames (unk3E8 100, unk3EC 0.7).</summary>
    [Export] public float HoldBase = 100f;
    [Export] public float HoldPerChar = 0.7f;
    /// <summary>The text box inside the window: left, top, right, bottom in frame pixels -- he_2 sits at (415, 140) 164 x 166 in the
    /// 194 x 216 window at (397, 128): 18 in from the left, 12 from the top, 12 from the right, 38 from the bottom. The tail corner
    /// piece is 68 x 40, so the text runs over the frame's right column, as the game's does.</summary>
    [Export] public Vector4 ContentInset = new Vector4(18f, 12f, 12f, 38f);
    /// <summary>The balloon's own scale: 1 = the game's frame (448 lines on the window's height), as the game draws it; the console's
    /// Size does not shrink it -- its text has to stay readable. Jaysonn's knob.</summary>
    [Export] public float SizeScale = 1f;
    /// <summary>Moves the window on the game's frame, frame pixels (the game: (397, 128), its right edge 49 in from the frame's).
    /// Jaysonn's knob; the console's shifts do not apply here.</summary>
    [Export] public Vector2 ShiftFrame = Vector2.Zero;

    private enum Phase { Hidden, Appear, Typing, Hold, Disappear }
    private Phase _phase = Phase.Hidden;
    private Vector2 _home, _full;
    private float _height, _alpha, _typed, _hold;
    private string _text = "";
    private int _priority = -1;
    private bool _autoClose = true;
    private string _queuedText;
    private int _queuedPriority;
    private bool _queuedAutoClose;
    private Vector2 _vp;
    private float _s = 1f;

    public bool IsShowing => _phase != Phase.Hidden;
    public string CurrentText => _phase == Phase.Hidden ? "" : _text;
    public string DebugPhase => $"{_phase} h {_height:F0}/{_full.Y:F0} a {_alpha:F0} typed {_typed:F0}/{_text.Length} hold {_hold:F0}";

    public override void _Ready()
    {
        _home = Position;
        _full = Size;
        _height = 0f;
        _alpha = 0f;
        Visible = false;
        if (Frame == null || Text == null)
            GD.PushWarning($"[SMS balloon] {GetPath()}: the scene's Frame / Text exports are not both set (see assets/Sms/Console/HintBalloon.tscn)");
        GetViewport().SizeChanged += Layout;
        Layout();
    }

    /// <summary>startAppearBalloon: false when a message of higher priority is up or queued. With one up, the new one closes it and
    /// follows (the game's unk3F4 queue).</summary>
    public bool Show(string text, int priority = 1, bool autoClose = true)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        if (_phase != Phase.Hidden)
        {
            if (priority < _priority || _queuedText != null)
                return false;
            _queuedText = text;
            _queuedPriority = priority;
            _queuedAutoClose = autoClose;
            Hide();
            return true;
        }
        _text = text;
        _priority = priority;
        _autoClose = autoClose;
        _typed = 0f;
        _height = 0f;
        _alpha = 0f;
        _hold = HoldBase + HoldPerChar * text.Length;
        if (Text != null)
            Text.Text = "";
        _phase = Phase.Appear;
        Visible = true;
        if (Voice != null && Voice.Stream != null)
            Voice.Play();
        Layout();
        return true;
    }

    /// <summary>startDisappearBalloon: the close, from any phase.</summary>
    public void Hide()
    {
        if (_phase == Phase.Hidden)
            return;
        _phase = Phase.Disappear;
    }

    public override void _Process(double delta)
    {
        if (_phase == Phase.Hidden)
            return;
        float motion = (float)delta * 30f * SmsHud.Anim; // the pops' pace, as every other pane
        float game = (float)delta * 30f;                   // the game's own frames: the typing and the hold
        switch (_phase)
        {
            case Phase.Appear:
                if (_alpha < 255f)
                    _alpha = Mathf.Min(255f, _alpha + FadePerFrame * motion);
                else
                {
                    _height = Mathf.Min(_full.Y, _height + GrowPerFrame * motion);
                    if (_height >= _full.Y)
                        _phase = Phase.Typing;
                }
                break;
            case Phase.Typing:
                _typed = Mathf.Min(_text.Length, _typed + CharsPerFrame * game);
                if (Text != null)
                    Text.Text = _text.Substring(0, (int)_typed);
                if (_typed >= _text.Length)
                    _phase = Phase.Hold;
                break;
            case Phase.Hold:
                if (Text != null && Text.Text != _text)
                    Text.Text = _text;
                _hold -= game;
                if (_autoClose && _hold <= 0f)
                    _phase = Phase.Disappear;
                break;
            case Phase.Disappear:
                if (_height > 0f)
                {
                    _height = Mathf.Max(0f, _height - GrowPerFrame * motion);
                    if (Text != null)
                        Text.Text = "";
                }
                else
                {
                    _alpha = Mathf.Max(0f, _alpha - FadePerFrame * motion);
                    if (_alpha <= 0f)
                    {
                        _phase = Phase.Hidden;
                        Visible = false;
                        _priority = -1;
                        if (_queuedText != null)
                        {
                            string t = _queuedText;
                            _queuedText = null;
                            Show(t, _queuedPriority, _queuedAutoClose);
                        }
                        return;
                    }
                }
                break;
        }
        Layout();
    }

    /// <summary>The window onto the screen: the game's 640 x 448 frame fitted to the window's height (times SizeScale), the balloon's frame
    /// place kept from the anchored edges (the right and the top: 49 px in, 128 px down on the frame) plus ShiftFrame -- the
    /// console's Size and shifts are for the counters, not for a text window; its bottom edge fixed while it grows and shrinks; the
    /// frame and the text box follow.</summary>
    private void Layout()
    {
        _vp = GetViewportRect().Size;
        _s = _vp.Y / SmsHud.ScreenH * SizeScale;
        Vector2 top = new Vector2(_home.X + ShiftFrame.X, _home.Y + ShiftFrame.Y + (_full.Y - _height));
        bool right = Anchor == SmsHudGroup.HudAnchor.TopRight || Anchor == SmsHudGroup.HudAnchor.BottomRight;
        bool bottom = Anchor == SmsHudGroup.HudAnchor.BottomLeft || Anchor == SmsHudGroup.HudAnchor.BottomRight;
        float x = right ? _vp.X - (SmsHud.ScreenW - top.X) * _s : top.X * _s;
        float y = bottom ? _vp.Y - (SmsHud.ScreenH - top.Y) * _s : top.Y * _s;
        Position = new Vector2(x, y);
        Scale = new Vector2(_s, _s);
        Size = new Vector2(_full.X, _height);
        Modulate = new Color(1f, 1f, 1f, _alpha / 255f);
        if (Frame != null)
        {
            Frame.Position = Vector2.Zero;
            Frame.Size = new Vector2(_full.X, Mathf.Max(_height, ContentInset.Y + ContentInset.W));
            Frame.Visible = _height > 0f;
        }
        if (Text != null)
        {
            Text.Position = new Vector2(ContentInset.X, ContentInset.Y);
            Text.Size = new Vector2(_full.X - ContentInset.X - ContentInset.Z, Mathf.Max(0f, _height - ContentInset.Y - ContentInset.W));
            Text.Visible = _height >= _full.Y - 0.5f || _phase == Phase.Hold;
        }
    }
}
