using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 157: KING BOO as the REMATCH fights him (KINGBOO_REMATCH_gdoc.txt) -- the arena's own actor, apart from the casino's
/// SmsBossTelesa (the roulette fight stays as it is). This pass is his ENTRANCE: hidden until <see cref="Spawn"/>, then the appear clip, the
/// laugh clip with the long laugh (MSD_SE_BS_TELESA_V_LAUGH1), and the wait loop -- hovering (HoverBob), turning to face Mario (FaceMario),
/// the soft glow and the twinkle at his body (the casino boss's GlowFx / ChikaFx, "a slight glow on him throughout"), the blink loop on his
/// eyes (btelesa_twait's 150-frame table), the ghost's screen mix at GhostOpacity. The attacks, the teleports, the faces and the hurt flip are
/// later passes; the animations another hand's. The model is the casino boss's (btelesa.glb at 1 / 74.44) with its shader materials
/// (BossTelesaBody/Crown/Ha/Mouth.tres: opacity, tint, mask_k, base_tex), copied per instance. Signals: Spawned (the appear starts), Laughed
/// (the laugh starts), Ready (the wait begins -- the fight may start).
/// </summary>
public partial class SmsKingBooRematch : Node3D
{
    /// <summary>The ghost's screen mix (the casino boss's GhostOpacity; the game's ghost is 0.588).</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float GhostOpacity = 0.72f;
    /// <summary>The glow quad's strength (x his opacity) and whether the twinkle stars show.</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float Glow = 0.9f;
    [Export] public bool Twinkle = true;
    /// <summary>The hover: he bobs this many metres up and down over this many seconds (0 = still).</summary>
    [Export] public float HoverBob = 0.35f, HoverBobSeconds = 2.4f;
    /// <summary>He turns to face Mario while he waits, at this many radians a second.</summary>
    [Export] public bool FaceMario = true;
    [Export] public float TurnSpeed = 3f;
    /// <summary>The long laugh at the laugh clip (through SmsActorSound: his sound node) and its gain.</summary>
    [Export] public string LaughSe = "MSD_SE_BS_TELESA_V_LAUGH1";
    [Export] public float LaughDb = 0f;
    [Export] public bool Blink = true;
    [Export] public Texture2D EyeOpen, EyeHalf, EyeClose;
    [Export] public bool DebugPrint;

    [Signal] public delegate void SpawnedEventHandler();
    [Signal] public delegate void LaughedEventHandler();
    [Signal] public delegate void ReadyForFightEventHandler();

    public enum Phase { Hidden, Appear, Laugh, Wait }
    public Phase State { get; private set; } = Phase.Hidden;
    public string Clip => _anim != null ? (string)_anim.CurrentAnimation : "";
    public bool IsSpawned => State != Phase.Hidden;
    public bool LaughPlayed { get; private set; }
    public int MaterialsCopied => _mats.Count;
    /// <summary>jnt_body's place in the world (where the glow sits).</summary>
    public Vector3 BodyPoint => _skel != null && _bBody >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(_bBody).Origin : GlobalPosition;

    // the clips used, their bck frame counts (one bck frame = 1 / 60 s) and loops -- SmsBossTelesa's table
    private const string BckAppear = "btelesa_appear", BckLaugh = "btelesa_laugh", BckWait = "btelesa_wait";
    private static readonly Dictionary<string, (int frames, bool loop)> Clips = new() { [BckAppear] = (59, false), [BckLaugh] = (89, false), [BckWait] = (60, true) };
    private static readonly int[] BlinkEnds = { 19, 21, 24, 26, 28, 30, 33, 35, 85, 87, 90, 92, 150 };   // the twait runs' ends (frames)
    private static readonly int[] BlinkEyes = { 0, 1, 2, 1, 0, 1, 2, 1, 0, 1, 2, 1, 0 };                   // open / half / close

