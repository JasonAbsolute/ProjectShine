using System.Collections.Generic;
using Godot;

public partial class SkyboxRotator : Node3D
{
    [Export]
    public float StarRotationSpeed = 1.5f;

    [Export]
    public float StarScrollSpeed = 0.02f;

    [Export]
    public float TrackScrollSpeed = 0.015f;

    private MeshInstance3D _starMesh;

    // Black-background keyed layers
    private static readonly HashSet<string> _needsRailShader = new()
    {
        "n1b0",
        "n2b1",
        "n3b2",
        "n4b3", // rails
        "n8b5",
        "n6b7", // cloud
        "n7b6", // plane
        // n5b4 (train) uses its own shader
    };

    // Per-mesh look tuning (tint / opacity / boost / priority)
    private readonly Dictionary<string, SkyLayerPreset> _presets = new()
    {
        ["n1b0"] = new SkyLayerPreset(new Color(1.10f, 1.05f, 0.80f), 0.78f, 1.45f, 0), // yellow rail
        ["n2b1"] = new SkyLayerPreset(new Color(0.85f, 1.15f, 0.85f), 0.78f, 1.40f, 1), // green rail
        ["n3b2"] = new SkyLayerPreset(new Color(0.80f, 0.95f, 1.30f), 0.80f, 1.55f, 2), // blue rail
        ["n4b3"] = new SkyLayerPreset(new Color(1.10f, 0.75f, 1.15f), 0.70f, 1.35f, 3), // purple rail
        ["n5b4"] = new SkyLayerPreset(Colors.White, 0.75f, 1.20f, 4), // train
        ["n6b7"] = new SkyLayerPreset(Colors.White, 0.65f, 1.15f, 5), // cloud
        ["n7b6"] = new SkyLayerPreset(Colors.White, 0.70f, 1.20f, 6), // plane
        ["n8b5"] = new SkyLayerPreset(new Color(0.95f, 0.95f, 0.95f), 0.70f, 1.05f, 7), // axe/sign
    };

    // Per-mesh UV scroll velocity overrides
    private readonly Dictionary<string, Vector2> _scrollVelocityOverrides = new()
    {
        // Rails
        ["n1b0"] = new Vector2(-0.015f, 0.000f),
        ["n2b1"] = new Vector2(-0.015f, 0.000f),
        ["n3b2"] = new Vector2(+0.015f, 0.000f),
        ["n4b3"] = new Vector2(-0.015f, 0.000f),

        // Other layers
        ["n5b4"] = new Vector2(+0.015f, 0.000f), // train (corrected earlier)
        ["n6b7"] = new Vector2(0.000f, 0.000f), // cloud should NOT scroll
        ["n7b6"] = new Vector2(+0.012f, 0.000f), // plane flipped (was backwards)
        ["n8b5"] = new Vector2(0.000f, 0.000f), // axe/sign static
    };

