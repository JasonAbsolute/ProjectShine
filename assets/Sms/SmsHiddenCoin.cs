using Godot;
using System;

/// <summary>
/// SMS pass 123: the game's HIDE OBJECTS (MoveBG/MapObjHide.cpp) -- an invisible spot that throws a hidden coin out when Mario does the
/// right thing to it:
///  * WaterHit (TWaterHitHideObj, hit cylinder r 60 x h 20 x scale): any FLUDD drop -> the coin appears 50 units up, the spot is spent;
///  * HipDrop (THipDropHideObj, r 100 x h 60): Mario ground-pounds on it -> the coin at the spot, spent;
///  * HeadHit (THideObj, r 100 x h 60 sitting 10 units under the placed point): Mario jumps up INTO it from below (marioHeadAttack: below
///    the point, in a jump, rising) -> the coin 100 units up, spent -- the hidden-block feel;
///  * FruitHit (TFruitHitHideObj, r 100 x h 60): a fruit touches it -> the coin 50 units up, the fruit is destroyed, spent;
///  * Picture (TWaterHitPictureHideObj, the graffiti M: KoopaJrSignM): a coloured quad whose alpha is a progress 255 -> 20; every drop hit
///    takes SprayProgressSpeed off it (1.8; the game uses 2.8 on a floor, 2.5 on a ceiling), it HEALS 0.4 a tick while you are not spraying;
///    under 20 the quad fades out, the coin appears, and when THAT coin dies by its timer the picture comes back and can be sprayed again
///    (ATTACH -> STATE_NORMAL, mAllowReveal). The same blue coin comes out each time until it is collected.
/// The coin flies with the game's own throw (throwObjToFrontFromPoint): along this node's forward (-Z, the blue gizmo arrow) at AppearSpeed
/// x 0.06 units a tick plus AppearYSpeed up (the stage default 20), then lives the game's item life (SmsCoinItem). A blue coin whose flag is
/// already set (GameData) reveals nothing (THideObjBase::loadAfter: mAllowReveal false; a Picture is dead from the start). The reveal emits
/// ms_watcoin_hit at the spot and binds ms_watcoin_kira to the coin. Bianco Hills' data (scene_hideobj.py): appear 0 or 100 (-> 6 u/tick),
/// y -1 (-> 20), disappear 1200 ticks; 17 of its 20 sprayed spots hide a yellow coin, 2 a blue, 1 a shine.
/// Collision: the body sits on layer 8 (Mario's mask 5 ignores it, the drops' ray hits it); the touch kinds use an Area3D on Mario's layer.
/// 123c -- HOW THE COIN FLIES OUT: `FlyOut` picks the launch direction (Forward = the game's throwObjToFrontFromPoint along this node's blue
/// arrow, tilt included; Up; TowardPlayer / AwayFromPlayer; Custom = FlyDirection in this node's frame), `AppearSpeed` the speed along it,
/// `AppearYSpeed` the extra lift. A [Tool] script: in the editor the node draws the predicted flight (the coin's own physics) from where the coin
/// will appear, so you can aim it before playing; `ShowFlightPath` hides it (in play the line shows only with DebugPrint).
/// Not ported: the barrel hit, the picture's back-face rule and its own texture (PictureTexture is yours), the twin pictures, the sounds (slots).
/// </summary>
[Tool]
[GlobalClass]
public partial class SmsHiddenCoin : StaticBody3D, ISmsSprayTarget
{
    public enum HideKind { WaterHit, HipDrop, HeadHit, FruitHit, Picture }
    /// <summary>Game: a yellow coin and any Picture coin disappear, a blue coin from a plain spot stays (MAP_OBJ_FLAG_UNK10000000, see SmsCoinItem).</summary>
    public enum DisappearRule { Game, Always, Never }
    /// <summary>Where the coin flies: Forward = the game (along this node's -Z arrow, the tilt included: throwObjToFrontFromPoint uses the object's full
    /// forward); Up = straight up (AppearSpeed ignored); TowardPlayer / AwayFromPlayer = the horizontal line to Mario at the reveal; Custom = FlyDirection.</summary>
    public enum FlyMode { Forward, Up, TowardPlayer, AwayFromPlayer, Custom }

