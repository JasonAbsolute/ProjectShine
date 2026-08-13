using System;
using System.Threading.Tasks;
using Godot;

public partial class ZEffectSpawner : Node3D
{
    [Export]
    private PackedScene zEffectScene; // The individual Z effect scene
    private Node3D zParent;

    public override void _Ready()
    {
        zParent = GetNode<Node3D>("ZParent"); // Container for all Zs
    }

    public async void StartZEffect()
    {
        Vector3 spawnPosition = GlobalTransform.Origin;
        for (int i = 0; i < 4; i++)
        {
            Node3D zEffectInstance = (Node3D)zEffectScene.Instantiate();
            zParent.AddChild(zEffectInstance);

            // Get the Sprite3D inside the instance
            Sprite3D zSprite = zEffectInstance.GetNode<Sprite3D>("Sprite3D");
            if (zSprite == null)
            {
                GD.PrintErr("ERROR: Sprite3D not found in ZEffect instance!");
                return;
            }

            // Adjust position (spawn slightly above given position)
            zEffectInstance.GlobalTransform = new Transform3D(
                zEffectInstance.GlobalTransform.Basis,
                spawnPosition
                    + new Vector3(
                        (float)GD.RandRange(-0.2, 0.2), // Small horizontal variation
                        .5f + (.75f * i) * 0.5f, // Lower starting height
                        (float)GD.RandRange(-0.2, 0.2)
                    )
            );

            // Assign a random color
            Color randomColor = new Color(
                (float)GD.RandRange(0.5, 1.0), // Red (50% - 100%)
                (float)GD.RandRange(0.5, 1.0), // Green (50% - 100%)
                (float)GD.RandRange(0.5, 1.0), // Blue (50% - 100%)
                1.0f // Fully opaque at start
            );
            // Decrease animation duration
            float animationTime = 0.4f;
            zSprite.Modulate = randomColor; // Apply color

            // Create a Tween for animation
            Tween tween = zSprite.CreateTween();

            // Scale animation (gradual increase)
            zSprite.Scale = Vector3.One * (.05f + (.1f * i));

            // Move animation (upward float)
            tween.TweenProperty(zSprite, "position:y", zSprite.Position.Y + .5f, animationTime);

            // Fade-out animation
            tween.TweenProperty(zSprite, "modulate:a", 0, animationTime);

            // Wait before spawning the next Z
            await ToSignal(GetTree().CreateTimer(0.3f), "timeout");
        }
    }

    public void StopZEffect()
    {
        foreach (Node child in zParent.GetChildren())
        {
            child.QueueFree(); // Remove all existing Z effects
        }
    }
}
