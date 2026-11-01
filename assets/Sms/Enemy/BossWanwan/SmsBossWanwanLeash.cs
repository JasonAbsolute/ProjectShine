using Godot;
using System;

/// <summary>
/// SMS pass 150 -- the Chain Chomp's leash (TBWLeash with its TRope of 15 points and 15 TBWLeashNode hit actors, Enemy/bosswanwan.cpp 67-344) and
/// its picket, the stake at the chain's end Mario grabs (<see cref="SmsBossWanwanPicket"/>, TBWPicket : TTakeActor, 346-523).
///
/// THE GAME. TRope(15, the body's point, LeashNodeLen 120, ChainGroundRadius 60, 0.7, -2.0): fifteen points a chain link apart, the head
/// (point 0) at the body's chain_null joint every tick (moveHead), the tail (point 14) where the picket stands. The rope hangs and drags under
/// -2 a tick with 0.7 of its motion kept, rests 60 u over the ground, and no segment stretches past 120: constraintTail(pos) plants the tail at
/// pos and shortens the segments from the tail to the head, so a taut chain MOVES THE HEAD -- pullTail keeps that head displacement as the owner's
/// mPicketPullDelta, the drag the body feels (control(): mPositionDelta += it). perform(CUE_MOVE): the head at joint 5; the picket staked
/// (mIsNearMario) -> pullTail(the picket's point, which is the tail itself: planted where it lies), then if the head sits over 650 u from the
/// body's centre (the radius 500 + HeadGap 150) the body is yanked 20 u toward it and flagged (mIsBurnt: this tick's yank), and the body turns to
/// put its back to the chain root (1.5 x the turn speed a tick). CUE_CALC_ANIM: the points kept out of the body (500 u from joint 1). The nodes:
/// node i's model (bwanwan_chain.bmd, the brk bwanwan_chain.brk) at point i + 30 up, facing point i + 1, the odd links turned 90 degrees about
/// the chain (calcMatrix: X = side / Y = normal, or the other way); node 14 draws nothing (the picket sits there). Each node has a TEMPERATURE:
/// node 0's is the body's hit points / max (written at 0 and at the max), the others follow their neighbour toward the body 0.02 a tick (0.005
/// within 0.1 -- the decomp's comparisons read inverted; the intent, a heat that flows down the chain, is what is ported); the brk frame = the
/// last frame x the temperature x a reach: nodes 0-4 i x 1/4 + hp / max, the last ten (15 - i) / 10, else 1 -- the chain glows by the body and
/// is cold by the stake. The hit: ACTOR_TYPE_BOSS_UNKC, the attack cylinder ChainHitRadius x 2 by ChainHitHeight x 2, the receive the plain
/// 100 x 100; Mario in a hot node's list (hp != 0, node < 8) -> BURN; Mario's checkCollision keeps him out (keepDistance); the last two nodes
/// have no collision. TBWPicket: ACTOR_TYPE_BOSS_WANWAN, PicketRadius 100 x PicketHeight 100 at the rope's tail, bwanwan_picket.bmd standing
/// upright there facing along the chain's last two segments, 70 u higher while the stake is pulled, set back 60 u along the chain (50 held).
/// receiveMessage from Mario: HIP_DROP -> the Chomp staked again (mIsNearMario = 1, the shake timer 0, the LOCK sound); TAKE -> releasePicket
/// (the smoke at the stake while it was staked), unk194 = 0 (pulled once), mIsNearMario = 0 (loose), the holder; THROWN / DETACH -> the holder
/// gone. moveRequest (his pulling(), each frame): in JumpToBath / Die or with hit points left -> FALSE -- the tail is then a free rope point
/// the chain drags along, and the picket's perform puts HIM at it (mHolder->moveRequest(the tail)): grab a hot Chomp's stake and it drags you
/// about; else pullTail(his hand) -> TRUE: a cooled Chomp is dragged by him. perform: the position = the tail; held -> the taking matrix from
/// (the holder - the head) with the up axis. Mario: hitNoKeepPull (MarioCheckCol.cpp 414) -> PULLING with pullparambgfirewanwanbosstail.prm
/// (V 0.25, H 0.07, oil 0.1 / 0.01).
///
/// THE PORT. A sibling node of the body (made at its Init, Setup on the first MoveObjectPre, freed with it), stepped by the body after its own move
/// each tick: <see cref="MoveHead"/> (the rope's own motion: every point but the head takes its verlet step, the ground by a ray on the body's
/// mask, the segments held from the head), then <see cref="PullTail"/> when something plants the tail (the stake while staked, his hand while
/// he drags a cooled one), <see cref="KeepOutOfBody"/>, the temperatures, the links placed with their own copies of BossWanwanChain.tres (c1 /
/// c2 from the chain brk's keys at the link's frame), the picket (<see cref="SmsBossWanwanPicket"/>, a child: the sms_pullable handle). The
/// hits are the body's HitCheckMario (the nodes' cylinders against Mario, the burn and the push). Units: game units throughout.
/// </summary>
public partial class SmsBossWanwanLeash : Node3D
{
    public const int NodeCount = 15;
    private const float U = SmsUnits.UnitsPerMetre;
    private SmsBossWanwan _owner;
    private readonly Vector3[] _pos = new Vector3[NodeCount], _prev = new Vector3[NodeCount];
    private readonly float[] _temp = new float[NodeCount];
    private readonly Node3D[] _links = new Node3D[NodeCount];
    private readonly ShaderMaterial[] _mats = new ShaderMaterial[NodeCount];
    private bool _ready;
    public float NodeLen = 120f, GroundRadius = 60f, Damping = 0.7f, GravityY = -2f;
    /// <summary>Where the stake is planted (the tail is put back there every tick while the Chomp is staked), units.</summary>
    public Vector3 StakeUnits;
    public SmsBossWanwanPicket Picket { get; private set; }
    public bool IsSetUp => _ready;
    public Vector3 NodeUnits(int i) => _pos[i];
    public float Temperature(int i) => _temp[i];
    public Vector3 HeadUnits => _pos[0];
    public Vector3 TailUnits => _pos[NodeCount - 1];
    public float StraightLengthUnits => (_pos[NodeCount - 1] - _pos[0]).Length();
    public float MaxLengthUnits => NodeLen * (NodeCount - 1);
    public int Pulls { get; private set; }
    public float LastPullUnits { get; private set; }
    /// <summary>The chain's length along its points (the sum of the segments).</summary>
    public float Length() { float t = 0f; for (int i = 0; i < NodeCount - 1; i++) t += (_pos[i + 1] - _pos[i]).Length(); return t; }

