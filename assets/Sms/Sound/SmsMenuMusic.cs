using Godot;

/// <summary>
/// SMS 251: the menu music -- Jaysonn's song for the Player Select and the Level Select (assets/Sms/Sound/Music/LevelCharSelectMusic.mp3, his
/// Ref/Level&amp;CharSelectMusic.mp3). Put this on an AudioStreamPlayer in a select screen with the song as its stream. The song is one piece for
/// both screens, so by default it does not restart on the way from the Player Select to the Level Select: the player that actually plays is a
/// KEEPER of this same class parked under the scene tree's root (/root/SmsMenuMusic), started by the first menu that readies and left alone by the
/// next one when it carries the same song; the keeper watches the current scene and fades itself out the moment the scene is no longer a menu
/// (nothing of this class among its children) -- a level, the title. A level's own music then starts clean, and a return to the Level Select
/// (a Shine's exit, the pause menu's Exit Area) starts the song over. The miss / game-over / shine-get cut (SmsActorSound.StopAllMusic) stops it
/// like every other music player on the Music bus. Volume: SmsActorSound.MusicDb (the music slots' level, the setting's knob) + StreamDb, on the
/// Music bus, so the Music/SFX volume settings apply; SmsActorSound.Music off = silent menus.
/// </summary>
public partial class SmsMenuMusic : AudioStreamPlayer
{
    /// <summary>Start as the screen loads. Off = nothing plays until Play() is called.</summary>
    [Export] public bool PlayOnLoad = true;

    /// <summary>Loop the song (an mp3 or ogg stream's own loop flag is set at play; a WAV loops by its import setting).</summary>
    [Export] public bool Loop = true;

    /// <summary>Where the loop returns to, in seconds (0 = the song's start).</summary>
    [Export(PropertyHint.Range, "0,600,0.1")] public float LoopOffset = 0f;

    /// <summary>This song's level on top of the music slots' level (SmsActorSound.MusicDb). The Level&amp;CharSelect mp3 is mastered loud (RMS -12.4 dBFS,
    /// peaks at 0) against the game themes' renders (the boss theme RMS -17.6 dBFS), so -5.2 dB puts it level with them.</summary>
    [Export(PropertyHint.Range, "-24,12,0.1")] public float StreamDb = -5.2f;

    /// <summary>Keep playing across menu screens: the song is handed to a keeper under /root that survives the scene change from the Player Select
    /// to the Level Select and stops when the scene is no longer a menu. Off = this node plays itself and the song restarts with each screen.</summary>
    [Export] public bool CarryAcrossMenus = true;

    /// <summary>How long the keeper fades out when the menus are left (seconds; 0 = cut).</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float FadeOutSeconds = 0.35f;

    public const string KeeperName = "SmsMenuMusic";
    /// <summary>A build marker for the drives (read in the built DLL before a run).</summary>
    public const string BuildMarker = "SMS251_MENU_MUSIC_A";

    private bool _isKeeper;
    private float _fade = -1f;
    private float _levelDb;

    public override void _Ready()
    {
        if (_isKeeper) return;              // the keeper is set up by Become()
        Bus = SmsActorSound.MusicBus;
        _levelDb = SmsActorSound.MusicDb + StreamDb;
        VolumeDb = _levelDb;
        if (!PlayOnLoad || Stream == null) return;
        if (!SmsActorSound.Music) return;
        if (!CarryAcrossMenus) { Arm(Stream); Play(); return; }

        var root = GetTree().Root;
        if (root.GetNodeOrNull(KeeperName) is SmsMenuMusic keeper && IsInstanceValid(keeper))
        {
            if (keeper.Playing && keeper.Stream != null && keeper.Stream.ResourcePath == Stream.ResourcePath)
            {
                keeper._fade = -1f; keeper.VolumeDb = _levelDb;     // the same song carries on (a fade that had begun is undone)
                if (SmsActorSound.DebugPrint) GD.Print($"[SMS menu music] {GetParent()?.Name}: {Stream.ResourcePath.GetFile()} carries on at {keeper.GetPlaybackPosition():F1} s");
                return;
            }
            keeper.QueueFree();                                      // another song: the old keeper goes
        }
        var k = new SmsMenuMusic { Name = KeeperName, Bus = SmsActorSound.MusicBus, _isKeeper = true, Stream = Stream, Loop = Loop, LoopOffset = LoopOffset, StreamDb = StreamDb, FadeOutSeconds = FadeOutSeconds, VolumeDb = _levelDb, _levelDb = _levelDb };
        k.Arm(Stream);
        root.CallDeferred(Node.MethodName.AddChild, k);
        k.CallDeferred(AudioStreamPlayer.MethodName.Play, 0.0);
        if (SmsActorSound.DebugPrint) GD.Print($"[SMS menu music] {GetParent()?.Name}: {Stream.ResourcePath.GetFile()} ({Stream.GetLength():F1} s) starts on the {Bus} bus at {_levelDb:F1} dB, kept under /root");
    }

    /// <summary>The stream's loop as asked (an mp3's / ogg's flag lives on the stream resource, shared by every player of it).</summary>
    private void Arm(AudioStream s)
    {
        if (s is AudioStreamMP3 mp3) { mp3.Loop = Loop; mp3.LoopOffset = LoopOffset; }
        else if (s is AudioStreamOggVorbis ogg) { ogg.Loop = Loop; ogg.LoopOffset = LoopOffset; }
    }

    public override void _Process(double delta)
    {
        if (!_isKeeper) return;
        if (_fade >= 0f)
        {
            _fade += (float)delta;
            float t = FadeOutSeconds > 0f ? Mathf.Clamp(_fade / FadeOutSeconds, 0f, 1f) : 1f;
            VolumeDb = Mathf.Lerp(_levelDb, -60f, t);
            if (t >= 1f) { Stop(); QueueFree(); }
            return;
        }
        if (!Playing) { QueueFree(); return; }      // cut by StopAllMusic, or the stream ended (Loop off)
        var scene = GetTree().CurrentScene;
        if (scene == null) return;                   // between scenes: wait for the next one
        foreach (Node c in scene.GetChildren())
            if (c is SmsMenuMusic m && !m._isKeeper) return;   // still a menu
        _fade = 0f;
        if (SmsActorSound.DebugPrint) GD.Print($"[SMS menu music] {scene.Name} is no menu: the song fades out over {FadeOutSeconds:F2} s");
    }

    /// <summary>For drives: the keeper, if one is playing.</summary>
    public static SmsMenuMusic Keeper(Node from) => from != null && from.IsInsideTree() ? from.GetTree().Root.GetNodeOrNull(KeeperName) as SmsMenuMusic : null;
}
