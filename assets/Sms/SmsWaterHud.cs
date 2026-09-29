using Godot;
using System;

/// <summary>
/// The FLUDD water gauge of the game's HUD: TGCConsole2 (GC2D/GCConsole2.cpp) drawing the 'w_0' panes of game_6/scrn/standard_1.blo,
/// read straight from the disc's screen file (tools/projectshine/blo_dump.py) -- a 600 x 480 screen, the group at (493, 355):
///   w_ba  water_back.bti    88 x 86 at (0, 0), alpha 120, black 0000FF00 / white 0000FFFF (an I4 disc: intensity is the alpha)
///   w_t0  water_icon_0.bti  44 x 70 at (22, 9)                                          the tank (IA4, translucent)
///   w_t2  water_icon_2.bti  18 x 56 at (23, 16), colour 64DCFF                          the water's left face (drawWater layer 1)
///   w_t3  water_icon_3.bti  24 x 54 at (39, 19), colour 00B4F0                          the right face (layer 2)
///   w_t1  water_icon_1.bti  40 x 18 at (23, 10), colour BEF0FF                          the surface
///   nz01..04 water_nozzle_01..04.bti at (6, -22) 56x44 / (0, -11) 84x41 / (22, -35) 42x56 / (14, -28) 54x54, each with the X
///         button water_button_x.bti 34 x 44 at (47, -19) / (57, -35) / (32, -26) / (38, -32) inside it   the current nozzle's icon
///   w_tx  water_text_0.bti  80 x 32 at (4, 66)                                          "WATER"
/// updateWaterGaugeFill (123): fill = mCurrentWater / AmountMax; the surface's alpha 255 full, 0 empty, 0x50 between; the icon is
/// mCurrentNozzle's, swapped only once the group has settled. drawWater (3736): the faces are coloured quads whose water rises with
/// the fill; the surface picture sits at y = tank.y + 47 x (1 - fill) - 0.5 + 1. drawWaterBack (2752): the disc is blue at alpha
/// 0x78; while a trigger nozzle holds pressure (isPressureOn: the rocket / turbo charge) the disc fills from the bottom with FF3F3F at
/// alpha 0xC8 by pressure / InsidePressureMax, and once full pulses red -> yellow -> red over 25 frames (getPressureFlashColor).
/// endCameraDemo (2125) / startAppearTank (2180) / processAppearTank (3194): the group pane (the tank rides it; the text, the icon and
/// the disc were detached from it in loadAfter and pop on their own) rises from 110 px below (465 - 355) over 20 frames; the disc pops
/// from 100 below through -30 over 50 frames, then at frame 6 the text from 50 below over 40, at 14 the tank (inside the rising group)
/// from 50, at 26 the icon from 100 -- each a setPanePosition quadratic (cUpTop / cUpMid, SmsHud.Pop) that the console follows with a
/// 30-frame settle from -30 to home (updateUpPaneState); the X button shows once the icon is home, and the tank counts as up.
/// startDisappearTank (2201): the group slides 170 px down over 40 frames, the pieces likewise (a quadratic whose middle is the
/// half-way point: straight). updateWaterTankState (1371): once up, the X button's alpha is 0 for 70 frames and 255 until 270, again
/// and again. The console's frames are the game's 30 a second; they are counted fractionally here so the motion is as smooth as the
/// display, the motions at SmsParams.Hud.AnimSpeed times the pace. The screen is the game's 640 x 448 frame (SmsHud): the group keeps
/// its margins to the bottom-right corner (the disc 59 px from the right edge, 7 px from the bottom; "WATER" ran 3 px past it on the
/// disc, into the CRT's overscan), moved by SmsParams.Hud's right / bottom shifts, all scaled by SmsHud.Scale.
/// </summary>
public partial class SmsWaterHud : Control
{
    private const float GroupX = 493f, GroupY = 355f;

