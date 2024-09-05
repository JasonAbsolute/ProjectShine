using Godot;
using System;

public partial class CameraController : Camera3D
{
    [Export] public NodePath TargetPath;
    [Export] public float FollowSpeed = 5.0f;
    [Export] public float RotationSpeed = 3.0f;
    [Export] public float MinDistance = 2.0f;
    [Export] public float MaxDistance = 5.0f;

    private Node3D _target;
    private Vector3 _currentOffset;

    public override void _Ready()
    {
        if (HasNode(TargetPath))
            _target = GetNode<Node3D>(TargetPath);
        
        _currentOffset = GlobalTransform.Origin - _target.GlobalTransform.Origin;
    }

    public override void _Process(double delta)
    {
        HandleCameraFollow((float)delta);
        HandleCameraRotation((float)delta);
    }

    private void HandleCameraFollow(float delta)
    {
        // Smoothly follow the target
        Vector3 targetPosition = _target.GlobalTransform.Origin + _currentOffset;
        GlobalTransform = new Transform3D(GlobalTransform.Basis, GlobalTransform.Origin.Lerp(targetPosition, FollowSpeed * delta));
    }

    private void HandleCameraRotation(float delta)
    {
        // Rotate camera based on player input
        if (Input.IsActionPressed("camera_rotate_left"))
            RotateY(Mathf.DegToRad(-RotationSpeed * delta));
        if (Input.IsActionPressed("camera_rotate_right"))
            RotateY(Mathf.DegToRad(RotationSpeed * delta));
    }
}
