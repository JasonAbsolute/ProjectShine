using System;
using Godot;

[Tool]
public partial class MovingPlatform : AnimatableBody3D
{
    public enum MovementMode
    {
        PingPong,
        Loop,
        OneShot,
        // Like Loop, but skips the origin on every lap after the first — jumps
        // straight from the last waypoint back to Waypoints[0] and keeps going
        // forward, instead of retracing back through the origin each cycle.
        LoopSkipStart,
    }

    [Export]
    public Vector3[] Waypoints = Array.Empty<Vector3>();

    // Home is cached once, in _Ready() — if you drag the node to a new resting
    // spot afterward (in the same editor session), that cache goes stale. Click
    // this to re-anchor home to wherever the node currently sits before adding
    // waypoints relative to it. Existing waypoints are offsets from home, so
    // re-anchoring shifts the whole path rigidly along with it.
    [ExportToolButton("Set Origin Here")]
    public Callable SetOriginHereButton => Callable.From(SetOriginAtCurrentLocation);

    // Drag the node in the viewport to where the next stop should be, then click —
    // captures that spot as a new waypoint (relative to home) and snaps the node
    // back home so it keeps representing its resting position.
    [ExportToolButton("Add Waypoint At Current Location")]
    public Callable AddWaypointHereButton => Callable.From(AddWaypointAtCurrentLocation);

    [ExportToolButton("Remove Last Waypoint")]
    public Callable RemoveLastWaypointButton => Callable.From(RemoveLastWaypoint);

    [Export]
    public float MoveSpeed = 2.0f;

    [Export]
    public MovementMode Mode = MovementMode.PingPong;

    [Export]
    public float WaitTime = 0.0f;

    [Export]
    public Vector3 SpinAxis = Vector3.Zero;

    [Export]
    public float SpinSpeed = 0.0f;

    [Export]
    public bool Active = true;

    [Export(PropertyHint.ColorNoAlpha)]
    public Color TintColor = new Color(1, 1, 1, 1);

    // Per-waypoint rotation triggers
    // Index matches Waypoints array: WaypointRotations[0] triggers when heading to Waypoints[0]
    [Export]
    public Vector3[] WaypointRotations = Array.Empty<Vector3>();

    [Export(PropertyHint.Range, "0,1")]
    public float RotationTriggerPercent = 0.8f;

    [Export]
    public float RotationDuration = 0.5f;

    public bool HasWaypointRotations
    {
        get
        {
            if (WaypointRotations == null || WaypointRotations.Length == 0)
                return false;
            foreach (var r in WaypointRotations)
            {
                if (r != Vector3.Zero)
                    return true;
            }
            return false;
        }
    }

    private Vector3 _startPosition;
    private int _currentWaypoint = 0;
    private int _direction = 1;
    private float _waitTimer = 0f;
    private bool _stopped = false;

    // Full path: origin + all waypoints
    // Index 0 = origin, index 1 = Waypoints[0], etc.
    private int _pathLength;

    // Segment rotation state
    private Vector3 _segmentStartPos;
    private float _segmentLength;
    private bool _segmentRotationTriggered = false;
    public bool IsWaypointRotating { get; private set; } = false;
    private float _rotationTimer = 0f;
    private Quaternion _rotationFrom;
    private Quaternion _rotationTo;

    public override void _Ready()
    {
        _startPosition = GlobalPosition;
        _pathLength = Waypoints.Length + 1; // +1 for origin
        _segmentStartPos = _startPosition;
        SyncToPhysics = false;
        if (_pathLength > 1)
            _segmentLength = (_startPosition - GetWaypointPosition(1)).Length();
        ApplyTint(this);
    }

    private void SetOriginAtCurrentLocation()
    {
        _startPosition = GlobalPosition;
        _segmentStartPos = _startPosition;
    }

    private void AddWaypointAtCurrentLocation()
    {
        Vector3 offset = GlobalPosition - _startPosition;
        Array.Resize(ref Waypoints, Waypoints.Length + 1);
        Waypoints[^1] = offset;

        // Snap back home so the node keeps representing its resting position —
        // drag to the next spot and click again to chain out the path.
        GlobalPosition = _startPosition;

        NotifyPropertyListChanged();
    }

    private void RemoveLastWaypoint()
    {
        if (Waypoints.Length == 0)
            return;

        Array.Resize(ref Waypoints, Waypoints.Length - 1);
        NotifyPropertyListChanged();
    }

