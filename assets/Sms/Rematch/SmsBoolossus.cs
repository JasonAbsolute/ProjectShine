using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 164: the BOOLOSSUS SMASH (KINGBOO_REMATCH_gdoc.txt, attack 2: "Crown glows white. A Boolossus falls from above Mario and tries to slam down
/// on him. On landing it splits into 15 Boos that spread out as a shockwave, then vanish after one second"). King Boo makes one (SmsKingBooRematch's
/// Smash move) and it runs on its own: it APPEARS DropHeight over Mario (a fade in; MSD_SE_EN_TELESA_APPEAR), HANGS there HangSeconds drifting after
/// him at TrackSpeed with its SHADOW on the floor under it (the tell -- the shadow grows as it falls), then FALLS under SmashGravity and LANDS (the
/// roulette's thud): Mario within its body (BodyRadius, from the model's own width x ModelScale) takes the plain hit (Mario.TakeDamage, the common
/// enemy profile) and it SPLITS -- it is gone in a flash and WaveCount Boos (<see cref="SmsWaveBoo"/>, boo.glb at WaveBooScale) spread out from its
/// footprint in a ring at WaveSpeed, WaveHeight over the floor, for WaveSeconds, each the plain hit on Mario it runs through, fading over their last
/// WaveFadeSeconds (MSD_SE_EN_TELESA_DISAPPEAR) -- a jump clears the ring. The models: Boolossus.obj (Jaysonn's Downloads, run through Blender:
/// models/Sms/Rematch/boolossus.glb, 1.07 m across at scale 1) and Teresa.dae (boo.glb, 2.5 m across) on GhostBoo.gdshader (the ghost's screen mix at
/// Opacity, a soft rim). Signals: Landed(hitMario), Finished (the Boos are gone, the node frees itself).
/// </summary>
public partial class SmsBoolossus : Node3D
{
    [ExportGroup("The look")]
    /// <summary>The model's size (1.07 m across at 1): 5.5 = about 6 m, Mario twice over; the ghost's screen mix; the body's brightness and rim.</summary>
    [Export(PropertyHint.Range, "0.5,12,0.1")] public float ModelScale = 5.5f;
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float Opacity = 0.75f;
    [Export] public float Lift = 1f, Glow = 0.5f;
    /// <summary>The model's face is on its +Z side (off: on -Z). It is turned to face the camera's side as it hangs.</summary>
    [Export] public bool FacesPlusZ = false;
    /// <summary>The shadow on the floor under it: its darkness and its size at the full fall (x the body's radius).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float ShadowOpacity = 0.55f;
    [Export] public float ShadowScale = 1.1f;
    [ExportGroup("The fall")]
    /// <summary>How high over Mario it appears, how long it hangs there (drifting after him at TrackSpeed), the fade in, and the fall's gravity (30 from 16 m
    /// is a 1.03 s fall; the game's is 19).</summary>
    [Export] public float DropHeight = 16f, HangSeconds = 0.45f, TrackSpeed = 6f, FadeInSeconds = 0.25f;
    [Export] public float SmashGravity = 30f;
    /// <summary>The body on Mario at the landing: its radius (0 = the model's own half width x ModelScale) and how high the hit reaches over the floor.</summary>
    [Export] public float BodyRadius = 0f, BodyHitHeight = 5f;
    [ExportGroup("The wave")]
    /// <summary>The Boos it splits into: how many, their size (boo.glb is 2.5 m across at 1), how fast they spread, how long they live, how high over the
    /// floor they ride (a jump clears them), their fade at the end, and where the ring starts (0 = the body's radius).</summary>
    [Export(PropertyHint.Range, "1,40,1")] public int WaveCount = 15;
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float WaveBooScale = 0.55f;
    [Export] public float WaveSpeed = 9f, WaveSeconds = 1f, WaveHeight = 0.9f, WaveFadeSeconds = 0.25f, WaveStartRadius = 0f;
    [Export] public bool BooFacesPlusZ = true;
    /// <summary>The Boos' own hit radius (0 = the model's half width x WaveBooScale, 0.7 m; 1.1 reaches a little past the ghost's skin, so fifteen of them
    /// close the ring out to about 8 m -- beyond that the wave has gaps a Mario can stand in).</summary>
    [Export] public float BooRadius = 1.1f;
    [Export] public PackedScene BooScene;
    [ExportGroup("The sounds")]
    [Export] public string AppearSe = "MSD_SE_EN_TELESA_APPEAR", LandSe = "MSD_SE_BS_TELESA_RLT_DOWN", SplitSe = "MSD_SE_EN_TELESA_CRUSH", WaveGoneSe = "MSD_SE_EN_TELESA_DISAPPEAR";
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void LandedEventHandler(bool hitMario);
    [Signal] public delegate void FinishedEventHandler();

    public enum Phase { Idle, Hang, Fall, Wave, Gone }
    public Phase State { get; private set; } = Phase.Idle;
    public float FloorY { get; private set; } = float.NaN;
    public float BottomY => GlobalPosition.Y + _modelBottom * ModelScale;
    public float Radius => BodyRadius > 0f ? BodyRadius : _modelHalfWidth * ModelScale;
    public bool HitMario { get; private set; }
    public bool WaveHitMario => _waveHits > 0;
    public int WaveHits => _waveHits;
    public Vector3 LandingPoint { get; private set; }
    public float FallSeconds { get; private set; }
    public float HangLeft => Mathf.Max(0f, HangSeconds - _t);
    public float Vy => _vy;
    public float OpacityShown => _opacityShown;
    public static readonly List<SmsBoolossus> All = new();
    public static readonly List<SmsWaveBoo> Boos = new();
    public int BoosOut { get { int n = 0; foreach (var b in _boos) if (b != null && IsInstanceValid(b)) n++; return n; } }

    private Node3D _model; private MeshInstance3D _shadow; private readonly List<ShaderMaterial> _mats = new(); private SmsActorSound _sound;
    private Mario _mario; private float _t, _vy, _opacityShown, _modelBottom = 0.035f, _modelHalfWidth = 0.535f; private int _waveHits;
    private readonly List<SmsWaveBoo> _boos = new(); private float _waveT; private bool _waveSounded;

    public override void _Ready()
    {
        All.Add(this);
        _model = GetNodeOrNull<Node3D>("Model");
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/GhostBoo.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/GhostBoo.gdshader") : null;
        if (_model != null)
        {
            _model.Scale = Vector3.One * ModelScale;
            var aabb = new Aabb(); bool first = true;
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
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
            if (!first) { _modelBottom = aabb.Position.Y; _modelHalfWidth = Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.5f; }
        }
        else GD.PushWarning($"[SMS boolossus] {GetPath()}: no Model child (boolossus.glb)");
        _shadow = new MeshInstance3D { Name = "Shadow", TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _shadow.Mesh = new QuadMesh { Size = Vector2.One * 2f };
        _shadow.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0f, 0f, 0f, ShadowOpacity), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = false, RenderPriority = 1 };
        AddChild(_shadow);
        BooScene ??= ResourceLoader.Exists("res://models/Sms/Rematch/boo.glb") ? GD.Load<PackedScene>("res://models/Sms/Rematch/boo.glb") : null;
        _sound = SmsActorSound.Attach(this);
        if (_model != null) _model.Visible = false;
        if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: {_mats.Count} materials, model bottom {_modelBottom:F3} half width {_modelHalfWidth:F3} (x{ModelScale}: radius {Radius:F2} m), boo scene {(BooScene != null ? "yes" : "NONE")}");
    }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>Over Mario: it appears DropHeight over the floor under him and hangs. The floor's y is the caller's (King Boo's probe).</summary>
    public void Begin(Mario mario, float floorY)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY;
        Vector3 m = mario.GlobalPosition;
        GlobalPosition = new Vector3(m.X, floorY + DropHeight, m.Z);
        FaceCamera();
        State = Phase.Hang; _t = 0f; _vy = 0f;
        if (_model != null) _model.Visible = true;
        _shadow.Visible = true; PlaceShadow(0f);
        if (_sound != null && !string.IsNullOrEmpty(AppearSe)) _sound.Play(AppearSe);
        if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: appears at {GlobalPosition} over Mario {m} (floor {floorY:F2})");
    }
    private void FaceCamera()
    {
        var cam = GetViewport()?.GetCamera3D();
        Vector3 to = cam != null ? cam.GlobalPosition - GlobalPosition : (_mario != null ? -_mario.SmsFacingDir() : Vector3.Back); to.Y = 0f;
        if (to.LengthSquared() < 1e-4f) return;
        float yaw = MathF.Atan2(to.X, to.Z) + (FacesPlusZ ? 0f : MathF.PI);
        Rotation = new Vector3(0f, yaw, 0f);
    }
    private void SetOpacity(float o) { _opacityShown = o; foreach (var m in _mats) m.SetShaderParameter("opacity", o); }
    private void SetFlash(float f) { foreach (var m in _mats) m.SetShaderParameter("flash", f); }
    private void PlaceShadow(float fallK)
    {
        if (_shadow == null || float.IsNaN(FloorY)) return;
        float r = Radius * ShadowScale * Mathf.Lerp(0.45f, 1f, fallK);
        _shadow.GlobalTransform = new Transform3D(new Basis(Vector3.Right, -MathF.PI / 2f).Scaled(new Vector3(r, r, 1f)), new Vector3(GlobalPosition.X, FloorY + 0.04f, GlobalPosition.Z));
        if (_shadow.MaterialOverride is StandardMaterial3D sm) sm.AlbedoColor = new Color(0f, 0f, 0f, ShadowOpacity * Mathf.Clamp(_opacityShown / Mathf.Max(0.01f, Opacity), 0f, 1f));
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        switch (State)
        {
            case Phase.Hang:
                _t += dt;
                SetOpacity(Opacity * Mathf.Clamp(FadeInSeconds > 0f ? _t / FadeInSeconds : 1f, 0f, 1f));
                if (_mario != null && TrackSpeed > 0f)
                {
                    Vector3 to = _mario.GlobalPosition - GlobalPosition; to.Y = 0f;
                    float step = TrackSpeed * dt;
                    if (to.Length() > step) to = to.Normalized() * step;
                    GlobalPosition += to;
                }
                PlaceShadow(0f);
                if (_t >= HangSeconds) { State = Phase.Fall; FallSeconds = 0f; if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: FALLS from {GlobalPosition}"); }
                break;
            case Phase.Fall:
                FallSeconds += dt;
                _vy += SmashGravity * dt;
                GlobalPosition += Vector3.Down * (_vy * dt);
                PlaceShadow(Mathf.Clamp(1f - (BottomY - FloorY) / Mathf.Max(0.1f, DropHeight), 0f, 1f));
                if (BottomY <= FloorY) Land();
                break;
            case Phase.Wave:
                _waveT += dt;
                if (_waveT < 0.15f) { SetOpacity(Opacity * (1f - _waveT / 0.15f)); if (_model != null) _model.Scale = new Vector3(ModelScale * (1f + _waveT * 2f), ModelScale * Mathf.Max(0.05f, 1f - _waveT * 6f), ModelScale * (1f + _waveT * 2f)); }
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

    /// <summary>The slam: the floor met -- Mario inside the body is the plain hit; the Boolossus splits into the wave.</summary>
    private void Land()
    {
        GlobalPosition = new Vector3(GlobalPosition.X, FloorY - _modelBottom * ModelScale, GlobalPosition.Z);
        LandingPoint = new Vector3(GlobalPosition.X, FloorY, GlobalPosition.Z);
        bool hit = false;
        if (_mario != null)
        {
            Vector3 m = _mario.GlobalPosition;
            float r = Radius + SmsParams.Mario.DamageRadius / 74.44f;
            if (new Vector2(m.X - LandingPoint.X, m.Z - LandingPoint.Z).Length() <= r && m.Y < FloorY + BodyHitHeight) { hit = true; _mario.TakeDamage(LandingPoint + Vector3.Up * 1f, Mario.SmsDamageProfile.EnemyCommon); }
        }
        HitMario = hit;
        if (_sound != null && !string.IsNullOrEmpty(LandSe)) _sound.Play(LandSe);
        if (_sound != null && !string.IsNullOrEmpty(SplitSe)) _sound.Play(SplitSe);
        SetFlash(1f);
        State = Phase.Wave; _waveT = 0f; _waveSounded = false;
        float r0 = WaveStartRadius > 0f ? WaveStartRadius : Radius;
        for (int i = 0; i < WaveCount; i++)
        {
            float a = i / (float)WaveCount * MathF.Tau;
            var dir = new Vector3(MathF.Sin(a), 0f, MathF.Cos(a));
            var boo = new SmsWaveBoo { Name = $"WaveBoo{i}" };
            GetParent().AddChild(boo);
            boo.Launch(BooScene, LandingPoint + dir * r0 + Vector3.Up * WaveHeight, dir, this);
            _boos.Add(boo);
        }
        if (DebugPrint) GD.Print($"[SMS boolossus] {Name}: LANDS at {LandingPoint} after {FallSeconds:F2} s ({(hit ? "ON Mario" : "a miss")}; Mario at {(_mario != null ? _mario.GlobalPosition.ToString() : "?")}, radius {Radius:F2}) -> {WaveCount} Boos at {r0:F2} m");
        EmitSignal(SignalName.Landed, hit);
    }
    internal void BooHit() { _waveHits++; }
}

/// <summary>164: one Boo of the Boolossus's wave -- boo.glb at the owner's WaveBooScale on GhostBoo.gdshader, riding outward along its line at WaveSpeed,
/// WaveHeight over the floor, for WaveSeconds, fading over the last WaveFadeSeconds, then gone. Mario in its body (its own radius + his damage cylinder)
/// takes the plain hit, once per Boo.</summary>
public partial class SmsWaveBoo : Node3D
{
    public SmsBoolossus Maker;
    public Vector3 Dir { get; private set; }
    public float Age { get; private set; }
    public bool Hit { get; private set; }
    public float Radius { get; private set; } = 0.7f;
    private Node3D _model; private readonly List<ShaderMaterial> _mats = new(); private Vector3 _from; private float _halfHeight = 0.6f;

    public void Launch(PackedScene scene, Vector3 at, Vector3 dir, SmsBoolossus owner)
    {
        Maker = owner; Dir = dir; _from = at; GlobalPosition = at;
        SmsBoolossus.Boos.Add(this);
        float k = owner.WaveBooScale;
        if (scene != null)
        {
            _model = scene.Instantiate<Node3D>(); _model.Name = "Model"; AddChild(_model); _model.Scale = Vector3.One * k;
            var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/GhostBoo.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/GhostBoo.gdshader") : null;
            var aabb = new Aabb(); bool first = true;
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                Aabb a = mi.Transform * mi.GetAabb(); aabb = first ? a : aabb.Merge(a); first = false;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    if (shader == null) break;
                    var sm = new ShaderMaterial { Shader = shader };
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm && bm.AlbedoTexture != null) sm.SetShaderParameter("base_tex", bm.AlbedoTexture);
                    sm.SetShaderParameter("use_vertex_color", true); sm.SetShaderParameter("lift", 1.8f); sm.SetShaderParameter("ambient", 0.35f); sm.SetShaderParameter("glow", owner.Glow); sm.SetShaderParameter("opacity", owner.Opacity);
                    mi.SetSurfaceOverrideMaterial(s, sm); _mats.Add(sm);
                }
            }
            if (!first) { Radius = Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.5f * k; _halfHeight = aabb.Size.Y * 0.5f * k; }
        }
        if (owner.BooRadius > 0f) Radius = owner.BooRadius;
        float yaw = MathF.Atan2(dir.X, dir.Z) + (owner.BooFacesPlusZ ? 0f : MathF.PI);
        Rotation = new Vector3(0f, yaw, 0f);
    }

    public override void _Process(double delta)
    {
        if (Maker == null || !IsInstanceValid(Maker)) { QueueFree(); return; }
        float dt = (float)delta; Age += dt;
        GlobalPosition = _from + Dir * (Maker.WaveSpeed * Age);
        float left = Maker.WaveSeconds - Age;
        if (left <= Maker.WaveFadeSeconds)
        {
            float k = Mathf.Clamp(left / Mathf.Max(0.01f, Maker.WaveFadeSeconds), 0f, 1f);
            foreach (var m in _mats) { m.SetShaderParameter("opacity", Maker.Opacity * k); m.SetShaderParameter("flash", (1f - k) * 0.6f); }
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
}
