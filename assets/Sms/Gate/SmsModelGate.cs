using Godot;

/// <summary>
/// The M-graffiti warp gate -- TModelGate (MoveBG/ModelGate.cpp; SMS pass 127). The game's object is a 400 x 400-unit quad
/// (05_gate01.bmd: the M mask over a scrolling rainbow sheen with the stage's movie inside -- MGate.gdshader) whose 'center' joint sits
/// 200 up; a hit actor of radius 300 / height 400 on the player category that takes FLUDD's water. Its life (perform / receiveMessage):
/// <list type="bullet">
/// <item>Shut (the M not painted yet, MSF_M_PAINTED_ON_*): nothing drawn, no collision. <see cref="Open"/> (startOpen) fades it in
/// over 100 frames (the bpk) and, as the game, asks for the hose once: ten drops (0.1 each) light it the first time.</item>
/// <item>Awake: Mario within 1000 units charges the glow 0.01 a frame; at 1 the gate is ACTIVE for 360 frames (12 s), renewed every
/// frame he stays near; the swirl (the four gatewind emitters) and its two sounds run while active. Away, the glow drops to the
/// timer's fraction and the timer runs out.</item>
/// <item>The take: Mario inside the box in front of the M (x within 150, y 1000 below .. 150 above the centre, 0 .. 300 in front) while
/// the gate is active and he is JUMPING (an air status) -> HIT_MESSAGE_TAKE -> Mario's WARP_IN (Mario.cs: SmsWarpIn*). Not active:
/// within 200 units of the centre he is nudged 10 units a frame back out.</item>
/// <item>Water: a drop within 200 units of the centre and at the plane charges it (the first time only) and throws the gatehit sparks
/// (70 %); the drop dies (TRUE).</item>
/// <item>Captured (Mario's warp-in done, HIT_MESSAGE_ATTACK back): the glow fades 0.025 a frame; the stage moves.</item>
/// </list>
/// The game performs CUE_MOVE on every object four times a 30 fps frame (the 120 Hz movement ticks, pass 125) and CUE_CALC_ANIM once:
/// so the nearness charge (0.01 a TICK: lit in 50 frames against the 0.02-a-frame fade), the far countdown (4 a frame on top of the
/// frame's own 1) and the push-out (10 units a TICK = 40 a frame, more than Mario's run) are tick-rate here, the glow fade and the states
/// frame-rate. The node's -Z is the front (the side the player stands on; the game's quad
/// faces +Z in its own frame -- the sign is turned here so the node points like every Godot node). Where it goes is <see cref="Stage"/>
/// (an SmsStageInfo: the level, its episodes, the select screen's art); the return from that level lands at this gate: a return spot
/// (SmsReturnSpot, pass 119) for the stage is added in front unless the level has one of its own. DEVIATIONS: the movie inside is a
/// still (<see cref="Preview"/>); the directional screen blur toward the gate (screenBlur, the after-effect) is not ported; the demo
/// camera on the gate during the warp-in is the locked camera.
/// </summary>
[Tool, GlobalClass]
public partial class SmsModelGate : StaticBody3D, ISmsSprayTarget
{
    /// <summary>unkC4: 0 the open fade (the bpk), 1 normal, 2 captured (Mario taken in, the glow fading out).</summary>
    public enum GateState { Opening = 0, Normal = 1, Captured = 2 }

    [ExportGroup("Where it goes")]
    /// <summary>The stage behind this gate: its level and episodes (the select screen reads it). None: the gate takes Mario and nothing loads.</summary>
    [Export] public SmsStageInfo Stage;
    /// <summary>The M is painted (the game's MSF_M_PAINTED_ON_STATUE / BOATHOUSE / LIGHTHOUSE flag): the gate exists. Off: nothing is here
    /// until <see cref="Open"/> is called (the event that paints the M).</summary>
    [Export] public bool Opened = true;
    /// <summary>The game's rule right after the M is painted (startOpen): the first activation needs the hose (ten drops); after that being
    /// near is enough. On here = a gate that starts Opened also wants the hose first.</summary>
    [Export] public bool SprayFirst = false;

