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

- Accelerate toward `min(32, mag)`: `fwd += 1.1 − fwd/43` per tick (the SM64 lineage); above target on flat ground
  brake 5/tick (`DecBrake`); stick released `−1/tick` (`walkEnd`); braking/skid `−4/tick`.
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

### Deliberate deviations from the game

- `SmsRollJumpMinSpeed = 20` (game 40) — feel.
- `SmsRollJumpKnockdown = false`: the roll-jump into a wall that reaches head height wall-slides. `rotateBroadJumping()` →
  `jumpingBasic(…, 1)` case 2 knocks it down above 40, and it never drops under 40 with the stick held; Jaysonn's
  memory of the game is a slide, and it is not verifiable here without Dolphin. Walls below head height were already
  no hit for it (ledge bit). Flip the toggle to get the code's rule back.
- `doRunning` target uses `min(32, mag)`; the NonMatching decomp prints `max()`, which contradicts SM64 (this code's
  ancestor) and makes the shipped `Soft2Walk` / `Walk2Soft` thresholds meaningless.
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
- Ledge hang: done, see Ledge hang. Not ported: the fence catch (`FENCE_CATCH`), the hang-under-roof states
  (`HANG_ROOF` / `MOVE_ROOF`).
