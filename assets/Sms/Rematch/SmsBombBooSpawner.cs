using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 158: the BOMB BOO SUPPLY (KINGBOO_REMATCH_gdoc.txt: "Black foggy spots appear at random through the fight and a Boo bomb comes out
/// of each one... The spawn rate, the cap on how many are out at once and whether one expires on its own are all knobs"). From the fight's
/// start (the intro's FightReady; at once without an intro, or StartAtOnce) every SpawnInterval seconds, while fewer than MaxOut Boos are out
/// and King Boo stands: a black foggy spot (FogSeconds of dark smoke at the point) and a Bomb Boo rising out of it. Where: SpawnMode --
/// Centre (this node's point: for the playtests, as Jaysonn asked), Random (anywhere within ArenaRadius of this node, at least MinFromMario
/// from Mario), the floor found under the point. King Boo's hit clears every Boo on the field (the doc) and the supply pauses ClearPause
/// seconds; his defeat stops it. Knobs: the timing (FirstDelay, SpawnInterval, FogSeconds, ClearPause), the cap (MaxOut), the Boo's own
/// (ExpireSeconds and the rest on BombBooScene's root), the place (SpawnMode, ArenaRadius, MinFromMario, FloorOffset).
/// </summary>
public partial class SmsBombBooSpawner : Node3D
{
    public enum Modes { Centre, Random }

    [Export] public PackedScene BombBooScene;
    [Export] public Modes SpawnMode = Modes.Centre;
    /// <summary>Random: within this of this node (x and z), and never nearer Mario than MinFromMario.</summary>
    [Export] public float ArenaRadius = 18f, MinFromMario = 4f;
    [ExportGroup("Supply")]
    [Export] public float FirstDelay = 1.5f, SpawnInterval = 6f, FogSeconds = 1.1f, ClearPause = 2f;
    [Export(PropertyHint.Range, "1,12,1")] public int MaxOut = 2;
    /// <summary>The fog's and the Boo's point: this far over the floor found under the spawn point.</summary>
    [Export] public float FloorOffset = 0.3f;
    /// <summary>Start without waiting for the intro's FightReady.</summary>
    [Export] public bool StartAtOnce = false;
    [Export] public bool Enabled = true;
    [ExportGroup("Fog")]
    [Export] public Color FogColor = new Color(0.12f, 0.03f, 0.2f, 0.85f);
    [Export] public float FogRadius = 1.1f;
    [Export] public Texture2D FogTexture;
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void SpawnedEventHandler(SmsBombBoo boo);

    public bool Running { get; private set; }
    public int SpawnCount { get; private set; }
    public int OutNow { get { int n = 0; foreach (var b in SmsBombBoo.All) if (b != null && IsInstanceValid(b) && b.State != SmsBombBoo.Phase.Gone) n++; return n; } }
    public float NextIn => _timer;

    private float _timer; private bool _fogging; private GpuParticles3D _fog; private Vector3 _fogAt; private float _fogT;
    private SmsKingBooRematch _boss; private SmsRematchIntro _intro; private Mario _mario; private readonly Random _rng = new(158);

    public override void _Ready()
    {
        BombBooScene ??= ResourceLoader.Exists("res://assets/Sms/Rematch/BombBoo.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Rematch/BombBoo.tscn") : null;
        FogTexture ??= ResourceLoader.Exists("res://assets/Sms/Fx/tex/kemuball3_dark.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/kemuball3_dark.png") : null;
        if (Engine.IsEditorHint()) return;
        SmsBombSupplyDriveHook();
        CallDeferred(nameof(Wire));
    }
    partial void SmsBombSupplyDriveHook();

    private void Wire()
    {
        _boss = GetTree().GetFirstNodeInGroup("sms_rematch_boss") as SmsKingBooRematch;
        if (_boss != null) { _boss.Hit += OnBossHit; _boss.Defeated += Stop; }
        foreach (Node n in GetTree().CurrentScene?.FindChildren("*", "", true, false) ?? new Godot.Collections.Array<Node>())
            if (n is SmsRematchIntro i) { _intro = i; break; }
        if (StartAtOnce || _intro == null || _intro.State == SmsRematchIntro.Phase.Done) Start();
        else _intro.FightReady += Start;
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: mode {SpawnMode}, every {SpawnInterval} s, {MaxOut} out at most; boss {(_boss != null ? "found" : "none")}, intro {(_intro != null ? "waits" : "none")}");
    }

    public void Start()
    {
        if (Running || !Enabled) return;
        Running = true; _timer = FirstDelay;
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: the supply starts (first in {FirstDelay} s)");
    }
    public void Stop() { Running = false; StopFog(); }

    private void OnBossHit(int hpLeft)
    {
        ClearAll();
        _timer = Mathf.Max(_timer, ClearPause);
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: King Boo hit ({hpLeft} left) -- the field cleared, next in {_timer:F1} s");
    }

    /// <summary>Every Boo on the field gone (the doc: King Boo's hit clears them), the fog with them.</summary>
    public void ClearAll()
    {
        foreach (var b in new List<SmsBombBoo>(SmsBombBoo.All)) if (b != null && IsInstanceValid(b)) b.Vanish();
        StopFog();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Running) return;
        float dt = (float)delta;
        if (_fogging)
        {
            _fogT += dt;
            if (_fogT >= FogSeconds) { _fogging = false; SpawnBoo(_fogAt); if (_fog != null) _fog.Emitting = false; }
            return;
        }
        if (_boss != null && !_boss.IsAlive) { Stop(); return; }
        if (OutNow >= MaxOut) return;
        _timer -= dt;
        if (_timer > 0f) return;
        _timer = SpawnInterval;
        Vector3 at = PickPoint();
        StartFog(at);
    }

    private Vector3 PickPoint()
    {
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        Vector3 p = GlobalPosition;
        if (SpawnMode == Modes.Random)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                float a = (float)(_rng.NextDouble() * Math.PI * 2.0), r = ArenaRadius * MathF.Sqrt((float)_rng.NextDouble());
                Vector3 c = GlobalPosition + new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
                if (_mario == null || new Vector2(c.X - _mario.GlobalPosition.X, c.Z - _mario.GlobalPosition.Z).Length() >= MinFromMario) { p = c; break; }
                p = c;
            }
        }
        float? floor = FloorUnder(p);
        if (floor.HasValue) p.Y = floor.Value + FloorOffset;
        return p;
    }

    private float? FloorUnder(Vector3 p)
    {
        var space = GetWorld3D()?.DirectSpaceState; if (space == null) return null;
        var q = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 20f, p + Vector3.Down * 40f);
        q.CollideWithAreas = false; q.CollideWithBodies = true;
        if (_mario != null && IsInstanceValid(_mario)) q.Exclude = new Godot.Collections.Array<Rid> { _mario.GetRid() };
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].AsVector3().Y : null;
    }

    private void StartFog(Vector3 at)
    {
        _fogging = true; _fogT = 0f; _fogAt = at;
        if (_fog == null)
        {
            _fog = new GpuParticles3D { Name = "Fog", TopLevel = true, Amount = 36, Lifetime = 1.2, OneShot = false, Emitting = false, FixedFps = 60,
                VisibilityAabb = new Aabb(new Vector3(-4f, -2f, -4f), new Vector3(8f, 6f, 8f)) };
            var pm = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = FogRadius, Direction = Vector3.Up, Spread = 60f,
                InitialVelocityMin = 0.2f, InitialVelocityMax = 0.9f, Gravity = new Vector3(0f, 0.4f, 0f), ScaleMin = 0.7f, ScaleMax = 1.5f, LifetimeRandomness = 0.4f
            };
            var ramp = new Gradient();
            ramp.SetColor(0, new Color(FogColor.R, FogColor.G, FogColor.B, 0f)); ramp.SetColor(1, new Color(FogColor.R, FogColor.G, FogColor.B, 0f));
            ramp.AddPoint(0.25f, FogColor); ramp.AddPoint(0.7f, FogColor);
            pm.ColorRamp = new GradientTexture1D { Gradient = ramp };
            _fog.ProcessMaterial = pm;
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                VertexColorUseAsAlbedo = true, AlbedoTexture = FogTexture, CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false
            };
            _fog.DrawPass1 = new QuadMesh { Size = new Vector2(1.4f, 1.4f), Material = mat };
            AddChild(_fog);
        }
        _fog.GlobalPosition = at;
        _fog.Restart(); _fog.Emitting = true;
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: a foggy spot at {at}");
    }
    private void StopFog() { _fogging = false; if (_fog != null) _fog.Emitting = false; }

    private void SpawnBoo(Vector3 at)
    {
        if (BombBooScene == null) { GD.PushWarning($"[SMS bomb supply] {GetPath()}: no BombBooScene (assets/Sms/Rematch/BombBoo.tscn)"); return; }
        var boo = BombBooScene.Instantiate<SmsBombBoo>();
        boo.Name = $"BombBoo{++SpawnCount}";
        if (DebugPrint) boo.DebugPrint = true;
        GetParent().AddChild(boo);
        boo.GlobalPosition = at;
        if (DebugPrint) GD.Print($"[SMS bomb supply] {Name}: {boo.Name} out of the fog at {at} ({OutNow} out)");
        EmitSignal(SignalName.Spawned, boo);
    }
}
