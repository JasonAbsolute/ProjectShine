using Godot;

/// <summary>
/// TGKHitObj (Enemy/gatekeeper.cpp 70-114; pass 126b): the gatekeeper's HEAD hit actor, "頭ヒット" -- a 150 x 200 u cylinder (initHitActor, ACTOR_TYPE_BIANCO_GATE_KEEPER,
/// HIT_CATEGORY_PLAYER) placed every frame at joint 10 (kutijnt1, the jaw) of the owner's animated model. A water hit: the WATHIT mark and MSD_SE_EN_COMMON_W_HIT_OK
/// always, a hit counted on the owner while the head is vulnerable (controlCollision), TRUE (the drop dies). Any other message goes to the owner. Dead owner: it
/// stops following and takes nothing. Here a StaticBody3D on the spray-only layer built by <see cref="SmsGateKeeper"/> at _Ready (or placed in its scene as
/// "HeadHit"), TopLevel, moved by the owner each physics frame.
/// </summary>
[GlobalClass]
public partial class SmsGateKeeperHead : StaticBody3D, ISmsSprayTarget
{
    /// <summary>The gatekeeper this head belongs to (set by it).</summary>
    public SmsGateKeeper Owner3;

    public bool SmsSprayedByWater(Vector3 hitPos) => Owner3 != null && GodotObject.IsInstanceValid(Owner3) && Owner3.HeadSprayedByWater(hitPos);
}
