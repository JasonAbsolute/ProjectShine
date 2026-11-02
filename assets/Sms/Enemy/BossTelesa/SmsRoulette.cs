using System.Collections.Generic;
using Godot;

/// <summary>
/// A ROULETTE DISC of King Boo's table -- TRoulette (MoveBG/MapObjSirena.cpp; ACTOR_TYPE_CASINORULET, the rulet00 / 01 / 02 models) for SMS pass 153.
/// A tier of the casino's great roulette table: it spins about its centre at the speed King Boo gives it (setRollSp: 0.05..0.1 deg a tick + 0.03 a hit
/// point he has lost; the middle ring the other way), carrying Mario, and a SWITCH on its face (TRouletteSw: a 500 x 100 hit cylinder at joint 1 -- the
/// `switch_XX` bone, at the disc's y - 100) that Mario GROUND-POUNDS while standing on the disc to stop it (switchStop: HIP_DROP with his feet within 20 u
/// of the ground and the disc turning -> the speed 0; the `_switch` material's TEVREG0 black, blue while it spins -- the register ADDS over the pad's
/// own red/green texture, so the pad is an ordinary pad while the disc stands and glows violet/cyan while it turns). The game's initMapObj lifts the disc
/// 500 u over its placed point; the stage's three sit at one height and the glb faces make the shallow bowl (the centre disc's face at -140, the middle
/// ring's -95, the outer ring's -80 of their origins). The port: an AnimatableBody3D (sync to physics) Mario rides, its collision a trimesh of the glb's
/// surfaces built at _Ready (the pond `_ike` of the centre disc left out: it is water -- the scene puts an sms_water Area3D there); the switch read
/// from Mario's ground-pound LANDING within SwitchRadiusUnits of the switch bone and SwitchHeightUnits of the face; the switch colour an emission on the
/// `_switch` surface. In the `sms_roulette` group: the boss finds the three by radius (the game's rulet00 is the small centre disc). The intro's fall of
/// the top disc (rulet00.bck, rouletteFall) is not ported.
/// </summary>
public partial class SmsRoulette : AnimatableBody3D
{
    private const float U = SmsUnits.UnitsPerMetre;
    /// <summary>The switch's reach (TRouletteSw: 500 x 100) and how far over the face Mario's pound may land.</summary>
    [Export] public float SwitchRadiusUnits = 500f, SwitchHeightUnits = 100f;
    /// <summary>The `_switch` surface's glow while the disc spins / when it is stopped (TEVREG0 (0, 0, 255) / (0, 0, 0)), added over the pad's own texture
    /// (red pad + blue = violet). Black = no glow: the pad looks like its neighbours.</summary>
    [Export] public Color SwitchSpinColor = new Color(0f, 0f, 1f), SwitchStopColor = new Color(0f, 0f, 0f);
    /// <summary>The glow's strength (1 = the game's plain add of the register).</summary>
    [Export(PropertyHint.Range, "0,4,0.1")] public float SwitchGlowEnergy = 1.5f;
    /// <summary>Build the collision from the glb's surfaces at start (a trimesh of them). Off: the scene's own CollisionShape3D children.</summary>
    [Export] public bool AutoCollision = true;
    /// <summary>The centre disc's pond (`_ike`) is solid floor too. Off: it is left out of the collision (for a water Area3D of the level's own over the basin).</summary>
    [Export] public bool PondSolid = true;
    /// <summary>153c: the centre disc's gold pond PRESSED DOWN into a bowl, as the game's table stands -- rulet00.bck (the clip the intro plays and leaves) ends
    /// with the IKE joint's Y scale at -0.897: the glb's rest dome (+160 u) turned into a 143-u-deep basin King Boo rises out of. Off: the glb's dome.</summary>
    [Export] public bool PondPressed = true;
    /// <summary>The IKE joint's Y scale the clip ends on (-0.897). -1 = the dome mirrored exactly; +1 = the dome.</summary>
    [Export(PropertyHint.Range, "-1.5,1.5,0.01")] public float PondScaleY = -0.8969f;
    /// <summary>Spin it by hand (degrees a game tick) -- the boss sets this when he hides; a disc placed on its own may spin from the start.</summary>
    [Export(PropertyHint.Range, "-2,2,0.01")] public float InitialRollSpeed = 0f;
    [Export] public AudioStreamPlayer3D StopSound;
    [Export] public bool DebugPrint;

