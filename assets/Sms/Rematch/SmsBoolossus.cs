using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 164: the BOOLOSSUS SMASH (KINGBOO_REMATCH_gdoc.txt, attack 2: "Crown glows white. A Boolossus falls from above Mario and tries to slam down
/// on him. On landing it splits into 15 Boos that spread out as a shockwave, then vanish after one second"). King Boo makes them (SmsKingBooRematch's
/// Smash move) and each runs on its own. Pass 165 (Jaysonn: "see Boolossuses DIVE down into the arena, and maybe 3 of them, not just one; some effects"):
/// it no longer fades in over Mario and drops -- it APPEARS DiveHeight over the floor and DiveDistance out from Mario along its approach (an appear puff,
/// MSD_SE_EN_TELESA_APPEAR) and DIVES at him: it falls under SmashGravity from DiveStartSpeed, steering its flat speed so it lands exactly on the TARGET
/// (its shadow on the floor, growing: the tell); the target follows Mario (at TrackSpeed) until LockSeconds before the landing, then is fixed -- the dodge
/// window; the body leans into the dive (DiveTilt) and trails ghost wisps (TrailColor). It LANDS (the roulette's thud, the Boo crush, a camera shake --
/// LandShake -- a ring of light racing out over the floor (RingColor, RingGrow, RingSeconds) and a burst of pale smoke (SmokeColor)): Mario within its
/// body (BodyRadius; 215: half of BodySize) takes the plain hit (Mario.TakeDamage, the common enemy profile) and it SPLITS -- gone in
/// a flash and a squash, and WaveCount Boos (<see cref="SmsWaveBoo"/>, the hotel Boo at WaveBooScale) spread out from its footprint in a ring at WaveSpeed,
/// WaveHeight over the floor, for WaveSeconds, each the plain hit on a Mario it runs through, fading over their last WaveFadeSeconds into a puff each
/// (the port's EnemyDisap burst, tinted) -- a jump clears the ring. The Boolossus model: Boolossus.obj (Jaysonn's Downloads, run through Blender:
/// models/Sms/Rematch/boolossus.glb, 1.07 m across at scale 1, its face on +Z) on GhostBoo.gdshader (unshaded: the arena's violet ambient made it a
/// lavender blob; a soft shade of its own, the ghost's mix at Opacity). The Boos: 165c, the port's OWN hotel Boo (models/Eniemes/Telesa/telesa.glb with
/// its TelesaToon materials, the game's look; the doc's Galaxy Teresa.dae came out with its tongue strip wrapped over the top of the head -- its UVs run
/// to u 3 for a GX mirror wrap the export lost -- and is dropped), playing telesa_wait, fading by the materials' `fade`. Signals: Landed(hitMario),
/// Finished (the Boos are gone, the node frees itself).
/// Pass 215 (Jaysonn: "i want the big boo to be sleepy boo from sms. since boolouses kinda looks out of place and thus we can make the impact sound of it
/// the death of sleepy boo like in sms when he gets eaten"): the body is the game's SLEEPY BOO -- TDebuTelesa's debutelesa.bmd (the hotel's, pass 152:
/// models/Eniemes/Telesa/debutelesa.glb, the scene's Model child) in its own toon materials, playing its one clip (debutelesa_wait, its snoring sleep, at
/// the game's pace), BodySize across (7 m, the Boolossus's size: the hit, the shadow and the circle as before); the landing sounds the Sleepy Boo's own
/// EATEN, what the game plays as Yoshi swallows it. GhostLook puts the Boolossus's ghost shader back over any model (boolossus.glb stays in the project).
/// </summary>
public partial class SmsBoolossus : Node3D
{
    [ExportGroup("The look")]
    /// <summary>215: the body's size side to side across its face, in metres -- the model is scaled to it whatever its units (the Sleepy Boo's glb is in the
    /// game's: 306 u across, 4.1 m in the hotel), and its body hit is half of it (BodyRadius 0). 7 = the Boolossus's size since 192 (Jaysonn: "make Boolossus a
    /// little bigger": ModelScale 5.5 -> 6.5 on its 1.07-m model).</summary>
    [Export(PropertyHint.Range, "1,20,0.1")] public float BodySize = 7f;
    /// <summary>215: off = the model in its own materials (the Sleepy Boo as the game draws it; its toon has no fade, so it shows at once under the appear
    /// puff and squashes away at the split); on = GhostBoo.gdshader over every surface, the Boolossus's see-through ghost (Opacity = the ghost's mix, Lift /
    /// Glow = the body's brightness and rim; the fade in and the landing's flash) -- for a model with no look of its own (boolossus.glb).</summary>
    [Export] public bool GhostLook = false;
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float Opacity = 0.9f;
    [Export] public float Lift = 1f, Glow = 0.5f;
    /// <summary>215: the model's clip, looped from the dive's start to the split ("" = none): the Sleepy Boo's one bck, debutelesa_wait (its snoring sleep),
    /// and that clip's length in the game's frames at 60 a second -- 94: the rip stretched it to 5.8 s, and the hotel's Sleepy Boo plays it at the game's
    /// pace the same way (SmsSmallEnemy's bck rate); 0 = the clip's own length.</summary>
    [Export] public string ModelClip = "debutelesa_wait";
    [Export(PropertyHint.Range, "0,600,1")] public float ModelClipFrames = 94f;
    /// <summary>The model's face is on its +Z side (the Sleepy Boo's, as the game's model; Boolossus.obj's too; off = on -Z). It faces along its dive, then
    /// the camera's side on the floor.</summary>
    [Export] public bool FacesPlusZ = true;
    /// <summary>The shadow on the floor under the target: its darkness and its size at the landing (x the body's radius). 214 (Jaysonn: "the shadow from
    /// boolossus is a rectangle"): a round soft disc -- it was a bare quad (no texture: square) scaled across the world's X and Y, so laid flat it stretched one
    /// way only (2 x r by 2 m: the rectangle).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float ShadowOpacity = 0.55f;
    [Export] public float ShadowScale = 1.1f;
    /// <summary>214 (Jaysonn: "like bomb boo attack with the yellowish circles let have them be white to signal its a boolossus attack"): the landing's CIRCLE --
    /// the Bomb Boo volley's mark (its ring texture, additive, pulsing quicker as the dive comes down) in WHITE, the body's whole footprint (the body's radius x
    /// ShadowScale) about the target from the dive's start, the shadow growing inside it; gone as it lands. Off = the shadow alone.</summary>
    [Export] public bool LandingMark = true;
    [Export] public Color MarkColor = new Color(1f, 1f, 1f, 0.6f);
    [ExportGroup("The dive (165)")]
    /// <summary>Where it appears: this high over the floor, this far out from Mario along its approach direction (King Boo gives one per Boolossus); the
    /// fade in; its speed downward as it appears; the fall's gravity (30 from 24 m at 6 m/s down is a 1.08 s dive; the game's gravity is 19).</summary>
    [Export] public float DiveHeight = 24f, DiveDistance = 20f, FadeInSeconds = 0.2f, DiveStartSpeed = 6f;
    [Export] public float SmashGravity = 30f;
    /// <summary>The target follows Mario at this speed (m/s; 0 = where he was when it appeared) until this many seconds before the landing, then holds --
    /// the dodge: Mario runs at about 8 m/s, so 0.6 s is near five metres. Its flat speed toward the target is capped at MaxDiveSpeed.</summary>
    [Export] public float TrackSpeed = 6f, LockSeconds = 0.6f, MaxDiveSpeed = 45f;
    /// <summary>How far the body leans into the dive (1 = it faces straight along its path; 0 = upright, facing its target).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float DiveTilt = 0.8f;
    /// <summary>The body on Mario at the landing: its radius (0 = half of BodySize) and how high the hit reaches over the floor.</summary>
    [Export] public float BodyRadius = 0f, BodyHitHeight = 5f;
    [ExportGroup("The effects (165)")]
    /// <summary>The ghost wisps it trails as it dives (their colour; 0 alpha = none), the puff as it appears, the puff as each Boo vanishes.</summary>
    [Export] public Color TrailColor = new Color(0.7f, 0.85f, 1f, 0.55f);
    [Export] public bool AppearPuff = true, BooVanishPuff = true;
    /// <summary>The landing: the ring of light over the floor (its colour, how many body radii it races out to, over how long), the smoke (its colour, how
    /// many puffs), the camera shake (x the hip drop's; 0 = none).</summary>
    [Export] public Color RingColor = new Color(0.75f, 0.85f, 1f, 0.9f);
    [Export] public float RingGrow = 3.5f, RingSeconds = 0.5f;
    [Export] public Color SmokeColor = new Color(0.8f, 0.82f, 0.95f, 0.5f);
    [Export(PropertyHint.Range, "0,80,1")] public int SmokeCount = 24;
    [Export(PropertyHint.Range, "0,3,0.1")] public float LandShake = 1f;
    /// <summary>The Boos' puffs and the appear puff: the port's enemy-vanish burst, tinted this colour.</summary>
    [Export] public Color PuffTint = new Color(0.7f, 0.85f, 1f, 1f);
    [Export] public PackedScene PuffScene;
    [ExportGroup("The wave")]
    /// <summary>The Boos it splits into: how many, their size (1 = the hotel Boo's own, 1.8 m across; BooModelScale is the glb's game units to metres), how
    /// fast they spread, how long they live, how high over the floor they ride (a jump clears them), their fade at the end, and where the ring starts (0 =
    /// the body's radius). 192 (Jaysonn: "have the boos last for 3 seconds instead of 1"): WaveSeconds 1 -> 3, and the Boos SLOW as they go -- they burst
    /// out at WaveSpeed and ease to a stop WaveReach out (at 9 m/s and 10 m: 5.9 m in the first second, 8.3 by two, 9.3 by three), hanging there as a ring
    /// of hazards until they fade; at the old constant speed three seconds would carry them 27 m, off the arena. WaveReach 0 = the constant speed as before.</summary>
    [Export(PropertyHint.Range, "1,40,1")] public int WaveCount = 15;
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float WaveBooScale = 1f;
    [Export] public float BooModelScale = 0.013434f;
    [Export] public float WaveSpeed = 9f, WaveSeconds = 3f, WaveHeight = 0.9f, WaveFadeSeconds = 0.25f, WaveStartRadius = 0f;
    [Export(PropertyHint.Range, "0,30,0.5")] public float WaveReach = 10f;
    [Export] public bool BooFacesPlusZ = true;
    /// <summary>165: the Boos' solidity (the materials' fade; 1 = the game's Boo as it is) and their idle clip (telesa_wait; "" = none).</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float WaveBooOpacity = 1f;
    [Export] public string BooClip = "telesa_wait";
    /// <summary>The Boos' own hit radius (0 = the model's half width x WaveBooScale, 0.7 m; 1.1 reaches a little past the ghost's skin, so fifteen of them
    /// close the ring out to about 8 m -- beyond that the wave has gaps a Mario can stand in).</summary>
    [Export] public float BooRadius = 1.1f;
    [Export] public PackedScene BooScene;
    [ExportGroup("The sounds")]
    /// <summary>The landing: 215 (Jaysonn: "we can make the impact sound of it the death of sleepy boo like in sms when he gets eaten") the Sleepy Boo's own
    /// MSD_SE_EN_DB_TELSA_EATEN (TDebuTelesa::receiveMessage as Yoshi swallows it: the game's render, its 94-ms lead cut so it lands with the slam; 211's
    /// Luigi's Mansion bounce, BOOLOSSUS_BOUND, stays in Sound/Rematch/); a Boo popping off as it splits (211: BOOLOSSUS_PON); the appear and the wave's end
    /// the game's.</summary>
    [Export] public string AppearSe = "MSD_SE_EN_TELESA_APPEAR", LandSe = "MSD_SE_EN_DB_TELSA_EATEN", SplitSe = "BOOLOSSUS_PON", WaveGoneSe = "MSD_SE_EN_TELESA_DISAPPEAR";
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void LandedEventHandler(bool hitMario);
    [Signal] public delegate void FinishedEventHandler();

    public enum Phase { Idle, Dive, Wave, Gone }
    public Phase State { get; private set; } = Phase.Idle;
    public float FloorY { get; private set; } = float.NaN;
    public float BottomY => GlobalPosition.Y + _modelBottom * _k;
    public float Radius => BodyRadius > 0f ? BodyRadius : BodySize * 0.5f;
    /// <summary>192: how far a wave Boo has ridden from its start at this age -- out at WaveSpeed, easing to a stop WaveReach out (0: WaveSpeed x age as before).</summary>
    public float WaveDistance(float age) => WaveReach > 0f ? WaveReach * (1f - MathF.Exp(-age * WaveSpeed / WaveReach)) : WaveSpeed * age;
    public bool HitMario { get; private set; }
    public bool WaveHitMario => _waveHits > 0;
    public int WaveHits => _waveHits;
    public Vector3 LandingPoint { get; private set; }
    public Vector3 StartPoint { get; private set; }
    public Vector3 Target => _target;
    public bool TargetLocked => _locked;
    public float DiveSeconds { get; private set; }
    public float Vy => _vy;
    public Vector3 Velocity => _vel;
    public float TimeToLand => _tRem;
    public float OpacityShown => _opacityShown;
    /// <summary>For the drives: how far the face points DOWN (degrees; 0 = level) -- the lean into the dive.</summary>
    public float ModelPitchDeg
    {
        get
        {
            if (_model == null) return 0f;
            Vector3 z = _model.GlobalTransform.Basis.Z.Normalized();
            float faceY = FacesPlusZ ? z.Y : -z.Y;
            return Mathf.RadToDeg(MathF.Asin(Mathf.Clamp(-faceY, -1f, 1f)));
        }
    }
    public bool TrailEmitting => _trail != null && _trail.Emitting;
    public int ShakesStarted { get; private set; }
    public static readonly List<SmsBoolossus> All = new();
    public static readonly List<SmsWaveBoo> Boos = new();
    public static int RingsMade, SmokesMade, PuffsMade;
    public int BoosOut { get { int n = 0; foreach (var b in _boos) if (b != null && IsInstanceValid(b)) n++; return n; } }

    private const float U = 74.44f;
    private Node3D _model; private MeshInstance3D _shadow, _mark; private StandardMaterial3D _markMat; private readonly List<ShaderMaterial> _mats = new(); private SmsActorSound _sound; private GpuParticles3D _trail;
    private static ImageTexture _disc;
    /// <summary>214: the shadow's round disc -- solid to 80 % of the way out, softening to nothing at the rim (alpha; white, the material's colour darkens it).</summary>
    private static ImageTexture Disc()
    {
        if (_disc != null) return _disc;
        const int N = 64; var img = Image.CreateEmpty(N, N, false, Image.Format.Rgba8);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f, r = MathF.Sqrt(u * u + v * v);
                img.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0.8f, r)));
            }
        return _disc = ImageTexture.CreateFromImage(img);
    }
    /// <summary>214, for the drives: the shadow's and the mark's world size across X and Z (metres) and the mark's colour as last placed.</summary>
    public Vector2 ShadowSizeNow { get; private set; }
    public Vector2 MarkSizeNow { get; private set; }
    public Color MarkColorNow { get; private set; }
    public bool MarkShown => _mark != null && _mark.Visible;
    public bool ShadowRound => _shadow?.MaterialOverride is StandardMaterial3D sm && sm.AlbedoTexture == _disc && _disc != null;
    /// <summary>215, for the drives: the model's scale (its units to metres: BodySize over its width), its width side to side at scale 1, its clip player,
    /// and the surfaces under the ghost shader (0 = every one in its own material).</summary>
    public float ScaleNow => _k;
    public float ModelWidth => _modelWidth;
    public AnimationPlayer ModelAnim => _anim;
    public int GhostMaterials => _mats.Count;
    private Mario _mario; private float _t, _vy, _tRem, _opacityShown, _modelBottom = 0.035f, _modelWidth = 1.07f, _k = 1f; private int _waveHits;
    private AnimationPlayer _anim;
    private Vector3 _target, _vel; private bool _locked;
    private readonly List<SmsWaveBoo> _boos = new(); private float _waveT; private bool _waveSounded;

    public override void _Ready()
    {
        All.Add(this);
        _model = GetNodeOrNull<Node3D>("Model");
        var shader = GhostLook && ResourceLoader.Exists("res://assets/Sms/Rematch/GhostBoo.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/GhostBoo.gdshader") : null;   // 215: the ghost look only
        if (_model != null)
        {
            var aabb = new Aabb(); bool first = true;
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;                                                       // 215: the round disc is its shadow (214) -- the opaque toon cast a second one, the body's outline, sliding over the floor far from the target with the arena's light
                Aabb a = mi.Transform * mi.GetAabb(); aabb = first ? a : aabb.Merge(a); first = false;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    if (shader == null) break;
                    var sm = new ShaderMaterial { Shader = shader };
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm && bm.AlbedoTexture != null) sm.SetShaderParameter("base_tex", bm.AlbedoTexture);
                    sm.SetShaderParameter("use_vertex_color", false); sm.SetShaderParameter("lift", Lift); sm.SetShaderParameter("glow", Glow); sm.SetShaderParameter("opacity", 0f);
                    mi.SetSurfaceOverrideMaterial(s, sm); _mats.Add(sm);
                }
            }
            if (!first) { _modelBottom = aabb.Position.Y; _modelWidth = aabb.Size.X; }
            _k = BodySize / Mathf.Max(1e-4f, _modelWidth);                                                                       // 215: scaled to BodySize across, whatever the model's units
            _model.Scale = Vector3.One * _k;
            foreach (Node n in _model.FindChildren("*", "AnimationPlayer", true, false)) { _anim = n as AnimationPlayer; if (_anim != null) break; }
        }
        else GD.PushWarning($"[SMS boolossus] {GetPath()}: no Model child (debutelesa.glb)");
        _shadow = new MeshInstance3D { Name = "Shadow", TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _shadow.Mesh = new QuadMesh { Size = Vector2.One * 2f };
        _shadow.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0f, 0f, 0f, ShadowOpacity), AlbedoTexture = Disc(), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, RenderPriority = 1 };   // 214: the disc
        AddChild(_shadow);
        var ring = SmsFireballs.Tex("hamonring3_flat");                                                                          // 214: the volley's mark's ring, in white
        if (LandingMark && ring != null)
        {
            _markMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = ring, AlbedoColor = MarkColor, RenderPriority = 2 };
            _mark = new MeshInstance3D { Name = "LandingMark", TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One * 2f }, MaterialOverride = _markMat };
            AddChild(_mark);
        }
        if (TrailColor.A > 0.01f) { _trail = SmsSmashFx.Trail(TrailColor, Mathf.Max(0.6f, Radius * 0.55f), Radius * 0.6f); AddChild(_trail); }
        BooScene ??= ResourceLoader.Exists("res://models/Eniemes/Telesa/telesa.glb") ? GD.Load<PackedScene>("res://models/Eniemes/Telesa/telesa.glb") : null;
        PuffScene ??= ResourceLoader.Exists("res://assets/Sms/Fx/EnemyDisap.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Fx/EnemyDisap.tscn") : null;
        _sound = SmsActorSound.Attach(this);
        if (_model != null) _model.Visible = false;
        if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: {_mats.Count} ghost materials, model bottom {_modelBottom:F3} width {_modelWidth:F3} (x{_k:F5}: {BodySize} m across, radius {Radius:F2} m), clip {(_anim != null && _anim.HasAnimation(ModelClip) ? ModelClip : "none")}, boo scene {(BooScene != null ? "yes" : "NONE")}, puff scene {(PuffScene != null ? "yes" : "NONE")}, trail {(_trail != null ? "yes" : "none")}");
    }
    public override void _ExitTree() { All.Remove(this); }
    /// <summary>203: gone at once with the field (the split's cut) -- its wave Boos freed outright, the body (shadow and trail with it) hidden and freed; true = anything went.</summary>
    public bool VanishNow()
    {
        bool any = false;
        foreach (var b in _boos) if (IsInstanceValid(b) && b.VanishNow()) any = true;
        if (!IsQueuedForDeletion()) { State = Phase.Gone; Visible = false; if (_trail != null) _trail.Emitting = false; QueueFree(); any = true; }
        return any;
    }

    /// <summary>165: the dive. It appears DiveHeight over the floor, DiveDistance out from Mario along <paramref name="approachDir"/> (flat; any length),
    /// and dives at where he stands. The floor's y is the caller's (King Boo's probe under Mario).</summary>
    public void Begin(Mario mario, float floorY, Vector3 approachDir)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY;
        Vector3 m = mario.GlobalPosition;
        approachDir.Y = 0f; if (approachDir.LengthSquared() < 1e-4f) approachDir = Vector3.Forward;
        approachDir = approachDir.Normalized();
        _target = new Vector3(m.X, floorY, m.Z); _locked = false;
        StartPoint = new Vector3(m.X + approachDir.X * DiveDistance, floorY + DiveHeight - _modelBottom * _k, m.Z + approachDir.Z * DiveDistance);
        GlobalPosition = StartPoint;
        _vy = Mathf.Max(0f, DiveStartSpeed); _vel = new Vector3(-approachDir.X, -_vy, -approachDir.Z); _tRem = 9f;
        State = Phase.Dive; _t = 0f; DiveSeconds = 0f;
        if (_model != null) { _model.Visible = true; Lean(0f); PlayClip(); }
        _shadow.Visible = true; if (_mark != null) _mark.Visible = true; PlaceShadow();
        if (_trail != null) { _trail.Restart(); _trail.Emitting = true; }
        if (AppearPuff && PuffScene != null) SmsSmashFx.Puff(PuffScene, GetParent(), GlobalPosition + Vector3.Up * (Radius * 0.6f), Radius * 1.6f, PuffTint);
        if (_sound != null && !string.IsNullOrEmpty(AppearSe)) _sound.Play(AppearSe);
        if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: appears at {GlobalPosition}, {DiveDistance} m out from Mario {m} (floor {floorY:F2}), and dives");
    }
    /// <summary>The 164 call: a dive from straight over Mario's far side (the camera's).</summary>
    public void Begin(Mario mario, float floorY) => Begin(mario, floorY, -mario.SmsFacingDir());

    private void SetOpacity(float o) { _opacityShown = o; foreach (var m in _mats) m.SetShaderParameter("opacity", o); }
    private void SetFlash(float f) { foreach (var m in _mats) m.SetShaderParameter("flash", f); }
    /// <summary>215: the model's clip from its start at the game's pace (ModelClipFrames at 60 a second over the clip's length); KeepClip loops it -- a
    /// replay as it ends, so the glb's shared clip is never changed (the hotel's Sleepy Boos use it too).</summary>
    private void PlayClip()
    {
        if (_anim == null || string.IsNullOrEmpty(ModelClip) || !_anim.HasAnimation(ModelClip)) return;
        float len = (float)_anim.GetAnimation(ModelClip).Length;
        _anim.SpeedScale = ModelClipFrames > 0f && len > 0f ? len * 60f / ModelClipFrames : 1f;
        _anim.Play(ModelClip); _anim.Seek(0.0, true);
    }
    private void KeepClip()
    {
        if (_anim != null && _model != null && _model.Visible && !_anim.IsPlaying() && !string.IsNullOrEmpty(ModelClip) && _anim.HasAnimation(ModelClip)) _anim.Play(ModelClip);
    }
    private void PlaceShadow()
    {
        if (_shadow == null || float.IsNaN(FloorY)) return;
        float k = State == Phase.Dive ? Mathf.Clamp(1f - _tRem / 1.2f, 0f, 1f) : 1f;
        float r = Radius * ShadowScale * Mathf.Lerp(0.4f, 1f, k);
        Basis flat = new Basis(Vector3.Right, -MathF.PI / 2f);                                                                   // the quad laid on the floor (it spans the world's X and Z)
        _shadow.GlobalTransform = new Transform3D(flat.Scaled(new Vector3(r, 1f, r)), new Vector3(_target.X, FloorY + 0.04f, _target.Z));   // 214: scaled across X AND Z (it was X and Y: the rectangle)
        ShadowSizeNow = new Vector2(_shadow.GlobalBasis.X.Length(), _shadow.GlobalBasis.Y.Length()) * 2f;
        float shown = Mathf.Clamp(_opacityShown / Mathf.Max(0.01f, Opacity), 0f, 1f);
        if (_shadow.MaterialOverride is StandardMaterial3D sm) sm.AlbedoColor = new Color(0f, 0f, 0f, ShadowOpacity * shown * Mathf.Lerp(0.5f, 1f, k));
        if (_mark != null && _markMat != null)
        {
            // 214: the white circle -- the whole footprint from the start, pulsing as the volley's mark does (quicker as the dive comes down: k over the whole dive)
            float R = Radius * ShadowScale, kDive = State == Phase.Dive ? Mathf.Clamp(DiveSeconds / Mathf.Max(0.05f, DiveSeconds + _tRem), 0f, 1f) : 1f;
            _mark.GlobalTransform = new Transform3D(flat.Scaled(new Vector3(R, 1f, R)), new Vector3(_target.X, FloorY + 0.08f, _target.Z));
            MarkSizeNow = new Vector2(_mark.GlobalBasis.X.Length(), _mark.GlobalBasis.Y.Length()) * 2f;
            _markMat.AlbedoColor = MarkColor with { A = MarkColor.A * shown * (0.55f + 0.45f * MathF.Sin(_t * (6f + 14f * kDive))) };
            MarkColorNow = _markMat.AlbedoColor;
        }
    }
    /// <summary>The body turned along its path by DiveTilt (its face along the velocity), else upright facing the target; <paramref name="k"/> 0 = upright.</summary>
    private void Lean(float k)
    {
        if (_model == null) return;
        Vector3 flat = _vel; flat.Y = 0f;
        if (flat.LengthSquared() < 1e-6f) { flat = _target - GlobalPosition; flat.Y = 0f; }
        if (flat.LengthSquared() < 1e-6f) flat = Vector3.Forward;
        Basis upright = Basis.LookingAt(flat.Normalized(), Vector3.Up);
        Vector3 along = _vel.LengthSquared() > 1e-6f ? _vel.Normalized() : flat.Normalized();
        Basis dive = Mathf.Abs(along.Dot(Vector3.Up)) > 0.999f ? upright : Basis.LookingAt(along, Vector3.Up);
        Basis b = new Basis(upright.GetRotationQuaternion().Slerp(dive.GetRotationQuaternion(), Mathf.Clamp(k * DiveTilt, 0f, 1f)));
        if (FacesPlusZ) b *= new Basis(Vector3.Up, MathF.PI);
        _model.GlobalBasis = b.Scaled(Vector3.One * _k);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        switch (State)
        {
            case Phase.Dive:
                _t += dt; DiveSeconds += dt;
                SetOpacity(Opacity * Mathf.Clamp(FadeInSeconds > 0f ? _t / FadeInSeconds : 1f, 0f, 1f));
                // the time left to the floor at the fall's own pace, the target followed until the lock, the flat speed set to land on it
                float h = Mathf.Max(0f, BottomY - FloorY);
                _tRem = SmashGravity > 0f ? (-_vy + MathF.Sqrt(_vy * _vy + 2f * SmashGravity * h)) / SmashGravity : (_vy > 0f ? h / _vy : 9f);
                if (!_locked && _tRem <= LockSeconds) { _locked = true; if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: the target LOCKS at {_target} ({_tRem:F2} s to the landing)"); }
                if (!_locked && _mario != null && TrackSpeed > 0f)
                {
                    Vector3 to = new Vector3(_mario.GlobalPosition.X, FloorY, _mario.GlobalPosition.Z) - _target;
                    float step = TrackSpeed * dt;
                    if (to.Length() > step) to = to.Normalized() * step;
                    _target += to;
                }
                Vector3 flat = new Vector3(_target.X - GlobalPosition.X, 0f, _target.Z - GlobalPosition.Z) / Mathf.Max(_tRem, dt);
                if (flat.Length() > MaxDiveSpeed) flat = flat.Normalized() * MaxDiveSpeed;
                _vy += SmashGravity * dt;
                _vel = new Vector3(flat.X, -_vy, flat.Z);
                GlobalPosition += _vel * dt;
                Lean(Mathf.Clamp(_t / 0.25f, 0f, 1f));
                KeepClip();
                PlaceShadow();
                if (BottomY <= FloorY) Land();
                break;
            case Phase.Wave:
                _waveT += dt;
                if (_waveT < 0.15f) { SetOpacity(Opacity * (1f - _waveT / 0.15f)); KeepClip(); if (_model != null) _model.Scale = new Vector3(_k * (1f + _waveT * 2f), _k * Mathf.Max(0.05f, 1f - _waveT * 6f), _k * (1f + _waveT * 2f)); }
                else if (_model != null && _model.Visible) { _model.Visible = false; _shadow.Visible = false; }
                if (!_waveSounded && _waveT >= WaveSeconds - WaveFadeSeconds) { _waveSounded = true; if (_sound != null && !string.IsNullOrEmpty(WaveGoneSe)) _sound.Play(WaveGoneSe); }
                if (BoosOut == 0 && _waveT > 0.3f)
                {
                    State = Phase.Gone;
                    if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: the wave is gone ({_waveHits} hit{(_waveHits == 1 ? "" : "s")} on Mario); finished");
                    EmitSignal(SignalName.Finished);
                    QueueFree();
                }
                break;
        }
    }

    /// <summary>The slam: the floor met -- the ring, the smoke, the shake; Mario inside the body is the plain hit; the Boolossus splits into the wave.</summary>
    private void Land()
    {
        GlobalPosition = new Vector3(GlobalPosition.X, FloorY - _modelBottom * _k, GlobalPosition.Z);
        LandingPoint = new Vector3(GlobalPosition.X, FloorY, GlobalPosition.Z);
        if (_trail != null) _trail.Emitting = false;
        if (_mark != null) _mark.Visible = false;                                                                                   // 214: the circle goes as it lands (the volley's mark does)
        Lean(0f); FaceCamera();
        bool hit = false;
        if (_mario != null)
        {
            Vector3 m = _mario.GlobalPosition;
            float r = Radius + SmsParams.Mario.DamageRadius / U;
            if (new Vector2(m.X - LandingPoint.X, m.Z - LandingPoint.Z).Length() <= r && m.Y < FloorY + BodyHitHeight) { hit = true; _mario.TakeDamage(LandingPoint + Vector3.Up * 1f, Mario.SmsDamageProfile.EnemyCommon); }
        }
        HitMario = hit;
        if (_sound != null && !string.IsNullOrEmpty(LandSe)) _sound.Play(LandSe);
        if (_sound != null && !string.IsNullOrEmpty(SplitSe)) _sound.Play(SplitSe);
        SetFlash(1f);
        Node fxParent = GetParent() ?? this;
        if (RingColor.A > 0.01f && RingSeconds > 0f) SmsSmashFx.Ring(fxParent, LandingPoint, Radius, Radius * RingGrow, RingSeconds, RingColor);
        if (SmokeCount > 0 && SmokeColor.A > 0.01f) SmsSmashFx.Smoke(fxParent, LandingPoint, Radius, SmokeCount, SmokeColor);
        if (LandShake > 0f && SmsCameraShake.Current != null) { SmsCameraShake.Current.Start(SmsCameraShake.Kind.HipDrop, LandShake); ShakesStarted++; }
        State = Phase.Wave; _waveT = 0f; _waveSounded = false;
        float r0 = WaveStartRadius > 0f ? WaveStartRadius : Radius;
        for (int i = 0; i < WaveCount; i++)
        {
            float a = i / (float)WaveCount * MathF.Tau;
            var dir = new Vector3(MathF.Sin(a), 0f, MathF.Cos(a));
            var boo = new SmsWaveBoo { Name = $"WaveBoo{i}" };
            fxParent.AddChild(boo);
            boo.Launch(BooScene, LandingPoint + dir * r0 + Vector3.Up * WaveHeight, dir, this);
            _boos.Add(boo);
        }
        if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: LANDS at {LandingPoint} after {DiveSeconds:F2} s ({(hit ? "ON Mario" : "a miss")}; Mario at {(_mario != null ? _mario.GlobalPosition.ToString() : "?")}, radius {Radius:F2}, the target was {_target}) -> {WaveCount} Boos at {r0:F2} m");
        EmitSignal(SignalName.Landed, hit);
    }
    private void FaceCamera()
    {
        if (_model == null) return;
        var cam = GetViewport()?.GetCamera3D();
        Vector3 to = cam != null ? cam.GlobalPosition - GlobalPosition : (_mario != null ? -_mario.SmsFacingDir() : Vector3.Back); to.Y = 0f;
        if (to.LengthSquared() < 1e-4f) return;
        float yaw = MathF.Atan2(to.X, to.Z) + (FacesPlusZ ? 0f : MathF.PI);
        _model.GlobalBasis = new Basis(Vector3.Up, yaw).Scaled(Vector3.One * _k);
    }
    internal void BooHit() { _waveHits++; }
}

