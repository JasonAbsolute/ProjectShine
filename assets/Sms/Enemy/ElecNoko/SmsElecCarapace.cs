using Godot;
using System;

/// <summary>
/// SMS pass 148 -- the Electrokoopa's shell (TElecCarapace : TEnemyAttachment, Enemy/elecNokonoko.cpp), the part its Koopa shoots: a body of its own
/// on the small-enemy base, owned and driven by an <see cref="SmsElecNokonoko"/>.
///
/// THE GAME. loadInit: the hit actor ELEC_CARAPACE (attack 80 x 80, receive 60 x 60, the player / enemy / boss lists), the collision on, the state 0
/// (hidden: on the host's back), the spine's default MOVE, the zigzag side random, the head height 80. appear (shoot1 frame 60): state 1 -- the
/// position and scale the host's (the scale from 0.01, x 2 a tick back up: recoverScale), the body radius 50, no collision. shoot (frame 62): the goal
/// = its point + the XZ way to Mario x CarapaceFlyDist 1500, the zigzag side flipped, the spin 0, state 2, the flags cleared, the collision on, the
/// spine reset to MOVE, setZigParameter (the cycle 3..5 x the goal distance, the angle 20..30). bind: control (the spine), then the attachment's: pos +=
/// the delta + the velocity, recoverScale, vy -= CarapaceGravity 0.5 (the terminal mVelocityMinY), the ground (the water surface counts) -> hitGround
/// (a bounce in progress -> now returning; water -> killed; landed, airborne off, the velocity 0), a wall (2 x the body radius) -> hitWall (mReflectSw:
/// the velocity -1.5 x (the delta . the normal) x the normal, vy 3, 2 u up, the goal = the host, straight home, a 5-tick cooldown), setBehavior (the
/// host dead -> killed; landed -> y = the ground), forceKill (off the map or in a pool -> killed). move: the spin + CarapaceSpinSpeed 50 deg a tick;
/// straight (after a wall) walkToCurPathNode(CarapaceSpeed 15, CarapaceTurnSpeed 100), else zigzagToCurPathNode(15, 100, the cycle, the angle). MOVE:
/// move; returning and within 64 x the speed (960 u) of the goal (the host) -> the host made catch-ready (COLLECT) and RETURN after; the goal reached
/// (XZ^2 under 100; the port: 20 u, its frame step being two ticks) without a bounce: returning -> RETURN after, else now returning with the goal = the host. WAIT (after shocking Mario): 60 ticks.
/// RETURN: tick 0 the step = (the host - it) / 64 and the host's catchIn (catch1), the first 20 ticks too; the host dead by thunder (frozen within
/// 200 u) -> MELT_ON_DEATH + killed, TLS_CHANGE; the spin; the position += the step, clamped at the host; the host dead -> its y scale x 0.8 to nothing.
/// sendMessage (the hit lists): Mario -> ELECTRIC_SHOCK and, taken, no collision + WAIT; the host -> the collision back on; water nothing; another actor
/// (mReflectSw) -> reflect: not the same twice, now bouncing, zigzag off, the normal = the dominant axis toward it, the velocity = the way to it x -7 x
/// (the way . the normal) (away from it), vy 2, 2 u up, the zigzag side by the quadrant, the goal = the host. receiveMessage: UNKD -> TLS_CHANGE,
/// killed; TRAMPLE -> Mario ELECTRIC_SHOCK; the hose taken and ignored. calcRootMatrix: the model at the position, the yaw + the spin, the scale;
/// DENNOKO_SPARK2 and DNK_BIRI / SPARK_L / SPARK_R at joint 2 every frame.
///
/// THE PORT. An SmsSmallEnemy with the base's physics (the walk in `_lin`, gravity on `_vel`, MoveAndSlide, the landing, OnWallContact for the bounce,
/// `_touching` for the reflect) and three nerves (Move / Wait / Return); the host sets it up (SetHost), owns its state (<see cref="State"/> 0 hidden /
/// 1 appearing / 2 shot) through Appear / Shoot / KillAttach, and the two bodies ignore each other's colliders (the game has none). The zigzag is the
/// game's walk with a heading offset of angle x sin(the distance left to the goal x 360 / the cycle + the side's 0 / 180). Hidden it moves nothing and shows nothing.
/// DEVIATIONS (logged): the collision comes back on at the next shot, not at a touch of the host; the sparks and the change puff are slots (the host's
/// ShockSparks by default); forceKill's map test is the base's ground ray (nothing under it -> killed); a shock counts as taken when it put Mario in
/// his hurt state.
/// </summary>
public partial class SmsElecCarapace : SmsSmallEnemy
{
    /// <summary>initHitActor(ELEC_CARAPACE, 80, 80, 60, 60): the attack cylinder 80 x 80, the receive 60 x 60, unscaled (the host's 1.5 is the look only);
    /// the head height 80, the body radius 50; the gravity CarapaceGravity.</summary>
    public static readonly Params CPrm = new Params { HeadHeight = 80f, BodyRadius = 50f, WallRadius = 50f, HitPointMax = 1, Gravity = 0.5f, AttackRadius = 80f, AttackHeight = 80f, DamageRadius = 60f, DamageHeight = 60f, BodyScaleLow = 1f, BodyScaleHigh = 1f, TurnSpeedLow = 100f, TurnSpeedHigh = 100f, MarchSpeedLow = 15f, MarchSpeedHigh = 15f };
    protected override Params P => CPrm;
    private static readonly string[] NoNames = Array.Empty<string>();
    protected override string[] BckNames => NoNames;
    protected override int[] BckFrames => Array.Empty<int>();
    protected override bool[] BckLoops => Array.Empty<bool>();

