using Godot;
using System;
using System.Linq;

/// <summary>
/// SMS pass 209: the arena's LIGHTS FOLLOW THE SKY (Jaysonn: "the lighting that follows the sky and the lightning flashes"). Until now the arena's light was
/// fixed -- a violet ambient, a thin violet fog, a cool blue-white sun -- under a sky that flashes and, in the final phase, burns: the floor stayed cold
/// blue-grey under the fire (208c's shots). This node, every frame:
///  * runs the SKY's CLOCK: it sets the sky material's sky_time (RematchSky.gdshader reads it in place of its own TIME while it is set), so the sky and the
///    lights below share ONE clock and a flash lands on the same frame in both;
///  * the FLASHES: Luigi's Mansion's boss-arena sky lights its clouds purple (frame 2 of its 300-frame cycle), grey (12), blue (118), green (154), lavender
///    (167) and dark red (237) -- the same colour keys as the shader's (the brk's) and the same pulse (up over a few frames, down over a second or two) --
///    and the cloud colour of the moment is ADDED to the ambient colour (FlashStrength x; 1 = as the sky shows it) and, fainter, to the sun's colour
///    (FlashOnSun x that): the lightning lights the ground. The sky's own flash_amount scales both (0 there = no flashes anywhere);
///  * the FINAL PHASE: as the sky goes over to the roof fight's fire (the material's final_blend, which King Boo drives through the second split's cutscene),
///    the ambient, the fog colour and the sun blend from the arena's own values (read off the Environment and the sun at start: your inspector values stay
///    the base) to FinalAmbient / FinalFog / FinalSun, and the flashes fade out with the old sky (the fire sky has none);
///  * 217: the PEACEFUL NIGHT after King Boo's defeat (the material's peace_blend, which he drives from his burst): from wherever they were to PeaceAmbient /
///    PeaceFog / PeaceSun, the flashes and the thunder gone with the stormy sky.
/// The Environment is duplicated at start so nothing here is written into the saved resource. ProcessMode Always: it runs through the split cutscenes'
/// frozen world as the sky itself does. Knobs: FollowSky (off = the lights as they were and the sky on its own clock), FlashStrength, FlashOnSun, the Final
/// colours and energies. Readouts for the drives: SkyTime, FlashNow, FlashColorNow, FinalBlendRead, the Base values, MaterialFound.
/// </summary>
[GlobalClass]
public partial class SmsArenaLighting : Node
{
    [ExportGroup("The lights follow the sky (209)")]
    /// <summary>Off = the ambient, the fog and the sun stay as set on them, the sky runs on its own clock, no flashes below.</summary>
    [Export] public bool FollowSky = true;
    /// <summary>The WorldEnvironment and the sun this node drives (siblings by default).</summary>
    [Export] public NodePath EnvironmentPath = "../WorldEnvironment";
    [Export] public NodePath SunPath = "../DirectionalLight3D";
    /// <summary>How hard the sky's flashes hit the ground: the cloud colour of the moment x this, added to the ambient colour (1 = the colour as the sky shows it; 0 = the ground never flashes).</summary>
    [Export(PropertyHint.Range, "0,4,0.05")] public float FlashStrength = 1.5f;
    /// <summary>The sun's share of a flash (x FlashStrength): the flash colours the sunlight too, so lit sides and shadows read differently under it.</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float FlashOnSun = 0.5f;

