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
/// display, the motions at the scene root's AnimSpeed times the pace. The screen is the game's 640 x 448 frame (SmsHud): the group keeps
/// its margins to the bottom-right corner (the disc 59 px from the right edge, 7 px from the bottom; "WATER" ran 3 px past it on the
/// disc, into the CRT's overscan), moved by the root's right / bottom shifts, all scaled by SmsHud.Scale.
/// The gauge is the WaterHud Control of assets/Sms/Console/SmsHud.tscn -- its place in the scene its place on the frame, its Anchor the
/// window corner it keeps to -- with the pieces as children laid out as the .blo has them: BlueClip/Disc and RedClip/DiscPressure
/// (the disc and the pressure gauge's red, each in a clip Control the code sizes), Tank, FaceLClip/FaceL and FaceRClip/FaceR (the
/// water's faces, clipped at the water line), Surface, Text ("WATER"), Icon0..3 (the nozzles' icons, each with its X button as a
/// child). The code reads the layout from the nodes and moves them.
/// </summary>
public partial class SmsWaterHud : Control
{
    private const float GroupY = 355f;                        // the .blo's row (the rise below starts from 465)
    /// <summary>The window corner the gauge keeps its frame distance from (the game: the bottom right).</summary>
    [Export] public SmsHudGroup.HudAnchor Anchor = SmsHudGroup.HudAnchor.BottomRight;

