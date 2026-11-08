using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>SMS 155: an ACTOR's sounds from the game's banks -- King Boo's fight first (TBossTelesa, the slot machine, the roulette discs, the Boo
/// attackers). One node under the actor, attached by <see cref="Attach"/> from its _Ready, does three things the game does:
///  1. the actor's ANIMATION SOUND TABLES (the species' bas files, SmsActorSoundTable: MAnmSound::animeLoop fires each entry as the bck passes its
///     frame; a 0x11 / 0x12 pair HOLDS a sound between two frames) -- read off SmsSmallEnemy's BckName / BckFrameNow every tick;
///  2. the script's own SOUND SLOTS (the exported AudioStreamPlayer3D fields each species already plays at the game's moments) filled with the
///     baked WAVs when they are empty, by name (<see cref="Fill"/>); a slot set in the inspector is left alone;
///  3. the CONTINUOUS sounds the game restarts every frame while a state holds (startSoundActor each tick: King Boo's FIRE through the spicy clip,
///     the roulettes' RLT_MOVE1 / 2 / 3 by how many discs roll, the intro's RLT_DOWN rumble and V_LAUGH1B laugh, the slot's OBJ_SLOT_SPIN rattle)
///     as loops keyed on the actor's state (<see cref="StateTick"/>).
/// Every channel is positional (an AudioStreamPlayer3D on the actor: Godot pans it) with Godot's own falloff and low-pass OFF and the GAME's distance
/// curve applied here (JAISound::setDistanceVolumeCommon: full within FullVolumeUnits of the camera, a straight fade to silence at SilentUnits), so
/// King Boo at the fixed camera's 30 m sits a few dB under Mario, as in the game, and never muffled. All on the "Sfx" bus (SmsMarioSound's volume).
/// 156: the BOSS MUSIC. The game's BGMs are sequences (sequence.arc's BMS files) played through the same banks as the sound effects; the port plays
/// them as stereo WAVs rendered from those sequences with those instruments (tools/projectshine/sound154/sms_bgm.py: the tracks' notes, volumes,
/// pans, pitch bends and vibrato, the instruments' envelopes and velocity curves, the loop as the sequence loops). A script's AudioStreamPlayer
/// (2D) slot named in <see cref="Fill"/> gets the game's track on the Music bus (<see cref="MusicDb"/>): King Boo's BossMusic = MSD_BGM_BOSS, the
/// boss theme TBossTelesa and TBossPakkun both start (bosstelesa.cpp 2296, bosspakkun.cpp 862: id 0x8001000D = sequence.arc's t_boss.com, 4.2 s +
/// a 95-s loop at tempo 190 -- the decomp's enum names it MSD_BGM_MAP_SELECT, one place off the game's own name table, mSound.asn, whose names these
/// are; 156 shipped the asn's MAP_SELECT, t_select.com, the FILE SELECT music, by that name); the Gatekeeper's = MSD_BGM_CHUBOSS (gatekeeper.cpp 532,
/// the literal 0x8001000B -- the mid-boss theme, 1.2 s + a 56-s loop). The scripts start / stop them at the game's moments already.
/// 155b: the intro's laugh. rouletteFall calls startSoundActor(V_LAUGH1B) EVERY FRAME of the descent, but that id is a one-shot (bit 0x800): the
/// game's JAISeEntry::storeBuffer stops the one already playing on the actor and starts it over, every frame, so the designed 8-syllable chuckle
/// never gets past its first 20-40 ms -- the quiet breath before the first "heh" -- and the descent is, to the ear, the RLT_DOWN rumble alone.
/// 156c: BUT the frame the disc seats, rouletteFall returns before those calls -- so the LAST start, from the frame before, is never stopped and the
/// whole chuckle plays out as the elevator lowers into place (6 s, through the slot machine's fall), before King Boo appears. That is the laugh of
/// the intro. <see cref="IntroLaugh"/> picks that (Game), 155's loop through the descent (Chuckle), or nothing.</summary>
public partial class SmsActorSound : Node3D
{
    // ---- knobs (static: the actors are spawned at runtime, one setting for all of them; the Sfx bus is the volume)
    /// <summary>Off = every actor silent.</summary>
    public static bool Enabled = true;
    /// <summary>The game's camera-distance curve on the actors' sounds (off = flat, like Mario's).</summary>
    public static bool Positional = true;
    /// <summary>MSound::init's JAIGlobalParameter values: full volume within 1200 u of the camera, a straight fade to silence at 12000 u (the
    /// largest of MSHandle's per-category distances; minDistanceVolume 0). 155 had JAudio's defaults (1000 / 5000): too quick a fade.</summary>
    public static float FullVolumeUnits = 1200f, SilentUnits = 12000f;
    public static float MasterDb = 0f;
    /// <summary>The intro's laugh. Game: what the game's engine makes of rouletteFall's per-frame V_LAUGH1B -- through the descent the restarted
    /// laugh's first 20-40 ms, 30 times a second (MSD_SE_BS_TELESA_V_LAUGH1B_ROULETTE, baked from that run of the SE sequence: a breath flutter some
    /// 30 dB under the rumble, in effect silence), then the whole 8-syllable chuckle ONCE as the disc seats (the last start, never stopped). Chuckle:
    /// 155's -- the chuckle looped through the descent, stopped at the seat. Off: the rumble alone, nothing at the seat.</summary>
    public enum IntroLaughs { Game, Chuckle, Off }
    public static IntroLaughs IntroLaugh = IntroLaughs.Game;
    /// <summary>The boss music's level over the Music bus (the renders peak at -1 dBFS; MSBgm plays BGMs at 0.75 of full -- -2.5 dB -- so this sits
    /// them a touch under that, under the effects). Off = the music slots left empty.</summary>
    public static float MusicDb = -4f;
    public static bool Music = true;
    public static bool DebugPrint = false;
    public const string SfxBus = "Sfx";
    public const string MusicBus = "Music";
    public int MusicSlots { get; private set; }
    public const string SoundRoot = "res://assets/Sms/Sound/";

