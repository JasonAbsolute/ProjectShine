using System;
using Godot;

public partial class LifeMeter : Node2D
{
    [Export]
    public int MaxHealth = 8;

    [Export]
    public float ScaleMultiplier = 1.1f;

    [Export]
    public float ToggleInterval = 1.0f;

    // HUD resting position (top-right corner, set dynamically in _Ready)
    [Export]
    public Vector2 HudPosition = new Vector2(-1, -1);

    [Export]
    public float HudMargin = 10f;

    // How long the meter lingers near Mario before flying
    [Export]
    public float SpawnLingerDuration = 1.0f;

    // How long the fly-to-corner tween takes
    [Export]
    public float FlyDuration = 0.6f;

    private int _currentHealth;

    // Orange layer
    private TextureRect _orangeSwirl;
    private TextureRect[] _orangeSegments = new TextureRect[8];
    private Vector2 _orangeSwirlBaseScale;
    private Vector2 _orangeSwirlBasePos;
    private Vector2[] _orangeSegBaseScales = new Vector2[8];
    private Vector2[] _orangeSegBasePositions = new Vector2[8];

    // Gray layer
    private TextureRect _graySwirl;
    private TextureRect[] _graySegments = new TextureRect[8];
    private Vector2 _graySwirlBaseScale;
    private Vector2 _graySwirlBasePos;
    private Vector2[] _graySegBaseScales = new Vector2[8];
    private Vector2[] _graySegBasePositions = new Vector2[8];

    private float _timer;
    private bool _isScaledUp;
    private Tween _flyTween;

    public override void _Ready()
    {
        var orangeLayer = GetNode<Control>("OrangeLayer");
        _orangeSwirl = orangeLayer.GetNode<TextureRect>("OrangeSwirl");
        _orangeSwirlBaseScale = _orangeSwirl.Scale;
        _orangeSwirlBasePos = _orangeSwirl.Position;

        var grayLayer = GetNode<Control>("GrayLayer");
        _graySwirl = grayLayer.GetNode<TextureRect>("GraySwirl");
        _graySwirlBaseScale = _graySwirl.Scale;
        _graySwirlBasePos = _graySwirl.Position;

        for (int i = 0; i < 8; i++)
        {
            _orangeSegments[i] = orangeLayer.GetNode<TextureRect>($"OrangeSegment{i}");
            _orangeSegBaseScales[i] = _orangeSegments[i].Scale;
            _orangeSegBasePositions[i] = _orangeSegments[i].Position;

            _graySegments[i] = grayLayer.GetNode<TextureRect>($"GraySegment{i}");
            _graySegBaseScales[i] = _graySegments[i].Scale;
            _graySegBasePositions[i] = _graySegments[i].Position;
        }

        _currentHealth = MaxHealth;

        // Default HUD position to top-right corner if not set
        if (HudPosition.X < 0)
        {
            Vector2 viewport = GetViewportRect().Size;
            HudPosition = new Vector2(viewport.X - HudMargin - 120, HudMargin);
        }

        // Start hidden at full health
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;

        _timer += (float)delta;

        if (_timer >= ToggleInterval)
        {
            _timer -= ToggleInterval;
            _isScaledUp = !_isScaledUp;
            ApplyScale(_isScaledUp ? ScaleMultiplier : 1.0f);
        }
    }

    private static void ScaleFromCenter(
        TextureRect node,
        Vector2 baseScale,
        Vector2 basePos,
        float mult
    )
    {
        node.Scale = baseScale * mult;
        Vector2 center = basePos + node.Size * baseScale / 2f;
        node.Position = center - node.Size * baseScale * mult / 2f;
    }

    // Check if a segment index is active (has health)
    // Segments deplete from 0 upward: segment 0 is lost first, segment 7 last
    private bool IsSegmentActive(int i) => i >= (MaxHealth - _currentHealth);

    private void ApplyScale(float mult)
    {
        // Orange swirl: only scales when health > 0
        if (_currentHealth > 0)
            ScaleFromCenter(_orangeSwirl, _orangeSwirlBaseScale, _orangeSwirlBasePos, mult);

        // Gray swirl: scales when health > 0, static when dead
        if (_currentHealth > 0)
            ScaleFromCenter(_graySwirl, _graySwirlBaseScale, _graySwirlBasePos, mult);
        else
            ScaleFromCenter(_graySwirl, _graySwirlBaseScale, _graySwirlBasePos, 1.0f);

        for (int i = 0; i < 8; i++)
        {
            // Orange segments: only scale if active (visible)
            if (_orangeSegments[i].Visible)
                ScaleFromCenter(
                    _orangeSegments[i],
                    _orangeSegBaseScales[i],
                    _orangeSegBasePositions[i],
                    mult
                );

            // Gray segments: scale if HP is still active (behind orange), static if depleted
            if (IsSegmentActive(i))
                ScaleFromCenter(
                    _graySegments[i],
                    _graySegBaseScales[i],
                    _graySegBasePositions[i],
                    mult
                );
            else
                ScaleFromCenter(
                    _graySegments[i],
                    _graySegBaseScales[i],
                    _graySegBasePositions[i],
                    1.0f
                );
        }
    }

    /// <summary>
    /// Spawns the meter at a screen position (near Mario) and tweens it to the HUD corner.
    /// Call this when Mario takes damage from full health.
    /// </summary>
    public void SpawnAtScreenPosition(Vector2 screenPos)
    {
        // Place at Mario's screen position (offset to the right)
        GlobalPosition = screenPos;
        Visible = true;

        // Linger near Mario, then fly to HUD corner
        _flyTween?.Kill();
        _flyTween = CreateTween();
        _flyTween.TweenInterval(SpawnLingerDuration);
        _flyTween
            .TweenProperty(this, "global_position", HudPosition, FlyDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
    }

    /// <summary>
    /// Slides the meter off-screen upward, then hides it.
    /// </summary>
    public void HideForFullHealth()
    {
        if (!Visible)
            return;

        _flyTween?.Kill();
        _flyTween = CreateTween();
        Vector2 offScreen = new Vector2(GlobalPosition.X, -200);
        _flyTween
            .TweenProperty(this, "global_position", offScreen, FlyDuration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);
        _flyTween.TweenCallback(Callable.From(() => Visible = false));
    }

    public void SetHealth(int hp)
    {
        _currentHealth = Mathf.Clamp(hp, 0, MaxHealth);
        float mult = _isScaledUp ? ScaleMultiplier : 1.0f;

        // Orange swirl: hide when dead
        _orangeSwirl.Visible = _currentHealth > 0;

        for (int i = 0; i < 8; i++)
        {
            _orangeSegments[i].Visible = IsSegmentActive(i);

            if (IsSegmentActive(i))
            {
                ScaleFromCenter(
                    _orangeSegments[i],
                    _orangeSegBaseScales[i],
                    _orangeSegBasePositions[i],
                    mult
                );
                ScaleFromCenter(
                    _graySegments[i],
                    _graySegBaseScales[i],
                    _graySegBasePositions[i],
                    mult
                );
            }
            else
            {
                // Depleted: reset gray to base size, stays static
                ScaleFromCenter(
                    _graySegments[i],
                    _graySegBaseScales[i],
                    _graySegBasePositions[i],
                    1.0f
                );
            }
        }
    }

    public int GetHealth() => _currentHealth;
}