    [Export] public HideKind Kind = HideKind.WaterHit;
    /// <summary>What comes out: his blue_coin.tscn by default; yellow_coin.tscn for the game's common case; any Node3D scene is placed and thrown if it is a coin.</summary>
    [Export] public PackedScene HiddenScene;
    /// <summary>The blue coin's flag (GameData): given to the revealed BlueCoin; empty = derived from the scene and this node's path (as a shine's ShineId).</summary>
    [Export] public string CoinId = "";
    /// <summary>loadHideObjInfo: the stage's appear speed, x 0.06 at load -> units a tick along the fly direction (Bianco: 0 or 100 = 6 u/tick = 9.7 m/s).
    /// 0 = straight up whatever FlyOut says. 50 = a gentle lob (4.8 m/s), 100 = a throw landing ~3 m out, 200 = ~6 m.</summary>
    [Export] public float AppearSpeed = 0f;
    /// <summary>loadHideObjInfo: the lift, units a tick; below 0 = the game's default 20 (a 7.5 m pop, half a second up). 10 = 1.9 m, 30 = 17 m.</summary>
    [Export] public float AppearYSpeed = -1f;
    /// <summary>123c: which way the coin flies out (see FlyMode). Forward is the game's rule: this node's blue arrow (-Z), tilt included.</summary>
    [Export] public FlyMode FlyOut = FlyMode.Forward;
    /// <summary>123c: FlyOut = Custom: the direction in THIS node's frame (normalised; (0, 0, -1) = the arrow). Rotating the node turns it.</summary>
    [Export] public Vector3 FlyDirection = new Vector3(0f, 0f, -1f);
    /// <summary>123c: the editor draws the coin's predicted flight (its own physics: gravity 0.35, drag 0.99, 2 s) from where it will appear. In
    /// play the line shows only with DebugPrint.</summary>
    [Export] public bool ShowFlightPath = true;
    /// <summary>The coin's life once touchable, 120 Hz ticks; 0 or less = the game's default 1200 (10 s). Applies when the coin disappears at all.</summary>
    [Export] public int DisappearTicks = 0;
    [Export] public DisappearRule CoinDisappears = DisappearRule.Game;
    /// <summary>ms_watcoin_hit at the spot (emitEffect).</summary>
    [Export] public bool HitEffect = true;
    /// <summary>ms_watcoin_kira bound to the coin (TCoin::appearWithoutSound).</summary>
    [Export] public bool CoinSparkle = true;
    /// <summary>MSD_SE_SY_TIMECOIN_APPEAR (a blue coin not yet got) / MSD_SE_SY_COIN_APPEAR (a yellow): a slot, empty = silent.</summary>
    [Export] public AudioStreamPlayer3D AppearSound;
    /// <summary>On: the body goes to layer 8 with no mask at _Ready (Mario's mask 5 never touches it; FLUDD's drops do). Off: your own layers.</summary>
    [Export] public bool AutoLayer = true;
    [Export] public bool DebugPrint = false;

    // ---- Picture ----
    /// <summary>The graffiti's mesh; empty = a PictureSize quad facing the forward, PictureColor / PictureTexture on it.</summary>
    [Export] public MeshInstance3D PictureMesh;
    [Export] public Color PictureColor = new Color(1f, 0f, 0f, 1f);     // the Bianco M signs: (255, 0, 0)
    [Export] public Texture2D PictureTexture;
    [Export] public Vector2 PictureSize = new Vector2(1.5f, 1.5f);      // metres
    /// <summary>TWaterHitPictureHideObj::touchWater forward(mSprayProgressSpeed): per drop hit (1.8 on a wall; 2.8 floor, 2.5 ceiling in the game).</summary>
    [Export] public float SprayProgressSpeed = 1.8f;
    /// <summary>KoopaJrSignM: mMinProgress 20 (the reveal), mMaxProgress 255 (the full picture), unk15C 0.4 (the heal a tick).</summary>
    [Export] public float PictureMin = 20f;
    [Export] public float PictureMax = 255f;
    [Export] public float HealPerTick = 0.4f;

