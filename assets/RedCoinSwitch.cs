using Godot;
using System.Linq;

/// <summary>
/// SMS-style red coin switch. Ground pound it to start the level's ShineTimer
/// — fires once; the switch itself doesn't reset (that's on the level/ShineTimer around it).
///
/// Pass 109 (SMS 1:1, TRedCoinSwitch MapObjTown.cpp 553 + the ex stages' timerredcoin.sb): the pound plays the press clip, the pad goes dead
/// for (clip end x 2 + 60) frames, the press sound (MSD_SE_OBJ_AP_BUTTON slot), the switch's collision goes; when the clip ends the stage
/// script's flag is set and the hidden message BOARD (an SmsNpc of the Board kind under the switch) is warped 400 u in front of Mario and
/// force-talked: the BOARD window shows MessageText (sys_message 30 in the game), A / B closes it with the race-start sound, the camera
/// comes back, and when the talk has fully ended (the script's isTalkModeNow loop) the 8 red coins appear, the timer starts and the HUD's
/// red coin counter comes in at 0. LEGACY RETIRED: the SignPopup banner for this switch (SignPopupScene stays as an unused export so the
/// level scenes load clean). Without SMS physics on Mario, or with no board, the challenge starts without a message.
///
/// Detection mirrors Nail.cs's ground-pound latch pattern exactly (same
/// double-count bug it avoids: the switch doesn't move out from under Mario
/// like the nail does, but latching per-pound keeps behavior consistent and
/// makes it safe even if someone re-ground-pounds on top of it later).
///
/// Scene structure expected (see red_coin_switch.tscn):
///   RedCoinSwitch (this script, Node3D — the raw RedCoinSwitch.glb instance)
///   ├─ Armature/Skeleton3D/Mesh_0 (visual, from the GLB)
///   ├─ AnimationPlayer ("redcoinswitch" clip — the plunger-press squash,
///   │    baked into the model itself, no need to author it)
///   ├─ SwitchBody (StaticBody3D) — so Mario can stand on it
///   │    └─ CollisionShape3D
///   └─ HitZone (Area3D, just above the switch top) — ground-pound detection
///        └─ CollisionShape3D
///
/// SwitchBody/HitZone collision sizes were placed by eye (no reliable AABB
/// read on this GLB's skinned mesh via tooling) — nudge them in the editor to
/// match the actual model once you can see it in-viewport.
/// </summary>
public partial class RedCoinSwitch : Node3D
{
    [Export] public NodePath ShineTimerPath;

    /// <summary>Where the sign popup gets parented (a CanvasLayer, typically).
    /// Falls back to the scene root if unset.</summary>
    [Export] public NodePath SignLayerPath;

    /// <summary>Mario's SunshineCamera rig — optional. When set, the camera
    /// glides into a TalkingPOV shot (Mario frozen, framed beside him) the
    /// instant the sign appears, and glides back out when it's dismissed.</summary>
    [Export] public NodePath CameraPath;

    /// <summary>Unused since pass 109 (the message goes through the SMS board window); kept so the level scenes that set it load clean.</summary>
    [Export] public PackedScene SignPopupScene;
    [ExportGroup("SMS message board (pass 109)")]
    /// <summary>The hidden board NPC that speaks the message (SmsNpc, Board kind). Empty = SignBoard.tscn is instanced hidden under the switch at
    /// start. The game's ex stages place an invisible NPCBoard ("見えない看板ＮＰＣ") for this.</summary>
    [Export] public NodePath MessageBoardPath;
    /// <summary>MSD_SE_OBJ_AP_BUTTON at the pound (a slot; no stream yet).</summary>
    [Export] public AudioStreamPlayer PressSound;
    /// <summary>MSD_SE_SY_RACE_START: the message window's close sound (Talk2D2.cpp 479 plays it for message 30 instead of the select tick).</summary>
    [Export] public AudioStreamPlayer RaceStartSound;
    /// <summary>TRedCoinSwitch::receiveMessage: the pad is disabled for the press clip's end x 2 + this many frames (0x3C).</summary>
    [Export] public int PadDisableExtraFrames = 60;
    /// <summary>MSD_SE_SY_COIN_DISAPPEAR: the red coins' death when the timer runs out (TItem::appeared, Item.cpp 50; pass 114). A slot, no stream yet.</summary>
    [Export] public AudioStreamPlayer CoinDisappearSound;
    /// <summary>DEVIATION knob (pass 114). The game's switch stays pressed for the rest of the stage visit after a time-up (control() state 4, the flag
    /// kept): no retry without leaving. true = this switch re-arms when its coins die -- the clip rewound, the collision back, a new pound starts a new
    /// challenge -- for levels with no exit. false = the game.</summary>
    [Export] public bool SmsRearmOnTimeUp = false;
    [ExportGroup("")]

