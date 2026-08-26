using Godot;

/// <summary>
/// One selectable character on the Player Select screen.
///
/// Deliberately separate from PlayerProfile: a profile describes how a body
/// LOOKS (mesh, materials, hands), while this describes a menu ENTRY (which
/// scene to spawn, what art to show). One character scene already carries its
/// own profile, so the menu never needs to know about profiles at all.
/// </summary>
[GlobalClass]
public partial class CharacterEntry : Resource
{
    [Export] public string DisplayName = "Mario";

    /// <summary>The character scene to spawn — Mario.tscn, Luigi.tscn, etc.</summary>
    [Export] public PackedScene PlayerScene;

    /// <summary>The character's head shot, dropped into the poster's photo panel.</summary>
    [Export] public Texture2D HeadArt;

    /// <summary>The stylised, arched name art that sits above the head.</summary>
    [Export] public Texture2D NameArt;

    /// <summary>
    /// A complete pre-made poster. Optional — set it only to bypass the
    /// frame+head+name composition entirely for one character.
    /// </summary>
    [Export] public Texture2D Portrait;

    /// <summary>Set false to show the entry but refuse selection (locked character).</summary>
    /// <summary>
    /// Stroke-order map for the letter stamped on when this character is picked,
    /// baked by tools/bake_stroke_order.py. Leave null to skip the stamp.
    /// </summary>
    [Export] public Texture2D StrokeArt;

    /// <summary>Colour the stamp is painted in.</summary>
    [Export] public Color PaintColor = new(0.933f, 0.016f, 0f, 1f);

    [Export] public bool Unlocked = true;
}
