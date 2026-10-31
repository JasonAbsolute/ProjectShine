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

### #10 -- PENDING -- queued 2026-10-30 20:40 PDT, updated 2026-10-31 (Kiro) -- PUSH main ONLY, no PR
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
