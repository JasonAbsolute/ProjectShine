using Godot;

/// <summary>
/// The screen fader: TSMSFader (GC2D/ScrnFader.cpp) and the Hx wiper (GC2D/hx_wiper.c). <see cref="StartWipe"/> is startWipe(type, _,
/// delay): the request waits its delay, then the wipe runs frame by frame at the wiper's rate; a wipe out ends FULLY_FADED_OUT (solid),
/// a fade in ends FULLY_FADED_IN (clear). The death wipe (10, Hx_Test2): four bands, each an Hx motion of 500 rows -- 2 frames of
/// acceleration, 8 / 8 / 7 / 9 of cruise, 1 of braking (Hx_MotionSet) -- over timers of 11 / 11 / 10 / 12 frames, the earlier bands
/// held complete. The hell death (2, Hx_Circle direction 0): the iris from 400 to 0 over 25 frames (400, 2, 13, 10). The plain fades
/// (startFadeout / startFadein): alpha over the frames given. The gate-in wipe (8, Hx_Test4; pass 127): a black spiral band winding in
/// from the edge to the centre over 38 frames. The game-over wipe (0xD, Hx_GameOver) draws a texture the port does not
/// have: a plain fade stands in. assets/Sms/Console/Fader.tscn holds the rect and its shader (Fader.gdshader); it sits above everything.
/// </summary>
public partial class SmsFader : CanvasLayer
{
    public enum Wipe { None, Arcs, Circle, FadeOut, FadeIn, Spiral }
    public enum Status { FullyFadedIn, FadingOut, FullyFadedOut, FadingIn }

    [Export] public ColorRect Rect;
    /// <summary>The Hx wiper's frame: the game's screen rate.</summary>
    [Export] public float FrameRate = 30f;
    /// <summary>A plain fade's length when none is given (startWipe(0xE, 0.3)).</summary>
    [Export] public float PlainFadeSeconds = 0.3f;

    public static SmsFader Current { get; private set; }
    public Status State { get; private set; } = Status.FullyFadedIn;
    public bool IsFullyFadedOut => State == Status.FullyFadedOut;
    public bool IsFullyFadedIn => State == Status.FullyFadedIn;
    /// <summary>The wipe running now (None between wipes) -- for the drives.</summary>
    public Wipe RunningWipe => _running;

    private ShaderMaterial _mat;
    private Wipe _pending = Wipe.None;
    private float _pendingDelay, _pendingSeconds;
    private Wipe _running = Wipe.None;
    private double _acc;          // time toward the next wiper frame
    private int _frame;           // wiper frames into the running wipe
    private int _stage;           // the band being drawn
    private float[] _rows = new float[4];
    private float _plainSeconds, _plainT;
    private float _spiralStep, _spiralThin;   // Hx_Test4: rstep, thin
    // Hx motion
    private float _mT1, _mT2, _mT3, _mAcc, _mDec, _mVel, _mPos, _mFrames;
    private int _timer;

    public override void _Ready()
    {
        Current = this;
        if (Rect == null)
        {
            GD.PushWarning($"[SMS fader] {GetPath()}: the scene's Rect export is not set (see assets/Sms/Console/Fader.tscn)");
            return;
        }
        _mat = Rect.Material as ShaderMaterial;
        Rect.Visible = false;
        SetParam("mode", 0);
        SetParam("color", new Vector3(FadeColor.R, FadeColor.G, FadeColor.B));
    }

    public override void _ExitTree()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>startWipe(type, _, delaySeconds): the request runs out its delay first. Seconds: a plain fade's length (else PlainFadeSeconds).</summary>
    public void StartWipe(Wipe type, float delaySeconds, float seconds = 0f)
    {
        _pending = type;
        _pendingDelay = Mathf.Max(0f, delaySeconds);
        _pendingSeconds = seconds > 0f ? seconds : PlainFadeSeconds;
    }

    /// <summary>The screen black now (the fader set to black before a stage move; the next stage fades in).</summary>
    public void SetBlack()
    {
        _pending = Wipe.None;
        _running = Wipe.None;
        State = Status.FullyFadedOut;
        Show(1);
    }

    /// <summary>startFadein over the seconds given (a stage's plain start, startWipe(0xF, 0.4)).</summary>
    public void FadeIn(float seconds) => StartWipe(Wipe.FadeIn, 0f, seconds);

    /// <summary>TSMSFader::setColor: what the fades and wipes are drawn in (black; the select screen's white). Kept until set again.</summary>
    public void SetColor(Color c) { FadeColor = c; SetParam("color", new Vector3(c.R, c.G, c.B)); }
    public Color FadeColor { get; private set; } = Colors.Black;

    private void SetParam(string name, Variant v) => _mat?.SetShaderParameter(name, v);

    private void Show(int mode)
    {
        if (Rect == null)
            return;
        Rect.Visible = mode != 0;
        SetParam("mode", mode);
    }

