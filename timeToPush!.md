# timeToPush! -- the push / PR hand-off log

**What this is.** Kiro (the remote agent working over ssh) commits every pass but is not allowed to push from this machine. This file is the
queue of what still has to be published. Kiro appends an entry at the end of each pass; **Claude Code on Jaysonn's PC** (run from this
project root, `C:\Users\Jaysonn\Documents\ProjectShine`) works the PENDING entries top to bottom, then marks each one DONE with the date and
the URL it produced. Jaysonn can also just run the commands himself.

## Standing procedure for the local Claude (every entry)

1. `git status --short | findstr /v addons` -- the tree should be clean. Changes under `addons/godot_ai/` are the plugin's own churn: never
   commit them. Anything else uncommitted is Jaysonn's live editor work: leave it, do not commit or stash it, just push the branches below.
2. `git fetch origin`, then for the branch named in the entry: `git log --oneline -1 <branch>` and check the commit matches the entry's sha
   (the tip may have moved on if Kiro committed more since -- that is fine, as long as the entry's sha is an ancestor:
   `git merge-base --is-ancestor <sha> <branch>`).
3. Push: `git push -u origin <branch>` (plain push; the GitHub login is cached in the credential manager -- a browser window may open once).
4. If the entry asks for a PR and `gh` is installed (`gh --version`): `gh pr create --base main --head <branch> --title "<title>" --body-file <file>`
   with the body the entry gives. If `gh` is not installed, either `winget install GitHub.cli` once, or open the compare URL the entry gives
   in the browser and create the PR there.
   **State 2026-10-25:** `gh` 2.103.2 IS now installed (`C:\Program Files\GitHub CLI\gh.exe`, not yet on the shell's PATH in a fresh session),
   but it is **not authenticated** -- `gh auth status` says "not logged into any GitHub hosts". The cached git credential is Git Credential
   Manager's and `gh` does not read it. Until Jaysonn runs `gh auth login` once (or sets `GH_TOKEN`), the local Claude cannot open a PR itself;
   the fallback is to build the compare URL with `?expand=1&title=<urlencoded>&body=<urlencoded>` and open it, so the form arrives prefilled
   and Jaysonn only presses "Create pull request".
5. If the entry asks for a MERGE into main: Kiro has already merged locally (`git merge --no-ff`), so the entry only asks to push `main` (and
   the feature branch for the record).
6. Verify: `git rev-list --count origin/<branch>..<branch>` prints `0`.
7. Edit this file: change the entry's `PENDING` to `DONE <date> -- <push/PR URL>`, then `git add timeToPush!.md && git commit -m "timeToPush: #N done"
   && git push origin main` (or the branch you are on). Never `--force`, never `--amend`, never push a feature branch's commits onto main by hand.

## Rules both agents follow

- `main` is the trunk: always buildable (0 errors), boot `ALL OK`. Nothing lands on it except a finished feature's merge, Kiro's small passes
  that are not part of a feature, and Jaysonn's own editor work.
- A feature = `feature/<short-name>` branched from `main`; a pass = one commit on it ("SMS pass N: ..." title, its entry appended to
  `PROJECT_LOG.md`, the headless drive green before the commit).
- A finished feature (Jaysonn has play-tested it) is merged with `git merge --no-ff feature/<name>` and a "Feature: <name> (passes N-M)" commit,
  or through a GitHub PR -- the entry says which.
- Keep `.import` compression flips, whitespace retabs of `.cs` files and `addons/godot_ai` out of commits.
- **A feature is merged ONCE, one way: either Kiro's local `git merge --no-ff`, or a GitHub PR -- never both.** If an entry asks for a PR,
  Kiro must NOT pre-merge the branch into local `main`; if Kiro has already merged locally, the entry must say "push only, no PR". Doing both
  put two independent merges of identical code on `main` on 2026-10-25 (see #2/#3). Also note GitHub's **squash** option makes the feature's
  own commits non-ancestors of `origin/main`, so any later `git merge-base --is-ancestor <pass sha> origin/main` check will say NO even though
  the code is present -- compare trees (`git diff origin/main main`) rather than ancestry after a squash merge.

## Entries

### #1 -- DONE 2026-10-25 -- https://github.com/JasonAbsolute/ProjectShine/commit/144b11e -- queued 2026-10-25 02:15 PDT (Kiro)
Push the trunk and the old long branch. No PR: `main` is the trunk and already carries everything.
- Branch `main` -- 307 commits ahead of `origin/main` (includes this file's commit; tip at or after `c48b3f9`).
- Branch `sms-physics` -- 294 commits ahead of `origin/sms-physics` (tip `c48b3f9`; historical pointer, nothing new goes on it).
```
git push origin main
git push origin sms-physics
git rev-list --count origin/main..main
```
Expect `0`. Nothing in the range is over GitHub's 100 MB limit (largest file: `models/ResortMario/.../ma_mdl1.glb`, 28 MB).

Result: `main` `3eed4c0..144b11e` (307 commits), `sms-physics` `61d5810..c48b3f9` (294 commits). Both
`rev-list --count` checks returned `0`. No PR, as the entry specified. Working tree was clean apart from
the 19 `addons/godot_ai/` entries, which were left untouched.

### #2 -- DONE 2026-10-25 -- https://github.com/JasonAbsolute/ProjectShine/pull/6 (SQUASH-merged by Jaysonn) -- queued 2026-10-25 03:55 PDT, updated 04:45 (Kiro)
Push the first feature branch and open its PR: the card save ("Save?" after a shine, a blue coin, the pause menu's SAVE). PLAYTESTED BY
JAYSONN 2026-10-25 04:40 ("ok looks good") -- the PR can be merged once it is open (squash or merge commit, his call; `main` then needs a
`git pull` here).
- Branch `feature/save-prompt` -- the branch tip is this file's latest commit; the code tip is `25f69fd` ("SMS pass 124c: card save counter --
  the blo's own digit / x / shine-icon textures"; 124b `3ab4893`, 124 `a804f34`), 7 commits since main (`7e448b9`).
- PR: title "Feature: the card save (SMS passes 124-124c)"; base `main`; body:
  > TCardSave (GC2D/CardSave.cpp) on the game's own `save.blo` + `savemessage.bmg`: the shine-get window (Save and Continue / Save and
  > Quit / Continue, the saved count or "New", the pointing hand), the blue-coin window (every coin, as the game), the pause menu's SAVE
  > ("Save?" YES/NO with the breathing green-to-yellow pick, then "Save complete. Continue playing?"), the 300-frame saving window with the
  > live count at frame 200. The save write moved from the pickup to the dialog's "Save and ..." (Continue leaves it unsaved, as the game).
  > New: SmsCardSave.cs, SmsTextBox.cs (a J2DTextBox on the system font), CardSave.tscn (under Mario in Player.tscn), 21 textures from the
  > disc (the frame as a tinted body + white rim, the digits / x / shine icon the blo's own).
  > 124b/c after Jaysonn's playtest: contents centred on the window, WindowColor knob (darker default), DigitPitch / CountShown knobs, the
  > counter on the disc's textures (the HUD's 36x36 re-renders overlapped).
  > Drive 124 34/34 through the real input path (across the stage move); 123 32/32, 118 35/35, 119 25/25; boot ALL OK. Playtested OK.
  > Knobs on CardSave.tscn: WindowColor (any colour; the game's is (0, 0.733, 0.725, 0.533)), CenterContents (off = the game's left-weighted
  > layout), DigitPitch, CountShown (Saved = the game / Live), HeadingMessage (33 = the game's "File created." quirk, 41 = "SHINE! Save
  > data?"), TitleScene, Size, SavingFrames, the six sound slots (empty).
  > Not ported: memory-card states, the YES/NO sparks, the dialog's mask (never shown for these purposes).
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/save-prompt
```
git push -u origin feature/save-prompt
git rev-list --count origin/feature/save-prompt..feature/save-prompt
```
Expect `0`. Then open the PR from the compare URL with the title and body above. Note the working tree carries Jaysonn's own uncommitted
`assets/Level.tscn` edit -- leave it, do not add it to this PR.

Result: `feature/save-prompt` pushed as a new remote branch, `main` `7e448b9..125ade9` on it (7 commits).
`git rev-list --count origin/feature/save-prompt..feature/save-prompt` returned `0`. Entry's code tip `25f69fd`
confirmed an ancestor of the branch, and 124b `3ab4893` / 124 `a804f34` are both in the range as the entry says.
**The PR itself is not open yet** -- `gh` was installed during this pass but is unauthenticated (see the procedure's
step 4 note), so the prefilled compare page was opened in the browser instead and Jaysonn creates it from there.
Working tree left untouched: Jaysonn's `assets/Level.tscn` plus two Koopa `.png.import` compression flips, and the
19 `addons/godot_ai/` entries.

Follow-up 04:52 (Kiro): Jaysonn playtested and said "merge the last branch so ready to go" -- merged LOCALLY into `main` (see #3). The PR
for #2 is no longer needed: if the prefilled form was already submitted, GitHub closes it as merged when #3's push lands; if not, do not
create it.

Closing note (local Claude): Jaysonn HAD already submitted the prefilled form, and he **squash-merged** it as PR #6 -> `origin/main` commit
`e123d1d`, which has a SINGLE parent (`7e448b9`). So the 7 feature commits are NOT ancestors of `origin/main` -- the code is there as one
squashed commit. Combined with Kiro's local `--no-ff` merge `a0590ea`, `main` had two independent merges of byte-identical code. See #3 for
how that was reconciled, and the new rule under "Rules both agents follow".

### #3 -- DONE 2026-10-25 -- https://github.com/JasonAbsolute/ProjectShine/commits/main -- queued 2026-10-25 04:52 PDT (Kiro)
Push the trunk: `feature/save-prompt` merged into `main` with `--no-ff` at Jaysonn's word (he playtested 124-124c and approved).
- Branch `main` -- tip `a0590ea` ("Feature: the card save (SMS passes 124-124c)", the merge commit), 9 commits ahead of `origin/main`
  (the 7 feature commits, the #2 result commit `bd60e67`, the merge). No PR.
- `feature/save-prompt` is already on the remote (#2) and fully contained in `main`; it can stay as the record.
```
git push origin main
git rev-list --count origin/main..main
```
Expect `0`. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn` and the two Koopa `.png.import` flips -- leave them.
Next feature branch in progress: `feature/enemy-base` (branched from this `main`).

Result: **the plain `git push origin main` in this entry could not run** -- `main` had diverged (10 local commits vs 1 remote), because
Jaysonn's PR #6 was squash-merged on GitHub while Kiro had already merged the same branch locally with `--no-ff`. `git diff origin/main main`
listed ONLY `timeToPush!.md`, confirming the code was byte-identical both ways, so the fix was a normal non-destructive merge (no `--force`,
no `--amend`, no rebase): `git merge origin/main` on `main`. Every code file auto-merged with zero conflicts (git sees the same change on both
sides); the one conflict was `timeToPush!.md`, where the remote side was the stale pre-squash version (it reverted #2 to PENDING and dropped
#3), so it resolved to ours. Merge commit `b63dead`, then pushed. `git rev-list --count origin/main..main` returned `0`.
Cost of the double merge: `main`'s history now holds both `e123d1d` (the squash) and the 7 original commits under `a0590ea`. Harmless --
identical content, nothing duplicated in the tree -- but avoidable; hence the new rule below.
`feature/enemy-base` was sitting at the old `main` tip with no commits of its own, so it was fast-forwarded onto the reconciled `main` to keep
the next feature branched from the real trunk. Working tree left untouched throughout.

### #4 -- DONE 2026-10-25 -- https://github.com/JasonAbsolute/ProjectShine/tree/feature/enemy-base -- queued 2026-10-25 05:35 PDT (Kiro)
Push the second feature branch so the work is visible on GitHub. NO PR YET: the feature is in progress (next passes: the DISAP / wall-hit
puffs, the Gooble moved onto the base, more Stu kinds and enemies). When Jaysonn says the feature is done, a later entry asks for the PR;
per the rule above Kiro will NOT merge this branch locally.
- Branch `feature/enemy-base` -- code tip `3f7927b` ("SMS pass 125b: the Stu's look -- the editor-size scale and default.bmd's TEV materials",
  on top of `e039b9c` "SMS pass 125: the enemy base (TSmallEnemy / TWalkerEnemy / Spine) + Strollin' Stu (THamuKuri)"), branched from the
  reconciled `main` (`985fb29`); the branch tip is this file's latest commit.
- New files: `assets/Sms/Enemy/` (SmsSmallEnemy.cs, SmsHamukuri.cs, Hamukuri.tscn + .uid; 125b: the EnemyToon shaders + include with their .uid,
  the five Hamukuri*.tres), `models/Eniemes/Hamukuri/` (the glb, 6 png, their .import -- the glb .import is the stripped one, now with the
  material swap); `assets/Gooble.cs` one line; PROJECT_LOG.md. Nothing near the 100 MB limit (the glb is 0.5 MB).
```
git push -u origin feature/enemy-base
git rev-list --count origin/feature/enemy-base..feature/enemy-base
```
Expect `0`. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn` (and the editor re-flips two Koopa `.png.import`
files every session) -- leave them.

Result: `feature/enemy-base` pushed as a new remote branch (4 commits since `main` `985fb29`), tracking set.
`git rev-list --count origin/feature/enemy-base..feature/enemy-base` returned `0`. All three shas confirmed
ancestors: code tip `3f7927b`, `e039b9c`, and the reconciled `main` `985fb29` -- so the branch really is off the
real trunk, not the pre-merge tip. No PR, as the entry specified; `main` untouched and still in sync at `985fb29`.
Hygiene checks on the range came back clean: 0 `addons/godot_ai` paths, 0 of the two Koopa `.png.import` flips.
The 35 changed paths are exactly the ones the entry lists. Largest blobs: `PROJECT_LOG.md` 0.79 MB, `Hamukuri.glb`
0.48 MB -- and `Hamukuri.glb.import` really is the stripped one (a few KB, not the multi-MB `slice_N` kind).
Working tree left untouched: Jaysonn's `assets/Level.tscn` and `assets/Sms/SmsHiddenCoin.cs` (open in his editor
this session), the two Koopa flips, and the `addons/godot_ai` churn.

### #5 -- DONE 2026-10-26 -- https://github.com/JasonAbsolute/ProjectShine/pull/7 (SQUASH-merged by Jaysonn) -- queued 2026-10-25 16:20 PDT, updated 23:50 (Kiro)
Push the enemy branch and open its PR: the enemy base (TSmallEnemy / TWalkerEnemy / the Spine) with the whole Stu family on it -- Strollin', Soarin',
the goop Stu, Smolderin', the tower, Swipin' with the cap theft -- the Gooble moved onto the base, the death / wall-hit puffs, the drop tables, and
the editor previews (passes 125-125h). PLAYTESTED BY JAYSONN 2026-10-25 23:37 ("awesome looks like its all working. includong seeing them in the
editor!") -- the PR can be merged once it is open (squash or merge commit, his call; `main` then needs a `git pull` here). Kiro has NOT merged this
branch locally (the entry asks for a PR: the one way).
- Branch `feature/enemy-base` -- the branch tip is this file's latest commit; the code tip is `4e10187` ("SMS pass 125h: the thief's cap on the body
  joint, the Smolderin' Stu's flames, the tower and the flame shown in the editor"), on top of `0867da7` (125g), `7f1a436` (125f), `26f9666` (125e),
  `6f4c588` (125d), `27f9e38` (125c), `3f7927b` (125b) and `e039b9c` (125), plus `59e3dec` (the editor's re-saves of the 125h fx scenes) and the
  timeToPush commits; 20 commits since `main` `985fb29` (= `origin/main`), this entry's own included. Already tracking `origin/feature/enemy-base` (entry #4), so a plain push
  is enough. Nothing large (the biggest new file is the 0.48 MB Hamukuri.glb, pushed in #4; since then the 116 KB Hanekuri.glb).
- PR: title "Feature: the enemy base and the Stu family (SMS passes 125-125h)"; base `main`; body = `pr125.md` at the project root (untracked --
  delete it after; the same text follows):
  > The small-enemy base from the decomp -- TSmallEnemy / TWalkerEnemy / the Spine nerves (Enemy/smallEnemy.cpp, walkerEnemy.cpp) -- and every Stu
  > kind on it (Enemy/hamukuri.cpp), from the game's own .prm numbers, models, clips and particle data:
  > - Strollin' Stu (THamuKuri): the wander, the find / the attack hop, the stomp, the flail and the kick, the wall crash, the water hit and the roll;
  >   the editor-size scale and default.bmd's TEV materials (passes 125, 125b).
  > - The puffs (125c): the death smoke + glow (ms_enm_disap_a / _a_w / _b) and the wall-crash puff + goop splat (ms_enm_wallhit / _o) from the
  >   disc's .jpa; SmsFxBurst.
  > - The drop table (125d): SmsDropTable / SmsDropEntry -- what an enemy drops when it dies (coins, 1-UPs, anything with a scene); the game's
  >   GenItemRate as UseEnemyRate; one editable .tres per kind.
  > - The Gooble (125e) moved onto the base (TNameKuri as a species), its leap and melt kept.
  > - Soarin' Stu (THaneHamuKuri: the hover, the dive, the hip drop, the wings) and the goop Stu (TDoroHamuKuri, the Stu's clips borrowed onto its
  >   own skeleton) (125f).
  > - Smolderin' Stu (TFireHamuKuri: burning from its first step, unstompable while lit, four drops put it out and it flees, the recover relights it;
  >   the MOE_FIRE flames and the fire-out smoke on its head), the Stu tower (TDangoHamuKuri / TBossDangoHamuKuri: the 2x boss making and carrying a
  >   tower of StackCount Stus, nothing in the chain stompable, water knocks the top off, the boss's death takes the tower) and Swipin' Stu with the
  >   cap theft (TDoroHaneKuri: its hit takes Mario's cap -- he goes bare-headed, the thief wears the cap and moustache and hovers at 800 u -- hosed
  >   down, stomped, the cap on the ground as an item and back on his head) (125g, 125h).
  > - In the editor: a placed tower shows the boss at its scale with its stack (SmsDangoTowerPreview, a helper that frees itself in the game); the
  >   Smolderin' Stu shows its flame.
  > New: assets/Sms/Enemy/* (the base SmsSmallEnemy, the kinds, the drop tables, the scenes, the TEV materials and shaders), assets/Sms/Fx/EnemyDisap*,
  > EnemyWallHit, StuFire*, CapSteal, assets/Sms/Items/MarioCap (SmsMarioCap), the models from the rip (models/Eniemes/Hamukuri, Hanekuri, Dorokuri,
  > Dorohane, models/Sms/MarioCap). Changed: Gooble.cs / Gooble.tscn, Mario.cs (the shine demo's killEnemiesWithin on the base; the stolen-cap state
  > SmsCapStolen / SmsWearingCap), PROJECT_LOG.md (passes 125-125h).
  > Knobs: every kind's .prm numbers as statics; DropTable per scene (plain .tres resources); StackCount on the tower; Disguise on the goop Stu; the fx
  > slots (DeathFxScene, DeathFxWetScene, WallHitFxScene, FireFxScene, FireOffFxScene, StealFxScene) and the sound slots (empty).
  > Verification: drive 125 85/85 through the real input path -- the Stu (A-W), the Gooble (G1-G14), the winged (H1-H11), the goop (D1-D6), the fire
  > (F1-F5), the tower (S1-S5), the thief and the cap (W1-W7) plus the 125h checks; regression 118 35/35; build 0 errors; boot ALL OK; headless editor
  > runs clean. Playtested OK by Jaysonn (2026-10-25).
  > Not ported (logged in PROJECT_LOG.md): the launcher / the pools (TSmallEnemyManager's spawning), Yoshi's juice (changeByJuice), the sounds (slots),
  > the goop Stus' cap exchange on the ground (RobCap), the capless Mario's sun damage, the tower's attack wobble (a static the shipped game leaves
  > off), the map-object bounce.
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/enemy-base
```
git push origin feature/enemy-base
git rev-list --count origin/feature/enemy-base..feature/enemy-base
gh pr create --base main --head feature/enemy-base --title "Feature: the enemy base and the Stu family (SMS passes 125-125h)" --body-file pr125.md
```
Expect `0` from the rev-list. `gh` is still unauthenticated here (`gh auth status`: "not logged into any GitHub hosts") -- unless Jaysonn has run
`gh auth login` since, open the compare URL in the browser instead, paste the title and the body, and Jaysonn presses "Create pull request".
The working tree carries Jaysonn's own uncommitted `assets/Level.tscn` and `assets/Sms/Enemy/Drops/StuDrops.tres` (the Stu's single coin made a
BLUE coin -- his call whether it goes in this PR: if yes, `git add` it and commit it on the branch BEFORE the push), the editor's retab of
`assets/Sms/SmsHiddenCoin.cs`, the two Koopa `.png.import` flips and the `addons/godot_ai` churn -- leave them unless he says otherwise.

Result: all nine shas confirmed ancestors of the branch (code tip `4e10187`, 125g `0867da7`, 125f `7f1a436`, 125e `26f9666`,
125d `6f4c588`, 125c `27f9e38`, 125b `3f7927b`, 125 `e039b9c`, the fx re-saves `59e3dec`) plus `main` `985fb29`.
**Asked and answered -- `StuDrops.tres` IS IN**: Jaysonn said "include it!", so it was committed on the branch BEFORE the push
as `1cd23b3` ("Stu drops: the single-coin drop made a blue coin"), staging that one file only. Worth recording what that diff
actually was, since it looked bigger than it is: the ONLY behavioural change is the 80 %-weight entry swapping
`yellow_coin.tscn` -> `blue_coin.tscn` (the 12 % fan of three stays yellow). Everything else the editor stripped equals its
script default -- `Chance` 1, `UseEnemyRate` false, `CountMin`/`CountMax` 1, `ScatterUnits` 4, `LifeTicks` 0,
`BodyPopMetresPerSecond` 6, and `PopUnitsPerTick` 20 = `SmsCoinItem.PopUpSpeedUnits` (SmsCoinItem.cs:29) -- so Godot omitted
them on re-save and behaviour is unchanged. **General lesson: a Godot re-save that deletes property lines is not a content
change when each deleted value matches the script default -- check the defaults before assuming the resource was gutted.**
Branch pushed `b289ee9..1cd23b3`, 21 commits since `main`; `git rev-list --count origin/feature/enemy-base..feature/enemy-base`
returned `0`. Hygiene on the range: 0 `addons/godot_ai` paths, 0 Koopa `.png.import` flips, and the `SmsHiddenCoin.cs` retab
stayed out, as the rules require.
`gh auth status` still said "not logged into any GitHub hosts", so the prefilled compare page was opened in the browser
(title + `pr125.md`'s body urlencoded onto the compare URL, 4256 chars) for Jaysonn to submit.
Working tree otherwise untouched: `assets/Level.tscn`, `assets/Sms/SmsHiddenCoin.cs`, the two Koopa flips, `addons/godot_ai`.

Merged 2026-10-26: Jaysonn opened and **squash-merged** it as PR #7 -> `origin/main` `aaf6279`, single parent `985fb29`.
Because Kiro did NOT pre-merge locally this time (the one-way rule holding), local `main` had 0 commits of its own and this
was a clean `git merge --ff-only origin/main` -- no divergence, no conflict, nothing to reconcile. That is the whole point of
the rule: compare with #2/#3, which needed a reconciling merge commit.
Two things worth knowing about GitHub's squash here:
- **The squash cut at `1cd23b3`, not at the branch tip `ef67d77`.** GitHub squashes the PR head as it stood, and the PR was
  created and merged in the window between those two pushes, so the `timeToPush!.md` result paragraph above did NOT travel
  with the code and had to be re-applied onto `main` by hand (`git checkout feature/enemy-base -- timeToPush!.md`). All CODE
  landed -- only this log file lagged. Lesson: push the log commit BEFORE opening the PR page, not after.
- Ancestry checks are again useless after a squash: `git merge-base --is-ancestor ef67d77 origin/main` says NO. Verified by
  tree instead -- `git diff --name-only main feature/enemy-base` listed only `timeToPush!.md`, and spot checks confirmed
  `blue_coin.tscn` in `StuDrops.tres`, `SmsSmallEnemy.cs`, `models/Sms/MarioCap/MarioCap.glb` and
  `assets/Sms/Items/SmsMarioCap.cs` (note: the cap files sit directly in `assets/Sms/Items/`, NOT in an `Items/MarioCap/`
  subfolder as the PR body's wording suggests).
`pr125.md` deleted now that the PR exists and is merged.

### #6 -- PENDING -- 2026-10-26 02:10 PDT (Kiro)
Push the third feature branch so the work is visible on GitHub: `feature/enemy-spawners`, branched from `main` `e77b85f` (the trunk after PR #7).
PUSH ONLY, NO PR YET -- the feature is in progress: pass 126 (the generator, the one-shot generator, the common launcher) and pass 126b (the
gatekeeper: the Polluted Piranha) landed; its particle effects (126c) come next. Jaysonn plays the spawners first; when he says the feature is done a
later entry asks for the PR. Kiro does not merge this branch locally.
- Branch `feature/enemy-spawners` -- code tip `87cbba2` ("SMS pass 126f: the gatekeeper's effects inherit the goop sheet in use ..."), on top of `a38f59f`
  (126e), `76f1e3f` (126d), `c0a49da` (126c), `3386d73` (126b, the gatekeeper), `a70ead0` (126a) and `3cdf15b` (126); the branch tip is this file's latest
  commit. A NEW remote branch: push with `-u`.
- New files: `assets/Sms/Enemy/Spawn/` (SmsEnemyPool.cs, SmsEnemySpawner.cs, SmsEnemyOneShotSpawner.cs, SmsEnemyLauncher.cs, SmsGateKeeper.cs,
  SmsGateKeeperHead.cs, SmsGateKeeperBasePin.cs + .uids, Spawner.tscn, OneShotSpawner.tscn, Launcher.tscn, GateKeeper.tscn, LauncherBody / GateKeeperBody / GateKeeperSkirt
  .gdshader + .uid + .tres), `assets/Sms/Fx/GeneHit.tscn`, `GeneDead.tscn`, `Gk{Bota,Dead,DeadSmoke,Kemuri,KemuriW,YodareL,YodareS}.tscn`, `SmsGoopStrand.cs` + .uid,
  `JpaStripe.gdshader` + .uid, `tex/{indwp1,kemuball3_gkdead,yodare_raw}.png` + .imports, `models/Eniemes/Launcher/` (Launcher.glb 180 KB + .import,
  generator_model1.png + .import, launcher_post_import.gd + .uid), `models/Eniemes/GateKeeper/` (GateKeeper.glb 2.8 MB + .import, three small pngs +
  .imports); changed: `assets/Sms/Enemy/SmsSmallEnemy.cs`, `assets/Sms/Fx/SmsNpcEffect.cs`, the editor's re-saves of `assets/Sms/Enemy/*.tres`, PROJECT_LOG.md.
  Nothing over 3 MB.
- To try it: drop `assets/Sms/Enemy/Spawn/Launcher.tscn` (the goop pile that spits Stus; hose it five times) or `Spawner.tscn` (an invisible point that
  launches a Stu every IntervalTicks; the editor shows a ghost of the kind) into a level. EnemyScene picks the kind; PoolSize how many are out at once.
  `GateKeeper.tscn` is the Polluted Piranha: hose the mound ~2 s to wake it, then spray its head when the mouth is down (Kind: Airport / Bianco /
  Plaza / PlazaRicco / PlazaGelato on the node).
```
git push -u origin feature/enemy-spawners
git rev-list --count origin/feature/enemy-spawners..feature/enemy-spawners
```
Expect `0`. The working tree carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the two Koopa
`.png.import` flips and the `addons/godot_ai` churn -- leave them.

<!-- Kiro appends the next entry below this line. Entry template:

### #N -- PENDING -- <date> (Kiro)
<one line: what this is -- the feature or pass, and whether it is a push only, a push + PR, or a merged feature>
- Branch `feature/<name>` -- tip `<sha>` ("<last commit title>"), N commits since main.
- PR: title "<title>"; base `main`; body: (the drive results, what Jaysonn should playtest, what is not ported)
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/<name>
```
git push -u origin feature/<name>
```
-->
