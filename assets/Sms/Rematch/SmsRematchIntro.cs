using Godot;
using System;

/// <summary>
/// SMS pass 157: the REMATCH arena's INTRO (KINGBOO_REMATCH_gdoc.txt, INTRO 3-6). The scene starts black (the fader set black); Mario is held
/// (SuppressForIntro) and King Boo hidden; the two-line card shows over the black (SmsRematchCard: Line1, then Line2 under it, Delfino,
/// the timings below); then Mario is revealed FallHeight over the floor at the MarioSpawn marker (he falls) and the screen fades in on him
/// falling; BooSpawnDelay after he lands (BooSpawnTimeout after the reveal at the latest) King Boo spawns at BooHeight over the floor at the
/// BooSpawn marker and laughs; the fight may start (FightReady). A (the jump button) skips the card. No FLUDD here: the Level's HasFludd is
/// off. After a miss (GameData.CameFromMiss) the card is skipped: he falls straight in and King Boo comes.
/// Knobs: the lines and their timing, the heights, the fade, SkipAllowed, the card's font (LabelSettings). PlayWithoutGameData: the intro
/// runs when the scene is opened on its own too (F6), as it does when the casino sends him here.
/// </summary>
public partial class SmsRematchIntro : Node
{
    [ExportGroup("Who and where")]
    [Export] public NodePath MarioPath = "../Mario";
    [Export] public NodePath MarioSpawnPath = "../MarioSpawn";
    [Export] public NodePath BooPath = "../KingBoo";
    [Export] public NodePath BooSpawnPath = "../BooSpawn";
    [Export] public NodePath FloorPath = "../Arena";
    /// <summary>Mario is revealed this high over the floor under his marker (falls in); King Boo hovers this high over the floor under his.</summary>
    [Export] public float FallHeight = 22f, BooHeight = 5.5f;
    [ExportGroup("The card")]
    [Export] public string Line1 = "You call that a fight fit for a king?";
    [Export] public string Line2 = "I'll show you a real fight...";
    /// <summary>Black before the first line; each line's fade in; the first line held alone; the gap before the second comes; the second held (both up); both lines' fade out; black after.</summary>
    [Export] public float BlackBefore = 0.6f, LineFadeIn = 0.45f, Line1Hold = 1.9f, LineGap = 0.3f, Line2Hold = 2.3f, LinesFadeOut = 0.5f, BlackAfter = 0.35f;
    /// <summary>The card's text style (empty = Delfino at 46 px, white, a black shadow).</summary>
    [Export] public LabelSettings LabelSettings;
    [Export] public int FontSize = 46;
    /// <summary>A (the jump button) skips the card.</summary>
    [Export] public bool SkipAllowed = true;
    [ExportGroup("The fall and the spawn")]
    /// <summary>The fade in once he is revealed falling.</summary>
    [Export] public float FadeInSeconds = 0.7f;
    /// <summary>King Boo comes this long after Mario lands -- or this long after the reveal if he never lands (a fall off the edge).</summary>
    [Export] public float BooSpawnDelay = 0.5f, BooSpawnTimeout = 5f;
    [Export] public bool PlayWithoutGameData = true;
    [Export] public bool DebugPrint;

    [Signal] public delegate void CardShownEventHandler();
    [Signal] public delegate void RevealedEventHandler();
    [Signal] public delegate void FightReadyEventHandler();

    public enum Phase { Waiting, Card, Falling, Spawned, Done }
    public Phase State { get; private set; } = Phase.Waiting;
    public bool Skipped { get; private set; }
    public bool CameFromMiss { get; private set; }
    public float? FloorY { get; private set; }
    public SmsRematchCard Card { get; private set; }

    private Mario _mario; private Node3D _marioSpawn, _booSpawn; private SmsKingBooRematch _boo; private SmsArenaFloor _floor;
    private float _t, _sinceReveal, _landedAt = -1f; private bool _booSpawned;

    public override void _Ready()
    {
        _mario = GetNodeOrNull<Mario>(MarioPath); _marioSpawn = GetNodeOrNull<Node3D>(MarioSpawnPath);
        _boo = GetNodeOrNull<SmsKingBooRematch>(BooPath); _booSpawn = GetNodeOrNull<Node3D>(BooSpawnPath);
        _floor = GetNodeOrNull<SmsArenaFloor>(FloorPath);
        if (Engine.IsEditorHint()) return;
        if (_mario == null) { GD.PushError($"[SMS rematch intro] {GetPath()}: MarioPath {MarioPath} is not a Mario"); return; }
        CameFromMiss = GameData.Instance != null && GameData.Instance.CameFromMiss;   // read before the Level clears it (a child's _Ready runs first)
        // held and dark from the first frame: his own _Ready has run (he is before this node in the scene), the fader is his
        _mario.SuppressForIntro();
        if (SmsFader.Current != null) SmsFader.Current.SetBlack();
        SmsRematchIntroDriveHook();
        CallDeferred(nameof(Start));
    }
    partial void SmsRematchIntroDriveHook();

