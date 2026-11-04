using Godot;

/// <summary>
/// SMS pass 157: the way OUT of the roulette fight into the King Boo REMATCH (KINGBOO_REMATCH_gdoc.txt, INTRO 1-3). King Boo's Rematch knob
/// sends <see cref="Begin"/> here where the game would make its Shine: the casino table cracks and shatters (the FloorShatter child,
/// SmsFloorShatter), Mario falls through the table into the load trigger -- a box TriggerDepth under this node (TriggerWidth across,
/// TriggerHeight tall) -- which fades the screen to black (FadeOutSeconds) and loads ArenaScene, the rematch arena. GameData.RematchFrom
/// keeps this stage's scene path for the arena (a way back later). Off (Enabled false) nothing happens and the boss makes his Shine.
/// Knobs: the shatter's are on the FloorShatter child; here the fade, the trigger's box, the arena scene, FallSeconds (a fail-safe: the
/// load runs this long after the break even if the trigger never sees him).
/// </summary>
public partial class SmsRematchExit : Node3D
{
    [Export] public bool Enabled = true;
    /// <summary>The rematch arena (a scene path; empty = res://assets/Sms/Rematch/RematchArena.tscn).</summary>
    [Export(PropertyHint.File, "*.tscn")] public string ArenaScene = "";
    /// <summary>The fade to black once he is through the floor.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float FadeOutSeconds = 0.7f;
    /// <summary>The load trigger: its top this many metres under this node (the disc faces are 1.1-1.9 m under it; the room's painted wall runs 13 m under),
    /// this wide (x and z) and this tall (down). 8: he falls about a second past the table with the shards before the fade starts.</summary>
    [Export] public float TriggerDepth = 8f, TriggerWidth = 70f, TriggerHeight = 30f;
    /// <summary>Fail-safe: the fade starts this long after the break if the trigger never saw him (0 = never).</summary>
    [Export] public float FallSeconds = 6f;
    /// <summary>The FloorShatter child (found by type under this node when unset).</summary>
    [Export] public SmsFloorShatter FloorShatter;
    [Export] public bool DebugPrint;

    [Signal] public delegate void TriggeredEventHandler();
    [Signal] public delegate void LoadingEventHandler(string scene);

    public const string DefaultArena = "res://assets/Sms/Rematch/RematchArena.tscn";
    public bool Begun { get; private set; }
    public bool IsTriggered { get; private set; }
    public bool IsLoading { get; private set; }
    private float _sinceBreak = -1f;
    private Mario _mario;

    public override void _Ready()
    {
        AddToGroup("sms_rematch_exit");
        if (FloorShatter == null) foreach (Node c in GetChildren()) if (c is SmsFloorShatter f) { FloorShatter = f; break; }
        if (FloorShatter != null) FloorShatter.Shattered += () => _sinceBreak = 0f;
        SetPhysicsProcess(false);
        SmsRematchExitDriveHook();
    }
    partial void SmsRematchExitDriveHook();

    /// <summary>The boss is down (where the Shine would appear): the table cracks. False when off or already begun.</summary>
    public bool Begin()
    {
        if (!Enabled || Begun || Engine.IsEditorHint()) return false;
        Begun = true;
        SetPhysicsProcess(true);
        if (FloorShatter != null) FloorShatter.Crack();
        else { GD.PushWarning($"[SMS rematch] {GetPath()}: no FloorShatter child -- the load trigger waits with the table whole"); _sinceBreak = 0f; }
        if (DebugPrint) GD.Print($"[SMS rematch] {Name}: the way out opens (fade {FadeOutSeconds} s, trigger {TriggerDepth} m under)");
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsLoading) return;
        if (_sinceBreak >= 0f) _sinceBreak += (float)delta;
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        if (!IsTriggered)
        {
            bool inBox = false;
            if (_mario != null)
            {
                Vector3 p = ToLocal(_mario.GlobalPosition);
                inBox = p.Y < -TriggerDepth && p.Y > -TriggerDepth - TriggerHeight && Mathf.Abs(p.X) < TriggerWidth * 0.5f && Mathf.Abs(p.Z) < TriggerWidth * 0.5f;
            }
            bool failSafe = FallSeconds > 0f && _sinceBreak >= FallSeconds;
            if (!inBox && !failSafe) return;
            IsTriggered = true;
            if (DebugPrint) GD.Print($"[SMS rematch] {Name}: {(inBox ? "Mario is through the floor" : "fail-safe: no trigger seen")} -> fade out");
            if (SmsFader.Current != null) SmsFader.Current.StartWipe(SmsFader.Wipe.FadeOut, 0f, FadeOutSeconds);
            EmitSignal(SignalName.Triggered);
            _fadeLeft = FadeOutSeconds + 0.1f;
            return;
        }
        _fadeLeft -= (float)delta;
        if ((SmsFader.Current != null && SmsFader.Current.IsFullyFadedOut) || _fadeLeft <= 0f) Load();
    }
    private float _fadeLeft;

    private void Load()
    {
        IsLoading = true;
        string dest = string.IsNullOrEmpty(ArenaScene) ? DefaultArena : ArenaScene;
        if (GameData.Instance != null)
        {
            GameData.Instance.RematchFrom = GetTree().CurrentScene?.SceneFilePath;
            GameData.Instance.FadeInFrom = null;
        }
        if (DebugPrint) GD.Print($"[SMS rematch] {Name}: moveStage -> {dest}");
        EmitSignal(SignalName.Loading, dest);
        if (!ResourceLoader.Exists(dest)) { GD.PushError($"[SMS rematch] {GetPath()}: the arena scene {dest} does not exist"); return; }
        GetTree().ChangeSceneToFile(dest);
    }
}
