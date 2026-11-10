using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 200 (Jaysonn: "one more attack involving boos somehow" -- the Bomb Boo volley): King Boo rears back and SPITS Count lit Bomb Boos out of his
/// mouth, Spacing seconds apart, each on a high lob (LobHeight over the mouth) to come down about Mario -- the first within AimedWithin of him, the rest
/// anywhere out to Scatter, all inside the arena's wall (WallInset) -- a ring on the floor marking where each lands (the blast's footprint). A Boo that lands
/// BLASTS: the Bob-omb explosion, a ring of light out to BlastRadius, Mario within it whose feet are under BlastHeight hit (a jump clears it, as the
/// Boolossus's wave). The LAST one is a DUD (Dud): it thuds down at least DudMinFromMario from Mario and FLOATS there as one of the supply's Bomb Boos --
/// his to grab -- its fuse still burning: DudSeconds later it blasts where it floats (0 = never). The Boos are the supply's own scene (SmsBombBoo.Lob);
/// King Boo's hit clears them with the rest of the field. Signals Spat(count) as the last leaves the mouth, Landed(dud) per landing, Blasted(hitMario) per
/// blast, Finished when every Boo has come down (the dud floats on; its blast is its own).
/// </summary>
public partial class SmsBooVolley : Node3D
{
    public enum Phase { Idle, Spitting, Flying, Done }
    [ExportGroup("The spit")]
    /// <summary>How many Bomb Boos he spits, the time between them, how high over the mouth they lob (metres), how far about Mario they come down (metres: the
    /// first within AimedWithin of him, the rest anywhere out to Scatter), how far in from the floor's edge the wall is.</summary>
    [Export(PropertyHint.Range, "1,8,1")] public int Count = 3;
    [Export] public float Spacing = 0.4f, LobHeight = 9f, Scatter = 5f, AimedWithin = 1f, WallInset = 2f;
    [ExportGroup("The blast")]
    /// <summary>The blast's reach on the floor (metres) and how high over the floor it reaches (Mario's feet higher are over it: the jump).</summary>
    [Export] public float BlastRadius = 3.5f, BlastHeight = 1.6f;
    [ExportGroup("The dud")]
    /// <summary>Whether the last Boo lands as a DUD (a live Bomb Boo for Mario to grab), how long its fuse burns once down (0 = never blasts on its own), how
    /// far from Mario at least it comes down (it must not land on him).</summary>
    [Export] public bool Dud = true;
    [Export] public float DudSeconds = 6f, DudMinFromMario = 2.5f;
    [ExportGroup("The sounds")]
    [Export] public string VolleySe = "MSD_SE_BS_TELESA_FIRE", SpitSe = "MSD_SE_EN_TELESA_APPEAR";
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void SpatEventHandler(int count);
    [Signal] public delegate void LandedEventHandler(bool dud);
    [Signal] public delegate void BlastedEventHandler(bool hitMario);
    [Signal] public delegate void FinishedEventHandler();

    public Phase State { get; private set; } = Phase.Idle;
    public int SpatCount { get; private set; }
    public int Landings { get; private set; }
    public int Blasts { get; private set; }
    public int Hits { get; private set; }
    public int InAir { get { int n = 0; foreach (var b in _boos) if (b != null && GodotObject.IsInstanceValid(b) && b.IsLobbed) n++; return n; } }
    public IReadOnlyList<SmsBombBoo> Boos => _boos;
    public SmsBombBoo DudBoo { get; private set; }
    public float FloorY { get; private set; }
    public Vector3 SpoutPoint => _spout != null ? _spout() : GlobalPosition;
    public Vector3 ArenaCentre => _centre;
    public float ArenaRadius => _radius;
    public static readonly List<SmsBooVolley> All = new();
    public static int Volleys, BlastCount, HitCount;

    private Mario _mario; private Func<Vector3> _spout; private SmsActorSound _sound; private readonly Random _rng = new(200);
    private PackedScene _scene; private Node _booParent; private bool _debugBoos;
    private readonly List<SmsBombBoo> _boos = new();
    private Vector3 _centre; private float _radius = 26f, _t, _nextAt;

    public override void _Ready() { All.Add(this); }
    public override void _ExitTree() { All.Remove(this); }

    /// <summary>The spit begins: Boos of <paramref name="booScene"/> (the supply's) under <paramref name="booParent"/>, out of <paramref name="spout"/> (the mouth, read per
    /// spit), down about Mario on the floor at <paramref name="floorY"/>, inside the arena given.</summary>
    public void Begin(Mario mario, float floorY, Func<Vector3> spout, Vector3 arenaCentre, float arenaRadius, PackedScene booScene, Node booParent, bool debugBoos = false)
    {
        if (State != Phase.Idle) return;
        _mario = mario; FloorY = floorY; _spout = spout; _centre = arenaCentre; _radius = Mathf.Max(5f, arenaRadius); _scene = booScene; _booParent = booParent ?? GetParent(); _debugBoos = debugBoos;
        _sound = SmsActorSound.Attach(this);
        GlobalPosition = spout();
        State = Phase.Spitting; _t = 0f; _nextAt = 0f; Volleys++;
        if (_sound != null && !string.IsNullOrEmpty(VolleySe)) _sound.Play(VolleySe);
        if (DebugPrint) GD.Print($"[SMS boo volley] {Name}: the SPIT -- {Count} Bomb Boos {Spacing:F2} s apart out of the mouth at {GlobalPosition}, lobbed {LobHeight} m up to come down within {Scatter} m of Mario (the first within {AimedWithin} m), the blast {BlastRadius} m{(Dud ? $", the last a DUD ({DudSeconds} s fuse, {DudMinFromMario} m+ from him)" : "")}; the wall at {_radius - WallInset:F1} m about {_centre}");
    }

