using Godot;

/// <summary>
/// Generic message banner — the "Collect 8 red coins..." style popup used by
/// SMS for switches, secret-course rules, and readable signs. Deliberately
/// knows nothing about what triggered it (see RedCoinSwitch for one caller);
/// anything that wants to show the player a line or two of text can spawn one
/// of these instead of building its own.
///
/// Set <see cref="Text"/> BEFORE this enters the tree (right after
/// Instantiate, same convention as GoTextPopup.PopupText) — it's read once in
/// _Ready().
///
/// Scene structure expected (see SignPopup.tscn):
///   SignPopup (this script, Control, full-rect, mouse-ignore)
///   └─ Banner (TextureRect — the parchment/scroll graphic; tilted, semi-transparent)
///        └─ Line1..Line7 (Label per ruled line baked into the banner texture;
///             inherit Banner's tilt since they're children)
///
/// Text is split on '\n' and each segment goes verbatim onto the next ruled
/// line (Line1, Line2, ...) so it sits ON the texture's printed lines instead
/// of free-floating — SignpopTextBox.png has 7 lines, so up to 7 segments are
/// used; a blank segment (e.g. the gap before "GOOD LUCK!") just leaves that
/// line empty. Extra segments beyond 7 are silently dropped.
/// </summary>
public partial class SignPopup : Control
{
    [Export(PropertyHint.MultilineText)]
    public string Text = "";

    [Export] public float FadeInDuration = 0.25f;
    [Export] public float FadeOutDuration = 0.25f;

    /// <summary>How long the sign stays up (after fading in) before it fades
    /// itself back out automatically. 0 = stays until Dismiss() is called
    /// externally or the player presses button_a (see DismissOnButtonA).</summary>
    [Export] public float HoldDuration = 4.0f;

    /// <summary>Same "press A to close" convention as SMS message signs.</summary>
    [Export] public bool DismissOnButtonA = true;

    [Export] public float PromptPulseHz = 1.8f;
    [Export] public float PromptMinScale = 0.72f;

    /// <summary>Fired the instant dismissal begins (button_a pressed or
    /// Dismiss() called externally) — BEFORE the fade-out tween runs. Callers
    /// that need to react to "player acknowledged this sign" (e.g. starting a
    /// challenge timer only once they've read it) should hook this rather than
    /// waiting on the node's tree-exit, since the fade-out is just cosmetic.</summary>
    [Signal] public delegate void DismissedEventHandler();

    private const int LineCount = 7;
    private Label[] _lines;
    private bool _dismissing = false;
    private Control _prompt;
    private Control _promptDot;
    private float _promptPulseTime = 0f;

    public override void _Ready()
    {
        _lines = new Label[LineCount];
        for (int i = 0; i < LineCount; i++)
            _lines[i] = GetNodeOrNull<Label>($"Banner/Line{i + 1}");

        _prompt = GetNodeOrNull<Control>("AcknowledgePrompt");
        _promptDot = GetNodeOrNull<Control>("AcknowledgePrompt/Dot");
        var banner = GetNodeOrNull<CanvasItem>("Banner");
        if (_prompt != null)
        {
            _prompt.Visible = DismissOnButtonA;
            float bannerAlpha = banner?.Modulate.A ?? 1f;
            _prompt.Modulate = new Color(
                _prompt.Modulate.R,
                _prompt.Modulate.G,
                _prompt.Modulate.B,
                bannerAlpha
            );
        }
        if (_promptDot != null)
            _promptDot.Modulate = Colors.White;

        string[] segments = Text.Split('\n');
        for (int i = 0; i < LineCount; i++)
        {
            if (_lines[i] == null)
                continue;
            _lines[i].Text = i < segments.Length ? segments[i] : "";
        }

        Modulate = new Color(1, 1, 1, 0);

        var t = CreateTween();
        t.TweenProperty(this, "modulate:a", 1f, FadeInDuration);

        if (HoldDuration > 0f)
        {
            var hold = CreateTween();
            hold.TweenInterval(FadeInDuration + HoldDuration);
            hold.TweenCallback(Callable.From(Dismiss));
        }
    }

    public override void _Process(double delta)
    {
        if (_prompt == null || !_prompt.Visible || _promptDot == null)
            return;

        _promptPulseTime += (float)delta;
        float wave = 0.5f + 0.5f * Mathf.Sin(_promptPulseTime * PromptPulseHz * Mathf.Tau);
        float pulseScale = Mathf.Lerp(PromptMinScale, 1.0f, wave);
        _promptDot.Scale = Vector2.One * pulseScale;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!DismissOnButtonA || _dismissing)
            return;
        if (@event.IsActionPressed("button_a"))
        {
            Dismiss();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Dismiss()
    {
        if (_dismissing)
            return;
        _dismissing = true;
        EmitSignal(SignalName.Dismissed);

        var t = CreateTween();
        t.TweenProperty(this, "modulate:a", 0f, FadeOutDuration);
        t.TweenCallback(Callable.From(FinishAndFree));
    }

    private void FinishAndFree()
    {
        if (GetParent() is CanvasLayer layer)
            layer.QueueFree();
        else
            QueueFree();
    }
}
