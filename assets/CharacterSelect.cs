using Godot;

/// <summary>
/// Player Select screen. Builds one button per entry in <see cref="Characters"/>,
/// stores the pick on GameData, then loads <see cref="LevelToLoad"/>.
///
/// The screen owns no art of its own — every visual comes off a CharacterEntry —
/// so adding a fifth character is a resource change with no code edit.
/// </summary>
public partial class CharacterSelect : Control
{
    [Export] public CharacterEntry[] Characters = System.Array.Empty<CharacterEntry>();

    /// <summary>Scene to load once a character is confirmed.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string LevelToLoad = "res://Levels/Rico4Secrect/Skybox/Rico4Secret.tscn";

    /// <summary>Container the portrait buttons are added to. Defaults to a child named "Portraits".</summary>
    [Export] public NodePath PortraitsContainerPath = "Portraits";

    /// <summary>
    /// The poster scene instantiated once per character. Its layout — frame,
    /// name and head placement — is authored in CharacterPoster.tscn, so posters
    /// are positioned in the editor rather than from here.
    /// </summary>
    [Export] public PackedScene PosterScene;

    /// <summary>The "P1" pointing hand. Slides under whichever portrait is highlighted.</summary>
    [Export] public NodePath SelectHandPath;

    /// <summary>Gap between the bottom of a portrait and the top of the hand.</summary>
    [Export] public float SelectHandGap = 12f;

    private Container _portraits;
    private Control _selectHand;
    private int _highlighted = -1;
    private readonly System.Collections.Generic.List<CharacterPoster> _buttons = new();

    public override void _Ready()
    {
        _portraits = GetNodeOrNull<Container>(PortraitsContainerPath);
        if (_portraits == null)
        {
            GD.PushError($"CharacterSelect: no container at '{PortraitsContainerPath}'.");
            return;
        }
        if (!SelectHandPath.IsEmpty)
            _selectHand = GetNodeOrNull<Control>(SelectHandPath);
        if (!FadeOutPath.IsEmpty)
            _fadeRect = GetNodeOrNull<ColorRect>(FadeOutPath);

        BuildButtons();

        if (_buttons.Count > 0)
        {
            _buttons[0].GrabFocus();
            Highlight(0);
        }
    }

    private void BuildButtons()
    {
        foreach (var child in _portraits.GetChildren())
            child.QueueFree();
        _buttons.Clear();

        if (PosterScene == null)
        {
            GD.PushError("CharacterSelect: PosterScene is not set.");
            return;
        }

        for (int i = 0; i < Characters.Length; i++)
        {
            var entry = Characters[i];
            if (entry == null)
                continue;

            if (PosterScene.Instantiate() is not CharacterPoster poster)
            {
                GD.PushError("CharacterSelect: PosterScene's root is not a CharacterPoster.");
                return;
            }

            poster.Name = $"Portrait{i}";
            poster.SetEntry(entry);

            int index = i; // capture per iteration, not the loop variable
            poster.Pressed += () => Confirm(index);
            poster.FocusEntered += () => Highlight(index);
            poster.MouseEntered += () => poster.GrabFocus();

            _portraits.AddChild(poster);
            _buttons.Add(poster);
        }
    }

    /// <summary>Beat to admire the finished stamp before the level loads.</summary>
    [Export] public float StampHoldSeconds = 0.45f;

    /// <summary>Full-screen rect faded up once the stamp is painted.</summary>
    [Export] public NodePath FadeOutPath = "FadeOut";

    /// <summary>Seconds to fade to black. 0 skips the fade.</summary>
    [Export] public float FadeSeconds = 0.7f;

    private ColorRect _fadeRect;

    private bool _confirming;

    private void Highlight(int index)
    {
        if (index < 0 || index >= Characters.Length)
            return;
        _highlighted = index;

        // Containers lay out on the next frame, so the button's rect isn't final
        // yet on the first call - defer the move rather than reading a stale size.
        if (_selectHand != null)
            CallDeferred(nameof(MoveHandTo), index);

        for (int i = 0; i < _buttons.Count; i++)
            _buttons[i].SetHighlighted(i == index);
    }

    /// <summary>Centre the hand under a portrait, in the hand's own parent space.</summary>
    private void MoveHandTo(int index)
    {
        if (_selectHand == null || index < 0 || index >= _buttons.Count)
            return;

        var button = _buttons[index];
        var target = new Vector2(
            button.GlobalPosition.X + (button.Size.X - _selectHand.Size.X) * 0.5f,
            button.GlobalPosition.Y + button.Size.Y + SelectHandGap
        );
        _selectHand.GlobalPosition = target;
        _selectHand.Visible = true;
    }

    private async void Confirm(int index)
    {
        if (_confirming || index < 0 || index >= Characters.Length)
            return;

        var entry = Characters[index];
        if (entry == null || !entry.Unlocked)
            return;

        if (entry.PlayerScene == null)
        {
            GD.PushError($"CharacterSelect: '{entry.DisplayName}' has no PlayerScene set.");
            return;
        }

        if (GameData.Instance == null)
        {
            GD.PushError("CharacterSelect: GameData autoload missing.");
            return;
        }

        GameData.Instance.SelectedCharacter = entry;
        GD.Print($"CharacterSelect: {entry.DisplayName} -> {LevelToLoad}");

        // Let the stamp finish painting before the level swallows the screen.
        _confirming = true;
        float wait = _buttons[index].PlayStamp() + StampHoldSeconds;
        if (wait > 0f)
            await ToSignal(GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this))
            return;

        // Fade the whole screen once the tag is painted, so the level doesn't
        // snap in over a poster that's still on screen.
        if (_fadeRect != null && FadeSeconds > 0f)
        {
            _fadeRect.Color = new Color(_fadeRect.Color, 0f);
            _fadeRect.Visible = true;

            Tween fade = CreateTween();
            fade.TweenProperty(_fadeRect, "color:a", 1f, FadeSeconds);
            await ToSignal(fade, Tween.SignalName.Finished);

            if (!IsInstanceValid(this))
                return;
        }

        var err = GetTree().ChangeSceneToFile(LevelToLoad);
        if (err != Error.Ok)
            GD.PushError($"CharacterSelect: could not load '{LevelToLoad}' ({err}).");
    }

    /// <summary>Left/right to move, accept to confirm — so a controller works without extra wiring.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_buttons.Count == 0 || !@event.IsPressed() || @event.IsEcho())
            return;

        if (@event.IsActionPressed("ui_accept") && _highlighted >= 0)
        {
            Confirm(_highlighted);
            GetViewport().SetInputAsHandled();
        }
    }
}
