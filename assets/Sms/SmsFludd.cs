using Godot;

/// <summary>
/// The FLUDD as a scene (assets/Sms/Fludd.tscn): the tank (watergun2/body/wg_mdl1) with the game's MAT3 materials on its mesh --
/// the translucent vertex-coloured shell, the waterline hidden (Mario.cs draws the level) -- the nozzle seat, a BoneAttachment3D
/// riding the tank skeleton's nozzle_center joint, and the four nozzles under it wearing the tank frame's atlas (their rips carry
/// an 8x8 dummy texture). Instance it in Player.tscn under a BoneAttachment3D on jnt_chest named SmsFluddAttach
/// (Armature/Skeleton3D/SmsFluddAttach/Fludd): Mario.cs (SmsFluddSetup) reads these exports and drives the clips, the water, the
/// tilt. Every part is a node -- swap a model or a material in the scene and the game wears it.
/// </summary>
public partial class SmsFludd : Node3D
{
    [Export] public Node3D Tank;
    /// <summary>The BoneAttachment3D on the tank's nozzle_center joint; the nozzles hang from it.</summary>
    [Export] public Node3D NozzleSeat;
    [Export] public Node3D SprayNozzle;
    [Export] public Node3D HoverNozzle;
    [Export] public Node3D RocketNozzle;
    [Export] public Node3D TurboNozzle;
    /// <summary>The tank frame's atlas (H_watergun_main): what the nozzles' dummy texture stands in for.</summary>
    [Export] public Material AtlasMaterial;

    /// <summary>The nozzle node by the port's SmsNozzle index (0 spray, 1 hover, 2 rocket, 3 turbo).</summary>
    public Node3D Nozzle(int i) => i switch { 1 => HoverNozzle, 2 => RocketNozzle, 3 => TurboNozzle, _ => SprayNozzle };
}
