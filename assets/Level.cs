using Godot;

/// <summary>
/// Attach to a level's root node (opt-in, not required — only levels that need
/// special intro behavior get this). Currently handles the intro camera pan
/// ("Preview Shot"): when enabled, Mario is suppressed (hidden, frozen, not
/// holding the camera — he hasn't "spawned" yet) while a hand-keyframed
/// Camera3D/AnimationPlayer rig plays a tour of the level, then Mario is
/// revealed at a spawn point once the pan finishes.
///
/// The pan rig and Mario's suppress/reveal are deliberately kept as separate,
/// swappable pieces (see SpawnMario) so a future cutscene that includes Mario
/// (not just an empty-level pan) can reuse this same structure — it would just
/// reveal him earlier (e.g. at the start) and add tracks targeting him to the
/// same AnimationPlayer, rather than needing a different system.
/// </summary>
public partial class Level : Node3D
{
    public enum EntranceType
    {
        DirectSpawn, // Mario just appears at the spawn point, no effect
        SunshineBeam, // TODO: warp-beam spawn effect, not implemented yet
    }

    [Export] public bool PreviewShot = false;
    [Export] public EntranceType Entrance = EntranceType.DirectSpawn;

    [Export] public NodePath MarioPath = "Mario";
    [Export] public NodePath IntroCameraPath;
    [Export] public NodePath IntroAnimationPlayerPath;
    [Export] public string IntroAnimationName = "intro_pan";
    [Export] public string SpawnAnimationName = "spawn_wipe";

    /// <summary>Where Mario appears once the intro finishes. Optional — if unset,
    /// he's revealed wherever he was already positioned in the scene.</summary>
    [Export] public NodePath MarioSpawnPointPath;

    /// <summary>Iris/barn-door transition ColorRects (see TransitionLayer in the
    /// level scene). Optional — if unset, the intro still plays, just without the
    /// transition overlay.</summary>
    [Export] public NodePath IrisRectPath;
    [Export] public NodePath BarnDoorVerticalRectPath;
    [Export] public NodePath BarnDoorHorizontalRectPath;

    /// <summary>The "GO!" banner shown after Mario lands (see GoTextPopup.cs).
    /// Text is configurable — same glyph-popup approach as Mario's Shine Get banner.</summary>
    [Export] public bool ShowGoText = true;

    [Export] public string GoText = "GO!";

    /// <summary>Delay after the spawn barn door finishes opening before GO!
    /// starts rising — matched to the reference clip's gap between the reveal
    /// completing and GO! first appearing.</summary>
    [Export] public float GoTextDelayAfterSpawnWipe = 0f;

    /// <summary>Pressing the jump button (button_a / key_space, same as everywhere
    /// else in Mario.cs) while the intro pan is playing skips it: the camera
    /// freezes exactly where it is (no jump-cut to wherever the pan's own closing
    /// wipe happens to sit on its timeline), the barn door closes independently
    /// from whatever it's currently at, then Mario's reveal (spawn_wipe, GO!)
    /// follows exactly as it would have anyway.</summary>
    [Export] public bool AllowIntroSkip = true;

    /// <summary>Fallback only, used if a BarnDoorVertical track can't be found on
    /// IntroAnimationName to read its authored close speed from — the skip path's
    /// independent wipe then closes over this many seconds instead.</summary>
    [Export] public float ClosingWipeDuration = 0.5f;

    /// <summary>Fires the instant Mario actually has control — right after
    /// RevealFromIntro (or immediately, deferred, if this level has no intro at
    /// all). Anything that shouldn't run during a non-interactive intro pan —
    /// e.g. a ShineTimer's AutoStart — should wait on this instead of assuming
    /// _Ready() means "the player can act now."</summary>
    [Signal] public delegate void MarioReadyEventHandler();

    private Mario _mario;
    private Camera3D _introCamera;
    private AnimationPlayer _introAnimPlayer;
    private Node3D _spawnPoint;
    private ColorRect _irisRect;
    private ColorRect _barnDoorVerticalRect;
    private ColorRect _barnDoorHorizontalRect;

