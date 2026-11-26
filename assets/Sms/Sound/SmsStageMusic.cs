using Godot;

/// <summary>
/// SMS 241: a stage's music, started as the game starts it (System/MSoundMainSide.cpp, MSMainProc::setMSoundEnterStage): every secret course --
/// stages 46-51, coro_ex0-5; the Hillside Cave Secret is 47 (coro_ex1), the Secret of Ricco Tower 48 (coro_ex2) -- has demoBgm = stageBgm =
/// MSD_BGM_EXTRA (k_ex.com, the Secret Course theme) with flags 0: startStageEntranceDemo starts it the moment the start camera's pan begins, the
/// pan's end neither stops it (flags bit 8) nor fades it (bit 4), and startStageBGM, when Mario takes control, does not start it again (bit 2), so
/// the one play carries straight on into the course and loops as the sequence loops (the WAV's loop region, assets/Sms/Sound/Music/MSD_BGM_EXTRA.wav:
/// a 6.8-s intro, then 82.3 s round and round). Put this on an AudioStreamPlayer in the level with the theme as its stream: it plays on the Music
/// bus at the boss themes' level (SmsActorSound.MusicDb; MSBgm plays BGMs at 0.75 of full) unless SmsActorSound.Music is off, and the miss, the
/// game over and the shine get cut it with every other music player (SmsActorSound.StopAllMusic), as MSBgm::startBGM's jingles cut every BGM; the
/// stage reloads after a miss, so it starts over with the course. Its level is the music slots' (SmsActorSound): MusicDb plus the sound table's
/// volume for the sound named by the stream's file (MSD_BGM_EXTRA: 100 of 127), the file itself on the boss theme's normalisation (level225.py) so
/// the game's balance between the themes holds.
/// </summary>
public partial class SmsStageMusic : AudioStreamPlayer
{
    /// <summary>The game's: demoBgm == stageBgm with flags 0 -- the theme starts as the level loads, through the intro pan. Off = nothing plays
    /// until something calls Play().</summary>
    [Export] public bool PlayOnLoad = true;

    /// <summary>Use the music slots' level -- SmsActorSound.MusicDb + the sound table's volume for the stream's sound -- as this player's volume;
    /// off = the volume_db set on the node.</summary>
    [Export] public bool ActorSoundLevel = true;

    public override void _Ready()
    {
        Bus = SmsActorSound.MusicBus;
        string sound = Stream != null ? System.IO.Path.GetFileNameWithoutExtension(Stream.ResourcePath) : "";
        if (ActorSoundLevel) VolumeDb = SmsActorSound.MusicDb + SmsActorSound.LevelDb(sound);
        if (!PlayOnLoad || !SmsActorSound.Music || Stream == null) return;
        Play();
        if (SmsActorSound.DebugPrint) GD.Print($"[SMS stage music] {Name}: {sound} ({Stream.GetLength():F1} s) on the {Bus} bus at {VolumeDb:F1} dB");
    }
}
