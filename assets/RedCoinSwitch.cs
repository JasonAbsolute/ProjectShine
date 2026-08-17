using Godot;

/// <summary>
/// SMS-style red coin switch. Ground pound it to start the level's ShineTimer
/// and pop up an explanatory sign (SignPopup) — fires once; the switch itself
/// doesn't reset (that's on the level/ShineTimer around it).
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

    [Export] public PackedScene SignPopupScene;

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
    }

    public override void _PhysicsProcess(double delta)
    {
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

        if (_anim != null && _anim.HasAnimation("redcoinswitch"))
            _anim.Play("redcoinswitch");

        // Both the coin burst and timer wait until A is pressed and the sign's
        // fade-out has completed (see OnSignClosed).
        ShowSign();
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
        }

        _redCoinsSpawned = true;

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
        parent.AddChild(shine);
        shine.GlobalTransform = spawn.GlobalTransform;
        PlayRedCoinShineRevealCamera(shine);
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

    private void ShowSign()
    {
        if (SignPopupScene == null)
        {
            GD.PushWarning($"[RedCoinSwitch] '{Name}' has no SignPopupScene assigned; starting challenge without a message.");
            _signAcknowledged = true;
            BeginRedCoinChallenge();
            return;
        }

        var popup = SignPopupScene.Instantiate();
        popup.TreeExited += OnSignClosed;

        // Set BEFORE entering the tree — SignPopup reads this in _Ready().
        if (popup is SignPopup sign)
        {
            sign.Text = MessageText;
            // This flow requires explicit acknowledgement (button_a). The
            // Dismissed signal records A immediately; TreeExited starts the
            // challenge only after the cosmetic fade-out has completed.
            sign.HoldDuration = 0f;
            sign.Dismissed += OnSignDismissed;
        }

        _camera?.EnterTalkingPOV();

        var layer = GetNodeOrNull(SignLayerPath);
        if (layer != null)
            layer.AddChild(popup);
        else
            GetTree().Root.AddChild(popup);
    }

    private void OnSignDismissed()
    {
        _signAcknowledged = true;
        _camera?.ExitTalkingPOV();
    }

    private void OnSignClosed()
    {
        if (!_signAcknowledged)
            return;
        BeginRedCoinChallenge();
    }

    private void BeginRedCoinChallenge()
    {
        if (_challengeStarted)
            return;

        _challengeStarted = true;
        SpawnRedCoins();
        _shineTimer?.StartTimer();
    }
}
