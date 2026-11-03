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

### #6 -- DONE 2026-10-26 -- https://github.com/JasonAbsolute/ProjectShine/pull/8 (SQUASH-merged by Jaysonn) -- queued 2026-10-26 02:10 PDT, PR added 23:05 PDT (Kiro)
Push the third feature branch and OPEN ITS PR: `feature/enemy-spawners`, branched from `main` `e77b85f` (the trunk after PR #7) -- the enemy spawners
(the generator, the one-shot generator, the goop-pile launcher) and the Polluted Piranha gatekeeper with its effects and knobs (passes 126-126g).
PLAYTESTED BY JAYSONN 2026-10-26 ("everything looks great"; the drool tip flicker fixed in 126g: "Smooth now -- the spawners feature is done") -- the PR
can be merged once it is open (squash or merge commit, his call; `main` then needs a `git pull` here). Kiro has NOT merged this branch locally (the
entry asks for a PR: the one way).
- Branch `feature/enemy-spawners` -- code tip `fa9e054` ("SMS pass 126g: the drool tip no longer flickers ..."), on top of `87cbba2` (126f), `a38f59f`
  (126e), `76f1e3f` (126d), `c0a49da` (126c), `3386d73` (126b, the gatekeeper), `a70ead0` (126a) and `3cdf15b` (126); the branch tip is this file's latest
  commit; 17 commits since `main` `e77b85f`, this entry's own included; 70 files, +3369 / -27. A NEW remote branch (never pushed): push with `-u`.
  The biggest new file is GateKeeper.glb (2.8 MB).
- PR: title "Feature: the enemy spawners and the Polluted Piranha gatekeeper (SMS passes 126-126g)"; base `main`; body = `pr126.md` at the project root
  (untracked -- delete it after).
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/enemy-spawners
```
git push -u origin feature/enemy-spawners
git rev-list --count origin/feature/enemy-spawners..feature/enemy-spawners
gh pr create --base main --head feature/enemy-spawners --title "Feature: the enemy spawners and the Polluted Piranha gatekeeper (SMS passes 126-126g)" --body-file pr126.md
```
Expect `0` from the rev-list. If `gh` is still unauthenticated here (`gh auth status`), open the compare URL in the browser instead, paste the title
and the body of `pr126.md`, and Jaysonn presses "Create pull request". Once merged: `git checkout main && git pull` here, then tell Kiro the sha.
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
The working tree carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the two Koopa and the two
Goop (FireGoop / OilGoop) `.png.import` flips (the editor's detect_3d) and the `addons/godot_ai` churn -- leave them; nothing of the feature is uncommitted.

Result: all eight pass shas confirmed ancestors (code tip `fa9e054`, 126f `87cbba2`, 126e `a38f59f`, 126d `76f1e3f`, 126c `c0a49da`,
126b `3386d73`, 126a `a70ead0`, 126 `3cdf15b`) plus the trunk `e77b85f`. Pushed as a NEW remote branch with `-u`, tracking set;
`git rev-list --count origin/feature/enemy-spawners..feature/enemy-spawners` returned `0`. 17 commits since `main`.
Diffstat measured 70 files / **+3377** / -27 against the entry's +3369 -- the 8-line gap is this log file's own entry commit,
written after Kiro took the count. Largest blobs: `GateKeeper.glb` 2.69 MB, `PROJECT_LOG.md` 0.83 MB, `Launcher.glb` 0.17 MB.
Hygiene on the range: 0 `addons/godot_ai`, 0 Koopa or Goop `.png.import` flips, 0 `SmsHiddenCoin.cs` -- all four of Jaysonn's
live edits stayed out.
**Ordering change, learned from #5:** this result commit was made and pushed BEFORE the PR page was opened, so that if Jaysonn
squash-merges it travels with the code. In #5 the squash cut at the commit before the log commit and the result paragraph had
to be re-applied onto `main` by hand.
`gh auth status` still reports "not logged into any GitHub hosts", so the PR is opened from the prefilled compare page again
(5573-char URL, under the ~8000 safe limit; the body was also put on the Windows clipboard as a fallback against truncation).

Merged 2026-10-26: PR #8 **squash-merged** -> `origin/main` `14c07c4`, single parent `e77b85f`. Kiro again did not pre-merge,
so local `main` had 0 commits of its own and this was a clean `git merge --ff-only` -- second time in a row the one-way rule
made the pull trivial.
**The #5 ordering fix is confirmed working.** Committing and pushing the result paragraph BEFORE opening the PR page meant it
travelled with the squash this time: `git diff --name-only main feature/enemy-spawners` came back EMPTY (in #5 it listed
`timeToPush!.md` and the paragraph had to be re-applied onto `main` by hand). **Keep this order for every future PR entry:
push the branch -> commit and push the log result -> only then open the PR page.**
Verified by tree, not ancestry (`merge-base --is-ancestor ee769a9 origin/main` says NO, as always after a squash): 27 files
under `assets/Sms/Enemy/Spawn/` and all 7 `assets/Sms/Fx/Gk*.tscn` present on `main`, plus `SmsEnemySpawner.cs`,
`SmsGateKeeper.cs`, `Launcher.tscn`, `GateKeeper.tscn` and `GateKeeper.glb`.
`pr126.md` deleted now that the PR exists and is merged. Jaysonn's four live edits were never touched.
Next: tell Kiro the trunk sha for branching the next feature.

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

### #7 -- DONE 2026-10-29 -- https://github.com/JasonAbsolute/ProjectShine/pull/9 (SQUASH-merged by Jaysonn) -- queued 2026-10-27, PR added 2026-10-29 00:45 PDT (Kiro)
Push the fourth feature branch and OPEN ITS PR: `feature/gate-entry`, branched from `main` `4c96841` (the trunk after PR #8) -- the M-graffiti gate,
Mario's warp-in, the spiral wipe, the episode select screen and the hand-off through the character select to the level and back to the gate, with
all their effects (SMS passes 127-127i). PLAYTESTED BY JAYSONN 2026-10-28/29 ("I'm happy with this for now") -- the PR can be merged once it is open
(squash or merge commit, his call; `main` then needs a `git pull` here). Kiro has NOT merged this branch locally (the entry asks for a PR: the one way).
- Branch `feature/gate-entry` -- code tip `c491f02` ("SMS 127i: the episode select's glow quad placed behind the body ..."), on top of `a54e6f8` (127h),
  `e0f3d2f` (127g), `155f3a3` (127f), `06c9c33` (127e), `4db0ff9` (127d), `4af9915` (127c), `9469471` (127b), `79bd8a8` (127a) and `84a2c73` (127); the
  branch tip is this file's own commit on top of `81972f8` (the 127i log); 23 commits since `main` `4c96841`, this entry's own included; 151 files,
  +6833 / -15 before this entry's lines. A NEW remote branch (never pushed): push with `-u`. Nothing over 1 MB except the two logs that are already
  on `main` (Mario.cs 1.0 MB, PROJECT_LOG.md 0.9 MB); the biggest new file is SmsEpisodeSelect.cs (53 KB); the 50 textures are small PNGs.
- PR: title "Feature: the M-gate entry and the episode select (SMS passes 127-127i)"; base `main`; body = `pr127.md` at the project root
  (untracked -- delete it after).
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/gate-entry
```
git push -u origin feature/gate-entry
git rev-list --count origin/feature/gate-entry..feature/gate-entry
gh pr create --base main --head feature/gate-entry --title "Feature: the M-gate entry and the episode select (SMS passes 127-127i)" --body-file pr127.md
```
Expect `0` from the rev-list. KEEP THE #6 ORDER: push the branch -> write the result paragraph below, commit it and push -> only THEN open the PR
(so the result travels with a squash). If `gh` is still unauthenticated here (`gh auth status`), open the compare URL in the browser instead, paste
the title and the body of `pr127.md`, and Jaysonn presses "Create pull request". Once merged: `git checkout main && git pull` here, then tell Kiro the sha.
- New files: `assets/Sms/Gate/` (SmsModelGate.cs, SmsStageInfo.cs, SmsEpisodeInfo.cs, SmsEpisodeSelect.cs, SmsSelectShineRing.cs, SmsSelectPickBurst.cs,
  SmsWarpBalls.cs, SmsEpisodeOnly.cs + .uids, MGate.tscn, MGate.gdshader, SelectShineFull.gdshader, SelectShineEmpty.gdshader + .uids, EpisodeSelect.tscn,
  the three gate textures + .imports, `Stages/RiccoHarbor.tres`, `select/` 36 textures + .imports, `fx/` (WarpBalls / GateWind / GateHit / SelectSparkle /
  SelectSparkleEmpty / SelectPickBurst .tscn, WarpBall / GateBubble / JpaDir2D / JpaDirBillboardAddRamp / JpaStripeTrailAdd .gdshader + .uids, 11 textures +
  .imports)); changed: assets/Mario.cs (the warp-in spliced in; the demo state invincible), assets/GameData.cs, assets/CharacterSelect.cs + .tscn (four
  characters), assets/Level.cs (the spawn's facing), assets/Sms/Console/SmsFader.cs + Fader.gdshader (the spiral wipe, the fader colour),
  Levels/Rico4Secrect/Skybox/Rico4Secret.tscn (ShineSprite2 exits to the Plaza), PROJECT_LOG.md.
- To try it: drop `assets/Sms/Gate/MGate.tscn` into the Plaza (assets/Level.tscn) against a wall, its -Z (the M's face) toward where Mario stands; set
  `Stage` = `assets/Sms/Gate/Stages/RiccoHarbor.tres`. Walk up (it lights in ~2 s), jump into the M; the Rico shine brings you back out of it.
The working tree carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the three gatekeeper /
launcher `.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the `addons/godot_ai` churn: his, leave them; nothing of the feature
is uncommitted.

Result: all ten pass shas confirmed ancestors (code tip `c491f02`, 127h `a54e6f8`, 127g `e0f3d2f`, 127f `155f3a3`, 127e `06c9c33`,
127d `4db0ff9`, 127c `4af9915`, 127b `9469471`, 127a `79bd8a8`, 127 `84a2c73`), plus the 127i log commit `81972f8` and the trunk
`4c96841`. Pushed as a NEW remote branch with `-u`, tracking set; `git rev-list --count origin/feature/gate-entry..feature/gate-entry`
returned `0`. 23 commits since `main`.
Diffstat measured 151 files / **+6844** / -15 against the entry's +6833 -- the 11-line gap is this log file's own entry commit, as
the entry itself predicted ("before this entry's lines"). Largest blobs: `Mario.cs` 1.00 MB and `PROJECT_LOG.md` 0.88 MB (both
already on `main`, so not new weight), then `SmsEpisodeSelect.cs` 53 KB -- nothing else over 30 KB, the 50 textures all small.
Hygiene on the range: 0 `addons/godot_ai`, 0 Koopa or Goop `.png.import` flips, 0 `SmsHiddenCoin.cs`, and 0 of the three
gatekeeper / launcher `.tres` re-saves -- all nine of Jaysonn's live edits stayed out.
Kept the #6 order as the entry asked: branch pushed -> this paragraph committed and pushed -> only then the PR page opened.
`gh auth status` still reports "not logged into any GitHub hosts" (fourth entry running), so the PR is opened from the prefilled
compare page again.
FILE NOTE: Kiro appended this entry BELOW the "Kiro appends the next entry below this line" template comment rather than above it,
so the template block now sits between #6 and #7. Left exactly as found -- it is Kiro's file and the entry reads fine -- but worth
knowing when looking for the newest entry: grep `^### #` rather than reading to the end.

Merged 2026-10-29: PR #9 **squash-merged** -> `origin/main` `6fc7a6f`, single parent `4c96841`. Local `main` had 0 commits of its
own (Kiro did not pre-merge), so a clean `git merge --ff-only` -- third consecutive trivial pull under the one-way rule.
The #6 ordering held again: `git diff --name-only main feature/gate-entry` came back EMPTY, so this result paragraph travelled
with the squash instead of needing a hand re-apply. That is now 2 for 2 since the fix.
Verified by tree, not ancestry (`merge-base --is-ancestor be9538b origin/main` says NO, as always after a squash): **141 files**
under `assets/Sms/Gate/` on `main`, including `SmsModelGate.cs`, `SmsEpisodeSelect.cs`, `MGate.tscn`, `EpisodeSelect.tscn`,
`Stages/RiccoHarbor.tres` and `fx/WarpBalls.tscn`.
`pr127.md` deleted now that the PR exists and is merged. Jaysonn's nine live edits were never touched.
Trunk for Kiro to branch the next feature from: **`main`'s current tip** -- read it with `git log --oneline -1 main`. (Stated this way
on purpose: a commit cannot carry its own sha, and the "#7 done" commit that closes this entry IS the new tip, so any literal sha
written here would already be one commit stale.)

### #8 -- DONE 2026-10-30 -- https://github.com/JasonAbsolute/ProjectShine/pull/10 (SQUASH-merged by Jaysonn) -- queued 2026-10-29 02:20 PDT, PR added 2026-10-30 01:35 PDT (Kiro)
Push the fifth feature branch and OPEN ITS PR: `feature/enemy-pokey-cataquack`, branched from `main` `e9eb60b` (the trunk after PR #9) -- the
Cataquack (TPoiHana, SMS pass 128, painted any colour in 128b), the Pokey + Pokey Head (THanaSambo / TSamboHead, 129) and the Bob-omb (TBombHei, 130;
carried, thrown and its blast effect in 130b, the carry fixed in 130c) on the small-enemy base, with the Cataquack's and Pokey's particles (131).
PLAYTESTED BY JAYSONN 2026-10-29/30 ("everything is good for now") -- the PR can be merged once it is open (squash or merge commit, his call; `main`
then needs a `git pull` here). Kiro has NOT merged this branch locally (the entry asks for a PR: the one way).
- Branch `feature/enemy-pokey-cataquack` -- code tip `a6b4dd3` ("SMS 131: the Cataquack's and the Pokey's particles ..."), on top of `c0127c5` (130c),
  `d968bc9` (130b), `1778a42` (130), `f08ea69` (129), `636ac9c` (128b) and `c565f39` (128); the branch tip is this file's own commit on top of `5bad9aa`
  (the 130c + 131 log); 14 commits since `main` `e9eb60b`, this entry's own included; 156 files, +7517 / -7 before this entry's lines. A NEW remote
  branch (never pushed): push with `-u`. Over 1 MB: `models/Eniemes/Poihana/Poihana.glb` (1.7 MB, the Cataquack's model -- NEW) and the two files
  already on `main` (Mario.cs 1.0 MB, PROJECT_LOG.md 0.95 MB); then Sambo.glb 0.36 MB, SamboHead.glb 0.27 MB, NejiBomb.glb 0.22 MB and
  `assets/Sms/Enemy/Bomb/BombExplosion.tscn` (103 KB of baked curves); nothing else over 80 KB. The range carries NONE of Jaysonn's nine live edits
  (checked 2026-10-30: 0 addons, 0 Level.tscn, 0 SmsHiddenCoin.cs, 0 of the three .tres, 0 of the four .png.import flips).
- PR: title "Feature: the Cataquack, the Pokey and the Bob-omb (SMS passes 128-131)"; base `main`; body = `pr128.md` at the project root
  (untracked -- delete it after).
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/enemy-pokey-cataquack
```
git push -u origin feature/enemy-pokey-cataquack
git rev-list --count origin/feature/enemy-pokey-cataquack..feature/enemy-pokey-cataquack
gh pr create --base main --head feature/enemy-pokey-cataquack --title "Feature: the Cataquack, the Pokey and the Bob-omb (SMS passes 128-131)" --body-file pr128.md
```
Expect `0` from the rev-list. KEEP THE #6 ORDER: push the branch -> write the result paragraph below, commit it and push -> only THEN open the PR
(so the result travels with a squash). If `gh` is still unauthenticated here (`gh auth status`), open the compare URL in the browser instead, paste
the title and the body of `pr128.md`, and Jaysonn presses "Create pull request". Once merged: `git checkout main && git pull` here, then tell Kiro the sha.
- New files (131): `assets/Sms/Enemy/Fx131/` (nine .tscn, tex/ seven pngs + .imports); changed: SmsPoihana.cs, Sambo/SmsHanaSambo.cs,
  SmsSamboHead.cs, SmsSamboFlower.cs (the effect defaults), assets/Sms/Fx/SmsFxBurst.cs (a child's start delay). 130c changed: assets/Mario.cs
  (+2: the held enemy placed from his step), SmsSmallEnemy.cs, SmsEnemyHoldProxy.cs, Bomb/SmsBombHei.cs (CarryByBall), Bomb/BombExplosion.tscn +
  BombFuse.tscn (the emit counts; Godot's uids).
- New files (130b): `assets/Sms/Enemy/SmsEnemyHoldProxy.cs` + .uid, `assets/Sms/Enemy/Bomb/` (BombExplosion.tscn, BombFuse.tscn, BombFireball.gdshader
  + .uid, SmsBombFireball.cs + .uid, tex/ eight pngs + .imports), `assets/Sms/Fx/JpaBillboardKeyedAdd.gdshader` + .uid; changed: assets/Mario.cs (+67:
  the enemy branch of the take search, the proxy's attach / release, SmsHeldGone), assets/Sms/Enemy/SmsSmallEnemy.cs (the TTakeActor side: taken /
  released / riding his hand), assets/Sms/Enemy/Bomb/SmsBombHei.cs (PickUp / Thrown, the coin, the fx defaults), assets/Sms/Fx/SmsFxBurst.cs
  (ISmsFxBurstPart, the sub-emitter tail).
- New files (130): `assets/Sms/Enemy/Bomb/` (SmsBombHei.cs + .uid, BombHei.tscn, BombBody.gdshader + .uid, five .tres materials), `models/Eniemes/Bomb/`
  (two glbs, seven pngs, .imports).
- New files (129): `assets/Sms/Enemy/Sambo/` (SmsHanaSambo / SmsSamboHead / SmsSamboFlower / SmsSamboLeaf / SmsBoneRoll .cs + .uid, HanaSambo /
  SamboHead / SamboFlower / SamboLeaf .tscn, four .tres materials, SamboDown / SamboYdown .tres clips), `assets/Sms/Enemy/EnemyToonCutoutTwoSided.gdshader`
  + .uid, `models/Eniemes/Sambo/` (five glbs, twelve pngs, .imports); changed: EnemyToon.gdshaderinc (form 4), SmsSmallEnemy.cs (the facing fix),
  assets/Sms/SmsWaterDrops.cs (the bud zone test).
- New files (128): `assets/Sms/Enemy/` (SmsPoihana.cs + .uid, Poihana.tscn, PoihanaBody / PoihanaEye / PoihanaMouth .tres, EnemyToonBlend.gdshader + .uid),
  `models/Eniemes/Poihana/` (Poihana.glb + .import, default_0.png, default_1.png, M_poihana_eye.png + .imports); changed: assets/Sms/Enemy/
  EnemyToon.gdshaderinc (two TEV forms), assets/Sms/Enemy/SmsSmallEnemy.cs (four small helpers), assets/Mario.cs (ONE line: SmsThrowMario's thrower
  parameter widened to Node), PROJECT_LOG.md.
- To try it: drop `assets/Sms/Enemy/Poihana.tscn` into a level with its blue arrow (+Z) toward where Mario comes from. It wanders, charges when it sees
  Mario and FLINGS him straight up (33 m, the game's 140); `Kind` = Red hurts too, Sleeping starts asleep (a touch or the hose wakes it). The hose
  flips it; on sand (`GroundKind`, or a floor in the "sms_sand" group) it lies twitching 25 s and a stomp pays one coin -- Mario cannot kill one
  (only Yoshi could; `StompKills` is the knob). Paint group: `UseBodyColor` + `BodyColor` paints the body any colour (shows in the editor too);
  off = the kind's game blue / red. The particle slots carry the 131 defaults; the sound slots are empty.
- To try the Pokey (129): drop `assets/Sms/Enemy/Sambo/HanaSambo.tscn` on the ground with its blue arrow (+Z) toward where Mario comes from; it starts
  buried under a sunflower bud, rises when he is within 13 m, leans its head at him within 6.7 m (the head hurts; the stalk hurts on touch), the hose
  on its head freezes it, a stomp on the head kills it (the stack falls apart), it sinks back when he is 27 m away. `SamboHead.tscn` = the Pokey Head:
  buried too, pops when Mario is in front within 8 m (three leaves fly), hops 4 m at him every 2.5 s, water knocks it back (into a wall = three coins),
  touching it on the ground stuns it for a stomp. `SamboFlower.tscn` alone = a bud the hose opens. The particle slots carry the 131 defaults; the sound slots are empty.
- To try the Bob-omb (130): drop `assets/Sms/Enemy/Bomb/BombHei.tscn` anywhere; it marches at Mario at once and for ever (touching it does nothing);
  after 8.3 s its LED counts 3-2-1 and it blows up (a 2.7 m blast burns Mario: the fire knock). The hose douses it (it sits with the dizzy blue
  face) until the fuse runs out anyway. Two close together go off together. A DOUSED one can be picked up (130b): stand beside it on the ground
  and press B -- Mario lifts it (the sitting dizzy bomb rides his hands); B again throws it (always a throw: the game pitches an enemy even
  standing still) ~17 m along his facing in a 10.8 m arc, and it blows up where it lands, dropping a coin toward him (20 a stage, the game's
  budget). Hold it too long and its fuse runs out in his hands: the LED counts, it goes off there -- one HP and a knockdown. A walking or
  counting one cannot be taken, nor any while jumping. The blast is now the game's: the white flash, the growing magma fireball dissolving
  to embers, the smoke ring, the black scraps with their trails, the shockwave and its cyan sparks; the fuse smokes while it counts.
  `FuseScale` shortens the fuse for testing; `BlastScale` the reach and the look; the sounds are still slots. The carried bomb now sits on his
  hand with no trail (130c); `CarryByBall` on the bomb puts the ball's centre in his hand instead of its feet (off = the game).
- The particles (131) come on by themselves: the Cataquack's dizzy stars and sleep Zs, its landing dust / sand spray and death splash; the
  Pokey's pollen (off the crown every wait loop, puffed at you as it leans, at the bud's bloom), the rocks and smoke as it rises and sinks
  (the Pokey Head too). Each is a slot on the node (empty = none; the default scenes live in assets/Sms/Enemy/Fx131/).
The working tree carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the three gatekeeper / launcher
`.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the `addons/godot_ai` churn: his, leave them.

Result: all seven pass shas confirmed ancestors (code tip `a6b4dd3`, 130c `c0127c5`, 130b `d968bc9`, 130 `1778a42`, 129 `f08ea69`,
128b `636ac9c`, 128 `c565f39`), plus the 130c+131 log commit `5bad9aa` and the trunk `e9eb60b`. Pushed as a NEW remote branch with
`-u`, tracking set; `git rev-list --count origin/feature/enemy-pokey-cataquack..feature/enemy-pokey-cataquack` returned `0`.
14 commits since `main`.
Diffstat measured 156 files / **+7523** / -7 against the entry's +7517 -- the 6-line gap is this log file's own entry commit, as the
entry predicted ("before this entry's lines"). Largest blobs confirm the entry: `Poihana.glb` 1.65 MB (the only new file over 1 MB),
`Mario.cs` 1.00 MB and `PROJECT_LOG.md` 0.93 MB (both already on `main`, so not new weight), then `Sambo.glb` 0.35,
`SamboHead.glb` 0.26, `NejiBomb.glb` 0.22, `BombExplosion.tscn` 0.10 and `NejiBombDown.glb` 0.08 -- nothing else over 80 KB.
Hygiene re-checked independently of Kiro's own check and agrees: 0 of the nine live edits in the range (0 `addons/godot_ai`,
0 `Level.tscn`, 0 `SmsHiddenCoin.cs`, 0 of the three `.tres` re-saves, 0 of the four `.png.import` flips).
Kept the #6 order: branch pushed -> this paragraph committed and pushed -> only then the PR page opened.
`gh auth status` still reports "not logged into any GitHub hosts" -- fifth entry running on the browser route.

**NEW LIMIT HIT -- the prefilled-URL trick ran out at this entry.** `pr128.md` is 7124 bytes, which urlencodes to an
**8151-character** compare URL, past the ~8000 that is safe to hand a browser. Fallback used instead, and it works: open the
compare URL with ONLY `?expand=1&title=<urlencoded>` (180 chars) and put the body on the Windows clipboard with
`Set-Clipboard`, so Jaysonn clicks the description box, Ctrl+V, Create. **Procedure for future entries: measure the encoded URL
first; under ~8000 prefill both, over it prefill the title and clipboard the body.** PR bodies have been growing steadily
(#2 1.6 KB -> #6 4.8 KB -> #7 5.2 KB -> #8 7.1 KB), so the clipboard route is now the default expectation, not the exception.
Authenticating `gh` once would retire this whole workaround.

Merged 2026-10-30: PR #10 **squash-merged** -> `origin/main` `b353ec6`, single parent `e9eb60b`. Local `main` had 0 commits of
its own, so a clean `git merge --ff-only` -- fourth consecutive trivial pull under the one-way rule.
The #6 ordering held: `git diff --name-only main feature/enemy-pokey-cataquack` came back EMPTY, so this result travelled with
the squash. That is 3 for 3 since the fix.
Verified by tree, not ancestry (`merge-base --is-ancestor d9a25cb origin/main` says NO, as always after a squash): on `main`,
all **9** `assets/Sms/Enemy/Fx131/*.tscn`, **20** files under `Sambo/`, **32** under `Bomb/`, plus `SmsPoihana.cs`,
`Poihana.tscn`, `SmsHanaSambo.cs`, `SamboHead.tscn`, `SmsBombHei.cs`, `BombExplosion.tscn`, `SmsEnemyHoldProxy.cs` and the
1.65 MB `models/Eniemes/Poihana/Poihana.glb`.
`pr128.md` deleted now that the PR exists and is merged. Jaysonn's nine live edits were never touched.
CARRIED FORWARD from this PR's own "Not in this PR": four effects already on `main` are sized by the misread emitter rate
instead of the engine's fixed-interval division (enemy death puff 30 vs 15, wet death 30 vs 18, wall-crash puff 1 vs 12,
trample dust 12 vs 10). Kiro flagged it as a small follow-up pass on `main` -- not lost, just not in #8.
Trunk for the next feature: `main`'s current tip (`git log --oneline -1 main`).

### #9 -- DONE 2026-10-30 -- https://github.com/JasonAbsolute/ProjectShine/commit/491a9fc -- queued 2026-10-30 02:05 PDT, updated 2026-10-30 19:25 PDT (Kiro) -- PUSH main ONLY, no PR
Push the trunk: two small passes committed straight on `main` (the rules allow Kiro's small passes that are not part of a feature), after PR #10's
squash and the "#8 done" commit `e4fffc5` that the local Claude already pushed. SMS 132 corrects the emit counts of thirteen one-shot effect
emitters in nine scenes (the JPA engine's FixedInterval rule: an emit frame creates the volume's division, not the dumped rate -- the wall kick's,
the wall hit's and the stomp's single puffs become the game's puff rings; the death smoke thins; the cap-steal poof grows). SMS 133 adds Mario's
own effects from the disc: the water splash in and out (drops + rings), the splash at his feet running through shallow water, the sand puffs and
footprints on a sand floor, the Rocket Nozzle's launch burst, the hit burst beside the stars, and the Cataquack fling's flash. Judged by GPU
capture; build 0 errors, boot ALL OK, drive 128 32/32. Jaysonn read the 133 summary and said "ok cool" (2026-10-30 19:18 PDT). No branch, no
merge, no PR.
- Branch `main` -- code tip `52015b3` (the seven new 133 fx scene headers as the Godot editor re-stamped them on first sight: `load_steps` dropped
  by ResourceSaver::set_uid on ACTION_FILE_ADD, a load-progress hint only, 7 files +7 / -7) over `e1e242d` (the 133 log), `505917c` ("SMS 133:
  Mario's own effects from the disc ..."), `30681e4` (the 132 log) and `eb8b1f0` (SMS 132); the tip is this file's own commit on top of it;
  6 commits ahead of `origin/main` `e4fffc5` (132: 10 files +19 / -19; 133: 26 files +1752 / -10, all under assets/Sms/Fx/ and Fx131/ plus
  assets/Mario.cs +121 and SmsPoihana.cs). The range carries none of Jaysonn's nine live edits.
- To try 133: jump into the sea and out (the splash: big from a fall, small from a step); run through ankle-deep water; put a floor in the
  "sms_sand" group (or name it with "sand") and run on it -- puffs at every step, prints when running; fire the Rocket Nozzle; take a hit.
```
git push origin main
git rev-list --count origin/main..main
```
Expect `0`. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the
three gatekeeper / launcher `.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the `addons/godot_ai` churn: his, leave them.
No feature branch is in progress; the next one branches from this `main`.

Result: `main` `e4fffc5..491a9fc`, 6 commits, 38 files / +1894 / -29. `git rev-list --count origin/main..main` returned `0`, and
`main..origin/main` was `0` before the push, so it was a plain fast-forward push with no divergence. All five shas confirmed
ancestors of `main` (tip `52015b3`, 133 log `e1e242d`, 133 `505917c`, 132 log `30681e4`, 132 `eb8b1f0`) on top of `e4fffc5`.
Hygiene: 0 of Jaysonn's nine live edits in the range -- agrees with Kiro's own check.
**NO PR, and that is correct even though Jaysonn asked for one.** He said "make a pr" out of habit from #5-#8, but this entry is
`PUSH main ONLY`: the two passes are already committed on the trunk, so there is nothing to open a PR *from*. Manufacturing one
would mean branching main's own commits off and merging them back, which the standing rules forbid ("never push a feature
branch's commits onto main by hand"). Told him plainly rather than inventing a branch. **Rule of thumb for future entries: read
the entry's own heading for push-only vs push+PR -- it overrides a habitual "make a pr" in the chat.**
**The #8 follow-up is CLOSED by SMS 132.** Last entry carried forward four effects on `main` sized by the misread emitter rate;
checked the commit and all four are corrected exactly as predicted -- `EnemyDisap.tscn` 30 -> 15, `EnemyDisapWet.tscn` 30 -> 18,
`EnemyWallHit.tscn` 1 -> 12, `TrampleDust.tscn` 12 -> 10 -- plus nine more emitters across `PauseSparks`, `CapSteal`,
`HipDropLanding`, `SuperHipDropLanding` and `WallKickDust` (thirteen in nine scenes, as the entry says). Nothing left outstanding
from #8.
NOTE on the tip commit `52015b3`: it is the Godot editor's re-stamp of seven new 133 fx scene headers (`load_steps` dropped by
`ResourceSaver::set_uid` on file-add), 7 files +7 / -7. A load-progress hint only, no content change -- same class of thing as the
`StuDrops.tres` re-save in #5, and harmless to commit.
Trunk for the next feature: `main`'s current tip (`git log --oneline -1 main`).

### #10 -- DONE 2026-10-31 -- https://github.com/JasonAbsolute/ProjectShine/commit/a0659b9 -- queued 2026-10-30 20:40 PDT, updated 2026-10-31 (Kiro) -- PUSH main ONLY, no PR
Push the trunk: small passes committed straight on `main` (the rules allow Kiro's small passes that are not part of a feature), after the #9 push
(`origin/main` `b32c023`, the "#9 done" commit). SMS 134 adds the dive splash MODEL from the disc -- mario/04_tobikomi in chr0.szs: the white
water column, the crown of droplets and the surface ring, the game's own five materials (one shader) and its bck / bpk / brk / btk baked a key a frame
into one AnimationPlayer clip -- fired by Mario beside the 133 splash particles: the one visible gap of 133 (drive 134 20/20). SMS 136 adds the
GOROGORO, Bianco Hills' rolling goop head (Petey's head under the stage's goop; igaiga.cpp): the roll along a graph or straight on, the landing
bounces with their splats, the goop trail, the hose washing the goop off to Petey's skin (never a kill), the stomp squash, the melt into water, the
stuck kill at a wall; the Bianco gatekeeper now spits it by default (its slot was any enemy). Drive 136 24/24 through the real paths. SMS 137 makes
the shallow-water flag the game's: a POOL is an sms_water Area3D in the group `sms_pool` (the fountain basins, the puddles) -- Mario wades it whatever
its depth and never swims it, the small splash and the small splash model are the pool's (a plain shallow shelf gives the big particles and no model,
as the game), the goop melt's small scale too; the 133 stand-in "under 50 u of water" is gone. Drive 137 8/8, drive 134 20/20. SMS 138 puts the
warp-in's light balls at the game's FixedInterval counts (head / cap / FLUDD 66 each: 458 balls, not 620; `GameCounts` off on WarpBalls.tscn = the
127b look) and gives the goop winged Stu a `StealPuffDensity` knob for its cap-steal poof (1 = the game's 38). Each pass: build 0 errors, boot ALL OK,
looks judged by GPU capture. No branch, no merge, no PR. (Two of eight headless boots this batch died at exit on Godot's C# finalize assertion AFTER
the self-test passed -- an exit-time race, not seen before this batch; the 138 log entry has the details. Tell Kiro if the game crashes on quit.)
- Branch `main` -- code tip `da3c478` ("SMS 138: the warp-in's light balls at the game's FixedInterval counts ...", SmsWarpBalls.cs +14 / -7,
  SmsDoroHaneKuri.cs +6 / -1) over `0690b2d` (the 137 log), `4cc2902` ("SMS 137: MARIO_FLAG_IN_SHALLOW_WATER proper ...", assets/Mario.cs +21 / -7), `25926a5` (the 136 log),
  `8af428e` ("SMS 136: the Gorogoro ...", 23 files +1234: assets/Sms/Enemy/Gorogoro.tscn + SmsGorogoro.cs, assets/Sms/Enemy/Gorogoro/ (the body
  shader + material, four fx scenes), models/Eniemes/Gorogoro/ (the glb, five pngs, their .import), SmsGateKeeper.cs +3), `0baa25a` (the 134 log),
  `440f24d` (SMS 134: 12 files +736 / -5); the tip is this file's own commit (the 138 log + this entry) on top of it: 8 commits ahead of
  `origin/main` `b32c023`. The range carries none of Jaysonn's nine live edits.
- To try 137: add the group `sms_pool` to a water Area3D (the Node tab > Groups) that should be a wading pool -- Mario walks through it however deep
  it is and splashes small; leave the sea without it.
- To try 136: drop assets/Sms/Enemy/Gorogoro.tscn in a level facing the way it should roll (or leave the Bianco gatekeeper to spit them); hose it to
  wash Petey's head out of the goop; stomp it; let one roll into water. Knobs on the node: Goop (the sheet / tint), Deviations (DeathStampScale: the
  game's 25.8 m goop stamp under a stomped head -- turn it down if it reads too big; LeavesTrail / TrailScale; SpeedScale), Effects, Sounds.
- To try 134: dive into deep water from a jump or a ledge -- the white column with the crown and the ring beside the drops; step into a pool
  under 50 u deep (or walk in from a beach) -- the squat small one. Knobs on assets/Sms/Fx/WaterSplashModel.tscn: SizeScale, Intensity (1 = the game).
```
git push origin main
git rev-list --count origin/main..main
```
Expect `0`. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the
three gatekeeper / launcher `.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the `addons/godot_ai` churn: his, leave them.
`feature/select-stages` (entry #11) is a separate branch off `0baa25a`; it gets pushed by its own entry.

Result: `main` `b32c023..a0659b9`, **9** commits, 39 files / +2236 / -17. `git rev-list --count origin/main..main` returned `0`,
and `main..origin/main` was `0` before the push, so a plain fast-forward with no divergence. All four code shas confirmed
ancestors on top of `b32c023` -- 138 `da3c478`, 137 `4cc2902`, 136 `8af428e`, 134 `440f24d` -- with their log commits
`8db0390` / `0690b2d` / `25926a5` / `0baa25a` interleaved. Every per-pass diffstat matches the entry exactly: 134 = 12 files
+736 / -5, 136 = 23 files +1234, 137 = `assets/Mario.cs` +21 / -7, 138 = `SmsWarpBalls.cs` +14 / -7 and
`SmsDoroHaneKuri.cs` +6 / -1.
NOTE on the ahead-count: the entry says 8 (corrected in `a0659b9` from an earlier number) but the real figure is **9** --
`a0659b9` is the correction commit itself, which cannot count itself. Same off-by-one as every previous entry: the log file's
own commit is always the one not counted. Not a problem, just the reason the numbers never match on the nose.
Largest new blobs: `bosspaku_head.glb` 110 KB, `WaterSplashModel.tscn` 53 KB (the 79-frame-a-key clip), `SmsGorogoro.cs` 30 KB,
the five Gorogoro pngs 11-15 KB each. Nothing near the `.import` bloat problem.
Hygiene: 0 of Jaysonn's nine live edits in the range -- agrees with Kiro's own check. Note `assets/Sms/Enemy/Spawn/SmsGateKeeper.cs`
IS in the range (+3, the Bianco kind's Gorogoro default); it is a different file from the three `GateKeeper*.tres` / `LauncherBody.tres`
re-saves sitting uncommitted in Jaysonn's tree, so no collision.
**NO PR, and that is correct even though Jaysonn asked for one again.** Second entry running: the heading says `PUSH main ONLY`
and the four passes are already on the trunk, so there is nothing to open a PR *from*. The #9 rule of thumb held -- read the
entry heading, it overrides a habitual "make a pr" in the chat.
**Entry #11 pushed in the same pass** (its text lives only on `feature/select-stages`, so its Result is committed there, not here).
`feature/select-stages` is now on GitHub as a new branch, 2 commits over `0baa25a`, and deliberately has **no PR**: the entry says
"do NOT open a PR until he says the feature is done". Jaysonn has not playtested SMS 135 yet.
Carried forward for the next entry: **the exit-time C# finalize crash.** Two of eight headless boots in this batch (one in 137,
one in 138) printed `ALL OK` and then died at exit on `csharp_lang && !csharp_lang->script_bindings.is_empty()`
(csharp_script.cpp 179) with a "leaked unsafe reference" dump. Not seen in the ~20 boots before this batch. Kiro's suspect is the
Bianco gatekeeper filling its pool of ten Gorogoros at Level boot. Watch for the game crashing on quit; if it does, that is the
bisect to run.
Trunk for the next feature: `main`'s current tip (`git log --oneline -1 main`).

### #12 -- DONE 2026-10-31 -- https://github.com/JasonAbsolute/ProjectShine/pull/11 (SQUASH-merged by Jaysonn) -- queued 2026-10-31 01:02 PDT, PR added 2026-10-31 17:45 PDT (Kiro) -- PUSH + PR
Push the seventh feature branch: `feature/enemy-ricco`, branched from `main` `55514c8` (the trunk after SMS 138 and entry #10). Ricco Harbor's
enemy family, one pass each on this branch. SMS 139: the Klamber (TKumokun), the wall-walking spider -- assets/Sms/Enemy/Kumokun.tscn +
SmsKumokun.cs, its body material and model under assets/Sms/Enemy/Kumokun/ and models/Eniemes/Kumokun/, and seven public getters on Mario.cs (what
the Klamber reads of him: fencing / roofing, the fence normal, the punch and kick windows). SMS 140: the small Blooper (TMameGesso), the hopping squid
that floats on the water and flattens into a platform under the hose -- assets/Sms/Enemy/MameGesso.tscn + SmsMameGesso.cs, its material and model
under assets/Sms/Enemy/MameGesso/ and models/Eniemes/MameGesso/. SMS 141: the land Blooper (TGesso / TLandGesso / TSurfGesso), the big squid
that sits on a walkway's edge, leans after Mario, spits goop balls, hangs under the walkway when he is below it, and walks the ground once the hose
knocks it off -- assets/Sms/Enemy/Gesso.tscn + SmsGesso.cs, its materials and models under assets/Sms/Enemy/Gesso/ and models/Eniemes/Gesso/, and the
water-surface helpers moved from SmsMameGesso.cs into the base SmsSmallEnemy.cs. SMS 142: the Cheep Cheep (TTobiPuku with its launch pad,
TPukuPuku), the fish that leaps out of the water in an arc, splashes down, loops under and swims back to leap again, or swims under the surface --
assets/Sms/Enemy/Puku.tscn + SmsPuku.cs, its material and model under assets/Sms/Enemy/Puku/ and models/Eniemes/Puku/. SMS 143: the Wind Spirit
(TKazekun), the glowing wisp that stays hidden until Mario comes near, circles over his head, tumbles to wind up and dashes through him, and drops
when sprayed -- assets/Sms/Enemy/Kazekun.tscn + SmsKazekun.cs, its two shaders and materials under assets/Sms/Enemy/Kazekun/, its model under
models/Eniemes/Kazekun/. That is the whole Ricco Harbor family (five species in five passes). APPROVED BY JAYSONN 2026-10-31 17:30 PDT
("ok ... tell claude how to get these PRed"): push the branch and OPEN ITS PR -- it can be merged once it is open (squash or merge commit, his
call; `main` then needs a `git pull` here). Kiro has NOT merged this branch locally (the entry asks for a PR: the one way). No more Ricco passes
are planned.
- Branch `feature/enemy-ricco` -- code tip `f0a834d` ("SMS 143: the Wind Spirit ...", 19 files +631: Kazekun.tscn, SmsKazekun.cs (+ .uid), the
  two shaders (+ .uid) and two .tres, the glb with four pngs and their .import files) on `c73f8c1` (the 142 log), `85f17aa` (SMS 142, 12 files +858),
  `5f10da6` (the 141 log), `50bda5f` (SMS 141, 17 files +1106 -25), `eea8e68` (the 140 log), `949e16f` (SMS 140, 12 files +763), `0bbbfcc` (the 139
  log) and `fb28f13` (SMS 139, 13 files +1068); the branch tip is this file's own commit on top of it; 10 commits since `main` `55514c8`. The range
  carries none of Jaysonn's nine live edits.
- To try 139: drop assets/Sms/Enemy/Kumokun.tscn into the Ricco test level within 1.3 m of a wall (a net), under a ceiling, or on a floor -- it
  attaches at its first tick and roams that surface. For Mario to climb the net its StaticBody3D (or a parent) needs metadata `sms_fence` (any
  value) or the group `sms_fence`; the Klamber walks any wall. On the net: it chases him when he is on the SAME side, hurts him on touch (through
  the net too -- the 'Attacks Across The Net' knob turns that off); punch it (B on the net) from the OTHER side to knock it off; the hose curls
  it up; stomp a floor one; ground-pound the slab over a ceiling one. Knobs: Deviations (SpeedScale, SearchRangeUnits, FlySpeedUnits,
  AttacksAcrossTheNet), Effects (the fly smoke slot), Sounds (four slots), RelaunchTicks 1800 (-1 = gone for good), Graph = a closed loop of
  Node3D children fencing the area it may roam.
- To try 140: drop assets/Sms/Enemy/MameGesso.tscn on a dock or on the water (an sms_water area: it floats on the surface) -- it hops from point to
  point within WanderRadiusUnits (or along a Graph's nodes), waits, hops again; its touch hurts. Hose it (or stomp it) and it flails stunned: B picks
  it up, B again throws it. Hose one FLOATING on the water and it flattens into a platform you can stand on (the Platform child's box) for
  ObjectRecoverTicks (25 s in the game), then swims off. Only a ground pound on a hopping one kills it. Knobs: Deviations (ObjectRecoverTicks,
  PlatformHeightUnits, WanderRadiusUnits), Effects (the water column: the small splash by default), Sounds (four slots), RelaunchTicks 300.
- To try 141: drop assets/Sms/Enemy/Gesso.tscn ON the edge of a walkway or beam, facing out over the edge (the yaw you give it is the way it looks;
  Kind = Regular) -- it sits there, leans after Mario, and every few seconds spits a goop ball at him that splats a goop stamp where it lands;
  stand under it (in front of the edge) and it rolls over to hang under the walkway; get above it again and it rolls back. Hose it from the side
  and it is knocked off, drops through the walkway to the ground and walks from then on. Kind = Land is a ground walker from the start: it
  searches, charges and punches; the hose paralyses a walker (the dizzy stars) for 10 s; a stomp kills a walker or an upright sitter (never a
  hanging one); knocked over water it drowns. Kind = Surf is the walker that never chases him. Knobs: Kind, Deviations (SpeedScale), Effects
  (the goop splat, the spit, the dizzy stars, the water column), Sounds (five slots), RelaunchTicks -1 by default (gone for good).
- To try 142: drop assets/Sms/Enemy/Puku.tscn IN the water with its origin at the surface, turned the way the fish should fly (Kind = Leaper: the node
  is the launch pad) -- a fish rises from under it, leaps in an arc (Launch pad: LaunchSpeedUnits = how hard, 18 clears about 7.7 m up and 11 m far;
  LaunchPitchDegrees = how steep), splashes down, loops under the surface, swims back to the pad and leaps again, forever. Stand in its path and it
  hurts you and drops; one that lands on a dock bounces, flops about (its touch hurts) and dies after 13 s; hose one in the air and it is flung away
  and drops dead into the water; stomp a flopping one. The pad launches a new one as soon as its fish dies. Kind = Swimmer: put it under the surface
  and it swims from point to point (a Graph of Node3D children, or within WanderRadiusUnits), bobbing; it bites a swimming Mario (set RelaunchTicks -1
  for one that stays dead). The water must be an sms_water area deep enough for the leap's rise (3 m or more). Knobs: Launch pad, Deviations
  (DiveDepthUnits, Bounces, SpeedScale), Effects (the water column, the swim bubbles, the flop drops), Sounds (five slots).
- To try 143: drop assets/Sms/Enemy/Kazekun.tscn anywhere in the air where the spirit should live (its place; it flies and passes through walls and
  floors, as the game's does). It stays invisible until you come within about 13 m, then puffs into view, circles about 10 m out and 2.7 m over your
  head for 4 s or so, tumbles in place to wind up, and dashes through you (its touch hurts) before vanishing in a puff; 2.5 s later it is back at
  its place waiting for you. Spray it with water while it is out (circling, winding up or dashing) and it drops with the hit clip and stays away
  12.5 s. Stomp it while it is out (easiest right after the spray, while it hangs) and it is gone for good (RelaunchTicks -1; set 1800 for one that
  comes back in 15 s). It gives up and vanishes if you get 8 m under its place or 27 m over it. Knobs: Deviations (SpeedScale), Effects (the appear
  puff -- the enemy death puff by default -- and the wind streaks slot), Sounds (six slots).
- PR: title "Feature: the Ricco Harbor enemies -- the Klamber, the Bloopers, the Cheep Cheep and the Wind Spirit (SMS passes 139-143)"; base
  `main`; body = `pr139.md` at the project root (untracked -- delete it after). Measured 2026-10-31: the body is 4.6 KB and the fully prefilled
  compare URL (title + body urlencoded) is 6958 characters, under the ~8000 that #8 found to be the limit, so prefilling both works; if the
  browser balks, prefill the title only and `Set-Clipboard` the body. 74 files, +4920 against `main`; the only new file over 1 MB is
  models/Eniemes/Gesso/geso_model1.glb (1.19 MB), then Puku 0.56, Kumokun 0.47, MameGesso 0.43, Kazekun 0.09 (PROJECT_LOG.md and Mario.cs are
  already on `main`). Hygiene checked 2026-10-31: 0 of the nine live edits in the range (0 addons, 0 Level.tscn, 0 SmsHiddenCoin.cs, 0 of the
  three .tres, 0 of the four .png.import flips).
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/enemy-ricco
```
git push -u origin feature/enemy-ricco
git rev-list --count origin/feature/enemy-ricco..feature/enemy-ricco
gh pr create --base main --head feature/enemy-ricco --title "Feature: the Ricco Harbor enemies -- the Klamber, the Bloopers, the Cheep Cheep and the Wind Spirit (SMS passes 139-143)" --body-file pr139.md
```
Expect `0` from the rev-list. STACKED ENTRY, so the order differs from #6-#8: the checkout is `feature/enemy-pinna` (#13 stacks on this branch;
do not check this one out -- `git push -u origin feature/enemy-ricco` pushes the local branch ref from where you are). Write this entry's result
paragraph in THIS file, commit it on `feature/enemy-pinna` and push THAT branch (`git push -u origin feature/enemy-pinna`: #13's push, due now
anyway); the result travels with #13's squash instead of this one's. Then open the PR: if `gh` is still unauthenticated (`gh auth status`), open
the compare URL with `?expand=1&title=<urlencoded>&body=<urlencoded>` (or the title only + the body on the clipboard) and Jaysonn presses
"Create pull request". Once merged: `git checkout main && git pull` here, mark this entry DONE (on `main`, as the procedure says), then go on to
#13. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the three
gatekeeper / launcher `.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the `addons/godot_ai` churn: his, leave them.

Result: `feature/enemy-ricco` pushed as a NEW remote branch, 10 commits over `main` `55514c8`, 74 files / +4920 / -0.
`git rev-list --count origin/feature/enemy-ricco..feature/enemy-ricco` returned `0`. All nine shas confirmed ancestors of the
branch before the merge: `f0a834d` (143), `c73f8c1`, `85f17aa` (142), `5f10da6`, `50bda5f` (141), `eea8e68`, `949e16f` (140),
`0bbbfcc`, `fb28f13` (139), under the branch tip `76c46d8`. File count and insertion total match the entry exactly. Largest new
files as stated: `geso_model1.glb` 1.19 MB, Puku 0.56, Kumokun 0.47, MameGesso 0.43, Kazekun 0.09. Only four files are modified
rather than added (`PROJECT_LOG.md`, `assets/Mario.cs`, `assets/Sms/Enemy/SmsSmallEnemy.cs`, `timeToPush!.md`), so the 70 others
are new and nothing of Mario's movement is touched beyond the seven getters. Hygiene: 0 of Jaysonn's nine live edits in the range.
PR #11 opened through the browser (`gh auth status` still prints "You are not logged into any GitHub hosts", the sixth entry
running). The compare URL prefilled with title and body measured 7183 characters including the attribution footer, under the
~8000 ceiling #8 established, so both fields prefilled and the clipboard fallback was not needed.
MERGED BY JAYSONN as a **SQUASH**: `main` is now `cbc812d`, one commit with a single parent `55514c8`, titled with the PR title
and "(#11)". Verified the way a squash requires, by TREE and not by ancestry: `git merge-base --is-ancestor f0a834d main` says
NO, as a squash implies, while `git diff --name-only feature/enemy-ricco main` lists only `timeToPush!.md` and nothing else
outside the log files, so every one of the 70 new files and both changed source files are byte-identical on the trunk. Spot
checked all five scenes present (`Kumokun`, `MameGesso`, `Gesso`, `Puku`, `Kazekun`) and `assets/Mario.cs` plus
`assets/Sms/Enemy/SmsSmallEnemy.cs` identical to the branch. `pr139.md` deleted.
NOTE on the pull: this repo has `pull.rebase` set, so `git pull` refused with "cannot pull with rebase: You have unstaged
changes". Used `git merge --ff-only origin/main` instead, which fast-forwards without touching the working tree. Jaysonn's nine
live edits were all still present afterwards, checked. Do NOT stash them to satisfy a rebase.
NOTE on this heading: the squash carried the Ricco branch's copy of this file, whose last entry commit was `76c46d8`, so `main`
arrived with the stale "#12 PUSH ONLY, no PR yet" heading. Kiro's `db9f9bf` update that re-marked #12 and #13 as PUSH + PR sits
on `feature/enemy-pinna` only. Corrected here. For the same reason `main` still has no #11 and no #13 section: #11's lives on
`feature/select-stages` and #13's on `feature/enemy-pinna`, and each arrives with its own merge.
Next: #13's merge-and-PR steps, which need `main` merged into `feature/enemy-pinna` first.

### #13 -- DONE 2026-10-31 -- https://github.com/JasonAbsolute/ProjectShine/pull/12 (SQUASH-merged by Jaysonn) -- queued 2026-10-31 15:15 PDT, PR added 2026-10-31 17:45 PDT (Kiro) -- PUSH + PR, the PR AFTER #12 HAS MERGED (stacked)
Push the eighth feature branch: `feature/enemy-pinna`, branched from the RICCO branch's tip `76c46d8` (the 143 log commit; see #12), not from
`main`: the Pinna species lean on the Ricco branch's base and Mario work (the water-surface helpers in SmsSmallEnemy.cs, Mario's fence getters
from 139) and the Klamber's net mechanics. Pinna Park's enemy family, one pass each on this branch. SMS 144: the Electrokoopa on the net
(TAmiNoko), the crackling Koopa that crawls the Ferris wheel's nets from node to node, shocks Mario on its side of the net and is punched off
from the other -- assets/Sms/Enemy/AmiNoko.tscn + SmsAmiNoko.cs, its material under assets/Sms/Enemy/AmiNoko/, its model under
models/Eniemes/AmiNoko/. SMS 145: the Snooza Koopa (TTamaNoko), the big Koopa asleep in its shell on the sand that a long hosing wakes, that runs
at Mario and leaps (the hang, the drop), that lands stuck in the sand for his ground pound -- a sunflower grows and bursts five coins -- or flat on
hard ground for his pick-up and throw -- assets/Sms/Enemy/TamaNoko.tscn + SmsTamaNoko.cs, the sunflower and the materials under
assets/Sms/Enemy/TamaNoko/, the models under models/Eniemes/TamaNoko/. SMS 146: the Monty Mole in its cannon (TCannon + TChorobei), Pinna
Beach's three-barrelled cannon that turns to Mario, whose mole lobs Bob-ombs at him, ducks inside when he comes close and takes three Bob-ombs
blown up on it before the cannon breaks to a prop -- assets/Sms/Enemy/Cannon.tscn + SmsCannon.cs, the mole's material under
assets/Sms/Enemy/Cannon/, the models under models/Eniemes/Cannon/. That is the whole Pinna Park family (three species in three passes). APPROVED BY
JAYSONN 2026-10-31 17:30 PDT ("ok ... tell claude how to get these PRed"): push the branch now (with #12) and OPEN ITS PR AFTER #12's has
merged -- it stacks on Ricco: the squash of #12 makes Ricco's commits non-ancestors of `main`, so until `main` is merged into this branch (the
steps below) the Pinna PR would show Ricco's files and conflict on this file. Kiro has NOT merged this branch locally (the entry asks for a PR:
the one way). No more Pinna passes are planned.
- Branch `feature/enemy-pinna` -- code tip `45574ec` ("SMS 146: the Monty Mole in its cannon ...", 16 files +760: Cannon.tscn, SmsCannon.cs
  (+ .uid), Cannon/TyorobeBody.tres, the three glbs with their pngs and .import files) on `ab46ae3` (the 145 log), `a702b49` (SMS 145, 16 files
  +850), `6537026` (the 144 log), `858718c` (SMS 144, 8 files +744) and `76c46d8` (the Ricco tip: the ten commits of #12); the branch tip is this
  file's own commit on top of it; 18 commits since `main` `55514c8`, 8 of them Pinna's (this PR update and the Kazekun.tscn uid line included). The range over the Ricco
  tip: 42 files, +2735; the largest new files tamanoko_model1.glb 0.92 MB, aminoko_model1.glb 0.81, tyorobe_model1.glb 0.63, tamaflower_model1.glb
  0.21 (nothing over 1 MB); nothing of Mario's or the base's changed. It carries none of Jaysonn's nine live edits (checked 2026-10-31: 0 addons,
  0 Level.tscn, 0 SmsHiddenCoin.cs, 0 of the three .tres, 0 of the four .png.import flips).
- To try 144: build a NET (a thin StaticBody3D with metadata sms_fence, or in the group sms_fence, so Mario can climb it), drop
  assets/Sms/Enemy/AmiNoko.tscn ON it (within 0.4 m of the face; it also takes a wall, a ceiling or a floor) and give it a Graph: a Node3D child
  whose children are the points of its path on the same face -- it crawls from point to point (a random other one each time), stops and turns at
  each. Its body and the charged air 1.3 m out from its back shock you on ITS side of the net (the shock pose, -1 HP); it then sits stunned a few
  seconds (the air stays charged). Climb the OTHER side and punch it (B) -- it flies off its back and vanishes in a puff; a ground pound kills
  one on a floor; the hose stuns it; jumping on it only bounces you. Knobs: Deviations (SpeedScale, ClipSet 1 = Pinna Park's clips / 2 = Pianta
  Village's, FeetOnPlane, AttacksAcrossTheNet = the game's rule: standing right behind the net within its reach shocks you through it), Effects
  (the sparks, the stun stars, the knock-off smoke, the wall puff), Sounds (five slots); RelaunchTicks -1 = gone for good (1800 = back in 15 s).
- To try 145: drop assets/Sms/Enemy/TamaNoko.tscn on SAND -- a floor whose StaticBody3D is in the group sms_sand or has "sand" in its name (the
  Deviations knob Ground = Sand forces it) -- and it sleeps there in its shell (it is big: 6 m). Hose it for about two seconds (WakeSeconds): it
  shakes, then stretches out and wanders (a Graph of Node3D children, or within WanderRadiusUnits); stop hosing early and it dozes off again. When it
  sees you it runs at you and leaps -- a somersault at the top, a hang, a straight drop onto where you stood (its touch hurts: move). Landing on
  sand it is stuck and struggles: ground-pound it then and it dies -- a sunflower grows there and bursts five coins. Landing on hard ground it lies
  flat for two seconds: pick it up (B) and throw it (B again), or wait and it gets up; held too long it wakes in your hands and hurts you. Walk far
  off and it goes back to sleep. Knobs: Deviations (SpeedScale, Ground, WakeSeconds), Effects (the Zs, the landing puffs, the stretch burst, the
  sunflower and its coin, and slots for the sand column, the fall streaks, the flower puff and the shake sparkle), Sounds (seven slots);
  RelaunchTicks -1 = gone for good.
- To try 146: drop assets/Sms/Enemy/Cannon.tscn on flat ground with room around it (it is 9 m across; +Z is its front) -- the mole pops up on
  top and the cannon turns to face you. Stand 10 to 40 m off and every 1.7 s the mole lobs a Bob-omb at you; it lands, walks at you and blows
  (or sits dizzy if you hose it: pick it up with B and throw it with B). Come within 6.7 m and the mole ducks inside (the lid's puff) until you
  are 20 m off. To beat it: throw a Bob-omb so it blows up ON TOP of the cannon by the mole -- the smoke and scraps, the mole flinches, the cannon
  hops; three hits and the mole collapses, the cannon blows and stays as a harmless prop. Touching the cannon itself hurts nothing; the mole's
  reach is 2.7 m around the top of it. The barrels' Bullet Bills need a scene in Shots > KillerScene (none yet). Knobs: Deviations (SpeedScale,
  MoleHeightUnits, HitPoints), Shots (the Bob-omb, the Bullet Bill), Effects (the blast; slots for the mole's smoke, the sweat, the lid's puff),
  Sounds (seven slots); RelaunchTicks -1 = gone for good.
- PR: title "Feature: the Pinna Park enemies -- the Electrokoopa, the Snooza Koopa and the Monty Mole cannon (SMS passes 144-146)"; base
  `main`; body = `pr144.md` at the project root (untracked -- delete it after). Measured 2026-10-31: the body is 4.9 KB and the fully prefilled
  compare URL is 7331 characters, under the ~8000 limit, so prefilling both works; if the browser balks, prefill the title only and
  `Set-Clipboard` the body.
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/enemy-pinna
```
git push -u origin feature/enemy-pinna
git rev-list --count origin/feature/enemy-pinna..feature/enemy-pinna
```
Expect `0` from the rev-list (push now, together with #12). THEN WAIT for #12's PR to merge. After it, with `main` pulled and #12 marked DONE on
`main`:
```
git checkout feature/enemy-pinna
git fetch origin
git merge origin/main
```
The code merges clean (both sides carry the identical Ricco changes: the squash on `main`, the original commits here); expect conflicts in
`PROJECT_LOG.md` and / or `timeToPush!.md` only. PROJECT_LOG.md: if `git diff --stat 76c46d8 origin/main -- PROJECT_LOG.md` prints nothing,
`git checkout --ours PROJECT_LOG.md` (this branch's copy: entries 139-146); else hand-merge (139-143 once, then 144-146). timeToPush!.md:
hand-merge -- everything through #12 from `origin/main` (its DONE header and result paragraph), #13 from this branch; nothing lost, nothing
twice. Then `git add PROJECT_LOG.md "timeToPush!.md" && git commit` (the merge commit), `git push origin feature/enemy-pinna`, and check that
`git diff --name-only origin/main feature/enemy-pinna` lists ONLY Pinna's files (assets/Sms/Enemy/{AmiNoko,TamaNoko,Cannon}*,
models/Eniemes/{AmiNoko,TamaNoko,Cannon}/, PROJECT_LOG.md, timeToPush!.md) plus assets/Sms/Enemy/Kazekun.tscn -- one line: its scene uid as the
editor normalized it on 2026-10-31 (`z` is outside Godot's uid alphabet a-y / 0-8; the 143 file carried `d143kazekun`), committed here so the
editor stops rewriting it. Write this entry's result paragraph here, commit and push it (so it travels with the squash), and only THEN open the PR:
```
gh pr create --base main --head feature/enemy-pinna --title "Feature: the Pinna Park enemies -- the Electrokoopa, the Snooza Koopa and the Monty Mole cannon (SMS passes 144-146)" --body-file pr144.md
```
If `gh` is still unauthenticated, the compare URL route as in #12. Once merged: `git checkout main && git pull` here, mark this entry DONE, tell
Kiro the sha. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`,
the three gatekeeper / launcher `.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the `addons/godot_ai` churn: his,
leave them.

Result: `feature/enemy-pinna` pushed as a NEW remote branch alongside Ricco, 18 commits over `main` `55514c8` at that point,
8 of them Pinna's. `git rev-list --count origin/feature/enemy-pinna..feature/enemy-pinna` returned `0`. All six shas confirmed
ancestors: `45574ec` (146), `ab46ae3`, `a702b49` (145), `6537026`, `858718c` (144) and the Ricco base `76c46d8`.
Hygiene: 0 of Jaysonn's nine live edits in the range.
NOTE on the stated size: Kiro gives 42 files / +2735 over the Ricco tip, measured figure 43 files / +2785 / -9. Measured at
`a889646` it is exactly 42 / +2735, so the gap is the two commits added after that line was written: `aa61b7c` (the Kazekun
scene uid) and `db9f9bf` (the PR update to this file). Accounted for in full, nothing unexplained.
UNSTACKED after PR #11's squash, by the entry's own recipe. `git merge origin/main` on this branch conflicted in exactly the two
files predicted plus the Kazekun line Kiro flagged separately, and every source file merged clean because both sides carry the
identical Ricco changes. Resolutions: `PROJECT_LOG.md` took this branch's copy, since
`git diff --stat 76c46d8 origin/main -- PROJECT_LOG.md` printed nothing and this side is the superset, verified to hold entries
139 through 146 once each. `assets/Sms/Enemy/Kazekun.tscn` kept the normalized uid `d143ka0ekun` over `main`'s `d143kazekun`,
which is the whole point of `aa61b7c`, and the file's diff against `main` is that one line alone.
`timeToPush!.md` was hand-merged, and ONE part of Kiro's instruction had to be inverted: the rule says take everything through
#12 from `origin/main`, but `main`'s copy of #12's BODY PROSE is the stale pre-approval version, because the squash carried the
Ricco branch's file whose last entry commit `76c46d8` predates Kiro's `db9f9bf`. So #12's DONE heading and result paragraph came
from `main` and #12's body text came from this branch, which is the accurate one. Checked afterwards: no conflict markers, the
DONE heading once, the result paragraph once, the interim "push done, merge pending" version gone, the approved
"APPROVED BY JAYSONN" prose present and the stale "do NOT open a PR until he says the family is done" line absent.
Merge commit `a58ef5f`, pushed. `git diff --name-only origin/main feature/enemy-pinna` then listed ONLY Pinna's files, the two
log files and the single Kazekun line, with no Ricco scene, no `assets/Mario.cs` and no `assets/Sms/Enemy/SmsSmallEnemy.cs`:
43 files / +2784 / -8. Largest new files as stated: `tamanoko_model1.glb` 0.92 MB, `aminoko_model1.glb` 0.81,
`tyorobe_model1.glb` 0.63, `tamaflower_model1.glb` 0.21, nothing over 1 MB.
`gh auth status` still prints "You are not logged into any GitHub hosts", so the browser route again.
PR #12 opened through the browser, the URL 7490 characters with title and body prefilled.
MERGED BY JAYSONN as a **SQUASH**: `main` is now `3732f6a`, one commit with a single parent `0546b25`. Verified by TREE, the way
a squash requires: `git merge-base --is-ancestor 45574ec main` says NO, while `git diff --name-only feature/enemy-pinna main`
is EMPTY, so every file matches the branch including both log files. All three scenes present on the trunk (`AmiNoko.tscn`,
`TamaNoko.tscn`, `Cannon.tscn`), the sunflower scene too, and `assets/Sms/Enemy/Kazekun.tscn` carries the normalized uid
`d143ka0ekun`, so the hand-merge of that one line survived. `PROJECT_LOG.md` on `main` holds entries 139 through 146 once each,
and this file holds both #12's and #13's result paragraphs once each with no conflict residue. `pr144.md` deleted.
THE SHA FOR KIRO: `main` is at `3732f6a` after PR #12's squash, with `e4bbf68` the "#13 done" commit on top of it, then this
one-line correction (a commit cannot state its own sha: the first write of this line guessed one, which was wrong. Read the
current trunk tip from `git log --oneline -1 main` rather than from any sha written inside this file).
Both Ricco and Pinna are on the trunk: eight species in eight passes, 139 through 146. Nothing from either branch is pending.
The two feature branches `feature/enemy-ricco` and `feature/enemy-pinna` are merged and can be deleted whenever Jaysonn wants,
local and remote. Left them in place rather than deleting without being asked.
STILL OPEN from earlier entries, carried forward: `feature/select-stages` (entry #11, SMS 135) is pushed and waiting on
Jaysonn's playtest before it gets a PR entry, and its copy of this file still holds the only text of entry #11, so `main` has
no #11 section until that branch merges. Also the exit-time C# finalize crash first seen in entry #10's batch has not recurred
in these eight passes, and Kiro reports every drive green, so it stays a watch item rather than an open bug.
A NOTE FOR THE NEXT STACKED PAIR: pushing both branches at once worked, and so did putting the lower entry's result on the
upper branch. The one thing to repeat is the inverted resolution above. Whenever a squash lands, `main`'s copy of this file is
whatever the merged branch had, so any queue edit Kiro made after that branch's last commit lives only on the branch that
carries it. Compare the two bodies before trusting "take main's side".

### #15 -- DONE 2026-11-01 -- https://github.com/JasonAbsolute/ProjectShine/pull/13 (SQUASH-merged by Jaysonn) -- queued 2026-10-31 23:40 PDT (Kiro) -- PUSH + PR (SMS 149-150e), PLAYTESTED BY JAYSONN, updated 2026-11-01 04:05 PST
Push the tenth feature branch: `feature/enemy-monte`, branched from `main` `f89159e` (the trunk after PR #12, the Pinna Park family). Pianta Village's
family, one pass each on this branch. (`feature/enemy-pinna2` -- entry #14, passes 147-148 -- is a sibling off the same `main`, still unmerged; whichever
of the two merges second will conflict at the tails of PROJECT_LOG.md and this file: keep both sides, nothing else overlaps.) SMS 149: the Chain
Chomplet (TFireWanwan), the red-hot little Chain Chomp on its chain -- it wanders its graph leaving burning goop, charges and burns Mario when it sees
him, is cooled black by the hose (every drop takes one of its 60 heat points), and then its chain can be GRABBED: Mario's new PULLING status (B held on
the ring at the chain's end, the stick back stretches the five-link rubber chain and it drags him back, B up releases) flings it away by the stretch,
it skids and bounces off walls, sits dizzy, reheats; flung into water it is beaten for good (silver, on its side) -- assets/Sms/Enemy/FireWanwan.tscn
+ SmsFireWanwan.cs, the chain, the eye shader and four materials under assets/Sms/Enemy/FireWanwan/, the two models under models/Eniemes/FireWanwan/;
Mario's pull plumbing assets/Sms/SmsPullHandle.cs + assets/Sms/MarioPull.cs and three anchored lines in assets/Mario.cs (the `pulling` state, its
dispatch, the hitNoKeepPull entry). PLAYTESTED BY JAYSONN (149 and 150, through the 150b-e polish -- he signed off 2026-11-01): push the branch AND open its PR; the family is done. ONE
merge path only -- this is a PR, so do NOT also merge it into local `main`. SMS 150: the Chain Chomp (TBossWanwan), Pianta Village's house-sized boss on a 15-link leash -- staked to the ground, it rampages at the end of its chain and burns you (its body and its hot chain); the hose cools its 255 heat points from red through to a cold blue; cooled, you pull its stake out (B) and drag it along its rail to the hot spring, where it leaps in, turns gold and drops the Shine (grab the stake while it is HOT and it drags YOU instead; ground-pound the stake to re-plant it) -- assets/Sms/Enemy/BossWanwan.tscn + SmsBossWanwan.cs, the leash / two shaders / eight materials under assets/Sms/Enemy/BossWanwan/, the three models under models/Eniemes/BossWanwan/; it reuses pass 149's PULLING (SmsPullHandle / MarioPull.cs, now with the stick accessors the boss's drag reads) and adds StartMode to assets/Sms/SmsCameraShake.cs. SMS 150b polishes both Chomps: their reignition now shows its fire building (the Chomplet's ring flame grows through the reheat; the Chain Chomp's body and chain glow back from cold across the bark) and the Chain Chomp walks upright instead of tumbling (RollSw off). SMS 150c then takes the Chain Chomp's stomp dust and impact streaks down to the ground (they were sitting too high), gives its hot mouth back its sparks (HitBurst), and fixes its hose steam, which was being dropped before it could show. SMS 150d then reworks the Chain Chomp's effects to Jaysonn's reference (Ref/TeethSparkRef): its mouth throws scattered yellow -> orange ember sparks (a new Fx/BwHibana), the hose steam rises low around the whole body instead of from the middle, and the stomp's blue impact streaks sit low again. SMS 150e then makes those mouth sparks jet directionally out of the teeth as longer, short-lived streaks (BwHibana is now a forward align_y streak jet, spawned facing the mouth). SMS 153: KING BOO (TBossTelesa), Casino Delfino's boss over his roulette table -- the slot machine, the fruit and pepper rounds, the spicy hits that take his three hit points, the three spinning discs with their ground-pound switches, the Boo attackers, the Shine -- assets/Sms/Enemy/BossTelesa.tscn + SmsBossTelesa.cs, the slot / discs / arena / materials under assets/Sms/Enemy/BossTelesa/, the models under models/Eniemes/BossTelesa/; SmsTelesa.cs gets the Boo's attacker half (and a goal-distance fix), Mario.cs a two-line SmsHP getter. SMS 153b: King Boo after the playtest -- the bmd's real materials (a WHITE see-through Boo with blue eyes, the blink loop and the rolled eyes), every ms_btls effect (the twinkle, the glow, the drool, the fruit-colour puffs, the confetti, the spicy fire and sweat), the slot finishing before he spits (ResultPauseTicks), HoverHeight (11.42 m; the game's 14.8), the hint balloons, the rumble, the real shake tables, BossMusic, the IntroRide knob (the FallDemo). SMS 153c: the second playtest -- King Boo SOLID by default with GhostOpacity a live slider (0.588 = the game's ghost), his and the small Boos' shaders writing depth (the mouth drawn over the head is gone), the slot machine sinking into the table and rising back under him, drool strands off his lip's corners (the Piranha's SmsGoopStrand, with FollowAnchor / AnchorOffsetUnits), the gold ring pressed into a bowl (rulet00.bck's end pose), Mario's right glove / shoe / left eye materials opaque in Player.tscn. SMS 153d: the pressed gold ring SHOWS (153c had reversed the bowl's winding and it was culled -- a hole), and the drool strands face the camera (SmsGoopStrand.FaceCamera; the Piranha's two crossed strips read as an X from the table). SMS 153e: a DroolOn toggle, King Boo floating just over a lower slot machine (SlotUnderUnits 960, HoverHeight 13 m), and the inside of his crown drawn (the bmd's _mat_crown is two-sided: the shader split into an include with a cull_back and a cull_disabled twin). SMS 153f: a fruit in Mario's hands that goes with the swallowed fan now empties his hands (Mario.SmsHeldGone) -- no more carrying a freed body until something hits him. SMS 153g: a damage FLASH (the whole body blinks the fruit's colour, three times in half a second), the spit waiting for the slot machine to sink out of sight, and his spicy fire rebuilt from the disc's own ms_btls_spicy_a..d (BtlsSpicyFire.tscn; the Stu's fire x 2.5 stood in before). SMS 153h: the damage flash three times longer (90 ticks), the slot machine riding his rise out of the table (no more trailing and popping), and a jump popping his Boo attackers (AttackersJumpable; SmsTelesa.JumpPopsUnsprayed, off for the hotel's Boos). SMS 153i: a jump pops EVERY Boo by default (JumpPopsUnsprayed on for the species), and only a real stomp from above does it -- 153h's cylinder test let King Boo's attackers pop as they rammed a descending Mario. SMS 153j: the slot machine bolted to his clips (it dives into the table with him) and him riding it down at the spit; the damage flash 2.5 s at 3 Hz; his Boo attackers out of his mouth toward Mario, flying over the table's surface (not 143 u under it), homing in a curve. SMS 153k: King Boo's ROOM -- the casino boss room's wall and ceiling from the game's own files round the arena (the 33-m drum the outer disc's rim pokes into, its starry purple paint with the three drifting coloured spots, the marble-ring ceiling): KingBooWall.tscn, the arena's new BossWall child. SMS 153l: King Boo's INTRO (Mario taken onto the centre disc 27 m up and lowered for 16.7 s under the game's intro camera, the slot's unseen fall, the discs' start) and the room's FIXED camera (the game's camera cube: the eye at the far wall, turning to follow Mario); SunshineCamera learns the bck demo camera and the fixed-point kind, SmsCameraArea an Eye marker, Mario.cs SmsCarryTo. Further passes re-point this entry's tip.
- Branch `feature/enemy-monte` -- code tip `73be638` ("SMS 150e: the Chain Chomp's mouth sparks jet out as directional streaks", 3 files --
  SmsBossWanwan.cs, Fx/BwHibana.tscn, BossWanwan.tscn) on the 150b-d polish chain (`a9984c3` 150d ember/steam/stomp, `75fd1eb` 150c, `3cbcc37`
  150b) on `7876f9a` ("SMS 150: the Chain Chomp ...", 48 files +2230) on `bf3de7c` ("SMS 149: the Chain Chomplet ...", 32 files +1791) on
  `main` `f89159e`; the branch tip is this file's own commit on top of them; 12 commits since `main`. The range carries none of Jaysonn's nine live edits.
- To try 149: drop assets/Sms/Enemy/FireWanwan.tscn on flat ground with room (about 3.8 m tall at the game's 2x scale; +Z is its front): it wanders,
  stamps burning goop as it goes, and when it sees you within ~17 m it turns and charges (its touch is the fire knock, -1 HP; it gives up past 27 m).
  Spray it: every drop takes one heat point of 60 (it reheats 1 every 10 ticks while hot); at 0 it turns black and runs off, and NOW you can grab the
  big ring at the end of its chain -- stand within 2 m of the ring and hold B (a new move: he takes hold without lifting), then walk BACKWARD: the stick
  pulls against the chain and the chain drags you back; let go of B and the Chomplet is flung AWAY from you by the stretch (a full stretch sends it
  ~70 m on flat ground), skids, bounces off walls, sits dizzy a while and heats up red again. Fling it into water (a pool / water area it lands in) and
  it is beaten for good: silver, lying on its side. Pressing A while pulling lets go and jumps (the game hops with the chain kept -- not ported).
  Knobs: Deviations (SpeedScale), Effects (the ring's fire, the slip smoke, the wall puffs, the launch dust, the dizzy stars, the steam, the splash;
  five slots), Sounds (eleven slots); RelaunchTicks -1 = gone for good.
- To try 150: drop assets/Sms/Enemy/BossWanwan.tscn on flat open ground facing the way it should start (+Z is its mouth; it is ~13 m across).
  Its stake plants 13 m behind it (or at a PicketPoint node you assign) and it rampages at the end of its chain: it slides and hops along its
  rail (a Graph node's children IN ORDER are its path; a child with metadata sms_jump = true makes it hop; no Graph = it roams within
  WanderRadiusUnits), slams the ground with every bob (the camera shakes within ~40 m), and burns you on touch -- its body and the first eight
  links of its chain while it is red-hot. Spray it: 255 heat points, 1 a drop (it heals 1 every 20 ticks while hot); it fades red -> dark -> a
  cold blue, and at 0 it sits dozing (the cold wait clip) ~20 s before it barks and reheats. COOLED, go to the stake and hold B: you pull it out
  and the Chomp is loose -- walk backward and the chain drags it along its rail toward you. Drag it back past the rail node nearest the BathPoint
  you assign (the hot spring; a loose Chomp over water with no BathPoint counts) and it LEAPS in, turns gold over ~10 s, and the Shine appears
  (ShineScene, e.g. assets/ShineSprite.tscn; ShineSpawnPoint or 13 m over it). Grab the stake while it is HOT and it drags YOU around instead;
  ground-pound the stake to lock it again (it shakes free after ~5 s while hot). Knobs: the level's points (BathPoint, PicketPoint, ShineScene,
  ShineSpawnPoint), Deviations (SpeedScale, RollSw, JumpSpeedY, ColdTicks, ShakeTicks), Effects (the stomp dust, steam, splash; the rocks /
  sparks / heat haze / sparkles slots), Camera (the two landing shakes), Sounds (ten slots). RelaunchTicks -1 = gone for good.
- PR: title "Feature: Pianta Village's Chain Chomps (SMS passes 149-150)"; base `main`; head `feature/enemy-monte`; body:
  > Pianta Village's two Chain Chomps on `feature/enemy-monte`.
  > **SMS 149 -- the Chain Chomplet** (TFireWanwan): the red-hot little Chain Chomp on its five-link rubber chain -- it wanders its graph leaving
  > burning goop, charges and burns Mario on sight, is cooled black by the hose (one of 60 heat points a drop); cooled, its chain-ring is GRABBED
  > (Mario's new PULLING -- hold B, lean the stick back to stretch the chain, release to fling it), it skids off walls, sits dizzy, reheats; flung
  > into water it is beaten (silver, on its side). New SmsFireWanwan.cs + FireWanwan.tscn, the chain / eye shader / four materials, two models; plus
  > Mario's pull plumbing (SmsPullHandle.cs, MarioPull.cs, three anchored lines in Mario.cs).
  > **SMS 150 -- the Chain Chomp** (TBossWanwan): Pianta Village's house-sized boss on a 15-link leash -- staked down it rampages at the end of its
  > chain and burns you (its body + its hot chain); the hose cools its 255 heat from red through to a cold blue; cooled, pull its stake out (B) and
  > drag it by the chain along its rail to the hot spring, where it leaps in, turns gold and drops the Shine (grab the stake HOT and it drags YOU;
  > ground-pound to re-stake). New SmsBossWanwan.cs + BossWanwan.tscn, the 15-node leash + picket, two shaders, eight materials, three models; reuses
  > 149's PULLING (+ the MarioPull stick accessors) and adds StartMode to SmsCameraShake.cs.
  > **Polish after Jaysonn's playtest (150b-e):** the reignition shows its fire building (the Chomplet's ring flame through the reheat, the Chomp's
  > body and chain glowing back from cold across the bark); the Chomp walks upright instead of tumbling; its stomp dust and impact streaks hug the
  > ground; its mouth jets a hibana of directional spark streaks (new Fx/BwHibana.tscn); its hose steam covers the whole body.
  > Drive 150 22/22 headless, boot ALL OK; 149 green in its own pass. Playtested by Jaysonn. The range carries none of his nine live editor edits.
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/enemy-monte
```
git push -u origin feature/enemy-monte
git rev-list --count origin/feature/enemy-monte..feature/enemy-monte
```
Expect `0` from the rev-list. Then open the PR with the title and body above (base `main`, head `feature/enemy-monte`) -- `gh pr create` if gh is
authed, else the prefilled compare URL. ONE merge only: this is a PR, so do NOT also `git merge` it into local `main`. The working tree still carries
Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of
`assets/Sms/SmsHiddenCoin.cs`, the three gatekeeper / launcher `.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the
`addons/godot_ai` churn: his, leave them.

Result: `feature/enemy-monte` pushed as a NEW remote branch, 13 commits over `main` `f89159e`, 81 files / +4503 / -0.
`git rev-list --count origin/feature/enemy-monte..feature/enemy-monte` returned `0`. All six code shas confirmed ancestors:
`73be638` (150e), `a9984c3` (150d), `75fd1eb` (150c), `3cbcc37` (150b), `7876f9a` (150), `bf3de7c` (149). Per-pass diffstats
match the entry exactly: 149 = 32 files / +1791, 150 = 48 files / +2230. `assets/Mario.cs` takes +13 and nothing else of his
movement, and `assets/Sms/SmsCameraShake.cs` +15 for StartMode. Largest new files: `bwanwan_body.glb` 0.28 MB,
`wanwan.glb` 0.18, then the two scripts at 65 KB and 47 KB, nothing near 1 MB. Hygiene: 0 of Jaysonn's nine live edits.
NOTE on the commit count: the entry says 12, measured 13. The extra one is `63cfd85`, the commit that flipped this entry from
push-only to push + PR, which cannot count itself. The same off-by-one as every entry so far, and always the queue file's own
commit.
SIBLING CHECK, since #14 is unmerged off the same `main`: `comm -12` over the two branches' file lists confirms Kiro's warning
precisely. The ONLY files both `feature/enemy-monte` and `feature/enemy-pinna2` touch are `PROJECT_LOG.md` and
`timeToPush!.md`. No scene, script, shader, material or model is shared, so whichever merges second conflicts at those two
tails alone and the resolution is to keep both sides.
PR body: the entry carries it inline as a blockquote rather than as a file, so it was extracted to `pr149.md` at the project
root (untracked, delete after merge) with Kiro's wording unchanged. One formatting fix was needed: his hard-wrapped blockquote
would have rendered the four sections as one run-on paragraph on GitHub, so blank lines were inserted between them. The words
are his.
`gh auth status` still prints "You are not logged into any GitHub hosts", the seventh entry running, so the browser route. The
compare URL prefilled with title and body measured well under the ~8000 ceiling #8 established.
PR #13 opened through the browser, the URL 3073 characters with title and body prefilled.
ONE merge path, as the entry insists: this branch gets a PR and is NOT also merged into local `main`.
MERGED BY JAYSONN as a **SQUASH**: `main` is now `de61353`, one commit with a single parent `f89159e`. Fast-forwarded with
`git merge --ff-only origin/main`, not `git pull`, which this repo's `pull.rebase` setting refuses while he has unstaged work.
Verified by TREE, the way a squash requires: `git merge-base --is-ancestor 7876f9a main` says NO, while
`git diff --name-only feature/enemy-monte main` is EMPTY, so every one of the 81 files matches the branch, both log files
included. Present on the trunk and checked individually: `FireWanwan.tscn`, `BossWanwan.tscn`, `assets/Sms/SmsPullHandle.cs`,
`assets/Sms/MarioPull.cs` and `assets/Sms/Fx/BwHibana.tscn` (150e's spark jet). `assets/Mario.cs` carries the pulling state.
`PROJECT_LOG.md` holds all six entries 149, 150, 150b, 150c, 150d and 150e. `pr149.md` deleted.
THE TRUNK TIP: read it from `git log --oneline -1 main`, not from a sha written here. This entry's closing commit cannot state
its own sha, which has now caught three entries, so the rule is written into the procedure rather than re-learned each time.
STILL OPEN, carried forward: `feature/enemy-pinna2` (entry #14, SMS 147 and 148, the Bullet Bill and the ground Electrokoopa)
is pushed and waiting on Jaysonn's playtest, and it is now the branch that merges SECOND, so it will conflict at the tails of
`PROJECT_LOG.md` and `timeToPush!.md` against this entry's sections. Keep both sides: nothing else overlaps, measured.
`feature/select-stages` (entry #11, SMS 135) is likewise pushed and waiting on a playtest, and holds the only copy of #11's
text. The exit-time C# finalize crash from #10's batch has still not recurred.

### #16 -- PENDING -- queued 2026-11-01 10:38 PST (Kiro) -- PUSH ONLY, no PR yet (SMS 151-153 + 153b..l), updated 2026-11-02 19:58 PST
Push the eleventh feature branch: `feature/enemy-sirena`, branched from `main` `c082a22` (the trunk after PR #13, the Chain Chomps). Sirena Beach's
family -- Hotel Delfino's ghosts -- one pass each on this branch. SMS 151: the Boo (TTelesa), the hotel's ghost -- it drifts between its graph's nodes
a little off the floor, bobbing and tilting, fading in and out (the LOOP kind), and hurts you on touch; your jump passes straight through it until you
HOSE it: a spray knocks it back, shows it and stuns it, sunk to the floor and solid, and NOW a jump pops it (the down clip, coins). The PINK box Boo
(Kind = Box) is always visible and a spray turns it into a pink Boo BLOCK you stand on for 25 s before the Boo comes back out; a jump on a box Boo only
makes it flinch -- assets/Sms/Enemy/Telesa.tscn + SmsTelesa.cs, the block, the shader and six materials under assets/Sms/Enemy/Telesa/, the two
models under models/Eniemes/Telesa/. NOT yet playtested by Jaysonn: push the branch so it is on GitHub; do NOT open a PR until he says the family is
done (Kiro will replace this entry with the PR entry then). SMS 152: the Sleepy Boo (TDebuTelesa), the hotel's huge Boo asleep across a doorway -- it snores, hurts and shoves you on touch, and nothing of Mario's moves it; in the game only Yoshi eats it, so here (BeatenBy, a knob, on by default) the hose wakes it and a ground pound on the woken one beats it -- assets/Sms/Enemy/DebuTelesa.tscn + SmsDebuTelesa.cs, two materials under Telesa/, the model under models/Eniemes/Telesa/. Further passes re-point this entry's tip.
- Branch `feature/enemy-sirena` -- code tip `6570dca` ("SMS 153l: King Boo's intro and camera ...", SunshineCamera.cs, SmsCameraKinds.cs,
  SmsCameraArea.cs, SmsCameraPath.cs, Mario.cs (SmsCarryTo), SmsBossTelesa.cs, BossTelesa.tscn, BtelesaRollCamera.tres, KingBooArena.tscn) on
  `cc59542` ("SMS 153k: King Boo's room ...") on `main` `c082a22`; the branch tip is this file's own commit on top of them; 28 commits since
  `main`. The range carries none of Jaysonn's nine live edits (nor his editor re-saves of the BossTelesa .tres); his re-save of the arena
  (the uids, GhostOpacity 0.45) went in with 153k's BossWall child of KingBooArena.tscn).
- To try 151: drop assets/Sms/Enemy/Telesa.tscn in a room where it should FLOAT (it keeps the floor under the placed point as its base and bobs
  0.7-2.3 m over it; +Z is its face) with a Graph node of Node3D children to drift between, or it roams within WanderRadiusUnits. Kind: Loop (the
  hotel's: fades in ~3 s, out, hidden ~5 s, again), Box (the PINK one, always visible), Normal (invisible until sprayed), CanSee (always visible).
  It hurts you on touch unless you are above it, and your jump passes through it -- hose it: it is knocked back, shown and stunned (it sinks to the
  floor, solid); jump on it or ground-pound it NOW and it pops. The stun ends by itself and it drifts off. A BOX Boo sprayed turns into a pink block
  you can stand on for 25 s (it blinks before the Boo comes back). Knobs: Kind, Deviations (SpeedScale, BaseOffsetUnits), Effects (the change puff,
  the drool slot, the block scene), Sounds (eight slots); RelaunchTicks -1 = gone for good.
- To try 152: drop assets/Sms/Enemy/DebuTelesa.tscn in a doorway or corridor (it is ~4 m across and never moves; +Z is its face). It
  snores with its Zs; walk into it and it hurts you and shoves you back out; your jump, ground pound and punch do nothing to it. Hose it and it
  wakes for AwakeSeconds (8 s: the Zs stop, it stirs) -- ground-pound it THEN and it is beaten (it shrinks away; coins if a DropTable is set).
  BeatenBy = YoshiOnly makes it the game's unbeatable wall (only Yoshi eats it, and Yoshi's eating is not in the port). Knobs: Deviations
  (BeatenBy, AwakeSeconds), Effects (the Zs, the drool slot), Sounds (two slots); RelaunchTicks -1 = gone for good.
- To try 153: drop assets/Sms/Enemy/BossTelesa/KingBooArena.tscn into the level where the table should be (the three discs sit at one height;
  their walking faces are 1.1 to 1.9 m BELOW the node, the outer ring 34 m across -- put the node ~2 m over the room's floor, or let the table stand
  in a pit). King Boo hangs 11 m up over the centre, hidden for HideWaitTicks (400 = 3.3 s) first, then laughs with his slot machine under him and
  spits a Boo every few seconds. HOSE HIS TONGUE: the slot rolls -- three FRUIT and he spits a fan of fruit and two RED PEPPERS over the table.
  Throw a pepper into his mouth (his tongue hangs low) and he turns spicy and solid; THEN a fruit on his body takes a hit point (he turns the
  fruit's colour, flees, the discs spin faster). A fruit on him while not spicy only makes him flinch, lick and laugh. Three hits and he is down:
  the Shine. Stand on the gold dome in the centre and he vanishes (and will not come back until you step off). Ground-pound the glowing pad (one red pad
  on each disc turns violet while the disc spins) to stop that disc. Knobs: The fight (HitPoints, HideWaitTicks, RoundTimeScale, the slot rates, ForceResult -- Coin / Enemy /
  Fruit / Jackpot for the custom fight), What he spits (the fruit / coin / enemy scenes, the Boo), Deviations (speed scales, the attackers, the
  hide), Effects, Camera, Sounds. SmsRoulette: SwitchRadiusUnits, the pad colours, InitialRollSpeed (a disc on its own).
- To try 153b: he is a white see-through Boo now (GhostOpacity 0.588 = the game; 1 = solid), 11.4 m up (HoverHeight), twinkling and glowing with
  drool off his tongue; the slot stops, the fanfare and its confetti, THEN the machine sinks and he spits (ResultPauseTicks); a plain fruit paints him
  in its colour for a moment (the brk sweep), a pepper rolls his eyes and lights his tongue; the hints show on the HUD balloon (ShowHints, the three
  lines are knobs); IntroRide (Deviations) is the game's intro -- Mario on the raised centre disc riding it down, the slot falling into place.
- To try 153c: he is SOLID now; GhostOpacity (Looks) is the slider and moves live in the inspector while he plays (0.588 = the game's ghost).
  The slot machine sinks into the table when he hides and rises back with him sitting on it (SlotSinkSpeed / SlotRiseSpeed). The gold ring is
  a bowl (SmsRoulette.PondPressed). Drool strands hang off his lip's corners and his tongue tip and ride with him (DroolFollow, DroolLipUnits,
  DroolSpreadUnits, DroolLength, DroolWidth). The small Boos' mouths no longer draw through their heads; Mario's right glove, right shoe and
  left eye are opaque (Player.tscn -- a model fix, not a knob).
- To try 153d: the gold ring is a visible golden bowl in the centre now; the drool strands read as round drops from the table (DroolFaceCamera,
  Looks -- off = the Piranha's crossed strips).
- To try 153e: DroolOn (Looks) turns the drool off and on, live; SlotUnderUnits (The arena, 960; the game's 700) is how far under his centre the
  slot machine hangs and HoverHeight (13 m) how high he floats -- together they set his seat over the machine; look down into his crown: its
  inside is drawn.
- To try 153f: hold a fruit of the fan when he swallows it (hit him with another, or walk under him): your hands empty at once, no stuck carry.
- To try 153g: hit him -- his whole body blinks the fruit's colour three times (DamageFlashTicks / Period / FruitColor / Color, Looks); after the
  fanfare the machine sinks ALL the way under the table before he spits; a pepper lights his tongue with the game's own fire (SpicyFireScale).
- To try 153h: hit him -- the blink runs a second and a half now (DamageFlashTicks); after a hide the slot machine comes up out of the bowl WITH him;
  jump on a Boo he spits and it pops (AttackersJumpable on him; JumpPopsUnsprayed on a placed Boo for the same in the hotel).
- To try 153i: jump on any Boo you placed -- it pops (JumpPopsUnsprayed, Deviations; off = hose first); King Boo's Boos pop only when you land ON
  them, not when they fly into you on the way down.
- To try 153j: walk under him while he waits -- the machine dives into the table with him; after the fanfare he sinks with the machine until it drops
  away, then spits; a hit blinks him 2.5 s at 3 Hz (DamageFlashTicks / Period); his Boos come out of his mouth at you, fly over the pads and curve
  after you (AttackersSpawnForwardUnits, AttackersFirstPointTowardMario, AttackersSmooth; off = the game's).
- To try 153k: the arena has its ROOM now -- the game's wall round the table (you cannot walk or fall off the outer disc) and the ceiling
  47 m up, built when the scene opens (the editor shows it too). Knobs on the arena's BossWall child: CeilingOn (off = open to the sky),
  RoomCastsShadows (on = a dark indoor room), Brightness / SpotStrength / SpotSpeed (1 = the game's dark purple with its drifting lights),
  WallRadiusUnits and the heights; TableLiftUnits is where the room sits under the node (500 u: the game's). KingBooWall.tscn on its own
  drops onto the same point as a table.
- To try 153l: walk into the arena's room (WallOn off on BossWall to walk in, or drop in from above) -- you are lifted onto the centre disc 27 m
  up and ride it down for 17 s while the game's intro camera sweeps from under the table up to the far wall; then the discs start and King Boo's
  wait begins. Through the fight the camera stands FIXED at the far wall 8 m up and turns to follow you (no C-stick, no L camera: the game's).
  Knobs: IntroRide / IntroStart (WhenMarioEntersRoom, WhenMarioLandsOnDisc, AtOnce) / IntroLiftUnits / IntroFallSpeed / IntroCamera / IntroSlotShown
  and CameraChase on King Boo; the FightCamera child's Eye marker is where the camera stands (move it, or delete the child for the normal camera).
  Your spawn is inside the room's trigger: the intro runs every time the level starts -- IntroRide off on your arena's BossTelesa for quick tests.
```
git push -u origin feature/enemy-sirena
git rev-list --count origin/feature/enemy-sirena..feature/enemy-sirena
```
Expect `0` from the rev-list. No PR, no merge. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of
`assets/Sms/SmsHiddenCoin.cs`, the three gatekeeper / launcher `.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the
`addons/godot_ai` churn: his, leave them.
