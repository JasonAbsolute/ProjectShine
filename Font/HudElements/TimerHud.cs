using Godot;

/// <summary>
/// Visual "TIME 00:09:65"-style display for a ShineTimer (see
/// assets/ShineTimer.cs) — MM:SS:CC (minutes:seconds:centiseconds), matching
/// the real game's precision rather than whole seconds. Purely a presenter —
/// reads the timer's value off TimerTick and formats it; has no opinion on
/// what starting/stopping/reaching-target means for gameplay. Same wave-in/out
/// show/hide animation as RedCoinHud/BlueCoinHud/YellowCoinHud.
///
/// Also nudges RedCoinHud (see RedCoinHud.SetTimerActive) up out of the way
/// while this is showing, so the two stack instead of overlapping — matches
/// the reference layout (red coin count on top, timer directly below it).
/// </summary>
public partial class TimerHud : Node2D
{
    [Export] public NodePath TimerPath;

    /// <summary>Optional — if set, this HUD is told to shift up while the timer
    /// is visible (see RedCoinHud.SetTimerActive) so the two stack cleanly.</summary>
    [Export] public NodePath RedCoinHudPath;

    /// <summary>Below this many seconds remaining (CountDown timers only), the
    /// digits flash red to signal urgency.</summary>
    [Export] public float LowTimeThreshold = 10f;

    [Export] public Color NormalColor = new Color(1, 1, 1, 1);
    [Export] public Color LowTimeColor = new Color(1, 0.25f, 0.25f, 1);
    [Export] public float LowTimeFlashSpeed = 6f;

    /// <summary>How far below the on-screen position the HUD rests when hidden.</summary>
    [Export] public float HiddenOffsetY = 200f;

    private ShineTimer _timer;
    private RedCoinHud _redCoinHud;

    private TextureRect _minTens;
    private TextureRect _minOnes;
    private TextureRect _colon1;
    private TextureRect _secTens;
    private TextureRect _secOnes;
    private TextureRect _colon2;
    private TextureRect _centiTens;
    private TextureRect _centiOnes;
    private TextureRect[] _digitRects;
    private Texture2D[] _digitTextures;

    private Vector2 _onScreenPos;
    private Vector2 _offScreenPos;
    private Tween _posTween;

    private Control[] _waveElements;
    private Vector2[] _restPositions;

    private const float WaveOffset = 50f;
    private const float WaveStagger = 0.05f;

    private bool _lowTime;
    private float _flashT;

    public override void _Ready()
    {
        _minTens = GetNode<TextureRect>("MinTens");
        _minOnes = GetNode<TextureRect>("MinOnes");
        _colon1 = GetNode<TextureRect>("Colon1");
        _secTens = GetNode<TextureRect>("SecTens");
        _secOnes = GetNode<TextureRect>("SecOnes");
        _colon2 = GetNode<TextureRect>("Colon2");
        _centiTens = GetNode<TextureRect>("CentiTens");
        _centiOnes = GetNode<TextureRect>("CentiOnes");

        _digitRects = new[]
        {
            _minTens, _minOnes, _secTens, _secOnes, _centiTens, _centiOnes,
        };

        _digitTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
            _digitTextures[i] = GD.Load<Texture2D>($"res://Font/HudElements/coin{i}.tres");

        _waveElements = new Control[]
        {
            GetNode<TextureRect>("TimeLabel"),
            _minTens, _minOnes, _colon1, _secTens, _secOnes, _colon2, _centiTens, _centiOnes,
        };

        _restPositions = new Vector2[_waveElements.Length];
        for (int i = 0; i < _waveElements.Length; i++)
            _restPositions[i] = _waveElements[i].Position;

        _onScreenPos = Position;
        _offScreenPos = new Vector2(Position.X, Position.Y + HiddenOffsetY);
        Position = _offScreenPos;
        ResetElements();

        _redCoinHud = GetNodeOrNull<RedCoinHud>(RedCoinHudPath);

        _timer = GetNodeOrNull<ShineTimer>(TimerPath);
        if (_timer == null)
        {
            GD.PushError("TimerHud: TimerPath not found.");
            return;
        }

        _timer.TimerStarted += ShowHud;
        _timer.TimerTick += OnTimerTick;
        _timer.TimerReachedTarget += HideHud;

        SetValue(_timer.StartValue);
    }

    private void OnTimerTick(float currentValue) => SetValue(currentValue);

    private void SetValue(float seconds)
    {
        int totalCentis = Mathf.Max(0, Mathf.RoundToInt(seconds * 100f));
        int minutes = Mathf.Clamp(totalCentis / 6000, 0, 99);
        int secs = (totalCentis / 100) % 60;
        int centis = totalCentis % 100;

        _minTens.Texture = _digitTextures[minutes / 10];
        _minOnes.Texture = _digitTextures[minutes % 10];
        _secTens.Texture = _digitTextures[secs / 10];
        _secOnes.Texture = _digitTextures[secs % 10];
        _centiTens.Texture = _digitTextures[centis / 10];
        _centiOnes.Texture = _digitTextures[centis % 10];

        _lowTime = _timer != null && _timer.CountDown && seconds <= LowTimeThreshold;
        if (!_lowTime)
        {
            SetDigitColor(NormalColor);
            _flashT = 0f;
        }
    }

    public override void _Process(double delta)
    {
        if (!_lowTime)
            return;
        _flashT += (float)delta * LowTimeFlashSpeed;
        float t = (Mathf.Sin(_flashT) + 1f) * 0.5f;
        SetDigitColor(NormalColor.Lerp(LowTimeColor, t));
    }

    private void SetDigitColor(Color c)
    {
        foreach (var rect in _digitRects)
            rect.Modulate = c;
    }

    public void ShowHud()
    {
        _posTween?.Kill();
        Position = _offScreenPos;
        ResetElements();

        _posTween = CreateTween();
        _posTween.TweenProperty(this, "position", _onScreenPos, 0.35f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);

        for (int i = 0; i < _waveElements.Length; i++)
        {
            float delay = 0.05f + i * WaveStagger;
            var t = CreateTween();
            t.TweenInterval(delay);
            t.TweenProperty(_waveElements[i], "position", _restPositions[i], 0.32f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
        }

        _redCoinHud?.SetTimerActive(true);
    }

    public void HideHud()
    {
        _posTween?.Kill();
        _posTween = CreateTween();
        _posTween.TweenProperty(this, "position", _offScreenPos, 0.2f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        _redCoinHud?.SetTimerActive(false);
    }

    private void ResetElements()
    {
        for (int i = 0; i < _waveElements.Length; i++)
            _waveElements[i].Position = _restPositions[i] + new Vector2(0f, WaveOffset);
    }
}
