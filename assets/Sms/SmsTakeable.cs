using Godot;
using System.Collections.Generic;

/// <summary>
/// The game's TTakeActor data for a thing Mario can pick up, as the port reads it (pass 62). OPTIONAL: a child node of the
/// RigidBody3D with the numbers exported -- without one the port derives them from his Fruit.Type (the disc has no .prm for
/// these; they are the compiled-in tables of MapObjInit.cpp: coconut_physical_data 1543, papaya 1571, pine 1593, durian 1653,
/// banana 1691, red_pepper 1713, watermelon 3531, wood_barrel 1976) or falls back to the generic fruit. Everything in the game's
/// units: the hit cylinder in units (74.44 a metre; a *_hit_data_table row = receive radius, height, attack radius, height: fruit
/// {60, 60, 80, 90} (the red pepper uses it), coconut / papaya {40, 60, 50, 80}, pine {80, 60, 50, 80}, banana {80, 60, 60, 90},
/// durian {150, 60, 60, 90}; MapObjBase.cpp setObjHitData multiplies radius by max(scale.x, scale.z) and height by scale.y --
/// Size below / Fruit.Size is that scale), ThrowRate x ThrowPower 2.8 = the launch speed forward in units a 120 Hz TICK (TMapObjGeneral::
/// thrown, MapObjGeneral.cpp 109: physical unk2C; TLiveActor::bind adds mVelocity whole every CUE_MOVE, 4 a 30 fps frame -- SmsUnits.
/// TickSpeedToMps, never Mario's SpeedToMps: pass 75), ThrowVy the same up (unk30), Gravity units a tick^2 (unk0, getGravityY), Bounce the
/// ground restitution (unk4: vy *= -Bounce while |vy| > BounceMinVy (unkC), touchGround 437), BounceFriction on vx / vz every tick
/// the object touches the ground (unk10: a fruit is a TMapObjBall, whose touchGround -- asm only, QbeRoot MapObjBall.s 801BCC64 --
/// multiplies by it on every ground contact, bounce or roll, so a coconut rolls out ~190 units from a 5.6 u/f landing), WallReflect
/// (unk8, calcReflectingVelocity: v -= (1 + k)(v.n)n). Kind: isThrowStart (MarioRun.cpp 50) always PITCHES an enemy (actor type bit
/// 0x10000000); a map object is pitched only while he runs faster than 16, else PUT down.
/// </summary>
[GlobalClass]
public partial class SmsTakeable : Node
{
    public enum TakeKind { Object, Enemy }
    public enum Preset { Custom, Fruit, Coconut, Papaya, Pineapple, Durian, Banana, RedPepper, Watermelon, WoodBarrel, Enemy }

    /// <summary>Picking one fills the fields from the game's table when the node is ready (Custom keeps what you typed).</summary>
    [Export] public Preset Numbers = Preset.Fruit;
    [Export] public TakeKind Kind = TakeKind.Object;
    [Export] public bool CanTake = true;
    /// <summary>The game's mScaling (uniform) for a body without a Fruit script: the hit cylinder below is multiplied by it. Scale the
    /// body's model and shapes yourself (a RigidBody3D must stay at scale 1).</summary>
    [Export] public float Size = 1f;
    [Export] public float HitRadius = 60f;
    [Export] public float HitHeight = 60f;
    [Export] public float ThrowRate = 2f;
    [Export] public float ThrowVy = 15f;
    [Export] public float Gravity = 0.3f;
    [Export] public float Bounce = 0.6f;
    [Export] public float BounceMinVy = 0.3f;
    [Export] public float BounceFriction = 0.97f;
    [Export] public float WallReflect = 0.3f;

    public override void _Ready()
    {
        if (Numbers != Preset.Custom)
            Apply(Numbers);
    }