    [ExportGroup("The M")]
    /// <summary>The quad: the game's 05_gate01 is 400 x 400 units, 05_gate03manma 400 x 435.</summary>
    [Export] public float WidthUnits = 400f;
    [Export] public float HeightUnits = 400f;
    /// <summary>The picture inside the M when it is lit (the game plays the stage's movie there -- DEVIATION, a still). None: the bright sheen.</summary>
    [Export] public Texture2D Preview;
    /// <summary>The M drawn this much of the quad (texmtx5: gate01 0.91, gate02rico 0.99, gate03manma 0.95).</summary>
    [Export] public float MaskScale = 0.91f;
    /// <summary>x the game's wobble of the sheen and the M's edge (the indirect stage); 0 = none.</summary>
    [Export] public float WarpStrength = 1f;
    /// <summary>DEVIATION: a plain multiplier on the gate's colour (the game: 1).</summary>
    [Export] public float Brightness = 1f;
    /// <summary>The glow the editor shows the gate at (0 = asleep, 1 = lit); in play the script drives it.</summary>
    [Export(PropertyHint.Range, "0,1")] public float EditorGlow = 1f;
    /// <summary>The three textures off the disc (assets/Sms/Gate/); replace to restyle the M.</summary>
    [Export] public Texture2D MaskTexture;
    [Export] public Texture2D SheenTexture;
    [Export] public Texture2D WarpTexture;

    [ExportGroup("Activation (the game's numbers)")]
    /// <summary>unkD4: the glow one water hit adds while the gate still wants the hose (ten hits light it).</summary>
    [Export] public float SprayChargePerDrop = 0.1f;
    /// <summary>A drop counts within this of the M's centre, in its plane (dist^2 &lt; 40000).</summary>
    [Export] public float SprayRadiusUnits = 200f;
    /// <summary>Mario within this of the gate charges the glow (SMS_DistanceFromMarioVec &lt; 1000).</summary>
    [Export] public float ProximityUnits = 1000f;
    /// <summary>The glow per 120 Hz TICK while he is near (0.01: four a frame against the 0.02 fade -- lit in 50 frames, 1.7 s).</summary>
    [Export] public float ProximityChargePerTick = 0.01f;
    /// <summary>unkC8 = 0x168: the active run after a charge (12 s of frames while he stays; away, the ticks count it down too, 5 a frame: 2.4 s).</summary>
    [Export] public int ActiveFrames = 360;
    /// <summary>unkD8: the glow's fall per frame once the run is out; unkDC: after the capture.</summary>
    [Export] public float FadePerFrame = 0.02f;
    [Export] public float CapturedFadePerFrame = 0.025f;
    /// <summary>The open fade's length in frames (the bpk: 100, alpha 12 -> 128 at 70 -> 255).</summary>
    [Export] public int OpenFrames = 100;

    [ExportGroup("The jump-in box (units, about the M's centre; -Z is the front)")]
    [Export] public float TakeHalfWidthUnits = 150f;
    [Export] public float TakeBelowUnits = 1000f;
    [Export] public float TakeAboveUnits = 150f;
    [Export] public float TakeDepthUnits = 300f;
    /// <summary>unk100 / the 10: not active, Mario this close to the centre (flat) is pushed out 10 units a TICK (40 a frame: more than his run).</summary>
    [Export] public float PushOutRadiusUnits = 200f;
    [Export] public float PushOutPerTickUnits = 10f;
    /// <summary>unkAC: where the warp-in holds Mario -- this far in front of the centre (and 80 below it: warpIn's -80, so he hangs centred on the M).</summary>
    [Export] public float HoldFrontUnits = 250f;
    [Export] public float HoldDownUnits = 80f;

    [ExportGroup("The return (SmsReturnSpot)")]
    /// <summary>Add a return spot for the stage in front of the gate when the level has none of its own, so coming back lands here.</summary>
    [Export] public bool MakeReturnSpot = true;
    /// <summary>Where the spot sits, in units in the gate's frame (z &lt; 0 = in front). He faces away from the gate with a shine, toward it without (returnStart).</summary>
    [Export] public Vector3 ReturnSpotOffsetUnits = new Vector3(0f, 0f, -250f);

