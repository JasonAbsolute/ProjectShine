using System.Collections.Generic;
using Godot;

/// <summary>
/// Leaf bounce for trees exported as a SINGLE merged MeshInstance3D with one surface
/// per leaf, rather than one MeshInstance3D per leaf like PalmLeaf.cs expects (that's
/// how the banana tree's glb re-export came out). Builds per-leaf collision at
/// runtime from each surface's raw (bind-pose) vertex data, corrected for the skin's
/// bind-pose scale — the same gotcha LeafCollisionFixer.cs solves for the palm tree,
/// just applied at runtime here instead of as an editor tool — and finds each leaf's
/// bone via its dominant skin weight so a bounce can drive that one bone's pose.
///
/// Usage: attach to a Node3D child of the tree root (e.g. "Leaves"), with
/// <see cref="MeshInstancePath"/> pointing at the merged mesh instance. Mario.cs
/// finds this via the generic ILeafBouncer check on whatever StaticBody3D he lands
/// on that's in the "palm_leaf_body" group (the collision body built here).
/// </summary>
public partial class BananaLeafBounce : Node3D, ILeafBouncer
{
    [Export] public NodePath MeshInstancePath;
    [Export] public int TrunkSurfaceIndex = 0;
    [Export] public float BounceDepth = 0.35f;
    [Export] public float DropDuration = 0.12f;
    [Export] public float RiseDuration = 0.75f;

    private MeshInstance3D _meshInstance;
    private Skeleton3D _skeleton;
    private float _skeletonScale = 1f;

    // Each entry: (XZ angle from trunk center, bone index) — same approach as PalmLeaf.
    private readonly List<(float angle, int boneIdx)> _leafByAngle = new();
    private readonly Dictionary<int, int> _surfaceToBone = new();

    // The collision is a separate, static system from the skinned visual mesh — moving
    // the bone pose alone doesn't move it. Track each leaf's collision shape (and its
    // rest position) by bone so the active bounce can drag it down too, or Mario just
    // stands there while the mesh dips away beneath him.
    private readonly Dictionary<int, CollisionShape3D> _leafCollisionByBone = new();
    private readonly Dictionary<int, Vector3> _leafCollisionRestPos = new();

    private bool _bouncing;
    private float _bounceTimer;
    private int _activeBoneIdx = -1;

    public override void _Ready()
    {
        _meshInstance = GetNodeOrNull<MeshInstance3D>(MeshInstancePath);
        if (_meshInstance == null)
        {
            GD.PushError("BananaLeafBounce: MeshInstancePath not found.");
            return;
        }

        _skeleton = _meshInstance.GetParent() as Skeleton3D;
        if (_skeleton == null)
        {
            GD.PushError("BananaLeafBounce: mesh instance's parent must be a Skeleton3D.");
            return;
        }

        // Converts a WORLD-space bounce offset into the skeleton's own bone-pose
        // space (SetBonePosePosition is relative to the skeleton's bone hierarchy,
        // not world space).
        _skeletonScale = _skeleton.GlobalTransform.Basis.Scale.X;

        BuildLeafData();
        BuildLeafCollision();
    }

    /// <summary>Public entry point: trigger a single bounce cycle on the leaf nearest to marioPos.</summary>
    public void Bounce(Vector3 marioPos)
    {
        if (_bouncing)
            return;
        _activeBoneIdx = FindNearestLeafBone(marioPos);
        _bouncing = true;
        _bounceTimer = 0f;
    }

    public override void _Process(double delta)
    {
        if (!_bouncing)
            return;

        _bounceTimer += (float)delta;

        float worldOffset;
        if (_bounceTimer < DropDuration)
        {
            float t = _bounceTimer / DropDuration;
            worldOffset = -BounceDepth * EaseOutQuad(t);
        }
        else
        {
            float t = (_bounceTimer - DropDuration) / RiseDuration;
            if (t >= 1f)
            {
                _bouncing = false;
                ApplyBoneOffset(0f);
                return;
            }
            worldOffset = -BounceDepth * (1f - EaseOutSine(t));
        }

        ApplyBoneOffset(worldOffset);
    }

    private void ApplyBoneOffset(float worldOffset)
    {
        if (_skeleton == null || _activeBoneIdx < 0)
            return;
        // SetBonePosePosition takes an ABSOLUTE bone-local position (it defaults to
        // the bone's rest position, not zero) — so the dip has to be added onto rest,
        // not passed as a bare delta, or it snaps the bone to (0, boneY, 0) instead
        // of nudging it.
        float boneY = worldOffset / _skeletonScale;
        Vector3 rest = _skeleton.GetBoneRest(_activeBoneIdx).Origin;
        _skeleton.SetBonePosePosition(_activeBoneIdx, rest + new Vector3(0f, boneY, 0f));

        // Drag the collision down with it (already in world-space units, no scale
        // conversion needed) so Mario's footing actually follows the visual dip.
        if (_leafCollisionByBone.TryGetValue(_activeBoneIdx, out var cs) &&
            _leafCollisionRestPos.TryGetValue(_activeBoneIdx, out var restPos))
        {
            cs.GlobalPosition = restPos + new Vector3(0f, worldOffset, 0f);
        }
    }

