using Godot;

/// <summary>
/// A Village Underside Chuckster. TBaseNPC::setIndividualDifference_ creates
/// TNpcThrow when placement flag bit 0 is set. Closing the talk then enters
/// TNerveNPCThrow: mom_throw winds up, collision turns off at tick 20 for 30
/// frames, and tick 40 calls TNpcThrow::throwMario. SmsNpc owns that faithful
/// state machine and calls Mario.SmsThrowMario(direction, speed, this); this
/// subtype makes the throw role explicit while each scene instance supplies the
/// vanilla speed and angle.
/// </summary>
public partial class SmsChuckster : SmsNpc
{
    /// <summary>Enable the inherited TNpcThrow path before SmsNpc initializes.</summary>
    public override void _Ready()
    {
        ThrowsMario = true;
        base._Ready();
    }
}
