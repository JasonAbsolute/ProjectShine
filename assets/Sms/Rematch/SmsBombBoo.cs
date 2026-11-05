using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 158 / 159: a BOMB BOO (KINGBOO_REMATCH_gdoc.txt, HOW MARIO DEALS DAMAGE) -- Galaxy's black Boo, Mario's only weapon in the rematch. It comes
/// out of a black foggy spot (SmsBombBooSpawner), rises, then DRIFTS toward Mario (DriftSpeed, stopping DriftStopDistance short, HoverHeight over the
/// floor, bobbing, facing him). Mario's SPIN (X without the FLUDD) within his BombGrabRadius GRABS it BY THE TONGUE (159, as Galaxy has it): he holds
/// the tongue up in one hand (SmsLassoArm) and the Boo SWINGS ROUND HIM on it -- OrbitRadius out, OrbitOverHand over his hand, OrbitDegPerSec round,
/// leaning outward (OrbitLeanDeg), facing in, the tongue stretched from its mouth to his hand; his spin jump makes it swing faster (SpinJumpBoost); a
/// DIVE drops it (released, it drifts again after DropGraceSeconds). It is NOT thrown: swung into King Boo's body (SmsKingBooRematch.InHitCylinder) it
/// BURSTS on him and he TAKES A HIT -- Mario walks it into him. HoldSeconds (0 = never) and it bursts in his hand for nothing. Expires on its own after
/// ExpireSeconds drifting (0 = never); King Boo's hit clears every Boo on the field (the spawner). Model: models/Sms/Rematch/bomb_boo.glb +
/// bomb_boo_tongue.glb (Jaysonn's .dae pair through Blender, their vertex colours kept) at ModelScale, on BombBoo.gdshader -- the maroon body texture
/// with its yellow eyes x the dark vertex colours x Lift, Opacity 1 (159: 158's was purple and see-through), the eyes glowing a little (FaceGlow), a
/// faint violet rim (Glow). Signals: Grabbed, Dropped, Burst(hitKingBoo).
/// </summary>
public partial class SmsBombBoo : Node3D
{
    public enum Phase { Rising, Drifting, Carried, Gone }
    /// <summary>The tongue glb's length along its +Z (its root at the origin), metres at scale 1 (76.6 cm in the .dae).</summary>
    public const float TongueModelLength = 0.766f;

    [ExportGroup("Look")]
    /// <summary>The glb at this scale (Galaxy's centimetres read as metres make the body 2.5 m wide: 0.8 = 2 m, 1.7 m tall against Mario's 2.15).</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float ModelScale = 0.8f;
    /// <summary>1 = solid (159). Galaxy's own is about 0.6 (the material's alpha 150 / 255) -- a knob.</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float Opacity = 1f;
    /// <summary>The body's brightness: texture x (vertex colour + Ambient) x Lift (2 = the game's stage doubling; the body stays a near-black red, the eyes bright).</summary>
    [Export(PropertyHint.Range, "0.5,4,0.05")] public float Lift = 2f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Ambient = 0f;
    /// <summary>The eyes' own light (the bright texels) and the faint rim.</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float FaceGlow = 0.5f;
    [Export] public Color GlowColor = new Color(0.35f, 0.1f, 0.55f);
    [Export(PropertyHint.Range, "0,3,0.05")] public float Glow = 0.25f;
    /// <summary>The tongue's root at the mouth (model metres, before ModelScale: the body is 2.5 m wide, its front 1.1 m ahead of the origin), how far
    /// it droops when it just hangs (degrees), and whether the model's face is on its +Z side (off: -Z).</summary>
    [Export] public Vector3 TongueOffset = new Vector3(0f, -0.25f, 1f);
    [Export(PropertyHint.Range, "-60,80,1")] public float TongueRestDroopDeg = 35f;
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
    [ExportGroup("The swing (carried by the tongue)")]
    /// <summary>Round Mario: this far out, this far OVER his raised hand (his arms are short next to his head: the raised wrist comes to his ear, 1.3 m up on a 2.15 m body -- 0.9 puts the Boo
    /// just over his head, the tongue rising to it), this fast (degrees a second; 480 = a turn in 0.75 s), leaning this far outward.</summary>
    [Export] public float OrbitRadius = 2.8f, OrbitOverHand = 0.9f, OrbitDegPerSec = 480f, OrbitLeanDeg = 25f;
    /// <summary>From the grab to the full swing (the radius and the height ease in).</summary>
    [Export] public float OrbitSettleSeconds = 0.35f;
    [Export] public bool OrbitClockwise = false;
    /// <summary>His spin jump: the swing this much faster for this long.</summary>
    [Export] public float SpinJumpBoost = 1.7f, SpinJumpBoostSeconds = 2.5f;
    /// <summary>Dropped: no grab again for this long (he has to spin for it again anyway).</summary>
    [Export] public float DropGraceSeconds = 0.6f;
    /// <summary>Carried this long, it bursts in his hand for nothing (Galaxy's held Bomb Boo blows up on its own; 0 = never). It flashes faster over its last 2 s.</summary>
    [Export] public float HoldSeconds = 0f;
    /// <summary>The Boo's own reach when it swings into King Boo (model metres, x ModelScale: the body is 1.27 m to its side).</summary>
    [Export] public float OwnRadius = 1f;
    [ExportGroup("Sound (empty = silent)")]
    [Export] public string AppearSe = "MSD_SE_EN_TELESA_APPEAR", GrabSe = "MSD_SE_EN_TELESA_FIX", BurstSe = "MSD_SE_EN_TELESA_CRUSH", GoneSe = "MSD_SE_EN_TELESA_DISAPPEAR";
    [ExportGroup("")]
    [Export] public PackedScene BurstFxScene;
    [Export] public Color BurstColor = new Color(0.55f, 0.2f, 0.85f);
    [Export] public bool DebugPrint;

