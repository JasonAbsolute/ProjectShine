using System.Collections.Generic;
using Godot;

/// <summary>
/// SMS 240: the game's palm leaves (TMapObjTree, MapObjTree.cpp) -- each leaf hangs on its own joint at the crown and dips about that joint's X
/// axis on a damped spring: when Mario lands on or stands on a leaf the leaf's angular velocity takes an impulse (touchPlayer: mLeafTouchImpulse
/// 0.001 rad every frame he is on it, mLeafHipDropImpulse 0.006 for a hip drop), and every frame (controlLeaf) angle += velocity, velocity -= angle x
/// mLeafStiffness (0.01), velocity x= mLeafDamping (0.97), the leaf's matrix = its joint x Rx(angle) and its collision moves with it. The game's numbers
/// for palmNormal (initEach) are the defaults; the step runs at the game's 30 frames a second.
/// Put this on the "Leaves" node at the crown: its children are one AnimatableBody3D per leaf (named as the model's leaf nodes, each turned to its
/// leaf's heading, carrying the leaf's collision in the joint's frame), and ModelPath names the model whose leaf nodes it turns with them. The bodies
/// join Mario's "palm_leaf_body" group so his landing hook (Mario.cs: ILeafBouncer on the body's parent) reaches Bounce.
/// </summary>
public partial class SmsPalmLeaves : Node3D, ILeafBouncer
{
    [Export] public NodePath ModelPath = "../Model";
    /// <summary>mLeafTouchImpulse: radians of angular velocity added every game frame Mario stands on the leaf.</summary>
    [Export] public float TouchImpulse = 0.001f;
    /// <summary>mLeafHipDropImpulse: the hip drop's kick.</summary>
    [Export] public float HipDropImpulse = 0.006f;
    /// <summary>mLeafStiffness: the spring's pull back per frame.</summary>
    [Export] public float Stiffness = 0.01f;
    /// <summary>mLeafDamping: the velocity kept each frame.</summary>
    [Export] public float Damping = 0.97f;
    [Export] public float FramesPerSecond = 30f;
    /// <summary>touchPlayer runs every frame he is on the leaf, so a leaf sags while he stands on it (off = only the landing pushes).</summary>
    [Export] public bool PushWhileStanding = true;

    private class Leaf
    {
        public AnimatableBody3D Body; public Node3D Mesh; public Basis RestBody, RestMesh; public float Angle, Velocity; public bool PushedThisStep;
    }

    private readonly List<Leaf> _leaves = new();
    private Mario _mario;
    private float _acc;

    public override void _Ready()
    {
        Node3D model = GetNodeOrNull<Node3D>(ModelPath);
        foreach (Node c in GetChildren())
        {
            if (c is not AnimatableBody3D body) continue;
            body.SyncToPhysics = true;
            body.AddToGroup("palm_leaf_body");
            Node3D mesh = model?.GetNodeOrNull<Node3D>(body.Name.ToString());
            _leaves.Add(new Leaf { Body = body, Mesh = mesh, RestBody = body.Transform.Basis, RestMesh = mesh != null ? mesh.Transform.Basis : Basis.Identity });
        }
    }

    private Mario FindMario()
    {
        if (_mario != null && IsInstanceValid(_mario)) return _mario;
        _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        return _mario;
    }

    private static bool HipDropping(Mario m)
    {
        var s = m.stateOfMario;
        return s == Mario.MarioState.groundPoundStartup || s == Mario.MarioState.groundPoundFalling || s == Mario.MarioState.groundPoundLanding;
    }

    /// <summary>The leaf Mario's floor contact is on this tick, if any (his slide collisions after MoveAndSlide).</summary>
    private Leaf LeafUnder(Mario m)
    {
        if (m == null || !m.IsOnFloor()) return null;
        int n = m.GetSlideCollisionCount();
        for (int i = 0; i < n; i++)
        {
            var col = m.GetSlideCollision(i);
            if (col.GetNormal().Y < 0.5f) continue;
            GodotObject hit = col.GetCollider();
            foreach (Leaf l in _leaves) if (l.Body == hit) return l;
        }
        return null;
    }

    private Leaf Nearest(Vector3 worldPos)
    {
        Vector3 d = worldPos - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() < 1e-6f) return _leaves.Count > 0 ? _leaves[0] : null;
        Leaf best = null; float bestDot = -2f;
        foreach (Leaf l in _leaves)
        {
            Vector3 fwd = l.Body.GlobalTransform.Basis.Z; fwd.Y = 0f;   // the leaf runs along its joint's +Z
            float dot = fwd.Normalized().Dot(d.Normalized());
            if (dot > bestDot) { bestDot = dot; best = l; }
        }
        return best;
    }

    /// <summary>Mario's landing hook (ILeafBouncer): the leaf he landed on takes the touch impulse, or the hip drop's if he came down pounding.</summary>
    public void Bounce(Vector3 marioPos)
    {
        Mario m = FindMario();
        Leaf l = LeafUnder(m) ?? Nearest(marioPos);
        if (l == null) return;
        l.Velocity += m != null && HipDropping(m) ? HipDropImpulse : TouchImpulse;
        l.PushedThisStep = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_leaves.Count == 0) return;
        _acc += (float)delta;
        float step = 1f / Mathf.Max(FramesPerSecond, 1f);
        bool moved = false;
        while (_acc >= step)
        {
            _acc -= step;
            Leaf under = PushWhileStanding ? LeafUnder(FindMario()) : null;
            foreach (Leaf l in _leaves)
            {
                if (l == under && !l.PushedThisStep) l.Velocity += HipDropping(_mario) ? HipDropImpulse : TouchImpulse;   // touchPlayer, every frame he is on it
                l.PushedThisStep = false;
                if (l.Velocity == 0f && l.Angle == 0f) continue;
                // controlLeaf: the spring with damping
                l.Angle += l.Velocity;
                l.Velocity -= l.Angle * Stiffness;
                l.Velocity *= Damping;
                if (Mathf.Abs(l.Angle) < 1e-4f && Mathf.Abs(l.Velocity) < 1e-5f) { l.Angle = 0f; l.Velocity = 0f; }
                moved = true;
            }
        }
        if (!moved) return;
        foreach (Leaf l in _leaves)
        {
            Basis dip = new Basis(Vector3.Right, l.Angle);   // the joint's own X: the hinge at the crown, the leaf along +Z dipping for a positive angle
            l.Body.Transform = new Transform3D(l.RestBody * dip, l.Body.Transform.Origin);
            if (l.Mesh != null) l.Mesh.Transform = new Transform3D(l.RestMesh * dip, l.Mesh.Transform.Origin);
        }
    }
}
