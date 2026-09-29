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
/// The console runs at the game's 30 frames a second; everything here takes a fractional frame count so the motion is evaluated at the
/// display's own rate. The screen is drawn 1:1 into the game's 640 x 448 frame (J2DOrthoGraph over the render rect, 1 unit = 1 EFB
/// pixel; the .blo's 600 x 480 is nominal), of which a CRT hid the outer few per cent. On a wide window the left-hand groups keep
/// their margin to the left edge, the right-hand ones to the right, the lives (centred on the frame) to the centre, the bottom ones
/// to the bottom. Jaysonn's SmsParams.Hud: Size scales the whole console (1 = the frame's 448 lines on the window's height), the
/// Shifts move the rows toward the window's edges, AnimSpeed runs the motions (not the waits) faster than the game's pace.
/// </summary>
public static class SmsHud
{
    public const string TexDir = "res://assets/Sms/hud/";
    public const float ScreenW = 640f, ScreenH = 448f;   // the game's frame: SMSGetGameRenderWidth / Height, the J2D ortho over it
    public const float Fps = 30f;
    public const int Settle = 30;                          // the follow-up quadratic's frames
    /// <summary>The motions' pace over the game's (SmsParams.Hud.AnimSpeed).</summary>
    public static float Anim => SmsParams.Hud.AnimSpeed;

    /// <summary>Game pixels to canvas pixels: the frame's 448 lines on the viewport's height, times SmsParams.Hud.Size.</summary>
    public static float Scale(Vector2 viewport) => viewport.Y / ScreenH * SmsParams.Hud.Size;
    /// <summary>A left-anchored group's canvas x: its game x, shifted, scaled.</summary>
    public static float LeftX(float x, float s) => (x + SmsParams.Hud.LeftShift) * s;
    public static float TopY(float y, float s) => (y + SmsParams.Hud.TopShift) * s;
    /// <summary>A right-anchored group's canvas x: the game's margin to the frame's right edge, shifted, from the viewport's.</summary>
    public static float RightX(float x, float s, Vector2 viewport) => viewport.X - (ScreenW - x - SmsParams.Hud.RightShift) * s;
    public static float BottomY(float y, float s, Vector2 viewport) => viewport.Y - (ScreenH - y - SmsParams.Hud.BottomShift) * s;
    /// <summary>A group the game centres on its frame (the lives): centred on the viewport.</summary>
    public static float CenterX(float x, float s, Vector2 viewport) => viewport.X * 0.5f + (x - ScreenW * 0.5f) * s;

    public static readonly Vector2 DownTop = new Vector2(0f, -100f), DownMid = new Vector2(0f, 30f);
    public static readonly Vector2 UpTop = new Vector2(0f, 50f), UpMid = new Vector2(0f, -30f);
    public static readonly Vector2 CoinMid = new Vector2(0f, 45f);

    /// <summary>The PNGs: through the importer when the editor has seen them, else straight off the disk (a headless run before it has).</summary>
    public static Texture2D Load(string name)
    {
        string path = TexDir + name + ".png";
        if (ResourceLoader.Exists(path))
            return ResourceLoader.Load<Texture2D>(path);
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        return img == null || img.IsEmpty() ? null : ImageTexture.CreateFromImage(img);
    }

    /// <summary>J2DPicture's black / white colours on a coloured texture: out = black + tex x (white - black), each channel (the life
    /// meter turned to air: black 003CFF00, white 00FFFFFF). Baked into a copy, the canvas has no such mapping.</summary>
    public static Texture2D Recolor(Texture2D src, Color black, Color white)
    {
        if (src == null)
            return null;
        Image img = src.GetImage();
        if (img == null)
            return src;
        img = (Image)img.Duplicate();
        if (img.IsCompressed())
            img.Decompress();
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = img.GetPixel(x, y);
                img.SetPixel(x, y, new Color(
                    black.R + c.R * (white.R - black.R), black.G + c.G * (white.G - black.G),
                    black.B + c.B * (white.B - black.B), black.A + c.A * (white.A - black.A)));
            }
        return ImageTexture.CreateFromImage(img);
    }

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

/// <summary>A picture pane of the screen with its TBoundPane: the home rect inside its group, the texture, the tint, its rotation,
/// whether it is shown, and the pop it is riding (setPanePosition's three points and the console's 30-frame settle).</summary>
public sealed class SmsHudPane
{
    public Rect2 Home;
    public Texture2D Tex;
    public Color Tint = Colors.White;
    /// <summary>The .blo's rotation in degrees about the pane's top-left corner (base position 0), counter-clockwise on the screen:
    /// J2DPane::makeMatrix rotates about z by -mRotation in a y-down space. The counters' strips, x's, digits and "MARIO" are 15,
    /// the Mario head 355 (5 the other way).</summary>
    public float Rot;
    public bool Shown;                     // J2DPane::show / hide
    public bool Used = true;               // a digit pane the count has no use for (the hundreds below 100)
    public float Inset;                    // the life meter's heartbeat: pixels off every side
    private float _popStart = float.NegativeInfinity;
    private Vector2 _top, _mid, _end;
    private int _frames;

    public SmsHudPane(float x, float y, float w, float h, Texture2D tex) { Home = new Rect2(x, y, w, h); Tex = tex; }

    /// <summary>setPanePosition(frames, top, mid, mid) at frame `at` (then the settle to `end`, (0, 0) unless told otherwise); shows the pane.</summary>
    public void Pop(float at, Vector2 top, Vector2 mid, int frames, Vector2? end = null)
    {
        _popStart = at; _top = top; _mid = mid; _frames = frames; _end = end ?? Vector2.Zero; Shown = true;
    }

    /// <summary>Home again: no motion (setPanePosition(1, 0, 0, 0)).</summary>
    public void Still() { _popStart = float.NegativeInfinity; }

    public Vector2 Offset(float now) => float.IsNegativeInfinity(_popStart) ? Vector2.Zero : SmsHud.Pop(now - _popStart, _top, _mid, _frames, _end);
    public bool PopDone(float now) => float.IsNegativeInfinity(_popStart) || (now - _popStart) * SmsHud.Anim >= _frames + SmsHud.Settle;

    public Rect2 Rect(float now, Vector2 groupOffset)
    {
        Vector2 o = Offset(now) + groupOffset;
        return new Rect2(Home.Position + o + new Vector2(Inset, Inset), Home.Size - new Vector2(2f * Inset, 2f * Inset));
    }

    /// <summary>Draws the pane inside its group's transform (the group's canvas position and the console's scale), turned by its
    /// own rotation about its top-left corner as J2D does.</summary>
    public void Draw(CanvasItem c, float now, Vector2 groupOffset, Transform2D group, float alpha = 1f)
    {
        if (!Shown || !Used || Tex == null)
            return;
        Color col = Tint;
        col.A *= alpha;
        Vector2 local = Home.Position + Offset(now) + groupOffset;
        c.DrawSetTransformMatrix(group * new Transform2D(Mathf.DegToRad(-Rot), local));
        c.DrawTextureRect(Tex, new Rect2(new Vector2(Inset, Inset), Home.Size - new Vector2(2f * Inset, 2f * Inset)), false, col);
    }
}
