using Godot;
using System.Collections.Generic;

/// <summary>An NPC part (pass 84): one of TNpcParts' twelve slots -- a hat, glasses, a moustache, a lapel, a tie, the luggage, the W's flower
/// or arrows, the Toad's sunglasses. Drop a part scene (assets/Sms/Npc/Parts/*) under an NPC and SmsNpc hangs it on <see cref="Joint"/>
/// (TSharedParts on the joint's matrix: the model is authored in the joint's space, so the part sits at the joint with no offset), and
/// colours it with the NPC's part colour index for its <see cref="ColorChannel"/> (the placement's three indices: 0 hats, 1 moustache /
/// belts / lapel, 2 glasses -- TNpcModelData's unk28). The per-material forms and tables are baked into the scene by gen_npc_parts.py
/// from the bmds and NpcInitData.cpp.</summary>
[GlobalClass]
public partial class SmsNpcPart : Node3D
{
    [Export] public string Joint = "kubi";
    [Export(PropertyHint.Range, "0,2")] public int ColorChannel = 0;
    /// <summary>The part's materials (TNpcModelData carries up to three colour changes): baked by gen_npc_parts.py.</summary>
    [Export] public SmsNpcPartMaterial Mat0, Mat1, Mat2;
    public const string RootJoint = "__ROOT_JOINT__";

    public int ColorIndexApplied { get; private set; } = -1;

    public IEnumerable<SmsNpcPartMaterial> Materials
    {
        get { if (Mat0 != null) yield return Mat0; if (Mat1 != null) yield return Mat1; if (Mat2 != null) yield return Mat2; }
    }

    /// <summary>The longest colour table among the materials: the range a random index is drawn from.</summary>
    public int ColorRows
    {
        get { int n = 0; foreach (var m in Materials) n = Mathf.Max(n, m.Rows); return n; }
    }

    /// <summary>SMS_InitChangeNpcColor over the part's materials: every MeshInstance3D surface whose import material is named like one of
    /// ours gets its baked ShaderMaterial with the table row, the toon ramp and the scene's sun.</summary>
    public void Apply(int colorIndex, Texture2D toon, DirectionalLight3D sun)
    {
        ColorIndexApplied = colorIndex;
        foreach (var mi in AllMeshes(this))
        {
            int n = mi.GetSurfaceOverrideMaterialCount();
            for (int s = 0; s < n; s++)
            {
                string name = mi.Mesh?.SurfaceGetMaterial(s)?.ResourceName ?? "";
                SmsNpcPartMaterial pm = null;
                foreach (var cand in Materials) if (cand.MaterialName == name) { pm = cand; break; }
                if (pm == null) continue;
                var m = pm.Instance(colorIndex);
                if (m == null) continue;
                if (toon != null) m.SetShaderParameter("toon_tex", toon);
                if (sun != null)
                {
                    m.SetShaderParameter("sun_dir", -sun.GlobalTransform.Basis.Z.Normalized());
                    m.SetShaderParameter("sun_color", new Vector3(sun.LightColor.R, sun.LightColor.G, sun.LightColor.B) * sun.LightEnergy);
                }
                mi.SetSurfaceOverrideMaterial(s, m);
            }
        }
    }

    public static IEnumerable<MeshInstance3D> AllMeshes(Node n)
    {
        if (n is MeshInstance3D mi) yield return mi;
        foreach (var c in n.GetChildren()) foreach (var r in AllMeshes(c)) yield return r;
    }
}
