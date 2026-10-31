using System;
using Godot;

/// <summary>
/// The game's dive splash MODEL (pass 134): TMarioEffect keeps two MActors of mario/04_tobikomi (chr0.szs) and fires one at the water
/// surface under Mario when MARIO_FLAG_IN_ANY_WATER flips (MarioParticle.cpp inOutWaterEffect -> MarioEffect.cpp setJumpIntoWaterEffect /
/// ...Small). The model: a 12-sided water column (162 u tall, r 34 -> 44), drawn twice over the same cone -- `_mizubashira` (the droplet
/// dissolve) and `_mizubashira1r` (wave on wave, the reversed copy); a wider crown cone (78 u, r 51 -> 67), outside and inside
/// (`_mizubashira2` / `2r`, GX_CULL_BACK on opposite windings); and a flat 150 u ring quad (`_hamon`) 5 u up. Five shapes on five joints
/// under the root `bashan` (scale 1.5). 04_tobikomi.bck (79 frames at 60/s): the column and the crown rise and widen to frame 48 and are
/// gone by 54 (the crown climbs 2 -> 54 u and back to 4), the ring widens 0.1 -> 2.94 to the end; .bpk: the material alphas 200 / 150 /
/// 255 -> 0 over frames 27..50; .brk: the ring's K0 alpha 200 -> 0 over 25..80; .btk: the droplet texture scrolls down the column and the
/// crown (T.y 0 -> 1.5), the wave copy 0.2 -> 0.8, the crown's droplets squash 1 -> 0.4. All of it is baked a key a frame into the
/// scene's AnimationPlayer by gen_splash_model.py; it runs on the physics clock (60 Hz = the bck rate) and the node frees itself when the
/// clip completes (TMarioEffect::perform: unk6C[i] = 0 on STATE_COMPLETED_ONCE). The meshes are built here from the bmd's exact vertices,
/// so the scene carries no binary surfaces (and the editor shows the rest pose).
/// <para>Big (setJumpIntoWaterEffect): the whole model at |vel.y|'s scale, JumpIntoScaleMin 0.7 + JumpIntoScaleWidth 0.3 x the ratio of
/// |vel.y| between JumpIntoMinY 20 and JumpIntoMaxY 50 u/f (watereffect.prm), only at |vel.y| >= JumpIntoMdlEffectSpY 10 and over 50 u of
/// water. Small (setJumpIntoWaterEffectSmall, the shallow-water flag): shape packets 1, 2, 4 -- the ring and both crown cones -- hidden,
/// the column at (0.8, 0.4, 0.8), no speed scale. Deviation, logged: the game writes the scale over the diagonal of Mario's facing matrix
/// and leaves the facing's sin terms in (at 90 degrees the model is sqrt(s^2 + 1) / s wider); here the scale is uniform.</para>
/// </summary>
[Tool]
public partial class SmsSplashModel : Node3D
{
    // 04_tobikomi.bmd, in game units (bmd_geom.py): the column (shapes 0 / 3), the crown (shapes 2 / 4), the ring (shape 1)
    private const float ColumnBottomY = -30.328f, ColumnBottomR = 34.18f, ColumnTopY = 132.008f, ColumnTopR = 44.392f;
    private const float CrownBottomY = -12.586f, CrownBottomR = 51.056f, CrownTopY = 65.102f, CrownTopR = 67.0f;
    private const float RingHalf = 75f, RingY = 5f;
    private const int Segments = 12;
    private const float USeam = 0.082f;          // the bmd's first column of u: the wave band's seam
    /// <summary>The root joint `bashan`.</summary>
    public const float RootScale = 1.5f;
    /// <summary>04_tobikomi.bck's length, at 60 frames a second.</summary>
    public const int Frames = 79;
    /// <summary>watereffect.prm (sms_shipped_params.json): the model's speed gate and its scale over the speed.</summary>
    public const float JumpIntoMdlEffectSpY = 10f, JumpIntoMinY = 20f, JumpIntoMaxY = 50f, JumpIntoScaleMin = 0.7f, JumpIntoScaleWidth = 0.3f;

    /// <summary>Knob: the whole model's size, 1 = the game.</summary>
    [ExportGroup("Deviations (1 = the game)")]
    [Export(PropertyHint.Range, "0,3,0.05")] public float SizeScale { get; set; } = 1f;
    /// <summary>Knob: the brightness of all five materials, 1 = the game.</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float Intensity { get; set; } = 1f;

    /// <summary>The small variant (the shallow-water splash) is playing.</summary>
    public bool Small { get; private set; }
    /// <summary>The speed scale given to Play (1 for the small variant).</summary>
    public float ModelScale { get; private set; } = 1f;
    /// <summary>|vel.y| in u/f the spawner measured, for the drives.</summary>
    public float LaunchVyUnits { get; set; }
    /// <summary>The clip completed; the node is freeing.</summary>
    public bool Done { get; private set; }
    /// <summary>The clip's frame now (0..79).</summary>
    public int Frame => _anim == null ? 0 : (int)MathF.Round((float)_anim.CurrentAnimationPosition * 60f);

    private AnimationPlayer _anim;
    private static readonly string[] JointNodes = { "J1", "J2", "J3", "J4", "J5" };
    private static readonly string[] SmallHidden = { "J5", "J4", "J1" };   // shape packets 1 (the ring), 2 and 4 (the crown)

    /// <summary>The game's scale for a dive at <paramref name="absVyUnits"/> u/f (setJumpIntoWaterEffect).</summary>
    public static float ScaleForSpeed(float absVyUnits)
    {
        float ratio = Mathf.Clamp((absVyUnits - JumpIntoMinY) / (JumpIntoMaxY - JumpIntoMinY), 0f, 1f);
        return JumpIntoScaleMin + JumpIntoScaleWidth * ratio;
    }