    /// <summary>For the drives: every play, newest last ("tick actor/clip fN what").</summary>
    public static readonly List<string> PlayLog = new();
    public static int PlayCount;
    public static readonly List<SmsActorSound> All = new();

    public string Actor { get; private set; } = "";
    public Node3D OwnerNode => _owner;
    public string CurrentClip => _clip;
    public float CurrentFrame => _frame;
    public float DistanceDb { get; private set; }
    public int SlotsFilled { get; private set; }
    public bool LoopPlaying(string se) => _loops.TryGetValue(se, out var p) && p.Playing;
    public IEnumerable<string> LoopsOn => _loops.Where(kv => kv.Value.Playing).Select(kv => kv.Key);

    private Node3D _owner; private SmsSmallEnemy _enemy;
    private readonly List<AudioStreamPlayer3D> _all = new();           // every player this node made (the curve goes on all of them)
    private readonly AudioStreamPlayer3D[] _se = new AudioStreamPlayer3D[4]; private int _seNext;
    private readonly Dictionary<string, AudioStreamPlayer3D> _loops = new();
    private readonly Dictionary<string, AudioStreamPlayer3D> _held = new();  // the tables' 0x11 .. 0x12 sounds
    private readonly Dictionary<AudioStreamPlayer3D, float> _base = new();
    private readonly List<(string name, AudioStreamPlayer3D p, bool was, float pos)> _slotWatch = new();   // the script's slot plays, logged as they start (or restart)
    private static readonly Dictionary<string, AudioStream> _streams = new();
    private static readonly HashSet<string> _missing = new();
    private string _clip = ""; private float _frame = -1f, _lastFrame = -1f; private int _tick;
    private int _prevRolling = -1; private bool _prevFall;