    [Signal] public delegate void GrabbedEventHandler();
    [Signal] public delegate void DroppedEventHandler();
    [Signal] public delegate void BurstEventHandler(bool hitKingBoo);

    public static readonly List<SmsBombBoo> All = new();
    public Phase State { get; private set; } = Phase.Rising;
    public bool IsFree => State == Phase.Drifting;
    public bool IsCarried => State == Phase.Carried;
    public float OrbitSpeedNow => OrbitDegPerSec * (_boostLeft > 0f ? SpinJumpBoost : 1f);
    public float Age { get; private set; }
    /// <summary>For the drives: the swing's radius and angle now, the tongue's length and tip (his hand), the mouth, the materials.</summary>
    public float OrbitRadiusNow { get; private set; }
    public float OrbitAngle => _orbitAngle;
    public float TongueLength { get; private set; }
    public Vector3 TongueTip { get; private set; }
    public Vector3 MouthPoint => GlobalTransform * (TongueOffset * ModelScale);
    public int MaterialCount => _mats.Count;
    public Node3D TongueNode => _tongue;

    private Node3D _model, _tongue; private readonly List<ShaderMaterial> _mats = new();
    private Mario _mario; private SmsKingBooRematch _boss; private SmsActorSound _sound;
    private float _t, _bobT, _yaw, _boostLeft, _grace, _floorY = float.NaN; private int _floorTick;
    private float _orbitAngle, _settle, _r0, _y0, _held;
    private Rid _marioRid; private bool _marioRidKnown;