    [ExportGroup("Effects and sounds")]
    /// <summary>The swirl while active: a scene of GpuParticles3D (ms_gatewind_a / a2 / a3 / b) placed at the centre, emitting while active (pass 127b).</summary>
    [Export] public PackedScene WindScene;
    /// <summary>127d DEVIATION knobs: the swirl's strength and size over the game's values (1 / 1 = the disc's data). Jaysonn wanted it dialed
    /// down: the wisps' and rays' alpha x WindAlpha, the wisps' size x WindSize.</summary>
    [Export(PropertyHint.Range, "0,1.5")] public float WindAlpha = 0.6f;
    [Export(PropertyHint.Range, "0.25,1.5")] public float WindSize = 0.75f;
    /// <summary>The rainbow rays out of the M (ms_gatewind_b): their alpha x this (1 = the game's 0.875; they are 3-6 m long additive streaks and read very bright over a sunny sky).</summary>
    [Export(PropertyHint.Range, "0,1.5")] public float RayAlpha = 0.4f;
    /// <summary>The sparks a drop throws (ms_gatehit_a / b), one-shot at the drop, 70 % of hits (pass 127b).</summary>
    [Export] public PackedScene HitScene;
    [Export(PropertyHint.Range, "0,1")] public float HitSparkChance = 0.7f;
    /// <summary>MSD_SE_OBJ_GATE_LIGHT / MSD_SE_OBJ_GATE_WAVE: started every active frame by the game; here looped while active.</summary>
    [Export] public AudioStreamPlayer3D LightSound;
    [Export] public AudioStreamPlayer3D WaveSound;
    [Export] public bool DebugPrint;

    /// <summary>Mario was taken (HIT_MESSAGE_TAKE accepted): the warp-in begins.</summary>
    [Signal] public delegate void MarioTakenEventHandler();
    /// <summary>The gate lit (the active timer started from zero).</summary>
    [Signal] public delegate void ActivatedEventHandler();
    /// <summary>Mario's warp-in finished (HIT_MESSAGE_ATTACK): the stage moves from here.</summary>
    [Signal] public delegate void CapturedEventHandler();

    public const float FrameRate = 30f;
    public const int TicksPerFrame = 4;   // CUE_MOVE runs on the 120 Hz movement ticks, CUE_CALC_ANIM once a frame
    public const float HitRadiusUnits = 300f, HitHeightUnits = 400f;

    private MeshInstance3D _quad;
    private ShaderMaterial _mat;
    private CollisionShape3D _shape;
    private Node3D _wind;
    private SmsReturnSpot _spot;
    private double _acc;
    private int _tickInFrame;
    private bool _wantsSpray;      // unk70 bit 2
    private int _openFrame;
    private float _glow;           // unkD0
    private int _activeLeft;       // unkCA
    private int _activeRun;        // unkCC
    private GateState _state = GateState.Normal;
    private Node3D _held;          // mHeldObject
    private bool _wasActive, _mesh, _rearmWhenOut;

    /// <summary>unkD0: 0 asleep .. 1 lit (the brk frame / 99).</summary>
    public float Glow => _glow;
    /// <summary>unkCA: frames of activity left; &gt; 0 = the gate takes a jumping Mario.</summary>
    public int ActiveLeft => _activeLeft;
    public bool Active => _activeLeft > 0;
    public GateState State => _state;
    public bool WantsSpray => _wantsSpray;
    public Node3D Held => _held;
    public SmsReturnSpot ReturnSpot => _spot;
    public float OpenAlpha => _state == GateState.Opening ? OpenCurve(_openFrame) : (Opened ? 1f : 0f);

