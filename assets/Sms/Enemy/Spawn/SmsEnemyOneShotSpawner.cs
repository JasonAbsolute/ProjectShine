using Godot;

/// <summary>
/// The ONE-SHOT generator -- TOneShotGenerator (Enemy/generator.cpp 95-142; pass 126): an invisible hit actor (a cylinder of radius 80 u, height
/// 120 u on the player hit category) that answers FLUDD's water ONCE: the first drop (any water actor's message) takes a far-out enemy of its
/// manager and resetSRTV()s it at the generator's point with the (0, 4, 0) launch velocity turned by its rotation, on its graph; mCount drops to 0
/// and later drops are taken (the message returns TRUE: the drop dies) but do nothing. Here a StaticBody3D on <see cref="SmsHiddenCoin.HideLayer"/>
/// (the drops' ray reaches it, nothing else collides) with the cylinder as its shape, an <see cref="ISmsSprayTarget"/>; the pool is
/// <see cref="Count"/> bodies of <see cref="EnemyScene"/> (one, as the game's; more = that many sprays). OneShotSpawner.tscn carries the shape.
/// </summary>
[GlobalClass]
public partial class SmsEnemyOneShotSpawner : StaticBody3D, ISmsSprayTarget
{
    /// <summary>The kind this spot releases (a scene whose root is an SmsSmallEnemy).</summary>
    [Export] public PackedScene EnemyScene;
    /// <summary>mCount: how many sprays release a body (the game's 1); the pool is this many bodies.</summary>
    [Export] public int Count = 1;
    /// <summary>The walk graph the released body takes; none = it wanders free.</summary>
    [Export] public Node3D Graph;
    /// <summary>The launch velocity in units a tick before the node's rotation (the game's (0, 4, 0)).</summary>
    [Export] public Vector3 LaunchVelocityUnits = new Vector3(0f, 4f, 0f);
    /// <summary>A live body farther than this (metres) from Mario counts as clipped out and may be taken back.</summary>
    [Export] public float RecycleDistance = 60f;
    /// <summary>On: the body goes on the spray-only layer (the hidden coins' HideLayer) with no mask at _Ready, whatever the scene says.</summary>
    [Export] public bool AutoLayer = true;
    [Export] public bool DebugPrint;
    public const float HitRadiusUnits = 80f, HitHeightUnits = 120f;

    private readonly SmsEnemyPool _pool = new();
    private int _left;
    private bool _filled;
    public SmsEnemyPool Pool => _pool;
    public int Left => _left;
    public int Sprays { get; private set; }

    public override void _Ready()
    {
        _left = Count;
        if (AutoLayer) { CollisionLayer = SmsHiddenCoin.HideLayer; CollisionMask = 0; }
        bool shaped = false;
        foreach (Node c in GetChildren()) if (c is CollisionShape3D) shaped = true;
        if (!shaped)
        {   // no shape in the scene: the game's cylinder
            float u = SmsUnits.UnitsPerMetre;
            AddChild(new CollisionShape3D { Name = "Hit", Shape = new CylinderShape3D { Radius = HitRadiusUnits / u, Height = HitHeightUnits / u }, Position = new Vector3(0f, HitHeightUnits / u * 0.5f, 0f) });
        }
        CallDeferred(MethodName.FillPool);
    }
    private void FillPool()
    {
        if (_filled) return;
        _filled = true;
        _pool.Fill(GetParent(), EnemyScene, Count, GlobalPosition, DebugPrint, Name);
    }

    /// <summary>TOneShotGenerator::receiveMessage from a water actor: the release while mCount is left; TRUE either way (the drop is taken).</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        Sprays++;
        if (_left <= 0 || !_filled) return true;
        var mario = GetTree().GetFirstNodeInGroup("player") as Node3D;
        var e = _pool.FarOut(mario != null ? mario.GlobalPosition : GlobalPosition, RecycleDistance);
        if (e != null)
        {
            e.SmsLaunch(GlobalPosition, 0f, GlobalTransform.Basis.Orthonormalized() * LaunchVelocityUnits, Graph);
            if (DebugPrint) GD.Print($"[SMS spawner] {Name}: sprayed -- released {e.Name}");
        }
        _left--;
        return true;
    }
}
