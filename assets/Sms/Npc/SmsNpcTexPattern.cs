using Godot;

/// <summary>
/// One J3D texture pattern animation (a .btp: J3DAnmTexPattern) -- the SMS NPCs' blinks and mouths. Per frame it names the texture each
/// listed material shows (getTexNo: the frame clamped to the keyed range), e.g. mom_wink: 90 looping frames of I_mom_eye01 with two quick
/// blinks (eye02 / eye03 / eye04) at frames 5-11 and 17-23; kinopio_ttalk: the Toad's mouth cycling closed / half / open under talking eyes.
/// The game pairs each anm kind with a bck AND a btp (NpcInitAnmData.cpp sMonte*/sKinopio_AnmIndex -> TLodAnm::setBckAndBtpAnm): a clip
/// with a pattern of its own name uses it (mom_wet + mom_wet.btp), every other Pianta clip gets the wink; the Toad's six patterns cover its
/// clips by table (<see cref="Clips"/>). Generated from the disc's .btp files by gen_npc_btp.py (tools/projectshine) into
/// assets/Sms/Npc/TexPatterns/*.tres; <see cref="SmsNpcTexPatternSet"/> holds a kind's patterns and their textures.
/// </summary>
[Tool, GlobalClass]   // [Tool] (pass 91): a [Tool] SmsNpc / SmsNpcProfile exports this type; a non-tool C# Resource is only a placeholder in the editor
public partial class SmsNpcTexPattern : Resource
{
    /// <summary>The .btp's stem (mom_wink, kinopio_ttalk).</summary>
    [Export] public string PatternName = "";
    /// <summary>The bck clips this pattern plays with (the anm-index table); empty on the kind's default pattern.</summary>
    [Export] public string[] Clips = System.Array.Empty<string>();
    /// <summary>TPT1 frameMax: the pattern's length in 60 Hz frames.</summary>
    [Export] public int FrameMax = 1;
    /// <summary>TPT1 attribute 2 = loop (the wink, the Toad's idles); 0 = once (mom_wet holds its last frame).</summary>
    [Export] public bool Loop = true;
    /// <summary>One entry per track: the material it drives (_eye_mat -> the eye shader's eye_tex; _mat_mouth -> the skin shader's base_tex).</summary>
    [Export] public string[] Materials = System.Array.Empty<string>();
    /// <summary>Materials.Length x FrameMax texture indices into the set's <see cref="SmsNpcTexPatternSet.Textures"/>, one row per track.</summary>
    [Export] public int[] Frames = System.Array.Empty<int>();

    /// <summary>getTexNo: the texture index of `track` at `frame` (clamped to the keyed range -- a once pattern holds its last frame).</summary>
    public int TexIndex(int track, int frame)
    {
        if (FrameMax <= 0 || track < 0 || track >= Materials.Length) return -1;
        int f = Mathf.Clamp(frame, 0, FrameMax - 1);
        int i = track * FrameMax + f;
        return i < Frames.Length ? Frames[i] : -1;
    }
}