    // ---- which script is which game actor (by the exact type: SmsDebuTelesa is a Boo of its own)
    private static readonly Dictionary<string, string> ActorOf = new()
    {
        ["SmsBossTelesa"] = "btelesa", ["SmsTelesa"] = "telesa", ["SmsTelesaSlot"] = "slot", ["SmsRoulette"] = "roulette",
        ["SmsGateKeeper"] = "gatekeeper",                                     // 156: its BossMusic slot (no tables: TBiancoGateKeeper's own sounds are a later pass)
        ["SmsKingBooRematch"] = "kingboo",                                    // 157: the rematch's King Boo -- Play(se) by name (the casino boss's laugh); no slot table of his own
        ["SmsBombBoo"] = "bombboo",                                           // 158: the rematch's Bomb Boo -- Play(se) by name (the small Boo's appear / fix / crush / vanish)
    };
    /// <summary>The script's sound slots and the game sound each stands for (the slot names are the species' own exports).</summary>
    private static readonly Dictionary<string, (string slot, string se)[]> Fill = new()
    {
        ["btelesa"] = new[]
        {
            ("LaughSound", "MSD_SE_BS_TELESA_V_LAUGH1"),        // the port's laugh at the laugh clip (the game's bck 6 has no table: a deviation it keeps)
            ("FruitHitSound", "MSD_SE_BS_TELESA_FRUIT_HIT"),    // receiveMessage: a fruit on the body while not spicy
            ("DownSound", "MSD_SE_BS_TELESA_DOWN"),             // TNerveBossTelesaDie at 0 HP
            ("DisappearSound", "MSD_SE_BS_TELESA_DISAPPEAR"),   // damageRecover / forceHide / PrepareSlot's end, not spicy
            ("EscapeSound", "MSD_SE_BS_TELESA_ESCAPE"),         // the same, spicy
            ("ThankYouSound", "MSD_SE_BS_TELESA_THANKYOU"),     // TNerveBossTelesaFreeze: hosed while he waits -> the wet clip
            ("LandSound", "MSD_SE_BS_TELESA_SLT_LAND"),         // TNerveBossTelesaAppear: the appear clip's frame 78 (the slot machine lands)
            ("Fanfare1Sound", "MSD_SE_BS_TELESA_FANFALE_1"),    // fanfale: three fruit
            ("Fanfare2Sound", "MSD_SE_BS_TELESA_FANFALE_2"),    // three coins
            ("Fanfare3Sound", "MSD_SE_BS_TELESA_FANFALE_3"),    // anything else
            ("BossMusic", "MSD_BGM_BOSS"),                      // 156b: the boss theme (t_boss.com; 156 had the file select music by the decomp's shifted name), started at his first appearance (unk384), faded at the down (the script's own lines)
            // not filled: FireSound (the FIRE is a loop; StateTick runs it)
        },
        ["gatekeeper"] = new[] { ("BossMusic", "MSD_BGM_CHUBOSS") },           // 156: TBiancoGateKeeper::startBGM at the appear demo, stopBGM at the finish demo (the script's own lines)
        ["telesa"] = new[]
        {
            ("FixSound", "MSD_SE_EN_TELESA_FIX"),               // changeByJuice: the box Boo into its block
            ("RecoverSound", "MSD_SE_EN_TELSA_RECOVER"),        // changeOut: back from the block
            ("AppearSound", "MSD_SE_EN_TELESA_APPEAR"),         // initAttacker / the fade-in's end
            ("DisappearSound", "MSD_SE_EN_TELESA_DISAPPEAR"),   // the fade-out's end / the attackers killed
            ("HitSound", "MSD_SE_EN_COMMON_W_HIT_OK"),          // TSmallEnemy::receiveMessage SPRAYED_BY_WATER: the common water hit
            // not filled: DownSound (telesa_down's table plays EN_TELESA_FIX at f0), CrushSound (telesa_clush's table: EN_COMMON_BOYOYON), WaitSound (EN_TELESA_FLY is a
            // DSP oscillator voice, bank 0 program 0xF7 -- JASBankMgr::noteOnOsc -- not a wave the baker can render; the Boo's hover stays silent for now)
        },
        ["slot"] = new[] { ("StopSound", "MSD_SE_BS_TELESA_SLT_STOP") },      // a drum stopping; SpinSound is a loop (StateTick)
        ["roulette"] = new[] { ("StopSound", "MSD_SE_BS_TELESA_RLT_STOP") },  // a disc stopped by its switch
    };

    /// <summary>From an actor's _Ready: a sound node under it when its script is one the tables know. Idempotent (an "SmsSound" child already there is kept).</summary>
    public static SmsActorSound Attach(Node3D owner)
    {
        if (owner == null || Engine.IsEditorHint()) return null;
        var had = owner.GetNodeOrNull<SmsActorSound>("SmsSound");
        if (had != null) return had;
        if (!ActorOf.TryGetValue(owner.GetType().Name, out var actor)) return null;
        var n = new SmsActorSound { Name = "SmsSound" };
        n.Actor = actor;
        owner.AddChild(n);
        return n;
    }

    public override void _Ready()
    {
        _owner = GetParent<Node3D>(); _enemy = _owner as SmsSmallEnemy;
        if (_owner == null) return;
        if (string.IsNullOrEmpty(Actor) && ActorOf.TryGetValue(_owner.GetType().Name, out var a)) Actor = a;
        SmsMarioSound.EnsureSfxBus();
        for (int i = 0; i < _se.Length; i++) _se[i] = Make("Se" + i);
        FillSlots();
        All.Add(this);
        SmsActorDriveHook();
        if (DebugPrint) GD.Print($"[SMS actor sound] {_owner.Name} ({Actor}): {SlotsFilled} slots filled");
    }
    public override void _ExitTree() { All.Remove(this); }

