using Godot;

/// <summary>
/// The episode select's ring of shines -- TSelectShineManager and TSelectShine (GC2D/SelectShine2.cpp; SMS pass 127). Eight slots on an
/// ellipse about cCenter (300, 160, -9000): slot i at angle (ring + 40 i) in the game's units (x 57.3 of them a short angle: one slot is
/// 12.6 degrees), position (1500 sin a + 300, 160, 9000 cos a - 9000) -- the selected slot stands at z 0, 1300 in front of the camera at
/// (300, 240, 1300) looking down -Z with a 30-degree view, its neighbours 327 right / 216 back, then 939 / 857. A cleared episode is the
/// full shine (shine_menu.bmd), an open one the hollow (shine_menu_empty.bmd); each faces the camera (getAngle), bobs 10 units on a
/// four-piece curve at 0.01 of a cycle a frame from a random phase, and fades with depth: the bpk frame is -0.05 z (at least 30), its
/// alpha 1 - frame / 99 -- so the selected one shows at 70 %, the next pair at 57 %, the third pair barely. The selected spins 3 degrees
/// a frame; a step turns the ring a slot over 10 frames (startIncrease / startDecrease); startClose fades every other shine out (+8 frames
/// a frame) while the selected one finishes its turn. His shine model (models/ShineSprite/ShineSprite.glb) stands in for the two bmds,
/// tinted see-through for the hollow ones (the game's empty shine look, ShineSprite.EmptyTint).
/// </summary>
public partial class SmsSelectShineRing : Node3D
{
    public static readonly Vector3 Centre = new Vector3(300f, 160f, -9000f);
    public const float SlotAngleUnits = 40f;              // the game's step in its own units
    public const float UnitsToDegrees = 57.295776f * 360f / 65536f;   // x 57.3 = a short angle (65536 a turn)
    public const int FullFrame = 99;                       // the bpk's last frame (alpha 0)

    /// <summary>The shine mesh (a glb whose root holds MeshInstance3Ds); the game's body is 165 units tall -- the mesh is scaled to it.</summary>
    [Export] public PackedScene ShineScene;
    /// <summary>The hollow (open, not cleared) shine's tint -- his ShineSprite.EmptyTint.</summary>
    [Export] public Color EmptyTint = new Color(0.72f, 0.82f, 0.95f, 0.55f);
    /// <summary>The game's shine_menu model is 535 units tall with its glow quad (the body 165 of them); the mesh's bounds are scaled to this
    /// (his ShineSprite.glb measures the same 535: scale 1).</summary>
    [Export] public float ModelHeightUnits = 535f;
    /// <summary>Degrees added to every shine's yaw: the game's model faces +Z (toward the camera at angle 0); if the glb faces -Z set 180.</summary>
    [Export] public float MeshYawOffset = 0f;
    [Export] public bool DebugPrint;

    private class Slot
    {
        public Node3D Node; public MeshInstance3D[] Meshes; public StandardMaterial3D[] Mats; public Color[] Base;
        public int State; public bool Selected, Closing, Opening; public float BobPhase, BobSpeed = 0.01f, BobHeight = 10f, SpinAngle, AngleY;
        public int AnmFrame; public Vector3 Pos, Offset;
    }
    private readonly Slot[] _slots = new Slot[8];
    private int _num, _selected;
    private float _ringAngle, _ringSpeed;
    private bool _increasing, _decreasing, _closing;
    private readonly RandomNumberGenerator _rng = new();

    public int Selected => _selected;
    public float RingAngle => _ringAngle;
    public bool Turning => _increasing || _decreasing;
    public bool Closing => _closing;
    /// <summary>The slot's world position (for the drive) and bpk frame.</summary>
    public Vector3 SlotPosition(int i) => _slots[i]?.Node?.GlobalPosition ?? Vector3.Zero;
    public int SlotFrame(int i) => _slots[i]?.AnmFrame ?? -1;
    public float SlotSpin(int i) => _slots[i]?.SpinAngle ?? 0f;
    public bool SlotExists(int i) => _slots[i] != null;