    private float _rollSp;                                      // unk13C: degrees a tick
    private float _accum;
    private Node3D _model; private Skeleton3D _skel; private int _switchBone = -1;
    private StandardMaterial3D _switchMat;
    private bool _stoppedByMario;
    private float _radiusU;
    private float _pondJointY; private bool _pondBowl;          // the IKE joint's rest height (mesh space) and whether the bowl is on
    private Vector3 _switchLocal; private bool _switchFromPad;    // the `_switch` pad's centre in this body's space (the bone's place otherwise)
    private Mario _mario; private Mario.MarioState _prevState = Mario.MarioState.idle; private bool _stateKnown;
    /// <summary>For the drives / the boss.</summary>
    public float RollSpeed => _rollSp;
    public bool Spinning => _rollSp != 0f;
    public bool StoppedByMario => _stoppedByMario;
    public float YawDeg => Mathf.RadToDeg(Rotation.Y);
    /// <summary>The glb's radius on the ground plane, units (the boss sorts the three: small = the centre disc, the game's rulet00).</summary>
    public float RadiusUnits => _radiusU;
    /// <summary>The disc's walking face, units: the switch bone's height (switch_XX sits on the face).</summary>
    public float FaceYUnits => SwitchPoint.Y * U;
    public int SwitchStops { get; private set; }
    /// <summary>The pond is the pressed bowl (the centre disc with PondPressed on).</summary>
    public bool PondIsBowl => _pondBowl;
    /// <summary>The pond's lowest / highest point over this body's origin, units (the bowl's bottom, or the dome's top).</summary>
    public float PondYUnits { get; private set; }

