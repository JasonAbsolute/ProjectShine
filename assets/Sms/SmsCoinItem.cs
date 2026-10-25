using Godot;
using System;

/// <summary>
/// SMS pass 123: the game's item life for a coin (TItem / TCoin, MoveBG/Item.cpp; TMapObjGeneral::bind, MapObjGeneral.cpp 440-520), as a
/// state the coin scripts (BlueCoin.cs, YellowCoin.cs) carry and tick on the 120 Hz clock. A coin thrown out of a hide object or dropped by
/// an enemy / a nail flies with the game's coin physics (coin_physical_data: gravity 0.35 u/tick^2, bounce 0.5, ground damping 0.5, air drag
/// 0.99, land under 1 u/tick; no wall or roof collision -- flags 0x18104100), cannot be taken for its first 120 ticks (TItem::appear:
/// HIT_FILTER_NO_COLLISION, mUntouchableFrames), then -- when it is a coin that disappears -- lives mDisappearAfterFrames ticks, drawn blinking
/// through the last 360 (hidden while (timer / 10) is odd, TMapObjGeneral::perform 584), and dies with MSD_SE_SY_COIN_DISAPPEAR and an ATTACH
/// to its container (the hide object re-arms). WHICH COINS DISAPPEAR: TItem::load sets MAP_OBJ_FLAG_UNK10000000 ("never") on every stage-loaded
/// item; TCoin::appearWithoutSound clears it for a yellow (ACTOR_TYPE_COIN) only, TWaterHitPictureHideObj::loadAfter clears it for any coin a
/// graffiti picture hides -- so a BLUE coin out of a plain sprayed / hip-drop spot stays, a yellow one goes (480 ticks by default), a blue one
/// out of a graffiti M goes (1200). The spin is the item manager's ONE matrix (TItemManager::perform: 2 deg a tick, 240 deg/s) -- every coin
/// in the stage turns in phase; <see cref="SharedYaw"/> gives it. Not ported: the talk-mode freeze of a resting coin, the mirror actor.
/// </summary>
public static class SmsCoinItem
{
    // coin_physical_data (MapObjInit.cpp 1163): { mGravity 0.35, unk4 0.5 (the bounce: vy *= -unk4), unk8 0.6, unkC 1.0 (land under it / the min
    // speed), unk10 0.5 (xz at a ground contact), unk14 0.95, unk18 0.99 (xz a tick in the air and on the ground) }
    public const float Gravity = 0.35f, Bounce = 0.5f, GroundDamp = 0.5f, MinSpeed = 1.0f, AirDrag = 0.99f;
    /// <summary>coin_hit_data_table { 70, 100, 70, 100 }: mBodyRadius 70 x scale (the velocity clamp), mHeadHeight 100 x scale (the ground probe's top).</summary>
    public const float BodyRadiusUnits = 70f, HeadHeightUnits = 100f;
    /// <summary>TItem::initMapObj: mUntouchableFrames 120, mDisappearAfterFrames 480; TMapObjGeneral: mNormalFlushTime 360, mNormalFlushInterval 10.</summary>
    public const int UntouchableTicks = 120, DisappearTicksDefault = 480, FlushTicks = 360, FlushInterval = 10;
    /// <summary>TItemManager unk70: the shared yaw's step, degrees a tick.</summary>
    public const float SpinDegPerTick = 2f;
    /// <summary>TSmallEnemy's death coin / the hide objects' default: straight up at 20 u a tick (smallEnemy.cpp 353; loadHideObjInfo ySpeed &lt; 0 -> 20).</summary>
    public const float PopUpSpeedUnits = 20f;
    /// <summary>The map's layers for the coin's ground probe: 1 (the level) + 3 (the one-way platforms), as Mario's mask -- never a hide body (layer 8).</summary>
    public const uint GroundMask = 5;
    /// <summary>Every coin dying by its timer (a headless drive counts them).</summary>
    public static int DisappearCount;

