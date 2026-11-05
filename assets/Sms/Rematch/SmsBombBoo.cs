using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 158: a BOMB BOO (KINGBOO_REMATCH_gdoc.txt, HOW MARIO DEALS DAMAGE) -- Galaxy's black Boo, Mario's only weapon in the rematch.
/// It comes out of a black foggy spot (SmsBombBooSpawner), rises, then DRIFTS toward Mario (DriftSpeed, stopping DriftStopDistance short,
/// HoverHeight over the floor, bobbing, facing him). Mario's grounded SPIN (X without the FLUDD) within his BombGrabRadius GRABS it: it
/// spins above his head (CarrySpin degrees a second; his spin jump makes it spin faster, SpinJumpBoost); a DIVE drops it (released, it drifts
/// again after a moment); another SPIN THROWS it the way he faces (Mario's BombThrowSpeed / BombThrowUp, the flight under ThrowGravity):
/// on King Boo (KingBooHitRadius of his body) it bursts and he TAKES A HIT; on the floor or a wall, or ThrowSeconds out, it bursts for
/// nothing. Expires on its own after ExpireSeconds (0 = never); King Boo's hit clears every Boo on the field (the spawner). Model:
/// models/Sms/Rematch/bomb_boo.glb + bomb_boo_tongue.glb (the .dae pair through Blender, their vertex colours kept) at ModelScale, on
/// BombBoo.gdshader (opacity, the violet rim). Signals: Grabbed, Dropped, Thrown, Burst(hitKingBoo).
/// </summary>
public partial class SmsBombBoo : Node3D
{
    public enum Phase { Rising, Drifting, Carried, Thrown, Gone }

    [ExportGroup("Look")]
    /// <summary>The glb at this scale (Galaxy's centimetres read as metres make the body 2.5 m wide: 0.8 = 2 m, 1.7 m tall against Mario's 2.15).</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float ModelScale = 0.8f;
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float Opacity = 0.78f;
    [Export] public Color GlowColor = new Color(0.55f, 0.15f, 0.85f);
    [Export(PropertyHint.Range, "0,3,0.05")] public float Glow = 0.9f;
    /// <summary>The tongue model's place under the body's origin (the mouth), and whether the model's face is on its +Z side (off: -Z).</summary>
    [Export] public Vector3 TongueOffset = new Vector3(0f, -0.3f, 0.6f);
    [Export] public bool FacesPlusZ = true;
    [ExportGroup("Drift")]
    /// <summary>Toward Mario at this speed (m/s), stopping this far from him; this high over the floor under it; the bob's height and period.</summary>
    [Export] public float DriftSpeed = 1.6f, DriftStopDistance = 1.6f, HoverHeight = 1.3f;
    [Export] public float BobHeight = 0.2f, BobSeconds = 1.8f;
    [Export] public float TurnSpeed = 4f;
    /// <summary>Gone on its own after this long drifting (0 = never).</summary>
    [Export] public float ExpireSeconds = 0f;
    /// <summary>Out of the fog: the rise's length (it scales up and lifts out over it).</summary>
    [Export] public float RiseSeconds = 0.7f;
    [ExportGroup("Carry")]
    /// <summary>Over Mario's head: its centre this far over his height; its spin (degrees a second); the spin jump's boost (x) and how long it lasts.</summary>
    [Export] public float CarryOver = 0.4f, CarrySpin = 360f, SpinJumpBoost = 2f, SpinJumpBoostSeconds = 2.5f;
    /// <summary>Dropped: no grab again for this long (he has to spin for it again anyway).</summary>
    [Export] public float DropGraceSeconds = 0.6f;
    [ExportGroup("Throw")]
    [Export] public float ThrowGravity = 9f, ThrowSeconds = 2.5f;
    /// <summary>The burst on King Boo: within this of his body point.</summary>
    [Export] public float KingBooHitRadius = 2.4f;
    [ExportGroup("Sound (empty = silent)")]
    [Export] public string AppearSe = "MSD_SE_EN_TELESA_APPEAR", GrabSe = "MSD_SE_EN_TELESA_FIX", BurstSe = "MSD_SE_EN_TELESA_CRUSH", GoneSe = "MSD_SE_EN_TELESA_DISAPPEAR";
    [ExportGroup("")]
    [Export] public PackedScene BurstFxScene;
    [Export] public Color BurstColor = new Color(0.55f, 0.2f, 0.85f);
    [Export] public bool DebugPrint;