/// <summary>164: one Boo of the Boolossus's wave -- 165c: the port's hotel Boo (telesa.glb at BooModelScale x WaveBooScale, its TelesaToon materials copied
/// per Boo and faded by `fade`, its wait clip playing), riding outward along its line at WaveSpeed, WaveHeight over the floor, for WaveSeconds, fading
/// over the last WaveFadeSeconds into a puff, then gone. Mario in its body (its own radius + his damage cylinder) takes the plain hit, once per Boo.
/// Another scene as BooScene (StandardMaterial3D surfaces) gets GhostBoo.gdshader instead.</summary>
public partial class SmsWaveBoo : Node3D
{
    public SmsBoolossus Maker;
    public Vector3 Dir { get; private set; }
    public float Age { get; private set; }
    public bool Hit { get; private set; }
    public float Radius { get; private set; } = 0.7f;
    public float OpacityShown { get; private set; }
    /// <summary>The materials are the hotel Boo's own (TelesaToon, faded by `fade`), else GhostBoo copies.</summary>
    public bool UsesTelesaMaterials { get; private set; }
    public bool ClipPlaying => _anim != null && _anim.IsPlaying();
    public int MaterialsMade => _mats.Count;
    private AnimationPlayer _anim;
    private Node3D _model; private readonly List<ShaderMaterial> _mats = new(); private Vector3 _from; private float _halfHeight = 0.6f; private bool _puffed;

