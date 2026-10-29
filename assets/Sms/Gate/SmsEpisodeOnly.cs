using Godot;

/// <summary>
/// SMS pass 127: what a level shows per EPISODE. The game loads a different scene.bin for each scenario of a stage (bianco0 .. bianco7: the
/// shine, the enemies, the goop and the NPCs of that episode); here one level holds every episode and this node keeps its children only for
/// the episodes listed in <see cref="Episodes"/> (1-based, as the screen shows them). Put a shine, a spawner, a goop patch, an NPC under
/// one of these and it exists only in those episodes. The episode is GameData.Episode (MSF_EPISODE), set by the episode select; a level run
/// on its own (no episode) keeps everything when <see cref="WhenNoEpisode"/> is on.
/// </summary>
[GlobalClass]
public partial class SmsEpisodeOnly : Node3D
{
    /// <summary>The episodes (1 = EPISODE 1) this node's children exist in. Empty = never.</summary>
    [Export] public int[] Episodes = new[] { 1 };
    /// <summary>A level run without an episode (GameData.Episode -1: from the editor, or no select screen): keep the children.</summary>
    [Export] public bool WhenNoEpisode = true;
    /// <summary>Off: the children are hidden and their processing stopped instead of freed (a node another script keeps a path to).</summary>
    [Export] public bool FreeChildren = true;
    [Export] public bool DebugPrint;

    /// <summary>Whether this node's children are in play for the current episode.</summary>
    public bool InPlay { get; private set; } = true;

    public override void _EnterTree()
    {
        int ep = GameData.Instance != null ? GameData.Instance.Episode : -1;
        bool keep = ep < 0 ? WhenNoEpisode : System.Array.IndexOf(Episodes, ep + 1) >= 0;
        InPlay = keep;
        if (keep) return;
        // before any child's _Ready: free (or silence) them, so an episode's shine never registers for another episode
        foreach (Node c in GetChildren())
        {
            if (FreeChildren) { RemoveChild(c); c.QueueFree(); }
            else { if (c is Node3D n) n.Visible = false; c.ProcessMode = ProcessModeEnum.Disabled; }
        }
        if (DebugPrint) GD.Print($"[SMS episode] {Name}: not in episode {(ep < 0 ? "none" : (ep + 1).ToString())} -- {(FreeChildren ? "children freed" : "children hidden")}");
    }
}
