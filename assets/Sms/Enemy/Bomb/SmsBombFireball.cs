using Godot;

/// <summary>
/// The Bob-omb blast's fireball (pass 130b): TEffectExplosion (src/Enemy/effectObj.cpp 548) -- the 10_bomb.bmd sphere (r 200 units) at a random yaw
/// (TEffectModel::reset), playing its bck (the joint's scale 0.16 -> 1.0 by frame 20 -> 1.2 at 58), brk (K0 / C0 / C1, see BombFireball.gdshader) and btk
/// (the three texture matrices' Ty) together over the clip's 59 frames at the game's 60 fps anim rate, dead when the clip ends (TEffectModel::moveObject:
/// curAnmEndsNext -> LIVE_FLAG_DEAD). A child of the BombExplosion burst (SmsFxBurst restarts it and waits for it like an emitter). The curves are the
/// disc's keys sampled a frame (gen_fx_bomb.py: brk_dump.py / bck.py); the mesh is a Godot sphere of the bmd's radius and UV layout.
/// </summary>
public partial class SmsBombFireball : MeshInstance3D, ISmsFxBurstPart
{
    [Export] public Curve K0Rgb, K0A, C0R, C0G, C0B, C1R, C1G, C1B, ScaleCurve;
    /// <summary>The clip's length in bck frames (10_bomb.bck: 59) and the frame rate (SMSGetAnmFrameRate: 60 a second).</summary>
    [Export] public float Frames = 59f;
    [Export] public float FrameRate = 60f;

    public event System.Action Finished;
    private float _frame = -1f;
    private ShaderMaterial _mat;
    private static readonly System.Random Rng = new();

    public float FrameNow => _frame;
    public bool Playing => _frame >= 0f;

    public override void _Ready()
    {
        _mat = (Mesh?.SurfaceGetMaterial(0) as ShaderMaterial) ?? (GetActiveMaterial(0) as ShaderMaterial);
        if (_mat != null) { _mat = (ShaderMaterial)_mat.Duplicate(); SetSurfaceOverrideMaterial(0, _mat); }   // per blast: two blasts at once keep their own frames
        if (!Playing) Visible = false;
    }

    /// <summary>generate(): reset -- the random yaw, frame 0 of the three anims, visible.</summary>
    public void Restart()
    {
        _frame = 0f;
        Rotation = new Vector3(0f, (float)(Rng.NextDouble() * Mathf.Tau), 0f);
        Visible = true;
        Apply();
    }

    public override void _Process(double delta)
    {
        if (!Playing) return;
        _frame += (float)delta * FrameRate;
        if (_frame + 1f >= Frames)                           // curAnmEndsNext
        {
            _frame = -1f;
            Visible = false;
            Finished?.Invoke();
            return;
        }
        Apply();
    }

    private static float At(Curve c, float t, float fallback) => c != null ? c.Sample(t) : fallback;

    private void Apply()
    {
        float t = Mathf.Clamp(_frame / 60f, 0f, 1f);         // the curves are sampled over frames 0..60
        float s = At(ScaleCurve, t, 1f);
        Scale = Vector3.One * Mathf.Max(s, 0.001f);
        if (_mat == null) return;
        float k = At(K0Rgb, t, 0f);
        _mat.SetShaderParameter("k0", new Vector4(k, k, k, At(K0A, t, 0f)));
        _mat.SetShaderParameter("c0", new Vector3(At(C0R, t, 1f), At(C0G, t, 1f), At(C0B, t, 1f)));
        _mat.SetShaderParameter("c1", new Vector3(At(C1R, t, 1f), At(C1G, t, 1f), At(C1B, t, 1f)));
        _mat.SetShaderParameter("scroll0", _frame / 60f);    // btk: Ty 0 -> 1 over 60 frames
        _mat.SetShaderParameter("scroll1", _frame / 60f);
        _mat.SetShaderParameter("scroll2", _frame / 240f);   // 0 -> 0.25
    }
}