    public void Launch(PackedScene scene, Vector3 at, Vector3 dir, SmsBoolossus owner)
    {
        Maker = owner; Dir = dir; _from = at; GlobalPosition = at;
        SmsBoolossus.Boos.Add(this);
        float k = owner.BooModelScale * owner.WaveBooScale;
        OpacityShown = owner.WaveBooOpacity;
        if (scene != null)
        {
            _model = scene.Instantiate<Node3D>(); _model.Name = "Model"; AddChild(_model); _model.Scale = Vector3.One * k;
            var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/GhostBoo.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/GhostBoo.gdshader") : null;
            var aabb = new Aabb(); bool first = true; int telesa = 0;
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                Aabb a = mi.Transform * mi.GetAabb(); aabb = first ? a : aabb.Merge(a); first = false;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    if (mi.GetActiveMaterial(s) is ShaderMaterial own)                   // the hotel Boo's TelesaToon materials: a copy per Boo, faded by `fade`
                    {
                        var copy = (ShaderMaterial)own.Duplicate();
                        copy.SetShaderParameter("fade", owner.WaveBooOpacity);
                        mi.SetSurfaceOverrideMaterial(s, copy); _mats.Add(copy); telesa++;
                        continue;
                    }
                    if (shader == null) continue;                                         // another scene: the ghost shader over its albedo texture
                    var sm = new ShaderMaterial { Shader = shader };
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm && bm.AlbedoTexture != null) sm.SetShaderParameter("base_tex", bm.AlbedoTexture);
                    sm.SetShaderParameter("use_vertex_color", false); sm.SetShaderParameter("lift", 1f); sm.SetShaderParameter("glow", owner.Glow); sm.SetShaderParameter("opacity", owner.WaveBooOpacity);
                    mi.SetSurfaceOverrideMaterial(s, sm); _mats.Add(sm);
                }
            }
            UsesTelesaMaterials = telesa > 0 && telesa == _mats.Count;
            if (!first) { Radius = Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.5f * k; _halfHeight = aabb.Size.Y * 0.5f * k; }
            foreach (Node n in _model.FindChildren("*", "AnimationPlayer", true, false)) { _anim = n as AnimationPlayer; if (_anim != null) break; }
            if (_anim != null && !string.IsNullOrEmpty(owner.BooClip) && _anim.HasAnimation(owner.BooClip)) { _anim.GetAnimation(owner.BooClip).LoopMode = Animation.LoopModeEnum.Linear; _anim.Play(owner.BooClip); _anim.Seek(GD.Randf() * (float)_anim.GetAnimation(owner.BooClip).Length, true); }
        }
        if (owner.BooRadius > 0f) Radius = owner.BooRadius;
        float yaw = MathF.Atan2(dir.X, dir.Z) + (owner.BooFacesPlusZ ? 0f : MathF.PI);
        Rotation = new Vector3(0f, yaw, 0f);
    }

    public override void _Process(double delta)
    {
        if (Maker == null || !IsInstanceValid(Maker)) { QueueFree(); return; }
        float dt = (float)delta; Age += dt;
        GlobalPosition = _from + Dir * Maker.WaveDistance(Age);                                                       // 192: out fast, easing to a stop WaveReach out
        float left = Maker.WaveSeconds - Age;
        if (left <= Maker.WaveFadeSeconds)
        {
            float k = Mathf.Clamp(left / Mathf.Max(0.01f, Maker.WaveFadeSeconds), 0f, 1f);
            OpacityShown = Maker.WaveBooOpacity * k;
            foreach (var m in _mats) { if (UsesTelesaMaterials) m.SetShaderParameter("fade", OpacityShown); else { m.SetShaderParameter("opacity", OpacityShown); m.SetShaderParameter("flash", (1f - k) * 0.6f); } }
            if (!_puffed && Maker.BooVanishPuff && Maker.PuffScene != null) { _puffed = true; SmsSmashFx.Puff(Maker.PuffScene, GetParent(), GlobalPosition, Radius * 1.3f, Maker.PuffTint); }
        }
        if (!Hit)
        {
            var mario = GetTree().GetFirstNodeInGroup("player") as Mario;
            if (mario != null)
            {
                Vector3 m = mario.GlobalPosition, p = GlobalPosition;
                float r = Radius + SmsParams.Mario.DamageRadius / 74.44f;
                bool inY = p.Y + _halfHeight >= m.Y && p.Y - _halfHeight <= m.Y + SmsParams.Mario.DamageHeight / 74.44f;
                if (inY && new Vector2(p.X - m.X, p.Z - m.Z).Length() <= r) { Hit = true; Maker.BooHit(); mario.TakeDamage(p, Mario.SmsDamageProfile.EnemyCommon); if (Maker.DebugPrint) GD.Print($"[SMS boolossus] {Name}: runs through Mario at {m} (Boo at {p}, {Age:F2} s out; hp {mario.SmsHP})"); }
            }
        }
        if (Age >= Maker.WaveSeconds) QueueFree();
    }
    public override void _ExitTree() { SmsBoolossus.Boos.Remove(this); }
    /// <summary>203: gone at once with the field (the split's cut): hidden and freed, no puff; true = it went.</summary>
    public bool VanishNow()
    {
        if (IsQueuedForDeletion()) return false;
        Visible = false; QueueFree();
        return true;
    }
}

