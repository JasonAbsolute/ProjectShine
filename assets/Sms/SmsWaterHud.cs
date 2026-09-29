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
/// startAppearTank (2180) / processAppearTank (3194): the group rises from 110 px below (465 - 355) over 20 frames; the disc, then at
/// frame 6 the text, 14 the tank, 26 the icon pop in over 40 frames through (0, 50) -> (0, -30) -> (0, 0) (cUpTop / cUpMid / cUpBot,
/// a quadratic through the three); the X button shows once the icon has settled. startDisappearTank (2201): 40 frames, 170 px down.
/// updateWaterTankState (1371): the X button's alpha is 0 for 70 frames and 255 until 270, again and again. Frames are the game's
/// 30 fps. The screen: the game's pixels scaled by the viewport height / 480, the group keeping its margins to the bottom-right
/// corner (the disc 19 px from the right edge, 39 px from the bottom).
/// </summary>
public partial class SmsWaterHud : Control
{
    private const string TexDir = "res://assets/Sms/hud/";
    private const float ScreenW = 600f, ScreenH = 480f;
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
    private int _frame;              // frames into the phase
    private int _shownIcon;          // unk310: the icon on screen (swapped only when settled)
    private int _xBlink;             // unk7C while shown
    private int _flash;              // unk30C
    private float _frameAcc;
    private float _groupOffset;      // the group's y offset (px), the TExPane interpolator
    private float _groupFrom, _groupTo; private int _groupFrames;
    private readonly float[] _pieceOffset = new float[4]; // disc, text, tank, icon
    private readonly int[] _pieceStart = { 0, 6, 14, 26 };
    private const int AppearFrames = 20, DisappearFrames = 40, PopFrames = 40;
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

    /// <summary>The PNGs: through the importer when the editor has seen them, else straight off the disk (a headless run before it has).</summary>
    private static Texture2D Load(string name)
    {
        string path = TexDir + name + ".png";
        if (ResourceLoader.Exists(path))
            return ResourceLoader.Load<Texture2D>(path);
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        return img == null || img.IsEmpty() ? null : ImageTexture.CreateFromImage(img);
    }

    private void Layout()
    {
        Vector2 vp = GetViewportRect().Size;
        float s = vp.Y / ScreenH;
        Scale = new Vector2(s, s);
        Position = new Vector2(vp.X - (ScreenW - GroupX) * s, vp.Y - (ScreenH - GroupY) * s);
    }

    public override void _Process(double delta)
    {
        if (!Ready)
            return;
        _frameAcc += (float)delta * 30f;
        while (_frameAcc >= 1f)
        {
            _frameAcc -= 1f;
            Tick();
        }
        QueueRedraw();
    }

