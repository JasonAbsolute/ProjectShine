using Godot;

/// <summary>
/// SMS pass 127: a STAGE as the game's select screen knows it -- the TGameSequence stage behind an M gate (TModelGate::unk71 -> the
/// stage index) with what TSelectMenu::initData reads for it: the stage banner (sc_map_*.bti), the eight scenarios of its shine table
/// (scShineTable*, StageUtil.hpp) and their names (scenarioname.bmg), and TSelectGrad::setStageColor's gradient. Make one .tres per level
/// you can enter through a gate and put it on the gate (<see cref="SmsModelGate.Stage"/>). The game's rule for what you may pick
/// (initData 460-490): episode i is open when i is the first or episode i - 1 has its shine; the newest open one starts selected.
/// </summary>
[GlobalClass]
public partial class SmsStageInfo : Resource
{
    /// <summary>The level this stage is: the scene the gate finally loads (the character select, when it runs, loads it too).</summary>
    [Export(PropertyHint.File, "*.tscn")] public string Scene = "";
    /// <summary>The stage's name as stagename.bmg gives it ("RICCO HARBOR"); drawn in the big letters when there is no <see cref="Banner"/>.
    /// The pause menu's bar and the level's own StageName are not read from here.</summary>
    [Export] public string StageName = "";
    /// <summary>The select screen's stage banner (sc_map_ricco.bti and the others are under assets/Sms/Gate/select/); none = the name in letters.</summary>
    [Export] public Texture2D Banner;
    /// <summary>The episodes in order (EPISODE 1 first). The game has eight; fewer is fine, more than eight the screen cannot show.</summary>
    [Export] public SmsEpisodeInfo[] Episodes = System.Array.Empty<SmsEpisodeInfo>();
    /// <summary>The screen to run between the episode pick and the level (his character select); empty = straight into the level.</summary>
    [Export(PropertyHint.File, "*.tscn")] public string CharacterSelectScene = "res://assets/CharacterSelect.tscn";

    /// <summary>TSelectGrad::setStageColor: the gradient behind the menu, its top-left and bottom-right colours and where in the six-step
    /// hue cycle each channel starts (the game: Bianco 3/1/5 yellow-green, Ricco 4/2/0 green-cyan, Gelato 2/0/4 red-yellow, Corona 0/4/2
    /// blue-magenta; every other stage the Gelato colours).</summary>
    [ExportGroup("Select screen gradient")]
    [Export] public Color GradientTopLeft = new Color(0f, 1f, 0f);
    [Export] public Color GradientBottomRight = new Color(0f, 1f, 1f);
    [Export] public Vector3I GradientCycle = new Vector3I(4, 2, 0);

    /// <summary>How many episodes may be picked (TSelectMenu::mNumUnlockedShines): one past the last cleared one, at least one, at most
    /// the count. A cleared episode = its shine's save flag (GameData.IsShineCollected on its ShineId).</summary>
    public int UnlockedCount()
    {
        int n = Episodes.Length;
        if (n == 0) return 0;
        int unlocked = 1;
        for (int i = 0; i < n; i++)
            if (IsCleared(i)) unlocked = i + 2;
        return Mathf.Clamp(unlocked, 1, n);
    }

    /// <summary>SMS_isGetShine(stage, scenario): the episode's shine is in the save (in RAM: the card save writes it).</summary>
    public bool IsCleared(int episode)
    {
        if (episode < 0 || episode >= Episodes.Length || Episodes[episode] == null) return false;
        string id = Episodes[episode].ShineId;
        return GameData.Instance != null && !string.IsNullOrEmpty(id) && GameData.Instance.IsShineCollected(id);
    }

    /// <summary>The scene as a res:// path (the editor saves a File export as uid:// in 4.4+).</summary>
    public string ScenePath => SmsReturnSpot.ScenePath(Scene);

    public string EpisodeName(int episode) => episode >= 0 && episode < Episodes.Length && Episodes[episode] != null ? Episodes[episode].Name : "";
}
