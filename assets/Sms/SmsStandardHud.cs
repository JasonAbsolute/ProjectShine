using Godot;
using System;

/// <summary>
/// The game's standard HUD besides the water gauge: TGCConsole2 (GC2D/GCConsole2.cpp) driving the panes of game_6/scrn/standard_1.blo
/// (tools/projectshine/blo_dump.py; a 600 x 480 screen) with the disc's textures (game_6/timg, assets/Sms/hud/*.png):
///   c_0 (13, 152) the coin counter: coin_back 198 x 22 (an I4 strip tinted 00FF00, alpha 180), coin_icon 30 x 40 at (9, -14), the x
///       (coin_number_x) 36 x 36 at (31, -16), the digits coin_number_0..9 36 x 36 at (60, -22) (87, -27) (114, -32) -- two digits below
///       100 (the tens shown even at 0), three from 100 (setCounterDigits 660). Its home is the shine counter's row (offset unk26A =
///       74 - 152); it steps down to its own row while the shine and blue coin counters are out. The strip, the x and the digits are
///       turned 15 degrees about their top-left corners (the .blo's rotation; the icon stays upright): the counters' slant.
///   d_0 (13, 113) the blue coins (bluecoin_icon), s_0 (13, 74) the shines (shine_icon 48 x 58 at (2, -29), x at (38, -18), digits at
///       (67, -24) (94, -29) (121, -34)) -- the "star" HUD, out for a while when a blue coin or a shine is taken and as a peek after
///       Mario has stood still (updateCounterState 934: 200 frames of MARIO_STATUS_WAIT; unk30 starts at -400 but the intro demo zeroes it).
///   m_0 (223, 74) the lives: coin_back, mario_icon 43 x 47 at (13, -23), mario_text 72 x 34 at (-12, -34), x at (48, -20), two digits
///       at (77, -26) (104, -31) (setTwoDigits: the tens even at 0). Out at the level's start and with the star HUD's peek, away with it.
///   l_0 (500, 50) the life meter: water_back 74 x 71 as an orange disc (FF4C00, alpha C8 x the pane's 200), the sun life_icon_0 54 x 58
///       at (9, 11) and the eight rays life_icon_1..8 at (53, 0) (59, 23) (57, 48) (38, 61) (13, 64) (-7, 49) (-12, 27) (-8, 2), each a lit
///       picture (_1) over its grey socket (_2), "LIFE" life_text 60 x 30 at (7, -18). Hidden at 8; a hit brings it in at
///       (-150, +180) from home (startAppearLife 2323: the middle-right of the screen), held 100 frames, then a 40-frame curve through
///       (-10, 10) to the corner (processAppearLife 3008); back at 8 it holds 120 frames and slides up and away (startLifeMeterDisappear
///       339, state 3); a camera demo slides it out at once and it comes back from above at home (startInsertLife 2260: the disc, then
///       "LIFE" at 14, the sun at 24, popping). The segments follow mHealth one a frame (updateLifeMeterState 556), the sun and the
///       text dim to 7F below 4 (updateLifeMeterColors 366), and the lit pieces beat (updateLifeMeterBlink 1085): 3 px in from every
///       side for half of a 200-frame cycle, 160 / 120 / 70 frames and 5 px at 3 / 2 / 1. Under water (the meter shows air, Mario's
///       240-tick rule) the disc is the water gauge's blue (0000FF, alpha 78) and the lit pieces go through black 003CFF00 / white
///       00FFFFFF (the sun and text 0010FFFF below 4).
///   r_0 (13, 419) the red coins: coin_back tinted FF28FF, redcoin_icon at (19, -16), x at (41, -18), the count at (70, -38), balloon_slash
///       50 x 60 at (87, -41), the 8 (coin_number_8 28 x 28) at (113, -16); it rises from below when the first red coin is taken
///       (startAppearRedCoin 2553) and drops off the bottom for a camera demo (startDownLeftBot 2366).
/// The pops are TBoundPane quadratics (SmsHud.Pop): the counters drop from (0, -100) through (0, 30) over 50 frames and settle over 30
/// (cDownTop / cDownMid), the red coins rise from (0, 50) through (0, -30), the coin counter's row step goes through (0, 45); the
/// groups slide linearly (TExPane): the coins 20 frames, the lives 50, the star HUD 40 (the shine group leading the blue one by
/// nothing). The pieces of a counter come in staggered: the strip at 0, the icon at 14, the x at 24, the digits at 28 / 34 / 40
/// (processAppearCoin 3236, processAppearStar 3055, processAppearMario 3275: icon 14, text and x 24, digits 24 / 30). A camera demo
/// (startCameraDemo 2056: the camera locked, a talk, the shine get, a death) takes the lives, the coins and the star HUD away (the
/// shine get brings the star HUD in instead) and endCameraDemo (2125) brings the coins back.
/// Frames are the game's 30 a second, counted fractionally so the motion runs at the display's rate; the motions (not the waits) go at
/// SmsParams.Hud.AnimSpeed times the game's pace. Where the game parks a hidden strip 11 px into the frame (the CRT's overscan hid it:
/// initHiddenPaneAbove) this parks it fully above the top edge. The screen is the game's 640 x 448 frame (SmsHud): the counters keep
/// their margin to the left edge, the lives (centred on the frame) to the centre, the life meter to the right, the red coins to the
/// bottom, moved by SmsParams.Hud's shifts and scaled by SmsHud.Scale; with SmsParams.Hud.IdleHold the star HUD and the lives stay
/// while Mario stands still instead of the game's peek.
/// </summary>
public partial class SmsStandardHud : Control
{
    // ---- what Mario tells it every physics frame (the flag manager's counts, his status) ----
    public int Coins, Lives = 4, Shines, BlueCoins, RedCoins;
    public int Health = 8;          // mHealth, 0..8
    public int Air = 8;             // (int)mAir, 0..8, while the meter shows air
    public bool AirMode;            // GCConsole2's meter switched to air (Mario's 240-tick rule)
    public bool Idle;               // MARIO_STATUS_WAIT
    public bool Demo;               // a camera demo runs: the camera locked, a talk, the shine get, death
    public bool ShineDemo;          // the demo is the shine get (DIRECTOR_FLAG_SHINE_TAKEN)
    public bool Dead;
    public bool Ready { get; private set; }