    protected const int NerveMove = NerveSpecies, NerveWaitShock = NerveSpecies + 1, NerveReturn = NerveSpecies + 2;
    protected override string NerveName(int n) => n == NerveMove ? "Move" : n == NerveWaitShock ? "Wait" : n == NerveReturn ? "Return" : base.NerveName(n);
    protected override int DefaultNerve => NerveMove;

    private SmsElecNokonoko _host;
    /// <summary>unk150: 0 on the host's back (hidden), 1 appearing at its point, 2 shot (its own body).</summary>
    public int State { get; private set; }
    private Node3D _lastReflected;                                                           // unk170
    private bool _zigSide, _straight, _bouncing, _landed, _returning;                        // unk174 / unk175 / unk176 / unk168 / unk184
    private float _zigCycle, _zigAngle, _spin;                                               // unk178 / unk17C / unk188
    private int _wallCooldown;                                                               // unk180
    private Vector3 _returnStep;                                                             // unk18C
    private float _hostScale = 1f;                                                           // unk164
    private Node3D _spark;
    private ulong _wallFrame; private KinematicCollision3D _wallCol;
    /// <summary>For the drives.</summary>
    public string CurrentNerveName => NerveName(_cur);
    public bool IsMove => !IsNerve(NerveWaitShock);
    /// <summary>For the drives: the body's own Visible stays on; hidden on the host's back means its model is off.</summary>
    public bool ModelVisible => _model == null || _model.Visible;
    public bool Returning => _returning;
    public bool Bouncing => _bouncing;
    public bool Straight => _straight;
    public float SpinDeg => _spin;
    public Vector3 GoalUnits => Goal();
    public int Shocks { get; private set; }
    public int Reflects { get; private set; }
    public int WallHits { get; private set; }

