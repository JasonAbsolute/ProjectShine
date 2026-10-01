using Godot;
using System;

/// <summary>
/// What the game's HUD console (TGCConsole2, GC2D/GCConsole2.cpp) builds its screens from -- shared by the water gauge
/// (SmsWaterHud) and the standard panes (SmsStandardHud):
///   the disc's textures (game_6/timg/*.bti decoded to assets/Sms/hud/*.png; the I4 ones white with the intensity as alpha, tinted per
///   pane the way J2D's black / white colours do it),
///   TBoundPane::setPanePosition (GC2D/BoundPane.cpp): a quadratic through three points over N frames, and the console's follow-up
///   (updateDownPaneState / updateUpPaneState / updateCoinPaneState: once the first quadratic ends off the home spot a second one of
///   30 frames brings the pane home) -- the "pop" of every icon, digit and back strip,
///   TExPane::setPaneOffset (GC2D/ExPane.cpp, a TCoord2D chase at constant speed): the linear slide of a whole group,
///   the console's three point triples: cDownTop (0, -100) / cDownMid (0, 30) for the counters dropping in from above, cUpTop (0, 50) /
///   cUpMid (0, -30) for the tank and the red coins rising from below, cCoinMid (0, 45) for the coin counter stepping down a row.
/// The console's frames are the game's SIMULATION TICKS: TGCConsole2 sits in "Group 2D" on the director's unk30 list, performed with
/// CUE_MOVE inside the 120 Hz simulation loop (MarDirectorInitECT.cpp 236, MarDirectorDirect.cpp 149) -- four frames per 30 fps
/// picture (pass 38; passes 19-37 took them as 30 fps frames, which is why his AnimSpeed 4 looked right). Everything here takes a
/// fractional frame count so the motion is evaluated at the display's own rate. The screen is drawn 1:1 into the game's 640 x 448 frame (J2DOrthoGraph over the render rect, 1 unit = 1 EFB
/// pixel; the .blo's 600 x 480 is nominal), of which a CRT hid the outer few per cent. On a wide window the left-hand groups keep
/// their margin to the left edge, the right-hand ones to the right, the lives (centred on the frame) to the centre, the bottom ones
/// to the bottom. The console is a scene (assets/Sms/Console/SmsHud.tscn, SmsHudLayer): every pane a TextureRect in its group, laid out
/// on the 640 x 448 frame as the .blo has it; the code here only moves them. Jaysonn's knobs live on that scene's root: Size scales
/// the whole console (1 = the frame's 448 lines on the window's height), the Shifts move the rows toward the window's edges,
/// AnimSpeed runs the motions (not the waits) faster than the game's pace, IdleHold keeps the star HUD and the lives while he idles.
/// </summary>
public static class SmsHud
{
    public const float ScreenW = 640f, ScreenH = 448f;   // the game's frame: SMSGetGameRenderWidth / Height, the J2D ortho over it
    public const float Fps = 120f;                        // console frames a second: the simulation tick (pass 38; was 30)
    public const int Settle = 30;                          // the follow-up quadratic's frames

    // ---- the scene root's knobs (SmsHudLayer.Apply copies its exports here; these are its defaults: Jaysonn's -- the game's are
    // 1 / 0 / 0 / 0 / 0 / 1 / false) ----
    public static float Size = 0.7f;
    public static float LeftShift = 0f, TopShift = -22f, RightShift = 30f, BottomShift = -10f;
    public static float AnimSpeed = 1f;                  // the scene root (SmsHudLayer) writes its export here; 1 = the game's pace
    public static bool IdleHold = true;
    /// <summary>The motions' pace over the game's.</summary>
    public static float Anim => AnimSpeed;

    /// <summary>Game pixels to canvas pixels: the frame's 448 lines on the viewport's height, times Size.</summary>
    public static float Scale(Vector2 viewport) => viewport.Y / ScreenH * Size;
    /// <summary>A left-anchored group's canvas x: its game x, shifted, scaled.</summary>
    public static float LeftX(float x, float s) => (x + LeftShift) * s;
    public static float TopY(float y, float s) => (y + TopShift) * s;
    /// <summary>A right-anchored group's canvas x: the game's margin to the frame's right edge, shifted, from the viewport's.</summary>
    public static float RightX(float x, float s, Vector2 viewport) => viewport.X - (ScreenW - x - RightShift) * s;
    public static float BottomY(float y, float s, Vector2 viewport) => viewport.Y - (ScreenH - y - BottomShift) * s;
    /// <summary>A group the game centres on its frame (the lives): centred on the viewport.</summary>
    public static float CenterX(float x, float s, Vector2 viewport) => viewport.X * 0.5f + (x - ScreenW * 0.5f) * s;

    /// <summary>A group's canvas position: its place on the game's frame through its anchor, at the console's scale.</summary>
    public static Vector2 Anchored(Vector2 home, SmsHudGroup.HudAnchor anchor, float s, Vector2 viewport)
    {
        bool right = anchor == SmsHudGroup.HudAnchor.TopRight || anchor == SmsHudGroup.HudAnchor.BottomRight;
        bool bottom = anchor == SmsHudGroup.HudAnchor.BottomLeft || anchor == SmsHudGroup.HudAnchor.BottomRight;
        float x = right ? RightX(home.X, s, viewport) : anchor == SmsHudGroup.HudAnchor.TopCenter ? CenterX(home.X, s, viewport) : LeftX(home.X, s);
        float y = bottom ? BottomY(home.Y, s, viewport) : TopY(home.Y, s);
        return new Vector2(x, y);
    }

