using Godot;

/// <summary>
/// The game's camera cube -- TCubeCameraInfo (CubeMapTool.cpp): an oriented box in the stage with a camera code, read from the stage's
/// cube list. Every tick CPolarSubCamera::controlByCameraCode_ (CameraCodeControl.cpp 9) tests Mario's position + 75 units against the
/// cubes in list order and the first hit's code is the kind the ladder of execCameraModeChangeProc_ (CameraChange.cpp 568) starts
/// from (FOLLOW itself yields to the wall jump and the climb; INDOOR, DELFINO, HANG, FENCE ... hold). Outside every cube the stage's
/// own kind applies (CamController.DefaultKind). Here an Area3D: drop assets/Sms/SmsCameraArea.tscn into a level, size its box, pick
/// the Kind -- or put metadata `sms_camera_kind` = a kind's name on any Area3D. Where areas overlap the highest Priority wins.
/// A cube with a camera map tool (a fixed or definite camera) is not ported yet.
/// </summary>
[GlobalClass]
public partial class SmsCameraArea : Area3D
{
    /// <summary>The kind the camera takes inside (the cube's camera code). LNormal is the L camera's own and is read as Follow.</summary>
    [Export] public SmsCamKind Kind = SmsCamKind.Indoor;

    /// <summary>Where areas overlap, the highest wins (the game takes the first cube in the stage's list).</summary>
    [Export] public int Priority = 0;

    public override void _Ready()
    {
        Monitoring = false; // the camera asks the space for the point; the area watches nothing itself
        Monitorable = true;
    }
}