    protected override void Init()
    {
        _flags150 = 0;
        if (_rng.Next(0, 300) < 150) _zigSide = false; else _zigSide = true;              // unk174 = 1 unless rand(0, 300) < 150
        if (_model != null && _model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") == null)
            _model.AddChild(new AnimationPlayer { Name = "AnimationPlayer" });              // the shell's glb has no clips (the game's koura_model1 has no bck): an empty player keeps the base's model check quiet
    }
    /// <summary>The host wires its shell up: the back-reference, the colliders ignored both ways, the sparks (its SparkFxScene).</summary>
    public void SetHost(SmsElecNokonoko host)
    {
        _host = host;
        AddCollisionExceptionWith(host);
        host.AddCollisionExceptionWith(this);
        if (host.SparkFxScene != null && _spark == null)
        {
            _spark = host.SparkFxScene.Instantiate<Node3D>();
            if (_spark is SmsFollowEffect fe)
            {
                // the game binds DNK_BIRI / SPARK_L / SPARK_R to joint 2 (effnul1); this glb has no clips, so its joints sit at the origin -- the dome's centre is lifted by hand
                fe.BoneName = "effnul1"; fe.OffsetUnits = new Vector3(0f, 40f * host.ScaleNow, 0f);
                AddChild(_spark); fe.Bind(this);
            }
            else AddChild(_spark);
            _spark.Scale = Vector3.One * 0.8f * host.ScaleNow;                                // setGlobalScale(mScaling): sized to the host's 1.5 body
            SetSpark(false);
        }
        HideAttach();
    }
    private void SetSpark(bool on)
    {
        if (_spark is not SmsShockSparks s || s.Active == on) return;
        s.Active = on;
        if (on) { if (s.Streaks != null) s.Streaks.Emitting = false; if (s.Arcs != null) s.Arcs.Emitting = false; }
    }
    protected override void Reset()
    {
        base.Reset();
        _bodyScale = 1f; _scale = 1f;
        HideAttach();
    }
    /// <summary>TEnemyAttachment::kill / rebirth: state 0, no collision -- on the host's back again, out of sight.</summary>
    public void KillAttach()
    {
        State = 0;
        _noBind = true; _vel = Vector3.Zero; _lin = Vector3.Zero; _airborne = false;
        SetCollisionLayerValue(1, false);
        if (_model != null) _model.Visible = false;
        SetSpark(false);
        _stack.Clear(); _cur = NerveNone; _prev = NerveNone; _time = 0; PushAfterCurrent(NerveMove);
    }
    private void HideAttach() => KillAttach();                                              // (not Node3D.Hide: that only drops Visible)
    /// <summary>appear: state 1 at the host's point and yaw, the scale from 0.01 growing back (recoverScale), the body radius 50, no collision.</summary>
    public void Appear()
    {
        if (_host == null || State != 0) return;
        State = 1;
        GlobalPosition = _host.GlobalPosition; _yaw = _host.YawDeg;
        _hostScale = _host.ScaleNow;
        _bodyScale = 1f; _scale = 0.01f;
        _lastReflected = null;
        SetCollisionLayerValue(1, false);
        if (_model != null) _model.Visible = true;
        _noBind = true; _vel = Vector3.Zero;
        ApplyLook();
    }
    /// <summary>shoot: the goal CarapaceFlyDist out along the way to Mario, the side flipped, the spin 0, state 2, the collision on, MOVE, the zigzag set.</summary>
    public void Shoot()
    {
        if (_host == null) return;
        _wallCooldown = 0;
        if (State == 2) return;
        Vector3 dir = MarioPos() - Pos(); dir.Y = 0f;
        if (dir.LengthSquared() < 1e-6f) dir = PolarXZ(_yaw, 1f); else dir = dir.Normalized();
        Vector3 goal = Pos() + dir * SmsElecNokonoko.Prm.CarapaceFlyDist;
        _zigSide = !_zigSide;
        _spin = 0f;
        State = 2;
        _bouncing = false; _landed = false; _returning = false; _straight = false;
        SetCollisionLayerValue(1, true);
        if (_model != null) _model.Visible = true;
        _noBind = false; _airborne = true; _vel = Vector3.Zero;
        _stack.Clear(); _cur = NerveNone; _prev = NerveNone; _time = 0; PushAfterCurrent(NerveMove);
        SetGoalPoint(goal);
        SetZigParameter();
        Shocks = 0; Reflects = 0; WallHits = 0;
        if (DebugPrint) GD.Print($"[SMS carapace] {Name}: shot toward {goal} (zig side {_zigSide})");
    }
    /// <summary>setZigParameter: the cycle = 3..5 x the goal distance, the angle 20..30 deg.</summary>
    private void SetZigParameter()
    {
        _zigCycle = Lerp(3f, 5f, Rand()) * (Goal() - Pos()).Length();
        _zigAngle = Lerp(20f, 30f, Rand());
    }
    protected override float GravityY() => SmsElecNokonoko.Prm.CarapaceGravity;
    protected override bool WallHopAllowed() => false;
    protected override bool IsCollidMove(Node3D other) => false;                               // the reflect is the shell's own; no push
    protected override Vector3 LookScale => Vector3.One * _scale;
    /// <summary>calcRootMatrix: the yaw + the spin at the scale (the host's y scale shrink at its death).</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        Basis b = Basis.FromEuler(new Vector3(0f, Mathf.DegToRad(Wrap360(_yaw + _spin)), 0f)) * Basis.FromScale(new Vector3(_scale, _scale * _yScale, _scale) * _modelScale);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, GlobalPosition); else _model.Transform = new Transform3D(b, Vector3.Zero);
    }
    private float _yScale = 1f;

