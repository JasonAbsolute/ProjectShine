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
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		Vector3 v = new Vector3(rng.RandfRange(-1.5f, 1.5f), BouncePhysics.INITIAL_POP_Y, rng.RandfRange(-1.5f, 1.5f)); // a tiny XZ scatter so fruits don't stack
		if (_bSim) { SmsBallLaunch(v); return; }   // pass 115: the ball sim owns the motion
		LinearVelocity = v;
	}

	public override void _PhysicsProcess(double delta)
	{
		SmsBallTick(delta);
	}

	// ------------------------------------------------------------------------------------------------------ pass 112 / 115: TResetFruit
	/// <summary>The game's per-type ball numbers (TMapObjBall::initMapObj, MapObjBall.cpp 451-607), units a 120 Hz TICK: the push of Mario's run
	/// (unk148) and away from him (unk14C), the lift when he runs in fast (unk150) / slow (unk154), the rebound of the fruit's own fall (unk160),
	/// the kick's run carry (unk170), fall share (unk174) and set hop (unk178), the wall pop (unk184) and the landing threshold (unk188); from the
	/// type's TMapObjPhysicalData (MapObjInit.cpp coconut_physical_data 1543 ...): gravity, the floor / roof restitution (unk4), the wall
	/// reflection (unk8), the min speed (unkC), the ground friction (unk10), the air drag (unk18, 1.0 = none); the body radius (units, x the
	/// scale) and the hit cylinder. Durian 0x00170100 is the ONLY fruit without MAP_OBJ_FLAG_UNK2000000 -> the only one Mario's body moves.</summary>
	private struct SmsBallParams
	{
		public float Run, Away, LiftFast, LiftSlow, Rebound, KickRun, KickFall, KickHop, WallPop, LandVy;
		public float Gravity, Bounce, WallReflect, MinSpeed, Friction, AirDrag, BodyRadius, DamageRadius, DamageHeight, AttackRadius, AttackHeight;
		public bool AnswersMario;
	}
	private static readonly SmsBallParams BallCommon = new SmsBallParams
	{
		Run = 0.4f, Away = 0.2f, LiftFast = 1.3f, LiftSlow = 0f, Rebound = 0.5f, KickRun = 0.9f, KickFall = 0.13f, KickHop = 20f, WallPop = 1.0f, LandVy = 1.5f,
		Gravity = 0.3f, Bounce = 0.6f, WallReflect = 0.3f, MinSpeed = 0.3f, Friction = 0.97f, AirDrag = 1.0f, BodyRadius = 50f, DamageRadius = 80f, DamageHeight = 90f, AttackRadius = 60f, AttackHeight = 60f, AnswersMario = false   // fruit_hit_data_table (MapObjInit.cpp 1536, the red pepper's): {60, 60, 80, 90}
	};
	private static SmsBallParams SmsBallParamsFor(FruitType t)
	{
		var p = BallCommon;
		switch (t)
		{
			case FruitType.Coconut: p.BodyRadius = 40f; p.AttackRadius = 40f; p.DamageRadius = 50f; p.DamageHeight = 80f; break;      // coconut_hit_data_table 1553 {40, 60, 50, 80}
			case FruitType.Papaya: p.BodyRadius = 40f; p.AttackRadius = 40f; p.DamageRadius = 50f; p.DamageHeight = 80f; break;       // papaya_hit_data_table 1594 {40, 60, 50, 80}
			case FruitType.Pineapple: p.AttackRadius = 80f; p.DamageRadius = 50f; p.DamageHeight = 80f; break;                        // pine_hit_data_table 1629 {80, 60, 50, 80}; R 50
			case FruitType.RedPepper: break;                                                                                             // RedPepper_data -> fruit_obj_hit_info: the common row
			case FruitType.Durian:   // durian_physical_data: unk8 / unkC 0.2; initMapObj 505: run 0.6, slow lift 15, rebound 1.0; R 45; the one that answers him
				p.Run = 0.6f; p.LiftSlow = 15f; p.Rebound = 1.0f; p.WallReflect = 0.2f; p.MinSpeed = 0.2f; p.BodyRadius = 45f; p.AnswersMario = true;
				p.AttackRadius = 150f; p.AttackHeight = 60f; p.DamageRadius = 60f; p.DamageHeight = 90f; break;                             // durian_hit_data_table 1702 {150, 60, 60, 90}
			case FruitType.Banana:   // banana_physical_data: bounce 0.4, wall 0.2, min speed 1.0, friction 0.4; its touch table is all zero
				p.Run = 0.2f; p.Away = 0f; p.LiftFast = 0f; p.Rebound = 0f; p.KickRun = 0f; p.KickFall = 0f; p.KickHop = 0f;
				p.Bounce = 0.4f; p.WallReflect = 0.2f; p.MinSpeed = 1.0f; p.Friction = 0.4f;
				p.AttackRadius = 80f; p.AttackHeight = 60f; p.DamageRadius = 60f; p.DamageHeight = 90f; break;                              // banana_hit_data_table 1743 {80, 60, 60, 90}
		}
		return p;
	}
	private SmsBallParams _ball;
	/// <summary>Pass 112: Mario walks through fruits as in the game (their TMapObjData has no map collision: he never collides with one); off =
	/// the port's solid fruits.</summary>
	[Export] public bool SmsPassThroughMario { get; set; } = true;
	/// <summary>DEVIATION knob (Jaysonn 2026-10-20 23:20): false = THIS fruit ignores Mario's touch entirely; picking it up is untouched.</summary>
	[Export] public bool SmsBallReacts { get; set; } = true;
	/// <summary>Pass 115: the game's own ball physics (TMapObjBall: gravity 0.3 a tick^2, the 0.6 floor bounce, the 0.97 roll-out, the downhill
	/// roll, the wall pop + reflection, the roof) drive this fruit while Mario runs SMS physics; the RigidBody3D is frozen kinematic and moved by
	/// the sim. false = his Godot RigidBody profile (ApplyPhysicsProfile) as before.</summary>
	[Export] public bool SmsBallPhysics { get; set; } = true;
	/// <summary>Pass 115c: the game scales an object's hit cylinders with its model (TMapObjBase::setObjHitData: radii x max(sx, sz), heights x sy).
	/// Your fruit models at Size 1 are about half the game's (coconut sphere 23.6 u vs the game's body radius 40, durian 20.6 vs 45, pineapple 24.7 vs
	/// 50, banana 19.5 vs 50), so the scale is read off the collision sphere: sphere radius / the game's body radius (Size is already baked into the
	/// sphere). false = the game's raw numbers x Size (passes 115 / 115b: a hold-off bubble well outside the model).</summary>
	[Export] public bool SmsHitScaleFromModel { get; set; } = true;
	/// <summary>The hit cylinders' scale (see SmsHitScaleFromModel).</summary>
	public float SmsHitScale => SmsHitScaleFromModel && _bGeomR > 0f ? _bGeomR * SmsUnits.UnitsPerMetre / _ball.BodyRadius : Size;
	/// <summary>keepDistance's hold-off radius about the axis, units: the scaled damage radius + Mario's 50.</summary>
	public float SmsHoldOffRadiusU => _ball.DamageRadius * SmsHitScale + SmsParams.Mario.KeepDistanceRadius;
	/// <summary>DEVIATION knob (pass 115): true = this fruit answers Mario's body the way the game's DURIAN does (a run-in pushes it, a rising jump
	/// kicks it) -- pass 112's look for every fruit. The game: TResetFruit::kicked (MapObjBall.cpp 795) and boundByActor's Mario branch (245)
	/// both return on MAP_OBJ_FLAG_UNK2000000, which every fruit but the durian carries (MapObjInit.cpp: coconut / papaya / pine / banana
	/// 0x02130100, red pepper 0x02130000, durian 0x00170100) -- a coconut sits still when he walks into or lands on it.</summary>
	[Export] public bool SmsTouchLikeDurian { get; set; } = false;
	/// <summary>Sound slots (no streams yet): the ball's own bound / roll (mSoundIdTable[4]) and MSD_SE_MA_KICK_DRIAN.</summary>
	[Export] public AudioStreamPlayer3D SmsBounceSound, SmsKickSound;

	private bool _bSim;                        // the sim drives the body (frozen kinematic)
	private Vector3 _bVel;                     // mVelocity, units a tick
	private bool _bAirborne, _bRest, _bHeld;   // LIVE_FLAG_AIRBORNE, LIVE_FLAG_UNK10 (bind skipped), STATE_HOLDING
	private int _bGuard;                       // unk194: 10 ticks after a touch, no second one
	private float _bTickAccum;
	private float _bGeomR = 0.3f;              // metres: HIS collision sphere's radius (the ground / wall geometry; the game's R is for the numbers)
	private Vector3 _bGroundN = Vector3.Up;
	private Node3D _bModel; private Basis _bModelBasis0 = Basis.Identity; private Basis _bRoll = Basis.Identity;
	private Mario _ballMario;
	/// <summary>The last touch, for the drives: 0 none, 1 kicked, 2 bound (run-in).</summary>
	public int SmsLastTouch { get; private set; }
	public int SmsTouchCount { get; private set; }
	public int SmsTickCount { get; private set; }
	public Vector3 SmsLastTouchVelUnits { get; private set; }
	/// <summary>For the drives: the sim's state.</summary>
	public bool SmsBallSimActive => _bSim;
	public Vector3 SmsBallVelUnits => _bVel;
	public bool SmsBallAirborne => _bAirborne;
	public bool SmsBallAtRest => _bRest;
	public bool SmsBallHeld => _bHeld;
	public int SmsBallGuard => _bGuard;
	public float SmsBallBottomY => GlobalPosition.Y - _bGeomR;
	public float SmsBallGeomRadius => _bGeomR;
	public Basis SmsBallRoll => _bRoll;
	/// <summary>TMapObjBall::hold (174): a ball faster than 10 units a tick cannot be grabbed.</summary>
	public bool SmsHoldable => !_bSim || _bVel.Length() <= 10f;
	/// <summary>Does this fruit answer Mario's body (the game: the durian; the knob: any).</summary>
	public bool SmsAnswersMario => SmsBallReacts && (_ball.AnswersMario || SmsTouchLikeDurian);

	private void SmsBallSetup()
	{
		_ball = SmsBallParamsFor(Type);
		// The game's fruits carry NO collision against Mario (their TMapObjData's collision slot is null: MapObjInit.cpp coconut_data): he walks
		// THROUGH a fruit and only the hit cylinders react. With SmsPassThroughMario the body leaves layer 1 for layer 2 (the floor still catches it;
		// Mario's mask 5 = layers 1 + 3 does not).
		if (SmsPassThroughMario) { CollisionLayer = (CollisionLayer & ~1u) | 2u; }
		foreach (Node c in GetChildren())
		{
			if (c is CollisionShape3D cs && cs.Shape is SphereShape3D sph) _bGeomR = sph.Radius * Mathf.Abs(cs.Scale.Y);   // ApplySize already baked Size into the radius
			else if (_bModel == null && c is Node3D n3 && c is not CollisionShape3D && c is not CollisionObject3D) { _bModel = n3; }
		}
		if (_bModel != null) _bModelBasis0 = _bModel.Basis;
	}

	// ---- the hooks Mario's take calls (hold / put / thrown; TMapObjBall::hold 174, TMapObjGeneral::put 92 / thrown 110) ----
	public void SmsHold() { _bHeld = true; _bVel = Vector3.Zero; _bRest = true; _bAirborne = false; }
	public void SmsPut()
	{
		_bHeld = false; _bVel = Vector3.Zero; _bAirborne = true; _bRest = false; _bGuard = 0;
		if (_bSim) { Freeze = true; FreezeMode = FreezeModeEnum.Kinematic; }
	}
	/// <summary>thrown(): the velocity Mario's code computed (m/s; the game's per-tick launch), already led one tick (his code).</summary>
	public void SmsThrown(Vector3 velMps)
	{
		_bHeld = false; _bAirborne = true; _bRest = false; _bGuard = 0;
		_bVel = SmsBallMpsToTick(velMps);
		if (_bSim) { Freeze = true; FreezeMode = FreezeModeEnum.Kinematic; }
	}
	/// <summary>A spawner's pop (LaunchAsDrop) while the sim runs.</summary>
	private void SmsBallLaunch(Vector3 velMps) { _bVel = SmsBallMpsToTick(velMps); _bAirborne = true; _bRest = false; }
	private static Vector3 SmsBallMpsToTick(Vector3 mps) => mps * (SmsUnits.UnitsPerMetre / SmsUnits.TicksPerSecond);

	/// <summary>Every physics frame: the 120 Hz ticks (TicksFor); each = TMapObjBase::control's touchActor + TMapObjGeneral::bind.</summary>
	private void SmsBallTick(double delta)
	{
		if (_ballMario == null || !IsInstanceValid(_ballMario)) _ballMario = GetTree().GetFirstNodeInGroup("player") as Mario;
		var mario = _ballMario;
		if (mario == null || !mario.UseSmsPhysics) return;
		if (!_bSim)
		{
			if (!SmsBallPhysics) { SmsBallTouchOnly(delta, mario); return; }
			// take the body over: its Godot velocity becomes the ball's, the body goes kinematic (moved by GlobalPosition from here on)
			_bVel = SmsBallMpsToTick(LinearVelocity);
			_bAirborne = true; _bRest = false;
			_bHeld = TopLevel;                  // Mario's take sets TopLevel on the held body
			Freeze = true; FreezeMode = FreezeModeEnum.Kinematic;
			_bSim = true;
		}
		if (!_bHeld && !Freeze) { Freeze = true; FreezeMode = FreezeModeEnum.Kinematic; }   // a release path that unfroze it: the sim keeps it kinematic
		// the game's fruit has NO map collision (MapObjInit.cpp coconut_data: the TMapCollisionInfo slot is nullptr, where WoodBox carries one):
		// Mario is never blocked by a fruit and cannot stand on one -- he meets it through the hit cylinders alone (touchActor). The sim's
		// kinematic body is excepted from his MoveAndSlide every frame (his throw adds the same exception and REMOVES it 0.6 s later).
		AddCollisionExceptionWith(mario);
		int n = SmsUnits.TicksFor(delta, ref _bTickAccum);
		for (int t = 0; t < n; t++) SmsBallStep(mario);
	}

	/// <summary>The knob off: the 112 touch reactions alone (the RigidBody keeps the motion), at the game's tick.</summary>
	private void SmsBallTouchOnly(double delta, Mario mario)
	{
		int n = SmsUnits.TicksFor(delta, ref _bTickAccum);
		for (int t = 0; t < n; t++)
		{
			if (_bGuard > 0) _bGuard--;
			if (!TopLevel) SmsHoldMarioOff(mario);
			if (_bGuard > 0 || Freeze || TopLevel || !SmsAnswersMario) continue;
			if (!SmsMarioTouches(mario)) continue;
			Vector3 mv = mario.SmsMarioVelUnits;
			Vector3 v = SmsBallMpsToTick(LinearVelocity);
			if (Mathf.Abs(v.Y) < 0.05f) v.Y = 0f;
			bool did = mv.Y != 0f ? SmsKicked(mario, mv, ref v, false) : SmsBoundByActor(mario, mv, ref v);
			if (did) { LinearVelocity = v * (SmsUnits.TicksPerSecond / SmsUnits.UnitsPerMetre); Sleeping = false; }
		}
	}

	private void SmsBallStep(Mario mario)
	{
		SmsTickCount++;
		if (_bGuard > 0) _bGuard--;
		bool held = _bHeld || TopLevel;
		// TMapObjBase::control (MapObjBase.cpp 399): touchActor for each collision this tick -> TResetFruit::pick -> TMapObjBall::touchActor (299)
		if (!held && _bGuard == 0 && SmsAnswersMario && SmsMarioTouches(mario))
		{
			Vector3 mv = mario.SmsMarioVelUnits;
			if (mv.Y != 0f) SmsKicked(mario, mv, ref _bVel, _bAirborne); else SmsBoundByActor(mario, mv, ref _bVel);
		}
		if (!held) SmsHoldMarioOff(mario);
		if (held || _bRest) return;                                      // STATE_HOLDING: the holder's matrix; LIVE_FLAG_UNK10: bind returns

		// ---- TMapObjGeneral::bind (500): calcVelocity (475) ----
		float R = _ball.BodyRadius * Size;
		if (_bAirborne) { _bVel.Y -= _ball.Gravity; _bVel.Y = Mathf.Clamp(_bVel.Y, -R, R); }
		_bVel.X = Mathf.Clamp(_bVel.X * _ball.AirDrag, -R, R);
		_bVel.Z = Mathf.Clamp(_bVel.Z * _ball.AirDrag, -R, R);
		if (_bGroundN.Y >= 0.9999f)
		{
			if (Mathf.Abs(_bVel.X) < _ball.MinSpeed) _bVel.X = 0f;
			if (Mathf.Abs(_bVel.Z) < _ball.MinSpeed) _bVel.Z = 0f;
		}
		// ---- pos += vel (metres); the game's mPosition is the ball's BOTTOM ----
		Vector3 bottom = GlobalPosition - Vector3.Up * _bGeomR + _bVel * (1f / SmsUnits.UnitsPerMetre);
		var space = GetWorld3D().DirectSpaceState;
		var excl = new Godot.Collections.Array<Rid> { GetRid(), mario.GetRid() };   // the map only: never Mario's body
		// ---- checkGroundCollision (464): the floor under the HEAD point, + 1 unit ----
		Vector3 headPt = bottom + Vector3.Up * (2f * _bGeomR);
		var gq = PhysicsRayQueryParameters3D.Create(headPt, headPt - Vector3.Up * 200f, CollisionMask, excl);
		var ghit = space.IntersectRay(gq);
		bool grounded = false;
		if (ghit.Count > 0 && SmsBallIsMap(ghit["collider"].AsGodotObject()))
		{
			float groundY = ((Vector3)ghit["position"]).Y + SmsUnits.UnitsToMetres(1f);
			Vector3 gn = (Vector3)ghit["normal"];
			if (bottom.Y <= groundY) { grounded = true; _bGroundN = gn; SmsBallTouchGround(ref bottom, gn, groundY); }
		}
		if (!grounded) _bAirborne = true;
		// ---- checkWallCollision (357): a wall within R of the centre -> pushed out in XZ, touchWall (47) ----
		Vector3 centre = bottom + Vector3.Up * _bGeomR;
		var sq = new PhysicsShapeQueryParameters3D { Shape = _bWallShape ??= new SphereShape3D(), CollisionMask = CollisionMask, Exclude = excl, Transform = new Transform3D(Basis.Identity, centre) };
		((SphereShape3D)sq.Shape).Radius = _bGeomR;
		var rest = space.GetRestInfo(sq);
		if (rest.Count > 0 && Mathf.Abs(((Vector3)rest["normal"]).Y) < 0.7f && SmsBallIsMap(GodotObject.InstanceFromId(rest["collider_id"].AsUInt64())))
		{
			Vector3 wn = (Vector3)rest["normal"]; wn.Y = 0f;
			if (wn.LengthSquared() > 1e-6f)
			{
				wn = wn.Normalized();
				Vector3 wp = (Vector3)rest["point"];
				float pen = _bGeomR - (centre - wp).Dot(wn);
				if (pen > 0f) { centre += wn * pen; bottom = centre - Vector3.Up * _bGeomR; }
				SmsBallTouchWall(wn);
			}
		}
		// ---- checkRoofCollision (429), while rising: the head against a roof, reflected with unk4 ----
		if (_bVel.Y > 0f)
		{
			var rq = PhysicsRayQueryParameters3D.Create(centre, centre + Vector3.Up * (_bGeomR + 0.02f), CollisionMask, excl);
			var rhit = space.IntersectRay(rq);
			if (rhit.Count > 0 && SmsBallIsMap(rhit["collider"].AsGodotObject()))
			{
				float roofY = ((Vector3)rhit["position"]).Y;
				bottom.Y = roofY - 2f * _bGeomR;
				Vector3 rn = (Vector3)rhit["normal"];
				_bVel -= rn * (_bVel.Dot(rn) * (1f + _ball.Bounce));
				SmsBounceSound?.Play();
			}
		}
		if (!_bAirborne && _bVel == Vector3.Zero) _bRest = true;        // bind's end: on the ground and still -> LIVE_FLAG_UNK10
		GlobalPosition = bottom + Vector3.Up * _bGeomR;
		// ---- calcCurrentMtx (318): the roll, |vxz| / R x 2 about the axis across the motion ----
		float sp = new Vector2(_bVel.X, _bVel.Z).Length();
		if (_bModel != null && (Mathf.Abs(_bVel.X) > _ball.MinSpeed || Mathf.Abs(_bVel.Z) > _ball.MinSpeed))
		{
			Vector3 dir = new Vector3(_bVel.X, 0f, _bVel.Z) / sp;
			Vector3 axis = Vector3.Up.Cross(dir);                                 // getVerticalVecToTargetXZ: the direction turned +90 deg about Y = up x d, WORLD space
			if (axis.LengthSquared() > 1e-8f)
			{
				float ang = SmsUnits.UnitsToMetres(sp) / _bGeomR * 2f;           // his sphere's radius: the model rolls (x2, the game's own factor)
				// calcCurrentMtx concatenates the roll in WORLD space (MTXConcat(rot, anmMtx)); the model child's Basis is in the BODY's frame, and the
				// body keeps whatever orientation it last had (Mario's hand at a throw: the sim moves GlobalPosition only) -- so the axis goes through
				// the body's inverse basis (pass 115c: a thrown fruit spun about a yawed axis)
				Vector3 axisL = GlobalTransform.Basis.Orthonormalized().Inverse() * axis;
				_bRoll = new Basis(axisL.Normalized(), ang) * _bRoll;
				_bModel.Basis = _bRoll * _bModelBasis0;
			}
		}
	}
	private SphereShape3D _bWallShape;
	/// <summary>The game's ball collides with the map (BG) only: not Mario, not another body, not an area.</summary>
	private static bool SmsBallIsMap(GodotObject o) => o is Node && o is not RigidBody3D && o is not CharacterBody3D && o is not Area3D;

	/// <summary>TMapObjBall::touchGround (118): (water / goop: the game respawns the fruit -- not ported, it rolls on) vy above -unk188 lands (vy
	/// 0, AIRBORNE off), else rebound (91: reflected about the floor with 1 + unk4, AIRBORNE on); on the ground vxz += unk180 x normal.xz; vxz x=
	/// unk10 on every contact.</summary>
	private void SmsBallTouchGround(ref Vector3 bottom, Vector3 n, float groundY)
	{
		if (_bVel.Y > -_ball.LandVy) { _bAirborne = false; _bVel.Y = 0f; bottom.Y = groundY; }
		else
		{
			_bVel -= n * (_bVel.Dot(n) * (1f + _ball.Bounce));
			bottom.Y = groundY; _bAirborne = true;
			SmsBounceSound?.Play();
		}
		if (!_bAirborne) { _bVel.X += 0.5f * n.X; _bVel.Z += 0.5f * n.Z; }   // unk180 = 0.5 for every fruit: it rolls downhill
		_bVel.X *= _ball.Friction; _bVel.Z *= _ball.Friction;
	}

	/// <summary>TMapObjBall::touchWall (47): a rolling ball pops vy += unk184 x speed; the velocity into the wall reflects with unk8 (see the
	/// generator's note on the decomp's sign).</summary>
	private void SmsBallTouchWall(Vector3 wn)
	{
		if (!_bAirborne) _bVel.Y += _ball.WallPop * _bVel.Length();
		float velDot = _bVel.X * wn.X + _bVel.Z * wn.Z;
		if (velDot < 0f)
		{
			float k = (1f + _ball.WallReflect) * velDot;
			_bVel.X -= k * wn.X; _bVel.Z -= k * wn.Z;
			SmsBounceSound?.Play();
		}
	}

	/// <summary>The hit cylinders overlap (THitActor::isHit): XZ within r_mario + r_fruit, the height spans crossing; the fruit's stands on its bottom.</summary>
	/// <summary>TMario::checkCollision (MarioCheckCol.cpp 487-493): coconut / papaya / pine / banana / red pepper -> hitWantToTake =
	/// keepDistance(us, 0) + the B take: inside our damage radius + 50 (his wall radius) of our axis he is set back out to that distance in XZ,
	/// 50 units a tick at most -- never through us, though we have no map collision; running in above ClashSpeed (40: the turbo dash) is the
	/// clash knockdown. The durian (495) -> hitPushup / hitNormal, NO keepDistance: the durian is the one moved (boundByActor / kicked). So the
	/// fruit that does not answer his body is the one that holds him off (SmsTouchLikeDurian moves instead; SmsBallReacts off stands like a rock).
	/// Pass 115b (Jaysonn 2026-10-22 23:34 "mario is walking thru them").</summary>
	private void SmsHoldMarioOff(Mario mario)
	{
		if (SmsAnswersMario || !SmsMarioAttacks(mario)) return;
		mario.SmsKeepDistance(GlobalPosition * SmsUnits.UnitsPerMetre, _ball.DamageRadius * SmsHitScale);
	}
	/// <summary>TObjHitCheck::checkActorsInList (ObjHitCheck.cpp 43), the fruit's side of the pair: OUR attack cylinder (the hit table's attack
	/// radius / height, scaled) against Mario's DAMAGE cylinder (50 x 160) puts him in our collision list -> TMapObjBase::control -> touchActor
	/// (kicked / boundByActor). The durian's attack radius is 150: a rising Mario kicks it from 150 x scale + 50 away.</summary>
	public bool SmsMarioTouches(Mario mario)
	{
		float u = SmsUnits.UnitsPerMetre, hs = SmsHitScale;
		float feet = mario.SmsFeetWorldY * u;
		float fBottom = (GlobalPosition.Y - _bGeomR) * u;
		float dx = (mario.GlobalPosition.X - GlobalPosition.X) * u, dz = (mario.GlobalPosition.Z - GlobalPosition.Z) * u;
		float r = _ball.AttackRadius * hs + Mario.SmsHitRadiusUnits;
		if (dx * dx + dz * dz > r * r) return false;
		float fTop = fBottom + _ball.AttackHeight * hs;
		return feet < fTop && feet + Mario.SmsHitHeightUnits > fBottom;
	}
	/// <summary>The other side of the pair: MARIO's attack cylinder (setNormalAttackArea HoldRadius 80 x AttackHeight 50 on the ground;
	/// setJumpingAttackArea PushupRadius 60 x PushupHeight 180 rising, TrampleRadius 100 x AttackHeight 50 falling -- the NPC rule, pass 85)
	/// against OUR damage cylinder puts us in his list -> TMario::checkCollision -> hitWantToTake (keepDistance).</summary>
	public bool SmsMarioAttacks(Mario mario)
	{
		float u = SmsUnits.UnitsPerMetre, hs = SmsHitScale;
		bool onGround = mario.IsOnFloor();
		bool rising = !onGround && mario.Velocity.Y > 0f;
		float ar = onGround ? SmsParams.Mario.HoldRadius : (rising ? SmsParams.Mario.PushupRadius : SmsParams.Mario.TrampleRadius);
		float ah = onGround ? SmsParams.Mario.AttackHeight : (rising ? SmsParams.Mario.PushupHeight : SmsParams.Mario.AttackHeight);
		float feet = mario.SmsFeetWorldY * u;
		float fBottom = (GlobalPosition.Y - _bGeomR) * u;
		if (feet > fBottom + _ball.DamageHeight * hs) return false;
		if (feet + ah < fBottom) return false;
		float dx = (mario.GlobalPosition.X - GlobalPosition.X) * u, dz = (mario.GlobalPosition.Z - GlobalPosition.Z) * u;
		float r = ar + _ball.DamageRadius * hs;
		return r * r > dx * dx + dz * dz;
	}

	/// <summary>TResetFruit::kicked (MapObjBall.cpp 793): a FALLING Mario kicks nothing (marioVy < 0 returns -- landing on a durian does nothing);
	/// a ball already rising, or airborne and moving toward him, is left alone; at rest it hops unk178 (20), falling it rebounds -unk160 x its
	/// fall + unk174 x his vy; xz += unk170 x his xz; both under the min speed -> a random unit xz; the guard; Mario's receiveMessage(ATTACK) -- the
	/// durian's freeze (MarioReceiveMsg.cpp 877); MSD_SE_MA_KICK_DRIAN.</summary>
	private bool SmsKicked(Mario mario, Vector3 mv, ref Vector3 v, bool airborne)
	{
		if (mv.Y < 0f) return false;
		Vector3 toMario = new Vector3(mario.GlobalPosition.X - GlobalPosition.X, 0f, mario.GlobalPosition.Z - GlobalPosition.Z);
		if (v.Y > 0f || (airborne && v.X * toMario.X + v.Z * toMario.Z > 0f)) return false;
		v.Y = v.Y == 0f ? _ball.KickHop : _ball.KickFall * mv.Y - _ball.Rebound * v.Y;
		v.X += _ball.KickRun * mv.X; v.Z += _ball.KickRun * mv.Z;
		if (Mathf.Abs(v.X) < _ball.MinSpeed && Mathf.Abs(v.Z) < _ball.MinSpeed) { v.X = GD.Randf() * 2f - 1f; v.Z = GD.Randf() * 2f - 1f; }
		_bGuard = 10; _bRest = false;
		SmsLastTouch = 1; SmsTouchCount++; SmsLastTouchVelUnits = v;
		if (Type == FruitType.Durian) mario.SmsKickFreeze();
		SmsKickSound?.Play();
		return true;
	}

	/// <summary>boundByActor(Mario) (229): inside mDamageRadius in XZ (the fruit's alone); the offset to him normalised; faster than the min speed
	/// on an axis lifts it unk150, else unk154 (the durian's 15: a slow touch pops it); xz += unk148 x his xz - dir x unk14C; receiveMessage(ATTACK).
	/// (The dive branch, 283, needs his vy &lt; 0 -- which touchActor routes to kicked(): for Mario it never runs.) The guard; AIRBORNE.</summary>
	private bool SmsBoundByActor(Mario mario, Vector3 mv, ref Vector3 v)
	{
		float u = SmsUnits.UnitsPerMetre;
		Vector3 off = new Vector3(mario.GlobalPosition.X - GlobalPosition.X, 0f, mario.GlobalPosition.Z - GlobalPosition.Z) * u;
		float r = _ball.DamageRadius * SmsHitScale;
		if (off.X * off.X + off.Z * off.Z > r * r) return false;
		if (off.X != 0f && off.Z != 0f) off = off.Normalized();
		bool fast = Mathf.Abs(mv.X) > _ball.MinSpeed || Mathf.Abs(mv.Z) > _ball.MinSpeed;
		v.Y += fast ? _ball.LiftFast : _ball.LiftSlow;
		v.X += _ball.Run * mv.X - off.X * _ball.Away;
		v.Z += _ball.Run * mv.Z - off.Z * _ball.Away;
		_bGuard = 10; _bRest = false; _bAirborne = true;
		SmsLastTouch = 2; SmsTouchCount++; SmsLastTouchVelUnits = v;
		if (Type == FruitType.Durian) mario.SmsKickFreeze();
		if (fast) SmsKickSound?.Play();
		return true;
	}

}
