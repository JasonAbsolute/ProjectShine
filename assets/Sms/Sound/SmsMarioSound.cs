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
/// loop. Knobs: the volumes, the floor material (DEVIATION: the game reads it off the floor; the port's floors carry none yet), the pieces.</summary>
public partial class SmsMarioSound : Node3D
{
    public enum FloorKind { Stone, Marble, StoneSand, Soil, Sand, Gravel, Wood, WoodBoard, WoodThin, MetalNet }

    [ExportGroup("Sounds")]
    [Export] public bool SoundOn = true;
    [Export(PropertyHint.Range, "-40,12,0.5")] public float MasterDb = 0f;
    [Export(PropertyHint.Range, "-40,12,0.5")] public float VoiceDb = 0f;
    [Export(PropertyHint.Range, "-40,12,0.5")] public float StepDb = 0f;
    [Export(PropertyHint.Range, "-40,12,0.5")] public float FluddDb = 0f;
    [Export(PropertyHint.Range, "1,60,0.5")] public float MaxDistance = 40f;
    [Export] public bool DebugPrint = false;

    [ExportGroup("Pieces")]
    [Export] public bool AnimSounds = true;
    [Export] public bool JumpVoices = true;
    [Export] public bool LandingCutsVoice = true;        // the game stops the voice as he lands (soundMovement case 1: stopVoice) unless it was a trample
    [Export] public bool ActionSounds = true;            // hip drop, wall hits, damage, slide, water
    [Export] public bool FluddSounds = true;
    [Export] public bool FluddPackClanks = true;         // PO_FRICTION_A/B/C with the steps while he carries FLUDD (the bas entries of category 0)

    [ExportGroup("Deviations")]
    [Export] public FloorKind FloorMaterial = FloorKind.Stone;   // the game: mGroundPlane->mSoundMaterial picks the WALK_* set; here one knob
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

    private Mario _mario;
    private AnimationTree _tree;
    private AnimationPlayer _ap;
    private AudioStreamPlayer3D _voice;
    private readonly AudioStreamPlayer3D[] _se = new AudioStreamPlayer3D[6];
    private int _seNext;
    private readonly Dictionary<string, AudioStreamPlayer3D> _loops = new();
    private readonly Dictionary<string, AudioStream> _streams = new();
    private readonly HashSet<string> _missing = new();
    private readonly Random _rng = new();
    private string _clip = ""; private float _frame = -1f, _lastFrame = -1f;
    private Mario.MarioState _prev; private bool _prevAir, _first = true, _prevOnFloor;
    private int _tick;
    private float _prevVy;
    // FLUDD
    private bool _prevEmit, _prevHover, _prevDash, _prevCharge, _prevRefill; private int _prevNozzle = -1, _prevWater = -1; private float _prevR, _prevHouse = -1f, _prevSwitch = -1f, _prevSwitchSpeed;
    private int _emptyCooldown;

