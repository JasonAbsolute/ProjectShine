using Godot;

/// <summary>
/// One bone of the body's time lag (SmsTimeLag): the fields of a /Mario/MtxEffectN.prm -- the position filter's acceleration and
/// brake per 120 Hz tick, its clamp in units, the rotation filter's acceleration and brake. The shipped files: MtxEffect14 (jnt_chest)
/// 0.1 / 0.9 / 5 / 1.0 / 0.5, MtxEffect16 and 22 (jnt_arm_R1 / L1) 0.1 / 0.8 / 5 / 0.05 / 0.8. A resource so the scene
/// assets/Sms/BodyLag.tscn lists them and the editor edits them.
/// </summary>
[GlobalClass]
public partial class SmsTimeLagEntry : Resource
{
    /// <summary>The bone whose CHILDREN trail (the bone's own skin keeps the clip): jnt_chest, jnt_arm_R1, jnt_arm_L1.</summary>
    [Export] public string BoneName = "";
    /// <summary>The position filter: velocity += accel x (target - lag) each tick, then x brake; the lag stays within +-limit units of the target.</summary>
    [Export] public float PosAccel = 0.1f;
    [Export] public float PosBrake = 0.9f;
    [Export] public float PosLimitUnits = 5f;
    /// <summary>The rotation filter on the quaternion's components, the same way (no clamp).</summary>
    [Export] public float QuatAccel = 1.0f;
    [Export] public float QuatBrake = 0.5f;
}
