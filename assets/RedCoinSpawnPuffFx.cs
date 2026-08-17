using Godot;

/// <summary>
/// Controller for the editor-authored RedCoinSpawnPuff.tscn particle burst.
/// All visual tuning lives on the two CPUParticles3D children; this script only
/// positions, restarts, and removes the one-shot effect.
/// </summary>
public partial class RedCoinSpawnPuffFx : Node3D
{
    [Export] public NodePath SmokeParticlesPath = new("SmokeParticles");
    [Export] public NodePath GreenDotParticlesPath = new("GreenDotParticles");
    [Export] public float CleanupPadding = 0.12f;

    private CpuParticles3D _smokeParticles;
    private CpuParticles3D _greenDotParticles;
    private float _cleanupTimer = -1f;

    public override void _Ready()
    {
        TopLevel = true;
        ResolveEmitters();
        SetProcess(false);
    }

    public void Burst(Vector3 worldCenter)
    {
        ResolveEmitters();
        GlobalPosition = worldCenter;

        float longestLifetime = 0f;
        if (_smokeParticles != null)
        {
            _smokeParticles.Restart();
            _smokeParticles.Emitting = true;
            longestLifetime = Mathf.Max(longestLifetime, (float)_smokeParticles.Lifetime);
        }
        if (_greenDotParticles != null)
        {
            _greenDotParticles.Restart();
            _greenDotParticles.Emitting = true;
            longestLifetime = Mathf.Max(longestLifetime, (float)_greenDotParticles.Lifetime);
        }

        if (_smokeParticles == null && _greenDotParticles == null)
        {
            GD.PushError($"[RedCoinSpawnPuffFx] '{Name}' has no particle emitters to play.");
            QueueFree();
            return;
        }

        _cleanupTimer = longestLifetime + Mathf.Max(CleanupPadding, 0f);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_cleanupTimer < 0f)
            return;

        _cleanupTimer -= (float)delta;
        if (_cleanupTimer > 0f)
            return;

        SetProcess(false);
        QueueFree();
    }

    private void ResolveEmitters()
    {
        _smokeParticles ??= GetNodeOrNull<CpuParticles3D>(SmokeParticlesPath);
        _greenDotParticles ??= GetNodeOrNull<CpuParticles3D>(GreenDotParticlesPath);
    }
}