    public override void _Ready()
    {
        SyncToPhysics = true;
        SetNotifyTransform(true);                              // a sync-to-physics body only hears its LOCAL transform: moved with its parent (the arena placed at
        AddToGroup("sms_roulette");                            // runtime, a drive), its physics body would stay behind -- _Notification pushes the global one
        _model = GetNodeOrNull<Node3D>("Model");
        if (_model != null)
        {
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null)
                for (int i = 0; i < _skel.GetBoneCount(); i++)
                {
                    string bn = _skel.GetBoneName(i).ToLower();
                    if (bn.StartsWith("switch") && _switchBone < 0) _switchBone = i;
                    if (bn == "ike") { _pondJointY = _skel.GetBoneRest(i).Origin.Y; _pondBowl = PondPressed && !Engine.IsEditorHint(); }
                }
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                var aabb = mi.Mesh.GetAabb();
                _radiusU = Mathf.Max(_radiusU, Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.5f * mi.GlobalTransform.Basis.Scale.X * U);
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    var m = mi.GetActiveMaterial(s);
                    if (m != null && (m.ResourceName ?? "").ToLower().Contains("_ike")) PressPond(mi, s, m);
                    if (m != null && (m.ResourceName ?? "").ToLower().Contains("switch"))
                    {
                        _switchMat = m is StandardMaterial3D sm ? (StandardMaterial3D)sm.Duplicate() : new StandardMaterial3D();
                        mi.SetSurfaceOverrideMaterial(s, _switchMat);
                        // the pad's centre, in this body's space (the switch_XX bone's pose reads oddly on the glb's skin; the pad itself is exact)
                        Vector3[] pv = mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                        if (pv.Length > 0)
                        {
                            Transform3D rel = GlobalTransform.AffineInverse() * mi.GlobalTransform;
                            Vector3 c = Vector3.Zero; foreach (var v in pv) c += rel * v;
                            _switchLocal = c / pv.Length; _switchFromPad = true;
                        }
                    }
                }
                if (AutoCollision && !Engine.IsEditorHint()) BuildCollision(mi);
            }
        }
        _rollSp = InitialRollSpeed;
        ApplySwitchColor();
        if (DebugPrint) GD.Print($"[SMS roulette] {Name}: radius {_radiusU:F0} u, face {FaceYUnits:F0} u, switch bone {_switchBone}, speed {_rollSp}");
    }
    /// <summary>The pond's Y in mesh space under the pressed bowl: mirrored about the IKE joint's plane by PondScaleY (the clip's end pose).</summary>
    private float PondY(float y) => _pondBowl ? _pondJointY + (y - _pondJointY) * PondScaleY : y;
    /// <summary>153c: the gold pond pressed down -- a copy of the `_ike` surface with its vertices mirrored about the IKE joint (the dome's base plane), its
    /// normals flipped and its winding turned so the bowl faces up, on the pond's own material; the skinned dome itself discarded (an alpha-scissor
    /// override nothing passes). The skeleton keeps its rest pose: a negative bone scale would turn the skinned faces inside out for the culling.</summary>
    private void PressPond(MeshInstance3D mi, int s, Material pondMat)
    {
        var arrays = mi.Mesh.SurfaceGetArrays(s);
        Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        float top = float.MinValue, bottom = float.MaxValue;
        foreach (var v in verts) { top = Mathf.Max(top, v.Y); bottom = Mathf.Min(bottom, v.Y); }
        float scaleToBody = (GlobalTransform.AffineInverse() * mi.GlobalTransform).Basis.Scale.Y;
        if (!_pondBowl) { PondYUnits = top * scaleToBody * U; return; }
        PondYUnits = PondY(top) * scaleToBody * U;
        var outArrays = new Godot.Collections.Array(); outArrays.Resize((int)Mesh.ArrayType.Max);
        var pv = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) pv[i] = new Vector3(verts[i].X, PondY(verts[i].Y), verts[i].Z);
        outArrays[(int)Mesh.ArrayType.Vertex] = pv;
        var nv = arrays[(int)Mesh.ArrayType.Normal];
        if (nv.VariantType != Variant.Type.Nil)
        {
            Vector3[] nn = nv.AsVector3Array();
            // the inverse-transpose of the (1, s, 1) scale, then turned to the side we look at (the bowl's inside faces up and inward)
            for (int i = 0; i < nn.Length; i++)
            {
                Vector3 n2 = new Vector3(nn[i].X, nn[i].Y / (Mathf.Abs(PondScaleY) < 1e-3f ? 1e-3f : PondScaleY), nn[i].Z);
                if (PondScaleY < 0f) n2 = -n2;
                nn[i] = n2.Normalized();
            }
            outArrays[(int)Mesh.ArrayType.Normal] = nn;
        }
        var uv = arrays[(int)Mesh.ArrayType.TexUV]; if (uv.VariantType != Variant.Type.Nil) outArrays[(int)Mesh.ArrayType.TexUV] = uv;
        var idx = arrays[(int)Mesh.ArrayType.Index];
        if (idx.VariantType != Variant.Type.Nil)
        {
            int[] ii = idx.AsInt32Array();
            if (PondScaleY < 0f) for (int t = 0; t + 2 < ii.Length; t += 3) { int tmp = ii[t + 1]; ii[t + 1] = ii[t + 2]; ii[t + 2] = tmp; }
            outArrays[(int)Mesh.ArrayType.Index] = ii;
        }
        var bowl = new ArrayMesh(); bowl.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, outArrays);
        bowl.SurfaceSetMaterial(0, pondMat);
        var bowlMi = new MeshInstance3D { Name = "PondBowl", Mesh = bowl, Transform = mi.Transform, CastShadow = mi.CastShadow };
        mi.GetParent().AddChild(bowlMi);
        var hide = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = 1.1f, AlbedoColor = new Color(1f, 1f, 1f, 0f) };
        mi.SetSurfaceOverrideMaterial(s, hide);
        if (DebugPrint) GD.Print($"[SMS roulette] {Name}: the pond pressed to a bowl {PondYUnits:F0} u deep at its centre (the dome top was {top * scaleToBody * U:F0})");
    }
    /// <summary>One trimesh of the glb's surfaces (the pond `_ike` left out when PondSolid is off, pressed to the bowl when PondPressed is on), its vertices
    /// carried into this body's space (a CollisionShape3D cannot be scaled: the glb's 1 / 74.44 is baked into the triangles).</summary>
    private void BuildCollision(MeshInstance3D mi)
    {
        var am = new ArrayMesh();
        Transform3D rel = GlobalTransform.AffineInverse() * mi.GlobalTransform;
        int tris = 0;
        for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
        {
            var m = mi.Mesh.SurfaceGetMaterial(s) ?? mi.GetActiveMaterial(s);   // the mesh's own material: PressPond has overridden the pond's with a nameless one
            string name = (m?.ResourceName ?? "").ToLower();
            bool pond = name.Contains("_ike");
            if (pond && !PondSolid) continue;
            var arrays = mi.Mesh.SurfaceGetArrays(s);
            Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            for (int i = 0; i < verts.Length; i++) verts[i] = rel * (pond ? new Vector3(verts[i].X, PondY(verts[i].Y), verts[i].Z) : verts[i]);
            var outArrays = new Godot.Collections.Array();
            outArrays.Resize((int)Mesh.ArrayType.Max);
            outArrays[(int)Mesh.ArrayType.Vertex] = verts;
            var idx = arrays[(int)Mesh.ArrayType.Index];
            if (idx.VariantType != Variant.Type.Nil) { outArrays[(int)Mesh.ArrayType.Index] = idx; tris += idx.AsInt32Array().Length / 3; }
            else tris += verts.Length / 3;
            am.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, outArrays);
        }
        if (am.GetSurfaceCount() == 0) return;
        var cs = new CollisionShape3D { Name = mi.Name + "Trimesh", Shape = am.CreateTrimeshShape() };
        AddChild(cs);
        if (DebugPrint) GD.Print($"[SMS roulette] {Name}: trimesh of {am.GetSurfaceCount()} surfaces, {tris} triangles, aabb {am.GetAabb()}");
    }
    private Vector3 _lastOrigin; private bool _originKnown;
    /// <summary>A sync-to-physics body hears only its LOCAL transform (and reverts a local change until the physics tick applies it): carried by its parent
    /// (the arena placed or moved at runtime) its physics body stays behind. When the ORIGIN moves (a spin about its own axis never moves it, so the
    /// engine's own flow keeps the rotations), the new place is pushed to the physics server.</summary>
    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what != NotificationTransformChanged || !IsInsideTree() || Engine.IsEditorHint()) return;
        Vector3 o = GlobalTransform.Origin;
        if (_originKnown && o.DistanceSquaredTo(_lastOrigin) < 1e-8f) return;
        _lastOrigin = o; _originKnown = true;
        PhysicsServer3D.BodySetState(GetRid(), PhysicsServer3D.BodyState.Transform, GlobalTransform);
    }
    /// <summary>TRoulette::setRollSp: the speed (degrees a tick), the switch blue, the Mario-stop flag cleared.</summary>
    public void SetRollSp(float degPerTick) { _rollSp = degPerTick; _stoppedByMario = false; ApplySwitchColor(); }
    /// <summary>switchStop's effect: the disc stops, the switch black, the RLT_STOP sound.</summary>
    public void SwitchStop()
    {
        if (_rollSp == 0f) return;
        _rollSp = 0f; _stoppedByMario = true; SwitchStops++; ApplySwitchColor();
        if (StopSound != null && StopSound.Stream != null) StopSound.Play();
        if (DebugPrint) GD.Print($"[SMS roulette] {Name}: stopped by Mario's pound");
    }
    private void ApplySwitchColor()
    {
        if (_switchMat == null) return;
        bool on = _rollSp != 0f;
        Color c = on ? SwitchSpinColor : SwitchStopColor;
        _switchMat.EmissionEnabled = true;
        _switchMat.Emission = c;
        _switchMat.EmissionEnergyMultiplier = c.R + c.G + c.B > 0.001f ? SwitchGlowEnergy : 0f;   // TEVREG0 added over the pad: blue while it turns, nothing (black) at rest
        _switchMat.AlbedoColor = Colors.White;                                                      // the pad keeps its own red/green texture
    }
    /// <summary>The switch's point (the `_switch` pad's centre; else the `switch_XX` bone; else the disc's centre), metres -- it turns with the disc.</summary>
    public Vector3 SwitchPoint => _switchFromPad ? GlobalTransform * _switchLocal : (_skel != null && _switchBone >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(_switchBone).Origin : GlobalPosition);
    /// <summary>TRoulette::moveObject: rotation.y += the speed each game tick; TRouletteSw's HIP_DROP: Mario's ground pound landing on this disc within the
    /// switch's reach while it turns -> switchStop.</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) return;
        int n = SmsUnits.TicksFor(delta, ref _accum);
        if (_rollSp != 0f && n > 0) RotateY(Mathf.DegToRad(_rollSp * n));
        if (_mario == null || !IsInstanceValid(_mario)) { _mario = GetTree().GetFirstNodeInGroup("player") as Mario; _stateKnown = false; }
        if (_mario == null) return;
        var st = _mario.stateOfMario;
        if (_stateKnown && st == Mario.MarioState.groundPoundLanding && _prevState != st) TrySwitch();
        _prevState = st; _stateKnown = true;
    }
    private void TrySwitch()
    {
        if (_rollSp == 0f) return;
        Vector3 sw = SwitchPoint, m = _mario.GlobalPosition;
        float dx = (m.X - sw.X) * U, dz = (m.Z - sw.Z) * U, dy = (m.Y - sw.Y) * U;
        if (dx * dx + dz * dz > SwitchRadiusUnits * SwitchRadiusUnits) return;
        if (dy < -SwitchHeightUnits || dy > SwitchHeightUnits + 20f) return;
        SwitchStop();
    }
}
