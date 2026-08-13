using Godot;

public partial class ShineHud : Node2D
{
    private TextureRect _tensDigit;
    private TextureRect _onesDigit;
    private Texture2D[] _digitTextures;

    private Vector2 _onScreenPos;
    private Vector2 _offScreenPos;
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
            GetNode<TextureRect>("ShineIcon"),
            GetNode<TextureRect>("CoinX"),
            _tensDigit,
            _onesDigit,
        };

        _restPositions = new Vector2[_waveElements.Length];
        for (int i = 0; i < _waveElements.Length; i++)
            _restPositions[i] = _waveElements[i].Position;

        _onScreenPos = Position;
        _offScreenPos = new Vector2(Position.X, Position.Y - 80f);

        Position = _offScreenPos;
        ResetElements();

        SetCount(0);
    }

    public void SetCount(int shines)
    {
        shines = Mathf.Clamp(shines, 0, 99);
        _count = shines;
        _tensDigit.Texture = _digitTextures[shines / 10];
        _onesDigit.Texture = _digitTextures[shines % 10];
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
    }

    public void HideHud()
    {
        _posTween?.Kill();
        _posTween = CreateTween();
        _posTween.TweenProperty(this, "position", _offScreenPos, 0.2f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
    }

    private void ResetElements()
    {
        for (int i = 0; i < _waveElements.Length; i++)
            _waveElements[i].Position = _restPositions[i] + new Vector2(0f, -WaveOffset);
    }

    /// <summary>Sets the new count and fires the flare-star burst ONLY on the
    /// digit(s) that actually changed — just the ones place on a normal tick, both
    /// digits on a rollover (e.g. 9 → 10).</summary>
    public void TickUp(int newCount)
    {
        int old = _count;
        SetCount(newCount);
        HudCountBurst.OnDigitChange(this, _tensDigit, _onesDigit, old, _count);
    }
}
