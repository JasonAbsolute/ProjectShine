using Godot;
using System;

/// <summary>The talk window the director opens on an NPC (TMarDirector::movement_game -> unkB0->openTalkWindow): the game's is a
/// white rounded speech bubble with a tail toward the speaker's head, the text typed in letter by letter, A to finish the page and
/// A again for the next, closing on the last. The game's BMG message files are not ported: the pages are the NPC's exported
/// Lines, plain strings. Styling lives in TalkWindow.tscn (the Box's StyleBoxFlat, the Text's font / size / colour): restyle it in
/// the editor, nothing here is hard-coded but the typing speed and the keys. Input is edge-detected here (pass 59: a
/// _Process-side IsActionJustPressed misses physics-stamped presses).</summary>
public partial class SmsTalkWindow : CanvasLayer
{
    [Export] public float CharsPerSecond = 45f;          // the typewriter; the game shows ~1-2 characters a frame
    [Export] public float TailLength = 60f;               // pixels the tail reaches toward the speaker
    [Export] public float NextBlinkHz = 2f;
    [Export] public string AdvanceAction = "button_a";
    [Export] public NodePath BoxPath = "Root/Box";
    [Export] public NodePath TextPath = "Root/Box/Margin/Text";
    [Export] public NodePath NextPath = "Root/Box/Next";
    [Export] public NodePath TailPath = "Root/Tail";

    public bool IsOpen { get; private set; }
    public int Page { get; private set; }
    public bool PageComplete => _text != null && _text.VisibleCharacters < 0;

    private Control _box; private RichTextLabel _text; private Control _next; private Polygon2D _tail;
    private string[] _pages = Array.Empty<string>();
    private Node3D _speaker; private float _speakerHeight;
    private Action _onClosed;
    private float _typed; private bool _lastPressed; private float _blink;

    public override void _Ready()
    {
        _box = GetNodeOrNull<Control>(BoxPath);
        _text = GetNodeOrNull<RichTextLabel>(TextPath);
        _next = GetNodeOrNull<Control>(NextPath);
        _tail = GetNodeOrNull<Polygon2D>(TailPath);
        Visible = false;
        _lastPressed = Input.IsActionPressed(AdvanceAction);
    }

    public void Open(string[] pages, Node3D speaker, float speakerHeightMetres, Action onClosed)
    {
        _pages = pages == null || pages.Length == 0 ? new[] { "..." } : pages;
        _speaker = speaker; _speakerHeight = speakerHeightMetres; _onClosed = onClosed;
        Page = 0; IsOpen = true; Visible = true;
        _lastPressed = Input.IsActionPressed(AdvanceAction); // the press that opened the talk must not also advance it
        ShowPage();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; Visible = false;
        var cb = _onClosed; _onClosed = null; _speaker = null;
        cb?.Invoke();
    }

    private void ShowPage()
    {
        if (_text == null) return;
        _text.Text = _pages[Page];
        _text.VisibleCharacters = 0;
        _typed = 0f;
        if (_next != null) _next.Visible = false;
    }

    /// <summary>A: a typing page completes; a complete page turns; the last page closes.</summary>
    public void Advance()
    {
        if (!IsOpen || _text == null) return;
        if (_text.VisibleCharacters >= 0 && _text.VisibleCharacters < _text.GetTotalCharacterCount()) { _text.VisibleCharacters = -1; return; }
        if (Page + 1 < _pages.Length) { Page++; ShowPage(); return; }
        Close();
    }

    public override void _Process(double delta)
    {
        if (!IsOpen) return;
        bool pressed = Input.IsActionPressed(AdvanceAction);
        bool just = pressed && !_lastPressed;
        _lastPressed = pressed;
        if (_text != null && _text.VisibleCharacters >= 0)
        {
            _typed += (float)delta * CharsPerSecond;
            int total = _text.GetTotalCharacterCount();
            int n = Mathf.Min(total, (int)_typed);
            _text.VisibleCharacters = n >= total ? -1 : n;
        }
        if (_next != null)
        {
            _blink += (float)delta * NextBlinkHz;
            _next.Visible = _text != null && _text.VisibleCharacters < 0 && (_blink % 1f) < 0.6f;
        }
        if (just) Advance();
        UpdateTail();
    }

    /// <summary>The bubble's tail: from the box's top edge toward the speaker's head on screen (clamped to a short stub).</summary>
    private void UpdateTail()
    {
        if (_tail == null || _box == null) return;
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null || _speaker == null || !IsInstanceValid(_speaker)) { _tail.Visible = false; return; }
        Vector3 head = _speaker.GlobalPosition + Vector3.Up * _speakerHeight;
        if (cam.IsPositionBehind(head)) { _tail.Visible = false; return; }
        Vector2 p = cam.UnprojectPosition(head);
        Rect2 r = _box.GetGlobalRect();
        Vector2 baseC = new Vector2(Mathf.Clamp(p.X, r.Position.X + 60f, r.End.X - 60f), r.Position.Y + 2f);
        Vector2 dir = p - baseC;
        if (dir.Y > -10f) { _tail.Visible = false; return; }   // the speaker is below the box: no tail
        Vector2 tip = baseC + dir.Normalized() * Mathf.Min(dir.Length(), TailLength);
        _tail.Polygon = new[] { baseC + new Vector2(-22f, 0f), baseC + new Vector2(22f, 0f), tip };
        _tail.Visible = true;
    }
}