    // the panes: (x, y, w, h) in the group's pixels
    private static readonly Rect2 BackRect = new Rect2(0, 0, 88, 86);
    private static readonly Rect2 TankRect = new Rect2(22, 9, 44, 70);
    private static readonly Rect2 FaceLRect = new Rect2(23, 16, 18, 56);
    private static readonly Rect2 FaceRRect = new Rect2(39, 19, 24, 54);
    private static readonly Rect2 SurfaceRect = new Rect2(23, 10, 40, 18);
    private static readonly Rect2 TextRect = new Rect2(4, 66, 80, 32);
    private static readonly Rect2[] IconRect = { new Rect2(6, -22, 56, 44), new Rect2(0, -11, 84, 41), new Rect2(22, -35, 42, 56), new Rect2(14, -28, 54, 54) };
    private static readonly Vector2[] XButtonOffset = { new Vector2(47, -19), new Vector2(57, -35), new Vector2(32, -26), new Vector2(38, -32) };
    private static readonly Vector2 XButtonSize = new Vector2(34, 44);
    private static readonly Color FaceLColor = new Color(0x64 / 255f, 0xDC / 255f, 1f);
    private static readonly Color FaceRColor = new Color(0f, 0xB4 / 255f, 0xF0 / 255f);
    private static readonly Color SurfaceColor = new Color(0xBE / 255f, 0xF0 / 255f, 1f);
    private static readonly Color BackColor = new Color(0f, 0f, 1f, 0x78 / 255f);
    private static readonly Color PressureColor = new Color(1f, 0x3F / 255f, 0x3F / 255f, 0xC8 / 255f);
    private const float SurfaceTravel = 47f;

    private Texture2D _back, _tank, _surface, _faceL, _faceR, _text, _xButton;
    private readonly Texture2D[] _icons = new Texture2D[4];
    public bool Ready { get; private set; }

    // what Mario tells it every physics frame
    public float Fill = 1f;          // mCurrentWater / AmountMax
    public int Nozzle = 0;           // 0 spray, 1 hover, 2 rocket, 3 turbo
    public float Pressure = -1f;     // the trigger nozzle's charge / InsidePressureMax; below 0: no pressure gauge
    public bool Wanted = false;      // MARIO_FLAG_HAS_FLUDD and no camera demo: the gauge belongs on screen

    private enum Phase { Hidden, Appearing, Shown, Disappearing }
    private Phase _phase = Phase.Hidden;
    private float _now;              // the console's frames (30 a second), fractional
    private float _f0;               // when the phase began
    private int _shownIcon;          // unk310: the icon on screen (swapped only when settled)
    private float _xBlink;           // unk7C while shown
    private float _flash;            // unk30C
    private float _groupOffset;      // the group's y offset (px), the TExPane interpolator
    private readonly SmsHudSlide _group = new SmsHudSlide(0f);
    private readonly float[] _pieceOffset = new float[4]; // disc, text, tank, icon
    private readonly bool[] _pieceShown = new bool[4];
    private float _disappearFrom;    // the pieces' offset when the disappear began
    private const int AppearFrames = 20, DisappearFrames = 40;
    private const float RiseFrom = 465f - GroupY;            // setPaneOffset from 465 - y1: the group starts this far below
    private bool _xShown;

    public override void _Ready()
    {
        _back = Load("water_back"); _tank = Load("water_icon_0"); _surface = Load("water_icon_1");
        _faceL = Load("water_icon_2"); _faceR = Load("water_icon_3"); _text = Load("water_text_0"); _xButton = Load("water_button_x");
        for (int i = 0; i < 4; i++)
            _icons[i] = Load($"water_nozzle_0{i + 1}");
        Ready = _back != null && _tank != null && _surface != null && _faceL != null && _faceR != null && _text != null && _xButton != null
            && _icons[0] != null && _icons[1] != null && _icons[2] != null && _icons[3] != null;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 10;
        Visible = false;
        Layout();
        GetViewport().SizeChanged += Layout;
    }

    private static Texture2D Load(string name) => SmsHud.Load(name);

    private void Layout()
    {
        Vector2 vp = GetViewportRect().Size;
        float s = SmsHud.Scale(vp);
        Scale = new Vector2(s, s);
        Position = new Vector2(SmsHud.RightX(GroupX, s, vp), SmsHud.BottomY(GroupY, s, vp));
    }

    public override void _Process(double delta)
    {
        if (!Ready)
            return;
        float df = (float)delta * SmsHud.Fps;
        _now += df;
        Tick(df);
        QueueRedraw();
    }