    /// <summary>The table row for a preset (MapObjInit.cpp; the hit cylinder from the matching *_hit_data_table).</summary>
    public static SmsTakeData Table(Preset p)
    {
        // { gravity, bounce, wallReflect, bounceMinVy, bounceFriction, unk14, airDrag, wallCheckUp, unk20, unk24, unk28, throwRate, throwVy }
        switch (p)
        {
            case Preset.Coconut: return new SmsTakeData(TakeKind.Object, 40f, 60f, 2f, 15f, 0.3f, 0.6f, 0.3f, 0.97f, 0.3f, "coconut");
            case Preset.Papaya: return new SmsTakeData(TakeKind.Object, 40f, 60f, 5f, 20f, 0.3f, 0.6f, 0.3f, 0.97f, 0.3f, "papaya");
            case Preset.Pineapple: return new SmsTakeData(TakeKind.Object, 80f, 60f, 2f, 22f, 0.3f, 0.6f, 0.3f, 0.97f, 0.3f, "pine");
            case Preset.Durian: return new SmsTakeData(TakeKind.Object, 150f, 60f, 0.01f, 5f, 0.3f, 0.6f, 0.2f, 0.97f, 0.2f, "durian"); // kicked in the game, never carried
            case Preset.Banana: return new SmsTakeData(TakeKind.Object, 80f, 60f, 2.5f, 14f, 0.3f, 0.4f, 1.0f, 0.4f, 0.2f, "banana");
            case Preset.RedPepper: return new SmsTakeData(TakeKind.Object, 60f, 60f, 5f, 12f, 0.3f, 0.6f, 0.3f, 0.97f, 0.3f, "red_pepper"); // RedPepper_data -> fruit_obj_hit_info
            case Preset.Watermelon: return new SmsTakeData(TakeKind.Object, 300f, 60f, 0f, 0f, 0.25f, 0.4f, 0.1f, 0.97f, 0.3f, "watermelon"); // rolled, not thrown
            case Preset.WoodBarrel: return new SmsTakeData(TakeKind.Object, 40f, 60f, 3.5f, 20f, 0.35f, 0f, 0f, 0.95f, 0f, "wood_barrel");
            case Preset.Enemy: return new SmsTakeData(TakeKind.Enemy, 40f, 60f, 2f, 10f, 0.2f, 0f, 0f, 1f, 0f, "enemy"); // tamanoko.prm: mSLThrownRateXZ 2.0, VY 10.0, gravity 0.20
            default: return new SmsTakeData(TakeKind.Object, 60f, 60f, 2f, 15f, 0.3f, 0.6f, 0.3f, 0.97f, 0.3f, "fruit");
        }
    }

    public void Apply(Preset p)
    {
        SmsTakeData d = Table(p);
        Kind = d.Kind; HitRadius = d.HitRadius; HitHeight = d.HitHeight; ThrowRate = d.ThrowRate; ThrowVy = d.ThrowVy;
        Gravity = d.Gravity; Bounce = d.Bounce; BounceMinVy = d.BounceMinVy; BounceFriction = d.BounceFriction; WallReflect = d.WallReflect;
    }

    public SmsTakeData Data => new SmsTakeData(Kind, HitRadius, HitHeight, ThrowRate, ThrowVy, Gravity, Bounce, BounceMinVy, BounceFriction, WallReflect,
        Numbers == Preset.Custom ? "custom" : Numbers.ToString().ToLowerInvariant()).Scaled(Size);

    /// <summary>The body's mScaling: its SmsTakeable's Size, else his Fruit's Size, else 1.</summary>
    public static float SizeOf(Node body)
    {
        if (body == null)
            return 1f;
        foreach (Node c in body.GetChildren())
            if (c is SmsTakeable t)
                return t.Size > 0f ? t.Size : 1f;
        if (body is Fruit f && f.Size > 0f)
            return f.Size;
        return 1f;
    }

    /// <summary>The numbers for a body: its SmsTakeable child if it has one, else his Fruit's type, else the generic fruit.</summary>
    public static SmsTakeData For(Node body)
    {
        if (body == null)
            return Table(Preset.Fruit);
        foreach (Node c in body.GetChildren())
            if (c is SmsTakeable t)
                return t.Data;
        if (body is Fruit f)
        {
            float s = f.Size > 0f ? f.Size : 1f;
            switch (f.Type)
            {
                case Fruit.FruitType.Banana: return Table(Preset.Banana).Scaled(s);
                case Fruit.FruitType.Coconut: return Table(Preset.Coconut).Scaled(s);
                case Fruit.FruitType.Papaya: return Table(Preset.Papaya).Scaled(s);
                case Fruit.FruitType.Pineapple: return Table(Preset.Pineapple).Scaled(s);
                case Fruit.FruitType.RedPepper: return Table(Preset.RedPepper).Scaled(s);
                case Fruit.FruitType.Durian: return Table(Preset.Durian).Scaled(s);
            }
            return Table(Preset.Fruit).Scaled(s);
        }
        return Table(Preset.Fruit);
    }

    /// <summary>Whether a body may be taken at all: its SmsTakeable's CanTake, else his Fruit.CanBePickedUp, else yes.</summary>
    public static bool MayTake(Node body)
    {
        if (body is Fruit ball && !ball.SmsHoldable) return false;   // pass 115: TMapObjBall::hold refuses a ball faster than 10 units a tick
        foreach (Node c in body.GetChildren())
            if (c is SmsTakeable t)
                return t.CanTake;
        return body is not Fruit f || f.CanBePickedUp;
    }
}