    [ExportGroup("The final phase's light (209)")]
    /// <summary>What the ambient, the fog and the sun become under the fire sky (blended in with the sky's own fade). The arena's own values are the other end.</summary>
    [Export] public Color FinalAmbient = new(0.62f, 0.26f, 0.1f);
    [Export(PropertyHint.Range, "0,4,0.05")] public float FinalAmbientEnergy = 0.9f;
    [Export] public Color FinalFog = new(0.18f, 0.035f, 0.01f);
    [Export] public Color FinalSun = new(1f, 0.6f, 0.3f);
    [Export(PropertyHint.Range, "0,4,0.05")] public float FinalSunEnergy = 0.55f;
    [ExportGroup("The peaceful night's light (217)")]
    /// <summary>217: what they become as the sky goes over to the PEACEFUL night after King Boo's defeat (the sky's peace_blend, which he drives from his burst): a
    /// moonlit blue ambient, a dark teal fog, a pale moon for the sun -- and the flashes and the thunder gone with the stormy sky.</summary>
    [Export] public Color PeaceAmbient = new(0.36f, 0.5f, 0.62f);
    [Export(PropertyHint.Range, "0,4,0.05")] public float PeaceAmbientEnergy = 0.9f;
    [Export] public Color PeaceFog = new(0.04f, 0.09f, 0.12f);
    [Export] public Color PeaceSun = new(0.72f, 0.85f, 1f);
    [Export(PropertyHint.Range, "0,4,0.05")] public float PeaceSunEnergy = 0.7f;
    [ExportGroup("Thunder (211)")]
    /// <summary>211: Jaysonn's thunder (THUNDER, 2.4 s; assets/Sms/Sound/Rematch/) rolls ThunderDelay seconds after the sky's big flash (the purple one, frame 2 of the cycle: once every 10 s), and after the green one too
    /// if ThunderOnGreenToo; not under the fire sky (no flashes there). ThunderDb over the sound table's level; empty ThunderSe = none.</summary>
    [Export] public string ThunderSe = "THUNDER";
    [Export(PropertyHint.Range, "0,3,0.05")] public float ThunderDelay = 0.4f;
    [Export(PropertyHint.Range, "-30,12,0.5")] public float ThunderDb = 0f;
    [Export] public bool ThunderOnGreenToo = false;
    /// <summary>211b (Jaysonn: "only plays after the intro cutscene to not ruin tension"): the thunder WAITS FOR THE FIGHT -- none through the card, Mario's fall and the intro
    /// cutscene (the laugh from nowhere, the look-about, King Boo's rise and dive); once the intro director (IntroPath) is Done -- its FightReady, King Boo up at his spot -- it
    /// rolls with the sky's next purple flash. A flash during the intro passes in silence (its thunder is dropped, not saved for the fight's start). Off = from the first frame,
    /// as 211. No intro director at IntroPath (or anywhere in the scene) = no wait.</summary>
    [Export] public bool ThunderAfterIntro = true;
    [Export] public NodePath IntroPath = "../RematchIntro";
    [Export] public bool DebugPrint;

    // ---- readouts
    /// <summary>The sky's clock (seconds; the shader's frame = this x 30 x its speed; wraps at an hour as the shader's own TIME does).</summary>
    public double SkyTime { get; private set; }
    /// <summary>The strongest of the sky's six pulses right now (0..1), the cloud colour added this frame (display values, before FlashStrength and flash_amount), the sky's final_blend as read.</summary>
    public float FlashNow { get; private set; }
    public Color FlashColorNow { get; private set; }
    public float FinalBlendRead { get; private set; }
    /// <summary>217: the sky's peace_blend as read (0 = the fight's sky, 1 = the peaceful night).</summary>
    public float PeaceBlendRead { get; private set; }
    public float SpeedRead { get; private set; } = 1f;
    public float FlashAmountRead { get; private set; } = 1f;
    public Color BaseAmbient { get; private set; }
    public Color BaseFog { get; private set; }
    public Color BaseSun { get; private set; }
    public float BaseAmbientEnergy { get; private set; }
    public float BaseSunEnergy { get; private set; }
    public bool MaterialFound => _mat != null;
    public bool ClockHeld { get; private set; }
    public int Ticks { get; private set; }
    public Godot.Environment Env => _env;
    public DirectionalLight3D Sun => _sun;
    /// <summary>211: thunders rolled so far, and whether the file was found.</summary>
    public int ThunderPlays { get; private set; }
    public bool ThunderFound => _thunder?.Stream != null;
    /// <summary>211b: the thunder's wait for the fight is over (latched; open from the first frame with ThunderAfterIntro off or no intro director); the thunders it dropped
    /// (flashes during the intro); whether the intro director was found; the sky's frame (of its 300-frame cycle) the last thunder rolled on.</summary>
    public bool ThunderGateOpen { get; private set; }
    public int ThunderHeld { get; private set; }
    public bool IntroFound => _intro != null;
    public float LastThunderFrame { get; private set; } = -1f;

