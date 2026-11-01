using Godot;
using System;

/// <summary>
/// SMS pass 149 -- MARIO_STATUS_PULLING (MarioSpecial.cpp 1168 pulling(), MarioCheckCol.cpp 226 hitNoKeepPull): holding something anchored (an
/// <see cref="SmsPullHandle"/>: the Chain Chomplet's chain end) without lifting it. hitNoKeepPull: touching the handle with B HELD in a take
/// situation (canTake) -> TAKE accepted -> PULLING with ANIM_HOLD, the handle his held object. pulling(): the hand input released (B up) -> DETACH,
/// PULL_END (ma_hold_return, then the wait); A -> DETACH and the jump (the game: PULL_JUMP, ma_hold_to_hang -- a hop with the handle kept; not
/// ported: he lets go and jumps); his face = -Z of the handle's taking matrix (toward the body); the stick read against his BACK: cosF = the part of
/// the stick pointing away from the body (never forward: a stick toward the body pulls nothing), sinF the part across it, both x mIntendedMag;
/// pos += rateV x cosF x back + rateH x sinF x across (pullparam*.prm: V 0.3, H 0.2 for the Chomplet's chain; OIL_PULLING on goop: 0.1 / 0.01);
/// the handle's moveRequest(pos) -> true -> he stands at pos; the clip by the frame's movement against his face: under 1 u -> ma_hold_wait,
/// toward the body (within 45 deg) -> ma_hold_drag (dragged), away (past 135) -> ma_hold_back (the pull), the sides -> ma_hold_move_r / _l;
/// ma_hold / ma_hang_to_hold run out into ma_hold_wait first. The handle's owner drags him back through <see cref="SmsPullMoveRequest"/>
/// (TMario::moveRequest: his position set outright).
/// </summary>
public partial class Mario
{
    private enum SmsPullSub { Hold, Wait, Drag, Back, MoveR, MoveL, End }
    private SmsPullSub _smsPull;
    private SmsPullHandle _smsPullHandle;
    private int _smsPullEndTicks;
    private Vector3 _smsPullPrevPos;

    /// <summary>For the drives / other actors: the handle in his hands (null when not pulling).</summary>
    public SmsPullHandle SmsPullingHandle => stateOfMario == MarioState.pulling ? _smsPullHandle : null;
    public string SmsPullSubName => _smsPull.ToString();
    /// <summary>mIntendedMag (0..32) and the stick's world direction of the last pulling frame -- for a handle's owner that reads his stick (pass 150: the
    /// Chain Chomp's drag speed, bosswanwan.cpp 1535: gpMarioOriginal->mIntendedYaw / mIntendedMag).</summary>
    public float SmsIntendedMag => _smsIntendedMag;
    public Vector3 SmsPullStickDir { get; private set; }

    /// <summary>hitNoKeepPull's trigger: a pullable handle he is touching with B held in a take situation (the canTake / isTakeSituation
    /// geometry of the take, pass 83 / 130b: within HoldRadius + the handle's radius of his feet, the cylinders overlapping, within 60 deg of his face).</summary>
    private bool SmsFindPullHandle(out SmsPullHandle best)
    {
        best = null;
        if (!SmsCanTakeNow())
            return false;
        Vector3 feet = GlobalPosition;
        Vector3 face = _smsFaceValid ? SmsMath.DirFromYaw(_smsFaceYaw) : SmsFacingOrVelocityDir();
        float cosCone = Mathf.Cos(SmsParams.Take.ConeBam * SmsUnits.BamToRad);
        float marioH = SmsUnits.UnitsToMetres(SmsUnits.MarioHeightUnits);
        float bestD = float.MaxValue;
        foreach (Node n in GetTree().GetNodesInGroup("sms_pullable"))
        {
            if (n is not SmsPullHandle h || !IsInstanceValid(h) || !h.IsInsideTree() || h.IsHeld || !h.CanBePulled)
                continue;
            Vector3 rel = h.GlobalPosition - feet;                                  // the hit actor's point is its cylinder's base (the ring - the height / 2)
            float dxz = new Vector2(rel.X, rel.Z).Length();
            float reach = SmsUnits.UnitsToMetres(SmsParams.Mario.HoldRadius + h.HitRadiusUnits);
            if (dxz > reach)
                continue;
            if (rel.Y + SmsUnits.UnitsToMetres(h.HitHeightUnits) < 0f || rel.Y > marioH)
                continue;
            float dist = rel.Length();
            if (dist > reach)
                continue;
            if (dxz > 1e-4f && new Vector3(rel.X, 0f, rel.Z).Normalized().Dot(face) < cosCone)
                continue;
            if (dist < bestD) { bestD = dist; best = h; }
        }
        return best != null;
    }

