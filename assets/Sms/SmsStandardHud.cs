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
///       picture (_1) over its grey socket (_2), "LIFE" life_text 60 x 30 at (7, -18). The game runs it as a state machine
///       (updateLifeMeterState 397, unk18; the frames are the console's 120 Hz ticks): 0 hidden -- a hit brings it in at (-150, +180)
///       from home (startAppearLife 2323: the middle-right of the screen), held 100 frames, then a 40-frame curve through (-10, 10) to
///       the corner (processAppearLife 3008; state 1) -- 2 shown; back at 8 it holds 120 frames and slides up and away
///       (startLifeMeterDisappear 339; state 3). WATER: once Mario's head has been under for 240 frames (isUnderWater: IN_ANY_WATER and
///       the head below the surface less CanBreathDepth; unk268) a shown health meter holds its 120 frames and slides away too, and the
///       hidden meter comes back as the AIR meter (startAppearLife(1): blue, the pieces lit per (int)mAir) at the same (-150, +180),
///       where it SITS while his air is still 8 and his head under (state 4 holds processAppearLife at frame 0), then the 100-frame hold
///       and the curve home; 5 shown as air, the pieces following mAir. Surfaced, the air refills (0.03 a frame) and once the shown
///       count is 8 the air meter holds 120 and slides up (state 6); then the HEALTH meter drops in from above if mHealth is not 8
///       (startInsertLife(0) 2260: the disc first, "LIFE" at 14, the sun at 24, popping; state 7 -> 2), else nothing. A camera demo
///       slides whatever shows out at once (state 10) and, when it ends, drops the health meter in if mHealth is not 8, the air meter if
///       his head is under water, else stays hidden. The three helmet stages (SMS_isDivingMap: the air meter inserted at the start,
///       standing in state 9) have no counterpart here. The segments follow the count one a frame (SE 0x4801 up / 0x4823 down), the
///       sun and the text dim to 7F below 4 (updateLifeMeterColors 366), and the lit pieces beat (updateLifeMeterBlink 1085): 3 px in
///       from every side for half of a 200-frame cycle, 160 / 120 / 70 frames and 5 px at 3 / 2 / 1. As the air meter the disc is the
///       water gauge's blue (0000FF, alpha 78) and the lit pieces go through black 003CFF00 / white 00FFFFFF (the sun and text 0010FFFF
///       below 4). NOTE the decomp's case labels for states 4..8 are wrong (the compiler laid the blocks out in source order 0 1 2 3 7 8
///       4 5 6 10; its updateLifeMeterState names them 4 5 6 7 8 in that order): the numbering above is the jump table's, checked
///       against the disassembly and the only one under which the machine is consistent (the air states 4 5 6 8 9 read mAir).
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
    public int Air = 8;             // (int)mAir, 0..8: what the meter shows in its air states
    public bool UnderWater;         // TMario::isUnderWater(): the console counts his frames under (unk268) and switches to air after 240
    public bool DebugPrint;         // the life meter's state changes to the output (Mario's SmsDebugPrint)
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
    /// <summary>The count's step SEs (updateLifeMeterState 556): 0x4801 as a piece lights, 0x4823 as one goes out; his to fill.</summary>
    [Export] public AudioStreamPlayer LifeUp, LifeDown;
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
    private const int LifeLeaveHold = 120;              // states 3 / 6: 0x78 frames before the meter slides away
    private const int LifeAppearHold = 100;             // processAppearLife: the frames in the middle of the screen before the curve home
    private const int LifeAirTimeout = 240;             // unk268: 0xF0 frames of isUnderWater() before the meter turns to air
    private const int LifeAppearFrames = 40, LifeInsertFrames = 20, LifeLeaveFrames = 40; // the group's three moves (unk98 = 20 the drop)
    private static readonly Vector2 LifeAppearAt = new Vector2(-150f, 180f); // unk1C8 / unk1CA: where the meter appears, from home

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
    // the life meter: the game's states (unk18), numbered as its jump table has them -- see the summary above
    private enum Life { Hidden = 0, Appear = 1, Shown = 2, Leave = 3, AirAppear = 4, AirShown = 5, AirLeave = 6, Insert = 7, AirInsert = 8, AirStand = 9, Demo = 10 }
    private Life _life = Life.Hidden;
    private int _lifeF;                                 // unk84: the state's frame counter (processAppearLife / processInsertLife's frame, the hold before a slide)
    private bool _lifeAppearing, _lifeLeaving;          // unk38 / unk4C: an appear or a slide-away is under way (a second is refused)
    private int _lifeUnder;                             // unk268: his frames under water, 0 the frame he is not
    private bool _lifeVisible;                          // the group pane's show / hide (unk1C4)
    private readonly LifeMove _lifeMove = new LifeMove(); // the group's TBoundPane: setPanePosition / update
    private SmsHudPane _lDisc, _lText, _lSun, _lSunB;  // his PaintBlobBG, LifeText, GrayLayer and OrangeLayer (the sun is two layers)
    private LifeMeter _lMeter;                          // his Font/LifeMeter/LifeMeter.tscn: the segments, the air recolour, the pulse
    private int _lifeShown = 8;                         // unk1CC[0]: the count the meter has reached, one step a frame
    private int _lifeLast = 8;                          // unk1C: the count after its last step
    private int _lifeLit = 8;                           // the pieces his meter has lit (the game lights them per the target in start*Life, the count catches up)
    private bool _lifeAirApplied, _lifeAirLowApplied, _lifeLowApplied;
    private int _lifeBeat;                              // unk86: the beat's frame, 0 .. 2 x rate
    // the demo
    private bool _inDemo, _started, _loaded; private int _demoEndCount;

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
    }

    /// <summary>loadAfter (1906): the counts as the flag manager holds them when the console comes up -- the group hidden, the pieces
    /// lit per mHealth, unk1CC = unk1C = mHealth, every counter's digits at its count. Taken on the console's FIRST frame: the scene's
    /// _Ready runs before Mario's with every field 0, and a snapshot there (passes 19-42) had the blue coins roll up from 0 at frame 1
    /// and bring the star HUD out with them -- not the game's start, which brings in the coins and the lives only.</summary>
    private void LoadCounts()
    {
        _lifeShown = _lifeLast = _lifeLit = Mathf.Clamp(Health, 0, 8);
        _lMeter.SetHealth(_lifeLit);
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

    /// <summary>The most console frames one display frame steps (2 s). Past it the console runs LATE -- the rest of the stall comes
    /// off its clock, never off its ticks -- as the game under lag shows every tick late and none missing. A tick is never skipped:
    /// the pops are armed on single frames (At(f, 14): the icons) and a frame jumped over leaves its pane hidden until the group's
    /// next appear. (Passes 19-42 stepped 8 and jumped the rest of a long display frame. Godot caps a frame at
    /// max_physics_steps_per_frame (8) of the 60 Hz physics = 133 ms = 16 console frames, so a level's first frame -- the load --
    /// dropped frames 9-16, the coin and Mario icons due at 15, and a hitch at the star HUD's peek its icons: Jaysonn's first-load
    /// HudRef.png, every strip, x and digit home and no icons, back at the next appear.)</summary>
    private const int MaxCatchUp = 240;

    public override void _Process(double delta)
    {
        if (!Ready)
            return;
        _now += (float)delta * SmsHud.Fps;
        int owed = (int)MathF.Floor(_now) - _frame;
        if (owed > MaxCatchUp)
        {
            _now -= owed - MaxCatchUp;
            owed = MaxCatchUp;
        }
        for (int i = 0; i < owed; i++)
        {
            _frame++;
            Step();
        }
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
        if (!_loaded)
        {
            _loaded = true;
            LoadCounts(); // the first frame: Mario's setup has fed the fields by now
        }
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
            _redVisible = false; _redParked = false; _timerVisible = false; _timerParked = false;
            _lifeVisible = false; _lifeAppearing = false; _lifeLeaving = false; GoLife(Life.Demo); // the tail of updateLifeMeterState hides the group at amount 0
            _coinSlide.Set(_now, CoinHidden, CoinHidden, 0); _marioSlide.Set(_now, MarioHidden, MarioHidden, 0);
            _shineSlide.Set(_now, ShineHidden, ShineHidden, 0); _blueSlide.Set(_now, BlueHidden, BlueHidden, 0); _redSlide.Set(_now, RedBelow, RedBelow, 0);
            _timerSlide.Set(_now, RedBelow, RedBelow, 0);
            return;
        }
        // startLifeMeterDisappear(this, 120): the slide away runs during the demo (state 10 updates it every frame, no hold); refused while
        // an appear or a slide is under way, in which case state 10 finishes that move instead
        StartLifeMeterDisappear(120);
        GoLife(Life.Demo);
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

    /// <summary>TGCConsole2::pauseIn (GCConsole2.cpp 2578), the director's pause entry (MarDirectorDirect.cpp 732): the help balloon away
    /// (startDisappearBalloon forced), the lives in (startAppearMario), the star HUD in (startAppearStar), the telop away
    /// (startDisappearTelop), the timer and the red coins parked off the bottom (startDownLeftBot: 20 frames to 525 - y1). The pause
    /// menu's mask sits under the console, so these show bright over it; the auto-hide waits for the resume.</summary>
    public void PauseIn()
    {
        var layer = SmsHudLayer.Current;
        layer?.Balloon?.Hide();
        StartAppearMario();
        StartAppearStar();
        layer?.Telop?.Disappear();
        if (_redVisible && !_redParked) { _redSlide.Retarget(_now, RedGone, 20); _redParked = true; }
        if (_timerVisible && !_timerParked) { _timerSlide.Retarget(_now, RedGone, 20); _timerParked = true; _timerF0 = -1; }
    }

    /// <summary>TGCConsole2::pauseOut (2588), the resume: the telop may come back on its own clock (startAppearTelop(false)), the timer
    /// (unk51C) and the red coins (unk448) parked by the pause come back, the lives and the star HUD go (startDisappearMario / Star --
    /// Jaysonn's IdleHold brings them back at once while he stands, as it would have anyway).</summary>
    public void PauseOut()
    {
        if (_timerParked && TimerRunning) { _timerParked = false; StartInsertTimer(); }
        if (_redParked) { _redParked = false; StartAppearRedCoin(); }
        StartDisappearMario();
        StartDisappearStar();
    }

    /// <summary>TTalk2D2::openTalkWindow (Talk2D2.cpp 467): startDisappearMario -- the lives slide away for the talk (the telop and the help
    /// balloon go through the layer's own Disappear / Hide). They come back as they would anyway: the game's console leaves them off until
    /// the next lives change or idle peek; here Jaysonn's IdleHold brings them back once he stands in WAIT again.</summary>
    public void TalkIn() => StartDisappearMario();

    /// <summary>For the drives: the console frame, the star HUD and the lives in view.</summary>
    public int Frame => _frame;
    public bool StarShown => _starVisible && !_starLeaving;
    public bool LivesShown => _marioVisible && !_marioLeaving;

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
        // the life meter: state 10 brings it back itself the frame after (updateLifeMeterState, case 10)
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
            && !GetTree().Paused // updateStarHudAutoHide (1070): not under STATE_PAUSE_MENU / STATE_CARD_SAVE -- the pause brought them in to stay
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
    /// <summary>The group's TBoundPane (unk1C4): setPanePosition arms a quadratic through three points over N frames and update() moves it
    /// one frame on -- only on the frames the game's state calls update, so an armed slide waits out the 120-frame hold and the appear's
    /// curve waits out the 100 frames in the middle of the screen. The motion runs at the console's pace (SmsHud.Anim).</summary>
    private sealed class LifeMove
    {
        private Vector2 _p0, _p1, _p2; private float _t, _step; private bool _pending;
        /// <summary>The pane's offset from home as of its last update (the pane stays put between updates).</summary>
        public Vector2 At { get; private set; }
        public void Set(Vector2 p0, Vector2 p1, Vector2 p2, int frames) { _p0 = p0; _p1 = p1; _p2 = p2; _t = 0f; _step = SmsHud.Anim / frames; _pending = true; }
        /// <summary>TBoundPane::update: one frame of the move; true once nothing is pending.</summary>
        public bool Update()
        {
            if (!_pending)
                return true;
            if (_t > 1f) { _t = 1f; _pending = false; }
            At = SmsHud.Bezier(_p0, _p1, _p2, _t);
            _t += _step;
            return !_pending;
        }
    }

    /// <summary>The first switch of updateLifeMeterState: the states that read (int)mAir -- 4 5 6 8 9; the rest read mHealth.</summary>
    private bool LifeAirMode => _life == Life.AirAppear || _life == Life.AirShown || _life == Life.AirLeave || _life == Life.AirInsert || _life == Life.AirStand;
    private bool _lifeAirColours;                       // the colours the pieces carry: set by start*Life's mode, then by the state at every step

    private void GoLife(Life to)
    {
        if (to == _life)
            return;
        if (DebugPrint)
            GD.Print($"[SMS hud] life meter {(int)_life} {_life} -> {(int)to} {to} at frame {_frame}: f {_lifeF} count {_lifeShown} lit {_lifeLit} health {Health} air {Air} under {_lifeUnder} off ({_lifeMove.At.X:F0}, {_lifeMove.At.Y:F0}) {(_lifeVisible ? "shown" : "hidden")}");
        _life = to;
    }

    /// <summary>startAppearLife (2325): the colours and the lit pieces per mHealth (0) or (int)mAir (1), the appear flag, frame 0; the pane's
    /// +(-150, 180) comes with processAppearLife's frame 0 here. Refused while an appear runs, in a demo, or with no health -- the callers
    /// move to their next state regardless, as the game's do.</summary>
    private bool StartAppearLife(int mode)
    {
        if (_lifeAppearing || _inDemo || Mathf.Clamp(Health, 0, 8) == 0)
            return false;
        SetLifeLit(mode == 1 ? Air : Health);
        _lifeAirColours = mode == 1;
        _lifeAppearing = true; _lifeF = 0;
        return true;
    }

    /// <summary>processAppearLife (3010): frame 0 arms the 40-frame curve from (-150, 180) through (-10, 10) home, shows the pane and lands
    /// it on the first point; frames 1..99 leave it there; from 100 the curve runs. True at once with no health or no air.</summary>
    private bool ProcessAppearLife(int frame)
    {
        if (Mathf.Clamp(Health, 0, 8) == 0 || Air == 0)
            return true;
        if (frame == 0)
        {
            _lifeMove.Set(LifeAppearAt, new Vector2(-10f, 10f), Vector2.Zero, LifeAppearFrames);
            _lifeVisible = true;
            _lDisc.Shown = true; _lDisc.Still(); _lText.Shown = true; _lText.Still(); SunShown(true); SunStill(); // whole, wherever an insert left them
            _lifeMove.Update();
            return false;
        }
        if (frame < LifeAppearHold)
            return false;
        return _lifeMove.Update();
    }

    /// <summary>startInsertLife (2262): the colours and the lit pieces per mHealth (0) / (int)mAir (1), frame 0, the group shown and armed
    /// to drop from above over 20 frames (unk98), the disc popping (50), "LIFE" and the sun hidden until processInsertLife shows them.</summary>
    private void StartInsertLife(int mode)
    {
        SetLifeLit(mode == 1 ? Air : Health);
        _lifeAirColours = mode == 1;
        _lifeF = 0;
        _lifeVisible = true;
        _lifeMove.Set(new Vector2(0f, LifeAbove), new Vector2(0f, MathF.Floor(LifeAbove * 0.5f)), Vector2.Zero, LifeInsertFrames);
        _lDisc.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
        _lText.Shown = false; _lText.Still(); SunShown(false); SunStill();
    }

    /// <summary>processInsertLife (3032): the group's drop, "LIFE" shown and popping at 14, the sun at 24; done when the drop and the three
    /// pops (each with its 30-frame settle: updateDownPaneState) are over.</summary>
    private bool ProcessInsertLife(int frame)
    {
        bool done = _lifeMove.Update();
        if (At(frame, 14)) _lText.Pop(_frame, SmsHud.DownTop, SmsHud.DownMid, 50);
        if (At(frame, 24)) SunPop(_frame);
        done &= _lDisc.PopDone(_now) && _lText.PopDone(_now) && _lSun.PopDone(_now) && _lSunB.PopDone(_now);
        return done;
    }

    /// <summary>startLifeMeterDisappear (340): the 40-frame slide from home up past the top, the hold counter at `frame` (0: the full 120
    /// before it moves; 120: at once, the camera demo). Refused while an appear or a slide runs.</summary>
    private bool StartLifeMeterDisappear(int frame)
    {
        if (_lifeAppearing || _lifeLeaving)
            return false;
        _lifeLeaving = true;
        _lifeMove.Set(Vector2.Zero, new Vector2(0f, MathF.Floor(LifeGone * 0.5f)), new Vector2(0f, LifeGone), LifeLeaveFrames);
        _lifeF = frame;
        return true;
    }

    /// <summary>resetLife (2304): the pieces lit per the count, every piece's bounds its own again (the beat's inset undone -- his pulse
    /// has none to undo).</summary>
    private void ResetLife(int count) => SetLifeLit(count);

    private void SetLifeLit(int count)
    {
        count = Mathf.Clamp(count, 0, 8);
        if (count == _lifeLit)
            return;
        _lifeLit = count;
        _lMeter.SetHealth(count);
    }

    private void PlayLifeStep(AudioStreamPlayer se)
    {
        // playLifeChangeSound: in play only (the director's state 4), not through a demo
        if (!_inDemo && se != null && se.Stream != null)
            se.Play();
    }

    // his sun is two layers (the grey sockets, the orange rays): both ride lm_0's motions
    private void SunShown(bool on) { _lSun.Shown = on; _lSunB.Shown = on; }
    private void SunStill() { _lSun.Still(); _lSunB.Still(); }
    private void SunPop(float at) { _lSun.Pop(at, SmsHud.DownTop, SmsHud.DownMid, 50); _lSunB.Pop(at, SmsHud.DownTop, SmsHud.DownMid, 50); }

    /// <summary>updateLifeMeterState (397), state by state as the game's jump table orders them, then the count's step and the beat.</summary>
    private void StepLife()
    {
        // the first switch: which count this state follows
        bool airMode = LifeAirMode;
        int amount = Mathf.Clamp(airMode ? Air : Health, 0, 8);
        // unk268: his frames under water; past 0xF0 the air timeout holds, and it is gone the frame he is not under
        bool airTimeout = false;
        if (UnderWater)
        {
            if (_lifeUnder < LifeAirTimeout) _lifeUnder++;
            else airTimeout = true;
        }
        else
            _lifeUnder = 0;

        switch (_life)
        {
            case Life.Hidden:
                _lifeVisible = false;
                // SMS_isDivingMap(): the helmet stages insert the air meter here (startInsertLife(1) -> 8); none in the port
                if (airTimeout)
                {
                    // under for 240 frames: the AIR meter appears in the middle of the screen (the pieces reset to the health first, as the game has it)
                    ResetLife(amount);
                    StartAppearLife(1);
                    amount = Mathf.Clamp(Air, 0, 8);
                    _lifeShown = amount;
                    GoLife(Life.AirAppear);
                }
                if (_lifeShown < 8 && !_lifeVisible && amount != 0)
                {
                    // hurt: the health meter appears (the game runs this check after the air one too: an air count under 8 at the timeout
                    // -- a quick re-dive -- turns that appear into a health one, its pieces still per the air)
                    StartAppearLife(0);
                    GoLife(Life.Appear);
                }
                break;
            case Life.Appear:
                if (ProcessAppearLife(_lifeF++)) { GoLife(Life.Shown); _lifeAppearing = false; }
                if (airTimeout && StartLifeMeterDisappear(1)) GoLife(Life.Leave); // never granted: the appear flag holds until it is done
                break;
            case Life.Shown:
                if (_lifeShown == 8) { StartLifeMeterDisappear(0); GoLife(Life.Leave); }      // full again: away (state 3 even when the slide was refused)
                else if (airTimeout && StartLifeMeterDisappear(0)) GoLife(Life.Leave);      // under water: away, the air meter follows from 0
                break;
            case Life.Leave:
                if (_lifeF > LifeLeaveHold) { if (_lifeMove.Update()) { _lifeLeaving = false; GoLife(Life.Hidden); } }
                else _lifeF++;
                break;
            case Life.AirAppear:
                if (ProcessAppearLife(_lifeF)) { GoLife(Life.AirShown); _lifeAppearing = false; }
                if (!UnderWater && _lifeF == 0) _lifeF = 1;                      // surfaced during the hold: the appear goes on
                if (!(amount >= 8 && _lifeF == 0) && _lifeF < 1000) _lifeF++;    // the hold in the middle of the screen lasts while his air is still 8
                break;
            case Life.AirShown:
                if (!UnderWater)
                {
                    if (_lifeShown == 8) { StartLifeMeterDisappear(0); GoLife(Life.AirLeave); } // the air back to 8: away (state 6 regardless)
                    else if (_lifeShown != _lifeLast)
                    {
                        // a count that never stepped since it was set straight: the game restarts it as a health appear in air colours
                        StartAppearLife(1);
                        amount = Mathf.Clamp(Health, 0, 8);
                        _lifeShown = amount;
                        ResetLife(amount);
                        GoLife(Life.Appear);
                    }
                }
                break;
            case Life.AirLeave:
                if (_lifeF > LifeLeaveHold)
                {
                    if (_lifeMove.Update())
                    {
                        _lifeLeaving = false;
                        if (Mathf.Clamp(Health, 0, 8) == 8)
                            GoLife(Life.Hidden);
                        else
                        {
                            // hurt before he went under: the health meter drops in from above
                            amount = Mathf.Clamp(Health, 0, 8);
                            _lifeShown = amount;
                            StartInsertLife(0);
                            ResetLife(amount);
                            GoLife(Life.Insert);
                        }
                    }
                }
                else _lifeF++;
                break;
            case Life.Insert:
                if (ProcessInsertLife(_lifeF++)) GoLife(Life.Shown);
                break;
            case Life.AirInsert:
                if (ProcessInsertLife(_lifeF++)) GoLife(Life.AirShown); // SMS_isDivingMap() ? 9 : 5
                break;
            case Life.AirStand:
                break;
            case Life.Demo:
                if (_inDemo)
                {
                    if (_lifeMove.Update()) _lifeLeaving = false;          // the slide away (or a refused one's pending move) runs through the demo
                }
                else if (Mathf.Clamp(Health, 0, 8) != 8) { StartInsertLife(0); GoLife(Life.Insert); }   // over: back from above as health,
                else if (UnderWater) { StartInsertLife(1); GoLife(Life.AirInsert); }                    // as air with his head under (or on a helmet stage),
                else { _lifeShown = 8; GoLife(Life.Hidden); }                                          // or not at all
                break;
        }

        // the count, one a frame (556): a piece lights with SE 0x4801, goes out with 0x4823 -- not in state 10. The game shows or hides
        // the one piece the step reaches; start*Life having lit them per the target already, his meter's lit count only moves when a
        // step passes it. The colours (updateLifeMeterColors) follow the state's mode from here on
        if (amount != _lifeShown && _life != Life.Demo)
        {
            if (_lifeShown < amount)
            {
                _lifeShown++;
                if (_lifeLit < _lifeShown) SetLifeLit(_lifeShown);
                PlayLifeStep(LifeUp);
            }
            else if (_lifeShown > 0)
            {
                if (_lifeLit >= _lifeShown) SetLifeLit(_lifeShown - 1);
                _lifeShown--;
                PlayLifeStep(LifeDown);
            }
            _lifeLast = _lifeShown;
            _lifeAirColours = airMode;
        }
        // updateLifeMeterBlink (1086): while the meter is on screen the lit pieces beat -- 3 px at 4 or more, 5 at 1..3 -- with a period of
        // 2 x rate frames (rate 100 / 80 / 60 / 35 for 4+ / 3 / 2 / 1) and the heartbeat SE 0x4800 at 1.5 x rate per cycle while the count
        // is 1..3 (not in a demo). His meter's own scale pulse (ScaleMultiplier) is the beat's look -- a deviation logged in pass 30 -- and
        // runs at the game's cadence through its ToggleInterval (HeartbeatRates); the beep is the console's (LifeBeep).
        if (_lifeVisible)
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
        LifeMeterGroup.Visible = _lifeVisible;
        if (_lifeVisible)
        {
            LifeMeterGroup.Place(_lifeMove.At, _scale, _viewport);
            _lDisc.Apply(n, Vector2.Zero);
            _lSun.Apply(n, Vector2.Zero); _lSunB.Apply(n, Vector2.Zero);
            _lText.Apply(n, Vector2.Zero);
            // updateLifeMeterColors (366): as the air meter the pieces are blue (the lowest darker under 4) -- his SetAirTint's remap; applied
            // on a change only (it swaps materials)
            bool air = _lifeAirColours, low = _lifeShown < 4;
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