    protected override void OnWallContact(KinematicCollision3D col) { _wallFrame = Engine.GetPhysicsFrames(); _wallCol = col; }
    protected override void OnLanded() { }
    /// <summary>The attachment's bind, after the spine: recoverScale, the landing (y to the ground, the velocity 0, a bounce -> returning, water -> killed),
    /// the wall (the reflect home), the host dead -> killed, off the map -> killed.</summary>
    protected override void SetBehavior()
    {
        if (State == 0 || _host == null) return;
        if (!IsInstanceValid(_host) || _host.IsDead) { KillAttach(); return; }                            // setBehavior: the host dead -> killed
        if (_scale < _hostScale) _scale = MathF.Min(_scale * 2f, _hostScale); else _scale = _hostScale;    // recoverScale
        if (State == 1) { GlobalPosition = _host.GlobalPosition; _yaw = _host.YawDeg; return; }         // set(): on the host while appearing
        if (IsNerve(NerveReturn)) return;                                                              // RETURN moves it by hand
        if (_wallCooldown != 0) { _wallCooldown++; if (_wallCooldown > 5) _wallCooldown = 0; }
        if (!_airborne)
        {
            if (!_landed)
            {
                if (_bouncing) _returning = true;                                                      // behaveToHitGround
                if (WaterSurfaceAt(GlobalPosition, out float surf, 0.3f) && surf >= GlobalPosition.Y - 0.05f) { Lost(); return; }
                _bouncing = false;
            }
            _landed = true;
            _vel = Vector3.Zero;
        }
        else _landed = false;
        if (Engine.GetPhysicsFrames() - _wallFrame <= 1 && _wallCol != null && _wallCooldown <= 0 && _host.ReflectSw) HitWall(_wallCol.GetNormal());
        foreach (Node3D o in _touching) if (o is SmsSmallEnemy e && e != _host && !e.IsDead && _host.ReflectSw) { Reflect(o); break; }
        if (_groundY <= Pos().Y - 59.5f * U) { Lost(); return; }                                        // forceKill: nothing under it (the base's ray stops at 60 m)
        SetSpark(true);
    }
    /// <summary>killed in water / off the map: hidden; the host's COLLECT gives up after its 800 ticks (REBIRTH).</summary>
    private void Lost() { if (DebugPrint) GD.Print($"[SMS carapace] {Name}: lost (water or off the map)"); KillAttach(); State = 0; }
    /// <summary>behaveToHitWall: the velocity -1.5 x (the step . the normal) x the normal, vy 3, 2 u up, straight home, a 5-tick cooldown.</summary>
    private void HitWall(Vector3 n)
    {
        n.Y = 0f; if (n.LengthSquared() < 1e-6f) return; n = n.Normalized();
        Vector3 step = _lin.LengthSquared() > 0f ? _lin : PolarXZ(_yaw, SmsElecNokonoko.Prm.CarapaceSpeed);
        float speed = step.Dot(n);
        _wallCooldown = 1; _returning = false; _bouncing = true; _straight = true;
        _vel = new Vector3(-1.5f * speed * n.X, 3f, -1.5f * speed * n.Z);
        GlobalPosition += Vector3.Up * (2f / U); _airborne = true;
        SetGoalPoint(_host.GlobalPosition * U);
        WallHits++;
        if (DebugPrint) GD.Print($"[SMS carapace] {Name}: off a wall, home");
    }
    /// <summary>reflect(actor): off another actor (not the same twice): the normal = the dominant axis toward it, the velocity away at 7 x the overlap, vy 2.</summary>
    private void Reflect(Node3D other)
    {
        if (_lastReflected == other) return;
        _returning = false; _lastReflected = other; _bouncing = true; _straight = false;
        Vector3 dir = other.GlobalPosition * U - Pos(); dir.Y = 0f;
        if (dir == Vector3.Zero) dir.X += 1f;
        dir = dir.Normalized();
        Vector3 normal = Vector3.Zero;
        if (MathF.Abs(dir.X) < 1e-6f || MathF.Abs(dir.Z / dir.X) > 1f) normal.Z = other.GlobalPosition.Z > GlobalPosition.Z ? 1f : -1f;
        else normal.X = other.GlobalPosition.X > GlobalPosition.X ? 1f : -1f;
        float speed = -7f * dir.Dot(normal);
        _vel = new Vector3(dir.X * speed, 2f, dir.Z * speed);
        GlobalPosition += Vector3.Up * (2f / U); _airborne = true;
        _zigSide = (_vel.X > 0f && _vel.Z > 0f) || (_vel.X < 0f && _vel.Z < 0f);
        SetGoalPoint(_host.GlobalPosition * U);
        Reflects++;
        if (DebugPrint) GD.Print($"[SMS carapace] {Name}: off {other.Name}, home");
    }
    /// <summary>move: the spin, the straight walk home or the zigzag toward the goal at CarapaceSpeed / CarapaceTurnSpeed. The zigzag is
    /// TSpineEnemy::zigzagToCurPathNode (enemy.cpp): the heading offset = angle x sin(wrap(the distance LEFT to the goal, 0, cycle) x 360 / cycle
    /// + the phase shift: 0 on one side, 180 on the other), so the shell leaves at nearly the full angle and straightens as it closes on its point.</summary>
    private void Move()
    {
        var p = SmsElecNokonoko.Prm;
        float k = _host != null ? _host.SpeedScale : 1f;
        float speed = p.CarapaceSpeed * k, turn = p.CarapaceTurnSpeed * k;
        if (_straight || _zigCycle <= 0f) WalkToCurPathNode(speed, turn, 0f);
        else
        {
            float left = (Goal() - Pos()).Length();
            float phase = (left % _zigCycle) * 360f / _zigCycle + (_zigSide ? 0f : 180f);
            WalkToCurPathNode(speed, turn, _zigAngle * Mathf.Sin(Mathf.DegToRad(phase)));
        }
        _spin += p.CarapaceSpinSpeed * k;
        if (_spin > 360f) _spin -= 360f;
    }
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveMove:                                      // TNerveElecCarapaceMove
                if (State != 2) return false;
                Move();
                if (_returning)
                {
                    float range = 64f * SmsElecNokonoko.Prm.CarapaceSpeed;
                    if ((Goal() - Pos()).Length() < range)
                    {
                        if (!_host.IsCatchReady) _host.ForceCatchReady();
                        PushAfterCurrent(NerveReturn);
                        return true;
                    }
                }
                {
                    // the game: the far point counts as reached within 10 u (XZ^2 < 100) of a 15 u tick step. The port's two ticks a physics frame land as
                    // one 30 u step from the same read position, so the band is kept at the game's 2/3 of the step (20 u here): any narrower and a step can
                    // straddle the point for ever (drive 148 run e: the shell hovered at its far point until the Koopa gave up and grew a new one)
                    float near = SmsElecNokonoko.Prm.CarapaceSpeed * (_host != null ? _host.SpeedScale : 1f) * 2f * (10f / 15f);
                    Vector3 d = Goal() - Pos(); d.Y = 0f;
                    if (!_bouncing && d.LengthSquared() < near * near)
                    {
                        if (_returning) { PushAfterCurrent(NerveReturn); return true; }
                        _returning = true;
                        SetGoalPoint(_host.GlobalPosition * U);
                        if (DebugPrint) GD.Print($"[SMS carapace] {Name}: the far point reached, home");
                    }
                }
                return false;