    private float _now;             // the console's frames, fractional
    private int _frame;             // the last whole frame stepped

    // ---- the textures ----
    private Texture2D _strip, _coinIcon, _blueIcon, _redIcon, _shineIcon, _x, _slash, _marioIcon, _marioText, _lifeText, _lifeTextAir, _lifeTextAirLow, _disc;
    private readonly Texture2D[] _digit = new Texture2D[10];
    private readonly Texture2D[] _lifeOn = new Texture2D[9], _lifeOff = new Texture2D[9], _lifeAir = new Texture2D[9], _lifeAirLow = new Texture2D[9];

    private static readonly Color StripGreen = new Color(0f, 1f, 0f, 180f / 255f);
    private static readonly Color StripMagenta = new Color(1f, 0x28 / 255f, 1f, 180f / 255f);
    private static readonly Color DiscHealth = new Color(1f, 0x4C / 255f, 0f, (0xC8 / 255f) * (200f / 255f));
    private static readonly Color DiscAir = new Color(0f, 0f, 1f, (0x78 / 255f) * (200f / 255f));
    private static readonly Color Dim = new Color(0x7F / 255f, 0x7F / 255f, 0x7F / 255f);
    private static readonly Color AirBlack = new Color(0f, 0x3C / 255f, 1f, 0f), AirWhite = new Color(0f, 1f, 1f, 1f), AirWhiteLow = new Color(0f, 0x10 / 255f, 1f, 1f);

    // ---- the groups ----
    private static readonly Vector2 CoinPos = new Vector2(13, 152), BluePos = new Vector2(13, 113), ShinePos = new Vector2(13, 74);
    private static readonly Vector2 LifePos = new Vector2(500, 50), MarioPos = new Vector2(223, 74), RedPos = new Vector2(13, 419);
    private const float CoinHome = 74f - 152f;          // unk26A: the coin counter's row while the star HUD is away
    private const float CoinHidden = -(152f + 10f + 1f) - 22f;  // startDisappearCoin: -(y2 + 1) less the strip
    private const float MarioHidden = -(74f + 10f + 1f) - 22f;
    private const float ShineHidden = -(74f + 10f + 1f) - 22f, ShineGone = -(74f + 10f + 1f) + CoinHome; // startDisappearStar's target
    private const float BlueHidden = -(113f + 10f + 1f) - 22f;
    private const float RedBelow = 465f - 419f, RedGone = 525f - 419f;
    private const float LifeAbove = -(50f + 10f + 1f);   // startInsertLife: -(unk4.y2 + 1)
    private const float LifeGone = LifeAbove - 71f - 22f; // startLifeMeterDisappear: on past the disc's height, fully out of the frame
    private const int IdleFrames = 200;                 // updateCounterState: 0xC8 frames of MARIO_STATUS_WAIT
    private const int BlueHold = 400;                   // unk16C: 0x190 frames after a blue coin
    private const int LifeHold = 120;                   // state 3: 0x78 frames at 8 before the meter leaves
    private const int LifeAppearHold = 100;             // processAppearLife: the frames in the middle of the screen

    // coins
    private readonly SmsHudSlide _coinSlide = new SmsHudSlide(CoinHidden);
    private bool _coinVisible; private int _coinF0 = -1; private int _coinDownF0 = -1;
    private SmsHudPane _cStrip, _cIcon, _cX; private readonly SmsHudPane[] _cDigit = new SmsHudPane[3];
    private int _coinShown, _coinRoll;
    // lives
    private readonly SmsHudSlide _marioSlide = new SmsHudSlide(MarioHidden);
    private bool _marioVisible, _marioLeaving; private int _marioF0 = -1;
    private SmsHudPane _mStrip, _mIcon, _mText, _mX; private readonly SmsHudPane[] _mDigit = new SmsHudPane[2];
    private int _livesShown = -1;
    // the star HUD
    private readonly SmsHudSlide _shineSlide = new SmsHudSlide(ShineHidden), _blueSlide = new SmsHudSlide(BlueHidden);
    private bool _starVisible, _starAppearing, _starLeaving; private int _starF0 = -1;
    private SmsHudPane _sStrip, _sIcon, _sX, _dStrip, _dIcon, _dX; private readonly SmsHudPane[] _sDigit = new SmsHudPane[3], _dDigit = new SmsHudPane[3];
    private int _shineShown, _blueShown = -1, _blueTimer, _shineTimer, _idleCount = -400;
    // red coins
    private readonly SmsHudSlide _redSlide = new SmsHudSlide(RedBelow);
    private bool _redVisible, _redParked; private int _redF0 = -1;
    private SmsHudPane _rStrip, _rIcon, _rX, _rDigit, _rSlash, _rEight;
    private int _redShown = -1;
    // the life meter
    private enum Life { Hidden, Appear, Insert, Shown, Hold, Leave, DemoHidden }
    private Life _life = Life.Hidden; private int _lifeF0 = -1; private Vector2 _lifeLeaveFrom;
    private SmsHudPane _lDisc, _lText, _lSun; private readonly SmsHudPane[] _lRay = new SmsHudPane[9], _lSocket = new SmsHudPane[9];
    private int _lifeShown = 8; private bool _lifeAirShown; private int _beat;
    // the demo
    private bool _inDemo, _started; private int _demoEndCount;

