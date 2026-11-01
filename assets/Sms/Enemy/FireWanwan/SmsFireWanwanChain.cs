using Godot;
using System;

/// <summary>
/// SMS pass 149 -- the Chain Chomplet's chain (TFireWanwanTailHit : TTakeActor with its TTailRubber and five TFireWanwanTailNode links,
/// Enemy/fireWanwan.cpp 80-745): the rope of five nodes from the body's tail joint to the ring Mario grabs, the grab point itself, and the chain's
/// fire (the Chomplet's heat burns at the end ring).
///
/// THE GAME. TTailRubber(5): nodes (pos, vel); the head fixed, the tail free (held: fixed), bound rate 0.25 / decay 0.5 (the prm's while it lives),
/// the max length 10000 (300 while the body flies). movement: adjust (each node's vel += (prev - it) x rate + (next - it) x rate, x decay -- the end
/// nodes against a copy of themselves, so one neighbour each), restrict (the horizontal length over the max -> from the tail (held) or the head
/// (free), each segment longer than the average shortened to it), bind (the inner nodes: the ground under next + 40 (+ 21: a node rests 21 u up),
/// at or under it the vel zeroed and y set to it; a one-wall XZ push of 20; vel = next - pos), then pos += vel for the inner nodes and the free ends.
/// TailHit: ACTOR_TYPE_FIRE_WANWAN_TAIL_HIT, the hit cylinder 30 x 200 -> TailEndColRange 150 x 200 every tick, no attack, in the enemy group; init:
/// the rubber reset at the tail joint (every node there), five wanwanTail.bmd models (the fifth, the ring, at twice the body scale), all five
/// carrying the body's GX_TEVREG0 colour (the heat lerp). perform: the damage radius; CUE_MOVE -> movementBody (the rates by the host: HUNGTAIL and
/// not taken and not ready to fly -> RubberBoundRateHitting / RubberDecayHitting (the snap back after the release), ATTACK -> 0.7 / 0.4, else the
/// prm's; the max length TailMaxLength flying else 10000; the head = the tail joint; movement); performNodes (link i at node i facing node i + 1
/// (the last along +Z), its tail_null1 joint set at node i + 1: the bar between the links stretches to the next); the hit point = the ring - the
/// height / 2; on fire -> MOE_FIRE_A/B/C/D at the ring, scaled by the body scale x hp / max (the flame shrinks as the hose cools it) + the flame
/// sound; held: the taking matrix from (the ring - the body) with the up axis, bindBody (the hit point += the tail node's vel x TailEndPowRate,
/// XZ; the holder moved there with his own y: the chain's spring drags him), the pull sound while the length grows. receiveMessage from Mario:
/// TAKE -> canTakenByMario (the body cooled: ESCAPE or FREEZE) -> behaveTaken (the holder, the tail fixed, the hold sound, the lengths);
/// THROWN / DETACH -> behaveApart (the tail free, the thrown sound, the throw power = (TailApartPowRate x the length / FlySpeed) clamped 0..1,
/// squared, x FlySpeed; the holder null). moveRequest (his pulling(), each frame): the ground under + 100 -> at or under it y = ground + 1; + 50;
/// a one-wall push of 70; the tail node, the hit point and the taking matrix there; true.
///
/// THE PORT. An <see cref="SmsPullHandle"/> (Mario's PULLING through MarioPull.cs) owned by an <see cref="SmsFireWanwan"/>, which steps it after its
/// own move each tick (<see cref="Step"/>) with the tail joint's world point; the five links are wanwantail.glb instances under it (TopLevel), the
/// bar joint set by SetBoneGlobalPose; the ground for the nodes is a ray (the body's collision mask), the walls are not read (DEVIATION). The
/// node radius and the clipping are not ported. The fire is the FireFxScene (the Stu's flames) under the ring, its emitters on while hot, scaled by
/// hp / max; the ring's colour follows the body's (<see cref="SetColor"/>). The node positions and velocities are game units.
/// </summary>
public partial class SmsFireWanwanChain : SmsPullHandle
{
    public const int NodeCount = 5;
    private const float U = SmsUnits.UnitsPerMetre;
    private static readonly string LinkPath = "res://models/Eniemes/FireWanwan/wanwantail.glb";

