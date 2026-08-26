# Red Coin Switch Challenge — PR Implementation Report

**Project:** ProjectShine  
**Level integration:** `assets/secrect_level.tscn`  
**Report date:** 2026-08-17  
**Status:** Feature implemented and compiling; final editor-tuned presentation should receive a manual playtest before merge.

## PR summary

This change adds an SMS-style red coin switch challenge to the secret level:

1. Mario ground-pounds the red coin switch.
2. The switch animation plays and an instructional sign opens using the talking camera.
3. The player acknowledges the sign with A.
4. The talking camera exits and the sign completes its fade-out.
5. Only after the sign has fully closed, eight red coins spawn at editor-authored markers and the challenge timer starts.
6. Every coin appears with a reusable white/blue-gray smoke and lime-green dot burst.
7. Collecting the eighth coin stops the timer and spawns a Shine at `RedCoinShineSpawn`.
8. The world pauses for the Shine entrance while a temporary reveal camera moves in, holds, and returns to the exact prior gameplay camera transform.
9. The reward Shine then uses the normal Shine collection flow.

The implementation is data-driven through exported Godot properties, marker nodes, and reusable scenes so designers can continue tuning placement, timing, VFX, and camera framing without rebuilding the feature.

## Player-facing behavior

### Switch activation and sign flow

- `RedCoinSwitch` only accepts Mario while he is in `groundPoundFalling` or `groundPoundLanding`.
- A per-ground-pound latch prevents one pound from triggering more than once.
- The switch's baked `redcoinswitch` animation plays once.
- `SignPopup` displays the configurable `MessageText` and requires explicit A-button acknowledgement for this challenge.
- `SignPopup.Dismissed` records acknowledgement and exits `SunshineCamera`'s talking POV immediately.
- The challenge does **not** start on button press. `RedCoinSwitch` waits for the popup's `TreeExited` event, which occurs after the 0.25-second fade-out finishes.
- If the sign scene is missing, the challenge falls back to starting directly instead of becoming blocked.

### Red coin spawning

- The switch resolves `RedCoinSpawnsPath` and iterates its direct `Marker3D` children.
- One `RedCoin` instance is created at each marker's complete global transform.
- The secret level currently contains `Spawn01` through `Spawn08`.
- Each spawned coin is named deterministically (`RedCoin01`, `RedCoin02`, etc.).
- Each coin emits a `Collected` signal so the switch counts only coins belonging to its own challenge group.
- `RequiredRedCoins` defaults to 8, and a warning is emitted if the marker count does not match.

### Timer and HUD

- The challenge starts `ShineTimer` only after the sign has fully closed and the coins have spawned.
- `ShineTimer` is reusable and supports count-up, count-down, bounded, unbounded, automatic, and externally controlled modes.
- `TimerHud` presents `MM:SS:CC`, including centiseconds.
- The timer HUD animates on/off with the same wave presentation used by the coin HUDs.
- A low-time count-down can flash between normal and warning colors.
- While the timer is visible, `RedCoinHud.SetTimerActive()` shifts the red coin count upward so the displays stack instead of overlap.
- Reaching the eighth challenge coin calls `StopTimer()` before the reward is spawned.

### Coin spawn VFX

The original procedural effect was replaced with an editor-tunable particle scene:

```text
RedCoinSpawnPuff
├── SmokeParticles      (CPUParticles3D)
└── GreenDotParticles   (CPUParticles3D)
```

- `RedCoin.PlaySpawnPuff()` instantiates `assets/RedCoinSpawnPuff.tscn` by default.
- `RedCoin.SpawnPuffScene` allows a per-scene override.
- `RedCoinSpawnPuffFx` positions the scene in world space, restarts both one-shot emitters, and frees the effect after the longest emitter lifetime plus cleanup padding.
- The effect is triggered after each spawned coin has received its marker transform.
- The original user-provided `assets/SpawnPuff.png` was preserved.
- A softened derivative was created as `assets/SpawnPuff_soft.png`.
- The current runtime texture is `assets/SpawnPuff_soft_blue.png`, which adds a subtle gray-blue gradient near the bottom while preserving alpha and the white upper cloud.
- `assets/red_coin_green_dot.png` provides the lime-green dot sprite.

Current editor tuning in `RedCoinSpawnPuff.tscn`:

| Setting | Current value |
|---|---:|
| Smoke quad size | `1.5 × 1.5` |
| Smoke amount | `18` |
| Smoke lifetime | `0.84s` |
| Smoke speed scale | `1.59` |
| Smoke explosiveness | `1.0` |
| Smoke spread | `180°` |
| Green dot amount | `7` |
| Green dot lifetime | `0.4s` |
| Green dot quad size | `0.11 × 0.11` |
| Green dot velocity | `1.75–3.05` |

These values are presentation tuning rather than gameplay requirements and can continue to be adjusted in the Inspector.

