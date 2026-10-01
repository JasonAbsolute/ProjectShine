using Godot;
using System.Collections.Generic;

/// <summary>
/// Helpers for the FLUDD scene (assets/Sms/Fludd.tscn, SmsFludd, instanced under Player.tscn's SmsFluddAttach -- Mario.cs reads that;
/// pass 37 removed the code-built stand-in and its MAT3 material fixes -- the scene's .tres materials do that since pass 19): the
/// nozzle rips' 8x8 H_watergun_main_dummy placeholder swapped for the pack's atlas material (NozzleMaterial; the nozzle items wear it
/// too through Mario.SmsApplyFluddAtlas) and the node helpers (FindChild, Collect, Scrub) the SMS scripts share.
/// </summary>
public static class SmsFluddModel
{
    /// <summary>The tank's base orientation on the bone; the lean is the pack's tilt (SmsFluddTilt).</summary>
    public static Basis BaseBasis(float lean) => Basis.FromEuler(new Vector3(0f, 0f, lean));

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
