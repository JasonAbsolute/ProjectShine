using Godot;

/// <summary>
/// Feeds a Label's height into TitleGradient.gdshader so the gradient spans the
/// text rather than restarting per glyph. Kept as its own script so the shader
/// works on any Label without the owning screen having to know about it.
/// </summary>
[Tool]
public partial class GradientLabel : Label
{
    private const string HeightParam = "control_height";

    public override void _Ready() => PushHeight();

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            PushHeight();
    }

    private void PushHeight()
    {
        if (Material is ShaderMaterial mat && Size.Y > 0f)
            mat.SetShaderParameter(HeightParam, Size.Y);
    }
}