/// <summary>165: the smash's EFFECTS, built in code from the port's Fx textures (assets/Sms/Fx/tex): the dive's wisp trail (radgrad, additive), the landing's
/// ring of light racing out over the floor (hamonring3_flat, additive; <see cref="SmsGhostRing"/> grows and fades itself away) and its pale smoke burst
/// (kemuball3, a one-shot that frees itself when its puffs are gone), and the port's enemy-vanish burst (EnemyDisap.tscn, an SmsFxBurst) tinted, for the
/// Boos' vanishing and the Boolossus's appearing.</summary>
public static class SmsSmashFx
{
    private const string Tex = "res://assets/Sms/Fx/tex/";
    private static Texture2D Load(string name) => ResourceLoader.Exists(Tex + name) ? GD.Load<Texture2D>(Tex + name) : null;
    private static StandardMaterial3D Additive(Texture2D tex, Color color, bool particles, bool additive = true)
    {
        var m = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoColor = color, AlbedoTexture = tex, VertexColorUseAsAlbedo = particles,
            BillboardMode = particles ? BaseMaterial3D.BillboardModeEnum.Particles : BaseMaterial3D.BillboardModeEnum.Disabled
        };
        return m;
    }
    private static GradientTexture1D Fade(float a0, float a1, float mid = 0.3f)
    {
        var g = new Gradient { Offsets = new float[] { 0f, mid, 1f }, Colors = new Color[] { new Color(1f, 1f, 1f, a0 * 0.6f), new Color(1f, 1f, 1f, a0), new Color(1f, 1f, 1f, a1) } };
        return new GradientTexture1D { Gradient = g };
    }

    /// <summary>The wisp trail: a child emitter whose puffs stay in the world where they were shed (LocalCoords off), rising a little.</summary>
    public static GpuParticles3D Trail(Color color, float puffSize, float emitRadius)
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = new Vector3(0f, 1f, 0f), Spread = 180f, InitialVelocityMin = 0.3f, InitialVelocityMax = 1.4f, Gravity = new Vector3(0f, 1.2f, 0f),
            ScaleMin = 0.6f, ScaleMax = 1.3f, DampingMin = 0.5f, DampingMax = 1f, EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = Mathf.Max(0.1f, emitRadius),
            ColorRamp = Fade(1f, 0f, 0.25f)
        };
        var p = new GpuParticles3D
        {
            Name = "DiveTrail", Amount = 56, Lifetime = 0.75, Emitting = false, LocalCoords = false, FixedFps = 60, ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = Vector2.One * puffSize, Material = Additive(Load("radgrad_raw.png"), color, true) },
            VisibilityAabb = new Aabb(new Vector3(-40f, -40f, -40f), new Vector3(80f, 80f, 80f))
        };
        return p;
    }

    /// <summary>The landing's ring of light, flat on the floor at <paramref name="at"/>, from radius <paramref name="r0"/> to <paramref name="r1"/> over
    /// <paramref name="seconds"/>, fading; it frees itself.</summary>
    public static SmsGhostRing Ring(Node parent, Vector3 at, float r0, float r1, float seconds, Color color)
    {
        var ring = new SmsGhostRing { Name = "SmashRing", R0 = r0, R1 = r1, Seconds = seconds, Tint = color, Tex = Load("hamonring3_flat.png") };
        parent.AddChild(ring);
        ring.GlobalPosition = at + Vector3.Up * 0.08f;
        SmsBoolossus.RingsMade++;
        return ring;
    }

    /// <summary>The landing's smoke: <paramref name="count"/> pale puffs (a mix blend, not additive) bursting out along the floor from the body's edge, braking,
    /// rising a little; a one-shot that frees itself when its puffs are gone.</summary>
    public static GpuParticles3D Smoke(Node parent, Vector3 at, float radius, int count, Color color)
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = new Vector3(1f, 0f, 0f), Spread = 180f, Flatness = 1f, InitialVelocityMin = 7f, InitialVelocityMax = 13f, Gravity = new Vector3(0f, 0.8f, 0f),
            DampingMin = 9f, DampingMax = 14f, ScaleMin = 0.8f, ScaleMax = 1.4f, EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring, EmissionRingAxis = Vector3.Up,
            EmissionRingRadius = Mathf.Max(0.2f, radius * 0.7f), EmissionRingInnerRadius = Mathf.Max(0.1f, radius * 0.4f), EmissionRingHeight = 0.2f,
            ColorRamp = Fade(1f, 0f, 0.2f)
        };
        var p = new GpuParticles3D
        {
            Name = "SmashSmoke", Amount = count, Lifetime = 0.9, OneShot = true, Explosiveness = 0.95f, Emitting = false, LocalCoords = false, FixedFps = 60, ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = Vector2.One * Mathf.Max(1.2f, radius * 0.9f), Material = Additive(Load("kemuball3_raw.png"), color, true, false) },   // a mix blend: additive puffs piled up to a white blot
            VisibilityAabb = new Aabb(new Vector3(-30f, -10f, -30f), new Vector3(60f, 20f, 60f))
        };
        parent.AddChild(p);
        p.GlobalPosition = at + Vector3.Up * 0.6f;
        p.Finished += p.QueueFree;
        p.Emitting = true;
        SmsBoolossus.SmokesMade++;
        return p;
    }

    /// <summary>The port's enemy-vanish burst (EnemyDisap.tscn: blue-grey smoke and a glow) at <paramref name="at"/>, x <paramref name="scale"/>, its glow tinted.</summary>
    public static SmsFxBurst Puff(PackedScene scene, Node parent, Vector3 at, float scale, Color tint)
    {
        if (scene == null || parent == null) return null;
        if (scene.Instantiate() is not SmsFxBurst b) return null;
        parent.AddChild(b);
        b.Play(new Transform3D(Basis.Identity.Scaled(Vector3.One * Mathf.Max(0.1f, scale)), at));
        var v = new Vector4(tint.R, tint.G, tint.B, 1f);
        b.SetShaderParam("prm", v); b.SetShaderParam("env", v);
        SmsBoolossus.PuffsMade++;
        return b;
    }
}

