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
    /// <summary>SMSGetAnmFrameRate() = 60 / SMSGetVSyncTimesPerSec() = 2 on the 30 fps loop: the spin is 3 degrees x this a frame.</summary>
    public const float AnmFrameRate = 2f;

    /// <summary>mShines[i]->mIsSelected: the flag and the game's play / stop of the shine's emitters.</summary>
    private void Select(int i, bool on)
    {
        if (i < 0 || i >= 8 || _slots[i] == null) return;
        _slots[i].Selected = on;
        SetSparkle(_slots[i], on);
    }

    /// <summary>The shine mesh (a glb whose root holds MeshInstance3Ds); the game's body is 165 units tall -- the mesh is scaled to it.</summary>
    [Export] public PackedScene ShineScene;
    /// <summary>The hollow (open, not cleared) shine's tint -- his ShineSprite.EmptyTint.</summary>
    [Export] public Color EmptyTint = new Color(0.72f, 0.82f, 0.95f, 0.55f);
    /// <summary>The game's shine_menu model is 535 units tall with its glow quad (the body 165 of them); the mesh's bounds are scaled to this
    /// (his ShineSprite.glb measures the same 535: scale 1).</summary>
    [Export] public float ModelHeightUnits = 535f;
    /// <summary>Degrees added to every shine's yaw: the game's model faces +Z (toward the camera at angle 0); if the glb faces -Z set 180.</summary>
    [Export] public float MeshYawOffset = 0f;
    /// <summary>127d: the GAME's body materials as shaders (shine_menu.bmd / shine_menu_empty.bmd's TEVs: SelectShineFull / SelectShineEmpty
    /// .gdshader, loaded from assets/Sms/Gate when empty) and their radgrad texture (P_ms_radgrad_i: fx/radgrad.png when empty).</summary>
    [Export] public Shader FullShader;
    [Export] public Shader EmptyShader;
    [Export] public Texture2D RadgradTexture;
    /// <summary>x the full shine's two highlights (the cream sheen and the white spot); 1 = the game.</summary>
    [Export(PropertyHint.Range, "0,2")] public float BodyHighlight = 1f;
    /// <summary>x the full shine's light (the select's one light, frontal; the ambient is the bmd's 0.5). 127f: 1.6 -- the select scene sets no
    /// light of its own, and the game's lemon-yellow body (248, 248, 88 in a real screenshot) needs 0.5 + 2.0 x N.L over the orange (225, 120, 30) -- 1.6 left the lower dome gold;
    /// 1.0 (127d) gave an orange body with cream patches.</summary>
    [Export(PropertyHint.Range, "0,3")] public float BodyLight = 2.0f;
    /// <summary>127f: the body's opacity at the FRONT of the ring (the selected shine and its neighbours). The game draws them at 0.72 -- the
    /// bpk's depth fade sits at frame 30 for anything nearer than 600 units -- which reads see-through over a blue stage; 1 = opaque
    /// (DEVIATION, his call). The shines further back fade from this level to nothing, as in the game.</summary>
    [Export(PropertyHint.Range, "0.3,1")] public float FrontAlpha = 1f;
    /// <summary>127g DEVIATION (off = Jaysonn's call): the hollow shine's glass look is mapped by the VIEW-space normal in the game, so turned edge-on
    /// every visible face is 'rim' and the whole star goes opaque light blue (his screenshot 2); off locks the look to the star's own face -- the
    /// grey-blue glass of his screenshot 1 at every spin angle, light blue only on the bevels. On = the game's mapping.</summary>
    [Export] public bool EmptyRimFollowsView = false;
    /// <summary>The stand-in colours when the shader files are missing (his shine_sprite.tscn gold; the hollow tint).</summary>
    [Export] public Color BodyColor = new Color(1f, 0.8f, 0.2f);
    /// <summary>The additive glow behind each shine: the model's own glow texture (ShineSprite_shine_0.png, the radial gradient) by default; its
    /// quad in the bmd is 562 x 535 units (the whole model's height) about the starglow joint; the bpk colours it (250, 240, 154) at alpha 155.</summary>
    [Export] public Texture2D GlowTexture;
    [Export] public float GlowSizeUnits = 540f;
    [Export] public Color GlowColor = new Color(250f / 255f, 240f / 255f, 154f / 255f, 155f / 255f);
    /// <summary>Where the glow and the sparkles sit above the slot: the starglow / body joint, 71.4 units up (the body spans 0 .. 165).</summary>
    [Export] public float BodyCentreUnits = 71.4f;
    /// <summary>127b: the game's sparkle emitters on a full shine (ms_2d_shine_kira + senko) and on a hollow one (kira_em), playing on the selected.</summary>
    [Export] public PackedScene SparkleScene;
    [Export] public PackedScene SparkleEmptyScene;
    [Export] public bool DebugPrint;

    private class Slot
    {
        public Node3D Node, Sparkle; public MeshInstance3D[] Meshes; public StandardMaterial3D[] Mats; public Color[] Base; public ShaderMaterial[] ShaderMats;
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
        Select(_selected, true);
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
        // his shine scene hides the glb's glow dome (the "_starglow1" surface: a bowl when drawn as imported) and the exporter's stray plane,
        // and draws its own additive billboard glow instead (ShineSprite.cs BuildShineEffects) -- the same here
        foreach (var mi in meshes) if (mi.Name.ToString().Contains("PlaneObject")) mi.Visible = false;
        var mats = new System.Collections.Generic.List<StandardMaterial3D>(); var bases = new System.Collections.Generic.List<Color>(); var shaderMats = new System.Collections.Generic.List<ShaderMaterial>();
        MeshInstance3D body = null; int bodySurface = -1;
        foreach (var mi in meshes)
        {
            if (!mi.Visible || mi.Mesh == null) continue;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var m = mi.GetActiveMaterial(i);
                string mn = m?.ResourceName ?? "";
                if (mn.Contains("starglow"))
                {   // the dome: off
                    mi.SetSurfaceOverrideMaterial(i, new StandardMaterial3D { ResourceName = "_starglow_hidden", Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1f, 1f, 1f, 0f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded });
                    continue;
                }
                bool eyes = mn.Contains("eyes");
                if (eyes)
                {   // 127c: the eyes keep their own texture (the black oval with its highlight: its alpha IS the eye's shape), unlit
                    var copy = m is StandardMaterial3D em ? (StandardMaterial3D)em.Duplicate() : new StandardMaterial3D();
                    copy.ResourceName = "_mat_shine_eyes (select)";
                    copy.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    copy.AlbedoColor = Colors.White;
                    copy.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    copy.CullMode = BaseMaterial3D.CullModeEnum.Back;
                    copy.RenderPriority = 1;   // 127f: drawn AFTER the body -- both are alpha-blended at one depth, and an opaque body drawn second painted the eyes out
                    mi.SetSurfaceOverrideMaterial(i, copy);
                    mats.Add(copy); bases.Add(copy.AlbedoColor);
                    continue;
                }
                // 127d: the body is the GAME's material, its TEV in a shader (SelectShineFull / SelectShineEmpty.gdshader, read from the two
                // bmds): the full shine the lit orange (225, 120, 30) with the cream and white highlights mapped by the view normal, z-written
                // so the star occludes itself; the empty one the translucent blue glass. The bpk's fade is the shader's `fade`.
                var shader = s.State == 3 ? (FullShader ?? LoadShader("res://assets/Sms/Gate/SelectShineFull.gdshader")) : (EmptyShader ?? LoadShader("res://assets/Sms/Gate/SelectShineEmpty.gdshader"));
                if (shader != null)
                {
                    var sm = new ShaderMaterial { Shader = shader, ResourceName = s.State == 3 ? "_mat_shine_body (select, the bmd's TEV)" : "_mat_shine_body empty (select, the bmd's TEV)" };
                    sm.SetShaderParameter("radgrad", RadgradTexture ?? (ResourceLoader.Exists("res://assets/Sms/Gate/fx/radgrad.png") ? GD.Load<Texture2D>("res://assets/Sms/Gate/fx/radgrad.png") : null));
                    sm.SetShaderParameter("fade", FrontAlpha);
                    if (s.State == 3) { sm.SetShaderParameter("highlight", BodyHighlight); sm.SetShaderParameter("light_strength", BodyLight); }
                    else
                    {   // 127g: the glass look locked to the star's own face (SelectShineEmpty.gdshader rim_follows_view = false): the face axis is the
                        // shine node's +Z (toward the camera at spin 0) expressed in the mesh's object space, through the glb's own node transforms
                        Transform3D rel = Transform3D.Identity;
                        for (Node3D p = mi; p != null && p != s.Node; p = p.GetParent() as Node3D) rel = p.Transform * rel;
                        Vector3 axis = rel.Basis.Inverse() * Vector3.Back;
                        sm.SetShaderParameter("face_axis", axis.Normalized());
                        sm.SetShaderParameter("rim_follows_view", EmptyRimFollowsView);
                    }
                    mi.SetSurfaceOverrideMaterial(i, sm);
                    shaderMats.Add(sm);
                }
                else
                {   // no shader files: a flat stand-in
                    var copy = new StandardMaterial3D { ResourceName = "_mat_shine_body (select, stand-in)", AlbedoColor = s.State == 3 ? BodyColor : EmptyTint, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Back };
                    mi.SetSurfaceOverrideMaterial(i, copy);
                    mats.Add(copy); bases.Add(copy.AlbedoColor);
                }
                if (body == null) { body = mi; bodySurface = i; }
            }
        }
        // the game's shine_menu model is 535 units tall with its glow quad; scale the mesh to it
        Aabb box = new Aabb(); bool first = true;
        foreach (var mi in meshes) { if (!mi.Visible) continue; var b = mi.GlobalTransform * mi.GetAabb(); box = first ? b : box.Merge(b); first = false; }
        if (!first && box.Size.Y > 1e-4f) inst.Scale = Vector3.One * (ModelHeightUnits / box.Size.Y) * inst.Scale.Y;
        // the glow: the model's own glow quad re-drawn as an additive billboard (the glb turned it into a dome): the bmd's starglow quad is 562 x 535
        // units about the starglow joint (71.4 up), its texture the radial gradient the exporter saved as ShineSprite_shine_0.png, its colour the
        // bpk's register (250, 240, 154) at alpha 155 for the full shine and white at 155 for the hollow one
        Vector3 centre = new Vector3(0f, BodyCentreUnits, 0f);
        Color glowCol = s.State == 3 ? GlowColor : new Color(1f, 1f, 1f, GlowColor.A);   // the empty bmd's starglow is white at the same alpha
        var glowMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = false,
            AlbedoColor = glowCol,
            AlbedoTexture = GlowTexture ?? (ResourceLoader.Exists("res://models/ShineSprite/ShineSprite_shine_0.png") ? GD.Load<Texture2D>("res://models/ShineSprite/ShineSprite_shine_0.png") : null),
        };
        var glow = new MeshInstance3D { Name = "Glow", Mesh = new QuadMesh { Size = new Vector2(GlowSizeUnits, GlowSizeUnits) }, MaterialOverride = glowMat, Position = centre, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        s.Node.AddChild(glow);
        mats.Add(glowMat); bases.Add(glowMat.AlbedoColor);
        // the sparkles: the game's emitters on the shine (promi / senko / kira for a full one, kira_em for a hollow one), playing while selected
        var sparkleScene = s.State == 3 ? SparkleScene : SparkleEmptyScene;
        if (sparkleScene != null)
        {
            s.Sparkle = sparkleScene.Instantiate<Node3D>();
            s.Node.AddChild(s.Sparkle);
            SetSparkle(s, false);
        }
        s.Meshes = meshes.ToArray();
        s.Mats = mats.ToArray(); s.Base = bases.ToArray(); s.ShaderMats = shaderMats.ToArray();
    }

    /// <summary>playCreateParticle / stopCreateParticle on the shine's emitters.</summary>
    private static void SetSparkle(Slot s, bool on)
    {
        if (s.Sparkle == null) return;
        foreach (Node c in s.Sparkle.GetChildren()) if (c is GpuParticles3D p) p.Emitting = on;
    }

    private static Shader LoadShader(string path) => ResourceLoader.Exists(path) ? GD.Load<Shader>(path) : null;

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
        Select(_selected, false);
        _selected += step;
        Select(_selected, true);
    }

    public void StartDecrease(int step)
    {
        _decreasing = true; _ringSpeed = step * SlotAngleUnits / 10f;
        Select(_selected, false);
        _selected -= step;
        Select(_selected, true);
    }

    /// <summary>startClose: the others fade out (their frame runs to the end), the selected one finishes its turn and stops.</summary>
    public void StartClose()
    {
        Select(_selected, false);
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
            float alpha = 1f - (float)s.AnmFrame / FullFrame;                         // the game's bpk fade: 0.7 at the front (frame 30), 0 at 99
            for (int m = 0; m < s.Mats.Length; m++) { Color c = s.Base[m]; c.A = s.Base[m].A * alpha; s.Mats[m].AlbedoColor = c; }
            // 127f: the body shaders take FrontAlpha at the front and fade from there (alpha / the frame-30 value); the glow keeps the game's curve
            float bodyAlpha = Mathf.Clamp(FrontAlpha * alpha / (1f - 30f / FullFrame), 0f, 1f);
            if (s.ShaderMats != null) foreach (var sm in s.ShaderMats) sm.SetShaderParameter("fade", bodyAlpha);
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
            s.SpinAngle += 3f * AnmFrameRate;
            if (s.SpinAngle > 360f) s.SpinAngle -= 360f;
        }
        else if (s.SpinAngle != 0f)
        {   // not selected any more: the turn runs on to 360 and stops there
            s.SpinAngle += 3f * AnmFrameRate;
            if (s.SpinAngle >= 360f) s.SpinAngle = 0f;
        }
        s.BobPhase += s.BobSpeed;
        if (s.BobPhase > 4f) s.BobPhase = 0f;
    }
}
