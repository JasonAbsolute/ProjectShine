using Godot;

/// <summary>The stand-in Mario's hold code carries when the thing in his hands is an NPC (pass 83). TBaseNPC is a TTakeActor: the game's
/// hold code is generic over the holder's taking matrix and the held object sets ITS OWN position from it each tick. Mario's port holds a
/// RigidBody3D (frozen, parented to the carry socket): this frozen, shapeless body is that object -- it rides his socket like a fruit, and the
/// SmsNpc reads it as the game reads mHolder->getTakingMtx(). It carries the NPC's receive cylinder as an SmsTakeable child (Kind Object:
/// a map object, pitched past fwd 16, else put -- an NPC's actor type has no 0x10000000 enemy bit) so canPut() probes with the NPC's radius.
/// Freed the tick Mario stops pending / holding it (ensureTakeSituation: the NPC releases itself when its holder's held object is not it).</summary>
public partial class SmsNpcHoldProxy : RigidBody3D
{
    public SmsNpc Npc;
    public Mario Holder;
    private bool _taken;

    public static SmsNpcHoldProxy Create(SmsNpc npc, Mario mario)
    {
        var p = new SmsNpcHoldProxy
        {
            Name = "SmsNpcHoldProxy", Npc = npc, Holder = mario,
            Freeze = true, FreezeMode = FreezeModeEnum.Kinematic, GravityScale = 0f, CollisionLayer = 0, CollisionMask = 0, TopLevel = true,
        };
        var take = new SmsTakeable
        {
            Numbers = SmsTakeable.Preset.Custom, Kind = SmsTakeable.TakeKind.Object, CanTake = true, Size = 1f,
            HitRadius = npc.HitRadiusUnits, HitHeight = npc.HitHeightUnits,
            ThrowRate = 0f, ThrowVy = 0f, Gravity = SmsParams.Npc.GravityY, // never used: the NPC flies on releaseTaken_'s own numbers
        };
        p.AddChild(take);
        npc.GetTree().Root.AddChild(p);
        // the search's body-centre convention (bottom = position - half the hit height): the NPC's origin is its ground point
        p.GlobalPosition = npc.GlobalPosition + Vector3.Up * SmsUnits.UnitsToMetres(npc.HitHeightUnits) * 0.5f;
        return p;
    }

    /// <summary>HIT_MESSAGE_TAKE at the raise's take frame: the NPC is in his hands from here (behaveToBeTaken_).</summary>
    public void OnAttached(Mario mario)
    {
        if (_taken || Npc == null || !IsInstanceValid(Npc)) return;
        _taken = true;
        Holder = mario;
        Npc.SmsTaken(mario, this);
    }

    /// <summary>dropObject() / putting(): both end as the NPC's releaseTaken_ -- its own throw speed along his facing.</summary>
    public void Release()
    {
        if (_taken && Npc != null && IsInstanceValid(Npc)) Npc.SmsReleased();
        _taken = false;
        QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        // ensureTakeSituation: a holder that no longer pends or holds this proxy (a cancelled raise, a knockdown's drop) releases the NPC
        if (Holder == null || !IsInstanceValid(Holder) || !Holder.SmsHoldsOrPends(this)) Release();
    }
}
