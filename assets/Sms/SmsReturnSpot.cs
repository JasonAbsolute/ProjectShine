using Godot;

/// <summary>
/// SMS pass 119: TMarioPositionObj, the stage's "マリオセット位置" (MarioPositionObj.cpp) -- the Plaza keeps one return spot per stage, and
/// TMarDirector::setMario (MarDirectorDirect.cpp 534) picks the one for the stage he just left and starts him there in the gate-out demo
/// (TMario::returnStart). Place one in a level for each level you can come back FROM: <see cref="FromScene"/> = that level's .tscn path
/// ("" = the spot for any level without its own). He appears at this node, facing its forward (-Z) when a shine sent him, the opposite way
/// otherwise (returnStart: rotation / rotation + 180), and drops from here to the floor -- so a spot in the air falls as the game's do.
/// </summary>
[GlobalClass]
public partial class SmsReturnSpot : Node3D
{
    /// <summary>The level this spot is the return for (its .tscn path); "" = any level that has no spot of its own.</summary>
    [Export(PropertyHint.File, "*.tscn")] public string FromScene = "";

    public const string Group = "sms_return_spot";

    public override void _EnterTree() => AddToGroup(Group);

    /// <summary>A scene reference as a res:// path: the editor saves a File export as "uid://..." (Godot 4.4+), SceneFilePath is the path.</summary>
    public static string ScenePath(string pathOrUid) => string.IsNullOrEmpty(pathOrUid) ? "" : ResourceUid.EnsurePath(pathOrUid);

    /// <summary>The spot for the level he left: its own first, else (allowAny) the one for any level; null = none placed.</summary>
    public static SmsReturnSpot Find(SceneTree tree, string fromScene, bool allowAny = true)
    {
        string want = ScenePath(fromScene);
        SmsReturnSpot any = null;
        foreach (Node n in tree.GetNodesInGroup(Group))
        {
            if (n is not SmsReturnSpot spot || !spot.IsInsideTree()) continue;
            if (want != "" && !string.IsNullOrEmpty(spot.FromScene) && ScenePath(spot.FromScene) == want) return spot;
            if (allowAny && string.IsNullOrEmpty(spot.FromScene)) any ??= spot;
        }
        return any;
    }
}