    /// <summary>initData(states, shine_num, selected): states 3 = cleared (the full shine), 2 = open (the hollow), 0 = none.</summary>
    public void Init(int[] states, int num, int selected)
    {
        foreach (Node c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
        _num = Mathf.Clamp(num, 0, 8); _selected = selected;
        _ringAngle = _selected * -SlotAngleUnits;
        for (int i = 0; i < 8; i++)
        {
            _slots[i] = null;
            if (i >= _num || states[i] == 0) continue;
            var s = new Slot { State = states[i], BobPhase = (int)(4000f * _rng.Randf()) / 1000f };
            s.Node = new Node3D { Name = $"Shine{i}" };
            AddChild(s.Node);
            BuildMesh(s);
            s.Pos = RingPosition(_ringAngle + i * SlotAngleUnits);
            s.AngleY = FacingAngle(s.Pos);
            s.Node.Position = s.Pos;
            s.Node.Rotation = new Vector3(0f, Mathf.DegToRad(s.AngleY + MeshYawOffset), 0f);
            _slots[i] = s;
        }
        if (_slots[_selected] != null) _slots[_selected].Selected = true;
        _increasing = _decreasing = _closing = false;
        if (DebugPrint) GD.Print($"[SMS select ring] {_num} shines, selected {_selected}, ring angle {_ringAngle}");
    }

    private void BuildMesh(Slot s)
    {
        if (ShineScene == null) return;
        var inst = ShineScene.Instantiate<Node3D>();
        s.Node.AddChild(inst);
        var list = inst.FindChildren("*", "MeshInstance3D", true, false);
        var meshes = new System.Collections.Generic.List<MeshInstance3D>();
        foreach (Node n in list) if (n is MeshInstance3D mi) meshes.Add(mi);
        if (inst is MeshInstance3D rootMi) meshes.Add(rootMi);
        // the game's body is 165 units: scale the whole mesh to it
        Aabb box = new Aabb(); bool first = true;
        foreach (var mi in meshes) { var b = mi.GlobalTransform * mi.GetAabb(); box = first ? b : box.Merge(b); first = false; }
        if (!first && box.Size.Y > 1e-4f) inst.Scale = Vector3.One * (ModelHeightUnits / box.Size.Y) * inst.Scale.Y;
        s.Meshes = meshes.ToArray();
        var mats = new System.Collections.Generic.List<StandardMaterial3D>(); var bases = new System.Collections.Generic.List<Color>();
        foreach (var mi in meshes)
        {
            for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++)
            {
                var m = mi.GetActiveMaterial(i);
                StandardMaterial3D copy = m is StandardMaterial3D sm ? (StandardMaterial3D)sm.Duplicate() : new StandardMaterial3D { AlbedoColor = new Color(0.88f, 0.47f, 0.12f) };
                copy.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                if (s.State != 3) { copy.AlbedoColor = EmptyTint; copy.AlbedoTexture = null; copy.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; }
                mi.SetSurfaceOverrideMaterial(i, copy);
                mats.Add(copy); bases.Add(copy.AlbedoColor);
            }
        }
        s.Mats = mats.ToArray(); s.Base = bases.ToArray();
    }

    /// <summary>getPosition(angle): the ring in the game's units.</summary>
    public static Vector3 RingPosition(float angleUnits)
    {
        float a = Mathf.DegToRad(angleUnits * UnitsToDegrees);
        return new Vector3(1500f * Mathf.Sin(a) + Centre.X, Centre.Y, 9000f * Mathf.Cos(a) + Centre.Z);
    }

    /// <summary>getAngle(pos): the yaw that faces the camera at (300, 1300), degrees.</summary>
    public static float FacingAngle(Vector3 pos)
    {
        Vector2 dir = new Vector2(300f, 1300f) - new Vector2(pos.X, pos.Z);
        Vector2 fwd = new Vector2(0f, 1f);
        float ang = Mathf.RadToDeg(Mathf.Abs(Mathf.Atan2(dir.Cross(fwd), dir.Dot(fwd))));
        if (pos.X > Centre.X) ang = -ang;
        return ang;
    }

    /// <summary>startIncrease(step): the selection moves right by step slots; the ring turns a slot in 10 frames.</summary>
    public void StartIncrease(int step)
    {
        _increasing = true; _ringSpeed = step * -SlotAngleUnits / 10f;
        if (_slots[_selected] != null) _slots[_selected].Selected = false;
        _selected += step;
        if (_slots[_selected] != null) _slots[_selected].Selected = true;
    }

