using Godot;

public partial class YellowCoinHud : Node2D
{
    [Export] public float NudgeAmount = 80f; // pixels to shift down to clear ShineHud + BlueCoinHud

    private TextureRect _tensDigit;
    private TextureRect _onesDigit;
    private Texture2D[] _digitTextures;

    private Vector2 _onScreenPos;
    private Vector2 _offScreenPos;
    private Vector2 _nudgedPos;
    private Tween _posTween;

    private Control[] _waveElements;
    private Vector2[] _restPositions;
    private int _count;

    private const float WaveOffset = 50f;
    private const float WaveStagger = 0.05f;

    public override void _Ready()
    {
        _tensDigit = GetNode<TextureRect>("TensDigit");
        _onesDigit = GetNode<TextureRect>("OnesDigit");

        _digitTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
            _digitTextures[i] = GD.Load<Texture2D>($"res://Font/HudElements/coin{i}.tres");

        _waveElements = new Control[]
        {
            GetNode<TextureRect>("CoinIcon"),
            GetNode<TextureRect>("CoinX"),
            _tensDigit,
            _onesDigit,
        };

        _restPositions = new Vector2[_waveElements.Length];
        for (int i = 0; i < _waveElements.Length; i++)
            _restPositions[i] = _waveElements[i].Position;

        _onScreenPos = Position;
        _offScreenPos = new Vector2(Position.X, Position.Y - 80f);
        _nudgedPos = new Vector2(Position.X, Position.Y + NudgeAmount);

        Position = _offScreenPos;
        ResetElements();

        SetCount(0);
    }

    public void SetCount(int coins)
    {
        coins = Mathf.Clamp(coins, 0, 99);
        _count = coins;
        _tensDigit.Texture = _digitTextures[coins / 10];
        _onesDigit.Texture = _digitTextures[coins % 10];
    }

    /// <summary>Sets the new count and pops flare-stars off the digit(s) that
    /// changed (ones on a normal tick, both on a rollover).</summary>
    public void TickUp(int newCount)
    {
        int old = _count;
        SetCount(newCount);
        HudCountBurst.OnDigitChange(this, _tensDigit, _onesDigit, old, _count);
    }

    // Called on level start — slides in with wave
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
            var el = _waveElements[i];
            var rest = _restPositions[i];
            float delay = 0.05f + i * WaveStagger;
            var t = CreateTween();
            t.TweenInterval(delay);
            t.TweenProperty(el, "position", rest, 0.32f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
        }
    }

    // Called when life counter appears — nudge down to make room
    public void NudgeDown()
    {
        _posTween?.Kill();
        _posTween = CreateTween();
        _posTween.TweenProperty(this, "position", _nudgedPos, 0.3f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);

        for (int i = 0; i < _waveElements.Length; i++)
        {
            var el = _waveElements[i];
            var rest = _restPositions[i];
            el.Position = rest + new Vector2(0f, -20f);
            float delay = i * WaveStagger;
            var t = CreateTween();
            t.TweenInterval(delay);
            t.TweenProperty(el, "position", rest, 0.25f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
        }
    }

    // Called when Mario moves — return to normal position
    public void ReturnToNormal()
    {
        _posTween?.Kill();
        _posTween = CreateTween();
        _posTween.TweenProperty(this, "position", _onScreenPos, 0.3f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);
    }

    private void ResetElements()
    {
        for (int i = 0; i < _waveElements.Length; i++)
            _waveElements[i].Position = _restPositions[i] + new Vector2(0f, -WaveOffset);
    }
}