    private AudioStreamPlayer3D Make(string name)
    {
        var p = new AudioStreamPlayer3D { Name = name, Bus = SfxBus, MaxPolyphony = 1, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled, AttenuationFilterDb = 0f, MaxDistance = 0f };
        AddChild(p); _all.Add(p); _base[p] = 0f;
        return p;
    }
    /// <summary>The script's empty slots get players under THIS node (adding to the owner inside its own _Ready is refused: "parent busy").</summary>
    private void FillSlots()
    {
        if (!Fill.TryGetValue(Actor, out var list)) return;
        var t = _owner.GetType();
        foreach (var (slot, se) in list)
        {
            var f = t.GetField(slot);
            if (f == null || (f.FieldType != typeof(AudioStreamPlayer3D) && f.FieldType != typeof(AudioStreamPlayer))) { GD.PushWarning($"[SMS actor sound] {t.Name} has no AudioStreamPlayer(3D) field '{slot}'"); continue; }
            if (f.FieldType == typeof(AudioStreamPlayer))
            {
                // 156: a MUSIC slot -- a 2D player on the Music bus with the game's BGM (its WAV loops as the sequence does); set by hand = kept
                if (!Music) continue;
                if (f.GetValue(_owner) is AudioStreamPlayer haveM && haveM.Stream != null) continue;
                var sm = Stream(se); if (sm == null) continue;
                var m = new AudioStreamPlayer { Name = "Music_" + slot, Bus = MusicBus, Stream = sm, VolumeDb = MusicDb + SeDb(se) };
                AddChild(m); f.SetValue(_owner, m); SlotsFilled++; MusicSlots++;
                if (DebugPrint) GD.Print($"[SMS actor sound] {_owner.Name}: {slot} = {se} ({(sm is AudioStreamWav wv ? wv.GetLength().ToString("F1") + " s" : "?")}, bus {MusicBus})");
                continue;
            }
            if (f.GetValue(_owner) is AudioStreamPlayer3D have && have.Stream != null) continue;    // set by hand in the inspector
            var s = Stream(se); if (s == null) continue;
            var p = Make("Slot_" + slot); p.Stream = s; SetBase(p, MasterDb + SeDb(se));
            f.SetValue(_owner, p); SlotsFilled++; _slotWatch.Add((slot, p, false, 0f));
        }
    }

    // ---- the files and the game's levels
    public static AudioStream Stream(string se)
    {
        if (_streams.TryGetValue(se, out var s)) return s;
        string folder = SmsMarioSoundTable.Sounds.TryGetValue(se, out var info) ? info.Folder : "Enemy";
        string path = SoundRoot + folder + "/" + se + ".wav";
        s = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        if (s == null && _missing.Add(se)) GD.PushWarning($"[SMS actor sound] no file for {se} ({path})");
        _streams[se] = s;
        return s;
    }
    private static float SeDb(string se) => SmsMarioSoundTable.Sounds.TryGetValue(se, out var info) && info.Volume > 0 ? Mathf.LinearToDb(info.Volume / 127f) : 0f;
    private void SetBase(AudioStreamPlayer3D p, float db) { _base[p] = db; p.VolumeDb = db + DistanceDb; }
    private void Log(string what)
    {
        PlayCount++;
        if (PlayLog.Count > 400) PlayLog.RemoveAt(0);
        PlayLog.Add($"{_tick} {Actor}/{_clip} f{_frame:F1} {what}");
        if (DebugPrint) GD.Print($"[SMS actor sound] {_owner?.Name} t{_tick} {_clip} f{_frame:F1}: {what}");
    }