    /// <summary>init: the fifteen points at the body (TRope(count, the position, ...)), the stake at its point, the links and the picket's model.</summary>
    public void Setup(SmsBossWanwan owner, Vector3 headU, Vector3 stakeU, PackedScene linkScene, ShaderMaterial linkMat, PackedScene picketScene, ShaderMaterial picketMat)
    {
        _owner = owner;
        for (int i = 0; i < NodeCount; i++) { _pos[i] = headU; _prev[i] = headU; _temp[i] = 0f; }
        for (int i = 0; i < NodeCount - 1; i++)
        {
            Node3D link = linkScene != null ? linkScene.Instantiate<Node3D>() : new Node3D();
            link.Name = $"Link{i}"; link.TopLevel = true;
            AddChild(link);
            _links[i] = link;
            foreach (Node n in link.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (linkMat == null) continue;
                var own = _mats[i] ??= (ShaderMaterial)linkMat.Duplicate();
                for (int k = 0; k < mi.Mesh.GetSurfaceCount(); k++) mi.SetSurfaceOverrideMaterial(k, own);
            }
            link.Scale = Vector3.One / U;
        }
        Picket = new SmsBossWanwanPicket { Name = "Picket" };
        AddChild(Picket);
        Picket.Setup(owner, this, picketScene, picketMat);
        StakeUnits = stakeU;
        LayStraight(stakeU);
        PlaceLinks();
        _ready = true;
    }

    /// <summary>The rope laid out straight from the head toward a point (the stake at a reset), its segments held.</summary>
    public void LayStraight(Vector3 tailU)
    {
        Vector3 head = _pos[0];
        for (int i = 1; i < NodeCount; i++) { _pos[i] = head + (tailU - head) * (i / (float)(NodeCount - 1)); _prev[i] = _pos[i]; }
        ConstrainFromHead();
    }

