using Godot;
using System;
using System.Collections.Generic;

/// <summary>Mario's and FLUDD's sounds, the way the game fires them (pass 154). Three sources, all the game's own:
///  1. The ANIMATION tables (mario.szs's bas files, SmsMarioSoundTable.Clips): TMario::animSound -> MAnmSound::animeLoop fires a sound when the
///     clip's frame crosses an entry -- the footsteps (heel / tip, left / right, per floor material), the FLUDD pack's clanks on the steps
///     (PO_FRICTION, category 0: skipped without FLUDD), the rolls, the slides, the snores, a few voices (category 7 -> the voice channel).
///  2. The STATE-driven ones (MarioSound.cpp soundMovement, MarioMove.cpp setStatusToJumping, MSound::startMarioVoice): the jump voices by jump
///     kind (small / mid / large / special), tired below 3 HP, the "yahoo" cut short by the landing (stopVoice), the hip drop's thud, the wall
///     hits, the damage cries, the slide loop, the water entry.
///  3. FLUDD (TWaterGun::emit, TNozzleTrigger): the spray's water loop (WATER_HI, volume with the pressure) with the nozzle's own hiss
///     (NORMAL_NOZZLE_IMI), the hover's loop, the rocket's charge and launch, the turbo's launch, the empty-tank press, the refill, the nozzle
///     switch, the pack's deploy clicks (HAND_OFF / NOZZLE_OFF / DOWN_OFF on the house progress).
/// The sounds are WAVs baked from the game's banks by tools/projectshine/sound154/sms_audio.py (one file per MSD_SE name, the game's layers
/// and pitches mixed in; a continuous sound carries a loop region). One voice channel (the game's), a small pool for effects, one player per
/// loop -- FLAT by default (154b: no panning, no camera-distance falloff; Positional turns the game's own curve on), never Godot's distance
/// low-pass. Knobs: the volumes, the floor material (DEVIATION: the game reads it off the floor; the port's floors carry none yet), the pieces.</summary>
public partial class SmsMarioSound : Node3D
{
    public enum FloorKind { Stone, Marble, StoneSand, Soil, Sand, Gravel, Wood, WoodBoard, WoodThin, MetalNet }

    [ExportGroup("Sounds")]
    [Export] public bool SoundOn = true;
    /// <summary>154c: the SOUND EFFECTS volume, 0..1 on the "Sfx" audio bus (default_bus_layout.tres; made at runtime if the layout is missing) --
    /// every effect of the port plays through it. 0.5 = -6 dB (Jaysonn: "make it 50% right now its very loud"). A menu slider sets it with
    /// SmsMarioSound.SetSfxVolume(v).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float SfxVolume { get => _sfxVolume; set { _sfxVolume = value; if (IsInsideTree()) ApplySfxVolume(); } }
    private float _sfxVolume = 0.5f;
    public const string SfxBus = "Sfx";
    [Export(PropertyHint.Range, "-40,12,0.5")] public float MasterDb = 0f;
    [Export(PropertyHint.Range, "-40,12,0.5")] public float VoiceDb = 0f;
    [Export(PropertyHint.Range, "-40,12,0.5")] public float StepDb = 0f;
    [Export(PropertyHint.Range, "-40,12,0.5")] public float FluddDb = 0f;
    [Export] public bool DebugPrint = false;

    [ExportGroup("Camera distance")]
    /// <summary>Off (154b, Jaysonn: "for mario we dont want that"): Mario's sounds play flat -- no panning, no falloff, wherever the camera is
    /// (the fixed camera of King Boo's room is 30 m off). On: positional players with Godot's panning and the GAME's distance volume
    /// (JAISound::setDistanceVolumeCommon: full within FullVolumeUnits of the camera, a straight fade to silence at SilentUnits; the voices'
    /// curve 2 stretches the fade 4x -- they all but never fade), and never a low-pass (the game has none).</summary>
    [Export] public bool Positional = false;
    [Export(PropertyHint.Range, "100,5000,10")] public float FullVolumeUnits = 1000f;    // JAIGlobalParameter::maxVolumeDistance
    [Export(PropertyHint.Range, "1000,20000,10")] public float SilentUnits = 5000f;     // JAIGlobalParameter::distanceMax (minDistanceVolume 0)

    [ExportGroup("Pieces")]
    [Export] public bool AnimSounds = true;
    [Export] public bool JumpVoices = true;
    [Export] public bool LandingCutsVoice = true;        // the game stops the voice as he lands (soundMovement case 1: stopVoice) unless it was a trample
    [Export] public bool ActionSounds = true;            // hip drop, wall hits, damage, slide, water
    [Export] public bool FluddSounds = true;
    [Export] public bool FluddPackClanks = true;         // PO_FRICTION_A/B/C with the steps while he carries FLUDD (the bas entries of category 0)
    /// <summary>154c: Mario.cs's own sound slots (FallCry, FireDamageSound, ElectricDamageSound, ExertVoice, PumpSqueakIn / Out, FencePunchSound,
    /// FenceCatchSound, HpRecover, SmsWarpInWipeSound -- each documented with its game SE and played by Mario.cs at the game's moment) get
    /// players with the baked sounds when they are empty. A slot you fill yourself in the inspector is left alone.</summary>
    [Export] public bool FillMarioSlots = true;

