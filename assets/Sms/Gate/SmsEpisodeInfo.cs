using Godot;

/// <summary>One episode of a stage: its name (scenarioname.bmg), the shine that marks it cleared, and the picture the select screen shows
/// for it (the game plays a THP movie of the episode there; a still stands in -- DEVIATION, pass 127).</summary>
[Tool, GlobalClass]
public partial class SmsEpisodeInfo : Resource
{
    /// <summary>The episode's name, as scenarioname.bmg gives it ("Red Coins in a Bottle").</summary>
    [Export] public string Name = "";
    /// <summary>The save key of the shine that clears this episode -- the ShineSprite's ShineId in the level (its SmsShineKey; empty on the
    /// shine = the scene path + node path, which you can copy from a debug print). Empty here = the episode never reads as cleared.</summary>
    [Export] public string ShineId = "";
    /// <summary>The episode's picture on the select screen (none = the stage's gradient shows through). The game: the episode's movie.</summary>
    [Export] public Texture2D Preview;
}
