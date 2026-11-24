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

    /// <summary>251b (Jaysonn: "the sound of when you pick yes or no for dialog option box as the sound it makes picking the levels"): the player that
    /// clicks when the hand moves to another level -- the talk balloon's yes / no cursor, MSD_SE_SY_E3_MENU_CURSOR (Sfx/Move). Swap its stream for
    /// another System sound (SELECT_COMMON is the yes / no pick itself). Empty = silent.</summary>
    [Export] public NodePath MoveSoundPath = "Sfx/Move";

    /// <summary>251b / 251c ("when you press A it plays the sound of a text box opening ... the sound the text box makes when it appears, not the text
    /// showing on the screen"): the player for the pick -- the sound as a talk box comes up, MSD_SE_SY_TALK_MODE_IN (MSound::talkModeIn plays it the moment
    /// a talk begins; Sfx/Confirm). TALK_OBI is the balloon's bands rolling out line by line (the text showing), SY_2D_IN the save card's window
    /// coming in -- both in System/ for the knob. Empty = silent.</summary>
    [Export] public NodePath ConfirmSoundPath = "Sfx/Confirm";

    private AudioStreamPlayer MoveSound, ConfirmSound;

    /// <summary>Put the two players on the Sfx bus at the game's volume for their sound (SmsActorSound.LevelDb by the stream's file name); off = as set on the nodes.</summary>
    [Export] public bool SoundsAtGameLevel = true;
    /// <summary>255 (Jaysonn: "A should select whatever the cursor is on and the control stick should move it left and right"): the pad's own actions
    /// on this screen. Godot's ui_accept carries only Enter / Space and ui_cancel only Escape, so no pad button confirmed or went back; the stick
    /// and the D-pad reach the hand through Godot's focus navigation (ui_left / ui_right carry axis 0 and the D-pad) and, when that did not take
    /// the event (no poster focused, an event from the adapter's physics pump), through this screen's own handler. The left stick past
    /// StickThreshold moves the hand one poster (once per push: it must come back inside StickRelease first); button_a confirms, button_b goes back.</summary>
    [Export] public bool PadControls = true;
    [Export(PropertyHint.Range, "0.2,0.9,0.05")] public float StickThreshold = 0.5f;
    [Export(PropertyHint.Range, "0.0,0.6,0.05")] public float StickRelease = 0.3f;
    /// <summary>A build marker for the drives (read in the built DLL before a run).</summary>
    public const string BuildMarker = "SMS255_LEVEL_SELECT_PAD_A";

    private Container _portraits;
    private Control _selectHand;
    private ColorRect _fadeRect;
    private int _highlighted = -1;
    private bool _confirming;
    private int _stickLatch;   // the left stick's last push the hand answered: -1 left, +1 right, 0 centred (ready)
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
        // 243: the row grows to its posters on the first layout (and on a window resize) -- re-seat the window round the highlighted poster then
        _portraits.Resized += () => { if (_highlighted >= 0) CallDeferred(nameof(SlideRowTo), _highlighted, false); };

        // 251b: the two sounds on the Sfx bus at the game's volume for them (the sound table's volume of 127 for the yes / no cursor, 60 for the band)
        MoveSound = MoveSoundPath.IsEmpty ? null : GetNodeOrNull<AudioStreamPlayer>(MoveSoundPath);
        ConfirmSound = ConfirmSoundPath.IsEmpty ? null : GetNodeOrNull<AudioStreamPlayer>(ConfirmSoundPath);
        foreach (var p in new[] { MoveSound, ConfirmSound })
        {
            if (p == null) continue;
            p.Bus = SmsActorSound.SfxBus;
            if (SoundsAtGameLevel && p.Stream != null) p.VolumeDb = SmsActorSound.LevelDb(System.IO.Path.GetFileNameWithoutExtension(p.Stream.ResourcePath));
        }

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
        if (_highlighted >= 0 && _highlighted != index && MoveSound != null && MoveSound.Stream != null)
            MoveSound.Play();                                   // 251b: the hand moves to another level -- the yes / no cursor's click (not on the first highlight)
        _highlighted = index;

        // Containers lay out on the next frame, so the button's rect isn't final yet on the first call - defer the move rather than reading a stale size.
        if (_selectHand != null)
            CallDeferred(nameof(MoveHandTo), index);
        CallDeferred(nameof(SlideRowTo), index, _rowShiftKnown);
        _rowShiftKnown = true;

        for (int i = 0; i < _buttons.Count; i++)
            _buttons[i].SetHighlighted(i == index);
    }

    /// <summary>Seconds the poster row takes to slide a newly picked poster into view (0 = it jumps). With four posters the row never moves.</summary>
    [Export] public float RowSlideSeconds = 0.18f;

    /// <summary>Width of the poster row's window on the screen, in pixels: the Player Select's row (four posters and their three gaps), centred
    /// like the authored Portraits container. 0 = the container's own width. The row slides inside this window when it holds more posters than fit.</summary>
    [Export] public float RowWindowWidth = 952f;

    private bool _rowShiftKnown;
    private Tween _rowTween;

    /// <summary>243: more posters than the row holds -- the row slides so the highlighted poster sits in the middle of the row's window, the first and
    /// last posters never leaving its edges. Four posters fill the window exactly, so the Player Select's layout is untouched; twelve scroll. (The
    /// HBoxContainer grows to its children's width in both directions, so its own Size is the whole row, not the window.)</summary>
    private void SlideRowTo(int index, bool animate)
    {
        if (_portraits == null || index < 0 || index >= _buttons.Count)
            return;
        float window = RowWindowWidth > 0f ? RowWindowWidth : _portraits.Size.X;
        float row = 0f;
        foreach (var b in _buttons) row = Mathf.Max(row, b.Position.X + b.Size.X);
        if (row <= window + 0.5f)
            return;
        var button = _buttons[index];
        float centre = button.Position.X + button.Size.X * 0.5f;
        float shift = Mathf.Clamp(window * 0.5f - centre, window - row, 0f);
        // the window sits where the authored row sits: centred in the parent
        float target = (_portraits.GetParentAreaSize().X - window) * 0.5f + shift;
        _rowTween?.Kill();
        if (!animate || RowSlideSeconds <= 0f || Mathf.Abs(target - _portraits.Position.X) < 0.5f)
        {
            _portraits.Position = new Vector2(target, _portraits.Position.Y);
            if (_selectHand != null) MoveHandTo(index);
            return;
        }
        _rowTween = CreateTween();
        _rowTween.TweenMethod(Callable.From<float>(x =>
        {
            if (!IsInstanceValid(_portraits)) return;
            _portraits.Position = new Vector2(x, _portraits.Position.Y);
            if (_selectHand != null) MoveHandTo(_highlighted);
        }), _portraits.Position.X, target, RowSlideSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
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
        if (ConfirmSound != null && ConfirmSound.Stream != null) ConfirmSound.Play();   // 251b / 251c: A -- a talk box comes up (TALK_MODE_IN)
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

    /// <summary>Left/right move the focus (the container's own navigation), accept confirms, cancel goes back -- the keyboard through Godot's ui_*
    /// actions, a pad through button_a / button_b and the D-pad (255; the stick is _Input's).</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_buttons.Count == 0 || !@event.IsPressed() || @event.IsEcho())
            return;

        if ((@event.IsActionPressed("ui_accept") || (PadControls && @event.IsActionPressed("button_a"))) && _highlighted >= 0)
        {
            Confirm(_highlighted);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ui_cancel") || (PadControls && @event.IsActionPressed("button_b")))
        {
            Back();
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
    }

    /// <summary>The hand one poster along (focus moves with it: FocusEntered -> Highlight, which slides the row); at the ends it stays.</summary>
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