    public struct State
    {
        public bool Active;        // this life owns the coin (after Throw)
        public bool Resting;       // LIVE_FLAG_UNK10: bind returns -- static on the ground
        public bool Airborne;      // LIVE_FLAG_AIRBORNE
        public Vector3 Vel;        // units a tick, world axes
        public Vector3 GroundN;    // mGroundPlane normal
        public bool CollisionOff;  // HIT_FILTER_NO_COLLISION (the pickup area off)
        public bool Disappearing;  // MAP_OBJ_FLAG_DISAPPEARING
        public int Timer;          // mStateTimer (the untouchable count, then the life)
        public int DisappearAfter; // mDisappearAfterFrames; -1 = MAP_OBJ_FLAG_UNK10000000 kept (never disappears)
        public float Scale;        // the object's scale for the hit radii
        public float Accum;        // the 120 Hz tick accumulator
        public int Ticks;          // ticks lived since the throw (the drives)
        public int Bounces;        // ground bounces so far (the drives)
        public float MaxY;         // the highest bottom seen, metres (the drives)
        public Node Container;     // THideObjBase mContainer: told when the coin dies by its timer
        public Area3D Pickup;      // the coin's PickupZone
        public bool Dead;
    }

    /// <summary>throwObjToFrontFromPoint / TSmallEnemy's drop: the coin appears at its current position with <paramref name="velUnitsPerTick"/>
    /// (world axes, units a tick), untouchable for 120 ticks; <paramref name="disappearAfter"/> ticks of life once touchable, -1 = never.</summary>
    public static void Throw(ref State st, Node3D coin, Vector3 velUnitsPerTick, int disappearAfter, Node container = null)
    {
        st.Active = true; st.Dead = false;
        st.Resting = false; st.Airborne = true;              // makeObjAppeared: UNK10 -> AIRBORNE; the throw offs UNK10
        st.Vel = velUnitsPerTick;
        st.GroundN = Vector3.Up;
        st.CollisionOff = true; st.Timer = UntouchableTicks;  // TItem::appear
        st.Disappearing = false; st.DisappearAfter = disappearAfter;
        st.Scale = coin.Scale.Y > 0f ? coin.Scale.Y : 1f;
        st.Accum = 0f; st.Ticks = 0; st.Bounces = 0; st.MaxY = coin.GlobalPosition.Y;
        st.Container = container;
        st.Pickup = coin.GetNodeOrNull<Area3D>("PickupZone");
        SetPickup(ref st, false);
    }

    private static void SetPickup(ref State st, bool on)
    {
        if (st.Pickup == null || !GodotObject.IsInstanceValid(st.Pickup)) return;
        st.Pickup.SetDeferred(Area3D.PropertyName.Monitoring, on);
        st.Pickup.SetDeferred(Area3D.PropertyName.Monitorable, on);
    }

    /// <summary>The coin's physics frame: every owed 120 Hz tick moves it (bind) and runs its timers. True = the coin died by its timer (the
    /// caller frees it after its own sound). Sets the coin's Visible for the flush blink.</summary>
    public static bool Tick(ref State st, Node3D coin, double delta, AudioStreamPlayer3D boundSound = null)
    {
        if (!st.Active || st.Dead) return false;
        int n = SmsUnits.TicksFor(delta, ref st.Accum);
        for (int t = 0; t < n && !st.Dead; t++)
        {
            st.Ticks++;
            if (!st.Resting) Bind(ref st, coin, boundSound);
            // one decrement a tick either way: TMapObjBase::perform 477 in the air, TCoin::perform 269 at rest
            if (st.Timer > 0) st.Timer--;
            else if (st.CollisionOff)
            {
                // TItem::perform 166 / TCoin::perform 272: the untouchable spell is over -- collision on; a coin that disappears starts its life
                st.CollisionOff = false;
                SetPickup(ref st, true);
                if (st.DisappearAfter >= 0) { st.Disappearing = true; st.Timer = st.DisappearAfter; }
            }
            else if (st.Disappearing)
            {
                // TCoin::perform 281 / TItem::appeared 43: the life ran out -- ATTACH to the container, MSD_SE_SY_COIN_DISAPPEAR, dead
                st.Dead = true;
                DisappearCount++;
                if (st.Container != null && GodotObject.IsInstanceValid(st.Container) && st.Container is SmsHiddenCoin hide)
                    hide.OnHiddenItemGone(coin);
            }
        }
        // TMapObjGeneral::perform 584: drawn unless DISAPPEARING, timer under the flush time and (timer / 10) odd
        bool shown = !(st.Disappearing && st.Timer > 0 && st.Timer < FlushTicks && ((st.Timer / FlushInterval) & 1) == 1);
        if (coin.Visible != shown) coin.Visible = shown;
        return st.Dead;
    }

