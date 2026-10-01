using Godot;

/// <summary>
/// One camera shake (a /Camera/shake*.prm, TCamSaveShake: mShakeTime, mShakeAmpX / VelX, AmpY / VelY, AmpZ / VelZ). TCameraShake runs it
/// for Time ticks (the camera is on PerformList Movement: 120 Hz): each tick the camera's polar angles about its target get
/// Amp x sin(Vel x tick) -- X the vertical angle (pitch about the target), Y the horizontal (yaw), Z the roll of the up vector --
/// while every Amp falls linearly to 0 over Time (mDec = Amp / Time). Angles are BAM: 65536 = 360 degrees; a Vel of 7008 a tick is a
/// 9.4-tick period (12.8 Hz). The three Mario shakes are inline resources on the CameraShake node (assets/Sms/CameraShake.tscn), his
/// to tune: shakeHipDrop.prm (25 ticks, pitch 296.9 at 7008), shakeDamage.prm (60 ticks, roll 900 at 5000), shakeKoopaHipdrop.prm --
/// the super hip drop's (200 ticks, pitch 200 at 1644, yaw 250 at 1715, roll 700 at 3653). shakeWallJump.prm ships with Time 0: nothing.
/// </summary>
[GlobalClass]
public partial class SmsCameraShakeMode : Resource
{
    /// <summary>mShakeTime: the shake's length in 120 Hz ticks (0 = this shake does nothing, as the game's wall-jump one).</summary>
    [Export(PropertyHint.Range, "0,600,1")] public int TimeTicks = 25;
    /// <summary>mShakeAmpX: the vertical angle's amplitude, BAM (65536 = 360 deg).</summary>
    [Export] public float AmpX;
    /// <summary>mShakeVelX: the vertical angle's sine speed, BAM a tick.</summary>
    [Export] public int VelX;
    /// <summary>mShakeAmpY / mShakeVelY: the horizontal angle (yaw about the target).</summary>
    [Export] public float AmpY;
    [Export] public int VelY;
    /// <summary>mShakeAmpZ / mShakeVelZ: the roll of the up vector.</summary>
    [Export] public float AmpZ;
    [Export] public int VelZ;
}