    private void Start()
    {
        if (SmsFader.Current != null) SmsFader.Current.SetBlack();   // the Level's own miss / return fades, if any, overruled: the card runs over black
        _mario.SuppressForIntro();                                    // again, after every _Ready has run
        if (CameFromMiss)
        {
            if (DebugPrint) GD.Print("[SMS rematch intro] after a miss: no card, the fall at once");
            Reveal();
            return;
        }
        Card = new SmsRematchCard { Name = "RematchCard", Line1Text = Line1, Line2Text = Line2, Settings = LabelSettings, FontSize = FontSize };
        Card.BlackBefore = BlackBefore; Card.LineFadeIn = LineFadeIn; Card.Line1Hold = Line1Hold; Card.LineGap = LineGap; Card.Line2Hold = Line2Hold; Card.LinesFadeOut = LinesFadeOut; Card.BlackAfter = BlackAfter;
        Card.Finished += Reveal;
        AddChild(Card);
        State = Phase.Card; _t = 0f;
        if (DebugPrint) GD.Print($"[SMS rematch intro] the card: \"{Line1}\" / \"{Line2}\" ({Card.TotalSeconds:F1} s)");
        EmitSignal(SignalName.CardShown);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (State == Phase.Card && SkipAllowed && Card != null && !Skipped && (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space")))
        {
            Skipped = true; Card.Skip();
            if (DebugPrint) GD.Print("[SMS rematch intro] the card skipped");
        }
        if (State == Phase.Falling || State == Phase.Spawned)
        {
            _sinceReveal += dt;
            if (_landedAt < 0f && _mario != null && IsInstanceValid(_mario) && _mario.IsOnFloor()) _landedAt = _sinceReveal;
            if (!_booSpawned && ((_landedAt >= 0f && _sinceReveal - _landedAt >= BooSpawnDelay) || _sinceReveal >= BooSpawnTimeout)) SpawnBoo();
        }
    }

    /// <summary>The card is over: Mario revealed FallHeight over the floor (falling), the screen fading in on him.</summary>
    private void Reveal()
    {
        if (State == Phase.Falling || State == Phase.Spawned || State == Phase.Done) return;
        Transform3D spawn = _marioSpawn != null ? _marioSpawn.GlobalTransform : _mario.GlobalTransform;
        FloorY = _floor?.FloorYUnder(spawn.Origin.X, spawn.Origin.Z, spawn.Origin.Y + 100f, 400f, Exclude());   // not Mario himself (he waits at his marker)
        if (!FloorY.HasValue && _floor != null && !float.IsNaN(_floor.TopY)) FloorY = _floor.TopY;
        if (FloorY.HasValue) spawn.Origin = new Vector3(spawn.Origin.X, FloorY.Value + FallHeight, spawn.Origin.Z);
        _mario.RevealFromIntro(spawn);
        _mario.Velocity = Vector3.Zero;
        if (SmsFader.Current != null) { SmsFader.Current.SetBlack(); SmsFader.Current.FadeIn(FadeInSeconds); }
        State = Phase.Falling; _sinceReveal = 0f; _landedAt = -1f;
        if (Card != null && IsInstanceValid(Card)) { Card.QueueFree(); Card = null; }
        if (DebugPrint) GD.Print($"[SMS rematch intro] revealed at {spawn.Origin} (floor {(FloorY.HasValue ? FloorY.Value.ToString("F2") : "?")}), fading in over {FadeInSeconds} s");
        EmitSignal(SignalName.Revealed);
    }

    private Godot.Collections.Array<Rid> Exclude() => _mario != null && IsInstanceValid(_mario) ? new Godot.Collections.Array<Rid> { _mario.GetRid() } : null;

    private void SpawnBoo()
    {
        _booSpawned = true;
        if (_boo != null)
        {
            Vector3 at = _booSpawn != null ? _booSpawn.GlobalPosition : _boo.GlobalPosition;
            float? fy = _floor?.FloorYUnder(at.X, at.Z, at.Y + 100f, 400f, Exclude()) ?? FloorY;
            if (fy.HasValue) at.Y = fy.Value + BooHeight;
            _boo.GlobalPosition = at;
            if (_booSpawn != null) _boo.GlobalRotation = new Vector3(0f, _booSpawn.GlobalRotation.Y, 0f);
            _boo.ReadyForFight += () => { State = Phase.Done; if (DebugPrint) GD.Print("[SMS rematch intro] the fight may start"); EmitSignal(SignalName.FightReady); };
            _boo.Spawn();
        }
        else
        {
            GD.PushWarning($"[SMS rematch intro] {GetPath()}: BooPath {BooPath} is not an SmsKingBooRematch -- no King Boo");
            State = Phase.Done; EmitSignal(SignalName.FightReady); return;
        }
        State = Phase.Spawned;
        if (DebugPrint) GD.Print($"[SMS rematch intro] King Boo spawns at {_boo.GlobalPosition} ({(_landedAt >= 0f ? "Mario landed" : "timeout")})");
    }
}

/// <summary>
/// The two-line card over black (SMS pass 157): Line1 fades in and holds, Line2 comes under it, both fade out; above the fader (layer 110) so it
/// reads over the black. Delfino (Font/Delfino.ttf) at FontSize, white with a black shadow, unless a LabelSettings is given. Skip() runs the
/// fade out at once. Finished fires when the black after is done.
/// </summary>
public partial class SmsRematchCard : CanvasLayer
{
    public string Line1Text = "", Line2Text = "";
    public LabelSettings Settings; public int FontSize = 46;
    public float BlackBefore = 0.6f, LineFadeIn = 0.45f, Line1Hold = 1.9f, LineGap = 0.3f, Line2Hold = 2.3f, LinesFadeOut = 0.5f, BlackAfter = 0.35f;
    [Signal] public delegate void FinishedEventHandler();

    public float TotalSeconds => BlackBefore + LineFadeIn + Line1Hold + LineGap + LineFadeIn + Line2Hold + LinesFadeOut + BlackAfter;
    public float Alpha1 { get; private set; }
    public float Alpha2 { get; private set; }
    public bool Done { get; private set; }

    private Label _l1, _l2; private float _t; private bool _skipped; private float _skipFrom1, _skipFrom2;

    public override void _Ready()
    {
        Layer = 110;
        var root = new Control { Name = "Card", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var settings = Settings ?? DefaultSettings(FontSize);
        _l1 = MakeLabel(Line1Text, settings, -0.5f); _l2 = MakeLabel(Line2Text, settings, 0.5f);
        root.AddChild(_l1); root.AddChild(_l2);
        _l1.Modulate = new Color(1f, 1f, 1f, 0f); _l2.Modulate = new Color(1f, 1f, 1f, 0f);
    }
    private static Label MakeLabel(string text, LabelSettings settings, float row)
    {
        var l = new Label { Text = text, LabelSettings = settings, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        l.OffsetTop = row * 80f - 40f + 10f; l.OffsetBottom = row * 80f + 40f + 10f;   // two rows about the centre, 80 px apart
        l.GrowHorizontal = Control.GrowDirection.Both; l.GrowVertical = Control.GrowDirection.Both;
        l.AnchorTop = 0.5f; l.AnchorBottom = 0.5f;
        return l;
    }
    public static LabelSettings DefaultSettings(int size)
    {
        var s = new LabelSettings { FontSize = size, FontColor = Colors.White, ShadowColor = new Color(0f, 0f, 0f, 0.9f), ShadowOffset = new Vector2(3f, 3f), ShadowSize = 2 };
        if (ResourceLoader.Exists("res://Font/Delfino.ttf")) s.Font = GD.Load<Font>("res://Font/Delfino.ttf");
        return s;
    }

    /// <summary>The lines fade out now (the rest of the timing dropped); Finished after the fade and the black after.</summary>
    public void Skip()
    {
        if (_skipped || Done) return;
        _skipped = true; _skipFrom1 = Alpha1; _skipFrom2 = Alpha2; _t = 0f;
    }

    public override void _Process(double delta)
    {
        if (Done) return;
        _t += (float)delta;
        if (_skipped)
        {
            float k = LinesFadeOut > 0f ? Mathf.Clamp(_t / LinesFadeOut, 0f, 1f) : 1f;
            Alpha1 = _skipFrom1 * (1f - k); Alpha2 = _skipFrom2 * (1f - k);
            Apply();
            if (_t >= LinesFadeOut + BlackAfter) Finish();
            return;
        }
        float t = _t;
        float a1 = 0f, a2 = 0f;
        float t1In = BlackBefore, t1Hold = t1In + LineFadeIn, t2In = t1Hold + Line1Hold + LineGap, t2Hold = t2In + LineFadeIn, tOut = t2Hold + Line2Hold, tEnd = tOut + LinesFadeOut;
        if (t < t1In) a1 = 0f;
        else if (t < t1Hold) a1 = (t - t1In) / Mathf.Max(LineFadeIn, 1e-3f);
        else if (t < tOut) a1 = 1f;
        else if (t < tEnd) a1 = 1f - (t - tOut) / Mathf.Max(LinesFadeOut, 1e-3f);
        if (t < t2In) a2 = 0f;
        else if (t < t2Hold) a2 = (t - t2In) / Mathf.Max(LineFadeIn, 1e-3f);
        else if (t < tOut) a2 = 1f;
        else if (t < tEnd) a2 = 1f - (t - tOut) / Mathf.Max(LinesFadeOut, 1e-3f);
        Alpha1 = Mathf.Clamp(a1, 0f, 1f); Alpha2 = Mathf.Clamp(a2, 0f, 1f);
        Apply();
        if (t >= tEnd + BlackAfter) Finish();
    }
    private void Apply()
    {
        if (_l1 != null) _l1.Modulate = new Color(1f, 1f, 1f, Alpha1);
        if (_l2 != null) _l2.Modulate = new Color(1f, 1f, 1f, Alpha2);
    }
    private void Finish() { Done = true; Alpha1 = Alpha2 = 0f; Apply(); EmitSignal(SignalName.Finished); }
}
