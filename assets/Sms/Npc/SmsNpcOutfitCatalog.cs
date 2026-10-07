using Godot;

/// <summary>A species' outfit catalog (pass 88): the slots an NPC's inspector offers as dropdowns (Outfit section). Profiles/OutfitPianta.tres,
/// OutfitPiantaFemale.tres and OutfitToad.tres hold the disc's parts (NpcInitData.cpp's twelve slots per model, pass 84, as scenes under
/// assets/Sms/Npc/Parts); duplicate one to make a new wardrobe, or add a slot / a part scene of your own. In the game an NPC's parts come
/// from its placement word (TNpcParts: bit i of the stage data picks slot i) -- these dropdowns ARE that word, per instance, and "Random"
/// is the DEVIATION of pass 85 kept per slot. Joints the parts hang on: 'kubi' (the head: hats, glasses, the moustache), 'body_jnt' (the
/// neck / chest: lapel, tie, the luggage), 'handR_jnt' (the W's arrows -- a thing held), 'yashi_jnt' (the W's flower).</summary>
[Tool, GlobalClass]   // [Tool] (pass 91): see SmsNpcProfile
public partial class SmsNpcOutfitCatalog : Resource
{
    [Export] public Godot.Collections.Array<SmsNpcOutfitSlot> Slots = new();

    /// <summary>The inspector property name for a slot: "Outfit" + the slot's name without spaces (OutfitHat).</summary>
    public static string PropertyFor(SmsNpcOutfitSlot slot) => "Outfit" + (slot?.Name ?? "").Replace(" ", "");

    public SmsNpcOutfitSlot SlotFor(string property)
    {
        foreach (var s in Slots) if (s != null && PropertyFor(s) == property) return s;
        return null;
    }
}
