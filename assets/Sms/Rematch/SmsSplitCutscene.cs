using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 188: THE SPLIT'S CUTSCENE (Jaysonn: "a little cutscene that plays when he splits into two, and three, showing three of him will now be attacking;
/// make it King Boo themed"). <see cref="SmsKingBooRematch"/> makes one as the hurt that earns a copy ends (177's split: every CopyEveryHits hits, up to
/// MaxCopies) and it runs on its own, ProcessMode Always with the WORLD FROZEN (the tree paused, as the Shine's appear demo does: Mario, the Bomb Boos, the
/// supply, the game camera held; King Boo, his copies and this node run on). A cutscene camera cuts to a LINE across the arena's middle facing Mario --
/// SplitLineSpacing apart, the real one in the middle -- from Mario's side (SplitCamDistance out, 0 = far enough to frame the line; SplitCamHeight up;
/// SplitCamFov). The beats: King Boo vanishes in a poof where he was (0 s); comes back at his place in the line out of a boil of violet-black ghost mist
/// (SplitMist) with the arrival's flash, fading in, and LAUGHS (the long laugh); his copies come in beside him one by one, SplitCopyEvery apart, each the
/// same way (a copy already out teleports to its place; the new one is made here, SpawnCopyHeld); then all of them turn on Mario and LAUGH TOGETHER -- the
/// copies a shade off his pitch either way (SplitLaughSpread: a chorus of him) -- for SplitHoldSeconds, under a card line only when SplitLine2 / SplitLine3
/// have text (189, Jaysonn: "no need for the text on the screen" -- none by default); the camera goes back, the world runs again and every one of them is in
/// the wait at his place: all attacking.
/// A (or space) skips to the end (SplitSkipAllowed). Signals Finished.
/// </summary>
public partial class SmsSplitCutscene : Node
{
    [Export] public bool DebugPrint;
    [Signal] public delegate void FinishedEventHandler();

    public enum Phase { Idle, Running, Done }
    public Phase State { get; private set; } = Phase.Idle;
    public float Time => _t;
    public int LineCount => _line.Count;
    public Vector3 SlotOf(int i) => i >= 0 && i < _slots.Count ? _slots[i] : Vector3.Zero;
    public SmsKingBooRematch BooAt(int i) => i >= 0 && i < _line.Count ? _line[i] : null;
    public Camera3D Camera => _cam;
    public SmsRematchCard Card => _card;
    public bool WorldFrozen => _paused;
    public bool Skipped { get; private set; }
    public int Laughs { get; private set; }
    public bool ChorusDone { get; private set; }
    public float CamDistance { get; private set; }

    private SmsKingBooRematch _boss; private Mario _mario;
    private readonly List<SmsKingBooRematch> _line = new();          // in line order, left to right as Mario sees them
    private readonly List<Vector3> _slots = new();
    private readonly List<float> _showAt = new();                      // when each comes into the line
    private readonly List<int> _stage = new();                         // 0 waiting, 1 shown (fading in), 2 laughed
    private readonly List<float> _laughPitch = new();
    private Camera3D _cam, _gameCam; private SmsRematchCard _card; private Node _cardHolder;
    private float _t, _tAll, _tEnd, _fadeSeconds = 0.55f, _laughAfter = 0.35f; private bool _paused, _allLaughed, _ended; private readonly List<bool> _popped = new();   // 194
    private readonly List<Node> _alwaysed = new();

