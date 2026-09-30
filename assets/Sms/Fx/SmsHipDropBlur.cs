using System.Collections.Generic;
using Godot;

/// <summary>
/// The hip drop's fall streak: TMario::emitBlurHipDrop (MarioParticle.cpp 546), called every frame of the fall (MarioJump.cpp 1140) --
/// emitAndBindToMtxPtr(MS_M_BLUR3, getCenterAnmMtx(), 1, this), one emitter kept alive per owner while the calls come. The particle
/// (ms_m_blur3.jpa, tools jpa_dump.py) is a StripeCross: one point a frame at the emitter (the centre joint, 20 units down and 14.6
/// forward), each living 10 frames, the points threaded into two crossed ribbons 100 units wide (BSP1 base 2.0 x 50) -- the game's
/// speed lines above the plummeting Mario; P_msm_blur1 (lerp (39,63,255) -> (255,39,15) by its intensity, repeat, scaled 1.75 x 0.1
/// and scrolling 0.2 a 60 Hz frame along the stripe) shaped by the second texture P_msm_blurmsk1 (an indirect stage; here the stripe shader's
/// mask). The scene assets/Sms/Fx/HipDropBlur.tscn carries the Trail mesh instance with its ShaderMaterial (stripe_blur.gdshader, both
/// textures, the scale and the scroll); this script only threads the recorded points into that mesh each frame. The points are kept in
/// world space (the ribbon does not move with him -- the game's particles are not FollowEmitter). Mario.cs turns the legacy
/// GroundPoundEffects streaks off when this scene is present.
/// </summary>
public partial class SmsHipDropBlur : SmsFollowEffect
{
    /// <summary>The ribbon's MeshInstance3D; its material override is the stripe shader with the two disc textures.</summary>
    [Export] public MeshInstance3D Trail;

    /// <summary>BSP1 base size X times the quad's 50: the ribbon's width in game units.</summary>
    [Export] public float WidthUnits = 100f;

    /// <summary>BEM1 life: how many particle frames (60 Hz: the JPA manager is calc'd twice a rendered frame) a point stays in the ribbon.</summary>
    [Export] public int LifeFrames = 10;

    private readonly List<(Vector3 pos, float time)> _points = new();
    private ImmediateMesh _mesh;
    private bool _active;
    private float _clock;

    public bool Active
    {
        get => _active;
        set => _active = value;
    }

    public override void _Ready()
    {
        if (Trail == null)
        {
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Trail export is not set (see assets/Sms/Fx/HipDropBlur.tscn)");
            return;
        }
        _mesh = new ImmediateMesh();
        Trail.Mesh = _mesh;
        Trail.TopLevel = true; // the points are world positions
        Trail.GlobalTransform = Transform3D.Identity;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_mesh == null)
            return;
        _clock += (float)delta;
        float life = LifeFrames / 60f;
        if (_active)
            _points.Add((GlobalPosition, _clock));
        _points.RemoveAll(p => _clock - p.time > life);
        Rebuild(life);
    }

    private void Rebuild(float life)
    {
        _mesh.ClearSurfaces();
        if (_points.Count < 2)
            return;
        float half = 0.5f * WidthUnits * SmsUnits.UnitsToMetres(1f);
        Basis yaw = GlobalTransform.Basis.Orthonormalized();
        Vector3[] across = { yaw.X * half, yaw.Z * half }; // the cross: one ribbon on his right axis, one on his forward
        foreach (Vector3 side in across)
        {
            _mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int i = _points.Count - 1; i >= 0; i--) // head (newest) first
            {
                float v = Mathf.Clamp((_clock - _points[i].time) / life, 0f, 1f);
                _mesh.SurfaceSetColor(new Color(1f, 1f, 1f, 1f));
                _mesh.SurfaceSetUV(new Vector2(0f, v));
                _mesh.SurfaceAddVertex(_points[i].pos - side);
                _mesh.SurfaceSetUV(new Vector2(1f, v));
                _mesh.SurfaceAddVertex(_points[i].pos + side);
            }
            _mesh.SurfaceEnd();
        }
    }

    /// <summary>Debug: how many points the ribbon threads right now.</summary>
    public int DebugPointCount => _points.Count;
}
