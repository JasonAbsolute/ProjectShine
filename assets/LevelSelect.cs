using Godot;

/// <summary>
/// Level Select screen (SMS 242) -- the Player Select screen again, for levels: it stands in for a hub. One poster per entry in
/// <see cref="Levels"/>, built from the same CharacterPoster scene the Player Select uses (a LevelEntry hands its picture, name art and
/// stamp over as a poster entry), the same pointing hand, the same stamp-and-fade on a pick; then the level loads. The level's way back is
/// this screen: on a pick the screen's own path goes to GameData.LevelSelectScene, and a Shine's exit or the pause menu's Exit Area with no
/// destination of their own return here (the two secret courses' Shines name it outright). Coming back, the poster of the level just left is
/// the one under the hand, and the screen fades in from what the exit left behind (the Shine demo's white, the Exit Area's black).
///
/// The screen owns no art of its own -- every visual comes off a LevelEntry -- so adding a level is a resource change with no code edit.
/// </summary>
public partial class LevelSelect : Control
{
    [Export] public LevelEntry[] Levels = System.Array.Empty<LevelEntry>();

    /// <summary>Container the posters are added to. Defaults to a child named "Portraits".</summary>
    [Export] public NodePath PortraitsContainerPath = "Portraits";

    /// <summary>The poster scene instantiated once per level -- the Player Select's CharacterPoster.tscn, so the posters match.</summary>
    [Export] public PackedScene PosterScene;

    /// <summary>The "P1" pointing hand. Slides under whichever poster is highlighted.</summary>
    [Export] public NodePath SelectHandPath;

    /// <summary>Gap between the bottom of a poster and the top of the hand.</summary>
    [Export] public float SelectHandGap = 12f;

    /// <summary>Beat to admire the finished stamp before the level loads.</summary>
    [Export] public float StampHoldSeconds = 0.45f;

    /// <summary>Full-screen rect faded up once the stamp is painted, and faded away on arrival.</summary>
    [Export] public NodePath FadeOutPath = "FadeOut";

    /// <summary>Seconds to fade to black before the level loads. 0 skips the fade.</summary>
    [Export] public float FadeSeconds = 0.7f;

    /// <summary>Seconds of the fade in on arrival (the game's stage fade-in, startWipe(.., 0.4)). 0 = none.</summary>
    [Export] public float ArriveFadeSeconds = 0.4f;

    /// <summary>What the screen fades in from when nothing says otherwise: the Player Select leaves black behind it.</summary>
    [Export] public Color ArriveFrom = Colors.Black;

    /// <summary>The Shine demo's fade-out colour (TShineFader: (210, 210, 210)) -- a Shine's exit lands here on it; the fade in starts from it.</summary>
    [Export] public Color ShineExitColor = new(210f / 255f, 210f / 255f, 210f / 255f, 1f);

    /// <summary>Cancel (B / Escape) goes back to this scene; empty = no way back from here.</summary>
    [Export(PropertyHint.File, "*.tscn")] public string BackScene = "res://assets/CharacterSelect.tscn";

    private Container _portraits;
    private Control _selectHand;
    private ColorRect _fadeRect;
    private int _highlighted = -1;
    private bool _confirming;
    private readonly System.Collections.Generic.List<CharacterPoster> _buttons = new();

    public override void _Ready()
    {
        _portraits = GetNodeOrNull<Container>(PortraitsContainerPath);
        if (_portraits == null)
        {
            GD.PushError($"LevelSelect: no container at '{PortraitsContainerPath}'.");
            return;
        }
        if (!SelectHandPath.IsEmpty)
            _selectHand = GetNodeOrNull<Control>(SelectHandPath);
        if (!FadeOutPath.IsEmpty)
            _fadeRect = GetNodeOrNull<ColorRect>(FadeOutPath);

        BuildButtons();

        // the poster of the level just left (a Shine's exit, the Exit Area) is the one under the hand; else the first
        int start = 0;
        string from = GameData.Instance?.ReturnFromScene;
        if (!string.IsNullOrEmpty(from))
            for (int i = 0; i < Levels.Length; i++)
                if (Levels[i] != null && SmsReturnSpot.ScenePath(Levels[i].ScenePath) == from) { start = i; break; }
        if (_buttons.Count > 0)
        {
            start = Mathf.Clamp(start, 0, _buttons.Count - 1);
            _buttons[start].GrabFocus();
            Highlight(start);
        }

        // the fade in: from what the way here left on the screen -- FadeInFrom if a screen set it, the Shine demo's white after a Shine's exit,
        // else black (the Player Select's fade, the Exit Area's iris)
        Color arriveFrom = ArriveFrom;
        if (GameData.Instance != null)
        {
            if (GameData.Instance.FadeInFrom.HasValue) { arriveFrom = GameData.Instance.FadeInFrom.Value; GameData.Instance.FadeInFrom = null; }
            else if (!string.IsNullOrEmpty(from) && GameData.Instance.ReturnWithShine) arriveFrom = ShineExitColor;
            // the return is used up here: the next level starts plain, not at a return spot for the level he left (119's ReturnFromScene)
            GameData.Instance.ReturnFromScene = null;
            GameData.Instance.ReturnWithShine = false;
        }
        if (_fadeRect != null && ArriveFadeSeconds > 0f)
        {
            _fadeRect.Color = new Color(arriveFrom, 1f);
            _fadeRect.Visible = true;
            Tween fadeIn = CreateTween();
            fadeIn.TweenProperty(_fadeRect, "color:a", 0f, ArriveFadeSeconds);
            fadeIn.TweenCallback(Callable.From(() => { if (IsInstanceValid(_fadeRect)) { _fadeRect.Visible = false; _fadeRect.Color = new Color(0f, 0f, 0f, 0f); } }));
        }
    }

