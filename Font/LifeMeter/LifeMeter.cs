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

    // ---- Air mode (SMS: under water the life meter shows Mario's air, in blue) ----
    // GCConsole2 updateLifeMeterColors(): J2D white 0x00FFFF / black 0x003CFF; at 3 segments or less white 0x0010FF. The J2D
    // picture maps the art's intensity between the two colours; here a canvas_item shader does the same with the orange art.
    private ShaderMaterial _airMaterial;
    private bool _airMode;
    public bool AirMode => _airMode;

    // The game's meter under water (ref/WaterLifeMeter.png): azure bodies #08C8F8, shading to #0898F8, highlights to
    // #08F8F8, navy outlines #0C2C78. The port's art is yellow/orange with black outlines, so the remap keys on luminance,
    // where its gradients live (outline 0.02, swirl 0.66-0.77, segments 0.82-0.87, highlights 0.97), through this ramp.
    private static readonly Color[] AirRamp =
    {
        new Color(0.04f, 0.12f, 0.44f), // 0.00 outline (navy)
        new Color(0.05f, 0.31f, 0.75f), // 0.35 edge
        new Color(0.03f, 0.60f, 0.97f), // 0.66 shading
        new Color(0.03f, 0.78f, 0.97f), // 0.80 body
        new Color(0.03f, 0.91f, 0.97f), // 0.90 light side
        new Color(0.38f, 0.97f, 1.00f), // 1.00 highlight
    };
    private static readonly float[] AirRampStops = { 0.00f, 0.35f, 0.66f, 0.80f, 0.90f, 1.00f };
    private static readonly Color AirLowBlue = new Color(0.06f, 0.20f, 1.00f);     // the game's low-air "white" 0x0010FF
    private static readonly Color AirBlobModulate = new Color(0.02f, 0.16f, 0.48f, 0.72f);
    private TextureRect _lifeText;
    private TextureRect _paintBlob;
    private Color _paintBlobModulate;
    private bool _paintBlobModulateSaved;

    private void SetAirTint(bool on, int units)
    {
        if (on && _airMaterial == null)
        {
            var shader = new Shader
            {
                Code = "shader_type canvas_item;\n"
                     + "uniform vec3 c0 = vec3(0.04, 0.12, 0.44);\n"
                     + "uniform vec3 c1 = vec3(0.05, 0.31, 0.75);\n"
                     + "uniform vec3 c2 = vec3(0.03, 0.60, 0.97);\n"
                     + "uniform vec3 c3 = vec3(0.03, 0.78, 0.97);\n"
                     + "uniform vec3 c4 = vec3(0.03, 0.91, 0.97);\n"
                     + "uniform vec3 c5 = vec3(0.38, 0.97, 1.00);\n"
                     + "uniform float s1 = 0.35;\n"
                     + "uniform float s2 = 0.66;\n"
                     + "uniform float s3 = 0.80;\n"
                     + "uniform float s4 = 0.90;\n"
                     + "uniform vec3 low_blue = vec3(0.06, 0.20, 1.0);\n"
                     + "uniform float low = 0.0;\n"
                     + "void fragment() {\n"
                     + "    vec4 t = texture(TEXTURE, UV);\n"
                     + "    float l = dot(t.rgb, vec3(0.299, 0.587, 0.114));\n"
                     + "    vec3 c;\n"
                     + "    if (l < s1) c = mix(c0, c1, l / s1);\n"
                     + "    else if (l < s2) c = mix(c1, c2, (l - s1) / (s2 - s1));\n"
                     + "    else if (l < s3) c = mix(c2, c3, (l - s2) / (s3 - s2));\n"
                     + "    else if (l < s4) c = mix(c3, c4, (l - s3) / (s4 - s3));\n"
                     + "    else c = mix(c4, c5, (l - s4) / (1.0 - s4));\n"
                     + "    c = mix(c, low_blue, low * 0.6 * smoothstep(0.4, 0.6, l));\n"
                     + "    COLOR = vec4(c, COLOR.a);\n"
                     + "}\n",
            };
            _airMaterial = new ShaderMaterial { Shader = shader };
            for (int i = 0; i < AirRamp.Length; i++)
                _airMaterial.SetShaderParameter($"c{i}", new Vector3(AirRamp[i].R, AirRamp[i].G, AirRamp[i].B));
            for (int i = 1; i <= 4; i++)
                _airMaterial.SetShaderParameter($"s{i}", AirRampStops[i]);
            _airMaterial.SetShaderParameter("low_blue", new Vector3(AirLowBlue.R, AirLowBlue.G, AirLowBlue.B));
            _lifeText = GetNodeOrNull<TextureRect>("LifeText");
            _paintBlob = GetNodeOrNull<TextureRect>("PaintBlobBG");
        }
        _airMode = on;
        if (on)
            _airMaterial.SetShaderParameter("low", units <= 3 ? 1.0f : 0.0f); // 3 or less: the game's white becomes 0x0010FF
        Material m = on ? _airMaterial : null;
        _orangeSwirl.Material = m;
        for (int i = 0; i < 8; i++)
            _orangeSegments[i].Material = m;
        if (_lifeText != null)
            _lifeText.Material = m;
        if (_paintBlob != null)
        {
            if (!_paintBlobModulateSaved)
            {
                _paintBlobModulate = _paintBlob.Modulate;
                _paintBlobModulateSaved = true;
            }
            _paintBlob.Modulate = on ? AirBlobModulate : _paintBlobModulate;
        }
    }

    /// <summary>The meter appears at its HUD place showing <paramref name="units"/> of air in blue (startAppearLife(1)).</summary>
    public void ShowAir(int units)
    {
        _flyTween?.Kill();
        GlobalPosition = HudPosition;
        Visible = true;
        SetAirTint(true, units);
        SetHealth(units);
    }

    public void SetAir(int units)
    {
        SetAirTint(true, units);
        SetHealth(units);
    }

    /// <summary>Back to HP when he surfaces: shown if it is not full, gone if it is.</summary>
    public void EndAir(int hp)
    {
        SetAirTint(false, hp);
        SetHealth(hp);
        if (hp >= MaxHealth)
            HideForFullHealth();
    }
}