    /// <summary>A one-shot on the node's pool (the next free player, else the oldest).</summary>
    public void Play(string se, float extraDb = 0f, float pitch = 1f)
    {
        if (!Enabled) return;
        var s = Stream(se); if (s == null) return;
        AudioStreamPlayer3D p = null;
        for (int i = 0; i < _se.Length; i++) { var c = _se[(_seNext + i) % _se.Length]; if (!c.Playing) { p = c; _seNext = (_seNext + i + 1) % _se.Length; break; } }
        if (p == null) { p = _se[_seNext]; _seNext = (_seNext + 1) % _se.Length; }
        p.Stream = s; SetBase(p, MasterDb + SeDb(se) + extraDb); p.PitchScale = pitch; p.Play();
        Log(se);
    }
    /// <summary>A continuous sound: on while `on`, stopped when not (the game restarts these every frame a state holds; a looping WAV loops its region).</summary>
    public void Loop(string se, bool on, float extraDb = 0f, float pitch = 1f)
    {
        if (!_loops.TryGetValue(se, out var p)) { p = Make("Loop_" + se); _loops[se] = p; }
        if (!on || !Enabled) { if (p.Playing) { p.Stop(); Log("loop-off " + se); } return; }
        var s = Stream(se); if (s == null) return;
        if (!p.Playing) { p.Stream = s; SetBase(p, MasterDb + SeDb(se) + extraDb); p.PitchScale = pitch; p.Play(); Log("loop " + se); }
        else SetBase(p, MasterDb + SeDb(se) + extraDb);
    }
    public void StopAll() { foreach (var p in _all) if (p.Playing) p.Stop(); }
    /// <summary>186: one sound stopped wherever it plays on this node (a one-shot still going, its loop) -- King Boo's intro chuckle cut at his dive.</summary>
    public void Stop(string se)
    {
        var s = Stream(se); if (s == null) return;
        bool any = false;
        foreach (var p in _all) if (p.Playing && p.Stream == s) { p.Stop(); any = true; }
        if (any) Log("stop " + se);
    }
    /// <summary>186, for the drives: the sound is playing on this node now (a one-shot or its loop).</summary>
    public bool IsPlaying(string se)
    {
        var s = Stream(se); if (s == null) return false;
        foreach (var p in _all) if (p.Playing && p.Stream == s) return true;
        return false;
    }