    private Godot.Environment _env; private DirectionalLight3D _sun; private ShaderMaterial _mat; private bool _wasFollowing; private SmsRematchIntro _intro;
    private AudioStreamPlayer _thunder; private int _thunderCycle = -1, _thunderCycleGreen = -1; private readonly Random _rng = new(209);

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        ProcessMode = ProcessModeEnum.Always;
        var we = GetNodeOrNull<WorldEnvironment>(EnvironmentPath) ?? GetTree()?.CurrentScene?.FindChildren("*", "WorldEnvironment", true, false).OfType<WorldEnvironment>().FirstOrDefault();
        _sun = GetNodeOrNull<DirectionalLight3D>(SunPath) ?? GetTree()?.CurrentScene?.FindChildren("*", "DirectionalLight3D", true, false).OfType<DirectionalLight3D>().FirstOrDefault();
        if (we?.Environment != null)
        {
            _env = (Godot.Environment)we.Environment.Duplicate();                                   // ours to write: the saved resource stays as the inspector has it
            we.Environment = _env;
            _mat = _env.Sky?.SkyMaterial as ShaderMaterial;
            BaseAmbient = _env.AmbientLightColor; BaseAmbientEnergy = _env.AmbientLightEnergy; BaseFog = _env.FogLightColor;
        }
        if (_sun != null) { BaseSun = _sun.LightColor; BaseSunEnergy = _sun.LightEnergy; }
        SkyTime = (Time.GetTicksMsec() / 1000.0) % 3600.0;                                            // where the shader's own TIME would be: the sky's phase at a load stays a matter of when, as before
        if (DebugPrint) GD.Print($"[SMS lighting] {GetPath()}: environment {(we != null ? we.Name : "NONE")} (material {(_mat != null ? "found" : "NOT FOUND")}), sun {(_sun != null ? _sun.Name : "NONE")}; base ambient {BaseAmbient} x{BaseAmbientEnergy:F2}, fog {BaseFog}, sun {BaseSun} x{BaseSunEnergy:F2}; clock from {SkyTime:F2} s");
        if (_env != null) Apply(0f, Vector3.Zero, 0f);
        else GD.PushWarning($"[SMS lighting] {GetPath()}: no WorldEnvironment with an Environment at {EnvironmentPath} -- the lights stay as they are");
        if (!string.IsNullOrEmpty(ThunderSe))                                                      // 211: the thunder's own player (not placed: it is the sky's)
        {
            SmsMarioSound.EnsureSfxBus();
            _thunder = new AudioStreamPlayer { Name = "Thunder", Stream = SmsActorSound.Stream(ThunderSe), Bus = SmsActorSound.SfxBus, MaxPolyphony = 2, ProcessMode = ProcessModeEnum.Always };
            AddChild(_thunder);
            if (_thunder.Stream == null) GD.PushWarning($"[SMS lighting] {GetPath()}: no sound file for the thunder '{ThunderSe}'");
        }
        _intro = GetNodeOrNull<SmsRematchIntro>(IntroPath) ?? GetTree()?.CurrentScene?.FindChildren("*", "Node", true, false).OfType<SmsRematchIntro>().FirstOrDefault();   // 211b: the thunder waits for its fight
        if (DebugPrint) GD.Print($"[SMS lighting] the thunder {(ThunderAfterIntro && _intro != null ? $"waits for {_intro.Name}'s fight" : "rolls from the start")}");
    }

    public override void _ExitTree()
    {
        if (_mat != null) _mat.SetShaderParameter("sky_time", -1f);                                  // the material is a shared resource: the sky back on its own clock
    }