    private static float Quad(float p0, float p1, float p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    /// <summary>One 30 fps frame of the console: the tank's appear / disappear and the X button's blink.</summary>
    private void Tick()
    {
        switch (_phase)
        {
            case Phase.Hidden:
                if (Wanted)
                {
                    _phase = Phase.Appearing; _frame = 0; _xShown = false; _xBlink = 0;
                    _groupFrom = ScreenH - 15f - GroupY; _groupTo = 0f; _groupFrames = AppearFrames; // 465 - 355
                    _shownIcon = Nozzle;
                    for (int i = 0; i < 4; i++) _pieceOffset[i] = 100f; // off, below
                    Visible = true;
                }
                break;
            case Phase.Appearing:
            {
                // startAppearTank: the disc (unk26C) rides 100 -> -30 -> 0 over 50 frames; processAppearTank pops the text (6), the
                // tank (14) and the icon (26) in with 50 -> -30 -> 0 over 40 frames each, the X button once the icon is home
                _groupOffset = Mathf.Lerp(_groupFrom, _groupTo, Mathf.Clamp(_frame / (float)_groupFrames, 0f, 1f));
                _pieceOffset[0] = Quad(100f, -30f, 0f, Mathf.Clamp(_frame / 50f, 0f, 1f));
                for (int i = 1; i < 4; i++)
                {
                    int f = _frame - _pieceStart[i];
                    _pieceOffset[i] = f < 0 ? 100f : Quad(50f, -30f, 0f, Mathf.Clamp(f / (float)PopFrames, 0f, 1f));
                }
                _frame++;
                if (_frame > _pieceStart[3] + PopFrames)
                {
                    _phase = Phase.Shown; _frame = 0; _xShown = true; _xBlink = 70; // shown at once, then the cycle
                    _groupOffset = 0f;
                    for (int i = 0; i < 4; i++) _pieceOffset[i] = 0f;
                }
                if (!Wanted)
                    StartDisappear();
                break;
            }
            case Phase.Shown:
                // updateWaterTankState: the X button hides for 70 frames of every 270
                _xBlink++;
                if (_xBlink >= 270) _xBlink = 0;
                _xShown = _xBlink >= 70;
                if (_shownIcon != Nozzle)
                    _shownIcon = Nozzle; // updateWaterGaugeFill swaps the icon once the interpolator is at zero: settled, it is
                if (!Wanted)
                    StartDisappear();
                break;
            case Phase.Disappearing:
                _groupOffset = Mathf.Lerp(_groupFrom, _groupTo, Mathf.Clamp(_frame / (float)_groupFrames, 0f, 1f));
                for (int i = 0; i < 4; i++)
                    _pieceOffset[i] = Mathf.Lerp(0f, 170f, Mathf.Clamp(_frame / (float)DisappearFrames, 0f, 1f)); // start 0, mid 85, end 170
                _frame++;
                if (_frame > DisappearFrames)
                {
                    _phase = Phase.Hidden; Visible = false;
                }
                break;
        }
        // the pressure flash counter runs while the charge is full (drawWaterBack)
        if (Pressure >= 1f) { _flash++; if (_flash >= 25) _flash = 0; }
        else _flash = 0;
    }

    private void StartDisappear()
    {
        _phase = Phase.Disappearing; _frame = 0; _xShown = false;
        _groupFrom = _groupOffset; _groupTo = 170f + (ScreenH - 15f - GroupY); _groupFrames = DisappearFrames; // 465 - y1 + 60, then some
    }

    /// <summary>getPressureFlashColor (97): FF3F3F -> FFFF00 over 10 frames, held 5, back over 10.</summary>
    private static Color FlashColor(int frame)
    {
        float g = 0x3F, b = 0x3F;
        if (frame < 10) { g += frame * 19.2f; b -= frame * 6.3f; }
        else if (frame < 15) { g = 255f; b = 0f; }
        else { int fade = 25 - frame; g += fade * 19.2f; b -= fade * 6.3f; }
        return new Color(1f, Mathf.Clamp(g, 0f, 255f) / 255f, Mathf.Clamp(b, 0f, 255f) / 255f, 0xC8 / 255f);
    }

    public override void _Draw()
    {
        if (!Ready || _phase == Phase.Hidden)
            return;
        Vector2 g = new Vector2(0f, _groupOffset);
        // the disc, blue; the pressure gauge fills it from the bottom (drawWaterBack)
        Rect2 back = new Rect2(BackRect.Position + g + new Vector2(0f, _pieceOffset[0]), BackRect.Size);
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
        // the tank with its water (the faces, the surface) -- they ride the tank's pop
        Vector2 tankOff = g + new Vector2(0f, _pieceOffset[2]);
        float fill = Mathf.Clamp(Fill, 0f, 1f);
        float surfaceY = TankRect.Position.Y + MathF.Floor(SurfaceTravel * (1f - fill) - 0.5f) + 1f; // drawWater's y for the surface picture
        float waterLine = surfaceY + SurfaceRect.Size.Y * 0.5f;                                     // the water shows below the surface's middle
        // perform (3999): drawWaterBack, then the screen's own panes (the tank among them), then drawWaterOrJuice -- the water over the tank
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
        // "WATER"
        DrawTextureRect(_text, new Rect2(TextRect.Position + g + new Vector2(0f, _pieceOffset[1]), TextRect.Size), false, Colors.White);
        // the nozzle's icon and its X button
        int n = Mathf.Clamp(_shownIcon, 0, 3);
        Vector2 iconOff = g + new Vector2(0f, _pieceOffset[3]);
        DrawTextureRect(_icons[n], new Rect2(IconRect[n].Position + iconOff, IconRect[n].Size), false, Colors.White);
        if (_xShown)
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
