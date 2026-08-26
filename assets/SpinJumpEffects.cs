using Godot;

public partial class SpinJumpEffects : Node3D
{
    [Export]
    public float MinY = 0.3f;

    [Export]
    public float MaxY = 1.0f;

    [Export]
    public float SpawnInterval = 0.05f;

    [Export]
    public float RingLifetime = 0.18f;

    [Export]
    public Vector3 LocalOffset = new(0f, 0f, 0f);

    [Export]
    public Texture2D PuffTex;

    // Offsets so rings aren't stacked perfectly. Named by ROLE like the
    // colours above - the child nodes are still white/blue/red, but those
    // are position labels, not the hue any given character uses.
    [Export]
    public float UpperYOffset = 0.10f;

    [Export]
    public float LowerYOffset = -0.06f;

    // Small scale differences so they feel layered
    [Export]
    public float UpperScaleMul = 1.03f;

    [Export]
    public float LowerScaleMul = 0.98f;

    // How much to randomize each ring per spawn
    [Export]
    public float YJitter = 0.08f;

    [Export]
    public float ScaleJitter = 0.12f;

    [Export]
    public float ZTiltMaxDeg = 12f;

    // --- Ring colours ------------------------------------------------------
    // Named by ROLE rather than by hue, because the hue is exactly what varies
    // per character: Mario's lower ring is red, Luigi's is green. The child
    // nodes are still called white/blue/red (Mario's original palette) so
    // existing scenes keep resolving - treat those as position labels.

    [ExportGroup("Ring Colors")]
    /// <summary>Tint for the middle/main ring - the "white" child node.</summary>
    [Export]
    public Color MainRingColor = Colors.White;

    /// <summary>Tint for the upper, slightly larger ring - the "blue" child node.</summary>
    [Export]
    public Color UpperRingColor = new(0.35f, 0.70f, 1.00f, 1f);

    /// <summary>Tint for the lower, slightly smaller ring - the "red" child node.</summary>
    [Export]
    public Color LowerRingColor = new(1.00f, 0.25f, 0.25f, 1f);
    [ExportGroup("")]

    private readonly RandomNumberGenerator _rng = new();

    private MeshInstance3D _whiteMesh,
        _blueMesh,
        _redMesh;
    private ShaderMaterial _whiteMat,
        _blueMat,
        _redMat;

    private float _spawnTimer;
    private float _timeLeft;
    private bool _active;

    // Set this true while debugging to keep rings on-screen
    private const bool DEBUG_HOLD = false;

    public override void _Ready()
    {
        _rng.Randomize();
        Position = LocalOffset;

        _whiteMesh = GetNodeOrNull<MeshInstance3D>("white");
        _blueMesh = GetNodeOrNull<MeshInstance3D>("blue");
        _redMesh = GetNodeOrNull<MeshInstance3D>("red");

        if (_whiteMesh == null || _blueMesh == null || _redMesh == null)
        {
            GD.PushWarning(
                "SpinJumpEffects: Expected MeshInstance3D children named: white, blue, red"
            );
            return;
        }

        _whiteMat = MakeUniqueShaderMat(_whiteMesh);
        _blueMat = MakeUniqueShaderMat(_blueMesh);
        _redMat = MakeUniqueShaderMat(_redMesh);

        if (_whiteMat == null || _blueMat == null || _redMat == null)
        {
            GD.PushWarning("SpinJumpEffects: One or more rings has no ShaderMaterial.");
            return;
        }

        // Apply shared puff texture (optional but recommended)
        if (PuffTex != null)
        {
            _whiteMat.SetShaderParameter("puff_tex", PuffTex);
            _blueMat.SetShaderParameter("puff_tex", PuffTex);
            _redMat.SetShaderParameter("puff_tex", PuffTex);
        }
        else
        {
            GD.PushWarning("SpinJumpEffects: PuffTex is not assigned in Inspector.");
        }

        // Tint per ring, driven by the exported Ring Colors so each character
        // scene can recolour the trail without touching code or the shader.
        // MakeUniqueShaderMat already duplicated these materials, so setting
        // them here is per-instance and cannot leak across characters.
        _whiteMat.SetShaderParameter("tint", MainRingColor);
        _blueMat.SetShaderParameter("tint", UpperRingColor);
        _redMat.SetShaderParameter("tint", LowerRingColor);

        SetVisibleAll(false);
    }

