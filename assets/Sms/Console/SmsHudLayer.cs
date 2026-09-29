using Godot;

/// <summary>
/// The console as a scene: assets/Sms/Console/SmsHud.tscn is this CanvasLayer with the StandardHud (the counters, the life meter, the
/// timer) and the WaterHud (the gauge) as children, every pane a TextureRect laid out on the game's 640 x 448 frame -- his to move
/// and re-texture. Instance it under Mario in Player.tscn: Mario.cs (SmsHudLayerGet) finds it there and feeds both. The knobs are
/// Jaysonn's deviations from the game's console (PROJECT_LOG); the game's own values are 1 / 0 / 0 / 0 / 0 / 1 / false.
/// </summary>
public partial class SmsHudLayer : CanvasLayer
{
    [Export] public SmsStandardHud Standard;
    [Export] public SmsWaterHud Water;
    /// <summary>1 = the frame's 448 lines on the window's height (a coin digit is 8 % of the screen); 0.7 keeps his own HUD's size.</summary>
    [Export] public float Size = 0.7f;
    /// <summary>Game pixels the left-hand rows move toward the left edge (the game has them 13 px in).</summary>
    [Export] public float LeftShift = 0f;
    /// <summary>Game pixels the top rows move up (the game has them at y 74; the CRT hid ~22 of those lines).</summary>
    [Export] public float TopShift = -22f;
    /// <summary>Game pixels the gauge and the life meter move toward the right edge (the game: 59 / 61 px in).</summary>
    [Export] public float RightShift = 30f;
    /// <summary>Game pixels the gauge and the red coins move up off the bottom (the game has "WATER" 3 px past it).</summary>
    [Export] public float BottomShift = -10f;
    /// <summary>The motions -- the pops, the settles, the slides, the staggers, the meter's mid-screen hold -- at this many times the
    /// game's 30 fps pace; the waits (the idle 200 frames, a blue coin's 400, the 120 at full health, the blinks) stay the game's.</summary>
    [Export] public float AnimSpeed = 4f;
    /// <summary>The star HUD and the lives stay while he stands idle, instead of leaving after the idle count.</summary>
    [Export] public bool IdleHold = true;

    public override void _EnterTree() => Apply();
    public override void _Ready() => Apply();

    /// <summary>The knobs into SmsHud, which every pane's motion reads.</summary>
    public void Apply()
    {
        SmsHud.Size = Size;
        SmsHud.LeftShift = LeftShift; SmsHud.TopShift = TopShift; SmsHud.RightShift = RightShift; SmsHud.BottomShift = BottomShift;
        SmsHud.AnimSpeed = AnimSpeed;
        SmsHud.IdleHold = IdleHold;
    }
}