/// <summary>165: a ring of light flat on the floor that grows from R0 to R1 over Seconds while it fades, then frees itself (the Boolossus's landing).</summary>
public partial class SmsGhostRing : MeshInstance3D
{
    public float R0 = 3f, R1 = 10f, Seconds = 0.5f; public Color Tint = new Color(0.75f, 0.85f, 1f, 0.9f); public Texture2D Tex;
    public float Age { get; private set; }
    public float RadiusNow { get; private set; }
    private StandardMaterial3D _mat;
    public override void _Ready()
    {
        TopLevel = true; CastShadow = ShadowCastingSetting.Off;
        Mesh = new QuadMesh { Size = Vector2.One * 2f };
        _mat = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoColor = Tint, AlbedoTexture = Tex, RenderPriority = 2
        };
        MaterialOverride = _mat;
        Apply(0f);
    }
    private void Apply(float k)
    {
        RadiusNow = Mathf.Lerp(R0, R1, 1f - (1f - k) * (1f - k));                            // fast out, slowing
        GlobalBasis = new Basis(Vector3.Right, -MathF.PI / 2f).Scaled(new Vector3(RadiusNow, RadiusNow, 1f));
        _mat.AlbedoColor = new Color(Tint.R, Tint.G, Tint.B, Tint.A * (1f - k));
    }
    public override void _Process(double delta)
    {
        Age += (float)delta;
        float k = Seconds > 0f ? Mathf.Clamp(Age / Seconds, 0f, 1f) : 1f;
        Apply(k);
        if (k >= 1f) QueueFree();
    }
}