    public override void _Ready()
    {
        _mario = GetNodeOrNull<Mario>(MarioPath);
        if (_mario == null)
        {
            GD.PushError($"Level: MarioPath '{MarioPath}' not found.");
            return;
        }

        if (!PreviewShot)
        {
            // No intro — Mario already has control from frame one. Deferred so
            // anything subscribing in its own _Ready() (which runs this same
            // frame, possibly after this one) still catches it.
            CallDeferred(nameof(EmitMarioReady));
            return;
        }

        _introCamera = GetNodeOrNull<Camera3D>(IntroCameraPath);
        _introAnimPlayer = GetNodeOrNull<AnimationPlayer>(IntroAnimationPlayerPath);
        _spawnPoint = GetNodeOrNull<Node3D>(MarioSpawnPointPath);
        _irisRect = GetNodeOrNull<ColorRect>(IrisRectPath);
        _barnDoorVerticalRect = GetNodeOrNull<ColorRect>(BarnDoorVerticalRectPath);
        _barnDoorHorizontalRect = GetNodeOrNull<ColorRect>(BarnDoorHorizontalRectPath);

        if (_introCamera == null || _introAnimPlayer == null)
        {
            GD.PushError(
                "Level: PreviewShot is on but IntroCameraPath/IntroAnimationPlayerPath "
                    + "aren't both set — skipping intro."
            );
            // Mario was never suppressed (that happens below) — he already has
            // control, so anything waiting on MarioReady still needs to hear it.
            CallDeferred(nameof(EmitMarioReady));
            return;
        }

        SizeTransitionRects();

        _mario.SuppressForIntro();
        _introAnimPlayer.AnimationFinished += OnIntroAnimationFinished;

        // Deferred so this wins even if Mario's own camera (current by default in
        // the scene file) or another _Ready() claims "current" this same frame.
        CallDeferred(nameof(StartIntro));
    }

    /// <summary>
    /// The transition ColorRects are laid out at an arbitrary editor-time size —
    /// stretch them to the actual runtime viewport here so the barn doors meet/part
    /// at screen center and the iris shader's rect_size (which controls how the
    /// circle radius maps to pixels) matches reality instead of stretching into an
    /// ellipse on a non-1920x1080 window.
    /// </summary>
    private void SizeTransitionRects()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;

        foreach (var rect in new[] { _irisRect, _barnDoorVerticalRect, _barnDoorHorizontalRect })
        {
            if (rect == null)
                continue;
            rect.Position = Vector2.Zero;
            rect.Size = size;
        }

