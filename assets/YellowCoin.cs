using Godot;

public partial class YellowCoin : Node3D
{
    [Export]
    public float SpinSpeed = 2.0f;

    // ---- SMS pass 123 (TCoin, MoveBG/Item.cpp) ----
    /// <summary>On: a thrown / dropped coin flies and lives by the game's item rules (SmsCoinItem). A yellow coin that appears (an enemy's,
    /// a nail's, a sprayed spot's) is the game's POOL coin: TCoin::appearWithoutSound clears MAP_OBJ_FLAG_UNK10000000, so it times out --
    /// 480 ticks (TItem::initMapObj) for a drop, the spot's own value for a hidden one -- blinking its last 3 s. Off: his BouncePhysics pop.</summary>
    [Export] public bool SmsItemLife = true;
    /// <summary>On: the game's spin -- TItemManager's one matrix, 2 deg a tick (240 deg/s), every coin in phase. Off: SpinSpeed.</summary>
    [Export] public bool SmsSpin = true;
    /// <summary>MSD_SE_SY_COIN_BOUND (a bounce): a slot, empty = silent. (A yellow coin's timed death is silent in the game: TItem::appeared plays
    /// MSD_SE_SY_COIN_DISAPPEAR for red / blue only.)</summary>
    [Export] public AudioStreamPlayer3D SmsBoundSound;

    private SmsCoinItem.State _sms;
    public SmsCoinItem.State SmsState => _sms;

    private BouncePhysics.State _bounce;

    /// <summary>Called by spawners (e.g. Nail) to give the coin a pop-and-bounce on spawn. SMS: TSmallEnemy's drop (smallEnemy.cpp 353) --
    /// straight up at 20 units a tick, alive 480 ticks once touchable.</summary>
    public void LaunchAsDrop()
    {
        if (SmsItemLife) { SmsThrow(new Vector3(0f, SmsCoinItem.PopUpSpeedUnits, 0f), SmsCoinItem.DisappearTicksDefault); return; }
        BouncePhysics.Launch(ref _bounce, new Vector3(0f, BouncePhysics.INITIAL_POP_Y, 0f));
    }

    /// <summary>throwObjToFrontFromPoint / a drop (see BlueCoin.SmsThrow).</summary>
    public void SmsThrow(Vector3 velUnitsPerTick, int disappearAfter, Node container = null)
        => SmsCoinItem.Throw(ref _sms, this, velUnitsPerTick, disappearAfter, container);

    public override void _Process(double delta)
    {
        if (SmsSpin) SmsCoinItem.Spin(this);
        else RotateY(SpinSpeed * (float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_sms.Active)
        {
            if (SmsCoinItem.Tick(ref _sms, this, delta, SmsBoundSound))
                QueueFree();
            return;
        }
        if (BouncePhysics.Tick(ref _bounce, this, (float)delta))
            QueueFree();
    }

    private void OnPickupZoneBodyEntered(Node3D body)
    {
        if (body is Mario mario)
        {
            mario.HealOneHP();
            mario.AddCoin();
            QueueFree();
        }
    }
}
