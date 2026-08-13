using Godot;

/// <summary>
/// [TOOL] Place on any node in the PalmTree scene.
/// Set SkeletonRootPath (the "skeleton_root" node, NOT Skeleton3D).
/// Set LeafBodyPath (the StaticBody3D that holds the collision shapes).
///
/// Matching: rename each CollisionShape3D under LeafBody to match its source mesh
/// (e.g. name it "mesh-1", "mesh-8", etc). Unmatched shapes fall back to index order.
///
/// Toggle FixCollisionsNow to apply.
/// </summary>
[Tool]
public partial class LeafCollisionFixer : Node3D
{
    /// <summary>The "skeleton_root" Node3D (parent of Skeleton3D). NOT the Skeleton3D itself.</summary>
    [Export] public NodePath SkeletonRootPath { get; set; }

    /// <summary>The LeafBody StaticBody3D that contains all the leaf CollisionShape3Ds.</summary>
    [Export] public NodePath LeafBodyPath { get; set; }

    private bool _fix;

    /// <summary>Toggle to true in the inspector to run the fix.</summary>
    [Export]
    public bool FixCollisionsNow
    {
        get => _fix;
        set
        {
            _fix = false;
            if (value && Engine.IsEditorHint())
                DoFix();
        }
    }

    private void DoFix()
    {
        var skeletonRoot = GetNodeOrNull<Node3D>(SkeletonRootPath);
        var leafBody     = GetNodeOrNull<StaticBody3D>(LeafBodyPath);

        if (skeletonRoot == null) { GD.PrintErr("[LeafCollisionFixer] SkeletonRootPath not found."); return; }
        if (leafBody     == null) { GD.PrintErr("[LeafCollisionFixer] LeafBodyPath not found.");     return; }

        var skeleton = skeletonRoot.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        if (skeleton == null) { GD.PrintErr("[LeafCollisionFixer] No Skeleton3D child found under skeleton_root."); return; }

        // Read bind scale from first skinned mesh (all bones share the same uniform scale).
        float bindScale = 1f;
        foreach (Node child in skeleton.GetChildren())
        {
            if (child is MeshInstance3D mi && mi.Skin != null && mi.Skin.GetBindCount() > 0)
            {
                bindScale = mi.Skin.GetBindPose(0).Basis.Scale.X;
                GD.Print($"[LeafCollisionFixer] Bind scale = {bindScale}");
                break;
            }
        }

        // Collect leaf meshes (skip mesh-0 = trunk).
        var leafMeshes = new System.Collections.Generic.List<MeshInstance3D>();
        foreach (Node child in skeleton.GetChildren())
        {
            if (child is MeshInstance3D mi && mi.Name != "mesh-0")
                leafMeshes.Add(mi);
        }

        // Collect collision shapes under LeafBody.
        var shapes = new System.Collections.Generic.List<CollisionShape3D>();
        foreach (Node child in leafBody.GetChildren())
        {
            if (child is CollisionShape3D cs)
                shapes.Add(cs);
        }

        // Correct formula:
        //   cs.Transform = leafBody.GlobalTransform.Inverse()
        //                * skeletonRoot.GlobalTransform
        //                * mesh.Transform          (mesh's local transform within Skeleton3D)
        //                * (Identity scaled by bindScale)
        //
        // This places the trimesh vertices (which are in the mesh's OWN local space)
        // at the correct world position/rotation/scale, matching where the skinned leaf
        // actually appears in the viewport.

        int fixed_count = 0;
        foreach (var cs in shapes)
        {
            // Try name-based match first (name the shape "mesh-1", "mesh-8", etc.)
            MeshInstance3D matched = null;
            foreach (var m in leafMeshes)
            {
                if (cs.Name.ToString() == m.Name.ToString())
                {
                    matched = m;
                    break;
                }
            }

            // Fall back to index order.
            if (matched == null)
            {
                int idx = shapes.IndexOf(cs);
                if (idx < leafMeshes.Count)
                    matched = leafMeshes[idx];
            }

            if (matched == null)
            {
                GD.PrintErr($"[LeafCollisionFixer] No mesh found for {cs.Name}, skipping.");
                continue;
            }

            // Build the bind-pose-scale transform (uniform +scale on all axes, no rotation/translation).
            // Note: Vector3.Back is (0,0,-1) in Godot — using it here would flip Z. Use Identity.Scaled instead.
            var bindTransform = Transform3D.Identity.Scaled(Vector3.One * bindScale);

            cs.Transform = leafBody.GlobalTransform.Inverse()
                         * skeletonRoot.GlobalTransform
                         * matched.Transform
                         * bindTransform;

            GD.Print($"[LeafCollisionFixer] Fixed '{cs.Name}' using '{matched.Name}'");
            fixed_count++;
        }

        GD.Print($"[LeafCollisionFixer] Done — fixed {fixed_count} shape(s).");
    }
}