    private void MotionSet(float distance, float t1, float t2, float t3)
    {
        _mT1 = t1; _mT2 = t1 + t2; _mT3 = _mT2 + t3;
        float v = 2f * distance / (t3 + (t2 + (t1 + t2)));
        _mAcc = t1 != 0f ? v / t1 : 0f;
        _mDec = t3 != 0f ? -v / t3 : 0f;
        _mVel = 0f; _mPos = 0f; _mFrames = 0f;
    }

    private float MotionUpdate()
    {
        if (_mT1 > _mFrames) _mVel += _mAcc;
        else if (_mT2 <= _mFrames) _mVel += _mDec;
        _mFrames += 1f;
        _mPos += _mVel;
        return _mPos;
    }

    private void Begin(Wipe w)
    {
        _running = w;
        _frame = 0;
        _acc = 0;
        switch (w)
        {
            case Wipe.Arcs:
                State = Status.FadingOut;
                _stage = 0;
                _rows = new float[4];
                MotionSet(500f, 2f, 8f, 1f); _timer = 11;
                SetParam("rows", new Vector4(0f, 0f, 0f, 0f));
                Show(3);
                break;
            case Wipe.Circle:
                State = Status.FadingOut;
                MotionSet(400f, 2f, 13f, 10f); _timer = 25;
                SetParam("radius", 400f);
                Show(4);
                break;
            case Wipe.FadeOut:
                State = Status.FadingOut;
                _plainSeconds = _pendingSeconds; _plainT = 0f;
                SetParam("alpha", 0f);
                Show(2);
                break;
            case Wipe.Spiral:
                // Hx_Test4 direction 0 (type 8, the gate-in): rstep 0 + 5 a frame, thin 124.3 + 0.15 a frame, 38 frames (SMS pass 127)
                State = Status.FadingOut;
                _spiralStep = 0f; _spiralThin = 124.3f; _timer = 38;
                SetParam("spiral", new Vector2(0f, _spiralThin));
                Show(5);
                break;
            case Wipe.FadeIn:
                State = Status.FadingIn;
                _plainSeconds = _pendingSeconds; _plainT = 0f;
                SetParam("alpha", 1f);
                Show(2);
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (Rect == null)
            return;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        Rect.Position = Vector2.Zero;
        Rect.Size = vp;
        SetParam("viewport", vp);
        if (_pending != Wipe.None)
        {
            _pendingDelay -= (float)delta;
            if (_pendingDelay <= 0f)
            {
                Wipe w = _pending;
                _pending = Wipe.None;
                Begin(w);
            }
        }
        switch (_running)
        {
            case Wipe.FadeOut:
            case Wipe.FadeIn:
            {
                _plainT += (float)delta;
                float t = _plainSeconds > 0f ? Mathf.Clamp(_plainT / _plainSeconds, 0f, 1f) : 1f;
                bool fadeIn = _running == Wipe.FadeIn;
                SetParam("alpha", fadeIn ? 1f - t : t);
                if (t >= 1f)
                {
                    _running = Wipe.None;
                    if (fadeIn) { State = Status.FullyFadedIn; Show(0); }
                    else { State = Status.FullyFadedOut; Show(1); }
                }
                break;
            }
            case Wipe.Arcs:
            case Wipe.Circle:
            case Wipe.Spiral:
                _acc += delta * FrameRate;
                while (_acc >= 1.0 && _running != Wipe.None)
                {
                    _acc -= 1.0;
                    Step();
                }
                break;
        }
    }

    /// <summary>One wiper frame (Hx_UpdateWipe -> the handler).</summary>
    private void Step()
    {
        _frame++;
        if (_running == Wipe.Arcs)
        {
            _rows[_stage] = MotionUpdate();
            SetParam("rows", new Vector4(_rows[0], _rows[1], _rows[2], _rows[3]));
            if (--_timer <= 0)
            {
                _rows[_stage] = 600f; // the finished band is drawn whole from here on
                _stage++;
                switch (_stage)
                {
                    case 1: MotionSet(500f, 2f, 8f, 1f); _timer = 11; break;
                    case 2: MotionSet(500f, 2f, 7f, 1f); _timer = 10; break;
                    case 3: MotionSet(500f, 2f, 9f, 1f); _timer = 12; break;
                    default:
                        SetParam("rows", new Vector4(600f, 600f, 600f, 600f));
                        _running = Wipe.None;
                        State = Status.FullyFadedOut;
                        Show(1);
                        return;
                }
                SetParam("rows", new Vector4(_rows[0], _rows[1], _rows[2], _rows[3]));
            }
        }
        else if (_running == Wipe.Spiral)
        {
            _spiralStep += 5f; _spiralThin += 0.15f;
            SetParam("spiral", new Vector2(_spiralStep, _spiralThin));
            if (--_timer <= 0)
            {
                _running = Wipe.None;
                State = Status.FullyFadedOut;
                Show(1);
            }
        }
        else if (_running == Wipe.Circle)
        {
            float r = 400f - MotionUpdate();
            if (r < 0f) r = 0f;
            SetParam("radius", r);
            if (--_timer <= 0)
            {
                _running = Wipe.None;
                State = Status.FullyFadedOut;
                Show(1);
            }
        }
    }
}
