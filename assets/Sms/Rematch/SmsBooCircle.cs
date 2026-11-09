using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 201 (Jaysonn: "a circle of boos attack similar to how ghost houses used to have them in super mario world" -- the Circling Boo Buddies): a RING of
/// hotel Boos (models/Eniemes/Telesa/telesa.glb on its own TelesaToon materials, as the Boolossus's wave Boos) about Mario -- Count places on the ring, Gap of
/// them EMPTY (the gap), Radius out from where he stood, Height over the floor, TURNING once every RevSeconds (one way or the other, at random). It holds its
/// size HoldSeconds, then CLOSES to MinRadius by Seconds while it drifts after Mario at DriftSpeed, and FADES (a puff per Boo). A Boo whose body meets Mario's
/// damage cylinder hurts him (the common enemy damage; his invincibility spaces the hits). Out through the gap as it comes round, or over a Boo (its top is
/// Height plus half its height). Signals Formed(boos), Hit, Finished.
/// </summary>
public partial class SmsBooCircle : Node3D
{
    public enum Phase { Idle, Turning, Fading, Done }
    [ExportGroup("The ring")]
    /// <summary>Places on the ring and how many of them are empty (the gap), the ring's radius about Mario (metres) and the radius it closes to, the Boos' height
    /// over the floor (their middles), a turn's length (seconds; the way at random), how long it holds its size before it closes, its whole life, how fast it
    /// drifts after Mario (m/s), how far in from the floor's edge its middle may go.</summary>
    [Export(PropertyHint.Range, "3,16,1")] public int Count = 8;
    [Export(PropertyHint.Range, "1,4,1")] public int Gap = 1;
    [Export] public float Radius = 7f, MinRadius = 1f, Height = 1.5f, RevSeconds = 4f, HoldSeconds = 2f, Seconds = 8f, DriftSpeed = 1f, WallInset = 1f;
    [ExportGroup("The Boos")]
    /// <summary>The hotel Boo's glb at this scale (game units: 0.013434 = the Boolossus's wave Boos) x BooScale; their hit radius (metres); their fade in and out; their
    /// opacity; the wait clip; whether they face the ring's middle (else their way round).</summary>
    [Export] public float ModelScale = 0.013434f;
    [Export(PropertyHint.Range, "0.3,3,0.05")] public float BooScale = 1.1f;
    [Export] public float BooRadius = 1.1f, FadeInSeconds = 0.4f, FadeSeconds = 0.3f;
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float BooOpacity = 1f;
    [Export] public string BooClip = "telesa_wait";
    [Export] public bool FaceMiddle = true, FacesPlusZ = true, VanishPuff = true;
    [Export] public Color PuffTint = new Color(0.75f, 0.6f, 1f);
    [ExportGroup("The sounds")]
    [Export] public string AppearSe = "MSD_SE_EN_TELESA_APPEAR", GoneSe = "MSD_SE_EN_TELESA_DISAPPEAR";
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void FormedEventHandler(int boos);
    [Signal] public delegate void HitEventHandler();
    [Signal] public delegate void FinishedEventHandler();

    public Phase State { get; private set; } = Phase.Idle;
    public int Hits { get; private set; }
    public float Age => _t;
    public float RadiusNow { get; private set; }
    public float Angle => _angle;
    public int Spin => _spin;
    public Vector3 Centre => _centre;
    public float FloorY { get; private set; }
    public int BoosOut { get { int n = 0; foreach (var b in _boos) if (b != null && IsInstanceValid(b)) n++; return n; } }
    public IReadOnlyList<SmsCircleBoo> Boos => _boos;
    /// <summary>The gap's middle, as a bearing about the ring's middle (radians, atan2(x, z)), and its width (radians).</summary>
    public float GapBearing => Mathf.Wrap(_angle + (Count - Gap + (Gap - 1) * 0.5f) * MathF.Tau / Mathf.Max(1, Count), -MathF.PI, MathF.PI);
    public float GapWidth => Gap * MathF.Tau / Mathf.Max(1, Count);
    public static readonly List<SmsBooCircle> All = new();
    public static int Circles, HitCount;

