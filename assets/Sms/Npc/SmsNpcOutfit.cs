using Godot;
using System.Collections.Generic;

/// <summary>The random-outfit roll (pass 85) -- a DEVIATION at Jaysonn's ask: the game has no random parts; every NPC's hats / glasses / moustache
/// come from its placement word (TNpcParts: bit i of the stage data picks slot i of NpcInitData's twelve). This table groups the shipped part
/// scenes (assets/Sms/Npc/Parts, pass 84) the way the slots exclude each other on the model -- one hat on the head joint, one pair of glasses,
/// one neck piece -- and gives each group the odds a Pianta in the plaza gets something there. <see cref="SmsNpc.RandomOutfit"/> rolls it once
/// at _Ready for the groups the scene has not filled by hand; <see cref="SmsNpc.RandomOutfitDensity"/> scales every group's odds (0.5 = these).</summary>
public static class SmsNpcOutfit
{
    public readonly struct Group
    {
        public readonly string Name; public readonly float Odds; public readonly string[] Scenes; public readonly bool All;
        /// <param name="all">The group's scenes come together (the W's two arrows) instead of one picked at random.</param>
        public Group(string name, float odds, string[] scenes, bool all = false) { Name = name; Odds = odds; Scenes = scenes; All = all; }
    }

    private const string P = "res://assets/Sms/Npc/Parts/";

    public static readonly Group[] Pianta =
    {
        new("hat", 0.6f, new[] { P + "Pianta/HatA.tscn", P + "Pianta/HatB.tscn", P + "Pianta/HatD.tscn", P + "Pianta/HatE.tscn", P + "Pianta/HatF.tscn", P + "Pianta/HatG.tscn" }),
        new("moustache", 0.35f, new[] { P + "Pianta/MustacheA.tscn" }),
        new("glasses", 0.25f, new[] { P + "Pianta/GlassesA.tscn", P + "Pianta/GlassesB.tscn" }),
        new("neck", 0.3f, new[] { P + "Pianta/LapelA.tscn", P + "Pianta/TieB.tscn" }),
        new("hand", 0.1f, new[] { P + "Pianta/Luggage.tscn" }),
    };

    public static readonly Group[] PiantaFemale =
    {
        new("flower", 0.5f, new[] { P + "PiantaFemale/Flower.tscn" }),
        new("hat", 0.3f, new[] { P + "PiantaFemale/HatA.tscn" }),
        new("glasses", 0.2f, new[] { P + "PiantaFemale/GlassesB.tscn" }),
        new("arrows", 0.1f, new[] { P + "PiantaFemale/ArrowL.tscn", P + "PiantaFemale/ArrowR.tscn" }, all: true),
    };

    public static readonly Group[] Toad =
    {
        new("glasses", 0.3f, new[] { P + "Toad/Sunglasses.tscn" }),
    };

    public static Group[] For(SmsNpcProfile.NpcKind kind) => kind switch
    {
        SmsNpcProfile.NpcKind.PiantaFemale => PiantaFemale,
        SmsNpcProfile.NpcKind.Toad => Toad,
        _ => Pianta,
    };

    /// <summary>The scene paths the roll picks: every group not already represented by <paramref name="placed"/> (a part scene the NPC carries
    /// by hand keeps its group out of the roll), each at its odds x density / 0.5, one scene of the group at random -- or all of them.</summary>
    public static List<string> Roll(SmsNpcProfile.NpcKind kind, float density, IEnumerable<string> placed, System.Random rng)
    {
        var picks = new List<string>();
        var have = new HashSet<string>(placed ?? System.Array.Empty<string>());
        float scale = Mathf.Clamp(density, 0f, 1f) / 0.5f;
        foreach (var g in For(kind))
        {
            bool filled = false;
            foreach (var s in g.Scenes) if (have.Contains(s)) { filled = true; break; }
            if (filled) continue;
            if (rng.NextDouble() >= Mathf.Min(g.Odds * scale, 1f)) continue;
            if (g.All) picks.AddRange(g.Scenes);
            else picks.Add(g.Scenes[rng.Next(g.Scenes.Length)]);
        }
        return picks;
    }
}
