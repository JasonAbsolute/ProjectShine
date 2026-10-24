using Godot;
using System.Collections.Generic;

public partial class GameData : Node
{
    public static GameData Instance { get; private set; }

    private const string SavePath = "user://save_data.json";
    private readonly HashSet<string> _collectedBlueCoins = new();
    private readonly HashSet<string> _collectedShines = new();

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    public void CollectBlueCoin(string id)
    {
        _collectedBlueCoins.Add(id);
        Save();
    }

    public bool IsBlueCoinCollected(string id)
    {
        return _collectedBlueCoins.Contains(id);
    }

    public int BlueCoinCount => _collectedBlueCoins.Count;

    // ------------------------------------------------------------------ shines (SMS pass 118c) --

    /// <summary>TFlagManager::setShineFlag (FlagManager.cpp 308): the shine's flag set, and MSF_SHINE_COUNT up by one only when it was clear;
    /// written to disk at once (the game's card save follows in the Plaza; its dialog is not ported). True when the flag was new.</summary>
    public bool CollectShine(string id)
    {
        if (!_collectedShines.Add(id))
            return false;
        Save();
        return true;
    }

    /// <summary>TFlagManager::getShineFlag: TShine loads shine_empty.bmd for a set flag.</summary>
    public bool IsShineCollected(string id) => _collectedShines.Contains(id);

    /// <summary>MSF_SHINE_COUNT: the shines collected, as saved. Mario's count starts from it.</summary>
    public int ShineCount => _collectedShines.Count;

    // ----------------------------------------------------- character select --

    /// <summary>
    /// Who the player picked on the Player Select screen. Set by
    /// CharacterSelect.cs before it changes scene; read by PlayerSpawner in
    /// whatever level loads next.
    ///
    /// Deliberately NOT saved to disk — a character choice belongs to the run,
    /// not the save file, and persisting it would silently override the menu.
    /// </summary>
    public CharacterEntry SelectedCharacter { get; set; }

    /// <summary>Convenience for PlayerSpawner. Null when nothing is selected yet.</summary>
    public PackedScene SelectedCharacterScene => SelectedCharacter?.PlayerScene;

    /// <summary>Display name of the current pick, or "None" if a level was run directly.</summary>
    public string SelectedCharacterName => SelectedCharacter?.DisplayName ?? "None";

    // ----------------------------------------------------------------- the run --

    /// <summary>The lives (the game's TFlagManager flag 0x20001): they outlive a stage reload after a miss. -1 = not started (Mario takes
    /// his StartingLives). Not saved to disk -- lives belong to the run, as in the game.</summary>
    public int Lives = -1;

    /// <summary>The game's flag 0x30002: the next stage load follows a miss -- no intro, a plain fade in. Level.cs clears it.</summary>
    public bool CameFromMiss;

    /// <summary>SMS pass 119: the stage he just left (its scene path; null = a plain start) and whether a shine sent him
    /// (MSF_GOT_SHINE_IN_PREV_STAGE). Mario's ExitLevel exit and the pause menu's Exit Area set them; the next level starts him at its
    /// SmsReturnSpot for that stage in the gate-out demo and skips its intro. Mario clears them once used. Not saved -- the run's.</summary>
    public string ReturnFromScene;
    public bool ReturnWithShine;

    /// <summary>The pause menu's SAVE (the game's card save): write the save file now.</summary>
    public void SaveNow() => Save();

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F8)
            ClearSave();
    }

    public void ClearSave()
    {
        _collectedBlueCoins.Clear();
        _collectedShines.Clear();
        Save();
        GetTree().ReloadCurrentScene();
    }

    private void Save()
    {
        var coinArray = new Godot.Collections.Array();
        foreach (var id in _collectedBlueCoins)
            coinArray.Add(id);

        var shineArray = new Godot.Collections.Array();
        foreach (var id in _collectedShines)
            shineArray.Add(id);

        var data = new Godot.Collections.Dictionary { ["blue_coins"] = coinArray, ["shines"] = shineArray };

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file != null)
            file.StoreString(Json.Stringify(data));
    }

    private void Load()
    {
        if (!FileAccess.FileExists(SavePath)) return;

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null) return;

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok) return;

        if (json.Data.AsGodotDictionary() is Godot.Collections.Dictionary data)
        {
            if (data.TryGetValue("blue_coins", out var coinsVar))
                foreach (var coin in coinsVar.AsGodotArray())
                    _collectedBlueCoins.Add(coin.AsString());
            if (data.TryGetValue("shines", out var shinesVar))
                foreach (var shine in shinesVar.AsGodotArray())
                    _collectedShines.Add(shine.AsString());
        }
    }
}