    private void BuildLeafData()
    {
        if (_meshInstance.Mesh is not ArrayMesh mesh || _meshInstance.Skin == null)
            return;

        var skin = _meshInstance.Skin;

        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            if (s == TrunkSurfaceIndex)
                continue;

            var arrays = mesh.SurfaceGetArrays(s);
            int skinBindIdx = GetDominantSkinBind(arrays);
            if (skinBindIdx < 0 || skinBindIdx >= skin.GetBindCount())
                continue;

            string boneName = skin.GetBindName(skinBindIdx);
            int boneIdx = _skeleton.FindBone(boneName);
            if (boneIdx < 0)
                continue;

            Vector3 localCenter = SurfaceCentroid(arrays);
            Vector3 worldCenter = _meshInstance.ToGlobal(localCenter);
            float angle = Mathf.Atan2(worldCenter.X - GlobalPosition.X, worldCenter.Z - GlobalPosition.Z);

            _leafByAngle.Add((angle, boneIdx));
            _surfaceToBone[s] = boneIdx;
        }
    }

    /// <summary>
    /// Builds one StaticBody3D ("LeafBody") with a ConcavePolygonShape3D per leaf
    /// surface, so Mario can physically stand on/land on the leaves. The raw surface
    /// vertex data is in bind-pose space, which — same gotcha as the palm tree's
    /// leaves — carries the Skin's bind-pose scale baked in, so it's corrected here
    /// the same way LeafCollisionFixer.cs corrects the palm tree's hand-placed
    /// shapes: scale by the bind pose's own scale, then place via the mesh
    /// instance's GlobalTransform (all surfaces share one transform since they're
    /// surfaces of the same merged mesh, not separately-positioned nodes).
    /// </summary>
    private void BuildLeafCollision()
    {
        if (_meshInstance.Mesh is not ArrayMesh mesh)
            return;

        float bindScale = 1f;
        if (_meshInstance.Skin != null && _meshInstance.Skin.GetBindCount() > 0)
            bindScale = _meshInstance.Skin.GetBindPose(0).Basis.Scale.X;
        Transform3D bindXform = Transform3D.Identity.Scaled(Vector3.One * bindScale);
        Transform3D desiredGlobal = _meshInstance.GlobalTransform * bindXform;

        var leafBody = new StaticBody3D { Name = "LeafBody", CollisionLayer = 4, CollisionMask = 0 };
        AddChild(leafBody);
        leafBody.AddToGroup("palm_leaf_body");

        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            if (s == TrunkSurfaceIndex)
                continue;

            var arrays = mesh.SurfaceGetArrays(s);
            Vector3[] faces = ExpandTriangles(arrays);
            if (faces.Length == 0)
                continue;

            var shape = new ConcavePolygonShape3D { Data = faces };
            var cs = new CollisionShape3D { Shape = shape, Name = $"leaf_{s}" };
            leafBody.AddChild(cs);
            cs.GlobalTransform = desiredGlobal;

            if (_surfaceToBone.TryGetValue(s, out int boneIdx))
            {
                _leafCollisionByBone[boneIdx] = cs;
                _leafCollisionRestPos[boneIdx] = cs.GlobalPosition;
            }
        }
    }

    private int FindNearestLeafBone(Vector3 marioPos)
    {
        if (_leafByAngle.Count == 0)
            return -1;

        float marioAngle = Mathf.Atan2(marioPos.X - GlobalPosition.X, marioPos.Z - GlobalPosition.Z);

        float bestDiff = float.MaxValue;
        int bestBone = _leafByAngle[0].boneIdx;
        foreach (var (angle, boneIdx) in _leafByAngle)
        {
            // Shortest angular distance between two angles
            float d = marioAngle - angle;
            d -= Mathf.Round(d / (Mathf.Pi * 2f)) * (Mathf.Pi * 2f);
            float diff = Mathf.Abs(d);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestBone = boneIdx;
            }
        }
        return bestBone;
    }

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseOutSine(float t) => Mathf.Sin(t * Mathf.Pi * 0.5f);

    private static Vector3 SurfaceCentroid(Godot.Collections.Array arrays)
    {
        var verts = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
        if (verts.Length == 0)
            return Vector3.Zero;
        Vector3 sum = Vector3.Zero;
        foreach (var v in verts)
            sum += v;
        return sum / verts.Length;
    }

    private static Vector3[] ExpandTriangles(Godot.Collections.Array arrays)
    {
        var verts = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
        var indicesVariant = arrays[(int)Mesh.ArrayType.Index];
        if (indicesVariant.VariantType == Variant.Type.Nil)
            return verts;

        var indices = indicesVariant.AsInt32Array();
        var faces = new Vector3[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            faces[i] = verts[indices[i]];
        return faces;
    }

    /// <summary>
    /// Reads the surface's BONES/WEIGHTS arrays and returns the skin-bind index that
    /// accumulates the highest total weight. For rigidly-bound leaves this just
    /// returns the single bone they're attached to.
    /// </summary>
    private static int GetDominantSkinBind(Godot.Collections.Array arrays)
    {
        if (arrays.Count <= (int)Mesh.ArrayType.Weights)
            return -1;

        var bonesVar = arrays[(int)Mesh.ArrayType.Bones];
        var weightsVar = arrays[(int)Mesh.ArrayType.Weights];
        if (bonesVar.VariantType == Variant.Type.Nil)
            return -1;

        int[] bones = bonesVar.AsInt32Array();
        float[] weights = weightsVar.AsFloat32Array();
        if (bones.Length == 0)
            return -1;

        var totals = new Dictionary<int, float>();
        int len = Mathf.Min(bones.Length, weights.Length);
        for (int i = 0; i < len; i++)
        {
            float w = weights[i];
            if (w <= 0f)
                continue;
            int b = bones[i];
            totals.TryGetValue(b, out float t);
            totals[b] = t + w;
        }

        int best = -1;
        float bestW = -1f;
        foreach (var kv in totals)
            if (kv.Value > bestW)
            {
                bestW = kv.Value;
                best = kv.Key;
            }
        return best;
    }
}
