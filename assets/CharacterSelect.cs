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

    /// <summary>251c (Jaysonn: "Give the Player Select the same two sounds"): the player that clicks when the hand moves to another character -- the talk
    /// balloon's yes / no cursor, MSD_SE_SY_E3_MENU_CURSOR (Sfx/Move). Swap its stream for another System sound (SELECT_COMMON is the yes / no pick). Empty = silent.</summary>
    [Export] public NodePath MoveSoundPath = "Sfx/Move";

    /// <summary>251c: the player for the pick -- the sound as a talk box comes up, MSD_SE_SY_TALK_MODE_IN (Sfx/Confirm). TALK_OBI (the bands rolling
    /// out, the text showing) and SY_2D_IN (the save card's window) are in System/ for the knob. Empty = silent.</summary>
    [Export] public NodePath ConfirmSoundPath = "Sfx/Confirm";

    /// <summary>Put the two players on the Sfx bus at the game's volume for their sound (SmsActorSound.LevelDb by the stream's file name); off = as set on the nodes.</summary>
    [Export] public bool SoundsAtGameLevel = true;
    /// <summary>255 (Jaysonn: "A should select whatever the cursor is on and the control stick should move it left and right"): the pad's own actions
    /// on this screen. Godot's ui_accept carries only Enter / Space and ui_cancel only Escape, so no pad button confirmed; the stick and the D-pad
    /// reach the hand through Godot's focus navigation (ui_left / ui_right carry axis 0 and the D-pad) and, when that did not take the event
    /// (no poster focused, an event from the adapter's physics pump), through this screen's own handler. The left stick past StickThreshold moves
    /// the hand one poster (once per push: it must come back inside StickRelease first).</summary>
    [Export] public bool PadControls = true;
    [Export(PropertyHint.Range, "0.2,0.9,0.05")] public float StickThreshold = 0.5f;
    [Export(PropertyHint.Range, "0.0,0.6,0.05")] public float StickRelease = 0.3f;
    /// <summary>A build marker for the drives (read in the built DLL before a run).</summary>
    public const string BuildMarker = "SMS255_SELECT_PAD_A";

    private AudioStreamPlayer MoveSound, ConfirmSound;
    private int _stickLatch;   // the left stick's last push the hand answered: -1 left, +1 right, 0 centred (ready)

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

        // 251c: the two sounds on the Sfx bus at the game's volume for them (the sound table: 127 for the yes / no cursor, 100 for talk mode's start)
        MoveSound = MoveSoundPath.IsEmpty ? null : GetNodeOrNull<AudioStreamPlayer>(MoveSoundPath);
        ConfirmSound = ConfirmSoundPath.IsEmpty ? null : GetNodeOrNull<AudioStreamPlayer>(ConfirmSoundPath);
        foreach (var p in new[] { MoveSound, ConfirmSound })
        {
            if (p == null) continue;
            p.Bus = SmsActorSound.SfxBus;
            if (SoundsAtGameLevel && p.Stream != null) p.VolumeDb = SmsActorSound.LevelDb(System.IO.Path.GetFileNameWithoutExtension(p.Stream.ResourcePath));
        }

        if (_buttons.Count > 0)
        {
            _buttons[0].GrabFocus();
            Highlight(0);
        }

        // 266: this screen is a finished level's way back now (the Level Select's AfterLevelScene): a Shine's exit leaves ReturnFromScene for the
        // Level Select's hand (the poster of the level just left) and ReturnWithShine for its fade from the Shine demo's white -- the white is spent
        // HERE (FadeInFrom below), so the Level Select, reached through this screen's fade to black, fades in from black as it should
        if (GameData.Instance != null) GameData.Instance.ReturnWithShine = false;

        // SMS pass 127: arriving from the episode select the screen is white (its startWipe(0xf) with the white fader): fade in from it
        // (266: and from a finished level's last screen -- the Shine demo's white, the Exit Area's or the game over's black)
        if (GameData.Instance != null && GameData.Instance.FadeInFrom.HasValue && _fadeRect != null)
        {
            Color from = GameData.Instance.FadeInFrom.Value;
            GameData.Instance.FadeInFrom = null;
            _fadeRect.Color = new Color(from, 1f);
            _fadeRect.Visible = true;
            Tween fadeIn = CreateTween();
            fadeIn.TweenProperty(_fadeRect, "color:a", 0f, ArriveFadeSeconds);
            fadeIn.TweenCallback(Callable.From(() => { if (IsInstanceValid(_fadeRect)) { _fadeRect.Visible = false; _fadeRect.Color = new Color(0f, 0f, 0f, 0f); } }));
        }
    }

    /// <summary>SMS pass 127: seconds of the fade in from the episode select's white (the game's stage fade-in, startWipe(.., 0.4)).</summary>
    [Export] public float ArriveFadeSeconds = 0.4f;

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
            // 258: back on this screen, the character you played keeps the colour you picked; everyone else starts on their default
            if (GameData.Instance != null && GameData.Instance.SelectedCharacter == entry)
                poster.SetColor(GameData.Instance.SelectedColor);

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
        if (_highlighted >= 0 && _highlighted != index && MoveSound != null && MoveSound.Stream != null)
            MoveSound.Play();                                   // 251c: the hand moves to another character -- the yes / no cursor's click (not on the first highlight)
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
        GameData.Instance.SelectedColor = _buttons[index].ColorIndex;   // 258: the colour on the poster goes with him
        // SMS pass 127: through an M gate the destination is the gate's stage (GameData.PendingStage), not this screen's own level
        string dest = GameData.Instance.PendingStage != null && GameData.Instance.PendingStage.ScenePath != "" ? GameData.Instance.PendingStage.ScenePath : LevelToLoad;
        if (ConfirmSound != null && ConfirmSound.Stream != null) ConfirmSound.Play();   // 251c: A -- a talk box comes up (TALK_MODE_IN)
        GD.Print($"CharacterSelect: {entry.DisplayName} -> {dest}");

        // Let the stamp finish painting before the level swallows the screen.
        _confirming = true;
        float wait = _buttons[index].PlayStamp() + StampHoldSeconds;
        if (wait > 0f)
            await ToSignal(GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this))
            return;

        // Fade the whole screen once the tag is painted, so the level doesn't
        // snap in over a poster that's still on screen.
        // 259: a destination that is not another select screen (a stage through an M gate: GameData.PendingStage) takes the menu song down with the
        // screen; the Level Select keeps it going
        if (GameData.Instance.PendingStage != null && GameData.Instance.PendingStage.ScenePath != "")
            SmsMenuMusic.LeaveMenus(this, FadeSeconds);
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

        var err = GetTree().ChangeSceneToFile(dest);
        if (err != Error.Ok)
            GD.PushError($"CharacterSelect: could not load '{dest}' ({err}).");
    }

    /// <summary>The left stick (a pad on any device: Godot's own joypads, the GameCube adapter's 20+), taken here BEFORE Godot's focus navigation
    /// sees it (so the two never both step on one push): past StickThreshold the hand moves one poster, and not again until the stick has come
    /// back inside StickRelease.</summary>
    public override void _Input(InputEvent @event)
    {
        if (!PadControls || _buttons.Count == 0 || @event is not InputEventJoypadMotion jm || jm.Axis != JoyAxis.LeftX)
            return;
        float v = jm.AxisValue;
        if (Mathf.Abs(v) < StickRelease)
            _stickLatch = 0;
        else if (v >= StickThreshold && _stickLatch != 1)
        {
            _stickLatch = 1;
            MoveHighlight(1);
        }
        else if (v <= -StickThreshold && _stickLatch != -1)
        {
            _stickLatch = -1;
            MoveHighlight(-1);
        }
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Left/right to move, accept to confirm -- the keyboard through Godot's ui_* actions, a pad through button_a and the D-pad (255; the
    /// stick is _Input's).</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_buttons.Count == 0 || !@event.IsPressed() || @event.IsEcho())
            return;

        if ((@event.IsActionPressed("ui_accept") || (PadControls && @event.IsActionPressed("button_a"))) && _highlighted >= 0)
        {
            Confirm(_highlighted);
            GetViewport().SetInputAsHandled();
        }
        else if (PadControls && @event.IsActionPressed("ui_right"))
        {
            MoveHighlight(1);        // the D-pad (ui_right carries it) when no poster had the focus
            GetViewport().SetInputAsHandled();
        }
        else if (PadControls && @event.IsActionPressed("ui_left"))
        {
            MoveHighlight(-1);
            GetViewport().SetInputAsHandled();
        }
        // 258: X = the next colour, Y = the previous one, on the highlighted poster (the keyboard: E / Q); the hand's click for the change
        else if (ColorCycling && _highlighted >= 0 && !_confirming && (@event.IsActionPressed("button_x") || (@event is InputEventKey kx && kx.Keycode == Key.E)))
        {
            CycleColor(1);
            GetViewport().SetInputAsHandled();
        }
        else if (ColorCycling && _highlighted >= 0 && !_confirming && (@event.IsActionPressed("button_y") || (@event is InputEventKey ky && ky.Keycode == Key.Q)))
        {
            CycleColor(-1);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>258: X / Y on the Player Select cycles the highlighted character's alternate colours (CharacterEntry.Colors).</summary>
    [Export] public bool ColorCycling = true;

    /// <summary>The highlighted poster's colour one step along; the move click plays when it changed.</summary>
    public bool CycleColor(int dir)
    {
        if (_highlighted < 0 || _highlighted >= _buttons.Count)
            return false;
        if (!_buttons[_highlighted].CycleColor(dir))
            return false;
        if (MoveSound != null && MoveSound.Stream != null)
            MoveSound.Play();
        return true;
    }

    /// <summary>The highlighted poster's colour index (for the drives).</summary>
    public int HighlightedColorIndex => _highlighted >= 0 && _highlighted < _buttons.Count ? _buttons[_highlighted].ColorIndex : -1;

    /// <summary>The hand one poster along (focus moves with it: FocusEntered -> Highlight); at the ends it stays.</summary>
    private void MoveHighlight(int dir)
    {
        if (_buttons.Count == 0)
            return;
        int cur = _highlighted < 0 ? 0 : Mathf.Clamp(_highlighted, 0, _buttons.Count - 1);
        int next = Mathf.Clamp(cur + dir, 0, _buttons.Count - 1);
        if (_highlighted >= 0 && next == cur)
            return;
        _buttons[next].GrabFocus();          // FocusEntered -> Highlight(next)
        if (_highlighted != next)
            Highlight(next);                 // a poster that cannot take the focus still gets the hand
    }
}