    public override void _Process(double delta)
    {
        if (_env == null) return;
        Ticks++;
        if (!FollowSky)
        {
            if (_wasFollowing) { Apply(0f, Vector3.Zero, 0f); if (_mat != null) _mat.SetShaderParameter("sky_time", -1f); _wasFollowing = false; }
            return;
        }
        _wasFollowing = true;
        if (!ClockHeld) { SkyTime += delta; if (SkyTime >= 3600.0) SkyTime -= 3600.0; }
        float k = 0f;
        if (_mat != null)
        {
            _mat.SetShaderParameter("sky_time", (float)SkyTime);
            Variant fb = _mat.GetShaderParameter("final_blend"), sp = _mat.GetShaderParameter("speed"), fa = _mat.GetShaderParameter("flash_amount");   // a parameter left at the shader's default reads nil on the headless renderer: keep the last / the shader's 1
            if (fb.VariantType != Variant.Type.Nil) k = fb.AsSingle();
            if (sp.VariantType != Variant.Type.Nil) SpeedRead = sp.AsSingle();
            if (fa.VariantType != Variant.Type.Nil) FlashAmountRead = fa.AsSingle();
            Variant pb = _mat.GetShaderParameter("peace_blend");                                       // 217: nil until King Boo sets it = 0
            PeaceBlendRead = pb.VariantType != Variant.Type.Nil ? Mathf.Clamp(pb.AsSingle(), 0f, 1f) : 0f;
        }
        FinalBlendRead = Mathf.Clamp(k, 0f, 1f);
        float fc = Mathf.PosMod((float)(SkyTime * 30.0 * SpeedRead), 300f);
        var (c0, c1, peak) = SkyFlash(fc);
        Vector3 cloud = (c0 + c1) * 0.5f * FlashAmountRead;                                            // the clouds show mix(c1, c0, texture): their middle
        FlashNow = peak * FlashAmountRead * (1f - PeaceBlendRead); FlashColorNow = new Color(cloud.X, cloud.Y, cloud.Z, 1f);
        Apply(FinalBlendRead, cloud, FlashStrength * (1f - FinalBlendRead) * (1f - PeaceBlendRead));   // 217: the night has no flashes
        // 211: the thunder -- ThunderDelay after the purple flash (frame 2), once a cycle; the green one (154) too if asked; none under the fire or with the flashes off.
        // 211b: and none before the fight -- the wait ends once the intro is Done; till then a flash's cycle is marked as had and its thunder dropped (Thunder), so the first
        // of the fight comes on the next flash's beat, not the moment the wait ends
        if (!ThunderGateOpen && (!ThunderAfterIntro || _intro == null || !IsInstanceValid(_intro) || _intro.State == SmsRematchIntro.Phase.Done))
        {
            ThunderGateOpen = true;
            if (DebugPrint) GD.Print($"[SMS lighting] the thunder's wait is over ({(!ThunderAfterIntro ? "ThunderAfterIntro off" : _intro == null || !IsInstanceValid(_intro) ? "no intro director" : "the fight has started")}; {ThunderHeld} dropped in the intro)");
        }
        if (_thunder != null && _thunder.Playing && SmsActorSound.Muted(ThunderSe)) _thunder.VolumeDb = -80f;   // 216: the miss's demo mode -- the sky's thunder is the environment's (category 5): silent at once
        if (_thunder != null && _thunder.Stream != null && FinalBlendRead < 0.5f && PeaceBlendRead < 0.5f && FlashAmountRead > 0.01f && SpeedRead > 0.001f)   // 217: none under the night
        {
            int cycle = (int)Math.Floor(SkyTime * 30.0 * SpeedRead / 300.0); float lag = ThunderDelay * 30f * SpeedRead;
            if (cycle != _thunderCycle && fc >= 2f + lag && fc < 150f) { _thunderCycle = cycle; Thunder(fc); }
            if (ThunderOnGreenToo && cycle != _thunderCycleGreen && fc >= 154f + lag) { _thunderCycleGreen = cycle; Thunder(fc); }
        }
    }
    private void Thunder(float fc)
    {
        if (!ThunderGateOpen) { ThunderHeld++; if (DebugPrint) GD.Print($"[SMS lighting] a thunder dropped (the intro is on: {_intro?.State}; the sky's cycle {_thunderCycle})"); return; }   // 211b
        LastThunderFrame = fc;
        _thunder.VolumeDb = SmsActorSound.Muted(ThunderSe) ? -80f : SmsActorSound.MasterDb + SmsActorSound.LevelDb(ThunderSe) + ThunderDb; _thunder.PitchScale = 0.9f + 0.2f * (float)_rng.NextDouble(); _thunder.Play();   // 216: silent under the miss's demo mode
        ThunderPlays++;                                                                            // (not in SmsActorSound's play log: the drives count that log against PlayCount)
        if (DebugPrint) GD.Print($"[SMS lighting] thunder {ThunderPlays} (the sky's cycle {_thunderCycle}, pitch {_thunder.PitchScale:F2})");
    }