    /// <summary>The console this frame: the tank's appear / disappear and the X button's blink, at fractional frames.</summary>
    private void Tick(float df)
    {
        float f = _now - _f0;
        switch (_phase)
        {
            case Phase.Hidden:
                if (Wanted)
                    StartAppear();
                break;
            case Phase.Appearing:
            {
                // endCameraDemo / startAppearTank: the group rises 110 px over 20 frames with the tank inside it (hidden until its pop);
                // the disc pops from 100 below over 50 frames; processAppearTank pops the text at 6 (from 50), the tank at 14 (from 50,
                // inside the group), the icon at 26 (from 100), each over 40 frames and then the 30-frame settle from -30 to home
                _groupOffset = _group.At(_now);
                _pieceOffset[0] = SmsHud.Pop(f, new Vector2(0f, 100f), SmsHud.UpMid, 50, Vector2.Zero).Y;
                _pieceShown[0] = true;
                float t6 = SmsHud.Paced(6f), t14 = SmsHud.Paced(14f), t26 = SmsHud.Paced(26f);
                _pieceShown[1] = f >= t6; _pieceOffset[1] = SmsHud.Pop(f - t6, SmsHud.UpTop, SmsHud.UpMid, 40, Vector2.Zero).Y;
                _pieceShown[2] = f >= t14; _pieceOffset[2] = SmsHud.Pop(f - t14, SmsHud.UpTop, SmsHud.UpMid, 40, Vector2.Zero).Y;
                _pieceShown[3] = f >= t26; _pieceOffset[3] = SmsHud.Pop(f - t26, new Vector2(0f, 100f), SmsHud.UpMid, 40, Vector2.Zero).Y;
                if (f >= SmsHud.Paced(26f + 40f + SmsHud.Settle)) // the icon home: unk288->show(), the appear done (unk45 -> unk46), the blink from 0
                {
                    _phase = Phase.Shown; _f0 = _now; _xShown = false; _xBlink = 0f;
                    _groupOffset = 0f;
                    for (int i = 0; i < 4; i++) { _pieceOffset[i] = 0f; _pieceShown[i] = true; }
                }
                if (!Wanted)
                    StartDisappear();
                break;
            }
            case Phase.Shown:
                // updateWaterTankState: the X button's alpha is 0 below 70 frames, 255 below 270, then the count restarts
                _xBlink += df;
                if (_xBlink >= 270f) _xBlink -= 270f;
                _xShown = _xBlink >= 70f;
                if (_shownIcon != Nozzle)
                    _shownIcon = Nozzle; // updateWaterGaugeFill swaps the icon once the interpolator is at zero: settled, it is
                if (!Wanted)
                    StartDisappear();
                break;
            case Phase.Disappearing:
                // startDisappearTank: the group's offset chases 170 more over 40 frames; the pieces ride a straight quadratic to +170
                _groupOffset = _group.At(_now);
                for (int i = 0; i < 4; i++)
                    _pieceOffset[i] = SmsHud.Bezier(new Vector2(0f, _disappearFrom), new Vector2(0f, (_disappearFrom + 170f) * 0.5f),
                        new Vector2(0f, 170f), f * SmsHud.Anim / DisappearFrames).Y;
                if (f >= SmsHud.Paced(DisappearFrames))
                {
                    _phase = Phase.Hidden; Visible = false;
                }
                break;
        }
        // the pressure flash counter runs while the charge is full (drawWaterBack)
        if (Pressure >= 1f) { _flash += df; if (_flash >= 25f) _flash -= 25f; }
        else _flash = 0f;
    }

    private void StartAppear()
    {
        _phase = Phase.Appearing; _f0 = _now; _xShown = false; _xBlink = 0f;
        _group.Set(_now, RiseFrom, 0f, AppearFrames);
        _groupOffset = RiseFrom;
        _shownIcon = Nozzle;
        for (int i = 0; i < 4; i++) { _pieceOffset[i] = 100f; _pieceShown[i] = i == 0; }
        Visible = true;
    }

    private void StartDisappear()
    {
        _phase = Phase.Disappearing; _f0 = _now; _xShown = false;
        _group.Retarget(_now, 170f + RiseFrom, DisappearFrames); // 465 - y1 + 60, from wherever the rise had got to
        _disappearFrom = _pieceOffset[0];
        for (int i = 0; i < 4; i++) _pieceShown[i] = true;
    }

    /// <summary>getPressureFlashColor (97): FF3F3F -> FFFF00 over 10 frames, held 5, back over 10.</summary>
    private static Color FlashColor(float frame)
    {
        float g = 0x3F, b = 0x3F;
        if (frame < 10f) { g += frame * 19.2f; b -= frame * 6.3f; }
        else if (frame < 15f) { g = 255f; b = 0f; }
        else { float fade = 25f - frame; g += fade * 19.2f; b -= fade * 6.3f; }
        return new Color(1f, Mathf.Clamp(g, 0f, 255f) / 255f, Mathf.Clamp(b, 0f, 255f) / 255f, 0xC8 / 255f);
    }