    private Node3D _model; private AnimationPlayer _anim; private Skeleton3D _skel; private int _bBody = -1;
    private readonly List<ShaderMaterial> _mats = new(); private ShaderMaterial _bodyMat;
    private GpuParticles3D _chika; private Node3D _glow; private SmsActorSound _sound;
    private Mario _mario; private float _yaw, _hoverT, _blinkT; private int _eyeNow = -1; private Vector3 _home; private bool _laughSounded;

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>("Model");
        _anim = GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_model != null)
        {
            if (_anim == null) foreach (Node n in _model.FindChildren("*", "AnimationPlayer", true, false)) { _anim = n as AnimationPlayer; if (_anim != null) break; }
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null) { _bBody = _skel.FindBone("jnt_body"); if (_bBody < 0 && _skel.GetBoneCount() > 1) _bBody = 1; }
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    if (mi.GetActiveMaterial(i) is not ShaderMaterial sm) continue;
                    var copy = (ShaderMaterial)sm.Duplicate();
                    mi.SetSurfaceOverrideMaterial(i, copy);
                    _mats.Add(copy);
                    if ((sm.ResourceName ?? "").StartsWith("_mat_body") || _bodyMat == null) _bodyMat = copy;
                }
            }
        }
        else GD.PushWarning($"[SMS kingboo] {GetPath()}: no Model child (btelesa.glb)");
        if (_anim != null) _anim.AnimationFinished += OnClipEnd;
        EyeOpen ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_open.png");
        EyeHalf ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_half.png");
        EyeClose ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_close.png");
        _chika = GetNodeOrNull<GpuParticles3D>("ChikaFx"); _glow = GetNodeOrNull<Node3D>("GlowFx");
        foreach (Node3D fx in new Node3D[] { _chika, _glow }) if (fx != null) { fx.TopLevel = true; fx.Visible = false; }
        foreach (var m in _mats) { m.SetShaderParameter("opacity", GhostOpacity); m.SetShaderParameter("tint", new Vector3(1f, 1f, 1f)); m.SetShaderParameter("mask_k", 0f); }
        ApplyEye(0);
        _sound = SmsActorSound.Attach(this);
        _yaw = Rotation.Y; _home = Position;
        if (_model != null) _model.Visible = false;
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: {_mats.Count} materials, clips {(_anim != null ? string.Join(",", _anim.GetAnimationList()) : "none")}, body joint {_bBody}");
    }
    private static Texture2D LoadTex(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

    /// <summary>He appears: the appear clip, then the laugh with the long laugh, then the wait. Nothing happens twice.</summary>
    public void Spawn()
    {
        if (State != Phase.Hidden || Engine.IsEditorHint()) return;
        State = Phase.Appear; _home = Position; _hoverT = 0f;   // the hover bobs about where he was put
        if (_model != null) _model.Visible = true;
        PlayClip(BckAppear);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: appears at {GlobalPosition}");
        EmitSignal(SignalName.Spawned);
    }

    private void PlayClip(string name)
    {
        if (_anim == null || !_anim.HasAnimation(name)) { if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: no clip {name}"); CallDeferred(nameof(OnClipEnd), new StringName(name)); return; }
        var clip = _anim.GetAnimation(name);
        var (frames, loop) = Clips.TryGetValue(name, out var c) ? c : (60, false);
        clip.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        float rate = clip.Length > 0f ? frames / (float)clip.Length : 60f;
        _anim.Play(name);
        _anim.Seek(0f, true);
        _anim.SpeedScale = 60f / rate;
    }

    private void OnClipEnd(StringName name)
    {
        string n = name;
        if (State == Phase.Appear && n == BckAppear)
        {
            State = Phase.Laugh; _laughSounded = false;
            PlayClip(BckLaugh);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the laugh");
            EmitSignal(SignalName.Laughed);
        }
        else if (State == Phase.Laugh && n == BckLaugh)
        {
            State = Phase.Wait;
            PlayClip(BckWait);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: waits -- the fight may start");
            EmitSignal(SignalName.ReadyForFight);
        }
    }

    public override void _Process(double delta)
    {
        if (State == Phase.Hidden || Engine.IsEditorHint()) return;
        float dt = (float)delta;
        // the long laugh with the laugh clip (the casino boss plays it two bck frames in: a thirtieth of a second, dropped here)
        if (State == Phase.Laugh && !_laughSounded)
        {
            _laughSounded = true; LaughPlayed = true;
            if (_sound != null && !string.IsNullOrEmpty(LaughSe)) _sound.Play(LaughSe, LaughDb);
            else if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: no sound node for the laugh");
        }
        if (State == Phase.Wait)
        {
            _hoverT += dt;
            if (HoverBob > 0f && HoverBobSeconds > 0f) Position = _home + Vector3.Up * (MathF.Sin(_hoverT / HoverBobSeconds * MathF.Tau) * HoverBob);
            if (FaceMario)
            {
                if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
                if (_mario != null)
                {
                    Vector3 d = _mario.GlobalPosition - GlobalPosition; d.Y = 0f;
                    if (d.LengthSquared() > 1e-4f)
                    {
                        float want = MathF.Atan2(d.X, d.Z);     // +Z forward: the model faces +Z at yaw 0 (the casino places him at rotation 180 to face its camera)
                        _yaw = Mathf.LerpAngle(_yaw, want, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
                        Rotation = new Vector3(0f, _yaw, 0f);
                    }
                }
            }
        }
        if (Blink) BlinkTick(dt);
        PlaceFx();
    }

    private void BlinkTick(float dt)
    {
        _blinkT += dt * 30f;                                     // the btp clock: 30 frames a second
        int f = (int)_blinkT % 150; int eye = 0;
        for (int i = 0; i < BlinkEnds.Length; i++) if (f < BlinkEnds[i]) { eye = BlinkEyes[i]; break; }
        ApplyEye(eye);
    }
    private void ApplyEye(int eye)
    {
        if (eye == _eyeNow || _bodyMat == null) return;
        Texture2D t = eye switch { 1 => EyeHalf, 2 => EyeClose, _ => EyeOpen } ?? EyeOpen;
        if (t == null) return;
        _eyeNow = eye;
        _bodyMat.SetShaderParameter("base_tex", t);
    }
    private void PlaceFx()
    {
        bool show = State != Phase.Hidden && _model != null && _model.Visible;
        Vector3 at = BodyPoint;
        if (_chika != null) { bool on = show && Twinkle; _chika.Visible = on; _chika.Emitting = on; if (on) _chika.GlobalPosition = at; }
        if (_glow != null)
        {
            _glow.Visible = show && Glow > 0f;
            if (_glow.Visible)
            {
                _glow.GlobalPosition = at;
                if (_glow is MeshInstance3D gm && gm.GetActiveMaterial(0) is ShaderMaterial gsm) gsm.SetShaderParameter("strength", Glow * GhostOpacity);
            }
        }
    }
}
