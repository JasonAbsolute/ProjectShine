using Godot;

/// <summary>
/// Shared "count ticked up" flourish for HUD digit counters: a tight, dense
/// cluster of little gold flare-stars that pop off a digit and float up while
/// fading, then free themselves. Used by ShineHud and the coin counters so the
/// burst fires only on the digit(s) that actually changed.
/// </summary>
public static class HudCountBurst
{
    private static readonly Color DefaultTint = new(1f, 0.92f, 0.45f);

    /// <summary>Burst on whichever of the two digits (tens / ones) changed between
    /// oldCount and newCount — just the ones on a normal tick, both on a rollover
    /// (e.g. 9 → 10).</summary>
    public static void OnDigitChange(
        Node2D parent,
        Control tensDigit,
        Control onesDigit,
        int oldCount,
        int newCount,
        Color? tint = null
    )
    {
        oldCount = Mathf.Clamp(oldCount, 0, 99);
        newCount = Mathf.Clamp(newCount, 0, 99);
        Color c = tint ?? DefaultTint;
        if (oldCount / 10 != newCount / 10)
            SpawnAt(parent, Center(tensDigit), c);
        if (oldCount % 10 != newCount % 10)
            SpawnAt(parent, Center(onesDigit), c);
    }

    /// <summary>Burst on a single digit (e.g. the red-coin count).</summary>
    public static void OnSingle(Node2D parent, Control digit, Color? tint = null) =>
        SpawnAt(parent, Center(digit), tint ?? DefaultTint);

    private static Vector2 Center(Control digit) => digit.Position + digit.Size * 0.5f;

    private static void SpawnAt(Node2D parent, Vector2 center, Color tint)
    {
        var tex = GD.Load<Texture2D>("res://assets/shine_sparkle.png");
        if (tex == null)
            return;

        var mat = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        for (int i = 0; i < 15; i++)
        {
            var star = new Sprite2D
            {
                Texture = tex,
                Material = mat,
                Modulate = new Color(tint.R, tint.G, tint.B, 1f),
                Scale = Vector2.One * (float)GD.RandRange(0.07, 0.13),
                Position = center + new Vector2((float)GD.RandRange(-10, 10), (float)GD.RandRange(-8, 8)),
                ZIndex = 20,
            };
            parent.AddChild(star);

            float rise = (float)GD.RandRange(20, 40);
            var t = parent.CreateTween();
            t.TweenInterval(i * 0.02f);
            t.TweenProperty(star, "position:y", star.Position.Y - rise, 0.55f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
            t.Parallel()
                .TweenProperty(star, "modulate:a", 0f, 0.55f)
                .SetEase(Tween.EaseType.In);
            t.TweenCallback(Callable.From(star.QueueFree));
        }
    }
}