    // the scene's pieces, and their rects in the group's pixels as the scene lays them out
    private TextureRect _disc, _discPressure, _tank, _faceL, _faceR, _surface, _text;
    private Control _blueClip, _redClip, _faceLClip, _faceRClip;
    private readonly TextureRect[] _icons = new TextureRect[4], _xButtons = new TextureRect[4];
    private Rect2 BackRect, TankRect, FaceLRect, FaceRRect, SurfaceRect, TextRect;
    private readonly Rect2[] IconRect = new Rect2[4];
    private Vector2 _home;
    private static readonly Color FaceLColor = new Color(0x64 / 255f, 0xDC / 255f, 1f);
    private static readonly Color FaceRColor = new Color(0f, 0xB4 / 255f, 0xF0 / 255f);
    private static readonly Color SurfaceColor = new Color(0xBE / 255f, 0xF0 / 255f, 1f);
    private static readonly Color BackColor = new Color(0f, 0f, 1f, 0x78 / 255f);
    private static readonly Color PressureColor = new Color(1f, 0x3F / 255f, 0x3F / 255f, 0xC8 / 255f);
    private const float SurfaceTravel = 47f;

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
        MouseFilter = MouseFilterEnum.Ignore;
        _home = Position;
        try
        {
            _blueClip = GetNode<Control>("BlueClip"); _disc = GetNode<TextureRect>("BlueClip/Disc");
            _redClip = GetNode<Control>("RedClip"); _discPressure = GetNode<TextureRect>("RedClip/DiscPressure");
            _tank = GetNode<TextureRect>("Tank");
            _faceLClip = GetNode<Control>("FaceLClip"); _faceL = GetNode<TextureRect>("FaceLClip/FaceL");
            _faceRClip = GetNode<Control>("FaceRClip"); _faceR = GetNode<TextureRect>("FaceRClip/FaceR");
            _surface = GetNode<TextureRect>("Surface"); _text = GetNode<TextureRect>("Text");
            for (int i = 0; i < 4; i++)
            {
                _icons[i] = GetNode<TextureRect>($"Icon{i}");
                _xButtons[i] = _icons[i].GetNode<TextureRect>("X");
                IconRect[i] = new Rect2(_icons[i].Position, _icons[i].Size);
            }
            Ready = true;
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"[SMS hud] SmsHud.tscn: a piece is missing from the WaterHud ({e.Message}) -- the gauge stays off");
            Ready = false;
            return;
        }
        BackRect = new Rect2(_blueClip.Position, _blueClip.Size);
        TankRect = new Rect2(_tank.Position, _tank.Size);
        FaceLRect = new Rect2(_faceLClip.Position, _faceLClip.Size);
        FaceRRect = new Rect2(_faceRClip.Position, _faceRClip.Size);
        SurfaceRect = new Rect2(_surface.Position, _surface.Size);
        TextRect = new Rect2(_text.Position, _text.Size);
        Visible = false;
        Layout();
        GetViewport().SizeChanged += Layout;
    }

    private float _s = 1f; private Vector2 _vp;

    private void Layout()
    {
        _vp = GetViewportRect().Size;
        _s = SmsHud.Scale(_vp);
        Scale = new Vector2(_s, _s);
        Position = SmsHud.Anchored(_home, Anchor, _s, _vp);
    }

    public override void _Process(double delta)
    {
        if (!Ready)
            return;
        float df = (float)delta * SmsHud.Fps;
        _now += df;
        Tick(df);
        Apply();
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

    /// <summary>The frame onto the scene's pieces: the disc and the pressure gauge's red through their clips, the tank riding the
    /// group's rise, the water's faces cut at the water line, the surface, "WATER", the nozzle's icon and its X.</summary>
    private void Apply()
    {
        if (!Ready || _phase == Phase.Hidden)
            return;
        Vector2 g = new Vector2(0f, _groupOffset);
        // the disc, blue (detached from the group: its own pop only); the pressure gauge fills it from the bottom (drawWaterBack)
        Vector2 discOff = new Vector2(0f, _pieceOffset[0]);
        float fillTop = BackRect.Size.Y;
        Color red = PressureColor;
        if (Pressure >= 0f)
        {
            float ratio = Mathf.Clamp(Pressure, 0f, 1f);
            fillTop = MathF.Floor((1f - ratio) * BackRect.Size.Y);
            if (ratio >= 1f)
                red = FlashColor(_flash);
        }
        _blueClip.Position = BackRect.Position + discOff;
        _blueClip.Size = new Vector2(BackRect.Size.X, fillTop);
        _blueClip.Visible = fillTop > 0f;
        _redClip.Visible = fillTop < BackRect.Size.Y;
        _redClip.Position = BackRect.Position + discOff + new Vector2(0f, fillTop);
        _redClip.Size = new Vector2(BackRect.Size.X, BackRect.Size.Y - fillTop);
        _discPressure.Position = new Vector2(0f, -fillTop);
        _discPressure.Modulate = red;
        // the tank with its water (the faces, the surface) -- inside the group, riding its rise and the tank's own pop
        Vector2 tankOff = g + new Vector2(0f, _pieceOffset[2]);
        float fill = Mathf.Clamp(Fill, 0f, 1f);
        float surfaceY = TankRect.Position.Y + MathF.Floor(SurfaceTravel * (1f - fill) - 0.5f) + 1f; // drawWater's y for the surface picture
        float waterLine = surfaceY + SurfaceRect.Size.Y * 0.5f;                                     // the water shows below the surface's middle
        // perform (3999): drawWaterBack, then the screen's own panes (the tank among them), then drawWaterOrJuice -- the water over the tank
        _tank.Visible = _pieceShown[2];
        _tank.Position = TankRect.Position + tankOff;
        Face(_faceLClip, _faceL, FaceLRect, waterLine, tankOff, _pieceShown[2] && fill > 0f);
        Face(_faceRClip, _faceR, FaceRRect, waterLine, tankOff, _pieceShown[2] && fill > 0f);
        float surfaceAlpha = fill >= 1f ? 1f : fill <= 0f ? 0f : 0x50 / 255f; // updateWaterGaugeFill: unk2A0[0]->mAlpha
        _surface.Visible = _pieceShown[2] && surfaceAlpha > 0f;
        _surface.Position = new Vector2(SurfaceRect.Position.X, surfaceY) + tankOff;
        _surface.Modulate = new Color(SurfaceColor, surfaceAlpha);
        // "WATER" (detached: its own pop)
        _text.Visible = _pieceShown[1];
        _text.Position = TextRect.Position + new Vector2(0f, _pieceOffset[1]);
        // the nozzle's icon and its X button (detached)
        int n = Mathf.Clamp(_shownIcon, 0, 3);
        Vector2 iconOff = new Vector2(0f, _pieceOffset[3]);
        for (int i = 0; i < 4; i++)
        {
            _icons[i].Visible = _pieceShown[3] && i == n;
            _icons[i].Position = IconRect[i].Position + iconOff;
            _xButtons[i].Visible = _xShown;
        }
    }

    /// <summary>A face of the water: the coloured mask from the water line down -- the clip Control cut to that, the picture held still
    /// inside it.</summary>
    private static void Face(Control clip, TextureRect face, Rect2 pane, float waterLine, Vector2 off, bool shown)
    {
        float cut = Mathf.Clamp(waterLine - pane.Position.Y, 0f, pane.Size.Y);
        clip.Visible = shown && cut < pane.Size.Y;
        clip.Position = pane.Position + off + new Vector2(0f, cut);
        clip.Size = new Vector2(pane.Size.X, pane.Size.Y - cut);
        face.Position = new Vector2(0f, -cut);
    }
}
