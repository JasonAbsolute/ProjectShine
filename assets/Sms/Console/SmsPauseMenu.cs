using Godot;

/// <summary>
/// The pause menu -- TPauseMenu2 (GC2D/PauseMenu2.cpp) on pause_1.blo: the dark mask (alpha 180), "PAUSE" in the big letters, three
/// items (CONTINUE, SAVE, EXIT AREA -- two where there is no area to exit, as in Delfino Plaza) and the stage / scenario bar. The
/// director (MarDirectorDirect.cpp 805-816) opens it on Start while Mario is not in a jumping status (checkActionThing3; else the
/// "not now" sound); the game pauses (gpMSound->pauseOn, SMSRumbleMgr->startPause). appearWindow (175): the clock counts 0.5 a frame;
/// letter i from 5 i: over 20 units it grows from a 50-px inset, spins in from +180 degrees (9 a unit), hops 40 px (2 n (1 - n) 80) and
/// fades in (12.8 a unit); the mask reaches its alpha at 30; the items from 30: grow through +15 px at 40 and settle at 45, fading in
/// 26 a unit, the first item's tilt turning in; open at 46. Open (301): Start / A confirm, B closes, up / down move the cursor (no
/// wrap with two items); the selected item pulses -- +1 px a unit to 18, back to 35 -- and shifts toward white, the others plain.
/// disappearWindow (246): over 10 units the panes fade 12 a frame, the letters and items shrink 2 % a frame and the menu pane turns
/// 2 degrees a unit. CONTINUE resumes; SAVE saves (the card dialog is not ported: the menu dips while GameData saves); EXIT AREA leaves
/// the stage (ExitAreaScene) behind the fader's iris. The item sparks (emitters 0x1F9 / 0x1FA) and the console's pauseIn / pauseOut
/// (the star HUD and the lives brought in, the telop away) are not ported. The scene: assets/Sms/Console/PauseMenu.tscn under Mario.
/// </summary>
public partial class SmsPauseMenu : CanvasLayer
{
    private enum State { Closed, Appearing, Open, Saving, Disappearing }

    [Export] public Control Root;          // the blo's 600 x 480 screen, placed about its centre
    [Export] public ColorRect Mask;        // 'mask': black, alpha 180
    [Export] public Control Menu;          // 't_0' at (296, 258)
    /// <summary>The title (the game: PAUSE), built from the big letters by <see cref="SmsBigText"/> about the blo's word (its five
    /// panes 'pa00'..'pa04' at x -153..86, y -111..-141 off 't_0', each turned 12 degrees); a Level's PauseTitle, when set, wins.
    /// DEVIATION (Jaysonn's): the game's word is fixed and white -- the letters are tinted from <see cref="Palette"/> in turn.</summary>
    [Export] public string TitleText = "PAUSE";
    /// <summary>Where the letters come from; {0} = the letter (lower-case, "ex" for !) -- or a ShineLetter{0}.tres style set.</summary>
    [Export] public string GlyphPathFormat = "res://assets/Sms/hud/big_tx_{0}.png";
    [Export] public bool Colored = true;
    [Export] public Color[] Palette = SmsBigText.DefaultPalette();
    [Export] public float GlyphHeight = 78f;
    [Export] public float LetterGap = -8f;
    [Export] public float SpaceWidth = 28f;
    /// <summary>The blo's PAUSE: pane centres averaging (-3, -78) off 't_0', on its hand-laid wave (+6, +17, -24, +1, +1), tilted -12.</summary>
    [Export] public Vector2 TitleCenter = new Vector2(-3f, -78f);
    [Export] public float[] TitleWave = { 5.8f, 16.8f, -24.2f, 0.8f, 0.8f };
    [Export] public float TitleTilt = -12f;
    /// <summary>A longer title is built smaller to fit this many blo px (760 fills a 16:9 window). 0 = never.</summary>
    [Export] public float MaxWidth = 760f;
    private TextureRect[] Letters = System.Array.Empty<TextureRect>(); // the title's letters, built under Menu
    private string _builtTitle;
    [Export] public TextureRect[] Items = System.Array.Empty<TextureRect>();   // 'tx_1'..'tx_3'
    [Export] public Control Stage;         // 'brek' at (296, 412)
    [Export] public Label StageName, ScenarioName;
    /// <summary>The selected item's colour (tx_1's white: 3cff3c).</summary>
    [Export] public Color ItemColor = new Color(0x3c / 255f, 1f, 0x3c / 255f);
    /// <summary>The Start button. A missing action gets Escape and the joypad's Start at run time -- add it to the Input Map to bind your own.</summary>
    [Export] public string PauseAction = "button_start";
    /// <summary>Where EXIT AREA goes. Empty: the item is hidden and the menu has two entries, as in the game's plaza.</summary>
    [Export] public string ExitAreaScene = "";
    /// <summary>1 = the blo's 480 lines on the window's height.</summary>
    [Export] public float Size = 1.0f;
    [Export] public AudioStreamPlayer PauseOn, CursorMove, NotNow;

