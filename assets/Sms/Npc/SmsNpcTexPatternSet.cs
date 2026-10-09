using Godot;

/// <summary>
/// A kind's texture pattern animations (MActorAnmData's .btp list for one NPC model): the patterns, the textures they index, and the default
/// pattern a clip without one of its own plays (the Piantas' wink; the Toad's twait). Profiles/*.tres point at TexPatterns/<Kind>.tres.
/// </summary>
[Tool, GlobalClass]
public partial class SmsNpcTexPatternSet : Resource
{
    /// <summary>The textures the patterns index (the bmd's eye / mouth sheets, assets/Sms/Npc/tex).</summary>
    [Export] public Godot.Collections.Array<Texture2D> Textures = new();
    /// <summary>The kind's patterns (SmsNpcTexPattern each).</summary>
    [Export] public Godot.Collections.Array Patterns = new();
    /// <summary>The pattern for a clip no entry claims (sMonte*_AnmIndex: the wink on every Pianta clip without its own btp; the Toad's twait).</summary>
    [Export] public SmsNpcTexPattern Default;

    /// <summary>The pattern the anm-index table pairs with `clip`, else <see cref="Default"/> (null when the set has none).</summary>
    public SmsNpcTexPattern For(string clip)
    {
        if (!string.IsNullOrEmpty(clip))
            foreach (var v in Patterns)
                if (v.Obj is SmsNpcTexPattern p && p.Clips != null && System.Array.IndexOf(p.Clips, clip) >= 0) return p;
        return Default;
    }

    public SmsNpcTexPattern ByName(string name)
    {
        foreach (var v in Patterns) if (v.Obj is SmsNpcTexPattern p && p.PatternName == name) return p;
        return null;
    }

    public Texture2D TextureAt(int index) => index >= 0 && index < Textures.Count ? Textures[index] : null;
}