    /// <summary>The M's centre (the 'center' joint: 200 up on the game's quad), world.</summary>
    public Vector3 CentreWorld => ToGlobal(new Vector3(0f, HeightUnits / SmsUnits.UnitsPerMetre * 0.5f, 0f));
    /// <summary>The gate's front, world (the node's -Z).</summary>
    public Vector3 FrontWorld => -GlobalTransform.Basis.Z.Normalized();
    /// <summary>unkAC - 80 y: where the warp-in pulls Mario (his feet).</summary>
    public Vector3 HoldPointWorld => CentreWorld + FrontWorld * (HoldFrontUnits / SmsUnits.UnitsPerMetre) - Vector3.Up * (HoldDownUnits / SmsUnits.UnitsPerMetre);

    public override void _Ready()
    {
        BuildQuad();
        if (Engine.IsEditorHint()) { SetParam("glow", EditorGlow); SetParam("open_alpha", 1f); return; }
        if (!Opened) { _state = GateState.Normal; SetParam("open_alpha", 0f); }
        _wantsSpray = Opened && SprayFirst;
        CollisionLayer = Opened ? SmsHiddenCoin.HideLayer : 0; CollisionMask = 0;
        bool shaped = false;
        foreach (Node c in GetChildren()) if (c is CollisionShape3D) shaped = true;
        if (!shaped)
        {   // the game's hit cylinder stands at the actor; the water must reach the PLANE, so a slab the M's size, 100 units deep about it
            float u = SmsUnits.UnitsPerMetre;
            _shape = new CollisionShape3D { Name = "Hit", Shape = new BoxShape3D { Size = new Vector3(WidthUnits / u, HeightUnits / u, 100f / u) }, Position = new Vector3(0f, HeightUnits / u * 0.5f, 0f) };
            AddChild(_shape);
        }
        if (WindScene != null)
        {
            _wind = WindScene.Instantiate<Node3D>();
            AddChild(_wind);
            _wind.Position = new Vector3(0f, HeightUnits / SmsUnits.UnitsPerMetre * 0.5f, 0f);
            ApplyWindKnobs();
            SetWind(false);
        }
        if (MakeReturnSpot && Stage != null && Stage.ScenePath != "")
        {
            var own = SmsReturnSpot.Find(GetTree(), Stage.ScenePath, allowAny: false);
            if (own == null)
            {
                _spot = new SmsReturnSpot { Name = "GateReturnSpot", FromScene = Stage.Scene, Position = ReturnSpotOffsetUnits / SmsUnits.UnitsPerMetre };
                AddChild(_spot);
            }
            else _spot = own;
        }
        SetParam("glow", 0f);
        if (DebugPrint) GD.Print($"[SMS gate] {Name}: {(Opened ? "open" : "SHUT")}{(_wantsSpray ? ", wants the hose first" : "")}, stage {(Stage != null ? Stage.StageName + " -> " + Stage.ScenePath : "none")}, return spot {(_spot != null ? _spot.Name + " at " + _spot.GlobalPosition : "none")}");
    }