    public void StartDecrease(int step)
    {
        _decreasing = true; _ringSpeed = step * SlotAngleUnits / 10f;
        if (_slots[_selected] != null) _slots[_selected].Selected = false;
        _selected -= step;
        if (_slots[_selected] != null) _slots[_selected].Selected = true;
    }

    /// <summary>startClose: the others fade out (their frame runs to the end), the selected one finishes its turn and stops.</summary>
    public void StartClose()
    {
        if (_slots[_selected] != null) _slots[_selected].Selected = false;
        for (int i = 0; i < 8; i++)
        {
            var s = _slots[i];
            if (s != null && i != _selected && !s.Closing) { s.Closing = true; s.Opening = false; }
        }
        _closing = true;
    }

    /// <summary>perform(CUE_MOVE | CUE_CALC_ANIM): one 30 fps frame.</summary>
    public void Frame()
    {
        for (int i = 0; i < 8; i++) if (_slots[i] != null) Move(_slots[i]);
        if (_increasing || _decreasing)
        {
            _ringAngle += _ringSpeed;
            if (_increasing && _ringAngle < _selected * -SlotAngleUnits) { _ringAngle = _selected * -SlotAngleUnits; _increasing = false; }
            if (_decreasing && _ringAngle > _selected * -SlotAngleUnits) { _ringAngle = _selected * -SlotAngleUnits; _decreasing = false; }
            for (int i = 0; i < 8; i++)
            {
                var s = _slots[i]; if (s == null) continue;
                s.Pos = RingPosition(_ringAngle + i * SlotAngleUnits);
                s.AngleY = FacingAngle(s.Pos + s.Offset);
            }
        }
        for (int i = 0; i < 8; i++)
        {
            var s = _slots[i]; if (s == null) continue;
            s.Node.Position = s.Pos + s.Offset;
            s.Node.Rotation = new Vector3(0f, Mathf.DegToRad(s.AngleY + s.SpinAngle + MeshYawOffset), 0f);
            float alpha = 1f - (float)s.AnmFrame / FullFrame;
            for (int m = 0; m < s.Mats.Length; m++) { Color c = s.Base[m]; c.A = s.Base[m].A * alpha; s.Mats[m].AlbedoColor = c; }
            s.Node.Visible = alpha > 0.002f;
        }
    }

    private static float Bezier(float t, float p0, float p1, float p2) { float u = 1f - t; return p0 * u * u + p1 * 2f * u * t + p2 * t * t; }

    /// <summary>TSelectShine::move: the bob, the depth fade (or the close's run-out), the spin.</summary>
    private void Move(Slot s)
    {
        float h = s.BobHeight, p = s.BobPhase, height;
        if (p < 1f) height = Bezier(p, 0f, h * 0.9f, h);
        else if (p < 2f) height = Bezier(p - 1f, h, h * 0.9f, 0f);
        else if (p < 3f) height = Bezier(p - 2f, 0f, -h * 0.9f, -h);
        else height = Bezier(p - 3f, -h, -h * 0.9f, 0f);
        s.Offset.Y = height;
        int frame = (int)(-0.05f * (s.Pos.Z + s.Offset.Z));
        if (frame < 30) frame = 30;
        if (s.Opening) { s.AnmFrame -= 8; if (s.AnmFrame < frame) s.AnmFrame = frame; }
        else if (s.Closing) { s.AnmFrame += 8; if (s.AnmFrame > FullFrame) s.AnmFrame = FullFrame; }
        else s.AnmFrame = Mathf.Clamp(frame, 0, FullFrame);
        if (s.Selected)
        {
            s.SpinAngle += 3f;
            if (s.SpinAngle > 360f) s.SpinAngle -= 360f;
        }
        else if (s.SpinAngle != 0f)
        {   // not selected any more: the turn runs on to 360 and stops there
            s.SpinAngle += 3f;
            if (s.SpinAngle >= 360f) s.SpinAngle = 0f;
        }
        s.BobPhase += s.BobSpeed;
        if (s.BobPhase > 4f) s.BobPhase = 0f;
    }
}
