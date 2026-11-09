using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SMS pass 158 / 159 / 160: a BOMB BOO (KINGBOO_REMATCH_gdoc.txt, HOW MARIO DEALS DAMAGE) -- Galaxy's black Boo, Mario's only weapon in the rematch.
/// It comes out of a black foggy spot (SmsBombBooSpawner), rises and fades in, then FLOATS AS SUNSHINE'S BOOS DO (160, TTelesa's numbers from
/// /enemy/telesa.prm as SmsTelesa carries them): it drifts between random points within WanderRadiusUnits of where it rose (walkToCurPathNode:
/// turning TurnSpeed degrees a tick toward the goal, MarchSpeed units a tick along its face; reached, it waits WaitTicks, then the next), its
/// height the base's FlyHeight (50..100 u, rising 1 u a tick to it) plus the bob (FlyAmplitude 50..75 u over FlyFrequency 200..400 ticks), its
/// nose tilting 10 - 35 cos(phase) degrees with the bob; and it HURTS Mario on touch unless he is above it (TTelesa::attackToMario; TouchHurts,
/// the common enemy damage). SeeksMario makes it drift toward him instead (158's). Mario's SPIN (X without the FLUDD) within his BombGrabRadius
/// GRABS it BY THE TONGUE (159, as Galaxy has it): he holds the tongue up in one hand (SmsLassoArm) and the Boo SWINGS ROUND HIM on it --
/// OrbitRadius out, OrbitOverHand over his hand, OrbitDegPerSec round, leaning outward (OrbitLeanDeg), facing in, the tongue stretched from its
/// mouth to his hand; his spin jump makes it swing faster (SpinJumpBoost); a DIVE drops it (released, it floats again after DropGraceSeconds). It
/// is NOT thrown: swung into King Boo's body (SmsKingBooRematch.InHitCylinder) it BURSTS on him and he TAKES A HIT -- Mario walks it into him.
/// HoldSeconds (0 = never) and it bursts in his hand for nothing. Expires on its own after ExpireSeconds floating (0 = never); King Boo's hit
/// clears every Boo on the field (the spawner). Model: models/Sms/Rematch/bomb_boo.glb + bomb_boo_tongue.glb (Jaysonn's .dae pair through
/// Blender, their vertex colours kept) at ModelScale, on BombBoo.gdshader -- the maroon body texture with its yellow eyes x the dark vertex
/// colours x Lift, Opacity 1 (159: 158's was purple and see-through), the eyes glowing a little (FaceGlow), a faint violet rim (Glow).
/// Signals: Grabbed, Dropped, Burst(hitKingBoo), Touched.
/// Pass 177: a swung Boo tests every King Boo on the field (the group sms_rematch_boo: the real one and his copies) and bursts on the one whose body it
/// is in (HitBoss). Pass 178 (Jaysonn: "having mario jump to have the bomb hit him is right"): the swing RISES with Mario's jump -- JumpSwingLift over the
/// hand while he is in the air (JumpSwingSeconds in and out) -- so a King Boo hovering 8.5 m up is hit from a jump's apex and never from the ground.
/// Pass 179 (Jaysonn: "make the boo bombs more expressive", "more like galaxy", "keep it black not purple"): the hanging TONGUE MOVES (it sways, wags, lags
/// behind a turn and tucks under with the drift -- The tongue group); a Boo Mario LOOKS AT is SHOCKED, Galaxy's way -- it freezes where it is, startles,
/// trembles, its eyes burn brighter, its tongue drops out and it DROOLS glossy drops until he looks away (The shock group); and it comes UP THROUGH THE FLOOR in
/// a burst of BLACK smoke (The rise group; the black pool, the mist and the glowing eyes before it are the supply's, SmsBombBooSpawner). Its rim glow and its
/// burst puff are grey-black now, not violet.
/// Pass 180 (Jaysonn: "a worried animation, not just frozen"): the shocked Boo WORRIES -- it shakes its head side to side, leans back from Mario, cowers with a
/// nervous breath and sweats beads off the top of its head (the Worry* and Sweat* knobs in The shock group), on top of 179's tremble, stare, eyes, tongue and drool.
/// </summary>
public partial class SmsBombBoo : Node3D
{
    public enum Phase { Rising, Drifting, Carried, Gone, Lobbed }   // 200: Lobbed -- spat by King Boo, in the air
    /// <summary>The tongue glb's length along its +Z (its root at the origin), metres at scale 1 (76.6 cm in the .dae).</summary>
    public const float TongueModelLength = 0.766f;
    private const float U = SmsUnits.UnitsPerMetre;

