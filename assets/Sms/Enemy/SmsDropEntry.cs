using Godot;

/// <summary>
/// One line of an <see cref="SmsDropTable"/>: a scene an enemy can drop when it dies, with its weight against the table's other lines and how
/// many come out. Make one per item kind in the inspector (New SmsDropEntry) or as its own .tres to share between tables -- a coin, a 1-UP, a
/// fruit, a water bottle, a shine sprite, anything with a scene: the spawner (<see cref="SmsDropTable.SpawnItem"/>) launches a coin with the
/// game's item life (SmsThrow), gives any CharacterBody3D / RigidBody3D a hop, and just places anything else -- and tells the item it was dropped
/// if it has an <c>SmsDropped(Node3D by)</c> method, for things that want to react (pass 125d).
/// </summary>
[GlobalClass]
public partial class SmsDropEntry : Resource
{
    /// <summary>What comes out (his yellow_coin.tscn, blue_coin.tscn, 1_up_mushroom.tscn, ...). Empty = this line drops nothing (a "nothing" slot with a weight).</summary>
    [Export] public PackedScene Scene;
    /// <summary>The odds against the table's other lines: a line of weight 80 next to lines of 12 and 8 comes out 80 % of the time a drop happens.</summary>
    [Export(PropertyHint.Range, "0,1000,0.1,or_greater")] public float Weight = 1f;
    /// <summary>How many of the scene come out, a random count in [CountMin, CountMax].</summary>
    [Export(PropertyHint.Range, "0,20,1")] public int CountMin = 1;
    [Export(PropertyHint.Range, "0,20,1")] public int CountMax = 1;
    /// <summary>The sideways scatter speed when several come out, units a tick (0 = all straight up; 6 = a gentle fan ~1.5 m wide). Coins only read it
    /// through SmsThrow; bodies get it in metres a second x the table's BodyPopScale.</summary>
    [Export(PropertyHint.Range, "0,30,0.5")] public float ScatterUnits = 4f;
    /// <summary>A coin's life once touchable, 120 Hz ticks: 0 = the kind's own default (a yellow coin 480 = 4 s, as the game's dropped coins; a blue one
    /// never), -1 = never, any other = that many ticks (the last 360 blinking).</summary>
    [Export] public int LifeTicks = 0;
    /// <summary>For the designer: what this line is.</summary>
    [Export] public string Note = "";

    public int RollCount(System.Random rng)
    {
        int lo = Mathf.Min(CountMin, CountMax), hi = Mathf.Max(CountMin, CountMax);
        return lo >= hi ? lo : lo + rng.Next(hi - lo + 1);
    }
}
