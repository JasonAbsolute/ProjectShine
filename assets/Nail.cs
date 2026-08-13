using Godot;

/// <summary>
/// Ground-poundable nail. Two ground pounds drive it fully into the ground,
/// at which point it spawns a configurable item above where it stood.
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
    /// <summary>Item scene to spawn when the nail is fully driven in.</summary>
    [Export] public PackedScene ItemToSpawn;

    /// <summary>How many ground pounds it takes to fully drive the nail in.</summary>
    [Export] public int RequiredHits = 2;

    /// <summary>How far the nail drops on each hit (world units).</summary>
    [Export] public float HitDropDistance = 1.5f;

    /// <summary>How long the drop animation takes.</summary>
    [Export] public float DropDuration = 0.18f;

    /// <summary>How high above the original nail position the spawned item appears.</summary>
    [Export] public float ItemSpawnYOffset = 1.5f;

    private int _hits = 0;
    private Vector3 _restPos;
    private Tween _tween;
    private bool _spawned = false;

    // Guard against firing twice per ground-pound. The nail moves out from under
    // Mario during the drop, which can cause body_exited+body_entered to fire
    // within a single pound — exit-based gating isn't reliable. Instead, latch a
    // flag on hit and only clear it once Mario's state has fully left the
    // ground-pound family.
    private bool _hitThisPound = false;
    private Mario _lastHitter;

    public override void _Ready()
    {
        _restPos = Position;

        // Accept any Area3D child as the hit zone (so the user isn't locked into one name).
        Area3D hitZone = null;
        foreach (var child in GetChildren())
        {
            if (child is Area3D a) { hitZone = a; break; }
        }
        if (hitZone == null)
        {
            GD.PushError($"[Nail] '{Name}' is missing an Area3D child to use as a hit zone. The nail won't react to ground pounds.");
            return;
        }
        hitZone.BodyEntered += OnHitZoneEntered;
        GD.Print($"[Nail] '{Name}' wired to hit zone '{hitZone.Name}'.");
    }

    public override void _PhysicsProcess(double delta)
    {
        // Reset the per-pound latch once Mario has fully left the ground-pound states.
        if (!_hitThisPound) return;
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
        if (body is not Mario mario) return;
        if (_spawned) return;
        if (_hitThisPound) return; // already counted this pound; wait until Mario leaves the state

        bool isPounding =
            mario.stateOfMario == Mario.MarioState.groundPoundFalling
            || mario.stateOfMario == Mario.MarioState.groundPoundLanding;
        if (!isPounding) return;

        _hitThisPound = true;
        _lastHitter = mario;
        Hit();
    }

    private void Hit()
    {
        _hits++;

        Vector3 newPos = _restPos + new Vector3(0f, -_hits * HitDropDistance, 0f);

        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(this, "position", newPos, DropDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        if (_hits >= RequiredHits && !_spawned)
        {
            _spawned = true;
            _tween.TweenCallback(Callable.From(SpawnItem));
        }
    }

    private void SpawnItem()
    {
        if (ItemToSpawn == null)
        {
            GD.PushWarning($"[Nail] '{Name}' has no ItemToSpawn set; nothing to spawn.");
            return;
        }

        var item = ItemToSpawn.Instantiate<Node3D>();
        // Parent into the same scene as the nail so the item lives on after the nail moves.
        GetParent().AddChild(item);

        Vector3 spawnPos = ToGlobal(_restPos - Position) + new Vector3(0f, ItemSpawnYOffset, 0f);
        item.GlobalPosition = spawnPos;

        // If the spawned item supports the drop-and-bounce behavior, kick it off.
        if (item.HasMethod("LaunchAsDrop"))
            item.Call("LaunchAsDrop");
    }
}