    private SmsFireWanwan _owner;
    private readonly Vector3[] _pos = new Vector3[NodeCount], _vel = new Vector3[NodeCount];
    public bool FixHead = true, FixTail = false;
    public float BoundRate = 0.25f, Decay = 0.5f, MaxLength = 10000f;
    private readonly Node3D[] _links = new Node3D[NodeCount];
    private readonly Skeleton3D[] _skels = new Skeleton3D[NodeCount];
    private readonly int[] _barBone = new int[NodeCount], _nearBone = new int[NodeCount];
    private readonly ShaderMaterial[] _mats = new ShaderMaterial[NodeCount];
    private Node3D _fire; private bool _fireOn;
    private Transform3D _taking = Transform3D.Identity;
    private float _hitRadius = 150f;
    /// <summary>mIsOnFire: the Chomplet's heat (the ring burns, the body burns Mario, the goop is stamped).</summary>
    public bool IsOnFire { get; set; }
    public float CurTailLength { get; private set; }
    public float PrevTailLength { get; private set; }
    /// <summary>mThrowPow: the fly speed the last release earned (calcApartPow).</summary>
    public float ThrowPow { get; private set; }
    public int Pulls { get; private set; }
    public float RingScale { get; private set; } = 1f;

    public override float HitRadiusUnits => _hitRadius;
    public override float HitHeightUnits => 200f;
    public override bool CanBePulled => _owner != null && IsInstanceValid(_owner) && _owner.CanTakenByMario;
    public override Transform3D TakingTransform => _taking;
    public Vector3 NodeUnits(int i) => _pos[i];
    public Vector3 NodeVelUnits(int i) => _vel[i];
    public Vector3 RingWorld => _pos[NodeCount - 1] / U;
    public bool IsTaken => IsHeld;

    /// <summary>init: the rubber at the tail joint, the five links, the grab cylinder, the pull params (pullparamfirewanwantail.prm).</summary>
    public void Setup(SmsFireWanwan owner, Vector3 tailJointUnits, float bodyScale, ShaderMaterial linkMaterial)
    {
        _owner = owner;
        PullRateV = 0.3f; PullRateH = 0.2f; OilPullRateV = 0.1f; OilPullRateH = 0.01f;
        ResetRubber(tailJointUnits, tailJointUnits);
        FixHead = true; FixTail = false;
        var scene = ResourceLoader.Exists(LinkPath) ? GD.Load<PackedScene>(LinkPath) : null;
        for (int i = 0; i < NodeCount; i++)
        {
            Node3D link = scene != null ? scene.Instantiate<Node3D>() : new Node3D();
            link.Name = $"Link{i}"; link.TopLevel = true;
            AddChild(link);
            _links[i] = link;
            float s = bodyScale * (i == NodeCount - 1 ? 2f : 1f);                     // the ring is the chain's end: twice the body scale
            RingScale = bodyScale * 2f;
            foreach (Node n in link.FindChildren("*", "Skeleton3D", true, false)) { _skels[i] = n as Skeleton3D; break; }
            if (_skels[i] != null)
            {
                _barBone[i] = _skels[i].FindBone("tail_null1"); _nearBone[i] = _skels[i].FindBone("tail_null2");
                if (_nearBone[i] >= 0) _skels[i].SetBoneGlobalPose(_nearBone[i], new Transform3D(Basis.Identity, new Vector3(0f, 0f, -10f)));   // the bar's near end at its game rest
                if (_barBone[i] >= 0) _skels[i].SetBoneGlobalPose(_barBone[i], new Transform3D(Basis.Identity, new Vector3(0f, 0f, 10f)));
            }
            foreach (Node n in link.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (linkMaterial != null)
                {
                    var own = _mats[i] ??= (ShaderMaterial)linkMaterial.Duplicate();
                    for (int k = 0; k < mi.Mesh.GetSurfaceCount(); k++) mi.SetSurfaceOverrideMaterial(k, own);
                }
            }
            link.Scale = Vector3.One * (s / U);
        }
        PlaceLinks();
    }

