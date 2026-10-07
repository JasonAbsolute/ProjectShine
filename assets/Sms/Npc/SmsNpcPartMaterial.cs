using Godot;

/// <summary>One material of an NPC part (pass 84): the baked ShaderMaterial (its TEV form and textures from the bmd) and the game's colour
/// table for it (NpcInitData.cpp's TColorChangeInfo): <see cref="ColorKind"/> 0 = GX_COLOR0, the lit channel's material colour
/// (<c>mat_color</c>); 1 = GX_TEVREG0 (<c>c0</c>); 2 = GX_TEVREG1 / REG2 (<c>c1</c> from <see cref="Table0"/>, <c>c2</c> from
/// <see cref="Table1"/>); -1 = no colour change. The NPC's part colour index (one of three, by the part's channel) picks the row.
/// Values are GX S10 / 255 and may exceed 1, as the game's do.</summary>
[Tool, GlobalClass]   // [Tool] (pass 91): see SmsNpcProfile
public partial class SmsNpcPartMaterial : Resource
{
    [Export] public string MaterialName = "";
    [Export] public ShaderMaterial Material;
    [Export] public int ColorKind = -1;
    [Export] public Vector3[] Table0 = System.Array.Empty<Vector3>();
    [Export] public Vector3[] Table1 = System.Array.Empty<Vector3>();

    /// <summary>SMS_InitChangeNpcColor for this material: a copy of the baked material with the table row applied.</summary>
    public ShaderMaterial Instance(int colorIndex)
    {
        if (Material == null) return null;
        var m = (ShaderMaterial)Material.Duplicate();
        switch (ColorKind)
        {
            case 0: if (Table0.Length > 0) m.SetShaderParameter("mat_color", Table0[Mod(colorIndex, Table0.Length)]); break;
            case 1: if (Table0.Length > 0) m.SetShaderParameter("c0", Table0[Mod(colorIndex, Table0.Length)]); break;
            case 2:
                if (Table0.Length > 0) m.SetShaderParameter("c1", Table0[Mod(colorIndex, Table0.Length)]);
                if (Table1.Length > 0) m.SetShaderParameter("c2", Table1[Mod(colorIndex, Table1.Length)]);
                break;
        }
        return m;
    }

    /// <summary>The longest table: the range a random colour index is drawn from.</summary>
    public int Rows => Mathf.Max(Table0.Length, Table1.Length);

    private static int Mod(int i, int n) => n <= 0 ? 0 : ((i % n) + n) % n;
}
