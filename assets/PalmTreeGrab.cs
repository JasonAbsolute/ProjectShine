using Godot;

/// <summary>
/// Place this script on an Area3D child of a PalmTree (e.g. "GrabZone").
/// The Area3D should be a vertical cylinder slightly larger than the trunk capsule.
/// When Mario walks into it (with stick pushed toward the trunk), he grabs the tree.
/// </summary>
public partial class PalmTreeGrab : Area3D
{
    /// <summary>NodePath to the Node3D that marks the trunk's central axis (XZ pivot).</summary>
    [Export] public NodePath TrunkAxisPath { get; set; }

    /// <summary>Y offset (RELATIVE to the PalmTree's instance position) at which Mario releases when sliding down to the base. Negative = below tree origin.</summary>
    [Export] public float TrunkBottomY { get; set; } = -0.5f;

    /// <summary>Y offset (RELATIVE to the PalmTree's instance position) at which the top-reach pop fires (where the leaves sit).</summary>
    [Export] public float TrunkTopY { get; set; } = 19.0f;

    /// <summary>Y offset (RELATIVE to the PalmTree's instance position) where Mario is placed when he reaches the top. Should sit slightly above the trunk top so he's standing on the trunk between the splayed leaves.</summary>
    [Export] public float LeafLandY { get; set; } = 19.5f;

    /// <summary>Distance from trunk axis at which Mario stands when at the BASE of the trunk (wide).</summary>
    [Export] public float TrunkRadiusBottom { get; set; } = 1.4f;

    /// <summary>Distance from trunk axis at which Mario stands when at the TOP of the trunk (narrow).</summary>
    [Export] public float TrunkRadiusTop { get; set; } = 0.6f;

    /// <summary>Require stick to be pushed toward trunk (dot > 0). Stops accidental grabs while running past.</summary>
    [Export] public bool RequireInputTowardTrunk { get; set; } = true;

    /// <summary>If false, Mario won't grab this tree — useful for decorative trees (e.g. banana trees) that should still bounce leaves but not be climbable.</summary>
    [Export] public bool Climbable { get; set; } = true;

    private Node3D _trunkAxis;

    public override void _Ready()
    {
        if (TrunkAxisPath != null && !TrunkAxisPath.IsEmpty)
            _trunkAxis = GetNodeOrNull<Node3D>(TrunkAxisPath);
        if (_trunkAxis == null)
            _trunkAxis = GetParent<Node3D>();

        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!Climbable) return;        // tree explicitly opted out of being climbed
        if (body is not Mario mario) return;
        if (_trunkAxis == null)
        {
            GD.PrintErr("[PalmTreeGrab] -> trunk axis is null");
            return;
        }

        Vector3 trunkXZ = _trunkAxis.GlobalPosition;
        trunkXZ.Y = 0f;

        if (RequireInputTowardTrunk)
        {
            Vector3 toTrunk = new Vector3(trunkXZ.X - mario.GlobalPosition.X, 0f,
                                          trunkXZ.Z - mario.GlobalPosition.Z);
            if (toTrunk.LengthSquared() < 0.0001f) return;
            toTrunk = toTrunk.Normalized();

            // Accept the grab if EITHER: stick is being pressed (any direction), OR
            // Mario has horizontal velocity moving him toward the trunk (jumping/sliding in).
            // Mario.cs converts the stick to a world dir via camera yaw, so we can't easily
            // do a stick-vs-trunk check here without the camera node. Velocity is fine.
            Vector2 stick = Input.GetVector("Lstick_left", "Lstick_right", "Lstick_up", "Lstick_down");
            bool stickActive = stick.LengthSquared() >= 0.04f;

            Vector3 horizVel = new Vector3(mario.Velocity.X, 0f, mario.Velocity.Z);
            bool movingTowardTrunk =
                horizVel.LengthSquared() > 0.5f && horizVel.Dot(toTrunk) > 0.3f;

            if (!stickActive && !movingTowardTrunk)
            {
                GD.Print("[PalmTreeGrab] -> no input or velocity toward trunk, ignoring");
                return;
            }
        }

        Node3D treeRoot = GetParent<Node3D>();
        float treeBaseY = treeRoot.GlobalPosition.Y;

        // Tree may be scaled non-uniformly in principle; in practice palm trees scale uniformly.
        // We use Y for vertical offsets and X for radial. Both fall out of the global basis scale.
        Vector3 treeScale = treeRoot.GlobalTransform.Basis.Scale;
        float scaleY = treeScale.Y;
        float scaleXZ = (Mathf.Abs(treeScale.X) + Mathf.Abs(treeScale.Z)) * 0.5f;

        float worldBottomY = treeBaseY + TrunkBottomY * scaleY;
        float worldTopY = treeBaseY + TrunkTopY * scaleY;
        float worldLeafLandY = treeBaseY + LeafLandY * scaleY;
        float scaledRadiusBottom = TrunkRadiusBottom * scaleXZ;
        float scaledRadiusTop = TrunkRadiusTop * scaleXZ;

        bool grabbed = mario.TryGrabTree(treeRoot, trunkXZ, worldBottomY, worldTopY, worldLeafLandY, scaledRadiusBottom, scaledRadiusTop);
        GD.Print($"[PalmTreeGrab] grab={grabbed} scaleY={scaleY:F2} scaleXZ={scaleXZ:F2} tree@{treeBaseY:F2} bottom={worldBottomY:F2} top={worldTopY:F2} leafLand={worldLeafLandY:F2} radB={scaledRadiusBottom:F2} radT={scaledRadiusTop:F2}");
    }
}