    /// <summary>TTailRubber::reset: the head and tail set, the inner nodes spread evenly between them, still.</summary>
    public void ResetRubber(Vector3 headU, Vector3 tailU)
    {
        _pos[0] = headU; _vel[0] = Vector3.Zero;
        _pos[NodeCount - 1] = tailU; _vel[NodeCount - 1] = Vector3.Zero;
        for (int i = 1; i < NodeCount - 1; i++) { _pos[i] = headU + (tailU - headU) * (i / (float)NodeCount); _vel[i] = Vector3.Zero; }
        CurTailLength = PrevTailLength = 0f;
    }

    /// <summary>The chain's horizontal length (getLength): the XZ segments summed.</summary>
    public float Length()
    {
        float t = 0f;
        for (int i = 0; i < NodeCount - 1; i++) { Vector3 d = _pos[i] - _pos[i + 1]; d.Y = 0f; t += d.Length(); }
        return t;
    }

    private void AdjustOne(int i, Vector3 prevPos, Vector3 nextPos)
    {
        Vector3 v = _vel[i];
        v += (prevPos - _pos[i]) * BoundRate;
        v += (nextPos - _pos[i]) * BoundRate;
        _vel[i] = v * Decay;
    }
    private void Adjust()
    {
        for (int i = 1; i < NodeCount - 1; i++) AdjustOne(i, _pos[i - 1], _pos[i + 1]);
        AdjustOne(0, _pos[0], _pos[1]);                                               // the ends against a copy of themselves
        AdjustOne(NodeCount - 1, _pos[NodeCount - 2], _pos[NodeCount - 1]);
    }
    private void Restrict()
    {
        float len = Length();
        if (len <= MaxLength) return;
        float avg = len / (NodeCount - 1);
        if (FixTail)
        {
            for (int i = NodeCount - 2; i >= 0; i--)
            {
                Vector3 d = _pos[i + 1] - _pos[i]; d.Y = 0f;
                float l = d.Length();
                if (avg < l) _pos[i] += d / l * (l - avg);
            }
        }
        else
        {
            for (int i = 1; i < NodeCount; i++)
            {
                Vector3 d = _pos[i - 1] - _pos[i]; d.Y = 0f;
                float l = d.Length();
                if (avg < l) _pos[i] += d / l * (l - avg);
            }
        }
    }
    /// <summary>bindOne: the ground under next + 40 u; at or under ground + 21 the vel zeroed and y set there (the node rests 21 u up); vel = next - pos.</summary>
    private void BindOne(int i)
    {
        Vector3 next = _pos[i] + _vel[i];
        if (GroundAt(next, out float groundU))
        {
            float g = groundU + 1f + 20f;
            if (next.Y <= g + 0.05f) { _vel[i] = Vector3.Zero; next.Y = g; }
        }
        _vel[i] = next - _pos[i];
    }
    private bool GroundAt(Vector3 pU, out float groundU)
    {
        groundU = float.NegativeInfinity;
        if (_owner == null || !IsInstanceValid(_owner) || !IsInsideTree()) return false;
        Vector3 from = (pU + Vector3.Up * 40f) / U;
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * (400f / U), _owner.CollisionMask);
        q.CollideWithAreas = false;
        q.Exclude = new Godot.Collections.Array<Rid> { _owner.GetRid() };
        var r = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (r.Count == 0) return false;
        groundU = ((Vector3)r["position"]).Y * U;
        return true;
    }
    /// <summary>TTailRubber::movement: adjust, restrict, bind, the inner nodes (and the free ends) moved by their vel.</summary>
    private void Movement()
    {
        Adjust();
        Restrict();
        for (int i = 1; i < NodeCount - 1; i++) BindOne(i);
        for (int i = 1; i < NodeCount - 1; i++) _pos[i] += _vel[i];
        if (!FixHead) _pos[0] += _vel[0];
        if (!FixTail) _pos[NodeCount - 1] += _vel[NodeCount - 1];
    }

    /// <summary>The TailHit's perform(CUE_MOVE) + the held part, one game tick, from the owner after its move: the rates by the owner's state, the head at
    /// the tail joint, the rope stepped; held: the taking matrix, the drag on the holder (bindBody), the pull sound.</summary>
    public void Step(Vector3 tailJointU)
    {
        var p = SmsFireWanwan.Prm;
        _hitRadius = p.TailEndColRange;
        if (_owner.IsHungTailNerve && !IsHeld && !_owner.IsReadyToFly) { BoundRate = p.RubberBoundRateHitting; Decay = p.RubberDecayHitting; }
        else if (_owner.IsAttacking) { BoundRate = 0.7f; Decay = 0.4f; }
        else { BoundRate = p.RubberBoundRate; Decay = p.RubberDecay; }
        MaxLength = _owner.IsFlying ? p.TailMaxLength : 10000f;
        _pos[0] = tailJointU;
        Movement();
        if (IsHeld)
        {
            Vector3 toRing = _pos[NodeCount - 1] - _owner.GlobalPosition * U;
            SetTaking(toRing, _pos[NodeCount - 1]);
            // bindBody: the hit point += the tail node's vel x TailEndPowRate (XZ); the holder moved there with his own y. The game applies it once a
            // 30 fps frame against his pull of that frame; the port steps the chain SmsUnits.TicksPerFrame times a frame, so each step drags a quarter
            // (run a: at the full rate the spring out-pulled his stick 4:1 and the chain settled at 130 u instead of the game's ~520)
            Vector3 pow = _vel[NodeCount - 1] * (p.TailEndPowRate / SmsUnits.TicksPerFrame); pow.Y = 0f;
            Vector3 hit = _pos[NodeCount - 1] + pow;
            _pos[NodeCount - 1] = hit;
            Holder.SmsPullMoveRequest(hit / U);
            _taking.Origin = hit / U;
            PrevTailLength = CurTailLength; CurTailLength = Length();
            if (CurTailLength - PrevTailLength > 0f) { Pulls++; PullSound?.Play(); }
        }
        GlobalPosition = (_pos[NodeCount - 1] - Vector3.Up * 100f) / U;              // the hit actor: the ring - the height / 2
    }
    private void SetTaking(Vector3 zU, Vector3 originU)
    {
        Vector3 z = zU; z.Y = 0f;
        if (z.LengthSquared() < 1e-6f) z = Vector3.Back; else z = z.Normalized();
        Vector3 x = Vector3.Up.Cross(z).Normalized();
        _taking = new Transform3D(new Basis(x, Vector3.Up, z), originU / U);
    }

    /// <summary>performNodes: link i at node i, facing node i + 1 (the last along +Z); the bar joint at the next node.</summary>
    public void PlaceLinks()
    {
        for (int i = 0; i < NodeCount; i++)
        {
            Node3D link = _links[i];
            if (link == null) continue;
            Vector3 at = _pos[i] / U;
            Vector3 dir = i == NodeCount - 1 ? Vector3.Back : (_pos[i + 1] - _pos[i]);
            dir.Y = i == NodeCount - 1 ? 0f : dir.Y;
            if (dir.LengthSquared() < 1e-8f) dir = Vector3.Back; else dir = dir.Normalized();
            Vector3 x = Vector3.Up.Cross(dir);
            if (x.LengthSquared() < 1e-6f) x = Vector3.Right; else x = x.Normalized();
            Vector3 y = dir.Cross(x).Normalized();
            float s = link.Scale.X;
            link.GlobalTransform = new Transform3D(new Basis(x, y, dir) * Basis.FromScale(Vector3.One * s), at);
            if (i < NodeCount - 1 && _skels[i] != null && _barBone[i] >= 0)
            {
                Vector3 local = _skels[i].GlobalTransform.AffineInverse() * (_pos[i + 1] / U);
                _skels[i].SetBoneGlobalPose(_barBone[i], new Transform3D(Basis.Identity, local));
            }
        }
        if (_fire != null && IsInstanceValid(_fire) && _links[NodeCount - 1] != null)
            _fire.GlobalPosition = _pos[NodeCount - 1] / U;
    }

    // ------------------------------------------------------------------------------------------------------------------ the messages
    /// <summary>receiveMessage(TAKE) from Mario: behaveTaken -- the holder, the tail fixed, the hold sound, the lengths, the tail node at his point.</summary>
    public override void BehaveTaken(Mario holder)
    {
        base.BehaveTaken(holder);
        FixTail = true;
        HoldSound?.Play();
        CurTailLength = Length(); PrevTailLength = CurTailLength;
        MoveRequest(holder.GlobalPosition);
        if (_owner.DebugPrint) GD.Print($"[SMS wanwan chain] {_owner.Name}: taken by {holder.Name}, the chain {CurTailLength:F0} u");
    }
    /// <summary>THROWN / DETACH: behaveApart -- the tail free, the thrown sound, the throw power (calcApartPow), the holder gone.</summary>
    public override void BehaveApart()
    {
        FixTail = false;
        _owner.StartThrownSound();
        ThrowPow = CalcApartPow();
        CurTailLength = 0f; PrevTailLength = 0f;
        if (_owner.DebugPrint) GD.Print($"[SMS wanwan chain] {_owner.Name}: let go at {Length():F0} u: the throw power {ThrowPow:F1} u a tick");
        base.BehaveApart();
    }
    /// <summary>calcApartPow: (TailApartPowRate x the length / FlySpeed) clamped 0..1, squared, x FlySpeed.</summary>
    public float CalcApartPow()
    {
        var p = SmsFireWanwan.Prm;
        float f = p.TailApartPowRate * Length() / p.FlySpeed;
        f = Mathf.Clamp(f, 0f, 1f);
        return f * f * p.FlySpeed;
    }
    /// <summary>moveRequest: the ground under + 100 -> at or under it y = ground + 1; + 50 up; (the one-wall push of 70 is not read); the tail node,
    /// the hit point and the taking matrix there; true.</summary>
    public override bool MoveRequest(Vector3 posM)
    {
        Vector3 next = posM * U;
        if (GroundAt(next + Vector3.Up * 60f, out float g) && next.Y <= g + 0.05f) next.Y = g + 1f;
        next.Y += 50f;
        _pos[NodeCount - 1] = next;
        GlobalPosition = (next - Vector3.Up * 100f) / U;
        _taking.Origin = next / U;
        return true;
    }

    // ------------------------------------------------------------------------------------------------------------------ the look
    /// <summary>The ring's heat colour (GX_TEVREG0 / 255, S10: the fire's 400 is 1.57) on the five links.</summary>
    public void SetColor(Vector3 c0)
    {
        foreach (var m in _mats) m?.SetShaderParameter("c0", c0);
    }
    [Export] public AudioStreamPlayer3D HoldSound, PullSound, FlameSound;
    /// <summary>onFireEffect: the flames at the ring while hot, scaled by the body scale x hp / max; offFireEffect: the puff when it cools.</summary>
    public void FireEffect(bool on, float scale, PackedScene fireScene, PackedScene offScene)
    {
        if (on)
        {
            if (_fire == null && fireScene != null)
            {
                _fire = fireScene.Instantiate<Node3D>(); _fire.TopLevel = true;
                AddChild(_fire);
            }
            if (_fire != null)
            {
                _fire.GlobalPosition = _pos[NodeCount - 1] / U;
                _fire.Scale = Vector3.One * MathF.Max(scale, 0.05f);
                if (!_fireOn) { SetEmitting(_fire, true); _fireOn = true; FlameSound?.Play(); }
            }
        }
        else if (_fireOn)
        {
            if (_fire != null) SetEmitting(_fire, false);
            _fireOn = false;
            if (offScene != null)
            {
                var off = offScene.Instantiate<Node3D>(); off.TopLevel = true; AddChild(off);
                off.GlobalPosition = _pos[NodeCount - 1] / U; off.Scale = Vector3.One * RingScale * 0.5f;
                SetEmitting(off, true);
                if (off is SmsFxBurst b) b.Play(off.GlobalTransform); else GetTree().CreateTimer(3.0).Timeout += () => { if (IsInstanceValid(off)) off.QueueFree(); };
            }
        }
    }
    public bool FireShown => _fireOn;
    private static void SetEmitting(Node3D fx, bool on)
    {
        foreach (Node n in fx.FindChildren("*", "GPUParticles3D", true, false)) ((GpuParticles3D)n).Emitting = on;
        if (fx is GpuParticles3D g) g.Emitting = on;
    }
    public void ShowLinks(bool on)
    {
        foreach (var l in _links) if (l != null) l.Visible = on;
        if (_fire != null && IsInstanceValid(_fire)) _fire.Visible = on;
    }
}