    public override void _Ready()
    {
        BuildMeshes();
        _anim = GetNodeOrNull<AnimationPlayer>("Anim");
        if (Engine.IsEditorHint() || _anim == null) return;
        _anim.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
        _anim.AnimationFinished += _ => { Done = true; QueueFree(); };
    }

    /// <summary>Fire at <paramref name="surfacePos"/> (x, the water surface, z): <paramref name="small"/> = setJumpIntoWaterEffectSmall,
    /// else setJumpIntoWaterEffect at <paramref name="scale"/> (see <see cref="ScaleForSpeed"/>). World-anchored (setBaseTRMtx once).</summary>
    public void Play(Vector3 surfacePos, bool small, float scale = 1f)
    {
        Small = small;
        ModelScale = small ? 1f : scale;
        TopLevel = true;
        Vector3 s = Vector3.One * (SmsUnits.UnitsToMetres(1f) * RootScale * ModelScale * SizeScale);
        if (small) s *= new Vector3(0.8f, 0.4f, 0.8f);
        GlobalTransform = new Transform3D(Basis.FromScale(s), surfacePos);
        foreach (string j in JointNodes)
            if (GetNodeOrNull<Node3D>(j) is Node3D joint)
                joint.Visible = !(small && Array.IndexOf(SmallHidden, j) >= 0);
        foreach (string j in JointNodes)
            if (GetNodeOrNull<MeshInstance3D>($"{j}/Mesh")?.MaterialOverride is ShaderMaterial sm)
                sm.SetShaderParameter("intensity", Intensity);
        if (_anim == null) { QueueFree(); return; }
        _anim.Stop();
        _anim.Play("splash");
    }

    /// <summary>For the drives: a material's parameter as the clip has it now.</summary>
    public Variant MaterialParam(string joint, string param) =>
        GetNodeOrNull<MeshInstance3D>($"{joint}/Mesh")?.MaterialOverride is ShaderMaterial sm ? sm.GetShaderParameter(param) : default;

    private void BuildMeshes()
    {
        Assign("J3/Mesh", Cone(ColumnBottomY, ColumnBottomR, ColumnTopY, ColumnTopR, false));   // shape 0  _mizubashira
        Assign("J2/Mesh", Cone(ColumnBottomY, ColumnBottomR, ColumnTopY, ColumnTopR, true));    // shape 3  _mizubashira1r (reversed)
        Assign("J4/Mesh", Cone(CrownBottomY, CrownBottomR, CrownTopY, CrownTopR, false));       // shape 2  _mizubashira2
        Assign("J1/Mesh", Cone(CrownBottomY, CrownBottomR, CrownTopY, CrownTopR, true));        // shape 4  _mizubashira2r (reversed)
        Assign("J5/Mesh", Ring());                                                              // shape 1  _hamon
    }

    private void Assign(string path, Mesh mesh)
    {
        if (GetNodeOrNull<MeshInstance3D>(path) is MeshInstance3D mi && mi.Mesh == null) mi.Mesh = mesh;
    }

    /// <summary>The bmd's strip: a segment every 30 degrees from -30 clockwise, (top_i, bottom_i, top_i+1) then (top_i+1, bottom_i,
    /// bottom_i+1), u = 0.082 + i / 12, v 0 at the top and 1 at the bottom; the outward face is the front (counter-clockwise), the
    /// reversed shapes swap two vertices.</summary>
    private static ArrayMesh Cone(float yb, float rb, float yt, float rt, bool inward)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < Segments; i++)
        {
            float a0 = Mathf.DegToRad(-30f - 30f * i), a1 = Mathf.DegToRad(-30f - 30f * (i + 1));
            float u0 = USeam + i / (float)Segments, u1 = USeam + (i + 1) / (float)Segments;
            Vector3 t0 = new(rt * MathF.Cos(a0), yt, rt * MathF.Sin(a0)), b0 = new(rb * MathF.Cos(a0), yb, rb * MathF.Sin(a0));
            Vector3 t1 = new(rt * MathF.Cos(a1), yt, rt * MathF.Sin(a1)), b1 = new(rb * MathF.Cos(a1), yb, rb * MathF.Sin(a1));
            Tri(st, (t0, new Vector2(u0, 0f)), (b0, new Vector2(u0, 1f)), (t1, new Vector2(u1, 0f)), inward);
            Tri(st, (t1, new Vector2(u1, 0f)), (b0, new Vector2(u0, 1f)), (b1, new Vector2(u1, 1f)), inward);
        }
        return st.Commit();
    }

    /// <summary>Shape 1: the 150 x 150 u quad 5 u up, uv (0,1) at (-75, -75) .. (1,0) at (75, 75).</summary>
    private static ArrayMesh Ring()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var a = (new Vector3(-RingHalf, RingY, -RingHalf), new Vector2(0f, 1f));
        var b = (new Vector3(RingHalf, RingY, -RingHalf), new Vector2(1f, 1f));
        var c = (new Vector3(-RingHalf, RingY, RingHalf), new Vector2(0f, 0f));
        var d = (new Vector3(RingHalf, RingY, RingHalf), new Vector2(1f, 0f));
        Tri(st, a, b, c, false);
        Tri(st, c, b, d, false);
        return st.Commit();
    }

    private static void Tri(SurfaceTool st, (Vector3 p, Vector2 uv) v0, (Vector3 p, Vector2 uv) v1, (Vector3 p, Vector2 uv) v2, bool flip)
    {
        if (flip) (v1, v2) = (v2, v1);
        st.SetUV(v0.uv); st.AddVertex(v0.p);
        st.SetUV(v1.uv); st.AddVertex(v1.p);
        st.SetUV(v2.uv); st.AddVertex(v2.p);
    }
}
