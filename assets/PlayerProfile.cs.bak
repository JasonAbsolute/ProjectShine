using Godot;

/// <summary>
/// Stores per-player cosmetic settings.
/// Create a .tres file for each skin variant:
///   Right-click in FileSystem → New Resource → PlayerProfile
/// </summary>
[GlobalClass]
public partial class PlayerProfile : Resource
{
    [Export] public string ProfileName = "Player 1";

    /// <summary>
    /// Replacement for the main body texture atlas (untitled_ma_mdl1_0.png).
    /// Paint your custom colors on a copy of that texture and assign it here.
    /// Leave null to keep the default look.
    /// </summary>
    [Export] public Texture2D BodyTexture = null;

    /// <summary>
    /// Optional replacement for the eyes/iris texture (untitled_ma_mdl1_1.png).
    /// Leave null to keep the default.
    /// </summary>
    [Export] public Texture2D EyesTexture = null;
}
