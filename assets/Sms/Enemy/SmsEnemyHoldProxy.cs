using Godot;

/// <summary>The stand-in Mario's hold code carries when the thing in his hands is a small enemy (pass 130b: the Bob-omb; the NPC has its own,
/// SmsNpcHoldProxy, pass 83). TSmallEnemy is a TTakeActor: the game's hold is generic over the holder's taking matrix and the held body sets ITS OWN
/// matrix from it each tick (TSpineEnemy::calcRootMatrix). Mario's port holds a RigidBody3D (frozen, TopLevel, placed at his carry socket): this frozen,
/// shapeless body is that object -- it rides his socket like a fruit and the enemy reads it as mHolder->getTakingMtx(). Its SmsTakeable child says
/// Kind Enemy (isThrowStart: an enemy is ALWAYS pitched, never put down -- actor type bit 0x10000000) with the body's receive cylinder; the flight
/// numbers are the enemy's own (TNerveBombHeiThrown), never this table's. Freed when the hold ends either way.</summary>
public partial class SmsEnemyHoldProxy : RigidBody3D
{
    public SmsSmallEnemy Enemy;
    public Mario Holder;
    private bool _taken, _gone;

    public static SmsEnemyHoldProxy Create(SmsSmallEnemy enemy, Mario mario)
    {
        var p = new SmsEnemyHoldProxy
        {
            Name = "SmsEnemyHoldProxy", Enemy = enemy, Holder = mario,
            Freeze = true, FreezeMode = FreezeModeEnum.Kinematic, GravityScale = 0f, CollisionLayer = 0, CollisionMask = 0, TopLevel = true,
        };
        var take = new SmsTakeable
        {
            Numbers = SmsTakeable.Preset.Custom, Kind = SmsTakeable.TakeKind.Enemy, CanTake = true, Size = 1f,
            HitRadius = enemy.TakeHitRadiusUnits, HitHeight = enemy.TakeHitHeightUnits,
            ThrowRate = 0f, ThrowVy = 0f, Gravity = 0.3f,       // never used: the enemy flies on its own nerve's numbers
        };
        p.AddChild(take);
        enemy.GetTree().Root.AddChild(p);
        // the search's body-centre convention (bottom = position - half the hit height): the enemy's origin is its ground point
        p.GlobalPosition = enemy.GlobalPosition + Vector3.Up * SmsUnits.UnitsToMetres(enemy.TakeHitHeightUnits) * 0.5f;
        return p;
    }

    /// <summary>HIT_MESSAGE_TAKE at the raise's take frame: the body is in his hands from here (TSmallEnemy::receiveMessage -> behaveToTaken).</summary>
    public void OnAttached(Mario mario)
    {
        if (_taken || _gone || Enemy == null || !IsInstanceValid(Enemy)) return;
        _taken = true;
        Holder = mario;
        Enemy.SmsTaken(mario, this);
    }

    /// <summary>dropObject() -> HIT_MESSAGE_THROWN, putting() -> HIT_MESSAGE_PUT: the enemy's behaveToRelease either way.</summary>
    public void Release(bool put)
    {
        if (_gone) return;
        _gone = true;
        if (_taken && Enemy != null && IsInstanceValid(Enemy)) Enemy.SmsReleased(put);
        _taken = false;
        QueueFree();
    }

    /// <summary>The holder has just placed this proxy on his carry socket (his physics step, pass 130c): the body goes there NOW, in the same step -- the
    /// body's own step may run before his and would otherwise show last frame's hand (a visible trail while he runs).</summary>
    public void RideNow()
    {
        if (!_taken || _gone || Enemy == null || !IsInstanceValid(Enemy)) return;
        Enemy.SmsRideHand(GlobalPosition);
    }

    /// <summary>The body died in his hands (the Bob-omb's blast) or reset: his ensureTakeSituation empties his hands with no throw.</summary>
    public void OnBodyGone()
    {
        if (_gone) return;
        _gone = true; _taken = false;
        if (Holder != null && IsInstanceValid(Holder)) Holder.SmsHeldGone(this);
        QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        // ensureTakeSituation: a holder that no longer pends or holds this proxy (a cancelled raise, a knockdown's drop that skipped the message) releases the body
        if (_gone) return;
        if (Holder == null || !IsInstanceValid(Holder) || !Holder.SmsHoldsOrPends(this)) Release(false);
    }
}