    /// <summary>changePlayerStatus(MARIO_STATUS_PULLING) with ANIM_HOLD; the handle told it is taken.</summary>
    private void SmsPullBegin(SmsPullHandle h)
    {
        _smsPullHandle = h;
        h.BehaveTaken(this);
        stateOfMario = MarioState.pulling;
        _smsPull = SmsPullSub.Hold;
        _smsPullEndTicks = 0;
        _smsPullPrevPos = GlobalPosition;
        velocity = Vector3.Zero;
        if (animTree != null) animTree.Active = false;
        animPlayer.Play("ma_hold");
        SmsPullFace();
        if (SmsDebugPrint) GD.Print($"[SMS pull] TAKE: {h.Name} in his hands (PULLING, ma_hold)");
    }

    /// <summary>mFaceAngle.y = matan(-mtx[2][2], -mtx[0][2]): he faces back along the taking matrix's Z (toward the body the handle hangs from).</summary>
    private void SmsPullFace()
    {
        Vector3 z = _smsPullHandle.TakingTransform.Basis.Z; z.Y = 0f;
        if (z.LengthSquared() < 1e-6f) return;
        Vector3 face = (-z).Normalized();
        lastFacingDirection = face;
        _smsFaceYaw = SmsMath.YawFromDir(face); _smsFaceValid = true;
        if (armature != null) armature.Rotation = armature.Rotation with { Y = BaseYawFromDir(face) };
    }

    /// <summary>HIT_MESSAGE_DETACH to the handle; <paramref name="toEnd"/>: PULL_END (ma_hold_return) or straight to the wait.</summary>
    private void SmsPullDetach(string why, bool toEnd)
    {
        var h = _smsPullHandle;
        _smsPullHandle = null;
        if (h != null && IsInstanceValid(h)) h.BehaveApart();
        if (SmsDebugPrint) GD.Print($"[SMS pull] DETACH ({why}){(toEnd ? ": PULL_END" : "")}");
        if (toEnd && animPlayer.HasAnimation("ma_hold_return"))
        {
            _smsPull = SmsPullSub.End; _smsPullEndTicks = 0;
            animPlayer.Play("ma_hold_return");
            return;
        }
        stateOfMario = MarioState.idle;
        SetMarioState(stateOfMario);
    }