    /// <summary>TMapObjGeneral::bind (500) for a coin: calcVelocity, pos += vel, checkGroundCollision; no wall / roof (the coin's flags).</summary>
    private static void Bind(ref State st, Node3D coin, AudioStreamPlayer3D boundSound)
    {
        float R = BodyRadiusUnits * st.Scale;
        // calcVelocity (476)
        if (st.Airborne) { st.Vel.Y -= Gravity; st.Vel.Y = Mathf.Clamp(st.Vel.Y, -R, R); }
        st.Vel.X = Mathf.Clamp(st.Vel.X * AirDrag, -R, R);
        st.Vel.Z = Mathf.Clamp(st.Vel.Z * AirDrag, -R, R);
        if (st.GroundN.Y >= 0.9999f)
        {
            if (Mathf.Abs(st.Vel.X) < MinSpeed) st.Vel.X = 0f;
            if (Mathf.Abs(st.Vel.Z) < MinSpeed) st.Vel.Z = 0f;
        }
        // pos += vel (the game's mPosition is the coin's BOTTOM, as his coin scenes' origin)
        Vector3 pos = coin.GlobalPosition + st.Vel * (1f / SmsUnits.UnitsPerMetre);
        // checkGroundCollision (464): the floor under the head point, + 1 unit
        var space = coin.GetWorld3D()?.DirectSpaceState;
        if (space != null)
        {
            Vector3 headPt = pos + Vector3.Up * SmsUnits.UnitsToMetres(HeadHeightUnits * st.Scale);
            var gq = PhysicsRayQueryParameters3D.Create(headPt, headPt - Vector3.Up * 200f, GroundMask);
            gq.CollideWithAreas = false;
            var ghit = space.IntersectRay(gq);
            if (ghit.Count > 0 && IsMap(ghit["collider"].AsGodotObject()))
            {
                float groundY = ((Vector3)ghit["position"]).Y + SmsUnits.UnitsToMetres(1f);
                Vector3 gn = (Vector3)ghit["normal"];
                if (pos.Y <= groundY)
                {
                    // (440) a ground contact: xz x unk10; a fall faster than unkC rebounds (the step undone, vy x -unk4) else it lands
                    st.GroundN = gn;
                    st.Vel.X *= GroundDamp; st.Vel.Z *= GroundDamp;
                    if (Mathf.Abs(st.Vel.Y) > MinSpeed)
                    {
                        pos.Y -= SmsUnits.UnitsToMetres(st.Vel.Y);
                        st.Vel.Y *= -Bounce;
                        st.Bounces++;
                        if (boundSound != null && boundSound.Stream != null) boundSound.Play();   // MSD_SE_SY_COIN_BOUND
                    }
                    else
                    {
                        st.Airborne = false;
                        st.Vel = Vector3.Zero;
                        pos.Y = groundY;
                    }
                }
                else st.Airborne = true;
            }
            else st.Airborne = true;
        }
        if (!st.Airborne && st.Vel == Vector3.Zero) st.Resting = true;   // bind's end: on the ground and still -> LIVE_FLAG_UNK10
        coin.GlobalPosition = pos;
        if (pos.Y > st.MaxY) st.MaxY = pos.Y;
    }

    /// <summary>The game's coin collides with the map (BG) only: not Mario, not another body, not an area, never a hide body.</summary>
    public static bool IsMap(GodotObject o) => o is StaticBody3D && o is not SmsHiddenCoin || o is CsgShape3D || o is AnimatableBody3D;

    /// <summary>TItemManager::perform: the one yaw every item wears, radians -- 2 deg a 120 Hz tick from the engine's physics clock.</summary>
    public static float SharedYaw()
    {
        double degPerPhysicsFrame = SpinDegPerTick * SmsUnits.TicksPerSecond / Math.Max(1, Engine.PhysicsTicksPerSecond);
        double deg = (Engine.GetPhysicsFrames() * degPerPhysicsFrame) % 360.0;
        return (float)(deg * Math.PI / 180.0);
    }

    /// <summary>Apply the shared yaw to a coin (TItem::calc writes the manager's matrix over the model's rotation, the position kept).</summary>
    public static void Spin(Node3D coin)
    {
        Vector3 r = coin.Rotation;
        coin.Rotation = new Vector3(r.X, SharedYaw(), r.Z);
    }
}