    private void BuildButtons()
    {
        foreach (var child in _portraits.GetChildren())
            child.QueueFree();
        _buttons.Clear();

        if (PosterScene == null)
        {
            GD.PushError("LevelSelect: PosterScene is not set.");
            return;
        }

        for (int i = 0; i < Levels.Length; i++)
        {
            var entry = Levels[i];
            if (entry == null)
                continue;

            if (PosterScene.Instantiate() is not CharacterPoster poster)
            {
                GD.PushError("LevelSelect: PosterScene's root is not a CharacterPoster.");
                return;
            }

            poster.Name = $"Portrait{i}";
            poster.SetEntry(entry.AsPosterEntry());

            int index = i; // capture per iteration, not the loop variable
            poster.Pressed += () => Confirm(index);
            poster.FocusEntered += () => Highlight(index);
            poster.MouseEntered += () => poster.GrabFocus();

            _portraits.AddChild(poster);
            _buttons.Add(poster);
        }
    }

    private void Highlight(int index)
    {
        if (index < 0 || index >= Levels.Length)
            return;
        _highlighted = index;

        // Containers lay out on the next frame, so the button's rect isn't final yet on the first call - defer the move rather than reading a stale size.
        if (_selectHand != null)
            CallDeferred(nameof(MoveHandTo), index);

        for (int i = 0; i < _buttons.Count; i++)
            _buttons[i].SetHighlighted(i == index);
    }

    /// <summary>Centre the hand under a poster, in the hand's own parent space.</summary>
    private void MoveHandTo(int index)
    {
        if (_selectHand == null || index < 0 || index >= _buttons.Count)
            return;

        var button = _buttons[index];
        _selectHand.GlobalPosition = new Vector2(
            button.GlobalPosition.X + (button.Size.X - _selectHand.Size.X) * 0.5f,
            button.GlobalPosition.Y + button.Size.Y + SelectHandGap
        );
        _selectHand.Visible = true;
    }

    private async void Confirm(int index)
    {
        if (_confirming || index < 0 || index >= Levels.Length)
            return;

        var entry = Levels[index];
        if (entry == null || !entry.Unlocked)
            return;

        string dest = SmsReturnSpot.ScenePath(entry.ScenePath);   // the editor saves a File export as uid://, resolved to the path
        if (dest == "" || !ResourceLoader.Exists(dest))
        {
            GD.PushError($"LevelSelect: '{entry.DisplayName}' has no level scene at '{entry.ScenePath}'.");
            return;
        }

        if (GameData.Instance != null)
            GameData.Instance.LevelSelectScene = SceneFilePath;   // the level's way back: a Shine's exit / the Exit Area with no destination of their own
        GD.Print($"LevelSelect: {entry.DisplayName} -> {dest}");

        // Let the stamp finish painting before the level swallows the screen.
        _confirming = true;
        float wait = _buttons[index].PlayStamp() + StampHoldSeconds;
        if (wait > 0f)
            await ToSignal(GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this))
            return;

        if (!await FadeOut())
            return;

        var err = GetTree().ChangeSceneToFile(dest);
        if (err != Error.Ok)
            GD.PushError($"LevelSelect: could not load '{dest}' ({err}).");
    }

    /// <summary>Fade the whole screen to black, so the next scene doesn't snap in over a poster still on screen. False = this node is gone.</summary>
    private async System.Threading.Tasks.Task<bool> FadeOut()
    {
        if (_fadeRect == null || FadeSeconds <= 0f)
            return true;
        _fadeRect.Color = new Color(0f, 0f, 0f, 0f);
        _fadeRect.Visible = true;
        Tween fade = CreateTween();
        fade.TweenProperty(_fadeRect, "color:a", 1f, FadeSeconds);
        await ToSignal(fade, Tween.SignalName.Finished);
        return IsInstanceValid(this);
    }

    /// <summary>Cancel: back to the Player Select (BackScene), behind the same fade.</summary>
    private async void Back()
    {
        if (_confirming || string.IsNullOrEmpty(BackScene))
            return;
        string dest = SmsReturnSpot.ScenePath(BackScene);
        if (dest == "" || !ResourceLoader.Exists(dest))
            return;
        _confirming = true;
        if (!await FadeOut())
            return;
        var err = GetTree().ChangeSceneToFile(dest);
        if (err != Error.Ok)
            GD.PushError($"LevelSelect: could not load '{dest}' ({err}).");
    }

    /// <summary>Left/right move the focus (the container's own navigation), accept confirms, cancel goes back -- a controller works without extra wiring.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_buttons.Count == 0 || !@event.IsPressed() || @event.IsEcho())
            return;

        if (@event.IsActionPressed("ui_accept") && _highlighted >= 0)
        {
            Confirm(_highlighted);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ui_cancel"))
        {
            Back();
            GetViewport().SetInputAsHandled();
        }
    }
}
