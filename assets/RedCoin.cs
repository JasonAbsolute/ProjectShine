using Godot;

public partial class RedCoin : Node3D
{
    [Export]
    public float SpinSpeed = 2.0f;

    private BouncePhysics.State _bounce;

    /// <summary>Called by spawners (e.g. Nail) to give the coin a pop-and-bounce on spawn.</summary>
    public void LaunchAsDrop()
    {
        BouncePhysics.Launch(ref _bounce, new Vector3(0f, BouncePhysics.INITIAL_POP_Y, 0f));
    }

    public override void _Process(double delta)
    {
        RotateY(SpinSpeed * (float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (BouncePhysics.Tick(ref _bounce, this, (float)delta))
            QueueFree();
    }

    private void OnPickupZoneBodyEntered(Node3D body)
    {
        if (body is Mario mario)
        {
            mario.HealOneHP();
            mario.HealOneHP();
            int count = mario.AddRedCoin();

            var popupScene = GD.Load<PackedScene>("res://assets/RedCoinPopup.tscn");
            if (popupScene != null)
            {
                var popup = popupScene.Instantiate<RedCoinPopup>();
                mario.AddChild(popup);
                popup.Position = Vector3.Up * 2.8f;
                popup.Setup(count);
            }

            QueueFree();
        }
    }
}