    private static ShaderMaterial MakeUniqueShaderMat(MeshInstance3D mi)
    {
        Material baseMat = mi.MaterialOverride;
        if (baseMat == null && mi.Mesh != null)
            baseMat = mi.Mesh.SurfaceGetMaterial(0);

        if (baseMat is not ShaderMaterial shaderMat)
        {
            GD.PushWarning($"SpinJumpEffects: {mi.Name} has no ShaderMaterial.");
            return null;
        }

        var unique = (ShaderMaterial)shaderMat.Duplicate();
        mi.MaterialOverride = unique;
        return unique;
    }

    public void SetSpinActive(bool on)
    {
        _active = on;
        _spawnTimer = 0f;

        if (!on)
        {
            SetVisibleAll(false);
            _timeLeft = 0f;
        }
        else
        {
            SpawnTripleRings();
        }
    }

    public override void _Process(double delta)
    {
        Position = LocalOffset;

        if (_whiteMat == null || _blueMat == null || _redMat == null)
            return;

        float dt = (float)delta;

        // Lifetime countdown
        if (_timeLeft > 0f)
        {
            _timeLeft -= dt;
            if (_timeLeft <= 0f)
                SetVisibleAll(false);
        }

        if (!_active)
            return;

        _spawnTimer -= dt;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = SpawnInterval;
            SpawnTripleRings();
        }
    }

    private void SpawnTripleRings()
    {
        // Base spawn height + base scale (shared "feel")
        float baseY = _rng.RandfRange(MinY, MaxY);
        float baseS = _rng.RandfRange(0.95f, 1.55f);

        // Random tilt so rings aren’t perfectly identical
        float zTilt = 0;

        // --- WHITE (middle/main) ---
        ApplyToRing(
            _whiteMesh,
            _whiteMat,
            y: baseY + _rng.RandfRange(-YJitter, YJitter),
            s: baseS + _rng.RandfRange(-ScaleJitter, ScaleJitter),
            zTilt: zTilt,
            alpha: 1f,
            radius: _rng.RandfRange(0.5f, 0.6f),
            thickness: _rng.RandfRange(0.08f, 0.11f),
            tileU: _rng.RandfRange(1f, 2f),
            speed: 5f
        );

        // --- BLUE (slightly higher, slightly bigger, slightly outside) ---
        ApplyToRing(
            _blueMesh,
            _blueMat,
            y: baseY + UpperYOffset + _rng.RandfRange(-YJitter, YJitter),
            s: (baseS * UpperScaleMul) + _rng.RandfRange(-ScaleJitter, ScaleJitter),
            zTilt: zTilt,
            alpha: 1f,
            radius: _rng.RandfRange(0.5f, 0.6f),
            thickness: _rng.RandfRange(0.08f, 0.11f),
            tileU: _rng.RandfRange(1f, 2f),
            speed: 5f
        );

        // --- RED (slightly lower, slightly smaller, slightly inside) ---
        ApplyToRing(
            _redMesh,
            _redMat,
            y: baseY + LowerYOffset + _rng.RandfRange(-YJitter, YJitter),
            s: (baseS * LowerScaleMul) + _rng.RandfRange(-ScaleJitter, ScaleJitter),
            zTilt: zTilt,
            alpha: 1f,
            radius: _rng.RandfRange(0.70f, 0.77f),
            thickness: _rng.RandfRange(0.08f, 0.11f),
            tileU: _rng.RandfRange(1f, 2f),
            speed: 5f
        );

        SetVisibleAll(true);
        _timeLeft = DEBUG_HOLD ? 9999f : RingLifetime;
    }

    private void ApplyToRing(
        MeshInstance3D mesh,
        ShaderMaterial mat,
        float y,
        float s,
        float zTilt,
        float alpha,
        float radius,
        float thickness,
        float tileU,
        float speed
    )
    {
        mesh.Position = new Vector3(0f, y, 0f);
        mesh.Scale = new Vector3(s, s, s);

        var rot = mesh.Rotation;
        rot.Z = zTilt; // keep X as set in editor (-90deg)
        mesh.Rotation = rot;

        mat.SetShaderParameter("alpha", alpha);
        mat.SetShaderParameter("ring_radius", radius);
        mat.SetShaderParameter("ring_thickness", thickness);
        mat.SetShaderParameter("ring_softness", 0.04f);
        mat.SetShaderParameter("tile_u", tileU);
        mat.SetShaderParameter("spin_speed", speed);
    }

    private void SetVisibleAll(bool v)
    {
        if (_whiteMesh != null)
            _whiteMesh.Visible = v;
        if (_blueMesh != null)
            _blueMesh.Visible = v;
        if (_redMesh != null)
            _redMesh.Visible = v;
    }
}
