using Godot;

public partial class RedCoinPopup : Node3D
{
    private Sprite3D _sprite;

    public override void _Ready()
    {
        _sprite = GetNode<Sprite3D>("Sprite3D");
    }

    public void Setup(int coinNumber)
    {
        int digit = Mathf.Clamp(coinNumber, 0, 9);
        _sprite.Texture = GD.Load<Texture2D>($"res://Font/HudElements/coin{digit}.tres");
        _sprite.Modulate = new Color(1f, 0.35f, 0.35f, 1f);

        Scale = Vector3.Zero;
        Vector3 spawnPos = Position;
        Vector3 jumpPeak = spawnPos + Vector3.Up * 0.6f;

        // Scale: elastic pop-in → hold → squeeze disappear
        var scaleTween = CreateTween();
        scaleTween.TweenProperty(this, "scale", Vector3.One, 0.5f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Elastic);
        scaleTween.TweenInterval(0.25f);
        scaleTween.TweenProperty(this, "scale", new Vector3(0f, 1.4f, 0f), 0.225f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        scaleTween.TweenCallback(Callable.From(QueueFree));

        // Position: jump arc runs at the same time as the elastic pop
        var jumpTween = CreateTween();
        jumpTween.TweenProperty(this, "position", jumpPeak, 0.22f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);
        jumpTween.TweenProperty(this, "position", spawnPos, 0.22f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
    }
}
