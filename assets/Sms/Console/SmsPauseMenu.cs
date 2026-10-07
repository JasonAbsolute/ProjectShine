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
/// the stage (ExitAreaScene) behind the fader's iris. THE HUD STAYS, BRIGHT (pass 70): the game draws its mask under the console's panes
/// and the director's pause entry runs the console's pauseIn (GCConsole2.cpp 2578: the balloon away, the lives and the star HUD in, the
/// telop away, the timer and the red coins parked down, no auto-hide while paused), the resume its pauseOut (2588: the telop back, the
/// timer and the red coins back, the lives and the star HUD away) -- here the mask sits on its own CanvasLayer (8) under the HUD (10) and
/// Open / Close call SmsStandardHud.PauseIn / PauseOut; the console's layer processes while the tree is paused, as the game performs it
/// under STATE_PAUSE_MENU. THE SMOKE (pass 70b): drawAppearPane (597) creates ms_2d_pause_smo (emitter 0x1F9) at a letter's centre when its
/// appear anim reaches 2.0 -- here one PauseSmoke.tscn (GPUParticles2D, the .jpa's numbers) per letter under Root/Smoke, Root's last child
/// (the 2D emitter manager is the last of Group 2D: over the letters), restarted at the crossing. The selected item's sparks (0x1FA,
/// ms_2d_pause_sel at bounce 3) -- THE SPARKS (pass 80): TPauseMenu2 (441-457) creates emitter 0x1FA at the selected item's global
/// bounds centre when its bounce clock hits mEffectKeyFrame (3: 6 frames into every 71-frame pulse), scaled (4 x width / 129, 1, 1) so
/// the 14 sparks spawn along the item's width, and force-deletes it when the cursor moves (402-408, 431-434) -- here one
/// PauseSparks.tscn (GPUParticles2D with PauseSparks.gdshader, the .jpa's numbers; pass 89: the emitter is rotated (90, 0, -12) degrees,
/// so its circle stands in the screen and the 14 sparks fly OUT radially, a sunburst tilted like the pane -- pass 80 had read the rotation
/// as 0.49 degrees and laid them on a line) under SmokeRoot, restarted at the item's centre with rim_scale_x = 4 x width / 129 and the
/// item's tilt, hidden on a move. The scene: assets/Sms/Console/PauseMenu.tscn under Mario.
/// </summary>
public partial class SmsPauseMenu : CanvasLayer
{
    private enum State { Closed, Appearing, Open, Saving, Disappearing }

