using Godot;

public partial class LifeCounterHud : Node2D
{
    private TextureRect _tensDigit;
    private TextureRect _onesDigit;
    private Texture2D[] _digitTextures;

    private Vector2 _onScreenPos;
    private Vector2 _offScreenPos;
    private Tween _stripTween;

    private TextureRect[] _waveElements;
    private Vector2[] _restPositions; // editor-placed position for each element

    private const float WaveOffset = 50f;  // how far above rest each element starts
    private const float WaveStagger = 0.05f; // delay between each element

    public override void _Ready()
    {
        _tensDigit = GetNode<TextureRect>("TensDigit");
        _onesDigit = GetNode<TextureRect>("OnesDigit");

        _digitTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
            _digitTextures[i] = GD.Load<Texture2D>($"res://Font/HudElements/coin{i}.tres");

        _waveElements = new[]
        {
            GetNode<TextureRect>("MarioHead"),
            GetNode<TextureRect>("MarioText"),
            GetNode<TextureRect>("CoinX"),
            _tensDigit,
            _onesDigit,
        };

        // Capture each element's editor position as its rest target
        _restPositions = new Vector2[_waveElements.Length];
        for (int i = 0; i < _waveElements.Length; i++)
            _restPositions[i] = _waveElements[i].Position;

        _onScreenPos = Position;
        _offScreenPos = new Vector2(Position.X, Position.Y - 80f);

        Position = _offScreenPos;
        ResetElements();
    }

    public void ShowLives(int lives)
    {
        lives = Mathf.Clamp(lives, 0, 99);
        _tensDigit.Texture = _digitTextures[lives / 10];
        _onesDigit.Texture = _digitTextures[lives % 10];

        _stripTween?.Kill();
        Position = _offScreenPos;
        ResetElements();

        // Strip slides down with overshoot — no auto-hide, caller controls that
        _stripTween = CreateTween();
        _stripTween.TweenProperty(this, "position", _onScreenPos, 0.35f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);

        // Wave: each element drops from above its rest position with staggered delay
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

    public void HideLives()
    {
        _stripTween?.Kill();
        _stripTween = CreateTween();
        _stripTween.TweenProperty(this, "position", _offScreenPos, 0.2f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
    }

    private void ResetElements()
    {
        for (int i = 0; i < _waveElements.Length; i++)
            _waveElements[i].Position = _restPositions[i] + new Vector2(0f, -WaveOffset);
    }
}