    public override void _Ready()
    {
        _strip = SmsHud.Load("coin_back"); _coinIcon = SmsHud.Load("coin_icon"); _blueIcon = SmsHud.Load("bluecoin_icon"); _redIcon = SmsHud.Load("redcoin_icon");
        _shineIcon = SmsHud.Load("shine_icon"); _x = SmsHud.Load("coin_number_x"); _slash = SmsHud.Load("balloon_slash");
        _marioIcon = SmsHud.Load("mario_icon"); _marioText = SmsHud.Load("mario_text"); _lifeText = SmsHud.Load("life_text"); _disc = SmsHud.Load("water_back");
        bool ok = _strip != null && _coinIcon != null && _blueIcon != null && _redIcon != null && _shineIcon != null && _x != null && _slash != null
            && _marioIcon != null && _marioText != null && _lifeText != null && _disc != null;
        for (int i = 0; i < 10; i++) { _digit[i] = SmsHud.Load($"coin_number_{i}"); ok &= _digit[i] != null; }
        for (int i = 0; i < 9; i++)
        {
            _lifeOn[i] = SmsHud.Load($"life_icon_{i}_1"); _lifeOff[i] = SmsHud.Load($"life_icon_{i}_2");
            ok &= _lifeOn[i] != null && _lifeOff[i] != null;
        }
        Ready = ok;
        if (Ready)
        {
            for (int i = 0; i < 9; i++)
            {
                _lifeAir[i] = SmsHud.Recolor(_lifeOn[i], AirBlack, AirWhite);
                _lifeAirLow[i] = SmsHud.Recolor(_lifeOn[i], AirBlack, AirWhiteLow);
            }
            _lifeTextAir = SmsHud.Recolor(_lifeText, AirBlack, AirWhite);
            _lifeTextAirLow = SmsHud.Recolor(_lifeText, AirBlack, AirWhiteLow);
            BuildPanes();
        }
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 10;
        Layout();
        GetViewport().SizeChanged += Layout;
    }

    private void BuildPanes()
    {
        // the .blo turns the strips, the x's, the digits and "MARIO" 15 degrees about their top-left corners (the icons stay upright,
        // the Mario head leans 5 the other way): the counters' slant
        const float Slant = 15f, HeadSlant = 355f;
        _cStrip = new SmsHudPane(0, 0, 198, 22, _strip) { Tint = StripGreen, Rot = Slant }; _cIcon = new SmsHudPane(9, -14, 30, 40, _coinIcon); _cX = new SmsHudPane(31, -16, 36, 36, _x) { Rot = Slant };
        _cDigit[0] = new SmsHudPane(60, -22, 36, 36, _digit[0]) { Rot = Slant }; _cDigit[1] = new SmsHudPane(87, -27, 36, 36, _digit[0]) { Rot = Slant }; _cDigit[2] = new SmsHudPane(114, -32, 36, 36, _digit[0]) { Rot = Slant };
        _dStrip = new SmsHudPane(0, 0, 198, 22, _strip) { Tint = StripGreen, Rot = Slant }; _dIcon = new SmsHudPane(9, -14, 30, 40, _blueIcon); _dX = new SmsHudPane(31, -16, 36, 36, _x) { Rot = Slant };
        _dDigit[0] = new SmsHudPane(60, -22, 36, 36, _digit[0]) { Rot = Slant }; _dDigit[1] = new SmsHudPane(87, -27, 36, 36, _digit[0]) { Rot = Slant }; _dDigit[2] = new SmsHudPane(114, -32, 36, 36, _digit[0]) { Rot = Slant };
        _sStrip = new SmsHudPane(0, 0, 198, 22, _strip) { Tint = StripGreen, Rot = Slant }; _sIcon = new SmsHudPane(2, -29, 48, 58, _shineIcon); _sX = new SmsHudPane(38, -18, 36, 36, _x) { Rot = Slant };
        _sDigit[0] = new SmsHudPane(67, -24, 36, 36, _digit[0]) { Rot = Slant }; _sDigit[1] = new SmsHudPane(94, -29, 36, 36, _digit[0]) { Rot = Slant }; _sDigit[2] = new SmsHudPane(121, -34, 36, 36, _digit[0]) { Rot = Slant };
        _mStrip = new SmsHudPane(0, 0, 198, 22, _strip) { Tint = StripGreen, Rot = Slant }; _mIcon = new SmsHudPane(13, -23, 43, 47, _marioIcon) { Rot = HeadSlant };
        _mText = new SmsHudPane(-12, -34, 72, 34, _marioText) { Rot = Slant }; _mX = new SmsHudPane(48, -20, 36, 36, _x) { Rot = Slant };
        _mDigit[0] = new SmsHudPane(77, -26, 36, 36, _digit[0]) { Rot = Slant }; _mDigit[1] = new SmsHudPane(104, -31, 36, 36, _digit[0]) { Rot = Slant };
        _rStrip = new SmsHudPane(0, 0, 198, 22, _strip) { Tint = StripMagenta, Rot = Slant }; _rIcon = new SmsHudPane(19, -16, 30, 40, _redIcon); _rX = new SmsHudPane(41, -18, 36, 36, _x) { Rot = Slant };
        _rDigit = new SmsHudPane(70, -38, 36, 36, _digit[0]) { Rot = Slant }; _rSlash = new SmsHudPane(87, -41, 50, 60, _slash); _rEight = new SmsHudPane(113, -16, 28, 28, _digit[8]) { Rot = Slant };
        _lDisc = new SmsHudPane(0, 0, 74, 71, _disc) { Tint = DiscHealth }; _lText = new SmsHudPane(7, -18, 60, 30, _lifeText);
        _lSun = new SmsHudPane(0, 0, 0, 0, null) { Shown = true }; // lm_0: the sun and rays ride its pop
        int[,] ray = { { 9, 11, 54, 58 }, { 53, 0, 26, 28 }, { 59, 23, 26, 24 }, { 57, 48, 26, 28 }, { 38, 61, 26, 28 }, { 13, 64, 24, 26 }, { -7, 49, 28, 28 }, { -12, 27, 24, 24 }, { -8, 2, 28, 28 } };
        for (int i = 0; i < 9; i++)
        {
            _lSocket[i] = new SmsHudPane(ray[i, 0], ray[i, 1], ray[i, 2], ray[i, 3], _lifeOff[i]) { Shown = true };
            _lRay[i] = new SmsHudPane(ray[i, 0], ray[i, 1], ray[i, 2], ray[i, 3], _lifeOn[i]) { Shown = true };
        }
        _lifeShown = Mathf.Clamp(Health, 0, 8);
        for (int i = 0; i < 9; i++) _lRay[i].Shown = i <= _lifeShown;
        _coinShown = Mathf.Clamp(Coins, 0, 999); SetCounterDigits(_cDigit, _coinShown);
        _shineShown = Mathf.Clamp(Shines, 0, 999); SetCounterDigits(_sDigit, _shineShown);
        _blueShown = Mathf.Clamp(BlueCoins, 0, 999); SetCounterDigits(_dDigit, _blueShown);
        _livesShown = Mathf.Clamp(Lives, 0, 99); SetTwoDigits(_mDigit, _livesShown);
        _redShown = Mathf.Clamp(RedCoins, 0, 8); _rDigit.Tex = _digit[_redShown];
    }

