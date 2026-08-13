using Godot;

/// <summary>
/// 1-Up Mushroom behavior — mirrors the SMS pickup item.
/// Supports four configurable modes set via StartMode in the inspector:
///   Wander      — wanders aimlessly, flees when Mario gets close
///   Flee        — always runs from Mario (set if spawned near him)
///   StayInPlace — sits still until Mario touches it
///   CirclePlayer— immediately starts circling (used internally on pickup)
/// </summary>
public partial class Mushroom1Up : CharacterBody3D
{
	public enum MushroomMode
	{
		Wander,
		Flee,
		StayInPlace,
		CirclePlayer,
		Collected,
	}

	// ── Inspector Exports ──────────────────────────────────────────────────

	[Export] public MushroomMode StartMode = MushroomMode.Wander;

	/// Cap base color — change this to recolor the whole mushroom (e.g. purple poison).
	[Export] public Color CapColor = new Color(0.0f, 0.72f, 0.0f);
	/// Lighter spot highlight color on the cap.
	[Export] public Color SpotColor = new Color(0.65f, 0.95f, 0.35f);

	[Export] public float WanderSpeed = 2.5f;
	[Export] public float FleeSpeed = 5.0f;
	[Export] public float WanderDirChangeMin = 1.5f;
	[Export] public float WanderDirChangeMax = 3.5f;

	/// How far Mario must be for the mushroom to start fleeing (radius in metres).
	[Export] public float FleeDetectionRadius = 6.0f;

	/// Orbit radius when circling Mario on pickup.
	[Export] public float CircleRadius = 0.55f;
	/// Orbit speed in radians per second.
	[Export] public float CircleSpeed = 14.0f;
	/// How many seconds the mushroom circles before granting a life.
	[Export] public float CircleDuration = 1.5f;

	// ── Private State ──────────────────────────────────────────────────────

	private static readonly Vector3 Gravity = new Vector3(0, -25f, 0);

	private MushroomMode _mode;
	private Vector3 _wanderDir = Vector3.Zero;
	private float _wanderTimer = 0f;
	private Node3D _mario = null;

	// Circle-phase data
	private float _circleAngle = 0f;
	private float _circleTimer = 0f;
	private Vector3 _circleCenter = Vector3.Zero;

	private Area3D _detectionZone;
	private Area3D _pickupZone;

	// ── Lifecycle ──────────────────────────────────────────────────────────