    private State _state = State.Closed;
    private float _fade, _bounce, _saveHold;
    private int _selected, _numItems;
    private bool _pressedB, _confirmed;
    private float _maskAlpha, _barAlpha;
    private Vector2[] _letterHome = System.Array.Empty<Vector2>(), _letterSize = System.Array.Empty<Vector2>();
    private float[] _letterRot = System.Array.Empty<float>();
    private Vector2[] _itemHome = System.Array.Empty<Vector2>(), _itemSize = System.Array.Empty<Vector2>();
    private float _firstItemRot;
    private Mario _mario;
    private readonly System.Collections.Generic.Dictionary<string, bool> _held = new();

    /// <summary>A press this frame, by the action's own state (Input.IsActionJustPressed is stamped per phase; the menu runs in the idle process while the tree is paused).</summary>
    private bool Pressed(string action)
    {
        bool now = InputMap.HasAction(action) && Input.IsActionPressed(action);
        _held.TryGetValue(action, out bool was);
        _held[action] = now;
        return now && !was;
    }

    public bool IsOpen => _state != State.Closed;
    /// <summary>The cursor's item (the headless drive reads it).</summary>
    public int Selected => _selected;
    public int LettersShown { get { int n = 0; foreach (TextureRect t in Letters) if (t.IsVisibleInTree()) n++; return n; } }
    public float MaskAlpha => Mask != null && Mask.Visible ? Mask.Color.A : 0f;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _mario = GetParent() as Mario;
        BuildTitle(CurrentTitle());
        _itemHome = new Vector2[Items.Length]; _itemSize = new Vector2[Items.Length];
        for (int i = 0; i < Items.Length; i++)
        {
            _itemHome[i] = Items[i].Position; _itemSize[i] = Items[i].Size;
            Items[i].PivotOffset = Items[i].Size * 0.5f;
        }
        _firstItemRot = Items.Length > 0 ? Items[0].Rotation : 0f;
        _maskAlpha = Mask != null ? Mask.Color.A : 180f / 255f;
        _barAlpha = Stage != null ? Stage.Modulate.A : 1f;
        _numItems = ExitAreaScene == "" ? 2 : 3; // Delfino Plaza's menu has no EXIT AREA
        if (!InputMap.HasAction(PauseAction))
        {
            InputMap.AddAction(PauseAction);
            InputMap.ActionAddEvent(PauseAction, new InputEventKey { Keycode = Key.Escape });
            InputMap.ActionAddEvent(PauseAction, new InputEventJoypadButton { ButtonIndex = JoyButton.Start });
            GD.Print($"[SMS pause] the Input Map has no '{PauseAction}': Escape and the joypad's Start stand in -- add the action to bind your own");
        }
        if (Root != null)
            Root.Visible = false;
        if (Mask != null)
            Mask.Visible = false;
    }

    private string CurrentTitle()
    {
        var level = GetTree()?.CurrentScene as Level;
        return !string.IsNullOrEmpty(level?.PauseTitle) ? level.PauseTitle : TitleText;
    }

    /// <summary>(Re)builds the title's letters under <see cref="Menu"/> and takes their rest places; the old ones are freed.</summary>
    private void BuildTitle(string text)
    {
        foreach (TextureRect t in Letters)
            if (IsInstanceValid(t))
            {
                t.GetParent()?.RemoveChild(t); // out of the tree now: the new letters may take the names
                t.QueueFree();
            }
        Letters = Menu != null
            ? SmsBigText.Build(Menu, text, GlyphPathFormat, TitleCenter, GlyphHeight, LetterGap, SpaceWidth, Palette, Colored, TitleWave, TitleTilt, MaxWidth).ToArray()
            : System.Array.Empty<TextureRect>();
        _letterHome = new Vector2[Letters.Length]; _letterSize = new Vector2[Letters.Length]; _letterRot = new float[Letters.Length];
        for (int i = 0; i < Letters.Length; i++)
        {
            _letterHome[i] = Letters[i].Position; _letterSize[i] = Letters[i].Size; _letterRot[i] = Letters[i].Rotation;
            Letters[i].Visible = false;
        }
        _builtTitle = text;
    }

    public override void _Process(double delta)
    {
        Layout();
        if (_state == State.Closed)
        {
            if (!GetTree().Paused && Pressed(PauseAction))
            {
                if (_mario == null || _mario.SmsCanPause())
                    Open();
                else if (NotNow != null && NotNow.Stream != null)
                    NotNow.Play(); // MSD_SE_SY_NOT_COLLECT
            }
            return;
        }
        if (_state != State.Open)
            foreach (string act in new[] { PauseAction, "button_a", "button_b", "Lstick_down", "Lstick_up" })
                Pressed(act); // keep the edges current while the menu moves
        float units = (float)delta * 60f * SmsHud.Anim; // mFadeAnim += 0.5 a console frame (120 Hz ticks here)
        switch (_state)
        {
            case State.Appearing: AppearWindow(units); break;
            case State.Open: OpenTick(units); break;
            case State.Saving: SavingTick(units); break;
            case State.Disappearing: DisappearWindow(units); break;
        }
    }

    private void Layout()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        if (Root != null)
        {
            float s = vp.Y / 480f * Size;
            Root.Scale = new Vector2(s, s);
            Root.Position = vp * 0.5f - new Vector2(300f, 240f) * s;
        }
        if (Mask != null)
        {
            Mask.Position = Vector2.Zero;
            Mask.Size = vp;
        }
    }

    /// <summary>setDrawStart (546) + the director's pause entry: the tree paused, the menu reset and appearing.</summary>
    public void Open()
    {
        GetTree().Paused = true;
        _pressedB = false; _confirmed = false; _selected = 0; _fade = 0f; _bounce = 0f;
        _state = State.Appearing;
        for (int i = 0; i < Items.Length; i++)
        {
            Items[i].Visible = false;
            Items[i].Modulate = i == 0 ? ItemColor : Colors.White;
            Items[i].Position = _itemHome[i]; Items[i].Size = _itemSize[i];
            Items[i].Rotation = i == 0 ? 0f : _firstItemRot; // the first item's tilt turns in during the appear
        }
        string title = CurrentTitle();
        if (title != _builtTitle)
            BuildTitle(title);
        for (int i = 0; i < Letters.Length; i++)
        {
            Letters[i].Visible = false;
            Letters[i].Rotation = _letterRot[i] + Mathf.Pi;
        }
        if (Menu != null) { Menu.Modulate = Colors.White; Menu.Rotation = 0f; }
        if (Mask != null) { Mask.Visible = true; Mask.Color = new Color(0f, 0f, 0f, 0f); }
        if (Stage != null) Stage.Modulate = new Color(1f, 1f, 1f, 0f);
        if (Root != null) Root.Visible = true;
        var level = GetTree().CurrentScene as Level;
        if (StageName != null) StageName.Text = level?.StageName ?? "";
        if (ScenarioName != null) ScenarioName.Text = level?.ScenarioName ?? "";
        if (PauseOn != null && PauseOn.Stream != null)
            PauseOn.Play(); // MSD_SE_SY_PAUSE_ON
    }

    /// <summary>drawAppearPane (584): a letter's inset, hop, spin and fade over 20 units.</summary>
    private void AppearPane(TextureRect pic, float anim, Vector2 home, Vector2 size, float rot)
    {
        if (anim < 0f) { pic.Visible = false; return; }
        pic.Visible = true;
        if (anim >= 20f)
        {
            pic.Position = home; pic.Size = size; pic.PivotOffset = size * 0.5f; pic.Rotation = rot; pic.Modulate = Colors.White;
            return;
        }
        float n = 0.05f * anim;
        float shrink = 2f * n * (1f - n) * 80f;         // the hop
        float offset = 0.75f * (19f - anim);              // the inset
        pic.Position = home + new Vector2(offset, offset - shrink);
        pic.Size = new Vector2(size.X - 2f * offset, size.Y - 2f * offset);
        pic.PivotOffset = pic.Size * 0.5f;
        pic.Rotation = rot - Mathf.DegToRad(180f - 9f * anim); // 180 + orig - 9 anim, J2D's sense
        pic.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, anim * 12.8f / 255f));
    }

    private void AppearWindow(float units)
    {
        if (_fade <= 45f)
        {
            for (int i = 0; i < Letters.Length; i++)
                AppearPane(Letters[i], _fade - 5f * i, _letterHome[i], _letterSize[i], _letterRot[i]);
            float a = Mathf.Min(_maskAlpha, _maskAlpha / 45f * _fade * 1.5f);
            if (Mask != null) Mask.Color = new Color(0f, 0f, 0f, a);
        }
        float stageA = _fade * (_maskAlpha / 45f) * 1.5f;
        if (Stage != null) Stage.Modulate = new Color(1f, 1f, 1f, stageA > _maskAlpha ? _barAlpha : Mathf.Min(_barAlpha, stageA));
        if (_fade >= 30f)
        {
            for (int i = 0; i < _numItems && i < Items.Length; i++)
            {
                TextureRect it = Items[i];
                if (!it.Visible) { it.Visible = true; it.Modulate = new Color(it.Modulate, 0f); }
                if (_fade <= 40f)
                {
                    float hx = 3f * (_fade - 35f), hy = 1.5f * (_fade - 35f);
                    Reform(it, _itemHome[i], _itemSize[i], hx, hy);
                    it.Modulate = new Color(i == 0 ? ItemColor : Colors.White, Mathf.Min(1f, 26f * (_fade - 30f) / 255f));
                    if (i == 0)
                        it.Rotation = _firstItemRot * 0.1f * (_fade - 30f);
                }
                else if (_fade <= 45f)
                {
                    float hx = 3f * (45f - _fade), hy = 1.5f * (45f - _fade);
                    Reform(it, _itemHome[i], _itemSize[i], hx, hy);
                    it.Modulate = new Color(i == 0 ? ItemColor : Colors.White, 1f);
                    if (i == 0) it.Rotation = _firstItemRot;
                }
                else if (_fade <= 46f)
                {
                    Reform(it, _itemHome[i], _itemSize[i], 0f, 0f);
                    _state = State.Open;
                    _bounce = 0f;
                }
            }
            if (_numItems > Items.Length) _state = State.Open;
        }
        _fade += units;
    }

    private static void Reform(TextureRect it, Vector2 home, Vector2 size, float hx, float hy)
    {
        it.Position = home - new Vector2(hx, hy);
        it.Size = size + new Vector2(2f * hx, 2f * hy);
        it.PivotOffset = it.Size * 0.5f;
    }

    private void OpenTick(float units)
    {
        int cur = _selected;
        bool start = Pressed(PauseAction), a = Pressed("button_a"), b = Pressed("button_b"), down = Pressed("Lstick_down"), up = Pressed("Lstick_up");
        if (start || a)
        {
            _confirmed = true;
            switch (cur)
            {
                case 1: // SAVE: the card dialog is not ported -- GameData saves while the menu dips
                    if (PauseOn != null && PauseOn.Stream != null) PauseOn.Play();
                    GameData.Instance?.SaveNow();
                    _saveHold = 0f;
                    _state = State.Saving;
                    return;
                default: // CONTINUE, EXIT AREA: setDrawEnd
                    _fade = 0f;
                    _state = State.Disappearing;
                    return;
            }
        }
        if (b)
        {
            _pressedB = true;
            _fade = 0f;
            _state = State.Disappearing;
            return;
        }
        if (down)
            _selected = _numItems > 2 ? (cur < _numItems - 1 ? cur + 1 : 0) : _numItems - 1;
        else if (up)
            _selected = _numItems > 2 ? (cur == 0 ? _numItems - 1 : cur - 1) : 0;
        if (_selected != cur)
        {
            if (CursorMove != null && CursorMove.Stream != null) CursorMove.Play(); // MSD_SE_SY_CURSOR
            Items[cur].Modulate = Colors.White;
            Reform(Items[cur], _itemHome[cur], _itemSize[cur], 0f, 0f);
            Items[_selected].Modulate = ItemColor;
            _bounce = 0f;
        }
        // the selected item's pulse: +1 px a unit to 18, back by 35, the colour shifting toward white
        TextureRect sel = Items[_selected];
        float shift;
        if (_bounce < 18f) { Reform(sel, _itemHome[_selected], _itemSize[_selected], _bounce, 0.5f * _bounce); shift = 10f * _bounce; }
        else if (_bounce < 35f) { Reform(sel, _itemHome[_selected], _itemSize[_selected], 35f - _bounce, 0.5f * (35f - _bounce)); shift = (35f - _bounce) * 10f; }
        else { _bounce = -0.5f; shift = 0f; }
        sel.Modulate = new Color(Mathf.Min(1f, ItemColor.R + shift / 255f), ItemColor.G, ItemColor.B, 1f);
        _bounce += units;
    }

    private void SavingTick(float units)
    {
        if (Menu != null)
            Menu.Modulate = new Color(1f, 1f, 1f, Mathf.Max(0f, Menu.Modulate.A - 16f / 255f * units));
        _saveHold += units;
        if (_saveHold >= 40f) // the dialog's time, then back to the open menu (mCardSave->unk2DF)
        {
            _confirmed = false;
            if (Menu != null) Menu.Modulate = Colors.White;
            _state = State.Open;
        }
    }

    private void DisappearWindow(float units)
    {
        if (_fade <= 10f)
        {
            float a = Mathf.Max(0f, (Menu != null ? Menu.Modulate.A : 1f) - 12f / 255f * units);
            if (Menu != null) Menu.Modulate = new Color(1f, 1f, 1f, a);
            if (Mask != null && a < Mask.Color.A) Mask.Color = new Color(0f, 0f, 0f, a);
            if (Stage != null && a < Stage.Modulate.A) Stage.Modulate = new Color(1f, 1f, 1f, a);
            float k = Mathf.Pow(0.98f, units);
            foreach (TextureRect t in Letters) Shrink(t, k);
            for (int i = 0; i < _numItems && i < Items.Length; i++) Shrink(Items[i], k);
            if (Menu != null) Menu.Rotation = Mathf.DegToRad(_fade * 2f); // -fade x 2 in J2D's sense: clockwise
            _fade += units;
            return;
        }
        Close();
    }

    private static void Shrink(TextureRect t, float k)
    {
        Vector2 c = t.Position + t.Size * 0.5f;
        t.Size *= k;
        t.PivotOffset = t.Size * 0.5f;
        t.Position = c - t.Size * 0.5f;
    }

    /// <summary>MENU_CLOSED -> the director's getNextState: 0 resume, 5 leave the area.</summary>
    private void Close()
    {
        _state = State.Closed;
        if (Root != null) Root.Visible = false;
        if (Mask != null) Mask.Visible = false;
        GetTree().Paused = false;
        if (_confirmed && _selected == 2 && ExitAreaScene != "")
        {
            SmsFader.Current?.StartWipe(SmsFader.Wipe.Circle, 0f); // unkE4 = 2: the iris
            _leaving = true;
        }
    }

    private bool _leaving;

    public override void _PhysicsProcess(double delta)
    {
        if (!_leaving)
            return;
        if (SmsFader.Current == null || SmsFader.Current.IsFullyFadedOut)
        {
            _leaving = false;
            GetTree().ChangeSceneToFile(ExitAreaScene);
        }
    }
}
