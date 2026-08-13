using Godot;

/// <summary>
/// Shared bounce-and-rest physics for spawned items (coins, etc).
/// The owning script holds a <see cref="State"/> and calls <see cref="Tick"/>
/// each physics frame. Tick returns true when the item should be freed.
/// </summary>
public static class BouncePhysics
{
    public const float GRAVITY = -25f;
    public const float INITIAL_POP_Y = 8f;
    public const float BOUNCE_RESTITUTION = 0.45f;
    public const float HORIZ_DAMP_PER_BOUNCE = 0.7f;
    public const float REST_VEL_THRESHOLD = 0.6f;
    public const float DESPAWN_AFTER_REST = 7f;
    public const float FLASH_START = 5f;          // after 5s of rest, start the "about to vanish" flash
    public const float FLASH_FREQ_START = 3f;     // Hz at the moment flashing begins
    public const float FLASH_FREQ_END = 12f;      // Hz at the moment of despawn
    public const float GROUND_RAY_UP = 0.4f;
    public const float GROUND_RAY_DOWN = 4.0f;

    public struct State
    {
        public bool Active;
        public Vector3 Velocity;
        public bool AtRest;
        public float RestTimer;
        public float FlashPhase;
    }

    public static void Launch(ref State s, Vector3 initialVelocity)
    {
        s.Active = true;
        s.Velocity = initialVelocity;
        s.AtRest = false;
        s.RestTimer = 0f;
    }

    /// <summary>Advance bounce physics by one frame. Returns true if the node should QueueFree.</summary>
    public static bool Tick(ref State s, Node3D node, float delta)
    {
        if (!s.Active) return false;

        if (s.AtRest)
        {
            s.RestTimer += delta;
            UpdateFlash(ref s, node, delta);
            return s.RestTimer >= DESPAWN_AFTER_REST;
        }

        s.Velocity.Y += GRAVITY * delta;
        node.GlobalPosition += s.Velocity * delta;

        var space = node.GetWorld3D()?.DirectSpaceState;
        if (space == null) return false;

        Vector3 from = node.GlobalPosition + new Vector3(0f, GROUND_RAY_UP, 0f);
        Vector3 to = from + new Vector3(0f, -(GROUND_RAY_UP + GROUND_RAY_DOWN), 0f);
        var q = PhysicsRayQueryParameters3D.Create(from, to);
        q.CollideWithBodies = true;
        q.CollideWithAreas = false;

        // Skip the player so the coin falls THROUGH Mario rather than landing on his
        // head. The coin's own pickup Area3D will trigger collection when its zone
        // overlaps Mario during the pass-through.
        var excludes = new Godot.Collections.Array<Godot.Rid>();
        foreach (var n in node.GetTree().GetNodesInGroup("player"))
            if (n is CollisionObject3D co) excludes.Add(co.GetRid());
        if (excludes.Count > 0) q.Exclude = excludes;

        var result = space.IntersectRay(q);
        if (result.Count == 0) return false;

        float groundY = ((Vector3)result["position"]).Y;
        if (node.GlobalPosition.Y > groundY) return false;

        // Hit the floor — bounce or rest.
        Vector3 pos = node.GlobalPosition;
        pos.Y = groundY;
        node.GlobalPosition = pos;

        if (Mathf.Abs(s.Velocity.Y) < REST_VEL_THRESHOLD)
        {
            s.AtRest = true;
            s.Velocity = Vector3.Zero;
        }
        else
        {
            s.Velocity.Y = -s.Velocity.Y * BOUNCE_RESTITUTION;
            s.Velocity.X *= HORIZ_DAMP_PER_BOUNCE;
            s.Velocity.Z *= HORIZ_DAMP_PER_BOUNCE;
        }
        return false;
    }

    private static void UpdateFlash(ref State s, Node3D node, float delta)
    {
        if (s.RestTimer <= FLASH_START)
        {
            if (!node.Visible) node.Visible = true;
            return;
        }

        // Ramp flash frequency over the final (DESPAWN_AFTER_REST - FLASH_START) seconds.
        float t = Mathf.Clamp((s.RestTimer - FLASH_START) / (DESPAWN_AFTER_REST - FLASH_START), 0f, 1f);
        float freq = Mathf.Lerp(FLASH_FREQ_START, FLASH_FREQ_END, t);

        s.FlashPhase += delta * freq;
        bool visible = ((int)Mathf.Floor(s.FlashPhase) % 2) == 0;
        if (node.Visible != visible) node.Visible = visible;
    }
}