### Eighth-coin reward Shine

- The switch tracks completion once and cannot spawn duplicate rewards.
- Reward creation is deferred out of the coin pickup area's physics callback.
- The reward is instantiated from `RedCoinShineScene` at the complete transform of `RedCoinShineSpawnPath`.
- `assets/secrect_level.tscn` retains the original level Shine as `ShineSprite2` and uses a separate `RedCoinShineSpawn` marker for the red-coin reward.
- `ShineSprite.PlaySpawnEntrance()` temporarily pauses the scene tree, changes the Shine to always-process mode, disables pickup monitoring, and animates scale, rise, and rotation.
- When the entrance finishes, pickup monitoring, process mode, and the prior pause state are restored.
- Cleanup prevents the scene tree from being left paused if the Shine exits early.

Current `shine_sprite.tscn` entrance overrides:

| Setting | Current value |
|---|---:|
| `SpawnEntranceDuration` | `4.0s` |
| `SpawnEntranceRise` | `2.5` world units |
| `SpawnEntranceTurns` | `13` full turns |

The final location, rotation, and scale come from the `RedCoinShineSpawn` marker. The spawn animation uses cubic-out movement/rotation and back-out scaling.

### Reward reveal camera

`RedCoinSwitch` creates a temporary top-level `Camera3D` for the reward reveal:

- It copies the active gameplay camera's FOV, near/far planes, cull mask, and starting transform.
- It frames the Shine relative to the current gameplay camera direction.
- Its tween processes while the world is paused.
- It moves toward the reward, holds for the remaining entrance duration, and returns to the exact captured gameplay transform.
- It restores the previously active gameplay camera and frees itself.
- `_ExitTree()` kills an active tween and performs camera cleanup so ownership is not stranded.

Inspector controls on the level's `RedCoinSwitch` instance:

| Property | Default | Purpose |
|---|---:|---|
| `EnableRedCoinShineCamera` | `true` | Enables/disables the reward shot |
| `RewardCameraTargetHeight` | `2.4` | Look target above the Shine origin |
| `RewardCameraDistance` | `7.5` | Reveal-camera distance |
| `RewardCameraHeight` | `1.6` | Additional camera elevation |
| `RewardCameraTravelDuration` | `0.3s` | Move-in and return duration |

The complete camera sequence is synchronized to `ShineSprite.SpawnEntranceDuration`; time not used by the move-in/return becomes the hold.

## Related fixes completed during this work

### Side-jump landing smoothness

- Side-flip touchdowns previously reused `tripleJumpLanding` / `ma_tjmp2`, which imposed a 20-frame landing lock.
- Side flips now enter `singleJumpLanding` / `ma_laend`, matching the normal single-jump landing and its 7-frame lock.
- The side-flip-only 180-degree authored-animation yaw offset is cleared on touchdown so the forward-authored landing animation faces correctly.

### Spin-jump VFX cleanup

- Entering a wall slide now calls the shared spin-effect shutdown path.
- Starting a Shine collection also shuts down spin effects defensively.
- This hides the procedural ring meshes immediately and disables the blue, white, and red spin particle emitters, preventing effects from leaking into wall-slide or Shine states.

### Airborne Shine pickup presentation

- Touching a Shine in the air stops spin VFX immediately.
- Mario switches to `MarioState.ledgeFall`, which plays the normal `ma_land` falling animation.
- Horizontal velocity is cleared and Mario drops vertically beneath the Shine.
- The Shine cutscene starts only after Mario reaches the floor, avoiding a frozen midair collection pose.

## Main implementation files

### New feature files

- `assets/RedCoinSwitch.cs`
- `assets/RedCoinSwitch.cs.uid`
- `red_coin_switch.tscn`
- `Levels/secret1/platforms/RedCoinSwitch.glb`
- `Levels/secret1/platforms/RedCoinSwitch.glb.import`
- `Levels/secret1/platforms/RedCoinSwitch_J_switch.png`
- `Levels/secret1/platforms/RedCoinSwitch_J_switch.png.import`
- `Levels/secret1/platforms/RedCoinSwitch_redcoinswitch.png`
- `Levels/secret1/platforms/RedCoinSwitch_redcoinswitch.png.import`
- `assets/ShineTimer.cs`
- `assets/ShineTimer.cs.uid`
- `Font/HudElements/TimerHud.cs`
- `Font/HudElements/TimerHud.cs.uid`
- `Font/HudElements/TimerHud.tscn`
- `Font/HudElements/timeLabel.tres`
- `Font/HudElements/timerColon.tres`
- `Font/HudElements/SignPopup.cs`
- `Font/HudElements/SignPopup.cs.uid`
- `Font/HudElements/SignPopup.tscn`
- `Font/HudElements/SignPopupLabelSettings.tres`
- `assets/SignpopTextBox.png`
- `assets/SignpopTextBox.png.import`
- `assets/RedCoinSpawnPuff.tscn`
- `assets/RedCoinSpawnPuffFx.cs`
- `assets/RedCoinSpawnPuffFx.cs.uid`
- `assets/SpawnPuff_soft_blue.png`
- `assets/SpawnPuff_soft_blue.png.import`
- `assets/red_coin_green_dot.png`
- `assets/red_coin_green_dot.png.import`