	public override void _Ready()
	{
		_mode = StartMode;

		_detectionZone = GetNode<Area3D>("DetectionZone");
		_detectionZone.BodyEntered += OnDetectionBodyEntered;
		_detectionZone.BodyExited += OnDetectionBodyExited;

		_pickupZone = GetNode<Area3D>("PickupZone");
		_pickupZone.BodyEntered += OnPickupBodyEntered;

		ApplyCapShader();

		if (_mode == MushroomMode.Wander)
			PickNewWanderDir();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// Circle and Collected states bypass physics entirely.
		if (_mode == MushroomMode.CirclePlayer)
		{
			ProcessCircle(dt);
			return;
		}
		if (_mode == MushroomMode.Collected)
			return;

		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity += Gravity * dt;

		switch (_mode)
		{
			case MushroomMode.Wander:
				velocity = ProcessWander(velocity, dt);
				break;
			case MushroomMode.Flee:
				velocity = ProcessFlee(velocity, dt);
				break;
			case MushroomMode.StayInPlace:
				velocity.X = 0;
				velocity.Z = 0;
				break;
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	// ── State Processing ───────────────────────────────────────────────────

	private Vector3 ProcessWander(Vector3 velocity, float dt)
	{
		_wanderTimer -= dt;
		if (_wanderTimer <= 0f)
			PickNewWanderDir();

		velocity.X = _wanderDir.X * WanderSpeed;
		velocity.Z = _wanderDir.Z * WanderSpeed;

		if (_wanderDir != Vector3.Zero)
			FaceDirection(_wanderDir, dt);

		return velocity;
	}

	private Vector3 ProcessFlee(Vector3 velocity, float dt)
	{
		if (!IsInstanceValid(_mario))
		{
			_mode = MushroomMode.Wander;
			PickNewWanderDir();
			return velocity;
		}

		Vector3 away = GlobalPosition - _mario.GlobalPosition;
		away.Y = 0;

		if (away.LengthSquared() > 0.01f)
		{
			Vector3 dir = away.Normalized();
			velocity.X = dir.X * FleeSpeed;
			velocity.Z = dir.Z * FleeSpeed;
			FaceDirection(dir, dt);
		}

		return velocity;
	}

	/// Orbits Mario, then grants 1-up and removes itself.
	private void ProcessCircle(float dt)
	{
		_circleAngle += CircleSpeed * dt;
		_circleTimer -= dt;

		// Update the center to follow Mario if he moves.
		if (IsInstanceValid(_mario))
			_circleCenter = _mario.GlobalPosition;

		Vector3 offset = new Vector3(
			Mathf.Sin(_circleAngle) * CircleRadius,
			2.2f,    // above Mario's head
			Mathf.Cos(_circleAngle) * CircleRadius
		);
		GlobalPosition = _circleCenter + offset;

		if (_circleTimer <= 0f)
			Collect();
	}

	private void Collect()
	{
		_mode = MushroomMode.Collected;

		if (IsInstanceValid(_mario) && _mario is Mario mario)
			mario.GainExtraLife();

		QueueFree();
	}

	// ── Shader / Visuals ───────────────────────────────────────────────────

	/// Walks the entire scene tree from this node to find cap meshes and
	/// overrides their materials with the palette-aware MushroomCap shader.
	private void ApplyCapShader()
	{
		var shader = GD.Load<Shader>("res://assets/MushroomCap.gdshader");
		if (shader == null)
		{
			GD.PushWarning("Mushroom1Up: MushroomCap.gdshader not found.");
			return;
		}

		// Walk from this node — works whether the GLB is an instanced "Model"
		// child OR baked directly into the scene (skeleton_root as direct child).
		WalkAndApplyShader(this, shader);
	}

	private void WalkAndApplyShader(Node node, Shader shader)
	{
		if (node is MeshInstance3D mesh)
		{
			int count = mesh.GetSurfaceOverrideMaterialCount();
			for (int i = 0; i < count; i++)
			{
				var mat = mesh.GetActiveMaterial(i);
				if (mat is BaseMaterial3D bmat)
				{
					string matName = bmat.ResourceName.ToLower();
					string texPath = bmat.AlbedoTexture?.ResourcePath ?? "";

					// Only recolor the cap mesh. The body/stem also uses the spot
					// texture but should stay white — skip anything named "body".
					bool isCapMesh = texPath.Contains("spot") && matName.Contains("head");
					if (!isCapMesh) continue;

					var shaderMat = new ShaderMaterial { Shader = shader };
					shaderMat.SetShaderParameter("cap_color", CapColor);
					shaderMat.SetShaderParameter("spot_color", SpotColor);
					mesh.SetSurfaceOverrideMaterial(i, shaderMat);
				}
			}
		}

		foreach (Node child in node.GetChildren())
			WalkAndApplyShader(child, shader);
	}

	// ── Helpers ────────────────────────────────────────────────────────────

	private void FaceDirection(Vector3 direction, float dt)
	{
		float target = Mathf.Atan2(direction.X, direction.Z);
		Rotation = new Vector3(
			Rotation.X,
			Mathf.LerpAngle(Rotation.Y, target, 10f * dt),
			Rotation.Z
		);
	}

	private void PickNewWanderDir()
	{
		float angle = (float)GD.RandRange(0.0, Mathf.Tau);
		_wanderDir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
		_wanderTimer = (float)GD.RandRange(WanderDirChangeMin, WanderDirChangeMax);
	}

	// ── Collision Callbacks ────────────────────────────────────────────────

	private void OnDetectionBodyEntered(Node3D body)
	{
		if (body is Mario && _mode is MushroomMode.Wander or MushroomMode.StayInPlace)
		{
			_mario = body;
			_mode = MushroomMode.Flee;
		}
	}

	private void OnDetectionBodyExited(Node3D body)
	{
		if (body != _mario) return;
		_mario = null;
		if (_mode == MushroomMode.Flee)
		{
			_mode = MushroomMode.Wander;
			PickNewWanderDir();
		}
	}

	private void OnPickupBodyEntered(Node3D body)
	{
		if (body is not Mario) return;
		if (_mode is MushroomMode.CirclePlayer or MushroomMode.Collected) return;

		_mario = body;
		_circleCenter = body.GlobalPosition;
		// Start orbiting from wherever the mushroom currently is.
		_circleAngle = Mathf.Atan2(
			GlobalPosition.X - body.GlobalPosition.X,
			GlobalPosition.Z - body.GlobalPosition.Z
		);
		_circleTimer = CircleDuration;
		_mode = MushroomMode.CirclePlayer;

		// Stop the pickup zone from firing again mid-circle.
		_pickupZone.SetDeferred(Area3D.PropertyName.Monitoring, false);

		// Kill collision so Mario can move freely under/through us during the orbit
		// AND so the camera's SpringArm raycast doesn't get pulled in by our body.
		SetDeferred(PropertyName.CollisionLayer, 0u);
		SetDeferred(PropertyName.CollisionMask, 0u);
		foreach (Node child in GetChildren())
			if (child is CollisionShape3D cs)
				cs.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
	}
}
