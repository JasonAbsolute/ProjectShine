using Godot;

public partial class CameraPivot : Node3D
{
    [Export]
    private CharacterBody3D _player; // Set this in the Godot editor by dragging the "Mario" node

    [Export]
    private float _followSpeed = 10.0f;

    [Export]
    private float _distance = 300.0f;

    [Export]
    private float _height = 3.0f;

    [Export]
    private float _rotationSpeed = 2.0f;

    [Export]
    private bool _invertY = false;

    [Export]
    private float _minDistance = 100.0f;

    [Export]
    private float _cameraAdjustSpeed = 15.0f;

    private Camera3D _camera;
    private float _yaw = 0.0f; // Horizontal rotation
    private float _pitch = 0.3f; // Vertical rotation (in radians, starting slightly upward)
    private Vector3 _currentCameraPos;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _currentCameraPos = new Vector3(0, 0, _distance);

        // Optional: Add a safety check to ensure _player is set
        if (_player == null)
        {
            GD.PushError("Player (Mario) not set in the editor for CameraPivot!");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null)
            return; // Safety check

        // Follow the player using GlobalTransform
        Vector3 targetPosition = _player.GlobalTransform.Origin + new Vector3(0, _height, 0);
        GlobalTransform = GlobalTransform.InterpolateWith(
            new Transform3D(Basis.Identity, targetPosition),
            (float)delta * _followSpeed
        );

        // Apply rotation (this will need to be updated via input from Mario.cs)
        Quaternion rotation =
            new Quaternion(Vector3.Up, _yaw) * new Quaternion(Vector3.Right, _pitch);
        Basis = new Basis(rotation);

        // Raycast for collision
        Vector3 cameraTargetPos = new Vector3(0, 0, _distance);
        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
        var rayQuery = new PhysicsRayQueryParameters3D
        {
            From = GlobalTransform.Origin,
            To = GlobalTransform * cameraTargetPos,
            CollideWithAreas = false,
            CollideWithBodies = true,
        };

        var result = spaceState.IntersectRay(rayQuery);
        if (result.Count > 0)
        {
            Vector3 hitPoint = (Vector3)result["position"];
            float newDistance = GlobalTransform.Origin.DistanceTo(hitPoint);
            cameraTargetPos.Z = Mathf.Max(_minDistance, newDistance);
        }

        // Smoothly adjust camera position
        _currentCameraPos = _currentCameraPos.Lerp(
            cameraTargetPos,
            (float)delta * _cameraAdjustSpeed
        );
        _camera.Position = _currentCameraPos;

        // Ensure camera faces the player
        _camera.LookAt(_player.GlobalTransform.Origin, Vector3.Up);
    }

    // Method to update camera rotation from Mario.cs (called externally)
    public void UpdateCameraRotation(float yawDelta, float pitchDelta)
    {
        _yaw -= yawDelta * _rotationSpeed;
        _pitch += _invertY ? pitchDelta * _rotationSpeed : -pitchDelta * _rotationSpeed;
        _pitch = Mathf.Clamp(_pitch, -Mathf.Pi / 4, Mathf.Pi / 2); // Limit vertical angle
    }
}
