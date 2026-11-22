using Godot;

/// <summary>
/// Ground-poundable nail, working as the game's (TMapObjNail, MapObjEx.cpp; model kugi.bmd, pass 248):
///  - a hip drop (the normal or the super one) drives it 50 u = 0.672 m into the ground AT ONCE; three hits sink it (150 u: the head ends up flush);
///  - after a hit it ignores hits for 120 movement ticks (1 s at the game's 120 ticks a second) -- one pound, one hit;
///  - the third hit throws the hidden item out of the top (the course data's event: a coin, a 1-Up, or nothing): it appears 200 u above the
///    SUNK nail, i.e. 50 u = 0.672 m above where the nail started, rising at the data's appear speed (17 u a tick straight up in every secret
///    course: the forward speed is 0 for all 38 nails), and a thrown coin lives 1200 ticks (10 s) before it fades (TMapObjBase::throwObjToFront,
///    loadHideObjInfo);
///  - the impact plays MSD_SE_OBJ_KUGI_IMPACT at the nail.
/// The game sends the hit when Mario's hip drop lands on the nail's own collision; here the hit zone over the head stands in for that: a Mario in
/// his ground-pound fall or landing inside it on a movement tick is the hit (checked every tick, as the game checks its hit cylinders).
/// Before 248 this was two pounds of 1.5 m with a 0.18-s tween and the item 1.5 m up with its own bounce; those remain as knobs.
///
/// Scene structure expected:
///   Nail (this script, on an AnimatableBody3D)
///   ├─ MeshInstance3D (the nail visual)
///   ├─ CollisionShape3D (so Mario can land on the head)
///   └─ HitZone (Area3D, just above the nail head)
///        └─ CollisionShape3D (small box / cylinder covering the top of the head)
/// </summary>
public partial class Nail : AnimatableBody3D
{
    /// <summary>Item scene to spawn when the nail is fully driven in (the course data's event: a coin, a 1-Up; none = nothing).</summary>
    [Export] public PackedScene ItemToSpawn;

    /// <summary>How many ground pounds it takes to fully drive the nail in. The game's: 3 (TMapObjNail::receiveMessage, unk150 &lt; 3).</summary>
    [Export] public int RequiredHits = 3;

    /// <summary>How far the nail drops on each hit, in metres. The game's mDownHeight = 50 u = 0.6717 m.</summary>
    [Export] public float HitDropDistance = 50f / 74.44f;

    /// <summary>How long the drop takes. The game's drop is instant (0); above 0 the old eased tween.</summary>
    [Export] public float DropDuration = 0f;

    /// <summary>How high above the nail's ORIGINAL position the item appears. The game's: 200 u above the sunk nail = 200 - 3 x 50 = 50 u = 0.6717 m.</summary>
    [Export] public float ItemSpawnYOffset = 50f / 74.44f;

    /// <summary>Ticks after a hit during which the nail ignores hits (the game's mStateTimer = 120: one second).</summary>
    [Export] public int LockoutTicks = 120;

    /// <summary>The game's movement ticks a second, for the lockout.</summary>
    [Export] public float TicksPerSecond = 120f;

    /// <summary>The item's upward speed, game units a tick (loadHideObjInfo's appear y speed: 17 in every secret course; the game uses 20 when the data is negative).</summary>
    [Export] public float ItemAppearYSpeedUnitsPerTick = 17f;

    /// <summary>The item's speed along the nail's facing (+Z), game units a tick (the data's appear speed x 0.06: 0 in every secret course).</summary>
    [Export] public float ItemAppearSpeedUnitsPerTick = 0f;

    /// <summary>How long a thrown coin lives, ticks (loadHideObjInfo: 1200 = 10 s when the data gives none). A 1-Up stays.</summary>
    [Export] public int ItemLifeTicks = 1200;

    /// <summary>Throw the item with the game's item physics (SmsDropTable.Launch: a coin flies as the game's coins do, a body hops). Off = the item's own LaunchAsDrop bounce (the old look).</summary>
    [Export] public bool UseGameItemThrow = true;

    /// <summary>The hop given to a body item (a 1-Up mushroom) when the game throw is used, metres a second up.</summary>
    [Export] public float BodyItemPopMetresPerSecond = 6f;

    /// <summary>The impact sound (MSD_SE_OBJ_KUGI_IMPACT, assets/Sms/Sound/Enemy/). Empty = silent.</summary>
    [Export] public string ImpactSound = "MSD_SE_OBJ_KUGI_IMPACT";

    /// <summary>Print each hit for a focused Godot verification run.</summary>
    [Export] public bool DebugPrint;

    /// <summary>Hits taken so far (for drives: callable from GDScript).</summary>
    public int HitCount() => _hits;
    /// <summary>True once the item has been thrown.</summary>
    public bool HasSpawned() => _spawned;

    private int _hits = 0;
    private Vector3 _restPos;
    private Tween _tween;
    private bool _spawned = false;
    private Area3D _hitZone;
    private float _lockTicks;      // the game's state timer: ticks left before the next hit counts
    private float _accumulator;
    private SmsActorSound _sound;

