using Godot;

/// <summary>
/// SMS 258: one alternate colour a character can wear (Jaysonn: "the color options mario can wear ... alt colors from super smash bros melee ... in
/// the char select screen pick them by pressing X or Y to cycle thru them"). A CharacterEntry lists these in Colors; the Player Select cycles them
/// with X / Y on the highlighted poster and GameData carries the pick (SelectedColor) to the spawn, where Mario.ApplyBodyTexture swaps the body
/// atlas (the surfaces whose albedo path contains BodyTextureMatch, "ma_mdl1_0": the cap, shirt, overalls, shoes, buttons, emblem; the eyes and the
/// gloves are other atlases). A colour with no BodyTexture is the character's own look (the default). Swatch is the dot the poster shows for it.
/// </summary>
[GlobalClass]
public partial class CharacterColor : Resource
{
    /// <summary>The colour's name (the poster's label): Red, Yellow, Blue, Green, Black ...</summary>
    [Export] public string DisplayName = "Red";

    /// <summary>The body atlas for this colour; null = the character scene's own texture.</summary>
    [Export] public Texture2D BodyTexture;

    /// <summary>Which albedo paths the body texture replaces (PlayerProfile.BodyTextureMatch's rule).</summary>
    [Export] public string BodyTextureMatch = "ma_mdl1_0";

    /// <summary>The dot shown on the poster for this colour.</summary>
    [Export] public Color Swatch = new(0.93f, 0.11f, 0.14f, 1f);
}
