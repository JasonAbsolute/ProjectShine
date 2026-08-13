using Godot;

public partial class BlueCoinHud : Node2D
{
    [Export] public float NudgeAmount = 40f; // pixels to shift down to clear ShineHud

    private TextureRect _tensDigit;
    private TextureRect _onesDigit;
    private Texture2D[] _digitTextures;

    private Vector2 _onScreenPos;
    private Vector2 _offScreenPos;
    private Vector2 _nudgedPos;
    private Tween _posTween;
    private int _count;

    public override void _Ready()
    {
        _tensDigit = GetNode<TextureRect>("TensDigit");
        _onesDigit = GetNode<TextureRect>("OnesDigit");

        _digitTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
            _digitTextures[i] = GD.Load<Texture2D>($"res://Font/HudElements/coin{i}.tres");

        _onScreenPos = Position;
        _offScreenPos = new Vector2(Position.X, Position.Y - 80f);
        _nudgedPos = new Vector2(Position.X, Position.Y + NudgeAmount);

        Position = _offScreenPos;
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

    public void ShowHud()
    {
        _posTween?.Kill();
        _posTween = CreateTween();
        _posTween.TweenProperty(this, "position", _nudgedPos, 0.35f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
    }

    public void HideHud()
    {
        _posTween?.Kill();
        _posTween = CreateTween();
        _posTween.TweenProperty(this, "position", _offScreenPos, 0.2f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
    }
}