    public override void _Ready()
    {
        _mario = GetParent<Mario>();
        _tree = _mario.GetNodeOrNull<AnimationTree>("AnimationTree");
        _ap = _mario.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        _voice = MakePlayer("Voice");
        for (int i = 0; i < _se.Length; i++) _se[i] = MakePlayer($"Se{i}");
        if (OS.GetEnvironment("SMS_SFX_SMOKE") == "1") SmsSoundDriveHook();
    }
    private AudioStreamPlayer3D MakePlayer(string name)
    {
        var p = new AudioStreamPlayer3D { Name = name, MaxDistance = MaxDistance, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, UnitSize = 10f, Bus = "Master", MaxPolyphony = 1 };
        AddChild(p);
        return p;
    }
    partial void SmsSoundDriveHook();

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
        AudioStreamPlayer3D p = null;
        for (int i = 0; i < _se.Length; i++) { var c = _se[(_seNext + i) % _se.Length]; if (!c.Playing) { p = c; _seNext = (_seNext + i + 1) % _se.Length; break; } }
        if (p == null) { p = _se[_seNext]; _seNext = (_seNext + 1) % _se.Length; }
        p.Stream = s; p.VolumeDb = MasterDb + SeDb(se) + extraDb; p.PitchScale = pitch; p.Play();
        Log(se);
    }
    /// <summary>The voice channel: the game's startMarioVoice -- one voice at a time, a new one replaces the old.</summary>
    public void PlayVoice(string se)
    {
        if (!SoundOn) return;
        var s = Stream(se); if (s == null) return;
        _voice.Stop(); _voice.Stream = s; _voice.VolumeDb = MasterDb + VoiceDb + SeDb(se); _voice.Play();
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
            p = MakePlayer("Loop_" + se); _loops[se] = p;
        }
        if (!on || !SoundOn)
        {
            if (p.Playing) { p.Stop(); if (DebugPrint) GD.Print($"[SMS sound] t{_tick}: loop {se} off"); PlayLog.Add($"{_tick} {_clip} f{_frame:F1} loop-off {se}"); }
            return;
        }
        var s = Stream(se); if (s == null) return;
        p.VolumeDb = MasterDb + SeDb(se) + db;
        if (!p.Playing) { p.Stream = s; p.Play(); Log("loop " + se); }
    }

    // ------------------------------------------------------------------------------------------------------------------------------ the tick
    public override void _PhysicsProcess(double delta)
    {
        if (_mario == null) return;
        _tick++;
        var st = _mario.stateOfMario;
        bool onFloor = _mario.IsOnFloor();
        if (_first) { _prev = st; _prevOnFloor = onFloor; _first = false; }
        if (AnimSounds) AnimTick();
        StateTick(st, onFloor);
        if (FluddSounds) FluddTick(st);
        _prev = st; _prevOnFloor = onFloor; _prevVy = _mario.Velocity.Y;
        SmsSoundDriveTick();
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
    private void AnimTick()
    {
        var (clip, pos) = Current();
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
        if (se.StartsWith("MSD_SE_MV") || se.StartsWith("MSD_SE_MA_VO_")) { if (JumpVoices) PlayVoice(se); return; }
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
        or Mario.MarioState.wallSlide or Mario.MarioState.hurt;
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
                case Mario.MarioState.SpinJump: Voice(VoiceKind.Large); break;          // ROTATE_JUMP -> MV23 (ROLL_JUMP's SE is in ma_jump_rolling's table)
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
                    if (JumpVoices) PlayVoice(_rng.NextDouble() < 0.25 ? "MSD_SE_MA_VO_DAMAGE_SMALL" : Pick("MSD_SE_MV02_DAMAGE_MID_0", 4));
                    break;
                case Mario.MarioState.dead:
                    if (JumpVoices) PlayVoice(_rng.NextDouble() < 0.15 ? "MSD_SE_MA_VO_DEAD" : Pick("MSD_SE_MV08A_DOWN_0", 3));
                    break;
                case Mario.MarioState.swimming:
                    if (IsAir(_prev) || _prev == Mario.MarioState.ledgeFall || !_prevOnFloor)
                        PlaySe(_prevVy < -12f ? "MSD_SE_MA_FALL_IN_WATER_DEP" : "MSD_SE_MA_FALL_IN_WATER_SLW");                         // the fall into water, by how hard
                    break;
                case Mario.MarioState.wallSlide:
                    PlaySe("MSD_SE_MA_SLIP_WALL"); break;
            }
            if (_prev == Mario.MarioState.swimming && (st == Mario.MarioState.singleJump || st == Mario.MarioState.doubleJump))
                PlaySe("MSD_SE_MA_JUMP_FR_WATER_SLW");
            if (_prev == Mario.MarioState.slipping) Loop("MSD_SE_MA_SLIP", false);
        }
        // the landing: soundMovement case 1 -- stopVoice() unless the touch-down was a trample
        if (LandingCutsVoice && IsAir(_prev) && _prev != Mario.MarioState.stomping && IsLanding(st) && st != Mario.MarioState.groundPoundLanding)
            StopVoice("landed");
    }
    private void Held(Mario.MarioState st)
    {
        if (!ActionSounds) return;
        Loop("MSD_SE_MA_SLIP", st == Mario.MarioState.slipping);
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
