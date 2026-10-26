using Godot;

/// <summary>
/// The enemy GENERATOR -- TGenerator (Enemy/generator.cpp; pass 126): an invisible point in the stage that every <see cref="IntervalTicks"/> ticks takes
/// a far-out enemy of its manager (<see cref="SmsEnemyPool.FarOut"/>: a dead one, else one the game would have clipped out) and resetSRTV()s it
/// at its own position -- rotation (0, 0, 0), the body's own scale, the velocity (0, 4, 0) units a tick turned by the generator's rotation (a
/// generator pointed sideways spits sideways) -- on its graph. The timer starts at a random fraction of the interval (mTimer = interval x MsRandF)
/// and counts down; it fires at 0 and reloads. The game's placement gives the manager (which kind) and the interval; here <see cref="EnemyScene"/>
/// (any SmsSmallEnemy kind) with <see cref="PoolSize"/> bodies (the manager's mSLActiveEnemyNum) is the manager. Point it with the node's rotation.
/// IN THE EDITOR ([Tool], no custom-resource exports): one ghost of the kind stands at the point so the generator can be placed by eye; it is
/// unowned (never saved) and rebuilt when EnemyScene changes.
/// </summary>
[Tool]
[GlobalClass]
public partial class SmsEnemySpawner : Node3D
{
    /// <summary>The kind this generator launches (a scene whose root is an SmsSmallEnemy: Hamukuri.tscn, Gooble.tscn, ...).</summary>
    [Export] public PackedScene EnemyScene;
    /// <summary>The manager's pool (mSLActiveEnemyNum): how many of the kind can be out at once; a dead one is launched again.</summary>
    [Export] public int PoolSize = 5;
    /// <summary>mInterval: ticks between launches (the placement's own number; 1 = every tick).</summary>
    [Export] public int IntervalTicks = 300;
    /// <summary>The walk graph the launched bodies take (getTracer()->setGraph); none = they wander free.</summary>
    [Export] public Node3D Graph;
    /// <summary>The launch velocity in units a tick before the generator's rotation (the game's (0, 4, 0)).</summary>
    [Export] public Vector3 LaunchVelocityUnits = new Vector3(0f, 4f, 0f);
    /// <summary>A live body farther than this (metres) from Mario counts as clipped out and may be taken back and launched again.</summary>
    [Export] public float RecycleDistance = 60f;
    /// <summary>Off: the timer stands still (a generator a level script turns on).</summary>
    [Export] public bool Enabled = true;
    [Export] public bool DebugPrint;

    private readonly SmsEnemyPool _pool = new();
    private int _timer;
    private float _accum;
    private bool _filled;
    private Node3D _ghost; private string _ghostScene = "\0";
    public SmsEnemyPool Pool => _pool;
    public int Timer => _timer;
    public int Launched { get; private set; }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { CallDeferred(MethodName.EditorRefresh); return; }
        _timer = (int)(IntervalTicks * GD.Randf());
        CallDeferred(MethodName.FillPool);                      // the parent is still setting up its children in _Ready
    }
    private void FillPool()
    {
        if (_filled) return;
        _filled = true;
        int made = _pool.Fill(GetParent(), EnemyScene, PoolSize, GlobalPosition, DebugPrint, Name);
        if (DebugPrint) GD.Print($"[SMS spawner] {Name}: {made} of {(EnemyScene != null ? EnemyScene.ResourcePath.GetFile() : "nothing")} pooled, the first launch in {_timer} ticks");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !_filled) return;
        int n = SmsUnits.TicksFor(delta, ref _accum);
        for (int i = 0; i < n; i++) Tick();
    }
    /// <summary>TGenerator::perform(CUE_MOVE).</summary>
    private void Tick()
    {
        if (!Enabled) return;
        _timer -= 1;
        if (_timer < 0) _timer = IntervalTicks;
        if (_timer == 0) Launch();
    }
    /// <summary>The far-out body of the pool at this point with the turned launch velocity; nothing when every body is live and near.</summary>
    public SmsSmallEnemy Launch()
    {
        var mario = GetTree().GetFirstNodeInGroup("player") as Node3D;
        var e = _pool.FarOut(mario != null ? mario.GlobalPosition : GlobalPosition, RecycleDistance);
        if (e == null) return null;
        e.SmsLaunch(GlobalPosition, 0f, GlobalTransform.Basis.Orthonormalized() * LaunchVelocityUnits, Graph);
        Launched++;
        if (DebugPrint) GD.Print($"[SMS spawner] {Name}: launched {e.Name} ({_pool.Alive} of {_pool.Count} out)");
        return e;
    }

    // ------------------------------------------------------------------------------------------------------------------ the editor
    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) return;
        string path = EnemyScene != null ? EnemyScene.ResourcePath : "";
        if (path != _ghostScene) EditorRefresh();
    }
    private void EditorRefresh()
    {
        if (!IsInsideTree()) return;
        if (_ghost != null && IsInstanceValid(_ghost)) { _ghost.QueueFree(); _ghost = null; }
        _ghostScene = EnemyScene != null ? EnemyScene.ResourcePath : "";
        if (EnemyScene == null) return;
        if (EnemyScene.Instantiate() is Node3D g) { g.Name = "EditorGhost"; _ghost = g; AddChild(g); }
    }
}