    public static readonly Vector2 DownTop = new Vector2(0f, -100f), DownMid = new Vector2(0f, 30f);
    public static readonly Vector2 UpTop = new Vector2(0f, 50f), UpMid = new Vector2(0f, -30f);
    public static readonly Vector2 CoinMid = new Vector2(0f, 45f);

    public static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    /// <summary>setPanePosition(frames, top, mid, mid) and, once that is done, setPanePosition(30, mid, mid, end): the pop, f frames in
    /// (game frames; the motion runs Anim times the game's pace).</summary>
    public static Vector2 Pop(float f, Vector2 top, Vector2 mid, int frames, Vector2 end)
    {
        f *= Anim;
        if (f <= 0f)
            return top;
        if (f < frames)
            return Bezier(top, mid, mid, f / frames);
        f -= frames;
        if (f < Settle)
            return Bezier(mid, mid, end, f / Settle);
        return end;
    }

    /// <summary>The TCoord2D chase: constant speed from one offset to the other over the frames (at Anim times the pace).</summary>
    public static float Slide(float f, int frames, float from, float to)
        => frames <= 0 ? to : Mathf.Lerp(from, to, Mathf.Clamp(f * Anim / frames, 0f, 1f));

    /// <summary>How many of the console's frames a motion of `frames` game frames takes at the port's pace.</summary>
    public static float Paced(float frames) => frames / Anim;
}

/// <summary>A TExPane's offset interpolator: one linear slide, retargetable from wherever it is (updatePaneOffset).</summary>
public sealed class SmsHudSlide
{
    public float From, To, Start; public int Frames;
    public SmsHudSlide(float at) { From = To = at; Frames = 0; Start = 0f; }
    public void Set(float now, float from, float to, int frames) { Start = now; From = from; To = to; Frames = frames; }
    public void Retarget(float now, float to, int frames) { From = At(now); Start = now; To = to; Frames = frames; }
    public float At(float now) => SmsHud.Slide(now - Start, Frames, From, To);
    public bool Done(float now) => (now - Start) * SmsHud.Anim >= Frames;
    /// <summary>isInterpolatorAtZero: home, and got there.</summary>
    public bool AtZero(float now) => To == 0f && Done(now);
}

/// <summary>A picture pane of the screen with its TBoundPane: the scene's TextureRect (its home rect, texture, tint and rotation the
/// scene's), whether J2D shows it, and the pop it is riding (setPanePosition's three points and the console's 30-frame settle). Apply
/// puts the frame's result onto the node. A pane may instead carry a Control (the life meter's Sun, whose rays ride its pop).</summary>
public sealed class SmsHudPane
{
    public readonly TextureRect Node;
    public readonly Control Carrier;
    public Rect2 Home;                     // the scene's position and size inside the group
    public Texture2D Tex;                  // the picture of the moment (a digit changes; the rest keep the scene's)
    public Color Tint = Colors.White;      // the scene's modulate; the disc's changes with the air meter
    public bool Shown;                     // J2DPane::show / hide
    public bool Used = true;               // a digit pane the count has no use for (the hundreds below 100)
    public float Inset;                    // the life meter's heartbeat: pixels off every side
    private float _popStart = float.NegativeInfinity;
    private Vector2 _top, _mid, _end;
    private int _frames;

    public SmsHudPane(TextureRect node)
    {
        Node = node;
        Home = new Rect2(node.Position, node.Size);
        Tex = node.Texture;
        Tint = node.Modulate;
    }

    public SmsHudPane(Control carrier)
    {
        Carrier = carrier;
        Home = new Rect2(carrier.Position, Vector2.Zero);
    }

    /// <summary>setPanePosition(frames, top, mid, mid) at frame `at` (then the settle to `end`, (0, 0) unless told otherwise); shows the pane.</summary>
    public void Pop(float at, Vector2 top, Vector2 mid, int frames, Vector2? end = null)
    {
        _popStart = at; _top = top; _mid = mid; _frames = frames; _end = end ?? Vector2.Zero; Shown = true;
    }

    /// <summary>Home again: no motion (setPanePosition(1, 0, 0, 0)).</summary>
    public void Still() { _popStart = float.NegativeInfinity; }

    public Vector2 Offset(float now) => float.IsNegativeInfinity(_popStart) ? Vector2.Zero : SmsHud.Pop(now - _popStart, _top, _mid, _frames, _end);
    public bool PopDone(float now) => float.IsNegativeInfinity(_popStart) || (now - _popStart) * SmsHud.Anim >= _frames + SmsHud.Settle;

    /// <summary>The frame onto the node: shown or not, the picture, the colour, the pop's offset from home, the heartbeat's inset.</summary>
    public void Apply(float now, Vector2 groupOffset, float alpha = 1f)
    {
        if (Carrier != null)
        {
            Carrier.Visible = Shown;
            Carrier.Position = Home.Position + Offset(now) + groupOffset;
            return;
        }
        bool on = Shown && Used && Tex != null;
        Node.Visible = on;
        if (!on)
            return;
        if (Node.Texture != Tex)
            Node.Texture = Tex;
        Color col = Tint;
        col.A *= alpha;
        Node.Modulate = col;
        Node.Position = Home.Position + Offset(now) + groupOffset + new Vector2(Inset, Inset);
        Node.Size = Home.Size - new Vector2(2f * Inset, 2f * Inset);
    }
}