    [ExportGroup("Deviations")]
    [Export] public FloorKind FloorMaterial = FloorKind.Stone;   // the game: mGroundPlane->mSoundMaterial picks the WALK_* set; here one knob
    /// <summary>The rollout's whoosh (ma_roll_jump's table: MA_ROPE_ROLL_FULL_A, the game's own for its ROTATE_BROAD_JUMP). Off = the voice alone.</summary>
    [Export] public bool RollWhoosh = true;
    [Export(PropertyHint.Range, "0,1,0.05")] public float VoiceVariantChance = 1f;   // x the game's 0.3 / 0.7 / 0.5 odds of the MA_VO_ voices over the MV ones
    [Export] public bool TiredVoices = true;             // at 2 HP or less the game swaps the jump voices for the tired MV41 set

    [ExportGroup("Files")]
    [Export] public string SoundDir = "res://assets/Sms/Sound/Mario/";

    /// <summary>For the drive: every play, newest last ("t clip f12 MSD_SE_...").</summary>
    public readonly List<string> PlayLog = new();
    public int PlayCount { get; private set; }
    public string LastVoice { get; private set; } = "";
    public bool VoicePlaying => _voice != null && _voice.Playing;
    public bool LoopPlaying(string name) => _loops.TryGetValue(name, out var p) && p.Playing;
    public string CurrentClip => _clip;
    public float CurrentFrame => _frame;
    /// <summary>The camera-distance volume in force (dB; 0 when not Positional), for the drive.</summary>
    public float DistanceDb { get; private set; }
    public float VoiceDistanceDb { get; private set; }

    /// <summary>One channel: a flat AudioStreamPlayer, or (Positional) an AudioStreamPlayer3D with Godot's own falloff and low-pass OFF -- the
    /// node applies the game's distance curve to its volume itself.</summary>
    private sealed class Chan
    {
        public readonly AudioStreamPlayer P2; public readonly AudioStreamPlayer3D P3;
        public float BaseDb; public readonly bool IsVoice;
        public Chan(Node parent, string name, bool positional, bool voice)
        {
            IsVoice = voice;
            if (positional)
            {
                P3 = new AudioStreamPlayer3D { Name = name, Bus = SfxBus, MaxPolyphony = 1, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled, AttenuationFilterDb = 0f, MaxDistance = 0f };
                parent.AddChild(P3);
            }
            else { P2 = new AudioStreamPlayer { Name = name, Bus = SfxBus, MaxPolyphony = 1 }; parent.AddChild(P2); }
        }
        public bool Playing => P3 != null ? P3.Playing : P2.Playing;
        public void Stop() { if (P3 != null) P3.Stop(); else P2.Stop(); }
        public void Play(AudioStream s, float db, float pitch, float distDb)
        {
            BaseDb = db;
            if (P3 != null) { P3.Stream = s; P3.VolumeDb = db + distDb; P3.PitchScale = pitch; P3.Play(); }
            else { P2.Stream = s; P2.VolumeDb = db; P2.PitchScale = pitch; P2.Play(); }
        }
        public void SetDb(float db, float distDb) { BaseDb = db; if (P3 != null) P3.VolumeDb = db + distDb; else P2.VolumeDb = db; }
        public void ApplyDistance(float distDb) { if (P3 != null) P3.VolumeDb = BaseDb + distDb; }
    }

    private Mario _mario;
    private AnimationTree _tree;
    private AnimationPlayer _ap;
    private Chan _voice;
    private readonly Chan[] _se = new Chan[6];
    private int _seNext;
    private readonly Dictionary<string, Chan> _loops = new();
    private readonly Dictionary<string, AudioStream> _streams = new();
    private readonly HashSet<string> _missing = new();
    private readonly Random _rng = new();
    private string _clip = ""; private float _frame = -1f, _lastFrame = -1f;
    private Mario.MarioState _prev; private bool _prevAir, _first = true, _prevOnFloor;
    private int _tick;
    private float _prevVy;
    private int _stateTicks;                 // ticks in the current state (the spin jump's ROLL_JUMP at 10, the thrown wail at 30)
    private bool _prevCap = true, _capKnown;  // SmsWearingCap last tick
    private int _waterWaitIn = -1;           // MSRandPlay's countdown for MA_WATER_WAIT while he treads water
    public int SlotsFilled { get; private set; }
    // FLUDD
    private bool _prevEmit, _prevHover, _prevDash, _prevCharge, _prevRefill; private int _prevNozzle = -1, _prevWater = -1; private float _prevR, _prevHouse = -1f, _prevSwitch = -1f, _prevSwitchSpeed;
    private int _emptyCooldown;

    public override void _Ready()
    {
        _mario = GetParent<Mario>();
        _tree = _mario.GetNodeOrNull<AnimationTree>("AnimationTree");
        _ap = _mario.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        EnsureSfxBus(); ApplySfxVolume();
        _voice = new Chan(this, "Voice", Positional, true);
        for (int i = 0; i < _se.Length; i++) _se[i] = new Chan(this, $"Se{i}", Positional, false);
        if (FillMarioSlots) FillSlots();
        if (OS.GetEnvironment("SMS_SFX_SMOKE") == "1") SmsSoundDriveHook();
    }
    partial void SmsSoundDriveHook();