/// <summary>One object's numbers (see <see cref="SmsTakeable"/>).</summary>
public readonly struct SmsTakeData
{
    public readonly SmsTakeable.TakeKind Kind;
    public readonly float HitRadius, HitHeight, ThrowRate, ThrowVy, Gravity, Bounce, BounceMinVy, BounceFriction, WallReflect;
    public readonly string Name;

    public SmsTakeData(SmsTakeable.TakeKind kind, float hitRadius, float hitHeight, float throwRate, float throwVy, float gravity, float bounce,
        float bounceMinVy, float bounceFriction, float wallReflect, string name)
    {
        Kind = kind; HitRadius = hitRadius; HitHeight = hitHeight; ThrowRate = throwRate; ThrowVy = throwVy; Gravity = gravity; Bounce = bounce;
        BounceMinVy = bounceMinVy; BounceFriction = bounceFriction; WallReflect = wallReflect; Name = name;
    }

    /// <summary>The row at an object scale: the hit cylinder's radius and height multiplied (setObjHitData); the flight numbers are per
    /// type, not per scale, in the game too.</summary>
    public SmsTakeData Scaled(float s) => s > 0f && !Mathf.IsEqualApprox(s, 1f)
        ? new SmsTakeData(Kind, HitRadius * s, HitHeight * s, ThrowRate, ThrowVy, Gravity, Bounce, BounceMinVy, BounceFriction, WallReflect, Name)
        : this;

    /// <summary>The row with another gravity (Mario's ThrowGravityScale deviation knob).</summary>
    public SmsTakeData WithGravity(float g) => Mathf.IsEqualApprox(g, Gravity) ? this
        : new SmsTakeData(Kind, HitRadius, HitHeight, ThrowRate, ThrowVy, g, Bounce, BounceMinVy, BounceFriction, WallReflect, Name);
}

/// <summary>
/// A thrown body flies on the game's numbers, not his: TMapObjGeneral::calcVelocity (MapObjGeneral.cpp 477) takes Gravity off vy every
/// 120 Hz tick with no air drag (unk18 = 1.0), touchGround (437) bounces at -Bounce while |vy| > BounceMinVy. The port keeps his RigidBody3D
/// and gives it the game's gravity (TickAccelToMps2(Gravity) / the project's) and restitution for the flight, then hands his own numbers
/// back once it has come to rest (asleep) or is picked up again. The ground friction at a bounce (BounceFriction) and the wall
/// reflection (WallReflect) stay the body's own material -- noted, not modelled. The ground ROLL-OUT is the game's (pass 62b): while the
/// body touches the ground its vx / vz (and spin) decay by BounceFriction a 120 Hz tick (TMapObjBall::touchGround, asm, from control()
/// every CUE_MOVE -- pass 75; 62b had guessed 60 Hz) and stop below
/// BounceMinVy on a flat floor (calcVelocity, MapObjGeneral.cpp 490). PENDING the US main.dol: the ball's own landing threshold and
/// slope acceleration (TMapObjBall 0x188 / 0x180, per fruit id in initMapObj) and its visual spin (calcCurrentMtx) are literals the
/// asm pulls from the DOL's float pool -- Godot's bounce / rolling stand in until the file is here.
/// </summary>
public static class SmsThrownFlight
{
    private sealed class Entry
    {
        public RigidBody3D Body;
        public float GravityScale, LinearDamp;
        public RigidBody3D.DampMode DampMode;
        public PhysicsMaterial Material;
        public double T;
        public SmsTakeData Data;
        public float HalfH;
    }

    private static readonly List<Entry> _flights = new();