    /// <summary>Begun by the real one as his hurt ends: the copy made (held unseen), the line laid out, the world frozen, the camera cut.</summary>
    public void Begin(SmsKingBooRematch boss, Mario mario)
    {
        _boss = boss; _mario = mario;
        ProcessMode = ProcessModeEnum.Always;
        // the line: the real one in the middle, the copies already out beside him, the new one last; left to right as Mario sees them
        var copies = new List<SmsKingBooRematch>();
        foreach (var c in boss.Copies) if (c != null && IsInstanceValid(c) && c.State != SmsKingBooRematch.Phase.Gone) copies.Add(c);
        var fresh = boss.SpawnCopyHeld();
        if (fresh != null) copies.Add(fresh);
        int n = copies.Count + 1;
        Vector3 mid = boss.ArenaCentre; float fy = boss.FloorYAt(mid); if (float.IsNaN(fy)) fy = mid.Y;
        Vector3 centre = new Vector3(mid.X, fy + boss.HoverOverFloor, mid.Z);
        Vector3 toMario = mario.GlobalPosition - centre; toMario.Y = 0f; toMario = toMario.LengthSquared() > 1e-4f ? toMario.Normalized() : Vector3.Back;
        Vector3 side = new Vector3(toMario.Z, 0f, -toMario.X);                                                     // Mario's left along the line
        float spacing = Mathf.Max(3f, boss.SplitLineSpacing);
        var order = new List<SmsKingBooRematch>();                                                                  // slot 0..n-1 left to right
        int midSlot = n / 2; int ci = 0;
        for (int i = 0; i < n; i++) order.Add(i == midSlot ? boss : copies[ci++]);
        for (int i = 0; i < n; i++)
        {
            float off = (i - (n - 1) * 0.5f) * spacing;
            Vector3 s = centre + side * off; float sy = boss.FloorYAt(s); if (!float.IsNaN(sy)) s.Y = sy + boss.HoverOverFloor;
            _line.Add(order[i]); _slots.Add(s); _stage.Add(0); _popped.Add(false);
        }
        _fadeSeconds = Mathf.Max(0.2f, boss.SplitFadeSeconds);                                                     // 194: the gather's length (0.3 before)
        // the beats: the real one first (he poofs at 0, comes in at 0.45), then the copies -- those already out first, the new one last -- SplitCopyEvery apart
        float every = Mathf.Max(0.2f, boss.SplitCopyEvery);
        for (int i = 0; i < n; i++) { _showAt.Add(0f); _laughPitch.Add(1f); }
        _showAt[midSlot] = 0.45f;
        float next = 0.45f + every; int ck = 0;
        foreach (var c in copies)
        {
            int slot = _line.IndexOf(c);
            _showAt[slot] = next; next += every;
            _laughPitch[slot] = 1f + (ck % 2 == 0 ? 1f : -1f) * boss.SplitLaughSpread * (1 + ck / 2);              // the copies a shade up, a shade down: the chorus
            ck++;
        }
        _tAll = next - every + _fadeSeconds + _laughAfter + 0.35f;                                                   // the last one in, faded, laughed: the chorus
        _tEnd = _tAll + Mathf.Max(0.3f, boss.SplitHoldSeconds);
        // everyone into the Split phase (their moves called off), running through the freeze
        foreach (var b in _line) { b.SplitBegin(); Always(b); }
        boss.SplitHide();                                                                                           // the poof where he was
        // the world frozen
        var tree = GetTree();
        if (tree != null && !tree.Paused) { tree.Paused = true; _paused = true; }
        // the camera: Mario's side of the line, framing it
        _gameCam = GetViewport().GetCamera3D();
        // he is BIG: a body some 9 m tall and 9 m across (the 182 shots: 8.5 m at 22 m), lifted 3.6 m by the laugh clip -- the frame fits the line's width
        // (the bodies' edges) and its height (the floor under them to the laughing crowns), whichever asks the longer lens
        float halfWidth = (n - 1) * 0.5f * spacing + 5.5f, halfHeight = 6.5f;
        float fov = Mathf.Clamp(boss.SplitCamFov, 20f, 110f);
        float aspect = GetViewport().GetVisibleRect().Size.Aspect(); if (aspect <= 0f) aspect = 16f / 9f;
        float tanV = Mathf.Tan(Mathf.DegToRad(fov) * 0.5f), tanH = tanV * aspect;
        float dist = boss.SplitCamDistance > 0f ? boss.SplitCamDistance : Mathf.Max(halfWidth / Mathf.Max(0.2f, tanH), halfHeight / Mathf.Max(0.2f, tanV)) * 1.25f + 2f;
        _cam = new Camera3D { Name = "SplitCutsceneCamera", Fov = fov, ProcessMode = ProcessModeEnum.Always };
        (tree?.CurrentScene ?? GetParent()).AddChild(_cam);
        _cam.GlobalPosition = centre + toMario * dist + Vector3.Up * boss.SplitCamHeight;
        _cam.LookAt(centre + Vector3.Up * boss.SplitCamLookUp, Vector3.Up);                                         // 190: SplitCamLookUp over the hover (188's 4, the bodies' middle, sat them low)
        _cam.Current = true;
        // the card: a line in the intro card's style, over the scene (no black); it fades in once the first of him is up
        string text = n >= 3 ? boss.SplitLine3 : boss.SplitLine2;
        if (!string.IsNullOrEmpty(text))
        {
            _card = new SmsRematchCard { Name = "SplitCard", Line1Text = text, Line2Text = "", FontSize = 44, ProcessMode = ProcessModeEnum.Always };
            _card.BlackBefore = 0.9f; _card.LineFadeIn = 0.4f; _card.Line1Hold = Mathf.Max(0.3f, _tEnd - 0.9f - 0.4f - 0.45f - 0.4f); _card.LineGap = 0f; _card.Line2Hold = 0f; _card.LinesFadeOut = 0.45f; _card.BlackAfter = 0f;
            (tree?.CurrentScene ?? GetParent()).AddChild(_card);
        }
        State = Phase.Running; _t = 0f;
        CamDistance = dist;
        if (DebugPrint) GD.Print($"[SMS split] {Name}: {n} of him -- the line about {centre} (spacing {spacing}, Mario {toMario}), the camera {dist:F1} m out {boss.SplitCamHeight} m up at {fov} deg looking {boss.SplitCamLookUp} m over the hover; the chorus at {_tAll:F2} s, the end at {_tEnd:F2} s; the world frozen {_paused}; the card '{text}'");
    }
    private void Always(Node n) { if (n != null && n.ProcessMode != ProcessModeEnum.Always) { _alwaysed.Add(n); n.ProcessMode = ProcessModeEnum.Always; } }