    // ------------------------------------------------------------------------------------------------------------------------------ the Sfx bus
    /// <summary>The "Sfx" bus: default_bus_layout.tres ships it (with a "Music" bus for later); without the layout it is made here, sent to Master.</summary>
    public static int EnsureSfxBus()
    {
        int idx = AudioServer.GetBusIndex(SfxBus);
        if (idx >= 0) return idx;
        AudioServer.AddBus(); idx = AudioServer.BusCount - 1;
        AudioServer.SetBusName(idx, SfxBus); AudioServer.SetBusSend(idx, "Master");
        return idx;
    }
    /// <summary>The sound effects volume, 0..1 (a menu slider's call): the Sfx bus's volume. Remembered by the node's SfxVolume knob.</summary>
    public static void SetSfxVolume(float linear)
    {
        int idx = EnsureSfxBus();
        AudioServer.SetBusVolumeDb(idx, linear <= 0.0005f ? -80f : Mathf.LinearToDb(Mathf.Clamp(linear, 0f, 1f)));
        AudioServer.SetBusMute(idx, linear <= 0.0005f);
    }
    public static float GetSfxVolume() { int idx = AudioServer.GetBusIndex(SfxBus); return idx < 0 ? 1f : (AudioServer.IsBusMute(idx) ? 0f : Mathf.DbToLinear(AudioServer.GetBusVolumeDb(idx))); }
    private void ApplySfxVolume() => SetSfxVolume(_sfxVolume);

    // ------------------------------------------------------------------------------------------------------------------------------ Mario's slots
    /// <summary>Mario.cs's sound slots, filled with the baked game sounds when empty (their SE ids are the slots' own doc comments). Flat players
    /// on the Sfx bus; Mario.cs plays them at the game's moments (the 2-s fall's cry, the burn, the shock, the take's and throw's grunt, the empty
    /// tank's pump squeaks, the fence's punch and catch, the heal, the warp's wipe).</summary>
    private void FillSlots()
    {
        Fill3D(ref _mario.FallCry, "FallCry", "MSD_SE_MV10B_CRY_JUMP_01", VoiceDb);
        Fill3D(ref _mario.FireDamageSound, "FireDamageSound", "MSD_SE_MA_DAMAGE_FIRE", 0f);
        Fill3D(ref _mario.ElectricDamageSound, "ElectricDamageSound", "MSD_SE_MA_DAMAGE_ELEC", 0f);
        Fill3D(ref _mario.ExertVoice, "ExertVoice", "MSD_SE_MV15_EXERT_INST_01", VoiceDb);
        Fill3D(ref _mario.PumpSqueakIn, "PumpSqueakIn", "MSD_SE_PO_POMPING1", FluddDb);
        Fill3D(ref _mario.PumpSqueakOut, "PumpSqueakOut", "MSD_SE_PO_POMPING2", FluddDb);
        Fill3D(ref _mario.FencePunchSound, "FencePunchSound", "MSD_SE_MA_FENCE_PUNCH", 0f);
        Fill3D(ref _mario.FenceCatchSound, "FenceCatchSound", "MSD_SE_MA_FENCE_CATCH", 0f);
        Fill2D(ref _mario.HpRecover, "HpRecover", "MSD_SE_SY_HP_RECOVER", 0f);
        Fill2D(ref _mario.SmsWarpInWipeSound, "SmsWarpInWipeSound", "MSD_SE_MA_INTO_DOKAN", 0f);
        if (DebugPrint) GD.Print($"[SMS sound] {SlotsFilled} of Mario's sound slots filled");
    }
    private void Fill3D(ref AudioStreamPlayer3D slot, string name, string se, float db)
    {
        if (slot != null) return;
        var s = Stream(se); if (s == null) return;
        slot = new AudioStreamPlayer3D { Name = "Slot_" + name, Bus = SfxBus, MaxPolyphony = 1, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled, AttenuationFilterDb = 0f, MaxDistance = 0f, Stream = s, VolumeDb = MasterDb + SeDb(se) + db };
        _mario.AddChild(slot); SlotsFilled++;
    }
    private void Fill2D(ref AudioStreamPlayer slot, string name, string se, float db)
    {
        if (slot != null) return;
        var s = Stream(se); if (s == null) return;
        slot = new AudioStreamPlayer { Name = "Slot_" + name, Bus = SfxBus, MaxPolyphony = 1, Stream = s, VolumeDb = MasterDb + SeDb(se) + db };
        _mario.AddChild(slot); SlotsFilled++;
    }
    /// <summary>The TAKE (Mario.SmsCarryTo with a facing: King Boo's ride, the gates): MARIO_STATUS_TAKEN's voice, MV06_DAMAGE_WATER (soundMovement).</summary>
    public void OnTaken() { if (JumpVoices) PlayVoice(Pick("MSD_SE_MV06_DAMAGE_WATER_0", 3)); }