            case NerveWaitShock:                                 // TNerveElecCarapaceWait
                return _time > 60;

            case NerveReturn:                                    // TNerveElecCarapaceReturn
                if (_time == 0)
                {
                    _returnStep = (_host.GlobalPosition * U - Pos()) * 0.015625f;
                    _host.CatchIn();
                    _noBind = true; _vel = Vector3.Zero;
                    if (DebugPrint) GD.Print($"[SMS carapace] {Name}: returning, step {_returnStep}");
                }
                if (_time < 20) _host.CatchIn();
                if (_host.IsDeadByThunder()) { _host.MeltKill(); if (DebugPrint) GD.Print($"[SMS carapace] {Name}: the host dead by thunder"); }
                _spin += SmsElecNokonoko.Prm.CarapaceSpinSpeed; if (_spin > 360f) _spin -= 360f;
                {
                    Vector3 p = Pos() + _returnStep, h = _host.GlobalPosition * U;
                    if (_returnStep.X > 0f) { if (p.X > h.X) p.X = h.X; } else if (p.X < h.X) p.X = h.X;
                    if (_returnStep.Y > 0f) { if (p.Y > h.Y) p.Y = h.Y; } else if (p.Y < h.Y) p.Y = h.Y;
                    if (_returnStep.Z > 0f) { if (p.Z > h.Z) p.Z = h.Z; } else if (p.Z < h.Z) p.Z = h.Z;
                    _lin += p - Pos();
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }

