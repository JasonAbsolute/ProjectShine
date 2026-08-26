using Godot;

/// <summary>
/// Snaps a Control between its authored rotation and that rotation plus
/// <see cref="AngleDegrees"/>, holding each pose for <see cref="HoldSeconds"/>.
///
/// The change is instantaneous — no easing, no tween — which is what gives the
/// hand-drawn menu doodles their two-frame, flip-book feel rather than looking
/// like something mechanically animated.
///
/// The node's rotation in the scene is the rest pose, so a graphic angled in the
/// editor flips relative to that angle instead of snapping to zero.
///
/// Runtime only, deliberately not a [Tool] script: animating in the editor would
/// bake whatever rotation it happened to be at into the scene on every save, and
/// you'd lose the poses you set by hand.
/// </summary>
public partial class SnapRotate : Control
{
    /// <summary>How far to flip, in degrees. Negative flips the other way.</summary>
    [Export] public float AngleDegrees = 15f;

    /// <summary>Seconds to hold each pose before flipping to the other.</summary>
    [Export] public float HoldSeconds = 5f;

    /// <summary>
    /// Shifts this node's place in the cycle, 0–1 of a hold. Leave at 0 so a row
    /// flips in unison — every node accumulates the same delta from the same
    /// start, so they stay frame-locked without a shared clock. Only set this if
    /// you deliberately want a staggered ripple.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float PhaseOffset = 0f;

    /// <summary>Start already flipped, so a row can alternate its resting state.</summary>
    [Export] public bool StartFlipped = false;

    /// <summary>
    /// Rotate around the middle of the graphic. Off uses the node's own
    /// pivot_offset — useful for something that should hinge from its base.
    /// </summary>
    [Export] public bool PivotAtCenter = true;

    private float _restRotation;
    private float _elapsed;
    private bool _flipped;

    public override void _Ready()
    {
        _restRotation = Rotation;
        ApplyPivot();

        _flipped = StartFlipped;
        // Bring the node forward in its cycle so staggered neighbours don't flip
        // on the same frame.
        _elapsed = PhaseOffset * Mathf.Max(HoldSeconds, 0f);
        ApplyRotation();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            ApplyPivot();
    }

    private void ApplyPivot()
    {
        if (!PivotAtCenter)
            return;

        Vector2 wanted = Size * 0.5f;
        if (PivotOffset.IsEqualApprox(wanted))
            return;

        // A Control renders each point as  position + pivot + M*(p - pivot),
        // where M is its rotation/scale basis. Moving the pivot therefore shifts
        // the whole node unless M is identity — which bit the scaled decor, since
        // the editor kept showing the authored pivot while the runtime used the
        // centre. Solving the two forms for equality gives:
        //     position += (I - M) * (oldPivot - newPivot)
        var basis = new Transform2D(Rotation, Scale, 0f, Vector2.Zero);
        Vector2 delta = PivotOffset - wanted;
        Position += delta - basis * delta;

        PivotOffset = wanted;
    }

    private void ApplyRotation()
    {
        Rotation = _flipped ? _restRotation + Mathf.DegToRad(AngleDegrees) : _restRotation;
    }

    public override void _Process(double delta)
    {
        if (HoldSeconds <= 0f)
            return;

        _elapsed += (float)delta;
        if (_elapsed < HoldSeconds)
            return;

        // Subtract rather than zero, so a long frame doesn't lose time and the
        // graphics stay in step with each other over a long session.
        _elapsed -= HoldSeconds;
        _flipped = !_flipped;
        ApplyRotation();
    }
}
