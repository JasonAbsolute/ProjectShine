using System;
using Godot;

/// <summary>
/// The water bottle -- TItem "bottle_short" (actor type 0x20000001; MoveBG/Item.cpp, MapObjInit.cpp 1017: bottle_short_data, the
/// item_hit_data_table's 40 x 40 unit touch cylinder, flags 0x0A040100) and its big brother "bottle_large" / "WaterRecoverObj"
/// (0x20000002, the same data with FullRefill). The Gooble drops one: TNameKuri::setAfterDeadEffect (namekuri.cpp 511), at the end of
/// its death -- a STOMPED one (unk198 from isHitValid: TRAMPLE / HIP_DROP, not water, not its own leap) while FLUDD's tank is under half
/// (getCurrentWater() x 2 &lt; getMaxWater()) -> gpItemManager->makeObjAppear(pos, 0x20000001, ground = true): the stage's ONE bottle_short
/// (TNameKuriManager::loadAfter registers a single one; canAppear fails while it is out) is put on the ground under the body.
///
/// THE ITEM'S LIFE, in 120 Hz movement ticks (every map object performs all four ticks a frame, like the enemies):
///  - TItem::appear (151): on the ground, still (LIVE_FLAG_UNK10: no physics), untouchable (HIT_FLAG_NO_COLLISION), STATE_APPEARING;
///    TItem::appearing (124, MAP_OBJ_FLAG_UNK2000000): mScaling grows 0.02 a tick (mAppearedScaleSpeed x 2) from 1 to 2, then
///    makeObjAppeared (the state timer zeroed; setObjHitData(0): the touch cylinder at 40 x mScaling = 80) with NO_COLLISION back on.
///    THAT SCALE IS NEVER DRAWN: the bottle's flags (0x0A040100) carry MAP_OBJ_FLAG_UNK8000000, so TItem::calcRootMatrix (87) -- the one
///    path to model->setBaseScale(mScaling) (TMapObjBase::calcRootMatrix 613) -- is a no-op for it, and TItem::calc (93) writes the root
///    matrix straight from the item manager's spin matrix + mPosition, no scale; J3DModel's base scale stays its initial (1, 1, 1)
///    (J3DModel.cpp 494). The bottle stands at its bmd's size (78 units = 1.05 m) from the first tick; the growth is 50 untouchable ticks;
///  - the next tick TItem::perform (163) drops NO_COLLISION (takeable) and arms the item timer: MAP_OBJ_FLAG_DISAPPEARING, 480 ticks
///    (TItem::initMapObj unk14C); TMapObjGeneral::perform (592) skips the draw on the odd 10-tick slots once the timer is under 360
///    (mNormalFlushTime / mNormalFlushInterval); TMapObjGeneral::appeared (240) makes it dead at 0. A PLACED bottle (TItem::load:
///    MAP_OBJ_FLAG_UNK10000000) never arms the timer: it waits, takeable, at the appeared scale;
///  - it spins with every item: TItemManager::perform (112) turns one shared matrix 2 degrees a tick, TItem::calc (93) copies it;
///  - touched by Mario (TItem::touchPlayer 67 -> taken 57: HIT_MESSAGE_ATTACK to him): TMario::receiveMessage (MarioReceiveMsg.cpp 150 /
///    159) addWater(getMaxWater() / 2) -- the whole tank for the large one -- when he has the pack, emitGetWaterEffect (ms_itemget1_b)
///    and MSD_SE_MA_GET_ITEM (98); then kill -> breaking -> makeObjDead at once (no break anim): the pool is free again.
/// The game's pooled quirk (a second appearance starts with mScaling already 2, so its 50-tick appearing state ends at once) is not kept:
/// each drop runs the full state. The scene (assets/Sms/Items/WaterBottle.tscn): Model = a MeshInstance3D of the bmd's mesh at 1 / 74.44
/// (the bmd's own size) with BottleLabel.tres (_mat_label4) and BottleGlass.tres (_suimen1), Hit = the 80 x 80 unit cylinder (the touch
/// data at mScaling 2). Drop one in a level as is
/// (Placed); Spawn() makes a dropped one. SmsNozzleItem is the same TItem family (the nozzle items keep their own timing).
/// </summary>
[GlobalClass]
public partial class SmsWaterBottle : Node3D
{
    /// <summary>The scene's parts: the model (spun; drawn at the bmd's size -- see the header), the touch cylinder (item_hit_data_table:
    /// 40 x 40 units at the item's mScaling).</summary>
    [Export] public Node3D Model;
    [Export] public Area3D Hit;
    /// <summary>emitGetWaterEffect: the water-get sparkle, ms_itemget1_b (assets/Sms/Fx/GetWater.tscn, pass 76: the streaks converging on
    /// him and their glow puffs), bound to his centre joint. Its Sound slot is the water-get chime's. Empty = nothing.</summary>
    [Export] public PackedScene GetEffect;
    /// <summary>bottle_short (0x20000001) refills half the nozzle's tank; bottle_large / WaterRecoverObj (0x20000002) fill it.</summary>
    [Export] public bool FullRefill = false;
    /// <summary>DEVIATION, off = the game: draw mScaling / AppearedScale on the Model during the appearing state, so a dropped bottle pops
    /// in from half its size over the 50 ticks. The game animates mScaling but never draws it (see the header); pass 73 shipped it drawn,
    /// which left the bottle at twice its size.</summary>
    [Export] public bool GrowIn = false;
    /// <summary>A bottle placed in the editor (TItem::load: MAP_OBJ_FLAG_UNK10000000): no item timer, takeable at once, at AppearedScale.
    /// Spawn() clears it for a dropped one.</summary>
    [Export] public bool Placed = true;
    /// <summary>The glass's sun (BottleGlass.gdshader sun_dir / sun_color): the scene's first DirectionalLight3D unless set.</summary>
    [Export] public DirectionalLight3D Sun;
    [Export] public bool DebugPrint;

