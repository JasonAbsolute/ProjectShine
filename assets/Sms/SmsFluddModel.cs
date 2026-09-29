using Godot;
using System.Collections.Generic;

/// <summary>
/// The FLUDD's model assembly, shared by Mario.cs (the pack at run time, SmsFluddSetup) and SmsFluddAttach (the editor's preview
/// of the same pack in Player.tscn): the tank (watergun2/body/wg_mdl1) under the jnt_chest attachment, the four nozzles (normal /
/// hover / rocket / back) on the tank's nozzle_center joint, and the game's MAT3 material fixes -- the translucent vertex-coloured
/// shell, the additive waterline, the nozzles' 8x8 H_watergun_main_dummy placeholder swapped for the tank frame's atlas. One place
/// builds it so the editor shows exactly what the game attaches.
/// </summary>
public static class SmsFluddModel
{
    public const string Dir = "res://models/Koopa/mario/watergun2/";
    public static readonly string[] NozzleFolder = { "normal_wg", "hover_wg", "rocket_wg", "back_wg" };
    /// <summary>The nozzles' bind position on the tank, for a rip without the nozzle_center joint.</summary>
    public static readonly Vector3 NozzleBindPosition = new Vector3(72.01f, -39.99f, 0f);

    /// <summary>What Build hung on the attachment.</summary>
    public sealed class Parts
    {
        public Node3D Tank;
        public Skeleton3D TankSkel;
        public AnimationPlayer TankAnim;
        public Node3D NozzleParent;                                        // a BoneAttachment3D on nozzle_center, or the bind-position node
        public readonly Node3D[] NozzleNode = new Node3D[4];
        public readonly AnimationPlayer[] NozzleAnim = new AnimationPlayer[4];
        public readonly Skeleton3D[] NozzleSkel = new Skeleton3D[4];       // the muzzle joints (null_G_muzzle) live here
        public Material Atlas;                                             // the tank frame's material: H_watergun_main (wg_mdl1_0.png)
    }

    /// <summary>The tank's base orientation on the bone; the lean is the pack's tilt (SmsFluddTilt).</summary>
    public static Basis BaseBasis(float lean) => Basis.FromEuler(new Vector3(0f, 0f, lean));

    public static PackedScene LoadScene(string basePath)
    {
        foreach (string ext in new[] { ".glb", ".fbx" })
        {
            string p = basePath + ext;
            if (ResourceLoader.Exists(p))
                return ResourceLoader.Load<PackedScene>(p);
        }
        return null;
    }

    public static T FindChild<T>(Node root) where T : Node
    {
        if (root is T t)
            return t;
        foreach (Node c in root.GetChildren())
        {
            var r = FindChild<T>(c);
            if (r != null)
                return r;
        }
        return null;
    }

    public static void Collect<T>(Node root, List<T> into) where T : Node
    {
        if (root is T t)
            into.Add(t);
        foreach (Node c in root.GetChildren())
            Collect(c, into);
    }

    /// <summary>The tank and the four nozzles under `attach` (Mario's jnt_chest attachment, or the editor preview's node), only the
    /// nozzle `visibleNozzle` (SmsNozzle: 0 spray, 1 hover, 2 rocket, 3 turbo) shown. Null when the tank model is missing.</summary>
    public static Parts Build(Node3D attach, int visibleNozzle)
    {
        var tankScene = LoadScene(Dir + "body/wg_mdl1/wg_mdl1");
        if (tankScene == null)
        {
            GD.PushWarning("[SMS fludd] wg_mdl1 (the tank) not found under " + Dir);
            return null;
        }
        var p = new Parts();
        p.Tank = tankScene.Instantiate<Node3D>();
        attach.AddChild(p.Tank);
        p.Tank.Transform = new Transform3D(BaseBasis(0f), Vector3.Zero);
        p.Atlas = TankMaterials(p.Tank);
        p.TankSkel = FindChild<Skeleton3D>(p.Tank);
        p.TankAnim = FindChild<AnimationPlayer>(p.Tank);
        if (p.TankSkel != null && p.TankSkel.FindBone("nozzle_center") >= 0)
        {
            var na = new BoneAttachment3D { Name = "SmsNozzleAttach", BoneName = "nozzle_center" };
            p.TankSkel.AddChild(na);
            p.NozzleParent = na;
        }
        else
        {
            GD.PushWarning("[SMS fludd] the tank has no nozzle_center bone; the nozzles sit at its bind position");
            var n = new Node3D { Name = "SmsNozzleAttach", Position = NozzleBindPosition };
            p.Tank.AddChild(n);
            p.NozzleParent = n;
        }
        for (int i = 0; i < 4; i++)
        {
            string f = NozzleFolder[i];
            var sc = LoadScene($"{Dir}{f}/{f}/{f}");
            if (sc == null)
            {
                GD.PushWarning($"[SMS fludd] nozzle model {f} not found");
                continue;
            }
            var node = sc.Instantiate<Node3D>();
            p.NozzleParent.AddChild(node);
            p.NozzleNode[i] = node;
            p.NozzleAnim[i] = FindChild<AnimationPlayer>(node);
            node.Visible = i == visibleNozzle;
            NozzleMaterial(node, p.Atlas); // the rip kept the nozzles' H_watergun_main_dummy placeholder: give them the FLUDD atlas
            p.NozzleSkel[i] = FindChild<Skeleton3D>(node);
        }
        return p;
    }