    public override void _Ready()
    {
        var railShader = GD.Load<Shader>("res://assets/test_from_website.gdshader");
        var starShader = GD.Load<Shader>("res://assets/SMSStarSky.gdshader");
        var whiteKeyShader = GD.Load<Shader>("res://assets/SMSWhiteKeyLayer.gdshader");
        var trainShader = GD.Load<Shader>("res://assets/SMSTrain.gdshader");

        if (railShader == null)
        {
            GD.PrintErr("Failed to load rail shader: res://assets/test_from_website.gdshader");
            return;
        }
        if (starShader == null)
        {
            GD.PrintErr("Failed to load star shader: res://assets/SMSStarSky.gdshader");
            return;
        }
        if (whiteKeyShader == null)
        {
            GD.PrintErr("Failed to load white-key shader: res://assets/SMSWhiteKeyLayer.gdshader");
            return;
        }
        if (trainShader == null)
        {
            GD.PrintErr("Failed to load train shader: res://assets/SMSTrain.gdshader");
            return;
        }

        foreach (var child in GetChildren())
        {
            if (child is not MeshInstance3D mesh)
                continue;

            if (mesh.Mesh == null)
            {
                GD.PrintErr($"[{mesh.Name}] has no mesh resource.");
                continue;
            }

            bool isStarSphere = mesh.Name == "n0b8";
            if (isStarSphere)
                _starMesh = mesh;

            Vector2 scrollDir = isStarSphere
                ? new Vector2(0f, StarScrollSpeed)
                : new Vector2(-TrackScrollSpeed, 0f);

            if (
                !isStarSphere
                && _scrollVelocityOverrides.TryGetValue(mesh.Name, out var customScroll)
            )
                scrollDir = customScroll;

            bool useRailShader = _needsRailShader.Contains(mesh.Name);

            for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
            {
                Material baseMat = mesh.GetSurfaceOverrideMaterial(s);
                if (baseMat == null)
                    baseMat = mesh.Mesh.SurfaceGetMaterial(s);

                if (baseMat == null)
                {
                    GD.PrintErr($"[{mesh.Name}] surface {s}: no material found.");
                    continue;
                }

                Texture2D tex = TryExtractTexture(baseMat);

                GD.Print(
                    $"[{mesh.Name}] surface {s} baseMat={baseMat.GetType().Name}, tex={(tex != null ? tex.ResourcePath : "NULL")}, scroll={scrollDir}"
                );

                // STAR SPHERE
                if (isStarSphere)
                {
                    if (tex == null)
                    {
                        GD.PrintErr($"[{mesh.Name}] surface {s}: star sphere texture missing.");
                        continue;
                    }

                    var shaderMat = new ShaderMaterial();
                    shaderMat.Shader = starShader;
                    shaderMat.SetShaderParameter("albedo_tex", tex);
                    shaderMat.SetShaderParameter("scroll_speed", scrollDir);
                    shaderMat.SetShaderParameter("tint_color", new Color(1f, 1f, 1f, 1f));
                    shaderMat.SetShaderParameter("opacity", 1.0f);
                    shaderMat.SetShaderParameter("color_boost", 1.05f);

                    mesh.SetSurfaceOverrideMaterial(s, shaderMat);
                    continue;
                }

                // TRAIN — dedicated shader with rainbow + bottom-pinned bop
                if (mesh.Name == "n5b4")
                {
                    if (tex == null)
                    {
                        GD.PrintErr(
                            $"[{mesh.Name}] surface {s}: train shader requested but texture missing."
                        );
                        continue;
                    }

                    var shaderMat = new ShaderMaterial();
                    shaderMat.Shader = trainShader;
                    shaderMat.SetShaderParameter("albedo_tex", tex);
                    shaderMat.SetShaderParameter("scroll_speed", scrollDir);
                    shaderMat.SetShaderParameter("opacity", 0.30f);
                    shaderMat.SetShaderParameter("min_visible_alpha", 0.15f);
                    shaderMat.SetShaderParameter("color_boost", 0.8f);
                    shaderMat.SetShaderParameter("gamma_lift", 0.85f);
                    shaderMat.SetShaderParameter("hue_speed", 0.06f);

                    mesh.SetSurfaceOverrideMaterial(s, shaderMat);
                    continue;
                }

                // BLACK-KEY LAYERS (rails/cloud/plane)
                if (useRailShader)
                {
                    if (tex == null)
                    {
                        GD.PrintErr(
                            $"[{mesh.Name}] surface {s}: rail shader requested but texture missing."
                        );
                        continue;
                    }

                    var shaderMat = new ShaderMaterial();
                    shaderMat.Shader = railShader;
                    shaderMat.SetShaderParameter("albedo_tex", tex);
                    shaderMat.SetShaderParameter("scroll_speed", scrollDir);

                    // Defaults (match your current shader)
                    shaderMat.SetShaderParameter("tint_color", new Color(1f, 1f, 1f, 1f));
                    shaderMat.SetShaderParameter("opacity", 0.75f);
                    shaderMat.SetShaderParameter("min_visible_alpha", 0.30f);
                    shaderMat.SetShaderParameter("color_boost", 1.6f);
                    shaderMat.SetShaderParameter("gamma_lift", 0.85f);

                    // Per-mesh preset overrides
                    if (_presets.TryGetValue(mesh.Name, out var preset))
                    {
                        shaderMat.SetShaderParameter("tint_color", preset.Tint);
                        shaderMat.SetShaderParameter("opacity", preset.Opacity);
                        shaderMat.SetShaderParameter("color_boost", preset.ColorBoost);
                    }

                    mesh.SetSurfaceOverrideMaterial(s, shaderMat);
                    continue;
                }

                // For any other meshes: keep as-is, but duplicate materials if needed
                if (baseMat is StandardMaterial3D stdMat)
                {
                    var uniqueMat = (StandardMaterial3D)stdMat.Duplicate();
                    mesh.SetSurfaceOverrideMaterial(s, uniqueMat);
                }
                else if (baseMat is ShaderMaterial sm)
                {
                    var dup = (ShaderMaterial)sm.Duplicate();
                    mesh.SetSurfaceOverrideMaterial(s, dup);
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_starMesh != null)
            _starMesh.RotateY(Mathf.DegToRad(StarRotationSpeed) * dt);
    }

    private Texture2D TryExtractTexture(Material mat)
    {
        if (mat is StandardMaterial3D std)
            return std.AlbedoTexture;

        if (mat is ShaderMaterial sm)
        {
            string[] possibleParams =
            {
                "albedo_tex",
                "texture_albedo",
                "albedo_texture",
                "main_tex",
            };

            foreach (string param in possibleParams)
            {
                Variant v;
                try
                {
                    v = sm.GetShaderParameter(param);
                }
                catch
                {
                    continue;
                }

                if (v.VariantType == Variant.Type.Object)
                {
                    var obj = v.AsGodotObject();
                    if (obj is Texture2D t)
                        return t;
                }
            }
        }

        return null;
    }

    private readonly struct SkyLayerPreset
    {
        public readonly Color Tint;
        public readonly float Opacity;
        public readonly float ColorBoost;
        public readonly int RenderPriority;

        public SkyLayerPreset(Color tint, float opacity, float colorBoost, int renderPriority)
        {
            Tint = tint;
            Opacity = opacity;
            ColorBoost = colorBoost;
            RenderPriority = renderPriority;
        }
    }
}
