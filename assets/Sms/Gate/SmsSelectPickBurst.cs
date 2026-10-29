using Godot;

/// <summary>
/// The episode select's pick burst -- TSelectMenu::perform MENU_INPUT_LOOP on A (SelectMenu.cpp 714-727), SMS pass 127b: five 2D emitters at
/// the screen's centre (300, 244): ms_2d_scsel_on_d (the glow pop, at once), then from frame 30 (their BEM1 start) _c (the white flood), _b
/// (the rainbow glows) and _a (the senko streaks) -- or _a2 (the red / green / blue lines) in place of _a when the picked episode is cleared.
/// SelectPickBurst.tscn (gen_fx127.py) carries them; <see cref="SmsBurst"/> fires them; the node frees itself after the longest life.
/// </summary>
public partial class SmsSelectPickBurst : Node2D
{
    /// <summary>The game's start frame of _c / _b / _a / _a2 (30 .jpa frames = 0.5 s).</summary>
    [Export] public float LateStartSeconds = 0.5f;

    private bool _cleared;
    private float _t;
    private bool _lateFired;

    /// <summary>startClose's burst: cleared = the picked episode has its shine (the lines instead of the streaks).</summary>
    public void SmsBurst(bool cleared)
    {
        _cleared = cleared;
        _t = 0f; _lateFired = false;
        Fire("Pop");
        SetProcess(true);
    }

    private void Fire(string name)
    {
        var p = GetNodeOrNull<GpuParticles2D>(name);
        if (p == null) return;
        p.Restart();
        p.Emitting = true;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (!_lateFired && _t >= LateStartSeconds)
        {
            _lateFired = true;
            Fire("Flood"); Fire("Glows"); Fire(_cleared ? "Lines" : "Streaks");
        }
        if (_t > LateStartSeconds + 3.6f) QueueFree();   // the flood's 200 frames and the glows' 160 are over
    }
}
