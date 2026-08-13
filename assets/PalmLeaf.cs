using Godot;
using System.Collections.Generic;

public partial class PalmLeaf : Node3D, ILeafBouncer
{
    [Export] public float BounceDepth = 0.35f;
    [Export] public float DropDuration = 0.12f;
    [Export] public float RiseDuration = 0.75f;

    private Vector3 _restPosition;
    private Skeleton3D _skeleton;
    private float _skeletonScale = 1f;

    // Each entry: (XZ angle from trunk center, bone index)
    private readonly List<(float angle, int boneIdx)> _leafByAngle = [];

    private bool _bouncing = false;
    private float _bounceTimer = 0f;
    private int _activeBoneIdx = -1;

    public override void _Ready()
    {
        _restPosition = Position;

        var skeletonRoot = GetParent().GetNodeOrNull<Node3D>("skeleton_root");
        if (skeletonRoot != null)
        {
            _skeletonScale = skeletonRoot.Transform.Basis.X.Length();
            _skeleton = skeletonRoot.GetNodeOrNull<Skeleton3D>("Skeleton3D");
        }

        if (_skeleton != null)
        {
            // For each leaf mesh, look up the bone it's actually skinned to (via its
            // Skin's bind list). The earlier "sort angles, sort bones, pair by index"
            // logic was wrong because the bone-index order doesn't match the angular
            // order of the meshes.
            foreach (var child in _skeleton.GetChildren())
            {
                if (child is not MeshInstance3D mi) continue;
                if (mi.Name == "mesh-0") continue; // trunk
                if (mi.Mesh is not ArrayMesh am || mi.Skin == null) continue;

                int skinBindIdx = GetDominantSkinBind(am);
                if (skinBindIdx < 0 || skinBindIdx >= mi.Skin.GetBindCount()) continue;

                string boneName = mi.Skin.GetBindName(skinBindIdx);
                int boneIdx = _skeleton.FindBone(boneName);
                if (boneIdx < 0) continue;

                Vector3 worldCenter = mi.ToGlobal(mi.GetAabb().GetCenter());
                float angle = Mathf.Atan2(
                    worldCenter.X - GlobalPosition.X,
                    worldCenter.Z - GlobalPosition.Z);

                _leafByAngle.Add((angle, boneIdx));
                GD.Print($"[PalmLeaf] {mi.Name} -> bone '{boneName}' (idx {boneIdx}) at angle {Mathf.RadToDeg(angle):F1}°");
            }
        }

        var area = GetNodeOrNull<Area3D>("LandingZone");
        if (area != null)
            area.BodyEntered += OnBodyEntered;

        // Tag the leaf body so Mario can recognize it from his landing-collider check.
        var leafBody = GetNodeOrNull<StaticBody3D>("LeafBody");
        if (leafBody != null)
            leafBody.AddToGroup("palm_leaf_body");
    }

    /// <summary>Public entry point: trigger a single bounce cycle on the leaf nearest to marioPos.</summary>
    public void Bounce(Vector3 marioPos)
    {
        if (_bouncing) return;
        _activeBoneIdx = FindNearestLeafBone(marioPos);
        _bouncing = true;
        _bounceTimer = 0f;
    }

    public override void _Process(double delta)
    {
        if (!_bouncing) return;

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
                ApplyOffset(0f);
                return;
            }
            worldOffset = -BounceDepth * (1f - EaseOutSine(t));
        }

        ApplyOffset(worldOffset);
    }

    private void ApplyOffset(float worldOffset)
    {
        Position = _restPosition + new Vector3(0f, worldOffset, 0f);

        if (_skeleton == null || _activeBoneIdx < 0) return;
        // SetBonePosePosition takes an ABSOLUTE bone-local position (it defaults to
        // the bone's rest position, not zero) — so the dip has to be added onto rest,
        // not passed as a bare delta, or it snaps the bone to (0, boneY, 0) instead
        // of nudging it.
        float boneY = worldOffset / _skeletonScale;
        Vector3 rest = _skeleton.GetBoneRest(_activeBoneIdx).Origin;
        _skeleton.SetBonePosePosition(_activeBoneIdx, rest + new Vector3(0f, boneY, 0f));
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not Mario mario) return;
        if (mario.Velocity.Y > 0f) return;  // jumping up through from below
        Bounce(mario.GlobalPosition);
    }

    private int FindNearestLeafBone(Vector3 marioPos)
    {
        if (_leafByAngle.Count == 0) return -1;

        float marioAngle = Mathf.Atan2(
            marioPos.X - GlobalPosition.X,
            marioPos.Z - GlobalPosition.Z);

        float bestDiff = float.MaxValue;
        int bestBone = _leafByAngle[0].boneIdx;
        foreach (var (angle, boneIdx) in _leafByAngle)
        {
            // Shortest angular distance between two angles
            float d = marioAngle - angle;
            d -= Mathf.Round(d / (Mathf.Pi * 2f)) * (Mathf.Pi * 2f);
            float diff = Mathf.Abs(d);
            if (diff < bestDiff) { bestDiff = diff; bestBone = boneIdx; }
        }
        return bestBone;
    }

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseOutSine(float t) => Mathf.Sin(t * Mathf.Pi * 0.5f);

    /// <summary>
    /// Reads the mesh's first surface BONES/WEIGHTS arrays and returns the skin-bind
    /// index that accumulates the highest total weight. For rigidly-bound leaves this
    /// just returns the single bone they're attached to.
    /// </summary>
    private static int GetDominantSkinBind(ArrayMesh am)
    {
        if (am.GetSurfaceCount() == 0) return -1;
        var arrays = am.SurfaceGetArrays(0);
        if (arrays.Count <= (int)Mesh.ArrayType.Weights) return -1;

        var bonesVar = arrays[(int)Mesh.ArrayType.Bones];
        var weightsVar = arrays[(int)Mesh.ArrayType.Weights];
        if (bonesVar.VariantType == Variant.Type.Nil) return -1;

        int[] bones = bonesVar.AsInt32Array();
        float[] weights = weightsVar.AsFloat32Array();
        if (bones.Length == 0) return -1;

        // 4 or 8 weights per vertex; tally totals per bind index
        var totals = new Dictionary<int, float>();
        int len = Mathf.Min(bones.Length, weights.Length);
        for (int i = 0; i < len; i++)
        {
            float w = weights[i];
            if (w <= 0f) continue;
            int b = bones[i];
            totals.TryGetValue(b, out float t);
            totals[b] = t + w;
        }

        int best = -1;
        float bestW = -1f;
        foreach (var kv in totals)
            if (kv.Value > bestW) { bestW = kv.Value; best = kv.Key; }
        return best;
    }
}