    public const int LifeTicks = 480;        // TItem::initMapObj unk14C: a spawned item's takeable window
    public const int AppearTicks = 120;      // TItem::initMapObj unk150: the untouchable start -- cut short by the growth's makeObjAppeared
    public const int FlushTicks = 360;       // TMapObjGeneral::mNormalFlushTime: the flicker starts under this
    public const int FlushInterval = 10;     // TMapObjGeneral::mNormalFlushInterval: hidden on the odd slots
    public const float ScaleStep = 0.02f;    // TItem::mAppearedScaleSpeed (0.01) x 2
    public const float AppearedScale = 2f;   // TItem::appearing's mScaling target (MAP_OBJ_FLAG_UNK2000000): the hit size's multiplier, never drawn
    public const float SpinDegPerTick = 2f;  // TItemManager unk70: every item's shared spin
    public const int Pool = 1;               // TNameKuriManager::loadAfter: ONE bottle_short per stage

    private static int _pooledLive;
    /// <summary>TMapObjBaseManager::canAppear: the stage's pooled bottle is free (dead) -- a Gooble may drop one.</summary>
    public static bool PoolFree => _pooledLive < Pool;
    public static int PooledLive => _pooledLive;

    private Vector3 _modelBase = Vector3.One; // the Model's scale in the scene = the bmd at 1 / 74.44
    private float _scale;                     // mScaling
    private int _timer;                       // mStateTimer, ticks
    private bool _noCollision;                // HIT_FLAG_NO_COLLISION
    private bool _disappearing;               // MAP_OBJ_FLAG_DISAPPEARING
    private bool _growing;                    // STATE_APPEARING: TItem::appearing's growth running
    private bool _taken, _dead, _pooled;
    private float _accum;

    public float ItemScale => _scale;
    public int Timer => _timer;
    public bool Takeable => !_noCollision && !_taken && !_dead;
    public bool Disappearing => _disappearing;

    /// <summary>makeObjAppear(x, y, z, 0x20000001, true) for a dropped bottle: the scene at the ground point the caller found (the
    /// game's checkGround from 5 units up), counted against the stage's pool. Null while the pool is out (canAppear) or when the scene
    /// is not this script.</summary>
    public static SmsWaterBottle Spawn(PackedScene scene, Node parent, Vector3 groundPos)
    {
        if (!PoolFree || scene == null || parent == null) return null;
        if (scene.Instantiate() is not SmsWaterBottle b)
        {
            GD.PushWarning($"[SMS water bottle] {scene.ResourcePath} did not load as SmsWaterBottle: no bottle");
            return null;
        }
        b.Placed = false;
        b._pooled = true;
        _pooledLive++;
        parent.AddChild(b);
        b.GlobalPosition = groundPos;
        return b;
    }

    public override void _Ready()
    {
        if (Model == null || Hit == null)
            GD.PushWarning($"[SMS water bottle] {GetPath()}: the scene's Model / Hit exports are not both set (see assets/Sms/Items/WaterBottle.tscn)");
        _modelBase = Model != null ? Model.Scale : Vector3.One;
        if (Placed)
        {
            // TItem::load -> makeObjAppeared: at the appeared scale, takeable, no timer
            _scale = AppearedScale; _growing = false; _noCollision = false; _disappearing = false; _timer = 0;
        }
        else
            Appear();
        ApplyScale();
        SetupSun();
        if (DebugPrint) GD.Print($"[SMS water bottle] {Name}: {(Placed ? "placed" : "dropped")} at {GlobalPosition}, scale {_scale:F2} -> {AppearedScale:F2}, pool {_pooledLive}/{Pool}");
    }

    public override void _ExitTree()
    {
        if (_pooled) { _pooledLive = Math.Max(0, _pooledLive - 1); _pooled = false; }
    }