    /// <summary>JAISound::setDistanceVolumeCommon, curve 0 for the effects and 2 for the voices (the fade's range x 4): the volume factor at
    /// the camera's distance from Mario, as dB. Only when Positional; 0 otherwise.</summary>
    private void DistanceTick()
    {
        if (!Positional) { DistanceDb = 0f; VoiceDistanceDb = 0f; return; }
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null || _mario == null) { DistanceDb = 0f; VoiceDistanceDb = 0f; return; }
        float d = (cam.GlobalPosition - _mario.GlobalPosition).Length() * SmsUnits.UnitsPerMetre;
        DistanceDb = CurveDb(d, 1f); VoiceDistanceDb = CurveDb(d, 4f);
        foreach (var c in _se) c.ApplyDistance(DistanceDb);
        foreach (var c in _loops.Values) c.ApplyDistance(DistanceDb);
        _voice.ApplyDistance(VoiceDistanceDb);
    }
    private float CurveDb(float distUnits, float rangeScale)
    {
        if (distUnits < FullVolumeUnits) return 0f;
        float over = distUnits - FullVolumeUnits, range = Mathf.Max(1f, SilentUnits - FullVolumeUnits) * rangeScale;
        float f = over < range ? 1f - over / range : 0f;
        return f > 0.0001f ? Mathf.LinearToDb(f) : -80f;
    }

    // ------------------------------------------------------------------------------------------------------------------------------ the files
    private AudioStream Stream(string se)
    {
        if (_streams.TryGetValue(se, out var s)) return s;
        string path = SoundDir + se + ".wav";
        s = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        if (s == null && _missing.Add(se)) GD.PushWarning($"[SMS sound] no file for {se} ({path})");
        _streams[se] = s;
        return s;
    }
    private static float SeDb(string se)
    {
        // the sound table's volume (0..127) as the game applies it: volume / 127 -> amplitude
        if (SmsMarioSoundTable.Sounds.TryGetValue(se, out var info) && info.Volume > 0) return Mathf.LinearToDb(info.Volume / 127f);
        return 0f;
    }
    private void Log(string what)
    {
        PlayCount++;
        if (PlayLog.Count > 400) PlayLog.RemoveAt(0);
        PlayLog.Add($"{_tick} {_clip} f{_frame:F1} {what}");
        if (DebugPrint) GD.Print($"[SMS sound] t{_tick} {_clip} f{_frame:F1}: {what}");
    }

    /// <summary>A one-shot effect on the pool (the next free player, else the oldest).</summary>
    public void PlaySe(string se, float extraDb = 0f, float pitch = 1f)
    {
        if (!SoundOn) return;
        var s = Stream(se); if (s == null) return;
        Chan p = null;
        for (int i = 0; i < _se.Length; i++) { var c = _se[(_seNext + i) % _se.Length]; if (!c.Playing) { p = c; _seNext = (_seNext + i + 1) % _se.Length; break; } }
        if (p == null) { p = _se[_seNext]; _seNext = (_seNext + 1) % _se.Length; }
        p.Play(s, MasterDb + SeDb(se) + extraDb, pitch, DistanceDb);
        Log(se);
    }
    /// <summary>The voice channel: the game's startMarioVoice -- one voice at a time, a new one replaces the old.</summary>
    public void PlayVoice(string se)
    {
        if (!SoundOn) return;
        var s = Stream(se); if (s == null) return;
        _voice.Stop(); _voice.Play(s, MasterDb + VoiceDb + SeDb(se), 1f, VoiceDistanceDb);
        LastVoice = se; Log("voice " + se);
    }
    public void StopVoice(string why = "")
    {
        if (_voice != null && _voice.Playing) { _voice.Stop(); if (DebugPrint) GD.Print($"[SMS sound] t{_tick}: voice stopped ({why})"); PlayLog.Add($"{_tick} {_clip} f{_frame:F1} voice-stop {why}"); }
    }
    /// <summary>A continuous sound (the spray, the hover, the slide...): on while `on`, stopped when not; a looping WAV loops its region.</summary>
    private void Loop(string se, bool on, float db = 0f)
    {
        if (!_loops.TryGetValue(se, out var p))
        {
            p = new Chan(this, "Loop_" + se, Positional, false); _loops[se] = p;
        }
        if (!on || !SoundOn)
        {
            if (p.Playing) { p.Stop(); if (DebugPrint) GD.Print($"[SMS sound] t{_tick}: loop {se} off"); PlayLog.Add($"{_tick} {_clip} f{_frame:F1} loop-off {se}"); }
            return;
        }
        var s = Stream(se); if (s == null) return;
        if (!p.Playing) { p.Play(s, MasterDb + SeDb(se) + db, 1f, DistanceDb); Log("loop " + se); }
        else p.SetDb(MasterDb + SeDb(se) + db, DistanceDb);
    }

    // ------------------------------------------------------------------------------------------------------------------------------ the tick
    public override void _PhysicsProcess(double delta)
    {
        if (_mario == null) return;
        _tick++;
        var st = _mario.stateOfMario;
        bool onFloor = _mario.IsOnFloor();
        if (_first) { _prev = st; _prevOnFloor = onFloor; _first = false; }
        DistanceTick();
        _stateTicks = st == _prev ? _stateTicks + 1 : 0;
        if (AnimSounds) AnimTick();
        StateTick(st, onFloor);
        if (ActionSounds) CapTick();
        if (FluddSounds) FluddTick(st);
        _prev = st; _prevOnFloor = onFloor; _prevVy = _mario.Velocity.Y;
        SmsSoundDriveTick();
    }
    /// <summary>soundMovement's cap: the cap leaving his head -> MA_HAT_STOLEN; back on -> MA_TAKE_HAT.</summary>
    private void CapTick()
    {
        bool cap = _mario.SmsWearingCap;
        if (!_capKnown) { _prevCap = cap; _capKnown = true; return; }
        if (_prevCap && !cap) PlaySe("MSD_SE_MA_HAT_STOLEN");
        else if (!_prevCap && cap) PlaySe("MSD_SE_MA_TAKE_HAT");
        _prevCap = cap;
    }
    partial void SmsSoundDriveTick();

    // ---- 1. the animation tables
    private (string clip, float pos) Current()
    {
        if (_tree != null && _tree.Active)
        {
            var pb = _tree.Get("parameters/BaseSM/playback").As<AnimationNodeStateMachinePlayback>();
            if (pb != null)
            {
                string node = pb.GetCurrentNode();
                float pos = (float)pb.GetCurrentPlayPosition();
                if (node == "ma_runblend")
                {
                    // the gait blend (Blend2 of ma_run1 / ma_run2): the clip whose weight leads
                    float b = (float)_tree.Get("parameters/BaseSM/ma_runblend/mix/blend_amount").AsDouble();   // Mario.cs's SMS_RUN_MIX_PATH: 0 run1 .. 1 run2
                    node = b < 0.5f ? "ma_run1" : "ma_run2";
                }
                return (node, pos);
            }
        }
        if (_ap != null && _ap.IsPlaying()) return (_ap.CurrentAnimation, (float)_ap.CurrentAnimationPosition);
        return ("", 0f);
    }
    /// <summary>Clips the port plays in another clip's role: their sound tables. The port's triple jump is the gate-out somersault (no table of
    /// its own); the game's ULTRA_JUMP is ANIM_ROLL (jumpingBasic(ULTRA_JUMP_SLIP, ANIM_ROLL)): its ROLL_C whoosh.</summary>
    private static readonly Dictionary<string, string> ClipAlias = new() { ["ma_demo_gate_out_rolling_get"] = "ma_roll" };
    private void AnimTick()
    {
        var (clip, pos) = Current();
        if (ClipAlias.TryGetValue(clip, out var alias)) clip = alias;
        float frame = pos * SmsMarioSoundTable.BckFps;
        if (clip != _clip)
        {
            // a new clip: MAnmSound::initAnmSound(frame) -- the entries before this frame are skipped, nothing fires for the past
            _clip = clip; _lastFrame = frame - 0.001f; _frame = frame;
            return;
        }
        _frame = frame;
        if (clip == "" || !SmsMarioSoundTable.Clips.TryGetValue(clip, out var evs)) { _lastFrame = frame; return; }
        if (frame < _lastFrame - 0.5f)
        {
            // the clip wrapped: the entries after the last frame, then the ones up to here
            foreach (var e in evs) if (e.Frame > _lastFrame) Fire(e);
            foreach (var e in evs) if (e.Frame <= frame) Fire(e);
        }
        else foreach (var e in evs) if (e.Frame > _lastFrame && e.Frame <= frame) Fire(e);
        _lastFrame = frame;
    }
    private void Fire(SmsMarioSoundTable.Ev e)
    {
        string se = e.Se;
        if (!SmsMarioSoundTable.Sounds.ContainsKey(se)) return;
        if (se.StartsWith("MSD_SE_PO_"))
        {
            // category 0 (POMP): MAnmSound::startAnimSound skips it when the actor's ground number carries 0x1000 -- no FLUDD
            if (!FluddPackClanks || !_mario.SmsFluddSound.HasFludd) return;
            PlaySe(se, FluddDb + Mathf.LinearToDb(Mathf.Max(0.05f, e.Volume / 127f)));
            return;
        }
        if (se.StartsWith("MSD_SE_MA_WALK_STONE_")) se = Footstep(se);
        if (se == "MSD_SE_MA_TAKE_BARREL")
        {
            // the tables say TAKE_BARREL for every pick-up and put-down; soundMovement's TAKE / PUTTING pick the fruit's DRIAN sounds by the held type
            bool fruit = _mario.SmsHoldsFruit;
            se = _clip == "ma_put" ? (fruit ? "MSD_SE_MA_PUT_DRIAN" : "MSD_SE_MA_PUT_BARREL") : (fruit ? "MSD_SE_MA_TAKE_DRIAN" : "MSD_SE_MA_TAKE_BARREL");
        }
        if (se.StartsWith("MSD_SE_MV") || se.StartsWith("MSD_SE_MA_VO_")) { if (JumpVoices) PlayVoice(se); return; }
        if (!RollWhoosh && se.StartsWith("MSD_SE_MA_ROLL") && _clip == "ma_roll_jump") return;
        float db = Mathf.LinearToDb(Mathf.Max(0.05f, e.Volume / 127f));
        if (se.Contains("_WALK_")) db += StepDb;
        PlaySe(se, db);
    }
    /// <summary>The game adds the floor's sound material to the footstep id (JAISeSwBit_GroundVariant); here the knob picks the set.</summary>
    private string Footstep(string stone)
    {
        // stone names: MSD_SE_MA_WALK_STONE_{L,R}_{HEEL,TIP}
        bool left = stone.Contains("_L_"); bool heel = stone.EndsWith("HEEL");
        string lr = left ? "L" : "R", ht = heel ? "H" : "T";
        switch (FloorMaterial)
        {
            case FloorKind.Marble: return $"MSD_SE_MA_WALK_MARBLE_{lr}_{ht}";
            case FloorKind.StoneSand: return $"MSD_SE_MA_WALK_STN_SND_{lr}{ht}";
            case FloorKind.Soil: return $"MSD_SE_MA_WALK_SOIL_{lr}{ht}";
            case FloorKind.Sand: return $"MSD_SE_MA_WALK_SAND_{lr}";
            case FloorKind.Gravel: return $"MSD_SE_MA_WALK_GRAVEL_{lr}";
            case FloorKind.Wood: return $"MSD_SE_MA_WALK_WOOD_{lr}{ht}";
            case FloorKind.WoodBoard: return $"MSD_SE_MA_WALK_WOODBOARD_{lr}{ht}";
            case FloorKind.WoodThin: return $"MSD_SE_MA_WALK_WOOD_THN_{lr}{ht}1";
            case FloorKind.MetalNet: return $"MSD_SE_MA_WALK_METALNET_{lr}{ht}1";
            default: return stone;
        }
    }

    // ---- 2. the state-driven sounds
    private static bool IsAir(Mario.MarioState s) => s is Mario.MarioState.singleJump or Mario.MarioState.doubleJump or Mario.MarioState.tripleJump
        or Mario.MarioState.backFlip or Mario.MarioState.sideFlip or Mario.MarioState.wallJump or Mario.MarioState.SpinJump or Mario.MarioState.ledgeFall
        or Mario.MarioState.stomping or Mario.MarioState.fluddRocket or Mario.MarioState.diving or Mario.MarioState.singleJumpDive
        or Mario.MarioState.doubleJumpDive or Mario.MarioState.tripleJumpDive or Mario.MarioState.groundPoundFalling or Mario.MarioState.groundPoundStartup
        or Mario.MarioState.wallSlide or Mario.MarioState.hurt or Mario.MarioState.singleRollout or Mario.MarioState.bellyRollout;
    private static bool IsLanding(Mario.MarioState s) => s is Mario.MarioState.landing or Mario.MarioState.singleJumpLanding or Mario.MarioState.doubleJumpLanding
        or Mario.MarioState.tripleJumpLanding or Mario.MarioState.idle or Mario.MarioState.walking or Mario.MarioState.running or Mario.MarioState.sprinting
        or Mario.MarioState.crouch or Mario.MarioState.bellySlidingFromDive or Mario.MarioState.singleRollout or Mario.MarioState.bellyRollout or Mario.MarioState.rolloutRun;

    private void StateTick(Mario.MarioState st, bool onFloor)
    {
        if (st == _prev) { Held(st); return; }
        // entering a jump: setStatusToJumping's voice by kind (MarioMove.cpp 440-660)
        if (JumpVoices)
        {
            switch (st)
            {
                case Mario.MarioState.singleJump: Voice(VoiceKind.Small); break;        // MARIO_STATUS_JUMP -> MV21 (MV19 from deep water)
                case Mario.MarioState.doubleJump: Voice(VoiceKind.Mid); break;          // SECOND_JUMP -> MV22
                case Mario.MarioState.tripleJump: Voice(VoiceKind.Large); break;        // ULTRA_JUMP -> MV23
                case Mario.MarioState.backFlip: Voice(VoiceKind.Large); break;          // BACK_JUMP -> MV23
                case Mario.MarioState.sideFlip: Voice(VoiceKind.Large); break;          // U_TURN_JUMP -> MV23
                case Mario.MarioState.wallJump: Voice(VoiceKind.Mid); break;            // WALL_JUMP -> MV22
                case Mario.MarioState.SpinJump: Voice(VoiceKind.Special); break;        // ROTATE_JUMP: setStatusToJumping's MV23, then soundMovement's MV24 the same frame -- MV24 is what plays
                case Mario.MarioState.singleRollout: Voice(VoiceKind.Mid); break;       // the belly slide's A at speed = ROTATE_BROAD_JUMP (ma_roll_jump) -> MV22; ma_roll_jump's table adds the roll's whoosh
                case Mario.MarioState.stomping: Voice(VoiceKind.Small); break;          // TRAMPLE -> MV21 / 22 / 23 by the step
                case Mario.MarioState.fluddRocket:
                    {
                        int noz = _mario.SmsFluddSound.Nozzle;                             // ROCKET: Rocket -> MV24, Turbo -> MV15, Hover -> MV21
                        if (noz == 2) Voice(VoiceKind.Special); else if (noz == 3) PlayVoice(Tired() ? "MSD_SE_MV38_EXERT_INST_T_01" : "MSD_SE_MV15_EXERT_INST_01"); else Voice(VoiceKind.Small);
                        break;
                    }
                case Mario.MarioState.ledgeHang: PlayVoice(Tired() ? "MSD_SE_MV34_ACTION_T_02" : "MSD_SE_MV15_EXERT_INST_02"); break;   // HANGING from a jump
                case Mario.MarioState.treeGrab: PlayVoice(Tired() ? "MSD_SE_MV34_ACTION_T_01" : "MSD_SE_MV13_ACTION_SMALL_01"); break;  // JUMP_CATCH
            }
        }
        if (ActionSounds)
        {
            switch (st)
            {
                case Mario.MarioState.groundPoundLanding:
                    PlaySe("MSD_SE_MA_HIP_ATTACK"); break;                                                  // HIP_DROP -> HIP_ATTACK_END
                case Mario.MarioState.bonk:
                    PlaySe("MSD_SE_MA_WALL_COL_HEAD");                                                        // the wall hit's down: WALL_COL_HEAD + the little damage cry
                    if (JumpVoices) PlayVoice(_rng.NextDouble() < 0.25 ? "MSD_SE_MA_VO_DAMAGE_SMALL" : Pick("MSD_SE_MV03_DAMAGE_LITLE_0", 4));
                    break;
                case Mario.MarioState.hurt:
                    {
                        // soundMovement's down: an actor's hit -> WALL_COL_CMN_H and MV03 (25% the VO grunt); the burn's MV05 and the shock's MV04
                        // (Mario.cs's own slots play MA_DAMAGE_FIRE / MA_DAMAGE_ELEC); the throw's wail comes 30 ticks in (Held)
                        string kind = _mario.SmsDownKindName;
                        if (kind == "Fire") { if (JumpVoices) PlayVoice(Pick("MSD_SE_MV05_DAMAGE_FIRE_0", 2)); }
                        else if (kind == "Electric") { if (JumpVoices) PlayVoice(Pick("MSD_SE_MV04_DAMAGE_ELEC_0", 2)); }
                        else if (kind == "Thrown" || kind == "ThrownEnd" || kind == "FireEnd") { }
                        else
                        {
                            PlaySe("MSD_SE_MA_WALL_COL_CMN_H");
                            if (JumpVoices) PlayVoice(_rng.NextDouble() < 0.25 ? "MSD_SE_MA_VO_DAMAGE_SMALL" : Pick("MSD_SE_MV03_DAMAGE_LITLE_0", 4));
                        }
                        break;
                    }
                case Mario.MarioState.dead:
                    if (JumpVoices) PlayVoice(_rng.NextDouble() < 0.15 ? "MSD_SE_MA_VO_DEAD" : Pick("MSD_SE_MV08A_DOWN_0", 3));
                    break;
                case Mario.MarioState.swimming:
                    if (IsAir(_prev) || _prev == Mario.MarioState.ledgeFall || !_prevOnFloor)
                        PlaySe(_prevVy < -12f ? "MSD_SE_MA_FALL_IN_WATER_DEP" : "MSD_SE_MA_FALL_IN_WATER_SLW");                         // the fall into water, by how hard
                    break;
            }
            if (_prev == Mario.MarioState.swimming && (st == Mario.MarioState.singleJump || st == Mario.MarioState.doubleJump))
                PlaySe("MSD_SE_MA_JUMP_FR_WATER_SLW");
            Held(st);
        }
        // the landing: soundMovement case 1 -- stopVoice() unless the touch-down was a trample
        if (LandingCutsVoice && IsAir(_prev) && _prev != Mario.MarioState.stomping && IsLanding(st) && st != Mario.MarioState.groundPoundLanding)
            StopVoice("landed");
    }
    /// <summary>Every tick's continuous sounds and timed ones: the slides' MA_SLIP (slippingBasic / catching: the slope slip and the dive's belly
    /// slide), the wall slide's SLIP_WALL (wallSlipEffect every frame the slide falls), the spin jump's ROLL_JUMP at its 10th tick, the throw's
    /// MV25A wail at its 30th, the water wait's MSRandPlay (5..60 frames apart) while he treads water.</summary>
    private void Held(Mario.MarioState st)
    {
        if (!ActionSounds) return;
        Loop("MSD_SE_MA_SLIP", st == Mario.MarioState.slipping || st == Mario.MarioState.bellySlidingFromDive);
        Loop("MSD_SE_MA_SLIP_WALL", st == Mario.MarioState.wallSlide);
        if (st == Mario.MarioState.SpinJump && _stateTicks == 10) PlaySe("MSD_SE_MA_ROLL_JUMP");
        if (st == Mario.MarioState.hurt && _stateTicks == 30 && _mario.SmsDownKindName == "Thrown" && JumpVoices)
            PlayVoice(Tired() ? Pick("MSD_SE_MV42_JUMP_HUGE_T_0", 2) : Pick("MSD_SE_MV25A_JUMP_HUGE_0", 2));
        bool treading = st == Mario.MarioState.swimming && _clip == "ma_swim_wait";
        if (treading)
        {
            if (_waterWaitIn < 0) _waterWaitIn = 10 + _rng.Next(111);
            else if (--_waterWaitIn == 0) { PlaySe("MSD_SE_MA_WATER_WAIT"); _waterWaitIn = 10 + _rng.Next(111); }
        }
        else _waterWaitIn = -1;
    }

    private enum VoiceKind { Small, Mid, Large, Special }
    private bool Tired() => TiredVoices && _mario.SmsHP <= 2;
    private string Pick(string prefix, int count) => $"{prefix}{1 + _rng.Next(count)}";
    /// <summary>MSound::startMarioVoice: below 3 HP the tired MV41 set; else the MA_VO_ variant with the game's odds (0.3 / 0.7 / 0.5 / 0.5), else
    /// the MV family (its numbered takes picked at random, as the SE sequence does).</summary>
    private void Voice(VoiceKind kind)
    {
        if (Tired()) { PlayVoice(Pick("MSD_SE_MV41_JUMP_T_0", 3)); return; }
        double r = _rng.NextDouble() / Mathf.Max(0.0001f, VoiceVariantChance);
        switch (kind)
        {
            case VoiceKind.Small: PlayVoice(r < 0.3 ? $"MSD_SE_MA_VO_JUMP_SMALL_{_rng.Next(3)}" : Pick("MSD_SE_MV21_JUMP_SMALL_0", 5)); break;
            case VoiceKind.Mid: PlayVoice(r < 0.7 ? $"MSD_SE_MA_VO_JUMP_MID_{_rng.Next(4)}" : "MSD_SE_MV22_JUMP_MID_01"); break;
            case VoiceKind.Large: PlayVoice(r < 0.5 ? $"MSD_SE_MA_VO_JUMP_BIG_{_rng.Next(3)}" : Pick("MSD_SE_MV23_JUMP_LARGE_0", 3)); break;
            case VoiceKind.Special: PlayVoice(r < 0.5 ? $"MSD_SE_MA_VO_JUMP_SPECIAL_{_rng.Next(4)}" : Pick("MSD_SE_MV24_JUMP_SPECIAL_0", 6)); break;
        }
    }

    // ---- 3. FLUDD (TWaterGun::emit / TNozzleTrigger / soundMovement's house progress)
    private void FluddTick(Mario.MarioState st)
    {
        var f = _mario.SmsFluddSound;
        if (!f.HasFludd) { Loop("MSD_SE_PO_WATER_HI", false); Loop("MSD_SE_PO_NORMAL_NOZZLE_IMI", false); Loop("MSD_SE_PO_HOVER", false); return; }
        bool spraying = f.Emitting && f.Nozzle == 0 && f.Water > 0;
        bool turbo = f.Emitting && f.Nozzle == 3 && f.Water > 0;
        bool hover = f.Emitting && f.Nozzle == 1 && f.Water > 0;
        // Spray: NORMAL_NOZZLE_IMI (the nozzle's own hiss, with the pressure) and WATER_HI (the water, with the strength); Turbo: WATER_HI
        float strengthDb = Mathf.LinearToDb(Mathf.Clamp(0.35f + 0.65f * f.Strength, 0.05f, 1f));
        Loop("MSD_SE_PO_WATER_HI", spraying || turbo, FluddDb + strengthDb);
        Loop("MSD_SE_PO_NORMAL_NOZZLE_IMI", spraying, FluddDb - 6f + strengthDb);
        Loop("MSD_SE_PO_HOVER", hover, FluddDb);
        // the trigger nozzles: SY_NEWP_AIR_TAME while the charge builds (R held, INACTIVE), the nozzle's trigger sound at the launch
        bool charging = f.Nozzle == 2 && f.TrigState == 0 && f.R > 0f && f.Water > 0 && st != Mario.MarioState.fluddRocket;
        Loop("MSD_SE_SY_NEWP_AIR_TAME", charging, FluddDb);
        if (st == Mario.MarioState.fluddRocket && _prev != Mario.MarioState.fluddRocket && f.Nozzle == 2) PlaySe("MSD_SE_PO_ROCKET_TRIGGER", FluddDb);
        if (f.Dashing && !_prevDash) PlaySe("MSD_SE_PO_LAUNCH_TRIGGER", FluddDb);
        // the empty tank: R on the spray with no water (soundMovement: (mInput & 0x100) && getCurrentWater() == 0)
        if (_emptyCooldown > 0) _emptyCooldown--;
        if (f.R > 0f && _prevR <= 0f && f.Water <= 0 && _emptyCooldown == 0) { PlaySe("MSD_SE_PO_ACTION_ON_EMPTY", FluddDb); _emptyCooldown = 30; }
        // the refill: SUCK_WATER_B while the tank takes water in (TWaterGun::suckWater)
        bool refill = _prevWater >= 0 && f.Water > _prevWater && f.Water < f.WaterMax;
        Loop("MSD_SE_PO_SUCK_WATER_B", refill, FluddDb);
        // the nozzle switch: SY_SELECT_POMP as the change to the second nozzle starts, _BACK as the change back starts
        if (f.SwitchSpeed != 0f && _prevSwitchSpeed == 0f) PlaySe(f.SwitchSpeed > 0f ? "MSD_SE_SY_SELECT_POMP" : "MSD_SE_SY_SELECT_POMP_BACK", FluddDb);
        // the pack's deploy: the house progress (0 housed .. 100 deployed) crossing 0 -> HAND_OFF, 18 -> 19 NOZZLE_OFF (spray only), 49 -> 50 DOWN_OFF
        float house = (1f - f.Stow) * 100f;
        if (_prevHouse >= 0f)
        {
            if (_prevHouse <= 0f && house > 0f) PlaySe("MSD_SE_PO_HAND_OFF", FluddDb);
            if (_prevHouse < 19f && house >= 19f && f.Nozzle == 0) PlaySe("MSD_SE_PO_NOZZLE_OFF", FluddDb);
            if (_prevHouse < 50f && house >= 50f) PlaySe("MSD_SE_PO_DOWN_OFF", FluddDb);
        }
        _prevHouse = house; _prevR = f.R; _prevWater = f.Water; _prevDash = f.Dashing; _prevSwitchSpeed = f.SwitchSpeed; _prevNozzle = f.Nozzle;
    }
}
