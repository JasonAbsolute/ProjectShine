using Godot;

/// <summary>TTalkCursor (System/TalkCursor.hpp): the white arrow the director hangs over the one NPC Mario can talk to right now,
/// at the NPC's cursor height (mSLCursorHeight over the body). The game's is a 2D-ish sprite; here a small cone, bobbing and
/// turning so it reads from any angle. Shown / hidden by the NPC that owns it.</summary>
public partial class SmsTalkCursor : Node3D
{
    [Export] public float BobAmplitudeMetres = 0.05f;
    [Export] public float BobHz = 1.5f;
    [Export] public float SpinDegPerSec = 90f;

    private float _t;
    private Vector3 _rest;
    private bool _restSet;

    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (!_restSet) { _rest = Position; _restSet = true; }
        _t += (float)delta;
        Position = _rest + Vector3.Up * (Mathf.Sin(_t * Mathf.Tau * BobHz) * BobAmplitudeMetres);
        RotateY(Mathf.DegToRad(SpinDegPerSec) * (float)delta);
    }

    public void SetRest(Vector3 local) { _rest = local; _restSet = true; Position = local; }
}
