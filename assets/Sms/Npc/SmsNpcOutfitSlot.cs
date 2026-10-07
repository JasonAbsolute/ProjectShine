using Godot;
using System.Collections.Generic;

/// <summary>One slot of an NPC's outfit (pass 88): the parts that exclude each other on the model -- one hat on 'kubi', one pair of
/// glasses, one neck piece on 'body_jnt', one thing in the hand ('handR_jnt'). The NPC's inspector shows a dropdown per slot:
/// None / Random / each part here (named from its scene file: HatA.tscn -> "Hat A"). Add your own part scene (any Node3D with
/// SmsNpcPart and a Joint) to <see cref="Parts"/> and it is a choice. <see cref="Odds"/> is the chance a Random slot gets something
/// (scaled by the NPC's RandomOutfitDensity / 0.5); <see cref="Together"/> hands out every part of the slot at once (the W's two arrows).</summary>
[GlobalClass]
public partial class SmsNpcOutfitSlot : Resource
{
    [Export] public string Name = "Hat";
    [Export(PropertyHint.Range, "0,1")] public float Odds = 0.5f;
    [Export] public Godot.Collections.Array<PackedScene> Parts = new();
    [Export] public bool Together = false;

    public const int None = 0, Random = 1, FirstPart = 2;

    /// <summary>The dropdown's labels: None, Random, then the parts (one entry for all of them when Together).</summary>
    public string OptionHint()
    {
        var labels = new List<string> { "None", "Random" };
        if (Together) labels.Add(string.Join(" + ", PartLabels()));
        else labels.AddRange(PartLabels());
        return string.Join(",", labels);
    }

    public IEnumerable<string> PartLabels()
    {
        foreach (var p in Parts) yield return Label(p);
    }

    /// <summary>HatA.tscn -> "Hat A", MustacheA -> "Mustache A", Luggage -> "Luggage" (a space before each capital after the first).</summary>
    public static string Label(PackedScene scene)
    {
        if (scene == null) return "(empty)";
        string n = System.IO.Path.GetFileNameWithoutExtension(scene.ResourcePath);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < n.Length; i++)
        {
            if (i > 0 && char.IsUpper(n[i]) && !char.IsUpper(n[i - 1])) sb.Append(' ');
            sb.Append(n[i]);
        }
        return sb.ToString().Replace(",", " ");
    }

    /// <summary>The scenes a choice stands for: a part index (FirstPart + i; every part when Together), or none.</summary>
    public IEnumerable<PackedScene> ScenesFor(int choice)
    {
        if (choice < FirstPart) yield break;
        if (Together) { foreach (var p in Parts) if (p != null) yield return p; yield break; }
        int i = choice - FirstPart;
        if (i < Parts.Count && Parts[i] != null) yield return Parts[i];
    }

    /// <summary>A Random roll: nothing, or one part (all of them when Together) at the odds x density / 0.5.</summary>
    public IEnumerable<PackedScene> Roll(System.Random rng, float density)
    {
        if (Parts.Count == 0) yield break;
        if (rng.NextDouble() >= Mathf.Min(Odds * Mathf.Clamp(density, 0f, 1f) / 0.5f, 1f)) yield break;
        if (Together) { foreach (var p in Parts) if (p != null) yield return p; yield break; }
        var p1 = Parts[rng.Next(Parts.Count)];
        if (p1 != null) yield return p1;
    }

    public bool Holds(string scenePath)
    {
        foreach (var p in Parts) if (p != null && p.ResourcePath == scenePath) return true;
        return false;
    }
}