    public override void _Draw()
    {
        if (!Ready || _phase == Phase.Hidden)
            return;
        Vector2 g = new Vector2(0f, _groupOffset);
        // the disc, blue (detached from the group: its own pop only); the pressure gauge fills it from the bottom (drawWaterBack)
        Rect2 back = new Rect2(BackRect.Position + new Vector2(0f, _pieceOffset[0]), BackRect.Size);
        if (Pressure >= 0f)
        {
            float ratio = Mathf.Clamp(Pressure, 0f, 1f);
            float fillTop = MathF.Floor((1f - ratio) * BackRect.Size.Y);
            if (fillTop > 0f)
                DrawTextureRectRegion(_back, new Rect2(back.Position, new Vector2(back.Size.X, fillTop)), new Rect2(0, 0, back.Size.X, fillTop), BackColor);
            if (fillTop < back.Size.Y)
            {
                Color c = ratio >= 1f ? FlashColor(_flash) : PressureColor;
                DrawTextureRectRegion(_back, new Rect2(back.Position + new Vector2(0f, fillTop), new Vector2(back.Size.X, back.Size.Y - fillTop)),
                    new Rect2(0, fillTop, back.Size.X, back.Size.Y - fillTop), c);
            }
        }
        else
            DrawTextureRect(_back, back, false, BackColor);
        // the tank with its water (the faces, the surface) -- inside the group, riding its rise and the tank's own pop
        Vector2 tankOff = g + new Vector2(0f, _pieceOffset[2]);
        float fill = Mathf.Clamp(Fill, 0f, 1f);
        float surfaceY = TankRect.Position.Y + MathF.Floor(SurfaceTravel * (1f - fill) - 0.5f) + 1f; // drawWater's y for the surface picture
        float waterLine = surfaceY + SurfaceRect.Size.Y * 0.5f;                                     // the water shows below the surface's middle
        // perform (3999): drawWaterBack, then the screen's own panes (the tank among them), then drawWaterOrJuice -- the water over the tank
        if (_pieceShown[2])
        {
            DrawTextureRect(_tank, new Rect2(TankRect.Position + tankOff, TankRect.Size), false, Colors.White);
            if (fill > 0f)
            {
                DrawFace(_faceL, FaceLRect, waterLine, tankOff, FaceLColor);
                DrawFace(_faceR, FaceRRect, waterLine, tankOff, FaceRColor);
            }
            float surfaceAlpha = fill >= 1f ? 1f : fill <= 0f ? 0f : 0x50 / 255f; // updateWaterGaugeFill: unk2A0[0]->mAlpha
            if (surfaceAlpha > 0f)
                DrawTextureRect(_surface, new Rect2(new Vector2(SurfaceRect.Position.X, surfaceY) + tankOff, SurfaceRect.Size), false,
                    new Color(SurfaceColor, surfaceAlpha));
        }
        // "WATER" (detached: its own pop)
        if (_pieceShown[1])
            DrawTextureRect(_text, new Rect2(TextRect.Position + new Vector2(0f, _pieceOffset[1]), TextRect.Size), false, Colors.White);
        // the nozzle's icon and its X button (detached)
        int n = Mathf.Clamp(_shownIcon, 0, 3);
        Vector2 iconOff = new Vector2(0f, _pieceOffset[3]);
        if (_pieceShown[3])
            DrawTextureRect(_icons[n], new Rect2(IconRect[n].Position + iconOff, IconRect[n].Size), false, Colors.White);
        if (_xShown && _pieceShown[3])
            DrawTextureRect(_xButton, new Rect2(IconRect[n].Position + XButtonOffset[n] + iconOff, XButtonSize), false, Colors.White);
    }

    /// <summary>A face of the water: the coloured mask from the water line down.</summary>
    private void DrawFace(Texture2D tex, Rect2 pane, float waterLine, Vector2 off, Color color)
    {
        float cut = Mathf.Clamp(waterLine - pane.Position.Y, 0f, pane.Size.Y);
        if (cut >= pane.Size.Y)
            return;
        Rect2 dst = new Rect2(pane.Position + off + new Vector2(0f, cut), new Vector2(pane.Size.X, pane.Size.Y - cut));
        Rect2 src = new Rect2(0f, cut, pane.Size.X, pane.Size.Y - cut);
        DrawTextureRectRegion(tex, dst, src, color);
    }
}
