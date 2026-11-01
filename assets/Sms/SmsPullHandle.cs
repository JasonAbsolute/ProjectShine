using Godot;

/// <summary>
/// SMS pass 149 -- a thing Mario grabs and PULLS without lifting (TTakeActor through TMario::hitNoKeepPull -> MARIO_STATUS_PULLING: the Chain
/// Chomplet's chain end, the Gooper Blooper's tentacles). The owner moves it; Mario holds it from <see cref="BehaveTaken"/> to <see cref="BehaveApart"/>,
/// asks it each tick to come to his hand (<see cref="MoveRequest"/>: true = he steps there too), and faces along -<see cref="TakingTransform"/>.Z.
/// A handle lives in the group "sms_pullable"; its origin is the game's hit-actor point (the grab cylinder's centre, <see cref="HitRadiusUnits"/> x
/// <see cref="HitHeightUnits"/>). The pull params are the game's pullparam*.prm for this handle kind.
/// </summary>
public abstract partial class SmsPullHandle : Node3D
{
    /// <summary>The grab cylinder (the hit actor's damage radius / height), game units.</summary>
    public abstract float HitRadiusUnits { get; }
    public abstract float HitHeightUnits { get; }
    /// <summary>receiveMessage(TAKE): may Mario take hold now (the owner's canTakenByMario)?</summary>
    public abstract bool CanBePulled { get; }
    /// <summary>getTakingMtx: Z = the way from the body to the grab point (Mario faces -Z of it), the origin at the grab point.</summary>
    public abstract Transform3D TakingTransform { get; }
    /// <summary>TTakeActor::moveRequest from his pulling(): the grab point asked to his hand (metres). True = he moves there as well.</summary>
    public abstract bool MoveRequest(Vector3 posM);

    /// <summary>TMario::getCurrentPullParams: how far he steps a 30 fps frame per unit of stick magnitude, back (V) and sideways (H), and the
    /// same on oily goop (OIL_PULLING).</summary>
    public float PullRateV = 0.3f, PullRateH = 0.2f, OilPullRateV = 0.1f, OilPullRateH = 0.01f;

    public Mario Holder { get; protected set; }
    public bool IsHeld => Holder != null && IsInstanceValid(Holder);

    public override void _Ready()
    {
        AddToGroup("sms_pullable");
    }
    /// <summary>receiveMessage(TAKE) accepted.</summary>
    public virtual void BehaveTaken(Mario holder) { Holder = holder; }
    /// <summary>receiveMessage(DETACH / THROWN): his hands off it.</summary>
    public virtual void BehaveApart() { Holder = null; }
}
