using Godot;

/// <summary>
/// The breakable kibako crate from TBreakHideObj and TWoodBox (MapObjHide.cpp). A real Mario
/// ground-pound landing swaps to the game's 29-frame broken model animation, disables collision,
/// and removes the crate. Nearby touching crates are broken too, matching TWoodBox::killNearWoodBox.
/// </summary>
public partial class SmsWoodBox : StaticBody3D
{
    /// <summary>The game's kibako_crash animation name in the broken-model GLB.</summary>
    [Export] public string BreakAnimation = "kibako_crash";
    /// <summary>The 29 animation frames played at the game's 60 animation frames per second.</summary>
    [Export] public float BreakDurationSeconds = 29f / 60f;
    /// <summary>Horizontal distance used to find a touching crate for TWoodBox's four nearby probes.</summary>
    [Export] public float ChainRadiusMeters = 2.8f;
    /// <summary>Print the ground-pound break during a focused Godot verification run.</summary>
    [Export] public bool DebugPrint;

    private Node3D _wholeModel;
    private Node3D _brokenModel;
    private AnimationPlayer _animation;
    private bool _broken;
    private float _remaining;

    public override void _Ready()
    {
        _wholeModel = GetNodeOrNull<Node3D>("Model");
        _brokenModel = GetNodeOrNull<Node3D>("BrokenModel");
        var hitZone = GetNodeOrNull<Area3D>("HitZone");
        if (_wholeModel == null || _brokenModel == null || hitZone == null)
        {
            GD.PushError($"[SmsWoodBox] '{Name}' needs Model, BrokenModel, and HitZone children.");
            return;
        }
        foreach (Node node in _brokenModel.FindChildren("*", "AnimationPlayer", true, false))
            if (node is AnimationPlayer player) { _animation = player; break; }
        _brokenModel.Visible = false;
        hitZone.BodyEntered += OnHitZoneEntered;
        AddToGroup("sms_wood_box");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_broken) return;
        _remaining -= (float)delta;
        if (_remaining <= 0f) QueueFree();
    }

    private void OnHitZoneEntered(Node3D body)
    {
        if (_broken || body is not Mario mario) return;
        bool pounding = mario.stateOfMario == Mario.MarioState.groundPoundFalling
            || mario.stateOfMario == Mario.MarioState.groundPoundLanding;
        if (pounding) Break(mario.GlobalPosition, true);
    }

    /// <summary>TBreakHideObj::receiveMessage(HIT_MESSAGE_HIP_DROP) and TWoodBox::kill.</summary>
    public void Break(Vector3 marioPosition, bool propagate)
    {
        if (_broken) return;
        _broken = true;
        _remaining = Mathf.Max(0.01f, BreakDurationSeconds);
        _wholeModel.Visible = false;
        _brokenModel.Visible = true;
        foreach (Node node in FindChildren("*", "CollisionShape3D", true, false))
            if (node is CollisionShape3D shape) shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        if (_animation != null && _animation.HasAnimation(BreakAnimation)) _animation.Play(BreakAnimation);
        if (DebugPrint) GD.Print($"[SmsWoodBox] {Name}: ground-pound break");
        if (!propagate) return;
        foreach (Node node in GetTree().GetNodesInGroup("sms_wood_box"))
        {
            if (node is not SmsWoodBox other || other == this || other._broken) continue;
            Vector2 a = new(GlobalPosition.X, GlobalPosition.Z);
            Vector2 b = new(other.GlobalPosition.X, other.GlobalPosition.Z);
            if (a.DistanceTo(b) <= ChainRadiusMeters) other.Break(marioPosition, false);
        }
    }
}