    private float _scale = 1f; private Vector2 _viewport;

    private void Layout()
    {
        _viewport = GetViewportRect().Size;
        _scale = SmsHud.Scale(_viewport);
        Position = Vector2.Zero;
        Size = _viewport;
    }

    public override void _Process(double delta)
    {
        if (!Ready)
            return;
        _now += (float)delta * SmsHud.Fps;
        int whole = (int)MathF.Floor(_now);
        int guard = 0;
        while (_frame < whole && guard++ < 8) // a stall skips frames rather than replaying a second's worth of logic
        {
            _frame++;
            Step();
        }
        if (_frame < whole) _frame = whole;
        QueueRedraw();
    }

    /// <summary>A process function's "param_1 == N": true on the first console frame at or past game frame N at the port's pace.</summary>
    private static bool At(int f, int game) => f >= (int)MathF.Ceiling(game / SmsHud.Anim) && f - 1 < (int)MathF.Ceiling(game / SmsHud.Anim);
    private static int Paced(int game) => (int)MathF.Ceiling(game / SmsHud.Anim);

    // ------------------------------------------------------------------------------------------------------------------ one frame
    private void Step()
    {
        // the camera demo (startCameraDemo / endCameraDemo)
        if (Demo && !_inDemo)
            StartCameraDemo();
        else if (!Demo && _inDemo)
        {
            if (++_demoEndCount >= 3) // endCameraDemo waits three frames
                EndCameraDemo();
        }
        else
            _demoEndCount = 0;

        if (!_started && !Demo)
        {
            // perform (3854), unk39: the first frame of the level brings the coin counter and the lives in
            _started = true;
            StartAppearCoin();
            StartAppearMario();
        }

        StepLife();
        StepCounters();
        StepStar();
        StepCoins();
        StepMario();
        StepRed();
    }

    // ------------------------------------------------------------------------------------------------------------------ the demo
    private void StartCameraDemo()
    {
        _inDemo = true; _demoEndCount = 0;
        if (Dead)
        {
            // startCameraDemo with no health: everything off at once
            _coinVisible = false; _marioVisible = false; _marioLeaving = false; _starVisible = false; _starAppearing = false; _starLeaving = false;
            _redVisible = false; _redParked = false; _life = Life.Hidden;
            _coinSlide.Set(_now, CoinHidden, CoinHidden, 0); _marioSlide.Set(_now, MarioHidden, MarioHidden, 0);
            _shineSlide.Set(_now, ShineHidden, ShineHidden, 0); _blueSlide.Set(_now, BlueHidden, BlueHidden, 0); _redSlide.Set(_now, RedBelow, RedBelow, 0);
            return;
        }
        if (_life == Life.Appear || _life == Life.Insert || _life == Life.Shown || _life == Life.Hold)
            StartLifeLeave(); // startLifeMeterDisappear(this, 120): out now, state 10 until the demo ends
        else if (_life == Life.Hidden)
            _life = Life.DemoHidden;
        if (_redVisible) { _redSlide.Retarget(_now, RedGone, 20); _redParked = true; } // startDownLeftBot
        StartDisappearMario();
        if (ShineDemo)
            StartAppearStar();      // DIRECTOR_FLAG_SHINE_TAKEN: the counters come in for the shine
        else
        {
            StartDisappearStar();   // (a no-op when it is away)
            StartDisappearCoin();
        }
    }