    /// <summary>The red coin scene instantiated when this switch is hit.</summary>
    [Export] public PackedScene RedCoinScene;

    /// <summary>Node3D containing the editor-placed Marker3D spawn points.</summary>
    [Export] public NodePath RedCoinSpawnsPath;

    /// <summary>Shine awarded after this switch's eight spawned coins are collected.</summary>
    [Export] public PackedScene RedCoinShineScene;

    /// <summary>Editor-authored location and scale for the red-coin Shine.</summary>
    [Export] public NodePath RedCoinShineSpawnPath;

    [Export] public int RequiredRedCoins = 8;

    [Export] public bool EnableRedCoinShineCamera = true;
    [Export] public float RewardCameraTargetHeight = 2.4f;
    [Export] public float RewardCameraDistance = 7.5f;
    [Export] public float RewardCameraHeight = 1.6f;
    [Export] public float RewardCameraTravelDuration = 0.3f;

    [Export(PropertyHint.MultilineText)]
    public string MessageText = "Collect 8 red coins\nbefore the timer\nruns out!\n\nGOOD LUCK!";

    private bool _triggered = false;
    private bool _redCoinsSpawned = false;
    private int _collectedRedCoins = 0;
    private bool _completionReached = false;
    private bool _redCoinShineSpawned = false;
    private bool _signAcknowledged = false;
    private bool _challengeStarted = false;
    private readonly System.Collections.Generic.List<RedCoin> _smsCoins = new();   // the coins of this challenge, in spawn order (pass 114)
    private readonly System.Collections.Generic.List<Area3D> _smsCoinPickups = new(); // their pickup areas, off for the first second (TItem::killByTimer's HIT_FILTER_NO_COLLISION)
    private bool _smsTimeUp = false;          // the coins' timers ran out (TItem::appeared -> makeObjDead for all eight)
    private bool _smsCoinsUntouchable = false;
    private int _smsUntouchableTicks = 0; private float _smsUntouchableAccum = 0f; // the first second, on its own 120 Hz clock (not the ShineTimer's)
    /// <summary>The game's coin flush: hidden every other 10 ticks (1 / 12 s) through the last 360 ticks (3 s) of its life (TMapObjGeneral::perform 584,
    /// mNormalFlushTime / mNormalFlushInterval); untouchable for its first 120 ticks (TItem::initMapObj mUntouchableFrames).</summary>
    public const int SmsFlushTicks = 360, SmsFlushInterval = 10, SmsUntouchableTicks = 120;
    private bool _pressed = false;            // MSF_RED_COIN_SWITCH_PRESSED: set when the press clip ends (control() state 2 -> 3)
    private bool _talkRequested = false;      // the script's forceTalk issued
    private bool _talkSeen = false;           // the forced talk started (isTalkModeNow was true)
    private SmsNpc _board;
    private Mario _messageMario;              // the Mario who pounded it (his _lastHitter latch clears when the pound ends; the message outlives that)
    /// <summary>The flag / the coins' appearance, for the drives (pass 109).</summary>
    public bool Pressed => _pressed;
    public bool ChallengeStarted => _challengeStarted;
    public SmsNpc MessageBoard => _board;
    /// <summary>The live coins / the time-up, for the drives (pass 114).</summary>
    public int LiveRedCoins { get { PruneCoins(); return _smsCoins.Count; } }
    public bool TimeUp => _smsTimeUp;
    public bool CoinsUntouchable => _smsCoinsUntouchable;