    [Signal] public delegate void GrabbedEventHandler();
    [Signal] public delegate void DroppedEventHandler();
    [Signal] public delegate void ThrownEventHandler();
    [Signal] public delegate void BurstEventHandler(bool hitKingBoo);

    public static readonly List<SmsBombBoo> All = new();
    public Phase State { get; private set; } = Phase.Rising;
    public bool IsFree => State == Phase.Drifting;
    public bool IsCarried => State == Phase.Carried;
    public float SpinDegPerSec => CarrySpin * (_boostLeft > 0f ? SpinJumpBoost : 1f);
    public float Age { get; private set; }
    public int Throws { get; private set; }
    public Vector3 Velocity => _vel;

    private Node3D _model, _tongue; private readonly List<ShaderMaterial> _mats = new();
    private Mario _mario; private SmsKingBooRematch _boss; private SmsActorSound _sound;
    private float _t, _bobT, _yaw, _spinYaw, _boostLeft, _grace, _floorY = float.NaN; private int _floorTick;
    private Vector3 _vel; private float _flight;
    private Rid _marioRid; private bool _marioRidKnown;

    public override void _Ready()
    {
        All.Add(this);
        _model = GetNodeOrNull<Node3D>("Model"); _tongue = GetNodeOrNull<Node3D>("Tongue");
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/BombBoo.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/BombBoo.gdshader") : null;
        foreach (var root in new[] { _model, _tongue })
        {
            if (root == null) continue;
            root.Scale = Vector3.One * ModelScale;
            if (root == _tongue) root.Position = TongueOffset * ModelScale;
            if (shader == null) continue;
            foreach (Node n in root.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    var sm = new ShaderMaterial { Shader = shader };
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm && bm.AlbedoTexture != null) sm.SetShaderParameter("base_tex", bm.AlbedoTexture);
                    sm.SetShaderParameter("opacity", Opacity);
                    sm.SetShaderParameter("glow_color", new Vector3(GlowColor.R, GlowColor.G, GlowColor.B));
                    sm.SetShaderParameter("glow", Glow);
                    mi.SetSurfaceOverrideMaterial(s, sm);
                    _mats.Add(sm);
                }
            }
        }
        BurstFxScene ??= ResourceLoader.Exists("res://assets/Sms/Fx/EnemyDisap.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Fx/EnemyDisap.tscn") : null;
        _sound = SmsActorSound.Attach(this);
        _yaw = Rotation.Y;
        SetScaleK(0.01f);
        if (!string.IsNullOrEmpty(AppearSe)) _sound?.Play(AppearSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: {_mats.Count} materials, at {GlobalPosition}");
    }
    public override void _ExitTree() { All.Remove(this); }

    private void SetScaleK(float k)
    {
        if (_model != null) _model.Scale = Vector3.One * ModelScale * k;
        if (_tongue != null) { _tongue.Scale = Vector3.One * ModelScale * k; _tongue.Position = TongueOffset * ModelScale * k; }
    }
    private void SetFlash(float f) { foreach (var m in _mats) m.SetShaderParameter("flash", f); }

    private Mario MarioNow()
    {
        if (_mario == null || !IsInstanceValid(_mario)) { _mario = GetTree().GetFirstNodeInGroup("player") as Mario; _marioRidKnown = false; }
        if (_mario != null && !_marioRidKnown) { _marioRid = _mario.GetRid(); _marioRidKnown = true; }
        return _mario;
    }
    private SmsKingBooRematch BossNow()
    {
        if (_boss == null || !IsInstanceValid(_boss)) _boss = GetTree().GetFirstNodeInGroup("sms_rematch_boss") as SmsKingBooRematch;
        return _boss;
    }

    /// <summary>The floor under (x, z): a ray from 3 m over the point, Mario's body left out; NaN = none.</summary>
    private float FloorUnder(Vector3 p)
    {
        var space = GetWorld3D()?.DirectSpaceState; if (space == null) return float.NaN;
        var q = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 3f, p + Vector3.Down * 30f);
        q.CollideWithAreas = false; q.CollideWithBodies = true;
        if (_marioRidKnown) q.Exclude = new Godot.Collections.Array<Rid> { _marioRid };
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].AsVector3().Y : float.NaN;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || State == Phase.Gone) return;
        float dt = (float)delta; Age += dt; _t += dt;
        if (_boostLeft > 0f) _boostLeft -= dt;
        if (_grace > 0f) _grace -= dt;
        var mario = MarioNow();
        switch (State)
        {
            case Phase.Rising:
            {
                float k = RiseSeconds > 0f ? Mathf.Clamp(_t / RiseSeconds, 0f, 1f) : 1f;
                SetScaleK(Mathf.Max(0.01f, Mathf.SmoothStep(0f, 1f, k)));
                if (float.IsNaN(_floorY)) _floorY = FloorUnder(GlobalPosition);
                float target = (float.IsNaN(_floorY) ? GlobalPosition.Y : _floorY) + HoverHeight;
                GlobalPosition = GlobalPosition with { Y = Mathf.Lerp(GlobalPosition.Y, target, k) };
                FaceMario(mario, dt);
                if (k >= 1f) { State = Phase.Drifting; _t = 0f; SetScaleK(1f); }
                break;
            }
            case Phase.Drifting:
            {
                if (ExpireSeconds > 0f && _t >= ExpireSeconds) { Vanish(); return; }
                if ((++_floorTick & 7) == 0) { float f = FloorUnder(GlobalPosition); if (!float.IsNaN(f)) _floorY = f; }
                Vector3 p = GlobalPosition;
                if (mario != null)
                {
                    Vector3 d = mario.GlobalPosition - p; d.Y = 0f;
                    float dist = d.Length();
                    if (dist > DriftStopDistance && dist > 1e-3f) p += d / dist * Mathf.Min(DriftSpeed * dt, dist - DriftStopDistance);
                }
                _bobT += dt;
                float baseY = float.IsNaN(_floorY) ? p.Y : _floorY + HoverHeight;
                p.Y = baseY + (BobSeconds > 0f ? MathF.Sin(_bobT / BobSeconds * MathF.Tau) * BobHeight : 0f);
                GlobalPosition = p;
                FaceMario(mario, dt);
                break;
            }
            case Phase.Carried:
            {
                if (mario == null || !IsInstanceValid(mario)) { Drop(); return; }
                float h = SmsUnits.UnitsToMetres(SmsUnits.MarioHeightUnits) + CarryOver + (_model != null ? 1.05f * ModelScale : 0.6f);
                GlobalPosition = mario.GlobalPosition + Vector3.Up * h;
                _spinYaw += Mathf.DegToRad(SpinDegPerSec) * dt;
                Rotation = new Vector3(0f, _spinYaw, 0f);
                SetFlash(_boostLeft > 0f ? 0.25f + 0.25f * MathF.Sin(_t * 18f) : 0.08f + 0.08f * MathF.Sin(_t * 9f));
                break;
            }
            case Phase.Thrown:
            {
                _flight += dt;
                Vector3 p0 = GlobalPosition;
                _vel.Y -= ThrowGravity * dt;
                Vector3 p1 = p0 + _vel * dt;
                _spinYaw += Mathf.DegToRad(SpinDegPerSec) * dt;
                Rotation = new Vector3(0f, _spinYaw, 0f);
                var boss = BossNow();
                if (boss != null && boss.CanBeHit)
                {
                    // the sweep p0 -> p1 against his body point
                    Vector3 b = boss.BodyPoint; Vector3 seg = p1 - p0; float len2 = seg.LengthSquared();
                    float u = len2 > 1e-6f ? Mathf.Clamp((b - p0).Dot(seg) / len2, 0f, 1f) : 0f;
                    if ((p0 + seg * u).DistanceTo(b) <= KingBooHitRadius) { GlobalPosition = p0 + seg * u; Explode(true); return; }
                }
                var space = GetWorld3D()?.DirectSpaceState;
                if (space != null)
                {
                    var q = PhysicsRayQueryParameters3D.Create(p0, p1 + _vel.Normalized() * 0.3f);
                    q.CollideWithAreas = false; q.CollideWithBodies = true;
                    if (_marioRidKnown) q.Exclude = new Godot.Collections.Array<Rid> { _marioRid };
                    var hit = space.IntersectRay(q);
                    if (hit.Count > 0) { GlobalPosition = hit["position"].AsVector3(); Explode(false); return; }
                }
                GlobalPosition = p1;
                if (_flight >= ThrowSeconds || p1.Y < (float.IsNaN(_floorY) ? -50f : _floorY - 30f)) { Explode(false); return; }
                break;
            }
        }
    }

    private void FaceMario(Mario mario, float dt)
    {
        if (mario == null) return;
        Vector3 d = mario.GlobalPosition - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() < 1e-4f) return;
        float want = MathF.Atan2(d.X, d.Z) + (FacesPlusZ ? 0f : MathF.PI);
        _yaw = Mathf.LerpAngle(_yaw, want, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
        Rotation = new Vector3(0f, _yaw, 0f);
    }

    /// <summary>Mario's spin reached it: carried from now (false when not free, or within the drop's grace).</summary>
    public bool Grab(Mario mario)
    {
        if (State != Phase.Drifting || _grace > 0f || mario == null) return false;
        _mario = mario; _marioRidKnown = false;
        State = Phase.Carried; _t = 0f; _spinYaw = _yaw; _boostLeft = 0f;
        if (!string.IsNullOrEmpty(GrabSe)) _sound?.Play(GrabSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: grabbed");
        EmitSignal(SignalName.Grabbed);
        return true;
    }
    /// <summary>His spin jump while he carries it: the Boo spins faster for a while.</summary>
    public void SpinBoost() { if (State == Phase.Carried) { _boostLeft = SpinJumpBoostSeconds; if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: spin boost x{SpinJumpBoost}"); } }
    /// <summary>Dropped (a dive, a hit, a death): it drifts again; no grab for DropGraceSeconds.</summary>
    public void Drop()
    {
        if (State != Phase.Carried) return;
        State = Phase.Drifting; _t = 0f; _grace = DropGraceSeconds; _boostLeft = 0f; SetFlash(0f);
        _yaw = _spinYaw; _floorY = FloorUnder(GlobalPosition);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: dropped");
        EmitSignal(SignalName.Dropped);
    }
    /// <summary>Thrown: the flight from here with this velocity.</summary>
    public void Throw(Vector3 velocity)
    {
        if (State != Phase.Carried) return;
        State = Phase.Thrown; _vel = velocity; _flight = 0f; Throws++;
        _floorY = FloorUnder(GlobalPosition);
        SetFlash(0.35f);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: thrown at {velocity} ({velocity.Length():F1} m/s)");
        EmitSignal(SignalName.Thrown);
    }
    /// <summary>The burst: the puff (BurstFxScene recoloured), the sound, King Boo's hit when it was him, and the Boo gone.</summary>
    public void Explode(bool hitKingBoo)
    {
        if (State == Phase.Gone) return;
        State = Phase.Gone;
        Puff(hitKingBoo ? 1.6f : 1.1f);
        if (!string.IsNullOrEmpty(BurstSe)) _sound?.Play(BurstSe);
        if (hitKingBoo) BossNow()?.TakeHit(this);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: burst{(hitKingBoo ? " on King Boo" : "")} at {GlobalPosition}");
        EmitSignal(SignalName.Burst, hitKingBoo);
        Gone();
    }
    /// <summary>Cleared (King Boo's hit clears the field; the expiry): a small puff and gone.</summary>
    public void Vanish()
    {
        if (State == Phase.Gone) return;
        if (State == Phase.Carried) MarioNow()?.SmsBombGone(this);
        State = Phase.Gone;
        Puff(0.9f);
        if (!string.IsNullOrEmpty(GoneSe)) _sound?.Play(GoneSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: vanishes");
        Gone();
    }
    private void Puff(float scale)
    {
        if (BurstFxScene == null || !IsInsideTree()) return;
        var fx = BurstFxScene.Instantiate<Node3D>();
        fx.Transform = new Transform3D(Basis.FromScale(Vector3.One * scale), GlobalPosition);
        GetParent().AddChild(fx);
        if (fx is SmsFxBurst b)
        {
            b.SetShaderParam("prm", new Vector4(BurstColor.R, BurstColor.G, BurstColor.B, 1f));
            b.SetShaderParam("env", new Vector4(BurstColor.R * 0.4f, BurstColor.G * 0.4f, BurstColor.B * 0.4f, 1f));
        }
    }
    private void Gone()
    {
        // the sound node plays out on its own: the Boo's visuals go now, the node a moment later
        if (_model != null) _model.Visible = false;
        if (_tongue != null) _tongue.Visible = false;
        SetPhysicsProcess(false);
        if (IsInsideTree()) GetTree().CreateTimer(2.0).Timeout += QueueFree; else QueueFree();
    }
}
