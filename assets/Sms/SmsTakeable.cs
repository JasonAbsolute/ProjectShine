using Godot;
using System.Collections.Generic;

/// <summary>
/// The game's TTakeActor data for a thing Mario can pick up, as the port reads it (pass 62). OPTIONAL: a child node of the
/// RigidBody3D with the numbers exported -- without one the port derives them from his Fruit.Type (the disc has no .prm for
/// these; they are the compiled-in tables of MapObjInit.cpp: coconut_physical_data 1543, papaya 1571, pine 1593, durian 1653,
/// banana 1691, red_pepper 1713, watermelon 3531, wood_barrel 1976) or falls back to the generic fruit. Everything in the game's
/// units: the hit cylinder in units (74.44 a metre; fruit_hit_data_table {40, 60, 50, 80} = receive radius, height, attack radius,
/// height, MapObjBase.cpp setObjHitData), ThrowRate x ThrowPower 2.8 = the launch speed forward in units a 30 fps frame (TMapObjGeneral::
/// thrown, MapObjGeneral.cpp 109: physical unk2C), ThrowVy the same up (unk30), Gravity units a tick^2 (unk0, getGravityY), Bounce the
/// ground restitution (unk4: vy *= -Bounce while |vy| > BounceMinVy (unkC), touchGround 437), BounceFriction on vx / vz at each bounce
/// (unk10), WallReflect (unk8, calcReflectingVelocity). Kind: isThrowStart (MarioRun.cpp 50) always PITCHES an enemy (actor type bit
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
    [Export] public float HitRadius = 40f;
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
            case Preset.Durian: return new SmsTakeData(TakeKind.Object, 40f, 60f, 0.01f, 5f, 0.3f, 0.6f, 0.2f, 0.97f, 0.2f, "durian"); // kicked in the game, never carried
            case Preset.Banana: return new SmsTakeData(TakeKind.Object, 40f, 60f, 2.5f, 14f, 0.3f, 0.4f, 1.0f, 0.4f, 0.2f, "banana");
            case Preset.RedPepper: return new SmsTakeData(TakeKind.Object, 40f, 60f, 5f, 12f, 0.3f, 0.6f, 0.3f, 0.97f, 0.3f, "red_pepper");
            case Preset.Watermelon: return new SmsTakeData(TakeKind.Object, 300f, 60f, 0f, 0f, 0.25f, 0.4f, 0.1f, 0.97f, 0.3f, "watermelon"); // rolled, not thrown
            case Preset.WoodBarrel: return new SmsTakeData(TakeKind.Object, 40f, 60f, 3.5f, 20f, 0.35f, 0f, 0f, 0.95f, 0f, "wood_barrel");
            case Preset.Enemy: return new SmsTakeData(TakeKind.Enemy, 40f, 60f, 2f, 10f, 0.2f, 0f, 0f, 1f, 0f, "enemy"); // tamanoko.prm: mSLThrownRateXZ 2.0, VY 10.0, gravity 0.20
            default: return new SmsTakeData(TakeKind.Object, 40f, 60f, 2f, 15f, 0.3f, 0.6f, 0.3f, 0.97f, 0.3f, "fruit");
        }
    }

    public void Apply(Preset p)
    {
        SmsTakeData d = Table(p);
        Kind = d.Kind; HitRadius = d.HitRadius; HitHeight = d.HitHeight; ThrowRate = d.ThrowRate; ThrowVy = d.ThrowVy;
        Gravity = d.Gravity; Bounce = d.Bounce; BounceMinVy = d.BounceMinVy; BounceFriction = d.BounceFriction; WallReflect = d.WallReflect;
    }

    public SmsTakeData Data => new SmsTakeData(Kind, HitRadius, HitHeight, ThrowRate, ThrowVy, Gravity, Bounce, BounceMinVy, BounceFriction, WallReflect,
        Numbers == Preset.Custom ? "custom" : Numbers.ToString().ToLowerInvariant());

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
            switch (f.Type)
            {
                case Fruit.FruitType.Banana: return Table(Preset.Banana);
                case Fruit.FruitType.Coconut: return Table(Preset.Coconut);
                case Fruit.FruitType.Papaya: return Table(Preset.Papaya);
                case Fruit.FruitType.Pineapple: return Table(Preset.Pineapple);
                case Fruit.FruitType.RedPepper: return Table(Preset.RedPepper);
                case Fruit.FruitType.Durian: return Table(Preset.Durian);
            }
        }
        return Table(Preset.Fruit);
    }

    /// <summary>Whether a body may be taken at all: its SmsTakeable's CanTake, else his Fruit.CanBePickedUp, else yes.</summary>
    public static bool MayTake(Node body)
    {
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
}

/// <summary>
/// A thrown body flies on the game's numbers, not his: TMapObjGeneral::calcVelocity (MapObjGeneral.cpp 477) takes Gravity off vy every
/// tick with no air drag (unk18 = 1.0), touchGround (437) bounces at -Bounce while |vy| > BounceMinVy. The port keeps his RigidBody3D
/// and gives it the game's gravity (AccelToMps2(Gravity) / the project's) and restitution for the flight, then hands his own numbers
/// back once it has come to rest (asleep) or is picked up again. The ground friction at a bounce (BounceFriction) and the wall
/// reflection (WallReflect) stay the body's own material -- noted, not modelled.
/// </summary>
public static class SmsThrownFlight
{
    private sealed class Entry
    {
        public RigidBody3D Body;
        public float GravityScale, LinearDamp;
        public PhysicsMaterial Material;
        public double T;
    }

    private static readonly List<Entry> _flights = new();

    public static void Begin(RigidBody3D rb, SmsTakeData d)
    {
        if (rb == null)
            return;
        End(rb);
        float g = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8f);
        var e = new Entry { Body = rb, GravityScale = rb.GravityScale, LinearDamp = rb.LinearDamp, Material = rb.PhysicsMaterialOverride };
        rb.GravityScale = g > 0f ? SmsUnits.AccelToMps2(d.Gravity) / g : rb.GravityScale;
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
            }
        }
    }

    private static void Restore(Entry e)
    {
        if (!GodotObject.IsInstanceValid(e.Body))
            return;
        e.Body.GravityScale = e.GravityScale;
        e.Body.LinearDamp = e.LinearDamp;
        e.Body.PhysicsMaterialOverride = e.Material;
    }
}