    // Same per-pound latch as Nail.cs: only clears once Mario's state has
    // fully left the ground-pound family, so a single pound can't double-fire.
    private bool _hitThisPound = false;
    private Mario _lastHitter;

    private AnimationPlayer _anim;
    private ShineTimer _shineTimer;
    private SunshineCamera _camera;

    private Camera3D _rewardCamera;
    private Camera3D _cameraToRestore;
    private Tween _rewardCameraTween;

    public override void _Ready()
    {
        _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        _shineTimer = GetNodeOrNull<ShineTimer>(ShineTimerPath);
        _camera = GetNodeOrNull<SunshineCamera>(CameraPath);

        var hitZone = GetNodeOrNull<Area3D>("HitZone");
        if (hitZone == null)
        {
            GD.PushError($"[RedCoinSwitch] '{Name}' is missing a HitZone Area3D child — it won't react to ground pounds.");
            return;
        }
        hitZone.BodyEntered += OnHitZoneEntered;
        SetupMessageBoard();
    }

    /// <summary>The message board: the SmsNpc at MessageBoardPath, else SignBoard.tscn instanced under the switch -- hidden either way (the game's
    /// setHide4LiveActor at the stage script's start), with MessageText as its page and the race-start slot as its close sound.</summary>
    private void SetupMessageBoard()
    {
        _board = MessageBoardPath != null && !MessageBoardPath.IsEmpty ? GetNodeOrNull<SmsNpc>(MessageBoardPath) : null;
        if (_board == null)
        {
            var scene = ResourceLoader.Exists(MessageBoardScenePath) ? GD.Load<PackedScene>(MessageBoardScenePath) : null;
            if (scene == null) { GD.PushWarning($"[RedCoinSwitch] '{Name}': no message board ({MessageBoardScenePath} missing); the challenge will start without a message."); return; }
            _board = scene.Instantiate<SmsNpc>();
            _board.Name = "MessageBoard";
            _board.Hidden = true;
            _board.CanTalk = false;                                    // only the forced talk reaches it (Hidden refuses the proximity talk too)
            AddChild(_board);
            _board.GlobalPosition = GlobalPosition;
        }
        else _board.Hidden = true;
        _board.Lines = new[] { MessageText ?? "" };
        _board.TalkCloseSound = RaceStartSound;
    }
    private const string MessageBoardScenePath = "res://assets/Sms/Npc/SignBoard.tscn";

    public override void _PhysicsProcess(double delta)
    {
        TickMessage();
        TickCoinTimer(delta);
        if (!_hitThisPound)
            return;
        if (_lastHitter == null || !IsInstanceValid(_lastHitter))
        {
            _hitThisPound = false;
            _lastHitter = null;
            return;
        }
        var s = _lastHitter.stateOfMario;
        bool stillPounding =
            s == Mario.MarioState.groundPoundStartup
            || s == Mario.MarioState.groundPoundFalling
            || s == Mario.MarioState.groundPoundLanding;
        if (!stillPounding)
        {
            _hitThisPound = false;
            _lastHitter = null;
        }
    }

    private void OnHitZoneEntered(Node3D body)
    {
        if (_triggered)
            return;
        if (body is not Mario mario)
            return;
        if (_hitThisPound)
            return;

        bool isPounding =
            mario.stateOfMario == Mario.MarioState.groundPoundFalling
            || mario.stateOfMario == Mario.MarioState.groundPoundLanding;
        if (!isPounding)
            return;

        _hitThisPound = true;
        _lastHitter = mario;
        Trigger();
    }

    public void Trigger()
    {
        _triggered = true;

        // receiveMessage(HIT_MESSAGE_HIP_DROP): startBck("redcoinswitch"); the pad disabled for the clip's end x 2 + 60 frames; MSD_SE_OBJ_AP_BUTTON;
        // removeMapCollision + HIT_FILTER_NO_COLLISION; state 2 (the flag follows when the clip ends)
        float clipFrames = 0f;
        if (_anim != null && _anim.HasAnimation("redcoinswitch"))
        {
            _anim.Play("redcoinswitch");
            clipFrames = (float)_anim.GetAnimation("redcoinswitch").Length * 30f;
        }
        var mario = _lastHitter;
        _messageMario = mario;
        if (mario != null && IsInstanceValid(mario) && mario.UseSmsPhysics)
            mario.SmsDisablePad(Mathf.RoundToInt(clipFrames * 2f) + PadDisableExtraFrames);
        PressSound?.Play();
        foreach (var shape in GetNodeOrNull<Node>("SwitchBody")?.FindChildren("*", "CollisionShape3D", true, false) ?? new Godot.Collections.Array<Node>())
            if (shape is CollisionShape3D cs) cs.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);   // Trigger runs inside the HitZone's signal (the physics flush): a direct write is refused