    [ExportGroup("Look")]
    /// <summary>The glb at this scale (Galaxy's centimetres read as metres make the body 2.5 m wide: 0.8 = 2 m, 1.7 m tall against Mario's 2.15).</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float ModelScale = 0.8f;
    /// <summary>1 = solid (159). Galaxy's own is about 0.6 (the material's alpha 150 / 255) -- a knob.</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float Opacity = 1f;
    /// <summary>The body's brightness: texture x (vertex colour + Ambient) x Lift (2 = the game's stage doubling; the body stays a near-black red, the eyes bright).</summary>
    [Export(PropertyHint.Range, "0.5,4,0.05")] public float Lift = 2f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Ambient = 0f;
    /// <summary>The eyes' own light (the bright texels) and the faint rim.</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float FaceGlow = 0.5f;
    /// <summary>The rim's colour: 179 a cool grey (Jaysonn: "keep it black not purple"; 158-178 had a violet rim, (0.35, 0.1, 0.55)).</summary>
    [Export] public Color GlowColor = new Color(0.22f, 0.22f, 0.28f);
    [Export(PropertyHint.Range, "0,3,0.05")] public float Glow = 0.25f;
    /// <summary>The tongue's root in the MOUTH (model metres, before ModelScale; 161, measured on the model: the body is 2.5 m wide, 1.8 tall and 2.1 deep about
    /// its origin, the face on its +Z side -- the mouth's centre at (0, -0.6, 0.86), the eyes at (0, 0.15, 0.93)), how far it droops when it just hangs
    /// (degrees), and whether the face is on the +Z side (off: -Z).</summary>
    [Export] public Vector3 TongueOffset = new Vector3(0f, -0.6f, 0.85f);
    [Export(PropertyHint.Range, "-60,80,1")] public float TongueRestDroopDeg = 35f;
    [Export] public bool FacesPlusZ = true;
    [ExportGroup("The tongue (179: it moves)")]
    /// <summary>179 (Jaysonn: "their tongue just kinda statically hangs and it needs to have motion like everything else"): the hanging tongue SWAYS -- its droop
    /// bobbing TongueSwayDeg either way and WAGGING TongueWagDeg side to side at about TongueWagHz (each Boo at its own phase), LAGGING behind a turn (TongueTurnLag
    /// degrees for every degree a second the body turns, opposite to the turn) and tucking under as it drifts (TongueMoveTuckDeg more droop at a full drift of
    /// 1.4 m/s); it follows its target at TongueFollow a second (lower = a heavier, lazier tongue). Shocked (below) it drops to ShockTongueDroopDeg and quivers.</summary>
    [Export(PropertyHint.Range, "0,30,0.5")] public float TongueSwayDeg = 7f, TongueWagDeg = 12f;
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float TongueWagHz = 0.9f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float TongueTurnLag = 0.15f;
    [Export(PropertyHint.Range, "0,30,0.5")] public float TongueMoveTuckDeg = 8f;
    [Export(PropertyHint.Range, "1,30,0.5")] public float TongueFollow = 9f;
    [ExportGroup("The shock (179, Galaxy: looked at, it freezes)")]
    /// <summary>179 (Jaysonn: "when mario looks at them they get shocked and kinda stay in place, have drool effects coming from their mouth", the Galaxy refs): a
    /// drifting Boo that Mario FACES (within LookConeDeg -- the whole cone -- of his facing, LookRange away or nearer) is SHOCKED: it stops where it is (the drift,
    /// the bob and the tilt frozen), startles (a squash-and-stretch hop of ShockHop over ShockHopSeconds), TREMBLES (ShockTremble metres of jitter), its eyes
    /// burning ShockEyeGlow x brighter and flickering, turning to STARE BACK at him (ShockTurnSpeed), its tongue dropped out and quivering, and it DROOLS -- glossy drops (DroolRadius, DroolColor) dribbling
    /// from the mouth every DroolEvery seconds, each clinging up to DroolClingSeconds before it falls (DroolGravity) and pops after DroolLife; DroolMaxOut at
    /// once. It stays shocked at least ShockMinSeconds and until he has looked away for LookAwaySeconds; a grab ends it; the touch still hurts. Off: 178's Boo,
    /// which never minded being looked at.</summary>
    [Export] public bool LookShock = true;
    [Export(PropertyHint.Range, "5,120,1")] public float LookConeDeg = 40f;
    [Export(PropertyHint.Range, "1,40,0.5")] public float LookRange = 12f;
    [Export(PropertyHint.Range, "0,5,0.05")] public float LookAwaySeconds = 0.6f, ShockMinSeconds = 0.8f;
    [Export(PropertyHint.Range, "0,0.2,0.005")] public float ShockTremble = 0.035f;
    /// <summary>Shocked, it turns to STARE BACK at Mario this fast (a second to close the angle at 1; 0 = it keeps facing where it drifted).</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float ShockTurnSpeed = 8f;
    [Export(PropertyHint.Range, "1,6,0.1")] public float ShockEyeGlow = 2.4f;
    [Export(PropertyHint.Range, "0,0.6,0.01")] public float ShockHop = 0.22f;
    [Export(PropertyHint.Range, "0.05,1.5,0.05")] public float ShockHopSeconds = 0.35f;
    [Export(PropertyHint.Range, "0,90,1")] public float ShockTongueDroopDeg = 62f;
    [Export] public string ShockSe = "";
    /// <summary>180 (Jaysonn: "when mario looks at the boos they should be doing like a worried animation not just frozen"): the WORRY while shocked -- the body
    /// SHAKES side to side about its stare (WorryShakeDeg either way at WorryShakeHz: a "no, no, no"), LEANS BACK from Mario (WorryLeanDeg, nose up), COWERS
    /// (WorryCower: squashed down and pushed out) with a quick nervous BREATH on it (WorryBreathHz), and SWEATS -- beads (SweatRadius, SweatColor) flung off the
    /// top of its head every SweatEvery seconds, arcing out and falling. All eased in over the first 0.3 s of the shock. 0 / off = 179's frozen Boo.</summary>
    [Export(PropertyHint.Range, "0,45,0.5")] public float WorryShakeDeg = 10f;
    [Export(PropertyHint.Range, "0.2,10,0.1")] public float WorryShakeHz = 3f, WorryBreathHz = 4f;
    [Export(PropertyHint.Range, "0,60,1")] public float WorryLeanDeg = 14f;
    [Export(PropertyHint.Range, "0,0.4,0.01")] public float WorryCower = 0.1f;
    [Export] public bool WorrySweat = true;
    [Export(PropertyHint.Range, "0.05,3,0.01")] public float SweatEvery = 0.3f;
    [Export(PropertyHint.Range, "0.02,0.3,0.005")] public float SweatRadius = 0.07f;
    [Export] public Color SweatColor = new Color(0.9f, 0.96f, 1f, 0.95f);
    [Export] public bool ShockDrool = true;
    [Export(PropertyHint.Range, "0.03,0.3,0.005")] public float DroolRadius = 0.11f;
    [Export(PropertyHint.Range, "0.05,3,0.01")] public float DroolEvery = 0.2f, DroolLife = 1.1f, DroolClingSeconds = 0.35f;
    [Export(PropertyHint.Range, "0,20,0.1")] public float DroolGravity = 5.5f;
    [Export] public Color DroolColor = new Color(0.8f, 0.84f, 0.9f, 0.92f);
    [Export(PropertyHint.Range, "1,30,1")] public int DroolMaxOut = 12;
    [ExportGroup("The rise (179: up through the floor, Galaxy)")]
    /// <summary>179 (Jaysonn: "when spawning in they should have that mist effect with the eyes glowing before they spawn in"; the black pool, the mist and the
    /// glowing eyes BEFORE it are the supply's, SmsBombBooSpawner): the Boo comes UP THROUGH THE FLOOR -- it starts SpawnDepth under its spawn point (hidden by
    /// the floor) and rises to its height over RiseSeconds, growing from RiseScaleFrom to full, solid from the start -- in a burst of BLACK SMOKE at the spot
    /// (RiseSmokePuffs puffs, RiseSmokeSize, RiseSmokeSeconds, RiseSmokeColor; SmokeTexture empty = the game's dark smoke ball). Off: 158's rise (it scales up
    /// from nothing and fades in at the spawn point).</summary>
    [Export] public bool RiseFromUnder = true;
    [Export(PropertyHint.Range, "0,4,0.05")] public float SpawnDepth = 1.3f;
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float RiseScaleFrom = 0.75f;
    [Export] public bool RiseSmoke = true;
    [Export(PropertyHint.Range, "0,60,1")] public int RiseSmokePuffs = 22;
    [Export(PropertyHint.Range, "0.3,4,0.05")] public float RiseSmokeSize = 1.5f, RiseSmokeSeconds = 1.1f;
    [Export] public Color RiseSmokeColor = new Color(0.09f, 0.09f, 0.11f, 0.95f);
    [Export] public Texture2D SmokeTexture;
    [ExportGroup("The float (Sunshine's Boo, telesa.prm)")]
    /// <summary>Its base point stands this high over the floor where it rose (the game's Boo keeps the height it was placed at); the fly height and the bob
    /// ride on it. 1.2: the body (0.85 m to its bottom at scale 0.8) clears the floor at the bob's lowest.</summary>
    [Export] public float BaseLiftOverFloor = 1.2f;
    /// <summary>FlyHeight 50..100 u over the base (picked at random, risen to at 1 u a tick), FlyAmplitude 50..75 u of bob over FlyFrequency 200..400 ticks (a cycle).</summary>
    [Export] public float FlyHeightMinUnits = 50f, FlyHeightMaxUnits = 100f, FlyAmplitudeMinUnits = 50f, FlyAmplitudeMaxUnits = 75f, FlyFrequencyMin = 200f, FlyFrequencyMax = 400f;
    /// <summary>The nose: 10 - FlyAngMax x cos(phase) degrees (up as it rises, down as it sinks), eased as SmsTelesa eases it.</summary>
    [Export] public float FlyAngMax = 35f, FlyAngBase = 10f;
    /// <summary>The drift: MarchSpeed 1..1.7 u a tick and TurnSpeed 1..1.5 degrees a tick (each picked at random per Boo), the goals within WanderRadiusUnits
    /// of the base (0.3..1 of it), reached within ReachedUnits, WaitTicks of hovering between them.</summary>
    [Export] public float MarchSpeedLow = 1f, MarchSpeedHigh = 1.7f, TurnSpeedLow = 1f, TurnSpeedHigh = 1.5f;
    [Export] public float WanderRadiusUnits = 800f, ReachedUnits = 100f;
    [Export] public int WaitTicks = 100;
    /// <summary>On: it drifts toward Mario instead (158's), stopping DriftStopDistance short.</summary>
    [Export] public bool SeeksMario = false;
    [Export] public float DriftStopDistance = 1.6f;
    /// <summary>TTelesa::attackToMario: the touch hurts him (the common enemy damage, his invincibility after) unless he is ABOVE it -- his feet over the
    /// body's middle less 50 u. Its attack cylinder: TouchRadius across, TouchHeight tall about its centre. Off: a Boo he can walk through.</summary>
    [Export] public bool TouchHurts = true;
    [Export] public float TouchRadius = 0.9f, TouchHeight = 1.4f;
    /// <summary>Gone on its own after this long floating (0 = never).</summary>
    [Export] public float ExpireSeconds = 0f;
    /// <summary>Out of the fog: the rise's length (it scales up, fades in and lifts to its height over it).</summary>
    [Export] public float RiseSeconds = 0.7f;
    [ExportGroup("The swing (carried by the tongue)")]
    /// <summary>Round Mario: this far out (161: 3.6 m, "the tongue radius longer"), this far OVER his raised hand (his arms are short next to his head: the raised
    /// wrist comes to his ear, 1.3 m up on a 2.15 m body -- 0.9 puts the Boo just over his head, the tongue rising to it), this fast (degrees a second; 480 = a
    /// turn in 0.75 s), leaning this far outward.</summary>
    [Export] public float OrbitRadius = 3.6f, OrbitOverHand = 0.9f, OrbitDegPerSec = 480f, OrbitLeanDeg = 25f;
    /// <summary>178 (Jaysonn: "having mario jump to have the bomb hit him is right"): while Mario is IN THE AIR the swing rises this much higher over his hand
    /// (the Boo flung up as he jumps, eased in over JumpSwingSeconds and back down as he lands) -- King Boo hovers 8.5 m up (173), his body's hit cylinder
    /// starting 5.7 m over the floor: a swing from the ground (2.5 m up) cannot reach him; a jump's apex with this lift can.</summary>
    [Export] public float JumpSwingLift = 1.6f, JumpSwingSeconds = 0.15f;
    public float JumpLiftNow { get; private set; }
    /// <summary>From the grab to the full swing (the radius and the height ease in).</summary>
    [Export] public float OrbitSettleSeconds = 0.35f;
    [Export] public bool OrbitClockwise = false;
    /// <summary>His spin jump: the swing this much faster for this long.</summary>
    [Export] public float SpinJumpBoost = 1.7f, SpinJumpBoostSeconds = 2.5f;
    /// <summary>Dropped: no grab again for this long (he has to spin for it again anyway).</summary>
    [Export] public float DropGraceSeconds = 0.6f;
    /// <summary>Carried this long, it bursts in his hand for nothing (Galaxy's held Bomb Boo blows up on its own; 0 = never). It flashes faster over its last 2 s.</summary>
    [Export] public float HoldSeconds = 0f;
    /// <summary>The Boo's own reach when it swings into King Boo (model metres, x ModelScale: the body is 1.27 m to its side, 1.05 m to its front).</summary>
    [Export] public float OwnRadius = 1f;
    [ExportGroup("Sound (empty = silent)")]
    [Export] public string AppearSe = "MSD_SE_EN_TELESA_APPEAR", GrabSe = "MSD_SE_EN_TELESA_FIX", BurstSe = "MSD_SE_EN_TELESA_CRUSH", GoneSe = "MSD_SE_EN_TELESA_DISAPPEAR";
    [ExportGroup("")]
    [Export] public PackedScene BurstFxScene;
    /// <summary>The burst puff's colour: 179 a dark smoke grey (Jaysonn: "keep it black not purple"; 158-178's was violet, (0.55, 0.2, 0.85)).</summary>
    [Export] public Color BurstColor = new Color(0.25f, 0.25f, 0.28f);
    /// <summary>170 (Jaysonn: "bomb boos need to explode when hitting king boo"): swung into him it EXPLODES -- the Bob-omb's blast (BombExplosion.tscn: the
    /// 10_bomb fireball with its glow, smoke, scraps, shockwave and sparks) at this scale, the water flower's boom, a kick of the camera -- over its own puff.</summary>
    [Export] public bool ExplodeOnKingBoo = true;
    [Export] public PackedScene ExplosionFxScene;
    [Export] public float ExplosionScale = 1.4f, ExplosionShake = 1f;
    [Export] public string ExplodeSe = "MSD_SE_PO_WFLOWER_EXPLOSION";
    [Export] public int Seed = 0;
    [ExportGroup("The lob (200: spat by King Boo -- the Bomb Boo volley)")]
    /// <summary>200 (Jaysonn: "one more attack involving boos"): a Boo King Boo SPITS flies on a lob (<see cref="Lob"/>: SmsBooVolley sets the arc, the target, the
    /// blast), TUMBLING head over heels at LobTumbleDegPerSec, its fuse flashing quicker as it comes down, a trail of black puffs behind it (LobTrail), a RING on
    /// the floor where it will land (LobMarkColor: the blast's own footprint; a dud's the body's). Landed, it BLASTS -- the Bob-omb explosion, a ring of light
    /// LobBlastColor out to the blast radius, Mario within it whose feet are under the blast height HIT (the common enemy damage: a jump clears it) -- or, a DUD,
    /// it thuds down in black smoke and FLOATS there as one of the supply's: Mario's to grab, its fuse still burning (DudFuseLeft; it blasts when that runs out).</summary>
    [Export(PropertyHint.Range, "0,1080,10")] public float LobTumbleDegPerSec = 420f;
    [Export] public bool LobTrail = true;
    [Export] public Color LobTrailColor = new Color(0.12f, 0.11f, 0.14f, 0.8f), LobMarkColor = new Color(1f, 0.82f, 0.25f, 0.6f), LobBlastColor = new Color(0.62f, 0.45f, 0.85f, 0.9f);
    /// <summary>Where the body's bottom meets the floor at the landing (metres over it; the body is 0.85 m to its bottom at scale 0.8).</summary>
    [Export] public float LobLandLift = 0.9f;
    [ExportGroup("")]
    [Export] public bool DebugPrint;

