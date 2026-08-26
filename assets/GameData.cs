using Godot;
using System.Collections.Generic;

public partial class GameData : Node
{
    public static GameData Instance { get; private set; }

    private const string SavePath = "user://save_data.json";
    private readonly HashSet<string> _collectedBlueCoins = new();

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

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F8)
            ClearSave();
    }

    public void ClearSave()
    {
        _collectedBlueCoins.Clear();
        Save();
        GetTree().ReloadCurrentScene();
    }

    private void Save()
    {
        var coinArray = new Godot.Collections.Array();
        foreach (var id in _collectedBlueCoins)
            coinArray.Add(id);

        var data = new Godot.Collections.Dictionary { ["blue_coins"] = coinArray };

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

        if (json.Data.AsGodotDictionary() is Godot.Collections.Dictionary data &&
            data.TryGetValue("blue_coins", out var coinsVar))
        {
            foreach (var coin in coinsVar.AsGodotArray())
                _collectedBlueCoins.Add(coin.AsString());
        }
    }
}