    /// <summary>The ambient, the fog and the sun for a blend k toward the fire and a flash colour x its strength (display values, as the inspector's own); 217: then
    /// toward the peaceful night by the sky's peace_blend.</summary>
    private void Apply(float k, Vector3 flash, float strength)
    {
        float p = PeaceBlendRead;
        Color amb = BaseAmbient.Lerp(FinalAmbient, k).Lerp(PeaceAmbient, p); Color fog = BaseFog.Lerp(FinalFog, k).Lerp(PeaceFog, p); Color sun = BaseSun.Lerp(FinalSun, k).Lerp(PeaceSun, p);
        _env.AmbientLightColor = new Color(amb.R + flash.X * strength, amb.G + flash.Y * strength, amb.B + flash.Z * strength, 1f);
        _env.AmbientLightEnergy = Mathf.Lerp(Mathf.Lerp(BaseAmbientEnergy, FinalAmbientEnergy, k), PeaceAmbientEnergy, p);
        _env.FogLightColor = fog;
        if (_sun != null)
        {
            float s = strength * FlashOnSun;
            _sun.LightColor = new Color(sun.R + flash.X * s, sun.G + flash.Y * s, sun.B + flash.Z * s, 1f);
            _sun.LightEnergy = Mathf.Lerp(Mathf.Lerp(BaseSunEnergy, FinalSunEnergy, k), PeaceSunEnergy, p);
        }
    }

    // ---- the shader's flashes, the same math (RematchSky.gdshader: pulse / c0 / c1)
    private static float Ease(float k) { k = Mathf.Clamp(k, 0f, 1f); return 0.5f - 0.5f * Mathf.Cos(Mathf.Pi * k); }
    /// <summary>A brk flash: nothing, up over a..b, down over b..c, nothing.</summary>
    public static float Pulse(float f, float a, float b, float c) => f < a || f > c ? 0f : (f < b ? Ease((f - a) / (b - a)) : 1f - Ease((f - b) / (c - b)));
    /// <summary>The sky's two cloud colours at frame fc of the 300-frame cycle (display values 0..1, before flash_amount) and the strongest pulse (0..1).</summary>
    public static (Vector3 c0, Vector3 c1, float peak) SkyFlash(float fc)
    {
        float p1 = Pulse(fc, 0f, 2f, 19f), p2 = Pulse(fc, 149f, 154f, 164f), p3 = Pulse(fc, 164f, 167f, 189f);
        float q1 = Pulse(fc, 9f, 12f, 39f), q2 = Pulse(fc, 108f, 118f, 166f), q3 = Pulse(fc, 226f, 237f, 300f);
        Vector3 c0 = (new Vector3(100f, 0f, 150f) * p1 + new Vector3(92f, 211f, 97f) * p2 + new Vector3(144f, 111f, 168f) * p3) / 255f;
        Vector3 c1 = (new Vector3(100f, 100f, 100f) * q1 + new Vector3(0f, 0f, 73f) * q2 + new Vector3(54f, 0f, 0f) * q3) / 255f;
        return (c0, c1, Mathf.Max(Mathf.Max(p1, Mathf.Max(p2, p3)), Mathf.Max(q1, Mathf.Max(q2, q3))));
    }
    /// <summary>The frames the six flashes peak on.</summary>
    public static readonly float[] PeakFrames = { 2f, 12f, 118f, 154f, 167f, 237f };

    /// <summary>For the drives and the shots: the clock stopped at a moment (seconds; frame / (30 x speed) lands the sky on that frame of its cycle), and let go again.</summary>
    public void HoldClock(double seconds) { SkyTime = seconds; ClockHeld = true; }
    public void ReleaseClock() { ClockHeld = false; }
}
