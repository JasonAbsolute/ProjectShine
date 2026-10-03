using Godot;

/// <summary>
/// Unified script for all Super Mario Sunshine fruits (Banana, Coconut, Papaya,
/// Pineapple, Red Pepper, Durian). The carryable variants integrate with Mario's
/// existing RigidBody3D pickup system (group "pickable" + carrySocket attach).
/// Pickup is B only: Mario's SmsFindTakeTarget (the game's canTake: the hit cylinders overlap within 80 + the fruit's radius,
/// within 60 degrees of his face) looks through the "pickable" group while B is held. No auto-pickup on contact.
/// Durian is the exception — it cannot be carried, only kicked.
///
/// Scene structure expected:
///   FruitX (RigidBody3D, this script attached)
///   ├── ModelRoot (Node3D from .glb instance)
///   └── CollisionShape3D (sphere — the body's solid shape)
///
/// (PickupZone Area3D is no longer required; safe to delete it if it exists.)
/// </summary>
public partial class Fruit : RigidBody3D
{
	public enum FruitType
	{
		Banana,
		Coconut,
		Papaya,
		Pineapple,
		RedPepper,
		Durian,
	}

	public enum YoshiColorEffect
	{
		Pink,
		Orange,
		Purple,
	}

	[Export]
	public FruitType Type { get; set; } = FruitType.Banana;

	/// <summary>False for Durian. Controls whether Mario can pick this up.</summary>
	[Export]
	public bool CanBePickedUp { get; set; } = true;

	/// <summary>The game's object scale (TMapObjBase::mScaling), uniform: the model is drawn at it, the hit cylinder is radius x
	/// max(sx, sz) and height x sy (setObjHitData, MapObjBase.cpp 70), the body radius / head height the same (MapObjInit.cpp 10985).
	/// 1 = the scene as authored. Applied once when the fruit enters the tree: the model and every collision shape under this body
	/// are scaled by it (the RigidBody3D itself stays at scale 1 -- Godot's bodies must; Mario's attach resets a body's scale to 1
	/// anyway), and Mario's take reach / put spacing read it through SmsTakeable.SizeOf. Set it per instance in a level.</summary>
	[Export]
	public float Size { get; set; } = 1f;

	/// <summary>Speed Mario must be running at for a Durian kick to fire.</summary>
	[Export]
	public float KickMinSpeed { get; set; } = 3.5f;

	/// <summary>Horizontal impulse magnitude applied when Mario kicks a Durian.</summary>
	[Export]
	public float KickImpulse { get; set; } = 14f;

	/// <summary>Slight upward component on Durian kick so it doesn't grind along the floor.</summary>
	[Export]
	public float KickUpImpulse { get; set; } = 3f;

	/// <summary>Cooldown between kicks so Mario doesn't keep re-kicking the same durian every frame.</summary>
	[Export]
	public float KickCooldown { get; set; } = 0.25f;

	/// <summary>Yoshi color this fruit feeds. Driven by Type.</summary>
	public YoshiColorEffect Color =>
		Type switch
		{
			FruitType.Banana or FruitType.Coconut => YoshiColorEffect.Pink,
			FruitType.Papaya or FruitType.Pineapple => YoshiColorEffect.Orange,
			FruitType.RedPepper or FruitType.Durian => YoshiColorEffect.Purple,
			_ => YoshiColorEffect.Orange,
		};

	private float _kickTimer = 0f;

	public override void _Ready()
	{
		ApplySize();
		if (CanBePickedUp)
		{
			// Pickup happens via B only: Mario's SmsFindTakeTarget looks through this group while B is held.
			// No auto-pickup-on-walk-into — that's reserved for things like coins.
			AddToGroup("pickable");
		}
		else
		{
			// Durian needs to detect collisions with Mario to kick.
			ContactMonitor = true;
			MaxContactsReported = 4;
		}

		// Red Pepper: SMS quirk — it never spins when thrown or kicked. Lock all
		// angular axes at the physics-server level and snap rotation to identity.
		if (Type == FruitType.RedPepper)
		{
			LockRotation = true;
			Rotation = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
		}

		ApplyPhysicsProfile();
	}

	/// <summary>mScaling applied to the children: the model root(s) by their transform, a collision shape by its own dimensions (a
	/// duplicate of the shared sub-resource, so one big coconut does not resize every coconut), an Area3D's shapes the same.</summary>
	private void ApplySize()
	{
		if (Size <= 0f || Mathf.IsEqualApprox(Size, 1f))
			return;
		foreach (Node c in GetChildren())
			ScaleChild(c, Size);
	}

