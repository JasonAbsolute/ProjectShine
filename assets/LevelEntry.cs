using Godot;

/// <summary>
/// One level on the Level Select screen (SMS 242) -- the Player Select screen's CharacterEntry, for a level: which scene to load and what
/// art its poster shows. The screen owns no art of its own, so adding a level is a resource change (a .tres under assets/levels/) with no
/// code edit, as adding a character is.
/// </summary>
[GlobalClass]
public partial class LevelEntry : Resource
{
    /// <summary>The level's name as the game gives it (the log, the editor list).</summary>
    [Export] public string DisplayName = "The Secret of Ricco Tower";

    /// <summary>The level scene to load when this entry is picked.</summary>
    [Export(PropertyHint.File, "*.tscn")] public string ScenePath = "";

    /// <summary>The level's picture, dropped into the poster's photo panel (where a character's head goes).</summary>
    [Export] public Texture2D Art;

    /// <summary>The stylised, arched name art that sits above the picture.</summary>
    [Export] public Texture2D NameArt;

    /// <summary>A complete pre-made poster. Optional -- set it only to bypass the frame + picture + name composition for one level.</summary>
    [Export] public Texture2D Portrait;

    /// <summary>Stroke-order map for the mark stamped on when this level is picked (tools/bake_stroke_order.py). Null = no stamp.</summary>
    [Export] public Texture2D StrokeArt;

    /// <summary>Colour the stamp is painted in.</summary>
    [Export] public Color PaintColor = new(0.933f, 0.016f, 0f, 1f);

    /// <summary>Set false to show the entry but refuse selection (a locked level).</summary>
    [Export] public bool Unlocked = true;

    /// <summary>The poster's entry: the same poster scene the Player Select uses (CharacterPoster) reads a CharacterEntry, so the level's
    /// art is handed over in one -- the picture where the head goes, the name art above it, the stamp, the lock.</summary>
    public CharacterEntry AsPosterEntry() => new CharacterEntry
    {
        DisplayName = DisplayName, HeadArt = Art, NameArt = NameArt, Portrait = Portrait, StrokeArt = StrokeArt, PaintColor = PaintColor, Unlocked = Unlocked,
    };
}