    [Signal] public delegate void GrabbedEventHandler();
    [Signal] public delegate void DroppedEventHandler();
    [Signal] public delegate void BurstEventHandler(bool hitKingBoo);
    [Signal] public delegate void TouchedEventHandler();
    /// <summary>200: the lob came down (a dud floats on; else the blast follows at once); the blast on the floor (whether it got Mario).</summary>
    [Signal] public delegate void LandedEventHandler(bool dud);
    [Signal] public delegate void BlastedEventHandler(bool hitMario);

    public static readonly List<SmsBombBoo> All = new();
    /// <summary>200, for the drives: the lob -- in the air now, a dud, where it was spat from and where it comes down, the flight's length and age, the tumble so far,
    /// whether it has landed (and where) and blasted (and whether that got Mario), the dud's fuse left (seconds; 0 = none), the mark's radius; the blasts on the floor
    /// and the hits of all Boos.</summary>
    public bool IsLobbed => State == Phase.Lobbed;
    public bool LobDud { get; private set; }
    public Vector3 LobFrom { get; private set; }
    public Vector3 LobTarget { get; private set; }
    public float LobFlightSeconds { get; private set; }
    public float LobAge => _lobT;
    public float LobTumbleDeg => _lobTumble;
    public bool HasLanded { get; private set; }
    public Vector3 LandedAt { get; private set; }
    public bool HasBlasted { get; private set; }
    public bool BlastHitMario { get; private set; }
    public float BlastRadius => _lobBlastR;
    public float DudFuseLeft => _dudFuse;
    public float LobMarkRadius { get; private set; }
    public Vector3 LobVelocity => _lobVel;
    public static int FloorBlasts, FloorBlastHits, Lobs, Duds;
    private Vector3 _lobVel; private float _lobT, _lobTumble, _lobBlastR, _lobBlastH, _dudFuse; private bool _lobDud; private MeshInstance3D _lobMark; private StandardMaterial3D _lobMarkMat; private GpuParticles3D _lobTrail;
    public Phase State { get; private set; } = Phase.Rising;
    public bool IsFree => State == Phase.Drifting;
    public bool IsCarried => State == Phase.Carried;
    public float OrbitSpeedNow => OrbitDegPerSec * (_boostLeft > 0f ? SpinJumpBoost : 1f);
    public float Age { get; private set; }
    /// <summary>For the drives: the float (the base point, the fly height and the bob in metres, the nose's pitch in degrees, the goal, the goals reached, the
    /// march and turn speeds picked), the swing (its radius and angle now), the tongue (its length and tip, the mouth), the materials, the touches.</summary>
    public Vector3 BasePoint => _base;
    public float FlyHeightNow => _flyCur / U;
    public float BobNow => _bob / U;
    public float PitchDeg => _rotX;
    public Vector3 GoalPoint => _goal;
    public int GoalsReached { get; private set; }
    public float MarchSpeed => _march;
    public float TurnSpeed => _turn;
    public bool Waiting => _wait > 0;
    public int Touches { get; private set; }
    public float OrbitRadiusNow { get; private set; }
    public float OrbitAngle => _orbitAngle;
    public float TongueLength { get; private set; }
    public Vector3 TongueTip { get; private set; }
    public Vector3 MouthPoint => GlobalTransform * (TongueOffset * ModelScale);
    public int MaterialCount => _mats.Count;
    public Node3D TongueNode => _tongue;
    /// <summary>179, for the drives: the shock (on, how long, whether Mario faces it now, how many so far), the drool (drops out / made, the lowest one's height),
    /// the eyes' glow now, the tongue's wag (yaw, degrees, + = to its left) and droop (pitch, degrees) and its root, the rise (where it started, its smoke), the opacity set.</summary>
    public bool Shocked { get; private set; }
    public float ShockSeconds => _shockT;
    public bool LookedAtNow { get; private set; }
    public int Shocks { get; private set; }
    public int DroolOut => _drool?.Live ?? 0;
    public int DroolMade => _drool?.Made ?? 0;
    public float DroolLowestY => _drool?.LowestY ?? float.NaN;
    /// <summary>180: the worry now -- the shake's yaw off the stare (degrees), the lean (degrees, - = nose up), the cower's squash (the model's height as a fraction of
    /// its rest), the sweat beads out / made.</summary>
    public float WorryYawDeg { get; private set; }
    public float WorryPitchDeg { get; private set; }
    public float WorrySquash { get; private set; } = 1f;
    public int SweatOut => _sweat?.Live ?? 0;
    public int SweatMade => _sweat?.Made ?? 0;
    public float EyeGlowNow { get; private set; }
    public float TongueYawDeg => _tYaw;
    public float TonguePitchDeg => _tPitch;
    public Vector3 TongueRootPoint => _tongue != null ? _tongue.GlobalPosition : GlobalPosition;
    public float RiseStartY { get; private set; } = float.NaN;
    public bool RiseSmokeFired { get; private set; }
    public float OpacityNow { get; private set; }

    private Node3D _model, _tongue; private readonly List<ShaderMaterial> _mats = new(), _bodyMats = new();
    private float _tPitch, _tYaw, _tonguePhase, _yawPrev; private bool _yawPrevKnown;
    private float _shockT, _lookAwayT, _droolT, _sweatT; private Vector3 _shockPos; private DropPool _drool, _sweat; private int _sweatSide = 1;
    private Mario _mario; private SmsKingBooRematch _boss, _hitBoss; private SmsActorSound _sound; private Random _rng;
    /// <summary>177: every King Boo on the field -- the real one and his copies (the group sms_rematch_boo).</summary>
    private IEnumerable<SmsKingBooRematch> BoosNow()
    {
        foreach (Node n in GetTree().GetNodesInGroup("sms_rematch_boo")) if (n is SmsKingBooRematch b) yield return b;
    }
    public SmsKingBooRematch HitBoss => _hitBoss;
    private float _t, _yaw, _boostLeft, _grace, _floorY = float.NaN;
    private Vector3 _base, _goal; private float _flyCur, _flyTarget, _bob, _bobAmp, _bobPhase, _bobFreq = 200f, _rotX, _march = 1f, _turn = 1f; private int _wait;
    private float _orbitAngle, _settle, _r0, _y0, _held;
    private Rid _marioRid; private bool _marioRidKnown;

