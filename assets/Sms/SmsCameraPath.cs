using System;
using Godot;

/// <summary>
/// A camera demo baked frame by frame -- the game's bck demo camera (TCameraBck, CameraBck.cpp: camera_model.bmd's joints cam_position /
/// cam_interest animated by a .bck in /scene/map/camera; CPolarSubCamera::updateDemoCamera_ reads the position joint's translation as the
/// eye, the interest joint's as the look-at, the position joint's Y axis as the up and its Y scale as the field of view) -- for SMS pass
/// 153l (btelesa_roll_camera.bck, King Boo's intro). The samples are in the STAGE's units (74.44 a metre) in the stage's own frame; the
/// player of the path (SunshineCamera.StartDemoCamera) gets the Transform3D that puts that frame into the level. Generated .tres files come
/// from tools/projectshine/npc82/sirena151/bck_cam153.py.
/// </summary>
[GlobalClass]
public partial class SmsCameraPath : Resource
{
    /// <summary>The bck's rate: 30 frames a second.</summary>
    [Export] public float FrameRate = 30f;
    /// <summary>The frames baked (the bck's end frame + 1).</summary>
    [Export] public int Frames = 0;
    /// <summary>The eye (cam_position) each frame, stage units.</summary>
    [Export] public Vector3[] EyeUnits = System.Array.Empty<Vector3>();
    /// <summary>The look-at (cam_interest) each frame, stage units.</summary>
    [Export] public Vector3[] AtUnits = System.Array.Empty<Vector3>();
    /// <summary>cam_position's Y axis each frame (its length is the Y scale: the fovy; normalised when read).</summary>
    [Export] public Vector3[] UpAxis = System.Array.Empty<Vector3>();
    /// <summary>The field of view each frame (cam_position's Y scale), degrees.</summary>
    [Export] public float[] Fovy = System.Array.Empty<float>();

    /// <summary>The demo's length in seconds (the bck's frames at its rate).</summary>
    public float Seconds => Frames > 0 && FrameRate > 0f ? Frames / FrameRate : 0f;
    public bool IsValid => Frames > 1 && EyeUnits.Length >= Frames && AtUnits.Length >= Frames;

    /// <summary>The pose at a (fractional) frame, the frames interpolated linearly, held at the ends.</summary>
    public void Sample(float frame, out Vector3 eyeU, out Vector3 atU, out Vector3 up, out float fovy)
    {
        if (!IsValid) { eyeU = atU = Vector3.Zero; up = Vector3.Up; fovy = 50f; return; }
        float f = Mathf.Clamp(frame, 0f, Frames - 1);
        int i0 = (int)MathF.Floor(f); int i1 = Mathf.Min(i0 + 1, Frames - 1); float t = f - i0;
        eyeU = EyeUnits[i0].Lerp(EyeUnits[i1], t);
        atU = AtUnits[i0].Lerp(AtUnits[i1], t);
        up = UpAxis.Length >= Frames ? UpAxis[i0].Lerp(UpAxis[i1], t) : Vector3.Up;
        up = up.LengthSquared() > 1e-6f ? up.Normalized() : Vector3.Up;
        fovy = Fovy.Length >= Frames ? Mathf.Lerp(Fovy[i0], Fovy[i1], t) : 50f;
    }
}