    /// <summary>pulling(), once a physics frame while PULLING. The handle gone -> the wait.</summary>
    private void SmsTickPull(double delta, Vector3 inputDirWorld, float stickStrength)
    {
        velocity.X = 0f; velocity.Z = 0f;
        if (_smsPull == SmsPullSub.End)
        {
            // PULL_END: ma_hold_return to its end (or 24 frames), then the wait
            if (++_smsPullEndTicks >= 24 || animPlayer.CurrentAnimation != "ma_hold_return" || !animPlayer.IsPlaying())
            {
                stateOfMario = MarioState.idle;
                SetMarioState(stateOfMario);
            }
            return;
        }
        var h = _smsPullHandle;
        if (h == null || !IsInstanceValid(h) || !h.IsInsideTree() || h.Holder != this)
        {
            _smsPullHandle = null;
            if (SmsDebugPrint) GD.Print("[SMS pull] the handle went: the wait");
            stateOfMario = MarioState.idle;
            SetMarioState(stateOfMario);
            return;
        }
        if (!_smsTalkSwallowA && _smsPadDisabledTicks == 0 && (Input.IsActionJustPressed("button_a") || Input.IsActionJustPressed("key_space")))
        {
            // the game: PULL_JUMP (ANIM_HOLD_TO_HANG, the handle kept through a hop). DEVIATION: he lets go; the ordinary jump reads the buffered A next frame
            SmsPullDetach("A", false);
            jumpBuffer = JUMP_BUFFER_FRAMES;
            return;
        }
        if (!Input.IsActionPressed("button_b"))
        {
            SmsPullDetach("B released", true);
            return;
        }
        SmsPullFace();
        SmsPullStickDir = inputDirWorld.LengthSquared() > 1e-6f ? new Vector3(inputDirWorld.X, 0f, inputDirWorld.Z).Normalized() : Vector3.Zero;
        Vector3 face = SmsMath.DirFromYaw(_smsFaceYaw);
        Vector3 back = -face;                                                       // backAngle = mFaceAngle.y + 0x8000
        Vector3 across = new Vector3(-back.Z, 0f, back.X);                          // (-cos b, 0, sin b): the sideways axis of pulling()'s sums
        float mag = _smsIntendedMag;
        float cosF = 0f, sinF = 0f;
        if (mag > 0f && inputDirWorld.LengthSquared() > 1e-6f)
        {
            Vector3 s = new Vector3(inputDirWorld.X, 0f, inputDirWorld.Z).Normalized();
            cosF = MathF.Max(0f, back.X * s.X + back.Z * s.Z) * mag;              // JMASCos(back - intended), floored at 0
            sinF = (back.X * s.Z - back.Z * s.X) * mag;                             // JMASSin(back - intended)
        }
        bool oily = _smsDirty;                                                      // OIL_PULLING: the oil rates while he stands on goop (MARIO_FLAG_DIRTY)
        float rateV = oily ? h.OilPullRateV : h.PullRateV, rateH = oily ? h.OilPullRateH : h.PullRateH;
        // units a 30 fps frame -> this physics frame (SpeedToMps' convention: x 30 / U a second)
        Vector3 stepU = back * (rateV * cosF) + across * (rateH * sinF);
        Vector3 pos = GlobalPosition + stepU * (30f * (float)delta / SmsUnits.UnitsPerMetre);
        if (h.MoveRequest(pos))
            GlobalPosition = pos;                                                   // mHeldObject->moveRequest(pos) == 1 -> mPosition = pos
        // the clip by this frame's movement (mPosition - mPrevPosition) against his face
        Vector3 d = GlobalPosition - _smsPullPrevPos; d.Y = 0f;
        _smsPullPrevPos = GlobalPosition;
        string cur = animPlayer.CurrentAnimation;
        if (_smsPull == SmsPullSub.Hold)
        {
            if (cur != "ma_hold" || !animPlayer.IsPlaying()) { _smsPull = SmsPullSub.Wait; animPlayer.Play("ma_hold_wait"); }
            return;
        }
        float lenU = SmsUnits.MetresToUnits(d.Length());
        SmsPullSub next;
        if (lenU < 1f) next = SmsPullSub.Wait;
        else
        {
            float adff = Mathf.RadToDeg(SmsMath.WrapAngle(SmsMath.YawFromDir(d.Normalized()) - _smsFaceYaw));
            if (adff >= -45f && adff <= 45f) next = SmsPullSub.Drag;
            else if (adff <= -135f || adff >= 135f) next = SmsPullSub.Back;
            else if (adff < -45f) next = SmsPullSub.MoveR;
            else next = SmsPullSub.MoveL;
        }
        if (next != _smsPull || !animPlayer.IsPlaying())
        {
            _smsPull = next;
            string clip = next switch { SmsPullSub.Drag => "ma_hold_drag", SmsPullSub.Back => "ma_hold_back", SmsPullSub.MoveR => "ma_hold_move_r", SmsPullSub.MoveL => "ma_hold_move_l", _ => "ma_hold_wait" };
            if (animPlayer.HasAnimation(clip)) animPlayer.Play(clip, -1, oily ? 5f : 1f);
        }
    }

    /// <summary>TMario::moveRequest from the handle's owner (the chain's end dragging him back): his position set outright, his height kept.</summary>
    public void SmsPullMoveRequest(Vector3 posM)
    {
        if (stateOfMario != MarioState.pulling) return;
        GlobalPosition = new Vector3(posM.X, GlobalPosition.Y, posM.Z);
        _smsPullPrevPos = new Vector3(posM.X, _smsPullPrevPos.Y, posM.Z);         // the drag is not his own step: the clip keeps reading the stick's part
    }
}
