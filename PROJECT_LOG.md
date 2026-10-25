# ProjectShine — Implementation Log

A consolidated reference for all systems built in this session. Organized by subsystem; each section lists the files involved, the key behaviors, the exported tunables, and any gotchas.

---

## Camera System

**Files:** `assets/SunshineCamera.cs` (new), `assets/Mario.cs` (CameraLocked property)

Replaces the old `CamController.cs`. SMS-faithful polar camera based on the `CPolarSubCamera` architecture in the SMS decomp.

### Architecture

- **Polar coordinates** around Mario: `yaw`, `pitch`, `radius`. Stored as `_curYaw/_tgtYaw` (and same for pitch/radius). Each frame `_cur` chases `_tgt` via `Mathf.Lerp` with `1 - exp(-rate * dt)`.
- **Scene hierarchy expected:**
  ```
  SunshineCamera (Node3D, TopLevel=true)
  └── SpringArmPivot (Node3D)            ← yaw rotation
      └── SpringArm3D                    ← pitch + wall collision
          └── Camera3D
  ```
- **TopLevel = true** so the camera doesn't inherit Mario's transform — it's positioned via `GlobalPosition`.
- **SpringArm3D** handles wall collision (pushback). Mario is added to its exclusion list automatically.

### Modes (`CamMode` enum)

| Mode | Trigger | Behavior |
|---|---|---|
| `Normal` | default | C-stick orbit + auto-behind when moving + no input recently |
| `LButton` | L held | Aggressive snap behind Mario, no orbit input accepted |
| `OverShoulder` | Y tap (toggle) | Mario freezes (or keeps airborne momentum), tight OS framing, L-stick free-look |
| `Free` | external | No auto-behind ever (cutscene staging) |
| `ShineGet` | `Mario.StartShineGet()` (gameplay event, not input) | Fixed low-angle hero shot during the shine-collect cutscene; see Shine Sprite section below |

L-tap (≤0.18s) = recenter pulse. L-hold = `LButton` mode.

### Auto-behind

- Engages when Mario is moving + no manual camera input for `AutoBehindDelay` (0.55s) + on floor (configurable).
- Suppressed when Mario is moving **toward the camera** (prevents the camera from spinning to chase backward motion).
- Uses **velocity** as primary direction source (immune to model flip conventions).
- `GetBehindYaw()` reads velocity; falls back to `_armature` orientation with optional `ArmatureForwardIsFlipped = true` for 180°-rotated rigs.

### Over-Shoulder mode

- Triggered by toggling `OverShoulderAction` (default `"button_y"`). Press once on, press again off.
- Enters via `EnterOverShoulder()`: sets targets only (no snap), starts `_modeTransitionTimer = OverShoulderEntryDuration` so the chase uses snap rates for a graceful sweep.
- Exits via `ExitOverShoulder()`: resets targets to Normal defaults using the same transition timer.
- **L-stick (not R-stick)** is used for control in this mode:
  - L-stick X turns Mario's body via `_armature.Rotation.Y` (with flip-aware sign).
  - L-stick Y tilts camera pitch (wider range `±75° / ±70°`).
- Camera yaw locked to Mario's visual facing each frame (`_tgtYaw = GetVisualYaw()`) so framing is always behind him.
- Look-at offset is in **Mario-local space** (`OverShoulderLocalOffset`), rotated by his visual yaw, so the shoulder framing tracks him cleanly.
- **Body-bone bend (chn_chest):** when camera pitch drops below `BodyBendStartPitchDeg = -25°`, Mario's `chn_chest` bone bends backward up to `BodyBendMaxDeg = 35°`. Lets him "look up" at the overhead camera. Resets to identity on exit.

### Linked Zoom (SMS-style pitch/radius coupling)

- `LinkPitchToRadius = true` (default): a single `_zoomT` (0..1) drives **both** pitch and radius along a fixed curve. `t=0` → closest+level, `t=1` → farthest+overhead. C-stick Y modifies `_zoomT` only.
- Disable for independent control: `LinkPitchToRadius = false`. Old behavior comes back (C-stick Y modifies pitch and radius separately).
- `DefaultZoomT` is computed from `BaseRadius` at startup; this becomes the recenter / OS-exit target.

### `CameraLocked` (Mario.cs property)

```csharp
public bool CameraLocked { get; set; } = false;
```

- Set true/false by SunshineCamera on OS enter/exit (and usable by cutscenes / talk later).
- **On floor:** Mario freezes completely (velocity zero).
- **Airborne:** keeps XZ momentum, applies gravity + terminal velocity cap, calls `MoveAndSlide`. Mario's trajectory continues so the SMS Y-turn / Kenny-kick mechanic works.
- Player input is fully ignored; no jumps, dives, etc. fire.
- Physics-driven state transitions still happen: just-landed triggers `SetMarioState(landing)` so the landing animation plays correctly even while locked.

### Key Inspector Exports

| Field | Default | Effect |
|---|---|---|
| `MinRadius / BaseRadius / MaxRadius` | 3.5 / 6 / 11 | Polar radius range |
| `MinPitchDeg / DefaultPitchDeg / MaxPitchDeg` | -55 / -18 / 10 | Pitch clamps. Note: positive pitch = camera below pivot |
| `LinkPitchToRadius` | true | SMS-style linked zoom |
| `CStickYInvertedPitch` | true | Up = closer + level (SMS direction); only used when link is off |
| `AutoBehindRate / AutoBehindDelay` | 0.8 / 0.55s | Auto-rotate gentleness |
| `SuppressWhenApproaching / ApproachConeDeg` | true / 25° | Prevent spin-around when moving toward camera |
| `ArmatureForwardIsFlipped` | true | Set to match your model's import |
| `OverShoulderLocalOffset` | (-0.55, 1.75, 0) | OS framing — Mario in lower-right by default |
| `OverShoulderRadius` | 1.4 | OS distance |
| `OverShoulderDefaultPitchDeg` | 5 | OS entry pitch (slight up-look) |
| `OverShoulderEntryDuration` | 0.35s | Swing-behind transition time |
| `BodyBoneName` | "chn_chest" | Bone used for waist bend |
| `BodyBendStartPitchDeg / BodyBendFullPitchDeg / BodyBendMaxDeg` | -25 / -65 / 35 | When and how much Mario bends |
| `HeightPanRate / HeightPanLeashUp / HeightPanLeashDown` | 7 / 1.2 / 2.5 | Vertical follow smoothing |

---

## Palm Tree System

### Leaf bouncing (`assets/PalmLeaf.cs`)

- Attached to the `Leaves` Node3D inside a PalmTree.
- **Per-leaf bone mapping** — reads each leaf MeshInstance3D's `Skin.GetBindName(dominantBoneIdx)` to find its actual rig bone (not by sort order). Each leaf knows which bone drives it.
- On Mario landing inside the leaves: finds the leaf bone closest to Mario's XZ angle and triggers `Bounce(marioPos)`.
- One-cycle bounce: drop (`DropDuration = 0.12s`, depth `BounceDepth = 0.35`), then rise back (`RiseDuration = 0.75s`, ease-out).
- The `LeafBody` StaticBody3D is auto-added to the group `"palm_leaf_body"` so Mario's landing-detection picks it up.

### Leaf collision alignment (`assets/LeafCollisionFixer.cs`)

Editor tool script. Builds correct collision-shape transforms for each leaf by composing:
```
leafBody.GlobalTransform⁻¹ × skeletonRoot.GlobalTransform × mesh.Transform × Identity.Scaled(bindScale)
```

**Important bug fixes baked in:**
- Name match uses **exact equality** (`cs.Name == mesh.Name`), not `Contains` — fixes `mesh-1` greedily grabbing `mesh-10/11/12`.
- Bind transform uses `Transform3D.Identity.Scaled(...)`. `Vector3.Back` is `(0, 0, -1)` and would flip the Z axis if used as the third basis vector — explicit avoidance.

### Mario trunk climbing

**Files:** `assets/Mario.cs` (states + methods), `assets/PalmTreeGrab.cs` (Area3D trigger)

**New Mario states:**
- `treeGrab` — ma_tree_catch, brief
- `treeWait` — ma_tree_wait, idle on trunk
- `treeClimb` — ma_tree_climb, used for both up and down
- `treeMoveL` / `treeMoveR` — ma_tree_move_l/r, orbit around trunk
- `treeTopReach` — ma_tjmp2, plays on reaching top

**Mario.cs methods:**
- `TryGrabTree(treeRoot, trunkCenterXZ, trunkBottomY, trunkTopY, leafLandY, radiusBottom, radiusTop)` — public entry. Called by the GrabZone Area3D.
- `TickTreeGrab(delta)` — runs the catch animation timer.
- `TickTreeOnTrunk(delta, lstick)` — reads stick: up/down moves `_treeY`, left/right adjusts `_treeAngle`.
- `DoTreeJumpOff()` — fires on A button. Velocity outward + up. State → `wallJump`. 50/50 chance to play `ma_tjmp1` instead of `ma_wjmp` (with armature +Pi correction; unflipped on landing).
- `DoTreeTopReach()` — when `_treeY >= _treeTopY`. Teleports Mario to `(trunkCenter.x, leafLandY, trunkCenter.z)` so he lands on the leaves.
- `ExitTreeToFall()` — bottom release or ground-blocked release. Pushes Mario outward + small downward velocity so he doesn't snag the trunk.
- `SampleGroundUnderMario(out hit)` — raycast for ground detection during climb (so trunks-sunk-below-ground still let Mario drop off at the actual floor).

**Tapered grab radius:** `_treeRadiusBottom` and `_treeRadiusTop` lerp by `_treeY` between bottom and top. Defaults `1.4` / `0.6`.

**Other behaviors:**
- Spin-jump particles auto-disable on grab (`isSpining(false)`).
- Wall-slide state is cleanly canceled on grab.
- Dive grab is allowed (`diving` and `bellySlidingFromDive` removed from blocklist).

### PalmTreeGrab (`assets/PalmTreeGrab.cs`)

Area3D script. Inspector fields:

| Field | Default | Notes |
|---|---|---|
| `TrunkAxisPath` | — | Set to TrunkBody or leave empty (defaults to parent) |
| `TrunkBottomY` | -0.5 | Relative to tree's instance Y. Negative = below tree origin |
| `TrunkTopY` | 19.0 | Relative to tree's instance Y |
| `LeafLandY` | 19.5 | Where Mario lands when popping top |
| `TrunkRadiusBottom / TrunkRadiusTop` | 1.4 / 0.6 | Mario's grab distance from trunk axis |
| `RequireInputTowardTrunk` | true | Skip grab if stick is neutral AND velocity isn't toward trunk |
| `Climbable` | true | Toggle off for banana trees etc. (still bounces leaves, doesn't grab) |

**Scale awareness:** reads tree root's `GlobalTransform.Basis.Scale` and multiplies offsets/radii by it. So a scaled-down tree just works.

---

## Nail System

**File:** `assets/Nail.cs`

`AnimatableBody3D`-based scene. Tweens downward in `HitDropDistance` chunks per ground-pound hit.

- Expects an `Area3D` child (any name) used as the hit zone.
- Detects Mario in `groundPoundFalling` or `groundPoundLanding` state on `BodyEntered`.
- **Latches one hit per pound:** `_hitThisPound` is set on hit and cleared in `_PhysicsProcess` only when Mario has fully exited ground-pound states. Prevents the "nail moves out from under Mario → body_exited/entered re-fires → second hit on same pound" bug.
- On the final hit (`_hits >= RequiredHits`), spawns `ItemToSpawn` at `_restPos + (0, ItemSpawnYOffset, 0)`.
- **Spawn integration:** if the spawned item has a `LaunchAsDrop()` method (via `HasMethod`), the nail calls it so coins pop and bounce instead of floating.

| Field | Default | Effect |
|---|---|---|
| `ItemToSpawn` | — | Any PackedScene |
| `RequiredHits` | 2 | Hits to fully drive in |
| `HitDropDistance` | 1.5 | World units per hit |
| `DropDuration` | 0.18s | Animation per drop |
| `ItemSpawnYOffset` | 1.5 | Spawn Y above original nail position |

---

## Bounce Physics (Spawned Items)

**Files:** `assets/BouncePhysics.cs` (helper), `assets/YellowCoin.cs`, `assets/RedCoin.cs`, `assets/BlueCoin.cs`

Shared static helper drives drop-and-rest physics for spawned items. Each coin exposes a `LaunchAsDrop()` method that's invoked by the Nail (or any spawner).

**`BouncePhysics.Tick(ref State, Node3D, delta)`:**
- Apply gravity, integrate position.
- Raycast down from the node (excludes player bodies in `"player"` group, so coins fall *through* Mario rather than resting on his head).
- On floor hit: bounce with restitution, or settle when Y velocity is below threshold.
- When settled, count toward `DESPAWN_AFTER_REST = 7s`.
- **Flash phase:** last 2s of the rest timer toggles `Visible` at an accelerating frequency (`FLASH_FREQ_START = 3 Hz` → `FLASH_FREQ_END = 12 Hz`).
- Returns `true` when the node should `QueueFree`.

| Constant | Value | Purpose |
|---|---|---|
| `GRAVITY` | -25 | Per-second |
| `INITIAL_POP_Y` | 8 | Pop velocity on Launch |
| `BOUNCE_RESTITUTION` | 0.45 | Energy retained per bounce |
| `HORIZ_DAMP_PER_BOUNCE` | 0.7 | XZ slowdown per bounce |
| `REST_VEL_THRESHOLD` | 0.6 | Below this Y vel, coin settles |
| `DESPAWN_AFTER_REST` | 7s | Total at-rest lifetime |
| `FLASH_START` | 5s | When flashing kicks in |

---

## Ground Pound

### GP-jump (chained jump out of ground pound)

**File:** `assets/Mario.cs`

- Pressing A during `groundPoundLanding` (any time during `ma_hiped` OR `ma_slped`) triggers a special jump.
- Uses `singleJump` state (same animation as normal jump), but:
  - Fixed velocity: `GroundPoundJumpVelocity = 19.5` (slightly higher than max double jump 17.77).
  - No variable-height hold: `initalJumpHold = false`.
  - Armature spins 360° during the rise. `_gpJumpSpinDuration` is computed as `velocity/gravity` so the full rotation lands at apex.
- Spin is driven each frame in `_Process`. On landing or state change, armature rotation resets to launch yaw.

### Streak trail during fall (`assets/GroundPoundEffects.cs`)

Self-contained `Node3D` script that creates three `CpuParticles3D` (white, blue, red) at runtime. No scene wiring required beyond placing it as a child of Mario named `GroundPoundEffects`.

- Vertical streaks emit in a ring around Mario, stationary (`StreakSpeed = 0`) — Mario falls *through* them, creating the trail effect.
- Mario.cs polls `groundPoundFx.SetActive(true)` only during `groundPoundFalling && !_groundPoundStalling`.
- Soft alpha texture (procedurally generated radial gradient) → fluffy/transparent look.

Tunables: `RingRadius`, `StreakLifetime`, `AmountPerColor`, `MaxAlpha`, `WhiteAmountMul`.

### Impact effect (`assets/GroundPoundImpactFx.cs`)

Self-contained `Node3D` child of Mario. Fires once on each ground-pound landing.

- **Dust-cloud shockwave:** soft tan/cream blob with low-amplitude lobes around the rim (procedural texture). Expands from `ShockwaveStartRadius` to `ShockwaveEndRadius` over `ShockwaveDuration = 0.28s`. Mix blend mode (not additive).
- **Radial blue streaks:** 12 capsule beams shoot outward at 35° from vertical, no gravity, straight lines. Additive blend, lifetime 0.3s.
- **TopLevel = true** so the FX stays anchored where Mario landed, even if he moves.
- **Floor-raycast self-positioning:** the FX raycasts downward from Mario's pos (excluding player bodies) to find the true floor surface — robust against capsule-offset bugs.

Mario.cs `TriggerLandingDust()` and the GroundPoundImpactFx are both called when Mario's `groundPoundFalling` transitions to `groundPoundLanding`.

---

## Mushroom 1-Up

**File:** `assets/Mushroom1Up.cs`

On pickup (entering `CirclePlayer` mode), the mushroom now:
- Sets `CollisionLayer = 0` and `CollisionMask = 0`.
- Disables all child `CollisionShape3D`s.

This stops the SpringArm-based camera from catching on the orbiting mushroom while it's animating around Mario's head.

---

## Slope / Floor Properties (Mario.tscn)

`Mario2.tscn` Mario node now has:
```
floor_max_angle = 1.134464     # 65° (was 50°, originally 45° default)
floor_snap_length = 0.6         # camera-snap-to-floor; default was 0 (caused flying off slopes)
floor_constant_speed = true     # no slowdown going uphill
floor_block_on_wall = true      # default but explicit
platform_on_leave = 2           # DoNothing — don't inherit platform velocity on leave
```

Collision shape is a **`CylinderShape3D`** (`height 1.8701391`, `radius 0.37304688`), swapped from the
original capsule — the capsule's rounded bottom was launching him off slope corners. The shape matters
beyond collision: see the belly-tilt sink below, which is derived from that radius.

These were missing entirely before, causing slope misbehavior. Documentation cheat-sheet:
- Mario flies off downhill → raise `floor_snap_length`.
- Mario slows uphill → ensure `floor_constant_speed = true`.
- Can't walk up a steep slope → raise `floor_max_angle` (50° = 0.873, 60° = 1.047, 65° = 1.134).

Angle bands, for reference:

| Angle from vertical | Classified as | Mario's state |
|---|---|---|
| 0° – 65° | Floor | Walkable |
| 65° – 75° | Wall (`floor_max_angle` exceeded) | `slipping` — slides down, can't stand |
| 75°+ | Wall (`WALL_TRUE_VERTICAL_MIN_ANGLE_DEG`) | `wallSlide` / `wallJump` eligible |

---

## Surface Tilt — belly-on-the-ground on slopes & moving platforms

`Mario.cs` → `UpdatePlatformTilt(double delta)`. Purely visual: rotates and offsets the **armature only**.
Never touches `GlobalPosition`, `Velocity`, or collision, so it can't push him through geometry.

Runs **last** in `_PhysicsProcess` so it layers on top of whatever yaw the state machine set that frame.

Active in three states: `diving`, `bellySlidingFromDive`, `slipping`.

### What it does

1. **Pitch** — leans him along his facing axis (plane pitching with level wings).
2. **Roll** — supplies the rest of the alignment, so his up-axis lands exactly on the surface normal.
   This is what makes him bank on a platform that tips sideways, and lie flush when sliding diagonally
   across a slope rather than straight down it.
3. **Sink** — drops the rig along its own (now surface-aligned) down axis to close the gap left by the
   collider, so the belly touches instead of hovering.

### Inspector exports (on Mario)

| Export | Default | Applies to | Effect |
|---|---|---|---|
| `TiltSlopeCompensation` | `1.0` | **all 3 states** | Scales the collider-derived gap `capRadiusWorld * sin(angle)`. `1.0` = true geometric value. `0` = disable. |
| `BellyTiltSurfaceOffset` | `0.15` | `diving`, `bellySlidingFromDive` | Flat sink (metres) for the **prone** pose's belly-above-origin height. |
| `SlipTiltSurfaceOffset` | `0.0` | `slipping` | Flat sink (metres) for the **upright** slip pose. Trim only — the slope term does nearly all the work at 65–75°. |

**Tune in this order:** `BellyTiltSurfaceOffset` on FLAT ground first (the slope term is zero there, so
it's the only thing acting), then check a ramp and only touch `TiltSlopeCompensation` if the exact
geometric value reads wrong. Splitting them this way means fixing ramps can't break flat ground.

### The sink formula

```
sink = capRadiusWorld * sin(slopeAngle) * TiltSlopeCompensation
     + (slipping ? SlipTiltSurfaceOffset : BellyTiltSurfaceOffset)
```

`capRadiusWorld` ≈ **0.373** (cylinder radius × world scale 1). `sin(slopeAngle)` is computed as
`new Vector2(normal.X, normal.Z).Length()` — the horizontal magnitude of a **unit** normal IS the sine,
so no trig call. Same `steepness` idiom `TickSlip` already uses.

### Computed sink at the defaults

| Slope | slope term | Belly (`+0.15`) | Slip (`+0.0`) |
|---|---|---|---|
| 0° (flat) | 0.000 | **0.150** | — |
| 10° | 0.065 | **0.215** | — |
| 20° | 0.128 | **0.278** | — |
| 30° | 0.187 | **0.337** | — |
| 45° | 0.264 | **0.414** | — |
| 60° | 0.323 | **0.473** | — |
| 65° (max floor) | 0.338 | **0.488** | **0.338** |
| 70° | 0.351 | — | **0.351** |
| 75° (slip ceiling) | 0.360 | — | **0.360** |

Belly slide can reach 65°; slip only exists in the 65–75° band.

### Why the slope term exists (the geometry)

The collider is a cylinder with a **flat bottom face that never tilts**. Resting it against an inclined
plane, the support point in the `-normal` direction maximises `(-t·cos - r·(n·u))` over axis height `t`
and radial direction `u` — that lands at `t = 0` for **every** angle. So contact is always on the
**bottom rim**, on the uphill side, leaving the body origin (centre of that face) hanging
`capRadiusWorld * sin(angle)` clear of the surface, measured perpendicular.

Sanity checks that it's the right formula: **0** on flat ground, and exactly `capRadiusWorld` against a
vertical wall (the pure side-contact case, recovered as a limit rather than needing its own branch).
Being bounded by 1.0 — unlike `tan` — it also can't blow up as a surface approaches vertical.

### ⚠️ Gotcha: the Mario body node is yawed 180° in every level

This is the big one, and it silently inverted the tilt for a long time. See also **Multi-Character System → the spawner must hand its rotation down to the character** — the same 180° is what makes `PlayerSpawner` transfer its basis to the spawned node instead of keeping it, since `Mario.cs` also uses that local basis as the movement input frame.

| Scene | Mario instance basis |
|---|---|
| `Rico4Secret.tscn` | `(-1,0,~0, 0,1,0, ~0,0,-1)` — 180° yaw |
| `secrect_level.tscn` | same |
| `Level.tscn` | same, **plus ~2° roll** (`0.0348995`) |

`armature.Transform` is a **LOCAL** transform. The tilt computes everything in **WORLD** space
(`Vector3.Up`, `GetFloorNormal()`, `YawFromDir(...)`), so writing a world rotation straight into the
local transform composes the body's 180° on top of it — which **mirrors the horizontal component of the
resulting up-axis**. The tilt amount stayed correct but leaned the opposite way, driving his face into
the slope.

Fix: conjugate only the **rotation axes** into body space —
`P⁻¹·Rot(v,θ)·P == Rot(P⁻¹v, θ)` — via `GlobalBasis.Orthonormalized().Inverse()`. Exact no-op when the
body is unrotated, so it's safe in any level.

**The yaw half deliberately stays in the local convention.** The mesh is authored backwards (local `+Z`
forward) and the body's 180° is precisely what cancels it. Converting the yaw too would snap him 180°
the instant the tilt engages.

### ⚠️ Gotcha: why pitch-then-roll, not shortest-arc

A shortest-arc rotation from `Up` to the normal rotates about `Up × normal` — an axis with no
relationship to his heading — so it drags the facing sideways as it tilts. Pitch-then-roll reaches the
identical final up-axis while leaving the heading exactly where the state machine put it, because the
roll axis **is** his forward axis and a rotation fixes its own axis.

That the roll lands `up` exactly on the normal isn't approximate: after the pitch,
`{right, upPitched, rollAxis}` is orthonormal and the normal has **no** component along `rollAxis`
(it's `normalInPitchPlane` plus a pure `right` term, both perpendicular to it), so it lies in the plane
the roll sweeps through.

### ⚠️ Gotcha: `UpdateRunBob` also writes `armature.Position`

It runs in `_Process` — **after** the tilt's `_PhysicsProcess` write — and its weight takes a few frames
to ease to zero when you dive out of a run. Without a guard it stomps the sink for exactly those frames.
`UpdateRunBob` now early-outs while `_wasPlatformTilting`, after the weight/phase update so it still
eases out normally.

The tilt also restores `armature.Position = armatureBaseLocalPos` on exit, so the sink can't leak into
the next state. The origin is rebuilt from `armatureBaseLocalPos` every frame, never accumulated, so it
can't creep over a long slide.

### Smoothing

Reuses `WALL_SLIDE_FACE_SPEED` (18) as the slerp rate toward the target basis — floor/wall normals
flicker frame-to-frame right at the classification boundary. Snaps instantly on the first tilting frame
rather than blending from a stale pose.

---

## Slip — sliding on too-steep ground

`Mario.cs` → `CanStartSlip()` / `EnterSlip()` / `TickSlip()`, plus the exit handling in the
post-`MoveAndSlide` block. State is `MarioState.slipping`.

Fires on ground steeper than `floor_max_angle` (65°) but shallower than
`WALL_TRUE_VERTICAL_MIN_ANGLE_DEG` (75°) — see the angle-band table in the Slope/Floor section.
Steeper than 75° is a real wall and goes to `wallSlide` / `wallJump` instead.

### Animations

| Situation | Animation | Chosen |
|---|---|---|
| Hit the face moving UPhill → slides backward | `ma_slpbk` | at entry, locked for the episode |
| Otherwise → faces downhill, slides forward | `ma_slip` | at entry, locked for the episode |
| Leaving slip for airborne (edge or escape hop) | `ma_slpla` | via `slipAirLaunchTimer` |

Locked via `_slipFacingUphill` at entry so the animation can't flip mid-slide. `SetMarioState` uses
`_sm.Start()` not `Travel()` for these — they're only graph-connected in the ground-pound/rollout
recovery neighbourhood, so `Travel()` pathfinds through unrelated clips and flashes them.

### Inspector exports

| Export | Default | Effect |
|---|---|---|
| `SlipMaxSpeed` | `22` | Top slide speed at vertical; actual cap is this × slope factor |
| `SlipGravityScale` | `1.0` | Fraction of real `GRAVITY` (62.2) pulling down the fall line. 1.0 = true physics |
| `SlipSteepnessCurve` | `1.0` | Exponent on `sin(angle)`. 1.0 = physics; higher exaggerates angle differences |
| `SlipSteerAccel` | `9.0` | Stick authority over the slide (m/s²) |
| `SlipMaxFallSpeed` | `-26` | Safety backstop on fall speed — **not** a speed control, see gotcha below |
| `SlipExitSlideMinSpeed` | `3.0` | Min horizontal speed on reaching flat ground to continue as a belly slide |
| `SlipExitSlideBoost` | `1.0` | Multiplier on speed carried from slip into the belly slide |

### Resulting speeds (at the defaults)

`accel = GRAVITY * SlipGravityScale * slopeFactor`, `maxSpeed = SlipMaxSpeed * slopeFactor`,
where `slopeFactor = sin(angle) ^ SlipSteepnessCurve`. `RUN_SPEED` is 11.55 for scale.

| Angle | sin | accel (m/s²) | max speed | vs RUN | time to max |
|---|---|---|---|---|---|
| 45° | 0.707 | 44.0 | 15.56 | 1.35× | 0.35 s |
| 65° | 0.906 | 56.4 | 19.94 | 1.73× | 0.35 s |
| 66° | 0.914 | 56.8 | 20.10 | 1.74× | 0.35 s |
| 70° | 0.940 | 58.4 | 20.67 | 1.79× | 0.35 s |
| 75° | 0.966 | 60.1 | 21.25 | 1.84× | 0.35 s |
| 90° | 1.000 | 62.2 | 22.00 | 1.90× | 0.35 s |

Previously these were hard constants `SLIP_SLIDE_SPEED = 6.5` / `SLIP_SLIDE_ACCEL = 14`, giving
**5.94 m/s** at 66° — about half walking pace — with accel 4.4× weaker than real gravity.

### ⚠️ Gotcha: `SLIP_MAX_FALL` was a hidden throttle

The old `SLIP_MAX_FALL = -13` clamp looked like a safety limit but was actually the binding speed
constraint. On a steep face the slide is almost entirely **vertical** — at 66° the downhill tangent is
`(0.407 horizontal, -0.914 vertical)` — so a fall-speed clamp of 13 caps the slide at `13 / 0.914 ≈
14.2 m/s` no matter what the speed cap says. Raising `SlipMaxSpeed` alone would have done nothing.

It's now clamped at runtime against the slide's own cap so it can never bind tighter:
```csharp
float fallFloor = Mathf.Min(SlipMaxFallSpeed, -(maxSpeed + WALL_STICK_SPEED + 1f));
```

### ⚠️ Gotcha: the angle barely varies inside the slip band

Across the entire 65–75° band, `sin` only runs **0.906 → 0.966** — about 7%. At
`SlipSteepnessCurve = 1.0` every slippable slope is effectively max speed and the angle is almost
unfelt. That's real physics, not a bug. `SlipSteepnessCurve` is the knob if you want it felt:

| curve | 65° | 75° | spread |
|---|---|---|---|
| 1.0 | 19.94 | 21.25 | 1.31 m/s |
| 2.0 | 18.07 | 20.53 | 2.46 m/s |
| 4.0 | 14.84 | 19.15 | 4.31 m/s |

Tradeoff: higher curve slows the shallow end. Matters much more if arbitrary-angle "slip zones" get
added later.

### Steering (modelled on the SMS decomp)

From `doSliding()` in [doldecomp/sms](https://github.com/doldecomp/sms) `src/Player/MarioRun.cpp`:
```c
mSlideVelX += sn * (mSlideVelZ * (mIntendedMag * 0.03125f)) * getSlideStickMult();
```
The key design point: **the stick never sets the slide direction.** It adds a small acceleration to an
existing slide velocity vector, scaled by stick magnitude × `0.03125` (~1/32 authority), so you carve
gradually rather than turning on a dime.

Ours works the same way. `slipSlideVel` is a vector **in the slope's tangent plane** (it used to be a
scalar speed pinned to the fall line, which made steering structurally impossible):

1. Re-project onto the *current* tangent plane each frame — curved surfaces and rotating platforms turn
   the normal underneath him, and carried velocity otherwise drifts out of the face.
2. Add gravity down the fall line.
3. Add stick acceleration, flattened onto the tangent plane so it can't push into or off the surface.
4. **Clamp the downhill component to never go negative** — steering can slow the descent but never
   reverse it, so no tuning of `SlipSteerAccel` makes a too-steep face climbable.
5. Cap total magnitude, so steering *redirects* speed rather than stacking on it.

He faces his actual slide direction (not the raw fall line) so steering reads on the model, within the
`_slipFacingUphill` choice locked at entry.

Not ported from SMS: per-surface friction params (`mSlipParamsNormal`, `mSlipParamsWaterGround`, …) and
the up/down-slope accel split. The single steepness-scaled accel covers that for now.

### Exiting onto walkable ground

He does **not** snap to idle. Momentum carries into `MarioState.bellySlidingFromDive`, which already
owns everything that should happen next — no duplicated logic:

| Behaviour | Provided by |
|---|---|
| Friction ramp-down | `Lerp(velocity, 0, .05)` in the belly-slide tick |
| Get-up when stopped, no input | → `gettingUpFromSliding` (`ma_lost`) under 0.1 speed |
| A → `bellyRollout` / `singleRollout` | `checkSpeedForBellyRoll()` |
| B → re-dive | belly-slide tick |
| Surface tilt continues | `bellySlidingFromDive` is already a tilt state |

**Speed carried** is only the HORIZONTAL part, since the floor absorbs the vertical: a 20.10 m/s slip at
66° arrives at `20.10 × cos(66°) ≈ **8.17 m/s**` (~0.71× RUN_SPEED), giving roughly a 1.4 s slide.
`SlipExitSlideBoost` exists if that reads too tame.

**Why the decision lives in the post-move slip block, not the landing block:** `justLanded` requires
`preSlideVelocity.Y < -3`, which a *slow* slip never reaches. The slip block keys off `IsOnFloor()`
directly so it fires either way. A guard in the landing block (`airStateAtImpact == slipping`) stops the
generic `EnterLanding`/`ma_laend` from overwriting the choice.

**Existing quirk, left alone:** `checkSpeedForBellyRoll()` returns true when speed is *low*, so it's
**slow → `bellyRollout`, fast → `singleRollout`**. It also tests per-axis rather than magnitude, so a
diagonal at 2.9/2.9 (≈4.1 m/s actual) still counts as "slow".

---

## HUD

### RedCoinHud (`Font/HudElements/RedCoinHud.cs`)

Added `HiddenOffsetY` export (default 200) controlling how far below the on-screen position the HUD rests when hidden. Was hardcoded to 80 previously.

---

## Mario states / animations summary

### State enum additions
```csharp
treeGrab, treeWait, treeClimb, treeMoveL, treeMoveR, treeTopReach
```

### Animation map (`SetMarioState` switch additions)
| State | Animation |
|---|---|
| `treeGrab` | `ma_tree_catch` (one-shot) |
| `treeWait` | `ma_tree_wait` (looping) |
| `treeClimb` | `ma_tree_climb` (looping) |
| `treeMoveL` | `ma_tree_move_l` (looping) |
| `treeMoveR` | `ma_tree_move_r` (looping) |
| `treeTopReach` | `ma_tjmp2` (one-shot) |

Tree animations have their loop modes set in `_Ready` (for the looping ones).

---

## Key gotchas / conventions used in this project

- **Skin bind scale ~51.48** on tree models. Critical when computing collision transforms.
- **Mario's armature is flipped 180°** (`ArmatureForwardIsFlipped = true` in SunshineCamera). Affects yaw computations.
- **`Vector3.Back` is `(0, 0, -1)` in Godot.** Avoid using it as a basis vector when you mean positive Z — bit my LeafCollisionFixer hard.
- **SpringArm3D extends along its local +Z**, not -Z (Godot's stated convention doesn't match observed behavior in 4.4).
- **`s16` BAM angles** in the SMS decomp — Godot uses `float` radians, no conversion needed but the chase semantics are equivalent (lerp toward target).
- Mario's character is in group `"player"`. Used by `BouncePhysics` raycast exclusion and GroundPoundImpactFx self-positioning.
- Coin scenes need an `Area3D` named (anything) for pickup detection — `PalmLeaf` and `Nail` both add their nodes to discoverable groups.
- **Canvas UI is authored against a 1152×648 base** (`stretch/mode=canvas_items`, no viewport override), stretched ~1.68× to the real window. Size `Control` pixel offsets for that base, not for what looks right at native screen resolution, or elements render way oversized (bit the `SignPopup` banner).
- **`SpringArm3D`'s collision avoidance can silently shorten a requested camera radius** in tight spots — always sanity-check `get_hit_length()` vs. the `spring_length` you set, and verify close-range camera framing with an actual screenshot (see TalkingPOV section above).
- **`CameraLocked` on Mario freezes velocity, not his current animation/pose** — anything that locks him mid-action should explicitly call `Mario.ForceStandingIdle()` (or similar) first, or he'll visibly hold whatever transient pose he was in.
- **`ma_mdl1.glb`'s root node had NO NAME, and Godot invents one — the invented name changed between engine versions.** Godot ≤4.6 called an unnamed glTF root `Armature`; 4.7 calls it `Node`. On the 4.7 upgrade the model silently re-imported and every one of the 200 animation `.res` files was re-baked with tracks pointing at `Node/Skeleton3D:...` while `Mario2.tscn` still had `Armature/Skeleton3D` — **6016 "couldn't resolve track" warnings and an editor that froze under the volume**. Restoring the files from git could not hold, because nothing in the project ever *specified* the name; the importer re-derived it on every import. **Fixed at the source:** the glb's JSON chunk was patched to give node 0 the explicit name `Armature` (BIN chunk untouched, byte-identical). **How to apply:** if you ever re-export this model from Blender, **name the armature object before exporting** or the unnamed root — and this entire bug — comes straight back. Symptom to watch for: mass `_update_caches: ... couldn't resolve track` spam right after any engine upgrade or reimport.
- **The Mario body node itself carries a 180° yaw in every level** (plus ~2° roll in `Level.tscn`), and `armature.Transform` is a LOCAL transform. Any world-space basis written straight into it gets the body's yaw composed on top — which mirrors the horizontal part of the result. Rotation *axes* must be conjugated into body space first (`GlobalBasis.Inverse()`). This silently inverted the surface tilt; see the Surface Tilt section. Note the yaw convention itself is load-bearing: the mesh is authored backwards and that 180° is what cancels it, so don't "fix" the body rotation.

---

## Files added/modified summary

### Added
- `assets/SunshineCamera.cs` — new camera
- `assets/PalmLeaf.cs` — leaf bounce
- `assets/PalmTreeGrab.cs` — climb trigger
- `assets/LeafCollisionFixer.cs` — tool
- `assets/Nail.cs` — nail item
- `assets/BouncePhysics.cs` — coin physics helper
- `assets/GroundPoundEffects.cs` — fall streaks
- `assets/GroundPoundImpactFx.cs` — landing burst
- `assets/RedCoinSwitch.cs`, `red_coin_switch.tscn` — red-coin challenge switch (ground pound → sign → 8-coin countdown → reward Shine)
- `assets/ShineTimer.cs` — generic countdown/count-up timer, HUD-agnostic
- `Font/HudElements/SignPopup.cs` + `.tscn`, `SignPopupLabelSettings.tres` — generic 7-line ruled-sign popup
- `Font/HudElements/TimerHud.cs` + `.tscn` — MM:SS:CC display for ShineTimer
- `Skyboxes/SecretCourseGlow/secret_course_glow_v9.gdshader` + `SecretCourseGlow_v9.tres`, `B_crasicmario.png`, `P_casino_glow2mm_level0.png` — Rico4 secret-course background (glow dot grid + climbing Mario sprite), shared/reusable for future secret courses

### Significantly modified
- `assets/Mario.cs` — tree climb states, GP-jump, CameraLocked, head-look hooks, `ForceStandingIdle()`, AIR_DIVE/AIR_ROLLOUT air-control accel tuning, `slipping` state, `UpdatePlatformTilt()` surface tilt + sink, etc.
- `assets/SunshineCamera.cs` — added `CamMode.TalkingPOV` (OverShoulder-style, locked, event-driven)
- `assets/YellowCoin.cs`, `RedCoin.cs`, `BlueCoin.cs` — LaunchAsDrop integration; `RedCoin` also gained `Collected` signal + spawn-puff
- `assets/Mushroom1Up.cs` — collision disable on pickup
- `assets/Mario2.tscn` — floor properties, capsule → `CylinderShape3D` collider swap
- `assets/secrect_level.tscn` — RedCoinSwitch instance wired to ShineTimer/SignPopup/CamController
- `Font/HudElements/RedCoinHud.cs` — HiddenOffsetY export
- `Font/HudElements/PalmTree.tscn` — collision shapes, scripts

---

## Next steps (not yet implemented)

- ~~**Talk-with-Pianta cam mode**~~ — done, see `CamMode.TalkingPOV` above (turned out to reuse OverShoulder's mechanics directly rather than needing a separate Free-style two-target rig).
- **Red-coin switch fail state** — nothing currently listens for `ShineTimer.TimerReachedTarget` on the red-coin challenge; undefined what happens if the player doesn't collect all 8 in time.
- **TalkingPOV framing is a fixed yaw offset** — works for this switch's placement; a switch/sign in a different spot with geometry on that particular side could clip the same way this one did before the radius got tuned down. Screenshot-check any new placement.
- **Shine persistence** — `GameData.cs` doesn't yet track collected shines (mirrors the existing blue-coin `HashSet<string>` pattern). Right now a shine can be re-collected on scene reload.
- **Episode-ending vs. bonus shine branch** — no scene-transition/hub-return system exists yet in this project at all, so every shine currently just resumes gameplay in place. Needed before "shine ends the level" can work.
- **Shine jingle/fanfare SFX** — no audio asset for this yet.
- **Banana tree leaf bounce config** — set up banana_tree.tscn structure to mirror PalmTree.tscn.
- **Optional: solid trimesh for trunk collision** — Mesh menu → Create Convex Collision Sibling on the trunk mesh, replace the capsule.

---

## Shine Sprite

### ⚠️ Asset swap — everything below "idle presentation" through "Eyes" describes a superseded model

The original `ShineSprite.glb` (textureless, broken skin) was replaced with a proper rip that has real UV-mapped textures, correct rigging, and — critically — **its own baked animations**: `shine_float` (idle, 1.29s), `shine_demo_shine_get` (6.9583335s — matches `ma_demo_shine_get`'s length to the full float precision, confirming they're meant to play in lockstep), and `shine_demo_shine_get_yo`. All of the procedural bob/spin/materials/dome-shader/eye-shader/hover-tween work described below no longer exists in the code — `ShineSprite.cs` was rewritten from scratch to just play the model's own animations. Kept the old write-up below for the debugging lessons (mesh-identification-by-size, the skin/scale gotchas, the Editable Children trap) since those are still generally true of this asset pipeline — just mentally file it under "history," not "current state." Current architecture is documented in **Pickup & Collect (current)** further down.

### Idle presentation (superseded)

**Files (historical):** `assets/ShineSprite.cs`, `assets/ShineDome.gdshader`, `shine_sprite.tscn`

Covers only how an uncollected Shine Sprite presents itself (bob/spin, glow, sparkle). Pickup detection, the collect cutscene (freeze Mario, hero camera, `ma_demo_shine_get`/`ma_demo_shine_get_yo` animation, HUD tie-in via `Mario.AddShine()`), and save persistence in `GameData.cs` are separate, not-yet-built follow-ups.

### Behavior

- Slow sine bob (`BobHeight`/`BobSpeed`) and constant Y spin (`SpinSpeed`), applied to the whole node in `_Process`.
- Materials are built procedurally at runtime — `ShineSprite.glb` has **no textures at all**, just one flat gray placeholder material (`theresonlyone`). The dome gets an unshaded/additive `ShineDome.gdshader` (radial falloff from local origin + a slow brightness pulse); everything else gets a metallic/emissive gold `StandardMaterial3D`.
- Sparkle drift via a runtime-built `CpuParticles3D` (soft-dot texture generated in code), same pattern as `GroundPoundEffects.cs`.

### Dome vs. star mesh identification

`ShineSprite.glb`'s three mesh nodes (`meshId0_name/1/2`) carry generic exporter placeholder names — there's no name to key off of. The dome is picked out by **largest AABB bounding-diagonal** (it dwarfs the star pieces: ~0.84m wide vs. ~0.22m and ~0.05m). A flatness-ratio heuristic was tried first and picked a small flat sliver piece of the star by mistake — absolute size, not proportions, is what actually distinguishes the dome here.

### Gotcha: broken skin bind data threw the dome far from the star (fixed at the source)

`ShineSprite.glb` shipped every part skinned to a 2-bone skeleton (`starglow`, `body`) for no reason this static prop needs — nothing ever poses those bones. The skin's bind data didn't match the bone node transforms (an export quirk in the same family as the vertex-color/white-texture BMD losses noted at the top of this doc), so at runtime the dome mesh rendered **far away from the star** instead of enveloping it, even though `GetAabb()` showed them centered together.

First pass fixed this at runtime (`mesh.Skin = null` in `_Ready()`), but that only takes effect when the game is actually playing — the editor's static scene view has no reason to run script logic, so placing a shine in a level always looked broken (huge gray blob) while the editor was open, and the real result only ever showed up in Play mode. **Superseded** by fixing the source data directly:

- The `skin` reference was stripped from all 3 mesh nodes in `models/ShineSprite/ShineSprite.glb` itself (direct edit of the glTF JSON chunk — see git history/scratch script if this ever needs redoing on a re-export), and the project's import cache was force-refreshed with `Godot.exe --headless --path . --import`. Each mesh now renders at its plain authored position with no skinning at all — correct in the editor **and** at runtime, no script involvement required.
- The runtime skin-strip code in `ShineSprite.cs` was removed as dead weight.
- Diagnosed originally via a temporary preview scene (`_ShinePreviewTemp.tscn/.cs`, deleted after use) that instanced `shine_sprite.tscn`, force-applied an unmistakable solid-cyan debug shader to the dome candidate, and screenshotted from several camera distances until the mismatch was visible.

### Editor WYSIWYG: `[Tool]` + baked scale

Two more pieces were needed so the editor viewport actually matches gameplay when placing shines in levels:

- **Scale** is baked directly into `shine_sprite.tscn`'s root `transform` instead of being applied via script. It's not an `[Export]` — every Shine Sprite must stay the same size, so there's deliberately no per-instance dial.

  Went through a bad detour: user-tuned "0.325 looks good" turned out to be calibrated against the stale, still-skinned editor view (see the Editable Children gotcha below) — that view was rendering measurably bigger than the true fixed asset, so 0.325 was actually only ~8% of Mario's height at runtime (confirmed: Mario's capsule collision height is `1.97665`, see `Mario2.tscn`). **Do not tune this by eyeballing the editor viewport** — it has been unreliable all session. Settled on **5.5** by rendering the shine next to a plain capsule mesh sized to Mario's exact known height in an isolated, guaranteed-fresh headless scene (no editor session involved) and comparing proportions directly — star reads as a solid, prominent object next to Mario-scale, dome forms a glowing pool a bit wider than he is tall. If this still needs adjustment, tune it from actual Play-mode gameplay next to Mario, not the editor viewport.
- **Materials** (gold star, glow dome, eyes) are applied in `_Ready()`, and `ShineSprite.cs` is now marked `[Tool]` so that code also runs in the editor. Idempotent (safe to re-run every scene load/reload) — but bob/spin (`_Process`) and the sparkle `CpuParticles3D` spawn are gated behind `if (Engine.IsEditorHint()) return;`, since motion is only meaningful at runtime and spawning a particle child node in the editor would get permanently baked into the scene the next time it's saved.

### ⚠️ Don't touch "Editable Children" on `shine_sprite.tscn` until the editor has reloaded the fixed import

Happened **twice** in one session. In-editor use of Editable Children turned `shine_sprite.tscn`'s root from a lightweight `instance=ExtResource(...)` reference into either a fully embedded copy (~140 lines, `ArrayMesh`/`Skin`/`Skeleton3D` baked in) or a set of per-child overrides pointing at `parent="shine/Skeleton3D"`. Both times this silently reintroduced the dome-detached-from-star bug and/or dropped the baked scale `transform` entirely — even though the `.glb` on disk and its `.godot/imported/` cache were (and still are, independently re-verified both times) correctly fixed.

**Root cause:** the *live, already-open* Godot editor process never reloaded `ShineSprite.glb` after it was patched — it's a long-running session, and reimporting on-disk doesn't retroactively refresh resources a running editor already has in memory. So the editor's in-memory scene tree for this instance still has the old `Skeleton3D`, and any Editable-Children-style interaction re-serializes that stale structure into the file, undoing the fix at the file level.

**Real fix (do this, not just re-patching the file again):** fully close and reopen the Godot editor/project so it loads `ShineSprite.glb` fresh from the corrected on-disk import. Re-patching `shine_sprite.tscn` without doing this just resets the symptom — the next Editable Children interaction in the same stale session will reintroduce it again.

**Going forward:** don't use Editable Children / manual `surface_material_override` entries on this node at all. `ShineSprite.cs` is `[Tool]` and re-applies every material automatically on load — manual overrides in the scene file are redundant and are what capture the stale structure in the first place. To customize appearance, use the script's `[Export]` fields (`GlowColor`, `StarColor`, `EyeColor`, `EyeSpotDirection`, etc.) or the root's `transform` for scale — both are safe, plain property edits that don't touch child node structure.

### Pickup & Collect (superseded — see "Pickup & Collect (current)" at the end of this section)

**Files:** `assets/ShineSprite.cs` (PickupZone + collect sequence), `assets/Mario.cs` (`TryCollectShine`/`StartShineGet`, `shineGet` state), `shine_sprite.tscn` (PickupZone Area3D)

- `shine_sprite.tscn` has a `PickupZone` Area3D child (`SphereShape3D`, local radius 0.18 — combined with the root's 5.5 scale, that's a ~1m world pickup radius). `ShineSprite.cs` wires `BodyEntered` to `OnPickupBodyEntered` in `_Ready()` (runtime-only, same guard as the rest of the gameplay logic).
- On contact with a `Mario` node, calls `Mario.TryCollectShine(this)` — mirrors the existing `TryStartFruitPickup()` pattern (public bool entry point, blocklist switch on `stateOfMario`, `CameraLocked` check first). Blocks re-entry during `shineGet`, `pickupRaise`/`carrying`/`putDown` (avoids conflicting with a held item), `hurt`, and `dead`. Deliberately does **not** block on diving/jumping/etc. — SMS doesn't gate shine pickup on Mario's current action, and the existing `CameraLocked` airborne-freeze logic (preserves momentum + gravity while input is locked) already handles a mid-air grab gracefully.
- `StartShineGet()`: zeroes XZ velocity, sets `CameraLocked = true`, transitions to the new `MarioState.shineGet`, calls `AddShine()` immediately (HUD ticks up right away — not keyed to an animation frame yet), and plays `ma_demo_shine_get` via `SetMarioState`'s new switch case (same `animTree.Active = false; animPlayer.Play(...)` pattern as `pickupRaise`/`putDown`). Both `ma_demo_shine_get` and `ma_demo_shine_get_yo` were already wired into Mario's `AnimationLibrary` in `Mario2.tscn` (not the AnimationTree state machine) — nothing needed adding there. Only `ma_demo_shine_get` is used for now; the `_yo` variant's exact intended use (gate/100-coin bonus flourish?) is still unconfirmed.
- `OnAnimationFinished` gets a new branch for `shineGet` + `"ma_demo_shine_get"`: clears `CameraLocked`, returns to `idle`/`ma_wait`, calls `_camera?.ExitShineGetShot()`, then `_activeShine?.FinishCollectSequence()` (see below) and clears the reference.
- **The shine doesn't just vanish on pickup** — first pass did `Visible = false` immediately, but that read as the shine disappearing instead of participating in the cutscene. Now: `TryCollectShine(ShineSprite shine)` stores the reference in `Mario._activeShine`; `ShineSprite.StartCollectSequence(mario)` tweens (`CreateTween`, Sine/Out) from its current position up to `mario.GlobalPosition + CollectHoverOffset` (default `(0, 2.2, 0)`) over `CollectRiseDuration` (0.6s), then just hovers there — bob stops (guarded by `_collected` in `_Process`) but spin continues, so it still reads as a glowing, lively object while Mario holds his pose. Mario is frozen for the whole sequence, so a one-time tween to a fixed point is enough — no continuous tracking needed.
- **Disappears in sync with the real animation end, not a guessed duration** — `Mario.OnAnimationFinished`'s shineGet branch calls `_activeShine.FinishCollectSequence()` exactly when `ma_demo_shine_get` actually completes. That method tweens `Scale` down to ~0 (Sine/In, `CollectFadeDuration` = 0.4s) then `QueueFree()`s — the node is genuinely freed now, not left sitting invisible forever (matters once persistence gets built).
- **Verified end-to-end** with a throwaway test scene instancing the real `Mario2.tscn` + `StartingPlatform.tscn` + `shine_sprite.tscn` together (not an isolated mockup) — confirmed via screenshot + direct property checks: shine visibly hovers above Mario mid-cutscene, `IsInstanceValid(shine)` correctly flips to `false` only after the full ~7s animation completes.
- No jingle/SFX yet (no audio asset available for it).

### Bug found + fixed while verifying the hover: headless frame count ≠ animation seconds

While diagnosing an earlier issue (below), frame-count-based test checkpoints repeatedly gave misleading "still not finished" readings. Root cause: this headless environment renders far faster than 60fps with nothing to vsync against, so `_Process` fires hundreds of times per real animation-second — a fixed frame budget silently assumed 60fps and undercounted by ~4x. Fix was procedural, not code: read `AnimationPlayer.CurrentAnimationPosition`/`IsPlaying()` directly instead of inferring elapsed time from a frame counter, in any future headless verification of a timed sequence in this project.

### Hero-shot camera (`SunshineCamera.cs`)

New `CamMode.ShineGet` — a fixed, dramatic low-angle shot during the get-cutscene, instead of just freezing wherever the camera happened to already be. Structurally a straight mirror of the existing `OverShoulder` mode: `EnterShineGetShot()`/`ExitShineGetShot()` sit right next to `EnterOverShoulder()`/`ExitOverShoulder()`, set the same `_tgtYaw/_tgtPitch/_tgtRadius` + `_modeTransitionTimer` fields, and get their own entries in `ChaseToTargets()`'s rate table and `UpdateFollow()`'s look-at-offset branch — no new subsystem, just another mode through the same polar-camera pipeline.

- **Trigger is a gameplay event, not input** — unlike OverShoulder (which polls a button press itself), `ShineGet` is entered/exited by explicit calls: `Mario.StartShineGet()` calls `_camera?.EnterShineGetShot()`, and the `OnAnimationFinished` completion branch calls `_camera?.ExitShineGetShot()`. Mario owns `CameraLocked` for this sequence entirely on its own (unlike OverShoulder, where the camera owns the lock via `SetMarioLocked`) — the camera's Enter/Exit here only ever touches its own `Mode`/framing, never `CameraLocked`. Deliberate split of ownership: Mario governs "is input locked", the camera governs "what does the shot look like".
- `_camera` is fetched via `GetNodeOrNull<SunshineCamera>("CamController")` in `Mario._Ready()` (the node keeps its legacy name "CamController" even though the script is `SunshineCamera.cs`).
- Framing: `ShineGetPitchDeg = 32` (positive = camera below, looking up — the dramatic hero angle), `ShineGetYawOffsetDeg = 155` (offset from Mario's current back-facing yaw, so the shot reads as a 3/4-angle face shot rather than dead-on or from-behind), `ShineGetRadius = 2.4`, look-at raised to `ShineGetTargetOffset = (0, 2.0, 0)`. All plain `[Export]`s, tunable without touching code. Verified by screenshot: close, low, face-on framing, clearly distinct from the normal behind-Mario follow view — confirmed the camera also sweeps cleanly back to `Normal` mode when the cutscene ends.
- **Not a static hold** — first pass set the yaw/pitch/radius targets once in `EnterShineGetShot()` and left them there for the whole ~7s cutscene, which read as flat/locked compared to SMS's actual shine-get shot (which slowly sweeps around Mario the entire time he's posing). Added `TickShineGet(dt)`, wired into the mode dispatch alongside the other `Tick*` methods, that continuously adds `ShineGetOrbitSpeed` (0.25 rad/sec) to `_tgtYaw` every frame — pitch/radius stay at their entry values, only yaw keeps advancing. Verified by reading `SpringArmPivot.GlobalRotation.Y` directly in a test (not the `SunshineCamera` root's own rotation, which never changes — the pivot child is what actually rotates) — confirmed ~0.83rad of sweep over the sampled window, matching the configured rate.

### Bug found + fixed while verifying the hero shot: `shineGet` got silently stomped by the landing-transition

The very first end-to-end test of the hero shot showed the animation **never actually playing** (`AnimationPlayer.IsPlaying()` false the entire time, camera stuck in `ShineGet` forever) despite every state flag looking correct. Root cause: `Mario._PhysicsProcess`'s `CameraLocked` block has a physics-driven "just landed" auto-transition —

```csharp
bool justLandedFromYCam = IsOnFloor() && wasAirPrev;
if (justLandedFromYCam && stateOfMario != MarioState.landing && ... other exclusions ...)
{
    stateOfMario = MarioState.landing;
    SetMarioState(MarioState.landing);
}
```

— and `shineGet` wasn't in that exclusion list (alongside `landing`/`idle`/the jump-landing states that already were). If Mario is even one physics frame short of fully settling onto the floor when the shine triggers `CameraLocked = true`, the very next tick's floor-landing detection silently overwrites `stateOfMario` back to `landing`, replaying a totally different animation and orphaning the camera in `ShineGet` forever (its exit path is gated on `stateOfMario == MarioState.shineGet`, which was no longer true). **Fixed** by adding `&& stateOfMario != MarioState.shineGet` to that condition. Caught only because the animation is ~7 seconds long and headless-rendered frames run far faster than 60/sec in this environment — worth remembering next time a fixed frame-count budget looks like it "isn't working" in a test: check real elapsed animation time before assuming a logic bug, but *also* don't stop there — this one turned out to be real. If any future one-shot cutscene-style state gets added to `MarioState`, it needs the same exclusion.

### Eyes (`meshId1_name`) (superseded)

Black base with a white highlight spot via `assets/ShineEye.gdshader` — same angle-from-normal "dot_spot" technique as `MushroomCap.gdshader` (no UV/texture data to work with, same as the rest of this model). Routed by exact node name (`mesh.Name == "meshId1_name"`) in `ApplyMaterials`, rather than the dome's size-based heuristic, since the user identified it directly.

`EyeSpotDirection`/`EyeSpotRadius` are `[Export]`ed rather than hardcoded — the eye mesh is tiny (~5cm raw) and its exact facing was impractical to nail from a script-driven headless screenshot (extreme close-ups kept getting occluded by the model's own dome glow and stray sparkle particles). Confirmed working (visible light-colored mark distinct from a black-forced diagnostic star), but exact highlight placement is best tuned live in the editor — `[Tool]` means changes preview immediately without pressing Play.

### Key Inspector Exports

| Field | Default | Effect |
|---|---|---|
| `BobHeight / BobSpeed` | 0.25 / 1.5 | Vertical bob (runtime-only) |
| `SpinSpeed` | 1.2 rad/s | Y-axis spin (runtime-only) |
| `StarColor` | warm gold | Emissive star body color |
| `GlowColor` | warm gold, alpha 0.65 | Dome tint/intensity |
| `EyeColor / EyeSpotColor` | near-black / white | Eye base and highlight color |
| `EyeSpotDirection / EyeSpotRadius` | (0, 0.6, 0.8) / 0.4 | Highlight placement on the eye mesh (mesh-local normal direction) — tune live in-editor if it's not sitting right |
| `DomeOuterRadiusOverride / DomeInnerRadiusOverride` | -1 (auto) | Manual override if the AABB-based auto radius looks wrong on a given instance |
| `SparkleAmount / SparkleRadius / SparkleColor` | 14 / 0.6 / cream | Sparkle particle tuning (note: Godot's `CpuParticles3D.Amount` rejects 0 — minimum is 1) |

---

## Shine Sprite — current architecture (post asset-swap)

**Files:** `assets/ShineSprite.cs` (rewritten from scratch), `assets/Mario.cs` (`TryCollectShine`, `shineGet` state — unchanged from before), `shine_sprite.tscn` (rewritten), `assets/SunshineCamera.cs` (hero-shot mode — unchanged from before)

The new `ShineSprite.glb` (real UV textures, correct 3-bone skin, and three baked animations: `shine_float`, `shine_demo_shine_get`, `shine_demo_shine_get_yo`) replaced the old placeholder entirely. This made almost all of the procedural work above obsolete — no more hand-rolled bob/spin, no dome/eye shaders, no AABB-based mesh identification, no hover-tween. `ShineSprite.cs` is now much smaller: it plays the model's own animations and handles pickup detection.

### What's inside the new glb

`Model → Armature → Skeleton3D (3 bones: shine/starglow/body) → Mesh_0 (MeshInstance3D, skinned, 3 primitives)`, plus a stray `fast64_f3d_material_library_PlaneObject` mesh (a Blender fast64-addon export artifact — a material-preview plane, not part of the actual model) and an `AnimationPlayer` sibling. Confirmed via a throwaway inspector scene that recursively printed the node tree and every animation's name/length/loop-mode — much faster than guessing from screenshots, and exactly the kind of check to redo first if this asset ever gets re-exported.

Per-primitive materials, keyed by name (all real, all textured — found via parsing the glTF JSON's `materials`/`primitives` arrays directly rather than guessing from renders):
- `_starglow1` — the glow/dome, `KHR_materials_unlit`, alpha BLEND.
- `_mat_shine_body` — the star body, 349 vertices (a real detailed mesh, unlike the old placeholder's simple shape).
- `_mat_shine_eyes` — 18 vertices, positioned within the body's bounds.

### Gotcha: the export kept textures but lost color (same disease, different symptom)

Both `ShineSprite_shine_0.png` (used by both `_starglow1` and `_mat_shine_body`) and `ShineSprite_shine_1.png` (`_mat_shine_eyes`) are **grayscale intensity maps** — soft radial gradients / silhouettes, no color. Checked all three primitives' attributes directly in the glTF JSON: none have a `COLOR_0` vertex-color attribute, and neither `_mat_shine_body` nor `_mat_shine_eyes` specifies a `baseColorFactor` (defaults to white). So the model renders white/silver out of the box — this is the same "BMD export loses vertex colors" issue noted at the top of this doc, just manifesting as "textures survived, but the color info that would have tinted them didn't" instead of "no textures at all."

**Fix, per user's choice ("gold tint via material"):** `ShineSprite.ApplyColorTint()` walks the mesh's surfaces, and for any material named `_starglow1` or `_mat_shine_body`, duplicates it and multiplies in a gold `AlbedoColor` (`GlowTint`/`BodyTint` exports) — keeping the real texture, alpha, and blend mode intact, just adding color back. `_mat_shine_eyes` is left untouched; its texture already reads correctly as a dark eye with a highlight. `[Tool]`-gated so it previews in the editor; idempotent, safe to rerun every load (unlike the old scale-multiply mistake from the first asset).

### Idle float + collect animation

- `_Ready()` plays `shine_float` looping. It ships with `LoopMode = None`, so the animation resource's `LoopMode` is set to `Linear` in code before `Play()` — otherwise it freezes on its last frame after 1.3s.
- On pickup (`PickupZone` Area3D → `Mario.TryCollectShine()`), it switches to `shine_demo_shine_get` (one-shot) and frees itself on that animation's own `AnimationFinished` signal.
- **Fully self-contained now** — no callback into/from Mario needed (the earlier hover-tween design needed `Mario._activeShine`/`FinishCollectSequence()`; that's been removed along with the tween code). Verified the two animations start in the same physics frame and stay in perfect lockstep throughout — `Mario.AnimationPlayer.CurrentAnimationPosition` and the shine's own matched exactly (0.04↔0.04 through 6.88↔6.88) at every sampled checkpoint in a full end-to-end test with the real `Mario2.tscn`.
- `ma_demo_shine_get_yo` / `shine_demo_shine_get_yo` are still unused — same open question as before about their intended trigger (gate/100-coin bonus?).

### Bug found + fixed: the shine didn't actually follow Mario

`shine_demo_shine_get` only animates the shine's *own* bones locally (spin/pulse in place around its own origin) — it never repositions the shine's root, since in the original game it was presumably reparented to Mario's hand for this cutscene rather than the animation itself carrying it there. Without that reparenting, the shine just played its animation wherever it happened to be sitting in the level, visibly disconnected from Mario. Confirmed by placing it at a distance in a test (simulating "jump into it from a few steps away") and watching `GlobalPosition` never move relative to the level.

**Fix:** `ShineSprite.PlayCollectSequence(Mario mario)` now reparents the shine onto the same hand socket held items already use (`Mario.GetNodeOrNull<Node3D>("Armature/Skeleton3D/RightHandBone/CarrySocket")`, same path `carrySocket` is resolved from in `Mario.cs`). Verified: `shine.GlobalPosition` matched the socket's position exactly (`distToSocket=0.000`) at every sampled frame through the whole ~7s animation, while both positions moved substantially (>1.6 units) as Mario's arm animates — genuine tracking, not a static snap.

Two things had to be handled carefully in the reparent itself:
- **Deferred, not immediate.** `OnPickupBodyEntered` runs from `Area3D.BodyEntered`, which fires during physics processing. `Reparent()` does a remove+add on a subtree that still contains a `CollisionObject3D` (the `PickupZone`) at that moment, which Godot logs as unsafe (`"Removing a CollisionObject node during a physics callback..."`). Deferred via `CallDeferred(MethodName.AttachToHandSocket, socket, Scale)` — the extra `Scale` argument is because of the next gotcha.
- **`Reparent(socket, keepGlobalTransform: true)` doesn't mean "move the shine to the socket."** It means the opposite — preserve the *current* global position across the parent change. Overwrote it explicitly afterward (`Reparent(socket, false)` + explicit `Position = HandSocketOffset`) to actually snap it onto the socket.

### Bug found + fixed: scale compounds when reparenting onto a bone socket — "size is still wrong"

After fixing the follow behavior, the shine became nearly invisible the instant it attached to Mario's hand. Root cause: the shine's `0.0036` root scale (see below) was calibrated assuming a scale-1 ancestor chain, matching how it sits in a level. Mario's hand socket, being deep in a bone-attachment chain, has its own small inherited scale (`~0.0134` in the test, likely from the same "rig authored in large units, corrected by scale" pattern this project has hit before on other skinned models) — and local scales compound multiplicatively across a reparent. `0.0036 × 0.0134 ≈ 4.8e-5`, i.e., a scale so small the shine was practically a single pixel. This is very likely the actual "size is still wrong" symptom, not the idle/pre-pickup scale — the shine looked fine sitting in the level; it only collapsed once picked up.

**Fix:** `AttachToHandSocket` captures the shine's world-scale-1 `Scale` *before* reparenting, then after reparenting divides that desired value by the socket's actual `GlobalTransform.Basis.Scale` component-wise, so the shine's final global scale lands back on the intended `0.0036` regardless of whatever scale the socket chain happens to carry. Verified directly: `shine.GlobalTransform.Basis.Scale` read exactly `(0.0036, 0.0036, 0.0036)` at every checkpoint post-fix, vs. `(4.8e-5, ...)` before it.

**If this ever needs revisiting:** any time a shine (or similar prop) gets reparented onto a Skeleton3D bone chain in this project, assume the destination has its own non-1 scale and compensate explicitly — don't trust `keepGlobalTransform` alone to produce a sane result, and don't trust the node's pre-existing local `Scale` to survive a reparent onto a differently-scaled parent unchanged.

### Scale + PickupZone radius — recalibrated, not reused

This asset uses a completely different unit convention than the old one (bone rest translation of ~71 units vs. the old model's ~0.036 root scale) — none of the old numbers carried over. Determined empirically the same way as before: rendered next to a plain capsule sized to Mario's exact known height (1.97665, from `Mario2.tscn`'s `CapsuleShape3D`) in an isolated scene, landed on **0.0036** as the root `transform` scale (combined object ≈ 2m span, comparable to Mario's height).

**Important:** the `PickupZone`'s `SphereShape3D` radius has to scale inversely with whatever the root transform scale is — it's a sibling `Area3D`, not baked into the mesh, so it doesn't automatically stay sized right when the asset's unit convention changes. At the old scale (5.5) a radius of `0.18` gave a ~1m world pickup zone; at this new scale (0.0036) the same `0.18` would be a **0.65mm** pickup zone — completely non-functional. Recalculated to `280.0` (280 × 0.0036 ≈ 1m). If the scale ever changes again, this needs to be recomputed too: `desired_world_radius / root_scale`.

### Cutscene now waits for Mario to land before starting — was firing mid-air

User feedback: touching a shine while airborne (jumping into it) should let Mario finish falling/landing normally, then play the whole cutscene — not freeze him out of the sky the instant he touches it.

- `Mario.TryCollectShine(ShineSprite shine)` now checks `IsOnFloor()`: if grounded, starts the cutscene immediately (`StartShineGet(shine)`) same as before; if airborne, stores the shine in a new `_pendingShine` field and returns `true` — the shine still disables its `PickupZone` right away (claimed, can't be double-collected) but does **not** start its own animation yet.
- `ShineSprite.OnPickupBodyEntered` no longer calls its own collect logic directly — `PlayCollectSequence` was renamed to public `BeginCollectSequence(Mario mario)`, now called *by Mario* once the cutscene actually starts (immediately, or after landing).
- A new check near the top of `Mario._PhysicsProcess` (right after the existing `CameraLocked` early-return) fires `StartShineGet(_pendingShine)` the instant `IsOnFloor()` becomes true while a shine is pending. Verified: touched a shine at `marioY=2.19` while still falling — `CameraLocked` stayed `false` and he kept falling completely normally all the way to the ground before the freeze/animation/camera kicked in.

**Bug found + fixed in the same pass:** the new landing-check didn't `return` after calling `StartShineGet()`, so the rest of that physics frame's normal state-machine kept running and immediately reprocessed/overwrote the `shineGet` state it had just set — symptom was `CameraLocked` getting stuck `true` forever even after the shine's own animation correctly finished and freed itself. This is the exact same bug class as the earlier "landing-transition silently stomps shineGet" fix, just recurring in a new spot for the same underlying reason (this state machine assumes any state-setting code path either handles the whole frame or returns immediately — half-measures get overwritten). Fixed with an explicit `return;` right after `StartShineGet(shine)`. **If another deferred-state-entry point ever gets added here, give it the same `return` treatment.**

### Scale corrected again — "much bigger" per user feedback

`0.0036` (the Mario-height-calibrated value) still read as too small in actual gameplay. Rather than guess a fourth time, asked the user directly for a direction + magnitude ("much bigger, roughly double or more") and applied that precisely: **`0.008`** (≈2.22×). `PickupZone`'s `SphereShape3D` radius recomputed to match (`126.0`, keeping the same ~1m world pickup radius: `1.008 / 0.008 ≈ 126`) — see the note above about this needing recalculation any time the root scale changes. Verified next to the Mario-height reference capsule again: combined span now roughly double Mario's height, matching the requested direction.

### Reference footage compared directly against the implementation — two real corrections

User provided an actual gameplay capture (298 frames extracted from a clip) of a real shine collection. Comparing it directly against what was built caught two things no amount of isolated guessing would have:

- **The early portion of that clip (frames ~1–140) is a level-specific reward sequence** — Mario asleep near a tree, then a warp into a starry void where he catches a small creature — almost certainly the Sleepy Boo secret challenge already in this project (`SleepyBoo.tscn`), not the generic shine-get. Deliberately **not** replicated; it's unique to that one encounter, not something every shine should do.
- **Frames ~140–220 are the universal "shine get" pose** and directly informed two fixes:
  1. **Camera pitch was backwards.** The real shot sits level with or slightly above Mario looking down/level at him — not the low up-angle "hero shot" originally guessed. `SunshineCamera.ShineGetPitchDeg` changed from `32` (camera below, looking up) to `-12` (camera above, looking down slightly — matches this project's existing pitch-sign convention, similar in spirit to Normal mode's `DefaultPitchDeg = -18`).
  2. **A "SHINE!" text popup was missing entirely.** The reference clearly shows colorful outlined letters bouncing in one after another, holding, then fading. New `Font/HudElements/ShineGetPopup.cs`/`.tscn`: six `Label` nodes ("S"/"H"/"I"/"N"/"E"/"!"), each a distinct color with a black outline, animated with the *exact* staggered wave-in technique already used by `ShineHud.ShowHud()` (`TweenInterval` delay per element + Back-ease position tween) — generalized to walk whatever `Label` children exist rather than hardcoding names. Unlike the persistent `ShineHud`/`RedCoinHud`/etc., this is spawned fresh per shine (mirroring `RedCoinPopup`'s dynamic-instantiate-then-free pattern) via a new `Mario.SpawnShineGetPopup()`, called from `StartShineGet()` alongside `AddShine()`. Verified by screenshot: colors and stagger read correctly, camera angle now looks natural rather than the exaggerated low angle.

### Camera orbit replaced with a hand-authored rail — orbit still didn't match reference

Even after the pitch-sign fix above, the user tested in real gameplay and reported the camera "literally does not do what it does" in the reference footage, and suggested a rail/waypoint system instead of a formula-driven orbit. Rather than tweak the orbit formula a third time, replaced `CamMode.ShineGet` entirely:

- `SunshineCamera._PhysicsProcess` now special-cases `ShineGet` with an early return straight into a new `TickShineGetRail(dt)`, bypassing the normal follow/chase/apply pipeline completely for the duration of the shot.
- `EnterShineGetShot()` computes three waypoints once, at the moment Mario grabs the shine: a side angle (`_railStart`), a swinging transition (`_railMid`), and a dead-on front shot (`_railEnd`) — all authored in Mario-local space and rotated into world space by his visual facing (`GetVisualYaw()`) at that instant. The camera is reparented off the `SpringArm3D` chain onto the `SunshineCamera` root for the duration (`_camera.Reparent(this, false)`) so the spring arm's own per-physics-step positioning can't fight a direct override.
- `TickShineGetRail` eases along `_railStart → _railMid → _railEnd` with a smoothstep over `ShineGetRailSweepDuration` (3s), then holds at `_railEnd` for the rest of the cutscene, always `LookAt`-ing a fixed point near Mario's chest/head (`_railLookTarget`).
- `ExitShineGetShot()` reparents the camera back onto `_springArm`, zeroes its local position/rotation, and resumes the normal "return to behind Mario" chase logic.

**Bug found + fixed during verification (the actual reason the old orbit "didn't match"-shaped issue recurred here too):** the first version of the rail waypoints used **positive local Z** for "in front of Mario," based on the intuitive assumption that a Node3D's -Z-forward convention meant +Z was backward-from-forward, i.e. "in front." That's backwards for *this specific rig*. Traced empirically (not just re-derived from Godot docs, since a comment-level assumption is exactly what caused the original pitch bug too): `GetBehindYaw()`'s own comment states the normal chase camera's `SpringLength` extends toward `-v` (opposite Mario's movement) to sit *behind* him, and a real headless test confirmed the normal-mode camera's final local position relative to `SpringArm3D` is `(0, 0, +9.27)` — i.e. **positive local Z is BEHIND Mario** in this rig's convention, not in front. The rail's `endLocal`/`midLocal` waypoints were built with `+ShineGetRailFrontDistance`, so the "front" hero shot was actually placing the camera *behind* Mario, clipped almost inside the back of his head (confirmed via an actual rendered screenshot — the fix in `assets/SunshineCamera.cs:685-687` negates the Z sign on all three waypoints. Re-verified with the same test: the endpoint shot now shows Mario's face dead-on, arm extended toward camera holding the shine, matching the intended hero-shot framing.

**Verification method used (important — screenshot capture needs real GPU rendering):** the first verification pass used `--headless`, which uses Godot's Dummy rendering driver — `GetViewport().GetTexture().GetImage()` returns a texture with a null RID under Dummy, so `SavePng` silently fails (`ERROR: Parameter "t" is null.`) even though all game logic and camera math run correctly. Position/parenting data logged to stdout was still valid and caught the reparent-to-`CamController`-and-back working correctly, but the visual framing bug was invisible until the exact same test scene was re-run **without** `--headless` (real windowed Vulkan/GPU rendering) — only then did the screenshots reveal the camera clipped into the back of Mario's head. **Lesson: log-based verification alone is not sufficient for camera framing bugs — a headless dummy-driver screenshot will "succeed" at the file-write level while producing no actual pixel data. Always drop `--headless` when the check is specifically about what something looks like.**

### Rail recalibrated against the actual reference frames, not just "closer is better" guessing

User pushed further after the direction-sign fix above: "I want it 1:1 with the screenshots I sent you... don't stop until it's just like Super Mario Sunshine." Re-opened the original 298-frame reference capture (`C:\Users\Jaysonn\Downloads\ezgif-267d728082501f64-jpg`) and looked directly at the universal shine-get portion (frames ~130-230) frame-by-frame instead of relying on the earlier high-level description. That close look overturned two assumptions the first rail implementation was built on:

- **The real camera barely travels.** It is NOT a slow 3-second side-to-front dolly ending in a wide shot. By the time the pose starts (~frame 132-148 of the capture) the camera is already a **tight close-up on Mario's head/chest**, and it stays there, close and mostly steady, for the whole pose + "SHINE!" beat. Almost all the apparent motion in the reference is Mario's OWN animation (hopping, raising the shine overhead, turning slightly), not the camera moving. Changed `ShineGetRailSweepDuration` from `3.0f` to `0.45f` — a quick snap-to-frame instead of a cinematic pan — and drastically closed the distance: `ShineGetRailFrontDistance` `2.2 → 0.78`, `ShineGetRailSideDistance` `1.8 → 0.9`.
- **The "SHINE!" text is a single diagonal banner, not individually-bouncing letters.** The reference shows one connected, slightly-rotated line of colored, outlined letters (ascending left-to-right) sliding/popping in as a unit with a few sparkling star accents around it — not each letter dropping from above on its own delay. Rebuilt `Font/HudElements/ShineGetPopup.tscn`: the six letter `Label`s now live inside a child `Banner` control rotated `-18°` (`rotation = -0.31416`), with three small `★` `Label`s scattered around it. Rewrote `ShineGetPopup.cs` to pop the whole `Banner` in as one unit (scale + position, Back-ease, ~0.22s) with the stars twinkling in with a slight stagger, instead of the old per-letter wave. Also corrected letter colors against the reference (I = yellow/gold, N = blue/cyan, E = orange — the first pass had I/N/E's colors rotated one slot off from the real game).

**Getting the height right took three iterations, not one — recorded here because the failure mode is a good general lesson for future close-up camera work:**
1. First pass (distance closed, but reused the old height/look-at values) put the camera under Mario's chin looking almost straight up his nostrils — because closing the distance without re-checking the height turns what was a *mild* angle at long range into a *steep* one at close range (same absolute height difference, much smaller horizontal distance ⇒ much larger `atan`).
2. Overcorrected the other way (raised both camera height and look-at height significantly) and ended up aiming *above Mario's actual head*, looking up into empty sky with only the top of his hat in frame.
3. Reverted the height/look-at values back to what was already working reasonably (`ShineGetRailHeight = 0.95`, `ShineGetLookAtOffset.Y = 1.05`) and changed **only** distance. That was the actual fix — dollying in on an already-correct angle just makes the same framing fill more of the screen; it doesn't require re-deriving the angle from scratch.

**A second, unrelated trap caught mid-verification:** the throwaway test scene's ground (`SecrectPlatform2` instanced with a hand-typed non-uniform `Transform3D(102.988, 0, 0, 0, 53.031, 0, 0, 0, 109.811, ...)` scale) was scaling its own `CollisionShape3D`'s local offset along with everything else, which pushed the actual standing surface to `y≈-0.78` while the visual mesh implied `y=0` — so Mario rendered "buried to the shoulders" in a screenshot that had nothing to do with the camera at all. Confirmed by logging `Mario.GlobalPosition` directly and noticing it didn't match the visible horizon. Fixed by throwing out the reused platform entirely and building the test ground from a plain `BoxShape3D`/`BoxMesh` pair with a known, sane top surface at `y=0` — much more trustworthy than trying to patch a hand-guessed scale on a complex imported asset. **Lesson: for any future camera/framing verification, prefer a deliberately simple test-ground primitive over reusing a scaled/imported platform scene — a wrong collision offset there is indistinguishable from a camera bug until you print the position and check.**

Final calibrated values: `ShineGetRailSweepDuration=0.45`, `ShineGetRailSideDistance=0.9`, `ShineGetRailSideForward=0.5`, `ShineGetRailFrontDistance=0.78`, `ShineGetRailHeight=0.95`, `ShineGetLookAtOffset=(0, 1.05, 0)`. Verified via the same real-GPU-rendering screenshot method (not `--headless`) — final framing is a tight front-on shot of Mario's head and upper torso with a slight up-tilt, and the "SHINE!" banner now sits beside his head/shoulder rather than floating disconnected above him.

**Still open / not verifiable from a synthetic test scene:** whether `ma_demo_shine_get`'s baked animation itself actually raises the shine overhead the way the reference shows (the throwaway test showed Mario in a neutral arms-out pose, not holding anything up — that's animation content/timing, not camera, and needs checking against the real animation in the real level rather than this isolated rig). Worth a real in-game look before calling this fully "1:1."

### Rail rebuilt on Path3D/PathFollow3D against actual VIDEO of the cutscene — the "static close-up" reading was wrong

User rejected the static close-up ("looks nothing like the reference photos") and provided a full ~10s @60fps video capture of the real cutscene (`C:\Users\Jaysonn\Downloads\ezgif-267d728082501f64-jpg\GMSE04_2026-07-21_21-11-35_0.avi`), plus an explicit request to use Godot's Path3D + PathFollow3D rail idiom. Frames were extracted with Python/OpenCV (no ffmpeg CLI on this machine — `cv2.VideoCapture` works fine) every 6th frame and read in sequence. **The video overturned the still-frame reading entirely:** individual JPEG stills made the celebration look like one held close-up; the video shows the camera is ACTUALLY in continuous motion:

1. On grab: CUT to a low shot in front of Mario (~chest height, close) — he reaches out and takes the shine. Held ~1.4s.
2. Then a continuous **crane-up** for the entire rest of the celebration (~3.8s): rising in front of him as he swings the shine and lifts it overhead, ending HIGH above looking down steeply (~40°), Mario small in frame with his shadow below him.
3. "SHINE!" letters: BIG (each ~15% of screen height), rushing in from the LEFT edge ~1.2s after the grab with a per-letter cascade, landing in an **ascending diagonal across the lower-left** (S lowest-left → ! highest, ending near screen center under Mario). Colors: S green, H pink, I yellow, N blue, E orange, ! pink. They fade out a couple seconds before the sequence ends (which closes with the classic iris wipe — not implemented, no scene-transition system yet).

**Implementation (assets/SunshineCamera.cs):** `CamMode.ShineGet` now lazily creates a `Path3D` ("ShineGetRail") + `PathFollow3D` ("ShineGetRailFollow", `RotationMode=None`, camera aims itself via LookAt) under the CamController. Each grab, `EnterShineGetShot()` pins the Path3D's GlobalTransform at Mario's feet rotated to his visual yaw and re-authors a 3-point `Curve3D` in that local space (`ShineGetCamStart/Mid/End`, -Z = in front — same empirically-verified sign convention as before), with gentle in/out handles on the mid point. The camera reparents onto the follower; `TickShineGetRail` drives `ProgressRatio` with a smoothstep of `(elapsed - ShineGetHoldLowDuration) / ShineGetRiseDuration`. Exit reparents back to the SpringArm as before. Tuned: hold 1.4s, rise 3.8s, start (0.1, 0.6, -1.5), mid (-0.2, 1.3, -2.0), end (0, 3.2, -2.6) rig-units.

**ShineGetPopup rebuilt:** letters now sit at hand-placed ascending-diagonal offsets in the lower-left (font 96, outline 18), slide in from 700px left with 0.05s per-letter stagger + Back ease (the video's motion-streak rush), 8 pale-yellow ★ labels scattered around pop in after, whole thing holds ~2.3s then fades. Colors corrected to the video's actual sequence (green/pink/yellow/blue/orange/pink).

**Test-harness gotcha (recurring, now confirmed in windowed mode too):** frame-count-based screenshot checkpoints were wrong AGAIN — the uncapped game window runs ~200fps `_Process` while the cutscene clock advances in 60Hz physics time, so "frame 700" was only ~3.5s into a ~7s sequence and the camera looked frozen at the low point. Fixed by switching the test to **time-based** checkpoints (`_PhysicsProcess` accumulating `delta`, shooting at fixed t values) — that immediately produced correct, interpretable data. **Any future cutscene verification should trigger on accumulated physics time, never on frame counts.**

Verified with real-GPU screenshots at t=1.2 (low grab shot ✓), 2.5 (rising, banner cascading in ✓), 3.5 (high, looking down, full banner + stars ✓), 5.5 (top hold, Mario small, high look-down ✓), 8.0 (Mode=Normal, camera back on SpringArm3D with clean local transform ✓).

**Open item from this pass:** in the synthetic test the shine sprite itself was hard to make out in Mario's hand at the later camera angles (a glow smudge tracks his hand at t=2.5-3.5, but at t=5.5 a glow sits near his feet instead of the raised shine the video shows at that beat). Could be animation-phase mismatch between `ma_demo_shine_get` and `shine_demo_shine_get`, or the hand-socket attach not tracking through the swing. Needs an in-game look before deciding whether anything is actually wrong.

### Behind-beat + swing added to the rail — user caught that the cutscene STARTS BEHIND Mario

User feedback on the crane-only rail: "the cutscene starts behind mario and then turns to the front. you can see this if you evaluate every frame." Went back through the video frame-by-frame in the f0120–f0240 range and confirmed: before the front-low grab shot there's an opening beat with the camera LOW BEHIND Mario's shoulder, aimed UP over his head at the sky (he's a corner element, bottom-right of frame, most of the frame is sky — in the original this is where he watches the shine fly down to him), and then the camera SWINGS AROUND HIS SIDE to the front, lowering its aim from the sky to his chest, landing in the front-low grab framing.

**Implementation:** the same Path3D curve now has five points — behind → side → front (the swing) → mid → end (the existing crane) — and `TickShineGetRail` runs four beats: hold-behind (1.2s) → swing (1.2s) → front-hold (0.5s) → crane (3.1s). `_railFrontRatio` (computed via `curve.GetClosestOffset(frontPoint)/GetBakedLength()`) marks where the front point sits on the curve so the swing sweeps [0.._railFrontRatio] and the crane sweeps the rest. The aim point is now animated too: `ShineGetLookAtHighOffset` (high, forward, offset to his LEFT so Mario lands in the bottom-RIGHT corner like the video) lerps down to the chest aim during the swing. Banner `AppearDelay` moved 1.1→3.5s since the front grab beat now lands at ~2.4s and the video shows letters ~1.2s after that.

**Framing lesson from verification:** the first behind-beat attempt centered the back of Mario's head filling half the frame — dead-centered aim. The video composition has him in the corner: fixed by offsetting the high aim laterally (aim moves left ⇒ subject slides right in frame), not by moving the camera. Verified all four beats via time-based screenshots (t=0.7 behind ✓ corner composition, t=1.8 mid-swing with Mario at right edge ✓, t=2.9 front-low grab ✓, t=4.5 rising with banner ✓, t=6-7 top hold ✓, t=9 clean Normal-mode restore ✓).

### Shine fly-down implemented — the behind beat now watches the shine descend, camera tracking it

User clarified the behind-the-shoulder opening: "this is what it sees when you land and the camera somewhat follows the shine sprite with these angles" — i.e., during that beat the shine is HIGH in the sky and the camera visibly tracks it as it descends to Mario. Two facts established before building:

- **The fly-down is NOT baked into shine_demo_shine_get.** Checked the glb directly (Python struct/json over the JSON chunk, accessor min/max — no buffer decoding needed): every translation track in all three shine animations is static. The original game moved the shine via level scripting, so ours scripts it too.
- **shine_demo_shine_get and ma_demo_shine_get are both exactly 6.96s** — a synchronized pair that must START TOGETHER. The descent occupies the animation's first ~2.4s (the shine spinning as it falls); the hand-socket attach belongs MID-animation at the grab moment, not at the start. (The old code attached at t=0 — that's why the shine was previously seen at hand-height during the "watching the sky" phase.)

**Implementation:**
- `ShineSprite.BeginCollectSequence(Mario, float visualYaw)` (signature grew a yaw param, passed by Mario from `SunshineCamera.CurrentVisualYaw` so the shine and rail share the exact facing convention): teleports the shine to `FlyDownStartOffset` (0, 6.5, −3) Mario-local (the camera cuts to the behind shot the same instant, so the teleport is never on screen), tweens `global_position` to `FlyDownGrabOffset` (0.15, 1.0, −0.55 — his reaching hand) over `FlyDownDuration` (2.4s, matching the camera's behind+swing beats exactly), THEN does the deferred hand-socket attach; the collect animation starts immediately, in sync with Mario's.
- `SunshineCamera.EnterShineGetShot(Node3D shineToTrack = null)`: during behind/swing beats the aim is the shine's live GlobalPosition (lerped toward the chest aim as the swing progresses — converges naturally since the shine lands at his hand right as the blend completes). `IsInstanceValid` guard + fixed-aim fallback since the shine frees itself at cutscene end. Mario's `StartShineGet` reordered: `BeginCollectSequence` BEFORE `EnterShineGetShot` so the camera's first LookAt sees the already-teleported shine.

**Verified** (time-based screenshots + logged positions): shine at y≈5.0 sky-high in the opening frame, camera aimed dead at it (speck centered); descends through the behind beat; camera swing catches Mario's profile mid-turn; shine reaches hand at t=2.4 exactly as the front grab shot lands; hand-tracked through the celebration; freed at end; camera restores cleanly.

**Known cosmetic gaps vs. the reference (documented for later, not camera/logic bugs):** (1) the radiating light-beam effect the real shine has during the descent doesn't exist in our version — in the reference those beams are most of what makes the distant shine read as big and bright; (2) in a BRIGHT test environment the shine reads as a dark speck during descent — the glow dome is a pale unshaded transparent material that washes out against light skies and the body is metallic=1 (near-black without env reflections). Against the real level's dark starry sky it should present much closer to the reference; check in-game before treating as a defect.

### Angled front shots + hand-socket attach REMOVED — the baked animation IS the star's choreography

Two user corrections this round: "the middle part is too in front of him. it needs to be at an angle again like in the ref video" and "fix the size of the shine sprite. its terribly tiny."

**Angle:** the reference never squares up dead-front — the grab and the whole celebration hold ~30° off Mario's facing axis on his left (continuing the swing direction; he reaches ACROSS with his right hand toward the camera-side shine, which is why f0270's grab reads so clearly). `ShineGetCamStart/Mid/End` now all carry a lateral −X component ((−0.8,0.6,−1.35) / (−0.95,1.35,−1.75) / (−1.15,3.2,−2.25)), and `ShineGetLookAtOffset` gained a −0.2 X bias (Mario right-of-center, banner owns lower-left). **Gotcha fixed in the same pass:** `_railLookLow` was built without rotating the offset by yaw — fine while the offset was pure-Y, silently wrong the moment it gained a lateral component. Now `.Rotated(Vector3.Up, yaw)`.

**Size:** root scale doubled 0.008 → **0.016**, `PickupZone` radius recomputed 126 → **63** (same ~1m world radius — see the standing rule about recomputing radius whenever scale changes). Body material also got gold **emission** (`emission_energy_multiplier 1.6`) — it was metallic=1 with a grayscale albedo, i.e. near-black in most lighting; the real shine pops on any background because it self-glows. `ApplyColorTint()` duplicates the active material and only touches AlbedoColor, so the emission survives the runtime tint.

**The big find — hand-socket attach was fighting the baked choreography and is now REMOVED entirely.** Diagnostic logging (position + `GlobalTransform.Basis.Scale` + parent at 8 time points) showed the shine root correctly hand-attached at 0.016 scale… while the STAR was invisible in renders. Root cause: the star geometry hangs off a bone with a ~71-raw-unit rest offset (≈1.1m at 0.016) and `shine_demo_shine_get` ROTATES that bone around the root — the animation IS the star's full routine (reach-height hover, low swing, overhead lift), authored as the synced pair of `ma_demo_shine_get` **with the root pinned at Mario's feet**. Attaching the root to his MOVING HAND double-applied motion and swept the star up to a meter away from the hand — at the final pose it ended up under the floor, which is why it "vanished." Fix: `BeginCollectSequence` now descends the root to **Mario's origin** (`FlyDownGrabOffset = (0,0,0)`) and pins it there, sets the root's yaw to Mario's facing + `CutsceneYawOffsetDeg` (export, default 180 — flip if the routine ever reads mirrored), and the baked animation does the rest. `AttachToHandSocket`/`HandSocketPath`/`HandSocketOffset` deleted. Verified: star at his reaching hand at the grab beat, swinging up beside his head for the pose — matches f0270→f0390.

**Remaining cosmetic gap:** our glow dome renders as a big soft blob around the star, where the game's has a tighter core plus distinct radiating RAY beams. That's material/FX work (the long-standing "dome vs. star" question below), not choreography.

### Mario turns to face the camera + dome removed + star made opaque

Three user corrections: "mario needs to look at the camera with the pose", "get rid of the dome of light", "make it so the shine sprite is not see through at all".

- **The face-the-camera turn:** the reference has Mario watch the descent with his back to the camera, then TURN during the grab so the entire celebration plays looking straight into the lens (that's why the pose previously read "off" — he held his original facing while the camera sat 30° to the side). Implemented in `Mario.StartShineGet`: a tween (1.2s delay + 1.0s duration, matching the camera's swing beat) rotates `armature.rotation.y` by `SunshineCamera.ShineGetFrontYawOffset` — a new camera property computed from the rail's front point (`Atan2(-ShineGetCamStart.X, -ShineGetCamStart.Z)`), so retuning the rail automatically keeps the turn aligned. Armature-delta rotation is flip-agnostic (works regardless of ArmatureForwardIsFlipped). Verified: at the pose beats Mario looks dead into the camera.
- **Dome gone:** the `_starglow1` surface override in `shine_sprite.tscn` is now a fully transparent material (renamed `_starglow_hidden`, alpha 0) — and `ShineSprite.ApplyColorTint()` no longer tints the glow material (that would have set its albedo alpha back to 1 and resurrected it; the tint switch now only handles `_mat_shine_body`).
- **Opaque star:** removed `transparency = 4` from the body material — solid gold star, no see-through. Emission kept but toned 1.6 → **0.9** so the texture's shading still reads (at 1.6 the star rendered as a flat yellow silhouette; the reference has visible depth).
- Size ratio verified against the video: star ≈ half Mario's height at the pose — 0.016 root scale is correct on ratio (the earlier "tiny" reading was the dark/washed-out material, not the scale).
- **Note:** `shine_sprite.tscn` carries user-set instance overrides (`Mesh_0` −48.2 Y offset recentering the star toward the root; `FlyDownStartOffset`/`FlyDownGrabOffset`) — honored as-is; script defaults differ but the instance wins. At the grab beat the star sits slightly camera-ward of Mario and reads a bit large from perspective — if that needs tightening, `FlyDownGrabOffset`'s Z (−0.5 → closer to 0) is the knob.

### Camera raised + render-rate smoothing + Delfino font + stable-star rework

User: camera too low, cutscene "rough" (not smooth), size, and "use the proper text in our font folder."

- **Smoothness = run the rail at render rate.** The ShineGet rail was ticked from `_PhysicsProcess` (60Hz); a hand-authored camera pan stepped at 60Hz reads as stutter on a high-refresh display. Moved `TickShineGetRail` to `_Process` (render rate). Nothing in the rail is physics-coupled — Mario is frozen and the descent/turn tweens are idle-processed — so everything now advances on one clock. This was the actual "rough" fix.
- **Camera height:** raised all the low-beat rail points to head level (`ShineGetCamBehind/Side/Start/Mid` Y bumped ~0.3–0.35) and the chest aim (`ShineGetLookAtOffset.Y` 1.05→1.15). Grab-beat camera Y went 0.9→1.45. No longer floor-hugging.
- **Font:** the "SHINE!" letters now use `res://Font/Delfino.ttf` (the game's actual UI font, already used by marioHUD) via `theme_override_fonts/font` on each letter — the bubbly game look instead of the engine default.
- **Star position/size — reworked the attach model (again), landed on free-node + shine_float.** History of what failed and why, so this doesn't get re-litigated:
  - Baked `shine_demo_shine_get` swings the star around its bone in a wide arc that drifts it away from Mario over the pose (star ends up floating up-left, detached).
  - Hand-socket parenting (`RightHandBone/CarrySocket`) makes the star VANISH — the star geometry sits ~1.1m off the node via bone rest, and that offset compounds with the socket's own tiny inherited scale into an off-screen/degenerate transform. Fragile; abandoned.
  - **Winner:** keep the shine a free world node, play **`shine_float`** (gentle in-place spin — its translation tracks are static per the glb, so the star holds a STABLE position instead of swinging), and place it purely via `FlyDownGrabOffset`. The star sits at a fixed offset above the node, so one offset value parks it beside Mario's head for the whole hold. `FlyDownGrabOffset.Y` tuned 1.0→0.35 to bring it down to just-above-head (was floating too high). Mario tells the shine to free itself when `ma_demo_shine_get` ends (`OnCollectSequenceEnd`) since the looping float can't self-signal.
  - **Emission toned 0.9→0.3** — at 0.9 the star bloomed into a featureless orange orb; at 0.3 the star shape (spikes + face) reads while still self-lighting against dark skies.
  - Size now reads ~head-to-1.5×-head at the pose, matching the reference ratio.

**Accepted deviations (diminishing returns, flagged honestly):** the star floats at a fixed spot above his head rather than being physically "caught" at his hand then raised (the real game parents it to the hand; our bone-offset + scale math makes that attach too fragile to be worth it). During the crane the star reads large/soft up close on the low camera beats. The hero-pose beat (where the "SHINE!" banner peaks) is the close match.

### Big pass to reach the reference finish pose: opaque star, baked animation, bottom-center text

User was at "~50%": animation wrong, shine see-through, scale off, text bad. Provided a finish-pose reference image (Mario reaching up to a big face-forward shine, "SHINE!" bottom-center). Fixes:

- **See-through → opaque.** The body material was `transparency = 4` (ALPHA_DEPTH_PRE_PASS — routes through the transparent pipeline and reads see-through with emission). Set `transparency = 0` (DISABLED). Also dropped the soft-circle albedo texture (`ShineSprite_shine_0.png` is just a radial gradient — it was making the star a fuzzy blob) in favor of solid gold + `metallic 0.85` / `roughness 0.32` / moderate emission. Now a crisp solid gold star with its face + ball-tipped rays.
- **Animation:** per user ("use the animation the shine sprite has"), the cutscene plays the baked **`shine_demo_shine_get`** again (not a manual billboard/bob I'd tried, and not `shine_float` which tumbles the star edge-on). The baked routine settles the star FACE-FORWARD (eyes toward camera) at the end = the finish pose. Mario still signals the end (`OnCollectSequenceEnd`) since it's looped.
- **Scale:** face-on, the star is much bigger than it read tumbled/edge-on, so earlier "head-sized" values ballooned. Landed at root scale **0.0127** (pickup radius 79) — star ≈ 1.5–2× Mario's head at the pose, matching the reference "big" shine. `FlyDownGrabOffset` = (-0.4, 0.75, -0.5) parks it up-left beside his raised hand.
- **Text — bottom-center, not left-diagonal.** The reference "SHINE!" sits along the bottom, near-horizontal with a gentle per-letter bounce (not the steep ascending diagonal I had). Rebuilt `ShineGetPopup.tscn`: letters at the lower-center in a shallow bounce, Delfino font, `outline_size 28`, per-letter rotation (±0.06–0.12 rad), reference colors (S green, H pink, I yellow, N blue, E orange, ! pink). Reads much closer to the game's bubbly outlined text.
- No pre-made "SHINE!" sprite exists in the repo — the `sms_alphabet_textures/*.bti` are original-game format and don't even include S/H/I/N/E; the "proper font" is Delfino, now styled to match.

Verified finish-pose frames (t=4.5–6.2) against the reference: big face-forward shine up-left, Mario reaching toward it, bouncy bottom-center SHINE!. Close match.

### Known cosmetic open question: dome vs. star framing

The dome (`_starglow1`, only 31 vertices — a simple bulging shape) seems to visually dominate/can occlude the star from some viewing angles, based on isolated test renders — the star was hard to see clearly except when the glow was made fully transparent for a diagnostic shot. Whether this matches the real game's actual look (the original reference screenshot the user shared early on also showed the dome as the dominant visual element with the star only partly visible) or needs further adjustment (e.g., a smaller/differently-shaped glow) is an open aesthetic question, not a functional bug — everything (scale, color, animation sync, pickup) works correctly regardless of this. Worth a look in actual gameplay framing/lighting before deciding anything needs to change.

---

## Red Coin Switch, Sign Popup & TalkingPOV Camera

**Files:** `assets/RedCoinSwitch.cs`, `red_coin_switch.tscn`, `Font/HudElements/SignPopup.cs` + `.tscn`, `Font/HudElements/SignPopupLabelSettings.tres`, `assets/SunshineCamera.cs` (new `CamMode.TalkingPOV`), `assets/Mario.cs` (`ForceStandingIdle()`), `assets/secrect_level.tscn` (wiring).

SMS-style red-coin challenge: ground-pound a switch → a rule sign pops up → pressing A starts an 8-coin countdown → collecting all 8 spawns a reward Shine with a reveal camera.

### Sequencing (important — timer does NOT start on ground pound)

`RedCoinSwitch.Trigger()` (fires from `HitZone` Area3D on ground-pound contact, latch-guarded the same way `Nail.cs` is) only plays the switch's baked `redcoinswitch` squash animation and shows the sign. The actual challenge (`BeginRedCoinChallenge`: `SpawnRedCoins()` + `_shineTimer.StartTimer()`) only fires from `OnSignClosed`, which itself only proceeds if `_signAcknowledged` was set by `OnSignDismissed` (hooked to `SignPopup.Dismissed`, emitted the instant button_a is pressed — **not** on a timer, `HoldDuration` is forced to 0 for this flow so the sign never auto-dismisses). This two-signal chain (`Dismissed` → camera exits + flag set; `TreeExited` after the fade-out → challenge actually begins) exists so the countdown can't start while the player is still reading, or mid-camera-glide.

Coins are spawned from `Marker3D` children under `RedCoinSpawnsPath` (currently 8, matching `RequiredRedCoins`) via `RedCoinScene`; each hooks `RedCoin.Collected` back to `OnGroupRedCoinCollected`, which stops the timer and spawns `RedCoinShineScene` (deferred, since collection reports from an `Area3D.BodyEntered` callback) once the count is met. The reward reveal uses a **throwaway `Camera3D`** (`PlayRedCoinShineRevealCamera`): grabs whatever `GetViewport().GetCamera3D()` currently is, tweens to a framing shot of the new Shine and back, then `MakeCurrent()`s the original camera back and frees itself — same "borrow ownership, hand it back" pattern as the level intro cam, not a `SunshineCamera` mode.

### TalkingPOV camera mode

Added `CamMode.TalkingPOV` to `SunshineCamera.cs` for "camera swings in to watch Mario read something" moments — `RedCoinSwitch` calls `_camera.EnterTalkingPOV()` right as the sign appears and `_camera.ExitTalkingPOV()` on dismissal. **User-corrected twice this round, both worth remembering:**

1. First pass used a fixed ~100° side-profile yaw offset + a wide-ish radius (2.2–3.2). User: "should be like the Y cam... over his shoulderish." Reworked to literally mirror `OverShoulder`'s mechanics instead of inventing new ones — `TalkingPOVLocalOffset` (Mario-local, same shape as `OverShoulderLocalOffset`), `_tgtYaw = GetVisualYaw()` (not offset), tight `TalkingPOVRadius` (1.4, matching `OverShoulderRadius`). Same `_modeTransitionTimer`-driven snap-rate glide as OverShoulder's entry/exit — that machinery was already mode-agnostic, just needed a `case CamMode.TalkingPOV: break;` added to the dispatch switch (targets set once on entry, held).
2. **`SpringArm3D`'s collision avoidance is real and will silently shorten your requested radius** — `arm.spring_length` (what you set) and `arm.get_hit_length()` (actual runtime distance) can diverge a lot in tight spots; this switch's nook (right next to a palm tree) settled at ~1.7–1.8 actual regardless of what radius was requested up to ~5. Manual `PhysicsShapeQueryParameters3D.intersect_shape` calls to find the exact blocking collider came up empty even when `get_hit_length()` clearly showed a hit — never fully diagnosed why (a static overlap test doesn't seem to reliably replicate whatever swept-motion test SpringArm3D uses internally); the pragmatic fix was tuning the default radius down to match what the collision system was already settling on, not fighting it.
3. **CameraLocked freezes velocity, not pose.** `EnterTalkingPOV` now also calls `Mario.ForceStandingIdle()` (new public method — sets `stateOfMario = idle` and calls the private `SetMarioState(idle)`, which does `_sm.Travel("ma_wait")` for a smooth blend). Without this, locking Mario right after a ground-pound landing left him visibly stuck in the transient impact-crouch pose for the whole conversation — Y-cam doesn't normally hit this because it's usually toggled while Mario's already standing.

### Sign Popup — line-locked text + transparency + sizing

`SignpopTextBox.png` (982×747) has **7 faint ruled horizontal lines baked in** at y-fractions `[0.1124, 0.2423, 0.3722, 0.5020, 0.6332, 0.7631, 0.8916]` of its height (measured by decoding the PNG's raw scanlines and diffing against background color — visually easy to miss, bit this project once already). `SignPopup.tscn`'s `Banner` now has 7 child `Label`s (`Line1..Line7`) each pinned to one ruled line via `offset_top/bottom` + `vertical_alignment=Bottom`, sharing one `SignPopupLabelSettings.tres`. `SignPopup.cs` splits `Text` on `\n` and drops each segment onto the next line — a blank segment (the gap before "GOOD LUCK!") just leaves that line empty. Generalizes to any sign up to 7 lines.

**Sizing gotcha:** project uses `stretch/mode=canvas_items` with the **default 1152×648 base viewport** (no explicit `viewport_width/height` override in project.godot) stretched to the real window (~1930×1086, ≈1.68× factor). A `Control` sized in what feels like reasonable screen pixels (e.g. 700×530) is actually ~61%×82% of the base canvas and renders enormous — this is what happened first pass ("text sign is too big in general"). Final size: Banner 500×380 canvas-units (anchored center, `pivot_offset` = center for the tilt rotation to work right), font 24px (down from 34), outline 4 (down from 6), side insets 50px. Banner `modulate.a = 0.87` for the see-through look from the reference.

**`SignPopup.Dismissed` signal** — fires the instant button_a is pressed (before the fade-out tween), specifically so callers (`RedCoinSwitch`) can react to "acknowledged" without waiting on the cosmetic fade. Also added an `AcknowledgePrompt` pulsing-dot UI element (own `_Process` loop, `PromptPulseHz`/`PromptMinScale` exports) to signal "press A" — visible whenever `DismissOnButtonA` is true, alpha-matched to the banner.

### Godot-AI MCP testing gotchas (reusable beyond this feature)

- **`Input.action_press()`/`action_release()` do NOT dispatch real `InputEvent`s** — they only change what `Input.is_action_pressed()`/polling returns. Code that polls (`Input.IsActionJustPressed(...)` in `_PhysicsProcess`, e.g. Mario's ground pound) works fine with it; code that relies on `_UnhandledInput(InputEvent)` (e.g. `SignPopup`'s button_a dismiss) never sees it. For the latter, synthesize and dispatch for real: `var ev = InputEventAction.new(); ev.action = "button_a"; ev.pressed = true; Input.parse_input_event(ev)`.
- **`game_eval`'s GDScript is whitespace-sensitive in a way that's easy to trip** — any indented block (`if`, `for`, `while`) reliably threw `EVAL_COMPILE_ERROR: Mixed use of tabs and spaces`, even with consistent spaces on my end, and a failed parse **leaves the game parked in a debugger break** (subsequent evals then fail with a stale/misleading error until you `project_manage(op="stop")` and relaunch). Reliable workaround: flatten everything to single-line ternary expressions (`x.set(...) if cond else null` — but note a `void`-returning call inside a ternary also errors; assign to a throwaway var first, or just skip the null-guard when you already know the target exists) and avoid `for` loops (index/slice instead, or just don't collect multiple results in one call).
- **`project_run(mode="current"|"main")` is idempotent when the game is already running** — `was_already_running: true`, no rebuild, no relaunch. If you just edited a `.cs` file and need the change live, you **must** `project_manage(op="stop")` first, *then* `project_run` again — otherwise you'll test stale code and get confusing results (burned a full diagnostic detour on exactly this once this session).
- **`editor_screenshot(source="game")` can silently return a stale/frozen frame** (`stale_frame: true`, "window appears backgrounded") if the game window isn't OS-focused, which it usually isn't in this headless-ish workflow — don't trust a screenshot that doesn't visually match what you just did; check `stale_frame` and retry, or better, verify state via `game_eval` reads (signals fired, mode enums, `IsRunning` flags) which aren't affected by window focus.
- For anything gameplay-adjacent (camera framing, physics-driven positioning), **verify with an actual screenshot, not just the math** — this file's own ShineGet section already learned this lesson once; TalkingPOV's aim-height bug (aiming at ~94% of Mario's height instead of chest-center, cropping his whole lower body out at close range) and the SpringArm collision-shortening were both only caught by looking at renders, not by reasoning about the polar-camera formulas.

---

## Air Control Tuning (Dive / Rollout)

**File:** `assets/Mario.cs` — `AIR_DIVE` and `AIR_ROLLOUT` (`AirControlProfile` instances)

`AirControlProfile.accel` is "how fast we steer toward desired velocity" while airborne — the knob that makes a dive or rollout feel like it can actually be curved mid-air versus just ballistically committed once launched. Bumped twice per playtest feedback, same multiplier applied to both moves each time so they stay matched:

| Move | Base accel | 1st pass (1.5×) | 2nd pass (2×, current) |
|---|---:|---:|---:|
| `AIR_DIVE` | 4.442643 | 6.663965 | **8.885286** |
| `AIR_ROLLOUT` | 8.885286 | 13.327929 | **17.770572** |

Other fields on the same profiles (`maxSpeed`, `brake`, `drag`, `reverseMax`, `stopSnap`) were untouched — only steering responsiveness changed, not top speed or how hard braking against your own momentum hits.

---

## Rico4 Secret Course — Background Glow Shader

**Files:** `Skyboxes/SecretCourseGlow/secret_course_glow_v9.gdshader` + `SecretCourseGlow_v9.tres`, applied as `material_override` on `Levels/Rico4Secrect/Skybox/rico_4_secret_skybox.tscn`'s `Rico4SecretSkybox/Armature/Skeleton3D/Mesh_0`.

Recreation of the classic SMS secret-course background: a dark void covered in a grid of glowing dots, with small clusters of Mario's 8-bit jump sprite scattered across it, slowly climbing, periodically cutting between a "normal" (red/yellow, big) look and a "green" (small) look.

### What "Rico 4" is

Episode 4 of Ricco Harbor: **"The Secret of Ricco Tower"** — the harbor's one secret course (rotating/gear platforms, reused later as Twisty Trials Galaxy in SMG2). Its replay mode is an 8-red-coins-in-90-seconds challenge — i.e. the exact same mechanic as the `RedCoinSwitch` system documented above. This background is the standard SMS secret-course backdrop, not unique to Ricco Tower specifically.

### Source assets

The glow/sprite textures didn't survive this project's usual Blender BMD→GLB export pipeline (same class of loss as the vertex-color/white-texture issue noted elsewhere in this doc). Recovered from a separate extraction tool output and copied in:

- `Skyboxes/SecretCourseGlow/B_crasicmario.png` (64×64, RGB) — the tiled Mario sprite. Blue field = "background" pixels, red/yellow pixels = the actual sprite.
- `Skyboxes/SecretCourseGlow/P_casino_glow2mm_level0.png` (128×128, gray+alpha) — the soft dot-grid glow pattern, baked as a 4×4 array of dots per tile.
- Source path (for re-extraction if ever needed): `C:\Users\Jaysonn\Documents\FinModelUtility-main\cli\out\super_mario_sunshine\data\scene\coro_ex2\map\map\sky\`. That folder also had the original TEV fragment shader recompiled to GLSL (`_VRsph.fragment.glsl`) — decoding it (`color = clamp(marioSprite.rgb * colorRegister * glowDots.rgb, 0, 1)`, both textures sampled through independent per-texture 2D transforms) is what the whole shader below is a reimplementation of.

### Architecture (v9)

- **Coordinate space:** does NOT use the mesh's own UV — uses a longitude/latitude remap computed from the surface normal instead (`atan(n.x,n.z)/TAU+0.5`, `asin(n.y)/PI+0.5`). See gotcha below for why.
- **Two-layer composite, not a plain multiply:** each `mario_tex` sample is classified as "background" (blue-dominant) or "character" (red/yellow-dominant) via `step(r+g, b)`. Background pixels always render as a fixed `bg_color`, completely ignoring the phase tint; only character pixels get `tint * sample`. This is what keeps the grid a clean, consistent blue in both phases instead of the tint discoloring everything including the dot grid.
- **Cell/fill split for independent size + spacing:** the sprite doesn't just tile at native size — each "cell" (pitch set by `mario_uv_scale_a/b`) samples the sprite only within a shrunk `mario_sprite_size` fraction of that cell, with everything outside that fraction treated as background. Lets size and spacing be tuned independently instead of being locked together by one tiling frequency.
- **Motion is discrete hops, not continuous scroll** — `floor(time_in_phase / hop_interval_seconds)` steps a fixed distance per interval, matching the reference video's "hop up a dot every ~15 frames" look rather than a smooth drift.
- **Phase switch is a hard cut** — `t` is exactly `0.0` or `1.0` (a step function on elapsed time-in-phase), never blended. Confirmed against frame-by-frame reference video: consecutive frames go from nothing to a full-size block with zero fade.
- **Hop offset resets to zero at the start of every phase** (`time_in_phase`, not raw global `TIME`) — since cell density changes at every phase cut, carrying an accumulated offset into a differently-sized grid put cells at an arbitrary alignment and occasionally clipped a sprite at a cell boundary. Resetting means every phase starts from the same clean alignment every cycle.
- **Background dot grid (`glow_tex`) doesn't scroll at all** — confirmed directly against the reference: only the Mario layer moves.

### Tunable parameters

All live on the `SecretCourseGlow_v9.tres` material — select the skybox mesh (`Rico4SecretSkybox/Armature/Skeleton3D/Mesh_0` inside `rico_4_secret_skybox.tscn`) and open its Material Override in the Inspector.

| Parameter | Current | Effect |
|---|---:|---|
| `mario_uv_scale_a` | (1.265, 1.265) | Cell spacing/density, "normal" phase. Lower = fewer, bigger, farther-apart cells. |
| `mario_uv_scale_b` | (4, 4) | Same, "green" phase. |
| `mario_sprite_size` | 0.85 | Fraction (0–1) of each cell the sprite actually fills. `1.0` = touches its neighbors, no gap. Lower = shrinks the sprite and opens a margin — independent of cell spacing above. |
| `hop_direction` | (0.6, 1.0) | Diagonal climb direction. Only the X:Y ratio matters (normalized in-shader). |
| `hop_interval_seconds` | 0.5 | Time between hops. `0.5` = 15 frames @ 30fps. Lower = faster hopping. |
| `hop_distance_dots` | 4.025 | How many glow-dot-cells each hop covers. |
| `glow_uv_scale` | (16, 16) | Density of the background dot grid itself. Higher = more, smaller dots. |
| `bg_color` | (0.15, 0.35, 0.95) | Fixed blue for background dots — ALWAYS this color, untouched by the phase tint. |
| `color_a` | white (1,1,1) | Tint during the "normal" phase — white shows the sprite's own red/yellow coloring unmodified. |
| `color_b` | (0.3, 1, 0.45) | Tint during the "green" phase. |
| `hold_a_seconds` | 4.3 | How long the "normal" phase holds before cutting to green. |
| `hold_b_seconds` | 4.3 | How long the "green" phase holds before cutting back. |

### Gotchas hit building this

- **Editing an already-loaded `.gdshader` file's source text in place does NOT hot-reload** — the `ShaderMaterial` keeps using stale default uniform values even after `filesystem_manage.scan()`. Workaround used throughout: any *structural* shader change (new logic, new uniforms) got a brand-new filename (`_v2`, `_v3`, ... `_v9`) to guarantee a fresh, uncached load. Pure *value* tweaks on an already-working shader (no code change) DO apply fine via `material_manage.set_shader_param` without needing a new file.
- **`material_override` set on a node that's a nested child of an *instanced* sub-scene doesn't reliably persist** through the outer level scene — same issue hit earlier with `SunshineCamera` export values. Fix used every time: open the skybox's OWN scene file (`rico_4_secret_skybox.tscn`) directly and set/save the material there, then `force_reload` the level scene that instances it. Setting it through `Rico4Secret.tscn` directly silently didn't save.
- **The skybox mesh's raw import scale was enormous** — even at an initial `0.19` Node3D scale, its AABB was still ~5864 units across (raw mesh ~30,000+ units), which sat right at/past the gameplay camera's `far=4000` clip plane and rendered solid black. Rescaled down to **0.03**, which brought the AABB to a sane ~925 units. Lesson for any future large-scale imported GLB: check `aabb_size` after placing it, don't assume a "small-looking" scale number is actually small.
- **Switching from raw mesh UV to normal-based coordinates changes what the same scale number visually means.** A value tuned to look right under the mesh's raw UV (e.g. cell scale `1.265`) can render as completely invisible (tile so huge the visible camera window samples only its background pixels) once the coordinate space changes — had to re-find equivalent values empirically after the seam fix rather than assume the old numbers still applied.
- **`NORMAL` is VIEW-SPACE by default in a spatial shader's `fragment()`** — using it directly for a coordinate meant to stay fixed in world/object space made the whole pattern swim/rotate with the camera. Fixed by capturing `NORMAL` in `vertex()` (object-space there) into a `varying` and using that in `fragment()` instead.
- **Any spherical-to-planar coordinate mapping has an inherent seam** (here: the ±180° longitude wrap and the pole singularities) — switching to normal-based coordinates fixed the *distortion* problem (mesh's own non-uniform raw UV) but doesn't eliminate seams as a concept. If a hard edge shows up again at very low cell counts, this is the next place to look.
- Same MCP testing gotchas as the TalkingPOV section apply here too (`project_manage(op="stop")` before re-testing a code change, `stale_frame` on `editor_screenshot`, prefer `game_eval` reads over screenshots for confirming state) — this shader's iteration loop leaned on all of them repeatedly.

---

## Multi-Character System

Seven playable characters share one base scene. Inspired by Super Mario Eclipse's three-character roster.

### Architecture

| File | Role |
|---|---|
| `assets/Player.tscn` | Shared base — skeleton, camera rig, hand slots, VFX, all 51 nodes. Formerly `Mario2.tscn`. |
| `assets/<Name>.tscn` | Thin inherited scene. Overrides only the mesh, skin, spin-jump colours and hand attachments. |
| `assets/<Name>.tres` | `PlayerProfile` — eye textures, head gear, texture-match strings. |
| `assets/characters/<Name>Entry.tres` | `CharacterEntry` — what the select screen shows: display name, scene, head art, name art, stroke art, paint colour. |

Adding a character touches **no code**. Everything is data.

### Roster

| | Mesh source | Head art | Name art | Stroke | Hands | Hat |
|---|---|---|---|---|---|---|
| Mario | original | ✓ | ✓ | M (extracted) | base | — |
| Luigi | Luigi.blend | ✓ | ✓ | L | ✓ | — |
| Koopa | — | ✓ | — | K | ✓ | — |
| Piantissimo | — | ✓ | ✓ | P | ✓ | ✓ |
| Wario | BSMSO | ✓ | — | W | ✓ | ✓ |
| Waluigi | BSMSO | ✓ | — | W | ✓ | ✓ |
| Yoshi | BSMSO | ✓ | — | Y | ✓ | — |

Outstanding: `NameArt` for Koopa, Wario, Waluigi, Yoshi (falls back to plain `DisplayName` text on the poster).

### PlayerSpawner (`assets/PlayerSpawner.cs`)

Levels no longer hard-code a character instance. A `PlayerSpawner` Node3D marks the spawn point; at
runtime it instantiates `GameData.Instance.SelectedCharacterScene` (or `FallbackCharacter` when running
the level directly from the editor) and adopts it as its own child.

- Adopts as **its own child**, not a sibling. During ready propagation the parent is blocked
  ("Parent node is busy setting up children"), and `call_deferred` would push the spawn past
  `Level._Ready`, which needs the player to already exist.
- Snapshots `GetChildren()` **before** `AddChild(player)`, or the player ends up in its own handover
  list and gets reparented into itself.
- Handover only suits nodes that don't resolve NodePaths in their own `_Ready` — those run before the
  spawner does. `ShineTimer` and `TimerHud` therefore live at the level root, declared *after* the spawner.

### ⚠️ Gotcha: the spawner must hand its rotation down to the character

Directly related to the 180°-yaw gotcha in the Surface Tilt section. `Mario.cs` composes movement as:

```csharp
inputDirWorld = (Transform.Basis * new Vector3(LstickVec.X, 0, LstickVec.Y)).Normalized();
inputDirWorld = inputDirWorld.Rotated(Vector3.Up, camYaw);   // camYaw is springArmPivot's LOCAL yaw
```

That is the character's **local** basis plus the camera's **local** yaw, and the two only sum to the
camera's world yaw while the character's parent is unrotated. Any rotation left on the spawner leaks in
as a constant offset — a 180° there comes out as **inverted controls**, not as a turned character.

So `PlayerSpawner._Ready` transfers its authored basis to the spawned character and keeps only the
position, reproducing exactly the graph levels had before spawners existed. `ExtraFacingDegrees` (default
0) is available for a per-spawn nudge.

Two wrong fixes were tried first, both because the 180° was placed relative to the wrong node:
putting it on the character root **while the spawner was also rotated** (doubled), and moving it to the
Armature (root went identity but the parent's rotation still leaked). The value was never the problem —
its position in the hierarchy was.

---

## Character Select Screen

`assets/CharacterSelect.tscn` / `.cs`. Base viewport is **1152 × 648** (project has no explicit
`viewport_width`/`viewport_height`, so Godot's default applies, scaled via `stretch/mode="canvas_items"`).
Authoring against 1920×1080 is why decor kept landing offscreen.

### Flow

Poster focused → `Highlight(i)` brightens it, scales it to `SelectedScale` (1.06) and slides the P1 hand
beneath → confirm → `PlayStamp()` paints the character's letter → hold `StampHoldSeconds` (0.45) →
fade to black over `FadeSeconds` (0.7) → `ChangeSceneToFile(LevelToLoad)`.

`Confirm` is `async void` with a `_confirming` guard so a second input can't double-load.

### Gotchas

- **`MoveHandTo` must be deferred.** Containers lay out on the next frame, so reading a poster's
  `GlobalPosition` in the same frame gives the pre-layout value.
- **Setting `PivotOffset` displaces a scaled or rotated node.** Safe only while scale is 1 — the poster
  sets its pivot before scaling for exactly this reason. The Palm decor hit the general case and needed
  `Position += delta - basis * delta` compensation.
- **`flat = true` does not suppress the focus StyleBox.** All five button states need `StyleBoxEmpty`
  or a white outline draws around the selected poster.
- **`anchors_preset = 8` is centre-anchored**, so offsets are measured from the centre, not the top.
- **`HBoxContainer` lays out on minimum size**, so scaling a poster is purely visual and won't shove
  its neighbours.

---

## Paint Stamp — stroke-order maps

The letter painted onto a poster when a character is picked. Full runbook, with a live demo, is
published as an artifact; this is the summary.

### The idea

Super Mario Eclipse animates its stamp as 16 frames. Those frames are **strictly additive** — ink is only
ever added, never erased — so every pixel has a well-defined moment it was painted. That collapses all 16
into one texture:

```
R channel = when this pixel was painted (0 = first stroke, 255 = last)
A channel = the finished shape
```

`assets/PaintReveal.gdshader` walks the whole draw from a single `progress` uniform. 138 KB for six
letters, against 35 MB of source frames — and the shader has no idea which letter it is drawing, so a new
letter is a new texture, not new code.

### Tools

| Tool | Does |
|---|---|
| `tools/bake_stroke_order.py` | Collapses a progressive frame sequence into one order map. Recovers frame order from ink coverage, since the source filenames are hashes. |
| `tools/make_stroke.py` | Synthesises a letter from brush paths, for letters the source art never had. |

### Brush parameters (`tools/make_stroke.py`)

| | Value | Notes |
|---|---|---|
| `NIB_RATIO` | 0.34 | Short ÷ long axis of the chisel tip. **The dominant value.** Above ~0.5 every stroke is uniform and reads as felt-tip. |
| `PEN_ANGLE` | −0.60 | Radians the nib is held at. |
| `SLANT` | 0.45 | Italic shear, x-units per y-unit. Re-leans every letter at once, then recentres. |
| `BASE_Y` | 0.82 | Height the shear pivots about. |
| `ROUGHNESS` | 40.0 | Outline wander. Above ~70 it speckles. |
| `GRAIN` | 14.0 | Wander wavelength. **Below ~10 it reads as a bad scan.** |
| `width` | 0.086 | Stroke radius as a fraction of canvas. Coupled to `NIB_RATIO`. |

### ⚠️ Gotcha: the raggedness metric is a trap

Edge roughness — perimeter against a smoothed copy — measures the extracted M at **1.067** and a clean
generated letter at **1.001**. Two full passes were spent adding noise to close that gap, and both made
the letter look like a bad scan. The M scores rough because of a few **sharp corners and flat cut ends**;
its actual outline is smooth.

Match on **per-stroke width** (9.4%), never on total ink — different letters have different stroke counts.
Matching ink made the L's strokes 46% too fat. And compare by eye, side by side at 300px+.

### ⚠️ Gotcha: soft-edge reveals need a hard endpoint

The shader runs its window slightly past 1: `progress * (1.0 + softness)`. Without it the last-painted
pixels sit permanently half-drawn. Same failure as the wipe shader that left a lingering line.

### ⚠️ Gotcha: the ShaderMaterial is shared

All four posters share the scene's material, so `PaintReveal._Ready` duplicates it per instance.
Remove that and every poster animates in unison.

---

## SMS Model Import Pipeline

Six scripts in `tools/` turn a mod archive into a playable character. Full runbook published as an
artifact — the traps are documented there in detail.

| Tool | Does |
|---|---|
| `unpack_rarc.py` | `.arc` / uncompressed `.szs` → loose BMDs. |
| `prep_character_glb.py` | Blender pass: merges the doubled skin, sets `metallicFactor` to 0. |
| `make_skin_tres.py` | Generates the Godot `Skin` from glTF inverse bind matrices. Replaces the manual Advanced-Import step. |
| `extract_bmd_texture.py` | Decodes RGB565 from a BMD's TEX1 when the converter mangles it. |
| `fix_sms_part.py` | Repairs an accessory glb: forces OPAQUE, zeroes metallic, resets bogus alpha. |
| `repoint_part_texture.py` | Rebinds an accessory to the atlas its UVs actually sample. |

Existing checks `vet_rig.py` and `predict_rest.py` still gate eligibility.

### Source: BSMSO 1.1 CustomModels

14 complete Mario-replacement archives. **11 convert cleanly** (Birdo, Daytendo, Luigi, Needle,
Nightendo, Nokissia, Piantissimo, Shadow Luigi, Waluigi, Wario, Yoshi). Shadow, Shadow Mario and Sonic
crash FinModelUtility with the same `ArgumentOutOfRangeException` — not an archive problem, their BMDs are
structurally identical to the working ones. They need SuperBMD or a Blender J3D importer.

The `.arc` files are plain uncompressed RARC despite the extension; only `Yoshi.szs` is Yaz0, and its
`.arc` twin isn't.

### ⚠️ Gotcha: accessories are bound to the wrong texture

The converter binds caps and hands to a small intensity mask from their own BMD, when the UVs actually
index into the **character's body atlas**. The part imports grey or white.

**Zero colour saturation on an accessory texture is the tell.** Confirm by sampling its `TEXCOORD_0`
against the body atlas — Wario's cap returned `#E8BC00` yellow, Waluigi's `#6000C8` purple. Same fault
that made Piantissimo's helmet white; it hits every hand as well as every cap.

Do **not** just tint a grey part — that looks nearly right and buries the real bug.

### ⚠️ Gotcha: I4 intensity is copied into alpha

Intensity-only textures arrive with the intensity duplicated into the alpha channel, and `alphaMode` set
to `BLEND`, so parts render semi-transparent. Signature is exact: every pixel has `alpha == red` and none
is fully opaque. A real cut-out mask won't match its own intensity on every pixel, so the test is safe to
automate.

### ⚠️ Gotcha: deleting a source texture mid-flight cascades

After repointing a glb, the old PNG is unused — but deleting it while Godot holds import state for it
makes every dependent reimport fail. **A resource whose dependency fails to load fails itself**, so a
broken `WarioCap.glb` takes down `Wario.tres`, `Profile` comes back null, and you get two symptoms that
look nothing like a texture problem:

- **No hat** — `ApplyHeadGear` has no `HeadGear`
- **Mario's eyes** — `SetupSleeping` does `Profile?.AwakeEyeTexture ?? <Mario's>`, and `SleepStatus(false)`
  writes that fallback into both eye materials

Read the editor log first; the reimport failures name the cause immediately. Stop the game, rescan, relaunch.

### ⚠️ Gotcha: two checks that lie

- **A Blender rest render is not predictive.** It shows the raw bind pose, not the skinned result on
  `Player.tscn`'s skeleton. Yoshi renders as a collapsed heap and is fine in game.
- **`predict_rest.py` cries wolf on wide characters.** Its "NOT upright" flag compares height against
  *arm span*; Wario and Waluigi both trip it and both are correct.

Three bind conventions are in play and **all are valid** — Luigi `(96.20, −2.95, −0.12)`,
Koopa/Piantissimo/Yoshi `(61.00, −39.01, 0.00)`, Wario/Waluigi `(1.50, 97.14, −0.35)`. Matching a
known-good character's convention is evidence; deviating from one is not evidence of a fault.

### Hand slot mapping

| BMD | Verts | Slot |
|---|---|---|
| `ma_hnd3l` / `ma_hnd3r` | 137 | LeftHandClosed / RightHandClosed |
| `ma_hnd2l` / `ma_hnd2r` | 132 | LeftHandSlightyOpen / RightHandSlightyOpen |
| `ma_hnd4r` | varies | RightHandWithHat |

Counts match Mario's own slot models. `ma_hnd4r` is the one most likely to fail conversion — fall back to
the closed fist for a character with no cap (Yoshi does).

---

## Level Intro — first-frame flash

`Level._Ready` called `OpenTransitionRects()`, which sets every transition rect fully open, while
`StartIntro` is deferred. The level therefore rendered in full for a frame or two before `intro_pan`'s
`t=0` Iris key landed — a visible flash of the level before the transition began.

`CoverForIntro()` now asserts that same `t=0` value up front when `PreviewShot` is on, so the first
rendered frame is already covered. Every path where the intro doesn't actually run re-opens the rects, so
a misconfigured level can't come up stuck black.

---

## SMS 1:1 Physics Port (branch `sms-physics`)

**Files:** `assets/Sms/SmsPhysics.cs` (new), `assets/Sms/SmsSlopeHipDrop.cs` (new), `assets/Mario.cs` (bridge methods at the end of the class + patched launch / ground / air / slide / landing / bonk / ground-pound sites)

Replaces the hand-tuned movement numbers with the real Super Mario Sunshine player physics, ported from the
`doldecomp/sms` decompilation (`src/Player/`: `MarioInit.cpp`, `MarioMove.cpp`, `MarioRun.cpp`, `MarioJump.cpp`,
`MarioPhysics.cpp`) and using the **shipped** tuning values decoded from the disc's `data/params.szs`
(`/mario/jump.prm`, `run.prm`, `mario.prm`, `slipparamnormal.prm`, `controller.prm`). The `MarioState` machine,
`SetMarioState` animation map, VFX and collision are unchanged; what changed is where the numbers and the movement
equations come from. Everything sits behind one toggle so the old feel is one click away. Ten commits,
`7eea539` → `e469a5e`, each one a playtest iteration with Jaysonn.

### Inspector exports (on Mario)

| Export | Default | Meaning |
|---|---|---|
| `UseSmsPhysics` | `true` | Master switch. Off = the previous hand-tuned physics, untouched. |
| `SmsDebugPrint` | `true` (while tuning) | Prints launches, slide decisions, jump apexes, bonks, wall hits, steps and ground pounds to Output. Turn off for release. |
| `SmsRollJumpMinSpeed` | `20` | `catching()`: slide speed above which A is the big roll-jump. **Game value is 40**; 20 chosen by feel — a deliberate deviation. |
| `KeepGroundPoundJump` | `false` | Restores the GP-jump (see Ground Pound section above). Off because Sunshine has no such move. |
| `SmsLedgeSnap` | `true` | Step DOWN ledges up to 100 units (1.34 m) without leaving the ground: sets `floor_snap_length` to 1.343 m (Player.tscn ships 0.6). |
| `SmsStepUp` | `true` | Step UP risers up to 30 units (0.40 m) without breaking stride (`SmsTryStepUp`, after MoveAndSlide). |
| `SmsStepSmoothRate` | `12` | Visual only. The body pops up or down a riser instantly (the game does too); the rig rides a virtual ramp through the staircase (one riser per tread of horizontal travel, see the stairs gotcha) and never settles slower than this standstill ease (1/s). 0 = the mesh pops with the body. `UpdateStepVisual` offsets Skeleton3D and WaistTarget together. |
| `SmsWallSlidePinTicks` | `4` | Ticks (120 Hz) of zero velocity when a wall slide begins. The game's is 10 (`stayWall`, 83 ms; the state machine runs per 5 ms tick, verified in `TMarDirector::direct`), which playtested as "sticks to the wall"; 4 (33 ms) keeps a slight catch before the slide. Deviation by request. 0 = slides at once. |
| `SmsRollJumpKnockdown` | `false` | **Deviation.** Off: the roll-jump wall-slides on a head-height wall. On: the decomp's rule (knockdown above 40; the roll-jump launches at 55 and never drops under 40 with the stick held). |

On Play, Output must show `[SMS selftest] ALL OK` and `[SMS selftest] slope/hipdrop ALL OK`. Anything else means a
constant or equation was edited into an inconsistent state — fix before playtesting.

### Units and time base

- SMS runs at 30 fps but the director steps the player **4 sub-ticks per frame (120 Hz)**. Velocity fields
  (`mForwardVel`, `mVel.y`) are in units per *frame*; each tick moves `vel / 4` and subtracts gravity once.
- Mario is 160 units tall. Our `ma_mdl1.glb` armature import scale is `0.0134334`, so **1 metre = 74.44 units**
  (`SmsUnits.UnitsPerMetre`). Jump heights therefore land at the right fraction of Mario's own height.
- Conversions (`SmsUnits`): `m/s = v × 30 / 74.44 = v × 0.403`; `m/s² = g × 3600 / 74.44 = g × 48.36`.
- Godot's 60 Hz physics frame runs however many 120 Hz ticks `SmsUnits.TicksFor(delta, ref accum)` says (normally 2),
  so timers stay correct if the physics rate changes.
- Vertical bookkeeping: `_smsTrueVy` is the post-gravity SMS value; `velocity.Y` handed to `MoveAndSlide` is the
  sub-tick average. `SmsAfterMove` restores the true value after the move. Don't read `velocity.Y` for SMS logic.

Reference conversions at this scale:

| SMS | Converted | |
|---|---|---|
| Gravity 1.0 | 48.36 m/s² | old `GRAVITY` was 62.2 |
| Spin-jump fall gravity 0.35 | 16.9 m/s² | only while descending in a spin jump |
| Run max 32 | 12.9 m/s | old `RUN_SPEED` was 11.55 — already close |
| ClashSpeed 40 | 16.1 m/s | the one threshold behind every wall/slide decision |
| Terminal −75 | −30.2 m/s | |
| Standing single jump vy 42 | apex 0.35 s, 3.03 m = 1.41 Mario heights | matches the real game |

### ⚠️ Gotcha: the compiled-in defaults are not the game

`MarioInit.cpp`'s `PARAM_INIT` values are what the code *starts* with; `params.szs` overrides them at boot and the
differences are large. Anyone porting from the decomp source alone tunes to the wrong game.

| Param | code default | shipped |
|---|---:|---:|
| Gravity | 0.8 | **1.0** |
| SpinJumpGravity | 0.6 | **0.35** |
| JumpAccelControl (air steer) | 0.5 | **0.008** |
| JumpSlideControl (air side drift) | 0.5 | 0.3 |
| UltraJumpForce (triple) | 70 | **75**, XZ kept ×1.0 |
| TurnJumpForce / BackJumpForceY / RotateJumpForceY | 62 | **70** |
| HipAttackSpeedY (GP fall) | −50 | **−80** |
| SecJumpEnableSp (double min speed) | 20 | **0** |
| TriJumpEnableSp (triple min speed) | 20 | **25** |
| RotBroadJumpForce / ForceY (roll-jump) | 60 / 20 | **55 / 38** |
| Run DecBrake / Soft2Walk / Walk2Soft | 1 / 8 / 5 | 5 / 13 / 8 |
| SlipFriction (belly slide) | 0.9 | **0.97** |

### ⚠️ Gotcha: 4 sub-ticks per frame (acceleration is ×3600, not ×900)

Gravity is subtracted once per *tick* while the velocity is per *frame*, so an acceleration of `g` is
`g × 120 × 30` units/s². Using 900 (one tick per frame) makes every jump 4× too floaty and is an easy mistake to
make when reading `jumpProcess` in isolation. Sanity check that catches it: a standing single jump must apex at
10.5 frames / 0.35 s.

### Stick (`SmsStick`, `controller.prm`)

`raw = 128·axis` → deadzone ±7 (with 6 subtracted) → length × 0.78² → clamp 64 → `mag = 32·(len/64)²`. Quadratic,
so a half tilt walks and full magnitude arrives at ~82 % tilt. `mag` is the run target speed (0..32); sneak goes
through the same curve.

### Run (`SmsRun`, `doRunning`)

- Accelerate toward `min(32, mag)`: `fwd += 1.1 − fwd/43` per tick (the SM64 lineage), **capped at the target**; above
  target on flat ground brake 5/tick (`DecBrake`), **never below the target**; stick released `−1/tick` (`walkEnd`);
  braking/skid `−4/tick`. The two clamps are ours (see the deviations): taken literally the accelerate step overshoots 32
  by 0.36 and the brake of 5 yanks it back in one tick, a 27..32 saw-tooth (10.9..12.9 m/s) at ~9 Hz that the stairs
  log made visible as a stutter. Sprint now holds 32 = 12.9 m/s.
- Turning is speed-independent (`RunningRotSp` 1500 BAM/tick shipped min == max) and **preserves speed** — velocity
  always points along the face angle on the ground. A stick reversal of more than ~100° skids instead of turning.
- Gait bands with hysteresis: soft step ↔ walk at 8 / 13 (`Walk2Soft` / `Soft2Walk`).

### Jumps and air (`SmsAir`)

Launch table (`setStatusToJumping`), all in SMS units, `fwd` = speed at the moment of the press:

| Launch | vy | fwd | Notes |
|---|---|---|---|
| Single | 0.25·fwd + 42 | ×0.8 | fall-cancel |
| Double | 0.25·fwd + 52 | ×0.8 | no minimum speed; fall-cancel |
| Triple | 0.15·fwd + 75 | ×1.0 | needs fwd ≥ 25; no cancel |
| Spin (ground) | 0.25·fwd + 70 | ×0.8 | no cancel |
| Sideflip (U-turn) | 70 | 8 | new direction |
| Backflip | 70 | −18 | |
| Wall kick | 62 | 24 | away from the wall; fall-cancel |
| Dive from the air | unchanged | **48 always** | `checkBackTrig` sets 80 then caps at 48 |
| Dive from the ground | 20 | min(15 + fwd, 48) | needs fwd > 15 |
| Roll-jump (from slide) | 38 | 55 | `ROTATE_BROAD_JUMP`, `ma_roll_jump`; fall-cancel |
| Small roll (from slide) | 30 | kept | `CATCH_STOP`, `ma_roll` |
| Stomp bounce 1 / 2 / 3 | 0.25·fwd + 35 / 45 / 70 | ×0.8 | |
| Ground pound | 0 | 0 | then −80 constant |

- **Variable height is the fall-cancel, not a hold ramp:** while A is released and `vy > 20`, `vy *= 0.75` per
  tick. Only the launches marked fall-cancel have it (`_smsFallCancel` is a property of the launch, so a spun
  single jump keeps its cut and a ground spin jump never has one).
- Air control is nearly committed: forward steer `0.008·mag·cos(Δ)` per tick, sideways drift `0.3·mag·sin(Δ)` applied as
  a transient offset (not accumulated, as in the game), horizontal air speed capped at 80, air brake 0.999/tick.
  It applies to every launched state **and to a plain fall off an edge**: `running()` → `walkProcess` 0 → `LANDING`,
  whose `landing()` is `jumpingBasic(LAND_SLIP, ANIM_LAND, 3)` — the same stick control as a jump, the ledge bit, B
  dives, and the landing opens the double-jump window (`SmsAirSteers`; the face is latched from the velocity at the
  edge). The `LAND_SAFE_DOWN` drop after letting go of a ledge has none. After **240 ticks (2 s) of falling** the game
  plays the cry (`MSD_SE_MV10B_CRY_JUMP_01`) and switches to `ANIM_THROWN` until he lands; the port drives `ma_thrown`
  directly (it is in the library, not in the tree; `SetMarioState` re-enables the tree on landing). The port has no
  voice assets, so no cry. Debug line: `[SMS fall] 2 s of falling: ma_thrown ...`.
- The face angle is latched **once** at launch, from the velocity direction (not the model, which the old code snaps to
  the stick — that snapped the takeoff direction and lost speed). The model holds it in the air; no per-frame yaw.
- Jump chain window 16 ticks (0.133 s) after touchdown; A inside it = next chained jump, B = dive (vy 20).
- Gravity mode `SpinJump` (0.35) applies only while falling in a spin jump; everything else 1.0. Terminal −75.

### Spin jump (`SmsStickRotate`)

There is no separate spin-input buffer. While the stick is off-centre the intended yaw is recorded into a 40-tick
(0.333 s) sliding window, cleared the instant the stick is released; a spin is recognised when all four 90° sectors
are present. Godot samples once per physics frame, so the window is 20 samples. Checked at two moments:
1. every ground A press, **before** single/double/triple (`considerRotateJumpStart`) — so a spin beats the chain;
2. every tick of a single, double, sideflip, wall or stomp jump (`considerJumpRotate`): the jump becomes a spin jump
   on the spot with velocity untouched. Triples, dives, rolls and backflips can't convert.
The model spins 2700°/s in the direction the stick was spun. The R-tap ground spin still exists and launches with the
`RotateJump` values.

### Belly slide and rollout (`SmsSlide`, `catching()`)

- Slide friction `fwd *= 0.97` per tick, plus `StickSlideMult 0.005·stick` pushing — net loss ≈1 %/tick with the
  stick held, 3 % neutral. Stops below 1 (`ma_lost` get-up). A dive lands at ≈47.
- Uphill the slide additionally loses `1.5·sin(angle)` per tick (`slipparamnormal` SlideAccele 0 / 1.5) and stops at
  the top; downhill gets **no** gravity assist, friction only.
- **A during the slide** (fresh press; SMS reads the frame-pressed bit, so holding A into the dive does nothing):
  `fwd > SmsRollJumpMinSpeed` → roll-jump (`singleRollout`, lands into run-out); otherwise the small roll
  (`bellyRollout`, `ma_roll` then `ma_slpla`), which keeps the slide speed and lands like `LAND_END`: velocity zeroed
  → dead stop unless the stick is held, in which case RUN on the first tick at `max(current, stick)`.
- **B during the slide** re-dives (vy 20).
- The press is accepted across the touchdown frame (this frame or the previous one): the game evaluates A on the
  same tick the dive lands, Godot reaches the slide code one physics frame later, and the press was being lost.

### ⚠️ Gotcha: the game's roll-jump is a 130 ms trick

With ClashSpeed 40 as the threshold and a dive landing at ≈47 decaying 1 %/tick, the big roll-jump only exists for
the first ~0.13 s after touchdown on flat ground; anything later is the small roll. That is what the decomp says
(`MarioRun.cpp` is NonMatching, so an inverted comparison is conceivable but unproven). Jaysonn wants the big roll
reachable at about half speed, hence `SmsRollJumpMinSpeed = 20`. Session log of the decision: `logForLarry.txt`.

### Landing window (`jumpSlipCommon` / `*_JUMP_END`)

No input-locked landing lag exists in the game. What exists:

| | Single / double | Triple |
|---|---|---|
| Slip window | 16 ticks (0.133 s) | 4 ticks (0.033 s) |
| Stick held during window | keep moving, `fwd *= 0.98`/tick, no steering | same |
| Stick neutral during window | skid −4/tick while ≥ 16, coast below | same |
| Exit, stick held | RUN on the first tick at `max(current, stick)` — no re-accel dip | same |
| Exit, stick neutral | dead stop; landing anim plays until stick or A interrupts | same |

The old code froze 7 / 20 frames; the triple no longer locks you for a third of a second.

### Wall hits and knockdowns (all keyed on ClashSpeed 40)

| Situation | Rule |
|---|---|
| Jump hits a wall | only when the wall is **in front of the face** (normal within ±65.9° of it, `checkGroundAtJumping`); then on the speed **along the face**: ≤ 16: stop against it, keep falling · 16–40: wall slide · > 40: knockdown. A wall behind or beside the face is not a hit: he presses against it and keeps falling |
| Dive (airborne) hits a wall | knockdown at **any** speed (`jumpCatch`), mirrored path (`Bounce`), rising speed cut to 0 |
| Belly slide hits a wall | > 16: knockdown (`slippingBasic` → `CATCH_DOWN`, mirrored) · ≤ 16: stop dead, get up (`CATCH_LOST`) |
| Running into a wall | > 40: knockdown with no hop (unreachable above the run cap 32 without borrowed speed) |
| A during belly slide | > `SmsRollJumpMinSpeed` (20): roll-jump · otherwise the small roll |

Knockdown: 16/tick backwards (6.4 m/s), vertical speed left alone (no pop), no extra gravity (the old bonk applied
its own on top of the general one — removed). On touchdown `SHORT_BACK_DOWN`: slide decays ×0.96/tick (~1.3 m)
while `ma_sdown` plays to the end, uninterruptible, then get-up, then WAIT. Normal jumps cap at 32, so air knockdowns
come from roll-jumps and dives, and ground knockdowns only from borrowed speed (roll-jump run-out, downhill).
The face is fixed for the whole jump (`doJumping` never turns it; the back stick drives the forward speed negative instead,
to −16), so pulling back toward a wall he just kicked, or drifting sideways into one he is flying past, never grabs it.
The ground pound is on **L**, Sunshine's layout (`button_l`, shared with the camera). The camera takes L only when Mario is
on the ground or in the wall slide; a press that starts in the air is the ground pound alone, no re-centre and no hold mode
(`SunshineCamera.UpdateLButtonState`; the game's camera re-centres on any L press — `execFrontRotate_` — a deviation by
request). On a wall slide neither B nor the ground-pound button does anything. The decomp's `checkBackTrig` would take both:
B dives along the face into the wall and `jumpCatch` case 2 bounces him off; L enters `HIP_DROP`, whose first `hipAttacking`
tick meets the wall in front of the face and turns it into `JUMP_SHORT_BACK_DOWN` — a knock-off either way, never a ground
pound. The shipped game as remembered does nothing on a wall but re-centre the camera; we follow the memory here.
Wall slide: velocity zeroed for the first **10** ticks (83 ms; `stayWall` advances its timer twice per pinned tick; the
port defaults to 4 — see `SmsWallSlidePinTicks`), then
`vy = −0.5·timer` with the timer already at 21 — −10.5 (−4.2 m/s) at once — capped at −30 (−12.1 m/s) from tick 50; the
position is nudged into the wall each tick (`WALL_STICK_SPEED`); the stick, B and the ground-pound button do nothing; the
slide ends by A (kick),
landing (face turned away, `LANDING` → `LAND_SLIP`: double available) or losing the wall (face away, velocity 0, fall).
Wall kick (`startJumpWall` → `setStatusToJumping` WALL_JUMP): vy 62, fwd 24 along the bounce direction (the heading
mirrored across the wall normal), fall-cancellable, no hold, no commit lock, no regrab cooldown. While the kick is
rising, `doJumping` ignores a stick more than 90° from the face, which is what stops him re-sticking to the wall he
kicked (the legacy `wallKickLock` / `wallRegrabCooldown` are off in SMS mode). `jumpWall` lands in `JUMP_SLIP`, so a
double jump follows a wall kick; a stomp bounce lands in `SEC_JUMP_SLIP`, so a triple follows it. In SMS mode the wall
slide is entered from `PostMoveWallCheck` only; the legacy pre-move `CanStartWallSlide` entry (8 m/s minimum, fall
ratio, takeoff cone) is off because it re-grabbed walls the game had just ignored.

In SMS mode `PostMoveWallCheck` decides all three air outcomes itself from `jumpingBasic()` case 2: knockdown above 40,
wall slide for 16–40 (no fall-speed ratio, no takeoff cone — the legacy gates refused slides the game gives; the
game's head-on band is `mClashAngle` 12000 BAM = 65.9°, approach dot ≥ 0.41), stop at or below 16, nothing at all
for a glancing touch. Two more rules from `checkGroundAtJumping` / `catchStop`:

- **Head-height rule.** Walls are tested at feet + 30 *and* feet + 150 units. For the statuses that pass the ledge bit
  (single/double jump, fall, sideflip, wall kick, stomp bounce, roll-jump — not the triple, backflip, spin, dive, small
  roll or ground pound) a wall that does **not** reach feet + 150 (2.0 m above his feet at that moment) is not a wall
  hit at all: `hangonCheck` may grab its ledge (falling, top more than 100 units above the feet, roof clear), otherwise
  nothing happens and he is just pushed off it. So a roll-jump into a platform edge never knocks him down; only a
  wall taller than his head at contact does (`SmsWallReachesHeight`, ray at feet + 2.015 m).
- **Small roll.** `catchStop()` into any wall: `setPlayerVelocity(0)` — he stops in the air and drops; no slide.

Why the roll-jump bonks in this build more than in the game: with `SmsRollJumpMinSpeed` at 20 nearly every A out of
a slide is the 55-speed roll-jump, and with the stick held forward `doJumping` adds 0.008·32 = 0.256/tick against
the −0.2/tick above 32, so it never drops under 40 — into a head-height wall that is a knockdown, every time, in the
game too. At the game's threshold (40) the common roll is the small one, which stops.
A riser lower than 30 units is not a wall in the air either (`checkGroundAtJumping` tests walls at feet + 30 and
+ 150): see Ground contact — the step-up runs for air states and a dive onto stairs belly-slides up them.

### ⚠️ Gotcha: surplus speed survives on slopes, and that is where the "random" knockdowns come from

`doRunning` only brakes speed above the target (`DecBrake` 5/tick) on ground with `normal.y ≥ 0.95`
(`DecStartNrmY`, flatter than ~18°). A roll-jump lands at ≈50 and drops to 32 within 4 ticks on flat ground, but on
any steeper ramp the 50 is kept the whole way up. Run off the top into a wall and `fwd > 40` knocks him down — as
in the game. Plain running (cap 32) can never bonk; if a knockdown looks wrong, read the `[SMS bonk]` line's speed.

### Ground pound (`SmsHipDrop`, `hipAttacking` / `hipAttackEnd`)

- R starts a **true hover: 60 ticks (0.5 s) with all velocity zeroed** — a pound from a sprint drops straight down.
- For the first 40 ticks he lifts `(40 − t) × 0.125` units/tick, 102.5 units = 1.38 m total, rate fading 8 m/s → 0.
  A roof within reach stops the lift. Floor contact during the hover = landing.
- The hover ends on the tick count, not on `ma_hipsr`; the fall is −80/tick **constant** (32.2 m/s), no gravity,
  no steering.
- Landing: no inputs read while `ma_hiped` plays (jump buffer zeroed); any stick or A input cancels `ma_slped`, and
  A there is an ordinary standing jump (`StartGroundJumpPreserveXZ`).
- The GP-jump (fixed 19.5 m/s spinning jump out of the landing) is not a Sunshine move and is off in SMS mode;
  `KeepGroundPoundJump` brings it back.

### Slopes (`SmsSlope`, `walkProcess` / `slopeProcess`)

- Shipped `slipparamnormal` has **SlopeAccele 0 / 0**: running speed is not changed by slopes at all. Only special
  surfaces accelerate (sandy "45" slopes 1.0 / 0.022, force-slip goop 4.5 / 2.0, slider courses) — see Surface types.
- `walkProcess` (`MarioPhysics.cpp:214`) steps horizontally by `vel/4 × normal.y` then snaps to the floor: speed
  **along the surface** is always `fwd`, horizontal speed is `fwd·cos(slope)` in every direction, across a hill too.
- Godot with `floor_constant_speed = true` (verified in `character_body_3d.cpp`
  `_move_and_slide_grounded`) already does this **along the gradient**, uphill and downhill (the downhill case is the
  `_on_floor_if_snapped` redirect). The only gap was *across* a slope, which Godot ran 1/ny too fast.
  `SmsSlope.GroundStepScale = sqrt(ny² + (n_h·dir)²)` (1 along the gradient, ny across) scales the horizontal velocity
  length before the move and un-scales it after.
- `SlipStart 0.4` (66°) is the game's standable limit; his slip system is left as is.

### Surface types (`SmsSlope.Surface`, `isSlipStart` / `doSliding` / `slideProcess`)

The game classes every collision triangle (`TBGCheckData`), and the class picks one of the `slipparam*.prm` sets. In
Godot the class lives on the collision body: metadata **`sms_surface`** = the class (a String) on the `StaticBody3D` or a
parent up to three levels up, or a metadata key named **`sms_<class>`** (any value), or a node group **`sms_<class>`**.
Untagged geometry is `normal`, so nothing changes until a body is tagged. A tag applies to the whole body: to make one
slope a slider, give it its own `StaticBody3D`. A flat slider has no slope to pull him, so he only stands in the slip
pose (A jumps out): tilt it.

| Tag | Game class | Slips when | Params (`slipparam*.prm`) |
|---|---|---|---|
| `normal` (default) | plain ground | floor steeper than `SlipStart 0.4` (66°) | friction 0.97, slide accel 0 / 1.5, stop 1, stick 0.005, yaw 100 |
| `45` | `isUnk2` (sandy / steep "45" slopes) | steeper than 30° | friction 0.93, **run slope accel 1.0 / 0.022** (running downhill speeds up), slide accel 0 / 0, stop 0 |
| `slider` | `isSlider` (the slide courses) | always, never stops | friction 0.975, slide accel 4.5 / 2, stop 3, stick 0.025, yaw 512 |
| `forceslip` | `isUnk1` / goop | always, never stops, **no jump out** | friction 0.95, run slope accel 4.5 / 2, slide accel 4 / 5 |
| `wet` | `isWetGround` | as normal (66°) | flat (n.y > 0.99): friction 0.985, slide accel 5 / 2, stop 5 · sloped: friction 0.98, stick 0.1 |

**Pollution (the hazards).** The game's hot and electric ground is the goop layer (`gpPollution`, a map sampled at the floor point
under Mario every tick), not a collision class, so it is a second tag, independent of `sms_surface`: metadata **`sms_pollution`** =
`fire` (`POLLUTION_TYPE_FIRE`) or `electric` (`POLLUTION_TYPE_ELECTRIC`) on the floor body or a parent up to three levels up. A
tagged body is polluted all over (the game's per-cell goop needs every sample of a 5-point box polluted for the fire; the port's sample
is the body under the feet). See Fire and electricity.
| `noslip` | `isUnk3` | never | normal |

What the class changes, all in `Mario.cs` (`SmsUpdateSurface` after the move, `SmsSurfaceSlipFrame` at the top of the
grounded branch):

- **Running** (`slopeProcess`): the run slope term uses the surface's `SlopeAccele`. Normal ground stays 0 / 0.
- **Belly slide** (`doSliding`): the surface's friction and stop speed; on a slider or force slip it never stops on its own.
- **The floor slip** (`SLIP_FORE` / `SLIP_BACK`), the game's standing slide, which the port did not have: when `isSlipStart`
  says the floor slips (by class and angle), standing, landing, squatting and wall-pushing enter it at once; running
  enters it only when (nearly) stopped or facing within 90° of downhill (`isRunningSlipStart`), so you can run up a slider
  while you face up it. It starts with his ground velocity; facing downhill is `ma_slip`, facing uphill `ma_slpbk`
  (slides backwards). Each tick (`doSliding` + `slideProcess`): the stick adds `0.02·(mag/32)·cos` to the friction
  multiplier (pulling against the slide brakes it) and rotates the slide toward the stick by `sin·(mag/32)·StickSlideMult`;
  the slope adds `(facing downhill ? SlideAccelUp : SlideAccelDown)·sin(slope)` along downhill; friction multiplies; the
  face turns toward the slide direction at `SlideAngleYSp·fwd/32` BAM per tick; speed caps at 100. It ends when the speed
  drops under `SlideStop` (never on a slider / force slip) — he stands — or by A after 20 ticks (facing downhill, if the
  class allows the jump), B (dive, vy 20), running out of floor (a plain fall), or a wall: still slipping, the slide bounces
  off it at ×0.9 (at least 4); on a floor that stopped slipping, above 16 it is the knockdown, below it a stop.
- Debug lines: `[SMS surface] <class> ('<node>', tag '<tag>')` on change, `[SMS slip] start / end / into a wall`.

The legacy steep-band slide (`CanStartSlip` / `TickSlip`: 68°–75° contact that Godot does not call a floor, since
`floor_max_angle` is 68°) is unchanged; the two never overlap (`_smsSurfaceSlip`).

### Ledge hang (`hangonCheck` / `hanging` / `checkDescent`, `SmsParams.Hanging`)

**Grab** (`hangonCheck`, reached from `checkGroundAtJumping` with the ledge bit): the wall is solid at feet + 30 and clear at
feet + 150 (the same two probes that make it a ledge rather than a wall hit); the top — the ground 60 units inside the wall,
looked up from feet + 160 — is **more than 100 units (1.34 m) above the feet**, so grabbable tops sit between chest and head
height; 160 units of headroom over it; not force-slip, not tagged `noledge`; he is falling or at the apex (vy ≤ 0) and moving
into the wall. Only the ledge-bit moves grab: jumps, falls, the second jump, the wall kick, landing hops, the U-turn jump, the
stomp bounce and the broad jumps — never the triple, spin, backflip, dives or the ground pound. The port grabbed tops between
0.58 and 1.04 m (chest ray hit, head ray clear) from any air state, so head-high ledges were unreachable and waist-high walls
were grabbed; SMS mode now uses the game's geometry (`SmsTryLedgeGrab`). The hang anchor and the climb (`ma_hgup`) are the
port's.

**Hang** (`hanging`), each tick: B → `startHangLanding(WALL_SLIDE)`: out to the wall face, down 100 units (or to the ground),
and he **wall-slides** down the wall (not a plain drop). A with 160 units of headroom → the vault. The stick counts after 40
ticks: within 0x400 (5.6°) of the face, i.e. straight into the wall, with headroom → **climb** (the port uses 45°: the game
has a hang camera that puts "up" exactly into the wall, ours does not — a deviation); beyond 0x71c7 (160°), pulling
away → **let go** (`LAND_SAFE_DOWN`: a *passive* fall — `landSafeDown` is `jumpDownCommon` → `jumpProcess(0)`, so no
ledge bit, no `doJumping`, no B; he drops straight down from the wall face and takes no input until he lands in
`LAND_SLIP`, a double jump available — in particular he **cannot re-grab the ledge he just left**); anything else →
**shimmy** along the ledge at `MoveSp 0.06 × mag`
units/tick (3.1 m/s at full stick), following the wall round corners while the normals agree within `HangWallMovableAngle`
(dot > 0.3) and the top stays within −100/+50, `ma_hmov_l/r` at `units moved × AnmRate 0.8`; the shimmy stops after
`RapidTime` 2000 ticks (16.7 s) — from then on `ma_hang` plays at `AnmRapid` **8×** (shipped `Hanging.prm`; he is losing his
grip; driven directly like the shimmy) — and at `LimitTime` **2400 ticks (20 s) he lets go by himself**. Ground within 100 units below
(30 behind) with headroom → he vaults up on his own.

**Descend** (`checkDescent`, end of `running`): walking slowly (fwd < `DescentSp` 10 = 4 m/s) up to an edge whose wall is
within 10 units at feet − 10, normal pointing the way he faces, with the ground beyond more than 160 units down: he turns to
face the platform, `ma_hgdwn` plays (`MarioState.ledgeDescend`, the animation is driven directly, it is not in the tree) and
he hangs. Run at it and he runs off as before.

Debug lines: `[SMS ledge] grab / vault / let go / stick into the wall: climb / edge ahead: climb down`.

Gotcha: the port's `ledgeFall` doubles as the game's FALL (walking off an edge), which *has* the ledge bit, so the let-go
sets `_smsHangDropFall` (cleared when the state moves on) to suppress the grab and the B-dive for that fall. Without it
he re-grabbed the same ledge as soon as the 10-frame cooldown ran out: the drop is exactly 100 units, so the top was
back inside the 100..150 grab band for the next 0.67 m of falling. B → wall slide and "platform gone" (the game's
`LANDING`) are not affected.

Gotcha: the library animations in `models/*.res` all ship with `loop_mode` None (the binary resources store no loop
property at all), and the tree plays `ma_hang` as is, so the hang played its 1.65 s once and froze on the last frame —
invisible until someone hangs for 17 s. `_Ready` now `ForceLoop`s `ma_hang`, `ma_hmov_l/r` and `ma_thrown`, as it
already did for `ma_wait`, `ma_run1` and the tree-climb loops; `ma_hgdwn` / `ma_hgup` / `ma_hgjmp` stay one-shots
(their end is a state exit).

Jumping out of the wall push (`doPushingAnimation` caps fwd at 6 along the face): the jump leaves along the push direction,
into the wall, where the air wall hit stops it — a near-vertical hop. It used to inherit the lateral shuffle velocity.

Gotcha: pinned states (hang, climb, descend) never call `MoveAndSlide`, so `IsOnFloor()` keeps the value of the last real
step. A hang entered from the top therefore looked "on the floor" and the grounded branch stood him up. They run before
the grounded / airborne split now, and the vault and the let-go refresh the floor state with a zero-velocity move.

### ⚠️ Gotcha: never hand `MoveAndSlide` an upward velocity while grounded

`c173ccf` lifted the ground step into the floor plane so it would follow the slope. Going uphill that vector points
up, and `CharacterBody3D` treats `velocity.dot(up) > 0` as "leaving the floor": it skips `_snap_on_floor` **and** the
downhill constant-speed redirect for that frame. Every uphill lip and every steeper-to-shallower triangle launched
Mario in a straight line — "no stick to ground" all over Delfino Plaza. Fixed in `e469a5e`: the ground velocity stays
horizontal (`vy = min(vy, 0)`, then 0 on floor) and only its **length** is scaled. Downhill still relies on the
per-frame drop being under `floor_snap_length` (0.6 m).

### ⚠️ Gotcha: the "still rising" landing guard plus a floor latch can trap a state

A landing was only accepted when the floor latch was clear **and** vy ≤ 0. A roll-jump clipping a rising slope
arrives still going up, the frame is refused, but the latch gets set anyway; `MoveAndSlide` then keeps pushing him
up the surface with positive vy, the guard keeps failing, and the latch prevents a retry — stuck in the roll state
until airborne again. Fix (`e0afd10`): ignore floor contact for 3 frames after a roll launch (the original reason for
the guard: on the launch frame floor snapping still reports the ground he just left), then **any** floor contact
lands regardless of vy, as `checkGroundAtJumping` does (`MarioPhysics.cpp`: `pos.y <= floor` → land, no test on
`mVel.y`). Widened afterwards from rolls to every launched air state (jumps, spin, flips, wall kick, dives, stomp
bounce): each launch sets `_smsNoLandFrames = 3`, and after that any floor contact in a `SmsStateSteers` state
lands. `ledgeFall` keeps its separate fall-speed-proof rule.

### ⚠️ Gotcha: the legacy triple jump re-aimed the velocity

`StartJumpFromLanding` stage 2 pointed the carried speed at `_jumpChainStartDir`, the direction the *first* jump of
the chain left in (recorded as the model's facing for a standing jump). In SMS `setStatusToJumping` never touches
the face angle and `jumpSlipCommon` never turns during the window, so a chained jump leaves along the current
velocity; the re-aim launched triples backwards when the chain had turned. Legacy path kept for `!UseSmsPhysics`.
Chained jumps cannot be aimed with the stick at the press in the game: only the tiny air control applies.

### ⚠️ Gotcha: a stale branch below new code

`982638b`: the first slide fix left an old `else if (A held)` rollout branch live below the new SMS decision, so
holding A during any slide took the legacy full-speed rollout regardless of speed. When patching a decision point in
`Mario.cs`, read to the end of the block — several sites have a legacy fallback below them.

### Debug output (`SmsDebugPrint`)

`[SMS launch] <kind> …` on every launch, `[SMS jump] <state>: apex X m = Y units = Z Mario heights, airtime T s` on
landing, `[SMS slide] A at fwd … -> CatchStop/RollJump …` on every slide press, `[SMS bonk] …` with speed and cause,
`[SMS gp] …` for hover/lift/land, `[LAND_DBG]` around landing detection. Expected standing single: 1.41 heights /
0.70 s airtime; double from a run ≈2.9 heights; triple ≈5; spin ≈3.9.

### Damage (`damageExec`, the `*_DOWN` statuses)

`TakeDamage(pos)` (what `Gooble.cs` calls) is `damageExec()` with `dmgenemycommon.prm`; `TakeDamage(pos, profile)` takes
another attacker's parameters (`Mario.SmsDamageProfile`: `EnemyCommon`, `Hamukuri` = Strollin' Stu, `Namekuri` = Swoopin'
Stu, `Poihana` = Cataquack, `Fire`, `Killer`, `LampTrap`, `MapCode0`, all decoded from the shipped `params/mario/dmg*.prm`:
HP taken, down type, knockback `MinSpeed`, `InvincibleTime`).

- **Nothing while invincible** (`mInvincibilityFrames`, a 120 Hz tick counter set from the attacker's `InvincibleTime` and
  counted down in `thinkParams`; enemycommon gives **300** = 2.5 s: a `dmg*.prm` that leaves `mInvincibleTime` out keeps
  `TEParams`' default of 300 (MarioInit.cpp 941) -- enemycommon, hamukuri, poihana and fire all do; passes 25-49 read the
  absent field as 0) or at 0 HP. Port-side rule: no second hit while he is already
  down — the game's enemies recoil from the hit, ours keep touching him through the fall.
- **Direction.** The face turns toward the attacker. Attacker within 90° of the *old* face (in front): `fwd *= -1`, he
  goes backwards away from it and lands on his **back** (the `BACK_DOWN` family, `bkdwn` / `sdown` / `sfbdn` / `bdwn`);
  from behind: face += 180°, forwards away from it, on his **face** (`FORE_DOWN`: `jfdwn` / `shfdn` / `sffdn` / `sdwnf`).
  The knockback is `max(his forward speed, MinSpeed)` — a sprinting Mario is flung harder. He drops what he carries.
- **Status table** `[type + 4 × airborne + 8 × fore]`: on the ground `SAFE` (type 0) / `SHORT` (1) / the full `DOWN` (2);
  in the air `JUMP_SHORT_*_DOWN` (types 0 and 1) / `JUMP_*_DOWN` (2). Type 3 is the upper-body damage run
  (`setUpperDamageRun`, `ma_dgrun`): HP only here, the animation is not ported.
- **Ground** (`downingCommon`): the clip plays once; while its frame is under the handler's limit (bdwn 86, sdwnf 42, sdown
  88, shfdn 80, sfbdn 200, sffdn 100 — of the port's 60 fps clips, so only bdwn and sdwnf stop sliding early) he slides at
  `fwd *= 0.96` a tick (0 under 1 unit), after it ±0.1; an edge drops him into `JUMP_SHORT_FORE/BACK_DOWN` by the sign of
  `fwd`; the clip's last frame is `WAIT`. No inputs.
- **Air** (`jumpDownCommon`): `setPlayerVelocity(±16)` *every tick* (the knockback speed only matters on the ground; 6.4
  m/s), `jumpProcess(0)` (gravity, no stick, vy left as it was at the hit), the floor hands over to the ground status of
  the family, a wall mirrors the face (`playerRefrection`), zeroes a rising vy and switches to `bkdwn`; the ±16 along the
  mirrored face then carries him away from it. `PostMoveWallCheck` skips the `hurt` state for this.
- **Then** HP −= damage (0 → the port's death, `ma_die`), the damage stars (the port's bonk particles) toward the attacker.
  Not ported: camera shake, rumble, the voice, FLUDD water loss, goop.
- **Low HP** (`waiting()`, MarioWait.cpp:175): at 1..3 HP, standing with nothing in his hands, `ma_damage_wait_start`
  plays once and `ma_damage_wait` loops instead of `ma_wait`. Both are library-only clips, driven directly from
  `SetMarioState(idle)` (which resumes the position if idle is re-entered mid-pant); `SmsDamageWaitTick` chains the two
  and hands back to the tree when he heals above 3, picks something up, or moves. He does **not** doze off meanwhile:
  `waiting()` only counts the sleepy timer while the animation is `ANIM_WAIT`, so the port's idle wind-down (sit, sleep)
  is held off at 1..3 HP the way it is while carrying or on a slope.
- **Eyes.** The game's clips carry eye texture patterns (BTP on `_mat_eye_L/R`); the rip has the open eye (`H_ma_eye1`)
  and the shut (sleep) variant only. Two more exist at runtime (`SmsSetupDamageEyeTextures`): **hurt** — eyes squeezed
  shut in pain, through the knockdown clips — and **tired** — heavy-lidded, through the pant. Each is loaded from
  `models/Mario/mario/bmd/ma_mdl1/H_ma_eye1_s3tc_hurt.png` / `_tired.png` when such a file exists (drop the real rips in
  under those names; the editor imports them), else derived at startup from the two we have: tired = the open eye with
  the upper lid brought down to half the eye's height plus a lash line; hurt = the shut eye with its relaxed lower-lid
  curve erased and a squeezed arch drawn across. Open again as he gets up / heals above 3.
- Gotcha: the running waist IK is `Start()`ed / `Stop()`ped every frame in the main path, which the `hurt` block skips
  (`goto EndFrame`). Hit while running, it kept bending the knockdown clip; `SmsDamageExec` stops it (`StopWaistIKHard`).
- **No fall damage in Sunshine**: the strong landing (`vy <= -70` past `DamageFallHeight`) is `CATCH`, HP untouched.

The legacy path (`UseSmsPhysics` off) is unchanged: −1 HP, a 2 m/s shove, a 0.5 s `hurt` timer. All knockdown clips are
driven directly (the tree lacks `bdwn` and `jfdwn`); `SetMarioState(idle)` at the end brings the tree back.
Debug: `[SMS damage] -1 HP -> 7/8: BACK_DOWN type 1 on the ground, ma_sdown at fwd -30.0 (12.1 m/s); invincible 0 ticks`,
`... landed: ma_sdown, slides for 100% of it`, `... wall: face mirrored`, `... up`.

### Fire and electricity (`checkGraffitoFire` / `checkGraffitoElec`, `FIRE_DOWN` / `FIRE_JUMP_END`, `ELECTRIC_DAMAGE` / `ELEC_DOWN`)

Pass 50. The two hazards live in the pollution layer (see Surface types: `sms_pollution` = `fire` | `electric` on the floor body) and
run after the status exec and the move every tick (`checkGraffito`, MarioMove.cpp 953 -> `SmsCheckGraffito` in `SmsAfterMove`). The
`hurt` state carries them as kinds (`SmsDownKind.Fire` / `FireEnd` / `Electric`), so every "not while down" gate holds.

- **The hot floor** (`checkGraffitoFire`, MarioMove.cpp 830): standing on it (within `FireHeight` 5 units of the floor -- `graffito.prm`;
  the code default is 1000) and not invincible: already burning, the face turns 180 first (he bounces back the way he came);
  `makeGraffitoDamage(dmggraffitofire)` = the full `damageExec` with the floor as the attacker (1 HP, type 0, no knockback, the
  stars 50 units in front, the camera shake, 50 ticks invincible -- `FireInvincibleTime` is 50 too); his forward speed is restored
  (`unk55C` is a constant 50, the restore always runs), what he holds is dropped, then **`FIRE_DOWN(1)`**: straight up at
  `FireDownForce` **50** (`jump.prm`; the code default is 80), fwd kept. Standing still that is up and down in place: the flight
  is ~100 ticks and the invincibility 50, so a hot floor burns him again at every landing -- the game's hurt loop; there is no
  steering (below). `FireDamageSound` is the slot for `MSD_SE_MA_DAMAGE_FIRE`. The cloud (particle 6, `ms_firedmg_a`) is
  `Fx/FireDamage.tscn`: a bound copy per burn at his feet (`emitAndBindToPosPtr(6, &mPosition, 0)`), 50 orange-then-grey puffs over
  100 frames living 100 +-35 %, rising half a metre under the 0.88 drag, 70 units wide, spinning (pass 51).
- **Fire enemies** (`MarioReceiveMsg.cpp` 454 / 555-571 / 580-599 / 665-685): `damageExec(dmgfire)` -- 1 HP, type 1, knockback 16,
  **300 ticks** invincible, the face toward the attacker -- then `changePlayerStatus(FIRE_DOWN, 1)` replaces the knockdown before
  it ticks: up at 50 with the knockback speed kept (negative: backwards, facing the attacker). `SmsDamageProfile.Fire` and
  `.Killer` (Bullet Bill, 3 HP) carry `FireDown`; `TakeDamage(pos, SmsDamageProfile.Fire)` is the call. The hinokuri class does not
  (its hit is a plain knockdown). Swimmers keep the swim damage path (the game would launch them burning).
- **`FIRE_DOWN`** (`fireDowning`, MarioJump.cpp 698): `ma_firejmp` at 1.0 every tick. The stick: `fwd += 0.03125 x mag x
  FireDownControl` along it and the face turns by its sine -- **`FireDownControl` ships as 0**, so the stick steers nothing; it still
  trims a fwd over 32 by 2 a tick. At rest `FConverge(fwd, 0, 0.35)`. `jumpProcess(0)`: gravity; a wall mirrors the face
  (`playerRefrection`, the clip stays); the floor, under two bounces and falling: `vy = 0.4 x -vy`, fwd halves (`mStatusState++`);
  else the reaction voice (not ported) and `FIRE_JUMP_END`. Launched from a hot floor he never gets to bounce (the re-burn comes
  first); knocked onto clean ground he bounces twice (vy 50 -> 20 -> 8) and gets up.
- **`FIRE_JUMP_END`** (`fireJumpEnd`, MarioWait.cpp 504): `jumpEndCommon(ANIM_FJPEND, WAIT)` -- `waitProcess()` (standing still on the
  floor), `ma_fjpend` once, its last frame is WAIT; `jumpEndEvents()` first: A is a jump (the jump buffer carries the press), the
  stick is RUN. Eyes squeezed shut through both clips (the game's `ma_fjpend_tx.btp`).
- **The burning run is unreachable.** `FIRE_DASH` (`fireDashing`, MarioRun.cpp 1052: 160 ticks of forced run between 8 and 48, water puts
  it out), `FIRE_JUMP` (the hop, vy 31.5 / fwd 8) and `FIRE_LAND` form a loop that only they enter -- every fire source in the game
  forces `FIRE_DOWN` (a whole-tree grep). Not ported, logged as dead code; `ma_hot_wait` has no handler in the game either.
- **The electric floor** (`checkGraffitoElec`, MarioMove.cpp 924): not standing on goop the tick before, the grace timer is set to
  `GraffitoNoDmgTime` 10 (`mario.prm`) and only counts down while it runs; then, not invincible and in a **WAITING or RUNNING type**
  status (standing, running, squatting, a landing, a slip, a turn -- never in the air, never swimming or hanging) and no goop
  footprints (none here): **`ELECTRIC_DAMAGE`**, `MSD_SE_MA_DAMAGE_ELEC` + `MSD_SE_EF_LIGHTNING` (the `ElectricDamageSound` slot). The
  port's grace starts at the landing (the game's flag follows the floor under him in the air too: a jump onto goop could shock at once).
- **Electric enemies** (`MarioReceiveMsg.cpp` 419-448: the electro-koopas' message 9, not while invincible): `elecEffect()` and
  `ELECTRIC_DAMAGE` directly, no `damageExec`. `TakeShock()` is the call (port-side: not while already down or shocked).
- **`ELECTRIC_DAMAGE`** (`electricDamage`, MarioAutodemo.cpp 437): a **DEMO-type** status -- control locked, nothing moves him. Tick 1:
  the shock voice (not ported), `ma_shock` at 1.0, `decHP(1)` (`dmggraffitoelec`), the rumble (not ported); every tick the sparks
  (`ms_m_biri_a / b / c`: `Fx/ShockSparks.tscn`, bound to the centre joint -- one emitter each kept alive while the ticks come, as
  `emitAndBindToPosPtr(..., 1)` does: 120 lightning streaks a second from a 10-unit shell, 200 x 50 units along the line from the
  centre, cyan going blue; 60 arcs a second from a 50 x 72-unit shell, 100-unit billboards; 24 flashes a second within 45..90 units,
  each throwing six white 20 x 5-unit sparks out at 7 units a frame that fall at 0.44 -- pass 51); past 0x78 = **120 ticks**: the
  reaction voice, `tremble(5, 2, 0.99, 600)` (not ported: every vertex of the model kicked +-5 units and springing back for 600
  frames) and the closing puff (`ms_moe_fire_off`: `Fx/ShockPuff.tscn`, 30 dark puffs over 60 frames from the centre + 30 units,
  rising 2 m), `damageExec(the floor, 0 HP, type 3, ..., 0x3C)` = **60 ticks invincible**, then WAIT. Standing on the goop he is
  shocked again 60 ticks later (the flag stays set, no new grace). Not ported: the game's blue shimmer, a second draw pass of the
  model (`drawLogic`, MarioDraw.cpp 2060).
- **`ELEC_DOWN`** (`loserExec`, MarioCollision.cpp 225): the shock taking the last HP is the death in the electric pose
  (`ma_shock_down`, holding) instead of `ma_die`; R respawns.
- Not ported: the voices, the rumbles, the goop map itself (a tagged body is polluted all over), `checkGraffitoLava` (empty in the
  game) and the lava / poison profiles (3 HP; no source calls them), the pause-menu interplay.

Shipped values beat the decomp's defaults throughout: `FireDownForce` 50 (not 80), `FireDownControl` 0 (not 1), `FireBackVelocity` 8
(not 1), `FireHeight` 5 (not 1000), `FireInvincibleTime` 50 (not 150); `SmsParams.Jump` / `.Mario` / `.Graffito`.
Debug: `[SMS surface] pollution Fire ('SecrectPlatform2')`, `[SMS fire] hot floor '...': -1 HP -> 7/8; invincible 50 ticks`, `...
FIRE_DOWN(1) from the hot floor: vy 50 (20.1 m/s), fwd 0.0 along the face, ma_firejmp`, `... bounce 1: vy 20.0`, `... landed after 2
bounce(s), 157 ticks burning: FIRE_JUMP_END`, `... up`; `[SMS elec] ELECTRIC_DAMAGE from electric goop '...'`, `... -1 HP -> 6/8`,
`... 121 ticks: WAIT, invincible 60 ticks`, `... ELEC_DOWN (ma_shock_down)`.

### Fences and the roof hang (`FENCE_CATCH` / `FENCE_JUMP_CATCH` / `FENCE_MOVE` / `FENCE_PUNCH`, `HANG_ROOF` / `WAIT_ROOF` / `MOVE_ROOF` / `KICK_ROOF`)

Pass 52 (MarioSpecial.cpp 199-410 and 1294-1535). **One tag does both.** The game has a single surface attribute, `BG_TYPE_CLIMBABLE_FENCE`
(0x10A, `TBGCheckData::isFence`): met as a WALL plane it is climbed, met as a ROOF plane it is hung under. In Godot the collision body
(`StaticBody3D`, or a parent up to three levels up) carries metadata **`sms_fence`** (any value) or the node group `sms_fence`. The whole
body is a fence; its vertical faces climb, its underside hangs. A fence should be at least a Mario wide (the step along it needs a fence
25 units ahead within 20 and within 140 units), and a hangable underside at least 160 units (2.15 m) above the floor.

- **Catching a fence.** Running into it under `ClashSpeed` (`MarioRun.cpp` 745: the bonk above 40 comes first) -> `FENCE_CATCH`
  (`ma_fence_catch`); any jump's wall hit on it, at any speed (`jumpingBasic` case 2 tests `isFence` before the 16 / 40 branches) ->
  `FENCE_JUMP_CATCH` (`ma_fence_jcatch`); swimming into it with the stick pushed and his face within 60 deg of into it
  (`checkSwimToHangFence`) -> 100 units up and `FENCE_CATCH`. He faces into the plane (`matan(n.z, n.x) + 0x8000`) and is pressed on it
  (the game adds 25 units into the wall every tick and the wall pushes him out; here he is pinned a capsule radius off it). Through the
  catch clip `fenceFootCheck` wants a fence 40 units ahead at feet + 50 -- gone, he turns around and drops passively (`LAND_SAFE_DOWN`).
  The clip's end is `FENCE_MOVE`. `FenceCatchSound` is the slot for `MSD_SE_MA_FENCE_CATCH`.
- **`FENCE_MOVE`** (`fenceMove`): the fence 25 units ahead within 80 -- gone, he turns around and falls (`LANDING`, the steerable fall).
  The stick: `0.015625 x the raw counts x FenceSpeed 1.3` a tick, where the counts are 128 x the pad library's axis with 7 dead and 6
  taken off (full tilt 122: **2.5 units a tick, 4.0 m/s**), the vertical axis up the plane, the horizontal along the camera's right on
  the plane (the game picks `(-n.x, n.z)` or `(n.x, -n.z)` by the camera's side); the step holds only where two deeper probes (radius 20
  and 140, 25 ahead of the new spot) still find a fence -- so he stops at a fence's edge. Then `jumpProcess(1)`: the floor under the new
  feet -> `LANDING`; the top -- `hangonCheck`: the fence there at feet + 30, gone at feet + 150, the ground 60 units inside more than 100
  above the feet -> `ASCEND` (`ma_hgup`, the hang's climb-up onto the top); a roof within 160 above -> clamped under it. **A** = `startJumpWall`: the
  face turned away, `WALL_JUMP` at vy 62 (1 if the roof is in reach) and 24 away (`FencePunchSound` plays `MSD_SE_MA_FENCE_PUNCH` here
  too, as the game does). **B** = `FENCE_PUNCH`. The clip is picked from the displacement a tick: `|v| > 5 |h|` -> `ma_fence_move_up` /
  `_down`, else `ma_fence_move_l` / `_r` (his left = up x face), at **rate = the distance a tick**; the stick at rest is `ma_fence_wait`.
  No fatigue timer on a fence.
- **`FENCE_PUNCH`** (`fencePunch`, `ma_fence_punch`): A still jumps off; the fence gone -> `LANDING`; the hit lands at clip frame 5 with
  radius 250 / height 200 (`attackfencepunch.prm`): the fence body's `sms_fence_punched(mario)` is called if it has one (the game sends
  `receiveMessage(3)` to the fence's actor); the clip's end is `FENCE_MOVE`. FLUDD on the fence is the animation table's business (pass
  46): `fence_wait` / `move_l` / `move_r` / `move_up` pump with `ma_fepmp`, `move_down` with `ma_hgpmp`, the catches and the punch not at all.
- **The roof hang.** `jumpProcess` roofCode 4 (`doHorizontalPhysics`): a roof plane that is a fence met by his head (160 + y > roofY)
  while rising or level, from a jump or the hover, hands empty -> `HANG_ROOF` (`ma_ladder_hang_catch`): the feet **160 units under the
  roof**, the stick honoured after **120 ticks** (or the clip's end) -> `WAIT_ROOF` (`ma_ladder_hang_wait_l` / `_r`, the hand by
  `mStatusArg`). **The stick** -> `MOVE_ROOF`: `mForwardVel + 1` a tick to **4.0**, the face toward the stick by **0x800 BAM** a tick,
  the step `-vel x the roof normal's y` (horizontal only); `hangingCheckRoof` at the new spot: a fence WALL within 50 ahead ->
  `FENCE_CATCH` (hand-over-hand onto a vertical fence); no roof there, or not a fence, or the roof slipped **30 units** above the head ->
  `LANDING`; the gap under 160 or the head 30 past the roof -> he stays (moved); in range -> pinned. The clip (`ma_ladder_hang_move_l` /
  `_r`) at **rate = the distance a tick x 0.3** (`hangroof.prm AnmMult`), the hand swapping as each clip ends; the stick at rest ->
  `WAIT_ROOF`. **B** lets go (`LANDING`, the face kept). **A** = `KICK_ROOF` (`ma_ladder_hang_kick`, radius 200 / height 250,
  `attackkickroof.prm`; the roof body's `sms_roof_kicked(mario)` if it has one) -> `WAIT_ROOF`.
- Not ported: the Boo net (`mActorType 0x4000006A`: the punch clamps him into the object and flips him to the far side, the kick rolls him
  up onto the net -- `KICK_ROOF_ROLL_UP` / `_DOWN`), moving fences, letting go of a ledge whose wall is a fence onto the fence (hanging(),
  -40 / -160), the fence camera kinds (`kindfence` = `kindhang`), the rumbles, the voices, the wall-kick dust of the jump-off.
Debug: `[SMS fence] FENCE_CATCH from the run on 'Fence', normal (...)`, `... caught: FENCE_MOVE`, `... the top is 149 units above the
feet: ASCEND`, `... B: FENCE_PUNCH`, `... punch lands (frame 5)`, `... A (on the fence): startJumpWall`, `... let go (...)`;
`[SMS roof] HANG_ROOF from singleJump under 'Roof': the head 160 units under the roof`, `... caught: WAIT_ROOF`, `... A: KICK_ROOF`,
`... let go (B): LANDING`.

### Swimming (`MarioSwim.cpp`, `MarioState.swimming`)

**Water in the level.** The game reads the surface from the map's water triangles; the plaza rip has no water geometry at
all, so the level marks it: an `Area3D` in the group **`sms_water`** (or with metadata `sms_water`) whose shape contains the
water, from the seabed up to the surface. Its surface is metadata `sms_water_surface_y` when set, else the **top of its first
`BoxShape3D`** child. Keep the area's Monitorable on (default). Several areas may overlap; the highest surface wins. Mario
tests the point at his feet every physics frame (`SmsWaterCheck`, `PhysicsPointQueryParameters3D` against areas) and rays
down for the ground under the water (`mFloorPosition.y`).

**Getting in** (`playerControl`): the surface at or above the feet is `MARIO_FLAG_IN_WATER`. His feet more than
`SwimDepth` **110 units (1.48 m)** under the surface → `SWIM_START` from any status (ours excludes dead, talking, the shine-get and the
ledge states), with `fwd *= 0.3` and `vy *= 2.0` (the plunge). Shallower is **wading**: the run continues, braked by depth
(`isRunningInWater`: `fwd *= 1 − depth/110 × 0.05` a tick).

**In the water** (`swimMain` → the status handler → `doSwimming`, every tick):
- **A** (`checkSwimJump`): with the feet within `CanJumpDepth` 100 of the surface, and the stick neutral, or a wall ahead, or
  the stick more than 120° from his face → a normal **jump out** (`JUMP`). Otherwise stick neutral → `SWIM_UP` (`ma_float`,
  `vy += 8` a tick), stick held → `SWIM_PADDLE_START` (`ma_paddle_start`, `fwd += 0.45`, `vy += 4` a tick, then `SWIM_PADDLE`).
- **The float limit**: the feet never rise above `surface − FloatHeight 80` (he floats chest-deep, feet 1.07 m under).
- **`doSwimming`**: **B** → `SWIM_DIVE` (`ma_swim_dive`; once the clip passes frame 16, `WaitSinkTime` 150 ticks of
  `vy −= 8`, but only while the dive clip plays; clip end → `PADDLE_END`). The clip is 36 frames = 0.6 s, so the sink
  really lasts frames 16..36 = 0.33 s = 40 ticks: about 1.25 m below his float depth, back at the surface 1.8 s after
  the press. `changePlayerStatus(.., false)` is a no-op for the status he is already in, so B while diving and A during
  the stroke's start or the float-up do nothing (no restart, no extra boost). Water shallower than `EndDepth` 80 under him → `WAIT`: he stands up. `fwd += mag × 0.3
  / 32`, `fwd *= 0.97`; the face turns to the stick at 300 BAM a tick; `vy −= 0.05`, `vy += clamp((surface − feet)/80) × 2.0`
  (waiting / starting) or `× 1.8` (moving), `vy *= 0.6`; a wall zeroes `fwd` and `vy`; the feet stay 35 above the floor.
- **The handlers**: `START` (`ma_swim_start` → `WAIT`), `WAIT` (`ma_swim_wait`; stick → `WAIT_TO_PADDLE`), `WAIT_TO_PADDLE`
  (`ma_wait_to_paddle` → `PADDLE_START`), `PADDLE` (`ma_swim` at rate 0.5; stick neutral → `PADDLE_END`), `PADDLE_END`
  (`ma_paddle_end` → `PADDLE_END_TO_WAIT`), `PADDLE_END_TO_WAIT` (`ma_paddle_to_wait` → `WAIT`), `UP` (→ `WAIT`), `DIVE`.
  Steady paddling is ≈10 units (4 m/s); a stroke bursts to ≈25 (10 m/s). Mario cannot stay under on his own: buoyancy brings
  him back up, so there is no air meter here.

**Standing up and wading.** `WAIT` out of the water puts him on the floor at once (`waitProcess`: `mPosition.y =
mFloorPosition.y`; `checkGroundAtWalking` snaps anything within 100 units), though he floated 35 units above it: no
drop, no landing. Wading (`isRunningInWater`: the surface above his feet and less than 110 units deep) brakes the run
`fwd *= 1 − depth/110 × 0.05` a tick (flat out ≈ 7.7 m/s in 1 m of water, 6.3 m/s in 1.4 m) and slows the run animation
by the same factor × `InWaterAnmBrake` 0.8. Running on into water deeper than 110 units is the plunge into `SWIM_START`
(`fwd × 0.3`, `vy × 2`). The A-jump's "wall ahead" is `mWallPlane`: any wall within his wall-check radius, whether or
not he is pushing into it.

Port notes: **SMS animation frame numbers are 60ths of a second** (the BCKs are authored at 60 fps: `SMSGetAnmFrameRate()`
= 60 / 30 = 2 frames per game frame; the port's clips are real time), so `checkPass(16)` is 16/60 s into the clip, not
16/30 (the first swim build had /30 and the dive barely dipped: 98 units deep instead of 173). All clips are driven
directly (none is a tree state; `ma_swim_wait` and `ma_swim` force-looped), `SetMarioState` at the exit brings the tree
back. The stand-up calls `ApplyFloorSnap()` itself (Godot's snap needs a grounded previous frame, and the swimmer's snap
length is 0); the A-jump's wall is `IsOnWall()` or a 0.75 m ray along the face at waist height (fwd is zeroed on the
wall, so `IsOnWall()` alone is off again the next frame). While swimming the floor snap is 0, the ground step is off, `PostMoveWallCheck` and the
landing latch skip him, and the swim tick runs in the pre-split block like the knockdown (no other handler). Leaving the
water volume's edge while swimming drops him into a fall (or a stand if there is ground). Not ported: `SWIM_P_DAMAGE` /
`SWIM_DOWN` / `SWIM_P_DOWN` (drowning, damage in water), the FLUDD paddle (`DashMax` surf), the fence catch from water, the
splash / ripple / bubble effects and sounds, the swimming camera, the air meter.
Debug: `[SMS swim] wading in: … m of water` / `wading out`, `[SMS swim] in: surface y …, feet N units under the surface, floor … m down, fwd … vy …`, `[SMS swim] <status>: fwd …`,
`[SMS swim] A near the surface: jump out`, `[SMS swim] out (shallow | left the water) -> idle, deepest … m`.

### Air, drowning, being hit in the water (`TMario::mAir`, `SWIM_P_DAMAGE`, `SWIM_DOWN`)

`mMaxAir = (HP + 1) − 1e-5` (8.99999), full at the start. **Under water** = the head joint more than `CanBreathDepth` **50
units (0.67 m)** below the surface (`isUnderWater`): floating keeps the head out, the dive puts it under. Every tick under,
`air −= AirDec 0.002`; each time the whole part drops (every 500 ticks, 4.2 s) the game rumbles and gives 50 ticks of
invincibility (the flash). `air < 1` → `air = 0`, `loserExec()`, `SWIM_DOWN`: **about 33 s of continuous submersion drowns
him.** Out of the water `air += AirInc 0.03` a tick (empty to full in 2.5 s). `decHP()` while under takes **air instead of
HP** (`incHP`, i.e. coins, gives air back — not wired here). `loserExec()` while swimming: no air → `SWIM_DOWN` (`ma_swim_down`,
drowned), else `SWIM_P_DOWN` (`ma_swim_p_down`, killed in the water); both `doSwimming()` + `jumpingDemoCommon(.., 0)`: speed
0, the clip holds its last frame, buoyancy floats him back up, then the death demo — here **R respawns**, as on land.

**Hit while swimming** (`damageExec`): face the attacker (+180 from behind), the knockdown status for a tick, then the water
check turns it into `SWIM_P_DAMAGE` (`swimPDamage`: `doSwimming()` then speed 0, `ma_swim_p_damage`, `SWIM_WAIT` at the last
frame); the attacker's invincibility; HP or air as above. Type 3 (the upper-body damage run) only takes HP.

**HUD** (`GCConsole2::updateLifeMeterState`): the life meter switches to **air** 240 ticks (2 s) after he goes under, showing
`(s16)air` segments (8 at 8.99), coloured J2D white `0x00FFFF` / black `0x003CFF` (cyan over blue), white `0x0010FF` at 3 or
less; surfacing puts HP back (hidden when full). Port: `LifeMeter.ShowAir / SetAir / EndAir`, the blue as a runtime
`canvas_item` shader (no scene change) remapping the yellow/orange art's **luminance** through a ramp sampled from
`ref/WaterLifeMeter.png`: navy outline `#0A1F70` → azure body `#08C8F8` → highlight `#60F8FF`, the LIFE text and the paint
blob included; at 3 units or less the body shifts toward the game's `0x0010FF`. (Keying on the art's value, as the first
build did, gave one flat cyan: the port's art is 0.99 in value everywhere but its outlines.) Not ported: the low-air
blink rates and beeps (the meter keeps its own pulse), the bubbles from the mouth, the helmet's `AirDecDive`.
Debug: `[SMS air] under: head … m below the surface, N air`, `[SMS air] N left after … s under`, `[SMS air] surfaced after …`,
`[SMS air] meter: air, N segments` / `meter: back to HP N`, `[SMS air] DROWNED after … s under; R respawns`,
`[SMS damage] hit under water: -N AIR` / `hit in the water: -N HP`.

### FLUDD (`TWaterGun`, `Watergun.cpp`; `MarioUpper.cpp`) — S1: the pack

The port had no FLUDD; the rips do: `models/Koopa/mario/watergun2/` has the tank (`wg_mdl1`: joints `nozzle_center`,
`waterline`, the handles; clips `wg_pump`, `wg_house`, `wg_hgpmp`, `wg_sqpmp`, `wg_fepmp`, `wg_rspmp`), the nozzles
(`normal_wg`, `hover_wg`, `rocket_wg`, `back_wg` = the turbo, `yoshi_wg`, `dummy_wg`) each with its `null_G_muzzle` emitter
(the hover has two) and `_change_start/end`, `_shooting`, `_shoot_start/end` clips, and Mario's `ma_pump`, `ma_turbo_dash`.
**The game's numbers** (shipped prms; the whole reading in the repo notes): tanks spray/hover 10000, rocket/turbo 2100; a
drop costs 1 (rocket 35); in water the tank refills `150 × R × SuckRate` a tick (spray 1.0, hover 0.5, rocket 0.2, turbo 0.1);
a hit costs 250 (hover 0); a nozzle swap keeps the fill fraction. The trigger nozzles charge `+150 × R − bleed` a tick to a
max, then spray for a set time, then are dead until R is released: hover bleed 0 / max 1 / 360 ticks (instant, 3 s), rocket
105 / 8000 / 5 ticks (1.5 s of charge at full R, then 5 ticks of `vy += 35` = +175), turbo 100 / 4500 / endless. The hover's
lift is Mario's own spring (`vy = (pressY + 230 − y) × 0.02`, `fwd × 0.985`, `ROCKET` status); the turbo's speed is Mario's
dash (`mDashSpeed` ramps 0.5 a tick to 32 replacing the stick, then the run is forced to 80).

**S1 (this commit).** The gun's base matrix is the `chn_chest` animated matrix (`calcAnim`: `setBaseTRMtx(getAnmMtx(
mJointIdChnChest))`, with a small roll tweak from the chest tilt — not ported yet); the nozzle hangs off the tank's
`nozzle_center`. Port: `BoneAttachment3D` "SmsFluddAttach" on `chn_chest` carrying `wg_mdl1.glb` (the skeleton's scale),
"SmsNozzleAttach" on the tank's `nozzle_center` carrying the four nozzle glbs, the current one visible.
**The bone (from the game's own files, his `aSms/` extraction).** The decomp hangs the gun on `mJointIdChnChest =
getIndex("chn_chest")` and tilts it in `setBaseTRMtx`: `angle = −0x1800 × (1 − |mtx[1][0]|)`, `mtx[1][0]` being the
world-vertical component of the joint's X axis, then `MTXConcat(anmMtx, RotZ(angle))`. But `chn_chest` is a fixed (0, 90°, 0)
joint in every BCK (`ma_wait.bck`, `ma_pump.bck` read; the rip's 200 clips agree): X = his back, never rotating — it is the
root the upper-body clips are grafted on (`M3UModelMario`'s second animation root), not the spine. On it the formula is a
constant −33.75° and, applied to the tank's real vertices (height along +X, `nozzle_center` at (72, −40, 0), the water
shell a 36-unit cylinder of radius 19 along X — all confirmed against the raw BMD), puts the pack from below his feet to his
shoulders with the nozzle at y = −15. On the spine bone `jnt_chest` (X up the spine, Y forward, Z left, leaning with the
chest: rotZ 99° idle, up to 28° forward in the jump) the same formula reads as designed: no tilt while upright, the tank
pitched 33.75° back off his belly when he lies flat (`unk1D04` 0 at |X.y| = 1, `unk1D06` −0x1800 at 0), and the tank's own
frame (X height, Y toward the straps, Z its left) is that bone frame, so the base is the identity plus the tilt. Port: the
attachment is on `jnt_chest`, `upness = |bone X · world up|`, tank basis = RotZ(−0x1800 × (1 − upness)). Recorded as a
reading discrepancy with the decomp's joint name; the test decides. The nozzle glbs are Y-up and `nozzle_center`'s frame has
Y = tank up, so they keep the identity.
**Colour (MAT3 of the BMDs).** The nozzles' texture 0 is `H_watergun_main_dummy` (8×8), a placeholder the game swaps for
the FLUDD atlas `H_watergun_main` (= the tank's 64×128 `wg_mdl1_0.png`); their UVs span that atlas, the rip kept the dummy
→ white. Port: the nozzles get the tank frame's imported material. The tank shell `_mat_G_main_tank` is XLU with no
texture: colour and alpha from the vertex colours ((0.75, 0.89, 0.98) × 0.5–0.8), blend (ONE, INVSRCALPHA), z update
off — Godot imported it opaque light blue. Port: transparency alpha, vertex colours as albedo, premultiplied blend, no
depth write. `_mat_waterline`: XLU (SRCALPHA, ONE) = additive, material colour (136, 129, 156), the water texture
`wg_mdl1_1.png` (`H_watergun_water`) scrolled by the fill (S2). The frame (`_mat_G_main`, `_mat_froater` ×3) keeps the
rip's import (atlas + toon `H_toon_2_4i`; TEV C0 (194,178,206,161), C1 (157,161,169), konst (76,108,97) not reproduced).
- **X** (`gunExec` → `changeBackup`): the swap tween runs 0 → 1 (to the second nozzle, the hover by default) or 1 → 0 (back to
  the spray) at `ChangeSpeed` 0.1 a tick; crossing 0.5 swaps the model. The leaving nozzle scrubs its `_change_start` by the
  tween, the arriving one its `_change_end` (the spray nozzle uses its `house_` variants while housed).
- **R** (analog; `checkPumping` / `stateMachineUpper`): R > 0 → `PUMPING`; released → `HOLDING_PUMP` for `PumpWaitTime` 5
  ticks → `IDLE`; `checkPumpEnable`: nothing carried, not switching nozzles, the clip allows it (ours: not hurt / dead /
  talking / shine / the ledge climbs). The upper body plays the pump clip (`addUpper`, ping-pong) over the legs: the tree's
  upper-body Blend2 (`CarryBlend`) with its `Animation 2` input pointed at `ma_pump` while pumping (`ma_carry_p` when carrying).
- **The tank** (`calcAnimation`): pumping / holding deploys it 0.1 a tick (`wg_house` scrubbed by the progress), then plays
  `wg_pump` (`wg_hgpmp` hanging) and resets a 360-tick hold; idle past the hold houses it again 0.005 a tick.
- **The nozzle clips** (the BCK index order is the rip's alphabetical order): trigger nozzles `shoot_start` once → `shooting`
  while spraying → `shoot_end` held; the spray nozzle `normal_wg_house` by the stow, `normal_wg_shooting` while spraying (its
  shoot_start / shoot_end are 2 of the game's 8 clips the rip lacks; `wg_swpmp` is missing too → `wg_pump`).
- S1 has no water, no spray, no hover / rocket / turbo: "spraying" for the clips = pumping with R down and the pack deployed.
Debug: `[SMS fludd] pack on chn_chest: …` (setup), `upper Pumping (R 0.73), nozzle Hover, housed 0.00`, `X: -> Hover`,
`nozzle -> Hover`, `deployed` / `housed`.

**S2 — the tank and the spray** (`gunExec` / `TWaterGun::suck` / `emit` / `damage`, `TNozzleDeform`, `TNozzleTrigger`,
`TModelWaterManager`; files `assets/Sms/SmsWaterDrops.cs` (new), `Mario.cs`, `SmsPhysics.cs`). A correction first: the spray
nozzle is `TNozzleDeform("normal_wg", "/Mario/WaterGun/NozzleDeform.prm")`, so its numbers are **nozzledeform.prm** (the S1
table above quoted nozzlenormal.prm, which nothing loads): tank 10000, 1 water a drop, refill `0.6 × pressure` a tick, 250 a
hit, **0.8 drops a tick** at full strength (1.0 at none), launch speed `40 × strength` (units a tick), a tight jet (dir tremble
0.02) that opens to 0.8 as the strength dies, drop size 50 (1..50 by the pressure), and **no recoil** at all (ReactionPow and
ReactionY are 0 — the 0.043 lift in the S1 table belongs to the unused prm). `SmsParams.Fludd` now carries every nozzle as a
`Nozzle` record (spray, burst, hover, rocket, turbo) and the water types (`WTP00` spray: gravity −0.4 a tick², alive 255;
`WTP02` burst −0.2; `WTP15` hover −1.0; `WTP03` −10; `WTP13` turbo 0, alive 3).
- **Strength** (`TNozzleBase::movement`, per tick): `var1 = 256 × R × 150`; a u16 ramp chases it by `TriggerRate` 50 a tick;
  `strength = (var1 − ramp) / 65536 × EmitPowScale 10`, clamped 1. So a full hold is a full jet for 5.3 s, fades over 1.1 s,
  then nothing until R is let off (the ramp snaps down to `var1` the moment R drops below it) — the game's "pump R again".
- **gunExec()** per tick: pressure `R × 150`; the nozzle's movement; **in water** `suck()` (+`pressure × SuckRate`, capped) and
  the emit only with a full tank while PUMPING (then the tank is reset to full — free spraying); **out of water** the emit.
  `TWaterGun::emit()`: nothing while the pack is housed (`unk1CEC > 0`) or the muzzle is under the water surface (+20 units).
- **The emit** (`TNozzleDeform::emit`): `count += s × (Num − NumMin) + NumMin` (the fraction carries); pow `s × 40`; dir tremble
  `s × (0.02 − 0.8) + 0.8`; size by the pressure lerp (`(s − 0) / 0.8`, clamped — the decomp's `(min − s)/(max − s)` is
  negative for 0 < s < 0.8 and is read as the plain lerp); the tank −1 per drop spawned; the recoil lines are ported but the
  spray's are 0. Emit position and direction = the muzzle joint `null_G_muzzle`'s animated world matrix, its +X column, read
  from the nozzle glb's skeleton (the shooting clip points the barrel level in the nozzle's frame; the aim is 0, see S2b).
  Mario's own velocity × 0.125 rides on the drops (`getEmitPosDirSpeed`).
- **The burst** (`TNozzleDeform::mBomb`, nozzledeformbomb.prm, `unk384 = true`): **A pressed while R is CLICKED** (`mInput & R`
  is the digital trigger meaning) with the burst INACTIVE → ACTIVE for 1 tick → 127 drops (2 water each, size 80 ± 30, cone
  0.6, pow 35, type 2) → DEAD until R is released; while it is not INACTIVE the normal spray does not emit. The port needs an
  input action **`button_rclick`** for the trigger click (none exists yet → the burst is off; the setup line says so).
- **The trigger machine** (`TNozzleTrigger::movement` / `emit`): charge `+150 × R` a tick while pumping (not while refilling),
  `− TriggerDec`, capped at `TriggerMax` → ACTIVE for `TriggerTime` ticks (→ DEAD when the time is up or the pump stops; DEAD →
  INACTIVE when R reads 0); while ACTIVE `pressure = fill / max` scales the drop count (`Num`/`NumMin`), the launch speed
  (`EmitPow`/`EmitPowMin`), the recoil. In with S2: **the hover nozzle sprays** down from both `null_G_muzzle` joints for 3 s
  when R is held (2 water a tick, `WTP15` drops, horizontal recoil `0.005 × pow × (−dir · face)` ≈ 0 for a downward jet); the
  lift is S3. Rocket / turbo are not selectable yet.
- **The drops** (`SmsWaterDrops`, `TModelWaterManager::move` per tick): `vel.y += gravity`; horizontal speed capped to the drop's
  size a tick (no tunnelling); `pos += vel`; alive 255 ticks; culled 40 m from Mario. The manager sits in the scene's perform
  list (the `.bin` scene data is not in the decomp); it is taken to move every tick like Mario (**assumption**: if the stream
  looks 4× too fast, the manager runs once a frame and pow / gravity are per frame). A step that meets geometry lands the
  drop: it sits, shrinks and dries over 40 ticks (a stand-in for the game's sliding case 2 / `getPlaneVanishSpeed`). Drawn as
  one `MultiMesh` of spheres, diameter `size × Magnify`, stretched along the flight by `(1 + Extension)` so successive drops
  read as a stream (the game draws a silhouette volume). Not wired: hits on enemies / goop (`HIT_MESSAGE_SPRAYED_BY_WATER`;
  a debug line names what a drop hit), splashes, sound.
- **The tank's level**: the shell `_mat_G_main_tank` is a cylinder along the tank's X (x −3.0..33.3, r 18.9 about y −40);
  the game shows the level on its `waterline` quad by a scrolled water texture (a BTK the rip lacks), so the port fills the
  shell with a blue cylinder cut at the fill (`SmsTankWater`) and hides the rip's quad.
- **damageExec()**: for each of the attacker's `mWaterEmit`: `−DamageLoss` water and a 20-drop dribble from the muzzle
  (damagewateremit.prm: no speed, size 17). `dmgenemycommon.prm` has **mWaterEmit 0** — the Gooble spills nothing; hinokuri
  (the fire Stu) 2. `SmsDamageProfile` gained `WaterEmit`. `changeNozzle()` keeps the fill **fraction** (10000 ↔ 10000 for
  the hover, so no visible change until the rocket / turbo come). A respawn refills the tank.
- **S2b, not in**: the squat aim (R clicked: `canSquat` → `SQUAT`, stick y × `RButtonMult` 10000 a tick chased 0.018, clamped
  −13000..8000 BAM; negative bends the **nozzle** up (`NozzleCtrl`, `RotZ` on `chn_muzzle_1`, only for < 0; the head turns by
  half), positive bends **Mario's waist** forward (`MarioWaistCtrl`, only for > 0)), the L-camera aim (`unk36E` = the camera
  pitch), the spray meter (`unk1C88`) and a HUD gauge, the empty-tank pump rate (`PumpAnmSpeed`), sounds, rumble.
Debug: `[SMS fludd] water 9500/10000 (Spray), drops 41` (every 5 %, and at empty / full), `spray strength 1.00 (R 0.98, ramp
1250), stream -3 deg from level, housed 0.00` (on each quarter of strength), `hover: charged -> ACTIVE for 360 ticks`,
`[SMS water] a drop hit Gooble (CharacterBody3D) -- spray hits are not wired to anything yet`, `hit: -N x 250 water`.
Next: S2b the squat aim (needs `button_rclick`), S4 rocket, S5 turbo (surf).

**S3 — the hover** (`rocketCheck` / `rocketing`, MarioJump.cpp; `hhover.prm`; `MarioState.fluddHover`; files `Mario.cs`,
`SmsPhysics.cs` (`SmsHover`), `assets/Sms/SmsBoneRotZ.cs` (new)). One status, `MARIO_STATUS_ROCKET`, serves the hover and the
rocket nozzle; with the hover it is a **spring**, not a thrust.
- **Entry** (`rocketCheck()`): the hover nozzle (`RocketType` 1), R down (`UPPER_STATE_PUMPING`) and the nozzle spraying
  (`canSpray()`: water, and the trigger ACTIVE — instant for the hover) → `ROCKET` with the target `unk314 = feet + HoverHeight`
  (230 units = 3.09 m). Who checks: every jump (`jumpingCommonEvents`: JUMP, SECOND_JUMP, U_TURN_JUMP, TRAMPLE; `ultraJumping`,
  `backJumping`, `jumpWall`, `rotateJumping`), the walk-off fall (`landing()` for LANDING), `running()`, `waiting()`,
  `squating()` (when the nozzle emits). Not the dives, the broad jumps, the rolls, the slides, the hang, the water. **One hover
  per airtime:** `rocketCheck()` returns 0 while the status is `ROCKET` or **`ROCKET_LANDING`**, and the fall after a hover keeps
  `ROCKET_LANDING` through wall stops until the landing, a ledge grab (`HANGING`; a jump from the hang may hover again), a
  knockdown or the water change it. The nozzle itself re-arms the moment R reads 0 (`TNozzleTrigger::movement`), so it is
  Mario's status, not the nozzle, that rations the hover (first shipped as "a re-press re-enters"; Jaysonn's test caught it).
  `setStatusToJumping(ROCKET)` writes `mVel.y = 10`, but `rocketing()` runs
  in the same tick (`stateMachine` loops on a status change) and assigns the spring before the move, so the 10 never moves
  him: a hover taken at the top of a jump drops the jump's rise and keeps its forward speed.
- **Each tick** (`rocketing()`): `vy = (target − y) × AccelRate 0.02` — **assigned**, so `fallProcess`'s gravity after the move is
  overwritten before the next one and never shows; `fwd × Brake 0.985`; `vel.xz = fwd` along the face. With the move at
  `0.25 × vel` a tick the gap closes 0.5 % a tick: 45 % a second, **192 of the 230 units in the 3 s window** (104 after 1 s),
  1.85 m/s at the start. **Stick:** within 30° of the face (`0x1555`) or beyond 150° (`0x6AAA`) it pushes along the face by
  `mag × cos × DivingParams.AccelControl` a tick — **shipped 0.01** (`/Mario/Diving.prm`; the code default 0.02 is what the S3 port carried until 2026-09-27, doubling the push: 21.3 units settled with the stick held, 8.6 m/s, not 42.7) with **no turn**; between, the face turns toward it at `RotSp` 200 BAM a
  tick (1.1°) with **no push** — to move sideways you turn first. No A, no B (`rocketing()` has neither). `ANIM_LAND` with the
  pump on the upper body; the head turns `HoverHeadAngle` 5294 (29°) **down** while he can spray (MarioDraw.cpp; upperbody.prm's
  FeelDeep +8192 looks down a cliff, FrontWall −8192 up a wall, so positive is down) — on the run's head-lock modifier, eased.
- **The floor and walls** (`jumpProcess(2)` in `rocketing()`): case 1 (the floor) and case 2 (a wall) `break` — **he does not land
  while R is held**; skimming over higher ground the spring lifts him off again, a wall just stops him. A roof (`checkGround
  AtJumping`'s roof branch) clamps him under it, `setPlayerVelocity(0)`, `vy` 0: stuck pressing on it until R lifts (ported:
  `IsOnCeiling` → fwd 0). Case 4 (a fence roof) → `HANG_ROOF`: not ported. No ledge bit (`jumpProcess(2)`, not 3): no ledge
  grab while hovering — the grab is for the fall after.
- **Exits** (checked first, before the move): R lifted (not PUMPING), the 360-tick window closed (the trigger DEAD → `canSpray`
  false), the tank empty, the nozzle swapped (X → `checkPumpEnable` false while switching, then `RocketType` 0) → `ROCKET_
  LANDING` = `landing()`: the plain fall (`jumpingBasic(LAND_SLIP, ANIM_LAND, 3)`: air control, the ledge bit, B dives) from
  the last tick's velocity (the spring's `vy` less one tick of gravity). Port: `ledgeFall` flagged `_smsRocketLanding`; on the
  floor at that instant he lands at once (case 1). One rule differs from a walk-off fall: `jumpingBasic` case 2 reads
  `mForwardVel > 16 && mStatus != ROCKET_LANDING`, so after a hover a wall at 16..40 **neither wall-slides nor safe-downs** —
  he stops against it and keeps falling (the knockdown above 40 still applies). Water: `SWIM_START` from any status, as before.
- **The nozzles bank** (`TWaterGun::movement`, `WaterGunDivingCtrlL / R`): `rocketing()` asks `unk1CC2 / unk1CC4` = `0.03125 ×
  −mag × SideAngleMax × cos | sin` (hover: Side 5000 = 27° opposite ways on a sideways stick, Front 0, Back 8000 = 44° together
  when pulled back), `unk1CC8 / unk1CCC` chase them by `ChangeSpeed` 0.1 a tick (0 when not spraying), and the joint callbacks
  concatenate `RotZ(−unk1CC8)` / `RotZ(−unk1CCC)` onto `jnt_nozzle_L / R` after the clip. Port: `SmsBoneRotZ`, a
  `SkeletonModifier3D` child of the hover nozzle's skeleton (the rip's `hover_wg.glb`: `jnt_hover_neck` → `jnt_nozzle_L`
  (`null_G_muzzle`), `jnt_nozzle_R` (`null_G_muzzle2`)); the emit follows the banked muzzles as in the game. The handedness of
  the bank is the port's stick convention (`SmsAir.Tick`'s `sin(d)` = left); if it reads mirrored, flip one sign.
- **Water**: `TWaterGun::emit()` calls the nozzle's emit once per emitter, so the hover's two muzzles cost **2 a tick** (the S3
  plan table said 1; S2 had it right): 5000 ticks = 41.7 s of hovering in a full tank, about 14 full windows. No recoil
  (`ReactionPow` 0.005 × `(−dir · face)` = 0 for a jet straight down, `ReactionY` 0).
- Not ported: the hover rumble (every 8 ticks), the voices and `SE_PO_HOVER`, `HANG_ROOF`, the camera's hover mode, the
  `MARIO_STATUS_FLAG_UNK8000` 2.5× stick magnitude in `doJumping` (rocketing() has its own stick code and never calls it).
- Playtest 1 (Jaysonn): the rise matches the model to the unit (+1.45 m at 1 s, 2.60 m at 3 s from the ground = 194 units;
  192 from a jump); "feels really good". Fixed from it: the mid-air re-hover above, and the spin jump's ring / particles
  staying on through a hover (the main air path switches `isSpining` every frame; the hover and the swim entries now switch
  it off like the hang, the stomp and the bonk do). Pulling the stick back flies him backwards fast (`cos` −1 at the dive
  rate against the 0.985 brake settles at ±42 units = 16.9 m/s either way, faster than the run's 32): the game's numbers.
  **Wrong — see "the hover's stick" under the waist's third look (2026-09-27): the shipped `Diving.prm` rate is 0.01, the
  settled speed ±21.3 units (8.6 m/s); this build pushed twice the game.**
- Playtest 2: "no upward motion now, but he still uses FLUDD after the hover expires; in the game you can't until you land,
  unless you switch nozzles". `checkPumpEnable()` (MarioUpper.cpp) has two clauses S1 left out: `mStatus != ROCKET_LANDING ||
  !checkCurrentNozzleRocketType(TWaterGun::Rocket)` — `TWaterGun::Rocket` is the enum value **1** and the check compares the
  nozzle's **`mRocketType` param** (the fabricated name misleads): the hover and the rocket ship 1, the spray / burst / turbo 0.
  So in `ROCKET_LANDING` a RocketType-1 nozzle cannot pump (`mUpperState = IDLE`): the trigger never charges, nothing sprays,
  no water goes, the arms drop, the pack houses after its hold — and X to the spray frees the pump, as he remembered. The
  other clause, `!checkCurrentNozzleKind(trigger) || state != DEAD`: while a trigger nozzle's window has closed (DEAD, R still
  down) the pump is off too — the arms relax the moment the hover sputters out, and only R at 0 re-enables them. Both ported
  in `SmsFluddPumpEnabled` (`Nozzle.RocketType` added). The exit line now names the window before the pump: `end (the 3 s ran
  out)`.
- **Pumping while swimming** (Jaysonn: "he should hold the handles while still doing the swim animation"; `addUpper`,
  MarioDraw.cpp). PUMPING / HOLDING_PUMP graft `gMarioAnimeData[mAnimationId].unk2` — the upper-body pump clip of the current
  body clip — onto the joints from `chn_chest` down (M3UModelMario's second animation root; the rip: `mdl1 > center >
  {chn_chest > jnt_chest > shoulders / arms / hands / head, jnt_waist > legs}`). The table maps every swim body clip to
  **`swpmp`** (the hang to `hgpmp`, the fences to `fepmp`, the rest to `pump`); `swim_dive`, `swim_p_damage`, `swim_down`,
  `swim_p_down` carry 0xC8 = no clip → `isPumpOK()` false → `checkPumpEnable()` forces IDLE: **no pumping in the dive or the
  water knockdowns** (no refill either). The tank already took `wg_swpmp` in water. The tree is off while swimming (the
  swim clips run on the AnimationPlayer), so the CarryBlend cannot layer the pump there; `SmsUpperPose`
  (`assets/Sms/SmsUpperPose.cs`, a `SkeletonModifier3D`, first child of the skeleton so the head jiggle / lock add on top)
  samples `ma_swpmp`'s rotation / position tracks for the 16 bones at and under `chn_chest` and writes them after the body
  clip; Mario ping-pongs its time while PUMPING (`ATTR_PING_PONG_LOOP`), holds it while HOLDING_PUMP, resets it when IDLE.
  Setup line: `[SMS fludd] swim pump: ma_swpmp (0.367 s) on 16 bones under chn_chest`. Not done: the hang's `hgpmp` and the
  other per-clip variants on the tree's clips (the CarryBlend plays `ma_pump` for all of them).

**S4 — the rocket nozzle, and the nozzle key** (`rocketCheck` / `rocketing` again; `TNozzleTrigger::movement` / `emit`;
`TWaterGun::changeNozzle`; `nozzletrgrocket.prm`; `SmsRocketNozzle`). `MarioState.fluddHover` is now **`fluddRocket`**: one
status, `MARIO_STATUS_ROCKET`, the nozzle decides what it does.
- **The charge** (`TNozzleTrigger::movement`, in the gun, not in Mario): `+150 × R − 105` a tick to 8000 → **1.48 s at full R**;
  at R ≤ 0.7 the bleed wins and it never charges (a light press does nothing — squeeze the trigger). Mario keeps whatever he
  was doing (standing, running, jumping) with the pump on the upper body; nothing on the nozzle model moves until it fires
  (its clip machine keys on ACTIVE). Then **5 ACTIVE ticks**: one 35-water drop each (2100 / 175 = **12 launches** a tank) and
  `mVel.y += 35` each (`−dir.y × EmitPow 50 × ReactionY 0.7`, the muzzle pointing down) — into whatever status Mario is in.
- **The status.** `rocketCheck()` takes a RocketType-1 nozzle into `ROCKET` when PUMPING and `canSpray()`; for the rocket that is
  ACTIVE, so it enters the tick **after** the first push, and `setStatusToJumping(ROCKET)` writes `mVel.y = 10` over that push.
  Then per tick `rocketing()`: both its switches have only the Hover case → **no push, no turn, no bank, `mVel.y` left to
  `fallProcess`, `fwd` not braked**; the emit adds 35 after the move: 10 → 44 → 78 → 112 → 146; the sixth movement turns the
  nozzle DEAD (no emit), `rocketing()` sees `!canSpray` → `ROCKET_LANDING` with **145**. From there the plain fall: apex `0.25 ×
  145 × 146 / 2` plus the launch ticks = **2744 units = 36.9 m = 17 Mario heights**, 1.2 s up, and **no cap** anywhere
  (`fallProcess` only clamps −75). Believable: the plaza lighthouse is climbed with one rocket from its base. Port: the emit's
  vertical push (`SmsFluddRecoil`) goes into the status's own vy while in `fluddRocket`; the first ACTIVE tick's push is dropped
  (the game's entry wipes it); a push fired earlier in the same physics frame than the entry waits in `_fluddRocketVyPending`
  and is added to the 10. The self-test checks 145 / 2743.75.
- **The flight.** `ANIM_LAND` for the 5 ticks, then `ROCKET_LANDING`'s `ANIM_LAND` fall with air control, the ledge grab and B;
  `checkPumpEnable` refuses the pump for a RocketType-1 nozzle in `ROCKET_LANDING`, so the arms drop and R does nothing until he
  lands (the same rule as the hover, already in). No rocket pose clip exists in the game's table (`rspmp` in the rip is not
  referenced by `gMarioAnimeData`). The roof: `fwd` 0 and a rising vy 0 (`checkGroundAtJumping`). Not ported: the launch
  voice, `rocketEffectStart` (three particle bursts at the feet and three bound to the muzzle), the charge rumble that ramps
  with the fill, the camera.
- **The nozzle key.** `TWaterGun::changeNozzle(type, animate)` is what a nozzle box does: the fraction of water kept, the second
  nozzle set (not for the spray), the swap progress put at 1 (0 for the spray), the nozzle equipped at once. **N** (keyboard)
  does that for the next nozzle: Spray → Hover → Rocket → Turbo → Spray, so the rocket and the turbo are reachable before any
  boxes exist; X keeps swapping the spray with whatever N picked last. Not mid-swap, not while pumping, hurt or dead. The turbo is
  selectable but its dash is S5: it charges (0.75 s) and sprays only.
Debug: `[SMS fludd] N: nozzle box -> Rocket (X now swaps Spray <-> Rocket); water 2100/2100`, `rocket: charged -> ACTIVE for 5
ticks`, `[SMS rocket] launch from idle: vy 45 (the entry's 10 + a push of 35 already this frame), fwd 0.0 …`, `off (the 5 launch
ticks are done) after 4 ticks: vy 145 from 4 pushes of 35 (apex about 35.6 m up from here), …`, then the fall's `[SMS jump]
ledgeFall: apex …` line on landing. Self-test: `rocket: vy handed to ROCKET_LANDING 145`, `apex above the launch point 2743.75`,
`charge ticks at full R 177.8`.
Next: S5 turbo (the dash, the broad jump, the surf), S2b the squat aim (needs `button_rclick`), the HUD gauge.

**S5 — the turbo nozzle: the dash, the broad jump, the surf** (MarioMove.cpp's input stage 1499-1550, `running()`,
`doRunningAnimation`, `broadJumping`, `swimPaddle` / `doSwimming` / `checkSwimJump`; `mario.prm` DashMax 80 / DashAcc 0.5 /
DashBrake 0.1 / DashStartTime 0; `jump.prm` **BroadJumpForce 60 / BroadJumpForceY 70** — the code defaults 80 / 30 are not
the game; `nozzletrgturbo.prm` bleed 100 / max 4500 → 0.75 s of charge at full R, then ACTIVE while held, 1 water a tick).
- **The dash is not a status.** Every tick, with the turbo nozzle, PUMPING, R > 0 and water: a stick at rest aims where he faces;
  `mDashSpeed += 0.5` to 32 (64 ticks); once at 32 the **`FLUDD_EMITTING`** flag goes on when the trigger is ACTIVE (the charge and
  the ramp run side by side, so the flag arrives at 0.75 s) and stays only while the status is RUN or SWIM_PADDLE; and
  **`mIntendedMag = mDashSpeed` — the dash replaces the stick's magnitude**: a standing Mario starts running by himself, a
  running one first drops to the ramp's magnitude and climbs back. Not pumping: `mDashSpeed × 0.1` a tick to 0 (the magnitude
  follows: the run brakes as if the stick were let go), flag off. Port: `SmsFluddDashTick` in the gun's loop,
  `SmsFluddDashOverride` rewriting the frame's stick (`inputDirWorld`, `stickStrength`, `walkingStrength`, `_smsIntendedMag`).
- **`running()` with the flag**: `setPlayerVelocity(DashMax 80)` at the end of every tick (32.2 m/s; the run's own brake above
  32 is undone each tick — ported as `fwd = 80` after `SmsGroundTick`'s loop), the turn rate `RunningRotSpMin` (shipped min ==
  max 1500: no change), **A with fwd > 79 → BROAD_JUMP** (vy 70, fwd 60, `ANIM_BROAD_JUMP`; the game lands it straight into
  RUN, ours through the usual landing window; not fall-cancelable — `BROAD_JUMP` lacks the 0x2000000 flag `ROTATE_BROAD_JUMP`
  has), a pushing wall clears the flag — but the knockdown above ClashSpeed 40 comes first (`SmsGroundWallBonk`): a dash into
  a wall is `JUMP_SHORT_BACK_DOWN`. `doRunningAnimation` swaps `ANIM_RUN2` for **`ANIM_TURBO_DASH`** at fwd ≥ 79 and back below;
  its rate `fwd × 0.06 + 1` is 5.8 against run2's 2.92 at 32, so the port plays it at twice the full run (**until 2026-09-27 the
  clip itself never showed — see "the dash clip" under the waist's fourth look**). Neither
  `ma_turbo_dash` nor `ma_broad_jump` is a BaseSM state in the scene: `SmsFluddSetupTurboAnims` adds both to the state machine
  in memory at start-up (`Travel` / `Start` teleport to them).
- **The surf** (`swimPaddle` with the flag): the flag coming on in the water → `SWIM_PADDLE` at once (`doSwimming` keeps any other
  swim status out of the way), `ANIM_PADDLE_SWIM` at **5.0** (0.5 without), `setPlayerVelocity(80)` every tick — under water too;
  the wall: on the surface **`JUMP_BACK_DOWN` with fwd × −0.8 and vy 50** (thrown back out of the water — ported on the bonk with
  its back speed raised to 64), under water `SWIM_P_DAMAGE`; `checkSwimJump()`: **A on the surface → y = surface + 1, BROAD_JUMP**.
  `surfingEffect` (the spray) is not ported.
- **The propeller** (`chn_back_nozzle_prop`, `RotateCtrl`): `rotateProp()` accumulates `strength × NozzleAngleYSpeed 100`, × 0.99,
  **capped at HoverRotMax — shipped 0**, so the shipped game never spins it. Nothing to port.
- Not ported: the sounds (`SE_PO_SNIPER_TRIGGER`, `SE_PO_SPREAD`, `SE_MA_SURF_*`), the surf spray, the flag's cosmetic hold through
  the broad jump (`broadJumping` re-sets it each tick; ours turns it off in the air and back on at the landing if he still pumps).
Debug: `[SMS turbo] dash on (from sprinting): 80 a tick = 32.2 m/s, water 2100`, `dash off (R released), water 1960`, `broad jump
(A at dash speed): vy 70, fwd 60 (24.2 m/s)`, `[SMS bonk] the surf into a wall at fwd 80.0 …`; setup `turbo clips: ma_turbo_dash in
BaseSM, ma_broad_jump in BaseSM`. Self-test: `turbo: dash ramp ticks to 32 64`, `charge ticks at full R 90`, `broad jump: apex
621.25`.
That closes the five FLUDD stages. Left on the FLUDD list: S2b the squat aim (needs `button_rclick`), the HUD gauge, the hang's
`hgpmp` upper clip, sounds, particles, rumble, the camera modes.

**S5 fix 1** (Jaysonn's turbo playtest: "feels great"; the dash, the charge and the water cost all read right in the log — one
broad jump, the surf at 80 with the paddle at 5.0):
- **No waist IK while FLUDD is in use** (his call): the run's forward / turn lean (`skeletonIK3DWaist`) is skipped while the upper
  body pumps or holds the pump, and while the turbo dashes or ramps (`SmsFluddBusy`). The game has no waist IK; the pump's upper
  clip owns the chest.
- **"Mario can't double or triple jump in turbo, just spin."** The game: `broadJumping()` lands with `jumpingBasic(MARIO_STATUS_RUN,
  ANIM_BROAD_JUMP, 1)` — straight into RUN, no `JUMP_SLIP` window, so no double / triple off a broad jump; and `changePlayerJumping()`
  asks `considerRotateJumpStart()` before changing to `BROAD_JUMP`, so a spun stick is the spin jump. The port's broad jump was a
  `singleJump` and took the ordinary landing window (`jumpChainStage` 1 — the log shows `doubleJump` / `tripleJump` states with the
  turbo pumping). Now it lands through `EnterRolloutRun` (the port's RUN landing for the rollouts — `ROTATE_BROAD_JUMP → RUN` is
  the same call) with the chain cleared, and the pre-split A check yields to `SmsOrBufferedSpin()`. Still the game, deliberately: a
  spin jump lands in `SEC_JUMP_SLIP`, so A there is the triple; A on the very landing tick of a broad jump (fwd 60 < 79) is the
  ordinary JUMP and opens its window — `jumpSlipEvents()` never looks at the flag.
- **The flag rides through the broad jump** (`broadJumping(): onFlag(FLUDD_EMITTING)` every tick): `SmsDashState` includes the
  broad jump, so the landing tick forces 80 at once, and a broad jump into water is the game's emitting entry (MarioMove.cpp 2119):
  `SWIM_PADDLE`, `mVel.y = 0`, `mPosition.y = the surface` — the surf carries on.
- The log's on / off flip-flop every tick in the air: the game literally does that (`onFlag` + `SE_PO_SNIPER_TRIGGER`, then
  `offFlag` because the status is not RUN / SWIM_PADDLE — **the turbo's stutter in the air is that SE**); nothing is heard here, so
  the print now waits for a flag that survives the tick.
- Read on the way, for S2b: `canSquat()` is false with the turbo nozzle (index 5) and with any RocketType-1 nozzle (hover, rocket):
  the squat aim is the spray nozzle's alone.
Debug: `[SMS turbo] broad jump landed into the run at fwd 60.0, no jump chain, dash still on`.

**S5 fix 2 — the steering with FLUDD, the turbo's nozzle angle** (Jaysonn: "shouldn't be able to cut turns so hard with the spray;
the turbo's spray angle in the water is off; the turbo's steering on land").
- **`doRunning()`'s turn rate** (MarioRun.cpp 492): while `isUpperPumpingStyle()` (the upper body PUMPING or HOLDING_PUMP) it is
  `0.03125 × fwd × (PumpingRotSpMax − Min) + Min` — `mario.prm` shipped **300 / 600** (300 standing, 600 at the run cap; the code
  defaults 0x100 / 0x200 are not the game) against the free run's 1500 / 1500: spraying on the run turns 2.5 to 5 times slower.
  Then `if FLUDD_EMITTING → rotSp = RunningRotSpMin` (1500): **the turbo dash turns at the free run's rate**, whatever the pumping
  says. Port: `SmsRun.PumpingTurnRateRad(fwd)`, `SmsRun.DashTurnRateRad()`, picked in `SmsGroundTick`.
- **`isRunningTurnning()` is false while pumping** (MarioRun.cpp 87): no U-turn — no skid, no `TURN_END`, no U-turn jump — while the
  upper body pumps; a reversed stick is a plain turn at the pumping rate. The port's U-turn (`sideFlipTurning`, and the skid branch
  in `SmsGroundTick`) was reachable from a spray on the run and from the dash — where the skid's brake fought the forced 80 and a
  reversed stick did nothing at all. Both gated on `SmsPumpingStyle()`.
- **The swim** (MarioSwim.cpp 29): `isUpperPumpingStyle()` → `swim.prm` `PumpingRotSp` **200** instead of `SwimmingRotSp` 300: the
  paddle turns slower while pumping (the spray in the water, the surf).
- **The turbo's nozzle angle.** `TNozzleBase::calcGunAngle()` runs every tick from the current nozzle's `movement()` (the trigger
  nozzle's too, after its early return without water): not squatting → `angle = −LAngleBase`, clamped `[LAngleMin, LAngleMax]`,
  chased at `LAngleChase`. `nozzletrgturbo.prm`: **LAngleBase 4096 = 22.5°**, chase 0.1 (the spray's and the hover's are 0).
  `NozzleCtrl`, the callback on **`jnt_back_nozzle_neck`**: a gun angle below 0 concatenates `MsMtxSetRotRPH(0, 0, deg)` after the
  clip — the standard right-hand Rz (MathUtil.cpp 226) about the joint's local Z. Measured on `back_wg.glb`'s shooting pose: −22.5°
  at the neck pitches the muzzle **22.8° down**. So the turbo's head sits tilted 22.5° down whenever it is selected with water (from
  0 after a nozzle change in ~30 ticks), on land and in the water alike — the port shot along the clip's level barrel, 22.5° too
  high, which is the surf's wrong angle. Ported: `_fluddGunAngle` now chased for the trigger nozzles too, applied by a second
  `SmsBoneRotZ` (`SmsTurboNeckAim`) on back_wg's neck. `rocket_wg` has no neck joint (its LAngleBase 4096 moves nothing); the
  spray's callback joint `chn_muzzle_l` is not in `normal_wg` and its base is 0; a gun angle **above** 0 bends the chest instead
  (`MarioWaistCtrl` — the squat aim up, S2b). The emit under the surface + 20 was already refused (WaterGun.cpp 1794).
- **The waist lean, read properly** (fix 1's note said the game has none; the first draft of this bullet said "only without
  FLUDD" — both wrong. The decomp's `checkStatusType(MARIO_FLAG_HAS_FLUDD)` in `MarioWaistCtrl` tests the *status* word for bit
  0x8000 — that bit is the **squat** (SQUAT / SQUAT_START / SQUAT_STANDUP / SQUAT_SLIP), not the has-FLUDD flag; the constant's name
  is a value coincidence.) `considerWaist()` (MarioDraw.cpp 1461) and `MarioWaistCtrl` on the chest joint (470): **the lean applies
  whenever the body animation is RUN1 / RUN2, pack or no pack.** Free: pitch `WaistPitch × fwd` (`bodyanglefree.prm` 230 → 7360 BAM =
  40° forward at the run cap 32; max 12000 = 66°) and roll `WaistRoll × Δface × fwd` (0.5, max 8442 = 46° — a full-rate turn
  saturates it), chased at 0.07 a tick. **While the upper body pumps or holds the pump** (`fabricatedIsPumping() || isHolding()`):
  `bodyanglewatergun.prm` — pitch −50 × fwd capped 2000 = **11° back**, roll capped 4000 = 22°, rate 0.05. **Not during the turbo
  dash** (its body animation is TURBO_DASH, not RUN1 / RUN2), nor in the squat, where the chest bends by a positive gun angle
  instead. So the port's IK lean on the run is the game's in kind (the game's numbers: 40° / 46° caps 66° / 46°, vs the IK's 30°),
  and fix 1's switch-off while pumping stands in for the game's small back-lean — exact for the dash, 11° short for the spray.
Debug: `[SMS turbo] dash on (...): ... jet −20.3 deg from level, −0.98 along the face (neck −4096 BAM)`; setup `turbo neck joint
jnt_back_nozzle_neck yes: the gun angle −4096 BAM = 22.5 deg down`. Self-test: `pumping turn at fwd 32 (BAM) 600`, `at fwd 0 300`,
`dash turn 1500`, `turbo neck: gun angle (deg) −22.5`.

**The waist lean, ported** (Jaysonn: "port the game's waist lean exactly, replacing the IK"). `considerWaist()` (MarioDraw.cpp
1461) runs every frame in every status: pitch target `WaistPitch × fwd`, roll target `WaistRoll × (this frame's yaw change, BAM) ×
fwd` — `unk9C` is the face latched at the top of `playerControl()` (MarioMove.cpp 2443) — each clamped to its Max and chased at the
set's rate. The set: while `fabricatedIsPumping()` (the pack on and the upper body PUMPING or HOLDING_PUMP) or `isHolding()`
(carrying) the pitch takes `bodyanglewatergun.prm`; the roll takes it only while pumping; otherwise `bodyanglefree.prm`:
- free: pitch 230 × fwd (7360 BAM = **40.4° forward at the run cap**, max 12000 = 66°), roll 0.5 × Δyaw × fwd (max 8442 = 46° — a
  1500 BAM/tick turn at 32 asks 24000, so a full-rate turn saturates it), rate 0.07 (95 % of the run lean in 42 ticks = 0.35 s);
- water gun: pitch −50 × fwd, max 2000 (**8.8° back at 32**, 11° cap), roll max 4000 (22°), rate 0.05.
`MarioWaistCtrl` (470), the callback on **`jnt_chest`**: (1) the L-camera look-up bend — not ported; (2) `checkStatusType(0x8000)`,
the **squat**, bends the chest by a positive gun angle — S2b; (3) else while `mAnimationId` is `ANIM_RUN1` or `ANIM_RUN2` (or the
shell ride): `MTXConcat(anmMtx, MsMtxSetRotRPH(deg(roll), 0, deg(pitch)))` — with p = 0 that matrix (MathUtil.cpp 226) is
`Rz(pitch) × Rx(roll)`: about the joint's local Z by the pitch, then about its local X by the roll. Any other animation shows no
lean — the turbo dash (`ANIM_TURBO_DASH`), the sneak, the wait, every jump — while the values keep chasing, so a landing back into
the run shows the lean at once and a stop hides it at once. Port: `SmsWaistLean` (`assets/Sms/SmsWaistLean.cs`, a
`SkeletonModifier3D`, first among the skeleton's modifiers) on `jnt_chest`; `SmsWaistFrame(delta)` in the pre-split block chases
`_smsWaistPitch / _smsWaistRoll` one tick per 1/120 s of the frame with the frame's speed and the previous frame's yaw change;
`SmsWaistApply()` applies while the tree's node is `ma_run1` (walking, running) or `ma_run2` (sprinting, the rollout run). **The
`SkeletonIK3D` waist lean is never started under SMS physics** (the legacy path keeps it, with fix 1's FLUDD switch-off). The pack on
`jnt_chest`, the arms and the head follow the chest, as in the game (`HeadForwardLock` works on the head's local pose, so the order
is safe). Not ported: the Gesso-surfing sets, the shell ride, the L-camera bend.
First run: the pitch reads right, the roll banked him **out** of a turn — the rig's `jnt_chest` local X runs the other way from
the BMD's (a converter artefact; the hover bank on `jnt_nozzle_L / R` is about Z and was unaffected). `_smsWaistRoll` keeps the
game's sign (a left turn is positive) and is negated at the joint (`SmsWaistApply`).

**The waist, second look** (Jaysonn: banks now, but "Mario's head is looking back and forth while running straight"). What the
roll *is*: `jnt_chest`'s local X runs up the spine (`ma_mdl1.glb`: X → world (0, 0.99, 0.15), Z → (1, 0, 0) lateral), so the game's
`Rz(pitch)` pitches the chest and **`Rx(roll)` twists the shoulders about the spine — toward the turn**: the torso leads a sharp
turn; it is not a sideways bank. The target is `0.5 × (this tick's yaw change, BAM) × fwd`: at the run cap every BAM of yaw change
asks 16 BAM of twist, so a 0.5° wobble of the intended yaw asks 8° of shoulders, chased at 0.07 a tick — and the head rides on the
shoulders. Running straight in the game asks nothing: `mIntendedYaw = matan(−mStickV, mStickH)` (MarioMove.cpp 1494) comes from the
pad's s8 stick, the yaw moves in ~0.6° steps and a held thumb reads the same integers frame after frame. The port built the
direction from Godot's float stick vector — the adapter's analog noise and the camera pivot's sub-degree drift went straight into
the face and ×16 into the shoulders. Now the SMS direction comes from `SmsStick.PadVector`, the same truncated, deadzone-shifted
integer pair the magnitude already used: the game's quantised yaw. (Checked on the way: `considerWaist` runs every 120 Hz step —
`TMario::perform` calls `calcAnim` on each `CUE_MOVE` (MarioMain.cpp 98) and the movement list gets `CUE_MOVE` on all four
iterations (MarDirectorDirect.cpp 141); `unk9C` is latched at the top of each `playerControl`, so `diffAngle` is that step's change;
the pad is read on the first step only (105–115). The per-tick chase is right.) The roll's sign: the rig's X reads UP in the glb, which
makes the game's own sign turn the shoulders into the turn — yet he saw "out" with it, so the flip stays until the new setup line
(`jnt_chest local X in world: (…) -> UP / DOWN`) says which way the rig really points; the once-a-second line now adds the second's
largest per-tick yaw change and the camera yaw drift, the roll's two drivers.
Debug: `[SMS waist] on (ma_run2): pitch +38.1 deg, roll -3.2 deg, the free set`, then once a second `pitch +40.4 deg, roll +0.0 deg
at 32.0 u/tick (free)`; setup `considerWaist on jnt_chest yes: ...`. Self-test: `waist: free pitch at fwd 32 (deg) 40.43`, `free
roll, 1500 BAM/tick turn at 32 (deg) 46.37`, `pumping pitch at fwd 32 (deg) -8.79`, `ticks to 95% of the run lean 42`.

**The waist, third look, and the hover's stick** (Jaysonn, 2026-09-27: "feeling good right now but steering feels off still"; his log:
a session of running, jumps, U-turns and twelve hovers — no spray, no turbo). Two things in that path were not the game's.
(1) **The hover pushed twice as hard as the game.** `rocketing()` adds `mag × cos(angleDiff) × mDivingParams.mAccelControl` a tick;
`TDivingParams`'s code default is 0.02 (MarioInit.cpp 923) but the class loads `/Mario/Diving.prm` (45) and the shipped file says
**0.01**. S3 had 0.02 and called it shipped. Full stick: +0.32 a tick, settling against the 0.985 brake at 21.3 units (8.6 m/s) —
not 42.7 (17 m/s); pulling back reverses him half as hard (his log had +32 → −17 within a second). `SmsHover.AccelControl` 0.01,
two self-test checks (`hover: full-stick push a tick 0.32`, `settled speed with the stick held 21.33`).
(2) **The twist was turned the wrong way.** The setup line read `jnt_chest local X in world: (0.00, 1.00, 0.00) -> UP`, and the
game's own model agrees: `ma_mdl1.bmd` JNT1 has `chn_chest` at rot (0, 90, 0) under `center` and `jnt_chest` at (0, 0, 90) under it,
so the chest's bind frame is X → up the spine, Y → +Z (the front: the toes point +Z, the left limbs sit at +X), Z → +X (lateral) —
the shoulders hang 29.9 along X. `MsMtxSetRotRPH(roll, 0, pitch)` = `Rz(pitch) × Rx(roll)` (MathUtil.cpp 226) concatenated onto that
joint: `Rx(roll)` is a twist about the spine, and with the game's sign (a left turn is a positive yaw change — the face `(sin, 0, cos)`
turns from +Z toward +X, his left) a positive roll turns the chest counterclockwise seen from above, **into the turn**. The flip
(0e545b1) turned it away: steering left, his chest and head faced right. Reverted — `_smsWaist.Roll = +_smsWaistRoll`. What he read as
"leans out" with the game's sign is the game's geometry: `Rz(pitch) × Rx(roll) × v` twists first and then pitches the twisted torso
forward, so the shoulder that came forward drops by `asin(sin pitch × sin roll)` — 27° at the caps (40° / 46°) — and in a left turn
the forward shoulder is the right one: **the outside shoulder dips while the chest and head look into the turn.** That is what the
original does with these joints and these numbers; the inside-shoulder-down look would be a deliberate deviation.
(3) The waist prints wrote negatives as `-+18.2` (`{x:+0.0}`: .NET takes the '+' literally); now `+0.0;-0.0`, and the on / once-a-second
lines add `(shoulders 31 deg into the turn)` / `AWAY from` — the twist's sign against the frame's yaw change.
Checked and left as the game: `RunningRotSp` 1500 / 1500, `PumpingRotSp` 300 / 600, swim 300 / 200, hover `RotSp` 200, `TurnNeedSp`
10, `WaitingRotSp` 220, the U-turn gate 0x471C (100°), `mIntendedYaw = matan(−mStickV, mStickH)` from the s16 pad with the ±7 / 6 shift
(the port's `PadVector`), the 120 Hz movement (`MarDirectorDirect.cpp` 85–150: the simulation-tick loop performs the movement list every
tick, gates A / B only thin out other actors).

**The waist and the head, fourth look; the dash clip** (Jaysonn, 2026-09-27 03:05: "hover feels fine: turbo steering feels off,
mario is still moving left and right with his head when running normally"; his log). First: that log came from the **17764c4 binary**
— its waist prints still read `-+18.2`, carry no `(shoulders … into the turn)` readout and lack the two `hover: full-stick push` /
`settled speed` self-tests — so the third look's changes (the hover's 0.01, the twist's sign) had not been played; both are in this
build with the two below.
(1) **The head.** `MarioHeadCtrl` (MarioDraw.cpp 402) does nothing to the head in a run: its yaw term is `unk100 × anmSpeed` with
`unk100` = 0 outside the L camera, and only a negative `gunAngle / 2` pitches it. The head is the clip's. `ma_run2.bck` read against
`ma_mdl1.bmd` (`tools/projectshine/bck.py`): over a stride the chest yaws **−14° .. +22°** (35° peak to peak, a stride every 0.34 s at
fwd 32) while the **head's baked track counter-rotates it**, holding the head's world yaw within ±4° (±5° with the 40° lean). The glb's
`ma_run2` head track matches the BCK. `HeadForwardLock` (ours) holds `jnt_head` at REST against `jnt_chest` — which deletes exactly
that counter-rotation. Under the legacy waist IK the chest's stride twist was overwritten, so the lock looked right; under SMS (IK
retired, e46bc07) the chest twists as authored and the locked head swung 35° with the shoulders three times a second — the "head
moving left and right". Fix: the lock's locomotion weight is 0 under SMS (the hover's head angle keeps the node). Its −12° look-up
goes with it: the game has none — at fwd 32 the lean puts the head's look **22–31° below level** (the clip alone has it 12–18° above).
(2) **The dash clip never played.** `SmsGroundTick` travels to `ma_turbo_dash`, then the grounded classification (`Mario.cs`
~3447) calls `SetMarioState(sprinting)` **every frame** → `Travel("ma_run2")`; `AnimationNodeStateMachinePlayback` honours only the
last `travel()` of the frame, so `ma_run2` stayed, at the doubled `RUN_TIMESCALE`. His log proves it: `[SMS waist] … roll -21.9 deg at
80.1 u/tick (pumping)` for three seconds — that line prints only while the lean *applies*, and it applies for RUN1 / RUN2, never
TURBO_DASH (MarioDraw.cpp 496). So the dash ran with the water-gun lean (−11° pitch) and a twist that saturates at the 22° cap on any
steering (0.5 × dYaw × 80 ≥ 4000 from 100 BAM a tick) — and, with the 17764c4 flip, AWAY from the turn: chest and head 22° out of
every dash turn. Fix: `SetMarioState`'s run cases travel to `ma_turbo_dash` while `_fluddDashAnimOn`; the dash now plays TURBO_DASH
with no lean, as the game. (Also why S5's "at twice the run's rate" was seen on `ma_run2`.)
(3) **The dash turn rate is left as the game's**: `doRunning()` `if (FLUDD_EMITTING) rotSp = RunningRotSpMin` (MarioRun.cpp 510) =
1500 (`Mario.prm` shipped, min = max), the intended yaw the stick's direction at any deflection past the deadzone (`checkController`
1494 takes the yaw, 1536 then overwrites `mIntendedMag` with `mDashSpeed`), the face converging 8.24° a tick, no U-turn. To be judged
again with the clip and the lean fixed.
Not ported (noted): `MarioHeadCtrl`'s `gunAngle / 2` — with the turbo's −4096 gun angle the head gets `Rz(−2048)` (11.25°; the hover's
+5294 is "down", so this is up) whenever the turbo is selected with water.
Debug: setup `[SMS waist] head lock off under SMS: …`; `[SMS waist] off (ma_turbo_dash): …` as a dash starts.
**Hotfix (03:45):** 63ebb3c's `jnt_chest local Y` setup print read the `armature` field, which `_Ready` assigns *after*
`SmsWaistSetup()` runs — a NullReferenceException inside `_Ready` skipped the rest of start-up (no `animPlayer`, no tree, no
pack), and the first `_PhysicsProcess` after the level intro threw every frame (`UpdateRunAnimationSpeed`): one idle loop, then
frozen. The print now resolves the node itself. Rule: a start-up helper may only read fields assigned above its call site.

**The waist, fifth look: what the callback really moves** (Jaysonn, 2026-09-27 03:45: "starts fine now but mario still looks odd when
running"). A headless run of cad5009 gave `jnt_chest local Y in the armature's frame: (0, 0, 1) -> BACKWARD (+Z)` — the *label* was
wrong, not the lean: the rig keeps the BMD's axes (skin bind: `jnt_head` at z −3, behind the chest joint; the left foot at +x), so the
model's FRONT is +Z in the armature's frame and a positive pitch tilts him forward. Then the real finding, in J3D itself.
**The game's waist lean never rotates the chest's own skin.** `J3DMtxCalcBasic::recursiveCalc` (JSystem `J3DJoint.cpp` 56–82) runs
`calcIn()` — `calcTransform()` concatenates the joint's TRS onto `mCurrentMtx` and *stores* it with `setAnmMtx` (a `MTXCopy`,
`J3DModel.hpp` 252) — and only then the joint callback with 0, which `MarioWaistCtrl` uses to post-multiply `mCurrentMtx` by
`Rz(pitch) × Rx(roll)`. The stored matrix is what `jnt_chest`'s vertices skin to (DRW1 rigid; EVP1 envelopes chest/waist 0.7/0.3 …
0.2/0.8); the modified `mCurrentMtx` is what the **children** inherit: `jnt_head` (→ `M_head`, `M_head_cap1` — the head meshes hang on a
zero-offset child joint for exactly this reason; `MarioHeadCtrl` works the same way), `jnt_sldr_L` / `R` (→ the arms, and the torso
vertices weighted chest/shoulder 0.3/0.7, 0.5/0.5). No Mario callback re-stores its joint's matrix. The chest joint sits at the
**pelvis** (bind (0, 58, 0)), the shoulders 30 and the head 39 units up its X: the game pivots the shoulder girdle, arms and head about
the hips while the belly / chest skin keeps the clip's pose and the upper torso stretches after the shoulders — the hunched Sunshine
sprint. The port turned `jnt_chest`'s own pose, so the whole torso bowed 40° from the pelvis and the pack bowed with it. `SmsWaistLean`
now leaves `jnt_chest` alone and writes `pose_c = R × pose_c` (origin and rotation) for each child bone, R in `jnt_chest`'s frame —
the game's arithmetic exactly. The head ends where it was (it is a child): at fwd 32 it looks 22–31° below level; that is the game.
The waist prints now carry a posture readout: `posture: chest -4 deg forward, shoulders +36 deg forward, head looking -26 deg (+2 deg
off the face, + = his left)`; setup `MarioWaistCtrl turns the CHILDREN of jnt_chest (...): jnt_sldr_R, jnt_sldr_L, jnt_head`.
Open, noted: the game hangs the pack on `chn_chest` (`TMario::perform` → `setBaseTRMtx(getAnmMtx(mJointIdChnChest))`), whose X is
horizontal, so `|mtx[1][0]|` is 0 and the tilt is the constant `unk1D06`; ours rides `jnt_chest` (the approved placement) and so
twists with the clip's stride while the game's does not. Settling it needs `wg_mdl1.bmd`'s own vertex axes, not the rip's.
**Sixth look, 04:05** ("still odd … his head is just leaning and still moving right to left"). The children rotation was being
half-undone: `JiggleBone` (ours, on `jnt_head`, `jnt_sldr_L`, `jnt_sldr_R`) runs in the modifier stage after `SmsWaistLean` and writes
each bone's position as `rest + shift` every frame, so the lean kept only its *rotation*: the head tilted 40° in place on top of an
upright torso while the game swings it 25 units forward and 9 down about the hips (bind: chest joint at y 58, head at 97, shoulders at
88). Fix: `SmsWaistOrderLast()` at the end of `_Ready` moves the lean behind every other modifier; setup prints the order. Gotcha for
anything that moves `jnt_head` / `jnt_sldr_*`: the jiggles own those bones' *positions* (rest + spring shift), not their rotations.
What the game's own arithmetic gives the head at fwd 32 (BCK + `Rz(40.4°)` on the children, `bck.py`): yaw within ±5°, look 22–31°
below level, a sideways **bank of −7..+14°** and a lateral sway of −7..+8 units, three times a second — the pitch is applied about the
chest's lateral axis, which yaws ±20° with the stride. That rocking is in the game's numbers; if Dolphin shows a steadier head, the
remaining candidates are the pitch magnitude (230 × fwd, `bodyanglefree.prm`) and nothing else in `MarioWaistCtrl` / `MarioHeadCtrl`
(`unk100` is only ever zeroed, so the head callback is the identity in a run).
**04:18** ("head is still looking off and leaning when turning is the wrong way still"): the twist's applied sign reversed as a
deliberate deviation — see the deviations list; the reading itself is unchanged after a third pass through the decomp.

**Pass 6 — the joint the waist really bends, and the game's jiggle** (Jaysonn, 2026-09-27 20:40: "still not matching … maybe the
bone jiggle physics might be a part here? … go thru that work and see how the jiggle on Mario's physics work and apply that").
(1) **`MarioWaistCtrl` sits on `chn_chest`, not `jnt_chest`.** The decomp writes `mJointIdChnChest = getIndex("chn_chest")`,
`mJointIdChest = getIndex("jnt_chest")` (MarioDraw.cpp 1136–1137), but the shipped data contradicts the field it uses for each: the
FLUDD hangs on `mJointIdChnChest` and `setBaseTRMtx` tilts the pack by `|mtx[1][0]|` (how vertical the joint's X is) — meaningless on
chn_chest (X = his back), right on jnt_chest (X up; and that is where the pack sits correctly in our build) — and the time-lag effect on
`mJointIdChnChest` loads `/Mario/MtxEffect<id>.prm`, shipped as **14** (jnt_chest), 16 (jnt_arm_R1), 22 (jnt_arm_L1); no 13 exists. So
the field the decomp calls chn is jnt, and the waist callback — on the other field — is on **chn_chest**: the constant, skinless,
zero-offset joint above the pelvis whose only child is jnt_chest, i.e. the whole upper body (the `chn_*` pattern: chn_foot_L / R host
the foot callbacks the same way). chn_chest's frame is X = back, Y = up, Z = left, so `Rz(pitch) × Rx(roll)` is a pitch about the
lateral axis (forward by speed) and a **bank about the front-back axis** — a positive roll, a left turn, tips him toward Z = his left,
**into the turn** — applied to the entire upper body, torso skin included, about an axis that never moves with the stride. Every
report fits: a plain lean, into the turn, no head sway, no hunch. `SmsWaistLean` now pre-multiplies jnt_chest's local pose by R in
chn_chest's frame; the 16ccd6d sign deviation and the 06702ea skinning deviation are both gone (removed from the list below).
(2) **The game's jiggle is `TMtxTimeLag`** (MarioUtil/MtxUtil.cpp), `TMultiMtxEffect` on three bones, all TIME_LAG: jnt_chest,
jnt_arm_R1, jnt_arm_L1 (MarioDraw.cpp 1402–1414). Per 120 Hz tick, in world space: `vel += accel × (target − lag); vel *= brake;
lag += vel`, clamped to ±limit per axis around the target; the quaternion the same way (shorter arc, component-wise, `MTXQuat`
normalises); written into the matrix the **children** inherit. Shipped: chest 0.1 / 0.9 / 5 units / 1.0 / 0.5 — the shoulders and head
trail the chest by up to 5 units (a constant 5 behind at a run, a ~6 Hz bob after the hips) — arms 0.1 / 0.8 / 5 / 0.05 / 0.8 — the
forearms and hands trail the upper arm's swing at ~4 Hz, the floppy Sunshine arms. Written only while `gMarioAnimeData[anim].unk6 & 2`
(run1, run2, wait, jump, land, sstep, turbo_dash, turn, the slips … — not hang / tree / ladder / rope / fence / squat / door / demo),
the upper body is IDLE (not pumping or holding), and `mMoveTickCount >= 0x4B0` (10 s into the level); the state integrates while off,
so enabling never pops. Port: `assets/Sms/SmsTimeLag.cs`, after `SmsWaistLean` (tree order: chn_chest's callback, then jnt_chest's);
our spring `JiggleBone`s (head, both arms) are off under SMS — they were the approximation of this.
Not yet: the FLUDD's own table — the pack root TIME_LAG 0.05 / 0.95 / 5 / 0.1 / 0.93 (the tank lags and bobs) and the two handles as
`SWING_RZ` pendulums (acc (0, −4, 0), L 50, brake 0.7), written while anime bit 4 and upper idle (`/Mario/WaterGun/MtxEffect0, 10, 12`).
Also seen on the way, not this pass: `doRunningAnimation` blends RUN2 with RUN1 (the model's fixed "old" slot 0x41) by
`mMotionBlendRatio = 1 − (rate − 1.5) / (2.5 − 1.5)` — a continuous walk↔run blend by speed where ours switches clips on the stick;
`setAnimation` rates are `rate × 0.5` frames a tick = 60 × rate BCK frames a second; the foot callbacks (`MarioFootPos/DirCtrl`) are
installed for every state, not idle / pivot only.
Debug: setup `[SMS waist] MarioWaistCtrl sits on chn_chest …`, `[SMS jiggle] TMtxTimeLag on jnt_chest …`; once a second `… (banks 24 deg
into the turn) …; lag on: chest 5.0 u / 3.1 deg, arm 12.4 deg`.

**Pass 7 — the FLUDD's own matrix effects** (Jaysonn 20:58: "do the other jiggles and we can do it all at once for testing").
The water gun's `TMultiMtxEffect` (WaterGun.cpp 1283) has two entries. **Entry 0, TIME_LAG on the tank root**: `/Mario/WaterGun/
MtxEffect0.prm` ships (0.05 / 0.95 / 5 / 0.1 / 0.93) but nothing ever sets its flag 1 — `addCallBack` only touches
`mMtxEffectTbl[1]` (MarioDraw.cpp 1801 / 1827 / 1830), the constructor starts at 0 and `setup` sets flag 2 only — so in the shipped
game the tank never lags. Not ported. **Entry 1, SWING_RZ on `jnt_G_handle`**: a point mass on a rope of length L hangs from the
joint in world space; per tick `vel += acc; vel *= brake; bob += vel`, the bob is put back on the sphere of radius L and that
correction is added to its velocity; the joint's world X is aimed at the bob (Y = Z × X, Z — the hinge, Mario's lateral axis —
untouched) and the children (`jnt_G_handle_R / L` → levers → grips) take the aimed frame. Written while anime bit 4 (everything but
the squat, the rope, the spin and the shine demo), the upper body is idle and he has the FLUDD. **The parameters are the compiled-in
defaults — acc (0, 0, 0), L 50, brake 0.9**: the shipped `wg_mdl1.bmd` (mario.szs and common.szs, identical) has `jnt_G_handle` at
joint 7, and both params.szs copies hold `MtxEffect10.prm` and `12.prm` (acc (0, −4, 0), L 50, brake 0.7, written for an older
rig) but no 7 — `TParams::load` finds nothing (the plaza's scene archive has no prm either) and leaves the defaults. With no gravity
the grips have no rest direction: at a run they trail straight back and they stay wherever the last move left them; the rope's
radial pull kills the velocity along the rope, so a stop does not swing them. Port: `assets/Sms/SmsSwingRZ.cs` on the tank
skeleton. If Dolphin shows the grips falling back to hanging after a run, the retail build is finding parameters we cannot see and the
10 / 12 values go in — one line in `SmsParams.MtxEffect`.
Also checked: **the cap's TIME_LAG** (`TMarioCap`, `/Mario/MarioCap/MtxEffect1.prm` 0.5 / 0.3 / 5 / 0.5 / 0.3, bone 1 of ma_cap1 /
ma_cap3) sits on `null_weight_B`, a LEAF joint — with J3D's store-then-callback order it changes nothing; and our cap is part of the
head mesh anyway. Nothing to port. **Rates**: `TMario::perform` runs `calcAnim` inside the CUE_MOVE block (MarioMain.cpp 92), so
`considerWaist`, `addCallBack`, the model calc with the joint callbacks and the water gun's calc all run on every 120 Hz move tick —
our per-tick stepping of the waist filter, the time lag and the swing is right (the director blacklists CUE_CALC_ANIM on the first
three ticks of a frame, but Mario does not use that cue for its own calc).
Debug: setup `[SMS jiggle] FLUDD handle swing (TMtxSwingRZ) on jnt_G_handle -> jnt_G_handle_R, jnt_G_handle_L …`; the once-a-second
line ends `…, handles 88 deg back` (`(off)` while a spray or a squat holds it).

**Pass 8 — the run, again, and the grips** (Jaysonn 2026-09-28 00:08: "marios head looks good now but flood is not. floods arms look
like they are doing weird things like going upside down. can you also have it play the right speed and such? go thru all the running
again since the speed feels off").
**Grips**: the no-gravity defaults did exactly what the note predicted — after a run they trail back, after a U-turn they point
forward, and they never come down. He wants them hanging: the shipped `MtxEffect10 / 12.prm` values go in as a deliberate
deviation (see the list) — gravity 4 units a tick², brake 0.7, rope 50. At 120 Hz that is a stiff, quick pendulum: ~41° back at a
full run (the pull `(v − 8, −4) × 0.7` balances at `atan(2.4 / 2.8)`), ~19° at a walk, hanging at rest, settled within ~10 ticks.
**The run clip**: `doRunningAnimation` (MarioRun.cpp 116) was still Jaysonn's original mapping — clip by STICK (over 0.5 = `ma_run2`,
under `SNEAK_MAX_INPUT` = `ma_sstep`, else `ma_run1`) and rate = `lerp(0.9, 1.4, speed / 11.55) × 1.5` on the tree — about 2.2 at a
full run. The game: `sp = min(mIntendedMag, mForwardVel)`, never below 4; **SSTEP** below `Walk2Soft` 8 (back to the run above
`Soft2Walk` 13) at `max(sp, 0.1) × SoftStepAnmMult` 0.15; **TURBO_DASH** at the dash; else **RUN2 at `sp × 0.055 + 1`** — 2.76 at 32,
1.44 at 8, ~5.4 in the dash — scaled in water by `(1 − depth / SwimDepth × (1 − InWaterBrake)) × InWaterAnmBrake`, and **blended
with RUN1** (the walk cycle, the model's fixed "old" slot) by `mMotionBlendRatio = 1 − (rate − 1.5) / (2.5 − 1.5)`: pure walk cycle
below rate 1.5 (sp ≈ 9), pure run above 2.5 (sp ≈ 27), a continuous mix between. `setAnimation` gives the frame control `rate × 0.5`
BCK frames a tick = 60 × rate frames a second (`SMSGetAnmFrameRate` = 60 / vsync for everything else: the BCKs are 60 fps), and the
glb's clips are those BCKs at 60 fps (`ma_run2` 60 keys, 0.983 s), so **the Godot time scale is the game's rate, no factor**. Ported:
`SmsSetupRunBlend` adds a `ma_runblend` state to BaseSM at start-up — a blend tree of `ma_run1` and `ma_run2` into a Blend2 driven
by `parameters/BaseSM/ma_runblend/mix/blend_amount` (verified by a read-back at start-up; if it fails the old clips stay) — and
`SmsRunAnimTick` picks the clip, the rate and the mix every frame under SMS; `SetMarioState`'s gait cases travel to what it picked.
`MarioWaistCtrl` applies on `ma_runblend` too (it is RUN1 / RUN2). Transitions into `ma_runblend` teleport (no cross-fade) — as the
game, whose `setAnimation` zeroes the blend ratio on every clip change.
Debug: the once-a-second line ends `…; anim ma_runblend rate 2.76 (run2 100%)` / `anim ma_sstep rate 0.90`; setup `[SMS run]
ma_runblend in BaseSM …`.

**Pass 9 — the head popping between two spots** (Jaysonn 2026-09-28 00:28: "marios head is frame then frame going back and forth …
going to one spot then back to normal every other frame … a weird aftertrail effect … maybe the jiggle values are slight off?").
Not the values — the clock. The game runs the matrix effects on every move tick, in lockstep with Mario's movement. The port moved
Mario in the PHYSICS frame but stepped the time lag inside the skeleton's modifier pass, i.e. per RENDER frame, against a world-space
target — the chest's world position — that only moves on physics frames. On a render frame with no physics step the chest stood
still while the lag copy ran on (16 units a frame at a full run) and hit the +5 clamp ahead of it; on the next frame with a physics
step the chest jumped 32 units and the copy sat at the −5 clamp behind. Head and shoulders swapped between ~+5 and −5 units around the
chest on every frame the two clocks did not line up — every other frame on a display above 60 Hz, now and then at 60 from phase drift.
Both effects (`SmsTimeLag`, `SmsSwingRZ`) now have a `Step(ticks)` that Mario calls from the physics frame (right after the waist,
with that frame's tick count) against the bones' current global poses, storing each bone's offset in ITS OWN frame; the modifier pass
only applies that offset to the children, so between physics frames it rides the animated bone smoothly. The FLUDD pendulum was
stepped the same way and gets the same fix. The parameters are untouched.

**Pass 10 — the foot callbacks, read properly** (2026-09-28 00:52, "do the walk-run stuff left over (foot planting in every
state)"). My earlier note was wrong: `addCallBack` does INSTALL `MarioFootPosR/LCtrl` (on `chn_foot_*`) and `MarioFootDirR/LCtrl` (on
`jnt_foot_*`) every frame whenever the graphics flag is set, but each callback checks Mario's status itself (MarioDraw.cpp 524–740)
and acts only while it is of the **WAITING type** — `WAIT`, `SQUAT` (+ start / stand-up), and the `*_END` recoveries (`JUMP_END`,
`SEC_JUMP_END`, `LAND_END`, `U_TURN_JUMP_END`, `HIP_ATTACK_END`, `SLIP_END`, `PULL_END`, `THROWN_END`) — and not `BRAKE_END`, not
`SLEEPY` / `SLEEP`, not on Yoshi. **The game does not plant feet while walking or running.** So `FootIK`'s idle / pivot gate was
already right in spirit; this pass widens it to the crouch, the three jump landings, `landing` and the ground-pound landing under SMS.
The mechanism itself differs and stays different: the game has no IK — `PosCtrl` shifts the foot chain to `ground + 10` units,
clamped to ±15, letting the ankle stretch (the BMD's envelopes hide it), and `DirCtrl` re-aims the foot's frame into the ground
normal with the forward projected onto the plane, writing the foot's own stored matrix too (the one callback that does). On our rigid
glb a detached ankle would show, so Jaysonn's two-bone IK with the shared hip drop stands as the deliberate substitute; it now uses the
game's numbers where they translate — the ankle 10 units above the ground it finds (`FootOffset`, was a by-eye 13.2) and a ±15-unit
cap on each foot's correction (`MaxCorrectionSkel`, new; past it the foot floats or sinks as in the game). The eased weight
(`FootIKEaseSpeed`) stays: the game snaps, but its statuses change when the feet are already on the ground.

**Pass 11 — the turbo dash on land, by request** (Jaysonn 2026-09-28 01:02: "going back to using turbo nozzle. the steering is
still too sharp. and running animation looks slow … making sure the steering and animations match up"). Checked against the decomp
first; both were already the game's numbers, so both changes are **deliberate deviations** (see the list):
- **Steering.** `doRunning` (MarioRun.cpp 510): `if (FLUDD_EMITTING) rotSp = RunningRotSpMin` — 1500 BAM a tick, the free run's own
  snap (shipped min = max = 1500), converged with `IConverge` onto the stick's camera-relative yaw. `run.prm` ships a `DashRotSp` of
  60 but no code reads it. The port had 1500 since S5 fix 2; he has now called it too sharp twice. Now `SmsParams.Fludd.DashRotSp`
  = 600 BAM a tick: a 90° turn in ~0.23 s at 32 m/s (radius ≈ 5 m) against the game's 0.09 s. One constant; 1500 restores the code.
  *(Pass 13, 01:40: at 600 he still wanted "bigger wider turns" → 300: 0.45 s, radius ≈ 9 m.)*
- **The dash clip.** `doRunningAnimation` paces `ANIM_TURBO_DASH` by `sp = min(mIntendedMag, mForwardVel)`, and in the dash
  `mIntendedMag = mDashSpeed`, which ramps to and caps at 32 (MarioMove.cpp 1509) — so the game runs the dash legs at 2.76, the same
  tempo as a full run, while he covers 2.5× the ground (the 60-frame `ma_turbo_dash.bck` is the glb's 60-key clip, so no import
  scaling either). The pre-pass-8 build ran it at 4.2 (the old lerp × 1.5 × 2), which is what he remembers as right. Now paced by
  `mForwardVel` (`DashAnimByForwardVel`): 80 → 5.4.
- **The water surf** was not touched: "shooting too high into the air like it's angled wrong" needs one answer before I change
  anything — is that Mario leaving the water, or the turbo's jet pointing at the sky? The game keeps him on the surface (`SWIM_PADDLE`
  with `setPlayerVelocity(DashMax)`, `mVel.y = 0`, `mPosition.y = the surface`), aims the turbo neck at −22.5° from the pack in
  every status (`calcGunAngle`: `−LAngleBase`, no swimming case), and plays the paddle clip at 5×; in the prone paddle pose the
  pack's back faces up, so the jet does leave upward-back in the game too.

**Pass 12 — the surf jet, measured before changed** (Jaysonn 2026-09-28 01:17: "while in water and in turbo the nozzle shoots more to
the sky … otherwise the nozzle is angled correctly"). What the game does, from the data: the surf plays `ma_swim` (`ANIM_PADDLE_SWIM`
→ BCK 0x8F is the same `ma_swim.bck` as `ANIM_SWIM`) with the base matrix yaw-only (`calcBaseMtx` zeroes `mFaceAngle.x` while
swimming), the origin held 80 units under the surface (`FloatHeight`), the turbo neck at −22.5° in every status (`calcGunAngle` =
`−LAngleBase`, no swimming case), and — because the dash only lives while R is held (`offFlag(FLUDD_EMITTING)` the tick the pump
stops) — the upper body PUMPING, i.e. `ma_swpmp` grafted onto the chest subtree. Sampling the BCKs: `ma_swim` alone leans the spine
64° from upright and puts the jet at **41.5° above level**; with the swim-pump upper body grafted (as in the surf) the spine is 46–48°
from upright and the jet leaves at **23–26° above level**, from a nozzle ~51 units over the water. So the game's surf jet is a low
backward spray; if ours points higher, the pump graft is not on the chest during the surf, or the body is deeper/shallower than 80.
The code says the port does all of the above (graft on while `_fluddUpper` is PUMPING, which the dash requires; neck chased in water;
80-unit float clamp every tick), so nothing was changed blind: a once-a-second `[SMS turbo] dashing: jet +N deg from level …` line
now prints the jet's elevation, the spine's tilt, the upper state, R, the trigger, the swim sub-state, the depth and the graft weight.
A water dash in the next log settles which of those is off; ~25° is the game.

**Pass 14 — the water leaves along the drawn barrel** (Jaysonn's water-dash log, 2026-09-28 01:48). The pass-12 readout said it
all: surf jet **+33…+36°** with the pump graft at weight 1 and the feet 79 units under; land jet **+6…+10°** with the spine leaning
7–12°. Modelling the whole chain (`jet_model.py`: chest pose × `setBaseTRMtx`'s pack tilt `−0x1800 × (1 − |spine·y|)` × nozzle_center
× `back_wg` neck × `null_G_muzzle`) reproduces both numbers exactly **only with the neck's −22.5° left out**: land = lean − 0.5°,
surf = 45.8–48.3° (the grafted spine) − 10–11° (the pack tilt) = 35.6–37.0°. Root cause: `SmsFluddMuzzle` read
`GetBoneGlobalPose(null_G_muzzle)` in the physics tick, and since Godot 4.3 a `SkeletonModifier3D`'s changes are applied for the
skin update and then **restored** — outside the modifier pass the poses are the clip's alone. The drawn turbo head is tilted
(`SmsBoneRotZ` on `jnt_back_nozzle_neck`), so "the nozzle is angled correctly", but the drops left along the un-tilted barrel: level
on land, 22.5° too high everywhere. Same for the hover's bank (`WaterGunDivingCtrlL/R`). Fix: `SmsBoneRotZ.DrawnGlobalPose()`
re-composes the chain with the callback rotations, and the emitter uses it (the game's `getEmitMtx()` is the anm matrix with the
callbacks in it). The game's numbers, now ours: **land −11…−15°** (the spray splashes the ground behind him — that is the game),
**surf +13…+14.5°** from a muzzle ~70 units over the water (the earlier "~25°" left out the pack tilt). No deviation needed. The
readout now measures the drawn chest (the pack attachment, which follows the graft) and names the swim clip while swimming.

**Pass 15 — the squat: `MARIO_STATUS_SQUAT` with the stick aim** (Jaysonn 02:03: "move on to … the squat aim (R-stick)"). From
`MarioMove.cpp canSquat()` (44), `MarioWait.cpp squating()` (308) / `squatStandup()` (387), `WaterGun.cpp calcGunAngle()` (216),
`MarioDraw.cpp MarioWaistCtrl` (486) / `MarioHeadCtrl` (457), `MarioUpper.cpp` (30):
- **Entry** from WAIT and RUN (he stops dead): the FLUDD out, not a RocketType-1 nozzle (hover / rocket), and `mInput 0x200` = R or L
  held while not `FLUDD_EMITTING`. `MEANING_R` is the GC trigger's **click**; the pad has none, so a pull ≥ 0.98 is the click
  (`SmsParams.Squat.RClickPull`) — the analog is still the pressure, and easing off the click while keeping it pressed is how the
  turbo's charge-then-go works (stand-up → the stick → RUN → the dash). The legacy Z stands in for L.
- **R held**: the stick's left/right turns him in place at `WaitingRotSp` (220 BAM a tick) through the `SquatRotMid` curve (5 % of the
  rate up to 70 % deflection, then to 100 %; compiled defaults 0.7 / 0.05 — controller.prm ships neither), truncated to whole BAM
  like the game's s16 cast. `ma_sqwat` at rate 1 (its pump clip is `ma_pump`, index 0x44, like wait/run — not `ma_sqpmp`).
- **The aim** (`calcGunAngle`'s SQUAT branch): target = the angle itself + stick-vertical × `RButtonMult` (10000), clamped
  [LAngleMin, LAngleMax], chased at LAngleChase — any push pins the target at a limit and the chase does the moving; a resting stick
  HOLDS the aim; leaving the squat drifts it back to −LAngleBase. A positive angle (stick forward) bends the whole upper body forward
  by that angle (`MarioWaistCtrl`, status bit 0x8000, in place of the run lean) — the spray, which leaves forward over his head, goes
  DOWN, to 44° (LAngleMax 8000). A negative angle (stick back) tilts the spray nozzle's muzzle chain UP by that angle — `NozzleCtrl` on
  `chn_muzzle_1`, to LAngleMin −13000 = 71° — while the body stays upright and the head looks up by half the angle (`MarioHeadCtrl`).
  *(Pass 15b — the first cut of this pass read the decomp's `getIndex("chn_muzzle_l")` (a letter) against the model's `chn_muzzle_1` (a
  digit) as a dead binding and left the water level on a pull-back; the shipped −13000 range, the geometry — Rz(−20°) at chn_muzzle_1
  lifts the barrel exactly 20° — and the game as played (R-squat, pull back, spray the bell tower) say the binding works and the
  decomp's literal is the transcription. Ported as the design; a Dolphin check is on the list.)* The head tilt is new for every
  nozzle: with the turbo out (−4096) Mario looks up 11.25°, as in the game.
- **L held**: the sidestep — `side = mIntendedMag × sin(stick − face) × PumpingSlideSp` (0.12) units a tick along his left, `ma_swlkl` /
  `ma_swlkr` at |side| × 0.5, `ma_sqwat` at rest. **A + R** with the spray nozzle: `BACK_JUMP` (the backflip; the legacy Z + A
  backflip is replaced under SMS by the game's trigger). Squatting with the upper body idle → `HOLDING_PUMP` (hands on the grips).
- **Release** (neither R nor L): `SQUAT_STANDUP`, `ma_sqend` (19 frames = 38 ticks) then WAIT; A → JUMP and the stick → RUN cut it.
  `ma_sqend`, `ma_swlkl`, `ma_swlkr` are runtime BaseSM states (the scene has `ma_sqwat` only); the stand-up auto-advances to `ma_wait`.
- Side effect, also the game's: the rocket's manual fire (A while R is clicked) now has a click to read.
- **15b**: `SmsFluddSetupSprayAim()` — an `SmsBoneRotZ` on the spray nozzle's `chn_muzzle_1`, fed the gun angle when negative; the
  emitter reads it through `DrawnGlobalPose` like the turbo's neck, so the drops leave where the lifted barrel points.

**Pass 16 — the Y camera** (Jaysonn 02:03: "lets move onto the Y cam"). New `assets/Sms/MarioYCam.cs` (a partial of Mario). The
game's `CAMERA_MODE_L_NORMAL` — its "L button camera" — from `CameraChange.cpp` (`doLButtonCameraOn_` 469 / `Off_` 479,
`setUpToLButtonCamera_` 256), `CameraNotice.cpp ctrlLButtonCamera_` 207, `cameragc.cpp` (`rotateX/Y_ByStick` 332/344,
`calcPosAndAt_` 464), `MarioGamePad.cpp` 69–88, and the SHIPPED `/Camera/kindLNormal.prm` (params.szs → `params/camera/kindlnormal.prm`;
the research pass had only the compiled defaults, which differ: FOV 60 not 50, dist 200 not 450–2000, pitch −12800..10000):
- *(Pass 17 corrects the rates below: the camera ticks at 120 Hz, so "a frame" is a tick — 198°/s, 0.83 s, a 0.5 s lock, a 0.13 s
  blend — and the Y press swings the normal camera behind him before the mode starts.)*
- **Y** on the ground (a normal camera, not in water, not the dash) enters; **Y, A or B** leave — but only after `unk282`, a 60-frame
  lock set on every change (2.0 s at the game's 30 fps), and the 16-frame blend (`mSLInbetLNormal`). Leaving the ground, the water,
  a hit or the dash ends it at once. The normal camera keeps running underneath and the view blends back to it.
- **The stick is the camera's** (`mCompSPos[4],[5]`; Mario's stick is starved — he stands): yaw at 300 BAM a frame (49°/s at full
  stick, unbounded), the pitch as a **0..1 ratio** at 0.01 a frame (3.3 s end to end), pitch = lerp(−12800, 10000, ratio): the camera
  from 70° UNDER the target (looking up at the sky) to 55° OVER it. **Stick up raises the camera, i.e. looks DOWN** — the game's own
  inversion, kept.
- **Placement** (`calcPosAndAt_`): the target is the nozzle top (30 units up `nozzle_center`'s Y; his origin + 150 without the FLUDD);
  the look-at is 72 units to its side (`OffsetLookatXZ −72` — the over-the-shoulder framing); the camera 200 units from the look-at at
  elevation pitch + 12.7° (`OffsetAngleX 2306`) and bearing yaw − 2.7° (`OffsetAngleY −489`); position and look-at chase at 0.1 (XZ)
  / 0.08 (Y) a frame. On entry the ratio starts where the pitch is −OffsetAngleX (the camera level with the target) and the yaw is the
  normal camera's bearing. FOV 60, near 10 units. No BG collision in the mode (the game turns its ground/roof/wall checks off).
- **Mario** (`MarioMove.cpp 2449`, `MarioUpper.cpp 17`, `MarioDraw.cpp 476`, `WaterGun.cpp 216`): faces `yaw + 0x8000 − OffsetAngleY`
  (2.7° off the camera's axis), holds the pump (R sprays, X switches), **the gun angle IS the camera pitch** — no clamp, no chase — so
  looking down bends the upper body forward by the pitch (`MarioWaistCtrl`, FLUDD or not) and the spray goes down; looking up lifts
  the spray nozzle (`chn_muzzle_1`, pass 15b), the turbo's neck, and the head by half the angle. A while in the mode is the camera's
  cancel, never a jump.
- Not ported: the enter/leave SEs (0x4824/5), the notice lock-on (`calcNoticeTargetYrot_`), the water particles' first-person
  survival tweak (`ModelWaterManager.cpp 536`), the talk/take suppression (no NPC system to suppress).

**Pass 17 — the Y camera, second pass** (Jaysonn 03:00: "y cam is very broken. no cam turning and its slow. flood does not work
on y cam and just other little bugs"). Three things, all real:
- **The rig's own Y mode was running underneath.** `SunshineCamera.UpdateOverShoulderState` toggles its OverShoulder mode on the same
  `button_y` and sets `Mario.CameraLocked`, whose early return at the top of `_PhysicsProcess` skips the whole SMS block: the L
  camera's stick never ticked (no turning), the lock never counted (no way out until Y toggled the rig's mode off again), the FLUDD tick
  never ran (no spray), and the rig's `UpdateBodyTracking` bent `chn_chest`. His log ends on the `[SMS ycam] on:` line. The rig's Y
  toggle is off under `UseSmsPhysics` (the Y camera is Mario's), and the rig gains `SmsYCamExit()` (below).
- **The camera runs at 120 Hz, not 30.** `data/PerformLists.bin` puts `camera 1` (the `PolarSubCamera`, `dolpic0/map/scene.bin`) on
  `PerformList Movement`, which the director performs with `CUE_MOVE` on every simulation tick — four a frame, like Mario (nothing sets
  the movement-gate flags on it; `CPolarSubCamera::perform` reads the first/last-tick bits). Every "a frame" rate in pass 16 was
  four times too slow: the stick turns **300 BAM a tick = 198°/s** (was 49), the pitch ratio 0.01 a tick (**the range in 0.83 s**, was
  3.3), the chase 0.1/0.08 a tick, **the lock is 60 ticks = 0.5 s** (was 2 s), the inbetween 16 ticks = 0.13 s. (Corroboration: the
  rig's own C-stick speed, tuned by feel, is 183°/s — the game's normal camera at 120 Hz is 250 BAM × 120 = 165°/s.)
- **The Y press is a sequence, not a mode change** (`execCameraModeChangeProc_` 596–628, `execFrontRotate_` 449, `CameraNormal.cpp`
  79–86): with no boss to lock on to, Y calls `execFrontRotate_`: the NORMAL camera's yaw halves its gap to "behind Mario" (`unk274 =
  face − 0x8000`, `YButtonRotateChase 2`, 180° in 16 ticks, position following at once), and a flag (`UNK8 → UNK10`) turns the L camera
  on when the swing settles — `changeCamMode_(L_NORMAL)` with the 16-tick `TCameraInbetween` (the look-at sweeps around him and the
  camera around the look-at in polar coordinates, then the 0.1/0.08 chase follows). The L camera's yaw is the swing's, so Mario — who
  faces the camera's look — keeps his facing; pass 16 started from the camera's current bearing and turned him to it. Mario is his own
  during the swing (the port too). On the way out (`changeCamMode_(-1)`) the normal camera resumes with the L yaw: behind the way he
  now faces — the rig snaps its yaw there (`SmsYCamExit`) while this camera sweeps onto its live pose over 16 ticks (1 for a cut).
`MarioYCam.cs` rewritten around a tick machine (Off → FrontRotate → LNormal → BlendOut) run from the top of the SMS block
(`SmsYCamFrame`, before any handler's `goto EndFrame`), so the hover / dash / hurt / water exits are seen whoever owns the frame; the
ground block only holds him (`SmsYCamHoldMario`: zero stick, jump and spin buffers cleared, idle) and takes the Y press. Mario's facing
is the game's `unk258 + 0x8000 − OffsetAngleY` from the CHASED camera (pass 16 used the target yaw + OffsetAngleY: 2.7° off).
`SmsParams.LCamera` holds the shipped values per tick; `SmsMath.ChaseAngleDecreaseS16` is `CLBChaseAngleDecrease` in s16 arithmetic.
Also ported: `onNeutralMarioKey()` at both ends of the mode (`changeCamModeSub_`): Mario's stick reads zero for 0.44 s and fades back in
over 0.22 s (`considerMarioStick`, `mNeutralKeyTimer 0x3c` at three counts a frame), so the stick that was aiming the camera does not
launch him when the mode ends. Self-test: `ycam: stick yaw at full deflection 197.75 deg/s`, `pitch range end to end 0.833 s`, `lock 0.5 s`, `swing behind from 180
deg 16 ticks`. Still not ported: a Y in the air (the game allows it — `execFrontRotate_` runs in any non-hip-drop status and only
sleeping / the surf / rocketing / the dash / climbing refuse; his air steering would fight the mode's facing), the squat inside the
mode (`canSquat` holds there: an R click or L squats him in the game), the notice lock-on, the SEs. Port-only: a Y needs the ground and
a standing / running / landing status; the mode cuts after 3 physics frames off the ground (the game keeps it through a fall).

**Pass 17b — a Y in the air, idle arms in the mode** (Jaysonn 03:50, on pass 17: "feels awesome just one bug i see right now is
that when i y cam mario should be idle then if i ress r he should be flood animation and also y cam should work in the air also").
- A Y in the air, as the game: `execCameraModeChangeProc_` starts the swing in any status but the hip drop (`execFrontRotate_` 453);
  only asleep / the surf / the hover or rocket / the dash / climbing refuse (588). `SmsYCamCanEnter` off the ground takes any state
  `doJumping()` steers (`SmsAirSteers`: the jumps, the flips, the dives, the rolls, a fall off an edge — not the hip drop, a wall
  slide, a hang). The press is read at the top of the frame (`SmsYCamFrame`), no longer in the ground block, and the mode keeps his
  status. In the mode A / B / space are the camera's (`MEANING_CAM_AB`): `Input.ActionRelease` on them after the tick — and on the
  frame whose press ENDED the mode too (`_ycamHoldThisFrame`), with the `jumpBuffer` the input block had set from it dropped — so no
  double jump, no dive, no A-held lift, on the ground or in the air (the first cut released only while `_ycamOn`, which the exit
  tick had just cleared: the exit A in the air fell through to a jump). His facing follows the look every tick in the air as on the
  ground (`playerControl` 2451: every status but the hang family): `SmsYCamFaceLook` latches the face as the launch source (2),
  which `SmsAirTick` keeps and projects the velocity onto — `mForwardVel` runs along `mFaceAngle`, so the flight bends with the
  look as the game's does. A landing inside the mode goes straight to the ground hold (WAIT). The port-only 3-frame off-the-ground
  cut is gone.
- Idle arms in the mode until R: deliberate deviation (below). `SmsParams.LCamera.HoldsPump = false`; R still pumps (`Pumping`) and
  the arms come down through `HoldingPump` → `Idle` after the release as anywhere else. The squat's hold is untouched.
- Verified headless with a scripted drive (temp, not committed): run → jump → double → triple, Y on the triple's first airborne
  frame: the swing (0° to go, the camera already behind him), the L camera on (lock 60, inbetween 16), stick right turns it 9000 BAM
  in 0.30 s (yaw −32115 → 22621 = 198°/s) with his face following (−176° → 129°) and the velocity bending with it (x 0.8 → −9.2 m/s,
  12 m up); the exit A at lock 0 while airborne: `off (Y / A / B)`, state still `tripleJump`, `jumpBuffer 0`, no jump, the 16-tick
  sweep back, the rig current 0.13 s later, the landing 0.7 s after that. A ground entry then: `upper Idle` through the lock, R →
  `Pumping` (R 1.00), release → `HoldingPump` → `Idle`, B → off. No errors.
- Debug: the lines are unchanged; `[SMS ycam] Y:` / `on:` now fire mid-air too.

**Pass 17c — his body turns with the look in the air; the grips are back in the mode** (Jaysonn 19:40, on 17b: "mario does grab
flod when hoding y cam. lets not do my deviation do it how the game has it ... mario turns not turn with the y cam but his direction
does. which is good but we need him to turn also to give him the y turn steer feel (just rotateing the armature in the air while
juming?)").
- The armature: pass 17b's `SmsYCamFaceLook` wrote the armature yaw on the ground only, trusting `RotateArmature()`'s face-source-2
  branch for the air — but nothing calls `RotateArmature()` off the ground, so the forced face bent his velocity (`SmsAirTick`) while
  his body kept its launch yaw. The game turns the model: `walkProcess()` and `jumpProcess()` end every ground and air tick with
  `mModelFaceAngle = mFaceAngle.y` (`MarioPhysics.cpp` 230 / 512), and `playerControl` (2452) forces the face before the state machine
  runs. Now the armature follows the look in every status but the hang family (`mStatus & 0x1FF` in `HANGING .. HANG_JUMPING`: the
  port's `ledgeHang` / `ledgeClimb` / `ledgeDescend` / `ledgeHopUp`, where the face is not forced either), except the spin jump, whose
  model angle is its own absolute spin (`rotateJumping()`: `mModelFaceAngle = ±timer × 4096`, `MarioJump.cpp` 1027 — `RotateSpinJump`
  keeps the armature), and the side flip, whose `+0x8000` (`uTurnJumping()`, 400) is the port's own locked offset, kept over the
  turning face. Not a deviation: 1:1.
- The grips: the pass-17b deviation (idle arms in the mode until R) is withdrawn by Jaysonn. `SmsParams.LCamera.HoldsPump` is gone; the
  upper body goes `HOLDING_PUMP` the moment the mode is on and `checkPumpEnable` holds (`checkPumping()`, `MarioUpper.cpp` 17), R
  pumps, the release comes back to the grips through the cooldown — pass 17's behaviour, which he had tested.
- Verified headless with the pass-17b scripted drive plus an armature readout (temp, not committed): the triple jump's Y, the stick
  right turning the camera with his face AND his armature following it in the air, the exit A with no jump, the landing; the ground
  entry with `upper HoldingPump` at once, R → `Pumping`, the release → `HoldingPump`, B → off.

**Pass 17d — the hover in the mode, still hands on the grips, the squat in the mode** (Jaysonn 20:08, on 17c: "when mario uses
the hover nozzle in the y cam it snaps back to the normal one. it needs to glide and not snap back to normal like sms"; "when in
the y cam the pumping animation shouldnt play? it should be still and then when you press R it pumps"; "Squat inside the Y cam
next (R click or L)").
- The hover: deliberate deviation (below). The game cuts the L camera in one frame the moment he is `ROCKET` / `ROCKET_LANDING`
  (`execCameraModeChangeProc_`, CameraChange.cpp 588: `isMarioRocketing()` → `doLButtonCameraOff_(true)`; the hover nozzle's
  hover IS `MARIO_STATUS_ROCKET`, `rocketCheck` 897: RocketType 1 is both nozzles), and refuses a Y while they last — which is the
  snap he saw. `SmsParams.LCamera.StaysOnRocketing`: the mode stays on through a hover / a launch and the fall after (his face and
  body follow the look, the hover's speed runs along the face as `rocketing()`'s does), and a Y during a hover enters.
- The pump clip: the tree in `Player.tscn` fed `CarryBlend`'s blend input straight from `Animation 2` (ma_pump), so the clip ran on
  its own clock — through `RunTimeScale`, at the run's rate — whenever the upper body was on the grips: `HOLDING_PUMP` pumped
  (unnoticed while the hold lasted only the PumpWaitTime cooldown; the L camera and the squat hold for as long as you like). The
  game: `addUpper()` binds the pump clip for PUMPING / HOLDING_PUMP and `changeMtxCalcSIAnmBQAnmTransform` inits its J3DFrameCtrl at
  frame 0, rate 1 (MarioBlend.cpp 11); with water in the tank the rate is never touched, so the clip ping-pongs at rate 1 while
  PUMPING and through the cooldown; then `stateMachineUpper()` does `setFrame(0)` every tick (MarioUpper.cpp 117): still hands on
  the grips until R. The tank's `wg_pump` is `setFrame(mMario->getPumpFrame())` at rate 0 (WaterGun.cpp 1594). Port: one clock
  (`SmsFluddPumpClock`) feeds the tree — rewired at start-up as `CarryBlend 1 ← PumpScale (0) ← PumpSeek ← Animation 2`, a seek
  every frame — the swim graft (`SmsUpperPose.Time`) and the tank (`SmsAnimScrub`). Not ported: the empty tank's `R × PumpAnmSpeed`
  rate (`addUpper` 1892).
- The squat in the mode: `canSquat()` (MarioMove.cpp 44) is `mInput & 0x200` = `MEANING_R` (the click) or `MEANING_L` held with the
  FLUDD out and not a RocketType-1 nozzle — and the pad still produces both in the L camera (MarioGamePad.cpp 77-79), so the R
  click or L squats him inside the mode. `squating()` there runs with Mario's stick zero and A as `MEANING_CAM_AB`: no turn, no
  sidestep, no backflip; R sprays where the camera looks (the gun angle is the camera pitch); both released → `SQUAT_STANDUP` → the
  hold (WAIT). The look-at (the nozzle top) lowers with him. Port: the ground block's hold takes the squat first (`SmsSquatFrame` /
  `StartCrouch` before `SmsYCamHoldMario`). His pad has no click: the full pull is the click (`SmsRClick`), so a full R in the mode
  squats him, as a GC trigger's click does in the game.

**Pass 17e — the shimmy's camera, the hover exit as a sweep** (Jaysonn 20:42, on 17d: "the shimmy right and left dont work well at
all need to take another pass camera spazzes out"; "Make the hover exit sweep smoothly instead of keeping the Y cam").
- The shimmy: the squat's L sidestep (`getSideWalkValues`) moves him along his flank at `mIntendedMag × sin(diff) × PumpingSlideSp`
  a tick with his face fixed — and the rig (`SunshineCamera`) answered L by locking "behind" him from his VELOCITY (`GetBehindYaw`:
  velocity first, the armature only when still), so the camera swung 90° to his flank while the stick was held and back when it
  rested, every move. The game's L is `execFrontRotate_` (CameraChange.cpp 447): the press edge (MEANING_CAM_L) starts a yaw chase
  to `*gpMarioAngleY - 0x8000` — behind his FACE — that `ctrlNormalOrTowerCamera_` (CameraNormal.cpp 79) runs with
  `CLBChaseAngleDecrease` at camera_normal.prm's `LButtonRotateChase` 10 a 120 Hz tick (180° in ~0.4 s; the Y press uses 2),
  skipping the stick rotate and the auto chase meanwhile; nothing on the hold or the release, no pitch / zoom reset. Under SMS the
  rig now does that (`SmsUpdateFrontRotate`, replacing its tap re-centre and held-L lock), and `GetBehindYaw()` is his face
  (`Mario.SmsBehindYaw`) for the auto-behind and the Y camera's exit too. The sidestep itself is unchanged (0.12 × 32 = 3.84 units a
  tick = 6.2 m/s at full stick, `ma_swlkl` / `ma_swlkr` at 0.5 × |side|).
- The hover: the mode ENDS on `ROCKET` as the game's does (pass 17d's stay-on is withdrawn), but the exit takes the 16-tick sweep
  of a Y / A / B cancel instead of the game's one-frame cut — the deviation below. `isMarioRocketing` also covers `ROCKET_LANDING`,
  so a Y is refused during the fall after a hover too (`_smsRocketLanding`), which the port had let in.

**Pass 17f — the squat end's flail** (Jaysonn 21:08, on 17e: "everything feels good! when mario does squat end tho he is kinda like
flailing his arms and jerks left with his upper body"). A probe (temp, not committed) measured the upper-body rotation tracks at t=0:
`ma_carry_p` vs `ma_pump` — jnt_arm_R1 176°, jnt_arm_L1 155°, jnt_hand_R 72°, jnt_head 36°, jnt_chest 22°; `ma_pump` vs `ma_sqend`
— 0° on every upper joint (the stand-up starts from the grip pose). When the upper body went IDLE at the stand-up (`HOLDING_PUMP`
→ `IDLE`, one frame into `ma_sqend`) the tree's `CarryBlend` faded 1 → 0 over 83 ms (`CARRY_BLEND_SPEED` 12) — but
`SmsFluddUpperBody` switched the blend's clip (`Animation 2`) to `ma_carry_p` on that same frame, so for five frames the arms
blended THROUGH the carrying pose: the flail, and the chest / head jerk. The game's `addUpper()` simply drops the upper mtx calc
(`unk3 = -1`), the body clip continuing from the very pose the grips held. The clip now stays `ma_pump` unless he carries something
(`heldBody != null`), so the fade runs from the grip pose into a clip that starts on it — nothing to see. The same fade ran after
every R release; a moving body hid it.

**Pass 18 — the HUD water gauge, the nozzle boxes** (Jaysonn 21:20: "lets move onto the HUD water gauge and hover, turbo and rocket
nozles boxes").
- The gauge (`assets/Sms/SmsWaterHud.cs`, a Control beside the other HUD nodes under Mario): `TGCConsole2` (GC2D/GCConsole2.cpp)
  drawing the `w_0` panes of `game_6/scrn/standard_1.blo`, read from the disc's screen file with `tools/projectshine/blo_dump.py` —
  a 600 x 480 screen, the group at (493, 355): `w_ba` water_back 88x86 alpha 120 tinted 0000FF (the disc), `w_t0` water_icon_0
  44x70 at (22, 9) (the tank), `w_t2` / `w_t3` water_icon_2 / 3 at (23, 16) / (39, 19) in 64DCFF / 00B4F0 (the water's two faces),
  `w_t1` water_icon_1 at (23, 10) in BEF0FF (the surface), `nz01..04` water_nozzle_01..04 with their `xb0N` X buttons (the current
  nozzle's icon), `w_tx` water_text_0 "WATER" at (4, 66). The textures are the disc's BTIs (`game_6/timg`, decoded with
  `tools/projectshine/bti2png.py`; the I4 ones exported white with the intensity as alpha, as the game's black / white colours use
  them) in `assets/Sms/hud/`. Behaviour from the code: `updateWaterGaugeFill` (fill = water / AmountMax, the surface's alpha 255 /
  0x50 / 0, the icon = mCurrentNozzle), `drawWater` (the faces up to the water line, the surface at tank.y + 47 x (1 - fill)),
  `drawWaterBack` (the disc blue at 0x78; a rocket / turbo charge fills it red (FF3F3F, 0xC8) by pressure / InsidePressureMax,
  pulsing red → yellow → red over 25 frames when full: `getPressureFlashColor`), `startAppearTank` / `processAppearTank` (the group
  rises 110 px over 20 frames, the disc, text, tank, icon popping in through 50 → -30 → 0 at frames 0 / 6 / 14 / 26),
  `startDisappearTank` (170 px down over 40 frames, in a cutscene hold), `updateWaterTankState` (the X button off 70 of every 270
  frames). The game's pixels scale with the viewport height / 480 and keep the bottom-right margins. Not ported: the Yoshi juice
  gauge, the tank SE at full (0x4807), the hint balloons.
- The nozzle boxes (`assets/Sms/SmsNozzleBox.cs`, a StaticBody3D to place in a level with its Nozzle set; `SmsNozzleItem.cs`):
  `TNozzleBox` (MoveBG/Item.cpp 1181-1338) — the dolpic0 `nozzlebox.bmd` (205.6 units a side; FinModelUtility's glb under
  `models/Koopa/scene/dolpic0/mapobj/`) with `nozzlebox.bmt`'s `J_nozzleBox` face (the FLUDD logo and four screws) tinted through
  TEVREG1: hover 0000FF, rocket FF0000, turbo 5A5A78. Mario landing on it (a TRAMPLE; not a belly slide) bounces him (`StompBounce`,
  the HIT_MESSAGE_ATTACK back) and breaks it: the item is thrown out of the front (`throwObjToFront`: 50 units up the base, along
  the box's +Z, the level's speeds — here the node's, 2 u/frame forward and 10 up), the crash model `nozzleboxcrash.bmd` plays its
  BCK (49 frames), the box is dead. The item (`TItemNozzle` on `nozzle_normal_physical_data`: gravity 0.35 u/frame², bounce 0.5,
  stop below 5, landing friction 0.8, no air drag; spinning 2° a frame as every item does; a 40 x 100 unit cylinder to touch; no
  timer — it waits) gives Mario the nozzle on touch: `TMario::getNozzle` (MarioReceiveMsg.cpp 18) = `changeNozzle(type, animate)`
  with the swap at 1 and the tank refilled (`Mario.SmsGetNozzle`), and every dead box of that nozzle comes back at once
  (`TItemManager::resetNozzleBoxesModel`; a short scale-in stands in for the smoke puff). Not ported: the valid / shut boxes before
  the nozzle right, the hint balloons, the SEs, the get sparkle. Jaysonn places the boxes (the editor is his).

**Pass 19 — the rest of the standard HUD, and the gauge smoothed** (Jaysonn 22:26: "nice when loading in however its a bit slow. can
you make it more smooth and while were here might as well fix the other hud elemets like coins and health and lives").
- The console's frames were stepped whole at 30 a second; on a 60+ Hz display that read as a stutter. `SmsHudCommon.cs` now carries
  the console's primitives evaluated at fractional frames — `TBoundPane::setPanePosition` (a quadratic through three points) with the
  console's 30-frame settle (`updateUpPaneState` / `updateDownPaneState`), `TExPane`'s linear slide — and the water gauge uses them.
  Re-read against `endCameraDemo` (2125) / `processAppearTank` (3194): the text, icon and disc are detached from the group in
  `loadAfter` and pop on their own (the tank alone rides the group's rise); each pop is 40 frames (the disc 50) from 50 / 100 below
  through -30, then 30 frames home; the tank and icon are hidden until their pops; the X button's blink starts with its 70 dark
  frames. The timings are the game's — the appear takes 96 frames (3.2 s) as it does on the disc; only the steps between them
  are gone.
- `assets/Sms/SmsStandardHud.cs`: the coin counter, the lives, the shine and blue coin counters, the red coin counter and the life
  meter, from `standard_1.blo`'s panes (c_0 (13, 152), d_0 (13, 113), s_0 (13, 74), m_0 (223, 74), l_0 (500, 50), r_0 (13, 419))
  and the disc's textures (38 more BTIs decoded into `assets/Sms/hud`), behaving as `TGCConsole2::perform` does: the coins and
  the lives drop in at the start (the coins to the shine row, `unk26A`), the digits with leading zeros (`setCounterDigits`,
  `setTwoDigits`), the coins rolling one per two frames; the shines and blue coins (the "star" HUD) drop in with the lives when
  Mario has stood still 200 frames, when a blue coin or a shine is taken, and go back up once settled with
  nothing on the clocks (`updateStarHudAutoHide`: 400 frames after a blue coin), the coin counter stepping down a row with them
  (`processDownCoin`, the 45-px dip); the life meter hidden at 8, in at a hit at (-150, +180) from its corner for 100 frames then
  curving home (`startAppearLife` / `processAppearLife`), the lit rays one a frame, the sun and "LIFE" dimmed to 7F below 4, the
  heartbeat (`updateLifeMeterBlink`: 3 px in for half of 200 frames; 160 / 120 / 70 frames and 5 px at 3 / 2 / 1), 120 frames at 8
  then up and away; the orange disc (FF4C00, alpha C8) behind it — the same `water_back.bti` as the gauge's blue one. Under water
  (the 240-tick air switch) the disc goes blue and the lit pieces through black 003CFF00 / white 00FFFFFF (baked copies of the
  textures: `SmsHud.Recolor`). A camera demo (the camera locked, a talk, the shine get, death) takes the lives, the coins and the
  star HUD away (the shine get brings the star HUD in instead), the life meter out at once, the red coins off the bottom; its end
  brings the coins back, the life meter from above at home (`startInsertLife`). His HUD nodes are hidden under UseSmsPhysics; the
  counts stay his fields, read every frame as the console reads the flag manager. Not ported: the counter sparkles (0x1FC), the
  SEs (0x4819 / 481A the moves, 0x4800 the heartbeat, 0x4801 / 4823 the segments), the pause fade, the telop, the timer, the
  balloon counters, the death-restart lives demo (flag 0x30002).
- 19b (Jaysonn 23:21: "all the uis are messed up now. too big and just not positioned correctly", "the animations of it are
  incredibly slow still and they seem to be coming on screen then auto going away even when mario is idle"). The frame: the
  console draws `standard_1.blo` 1:1 into the game's render rect — `J2DOrthoGraph(graphics->mViewportRect)` over
  `SMSGetGameRenderWidth / Height` = 640 x 448 (Resolution.cpp), the .blo's 600 x 480 being nominal — so the panes were 7 % small
  and the gauge anchored to the wrong edges (its disc sits 59 px from the right and 7 px from the bottom; "WATER" runs 3 px past
  the frame, into what a CRT never showed). `SmsHud.ScreenW / ScreenH` are the frame now, `SmsHud.Scale / LeftX / TopY / RightX /
  BottomY` place every group from its edge. His four asks are `SmsParams.Hud` (below): Size 0.7 (his own HUD had the game's
  textures 1:1 in the 648-line canvas, 0.69 of the frame's scale), EdgeInset 16 (the overscan as a margin), AnimSpeed 2 (the
  motions, not the waits), IdleHold (the peek stays while he stands). Headless drive re-run: the same states at half the frames.
- 19c (Jaysonn 23:42: "misplaced and the animation is still wayyyy to slow", with `Ref/HudRef.png`: arrows from the shine and
  lives counters up to the top edge, from the gauge to the corner). The lives counter is now centred on the window as the game
  centres it on its frame (`SmsHud.CenterX`; his own sat at canvas x 481..701 of 1152), EdgeInset became the four shifts (top
  -34, right 30, bottom -10), AnimSpeed 4.
- 19d (Jaysonn 23:51: "the speed is perfect now however we still have the allignemnt being off", the capture re-saved). Measured
  in it, every group sat exactly where the code puts it — what was off was the panes themselves: `blo_dump.py` read each pane's
  rotation and never printed it. In `standard_1.blo` the counters' strips, x's, digits and "MARIO" are turned 15° about their
  top-left corners (`J2DPane::makeMatrix`: about z by -mRotation in a y-down space = counter-clockwise on the screen, the right
  end up), the Mario head 355 (5° the other way), the icons upright, the gauge and the life meter unturned. That slant is the
  SMS look his own HUD had approximated with a -0.2 rad node rotation. `SmsHudPane.Rot`, drawn through the group's transform
  (`DrawSetTransformMatrix`). The dumper prints `ROT` now.
- 19e (Jaysonn 00:03: "nice can we shift it down just a bit from the top? ... you updated the timer and red coin huds also right?").
  TopShift -34 → -22. The red coins were in since 19; the timer was not — now `t_0`: "TIME" (time_text, turned 13), the 270-px
  strip, the mm:ss:cc layout (`t_1`) for a count-up or a countdown from 60 s, ss:cc (`t_2`) below that (startAppearTimer 2451),
  rising like the red coins with the digits at 24 + 6i and the colons at 32 / 44 (startInsertTimer 2483, processAppearTimer
  3513), the stopwatch every frame in centiseconds (setTimer 2920; the short layout's digits and colon FF82FF under 10 s, the
  tick SE not ported), away off the bottom (startDisappearTimer 2444) and parked for a demo (startDownLeftBot), the red coins 73
  px above it while it is out. Fed from his `ShineTimer` (found in the level tree; his `TimerHud` nodes hidden): a start brings
  it in, the target reached takes it away, a stop before that freezes it as `stopMoveTimer` does.
- 19f: the timer came back after a demo only when the demo had parked it; a death hides every pane at once and left a running
  timer off for good. `endCameraDemo` re-inserts it whenever the `ShineTimer` still runs. Centiseconds floored as the game's
  `(u32)(ms * 0.1)`, not rounded.
- 20 (Jaysonn 00:45: "have we been making all these changes without being able to see them in the editor? i wanna see fludd in
  the player file and such"). Yes: everything the port shows was built by code when the game ran -- the pack, the nozzle boxes'
  models, the HUD -- so the editor showed none of it. Now: the FLUDD's model assembly lives in `SmsFluddModel` (the tank on the
  attachment, the nozzles on `nozzle_center`, the MAT3 fixes), used by `SmsFluddSetup` at run time and by `SmsFluddAttach.cs`,
  a `[Tool]` BoneAttachment3D for Player.tscn (Armature/Skeleton3D/SmsFluddAttach, bone jnt_chest) that hangs the same pack,
  housed, on the bone in the editor as unsaved children; `SmsFluddSetup` adopts that node when the scene has it (its bone
  forced) and makes its own otherwise, so the game is unchanged either way. `SmsNozzleBox` is a `[Tool]` too: in the editor it
  shows the box's tinted model and retints it when `Nozzle` changes; its gameplay (`_Ready` / `_PhysicsProcess` / the group)
  is gated on `Engine.IsEditorHint()`. The nozzle items wear his textured exports (`models/Nozzles/<Hover|Rocket|Turbo>/`)
  when present (18b). The HUD stays a run-time overlay, as the game's is.
- 21 (Jaysonn 00:45: "i want everything we do to always use a scene so i can see it in the editor. i dont want code to
  dominate how things are loaded in ... i want it to be built to be customizible"). A standing rule from here on: what the
  port shows is a scene with exported knobs; scripts drive behaviour and read what the scene provides; the physics numbers stay
  the game's in `SmsParams`. First conversions: `assets/Sms/Fludd.tscn` (`SmsFludd`: the tank glb with the MAT3 materials as
  surface overrides on its mesh, `NozzleSeat` a BoneAttachment3D riding the tank skeleton's nozzle_center through
  `external_skeleton`, the four nozzle glbs under it wearing the frame's atlas; exports Tank / NozzleSeat / the nozzles /
  AtlasMaterial) -- instanced under Player.tscn's `SmsFluddAttach`, which `SmsFluddSetup` adopts; the FLUDD setup print is
  byte-identical to the code-built pack, which stays as the fallback with a warning. `models/Nozzles/NozzleBox.tscn` (the
  collision box, the stomp zone, the box and crash models with one local-to-scene face material the script tints for the
  nozzle, the three item scenes as exports) and `models/Nozzles/{Hover,Rocket,Turbo}/*NozzleItem.tscn` (his textured model +
  the 40 x 100 unit Hit cylinder). The pass-20 editor previews are gone with them; `SmsFluddAttach.cs` only defaults the bone
  and shows a configuration warning until the scene is in.
- 21b (Jaysonn 01:28: "the nozzle boxes have a wierd see through proptery ... at some angles its like transparent. and the
  item comming out of the box is slow just like the hud menu was"; "items also dont have textures they are just white models.
  and fludd also has materials attached to it so it looks oddly shiny"). The rips import with glTF's default metallic 1 --
  that is the shine; every FLUDD surface now wears a scene material with metallic 0 and no specular: `FluddFrame.tres` (the
  atlas x vertex colours) on the tank frame and floaters, the shell as before, `FluddAtlas.tres` on the nozzles. The items
  were white because their BMDs carry no texture of their own -- texture 0 is the 8x8 H_watergun_main_dummy the game swaps
  for the FLUDD atlas (nozzleitem.bmt) -- so the item scenes put `FluddAtlas.tres` on the model, as the game does; the box
  face is drawn on both sides now (cull off) against the see-through. The item's motion is the game's (gravity 0.35 a frame,
  bounce 0.5, the throw speeds level data we do not have) and floats for two seconds; `SmsNozzleItem.Speed` runs its clock
  faster, 3 by default -- a deviation, logged below.
- 22 (Jaysonn 01:59, `Ref/NozzleBoxRef.png` against `Ref/real.png`: "the colors are off"; "when mario hard clicks with turbo he
  should not sqaut he should do his foward walk when chagring turbo up"). The game's box is a white panel whose logo, screws and
  frame take the nozzle's colour; the port had multiplied the whole face by it (a red panel with a black logo). nozzlebox.bmt's
  TEV stage is C1 x (1 - tex) + tex -- `NozzleBoxFace.gdshader`, a ShaderMaterial in NozzleBox.tscn with `tint` and `face`
  uniforms; the script sets `tint`. The see-through boxes in his capture were the editor's Debug > Visible Collision Shapes.
  `canSquat()` (MarioMove.cpp 44) refuses the turbo outright (`mCurrentNozzle != 5`) besides the RocketType-1 nozzles; the port
  had only the RocketType check, so the R click with the turbo squatted him instead of charging. Drive: the turbo on, R
  clicked while he stands -- no squat, he walks then sprints as mDashSpeed climbs 30 -> 32, FLUDD_EMITTING at 32.
- 23 (Jaysonn 01:59: "Do the HUD as a scene next"). The console is `assets/Sms/Console/SmsHud.tscn`: a CanvasLayer (`SmsHudLayer`,
  the knobs below as its exports) with the StandardHud and the WaterHud as Controls, every pane of standard_1.blo a TextureRect
  laid out on the 640 x 448 frame as the .blo has it -- a `SmsHudGroup` per group (Coins, BlueCoins, Shines, LifeMeter, Lives,
  RedCoins, Timer; its place in the scene its place on the frame, its `Anchor` the window edge it keeps to) with Strip / Icon / X /
  Digit0..2 and the rest as children; the gauge's pieces as children of the WaterHud, the cut draws (the water's faces at the
  water line, the pressure gauge's red) as clip Controls the code sizes. The digit pictures are the StandardHud's `Digits` export;
  the air recolour is `J2DBlackWhite.gdshader` on the rays and "LIFE" (the TEV's black / white, set by the code) in place of the
  baked copies. `SmsHudPane` wraps a node: the code reads the layout from the scene and moves the nodes -- nothing is drawn or
  loaded by code any more; `SmsParams.Hud` is gone. Mario finds the scene under himself (Player.tscn) and instances it from the
  file, with a warning, when it is not there. The HUD drives (the counters, the life meter, the demo park, the timer) run as
  before on the nodes.
- 24 (Jaysonn 02:33: "if i disable fludd for lets say a screct stage it will take that hud away?"). MARIO_FLAG_HAS_FLUDD per
  stage: `Level.HasFludd`, an export on his Level scene root (on by default; a secret course unticks it). `SmsFluddSetup` reads
  the Level above Mario: off, the scene's Fludd stays hidden under its seat, `_fluddReady` never turns on -- so the gauge never
  comes (its startAppearTank waits on the pack), R and X do nothing, the pump clips and the dash never start. Drive: HasFludd
  off, the turbo forced on and R held -- he stands idle, nothing reads R, no gauge.
- 25 (Jaysonn 02:41: "mario should walk whichever way hes facing ... he kinda studdters if he was trying to start it. and if
  stick is netural he just goes what seems to be a pre rgramed direction"; "we should have the option to pick up fludd when hes
  placed on the ground ... think pianta3"). The turbo's resting stick (mIntendedYaw = mFaceAngle.y) now aims where the model
  LOOKS -- the armature's yaw read back -- and latches it as the face and the legacy facing, where it had used the cached SMS
  face: after a turn the two could differ, the charge set off along the cached one and the run's first ticks swung him round.
  MARIO_FLAG_HAS_FLUDD is a run-time flag now: `getNozzle` turns it on (MarioReceiveMsg.cpp 22), so any nozzle item -- and
  `assets/Sms/FluddItem.tscn`, the game's watergun_item (0x2000001F): the pack itself lying in the stage, Nozzle = Spray -- gives
  the FLUDD back: the pack is built then, the seat shown, the gauge comes in on its own. SmsNozzleItem has its own
  SmsNozzleKind (Spray included). Drives: no FLUDD, the item touched -> the pack, the gauge, the spray nozzle, the tank full;
  the turbo from a stand after a run -> along the facing, no swing.
- 26 (Jaysonn 03:40: "it feels like neither thing about the turbo was fixed. looks like hes sqauting before using turbo still
  and when netrual he is still going not the direction hes facing"; "rocket doesnt studder if that helps"). Found with a
  telemetry drive and a 4 percent stick drift -- a real pad at rest, under every deadzone: the LEGACY PIVOT (turn in place on
  a stick between 0.06 and 0.2) fired for five frames of every charge. SmsFluddDashOverride makes the dash speed the stick
  (mIntendedMag = mDashSpeed, the game's rule), and 2..6 of 32 sits in the pivot's band while aimDirWorld still held the pad's
  drift: the pivot killed his speed, swung the armature toward the drift (camera-relative, so always the same way), and the
  charge then set off along the turned model -- the "pre-programmed direction"; the five dead frames, ma_pivot and the snap
  were the "squat" / stutter. The rocket never moves, so it never reached the pivot. An exactly-zero (headless) stick never
  showed it. The pivot is skipped while the dash speed drives the stick (the game: WAIT -> RUN at once, no turn-in-place).
  Also, a deviation (below): no SSTEP tiptoe during the charge -- the run blend from the first tick. `[SMS turbo] charge on
  (from idle): stick 0.0 of 32, model 109 deg, face 109 deg, fwd 0.0` prints at every charge start. The FLUDD item is the
  game's own sitting model now: `models/Koopa/scene/monte2/mapobj/watergun_item/` (Pianta 3 / the airstrip, one file on the
  disc) in `FluddItem.tscn`, its bake as `assets/Sms/WatergunItem.tres`, the tank shell translucent as on the pack; no spin,
  no fall (`SmsNozzleItem.Spin` / `Falls` exports, both on for the nozzle items): watergun_item_data has no physics and the
  0x04000000 flag that keeps it out of the item manager's shared rotation. Drives: the charge with the drift -> no pivot,
  the run blend from the first frame, along the facing; the sitting item touched -> the pack, the gauge, the spray, full.
- 26b (Jaysonn 20:48: "Still off ... when charging up he should have to stand still for a frame or whatever is happening";
  his log: every charge `from sprinting: stick 32.0 of 32 ... fwd 32.0`). That stop is the game's own wind-up: mDashSpeed
  starts at 0 and mIntendedMag = mDashSpeed, so doRunning brakes a full sprint to a crawl at DecBrake 5 a tick (6 ticks,
  0.05 s) and the 0.53 s climb starts from there. Deviation (below): the charge picks up at the forward speed he already has
  along his face -- a sprint keeps 32 until the trigger fills (0.75 s), then the dash; from a stand nothing changes. The
  charge-on line now ends `dash speed from N`. Drive: R at a 32 sprint -> 32 held, no dip, the dash 0.75 s later.
- 27 (Jaysonn 21:18: "lets do 1 and 2. make them secenes so they ccan be easily iterated on later"). THE TWO SECONDARY-MOTION
  SYSTEMS AS SCENES. `Fludd.tscn` carries `Tank/Node/Skeleton3D/HandleSwing` (SmsSwingRZ, `SmsFludd.HandleSwing`): the grips'
  pendulum with its acceleration, rope and brake as exports. What the data says: TMultiMtxEffect::setup names the prm by the
  JOINT index (`/Mario/WaterGun/MtxEffect%d.prm`, MtxUtil.cpp 308), jnt_G_handle is joint 7, and no MtxEffect7.prm ships -- retail
  runs TMtxSwingRZ's compiled-in defaults, acc (0, 0, 0) / brake 0.9: no gravity, the grips stay where the last move left them.
  The scene ships (0, -4, 0) / 0.7 -- the disc's MtxEffect10 / 12 for an older rig -- the deviation logged in pass 7 (Jaysonn:
  "floods arms look like they are doing weird things"). Zero the acceleration in the scene for retail's look. `assets/Sms/
  BodyLag.tscn` is the body's TMtxTimeLag (SmsTimeLag) with one SmsTimeLagEntry resource per bone -- jnt_chest MtxEffect14
  0.1 / 0.9 / 5 / 1.0 / 0.5, jnt_arm_R1 and L1 MtxEffect16 / 22 0.1 / 0.8 / 5 / 0.05 / 0.8, the shipped values -- for him to
  instance under `Armature/Skeleton3D` in Player.tscn; Mario adopts it there (moved after SmsWaistLean) and builds a copy with a
  warning naming the scene when it is missing. The forearm lag (13-47 deg at a run) is those shipped numbers; the knobs are now
  in the editor for the Dolphin comparison. `[SMS jiggle] ... (from the scene)` says which was used.
  THE BOX PACKAGE. (a) The shut box: TNozzleBox's "valid" (unk15C) -- a rocket / turbo box is a HOLOGRAM until the nozzle right
  is won: anim 3 is the model nozzleboxalpha.bmd (MapObjInit.cpp NozzleBox_anim_data), drawn translucent in the nozzle's colour at
  alpha 100 (unk15E.a 0x64); HIT_FLAG_CANNOT_GET_HIT -- no trample and nothing to stand on. `NozzleBox.tscn`: the Hologram model
  (models/Koopa/scene/dolpic0/mapobj/nozzleboxalpha, its material the scene's), `Valid` (the level data; off = the hologram until
  `SmsNozzleBox.GrantRight(node, nozzle)` -- TFlagManager::setNozzleRight, his progression's call; a hover box is always valid, as
  loadAfter has it), `BreakSound` (SE 0x3801, an empty stream slot). (b) The balloons: touching the hologram brings the FLUDD's
  help balloon with balloon.bmg 0x59 "This is one of my nozzles, the... Oh, you cannot use it yet. It is only a hologram."; a
  hover box before any nozzle right, 0x5A "This is one of my nozzles, the Gadd Science, Inc., Hover Nozzle!" (the decomp's ids
  0xE0056 / 57 are three entries off the US disc's) -- both texts exports on the box. `assets/Sms/Console/HintBalloon.tscn`
  (SmsHintBalloon, instanced in SmsHud.tscn, `SmsHudLayer.Balloon`, `SmsHudLayer.Current.ShowBalloon(text)`): the 'he_w' window
  at (397, 128) 194 x 216 with the four help_window pieces composed as J2DWindow draws them into hud/help_window.png (a NinePatch,
  the contents blue 0000FF at alpha 146 baked in), the Label's LabelSettings the text (20 x 26, white over a black copy 2 right,
  3 down), the FLUDD's voice a stream slot. The motion is GCConsole2.cpp 2630 / 3581 / 3610: alpha +0x30 a frame, then 30 px a
  frame from the bottom edge, the text typed 2 characters a frame, held 0.7 x length + 100 frames, then down and out; a
  higher-priority message replaces, a new one queues behind the close. (c) The get effect: emitGetEffect (MS_ITEMGET1_A + SE
  0x1989; the boxes' items also MSD_SE_SY_GET_NOZZLE) -- `assets/Sms/Fx/GetSparkle.tscn`, a one-shot star burst with a sound
  slot, the `GetEffect` export of every item scene, instanced at Mario's chest when an item is taken.
- 28 (Jaysonn 22:16: the balloon "a bit high", the font "off", the sparkles "supposed to follow mario", "we can also just fix the
  spin jump and ground pound effects. right?"). (a) THE BALLOON: it took the console's Size (0.7) and TopShift on its frame place,
  so it sat at 17 % of the window's height where the game has it at 128 / 448 = 29 %; it now fits the 640 x 448 frame to the
  window's height with its own `SizeScale` (1 = the game) and `ShiftFrame`, the console's knobs left to the counters. The text box
  is he_2's: (415, 140) 164 x 166 in the window -- 18 in from the left, 12 from the top, 12 from the right, over the tail piece
  (ContentInset (18, 12, 12, 38)); the font is his Delfino.ttf (the fan cut of the game's) at 20 px on the game's 28 px line
  (`assets/Sms/Console/HintBalloonText.tres`: Delfino's height 24 + line_spacing 4; 'yet. It is only' fits the 164). (b) THE
  PARTICLES FROM THE DISC: the .jpa files are JEFFjpa1 (tools/jpa_dump.py reads them after noclip's JPA.ts): BEM1 emitter, BSP1
  shape (a quad is 50 x base size x scale game units, +-25 per side), ESP1 life curves, SSP1 children, FLD1 fields, KFA1 keys,
  TEX1 textures (BTI; a mirror-wrapped texture at tiling 2 is a quarter picture -- the star). The TEV colour is baked into RGB
  (lerp(env -> prm, I); flat prm), the intensity into the alpha (tools/jpa_bake.py -> assets/Sms/Fx/tex). Port units: 74.44 a
  metre, 30 game frames a second. The get effect (ms_itemget1_a): emitAndBindToPosPtr(&unk160) -- unk160 is joint 1 'center''s
  anim-matrix translation (MarioDraw.cpp 834), so the emitter rides on that joint (+15 units) and FollowEmitter moves the stars
  with it: `SmsFollowEffect` (Bind(owner) -> the skeleton's 'center', process priority 100) is the base of every bound effect;
  the item binds the sparkle to Mario instead of dropping it in place. The stars: 0.8 a frame for 120 frames, 35 +-12.5 % each,
  from a cylinder r 15..60 h +-60 (directed emission points, the normals outward from the axis = 'vel axis' 2 units a frame), a
  +0.2 unit/frame^2 lift field, a vortex of 7.5 (orbit velocity), 125 x 140-unit billboards of P_msm_star4s (additive), prm pink
  -> yellow over the first 16 frames, alpha 0.4 -> 0.875 -> 0 from 62.5 %, one P_msm_stardust1 child every 20 frames (sub-emitter,
  life 20, a 0.28 fall). (c) THE SPIN JUMP: emitBlurSpinJump each frame (MarioJump.cpp 1022) -> ms_m_blur2 + blur2sp bound to
  getCenterAnmMtx() with the keep-alive flag (EmitterViewObj.cpp 352): flat Direction quads (the quad's up = the emitter's Z, its
  width = the emitter's X, the plane the ground's) of 100 and 55 units on a vertical line +-25 / +-50 through the centre joint +15,
  3 a frame living 6, P_msm_hamonring3 turning -0.3 turns a frame, alpha in by 37.5 % out from 62.5 %; the large ones lerp
  (183,231,255) -> white at alpha 159, the small flat (63,87,255) after one frame of (255,87,72,183) --
  `assets/Sms/Fx/SpinJumpBlur.tscn` (SmsSpinJumpBlur.Active = the spin). (d) THE HIP DROP: emitBlurHipDrop each frame of the fall
  (MarioJump.cpp 1140) -> ms_m_blur3, a StripeCross: a point a frame at the centre joint (0, -20, 14.6), 10 frames of them threaded
  into two crossed ribbons 100 units wide, P_msm_blur1 (lerp (39,63,255) -> (255,39,15), scaled 1.75 x 0.1, scrolling 0.2 a frame)
  through P_msm_blurmsk1 -- `assets/Sms/Fx/HipDropBlur.tscn` (an ImmediateMesh ribbon under stripe_blur.gdshader). The landing
  (MarioJump.cpp 1184): ms_hipdrop_a 12 DirectionCross streaks (P_msm_radgrad blue -> white) out at 22 and up at 12 units a frame
  from a 5-unit circle, 200 x 33 units grown in by 29 % and out from 67 %, alpha 1 -> 0.45 by 12.5 %, gone from 50 %, 20 frames
  +-25 %; ms_hipdrop_b 20 P_msm_kemuball3 puffs (lerp (26,26,2) -> (255,255,242)) from an 18-unit ring 80 up, out at 20 units a
  frame under a 0.88/frame drag fading in over 23 % (a linear damping of 8 m/s^2 lands them at the game's 4.1 m), 125 units at full
  size (0.085 -> 1 by 33 % -> 0.2, +-35 %), alpha in by 5 % out from 41 %, 75 frames +-47 %; ms_hipdrop_c one flat 250-unit
  P_msm_shwave1 ring (lerp (255,144,0) -> white) 7 up for 18 frames, fading from 25 % -- `assets/Sms/Fx/HipDropLanding.tscn`
  (SmsHipDropLanding.Play(feet)). Mario.cs adopts the three from under itself (SmsSetupFx) and turns the legacy SpinJumpEffects /
  GroundPoundEffects / GroundPoundImpactFx + landing dust off where a scene is present; a missing scene keeps the legacy one with a
  warning naming the scene to instance. Not carried: the stars' alpha sine flicker (ESP1 wave type 1), the blur3 stripe's indirect
  warp (the mask multiplies instead), the vortex as a constant turn rate (the game's is a constant tangential speed).
Debug: `[SMS hover] start from singleJump: target +230 units (3.09 m) over feet y 1.53, fwd 12.0 (4.8 m/s), water 9800, R 0.61`,
`1.0 s: +1.39 m of 3.09, vy 2.53, fwd 8.9 (3.6 m/s), water 9560` (each second), `end (R released) after 212 ticks (1.77 s):
rose 1.98 m = 147 units of the 230, fwd 7.1 (2.9 m/s), vy 0.6, water 9376`, `a roof overhead: speed 0 …`, `[SMS wall] air hit
at fwd 22.0 after a hover (ROCKET_LANDING): stop against the wall, no slide`; setup: `hover nozzle bank joints: jnt_nozzle_L
yes, jnt_nozzle_R yes`. Self-test: `hover: first tick vy 4.6`, `rise after the 360-tick window 192.1`, `rise after 1 s 104.0`.
Next: S2b the squat aim (needs `button_rclick`), S4 rocket, S5 turbo (surf).

### Deliberate deviations from the game
- The help balloon's text is Jaysonn's Delfino.ttf (`assets/Sms/Console/HintBalloonText.tres`), the fan cut of the game's
  standard_font.bfn, which is not decoded; the Mario particles are GPUParticles3D built from the disc's .jpa numbers, with the
  JPA fields approximated (the vortex as an orbit rate, the 0.88/frame drag as a linear damping, the alpha sine flicker and
  the stripe's indirect warp dropped); the sounds are empty stream slots (no SE archive is decoded). Each is a scene resource.
- The turbo charge keeps his speed (`SmsParams.Fludd.ChargeKeepsSpeed`, true): the game's `mDashSpeed` starts at 0 whatever he
  was doing, and `mIntendedMag = mDashSpeed` brakes a sprint to a crawl in 6 ticks (DecBrake 5) before the 0.53 s climb -- the
  wind-up. Jaysonn (20:48): "when charging up he should have to stand still for a frame or whatever is happening". Here the
  dash speed starts at the forward speed he already carries along his face. `false` = the game.
- No tiptoe in the turbo charge (`SmsParams.Fludd.ChargeSoftStep`, false): `doRunningAnimation` picks SSTEP while
  min(mDashSpeed, mForwardVel) is under Walk2Soft (8) -- the first 26 ticks of a charge from a stand, back to the run above
  Soft2Walk (13). Jaysonn (01:59): "he should do his foward walk when chagring turbo up", (03:40) "looks like hes sqauting
  before using turbo". The charge holds the walk / run blend from its first tick; the speeds are the game's. `true` = the game.
- The nozzle item's clock (`SmsNozzleItem.Speed`, an export on the item scenes) runs at 3; the game's map objects run at 1 --
  the thrown nozzle floats down on gravity 0.35 a frame and bounces for about two seconds before it can be taken. Jaysonn
  (01:28): "the item comming out of the box is slow just like the hud menu was".

- ~~**The waist lean rotates the chest bone, not only its children** (2026-09-27 04:29)~~ — WITHDRAWN in pass 6: on chn_chest the
  game itself rotates jnt_chest and everything under it. J3D applies `MarioWaistCtrl`'s rotation
  after the chest's own matrix is stored, so in the game only `jnt_head`, `jnt_sldr_L` / `R` and what hangs under them take the
  lean; the chest's skin keeps the clip and the smooth chest/shoulder envelopes (`EVP1`: 0.3/0.7, 0.5/0.5, 0.15/0.7/0.15) bend
  between. Our glb's shirt is rigid on `jnt_chest` (138 of 239 vertices; 2–4 chest/shoulder blends), so the literal port
  (ad0ed9e–16ccd6d) pulled the shoulders 19 units off the chest into a hard hunch ("really off-putting"). `SmsWaistLean` now
  post-multiplies the chest bone's pose by `Rz(pitch) × Rx(roll)`: head, shoulders and arms land exactly where the game's
  arithmetic puts them; the torso skin leans with them as one piece. Skinning only; angles, chase and gating are the code's.
  If the shirt ever gets the BMD's blends back, the children-only version is in git (ad0ed9e's `SmsWaistLean.cs`).
- ~~**The waist twist's sign is reversed** (2026-09-27 04:18)~~ — WITHDRAWN in pass 6: the callback is on chn_chest, where the
  game's own sign banks into the turn. `considerWaist` makes `mWaistRoll` positive for a left turn (`diffAngle`
  = this tick's face change, +Y = counterclockwise = left), `MarioWaistCtrl` applies `MsMtxSetRotRPH(roll, 0, pitch)` =
  `Rz(pitch) × Rx(roll)` on `jnt_chest` (X up the spine, Y the front, Z his left — verified in the BMD, the rig, and by the pack
  sitting on his back), and the surf's body bank uses `mRoll = −0.45` with the same `delta`, so the code's intent is unambiguous:
  the chest and head turn **into** the turn. Twisting before the forward pitch drops the shoulder that came forward — the *outside*
  one — by `asin(sin roll × sin pitch)`, 27° at the caps, which from behind reads as leaning out. Jaysonn has reported that look as
  the wrong way three times (e46bc07, 63ebb3c, a120537) against his memory of the game leaning in. Ported with the applied sign
  negated (`SmsWaistApply`): the inside shoulder drops, chest and head face out of the turn by the same angle. The value keeps the
  game's sign; the prints report the applied direction. **To settle it:** Dolphin, tight circles at full speed — which shoulder is
  low, and does his chest face the inside of the circle? If the inside, drop the minus.
- **The FLUDD grips' pendulum has gravity** (2026-09-28 00:08). The shipped game runs `TMtxSwingRZ` on `jnt_G_handle` with the
  compiled-in defaults — acc (0, 0, 0), brake 0.9 — because the model's handle joint is 7 and no `MtxEffect7.prm` ships (pass 7).
  Ported that way first; Jaysonn: "floods arms look like they are doing weird things like going upside down" — the grips stay
  wherever the last move left them. Now the shipped `MtxEffect10 / 12.prm` values, the only tuning ever written for it: acc
  (0, −4, 0), L 50, brake 0.7 (`SmsParams.MtxEffect.Handle*`). If Dolphin shows the retail grips frozen backwards, the defaults are
  one line away.
- **The turbo dash turns at 300 BAM a tick, not 1500** (2026-09-28 01:02 → 600; 01:40 → 300). `doRunning` uses `RunningRotSpMin`
  (1500) while `FLUDD_EMITTING` — a 90° turn in 0.09 s, a 2 m radius at 32 m/s. Jaysonn called that too sharp twice, and 600
  (0.23 s, 5 m) still too sharp: "harder to steer … bigger wider turns". 300 is 0.45 s and a 9 m radius. `SmsParams.Fludd.DashRotSp`.
- **The turbo dash clip is paced by the actual speed** (2026-09-28 01:02). The game paces it by `mDashSpeed`, capped at 32 — the
  run's tempo (2.76) at 2.5× the ground speed; he reads that as slow. Now `mForwardVel` × 0.055 + 1 = 5.4 at 80.
  `SmsParams.Fludd.DashAnimByForwardVel`.
- **The hover's exit from the L camera sweeps** (2026-09-28 20:45 → the mode stayed on; 20:55 → it ends with the sweep).
  `execCameraModeChangeProc_` (CameraChange.cpp 588) cuts the L camera in ONE frame (`doLButtonCameraOff_(true)` →
  `changeCamModeSpecifyFrame_(-1, 1)`) the moment `isMarioRocketing()` — `MARIO_STATUS_ROCKET` or `ROCKET_LANDING`, the hover
  nozzle's hover included — and refuses a Y while it lasts; the port did the same and Jaysonn saw the snap ("it needs to glide and
  not snap back to normal like sms"). Pass 17d kept the mode on through the hover; he chose the smaller change: the exit takes the
  16-tick sweep of a Y / A / B cancel (`InbetLNormal`) instead of the frame cut. The mode still ends, a Y is still refused while he
  hovers or falls out of one. `SmsParams.LCamera.RocketExitSweeps` (true; false = the game's cut).
- ~~**Idle arms in the L camera until R** (2026-09-28 03:53)~~ — WITHDRAWN in pass 17c by Jaysonn ("mario does grab flod when hoding
  y cam. lets not do my deviation do it how the game has it"): `checkPumping()` (`MarioUpper.cpp` 17) puts the upper body on the
  grips (`HOLDING_PUMP`, the pump clip's frame 0) the moment `isLButtonCamera` && `checkPumpEnable`, and so does the port again;
  `SmsParams.LCamera.HoldsPump` is removed.
- **The hidden HUD strips park fully above the frame** (2026-09-28 22:50). `initHiddenPaneAbove` / `startAppearMario` /
  `startAppearStar` park a group at -(y2 + 1) of its own 10 x 10 pane, which leaves 11 px of the 22-px strip inside the 480-line
  frame (the CRT's overscan hid it; Dolphin shows the sliver). Here they park a strip's height further up; the slides are the
  game's frames over the slightly longer way.
- **The shine count flips when Mario's shine get hands it over** (2026-09-28 22:50). The game's flag changes as the shine is taken
  and the console flips the digits 252 frames into the shine dance (`updateCounterState` 995); his shine get is his own length and
  `AddShine` fires 2 s in, so the digits follow it. The star HUD then stays out for the blue coins' 400 frames (in the game the
  level ends before `unk8A` — a u16 that never resets — would let it go).
- **A lost life shows the counter too** (2026-09-28 22:50). The game shows the lives on a death through the restart demo (flag
  0x30002, `startAppearMario(true)`: the old count 200 frames, then the new); his respawn is in place, so any change brings the
  counter in with the new count (`updateMarioLifeCounter` does that for a gain).
- **The HUD is 0.7 of the game's size** (2026-09-28 23:21, "too big"). At 1 the frame's 448 lines fill the window's height and a
  coin digit is 8 % of the screen — the game on a TV. His own HUD had the disc's textures 1:1 in the 648-line canvas, 0.69 of
  that. `Size` on SmsHud.tscn's root (SmsHudLayer; `SmsParams.Hud` until pass 23); 1 = the game.
- **The HUD's rows move toward the window's edges** (2026-09-28 23:21 → 23:42, "misplaced", `Ref/HudRef.png`: arrows from the
  counters to the top edge, from the gauge to the corner). The game's top rows sit at y 74 of 448 (a CRT hid ~22 of them), the
  gauge 59 px from the right with "WATER" 3 px past the bottom; his own HUD had the strips 40 px from the top. The scene root's
  `TopShift -34 / RightShift 30 / BottomShift -10 / LeftShift 0`, game pixels; 0 = the game (Dolphin's look). Not a deviation:
  the lives strip (223..421 of 640) is centred on the frame, so on a wide window it is centred on the window.
- **The HUD's motions run at four times the game's pace** (2026-09-28 23:21 "incredibly slow" → 2; 23:42 "wayyyy to slow" → 4;
  22:26 "a bit slow"). The console's
  quadratics (50 / 40 frames), settles (30), slides (20 / 40 / 50), staggers (14 / 24 / 28 ...) and the life meter's 100-frame
  mid-screen hold are the game's at 30 fps — a counter takes 4 s to settle on the disc. the root's `AnimSpeed` (4) divides
  them; the waits (the idle 200, a blue coin's 400, the 120 at full health, the X blink, the heartbeat, the coin roll) stay.
- **The idle peek stays while Mario stands** (2026-09-28 23:21, "auto going away even when mario is idle").
  `updateStarHudAutoHide` (1061) takes the shine / blue coin counters and the lives away the moment they have settled and
  nothing is on the clocks, idle or not; `waitForStarHud` (934) then brings them back 200 frames later — a peek every 320
  frames while he stands. The console has an `unk60` guard on that hide which the game never sets; the root's `IdleHold`
  sets it while he is MARIO_STATUS_WAIT, so they hold until he moves. false = the game's peek.
- **The life meter turns to air in place** (2026-09-28 22:50). The decomp's state 2 → 3 → 0 → 4 dance for a meter already on
  screen when the air switch comes (a hide, a 120-frame wait, an appear in the air colours) is not reproduced; the colours and
  the count switch where it stands, and a meter coming in for the air uses the hit's mid-screen appear.
- `SmsRollJumpMinSpeed = 20` (game 40) — feel.
- `SmsRollJumpKnockdown = false`: the roll-jump into a wall that reaches head height wall-slides. `rotateBroadJumping()` →
  `jumpingBasic(…, 1)` case 2 knocks it down above 40, and it never drops under 40 with the stick held; Jaysonn's
  memory of the game is a slide, and it is not verifiable here without Dolphin. Walls below head height were already
  no hit for it (ledge bit). Flip the toggle to get the code's rule back.
- `doRunning` target uses `min(32, mag)`; the NonMatching decomp prints `max()`, which contradicts SM64 (this code's
  ancestor) and makes the shipped `Soft2Walk` / `Walk2Soft` thresholds meaningless.
- `doRunning` settles **at** the target: the accelerate step is capped at `sp` and the `DecBrake` step never goes below
  it. Read literally, the decomp's three branches with the shipped `DecBrake` 5 saw-tooth 27..32 every ~12 ticks at full
  stick (SM64's identical code brakes 1, a wobble nobody sees). `MarioRun.cpp` is NonMatching, and the game's run is
  steady at top speed (Jaysonn, 2026-09-25); the port had the pulse (b856f6a's `[SMS step] ... tick in -> out` fields,
  10.9 / 12.9 m/s alternating) and it read as a slight stutter everywhere he sprinted.
- `KeepGroundPoundJump` toggle (off) keeps the non-Sunshine GP-jump available.
- The slip system, collision model (`MoveAndSlide`, cylinder) and animation timings are still ours.

### Ground contact (`SmsGroundContact`, `checkGroundAtWalking`)

`walkProcess` steps horizontally, then `checkGroundAtWalking` looks the floor up from **feet + 30 units**: if it is
more than 100 units below, Mario falls; otherwise his y is *set* to it. So ledges up to 100 units (1.34 m) are
stepped down instantly with no fall state, and a floor up to 30 units (0.40 m) *above* the feet is stepped onto,
because wall planes are only tested at feet + 30 and feet + 60 and a lower riser is not a wall to the game.

- Ledge-down: Godot's `floor_snap_length` is the same mechanism, so SMS mode sets it to 1.343 m every frame
  (`SmsLedgeSnap`); the launch-frame override to 0 stays. Expect no `ledgeFall` on curbs, stairs and dock edges.
- Step-up: the cylinder is stopped by any riser. `SmsTryStepUp` runs after any `MoveAndSlide` that reported a wall
  and asks the game's two questions with two rays anchored on the contact point Godot reported. (1) The feet + 30
  wall check: a ray from the body centre at feet + 0.45 m toward the contact, 6 cm past the face; a hit means the
  wall continues above the step limit and it stays a wall. (2) The feet + 30 ground lookup: a ray straight down from
  feet + 0.45 m at a point 3 cm past the riser face. A rest 0.01–0.40 m above the feet on a floor-angled face
  (normal within `floor_max_angle`) of a `StaticBody3D` / `AnimatableBody3D` / `CsgShape3D` is a step: he is moved by
  the blocked remainder of the motion plus the rise, `ApplyFloorSnap()`ed, and given back the horizontal speed the
  riser clipped — the advance is capped at the next riser (a ray along the tread from the landing point), so on treads
  shorter than the body radius the front edge stops at the riser instead of entering it and being pushed back out the
  next frame (the jitter). A rest *level* with the feet (or below) means the "wall" was a trimesh edge, a lip under a
  centimetre or a knee-high rail: it is dismissed, speed kept, no move. Both outcomes set `_smsSteppedUp`, which
  makes `TryEnterDiveBonkAfterMove`, `PostMoveWallCheck`, `CheckGroundWallEntry` and `SmsGroundWallBonk` skip the
  frame. Rays, not a full-body `TestMove`: a 1.87 m cylinder raised above the step limit overlaps the next risers of
  any staircase steeper than ~40° and was rejecting real stairs. Every rejection prints an `[SMS step] ... stays a
  wall / rejected ...` line with the measured rise and collider.
  Allowed from the locomotion states and the belly slide (`SmsStepUpAllowed`): not while bonked, slipping, crouching
  or spinning. Debug line: `[SMS step] up 0.30 m (22 units) onto StaticBody3D 'Stairs' ...`.
- Landing frame: a dive or jump that lands on a tread and touches the next riser in the same `MoveAndSlide` is on
  the floor *after* the move but was airborne *before* it, so it was neither the ground case nor the air case and
  fell through to the dive rule (knockdown at any speed: `logForLarry.txt`, `dive at fwd 48.3`, floorAngle 0.0 on
  the plaza stairs). It is now the third case of the same probe.
- Air step-up: `checkGroundAtJumping` uses the same feet + 30 lookup and only tests walls at feet + 30 / + 150, so a
  jump or dive that meets a riser lower than 30 units lands ON the step. `SmsTryStepUp` therefore also runs for
  launched air states (`SmsStateSteers`, not from `ledgeFall`), **including the launch frames**: `_smsNoLandFrames`
  guards floor contact with the ground just left, not wall contact, and a dive or roll from the foot of a staircase
  meets the first riser within one frame (`logForLarry.txt`: re-dive from the slide, `dive at fwd 48.1` a frame
  later). A step taken from the air zeroes `_smsNoLandFrames` so the landing fires the same frame; a success is a landing
  (`_smsSteppedUp` skips `TryEnterDiveBonkAfterMove` and `PostMoveWallCheck` that frame, the landing rule does the
  rest: dive → belly slide, jump → `JUMP_END`). Risers taller than 0.40 m relative to the feet are walls, as in the game.

### ⚠️ Gotcha: a wide flat-bottomed body climbs narrow stairs as a ramp

`logForLarry.txt`: `ground bellySlidingFromDive: up 0.35 m (26 units)` eleven frames running, `posY` −13.41 → −9.56.
Those plaza stairs have 26-unit risers and treads shorter than the 0.37 m body radius, so the cylinder's front edge
is against the next riser every frame and it rises one riser per frame at slide speed: 3.85 m in 0.18 s. That is
the geometry, not a bug — a body this wide can only stand at the height of the highest tread under its footprint,
so a staircase steeper than its own diameter allows is a ~58° ramp to it. The game's point-Mario climbs the same
geometry the same way (one riser per tread crossed). If a particular staircase feels like a launch, the fix is the
collision mesh (a ramp under the visual steps, as Nintendo did for many SMS stairs), not the physics.

What *is* smoothed is the picture, both ways. Going **up**, `SmsTryStepUp` reports each riser (and the tread it
measured); going **down**, the floor snap (`SmsLedgeSnap`, 100 units — the game's `checkGroundAtWalking` sets
Mario onto the floor just the same) drops the body a riser per tread edge in one move, and `SmsStepVisualCheckSnap`
spots it by comparing the body's vertical move with staying on the floor plane it set off on (more than 0.12 m of
difference is a riser, not slope walking: 30° at 12 m/s is 0.115 m a frame). Either way the rig (Skeleton3D and
WaistTarget together, `UpdateStepVisual` in `_Process`) stays where it was and rides a virtual **ramp** through the
staircase: the offset is paid down at rise/tread per metre of horizontal travel — one riser per tread, whatever the
speed — clamped to one riser, and never slower than the standstill ease `SmsStepSmoothRate` (12/s). The first cut
(c71aa43) eased by time alone (35/s): at one riser a frame that sank the rig 0.45 m into the stairs and still
saw-toothed, and it knew nothing about going down. The camera's height pan already eases. Nothing else writes those
two node positions (the run bob, the surface tilt and `LockArmatureVisualRoot` all write `armature.Position`); the
offset is snapped to zero in the pinned ledge states.

Open (2026-09-26): the `[SMS step]` lines from the plaza stairs show the step-frame speed alternating 12.2 / 11.0 m/s
frame to frame (and the frame's travel 0.203 / 0.183 m with it). Unexplained; the line now also prints the tread
found, the frame's horizontal travel and the ground tick's in → out speed to place it.

### ⚠️ Gotcha: a long floor snap is also a long downhill redirect

`floor_snap_length` feeds `_on_floor_if_snapped`, the downhill constant-speed redirect. At 1.34 m a run off a
1 m ledge is *redirected along the previous floor plane* (a no-op on flat ground) and then snapped down, so
horizontal speed is preserved through the drop exactly as in the game. Anything that must actually leave the
ground has to carry upward velocity (launches do) or exceed 1.34 m.

### Not ported (next)

- Not mechanics after all: `LAND_SAFE_DOWN` *as a wall-hit outcome* is only reached from invalid ground data (out-of-bounds
  recovery) — as a state it is the passive drop after letting go of a ledge, see Ledge hang; the
  skid-into-wall knockdown lives in `braking()`, entered only by releasing the stick during a U-turn skid, and needs
  the wall inside the 4-tick window above 16 — skipped; the "ground wall kick" in `running()` is dead code (A is
  consumed as a jump first).
- Surface types and the floor slip: done, see Surface types. Tagging the plaza's surfaces is editor work.
- Ledge hang: done, see Ledge hang. Fences and the roof hang: done, see Fences and the roof hang (pass 52). Needs the level's
  `sms_fence` tags.
- FLUDD: S1 (the pack) and S2 (the tank, the spray, the drops, the refill, the burst machine, the hover nozzle's spray) done,
  see FLUDD. Next S2b squat aim, S3 hover lift, S4 rocket, S5 turbo.
- Swimming: done, see Swimming and Air. Needs the level's water Area3D (`sms_water`). Not ported: the FLUDD surf, the
  fence catch from water, the effects and sounds, the swimming camera, coins refilling air.
- Damage: done, see Damage. Not ported: the type-3 upper-body damage run (`ma_dgrun`), rumble, the voices, goop on hit.
- Fire and electricity: done, see Fire and electricity (passes 50-51, the effects included). Needs the level's `sms_pollution`
  tags. Not ported: the model tremble after a shock, the blue shimmer during it, the voices, the rumbles.

### Pass 29 -- the Mario particles at the game's 60 Hz particle clock; the legacy effects out; ma_hipsr through the tree

Jaysonn (2026-09-30): "the nozzle pick up sparkles are too slow. need that to speed up and it seems the sparkles are a bit too much.
can we downsize it? try alittle harder to look at what SMS does ... when mario hip drops with the normal spray nozzle his arms are not
in the right spot? hes like holding them up ... the landing effects dont look quite as good. seems like the light blue streaks are not as
line like. and the cloud puff is good just needs to be abit more condense ... spin looks good but ust lacks the colors" -- and the rule
from now on: "we can either modify what is exsiting or remove it for something new. we dont need to keep legacy stuff."

THE CLOCK (the "too slow"): TMarioParticleManager::perform (EmitterViewObj.cpp 106) runs `for (i = SMSGetAnmFrameRate(); i > 0; --i)
calc()` -- SMSGetAnmFrameRate() = 60 / SMSGetVSyncTimesPerSec() = 60 / 30 = 2 on NTSC (Application.cpp 97-118: the VSync count is
halved to the 30 fps render rate; TMarDirector::direct's 4 x 5 ms movement ticks a frame come from the same 30). The JPA manager is
calc'd twice a rendered frame: a .jpa frame is 1/60 s, not the 1/30 s pass 28 baked. Every particle duration was twice too long and every
speed half: the sparkle's 120-frame emit is 2 s (was 4), its stars live 0.58 s (was 1.17) and rise under 9.7 m/s^2 (was 2.4); the spin
rings live 0.1 s at 180 a second; the streaks fly 0.33 s at 20 m/s; the smoke lives 0.66..1.25 s; the fall streak's 10-frame ribbon is
0.17 s (SmsHipDropBlur LifeFrames / 60, stripe_blur.gdshader TIME * 60). The scenes are generated by tools gen_fx.py from the .jpa
fields (jpa_dump.py, jpa_sim_smoke.py); jpa_dump's JEFFjpa1 readings corrected against noclip's JPA.ts: the FLD1 status flags are the
u16 at +0x10 (the fade flags), BSP1 +0x22 is the COLOUR calc flags (bit 0: the random phase indexes the colour table).

The get sparkle (ms_itemget1_a): the prm colour is a 17-entry pink -> yellow table with calc type Random and anmRndm 17 -- one colour PER
STAR, not a drift over its life (color_initial_ramp); the ESP1 AddSin twinkle (params 0.1748 / 0.75 / 0.85) baked into the alpha ramp
(the stars flicker, mean visibility ~0.55); the vortex field (7.5 units a frame tangential, FieldVelocity) as a Y orbit of 1.2..2.75
turns/s decaying with the radius (Godot has no constant-tangential-speed field; pass 28's orbit had no curve and did nothing in 3D);
the stardust children at birth and frame 21 (two a star: sub-emitter 5 Hz), falling 13.6 m/s^2. The star quad stays the disc's 125 x 140
units (1.68 x 1.88 m, BSP1 2.5 x 2.8 x 50): the game's stars are that big; the size knobs are scale_min / scale_max on the Stars material.

The spin blur (ms_m_blur2sp): its prm is a 2-entry table, (255,87,72,183) and (63,87,255,255), calc Random with anmRndm 2 -- each small
ring is RED or BLUE, half and half (the colours he missed); pass 28 read it as red for one frame then blue. The ring texture turns
-9814/65536 turns a frame = -9 turns/s (the same -3234 deg/s either way).

The landing (ms_hipdrop_a / b / c): the streaks' quad had its LONG side across the velocity (QuadMesh size (2.687, 0.448) under
align_y, whose Y is the velocity) -- a bar, not a line; now (0.448, 2.687), the game's 33 x 200 units along the flight, from 96
directed points at the game's 28.6 deg elevation (radial 22 : up 12 units a frame), 15.7..24.7 m/s, both scale axes 0.1 -> 1 -> 0.1
(diffXY off: Y follows X), +-65 % size. The smoke's outward motion was a constant radial_velocity for its whole life (16+ m over 2.5 s);
now the game's: velOmni 20 units a frame under the Drag field 0.88/frame fading in over 23 % of life, then the +8 units/frame^2 Gravity
field from 25 % (faded to 49 %) under the same drag -- simulated frame by frame (jpa_sim_smoke.py) into a radial_velocity_curve and a
directional_velocity_curve: the puffs reach 3.2..4.1 m in 0.3 s and stop, then lift 0.7 m while fading. The shockwave lives 0.3 s.

The legacy effects: Mario.cs no longer drives SpinJumpEffects / GroundPoundEffects / GroundPoundImpactFx / the GP landing dust; the
three Fx scenes are required (a missing one is an error and that effect does not play). His Player.tscn still carries the three old
nodes (inert: their scripts start hidden); he deletes them, then assets/SpinJumpEffects.*, GroundPoundEffects.cs, GroundPoundImpactFx.cs
go. The other adopt-or-build fallbacks (the code-built pack, BodyLag, the HUD) are next.

The hip drop's arms: a headless drive (two pounds, the spray nozzle then the hover) read the arm joints' final rotations every frame
through BoneAttachment3Ds -- identical for both nozzles, the disc's ma_hipsr / ma_hipat / ma_hiped poses with no pump blend (upper
Idle, carry 0). One thing the port did differently from the game: ma_hipsr played on the AnimationPlayer with the tree OFF for the
60-tick hover, so a held R (the spray nozzle pumps through hipsr and hipat: gMarioAnimeData 0x3C / 0x3D upper 0x44) showed no pump arms
during the hover and snapped them in at the fall. ma_hipsr is a BaseSM state now (added at start-up like ma_turbo_dash) and the start
travels to it with the tree on; the clip holds its last frame for the rest of the hover, as the game's setAnimation(ANIM_HIPSR) does.

### Pass 30 -- the landing streaks along their flight, the spin rings bigger and coloured, his life meter in the console, the hands by the game's table, the balloon over the tank

Jaysonn (2026-09-30): "the ground pound streak effects are going to the side seems like they need to be rotated. other wise the spin jump
needs to be slightly bigger (like they are too close to mario) and still are only showing white: should be red, blue and white. also i
notcied the health was changed my legacy one looks way better then the one you made so either do another pass on it or combine mine with
the new hud. arms are fixed. but should be using the fist closed model. otherwise i updated hud ref for the text box and it is not right
still. the text is perfect but the size is a bit big for my liking and not above fludds tank ... also the sparkle is like perfect".

THE STREAKS (ms_hipdrop_a): Godot's directed-point emission (particle_process_material.cpp, RESTART_VELOCITY) multiplies the velocity
built from `direction` + spread by mat3(tangent, bitangent, normal): the point's normal is that basis' +Z. Pass 29 set `direction` (0, 1, 0)
-- the bitangent -- so every streak flew perpendicular to its normal, sideways round the ring instead of out and up along it; gen_fx.py
now emits (0, 0, 1). Nothing else about them changed (96 directed points at 28.6 deg, 15.7..24.7 m/s, align_y, the crossed 33 x 200 quads).

THE SPIN RINGS (ms_m_blur2 / blur2sp): the port's sizes were the disc's -- 100 and 55 units -- and P_msm_hamonring3's ring fills 90 % of its
quad (radii 45 and 25 units). The small RED / BLUE rings at 25 units (0.34 m) sat inside his torso, hidden by the depth test: that is why he
saw white only. DEVIATION: `SizeScale` on the SpinJumpBlur node (SmsSpinJumpBlur scales the two ring quads at ready; the line the rings
are born on stays the game's +-25 / +-50 units), 1.5 in the scene -- the white rings 150 units (2 m, about his height), the coloured 82
units (1.1 m), clear of the body; 1 = the disc.

HIS LIFE METER: the console's LifeMeter group now carries his Font/LifeMeter/LifeMeter.tscn (the Meter node in SmsHud.tscn, at (-20, -16)
so its right edge lands where his legacy HUD kept it) in place of the disc's panes: SmsStandardHud moves his PaintBlobBG as l_ba, LifeText
as l_tx and his Gray / Orange layers as lm_0 (the appear at a hit from (-150, +180), the curve to the corner, the insert pops at 0 / 14 /
24, the leave, the demo hide -- the game's states and timing), calls his SetHealth as the count steps one a frame and his SetAirTint (now
public) when the meter switches to air. DEVIATION: the game's 3 / 5 px inset heartbeat (updateLifeMeterBlink) is replaced by his meter's
own scale pulse (LifeMeter.ScaleMultiplier / ToggleInterval); the dim below 4 (updateLifeMeterColors) is not applied to his art. Gone:
assets/Sms/hud/life_icon_*.png, life_text.png, Console/J2DBlackWhite.gdshader (nothing else drew them). His Player.tscn's own LifeMeter
node is the legacy one (hidden under SMS physics): he deletes it.

THE HANDS: TMario::setAnimation calls changeHand(gMarioAnimeData[anm].unk5) on every clip change (MarioDraw.cpp 1068); changeHand (907)
sets / clears the shape flag J3D's J3DJoint::entryIn reads as SKIP (a shape whose bit is clear is drawn -- the decomp's J3DShpFlag_Visible
is named backwards), so: hand 0 = the body's shapes 5 / 6 (M_hand1_L / R, the open hands), hand 1 = ma_hnd2 (the grip), hand 2 = ma_hnd3
(the fist), ma_hnd4r never drawn. The table (198 rows, 191 clips): 0 for wait / walk / sstep / land / laend / brake / 2jmp1 / 2jmp2 /
wjmp / wsld and the demos; 1 for hgup / hang / hgdwn / hgjmp, the ladders, fences and ropes, hold_* / throw / raise / put, sqsta / sqwat /
sqend, turn / trned, slip / slped / slpla, jmped, run1; 2 for hipsr / hipat / hiped, jump, tjmp1 / tjmp2, jump_rolling, roll, run2,
step1..3, jkick, dgrun, turbo_dash. doRunningAnimation (MarioRun.cpp 131) sets ANIM_RUN2 for every run above the soft step (RUN1 is the
motion-blend partner, unk414.z), so a run's hands are fists. Mario.cs: SmsHandTable (the column, as `ma_<clip>`), SmsCurrentClip (BaseSM's
state, "ma_tjmp1 2" folded onto ma_tjmp1; the AnimationPlayer's clip with the tree off), SmsHandPose (a carried object: the grip, as the
hold_* clips; ma_runblend: RUN2's fist); UpdateHandPose uses it under UseSmsPhysics. The port's legacy list (fists for the sprint and
every jump) had the double jump and the wall jump clenched (the game: open) and the hip drop open (the game: fists); the grip hand
(RightHandSlightyOpen / LeftHandSlightyOpen = ma_hnd2) was never shown.

THE BALLOON: standard_1.blo's he_w window (397, 128) 194 x 216 ends at y 344, eleven frame pixels above the water gauge w_0 (493, 355),
its 68 x 40 tail piece (x 523..591) over the tank (x 515..559): the game draws the FLUDD's balloon above the FLUDD's tank. Pass 28 laid it
out from the window's edges at the frame's full scale (SizeScale on vp.Y / 448) while the gauge sits at the console's 0.7 with RightShift
30 / BottomShift -10: 1.4 times the gauge's size, mid-screen. SmsHintBalloon now takes the console's scale (x its own SizeScale, 1) and
the game's offset from the gauge's frame home (SmsWaterHud.Home) through the gauge's anchor -- wherever the gauge is, the balloon sits over
its tank; the `Anchor` export is gone. The text scales with the window (Delfino 20 px on the frame, as the game's 20 x 26 font).

### Pass 31 -- the legacy HUD hooks out (the doubled life meter); a rising jump keeps its arc past a ledge

Jaysonn (2026-09-30 01:32): "the health is doubleing up so i think my legacy one is stil being used can you remove it? otherwise everything
looks good! new issue i found was that mario when running into a wall then jumping and if you can jump higher then the ledge mario will
just snap to the ledge on the ground. instead of doing the full jump."

THE DOUBLED METER: pass 30's console hid his Player.tscn LifeMeter node at start, but the legacy damage code still called its
SpawnAtScreenPosition (which sets Visible = true) at the first hit -- two meters. Mario.cs no longer references any legacy HUD: the
LifeMeter / LifeCounterHud / YellowCoinHud / RedCoinHud / BlueCoinHud / ShineHud fields, their lookups, ShowHud / TickUp / NudgeDown /
ShowLives / HideLives / ReturnToNormal / SpawnAtScreenPosition / HideForFullHealth / ShowAir / SetAir / EndAir calls, the legacy idle
peek (_idleTimer / _lifeCounterShowing / _blueCoinDisplayTimer / ShowCollectibleHud) are gone; the console reads _health, _coins, _lives,
_shines, _blueCoins, _redCoins and the air state every frame and has the game's own idle peek and blue-coin hold. While the six legacy
nodes are still in Player.tscn, SmsStandardHudSetup hides them once and warns with their names -- he deletes them, then the scripts
under Font/HudElements and Font/LifeMeter/LifeMeter.tscn's node (the LifeMeter.tscn itself STAYS: it is the console's meter).

THE LEDGE: jumpProcess (MarioPhysics.cpp 486) calls checkGroundAtJumping(next = pos + 0.25 vel) every tick with NO mVel.y test: the wall
planes are tested at feet + 150 and + 30 (checkWallPlane pushes pos out of them), the ground from pos.y + 30 down, and `if (pos.y <=
floor) mPosition.y = floor; groundCode = 1` -- a landing. So when a rising jump's feet come within 30 units (0.40 m) below a ledge top,
the riser is no longer a wall to the game, the next position enters the ledge's footprint and Mario is SET onto it: SMS lands a rising
jump on a ledge the moment his feet reach it. The port's SmsTryStepUp (air branch) did exactly this ("[SMS step] air singleJump: up 0.3x
m" while rising) -- 1:1, and what he saw. DEVIATION: SmsParams.Jump.LedgeLandWhileRising = false -- in the launched jump states
(single / double / triple, the rotate jumps, the back flip, the wall kick: SmsIsJumpState) a rising jump (preMoveVel.Y > 0) skips the
step-up, so the riser stays a wall until he clears it and he lands on the way down as his arc says; the dives, rollouts and the stomp
keep the game's rule (a dive from the foot of a staircase still lands on the first riser). true = the game.

### Pass 32 -- the legacy HUD and effect nodes and their files out of the project

Jaysonn (2026-09-30 01:58): "cant you remove the legacy files?" -- pass 31 left his six legacy HUD nodes and the three legacy effect
nodes in Player.tscn for him to delete (hidden by name, with a warning). Done here instead, with the editor closed.

**Player.tscn.** The nine nodes and their ext_resources are gone: `YellowCoinHud`, `RedCoinHud`, `LifeMeter`, `BlueCoinHud`,
`LifeCounterHud`, `ShineHud` (the console -- SmsHud.tscn -- draws every one of them from Mario's fields), `GroundPoundImpactFx`,
`GroundPoundEffects`, `SpinJumpEffects` (the Fx scenes HipDropLanding / HipDropBlur / SpinJumpBlur replaced them in pass 29). The
`Font/LifeMeter/LifeMeter.tscn` FILE stays: it is the console's life meter (pass 30). `assets/Sms/BodyLag.tscn` is now instanced as
`BodyLag` under `Armature/Skeleton3D` (the body's time lag from the scene's exports, as the run log had been asking since pass 27). His
pending Player.tscn edits (the FLUDD attach, SmsHud, the three Fx instances, two AnimationTree states) are committed with it.

**The six inherited character scenes** (Koopa, Luigi, Piantissimo, Waluigi, Wario, Yoshi) overrode the ring colours of the removed
`SpinJumpEffects` node; the override blocks are gone (a node override on a node the base no longer has). The spin blur is the game's
disc now (SpinJumpBlur.tscn) with the .jpa's colours; a per-character tint would be an export on that scene if wanted.

**Code.** `SmsStandardHudSetup` no longer looks for or hides the legacy nodes. `TimerHud` no longer nudges the legacy `RedCoinHud`
(field, `RedCoinHudPath` export, lookup and the two `SetTimerActive` calls removed -- the console lays the red coins out). The two
level scenes (Rico4Secret.tscn, secrect_level.tscn) still carry a saved `RedCoinHudPath = NodePath(...)` value for that export; Godot
ignores a saved property the script no longer has, and the line drops out the next time those levels are saved.

**Deleted.** `Font/HudElements/{LifeCounterHud,YellowCoinHud,RedCoinHud,BlueCoinHud,ShineHud}.{cs,tscn}` (+ .uid), their
`HudCountBurst.cs`, `assets/SpinJumpEffects.{cs,tscn,gdshader}` (+ .uid), `assets/GroundPoundEffects.cs`, `assets/GroundPoundImpactFx.cs`
(+ .uid). The HUD atlas art (`hud.png`, the `coin*.tres` / `marioHeadIcon.tres` / `marioTextHud.tres` / `shineHudElement.tres` atlas
textures) stays: TimerHud.tscn still reads `coinBackground.tres` / `coin0.tres`, ShineGetPopup the letters.

### Pass 33 -- the super hip drop; the game's bonk stars

Jaysonn (2026-09-30 02:00): "you can also implement the super hip drop / can you also fix the bonking stars? we can make them a bit more
like sunshines bonk effects."

**The super hip drop** (MarioJump.cpp 1128-1198, hipAttacking() states 2 / 3). The fall counts `mStatusTimer`; past `mSuperHipAttackCt` it
is state 3, the SUPER hip drop. The shipped `jump.prm` says: `mSuperHipAttackCt` 80 ticks (0.67 s, 21 m of fall at -80; the code default
was 50), `mHipAttackSpeedY` -80 and `mSuperHipAttackSpeedY` -80 (the same speed -- the code defaults were -50 / -80; the port already ran the
shipped -80). What changes in state 3: the fall effect is `emitBlurHipDropSuper()` (MarioParticle.cpp 552: ms_m_sphipd_a /
b / c / d bound to the 'center' joint -- red flames riding with him, sparkles and dark orange glows left in the air, a fire streak) instead
of the blur (ms_m_blur3); the landing rumbles harder and shakes the camera in mode 0x27 (neither exists in the port); what he lands on
receives `HIT_MESSAGE_SUPER_HIP_DROP` as well as `HIT_MESSAGE_HIP_DROP` (the game's TSuperHipDropBlock breaks only to the former); and the
landing burst is `MS_M_SPHIPD_HIT_A..D` (a shockwave going pink, 30 smoke puffs, 16 flame streaks, 8 spinning stars) instead of
`MS_HIPDROP_A..C`. `SmsParams.Jump.SuperHipAttackCt = 80`; `Mario.IsSuperHipDrop` is true from the moment the fall passes it until the
landing (HIP_ATTACK_END) ends -- Nail / RedCoinSwitch style objects that read `stateOfMario` can read it. Scenes: `SuperHipDropBlur.tscn`
(SmsSuperHipDropBlur: an SmsHipDropBlur stripe with `stripe_blur_add.gdshader`, WidthUnits 150 / LifeFrames 20, plus three GPUParticles3D)
and `SuperHipDropLanding.tscn` (SmsSuperHipDropLanding), both from the .jpa fields (gen_fx_super.py; textures jpa_bake.py). Both instanced
under Mario; a missing one is an error.

**The bonk stars.** In the game a jump or a dive that hits a wall above `mClashSpeed` (mario.prm: 40) emits `PARTICLE_MS_DMG_C` at
mPosition and drops into JUMP_SHORT_BACK_DOWN (MarioJump.cpp 162-165, 462-465; the port already has that knock-back, SmsEnterBonk); the same
dmg_c is one of the three effects of a hit (at calcDamagePos = mPosition + 50 units toward the attacker, MarioCollision.cpp 263). His
code-built `BonkStars` GpuParticles3D (10 stars, 0.6 s, from the head toward the wall) is gone; `BonkStars.tscn` (SmsBonkStars, from
ms_dmg_c.jpa) plays at the game's positions: 6 spinning P_msm_hoshi stars, 65 units, yellow / pink / peach, out at 10 and up at 18 units a
tick under a 0.96 air resistance, flipping from a tall sliver to a flat one over 75 frames (+-46 %), in by 12.5 %, out from 80 %.

**Approximations, named in the scenes' resource names:** the game's random-phase colour tables are an initial ramp (one colour per
particle) over a texture baked with the env colours' mean; the Random jitter fields are dropped; the directional spread cones of hit_c /
hit_d / dmg_c are the ring spread plus the moment spread; the MultSin twinkle of sphipd_b uses the AddSin form; the fire stripe's indirect
stage is the blur shader's mask.

### Pass 34 -- the bonk stars along the impact; more stars; the super landing's flame cone

Jaysonn (2026-09-30 02:59): "the stars look good! they just always go up instead of like an impact when bonking and maybe add a few more
stars? we can make that editable with how many stars get launched. also the impact with super hip drop look good just same issue not as
many stars and the red impact lines go to upwards. should shoot out a bit more."

**Bonk stars.** The game does throw them upward: `emitParticle(PARTICLE_MS_DMG_C)` emits at mPosition with the emitter unrotated, and
ms_dmg_c's velocities are 18 along the axis (up) against 10 out from it. DEVIATION `SmsBonkStars.AimAlongImpact = true` (a scene export):
for a bonk the emitter's axis is turned from up to the knock-back direction (the wall's normal), so the same numbers spray away from the
wall like a hit; a damage hit keeps the game's up. DEVIATION `SmsBonkStars.Count = 10` (export; the game emits 6). `SpawnBonkStars(at,
impact)` passes the knock-back for the two bonk entries and up for the two hit entries.

**Super landing.** DEVIATION `SmsSuperHipDropLanding.StarCount = 16` (export; the game's hit_d emits 8). The flame streaks: a TRANSCRIPTION
FIX, not a deviation -- JPA's spread randomises only the velDir component (22 up) within a cone of acos(1 - 0.32) = 47 degrees while the
velAxis component (26 out) stays level, so the game's streaks leave between about 20 and 56 degrees of elevation around 40; pass 33 put a
47-degree Godot cone on the whole velocity and sent some of them nearly vertical ("the red impact lines go upwards"). The scene's cone is
now 18 degrees around the 40-degree direction. The speeds are exports (`FlameOutSpeed` 26, `FlameUpSpeed` 22 -- the game's; lower the up
speed for flatter streaks): the script rebuilds the emitter's directed normals and speed from them at ready.

### Pass 35 -- the bonk stars' real launch; the stars again on the knockdown landing; the flames' clock

Jaysonn (2026-09-30 20:18): "just the bonk stars i was wrong they do go up but they go too far up and should be dispersed a bit better.
also when mario lands on the ground from bonking the stars should also shoot out".

**The launch.** He is right on both counts and both are the game's data. ms_dmg_c (BEM1): volume Sphere size 10 sweep 0.5 at (0, 5, 0),
velAxis 10 + velDir 18, velRatio 0.3, spread 0.4, airResist 0.96. In JPA (noclip JPA.ts init_p / calcVolumeSphere, the reference for
pass 28 on) a star spawns on the UPPER HALF of the shell, gets velAxis straight out from the up axis along its spawn point's horizontal
direction, plus velDir along the axis TILTED by `computeModelMatrixR(0, spread x randY x pi, randZ x pi)` -- a tilt uniform in
[-spread, spread] x 180 degrees = up to 72 degrees at a random azimuth -- the sum x (1 +- 0.3). Sampled: elevation 12..90 degrees
(median 48, mean 47), speeds 7..36 units a frame (p5 11.6, p95 29.4), a median full-life rise of 4.3 m. The port (pass 33) had every
star rising straight up at 18 x the decay (5.8 m) with only the 10 out: "too far up, not dispersed".

CORRECTION to the pass-34 text: JPA's spread is NOT acos(1 - spread); it is spread x 180 degrees, uniform in angle. The super landing's
flame cone (ms_m_sphipd_hit_c spread 0.32) is therefore up to 57.6 degrees of tilt on the 22-up component, i.e. elevations of about
15..58 degrees around 37; the scene's 18-degree Godot cone about 40 (pass 34) stays as a close stand-in Jaysonn approved.

Godot's ParticleProcessMaterial cannot give each particle its own direction AND the shared decay curve, so the launch is SAMPLED:
`SmsBonkStars.ApplyLaunch` (and `gen_fx_super.points_launch` for the baked scene) draws 256 stars by the game's rule, puts each one's
direction on the shell around the pivot as an emission POINT, and the material's radial velocity from the pivot (the 0.96 decay curve)
flies it along that line; the radial min / max are the sampled speeds' 5th / 95th percentile (9.17 / 23.35 m/s). Lost: the pairing of a
star's direction with its speed (uniform instead), the +-1.8 jitter field (as before), and the decay stays relative to each star's life
(as every effect since pass 28). Exports on the BonkStars node with the game's values: `UpSpeed` 18, `OutSpeed` 10, `SpreadDeg` 72,
`SpeedRandom` 0.3 (and `Count` 10 -- DEVIATION, pass 34). `AimAlongImpact` is now OFF (the game's up; he confirmed) and stays a knob.

**The landing.** Every ground knockdown status -- backDown, foreDown, shortBackDown, shortForeDown, safeBackDown, safeForeDown,
catchDown (MarioRun.cpp 1399-1470) -- emits PARTICLE_MS_DMG_C at mPosition on its first tick and rumbles (0x15, mMotorWall). The bonk's
JUMP_SHORT_BACK_DOWN lands into SHORT_BACK_DOWN (jumpDownCommon, MarioJump.cpp 522), so the game bursts twice: at the wall and on the
ground. `TickBonk`'s landing and `SmsDownGroundAnim()` (the start of every ground *_DOWN: a hit taken on the ground, an air knockdown
landing) now call `SpawnBonkStars(feet, up)`. The rumble is not ported (no rumble system).

**The flames (pass-34 bug).** `SmsSuperHipDropLanding.ApplyFlameSpeeds` converted the streaks' 34 units a frame with `SmsUnits.SpeedToMps`
(the 30 fps MOVEMENT convention) and set 10.6..16.8 m/s where the scene's baked data (and the game) say 21.3..33.6: pass 34 ran the
flames at half speed. `SmsUnits.FxSpeedToMps` (x 60 / 74.44, the particle clock) replaces it, with a self-test check.

**Follow-up (20:54): "when mario gets hit from an enemy the stars spawn too far away from him. almost like the offset is wrong".** The
offset is the game's (calcDamagePos: mPosition + 50 units toward the attacker, MarioCollision.cpp 263-271) -- the drift came from the
node. The Fx scenes are instanced under Mario, and `Play` moved the BonkStars node to the spot and restarted its emitter in place; the
particles are world space, but Godot's ParticleProcessMaterial reads the node's CURRENT transform every frame for the radial pivot
(`process_radial_displacement`: normalize(pos - emission_transform x velocity_pivot)), so as he was knocked back the pivot receded with him
and every star was steered away from his position: the burst ran off toward the attacker instead of rising over the hit. Pass 33/34 only
had the 10-out radial part on the pivot, pass 35 put the whole launch on it, which is why it showed now. The game's emitters here are
UNBOUND (emitParticle(id, &pos) with no bind), so `SmsUnboundBurst.Spawn` (new, assets/Sms/Fx) duplicates the emitter as a top-level
one-shot at the spot, frees it on `finished`, and the scene's node is only the template: BonkStars, HipDropLanding and SuperHipDropLanding
all go through it (the landings' smoke and stars have radial pivots too and would have followed him off the landing spot; the material
and its ready-time edits are shared by the copies). A second burst no longer cuts the first short either (the bonk's wall + landing pair).

### Pass 36 -- a hit's stars: fewer and shorter

Jaysonn (2026-09-30 21:34): "maybe just a little less on the bonk stars for when mario gets hit. just shorter effect and not as many
stars. otherwise looks great."

The game fires the identical ms_dmg_c emitter (6 stars, 75 frames) for a wall bonk, for the damage burst at calcDamagePos AND for the
first tick of every knockdown status -- so a hit taken on the ground is two bursts at once, and with the pass-34 Count of 10 that was 20
stars where the game shows 12. DEVIATION, two exports on the BonkStars node: `HitCount` 6 (the game's own count; the bonk keeps `Count`
10) and `HitLifeScale` 0.7 (a hit's stars live 0.875 s of the bonk's 1.25; the flight, the flip and the fade compress with it; 1.0 = the
game). `SmsBonkStars.PlayHit(at)` spawns the hit variant through `SmsUnboundBurst.Spawn`'s configure step (amount and lifetime set on the
copy before it emits); Mario.cs routes the two damage sites (ground / swim damageExec) and `SmsDownGroundAnim()` (the knockdown status's
own burst: a ground hit's second, an air knockdown's landing) through `SpawnHitStars`. The bonk at the wall and its own landing
(TickBonk -> SHORT_BACK_DOWN) keep the full burst, which he approved.

### Pass 37 -- the last stand-ins become errors

The no-legacy rule (pass 29 on: modify or replace, no fallbacks): four places still quietly built a stand-in when a scene node was
missing. They now log one `PushError` naming the node to instance and leave that feature off, like the Fx scenes since pass 29:

- the FLUDD pack: `SmsFluddSetup` no longer creates the `SmsFluddAttach` BoneAttachment3D nor hangs the pack from the rips
  (`SmsFluddModel.Build`); without the attachment or the Fludd scene under it there is no FLUDD. `SmsFluddModel.Build`, `Parts`,
  `LoadScene`, `TankMaterials` (the code-built tank's MAT3 fixes, with a texture-file fallback of its own; the scene's .tres
  materials do this since pass 19), `Dir`, `NozzleFolder`, `NozzleBindPosition` and Mario.cs's unused `SmsLoadScene` /
  `SmsFindChild` wrappers are deleted; the class keeps NozzleMaterial (the nozzle items' atlas) and the shared node helpers.
- the grips' pendulum: `SmsFluddSetupHandleSwing` no longer builds an SmsSwingRZ when Fludd.tscn lacks its `HandleSwing` node.
- the body's time lag: `SmsWaistSetup` no longer builds an SmsTimeLag when Player.tscn lacks `BodyLag` under Armature/Skeleton3D.
- the console: `SmsHudLayerGet` no longer instances SmsHud.tscn from the file when Player.tscn lacks the `SmsHud` node (the error
  once; the counters, life meter, gauge and balloon stay off).

`SmsParams.MtxEffect` keeps the shipped MtxEffect numbers as reference (the scenes carry them as exports). Player.tscn has every node
(pass 32), so nothing changes for the game as it is; the errors are for a Player scene that loses one.

### Pass 38 -- the telop (Dolpic News) as a scene; the console's clock is the 120 Hz tick

**The clock (a transcription fix).** TGCConsole2 is a view object in "Group 2D", which TMarDirector::setupPerformList_console pushes
into its `unk30` list with CUE_MOVE | CUE_CALC_ANIM (MarDirectorInitECT.cpp 236); `unk30->perform(unk30Cue)` runs INSIDE the simulation
loop (MarDirectorDirect.cpp 149), once per 120 Hz tick with the MOVE cue (CALC_ANIM only on the frame's last tick). So every console
"frame" -- the TExPane slides, the pops, the balloon's typing and hold, the gauge's X blink, the idle counts, the telop's waits
(`telopWait * 120` = seconds) -- is a 120 Hz tick, not a 30 fps frame as passes 19-37 took it. `SmsHud.Fps` 30 -> 120; the pops,
settles and slides already ran at AnimSpeed 4 at Jaysonn's request, which is exactly the missing factor, so `AnimSpeed` is now 1 with the
same motion, and the parts that stayed "the game's" (the balloon's typing and hold, the X blink, the idle peeks) are now 4x faster --
the game's. The AnimSpeed knob keeps working on top.

**The telop.** The Dolpic News ticker of Delfino Plaza (GCConsole2.cpp updateTelopState 1313, startAppearTelop 2396, processDrawTelop
3323, the draw 4016; standard_1.blo 'te_0' at (294, 53): the 'te_w' window at (37, 400) 443 x 42 with contents (8, 8) 427 x 26 and the
telop_window_1..4 I4 pieces -- black at alpha 136, a flat 136 top-left so the bar is one dark band, the right and bottom pieces fading
out as a soft shadow (compose_telop_window.py -> hud/telop_window.png, a NinePatch) -- and the 'tet2' text, standard_font 20 x 26 white,
over 'tet1' black 2 px right and down). The rule: while Mario's status is MARIO_STATUS_WAIT (standing), no camera demo (unk50) and no
timer out (unk44C), a count runs (held in the pause menu); at `unk562` = 5 s the first time (unk56C) and `unk560` = 60 s after each news
(the count restarts when one ends), the next line of the stage's list is picked -- `++unk558` BEFORE the pick, wrapping at the end, so a
two-line list opens on its second -- MSD_SE_SY_NEWS plays and the group rises from 465 (off the bottom) over 80 frames; the text then
scrolls right to left at `unk564` 0.7 frame pixels a frame (84 px/s) scissored to the contents, until it is past the window's left edge
by its own width; the group drops back over 80 frames. Only Delfino Plaza has lists (checkChangeTelopArray: per episode and flags);
infomess.bmg's 20 D.E.B.S. ALERT lines (group 0x0E) are decoded into assets/Sms/Console/dolpic_news.txt with the episode picks.

The port: `assets/Sms/Console/Telop.tscn` (SmsTelop, instanced in SmsHud.tscn as SmsHudLayer.Telop): a Control laid out in frame pixels
and scaled like the balloon -- Frame (NinePatchRect of telop_window.png), Clip (the contents, clip_contents), Clip/Text (a Label on
TelopText.tres: Delfino 20 px white, shadow black (2, 2) -- his to swap), Sound (an AudioStreamPlayer slot for the jingle). Exports with
the game's values: FirstWaitSeconds 5, WaitSeconds 60, SlideFrames 80, ScrollPerFrame 0.7, WidthFrame 443 (widen it toward the gauge on
a wide canvas), SizeScale, ShiftFrame. The rise and drop follow AnimSpeed like every pane; the waits and the scroll are the game's. The
stage's lines come from a new `News` export on his Level (Mario.SmsLevelNews at the console setup); SmsStandardHud feeds MarioWaiting =
Idle && !Demo && !timer out, and the pause. Not resolved: the game's scissor is 16/15 wider than the contents and 16 px higher (a
J2DPrint origin detail); the port clips to the band and centres the text in it. The bar sits bottom-left at the game's (37, 400) through
the console's shifts.

### Pass 39 -- the camera shake as a scene

gpCameraShake (TCameraShake, Camera/camerashake.cpp): four slots; `startShake(mode, scale)` loads a mode's /Camera/shake*.prm
(TCamSaveShake: mShakeTime, Amp / Vel for X, Y, Z) into a free slot (or the decreasing one with the least left), amp x scale, mDec =
amp / time. `execShake` runs LAST in CPolarSubCamera::calcFinalPosAndAt_ (cameragc.cpp 832) on the final eye and up, at the camera's
120 Hz: each tick every active slot adds Amp x JMASSin(Vel x frame) (BAM) to the eye's vertical and horizontal polar angles about the
camera's target (CLBCrossToPolar / CLBPolarToCross) and to a roll of the up vector, then decays its amplitudes by mDec and ends at
frame >= time. Mario's: the hip drop's landing startShake(0, 1.0) = shakeHipDrop.prm (25 ticks, pitch 296.9 BAM = 1.6 deg at 7008 a
tick = 12.8 Hz); the super hip drop's startShake(0x27, 1.0) = shakeKoopaHipdrop.prm (200 ticks: pitch 200 at 1644, yaw 250 at 1715,
roll 700 = 3.8 deg at 3653); damage startShake(2, 1.0) = shakeDamage.prm (60 ticks, roll 900 = 4.9 deg at 5000 = 9.2 Hz) for every
damage type but the upper-body 3 (MarioCollision.cpp 380, before the swim branch). The wall slide's startShake(1) = shakeWallJump.prm
ships with time 0: nothing. The files sit in data/params/camera/ (lower-case) on the disc.

The port: `assets/Sms/CameraShake.tscn` (SmsCameraShake, a Node under Mario; `SmsCameraShake.Current` for objects) with the three shakes
as inline `SmsCameraShakeMode` resources (TimeTicks, AmpX..Z, VelX..Z -- the prm fields, his to tune) and a `Strength` knob. The camera
scripts are never touched: while a shake runs the node's own `ShakeCamera` copies whichever camera is current (SunshineCamera, the Y
camera, a demo or reward camera -- whatever made itself current becomes the source), orbits the eye about the view axis at Mario's
distance by the slot sum of vertical / horizontal angles, rolls the up vector about the view direction, and is the current camera;
when the last slot ends it hands current back to its source. Ticks at 120 Hz; the per-tick integer truncation of the BAM sums is kept.
Not resolved: the decomp's execShake rolls the up vector about the eye's displacement direction (zero for a pure roll such as the
damage shake, which would do nothing) -- the port rolls about the view direction. Not ported beside them: the rumbles.

### Pass 40 -- the life meter at low health and low air, checked against the console

Jaysonn: "when mario is low health the center piece and the text life gets darker i want you to make sure anything like that is checked
off and done including with the air health meter also for swimming". TGCConsole2's life meter, read in full:

- **Colours (updateLifeMeterColors 358 / updateLifeMeterState 597).** When the SHOWN count changes (one a frame toward Mario's HP or
  air), the 'l_tx' text (unk178) and the 'lm01' centre (unk17C[0]) are re-coloured: at 4..8 white 0xFFFFFF (health) / 0x00FFFF over
  black 0x003CFF (air); at 1..3 white 0x7F7F7F -- half-bright, "darker" -- (health) / 0x0010FF over 0x003CFF (air). The eight
  segments ('lm11'..'lm81') keep their colour in both modes. The 'l_ba' disc behind is 0xFF4C00 at alpha 0xC8 in health mode.
  PORT: his LifeMeter gets `SetLow(bool)` -- OrangeSwirl (the centre) and LifeText modulated by 0.5 grey under 4, restored at 4 --
  called by the console on a change in health mode; `SetAirTint`'s low variant (the mix toward 0x0010FF) now applies to the centre and
  the text only (a second material), the segments keep the plain remap -- it used to darken every piece.
- **The beat (updateLifeMeterBlink 1086).** While the meter is on screen the lit pieces shrink by 3 px (5 at 1..3) for half of a cycle of
  2 x rate frames, rate 100 at 4 or more, 80 at 3, 60 at 2, 35 at 1 (120 Hz: 1.67 / 1.33 / 1.0 / 0.58 s), and at frame 1.5 x rate the
  heartbeat SE 0x4800 plays while the count is 1..3 (not in a demo). PORT: his meter's scale pulse (ScaleMultiplier 1.1 -- the look is
  his, logged in pass 30) now runs at the game's cadence: the console writes rate / 120 into his ToggleInterval (`HeartbeatRates`
  export, on); the beep is a `LifeBeep` AudioStreamPlayer slot on the console's StandardHud (SmsHud.tscn), his to fill.
- Already there: the air mode's cyan remap and its low-air blue (pass 29), the meter appearing at a hit mid-screen and curving home, the
  air meter after 240 frames under water, the fade in the pause menu is not ported (no pause menu yet).

### Pass 41 -- CameraShake.tscn instanced

`res://assets/Sms/CameraShake.tscn` is instanced under Mario in `Player.tscn` (the pass-39 to-do), so the hip drop, super hip drop
and damage shakes play and the boot no longer errors about the missing node. The `[SMS fx]` debug line names it with the other scenes.

### Pass 42 -- the life meter around water, 1:1

Jaysonn: "when jumping in the water it goes away but sliding up then water covers it and when you go out of the water it slides up and
swaps back to the normal one. also the water health shows up near the middle of the screen (or mario you need to check how it spawns in)
then goes where health is supposed to go. basically you need to cover the cases of the health spawning in and going away and what should
be showing when mario is swimming or just on land."

- **The source.** `TGCConsole2::perform`'s life-meter block (GCConsole2.cpp 397-640, updateLifeMeterState) is NonMatching in the decomp and
  its case labels for states 4..8 are WRONG: read against the original disassembly (QbeRoot/sms asm/GC2D/GCConsole2.s, 80202788-80202F74),
  the compiler laid the case blocks out in the source order 0 1 2 3 7 8 4 5 6 10 and the decomp names them 4 5 6 7 8 in block order. The
  jump table's numbering is the only one under which the machine is consistent (the first switch reads mAir in 4 5 6 8 9): 0 hidden,
  1 health appearing, 2 health shown, 3 health leaving, 4 AIR appearing, 5 air shown, 6 air leaving, 7 health inserting (from above),
  8 air inserting, 9 air standing (helmet stages), 10 camera demo. The helpers (startAppearLife 2325, processAppearLife 3010,
  startInsertLife 2262, processInsertLife 3032, resetLife 2304, startLifeMeterDisappear 340) match the asm and were already ported.
- **What the game does (120 Hz frames).** `isUnderWater()` = in water and the head joint deeper than CanBreathDepth 50 under the surface;
  the console counts his frames under (unk268) and the air timeout holds from frame 240 on, gone the frame he is not under. Health meter
  shown (state 2) + timeout -> it holds 120 frames and slides up off the top (3) -> hidden (0) -> still under: the AIR meter (blue, the
  pieces per (int)mAir) appears at (-150, +180) from its home -- the middle-right of the screen -- and SITS there while his air is still
  8 and his head under (state 4 holds processAppearLife at frame 0; the first unit goes 500 frames after he went under), then holds 100
  frames and curves home over 40 (through (-10, 10)) -> 5, the pieces following mAir. Surfaced: the air refills 0.03 a frame; once the
  shown count is 8 the air meter holds 120 and slides up (6); then the HEALTH meter drops in from above over 20 frames (the disc first,
  "LIFE" popping at 14, the sun at 24: startInsertLife(0) -> 7 -> 2) if mHealth is not 8, else nothing. Hidden + timeout (full health):
  the air meter appears as above (0 -> 4). A camera demo slides whatever shows out at once (10, no hold) and, over, drops the health
  meter in if mHealth is not 8, the air meter if his head is under, else stays hidden. Quirks kept: a count under 8 at the timeout
  (a quick re-dive) turns the air appear into a health appear with the pieces per the air (the state-0 check after the air one), and
  the state-5 "count never stepped" restart.
- **PORT.** `SmsStandardHud.cs`: the eleven states as an enum numbered the game's way, unk84 / unk38 / unk4C / unk268 / unk1C as fields,
  the group's TBoundPane as `LifeMove` (setPanePosition + update: the pane only moves on the frames the state updates it, which is what
  holds a slide through the 120-frame wait and the appear through its 100), startAppearLife / processAppearLife / startInsertLife /
  processInsertLife / resetLife / startLifeMeterDisappear as methods, the count stepping with `LifeUp` / `LifeDown` AudioStreamPlayer
  slots (SE 0x4801 / 0x4823, his to fill), the state-10 return in the machine itself. The pieces his meter lights follow the game's
  start*Life targets (`_lifeLit`), the count catches up as the game's does. The console reads `UnderWater` (isUnderWater) and `Air`
  ((int)mAir) from Mario and runs the 240-frame timer itself; `Mario.cs` loses its `_smsAirHudTicks` / `_smsAirHudShown` /
  `_smsAirUnitsShown` switch and `SmsParams.Swim.AirHudDelayTicks` goes. `DebugPrint` (Mario's SmsDebugPrint) logs every state change
  as `[SMS hud] life meter N -> M`. The pass-30 deviation "the meter turns to air in place" is WITHDRAWN. `SmsHud.AnimSpeed`'s code
  default is 1 (the scene's value; it read 4 from before pass 38).
- Not ported: the three helmet stages (SMS_isDivingMap: the air meter inserted at the start, state 9), the pause menu's alpha fade.
- Drive (patch_smoke26.py, removed): a hit at 1 s (7 HP), under water 3-12 s, a unit of air taken at 10 s, HP to 8 at 16 s, under 18-24 s,
  under from 26.5 s with a camera demo 27-29 s; every state change and the group's position printed and compared with the numbers above.

### Pass 43 -- the console's clock never skips a tick; the telop centred

Jaysonn: "i got it to show but its off to the left. as shown in hudref.png ... also some UI hud elements are missing on first load.
they seem to load later but this should not happen."

- **The missing pieces (a port bug).** `SmsStandardHud._Process` stepped at most 8 console frames a display frame and JUMPED the rest
  (`_frame = whole`). Godot caps a display frame at max_physics_steps_per_frame (8) of the 60 Hz physics = 133 ms = 16 console frames
  (main.cpp 4951: the process step is shortened with the dropped physics steps), so a frame past 67 ms lost console frames. Every pane's
  pop is armed on a single frame (`At(f, 14)`: the icon, 24 the x, 28 / 34 / 40 the digits -- the game's processAppear* `param_1 == N`),
  and a frame jumped over left its pane hidden (Shown false) until the group's next appear. A level's first frame is the load, 133 ms
  capped: frames 9-16 went, the coin and Mario icons due at 15 with them, the x's (25) and digits (29+) came; a hitch at the star HUD's
  idle peek did the same to the shine and blue coin icons -- his HudRef.png: every strip, x and digit home, no icons, "back later" =
  the next appear (the peek, a coin step-down, a lives change). The game's console never skips a tick (CUE_MOVE, every simulation tick).
  FIX: every owed frame is stepped (`MaxCatchUp` 240 = 2 s a display frame; past that the rest of the stall comes off the clock, not off
  the ticks -- the console runs late, as the game under lag shows every tick late and none missing). The drive (patch_smoke27.py,
  removed) logged every display frame that owed 9+ console frames and forced two 220 ms stalls: the level's own first frame (150 ms,
  console 0 -> 18) was enough -- before, every icon stayed OFF with everything else on (his picture); after, all on.
- **The star HUD at boot (a port bug).** The console took the game's loadAfter snapshot (the counts the digits start at) in the scene's
  _Ready, which runs before Mario's with every field 0; Mario then fed his save (GameData.BlueCoinCount: 2) and at frame 1 the blue coins
  rolled 0 -> 2, which brings the star HUD out -- so a boot showed the shines and blue coins with the coins and the lives (his picture's
  three rows), where the game's start brings in the coins and the lives only (perform 3854). `LoadCounts()` now runs on the console's
  first frame, after his setup's feed: no roll, no star HUD until the idle peek or a take.
- **The telop's place (DEVIATION).** The port had the game's te_w at x 37 of its 640 frame, left-anchored like the other left-hand groups
  -- right on the 4:3 screen, parked at the left of the 16:9 canvas (his HudRef.png: 61 of 1929 window px, 746 wide). `SmsTelop.Centered`
  (export, on): the bar centred on the canvas, the game's bottom margin and width kept, ShiftFrame still applied; off = the game's place.
  At his Size 0.7 on the 1152 x 648 canvas: x 351.7 (was 37.5), y 589.3, 448.5 x 42.5 px.
- The boot line said the star HUD peeks "after 6.7 s idle" -- 200 frames at the 30 fps clock of passes 19-37; at 120 Hz it is 1.7 s.

### Pass 44 -- the pad library's stick clamp (the idle turn); the long fall's cry slot
- **The stick, 1:1 from the pad up (a port gap).** The game's stick reaches Mario through three stages and the port had the last one
  only: PADClamp (dolphin/pad/Padclamp.c: each axis of the raw s8 loses a 15-count dead zone -- subtracted, not just zeroed -- and the
  pair is squeezed into the 72 / 40 octagon), JUTGamePad::CStick::update (the main stick / 54, a length past 1 normalised), then
  TMario::checkController (128 x, the +-7 / 6 band, x 0.78^2, clamp 64, 32 (len/64)^2 -- `SmsStick.IntendedMag`, pass 7). Godot's
  -1..1 axis went in as mPosX directly, so a pad's rest drift reached the wait's turn-in-place (the legacy pivot band 0.06..0.2 on the
  vector length; the game's own waiting() turn is the same band at WaitingRotSp) and Mario slowly turned toward the drift while
  standing (pass 26b: `[LAND_DBG] state=pivot` with no stick). `SmsStick.PadStick` is the two missing stages, integer for integer,
  and every stick read in Mario.cs goes through it (`SmsParams.Controller.PadClampMin / Max / XY = 15 / 72 / 40, PadStickDiv 54`).
  The one mapping constant: a modern pad's full tilt is raw 100 (`PadGateRaw`, Dolphin's GCPadStatus::MAIN_STICK_RADIUS -- how every
  pad reaches the game; a GameCube stick's gate reads about the same). So: dead zone 15 % of the tilt (checkController's own band
  then sits at 18 %), the run full at 63 % (was 82 %), a full diagonal = the octagon's corner (40, 40) = a full run. Godot's own
  deadzone on the Lstick_* actions stays 0.0 -- this is the dead zone. Self-test: 6 % -> 0, 15 % -> 0, 16 % -> 1/54, 72 % saturated,
  the diagonal 0.7071, 50 % -> mag 16.7, 63 % -> 32. Not touched: the legacy camera scripts and the L camera's own stick read
  (MarioYCam: Godot's 0.2 deadzone); the C stick's clamp (59 / 31 / 15, / 42) has no SMS consumer yet.
- **The long fall's cry (a slot).** landing(): past 240 ticks (2 s) of falling the cry MSD_SE_MV10B_CRY_JUMP_01 and ANIM_THROWN to
  the ground were in since the fall pass; the cry is now `Mario.FallCry` (an AudioStreamPlayer3D under Mario, the voice in its
  Stream), played once when the fall passes 240 -- the game re-issues the SE every other tick (`mStatusTimer++ > 240` with the reset
  to 240) and its sound system keeps the one playing. Empty slot = silent, as before.
- Checked and already 1:1, nothing changed: the ledge fatigue (Hanging.prm as shipped: RapidTime 2000 ticks = 16.7 s -> ANIM_HANG at
  AnmRapid 8x and no shimmy; LimitTime 2400 = 20 s -> LAND_SAFE_DOWN; MoveSp 0.06, AnmRate 0.8 -- the shipped file, not the code
  defaults 0.1 / 0.5), and the long fall's 240 ticks in LANDING / ROCKET_LANDING only (a jump never flails: jumping() has no timer).

### Pass 45 -- the Y camera's stick through the pad library
- **The L camera's stick (a port approximation made exact).** ctrlLButtonCamera_ reads mCompSPos[4],[5] = mMainStick.mPosX / mPosY --
  the same PADClamp + JUTGamePad pair Mario reads (pass 44). MarioYCam read Godot's vector with a 0.2 circular deadzone standing in for
  PADClamp's 15 of 72; GetVector also rescales what is left, so a half stick came out 0.375 and turned the camera at 112 BAM a tick
  (YAngleManualSpeed 300) where the game's mPos 0.648 gives 194, and full speed needed a full tilt where the game has it at 72 %. Now
  `SmsStick.PadStick` -- the dead zone 15 %, the rates the game's. Drive: a 6 % drift in the mode moved the yaw 0 BAM before and after;
  a half stick moved it 3584 BAM per 32 ticks before, 6208 after (194 a tick).
- Checked and already 1:1: a Y in the air. execCameraModeChangeProc_ (CameraChange.cpp 588) refuses only asleep / SURF / ROCKET and
  ROCKET_LANDING / FLUDD_EMITTING (the turbo's) / the pole climbs, and execFrontRotate_ the hip drop; SmsYCamCanEnter takes any
  doJumping()-steered air state -- the drive's Y at the top of a jump swung the camera and had the L camera on while still airborne.

### Pass 46 -- FLUDD: the upper-body column of the animation table; the empty tank's pumping rate
- **gMarioAnimeData's upper column (unk2), by clip (a port gap).** addUpper() binds over the body clip the upper clip the table names
  for it -- 0x44 ma_pump, 0x45 ma_hgpmp (hang, fence_move_down, rope_hgwat), 0x94 ma_fepmp (the fence set), 0x95 ma_swpmp (the swim set)
  -- or 0xC8, none, which is checkPumpEnable()'s isPumpOK(): no pump over that clip. The port chose by state (hang / water / else) and
  refused by a short state list. Now `SmsUpperPumpTable` carries every named row; the pump is refused over a 0xC8 clip -- which adds the
  game's refusals the states missed: the slips (slip, slped, slpla, sldct, slpbk, sldwn), hiped, wsld, the shimmy (hmov_l / r), thrown
  (the long fall), jkick, dgrun, sit / sleep / yawn, the holds -- and the upper clip is the column's: one SmsUpperPose graft per variant
  in the library (ma_swpmp existed; ma_hgpmp and ma_fepmp join when the clips are there, else the tree's ma_pump stands), weighted in
  while the body clip runs outside the tree. The frame ctrl re-inits (frame 0, rate 1) when the bound clip changes, as J3DFrameCtrl does.
- **The empty tank's pumping (a port gap).** addUpper(): while PUMPING with mCurrentWater <= 0 the upper clip's rate is mAnalogR (the R
  trigger after PADClamp, 0..150) x UpperBody.prm PumpAnmSpeed 0.015 -- 2.25 at full R, 0.75 at a third -- and the squeaks
  MSD_SE_PO_POMPING1 / 2 fire as the frame passes 8 forward / 13 backward: slots `Mario.PumpSqueakIn / PumpSqueakOut`
  (AudioStreamPlayer3Ds under Mario). The rate belongs to the frame ctrl: it stays until the next bind (IDLE), so a tank refilled
  mid-pump keeps the last empty rate until R is let go, as the game's does; with water the rate is the bind's 1. (The pass-7 note read the
  0.015 as a rate on its own and called it too slow -- the multiplier is the raw 0..150 analog.)
- Checked and already in, nothing changed: setBaseTRMtx's roll tweak (-0x1800 x (1 - |mtx[1][0]|) about the spine, the tank pitched
  33.75 deg off his belly when he lies flat).

### Pass 47 -- water: a heal under water refills the air
- **incHP() under water (a port gap).** TMario::incHP (MarioCollision.cpp 25): while isUnderWater() (the pass-42 rule: head below the
  surface less CanBreathDepth) a heal goes to mAir -- capped at mMaxAir -- not to mHealth, so a coin taken while submerged puts a
  unit of air back (the blue meter steps up, LifeUp); on land it goes to the health as before. MSD_SE_SY_HP_RECOVER plays either way
  when the heal was not already full: the `Mario.HpRecover` slot (an AudioStreamPlayer -- a system SE, not positional). The coins call
  HealOneHP (a red coin twice), which is now incHP(1).

### Pass 48 -- water: the bubbles
- **The bubbles under water (not in the port).** swimmingBubbleEffect() (MarioMove.cpp 2097, every tick in the water): with the head
  mBubbleDepth (10 u) under the surface the mouth bubbles (PARTICLE_MS_M_AWA in the head joint's matrix: a point at (22, 5, 0) -- the
  mouth -- 0.1 a frame, 2 u/f along (1, 0.25, 0) in a 45 deg cone, life 130, the Gravity field +0.06 u/f^2 under airResist 0.98 = a
  2.4 m/s rise, a +-4 u shimmer every 2 frames and a +-1 u/f drift every 8, quad 10 u wobbling on an 8-frame loop, alpha 0.625 fading
  from 92.5 %, lerp((135,183,231) -> white) additive); with the 'center' joint 10 u under, bubbleFromBody() (PARTICLE_MS_M_AWA_S: a
  shell 25..50 u about the centre, the rate lerp(0, 0.5, mForwardVel / 40) a frame -- none at rest -- life 90 -25 %, Gravity +0.04,
  quad 6.5 u). TBubbleCallBack: a bubble that rises past the surface dies there and leaves PARTICLE_MS_M_AWAHAMON, a flat 45-u ring,
  30 frames, alpha 0.5 -> 0, its texture matrix 1.5 shrinking 0.0099 a frame (the ring grows). decHP() under water (a hit): ten
  bubbleFromMouth emitters for the one frame -- a new JPA emitter always puts out a particle on its first frame (FIRST_EMISSION), so a
  gulp of ten; the game's emitters live while called and stop (their bubbles finishing) the frame they are not
  (TMarioParticleManager::perform). All as
  `assets/Sms/Fx/SmsBubbles.tscn` + SmsBubbles.cs: the three emitters simulated particle for particle (the pop into a ripple needs the
  CPU) on the 60 Hz particle clock, one frame a physics frame, drawn as two MultiMeshes (billboard quads + flat planes, the I8
  textures P_msm_mizutama1_i / P_msm_hamonring2, bubble_add / ring_add shaders); every field an export. INSTANCE it under Mario in
  Player.tscn (like the other Fx); until then one error at boot and no bubbles. Not ported: the bubbles' own spin (+-0.25 turns a
  frame on a round texture). Reading of the Gravity field: world up, as the hip-drop smoke's (pass 29) -- the bubbles rise whatever
  the head does; the mouth offset and the exhale direction are in the head joint's own frame.

### Pass 49 -- SmsBubbles.tscn instanced

`res://assets/Sms/Fx/SmsBubbles.tscn` is instanced under Mario in `Player.tscn` (the pass-48 to-do), so the bubbles from the mouth,
the stream off the body while swimming, the gulp on a hit under water and the pop rings at the surface play, and the boot no longer
errors about the missing node. The `[SMS fx]` debug line already named it with the other scenes.

### Pass 50 -- fire and electricity: the hot floor, the electric floor, FIRE_DOWN / FIRE_JUMP_END, ELECTRIC_DAMAGE / ELEC_DOWN

Jaysonn (2026-10-01): "yes lets do fire and shock". The two hazards as a pollution tag on the floor body (`sms_pollution` = `fire` |
`electric`, independent of `sms_surface`), the statuses they put him in as kinds of the `hurt` state, fire enemies' hits forcing
`FIRE_DOWN`, `TakeShock()` for electric enemies -- see Fire and electricity for the whole machine and the shipped values (the decomp's
defaults were wrong on five of them). Two sound slots on Mario: `FireDamageSound`, `ElectricDamageSound`. The effects (the burn's burst,
the sparks, the puff, the tremble, the shimmer) are the next pass.

A correction that came out of reading the damage files again: a `dmg*.prm` that leaves `mInvincibleTime` out keeps `TEParams`' default of
**300 ticks** (2.5 s), not 0 -- enemycommon, hamukuri, poihana and fire all leave it out. A Gooble's hit now gives 2.5 s during which
nothing hurts him, as in the game; the port-side "no second hit while down" rule stays.

Verified headless (a drive tagging the floor under him at run time): the burn launches at vy 50 (apex 4.4 m) and lands 100 ticks
later; the hot floor burns him again at every landing with the face turned 180 (invincible 50 < the 100-tick flight: -1 HP every 0.84 s);
a full stick held through a flight turns the face by 0.00 deg and moves him by nothing; on clean ground the landing bounces at vy 20.8
then 10.1 (0.4 x the landing speed) with fwd halved, then `ma_fjpend` (0.5 s) and WAIT; A three frames into `ma_fjpend` is a jump the
next frame. The electric floor shocks on the 11th tick (10 of grace), holds him 121 ticks, 1 HP on the first, 60 ticks invincible after,
shocks again 60 ticks later with no new grace; at 1 HP the shock ends in `ELEC_DOWN` (`ma_shock_down`), R respawns.

### Pass 51 -- the fire and shock effects: FireDamage.tscn, ShockSparks.tscn, ShockPuff.tscn

The three effects of pass 50 as scenes under Mario, generated from the disc's `.jpa` files by `tools/projectshine/gen_fx_fire.py`
(every number derived from the data; the field maths per noclip's JPA.ts, the colour keys stretched over the life as calc type Merge
does): `Fx/FireDamage.tscn` -- the burn's cloud, `ms_firedmg_a`, a bound one-shot copy per burn at his feet (`SmsUnboundBurst.SpawnBound`:
a child of the node, so the emitter rides along while the born puffs stay in the world, as a pos-ptr bind does); `Fx/ShockSparks.tscn`
-- `ms_m_biri_a / b / c` and c's spark children, four GPUParticles3D bound to the centre joint, emitting while `ELECTRIC_DAMAGE` ticks
and stopped the frame it ends (the game's bind-1 lifetime); `Fx/ShockPuff.tscn` -- `ms_moe_fire_off` at the centre + 30 units as the
shock ends. Textures baked from the files into `Fx/tex/` (kemuball3_fire, kemuball3_dark, biri_l1, biri_r (two frames), hitmrk1,
shikaku). Adopted in `SmsSetupFx` with the pass-29 rule (a missing scene is an error, the effect simply does not play); instanced in
`Player.tscn` after `SmsBubbles` with his editor closed. See Fire and electricity for what each shows and what was approximated
(the sine-wave flicker and the fast spin of the sparks, the sideways drift of the clouds, the children spawning in the same sphere as
their flash). The look is for his eyes: the headless run proves the scenes load, bind and switch with the statuses.

### Pass 52 -- fences and the roof hang

Jaysonn (2026-10-01): "might as well get the code done for the hanging and fences". The four fence statuses and the four roof statuses
as two new states (`MarioState.fence` / `roofHang`) off the one `sms_fence` tag, with the catches hooked where the game has them: the
run's wall contact, every jump's wall hit, the swim's wall, and the ceiling met while rising -- see Fences and the roof hang. The
clips (`ma_fence_*`, `ma_ladder_hang_*`, `ma_hgup`) are library-only and driven directly; `SmsParams.HangRoof` / `.Fence` hold the
shipped and code constants. Two sound slots on Mario: `FencePunchSound`, `FenceCatchSound`.

Verified headless (a drive spawning a 6 x 4 x 1.2 m tagged wall and a 4 x 0.3 x 4 m tagged slab 2.5 m up): the run into the wall is
`FENCE_CATCH`, the catch clip 0.31 s then `FENCE_MOVE`; a full stick up climbs at 3.9 m/s (`ma_fence_move_up`), right sidesteps at 4.0 m/s
(`ma_fence_move_l`, the camera's right); B is the punch with the hit at frame 5; A is `WALL_JUMP` at vy 62 away from it; climbing on, the
top 149 units above the feet is `ASCEND` and he stands on the fence's top. A jump under the slab is `HANG_ROOF` with the feet 160 units
under it (26 above the floor), `WAIT_ROOF` after the catch clip, the stick is `MOVE_ROOF` at 4.0 units a tick (`ma_ladder_hang_move_r`),
the slab's edge is `LANDING`; hand-over-hand into a fence wall is `FENCE_CATCH`. The kick is built, not driven.

### Pass 53 -- the normal camera: CPolarSubCamera's follow camera and its kind records

`assets/SunshineCamera.cs` is now the game's camera (cameragc.cpp ctrlGameCamera_ / calcPosAndAt_ / calcFinalPosAndAt_, CameraNormal.cpp
ctrlNormalOrTowerCamera_, CameraHeightPan.cpp, CameraBGCheck.cpp, CameraInbetween.cpp, CameraKindParam.cpp, CameraSecureView.cpp,
CameraChange.cpp's status ladder and mode change, CameraMarioData.cpp), on the same node and rig as before: the spring arm's own
processing is off and the Camera3D is posed each 120 Hz tick; this node sits on the look-at and the pivot's yaw is the eye's bearing, as
Mario's stick and the Y camera read them. The legacy spring-arm follow, its held-L lock, its own Y over-the-shoulder mode and the unused
ShineGet rail are gone (NO LEGACY); the TalkingPOV shot (RedCoinSwitch) stays and hands back through the game's resume
(calcNowTargetFromPosAndAt_ + a 16-tick blend).

`assets/Sms/SmsCameraKinds.cs` is GENERATED (tools/projectshine/gen_camera_kinds.py) from the disc's /Camera/kind*.prm and
camera_normal.prm: 14 kinds (Follow, Delfino, Indoor, Swimming, AquaticTurbo, Turbo, Hang, Fence, WallJump, HipAttack, Hovering,
RocketJump, LookDown, LNormal), the 46 runtime fields each, a field a .prm lacks at the camerasave.cpp default (kindRocketJump.prm has 92
fields), the Inbet table (the CURRENT kind's mSLInbet<new> is the blend into the new kind: 16 mostly, Follow->Hang 0 -> 1, Turbo->Follow 30,
AquaticTurbo->Swimming 300), and camera_normal.prm's 23 shipped globals + 3 defaults. `assets/Sms/MarioCamData.cs` (partial Mario) is what
the camera reads off him each frame: his origin, his face (*gpMarioAngleY: the port's face yaw is "behind him" in the camera's bearing
convention, so the face is that + 0x8000), the move distances (his displacement per 30 fps frame SQUARED, clamped 100 -- the camera ticks
at 120 Hz but Mario moves on CUE_MOVEMENT_GATE_A | B, once a frame, so every tick sees the frame's move; HANGING / ASCEND read 0), the
status bits the ladder tests, isMarioReadyGun_ (the FLUDD and a SQUAT status: the only statuses with type bit 0x8000), the aim ratio unk1C
(the gun angle over the current nozzle's LAngleMin).

Per tick, the game's order: the look point (his origin + AtOffsetY + ratio x XRotRatioAtOffsetY; +260 on KICK_ROOF_ROLL_UP); the status ->
kind ladder (dash -> TURBO / AQUATIC_TURBO; swimming -> SWIMMING, neither on a Delfino Plaza stage; fence; ROCKET / ROCKET_LANDING ->
HOVERING with the hover nozzle, else ROCKET_JUMP; the ROOF family -> HANG (the ledge hang is FOLLOW: HANGING carries no 0x200000); HIP_DROP
-> HIP_ATTACK unless the area kind shows through it; a non-follow area kind; WALL_JUMP / WALL_SLIDE; FOLLOW); the kind record blended
over the inbetween (inbetweenData: 1/remaining of each gap a tick, OffsetAngle and the AutoChase frames at once); the stick (the C-stick
through PADClamp's sub-stick region 59 / 31 / 15 and JUTGamePad's 42: yaw += x * YAngleManualSpeedX a tick, ratio -= y * 0.01 a tick, up =
lower and nearer); without a stick the auto follow toward behind him at FollowSpeedX (JumpFollowSpeedX airborne) x how far off ((1 - cos
2d) / 2; linear for HOVERING, with the move fixed at 100) x the obstruct magnifier when a ray from the eye to his waist meets the world
(MARIO_FLAG_OCCLUDED is a framebuffer peek in the game) x the L-held magnifier x his move distance x the auto-chase ramp, clamped; the
squat aim's hold (unk2AC: the pitch offset and the extra distance chased toward the aim ratio's share of the kind's manual yaw speed and
HoldAddDistXZ at AimAngleYChaseMin 60 / HoldDistChase 0.02); the squat aim's yaw chase behind him eased from 60; the polar solve of the
DESIRED eye (unk18) about the look point with the cushion rule (a kind with CushionMin 0 keeps its exact distance; a cushioned kind lets
him approach and holds the eye), the MinCushionXZ 300 floor and the hold pull-in; the height pan (the eye rides last tick's height toward
the desired at HeightPanChaseRateY while airborne and rising, then settles at PanAfterMagnif 0.01 but at least 1.5 u a tick; the look-at
gets TargetAtJumpOffsetY -100 airborne at 20 a tick); the wall check (the walls within 150 u of the eye's XZ at last tick's eye height + 10
push it out by the overlap; a push recomputes pitch / yaw from the pushed eye and restores LAST tick's ratio) and the ground check (the eye
kept 144 u over the ground under it, 30 u when squat-aiming, probed from last tick's eye height + 20; skipped 120 ticks when the eye sits
over 1.25 x the kind's highest reach); the inbetween's polar sweep of eye and look-at; the actual eye chasing the inbetween's at
PosChaseRateXZ / Y (the _C variants eased in while the stick moves and he stands still; 1 under the L swing) and the look-at at
AtChaseRateXZ / Y; the look-at's elevation from the eye clamped to +-15872. The L press (MEANING_CAM_L) is execFrontRotate_: the yaw closes
a tenth of its gap to behind him each tick with the eye following at once, no wall check meanwhile (as before, now inside the same rig).
The Y camera (MarioYCam.cs) hands back the way the game pops L_NORMAL: a 16-tick blend from the L camera's pose and record into the kind,
the yaw where the L camera left it. A respawn / the intro's spawn / a teleport (20 m in a frame) starts the camera behind him as a stage
load does (loadAfter: ratio XRotStart 0, the kind's near distance, no lag anywhere).

The decomp's calcPosAndAt_ is NonMatching and mislabels one field: against the original asm (QbeRoot/sms asm/Camera/cameragc.s) the
polar solve stores to mCurrentTarget.unk18 (the desired eye), mCurrentTarget.mPosition is the panned eye whose x / z are copied from unk18
just before execHeightPan_ (stfs 0x98 -> 0x80, 0xA0 -> 0x88), finalAt.y is mCurrentTarget.mTarget.y (lfs 0x90), and the ratio restored
after a wall push is mPreviousTarget.unk28 (lfs 0xDC), not unk30. mHoldOffsetAngleX* and mSLHoldAngleXChase are dead data (never read).

Not ported, logged: the in-house (OOB) magnifier (no OOB data), the roof check (Monte village only -- a dummy roof at -512.5 everywhere
else, so the game skips it), the camera cubes / fixed and definite cameras / the camera map tools (pass 54: an area's kind by a tag;
`DefaultKind` on the CamController node is the stage's area kind meanwhile, `DelfinoPlazaRules` the map-7 exceptions), the notice camera
(no bosses), the tower / parallel / rail / wire / underground / diving / jet-coaster kinds, the 0x8000 CAMERA_WONT_CLIP surface flag (every
static body clips the camera; metadata `sms_camera_pass` on a body or a parent opts it out), the secure view's turn-rate slowdown (unk9C;
every carried kind ships SecureViewDist 0 so it is a no-op), SE 0x4826 on the L press. CLIMB is unreachable (the BAR statuses: no bars).
DEVIATION kept from pass 17: an L press in the air is the hip drop's, never the camera's (the game's execFrontRotate_ refuses only while
the status is already HIP_DROP).

### Pass 54 -- the model's rest facing (he faced +Z)
Since the project's first day Mario had to be placed at rotation 180 in every level: the imported model's rest pose faces +Z
(every Sunshine model does), while Mario.cs and Godot take -Z as forward (YawFromDir = atan2(-x, -z), Vector3.Forward,
Camera3D). The armature's yaw is set from world directions in the body's frame, so with the body unrotated the mesh ran
backwards; the 180 on the body flipped it right, and the camera carried an ArmatureForwardIsFlipped (+180) to read the facing.
FIX (scene-first): Player.tscn's Armature/Skeleton3D is rotated 180 about Y, so the mesh faces the armature's -Z; the siblings
fitted to the mesh in the body's frame mirror with it (WaistTarget's basis, HangAnchor, pickupCast at -Z, the collision cylinder's
offset). Level.tscn, secrect_level.tscn and Rico4Secret.tscn's PlayerSpawner go back to rotation 0 -- he still starts facing -Z.
The camera's ArmatureForwardIsFlipped, Mario.cs's MODEL_YAW_OFFSET knob and the one +Z read (the head-look debug summary) are
gone. START FACING: Mario._Ready reads the node's authored yaw once (the game's stage carries one in its Mario-set-position entry,
MarDirectorDirect.cpp 538) into the face angle and the armature, then zeroes the body -- so rotating Mario or a PlayerSpawner in
a level now turns his start facing instead of inverting the controls; the spawner hands its yaw over before the add.
Also in this pass: the camera (pass 53) wrote the eye's bearing into the pivot's LOCAL yaw; Mario.cs composes the stick as
(body basis) x (pivot yaw), so under the 180 the stick ran him toward the camera -- the pivot's yaw is global again (76e19d7).
Headless: stick up -> velocity . camera-forward +1.00, mesh facing . velocity +1.00, armature -Z . velocity +1.00, body yaw 0.

### Pass 55 -- the fire and the shock after his playtest
FIRE: the game keeps his run speed through a burn -- checkGraffitoFire saves mForwardVel and mVel.y, makeGraffitoDamage's damageExec
reverses the speed (the floor actor sits 1 unit in front), then `if (unk55C > 0)` (a constant 50) puts the saved speed back and
changePlayerStatus(FIRE_DOWN, 1) only sets vy = FireDownForce 50 (MarioMove.cpp 827-856, verified in the asm: the 0xB0 / 0xA8 stores
after the call); in flight fireDowning() only decays it 0.35 a tick (FConverge is linear), so a sprint (32 a tick) carries ~1460 units,
~20 m, before the two 0.4x bounces -- the speedrunners' fire boost. A landing back in the fire while still burning turns the face 180
and sends him back the way he came, which is the "bounce back" of a wide goop patch. Jaysonn wants the first touch to send him back,
and not far: DEVIATION SmsParams.Jump.GraffitoFireStatusArg = 0 (the game: 1) -- the hot floor enters FIRE_DOWN with the status arg
no shipped caller passes, whose kick is the designers' own -FireBackVelocity (jump.prm 8): 8 a tick back along the face, facing the
fire, about 1.2 m, the same vy 50. Fire enemies (ReceiveMsg) keep arg 1 as in the game. FireDamage.tscn: the cloud's ramp x0.8
(FIRE_SMOKE_DARKEN, "a little darker"). SHOCK: ShockPuff.tscn's quad x1.25 (SHOCK_PUFF_SCALE, "slightly bigger"). THE NEGATIVE: the
game's TMario::drawSpecial -> drawLogic() (MarioDraw.cpp 2058-2108) draws the body model's two draw buffers again during
MARIO_STATUS_ELECTRIC_DAMAGE with GX_BM_LOGIC and mBlendLogicOp = 10 = GX_LO_INV (MarioInit.cpp 420) after a colour-off depth pass:
every pixel the body covers becomes its own inverse, every frame, no flicker (Ref/Electric.png). Ported as assets/Sms/Fx/ShockNegative
(.tscn + SmsShockNegative.cs + ShockNegative.gdshader): a material overlay on the meshes under Armature (the water gun's subtree
excluded -- TWaterGun draws its own model) whose shader writes 1 - screen at its pixels; reading the screen puts it in the transparent
pass over the shaded body at its own depth. On with ELECTRIC_DAMAGE, off at its end, at ELEC_DOWN and at a respawn. The earlier log
called this "a blue shimmer": wrong -- it is a logic-op inversion.
FIX (his playtest, 23:13): a lethal burn taken as he landed back in the fire left him standing at 0 HP -- EnterDead() ran inside the
surface checks after the move, and the frame's tail landing block (airborne -> floor) skipped `hurt` but not `dead`, so it re-landed him as
idle. `dead` joins the skip list: loserExec()'s status is final. Drive: HP 1 standing -> dead; HP 2, floor kept hot -> burn, the landing's
re-burn -> dead and stays.

### Pass 56 -- the damage fog and the loser statuses (his playtest, 23:23)
"MARIO SHOULD PULSE RED WHEN HE'S HIT": the game's TMario::addDamageFog (MarioDraw.cpp 2185, from perform's draw): while
mInvincibilityFrames > 0 and he is not talking, every material of the body, the cap and the hands gets a fog of colour (0xFF, 0x00,
0x80); SMS_AddDamageFogEffect (DrawUtil.cpp) sets its range about his view depth d -- start = d - 700 + 300 s, end = d + 500 + 300 s,
s = sin(moveTick x 0x888), a 30-tick (1 s) pulse free-running on the director's tick -- so the linear fog factor at his depth is
(700 - 300 s) / 1200 = 7/12 - s/4: he blends 33..83 % toward the pink. (The fog type is taken as linear; the BMD's fog block was not
read.) PORT: assets/Sms/Fx/BodyOverlay (.tscn, SmsBodyOverlay.cs, BodyOverlay.gdshader) REPLACES ShockNegative: one overlay on the
meshes under Armature (the water gun's subtree excluded) whose shader does the game's two draws in the game's order -- mix(screen,
(1, 0, 0.5), fog) then, when negative, 1 - c. Mario pushes addDamageFog's condition each tick (invincible and not talking); the node
runs the pulse in _Process. The shock's negative is the same node's Negative. THE LOSER STATUSES ("the final hit still sends him
straight up and then he lands then plays the animation"): loserExec() (MarioCollision.cpp 202) sets MARIO_FLAG_GAME_OVER and
DOWN_LOSER; downLoser() (MarioAutodemo.cpp 148) drops him straight down in ANIM_LAND (setPlayerVelocity(0) each tick) until the floor,
then LOSER_DOWN (loserDown(), MarioRun.cpp 1474): ANIM_DIE + the voice. For the hot floor, checkGraffitoFire's changePlayerStatus(
FIRE_DOWN, 1) then REPLACES DOWN_LOSER (changePlayerStatus refuses only from LOSER_DOWN), so the fire jump flies at 0 HP; the game's
director takes the GAME_OVER flag two ticks later and fades the stage out mid-flight (STATE_UNK7: the Miss screen, a 2 s wipe, the
stage reload). The port has no stage reload: the fire jump lands and the landing takes the loser pose (SmsTickFireDown -> SmsLoserDown),
no bounces. EnterDead() is now loserExec()'s pair: airborne -> ma_land down to the floor -> ma_die; grounded -> ma_die at once (as
the game). _smsGameOver blocks re-burns and hits (the decHP guards); Respawn clears it. The reaction voices (MV08A / MV08B) are not
ported.

### Pass 57 -- camera areas
The game's stage carries camera cubes (TCubeCameraInfo, CubeMapTool.cpp: an oriented box and a camera code, or a camera map tool for a
fixed / definite camera). Every tick CPolarSubCamera::controlByCameraCode_ (CameraCodeControl.cpp 9) tests Mario's position + 75 units
against the cubes in list order; the first hit's code is the param_1 of execCameraModeChangeProc_'s ladder (CameraChange.cpp 568):
the kind the area asks for, which the status kinds (dash, swim, fence, rocket, roof, hip drop) override as the ladder says, and which
itself beats the wall jump and the climb unless it is FOLLOW; no cube -> code -1 -> the stage's initial mode. PORT: assets/Sms/
SmsCameraArea.tscn (+ SmsCameraArea.cs, [GlobalClass]): an Area3D with a box, Kind (the kind dropdown) and Priority (overlaps). Drop
it into a level and size it; or put metadata `sms_camera_kind` = a kind's name on any Area3D. SunshineCamera samples the point
(feet + 75 u) with a PhysicsPointQueryParameters3D (areas only) each tick before the mode change; outside every area the ladder starts
from CamController.DefaultKind as before. Entering / leaving an area blends over the kinds' inbetween as any kind change. NOT PORTED:
the camera map tools (fixed cameras that hold a position and a pitch/yaw, definite cameras that hold a position and watch him), the
area cubes' Delfino hotel floor rule, the door-opening hold.

### Pass 58 -- the miss sequence
The game after loserExec(): TMarDirector (MarDirectorDirect.cpp 798-822) sees MARIO_FLAG_GAME_OVER, sets GAME_OVER_PENDING, and the next
tick enters STATE_UNK7 (769-787): the lives flag 0x20001 down one, the console's startAppearMiss, a BGM, and the fader's request -- with
a life left startWipe(10, 0, 2.2) (the hell death: startWipe(2, 0, 2.0), the iris), with none startWipe(0xD, 0, 2.0). The wipe runs
after its delay; when the fader is fully out and the BGM has ended (or 1200 frames passed) (408-430): lives left -> flag 0x30002,
decideNextStageOfMiss + moveStage (the stage from its start, no intro, unkE4 = 0xF: a plain 0.4 s fade in at the next start), the fader
black meanwhile; none -> the save / title flow. Mario keeps flying his FIRE_DOWN or lying in LOSER_DOWN while this runs. TOO BAD!:
TConsoleStr (ConsoleStr.cpp 315 / 554): big_tx_1.blo's ms00..ms06 (78 x 78 at y 338 on its 600 x 480 screen; textures big_tx_t/o/o/
b/a/d/ex), letter i from frame i x 10: a quadratic drop from 270 above to 30 below over 60 frames with a full turn and alpha +12 a
frame, a bounce 110 up over 40 at +60, back over 40 at +100, out over 30 at +300 (never reached: the stage has moved). The death wipe
(hx_wiper.c Hx_Test2 / Hxs1_Test2): four black bands, each the part of a ring about a centre 200 / 250 px off the right edge --
(840,300) 650..900 from the bottom up, (840,150) 450..700 from the top down, (890,370) 400..650 up, (890,300) 200..420 down --
revealed row by row along an Hx motion of 500 rows (2 frames in, 8 / 8 / 7 / 9 cruising, 1 braking; timers 11 / 11 / 10 / 12), the
earlier bands held: curved stripes filling the screen from the left in ~1.5 s at the wiper's 30 frames a second. PORT: assets/Sms/
Console/ConsoleStr.tscn + SmsConsoleStr.cs (CanvasLayer 15; the letters placed about the blo's centre, 120 Hz console frames x
SmsHud.Anim), Fader.tscn + SmsFader.cs + Fader.gdshader (CanvasLayer 100: StartWipe(type, delay), SetBlack, FadeIn; Arcs, Circle,
FadeOut, FadeIn; FrameRate 30), both under Mario in Player.tscn; Mario.SmsMissTick from SmsAfterMove runs the director's state (the
lives in GameData.Lives across the reload, GameData.CameFromMiss for Level.cs: no intro, black, a 0.4 s fade in); Mario.GameOverScene
(empty: the stage again, lives reset), Mario.MissJingle (a slot). The dead state's R key is gone: the sequence moves the stage. NOT
PORTED: the Hx_GameOver wipe (a texture the port does not have: a plain 1 s fade stands in), the save / title flow after a game over,
the letters' sparkle (emitter 0x1F9), the miss BGM, the hell death's trigger (no death plane here yet), the camera's dead demo.

### Pass 59 -- the pause menu
TPauseMenu2 (GC2D/PauseMenu2.cpp) on pause_1.blo: 'mask' (juice_mask, black at alpha 180 -- a flat texture), 't_0' at (296, 258)
with the PAUSE letters pa00..pa04 (big_tx p/a/u/s/e, 78 x 78, tilted 12) and the items tx_1 CONTINUE (182 x 42, white 3cff3c = the
selected colour), tx_2 SAVE, tx_3 EXIT AREA, 'brek' at (296, 412) with the water_back bar (293 x 34, 8200b4, alpha 180) and the
'map' / 'task' text boxes (the stage's and the episode's names from stagename.bmg / scenarioname.bmg). Two items where the stage is
Delfino Plaza (mNumItems 2). The director (MarDirectorDirect.cpp 805-816): Start in play with checkActionThing3 (not a jumping status)
-> STATE_PAUSE_MENU, else SY_NOT_COLLECT; the game pauses (pauseOn, startPause); the console's pauseIn brings the star HUD and the
lives in and the telop out. appearWindow (175): a 0.5-a-frame clock; letter i from 5 i over 20 units: inset 0.75 (19 - a), hop
2 n (1 - n) 80, rotation 180 + orig - 9 a, alpha 12.8 a; the mask to its alpha by 30; the items from 30: +-3 / 1.5 px a unit through
40 and back by 45, alpha 26 a unit, tx_1's tilt turning in; open at 46. Open (301): Start / A confirms, B closes, up / down move the
cursor (no wrap with two items, SY_CURSOR); the selected item pulses +1 px a unit to 18 and back by 35 with its red channel +10 a unit
(toward white); sparks 0x1FA at the key frame. disappearWindow (246): 10 units -- the pane alpha -12 a frame, the mask and bar following,
every letter and item shrinking 2 % a frame with a drift, the menu pane turning 2 degrees a unit. getNextState (514): CONTINUE -> play,
SAVE -> the card save then back, EXIT AREA -> decideNextStage + moveStage with unkE4 = 2 (the iris). PORT: assets/Sms/Console/
PauseMenu.tscn + SmsPauseMenu.cs (CanvasLayer 50, process mode Always; the tree paused while open; Root placed about the blo's
centre; PauseAction "button_start" -- Escape and the joypad's Start stand in at run time when the Input Map lacks it; ExitAreaScene
(empty: two items, as the plaza); ItemColor; PauseOn / CursorMove / NotNow sound slots), under Mario in Player.tscn. Mario.SmsCanPause
(the pausable rule + not during the Miss / a death / the shine demo / a talk), GameData.SaveNow, Level.StageName / ScenarioName for
the bar. The console frames are this port's 120 Hz ticks (pass 38): the clock runs 60 units a second x SmsHud.Anim. NOT PORTED: the
card-save dialog (SAVE writes GameData and the menu dips 40 units), the sparks, the console's pauseIn / pauseOut, the letters' drift
on the way out (a uniform 2 % shrink), the shadow copies of the two text lines, the system font (a Label).

### Pass 60 -- the wall kick's dust, the wall slide's dust, the slide's smoke
TMario::startJumpWall (MarioJump.cpp 16): emitParticle(PARTICLE_MS_WALLKICK_A, angle) and (_B, angle) at mPosition, angle = the wall
normal's bearing + 0x8000 (facing into the wall). ms_wallkick_a.jpa: a 15-unit circle, one puff (rate 1 for one frame), omni 13 u/f
x (1 +- 0.125), airResist 0.9 a frame (130 units in all), life 35 +- 50 %, Billboard 1.8 (90 units) kemuball3 lerp (26,26,2) ->
(255,255,242), alpha 0.875 out over the last eighth, spin +- 0.125 turns/frame; the 3 u/f downward component is dropped.
ms_wallkick_b.jpa: a 20-unit circle, eight (rate 8 for one frame), life 6, DirectionCross 0.9 x 1.0 (45 x 50 units) with scale
X 0.09 -> 0.125, Y 0.07 -> 0.5 over the life, rotation random +- 1 turn and 0.072 +- 1 turns/frame, hitmrk1 lerp (207,207,183) ->
white, alpha 0.875 (the texture's half-tiling and scroll dropped). TMario::wallSlipEffect (MarioParticle.cpp 309): emitAndBindToPosPtr
(MARIWALLSL1, &mPosition, 1) each WALL_SLIDE tick with mVel.y < -10 (MarioJump.cpp 623). ms_mariwallsl1.jpa: a 20-unit shell, 0.8 a
frame, life 60 +- 35 %, up 2 u/f (+- 30 %, spread 0.35), Gravity +0.7 up from 25 %, Drag 0.88 fading in over 34 %, Billboard 1.2 (60
units), alpha 0.875 -> 0 from 50 %. TMario::frontSlipEffect (326) from the slipping walkProcess (MarioRun.cpp 1102): plain ground ->
emitAndBindToMtxPtr(SLIPSMOKE, the root joint's matrix, 1). ms_m_slipsmoke.jpa: a 15-unit shell (minRad 0.75) 15 up, 1.5 a frame, life
50 +- 35 %, thrown 6 u/f behind him (the emitter's (0,0,-1): the game's -Z is his back) and 2.5 up, spread 0.175, Gravity +0.7 up
fading in over 52 %, Drag 0.88, Billboard 1.2, alpha 0 -> 0.8 by 10 % -> 0 from 50 %. PORT (gen_fx_dust.py -> assets/Sms/Fx/
WallKickDust.tscn + SmsWallKickDust.cs (a top-level one-shot pair, Play(feet, yaw)), WallSlideDust.tscn + SmsWallSlideDust.cs and
SlipSmoke.tscn + SmsSlipSmoke.cs (SmsFollowEffect, AtFeet; the smoke TurnWithOwner so it flies behind him), the existing kemuball3 and
hitmrk1 bakes): DoWallJump and the fence's jump-off play the kick at the feet facing into the wall; TickWallSlide sets the dust's
Active by the vy rule; the belly slide and the slope slip set the smoke's; the frame's end clears either when its status is gone (the
emitter invalidated). Jaysonn's SlideDustFx (node + assets/SlideDustFx.cs) is REPLACED. SmsFollowEffect: the owner-frame offsets and
the new turning follow the ARMATURE (his facing) -- the body never turns since pass 54 -- and AtFeet follows mPosition. NOT PORTED:
the wet ground's spray (ms_m_watslide_a/b/c), the sand's (ms_m_slidesand_a/b), the puff's downward 3 u/f, the hit marks' texture
scroll; the airResist brake is a linear one over the same distance.

### Pass 61 -- the big letters coloured and free, the death wipe's frame
The console's big letters (big_tx_*.bti, 64 x 64 CMPR: white glyphs with a black outline) are laid out by hand in big_tx_1.blo /
pause_1.blo as 78 x 78 panes, white (PIC1 white ffffffff; TConsoleStr and TPauseMenu2 never tint them): TOO BAD! at x 76 / 133 / 186 /
274 / 335 / 400 / 463, y 338 (advances 57, 53, 88 across the space, 61, 65, 63); PAUSE off 't_0' at (-153,-111) (-102,-100) (-52,-141)
(13,-116) (86,-116), each ROT 12. The disc has 17 letters (a b d e g h i m n o p r s t u y, !); Font/sms_alphabet_textures (a fan pack)
adds c f j k l q v w x z -- the whole alphabet is now assets/Sms/hud/big_tx_{letter}.png. PORT: assets/Sms/Console/SmsBigText.cs builds
a word from a string (a glyph's pane from the texture's aspect, advancing by its opaque width + gap (-8, the blo's mean hand spacing, a
space 28; a word wider than MaxWidth (760 = a 16:9 window) is built smaller to fit), centred on a point, an optional vertical wave and tilt, SelfModulate from a palette in turn; {0} in the path format = the
letter, "ex" for !, or a ShineLetter{0}.tres style token). SmsConsoleStr: MissText (TOO BAD!) built about the blo's word centre
(308.5, 377), rebuilt when a Level's MissText differs; SmsPauseMenu: TitleText (PAUSE) under 't_0' at centre (-3, -78) on the game's
wave (+5.8, +16.8, -24.2, +0.8, +0.8), tilt -12, a Level's PauseTitle winning; both Colored with Jaysonn's Shine Get palette (green,
pink, yellow, teal, orange, pink-red) -- DEVIATION, his: the game's words are fixed and white (Colored off = the game). Level.cs:
MissText / PauseTitle exports. The scenes' hand-placed panes are gone. THE WIPE: the Hx wiper's frame is the game's RENDER size,
640 x 448 (TSMSFader::setDisplaySize(SMSGetGameRenderWidth/Height), Resolution.cpp 61-63; Hx_ResetWipe), and Hxs1_Test2 skips rows
off that frame without spending its count; the port stretched a 640 x 480 frame, whose bottom 30 rows the four rings do not cover (a
wedge at x 215-222 on rows 478-480, Jaysonn's Ref/screenWipe.png) -- Fader.gdshader now works in 640 x 448 (the iris centre (320, 224)).
NOT PORTED: the letters' sparkle (0x1F9) still; the items (CONTINUE / SAVE / EXIT AREA) keep the game's white + selected green.

### Pass 62 -- holding, carrying and throwing (TTakeActor)
The carry is a mutual TTakeActor link (mHeldObject / mHolder, include/Strategic/TakeActor.hpp 35) validated every tick by considerTake
(MarioCollision.cpp 406). THE TAKE is a collision event: hitWantToTake (MarioCheckCol.cpp 101) -> canTake = B HELD (MEANING_B) &&
isTakeSituation (86): no invincibility frames, not in a status flagged 0x80000000 / 0x10000000 nor CATCH, not on Yoshi, the upper state
IDLE, not holding or held, the object within +-0x2aaa (60 deg) of mFaceAngle.y and within mAttackRadius + its damage radius -- and
mAttackRadius IS mario.prm HoldRadius 80 (setNormalAttackArea, MarioMove.cpp 228). The collision check runs before the status machine,
so a B press against a fruit takes it before running() can dive. taking() (MarioAction.cpp 8): ANIM_RAISE at 1.0, waitProcess (standing);
HIT_MESSAGE_TAKE as the clip passes frame 11 (the object snaps to getTakingMtx = the RIGHT HAND joint, MarioDraw.cpp 866; the exert voice),
UPPER_STATE_HOLDING_OBJECT + WAIT at the last frame; mInput & 4 (the floor > 100 units under him) -> changePlayerDropping(LANDING).
THE CARRY is only the upper state: bck 0x4B = ma_carry_p grafted on the second root over whatever the body plays (MarioDraw.cpp 1880);
no run speed change; it blocks the ledge / fence / roof / wire catches (mHeldObject == nullptr gates) and the pump (checkPumpEnable);
jumping is allowed; the WATER keeps the hold (no dropObject in MarioSwim.cpp -- he swims with it; the port's own drop at the swim entry
is gone). Sweat while running > 20 (not ported). THE THROW: isThrowStart (MarioRun.cpp 50, the run / brake / turn ticks; MarioWait.cpp 566
standing) on mInput & 0x2000 = B's frame press (TMarioGamePad B = 0x200 -- the spec's 'X' was wrong): an enemy (actor type bit 0x10000000)
-> PITCHING always; a map object -> PITCHING when mForwardVel > 16, else PUTTING if canPut() (MarioWait.cpp 65: no wall within the
object's radius 100 units ahead at y + 10 nor at his feet), else kept. pitching() / putting() are stopCommon: waitProcess zeroes his speed
at once, the clip at 1.0, WAIT at its end; checkThrowObject (MarioMove.cpp 1282) -> dropObject at ANIM_THROW frame 4 (+ the voice),
HIT_MESSAGE_PUT at ANIM_PUT frame 20. In the air checkJumpingThrowStart (MarioJump.cpp 33) -> JUMP_THROW: ANIM_THROW over the ordinary
jump physics, the release at frame 4, JUMP_THROW_END on landing (jumpThrowEnd: the clip finishes standing). Every other drop is dropObject
= THROWN: changePlayerDropping on a knockdown (damageExec, MarioCollision.cpp 274 -- plus a type-3 hit while running, via setUpperDamageRun's
FIXED_ANIMATION upper state and considerTake), the bonk, the fire, the catch. THE OBJECT computes its own launch (TMapObjGeneral::thrown,
MapObjGeneral.cpp 109): position = HIS position (the feet), vel = gpMarioThrowPower 2.8 x physical.unk2C x (sin, cos)(his yaw) +
mNormalThrowSpeedRate 0.5 x his mVel (zero on a ground throw, live in the air and on a hit), vy = unk30, += vel once; then gravity unk0
a tick with no air drag (calcVelocity 477), the ground bounce vy *= -unk4 while |vy| > unkC (touchGround 437). The numbers are compiled in
(MapObjInit.cpp; no .prm): coconut 2.0 / 15, papaya 5.0 / 20, pine 2.0 / 22 (hit radius 80), durian 0.01 / 5 (kicked, never carried),
banana 2.5 / 14 (bounce 0.4), red pepper 5.0 / 12, watermelon 0 / 0 (rolled), barrel 3.5 / 20, gravity 0.3 and bounce 0.6 unless noted.
put(): objRadius + 42 + 10 ahead of him at his feet. PORT: SmsParams.Take; assets/Sms/SmsTakeable.cs = the per-object numbers (an
optional child node with a Preset dropdown; without one the port reads his Fruit.Type -- Banana / Coconut / Papaya / Pineapple / RedPepper /
Durian -- else the generic fruit) and SmsThrownFlight (a thrown RigidBody3D gets the game's gravity (0.3 x 3600 / 74.44 = 14.5 m/s2 against
the project's 19) and restitution for the flight, his own back once asleep or re-taken; the bounce friction and wall reflection stay his
material). Mario.cs: SmsFindTakeTarget over the "pickable" group (his Fruit.CanBePickedUp / SmsTakeable.CanTake respected), the raise's
frame-11 attach, SmsThrowOrPut, SmsCanPut (eight horizontal rays stand in for isTouchedOneWall), SmsStartJumpThrow + the landing's
JUMP_THROW_END, SmsReleaseHeld (THROWN / PUT), the ma_carry_p graft (SmsUpperPose) for the tree-off frames; B with nothing in reach:
TAKE_POSE (ma_get_fail) only while moving / landing (running() 668, landing() 1005) -- waiting() tests mInput & 0x10000, which nothing
sets, so standing still B does nothing (the legacy's swipe is gone). ExertVoice is the slot. NOT PORTED: the sweat, the per-fruit
rotation handed to the thrown object, the object's own startSound(5) / startAnim(5), TAKEN (Mario carried by an enemy) and THROWN_DOWN.

### Pass 63 -- riding moving and rotating objects
TMario::checkRideMovement (MarioMove.cpp 1706) runs at the TOP of the tick (playerControl 2381: checkPlayerAction, before his own move and
the BG check) and checkRideReCalc (1750) at the BOTTOM (2392). The game carries a rider by the owner's MATRIX, not by a velocity: his
position is kept as a point in the ride actor's local space (unk2F4) and put through the actor's CURRENT transform each tick
(getRidingMtx: SMS_GetActorMtx = TRS of mPosition / mRotation / mScaling, or the root joint's matrix for a bone-driven object) --
translation and the turn about the origin in one multiply -- and mFaceAngle.y is turned by the actor's yaw delta (mRotation.y - unk30C,
degrees x 32768 / 180). His own move then happens in world space and the local point is re-measured at the bottom, so walking on a
turntable composes exactly; standing still he orbits the origin and turns with it. ATTACH (read against the original assembly, QbeRoot
asm/Player/MarioMove.s: the decomp's 'wall != nullptr' is wrong -- r5 = mGroundPlane->mActor is what is tested): the floor triangle's
owner exists, not a JUMPING-type status, isTouchGround4cm; or the hip drop's sit phases (state 2 / 3); or a 0x20000000 status (the
fences, ascend / descend) with a wall of an actor near the 40-unit probe -> that actor. The mount tick seeds unk30C and the local
point and moves nothing. LEAVING: actor null -> unk2C0 = null, NOTHING added to mVel -- a jump off a moving lift is his own jump, the
lift goes on without him. A descending deck keeps him (position-driven) while he reads grounded. No ride .prm; the constants (0.8 x 50
probe, 50 / unk15C wall check, 4 cm) are compiled in. PORT: the engine's CharacterBody3D carry is OFF (PlatformFloorLayers /
PlatformWallLayers 0, PlatformOnLeave DoNothing -- it moved him by the contact point's velocity, knew nothing of the pivot (a tangent
step a frame: an outward drift on a fast turntable), never turned his face, and ADDED the platform's velocity on leaving). SmsRideApply
at the top of _PhysicsProcess (GlobalPosition = body.GlobalTransform x _smsRideLocal; _smsFaceYaw / lastFacingDirection / the armature
turned by the body's Euler-Y delta) and SmsRideReCalc after MoveAndSlide (the floor collider that is an AnimatableBody3D or in the group
"sms_ride", grounded, not an air / pinned state -> mount or re-measure; else off). RideKeepVelocityOnLeave (Mario export, off) is the
DEVIATION knob that adds the platform's point velocity back when he leaves, as the engine used to. NOT PORTED: the fence / roof hang
on a moving object (the port's ledge tracking is already the game's rule; the fence pin is not), the rail ride (TOROCCO is its own
path), the camera's response to a turntable (none in checkRideMovement).

### Pass 62b -- fruit size, the ball's roll-out, the fail-pose knob, and what the legacy lost in 62 / 63
SIZE: the game scales an object by its stage-data mScaling: the model is drawn at it and TMapObjBase::setObjHitData (MapObjBase.cpp 70)
makes the hit cylinder radius x max(scale.x, scale.z), height x scale.y; MapObjInit.cpp 10985 scales the body radius / head height the
same; the flight numbers (throw rate, vy, gravity, bounce) are per TYPE, never per scale. Port: Fruit.Size (export, per instance) and
SmsTakeable.Size scale the model root(s), every collision shape (a duplicate of the shared shape, so one big coconut does not resize
every coconut), an Area3D's shapes, and -- through SmsTakeable.SizeOf / SmsTakeData.Scaled -- Mario's take reach and put spacing. The
RigidBody3D itself stays at scale 1 (Godot's bodies must; his Anim_AttachPickup resets a body's scale to 1 anyway, which is why a scaled
fruit root used to shrink the moment it was picked up). HIT ROWS corrected against the *_hit_data_tables (MapObjInit.cpp 1536-1700):
fruit {60, 60, 80, 90} (RedPepper_data points at fruit_obj_hit_info), coconut / papaya {40, 60, 50, 80}, pine {80, 60, 50, 80}, banana
{80, 60, 60, 90}, durian {150, 60, 60, 90} -- the pass-62 rows had 40 for the banana, durian, red pepper and generic fruit.
THE FRUIT IS A BALL: TResetFruit : TMapObjBall : TMapObjGeneral. TResetFruit::thrown (asm 801BAFF4) = TMapObjGeneral::thrown + state 0xB,
so the LAUNCH stays as pass 62 ported it: the object is placed at HIS position (the feet) + one frame of velocity, vel = 2.8
(mThrowPower) x the type's rate along his face + 0.5 x his mVel, vy = the type's -- and a GROUND throw is a stopCommon state whose
waitProcess (MarioPhysics.cpp 202) zeroes mVel at once, so his run adds NOTHING on the ground (only a jump throw adds 0.5 x his air
speed); the coconut (rate 2.0, vy 15, g 0.3) is a 1.3 m high, 1.9 m long lob, the papaya (rate 5, vy 20) flies 6 m. TMapObjBall overrides
touchGround / rebound / touchWall / touchRoof / checkWallCollision / calcCurrentMtx (the rolling), none decompiled: read in the asm
(QbeRoot MapObjBall.s): touchGround (801BCC64) lands when vy > -(0x188) (vy = 0, y = ground) else rebound (801BCFBC: velocity reflected
off the GROUND normal with 1 + unk4 (Bounce), y = ground, airborne), then on every ground contact vx, vz *= unk10 (BounceFriction 0.97)
plus a slope term (0x180) x the ground normal's x / z while resting; calcVelocity (MapObjGeneral.cpp 490) zeroes a component under unkC
on a flat floor. The two per-fruit numbers (0x180, 0x188) and the roll's visual rate are float literals TMapObjBall::initMapObj loads by
object id from the DOL's pool (.incbin baserom.dol) -- UNKNOWN until his dump has sys/main.dol (the US DOL; the same file unlocks the
sound index). PORTED NOW: the ground roll-out (SmsThrownFlight.RollOut: on a map floor vx, vz and the spin decay by BounceFriction a 60 Hz
tick -- the port's per-frame-decay clock since pass 50 -- and stop under BounceMinVy on a flat floor), so a thrown coconut rolls ~2.5 m and
stops instead of rolling off on Godot's friction. PENDING THE DOL: the ball's own landing threshold / slope acceleration / spin; Godot's
restitution and rolling stand in.
FAIL POSE: TakePoseStanding (Mario export, off = the game) plays TAKE_POSE (ma_get_fail) on B while standing still. The shipped game does
not: waiting() (MarioWait.cpp 115) tests mInput & 0x10000, and no code sets that bit (MarioMove.cpp 1487-1515 set 1, 2, 4, 8, 0x20, 0x80,
0x100, 0x200, 0x2000, 0x4000, 0x8000); running() 668 and landing() 1005 are the live TAKE_POSE entries -- a walking / running / landing
B with nothing to take and no dive.
LEGACY RETIRED BY PASS 62 (recorded at his request -- every pass lists what it replaced from here on): the pickup cast (Mario's pickupCast
ShapeCast3D, UpdatePickupCastFacing / TryFindPickable -- the fruit's PickupZone Area3D was already unused) -> SmsFindTakeTarget over the
"pickable" group with the game's test (hit cylinders overlapping, 80 + the object's radius, +-0x2aaa of his face); the legacy throw
(THROW_FORWARD_BOOST / THROW_UP_BOOST, Fruit.ThrowSpeedMultiplier, the X-button throw) -> SmsThrowOrPut / SmsReleaseHeld with the game's
thrown(); the gentle drop on Anim_DetachPickup -> every drop is dropObject() (THROWN), the put is HIT_MESSAGE_PUT only; the swim-entry drop
-> removed (the game keeps the object in water); the fruit's own GravityScale / LinearDamp / bounce during a throw -> the type's numbers for
the flight, his profile handed back at rest (Fruit.ApplyPhysicsProfile still rules a fruit nobody threw). STILL HIS: the durian kick
(contacts + impulse), LaunchAsDrop, the red pepper's rotation lock, the Anim_AttachPickup carry socket (and its three GD.Print scale
lines, which print on every pickup). LEGACY RETIRED BY PASS 63: Godot's CharacterBody3D platform carry (PlatformFloorLayers /
PlatformWallLayers / PlatformOnLeave = add velocity) and SmsStepVisualCheckSnap's GetPlatformVelocity test -> SmsRideApply / SmsRideReCalc.

### Pass 62c -- the retired carry / throw legacy removed; the standing fail pose on; throw-feel knobs
REMOVED (his ask: "remove anything that's not used anymore" -- the SMS take / throw of pass 62 had left these as fallbacks; the port's
rule is replace, not fall back): Mario.cs -- the pickup cast (the pickupCast / pickupCastInitialLocalPos fields and their _Ready setup,
UpdatePickupCastFacing, TryFindPickable), the belly-slide pickup block that used the cast (the SMS held-B take already covers a slide:
SmsCanTakeNow allows diving / bellySlidingFromDive on the floor, as the game's isTakeSituation does), the B-press fallbacks (the 45-degree
instant throw / gentle put: StartThrow, StartPutDown, ThrowHeldInstant, Anim_FinishThrow, THROW_FORWARD_BOOST 9 / THROW_UP_BOOST 3 /
THROW_UP_VELOCITY 39 / THROW_FORWARD_VELOCITY 28, _queuedThrowFacing, Anim_DetachPickup's legacy tail), TryStartFruitPickup (no caller
since the PickupZone auto-pickup went), the GD.Print(Scale) in _Ready and the three scale prints in Anim_AttachPickup, the "[Throw]
Airborne intercept fired" print. Fruit.cs -- ThrowSpeedMultiplier (the game has per-type rates in the physical table instead). KEPT:
Anim_AttachPickup (the carry socket attach, scale reset to 1), Anim_DetachPickup (= SmsReleaseHeld), StartPickupRaise, _isThrowQueued
(PITCHING vs PUTTING), _aerialThrowLockTimer (the SMS jump throw uses the guard), OnAnimationFinished's putDown branch as a safety net,
the fruit's own physics profile (rules a fruit nobody threw), the durian kick, LaunchAsDrop. NOT REMOVED (scenes, his editor open): the
`pickupCast` ShapeCast3D node in assets/Player.tscn and the `PickupZone` Area3D in the six fruit scenes -- both unused; delete them in
the editor or say when it is closed. With UseSmsPhysics off the take / throw now has no path (by the rule).
STANDING FAIL POSE: TakePoseStanding defaults ON (his ask) -- a DEVIATION: the shipped game does nothing on B while standing (waiting()'s
0x10000 input bit is never set); off = the game.
THROW FEEL (his "too light and slow", twice): DEVIATION knobs on Mario, with the game at 1 / false: ThrowPowerScale 1.8 (the forward
launch: the game's mThrowPower 2.8 x the type's rate -- coconut 5.6 u/f = 2.3 m/s, now 4.1), ThrowVyScale 1.15 (the type's vy -- coconut
15 = 6.0 m/s, now 7.0), ThrowGravityScale 1.6 (the type's gravity -- coconut 0.3 = 14.5 m/s2, now 23: a 0.60 s, 1.0 m high arc instead
of 0.83 s and 1.3 m), ThrowKeepsRunSpeed true (0.5 x the speed he had when B was pressed goes into the object; the game's ground throw
adds nothing because stopCommon's waitProcess zeroes mVel before the frame-4 release -- at a 32 u/f run that is +6.5 m/s). The flight
numbers reach the object through SmsTakeData.WithGravity; _smsThrowRunVel is sampled in SmsThrowOrPut before the stop. The game's own
feel: try a papaya (rate 5: 5.6 m/s forward even at the game's values).

### Pass 62d -- the dive takes a fruit with B held (no bonk); the belly slide does not
His 22:05 ("when you dive into a fruit and hold B Mario will still grab the fruit and won't bonk"), checked in the source: the take is a
collision event (hitWantToTake -> canTake = MEANING_B held && isTakeSituation, MarioCollision.cpp 86). isTakeSituation excludes: invincibility,
the 0x80000000 statuses (PITCHING 0x80000588, PUTTING 0x80000387, JUMP_THROW 0x820008ab -- the decomp's '// Airborn?' comment is a wrong
guess: JUMP is 0x2000880 and DIVE 0x891, neither carries the bit), the 0x10000000 family (BAR_*, WIRE_*, FENCE_*, HANGING, ASCEND / DESCEND,
TAKEN, READ_BILLBOARD, the SINK / DOWN losers), CATCH (0x800456 -- the belly slide the dive lands into, MarioJump.cpp 475), Yoshi, busy hands,
holding / held; then the face cone and 80 + radius. taking() (MarioAction.cpp 8) first tests mInput & 4 (MarioMove.cpp 1699: his feet more
than 100 units above the floor) -> changePlayerDropping(LANDING), else waitProcess puts his feet on the floor while the raise plays. So:
a DIVE with B held into a fruit within 100 units of the floor TAKES it (he lands and raises -- no bonk: the dive's wall check is against
the map, a fruit is a hit actor that boundByActor bounces), a jump with B held does the same near the floor, a BELLY SLIDE into one does
not (CATCH is excluded). PORT: SmsCanTakeNow -- the floor within 100 units (was IsOnFloor), the dive family (diving, *JumpDive) and the
plain jumps allowed, bellySlidingFromDive dropped; the held-B take runs in the airborne branch too, with waitProcess's floor snap (also
every raise tick while he is off the floor -- SmsFloorYBelow); TryEnterDiveBonkAfterMove and the run's wall push ignore a collider that is a
RigidBody3D / CharacterBody3D / Area3D (SmsIsNonMapBody: the dive / slide bonk and the wall push only against the map; running into a
coconut shoves it, as boundByActor does, instead of the wallPush stance the port fell into). His legacy had the dive-slide pickup by the cast (removed in 62c)
and the slide take (62's SmsCanTakeNow, now the game's no). NOT PORTED: boundByActor (the fruit bounced by Mario's dive: TMapObjBall
asm with DOL literals -- Godot's push stands in), the take from water (the same 100-unit rule applies; shallow water would take).
FRUIT ATTRIBUTES, re-checked against MapObjInit.cpp 1543-1716 (his 22:05 "does all the fruit have the right attributes"): every port
column matches -- {gravity, bounce, wallReflect, minVy, groundFriction, ., airDrag, wallCheckUp, ., ., ., throwRate, throwVy}: coconut
{0.3, 0.6, 0.3, 0.3, 0.97, 0.98, 1.0, 5.0, 0.5, 0.72, 0, 2.0, 15}, papaya the same with 5.0 / 20, pine 2.0 / 22, red pepper 5.0 / 12,
durian {0.3, 0.6, 0.2, 0.2, 0.97, ..., 0.01, 5} (kicked), banana {0.3, 0.4, 0.2, 1.0, 0.4, 0.5, 1.0, 5.0, 0.5, 0.72, 0, 2.5, 14}. Not
modelled: unk14 (0.98 / banana 0.5), unk20 0.5, unk24 0.72, unk28 0 -- their readers are in the asm. So every fruit DOES throw differently
in the game: the papaya flies fast and far (rate 5, vy 20), the red pepper fast and low (5 / 12), the coconut a short lob (2 / 15), the
pineapple a high short lob (2 / 22), the banana medium (2.5 / 14) and dead on landing (bounce 0.4, friction 0.4 a tick, rest under 1.0),
the durian is kicked (0.01 / 5). The throw knobs (62c) scale all types alike, so the differences stay.

### Pass 62e -- the unused pickup nodes out of the scenes
With his editor closed (10-02 22:26): assets/Player.tscn loses the `pickupCast` ShapeCast3D and its BoxShape3D_uiq8p (the legacy grab probe
pass 62 replaced and 62c unreferenced); fruit_coconut / fruitpapaya / fruit_banana / fruit_pineapple / fruit_red_pepper lose the
`PickupZone` Area3D with its CollisionShape3D and SphereShape3D (the auto-pickup zone Fruit.cs stopped using; the durian never had one);
load_steps recounted (8 -> 7). Text surgery (tscn_strip.py), not an editor re-save, so nothing else in the files moved; Player.tscn was
staged as HEAD + this hunk through the index, his uncommitted editor edits (the uid fixes, the BodyLag expansion, ThrowPowerScale 5.03 /
ThrowVyScale 3.645) stay his. Checked headless: all seven scenes load and instantiate with no stale pickup node; the boot is clean.

### Pass 62f -- the ma_hgjmp state and the button_start action (his two editor to-dos)
With the editor still closed (10-02 22:35): assets/Player.tscn's BaseSM state machine gets `ma_hgjmp` (AnimationNodeAnimation_hgjmp on the
clip the AnimationPlayer library already held -- SetMarioState(ledgeHopUp) travels to it; every vault used to log 'not found' and play
nothing). The machine has only the Start -> ma_wait transition: the port's Travel() calls teleport (Godot's reset_on_teleport), so a state
needs no transitions, as every other state here. project.godot gets `button_start` = joypad button 6 (Start / Xbox Menu / Nintendo +) +
the Enter key, the action SmsPauseMenu.PauseAction reads (Escape stays the separate key_esc action). Both files carry his uncommitted
editor edits: staged as HEAD + these hunks through the index, his stay his. Checked headless (a SceneTree script): BaseSM.has_node
("ma_hgjmp") true and a travel to it accepted; InputMap.action_get_events("button_start") = the two events; the boot is clean.

### Pass 64 -- ordinary goop by the sms_pollution tag (SLIP, SINK, INSTAKILL, GLASS_WALL, SAFE)
THE GAME: a goop LAYER (TPollutionLayer) has a type (EPollutionType: 0 SINK, 1 FIRE, 2 SLIP, 3 GLASS_WALL, 4 ELECTRIC, 5 INSTAKILL, 6
SAFE, 7 = FIRE again, 8 / 10 none) and a painted map; checkGraffito (MarioMove.cpp 953), once a tick after the move, reads the type under
his feet and "isDirty" = a ring of isPolluted samples 32 units apart (3 x 3 at his height for SLIP / INSTAKILL / SAFE, 5 at the floor for
SINK / FIRE / GLASS_WALL, 1 for ELECTRIC) all painted; then the dispatch (ELECTRIC / FIRE already ported; SLIP -> checkGraffitoSlip;
GLASS_WALL -> mPosition.x / z = mPrevPosition: a wall; SINK / INSTAKILL / SAFE nothing here), MARIO_FLAG_DIRTY, the footprint stamps
(MAP_POLLUTION_MS_M_ASHIOS in the top half of a 520-tick window) and the sink splash; then thinkDirty (1129) and checkSink (1183).
SLIP (checkGraffitoSlip 862, on the ground): the brake and skid timer for the status he is in (OIL_SLIP / OIL_SLOPE: 1.0 / 60; OIL_RUN /
OIL_PULLING: 0.98 / 10), then: floor normal.y <= 0.95 -> OIL_SLOPE (brake 1, 60); JUMP_CATCH / CATCH / OIL_SLIP / OIL_SLOPE -> OIL_SLIP
(1, 60); anything else but CATCH_LOST -> OIL_RUN (0.98, 10; PULLING -> OIL_PULLING). The fog clock: 600 (240 yellow + 360 red) set while
not yet DIRTY, down a tick, floorDamageExec(1, 3, 0, mMotorReturn) as it passes 360, 600 again at 0 -- 1 HP every 600 ticks standing in
plain goop (5 s at the 120 Hz movement tick), the fog yellow-green (0x82, 0x96, 0x00) above 240 and the damage pink below (addDamageFog,
MarioDraw.cpp 2184). Off the ground, OIL_SLIP / OIL_SLOPE keep brake 0.998 / 60. oilRun (MarioRun.cpp 1238): A -> speed 0, JUMP; B ->
mVel.y 20, JUMP_CATCH(1); |vx|, |vz| < 1 -> WAIT; pollute(70); the face to the stick at 256 BAM a tick; mVel += mag x face x 0.01; the
timer down, 0 -> brake 0; mVel x brake (v = 0.98(v + 0.32) -> 15.7 u/f at a full stick, half the run); ANIM_RUN2 at 0.5 x mag x 0.25
(ANIM_WAIT 0.5 / ANIM_SQWAT with no stick), the slip SE 0x1001; walkProcess 0 -> LANDING. oilSlip (1306): A jumps; face at 150 BAM;
timer 0 -> brake 0 and CATCH; pollute(130), SE 0x1141; mForwardVel += mag x cos(face - stick) x 0.0001, x brake, setPlayerVelocity;
|fwd| < 1 -> CATCH_LOST; the clip's frame clamped at 50. oilSlope (1355): the timer, pollute(130), slipBackCommon(CATCH_LOST, LANDING).
thinkDirty: mDirty += 0.15 (RUN / OIL_RUN) / 1.5 (CATCH / OIL_SLIP / OIL_SLOPE) / 0.5 (SLIP) while DIRTY, -= 0.3 in water (+ the melt
particles), 0.5 in a spin jump, 0.2 the tick he leaves water, clamped 0..150 -- it feeds the melt effect only. SINK (checkSink): not
invincible, the floor within 100 units, DIRTY on SINK: mSinkTimer += 1, HELD at 240 x 0.6 = 144 while HP > 0 (so the drown past 240 is
reached only at 0 HP), floorDamageExec(1) every 90 move ticks (gpMarDirector->mMoveTickCount), MSD_SE_MA_SINK_IN_MUD, past 240 ->
loserExec + SINK_LOSER (0x10001123), the sink splash, the cry; the model is drawn (timer / 240) x 150 units lower (MarioDraw.cpp 1760);
the stick x ((0.3 - 0.3)(1 - t) + 0.3) = 0.3 (checkController 1327); a jump's vy and fwd x 0.5 and the depth bleeds (1 - t) x 120
(setStatusToJumping 657). Off the paint the depth is 0 at once. INSTAKILL: speed 0, loserExec, SINK_LOSER the tick it confirms.
GLASS_WALL: the revert only. SAFE: DIRTY (+ footprints) and nothing else. POISON is not a pollution type (a WATER type; dmggraffitopoison
3 HP is applied by the water side -- not this pass). DIZZY is dead code (mDizzyTimer never set; DizzyPower 0 shipped).
PORT: sms_pollution = fire | electric (as before) | slip / oil / goop / paint / normal | sink / mud | instakill / kill / lethal | glasswall /
glass / wall | safe / clean / harmless (fire2 / unk7 = fire), on the floor body or a parent up to three levels. isDirty = on the floor with
a type (the tagged floor is painted all over: NO paint map -- nothing spread or stamped, FLUDD cannot clean it, SAFE is invisible).
SmsCheckGraffito dispatches per 120 Hz tick, then SmsThinkDirty and SmsCheckSink. New MarioState oilRun / oilSlip; OIL_SLOPE = the
surface slip (SmsEnterSurfaceSlip) flagged _smsOilSlopeSlide, re-asserted every tick on the steep paint. SmsOilFrame owns the ground
frame in OIL_RUN / OIL_SLIP (the tree off, ma_run2 at the slip rate via the AnimationPlayer's SpeedScale, ma_wait 0.5 with no stick,
ma_slpbk for the skid; the rate reset the tick the state is left any other way). OIL_RUN enters from his ground statuses only (idle,
pivot, sneak, walk, run, sprint, the landings, rolloutRun, carrying, crouch, wallPush / Shuffle, pickupFail, the surface slip) -- the game
converts any status but CATCH_LOST; the knockdowns, deaths, pinned and demo states keep theirs here. SmsGoopFloorDamage = SmsDamageExec
(the floor actor 1 unit ahead, 1 HP, type 3, min speed 16, 60 ticks invincible). SmsBodyOverlay.SetFogColor (fog_color uniform):
GoopYellow while dirty on SLIP paint with the clock above 240, DamagePink otherwise; the fog is on while invincible OR dirty on SLIP.
SmsCheckSink as the game, EnterDead for SINK_LOSER (the port's loser pose; ma_sink_down is in the library, not wired); SmsSinkDraw
offsets the armature by (depth / 240) x 150 units and restores it; the stick scale in the stick read, the jump scale in SmsLaunch.
GlassWall: GlobalPosition.x / z back to _smsTickStartPos (sampled after the ride carry). NOT PORTED: the paint map and everything on it
(pollute, the footprints, the sink / melt / goop splash particles, FLUDD cleaning), the slip / mud sounds and voices, OIL_PULLING, the
SQWAT variant of the OIL_RUN idle, the OIL_SLIP frame clamp, the water decrements of mDirty beyond swimming. CLOCK: every "tick" above is
the port's 120 Hz movement tick (SmsUnits.TicksFor), as the fire's 50 invincible ticks are.
ALSO: the step-up's sub-centimetre branch now puts him ON a real lip (rise > 2 mm) with the frame's remaining motion -- a goop slab
authored as a thin StaticBody3D on top of a floor used to stop a sprint dead (speed kept, position blocked every frame).

### Pass 65 -- the goop layer: the paint map Mario samples, spreads and FLUDD cleans (TPollutionLayer)
THE GAME: goop is a LAYER (TPollutionLayer, Map/PollutionLayer.cpp): a joint model whose I8 texture IS the paint map -- one byte a
texel (0 clean .. 255), texels of info->mTexelSize units over the layer's rectangle (mLeft / mTop / mRight / mBottom), a power-of-two
grid (log2 width / height) -- plus a HEIGHT MAP (TPollutionPos::mHeightMap, one depth unit = 40 units: worldToDepth = (y - offset) x
0.025; 0xFF = no surface). isPolluted(x, y, z) (249): inside the rectangle, the texel's surface within +-2 depth units (unk48 = 2 ->
80 units) of y (TPollutionPos::isSame), paint above mPollutedThreshold (30; FIRE 128; UNK7 200). The initial paint (initTexImage) is
the layer's /scene/map/pollution/<name>.bmp, each surface texel losing mEdgeAlpha 50 per neighbouring non-surface texel. STAMPS
(stamp 233 -> the GPU pass PollutionCount.cpp doTask / initGXforStamp): a quad of worldToTexSize(size) texels (size is the FULL width;
under two texels the quad has no area and paints nothing), each texel whose height is within +-2 of the stamp's blended toward 255
(type 1, pollute: GX_CC_ONE) or toward the stamp texture's colour (type 0, clean: black) by the stamp texture's alpha (SRCALPHA /
INVSRCALPHA); the default clean / pollute stamps are textures (registerTexStamp(0 / 1, 0xff, tex) -- not in the decomp). THE SETTLE
(countTexDegree -> drawPollutionLayer, a draw-sync pass at the 30 fps render rate): paint UNDER mPerFrameChangeThreshold (50; FIRE 128)
loses mPerFrameChangeDelta 8 a frame ("clean-ish stuff gets cleaner" -- the half-cleaned rim of a splash is gone in a quarter second);
a UNK7 layer's paint ABOVE 160 GAINS 8 ("dirty stuff gets dirtier": the regrowing fire goop); FLAG_DECAY: every texel loses 2. The
count = texels passing alpha > 0 -> getPollutionDegree sums the layers; cleanedAll = degree < TMapEventSink::mCleanedDegree (per stage;
Mare's wall events use 42 / 50). action() (PollutionAction.cpp 139, a CUE_MOVE tick): the type's effects (fire: MS_NEWFIRE_A/B + SE_EF_FIRE
at polluted spots near Mario; electric: MS_THUNDER_S + SE_EF_ELEC; glass: a counter), then with paint within mSpreadArea 2000 of Mario
(getPollutedPos: five random points at his height) PARTICLE_MS_RAKU_INDAWA and, FLAG_CAN_SPREAD, spread(): every 4th tick
(mSpreadFrequency 3) pollute(a painted texel near Mario no higher than him (getPollutedPosNear: (rnd - 0.5)(0.7 + rnd) x 2000), 128).
cleaned() (170): every 6th clean call probes one spot from two offset tables (x 32 units); polluted and over 100 there ->
MSD_SE_ERASE_SCRAWL + PARTICLE_MS_RAKU_KIE (MAPOBJ_SANDSTEAM on map 5). TPollutionManager (gpPollution): getPollutionType = the FIRST
layer whose rectangle holds the point (no height asked); isPolluted = any layer; stamp = every layer; clean(x, y, z, size) = stamp(0)
(Delfino's sea floor below -10 excepted); pollute = stamp(1); stampGround = plane-type-0 layers (the floors; TPollutionLayerWallPlusX /
PlusZ project along an axis). MARIO: checkGraffito (MarioMove.cpp 953): illegal ground / Yoshi / Shadow Mario -> out;
mPollutionTypeStandingOn = getPollutionType(mPosition); isDirty = SLIP / INSTAKILL / SAFE: all NINE points of a 3 x 3 ring 32 units
apart at mPosition.y painted; SINK / FIRE / GLASS_WALL / UNK7: the FIVE points of a cross at mFloorPosition.y; ELECTRIC: the one point
at the floor; UNK8 / UNK10: clean. Then the dispatch (pass 64), the DIRTY flag, and on isTouchGround4cm: the sink-in effect, the
footprint timer (520 set in checkGraffitoSlip's grounded branch and in checkSink's mud branch; down a grounded tick; footprints
(emitDirtyFootPrint -> MAP_POLLUTION_MS_M_ASHIOS, a particle, mPolSizeFootPrint 0: they paint nothing) while above 260; zeroed in water
and in a spin jump). oilRun / oilSlip / oilSlope call pollute(mPosition, 70 / 130 / 130) every move tick; setStatusToJumping (432)
stamps pollute(mPosition, mPolSizeJump 250) when the footprint timer is above 260 -- the goop he tracks on a jump. checkGraffitoSlip's
airborne branch (918): OIL_SLIP / OIL_SLOPE off the ground keep mBrakeSlipNoPollute 0.998 and the 60-tick timer. FLUDD:
TModelWaterManager::splashGround / splashWall (ModelWaterManager.cpp 316 / 328): a landed drop calls gpPollution->clean(pos, mCleanSize
x 10 (floor) / x 32 (wall)) every frame it lives on the plane -- wtp00 normal 18, 15 hover 20, 14 rocket 12, 13 turbo 10, 10 back 5, 02
bomb 24, 03 trgrocket 25 (the shipped wtp .prm).
PORT: assets/Sms/SmsPollutionLayer.cs = a [GlobalClass] MeshInstance3D (TPollutionLayer): Type, TexelSize 32, InitialPaint (Empty /
Full / Texture: red = paint, pixel (x, y) = texel from the -X / -Z corner), Decay, CanSpread, GoopShader. Build(): the mesh's world AABB
in XZ is the rectangle, the texel count the next power of two (<= 1024), the height map rasterised from the mesh's triangles (the
highest surface), VerticalOffset = the lowest vertex. The paint, the R8 Image / ImageTexture uploaded once a frame when changed, the
material: the mesh's own ShaderMaterial or a new one on assets/Sms/Fx/Goop.gdshader (world-XZ lookup of pollution_map by layer_min /
layer_inv_size; alpha = smoothstep over the layer's threshold; procedural blotches + an optional albedo -- no goop textures in the
assets). IsPolluted / Stamp (the quad, the height mask, a soft disc for the stamp textures: full to 60 % of the radius) / the 30 Hz
Settle (threshold / delta / decay / regrow as the game; Degree = texels above 0) / Action every movement tick (the spread; no effects) /
Cleaned signal (the game's effect rule, your sound + particle hook) / PaintAt / SurfaceYAt. SmsPollutionMap (TPollutionManager, static):
Layers in tree order, GetPollutionType (first rectangle), IsPolluted, Stamp / Pollute / Clean, PollutionDegree, CleanedAll(n), MarioPos
(the spread probes), EnsureImplicitLayers: a StaticBody3D / CSG with metadata sms_pollution (on it or a parent up to three up) gets an
SmsPollutionLayer over its collision AABB, the height map from a ray per texel onto that body, painted full, drawn by nothing -- the
pass-56 / 64 tags keep working as layers (his Level.tscn's electric floor). assets/Sms/Goop.tscn: a 10 x 10 m slip layer to instance.
MARIO: SmsPollutionSample(feet) = the 9 / 5 / 1-point rings (SmsFloorYBelow(2000) for mFloorPosition; none -> illegal -> clean);
SmsPollutionMap.MarioPos each frame; _smsGround4cm = on the floor or within 4 units; SmsPolluteTick (70 in oilRun, 130 in oilSlip and
the goop slope) each move tick; _smsFootPrintTimer (520 on paint / mud, down a grounded tick, 0 in water / a spin jump) and the jump
stamp (250) in SmsLaunch while it is above 260; SmsGraffitoSlip's airborne branch. WATER: SmsWaterDrops cleans at the hit point the tick
a drop lands and every tick it sits (CleanSize x 10 / x 32; wall = normal.y < 0.5 -- the game asks the polygon's class).
LEGACY RETIRED: (1) the floor BODY's sms_pollution tag as the sampled thing -- SmsUpdateSurface's `_smsPollution = SmsPollutionOf(hit)`
and SmsPollutionOf (passes 56 / 64: "the tagged floor is painted all over", no paint, no cleaning) -> the layer sampling; the tag itself
survives only as EnsureImplicitLayers' shorthand for a full layer over the body; (2) `IsOnFloor() && type != None` as isDirty -> the
game's rings (a hop over goop is dirty, a bridge over it is not); (3) the `sms_surface = goop` alias of ForceSlip (the slope-slip
surface class) -- goop is the layer now; forceslip / all remain.
NOT PORTED: the wall layers, the stamp textures (soft discs stand in), the joint-object / model stamps (enemies painting), the prohibit
areas and revival stamps, the layer effects (fire / thunder / wave particles, RAKU_INDAWA, RAKU_KIE: the Cleaned signal is the hook),
the sounds (ERASE_SCRAWL, EF_FIRE, EF_ELEC), the footprints' particle, the sink-in splash, Delfino's sea-floor exception, stampModel.
DEVIATIONS (logged, not knobs): getPollutedPosNear reads the z texel by z (the decomp uses the x mapping for z -- a game bug); the
initial paint's edge rule clamps at 0 where the game's u8 wraps; stamps exclude no-surface texels outright (the game's height compare
lets 0xFF through only for a stamp 253+ units up).

### Pass 65b -- the game's goop textures (assets/Sms/Fx/Goop) on the layer
He dropped the six goop surface textures (250 x 250, from the game's BTIs) in assets/Sms/Fx/Goop: BrownGoop (the ordinary paint),
OilGoop (the dark glossy one), ElectricGoop (teal + yellow), FireGoop (orange), PinkGoop (pink + yellow + orange), BetaGoop (the demo's,
with alpha). SmsPollutionLayer: [Export] Texture2D GoopTexture; empty = DefaultGoopTexture(Type) -- FireGoop for Fire / FireRegrow,
ElectricGoop for Electric, BrownGoop for everything else (Oil / Pink / Beta are picks). When the layer made the material itself it sets
goop_albedo and blotch_mix 0 (the texture as painted); a ShaderMaterial of yours only gets the map uniforms. Goop.gdshader:
blotch_mix (0 = the texture, 1 = tinted by the procedural blotches -- the default for a layer with no texture), albedo_tiles_per_metre
0.5 (the swirl repeats every 2 m), the texture tiles by world XZ like the paint. RETIRED AS THE DEFAULT LOOK: the procedural blotches
(kept behind blotch_mix for a layer without a texture). The .import files are the editor's defaults (detect_3d/compress_to=1: the
editor re-imports them VRAM-compressed with mipmaps on their first 3D use). NOTE: the game's layer models carry their own UVs; the port
tiles by world distance -- albedo_tiles_per_metre is the knob if the swirl should be larger / smaller.

### Pass 65o -- fire flames, electric sparks (the layer's own effects), and FLUDD through the triple jump
THE GAME (PollutionAction.cpp 79 / 112, action() 139): a layer's action() runs its type's effect before the goop probe: fire() -- with a
painted spot near Mario (getPollutedPosNear(mFireArea 1600)) the effect timer advances; past mFireEffectWaitTime (20) it plays
SE_EF_FIRE and emits MAP_POLLUTION_MS_NEWFIRE_B then _A at that spot, both setGlobalScale(1.5); electric() -- the same with
mThunderArea 1000, `> 15`, SE_EF_ELEC and MS_THUNDER_S; glassWall() -- a counter only. The electric layer also gets the RAKU_INDAWA
bubble (the non-fire block) -- in the game both share mEffectTimer.
PORT: SmsPollutionLayer: FireEffect / ElectricEffect per the game's probes and timers (FlameEveryTicks 20 = mFireEffectWaitTime, `>`;
SparkEveryTicks 16 = `> 15`; their own timers -- the port's bubbles run on BubbleEveryTicks, so the game's shared timer is split);
FxPool = a MultiMesh of billboard quads in world space (size = the instance scale, age = INSTANCE_CUSTOM.x): GoopFlames (Fx/GoopFlame
.gdshader: an upright cylindrical billboard, a noise-torn tongue, yellow core -> orange -> red edge, additive, growing in and fading;
0.5 x 0.9 m x FlameSize 1.5 = the game's scale; FlameLife 0.7 s, a 0.25 m rise) and GoopSparks (Fx/GoopSpark.gdshader: a camera
billboard, 6 jagged spokes gated per flicker frame around a bright core, cyan-white, additive; SparkSize 0.6 m, SparkLife 0.25 s).
Exports Flames / Sparks / FxMax 24 / the shaders. SmsParams.Pollution: FireArea, FireEffectWaitTime, ThunderArea, GlassWallArea. NOT
PORTED: the sounds (SE_EF_FIRE / SE_EF_ELEC), the real jpa particles (drop them in when extracted: the pools are the hook).
FLUDD BUG: "Mario can't use FLUDD while triple jumping". Cause: checkPumpEnable reads gMarioAnimeData[mAnimationId].isPumpOK(); the port
reads its table by the clip SHOWING, and the port's triple jump shows ma_demo_gate_out_rolling_get (his stand-in for the flip), a demo
row = 0xC8 = no pump. The game's ultraJumping plays ANIM_ROLL (MarioJump.cpp: jumpingBasic(MARIO_STATUS_ULTRA_JUMP, ANIM_ROLL, 0)),
whose row is 0x44 = pump OK. FIX: SmsPumpClip() = "ma_roll" while stateOfMario == tripleJump, else the clip showing; used by
SmsFluddPumpEnabled and SmsFluddBindId. No legacy retired (a data-row lookup corrected).

### Pass 65r -- the layer effects at the game's frame rate, Sunshine fire, the bolt quicker, and the fire goop's height gate
THE GAME: TPollutionLayer::perform(CUE_MOVE) -> action() runs once a FRAME (30 fps); its counters are frame counts: the one mEffectTimer
(electric() `> 15`, the RAKU_INDAWA block `> 15` checked before its own increment, fire() `> mFireEffectWaitTime 20`) and spread()'s
every-fourth. On an electric layer both electric() and the bubble block advance the one timer each frame, so a bolt comes about every 8
frames and the bubble almost never; on other goop a bubble every 16 frames (0.53 s); on fire goop a fire every 21 frames (0.7 s).
checkGraffitoFire (MarioMove.cpp 830): `mPosition.y - mFloorPosition.y > mFireHeight` -> no burn; graffito.prm ships mFireHeight 5.
PORT -- LEGACY RETIRED: (1) the layer ran action() once per 120 Hz TICK -> once per frame (SmsUnits.TicksPerFrame 4): every cadence was
4x the game's (why the bubbles read "too many" at the game's own 16). (2) BubbleEveryTicks 48 (the deviation knob from 65g) -> gone: the
game's `> EffectWaitFrames 15` at the frame rate is sparser than his pick (0.53 s vs 0.4 s). (3) The split _fireTimer / _sparkTimer ->
the game's one shared _effectTimer with its exact ordering. (4) The FxPool flame + Fx/GoopFlame.gdshader -> Fx/GoopFire.tscn: a fire
cluster placed at the painted spot at FlameSize 1.5 (the game's setGlobalScale) -- tongues of the game's own firestripe sprite (per-axis
scale curve: they stretch up and thin, tinted yellow-white -> orange -> red over 0.55 s, rising), glow3_flame cores additive, embers
(firecolor) -- emitting FlameClusterLife 1.2 s then burning out; FlameWaitFrames 20 = mFireEffectWaitTime; FlameScene a knob; the
scene is editor-tweakable like FireDamage.tscn. (5) The fire burn's missing height gate (lost when 65 moved isDirty to the layer sample,
which reads the floor's Y under him, so a jump over fire goop burned in the air; electric needs WAITING / RUNNING so it was fine) ->
`SmsFloorWithin(SmsParams.Graffito.FireHeight)` in SmsGraffitoFire = the game's mFireHeight test.
BOLT (his ask: a little thicker, a shorter burst): GoopSpark thickness 2.0 (was 1.5), SparkLife 0.25 s (was 0.45), climb_time 0.35.
NOT PORTED: SE_EF_FIRE / SE_EF_ELEC, the real jpa emitters (GoopFire.tscn / the bolt pool stand in).

### Pass 65s -- the fire from his reference (tall pale wisps), the bolts at random angles
HIS REFERENCE (Ref/fireRef.png, Pianta Village, The Goopy Inferno): the fire goop's flames are tall, thin, pale yellow-white TRANSLUCENT
wisps -- columns of fire standing on the goop, about Mario's height, wavering a little, their tops dissolving; several visible at once.
Not cartoon tongues. "The electric should shoot up at random angles, not just straight up."
PORT -- LEGACY RETIRED: the 65r fire cluster scene (Fx/GoopFire.tscn: firestripe tongues / glow3_flame cores / embers as GPUParticles3D,
"does not look good") -> one wisp per fire(), as the game emits one fire per fire(): the GoopFlames pool (the same FxPool as the bolts) of
upright billboards FlameWidth 0.32 x FlameHeight 1.2 m x FlameSize 1.5 (the game's setGlobalScale), living FlameLife 2.0 s, drawn by
Fx/GoopFlame.gdshader: the game's glow3_flame sprite (a bright vertical slit in a soft glow) stretched tall, ADDITIVE at opacity 0.75,
pale yellow (near white at the slit), wavering with height (wave 0.07, 2.4 Hz), its body rippling upward (ripple_speed 1.6), a second
narrower out-of-phase sample for the flicker, the top dissolving from 70 %, growing up out of the goop over the first 18 % of its life,
fading from 65 %. DEVIATION knob FlamesPerFire 1 (the game's one; extras go to other painted spots near him). FlameTexture / FlameShader
knobs. The FxPool now carries a per-instance random in INSTANCE_CUSTOM.y and exposes its ShaderMaterial (flame_tex / lean_max are set
there). BOLT: GoopSpark.gdshader lean_max (radians) -- each bolt turns a random angle up to it off vertical, in the billboard's plane
about its base; the layer's SparkLean 32 degrees sets it. NOT PORTED: the sounds, the real jpa emitters (these stand in).

### Pass 65t -- the fire wisp redone against his reference, previewed before shipping
"Again the fire looks nothing like the ref -- it just looks like a line." The 65s wisp stretched the game's glow3_flame sprite (a slit in a
soft glow) tall and weighted it by luminance: the glow vanished over the bright goop and only the slit read -- a line. This time the look
was PREVIEWED: tools/projectshine/wisp_preview.py renders the fragment math (pure Python) additively over goop orange next to his
Ref/fireRef.png, three rounds until it read as the reference's wisps -- thin, wavering, translucent columns with a secondary tongue.
PORT -- LEGACY RETIRED: the textured 65s wisp -> a PROCEDURAL wisp (Fx/GoopFlame.gdshader, no texture; FlameTexture export gone): three
thin soft streaks rising from one spot (the column; two shorter tongues beside it, out of phase), each a Gaussian body exp(-d^2 1.3) about
a centre that sways (two sines + noise) more toward the tip and diverges a little, rippling upward (0.5 + 0.5 noise), a torn dissolving
top, pale orange-yellow edges to near-white centres, a soft foot glow (0.22); additive at opacity 0.7; it grows up out of the goop over
the first 18 % of its life and fades from 65 %. FlameWidth 0.34 (the quad 0.51 x 1.8 m at the game's x1.5). Knobs on the material:
opacity, wave, wave_speed, ripple_speed, foot_glow, outer_color, core_color, intensity, grow_time, die_from.

### Pass 65u -- the fire spread out, more streaks, the animation much faster (his tuning)
"Spread the fire out, add some streaks and make the animation much faster." Previewed again (wisp_preview.py) for the streaks. DEVIATION
(his ask, the game's values stated): FlamesPerFire 3 -- the game emits ONE fire per fire(); the two extras go to other painted spots
within mFireArea (1600 units) around Mario, with lives 0.8-1.2 x. GoopFlame.gdshader: five streaks (the column, two tongues, two short
outer ones; half-widths 0.46 / 0.36 / 0.33 / 0.26 / 0.28), more spread between them (xo x (0.35 + 0.5 hh)), anim_speed 2.6 multiplying
all the motion (sway, ripple, tear), grow_time 0.1. FlameLife 1.5 s (was 2). No legacy retired (tuning).

### Pass 65v -- the fire more aggressive: a wider base, licking, jittering, flickering (his tuning)
"More aggressive looking; a wider base so a bigger circle it spawns from; the animations need to be aggressive." Previewed (wisp_preview.py,
two rounds: the outer streaks hit the quad's sides as a hard cut -> an edge fade). GoopFlame.gdshader: base_spread 0.9 (the streaks fan
out from a wide footprint: xo x (base_spread + 0.4 hh)), a fatter base shape (0.8 + 0.2 sin), each streak's height LICKS (x 0.78-1.1 by a
slow noise), a whole-streak jitter in the sway, wave 0.4, ripple contrast 0.35 + 0.65, tear 0.4 with the top from 0.6, a whole-wisp
flicker 0.82-1.0, the foot glow wider (0.3), anim_speed 4.0, opacity 0.75, an edge fade at the quad's sides. Layer: FlameWidth 0.48,
FlameHeight 1.3 (0.72 x 1.95 m at the game's x1.5). No legacy retired (tuning).

### Pass 66 -- WIRES: the tightropes (TMapWire + Mario's WIRE statuses)
THE GAME (verified in source; the spec on file got three things wrong, see NOTES): a wire is a cube (centre = its bottom face's centre, x / z
half extents, y running UP from it: CLBIsPointInCube) with the tips at its TOP (initTipPoints: centre +- length/2, + size.y), the idle
rope a half-sine sag of size.y/2 under the chord, one render point per 50 units. Mario's position inside the cube IS the grab (the
manager's doActorToWire + the helper actor's hit). receiveMessage(0x40000098): refused in WIRE_JUMP / ROLL_JUMP going up and in the
let-go fall; else bounceVel = 0.2 x -vy, falling onto it -> WIRE_WAIT on top, rising into it (or a roll jump / a dive) -> hanging; the
ends swapped to his facing. Every frame he is held the wire is HANGING with its hang point at his model ROOT (setFootPointsAtHanged
(getTakenMtx)): a V pinned at his feet on top, at his hands under -- the rope clips are authored that way (ma_rope_wait centre +55, the
hang clips -64), so getOnWirePosAngle's `y -= 160` puts his root 160 under the chord in every wire status: the rope's give. specMain
runs the ROPE SPRING every frame: bounceVel -= sag x 0.005; sag += bounceVel; sag x= 0.99; |sag| <= 200. wireWait: A latches, the jump
goes when sag <= 0 (the rope back up): WIRE_JUMP = 42 (+ 3 x -bounceVel when it is moving up) + 25 -> the trampoline; B drops to the
hang; the stick within 45 deg walks 0.1 x counts a frame (ROPE_RUN over 16, rate 0.05 x counts), beyond turns sideways. wireHanging:
A climbs (ROPE_RETURN) with roof - floor >= 160; B lets go (startHangLanding: 60 back, ground within 100 or 100 down, LAND_SAFE_DOWN);
the stick moves hand over hand 3 a frame / climbs / drops by its quadrant; the FLUDD pump spraying kicks the swing (2 x RotStop + the
nozzle's impulse: rocket / turbo 1400, hover 8, spray -8). wireRolling: angVel -= 20 sin(angle), R brakes 0.98, cap 1400, sag = 0.1
|angVel|, A releases ROLL_JUMP in the windows about -0x2000 / 0x6000 with power = angVel x 0.09 at 0xE000 / 0x6000; settled -> hanging;
the model faces a quarter turn on (ANIM_HANG) and pitches. The wire's release(): a damped cosine, amplitude 1.3 x his speed (100 going
down), decay 0.003 x 2|hangPos - 0.5| (none from the exact middle), phase 0.03 a frame. The input bits: 0x2 = A, 0x8000 = B, 0x4000 = R
(0x10000, the sideways drop, is never set).
PORT: assets/Sms/SmsWire.cs ([GlobalClass] Node3D = the cube: LengthUnits, CubeHeightUnits 320 (tips, sag/2, grab height), WidthUnits
100; the ribbon (ImmediateMesh, upper / lower faces + sides), the posts (StaticBody3D cylinders), InCube / PosInWire / SetHang / Release
/ PointOnWire; SmsWireMap.InCube) + assets/Sms/Wire.tscn. Mario.cs: MarioState.wire + SmsWireState (Wait, SWait, the four turns, the
two drops, Hanging, Rolling, Return) + SmsWireAir (Jump, RollJump, SafeDown); SmsWireCheck every frame; SmsTickWire (no other handler
runs); the pose SmsWirePose (root = chord - 160 + the sag turned by pitch then yaw; the model's Godot pitch is the negative: the port's
forward is -Z); the jumps into singleJump with the wire air flags (the landing chain = the second jump's; the roll jump never
clash-crashes; getJumpAccelControl 0.01 via a SmsAir.Tick overload); the drops into ledgeFall with _smsHangDropFall; damage on the wire
-> the safe fall. SmsParams.Wire = wire.prm shipped + MapWire statics + the literals. Per-frame values run per tick at a quarter.
LEGACY RETIRED: none (new mechanic). NOT PORTED: the sounds, kindWire / kindWireHang camera params, the stale mForwardVel in
setPlayerJumpSpeed(0.25, 42) (fwd taken as 0), ANIM_GIANT_ROLLING (ma_roll stands in), the LANDING-with-a-held-object branch's dangling
holder. DEVIATION knobs: SmsWire.DrawSides (the game draws two faces).

### Pass 67 -- the goop STANDING LOCK (oilRun's WAIT re-dispatch), and pass 66z reverted
HIS REPORT: "if mario stands still on goop he can't move at all and is softlocked until he jumps" (he asked for an auto-recover deviation).
THE GAME: oilRun() (MarioRun.cpp 1238) turns a nearly-still Mario (|vx| < 1 and |vz| < 1) into WAIT after zeroing the velocity, and
checkGraffitoSlip (MarioMove.cpp 862) turns any status but CATCH_LOST back into OIL_RUN at the tick's end -- so standing on the paint is a
WAIT <-> OIL_RUN flip every tick. It does not lock because TMario::stateMachine() (MarioMove.cpp 1772) LOOPS while a status change returns
true: the WAIT runs in the same tick, waitingCommonEvents turns a pushed stick into RUN, running()/doRunning() gives the first 1.1 of speed,
and the next tick's oilRun is past the near-still gate and accelerates (0.01 x mag a tick under the 0.98 brake, toward 15.7).
THE BUG (port, pass 64): SmsOilFrame ended the frame at the WAIT change (goto EndFrame), so from a standstill every frame went OIL_RUN ->
WAIT -> OIL_RUN with the stick never reaching the ground code: speed 0 for as long as the stick was held, a jump the only way out. Not a
deviation case -- verified faithful by a headless drive (patch_smoke51.py: a 16 m slip layer under him, stand 0.6 s, stick 1.5 s, release
1.5 s, A): BEFORE max speed 0.00 u/f over 90 frames, state oilRun, the AnimationTree off (a frozen pose); AFTER 3.2 / 6.2 / 10.4 u/f at
frames 3 / 10 / 30, 12.1 at 1.5 s on ma_run2, the release settles to 0 with the tree on, A jumps out. Drive removed before the commit.
PORT: SmsOilFrame checks the near-still exit ONCE A FRAME before the ticks and returns false (the rest of the frame runs as WAIT = the
game's loop; the ground code then gets the frame's ticks exactly once). SmsOilEnter no longer takes the AnimationTree down for OIL_RUN
(OIL_SLIP still does, for its ma_slpbk): the OIL_RUN clip block in SmsOilFrame takes it down each frame it plays RUN2 / WAIT, so the
standing flip keeps the idle clip playing -- the game's waiting() anim; before, the tree was off during every render while he stood on
goop. OIL_SLIP's constant-speed slide on the paint (dirty.prm mBrakeStartValSlip 1.0, walkProcess does not brake, exits = a jump or off
the paint) is UNCHANGED: that is the game, and it was not his lock.
PASS 66z REVERTED (8189c88, an exact restore of Mario.cs from 5e2d7d6): the x4 on _smsElecTicks (120 -> 480), the end-of-shock i-frames
(0x3C -> 240) and _smsRoofTicks (120 -> 480) was wrong. Mario's exec runs every 120 Hz simulation tick (TMarDirector::direct: 4 steps of 5
a 30 fps frame, mPerformListMovement->perform(CUE_MOVE) each -> TMario::perform -> playerControl -> stateMachine + thinkParams +
checkGraffito, no frame gate: TViewObj::testPerform gates only an object whose own unkC carries CUE_MOVEMENT_GATE_A/B), so mStatusTimer
and mInvincibilityFrames count ticks and the game's literals were already right (the electric lock 1 s, its i-frames 0.5 s, the roof hang
1 s). 65r's "thinkParams decrements once a frame" had nothing behind it; the dmg*.prm mInvincibleTime family, FireInvincibleTime,
AirUnitFlashTicks, SuperHipAttackCt and WaitSinkTime stay 1:1 as tick counts -- the "pending sign-off" is withdrawn.
LEGACY RETIRED: none (a port fix). NOT PORTED: unchanged. DEVIATION: none added (the lock was a bug, not the game's behaviour).

### Pass 68 -- the Y (L) camera folded into the rig: CAMERA_MODE_L_NORMAL is a mode of SunshineCamera
THE GAME: the L button camera is not a second camera. CPolarSubCamera's execCameraModeChangeProc_ (CameraChange.cpp 568) takes MEANING_Y in
a normal kind (unk282 out) into execFrontRotate_ (447): the yaw chases behind him with YButtonRotateChase (2: the gap halved a tick) under
CAMERA_FLAG_UNK4 | UNK8; when it settles (CameraNormal.cpp 79-86) UNK10 is set and the next proc's doLButtonCameraOn_ (469) runs
changeCamMode_(L_NORMAL): unk282 = 60, the kind pushed (unk60), setUpToLButtonCamera_ (256) saves the ratio (unk30) and sets it where the pitch
is -OffsetAngleX (the camera level with its target), the current kind's Inbet<LNormal> (16) sweeps from the swing's pose. In the mode
ctrlGameCamera_ (cameragc.cpp 929) runs ctrlLButtonCamera_ (CameraNotice.cpp 208) instead of ctrlNormalOrTowerCamera_: the look target is
the nozzle top (getNozzleTopPos_: the nozzle matrix's origin + 30 up its Y) or his look point, the MAIN stick (-mCompSPos[4] / -mCompSPos[5])
turns the yaw (rotateY_ByStickX_, 300 BAM a tick) and the ratio (rotateX_ByStickY_, 0.01 a tick), then the SAME calcPosAndAt_ -- for a kind
that is not isNormalCameraSpecifyMode the cushion rule is skipped, but the MinCushionXZ push (704: the eye at least 300 u out from his
origin, fabricatedInline3) applies, the slope aim is 0 (not a slope kind), no wall check (isNeedWallCheck_ 121: the L camera or its inbetween)
and no ground check once the inbetween is over (isNeedGroundCheck_ 82), the inbetween and the chase as ever, the record's fov 60 / near 10
blended in with the rest (inbetweenData). Y / A / B (MEANING_Y | MEANING_CAM_AB, unk282 out, the inbetween over) -> doLButtonCameraOff_(false):
unk282 = 60, changeCamMode_(-1) pops the pushed kind with the L record's Inbet (16), setUpFromLButtonCamera_ restores the ratio; a knockdown
(status bit 0x20000), the water, the hover / rocket, the dash, a climb -> Off_(true): one tick. Mario's side (MarioGamePad.cpp 70-88,
playerControl 2449, checkPumping, WaterGun.cpp 219) is unchanged from pass 16-17c.
BEFORE: assets/Sms/MarioYCam.cs ran all of that on a SECOND TopLevel Camera3D made current from the Y press (its own polar inbetween, FOV
blend, the hand-back SmsYCamExit onto the rig), the rig following underneath. One camera too many, and its eye sat 200 u off the shifted
look-at (~213 u from his origin): the game's calcPosAndAt_ pushes the L eye out to 300 u.
PORT: SunshineCamera.cs -- ExecFrontRotate(yPress) (the Y variant sets FlagFrontY; the settled swing sets FlagFrontDone, already there since
pass 53), ModeChangeProc rewritten on the game's structure (the refusing states, the L camera's own exits, the normal kind's Y / L
presses, then the ladder), LCameraOn() / LCameraOff(cut, sweepAnyway, force) / DropLCamera() (a warp, the talk shot), CtrlL() (the target,
the main stick, CalcPosAndAt), _lLock (unk282) / _lModeBefore (unk60), ChangeMode(to, frames) (changeCamModeSpecifyFrame_), the BG checks' L
rules, a demo's hold cuts an open L camera; public IsLCamera / FinalYawBam / CurPitchBam / OffsetAngleYBam for Mario. MarioYCam.cs is
Mario's half only: the pad's frame meanings for the camera captured BEFORE the mode eats A / B (SmsCamMarioData.YPressed /
CamCancelPressed / MainStick -- the rig is his child and ticks after him; a released action is no longer "just pressed"), onNeutralMarioKey
on the mode's edges, A / B eaten and the buffered jump dropped in the mode, his face from FinalYawBam - OffsetAngleY, the hold, the status
gate (SmsYCamCanEnter, now with OIL_RUN: standing on goop is WAIT <-> OIL_RUN). MarioCamData.cs: Downed / InWater / Climbing / HasFludd /
NozzleTopU / MainStick / YPressed / CamCancelPressed / YCamCanEnter; YCamActive gone. Mario.cs: _ycamOn -> SmsYCamOn (the rig's IsLCamera),
SmsYCamRender / SmsYCamReset gone (the rig cuts under a demo hold and drops the L camera on a warp). SmsPhysics.cs: SmsParams.LCamera keeps
only LockTicks, the neutral key and RocketExitSweeps (the rest IS the LNormal kind record / SmsCamNormal); the selftest reads the record.
SmsCameraKinds.cs regenerated (the LNormal note). The eye in the mode is now the game's ~300 u out (was ~213): tell me if it reads far.
DRIVE 52 (patch_smoke52.py, removed before the commit): Y -> swing -> "kind Follow -> LNormal over 16 ticks" -> IsLCamera, Mario held,
grips held, eye 297 u out, fov 60; the main stick right 0.5 s -> the camera -84 deg and his face -81 deg with his speed 0; up 0.4 s -> the
pitch +60 deg and the gun angle on it (one frame behind while it moves); R -> Pumping; A -> the 16-tick sweep back, no jump, fov 50; Y 0.4
s later refused (the lock), 1.0 s later taken; B -> off, no dive. ALL OK; build 0 err; boot ALL OK, 16 kinds.
LEGACY RETIRED: the second Camera3D "SmsYCamera" (TopLevel, MakeCurrent) and MarioYCam.cs's own camera model (phases FrontRotate /
LNormal / BlendOut, its polar inbetween, FOV blend, SmsYCamExit's hand-back, SmsYCamRender's cut, SmsYCamReset) -> the rig's L mode.
DEVIATION kept: SmsParams.LCamera.RocketExitSweeps (the hover's cut sweeps). NOT PORTED: the notice lock-on, the SEs 0x4824-0x4826.

### Pass 68b -- the L camera's eye back at the record's 200 u (his Ref/yCAMrEF.png against Dolphin)
HIS REPORT: "it was better before your change" with Ref/yCAMrEF.png -- Dolphin's Y camera (left) beside the pass-68 port (right). READ: the
game's L eye sits ~2.9 m behind over his right shoulder, the nozzle top at the frame's centre (the 72-u look-at shift), Mario filling about
two thirds of the height; that is the record's 200 u off the look-at, what the pre-68 second camera did -- NOT the 300 u pass 68 produced.
WHY 68 PUSHED IT: calcPosAndAt_'s MinCushionXZ clause (cameragc.cpp 704: the eye at least mSLMinCushionXZ 300 out from his origin, under
fabricatedInline3 = "not UNDER_GROUND") reads as applying to every kind, the L camera included, and the fold-in routed the L camera through
the shared CalcPosAndAt as the game does -- the push came with it. The game as played does not show it. The function is NonMatching and
the asm of that branch has not been read: the real guard may be narrower (isNormalCameraSpecifyMode, say), or the inline's reconstruction
wrong. Until it is read, the game's own framing wins.
PORT: SunshineCamera.CalcPosAndAt applies the MinCushionXZ push only for the normal kinds (IsNormalClass), as the cushion rule already is.
Nothing else of pass 68 changes. Drive 52 again: the eye 231 u out from his origin in the mode (the nozzle top ~25 u behind him + the 72-u
shift + 200), every other step as before -- ALL OK; build 0 err; boot ALL OK.
LEGACY RETIRED: none. DEVIATION: none logged -- this matches the game as observed; the open question is the decomp's reading, noted above.

### Pass 69 -- the hip drop's camera: the landing is not the hip drop (the camera sank to the floor through the landing pose)
HIS REPORT: "when mario ground pounds the camera sinks low. the framing is not right."
THE GAME: MARIO_STATUS_HIP_DROP (0x8008A9, MarioStatus.hpp 175) is the AIRBORNE hip drop only -- hipAttacking()'s hover and fall. Touching
down is changePlayerStatus(HIP_ATTACK_END) (MarioJump.cpp 1078) -> hipAttackEnd() -> jumpEndCommon(ANIM_HIPED, SLIP_END) -> slipEnd ->
WAIT: WAITING-type statuses. The camera's ladder (CameraChange.cpp 744) gives CAMERA_MODE_HIP_ATTACK for `status == MARIO_STATUS_HIP_DROP`
and nothing else, so the moment he lands the kind is the normal one again (the current kind's Inbet 16). kindhipattack.prm (faithful in the
record): dist 800..2000, pitch -1000..6504, AtOffsetY 0 + ratio x 400 (the look-at at his FEET at ratio 0), TargetAtJumpOffsetY -100 with
AtJumpOffsetSpeed 0 -- the height-pan offset cannot move in this kind, so whatever a jump left in it (-100 after 5 ticks airborne in Follow)
stays: in the air the look-at sits 100 u under his feet with the eye 5.5 deg under THAT, 800 u out, held 144 u over the ground by the ground
check. That is the game's hip-drop camera, and it lasts exactly as long as the airborne hip drop.
THE BUG (port, pass 53): SmsCamMarioData.HipDrop counted groundPoundLanding too, so the HipAttack kind ran on through the landing pose
(ma_hiped, then ma_slped: ~0.9 s): the frozen -100 offset kept the look-at 100 u under his feet while he stood on them and the eye sank to the
ground check's 144 u -- the camera on the floor, looking at the ground under Mario, for most of a second after every ground pound.
DRIVE 53 (patch_smoke53.py: a jump, L, the camera sampled every 0.1 s; removed before the commit). BEFORE: landing at t=1.9 -> kind HipAttack
through t=2.7, look-at -100 u, eye 244 -> 144 u over his feet; Follow only at t=2.8, the eye back at 313 by t=3.5. AFTER: at the landing
"kind HipAttack -> Follow over 16 ticks", the look-at 23 -> 150 u and the eye 246 -> 309 u within the landing pose, 313 / 157 (the Follow
pose) by t=2.8. The airborne dip (the hover's eye 233 -> 72 u over his rising feet, the look-at -100) is unchanged: the game's.
PORT: MarioCamData.cs d.HipDrop = groundPoundStartup || groundPoundFalling (the landing is HIP_ATTACK_END / SLIP_END: normal kind). The L / Y
refusal in the hip drop (execFrontRotate_ 454) follows the same flag and is likewise the game's. SunshineCamera: CurrentKind / InbetweenTicks
getters (drives and Mario's half may read the kind). Build 0 err, boot ALL OK.
LEGACY RETIRED: none. DEVIATION: none. If the airborne dip itself reads wrong to him, a Dolphin reference of a ground pound decides it.

### Pass 70 -- the pause screen keeps the HUD, bright (the console's pauseIn / pauseOut)
HIS REPORT: "the pause screen should be showing the hud elements and they shouldn't be faded out" (his PauseScrenRef had not reached Ref/ when
this was built; done from the console's source).
THE GAME: the pause mask (pause_1.blo 'mask', alpha 180) darkens the stage, not the console: TGCConsole2's panes draw over it, and the
director's pause entry (MarDirectorDirect.cpp 732) runs pauseIn (GCConsole2.cpp 2578) -- startDisappearBalloon forced, startAppearMario (the
lives in), startAppearStar (the shine / coin / blue-coin counters in), startDisappearTelop, startDownLeftBot (the timer and the red coins
parked off the bottom over 20 frames, flagged unk51C / unk448); updateStarHudAutoHide (1070) does nothing under STATE_PAUSE_MENU /
STATE_CARD_SAVE, so they stay; the resume runs pauseOut (2588): the telop may return, the parked timer and red coins come back, the lives and
the star HUD go. The console keeps performing while paused: the director blacklists CUE_MOVE for the Movement list only (MarDirectorDirect.cpp
94-99); unk30 (the console) gets its cue every tick.
THE PORT BEFORE: PauseMenu.tscn's Mask on the menu's own CanvasLayer 50 lay over the HUD (CanvasLayer 10) -- the counters dimmed to 30 %; and
the HUD layer inherited the tree's pause, so nothing of pauseIn could have moved anyway.
PORT: PauseMenu.tscn -- the Mask moves to its own MaskLayer (CanvasLayer 8: over the world, under the HUD 10 and the console strings 15); the
PAUSE letters, items and stage bar stay on 50. SmsPauseMenu.Open -> SmsStandardHud.PauseIn, Close -> PauseOut. SmsStandardHud.PauseIn /
PauseOut as above (the parking reuses the camera demo's startDownLeftBot code; the auto-hide gated on GetTree().Paused); SmsTelop.Disappear
(startDisappearTelop: the bar drops now, its count restarts); SmsHud.tscn regenerated with process_mode Always on the console's layer
(gen_hud_scene.py) so its moves play while the tree is paused -- the telop's count already held under Paused, the Level's clock feeds the
timer, Mario's fields are still: nothing of the game advances. Getters Frame / StarShown / LivesShown for the drives.
DRIVE 54 (patch_smoke54.py, removed): the mask's layer 8 under the HUD's 10, the panes' 50 over it; Start -> paused, 5 letters, mask 0.71,
118 console frames in 1 s of pause, the star HUD (out before the pause) and the lives IN and staying, the telop and balloon away; B -> unpaused,
pauseOut sends the lives and the star HUD away. ALL OK; build 0 err; boot ALL OK.
LEGACY RETIRED: none. DEVIATION: none (his IdleHold still brings the star HUD back while he stands after the resume, as before). NOT PORTED:
the item sparks, the card dialog.

### Pass 70b -- the pause menu's smoke (ms_2d_pause_smo)
HIS REPORT: "smoke should appear when pause is spawning in! last touch to the pause screen for now".
THE GAME: TPauseMenu2::drawAppearPane (PauseMenu2.cpp 597): when a PAUSE letter's appear anim reaches 2.0 (mFadeAnim 2, 7, 12, 17, 22 for
the five), gpEmitterManager4D2->createEmitter(the pane's global bounds centre, 0x1F9) -- ms_2d_pause_smo.jpa (MarDirectorLoadResource.cpp 262;
his disc extract data\particle\). Dumped (jpa_dump.py) and read with noclip's JPA.ts: a Circle volume of size 25, minRad 0.25, FixedInterval
div 10, rate 1, maxFrame 1 = ONE burst of 10 puffs at 36-degree steps on radii 6.25..25 px (sizeXZ = 25 x lerp(0.25, 1, rand)); velOmni 2.0 px/frame
away from the centre x (1 +- 0.125 velRatio) x moment (1 - 0.125 rand); airResist 0.98 a frame on the base velocity; one Gravity field, mag 0.07,
dir (0,-1,0) = screen UP, FieldAccel (the field velocity grows 0.07 px/f each frame: ~25 px of rise over a full life); life 26 x (1 - 0.5 rand) =
13..26 frames; Billboard 1.0 x 1.0 = a 50 px quad, no scale anim, alpha 1.0 until 25 % of the life then to 0, rotate speed 0 (rotateSpeedRandom
is a RATIO of the speed in JPA: no spin); colour lerp(env (50,183,255) -> prm white, I) of P_ms_kemuball3_ia (IA8 64 x 64), SRCALPHA /
INVSRCALPHA. The 2D emitter manager is a TEmitterViewObj at the END of "Group 2D" (MarDirectorInitECT.cpp 234, insert = push_back): the smoke
draws OVER the menu's panes; it calcs SMSGetAnmFrameRate() = 2 x a rendered frame = 60 Hz like the Mario particles, and runs under the pause
(unk30's CUE_CALC_ANIM).
PORT: assets/Sms/Fx/tex/pause_smoke.png (jpa_bake.py job pause_smoke: the TEV colour folded in, the IA alpha); assets/Sms/Console/PauseSmoke.tscn:
a one-shot GPUParticles2D -- amount 10, explosiveness 1, lifetime 26/60 s, lifetime_randomness 0.5 (Godot's lifetime x (1 - rand x r) = JPA's),
fixed_fps 60, local_coords; ParticleProcessMaterial: Ring emission 6.25..25 px (axis z, height 0), radial_velocity 91.875..135 px/s (2 x 60 x the
ratio and moment spreads), gravity (0, -252) px/s^2 (0.07 x 3600), damping 117 px/s^2 (the linear fit of 0.98^n over 26 frames: the same 41 px of
travel and 70 px/s at the end), scale 0.78125 (50 / 64), colour ramp alpha 1 / 1 at 0.25 / 0 at 1. PauseMenu.tscn: Root/Smoke (Node2D) as Root's
LAST child + the SmokeRoot / SmokeScene exports. SmsPauseMenu: BuildTitle instances one puff a letter under Smoke (freed with the letters);
AppearWindow fires letter i's puff once when its anim crosses 2.0, at the letter's rotation centre (= J2D's bounds centre, as set the tick
before), SpeedScale = the menu's pace knob; Restart(). Getters SmokeFired / SmokeLog / SmokePuffs.
DRIVE 55 (patch_smoke55.py, removed): 5 puff nodes for the 5 letters, Smoke the last child of Root over Menu, the scene's numbers as above
(it caught emission_shape 5 = DirectedPoints; Ring is 6), Start -> paused, puffs at fade 2.6 / 7.3 / 12.2 / 17.2 / 22.1 (the tick after
2 / 7 / 12 / 17 / 22) at the letters' centres (12 px up: the appear's hop at anim ~2, as the game's getGlobalBounds), the first processing
and emitting under the pause; B -> closed. ALL OK; build 0 err; boot ALL OK. The headless dummy renderer does not simulate GPU particles:
the look is his to confirm in the editor.
LEGACY RETIRED: none. DEVIATION: none -- the ring's angles are random where the game's are fixed 36-degree steps (10 overlapping 50 px puffs:
not tellable apart), and the drag is linear where the game's is 0.98 a frame (the same travel and end speed). NOT PORTED: the selected item's
sparks (ms_2d_pause_sel, 0x1FA at bounce 3: 140 yellow-to-red DirBillboard sparks along the emitter's z).
OPEN CHECK (not this pass): the shipped Fx that spin (SlipSmoke's +-2700 deg/s) read "speed 0 +-0.125 turns/frame" -- in JPA the random is a
ratio of the speed, so a speed of 0 does not spin; re-read their ESP1 rotate blocks before the next Fx pass.

### Pass 71 -- the Gooble: TNameKuri (the Swoopin' Stu) 1:1
HIS ASK: "can we fix the gooble enemy next?" -- his Gooble.cs was a from-scratch wander / chase / charge / jump / land / die with hand-tuned
seconds and metres; Gooble.glb carries the game's name.bmd clips (name_down / hit / jump_end / jump_start / land / set / wait / walk).
THE GAME: Enemy/namekuri.cpp (TNameKuri : TWalkerEnemy : TSmallEnemy : TSpineEnemy), the nerves on Strategic/Spine.hpp (a stack:
pushNerve suspends the current, pushAfterCurrent queues, a nerve returning true pops the top), /enemy/namekuri.prm from his disc extract
(prm_dump: the shipped values differ from the code defaults -- JumpAttackSp 6 not 20, gravity 0.135 not 0.1, radius 350 not 400,
ColorChangeRate 5 not 25, LandHeight 100 not 500, march 1..1.5). THE CLOCK (settled this pass, the enemy half of the frame / tick rule):
TMarDirector::direct runs 4 ticks a 30 fps frame with NO gate on the movement list (TPerformList::load sets GATE_A|B on every CUE_MOVE
link, but testPerform only honours an object's own unkC gate bits and nothing sets them) -> every live actor's perform(CUE_MOVE) runs at
120 Hz; TLiveActor::bind moves the actor by its FULL mVelocity + mLinearVelocity each tick (liveactor.cpp 192-194) while Mario's
jumpProcess / walkProcess step pos + vel / 4 (MarioPhysics.cpp 226 / 500). So enemy .prm values are units a 120 Hz tick:
SmsUnits.TickSpeedToMps (x 120 / 74.44) and TickAccelToMps2 (x 14400 / 74.44), NOT Mario's SpeedToMps (x 30). The leap: vy 6 = 9.7 m/s,
gravity 0.135 = 26 m/s^2, 1.8 m high, 0.74 s to Mario's height; the walk 1..1.5 u = 1.6..2.4 m/s; the fall 0.15 = 29 m/s^2, terminal 40.
PORT (assets/Gooble.cs rewritten; the scene untouched): one 120 Hz tick = TNameKuri::moveObject. Generate (set anim) -> WalkerGraphWander
(walk anim, random goals -- an optional Graph node's children are the graph "main", else random points within WanderRadiusUnits 600
of the placed point: DEVIATION, his levels carry no graph; the zigzag weave needs the graph's factor and is the plain walk) -> isFindMario
(not airborne, Mario not in water / swimming, |dy| < 300, within 800 u, 360 deg, aware 10) -> NKFollowMario (march, 3 deg a tick; no
give-up) -> in sight within 350 u / 180 deg -> JumpAttackPrepare (wait anim, 1 deg a tick until within 15 deg) -> JumpAttack (jump_start;
the flash |sin(5 t)| x 255 on the body material's emission, fading 5 a tick after frame 62; at bck frame 62 SMSCalcJumpVelocityY to
Mario's point (the goal becomes that point), +2 u, airborne, 6 deg a tick; landed + clip done -> jump_end -> SmallEnemyDie, no reward).
walkToCurPathNode / calcTurnSpeedToReach / MsIsInSight / MsAngleDiff ported in degrees, units. Land nerve (a fall past 100 u while
wandering). The wall hop (TWalker's blocked flag = IsOnWall against the world, not another Gooble): vy 2.2 x scale, MarchSpeedHigh ahead,
+5 deg. Enemy-enemy contact: isCollidMove's push apart at march x 3. HITS: TObjHitCheck::checkDistance with the .prm cylinders (attack
50 x 30, damage 40 x 90, x the body scale 0.6..1.0; Mario TrampleRadius 100 x AttackHeight 50 / DamageRadius 42 x 130): the Gooble's
collision -> TNameKuri::attackToMario = his TakeDamage(DmgNamekuri) (+ a leaping one stops, no reward, dies); Mario's collision ->
TMario::hitNormal: falling in a jump with the Gooble below -> TRAMPLE (kill + his StompBounce), a ground pound -> HIP_DROP (kill), the
hose spraying -> SPRAYED_BY_WATER. WATER: ISmsSprayTarget (new, SmsWaterDrops.cs: a flying drop's ray hit on such a body sends it;
Mario.SmsHoseSpraying for the close-range hit) -> receiveMessage: 30-tick cooldown, HP 1 -> 0, behaveToWater: Die, melting (hit anim,
gravity 0.01) when on the ground, no reward. DIE: decHitPoints, the collision layer off, the death's goop stamp (unk150 & 0x20: stamp
range 15 x 32 u, SmsPollutionMap.Pollute) unless melting, down anim + sound export; done when the anim ends (or 360 ticks): genRandomItem
-> 50 % the DropScene export (his coin; none set = no drop) unless no-reward; DEAD. ALIVE TIME 2000 ticks, x 1.01 a tick from 1900 to twice
the scale, then kill. DEAD -> re-launched from the placed point after RelaunchTicks 180 (TLauncher's period; -1 frees it): resetSRTV with
velocity (0, 4, 0) that control() zeroes at once as in the game (setGenerateAnm), airborne, the Generate nerve -- DEVIATION: a placed Gooble
is its own launcher (the game pools 2 behind goop launchers). Body scale, turn 0.2, march 1..1.5 random per life; the 7 TEV register-0
colours cycle per instance on a duplicated "_mat_body" (his glb has _mat_body / _mat_eye) -- DEVIATION: scaled to full brightness, the TEV
stage's scale unread. SmsParams.NameKuri (all the .prm values, defaults noted). Mario.cs: SmsInWaterForEnemies, SmsHoseSpraying.
SmsPhysics selftests: 6 u/tick -> 9.672 m/s, 0.135 -> 26.11 m/s^2.
DRIVE 56 (patch_smoke56.py, removed): Mario set 3 m in front of gooby; gooby Wander -> Prepare (turning 280 -> 358 at 1 deg a tick) ->
JumpAttack (flash 87..180) -> the leap (vel (0.03, 6 -> -5, 2.48) u/tick, apex dy 1.74 m = the 133 u, landing 0.8 s on, at the point Mario
stood on -- another Gooble's hit had knocked him back 1.5 m, as in the game) -> jump_end -> Die (down) -> dead -> re-launched 1.5 s later
(Generate / set) -> again; Mario HP 8 -> 7 (hurt) from a leap; gooby2 sprayed directly: Die / hit (melt), gone, re-launched. Caught: the
collision exception with Mario raced his spawn (now set when he is first found); a Gooble bumping another counted as a wall (hopped).
Clips: his glb is 60 fps-timed (jump_start 1.458 s for the bck's 89 frames; SetBck scales each clip to its bck frame count).
ALL OK; build 0 err; boot ALL OK (+2 selftests).
LEGACY RETIRED: his Gooble.cs behaviour (MoveSpeed / ChaseSpeed / ChargeTime 1.09 s / JumpForwardSpeed 12 / JumpUpForce 10 / LandingTime,
the zone-driven detection and hurt / stomp) -- the scene's DetectionZone / AttackZone / Hurtbox / StompZone are no longer read: delete them
from Gooble.tscn when it is next open. DEVIATIONS: the random-point wander without a graph (+ WanderRadiusUnits), the self-relaunch
(RelaunchTicks), the colours at full brightness, the enemy push by physics contact rather than the hit cylinders. NOT PORTED: the goop
body (the screen texture) and the brain part, ms_deadnamekli_o / n and the WATHIT particles, the water bottle (bottle_short when his tank
is under half), the sounds (exports WalkSound / DownSound / MeltSound, unset = silent), the oil-slip / catch PUNCH kill, TWalker's slope
blend (unk1A8: it leaps from any floor), the coin id (genEventCoin), the pollution launchers. OPEN CHECK (not this pass): other live
actors ported with SpeedToMps (the coconut lob, pass 52-ish) may be 4x slow by the same rule -- re-read their integration.

### Pass 71b -- the Gooble's body tint off
HIS REPORT (his screenshot, 00:46): "they don't look right, they kinda just have blobs of colors" -- the 7 TEV register-0 colours I multiplied
into _mat_body's albedo (scaled to full brightness) flattened the model into coloured blobs.
THE GAME: SMS_InitPacket_TwoTevColor puts the colour in GX_TEVREG0 and the flash in GX_TEVREG1 of _mat_body; what the material's TEV
stages do with REG0 (its alpha is 169; the body also refracts the screen texture) is in name.bmd's MAT3, unread -- a plain multiply is NOT it.
PORT: TintBody is a knob, default OFF: the model keeps its own look; the body material is still duplicated per instance so the charge flash
(REG1 -> emission) plays. On, the register's raw value multiplies the albedo (no brightness scaling). NOT PORTED: the TEV stages of
_mat_body (the per-instance colour, the screen-texture goop) -- a bmd MAT3 read would settle it. Build 0 err, boot ALL OK.

### Pass 71c -- the real Gooble colouring: _mat_body's TEV from name.bmd
HIS ASK: "Read the bmd material and do the real Gooble colouring".
THE GAME (tools/projectshine/bmd_mat_dump.py, new: a J3D MAT3 / TEX1 reader after JSystem's J3DMaterialBlock + J3DMaterialInitData, run on
his extract's scene/bianco2/namekuri2/name.bmd and brain.bmd): name.bmd has _mat_body and _mat_eye; joints root / center / jnt_brain /
jnt_body / body. _mat_body: opaque (blend NONE, z EQUAL + write, zCompLoc 1), 4 texgens, 5 TEV stages, 2 colour channels. Textures:
K_new_name (8 x 8 IA4: intensity 255, ALPHA = the V coordinate 0 -> 255 by rows), P_msm_indwp2_ia (a 32 x 32 ripple -- bound to texmap 1
but used by no stage; no indirect block), H_ma_rak_dummy (the 8 x 8 placeholder TNameKuri::init swaps for the SCREEN TEXTURE), H_toon_2_4i
(a 32 x 32 toon ramp: 136 everywhere but the far corner, 153 on its edge, 238..255 past it). Texgens: TEX0 (the model UVs), TEX0 x 0.869
(unused), POS through TEXMTX2 (ViewProjmap 0x09 with a perspective effect matrix, replaced each frame by SMS_GetLightPerspectiveForEffectMtx:
the camera projection -> the fragment's own screen position), SRTG from COLOR0 (the toon lookup at u = R, v = G). Channels: COLOR0 lit
(ambient 128,128,128 + the object sun "太陽（オブジェクト）", GX_DF_CLAMP), COLOR1 lit by light 2 with ambient 0, GX_AF_SPEC / GX_DF_SIGN =
the sun's specular. Registers: PREV (0,0,0), REG0 (0,0,0) <- the per-instance colour (nameKuriTevColorData), REG1 (255,255,255) <- the flash
(unk1BC, reset to black, rising to white before the leap), REG2 computed; konst0 = (255,255,255,80). Stages (out = d + (1-c)a + cb):
  0: REG2 = SCREEN x lit0 (alpha SCREEN.a x 1)
  1: PREV = REG0 + mix(toon, lit0, 5/8) - 1/2 (bias SUBHALF): the colour, lifted by the light (+0.19 lit, +0.01 in shadow)
  2: PREV = 2 x (spec + K_new_name.A x PREV) (scale x2): the colour DOUBLED toward the top of the body (why the registers are ~100/255)
  3: PREV = REG1 + mix(REG2, PREV, 80/255): 69 % the stage behind the body, 31 % the coloured goop, plus the flash
  4: PREV = PREV + REG0 x K_new_name.A: the colour again along V; alpha = RASA0 = 1 -> opaque
=> a see-through jelly, most transparent at its bottom, tinted its register colour toward the top, a wet specular, the white flash on the
charge. _mat_eye: 2 stages (the toon-lit eye texture) -- his glb's StandardMaterial3D stays. brain.bmd (_mat_brain): 1 stage, RASC x TEXC
of K_name_dummy = the goop texture H_ma_rak the game swaps in (SMS_ChangeTextureAll) -- the dark goop core seen through the body, attached
to jnt_brain (TSharedParts(this, 2, brainModel, 3)); his Gooble.glb has no brain mesh.
PORT: assets/Sms/GoobleBody.gdshader (the five stages as written; SCREEN_UV on hint_screen_texture for the screen copy, the toon ramp by
its texel rule, K_new_name's alpha = clamp(UV.y), COLOR0 from the scene's DirectionalLight3D + the 128 ambient, COLOR1 as a Blinn-Phong lobe
(spec_power 24: TLightCommon's shininess coefficients unread) -- unshaded, depth_draw_always, cull_back) + assets/Sms/GoobleBody.tres (the
ShaderMaterial, uid cgooblebdy71, the game's constants as parameters). Gooble.cs: BodyMaterial export (default the .tres), duplicated per
instance onto the "_mat_body" surface; body_color / flash_color / sun_dir (Basis.Z, toward the light) / sun_color fed in ApplyLook; the
TintBody knob and the StandardMaterial3D tint / emission path are gone.
LEGACY RETIRED: 71b's TintBody / albedo-emission stand-in. DEVIATION: the specular lobe's shape. NOT PORTED: the brain part (export brain.bmd
with FinModelUtility and hang it on jnt_brain with the goop texture -- next), the ripple texture (bound, unused by the bmd's stages), the eye
material's TEV (his StandardMaterial3D eye stays), the 0.869 second UV set. Build 0 err; boot ALL OK; the look is his to confirm (headless
compiles no shaders). --import run for the shader's .uid (committed).

### Pass 71d -- the Gooble look complete: eyes, brain, the lights of the Light Group, the body shader fed its textures
HIS ASK: "updated goobleRef ... SuperBMD in my downloads, put it all together ... their eyes are still not working". Ref/goobleRef.png
(01:15) = his 71c build: white, near-opaque blobs, no eyes. No SuperBMD anywhere; FinModelUtility (his, Documents) had already exported
every model of the disc: cli/out/.../bianco0/namekuri2/{name,brain}/*.glb + its own TEV-as-GLSL (_mat_body.fragment.glsl: an independent
reading that agrees with mine stage for stage). Against the real game (Super Mario Wiki's in-game Gooble screenshots: a clear dome
showing the grass / stone behind it with a glossy white highlight, the colour saturated in the skirt and the tufts, a dark red-brown goop
core with cream swirls inside, a white oval eye with a dark pupil) the 71c build was wrong in four places, all found in the data:
1. THE LIGHTS: the ambient is the bmd's (128,128,128) (J3DColorBlockLightOn::load sets it from the material; SMS never overrides it),
   the sun is scene.bin's Light Group "太陽（オブジェクト）" = (255,255,255) from (200000, 500000, 200000) (tools/projectshine/scene_lights.py:
   bianco2 has 15 lights x 3 sets player / object / enemy, 6 ambients), loaded by TLightCommon::setLight as GX_LIGHT0 (no attenuation)
   AND GX_LIGHT2 = the same sun as a specular light: GXInitSpecularDir(-L), GXInitLightShininess(50) -> attn = c^2 / (25 - 24 c^2), c = N.H
   with H = normalize(L + (0,0,1)) in view space, gated N.L >= 0; J3D forces GX_DF_NONE on a spec channel, so no N.L factor (Dolphin's
   LightingShaderGen is the reference). GX lights the VERTICES: both shaders light in vertex() and interpolate (the game's soft
   highlights). 71c's Blinn-Phong pow 24 is gone.
2. THE TEXTURES: the glb carries the wrong ones -- FinModelUtility gave _mat_body the 8 x 8 H_ma_rak_dummy (the screen placeholder) and
   _mat_eye the 32 x 32 toon ramp, which is why the eyes were white smears. Shipped from the bmd via bti2png (assets/Sms/models/):
   gooble_body.png (K_new_name, IA4 8 x 8: alpha = the V ramp), gooble_eye.png (K_new_name_eye, IA4 64 x 64: I = white + dark pupil,
   A = the eye's shape), toon_2_4i.png (H_toon_2_4i I4 32 x 32), goop_mark_bianco.png (H_ma_rak.bti CMPR 128 x 128, Bianco's goop).
   Imports pinned (pin_gooble_imports.py): the ramps lossless, no mipmaps, detect_3d off (the editor would VRAM-compress them on first
   use); the goop mark VRAM + mipmaps like the game's 5 mips. The body UVs put v ~0.95 on the skirt and the tufts and ~0.45 on the
   dome: the game's gradient (checked against the glb's TEXCOORD_0).
3. THE MATHS' SPACE: GX TEV is 8-bit display-referred; Godot is linear. The shaders take the screen copy to sRGB, run the stages, and
   put the result back to linear (71c ran the stages on linear values: 69 % of a linear screen is much lighter than the game's 69 %).
   The register colours are raw vec3 uniforms (no source_color decode).
4. THE EYES: GoobleEye.gdshader / .tres on the glb's _mat_eye surface -- COLOR0 lit GX_DF_SIGN (the back darkens below the ambient),
   ALPHA0 lit (ambColor0.a = 50 + the sun's alpha), stage 0 PREV = (C0 (400,404,424)/255 + mix(toon, lit, 3/4) - 1/2) / 2 (1.0 lit, ~0.8
   in shadow), stage 1 PREV = mix(spec, EYE, PREV), alpha = 4 x EYE.A x ALPHA0, alpha compare GEQUAL 128 AND LEQUAL 255 -> discard;
   render_priority 1 so it draws after the body (the game's material order) and wins the depth tie on the dome.
THE BRAIN: brain.bmd (one joint "brain"; _mat_brain = one stage RASC x TEXC with lighting off = the texture, K_name_dummy swapped for
the stage's H_ma_rak by TNameKuriManager::initSetEnemies) is TSharedParts(this, 2, brainModel, 3): its base matrix = the body's joint 2
(jnt_brain) every frame. assets/Sms/models/gooble_brain.glb (FinModelUtility's brain.glb with the dangling brain.png reference stripped
from the JSON chunk) instanced under a BoneAttachment3D "jnt_brain" on the glb's Skeleton3D, its surface GoobleBrain.tres
(StandardMaterial3D unshaded + the goop mark). It inherits his Armature scale: a brain of ~0.8 m inside a 1.2 m dome at body scale 1.
Gooble.cs: SetupLook() (body per instance, eye shared, brain, a ShadowsOnly copy of the mesh -- the screen-reading body is in the
transparent pass and casts nothing on its own; the game draws the Gooble into the インダイレクト影 buffer), SetSun() once from the
scene's DirectionalLight3D (direction = its +Z, colour x energy; the materials' defaults are the game's sun); ApplyLook sets body_color
/ flash_color only. Exports: BodyMaterial, EyeMaterial, BrainScene, BrainMaterial.
DEVIATION: the stage's goop mark is Bianco's for every stage (the game swaps the current stage's); the scene's light direction stands in
for the game's sun direction (his DirectionalLight3D is at rotation (-60, 150, 0); the game's object sun is (-60.5, 45, 0)); the brain's
joint scale (0.9, 1, 0.965) applied twice by the game (jnt_brain's and the brain's own) is applied as the glb has it. NOT PORTED: the
ripple texture P_msm_indwp2_ia (bound, unused by any stage), the 0.869 second UV set, the pollution launchers / brain re-texturing per
stage. bmd_mat_dump.py fixed: GXCompare order (LEQUAL is 3, GEQUAL 6), tevColor[i] -> GX_TEVREG i (slot 3 unused). Build 0 err; drive 57
ALL OK (both surfaces' ShaderMaterials with their textures, the body colours cycling 6 distinct, BrainAttach on bone idx 2 with the
unlit goop-mark brain 0.32..0.49 m up inside each body, ShadowCaster shadows-only on the same mesh / skeleton) and removed; boot ALL OK.
The look is his to confirm (headless renders nothing).

### Pass 72 -- the Gooble's death particles: ms_deadnamekli_o / _n
HIS ASK: "Looks right, move on to the death particles -- they look great!!!!" (71d confirmed in-engine).
THE GAME: TNameKuri::setDeadAnm (the stomp) and setMeltAnm (the spray) both emitAndBindToMtxPtr(PARTICLE_MS_DEADNAMEKLI_O, getAnmMtx(2))
and (_N, the same) with emitter->setGlobalScale(mScaling) (namekuri.cpp 490-545): the emitters ride joint 2 = jnt_brain, whose frame is
rotated (90, 0.5, 90) -- its X is up, Y forward, Z right -- and TMarioEmitterCallBackBindToMtxPtr keeps setGlobalRTMatrix(mtx), so the
emission volumes and launch velocities are in that frame (JPAEmitter.cpp calcEmitterGlobalParams: the emitter axes = mGlobalRotation x the
emitter's own rotation). The fields are not: JPAField.cpp's status bit 0x02 is STATUS_USE_GLOBAL_COORDS (noclip's JPA.ts names it
LocalSpace -- backwards), and both files set it: gravity straight down in the world. The engine as SMS built it (libs/JSystem/src/JParticle):
life = base x (1 - rndm x rand) (shortening only = Godot's lifetime_randomness); baseVel = (velDir + velRndm + velOmni + velAxis) x
(1 + ratio x rand(-1..1)); each frame velocity = drag x (baseVel x airResist^age + the fields' summed acceleration) x moment, moment =
base x (1 - rndm x rand); Circle with FixedDensity: r = size x lerp(minRad, 1, sqrt(rand)) (uniform in area); Sphere minRad 1 = its surface;
velDir's spread: a tilt uniform in +-spread x 180 deg about Y then a random roll (JPAGetYZRotateMtx).
  ms_deadnamekli_o (jpa_dump.py): Circle r 25 minRad 0.75, rate 8 for 2 frames = 16, life 34 +-0.5, vel axis 12 (out from the axis) + dir 6
  (along it), ratio 0.4, airResist 0.98, moment 1 +-0.375, Gravity 0.6 u/f^2 FieldAccel fading in over the first half; DirBillboard (dir
  Vel) 0.8 = 40 u, ESP1 scale X 0.05 -> 1 by 74 % / Y 0.2 -> 1, 0.03 at the end, alpha enabled but constant (out timing 1.0), rotate
  speed 0 (x 1 +- 1 = none); P_msm_osenball_ia IA4 (a mottled disc, alpha 255 inside r 12 of 16), lerp(env black -> prm (111,111,111)),
  blend, alpha compare GEQUAL 190, z write.
  ms_deadnamekli_n: Sphere r 50 on its surface, rate 7 for 4 frames = 28, life 55 +-0.5, vel axis 12 + dir 12 within 36 deg of the axis
  (spread 0.2), ratio 0.3, airResist 0.98, Gravity 0.3 u/f^2, a Random field 14 u/f (FieldVelocity, re-rolled every 3 frames) enabled from
  25 % of the life and fading in to full; DirBillboard 0.7 = 35 u, scale X 0.08 -> 1 by 62.5 % / Y 0.325 -> 1, 0.03 from 79 %; P_msm_
  mizutama1_ia IA8 (a water drop: dark centre, bright rim), lerp(env -> white) with the env a 7-key rainbow table over 28 frames from a
  random phase per drop (colour calc flag bit 0), alpha compare GREATER 193, no z write.
PORT: assets/Sms/Fx/GoobleDeath.tscn (tools/projectshine/gen_fx_gooble.py -- every number derived from the fields): Goop + Drops
GPUParticles3D in LOCAL coordinates, DirectedPoints (256 baked points with the launch direction as the normal: ring r 18.75..25 u in the
XZ plane with normals (radial x 12 + Y x 6); sphere r 50 u with normals (horizontal radial x 12 + a 36-degree cone x 12)), initial_velocity
13.42 u/f x 0.375..1.4 (the ratio x the moment) / the sphere's mean 16.8 u/f x 0.7..1.3, the airResist as the linear brake covering the
same distance (9.1 / 11.5 m/s^2), gravity 15.3 m/s^2 (0.6 u/f^2 x 0.65 for the fade-in's equal fall x the mean moment) / 14.5 (0.3 u/f^2),
lifetime 34 / 55 frames with randomness 0.5, 16 / 28 one-shot at explosiveness 0.941 / 0.927 (the 2 / 4 emitting frames), fixed_fps 60,
scale CurveXYZ per axis as the ESP1 says, anim_offset 0..1 = the colour table's random phase, the Random field as turbulence (influence
0.15 following the field's 25 %-to-full fade). assets/Sms/Fx/JpaDirBillboard.gdshader (new, reusable): a GPUParticles3D quad whose Y
runs along the velocity on screen (JPADrawVisitor DirBillboard: the view-space velocity with z dropped) and X across, each side scaled by
the particle's axis scale (particle_flag_align_y puts the velocity in the transform's Y column), colour lerp(env, prm, I) with the env a
constant or a GradientTexture1D table cycling over the life from INSTANCE_CUSTOM.z (the anim_offset), display-referred maths put to
linear, the GX alpha compare as ALPHA_SCISSOR_THRESHOLD. tex/osenball_ia.png + mizutama1_ia.png from the .jpa (pin_fx_imports.py:
lossless, no detect_3d). SmsGoobleDeath.cs: Play(jointFrame, bodyScale) puts the top-level node in the brain bone's frame at the body
scale and turns each emitter's authored gravity into that frame (its own ParticleProcessMaterial copy) so it stays world-down.
Gooble.cs: DeathFxScene export (instanced per Gooble), PlayDeathFx() from SetDeadAnm and SetMeltAnm with the BrainAttach's transform and
_scale; Dead() hides the Model (not the whole node) so the splash under it lives on; Reset shows it again.
DEVIATION: the moment (0.625..1) scales the game's whole motion including the fall -- folded into the velocity range and a mean gravity;
the airResist on the base velocity only -> a linear brake on all of it; the Random field's per-particle kicks -> a turbulence field
steering the direction; the colour table phase runs on the normalised life (a short-lived drop cycles faster than the game's); the
drops write depth (one shader for both). Build 0 err; drive 58 ALL OK (spray gooby2: Model hidden, node visible; the splash at the
brain 0.00 m off, uniform scale 0.72 = the body's, the frame's X up / Y level, both gravities -> world-down 15.3 / 14.5, own materials,
one-shot local amounts 16 / 28) and removed; boot ALL OK. The look is his to confirm (headless renders nothing).

### Pass 72b -- the death splash upright
HIS REVIEW of 72: "the effects are like shooting to the side, they should be going up ... rotated 90 degrees ... seems random angle ...
it needs to go up towards the sky". That is exactly what binding to joint 2 gives: jnt_brain's matrix is rotated (90, 0.5, 90) (JNT1 and
every bck frame: its X is up, its Y forward), the JNT1 remap is identity, no joint is a billboard (JNT1 kind 2/2/1/1/0 all have mtxType
0), and JPABaseEmitter::calc runs the bind callback (setGlobalRTMatrix, the full normalised rotation) before calcEmitterGlobalParams, so
the volumes and velocities are composed in that frame -- a vertical fan along the heading, hence a different direction per Gooble. He
knows the game's splash goes up, so the port binds the splash to the body's upright frame (joint 1 'center' = the heading + the ground
tilt, the frame TNameIndParCallback uses for the Gooble's other emitter) at the brain joint's position: Gooble.DeathFxUpright (DEVIATION,
named). The gravity rotation in SmsGoobleDeath.Play stays (the ground tilt). Drive 58 re-run: the frame's Y is up, X level, the splash at
the brain 0.00 m off, both gravities world-down; removed. Build 0 err; boot ALL OK.

### Pass 72c -- the death splash's launch direction
HIS REVIEW of 72b: "the effects don't go up, seems nothing changed". Right: the frame was not the bug. Godot's DirectedPoints emission
rotates the material's `direction` into each point's normal frame WITH THE NORMAL AS Z (particle_process_material.cpp: VELOCITY =
mat3(tangent, bitangent, normal) * VELOCITY), so `direction` must be (0, 0, 1) for the launch to follow the baked normal -- pass 72 wrote
(0, 1, 0), which sends every particle along the bitangent: sideways along one axis of the node whatever the frame, the same in 72 and
72b. GoobleDeath.tscn regenerated with direction (0, 0, 1) on both emitters (gen_fx_gooble.py; the HipDropLanding sparks, the fire streaks
and the super flames already had it right). OPEN CHECK: GetSparkle's stars use directed points with (0, 1, 0) too -- their 0.4..0.8 u/f
radial drift runs tangentially; invisible under the orbit, to fix when that scene is next touched. The upright frame (72b) stays. Boot
ALL OK; the scene reloads without a build.

### Pass 73 -- the Gooble's water bottle
HIS CALL after 72c: "Goes up now, do the water bottle drop". THE GAME: TSmallEnemy::genRandomItem (smallEnemy.cpp 334) runs at the end
of TNerveSmallEnemyDie -- setAfterDeadEffect, genEventCoin, generateItem. TNameKuri OVERRIDES setAfterDeadEffect (namekuri.cpp 511; no
disappearance smoke): `if (unk198 && getCurrentWater() * 2 < getMaxWater()) gpItemManager->makeObjAppear(mPosition, 0x20000001, true)`.
unk198 is TNameKuri::isHitValid (618): 1 for any hit message except HIT_MESSAGE_SPRAYED_BY_WATER -- TSmallEnemy::receiveMessage (531)
only calls it for TRAMPLE / HIP_DROP / SUPER_HIP_DROP / UNKB, so a stomped Gooble may leave a bottle, a sprayed one or one that died on
its own leap (attackToMario: kill without it) never does; reset() zeroes it. 0x20000001 = "bottle_short" (MapObjInit.cpp 1017: a TItem,
item_hit_data_table 40 x 40, flags 0x0A040100; 0x20000002 = bottle_large / WaterRecoverObj): TNameKuriManager::loadAfter (110)
registers ONE per stage, so canAppear fails while it is out. makeObjAppear(x, y, z, id, true) (MapObjManager.cpp 213) puts it on the
ground found from y + 5 (nothing on illegal ground). THE ITEM (120 Hz movement ticks, like every map object): TItem::appear (Item.cpp
151): still on the ground (LIVE_FLAG_UNK10, no physics), HIT_FLAG_NO_COLLISION, the timer 120 (unk150); TItem::appearing (124,
MAP_OBJ_FLAG_UNK2000000) grows the scale 0.02 a tick (mAppearedScaleSpeed x 2) from 1 to 2 -- the bottle is drawn at TWICE its bmd --
then makeObjAppeared (the timer zeroed) + NO_COLLISION on + DISAPPEARING off; the next tick TItem::perform (163) drops NO_COLLISION
and, for a spawned item (not MAP_OBJ_FLAG_UNK10000000 = TItem::load's placed ones), arms DISAPPEARING with 480 ticks (unk14C);
TMapObjGeneral::perform (592) skips the draw on the odd 10-tick slots under 360 (mNormalFlushTime / mNormalFlushInterval);
TMapObjGeneral::appeared (240) makes it dead at 0. TItemManager::perform (112) spins one shared matrix 2 degrees a tick, TItem::calc
(93) copies it (MAP_OBJ_FLAG_UNK8000000). Taken: TItem::touchPlayer (67) -> taken (57) -> Mario receiveMessage HIT_MESSAGE_ATTACK
(MarioReceiveMsg.cpp 150): addWater(getMaxWater() / 2) with MARIO_FLAG_HAS_FLUDD (the large one: the tank, 159), emitGetWaterEffect
(ms_itemget1_b bound to unk160), MSD_SE_MA_GET_ITEM (98); kill -> breaking (no anim) -> makeObjDead at once. THE 50 % COIN IS NOT THE
GAME: generateItem (414) asks for actor type 0x20000008 and no TMapObjData in MapObjInit.cpp carries it (the whole 0x200000xx table:
bottles 1/2, mushrooms 5-7, egg 9, coins E/F/10, hide objs 11, shine 13, nozzles 1F/22/25/26/2A, cap 3C, box 68) -> the shipped game
drops nothing there; Gooble.DropScene is now documented as an opt-in DEVIATION, off by default.
PORT: assets/Sms/Items/WaterBottle.tscn (uid cwbottlesc073; Node3D + SmsWaterBottle.cs, Model = MeshInstance3D of
assets/Sms/models/bottle_short/bottle_short_mesh.res at 2 / 74.44 with surface overrides BottleLabel.tres (_mat_label4: StandardMaterial3D
unshaded, vertex colour x texture, cull disabled) and BottleGlass.tres (_suimen1: BottleGlass.gdshader -- blend_add, cull_back,
depth_draw_always, unshaded; TEV stage 0 TEX x COLOR0 + 1/2 / A = vcA, stage 1 + the GX_LIGHT2 specular (shininess 50, the GoobleBody
maths) / A = vcA + 1/4 - 1/2; display-referred), Hit = Area3D with the 80 x 80 unit cylinder, GetEffect = GetSparkle.tscn). The mesh:
FinModelUtility's bottle_short.glb (cli/out .../bianco2/mapobj, the real J_bottle_s 128 x 64 CMPR beside it; bottle_short.bmd read with
bmd_mat_dump.py: label 1 stage opaque no-cull unlit, glass 2 stages SRCALPHA + ONE z-write, vertex alpha 0.24 .. 1) imported headless,
the glb's .import stripped of the 11.6 MB scene defaults (bottle_imports.py), the png pinned lossless + mipmaps + no detect_3d, the
ArrayMesh pulled out by a one-off extract_bottle_mesh.gd (materials nulled; 2 surfaces in the bmd's order, colours kept). SmsWaterBottle:
Placed (editor) vs Spawn(scene, parent, groundPos) (the pool: ONE live dropped bottle, PoolFree); the tick machine above (AppearedScale
2, 0.02 a tick, LifeTicks 480, FlushTicks 360 / 10, 2 deg a tick); Take -> Mario.SmsGetWaterBottle(FullRefill) + the GetEffect bound to
him; the glass's sun from the scene's DirectionalLight3D. Gooble.cs: _hitValid (set before the two stomp kills), SetAfterDeadEffect
(the water test, PoolFree, the ground ray from 5 u up, Spawn under the Gooble's parent), BottleScene export (default the scene).
Mario.cs: SmsWaterNow / SmsWaterMax (getCurrentWater / getMaxWater = the current nozzle's AmountMax), SmsGetWaterBottle(full).
NOT PORTED: ms_itemget1_b (the water sparkle: a 330 u sphere of DirectionCross streaks converging on him, 80 frames -- GetSparkle stands
in), the appear / taken sounds, bottle_large's model (the script takes it: FullRefill). DEVIATION (small): each drop grows in from 1;
the game's pooled object keeps its scale, so its second appearance pops at 2 at once. OPEN: SmsNozzleItem runs its TItem clock at
"30 fps x Speed 3" -- the real clock is the 120 Hz tick (x4), so Speed 4 = the game; his 3 stays as his taste. Drive 59 (patch_smoke59.py,
removed): tank 1000 / 10000 -> Mario dropped 3.5 m onto gooby -> ONE bottle 0.47 s later on the floor under it (0.000 m off), under
Level, MeshInstance3D + both overrides, the 80 x 80 Area, scale 1 -> 2 then takeable with 479 of 480 ticks armed; stepping on it: water
1000 -> 6000, gone in a frame, the sparkle up, the pool free; tank 0 -> gooby2 -> the bottle flickers 90 shown / 90 hidden and times
out at 4.47 s; tank full -> gooby dies, no bottle. ALL OK. Build 0 err; boot ALL OK. The look is his to confirm (headless renders nothing).

### Pass 73b -- the bottle's size: the item's scale is never drawn
HIS REVIEW of 73: "functionally it looks great! however the bottle is too big i would say should be half the size". He is right, and
the source says why: pass 73 read TItem::appearing (Item.cpp 124: with MAP_OBJ_FLAG_UNK2000000, mScaling grows 0.02 a tick from 1 to 2)
as the DRAWN size and put the model in the scene at 2 / 74.44. But the draw never sees mScaling for this item. The only path to
model->setBaseScale(mScaling) is TMapObjBase::calcRootMatrix (MapObjBase.cpp 613); TItem::calcRootMatrix (Item.cpp 87) is
`if (!checkMapObjFlag(MAP_OBJ_FLAG_UNK8000000)) TMapObjGeneral::calcRootMatrix()`, and bottle_short_data's flag word 0x0A040100
(MapObjInit.cpp 1017, copied to the object at 11143) carries 0x08000000 -- a no-op. TItem::calc (Item.cpp 93) then writes the root
matrix itself: the item manager's spin matrix (unk40, a pure rotation) for the 3x3 and mPosition for the translation, no scale, into
getAnmMtx(0) (the flags also carry MAP_OBJ_FLAG_UNK100, so joint 0 keeps the written matrix: setMtxCalc at MapObjBase.cpp 255). J3DModel's
base scale stays its initial (1, 1, 1) (J3DModel.cpp 494), and TSmJ3DAct::perform's setBaseScale is not on TLiveActor's perform chain.
So the bottle stands at its bmd's size from the first tick -- 78 units = 1.05 m tall (the glb: y 0..78), exactly half of what 73 drew.
What mScaling 1 -> 2 DOES do: it is the 50-tick appearing state (untouchable), and at makeObjAppeared setObjHitData(0) scales the touch
cylinder by it -- item_hit_data_table's 40 x 40 becomes 80 x 80 (MapObjBase.cpp 81). Both stay as they were.
THE FIX: assets/Sms/Items/WaterBottle.tscn: Model at 1 / 74.44 (0.0134336). SmsWaterBottle.cs: AppearedScale is a const (the hit size's
multiplier), ApplyScale leaves the Model at the scene's scale; the growth still runs for the state and the hit. DEVIATION knob (off = the
game): `GrowIn` draws mScaling / 2 on the Model during the appearing state, i.e. the pass-73 pop-in from half size -- for his eye only.
No legacy retired (73's own 2x is what goes).
VERIFIED headless (bottle_scale_check.gd, a SceneTree script, removed): two dropped bottles, A = the game, B = GrowIn. A's Model scale
0.0134336 (1.048 m tall) at every sample from t 0.02 to 4.42 s; B 0.0067168 -> 0.0134336 over 0.52 s, then level. Both flicker on the odd
10-tick slots from 1.5 s and time out together at 4.47 s ("timed out"). Build 0 errors; boot [SMS selftest] ALL OK + slope/hipdrop ALL OK.
He builds the hammer (SmsWaterBottle.cs) and reloads WaterBottle.tscn.
LESSON: a decomp field that is animated is not necessarily drawn -- trace it to the matrix the renderer reads before porting it as a look.

### Pass 74 -- three small fixes: the spawn's floor normal, the roll jump's clip, the Get Sparkle's launch axis
HIS PICK from the verified backlog ("Do the three small fixes first"). Each re-checked in the code and the source before the edit.
1. THE SPAWN'S ONE-FRAME OIL_SLOPE. Every boot logged "[SMS goop] OIL_SLOPE on '' (floor normal.y 0.000 <= 0.95)" on the goop under
the spawn, then "oilRun from landing". Cause: checkGraffitoSlip (Mario.cs ~14928) read Godot's GetFloorNormal(), which is ZERO until
MoveAndSlide has registered the floor, and the first tick after a spawn is grounded only by the 4-unit ray (_smsGround4cm = IsOnFloor()
|| SmsFloorWithin(4)); a zero normal passed `y <= SlopeAngle` as a steep slope. The game reads mFloorTriangle->getNormal(), the floor under
him, always valid when grounded (MarioMove.cpp 862). FIX: SmsFloorNormal4cm() -- GetFloorNormal() when IsOnFloor(), else the 4-unit
ray's own normal, else Up. Only this call site changed; the other GetFloorNormal() readers already sit under IsOnFloor() gates.
2. THE WIRE ROLL JUMP'S CLIP. wireJumping (MarioJump.cpp 1023-1028): from WIRE_ROLLING, mStatusArg 0 (the spin with unkF6 < 0, released
through (-0x2000, RotSpeedMax - 0x2000]) plays ANIM_GIANT_ROLLING, arg 1 (unkF6 > 0, through [0x6000 - RotSpeedMax, 0x6000)) plays ANIM_ROLL
-- the two rolls turn opposite ways, each with the spin that launched it. Pass 66 played ma_roll for both with the comment "no giant
rolling clip in the library"; wrong -- his imported Mario.glb carries ma_giant_rolling (0.317 s, the same length as ma_roll; gMarioAnimeData
0xF8 and 0x6F are both row-kind 0x02). FIX: SmsWireRollJumpOff(arg) picks ma_giant_rolling first for arg 0 and ma_roll first for arg 1,
the other roll as the fallback, the jump_rolling clips after. Legacy retired: the one-clip stand-in.
3. THE GET SPARKLE'S LAUNCH AXIS. assets/Sms/Fx/GetSparkle.tscn's Stars material is EMISSION_SHAPE_DIRECTED_POINTS with direction
(0, 1, 0) -- the pass-72c bug (Godot rotates `direction` into each point's frame with the baked normal as +Z, so (0, 1, 0) sent the
1.612 m/s drift along one node axis instead of out along the normals). Hidden under the orbit motion, still wrong. FIX: (0, 0, 1) in the
scene and in gen_fx.py, the generator that writes it (tools/projectshine, not in the repo). The Stardust node is a point emitter (shape
0): `direction` is world-up there and stays.
VERIFIED: (1) boot before / after: the OIL_SLOPE line is gone, he goes "oilRun from landing" straight away; [SMS selftest] ALL OK +
slope/hipdrop ALL OK, no ERROR. (2) drive 60 (removed before the commit): the library has ma_giant_rolling and ma_roll (0.317 s each); a wire
built over him, A -> Hanging; SmsWireRollJumpOff(0) with a -2000 spin -> clip ma_giant_rolling, current animation ma_giant_rolling, tree
off, vy 51.3 m/s, the spin ended at touchdown after 108 frames with the tree back on; back under the wire, Hanging, SmsWireRollJumpOff(1)
with +2000 -> ma_roll the same way. [F60] ALL OK, 17 checks. (3) sparkle_dir_check.gd (a SceneTree script, removed): GetSparkle.tscn
loads, Stars shape 5 direction (0, 0, 1), Stardust shape 0 -- ALL OK. Build 0 errors. He builds the hammer (Mario.cs); GetSparkle.tscn
reloads from disk.

### Pass 75 -- the thrown object's clock (the coconut check)
HIS PICK: "run the coconut check". THE SOURCE: TMapObjGeneral::thrown (MapObjGeneral.cpp 109) sets mVelocity = gpMarioThrowPower x
physical unk2C (the type's rate) along his face + mNormalThrowSpeedRate x his mVel, vy = unk30, then mPosition.add(vel) once (one tick
of lead). The object then moves through TLiveActor::perform(CUE_MOVE) -> moveObject -> bind (LiveActor.cpp 182): nextPos += mVelocity
WHOLE and mVelocity.y -= gravity, every CUE_MOVE -- the Gooble's path. MarDirectorDirect.cpp 65-144: the Movement perform list runs
once per 5-unit simulation tick, 4 a 30 fps frame; the only gate (TViewObj::testPerform, JDRViewObj.cpp 5-10, via the GATE_A/B cue
bits the loader adds to every CUE_MOVE link) needs the object's OWN unkC gate flags, and no object class sets them. So a map object's
velocity is per 120 Hz tick (SmsUnits.TickSpeedToMps = x 120 / 74.44), its gravity per tick^2 (TickAccelToMps2 = x 14400 / 74.44),
its friction / stop threshold per tick. Mario alone steps mVel / 4 a tick (MarioPhysics.cpp 226 / 499), which is why HIS values read
per 30 fps frame (SpeedToMps = x 30 / 74.44).
THE BUG: pass 62 converted the throw on Mario's clock -- SpeedToMps for the launch and the stop threshold, AccelToMps2 for the gravity, a
60 Hz guess for the roll-out decay -- 4x slow, 4x weak gravity: a coconut left at 2.3 m/s and 6 m/s up and arced 1.3 m high, 1.9 m long.
The game's coconut: 5.6 u/tick = 9.03 m/s forward, 15 u/tick = 24.18 m/s up, 0.3 u/tick^2 = 58.03 m/s^2 -> 5.04 m high, 7.53 m long,
the same 0.83 s airtime (the ratio v/g is clock-free). He had found the old lob "too light and slow" and compensated with the DEVIATION
knobs: 1.8 / 1.15 / 1.6 / true in code and ThrowPowerScale 5.03, ThrowVyScale 3.645 in Player.tscn -- his eye landing near the 4x.
A SECOND, OLDER DEVIATION found by the drive: the flight's RigidBody3D kept Godot's default_linear_damp 0.1 (LinearDampMode Combine), an
air drag the game's object has not (physical unk18 = 1.0): the arc fell 7-9 % short of its own launch velocity.
THE FIX (Mario.cs SmsReleaseHeld, SmsTakeable.cs SmsThrownFlight): TickSpeedToMps for the launch (forward + vy; his mVel is mixed in RAW,
units a frame into units a tick, as the game does), vel / 120 for the one-tick lead, TickAccelToMps2 for the flight's gravity, LinearDamp
0 in Replace mode for the flight (restored after), TickSpeedToMps for BounceMinVy, the roll-out decay per 120 Hz tick (touchGround /
calcVelocity run from control() every CUE_MOVE). The knobs' defaults are the game's (1 / 1 / 1 / false). HIS Player.tscn still carries
5.03 / 3.645 (his scene, his uncommitted edits, his editor open -- not touched): until he resets them the throw is 5x the game's.
Legacy retired: the Mario-clock conversions of pass 62 and the 60 Hz decay guess of 62b.
VERIFIED (drive 61, removed): a coconut body (SmsTakeable Coconut) taken on clean floor (the spawn stands on his goop test area, where
OIL_RUN caps the run under the pitch's 16 u/f and B does not throw), his run speed set to 24 u/f for a few frames, B -> PITCHING ->
THROWN at frame 4: launch 9.03 m/s forward, 23.21 up on the first 60 Hz sample (24.18 less one step of g), g 58.03, damp 0 Replace; the
apex 4.45 m over the first sample and the return 7.07 m out, both exactly Godot's semi-implicit Euler arc for that launch (the continuous
4.64 / 7.22 less half a step) -- the game's full arc from the release point is 5.04 / 7.53. 10 checks ALL OK. Build 0 errors; boot
[SMS selftest] ALL OK + slope/hipdrop ALL OK. He builds the hammer (Mario.cs, SmsTakeable.cs) and resets the two Player.tscn knobs.

### Pass 75b -- the particle spins (the spin check)
HIS PICK: "run the spin check". THE FINDING: tools/projectshine/jpa_dump.py's ESP1 rotate line had the fields crossed -- it printed 0x5E
(rotateRandomSpeed) as the random ANGLE, 0x60 (rotateRandomAngle) as the random SPEED, and read the direction at 0x52 (it is 0x62). The
engine (JPAExtraShape.cpp 61-65; JPADraw.cpp 216-236): start angle = rotateAngle (0x5A, half-turns) x 180 deg + SF[-0.5, 0.5) x
rotateRandomAngle (0x60) x 360 deg; speed per frame = +-rotateSpeed (0x5C, half-turns) x (1 + rotateRandomSpeed (0x5E) x RF[-1, 1)) x
180 deg, + when RF < rotateDirection (0x62: 1 = always +, 0 = 50/50). rotateSpeed 0 is NO spin whatever the random says. Re-read:
  ms_wallkick_a, ms_mariwallsl1, ms_m_slipsmoke, ms_firedmg_a, ms_moe_fire_off, ms_2d_pause_smo: start +-22.5 deg, speed 0 (no spin).
      The port spun the first five at +-2700 deg/s (the 0.125 random ANGLE read as a speed in turns/frame) and started the fire / shock
      clouds at a full random angle.
  ms_wallkick_b (the kick marks, a DirectionCross): rotate DISABLED. The port spun them -20045..23155 deg/s.
  ms_dmg_c (bonk / damage stars): start +-180 deg, 0.02 x (1 +- 0.25) half-turns/frame = 162..270 deg/s, sign 50/50. The port: +-90 deg,
      +-864 deg/s (0.02 read as turns, 1.0 read as its random).
  ms_m_sphipd_hit_d (super hip drop stars): +-180 deg, 135..297 deg/s, sign 50/50. The port: +-135 deg, +-864.
  Correct already: ms_m_biri_b arcs (+-180 random, no spin), ms_m_biri_c (no ESP1), ms_m_blur2sp (its -3234.6 is texIncRot, a different
  field), the hip drop / super hip drop clouds and shockwaves (fixed angles, no spin), ms_itemget1_a (angle 45 deg fixed).
THE FIX (assets/Sms/Fx: WallKickDust, WallSlideDust, SlipSmoke, FireDamage, ShockPuff, BonkStars, SuperHipDropLanding + the three generators
gen_fx_dust / gen_fx_fire / gen_fx_super in tools/projectshine): the five smokes angle_min / max +-22.5, no angular velocity; the kick marks no
angular velocity (their +-180 start angle KEPT as the stand-in for the cross's orientation along a ~0 velocity -- not a rotation the game
draws; approximation); the stars angle +-180 and angular velocity +-270 / +-297 (Godot's angular_velocity is ONE uniform range: the
game's two-sided 162..270 / 135..297 magnitude band cannot be expressed, so slow stars occur that the game has not -- approximation,
named in the generators). PauseSmoke.tscn's +-22.5 deg start on a symmetric puff is invisible and the file carries his uncommitted
edits: left alone. NOT ported, found here: ms_m_biri_a's shock streaks twist about their own axis at 540..1620 deg/s
(JPADrawExecRotDirectional applies the rot-Y matrix in the quad's own frame = a width flicker); a shader job for the ShockSparks streaks.
FireDamage.tscn / ShockPuff.tscn were patched from HIS files (they carry the pass-55 FIRE_SMOKE_DARKEN / SHOCK_PUFF_SCALE tweaks my
scenes/Fx mirror had missed -- the md5 check caught it; the mirror is current now). Legacy retired: the misread spins of passes 33 / 51 /
60 and the dumper's wrong labels (jpa_dump.py fixed and commented with the engine lines).
VERIFIED: fx_spin_check.gd (a SceneTree script, removed): the seven scenes load, 8 emitters at the wanted angle / spin ranges -- ALL OK.
Scenes hot-reload; no build needed.

### Pass 76 -- the water-get sparkle (ms_itemget1_b)
HIS PICK: "bake the bottle's real water sparkle". TMario::emitGetWaterEffect: emitAndBindToPosPtr(MS_ITEMGET1_B, &unk160), bound to his
centre joint, when a water bottle refills the tank (MarioReceiveMsg.cpp 150 / 159). The disc's ms_itemget1_b.jpa, read with the fixed
dumper and the JPA engine source (libs/JSystem/src/JParticle):
  BEM1: a sphere SHELL of r 330 units around the joint (minRad 1.0), FollowEmitter; 1.2 streaks a frame for 80 frames (96 over 1.33 s),
        life 25; vel omni -12 = 12 units a frame INWARD -- 300 units in the life, from r 330 to 30: water streaking into Mario.
  BSP1: DirectionCross (two quads crossed about the velocity), X 0.1848 x Y 1.3354 -> 9.2 wide x 66.8 units LONG along the flight
        (JPABaseShape.cpp 102-103: 0x18 is mBaseSizeY -- THE DUMPER PRINTED Y x X AS "X x Y"; fixed); P_msm_glow3_i; additive (SRCALPHA /
        ONE), z test, no z write; colour lerp(env -> prm, TEXC), alpha TEXA x prmA; prm (135,255,255,155) -> (183,255,255,179), env
        (0,183,255) -> (0,127,135) over a 10-frame table sampled at a random phase per streak (calc Random).
  ESP1: alpha 0 -> 1 by 37.5 %, -> 0 from 87.5 %. Scale in by 75 %: X from 1.0, Y from 0.0 -- THE FIXED-POINT SCALE VALUES ARE x10 IN
        THE ENGINE (JPAExtraShape.cpp 32-39: mScaleInValueX/Y, unk44/unk4C = fx x 10.0); the dumper printed them raw (0.1 / 0.0), so
        the streak keeps its width and stretches from nothing to 67 units along its flight. No scale-out, no rotation.
  SSP1: one child per streak at its death (timing 1.0, rate 1): Billboard, life 16, no velocity, moment -0.3285 -> 0.3285 u/f^2 UP
        (createChildParticle: unk68 = (0,-1,0), baseVel += moment x unk68 a frame), scale2D X 1.5 x Y 2.0 = 75 wide x 100 tall
        (JPASweepShape.cpp 24-25: 0x4C is Y; the dumper had these swapped too), shrinking linearly to 0 (JPADrawCalcChildScaleOut), alpha
        1 (no alphaOut), prm white, env (0,0,255): lerp(blue -> white, I) of P_msm_glow2_i.
THE SCENE, assets/Sms/Fx/GetWater.tscn (gen_fx_water.py, Godot-native): Streaks = GPUParticles3D, local_coords, amount 30 (72 a second over
the 0.4167 s life; the script stops emitting after 80 frames), EMISSION_SHAPE_SPHERE_SURFACE r 4.433 m, radial_velocity -9.672 m/s,
particle_flag_align_y, two PlaneMesh draw passes 0.124 x 0.897 m (FACE_Z + FACE_X = the cross, Y the long axis on both) on
GetWaterStreak.gdshader (the TEV lerp; COLOR.r = the random phase from color_initial_ramp, COLOR.a = the ESP1 alpha from color_ramp;
GX colours raw, the result to linear), scale_curve Y 0 -> 1 by 75 %; sub_emitter_mode AT_END x1 -> Puffs: a billboard QuadMesh 1.007 x
1.343 m, StandardMaterial3D additive on tex/glow2_water.png (lerp(blue -> white, I) baked, alpha I), gravity (0, +15.886, 0), scale 1 -> 0,
lifetime 0.2667. SmsGetWater.cs : SmsGetSparkle (the same bind / emit-N-frames / free lifecycle; EmitFrames 80 in the scene). Textures
tex/glow3_i.png (the intensity, read as r) and tex/glow2_water.png, imports pinned lossless (water_imports.py). WaterBottle.tscn's GetEffect
-> GetWater.tscn (the ms_itemget1_a stand-in retired for the bottle; GetSparkle stays for the nozzle items). Sound slot for him.
APPROXIMATIONS: GX adds in display space, Godot in linear (the glow sums differ); the Direction quad's roll about the velocity axis
(JPA: from the emitter's Y) is Godot's own frame.
TWO SYSTEMIC FINDINGS for the backlog (the dumper is fixed; the shipped scenes are not re-audited here): (a) every ESP1 scale-in / out
value the earlier passes baked is 10x too small (the wall-kick marks' 0.09 -> 0.125 / 0.07 -> 0.5 are really 0.9 -> 1.25 / 0.7 -> 5.0; the
bonk and super-hip-drop stars' "0.03 -> 1 -> 0.14" slivers are really 0.3 -> 1 -> 1.4; the Gooble drops, the hip drop, the fire goop
scales likewise); (b) every asymmetric Direction / DirectionCross quad baked from the dumper (ms_m_biri_a 4.0 x 1.0, ms_m_sphipd_a /
hit_a / hit_c, the hip drop streaks) may have X and Y swapped. Both need a scene-by-scene re-read.
VERIFIED: getwater_check.gd (a SceneTree script, removed): the scene loads with every number above (18 checks ALL OK), both bars' AABB
long axis is Y, the bottle's GetEffect resolves to GetWater.tscn; boot [SMS selftest] ALL OK + slope/hipdrop ALL OK; build 0 errors. The
look is his to judge (headless cannot draw GPU particles). He builds the hammer (SmsGetWater.cs, SmsWaterBottle.cs); the scenes reload.

### Pass 76b -- the wall kick's emitters were points, not rings
FOUND while checking the Fx scenes' enums for pass 76: WallKickDust.tscn's two emitters carried emission_shape = 4 with ring parameters.
4 is EMISSION_SHAPE_POINTS (RING is 6; checked against the engine's constants headless); with no point texture Godot emits every
particle at the node's origin, so since pass 60 the kick's puff (ms_wallkick_a: a 15-unit circle) and its eight marks (ms_wallkick_b: a
20-unit circle) have all come out of one point. FIX: emission_shape = 6 in the scene and in gen_fx_dust.py (both emitters). The other
ring emitters (the super hip drop's smoke and stars) already had 6. VERIFIED headless (ring_check.gd, removed): both emitters RING with
their radii 0.201 / 0.269 m. The scene hot-reloads.

### Pass 77 -- the water bottle's shaders compiled in play; the Gooble scene carries its resources; the particle textures pinned
HIS REVIEW of 76: the water-get sparkle "looked great" but "lagged the game for a second when I picked it up". The cause is in the
game's own shader cache: user://shader_cache gained exactly three entries at 23:47:33 during his session -- the two ParticlesShaderRD
programs of GetWater.tscn's process materials and the SceneForwardClustered program of GetWaterStreak.gdshader. A shader's FIRST load
runs every variant through glslang on the main thread (around a second for a new spatial shader); the cache makes every later load
instant. The three compiled mid-play because of WHEN they loaded: Gooble.SetAfterDeadEffect resolved BottleScene with GD.Load at the
first stomp (the scene was never set in assets/Gooble.tscn), and WaterBottle.tscn carries GetWater.tscn (its GetEffect) and so the
shaders. Everything else of the kind already loads with the level (Player.tscn instances its effects, Gooble._Ready loads the death
splash, the pollution layer builds its pools at _Ready) -- the bottle was the one lazy load left.
FIX (1:1 in spirit: TNameKuriManager::loadAfter registers the stage's bottle at load): Gooble._Ready resolves BottleScene
(`BottleScene ??= GD.Load(...)`, a warning if it fails) and setAfterDeadEffect only spawns it; assets/Gooble.tscn now sets BottleScene,
DeathFxScene, BodyMaterial, EyeMaterial, BrainScene and BrainMaterial to their default resources (the code paths stay as fallbacks),
so the scene brings every resource -- and every shader -- in with the level. The four inert Area3D zones (DetectionZone, AttackZone,
Hurtbox, StompZone, unread since pass 71) and their shapes are gone from the scene. His cache already holds the three programs, so
the hitch would not have come back on this machine either way; it would have on any other machine, and for every new effect loaded
the same lazy way. LEGACY RETIRED: the zone nodes and the at-death GD.Load.
ALSO (his editor closed, at his word): the two throw overrides left in assets/Player.tscn from the pass-62 lob (ThrowPowerScale 5.03,
ThrowVyScale 3.645 -- his tuning around the 4x-short throw pass 75 fixed) are removed from the working tree, nothing else of his in
that file touched (uncommitted, as the file was). AND the particle textures: the editor's detect_3d had re-imported 18 of
assets/Sms/Fx/tex's textures VRAM-compressed (S3TC) in the working tree, uncommitted -- the game's particle textures are I8 / IA8
intensity maps, and block compression bands a 32 x 32 glow -- so all 30 .import files there are pinned compress/mode 0 (lossless),
mipmaps on, detect_3d off (fx_tex_imports77.py, then `--import`). The goop albedo tiles and the model textures are left as they are
(CMPR on the disc; compression is their native state).
VERIFIED: gooble_preload_check.gd (a SceneTree script, removed) instanced Level.tscn headless: WaterBottle.tscn, GetWater.tscn,
GetWaterStreak.gdshader and GoobleDeath.tscn were not cached before the level and all four were two frames after it; the six
Goobles carry BottleScene / DeathFxScene / BodyMaterial and no zone node. Build 0 errors; boot [SMS selftest] ALL OK + slope/hipdrop
ALL OK, no errors; the re-import wrote lossless .ctex files (glow3_hit: vram_texture false, compress/mode 0). He builds the hammer
(Gooble.cs); Gooble.tscn reloads.

### Pass 78 -- the particle scale audit: every baked effect re-read against the engine's own loader
THE DUMPER HAD TWO MORE BUGS (jpa_dump.py, fixed): (1) the ESP1 scale flags byte (data[0x4E] = JPAExtraShape::unk7E) was read one bit high
-- JPAExtraShape.hpp 18-30 has 0x01 isEnableScale, 0x02 isDiffXY, 0x04 anmY, 0x08 anmX, 0x10 bySpeedY, 0x20 bySpeedX -- so the enable bit
was never printed, "scale True/False" was really diffXY, and the anm (cycle) flags sat one place off; (2) with pass 76's x10 on the in /
out values. Re-dumped all 31 .jpa the Fx scenes were baked from (tools/projectshine/audit_dump.py). THE ENGINE'S CURVE (JPADrawVisitor.cpp
1478-1617): t < in -> v_in + (1 - v_in) t / in; t > out -> 1 + (v_out - 1)(t - out) / (1 - out); else 1 -- an in of 0 has NO in phase (the
start is 1, whatever the file's in value), an out of 1 no out phase; with an anm flag the timer is not the life but (age % cycle) / cycle,
run backwards on the odd cycles when the anm type is not 0 (a ping-pong); and JPADraw.cpp 242 multiplies EVERY particle's scale by
(1 + RF[-1,1) x rndm) whether or not a curve is enabled.
WHAT THE SCENES HAD vs THE GAME (all enabled; the random factor was missing almost everywhere): wall kick puff -- no curve (should grow
0.75 -> 1 -> 1.7, x 1 +- 0.375); wall kick marks -- 0.09 -> 0.125 / 0.07 -> 0.5 (really 1 -> 1.25 across, 1 -> 5.0 along: drawn out
five times; quad 1.0 x 0.9, was 0.9 x 1.0); wall slide dust -- none (0.7 -> 1 -> 1.6, +-0.5); slide smoke -- none (0.75 -> 1 -> 2.2,
+-0.35); bonk / damage stars -- 0.03 -> 1 -> 0.14 over the life (really X 0.3 -> 1 -> 1.4 and Y 1.4 -> 1 -> 0.3 ping-ponged every 12
frames over the 75: the star squashes and stretches, +-0.25); fire cloud -- none (1 -> 2.7, +-0.4); shock puff -- none (1 -> 2.2, +-0.5,
his x 1.25 kept); shock streaks / arcs -- no random (+-0.5); spin jump rings -- no random (+-1.0 / +-0.875: rings from nothing to double);
get sparkle stars -- none (1.5 -> 1 by 6 %, -> 0.1 at the end, +-0.5; quad 2.8 x 2.5, was 2.5 x 2.8); hip drop sparks -- 0.1 -> 1 -> 0.1
on both axes (really X flat, Y 0.75 -> 1 -> 0.5); hip drop smoke -- 0.085 -> 1 -> 0.2 (really 0.85 -> 1 -> 2.0 / 2.2: it GROWS);
hip drop shockwave -- none (1 -> 5.0: 250 -> 1250 units); super flames -- 0.4 -> 1 -> 0.1 (really X 4.0 -> 1 then flat, Y flat then
-> 1.5); super sparkles -- none (1 to 50 %, -> 0.1, +-0.5); super shockwave -- none (1 -> 8.0); super smoke -- 0.085 -> 1 -> 0.2 (really
1 to 32.7 % then -> 2.0 / 2.2); super flames at the landing -- 0.1 -> 1 -> 0.1 (X flat, Y 0.75 -> 1 -> 0.5); super stars -- no random
(+-0.375); Gooble goop -- 0.05 / 0.2 -> 1 -> 0.03 (really 0.5 / 2.0 -> 1 -> 0.3, +-0.4); Gooble drops -- 0.08 / 0.325 -> 1 -> 0.03
(really 0.8 / 3.25 -> 1 -> 0.3, +-0.6); the swim bubbles (SmsBubbles.cs) -- 0.06 / 0.14 and 0.07 / 0.13 (really 0.6 / 1.4 and 0.7 /
1.3, BOTH axes ping-ponged on the 8-frame cycle -- the code looped Y only, as a sawtooth -- and the +-0.4 on the whole scale, not
the out value), the pop ring -- no quad scale (0.3 -> 1 -> 1.8, +-0.3, on top of the texture matrix's shrink); the pause smoke -- none
(0.75 -> 1 -> 1.4, +-0.4). Already right: every base quad but the two swapped ones, the GetWater streaks (pass 76).
HOW: gen_fx.py gained esp1_scale_at / esp1_scale / esp1_scale_anm (the engine's formulas; the anm loop baked over the life -- Godot
samples a scale curve by life fraction, so a shorter random life compresses the loop: the knowing approximation) and curve() now writes a
Curve's max_value when a point exceeds 1 (a Godot Curve's range defaults to 0..1); every generator takes its numbers from them
(gen_fx_super.esp_scale, gen_fx_gooble.xyz_scale rewritten on top); the 13 scenes regenerated (the pre-patch generators reproduced the
shipped scenes byte for byte first -- they are the source of truth); PauseSmoke.tscn and SmsBubbles.cs edited by hand. LEGACY RETIRED:
the hand-rolled curve point lists in the generators.
VERIFIED: fx_scale_check.gd (removed) loaded all 14 scenes and read the curves / ranges / quads back from the resources -- 86 checks ALL
OK, the values above 1 intact (5.0, 8.0; the max_value matters); build 0 errors; boot [SMS selftest] ALL OK + slope/hipdrop ALL OK. The
look is his (headless draws no particle): effects he approved will change toward the game -- the stars twinkle in shape, the smokes
grow, the shockwaves spread, the kick marks streak. He builds the hammer (SmsBubbles.cs); the scenes reload.

### Pass 79 -- the shock streak twists about its own axis
ms_m_biri_a's ESP1 has rotation on (the briefing's "shader job"): angle 0 +- 0.5 turn at birth, speed 0.1 half-turn a frame x (1 +- 0.5) =
540..1620 deg/s, sign 50/50, rot type Y. WHAT THAT MEANS for a Direction quad (JPADrawExecRotDirectional, JPADrawVisitor.cpp 674-752): the
quad is no billboard -- its Y is the direction, its X the perpendicular closest to the previous X (seeded by the emitter's up), and the
rotation matrix is applied to the quad's offsets about its own Y BEFORE the frame: the streak twists about the line it lies on, showing
edge-on and flat in turn. The port drew it as a fixed-Y StandardMaterial3D billboard (the camera-facing baseline, no twist). GODOT 4.4
CANNOT do this with flags: particle_flag_rotate_y REPLACES the particle's transform with a world-Y spin after the align (particle_process_
material.cpp), so the twist lives in a spatial shader: ShockStreak.gdshader reads INSTANCE_CUSTOM.x (the process material's angle_min /
max +-180 and angular_velocity 540..1620 deg/s, accumulated by Godot) and rotates the quad's vertices about local Y; colour / alpha / blend
as the StandardMaterial3D had them (COLOR x the texture, additive, unshaded). The random birth angle makes the baseline frame's seed
invisible; the random sign is invisible on a symmetric quad (|cos| is even), so one positive range stands in for the two-sided one.
gen_fx_fire.py writes the shader with the scenes. LEGACY RETIRED: the fixed-Y billboard stand-in for the Direction type.
VERIFIED: twist_check.gd (removed) -- the loaded scene's streak material is the ShaderMaterial on ShockStreak.gdshader with its texture, the
process material carries angle +-180 / angular velocity 540..1620 with align_y on and rotate_y off; boot ALL OK with no shader error
(headless cannot compile GLSL: the render_mode list is GetWaterStreak's, which compiled on his machine in pass 76). The scene hot-reloads.

### Pass 80 -- the pause menu's selection sparks (ms_2d_pause_sel)
THE GAME (GC2D/PauseMenu2.cpp 441-457): on every pulse of the selected item, when its bounce clock equals mEffectKeyFrame (3 -- the clock
steps 0.5 a frame, so 6 frames into each 71-frame pulse) TPauseMenu2 creates emitter 0x1FA (ms_2d_pause_sel) at the item's global bounds
centre and setEmitterScale(4 x getWidth() / mEffectStretch (129), 1, 1); a cursor move forceDeleteEmitters the last one (402-408, 431-434).
THE EFFECT (jpa_dump.py): a 2D emitter whose Circle (r 15, the rim, FixedInterval: 14 evenly spaced for one frame = one burst of 14) lies
in XZ -- on the screen (XY) a horizontal line, denser at the ends; vel axis 10 u/f along the point's own XZ direction (JPAEmitter.cpp 554:
the emitter scale touches the position, not the velocity), so each spark flies sideways at 10 x cos(angle) px/f -- the end ones fast, the
middle ones barely -- under airResist 0.85/f, 25 frames; DirBillboard 0.2 x 0.5 (10 px across, 25 along the velocity) of P_msm_shikaku_i
(I4 16 x 16), TEXC x prm additive, prm keys (255,255,72) at frame 5 -> (255,39,0) at 30, calc 3 = Merge (JPADrawVisitor.cpp 1673: the
31-frame table stretched over the particle's life: yellow to 16 %, red by 97 %); alpha 1 to 50 % -> 0; scale X 1, Y 1 -> 0.2 (the streak
shortens to 5 px); no rotation.
GODOT: assets/Sms/Console/PauseSparks.tscn (gen_fx_pause.py): GPUParticles2D, one shot, explosiveness 1, 14 x 25/60 s, fixed 60 fps, with
particle_flag_disable_z + DIRECTED POINTS on the rim (64 points (x, 0, z), 'normals' (cos, 0, sin)): in 2D the normal's XY rotates the
(speed, 0) velocity, so the non-unit (cos, 0) gives speed x cos and the Z drops -- the game's projection exactly; emission_shape_scale.x
= 4 x width / 129 set per burst; the exponential drag as a linear brake over the same 65.5 px; align_y for the quad along the velocity;
the 16 px texture scaled (10/16, 25/16 -> 5/16) by a CurveXYZTexture; the ramp as above; a CanvasItemMaterial additive. The texture:
jpa_bake.py's new 'pause_spark' job (flat: white, the I4 intensity as alpha), pinned lossless. SmsPauseMenu.cs: SparksScene export (set in
PauseMenu.tscn), one GPUParticles2D under SmokeRoot (over the panes, as gpEmitterManager4D2 draws); SparksAt(item) at the tick the bounce
clock has just reached 3 (the game's == 3 at the top of the tick), at the item's global-transform centre with the pane's bounds width (as
reformed last tick, getWidth()); SparksOff() (hidden, a one-shot keeps its particles) on a cursor move, on Open and on Close. LEGACY
RETIRED: the "not ported" note. The ShockStreak.gdshader.uid the import wrote for pass 79 rides along.
VERIFIED: pause_sparks_check.gd (removed; Level.tscn under a --script tree with GameData added by hand): the first burst 6 frames into the
first pulse (bounce 3.3) at item 0's centre to 0.0 px with the shape scale 4 x 188 / 129; a second burst one pulse later; a stick-down move
hides it at once and item 1's burst follows 7 frames later at its centre (width 120 -> scale 3.72). Build 0 errors; boot ALL OK. The look
is his (headless draws no particle). He builds the hammer (SmsPauseMenu.cs); PauseMenu.tscn reloads.

### Pass 81 -- the ride's one-step hover (pass 63's residue)
MEASURED (patch_smoke81.py, removed: the pass-63 lift, an AnimatableBody3D moved by code before the carry, Mario on it; down 3 m/s, up 3
m/s, +X 1.5 m/s with a 90 deg/s turn, the gap between his feet and the deck's drawn top sampled every 0.1 s before the lift's move):
BEFORE -- 5 cm over a deck going down and 5 cm into one going up while they moved, 5 cm left in his local point (0.245 instead of 0.195)
after the descent stopped; the turn exact. WHY: at his tick the collision world holds a kinematic deck TWO steps behind its node (a
transform set in a script is flushed to the server after that frame's step and moved in the next; readable two ticks later), so the
carry's one-step extrapolation of the server matrix still placed him a step behind the drawn deck, MoveAndSlide then settled him against
the lagging collision (pushed up on a descent, snapped down on a climb), and checkRideReCalc measured that settled position against the
carry's matrix -- the step baked into unk2F4 for good.
FIX (Mario.cs SmsRideApply / SmsRideReCalc): the carry puts him on the deck the COLLISION world knows (the server's matrix, no
extrapolation), so his own move runs consistent with the floor he touches; the step from that deck to the DRAWN one (node x local -
server x local, _smsRideShift) is added after MoveAndSlide when he still rides the same actor, and his point is re-measured against the
drawn matrix; the mount still measures against the collision's (the deck he landed on); the facing turn and the knob's leave velocity read
the drawn matrix. SmsRideExtrapolate and its teleport guard are gone (a teleport is just a bigger shift). A lift whose script runs before
his (tree order, or a lower process priority) is exact; one that runs after is one step behind at render, as the engine's own carry is.
The game's matrix is its own collision, so this is checkRideMovement's result in Godot's terms. LEGACY RETIRED: the extrapolated carry.
VERIFIED: the same drive after the fix -- all three legs mean gap -0.49 cm (the resting contact, the same at a standstill), worst -0.49,
floor True throughout, the local point 0.195 steady, no hover left after either stop; build 0 errors; boot ALL OK. He builds the hammer.

### Pass 82 -- the NPCs: Piantas (M / W) and Toads, TBaseNPC 1:1
SOURCE: NPC/NpcBase.cpp, NpcNerve.cpp, NpcChange.cpp, NpcWalkTurn.cpp, NpcAnm.cpp, NpcCallback.cpp (the neck), NpcInitData.cpp (hit cylinders,
colour tables), NpcInitAnmData.cpp (kind -> bck index = the archive's alphabetical order), NpcSave.hpp; System/MarDirectorEvent.cpp
(findNearestTalkNPC / movement_game: B talks), Player/MarioWait.cpp + MarioAutodemo.cpp (startTalking -> READ_BILLBOARD / readBillboard);
his disc: params/npc/npcnormal.prm, npcmontem.prm, npcmontew.prm, npckinopio.prm (npc_prm_dump.py); the bmds' TEV (bmd_mat_dump.py):
mom_model / mow_model / kinopio_body.
WHAT: assets/Sms/Npc/ -- SmsNpcProfile.cs (Resource: every npc*.prm field, the per-type switches, the clip per anim kind, the hit
cylinders, the GX S10 colour tables; Profiles/PiantaMale.tres, PiantaFemale.tres, Toad.tres), SmsNpc.cs (CharacterBody3D: the nerve
stack GraphWander / UTurn / GraphWait / WaitMarioApproach / TurnToMario / Talk / Wet / Mad; execWalk's march + turn per 120 Hz tick;
isInBodyTurnSearchRange -> a Toad turns its body to Mario (isTurnToMarioWhenApproach: 0x4000016..18 only), a Pianta turns back to its
placed yaw (isNeedTurnToFirstState); changeNerveProc_'s talk accept (TalkAcceptDist 400 / Height 500 / Degree 360, Mario facing it within
150 deg, MARIO_STATUS_WAIT, not holding, not the L camera, the nearest talkable) -> the cursor; B -> Talk: the NPC turns to him
(isTurnToMarioWhenTalk) on the talk clip, Mario READ_BILLBOARD: set 100 u off, turns at mReadRotSp 1024 BAM/tick, facing -> the window;
npcTalkOut: talk forbidden 60, walk 120; behaveToHitObject_(WATER) -> Wet (the wet clip, 50 % the alternate, a Pianta's cut not blended)
-> a mad Pianta (isMadNpc) in MadSearchRange -> Mad (step to face him at 20 deg/tick, the mad clip); behaveToBeTrampled_ + TNpcTrample
(scale.y x (1 - amp sin), amp 0.75 -> 0 over 40 ticks, then 60 ticks to Mad) on Mario landing on its damage cylinder (Mario StompBounce);
the look: SMS_InitChangeNpcColor on _hand_mat (Pianta, TEVREG0, 10 M / 6 W colours) and _mat_cap + _mat_cloth (Toad, REG1/REG2, 5 colours),
NpcSkin.gdshader = the three TEV layouts in display space with the game's per-vertex GX lighting (the Pianta's skin is the REGISTER,
no texture -- why his StandardMaterial import read grey/yellow), NpcEye.gdshader; ColorIndex -1 = random), SmsNpcNeck.cs
(SkeletonModifier3D = NPCNeckCallBack: the head follows Mario + 85 u within NeckTurnSearchDist, yaw +-NeckMaxAngleY chased at
NeckAngleYSpeed a frame, pitch eased by the yaw ratio; axes read off the kubi bone's rest pose), SmsTalkWindow.cs + TalkWindow.tscn
(the bubble: paged Lines, typed at 45 cps, A pages / closes, a tail toward the speaker; restyle the StyleBoxFlat), SmsTalkCursor.cs +
TalkCursor.tscn (the white cone over the one talkable NPC, bob + spin); Pianta.tscn / PiantaFemale.tscn / Toad.tscn (the glb at
1/74.44 facing -Z, a capsule from the game's damage cylinder, Profile assigned, Lines / Idle / Wander / Graph / ColorIndex exports);
models/NPC/Toad (kinopio_body.glb + 4 textures) and models/NPC/PiantaFemale (mow_model.glb + 3) from his FinModelUtility export,
imports stripped of the 11.6 MB scene defaults and the textures pinned lossless (npc_imports.py). Mario.cs: SmsCanStartTalk /
SmsTalkStart / SmsTalkEnd / SmsTalkTick / SmsFacingDir, the talk freeze before the state machine (goto EndFrame). SmsPhysics.cs:
SmsParams.Npc (npcnormal.prm).
CUSTOMIZE: duplicate a Profiles/*.tres and change numbers / clip names / colours; per instance: Lines (the pages), Idle (Wait / WaitA /
WaitB / Talking / Angry / Dance / Sit / SitTalk = the placement action flags), ColorIndex, Wander + Graph (a Node3D of Marker3Ds) or
WanderRadiusUnits, CanTalk, the four sound slots.
DEVIATIONS: none in the values. NOT PORTED (logged): being carried / thrown (LIVE_FLAG_UNK100000 Piantas -- the take system is
RigidBody-only), the pollution (dirty / sink / wash), TNpcThrow (the Pianta that throws Mario), the balloon messages, the talk camera,
TNpcParts (hats / glasses / moustaches / the W's flower + arrows and their colour tables), the Pianta's real leaf texture
(i_mom_mino_rgba.bti; the glb carries the 8x8 dummy), sounds (slots exist), the BMG text.
CLOCK: NPCs are TLiveActors -- every value per 120 Hz tick (MaxMarchSpeed 1.2 u/tick = 1.93 m/s). The thrown-NPC arc (3 / 2.5 / 0.3)
is unported, so the one place the clock could bite is not yet live.
LEGACY TO RETIRE: assets/PiantaMaleNormal.tscn (his script-less placeholder, instanced in Level.tscn) -> swap the Level's instance
for assets/Sms/Npc/Pianta.tscn when his editor is closed, then delete it.
DRIVE 82 (patch_smoke82.py, removed; 31 checks, all pass): spawned a Pianta, a Toad and a wandering female on the nearest goop-free
spot (OIL_RUN is not WAIT): profiles / colours / 2 + 5 shader surfaces / nose at -Z; the Toad turned its body to Mario at 3 m and its
neck tracked him, the Pianta kept its placed facing; Mario at 2 m facing it: talkable, B -> Talk on both sides, Mario facing the speaker
in 0.00 deg at 149 u, the Pianta turned 60 deg to him, the window open on page 0 with the talk clip, pages typed and turned by A,
closed by the last A, the talk ended on both sides -- the Pianta resumes in TurnToMario, not WaitMarioApproach, because
changeNerveFromTalk_ resumes the interrupted nerve and TNerveNPCWaitMarioApproach re-pushes TurnToMario while he stands inside the
250 u body-turn search (a Pianta there only turns back to its placed yaw) -- then talk forbidden 60 ticks and talkable again; a spray
-> Wet (mom_wet) -> Mad -> back to waiting; Mario dropped onto the Toad: squashed to scale.y 0.44 and no Mad (isMadNpc false); the
female walked 14.2 m in 19 s on mow_walk at the 1.2 u/tick cap. Build 0 errors, boot ALL OK.

### Pass 82b -- the Level's Pianta placeholder retired
LEGACY RETIRED: assets/PiantaMaleNormal.tscn (the script-less baked import: StandardMaterials, a 0.03 uniform scale = a 7.4 m Pianta,
no behaviour) -- deleted; its instance in assets/Level.tscn is now assets/Sms/Npc/Pianta.tscn (node "Pianta", identity basis: the NPC
scene carries the 1/74.44 model scale itself). Only this swap and the deletion are in the commit; his other uncommitted Level edits stay his.
PLACEMENT: his placeholder stood at local y -9.27239, which is 19.2 m of open air above the plaza's floor at that x/z (headless down-ray
from 50 m up: the Plaza collision at global y -28.4572; nothing else within 150 m below; outside the Sea area). A physics Pianta fell 19 m
at every boot, so the Level node is at local y -28.4903 (= the floor; the Level root carries an origin of (3.09137, 0.032806, -2.65286)),
same x/z. If the spot was meant differently, drag the node in the editor -- the old y is the one above.
CHECK (npc_level_check.gd, removed): the main scene headless, the Pianta the only SmsNpc, no PiantaMaleNormal node, standing on the Plaza
with 0.000 m of drift over 240 ticks, profile on, model scale 0.013434, nerve WaitMarioApproach on mom_wait; boot ALL OK.

### Pass 83 -- liftable / throwable NPCs (TBaseNPC as a TTakeActor) + the NPC clock at 120 Hz
SOURCE: NPC/NpcChange.cpp (behaveToBeTaken_ 240, releaseTaken_ 216, isNowCanTaken 433, isNerveCanGoToTaken 127), NpcBase.cpp (receiveMessage
447: HIT_MESSAGE_TAKE needs LIVE_FLAG_UNK100000 and no holder; perform 555-580: mPosition = mHolder->getTakingMtx() translation, mRotation.y =
his yaw - mAngleYDiffWhenTaken; moveObject 492: ensureTakeSituation -> releaseTaken_ when the holder no longer holds us; 689-713: the once-a-
frame block = DIRECTOR_FLAG_LAST_SIMULATION_TICK (MarDirectorDirect.cpp 119-124) decrements unk1DC), NpcNerve.cpp (TNerveNPCWaitContinue 93:
holder -> npcTakenIn), NpcAnm.cpp 534 (TAKEN clip = NpcInitAnmData kind 9: mom_wait for the M), NpcCollision.cpp bind 113-150 (gravity a tick,
the ground zeroes mVelocity), NpcInbetween.cpp (execPosInbetween: 6 frames), Player/MarioCheckCol.cpp 81-96 (hitNpc: the flag -> wantToTakeActor
-> canTake = B && isTakeSituation), System/MarDirectorEvent.cpp 44 (findNearestTalkNPC SKIPS the flag), System/EventWatcher.cpp 320
(evSetFlagNPCCanTaken: the stage script sets the flag per NPC -- it is not a type default). Strategic/TakeActor.hpp.
WHAT: SmsNpc.cs -- `CanBeTaken` export (off = the game's default; on = B lifts, never talks: no cursor), IsNowCanTaken (the flag, no holder,
the hit filter on, not talking, the wander / wait / approach / turn / wet nerves), SmsTaken (behaveToBeTaken_: the yaw difference kept, a
6-frame position blend into his hands, HIT_FILTER_NO_COLLISION = the capsule leaves its layer + his body excepted, the Taken nerve ->
ClipTaken), TickHeld (perform with mHolder: origin = his taking matrix, yaw = his yaw - the difference), ReleaseTaken (releaseTaken_: velocity
ThrowSpeedXZ 3 / ThrowSpeedY 2.5 u/tick along HIS facing -- the same for a throw and a put-down, TBaseNPC ignores THROWN and PUT -- 15 frames
of no collision, the nerve set to WaitMarioApproach whose time 0 turns a Pianta back to its placed yaw in the air), Move: the thrown velocity
rides whole until the ground zeroes it. SmsNpcHoldProxy.cs (new) -- the frozen, shapeless RigidBody3D his RigidBody-typed hold code carries in
place of the CharacterBody3D NPC (an SmsTakeable child with the profile's damage cylinder: Kind Object -> pitched past fwd 16, else put if
canPut, else kept), freed the tick he stops pending / holding it. Mario.cs -- SmsFindTakeTarget offers NPCs with the flag on the fruit's
isTakeSituation geometry (his DamageRadius 42 + the NPC's 100 in XZ, HoldRadius 80 + 100 in 3D, the 60-degree cone), Anim_AttachPickup
sends the take at raise frame 11, SmsReleaseHeld hands an NPC proxy to its own releaseTaken_, SmsHoldsOrPends. SmsPhysics.cs --
SmsParams.Npc.ReleaseNoCollisionFrames 15.
THE CLOCK FIX: pass 82 ran ONE NPC tick per 60 Hz physics frame while every npc*.prm value is per 120 Hz tick (TLiveActor) -- turn speeds,
the march's anim rate, the talk / walk forbids, the trample and wander timers all ran at half speed (the march in m/s was right). The NPC now
sub-steps like Mario and the Gooble (SmsUnits.TicksFor: two ticks a frame); the once-a-frame game block (the pos blend, unk1DC) steps on
TicksPerFrame. Drive 82 re-run under it: 31 / 31 (the forbid check moved to its real 30-frame window).
CUSTOMIZE: tick `CanBeTaken` on the Piantas you want liftable (per instance; the game decides per NPC from the stage script). A liftable NPC
cannot be talked to -- that is the game's rule (findNearestTalkNPC), not a limitation.
DEVIATIONS: none in the values. NOT PORTED (logged): mInbetweenCtrl's motion blend on the TAKEN clip, Peach's carried balloon lines, the
thrown-NPC wall slide (isTouchedOneWallAndMoveXZ 150 u -- MoveAndSlide's own wall response stands in), sounds.
LEGACY RETIRED: the "(unported)" tag on SmsParams.Npc.ThrowSpeedXZ / Y; the pass-82 one-tick-a-frame NPC clock.
DRIVE 83 (patch_smoke83.py, removed; 33 / 33): a liftable Pianta 2 m ahead is never talkable; B at 2.0 m (dxz past 142 u) does not take; at
1.8 m facing it the raise starts with the proxy pending; at raise frame 11.5 it is held (Taken nerve, mom_wait, layer 0, not takeable
again); the hold is the upper-body state over his WAIT; after the 6-frame blend its origin is on his taking matrix (0.2 cm) 1.16 m over
his feet; it faced him (-180 deg) and kept that difference through a 168-degree turn; B standing = PUTTING (ma_put): released at frame 20
with exactly 4.84 / 4.03 m/s (3 / 2.5 u/tick) along his facing, landed 1.05 m ahead 14 frames later on the floor, TurnToMario (he stands
inside its 250 u) turning back to its placed yaw; the capsule's layer off and not takeable for the 30 frames, back and takeable after; taken
again, run to fwd 32, B = PITCHING (ma_throw): the SAME 4.84 / 4.03 release (TBaseNPC ignores HIT_MESSAGE_THROWN), landed 2.02 m ahead.
Build 0 errors, boot ALL OK.

### Pass 84 -- NPC parts: hats, glasses, moustache, lapel, tie, luggage, the W's flower and arrows, the Toad's sunglasses (TNpcParts)
SOURCE: NPC/NpcParts.cpp (TNpcParts: for each of the 12 slots whose bit the placement sets, a TSharedParts on the joint TNpcModelData
names, the model authored in that joint's space; the part colour = the placement's second colour triple indexed by the slot's channel
(unk28: 0 hats, 1 moustache / belts / lapel / flower centre, 2 glasses), applied through SMS_InitChangeNpcColor), NpcColor.cpp (kind 0 =
GX_COLOR0 the lit channel's material colour, 1 = TEVREG0, 2 = TEVREG1 + REG2 from the two buffers), NpcInitData.cpp (the slot tables
sMonteM / sMonteW / sKinopio_InitData, every joint, model file, colour-change kind and colour buffer -- parsed by gen_npc_parts.py, not
hand-copied), NpcInitPrg.cpp 365-440 (setIndividualDifference_: the two colour triples and the parts mask from the placement). The 18
bmds' TEV read with bmd_mat_dump.py (montemcommon, montewcommon, kinopio): every part is two stages over the body's GX lighting.
WHAT: assets/Sms/Npc/Parts/{Pianta,PiantaFemale,Toad}/*.tscn (18 scenes, GENERATED by tools gen_npc_parts.py: an SmsNpcPart root with
Joint / ColorChannel / up to three SmsNpcPartMaterial resources, the glb instance; each material a baked ShaderMaterial carrying its TEV
form, default registers and texture, plus the game's colour tables); SmsNpcPart.cs (hang + colour: SMS_InitChangeNpcColor over the
surfaces by material name), SmsNpcPartMaterial.cs (the kind + tables, S10 / 255 kept above 1); NpcPartCommon.gdshaderinc + NpcPart /
NpcPartCutout / NpcPartBlend.gdshader (the two-stage evaluator: stage 0 = s0_scale x (d + mix(toon, lit, k) + s0_bias) with d = 0 /
TEVREG0 / a konst; stage 1 = the two-register mask mix(c1, c2, tex), the belt passthrough, prev x tex, prev + spec x (1 - prev), or the
luggage's mix(prev, spec, 1/2) + tex - 1/2; alpha: opaque, cutout at 128 (hatB / hatD crowns, the petals), or SRCALPHA with no depth
write (the glasses, the sunglasses) -- three shader files because a render mode cannot switch on a uniform); models/NPC/Parts/<model>/
(the 18 glbs + their textures from his FinModelUtility export, imports stripped of the 11.6 MB scene defaults and pinned lossless:
copy_parts84.py). SmsNpc.cs: `PartColorIndices` (Vector3I, the placement's three part colour indices; -1 = drawn once per channel
inside the tables), SetupParts (every SmsNpcPart under the NPC -> a BoneAttachment3D on its joint under the skeleton, identity transform;
"__ROOT_JOINT__" or a missing bone -> the model root; coloured with the body's toon ramp and sun). Pianta / PiantaFemale / Toad.tscn:
an empty `Parts` Node3D as the drop target.
CUSTOMIZE: instance any assets/Sms/Npc/Parts/<Type>/*.tscn under an NPC (its Parts node); set PartColorIndices or leave -1. The slots:
Pianta HatA / HatB / HatD / HatE / HatF / HatG / MustacheA / GlassesA / GlassesB / LapelA / TieB / Luggage; PiantaFemale Flower / HatA
/ GlassesB / ArrowR / ArrowL; Toad Sunglasses. New colours = edit a part scene's Table0 / Table1.
DEVIATIONS: none in the values. NOT PORTED (logged): the LOD-1 part models (the game's second model set), the pollution colour on parts
(kind + GX_KCOLOR0 for a dirty NPC -- the port has no NPC pollution), Peach's / the Mare's special parts, a parts mask on the Level's
placement (parts are scene children here -- the godot-positive form of the mask).
LEGACY RETIRED: none (new feature). Imported-material fallback: a surface whose name no SmsNpcPartMaterial carries keeps its import.
CHECK (npc_parts_check.gd, removed; 26 / 26): a Pianta with HatA + MustacheA + GlassesB at indices 3 / 1 / 2, a female with Flower +
HatA + GlassesB + ArrowR at random indices, a Toad with the sunglasses: every part on a BoneAttachment3D of its joint (kubi / yashi_jnt /
handR_jnt) under the NPC's skeleton at identity, 2.36 m over the Pianta's feet, 0.53 m over the Toad's; the crown's c1 / c2 = the hat
tables' row 3, the belt's c0 = its row 3, the glasses' mat_color = row 2, the moustache's c0 = row 1 with the spec form, the petals'
mat_color = the FlowerOut row of the drawn index inside 3, the arrow on the raw lit colour, the sunglasses on the blend shader with
their texture; the female's random indices inside 4 / 3 / 3; shaders compile (0 errors); boot ALL OK, build 0 errors.

### Pass 85 -- the talk is the director's, the NPC is no floor (trample / keepDistance as TMario::hitNpc), the random outfit (DEVIATION)
SOURCE: System/MarDirectorEvent.cpp 34-104 (findNearestTalkNPC: MARIO_STATUS_WAIT, the nearest NPC with LIVE_FLAG_UNK20000 and without
UNK100000; findNearestTakeNPC first; movement_game: not holding, not the L camera -> TTalkCursor::associateNPC, PAD_FLAG_TALK_NPC,
unk128 bit 1 this frame / bit 2 "last frame too", MEANING_TALK_B just pressed -> unk126 = 1), System/MarioGamePad.cpp 100 (with
PAD_FLAG_TALK_NPC on, B's meaning is TALK_B, else MEANING_B: his own B never sees a press by a talkable NPC), MarDirectorDirect.cpp
973-1043 (the talk state machine; PAD_FLAG_TALK_SELECT during the talk; mEnabledFrameMeaning = mMeaning & ~prevMeaning: the A that
closed the window is no new press), NPC/NpcChange.cpp 447-540 (changeNerveProc_: the talkable flag, UNK40000 from the director ->
pushNerve(Talk)), Player/MarioCheckCol.cpp 15-110 (hitNormal / hitNpc: a JUMPING-type status, not HIP_DROP, mVel.y < 0, the NPC's
position below his, isBeTrampledNpc -> trampleExec, else keepDistance(actor, 0) and wantToTakeActor for a liftable one), MarioCollision.cpp
86-175 (isTakeSituation: 3D distance <= mAttackRadius + the object's damage radius inside the 60-degree cone; trampleExec: not DIVE,
HIT_MESSAGE_TRAMPLE -> MARIO_STATUS_TRAMPLE, ANIM_STEP1/2/3, FUMI_B / C at 0.5 for an NPC), MarioPhysics.cpp 27-94 (keepDistance: inside
radius + unk15C he is set out to that distance along the line from the actor, at most 50 units a tick, after a wall check; mForwardVel or
the XZ speed above mClashSpeed 40 -> PARTICLE_MS_DMG_C + changePlayerDropping(JUMP_SHORT_BACK_DOWN)), MarioMove.cpp 1615-1626 (unk15C = 50,
80 on Yoshi), MarioInit.cpp 426 / MarioMove.cpp 225 / MarioJump.cpp 65 (his attack cylinder: HoldRadius 80 x AttackHeight 50 standing,
TrampleRadius 100 x 50 falling, PushupRadius 60 x PushupHeight 180 rising; damage 42 x 130), Strategic/ObjHitCheck.cpp 10-67 (the pair
test: attacker's attack cylinder vs sufferer's damage cylinder; the sufferer lands in the ATTACKER's list -- so his checkCollision sees
an NPC through HIS attack area), NPC/NpcCollision.cpp 9-114 (initNpcObjCollision_: the NPC's cylinders from the init data; execNpcObjCollision_:
a WALKING NPC moves itself out of what it overlaps by the overlap depth, a standing one does not; setVariableDamageRadius_: a trampled NPC's
damage radius is mSLDamageRadiusSmall while Mario is airborne above it within 3 R; bind: mGroundHeight + 1 -- the NPC stands one unit over
its ground), NpcBase.cpp 285-303 (isBeTrampledNpc: Piantas, Nokis, the Toads), the shipped npcmontem / montew / kinopio.prm
(mSLDamageRadiusSmall 10) and mario.prm (mClashSpeed 40, the radii). NpcParts.cpp 47-60 (TNpcParts: slot i of twelve for bit i of the
placement word -- the parts are the stage data's, never random).
WHY: his report after passes 82-84: "talking to them is not working" and "when I jump directly on them I don't jump off them, Mario stands
on their head", plus the ask for a random-outfit toggle. (1) Pass 82 read B in the NPC's own tick with Mario's SmsCanStartTalk (WAIT): the
Level's own Pianta sits before Mario in the tree and talked, but his Pianta2 / PiantaFemale (and any NPC spawned at runtime) come after
him -- their tick saw his B only after waiting() had turned it into TAKE_POSE (stateOfMario pickupFail), so the talk never started. Drive 85
reproduces a human press (the SceneTree's PhysicsFrame signal, before every node) and the fix: the DIRECTOR runs at the top of HIS tick,
before any actor, finds the cursor's NPC and starts the talk; B is TALK_B while the flag is on, so his own B sites (the held-B take, the
standing B) are off -- order in the tree no longer matters. (2) The NPC's capsule was a Godot floor: he landed on it before his feet ever
entered the damage cylinder, and the trample test (pass 82: his feet above HALF the NPC's height) never ran. The game has no NPC floor at
all: THitActor cylinders, trampleExec on a jumping status falling with his feet above the NPC's, keepDistance for every other overlap.
(3) The parts are placement data in the game; a random roll is his convenience.
WHAT: Mario.cs -- SmsDirectorTalkCheck (movement_game at the top of the SMS block, before the READ_BILLBOARD gate: findNearestTakeNPC first,
SmsNpc.FindNearestTalk, SmsTalkTargetNpc = the cursor's NPC, _smsTalkPadFlag / Prev = PAD_FLAG_TALK_NPC this frame / as the pad read it, a
B just pressed with the flag already on -> npc.StartTalk()), SmsBIsTalkB gating the held-B take and the standing B (TAKE_POSE), SmsHitNpcTrample
(hitNpc + trampleExec from the NPC's hit check: TryGetAirProfile / FALL / the knock-downs / the airborne bonk = FLAG_JUMPING, not the hip drop,
velocity.Y < 0, feet above the NPC's + TrampleFeetMargin, not a dive -> StompBounce), SmsKeepDistance (keepDistance(actor, 0): radius +
KeepDistanceRadius 50, at most KeepDistanceStepMax 50 a tick through MoveAndCollide; above ClashSpeed the bonk entry with dropObject),
SmsTouchGround4cm, _smsTalkSwallowA (PAD_FLAG_TALK_SELECT: the A that closed the window sets no jump buffer until released -- drive 85 saw a
hop after every talk without it), SmsFindTakeNpc shared by the take and the director, and the take's XZ pre-filter corrected to his STANDING
attack radius 80 + the object's (the Mario-as-attacker pair; pass 83 had 42 + R, which keepDistance's 150 u sits outside -- a liftable Pianta
could no longer be taken). SmsPhysics.cs -- SmsParams.Mario PushupRadius 60 / PushupHeight 180 / KeepDistanceRadius 50 / KeepDistanceStepMax 50,
SmsParams.Npc DamageRadiusSmall 10 / TrampleFeetMargin 5. SmsNpc.cs -- ExceptMario (his body excepted from the capsule for good; SetCollisionOff
now only drops the layer), HitCheckMario as the TObjHitCheck pair (his attack cylinder by state against our damage cylinder, DamageRadiusNow =
setVariableDamageRadius_) -> SmsHitNpcTrample or SmsKeepDistance, no hit pairs while he reads a billboard (FLAG_UNK1000), a walking NPC
stepping out of its overlap with him (execNpcObjCollision_), TalkCheck sets the flag only, FindNearestTalk public, the cursor = the director's
association (SmsTalkTargetNpc: no cursor while he walks, as findNearestTalkNPC wants WAIT), RandomOutfit / RandomOutfitDensity exports +
SetupRandomOutfit before SetupParts. SmsNpcOutfit.cs (new) -- the groups (Pianta: one of six hats 60 %, the moustache 35 %, one of two glasses
25 %, lapel or tie 30 %, the luggage 10 %; W: flower 50 %, hat 30 %, glasses 20 %, both arrows 10 %; Toad: sunglasses 30 %), hand-placed
parts keep their group out of the roll, density scales the odds.
DEVIATIONS: RandomOutfit (off = the game); TrampleFeetMargin 5 u stands in for the game's +1 unit NPC ground lift (Godot's Mario rests ~1.4 u
over a Pianta on one floor, so "his feet above the NPC's" needs the margin to exclude a same-floor WAIT); keepDistance does not check the
ground at the new spot (the game skips the push when it is not legal). NOT ported: trampleExec's FUMI_B / C at half scale (no stomp dust is
baked), MSD_SE_MA_KICK_ENEMY, keepDistance's checkWallPlane (MoveAndCollide stops at the walls instead).
LEGACY RETIRED: the NPC's own B read (pass 82's TalkCheck), its stomp latch, the capsule as a floor for Mario.
CHECK: drive 85 (removed; 32/32): A -- an NPC after him in the tree: cursor on it, a human B (pad before every node) starts the talk, no TAKE_POSE,
the window pages, the forbid, talkable again, no jump after the closing A; B -- the Level's own Pianta (before him) the same; C -- a 40-frame run,
WAIT again, cursor on the Pianta he stopped before, the talk; D -- a drop onto a Pianta: MARIO_STATUS_TRAMPLE, the squash (0.44), never a floor
contact above its feet; E -- standing 1.0 m from the axis -> 2.015 m (150 u); H -- 22 frames beside it: no trample; F -- a fall 1.6 m off the
axis: no trample (110 u reach), lands, pushed to 150 u; G -- 14 random outfits: hung, coloured, never two hats. Drives 82 (31/31) and 83
(33/33, its reach expectation corrected to 180 u) re-run green. Build 0 errors, boot ALL OK.

### Pass 86 -- the talk balloon (TTalk2D2), the talk camera (TALK_A / TALK_B), the talk cursor model (TTalkCursor): the game's, 1:1
SOURCE: GC2D/Talk2D2.cpp (the constructor 56-99: unk220 26, unk338 0.04, unk33C 1.0, cColorTable; loadAfter 220-281: the curve's length in
100 f32 steps -> unk94, 90 one-glyph 20 x 20 J2DTextBox panes with setBlackWhite(col & ~0xFF, col), the root parked (393, 115) -18; setMessageID
282-392: unk280 / unk26C / unk27C / unk340 0x40 reset per message, the first page laid out; openTalkWindow 432-483: makeMtxForTalk, the root
at (388, 115) -18 for TALK_A else (160, 135) 20 for TALK_B, startDisappearTelop / Balloon / Mario, talkModeIn, state 2; makeBoxLine 485-550:
w += unk94 x (0.7 width + 4), pos = R(ang) p, box = (round x, -0x25 - round y), mRotation = ang, the glyph laid past 1.1 is the line's last;
openNormalWindow 575-643: unk234[i] -= 0.04 to -0.109, held at 1 while the line above's last glyph is not visible, the band sound and the
first glyph shown at < 1.0, the fade loop (0x40 a tick; A held past unk2DE 2 -> 0x80 and unk2DC 0), unk3C 'bac' shown when every band has
landed; moveTalkWindow 704-790: one fade step a line, the last line's mark group fades 0x10, cc / cs by unk26A, the pulse 255 -> 60 by 4 /
back by 2; checkControler 792-844: MEANING_SELECT_A | B -> state 7 or closeTalkWindow; closeNormalWindow / eraseNormalWindow 846-912: the
root 0x10 a tick (the erase again once a frame), the next page's setupTextBox; perform 945-1062: CUE_MOVE = the states, CUE_CALC_ANIM = the
20-frame pause (unk251 0x14) and the erase's extra step, CUE_DRAW = openWindow's ramp quads in state 4; setupTextBox 1126-1221: 0x0A breaks
the line, 0x00 ends the message (unk26A), the colour per glyph (mCharColor / mGradColor), unk281[unk2DE] = unk280 per glyph; setTagParam
1223-1437: the wait tag -> unk280 (kept), the colour tag -> cColorTable[n]; openWindow 1439-1512: the band quad under the root's -rotation
about (x1 + 5, y1 + 5), TEXMAP0 = message_back_1 translated by unk234 with GX_CLAMP, C0 0000ff00 / C1 0000ffa0, x TEXMAP1 = the 'bac' picture's
alpha), game_6/scrn/message_2.blo (me_0 10 x 10 base 4; me_1..3 at y -33 / 0 / 33; bacN 292 x 58 at (-141, -25) alpha 160 ROT 1 black 0000ff00
white 0000ffff; f_1 (-128, 4) / f_2 (-5, -16) / f_3 (118, 4) 20 x 20; cuN at (159, 24): cr 26 x 26 at (-8, -8) alpha 160 blue, cc 18 x 18 at
(-2, -3) ROT 70, cs 16 x 16 at (-3, -3)), nintendo/font/standard_fontex.bfn (JUTResFont: 20 x 20 cells on one 512 x 256 I4 sheet, WID1 kern /
advance per code, INF1 ascent 20), JSystem J2DPane::makeMatrix (T(pos + pivot) R(-mRotation) T(-pivot)), System/MarDirectorDirect.cpp 973-1043
(unk124 1 -> 2: openTalkWindow; 2 -> 0 when unk248 == 0: PAD_FLAG_TALK_SELECT off), Player/MarioAutodemo.cpp 43-91 (readBillboard: facing ->
unk126 = 2; unk124 0 -> WAIT), Camera/CameraTalk.cpp (makeMtxForTalk: killHeightPan_, unk2C = mYaw, mYaw = MarioAngleY - 0x8000, mSavedModeBeforeTalk,
changeCamMode_(TALK_A, or TALK_B for isSmallNpc); makeMtxForPrevTalk: the yaw back, changeCamMode_(saved), onNeutralMarioKey, npcTalkOut on every
NPC of unk88; ctrlTalkCamera_: the target = the look point, calcPosAndAt_), Camera.hpp 115 (isThing: a talk kind and no inbetween), CameraMode.cpp
114 / 140 (isTalkCameraSpecifyMode; the talk kinds are not normal class), CameraBGCheck.cpp 118 (no wall check in a talk kind or its inbetween),
cameragc.cpp 891 (under a talk kind the mode ladder never runs; the talk over -> mInitialMode), /Camera/kindtalka.prm (dist 230, XAngle 0,
AtOffsetY 141.8, OffsetAngleX -769, OffsetAngleY -8157, OffsetLookatXZ -67.6, chase 0.1 / 0.08) and kindtalkb.prm (270, XAngle 2500, 115.7,
-1188, -6153, -60), NpcBase.cpp 250 (isSmallNpc: the Toads, children, Nokis) / 418 (getCursorPos: y + BodyHeight x scale + CursorHeight),
NpcAnm.cpp 509 (npcTalkOut: mTalkForbidCount 60 for every NPC; the talking one's walk forbid 120 and changeNerveFromTalk_), System/TalkCursor.cpp
(/common/cursor_b/default.bmd with icon_rot.bck and icon_flash.brk, the base matrix at getCursorPos, GX_ALWAYS on draw), the bmd (joint 0 kind 2
Y-billboard; joint 1 at y 20.04 -> shape 1 / _mat7: the chevron 54 x 25 x 17.5 units, texgen NRM through TEXMTX0 (0.919, (0.62, 0.37)) over
H_soap_ref_ia8, colour 4 x (TEXA + C0 (210, 40, 40) x TEXC); joint 2 at y 75 -> shape 0 / _mat6: the 80.27-unit icon quad, matrix type 1
billboard, lerp(C0 (240, 68, 68), C1 (246, 234, 234), icon I) + K0 x icon_b, alpha icon A + icon_b I), icon_rot.bck (100 frames, loop: joint 1
rot y 0 -> 720, trans y 20.04 <-> 8.76 every 20 frames), icon_flash.brk (_mat6 K0 (200, 44, 44) -> (255, 255, 255) at 49 -> back at 100), MActor
(frameUpdate once a CUE_CALC_ANIM at rate 1.0: TTalkCursor never sets SMSGetAnmFrameRate, so one bck frame a 30 fps frame), GCConsole2.cpp
startDisappearTelop / startDisappearMario.
WHY: his review of pass 85: "talking works but looks nothing like it does in Mario Sunshine. complete redo ... follow the decomp ... should be a blue
text with 3 lines where the text spawns in and should kinda roll out the text". Pass 82's SmsTalkWindow was a cream StyleBox at the bottom of the
screen with Godot labels, no camera, and a cone for a cursor -- none of it the game's.
WHAT: assets/Sms/Talk/ (new) -- SmsTalkBalloon.cs (TTalk2D2's normal window: the states 2 -> 3 -> 4 -> 5 -> 7 / 6 on the 120 Hz tick with the
once-a-frame half, openNormalWindow / moveTalkWindow / checkControler / close / erase as the source has them, makeBoxLine's f32 arithmetic, the
pages from the NPC's Lines, the tags <c0>..<c5> and <w=N> as the .bmg's, the root parked by the camera's mode and placed as J2DPane::makeMatrix
with the (5, 5) pivot, the glyphs drawn as the font's 20 x 20 cells coloured with the sheet's intensity as alpha, the telop / help balloon / lives
away at the open, Mario freed when the fade is done), TalkBalloon.tscn (message_2.blo as Controls: Root / Line1..3 / Band + Mark { Base cr, Arrow
cc, Dot cs } / Glyphs, the two sound slots), TalkBand.gdshader (openWindow's ramp wipe: the band shape x the ramp at (s + roll) clamped, blue,
alpha 160; roll <= -1 IS the 'bac' pane), SmsTalkFont.cs + TalkFont.tres (the bfn's cells, kern and advance per ASCII code, from gen_talk_assets.py),
tex/ (the sheet, the band, the ramp, the two marks: intensity as alpha on white, pinned lossless). assets/Sms/Npc/ -- TalkCursor.tscn (generated by
gen_talk_cursor.py: Spin -> Chevron (TalkCursorArrow.obj from the bmd, TalkCursorArrow.gdshader), Icon (a 1.078 m QuadMesh, TalkCursorIcon.gdshader),
Anim: icon_rot + the brk's K0 as one 100-frame looping Animation at 30 fps), SmsTalkCursor.cs (the Y-billboard root), tex/talk_cursor_*.png (pinned
lossless, mipmapped). SmsNpc.cs -- MarioFacesYou opens the balloon (Ensure'd at _Ready so it loads with the level), TalkWindowClosing = npcTalkOut
(every NPC: NpcTalkOutAll from the balloon's close), EndTalk = forceCloseTalk when the balloon is up, IsSmallNpc (Kind == Toad), the window exports
gone. SunshineCamera.cs -- the kinds TalkA / TalkB (gen_camera_kinds.py: kindtalka / kindtalkb, LookDown's id corrected to 0x33, IsTalkKind, the
talk kinds out of IsNormalClass), MakeMtxForTalk(small, marioAngleY) / MakeMtxForPrevTalk / CtrlTalk / IsTalkCamera / IsTalkCameraSmall /
TalkCameraSettled, no ladder and no wall check under a talk kind, the talk over without PrevTalk -> the stage's kind. MarioCamData.cs -- Talking,
SmsCamFaceAngleBam (his face fresh at the open). Mario.cs -- SmsCamera, the HUD's WAIT off during READ_BILLBOARD, the cursor over a liftable NPC
in his take situation too (findNearestTakeNPC -> associateNPC). SmsStandardHud.cs -- TalkIn (startDisappearMario).
DEVIATIONS: AutoWrap (on; off = the game's hand-broken lines): a line the curve cannot hold breaks by words where makeBoxLine would truncate it
and a fourth line spills into a new page; the balloon's Size knob (1 = the game's; the console's own 0.7 is separate); the telop comes back on
the port's own clock (startAppearTelop(false) is a permission). NOT ported: the choice window (me_4), the board window, the Sunflowers' TALK_C / D
and the elder's TALK_E, the sounds (MSD_SE_SY_TALK_OBI / SELECT_COMMON slots exist; talkModeIn / Out's BGM duck has no BGM), onNeutralMarioKey
(his stick is not latched across the talk).
LEGACY RETIRED: assets/Sms/Npc/TalkWindow.tscn + SmsTalkWindow.cs (pass 82's cream window), the cone cursor (pass 82's TalkCursor.tscn body),
SmsNpc.TalkWindowScene / TalkWindowPath.
CHECK: drive 86 (removed; 40/40): the balloon preloaded; the cursor model over PA (Spin / Icon / Anim playing, the icon 75 u up, 216 deg/s, the bob
8.76..20.04 u, K0 at the brk's frame 40 / 55 values); B -> the talk -> WaitCamera / Pause / Opening / Open; TALK_A, the root park (388, 115) -18, the
cursor away; 24 glyphs fading one by one after the 0.04-a-tick roll (1.0 -> -0.109); 'W' at (-123, 0) 18.5 deg and '!' at (138, 10) -22.9 deg =
the game's f32 arithmetic; the first glyph at 255; the camera 229.4 u off a look point 141.8 u up, the bearing exactly behind his face - 44.8 deg;
the lives away for the whole talk, the HUD's WAIT off; the arrow mark then A -> erase -> page 1 (16 glyphs) -> the dot mark -> A -> the camera back
at once, the root fading, Mario freed after 16 ticks with PA talk-forbidden; a spray mid-talk = forceCloseTalk (camera back, PB wet, Mario freed);
a Toad: TALK_B and the (160, 135) 20 park; <c3> / <c4> colours on the right glyphs and <w=8> slowing page 1 to 6 of 14 glyphs 40 frames in; a
52-char line wrapped 24 / 23 / 3 with line 2's band held until line 1's last glyph; B closes. Drive 83 re-run 33/33 (the take cursor path moved).
Build 0 errors, boot ALL OK.

## SMS pass 87 -- the talk, round 2 (Jaysonn's look at 86)

- **Mario talks like the game.** readBillboard (MarioAutodemo.cpp 63) plays ANIM_T_WAIT: the talk wait `ma_t_wait` now runs for the
  whole talk (the idle case of SetMarioState plays it directly, tree off, while `_smsTalking`; passes 82-86 left `ma_wait` on). MarioHeadCtrl's
  READ_BILLBOARD branch (MarioDraw.cpp 405-418): jnt_head gets Rz(-matan(horizontal, dy)) toward the NPC's focal point (TBaseNPC::getFocalPoint,
  NpcBase.cpp 427: its position + BodyHeight - LookatHeight = 190 u for a Pianta) from his eye line 112 u up (`SmsParams.Npc.MarioEyeHeight`) --
  -27.6 deg (up) at the 150 u he stands from a Pianta. Same SmsBoneRotZ as the gun-angle term, which stands aside during a talk; the end of
  the talk puts the tree back and the head to 0.
- **No pause during a talk.** The director reads START in its case 0 only (MarDirectorDirect.cpp 804-822): with the talk running (unk124
  1 / 2) nothing happens, no sound either. `Mario.SmsDirectorInPlay` (a talk, the legacy talking state or the shine demo = out of play);
  SmsPauseMenu returns before the open; SmsCanPause also refuses while `_smsTalking`.
- **The balloon in high resolution** (his "looks blurry and kinda bad"). The game draws the 20 x 20 glyph cells and the 292 x 58 band 1:1
  on its 640 x 448 frame; scaled x2.4 to a 1080p window every one-texel edge is a 2.4 px blur. gen_talk_hires.py resamples the disc's
  sheets x4 with a Catmull-Rom bicubic (tex/talk_font_0_x4.png 2048 x 1024, talk_band_x4.png, talk_cursor_mark_x4.png, talk_return_mark_x4.png;
  lossless, mipmaps on) and TalkGlyph.gdshader (new; on the glyph canvas and the nine marks) + TalkBand.gdshader sharpen the coverage back to
  a one-pixel edge: a = smoothstep(0.5 -/+ 0.75 fwidth(c), c) -- crisp at any Size, the soft bilinear look again once minified. The shapes
  are the disc's; `sharpness` (1) / `edge` (0.75) on both shaders. SmsTalkFont.PageScale (4): Cell() answers in texels, the drawn box stays
  the game's 20.
- **Three-line pages from the inspector.** `Lines` carries the MultilineText hint on its ELEMENTS now (TypeString "4/18:"): Enter in a page
  breaks a line (the game's .bmg pages are hand-broken the same way; AutoWrap still wraps a long one). Pass 82's array-level hint left
  every page a single-line field.
- **ParkOffset** (DEVIATION knob, TalkBalloon.tscn root; default (0, 0) = the game's (388, 115) / (160, 135)): frame units added to the
  park, for his "might need to be shifted up a bit".
- LEGACY RETIRED: the WAIT clip standing in for ANIM_T_WAIT during a talk; the 1x sheets as the drawn textures (kept as the sources).
- Drive 87 (16/16, removed): PageScale 4 on the 2048 x 1024 page, Cell('W') 80 x 80 texels, the glyph shader on the canvas and marks, the
  x4 band / marks in their panes, ParkOffset (0, 0); ma_t_wait with the tree off and looping through the talk; the focal point 190 u; the head
  Rz -0.4816 rad = -atan2(78.4, 150.0) exactly; a three-line page laid; START mid-talk opened nothing (the pad step on PhysicsFrame watched
  the menu while Mario's tick would have been frozen); after the talk START opened the menu (100 frames seen open), B closed it. Build 0
  errors, boot ALL OK.
- Tools: pngio.py (PNG in / out without PIL), gen_talk_hires.py, preview_talk_font.py (the shader's math on the sheet, next to the 1x
  bilinear look). jpa_dump.py: the BEM1 emitter ROTATION is three DEGREE shorts (JPAEmitter.cpp 204: x182 -> BAM), not BAM -- the pass-80
  reading of ms_2d_pause_sel's (90, 0, -12) as (0.49, 0, -0.07) is why the pause sparks lie on a line (pass 89 fixes them).

## SMS pass 88 -- the outfit as inspector options (Jaysonn: "make outfits like options, items they hold, really customizable")

- **Per-slot dropdowns on every NPC.** SmsNpc is a `[Tool]` script now: with a wardrobe (`Outfit`, an SmsNpcOutfitCatalog) assigned, the
  inspector grows an "Outfit Slots" section with one dropdown per slot -- None / Random / each part -- saved per instance as OutfitHat,
  OutfitGlasses, OutfitNeck, OutfitHand, OutfitMoustache (the W: OutfitFlower / Hat / Glasses / Arrows; the Toad: OutfitGlasses). That is
  TNpcParts' placement word (bit i picks slot i of NpcInitData's twelve), chosen in the editor instead of the stage data; "Random" keeps pass
  85's DEVIATION per slot. A chosen part shows on the model in the editor at once (EditorPreviewOutfit: instanced with no owner, hung on the
  joint, coloured; never saved into the scene; Random slots show at play).
- **The wardrobes are resources**: Profiles/OutfitPianta.tres (Hat x6 0.6, Moustache 0.35, Glasses x2 0.25, Neck: lapel / tie 0.3, Hand:
  luggage 0.1), OutfitPiantaFemale.tres (Flower 0.5, Hat 0.3, Glasses 0.2, Arrows 0.1 -- a Together slot: both at once), OutfitToad.tres
  (Glasses 0.3). SmsNpcOutfitSlot (Name, Odds, Parts, Together) and SmsNpcOutfitCatalog (Slots) are [GlobalClass] resources: add a part scene
  of your own to a slot (any Node3D with SmsNpcPart and its Joint -- 'kubi' head, 'body_jnt' chest, 'handR_jnt' the right hand, 'yashi_jnt'
  the W's hair) and it is a choice, named from its file (HatA.tscn -> "Hat A"); duplicate a catalog for a new wardrobe.
- **ExtraParts** (Array of PackedScene): always-worn parts on top of the slots -- things held, badges, your own models. **RandomOutfit** keeps
  its pass-85 meaning (every slot still at None is rolled; a part placed by hand under the node fills its slot), RandomOutfitDensity scales
  the odds (0.5 = the catalog's), PartColorIndices the three colour rows.
- LEGACY RETIRED: SmsNpcOutfit.cs (the static group table of pass 85, deleted); the "drop a scene under Parts" workflow stays as the hand-placed path.
- Drive 88 (11/11, removed): the five slot properties with their enum hints under the group, Get / revert, Hat B + Glasses A on kubi and the
  Luggage + Tie on body_jnt from the choices, the W's Together arrows on handR_jnt with a certain Random flower, the Toad's sunglasses, an
  ExtraPart of another species on the shared joint with a hand-placed Hat G keeping its slot out of the roll, 15 / 30 moustaches at 0.35,
  RandomOutfit dressing 20 / 20 with 15 hats and never two, the nerves running under the [Tool] script. Build 0 errors, boot ALL OK.

## SMS pass 89 -- the pause menu (Jaysonn: the sparks "not exploding", the stick, START)

- **The sparks explode now, like the game's.** jpa_dump.py had printed every emitter's ROTATION as if the three shorts were BAM; they are
  DEGREES (JPAEmitter.cpp 204: mRot x 182 -> BAM, Rz Ry Rx on the positions with the scale and on the velocities, calcEmitterGlobalParams).
  ms_2d_pause_sel's emitter is rotated (90, 0, -12): its Circle r 15, authored in XZ, stands up into the 2D screen's XY plane and tilts
  like the menu pane, so the 14 sparks (FixedInterval: i x 65536 / 14) fly OUT radially at 10 px/f under 0.85/f drag for 25 frames -- the
  sunburst of his pauseRef.png. Pass 80 had read the rotation as 0.49 degrees and laid the circle edge-on: a line of sparks sliding sideways.
  The scale (4 x width / 129, 1, 1) applies before the rotation and to the positions only (unkFC vs unkCC): an ellipse along the item. ESP1's
  scale is DISABLED (isEnableScale false): the 10 x 25 px streak (DirBillboard along the flight) keeps its size; alpha 1 to 50 % then -> 0;
  colour (255, 255, 72) -> (255, 39, 0) over the life. PauseSparks.gdshader (new, a particles shader: the fixed angles, the radial launch, the
  exponential drag integrated at 60 Hz, the quad along the velocity) on PauseSparks.tscn (GPUParticles2D, local coords); SmsPauseMenu sets
  rim_scale_x per burst and the node's rotation to the item's (-12 deg).
- **The cursor's stick** (DEVIATION, his ask): the stick is a menu button once past MenuStickOn (0.3; the game's JUTGamePad CStick::getButton
  uses 0.5, JUTGamePad.cpp 372) and released again below MenuStickOff (0.15), one move per press (the pad's trigger; the game sets no repeat
  mask for the main stick) -- read from the actions' RAW strength, so the InputMap's deadzone no longer decides and a D-pad bound to them reads 1.
- **START resumes** (DEVIATION, his ask, `StartResumes` on the menu, default on): START in the open menu leaves it like B whatever the cursor
  is on. The game's START is MENU_A -- it confirms the item (PauseMenu2.cpp 342), which on SAVE ran the save and the menu's dip (his "the menu
  kinda reloads"). Off = the game. A still confirms.
- LEGACY RETIRED: the ParticleProcessMaterial sparks with the rim / normal point textures (the directed-points trick of pass 80).
- Drive 89 (13/13, removed; the checks ran from a PhysicsFrame handler since the open menu pauses Mario's tick): START opened the menu with
  the tree paused, the first burst with the shader material and rim_scale_x 5.82 = 4 x 188 / 129 at the item's tilt -12 with 14 one-shot
  particles; 0.2 did not move the cursor, 0.35 did once, held 80 frames no second move, eased to 0.2 still held, released then up 0.35 back,
  a full push down again; START on SAVE closed the menu and unpaused with nothing confirmed, the sparks hidden. Build 0 errors, boot ALL OK.
  Not touched: PauseSmoke.tscn (ms_2d_pause_smo has the same (90, 0, 0) rotation -- its one puff per letter would spawn anywhere on a
  circle around the letter instead of on a line; the file carries his edits).

## SMS pass 90 -- the emitter-rotation audit (the fourth jpa_dump.py bug, found by the pause sparks)

- **The bug**: BEM1's emitter rotation is three s16 DEGREES; JPAEmitter.cpp 204 multiplies them by 182 (-> BAM) and JPAGetXYZRotateMtx
  (JPAMath.cpp 16) builds Rz Ry Rx, applied to the volume positions (with the emitter scale, unkFC) and to every velocity (unkCC) in
  calcEmitterGlobalParams. jpa_dump.py had divided the shorts by 0x7FFF x 180 (as if BAM): a 90-degree emitter printed as 0.49, so every
  bake since pass 33 took the emitters as unrotated. Fixed in the dumper (10-07); every shipped effect re-read:
    ms_2d_pause_sel (90, 0, -12) -> pass 89 (the sunburst);
    ms_wallkick_a / b (90, 0, 0): the kick's Circle r 15 ring and the marks' r 20 ring stand up ON THE WALL (the emitter's frame is
      emitWithRotate(yaw facing into the wall); Rx(90) turns the XZ circle into the wall plane) and the puff's dir 3 along (0, -1, 0)
      comes OFF the wall -- the port had both rings flat on the floor at the wall's foot since pass 60. FIXED HERE: WallKickDust.tscn, the
      Puff and Marks nodes turned Rx(-90) in the port's frame (its -Z faces the wall, the mirror of the game's +Z): the same ring on the wall,
      the same puff off it;
    ms_m_slipsmoke (12, 0, 0): the slide smoke's 6 u/f backward throw leans 12 degrees UP. FIXED HERE: SlipSmoke.tscn, the Smoke node Rx(-12)
      in the port's frame;
    ms_m_blur3 (-79, 0, 0) and ms_m_sphipd_c (-79, 0, 0): the hip-drop ribbons' points drift 2 u/f mostly UP (0, 0.98, 0.19) instead of
      forward -- 20 u over a point's 10 frames against the 800 u fall; the port threads the ribbon through the emitter's path without the
      drift at all. NOT changed (documented; a 2.5 % effect);
    ms_m_sphipd_a (11, 0, 0): the super hip drop's flame ring tilts 11 degrees -- NOT changed (minor; sphipd_b / d are a sphere and a point:
      the rotation is moot);
    ms_deadnamekli_n / o (0, 0, -90): the Gooble's death splash velocities turn Rz(-90) in the bound joint's frame: the dir (0, 1, 0) launch
      is the joint's +X. The shipped splash (72c) goes up and Jaysonn confirmed it; whether the joint's +X IS up has not been re-read -- open;
    ms_2d_pause_smo (90, 0, 0): the letters' one puff each would spawn anywhere on a 6..25 px circle around the letter instead of on a line;
      PauseSmoke.tscn carries his edits -- left alone.
- The spin jump's ms_m_blur2 / blur2sp carry no emitter rotation (0, 0, 0): his "spin jump effects look off" is something else -- his
  screenshot is needed.
- Verified: both scenes load in the headless boot (ALL OK, no scene / shader errors); the look is his to judge (the dust ring should now
  sit on the wall he kicked, not on the floor).

## SMS pass 91: the editor's InvalidCastException on every NPC, the female's T-pose, the spin rings compounding

- THE CAST ERRORS (his output: `Unable to cast object of type 'Godot.Resource' to type 'SmsNpcOutfitCatalog'` at SmsNpc.SetGodotClassPropertyValue,
  once per NPC, again on every inspector edit): pass 88 made SmsNpc a [Tool] script, and its two typed resource exports (Profile: SmsNpcProfile,
  Outfit: SmsNpcOutfitCatalog) point at C# Resource classes that were NOT [Tool]. In the editor a non-tool C# script gets only a placeholder
  instance (csharp_script.cpp instance_create: `!tool && !is_scripting_enabled` -> PlaceHolderScriptInstance), so the loaded .tres comes back to
  managed code as a plain Godot.Resource and the generated setter's cast throws (godotengine/godot #119603, open). The game process never sees
  it (the headless boot was clean), the editor does on every scene load and every "Set Outfit".
  FIX: [Tool] on SmsNpcProfile, SmsNpcOutfitCatalog, SmsNpcOutfitSlot (the catalog's typed Slots array casts each element), SmsNpcPart and
  SmsNpcPartMaterial (EditorPreviewOutfit instances part scenes: a non-tool part came back as a plain Node3D, so the pass-88 preview showed
  nothing). All five are data classes with no _Ready / _Process of their own -- nothing new runs in the editor.
- THE FEMALE'S T-POSE ("T posing but still looks at Mario"): a consequence of the above. While the casts failed, the editor's SmsNpc instances
  held Profile = null and Outfit = null, and when he saved the Level it wrote those as instance overrides on all three NPCs (Level.tscn lines
  75 / 76, 301 / 303, 308 / 309: `Profile = null`, `Outfit = null`). At run time `Profile ??= new SmsNpcProfile()` fell back to the class
  defaults -- a male Pianta's clip names -- so the female asked her player for 'mom_wait', which mow_model lacks, and Play() returned without a
  word: rest pose. Her neck tracking is a separate modifier on a bone both models have, so she still looked at him. The two males happen to
  equal the default profile and looked fine; all three lost their outfit catalog (no dropdowns, no rolls).
  The Level is HIS file with his editor open: the six override lines are for him to revert (select each NPC, the revert arrow on Profile and
  Outfit) or for me once the editor is closed. Verified: PiantaFemale.tscn instanced on its own plays mow_wait (probe); the Level's three
  NPCs now report the null Profile as an error naming the fix, and the female's missing clip as a warning (both new, below).
- Guards in SmsNpc (pass 91): a null Profile is a GD.PushError naming the node and the inspector revert (then the old fallback); Play() of a
  clip the model lacks warns once per clip name (`no clip 'mom_wait' in Model (profile ...)`) instead of silently keeping the pose.
- THE SPIN JUMP RINGS ("looks off", his spinJumpRef.png = the game): two things. (1) SmsSpinJumpBlur.ScaleQuads did `q.Size *= SizeScale` on
  the scene's QuadMesh sub-resources, which are SHARED through the cached PackedScene -- every re-instance (a death's ReloadCurrentScene,
  Mario.cs 14732 / 14742) multiplied the same mesh again: measured 1.343 -> 2.01 -> 3.02 -> 4.53 m over three instances (x1.5 each). After a
  few deaths the rings were several metres across. FIX: the two QuadMeshes are resource_local_to_scene (gen_fx.py + SpinJumpBlur.tscn) and
  ScaleQuads sets size = (base captured once) x SizeScale -- idempotent; measured 1.343 / 0.739 on all three instances. (2) SizeScale's
  default returns to 1.0 = the game's quads (100 / 55 units, then x 0..2 per ring at random as ms_m_blur2's ESP1 says -- rndm 0x7FFF, JPADraw.cpp
  242 `mScaleOut * (RF * randomScale + 1)`, RF in [-1, 1)). The 1.5 was pass 30's deviation at his word when the rings were a FIXED 100 units
  (radius 45 u, inside his torso); the random size has done that job since pass 78, and 1.5 on top made the biggest rings 3x the game's.
  The knob stays on Player.tscn's SpinJumpBlur instance (DEVIATION, his to set). Re-read for this: ms_m_blur2 / blur2sp carry no scale curve
  (in 0 / out 1: flat), rotation 90 deg fixed, no spin; the bound matrix is RT only (TMarioParticleManager::emitAndBindToMtxPtr, param 1:
  INFO_FLAG_BIND_TO_RT_MTX) -- no scale from Mario's matrix.
- Also committed: the editor's rewrite of OutfitPianta.tres / OutfitPiantaFemale.tres / PiantaMale.tres (uids added, default-valued fields
  dropped -- value-identical) and the two .cs.uid sidecars it generated for the pass-88 scripts, so the .tres uids resolve on a fresh clone.
- TELL HIM: build the hammer, then CLOSE AND REOPEN the project once (the editor's already-loaded placeholder resources do not become real on
  an assembly reload); then revert the six null overrides in the Level. The outfit dropdowns and the hat preview appear after that.
- Verified: build 0 errors; boot [SMS selftest] ALL OK (the three new NPC errors are the Level's overrides, by design); probes: the female
  alone on mow_wait, blur sizes constant over three instances; probes and logs removed before the commit.

## SMS pass 92 / 92b -- the female's T-pose closed, the outfit runbook

- 92 (no commit, your working Level.tscn): the six `Profile = null` / `Outfit = null` instance overrides the pass-91 editor
  session had saved onto Pianta / Pianta2 / PiantaFemale are removed (HEAD never had them). The female plays `mow_wait` again;
  probe + main-scene boot clean. Spin rings confirmed in-engine.
- 92b: `assets/Sms/Npc/OUTFITS.md` -- the outfit runbook: every slot and piece per wardrobe, what mixes with what, joints,
  colour channels and the game's colour tables as hex, the random odds per density, the rules and gotchas, how to add a piece /
  slot / wardrobe / something held, the pass-91 error fixes. Docs only; no legacy retired.

## SMS pass 93 -- NPC pollution: the sink, the dirty Pianta, the wash, the thanks

TBaseNPC's pollution behaviour (NpcChange.cpp 539-590, NpcAnm.cpp npcSinking / npcRecoverFromSinking / npcWetIn / npcWetting,
NpcEffect.cpp, NpcBase.cpp 718, NpcInitPrg.cpp 379) on `SmsNpc.cs`, for every `Profile.SinksInPollution` NPC (isPollutionNpc:
the Piantas, the Nokis, the Toad):
- **The sink.** Once a second per NPC (TNpcSink, staggered) a standing NPC asks the goop map under its feet; painted -> npcFallIn
  (the FALL clip) and TNerveNPCSink: y chases ground - SinkHeight (220 u a Pianta, 70 u a Toad) at SinkSpeed a tick (0.8 / 0.3,
  the shipped npc*.prm) while mPollutionAmount rides the depth to 1; at the bottom SINK_BOTTOM: no collision, no talk, no take,
  the WAIT_FALL clip. The goop cleaned under it -> TNerveNPCRecoverFromSink: collision back, the APPEAR clip, at its frame 32 a hop
  of CalcJumpVelocityY(220 + 150) (peaks 150 u over the ground), the clip's end sets it on its ground, TNerveNPCRecoverAfter plays
  RECOVER, then whatever the sink interrupted resumes -- dirty.
- **Dirty** (amount > 0): the DIRTY wait (mom_wait_yogore), no talk (changeNerveProc_ isClean), no walking or turning (execWalk /
  execUTurn / isNeedTurnToFirstState / isTurnToMarioWhenApproach), the head held straight (isNeedNeckStraight -- which also covers
  an NPC in Mario's hands, added), no anger (isStateGoToMad_), and the skin: every bmd's stage 0 compares H_ma_polmask_sample_i4
  (a 32 x 64 gradient) against GX_KCOLOR0.a = amount x PollutionMax (150) -- where the mask is under it the stage's h_ma_rak.bti
  (the goop; Delfino Plaza's from the disc) replaces the skin, lit by the same term (NpcSkin.gdshader, all three modes from the
  dumps). `Pollution` on the NPC = the placement's initial amount (the second colour triple's B / 255): place a Pianta already stuck.
- **The wash.** A dirty NPC the hose hits goes Wet on the WASH clip (mom_wet_yogore) and loses PollutionCleanSpeed (0.008) a hit;
  at 0 the thanks: talk forbidden 60 ticks, npcHappyIn -- the HAPPY pose (mom_happy, a loop) until the next talk ends (npcTalkOut),
  which may drop a coin: `ThankYouCoin` (TNpcCoin: 40 frames after the talk, at the cursor point, 15 u/tick at 75 deg along the
  facing; talk and walk forbidden 360). Empty = none, the game's default (the coin is a placement event).
- Textures: assets/Sms/Npc/tex/H_ma_rak.png (dolpic0's), H_ma_polmask_sample.png (from the bmd), pinned lossless. Toad.tres gets its
  shipped SinkSpeed 0.3 / SinkHeight 70 (the class defaults are the male's).
- DEVIATIONS: the Toad's bmd samples the mask and the goop through TEX1; its glb has no second UV set, so UV0 serves (`pol_uv2` is
  wired for a re-export). Not ported: the sink ripple (SMS_EmitSinkInPollutionEffect, Mario's too), the drip / wash particles
  (MS_MNT_YOGORE_B/F, MS_MNT_WASH), the happy sparkle (MS_MNT_KIRA), the sounds (NPC_APPEAR, APPEAR_ITEM).
- LEGACY RETIRED: the "pollution mask = 1" stand-in in NpcSkin.gdshader (pass 82 evaluated the clean case only).
- Verified headless (drive 93, 30 / 30): the goop layer's report; the placed-dirty Pianta's clip, K0 alpha 150/255, straight neck,
  refused talk; the sink within the poll's second, FALL then WAIT_FALL, 220.0 u down at 1.6 u a frame, amount 0.455 at 100 u, 1 at
  the bottom, collision off; the Toad's 70.0 u; the clean -> APPEAR / RECOVER, the hop's 150.0 u peak, back on its ground dirty with
  collision; the first hit -> Wet on WASH, 60 hits -> 0.520, 126 hits -> clean -> HAPPY, uniform 0, head tracking, talkable after the
  forbid; the talk while happy, its end -> the plain wait, the coin 40 frames later. Drives 83 (33/33) and 87 (16/16) re-run green.

## SMS pass 94 -- the throwing NPC (TNpcThrow) and Mario's THROWN_DOWN

- **The NPC** (`ThrowsMario`, `ThrowSpeed`, `ThrowAngleDeg` on SmsNpc -- the placement's flag 0x1 with its two numbers): a throw NPC
  takes the talk only within ThrowTalkAcceptDist 200 / Height 150 (npcnormal.prm) instead of 400 / 500; when the talk closes,
  changeNerveFromTalk_ runs TNerveNPCThrow instead of resuming -- the THROW clip (mom_throw, 119 frames), the hit filter
  NO_COLLISION from spine tick ThrowStartFrame - 20 for 30 frames (unk1DC), and at tick ThrowStartFrame (40) TNpcThrow::throwMario:
  the vector (0, sin a, -cos a) -- (0, 1, 0) at 90 deg or more -- turned by the NPC's yaw. The game's actor forward is (sin yaw,
  cos yaw), so -cos lands BEHIND the NPC, which turned to face Mario for the talk: Mario is hurled over it to its back, passing through
  it while the collision is off. The nerve ends with the clip; the interrupted nerve resumes.
- **Mario** (`SmsThrowMario(dir, speed, npc)`): receiveMessage(HIT_MESSAGE_THROWN) -> THROWN_DOWN, then TMario::throwMario: the vector
  normalised (zero = straight up), mFaceAngle.y = matan(z, x) + 0x8000 -- he faces BACK along the flight, at the thrower --
  mForwardVel = -speed x |xz| (he moves backwards along the vector), mVel.y = speed x y (units a tick, vel / 4 a tick like every Mario
  speed). thrownDowning() a tick: the stick adds 0.03125 x mag x cos(stick - face) x ThrownAccel (0.3, jump.prm) to the speed and
  0.03125 x mag x sin x ThrownSlide (0.1) BAM to the face -- a u16, so under one BAM a tick it truncates to nothing, as it does with
  the shipped values -- fwd x= ThrownBrake (1.0); jumpProcess: a landing bounces twice (vy = 0.4 x -vy, speed halved), the third is
  THROWN_END (squatStandup: ma_thrown_end once, a pushed stick is RUN at once, the clip's end WAIT); a wall mirrors the face
  (playerRefrection). ANIM_THROWN loops through the flight. Lives in the hurt state as SmsDownKind.Thrown / ThrownEnd next to the
  fire down (no FLUDD, no second hit while down: the game's isPumpOK is false on ANIM_THROWN too).
- Not ported: MA_HANEAGARI / the surprise voices on the message. The spine clock: tick 40 = 0.33 s into the clip (the spine counts
  CUE_MOVE ticks; changeNerveProc_ and unk1DC count frames) -- if the hurl looks early against the clip in-engine, ThrowStartFrame
  is the knob (SmsParams.Npc).
- LEGACY RETIRED: none (the port had no thrown status; ma_thrown served only the long fall).
- Verified headless (drive 94, 13 / 13): no talk from 223 u, the talk from 134 u; the close -> THROW nerve on mom_throw; collision off
  at +10 frames (tick 20), the throw at +20 (tick 40); the flight along the NPC's back at 21.2 u/tick (30 x cos 45), vy 19.2 after the
  first frame's two ticks, Mario facing back along it; ANIM_THROWN, two bounces (3.35 m, 4.31 m), THROWN_END on ma_thrown_end at
  4.67 m, up at idle with full HP; the apex 67.9 u = the game's 58.9 + the port's 8.9 u launch clearance nudge (Mario.cs EndFrame, every
  launch); the collision back after 60 physics frames (30 game frames), the nerve ending with the clip into the wait. Drives 87 (16/16)
  and 83 (33/33) re-run green.

## SMS pass 95 -- the NPC particles (NpcEffect.cpp), the goop sink ripple, and the Pianta leaves

- **The leaves** (`NpcLeaf.gdshader`, `SmsNpcProfile.LeafMaterial` / `LeafTexture`): `_leafback_mat` on mom_model / mow_model kept the glb
  import's StandardMaterial3D -- metallic 1.0 (the glTF default when the exporter writes none), spec 0.5 -- on an 8 x 8 DUMMY texture,
  which is the "metallic NPCs". The game swaps that dummy (I_mom_mino_dummyI4 / I_mow_mino_dummyI4) for the STAGE's
  /scene/monteMCommon/I_mom_mino_rgba.bti at load (NpcManager.cpp); every stage ships its own, Delfino Plaza's two are under
  assets/Sms/Npc/tex (RGB5A3 128 x 128, alpha-tested at 128). The shader is the bmd's TEV: the pollution mask compare (TEX1 on this
  material), rak x (1 - clean) + white x (1 - clean) + leaf x clean (chan1 is UNLIT white on this material -- a goop-covered leaf region
  reads WHITE, literally as the data says; the leaves' mask UVs sit mostly in the mask's clean rows), then + 1/2 x (C0 (150,150,180) +
  mix(toon, lit, 5/8) - 1/2) - 1/2 with the same per-vertex GX light as the skin; cull NONE, z write, blend NONE.
- **The pollution mask's UV set is the MATERIAL's** (`PollutionUv2Materials`): pass 93 used any UV2 array the mesh carried -- the Pianta
  body's glb has a ZERO-FILLED second set, so the mask was sampled at one texel of its top row and the body never showed the feet-up
  gradient (the goop came on all at once near the full amount). The bmd reads the body's mask through TEX0 (texgen0 / 1 src TEX0 on
  _hand_mat, both models), the leaves' through TEX1. The Toad keeps its documented UV0 deviation.
- **The effects** (`SmsNpcEffect.cs`, assets/Sms/Fx/Npc*.tscn from gen_fx_npc.py; the profile's `EffectJoint` / `HappyEffect` /
  `DirtyDripEffect` + `DirtyDripFrames` / `FootDripEffect` + `FootJointL` / `R` + frames / `WashEffect` / `SinkEffect`, Kind defaults
  when empty): emitHappyEffect_ -- ms_mnt_kira (Piantas, koshi_null) / ms_mare_kira (Toads, jnt_body) when the HAPPY clip starts, x the
  NPC's scale x mSLCleanEffectScale 1.65 (npcnormal.prm): 60 / 45 stars a second for 80 frames, life 30 +-50 %, a shell 69..110 / 50..80 u,
  drifting up at 0.15 u/f^2, env a random gold (255,206,24) .. red (255,63,15) per star to white, the AddSin twinkle, 1 -> 0.1 from half
  life, additive. emitDirtyEffect_ -- on the DIRTY idle's check frames (sCheckFrameMonte 28 52 76 128 152 176 at the centre, the left foot
  28 52 76, the right 128 152 176; Kino 22 44 at jnt_body) a burst of goop balls (P_msm_osenball, alpha test 190, z write, lerp(env ->
  prm) with the STAGE's pollution colours: SMSSetEmitterPolColor 6 on Delfino = (98,14,0) -> (181,132,102); the shader's env / prm for
  another map's row). emitWashEffect_ -- one emitter at EffectJoint kept alive while the WASH clip plays (flag 1 = the owner's slot),
  0.85 / 0.7 balls a frame off a 45..60 / 19..25 u shell. The emitter sits on a BoneAttachment3D of the joint (the game's MtxPtr bind),
  TOP-LEVEL and following the joint's orthonormalised frame (the skeleton carries the model's 1/74.44), particles world-space.
- **The goop sink ripple** (`SmsGoopRipple.cs`, GoopSinkRipple.tscn): SMS_EmitSinkInPollutionEffect (EffectUtil.cpp 56) -- every 20 of
  the director's move ticks (a 120 Hz counter: MarDirectorDirect.cpp 104, +1 per simulation tick) an UNBOUND ms_mari_rakuhamon at the
  sink point with the ground normal as Y, plus ms_mari_rakuball while not at the bottom. Both are PROJECTED screen quads: BSP1 projection
  + the main texture swapped for TScreenTexture (320 x 224, flag 2) + ETX1 indirect mode 1 -- the warp texel (I - 128) x [0.5 0 / 0 0.5]
  x 2^-2 = (I - 128) / 8 texels of the copy displaces the screen lookup, the second texture (indmsk) multiplies colour and alpha
  (goop_refract.gdshader: hint_screen_texture, SCREEN_UV + d). The ring: a Direction quad flat on the goop, 300 u x 0.2 -> 1.0 over 80
  frames, 6 a second; the balls: billboards 35 u, X 0.85 Y 3.0 -> round -> tall, 20 u/f up (1 +- 0.375) off a 37.5..100 u circle under
  1.5 u/f^2, 48 a second. Wired for the NPCs (NpcBase.cpp 554: while sinking, sunk or climbing out; no balls at SINK_BOTTOM) and for
  MARIO (MarioMove.cpp 1102: the ring while isTouchGround4cm && isDirty on ANY paint; 1221: ring + balls while he sinks) -- GoopRipple
  on Player.tscn, found like every Fx scene.
- Deviations, logged: a faded-in JPA gravity and a constant Air lift are folded into one constant gravity matched on the end
  displacement (Godot has one gravity); the kira's per-star moment 1 +- 0.375 is dropped; the rakuball's circle is uniform in the area
  (the game's in the radius); the drips' random initial turn is dropped (a round ball); mipmaps on the warp textures.
- Not ported: the burning NPC (ms_npc_kokuen / moe_fire), the note (MONTE_MH / MARE_MD), the water ripples (MONTE_MF / map 4), the DMG
  stars (HIT_MESSAGE_UNK10 / the barrel), ENM_WATHIT (only on a burning NPC), HIDDEN / CLIPPED_OUT, the sounds.
- LEGACY RETIRED: the leaves' import material (the metallic StandardMaterial3D on the dummy).
- Verified headless (drive 95, 22 / 22): the male's and female's leaves on NpcLeaf.gdshader with I_mom / I_mow_mino_rgba, the leaf's
  mask on UV2 and the body's on UV0, no leaf on the Toad; 14 drip bursts in 430 frames of the DIRTY idle, the first at clip frame 29.3
  (28 + the frame's step), NpcDripBody at koshi_null and NpcDripFoot at footL_jnt / footR_jnt, the node at world scale 1.0000 within
  3 cm of the joint; the sinking Pianta's ring + balls on the way down, ring only at the bottom, the ripple at the sink point with Y up,
  off again on its ground; the wash emitter alive on mom_wet_yogore and gone once clean; the happy sparkle at x 1.650; the Toad's drips
  at jnt_body with no feet; Mario's ring + balls on a sink layer (sinkTimer 62), both off beside the clean spot. Drive 93 re-run 30/30.
  Headless cannot draw: the refraction's look, the leaf texture on the fringe and the drips' colour are for the eye.

## SMS pass 95b -- the damage fog's pulse clock

- SMS_AddDamageFogEffect's s = sin(gpMarDirector->mMoveTickCount x 0x888) has a period of 65536 / 0x888 = 30 ticks. The port (pass 45's
  BodyOverlay) read the move tick as a 30 Hz count and ran the pink pulse at ONE second; mMoveTickCount is incremented once per
  SIMULATION tick (MarDirectorDirect.cpp 104: inside the loop that consumes 5 of the frame's 20 pending time units -- four ticks a drawn
  frame, 120 Hz), so the game's pulse is a QUARTER second: the fast pink flicker after a hit. `FogPeriodSeconds` 1.0 -> 0.25 on
  SmsBodyOverlay and BodyOverlay.tscn. (Found while porting the sink ripple, which keys off the same counter's % 20.)
- The same counter already drives the sink damage cadence (% 90 = 0.75 s) and the time-lag's 0x4B0 wait at 120 Hz in Mario.cs -- those
  were right.

## SMS pass 96 -- the goop into the opaque pass (the ripple showed the floor, not the goop)

- His rippleEffect.png / rippleEffect2.png: the pass-95 goop ripple ring read as a soft WHITE disc that erased the goop under it, with Mario's
  shadow warped inside. Cause: Goop.gdshader blended its soft edge (blend_mix + ALPHA), which put the WHOLE goop surface in Godot's
  TRANSPARENT pass -- and `hint_screen_texture` is a copy of the OPAQUE pass only. Every screen-reading surface over goop (the ripple
  ring and balls of GoopSinkRipple, the Gooble's jelly body, Mario's damage fog) saw the bare floor plus Mario's shadow where the goop
  should have been. The game's TScreenTexture (EmitterViewObj.cpp 472 / ScreenUtil.cpp 218) copies the frame drawn so far, pollution
  layer included.
- FIX, Godot-native: the shader body moves to assets/Sms/Fx/Goop.gdshaderinc. Goop.gdshader = the covered interior (the edge smoothstep
  at or above RIM_CUT 0.999), drawn OPAQUE -- it discards the rim and writes no ALPHA, so it lands in the opaque pass and the screen
  texture holds the goop. GoopRim.gdshader = the soft rim alone (the band under RIM_CUT, ~11 cm at the 32-unit texel), blended as before,
  drawn as the goop material's next_pass. SmsPollutionLayer.EnsureRimPass() (after the layer's uniforms) creates that next_pass (or
  reuses one already on GoopRim) and copies EVERY uniform of the interior material across, so a material of yours keeps its look on the rim;
  the map texture is the same object (updated in place); `GoopRimShader` export for a rim of your own; a foreign next_pass is left alone
  with a warning. The look is unchanged: the interior was already fully opaque. LEGACY RETIRED: none (one material becomes two passes).
- VERIFIED headless (probe96.gd): the three shader files load with no SHADER ERROR; the interior and rim expose the same 29 uniforms; a
  Goop.tscn with its own material gets Goop.gdshader + a GoopRim next_pass carrying pollution_map (the same ImageTexture), threshold,
  layer_min / inv_size / to_local, goop_albedo, blotch_mix; a Goop.tscn given a ShaderMaterial of its own (PinkGoop, brightness 0.91,
  flow_speed 0.33, goop_color) gets the same knobs on its rim. Your Level's five goop instances set `GoopShader` (the interior shader),
  no material override, no rim shader -> the default rim. Floor clearance (probe96b.gd, your Level): the goop quads sit 6.1 mm above the
  floor's BoxMesh top (0.3493 vs 0.3432), no z-fight risk in the opaque pass. Build 0 errors, boot `[SMS selftest] ALL OK`.
- NOT A BUG (his "weird texture on the gloves and boots"): Sunshine's dirty fog. TMario::addDamageFog (MarioDraw.cpp 2184) fogs body, cap
  and hands (0x82, 0x96, 0x00) while MARIO_FLAG_DIRTY && mPollutionTypeStandingOn == POLLUTION_TYPE_SLIP (2) -- DIRTY is simply "standing on
  paint" (MarioMove.cpp 1095) -- yellow while mStandingOnGraffitoTimer > FogTimeYellow (240 of the 840-tick cycle: 5 s yellow, 2 s pink,
  the floorDamageExec hit 2 s in), the strength 7/12 - s/4 (0.33..0.83) at the 0.25 s pulse of 95b. The whole body is tinted (cap orange,
  overalls teal in the same shots); white gloves and brown boots just show it most. Mario's bmds (ma_mdl1 / ma_cap1 / ma_hnd2l) carry fog
  type 2 = GX_FOG_LIN with range adjust off -- the pass-56 linear assumption confirmed.
- NOTED, his Level: the test floor's collision top (0.5715) sits 22.8 cm above its visible BoxMesh top (0.3432), so Mario and the NPCs stand
  23 cm above the visible floor and the ripple ring (placed at his feet) floats that much over the goop. Not touched.

## SMS pass 97 -- the dirty Pianta's white skirt (a TEV channel misread) and Mario's damage fog into his own draw

- His piantaSkirtRef.png: a Pianta that had been in the goop stood with a WHITE skirt (a bluish white; the clean Pianta beside it had the
  green leaves). Cause: NpcLeaf.gdshader (pass 95) read the leaf material's TEV stage 2 -- `PREV = CPREV + (1 - REG2) x RASC + REG2 x TEXC`,
  RASC from channel COLOR1A1 -- with RASC taken as a constant white. bmd_mat_dump prints J3D's FOUR channel entries, and J3D's order is
  COLOR0, ALPHA0, COLOR1, ALPHA1 (J3DColorChan::load's chanTbl, J3DSys.cpp 190): the dump's `chan1` (lighting off, matColor white) is
  ALPHA0, and GX_COLOR1 is `chan2` -- lighting ON, lightMask 0x04 (the stage's light 2), attn 0 = GX_AF_SPEC (setChanCtrlMacro forces
  GX_DF_NONE with it), ambColor1 black, matColor1 white: the SPECULAR channel, the same one the body's stage 6 and the Toad's cap rasterise
  (NpcSkin.gdshader's `spec`). So the goop case is rak + spec -- the goop texture with the highlight -- not rak + white (which clamps to
  white, then stage 4's half-light term tints it blue: exactly the shot). FIX: `v_spec` (NpcSkin's formula, shininess 50) in the leaf
  shader's vertex pass, stage 2 = rak x (1 - clean) + spec x (1 - clean) + leaf x clean; the header's channel reading corrected. The
  clean leaf is unchanged (clean = 1 zeroes both goop terms). LEGACY RETIRED: none (one term of one shader).
- His marioTextrueRefbeingSeethru.png (Mario sinking in pink goop, the ripple ring and balls around him, the damage fog on): his navy
  overalls appeared SMEARED around his magenta legs inside the ring, and the balls showed an unfogged red-and-blue Mario. Cause: the pass-56
  damage fog was a screen-reading overlay (material_overlay, hint_screen_texture) -- which lives in Godot's TRANSPARENT pass -- so the
  OPAQUE pass, which is what hint_screen_texture copies and what the goop ripple (goop_refract.gdshader) and the Gooble's body refract,
  held the UNFOGGED Mario. The game's copy is the finished normal scene: /data/PerformLists.bin (performlists_dump.py) runs 通常シーン描画
  ステージ (the TEfbCtrlTex whose DRAW is the GXCopyTex into TScreenTexture, JDREfbCtrl.cpp 95-108) at entry 140, after the player group
  (73-75, 134-135) and before the indirect / particle draws (141-160) -- the fog (a GX fog on the body's own materials) is in it. The
  rest of the shot is the game: he sinks (SmsSinkDraw, MarioDraw.cpp 1760: 150 u x timer / 240), the floor hides his feet, the ring at his
  feet height; the rakuball billboards refract the screen displaced up to 16 texels of a 320-wide copy (5 % of the screen) -- so a ball in
  front of his shin shows the goop beside it. The one port error was the unfogged copy.
- FIX, Godot-native: the fog becomes part of the body's OWN draw, as the GX does it. assets/Sms/Fx/MarioFog.gdshaderinc = the surface's
  StandardMaterial3D re-stated as a shader (albedo colour x texture [x vertex colour], roughness, metallic, specular, Burley / Schlick-GGX,
  uv1 scale / offset, linear + mipmap sampling, repeat or clamp in the UV) plus the fog, with four thin variants for the static part
  Mario's surfaces use: MarioFog (opaque, cull back: 7 body surfaces), MarioFogTwoSided (the 5 hand meshes' glb materials, cull off),
  MarioFogAlpha (transparency ALPHA, cull back: _mat_head(4) / (6) / _mat_eye_L), MarioFogAlphaTwoSided (_mat_head(2), albedo alpha 0).
  SmsBodyOverlay.Bind builds one ShaderMaterial per body surface from its StandardMaterial3D (the values copied in; a surface with a feature
  the shader does not state -- another transparency / cull / filter mode, emission, normal map, a metallic or roughness texture, fades,
  billboard, grow, a next_pass -- is left alone with a warning and stays unfogged, so the body can never silently change look); SetDamageFog
  swaps each surface's override for its fog copy and puts the exact previous override back when off (FoggedCount / FogSkippedCount /
  FogMaterialOf for the drive). The fog itself is Godot's FOG output -- applied to the LIT colour, in the opaque pass -- and the GC mixes
  the framebuffer's gamma values, so FOG is solved per pixel for the surface lit at 1: mix(a, F, fa) = to_linear(mix(to_gamma(a), colour,
  f)), fa raised above f only as far as keeps F >= 0 (a gamma mix darkens a bright channel more than any non-negative linear fog at f can;
  a negative F would go negative on a shaded pixel and NaN in the tonemapper). fogcheck97.py: exact at full light for both colours
  (0.00 / 255), and a half-lit pixel lands within ~5-20 of 255 of the game's value where a plain linear mix is 30-50 off (the glove's
  green at f 0.58: game 75, this 73, linear 120). DOCUMENTED APPROXIMATION: the shaded side of the body is not exactly the game's mix.
  BodyOverlay.gdshader keeps the shock's NEGATIVE only (drawLogic IS a second pass over the finished body in the game; a screen-reading
  overlay is the right place for it) -- `fog` / `fog_color` removed from it and from BodyOverlay.tscn, which now carries the four shader
  exports. LEGACY RETIRED: the fog's screen-reading overlay (passes 56 / 95b / 96's "the damage fog saw the bare floor" note is moot --
  the fog no longer reads the screen at all).
- VERIFIED headless (drive97.gd, 29 / 29; run under run_bounded.ps1 with a hard time limit after the first run stalled -- see below): the
  six shaders load; the leaf shader carries `v_spec x (1 - clean)` and no white term; the overlay shader has no fog uniforms; Mario's 6
  body meshes / 16 surfaces all get a fog copy (0 skipped), 7 + 5 + 3 + 1 across the four variants as the surfaces' (transparency, cull)
  say, every copy carrying its base's albedo colour, albedo texture (the same resource), texture_repeat, roughness, specular, metallic,
  vertex-colour flags, uv1 scale / offset and render priority, with no material_overlay on them; the FLUDD's 5 meshes untouched; the pulse
  0.333..0.833 at the 0.25 s period with the `fog` uniform following the factor every frame; the colour switch (pink -> the goop yellow)
  reaches all 16; the negative puts the overlay on the 6 meshes with the fog copies still on (6 / 16) and takes it off; fog off restores
  every override and active material by identity, twice; the Pianta's leaf material is on NpcLeaf.gdshader with pol_tex / pol_mask_tex
  bound and the shininess knob declared. Build 0 errors, boot `[SMS selftest] ALL OK`, 0 ERROR lines, no `[SMS fx]` skip warning.
- TOOLING: drive97's first run hit a script error before its quit() and the headless tree ran 90 minutes writing a 939 MB log (Mario's
  _PhysicsProcess throwing every tick with no GameData autoload). run_bounded.ps1 (Start-Process + WaitForExit + Kill) now runs every probe
  / boot with a hard limit; the drive carries its own 60 s watchdog Timer and null-safe reads. Also: under --script Mario._Ready stops at
  GameData.Instance BEFORE its Bind calls, so a probe must call BodyOverlay.Bind(mario) itself (and switch his physics tick off, since
  SmsAfterMove drives SetDamageFog from the game's condition every tick). performlists_dump.py reads /data/PerformLists.bin (the game's draw
  order, Shift-JIS names + cue bits).
- NOT CHANGED, for his eye: the ring sits at his collision feet (22.8 cm over the visible floor on his test level, pass 96's note); the
  balls' refraction of his legs is the game's (JPA indirect warp of the screen copy); the gamma solve's shaded-side approximation above.

## SMS pass 98 -- the burning NPC (NPC_ACTION_BURNING)

- The Bianco Hills Pianta on fire, from TBaseNPC. In the game it is a PLACEMENT: NpcInitPrg.cpp 451 `isNormalMonteM() && NPC_ACTION_BURNING`
  (the action flags come with the stage's object data, BURNING together with NPC_ACTION_RUN and UNK80) -- only a normal male Pianta burns.
  PORT: `Burning` on the NPC instance (+ `BurnStrength`, the game's mBurnStrength init 1.0, as a knob); a female or a Toad with it set warns and
  ignores it (`Profile.CanBurn`, false on PiantaFemale.tres / Toad.tres). NOT MODELLED: NpcInitPrg.cpp 456-469 -- the burn is skipped when the
  NPC's blue coin (ids 0..49 via TFlagManager::getBlueCoinFlag) is already collected; the port has no per-NPC coin flags.
- WHILE BURNING (every site checked in the source): changeNerveProc_ 465 refuses the talk (`!_burning` in TalkCheck); isStateGoToMad_ 425 refuses
  Mad (CanGoToMad); changeNerveProc_ 542 skips the pollution sink poll (PollutionCheck); behaveToHitObject_ 344 takes ONLY the water spray and only
  as fire-fighting (no Wet nerve, no wash): mBurnStrength -= mSLFireDecSpeed (npcnormal 0.002, SmsParams.Npc.FireDecSpeed) a hit, MSD_SE_NPC_
  FIRE_FIGHTING gated (the `FireFightingSound` slot), PARTICLE_MS_ENM_WATHIT (not ported); isExtinguished_ (<= 0) -> strength 0, BURNING | UNK80 |
  RUN off, npcHappyIn(1) = the HAPPY pose + the sparkle until the next talk ends (that talk's ThankYouCoin). NPC_ACTION_RUN: the wander plays the RUN
  clip (walkAnmRateChange_ 356) with execWalk's run speed and accel x mSLSmokeRunMagnif (NpcWalkTurn.cpp 60, npcnormal 1.5 = SmsParams.Npc.
  SmokeRunMagnif: 5 -> 7.5 u/tick, 0.08 -> 0.12) and the clip rate's max and speed cap x the same (NpcAnm.cpp 325). UNK80: TNerveNPCGraphWander 44
  never pushes GraphWait -- the burning runner takes the next node at once. MSD_SE_NPC_BURNING (NpcBase.cpp 728) = the looping `BurningSound` slot.
  Mario is NOT hurt by touching it: no NPC path sends HIT_MESSAGE_BURN (MarioReceiveMsg.cpp's BURN comes from fire actors, never TBaseNPC) -- the
  pass-85 hitNpc keepDistance / trample stand.
- THE FIRE (emitParticle_, NpcEffect.cpp 233): four LOOP emitters SMS_EasyEmitParticle binds to mFireParticlePos -- emitAndBindToPosPtr flag 1 / 3
  (EmitterViewObj.cpp 289-320: ONE emitter per (effect, owner), re-positioned each frame, dropped when no longer asked for) -- at the TRANSLATION
  of mSmokeEffectMtxPtr (setSmokeEffectMtxPtr_, NpcEffect.cpp 82): `yashi_jnt` (the palm on his head) or, with parts bit 0x800 = slot 11 = the
  LUGGAGE (sMonteM parts table NpcInitData.cpp 1374-1377: HatA, MustacheA, GlassesA, GlassesB, HatB, HatE, HatD, HatF, HatG, LapelA, TieB,
  Luggage), the luggage model's `ef_null` (nimotsu_model.glb node 2, a child of `nimotsu`). A position bind keeps the WORLD axes: the fire rises
  world-up whatever the head does. All four x getEffectScale_ (mInitialScale); ms_moe_fire_a / b / c x mBurnStrength as well (setGlobalScale each
  frame -> the quads and offsets shrink to nothing as it is hosed). PORT: SmsNpcEffect gains `FollowRotation` (off: position only) and
  `SetStrength` for the emitters in node group `sms_burn_strength` (per-instance process-material copies: scale_min / max, the emitter offset,
  the flame's sphere x strength); the fire joint = the first attached part whose Skeleton3D carries Profile.FireJointOnLuggage (`ef_null`), else
  Profile.FireJoint (`yashi_jnt`); the effect cache is keyed per (skeleton, joint) so a part's bone and the body's share one dictionary.
  assets/Sms/Fx/NpcBurn.tscn (gen_fx_npc.py burn(), from jpa_dump + the JPA source -- Gravity field add type 1 = addBaseVelVec per frame = an
  ACCELERATION, add type 2 = mFieldVelocity zeroed every frame (JPAParticle::calcVelocity) = a CONSTANT drift):
    Smoke  ms_npc_kokuen: Point at (0, 20, 0) u, 0.35/f, life 70 +-30 %, velDir 2 up + omni 0.5, +0.05 u/f^2 up, Billboard 45 u growing 1 -> 2.2
           over the life (ESP1 outTiming 0) x (1 +- 0.4), alpha 0 -> 0.9 by 12.5 % -> 0 from 62.5 %, a fixed +-22.5 deg tilt; colour lerp(env -> prm, I)
           with BOTH registers keyed over the life -- colour calc 3 = Merge (JPADrawCalcColorAnmFrameMerge: the 21-entry table stretched over the
           life): prm (87,8,0) @2 -> (45,34,34) @8 -> (72,72,72) @15 of 20, env (255,111,48) -> (96,96,96) -> (120,120,120) -- fire_smoke.gdshader
           samples two GradientTexture1Ds by INSTANCE_CUSTOM.y (ParticleProcessMaterial's life phase).
    Flame  ms_moe_fire_a: Sphere r 8 at (0, 10, 0), 0.5/f, life 15, velDir 1 + the field's 8 u/f drift = 9 u/f straight up (1.8 m in a quarter
           second), StripeCross 80 u ALONG the velocity (two crossed QuadMeshes, particle_flag_align_y) shrinking -> 0.5 from 25 % x (1 +- 0.1), a
           +-18 deg turn; TEXC x prm (255,228,170) + env keyed (255,54,0) -> (255,118,31) over 5 frames, colour calc 1 = Repeat (frame = age % 6: a
           10 Hz red -> orange flicker per particle), alpha = TEXA x 231/255, no ESP1 alpha -- flame_tongue.gdshader.
    Glow   ms_moe_fire_b: Point at (0, 10, 0), 0.25/f, life 15, 10 u/f up, Billboard 137.5 u -> 0.6 from 29.4 %, alpha 0 -> 0.4248 by 15 % -> 0
           from 71 %, colour prm FLAT (255,172,101), alpha = I (radgrad) x the fade, blend SRCALPHA + ONE -- glow_add.gdshader.
    Haze   ms_moe_fire_c: Point at (0, 35, 0), 0.125/f, life 22, 6.5 u/f up, Billboard 62.5 u -> 2.5 over the life x (1 +- 0.05), any turn, alpha
           0 -> 1 by 55.1 % -> 0 from 62.5 %; BSP1 projection + ETX1 indirect mode 1: the SCREEN copy (P_ms_fb_8x8i4 swapped for TScreenTexture)
           warped by P_msm_indwp2 with mtx 0.5 x 2^-5 = (I - 128) / 64 texels of the 320 x 224 copy (an eighth of the goop ripple's), the second
           texture P_msm_indmsk1 (ETX1 flags 0x01, index 0) on colour and alpha -- goop_refract.gdshader with ind_scale 0.015625. The heat haze
           refracts the opaque pass: the goop and the fogged Mario are in it (passes 96 / 97); the flames are not, as in the game (the particle stage
           draws after the copy, PerformLists.bin 140 / 152-160).
  Textures (jpa_bake.py, raw mode: I in RGB, A in A): kemuball3_raw, fire1_raw, radgrad_raw, indmsk1, indwp2 under assets/Sms/Fx/tex, pinned
  lossless / mipmapped / no 3D auto-compression (fx_tex_imports.py).
  DEVIATIONS: the smoke's omni 0.5 becomes a 14 deg cone at 1.5..2.5 u/f (Godot adds no independent radial velocity); the StripeCross's roll about
  the velocity is Godot's basis, not the game's +-18 deg random; the flame's emission sphere shrinks with the strength as the quads do (the game
  scales the emitter volume the same way). LEGACY RETIRED: none (a new state on the NPC).
- RUNBOOK: OUTFITS.md 7b gains the Burning switches, 7c the profile's Can Burn / Fire Joint / Fire Joint On Luggage / Burn Effect.
- VERIFIED headless (drive 98, SmsF98: 21/21): the two placed males burn and the female warns and ignores it; the fire joint yashi_jnt bare / the
  luggage part's ef_null worn; strength 1; the burning wanderer on mom_run, top speed 7.50 u/tick and clip rate 4.50 (both past the plain caps, =
  x1.5), no GraphWait in 600 ticks; one NpcBurn with 4 emitters, 3 on the strength group, world axes kept (dot 1.0000) at the joint position at scale
  1; not talkable while burning (TurnToMario, mom_wait); 100 hits -> 0.800 with the effect at 0.802; out after 500 hits -> HAPPY on mom_happy with
  the sparkle, the effect dropped, never a Wet nerve; talkable again once out; the runner put out stands (speed 0); the luggage one untouched.
  Two first-run FAILs in the engine, fixed before anything was committed: INSTANCE_CUSTOM is a VERTEX built-in (fire_smoke / flame_tongue read it
  in fragment() -> "Unknown identifier", the shaders did not compile): read in vertex(), carried by a varying; and the Flame node's draw_pass_2
  needs `draw_passes = 2` declared first (the second StripeCross quad was out of bounds). Drives 95 (22/22) and 93 (30/30) re-run green; build
  0 errors; boot [SMS selftest] ALL OK with no error or shader line.

## SMS pass 99 -- the burning Pianta's luggage, the water-hit mark (ENM_WATHIT), the fire's billboards

Jaysonn (10-08): "piantas are only supposed to be on fire when they have the backpack model? or that's where the fire should be ... can
you make the effect of FLUDD hitting enemies? I couldn't tell if I was hitting the Pianta to put out the fire ... positions and placement
seem off".

### What the game does (the source + the disc)
- The fire joint (NpcEffect.cpp 82 setSmokeEffectMtxPtr_): by NAME -- `yashi_jnt` (the palm on his head) on a bare body, the luggage part's
  `ef_null` with the luggage (parts bit 0x800); mFireParticlePos = that matrix's translation, no offset (233). So the code has both cases.
- The DATA only ever uses one: every stage scene.bin was parsed (npc_placements.py, the TBaseNPC::setIndividualDifference_ layout) -- of 912
  Pianta placements, 8 have the burning action (index 24 = BURNING | UNK80 | RUN) and ALL 8 carry parts bit 0x800, the Luggage: dolpic6-10's
  'MonteM' (blue coin 4, graph monte0/8/10/11), dolpic_ex3's (HatG + Luggage, coin 200), monte2's 'monte28' (coin 22), monte_ex0's (coin 2000).
  The game's fire is only ever seen on the bag; his Pianta3 (Burning, no luggage) was the bare case nobody saw in the game -- the fire at the
  palm base, 2.35 m up a 3.43 m body (probe99.gd measured it; the luggage's ef_null is 1.93 m up, 0.62 m behind, the bag's centre).
- The water-hit mark: PARTICLE_MS_ENM_WATHIT (0xE7, ms_enm_wathit.jpa), emitted unbound once per hit -- TSmallEnemy::receiveMessage 547 at the
  water hit actor's position (the drop), TBaseNPC::behaveToHitObject_ 351 ONLY while BURNING, at the NPC's own mPosition (its feet). A plain or
  washing NPC emits nothing (the game's TSplashManager splash there, 30 % of accepted hits, size 5, is not ported). TModelWaterManager::move()
  429: when the actor takes the message (both return true) the DROP DIES -- no landing, no clean stamp (+ a splash sound, not ported).
- The jpa: Sphere 5 (a point), rate 1 maxFrame 1 (ONE particle per emit), life 12 +-12.5 %, no velocity; Billboard 2.5 (125 u) scaling 0.8 -> 1
  by 25 %, 1 -> 2.25 from 25 % to the end, x (1 +- 0.35), a random angle; colour lerp(env -> prm, TEXC) with both keyed over the life (Merge,
  the 11-entry table): prm (255,255,207) @3 -> (255,207,63) @10, env (255,255,58) @3 -> (255,67,31) @10; alpha 1 until 42.5 % then -> 0;
  P_ms_hitmrk2_ia (IA4 64 x 64, the cartoon splat); blend SRCALPHA + INVSRCALPHA, z test on / no write.

### Fixed / ported
- `SmsNpcProfile.BurnWithLuggage` (default ON) + `BurnLuggagePart`: a `Burning` Pianta whose slots / extras / hand-placed parts carry no luggage
  gets the Luggage part (never two with a Random hand slot); the editor preview shows the bag when Burning is ticked (the export is a property
  now). Off = the bare code case. `WaterHitAtDrop` (DEVIATION knob, off = the game): the burning NPC's mark at the drop instead of its feet.
- `WaterHitMark.tscn` (gen_fx_npc.py wathit(), tex/hitmrk2_raw.png) + `SmsWaterHitMarks.cs`: a pool of 24 one-particle one-shot emitters under
  the tree root (Ensure at the burners' and the Gooble's _Ready: the shader compiles with the level), `Emit(at)` restarts the next one -- the
  spray lands tens of drops a second; a node per hit would churn. Emitted by SmsNpc.Sprayed (the burning branch, at GlobalPosition) and by
  Gooble.SmsSprayedByWater (at the drop, every hit, before the cooldown -- TSmallEnemy 547).
- `ISmsSprayTarget.SmsSprayedByWater` returns bool; SmsWaterDrops kills an accepted drop (move() 429) instead of landing it on the body.
  A held / just-released NPC and a dying Gooble return false (no hit pairs in the game).
- BUG (pass 98): fire_smoke.gdshader and glow_add.gdshader had no billboard -- the smoke puffs and the glow drew as quads flat in the emitter's
  XY, edge-on from most angles ("positions seem off"). Both are billboards now, rolled by the particle's angle (CUSTOM.x -- the smoke's +- 22.5
  deg and the mark's random turn, which the flat quads had also lost). The hit mark reuses fire_smoke.gdshader.

### Verified (drive 99, SmsF99, 12/12; drive 98 re-run 21/21 with its bare cases made explicit; build 0 errors; boot ALL OK, zero ERROR /
SHADER / WARNING lines)
L1 a Burning Pianta with empty slots wears exactly one Luggage and its fire joint is ef_null; L2 BurnWithLuggage off: no luggage, yashi_jnt;
L3 Burning + RandomOutfit at density 1: exactly one luggage; L4 a plain Pianta gets none; F1 the fire hangs on Fx_ef_null, none on the palm;
M1 the mark pool in the tree, 24 emitters; M2 10 fire-fighting hits -> 10 marks at the NPC's feet, each taken, strength 0.98; M3 5 hits on a
plain Pianta: taken, no marks; M4 the knob puts the mark at the drop; D1 a real drop onto a Gooble: one mark at its body's top, the drop
consumed that tick (0 left), D2 the dying Gooble takes no further spray; M5 the pool rotated once per mark.
Headless cannot draw: the bag's fire, the billboards, the splat's look and size are his to judge.

### Not ported / deviations (logged)
- The TSplashManager splash (30 % of accepted hits) and the sounds (MSD_SE_EN_COMMON_W_HIT_OK, the fire-fighting gate). The NPC blue-coin skip.
- WaterHitAtDrop (off) is the only deviation; a dead actor returning false lands the drop on it where the game's would fly through.

## SMS pass 99b -- the burning Pianta's water-hit mark on the backpack (DEVIATION at Jaysonn's word)

Jaysonn (10-09 00:20): "the yellow splat is at the feet of the pianta, I think it should be on the backpack area".

- The game: TBaseNPC::behaveToHitObject_ 351 emits PARTICLE_MS_ENM_WATHIT at `&mPosition` with flag 0 = TMarioParticleManager::emit ->
  createSimpleEmitterID at that point, a one-shot -- the NPC's position is its ground point, so every splat of a hosing stacks at its feet.
  Pass 99 shipped exactly that. His eye says the backpack; the enemies' convention (TSmallEnemy 547) is the drop's point.
- `SmsNpcProfile.WaterHitAtDrop` (bool) -> `WaterHitMarkAt` (enum WaterHitMarkPlace): **Fire** (default, the deviation) = the fire's position --
  the live NpcBurn root (the bag's ef_null, or the palm), read every hit, else the joint, else the body's top; **Drop** = where the drop hit the
  NPC's hit sphere; **Feet** = the game's own code. SmsNpc.WaterHitMarkPosition(hitPos) picks it; nothing else changed. No .tres carried the old
  bool (it was at its default), so the rename touches no scene or resource.
- Verified (drive 99, 13/13): M2 10 hits on the default burner -> 10 marks at the fire root, 1.90 m up and 0.61 m behind the feet (the bag);
  M4 Drop at the drop; M6 (new) Feet at the NPC's position; the luggage rule, the Gooble's drop-point mark, the consumed drop and the pool
  rotation as before. Build 0 errors; boot ALL OK, zero ERROR / SHADER / WARNING lines. Runbook rows (7b Burning, 7c Water Hit Mark At) updated.

## SMS pass 100 -- the NPCs' real textures (the Toad's cap, vest and eyes; the Piantas' eyes)

Jaysonn (10-09 01:22): "toad however looks not right. texture for head is off and jacket? eyes are fine. and he looks metallic?" (Ref/ToadRef.png:
a pink-red wash over the cap with no spots, an orange smear on the vest, white wedge eyes, a glossy sheen).

### Cause: the glb export's textures, not the shaders
The Godot materials came from FinModelUtility's glb, and its one-texture-per-material export picked the WRONG texture on kinopio_body:
- `_mat_cap` and `_mat_cloth` got `H_ma_polmask_sample_i4` -- the 32 x 64 POLLUTION GRADIENT (stage 0's mask texture), not the real masks
  `N_kinopio_cap` (I4 64 x 64: a black circle on white) and `N_kinopio_cloth` (a black border on white). NpcSkin.gdshader's mode 2 mixes two
  register colours by that mask (mix(C1, C2, TEXC), bmd stage 1), so a vertical gradient became a smooth spot-colour -> white wash: the "metallic"
  sheen and the missing spots on the cap, the orange -> vest-colour smear on the vest.
- `_eye_mat` got `H_toon_2_4i` -- the TOON RAMP (stage 0's texture), not `N_kinopio_me_open` (IA4: a black oval with a white glint on a
  transparent sheet). The ramp is 136..255 grey with alpha = intensity: white wedges where the eyes should be black ovals.
- The Piantas' `_eye_mat` images carry `I_mom_eye01_IA8` / `I_mow_eye01_IA8` with the alpha exact and the INTENSITY ^ 0x80 (every pixel 128 off:
  the pupil grey, the glint grey) -- a signed/unsigned slip in the exporter. Their body (`_hand_mat`) samples no texture, so it was never affected.
- Also found: the Toad glb DOES carry TEXCOORD_1 on body / cap / cloth / mouth (passes 93 / 95 said it had no second set); the bmd reads the
  pollution mask and the goop through TEX1 on all four.

### Ported
- `assets/Sms/Npc/tex/`: the bmds' own textures, decoded by bmd_mat_dump.py / bti2png: `N_kinopio_me_open`, `N_kinopio_cap`, `N_kinopio_cloth`,
  `N_kinopio_body`, `N_kinopio_kuti_close`, `I_mom_eye01_IA8`, `I_mow_eye01_IA8` (imports pinned lossless, mipmapped, no 3D auto-compression,
  no alpha-border fix: pin_npc100.py).
- `SmsNpcProfile`: a "Textures" group -- `EyeTexture`, `BodyMaterialTexture` (the cap mask), `ClothTexture`, `SkinTexture`, `MouthTexture`;
  SetupLook uses the slot when set, else the glb's albedo as before. `Toad.tres` carries all five + `PollutionUv2Materials` = the four materials;
  `PiantaMale.tres` / `PiantaFemale.tres` carry the eye.
- Nothing changed in the shaders: NpcSkin / NpcEye were right, they were fed the wrong textures.

### Verified (drive 100, SmsF100, 11/11; build 0 errors; boot ALL OK, zero ERROR / SHADER / WARNING lines)
T1 all five Toad surfaces on ShaderMaterials (nothing left on the glb's metallic StandardMaterial3D); T2-T5 each material samples its bmd texture;
T6 the four pollution materials read UV2; T7 the shipped PNGs are the bmd's (cap centre black / corner white, cloth the reverse, eye black
opaque centre / transparent corner); P1 the Piantas' eyes from the bmd; P2 no Pianta surface on a StandardMaterial3D; P3 the eye PNG's pupil I 72
opaque on a transparent sheet; P4 the Pianta body and leaves untouched. Headless cannot draw: the white cap with coloured spots, the coloured vest
with its orange trim, the black eyes with the glint are his to judge.

### Not ported
The Toad's BTP texture animations (5 eye sets: open / half / close / worry / wet; 7 mouths) and the Piantas' 8 eye sets -- the models show the
bmd's default (open eye, closed mouth); no blinking.

## SMS pass 101 -- the NPCs blink: the .btp texture pattern animations (eyes and mouths)

Jaysonn (10-09 01:52): "Port the blinking next".

### The game
- Every NPC anm kind pairs a bck with a btp (NpcInitAnmData.cpp sMonteM_ / sMonteW_ / sKinopio_AnmIndex -> TLodAnm::setBckAndBtpAnm ->
  MActor::setBtpFromIndex). The indices are per TYPE into MActorAnmData's lists, which it sorts by file name with an UPPERCASE compare
  (MActorData.cpp strcmp_ignore_case / to_upper_hack) -- so `moma_*` / `momb_*` ... sort BEFORE `mom_*` ('A' 0x41 < '_' 0x5F) and the plain
  Pianta's thirteen btps are indices 54..66 of 67 (the female's 26..38 of 39; the Toad's six 0..5). Resolved: a Pianta clip with a same-named
  .btp plays it (mom_appear / fall / happy / mad / madloop / recover / sitwet / throw / wait_yogore / wet / wet_a / wet_yogore), every other clip
  plays `mom_wink`; the Toad: wait / walk / run / step / sit -> `kinopio_twait`, talk / sittalk -> `ttalk`, happy / appear -> `twet`, fall /
  recover / wet / wet_a / wait_fall / sitwet / sitshake / stumble / wet_yogore -> `twet_a`, wait_yogore (+ the worried `ororo` clips) -> `twait2`,
  worry / sitworry -> `tworry` (sIndividualKinopioBtp remaps twait -> twait2, ttalk -> tworry for the worried kind).
- The .btp (J3DAnmTexPattern, read by btp_dump.py from the engine's J3DAnmTexPatternFullData layout): per material a texture index per
  frame (getTexNo clamps to the keyed range); attribute 2 loops, 0 plays once and holds. `mom_wink`: 90 frames looping -- eye01, then two quick
  blinks (eye02 / eye03 / eye04 at 5-11 and 17-23), open to the end. `kinopio_twait`: 150 frames, three blinks (open / half / close / half),
  the mouth closed; `kinopio_ttalk`: 100 frames, the mouth cycling close / half / open every ~10 frames under two blinks; `twet`: eyes open +
  mouth open (a shout, 3 frames); `twet_a`: wet eyes + wet mouth; `twait2` / `tworry`: the worry eyes and the worry mouths.
- The BTP's J3DFrameCtrl advances at rate 1 per frame (the NPC code sets the BCK's rate -- the walk speed -- never the BTP's); on load
  randomizeBckAndBtpFrame_ (NpcAnm.cpp 259, right after npcWaitIn) starts the clip at a random frame and the btp at the same frame when their
  lengths match, else at a random frame of its own -- so a crowd never idles or blinks in unison.

### Ported
- `SmsNpcTexPattern` (one .btp: name, the clips it plays with, frameMax, loop, the materials it drives, the frame table) and
  `SmsNpcTexPatternSet` (a kind's patterns, their textures, the default) -- [Tool, GlobalClass] Resources; `TexPatterns/{PiantaMale, PiantaFemale,
  Toad}.tres` generated from the disc by gen_npc_btp.py (13 / 13 / 6 patterns; 8 / 8 / 12 textures), the 24 new eye / mouth sheets in
  `assets/Sms/Npc/tex` (pinned like pass 100's).
- `SmsNpcProfile.TexPatterns` slot (Textures group); `SmsNpc`: `SelectTexPattern` on every clip start (the same pattern staying on keeps its
  frame, as the game's getCurAnmIdx check does), `TickTexPattern` in _Process (+60 frames a second, loop / hold), `ApplyTexPattern` sets the eye
  shader's `eye_tex` / the mouth's `base_tex` only when the index changes, `RandomizeClipAndPatternFrame` on the NPC's first clip (the bck too:
  Piantas no longer idle in lockstep -- the game's). `_matByName` keeps every override material by its bmd name.

### Verified (drive 101, SmsF101, 11/11; build 0 errors; boot ALL OK, zero ERROR / SHADER / WARNING lines)
S1 the three sets (13/8, 13/8, 6/12); S2 the Pianta table (own btp vs the wink); S3 the Toad table; S4 mom_wink's frames (eye01 0-4, eye02 5,
eye03 6-10, eye04 11, the second blink, eye01 to 89); S5 every idle NPC on its wait pattern (the dirty Toad on twait2); R1 three Piantas at
different clip positions AND wink frames; B1 / B2 the Piantas blinked within 2.5 s (only the wink's four sheets); B3 the Toad's open / half /
closed eyes with the mouth shut; B4 the dirty Toad's worry eyes + worryclose mouth; B5 the pattern clock at 60 frames a second (300.3 in 5 s,
three wraps of the 90-frame loop). Headless cannot draw the blink itself -- his eye.

### Not ported
The individual BTP remaps of the special Pianta kinds (MonteMA..MD's moma_*..momd_* sets) and the Toad's worried kind as a placement flag
(NPC_ACTION_UNK100 -- the clips are mapped, the flag is not); BTK / BRK material animations (none on these models).

## SMS pass 102 -- the talk's yes/no choice window ('me_4') and the Toad's worried kind

Jaysonn (10-09 02:21, after "The blinking looks right whats next"): the yes/no choice window, plus the worried Toad flag "since we can test the
yes and no text box with having toad worried then selecting yes to make him not worry and then no to keep him worried".

### The game
- The choice window is the talk layout's 'me_4' pane (message_2.blo, decoded like the balloon's): a 10 x 10 pane the code parks at (362, 187)
  turned -18 whatever the camera mode, holding the window 'bac4' (-48, -32) 106 x 74 (a J2DWindow of message_window.bti's corner, tint 0000ff
  alpha 160), the text box 'slct' (35, 15) 56 x 46 (the system font at 20 x 24, char space -2, line space 26, bound left / bottom; the box does
  NOT inherit the pane's alpha) and the marks 'sc_1' (-35, -18) / 'sc_2' (-35, 8), 20 x 20 message_cursor arrows (Talk2D2.cpp 480-560).
- A message's option tags (setTagParam group 1, tag 0 / 1: a 16-char string each) set the two options; the first met while unk214 is -1 becomes
  the highlighted one. When the page is complete the pane shows instead of the continue mark (725-790): it fades in 0x10 a step, the highlighted
  mark pulses like the continue mark (down 4 a tick to 0x60, up 2) and the text is rewritten with it -- the highlighted option white at the
  pulse's alpha, the other at 0x60. checkControler (810-842): up / down (MEANING_SELECT_UP / DOWN = the main stick's direction bit, set at
  |y| >= 0.5, or the D-pad, on the press edge; the game moves only from the OTHER option) move the highlight with MSD_SE_SY_E3_MENU_CURSOR; A or
  B confirm (B does not cancel) and go on as the continue mark would; closeNormalWindow hides the pane with the root; getSelectedValue hands
  the answer to the stage script at npcTalkOut; setMessageID resets unk214 to -1 (one answer per talk).
- The worried Toad is a placement flag (NPC_ACTION_UNK100, NpcInitPrg.cpp 248-281 -> setIndividualBck / setIndividualBtp): wait / sit / run /
  walk -> the fidgety `kinopio_waitororo` / `sitororo` / `runororo` / `walkororo`, talk -> `kinopio_worry`, sittalk -> `sitworry`; the blink
  `twait` -> `twait2`, the talking face `ttalk` -> `tworry` (TLodAnm's mIndividualBck / mIndividualBtp). Every one of those swaps is a .bck
  with loop attribute 2 on dolpic0's disc, like the plain clip it replaces.

### Ported
- `SmsTalkBalloon`: the `Choice` pane under the balloon (`Window` NinePatchRect of `tex/talk_window_x4.png` -- the disc's message_window.bti
  corner, x4 sharpened like the rest, margins 128 at scale 0.25, blue alpha 160; `Mark1` / `Mark2` the cursor arrows; `ChoiceText` in the
  system font; `Sfx/Cursor` slot), parked at the game's spot; a page carrying `<yesno>` (or `<s0=..>` / `<s1=..>`) shows it once complete,
  fades and pulses per the fade block, moves the highlight on `StickUpAction` / `StickDownAction` (`Lstick_up` / `Lstick_down`, read as
  strengths past 0.5) or `DpadUpAction` / `DpadDownAction` (`ui_up` / `ui_down`), confirms on A or B; `SelectedValue`, `ChoiceOption(i)`.
- `SmsNpc`: `LastChoice`, the `TalkChoiceMade(choice)` signal, `OnYes` / `OnNo` (None / StopWorrying / StartWorrying) applied at the talk's
  close; `Worried` (re-plays the current clip in the other variant when flipped); `SmsNpcProfile.WorriedClips` / `WorriedPatterns` with
  `WorriedClip()` / `WorriedPattern()`; `ActualClip` maps the asked clip to the worried swap in Play, `SelectTexPattern` the pattern; the
  loop table marks each swap as its plain clip (found by the drive: the worry talk clip stopped after one play and the Toad froze).
  `Toad.tres` carries the game's two swap tables; `Toad.tscn` ships Worried with the test script ("Th-the whole plaza is covered in that goop!
  I can't stop shaking!" / "Is it safe to come out now?<yesno>", OnYes = StopWorrying).
- `Toad.tres` is his editor's re-save after pass 101 (uids added, defaults dropped) plus the hunk -- committed as such.

### Verified (drive 102, SmsF102, 20/20; drives 101 11/11 and 87 16/16 re-run; build 0 errors; boot ALL OK, zero ERROR / SHADER / WARNING)
S1-S4 the tables, the ororo / worry clips in the glb, the worried idle (waitororo + twait2), the pane's nodes and texture; T1 the worried talk
plays kinopio_worry with tworry; C0 a tagless page: the continue mark, no window; C1 the question page: the window, 'Yes' highlighted, the mark
hidden; C2 the pane at (362, 187) +18 deg in both camera modes, the text riding it; C3 the 0x10 fade, the text box not inheriting it; C4-C7
down / down-ignored / up / down with the pulse's 4-down / 2-up and its 0x60 floor; A1 A on 'No': SelectedValue 1, LastChoice 1, the signal
once; A2 he stays worried (waitororo + twait2); A3 the pane hidden after the close; B1 a new talk resets the choice; B2 'Yes' highlighted again;
B3 A on 'Yes': 0 / 0 / the signal twice; B4 StopWorrying: the plain kinopio_wait with twait. Headless cannot draw the pane -- his eye.

### Deviations / not ported
The answer reaches the NPC (a signal and two inspector effects) instead of the game's stage script; MSD_SE_SY_E3_MENU_CURSOR is a slot; the
board window and TALK_C / D / E remain unported. Drive 101's plain Toad is now spawned with Worried off (Toad.tscn defaults to the test).

## SMS pass 103 -- the sign text: the board window ('message_board_1') and the board NPC kind

Jaysonn (10-11, after pass 102 "lets to the sign text talking too"): the readable signs' text, in the game's window.

### The game
- A readable sign is two objects: the sign MESH (a map object with collision) and, in front of it, an invisible board NPC (`NPCBoard`,
  `ACTOR_TYPE_NPC_BOARD`, NpcInitPrg.cpp 145-166: no model, LIVE_FLAG_UNK2000 | UNK10 | UNK8, the hit cylinder 80 / 160 from
  sBoardNpc_InitData, the spine at WaitMarioApproach, turn speed 0). Its perform is movementOnlyTalk_ + performOnlyDraw (NpcBase.cpp 598 /
  642: no bind, no hit, no water -- isBehaveToWaterNpc false); its talk accept cone is mSLSitTalkAcceptDegree (100: Mario in front of the sign,
  NpcBase.hpp 372); the talk camera is TALK_A (a board is not a small NPC). npcBoard.prm (his disc, params/npc): BodyHeight 180, CursorHeight
  5, LookatHeight 40.
- The talk opens the BOARD window instead of the balloon (TTalk2D2: unk28 = the NPC is a board, Talk2D2.cpp 387): message_board_1.blo's pane
  'mb_0' (296, 223) turned 10 holding the window 'back' (-155, -118) 320 x 246 at alpha 160 -- a J2DWindow of message_board_1 / _2's 32 x 100
  corners (TL _2 flipped x, TR _1 flipped x, BL _1 flipped y, BR _2 flipped y; contents (32, 100) 256 x 46; J2DWindow.cpp 170), the text box
  'text' (-124, -88) 257 x 186 (the system font at 20 x 26, char space -1, line space 32, bound centre / top), seven ruled lines 'li_1..7'
  (message_board_0, 256 x 3, alpha 60, 32 apart from -91) and the mark group 'cu_1' at (0, 142) ('cr_1' the blue ring, 'cc_1' the 18 x 18
  more-pages arrow, 'cs_1' the 16 x 16 last-page dot).
- The flow (Talk2D2.cpp 796-942, 1138-1226; BoundPane.cpp): openBoardWindow arms a TBoundPane slide from 80 below to the park over 25 steps
  (a quadratic Bezier with the control at the start; update() runs once idle before the arming, so 26 updates then the clamp), then shows the
  mark group at alpha 0; moveBoardWindow fades the group in 4 a tick and, at 255, zeroes the arrow and the dot and pulses the one for this
  page (arrow: more pages; dot: the last) 4 a tick between 0 and 255; checkBoardControler: A or B on a page with more to come -> eraseBoardWindow
  (the text 4 a tick down; at < 0 the next page is laid) -> appearBoardBoxWindow (4 a tick up to 255); on the last page A or B -> closeTalkWindow
  at once (the camera back, every NPC's npcTalkOut) while the pane slides 600 up over 60 steps. setupBoardTextBox: up to six lines a page, every
  0x1A tag skipped. Quirk kept: after a flip to the last page the arrow stays at the alpha the flip left it while the dot pulses -- nothing hides it.

### Ported
- `SmsTalkBalloon`: the board branch of TTalk2D2 1:1 -- the `Board` pane (hidden unless the talk's NPC is a board) with `Back`
  (`tex/talk_board_x4.png`: the two corner textures composed as J2DWindow::draw_private does, sharpened x4 like the balloon's), `Rule1..7`
  (`talk_board_rule_x4.png`), `Text` (J2DPrint's centred layout, integer x = (257 - round(width)) / 2, the first baseline at 26, 32 a line)
  and `Mark` (`Base` / `Arrow` / `Dot`); `BoardOffset` (the TBoundPane port: `BoundSet` / `BoundUpdate`), the states Opening / Open / Closing /
  Erasing / Appearing on the board path, `LayoutBoardPages` (six lines a page, tags dropped, a wider line broken by words when `AutoWrap`),
  `BoardLineWidth` (the font's kern + advance at 20 / Width, char space -1); `TickCount` (the balloon's 120 Hz ticks since it opened, for
  tests). `TalkBalloon.tscn` carries the pane.
- `SmsNpcProfile.Kind.Board` / `IsBoard`; `SmsNpc`: a board's tick is `TickBoard` (movementOnlyTalk_: the forbid counts, the talk accept, the
  spine -- no Move, no pollution, no effects, no water, no trample), the sit-talk cone for a board; `Profiles/Board.tres` (the prm heights; no
  clips, no shadow, no reactions); `SignBoard.tscn` -- an `SmsNpc` on it with collision layer / mask 0 and a disabled 80 x 160 capsule as the
  talk-cylinder gizmo; two sample pages sized to the game's box (the longest line 'Gateway to Isle Delfino,' = 252 of 257).
- Textures from his disc: game_6/timg/message_board_{0,1,2}.bti (bti2png, raw intensity -> alpha); `pin_talk103.py` pins them lossless.
- OUTFITS.md 7d "Signs": how to place one, the cone, the six-line pages, the tags dropped, the knobs that still apply.

### Verified (drive 103, SmsF103, 21/21; drives 102 20/20, 101 11/11, 87 16/16 re-run; build 0 errors; boot ALL OK, zero ERROR / SHADER, empty err)
S1-S3 the scene (kind, no model, no collision, the cylinder, two pages), the pane's nodes and geometry to the blo, the line width (252 / 571 for
30 W); C1-C4 the 100-degree cone (behind: no; 70 off: no; 40 off: talkable, the director's cursor on it) and the hose refused; T1 B opens the
board mode with the root hidden and TALK_A; O1-O3 the slide (0 on the arming tick, 80 -> 0 monotonic, 27 ticks), the park at (296, 223) / -10
deg, six lines up at 255 (the 4th empty); M1-M3 the mark group 0 -> 255 in 62 ticks, the arrow pulsing 3..252 with the dot at 0, the root
never shown; N1 the sign never turned or moved; P1-P2 the erase (255 -> 3 in 63, 0 on the flip) and appear (63) with page 1's three lines,
the dot pulsing and the arrow frozen at 167; X1-X3 the close: the camera back on the first Closing frame, the pane -600 after 61 updates, the
sign's talk forbidden while it slides, Mario freed in WAIT, talkable again after the forbid, the root back for the next NPC. Headless cannot
draw the plank -- his eye.

### Found by the drive, fixed before the commit
- My sample sign text was wider than the game's box (a 28-char line = 308 of 257): the sample was shortened, not the box.
- The drive's talk spot lay on his Level's goop quads: Mario stood in OIL_RUN and the director (SmsCanStartTalk wants WAIT) rightly refused the
  talk -- the drive now sets the sign on the floor it finds by a ray and cleans the paint there (SmsPollutionMap.Clean at the FLOOR height:
  a stamp at his spawn height missed the +-2 x 40-unit depth mask). The mechanics were right; the test was not.
- The drive first counted the fades in physics frames (60 Hz) and read them as half the game's ticks; it now reads the balloon's own
  `TickCount` (120 Hz).

### Deviations / not ported
A wider line is broken by words (J2DPrint breaks by glyph); the SY_SELECT_COMMON / SY_2D_OUT sounds are the balloon's existing slots; the
message comes from the NPC's `Lines` (the game's from the stage's message data); TALK_C / D / E (the sunflowers' and one Pianta's cameras) are
not the sign's and stay unported.

### Legacy
`Font/HudElements/SignPopup.{cs,tscn}` (his generic banner: "the 'Collect 8 red coins' style popup used by SMS for switches, secret-course
rules, and readable signs") is SUPERSEDED FOR READABLE SIGNS by `SignBoard.tscn` -- a sign now talks through the game's board window. It is
not removed: `RedCoinSwitch.cs` (`SignPopupScene`), `secrect_level.tscn` and `Rico4Secret.tscn` keep it for the switch's message (the game
shows that one through the talk system by stage script -- a later pass, not this one).

## SMS pass 104 -- the sign's model, the board window hidden until its slide, the page fade knob

Jaysonn (10-19 21:22, after pass 103): "the sign when reading the second time it will just appear then play the animation of it sliding
into frame"; "where is the sign model?"; "can we speed up the page fade? make it faster".

### The game
- The board NPC HAS a model, and it is the sign: `TBoardNpcManager::createModelData` (NpcManager.cpp 61-70) loads `boardNpc.bmd`, the
  NPC's init makes its MActor from it (NpcInitPrg.cpp 145-147) and `TLiveActor::performOnlyDraw` draws it (NpcBase.cpp 649). Pass 103's
  "the sign is a separate mesh + an invisible NPC" was WRONG -- corrected here (PROJECT_LOG 103, OUTFITS.md 7d). On his disc:
  `scene/<stage>/boardnpc/boardnpc.bmd` in every stage with a sign; one material `_mat_boradNpc` (the bmd's own spelling), one texture
  `K_boardNpc_US` (CMPR 64 x 64: wood grain, the Pianta-script glyphs), ONE TEV stage TEXC x RASC (alpha TEXA x RASA) over chan0 lit
  (matSrc VTX, ambColor0 128, lightMask 0x01 = the sun), cull back, opaque. 164 vertices: x +-77, y -17.8 .. 180.1, z -7.2 .. 28 units
  (2.4 m tall); the printed face is the model's +Z side, the back plain wood. `npcBoard.prm` mCircleShadowSize 60 (requestShadow; the NPC
  circle shadow is not ported). Its hit actor is HIT_FILTER_NO_ATTACK and its perform skips the hit checks: the game's board does not push
  Mario -- a solid post, where there is one, is the stage's collision.
- `TTalk2D2::perform` CUE_DRAW (Talk2D2.cpp 1350-1365) draws the board screen (unk10) in states 4-7 only: nothing is drawn through
  STATE_UNK2 / 3 (the camera wait and the 20-frame pause). (The switch also omits STATE_UNK8, the appear fade; Talk2D2.cpp is NonMatching
  in the decomp -- the port keeps drawing the plank through the fade.)
- eraseBoardWindow / appearBoardBoxWindow step the text alpha 4 a tick: 64 ticks (0.53 s) each way.

### Ported
- `SignBoard.tscn`: the `Model` child = `models/NPC/SignBoard/boardnpc.glb` (FinModelUtility's export; its texture equals the bmd's TEX1
  within CMPR decode rounding -- max 14 / 255, tex_compare.py) at the NPC transform (1 / 74.44, turned 180 so the printed +Z face is the
  node's -Z = the talk cone's side). `SmsNpc.SetupBoardLook`: the one material on `NpcPart.gdshader` in the bmd's form -- s0_form 1 (RASC
  alone), s1_form 2 (x TEXC), the vertex colour, ambient 128, the scene sun; `Profile.BodyMaterialTexture` overrides the texture, an empty
  `Profile.BodyMaterial` dresses every material. `Board.tres`: `BodyMaterial = "_mat_boradNpc"`, `CircleShadowSize = 60` (the prm's).
  `sign_imports.py` strips the glb's inherited import defaults (11.67 MB -> 1.2 KB) and pins the texture lossless.
- `SmsTalkBalloon`: the Board pane is hidden from `Open()` through WaitCamera / Pause and shown on openBoardWindow case 1's first update
  (the pane then at the slide's start, 80 below). Pass 103 showed it from `Open()` -- PARKED, at offset 0 -- for the camera's settle plus
  the 0.67 s pause, then case 1 jumped it 80 below and slid it up: "it just appears, then slides in", on every read (more visible on the
  second, when the eye is no longer on the camera move).
- `SmsTalkBalloon.BoardPageFadeStep` (export, default 8): the page flip's text fade per tick. DEVIATION at his ask -- the game's 4 (the
  const `BoardFadeStep`, still used by the mark group's fade-in and the arrow / dot pulse). 8 = 0.27 s each way. OUTFITS.md 7d rewritten.

### Verified (drive 104, SmsF104, 23/23; drives 102 20/20, 101 11/11, 87 16/16 re-run; build 0 errors; boot ALL OK, zero ERROR / SHADER, empty err)
Drive 103's 21 checks carried over, with S1 flipped (the model present) and T1 flipped (the pane NOT shown during WaitCamera / Pause), plus
S1b: '_mat_boradNpc' is the model's one material, on NpcPart.gdshader with s0_form 1 / s1_form 2 / vertex colour / ambient 128, textured
with boardnpc.png, the model 2.4 m tall and 2.1 m wide with its slab on the node's -Z side; O0: the pane never showed before Opening and
its first visible frame had it at 80 below; P1 reads the knob (8 a tick: ~32 ticks each way). Drive 103 is superseded by 104 (its S1 / T1
expect the pass-103 state). Headless cannot see the board's look -- the wood, the glyphs, the lighting are his.

### Deviations / not ported
BoardPageFadeStep 8 (the game: 4). The NPC circle shadow (60 u) is not drawn. The appear-fade draw question (STATE_UNK8) is left as the port
has it. No collision on the sign (the game's NPC has none either).

### Legacy
None retired beyond pass 103's note: `Font/HudElements/SignPopup` stays superseded for readable signs, kept for the red coin switch.

## SMS pass 105 -- the trample bounce keeps his momentum (NPCs, enemies, nozzle boxes)

Jaysonn (10-19 22:10): "can you fix bouncing on the nozzle boxes and npcs now? it seems i lose momentum when jumping on people".

### The game
- `TMario::trampleExec` (MarioCollision.cpp 135-175): from a JUMPING-type status (not DIVE) onto an actor that takes HIT_MESSAGE_TRAMPLE.
  A BROAD_JUMP goes to MARIO_STATUS_BACK_JUMP instead -- changePlayerStatus runs setStatusToJumping(BACK_JUMP) = the backflip's launch
  (BackJumpForce -18 along the face, BackJumpForceY 70), no step. Else the step animation escalates (ANIM_STEP1 -> STEP2, STEP2 / STEP3 ->
  STEP3, anything else -> STEP1), then changePlayerStatus(MARIO_STATUS_TRAMPLE) AND setStatusToJumping(MARIO_STATUS_TRAMPLE).
- `setStatusToJumping(TRAMPLE)` (MarioMove.cpp 588-603): setPlayerJumpSpeed(0.25, TramplePowStep1 / 2 / 3 by the anim) = mVel.y = 0.25 x
  mForwardVel + step, then mForwardVel *= 0.8. mario.prm ships 35 / 45 / 70. `changePlayerStatus` (MarioMove.cpp) returns 0 when status ==
  mStatus and otherwise runs setStatusToJumping for a TYPE_JUMPING status (TRAMPLE = 0x2000890 has the type) -- so the FIRST stomp of a chain
  launches TWICE (fwd x 0.64, vy = 0.2 fwd + step) and a stomp while already in TRAMPLE once (fwd x 0.8, vy = 0.25 fwd + step). The files are
  NonMatching in the decomp; the two calls are its code as written, ported as written.
- `TMario::trample()` (MarioJump.cpp 773): jumpingCommonEvents() -- checkBackTrig (the B dive), rocketCheck (the hover), considerJumpRotate
  (the spin) -- then jumpingBasic(MARIO_STATUS_SEC_JUMP_SLIP, mAnimationId, 3): doJumping's stick control (fwd x 0.999 a tick, the tiny
  accel), 1.0 / tick gravity, the ledge bit, the fall cancel (TRAMPLE carries FLAG_UNK2000000: a released A cuts the rise 0.75 a tick), the
  landing into SEC_JUMP_SLIP (a triple is next). The SAME air as a jump; the step anim is kept, so the chain lives in the animation.
- `TNozzleBox::receiveMessage(TRAMPLE)` (Item.cpp 1222): the box breaks and the same trampleExec bounces him -- one path for NPCs, enemies and boxes.
- `hitNpc` / `keepDistance` (MarioCheckCol.cpp 81, MarioPhysics.cpp 27): over an NPC he is still pushed out to its damage radius (10 while he is
  above it) + 50 each tick, the position only -- head-on over a tall Pianta with A released he hangs there until the arc clears its height; with
  A held the full bounce clears it at once. Unchanged, the game's.

### The bug
`Mario.cs` ran the `stomping` state in an early block of its own whatever UseSmsPhysics said: his legacy ApplyAirControl(AIR_SINGLE -- a
8.9 m/s cap hauled a 10.3 m/s bounce down in 0.16 s, a neutral stick dragged 8.9 m/s a second), the legacy gravity and its own landing. The
SMS air path (SmsAirTick) and the landing dispatcher never saw the state. Also: the second stomp used Step1 (Step2 never played); the chain
survived a dive or a landing; the broad jump's BACK_JUMP was missing; B could not dive out of a stomp with a stick held.

### Ported
- The early block runs for the LEGACY only. Under SMS a `stomping` frame falls through to the jump's path: SmsAirTick (doJumping + gravity),
  the airborne branch (dive / hover / hip drop / spin / throw), the landing dispatcher (justLanded via SmsStateSteers -> EnterLanding,
  jumpChainStage 2, the chain reset). A frame still standing on what he stomped (the box lid, vy > 0) only moves.
- `StompBounce` (SMS): the BROAD_JUMP -> `StartBackflip()` (SmsLaunch BackJump) with the flag dropped; else the chain by the previous STATE
  (stomping -> +1 capped at 3, anything else -> 1), `SmsLaunch(Trample1 / 2 / 3)` once, and a second time when the status changed (the
  double launch above). SmsLaunch carries the game's setStatusToJumping side effects (the face latch, the fall cancel, the paint stamp).
- The dispatcher: `stomping` leaves its exclusion list under SMS; the SEC_JUMP_SLIP branch resets the chain. The airborne B dive with a
  stick held accepts `stomping` (checkBackTrig). Legacy paths untouched (`StompBounce`'s else branch, the early block).

### Verified (drive 105, SmsF105, 13/13; drives 104 23/23, 102 20/20, 101 11/11, 87 16/16 re-run; build 0 errors; boot ALL OK, zero ERROR, empty err)
L1-L6 by direct calls (no frame between): fwd 30 -> 19.2 / vy 41 (the double launch), -> 15.36 / 49.8 (Step2), -> 12.288 / 73.84, -> 9.83 /
73.07 (STEP3 stays), another status -> 6.29 / 36.97 (STEP1 again, doubled), the broad jump -> backFlip at 18 backwards / 70, flag dropped.
T1-T3 live: a Pianta trampled from a 30-fwd fall, 0.5 m off its axis, A held -- x0.639 on the bounce frame, then twelve frames whose XZ
speed follows 0.999 a tick to within 0.00 % (the legacy drag is gone), vy falling 1.0 a tick, the landing through the dispatcher in a landing
state with the triple armed and the chain reset. B1-B4: a nozzle box (NozzleBox.tscn) stomped from a 10-fwd fall -- x0.639, ten frames at
0.999 a tick, the box broken (shape off, model hidden), the landing with the triple armed.

### Found by the drive, fixed in the drive
A jump written while Godot's floor flag was still up ran the grounded branch for a frame (WAIT, the run braked to 28): the set-up now lifts
him one frame, then writes the jump. Dead-centre over the Pianta with A released he hung on keepDistance until a second trample -- the game's
own rule; the test approaches 0.5 m off the axis and holds A. The port's SMS air brakes 0.999 a tick (JumpSpeedBrake) as the game does: the
expectation is that curve, not 'unchanged'.

### Deviations / not ported
None new. Still not ported: PARTICLE_MS_FUMI_B / C at half scale on an NPC trample (FUMI_A / B / C on an enemy), MSD_SE_MA_KICK_ENEMY, the
rumble.

### Legacy
His stomp block (`stateOfMario == MarioState.stomping`: AIR_SINGLE air control, GRAVITY, its own landing) and his STOMP_BOUNCE_VELOCITY
launch are RETIRED under UseSmsPhysics -- SmsAirTick, the airborne branch and the landing dispatcher do the job; both stay for the legacy mode.

## SMS pass 105b -- the trample bounce is a set height (no fall cancel)

Jaysonn (10-19 23:12): "is holding A supposed to effect it? i thought bouncing off people were set heights?" -- he is right.

### The game
`TMario::isFallCancel` (MarioPhysics.cpp 450-461): the 0.75-a-tick cut on a released A needs unk78 & 0x100. `setStatusToJumping`
(MarioMove.cpp 670-673) sets that bit for any status carrying MARIO_STATUS_FLAG_UNK2000000 -- TRAMPLE has it -- and `trampleExec`
(MarioCollision.cpp 178) clears it again right after the launch, for the TRAMPLE and for the BROAD_JUMP -> BACK_JUMP branch alike:
`unk78 &= ~0x100`. So the launch arms the cancel and trampleExec disarms it: a stomp bounce never shortens, whatever A does. Pass 105 had
routed StompBounce through SmsLaunch, whose pre-105 FallCancelEligible list named Trample1 / 2 / 3 as cancellable (a list that had never
mattered for a trample until then), and told him to hold A. Wrong on both counts.

### Ported
`StompBounce` clears `_smsFallCancel` after its launches (and after the BACK_JUMP's), as trampleExec's line does; SmsLaunch still arms it
(setStatusToJumping's rule, 1:1 in structure). No other change.

### Verified (drive 105b, SmsF105B, 14/14 = drive 105's checks with the cancel flag asserted OFF after every launch + T2b; build 0 errors; boot ALL OK, empty err)
T2b: a live Pianta trample with A RELEASED rose 215.1 u (2.89 m) from the bounce -- the game's arc for a 41-u launch is 41^2 / 8 = 210 u;
a cancelled rise would have been ~70 u. The XZ speed still follows the 0.999-a-tick brake (T2), the landing still arms the triple (T3), the
nozzle box unchanged (B1-B4).

### Deviations / not ported
None.

## SMS pass 106 -- the trample dust (ms_fumi_a / b / c)

Jaysonn (10-19 23:22, after "Bouncing feels right now"): "Do the stomp dust next".

### The game
`TMario::trampleExec` (MarioCollision.cpp 166-175), after the launch: on an NPC (`isHitCategory(HIT_CATEGORY_NPC)`) `SMS_EasyEmitParticle(
PARTICLE_MS_FUMI_B / C, &mPosition, this, scale 0.5)` -- emitAndBindToPosPtr + setGlobalScale(0.5): the puff and the hit marks at HALF size;
on anything else (an enemy, the nozzle box) `emitParticle(FUMI_A), (FUMI_B), (FUMI_C)` = gpMarioParticleManager->emit(id, &mPosition, 0,
nullptr): unbound, full size, the splash ring joining in. ms_fumi_a / b / c.jpa (MarDirectorLoadResource.cpp 292-294; jpa_dump.py; the JPA
engine in libs/JSystem/src/JParticle):
- a: 12 DirBillboard streaks (0.5 x 1.5 = 25 x 75 u) off a circle r 2.5..5 u, 5 up, 3.9 u/f straight up (velAxis 4 - velDir 0.1) x 0.8..1.2,
  life 15 +-25 %, no fields; ESP1 scale a straight run 1 -> 6 on both axes (in 0 / out 0; x 1 +- 0.4), alpha 1 until 50 % then -> 0, turned
  90 deg; colour lerp(env -> prm, I) with BOTH registers keyed over the life (Merge): prm white -> (255,255,207) with its alpha 255 -> 111,
  env (253,255,255) -> (255,111,0). P_msm_smash5_ia (IA8, the splash crown). A white flash going orange.
- b: ONE Billboard puff (2.0 = 100 u) on a circle r 30 u, 10 up, 22 u/f outward (within 22.5 deg) x moment 0.75..1, airResist 0.9 a frame,
  life 28 +-37.5 %; scale 1 until 37.5 % -> 1.7 (x 1 +- 0.375), alpha 1 until 12.5 % -> 0, a random angle +- 1/16 turn; lerp((26,26,2) ->
  (255,255,242), I). P_msm_kemuball3_ia.
- c: 8 Billboard marks (2.25 x 9.0 = 112 x 450 u) at the point, no velocity, life 9, Drag 0.96; colour = prm FLAT keyed over 6 frames
  (Merge over the life): white @1, (255,142,174) @3, white @5, (255,231,87) @6 -- a white / pink / yellow flicker; alpha 1 -> 0 over the life;
  scale 0.8 -> 1 by 24 % -> 0.5 (x 1 +- 0.3); a random angle, spin 0.01 half-turns/frame x (1 +- 0.4). P_msm_hitmrk1_ia (IA4). SSP1 children:
  one DirectionCross spoke per mark (0.8 x 7.0 = 40 x 350 u, two crossed quads along the velocity) flying 22 u/f +-0.2 out of the point, life
  24, scaleOut (-> 0), prm (200,240,255,183) env (0,150,255): sky-blue rays. P_msm_glow3_i.
Every emitter emits everything on its first frame (maxFrame 1), so the NPC case's bind to his position pointer moves nothing visible.

### Ported
- `assets/Sms/Fx/TrampleDust.tscn` (gen_fx_trample.py): four GPUParticles3D templates -- `Ring` (ms_fumi_a: DirectedPoints on the ring with
  up normals, align_y, the JpaDirBillboard construction turned 90 deg, the two colour ramps), `Puff` (ms_fumi_b: the fire_smoke billboard
  shader, airResist 0.9^age baked as a velocity_limit_curve), `Marks` (ms_fumi_c: the flat prm ramp, a spinning billboard), `Spokes` (the
  SSP1 children: DirectedPoints out of the point, align_y, drawn as the aligned cross with two draw passes). `trample_mark.gdshader`: the JPA
  TEV (lerp(env -> prm, I) or prm flat, both ramps by the life phase, alpha = A x prm.a x the fade) with three quad kinds -- a billboard
  rolled by the particle's angle + an offset, a DirBillboard, an aligned cross. Textures `tex/smash5_raw.png` + `hitmrk1_raw.png` (jpa_bake.py
  raw, pinned lossless by fx_tex_imports.py); kemuball3_raw / glow3_i already there.
- `SmsTrampleDust.cs`: `Play(feet, npc)` spawns each template as a top-level one-shot burst at his feet through SmsUnboundBurst (the pass-56
  rule: a child emitter streams a burst after a moving Mario), the copy's transform scaled 0.5 for the NPC case (the game's global scale
  scales the emitter's positions and quads alike), the Ring only for the enemy / object case; a `Sound` slot for MSD_SE_MA_KICK_ENEMY.
- `Mario.cs`: `_smsTrampleDust` adopted from under Mario in SmsSetupFx or, when his Player.tscn has no node yet, instanced from the scene (his
  scene is only edited with the editor closed; dropping TrampleDust.tscn under Mario takes over and exposes the Sound slot); `StompBounce`
  fires it after the launch, the NPC flag set by SmsHitNpcTrample (the Gooble's and the nozzle box's calls are the enemy / object case); the
  BACK_JUMP branch fires it too (trampleExec emits after either branch).

### Verified (drive 106, SmsF106, 18/18 = drive 105b's 14 + D1 / L7 / T1b / B1b; drives 105b 14/14, 104 23/23, 102 20/20, 101 11/11, 87 16/16; build 0 errors; boot ALL OK, zero ERROR / SHADER, empty err)
D1 the scene under Mario with Ring 12 / Puff 1 / Marks 8 / Spokes 8 as non-emitting one-shot templates; L7 six direct StompBounce calls (no
NPC flag) spawned 24 full-scale bursts at the air spot and none at half scale; T1b the live Pianta trample spawned Puff + Marks + Spokes at
scale 0.5 and NO Ring, at his feet; B1b the nozzle box trample spawned Ring + Puff + Marks + Spokes at scale 1, one each. The log's error
stream is empty and no shader line appears (the three new quad shaders compiled in the real scene). Headless cannot see the look -- his eye.

### Deviations / not ported
The NPC case is spawned unbound at the stomp point rather than bound to his position pointer (nothing visible differs: every FUMI emitter emits
on frame 0). The puff's cone of 22.5 deg is Godot's `spread` around the point's radial (the JPA's spread 0.25 x pi/2). The marks' Drag field
(0.96) is moot (no velocity). Not ported: MSD_SE_MA_KICK_ENEMY (the Sound slot), the rumble.

### Legacy
None retired: his `TriggerLandingDust` (LandingDustFx) is the LANDING's dust and stays; the trample had no dust of its own.

## SMS pass 107 -- Mario's goop particles: the footprints, the melt in water, the spin's fling (+ two corrections)

Jaysonn (10-19 23:57): "Do Mario's goop smalls (sink-in, drips, wash sparkle)" -- the first of his three picks (then the red coin switch's board
message, then the Mario leftovers). A correction to my own offer first: the sink-in ripple (ms_mari_rakuhamon / rakuball) was pass 95's, and the
drips / wash / sparkle are the NPCs' (ms_mnt_* / mare / kino, pass 95 too). What was still open on Mario's goop was the three particles the game
loads PER STAGE from /scene/<stage>/map/pollution/ (MarioParticle.cpp 31-54, TMario::initParticle; his disc has moved to
E:\FromC\Documents\FinModelUtility-main\...\extracted -- 79 stages carry the set; dolpic0's == bianco0's, airport0's differ):

### The game
- ms_m_ashios (MAP_POLLUTION_MS_M_ASHIOS 0x50), the dirty FOOTPRINT. checkGraffito's tail (MarioMove.cpp 1104-1118): within 4 cm of the ground, the
  footprint timer in its upper half, a legal ground plane, not in any water -> emitDirtyFootPrint = emitFootPrintWithEffect(ASHIOS, -1)
  (MarioParticle.cpp 599-664): in MARIO_STATUS_RUN, as the motion frame ctrl passes 38 the LEFT foot joint (jnt_foot_L), 8 the RIGHT, calcGroundMtx
  (MarioMove.cpp 1799) at the joint's translation: the ground under it + 18 u, X his right along the plane, Z his forward along it; the print emitted
  bound to a COPY of that matrix (emitAndBindToMtx: it stays). The jpa: ONE Direction quad 1.0 x 1.15 (50 x 57.5 u), trs (0,-11,0) -> 7 u over the
  ground, life 240; JPADrawExecRotDirectional (JPADrawVisitor.cpp 679): X = the seed axis (0,1,0) re-orthogonalised, Y = the direction (the nil
  velocity along the frame's Z), Z = X x Y, then "rotate 0.5 half-turns" about Y -> the vertical quad ROLLS FLAT, its texture V (the toe, the first
  row of P_msm_footmarkj_ia) along his forward. Scale 0.4 -> 1 over the first 9 frames then -> 1.5 by the end; alpha 1 until 37.5 % -> 0; blend
  src DSTCLR dst ZERO (GX_BL_SRCCLR as a SOURCE factor reads the destination) = a MULTIPLY into the ground, alphaCmp GREATER 29 (the fade eats the
  print from its soft edges in), z test no write; colour lerp(env -> prm, I) picked at RANDOM per print from a 3-entry table (two browns).
- ms_m_tokeos (0x12B), the goop MELTING off him in water. thinkDirty (MarioMove.cpp 1128): IN_ANY_WATER and within 200 u of the surface ->
  meltInWaterEffect (MarioParticle.cpp 671): while mDirty > 0, ONE emitter kept alive (emitAndBindToMtxPtr flag 1) on unk220 = his x / z at the water
  surface with his yaw (MarioMove.cpp 2057), setRate(mDirty x mMeltInWaterMax 0.7 / 255), setGlobalScale 0.6 in shallow water. The jpa: a ring
  13.1..15 u (Cylinder r 15 minRad 0.875 scaled 1 / 0.1 / 1), the cylinder's AXIS velocity = the RADIAL (JPAEmitter.cpp 560): 2.5 u/f outward x (1
  +- 0.3), airResist 0.99, life 80 +-40 %; a 40 u Direction quad rolled flat along the drift (as above), X 0.7 / Y 1.7 -> 1 by 36.65 % -> 0.3; alpha
  0 -> 0.875 by 12.5 % -> 0 from 50 %; colour RANDOM over 6 (4 pollution-brown, 1 half, 1 pale); P_msm_osenball_ia.
- ms_m_spinos (0x51), the goop FLUNG OFF in a spin. emitBlurSpinJump (MarioParticle.cpp 574) every frame of a rotate jump (MarioJump.cpp 1006) and
  of the ground spin (MarioRun.cpp 779): while mDirty > 0, an emitter kept alive on getCenterAnmMtx(), setRate(1.75 x mDirty / 255). The jpa: a ring
  21..24 u, +-60 u tall about the centre joint, 4 u/f along the position + 15 u/f radially x (1 +- 0.2), Gravity 1 u/f^2 fading in over 50 %,
  airResist 0.99, life 15 +-40 %; a 25 u DirBillboard along the velocity, X 0.8 / Y 3.0 -> 1 by 50 % -> 0.1; alpha 1; alphaCmp GEQUAL 190, z write;
  colour RANDOM over 6 (3 brown, 1 half, 2 pale).

### Ported
- assets/Sms/Fx/GoopMario.tscn (npc82/goop107/gen_fx_goop.py): FootPrint (a one-shot template spawned as an unbound burst at the ground frame --
  pass 56's rule), Melt and Fling (continuous emitters moved every tick to the game's matrix, their particles in the world, the game rate as
  GpuParticles3D.AmountRatio). goop_footprint / goop_melt / goop_fling.gdshader: one body, three render modes (blend_mul + no z write; blend_mix + no
  z write; blend_mix + z write): the per-axis ESP1 scale by the life phase (INSTANCE_CUSTOM.y), the random colour index from a black -> white
  color_initial_ramp (COLOR.r), the alpha curve in COLOR.a, three quad kinds (flat in the particle frame; flat along the velocity -- Godot's align_y
  puts the vertical in Z, so the plain quad lies flat; a DirBillboard). tex/footmarkj_raw.png (jpa_bake.py raw, pinned). SmsGoopMario.cs:
  SpawnFootPrint(groundFrame), MeltSet(surface, rate, shallow) / MeltOff, FlingSet(centre, rate) / FlingOff, RateToAmountRatio.
- Mario.cs: `_smsGoopFx` adopted from under Mario in SmsSetupFx or instanced from the scene (his Player.tscn: editor open -- drop GoopMario.tscn under
  Mario to own it); SmsFootPrintTick completed (the timer's upper half, a floor, no water) -> SmsEmitDirtyFootPrint: the gait states, the state
  machine's play position folded into the gait clip (it counts past the loop), checkPass on 38 / 8 (60ths), the foot joint, a ray to the ground,
  the frame from the normal and his face; SmsThinkDirty's water branch keyed on `_smsInWater` (ANY water -- it was the swim state) with the melt
  (feet within 200 u of the surface; shallow = wading); the fling through isSpining + SmsGoopFxTick every physics frame, gated on the SpinJump /
  groundSpin states (the port's isSpining flag also serves the roll blurs and a landing leaves it up).
- SmsPhysics.cs: Graffito.FootPrintTimerMax 520 -> 250 and Dirty.MeltInWaterMax 0.7 (below).

### Corrections
1. Pass 65's `FootPrintTimerMax = 520 "not in any .prm"` was wrong: /Mario/Mario.prm SHIPS mFootPrintTimerMax = 250 (sms_shipped_params.json
   params/mario/mario.prm/13; TDeParams reads that file, MarioInit.cpp 466). The footprint window (and the jump stamp's) is 125 ticks, not 260.
2. Pass 106's trample puff: its airResist 0.9 was baked as a velocity_limit_curve of 0..1 -- Godot's curve is an ABSOLUTE max speed in m/s
   (particle_process_material.cpp 1160: min(|v|, curve)), so the puff was clamped to <= 1 m/s and crept instead of its 22 u/f (17.7 m/s). The curve
   is now 17.7 x 0.9^age m/s (gen_fx_trample.py drag_curve); TrampleDust.tscn re-shipped. The melt's curve was baked in m/s from the start.

### Verified (drive 107 SmsF107, 18/18; 106 18/18; 105b 14/14; 93 30/30; 95 22/22; 98 21/21; 99 13/13; build 0 errors; boot ALL OK; .err empty, no shader lines)
D1-D6 the scene under Mario (1 / 36 / 18, none emitting), the quad 0.672 x 0.772 m on goop_footprint quad_kind 0 alpha test 30/255, align_y + gravity
on the fling, the melt's limit curve 2.62 m/s -> 1.17, the trample puff's 17.73 m/s, the parameters 250 / 0.7, jnt_foot_L / R found (11 / 6). R1-R5 a
dirty sprint (32 u/f, the gait clip 0.983 s) left 11 prints in 2 s, 5 left / 6 right, all 10 consecutive pairs on opposite sides of his facing, every
one flat 5..9 u over the floor, within 1.2 m of him with its toe along his facing, none once the window's lower half began. S1-S3 a spin jump at
dirty 150: the fling emitting on the centre joint (3 cm) at AmountRatio 0.829 (1.75 x 144 / 255 of the stream), mDirty down 0.5 a tick; closed the
frame after the landing; a clean spin flings nothing. W1-W3 dropped into the plaza's Sea (surface y -19.522): the melt at the surface under his x /
z, ratio 0.902 (150 x 0.7 / 255 of the stream), scale 1 swimming; mDirty 150 -> 113 in 60 frames and the ratio following; closed out of the water.
Headless cannot see the prints or the blobs -- his eye.

### Deviations / not ported
The fling's airResist 0.99 is a linear damping of the same mean loss (a clamp would fight its gravity); its Gravity field's fade-in is an equivalent
constant (the same end displacement). The melt's clamp lets a slower blob keep its speed a few frames before the curve passes it. The ground spin
emits only while the port is in its groundSpin state. Not ported: the walk-sand / wet-ground running ripple (other particles of the same hook).

### Legacy
None retired: his LandingDustFx is the landing's; the water branch of SmsThinkDirty was mine (pass 65) and is corrected, not replaced.

## SMS pass 107b -- the footprints and the melt were invisible (and 106's hit marks and spokes): POSITION on every shader path

Jaysonn (10-20 02:07): "the spin fling is the only one i see working otherwise nothing else is it seems?"

### The cause
Godot gives a spatial shader that writes POSITION ANYWHERE the whole-shader define OVERRIDE_POSITION (scene_shader_forward_clustered.cpp 839:
`usage_defines["POSITION"]`), and the vertex stage then does `gl_Position = position` on EVERY path, the default `projection x vertex` skipped
(scene_forward_clustered.glsl 377 / 522; `position` starts as vec4(1.0)). goop_*.gdshader's DirBillboard kind (the fling) writes POSITION; its two
flat kinds (the footprint, the melt) only moved VERTEX -- so they drew at vec4(1.0): nowhere. The same shape sat in pass 106's trample_mark.gdshader:
the ring's DirBillboard branch writes POSITION, the marks' billboard branch and the spokes' aligned-cross branch do not -- the hit marks and the blue
spokes have been invisible since 106 (he saw the ring and the puff). Headless cannot catch this: the quads compile fine and simply land off-screen.

### The fix
Every branch of goop_footprint / goop_melt / goop_fling.gdshader and of trample_mark.gdshader writes `POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX
* vec4(VERTEX, 1.0)` after its own VERTEX / MODELVIEW_MATRIX work (gen_fx_goop.py, gen_fx_trample.py). Also: the footprint's ground frame is now
right-handed (X = normal x forward, as the game's calcGroundMtx: X x Y = Z) -- it was a mirror, which cull_disabled hid.

### Verified (drive 107 18/18, 106 18/18 on the new Mario.cs; build 0 errors; boot ALL OK -- the drives see nodes, not pixels: the look is his)

### Also raised (10-20 02:08): "the goop on npc is just brown? it should be whatever goop they are in"
The game's rule is PER STAGE, not per goop: an NPC's goop skin is the stage's one /map/pollution/h_ma_rak.bti (Delfino's is brown; the port carries it
as assets/Sms/Npc/tex/H_ma_rak.png) and its drip / wash / sparkle colours come from SMSSetEmitterPolColor's per-MAP table (EmitterViewObj.cpp 644:
Delfino's row (181,132,102) / (98,14,0); Sirena's pink is a different row). The port's goop LAYERS do differ per type (BrownGoop / FireGoop /
ElectricGoop textures, pass 65). Making an NPC wear the goop it sank in is a DEVIATION the game never had; offered as a knob (SmsNpcProfile.GoopFromLayer)
for his word.

## SMS pass 108 -- goop from the layer: several goops in one stage, and the characters wear the one they got dirty on (DEVIATION)

Jaysonn (10-20 02:08 / 13:59): "the goop on npc is just brown? it should be whatever goop they are in" -- "lets make it so we can have multiple goop
in a stage and have that reflect on the chars".

### The game
One goop look per stage LOAD. TNpcManager swaps every NPC bmd's dummy for the stage's one /map/pollution/h_ma_rak.bti (NpcManager.cpp 38); the NPC
drips and wash, Mario's melt-in-water and spin fling are coloured by SMSSetEmitterPolColor's per-MAP row (EmitterViewObj.cpp 597 / 644; Delfino: prm
(181,132,102) over env (98,14,0)); Mario's footprint jpa (ms_m_ashios) is loaded per stage. What a patch DOES is per layer (its type); what the goop
LOOKS like on a body never asks the layer. Each stage was one villain's mess, so it never needed to.

### The deviation
The port's layers each carry a type and a texture (SmsPollutionLayer.Type / GoopTexture / DefaultGoopTexture: BrownGoop / FireGoop / ElectricGoop), so a
level can mix goops -- and with this pass the characters follow the layer they got dirty on. The rule (SmsPollutionLayer.CharacterGoop):
* a brown layer with nothing of its own keeps the stage default (H_ma_rak, Delfino's row) -- every look confirmed so far is unchanged;
* a Fire / FireRegrow / Electric layer, or a layer with a GoopTexture set, dresses the character in that texture and in two colours measured from it
  (the mean of its brightest quarter = the light / prm colour, of its darkest quarter = the dark / env);
* the layer's new `GoopLightColor` / `GoopDarkColor` exports name the two colours outright (alpha 0 = measured); set on a brown layer they recolour
  the particles and keep the stage skin.
Knobs, both default ON: `SmsNpcProfile.GoopFromLayer` and `SmsGoopMario.GoopFromLayer`; off = the game.

### Where it lands
* SmsPollutionLayer: `EffectiveGoopTexture`, `DressesCharacters`, `CharacterGoop`, `MeasureGoopColors` (cached per texture; the pixels from the texture's
  image or its source file), the two colour exports; SmsPollutionMap.PollutedLayerAt (the first layer with PAINT under a point).
* SmsNpc: at the sink start (PollutionCheck) `WearGoopOf(PollutedLayerAt)` puts the layer's skin into every skin / leaf material's `pol_tex` and keeps
  its colours; an NPC placed dirty reads the layer under its feet on its first tick; the body / foot drips and the wash spawn through SpawnGoopFx ->
  SmsNpcEffect.SetGoopColors: a per-instance MaterialOverride copy of the goop-ball material with the layer's prm / env (the scene's shared material is
  never edited; the sparkle, burn and sink ripple carry no such uniforms and are left alone).
* Mario (thinkDirty, every tick on paint): `SmsGoopMario.SetGoopLayer(PollutedLayerAt(feet))` -- a change rewrites the footprint / melt / fling materials'
  colour tables in a MaterialOverride: prm0 / env0 = the pair, prm1 / env1 half-way to the pale top row, which the melt and the fling keep (the game's
  PolColor writes the goop rows of a 6-row table); the footprint's table is all goop, so it becomes the pair. A footprint burst spawned while dressed
  carries the override (Duplicate copies material_override). Mario's own dirty tint stays the game's fog colour (code-fixed, not per stage).
* The runbook (OUTFITS.md 7c): the `Goop From Layer` row, the colour paragraph.

### Verified (drive 108, 26 checks, on his machine; regressions 107 / 95 / 93 / 106 / 102 / 87; build 0 errors; boot ALL OK)
Four layers in a strip (brown / fire / electric / brown-type with set colours): PollutedLayerAt names each; CharacterGoop false on the brown, FireGoop /
ElectricGoop on the hot ones with colours measured from the images (the image's mean luminance between dark and light; fire and electric differ), the
set pair on the fourth with no skin. Six NPCs: the brown sinker keeps H_ma_rak + Delfino's row; the fire sinker wears FireGoop.png on every skin / leaf
material with the fire pair; the electric one ElectricGoop.png; the knob-off Pianta on the fire layer keeps the stage look; the set-colour layer's Pianta
keeps H_ma_rak with the set colours; a Toad placed dirty on the electric goop wears it from its first tick. Out of the goop and dripping: the fire Pianta's
body and foot drips wear MaterialOverrides with the fire prm / env, the brown one's drips none; its wash emitter too. Mario: on the set-colour layer his
three emitters are dressed (the melt's prm0 / env0 the pair, prm2 / env2 the baked pale, prm1 half-way; the footprint's whole table the pair; the baked
materials untouched), a footprint burst carries the override, the brown layer drops the overrides, the knob off dresses nothing.
Headless cannot see colours drawn: the look is his -- the fire / electric Pianta skin, the recoloured drips, the footprints behind a run off coloured goop.

### What the drive caught (all on the test side; the port's code is unchanged from the staged bytes)
The first run was 18 / 26: two Piantas (the fire one, the knob-off one) 0.9 m apart on one 3 m layer -- their 1.34 m capsules shoved both off it, so
neither sank; Mario bounced on the Level's spawn NPC through the setup and landed in oilRun on the Level's own Goop7 (its paint was cleaned at his
AIRBORNE height, so the clean missed the floor); the brown layer was emptied at the climb-out and never repainted for Mario's visit; and Mario's spot
on the brown layer sat 1.7 m back of its centre, where the texel grid (anchored at the low edge) leaves ~0.2 m of the high edge unsurfaced, so a
Slip sample's 0.43 m ring read clean. Fixed in the drive: 4.5 m layers 5 m apart with the floor read per layer, bodies 2.4-2.8 m apart, Mario parked
on cleaned floor (its paint cleaned at the floor's height) with the boot dress reset, the brown layer re-stamped alone before his visit, his spot
1.3 m forward of the centre. Note for Level authors: his Level's Goop7 already carries a GoopTexture (PinkGoop), so with this pass a character dirtied
there wears pink -- the spawn sits on it.
Drive 95's D2 (the first drip at the clip's frame 28) was a chance check since pass 101's random first frame: it now accepts any check frame.
One boot ended in a Godot C# exit-time finalizer crash (Godot.Collections.Array.Finalize after the native side tore down; .err only, after ALL OK);
the re-run exited clean. Not seen before; watch for it.

### Not done
The Gooble's brain mark (H_ma_rak, per stage in the game as well) stays the stage's; the sink ripple is a screen refraction and has no colour of its own.

## SMS pass 109 -- the red coin switch's message through the BOARD window (the game's stage-script flow)

Jaysonn (10-20 20:23): "yeha do both the red coins switch and mario leftovers stuff."

### The game
TRedCoinSwitch::receiveMessage(HIT_MESSAGE_HIP_DROP) (MapObjTown.cpp 553): the BCK "redcoinswitch", the pad disabled for (clip end x 2 + 60)
frames (TMarioGamePad::mDisabledFrames), MSD_SE_OBJ_AP_BUTTON, removeMapCollision + HIT_FILTER_NO_COLLISION, state 2; control(): the clip ends ->
state 3 and the flag MSF_RED_COIN_SWITCH_PRESSED. The rest is the ex stages' stage script `map/sp/timerredcoin.sb` (an SPC binary; disassembled
with tools/projectshine/spc_dis.py written from TSpcInterp, src/Strategic/spcinterp.cpp): at load it hides the stage's invisible board NPC
("見えない看板ＮＰＣ", type NPCBoard, setHide4LiveActor); on the flag it loops `warpFrontToMario(board); if (forceTalk(board)) { talkAndClose(30);
appear8RedCoinsAndTimer(); startBGM(..); appearRedCoinCounter(); break; } yield;`. evWarpFrontToMario (EventWatcher.cpp 1221): 400 units ahead
along Mario's angle, at his height, facing him. __forceStartTalk (NpcEvent.cpp 90) takes only when not in talk / demo mode, isMarioTouchGround4cm
and no JUMPING-type status -- the script yields and retries. setTalkMsgID(30, 1): an NPC_BOARD speaker = the BOARD window (Talk2D2.cpp 387);
message 30 is /common/2d/sys_message.bmg "Collect 8 red coins / before the timer / runs out! / / GOOD LUCK!" (bmg_dump.py); flag 1 =
closeTalkWindow, and for message 30 the close plays MSD_SE_SY_RACE_START instead of the select tick (Talk2D2.cpp 479 / 944); the script waits for
!isTalkModeNow (the window gone, Mario freed), then the 8 red coins appear (with their sparkle), the console timer and the director's timer
start, and startAppearRedCoin brings the red coin counter in at 0.

### The port
* Mario: `SmsForceStartTalk(npc)` -- the director's pending forced talk (SmsDirectorTalkCheck runs it before the cursor search): the game's three
  conditions checked every frame, then the NPC's WarpFrontOfMario + StartTalk (READ_BILLBOARD as for any talk); `SmsCancelForceTalk`,
  `SmsForceTalkPending`. `SmsDisablePad(frames)` -- the pad's dead frames: the stick reads neutral at its one read and A buys no jump (the
  jump buffer's fill is gated: a dead pad has no edges). R / Z / X are read where they are used and are NOT gated (a logged gap; the exposure is
  the press clip's 0.2 s, then the talk owns him).
* SmsNpc: `Hidden` (LIVE_FLAG_HIDDEN + HIT_FILTER_NO_COLLISION: the model not drawn, no collision, no cursor; DEVIATION: the proximity talk is
  refused too -- the game's hidden board keeps it at its placement, but here the board sits under the switch where Mario stands);
  `WarpFrontOfMario`; `TalkCloseSound` (the per-NPC close-sound slot; the game keys it on the message id).
* SmsTalkBalloon: the close plays the speaker's TalkCloseSound when set (else the select tick); `LastCloseSound` for the drives.
* SmsStandardHud: `AppearRedCoins()` (appearRedCoinCounter: the counter at 0 at once; it used to wait for the first coin), `RedCoinsVisible`.
* RedCoinSwitch.cs (his): Trigger = the game's receiveMessage (the clip, SmsDisablePad(clip x 2 + 60), PressSound, the SwitchBody's shapes disabled
  -- deferred: Trigger runs inside the HitZone's signal, the physics flush refuses a direct write); the clip's end sets `Pressed`; TickMessage is the
  script: forceTalk(board) once, wait while pending, wait while talking, then BeginRedCoinChallenge (his coins + his ShineTimer + the HUD's
  AppearRedCoins). The board: `Message Board Path`, else SignBoard.tscn instanced hidden under the switch with MessageText as its page and the
  `Race Start Sound` slot as its close sound; `Press Sound`; `Pad Disable Extra Frames` (60). The Mario who pounded it is kept for the message
  (`_lastHitter` clears when the pound ends). Without SMS physics or a board the challenge starts without a message.
* Runbook (OUTFITS.md): 7b rows `Hidden`, `Talk Close Sound`; a 7e section for the switch.
LEGACY RETIRED: Font/HudElements/SignPopup for the red coin switch (`SignPopupScene` / `SignLayerPath` stay as unused exports so secrect_level.tscn
and Rico4Secret.tscn load clean; the class itself is untouched). Not ported: the coins' kill-by-timer and their appear particles, startBGM, the
switch's 120-frame state 3 (no effect), the pad disable's R / Z / X.

### Verified (drive 109, 19 checks, on his machine; regressions 104 / 102 / 87 / 101 / 108 / 106 / 105b green; build 0 errors; boot ALL OK)
His red_coin_switch.tscn instanced on the test floor with red_coin.tscn and eight markers: the hidden board under it (Board kind, model off,
CanTalk off, MessageText as its page, the RaceStart slot as its close); B in front of the hidden board opens nothing; a hip drop onto it triggers
it (the SwitchBody's shape off, the flag at the clip's end), the pad dead for exactly (6.2 x 2 + 60) frames with the stick held right reading
neutral and A buying no jump; the board warped 400 u ahead facing him to the millimetre and the talk started (pending seen first); the board
stayed hidden; no coins before the close; the BOARD window with the five lines; the close played RaceStart; the challenge began after the talk had
fully ended (closed at frame 151, began at 183); 8 coins; the HUD's counter at 0; the pad's disable ran out after; a forced talk asked in the
air waited 8 airborne frames and took on landing; a second board without the slot closed with the select tick. Headless cannot see the plank or
the camera: the look is his.

### Caught by the drive (test-side and port-side)
The first run leaked A through the dead pad: the swallow flag clears on the release frame and Godot's "just pressed" for a scripted press lands
after it -- the jump-buffer fill is now gated on the disabled ticks (port). The switch forgot its Mario when the pound ended (`_lastHitter` is a
per-pound latch), so the message never ran, or the challenge began during the talk -- the switch keeps the pounding Mario for the message (port).
`cs.Disabled = true` inside the HitZone's BodyEntered was refused by the physics flush -- SetDeferred (port). The boot's landing on his Level's
Goop7 left the footprint window open, so leaked jumps stamped goop on the strip -- the drive resets the timer at setup (test).

## SMS pass 110 -- the Mario leftovers: the oil run's held squat, the upper-body damage run (and two "gaps" that were not)

Jaysonn (10-20 20:23 / 21:42): "do both the red coins switch and mario leftovers stuff" -- "go on with 110".

### Verified first, against the decomp
Of the five items on the list, two were not gaps and one belongs elsewhere:
* The hip drop under a fence roof never hangs Mario. hipAttacking (MarioJump.cpp 1096) calls jumpProcess(0), and checkGroundAtJumping returns the
  fence-roof code 4 only when its argument carries bit 2 (MarioPhysics.cpp 393) -- jumpingBasic and rocketing pass 2, the hip drop does not. The
  port's SmsRoofHangCheck already covers the rocket's rise. The Mario.cs comment that called this "not ported" is corrected.
* The wire / wire-hang cameras ARE ported (SunshineCamera.cs 557, pass 66y: kindWire / kindWireHang).
* The fruits bouncing off Mario (TMapObjBall::boundByActor / kicked, MapObjBall.cpp 184-330) is a map-object physics system with per-fruit tables --
  its own pass (112).

### Shipped
* The oil run's squat (MarioRun.cpp 1280-1284, oilRunning): with the stick centred, L held (mInput & TMarioGamePad::L) plays ANIM_SQWAT at 0.5,
  else ANIM_WAIT at 0.5 -- the port played ma_wait either way. Now `SmsSquatLHeld()` -> ma_sqwat. (A STANDSTILL on paint is the game's own
  OIL_RUN -> WAIT loop each tick (pass 67), so the squat shows while he still slides with the stick released.)
* The type-3 upper-body damage run (MarioCollision.cpp 316-319: damageAnimType 3 while RUN / OIL_RUN -> setUpperDamageRun; MarioDraw.cpp 1854:
  bck 0x4A "dgrun" on the upper track from its start at rate 0.5, mUpperState = UPPER_STATE_FIXED_ANIMATION; MarioUpper.cpp 131: back to IDLE
  when the clip completes; addUpper leaves a FIXED animation alone, so the pump / carry upper clips yield meanwhile). The legs keep running;
  no knockback, no camera shake. Port: an SmsUpperPose graft of ma_dgrun on the chn_chest subtree (the same modifier the swim pump uses),
  started by the type-3 branch of SmsDamageExec while running / sprinting / walking / in the oil run, advanced at 0.5 x from SmsTakeTick, off
  when the clip completes or a knockdown / the water / a talk / a carry takes the body. `SmsUpperDamageRunOn` / `SmsUpperDamageRunTime`.
  The goop's HP tick (floorDamageExec, type 3) is the one type-3 source in the port today.
LEGACY RETIRED: none (two stale "not ported" comments corrected).

### Verified (drive 110, 8 checks, on his machine; regressions 109 / 105b / 106 / 107 / 102 green; build 0 errors; boot ALL OK)
The graft on the skeleton (16 bones under chn_chest, weight 0 idle); a run on painted SLIP goop is the oil run (ma_run2 pedalling); the stick
released with L held while sliding at 3.8 m/s -> ma_sqwat at 0.5; L released -> ma_wait at 0.5; a type-3 hit while sprinting: the fixed animation
on from frame 0 at weight 1, HP 8 -> 7, the state still sprinting; it ran 57 frames (the 0.467 s clip at 0.5 = 56 at 60 Hz), the time monotone,
the weight back to 0 at the end; the legs never left RUN; a type-3 hit while standing starts nothing. Headless cannot see the pose -- the look is his.

### Caught by the drive
The first run's squat checks were mis-written on my side (a standstill on paint, where the game itself leaves the oil run each tick); rewritten to
slide. The port's code was right both runs. Run 2 was cut by the network (his PC dropped at 21:58 with the drive installed in Mario.cs);
resumed at 22:26 from the recorded state.

## SMS pass 111 -- Mario's goop skin: TMario::addDirty, the dirt on his body and hands

Jaysonn (10-20 21:42): "also mario is not being covered in goop? did we implement that?" -- no; the port had only the yellow fog. Now it does.

### The game
TMario::addDirty (MarioDraw.cpp 2139-2163, every frame from MarioMain.cpp 136) writes mDirty (0..150, thinkDirty's amount) into the TEV konst 0
ALPHA of every body material and every hand material. ma_mdl1.bmd (all 11 materials) and ma_hnd2l.bmd (bmd_mat_dump.py) run the same chain:
  stage 0: (TEXA, KONST, ONE, ZERO) COMP_GR16_GT -> REG2, texmap1 = the pollution MASK (H_ma_polmask1_i4 64 x 64 on the body, H_ma_polmask_hand_i4
           32 x 64 on the hands) through TEX1:  REG2 = mask > mDirty ? 1 : 0   (1 clean, 0 goop)
  stage 1: (ZERO, TEXC, C2, ZERO):  the body texture where clean
  stage 2: (TEXC, ZERO, C2, CPREV), texmap2 = H_ma_rak_dummy (8 x 8, swapped at load for the stage's /map/pollution/h_ma_rak.bti, as the NPCs')
           through TEX2:  + the goop texture where not clean
  stages 3-4: the toon / spec lighting on the result
A per-texel SWAP of the albedo for the goop texture, keyed by the mask against mDirty -- the NPC skin's chain (pass 93) on Mario. The body mask
runs 51..255 (mean 147): nothing shows under mDirty 51, 19 % of the texels goop at 90, 37 % at the 150 cap -- the splatter grows from the dark cells
out and never covers him. TEX1 and TEX2 are the same UV set in both bmds (bmd_uv_probe.py), and his glb exports carry it as TEXCOORD_1 on every
surface (glb_uv_probe.py: distinct from UV0 on all 11 body surfaces and the hands).

### The port
* MarioFog.gdshaderinc (pass 97's per-surface replica of his StandardMaterial3D) gains the swap: `dirty` (mDirty / 255), `pol_mask_tex`, `pol_tex`,
  `pol_uv` (1 = UV2, the bmd's TEX1 / TEX2; chosen at Bind from the mesh format). Where floor(mask x 255 + 0.5) / 255 <= dirty the albedo texture
  sample is the goop texture's -- before the lighting, as the TEV does it. The fog term is unchanged.
* SmsBodyOverlay: `SetDirty(amount255, goopTexture)` -- the 16 shader copies get `dirty` and the goop texture, and are INSTALLED while dirty > 0 or
  fogged (they used to swap in for the fog alone; the restore by identity is the same). New exports on BodyOverlay.tscn: `Body Pollution Mask`
  (tex/H_ma_polmask1.png), `Hand Pollution Mask` (tex/H_ma_polmask_hand.png) -- both from his bmds via bti2png, pinned lossless -- and `Goop Texture`
  (Npc/tex/H_ma_rak.png, the stage skin). The five hand meshes (ma_hnd2l / 3l / 2r / 3r / 4r under Left/RightHandBone) take the hand mask by MESH
  name (`Hand Mesh Prefixes`); their material names vary with the export. `Dirty`, `DirtyCount`, `GoopTextureNow`, `PollutionUvNow` for the drives.
* Mario: after the fog line in SmsAfterMove, `_smsBodyOverlay.SetDirty(_smsDirtyAmount, <the 108 layer's skin>)` every tick -- the layer that dirtied
  him dresses the skin too when SmsGoopMario.GoopFromLayer is on (pass 108's rule; off = the stage skin, the game's one look).
LEGACY RETIRED: none. DEVIATION (inherited from 108, behind its knob): the goop texture per layer; the game keys it per stage.

### Verified (drive 111, 8 checks, on his machine; drive97 (the fog) 29/29; regressions 110 / 107 / 108 / 106 green; build 0 errors; boot ALL OK)
The overlay carries the two masks (64 x 64 / 32 x 64) and H_ma_rak and picked UV2 from the glb; clean = all 16 surfaces on their StandardMaterial3D,
nothing installed; the body mask's CPU census matches the game's range (0 % <= 50, 19 % <= 90, 37 % <= 150); mDirty 90 installs all 16 copies with
dirty 90/255, a mask each (the five hands the hand mask), the stage goop, pol_uv 1, the fog off; back to 0 restores the 16 standard materials; a run
onto a layer with its own goop texture dirties him (thinkDirty) and the skin wears that layer's texture, dirty following mDirty; the knob off puts
the stage goop back. Headless cannot see the texels -- the look is his: run through goop and the brown splatter should grow over the overalls,
cap, gloves and face from the dark cells of the mask outward, and wash off in water as mDirty falls.

### Caught by the drive
The hand mask was first keyed on the material name and matched 3 of 5 (the exports name them `_mat_hand3_L`, `LeftHandSlightyClosed`,
`_mat_hand4_r1`...; one ResourceName differs at runtime) -- keyed on the mesh name / the HandBone ancestor instead. A FIRE layer in the drive
BURNED him instead of dirtying him (floorDamage, no dirt -- the game's too); the dress test uses a Slip layer carrying the fire texture.

## SMS pass 112 -- the fruits bounce off Mario: TMapObjBall::kicked / boundByActor, and Mario walks THROUGH fruits

Jaysonn (10-20 22:08): "Finish 110, then do 111 Mario's goop skin, then fruit bounce".

### The game
TMapObjBall (MapObjBall.cpp) is every fruit, the football and the watermelon. How one reacts to Mario (touchActor 299 / receiveMessage 427-449,
every frame their hit cylinders overlap, while the fruit is not held and its 10-frame guard unk194 is down):
* kicked() (184) when Mario's mVel.y != 0 -- he jumps, falls or DIVES onto it: a fruit already rising is left alone; at rest it hops unk178 (20 u /
  frame), falling it rebounds -unk160 x its own vy + unk174 (0.13) x his vy; xz += unk170 (0.9) x his xz; both axes under the physical's min speed
  -> a random unit xz; AIRBORNE; Mario's receiveMessage(HIT_MESSAGE_ATTACK).
* boundByActor() (229) when his vy == 0 -- he runs or stands into it: inside mDamageRadius in XZ; faster than the min speed on an axis -> vy +=
  unk150 (1.3), else unk154 (durian 15); xz += unk148 x his xz - (the unit offset to him) x unk14C; receiveMessage(ATTACK). Its "dive" branch
  (his vy < 0 and his feet + 130 u under the fruit's top) is only reached from the WATERMELON's receiveMessage: for a fruit a Mario with any
  vertical speed goes to kicked() first.
* What Mario does with HIT_MESSAGE_ATTACK from a fruit (MarioReceiveMsg.cpp 877): a DURIAN (and the football) freezes him mKickFreezeTime = 2
  frames (mario.prm; his move skipped, MarioMain.cpp 96, then mNoFreezeTime 2 of immunity) with the AMIATTACK puff; every other fruit: nothing.
  Picking up stays HIT_MESSAGE_TAKE (B).
* The per-type numbers (initMapObj 451-607), u / frame: coconut / papaya / pineapple / pepper 148 .4, 14C .2, 150 1.3, 154 0, 158 1.2, 15C .8,
  160 .5, 170 .9, 174 .13, 178 20; durian 148 .6, 154 15, 158 .5, 15C 1.3, 160 1.0; BANANA all zero (it never bounces off him -- it slides).
  Min speeds (the physical data): 0.3, durian 0.2, banana 1.0. Body radius 50 u x the scale (coconut / papaya 40, durian 45); hit cylinders
  (TMapObjHitDataTable) 80 r x 90 h, the coconut's 50 x 80; Mario's 50 x 160.
* And the fact that made the port's run-in impossible: a fruit's TMapObjData carries NO map collision (the collision slot is null, MapObjInit.cpp
  coconut_data) -- Mario walks THROUGH fruits; only the cylinders react.

### The port
* Fruit.cs (his RigidBody3D fruits; the bounce / roll PhysicsMaterial profile is his and stays): `SmsBallTick` from _PhysicsProcess at the map
  objects' 30 fps -- the cylinder test (SmsMarioTouches), the guard, a held fruit (Freeze) skipped, then `SmsKicked` / `SmsBoundByActor` as above
  with the per-type table (`SmsBallParamsFor`) as velocity writes on the body (units a frame <-> m/s through SmsUnits). A resting Godot body's
  vy jitter (|vy| < 0.05 u/f) counts as the game's exact 0 in kicked(). `SmsPassThroughMario` (default ON, a knob): at _Ready the body leaves
  collision layer 1 for 2, so Mario's mask (1 + 3) no longer stops on it -- the floor still catches it (its own mask is unchanged), the take
  (group "pickable") and the throw are physics-layer-free. Off = the port's solid fruits (with which a run-in stops 60 u short, inside the
  touch radius but outside a coconut's 50 u damage radius: the game's run-in can then never fire). `SmsLastTouch` / `SmsTouchCount` /
  `SmsLastTouchVelUnits` for the drives. LEGACY RETIRED: the durian-only kick (`_PhysicsProcess`'s GetCollidingBodies / KickImpulse block); the
  Kick* exports stay so the scenes load, read by nothing.
* Mario: `SmsMarioVelUnits`, `SmsFeetWorldY`, `SmsHitRadiusUnits` / `SmsHitHeightUnits` (50 / 160); `SmsKickFreeze()` -> mFreezeTimer: the SMS
  frame skips his move (velocity zeroed) for 2 frames, then 2 of immunity (`SmsFrozen`). The AMIATTACK puff is not baked (no slot yet).

### Verified (drive 112, 7 checks, on his machine; regressions 110 / 111 / 105b / 107 / 106 green; build 0 errors; boot ALL OK)
A sprint (32 u/f) into a resting coconut: ONE touch, the run-in, lifted exactly 1.30 u/f and carried 0.4 x his run minus the push (-37.4 u/f), no
freeze on him; a fall onto a resting durian: the FIRST touch a kick with the set hop 20.00 u/f and a random unit xz, and he froze 2 frames a touch
(12 physics frames over 3 touches as he settled beside it); a DIVE onto a resting coconut: a kick (20.00 up, 0.9 x his run forward), as the game
dispatches it; a sprint into a banana: the touch registers, the velocity unchanged. Headless cannot see the arc -- the look is his.

### Caught by the drive (port and test)
Run 1: no run-in touch at all -- the probe showed Mario STOPPED 60 u from the coconut (the port's solid fruit body), and the game's boundByActor
radius is the fruit's own 50 u: the run-in could never fire while fruits block him. The decomp's data gave the answer (no map collision on a
fruit); SmsPassThroughMario is the port of that. Run 2: a resting RigidBody's vy reads +-0.00x, which kicked()'s `vy > 0 -> return` took as rising:
clamped. The test's D1 first expected boundByActor's dive branch for a diving Mario -- wrong: the game's dispatch (vy != 0 -> kicked) sends a dive
to kicked(); the drive now asserts the game's rule. No regression drive covers the take / throw (62 / 63's installers are gone); the take search
is group-based and the held fruit is Freeze = true in both paths, so the layer change does not touch them -- his hands-on check is owed there.

## SMS pass 113 -- Mario's goop skin follows the STAGE, not the layer (the all-black oil skin); a per-fruit kick flag

Jaysonn (10-20 23:20): "when mario has dirt on him hes turning all black but when walking on goop he flashes like normal" and "also all the fruit
are kickable? shouldnt we have that as a flag?". Both the skin and the flag land here; his editor was CLOSED (00:01), so any file could be touched.

### The all-black oil skin
Pass 111 ported TMario::addDirty, and handed SmsBodyOverlay.SetDirty the goop texture that pass 108's rule (SmsPollutionLayer.CharacterGoop)
picked for the LAYER Mario stood in -- that was his 02:08 ask for the NPCs ("the goop on npc should be whatever goop they are in"). On a layer
whose own texture is dark (OilGoop = the glossy green-black), the per-texel swap painted his splatter black: his dirtRef.png shot measured the
head at (0,0,0) and the overalls at OilGoop's teal (0,44,55) -- the LAYER's texture, not goop-brown. The GAME never does this: TMario's
H_ma_rak_dummy is swapped at LOAD for the stage's one h_ma_rak.bti (Delfino's is the brown H_ma_rak.png) -- a skin per STAGE, never per goop; a
dark goop still dirties Mario brown. (The fog "flashing normal" he contrasted it with is a separate effect -- addDamageFog, untouched.)

### The port
* Mario.cs: new DEVIATION knob `SmsSkinFromLayer` (default FALSE = the game). The SetDirty call now gates the layer texture on it: false -> the
  texture arg is null, and SetDirty falls back to the BodyOverlay's GoopTexture export (the stage's H_ma_rak); true -> pass 108's rule (the
  layer's own texture, pink in pink goop, BLACK in oil -- his old look, kept on a knob). The NPCs are UNCHANGED (their GoopFromLayer still dresses
  them in the layer they sank in -- his explicit 02:08 ask); only Mario moved to the game's per-stage skin.
* MarioFog.gdshaderinc: the gooped texel now takes the goop texture AS-IS (`base = texture(pol_tex)`), matching the GX TEV chain (stage 1 = body
  x REG2, stage 2 = + rak x (1 - REG2): a replace, the rak never multiplied by a material colour). The old line multiplied by the material's
  AlbedoColor -- a NO-OP here (all 11 of Mario's body materials have baseColorFactor 1,1,1, verified from ma_mdl1.glb) but wrong in principle and
  corrected so a future tinted material cannot darken the goop.
* Fruit.cs: new DEVIATION flag `SmsBallReacts` (default TRUE = the game) -- false makes THAT fruit ignore Mario's touch entirely (SmsBallTick
  returns: no kick, no bounce, no durian freeze); picking it up is untouched. The GAME has no such flag: every TMapObjBall runs touchActor
  (MapObjBall.cpp 299); what makes the banana inert is its all-zero table, not a flag. Offered because he asked for one.

### Verified (on his machine; editor closed; build 0 errors; boot ALL OK; Mario.cs back to 210a09de drive-free)
* The dll carried SmsSkinFromLayer and boot was ALL OK BEFORE this log -- the "still black" screenshot (dirtRef.png, 23:38) predated the fixes
  AND a running instance never hot-reloads C#, so it showed the old 111 skin regardless of the 23:35 rebuild.
* Drive 111 (patch_smoke111.py, retargeted for the 113 default) 8/8: S1 masks + stage goop + pol_uv 1; C1 clean 16 StandardMaterial3Ds; M1 the
  mask census 0 / 19 / 37 %; D1 mDirty 90 -> 16 copies, the STAGE goop, 5 hand masks; D2 fog off; D3 restore; **L1 `SmsSkinFromLayer` ON -> the
  skin wears the LAYER's FireGoop on all 16; K1 (THE BUG) `SmsSkinFromLayer` OFF (the default) -> though he STILL stands dirty on the FireGoop
  layer, all 16 surfaces wear the STAGE H_ma_rak and NEVER the layer's (layerWorn 0, skin H_ma_rak.png)** -- the headless proof the oil skin is brown.
* Drive 112 (patch_smoke112.py) 8/8: B1/B2/K1/K2/D1/N1/N2 as pass 112, plus **R1 `SmsBallReacts` off -> a sprint into a coconut registers NO
  touch (touches 0, velocity at rest): the fruit ignores Mario**.
* Regressions: 107 18/18, 108 26/26, 110 8/8, 106 18/18, 105b 14/14 (the goop-adjacent set -- 113 touches the goop skin and the fruits).

### Not ported / owed
The goop PARTICLES (107's footprints / melt / fling) still take the layer's colour from pass 108's WearGoopOf -- so on oil they stay oil-coloured
even though the skin is now stage brown; offered to move them to the stage colour too (his call). HIS EYE still owes the look: run through goop ->
brown splatter over overalls / cap / gloves / face (heaviest on the shoes and cap at the dirt cap), oil no longer black.

## SMS pass 113b -- the goop skin's mask gates on the GPU (the "all-black Mario" was the whole body gooping)

Jaysonn kept seeing Mario go black when dirty. The chase (logged honestly, it was long): the skin texture was proven correct (H_ma_rak, SmsSkinFromLayer off, no shock overlay) by a runtime probe, yet the GPU rendered him dark. His decisive clue -- "fine WHILE IN the goop (fog flashing), black when NOT in it" -- showed the goop fog was masking a dark body: the WHOLE body was wearing the goop skin instead of the ~20 % the mask intends, so Mario was uniformly dark H_ma_rak brown (near-black on the shadowed back; olive where the yellow goop-fog tinted it).

ROOT CAUSE: the pollution mask (H_ma_polmask1, intensity DATA) imports as a colour texture, so the GPU samples it sRGB-DECODED -- 147 reads as ~75 -- and `mask <= mDirty` passes almost everywhere. Every headless drive (drive 111 M1/D1, the FDUMP) read the mask IMAGE on the CPU, where the values are already raw, so NONE could see it; the NPC skin (SmsNpc, same shader comparison) survived because its mask is a smooth GRADIENT that tolerates the shift, while Mario's splatter does not. The two masks' .import files are byte-identical, confirming it is the SAMPLE space, not the import params.

FIX (SmsBodyOverlay.cs b693b2b8 -> d0ccc02f): `RawLinearMask()` re-wraps the mask as a linear R8 ImageTexture (R8_UNORM is never sRGB-decoded) before binding pol_mask_tex, so the shader's `floor(.r * 255)` reads the true 8-bit thresholds. Cached per source texture. No shader or import change; the NPC path is untouched.

VERIFIED on his RTX 3070 Ti via a new NON-headless capture harness (patch_fogoff.py force-dirties mDirty 150 with the fog OFF, run_gpu.ps1 runs windowed, saves the real viewport): at mDirty 150 the cap renders RED, the gloves yellow, the FLUDD blue -- all CLEAN -- and the goop is now PATCHY H_ma_rak brown on the back / torso / legs, no longer the whole body. Build 0 errors, boot ALL OK. The harness means the goop LOOK can now be judged/tuned without a user run -- the gap that made this take so long (headless cannot sample textures on a GPU).

STILL OPEN (his eye / a deviation call): the gooped areas are a DARK brown (H_ma_rak is a dark albedo; the decomp does a hard albedo replace, so this is close to faithful) and read near-black on the shadowed side. If the game's dirt looks lighter / more like a tint over the clothes, that is a deviation to tune (a goop brightness / blend knob) -- offered, awaiting his word. Committed the gating fix; NOT yet committed as a drive check (the gating is GPU-only, so a headless drive cannot assert it -- the capture harness is the check).

### SMS pass 113c -- a dirty Mario off the goop no longer renders BLACK (2026-10-21)

**Jaysonn's report:** "fine while standing in goop getting dirty, but the moment he's not in that pulse red/yellow state and dirty, his mesh
goes black." Confirmed on the RTX 3070 Ti with the fog-off capture harness: at mDirty 150 with the fog off the gooped body was PURE (0,0,0)
-- not a dark brown, a true zero -- while the cap, gloves and FLUDD rendered normally.

**Cause -- the engine's FOG contract, not the goop.** `MarioFog.gdshaderinc` (pass 97) writes `FOG` only inside `if (fog > 0.0)`. In Godot
4.4+ (`scene_forward_clustered.glsl`) `fog` starts at (0,0,0,1) and, when a shader writes FOG at all (`CUSTOM_FOG_USED`), the engine
unconditionally converts it with `rgb *= a; a = 1 - a` and blends `frag x a + rgb`. A fragment that leaves FOG untouched therefore ends with
a = 0 and renders BLACK. Godot 4.3 blended `mix(frag, fog.rgb, fog.a)` from (0,0,0,0), where the unwritten case was harmless. Pass 111 made
the hazard visible: it keeps the shader copies installed while `dirty > 0` with `fog = 0` -- exactly the branch-skipped case -- so the body
was black whenever he was dirty and NOT pulsing; the pulse (fog > 0) wrote FOG and looked right. The goop skin, the mask (113b) and the
texture (113) were all correct; six probe variants (albedo green, emission green, discard, FOG blue) pinned it: only a written FOG changed
the pixels.

**Fix (`assets/Sms/Fx/MarioFog.gdshaderinc`):** `FOG = vec4(0.0);` on every path before the `if (fog > 0.0)` -- a written (0,0,0,0)
converts to (0,0,0,1) = no fog. The 113c "brightness" change (feeding the goop at gamma brightness) is NOT shipped: it was a diagnosis of
the wrong variable -- with FOG written, the gooped texels render as the lit stage brown (`H_ma_rak`), mean (94,49,34) on the shadowed back.

**Verified on his machine:** fog-off GPU capture at mDirty 150 -- 0 pure-black body pixels (2441 before), cap red, gloves yellow, goop a lit
brown on back/torso/legs; drive97 (the fog) 29/29; build 0 errors; boot `ALL OK`. Only the shader include changed; `Mario.cs` /
`SmsBodyOverlay.cs` untouched (210a09de / d0ccc02f).

**Rule for every spatial shader here:** if a shader writes `FOG` anywhere, write it on EVERY path. The tree has no other FOG writer.

### SMS pass 114 -- the red coin challenge's time-up, the whole dead pad, and the goop skin follows the goop (2026-10-22)

**The time-up (SMS 1:1).** In the game `evAppear8RedCoinsAndTimer` hands every red coin `killByTimer(timer - 120)` the moment it appears
(EventWatcher.cpp 1193), so a coin's life IS the challenge timer, on the 120 Hz clock: untouchable for its first 120 ticks (`TItem::initMapObj`
mUntouchableFrames: HIT_FILTER_NO_COLLISION), blinking through its last 360 (`mNormalFlushTime`, hidden while ticks / 10 is odd = 12 Hz), dying
with MSD_SE_SY_COIN_DISAPPEAR the tick the timer hits zero (`TItem::appeared`). Nothing else reacts: the switch sits in `control()` state 4
for the stage visit (no retry), the console clamps 0:00.00, the counter keeps its count (`timerredcoin.sb` ends after `appearRedCoinCounter`).
Port: `RedCoinSwitch` keeps the coins it spawns (`_smsCoins`), runs the flush and the death off your `ShineTimer`'s countdown (no countdown
= no kill), turns the coins' `PickupZone` Area3D off for the first second, `CoinDisappearSound` slot (no stream yet). DEVIATION knob
`SmsRearmOnTimeUp` (default off = the game): on, the time-up re-arms the switch (your test levels have no exit to reset it).
Your `ShineTimer.cs` / `RedCoin.cs` untouched; the HUD timer still leaves at the target (nothing in the game's script decides otherwise).

**The dead pad, complete.** `TMarioGamePad::updateMeaning` (MarioGamePad.cpp 70) gives NOTHING but START a meaning while `mDisabledFrames`
> 0. Pass 109 neutralised the stick and A; now B / R / Z / L / Y / X / key_z / rclick are released each dead tick (MarioYCam's ActionRelease
precedent), the FLUDD's R strength and rHeld / bHeld, the X swap and the Y / cancel edges are gated at their reads, and the camera's C-stick,
L edge, AimR and CamL go through `SmsCamMarioData.PadDead`. Logged deviation: a button held across the window's end stays released until
re-pressed.

**The goop skin follows the goop he is in -- by default** (Jaysonn 2026-10-21 23:36: "covered in pink but when steps on brown it changes to
that color"). `SmsSkinFromLayer` (pass 113) now defaults ON: the skin is re-read every tick he stands on paint, so pink in pink goop, the
stage brown on a plain layer, oil's dark green-black on oil; off = the game's one skin per stage (each Sunshine stage has one goop; your
levels mix them). The switch is instant and whole-body (one goop texture slot in the material chain, as in the game).

**Verified on your machine:** drive 114 15/15 -- two switches with 7 s / 2.5 s timers: the untouchable second (a coin stood in from 0.35 s is
still live at 0.70 s, taken at 1.70 s), no blink above 3 s, 23 toggles through the last 1.9 s, all seven left gone at zero with the switch
still pressed and inert to a second pound, the HUD counter kept, R / Z / L / Y / X / B and the C-stick dead through the press (the camera's
own follow aside) and live again after (the C-stick turned the camera 83 deg, Y entered the Y camera), the knob's re-arm and a new challenge
from it. Drive 111 11/11 with three new checks: N1 the default, W1 a dirty Mario stepping from a FireGoop-textured layer onto a plain one
switches to the stage brown with his dirt kept, W2 back again. Regressions 109 19/19, 110 8/8, 112 8/8, 106 18/18, 105b 14/14, 102 20/20,
104 23/23; build 0 errors; boot `ALL OK`. Three drive-side fixes (the yaw read was Mario's armature, not the camera's; the re-pound needed
a step off the switch to re-enter its hit zone; D1 compared the 113b raw mask by identity) -- no code change from them.

### SMS pass 115 -- the fruits' own ball physics, and only the durian answers Mario's body (2026-10-22)

**Only the durian reacts to Mario's body (SMS 1:1) -- pass 112 was a mis-port.** Every fruit is a `TResetFruit` (MapObjManager.cpp 290), whose
`kicked()` (MapObjBall.cpp 793) RETURNS on MAP_OBJ_FLAG_UNK2000000 and on a falling Mario (SMS_GetMarioSpeedY() < 0), and whose
`boundByActor` Mario branch (245) is gated by the same flag. Coconut / papaya / pineapple / banana / pepper all carry 0x02000000 (MapObjInit.cpp
0x02130100 / 0x02130000); the durian (0x00170100) does not. So walking into or landing on a coconut does nothing; the durian is pushed when
you run into it (`boundByActor`: vy += 1.3 over the min speed else 15, xz += 0.6 x his run - 0.2 away, Mario gets HIT_MESSAGE_ATTACK) and
kicked when a RISING Mario meets it (`kicked`: a resting one hops unk178 = 20 u/tick, else 0.13 x his vy - 1.0 x its vy; xz += 0.9 x his,
a random unit xz under the min speed; the 10-tick guard). The dispatch is `touchActor` 299: Mario's vy != 0 -> kicked, else boundByActor.
DEVIATION knob `SmsTouchLikeDurian` (per fruit, default off): that fruit answers Mario like the durian (pass 112's look). Drive 112's
expectations are wrong by the game: dropped from the chain, 115 replaces it.

**The ball itself (`TMapObjGeneral::bind` 500 every 120 Hz tick, `TMapObjBall` 37-180, physical data MapObjInit.cpp).** While Mario runs SMS
physics the RigidBody3D goes frozen KINEMATIC (moved by GlobalPosition) and the game's sim drives it: gravity 0.3 a tick (clamped to the body
radius), air drag 1.0, a fall slower than 1.5 u/tick lands else rebounds about the floor with 1 + 0.6 (banana 0.4), the roll decays x 0.97
(banana 0.4) every contact tick and stops under 0.3 (banana 1.0), 0.5 x the floor normal's xz downhill, a wall pops a rolling ball 1.0 x its
speed and reflects the into-wall velocity with 1 + 0.3 (durian 0.2; the decomp's own touchWall line reads INTO the wall -- the general
TMapObjGeneral::touchWall reflection is used, noted), a roof clamps the head and reflects with 0.6, the model rolls |vxz| / R x 2 a tick,
rest = bind skipped (LIVE_FLAG_UNK10). `hold()` refuses a ball faster than 10 u/tick (`SmsTakeable.MayTake`). Mario's take / put / throw feed
the sim through `SmsHold` / `SmsPut` / `SmsThrown(vel)`; `SmsThrownFlight.Begin` skips a sim fruit. Knob `SmsBallPhysics` (default on); off =
your Godot RigidBody profile with the corrected touch rules.

**A fruit has no map collision.** `coconut_data`'s TMapCollisionInfo slot is nullptr (WoodBox carries one): Mario is never blocked by a fruit and
cannot stand on one -- he meets it through the hit cylinders alone. The sim's kinematic body is excepted from his MoveAndSlide every frame
(his throw adds the same exception and removes it 0.6 s later). Found by the drive: he landed ON the durian and kept pushing it.

**Not ported (logged):** the TResetFruit life cycle (14400 ticks living -> rotting / breaking -> 360 waiting -> reappear at home; your fruits
are your own supply), the water-surface / goop respawn, the banana / pineapple model Y offsets, the ground actor's carry, the sounds (slots
`SmsBounceSound` / `SmsKickSound` only). Also fixed: the stale "empty-tank rate not ported" comment at Mario.cs 13058 -- pass 46 ported it
(`_fluddPumpRate = R x PressureMax x PumpAnmSpeed`); 116 needs no pass.

**Verified on your machine:** drive 115 13/13 -- the sim owns a spawned coconut (kinematic, at rest, bottom 1 u over the floor), a 300 u drop
rebounds to 106 u (want ~108) and rests after 4 rebounds, a 6 u/tick roll runs out 190 u with the model rolling, no take while faster than
10 u/tick, a wall: arrival 4.9 -> pop 4.6 and -1.39 back (1.0x / 0.3x), a roof: 12 up -> -6.24 down with the head clamped under it, a run
THROUGH a resting coconut moves nothing and he is 5.7 m past it, the durian run-in (kind 2, lift 1.3, pushed 7 m), a FALLING Mario kicks
nothing and falls through to the floor (the landing's boundByActor is kind 2), a RISING Mario kicks it (hop 20, kind 1), the knob, every
fruit frozen kinematic. Regressions 111 11/11, 110 8/8, 114 15/15, 109 19/19, 106 18/18, 105b 14/14, 107 18/18; build 0 errors; boot
`ALL OK`. Drive-side fixes only: W1's expectations were written for the launch speed (the ball reaches the wall at 4.9 after the game's
0.97/tick roll-out), F1's roof stood where the ball popped off the wall (it rolled 1.6 m out from under it), T4 needed a re-settled durian.

### SMS pass 115b -- the fruit holds Mario off (keepDistance); he no longer walks through it (2026-10-22)

Jaysonn 23:34: "fruits feel right but mario is walking thru them". 115's "no map collision" reading was half the game: a fruit has no MAP
collision (he is never blocked by its body, never stands on it), but Mario's own collision pass holds him off its HIT cylinder.
`TMario::checkCollision` (MarioCheckCol.cpp 487-493): coconut / papaya / pine / banana / red pepper -> `hitWantToTake` = `keepDistance(*actor,
0)` + the B take; the durian (495) -> `hitPushup` (a rising jump sends HIT_MESSAGE_PUSH_UP) + `hitNormal` -- NO keepDistance: the durian is the
one moved. `keepDistance` (MarioPhysics.cpp 27): inside the actor's damage radius + unk15C (50, his wall radius) of its axis he is set back out
to that distance along the line from it, 50 units a call at most; running in above mClashSpeed (40 -- the turbo dash) is the clash knockdown
instead. It is a RADIAL push: a run that is not dead-centre slides AROUND the fruit rather than stopping dead, as in the game. Port: the fruit
calls Mario's existing `SmsKeepDistance` (pass 85, the NPCs) every tick its cylinder holds him, when it is a fruit that does not answer his body
(`SmsTouchLikeDurian` moves instead; `SmsBallReacts` off stands like a rock); in both the sim and the knob-off path. Fruit.cs only.

**Verified on your machine:** drive 115 14/14 -- T1 a run at a resting coconut: closest approach 100 u from its axis over the whole run (never
through it; he slid around and past), no touch, still at rest; T1b from the held-off spot the B take still finds it (the take pair 80 + 50 = 130
u > 100); the durian run-in / fall / rising kick unchanged. Regressions 111 11/11, 114 15/15, 109 19/19, 107 18/18; build 0 errors; boot `ALL OK`.

### SMS pass 115c -- the fruits' hit cylinders at your models' size, and the roll in world space (2026-10-23)

Jaysonn 00:04: "the fruits hit boxes seem off and they spin the wrong way when thrown". Three faults in 115, all in `Fruit.cs`:

**The per-type hit tables.** MapObjInit.cpp `{attackR, attackH, damageR, damageH}`: coconut 1553 and papaya 1594 {40, 60, 50, 80}, pine 1629
{80, 60, 50, 80}, durian 1702 {150, 60, 60, 90}, banana 1743 {80, 60, 60, 90}; only the red pepper uses the generic `fruit_hit_data_table`
1536 {60, 60, 80, 90}. 115 had given everything but the coconut the pepper's 80 / 90.

**The pair is two-sided** (ObjHitCheck.cpp 43 `checkActorsInList`): Mario's ATTACK cylinder (HoldRadius 80 x AttackHeight 50 standing;
PushupRadius 60 x 180 rising; TrampleRadius 100 x 50 falling -- the NPC rule, pass 85) against the fruit's DAMAGE cylinder puts the fruit
in HIS list (`checkCollision` -> `hitWantToTake` -> keepDistance); the fruit's ATTACK cylinder against Mario's DAMAGE cylinder (50 x 160)
puts him in the FRUIT's list (`control` -> `touchActor` -> kicked / boundByActor). 115 tested one cylinder (50 + damage) for both. So the
durian's rising kick reaches 150 x scale + 50, and a coconut's hold-off engages from 80 + 50 x scale.

**The scale.** `TMapObjBase::setObjHitData` (MapObjBase.cpp 70) scales an object's hit radii by max(sx, sz) and heights by sy -- the game ties
the hit boxes to the model. Your fruit models at Size 1 are about half the game's: `fruit_coconut.tscn` sphere 0.3166 m = 23.6 u (the game's
body radius 40), durian 20.6 u (45), pineapple 24.7 u (50), banana 19.5 u (50). 115 / 115b used the game's raw numbers, so a coconut held
Mario off at 100 u (a 26 u gap outside the model; a pineapple at 130). Now `SmsHitScale` = the collision sphere's radius / the game's body
radius (Size is already in the sphere via ApplySize); knob `SmsHitScaleFromModel` (default on; off = the game's numbers x Size). Coconut:
scale 0.59, hold-off 50 x 0.59 + 50 = 79.5 u (the game's proportion: a 10-unit gap), inside the B take's 80 + 40 = 120 reach. boundByActor's
own XZ test (mDamageRadius) scales too: the durian is shoved when his feet pass within 60 x 0.46 = 27.5 u of its axis.

**The roll's frame.** `calcCurrentMtx` (MapObjBall.cpp 318) rolls about `getVerticalVecToTargetXZ` -- the direction turned +90 deg about Y
(MapObjLib.cpp 549) = up x d, the physical rolling axis -- by |vxz| / R x 2 a tick (radians, `TRotation3::setRotate`), and concatenates it on
the WORLD side of the model matrix (`MTXConcat(rot, anmMtx)`). 115 built that world axis but applied it to the model child in the BODY's
frame; after a hold the body keeps Mario's hand orientation (the throw sets GlobalPosition only), so a thrown fruit rolled about a yawed
axis -- the wrong way. The axis now goes through the body's inverse basis.

**Verified on your machine:** drive 115 16/16 -- R0 with the body yawed 180 deg (as after a hold) one frame into a +X roll the model's top has
tipped toward +X (0.83, 0.56, 0); H2 hit scale 0.589, hold-off 79.5 u inside the 120 u take reach; T1 a run at a coconut: closest approach
79 u (the hold-off, never through), no touch, at rest; T1b the B take finds it from there; T2 the durian run-in (kind 2, lift 1.3, pushed 8 m);
T3 falling kicks nothing; T4 standing beside the durian outside its 27 u damage radius then jumping: the first rising tick kicks it (hop 20,
kind 1); K1 the knob. Regressions 111 11/11, 114 15/15, 109 19/19, 107 18/18; build 0 errors; boot `ALL OK`. Drive-side: T4 and K1 re-staged for
the smaller cylinders (a 27 u attack band is one rising tick tall; the camera-relative run drifts in X).

### SMS pass 116 -- the shine get as the game plays it (2026-10-23)

The disc files came back: `super_mario_sunshine.iso` on your E: drive (`E:\fromC\downloads\games\GAMECUBE GAMES`). `tools/projectshine/gcm_tools.py`
(new: GCM FST + Yaz0 + RARC, pure Python, run on your PC) pulled `/data/common.szs` -> `camera/camera_demo_shine_get_outside.bck`,
`_inside.bck`, `camera_model.bmd`; `/data/scene/dolpic0.szs` -> `mapobj/shine.bmd`, `shine_demo_shine_get.bck` (+ `_yo`, `shine_empty_*`,
`shine_float.bck`); `/data/game_6.szs` -> `scrn/big_tx_1.blo`. `gen_shine116.py` bakes the two camera clips and the shine's flight into
`assets/Sms/SmsShineGetData.cs` (419 frames at 30 fps each, game units, in the demo frame: origin Mario, +Z his facing, +X its LEFT -- SMS is
right-handed with +Z forward; Godot: Basis(Up x F, Up, F)).

**The touch** (MarioReceiveMsg.cpp 236-250): `mPosition.xz` = the shine's (your snap was already the game), `mFaceAngle.y` = the SHINE's placed
yaw (`mInitialRotation.y`, captured in `ShineSprite._Ready` before the entrance spin; the port used to keep his facing and aim the rig with the
camera's), velocity 0, HP = max, air = max, `WIN_DEMO`. `winDemo` (MarioAutodemo.cpp 18): the held object dropped (HIT_MESSAGE_UNKD -- the take
while carrying is no longer refused), `killEnemiesWithin(2000)` (the Goobles in group `sms_enemy`, new `Gooble.SmsKillByDemo`), and if he is
airborne he keeps falling until he lands (your `_pendingShine` wait IS `jumpProcess`); then `fireGetStar` + the shine's TAKE and state 1:
`demo_shine_get` + `stopProcess` every frame (velocity 0; the clip's end no longer hands control back -- the fade does).

**The camera** (TMarDirector::fireGetStar -> fireStartDemoCamera; TCameraBck::updateDemo): `camera_model.bmd` has two joints, `cam_interest`
(the look-at) and `cam_position` (the eye) as its CHILD, and J3DMtxCalcBasic composes the child under the parent's full matrix INCLUDING its
scale (the interest's scale.y runs 36..42 -- so the eye's local (230, -9, 53) lands at (220, 18, 60): a low angle 237 u out on his left,
looking up at the descending shine, then a sweep up and around to the front, high (eye y 461, z 437 at 6.33 s) looking down at him). The fovy
= the position joint's scale.y (49.3 deg -> 35 deg). Driven on its own `Camera3D` (`SmsShineGetCam`) with LookAt; knob `SmsShineGetInside`
picks the `_inside` clip (`TShine::mIsInside`).

**The shine's flight** (TShine::receiveMessage 640: the shine jumps to Mario's position / angle and plays `shine_demo_shine_get` in HIS frame):
the root rises 214 -> 364 u (0.67 s), descends to his hands (~16 u at 3 s), the star spins about Y (the `body` joint, -1382 deg -> +1074 deg
over 6 s) and tilts; root scale 0.5 (the game's demo shrinks the shine; `mEffectScale` 0.5). `ShineSprite.SmsDemoDrive` sets your root so the
star's centre (`SmsStarLocal` (0, 200, 0) root-local) lands on the composed `body` joint each frame; `SmsDemoScale` (default 1 = your full
size; 0.5 = the game's) -- a DEVIATION by default since your star model is not the game's. No baked clip while driven (the neutral pose).

**The letters** (ConsoleStr.cpp 364 / 570): the US `big_tx_1.blo` spells **SHINE!** (sg00..sg05, 70 x 70, ROT 12; JP / PAL have SHINE GET!) at
(59, 364) (118, 349) [I: 417 x 51 in this blo, interpolated] (224, 320) (283, 301) (341, 287) -- the lower third, rising to the right. On the
120 Hz console clock: `unk18 += 0.5` a tick, the first letter at `unk20` = 0xB4 = **3.0 s**; then index = ticks: letter i shows at 6i with a
40-tick Bezier from (150, -50) via (-21, 7), alpha +7 a tick; at 6i + 40 a 20-tick settle to its home; at 6i + 200 (4.67 s) a 40-tick fly-out to
(-200, 65) -- gone by 5.25 s. `SmsConsoleStr.StartAppearShineGet` / `ProcessShineGet` through pass 61's SmsBigText (your palette, one colour a
letter). Not ported: the sparkle emitters 0x1FE / 0x1FF at each letter (eye-owed). Knob `SmsShineGetLetters` (off = your Wonderful! popup).

**The end** (MarDirectorDirect.cpp 839-856): the pending step starts the letters, `MSD_BGM_CHUBOSS` (slot `ShineGetFanfare`), and
`TShineFader::registFadeout(60, 320)` at rate 60: a **1 s fade to BLACK starting 5.333 s** after the demo began; then the stage transition.
DEVIATION knob `SmsShineGetResume` (default on): your levels have no hub, so the screen fades back in over the same second and play resumes
(idle, the gameplay camera, the shine let go into its end tween); off = hold black for your transition. `SmsShineGetFadeAt` / `FadeLen` knobs.
NOT stop-the-world: `DEMO_FLAG_SHINE_GET_STOP_THE_WORLD` belongs to the shine APPEAR demo (Item.cpp 719); the get is `GAME_MODE_INNER_DEMO` --
the world keeps moving (brief114's audit said otherwise; corrected).

Master knob `SmsShineGetGame` (default on); off = the hand-authored cutscene exactly as before.

**Verified on your machine:** drive 116 18/18 -- A the ground touch: shineGet at once with velocity 0; face 90 deg = the shine's placed yaw (the
model too); HP 8/8 and air 9/9 from 3 and 1; the demo camera current at the bck's eye to 5 cm with fovy 49.3, starting on his LEFT; the star at
the bck's (0.6, 389, -0.1) u at 1 s; the letters armed and hidden at 2.9 s, all six in and settled by 3.6 s, flying out at 5.0 s with no fade
yet; at 5.83 s the fade half way, the letters gone, the camera high in front with fovy narrowing, Mario still frozen; black at 6.3 s; at 6.333
the resume: idle, the lock off, the gameplay camera, the shine let go; half way back in at 0.5 s, gone at 1.2 s. B carrying a coconut into a
shine: dropped (its sim resumes on the floor), the shine taken. C a Gooble 1500 u away dies at the touch, one at 2500 u lives. D touched in the
air: pending (locked, falling) until the floor, then the demo. Regressions 115 16/16, 111, 114, 109, 110, 107, 104, 102 green; build 0 errors
(first compile clean); boot `ALL OK`. Drive-side only: the Level root carries an offset (spawn, then set the global position), his physics
ticks are not 60 Hz (time by seconds, never frames), a leaked stick press, the carry state is the run's.

### SMS pass 117 -- the shine APPEAR demo (2026-10-23)

The last 1:1 piece of the shine front. The game (Item.cpp: `TShine::appearWithTime` 677, `appearWithDemo` 757, `control` 536-626; the stage
script's `appearShine(shine, demoCamera)` in EventWatcher.cpp 736): the shine is put where the event says, and `appearWithDemo` starts a
demo for the stage's placed camera tool's length with the world FROZEN (`DEMO_FLAG_SHINE_GET_STOP_THE_WORLD` -- this flag belongs to the
APPEAR, not the get; only the shine manager and camera 1 move) and runs `appearWithTime(len, -1, -1, -1)` with `initMapObj`'s defaults. All
timers are 120 Hz ticks (`TMapObjBase::control` runs every MOVE tick): `STATE_APPEAR_WAIT` (0) -> `MOVING_UP` 240 ticks at `mUpSpeed` 1.0 u a
tick (240 u = 3.2 m straight up) -> `MOVING_CIRCLE` for len - 480 ticks (`movingCircle` 481: xz stepped linearly to the PLACED spot; y = the
half-sine arc of `mCircleHeight` = 0.5 x (the xz distance, 1000 u when it appears where it belongs, + |yDelta| if the placed spot is above the
rise's top) over the lerp from the rise's top to the placed y; rotation += 7 deg a tick) -> `FLOAT` 240 ticks (above the placed y it keeps the
arc's last vy decaying x 0.99 a tick, else clamps; the spin slows from 7 toward 2 by 0.1 a tick) -> `WAIT` (2 deg a tick = 240 deg/s idle).
`HIT_FILTER_NO_COLLISION` (no pickup) from the appear until the float ends. Without a demo camera the script path is `appearWithTime(1200)`:
the same flight over 10 s with the world running.

Port (`ShineSprite.cs`): `PlaySpawnEntrance()` runs `SmsAppear(SmsAppearDemoTicks, withDemo: true)` when `SmsAppearGame` (default on; off = your
tween entrance exactly as before): the state machine above on the PHYSICS clock (`_PhysicsProcess` + `SmsUnits.TicksFor`, so it runs through
the pause with `ProcessMode` Always), the pickup zone off until the float ends, `SpawnEntranceFinished` at the demo's end (your red coin switch
listens). `SmsAppearFrom(start, ticks, demo)` for a script-style appear from elsewhere. The placed transform (`SmsInitialPosition`, and 116's
`SmsInitialYaw`) is now latched at the first frame or first use, not in `_Ready` -- a spawner sets the transform AFTER `AddChild`, so 116's facing
read the pre-placement yaw for a switch-spawned shine (fixed here). DEVIATIONS, both knobs, both because the stage data is yours: the demo length
`SmsAppearDemoTicks` (default 1200 = the engine's own no-camera path; the game reads it from the placed `TCameraMapTool`), and the camera
`SmsAppearCamera` / `SmsAppearCamOffset` (0, 2.5, 7): a fixed camera 7 m in front of and 2.5 m above the placed spot, looking at the shine as it
flies -- the game's is a hand-placed stage tool. `SmsAppearStopWorld` (default on). The game's idle spin after an appear, `SmsIdleSpinDegPerTick`
2 (240 deg/s); shines placed at level start keep `IdleSpinSpeed`. Sound slot `AppearSound` (MSD_SE_SHINE_APPEAR + the jingle BGM 0x8001002D;
MSD_SE_SHINE_EXIST every tick not ported). `RedCoinSwitch` skips its own reveal camera when the game's demo brings one.

**Verified on your machine:** drive 117 9/9 -- A the appear where it belongs: at once the demo running, the world frozen, its camera current, the
zone off, visible; 119 u straight up at 1 s; the arc under way past 240 u at 2.2 s; the crest 626 u over the placed spot (want ~620: 500 of the
arc + the lerp's 120); landed and settling at 8.1 s with the zone still off; at 10 s `WAIT` on the spot to 0.0 u, the zone on, the world
running, the gameplay camera back, `SpawnEntranceFinished` emitted; the idle spin 610 deg in 2.2 s (240 deg/s + the 7 -> 2 decay). B an
appear 5 m from its placed spot with no demo: the arc (height 186 u over the 166 u rise) delivers it onto the spot to 0.0 u and it settles.
Regressions 116 18/18, 114 15/15, 109 19/19, 115 16/16; build 0 errors (first compile clean); boot `ALL OK`. Drive-side: the flight first ticked
on the render clock (its _Process) and read 1.2x the drive's physics seconds, then both (2.3x) -- the physics clock alone is right.

### SMS pass 116b -- the shine get at the game's speed, with the game's shine (2026-10-23)

Jaysonn, first play of 116: "the shine sprite is too small, the scene plays too slow and cuts away too fast". Three things, all 116's:

**The rate.** 116 played the two bcks (the camera's `camera_demo_shine_get_outside`, the shine's `shine_demo_shine_get`, 419 frames each) at
30 fps -- 14 s of camera, cut by the fade at 5.33 s a third of the way through. The game runs EVERY MActor bck at `SMSGetAnmFrameRate()` =
60 / 30 = 2 frames per display frame (`MActorAnmBase::setFrameCtrl`, MActorAnm.hpp 149) with one `calcAnm` a frame (`TCameraBck::updateDemo`
runs on the last simulation tick, cameragc.cpp 964 / CameraDemo.cpp 59), and `getTotalDemoFrames` = (end + 1) x 2 ticks says the same: the
clips are 60 fps, 6.98 s long. `SmsShineGetData.Fps` 30 -> 60. The timeline now: the shine arcs up and comes down into his hands at 1.5 s,
the camera swings round and up to the pose shot at 4 s (lens 28.5 deg), the letters 3.0-4.7 s, the fade at 5.33 s while the camera is still
pulling back -- which is how the game does it (the clip's last 0.6 s are under the black).

**The shine.** Two faults. (1) 116 placed the shine's ROOT around the "neutral pose" captured at `_Ready` -- the bones wherever the editor's
`shine_float` frame left them, and that clip spins the body joint two turns (`body R.y 0 -> -720`), so the star's yaw inside the root was
arbitrary: his GPU capture showed the star EDGE-ON through the whole demo, a sliver with two balls on top. (2) the size was scaled off his
placed shine (0.006 a model unit = 0.45 of the game's 1 / 74.44). Now as the game: the actor's root at Mario's position and angle at the
game's 74.44 units a metre, and the bck's TRS written onto the three JOINTS each frame (`SmsShineGetData.ShineJoint`, root / starglow /
body -- J3D replaces each joint's TRS, and Godot's bone hierarchy composes the Softimage way, child T x parent S). The star faces the camera
on the pose (face . toEye 0.99), 0.5 of the model through the catch and 0.7 held up -- 115 u tall against his 150: the game's proportion.
`SmsDemoScale` (1 = the game's size) now multiplies that, independent of the size shines are placed at.

**The camera roll.** `TCameraBck::updateDemo` hands `C_MTXLookAt` the cam_position joint's Y axis as the UP vector (unkC column 1); the
joint's frame IS a camera frame (its X axis points exactly from the eye to the interest at every frame -- which also proves the eye
composition under the interest joint's scale 36..42 on Y, `J3DMtxCalcSoftimage::calcTransform`, J3DJoint.cpp 132). Level through the catch,
a -25.4 deg dutch angle from the pose shot on (frame 240). Baked as `CamUpOutside/Inside`; `LookAt(target, up)`. Knob `SmsShineGetRoll`
(default on; off = 116's level camera).

Verified on his 3070 with the GPU capture harness (`patch_sgcap.py` + `run_gpu.ps1`: the level's own shine, frames at 0.3 / 1.5 / 3.0 /
4.0 / 5.2 s -- the star a proper shine in his raised hand at 4 s, the letters out at 5.2 s) and drive 116 20/20 (new: A5b the catch at
1.5 s pins the 60 fps rate -- 53 u up, the table's 53; body scale 0.00675 vs 0.00672; face . toEye 0.54 -- and A7b the pose shot: up = the
bck's (1.000), roll -25.4 deg, fov 28.8, face . toEye 0.99, body scale 0.00949 = 0.71 / 74.44). Regressions 117 9/9, 114 15/15, build 0
errors, boot ALL OK. Files: `Sms/SmsShineGetData.cs` (regenerated: Fps, CamUp, ShineJoint tables), `ShineSprite.cs` (SmsDemoDrive bone
driven, SmsDemoScale absolute, SmsDemoBodyJointWorld/Basis), `Mario.cs` (SmsShineDemoCamera up, SmsShineGetRoll).

Lessons logged: every MActor bck -- Mario's, an object's, a CAMERA's -- is 60 fps data at 30 fps play (rate 2); a captured "neutral pose"
is a snapshot of whatever clip frame the editor was on, never a reference frame for driving a model -- drive the joints from the data.

### SMS pass 116c -- Mario's shine-get clip on the game's clock; the shine's hold measured against the game (2026-10-23)

Jaysonn on 116b: "a bit better, but the shine is behind his hand in the finished pose". Two findings.

**A real bug, fixed.** `SetMarioState(shineGet)` played `ma_demo_shine_get` on the AnimationPlayer without resetting its `SpeedScale`, so
whatever the run had left there (1.25-2.8x from a run or dash, 4x in the drives) ran the 6.97 s clip out early and FROZE its last frame
while the shine kept flying on the demo's clock -- Mario struck the pose and stood in it while the star was still coming down. The game's
`setAnimation(ANIM_DEMO_SHINE_GET, 1.0)` is rate 1.0; now `SpeedScale = 1` on entry. Drive 116 A7c pins it (the clip at 4.59 s at T 4.60).

**The pose itself is the game's, to the unit.** I took the game's own Mario (`ma_mdl1.bmd` + `ma_demo_shine_get.bck`, pulled from the ISO's
`chr0.szs` with gcm_tools.py) and composed its skeleton in Python: at the pose `jnt_hand_R` = (-36, 91, -5), and the port's Godot skeleton
measures (-36, 91, -5) with the same bone axes to two decimals. The shine's bck puts its ROOT joint at exactly (-36.5, 91.4, -4.9) = the
wrist (the animators parented it there), the body joint 81 u out along the root's X, scaled 0.7 = 57 u. FinModelUtility's independent decode
of the same bck (the glb's `shine_demo_shine_get` clip) matches my table at every joint to 0.1 u / 0.1 deg. So the rendered arrangement --
the glove in front of the star's lower-right point, the star body beyond the fingertips -- is what the data draws.

Compared against the game's actual screen (the Mario Wiki's `Shine!.png`, a 640x448 US capture at ~4.7 s of the demo): the star's CENTRE
sits at the same place relative to Mario's face (1.30 vs 1.35 cap widths from the eyes), so the attachment is right; the star's drawn SIZE
in that shot reads 1.2-1.4x ours (the glow blurs the edge; the solid-body measures vary). The decomp's J3D (`J3DMtxCalcSoftimage::
calcTransform`) applies the root's 0.7 to the geometry, so 0.7 is kept as the default; `SmsDemoScale` lifts it if his eye wants the
screenshot's size. Not touched: the hand mesh (hand id 0 = the body's open hands in the game's `gMarioAnimeData[0xCD]`, as the port shows).

Verified: drive 116 21/21 (A7c: SpeedScale 1, the clip on the clock, the shine root on jnt_hand_R to 0.0 u, the star centre 57 u out),
117 9/9, 114 15/15, build 0 errors, boot ALL OK. GPU capture of the 116c demo at 0.3 / 1.5 / 3.0 / 4.0 / 5.2 s.

### SMS pass 116d -- the demo shine drawn at the game's screen size; FLUDD's water keeps falling through the demo (2026-10-23)

Jaysonn on 116c, with screenshots: "the shine is still super small by default, it's not positioned correctly, and the water from FLUDD
froze in the air". 116c had argued the pose from the game's data; the user's eye and the game's own screen said otherwise, so this pass
goes by the screen.

**The star's size.** The bck's root scale track (0.5 through the catch, 0.7 on the pose) applied to the star's geometry, as the J3D maths
reads, gives a star 1.4x smaller than the game actually shows: against the Mario Wiki's in-game capture of this pose (640x448, US), the
star measured 2.15 cap-widths across vs 116c's 1.53, while its CENTRE sat at the same place relative to his face (1.29 vs 1.35 cap
widths from the eyes). So the game's star is the full model with its centre where the scaled chain puts it. Now: the root bone keeps
scale 1 ON the wrist (116c's measured attachment), the children's positions follow J3D (child T x the root's track), and the star's drawn
scale is track / 0.7 x `SmsDemoScale` -- full size on the pose, 0.71 at the catch (where its bottom point sits just at the ground, which
is the authored look: the star resting on the sand in front of him). 116d measures 2.06 cap-widths and 1.35 from the eyes -- within 5 %
of the game's screen on both. `SmsDemoScale` now scales the star about its own joint (116b/c scaled the whole actor and moved the wrist
attachment with it); 0.7 = the J3D maths taken literally. Why the engine's maths and its screen disagree is not resolved; the screen wins.

**The water.** The CameraLocked block returned before `SmsFluddTick`, so `SmsWaterDrops.Tick()` (TModelWaterManager::move) stopped the
moment the demo began: a spray carried into the shine hung in the air for the whole demo. In the game the water manager is its own
perform-list entry and runs every tick whatever Mario's status. Now `SmsFluddDropsTickLocked` ticks the drops inside the lock (nothing
is emitted: gunExec does not run). Drive 116 E1: 30 drops launched at the touch rise from 3.71 to 7.36 m by 0.5 s into the demo; the same
check on the 116c code reads 3.71 -> 3.71 (frozen to the millimetre) -- the control fails as it should.

Verified: drive 116 22/22 (E1 new; A5b / A7b re-pinned to the screen's sizes, A7c the attachment), 117 9/9, 114 15/15, build 0 errors,
boot ALL OK; GPU capture of the pose and the catch at the new size. Files: `ShineSprite.cs`, `Mario.cs`.

### SMS pass 116e -- the demo shine sized from the game's footage: the catch at the bck's 0.5, the pose at 0.9 (2026-10-23)

Jaysonn on 116d: "the shine before Mario grabs it is wrong, and during the shine get it's wrong also" (his game was on the 116d build:
editor restarted 21:38:23, dll 21:38:44, Play 21:39:40). This pass stops arguing from the data and goes by real footage of the sequence
(a 43-frame capture of the indoor variant, decoded frame by frame with the new `tools/projectshine/gif_frames.py`; plus the wiki's outdoor
pose shot):

- The catch / low hold: the star is SMALL -- the bck's 0.5 of the model -- and HOVERS at his hip (its bottom point well clear of the
  floor); Mario walks up to it and spin-grabs, then holds it low in his right hand while the letters come in. 116d had drawn it at 0.71
  and set it on the ground. Back to the track's 0.5.
- The raise (the pose): the star grows to about 0.9 of the model (two captures measured against his cap, corrected for the inside
  camera's wide lens); the bck's scale track says 0.7, i.e. the raise grows the star ~1.8x on screen where the track grows it 1.4x. No
  engine mechanism found (J3D applies the root's scale to the geometry; checked the mtx calcs, the SDL model path, the loader flags). The
  screen wins: the 0.5 -> 0.7 rise of the track (its own keys, frames 211-222) is mapped onto 0.5 -> `SmsDemoPoseScale` (default 0.9;
  0.7 = the data literally, 116c's "super small"). `SmsDemoScale` multiplies the whole thing about the star's own joint.

The attachment and timing are unchanged from 116c/d: the shine's root on `jnt_hand_R`, the star's centre 57 u out along the arm, Mario's
clip at rate 1.0, the water falling through the demo.

Verified: drive 116 22/22 (A5b the catch at the track's 0.5 = 0.00679 a model unit, A7b the pose at 0.9), 117 9/9, build 0 errors,
boot ALL OK; GPU capture of the catch and pose. Files: `ShineSprite.cs`.

### SMS pass 116f -- the star where its joint is, and the SHINE! letters' gradients (2026-10-23)

Jaysonn on 116e, with a screenshot: "the shine is still not in the right spot -- sitting low and offset behind Mario a bit -- and the font
looks off." Both right, both found by measuring his frame against the game's.

**The star sat 48 u below its joint.** Godot's own `UnprojectPosition` of the bck-driven body joint landed 36 px ABOVE the drawn star in the
4.75 s capture, with the wrist and head projecting where they render. The cause is in `shine_sprite.tscn`: the skinned `Mesh_0` node
carries `transform = (…, 0, -48.1984, 0)`, a 48 u drop in the skeleton's frame that Godot adds to the skinned vertices on top of the bones
-- how the placed shine's height was set up, and harmless there, but through the demo it put the rendered star 0.65 m under the joint the
game's animation drives: the star beside the glove instead of above it, "sitting low". Every drive check since 116b measured the JOINT
(right to the unit) and so passed. While the demo drives the bones the mesh node's transform is identity; the shine is freed after the
demo, nothing to restore. Drive 116 A7d now checks the mesh node, not just the joint. Against the game's outdoor capture at the same
moment the star now sits up-left of his head with the glove at its lower-right point, as the game draws it.

**The letters were flat.** The game's SHINE! glyphs are white textures tinted by each pane's four CORNER COLOURS (big_tx_1.blo PIC1, after
the black/white pair -- blo_dump.py stopped before them): a vertical gradient per letter, light at the top, saturated at the bottom
(S 00ff00 -> 006400, H ffdcdc -> ff0064, I ffff50 -> e68c00, N 00ffff -> 001eff, E ffd264 -> ff0000, ! ffdcdc -> ff0064; the file's first
pair is the bottom on screen). 116 tinted them flat from his palette. Now a canvas_item shader per letter multiplies the glyph by
mix(top, bottom, v) with v the glyph's own vertical coordinate (so it rotates with the pane's 12 deg, as the game's does); the black outline
stays black. Knob `SmsShineGetGradient` (off = the flat palette). Letter size and spacing were already the game's (0.12 of the frame height,
the blo's homes).

Verified: drive 116 23/23 (A7d new), 117 9/9, build 0 errors, boot ALL OK; GPU capture at 4.75 s with Godot's projections matched to the
drawn star. Files: `ShineSprite.cs`, `Sms/Console/SmsConsoleStr.cs`.

### SMS pass 118 -- after the shine: leave the level or stay; the SHINE! letters lean the right way and spark (2026-10-23)

Jaysonn, on 116f ("that looks right"): the shine should have an option to transition out of a level with the game's white fade; otherwise
Mario pauses a moment and play resumes; and the letters lacked their effects ("stars and such") and were tilted the wrong way.

**What the game does after the demo** (TMarDirector::updateGameMode, MarDirectorDirect.cpp 839): it registers the TShineFader's fade --
`registFadeout(1 s x 120, 5.3333 s x 120)`, and the fader's colour is `(0xD2, 0xD2, 0xD2)` (MarDirectorSetup2.cpp 76), a near-white, not the
black 116 had -- and sets the stage transition; `decideNextStage()` is Delfino Plaza for EVERY shine (stage 1, scenario by the story flags), the
Plaza included, which simply reloads. The inner demo ends with the camera bck (419 frames = 6.98 s; Mario's own ma_demo_shine_get is 6.97 s)
and moveStage runs then, behind the white. There is no "stay" in the game; a shine that keeps you in the level is Jaysonn's deviation, so
it is the shine's option:

- `ShineSprite.ExitLevel` (off) + `ExitScene`: on, the (210, 210, 210) fade from 5.333 s over 1 s, the white held, and at 6.98 s the scene
  changes -- `ExitScene`, else the pause menu's Exit Area scene, else this level reloaded (the Plaza's own case). `Mario.SmsShineGetExit(scene)`
  fires right before (a hook for saving). Off: no fade; the camera plays to its end and control returns where he stands.
- Knobs: `SmsShineGetFadeColor` (the D2 grey-white), `SmsShineGetFadeAt / Len` (5.333 / 1), `SmsShineGetEndAt` (0 = the camera's end).
  `SmsShineGetResume` and its fade back in are gone.

**The letters leaned the wrong way.** J2DPane::makeMatrix (J2DPane.cpp 256) applies `-mRotation` about Z, so the blo's ROT 12 leans the
letters' tops LEFT on screen; 116 passed +12 to Godot and leaned them right. The I gets the same tilt: its pane in the US blo reads 417 x 51
with no ROT (unreadable), and the footage tilts it like its neighbours.

**The effects.** processShineGet (ConsoleStr.cpp 570) makes two JPA emitters per letter at its pane's centre: 0x1FE when the letter is called
in (the footage: white speed lines shooting left from where it will land) and 0x1FF sixty ticks later, when it has settled (small pastel
stars flung out, falling and fading). The particle files are not ported; both are APPROXIMATED in code from the footage (sizes and speeds
in the blo's pixels, the lines drawn under the letters so they never slice one still flying in, the stars over them). Knobs
`SmsShineGetSparks`, `ShineStreakCount` (6), `ShineStarCount` (10), `ShineStarColors` (white, pink, pale yellow).

Verified: drive 118 28/28 (the 116 timeline through the pose, the stay case to 6.98 s with no fade, every letter at -12 deg, the sparks' two
moments, the ExitLevel case's half white at 5.83 s in (0.824, 0.824, 0.824), full at 6.4 s, the scene change at 6.98 s seen from the reloaded
level), 117 9/9, build 0 errors, boot ALL OK; GPU captures at 3.1 / 3.25 / 3.6 / 4.0 / 6.9 s viewed against the game's frames. Drive 118
supersedes drive 116. Files: `Mario.cs`, `ShineSprite.cs`, `Sms/Console/SmsConsoleStr.cs`.

### SMS pass 118b -- any word on the shine: a per-shine ShineText, the colours cycling, a long word shrunk to fit (2026-10-24)

Jaysonn: "font looks great! we made it scale to be anything even something like wonderful!?" -- then: add a per-shine ShineText override.

The word was already free (`SmsConsoleStr.ShineText`, every letter on the game's timing, lean and sparks; he has glyphs for A-Z and !), but
only the six gradient pairs existed (letters past the sixth fell to white), nothing shrank a long word, and the word was the console's,
not a shine's. Now:

- `ShineSprite.ShineText` ("" = the console's word, the game's SHINE!): the shine's own word, handed to the console as its demo starts; the
  console rebuilds the letters whenever the word changes.
- The six corner-colour pairs cycle past the sixth letter (WONDERFUL!: W green, O pink, N yellow, D blue, E orange, R pink, F green ...).
- A word whose right edge would pass `ShineMaxRight` (0 = the window's right edge in the blo's pixels, less 30) is built smaller about the
  first letter's home -- letters and steps alike -- so it fits. SHINE! never shrinks; WONDERFUL! (ten letters) fits a 16:9 window at full
  size; CONGRATULATIONS! (sixteen) comes out at 0.69 of the size with its edge at the window's.

Verified: drive 118 31/31 (B says WONDERFUL!: ten letters, cycling, full size, inside the window; C says CONGRATULATIONS!: sixteen, shrunk to
the edge; D back to the console's SHINE!, six letters rebuilt), 117 9/9, build 0 errors, boot ALL OK; a GPU capture of WONDERFUL! at 4.1 s
viewed. Files: `Mario.cs`, `ShineSprite.cs`, `Sms/Console/SmsConsoleStr.cs`.

### SMS pass 118c -- shine flags and the save (2026-10-24)

Jaysonn: "Do the shine flags and save". Nothing remembered a shine: GameData saved blue coins by id but no shines, the count lived on Mario and
died with the scene, an ExitLevel reload brought the same shine back.

**The game** (TFlagManager::setShineFlag, FlagManager.cpp 308): the pending step of the shine get sets the shine's flag (MSF_SHINE_BASE +
event id) and raises MSF_SHINE_COUNT by one ONLY if the flag was clear, then marks the card save (PURPOSE_SHINE_GET: the dialog in the
Plaza). TShine::makeMActors loads `shine_empty.bmd` for a shine whose flag is set -- it stands where it was, see-through, and its promi
(flare) and bow (rainbow) emitters are skipped; touching it plays the same demo (`shine_empty_demo_shine_get`) and the count stays.

**Port:**
- `GameData`: a saved set of shine keys (`"shines"` in user://save_data.json beside the blue coins) -- `CollectShine(id)` (true when new, written
  at once), `IsShineCollected`, `ShineCount`; F8 clears it with the rest.
- `ShineSprite.ShineId` -- the flag key. Empty: made from the scene the shine is placed in and its path there, so every placed shine is
  remembered without setup; a spawned shine has no scene, so its spawner sets it (the red coin switch: `<scene>:<switch>:red_coins`), else
  it is counted for the run only. `SmsIsEmpty` at load -> the empty look: the body's gold texture dropped for a glassy EmptyTint (0.72,
  0.82, 0.95, 0.55) with a lit rim, no rays and no ring, the light halved; eyes and sparkles stay.
- `Mario`: the count starts from the save (`_shines = GameData.ShineCount`); the flag is set at the touch (StartShineGet, both demo paths); the
  2 s count-up adds one only for a fresh flag. The save dialog itself is NOT ported (the write is immediate, as the blue coins' already is).

Verified: drive 118 35/35 -- E's shine carries a key: set at the touch and in the file at once (E0b), across the ExitLevel reload the new
Mario's count is the saved set's (F1), the same key placed again loads empty (F2), taking the empty one plays the demo and leaves the count
and the file alone (F3); his save file is put back by the drive. 117 9/9, build 0 errors, boot ALL OK; a GPU frame of the empty shine beside
a fresh one viewed. Files: `Mario.cs`, `ShineSprite.cs`, `GameData.cs`, `RedCoinSwitch.cs`.

### SMS pass 119 -- where he lands after leaving a stage: the return spot and the gate-out (2026-10-24)

Jaysonn: "lets do 1, 2, 3" -- this is 2, the spawn on exit.

**The game.** Leaving a stage sets MSF_GOT_SHINE_IN_PREV_STAGE (a shine) and the previous area; the next stage's director picks the
"マリオセット位置" entry (TMarioPositionObj: the Plaza's return spots, one per stage -- MarDirectorSetupObjects.cpp 80) for the stage he left and
calls `TMario::returnStart(pos, rot, gotShine, type)` (MarioAutodemo.cpp 318): startCommon warps him there facing `rot` with a shine or
`rot + 180` without, then `warpOut()` (368): ANIM_DEMO_GATE_OUT_APPEAR(_GET) plus the gate particle, held 0xB4 = 180 frames, then
ANIM_DEMO_GATE_OUT_ROLLING(_GET) under jumpProcess -- he drops from the spot -- and on the floor ANIM_DEMO_GATE_OUT (_GET2, the shine
pose) to its last frame, then WAIT. No intro runs; the screen comes in with startWipe(7) = Hx_Test4, the spiral.

**Port.**
- `SmsReturnSpot` (new, `assets/Sms/SmsReturnSpot.cs`, a Node3D marker): `FromScene` = the level it is the return for ("" = any level
  without its own). Place one per level you can come back from; he faces its forward (-Z) with a shine, the opposite way without, and drops
  from it, so a spot in the air falls as the game's do.
- `GameData.ReturnFromScene / ReturnWithShine` (run-scoped): set by an ExitLevel shine's exit (with the shine) and the pause menu's Exit Area
  (without), read by the next level: `Level` skips its intro (black and a plain 0.4 s fade in -- the spiral is NOT ported) and Mario, on his
  first tick, finds the spot and runs the gate-out in the demo state (`SmsGateOutHoldSeconds` 3.0 for the hold). The gate particle is not
  ported. No spot: the plain start, a note in the log. Mario clears the flags once used, as setMario does.

Verified: drive 119 13/13 -- the first tick after the load has him at the spot in the demo state with the right clip and facing, the fader
black then in; 2.8 s in he still hangs there (the appear clip over, its last frame held); past 3 s the rolling clip and the fall; on the floor
the gate-out clip (the pose with a shine, the plain one without); then idle with control; a return with no spot = the plain start, intro still
skipped. Drives 118 35/35 and 117 9/9 on the new code, build 0 errors, boot ALL OK; a GPU capture of the gate-out (hanging, tumbling, the
pose) viewed. Files: `Mario.cs`, `GameData.cs`, `Level.cs`, `Sms/Console/SmsPauseMenu.cs`, `Sms/SmsReturnSpot.cs` (new).

Note: between two drive runs the level's own shine key turned up in the save file once (the save showed one shine Jaysonn did not collect);
no run reproduces it and no log records a collection. The save was put back clean; both drives now back it up and restore it. F8 clears it.

### SMS pass 119b -- the return spot never matched: the editor saves scene paths as uid:// (2026-10-24)

Jaysonn placed an SmsReturnSpot in Level.tscn pointing at the level itself and a shine with ExitLevel + ExitScene = the level: "mario just
resets like normal". His scene file shows why: Godot 4.4+ saves a `PropertyHint.File` string export as `"uid://cwve1f1kpa2pf"`, while a
scene's `SceneFilePath` is `"res://assets/Level.tscn"` -- the spot's FromScene could never equal the level he left, so every return fell back
to the plain start (the exit itself worked: ChangeSceneToFile takes a uid).

- `SmsReturnSpot.ScenePath()` resolves either form through `ResourceUid.EnsurePath`; `Find` compares resolved paths; the shine's `ExitScene`
  and the pause menu's `ExitAreaScene` are resolved the same way before the scene change.
- A shine that reloads its own level (ExitScene empty and no Exit Area scene) records the return too, so a spot made for that level catches
  it; for a same-level reload only a spot made for it counts (no "any level" fallback) -- the game's Plaza reload is the plain start.

Verified: drive 119 13/13 with the plain-return run's spot naming its level by uid://, 118 35/35, build 0 errors, boot ALL OK. Files:
`Mario.cs`, `Sms/SmsReturnSpot.cs`. (The "shine in the save I did not collect" note in 119 was his own play-test with the 118c build --
the save is right; the drives back it up and restore it.)

### SMS pass 120 -- the gate stream: ms_m_gout_gate on the return (2026-10-24)

Jaysonn, on the return: "it looks kinda off? also where are the particle effects when spawning in?" The spawn effect is the game's
`ms_m_gout_gate.jpa` (TMario::warpOutEffect kind 0, MarioParticle.cpp 494: emitWithRotate at his feet, the emitter turned to the spot's
rotation whether or not a shine sent him), and it is what the 3 s hold reads as: the appear clip keeps Mario at scale zero for 0.9 s, pops
him in spinning over half a second and holds its last frame -- in the game inside the stream, in the port in empty air.

**The file** (jpa_dump.py): a 60 u sphere emitter 3.3 m behind the spot and 0.7 m low, 10 beads a frame for 30 frames, life 60, launched
40 u/f up-and-forward under airResist 0.96 and a moment of 0.75 +- 0.125; a Newton field toward (0, 120, 350) (mag 0.3, 1/d^2 beyond 100 u,
faded in over the first half of life, accumulating); a Magnet field toward (0, 110, 350) (20 u/f from half life, faded in to the end); a
Vortex that never acts (enabled from progress 1.0). 25 u beads along their velocity, alpha 0 -> 1 by 25 %, -> 0 from 75 %; the colour is
the TEV's lerp(env, prm, intensity) with BOTH colours keyed over a 23-frame cycle -- red, yellow, blue held eight frames, salmon, white,
brown -- each bead at a random phase; sweep children from 87.5 % of life, one every 2 frames, 55 u shrinking over 16 frames.

**Port:** `assets/Sms/Fx/SmsGateStream.cs` (new) runs the jpa on the CPU exactly as JPAField.cpp / JPAParticle.cpp do -- one particle
frame a 60th, fields in the emitter's frame, field acceleration accumulating, field velocity per frame, air resistance on the base
velocity, moment on the sum -- and draws the beads as one MultiMesh of velocity-aligned quads with a spatial shader doing the TEV lerp
(prm in the instance colour, env in the custom data). Texture: the game's `P_msm_mizutama1_ia` baked raw (`assets/Sms/Fx/tex/
gout_drop_raw.png`; a drawn bead stands in until the editor imports it). Spawned by `SmsGateOutStart`; frees itself when spent (~1.8 s).
Knob `SmsGateOutStream`. Not ported: the pipe variant (ms_m_gout_dokan), the body-part sparkles (kind 2), the children's field response.

Verified: drive 119 19/19 (the stream spawned at his feet with the appear, 240-250 beads alive at 0.4 s, gone by 2.8 s), 117 9/9, build 0
errors, boot ALL OK; a GPU capture of the stream rising behind the gate at 0.3 s and Mario popping in at 1.6 s viewed.
Files: `Mario.cs`, `Sms/Fx/SmsGateStream.cs` (new), `Sms/Fx/tex/gout_drop_raw.png` (new).

### SMS pass 120b -- no hang at the gate: the hold is 1.5 s, the appear clip's own length (2026-10-24)

Jaysonn: "looks good! ... mario does hang for a second floating in the air and doesn't seem right." He was right, and the data agrees once
the clock is read properly: warpOut's 0xB4 = 180 is counted in Mario's status ticks, and Mario performs on the director's 120 Hz movement
ticks (the console's unk18 += 0.5 a perform, TShineFader's mRate 120 and the hit check every 4th tick all run on that cadence), so the hold
is 1.5 s -- which is the appear clip's 89 frames at 60 to the frame: he spins in and goes straight into the rolling fall. 119 held him 3.0 s
(a 60 Hz assumption), so he hung a second and a half in the last frame.

- `SmsGateOutHoldSeconds` 3.0 -> 1.5.
- The return's fade in now lasts the spiral wipe's 38 wiper frames at 30 Hz (1.27 s) instead of the miss's 0.4 s, so the appear clip's
  invisible first 0.9 s passes under the fade as it does under the spiral in the game.

Verified: drive 119 19/19 on the new timeline (still hanging at 1.3 s with the appear clip playing, falling by 1.75 s, the whole return over
in 5.1 s), 117 9/9, build 0 errors, boot ALL OK. Files: `Mario.cs`, `Level.cs`.

### SMS pass 121 -- the cap off in the return pose (2026-10-24)

Jaysonn: "now we gotta have mario take his hat off just like in game when he does the ending pose!"

**The game** (TMarioCap::perform, MarioCap.cpp 137; TMario::calcAnim, MarioDraw.cpp 1964): while the clip is ANIM_DEMO_GATE_OUT_GET2 and
its frame is under 157 the head cap (ma_cap1.bmd) is hidden and the hand cap (ma_cap3.bmd, on the right hand joint) shown, the body model's
open-hand shapes shown; from frame 157 the cap is back on the head and from 158 the right hand becomes the ma_hnd4r model for the last 24
of the clip's 181 frames. Only this clip does it -- the plain gate-out keeps the cap on.

**Port.** A new hand pose `WithHat`: the scene's `RightHandWithHat` (his hand-holding-the-cap model under the right hand bone, hidden until
now) shown, the open right hand hidden, the left hand open; and the body mesh's cap -- the dome at the top of the head in Mario.glb, the
surface whose material is `SmsCapSurfaceMaterial` ("_mat_head(7)") -- hidden by a transparent override. Both for frames < 157 of
`ma_demo_gate_out_get2`, restored from 157 and at the pose's end; the hair under the cap shows meanwhile (it is in the head mesh). Another
character's mesh names its cap surface differently: set the knob, or the cap stays on. hnd4r for the last 24 frames is not ported (the open
hand).

Verified: drive 119 22/22 (cap off and in hand through the pose, back on from frame 157 with the clip still playing, on again at the end, the
plain gate-out untouched), 117 9/9, build 0 errors, boot ALL OK; a GPU capture of the pose at 2.6 / 3.5 / 4.4 / 5.0 s viewed -- the cap in
his hand, his hair showing, the cap back on. File: `Mario.cs`.

### SMS pass 122 -- the top of the head while the cap is off (2026-10-24)

Jaysonn (after 121): "the hat is working but mario is missing the top of his head?" He was right: the capture showed the head cut flat
at the hairline with the scenery through it. Mario.glb (= ma_mdl1.bmd, 11 shapes) has no top under the cap -- and neither does the
game's body model. The game never draws one from the body: TMarioCap holds TWO head models placed at the M_head joint (MarioCap.cpp
20-68): ma_cap1.bmd, the cap (159 tris -- the body's own cap shape 4 is the same geometry, hidden for good at MarioDraw.cpp 1157), and
ma_cap3.bmd, material `_mat_hair`, 151 tris: the HAIR DOME, the capless head top. perform() (137-146): get2 frame < 157 -> cap1 hidden,
cap3 shown; else cap1 shown, cap3 hidden. (In SMS's J3D a shape with flag bit 1 SET is skipped by J3DJoint's draw, so
onFlag1OnAllShapes HIDES -- the JSystem header's comment reads it backwards; the hnd/hand toggles in changeHand read consistently only
this way.) calcAnim (1964-1982): the right hand is ma_hnd4r (hand + cap: 328 = 169 + 159 tris) while the frame < 158, hnd3r after -- one
frame past the head's 157; the left is hnd3l throughout.

**Port.** New `assets/Sms/SmsCaplessHead.cs` (generated from his ma_cap3.bmd by `gen_capless_head.py`: 106 vertices, 151 triangles,
positions / normals / UVs the bmd's own, in the M_head frame, Godot's clockwise winding): `Build(material)` -> an ArrayMesh. Mario
builds it once under a BoneAttachment3D on `M_head` (the game's attach joint; `jnt_head` if missing) with the head surface's material
(knob `SmsHeadSurfaceMaterial`, "_mat_head(8)" -- the game gives cap3 the body's texture, and its UVs land in the atlas's hair strip),
shown while the cap surface is hidden, hidden when the cap is back; `SmsCaplessHeadDome` turns it off. The hand's cap now leaves at
frame 158 (`SmsHandCap`), the head's at 157, as the game. Nothing in the scene to touch; the other characters' meshes keep their caps
unless they name the surfaces.

Verified: drive 119 25/25 (S4d the dome on M_head, visible, 151 tris, the head's material while the cap is off; S4e hidden with the cap
back and the hand open from 158; P4d never built on the plain gate-out), 118 35/35, 117 9/9, build 0 errors, boot ALL OK; GPU capture
`capless122.png` -- full hair through the pose, the cap back at 5.0 s with no double. Files: `Mario.cs`, `assets/Sms/SmsCaplessHead.cs`.

## SMS pass 123 -- the hidden coin and the coin's game life (TItem / TCoin / the hide objects)

**Files:** `assets/Sms/SmsHiddenCoin.cs` (new, `[GlobalClass]` -- a node type in Add Node after the editor's Build), `assets/Sms/SmsCoinItem.cs` (new),
`assets/BlueCoin.cs`, `assets/YellowCoin.cs`, `assets/Sms/Fx/WatCoinHit.tscn`, `WatCoinKira.tscn`, `drop_mix.gdshader` (generated).

**Game (doldecomp/sms, MoveBG/Item.cpp + MapObjHide.cpp + MapObjGeneral.cpp):** a hidden coin lives in a HIDE OBJECT -- an invisible spot
that throws it out when you do the right thing to it. Four plain kinds and the graffiti picture:
- **WaterHit** (hit cylinder r 60 x h 20 x scale): any FLUDD drop -> the coin 50 units up, the spot spent (makeObjDead).
- **HipDrop** (r 100 x h 60): a ground pound on it -> the coin at the spot.
- **HeadHit** (r 100 x h 60, 10 units under the point): a jump up INTO it from below, rising -> the coin 100 units up (the hidden block).
- **FruitHit**: a fruit touching it -> the coin 50 up, the fruit destroyed.
- **Picture** (the red M, KoopaJrSignM): its alpha is a progress 255 -> 20; every drop hit takes 1.8 off (the game: 2.8 on a floor, 2.5 on
  a ceiling), it heals 0.4 a tick while you do not spray, under 20 it fades out and the coin comes out; when THAT coin dies by its timer the
  picture is back and sprayable again -- the same blue coin comes every time until it is collected.
The throw (throwObjToFrontFromPoint): along the spot's forward at the stage's appear speed x 0.06 units a tick + 20 up (the default; Bianco Hills
uses 0 or 100 -> 6 u/tick, y 20 / 10, disappear 1200 ticks). The coin then flies by the game's coin physics (coin_physical_data: gravity 0.35
u/tick^2, bounce x -0.5, 0.5 on the ground speed at a contact, 0.99 air drag, lands under 1 u/tick, no wall or roof collision) at 120 Hz: a
20 u/tick pop reaches 561 units (7.5 m) in half a second, bounces 20 -> 10 -> 5 -> 2.6 -> 1.3, rests in ~2 s. Untouchable for its first 120
ticks (TItem::appear). **Which coins disappear:** TItem::load marks every stage item "never" (MAP_OBJ_FLAG_UNK10000000); a yellow pool coin
clears it (TCoin::appearWithoutSound) -> 480 ticks for a drop, the spot's value when hidden; a graffiti picture clears it for ANY coin it hides
(1200). So a blue coin out of a plain sprayed / pounded spot STAYS; one out of a graffiti M goes after 10 s, blinking its last 3 s (hidden while
(timer / 10) is odd), then MSD_SE_SY_COIN_DISAPPEAR + the picture re-armed. A blue coin already got (its flag) reveals nothing (the spot still
dies to a spray; a picture is dead from the start). The spin is the item manager's ONE matrix: 2 deg a tick (240 deg/s), every coin in phase.
The collect: TMario::getCoinBlue incHP(2); fireGetBlueCoin sets the flag and saves (TIMECOIN_APPEAR plays only for a blue not yet got).

**Port:**
- `SmsHiddenCoin` (StaticBody3D): `Kind`, `HiddenScene` (blue_coin.tscn by default; yellow_coin.tscn for the game's common case), `CoinId`
  (empty = derived from the scene + node path, like a ShineId; given to the revealed BlueCoin so the collect saves it), `AppearSpeed` (the
  stage number; x 0.06), `AppearYSpeed` (-1 = 20), `DisappearTicks` (0 = 1200), `CoinDisappears` Game / Always / Never, `HitEffect`,
  `CoinSparkle`, `AppearSound` (slot), `AutoLayer` (layer 8, no mask: Mario's mask 5 never touches it, the drops' ray does); Picture:
  `PictureMesh` (empty = a `PictureSize` quad facing the forward with `PictureColor` / `PictureTexture`), `SprayProgressSpeed`, `PictureMin`,
  `PictureMax`, `HealPerTick`. Place it like a marker: forward (-Z) = where the coin flies; a Picture's quad faces the forward.
- `SmsCoinItem`: the item life as a state the coin scripts carry (`Throw`, `Tick`, `SharedYaw`, `Spin`); `IsMap` = the level only.
- `BlueCoin` / `YellowCoin`: `SmsItemLife` (on: a thrown / dropped coin uses the game's life; `LaunchAsDrop` from a nail = TSmallEnemy's
  (0, 20, 0) pop -- a yellow then lives 480 ticks, a blue for good), `SmsSpin` (the shared 240 deg/s; off = SpinSpeed), BlueCoin `SmsHealTwo`,
  slots `SmsBoundSound` / `SmsDisappearSound`. Placed coins are untouched (static, as the game's). RedCoin.cs left alone (the switch's coins).
- Effects from the disc's own .jpa: `WatCoinHit` (ms_watcoin_hit: 10 water drops out of a 10-unit sphere at 12 + 4 up u/f, gravity 0.5 faded
  in, 0.75 billboards lerp((150,199,231) -> white, I) x A, mix blend) at the spot; `WatCoinKira` (ms_watcoin_kira: a 50-frame sparkle of
  1.3 x 2.0 stars, yellow -> cyan at a random phase, additive, twinkling, 80 units over the coin) riding the coin. `drop_mix.gdshader` is
  the mix-blend sibling of kira_add.

**Verified (drive 123, 22/22, headless on the Level):** the body's layer and the two rays; the WaterHit reveal (position, 3 + 20 u/tick,
untouchable 120, never disappearing, both effects, the spot spent); the already-got spot refusing; the yellow's 240-tick life with the blink
and the death; the apex at 561.4 u; touchable again at 1 s in flight; at rest on the floor + 1 u after 5 bounces, carried forward; the pound,
the head hit (falling: nothing; rising: 100 up), the fruit (killed); the picture: 10 hits -> 237, healed to 255 in a second, 131 hits -> 20,
the coin with the id and a 240-tick life, a drop refused while finished, the coin's death re-arming it (healing from 20), the same coin
again; the shared yaw on two coins; the collect: +2 HP, the flag written, the count. Regressions 119 25/25, 118 35/35, 117 9/9; boot ALL OK.

**Not ported:** the talk-mode freeze of a resting coin, the barrel hit on a picture, the picture's back-face rule and the M's own texture
(PictureTexture is yours), the twin pictures and their demo camera, the shine kind of hidden object (place a shine as the HiddenScene and it is
placed, not thrown), the sounds (slots: TIMECOIN_APPEAR / COIN_APPEAR, COIN_BOUND, COIN_DISAPPEAR, BLUE_COIN_GET). The game emits the hit
burst twice for a fruit spot (touchFruit + appearObjFromPoint) -- once here.

## SMS pass 123b -- the water never reached the spots (two bugs under "nothing is happening")

**Files:** `assets/Mario.cs` (one line), `assets/Sms/SmsWaterDrops.cs`, `assets/Sms/SmsHiddenCoin.cs`.

Jaysonn put a WaterHit `SmsHiddenCoin` on a sign and sprayed it: nothing. Drive 123 had called the spot's handler directly instead of spraying
real water, so it never saw either problem:
1. **The drops could not see the spots.** `SmsFluddSetupWater` built the drop manager with Mario's own collision mask (layers 1 + 3), and the
   spots sit on layer 8 precisely so Mario never bumps into one -- every drop's ray passed straight through them. The mask now carries the hide
   layer too.
2. **A ray is the wrong test anyway.** The game (`TObjHitCheck::checkWater`, ObjHitCheck.cpp 97) treats every flying drop as a cylinder of
   r 50 x h 80 (`TModelWaterManager::mStaticHitActor`) and tests it against each actor's damage cylinder with `checkDistance`: the drop is taken
   when it is no higher than the spot's top, no lower than 80 units under its base, and within 50 + the spot's radius in XZ. For a WaterHit spot
   (60 x 20) that is a 2.2 m wide, 1.3 m tall zone -- a drop landing on a sign anywhere near the spot counts, and so does one passing beside it.
   My ray only fired when a drop's own path crossed the 27 cm puck. `SmsWaterDrops.Tick` now asks `SmsHiddenCoin.WaterZoneAt(p)` for every flying
   drop before its step (move() 424: a taken drop dies there); the picture uses the game's KoopaJrSignM cylinder (150 x 300, centred on the point).
   One rule to know: a drop ABOVE the spot's top is refused -- the spray arcing over a ground spot does nothing; it must come down to it.

**Verified (drive 123 now 26/26):** R0 the mask; R1 real water from the nozzle on a ground spot placed where the standing spray lands (9.9 m out)
-- taken 16 frames after the spot appeared; W0 the cylinder rule to the unit on all six edges; W1 HIS case: a spot on the face of a 3 m wall,
Mario 3 m back pressing R with no water in the air -- the coin out 3 frames later. Regressions 119 25/25, 118 35/35, 107 15/15 (on the committed
Level: his uncommitted level puts a NozzleBox in that drive's run lane), 106 16/18 -- the same two fail on the 122 Mario.cs with the committed
level, so not this pass (T2b/T3: the bounce off the Toad rises 252 u; noted for the next Mario pass). Build 0 errors, boot ALL OK.

## SMS pass 123c -- pick how the coin flies out (and see it in the editor)

**File:** `assets/Sms/SmsHiddenCoin.cs` (now a `[Tool]` script).

Jaysonn: "can we make it so we pick how it flys out?" -- yes, and it fixes a deviation of mine: 123 flattened the node's forward to the horizontal,
while the game's `throwObjToFrontFromPoint` throws along the hide object's FULL forward (anmMtx column 2 -- a pitched spot throws up or down).
- `FlyOut`: **Forward** (the game: along this node's blue arrow, tilt included), **Up** (straight up, AppearSpeed ignored), **TowardPlayer** /
  **AwayFromPlayer** (the horizontal line to Mario at the moment of the reveal), **Custom** (`FlyDirection`, a vector in the node's own frame --
  rotating the node turns it). The speed along the direction is still `AppearSpeed` x 0.06 units a tick (100 = 6 u/tick = 9.7 m/s, a throw
  landing about 3 m out; 200 = ~6 m), the lift `AppearYSpeed` (default 20 = a 7.5 m pop). AppearSpeed 0 pops straight up in every mode.
- **The flight line.** In the editor each spot draws the coin's predicted flight (the coin's own physics: gravity 0.35, drag 0.99, two seconds,
  from where the coin will appear -- 50 units up for a sprayed / fruit spot, 100 for a head-hit, the spot itself for a pound, 30 out of a
  picture's face), redrawn 5x a second as you move the node or turn a knob; a yellow cross marks the appear point. `ShowFlightPath` hides it. In
  play the line shows only with `DebugPrint`. The line is a runtime child with no owner, so it is never saved into your scene. TowardPlayer
  previews toward a node named Mario in the open scene, if there is one.

**Verified (drive 123 now 29/29):** L1 the five modes' velocities on a node yawed 90 and pitched 30 (Forward carries the pitch; Up; toward /
away along the horizontal line to a given Mario; Custom along the node's +X); L2 the predicted path's apex = the real coin's 561.5-unit pop and
its start = the appear point; L3 the line is a runtime, owner-less, top-level ImmediateMesh. Build 0 errors, boot ALL OK. Not editor-tested
headless: the drawing itself in the editor is for Jaysonn's eye (a [Tool] script runs there after the Build).

## SMS pass 123d -- the graffiti erodes like the game's (and splashes, and is gone when the coin comes)

**Files:** `assets/Sms/SmsHiddenCoin.cs`, new `assets/Sms/Fx/graffiti_erode.gdshader`, new `assets/Sms/Fx/tex/jr_picture_a.png` (the game's M).

Jaysonn: "i dont see the effects of hitting a picture and its not disappearing like it does in sms its just getting more transparent ... when the
coin spawns it still shows the faintest alpha of the picture." All three are the game's material, read from `koopa_jr_sign.bmd` in the Bianco
archive on his ISO (bmd_mat_dump): ONE TEV stage, colour = GX_TEVREG0 (the stage's picture colour -- the texture's colour is never read, the
`Q_jr_picture_IA8` 32 x 32 has I = 0 everywhere; its ALPHA is the M with soft edges), alpha = TEXA x A0 (the register alpha = mProgress) with
the stage's scale x4, blend NONE, **alphaCmp GEQUAL 128**. So a texel draws only while texA x progress x 4 >= 128: the M does not fade, it
**erodes from its soft edges inward** -- 253 of 1024 texels at 255, 183 at 128, 110 at 64, 5 at 33, NONE at 32 -- and is completely gone before
the game's minimum of 20 (which is why 20 is the minimum: nothing is left to see). Each drop that hits emits PARTICLE_MS_ENM_WATHIT at the drop and
plays MSD_SE_EN_COMMON_W_HIT_OK (touchWater 349). The quad is 300 x 300 units (4.03 m; the Bianco signs stand at scale 2 and 1.5 on top).

**Port:**
- `graffiti_erode.gdshader`: the TEV and the alpha test to the register (`tex`, `color`, `progress`, `alpha_scale` 4, `alpha_ref` 128/255),
  lit, opaque, `discard` under the threshold. The spot's auto quad now wears it with the game's M alpha (`jr_picture_a.png`, white RGB so
  PictureColor is the colour) and the game's 300 x 300 units (PictureSize 0 = that; set it for another size). Your own `PictureTexture`
  works the same way: its alpha is the shape that erodes, its colour multiplies PictureColor.
- Every hit: the water-hit mark from pass 99 (`SmsWaterHitMarks.Emit` at the drop; the pool loads with the level) and the new `HitSound` slot.
- FINISHED hides the mesh outright (the alpha test already drew nothing at 20 -- no ghost under the coin); the regrow shows it again. Your own
  `PictureMesh` still fades by transparency (no erosion without its material) and is hidden / shown the same way.

**Verified (drive 123 now 30/30):** G1 the quad's shader, texture and size, `progress` 237/255 after ten hits, ten marks emitted; G1b the alpha
test on the real texture on the CPU (253 / 183 / 5 / 0 / 0 texels at 255 / 128 / 33 / 32 / 20); G2 healed and drawn in full; G3 finished = hidden,
the coin out; G5 regrown = drawn again. The real-spray phases also reached this picture 8 m away through the zone test (counted). Build 0 errors,
boot ALL OK. The look of the erosion on screen is for Jaysonn's eye (headless draws nothing).

## SMS pass 123e -- the graffiti dissolve knob (a deviation: the game's erosion is all outline until the last 18 drops)

**Files:** `assets/Sms/Fx/graffiti_erode.gdshader`, `assets/Sms/SmsHiddenCoin.cs`.

Jaysonn: "it feels like the erosion kinda doesnt work untill right before its done ... seems like it need more random erosion?" He read the game's
curve correctly: by koopa_jr_sign's alpha test the first ~110 of the 131 drops only nibble the soft outline (100 % -> 65 % of the fresh pixels by
drop 81, 50 % by drop 97) and the whole body goes in the last ~18 (34 % at drop 108, 0 at 124). Faithful, and not what he wants.

- `ErosionNoise` (0..1, default **0.6**; 0 = the game's exact look) adds a random dissolve ON TOP of the game's test: a two-octave smooth value
  noise over the quad (`ErosionGrain` cells across, default 6 -- small = a few big ragged holes, large = fine speckle) removes a pixel once its
  noise, stretched over the noise's own 0.35..0.78 band so the knob reads as a share of the body, falls under ErosionNoise x the erosion
  fraction (0 fresh -> 1 at the game's finish of 20/255). Holes open from their centres and grow as you spray. The finish is unchanged.
- Measured on the game's texture at the quad's pixel scale (`erosion_curve.py`, the shader's own hash / noise reproduced): fresh pixels still
  drawn at drops 31 / 47 / 64 / 81 / 97 / 108 / 117 / 124 -- game 92 / 83 / 75 / 65 / 50 / 34 / 18 / 0 %; knob 0.6: 86 / 75 / 63 / 49 / 32 /
  18 / 8 / 0 %; knob 1.0: 81 / 61 / 43 / 25 / 9 / 3 / 1 / 0 %. So at 0.6 a quarter of the body is gone by drop 47 and half by drop 81.

**Verified (drive 123 now 31/31):** G1c the three uniforms reach the material (0.6 / 6 / 20/255) from the node's exports. Build 0 errors, boot
ALL OK, no shader errors. The feel is his to tune: `ErosionNoise` up for more holes earlier, `ErosionGrain` for their size.

## SMS pass 124 -- the card save (feature/save-prompt): TCardSave's "Save?" windows after a shine, a blue coin, and the pause menu's SAVE

**Files:** `assets/Sms/Console/SmsCardSave.cs` (new), `assets/Sms/Console/SmsTextBox.cs` (new), `assets/Sms/Console/CardSave.tscn` (new, instanced in
`assets/Player.tscn` beside PauseMenu), `assets/Sms/Console/save/*.png` (8 textures from game_6.szs), `assets/GameData.cs`, `assets/Mario.cs`,
`assets/BlueCoin.cs`, `assets/Sms/Console/SmsPauseMenu.cs`.

The game's TCardSave (GC2D/CardSave.cpp, 2408 lines) on its own screen `save.blo` (game_6.szs) with `savemessage.bmg`'s strings (common.szs),
three of its ten purposes:

- **PURPOSE_SHINE_GET** -- fireGetStar sets DIRECTOR_FLAG_CARD_SAVE_PENDING; when the shine demo ends the director enters STATE_CARD_SAVE (the
  whole scene's CUE_MOVE blacklisted = the game stopped) BEFORE the stage moves. Window 'w_5' (373 x 300): the heading 'sm6a', the file's
  "Mario" text + letter A/B/C, the SAVED shine count (shine icon x digits; "New" for a file never saved), three options "Save and Continue" /
  "Save and Quit" / "Continue" with the pointing-hand cursor (cursor.bti 29 x 41 turned 270, a black twin at alpha 125). Up/down move the hand
  (E3_MENU_CURSOR, no wrap, one move per stick press), A picks (SELECT_COMMON). 0 / 1 save (1 quits to the title afterwards), 2 just closes.
  Here: `Mario.SmsShineGetEnd` opens the dialog with the tree paused; its `Finished(next)` runs the old exit (`SmsShineGetFinish`): 0 = the
  stage move (the ExitScene / this level again), 1 = `SmsCardSave.TitleScene` (default `res://assets/CharacterSelect.tscn`).
- **PURPOSE_BLUE_COIN_GET** -- fireGetBlueCoin, EVERY blue coin (as the game): window 'w_4' (373 x 239) with bmg 36 "You got a Blue Coin!...",
  two options "Save and Continue" / "Continue". BlueCoin.cs opens it right after the pickup; the tree is paused under it.
- **PURPOSE_SAVE** -- the pause menu's SAVE (was a 40-frame dip while GameData wrote): window 'w_1' (402 x 202) "Save?" (yellow text) with the
  bookmark and the YES / NO pictures (select_yes/no.bti): left/right pick; the pick is green (0,255,0), the other white; from its 4th frame the
  pick BREATHES -- TExPane::setCenteredSize to 1.5 x over 40 frames, back over 40 -- while its colour runs (t, 255, 0), t 0 -> 255 -> 0 over 80
  frames (green to yellow); the clock restarts on a move. A = SELECT_COMMON (yes) / CANCEL_COMMON (no). YES saves; then "Save complete.
  Continue playing?" YES/NO with the live count: YES -> TPauseMenu2::setDrawEnd (the menu disappears, play resumes), NO -> the title. NO at
  "Save?" also closes the menu (PROGRESS_UNK1).
- **Every window** opens as `setCenteredSize(20, w, h, 0, 0)`: 20 frames of LINEAR growth about its centre (TCoord2D: (target - start) / 20 a
  frame), the contents hidden until it is open (SY_2D_IN then); it closes shrinking the same way (SY_2D_OUT at the start).
- **THE SAVING** (PROGRESS_SAVING): the file is written AT ONCE (saveSuccess), then 'w_1' shows bmg 26 "Saving file... Please do not touch the
  Memory Card or the POWER Button." with the bookmark as it WAS (the saved count or "New") for 300 frames: at frame 200 SY_DECIDE_COMMON and the
  count becomes the live MSF_SHINE_COUNT ("New" goes); past 300 the option block is "written" and the window shrinks. The blue coin's saving shows
  the name but no count (the game's purpose check). ~5.4 s, as the game -- knobs `SavingFrames` 300 / `SavingDecideFrame` 200.
- **THE WRITE MOVED**: `GameData.CollectShine` / `CollectBlueCoin` now set the flag in RAM only (TFlagManager); the card save's "Save and ..."
  calls `SaveNow()`. "Continue" leaves the pickup unsaved, as the game -- quitting loses it. `GameData.HasSaveFile` / `SavedShineCount` are the
  card's bookmark (TCardBookmarkInfo unk18 / unk1C): what the windows show before the write. F8's ClearSave now DELETES the file ("New").
  With `Mario.SmsShineGetSavePrompt` off (or no CardSave node) the touch writes at once as before 124 (drives 118 / 119 run that way).
- **The frame**: standard_window.bti (IA4 20 x 20, one rounded corner) at the four corners mirrored, the edges stretched from its last
  column / row, the contents 00bbb988 (teal at alpha 136); J2DWindow's TEV colour = teal + (white - teal) x I, alpha = the texture's --
  composed by `tools/projectshine/npc82/save124/gen_save_assets.py` into `save/standard_window.png`, a 42 x 42 NinePatch (margins 20).
- **The text**: `SmsTextBox` -- a J2DTextBox on the system font the talk balloon already carries (TalkFont.tres, TalkGlyph.gdshader): 20 x 24,
  line space 20, HBIND/VBIND as the blo (options left-bound, messages centred, the saving text top-bound), each with its black twin box
  (alpha 125, +3 +3) drawn under. Any blo text box can use it.
- **Input**: A only (MEANING_MENU_A; START is not read by TCardSave), the stick as a button (JUTGamePad's mRepeat with no repeat mask = the
  trigger: one move per press) with the pause menu's knobs `MenuStickOn` 0.3 / `MenuStickOff` 0.15.

**Exported tunables (CardSave.tscn):** Font, GlyphShader, the textures, `FileLetter` (0 = Mario A), `TitleScene`, `Size` (1 = the blo's 480
lines on the window height), `HeadingMessage`, `SavingFrames` / `SavingDecideFrame`, `MenuStickOn/Off`, the six sound slots (WindowIn 0x4819,
WindowOut 0x481A, Decide 0x481B, Select 0x481C, Cancel 0x481D, CursorMove 0x481E -- empty, his to fill), `DebugPrint`. Mario: `SmsShineGetSavePrompt`.

⚠️ **The heading quirk (verified on his disc, the US build):** CardSave.cpp 1460 writes savemessage.bmg entry 0x21 into the shine-get window's
heading. In the US bmg that entry is **"File created."** -- the Japanese order's "SHINE! Save data?" sits at 41 and the index was never
updated when the table was re-ordered (the options 28/29/30 and every other id line up with the US strings, so this is not a decode slip).
`HeadingMessage` = 33 ships the game as it is; 41 the line the designers meant.

**Deviations (logged):** no memory-card states (no card / format / corrupt -- the file is user://save_data.json, so the bookmark's "Corrupt"
never shows); the dialog's own dark mask never shows (the game shows it for GAME_OVER / ALL_SHINES / EXPLANATION only, none ported); the
YES / NO pick's sparks (emitter 0x1FA, ms_2d_pause_sel rotated 12 deg, scale (0.9, 1, 0.1)) not ported; a NinePatch below 40 px draws its
corners overlapping for the first ~2 frames of the grow (J2DWindow clips them); the pause menu stays drawn (static) under the dialog.

**Drive 124** (`npc82/save124/patch_smoke124.py`, the real input path, 34/34): A0-A15 the shine -> demo -> 'w_5' grows (131 x 105 at 8
frames) -> heading / hand / saved count -> the stick walks the three options with no wrap -> A -> the list closes unwritten -> SAVING writes
at once, 'w_1' with the saved count at frame ~90, the LIVE count past 200, shrinking past 300 -> the scene moves, the new Mario in the gate-out
with the bookmark 'saved'; B1-B5 the blue coin -> 'w_4' with bmg 36, no count -> Continue = no write, unpaused; C0-C10 START -> SAVE -> "Save?"
(YES (t,255,0), NO white) -> right / left -> the breath at the card's own clock 44 (96 wide, ebff00) and 80 (67 wide, 06ff00) -> YES -> the
file now carries the blue coin -> "Save complete." -> YES -> the menu gone; D1-D2 SAVE -> NO = no write, the menu closes. Drive 123 32/32
(its P-case updated: the coin opens the dialog, A saves), 118 35/35, 119 25/25 (the prompt off inside them). Build 0 errors, boot ALL OK.

## SMS pass 124b -- the card save after the playtest: centred contents, the window colour as a knob, the counter's pitch and mode

**Files:** `assets/Sms/Console/SmsCardSave.cs`, `assets/Sms/Console/CardSave.tscn`, `assets/Sms/Console/save/standard_window_body.png` +
`standard_window_rim.png` (replace `standard_window.png`).

Jaysonn (SaveRef.png): "the font is not in the right spots ... have everything middle aligned. and fix the counter. next the blue should be
darker i think but lets make it so i can set it to any color."

- **`WindowColor`** (Color, default (0, 0.5, 0.5, 0.85)): the frame is now two NinePatch layers -- the BODY (white, alpha = the piece's A x (1 - I),
  normalised; `SelfModulate` = WindowColor, so its alpha is the window's density) and the RIM (white, alpha = A x I: the inner line and the corner
  dots) drawn over it untinted. The game's own value is (0, 0.733, 0.725, 0.533) -- 00bbb9 at 0x88 -- which over the near-white shine fade read
  as the pale blue he saw; his default is darker and denser. Any colour works; a change applies to the three frames at once (a property setter).
- **`CenterContents`** (default on, a DEVIATION -- the blo is left-weighted: the file name at x 114/129, the count under it, the options left-bound at
  x 100 with the hand at 49): the heading / message box, the "MARIO A" group (90 + the letter's 30), the "New" box, the count groups and the option
  boxes are centred on the window; each option text is centred (HBIND centre) and its hand follows the text's left edge at the blo's distance (text
  left 97 -> hand centre 63.5 = 33.5 px). Off = the game's layout.
- **`DigitPitch`** (default 17 = the blo's: the digit panes 17 apart with 25-px cells -- the game's own tight "x01"; the HUD's ratio would be ~19):
  widen it for air between the digits. The three-digit group keeps the blo's shape (the third digit at 37/17 of the pitch).
- **`CountShown`**: `Saved` (the game: the FILE's count before the write -- "x01" after the second shine -- then the saving window ticks it to the live
  count at frame 200 with the DECIDE) or `Live` (the count with this pickup in it from the start, never "New").

Drive 124 34/34 unchanged (it asserts sizes, states and the count class, not positions). Build 0 errors.
