using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// What an enemy drops when it dies (pass 125d) -- a Resource you make once (New SmsDropTable, or a .tres under assets/Sms/Enemy/Drops/) and
/// assign to any SmsSmallEnemy's DropTable; one table can serve a whole species or one placed enemy can have its own. A death ROLLS ONCE:
/// <see cref="Chance"/> decides whether anything drops at all, then one line is picked by weight and its count of the scene comes out at the
/// death point, popped the game's way. Add kinds of item later by adding lines -- nothing in the enemy code names a coin or a mushroom.
///
/// THE GAME: TSmallEnemy::genRandomItem (smallEnemy.cpp 326) = setAfterDeadEffect + genEventCoin (a coin the stage placed WITH the enemy,
/// thrown straight up at 20 u a tick) + generateItem, which rolls 0..100 against mSLGenItemRate + mSLGenEggRate of the .prm (the Stu: 100 + 80
/// = always) and asks the map-object manager for ACTOR_TYPE_ITEM_UNK8 -- a type no shipped stage carries, so the retail game drops NOTHING
/// from it; its coins come from the placed coin and the serial crash. The table is the DEVIATION Jaysonn asked for; what is kept of the game:
/// the hook (genRandomItem, skipped for a death flagged UNK20000 -- the wall crash -- or an already-dead body), the point (x, mGroundHeight, z),
/// the pop (0, 20, 0) u a tick with the coin's own item life, the .prm rate as an option (<see cref="UseEnemyRate"/>).
/// </summary>
[GlobalClass]
public partial class SmsDropTable : Resource
{
    /// <summary>The odds that a death drops anything: 1 = always, 0.3 = three deaths in ten. Ignored when <see cref="UseEnemyRate"/> is on.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float Chance = 1f;
    /// <summary>Use the species' .prm odds instead (generateItem's mSLGenItemRate + mSLGenEggRate against 0..100; the Stu's 180 = always).</summary>
    [Export] public bool UseEnemyRate = false;
    /// <summary>The lines, picked by weight. An entry with no Scene is a "nothing" result with a weight of its own.</summary>
    [Export] public SmsDropEntry[] Entries = Array.Empty<SmsDropEntry>();
    /// <summary>The pop: the game's genEventCoin throws the death coin at (0, 20, 0) units a tick (7.5 m up under the coin's gravity).</summary>
    [Export(PropertyHint.Range, "0,60,0.5")] public float PopUnitsPerTick = SmsCoinItem.PopUpSpeedUnits;
    /// <summary>A CharacterBody3D / RigidBody3D item (his 1-UP mushroom) has no SMS item life: it gets this many metres a second upward instead
    /// (6 = a 1.8 m hop under 9.8 m/s^2), and the scatter x this / the pop for its sideways part.</summary>
    [Export(PropertyHint.Range, "0,30,0.5")] public float BodyPopMetresPerSecond = 6f;

    /// <summary>One death's result: the lines that came out with their counts (one line per roll; empty = nothing). Deterministic for a given rng.</summary>
    public List<(SmsDropEntry entry, int count)> Roll(Random rng, float enemyRatePercent)
    {
        var result = new List<(SmsDropEntry, int)>();
        bool drops = UseEnemyRate ? rng.NextDouble() * 100.0 < enemyRatePercent : rng.NextDouble() < Chance;
        if (!drops) return result;
        var e = Pick(rng);
        if (e == null || e.Scene == null) return result;
        int n = e.RollCount(rng);
        if (n > 0) result.Add((e, n));
        return result;
    }

    /// <summary>The weighted pick among the lines (null when the table is empty or every weight is 0).</summary>
    public SmsDropEntry Pick(Random rng)
    {
        float total = 0f;
        foreach (var e in Entries) if (e != null && e.Weight > 0f) total += e.Weight;
        if (total <= 0f) return null;
        double r = rng.NextDouble() * total;
        foreach (var e in Entries)
        {
            if (e == null || e.Weight <= 0f) continue;
            r -= e.Weight;
            if (r < 0.0) return e;
        }
        for (int i = Entries.Length - 1; i >= 0; i--) if (Entries[i] != null && Entries[i].Weight > 0f) return Entries[i];
        return null;
    }

