using Godot;

/// <summary>
/// Unified script for all Super Mario Sunshine fruits (Banana, Coconut, Papaya,
/// Pineapple, Red Pepper, Durian). The carryable variants integrate with Mario's
/// existing RigidBody3D pickup system (group "pickable" + carrySocket attach).
///
/// Pass 112 (SMS 1:1): how a fruit reacts to MARIO is the game's TMapObjBall::touchActor (MapObjBall.cpp 299): while his hit cylinder
/// overlaps the fruit's and the fruit is not held, a Mario with vertical speed KICKS it (kicked(): a set hop, or a rebound of his own fall,
/// 0.9 of his run carried) and a Mario running into it BOUNCES it (boundByActor(): the per-type lift and push; a DIVING Mario onto it bounces it
/// off his own fall). The per-type numbers are the game's (initMapObj); the banana's are all zero -- it never bounces off him. Only the DURIAN
/// (and the game's football) answers back: Mario freezes 2 frames (mKickFreezeTime) and nothing else; the pickup stays B (a TAKE). The body's
/// own bounce / roll (the PhysicsMaterial profile below) is this port's, not the game's ball physics. LEGACY RETIRED: the durian-only kick
/// (Kick* exports: read by nothing, kept so the scenes load).
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
		SmsBallSetup();

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
		SmsBallTick(delta);
	}

	// ------------------------------------------------------------------------------------------------------------ pass 112: TMapObjBall
	/// <summary>The game's per-type ball numbers (TMapObjBall::initMapObj, MapObjBall.cpp 451-607), in units a FRAME: the push of Mario's run
	/// (unk148) and away from him (unk14C), the lift when he runs in fast (unk150) / slow (unk154), the dive case's run carry (unk158) and his
	/// fall's share (unk15C) and the rebound of the fruit's own fall (unk160), the kick's run carry (unk170), fall share (unk174) and set hop
	/// (unk178); the physical's min speed (unkC) and the body radius (units, x the scale).</summary>
	private struct SmsBallParams { public float Run, Away, LiftFast, LiftSlow, DiveRun, DiveFall, Rebound, KickRun, KickFall, KickHop, MinSpeed, BodyRadius, DamageRadius, DamageHeight; }
	private static readonly SmsBallParams BallCommon = new SmsBallParams { Run = 0.4f, Away = 0.2f, LiftFast = 1.3f, LiftSlow = 0f, DiveRun = 1.2f, DiveFall = 0.8f, Rebound = 0.5f, KickRun = 0.9f, KickFall = 0.13f, KickHop = 20f, MinSpeed = 0.3f, BodyRadius = 50f, DamageRadius = 80f, DamageHeight = 90f };
	private static SmsBallParams SmsBallParamsFor(FruitType t)
	{
		var p = BallCommon;
		switch (t)
		{
			case FruitType.Coconut: p.BodyRadius = 40f; p.DamageRadius = 50f; p.DamageHeight = 80f; break;   // coconut_hit_data_table 50 / 80
			case FruitType.Papaya: p.BodyRadius = 40f; break;
			case FruitType.Durian: p.Run = 0.6f; p.LiftSlow = 15f; p.DiveRun = 0.5f; p.DiveFall = 1.3f; p.Rebound = 1.0f; p.MinSpeed = 0.2f; p.BodyRadius = 45f; break;
			case FruitType.Banana: p = new SmsBallParams { MinSpeed = 1.0f, BodyRadius = 50f, DamageRadius = 80f, DamageHeight = 90f }; break;   // all zero: no bounce off him
		}
		return p;
	}
	private SmsBallParams _ball;
	private int _ballGuardFrames;          // unk194: 10 frames after a touch, no second one
	private float _ballFrameAccum;
	private Mario _ballMario;
	/// <summary>The last touch, for the drives: 0 none, 1 kicked, 2 bound (run-in), 3 bound (the dive case).</summary>
	public int SmsLastTouch { get; private set; }
	public int SmsTouchCount { get; private set; }
	/// <summary>How many times the touch test ran (the drives: is the 30 fps tick reaching it?).</summary>
	public int SmsTickCount { get; private set; }
	public Vector3 SmsLastTouchVelUnits { get; private set; }

	private void SmsBallSetup()
	{
		_ball = SmsBallParamsFor(Type);
		// The game's fruits carry NO collision against Mario (their TMapObjData's collision slot is null: MapObjInit.cpp coconut_data): he walks
		// THROUGH a fruit and only the hit cylinders react. The port's RigidBody was solid to him (layer 1 in his mask 5), which stopped him 60 u
		// short -- inside the touch radius but outside boundByActor's own mDamageRadius (50 u on a coconut), so a run-in could never bounce it.
		// With SmsPassThroughMario the body leaves layer 1 for layer 2 (the floor still catches it; Mario's mask 5 = layers 1 + 3 does not).
		if (SmsPassThroughMario) { CollisionLayer = (CollisionLayer & ~1u) | 2u; }
	}
	/// <summary>Pass 112: Mario walks through fruits as in the game (their bodies leave his collision mask); off = the port's solid fruits.</summary>
	[Export] public bool SmsPassThroughMario { get; set; } = true;

	/// <summary>DEVIATION knob (Jaysonn 2026-10-20 23:20 "all the fruit are kickable? shouldn't we have that as a flag?"): false = THIS fruit ignores
	/// Mario's touch -- no kick, no bounce, no durian freeze; picking it up is untouched. The game has no such flag: every TMapObjBall goes through
	/// touchActor (MapObjBall.cpp 299); what makes the banana inert is its all-zero table (initMapObj 583). true = the game.</summary>
	[Export] public bool SmsBallReacts { get; set; } = true;

	/// <summary>touchActor each 30 fps frame (the map objects' CUE_MOVE): Mario's cylinder (50 x 160 u) against the fruit's damage cylinder; a
	/// held fruit (STATE_HOLDING) and the guard skip it; kicked() when his mVel.y != 0, else boundByActor().</summary>
	private void SmsBallTick(double delta)
	{
		if (!SmsBallReacts) return;
		if (_ballMario == null || !IsInstanceValid(_ballMario)) _ballMario = GetTree().GetFirstNodeInGroup("player") as Mario;
		var mario = _ballMario;
		if (mario == null || !mario.UseSmsPhysics) return;
		_ballFrameAccum += (float)delta;
		int frames = 0;
		while (_ballFrameAccum >= 1f / 30f && frames < 4) { _ballFrameAccum -= 1f / 30f; frames++; }
		for (int f = 0; f < frames; f++)
		{
			if (_ballGuardFrames > 0) _ballGuardFrames--;
			if (_ballGuardFrames > 0 || Freeze) continue;        // unk194 / STATE_HOLDING (Mario's take sets Freeze = true on the held body)
			if (!SmsMarioTouches(mario)) continue;
			Vector3 mv = mario.SmsMarioVelUnits;
			if (mv.Y != 0f && Type != FruitType.Banana) SmsKicked(mario, mv);              // the banana: no kicked() case either (its numbers are 0)
			else SmsBoundByActor(mario, mv);
		}
	}

	/// <summary>The hit cylinders overlap: XZ within r_mario + r_fruit, the height spans crossing (THitActor::isHit's cylinder test).</summary>
	public bool SmsMarioTouches(Mario mario)
	{
		float u = SmsUnits.UnitsPerMetre;
		SmsTickCount++;
		Vector3 fp = GlobalPosition * u;
		float feet = mario.SmsFeetWorldY * u;
		Vector3 mp = new Vector3(mario.GlobalPosition.X * u, feet, mario.GlobalPosition.Z * u);
		float dx = mp.X - fp.X, dz = mp.Z - fp.Z;
		float r = Mario.SmsHitRadiusUnits + _ball.DamageRadius * Size;
		if (dx * dx + dz * dz > r * r) return false;
		float fBottom = fp.Y - _ball.BodyRadius * Size, fTop = fBottom + _ball.DamageHeight * Size;   // the fruit's cylinder stands on its bottom
		return feet < fTop && feet + Mario.SmsHitHeightUnits > fBottom;
	}

	private Vector3 SmsVelUnits => new Vector3(SmsUnits.MpsToSpeed(LinearVelocity.X), SmsUnits.MpsToSpeed(LinearVelocity.Y), SmsUnits.MpsToSpeed(LinearVelocity.Z));
	private void SmsSetVelUnits(Vector3 v) { LinearVelocity = new Vector3(SmsUnits.SpeedToMps(v.X), SmsUnits.SpeedToMps(v.Y), SmsUnits.SpeedToMps(v.Z)); Sleeping = false; }

	/// <summary>kicked() (MapObjBall.cpp 184): a fruit already rising is left alone; at rest it hops unk178 (20 u / frame), falling it rebounds
	/// -unk160 x its fall + unk174 x Mario's vy; xz += unk170 x his xz; both axes under the min speed -> a random unit xz; the guard; AIRBORNE;
	/// Mario's receiveMessage(ATTACK) -- the durian's freeze, nothing for the others (MarioReceiveMsg.cpp 877).</summary>
	private void SmsKicked(Mario mario, Vector3 mv)
	{
		Vector3 v = SmsVelUnits;
		if (Mathf.Abs(v.Y) < 0.05f) v.Y = 0f;                 // a Godot body at rest jitters by a hair; the game's ball sits at exactly 0
		if (v.Y > 0f) return;
		v.Y = v.Y == 0f ? _ball.KickHop : _ball.KickFall * mv.Y - _ball.Rebound * v.Y;
		v.X += _ball.KickRun * mv.X; v.Z += _ball.KickRun * mv.Z;
		if (Mathf.Abs(v.X) < _ball.MinSpeed && Mathf.Abs(v.Z) < _ball.MinSpeed) { v.X = GD.Randf() * 2f - 1f; v.Z = GD.Randf() * 2f - 1f; }
		SmsSetVelUnits(v);
		_ballGuardFrames = 10;
		SmsLastTouch = 1; SmsTouchCount++; SmsLastTouchVelUnits = v;
		if (Type == FruitType.Durian) mario.SmsKickFreeze();
	}

	/// <summary>boundByActor(actor = Mario) (MapObjBall.cpp 229): inside mDamageRadius in XZ; the offset to him normalised; a Mario faster than the
	/// min speed on an axis lifts it unk150 (else unk154) and it takes unk148 x his xz pushed unk14C away from him; receiveMessage(ATTACK). Then
	/// the DIVE case: his vy < 0 and his feet + 130 under the fruit's top -> vy = -unk160 x its own vy, xz += unk158 x his xz, vy += unk15C x his
	/// vy. The guard; AIRBORNE. (Nothing checks the fruit's own motion: a fruit rolled into also bounces.)</summary>
	private void SmsBoundByActor(Mario mario, Vector3 mv)
	{
		float u = SmsUnits.UnitsPerMetre;
		Vector3 off = new Vector3(mario.GlobalPosition.X - GlobalPosition.X, 0f, mario.GlobalPosition.Z - GlobalPosition.Z) * u;
		float r = _ball.DamageRadius * Size;
		if (off.X * off.X + off.Z * off.Z > r * r) return;
		if (off.X != 0f && off.Z != 0f) off = off.Normalized();
		Vector3 v = SmsVelUnits;
		bool fast = Mathf.Abs(mv.X) > _ball.MinSpeed || Mathf.Abs(mv.Z) > _ball.MinSpeed;
		v.Y += fast ? _ball.LiftFast : _ball.LiftSlow;
		v.X += _ball.Run * mv.X - off.X * _ball.Away;
		v.Z += _ball.Run * mv.Z - off.Z * _ball.Away;
		int kind = 2;
		if (Type == FruitType.Durian) mario.SmsKickFreeze();
		float feet = mario.SmsFeetWorldY * u, top = GlobalPosition.Y * u + _ball.BodyRadius * Size;
		if (mv.Y < 0f && feet + 130f < top)
		{
			v.Y = -_ball.Rebound * v.Y;
			v.X += _ball.DiveRun * mv.X; v.Y += _ball.DiveFall * mv.Y; v.Z += _ball.DiveRun * mv.Z;
			kind = 3;
		}
		SmsSetVelUnits(v);
		_ballGuardFrames = 10;
		SmsLastTouch = kind; SmsTouchCount++; SmsLastTouchVelUnits = v;
	}
}
