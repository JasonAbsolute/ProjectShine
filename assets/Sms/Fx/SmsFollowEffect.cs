using Godot;

/// <summary>
/// An effect bound to Mario the way TMarioParticleManager binds an emitter (emitAndBindToPosPtr / emitAndBindToMtxPtr, EmitterViewObj.cpp):
/// every frame the node moves to a joint's position -- the game re-reads the pointer each frame. The joint is 'center' (joint 1 of the
/// model: TMario::unk160 is that matrix's translation, getCenterAnmMtx() the matrix itself); the emitter's own translation (BEM1 trs,
/// game units) is added -- in world axes for a pos-ptr bind, in the owner's frame for a matrix bind (OffsetInOwnerFrame). Position only: the node keeps its parent's rotation, so an effect
/// instanced under Mario turns with him (the mtx binds do too; the flat rings and stars do not show a yaw). Bind(owner) finds the
/// Skeleton3D under the owner; without one, or without the joint, the owner's origin stands in with a warning. Runs after the
/// animation (process priority 100). The scenes: assets/Sms/Fx/*.tscn.
/// </summary>
public partial class SmsFollowEffect : Node3D
{
    /// <summary>The joint the emitter is bound to. The game: 'center' (joint 1) for every Mario effect here.</summary>
    [Export] public string BoneName = "center";

    /// <summary>The emitter's own translation, BEM1 trs in game units (74.44 per metre), added to the joint's position.</summary>
    [Export] public Vector3 OffsetUnits = Vector3.Zero;

    /// <summary>False: a pos-ptr bind, the offset in world axes (emitAndBindToPosPtr). True: a matrix bind, the offset in the owner's
    /// frame -- it turns with Mario's yaw (emitAndBindToMtxPtr: the emitter sits in the joint's matrix).</summary>
    [Export] public bool OffsetInOwnerFrame = false;
    /// <summary>Follow Mario's feet (the game's mPosition) instead of a joint or the body's origin.</summary>
    [Export] public bool AtFeet = false;
    /// <summary>Turn this node with his facing (a matrix bind's frame): the emitters' directions follow him.</summary>
    [Export] public bool TurnWithOwner = false;

    private Node3D _owner;
    private Node3D _frame; // his facing: the Armature (the body stands unrotated since pass 54)
    private Skeleton3D _skeleton;
    private int _bone = -1;

    public bool IsBound => _owner != null;

    /// <summary>Bind to an actor: its Skeleton3D's joint is followed from now on.</summary>
    public virtual void Bind(Node3D owner)
    {
        _owner = owner;
        _frame = owner.GetNodeOrNull<Node3D>("Armature") ?? owner;
        _skeleton = owner as Skeleton3D ?? SmsFluddModel.FindChild<Skeleton3D>(owner);
        _bone = _skeleton != null && BoneName != "" ? _skeleton.FindBone(BoneName) : -1;
        if (_bone < 0 && BoneName != "")
            GD.PushWarning($"[SMS fx] {GetPath()}: no joint '{BoneName}' under {owner.Name} -- following its origin instead (set BoneName on the scene, {SceneFilePath})");
        ProcessPriority = 100; // after the AnimationPlayer and the skeleton modifiers
        Follow();
    }

    public override void _Process(double delta) => Follow();

    protected void Follow()
    {
        if (_owner == null || !IsInsideTree())
            return;
        Vector3 at = _bone >= 0
            ? _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_bone).Origin
            : AtFeet && _owner is Mario feetOf ? feetOf.SmsFeetWorld() : _owner.GlobalPosition;
        Vector3 offset = OffsetUnits * SmsUnits.UnitsToMetres(1f);
        if (OffsetInOwnerFrame)
            offset = _frame.GlobalBasis.Orthonormalized() * offset;
        GlobalPosition = at + offset;
        if (TurnWithOwner)
            GlobalRotation = new Vector3(0f, _frame.GlobalRotation.Y, 0f);
    }
}