    public static void Begin(RigidBody3D rb, SmsTakeData d)
    {
        if (rb == null)
            return;
        if (rb is Fruit simFruit && simFruit.SmsBallSimActive)
            return; // pass 115: the fruit's own ball sim flies it (Fruit.SmsThrown); nothing here to restore
        End(rb);
        float g = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8f);
        var e = new Entry { Body = rb, GravityScale = rb.GravityScale, LinearDamp = rb.LinearDamp, DampMode = rb.LinearDampMode, Material = rb.PhysicsMaterialOverride, Data = d, HalfH = HalfHeight(rb) };
        rb.GravityScale = g > 0f ? SmsUnits.TickAccelToMps2(d.Gravity) / g : rb.GravityScale; // units a tick^2 (pass 75)
        rb.LinearDampMode = RigidBody3D.DampMode.Replace; // not Combine: the world's default 0.1 damp would be an air drag the game's object has not (unk18 = 1.0)
        rb.LinearDamp = 0f;
        var mat = new PhysicsMaterial { Bounce = d.Bounce, Friction = e.Material != null ? e.Material.Friction : 1f };
        rb.PhysicsMaterialOverride = mat;
        _flights.Add(e);
    }

    /// <summary>Hands the body its own numbers back (a re-take, or the flight is over).</summary>
    public static void End(RigidBody3D rb)
    {
        for (int i = _flights.Count - 1; i >= 0; i--)
            if (_flights[i].Body == rb)
            {
                Restore(_flights[i]);
                _flights.RemoveAt(i);
            }
    }

    /// <summary>Once a physics frame: a body asleep (Godot's rest, physics/3d/time_before_sleep) or flying for 12 s gets its numbers back.</summary>
    public static void Tick(double delta)
    {
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            Entry e = _flights[i];
            if (!GodotObject.IsInstanceValid(e.Body))
            {
                _flights.RemoveAt(i);
                continue;
            }
            e.T += delta;
            if (e.Body.Sleeping || e.T > 12.0)
            {
                Restore(e);
                _flights.RemoveAt(i);
                continue;
            }
            RollOut(e, delta);
        }
    }

    /// <summary>TMapObjBall::touchGround's ground tick (asm 801BCC64): on a map floor vx, vz *= unk10 (BounceFriction) -- once a 120 Hz
    /// tick, control() running every CUE_MOVE (pass 75) -- then calcVelocity (MapObjGeneral.cpp 490): on a flat floor (normal.y == 1) a
    /// component under unkC (BounceMinVy, units a tick) is 0. The spin follows (the ball's visual roll is its velocity's).</summary>
    private static void RollOut(Entry e, double delta)
    {
        RigidBody3D rb = e.Body;
        if (rb.Freeze || !rb.IsInsideTree())
            return;
        var space = rb.GetWorld3D().DirectSpaceState;
        Vector3 from = rb.GlobalPosition;
        var q = PhysicsRayQueryParameters3D.Create(from, from - Vector3.Up * (e.HalfH + 0.06f), rb.CollisionMask, new Godot.Collections.Array<Rid> { rb.GetRid() });
        var hit = space.IntersectRay(q);
        if (hit.Count == 0)
            return;
        if (hit["collider"].AsGodotObject() is not Node col || col is RigidBody3D || col is CharacterBody3D || col is Area3D)
            return; // the game's object collides with the map (BG) only
        Vector3 n = (Vector3)hit["normal"];
        float k = Mathf.Pow(Mathf.Clamp(e.Data.BounceFriction, 0f, 1f), SmsUnits.TicksPerSecond * (float)delta);
        Vector3 v = rb.LinearVelocity;
        v.X *= k;
        v.Z *= k;
        if (n.Y > 0.999f)
        {
            float minV = SmsUnits.TickSpeedToMps(e.Data.BounceMinVy);
            if (Mathf.Abs(v.X) < minV) v.X = 0f;
            if (Mathf.Abs(v.Z) < minV) v.Z = 0f;
        }
        rb.LinearVelocity = v;
        rb.AngularVelocity = (v.X == 0f && v.Z == 0f) ? Vector3.Zero : rb.AngularVelocity * k;
    }

    /// <summary>Half the body's collision height (the ground probe's reach): its first shape's extent, else 0.3 m.</summary>
    private static float HalfHeight(RigidBody3D rb)
    {
        foreach (Node c in rb.GetChildren())
        {
            if (c is not CollisionShape3D cs || cs.Shape == null)
                continue;
            float sy = Mathf.Abs(cs.GlobalTransform.Basis.Scale.Y);
            switch (cs.Shape)
            {
                case SphereShape3D s: return s.Radius * sy;
                case BoxShape3D b: return b.Size.Y * 0.5f * sy;
                case CapsuleShape3D cap: return cap.Height * 0.5f * sy;
                case CylinderShape3D cyl: return cyl.Height * 0.5f * sy;
            }
        }
        return 0.3f;
    }

    private static void Restore(Entry e)
    {
        if (!GodotObject.IsInstanceValid(e.Body))
            return;
        e.Body.GravityScale = e.GravityScale;
        e.Body.LinearDampMode = e.DampMode;
        e.Body.LinearDamp = e.LinearDamp;
        e.Body.PhysicsMaterialOverride = e.Material;
    }
}
