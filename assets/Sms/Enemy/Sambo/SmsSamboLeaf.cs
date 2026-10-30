using Godot;

/// <summary>
/// SMS pass 129 -- TSamboLeaf (hanasambo.cpp): one of the flower manager's 18 leaves. TSamboFlowerManager::dropLeaf(pos, scale) fires three free
/// ones when a Pokey Head rises (TNerveSamboHeadAppear, tick 20): at 0 / 120 / 240 degrees, velocity (0, rand(vy, 1.2 vy), rand(vxz, 1.2 vxz))
/// turned by the angle (samboflower.prm: LeafVelocityXZ 3, LeafVelocityY 4), the leaf facing angle - 90, at the head's scale. perform(CUE_MOVE):
/// pos += vel; vel.y -= LeafGravity (0.15) while above -20; gone once below the ground under it (checkGround at y + 20). Drawn at pos, yawed, and
/// tilted about Z by -MsGetRotFromZaxis(vel).x (it pitches along its flight). Units: velocities per tick (120 Hz), as every actor's.
/// </summary>
[GlobalClass]
public partial class SmsSamboLeaf : Node3D
{
    private Vector3 _pos, _vel, _scale = Vector3.One;   // units / units a tick
    private float _yaw, _gravity = 0.15f, _accum;
    private bool _flying;
    private Node3D _model;
    public bool Flying => _flying;

    public override void _Ready() { _model = GetNodeOrNull<Node3D>("Model"); if (_model != null) _baseScale = _model.Scale; Visible = false; }

    /// <summary>generate(pos) + the manager's velocity / yaw / scale: the leaf starts 10 units over the point and flies.</summary>
    public void Fly(Vector3 posUnits, Vector3 velUnitsPerTick, float yawDeg, Vector3 scale, float gravityPerTick)
    {
        _pos = posUnits + Vector3.Up * 10f; _vel = velUnitsPerTick; _yaw = yawDeg; _scale = scale; _gravity = gravityPerTick;
        _flying = true; Visible = true;
        Place();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_flying) return;
        int n = SmsUnits.TicksFor(delta, ref _accum);
        for (int i = 0; i < n && _flying; i++)
        {
            _pos += _vel;
            if (_vel.Y > -20f) _vel.Y -= _gravity;
            if (_pos.Y < GroundUnder(_pos) ) { _flying = false; Visible = false; return; }
        }
        Place();
    }
    private float GroundUnder(Vector3 pUnits)
    {
        float u = SmsUnits.UnitsPerMetre;
        var space = GetWorld3D().DirectSpaceState;
        Vector3 from = pUnits / u + Vector3.Up * (20f / u);
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 50f, 1);
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? ((Vector3)hit["position"]).Y * u : pUnits.Y - 1000f;
    }
    private void Place()
    {
        float u = SmsUnits.UnitsPerMetre;
        GlobalPosition = _pos / u;
        Vector3 v = _vel; float pitch = 0f;
        if (v.LengthSquared() > 1e-6f) pitch = Mathf.RadToDeg(Mathf.Atan2(-v.Y, new Vector2(v.X, v.Z).Length()));   // MsGetRotFromZaxis(vel).x
        Basis b = Basis.FromEuler(new Vector3(0f, Mathf.DegToRad(_yaw), 0f)) * Basis.FromEuler(new Vector3(0f, 0f, Mathf.DegToRad(-pitch)));
        GlobalBasis = b;
        if (_model != null) _model.Scale = _baseScale * _scale;    // the scene's glb scale (1 / 74.44) x the head's body scale (mScaling)
    }
    private Vector3 _baseScale = Vector3.One;
}