    /// <summary>One Boo out of the mouth, lobbed to its own spot about Mario.</summary>
    private void Spit()
    {
        if (_scene == null) { GD.PushWarning($"[SMS boo volley] {Name}: no Bomb Boo scene -- nothing to spit"); Finish(); return; }
        Vector3 from = SpoutPoint;
        Vector3 m = _mario != null && GodotObject.IsInstanceValid(_mario) ? _mario.GlobalPosition : _centre;
        int i = SpatCount; bool dud = Dud && i == Count - 1 && Count > 0;
        Vector3 target = Vector3.Zero;
        for (int draw = 0; draw < 12; draw++)
        {
            float ang = (float)(_rng.NextDouble() * Math.Tau);
            float dist = i == 0 && !dud ? AimedWithin * (float)Math.Sqrt(_rng.NextDouble()) : Mathf.Lerp(dud ? Mathf.Max(AimedWithin, DudMinFromMario) : AimedWithin, Scatter, (float)Math.Sqrt(_rng.NextDouble()));
            target = new Vector3(m.X + MathF.Sin(ang) * dist, FloorY, m.Z + MathF.Cos(ang) * dist);
            Vector3 cd = new Vector3(target.X - _centre.X, 0f, target.Z - _centre.Z); float cl = cd.Length(), wallR = _radius - WallInset;
            if (cl > wallR && cl > 1e-3f) target = new Vector3(_centre.X + cd.X / cl * wallR, FloorY, _centre.Z + cd.Z / cl * wallR);   // inside the wall
            if (!dud || new Vector2(target.X - m.X, target.Z - m.Z).Length() >= DudMinFromMario) break;                                  // a dud never lands on him
        }
        var boo = _scene.Instantiate<SmsBombBoo>();
        boo.Name = $"VolleyBoo{Volleys}_{i + 1}";
        if (_debugBoos) boo.DebugPrint = true;
        (_booParent ?? GetParent()).AddChild(boo);
        boo.GlobalPosition = from;
        boo.Lob(from, target, LobHeight, dud, BlastRadius, BlastHeight, DudSeconds);
        boo.Landed += d => { Landings++; EmitSignal(SignalName.Landed, d); };
        boo.Blasted += hit => { Blasts++; BlastCount++; if (hit) { Hits++; HitCount++; } EmitSignal(SignalName.Blasted, hit); };
        _boos.Add(boo); SpatCount++; if (dud) DudBoo = boo;
        if (_sound != null && !string.IsNullOrEmpty(SpitSe)) _sound.Play(SpitSe, -2f, 0.9f + 0.2f * (float)_rng.NextDouble());
        if (DebugPrint) GD.Print($"[SMS boo volley] {Name}: Boo {SpatCount} of {Count} out of the mouth at {from} toward {target} ({new Vector2(target.X - m.X, target.Z - m.Z).Length():F1} m from Mario{(i == 0 && !dud ? ", the aimed one" : dud ? ", the DUD" : "")}; {boo.LobFlightSeconds:F2} s in the air)");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (State == Phase.Idle || State == Phase.Done) return;
        _t += dt;
        if (State == Phase.Spitting)
        {
            while (State == Phase.Spitting && SpatCount < Count && _t >= _nextAt) { Spit(); _nextAt += Mathf.Max(0.05f, Spacing); }
            if (State == Phase.Spitting && SpatCount >= Count) { State = Phase.Flying; EmitSignal(SignalName.Spat, SpatCount); }
        }
        if (State == Phase.Flying && InAir == 0) Finish();
    }
    private void Finish()
    {
        if (State == Phase.Done) return;
        State = Phase.Done; All.Remove(this);
        if (DebugPrint) GD.Print($"[SMS boo volley] {Name}: every Boo is down -- {SpatCount} spat, {Landings} landings, {Blasts} blast{(Blasts == 1 ? "" : "s")}, {Hits} hit{(Hits == 1 ? "" : "s")}{(DudBoo != null && GodotObject.IsInstanceValid(DudBoo) && DudBoo.IsFree ? $", the dud floating at {DudBoo.GlobalPosition} ({DudBoo.DudFuseLeft:F1} s on its fuse)" : "")}");
        EmitSignal(SignalName.Finished);
        QueueFree();
    }
    /// <summary>King Boo hit through the spit: no more Boos (the field is cleared with his hit -- the supply vanishes those in the air).</summary>
    public void Cancel()
    {
        if (State == Phase.Done) return;
        State = Phase.Done;                                                                                     // 203: it stays in All till it is freed (the field clear)
        if (DebugPrint) GD.Print($"[SMS boo volley] {Name}: cut off after {SpatCount} of {Count}");
        EmitSignal(SignalName.Finished);
        GetTree().CreateTimer(0.3f).Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
    }
    /// <summary>203: gone at once with the field (the split's cut): no more spits, the node freed -- the Boos it spat go on their own (SmsBombBoo.VanishIfSpat); true = it went.</summary>
    public bool VanishNow()
    {
        if (IsQueuedForDeletion()) return false;
        State = Phase.Done;
        QueueFree();
        return true;
    }
}