    // ------------------------------------------------------------------------------------------------------------------ TRope
    /// <summary>moveHead: the head at the joint, then the rope's own step -- every other point falls under GravityY with Damping of its motion
    /// kept and rests GroundRadius over the ground; every segment is held to NodeLen from the head down (a taut chain drags its tail).</summary>
    public void MoveHead(Vector3 headU)
    {
        _prev[0] = _pos[0] = headU;
        float ceil = headU.Y + 10f;                                                      // the chain hangs at or below where it joins the body's back: it never rises above the head (a jerk of the head can otherwise fling a free node up -- an instability the game's ground-bound rope never shows)
        for (int i = 1; i < NodeCount; i++)
        {
            Vector3 vel = (_pos[i] - _prev[i]) * Damping + new Vector3(0f, GravityY, 0f);
            if (vel.Y > 20f) vel.Y = 20f; if (vel.Y < -40f) vel.Y = -40f;                 // the per-tick vertical step is bounded (no spring blow-up)
            _prev[i] = _pos[i];
            Vector3 next = _pos[i] + vel;
            if (next.Y > ceil) { next.Y = ceil; _prev[i].Y = next.Y; }
            if (GroundAt(next, out float g) && next.Y < g + GroundRadius) { next.Y = g + GroundRadius; _prev[i].Y = next.Y; }
            _pos[i] = next;
        }
        ConstrainFromHead();
    }
    private void ConstrainFromHead()
    {
        for (int i = 1; i < NodeCount; i++)
        {
            Vector3 d = _pos[i] - _pos[i - 1]; float l = d.Length();
            if (l > NodeLen) _pos[i] = _pos[i - 1] + d / l * NodeLen;
        }
    }
    /// <summary>constraintTail: the tail planted at pos, the segments shortened from the tail to the head (the head moves when the chain is taut).</summary>
    private void ConstrainFromTail(Vector3 tailU)
    {
        _pos[NodeCount - 1] = tailU; _prev[NodeCount - 1] = tailU;
        for (int i = NodeCount - 2; i >= 0; i--)
        {
            Vector3 d = _pos[i] - _pos[i + 1]; float l = d.Length();
            if (l > NodeLen) _pos[i] = _pos[i + 1] + d / l * NodeLen;
        }
    }
    /// <summary>pullTail: the tail planted at pos, the chain shortened toward the head; the head's displacement (mPicketPullDelta) returned and
    /// handed to the owner (control() adds it to the body's movement this tick).</summary>
    public Vector3 PullTail(Vector3 tailU)
    {
        Vector3 before = _pos[0];
        ConstrainFromTail(tailU);
        Vector3 delta = _pos[0] - before;
        if (delta.LengthSquared() > 1e-6f) { Pulls++; LastPullUnits = delta.Length(); }
        _owner?.AddPullDelta(delta);
        return delta;
    }
    /// <summary>CUE_CALC_ANIM: a point within 500 u of the body's centre is pushed out to 500 and stilled.</summary>
    public void KeepOutOfBody(Vector3 centerU)
    {
        for (int i = 0; i < NodeCount; i++)
        {
            Vector3 d = _pos[i] - centerU; float l = d.Length();
            if (l > 0.001f && l < 500f) { _pos[i] = centerU + d * (500f / l); _prev[i] = _pos[i]; }
        }
    }
    private bool GroundAt(Vector3 pU, out float groundU)
    {
        groundU = float.NegativeInfinity;
        if (_owner == null || !IsInstanceValid(_owner) || !IsInsideTree()) return false;
        Vector3 from = (pU + Vector3.Up * 200f) / U;
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * (1200f / U), _owner.CollisionMask);
        q.CollideWithAreas = false;
        var excl = new Godot.Collections.Array<Rid> { _owner.GetRid() };
        if (_owner.BodySphere != null) excl.Add(_owner.BodySphere.GetRid());
        q.Exclude = excl;
        var r = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (r.Count == 0) return false;
        groundU = ((Vector3)r["position"]).Y * U;
        return true;
    }

    // ------------------------------------------------------------------------------------------------------------------ the nodes
    /// <summary>calcTemperature over the chain: node 0 from the body (at 0 and at full), the rest toward their inner neighbour.</summary>
    public void TemperatureTick(float hpRatio, int hp, int hpMax)
    {
        if (hp == 0 || hp == hpMax) _temp[0] = hpRatio;
        for (int i = 1; i < NodeCount; i++)
        {
            float delta = _temp[i - 1] - _temp[i];
            float step = MathF.Abs(delta) < 0.1f ? 0.005f : 0.02f;
            if (delta > 0f) _temp[i] = MathF.Min(_temp[i] + step, _temp[i - 1]);
            else if (delta < 0f) _temp[i] = MathF.Max(_temp[i] - step, _temp[i - 1]);
            _temp[i] = Mathf.Clamp(_temp[i], 0f, 1f);
        }
    }
    /// <summary>The brk frame of node i (0..299): the temperature x the reach.</summary>
    public float NodeFrame(int i, float hpRatio)
    {
        float progress = i < 5 ? i * 0.25f + hpRatio : (i >= NodeCount - 10 ? (NodeCount - i) / 10f : 1f);
        return 299f * _temp[i] * Mathf.Clamp(progress, 0f, 1f);
    }
    /// <summary>The links' colours from the chain brk at each node's frame (c1 = colour id 1, c2 = id 2).</summary>
    public void ApplyColors(float hpRatio)
    {
        for (int i = 0; i < NodeCount - 1; i++)
        {
            if (_mats[i] == null) continue;
            float f = NodeFrame(i, hpRatio);
            _mats[i].SetShaderParameter("c1", SmsBossWanwan.BrkBodyC1(f));
            _mats[i].SetShaderParameter("c2", SmsBossWanwan.BrkBodyC2(f));
        }
    }
    /// <summary>The gold brk on the links after the bath (bwanwan_chain_gold.brk: the same keys as the body's).</summary>
    public void ApplyGold(float goldFrame)
    {
        for (int i = 0; i < NodeCount - 1; i++)
        {
            if (_mats[i] == null) continue;
            _mats[i].SetShaderParameter("c1", SmsBossWanwan.BrkGoldC1(goldFrame));
            _mats[i].SetShaderParameter("c2", SmsBossWanwan.BrkGoldC2(goldFrame));
        }
    }
    /// <summary>calcMatrix for every link: at point i + 30 up, Z toward the next point, the odd links a quarter turn about the chain.</summary>
    public void PlaceLinks()
    {
        for (int i = 0; i < NodeCount - 1; i++)
        {
            Node3D link = _links[i];
            if (link == null) continue;
            Vector3 dir = _pos[i + 1] - _pos[i];
            if (dir.LengthSquared() < 1e-8f) dir = Vector3.Back; else dir = dir.Normalized();
            Vector3 side = Vector3.Up.Cross(dir);
            if (side.LengthSquared() < 1e-6f) side = Vector3.Right; else side = side.Normalized();
            Vector3 n = dir.Cross(side).Normalized();
            Basis b = (i & 1) == 1 ? new Basis(side, n, dir) : new Basis(n, side, dir);
            link.GlobalTransform = new Transform3D(b * Basis.FromScale(Vector3.One / U), (_pos[i] + Vector3.Up * 30f) / U);
        }
        Picket?.Place();
    }
    public void ShowLinks(bool on) { foreach (var l in _links) if (l != null) l.Visible = on; if (Picket != null) Picket.Visible = on; }
}