        if (_anim != null && _anim.IsPlaying() && _anim.CurrentAnimation == "redcoinswitch")
            _anim.AnimationFinished += OnPressClipFinished;
        else
            OnPressClipFinished("");
    }

    /// <summary>control() state 2: the press clip ended -> MSF_RED_COIN_SWITCH_PRESSED (the stage script wakes). The message follows in TickMessage.</summary>
    private void OnPressClipFinished(StringName anim)
    {
        if (_pressed) return;
        _pressed = true;
        if (_anim != null) _anim.AnimationFinished -= OnPressClipFinished;
    }

    /// <summary>The stage script (timerredcoin.sb), one step a frame: on the flag, warpFrontToMario + forceTalk(board) (retried by Mario's director
    /// until he is grounded and not jumping), talkAndClose(30), then -- when isTalkModeNow is false again (the window gone, Mario freed) --
    /// appear8RedCoinsAndTimer + appearRedCoinCounter. No board / no SMS physics: the challenge starts without a message.</summary>
    private void TickMessage()
    {
        if (!_pressed || _challengeStarted) return;
        var mario = _messageMario;
        bool sms = mario != null && IsInstanceValid(mario) && mario.UseSmsPhysics && _board != null && IsInstanceValid(_board);
        if (!sms) { _signAcknowledged = true; BeginRedCoinChallenge(); return; }
        if (!_talkRequested)
        {
            _board.Lines = new[] { MessageText ?? "" };
            _board.TalkCloseSound = RaceStartSound;
            mario.SmsForceStartTalk(_board);
            _talkRequested = true;
            return;
        }
        if (mario.SmsForceTalkPending) return;                             // the script yields until forceTalk takes
        if (mario.SmsIsTalking || _board.IsTalking) { _talkSeen = true; return; }
        if (!_talkSeen) { GD.PushWarning($"[RedCoinSwitch] '{Name}': the forced talk did not start; starting the challenge without a message."); }
        _signAcknowledged = true;
        BeginRedCoinChallenge();
    }

    private void SpawnRedCoins()
    {
        if (_redCoinsSpawned)
            return;

        if (RedCoinScene == null)
        {
            GD.PushWarning($"[RedCoinSwitch] '{Name}' has no RedCoinScene assigned; no red coins were spawned.");
            return;
        }

        var spawnRoot = GetNodeOrNull<Node3D>(RedCoinSpawnsPath);
        if (spawnRoot == null)
        {
            GD.PushWarning($"[RedCoinSwitch] '{Name}' could not find its RedCoinSpawnsPath; no red coins were spawned.");
            return;
        }

        int spawnCount = 0;
        foreach (var child in spawnRoot.GetChildren())
        {
            if (child is not Marker3D marker)
                continue;

            var coin = RedCoinScene.Instantiate<RedCoin>();
            coin.Collected += OnGroupRedCoinCollected;
            spawnRoot.AddChild(coin);
            coin.GlobalTransform = marker.GlobalTransform;
            spawnCount++;
            coin.Name = $"RedCoin{spawnCount:00}";
            coin.PlaySpawnPuff();
            _smsCoins.Add(coin);
            // killByTimer: HIT_FILTER_NO_COLLISION for the first 120 ticks -- the coin cannot be taken for a second (its pickup Area3D, if one is reachable)
            Area3D pickup = ((Node)coin) as Area3D ?? coin.FindChildren("*", nameof(Area3D), true, false).OfType<Area3D>().FirstOrDefault();
            if (pickup != null) { pickup.SetDeferred(Area3D.PropertyName.Monitoring, false); pickup.SetDeferred(Area3D.PropertyName.Monitorable, false); _smsCoinPickups.Add(pickup); }
        }

        _redCoinsSpawned = true;
        _smsTimeUp = false;
        _smsCoinsUntouchable = _smsCoinPickups.Count > 0;
        _smsUntouchableTicks = _smsCoinsUntouchable ? SmsUntouchableTicks : 0;
        _smsUntouchableAccum = 0f;
        if (spawnCount > 0 && _smsCoinPickups.Count == 0)
            GD.Print($"[RedCoinSwitch] '{Name}': no pickup Area3D found on the red coins -- the game's first untouchable second is skipped (pass 114)");

        if (spawnCount != RequiredRedCoins)
            GD.PushWarning($"[RedCoinSwitch] '{Name}' spawned {spawnCount} red coins; expected {RequiredRedCoins} Marker3D children.");
    }

    private void OnGroupRedCoinCollected()
    {
        if (_completionReached)
            return;

        _collectedRedCoins++;
        GD.Print($"[RedCoinSwitch] group progress {_collectedRedCoins}/{RequiredRedCoins}");

        if (_collectedRedCoins < RequiredRedCoins)
            return;

        _completionReached = true;
        _shineTimer?.StopTimer();

        // Collection is reported from an Area3D body-entered callback. Defer
        // adding the reward until physics has finished flushing that event.
        Callable.From(SpawnRedCoinShine).CallDeferred();
    }

    private void SpawnRedCoinShine()
    {
        if (_redCoinShineSpawned)
            return;

        if (RedCoinShineScene == null)
        {
            GD.PushWarning($"[RedCoinSwitch] '{Name}' has no RedCoinShineScene assigned; reward was not spawned.");
            return;
        }

        var spawn = GetNodeOrNull<Node3D>(RedCoinShineSpawnPath);
        if (spawn == null)
        {
            GD.PushWarning($"[RedCoinSwitch] '{Name}' could not find RedCoinShineSpawnPath; reward was not spawned.");
            return;
        }

        var shine = RedCoinShineScene.Instantiate<ShineSprite>();
        var parent = GetTree().CurrentScene ?? GetParent();
        _redCoinShineSpawned = true;
        if (string.IsNullOrEmpty(shine.ShineId))
            shine.ShineId = $"{GetTree().CurrentScene?.SceneFilePath ?? ""}:{Name}:red_coins";   // 118c: the reward's save flag (a spawned shine has no scene of its own)
        parent.AddChild(shine);
        shine.GlobalTransform = spawn.GlobalTransform;
        if (!(shine.SmsAppearGame && shine.SmsAppearCamera)) PlayRedCoinShineRevealCamera(shine);   // pass 117: the game's appear demo brings its own camera
        shine.PlaySpawnEntrance();

        GD.Print($"[RedCoinSwitch] red-coin Shine spawned at {shine.GlobalPosition}");
    }

    /// <summary>
    /// Uses the same ownership pattern as the level intro: a dedicated Camera3D
    /// becomes current for a short authored move, then returns ownership to the
    /// exact camera that was active before the reveal. The tween processes while
    /// paused so it stays synchronized with ShineSprite.PlaySpawnEntrance().
    /// </summary>
    public void PlayRedCoinShineRevealCamera(ShineSprite shine)
    {
        if (!EnableRedCoinShineCamera || shine == null)
            return;
        if (_rewardCamera != null && IsInstanceValid(_rewardCamera))
            return;

        var gameplayCamera = GetViewport().GetCamera3D();
        if (gameplayCamera == null)
        {
            GD.PushWarning($"[RedCoinSwitch] '{Name}' could not find the active gameplay camera; skipping reward shot.");
            return;
        }

        _cameraToRestore = gameplayCamera;
        Transform3D gameplayTransform = gameplayCamera.GlobalTransform;

        _rewardCamera = new Camera3D
        {
            Name = "RedCoinRewardCamera",
            ProcessMode = ProcessModeEnum.Always,
            TopLevel = true,
            Fov = gameplayCamera.Fov,
            Near = gameplayCamera.Near,
            Far = gameplayCamera.Far,
            CullMask = gameplayCamera.CullMask,
        };
        GetTree().Root.AddChild(_rewardCamera);
        _rewardCamera.GlobalTransform = gameplayTransform;

        Vector3 target = shine.GlobalPosition + Vector3.Up * RewardCameraTargetHeight;
        Vector3 away = gameplayTransform.Origin - target;
        away.Y = 0f;
        if (away.LengthSquared() < 0.001f)
        {
            away = gameplayTransform.Basis.Z;
            away.Y = 0f;
        }
        if (away.LengthSquared() < 0.001f)
            away = Vector3.Back;
        away = away.Normalized();

        Vector3 revealPosition = target
            + away * Mathf.Max(RewardCameraDistance, 0.1f)
            + Vector3.Up * RewardCameraHeight;
        Transform3D revealTransform = new Transform3D(Basis.Identity, revealPosition)
            .LookingAt(target, Vector3.Up);

        float totalDuration = Mathf.Max(shine.SpawnEntranceDuration, 0.1f);
        float travelDuration = Mathf.Min(
            Mathf.Max(RewardCameraTravelDuration, 0.05f),
            totalDuration * 0.5f
        );
        float holdDuration = Mathf.Max(0f, totalDuration - travelDuration * 2f);

        _rewardCamera.MakeCurrent();
        _rewardCameraTween = _rewardCamera.CreateTween();
        _rewardCameraTween.SetPauseMode(Tween.TweenPauseMode.Process);
        _rewardCameraTween.TweenProperty(
                _rewardCamera,
                "global_transform",
                revealTransform,
                travelDuration
            )
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        if (holdDuration > 0f)
            _rewardCameraTween.TweenInterval(holdDuration);
        _rewardCameraTween.TweenProperty(
                _rewardCamera,
                "global_transform",
                gameplayTransform,
                travelDuration
            )
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        _rewardCameraTween.Finished += FinishRedCoinShineRevealCamera;

        GD.Print($"[RedCoinSwitch] reward camera started; total={totalDuration:0.00}s current={_rewardCamera.Current}");
    }

    private void FinishRedCoinShineRevealCamera()
    {
        bool hadRewardCamera =
            (_rewardCamera != null && IsInstanceValid(_rewardCamera))
            || (_cameraToRestore != null && IsInstanceValid(_cameraToRestore));
        if (!hadRewardCamera)
            return;

        _rewardCameraTween = null;

        if (_cameraToRestore != null
            && IsInstanceValid(_cameraToRestore)
            && _cameraToRestore.IsInsideTree())
        {
            _cameraToRestore.MakeCurrent();
        }

        if (_rewardCamera != null && IsInstanceValid(_rewardCamera))
            _rewardCamera.QueueFree();

        _rewardCamera = null;
        _cameraToRestore = null;
        GD.Print("[RedCoinSwitch] reward camera restored gameplay view");
    }

    public override void _ExitTree()
    {
        if (_rewardCameraTween != null)
        {
            _rewardCameraTween.Kill();
            _rewardCameraTween = null;
        }
        FinishRedCoinShineRevealCamera();
    }

    /// <summary>appear8RedCoinsAndTimer (EventWatcher.cpp 1193) + appearRedCoinCounter: the coins, the timer, the HUD's red coin counter at 0.</summary>
    private void BeginRedCoinChallenge()
    {
        if (_challengeStarted)
            return;

        _challengeStarted = true;
        SpawnRedCoins();
        _shineTimer?.StartTimer();
        SmsHudLayer.Current?.Standard?.AppearRedCoins();
    }

    private void PruneCoins()
    {
        for (int i = _smsCoins.Count - 1; i >= 0; i--)
            if (!IsInstanceValid(_smsCoins[i]) || !_smsCoins[i].IsInsideTree() || _smsCoins[i].IsQueuedForDeletion())
                _smsCoins.RemoveAt(i);
    }

    /// <summary>The coins' remaining life in 120 Hz ticks, from his ShineTimer (a countdown to its target); -1 when there is no countdown to read.</summary>
    private int SmsCoinTicksLeft(out bool reached, out bool running)
    {
        reached = false; running = false;
        var t = _shineTimer;
        if (t == null || !IsInstanceValid(t) || !t.CountDown) return -1;
        float remaining = (float)(t.CurrentValue - t.TargetValue);
        running = t.IsRunning;
        reached = !t.IsRunning && remaining <= 0f;
        return Mathf.Max(0, Mathf.CeilToInt(remaining * SmsUnits.TicksPerSecond));
    }

    /// <summary>evAppear8RedCoinsAndTimer gives every coin killByTimer(timer - 120): its life is the challenge timer itself, so the coins die the tick the
    /// countdown reaches zero, drawn blinking through the last 360 ticks (hidden while ticks / 10 is odd, TMapObjGeneral::perform 584), untouchable for
    /// the first 120. His ShineTimer is the clock (no countdown = the coins live on; stopped early by the 8th coin = frozen, like stopMoveTimer).</summary>
    private void TickCoinTimer(double delta)
    {
        if (!_challengeStarted || _smsTimeUp || _completionReached) return;
        if (_smsCoinsUntouchable)
        {
            _smsUntouchableTicks -= SmsUnits.TicksFor(delta, ref _smsUntouchableAccum);
            if (_smsUntouchableTicks <= 0)
            {
                foreach (var a in _smsCoinPickups) if (IsInstanceValid(a)) { a.SetDeferred(Area3D.PropertyName.Monitoring, true); a.SetDeferred(Area3D.PropertyName.Monitorable, true); }
                _smsCoinPickups.Clear();
                _smsCoinsUntouchable = false;
            }
        }
        int ticks = SmsCoinTicksLeft(out bool reached, out bool running);
        if (ticks < 0) return;
        if (reached) { SmsCoinsTimeUp(); return; }
        if (!running) return;
        PruneCoins();
        bool shown = ticks >= SmsFlushTicks || ((ticks / SmsFlushInterval) & 1) == 0;
        foreach (var c in _smsCoins) if (c.Visible != shown) c.Visible = shown;
    }

    /// <summary>TItem::appeared (Item.cpp 42) for every coin whose timer ran out: MSD_SE_SY_COIN_DISAPPEAR, makeObjDead. The switch stays pressed -- control()
    /// state 4 and MSF_RED_COIN_SWITCH_PRESSED for the stage visit -- unless SmsRearmOnTimeUp.</summary>
    private void SmsCoinsTimeUp()
    {
        _smsTimeUp = true;
        PruneCoins();
        int n = 0;
        foreach (var c in _smsCoins) { c.Visible = true; c.QueueFree(); n++; }
        _smsCoins.Clear();
        _smsCoinPickups.Clear();
        _smsCoinsUntouchable = false;
        if (n > 0) CoinDisappearSound?.Play();
        GD.Print($"[RedCoinSwitch] '{Name}': time up -- {n} red coins gone (killByTimer), {_collectedRedCoins}/{RequiredRedCoins} taken; the switch {(SmsRearmOnTimeUp ? "re-arms (SmsRearmOnTimeUp)" : "stays pressed (the game)")}");
        if (SmsRearmOnTimeUp) Rearm();
    }

    /// <summary>DEVIATION (SmsRearmOnTimeUp): back to the unpressed switch -- the press clip rewound to its first frame, the collision back, every latch cleared;
    /// his ShineTimer and the HUD's counter are left as they are (the counter keeps the count, as the game's does).</summary>
    private void Rearm()
    {
        _triggered = false; _pressed = false; _talkRequested = false; _talkSeen = false; _signAcknowledged = false;
        _challengeStarted = false; _redCoinsSpawned = false; _collectedRedCoins = 0; _completionReached = false;
        _hitThisPound = false; _lastHitter = null; _messageMario = null;
        if (_anim != null && _anim.HasAnimation("redcoinswitch"))
        {
            _anim.Play("redcoinswitch");
            _anim.Seek(0.0, true);
            _anim.Stop(true);
        }
        foreach (var shape in GetNodeOrNull<Node>("SwitchBody")?.FindChildren("*", "CollisionShape3D", true, false) ?? new Godot.Collections.Array<Node>())
            if (shape is CollisionShape3D cs) cs.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
    }
}