    [Export] public Control Root;          // the blo's 600 x 480 screen, placed about its centre
    [Export] public ColorRect Mask;        // 'mask': black, alpha 180 -- on MaskLayer (CanvasLayer 8): under the HUD, over the world
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
    /// <summary>Root's last child: the letters' smoke puffs (gpEmitterManager4D2's draw, after the panes) are instanced here.</summary>
    [Export] public Node2D SmokeRoot;
    /// <summary>assets/Sms/Console/PauseSmoke.tscn: ms_2d_pause_smo as a one-shot GPUParticles2D; one instance a letter.</summary>
    [Export] public PackedScene SmokeScene;
    /// <summary>assets/Sms/Console/PauseSparks.tscn: ms_2d_pause_sel as a one-shot GPUParticles2D -- the selected item's sparks (pass 80), one
    /// instance under SmokeRoot, restarted at bounce <see cref="SparkBounce"/> of every pulse. Empty = no sparks.</summary>
    [Export] public PackedScene SparksScene;
    /// <summary>TPauseMenu2::mEffectKeyFrame (3) and mEffectStretch (129): the bounce clock's value that fires the sparks, and the item
    /// width the emitter's (4, 1, 1) scale is drawn for -- setEmitterScale(4 x width / mEffectStretch, 1, 1).</summary>
    public const float SparkBounce = 3f, SparkStretch = 129f;
    /// <summary>DEVIATION (his ask, pass 89): the stick moves the cursor once it passes this (the game's JUTGamePad CStick::getButton makes the
    /// stick a button at |pos| >= 0.5, JUTGamePad.cpp 372; one move per press, mRepeat with no repeat mask = the trigger) and is released
    /// again below <see cref="MenuStickOff"/>. 0.5 / 0.25 would be the game's feel; he asked for 0.3.</summary>
    [Export(PropertyHint.Range, "0.05,1")] public float MenuStickOn = 0.3f;
    [Export(PropertyHint.Range, "0,1")] public float MenuStickOff = 0.15f;
    /// <summary>DEVIATION (his ask, pass 89): START in the open menu resumes play whatever the cursor is on. The game's START is MENU_A: it
    /// confirms the item (MarDirectorDirect / PauseMenu2.cpp 342: MEANING_START | MEANING_MENU_A), so START on SAVE saved. Off = the game.</summary>
    [Export] public bool StartResumes = true;
    private int _stickDir;                                                     // the stick as a button: -1 up, 0, +1 down (CStick::getButton)
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
    private GpuParticles2D[] _smoke = System.Array.Empty<GpuParticles2D>(); // one puff a letter, under SmokeRoot
    private bool[] _smoked = System.Array.Empty<bool>();                      // the letter's puff went this appear
    private int _smokeFired; private string _smokeLog = "";
    private GpuParticles2D _sparks;                                            // the one selection-sparks emitter (mEmitter)
    private int _sparksFired; private string _sparksLog = "";
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
    /// <summary>For the drives: puffs started since Open, and "i@fade(x,y)" per puff.</summary>
    public int SmokeFired => _smokeFired;
    public string SmokeLog => _smokeLog;
    public GpuParticles2D[] SmokePuffs => _smoke;
    /// <summary>For the drives: sparks bursts since Open, "item@bounce(x,y)w" per burst, and the emitter.</summary>
    public int SparksFired => _sparksFired;
    public string SparksLog => _sparksLog;
    public GpuParticles2D Sparks => _sparks;
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
        if (SmokeRoot == null || SmokeScene == null)
            GD.PushWarning($"[SMS pause] {GetPath()}: the scene's SmokeRoot / SmokeScene exports are not both set (see assets/Sms/Console/PauseMenu.tscn): no smoke");
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
        foreach (GpuParticles2D g in _smoke)
            if (IsInstanceValid(g))
            {
                g.GetParent()?.RemoveChild(g);
                g.QueueFree();
            }
        Letters = Menu != null
            ? SmsBigText.Build(Menu, text, GlyphPathFormat, TitleCenter, GlyphHeight, LetterGap, SpaceWidth, Palette, Colored, TitleWave, TitleTilt, MaxWidth).ToArray()
            : System.Array.Empty<TextureRect>();
        _letterHome = new Vector2[Letters.Length]; _letterSize = new Vector2[Letters.Length]; _letterRot = new float[Letters.Length];
        _smoke = new GpuParticles2D[Letters.Length]; _smoked = new bool[Letters.Length];
        for (int i = 0; i < Letters.Length && SmokeRoot != null && SmokeScene != null; i++)
        {
            _smoke[i] = SmokeScene.Instantiate<GpuParticles2D>();
            _smoke[i].Name = $"Smoke{i}";
            _smoke[i].Emitting = false;
            SmokeRoot.AddChild(_smoke[i]);
        }
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
                if (_mario != null && !_mario.SmsDirectorInPlay)
                    return; // MarDirectorDirect.cpp 804: START is read in case 0 only -- a talk (unk124 1 / 2) ignores it, silently (pass 87)
                if (_mario == null || _mario.SmsCanPause())
                    Open();
                else if (NotNow != null && NotNow.Stream != null)
                    NotNow.Play(); // MSD_SE_SY_NOT_COLLECT
            }
            return;
        }
        if (_state != State.Open)
        {
            foreach (string act in new[] { PauseAction, "button_a", "button_b" })
                Pressed(act); // keep the edges current while the menu moves
            MenuStickEdge();  // and the stick's button state (a stick held through the opening is no move)
        }
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
        System.Array.Fill(_smoked, false); _smokeFired = 0; _smokeLog = "";
        _sparksFired = 0; _sparksLog = "";
        SparksOff();
        if (Menu != null) { Menu.Modulate = Colors.White; Menu.Rotation = 0f; }
        if (Mask != null) { Mask.Visible = true; Mask.Color = new Color(0f, 0f, 0f, 0f); }
        if (Stage != null) Stage.Modulate = new Color(1f, 1f, 1f, 0f);
        if (Root != null) Root.Visible = true;
        var level = GetTree().CurrentScene as Level;
        if (StageName != null) StageName.Text = level?.StageName ?? "";
        if (ScenarioName != null) ScenarioName.Text = level?.ScenarioName ?? "";
        if (PauseOn != null && PauseOn.Stream != null)
            PauseOn.Play(); // MSD_SE_SY_PAUSE_ON
        SmsHudLayer.Current?.Standard?.PauseIn(); // the director's pause entry (MarDirectorDirect.cpp 732): the console's pauseIn
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

    /// <summary>gpEmitterManager4D2->createEmitter(the pane's global centre, 0x1F9): letter i's smoke puff, once an appear. Its position is the
    /// letter's rotation centre (J2D's bounds are the unrotated rect; the Control's pivot is its middle), in SmokeRoot's frame.</summary>
    private void Smoke(int i)
    {
        _smoked[i] = true;
        GpuParticles2D g = _smoke[i];
        if (g == null || SmokeRoot == null)
            return;
        TextureRect t = Letters[i];
        Vector2 c = SmokeRoot.ToLocal(t.GetGlobalTransform() * (t.Size * 0.5f));
        g.Position = c;
        g.SpeedScale = SmsHud.Anim; // the menu's pace knob (the game: 1)
        g.Restart();
        _smokeFired++;
        _smokeLog += $"{i}@{_fade:F1}({c.X:F0},{c.Y:F0}) ";
    }

    /// <summary>TPauseMenu2 441-457: createEmitter(the item's global bounds centre, 0x1FA) + setEmitterScale(4 x width / 129, 1, 1): the
    /// sparks along the item's width. One emitter: a restart at the new place (the game keeps only the latest, mEmitter).</summary>
    private void SparksAt(int i)
    {
        if (SparksScene == null || SmokeRoot == null || i < 0 || i >= Items.Length)
            return;
        if (_sparks == null || !IsInstanceValid(_sparks))
        {
            _sparks = SparksScene.Instantiate<GpuParticles2D>();
            _sparks.Name = "Sparks";
            _sparks.Emitting = false;
            if (_sparks.ProcessMaterial != null)
                _sparks.ProcessMaterial = (Material)_sparks.ProcessMaterial.Duplicate(); // rim_scale_x is set per item
            SmokeRoot.AddChild(_sparks);
        }
        TextureRect it = Items[i];
        Vector2 c = SmokeRoot.ToLocal(it.GetGlobalTransform() * (it.Size * 0.5f));  // getGlobalBounds's centre (the bounce keeps it)
        float width = it.Size.X;                                                     // getWidth(): the pane's bounds as reformed last tick (the bounce's +hw each side)
        if (_sparks.ProcessMaterial is ShaderMaterial sm)
            sm.SetShaderParameter("rim_scale_x", 4f * width / SparkStretch);   // setEmitterScale(4 x width / 129, 1, 1): the rim along the item
        _sparks.Position = c;
        _sparks.Rotation = it.Rotation;                                       // the emitter's Rz(-12): the 2D screen's tilt of the menu pane (the item's)
        _sparks.Visible = true;
        _sparks.SpeedScale = SmsHud.Anim;
        _sparks.Restart();
        _sparksFired++;
        _sparksLog += $"{i}@{_bounce:F1}({c.X:F0},{c.Y:F0})w{width:F0} ";
    }

    /// <summary>forceDeleteEmitter: the sparks vanish at once (a one-shot keeps its particles, so the node is hidden until the next burst).</summary>
    private void SparksOff()
    {
        if (_sparks != null && IsInstanceValid(_sparks))
        {
            _sparks.Emitting = false;
            _sparks.Visible = false;
        }
    }

    private void AppearWindow(float units)
    {
        if (_fade <= 45f)
        {
            for (int i = 0; i < Letters.Length; i++)
            {
                float anim = _fade - 5f * i;
                if (anim >= 2f && i < _smoked.Length && !_smoked[i])
                    Smoke(i); // drawAppearPane (597): anim == 2.0 -> ms_2d_pause_smo at the pane's bounds centre (as set last tick)
                AppearPane(Letters[i], anim, _letterHome[i], _letterSize[i], _letterRot[i]);
            }
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

    /// <summary>The stick as a menu button (JUTGamePad::CStick::getButton + the pad's mTrigger): pressed once past MenuStickOn, released below
    /// MenuStickOff, one move per press -- the raw strength of the two actions (the InputMap's deadzone does not decide; a D-pad bound to
    /// them reads 1). Returns -1 up / +1 down on the press edge, else 0.</summary>
    private int MenuStickEdge()
    {
        float v = (InputMap.HasAction("Lstick_down") ? Input.GetActionRawStrength("Lstick_down") : 0f) - (InputMap.HasAction("Lstick_up") ? Input.GetActionRawStrength("Lstick_up") : 0f);
        int now = _stickDir;
        if (now == 0) { if (v >= MenuStickOn) now = 1; else if (v <= -MenuStickOn) now = -1; }
        else if (Mathf.Abs(v) < MenuStickOff || Mathf.Sign(v) != now) now = 0;   // let go (or flipped: through the centre first)
        int edge = now != 0 && now != _stickDir ? now : 0;
        _stickDir = now;
        return edge;
    }

    private void OpenTick(float units)
    {
        int cur = _selected;
        bool start = Pressed(PauseAction), a = Pressed("button_a"), b = Pressed("button_b");
        int stick = MenuStickEdge();
        bool down = stick > 0, up = stick < 0;
        if (start && StartResumes)
        {
            // DEVIATION: START leaves the menu like B (setDrawEnd with nothing confirmed), whatever the cursor is on
            _pressedB = true;
            _fade = 0f;
            _state = State.Disappearing;
            return;
        }
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
            SparksOff(); // gpEmitterManager4D2->forceDeleteEmitter(mEmitter): the old item's sparks go at once
        }
        // mBounceAnim == mEffectKeyFrame (441): the clock steps 0.5 a frame in the game and lands on 3 exactly; here it crosses it
        if (_bounce >= SparkBounce && _bounce - units < SparkBounce)
            SparksAt(_selected);
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
        SparksOff();
        if (Root != null) Root.Visible = false;
        if (Mask != null) Mask.Visible = false;
        GetTree().Paused = false;
        SmsHudLayer.Current?.Standard?.PauseOut(); // the director's resume: the console's pauseOut
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