    /// <summary>The 400 x 400 quad at the node (its bottom edge on the floor, the M's centre 200 up), facing -Z, UV (0,0) top-left.</summary>
    private void BuildQuad()
    {
        if (_mesh) return;
        _mesh = true;
        float u = SmsUnits.UnitsPerMetre;
        float w = WidthUnits / u, h = HeightUnits / u;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 n = Vector3.Back * -1f;   // -Z: the front
        Vector3 bl = new Vector3(-w * 0.5f, 0f, 0f), br = new Vector3(w * 0.5f, 0f, 0f), tl = new Vector3(-w * 0.5f, h, 0f), tr = new Vector3(w * 0.5f, h, 0f);
        // seen from the front (-Z), +X is on the viewer's LEFT: the texture's left column goes on +X so the M reads the right way round
        void V(Vector3 p, Vector2 uv) { st.SetNormal(n); st.SetUV(uv); st.AddVertex(p); }
        V(tr, new Vector2(0f, 0f)); V(br, new Vector2(0f, 1f)); V(bl, new Vector2(1f, 1f));
        V(tr, new Vector2(0f, 0f)); V(bl, new Vector2(1f, 1f)); V(tl, new Vector2(1f, 0f));
        var mesh = st.Commit();
        _quad = GetNodeOrNull<MeshInstance3D>("Quad");
        if (_quad == null) { _quad = new MeshInstance3D { Name = "Quad" }; AddChild(_quad); }
        _quad.Mesh = mesh;
        _quad.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/Sms/Gate/MGate.gdshader") };
        _mat.SetShaderParameter("mask_tex", MaskTexture ?? GD.Load<Texture2D>("res://assets/Sms/Gate/P_gate_msk_m2.png"));
        _mat.SetShaderParameter("sheen_tex", SheenTexture ?? GD.Load<Texture2D>("res://assets/Sms/Gate/P_gate_spec2s3tc.png"));
        _mat.SetShaderParameter("warp_tex", WarpTexture ?? GD.Load<Texture2D>("res://assets/Sms/Gate/P_ms_indwp1_ia.png"));
        if (Preview != null) _mat.SetShaderParameter("preview_tex", Preview);
        _mat.SetShaderParameter("has_preview", Preview != null);
        _mat.SetShaderParameter("mask_scale", MaskScale);
        _mat.SetShaderParameter("warp_strength", WarpStrength);
        _mat.SetShaderParameter("brightness", Brightness);
        _quad.MaterialOverride = _mat;
    }

    private void SetParam(string name, Variant v) => _mat?.SetShaderParameter(name, v);

    /// <summary>startOpen: the M painted -- the gate fades in (the bpk), takes collision, and wants the hose for its first light.</summary>
    public void Open()
    {
        if (Opened && _state != GateState.Opening) return;
        Opened = true;
        _state = GateState.Opening; _openFrame = 0;
        _wantsSpray = true;
        CollisionLayer = SmsHiddenCoin.HideLayer;
        SetParam("open_alpha", OpenCurve(0));
        if (DebugPrint) GD.Print($"[SMS gate] {Name}: OPEN (the M painted) -- fading in, the hose lights it");
    }

    /// <summary>The bpk's alpha: 12 at 0, 128 at 70, 255 at 100 (two straight runs).</summary>
    private float OpenCurve(int f)
    {
        float a = f <= 70 ? Mathf.Lerp(12f, 128f, f / 70f) : Mathf.Lerp(128f, 255f, (f - 70) / 30f);
        return Mathf.Clamp(a / 255f, 0f, 1f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) { SetParam("glow", EditorGlow); return; }
        if (!Opened) return;
        _acc += delta * FrameRate * TicksPerFrame;
        int guard = 0;
        while (_acc >= 1.0 && guard++ < 32)
        {
            _acc -= 1.0;
            MoveTick();
            if (++_tickInFrame >= TicksPerFrame) { _tickInFrame = 0; AnimFrame(); }
        }
        if (_acc > 32.0) _acc = 0.0;
    }