    public override void _Ready()
    {
        All.Add(this);
        _rng = new Random(Seed != 0 ? Seed : (int)(GetInstanceId() & 0x7fffffff));
        _model = GetNodeOrNull<Node3D>("Model"); _tongue = GetNodeOrNull<Node3D>("Tongue");
        var shader = ResourceLoader.Exists("res://assets/Sms/Rematch/BombBoo.gdshader") ? GD.Load<Shader>("res://assets/Sms/Rematch/BombBoo.gdshader") : null;
        foreach (var root in new[] { _model, _tongue })
        {
            if (root == null) continue;
            if (shader == null) continue;
            foreach (Node n in root.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                {
                    var sm = new ShaderMaterial { Shader = shader };
                    if (mi.GetActiveMaterial(s) is BaseMaterial3D bm && bm.AlbedoTexture != null) sm.SetShaderParameter("base_tex", bm.AlbedoTexture);
                    sm.SetShaderParameter("opacity", 0f); sm.SetShaderParameter("lift", Lift); sm.SetShaderParameter("ambient", Ambient);
                    sm.SetShaderParameter("face_glow", root == _tongue ? 0f : FaceGlow);
                    sm.SetShaderParameter("glow_color", new Vector3(GlowColor.R, GlowColor.G, GlowColor.B));
                    sm.SetShaderParameter("glow", Glow);
                    mi.SetSurfaceOverrideMaterial(s, sm);
                    _mats.Add(sm); if (root == _model) _bodyMats.Add(sm);
                }
            }
        }
        EyeGlowNow = FaceGlow;
        SmokeTexture ??= ResourceLoader.Exists("res://assets/Sms/Fx/tex/kemuball3_dark.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/kemuball3_dark.png") : null;
        BurstFxScene ??= ResourceLoader.Exists("res://assets/Sms/Fx/EnemyDisap.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Fx/EnemyDisap.tscn") : null;
        ExplosionFxScene ??= ResourceLoader.Exists("res://assets/Sms/Enemy/Bomb/BombExplosion.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Enemy/Bomb/BombExplosion.tscn") : null;
        _sound = SmsActorSound.Attach(this);
        _yaw = Rotation.Y;
        // TTelesa::init's setFlyParam + TWalkerEnemy's speeds: at random in their ranges
        _flyTarget = Lerp(FlyHeightMinUnits, FlyHeightMaxUnits, Rand()); _flyCur = 0f;
        _bobAmp = Lerp(FlyAmplitudeMinUnits, FlyAmplitudeMaxUnits, Rand()); _bobPhase = 0f;
        _bobFreq = MathF.Max(1f, Lerp(FlyFrequencyMin, FlyFrequencyMax, Rand()));
        _march = Lerp(MarchSpeedLow, MarchSpeedHigh, Rand()); _turn = Lerp(TurnSpeedLow, TurnSpeedHigh, Rand());
        _rotX = FlyAngBase - FlyAngMax;
        _tonguePhase = Rand() * MathF.Tau; _tPitch = TongueRestDroopDeg; _tYaw = 0f;
        SetScaleK(RiseFromUnder ? RiseScaleFrom : 0.01f, RiseFromUnder ? 1f : 0f);
        if (!string.IsNullOrEmpty(AppearSe)) _sound?.Play(AppearSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: {_mats.Count} materials, at {GlobalPosition}; fly {_flyTarget:F0} u, bob {_bobAmp:F0} u / {_bobFreq:F0} ticks, march {_march:F2} u/t, turn {_turn:F2} deg/t");
    }
    public override void _ExitTree() { All.Remove(this); }
    private float Rand() => (float)_rng.NextDouble();
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>The body and the hanging tongue at the model scale x k (the rise), the fade to <paramref name="alpha"/> (179: up through the floor it stays solid).</summary>
    private void SetScaleK(float k, float alpha)
    {
        if (_model != null) _model.Scale = Vector3.One * ModelScale * k;
        TongueRest(k);
        OpacityNow = Opacity * Mathf.Clamp(alpha, 0f, 1f);
        foreach (var m in _mats) m.SetShaderParameter("opacity", OpacityNow);
    }
    /// <summary>The tongue just hanging out of the mouth: its root at TongueOffset, drooping _tPitch (TongueRestDroopDeg and the sway, TongueSway) and turned
    /// _tYaw to the side (the wag).</summary>
    private void TongueRest(float k = 1f)
    {
        if (_tongue == null) return;
        Basis b = new Basis(Vector3.Up, Mathf.DegToRad(_tYaw)) * new Basis(Vector3.Right, Mathf.DegToRad(_tPitch));
        _tongue.Transform = new Transform3D(b.Scaled(Vector3.One * ModelScale * k), TongueOffset * ModelScale * k);
        TongueLength = TongueModelLength * ModelScale * k; TongueTip = _tongue.GlobalTransform * new Vector3(0f, 0f, TongueModelLength);
    }
    /// <summary>179: the hanging tongue's motion -- its droop and wag targets (the sway and the wag on their sines, the lag behind a turn, the tuck under with the
    /// drift; shocked: dropped out to ShockTongueDroopDeg and quivering) followed at TongueFollow a second.</summary>
    private void TongueSway(float dt, float speedMps)
    {
        float yawRate = 0f;
        if (_yawPrevKnown && dt > 1e-5f) yawRate = Mathf.RadToDeg(Mathf.Wrap(_yaw - _yawPrev, -MathF.PI, MathF.PI)) / dt;
        _yawPrev = _yaw; _yawPrevKnown = true;
        float w = MathF.Tau * TongueWagHz * Age + _tonguePhase;
        float pitchT, yawT;
        if (Shocked)
        {
            pitchT = ShockTongueDroopDeg + 3f * MathF.Sin(Age * 88f);                                   // the quiver: 14 Hz, 3 degrees
            yawT = 3f * MathF.Sin(Age * 97f + 1f);
        }
        else
        {
            pitchT = TongueRestDroopDeg + TongueSwayDeg * MathF.Sin(w * 1.37f) + TongueMoveTuckDeg * Mathf.Clamp(speedMps / 1.4f, 0f, 1.5f);
            yawT = TongueWagDeg * MathF.Sin(w) - Mathf.Clamp(yawRate * TongueTurnLag, -35f, 35f);
        }
        float f = Mathf.Clamp(TongueFollow * dt, 0f, 1f);
        _tPitch += (pitchT - _tPitch) * f; _tYaw += (yawT - _tYaw) * f;
    }
    /// <summary>179: Mario FACES it -- it lies within LookConeDeg (the whole cone) of his facing, LookRange away or nearer, flat.</summary>
    private bool LookedAt(Mario mario)
    {
        Vector3 to = GlobalPosition - mario.GlobalPosition; to.Y = 0f;
        float d = to.Length(); if (d > LookRange || d < 1e-3f) return false;
        Vector3 f = mario.SmsFacingDir(); f.Y = 0f; if (f.LengthSquared() < 1e-6f) return false;
        return f.Normalized().Dot(to / d) >= MathF.Cos(Mathf.DegToRad(LookConeDeg * 0.5f));
    }
    /// <summary>179: into the shock (it holds where it hangs) or out of it (the float goes on from there: a half wait, then the next goal).</summary>
    private void SetShock(bool on)
    {
        if (Shocked == on) return;
        Shocked = on; _shockT = 0f; _lookAwayT = 0f; _droolT = DroolEvery * 0.5f; _sweatT = SweatEvery * 0.6f;
        if (on) { _shockPos = GlobalPosition; Shocks++; if (!string.IsNullOrEmpty(ShockSe)) _sound?.Play(ShockSe); }
        else
        {
            WorryYawDeg = 0f; WorryPitchDeg = 0f; WorrySquash = 1f;
            if (_model != null) _model.Scale = Vector3.One * ModelScale;
            EyeGlowNow = FaceGlow; foreach (var m in _bodyMats) m.SetShaderParameter("face_glow", FaceGlow);
            GlobalTransform = new Transform3D(FaceBasis(_yaw, _rotX), _shockPos);
            _wait = WaitTicks / 2;
        }
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: {(on ? "SHOCKED -- Mario faces it; it holds" : "the shock over -- it floats on")} at {GlobalPosition}");
    }
    /// <summary>179: a shocked tick -- held at the shock spot with the tremble, the startle's squash and stretch, the eyes' glow and flicker, the tongue dropped out,
    /// a drop of drool every DroolEvery; the touch still hurts.</summary>
    private void ShockTick(float dt, Mario mario)
    {
        if (mario != null)                                                                              // it turns to stare back at him (the ref: caught, it faces Mario)
        {
            Vector3 d = mario.GlobalPosition - _shockPos; d.Y = 0f;
            if (d.LengthSquared() > 1e-4f) _yaw = Mathf.LerpAngle(_yaw, MathF.Atan2(d.X, d.Z), Mathf.Clamp(ShockTurnSpeed * dt, 0f, 1f));
        }
        // 180: the WORRY -- the head shake about the stare, the lean back, the cower with its nervous breath (all eased in over 0.3 s)
        float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_shockT / 0.3f, 0f, 1f));
        WorryYawDeg = WorryShakeDeg * MathF.Sin(MathF.Tau * WorryShakeHz * _shockT) * ease;
        WorryPitchDeg = -WorryLeanDeg * ease;
        Vector3 jitter = ShockTremble > 0f ? new Vector3(Rand() - 0.5f, Rand() - 0.5f, Rand() - 0.5f) * (2f * ShockTremble) : Vector3.Zero;
        GlobalTransform = new Transform3D(FaceBasis(_yaw + Mathf.DegToRad(WorryYawDeg), _rotX + WorryPitchDeg), _shockPos + jitter);
        if (_model != null)
        {
            float h = ShockHopSeconds > 0f && _shockT < ShockHopSeconds ? MathF.Sin(MathF.PI * _shockT / ShockHopSeconds) * ShockHop : 0f;   // the startle: tall and thin, then back
            float c = WorryCower * ease * (1f + 0.3f * MathF.Sin(MathF.Tau * WorryBreathHz * _shockT));                                       // the cower: squashed down, pushed out, breathing
            _model.Scale = new Vector3(1f - 0.5f * h + 0.5f * c, 1f + h - c, 1f - 0.5f * h + 0.5f * c) * ModelScale;
            WorrySquash = 1f + h - c;
        }
        EyeGlowNow = FaceGlow * (ShockEyeGlow + 0.35f * MathF.Sin(Age * 57f));                      // the flicker: 9 Hz
        foreach (var m in _bodyMats) m.SetShaderParameter("face_glow", EyeGlowNow);
        TongueSway(dt, 0f); TongueRest();
        if (ShockDrool)
        {
            _droolT += dt;
            if (_droolT >= DroolEvery) { _droolT -= DroolEvery; (_drool ??= new DropPool(this, DroolRadius, DroolColor, DroolLife, "Drool")).Drip(MouthPoint, GlobalTransform.Basis.Orthonormalized(), FacesPlusZ, Rand(), Rand(), Rand()); }
        }
        if (WorrySweat)                                                                                 // 180: a bead off the top of the head, this side then that, arcing out
        {
            _sweatT += dt;
            if (_sweatT >= SweatEvery)
            {
                _sweatT -= SweatEvery; _sweatSide = -_sweatSide;
                Basis b = GlobalTransform.Basis.Orthonormalized(); Vector3 face = FacesPlusZ ? b.Z : -b.Z;
                Vector3 at = GlobalPosition + b * (new Vector3(_sweatSide * 0.55f, 0.75f, FacesPlusZ ? 0.15f : -0.15f) * ModelScale);
                Vector3 vel = b.X * (_sweatSide * (1.4f + 0.6f * Rand())) + Vector3.Up * (2f + 0.8f * Rand()) + face * 0.4f;
                (_sweat ??= new DropPool(this, SweatRadius, SweatColor, 0.8f, "Sweat")).Fling(at, vel, 0.7f + 0.6f * Rand());
            }
        }
        if (TouchHurts && mario != null) TouchCheck(mario);
    }
    /// <summary>179: the black smoke the Boo comes up through (Galaxy's Bomb Boo: a burst of dark puffs round it at the floor) -- a one-shot emitter at the spot,
    /// freed when its puffs are gone.</summary>
    private void FireRiseSmoke(Vector3 at)
    {
        if (RiseSmokePuffs <= 0 || !IsInsideTree()) return;
        var p = new GpuParticles3D { Name = Name + "RiseSmoke", TopLevel = true, Amount = RiseSmokePuffs, Lifetime = RiseSmokeSeconds, OneShot = true, Explosiveness = 0.9f, FixedFps = 60,
            VisibilityAabb = new Aabb(new Vector3(-5f, -2f, -5f), new Vector3(10f, 8f, 10f)) };
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.45f * RiseSmokeSize, Direction = Vector3.Up, Spread = 75f,
            InitialVelocityMin = 1.2f * RiseSmokeSize, InitialVelocityMax = 2.6f * RiseSmokeSize, Gravity = new Vector3(0f, -0.6f, 0f), DampingMin = 2.2f, DampingMax = 3f,
            ScaleMin = 0.8f * RiseSmokeSize, ScaleMax = 1.4f * RiseSmokeSize, LifetimeRandomness = 0.3f, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -70f, AngularVelocityMax = 70f
        };
        // the ramp's colour reaches the particle shader raw (no sRGB decode: a 0.09 grey drew as a 0.33 grey), so it is given in linear
        Color lin = RiseSmokeColor.SrgbToLinear(); lin.A = RiseSmokeColor.A;
        var ramp = new Gradient();
        ramp.SetColor(0, lin); ramp.SetColor(1, new Color(lin.R, lin.G, lin.B, 0f));
        ramp.AddPoint(0.45f, lin);
        pm.ColorRamp = new GradientTexture1D { Gradient = ramp };
        p.ProcessMaterial = pm;
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true, AlbedoTexture = SmokeTexture, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        p.DrawPass1 = new QuadMesh { Size = new Vector2(1.4f, 1.4f), Material = mat };
        p.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        p.Finished += p.QueueFree;
        GetParent().AddChild(p);
        p.GlobalPosition = at + Vector3.Up * 0.4f; p.Emitting = true;                                  // a little off the floor: the puffs' lower halves were cut by it
        RiseSmokeFired = true;
    }

    /// <summary>179: the shocked Boo's DROOL (the Galaxy ref: glossy grey drops about the mouth) -- a sphere each, DroolColor with the light's highlight and a little
    /// light of its own for the dark arena, dribbling from the mouth: each clings to a point about the mouth growing to size, then falls under DroolGravity drifting
    /// a little outward, shrinking away over its last third. Pooled under a world-fixed node of the Boo's; the pool ticks on after the shock so the last drops fall.
    /// 180: the same pool, in its own radius and colour, carries the SWEAT -- beads flung off the head (Fling), falling from the start.</summary>
    private sealed class DropPool
    {
        private sealed class Drop { public MeshInstance3D Mi; public Vector3 Pos, Vel, Cling; public float Age, ClingFor, Life, Size = 1f; public bool Live, Falling; }
        private readonly SmsBombBoo _boo; private readonly Node3D _root; private readonly List<Drop> _drops = new(); private readonly SphereMesh _mesh; private readonly StandardMaterial3D _mat; private readonly float _life;
        public int Made { get; private set; }
        public int Live { get { int n = 0; foreach (var d in _drops) if (d.Live) n++; return n; } }
        public float LowestY { get { float y = float.NaN; foreach (var d in _drops) if (d.Live && (float.IsNaN(y) || d.Pos.Y < y)) y = d.Pos.Y; return y; } }
        /// <summary>A pool of glossy drops of one radius and colour (180: the drool's and the sweat's), each living `life` seconds once it falls.</summary>
        public DropPool(SmsBombBoo boo, float radius, Color c, float life, string name)
        {
            _boo = boo; _life = life;
            _root = new Node3D { Name = name, TopLevel = true }; boo.AddChild(_root); _root.GlobalTransform = Transform3D.Identity;
            _mesh = new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = 14, Rings = 8 };
            _mat = new StandardMaterial3D { AlbedoColor = c, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.12f, Metallic = 0.05f, EmissionEnabled = true, Emission = new Color(c.R * 0.3f, c.G * 0.3f, c.B * 0.3f) };
        }
        private Drop Take()
        {
            foreach (var x in _drops) if (!x.Live) return x;
            if (_drops.Count >= _boo.DroolMaxOut) return null;
            var d = new Drop { Mi = new MeshInstance3D { Mesh = _mesh, MaterialOverride = _mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off } };
            _root.AddChild(d.Mi); _drops.Add(d);
            return d;
        }
        /// <summary>A new drop at the mouth: to one side of its middle (r2), a little under it (r3), clinging r1 x DroolClingSeconds before it falls.</summary>
        public void Drip(Vector3 mouth, Basis b, bool facesPlusZ, float r1, float r2, float r3)
        {
            var d = Take(); if (d == null) return;
            d.Live = true; d.Falling = false; d.Age = 0f; d.Life = _life; d.ClingFor = _boo.DroolClingSeconds * r1; d.Size = 0.7f + 0.6f * r3;   // drops of 0.7..1.3 x the radius (the ref's vary)
            d.Cling = new Vector3((r2 - 0.5f) * 0.34f, -0.04f - 0.12f * r3, facesPlusZ ? 0.06f : -0.06f);   // the Boo's own frame: x its sideways, z its face
            d.Vel = Vector3.Zero; d.Pos = mouth + b * d.Cling;
            d.Mi.Visible = true; d.Mi.Scale = Vector3.One * (0.2f * d.Size); d.Mi.GlobalPosition = d.Pos;
            Made++;
        }
        /// <summary>180: a bead FLUNG from a point with a velocity (the sweat): no cling, full size at once, falling under the gravity from the start.</summary>
        public void Fling(Vector3 at, Vector3 vel, float size)
        {
            var d = Take(); if (d == null) return;
            d.Live = true; d.Falling = true; d.Age = 0f; d.Life = _life; d.ClingFor = 0f; d.Size = size; d.Cling = Vector3.Zero;
            d.Vel = vel; d.Pos = at;
            d.Mi.Visible = true; d.Mi.Scale = Vector3.One * d.Size; d.Mi.GlobalPosition = d.Pos;
            Made++;
        }
        /// <summary>The drops' tick; a falling drop is gone at the floor (<paramref name="floorY"/>, NaN = none known) or at the end of its life.</summary>
        public void Tick(float dt, Vector3 mouth, Basis b, bool facesPlusZ, float floorY)
        {
            foreach (var d in _drops)
            {
                if (!d.Live) continue;
                d.Age += dt;
                if (d.Falling && !float.IsNaN(floorY) && d.Pos.Y <= floorY + _mesh.Radius) { d.Live = false; d.Mi.Visible = false; continue; }
                if (!d.Falling && d.Age <= d.ClingFor) { d.Pos = mouth + b * d.Cling; d.Mi.Scale = Vector3.One * (d.Size * Mathf.Lerp(0.2f, 1f, d.ClingFor > 0f ? d.Age / d.ClingFor : 1f)); }
                else
                {
                    if (!d.Falling) { d.Falling = true; Vector3 outward = facesPlusZ ? b.Z : -b.Z; outward.Y = 0f; d.Vel = (outward.LengthSquared() > 1e-6f ? outward.Normalized() : Vector3.Zero) * 0.25f + b.X * (d.Cling.X * 0.6f); }
                    float fall = d.Age - d.ClingFor;
                    d.Vel += Vector3.Down * (_boo.DroolGravity * dt); d.Pos += d.Vel * dt;
                    float left = d.Life - fall;
                    if (left <= 0f) { d.Live = false; d.Mi.Visible = false; continue; }
                    d.Mi.Scale = Vector3.One * (d.Size * Mathf.Clamp(left / (d.Life * 0.33f), 0.05f, 1f));
                }
                d.Mi.GlobalPosition = d.Pos;
            }
        }
        public void HideAll() { foreach (var d in _drops) { d.Live = false; d.Mi.Visible = false; } }
    }
    /// <summary>The tongue stretched from the mouth to <paramref name="hand"/>: its +Z along the line, scaled to the length, its width kept.</summary>
    private void TongueTo(Vector3 hand)
    {
        if (_tongue == null) return;
        Vector3 mouth = MouthPoint;
        Vector3 d = hand - mouth; float len = d.Length();
        if (len < 0.05f) { TongueRest(); return; }
        Vector3 z = d / len;
        Vector3 x = Vector3.Up.Cross(z); if (x.LengthSquared() < 1e-6f) x = Vector3.Right.Cross(z); x = x.Normalized();
        Vector3 y = z.Cross(x);
        _tongue.GlobalTransform = new Transform3D(new Basis(x * ModelScale, y * ModelScale, z * (len / TongueModelLength)), mouth);
        TongueLength = len; TongueTip = hand;
    }
    private void SetFlash(float f) { foreach (var m in _mats) m.SetShaderParameter("flash", f); }

    private Mario MarioNow()
    {
        if (_mario == null || !IsInstanceValid(_mario)) { _mario = GetTree().GetFirstNodeInGroup("player") as Mario; _marioRidKnown = false; }
        if (_mario != null && !_marioRidKnown) { _marioRid = _mario.GetRid(); _marioRidKnown = true; }
        return _mario;
    }
    private SmsKingBooRematch BossNow()
    {
        if (_boss == null || !IsInstanceValid(_boss)) _boss = GetTree().GetFirstNodeInGroup("sms_rematch_boss") as SmsKingBooRematch;
        return _boss;
    }

    /// <summary>The floor under (x, z): a ray from 3 m over the point, Mario's body left out; NaN = none.</summary>
    private float FloorUnder(Vector3 p)
    {
        var space = GetWorld3D()?.DirectSpaceState; if (space == null) return float.NaN;
        var q = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 3f, p + Vector3.Down * 30f);
        q.CollideWithAreas = false; q.CollideWithBodies = true;
        if (_marioRidKnown) q.Exclude = new Godot.Collections.Array<Rid> { _marioRid };
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].AsVector3().Y : float.NaN;
    }

    /// <summary>The face's yaw (the model's +Z, or -Z) and the nose's pitch (positive = down) as a basis.</summary>
    private Basis FaceBasis(float yaw, float pitchDeg)
    {
        Basis b = new Basis(Vector3.Up, yaw + (FacesPlusZ ? 0f : MathF.PI));
        return b * new Basis(Vector3.Right, Mathf.DegToRad(FacesPlusZ ? pitchDeg : -pitchDeg));
    }
    /// <summary>goToRandomNextGraphNode with no graph: a random point within WanderRadiusUnits (0.3..1 of it) of the base.</summary>
    private void NextGoal()
    {
        float a = Rand() * MathF.Tau, r = WanderRadiusUnits / U * (0.3f + 0.7f * Rand());
        _goal = new Vector3(_base.X + MathF.Cos(a) * r, _base.Y, _base.Z + MathF.Sin(a) * r);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || State == Phase.Gone) return;
        float dt = (float)delta; Age += dt; _t += dt;
        if (_boostLeft > 0f) _boostLeft -= dt;
        if (_grace > 0f) _grace -= dt;
        var mario = MarioNow();
        _drool?.Tick(dt, MouthPoint, GlobalTransform.Basis.Orthonormalized(), FacesPlusZ, _floorY);   // 179: the drops out there fall on, shocked or not, to the floor
        _sweat?.Tick(dt, MouthPoint, GlobalTransform.Basis.Orthonormalized(), FacesPlusZ, _floorY);   // 180: the sweat beads too
        switch (State)
        {
            case Phase.Rising:
            {
                float k = RiseSeconds > 0f ? Mathf.Clamp(_t / RiseSeconds, 0f, 1f) : 1f;
                if (float.IsNaN(_floorY))
                {
                    Vector3 spot = GlobalPosition;
                    _floorY = FloorUnder(spot); _base = new Vector3(spot.X, (float.IsNaN(_floorY) ? spot.Y : _floorY) + BaseLiftOverFloor, spot.Z);
                    _spawnY = spot.Y - (RiseFromUnder ? SpawnDepth : 0f); RiseStartY = _spawnY;        // 179: it starts under the floor and comes up through it
                    if (RiseFromUnder) GlobalPosition = spot with { Y = _spawnY };
                    if (RiseSmoke) FireRiseSmoke(new Vector3(spot.X, (float.IsNaN(_floorY) ? spot.Y : _floorY) + 0.05f, spot.Z));
                }
                float s = Mathf.SmoothStep(0f, 1f, k);
                TongueSway(dt, 0f);
                if (RiseFromUnder) SetScaleK(Mathf.Lerp(RiseScaleFrom, 1f, s), 1f); else SetScaleK(Mathf.Max(0.01f, s), s);
                GlobalPosition = GlobalPosition with { Y = Mathf.Lerp(_spawnY, _base.Y, s) };
                FaceMario(mario, dt);
                if (k >= 1f) { State = Phase.Drifting; _t = 0f; SetScaleK(1f, 1f); _wait = WaitTicks / 2; NextGoal(); }
                break;
            }
            case Phase.Lobbed:                                                                                     // 200: spat by King Boo -- the lob, the tumble, the fuse, the landing
            {
                _lobT += dt;
                _lobVel = _lobVel with { Y = _lobVel.Y - SmsFireballs.G * dt };
                Vector3 p = GlobalPosition + _lobVel * dt;
                _lobTumble += dt * LobTumbleDegPerSec;
                GlobalTransform = new Transform3D(FaceBasis(_yaw, _lobTumble % 360f), p);
                TongueSway(dt, 0f); TongueRest();
                float k = Mathf.Clamp(_lobT / Mathf.Max(0.1f, LobFlightSeconds), 0f, 1f);
                SetFlash(_lobDud ? 0.08f + 0.06f * MathF.Sin(_lobT * 10f) : 0.1f + 0.12f * MathF.Sin(_lobT * (8f + 22f * k)));   // the fuse: quicker as it comes down
                if (_lobMarkMat != null) _lobMarkMat.AlbedoColor = LobMarkColor with { A = LobMarkColor.A * (0.55f + 0.45f * MathF.Sin(_lobT * (6f + 14f * k))) };
                if (_lobTrail != null && IsInstanceValid(_lobTrail)) _lobTrail.GlobalPosition = p;
                if (p.Y <= _floorY + LobLandLift && _lobVel.Y < 0f)
                {
                    p.Y = _floorY + LobLandLift; GlobalPosition = p; HasLanded = true; LandedAt = p;
                    if (_lobTrail != null && IsInstanceValid(_lobTrail)) { _lobTrail.Emitting = false; var tr = _lobTrail; GetTree().CreateTimer(1.0).Timeout += () => { if (IsInstanceValid(tr)) tr.QueueFree(); }; _lobTrail = null; }
                    if (_lobMark != null && IsInstanceValid(_lobMark)) { _lobMark.QueueFree(); } _lobMark = null; _lobMarkMat = null;
                    if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: the lob comes down at {p} after {_lobT:F2} s ({Flat2(p, LobTarget):F2} m off its mark){(_lobDud ? " -- a DUD: it floats here, the fuse burning " + _dudFuse.ToString("F1") + " s" : " -- the BLAST")}");
                    EmitSignal(SignalName.Landed, _lobDud);
                    if (!_lobDud) { Detonate(); return; }
                    // the dud: a thud of black smoke, then it floats from here as one of the supply's (Drop()'s re-basing), its fuse still burning
                    if (RiseSmoke) FireRiseSmoke(new Vector3(p.X, _floorY + 0.05f, p.Z));
                    State = Phase.Drifting; _t = 0f; _grace = DropGraceSeconds; SetFlash(0f);
                    _base = new Vector3(p.X, _floorY + BaseLiftOverFloor, p.Z);
                    _flyCur = Mathf.Clamp((p.Y - _base.Y) * U - _bob, 0f, _flyTarget);
                    if (mario != null) { Vector3 tm = mario.GlobalPosition - p; if (tm.LengthSquared() > 1e-4f) _yaw = MathF.Atan2(tm.X, tm.Z); }
                    _rotX = 0f; GlobalTransform = new Transform3D(FaceBasis(_yaw, _rotX), p);
                    TongueRest(); _wait = WaitTicks; NextGoal();
                    if (!string.IsNullOrEmpty(AppearSe)) _sound?.Play(AppearSe);
                }
                break;
            }
            case Phase.Drifting:
            {
                if (ExpireSeconds > 0f && _t >= ExpireSeconds) { Vanish(); return; }
                if (_lobDud && _dudFuse > 0f)                                                                       // 200: the dud's fuse -- it blasts where it floats when that runs out (grabbed, the hold's own clock takes over)
                {
                    _dudFuse -= dt;
                    if (_dudFuse <= 0f) { Detonate(); return; }
                    if (_dudFuse < 2f) SetFlash(0.12f + 0.12f * MathF.Sin(_t * (12f + (2f - _dudFuse) * 14f)));
                }
                // 179: looked at -> the shock; shocked, it holds where it hangs until he has looked away long enough
                LookedAtNow = LookShock && mario != null && LookedAt(mario);
                if (Shocked)
                {
                    _shockT += dt; _lookAwayT = LookedAtNow ? 0f : _lookAwayT + dt;
                    if (_shockT >= ShockMinSeconds && _lookAwayT >= LookAwaySeconds) SetShock(false);
                }
                else if (LookedAtNow) SetShock(true);
                if (Shocked) { ShockTick(dt, mario); break; }
                Vector3 p = GlobalPosition;
                Vector3 basePos = new Vector3(p.X, _base.Y, p.Z); Vector3 base0 = basePos;
                if (SeeksMario)
                {
                    if (mario != null)
                    {
                        Vector3 d = mario.GlobalPosition - basePos; d.Y = 0f;
                        float dist = d.Length();
                        if (dist > DriftStopDistance && dist > 1e-3f) { basePos += d / dist * MathF.Min(_march * 60f / U * dt, dist - DriftStopDistance); _yaw = Mathf.LerpAngle(_yaw, MathF.Atan2(d.X, d.Z), Mathf.Clamp(4f * dt, 0f, 1f)); }
                    }
                }
                else
                {
                    // TNerveWalkerGraphWander for a flyer: wait at a goal, then walkToCurPathNode -- turn toward the next by TurnSpeed a tick, move MarchSpeed a tick along the face
                    if (_wait > 0) _wait--;
                    else
                    {
                        Vector3 to = _goal - basePos; to.Y = 0f;
                        if (to.Length() < ReachedUnits / U) { GoalsReached++; _wait = WaitTicks; NextGoal(); }
                        else
                        {
                            float want = MathF.Atan2(to.X, to.Z);
                            float diff = Mathf.Wrap(want - _yaw, -MathF.PI, MathF.PI);
                            float step = Mathf.DegToRad(_turn) * 60f * dt;
                            _yaw += Mathf.Clamp(diff, -step, step);
                            basePos += new Vector3(MathF.Sin(_yaw), 0f, MathF.Cos(_yaw)) * (_march * 60f / U * dt);
                        }
                    }
                }
                // setBehavior: the fly height +1 u a tick to its target, the bob phase +1 a tick round its frequency, the tilt with the bob (eased as SmsTelesa eases it)
                float ticks = 60f * dt;
                _flyCur = Mathf.MoveToward(_flyCur, _flyTarget, ticks);
                _bobPhase += ticks; if (_bobPhase > _bobFreq) _bobPhase -= _bobFreq;
                float ph = Mathf.DegToRad(_bobPhase * 360f / _bobFreq);
                _bob = _bobAmp * MathF.Sin(ph);
                float nx = FlyAngBase - FlyAngMax * MathF.Cos(ph);
                _rotX = MathF.Abs(nx - _rotX) <= 5f ? nx : Lerp(_rotX, nx, 0.2f);
                GlobalTransform = new Transform3D(FaceBasis(_yaw, _rotX), basePos + Vector3.Up * ((_flyCur + _bob) / U));
                TongueSway(dt, dt > 1e-5f ? new Vector2(basePos.X - base0.X, basePos.Z - base0.Z).Length() / dt : 0f); TongueRest();
                if (TouchHurts && mario != null) TouchCheck(mario);
                break;
            }
            case Phase.Carried:
            {
                if (mario == null || !IsInstanceValid(mario)) { Drop(); return; }
                Vector3 hand = mario.SmsLassoHand;
                _settle = Mathf.Min(1f, _settle + dt / Mathf.Max(0.01f, OrbitSettleSeconds));
                float k = Mathf.SmoothStep(0f, 1f, _settle);
                _orbitAngle += Mathf.DegToRad(OrbitSpeedNow) * dt * (OrbitClockwise ? -1f : 1f);
                float r = Mathf.Lerp(_r0, OrbitRadius, k);
                JumpLiftNow = Mathf.MoveToward(JumpLiftNow, mario.IsOnFloor() ? 0f : JumpSwingLift, dt * JumpSwingLift / Mathf.Max(0.02f, JumpSwingSeconds));   // 178: the swing rises with his jump
                float y = Mathf.Lerp(_y0, hand.Y + OrbitOverHand + JumpLiftNow, k);
                Vector3 c = mario.GlobalPosition;
                Vector3 p = new Vector3(c.X + MathF.Cos(_orbitAngle) * r, y, c.Z + MathF.Sin(_orbitAngle) * r);
                OrbitRadiusNow = r;
                // facing in at Mario (the tongue in its mouth leads to his hand), leaning outward with the swing
                Vector3 outward = new Vector3(p.X - c.X, 0f, p.Z - c.Z);
                Basis b = GlobalTransform.Basis.Orthonormalized();
                if (outward.LengthSquared() > 1e-6f)
                {
                    outward = outward.Normalized();
                    _yaw = MathF.Atan2(-outward.X, -outward.Z);
                    b = new Basis(Vector3.Up, _yaw + (FacesPlusZ ? 0f : MathF.PI));
                    Vector3 tangent = Vector3.Up.Cross(outward);
                    if (tangent.LengthSquared() > 1e-6f) b = b.Rotated(tangent.Normalized(), Mathf.DegToRad(OrbitLeanDeg) * k);
                }
                GlobalTransform = new Transform3D(b, p);
                TongueTo(hand);
                // 160: no pulse on the plain swing (a tenth of white on a near-black body read as a GREY Boo, Jaysonn's screenshot); the boost and the hold's warning show
                float pulse = _boostLeft > 0f ? 0.06f + 0.04f * MathF.Sin(_t * 18f) : 0f;
                if (HoldSeconds > 0f)
                {
                    _held += dt;
                    float left = HoldSeconds - _held;
                    if (left <= 0f) { Explode(false); return; }
                    if (left < 2f) pulse = 0.12f + 0.12f * MathF.Sin(_t * (12f + (2f - left) * 14f));
                }
                SetFlash(pulse);
                foreach (var boss in BoosNow())                                                        // 177: the real King Boo or a COPY of him -- whichever body it is swung into
                    if (boss != null && IsInstanceValid(boss) && boss.CanBeHit && boss.InHitCylinder(p, OwnRadius * ModelScale)) { _hitBoss = boss; Explode(true); return; }
                break;
            }
        }
    }
    private float _spawnY;

    /// <summary>TTelesa::attackToMario on its attack cylinder against his damage cylinder (42 u across, 160 tall): the hit unless he is above it -- his feet over the
    /// body's middle less 50 u. His own invincibility (the common profile's 300 ticks) spaces the hits.</summary>
    private void TouchCheck(Mario mario)
    {
        Vector3 m = mario.GlobalPosition, g = GlobalPosition;
        float marioR = SmsUnits.UnitsToMetres(42f), marioH = SmsUnits.UnitsToMetres(SmsUnits.MarioHeightUnits);
        float dxz = new Vector2(m.X - g.X, m.Z - g.Z).Length();
        if (dxz > TouchRadius + marioR) return;
        float lo = g.Y - TouchHeight * 0.5f, hi = g.Y + TouchHeight * 0.5f;
        if (m.Y + marioH < lo || m.Y > hi) return;
        if (m.Y >= g.Y - 50f / U) return;                        // above it: no hurt (the game's y + attackHeight - 50 against the body's foot; our origin is the body's middle)
        if (mario.stateOfMario == Mario.MarioState.hurt || mario.stateOfMario == Mario.MarioState.dead) return;
        int hp = mario.SmsHP;
        mario.TakeDamage(g, Mario.SmsDamageProfile.EnemyCommon);
        if (mario.SmsHP < hp || mario.stateOfMario == Mario.MarioState.hurt)
        {
            Touches++;
            if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: touched Mario -- hp {hp} -> {mario.SmsHP}");
            EmitSignal(SignalName.Touched);
        }
    }

    private void FaceMario(Mario mario, float dt)
    {
        if (mario == null) return;
        Vector3 d = mario.GlobalPosition - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() < 1e-4f) return;
        float want = MathF.Atan2(d.X, d.Z);
        _yaw = Mathf.LerpAngle(_yaw, want, Mathf.Clamp(4f * dt, 0f, 1f));
        GlobalTransform = new Transform3D(FaceBasis(_yaw, _rotX), GlobalPosition);
    }

    /// <summary>Mario's spin reached it: he has the tongue -- it swings round him from where it is (false when not free, or within the drop's grace).</summary>
    public bool Grab(Mario mario)
    {
        if (State != Phase.Drifting || _grace > 0f || mario == null) return false;
        SetShock(false);                                                                                 // 179: grabbed, the shock is over
        _mario = mario; _marioRidKnown = false;
        State = Phase.Carried; _t = 0f; _boostLeft = 0f; _held = 0f; _settle = 0f;
        Vector3 rel = GlobalPosition - mario.GlobalPosition;
        _orbitAngle = MathF.Atan2(rel.Z, rel.X);
        _r0 = MathF.Max(0.3f, new Vector2(rel.X, rel.Z).Length()); _y0 = GlobalPosition.Y;
        OrbitRadiusNow = _r0; JumpLiftNow = 0f;
        if (!string.IsNullOrEmpty(GrabSe)) _sound?.Play(GrabSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: grabbed by the tongue at {_r0:F2} m -- the swing");
        EmitSignal(SignalName.Grabbed);
        return true;
    }
    /// <summary>His spin jump while he swings it: faster for a while.</summary>
    public void SpinBoost() { if (State == Phase.Carried) { _boostLeft = SpinJumpBoostSeconds; if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: the swing x{SpinJumpBoost} for {SpinJumpBoostSeconds} s"); } }
    /// <summary>Let go (a dive, a hit, a death): it floats again from where the swing left it -- a new base under it, its fly height risen to again; no grab for DropGraceSeconds.</summary>
    public void Drop()
    {
        if (State != Phase.Carried) return;
        State = Phase.Drifting; _t = 0f; _grace = DropGraceSeconds; _boostLeft = 0f; SetFlash(0f);
        _floorY = FloorUnder(GlobalPosition);
        _base = new Vector3(GlobalPosition.X, (float.IsNaN(_floorY) ? _base.Y : _floorY + BaseLiftOverFloor), GlobalPosition.Z);
        _flyCur = Mathf.Clamp((GlobalPosition.Y - _base.Y) * U - _bob, 0f, _flyTarget);
        GlobalTransform = new Transform3D(FaceBasis(_yaw, _rotX), GlobalPosition);
        TongueRest(); _wait = WaitTicks; NextGoal();
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: let go");
        EmitSignal(SignalName.Dropped);
    }
    /// <summary>The burst: the puff (BurstFxScene recoloured), the sound, King Boo's hit when it was swung into him, Mario's hand emptied, and the Boo gone.</summary>
    public void Explode(bool hitKingBoo)
    {
        if (State == Phase.Gone) return;
        if (State == Phase.Carried) MarioNow()?.SmsBombGone(this);
        State = Phase.Gone;
        Puff(hitKingBoo ? 1.6f : 1.1f);
        if (!string.IsNullOrEmpty(BurstSe)) _sound?.Play(BurstSe);
        if (hitKingBoo && ExplodeOnKingBoo) Blast();
        if (hitKingBoo) (_hitBoss != null && IsInstanceValid(_hitBoss) ? _hitBoss : BossNow())?.TakeHit(this);   // 177: the one it burst on (a copy is found out, no damage)
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: burst{(hitKingBoo ? " on King Boo" : "")} at {GlobalPosition}");
        EmitSignal(SignalName.Burst, hitKingBoo);
        Gone();
    }
    /// <summary>200: SPAT by King Boo (the Bomb Boo volley, SmsBooVolley) -- it flies from <paramref name="from"/> on a lob to <paramref name="target"/> (a floor point:
    /// the apex <paramref name="apexOver"/> metres over the higher of the mouth and the landing, SmsFireballs.G the gravity), tumbling, its fuse flashing quicker as it
    /// falls, a ring on the floor marking the blast's footprint. Landed, <see cref="Detonate"/> -- or, <paramref name="dud"/>, it floats there as a supply Boo with
    /// <paramref name="dudFuseSeconds"/> on its fuse (0 = it never blasts on its own).</summary>
    public void Lob(Vector3 from, Vector3 target, float apexOver, bool dud, float blastRadius, float blastHeight, float dudFuseSeconds)
    {
        if (State == Phase.Gone || State == Phase.Carried) return;
        State = Phase.Lobbed; _t = 0f; _lobT = 0f; _lobTumble = 0f; _lobDud = dud; LobDud = dud; _lobBlastR = Mathf.Max(0.5f, blastRadius); _lobBlastH = Mathf.Max(0.2f, blastHeight); _dudFuse = dud ? Mathf.Max(0f, dudFuseSeconds) : 0f;
        _floorY = target.Y; LobFrom = from; LobTarget = target; HasLanded = false; HasBlasted = false; BlastHitMario = false; _grace = DropGraceSeconds;
        GlobalPosition = from; SetScaleK(1f, 1f); SetShock(false);
        float g = SmsFireballs.G, landY = target.Y + LobLandLift;
        float apexY = Mathf.Max(from.Y, landY) + Mathf.Max(0.5f, apexOver);
        float vy0 = MathF.Sqrt(2f * g * Mathf.Max(0.1f, apexY - from.Y)), tUp = vy0 / g, tDown = MathF.Sqrt(2f * Mathf.Max(0.05f, apexY - landY) / g);
        LobFlightSeconds = tUp + tDown;
        _lobVel = new Vector3((target.X - from.X) / LobFlightSeconds, vy0, (target.Z - from.Z) / LobFlightSeconds);
        if (new Vector2(_lobVel.X, _lobVel.Z).LengthSquared() > 1e-6f) _yaw = MathF.Atan2(_lobVel.X, _lobVel.Z);
        GlobalTransform = new Transform3D(FaceBasis(_yaw, 0f), from);
        // the mark on the floor: the blast's footprint (a dud: the body's own)
        LobMarkRadius = dud ? OwnRadius * ModelScale * 1.3f : _lobBlastR;
        var tex = SmsFireballs.Tex("hamonring3_flat");
        _lobMarkMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoTexture = tex, AlbedoColor = LobMarkColor };
        _lobMark = new MeshInstance3D { Name = "LobMark", TopLevel = true, Mesh = new QuadMesh { Size = Vector2.One * (LobMarkRadius * 2f) }, MaterialOverride = _lobMarkMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_lobMark); _lobMark.GlobalPosition = new Vector3(target.X, target.Y + 0.08f, target.Z); _lobMark.Rotation = new Vector3(-MathF.PI / 2f, 0f, 0f);
        if (LobTrail)
        {
            _lobTrail = SmsSmashFx.Trail(LobTrailColor, 0.7f, 0.35f); _lobTrail.Name = "LobTrail"; _lobTrail.TopLevel = true; _lobTrail.Amount = 40; _lobTrail.Lifetime = 0.6;
            AddChild(_lobTrail); _lobTrail.GlobalPosition = from; _lobTrail.Emitting = true;
        }
        Lobs++; if (dud) Duds++;
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: SPAT from {from} toward {target} ({Flat2(from, target):F1} m), the apex {apexY - from.Y:F1} m up, {LobFlightSeconds:F2} s in the air{(dud ? $", a DUD (fuse {_dudFuse:F1} s)" : $", the blast {_lobBlastR:F1} m")}");
    }
    /// <summary>200: the blast on the floor -- the puff and the Bob-omb explosion where it is, a ring of light out to the blast radius, Mario within that radius whose
    /// feet are under the blast height hit (the common enemy damage); then gone. A dud's fuse ends here too.</summary>
    public void Detonate()
    {
        if (State == Phase.Gone) return;
        if (State == Phase.Carried) MarioNow()?.SmsBombGone(this);
        State = Phase.Gone; HasBlasted = true; FloorBlasts++;
        Vector3 at = GlobalPosition; float fy = float.IsNaN(_floorY) ? FloorUnder(at) : _floorY; if (float.IsNaN(fy)) fy = at.Y - LobLandLift;
        Puff(1.3f);
        if (!string.IsNullOrEmpty(BurstSe)) _sound?.Play(BurstSe);
        Blast();
        if (GetParent() != null) SmsSmashFx.Ring(GetParent(), new Vector3(at.X, fy, at.Z), 0.4f, _lobBlastR, 0.35f, LobBlastColor);
        var mario = MarioNow();
        if (mario != null && IsInstanceValid(mario))
        {
            Vector3 feet = mario.GlobalPosition; float mr = SmsParams.Mario.DamageRadius / U;
            if (feet.Y - fy < _lobBlastH && Flat2(feet, at) <= _lobBlastR + mr && mario.stateOfMario != Mario.MarioState.hurt && mario.stateOfMario != Mario.MarioState.dead)
            {
                int hp = mario.SmsHP;
                mario.TakeDamage(new Vector3(at.X, fy + 1f, at.Z), Mario.SmsDamageProfile.EnemyCommon);
                if (mario.SmsHP < hp || mario.stateOfMario == Mario.MarioState.hurt) { BlastHitMario = true; FloorBlastHits++; }
            }
        }
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: BLASTS on the floor at {at} ({_lobBlastR:F1} m) -- {(BlastHitMario ? "Mario HIT" : "Mario clear")}");
        EmitSignal(SignalName.Blasted, BlastHitMario);
        EmitSignal(SignalName.Burst, false);
        Gone();
    }
    private static float Flat2(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    /// <summary>Cleared (King Boo's hit clears the field; the expiry): a small puff and gone.</summary>
    public void Vanish()
    {
        if (State == Phase.Gone) return;
        if (State == Phase.Carried) MarioNow()?.SmsBombGone(this);
        State = Phase.Gone;
        Puff(0.9f);
        if (!string.IsNullOrEmpty(GoneSe)) _sound?.Play(GoneSe);
        if (DebugPrint) GD.Print($"[SMS bomb boo] {Name}: vanishes");
        Gone();
    }
    /// <summary>170: the Bob-omb's blast at the Boo, the boom, the shake.</summary>
    private void Blast()
    {
        if (ExplosionFxScene != null && IsInsideTree())
        {
            var fx = ExplosionFxScene.Instantiate<Node3D>();
            fx.TopLevel = true;
            GetParent().AddChild(fx);
            var at = new Transform3D(Basis.FromScale(Vector3.One * ExplosionScale), GlobalPosition);
            fx.GlobalTransform = at;
            if (fx is SmsFxBurst b) b.Play(at);
            else if (fx is GpuParticles3D gp) { gp.OneShot = true; gp.Emitting = true; gp.Finished += gp.QueueFree; }
            Blasts++;
        }
        if (!string.IsNullOrEmpty(ExplodeSe)) _sound?.Play(ExplodeSe);
        if (ExplosionShake > 0f) SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, ExplosionShake);
    }
    public static int Blasts;
    private void Puff(float scale)
    {
        if (BurstFxScene == null || !IsInsideTree()) return;
        var fx = BurstFxScene.Instantiate<Node3D>();
        fx.Transform = new Transform3D(Basis.FromScale(Vector3.One * scale), GlobalPosition);
        GetParent().AddChild(fx);
        if (fx is SmsFxBurst b)
        {
            b.SetShaderParam("prm", new Vector4(BurstColor.R, BurstColor.G, BurstColor.B, 1f));
            b.SetShaderParam("env", new Vector4(BurstColor.R * 0.4f, BurstColor.G * 0.4f, BurstColor.B * 0.4f, 1f));
        }
    }
    private void Gone()
    {
        // the sound node plays out on its own: the Boo's visuals go now, the node a moment later
        if (_model != null) _model.Visible = false;
        if (_tongue != null) _tongue.Visible = false;
        _drool?.HideAll(); _sweat?.HideAll();
        if (_lobMark != null && IsInstanceValid(_lobMark)) _lobMark.QueueFree(); _lobMark = null; _lobMarkMat = null;                       // 200: the lob's mark and trail go with it
        if (_lobTrail != null && IsInstanceValid(_lobTrail)) _lobTrail.Emitting = false;
        SetPhysicsProcess(false);
        if (IsInsideTree()) GetTree().CreateTimer(2.0).Timeout += QueueFree; else QueueFree();
    }
}