    // ---- the tick
    public override void _PhysicsProcess(double delta)
    {
        if (_owner == null || !IsInstanceValid(_owner)) return;
        _tick++;
        DistanceTick();
        if (_enemy != null) AnimTick();
        StateTick();
        for (int i = 0; i < _slotWatch.Count; i++)
        {
            var (name, p, was, pos) = _slotWatch[i]; bool now = p.Playing; float at = now ? p.GetPlaybackPosition() : 0f;
            if (now && (!was || at < pos - 0.01f)) { PlayCount++; if (PlayLog.Count > 400) PlayLog.RemoveAt(0); PlayLog.Add($"{_tick} {Actor}/{_clip} f{_frame:F1} slot {name}"); if (DebugPrint) GD.Print($"[SMS actor sound] {_owner.Name} t{_tick}: slot {name} plays"); }
            _slotWatch[i] = (name, p, now, at);
        }
    }
    /// <summary>The game's distance volume (JAISound::setDistanceVolumeCommon) for the camera's distance to the actor, on every channel.</summary>
    private void DistanceTick()
    {
        float db = 0f;
        if (Positional)
        {
            var cam = GetViewport()?.GetCamera3D();
            if (cam != null)
            {
                float d = (cam.GlobalPosition - _owner.GlobalPosition).Length() * SmsUnits.UnitsPerMetre;
                if (d >= FullVolumeUnits)
                {
                    float over = d - FullVolumeUnits, range = Mathf.Max(1f, SilentUnits - FullVolumeUnits);
                    float f = over < range ? 1f - over / range : 0f;
                    db = f > 0.0001f ? Mathf.LinearToDb(f) : -80f;
                }
            }
        }
        DistanceDb = db;
        foreach (var p in _all) p.VolumeDb = _base[p] + db;
    }
    /// <summary>MAnmSound::animeLoop over the species' table for the clip the enemy plays: the entries between last tick's frame and this one fire; a
    /// new clip fires its entries up to the frame it is first seen at (setBckAnm starts the clip and its sound cursor at frame 0; the node sees it a
    /// tick in) when it evidently just began, else skips its past; the 0x11 / 0x12 pairs hold a sound between their frames; a clip change stops them.</summary>
    private void AnimTick()
    {
        string clip = _enemy.BckName; float frame = _enemy.BckFrameNow;
        if (clip != _clip)
        {
            foreach (var kv in _held) if (kv.Value.Playing) { kv.Value.Stop(); Log("held-off " + kv.Key); }
            _clip = clip; _frame = frame;
            if (frame <= 6f && SmsActorSoundTable.Clips.TryGetValue(Actor + "/" + clip, out var start))
                foreach (var e in start) if (e.Frame <= frame) Fire(e);
            _lastFrame = frame;
            return;
        }
        _frame = frame;
        if (!SmsActorSoundTable.Clips.TryGetValue(Actor + "/" + clip, out var evs)) { _lastFrame = frame; return; }
        if (frame < _lastFrame - 0.5f)
        {
            foreach (var e in evs) if (e.Frame > _lastFrame) Fire(e);          // the clip wrapped: the tail, then the head
            foreach (var e in evs) if (e.Frame <= frame) Fire(e);
        }
        else foreach (var e in evs) if (e.Frame > _lastFrame && e.Frame <= frame) Fire(e);
        _lastFrame = frame;
    }
    private void Fire(SmsMarioSoundTable.Ev e)
    {
        if (!Enabled || !SmsMarioSoundTable.Sounds.ContainsKey(e.Se)) return;
        float tableDb = Mathf.LinearToDb(Mathf.Max(0.05f, e.Volume / 127f));   // the entry's own volume (0..127) over the sound's
        float db = MasterDb + SeDb(e.Se) + tableDb;
        if ((e.Flags & 0x10) != 0)
        {
            // 0x11 the start of a held sound, 0x12 its end (JAIAnimation: the entry pair shares the id; the sound runs between the two frames)
            if (!_held.TryGetValue(e.Se, out var p)) { p = Make("Held_" + e.Se); _held[e.Se] = p; }
            if ((e.Flags & 0xF) == 1) { var s = Stream(e.Se); if (s == null) return; p.Stream = s; SetBase(p, db); p.PitchScale = e.Pitch; p.Play(); Log("held " + e.Se); }
            else if (p.Playing) { p.Stop(); Log("held-off " + e.Se); }
            return;
        }
        Play(e.Se, tableDb, e.Pitch);
    }
    /// <summary>The continuous sounds the game keeps alive by state, per actor.</summary>
    private void StateTick()
    {
        switch (Actor)
        {
            case "btelesa":
                {
                    var boss = _owner as SmsBossTelesa; if (boss == null) return;
                    Loop("MSD_SE_BS_TELESA_FIRE", boss.BckIndex == SmsBossTelesa.BckSpicy && !boss.IsHidden);       // calcRootMatrix: startSoundActor(FIRE) every frame of clip 12
                    int rolling = 0;
                    for (int i = 0; i < 3; i++) { var r = boss.RouletteAt(i); if (r != null && IsInstanceValid(r) && r.Spinning) rolling++; }   // the discs the boss found (RouletteAt: the exports, else the arena's)
                    for (int n = 1; n <= 3; n++) Loop("MSD_SE_BS_TELESA_RLT_MOVE" + n, rolling == n);               // perform: by getRollingRouletteNum, every frame
                    if (rolling != _prevRolling && DebugPrint) GD.Print($"[SMS actor sound] roulettes rolling: {rolling}");
                    _prevRolling = rolling;
                    bool fall = boss.IntroPhase == 1;                                                                 // rouletteFall: the disc sinking with Mario on it
                    Loop("MSD_SE_BS_TELESA_RLT_DOWN", fall);                                                          // (RLT_SET at the DETACH: SmsBossTelesa's own line plays it through this node)
                    float lp = Mathf.Clamp(boss.IntroLaughPitch, 0.25f, 4f);                                           // 156d: the inspector's pitch / pace for the chuckle (1 = the game's notes)
                    Loop("MSD_SE_BS_TELESA_V_LAUGH1B_ROULETTE", fall && IntroLaugh == IntroLaughs.Game, 0f, lp);        // 155b: the laugh as the engine restarts it every frame (see the class note)
                    Loop("MSD_SE_BS_TELESA_V_LAUGH1B", fall && IntroLaugh == IntroLaughs.Chuckle, 0f, lp);              //       the laugh as designed (155's)
                    if (_prevFall && !fall && IntroLaugh == IntroLaughs.Game) Play("MSD_SE_BS_TELESA_V_LAUGH1B", 0f, lp);   // 156c: the disc seats -> the last of those starts plays out whole: the chuckle
                    _prevFall = fall;
                    break;
                }
            case "slot":
                {
                    var slot = _owner as SmsTelesaSlot; if (slot == null) return;
                    bool spinning = slot.Visible && (slot.SpeedOf(0) != 0f || slot.SpeedOf(1) != 0f || slot.SpeedOf(2) != 0f);
                    Loop("MSD_SE_OBJ_SLOT_SPIN", spinning);                                                           // TTelesaSlot::calcRootMatrix: SLOT_SPIN every other frame a drum rolls
                    break;
                }
        }
    }

    partial void SmsActorDriveHook();
}
