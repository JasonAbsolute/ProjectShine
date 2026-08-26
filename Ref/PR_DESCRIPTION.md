<!-- Title field (paste separately into the PR title box): -->
# Add red coin switch challenge: sign, TalkingPOV camera, timer, coin spawn/collect, reward Shine

<!-- Everything below goes in the PR description box: -->

Adds an SMS-style red coin switch challenge to the secret level. Ground-pounding the switch opens an acknowledged sign (7-line ruled banner, transparent, TalkingPOV camera swing to Mario's side), then spawns eight marker-authored red coins and starts a reusable timer only after the sign has fully closed and faded out. Collecting all eight stops the timer and reveals a reward Shine with a pause-safe entrance and a temporary camera that restores gameplay exactly.

Also includes:

- **`SunshineCamera.CamMode.TalkingPOV`** — OverShoulder-style, locked, event-driven
- **`Mario.ForceStandingIdle()`** — `CameraLocked` freezes velocity, not pose; needed so Mario doesn't visibly freeze mid-groundpound-landing during the sign
- **`Level.MarioReady` signal** — `ShineTimer.AutoStart`'s dependency, so it never starts during a non-interactive intro pan
- Side-jump landing reuse fix, spin-VFX cleanup on wall slide/Shine collection, normal falling pose for airborne Shine pickups

39 files changed — cross-referenced against a progress report from another session (`Ref/RED_COIN_FEATURE_PR_REPORT.md`) and this repo's actual diff to scope exactly what belongs in this PR. Deliberately excludes ~300 other changed/untracked files in the working tree: unrelated `addons/godot_ai` plugin churn, a mass `models/*.res` resave, unrelated `Font/LifeMeter` `.tres` resaves, backup files (`Mario.cs.bak`, `ProjectShine.csproj.old.2`), and a few unrelated scene tweaks (`Mario2.tscn` floor properties, `SleepyBoo.tscn` format churn) that don't belong to this feature.
