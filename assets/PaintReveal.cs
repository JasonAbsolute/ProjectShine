using Godot;

/// <summary>
/// Paints a stroke-order map on over time — the "M" stamped onto a poster when
/// a character is picked.
///
/// The texture comes from tools/bake_stroke_order.py. Because the reveal is
/// driven by one shader uniform rather than a frame flipbook, the letter and
/// the colour are both just data: give it a different map for a different
/// letter, a different <see cref="PaintColor"/> for a different character.
/// </summary>
[GlobalClass]
public partial class PaintReveal : TextureRect
{
    [Signal] public delegate void FinishedEventHandler();

    /// <summary>Seconds for the stroke to draw from nothing to complete.</summary>
    [Export] public float Duration = 0.8f;

    /// <summary>Beat before the stroke starts, so the pick registers first.</summary>
    [Export] public float Delay = 0.08f;

    [Export] public Color PaintColor = new(0.933f, 0.016f, 0f, 1f);

    private ShaderMaterial _mat;
    private float _time;
    private bool _playing;

    public float TotalSeconds => Delay + Duration;

    public override void _Ready()
    {
        // Every poster instance shares the scene's ShaderMaterial by default, so
        // without a per-instance copy all four would animate as one.
        if (Material is ShaderMaterial shared)
        {
            _mat = (ShaderMaterial)shared.Duplicate();
            Material = _mat;
        }
        else
        {
            GD.PushWarning($"PaintReveal '{Name}': no ShaderMaterial, nothing will draw.");
        }

        Reset();
    }

    /// <summary>Hides the stamp and rewinds it to un-drawn.</summary>
    public void Reset()
    {
        _playing = false;
        _time = 0f;
        SetProcess(false);
        Visible = false;
        Apply(0f);
    }

    /// <summary>Draws the stroke on. Returns how long that will take.</summary>
    public float Play()
    {
        _time = 0f;
        _playing = true;
        Visible = true;
        Apply(0f);
        SetProcess(true);
        return TotalSeconds;
    }

    /// <summary>Jumps straight to fully painted, skipping the animation.</summary>
    public void Complete()
    {
        _playing = false;
        SetProcess(false);
        Visible = true;
        Apply(1f);
    }

    public override void _Process(double delta)
    {
        if (!_playing)
            return;

        _time += (float)delta;

        float t = Duration <= 0f ? 1f : Mathf.Clamp((_time - Delay) / Duration, 0f, 1f);
        Apply(t);

        if (_time >= TotalSeconds)
        {
            _playing = false;
            SetProcess(false);
            EmitSignal(SignalName.Finished);
        }
    }

    private void Apply(float progress)
    {
        if (_mat == null)
            return;

        _mat.SetShaderParameter("progress", progress);
        _mat.SetShaderParameter("paint_color", PaintColor);
    }
}
