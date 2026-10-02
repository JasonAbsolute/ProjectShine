using Godot;

/// <summary>
/// The console's big text -- TConsoleStr (GC2D/ConsoleStr.cpp) over the HUD: the "TOO BAD!" of a miss. The letters are big_tx_1.blo's
/// panes ms00..ms06 (78 x 78 at y 338 on its 600 x 480 screen, textures big_tx_t/o/o/b/a/d/ex), laid out here as children of
/// <see cref="Miss"/>. startAppearMiss (315) then processMiss (554) choreograph them, one frame per console frame (120 Hz ticks in this
/// port, pass 38; the game's TConsoleStr counts 0.5 a frame and doubles it): letter i starts at frame i x 10 -- a quadratic drop from
/// 270 above to 30 below its place over 60 frames (TBoundPane's bezier with the control point at the start), one full turn on the way
/// (rotation (start - frame) x 6 degrees), alpha +12 a frame -- then at +60 a bounce 110 up over 40 (the control point at the end),
/// at +100 back to its place over 40, at +300 down and out over 30, hidden past +360; the stage has long reloaded by then. The sparkle
/// each letter makes as it appears (emitter 0x1F9) is not ported. The director starts this the tick after MARIO_FLAG_GAME_OVER.
/// </summary>
public partial class SmsConsoleStr : CanvasLayer
{
    /// <summary>The TOO BAD! group: its TextureRect children are the letters, at their blo positions.</summary>
    [Export] public Control Miss;
    /// <summary>The blo's screen is 600 x 480; the group is placed about its centre on the viewport.</summary>
    [Export] public Vector2 LayoutCenter = new Vector2(300f, 240f);
    /// <summary>1 = the blo's 480 lines on the window's height.</summary>
    [Export] public float Size = 1.0f;

    private TextureRect[] _letters = System.Array.Empty<TextureRect>();
    private Vector2[] _home = System.Array.Empty<Vector2>();
    private float _frame = -1f;

    public bool MissRunning => _frame >= 0f;

    public override void _Ready()
    {
        if (Miss == null)
        {
            GD.PushWarning($"[SMS console] {GetPath()}: the scene's Miss export is not set (see assets/Sms/Console/ConsoleStr.tscn)");
            return;
        }
        var list = new System.Collections.Generic.List<TextureRect>();
        foreach (Node c in Miss.GetChildren())
            if (c is TextureRect t)
                list.Add(t);
        _letters = list.ToArray();
        _home = new Vector2[_letters.Length];
        for (int i = 0; i < _letters.Length; i++)
        {
            _home[i] = _letters[i].Position;
            _letters[i].PivotOffset = _letters[i].Size * 0.5f; // the panes' base position is their centre (blo base 4)
            _letters[i].Visible = false;
        }
        Miss.Visible = false;
    }

    /// <summary>TConsoleStr::startAppearMiss: the letters hidden and clear, the choreography from frame 0.</summary>
    public void StartAppearMiss()
    {
        _frame = 0f;
        foreach (TextureRect t in _letters)
        {
            t.Visible = false;
            t.Modulate = new Color(1f, 1f, 1f, 0f);
            t.Rotation = 0f;
        }
        Miss.Visible = true;
    }

    public void Clear()
    {
        _frame = -1f;
        foreach (TextureRect t in _letters)
            t.Visible = false;
        if (Miss != null)
            Miss.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (Miss == null)
            return;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float s = vp.Y / 480f * Size;
        Miss.Scale = new Vector2(s, s);
        Miss.Position = vp * 0.5f - LayoutCenter * s;
        if (_frame < 0f)
            return;
        _frame += (float)delta * 120f * SmsHud.Anim;
        ProcessMiss(_frame);
    }

    private void ProcessMiss(float f)
    {
        bool anyVisible = false;
        for (int i = 0; i < _letters.Length; i++)
        {
            TextureRect t = _letters[i];
            float start = i * 10f;
            if (f < start || f > start + 360f)
            {
                t.Visible = false;
                continue;
            }
            anyVisible = true;
            t.Visible = true;
            float since = f - start;
            Vector2 off;
            float rotDeg = 0f;
            if (since < 60f)
            {
                off = SmsHud.Bezier(new Vector2(0f, -270f), new Vector2(0f, -270f), new Vector2(0f, 30f), since / 60f);
                rotDeg = -since * 6f; // (i * 10 - param) * 6: a full turn on the way down
            }
            else if (since < 100f)
                off = SmsHud.Bezier(new Vector2(0f, 30f), new Vector2(0f, -80f), new Vector2(0f, -80f), (since - 60f) / 40f);
            else if (since < 300f)
                off = SmsHud.Bezier(new Vector2(0f, -80f), new Vector2(0f, -80f), Vector2.Zero, (since - 100f) / 40f);
            else
                off = SmsHud.Bezier(Vector2.Zero, Vector2.Zero, new Vector2(0f, 150f), (since - 300f) / 30f);
            t.Position = _home[i] + off;
            t.Rotation = Mathf.DegToRad(rotDeg);
            float a = Mathf.Min(1f, since * 12f / 255f);
            t.Modulate = new Color(1f, 1f, 1f, a);
        }
        if (!anyVisible && f > 360f)
            Clear();
    }
}
