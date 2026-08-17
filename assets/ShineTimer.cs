using Godot;

/// <summary>
/// Generic countdown/count-up timer for the SMS-style "TIME" challenges some
/// shines use (secret courses, races, etc.). Deliberately knows nothing about
/// what should happen when it starts or reaches its target — level-specific
/// code hooks the signals below and does whatever that level's task calls for
/// (fail the level, spawn something, unlock a shine, restart a phase...).
/// Different levels need very different behavior here, so this node is just
/// the clock: value, direction, and the moments other code cares about.
///
/// Not tied to any HUD — pair with TimerHud.cs (or nothing, for an invisible
/// timer) via its own NodePath export.
/// </summary>
public partial class ShineTimer : Node
{
    /// <summary>True = counts down from StartValue to TargetValue (typical
    /// "time's up" challenge). False = counts up from StartValue toward
    /// TargetValue — or, with Unbounded on, just counts up freely (a
    /// speedrun-style stopwatch).</summary>
    [Export] public bool CountDown = true;

    [Export] public float StartValue = 60f;
    [Export] public float TargetValue = 0f;

    /// <summary>When true, TargetValue is ignored — the timer never auto-stops
    /// on its own, it just keeps ticking until something external calls
    /// StopTimer(). That's the speedrun case: CountDown=false, Unbounded=true,
    /// StartValue=0, and whatever CurrentValue is when StopTimer() gets called
    /// (e.g. a finish-line trigger) is the run's final time. Without this, a
    /// count-up timer would need a manually-guessed "big enough" TargetValue —
    /// and the default TargetValue=0 would make it stop on its very first
    /// tick, which is exactly backwards for a stopwatch.</summary>
    [Export] public bool Unbounded = false;

    /// <summary>Start ticking with no external trigger needed — but not before
    /// the player can actually act. If LevelPath resolves to a Level, that
    /// means waiting for its MarioReady signal (so this never starts during a
    /// non-interactive intro pan); with no Level wired, it starts as soon as
    /// this node enters the tree.</summary>
    [Export] public bool AutoStart = false;

    /// <summary>Optional. When set (and AutoStart is on), waits for this
    /// Level's MarioReady signal instead of starting immediately — see
    /// AutoStart.</summary>
    [Export] public NodePath LevelPath;

    /// <summary>Optional. When set, grabbing any shine (Mario.ShineCollected)
    /// pauses this timer — StopTimer(), so CurrentValue freezes right where it
    /// is and stays on screen (nothing hides a paused-not-reached-target timer)
    /// instead of continuing to run through the collect cutscene.</summary>
    [Export] public NodePath MarioPath;

    [Signal] public delegate void TimerStartedEventHandler();
    [Signal] public delegate void TimerStoppedEventHandler();
    [Signal] public delegate void TimerTickEventHandler(float currentValue);
    [Signal] public delegate void TimerReachedTargetEventHandler();

    public float CurrentValue { get; private set; }
    public bool IsRunning { get; private set; }

    private bool _reachedTarget;

    public override void _Ready()
    {
        CurrentValue = StartValue;

        var mario = GetNodeOrNull<Mario>(MarioPath);
        if (mario != null)
            mario.ShineCollected += StopTimer;

        if (!AutoStart)
            return;

        var level = GetNodeOrNull<Level>(LevelPath);
        if (level != null)
        {
            // One-shot: MarioReady only ever fires once per level, same as
            // this AutoStart only ever wants to fire once.
            level.MarioReady += StartTimer;
        }
        else
        {
            // No level to wait on — deferred so any listener (e.g. TimerHud)
            // wired up as a LATER sibling has already run its own _Ready() and
            // subscribed to TimerStarted before this fires; otherwise the
            // one-time signal can go out before anyone's listening, and the
            // HUD never shows itself even though the timer is genuinely
            // ticking underneath.
            CallDeferred(nameof(StartTimer));
        }
    }

    /// <summary>(Re)starts ticking from StartValue. Safe to call again after
    /// StopTimer/reaching target — e.g. a level restarting a timed phase.</summary>
    public void StartTimer()
    {
        CurrentValue = StartValue;
        _reachedTarget = false;
        IsRunning = true;
        EmitSignal(SignalName.TimerStarted);
        EmitSignal(SignalName.TimerTick, CurrentValue);
    }

    /// <summary>Freezes the current value — ticking stops but CurrentValue is
    /// left as-is (unlike StartTimer, this doesn't reset it).</summary>
    public void StopTimer()
    {
        if (!IsRunning)
            return;
        IsRunning = false;
        EmitSignal(SignalName.TimerStopped);
    }

    public void ResumeTimer()
    {
        if (_reachedTarget)
            return;
        IsRunning = true;
    }

    /// <summary>Snaps back to StartValue without emitting TimerStarted — use
    /// StartTimer() instead if the intent is "begin a fresh run."</summary>
    public void ResetTimer()
    {
        CurrentValue = StartValue;
        _reachedTarget = false;
        EmitSignal(SignalName.TimerTick, CurrentValue);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsRunning || _reachedTarget)
            return;

        float dt = (float)delta * (CountDown ? -1f : 1f);
        CurrentValue += dt;

        if (!Unbounded)
        {
            bool hitTarget = CountDown ? CurrentValue <= TargetValue : CurrentValue >= TargetValue;
            if (hitTarget)
            {
                CurrentValue = TargetValue;
                _reachedTarget = true;
                IsRunning = false;
            }
        }

        EmitSignal(SignalName.TimerTick, CurrentValue);

        if (_reachedTarget)
            EmitSignal(SignalName.TimerReachedTarget);
    }
}