    private Mario _mario; private SmsActorSound _sound; private readonly Random _rng = new(201);
    private readonly List<SmsCircleBoo> _boos = new();
    private Vector3 _centre, _arenaCentre; private float _arenaRadius = 26f, _t, _angle, _fadeT; private int _spin = 1; private bool _puffed;
    private PackedScene _scene, _puffScene;

    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>The ring forms about Mario where he stands, on the floor at <paramref name="floorY"/>, inside the arena given.</summary>
    public void Begin(Mario mario, float floorY, Vector3 arenaCentre, float arenaRadius)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY; _arenaCentre = arenaCentre; _arenaRadius = Mathf.Max(5f, arenaRadius);
        _centre = mario != null && GodotObject.IsInstanceValid(mario) ? new Vector3(mario.GlobalPosition.X, floorY, mario.GlobalPosition.Z) : new Vector3(arenaCentre.X, floorY, arenaCentre.Z);
        GlobalPosition = _centre;
        _sound = SmsActorSound.Attach(this);
        _scene = ResourceLoader.Exists("res://models/Eniemes/Telesa/telesa.glb") ? GD.Load<PackedScene>("res://models/Eniemes/Telesa/telesa.glb") : null;
        _puffScene = ResourceLoader.Exists("res://assets/Sms/Fx/EnemyDisap.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Fx/EnemyDisap.tscn") : null;
        _spin = _rng.NextDouble() < 0.5 ? 1 : -1; _angle = (float)(_rng.NextDouble() * Math.Tau);
        RadiusNow = Radius; State = Phase.Turning; _t = 0f; Circles++;
        int n = Mathf.Max(1, Count - Mathf.Clamp(Gap, 0, Count - 1));
        for (int i = 0; i < n; i++)
        {
            var b = new SmsCircleBoo { Name = $"CircleBoo{i + 1}" };
            AddChild(b);
            b.Setup(this, _scene, i);
            _boos.Add(b);
        }
        Place(0f);
        if (_sound != null && !string.IsNullOrEmpty(AppearSe)) _sound.Play(AppearSe);
        if (DebugPrint) GD.Print($"[SMS boo circle] {Name}: the RING -- {n} Boos on {Count} places ({Gap} empty) {Radius} m about {_centre}, {Height} m up, a turn every {RevSeconds} s {(_spin > 0 ? "anticlockwise" : "clockwise")}; holds {HoldSeconds} s, closes to {MinRadius} m by {Seconds} s, drifting {DriftSpeed} m/s");
        EmitSignal(SignalName.Formed, n);
    }

    /// <summary>Every Boo to its place on the ring: index i at angle + i x (2 pi / Count) -- the empty places are the last Gap indices.</summary>
    private void Place(float alpha)
    {
        int n = _boos.Count;
        for (int i = 0; i < n; i++)
        {
            var b = _boos[i]; if (b == null || !IsInstanceValid(b)) continue;
            float ang = _angle + i * MathF.Tau / Mathf.Max(1, Count);
            Vector3 p = new Vector3(_centre.X + MathF.Sin(ang) * RadiusNow, FloorY + Height, _centre.Z + MathF.Cos(ang) * RadiusNow);
            Vector3 face = FaceMiddle ? new Vector3(_centre.X - p.X, 0f, _centre.Z - p.Z) : new Vector3(MathF.Cos(ang), 0f, -MathF.Sin(ang)) * _spin;
            b.PlaceAt(p, face, alpha);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (State == Phase.Idle || State == Phase.Done) return;
        _t += dt;
        if (State == Phase.Turning)
        {
            _angle += dt * MathF.Tau / Mathf.Max(0.5f, RevSeconds) * _spin;
            float close = Mathf.Clamp((_t - HoldSeconds) / Mathf.Max(0.1f, Seconds - HoldSeconds), 0f, 1f);
            RadiusNow = Mathf.Lerp(Radius, MinRadius, close);
            if (_mario != null && GodotObject.IsInstanceValid(_mario) && DriftSpeed > 0f)                                 // after him, slowly, inside the arena
            {
                Vector3 d = _mario.GlobalPosition - _centre; d.Y = 0f; float dist = d.Length();
                if (dist > 0.2f) _centre += d / dist * MathF.Min(dist, DriftSpeed * dt);
                Vector3 cd = _centre - _arenaCentre; cd.Y = 0f; float cl = cd.Length(), lim = Mathf.Max(2f, _arenaRadius - WallInset);
                if (cl > lim) _centre = new Vector3(_arenaCentre.X + cd.X / cl * lim, _centre.Y, _arenaCentre.Z + cd.Z / cl * lim);
            }
            float alpha = FadeInSeconds > 0f ? Mathf.Clamp(_t / FadeInSeconds, 0f, 1f) : 1f;
            Place(alpha);
            if (_t >= Seconds || RadiusNow <= MinRadius + 0.01f) { State = Phase.Fading; _fadeT = 0f; if (_sound != null && !string.IsNullOrEmpty(GoneSe)) _sound.Play(GoneSe); if (DebugPrint) GD.Print($"[SMS boo circle] {Name}: closed to {RadiusNow:F2} m at {_t:F2} s -- the Boos fade ({Hits} hit{(Hits == 1 ? "" : "s")})"); }
        }
        else if (State == Phase.Fading)
        {
            _fadeT += dt; _angle += dt * MathF.Tau / Mathf.Max(0.5f, RevSeconds) * _spin;
            float k = FadeSeconds > 0f ? Mathf.Clamp(1f - _fadeT / FadeSeconds, 0f, 1f) : 0f;
            Place(k);
            if (!_puffed && VanishPuff && _puffScene != null) { _puffed = true; foreach (var b in _boos) if (b != null && IsInstanceValid(b)) SmsSmashFx.Puff(_puffScene, GetParent(), b.GlobalPosition, b.Radius * 1.3f, PuffTint); }
            if (k <= 0f) Finish();
        }
    }
    /// <summary>A Boo met Mario.</summary>
    public void BooHit() { Hits++; HitCount++; EmitSignal(SignalName.Hit); }
    private void Finish()
    {
        if (State == Phase.Done) return;
        State = Phase.Done; All.Remove(this);
        if (DebugPrint) GD.Print($"[SMS boo circle] {Name}: gone -- {Hits} hit{(Hits == 1 ? "" : "s")} over {_t:F2} s");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through it: the Boos fade now.</summary>
    public void Cancel()
    {
        if (State == Phase.Done || State == Phase.Fading) return;
        State = Phase.Fading; _fadeT = 0f;
        if (DebugPrint) GD.Print($"[SMS boo circle] {Name}: cut off at {_t:F2} s ({RadiusNow:F2} m)");
    }
}

/// <summary>201: one Boo of the circle -- the hotel Boo at its place on the ring (its TelesaToon materials copied per Boo and faded by `fade`; another scene gets
/// GhostBoo.gdshader), its wait clip playing, facing the ring's middle; Mario's damage cylinder in its body is the plain hit.</summary>
public partial class SmsCircleBoo : Node3D
{
    public SmsBooCircle Maker;
    public float Radius { get; private set; } = 0.7f;
    public float HalfHeight { get; private set; } = 0.6f;
    public float OpacityShown { get; private set; }
    public int Index { get; private set; }
    public bool UsesTelesaMaterials { get; private set; }
    public int HitsGiven { get; private set; }
    private Node3D _model; private AnimationPlayer _anim; private readonly List<ShaderMaterial> _mats = new(); private float _cool;

    public void Setup(SmsBooCircle owner, PackedScene scene, int index)
    {
        Maker = owner; Index = index;
        float k = owner.ModelScale * owner.BooScale;
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
                    if (mi.GetActiveMaterial(s) is ShaderMaterial own) { var copy = (ShaderMaterial)own.Duplicate(); copy.SetShaderParameter("fade", 0f); mi.SetSurfaceOverrideMaterial(s, copy); _mats.Add(copy); telesa++; continue; }
                    if (shader == null) continue;
                    var sm = new ShaderMaterial { Shader = shader };
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm && bm.AlbedoTexture != null) sm.SetShaderParameter("base_tex", bm.AlbedoTexture);
                    sm.SetShaderParameter("use_vertex_color", false); sm.SetShaderParameter("lift", 1f); sm.SetShaderParameter("glow", 0.25f); sm.SetShaderParameter("opacity", 0f);
                    mi.SetSurfaceOverrideMaterial(s, sm); _mats.Add(sm);
                }
            }
            UsesTelesaMaterials = telesa > 0 && telesa == _mats.Count;
            if (!first) { Radius = Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.5f * k; HalfHeight = aabb.Size.Y * 0.5f * k; }
            foreach (Node n in _model.FindChildren("*", "AnimationPlayer", true, false)) { _anim = n as AnimationPlayer; if (_anim != null) break; }
            if (_anim != null && !string.IsNullOrEmpty(owner.BooClip) && _anim.HasAnimation(owner.BooClip)) { _anim.GetAnimation(owner.BooClip).LoopMode = Animation.LoopModeEnum.Linear; _anim.Play(owner.BooClip); _anim.Seek(GD.Randf() * (float)_anim.GetAnimation(owner.BooClip).Length, true); }
        }
        if (owner.BooRadius > 0f) Radius = owner.BooRadius;
    }
    /// <summary>Its place and way this frame, its opacity (the fade in and out).</summary>
    public void PlaceAt(Vector3 at, Vector3 face, float alpha)
    {
        GlobalPosition = at;
        if (face.LengthSquared() > 1e-6f) Rotation = new Vector3(0f, MathF.Atan2(face.X, face.Z) + (Maker.FacesPlusZ ? 0f : MathF.PI), 0f);
        float o = Maker.BooOpacity * Mathf.Clamp(alpha, 0f, 1f);
        if (MathF.Abs(o - OpacityShown) > 1e-4f) { OpacityShown = o; foreach (var m in _mats) m.SetShaderParameter(UsesTelesaMaterials ? "fade" : "opacity", o); }
    }
    public override void _Process(double delta)
    {
        if (Maker == null || !IsInstanceValid(Maker) || Maker.State != SmsBooCircle.Phase.Turning || OpacityShown < 0.5f) return;
        if (_cool > 0f) { _cool -= (float)delta; return; }
        var mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        if (mario == null || mario.stateOfMario == Mario.MarioState.hurt || mario.stateOfMario == Mario.MarioState.dead) return;
        Vector3 m = mario.GlobalPosition, p = GlobalPosition;
        float r = Radius + SmsParams.Mario.DamageRadius / 74.44f;
        bool inY = p.Y + HalfHeight >= m.Y && p.Y - HalfHeight <= m.Y + SmsParams.Mario.DamageHeight / 74.44f;
        if (inY && new Vector2(p.X - m.X, p.Z - m.Z).Length() <= r)
        {
            int hp = mario.SmsHP;
            mario.TakeDamage(p, Mario.SmsDamageProfile.EnemyCommon);
            if (mario.SmsHP < hp || mario.stateOfMario == Mario.MarioState.hurt) { HitsGiven++; Maker.BooHit(); if (Maker.DebugPrint) GD.Print($"[SMS boo circle] {Name}: gets Mario at {m} (the Boo at {p}, the ring {Maker.RadiusNow:F2} m; hp {mario.SmsHP})"); }
            _cool = 0.5f;
        }
    }
}