    /// <summary>The tank's materials as the game's MAT3 has them: the shell (_mat_G_main_tank) is translucent, vertex-coloured,
    /// blended (ONE, INVSRCALPHA) without a depth write; the waterline is additive; the frame keeps its atlas. Returns the frame's
    /// atlas material (H_watergun_main_s3tc): what the nozzles' dummy texture stands in for.</summary>
    public static Material TankMaterials(Node3D tank)
    {
        Material atlas = null;
        var meshes = new List<MeshInstance3D>();
        Collect(tank, meshes);
        foreach (var mi in meshes)
        {
            if (mi.Mesh == null)
                continue;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var src = mi.GetActiveMaterial(i);
                string name = src?.ResourceName ?? "";
                if (name.Length == 0 && mi.Mesh is ArrayMesh am)
                    name = am.SurfaceGetName(i);
                if (name == "_mat_G_main_tank")
                {
                    mi.SetSurfaceOverrideMaterial(i, new StandardMaterial3D
                    {
                        ResourceName = "_mat_G_main_tank (sms)",
                        VertexColorUseAsAlbedo = true,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        BlendMode = BaseMaterial3D.BlendModeEnum.PremultAlpha,
                        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
                        CullMode = BaseMaterial3D.CullModeEnum.Back,
                        Roughness = 0.35f,
                        Metallic = 0f,
                    });
                }
                else if (name == "_mat_waterline" && src is StandardMaterial3D wl)
                {
                    var m = (StandardMaterial3D)wl.Duplicate();
                    m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
                    m.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
                    m.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    m.AlbedoColor = new Color(136f / 255f, 129f / 255f, 156f / 255f); // its material colour
                    mi.SetSurfaceOverrideMaterial(i, m);
                }
                else if (atlas == null && src is StandardMaterial3D sm && sm.AlbedoTexture != null)
                    atlas = sm;
            }
        }
        if (atlas == null)
        {
            var tex = ResourceLoader.Load<Texture2D>(Dir + "body/wg_mdl1/wg_mdl1_0.png");
            if (tex != null)
                atlas = new StandardMaterial3D { ResourceName = "H_watergun_main (sms)", AlbedoTexture = tex, Roughness = 1f, Metallic = 0f };
        }
        return atlas;
    }

    /// <summary>The nozzle BMDs' texture 0 is H_watergun_main_dummy, an 8x8 placeholder the game swaps for the FLUDD atlas; the
    /// nozzles' UVs span that atlas, so every surface gets the tank frame's material.</summary>
    public static void NozzleMaterial(Node root, Material atlas)
    {
        if (atlas == null)
            return;
        var meshes = new List<MeshInstance3D>();
        Collect(root, meshes);
        foreach (var mi in meshes)
            for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++)
                mi.SetSurfaceOverrideMaterial(i, atlas);
    }

    /// <summary>A clip held at a fraction of its length (the tank's wg_house by the stow progress, and so on).</summary>
    public static void Scrub(AnimationPlayer ap, string name, float t)
    {
        if (ap == null || !ap.HasAnimation(name))
            return;
        if (ap.AssignedAnimation != name)
            ap.Play(name);
        ap.Seek(Mathf.Clamp(t, 0f, 1f) * ap.GetAnimation(name).Length, true);
        ap.Pause();
    }
}