    public override void _Process(double delta)
    {
        if (State != Phase.Running) return;
        float dt = (float)delta; _t += dt;
        if (_boss == null || !IsInstanceValid(_boss)) { End(); return; }
        if (_boss.SplitSkipAllowed && !Skipped && (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))) { Skipped = true; _card?.Skip(); _t = Mathf.Max(_t, _tAll); }
        for (int i = 0; i < _line.Count; i++)
        {
            var b = _line[i]; if (b == null || !IsInstanceValid(b)) continue;
            float at = _showAt[i];
            if (_stage[i] == 0 && (_t >= at || Skipped))
            {
                _stage[i] = 1;
                if (b.IsCopy && b.State != SmsKingBooRematch.Phase.Split) b.SplitBegin();
                b.SplitHide();                                                                                    // a copy already out: the poof where it was
                b.SplitShowAt(_slots[i], teleportFx: false); b.SplitFade(Skipped ? 1f : 0f);
                if (!Skipped) _boss.SplitGather(_slots[i]); else _boss.SplitMist(_slots[i]);                       // 194: the mist GATHERS into him as he fades in (skipped: the boil at once)
                if (DebugPrint) GD.Print($"[SMS split] {Name}: {b.Name} comes into the line at slot {i} {_slots[i]} ({_t:F2} s) -- the mist gathering over {_fadeSeconds:F2} s");
            }
            if (_stage[i] == 1)
            {
                float k = Skipped ? 1f : Mathf.Clamp((_t - at) / _fadeSeconds, 0f, 1f);
                float flicker = k < 1f ? 1f - Mathf.Clamp(_boss.SplitFlicker, 0f, 1f) * (0.5f + 0.5f * MathF.Sin((_t - at) * MathF.Tau * 9f)) : 1f;   // 194: a ghost flickering in
                float body = Mathf.Clamp((k - 0.3f) / 0.7f, 0f, 1f);                                                                                   // the body FORMS as the mist arrives: nothing for the gather's first third, then up quick
                b.SplitFade(MathF.Pow(body, 0.7f) * flicker);
                if (k >= 1f && !_popped[i]) { _popped[i] = true; if (!Skipped) _boss.SplitPop(_slots[i]); }       // 194: solid -- the mist boils back out of him, the flash
                if (k >= 1f && (_t >= at + _fadeSeconds + _laughAfter || Skipped)) { _stage[i] = 2; if (!Skipped) { b.SplitLaugh(_laughPitch[i]); Laughs++; } }
            }
        }
        if (!_allLaughed && _t >= _tAll)
        {
            _allLaughed = true; ChorusDone = true;
            foreach (var b in _line) if (b != null && IsInstanceValid(b)) { b.SplitFace(); }
            for (int i = 0; i < _line.Count; i++) if (_line[i] != null && IsInstanceValid(_line[i])) { _line[i].SplitLaugh(_laughPitch[i]); Laughs++; }
            if (DebugPrint) GD.Print($"[SMS split] {Name}: the CHORUS -- {_line.Count} of him laughing at Mario ({_t:F2} s)");
        }
        if (_t >= _tEnd) End();
    }

    /// <summary>Over: the camera back, the world running, every one of him in the wait at his place; freed.</summary>
    private void End()
    {
        if (_ended) return; _ended = true;
        State = Phase.Done;
        for (int i = 0; i < _line.Count; i++)
        {
            var b = _line[i]; if (b == null || !IsInstanceValid(b)) continue;
            if (_stage[i] == 0) { b.SplitShowAt(_slots[i]); }
            b.SplitFade(1f); b.SplitEnd();
        }
        foreach (var n in _alwaysed) if (n != null && IsInstanceValid(n)) n.ProcessMode = ProcessModeEnum.Inherit;
        if (_gameCam != null && IsInstanceValid(_gameCam)) _gameCam.Current = true;
        if (_cam != null && IsInstanceValid(_cam)) { _cam.QueueFree(); _cam = null; }
        if (_card != null && IsInstanceValid(_card)) { _card.QueueFree(); _card = null; }
        var tree = GetTree();
        if (_paused && tree != null) tree.Paused = false; _paused = false;
        if (DebugPrint) GD.Print($"[SMS split] {Name}: over at {_t:F2} s{(Skipped ? " (skipped)" : "")} -- {_line.Count} of him in the wait, the world running");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
}
