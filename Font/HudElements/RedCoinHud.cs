using Godot;

public partial class RedCoinHud : Node2D
{
    [Export]
    public int MaxRedCoins = 8;

    /// <summary>How far below the on-screen position the HUD rests when hidden. Larger = further off-screen.</summary>
    [Export]
    public float HiddenOffsetY = 200f;

    /// <summary>How far up this HUD moves (negative Y) while a TimerHud is
    /// showing, so the two stack (red coins on top, timer below) instead of
    /// overlapping — see SetTimerActive, called by TimerHud on show/hide.</summary>
    [Export]
    public float TimerActiveShiftY = -52f;

    private TextureRect _countDigit;
    private TextureRect _totalDigit;
    private Texture2D[] _digitTextures;

    private Vector2 _onScreenPos;
    private Vector2 _offScreenPos;
    private Tween _posTween;

    private Control[] _waveElements;
    private Vector2[] _restPositions;
    private int _count;
    private bool _isShown;
    private Vector2 _timerShift = Vector2.Zero;

    private const float WaveOffset = 50f;
    private const float WaveStagger = 0.05f;

    public override void _Ready()
    {
        _countDigit = GetNode<TextureRect>("CountDigit");
        _totalDigit = GetNode<TextureRect>("TotalDigit");

        _digitTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
            _digitTextures[i] = GD.Load<Texture2D>($"res://Font/HudElements/coin{i}.tres");

        _totalDigit.Texture = _digitTextures[Mathf.Clamp(MaxRedCoins, 0, 9)];

        _waveElements = new Control[]
        {
            GetNode<TextureRect>("CoinIcon"),
            GetNode<TextureRect>("CoinX"),
            _countDigit,
            GetNode<TextureRect>("CoinSlash"),
            _totalDigit,
        };

        _restPositions = new Vector2[_waveElements.Length];
        for (int i = 0; i < _waveElements.Length; i++)
            _restPositions[i] = _waveElements[i].Position;

        _onScreenPos = Position;
        _offScreenPos = new Vector2(Position.X, Position.Y + HiddenOffsetY); // below screen

        Position = _offScreenPos;
        ResetElements();

        SetCount(0);
    }

    public void SetCount(int coins)
    {
        _count = Mathf.Clamp(coins, 0, 9);
        _countDigit.Texture = _digitTextures[_count];
    }

    /// <summary>Sets the new count and pops flare-stars off the count digit when
    /// it changes.</summary>
    public void TickUp(int newCount)
    {
        int old = _count;
        SetCount(newCount);
        if (old != _count)
            HudCountBurst.OnSingle(this, _countDigit);
    }

    public void ShowHud()
    {
        _isShown = true;
        _posTween?.Kill();
        Position = _offScreenPos;
        ResetElements();

        _posTween = CreateTween();
        _posTween
            .TweenProperty(this, "position", _onScreenPos + _timerShift, 0.35f)
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
    }

    public void HideHud()
    {
        _isShown = false;
        _posTween?.Kill();
        _posTween = CreateTween();
        _posTween
            .TweenProperty(this, "position", _offScreenPos, 0.2f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
    }

    /// <summary>Called by TimerHud when it shows/hides — slides this HUD up out
    /// of the timer's way (or back down) if currently visible; if currently
    /// hidden, just remembers the shift for the next ShowHud().</summary>
    public void SetTimerActive(bool active)
    {
        _timerShift = active ? new Vector2(0f, TimerActiveShiftY) : Vector2.Zero;
        if (!_isShown)
            return;

        _posTween?.Kill();
        _posTween = CreateTween();
        _posTween
            .TweenProperty(this, "position", _onScreenPos + _timerShift, 0.25f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
    }

    private void ResetElements()
    {
        // Reversed: elements start BELOW their rest position and rise up
        for (int i = 0; i < _waveElements.Length; i++)
            _waveElements[i].Position = _restPositions[i] + new Vector2(0f, WaveOffset);
    }
}
