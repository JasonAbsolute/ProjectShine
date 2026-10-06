using Godot;

/// <summary>
/// The water-get sparkle: TMario::emitGetWaterEffect -- emitAndBindToPosPtr(MS_ITEMGET1_B, &amp;unk160), bound to the 'center' joint, fired
/// when a water bottle refills the tank (MarioReceiveMsg.cpp 150 / 159; the item's SmsWaterBottle plays it). The particle is the disc's
/// ms_itemget1_b.jpa (tools/projectshine/jpa_dump.py + gen_fx_water.py): 1.2 streaks a frame for 80 frames from a 330-unit sphere around
/// him, each flying INWARD at 12 units a frame for 25 frames -- a cross of two 9 x 67 unit glow bars along its flight, stretching out from
/// nothing over 75 % of the life, cyan on blue, additive -- and ending in a 75 x 100 unit white-on-blue glow puff that rises and
/// shrinks away over 16 frames. All of that is the scene, assets/Sms/Fx/GetWater.tscn; this script (SmsGetSparkle's lifecycle: bind,
/// emit EmitFrames, free once the last particle is gone) only names it. Sound is his to fill.
/// </summary>
public partial class SmsGetWater : SmsGetSparkle
{
}