    public override void _Ready()
    {
        _restPos = Position;

        // Accept any Area3D child as the hit zone (so the user isn't locked into one name).
        foreach (var child in GetChildren())
        {
            if (child is Area3D a) { _hitZone = a; break; }
        }
        if (_hitZone == null)
        {
            GD.PushError($"[Nail] '{Name}' is missing an Area3D child to use as a hit zone. The nail won't react to ground pounds.");
            return;
        }
        if (DebugPrint) GD.Print($"[Nail] '{Name}' wired to hit zone '{_hitZone.Name}'.");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || _hitZone == null) return;
        // the game's clock: the lockout counts movement ticks, and the hit is tested on every one
        _accumulator += (float)delta * Mathf.Max(1f, TicksPerSecond);
        int steps = Mathf.FloorToInt(_accumulator);
        if (steps > 240) steps = 240;
        _accumulator -= steps;
        if (steps <= 0) return;
        _lockTicks = Mathf.Max(0f, _lockTicks - steps);
        if (_lockTicks > 0f || _hits >= RequiredHits) return;
        foreach (Node3D body in _hitZone.GetOverlappingBodies())
        {
            if (body is not Mario mario) continue;
            bool isPounding =
                mario.stateOfMario == Mario.MarioState.groundPoundFalling
                || mario.stateOfMario == Mario.MarioState.groundPoundLanding;   // HIT_MESSAGE_HIP_DROP (and the super drop's, which sends both)
            if (!isPounding) continue;
            Hit();
            break;
        }
    }

    /// <summary>Where the last item appeared (for drives).</summary>
    public Vector3 LastItemSpawn() => _lastSpawn;
    private Vector3 _lastSpawn;

    /// <summary>TMapObjNail::receiveMessage(HIT_MESSAGE_HIP_DROP): nothing while the state timer runs or after the third hit; else the drop, the collision
    /// moved with it, the sound, the lockout, the item on the last hit. Public so a drive (or another sender) goes through the same rule.</summary>
    public void Hit()
    {
        if (_lockTicks > 0f || _hits >= RequiredHits) return;
        _hits++;
        _lockTicks = Mathf.Max(0, LockoutTicks);

        Vector3 newPos = _restPos + new Vector3(0f, -_hits * HitDropDistance, 0f);
        _tween?.Kill();
        _tween = null;
        if (DropDuration > 0f)
        {
            _tween = CreateTween();
            _tween.TweenProperty(this, "position", newPos, DropDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
        }
        else
        {
            Position = newPos;   // mPosition.y -= mDownHeight; removeMapCollision(); setUpCurrentMapCollision(): at once, the collision with it
        }

        if (!string.IsNullOrEmpty(ImpactSound))
        {
            _sound ??= SmsActorSound.Attach(this);
            _sound?.Play(ImpactSound);
        }
        if (DebugPrint) GD.Print($"[Nail] '{Name}' hit {_hits}/{RequiredHits}: y {Position.Y:F3} (rest {_restPos.Y:F3}), lockout {LockoutTicks} ticks");

        if (_hits >= RequiredHits && !_spawned)
        {
            _spawned = true;
            if (_tween != null) _tween.TweenCallback(Callable.From(SpawnItem));
            else SpawnItem();
        }
    }

    /// <summary>throwObjToFront(obj, 200, mAppearSpeed, mAppearYSpeed): the item appears over the nail and is thrown up its facing.</summary>
    private void SpawnItem()
    {
        if (ItemToSpawn == null)
        {
            if (DebugPrint) GD.Print($"[Nail] '{Name}' has no ItemToSpawn: an empty nail (the course data's event -1).");
            return;
        }

        var item = ItemToSpawn.Instantiate<Node3D>();
        // Parent into the same scene as the nail so the item lives on after the nail moves.
        GetParent().AddChild(item);

        // the rest position in world space (through the parent, so the nail's own scale and the drop do not enter), then the offset straight up
        Vector3 restGlobal = GetParent() is Node3D p ? p.ToGlobal(_restPos) : _restPos;
        Vector3 spawnPos = restGlobal + new Vector3(0f, ItemSpawnYOffset, 0f);
        item.GlobalPosition = spawnPos;
        _lastSpawn = spawnPos;

        if (UseGameItemThrow)
        {
            Vector3 facing = GlobalBasis.Z.Normalized();   // the model's +Z (anmMtx column 2): the nail's facing
            Vector3 vel = new Vector3(facing.X * ItemAppearSpeedUnitsPerTick, ItemAppearYSpeedUnitsPerTick, facing.Z * ItemAppearSpeedUnitsPerTick);
            SmsDropTable.Launch(item, vel, ItemLifeTicks, BodyItemPopMetresPerSecond);
            if (DebugPrint) GD.Print($"[Nail] '{Name}' threw {item.Name} at {spawnPos} with {vel} u/tick, life {ItemLifeTicks}");
            return;
        }
        // the old look: the item's own drop-and-bounce
        if (item.HasMethod("LaunchAsDrop"))
            item.Call("LaunchAsDrop");
    }
}