/// <summary>
/// The stake at the chain's end (TBWPicket : TTakeActor): an <see cref="SmsPullHandle"/> Mario grabs with B (hitNoKeepPull -> PULLING) and
/// drags the chain by; a ground pound on it stakes the Chomp again. See <see cref="SmsBossWanwanLeash"/>.
/// </summary>
public partial class SmsBossWanwanPicket : SmsPullHandle
{
    private const float U = SmsUnits.UnitsPerMetre;
    private SmsBossWanwan _owner; private SmsBossWanwanLeash _leash; private Node3D _model;
    private Transform3D _taking = Transform3D.Identity;
    /// <summary>onHitFilter(1): no grab, no stake (the bath).</summary>
    public bool Disabled;
    public int HipDrops { get; private set; }
    public int Drags { get; private set; }

    public override float HitRadiusUnits => SmsBossWanwan.Prm.PicketRadius;
    public override float HitHeightUnits => SmsBossWanwan.Prm.PicketHeight;
    public override bool CanBePulled => !Disabled && _owner != null && IsInstanceValid(_owner) && !_owner.IsDead;
    public override Transform3D TakingTransform => _taking;
    public Vector3 PointUnits => _leash != null ? _leash.TailUnits : GlobalPosition * U;

    public void Setup(SmsBossWanwan owner, SmsBossWanwanLeash leash, PackedScene scene, ShaderMaterial mat)
    {
        _owner = owner; _leash = leash;
        PullRateV = 0.25f; PullRateH = 0.07f; OilPullRateV = 0.1f; OilPullRateH = 0.01f;          // pullparambgfirewanwanbosstail.prm
        _model = scene != null ? scene.Instantiate<Node3D>() : new Node3D();
        _model.Name = "PicketModel"; _model.TopLevel = true;
        AddChild(_model);
        if (mat != null)
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                for (int k = 0; k < mi.Mesh.GetSurfaceCount(); k++) mi.SetSurfaceOverrideMaterial(k, mat);
            }
    }
    /// <summary>receiveMessage(TAKE): the owner's releasePicket (the smoke if it was staked), pulled once, loose, the holder.</summary>
    public override void BehaveTaken(Mario holder)
    {
        base.BehaveTaken(holder);
        _owner.OnPicketTaken();
    }
    /// <summary>THROWN / DETACH: the holder gone.</summary>
    public override void BehaveApart()
    {
        base.BehaveApart();
        _owner.OnPicketReleased();
    }
    /// <summary>moveRequest: in JumpToBath / Die or with hit points left -> false (he stands holding a stake that will not come -- and the chain
    /// takes him where it goes); else pullTail(his hand): the cooled Chomp is dragged.</summary>
    public override bool MoveRequest(Vector3 posM)
    {
        if (_owner == null || !IsInstanceValid(_owner) || _owner.IsJumpingToBath || _owner.IsDefeat || _owner.Hp != 0) return false;
        _leash.PullTail(posM * U);
        Drags++;
        return true;
    }
    /// <summary>receiveMessage(HIP_DROP) from Mario: staked again where the tail lies.</summary>
    public void OnHipDrop() { HipDrops++; _owner.OnPicketHipDrop(); }
    /// <summary>perform(CUE_MOVE): the hit point at the tail; held: the taking matrix from (the holder - the head) with the up axis, and the holder put
    /// at the tail (TMario::moveRequest) -- the drag of a hot Chomp.</summary>
    public void Step()
    {
        Vector3 tail = _leash.TailUnits;
        GlobalPosition = tail / U;
        if (IsHeld && Holder.SmsPullingHandle != this) BehaveApart();                        // ensureTakeSituation: his hands are not on it any more (a hit, a fall)
        if (IsHeld)
        {
            Vector3 z = Holder.GlobalPosition * U - _leash.HeadUnits; z.Y = 0f;
            if (z.LengthSquared() < 1e-6f) z = Vector3.Back; else z = z.Normalized();
            Vector3 x = Vector3.Up.Cross(z).Normalized();
            _taking = new Transform3D(new Basis(x, Vector3.Up, z), tail / U);
            if (_owner.Hp != 0) Holder.SmsPullMoveRequest(tail / U);                            // a hot one drags him by the stake he holds
        }
    }
    /// <summary>CUE_CALC_ANIM: the stake upright at the tail, facing along the chain's last two segments, 70 u up while pulled out, set back 60 u
    /// along the chain (50 held).</summary>
    public void Place()
    {
        if (_model == null || _leash == null) return;
        Vector3 tail = _leash.TailUnits;
        Vector3 dir = tail - _leash.NodeUnits(SmsBossWanwanLeash.NodeCount - 3); dir.Y = 0f;
        if (dir.LengthSquared() < 1e-6f) dir = Vector3.Back; else dir = dir.Normalized();
        Vector3 side = Vector3.Up.Cross(dir).Normalized();
        Vector3 at = tail;
        if (_owner != null && !_owner.Staked) at.Y += 70f;
        at -= dir * (IsHeld ? 50f : 60f);
        _model.GlobalTransform = new Transform3D(new Basis(side, Vector3.Up, dir) * Basis.FromScale(Vector3.One / U), at / U);
    }
}