    public override void _Ready()
    {
        All.Add(this);
        _model = GetNodeOrNull<Node3D>("Model"); _tongue = GetNodeOrNull<Node3D>("Tongue");
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/BombBoo.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/BombBoo.gdshader") : null;
        foreach (var root in new[] { _model, _tongue })
        {
            if (root == null) continue;
            if (shader == null) continue;
            foreach (Node n in root.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    var sm = new ShaderMaterial { Shader = shader };
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm && bm.AlbedoTexture != null) sm.SetShaderParameter("base_tex", bm.AlbedoTexture);
                    sm.SetShaderParameter("opacity", Opacity); sm.SetShaderParameter("lift", Lift); sm.SetShaderParameter("ambient", Ambient);
                    sm.SetShaderParameter("face_glow", root == _tongue ? 0f : FaceGlow);
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

    /// <summary>The body and the hanging tongue at the model scale x k (the rise).</summary>
    private void SetScaleK(float k)
    {
        if (_model != null) _model.Scale = Vector3.One * ModelScale * k;
        TongueRest(k);
    }
    /// <summary>The tongue just hanging out of the mouth: its root at TongueOffset, drooping TongueRestDroopDeg.</summary>
    private void TongueRest(float k = 1f)
    {
        if (_tongue == null) return;
        _tongue.Transform = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(TongueRestDroopDeg)).Scaled(Vector3.One * ModelScale * k), TongueOffset * ModelScale * k);
        TongueLength = TongueModelLength * ModelScale * k; TongueTip = _tongue.GlobalTransform * new Vector3(0f, 0f, TongueModelLength);
    }
    /// <summary>The tongue stretched from the mouth to <paramref name="hand"/>: its +Z along the line, scaled to the length, its width kept.</summary>
    private void TongueTo(Vector3 hand)
    {
        if (_tongue == null) return;
        Vector3 mouth = MouthPoint;
        Vector3 d = hand - mouth; float len = d.Length();
        if (len < 0.05f) { TongueRest(); return; }
        Vector3 z = d / len;
        Vector3 x = Vector3.Up.Cross(z); if (x.LengthSquared() < 1e-6f) x = Vector3.Right.Cross(z); x = x.Normalized();
        Vector3 y = z.Cross(x);
        _tongue.GlobalTransform = new Transform3D(new Basis(x * ModelScale, y * ModelScale, z * (len / TongueModelLength)), mouth);
        TongueLength = len; TongueTip = hand;
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
                TongueRest();
                break;
            }
            case Phase.Carried:
            {
                if (mario == null || !IsInstanceValid(mario)) { Drop(); return; }
                Vector3 hand = mario.SmsLassoHand;
                _settle = Mathf.Min(1f, _settle + dt / Mathf.Max(0.01f, OrbitSettleSeconds));
                float k = Mathf.SmoothStep(0f, 1f, _settle);
                _orbitAngle += Mathf.DegToRad(OrbitSpeedNow) * dt * (OrbitClockwise ? -1f : 1f);
                float r = Mathf.Lerp(_r0, OrbitRadius, k);
                float y = Mathf.Lerp(_y0, hand.Y + OrbitOverHand, k);
                Vector3 c = mario.GlobalPosition;
                Vector3 p = new Vector3(c.X + MathF.Cos(_orbitAngle) * r, y, c.Z + MathF.Sin(_orbitAngle) * r);
                OrbitRadiusNow = r;
                // facing in at Mario (the tongue in its mouth leads to his hand), leaning outward with the swing
                Vector3 outward = new Vector3(p.X - c.X, 0f, p.Z - c.Z);
                Basis b = GlobalTransform.Basis.Orthonormalized();
                if (outward.LengthSquared() > 1e-6f)
                {
                    outward = outward.Normalized();
                    _yaw = MathF.Atan2(-outward.X, -outward.Z) + (FacesPlusZ ? 0f : MathF.PI);
                    b = new Basis(Vector3.Up, _yaw);
                    Vector3 tangent = Vector3.Up.Cross(outward);
                    if (tangent.LengthSquared() > 1e-6f) b = b.Rotated(tangent.Normalized(), Mathf.DegToRad(OrbitLeanDeg) * k);
                }
                GlobalTransform = new Transform3D(b, p);
                TongueTo(hand);
                float pulse = _boostLeft > 0f ? 0.2f + 0.2f * MathF.Sin(_t * 18f) : 0.05f + 0.05f * MathF.Sin(_t * 9f);
                if (HoldSeconds > 0f)
                {
                    _held += dt;
                    float left = HoldSeconds - _held;
                    if (left <= 0f) { Explode(false); return; }
                    if (left < 2f) pulse = 0.3f + 0.3f * MathF.Sin(_t * (12f + (2f - left) * 14f));
                }
                SetFlash(pulse);
                var boss = BossNow();
                if (boss != null && boss.CanBeHit && boss.InHitCylinder(p, OwnRadius * ModelScale)) { Explode(true); return; }
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

    /// <summary>Mario's spin reached it: he has the tongue -- it swings round him from where it is (false when not free, or within the drop's grace).</summary>
    public bool Grab(Mario mario)
    {
        if (State != Phase.Drifting || _grace > 0f || mario == null) return false;
        _mario = mario; _marioRidKnown = false;
        State = Phase.Carried; _t = 0f; _boostLeft = 0f; _held = 0f; _settle = 0f;
        Vector3 rel = GlobalPosition - mario.GlobalPosition;
        _orbitAngle = MathF.Atan2(rel.Z, rel.X);
        _r0 = MathF.Max(0.3f, new Vector2(rel.X, rel.Z).Length()); _y0 = GlobalPosition.Y;
        OrbitRadiusNow = _r0;
        if (!string.IsNullOrEmpty(GrabSe)) _sound?.Play(GrabSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: grabbed by the tongue at {_r0:F2} m -- the swing");
        EmitSignal(SignalName.Grabbed);
        return true;
    }
    /// <summary>His spin jump while he swings it: faster for a while.</summary>
    public void SpinBoost() { if (State == Phase.Carried) { _boostLeft = SpinJumpBoostSeconds; if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: the swing x{SpinJumpBoost} for {SpinJumpBoostSeconds} s"); } }
    /// <summary>Let go (a dive, a hit, a death): it drifts again from where the swing left it; no grab for DropGraceSeconds.</summary>
    public void Drop()
    {
        if (State != Phase.Carried) return;
        State = Phase.Drifting; _t = 0f; _grace = DropGraceSeconds; _boostLeft = 0f; SetFlash(0f);
        Rotation = new Vector3(0f, _yaw, 0f); TongueRest();
        _floorY = FloorUnder(GlobalPosition);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: let go");
        EmitSignal(SignalName.Dropped);
    }
    /// <summary>The burst: the puff (BurstFxScene recoloured), the sound, King Boo's hit when it was swung into him, Mario's hand emptied, and the Boo gone.</summary>
    public void Explode(bool hitKingBoo)
    {
        if (State == Phase.Gone) return;
        if (State == Phase.Carried) MarioNow()?.SmsBombGone(this);
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
