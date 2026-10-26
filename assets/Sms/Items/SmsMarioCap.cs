using Godot;

/// <summary>
/// Mario's cap on the ground -- TMarioCap (ACTOR_TYPE_MARIO_CAP, the item a cap-thief puts down when it dies: THamuKuri::releaseCap) (pass 125g).
/// The model is the game's mapobj/mariocap.bmd (his rip), spinning like every item (TItemManager's 2 deg a tick); Mario within <see cref="PickupRadius"/>
/// takes it back (<see cref="Mario.SmsCapStolen"/>(false)) and it is gone. If his cap is already on his head when it lands (a second thief, a reset) it
/// just goes. NOT PORTED: the capless Mario's sun damage, MSD_SE_MA_GET_CAP.
/// </summary>
[GlobalClass]
public partial class SmsMarioCap : Node3D
{
    [Export] public float PickupRadius = 1.0f;
    [Export] public float SpinDegPerTick = 2f;
    [Export] public AudioStreamPlayer3D GetSound;
    private float _accum;
    private Mario _mario;
    public bool Taken { get; private set; }

    public override void _PhysicsProcess(double delta)
    {
        int n = SmsUnits.TicksFor(delta, ref _accum);
        RotateY(Mathf.DegToRad(SpinDegPerTick * n));
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        if (_mario == null || Taken) return;
        if (!_mario.SmsCapStolenNow) { Taken = true; QueueFree(); return; }
        Vector3 d = _mario.GlobalPosition - GlobalPosition;
        float dy = d.Y; d.Y = 0f;
        if (d.Length() < PickupRadius && dy > -0.5f && dy < 1.5f)
        {
            Taken = true;
            _mario.SmsCapStolen(false);
            if (GetSound != null && GetSound.Stream != null) GetSound.Play();
            QueueFree();
        }
    }
}