    /// <summary>TModelGate::perform's CUE_MOVE: one 120 Hz movement tick -- the charge by nearness (once the hose has lit it, or never asked),
    /// the far countdown, the take box, the push-out.</summary>
    private void MoveTick()
    {
        var mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        if (!_wantsSpray)
        {
            if (mario != null && mario.GlobalPosition.DistanceTo(GlobalPosition) < ProximityUnits / SmsUnits.UnitsPerMetre)
            {
                _glow += ProximityChargePerTick;
                if (_glow > 1f) { _glow = 1f; Renew(); }
            }
            else
            {
                if (--_activeLeft <= 0) _activeLeft = 0;
                _glow = _activeLeft / 1000f;
            }
        }
        if (mario != null && _state != GateState.Captured)
        {
            float u = SmsUnits.UnitsPerMetre;
            Vector3 local = GlobalTransform.AffineInverse() * mario.GlobalPosition;   // the gate's frame
            local.Y -= HeightUnits / u * 0.5f;                                         // about the M's centre
            float front = -local.Z;                                                    // the game's +z: toward the player
            if (Mathf.Abs(local.X) < TakeHalfWidthUnits / u && local.Y > -TakeBelowUnits / u && local.Y < TakeAboveUnits / u && front > 0f && front < TakeDepthUnits / u)
            {
                if (_rearmWhenOut) { }   // released inside the box: nothing until he has left it
                else if (_activeLeft > 0)
                {
                    if (_held == null && mario.SmsWarpInJumping() && mario.SmsWarpInTake(this))
                    {
                        _held = mario;
                        EmitSignal(SignalName.MarioTaken);
                        if (DebugPrint) GD.Print($"[SMS gate] {Name}: Mario TAKEN at physics frame {Engine.GetPhysicsFrames()} -- the warp-in");
                    }
                }
                else
                {
                    Vector3 c = GlobalPosition;
                    float dx = mario.GlobalPosition.X - c.X, dz = mario.GlobalPosition.Z - c.Z;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > 1e-4f && dist < PushOutRadiusUnits / u)
                        mario.GlobalPosition += new Vector3(dx / dist, 0f, dz / dist) * (PushOutPerTickUnits / u);   // SMS_MarioMoveRequest
                }
            }
            else _rearmWhenOut = false;   // out of the box: a new jump in may take him again
        }
    }

    /// <summary>TModelGate::perform's CUE_CALC_ANIM: one 30 fps frame -- the open fade, the active run (the swirl, the sounds), the glow's fall.</summary>
    private void AnimFrame()
    {
        switch (_state)
        {
            case GateState.Opening:
                _openFrame++;
                SetParam("open_alpha", OpenCurve(_openFrame));
                if (_openFrame >= OpenFrames) _state = GateState.Normal;
                break;
            case GateState.Normal:
                if (_activeLeft > 0)
                {
                    _activeLeft--; _activeRun++;
                    if (!_wasActive) { _wasActive = true; SetWind(true); if (DebugPrint) GD.Print($"[SMS gate] {Name}: lit -- the swirl and the sounds for {_activeLeft + 1} frames"); }
                }
                else
                {
                    _activeLeft = 0; _activeRun = 0;
                    _glow -= FadePerFrame;
                    if (_glow < 0f) _glow = 0f;
                    if (_wasActive) { _wasActive = false; SetWind(false); if (DebugPrint) GD.Print($"[SMS gate] {Name}: out -- the swirl stops, the glow fades"); }
                }
                break;
            default:
                _glow -= CapturedFadePerFrame;
                if (_wasActive) { _wasActive = false; SetWind(false); }
                break;
        }
        _glow = Mathf.Clamp(_glow, 0f, 1f);
        SetParam("glow", _glow);   // the brk frame = unkD0 x 99
    }

    /// <summary>unkCA = unkC8: the active run (re)starts.</summary>
    private void Renew()
    {
        bool was = _activeLeft > 0;
        _activeLeft = ActiveFrames;
        if (!was) EmitSignal(SignalName.Activated);
    }

    /// <summary>127d: WindAlpha / WindSize onto this gate's own copies of the wind's materials: a draw pass's ShaderMaterial (the wisps, the trails:
    /// prm.a) or StandardMaterial3D (the rays: the albedo alpha), and the heads' / trails' quad / ribbon size (the rays keep their size).</summary>
    private void ApplyWindKnobs()
    {
        if (_wind == null || (Mathf.IsEqualApprox(WindAlpha, 1f) && Mathf.IsEqualApprox(WindSize, 1f) && Mathf.IsEqualApprox(RayAlpha, 1f))) return;
        var seen = new System.Collections.Generic.Dictionary<Material, Material>();
        foreach (Node c in _wind.GetChildren())
        {
            if (c is not GpuParticles3D p) continue;
            bool head = c.Name.ToString().StartsWith("Wind_a") || c.Name.ToString().StartsWith("Trail_");
            for (int i = 0; i < p.DrawPasses; i++)
            {
                var mesh = p.GetDrawPassMesh(i);
                if (mesh == null) continue;
                var m = mesh.SurfaceGetMaterial(0);
                if (m == null) continue;
                if (!seen.TryGetValue(m, out var copy))
                {
                    copy = (Material)m.Duplicate();
                    if (copy is ShaderMaterial sm)
                    {
                        var prm = sm.GetShaderParameter("prm").AsVector4(); prm.W *= WindAlpha; sm.SetShaderParameter("prm", prm);
                    }
                    else if (copy is StandardMaterial3D std) { Color a = std.AlbedoColor; a.A *= head ? WindAlpha : RayAlpha; std.AlbedoColor = a; }
                    seen[m] = copy;
                }
                var meshCopy = (Mesh)mesh.Duplicate();
                meshCopy.SurfaceSetMaterial(0, copy);
                if (head && meshCopy is QuadMesh q) q.Size *= WindSize;
                if (head && meshCopy is RibbonTrailMesh r) { r.Size *= WindSize; r.SectionLength *= WindSize; }
                p.SetDrawPassMesh(i, meshCopy);
            }
        }
    }

    private void SetWind(bool on)
    {
        if (_wind != null)
            foreach (Node c in _wind.GetChildren()) if (c is GpuParticles3D p) p.Emitting = on;
        if (LightSound != null) { if (on && !LightSound.Playing) LightSound.Play(); else if (!on) LightSound.Stop(); }
        if (WaveSound != null) { if (on && !WaveSound.Playing) WaveSound.Play(); else if (!on) WaveSound.Stop(); }
    }

    /// <summary>receiveMessage from a water actor: within 200 units of the centre at the plane -> the first-light charge (while wanted) and
    /// the sparks; TRUE = the drop dies here. Elsewhere on the slab: FALSE, it flies on.</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (!Opened || _state == GateState.Captured) return false;
        Vector3 local = GlobalTransform.AffineInverse() * hitPos;
        local.Y -= HeightUnits / SmsUnits.UnitsPerMetre * 0.5f;
        float r = SprayRadiusUnits / SmsUnits.UnitsPerMetre;
        if (local.X * local.X + local.Y * local.Y >= r * r) return false;
        if (_wantsSpray)
        {
            _glow += SprayChargePerDrop;
            if (_glow > 1f)
            {
                _glow = 1f; _wantsSpray = false; Renew();
                if (DebugPrint) GD.Print($"[SMS gate] {Name}: the hose lit it -- from here being near is enough");
            }
        }
        if (HitScene != null && GD.Randf() < HitSparkChance)
        {
            var fx = HitScene.Instantiate<Node3D>();
            (GetTree().CurrentScene ?? GetParent()).AddChild(fx);
            fx.GlobalPosition = hitPos;
            fx.LookAt(hitPos + FrontWorld, Vector3.Up);   // emitWithRotate(.., unk74 = the gate's yaw): the sparks fly out of the gate's face (the scene's -Z)
            foreach (Node c in fx.GetChildren()) if (c is GpuParticles3D p) { p.OneShot = true; p.Restart(); p.Emitting = true; }
            GetTree().CreateTimer(1.0).Timeout += () => { if (IsInstanceValid(fx)) fx.QueueFree(); };
        }
        return true;
    }

    /// <summary>Mario's warp-in is complete (HIT_MESSAGE_ATTACK from him): the glow fades for good; the stage moves.</summary>
    public void SmsCaptured()
    {
        if (_state == GateState.Captured) return;
        _state = GateState.Captured;
        _activeLeft = 0;
        if (DebugPrint) GD.Print($"[SMS gate] {Name}: CAPTURED -- the stage moves to {(Stage != null ? Stage.ScenePath : "nowhere")}");
        EmitSignal(SignalName.Captured);
    }

    /// <summary>The take fell through (he left some other way, or the gate has no stage): the gate forgets him and will not take him again
    /// until he has left the box -- else the fall from the hold point is a new jump inside it and the take loops.</summary>
    public void SmsRelease()
    {
        _held = null;
        _rearmWhenOut = true;
        if (_state == GateState.Captured) _state = GateState.Normal;
    }

    /// <summary>For the drive and the inspector: the stage's name.</summary>
    public override string ToString() => $"SmsModelGate {Name} glow {_glow:F2} active {_activeLeft} state {_state}";
}