	private static void ScaleChild(Node c, float s)
	{
		switch (c)
		{
			case CollisionShape3D cs:
				cs.Position *= s;
				if (cs.Shape == null)
					break;
				var shape = (Shape3D)cs.Shape.Duplicate();
				switch (shape)
				{
					case SphereShape3D sp: sp.Radius *= s; break;
					case BoxShape3D bx: bx.Size *= s; break;
					case CapsuleShape3D cap: cap.Radius *= s; cap.Height *= s; break;
					case CylinderShape3D cyl: cyl.Radius *= s; cyl.Height *= s; break;
					case ConvexPolygonShape3D cv:
					{
						var pts = cv.Points;
						for (int i = 0; i < pts.Length; i++)
							pts[i] *= s;
						cv.Points = pts;
						break;
					}
					default: cs.Scale *= s; break; // a mesh shape: the node's (uniform) scale
				}
				cs.Shape = shape;
				break;
			case CollisionObject3D area: // a PickupZone-style Area3D: its shapes by the same rule
				foreach (Node g in area.GetChildren())
					ScaleChild(g, s);
				break;
			case Node3D model:
				model.Scale *= s;
				model.Position *= s;
				break;
		}
	}

	/// <summary>
	/// Per-fruit physics tuning — mass, gravity feel, bounce, ground damping.
	/// Captures the unique SMS feel of each fruit:
	/// - Banana: light, flat throw, slides on landing
	/// - Coconut: bouncy, rolls
	/// - Papaya: soft, splats with little bounce
	/// - Pineapple: heavy, thumps and stays put
	/// - Red Pepper: very floaty, slow fall
	/// - Durian: heavy, dead bounce (kick-only)
	/// </summary>
	private void ApplyPhysicsProfile()
	{
		float bounce = 0f;
		float friction = 1f;

		switch (Type)
		{
			case FruitType.Banana:
				Mass = 0.5f;
				GravityScale = 0.85f;
				LinearDamp = 0.4f;
				AngularDamp = 1.0f;
				bounce = 0.12f;
				friction = 0.3f; // slides
				break;
			case FruitType.Coconut:
				Mass = 1.5f;
				GravityScale = 1.0f;
				LinearDamp = 0.2f;
				AngularDamp = 0.3f;
				bounce = 0.55f; // bouncy
				friction = 0.6f;
				break;
			case FruitType.Papaya:
				Mass = 1.0f;
				GravityScale = 1.0f;
				LinearDamp = 2.2f; // kills momentum fast
				AngularDamp = 2.0f;
				bounce = 0.05f; // splats
				friction = 0.9f;
				break;
			case FruitType.Pineapple:
				Mass = 2.5f;
				GravityScale = 1.2f; // heavier feel
				LinearDamp = 3.5f; // stops dead
				AngularDamp = 3.0f;
				bounce = 0.0f;
				friction = 1.0f;
				break;
			case FruitType.RedPepper:
				Mass = 0.3f;
				GravityScale = 0.4f; // very floaty
				LinearDamp = 1.2f;
				AngularDamp = 0.0f; // n/a (rotation locked)
				bounce = 0.15f;
				friction = 0.5f;
				break;
			case FruitType.Durian:
				Mass = 3.0f;
				GravityScale = 1.15f;
				LinearDamp = 1.5f;
				AngularDamp = 1.0f;
				bounce = 0.1f; // dead bounce
				friction = 0.95f;
				break;
		}

		// Apply bounce/friction via a PhysicsMaterialOverride so it works at the
		// physics-server level for all collisions, not just specific contacts.
		var mat = new PhysicsMaterial
		{
			Bounce = bounce,
			Friction = friction,
		};
		PhysicsMaterialOverride = mat;
	}

	/// <summary>
	/// Public entry point for spawners (e.g. nails or palm-tree shake) to pop the
	/// fruit upward with a small bounce. Mirrors the coin LaunchAsDrop pattern.
	/// </summary>
	public void LaunchAsDrop()
	{
		LinearVelocity = new Vector3(0f, BouncePhysics.INITIAL_POP_Y, 0f);
		// Add a tiny random XZ scatter so multiple fruits don't stack
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		LinearVelocity += new Vector3(rng.RandfRange(-1.5f, 1.5f), 0f, rng.RandfRange(-1.5f, 1.5f));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (CanBePickedUp)
			return; // only Durian past this point

		if (_kickTimer > 0f)
			_kickTimer -= (float)delta;

		// Look for Mario in the contacts. RigidBody3D.GetCollidingBodies requires
		// ContactMonitor = true (we set it in _Ready).
		foreach (var contact in GetCollidingBodies())
		{
			if (contact is not Mario mario)
				continue;

			Vector3 horizVel = new Vector3(mario.Velocity.X, 0f, mario.Velocity.Z);
			if (horizVel.LengthSquared() < KickMinSpeed * KickMinSpeed)
				continue;
			if (_kickTimer > 0f)
				continue;

			// Kick direction = away from Mario, in the direction he was moving
			Vector3 kickDir = horizVel.Normalized();
			ApplyImpulse(kickDir * KickImpulse + Vector3.Up * KickUpImpulse);
			_kickTimer = KickCooldown;
			break;
		}
	}
}