    /// <summary>The death's drops at <paramref name="groundPoint"/> (metres; the game's (x, mGroundHeight, z)), under <paramref name="parent"/>: every
    /// item popped up, the extra ones fanned out. Returns the nodes.</summary>
    public List<Node3D> Spawn(Random rng, float enemyRatePercent, Node parent, Vector3 groundPoint, Node3D by = null)
    {
        var nodes = new List<Node3D>();
        foreach (var (entry, count) in Roll(rng, enemyRatePercent))
            for (int i = 0; i < count; i++)
            {
                // the scatter: the first one straight up, the rest in a fan around the point
                Vector3 side = Vector3.Zero;
                if (i > 0 && entry.ScatterUnits > 0f)
                {
                    float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                    side = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * entry.ScatterUnits * (0.5f + 0.5f * (float)rng.NextDouble());
                }
                var node = SpawnItem(entry.Scene, parent, groundPoint, new Vector3(side.X, PopUnitsPerTick, side.Z), entry.LifeTicks, by);
                if (node != null) nodes.Add(node);
            }
        return nodes;
    }

    /// <summary>One item of <paramref name="scene"/> at <paramref name="at"/> with <paramref name="velUnitsPerTick"/> (SMS units a tick, world axes): a coin
    /// (anything with SmsThrow) flies with the game's item life for <paramref name="lifeTicks"/> (0 = its kind's default); a CharacterBody3D /
    /// RigidBody3D gets the velocity as a hop (BodyPopMetresPerSecond for the up part); anything else is placed. An <c>SmsDropped(Node3D)</c>
    /// method on the item is called last. Also used for the Stu's serial 1-UP and serial-crash coin, so every drop pops the same way.</summary>
    public Node3D SpawnItem(PackedScene scene, Node parent, Vector3 at, Vector3 velUnitsPerTick, int lifeTicks = 0, Node3D by = null)
    {
        if (scene == null || parent == null) return null;
        var node = scene.Instantiate<Node3D>();
        parent.AddChild(node);
        node.GlobalPosition = at;
        Launch(node, velUnitsPerTick, lifeTicks, BodyPopMetresPerSecond);
        if (node.HasMethod("SmsDropped")) node.Call("SmsDropped", by);
        return node;
    }

    /// <summary>The launch by the item's kind (static: a drive or another spawner can use it).</summary>
    public static void Launch(Node3D node, Vector3 velUnitsPerTick, int lifeTicks, float bodyPopMetresPerSecond)
    {
        if (node.HasMethod("SmsThrow"))
        {
            int life = lifeTicks != 0 ? lifeTicks : (node is YellowCoin ? SmsCoinItem.DisappearTicksDefault : -1);   // TCoin::appearWithoutSound: a yellow goes, a blue stays
            node.Call("SmsThrow", velUnitsPerTick, life, default(Variant));
            return;
        }
        // units a tick -> metres a second for a Godot body: x 120 / 74.44; the up part is the table's hop (the body has its own gravity)
        float k = SmsUnits.TicksPerSecond / SmsUnits.UnitsPerMetre;
        Vector3 v = new Vector3(velUnitsPerTick.X * k * (bodyPopMetresPerSecond / Mathf.Max(velUnitsPerTick.Y * k, 1e-3f)), bodyPopMetresPerSecond, velUnitsPerTick.Z * k * (bodyPopMetresPerSecond / Mathf.Max(velUnitsPerTick.Y * k, 1e-3f)));
        if (node is CharacterBody3D cb) cb.Velocity = v;
        else if (node is RigidBody3D rb) rb.LinearVelocity = v;
    }
}