### Modified integration files

- `assets/secrect_level.tscn` — switch, timer/HUD, eight spawn markers, reward marker, and scene wiring.
- `assets/RedCoin.cs` — collection signal and reusable spawn-puff integration.
- `Font/HudElements/RedCoinHud.cs` — timer-aware positioning and count presentation.
- `assets/ShineSprite.cs` — pause-safe spawn entrance and Shine lifecycle support.
- `shine_sprite.tscn` — current entrance and collection tuning overrides.
- `assets/Mario.cs` — reward collection flow plus side-jump and spin-effect fixes.
- `assets/SunshineCamera.cs` — talking POV used while the challenge sign is open.

### Optional source/intermediate VFX assets

These document the texture iteration but are not all required by the current runtime scene:

- `assets/SpawnPuff.png` — preserved original.
- `assets/SpawnPuff_soft.png` — softened white intermediate.
- `assets/red_coin_smoke_puff.png` — earlier smoke mask, no longer referenced by the current puff scene.

Before staging, decide whether the PR should retain all source/intermediate textures or only the original plus final runtime asset.

## Validation completed

### Core delayed-spawn and puff lifecycle

A focused headless integration run previously confirmed:

```text
DELAYED_SPAWN_SIGN_OPEN coins=0
DELAYED_SPAWN_DURING_FADE coins=0
DELAYED_SPAWN_AFTER_CLOSE coins=8 effects=8 smoke=56 green_dots=112
RED_COIN_PUFF_LIFECYCLE_VERIFIED effects=0 coins=8
```

This verified that coins do not appear while the sign is open or fading, exactly eight appear after it closes, and the temporary VFX nodes clean themselves up.

### Reward camera

A focused runtime check previously confirmed:

- The temporary reward camera becomes current while the world is paused.
- It moved `8.5586` world units during the paused reveal.
- The original gameplay camera was restored to its exact transform.
- The temporary camera was freed.

### Build

The latest recorded rebuild after the Mario state/VFX fixes completed successfully:

```text
Build succeeded.
13 Warning(s)
0 Error(s)
```

The warnings were existing Godot/C# obsolete API and unused-variable warnings, not errors from this feature.

### Manual checks recommended before merge

- Ground-pound the switch and verify the sign camera framing.
- Confirm pressing A does not spawn coins until the sign is fully invisible.
- Check all eight marker positions and pickup accessibility.
- Review smoke size, blue-gray lower tint, dot count, and lifetime in motion.
- Confirm the timer and red coin HUD do not overlap.
- Collect coin eight and review the current 4-second / 13-turn reward entrance.
- Verify the reward camera framing from multiple gameplay camera angles.
- Collect the reward while airborne and confirm Mario uses `ma_land` while dropping.
- Enter a wall slide from a spin jump and confirm no spin emitters remain active.
- Confirm the original `ShineSprite2` reward still behaves independently.

## PR-scope warning

At report time, the repository had a large mixed working tree: **292 tracked files changed**, plus many untracked files. The changes include unrelated `addons/godot_ai` work, broad animation `.res` rewrites, imported-resource churn, backups, and project copies.

Do **not** use `git add .` for this PR. Stage the intended files explicitly and review each diff. In particular, do not accidentally include items such as:

- unrelated `addons/godot_ai/**` changes;
- mass `models/*.res` rewrites unless intentionally required;
- `assets/Mario.cs.bak`;
- `ProjectShine.csproj.old.2`;
- unrelated HUD/LifeMeter `.tres` import changes;
- unrelated level, tree, camera, or plugin work.

Some shared files (`assets/Mario.cs`, `assets/ShineSprite.cs`, `assets/SunshineCamera.cs`, and `assets/secrect_level.tscn`) contain changes beyond the isolated red-coin feature. Their hunks should be reviewed and split carefully if the PR must contain only this feature.

## Suggested PR description

> Adds an SMS-style red coin switch challenge to the secret level. Ground-pounding the switch opens an acknowledged sign, then spawns eight marker-authored red coins and starts a reusable timer only after the sign fade completes. Coins use an editor-tunable smoke/dot burst. Collecting all eight stops the timer and reveals a reward Shine with a pause-safe entrance and temporary camera that restores gameplay exactly. Includes HUD stacking, smoother side-jump landing reuse, spin-VFX cleanup on wall slide/Shine collection, and a normal falling pose for airborne Shine pickups.