    private void ApplyTint(Node node)
    {
        if (TintColor == new Color(1, 1, 1, 1))
            return;

        if (node is MeshInstance3D mesh)
        {
            for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
            {
                // Get the active material (override or from mesh resource)
                var mat = mesh.GetActiveMaterial(i);
                if (mat is StandardMaterial3D stdMat)
                {
                    var unique = (StandardMaterial3D)stdMat.Duplicate();
                    unique.AlbedoColor = TintColor;
                    mesh.SetSurfaceOverrideMaterial(i, unique);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            ApplyTint(child);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !Active)
            return;
        float dt = (float)delta;

        // Handle rotation
        if (SpinSpeed != 0f && SpinAxis != Vector3.Zero)
        {
            RotateObjectLocal(SpinAxis.Normalized(), Mathf.DegToRad(SpinSpeed * dt));
        }

        // Handle waypoint movement
        if (Waypoints.Length == 0 || _stopped)
            return;

        if (_waitTimer > 0f)
        {
            _waitTimer -= dt;
            return;
        }

        Vector3 target = GetWaypointPosition(_currentWaypoint);
        Vector3 toTarget = target - GlobalPosition;
        float distanceToMove = MoveSpeed * dt;

        // Check if we should trigger a waypoint rotation
        if (!_segmentRotationTriggered && WaypointRotations.Length > 0 && _segmentLength > 0f)
        {
            int rotIdx = _currentWaypoint - 1; // maps to Waypoints/WaypointRotations array
            if (
                rotIdx >= 0
                && rotIdx < WaypointRotations.Length
                && WaypointRotations[rotIdx] != Vector3.Zero
            )
            {
                float distCovered = (_segmentStartPos - GlobalPosition).Length();
                float segProgress = distCovered / _segmentLength;
                if (segProgress >= RotationTriggerPercent)
                {
                    _segmentRotationTriggered = true;
                    IsWaypointRotating = true;
                    _rotationTimer = 0f;
                    _rotationFrom = Quaternion;
                    var deg = WaypointRotations[rotIdx];
                    var eulerRad = new Vector3(
                        Mathf.DegToRad(deg.X),
                        Mathf.DegToRad(deg.Y),
                        Mathf.DegToRad(deg.Z)
                    );
                    _rotationTo = _rotationFrom * new Quaternion(Basis.FromEuler(eulerRad));
                }
            }
        }

        // Process smooth rotation
        if (IsWaypointRotating)
        {
            _rotationTimer += dt;
            float t = Mathf.Clamp(_rotationTimer / RotationDuration, 0f, 1f);
            t = t * t * (3f - 2f * t); // smoothstep easing
            Quaternion = _rotationFrom.Slerp(_rotationTo, t);
            if (t >= 1f)
                IsWaypointRotating = false;
        }

        if (toTarget.Length() <= distanceToMove)
        {
            GlobalPosition = target;
            _waitTimer = WaitTime;
            AdvanceWaypoint();
        }
        else
        {
            GlobalPosition += toTarget.Normalized() * distanceToMove;
        }
    }

    private Vector3 GetWaypointPosition(int index)
    {
        if (index == 0)
            return _startPosition;
        return _startPosition + Waypoints[index - 1];
    }

    private void AdvanceWaypoint()
    {
        _segmentStartPos = GetWaypointPosition(_currentWaypoint);

        switch (Mode)
        {
            case MovementMode.PingPong:
            {
                int next = _currentWaypoint + _direction;
                if (next >= _pathLength || next < 0)
                {
                    _direction *= -1;
                    next = _currentWaypoint + _direction;
                }
                _currentWaypoint = next;
                break;
            }
            case MovementMode.Loop:
            {
                _currentWaypoint = (_currentWaypoint + 1) % _pathLength;
                break;
            }
            case MovementMode.OneShot:
            {
                if (_currentWaypoint < _pathLength - 1)
                    _currentWaypoint++;
                else
                    _stopped = true;
                break;
            }
            case MovementMode.LoopSkipStart:
            {
                int next = _currentWaypoint + 1;
                // Wrap straight to Waypoints[0] (index 1) instead of the origin (index 0).
                if (next >= _pathLength)
                    next = 1;
                _currentWaypoint = next;
                break;
            }
        }

        _segmentLength = (GetWaypointPosition(_currentWaypoint) - _segmentStartPos).Length();
        _segmentRotationTriggered = false;
    }
}