    // ------------------------------------------------------------------------------------------------------------------ hits
    /// <summary>sendMessage: Mario in its attack cylinder -> ELECTRIC_SHOCK; taken -> no collision and WAIT (60 ticks).</summary>
    protected override void AttackToMario()
    {
        if (_mario == null) return;
        bool hurtBefore = _mario.stateOfMario == Mario.MarioState.hurt;
        _mario.TakeShock();
        Shocks++;
        if (!hurtBefore && _mario.stateOfMario == Mario.MarioState.hurt)
        {
            SetCollisionLayerValue(1, false);
            if (!IsNerve(NerveWaitShock)) PushNerve(NerveWaitShock);
        }
    }
    /// <summary>receiveMessage(TRAMPLE): Mario's stomp is a shock; never a kill.</summary>
    protected override bool IsHitValid(HitMessage msg) { if (msg == HitMessage.Trample) _mario?.TakeShock(); return false; }
    /// <summary>receiveMessage(SPRAYED_BY_WATER): taken, nothing happens.</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos) { if (_dead || State != 2) return false; SmsWaterHitMarks.Emit(this, hitPos); return true; }
    protected override void BehaveToWater() { }
    public override void Kill() { KillAttach(); }
    protected override void HitCheckMario() { if (State == 2) base.HitCheckMario(); }
}
