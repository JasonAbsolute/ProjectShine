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
///       THE ART IS JAYSONN'S (pass 30: "my legacy one looks way better then the one you made so either do another pass on it or combine
///       mine with the new hud"): the group carries his Font/LifeMeter/LifeMeter.tscn (the Meter) in place of the disc's panes -- its
///       PaintBlobBG is the disc, LifeText the text, its Gray and Orange layers the sun; his script keeps the segments (SetHealth), the
///       air recolour (SetAirTint) and its own scale pulse (the beat); the console keeps the game's states, motions and timing above.
///   r_0 (13, 419) the red coins: coin_back tinted FF28FF, redcoin_icon at (19, -16), x at (41, -18), the count at (70, -38), balloon_slash
///       50 x 60 at (87, -41), the 8 (coin_number_8 28 x 28) at (113, -16); it rises from below when the first red coin is taken
///       (startAppearRedCoin 2553) and drops off the bottom for a camera demo (startDownLeftBot 2366); 73 px higher while the timer is out.
///   t_0 (13, 419) the timer: "TIME" time_text 68 x 28 at (5, -37), coin_back 270 x 22 tinted FF28FF, and two layouts -- t_1 at (4, -11)
///       mm:ss:cc (digits at (0, 0) (27, -5) (71, -17) (98, -22) (142, -34) (169, -39), coin_number_colon 18 x 36 at (60, -16) (131, -33))
///       for a count-up or a countdown from 60 s or more, t_2 at (31, -16) ss:cc (digits at (0, 0) (27, -5) (71, -17) (98, -22), the colon
///       at (60, -16)) for a shorter countdown (startAppearTimer 2451). It rises from below like the red coins (startInsertTimer 2483:
///       the strip, "TIME" and the digits at 24 + 6i, the colons at 32 / 44; processAppearTimer 3513), shows the stopwatch every frame
///       (setTimer 2920: centiseconds; the short layout's digits and colon turn FF82FF under 10 s), and leaves off the bottom
///       (startDisappearTimer 2444: 525 - y).
/// The pops are TBoundPane quadratics (SmsHud.Pop): the counters drop from (0, -100) through (0, 30) over 50 frames and settle over 30
/// (cDownTop / cDownMid), the red coins rise from (0, 50) through (0, -30), the coin counter's row step goes through (0, 45); the
/// groups slide linearly (TExPane): the coins 20 frames, the lives 50, the star HUD 40 (the shine group leading the blue one by
/// nothing). The pieces of a counter come in staggered: the strip at 0, the icon at 14, the x at 24, the digits at 28 / 34 / 40
/// (processAppearCoin 3236, processAppearStar 3055, processAppearMario 3275: icon 14, text and x 24, digits 24 / 30). A camera demo
/// (startCameraDemo 2056: the camera locked, a talk, the shine get, a death) takes the lives, the coins and the star HUD away (the
/// shine get brings the star HUD in instead) and endCameraDemo (2125) brings the coins back.
/// Frames are the game's 30 a second, counted fractionally so the motion runs at the display's rate; the motions (not the waits) go at
/// the scene root's AnimSpeed times the game's pace. Where the game parks a hidden strip 11 px into the frame (the CRT's overscan hid
/// it: initHiddenPaneAbove) this parks it fully above the top edge. The screen is the game's 640 x 448 frame (SmsHud): the counters
/// keep their margin to the left edge, the lives (centred on the frame) to the centre, the life meter to the right, the red coins to
/// the bottom, moved by the root's shifts and scaled by SmsHud.Scale; with its IdleHold the star HUD and the lives stay while Mario
/// stands still instead of the game's peek.
/// The screen is the scene assets/Sms/Console/SmsHud.tscn: this Control with a SmsHudGroup per group (Coins, BlueCoins, Shines, LifeMeter,
/// Lives, RedCoins, Timer -- the *Group exports below), every pane a TextureRect child laid out as standard_1.blo has it -- a counter's
/// Strip, Icon, X and Digit0..2 (the lives Digit0..1 and a Text), the red coins' Digit, Slash and Eight, the timer's Text, Strip,
/// Long0..5 with LongColon0..1 and Short0..3 with ShortColon, the life meter's Meter (his LifeMeter.tscn: PaintBlobBG, LifeText,
/// GrayLayer, OrangeLayer). The code reads the layout from the nodes and moves them; the ten digit pictures are the Digits export.
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
    public bool TimerRunning;       // his ShineTimer runs (the game's startMoveTimer)
    public bool TimerReached;       // it stopped at its target (his TimerReachedTarget: the game's startDisappearTimer)
    public bool TimerCountDown = true;
    public float TimerSeconds, TimerStartSeconds;
    public bool Ready { get; private set; }

    private float _now;             // the console's frames, fractional
    private int _frame;             // the last whole frame stepped

    // ---- the scene: the groups and the digit pictures ----
    [Export] public SmsHudGroup CoinGroup;
    [Export] public SmsHudGroup BlueCoinGroup;
    [Export] public SmsHudGroup ShineGroup;
    [Export] public SmsHudGroup LifeMeterGroup;
    [Export] public SmsHudGroup LivesGroup;
    [Export] public SmsHudGroup RedCoinGroup;
    [Export] public SmsHudGroup TimerGroup;
    /// <summary>updateLifeMeterBlink's heartbeat SE (0x4800), once a beat while the shown count is 1..3; his to fill.</summary>
    [Export] public AudioStreamPlayer LifeBeep;
    /// <summary>The meter's beat at the game's cadence: a 2 x rate period, rate 100 frames at 4 or more, 80 / 60 / 35 at 3 / 2 / 1
    /// (updateLifeMeterBlink) -- written into his LifeMeter.ToggleInterval. Off: his own ToggleInterval stays.</summary>
    [Export] public bool HeartbeatRates = true;
    /// <summary>coin_number_0 .. 9: what the counters' digit panes show.</summary>
    [Export] public Texture2D[] Digits;
    private Texture2D[] _digit;

    private static readonly Color StripGreen = new Color(0f, 1f, 0f, 180f / 255f);
    private static readonly Color StripMagenta = new Color(1f, 0x28 / 255f, 1f, 180f / 255f);
    private static readonly Color TimerLow = new Color(1f, 0x82 / 255f, 1f);        // t_n7's white: the short countdown under 10 s

    // ---- the groups' rows on the frame (the scene puts them there; these are the .blo's, for the hidden spots below) ----
    private const float CoinRow = 152f, BlueRow = 113f, ShineRow = 74f, LifeRow = 50f, MarioRow = 74f, RedRow = 419f;
    private const float CoinHome = ShineRow - CoinRow;          // unk26A: the coin counter's row while the star HUD is away
    private const float CoinHidden = -(CoinRow + 10f + 1f) - 22f;  // startDisappearCoin: -(y2 + 1) less the strip
    private const float MarioHidden = -(MarioRow + 10f + 1f) - 22f;
    private const float ShineHidden = -(ShineRow + 10f + 1f) - 22f, ShineGone = -(ShineRow + 10f + 1f) + CoinHome; // startDisappearStar's target
    private const float BlueHidden = -(BlueRow + 10f + 1f) - 22f;
    private const float RedBelow = 465f - RedRow, RedGone = 525f - RedRow, RedOverTimer = -73f;
    private const float LifeAbove = -(LifeRow + 10f + 1f);   // startInsertLife: -(unk4.y2 + 1)
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
    // the timer
    private readonly SmsHudSlide _timerSlide = new SmsHudSlide(RedBelow);
    private bool _timerVisible, _timerParked, _timerLong, _timerWasRunning; private int _timerF0 = -1;
    private SmsHudPane _tStrip, _tText; private readonly SmsHudPane[] _tLong = new SmsHudPane[6], _tShort = new SmsHudPane[4], _tColon = new SmsHudPane[3];
    // the life meter
    private enum Life { Hidden, Appear, Insert, Shown, Hold, Leave, DemoHidden }
    private Life _life = Life.Hidden; private int _lifeF0 = -1; private Vector2 _lifeLeaveFrom;
    private SmsHudPane _lDisc, _lText, _lSun, _lSunB;  // his PaintBlobBG, LifeText, GrayLayer and OrangeLayer (the sun is two layers)
    private LifeMeter _lMeter;                          // his Font/LifeMeter/LifeMeter.tscn: the segments, the air recolour, the pulse
    private int _lifeShown = 8; private bool _lifeAirShown; private bool _lifeAirApplied, _lifeAirLowApplied, _lifeLowApplied;
    private int _lifeBeat;                              // unk86: the beat's frame, 0 .. 2 x rate
    // the demo
    private bool _inDemo, _started; private int _demoEndCount;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Ready = CoinGroup != null && BlueCoinGroup != null && ShineGroup != null && LifeMeterGroup != null && LivesGroup != null && RedCoinGroup != null && TimerGroup != null
            && Digits != null && Digits.Length == 10 && System.Array.TrueForAll(Digits, d => d != null);
        if (!Ready)
        {
            GD.PushWarning("[SMS hud] SmsHud.tscn: the StandardHud's seven group exports and its ten Digits are not all set -- the counters stay off");
            return;
        }
        _digit = Digits;
        try
        {
            BuildPanes();
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"[SMS hud] SmsHud.tscn: a pane is missing from its group ({e.Message}) -- the counters stay off");
            Ready = false;
            return;
        }
        Layout();
        GetViewport().SizeChanged += Layout;
    }

    private static TextureRect Pane(Control group, string name) => group.GetNode<TextureRect>(name);

    private void BuildPanes()
    {
        // the .blo's layout -- the strips, the x's, the digits and "MARIO" turned 15 degrees about their top-left corners, the Mario
        // head 5 the other way, the icons upright -- is the scene's; each pane keeps its node's rect, picture, tint and rotation
        _cStrip = new SmsHudPane(Pane(CoinGroup, "Strip")); _cIcon = new SmsHudPane(Pane(CoinGroup, "Icon")); _cX = new SmsHudPane(Pane(CoinGroup, "X"));
        _dStrip = new SmsHudPane(Pane(BlueCoinGroup, "Strip")); _dIcon = new SmsHudPane(Pane(BlueCoinGroup, "Icon")); _dX = new SmsHudPane(Pane(BlueCoinGroup, "X"));
        _sStrip = new SmsHudPane(Pane(ShineGroup, "Strip")); _sIcon = new SmsHudPane(Pane(ShineGroup, "Icon")); _sX = new SmsHudPane(Pane(ShineGroup, "X"));
        for (int i = 0; i < 3; i++)
        {
            _cDigit[i] = new SmsHudPane(Pane(CoinGroup, $"Digit{i}"));
            _dDigit[i] = new SmsHudPane(Pane(BlueCoinGroup, $"Digit{i}"));
            _sDigit[i] = new SmsHudPane(Pane(ShineGroup, $"Digit{i}"));
        }
        _mStrip = new SmsHudPane(Pane(LivesGroup, "Strip")); _mIcon = new SmsHudPane(Pane(LivesGroup, "Icon")); _mText = new SmsHudPane(Pane(LivesGroup, "Text")); _mX = new SmsHudPane(Pane(LivesGroup, "X"));
        _mDigit[0] = new SmsHudPane(Pane(LivesGroup, "Digit0")); _mDigit[1] = new SmsHudPane(Pane(LivesGroup, "Digit1"));
        _rStrip = new SmsHudPane(Pane(RedCoinGroup, "Strip")); _rIcon = new SmsHudPane(Pane(RedCoinGroup, "Icon")); _rX = new SmsHudPane(Pane(RedCoinGroup, "X"));
        _rDigit = new SmsHudPane(Pane(RedCoinGroup, "Digit")); _rSlash = new SmsHudPane(Pane(RedCoinGroup, "Slash")); _rEight = new SmsHudPane(Pane(RedCoinGroup, "Eight"));
        // the timer: t_ba, t_tx (turned 13), t_1's six digits and two colons (turned 10), t_2's four and one
        _tStrip = new SmsHudPane(Pane(TimerGroup, "Strip")); _tText = new SmsHudPane(Pane(TimerGroup, "Text"));
        for (int i = 0; i < 6; i++) _tLong[i] = new SmsHudPane(Pane(TimerGroup, $"Long{i}"));
        for (int i = 0; i < 4; i++) _tShort[i] = new SmsHudPane(Pane(TimerGroup, $"Short{i}"));
        _tColon[0] = new SmsHudPane(Pane(TimerGroup, "LongColon0")); _tColon[1] = new SmsHudPane(Pane(TimerGroup, "LongColon1")); _tColon[2] = new SmsHudPane(Pane(TimerGroup, "ShortColon"));
        // the life meter: Jaysonn's Meter -- its PaintBlobBG is l_ba (the disc), LifeText l_tx, its two layers lm_0 (the sun the rays
        // ride); carriers only: his script owns their pictures, tints and the segments' pulse
        _lMeter = LifeMeterGroup.GetNodeOrNull<LifeMeter>("Meter") ?? throw new System.Exception("LifeMeter/Meter (Font/LifeMeter/LifeMeter.tscn) is not in the group");
        _lDisc = new SmsHudPane(_lMeter.GetNode<Control>("PaintBlobBG")); _lText = new SmsHudPane(_lMeter.GetNode<Control>("LifeText"));
        _lSun = new SmsHudPane(_lMeter.GetNode<Control>("GrayLayer")) { Shown = true };
        _lSunB = new SmsHudPane(_lMeter.GetNode<Control>("OrangeLayer")) { Shown = true };
        _lMeter.Visible = true;                       // his _Ready hides it; the group's visibility is the meter's from here on
        _lifeShown = Mathf.Clamp(Health, 0, 8);
        _lMeter.SetHealth(_lifeShown);
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
        Apply();
        // the telop (updateTelopState): news only while he stands in WAIT with no camera demo and no timer out; the count holds in a pause
        var telop = SmsHudLayer.Current?.Telop;
        if (telop != null)
        {
            telop.MarioWaiting = Idle && !Demo && !_timerVisible;
            telop.Paused = GetTree().Paused;
        }
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
        StepTimer();
    }

    // ------------------------------------------------------------------------------------------------------------------ the demo
    private void StartCameraDemo()
    {
        _inDemo = true; _demoEndCount = 0;
        if (Dead)
        {
            // startCameraDemo with no health: everything off at once
            _coinVisible = false; _marioVisible = false; _marioLeaving = false; _starVisible = false; _starAppearing = false; _starLeaving = false;
            _redVisible = false; _redParked = false; _timerVisible = false; _timerParked = false; _life = Life.Hidden;
            _coinSlide.Set(_now, CoinHidden, CoinHidden, 0); _marioSlide.Set(_now, MarioHidden, MarioHidden, 0);
            _shineSlide.Set(_now, ShineHidden, ShineHidden, 0); _blueSlide.Set(_now, BlueHidden, BlueHidden, 0); _redSlide.Set(_now, RedBelow, RedBelow, 0);
            _timerSlide.Set(_now, RedBelow, RedBelow, 0);
            return;
        }
        if (_life == Life.Appear || _life == Life.Insert || _life == Life.Shown || _life == Life.Hold)
            StartLifeLeave(); // startLifeMeterDisappear(this, 120): out now, state 10 until the demo ends
        else if (_life == Life.Hidden)
            _life = Life.DemoHidden;
        if (_redVisible) { _redSlide.Retarget(_now, RedGone, 20); _redParked = true; } // startDownLeftBot
        if (_timerVisible) { _timerSlide.Retarget(_now, RedGone, 20); _timerParked = true; _timerF0 = -1; }
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
        // unk51C: the timer comes back after the demo while it still runs (parked by the demo, or gone with a death)
        _timerParked = false; _timerVisible = false;
        if (TimerRunning)
            StartInsertTimer();
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
            && !(SmsHud.IdleHold && Idle))
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
        _redSlide.Set(_now, RedBelow, _timerVisible ? RedOverTimer : 0f, 40); // 73 px up while the timer is out
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

    // ------------------------------------------------------------------------------------------------------------------ the timer
    private void StartInsertTimer()
    {
        // startAppearTimer (2451): the long layout for a count-up or a countdown from 60 s, the short one below; startInsertTimer (2483)
        _timerLong = !TimerCountDown || TimerStartSeconds >= 60f;
        _timerVisible = true; _timerParked = false; _timerF0 = _frame;
        _timerSlide.Set(_now, RedBelow, 0f, 40);
        _tStrip.Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
        _tText.Shown = false;
        foreach (var d in _tLong) { d.Shown = false; d.Tint = Colors.White; }
        foreach (var d in _tShort) { d.Shown = false; d.Tint = Colors.White; }
        foreach (var c in _tColon) { c.Shown = false; c.Tint = Colors.White; }
        SetTimerDigits();
        if (_redVisible && !_redParked)
            _redSlide.Retarget(_now, RedOverTimer, 40); // the red coins make room above it
    }

    private void StartDisappearTimer()
    {
        _timerSlide.Retarget(_now, RedGone, 40); _timerF0 = -1;
        if (_redVisible && !_redParked)
            _redSlide.Retarget(_now, 0f, 40);
    }

    /// <summary>setTimer (2920): the stopwatch in centiseconds, capped at 99:59.99; the short layout's pieces go FF82FF under 10 s.</summary>
    private void SetTimerDigits()
    {
        int centis = Mathf.Clamp((int)MathF.Floor(TimerSeconds * 100f + 1e-4f), 0, 599999); // the game's (u32)(ms * 0.1)
        int minutes = centis / 6000, rest = centis - minutes * 6000, seconds = rest / 100, cc = rest % 100;
        if (_timerLong)
        {
            _tLong[0].Tex = _digit[minutes / 10]; _tLong[1].Tex = _digit[minutes % 10];
            _tLong[2].Tex = _digit[seconds / 10]; _tLong[3].Tex = _digit[seconds % 10];
            _tLong[4].Tex = _digit[cc / 10]; _tLong[5].Tex = _digit[cc % 10];
        }
        else
        {
            _tShort[0].Tex = _digit[seconds / 10]; _tShort[1].Tex = _digit[seconds % 10];
            _tShort[2].Tex = _digit[cc / 10]; _tShort[3].Tex = _digit[cc % 10];
            Color c = centis < 1000 ? TimerLow : Colors.White;
            foreach (var d in _tShort) d.Tint = c;
            _tColon[2].Tint = c;
        }
    }

    private void StepTimer()
    {
        // his ShineTimer: a start brings the timer in (startAppearTimer + startMoveTimer), the target reached takes it away
        // (startDisappearTimer); stopped early it stays frozen, as the game's does once stopMoveTimer is called
        if (TimerRunning && !_timerWasRunning && !_timerVisible && !Demo)
            StartInsertTimer();
        if (!TimerRunning && _timerWasRunning && _timerVisible && TimerReached)
            StartDisappearTimer();
        _timerWasRunning = TimerRunning;
        if (!_timerVisible)
            return;
        if (TimerRunning)
            SetTimerDigits();
        if (_timerF0 >= 0)
        {
            // processAppearTimer (3513): "TIME" at 24, the digits at 24 + 6i, the colons at 32 / 44 (the short layout's at 32), all rising
            int f = _frame - _timerF0;
            if (At(f, 24)) _tText.Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
            if (_timerLong)
            {
                for (int i = 0; i < 6; i++)
                    if (At(f, 24 + 6 * i)) _tLong[i].Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
                for (int i = 0; i < 2; i++)
                    if (At(f, 32 + 12 * i)) _tColon[i].Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
                if (f >= Paced(54 + 50 + SmsHud.Settle)) _timerF0 = -1;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    if (At(f, 24 + 6 * i)) _tShort[i].Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
                if (At(f, 32)) _tColon[2].Pop(_frame, SmsHud.UpTop, SmsHud.UpMid, 50);
                if (f >= Paced(42 + 50 + SmsHud.Settle)) _timerF0 = -1;
            }
        }
        if (!_timerParked && _timerSlide.To == RedGone && _timerSlide.Done(_now))
            _timerVisible = false;
    }

    // ------------------------------------------------------------------------------------------------------------------ the life meter
    private bool LifeOnScreen => _life == Life.Appear || _life == Life.Insert || _life == Life.Shown || _life == Life.Hold || _life == Life.Leave;

    private void StartLifeAppear()
    {
        // startAppearLife (2323): the colours, the lit pieces per the health, the pane +(-150, 180) from home; processAppearLife (3008)
        _life = Life.Appear; _lifeF0 = _frame;
        _lDisc.Shown = true; _lDisc.Still(); _lText.Shown = true; _lText.Still(); SunShown(true); SunStill();
        ResetLifePieces();
    }

    private void StartLifeInsert()
    {
        // startInsertLife (2260): from above at home over 20 frames, the disc popping, "LIFE" at 14, the sun at 24 (processInsertLife 3030)
        _life = Life.Insert; _lifeF0 = _frame;
        _lDisc.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
        _lText.Shown = false; SunShown(false);
        ResetLifePieces();
    }

    private void StartLifeLeave()
    {
        _lifeLeaveFrom = LifeGroupOffset(_now);
        _life = Life.Leave; _lifeF0 = _frame;
        _lDisc.Still(); _lText.Still(); SunStill(); _lDisc.Shown = true; _lText.Shown = true; SunShown(true);
    }

    // his sun is two layers (the grey sockets, the orange rays): both ride lm_0's motions
    private void SunShown(bool on) { _lSun.Shown = on; _lSunB.Shown = on; }
    private void SunStill() { _lSun.Still(); _lSunB.Still(); }
    private void SunPop(float at) { _lSun.Pop(at, SmsHud.DownTop, SmsHud.DownMid, 50); _lSunB.Pop(at, SmsHud.DownTop, SmsHud.DownMid, 50); }

    private void ResetLifePieces()
    {
        // resetLife (2302): the lit pieces as the count says, every bounds back to its own -- his SetHealth lights the segments
        _lMeter.SetHealth(_lifeShown);
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
                if (At(f, 24)) SunPop(_frame);
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

        // the count, one a frame (updateLifeMeterState 556): his SetHealth lights the segment (and pulses it)
        if (amount != _lifeShown && _life != Life.DemoHidden)
        {
            if (_lifeShown < amount)
                _lifeShown++;
            else if (_lifeShown > 0)
                _lifeShown--;
            _lMeter.SetHealth(_lifeShown);
        }
        _lifeAirShown = AirMode;
        // updateLifeMeterBlink (1086): while the meter is on screen the lit pieces beat -- 3 px at 4 or more, 5 at 1..3 -- with a period of
        // 2 x rate frames (rate 100 / 80 / 60 / 35 for 4+ / 3 / 2 / 1) and the heartbeat SE 0x4800 at 1.5 x rate per cycle while the count
        // is 1..3 (not in a demo). His meter's own scale pulse (ScaleMultiplier) is the beat's look -- a deviation logged in pass 30 -- and
        // runs at the game's cadence through its ToggleInterval (HeartbeatRates); the beep is the console's (LifeBeep).
        if (LifeOnScreen)
        {
            float rate = _lifeShown == 3 ? 80f : _lifeShown == 2 ? 60f : _lifeShown == 1 ? 35f : 100f;
            if (HeartbeatRates && _lMeter != null)
                _lMeter.ToggleInterval = rate / SmsHud.Fps;
            if (_lifeBeat < (int)(rate * 2f))
            {
                if (_lifeShown >= 1 && _lifeShown <= 3 && _lifeBeat == (int)(1.5f * rate) && !Demo && LifeBeep != null && LifeBeep.Stream != null)
                    LifeBeep.Play();
                _lifeBeat++;
            }
            else
                _lifeBeat = 0;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------ the apply
    /// <summary>The frame onto the scene: each group placed on the window (its anchor, the console's scale, its slide), each pane onto
    /// its node. A group off screen is simply not shown.</summary>
    private void Apply()
    {
        if (!Ready)
            return;
        float n = _now;
        CoinGroup.Visible = _coinVisible;
        if (_coinVisible)
        {
            CoinGroup.Place(new Vector2(0f, _coinSlide.At(n)), _scale, _viewport);
            _cStrip.Apply(n, Vector2.Zero); _cIcon.Apply(n, Vector2.Zero); _cX.Apply(n, Vector2.Zero);
            foreach (var d in _cDigit) d.Apply(n, Vector2.Zero);
        }
        BlueCoinGroup.Visible = _starVisible; ShineGroup.Visible = _starVisible;
        if (_starVisible)
        {
            BlueCoinGroup.Place(new Vector2(0f, _blueSlide.At(n)), _scale, _viewport);
            _dStrip.Apply(n, Vector2.Zero); _dIcon.Apply(n, Vector2.Zero); _dX.Apply(n, Vector2.Zero);
            foreach (var d in _dDigit) d.Apply(n, Vector2.Zero);
            ShineGroup.Place(new Vector2(0f, _shineSlide.At(n)), _scale, _viewport);
            _sStrip.Apply(n, Vector2.Zero); _sIcon.Apply(n, Vector2.Zero); _sX.Apply(n, Vector2.Zero);
            foreach (var d in _sDigit) d.Apply(n, Vector2.Zero);
        }
        LifeMeterGroup.Visible = LifeOnScreen;
        if (LifeOnScreen)
        {
            LifeMeterGroup.Place(LifeGroupOffset(n), _scale, _viewport);
            _lDisc.Apply(n, Vector2.Zero);
            _lSun.Apply(n, Vector2.Zero); _lSunB.Apply(n, Vector2.Zero);
            _lText.Apply(n, Vector2.Zero);
            // updateLifeMeterColors (366): under water the meter is blue (the lowest darker under 4) -- his SetAirTint's remap; applied
            // on a change only (it swaps materials)
            bool air = _lifeAirShown, low = _lifeShown < 4;
            if (air != _lifeAirApplied || (air && low != _lifeAirLowApplied))
            {
                _lMeter.SetAirTint(air, _lifeShown);
                _lifeAirApplied = air; _lifeAirLowApplied = low;
                _lifeLowApplied = false; // the health-mode grey is re-applied below after a mode change
            }
            // updateLifeMeterColors (health mode): under 4 the centre and the text are half-bright (white 0x7F7F7F), the segments not
            bool lowHealth = !air && low;
            if (lowHealth != _lifeLowApplied)
            {
                _lMeter.SetLow(lowHealth);
                _lifeLowApplied = lowHealth;
            }
        }
        LivesGroup.Visible = _marioVisible;
        if (_marioVisible)
        {
            LivesGroup.Place(new Vector2(0f, _marioSlide.At(n)), _scale, _viewport); // m_0's strip (223..421) is centred on the frame's 320
            _mStrip.Apply(n, Vector2.Zero); _mIcon.Apply(n, Vector2.Zero); _mText.Apply(n, Vector2.Zero); _mX.Apply(n, Vector2.Zero);
            foreach (var d in _mDigit) d.Apply(n, Vector2.Zero);
        }
        RedCoinGroup.Visible = _redVisible;
        if (_redVisible)
        {
            RedCoinGroup.Place(new Vector2(0f, _redSlide.At(n)), _scale, _viewport);
            _rStrip.Apply(n, Vector2.Zero); _rIcon.Apply(n, Vector2.Zero); _rX.Apply(n, Vector2.Zero);
            _rDigit.Apply(n, Vector2.Zero); _rSlash.Apply(n, Vector2.Zero); _rEight.Apply(n, Vector2.Zero);
        }
        TimerGroup.Visible = _timerVisible;
        if (_timerVisible)
        {
            TimerGroup.Place(new Vector2(0f, _timerSlide.At(n)), _scale, _viewport);
            _tText.Apply(n, Vector2.Zero); _tStrip.Apply(n, Vector2.Zero);
            // the layout of the moment: t_1 (six digits, two colons) or t_2 (four and one); the other's nodes stay hidden
            foreach (var d in _tLong) { d.Apply(n, Vector2.Zero); if (!_timerLong) d.Node.Visible = false; }
            foreach (var d in _tShort) { d.Apply(n, Vector2.Zero); if (_timerLong) d.Node.Visible = false; }
            for (int i = 0; i < 3; i++) { _tColon[i].Apply(n, Vector2.Zero); if ((i < 2) != _timerLong) _tColon[i].Node.Visible = false; }
        }
    }
}
