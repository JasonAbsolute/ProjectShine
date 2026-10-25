using Godot;

public partial class BlueCoin : Node3D
{
    [Export] public string CoinId = "";
    [Export] public float SpinSpeed = 2.0f;

    // ---- SMS pass 123 (TCoinBlue, MoveBG/Item.cpp) ----
    /// <summary>On: a thrown / dropped coin flies and lives by the game's item rules (SmsCoinItem: the coin physics, 120 untouchable ticks, the
    /// timed life with its blink for a coin that disappears). Off: his BouncePhysics pop as before.</summary>
    [Export] public bool SmsItemLife = true;
    /// <summary>On: the game's spin -- TItemManager's one matrix, 2 deg a tick (240 deg/s), every coin in the stage in phase. Off: SpinSpeed.</summary>
    [Export] public bool SmsSpin = true;
    /// <summary>TMario::getCoinBlue: incHP(2) (MarioReceiveMsg.cpp 88) -- a blue coin heals two, like a red one.</summary>
    [Export] public bool SmsHealTwo = true;
    /// <summary>MSD_SE_SY_COIN_BOUND (a bounce) / MSD_SE_SY_COIN_DISAPPEAR (the timed death): slots, empty = silent.</summary>
    [Export] public AudioStreamPlayer3D SmsBoundSound;
    [Export] public AudioStreamPlayer3D SmsDisappearSound;

    private SmsCoinItem.State _sms;
    /// <summary>The item life's state (the drives read it).</summary>
    public SmsCoinItem.State SmsState => _sms;

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(CoinId) && GameData.Instance.IsBlueCoinCollected(CoinId))
            QueueFree();
    }

    private BouncePhysics.State _bounce;

    /// <summary>Called by spawners (e.g. Nail) to give the coin a pop-and-bounce on spawn. SMS: TSmallEnemy's drop (smallEnemy.cpp 353) --
    /// straight up at 20 units a tick; a blue coin keeps MAP_OBJ_FLAG_UNK10000000, so it never times out.</summary>
    public void LaunchAsDrop()
    {
        if (SmsItemLife) { SmsThrow(new Vector3(0f, SmsCoinItem.PopUpSpeedUnits, 0f), -1); return; }
        BouncePhysics.Launch(ref _bounce, new Vector3(0f, BouncePhysics.INITIAL_POP_Y, 0f));
    }

    /// <summary>throwObjToFrontFromPoint / a drop: the coin leaves its current position at <paramref name="velUnitsPerTick"/> (world axes, units
    /// a tick), untouchable for 120 ticks, then alive for <paramref name="disappearAfter"/> ticks (-1 = for good). <paramref name="container"/>
    /// = the SmsHiddenCoin that threw it (told when it dies by its timer).</summary>
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
            {
                // TItem::appeared: MSD_SE_SY_COIN_DISAPPEAR for a red / blue coin, then dead
                if (SmsDisappearSound != null && SmsDisappearSound.Stream != null)
                {
                    SmsDisappearSound.Reparent(GetParent() ?? GetTree().CurrentScene, true);
                    SmsDisappearSound.Play();
                    SmsDisappearSound.Finished += SmsDisappearSound.QueueFree;
                }
                QueueFree();
            }
            return;
        }
        if (BouncePhysics.Tick(ref _bounce, this, (float)delta))
            QueueFree();
    }

    private void OnPickupZoneBodyEntered(Node3D body)
    {
        if (body is Mario mario)
        {
            if (!string.IsNullOrEmpty(CoinId))
                GameData.Instance.CollectBlueCoin(CoinId);       // TMarDirector::fireGetBlueCoin: setBlueCoinFlag (in RAM) ...

            if (SmsHealTwo) { mario.HealOneHP(); mario.HealOneHP(); }
            mario.AddBlueCoin();
            QueueFree();
            // ... + DIRECTOR_FLAG_CARD_SAVE_PENDING with PURPOSE_BLUE_COIN_GET (MarDirectorEvent.cpp 114): the "You got a Blue Coin!" save window
            // over the stopped game, every coin (pass 124). The write is its "Save and Continue".
            if (!string.IsNullOrEmpty(CoinId) && SmsCardSave.Current != null && !SmsCardSave.Current.IsOpen)
                SmsCardSave.Current.Open(SmsCardSave.Purpose.BlueCoinGet, pauseTree: true);
        }
    }
}