    public const string DefaultCoinScene = "res://blue_coin.tscn";
    public const string HitFxScene = "res://assets/Sms/Fx/WatCoinHit.tscn";
    public const string KiraFxScene = "res://assets/Sms/Fx/WatCoinKira.tscn";
    public const uint HideLayer = 1u << 7;                       // layer 8
    public const float KiraHeightUnits = 80f;                    // ms_watcoin_kira BEM1 trs (0, 80, 0)

    private bool _allowReveal = true;     // mAllowReveal
    private bool _dead;                   // makeObjDead: reacts to nothing
    private bool _finished;               // the picture's STATE_FINISHED
    private float _progress;              // the picture's mProgress (its alpha x 255)
    private float _accum;
    private Node3D _coin;
    private CollisionShape3D _shape;
    private Area3D _touch;
    private static PackedScene _hitFx, _kiraFx, _defaultCoin;

    /// <summary>For the drives: the last revealed coin, where it appeared, and the count of reveals.</summary>
    public static Node3D LastRevealed;
    public static Vector3 LastRevealPos;
    public static int RevealCount;

    // ---- 123b: the game's water hit (TObjHitCheck::checkWater, ObjHitCheck.cpp 97; TModelWaterManager::mStaticHitActor r 50 x h 80, ModelWaterManager.cpp 106) ----
    /// <summary>Every FLYING drop is a cylinder of this radius / height tested against each actor's damage cylinder (checkDistance, ObjHitCheck.cpp 10):
    /// taken when the drop is no higher than the spot's top, no lower than 80 units under its base, and within 50 + the spot's radius in XZ.</summary>
    public const float WaterAttackRadiusUnits = 50f, WaterAttackHeightUnits = 80f;
    private static readonly System.Collections.Generic.List<SmsHiddenCoin> _live = new();
    public override void _EnterTree() { if (!Engine.IsEditorHint() && !_live.Contains(this)) _live.Add(this); }
    public override void _ExitTree() { _live.Remove(this); }

    /// <summary>The spot whose damage cylinder a flying drop at <paramref name="dropPos"/> (world metres) overlaps by the game's test, or null.
    /// SmsWaterDrops asks this for every flying drop each tick before it moves (move() 424: a taken drop dies there).</summary>
    public static SmsHiddenCoin WaterZoneAt(Vector3 dropPos)
    {
        for (int i = 0; i < _live.Count; i++)
        {
            var h = _live[i];
            if (!GodotObject.IsInstanceValid(h) || h._dead || !h.IsInsideTree()) continue;
            if (h.Kind != HideKind.WaterHit && h.Kind != HideKind.Picture) continue;
            if (h.Kind == HideKind.Picture && h._finished) continue;                 // HIT_FILTER_NO_DAMAGE: checkWaterWithActorsInList skips it
            if (h.WaterCylinderHit(dropPos)) return h;
        }
        return null;
    }

    /// <summary>checkDistance(drop, 50, 80, spot, damageRadius, damageHeight) in game units. WaterHitHideObj: {60, 20} x scale, base at the point;
    /// KoopaJrSignM (the graffiti M): {150, 300} x scale with mYOffset -150 x scale -- a cylinder centred on the placed point, 2 m wide and 4 m
    /// tall at scale 1 (the game's generous wall-graffiti zone). Radius x max(scale.x, scale.z), height x scale.y (initObjCollisionData).</summary>
    public bool WaterCylinderHit(Vector3 dropPos)
    {
        float sy = Mathf.Max(0.01f, Scale.Y), sxz = Mathf.Max(0.01f, Mathf.Max(Scale.X, Scale.Z));
        float dmgR, dmgH, baseY;
        if (Kind == HideKind.Picture) { dmgR = 150f * sxz; dmgH = 300f * sy; baseY = SmsUnits.MetresToUnits(GlobalPosition.Y) - 150f * sy; }
        else { dmgR = 60f * sxz; dmgH = 20f * sy; baseY = SmsUnits.MetresToUnits(GlobalPosition.Y); }
        float dropY = SmsUnits.MetresToUnits(dropPos.Y);
        if (dropY > baseY + dmgH) return false;
        if (dropY + WaterAttackHeightUnits < baseY) return false;
        float r = WaterAttackRadiusUnits + dmgR;
        float dx = SmsUnits.MetresToUnits(dropPos.X - GlobalPosition.X), dz = SmsUnits.MetresToUnits(dropPos.Z - GlobalPosition.Z);
        return r * r > dx * dx + dz * dz;
    }