        if (_irisRect?.Material is ShaderMaterial irisMat)
            irisMat.SetShaderParameter("rect_size", size);
    }

    private void StartIntro()
    {
        _introCamera.MakeCurrent();
        _introAnimPlayer.Play(IntroAnimationName);
    }

    private bool _introSkipRequested = false;

    public override void _Process(double delta)
    {
        if (!AllowIntroSkip || _introSkipRequested || _introAnimPlayer == null)
            return;
        if (!_introAnimPlayer.IsPlaying() || _introAnimPlayer.CurrentAnimation != IntroAnimationName)
            return;
        if (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space"))
            SkipIntro();
    }

    private void SkipIntro()
    {
        _introSkipRequested = true; // guard: keep this from firing again below
        _introAnimPlayer.AnimationFinished -= OnIntroAnimationFinished;

        // NOT snapping Iris here — IntroAnimationName is deliberately left
        // playing (see below), so if skip happens before its own Iris-in track
        // finishes (~0.4s), that track is still actively writing to the same
        // shader param every frame and would just overwrite a one-time set
        // made at this point on the very next tick. SpawnMario() does the
        // authoritative clear instead, at the one moment nothing can fight it.

        // Deliberately NOT stopping/seeking IntroAnimationName here — the camera
        // keeps panning exactly as authored, just hidden behind the wipe that's
        // about to cover it. It gets cut off a moment later anyway when
        // SpawnMario() below calls Play(SpawnAnimationName) on this same player
        // (replacing whatever's currently playing) — by then the screen is
        // already covered, so the cut is invisible. Earlier versions of this
        // either snapped the camera to match the pan's own closing-wipe
        // keyframes (jarring jump-cut) or froze it via Stop(keepState:true)
        // (fine, but the ask here was to let it keep moving).
        PlayClosingWipeThenSpawn();
    }

    /// <summary>
    /// Closes BarnDoorVertical independently of IntroAnimationName's timeline —
    /// from whatever value it's currently holding, over the same duration as its
    /// authored close track (see <see cref="GetAuthoredCloseDuration"/>) — then
    /// reveals Mario. This way the wipe always plays cleanly regardless of where
    /// in the pan the player skipped from.
    /// </summary>
    private void PlayClosingWipeThenSpawn()
    {
        if (_barnDoorVerticalRect?.Material is ShaderMaterial mat)
        {
            float from = (float)mat.GetShaderParameter("progress");
            float duration = GetAuthoredCloseDuration();

            var t = CreateTween();
            t.TweenMethod(
                Callable.From((float p) => mat.SetShaderParameter("progress", p)),
                from,
                0f,
                duration
            );
            t.TweenCallback(Callable.From(SpawnMario));
        }
        else
        {
            SpawnMario();
        }
    }

    /// <summary>How long IntroAnimationName's own BarnDoorVertical close track
    /// takes (its last key time minus its first), so the skip path's independent
    /// wipe matches the pan's authored closing speed. Falls back to
    /// ClosingWipeDuration if that track can't be found.</summary>
    private float GetAuthoredCloseDuration()
    {
        var anim = _introAnimPlayer.GetAnimation(IntroAnimationName);
        if (anim != null)
        {
            for (int i = 0; i < anim.GetTrackCount(); i++)
            {
                int keyCount = anim.TrackGetKeyCount(i);
                if (keyCount < 2 || !((string)anim.TrackGetPath(i)).Contains("BarnDoorVertical"))
                    continue;
                float first = (float)anim.TrackGetKeyTime(i, 0);
                float last = (float)anim.TrackGetKeyTime(i, keyCount - 1);
                return Mathf.Max(0.05f, last - first);
            }
        }
        return ClosingWipeDuration;
    }

    private void OnIntroAnimationFinished(StringName animName)
    {
        if (animName != IntroAnimationName)
            return;
        _introAnimPlayer.AnimationFinished -= OnIntroAnimationFinished;
        SpawnMario();
    }

    private void SpawnMario()
    {
        // Authoritative clear: whatever state IntroAnimationName's Iris-in
        // track left this in (finished normally, or cut short by a skip that
        // landed before it finished), this is the one moment nothing can fight
        // it — Play(SpawnAnimationName) below replaces IntroAnimationName on
        // this same player outright, so no other animation is still writing to
        // this shader param after this point.
        if (_irisRect?.Material is ShaderMaterial irisMat)
            irisMat.SetShaderParameter("progress", 1f);

        Transform3D? spawnXform = _spawnPoint?.GlobalTransform;

        switch (Entrance)
        {
            case EntranceType.SunshineBeam:
                // TODO: play the beam-down effect once it exists, then reveal.
                // Falls back to a plain reveal for now so the intro still works.
                _mario.RevealFromIntro(spawnXform);
                break;

            case EntranceType.DirectSpawn:
            default:
                _mario.RevealFromIntro(spawnXform);
                break;
        }

        // Mario is already positioned/visible underneath by this point — the
        // horizontal barn door just uncovers him, like he's landing in the level.
        _introAnimPlayer.Play(SpawnAnimationName);

        if (ShowGoText)
        {
            var spawnAnim = _introAnimPlayer.GetAnimation(SpawnAnimationName);
            float wipeLength = spawnAnim != null ? (float)spawnAnim.Length : 1.2f;
            GetTree().CreateTimer(wipeLength + GoTextDelayAfterSpawnWipe).Timeout += SpawnGoTextPopup;
        }

        // RevealFromIntro above already re-enabled Mario's input/physics — the
        // barn door and GO! banner are purely cosmetic overlays happening on
        // top of a Mario who can already move, so control starts here, not
        // whenever those finish.
        EmitMarioReady();
    }

    private void EmitMarioReady() => EmitSignal(SignalName.MarioReady);

    /// <summary>"GO!" banner seen when Mario lands — spawned fresh, frees itself
    /// when its animation finishes (see GoTextPopup.cs), not a persistent HUD element.</summary>
    private void SpawnGoTextPopup()
    {
        var popupScene = GD.Load<PackedScene>("res://Font/HudElements/GoTextPopup.tscn");
        if (popupScene == null)
            return;

        var popup = popupScene.Instantiate<GoTextPopup>();
        popup.PopupText = GoText; // dynamic word; set before entering the tree

        var layer = new CanvasLayer { Layer = 100 };
        layer.AddChild(popup);
        AddChild(layer);
    }
}
