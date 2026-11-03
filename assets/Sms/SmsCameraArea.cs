using Godot;

/// <summary>
/// The game's camera cube -- TCubeCameraInfo (CubeMapTool.cpp): an oriented box in the stage with a camera code, read from the stage's
/// cube list. Every tick CPolarSubCamera::controlByCameraCode_ (CameraCodeControl.cpp 9) tests Mario's position + 75 units against the
/// cubes in list order and the first hit's code is the kind the ladder of execCameraModeChangeProc_ (CameraChange.cpp 568) starts
/// from (FOLLOW itself yields to the wall jump and the climb; INDOOR, DELFINO, HANG, FENCE ... hold). Outside every cube the stage's
/// own kind applies (CamController.DefaultKind). Here an Area3D: drop assets/Sms/SmsCameraArea.tscn into a level, size its box, pick
/// the Kind -- or put metadata `sms_camera_kind` = a kind's name on any Area3D. Where areas overlap the highest Priority wins.
/// Pass 153l: a cube WITH a camera map tool (TCameraMapTool: the cube names a CameraMapInfo of the stage's table, whose position is the
/// camera's point and whose mode is a FIX or DEFINITE kind). A DEFINITE kind (DefiniteD2 here: the casino boss room) keeps the eye at
/// that point and the look-at on Mario (TCameraMapTool::calcPosAndAt: the usual look-at); the stick, the L camera and the status ladder
/// are all dead inside it (execCameraModeChangeProc_ returns at once for a fix / definite code). The point is a child Marker3D named Eye
/// (EyePath): its global position. A definite Kind without an Eye is read as the stage's own kind. The FIX kinds (a fixed eye AND a
/// fixed direction) are not carried.
/// </summary>
[GlobalClass]
public partial class SmsCameraArea : Area3D
{
    /// <summary>The kind the camera takes inside (the cube's camera code). LNormal is the L camera's own and is read as Follow. DefiniteD2
    /// needs the Eye child: the camera stands there.</summary>
    [Export] public SmsCamKind Kind = SmsCamKind.Indoor;

    /// <summary>Where areas overlap, the highest wins (the game takes the first cube in the stage's list).</summary>
    [Export] public int Priority = 0;

    /// <summary>153l: the camera map tool's point for a DEFINITE kind -- a child Node3D (a Marker3D) whose global position is where the
    /// camera stands while Mario is inside.</summary>
    [Export] public NodePath EyePath = new("Eye");

    private Node3D _eye;

    public override void _Ready()
    {
        Monitoring = false; // the camera asks the space for the point; the area watches nothing itself
        Monitorable = true;
        _eye = GetNodeOrNull<Node3D>(EyePath);
    }

    /// <summary>The map tool's eye, if this area carries one (a definite kind needs it).</summary>
    public bool HasEye => _eye != null && IsInstanceValid(_eye);
    /// <summary>The eye's global position, metres.</summary>
    public Vector3 EyeGlobal => HasEye ? _eye.GlobalPosition : GlobalPosition;
}