    public bool AllowReveal => _allowReveal;
    public bool IsDead => _dead;
    public bool PictureFinished => _finished;
    public float PictureProgress => _progress;
    public Node3D RevealedCoin => _coin;
    public string ResolvedCoinId { get; private set; } = "";

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            // the editor: only the flight preview (no bodies, no areas, no registry); redrawn by _Process when a knob or the transform changes
            SetProcess(true);
            RebuildArc();
            return;
        }
        if (AutoLayer) { CollisionLayer = HideLayer; CollisionMask = 0; }
        ResolvedCoinId = CoinId;
        if (string.IsNullOrEmpty(ResolvedCoinId) && Owner != null && !string.IsNullOrEmpty(Owner.SceneFilePath))
            ResolvedCoinId = Owner.SceneFilePath + ":" + Owner.GetPathTo(this);
        // THideObjBase::loadAfter: a blue coin already got -> no reveal; TWaterHitPictureHideObj::loadAfter: the picture is dead outright
        bool got = !string.IsNullOrEmpty(ResolvedCoinId) && GameData.Instance != null && GameData.Instance.IsBlueCoinCollected(ResolvedCoinId);
        if (got && HidesBlue()) _allowReveal = false;
        float s = Mathf.Max(0.01f, Scale.Y);
        switch (Kind)
        {
            case HideKind.WaterHit:
                _shape = AddCylinder(this, 60f * s, 20f * s, 0f);
                break;
            case HideKind.HipDrop:
            case HideKind.FruitHit:
                _touch = MakeTouchArea(100f * s, 60f * s, 0f);
                break;
            case HideKind.HeadHit:
                _touch = MakeTouchArea(100f * s, 60f * s, -10f * s);        // mYOffset = -10 x scale: the cylinder starts 10 units under the point
                break;
            case HideKind.Picture:
                SetupPicture();
                if (got && HidesBlue()) { Die(); if (PictureMesh != null) PictureMesh.Visible = false; }
                break;
        }
        if (DebugPrint) GD.Print($"[SmsHiddenCoin] '{Name}' {Kind} id '{ResolvedCoinId}' allowReveal {_allowReveal} dead {_dead}");
        SetProcess(DebugPrint && ShowFlightPath);   // 123c: the flight line in play only while debugging
    }

    private bool HidesBlue()
    {
        var scene = HiddenScene ?? LoadDefaultCoin();
        if (scene == null) return true;
        // the scene's root script decides: a BlueCoin carries the flag (the game's TCoinBlue event id)
        var st = scene.GetState();
        for (int i = 0; i < st.GetNodePropertyCount(0); i++)
            if (st.GetNodePropertyName(0, i).ToString() == "script" && st.GetNodePropertyValue(0, i).AsGodotObject() is CSharpScript cs)
                return (cs.ResourcePath ?? "").EndsWith("BlueCoin.cs");
        return false;
    }

    private static PackedScene LoadDefaultCoin() => _defaultCoin ??= ResourceLoader.Exists(DefaultCoinScene) ? GD.Load<PackedScene>(DefaultCoinScene) : null;

    private static CollisionShape3D AddCylinder(Node parent, float radiusUnits, float heightUnits, float baseUnits)
    {
        var cs = new CollisionShape3D { Name = "SmsHitCylinder" };
        float h = SmsUnits.UnitsToMetres(heightUnits);
        cs.Shape = new CylinderShape3D { Radius = SmsUnits.UnitsToMetres(radiusUnits), Height = h };
        cs.Position = new Vector3(0f, SmsUnits.UnitsToMetres(baseUnits) + h * 0.5f, 0f);
        parent.AddChild(cs);
        return cs;
    }

    private Area3D MakeTouchArea(float radiusUnits, float heightUnits, float baseUnits)
    {
        var a = new Area3D { Name = "SmsTouch", CollisionLayer = 0, CollisionMask = 3, Monitorable = false };   // layer 1 Mario, layer 2 the SMS fruit (Fruit.SmsPassThroughMario)
        AddChild(a);
        AddCylinder(a, radiusUnits, heightUnits, baseUnits);
        return a;
    }

    private void SetupPicture()
    {
        if (PictureMesh == null)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = PictureColor, AlbedoTexture = PictureTexture, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Back,
            };
            PictureMesh = new MeshInstance3D { Name = "SmsPicture", Mesh = new QuadMesh { Size = PictureSize, Material = mat } };
            PictureMesh.RotateY(Mathf.Pi);                                  // a QuadMesh faces +Z; the picture faces this node's forward (-Z)
            AddChild(PictureMesh);
        }
        _shape = new CollisionShape3D { Name = "SmsPictureBox", Shape = new BoxShape3D { Size = new Vector3(PictureSize.X, PictureSize.Y, 0.05f) } };
        AddChild(_shape);
        _progress = PictureMax;
        ApplyPictureAlpha();
    }

    private void ApplyPictureAlpha()
    {
        if (PictureMesh == null) return;
        PictureMesh.Transparency = 1f - Mathf.Clamp(_progress / 255f, 0f, 1f);   // initPacketMatColor GX_TEVREG0 alpha = mProgress
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead || Engine.IsEditorHint()) return;
        if (Kind == HideKind.Picture)
        {
            // control() STATE_NORMAL (mReverse): the picture heals unk15C a tick toward the max
            int n = SmsUnits.TicksFor(delta, ref _accum);
            if (!_finished && n > 0 && _progress < PictureMax)
            {
                _progress = Mathf.Min(_progress + HealPerTick * n, PictureMax);
                ApplyPictureAlpha();
            }
            return;
        }
        if (_touch == null) return;
        // TMapObjBase::control: touchActor for every body in the cylinder, each tick
        foreach (var body in _touch.GetOverlappingBodies())
        {
            if (Kind == HideKind.FruitHit)
            {
                if (body is Fruit fruit)
                {
                    // TFruitHitHideObj::touchFruit: appearObj(50) + emitEffect + makeObjDead; the fruit is killed
                    Reveal(50f);
                    Die();
                    fruit.QueueFree();
                    break;
                }
                continue;
            }
            if (body is not Mario mario) continue;
            if (Kind == HideKind.HipDrop)
            {
                // THipDropHideObj::touchPlayer: SMS_IsMarioStatusHipDrop -> appearObj(0) + dead
                var s = mario.stateOfMario;
                if (s == Mario.MarioState.groundPoundFalling || s == Mario.MarioState.groundPoundLanding)
                { Reveal(0f); Die(); break; }
            }
            else if (Kind == HideKind.HeadHit)
            {
                // THideObj::touchPlayer: marioHeadAttack -- Mario under the placed point, in a jump, rising -> appearObj(100) + dead
                if (mario.GlobalPosition.Y < GlobalPosition.Y && IsJumpState(mario.stateOfMario) && mario.Velocity.Y > 0f)
                { Reveal(100f); Die(); break; }
            }
        }
    }

    private static bool IsJumpState(Mario.MarioState s) =>
        s == Mario.MarioState.singleJump || s == Mario.MarioState.doubleJump || s == Mario.MarioState.tripleJump
        || s == Mario.MarioState.SpinJump || s == Mario.MarioState.sideFlip || s == Mario.MarioState.backFlip || s == Mario.MarioState.wallJump;

    /// <summary>HIT_MESSAGE_SPRAYED_BY_WATER (a drop or the hose). WaterHit: appearObj(50) + dead, the drop taken. Picture: forward(speed) while
    /// not finished, the drop taken. Other kinds: not a water target (false: the drop lands).</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_dead) return false;
        if (Kind == HideKind.WaterHit)
        {
            Reveal(50f);
            Die();
            return true;
        }
        if (Kind == HideKind.Picture)
        {
            if (_finished) return false;
            Forward(SprayProgressSpeed);
            return true;
        }
        return false;
    }

    /// <summary>makeObjDead for a spent spot: no more reactions, the hit body gone (removeMapCollision + HIT_FILTER_NO_COLLISION).</summary>
    private void Die()
    {
        _dead = true;
        _shape?.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
    }

    /// <summary>TWaterHitPictureHideObj::forward (mReverse): the progress down by the speed; under the min -> afterFinishedAnim.</summary>
    public void Forward(float amount)
    {
        _progress -= amount;
        if (_progress < PictureMin)
        {
            // afterFinishedAnim: removeMapCollision, HIT_FILTER_NO_DAMAGE, appearObjFromPoint(getObjAppearPos()), STATE_FINISHED
            _progress = PictureMin;
            _finished = true;
            _shape?.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
            Reveal(0f, alongForwardUnits: 30f);
        }
        _progress = Mathf.Clamp(_progress, PictureMin, PictureMax);
        ApplyPictureAlpha();
    }

    /// <summary>THideObjBase::appearObjFromPoint: the hidden coin thrown from this spot + <paramref name="yOffsetUnits"/> up (the picture's
    /// getObjAppearPos is approximated as 30 units out of its face), ms_watcoin_hit here, ms_watcoin_kira on the coin; mAllowReveal off.</summary>
    public void Reveal(float yOffsetUnits, float alongForwardUnits = 0f)
    {
        if (!_allowReveal) return;
        var scene = HiddenScene ?? LoadDefaultCoin();
        if (scene == null) { GD.PushWarning($"[SmsHiddenCoin] '{Name}': no HiddenScene and {DefaultCoinScene} is missing -- nothing to reveal."); return; }
        Node parent = GetTree()?.CurrentScene ?? GetParent();
        if (parent == null) return;
        var coin = scene.Instantiate<Node3D>();
        coin.Name = $"{Name}_coin";
        if (coin is BlueCoin blue && !string.IsNullOrEmpty(ResolvedCoinId)) blue.CoinId = ResolvedCoinId;   // TCoinBlue's event id: the flag it will set
        parent.AddChild(coin);
        var (appearAt, vel) = LaunchFor(yOffsetUnits, alongForwardUnits, PlayerPosition());
        coin.GlobalPosition = appearAt;
        int life = DisappearTicks <= 0 ? 1200 : DisappearTicks;
        bool disappears = CoinDisappears switch
        {
            DisappearRule.Always => true,
            DisappearRule.Never => false,
            _ => Kind == HideKind.Picture || coin is not BlueCoin,                      // the game's UNK10000000 rule (SmsCoinItem's summary)
        };
        int after = disappears ? life : -1;
        if (coin is BlueCoin b) b.SmsThrow(vel, after, this);
        else if (coin is YellowCoin y) y.SmsThrow(vel, after, this);
        else if (DebugPrint) GD.Print($"[SmsHiddenCoin] '{Name}': {coin.GetType().Name} placed, not thrown (not a coin)");
        if (AppearSound != null && AppearSound.Stream != null) AppearSound.Play();        // TCoin::appear: TIMECOIN_APPEAR (blue) / COIN_APPEAR (yellow)
        if (HitEffect) SpawnFx(ref _hitFx, HitFxScene, parent, GlobalPosition, null);
        if (CoinSparkle) SpawnFx(ref _kiraFx, KiraFxScene, coin, coin.GlobalPosition + Vector3.Up * SmsUnits.UnitsToMetres(KiraHeightUnits), coin);
        _allowReveal = false;
        _coin = coin;
        LastRevealed = coin;
        LastRevealPos = coin.GlobalPosition;
        RevealCount++;
        if (DebugPrint) GD.Print($"[SmsHiddenCoin] '{Name}' {Kind}: revealed {coin.Name} at {coin.GlobalPosition} vel {vel} u/tick, life {(after < 0 ? "never" : after + " ticks")}");
    }

    private static void SpawnFx(ref PackedScene cache, string path, Node parent, Vector3 at, Node3D follow)
    {
        cache ??= ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
        if (cache == null) return;
        var fx = cache.Instantiate<Node3D>();
        parent.AddChild(fx);
        fx.GlobalPosition = at;
        if (fx is SmsNpcEffect e) e.Start();
    }

    // ---- 123c: how the coin flies out ----
    /// <summary>The coin's starting point and velocity (world axes, units a tick) for a reveal <paramref name="yOffsetUnits"/> up and
    /// <paramref name="alongForwardUnits"/> out of the face. throwObjToFrontFromPoint: vel = dir x (AppearSpeed x 0.06) + (0, lift, 0) with dir by
    /// FlyOut -- Forward = this node's -Z WITH its tilt (the game reads the object's anmMtx column 2 whole), Up = none, Toward / AwayFromPlayer =
    /// the horizontal line to <paramref name="player"/> (null = Forward), Custom = FlyDirection in this node's frame.</summary>
    public (Vector3 appearAt, Vector3 velUnitsPerTick) LaunchFor(float yOffsetUnits, float alongForwardUnits, Vector3? player)
    {
        Vector3 fwdFull = -GlobalTransform.Basis.Z;
        fwdFull = fwdFull.LengthSquared() > 1e-8f ? fwdFull.Normalized() : Vector3.Forward;
        Vector3 fwdFlat = new Vector3(fwdFull.X, 0f, fwdFull.Z);
        fwdFlat = fwdFlat.LengthSquared() > 1e-8f ? fwdFlat.Normalized() : Vector3.Forward;
        Vector3 appearAt = GlobalPosition + Vector3.Up * SmsUnits.UnitsToMetres(yOffsetUnits) + fwdFlat * SmsUnits.UnitsToMetres(alongForwardUnits);
        Vector3 dir;
        switch (FlyOut)
        {
            case FlyMode.Up: dir = Vector3.Zero; break;
            case FlyMode.TowardPlayer:
            case FlyMode.AwayFromPlayer:
                if (player.HasValue)
                {
                    Vector3 to = player.Value - GlobalPosition; to.Y = 0f;
                    dir = to.LengthSquared() > 1e-6f ? to.Normalized() : fwdFlat;
                    if (FlyOut == FlyMode.AwayFromPlayer) dir = -dir;
                }
                else dir = fwdFull;
                break;
            case FlyMode.Custom:
                dir = GlobalTransform.Basis.Orthonormalized() * FlyDirection;
                dir = dir.LengthSquared() > 1e-8f ? dir.Normalized() : fwdFull;
                break;
            default: dir = fwdFull; break;
        }
        float lift = AppearYSpeed < 0f ? SmsCoinItem.PopUpSpeedUnits : AppearYSpeed;
        return (appearAt, dir * (AppearSpeed * 0.06f) + Vector3.Up * lift);
    }

    /// <summary>Mario's position (the "player" group), or null; in the editor the scene's node named Mario if any.</summary>
    private Vector3? PlayerPosition()
    {
        if (Engine.IsEditorHint())
        {
            var root = GetTree()?.EditedSceneRoot;
            return root?.GetNodeOrNull<Node3D>("Mario")?.GlobalPosition;
        }
        return (GetTree()?.GetFirstNodeInGroup("player") as Node3D)?.GlobalPosition;
    }

    /// <summary>The reveal's own offsets by kind (appearObj: WaterHit / FruitHit +50, HipDrop 0, HeadHit +100; the picture 30 out of its face).</summary>
    private (float up, float forward) RevealOffsets() => Kind switch
    {
        HideKind.HipDrop => (0f, 0f), HideKind.HeadHit => (100f, 0f), HideKind.Picture => (0f, 30f), _ => (50f, 0f),
    };

    /// <summary>The coin's predicted flight, world metres: SmsCoinItem's physics without the map (gravity 0.35, drag 0.99, the +-70 x scale clamp),
    /// one point every <paramref name="everyTicks"/> ticks for <paramref name="ticks"/> ticks or until it has fallen <paramref name="stopBelow"/> m under the start.</summary>
    public System.Collections.Generic.List<Vector3> PredictedPath(int ticks = 240, int everyTicks = 4, float stopBelow = 6f)
    {
        var (p, v) = LaunchFor(RevealOffsets().up, RevealOffsets().forward, PlayerPosition());
        float R = SmsCoinItem.BodyRadiusUnits;
        var pts = new System.Collections.Generic.List<Vector3> { p };
        float startY = p.Y;
        for (int t = 1; t <= ticks; t++)
        {
            v.Y = Mathf.Clamp(v.Y - SmsCoinItem.Gravity, -R, R);
            v.X = Mathf.Clamp(v.X * SmsCoinItem.AirDrag, -R, R);
            v.Z = Mathf.Clamp(v.Z * SmsCoinItem.AirDrag, -R, R);
            p += v * (1f / SmsUnits.UnitsPerMetre);
            if (t % everyTicks == 0) pts.Add(p);
            if (p.Y < startY - stopBelow) break;
        }
        return pts;
    }

    private MeshInstance3D _arc;
    private int _arcHash;
    private double _arcCooldown;

    /// <summary>The flight line (an ImmediateMesh line strip, unshaded, drawn over everything) -- the editor only, or in play with DebugPrint.</summary>
    public void RebuildArc()
    {
        bool want = ShowFlightPath && (Engine.IsEditorHint() || DebugPrint);
        if (!want) { if (_arc != null) _arc.Visible = false; return; }
        if (_arc == null || !GodotObject.IsInstanceValid(_arc))
        {
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, NoDepthTest = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, RenderPriority = 10,
            };
            _arc = new MeshInstance3D { Name = "SmsFlightPath", Mesh = new ImmediateMesh(), MaterialOverride = mat, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_arc);   // no Owner: never saved into the scene
        }
        _arc.Visible = true;
        _arc.GlobalTransform = Transform3D.Identity;
        var im = (ImmediateMesh)_arc.Mesh;
        im.ClearSurfaces();
        var pts = PredictedPath();
        im.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        for (int i = 0; i < pts.Count; i++)
        {
            float f = pts.Count > 1 ? i / (float)(pts.Count - 1) : 0f;
            im.SurfaceSetColor(new Color(0.25f, 0.85f, 1f, 1f - 0.6f * f));   // bright at the start, fading along the fall
            im.SurfaceAddVertex(pts[i]);
        }
        im.SurfaceEnd();
        // the appear point: a small cross
        float c = 0.12f;
        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        im.SurfaceSetColor(new Color(1f, 0.9f, 0.2f)); im.SurfaceAddVertex(pts[0] + Vector3.Left * c); im.SurfaceAddVertex(pts[0] + Vector3.Right * c);
        im.SurfaceSetColor(new Color(1f, 0.9f, 0.2f)); im.SurfaceAddVertex(pts[0] + Vector3.Forward * c); im.SurfaceAddVertex(pts[0] + Vector3.Back * c);
        im.SurfaceSetColor(new Color(1f, 0.9f, 0.2f)); im.SurfaceAddVertex(pts[0] + Vector3.Down * c); im.SurfaceAddVertex(pts[0] + Vector3.Up * c);
        im.SurfaceEnd();
    }

    private int ArcHash()
    {
        var h = new System.HashCode();
        h.Add(Kind); h.Add(FlyOut); h.Add(FlyDirection); h.Add(AppearSpeed); h.Add(AppearYSpeed); h.Add(ShowFlightPath); h.Add(DebugPrint);
        var tr = GlobalTransform; h.Add(tr.Origin); h.Add(tr.Basis.Z); h.Add(tr.Basis.X); h.Add(Scale);
        var pl = PlayerPosition(); if (pl.HasValue) h.Add(pl.Value);
        return h.ToHashCode();
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint() && !(DebugPrint && ShowFlightPath)) { SetProcess(false); return; }
        _arcCooldown -= delta;
        if (_arcCooldown > 0) return;
        _arcCooldown = 0.2;
        int hash = ArcHash();
        if (hash == _arcHash && _arc != null) return;
        _arcHash = hash;
        RebuildArc();
    }

    /// <summary>HIT_MESSAGE_ATTACH from the coin dying by its timer (TItem::appeared / TCoin::perform): mAllowReveal back on; a Picture gets its
    /// collision back and heals (TWaterHitPictureHideObj::receiveMessage: STATE_NORMAL). A spent plain spot stays dead, as in the game.</summary>
    public void OnHiddenItemGone(Node coin)
    {
        _allowReveal = true;
        _coin = null;
        if (Kind == HideKind.Picture)
        {
            _finished = false;
            _dead = false;
            _shape?.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        }
        if (DebugPrint) GD.Print($"[SmsHiddenCoin] '{Name}': the coin is gone -- re-armed ({Kind})");
    }
}