    private void EndCameraDemo()
    {
        _inDemo = false; _demoEndCount = 0;
        if (_redParked) { _redParked = false; StartAppearRedCoin(); }
        if (!_coinVisible)
            StartAppearCoin();
        if (_life == Life.DemoHidden)
        {
            // state 10: back from above if there is anything to show
            if (AirMode || Mathf.Clamp(Health, 0, 8) != 8)
                StartLifeInsert();
            else
            {
                _life = Life.Hidden; _lifeShown = 8; ResetLifePieces(); // unk1CC = 8: the count caught up while it was away
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ counters
    private void SetCounterDigits(SmsHudPane[] panes, int value)
    {
        // setCounterDigits (660): two digits below 100, three from 100 (the third pane shown only then)
        if (value < 100)
        {
            panes[0].Tex = _digit[value / 10]; panes[1].Tex = _digit[value % 10]; panes[2].Used = false;
        }
        else
        {
            panes[0].Tex = _digit[value / 100]; panes[1].Tex = _digit[(value / 10) % 10]; panes[2].Tex = _digit[value % 10]; panes[2].Used = true;
        }
    }

    private void SetTwoDigits(SmsHudPane[] panes, int value)
    {
        panes[0].Tex = _digit[(value / 10) % 10]; panes[1].Tex = _digit[value % 10];
    }

    private void StepCounters()
    {
        // updateCounterState (934): the coins roll one at a time (updateCoinCounterAnimation 792: a step, a frame for the panes)
        int coins = Mathf.Clamp(Coins, 0, 999);
        if (_coinShown != coins)
        {
            if (--_coinRoll <= 0)
            {
                _coinShown += coins > _coinShown ? 1 : -1;
                SetCounterDigits(_cDigit, _coinShown);
                _coinRoll = 2;
            }
        }
        else
            _coinRoll = 0;

        // the idle peek: MARIO_STATUS_WAIT with the star HUD away, 200 frames (unk30 starts at -400; any other frame zeroes it, as the
        // game's intro demo does, so it is 200 here too)
        bool starAway = !_starVisible || !_shineSlide.AtZero(_now);
        if (Idle && !Demo && starAway)
        {
            if (++_idleCount > IdleFrames)
            {
                StartAppearStar();
                StartAppearMario();
                _idleCount = 0;
            }
        }
        else
            _idleCount = 0;

        // the blue coins: one a frame toward the count, the star HUD out if it is not, 400 frames on the clock
        int blue = Mathf.Clamp(BlueCoins, 0, 999);
        if (_blueShown != blue)
        {
            _blueShown += blue > _blueShown ? 1 : -1;
            SetCounterDigits(_dDigit, _blueShown);
            if (!_starVisible || _starLeaving)
                StartAppearStar();
            _blueTimer = 1;
        }
        if (_blueTimer != 0 && ++_blueTimer > BlueHold)
            _blueTimer = 0;

        // the shines: the count as Mario's shine get hands it over (the game's demo flips it at frame 252 of its own dance), and the
        // star HUD stays out for the blue coins' 400 frames afterwards (in the game the level ends before it would go)
        int shines = Mathf.Clamp(Shines, 0, 999);
        if (_shineShown != shines)
        {
            _shineShown = shines;
            SetCounterDigits(_sDigit, _shineShown);
            if (!_starVisible || _starLeaving)
                StartAppearStar();
            _shineTimer = 1;
        }
        if (_shineTimer != 0 && ++_shineTimer > BlueHold)
            _shineTimer = 0;

        // the lives (updateMarioLifeCounter 674): a change sets the digits and brings the counter in
        int lives = Mathf.Clamp(Lives, 0, 99);
        if (_livesShown != lives)
        {
            _livesShown = lives;
            SetTwoDigits(_mDigit, lives);
            StartAppearMario();
        }

        // the red coins (updateRedCoinCounter 771)
        int red = Mathf.Clamp(RedCoins, 0, 8);
        if (_redShown != red)
        {
            _redShown = red;
            _rDigit.Tex = _digit[red];
            if (!_redVisible)
                StartAppearRedCoin();
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ coins
    private void StartAppearCoin()
    {
        if (_coinVisible)
            return;
        _coinVisible = true;
        _coinF0 = _frame; _coinDownF0 = -1;
        _coinSlide.Set(_now, CoinHidden, CoinHome, 20);
        _cStrip.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
        _cIcon.Shown = false; _cX.Shown = false;
        foreach (var d in _cDigit) d.Shown = false;
    }

    private void StartDisappearCoin()
    {
        if (!_coinVisible)
            return;
        _coinSlide.Retarget(_now, CoinHidden, 40);
        _coinF0 = -1; _coinDownF0 = -1;
    }

    private void StepCoins()
    {
        if (!_coinVisible)
            return;
        if (_coinF0 >= 0)
        {
            // processAppearCoin (3236): the icon at 14, the x at 24, the digits at 28 / 34 / 40, dropping in
            int f = _frame - _coinF0;
            if (At(f, 14)) _cIcon.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
            if (At(f, 24)) _cX.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
            for (int i = 0; i < 3; i++)
                if (At(f, 28 + 6 * i)) _cDigit[i].Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
            if (f >= Paced(40 + 50 + SmsHud.Settle))
                _coinF0 = -1;
        }
        if (_coinDownF0 >= 0)
        {
            // processDownCoin (3148): stepping down a row with the star HUD, each piece dipping 45 past its new home
            int f = _frame - _coinDownF0;
            if (At(f, 14)) _cIcon.Pop(_frame, Vector2.Zero, SmsHud.CoinMid, 50);
            if (At(f, 24)) _cX.Pop(_frame, Vector2.Zero, SmsHud.CoinMid, 50);
            for (int i = 0; i < 3; i++)
                if (At(f, 28 + 6 * i)) _cDigit[i].Pop(_frame, new Vector2(0f, 6f * i), SmsHud.CoinMid, 50);
            if (f >= Paced(40 + 50 + SmsHud.Settle))
                _coinDownF0 = -1;
        }
        if (_coinSlide.To == CoinHidden && _coinSlide.Done(_now))
            _coinVisible = false;
    }

    // ------------------------------------------------------------------------------------------------------------------ lives
    private void StartAppearMario()
    {
        if (_marioVisible && !_marioLeaving)
            return;
        _marioVisible = true; _marioLeaving = false;
        _marioF0 = _frame;
        _marioSlide.Set(_now, MarioHidden, 0f, 50);
        _mStrip.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
        _mIcon.Shown = false; _mText.Shown = false; _mX.Shown = false;
        foreach (var d in _mDigit) d.Shown = false;
    }

    private void StartDisappearMario()
    {
        if (!_marioVisible || _marioLeaving)
            return;
        _marioLeaving = true; _marioF0 = -1;
        _marioSlide.Retarget(_now, MarioHidden, 50);
    }

    private void StepMario()
    {
        if (!_marioVisible)
            return;
        if (_marioF0 >= 0)
        {
            // processAppearMario (3275): the icon at 14, the text and the x at 24, the digits at 24 and 30
            int f = _frame - _marioF0;
            if (At(f, 14)) _mIcon.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
            if (At(f, 24)) { _mText.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50); _mX.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50); }
            for (int i = 0; i < 2; i++)
                if (At(f, 24 + 6 * i)) _mDigit[i].Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
            if (f >= Paced(30 + 50 + SmsHud.Settle))
                _marioF0 = -1;
        }
        if (_marioLeaving && _marioSlide.Done(_now))
        {
            _marioVisible = false; _marioLeaving = false;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ the star HUD
    private void StartAppearStar()
    {
        if (_starAppearing || (_starVisible && _shineSlide.AtZero(_now)))
            return;
        _starLeaving = false;
        _starVisible = true; _starAppearing = true; _starF0 = _frame;
        _shineSlide.Set(_now, ShineHidden, 0f, 40);
        _sStrip.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
        _sIcon.Shown = false; _sX.Shown = false; foreach (var d in _sDigit) d.Shown = false;
        _blueSlide.Set(_now, BlueHidden, 0f, 40);
        _dStrip.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
        _dIcon.Shown = false; _dX.Shown = false; foreach (var d in _dDigit) d.Shown = false;
        // the coin counter steps down to its own row (unk108->updatePaneOffset(40, 0, 0), the strip through cCoinMid)
        if (!_coinVisible)
        {
            _coinVisible = true;
            _cIcon.Shown = true; _cX.Shown = true; foreach (var d in _cDigit) d.Shown = true;
            _cIcon.Still(); _cX.Still(); foreach (var d in _cDigit) d.Still();
        }
        _coinDownF0 = _frame; // processAppearCoin, if still running, keeps revealing the pieces on its own schedule
        _coinSlide.Retarget(_now, 0f, 40);
        _cStrip.Pop(_frame, Vector2.Zero, SmsHud.CoinMid, 50);
    }

    private void StartDisappearStar()
    {
        if (!_starVisible || _starLeaving)
            return;
        _starLeaving = true; _starAppearing = false; _starF0 = -1;
        _shineSlide.Retarget(_now, ShineGone, 40);
        _blueSlide.Retarget(_now, BlueHidden, 40);
        if (_coinVisible && _coinSlide.To != CoinHidden)
            _coinSlide.Retarget(_now, CoinHome, 40);
    }

    private void StepStar()
    {
        if (_starAppearing)
        {
            // processAppearStar (3055): the icons at 14, the x's at 24, the digits at 28 / 34 / 40 (the third only from 100)
            int f = _frame - _starF0;
            if (At(f, 14)) { _sIcon.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50); _dIcon.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50); }
            if (At(f, 24)) { _sX.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50); _dX.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50); }
            for (int i = 0; i < 3; i++)
                if (At(f, 28 + 6 * i)) { _sDigit[i].Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50); _dDigit[i].Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50); }
            if (f >= Paced(40 + 50 + SmsHud.Settle))
                _starAppearing = false;
        }
        // updateStarHudAutoHide (1061): out, settled, nothing on the clocks, no demo: away again, the lives with it -- unless Jaysonn's
        // IdleHold keeps them while he stands (the console's unk60 guard, never set in the game)
        if (_starVisible && !_starAppearing && !_starLeaving && _shineSlide.AtZero(_now) && !Demo && _blueTimer == 0 && _shineTimer == 0
            && !(SmsParams.Hud.IdleHold && Idle))
        {
            StartDisappearStar();
            StartDisappearMario();
        }
        if (_starLeaving && _shineSlide.Done(_now) && _blueSlide.Done(_now))
        {
            _starVisible = false; _starLeaving = false;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ red coins
    private void StartAppearRedCoin()
    {
        _redVisible = true; _redParked = false; _redF0 = _frame;
        _redSlide.Set(_now, RedBelow, 0f, 40);
        _rStrip.Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
        _rIcon.Shown = false; _rX.Shown = false; _rDigit.Shown = false; _rSlash.Shown = false; _rEight.Shown = false;
    }

    private void StepRed()
    {
        if (!_redVisible || _redF0 < 0)
            return;
        // processAppearRed (3472): the icon at 14, the x at 24, the count at 28, the slash at 32, the 8 at 34, all rising
        int f = _frame - _redF0;
        if (At(f, 14)) _rIcon.Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
        if (At(f, 24)) _rX.Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
        if (At(f, 28)) _rDigit.Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
        if (At(f, 32)) _rSlash.Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
        if (At(f, 34)) _rEight.Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
        if (f >= Paced(34 + 50 + SmsHud.Settle))
            _redF0 = -1;
    }

    // ------------------------------------------------------------------------------------------------------------------ the life meter
    private bool LifeOnScreen => _life == Life.Appear || _life == Life.Insert || _life == Life.Shown || _life == Life.Hold || _life == Life.Leave;

    private void StartLifeAppear()
    {
        // startAppearLife (2323): the colours, the lit pieces per the health, the pane +(-150, 180) from home; processAppearLife (3008)
        _life = Life.Appear; _lifeF0 = _frame;
        _lDisc.Shown = true; _lDisc.Still(); _lText.Shown = true; _lText.Still(); _lSun.Shown = true; _lSun.Still();
        ResetLifePieces();
    }

    private void StartLifeInsert()
    {
        // startInsertLife (2260): from above at home over 20 frames, the disc popping, "LIFE" at 14, the sun at 24 (processInsertLife 3030)
        _life = Life.Insert; _lifeF0 = _frame;
        _lDisc.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
        _lText.Shown = false; _lSun.Shown = false;
        ResetLifePieces();
    }

    private void StartLifeLeave()
    {
        _lifeLeaveFrom = LifeGroupOffset(_now);
        _life = Life.Leave; _lifeF0 = _frame;
        _lDisc.Still(); _lText.Still(); _lSun.Still(); _lDisc.Shown = true; _lText.Shown = true; _lSun.Shown = true;
    }

    private void ResetLifePieces()
    {
        // resetLife (2302): the lit pieces as the count says, every bounds back to its own
        for (int i = 0; i < 9; i++) { _lRay[i].Shown = i <= _lifeShown; _lRay[i].Inset = 0f; _lSocket[i].Inset = 0f; }
    }

    private Vector2 LifeGroupOffset(float now)
    {
        float f = now - _lifeF0;
        switch (_life)
        {
            case Life.Appear:
                // held at (-150, +180) for 100 frames, then 40 frames through (-10, 10) home
                f *= SmsHud.Anim;
                return f < LifeAppearHold ? new Vector2(-150f, 180f)
                    : SmsHud.Bezier(new Vector2(-150f, 180f), new Vector2(-10f, 10f), Vector2.Zero, (f - LifeAppearHold) / 40f);
            case Life.Insert:
                return SmsHud.Bezier(new Vector2(0f, LifeAbove), new Vector2(0f, LifeAbove * 0.5f), Vector2.Zero, f * SmsHud.Anim / 20f);
            case Life.Leave:
                return SmsHud.Bezier(_lifeLeaveFrom, new Vector2(0f, LifeGone * 0.5f), new Vector2(0f, LifeGone), f * SmsHud.Anim / 40f);
            default:
                return Vector2.Zero;
        }
    }

    private void StepLife()
    {
        int amount = Mathf.Clamp(AirMode ? Air : Health, 0, 8);
        // the states (updateLifeMeterState 408), health and air alike
        switch (_life)
        {
            case Life.Hidden:
                if (Demo)
                    _life = Life.DemoHidden;
                else if ((_lifeShown < 8 || AirMode) && amount != 0)
                    StartLifeAppear();
                break;
            case Life.Appear:
                if (Demo) StartLifeLeave();
                else if (_frame - _lifeF0 >= Paced(LifeAppearHold + 40)) { _life = Life.Shown; _lifeF0 = _frame; }
                break;
            case Life.Insert:
            {
                int f = _frame - _lifeF0;
                if (At(f, 14)) _lText.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
                if (At(f, 24)) _lSun.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
                if (Demo) StartLifeLeave();
                else if (f >= Paced(24 + 50 + SmsHud.Settle)) { _life = Life.Shown; _lifeF0 = _frame; }
                break;
            }
            case Life.Shown:
                if (Demo) StartLifeLeave();
                else if (!AirMode && _lifeShown == 8) { _life = Life.Hold; _lifeF0 = _frame; }
                break;
            case Life.Hold:
                if (Demo) StartLifeLeave();
                else if (_frame - _lifeF0 > LifeHold) StartLifeLeave();
                break;
            case Life.Leave:
                if (_frame - _lifeF0 >= Paced(40))
                    _life = Demo ? Life.DemoHidden : Life.Hidden;
                break;
            case Life.DemoHidden:
                break;
        }

        // the count, one a frame, the lost piece's bounds back to its own (updateLifeMeterState 556)
        if (amount != _lifeShown && _life != Life.DemoHidden)
        {
            if (_lifeShown < amount)
            {
                _lifeShown++;
                _lRay[_lifeShown].Shown = true;
            }
            else if (_lifeShown > 0)
            {
                _lRay[_lifeShown].Shown = false; _lRay[_lifeShown].Inset = 0f; _lSocket[_lifeShown].Inset = 0f;
                _lifeShown--;
            }
        }
        _lifeAirShown = AirMode;

        // updateLifeMeterBlink (1085): the lit pieces beat while the meter is on screen
        if (LifeOnScreen)
        {
            float rate = _lifeShown == 3 ? 80f : _lifeShown == 2 ? 60f : _lifeShown == 1 ? 35f : 100f;
            if (_beat < rate * 2f)
            {
                float diff = MathF.Abs(_beat - rate);
                int delta = (int)(0.5f + diff / rate);
                float inset = delta * (_lifeShown > 3 ? 3f : 5f);
                for (int i = 0; i < 9; i++)
                    if (_lRay[i].Shown) { _lRay[i].Inset = inset; _lSocket[i].Inset = inset; }
                _beat++;
            }
            else
                _beat = 0;
        }
        else
            _beat = 0;
    }

    // ------------------------------------------------------------------------------------------------------------------ the draw
    private enum Anchor { TopLeft, TopCenter, TopRight, BottomLeft }

    /// <summary>A group's canvas transform: its anchor on the window, the console's scale.</summary>
    private Transform2D Group(Vector2 pos, Anchor anchor)
    {
        float x = anchor == Anchor.TopRight ? SmsHud.RightX(pos.X, _scale, _viewport)
            : anchor == Anchor.TopCenter ? SmsHud.CenterX(pos.X, _scale, _viewport) : SmsHud.LeftX(pos.X, _scale);
        float y = anchor == Anchor.BottomLeft ? SmsHud.BottomY(pos.Y, _scale, _viewport) : SmsHud.TopY(pos.Y, _scale);
        return new Transform2D(0f, new Vector2(_scale, _scale), 0f, new Vector2(x, y));
    }

    public override void _Draw()
    {
        if (!Ready)
            return;
        // the screen's pane order: c_0, d_0, s_0, l_0, (w_0), m_0, r_0
        if (_coinVisible)
        {
            Transform2D t = Group(CoinPos, Anchor.TopLeft);
            Vector2 g = new Vector2(0f, _coinSlide.At(_now));
            _cStrip.Draw(this, _now, g, t); _cIcon.Draw(this, _now, g, t); _cX.Draw(this, _now, g, t);
            foreach (var d in _cDigit) d.Draw(this, _now, g, t);
        }
        if (_starVisible)
        {
            Transform2D t = Group(BluePos, Anchor.TopLeft);
            Vector2 g = new Vector2(0f, _blueSlide.At(_now));
            _dStrip.Draw(this, _now, g, t); _dIcon.Draw(this, _now, g, t); _dX.Draw(this, _now, g, t);
            foreach (var d in _dDigit) d.Draw(this, _now, g, t);
            t = Group(ShinePos, Anchor.TopLeft);
            g = new Vector2(0f, _shineSlide.At(_now));
            _sStrip.Draw(this, _now, g, t); _sIcon.Draw(this, _now, g, t); _sX.Draw(this, _now, g, t);
            foreach (var d in _sDigit) d.Draw(this, _now, g, t);
        }
        if (LifeOnScreen)
        {
            Transform2D t = Group(LifePos, Anchor.TopRight);
            Vector2 g = LifeGroupOffset(_now);
            bool air = _lifeAirShown, low = _lifeShown < 4;
            _lDisc.Tint = air ? DiscAir : DiscHealth;
            _lDisc.Draw(this, _now, g, t);
            if (_lSun.Shown)
            {
                Vector2 gs = g + _lSun.Offset(_now);
                for (int i = 0; i < 9; i++)
                {
                    _lSocket[i].Draw(this, _now, gs, t);
                    if (!_lRay[i].Shown)
                        continue;
                    _lRay[i].Tex = air ? (i == 0 && low ? _lifeAirLow[i] : _lifeAir[i]) : _lifeOn[i];
                    _lRay[i].Tint = !air && low && i == 0 ? Dim : Colors.White;
                    _lRay[i].Draw(this, _now, gs, t);
                }
            }
            _lText.Tex = air ? (low ? _lifeTextAirLow : _lifeTextAir) : _lifeText;
            _lText.Tint = !air && low ? Dim : Colors.White;
            _lText.Draw(this, _now, g, t);
        }
        if (_marioVisible)
        {
            Transform2D t = Group(MarioPos, Anchor.TopCenter); // m_0's strip (223..421) is centred on the frame's 320
            Vector2 g = new Vector2(0f, _marioSlide.At(_now));
            _mStrip.Draw(this, _now, g, t); _mIcon.Draw(this, _now, g, t); _mText.Draw(this, _now, g, t); _mX.Draw(this, _now, g, t);
            foreach (var d in _mDigit) d.Draw(this, _now, g, t);
        }
        if (_redVisible)
        {
            Transform2D t = Group(RedPos, Anchor.BottomLeft);
            Vector2 g = new Vector2(0f, _redSlide.At(_now));
            _rStrip.Draw(this, _now, g, t); _rIcon.Draw(this, _now, g, t); _rX.Draw(this, _now, g, t);
            _rDigit.Draw(this, _now, g, t); _rSlash.Draw(this, _now, g, t); _rEight.Draw(this, _now, g, t);
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