    /// <summary>TItem::appear (151): still on the ground, untouchable, the growth from 1 running, the 120-tick timer set -- which the
    /// growth's makeObjAppeared zeroes before it can run out.</summary>
    private void Appear()
    {
        _scale = 1f; _growing = true; _noCollision = true; _disappearing = false; _timer = AppearTicks;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_taken || _dead) return;
        _accum += (float)delta * SmsUnits.TicksPerSecond;
        int ticks = (int)_accum;
        _accum -= ticks;
        for (int i = 0; i < ticks && !_dead; i++)
            Tick();
        if (_dead) return;
        if (Model != null)
        {
            // TItemManager::perform 112 + TItem::calc 93: the items' shared matrix, 2 degrees a tick about Y, at the item's position
            Model.RotationDegrees = new Vector3(0f, Model.RotationDegrees.Y + SpinDegPerTick * ticks, 0f);
            // TMapObjGeneral::perform 592: the draw skipped on the odd 10-tick slots once the timer is under the flush time
            Model.Visible = !(_disappearing && _timer > 0 && _timer < FlushTicks && ((_timer / FlushInterval) & 1) == 1);
        }
        // TItem::touchPlayer -> taken, unless HIT_FLAG_NO_COLLISION. Polled: he may already stand where it appears.
        if (!_noCollision && Hit != null)
            foreach (Node3D body in Hit.GetOverlappingBodies())
                if (body is Mario mario)
                {
                    Take(mario);
                    break;
                }
    }

    /// <summary>One movement tick: TMapObjBase::perform's timer (517), TItem::perform's NO_COLLISION drop + item timer (163), then
    /// work() -- TItem::appearing (124) while growing, TMapObjGeneral::appeared (240) once appeared.</summary>
    private void Tick()
    {
        if (_timer > 0) _timer--;
        if (_noCollision && _timer <= 0 && !_growing)
        {
            _noCollision = false;                                     // takeable
            if (!Placed) { _disappearing = true; _timer = LifeTicks; } // the item timer, spawned items only (MAP_OBJ_FLAG_UNK10000000 spares the placed)
        }
        if (_growing)
        {
            if (_scale < AppearedScale)
            {
                _scale = MathF.Min(_scale + ScaleStep, AppearedScale);
                ApplyScale();
            }
            else
            {
                // makeObjAppeared (the timer zeroed, STATE_NORMAL) + onHitFlag(NO_COLLISION) + offMapObjFlag(DISAPPEARING)
                _growing = false; _timer = 0; _noCollision = true; _disappearing = false;
            }
        }
        else if (_disappearing && _timer <= 0)
            Vanish();                                                  // appeared(): DISAPPEARING and the timer out -> makeObjDead
    }

    /// <summary>The draw: the bmd's own size (TItem::calc writes the root matrix with no scale) -- unless GrowIn draws mScaling / 2.</summary>
    private void ApplyScale()
    {
        if (Model == null) return;
        Model.Scale = GrowIn ? _modelBase * (_scale / AppearedScale) : _modelBase;
    }

    /// <summary>taken (57): HIT_MESSAGE_ATTACK to Mario -- the water, the effect at his centre (bound to him), the chime -- then kill:
    /// gone at once, the pool free.</summary>
    private void Take(Mario mario)
    {
        _taken = true;
        mario.SmsGetWaterBottle(FullRefill);
        if (GetEffect?.Instantiate() is Node3D fx)
        {
            // emitAndBindToPosPtr(MS_ITEMGET1_B, &unk160): bound to his centre joint; the level keeps it after this item is gone
            GetParent().AddChild(fx);
            if (fx is SmsFollowEffect bound)
                bound.Bind(mario);
            else
                fx.GlobalPosition = mario.GlobalPosition + Vector3.Up * 1.0f;
        }
        if (DebugPrint) GD.Print($"[SMS water bottle] {Name}: taken -> water {mario.SmsWaterNow}/{mario.SmsWaterMax}");
        QueueFree();
    }

    /// <summary>makeObjDead at the item timer's end: no effect, no sound (TItem::appeared plays the coin's only for red / blue coins).</summary>
    private void Vanish()
    {
        _dead = true;
        if (DebugPrint) GD.Print($"[SMS water bottle] {Name}: timed out");
        QueueFree();
    }

    /// <summary>The glass material's sun (its default is the game's "太陽（オブジェクト）") from the scene's DirectionalLight3D: its +Z basis
    /// points toward the light, its colour x energy is the light colour. The material is the scene's shared BottleGlass.tres unless the
    /// scene carries its own copy.</summary>
    private void SetupSun()
    {
        DirectionalLight3D sun = Sun ?? FindSun(GetTree().CurrentScene);
        if (sun == null || Model is not MeshInstance3D mi) return;
        for (int s = 0; s < mi.GetSurfaceOverrideMaterialCount(); s++)
            if (mi.GetSurfaceOverrideMaterial(s) is ShaderMaterial m && m.Shader != null && m.Shader.ResourcePath.EndsWith("BottleGlass.gdshader"))
            {
                m.SetShaderParameter("sun_dir", sun.GlobalTransform.Basis.Z.Normalized());
                Color c = sun.LightColor * sun.LightEnergy;
                m.SetShaderParameter("sun_color", new Vector3(c.R, c.G, c.B));
            }
    }

    private static DirectionalLight3D FindSun(Node n)
    {
        if (n == null) return null;
        if (n is DirectionalLight3D d) return d;
        foreach (Node c in n.GetChildren())
        {
            var r = FindSun(c);
            if (r != null) return r;
        }
        return null;
    }
}
