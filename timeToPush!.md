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

- **A STACKED branch gets NO PR of its own.** If branch B was cut from branch A's tip, then A's commits are in B's history and B's PR
  carries A's code. Open ONE PR, B's. Merging B first is the clean order, because GitHub's squash of B lands A and B together as one
  commit and `main` is complete in a single step. A's own PR, opened afterwards, will then report conflicts in every file BOTH branches
  touched, and those are NOT conflicts to resolve: `main` holds the newer version of each (B's passes changed them after A's tip), so
  resolving toward A reverts them. **Close A's PR without merging.** This happened on 2026-11-17 with `feature/sounds` (A) and
  `feature/kingboo-rematch` (B): PR #15 (B) squash-merged the whole stack, PR #16 (A) was opened after it and had to be closed. The
  check that settles it: `git diff --name-only <B> origin/main` empty means the squash took everything, so A has nothing left to give.

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
merge path only -- this is a PR, so do NOT also merge it into local `main`. SMS 150: the Chain Chomp (TBossWanwan), Pianta Village's house-sized boss on a 15-link leash -- staked to the ground, it rampages at the end of its chain and burns you (its body and its hot chain); the hose cools its 255 heat points from red through to a cold blue; cooled, you pull its stake out (B) and drag it along its rail to the hot spring, where it leaps in, turns gold and drops the Shine (grab the stake while it is HOT and it drags YOU instead; ground-pound the stake to re-plant it) -- assets/Sms/Enemy/BossWanwan.tscn + SmsBossWanwan.cs, the leash / two shaders / eight materials under assets/Sms/Enemy/BossWanwan/, the three models under models/Eniemes/BossWanwan/; it reuses pass 149's PULLING (SmsPullHandle / MarioPull.cs, now with the stick accessors the boss's drag reads) and adds StartMode to assets/Sms/SmsCameraShake.cs. SMS 150b polishes both Chomps: their reignition now shows its fire building (the Chomplet's ring flame grows through the reheat; the Chain Chomp's body and chain glow back from cold across the bark) and the Chain Chomp walks upright instead of tumbling (RollSw off). SMS 150c then takes the Chain Chomp's stomp dust and impact streaks down to the ground (they were sitting too high), gives its hot mouth back its sparks (HitBurst), and fixes its hose steam, which was being dropped before it could show. SMS 150d then reworks the Chain Chomp's effects to Jaysonn's reference (Ref/TeethSparkRef): its mouth throws scattered yellow -> orange ember sparks (a new Fx/BwHibana), the hose steam rises low around the whole body instead of from the middle, and the stomp's blue impact streaks sit low again. SMS 150e then makes those mouth sparks jet directionally out of the teeth as longer, short-lived streaks (BwHibana is now a forward align_y streak jet, spawned facing the mouth). SMS 153: KING BOO (TBossTelesa), Casino Delfino's boss over his roulette table -- the slot machine, the fruit and pepper rounds, the spicy hits that take his three hit points, the three spinning discs with their ground-pound switches, the Boo attackers, the Shine -- assets/Sms/Enemy/BossTelesa.tscn + SmsBossTelesa.cs, the slot / discs / arena / materials under assets/Sms/Enemy/BossTelesa/, the models under models/Eniemes/BossTelesa/; SmsTelesa.cs gets the Boo's attacker half (and a goal-distance fix), Mario.cs a two-line SmsHP getter. SMS 153b: King Boo after the playtest -- the bmd's real materials (a WHITE see-through Boo with blue eyes, the blink loop and the rolled eyes), every ms_btls effect (the twinkle, the glow, the drool, the fruit-colour puffs, the confetti, the spicy fire and sweat), the slot finishing before he spits (ResultPauseTicks), HoverHeight (11.42 m; the game's 14.8), the hint balloons, the rumble, the real shake tables, BossMusic, the IntroRide knob (the FallDemo). SMS 153c: the second playtest -- King Boo SOLID by default with GhostOpacity a live slider (0.588 = the game's ghost), his and the small Boos' shaders writing depth (the mouth drawn over the head is gone), the slot machine sinking into the table and rising back under him, drool strands off his lip's corners (the Piranha's SmsGoopStrand, with FollowAnchor / AnchorOffsetUnits), the gold ring pressed into a bowl (rulet00.bck's end pose), Mario's right glove / shoe / left eye materials opaque in Player.tscn. SMS 153d: the pressed gold ring SHOWS (153c had reversed the bowl's winding and it was culled -- a hole), and the drool strands face the camera (SmsGoopStrand.FaceCamera; the Piranha's two crossed strips read as an X from the table). SMS 153e: a DroolOn toggle, King Boo floating just over a lower slot machine (SlotUnderUnits 960, HoverHeight 13 m), and the inside of his crown drawn (the bmd's _mat_crown is two-sided: the shader split into an include with a cull_back and a cull_disabled twin). SMS 153f: a fruit in Mario's hands that goes with the swallowed fan now empties his hands (Mario.SmsHeldGone) -- no more carrying a freed body until something hits him. SMS 153g: a damage FLASH (the whole body blinks the fruit's colour, three times in half a second), the spit waiting for the slot machine to sink out of sight, and his spicy fire rebuilt from the disc's own ms_btls_spicy_a..d (BtlsSpicyFire.tscn; the Stu's fire x 2.5 stood in before). SMS 153h: the damage flash three times longer (90 ticks), the slot machine riding his rise out of the table (no more trailing and popping), and a jump popping his Boo attackers (AttackersJumpable; SmsTelesa.JumpPopsUnsprayed, off for the hotel's Boos). SMS 153i: a jump pops EVERY Boo by default (JumpPopsUnsprayed on for the species), and only a real stomp from above does it -- 153h's cylinder test let King Boo's attackers pop as they rammed a descending Mario. SMS 153j: the slot machine bolted to his clips (it dives into the table with him) and him riding it down at the spit; the damage flash 2.5 s at 3 Hz; his Boo attackers out of his mouth toward Mario, flying over the table's surface (not 143 u under it), homing in a curve. SMS 153k: King Boo's ROOM -- the casino boss room's wall and ceiling from the game's own files round the arena (the 33-m drum the outer disc's rim pokes into, its starry purple paint with the three drifting coloured spots, the marble-ring ceiling): KingBooWall.tscn, the arena's new BossWall child. SMS 153l: King Boo's INTRO (Mario taken onto the centre disc 27 m up and lowered for 16.7 s under the game's intro camera, the slot's unseen fall, the discs' start) and the room's FIXED camera (the game's camera cube: the eye at the far wall, turning to follow Mario); SunshineCamera learns the bck demo camera and the fixed-point kind, SmsCameraArea an Eye marker, Mario.cs SmsCarryTo. SMS 153m: King Boo faces the camera (the game's rotation 180; every enemy's spawn yaw read off its node's axis), Mario in control from the disc's landing, the intro camera where the game's bck puts it (12 m over the table, not under it). SMS 153n: the intro -- Mario rides the centre disc's switch pad (the game's taking matrix), the gold dome stands through the ride and sinks into its bowl as the fight begins (rulet00.bck as the game plays it), IntroSpeed paces the whole intro (1.5 = 11 s; 1 = the game's 16.7 s). Further passes re-point this entry's tip.
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

### #16 -- DONE 2026-11-03 -- https://github.com/JasonAbsolute/ProjectShine/pull/14 (SQUASH-merged by Jaysonn) -- queued 2026-11-01 10:38 PST (Kiro) -- PUSH + PR (SMS 151-153 + 153b..n), PLAYTESTED BY JAYSONN 2026-11-03, updated 2026-11-03 00:16 PST
Push the eleventh feature branch: `feature/enemy-sirena`, branched from `main` `c082a22` (the trunk after PR #13, the Chain Chomps). Sirena Beach's
family -- Hotel Delfino's ghosts -- one pass each on this branch. SMS 151: the Boo (TTelesa), the hotel's ghost -- it drifts between its graph's nodes
a little off the floor, bobbing and tilting, fading in and out (the LOOP kind), and hurts you on touch; your jump passes straight through it until you
HOSE it: a spray knocks it back, shows it and stuns it, sunk to the floor and solid, and NOW a jump pops it (the down clip, coins). The PINK box Boo
(Kind = Box) is always visible and a spray turns it into a pink Boo BLOCK you stand on for 25 s before the Boo comes back out; a jump on a box Boo only
makes it flinch -- assets/Sms/Enemy/Telesa.tscn + SmsTelesa.cs, the block, the shader and six materials under assets/Sms/Enemy/Telesa/, the two
models under models/Eniemes/Telesa/. PLAYTESTED BY JAYSONN 2026-11-03 ("nice played thru the fight. lets pr it"): push the branch AND open its PR -- the
family is done (the sounds come as their own pass on a later branch). ONE merge path only: this is a PR, so do NOT also merge it into
local `main` (Kiro has not). It can be merged once it is open (squash or merge commit, his call; `main` then needs a `git pull` here). SMS 152: the Sleepy Boo (TDebuTelesa), the hotel's huge Boo asleep across a doorway -- it snores, hurts and shoves you on touch, and nothing of Mario's moves it; in the game only Yoshi eats it, so here (BeatenBy, a knob, on by default) the hose wakes it and a ground pound on the woken one beats it -- assets/Sms/Enemy/DebuTelesa.tscn + SmsDebuTelesa.cs, two materials under Telesa/, the model under models/Eniemes/Telesa/. Further passes re-point this entry's tip.
- Branch `feature/enemy-sirena` -- code tip `28674e1` ("SMS 153n: King Boo's intro -- Mario rides the centre disc's switch pad ...",
  SmsRoulette.cs, SmsBossTelesa.cs, SunshineCamera.cs, Mario.cs) on `877791a` ("SMS 153m: King Boo faces the camera ...") on `main` `c082a22`;
  the branch tip is this file's own commit on top of them; 32 commits since `main`. The range carries none of Jaysonn's nine live edits (nor
  his editor re-saves of the BossTelesa .tres); his re-save of the arena (the uids, GhostOpacity 0.45) went in with 153k's BossWall child of
  KingBooArena.tscn).
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
- To try 153m: King Boo faces you and the camera now (his fan and his Boos come at you); you can move the moment the disc lands and the
  camera cuts to the fight's spot (the slot machine still drops into place unseen for 4 s); the intro camera opens 12 m over the table
  looking up at your pedestal and circles up to the far wall.
- To try 153n: walk into the room -- you are set on the centre disc's purple switch pad, facing the gold dome standing up in the middle, and
  ride down for 11 s (IntroSpeed 1.5 on King Boo: 1 = the game's 17 s, 2 = 8 s -- the fall, the camera sweep and the slot's drop all follow);
  the moment the disc lands the dome sinks into its bowl (PondPressTicks on Roulette00: the second it takes; IntroPondPress off on King Boo
  = the bowl from the start) and you are in control.
- PR: title "Feature: Sirena Beach's ghosts -- the Boo, the Sleepy Boo and King Boo's casino fight (SMS passes 151-153n)"; base `main`;
  head `feature/enemy-sirena`; body (extract it to `pr151.md` at the project root for `--body-file`, untracked, delete it after):
  > Sirena Beach's enemy family, one pass per species on `feature/enemy-sirena` off `main` `c082a22`, each pass read off the decompiled game
  > (TTelesa / TDebuTelesa / TBossTelesa) and logged in `PROJECT_LOG.md` (entries 151, 152, 153, 153b-153n).
  >
  > **SMS 151 -- the Boo (TTelesa).** Hotel Delfino's ghost: drifts between its graph's nodes a little off the floor, bobbing and tilting, fading
  > in and out (the LOOP kind), hurts on touch; a jump passes through it until it is HOSED -- knocked back, shown, stunned on the floor and
  > solid, and then a jump pops it (coins). The PINK box Boo (Kind = Box) is always visible and a spray turns it into a pink block you stand on
  > for 25 s. Kinds: Loop, Box, Normal, CanSee. `assets/Sms/Enemy/Telesa.tscn` + `SmsTelesa.cs`, the block, shader and materials under
  > `assets/Sms/Enemy/Telesa/`, the models under `models/Eniemes/Telesa/`.
  >
  > **SMS 152 -- the Sleepy Boo (TDebuTelesa).** The huge Boo asleep across a doorway: snores, hurts and shoves on touch, nothing of Mario's
  > moves it. The game lets only Yoshi eat it; here `BeatenBy` (a knob, on by default) lets the hose wake it (AwakeSeconds) and a ground pound
  > on the woken one beat it. `assets/Sms/Enemy/DebuTelesa.tscn` + `SmsDebuTelesa.cs`.
  >
  > **SMS 153 -- King Boo (TBossTelesa), Casino Delfino's boss.** The roulette table (three discs with ground-pound switches), the slot machine
  > under him, the fruit / coin / enemy / jackpot rounds, the fan of fruit and two red peppers, a pepper in his mouth making him spicy and
  > solid, a fruit on him then taking one of three hit points, his Boo attackers, the Shine. `assets/Sms/Enemy/BossTelesa.tscn` +
  > `SmsBossTelesa.cs`, the slot / discs / arena / room / camera path / materials / effects under `assets/Sms/Enemy/BossTelesa/`, the models
  > under `models/Eniemes/BossTelesa/`. The polish passes after Jaysonn's playtests:
  > 153b the bmd's materials, every ms_btls effect, the hints, the rumble, BossMusic; 153c solid by default with a live GhostOpacity, depth-writing
  > shaders, the slot sinking into the table, drool strands, the gold ring pressed into a bowl; 153d-153e the bowl drawn, the drool facing the
  > camera, DroolOn, the seat over a lower slot, the crown's inside; 153f-153j hands emptied with a swallowed fruit, the damage flash, the spicy
  > fire from the game's own particles, the slot bolted to his clips, jumpable Boo attackers flying out of his mouth at Mario; 153k the ROOM
  > (the casino boss room's wall and ceiling from the game's files: `KingBooWall.tscn`, the arena's BossWall child); 153l the INTRO (Mario taken
  > onto the raised centre disc and lowered under the game's btelesa_roll_camera.bck, the slot's unseen fall) and the room's FIXED camera (the
  > game's camera cube: `SunshineCamera` learns the bck demo camera and the DefiniteD2 kind, `SmsCameraArea` an Eye marker); 153m King Boo
  > facing the camera, Mario in control from the landing, the intro camera where the bck puts it; 153n Mario riding the switch pad (the game's
  > taking matrix), the gold dome standing through the ride and sinking into its bowl as the fight begins, `IntroSpeed` pacing the intro
  > (1.5 = 11 s; 1 = the game's 16.7 s).
  >
  > **To try:** drop `assets/Sms/Enemy/Telesa.tscn` (with a Graph of Node3D children, or it roams), `assets/Sms/Enemy/DebuTelesa.tscn` in a
  > doorway, `assets/Sms/Enemy/BossTelesa/KingBooArena.tscn` where the table should stand (the room comes with it). Walk into the room: the
  > intro ride, then HOSE HIS TONGUE for a slot round; a pepper in his mouth, then a fruit on his body. The knobs are grouped and noted on each
  > scene (The fight, What he spits, Deviations, Looks, Effects, Camera, Sounds).
  >
  > **Drives:** every pass's headless drive green before its commit (the King Boo drive 153 at 74 / 74 checks for 153n, with captures through the
  > game camera); the numbers per pass are in PROJECT_LOG.md.
  >
  > **Not ported / deviations (logged):** Yoshi eating the Sleepy Boo (BeatenBy stands in); the sounds (slots on every species, nothing wired --
  > the sound pass follows on its own branch); the game's fixed intro pace (IntroSpeed 1 restores it); King Boo solid by default (GhostOpacity
  > 0.588 = the game's ghost); the Boo attackers' homing curve and spawn point (knobs, off = the game's); the pressed bowl as a mesh rebuild.
  > Also in the range: `Mario.cs` (SmsHP getter, SmsHeldGone, SmsCarryTo), `SmsSmallEnemy.cs` (the spawn yaw off the node's axis -- every enemy),
  > `SmsGoopStrand.cs` (FollowAnchor / FaceCamera), `SmsCameraShake.cs`, `Player.tscn` (three opaque materials). Jaysonn's live edits
  > (`assets/Level.tscn` and the rest) are not in it.
```
git push -u origin feature/enemy-sirena
git rev-list --count origin/feature/enemy-sirena..feature/enemy-sirena
gh auth status
gh pr create --base main --head feature/enemy-sirena --title "Feature: Sirena Beach's ghosts -- the Boo, the Sleepy Boo and King Boo's casino fight (SMS passes 151-153n)" --body-file pr151.md
```
Expect `0` from the rev-list. KEEP THE #6 ORDER: push the branch -> write the result paragraph below, commit it and push -> only THEN open the PR
(`gh pr create` if `gh auth status` is logged in, else the prefilled compare URL with the title, and the body from `pr151.md` -- over ~8000
characters of URL prefill the title and clipboard the body, as #8 learned). ONE merge only: this is a PR, so do NOT also `git merge` it into local
`main`. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the three
gatekeeper / launcher `.tres` re-saves, the four BossTelesa `.tres` re-saves, the two Koopa and the two Goop `.png.import` flips and the
`addons/godot_ai` churn: his, leave them (none of them belong in this PR).
Result: `feature/enemy-sirena` pushed as a NEW remote branch, 33 commits over `main` `c082a22`, 186 files / +2168381 / -36.
`git rev-list --count origin/feature/enemy-sirena..feature/enemy-sirena` returned `0`. `git merge-base feature/enemy-sirena main`
is exactly `c082a22`, so the branch sits on the trunk with no merge in it. All sixteen pass shas named in PROJECT_LOG.md confirmed
ancestors: `a5b0234` (151), `f29bf3e` (152), `525f1b5` (153), `a550172` (153b), `47b3351` (153c), `38322f3` (153d), `da307fd`
(153e), `acd4554` (153f), `570076c` (153g), `be8ce44` (153h), `cff77b9` (153i), `80160d0` (153j), `cc59542` (153k), `6570dca`
(153l), `877791a` (153m), `28674e1` (153n). All sixteen log entries are in the branch's PROJECT_LOG.md (151, 152, 153 and 153b through 153n: sixteen, one per sha, corrected from a miscount of seventeen in this paragraph's first draft). Nine of the 186 files
are modifications and 177 are additions: PROJECT_LOG.md, `assets/Mario.cs`, `assets/Player.tscn`, `assets/Sms/Enemy/SmsSmallEnemy.cs`,
`assets/Sms/Fx/SmsGoopStrand.cs`, `assets/Sms/SmsCameraArea.cs`, `assets/Sms/SmsCameraKinds.cs`, `assets/SunshineCamera.cs` and
this file.
THE INSERTION COUNT IS NOT WHAT IT LOOKS LIKE. 2160288 of the 2168381 insertions are six `.glb.import` files, 360048 lines and
11.67 MB each: `rulet00`, `rulet01`, `rulet02`, `srot_waku` and `srotrulet` under models/Eniemes/BossTelesa/, plus
models/Koopa/scene/delfinoBoss/map/map.glb.import. Everything hand-written in the branch is the other 8093 insertions across
180 files. This is the long-flagged `.import` bloat, not a new regression: `main` already carries 172 `.import` files over 1 MB,
the largest `ma_mdl1.glb.import` at 13.9 MB. Largest real assets added: `btelesa.glb` 1.6 MB, `telesa.glb` 0.26,
`debutelesa.glb` 0.16, then `SmsBossTelesa.cs` at 124 KB. Eight new scripts: SmsTelesa, SmsDebuTelesa, SmsBossTelesa,
SmsRoulette, SmsTelesaSlot, SmsBossWall, SmsTelesaBlock, SmsCameraPath.
Hygiene: 0 of Jaysonn's nine live edits in the range. The four BossTelesa `.tres` files do appear, as ADDITIONS the branch makes;
his uncommitted re-saves of them sit on top in the working tree and are not in the push, which is what the entry meant.
NOTE on the commit count: the entry says 32, measured 33. The extra one is `59cede2`, the commit that gave this entry its PR
title, body and gh command, which cannot count itself. The same off-by-one as every entry so far, and always the queue file's
own commit.
ONE CORRECTION TO THE PR BODY. Kiro's "Also in the range" list names `SmsCameraShake.cs`. That file is NOT in the range:
it exists on `main` already and the branch does not touch it (153b set its shake tables as data, it did not change the script).
It was replaced with `SmsCameraKinds.cs` (the DefiniteD2 kind), which the branch does modify. Everything else in the body is
Kiro's wording unchanged, with blank lines inserted between his hard-wrapped sections so GitHub does not render them as one
run-on paragraph, the same fix #15 needed.
UNMERGED SIBLINGS, measured by trial merge rather than by eye. Two branches are still open off older trunks: `feature/enemy-pinna2`
(entry #14) and `feature/select-stages` (entry #11). `git merge-tree` against each one conflicts in `PROJECT_LOG.md` and
`timeToPush!.md` only, so whichever merges after this one keeps both sides at those two tails. `feature/select-stages` also
touches `assets/Mario.cs`, which `comm -12` flags as a shared file, but its hunks sit around line 20340 while this branch's sit
at 17290 and 17528, and the trial merge auto-merges the file with no conflict. No scene, script, shader, material or model
collides.
`gh auth status` still prints "You are not logged into any GitHub hosts", the eighth entry running, so the browser route again.
The PR body went to `pr151.md` at the project root (untracked, delete after merge), 4650 bytes with the attribution footer.
ONE merge path, as the entry insists: this branch gets a PR and is NOT also merged into local `main`.
PR #14 OPENED at https://github.com/JasonAbsolute/ProjectShine/pull/14, base `main`, head `feature/enemy-sirena` at `a920350`,
34 commits / 186 files, `mergeable_state` clean. The compare URL carried the title and body prefilled at 7050 characters, under
the ~8000 ceiling #8 established. Verified through the public API that all 4647 characters of Kiro's prose landed byte for byte.
ONE DEFECT, cosmetic: the attribution footer's robot glyph arrived as `ðŸ¤–`. Windows PowerShell 5.1's `Get-Content -Raw`
reads a UTF-8 file with no BOM as cp1252, so the clipboard copy of `pr151.md` carried the mojibake and that is what reached the
body field. Next entry: read the body with `[System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)` before
`Set-Clipboard`, or keep the footer out of the clipboard path. A repaired copy of the body sits in the session scratchpad.
MERGED BY JAYSONN as a **SQUASH**: `main` is now `8348043`, one commit with a single parent `c082a22`. Local `main` was advanced
with `git fetch origin main:main`, which moves the branch ref without touching the working tree, so HEAD stayed on
`feature/enemy-sirena` and Jaysonn's thirteen uncommitted edits were never at risk. This is better than the `git merge --ff-only`
the last three entries used, which needs `main` checked out.
Verified by TREE, the way a squash requires: `git merge-base --is-ancestor 28674e1 main` says NO, while
`git diff --name-only feature/enemy-sirena main` is EMPTY, so every one of the 186 files matches the branch, both log files
included. Present on the trunk and checked individually: `Telesa.tscn`, `SmsTelesa.cs`, `DebuTelesa.tscn`, `SmsDebuTelesa.cs`,
`BossTelesa.tscn`, `SmsBossTelesa.cs`, `BossTelesa/SmsRoulette.cs`, `SmsTelesaSlot.cs`, `SmsBossWall.cs`, `KingBooArena.tscn`,
`KingBooWall.tscn`, `Telesa/SmsTelesaBlock.cs` and `SmsCameraPath.cs`. The fixed-point camera kind is on the trunk too:
`DefiniteD2` appears five times in `assets/SunshineCamera.cs` and four in `assets/Sms/SmsCameraKinds.cs`. `PROJECT_LOG.md` holds
all sixteen entries. `pr151.md` deleted.
THE TRUNK TIP: read it from `git log --oneline -1 main`, not from a sha written here. A commit cannot state its own sha, which
has now caught three entries.
STILL OPEN, carried forward: `feature/enemy-pinna2` (entry #14, SMS 147 and 148, the Bullet Bill and the ground Electrokoopa) and
`feature/select-stages` (entry #11, SMS 135) are both pushed and waiting on Jaysonn's playtest, and `feature/select-stages` holds
the only copy of #11's text. The exit-time C# finalize crash from #10's batch has still not recurred, now across the 151-153n
passes as well.

### #17 -- DONE 2026-11-17 -- https://github.com/JasonAbsolute/ProjectShine/pull/15 (SQUASH-merged by Jaysonn, inside #18's PR) -- ACTION LEFT: close PR #16, do not resolve it -- queued 2026-11-03 02:42 PST (Kiro) -- PUSH ONLY, its PR FOLDED INTO #18's (SMS 154 + 154b + 154c + 154d + 155 + 155b + 156 + 156b + 156c + 156d + 156e), updated 2026-11-17 01:30 PST
Push the twelfth feature branch: `feature/sounds`, branched from `main` `90efb17` (the trunk after PR #14, Sirena Beach's ghosts). The SOUND
branch -- the project had no audio at all. SMS 154: Mario's and FLUDD's sound effects from the game's own banks, fired the way the game fires them:
the footsteps and the pack's clanks off his animation tables (mario.szs's bas files), the jump voices by jump kind with the game's odds of the
grunts and the tired set, the landing cutting the voice, the hip drop, the wall hits, the damage cries, the slide, the water; the spray's two loops
(volume with the pressure), the hover's loop, the rocket's charge and launch, the turbo's launch, the empty tank, the refill, the nozzle swap, the
pack's deploy clicks. The waves are baked from the disc image's AudioRes by tools/projectshine/sound154/sms_audio.py (a port of the game's SE
sequence player: one WAV per MSD_SE id, 310 under assets/Sms/Sound/Mario/, loops carried in smpl chunks). NOT yet playtested by Jaysonn: push the
branch so it is on GitHub; do NOT open a PR until he says the sound work is done (more passes follow on this branch: the species' slots, the music). SMS 154b: Mario's sounds play FLAT -- no camera-distance falloff, no panning, no low-pass (154's 3D players made them quiet and muffled at King Boo's fixed camera); Positional (off) brings 3D channels back with the GAME's own distance curve (full within 1000 u, silent at 5000, the voices four times longer) instead of Godot's. SMS 154c: a SOUND EFFECTS volume (the Sfx bus, SfxVolume 0.5 = -6 dB; default_bus_layout.tres new), the rollout's voice over its whoosh (the game's ROTATE_BROAD_JUMP: MV22 + MA_ROPE_ROLL_FULL_A), and the missing Mario sounds (the dive's slide loop, the wall slide's loop, the spin jump's roll, the damage cries by kind, the throw's wail, the cap, the fruit's take / put, the water wait, the TAKE's cry) with Mario.cs's ten own sound slots filled from the game's banks. SMS 154d: the dive's start grunt (the game's JUMP_CATCH voice MV13, the tired MV34 under 3 HP) and the wall slide's thud by the speed he meets the wall with (getWallSound: soft under 30 units a tick, else hard) over the scrape; the wall kick checked against the game -- it has no sound effect of its own there, only the MV22 'woo' and ma_wjmp's push-off steps, both already played; the tree grab's wrong MV13 removed; and a Mario.cs fix the drive found -- a jump into a wall facing +Z never slid (the head-height ray left the node origin, 11.6 cm off the collision cylinder, and fell short). SMS 155: KING BOO's fight sounds from the game's banks through a new SmsActorSound node any species can take -- his animation sound tables, his script's eleven slots filled, the fire / roulette / intro / slot-machine loops, the Boo attackers' slots (assets/Sms/Sound/Enemy/, 39 WAVs); plus the slow slide's silent flip (a clip's frame-0 sounds never fired), the sequencer's held-note timing (five FLUDD / menu sounds re-baked) and the tables' per-entry pitch. SMS 155b: the intro's laugh as the game's engine really plays it -- rouletteFall restarts the one-shot V_LAUGH1B every frame, so its chuckle is never heard, only its first breath under the rumble (a knob: Game / Chuckle / Off; the laugh you hear is V_APPEAR as he surfaces); the baker's volume envelopes, same-voice note cuts, track closes and settled loop regions -- every WAV re-baked (357: 317 Mario/, 40 Enemy/); the game's own distance-curve values (1200 / 12000). SMS 156: the BOSS MUSIC -- the game's BGM sequences rendered with the game's instruments (sms_bgm.py: the tracks, their volumes / pans / bends / vibrato, the instruments' envelopes and velocity curves, the loop as the sequence loops): King Boo's BossMusic = MSD_BGM_MAP_SELECT, the boss theme he and Petey share; the Gatekeeper's = MSD_BGM_CHUBOSS, the mid-boss theme; music slots filled by the actors' sound node as 2D players on the Music bus (assets/Sms/Sound/Music/). SMS 156b: King Boo's music is the BOSS theme, t_boss.com (156 had the file select music: the decomp's BGM enum sits one place off the game's name table); Music/ is MSD_BGM_BOSS.wav (12.7 MB) + MSD_BGM_CHUBOSS.wav (7.3 MB). SMS 156c: the intro's chuckle plays once as the disc seats -- the game's last per-frame start of V_LAUGH1B is never stopped, so it plays out whole right as the elevator lowers into place. SMS 156d: IntroLaughPitch, King Boo's inspector knob for the chuckle's pitch and pace (1 = the game's notes); the baker squares velocity and honours gate times as the game does -- 51 WAVs re-baked. SMS 156e: those 51 WAVs back to 156c's at Jaysonn's call (the two rules now switches, off); the knob stays. Further passes re-point this entry's tip.
- Branch `feature/sounds` -- code tip `79b4c87` ("SMS 156e: the sounds back to 156c's ...", the 51 WAVs + SmsMarioSoundTable.cs restored) on the 156d commit ("SMS 156d: the intro chuckle's pitch / pace as an inspector knob ...", assets/Sms/Sound/ SmsActorSound.cs, SmsMarioSoundTable.cs, 51 WAVs under Mario/ and Enemy/, assets/Sms/Enemy/SmsBossTelesa.cs) on the 156c commit ("SMS 156c: the intro's chuckle plays once as the disc seats ...", assets/Sms/Sound/SmsActorSound.cs) on the 156b commit ("SMS 156b: King Boo's boss music is the game's BOSS theme ...", assets/Sms/Sound/ SmsActorSound.cs, SmsMarioSoundTable.cs, Music/ MSD_BGM_BOSS.wav new, MSD_BGM_MAP_SELECT.wav removed) on the 156 commit ("SMS 156: the BOSS MUSIC ...", assets/Sms/Sound/ SmsActorSound.cs, SmsMarioSoundTable.cs, Music/ 2 WAVs new, assets/Sms/Enemy/Spawn/SmsGateKeeper.cs a line) on the 155b commit ("SMS 155b: the intro laugh as the game's engine really plays it ...", assets/Sms/Sound/ SmsActorSound.cs, SmsMarioSoundTable.cs, every WAV under Mario/ and Enemy/ re-baked + Enemy/MSD_SE_BS_TELESA_V_LAUGH1B_ROULETTE.wav new) on the 155 commit ("SMS 155: KING BOO's fight sounds ...", assets/Sms/Sound/ SmsActorSound.cs + SmsActorSoundTable.cs new, SmsMarioSound.cs, SmsMarioSoundTable.cs, Enemy/ 39 WAVs, Mario/ 5 WAVs, assets/Sms/Enemy/ SmsSmallEnemy.cs + SmsBossTelesa.cs + BossTelesa/SmsTelesaSlot.cs + SmsRoulette.cs a line or two each) on the 154d commit ("SMS 154d: the dive's start grunt ... and the wall slide's thud by speed ...", SmsMarioSound.cs, SmsMarioSoundTable.cs, Mario/ +3 WAVs, Mario.cs) on the 154c commit ("SMS 154c: a SOUND EFFECTS volume ...", default_bus_layout.tres, SmsMarioSound.cs, SmsMarioSoundTable.cs, Mario/ +4 WAVs, Mario.cs) on the 154b commit ("SMS 154b: Mario's sounds play FLAT ...") on the 154 commit ("SMS 154: SOUND -- Mario's and FLUDD's sound effects ...", assets/Sms/Sound/ new: SmsMarioSound.cs,
  SmsMarioSound.tscn, SmsMarioSoundTable.cs, Mario/ 310 .wav + 310 .wav.import; assets/Mario.cs the SmsFluddSound probe; assets/Player.tscn the
  SmsSound child) on `main` `90efb17`; the branch tip is this file's own commit on top of them; 22 commits since `main`. The 310 WAVs are 19 MB in
  all (the biggest 0.4 MB); nothing near the 100 MB limit. The range carries none of Jaysonn's live edits (assets/Level.tscn and the rest).
- To try 156e: the sounds are 156c's again (the chuckle as it was); IntroLaughPitch on King Boo's inspector still there, 1 = as is.
- To try 156d: King Boo's inspector, IntroLaughPitch -- 1 is the game's own notes (deep: half to a third of the sample's speed), 1.25 a
  quarter faster and higher; FLUDD's nozzle clicks are the short clicks they are in the game, soft notes everywhere a touch quieter.
- To try 156c: King Boo's room -- as the elevator lowers into place, his chuckle (the little laughs, 6 s, through the slot machine's
  fall); nothing but the rumble on the way down, as in the game.
- To try 156b: King Boo's room -- the BOSS theme this time (t_boss.com: the brass-and-drums piece he and Petey share), from his first
  appearance, looping after 4 s of intro, fading at the down.
- To try 156: King Boo's room -- the boss theme starts as he surfaces (the game's own tune for him and Petey, rendered from its sequence),
  loops seamlessly, fades at the down; the Gatekeeper's mid-boss theme on its appear demo. The Music bus sets the level (MusicDb -4 dB under it);
  the render is dry -- a Reverb effect on the Music bus brings the GC's room back if wanted.
- To try 155b: King Boo's room again. The ride down is the rumble alone now (the game's engine never lets that laugh be heard --
  the knob SmsActorSound.IntroLaugh brings 155's chuckle back if wanted); his cackle comes as he surfaces, as before. The whole set
  re-baked: Mario's slide swells in, King Boo's fire dips after its burst, the rumbles and the slot's rattle loop without a pulse,
  one-shots end a touch tighter; his sounds carry further from the camera (the game's 12000-u fade, not 5000).
- To try 155: King Boo's room. The intro ride rumbles and he laughs as the disc sinks (a clunk as it seats); his "appear" cry; spray his tongue:
  the roll's "woo" and whirr, the drums' rattle, a clunk per drum, the fanfare; a pepper on the tongue: the spicy hit, then his fire roars
  through the burn; a fruit on the hot body: the damage cry and his long laugh; a plain fruit: the gulp; he fades with a whoosh; the discs
  rumble while they roll (a clunk when a switch stops one); hose him on the table: "thank you" and a giggle; the last hit: the down. The Boos
  pop, bounce and splash as before, with their appear / vanish. Mario: A on a slow belly slide -- the little flip whooshes now.
- To try 154d: dive (B in a run or a jump): the "hup" as it starts. Jump at a wall from a walk: a soft thud as the slide starts, then the
  scrape; from a run: a hard thud. A on the slide: the kick's "woo" and his push-off steps -- the game has no other kick sound. WallHitSounds
  (Deviations) turns the thud off. Grab a tree: the hand slaps only (154c's grunt there was wrong). Jump at a wall you are facing while
  running toward the level's -Z (the camera's default 'away'): it slides now -- before, the head-height check missed and you stopped dead.
- To try 154c: everything is at half volume now (SfxVolume on the Player's SmsSound, 0.5; the Sfx bus in the Audio panel). Dive, slide: the
  slide hisses; A on the slide: the rollout's "woo" over its roll whoosh (RollWhoosh off = the voice alone). Spin jump: the roll at its 10th
  tick. Get hit: the thud and the little cry; burn / shock: their cries with the slot sounds; thrown: the wail. Lose the cap, get it back.
  Pick up a fruit vs a barrel. Tread water. Fall for 2 s: the cry. Pump an empty tank: the squeaks. Climb a fence: punch and catch.
  Heal: the chime. Walk into King Boo's room: the "wah" as the ride takes you.
- To try 154b: the same, with the camera anywhere -- King Boo's fixed camera included: Mario's sounds stay at one volume, centred, clear.
  Positional (Camera distance, on the Player's SmsSound) puts them back in 3D with the game's long curve (FullVolumeUnits / SilentUnits).
- To try 154: play. Run: his steps and FLUDD's pack clanking; jump: the small grunt, the double jump's "woo", the triple's "yahoo", the back / side
  flip's big one, the spin jump's -- and the landing cuts them off, as the game does; hip drop: the thud; run into a wall: the hit and the cry; R:
  the spray's water and the nozzle's hiss (quieter at a light press); the hover's loop in the air; X: the nozzle swap's chime; the rocket: the
  charge's hiss then the launch. Knobs on the Player's SmsSound child: the volumes (Master / Voice / Step / Fludd dB), the pieces (AnimSounds,
  JumpVoices, LandingCutsVoice, ActionSounds, FluddSounds, FluddPackClanks), FloorMaterial (Stone by default: the game reads it off the floor,
  the port's floors have no material yet), VoiceVariantChance, TiredVoices, DebugPrint (every play in the output).
```
git push -u origin feature/sounds
git rev-list --count origin/feature/sounds..feature/sounds
```
Expect `0` from the rev-list. No PR of its own, no merge (the UPDATE below). The working tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of
`assets/Sms/SmsHiddenCoin.cs`, the gatekeeper / launcher and BossTelesa `.tres` re-saves, the Koopa and Goop `.png.import` flips and the
`addons/godot_ai` churn: his, leave them.

UPDATE 2026-11-17 (Jaysonn, after playing the rematch through SMS 229: "so far play testing feels good", "ok lets update timetopush!"): the
sound work for this release is done, and every one of this branch's 22 commits is in `feature/kingboo-rematch`'s history (#18 branched from this
tip), so this branch gets NO PR of its own: push it for the record (the commands above) and #18's single PR carries the sounds into `main` with
the rematch. Do not open a second PR for it -- the two would land the same code twice (the rule above). Mark this entry DONE with #18's PR URL
when that PR merges.

Result: `feature/sounds` pushed as a NEW remote branch, 22 commits over `main` `90efb17`, which matches the entry exactly with no
off-by-one for once. `git rev-list --count origin/feature/sounds..feature/sounds` returned `0`. `git merge-base feature/sounds main`
is `90efb17`, so it sits straight on the trunk with no merge in it.
NO PR OF ITS OWN, as the 2026-11-17 update instructs. `git merge-base --is-ancestor feature/sounds feature/kingboo-rematch` confirms
every one of these 22 commits is already in #18's history, so #18's single PR carries the sounds into `main` with the rematch. Opening
a second PR would land the same code twice. The 11 log entries 154 through 156e are in the range this branch adds.
This entry closes with #18's PR URL once that PR merges.


MERGED 2026-11-17 inside #18's PR, as this entry's update instructed: https://github.com/JasonAbsolute/ProjectShine/pull/15
(SQUASH-merged by Jaysonn). `main` is now `9b85369`. The sound code is on the trunk and was verified there directly, not inferred:
317 WAVs under assets/Sms/Sound/Mario/, and `SmsMarioSound.cs`, `SmsActorSound.cs`, `SmsMarioSoundTable.cs`, `MSD_BGM_BOSS.wav` and
`MSD_BGM_CHUBOSS.wav` all present. No separate merge of `feature/sounds` was needed or made.

PR #16 EXISTS AND MUST BE CLOSED, NOT FIXED. Jaysonn opened a second PR for `feature/sounds` after #15 had merged
(https://github.com/JasonAbsolute/ProjectShine/pull/16, titled "Feature/sounds", no description), and GitHub reports "This branch has
conflicts that must be resolved" in nine files: PROJECT_LOG.md, assets/Mario.cs, assets/Player.tscn, the two Music WAVs,
SmsActorSound.cs, SmsMarioSound.cs, SmsMarioSoundTable.cs and this file.
THE ACTION: close PR #16 with the "Close pull request" button. Do not resolve the conflicts, do not rebase, do not merge. Nothing is
lost by closing it, because `git diff --name-only feature/kingboo-rematch main` is EMPTY: #15's squash carried all 935 files of the
stack, the 22 sound commits included.
WHY RESOLVING IT WOULD BE WRONG, measured rather than assumed. Every one of those nine files was modified by the rematch passes AFTER
`feature/sounds`'s tip `7df58ad`: `git rev-list --count 7df58ad..feature/kingboo-rematch -- <file>` gives Mario.cs 20 commits,
SmsActorSound.cs 9, SmsMarioSoundTable.cs 6, SmsMarioSound.cs 3, Player.tscn 1, each music WAV 1 (pass 225 re-rendered them with the
release curves the right way round), PROJECT_LOG.md 80 and this file 82. So `main` holds the NEWER version of all nine and the branch
holds the 156e version. Taking the branch's side would roll Mario.cs back past twenty passes of the rematch, undo 225's music render
and drop entries 157 to 229 from the log. The conflict is the stack telling you the branch is spent, not that work is missing.
A CORRECTION to what the local Claude advised before the merges: it said to merge the SOUNDS PR first. That was wrong. Merging #18's
PR first was the better order and is what Jaysonn did, because the squash of the upper branch lands the whole stack in one commit and
leaves nothing to reconcile. The new rule at the top of this file records it that way round.
`feature/sounds` can be deleted, local and remote, once PR #16 is closed.

### #18 -- DONE 2026-11-17 -- https://github.com/JasonAbsolute/ProjectShine/pull/15 (SQUASH-merged by Jaysonn) -- queued 2026-11-04 02:37 PST (Kiro) -- PUSH + PR (SMS 157 + 158 + 159 + 160 + 161 + 162 + 163 + 164 + 165 + 165b + 166 + 167 + 168 + 169 + 170 + 171 + 172 + 173 + 174 + 175 + 176 + 176b + 177 + 178 + 179 + 180 + 181 + 182 + 183 + 184 + 185 + 186 + 187 + 188 + 189 + 190 + 191 + 192 + 193 + 194 + 195 + 196 + 197 + 198 + 199 + 200 + 200b + 201 + 202 + 203 + 204 + 205 + 206 + 206b + 207 + 208 + 208b + 208c + 209 + 210 + 211 + 211b + 212 + 213 + 214 + 215 + 216 + 217 + 218 + 219 + 220 + 221 + 222 + 223 + 224 + 225 + 226 + 227 + 228 + 229, with #17's SMS 154-156e in its history), PLAYTESTED BY JAYSONN through 229 (2026-11-17), updated 2026-11-17 01:30 PST
Push the thirteenth feature branch: `feature/kingboo-rematch`, branched from `feature/sounds` at `7df58ad` (entry #17's tip: the 22 sound commits
are in its history -- push #17 first, or this push carries them). THE KING BOO REMATCH, part 1: the transition from the roulette fight to the
custom fight of `KINGBOO_REMATCH_gdoc.txt` (the design doc at the project root, Jaysonn's; not committed). SMS 157: King Boo's `Rematch` knob
(on in KingBooArena.tscn; off = the game's Shine) -- when he is down, where the game makes its Shine, the casino table cracks like glass from the
centre to the rim and breaks (the three roulette discs re-cut into Voronoi cells on their own triangles and textures, FloorShatter.gdshader; the
collision goes, the pieces fly and fade, the game's glass-break sound), Mario falls through into a load trigger, the screen fades to black and the
rematch arena loads: Bouldergeist's planet from Super Mario Galaxy (models/Sms/Rematch/polta_planet.glb -- the .dae from Jaysonn's Downloads run
through Blender headless with its vertex colours kept), no FLUDD, a violet night; the two-line card over black ("You call that a fight fit for a
king?" / "I'll show you a real fight..."), Mario revealed 22 m up and falling as the screen fades in, King Boo (SmsKingBooRematch, the arena's
own actor on the casino boss's model) appearing 5.5 m over the floor once he lands, with the long laugh, then hovering and watching him. The
fight itself -- the attacks, the teleports, the faces -- is later passes on this branch; the animations another AI's. SMS 158: the BOMB BOOS
and the SPIN -- X without the FLUDD is Mario's grounded spin (a new state): it grabs a Bomb Boo in reach, a second spin throws it; the Boos rise
out of black foggy spots at the arena's centre (the supply; Random is a knob), drift to him and hover; three bursts on King Boo and he is down.
SMS 159: Galaxy's way -- the spin takes the Boo BY THE TONGUE, Mario holds it up in one arm (a lasso pose on his skeleton) and the Boo swings round
him; he walks it into King Boo (no throw). The Boo's look fixed (158's was purple and see-through): the files' own black body and yellow eyes, solid.
SMS 160: the Boo FLOATS as Sunshine's Boos do (telesa.prm: the bob and the tilt, the wander between random goals, the touch that hurts Mario unless
he is above it); the lasso arm out to the side, clear of his head (it was inside it). SMS 161: the arm further out, the closed FIST as the
lasso hand, the tongue rooted in the middle of the Boo's mouth and ending in the middle of the fist, the swing 3.6 m. SMS 162: King Boo's MOVES -- he
teleports between spots (the hide clip under a fade, a fade in elsewhere) and LICKS: in front of Mario, the roll clip's rear-up as the half-second tell,
the tongue re-aimed onto the floor under Mario (a new SkeletonModifier3D, FABRIK over the tongue chain); the plain hit; a jump clears it; a swung Boo
through the lick breaks it off; the gaps shorten per hit. SMS 163: the tongue aim dropped (it looked wrong) -- the roll clip's own whirl is the
whip, the tongue a bit longer (SmsTongueStretch), the hit the whip's joints through Mario over its front crossing, the spot where the tip skims the
floor through him. SMS 164: the BOOLOSSUS SMASH -- the crown pulses white, a Boolossus (Boolossus.obj -> boolossus.glb) falls on Mario and splits
into 15 Boos (Teresa.dae -> boo.glb) that ring outward for a second; the plain hit for both; a jump clears the ring; ~110 KB of new model files.
SMS 165: THREE Boolossi DIVE in from 20 m out and 24 m up (the target locks 0.6 s before the landing), with effects (a wisp trail, a ring of light and
smoke on the landing, the camera shake, puffs); the Boos solid white; the CROWN GLOWS the attack's colour (white for the smash) -- a light at the crown
and two new uniforms in the shared BossTelesaToon.gdshaderinc (0 = the casino's crown unchanged). SMS 165b (after screenshots): the Boolossus
faces its dive; the Boos are the port's own hotel Boo (telesa.glb; the doc's Galaxy boo.glb REMOVED -- its export lost the GX mirror wrap); the
crown SHINES (a sliding sheen + rim in the shader, solid while lit, sparkles, a flash, a quickening pulse) with no halo quad. SMS 166: GHOST
LIGHTNING -- the crown red, two walls of bolts (a new shader on the shock's biri sheet) close on Mario three times, the shock (Mario.TakeShock) when
a wall reaches him; the attacks picked by weight. SMS 167: GHOST BREATH -- the crown frosty blue, a stream of cold mist sweeping across the
arena along the floor; the hit is a new FREEZE on Mario (Mario.SmsFreezeFor: a second with his pose held and a frost fog, inside an ice block that
shatters as he thaws; no HP) -- a jump as the cold reaches him clears it. Mario.cs and SmsBodyOverlay.cs are touched. SMS 168: FIREBALLS --
the crown red-orange, a volley of fireballs spat out of his mouth that roll along the floor, bounce off the arena wall and shrink away; the hit is
the port's burn (Mario.TakeDamage with the Fire profile: FIRE_DOWN, 1 HP); the full jump clears a ball. SMS 169: GHOST WIND -- the crown pink,
two ghost tornados rise out of the floor and hunt Mario for 7 s; one that reaches him FLINGS him (the port's thrown state, Mario.SmsThrowMario: up
10 m, 18 m across, no HP); a run outpaces them. All six of the doc's attacks are in. SMS 170: the EFFECTS pass -- the lightning's bolts run
along the walls between glowing posts (lights, a meet shockwave), the tornado is a gust of spiralling ribbons and debris, the ice is a jagged chunk, a
fireball bursts on Mario, and a Bomb Boo swung into King Boo EXPLODES with the Bob-omb's blast. SMS 171: the frozen Mario holds a frame of the SHOCK
clip (ma_shock at half its length, the eyes squeezed shut) instead of the pose he was caught in; and a fix in SmsBodyOverlay.cs -- the damage fog's
shader copies now follow the eye textures (the knockdown's hurt eyes were hidden under the pink fog since the damage passes). SMS 172: the
lightning walls crackle with the shock's own particles (bolt streaks, arcs, flashes, spark crosses, post sparks, floor crawl) and ZAPS jump the gap as
they close; the fireballs are MOLTEN LAVA BALLS (LavaBall.gdshader NEW) popping out of the CROWN three at a time, lobbed down about Mario with landing
marks, bouncing and rolling; the breath is THICKER (bigger, denser puffs and a solid core). SMS 173: King Boo sits 4.5 m HIGHER (HoverLift; the
tongue clear of the floor) with the breath re-aimed to meet the floor 10 m out whatever his height, the lava balls 3 m across, the electricity YELLOW
and quicker. SMS 174: the intro CUTSCENE -- Mario lands, a laugh from nowhere, he looks about for it, King Boo surfaces 7 m in front of him (as the casino
boss rises from his table), laughs 3.4 s, sinks back under the floor, then rises at his spot for the fight; A skips it. SMS 175: Mario looks about with his
HEAD (a new SmsHeadLook modifier: left, right, ahead, the shoulders following; his body standing), his head on King Boo as he laughs; King Boo surfaces 12 m out
(7 stood them too close) with the two-shot's camera back for it. SMS 176: CUSTOM ANIMATIONS -- the look-around is a real clip now, Mario's
"where am I?" (ma_lookabout: left, right, then up with a shrug), and on the cut to the stand-off he drops into a BATTLE STANCE from Jaysonn's sprite refs
(ma_battle_stance_in, then the ma_battle_stance loop, fists closed) and holds it until the fight; the clips are baked from pose scripts by tools/anim/bake_anim.gd
(assets/Mario/Anims, README there) and sit in Mario's AnimationPlayer (Player.tscn) to edit like any other. SMS 176b: the stance turns his WHOLE
body (hips and legs, as the sprites) and he HOLDS it after the fight starts until the player's first input; the head finds King Boo over the turned chest.
SMS 177: King Boo's hurt is his casino TUMBLE (btelesa_damage), and every third hit he splits off a COPY (Luigi's Mansion 3) that keeps away and only throws,
slower and rarer; HitPoints 9. SMS 178: the swing RISES with Mario's jump (the hit from the air -- King Boo is out of reach from the ground), every
hurt ends in a TELEPORT to another spot (the reset), a hit on a copy is the same tumble and teleport with no damage (no more reveal-and-fade), three of him by the
sixth hit (MaxCopies 2) with a TELL (CopyTell: a silver crown, or no fangs), and the frost breath's hit SHOWS (a flash, the frost splashing on him, the ice climbing
him, a shake). SMS 179: the Bomb Boo's EXPRESSIONS -- its tongue MOVES (it sways, wags, lags behind a turn), a Boo Mario LOOKS AT is SHOCKED (it freezes where it
hangs, startles, trembles, stares back, its eyes burn and it DROOLS glossy drops) until he looks away, and it spawns Galaxy's way (a BLACK pool on the floor with two
GLOWING EYES in it facing Mario, then the Boo up THROUGH the floor in black smoke); its rim glow and burst puff grey-black, not violet. SMS 180: the shocked Boo
WORRIES (a head shake, a lean back, a cower with a nervous breath, sweat beads off its head), the spot's eyes a little bigger, and the intro's sink is King Boo's HIDE clip -- a
leap and a DIVE through the floor in a burst of his twinkle stars, a flash on the stone, a ring of ghost mist, a shock ring and a camera kick. SMS 181: his RISES
(the intro's and the fight's) have their effects -- a glow pulsing on the stone with wisps seeping up before he breaks through (a 0.7 s tell), then the flash, the shock
ring, the mist and twinkle stars streaming up round him -- and so does every TELEPORT (a poof and stars as he goes, a flash, a poof and stars where he comes in); the
flashes brighter and bigger. SMS 182: the APPEAR is clean and smooth -- the appear clip's own leap is the rise (the node waits at the hover height, the body
comes up out of the floor in one arc, no stall on the stone) -- and the CROWN SPARKLES are off (the tell's stars, the teleport-in stars; the rise's stars shed
off his body instead of a column up past his crown). SMS 183: the fight's END -- his DEFEAT (the down clip's tumble with his body blinking, mist and stars boiling off
him, his glow and crown flaring, sinking low, then a BURST: a flash, a shock ring, a poof, stars, a kick, an afterglow) and the SHINE (a golden flash where he burst,
the game's appear flight down onto its spot at the arena's middle under a demo camera, the world frozen); the Bomb Boo supply ends at the last hit. SMS 184:
the Bomb Boos spawn in RANDOM places -- the supply's Random mode is the default, its spots on the stone inside the arena's edge, 4-14 m from Mario, 6 m from the
last spot and 3 m from any Boo out. SMS 185: the pace UP (a move about every 2 s at the start, every second by the end, the first in 2 s, fewer plain hops) and the
THUNDER WALLS long and LOW (18 x 2.2 m: a held jump clears them, the ends 9 m off). SMS 186: the BEGINNING LAUGH is the casino intro's chuckle (V_LAUGH1B,
the climbing hee-hee-hee), the intro laughing 4.6 s, the chuckle cut at his dive; SmsActorSound gets Stop(se). SMS 187: the FREEZE BREATH made fair -- only
the dense cold freezes (the thin edge and the fading tail no longer hit), a jump clears it at 1 m, and the ICE is a shield (a hit through the frost shatters it and
does nothing else; Mario.cs SmsFrostShields). SMS 188: the SPLIT's CUTSCENE -- the world freezes, a camera cuts to a line of him across the arena, his copies come
in beside him in violet mist, all laugh together under a card line, then every one of them attacks (SmsSplitCutscene.cs, new). SMS 189: the card is off --
no text on the screen (SplitLine2 / SplitLine3 empty). SMS 190: the line sits higher in the frame (SplitCamLookUp) and the second split -- three of him --
is verified and shot. SMS 191: a hit on a fake is the mock -- it laughs it off, phases see-through and teleports off (CopyMock). SMS 192: the Boolossus bigger
and its Boos out 3 s; the walls' and the breath's hits put with their visuals (ShowHitBoxes draws them); the intro laugh climbs. SMS 193: the walls' last wrong
shock (outside them at the meet) gone; a Bomb Boo in hand bursts on a hit; the split mist white; the intro laugh 3 s. SMS 194: the split's arrivals rebuilt --
the mist and his stars gather into his centre as he forms, flickering, a ring under him, the pop; the laugh's climb ends at 1.15. SMS 195: the boss music
(the casino theme, from the fight's start, faded at the down) with the effects -4 dB under it; the wind is five tornados on a ring circling the arena in to the
middle and back out. SMS 196: the copies teleport about and keep 11 m apart; the music starts at the intro's cut to Mario in the battle stance.
SMS 197: the music is Jaysonn's Antasma Final Battle mp3 (NEW: assets/Sms/Sound/Music/AntasmaFinalBattle.mp3 + .import, 2.8 MB), from the intro's first frame, the
fight on its cue 7.5 s in. SMS 198: his clip changes cross-fade (ClipBlendSeconds 0.25). SMS 199: the breath leaves a 4.5 m mouth, its hit in 3 lanes; Mario's look-about without the shrug. SMS 200: a new attack, the Bomb Boo volley (three spat on lobs, two blast where they land, the last a dud to grab). SMS 200b: the Bomb Boos blink red before they blow. SMS 201: two new attacks -- the ambush from below (his pool hunts Mario, he erupts with a ring) and the Boo circle (SMW's ghost-house ring, one gap, closing). SMS 202: the Bomb Boo's puffs black, its one tongue. SMS 203: the split cutscenes clear the field, the spin jump grabs a Bomb Boo, no dud. SMS 204: the fireballs trail fire. SMS 205: a fireball goes out in a puff. SMS 206: the Bomb Boo in the Sunshine Boo's body (Look). SMS 206b: in the Bomb Boo's colours (black, yellow eyes). SMS 207: Mario's look-about rebuilt from curves, at its own pace. SMS 208: the arena under Luigi's Mansion's boss-arena sky, ported from the disc. SMS 208b: the final phase under its final-boss sky, the fire. SMS 208c: that fire pulled down the sky for the fight's downward camera. SMS 209: the arena's lights follow the sky -- the flashes on the ground, the fire's light in the final phase. SMS 210: embers in the air for the final phase, glow on the arena. SMS 211: scorch marks on the floor, Jaysonn's sounds on the attacks (and the attacks' sounds switched on at all). SMS 211b: the thunder waits for the fight (none through the intro). SMS 212: a death plane under the arena -- a fall off it is the game's fall death (the camera stops and watches him fall, the iris, a life down). SMS 213: the miss's music -- every music track cut, the game's TOO BAD! jingle (the game-over one with no life left), the stage waiting for its end. SMS 214: the iris's hard edge (the game's own), King Boo idling through the split, Boolossus's shadow round with a white circle where it lands. SMS 215: the smash's Boolossus is the game's Sleepy Boo, its landing the Sleepy Boo's own eaten sound. SMS 216: the miss's quiet (every sound effect silent but Mario's voice), the slow zoom onto a Mario who dies on the floor, King Boo's stars without the square halo. SMS 217: the Shine two and a half times bigger when it spawns, and King Boo's defeat turns the sky to a peaceful starry night with the northern lights. SMS 218: the fight sky's faint streaks in the game's magenta (they were a dark red-pink). SMS 219: the fight sky's colour flashes without their hard lines. SMS 220: the Shine's sounds from the game -- the appear's chime and fanfare, its hum, the chime at the touch, the Shine Get's fanfare -- for every Shine. SMS 221: King Boo's death camera pans out to a wide shot of the whole arena while the Shine jumps to the middle; the Shine's hum loops without its drop. SMS 222: the death camera cuts (no pan); the line down the arena floor gone; the HUD hides through a Shine's appear and behaves the game's way at the shine get; the volley's freed-owner error gone. SMS 223: his death plays out in full before the Shine comes and the camera cuts; the Shine's get chime and its two fanfares re-rendered with the game's release curves the right way round. SMS 224: the frame hitch at the Shine's spawn gone (the Shine made at the fight's start, parked hidden). SMS 225: the hitch at the Shine Get's landing gone (the SHINE! letters built at the console's start); the boss and mid-boss themes and the miss and game-over jingles re-rendered with the release curves the right way round. SMS 226: the .NET collector held off (a no-GC region) from the fatal hit through his death, the Shine, the get and the card save -- the two hitches left were its pauses. SMS 227: the port's garbage rate cut from about 1.4 MB/s to about 0.6 (the camera rig's and Mario's physics queries re-used, every Godot name they hand the engine made once). SMS 228: a touchdown turns his face onto the way he is moving (a spin jump landed facing its takeoff, 30 degrees off its momentum). SMS 229: the Bomb Boos keep off the rim -- the arena floor measures its own stone's edge (25.1 m), a Boo's wander and its body stay RimMargin (3 m) inside it, one let go or landed past it comes straight back in; the supply's margin and the volley's wall from the same edge. Further passes re-point this entry's tip.
- Branch `feature/kingboo-rematch` -- code tip `d05ba70` ("SMS 229: the King Boo REMATCH, part 74 -- the Bomb Boos KEEP OFF THE RIM: the arena floor measures its own STONE (the walkable top's edge, 25.1 m, apart from the kerb and the rock's shoulder), a Boo's wander goals and its body stay RimMargin (3 m) inside that edge, a Boo let go or landed as a dud past it comes straight back in, and the supply's margin and the volley's wall are measured from the same edge";
  assets/Sms/Rematch/ SmsArenaFloor.cs, SmsBombBoo.cs, SmsBombBooSpawner.cs, SmsKingBooRematch.cs) on the 228 commit
  ("SMS 228: the King Boo REMATCH, part 73 -- a spin jump came down facing its takeoff with the momentum 30 degrees off to one side; the touchdown now turns his face (and the model) onto the way he is moving, the momentum kept -- LandingKeepsDrift off is the game's own rule, the drift dropped and the slide along his face";
  assets/ Mario.cs) on the 227 commit
  ("SMS 227: the King Boo REMATCH, part 72 -- the port's garbage rate cut from about 1.4 MB/s to about 0.6 in the quiet fight: the camera rig's and Mario's physics queries made once and re-used (one ray query, one exclude list, the result dictionaries disposed), every Godot name they hand the engine (input actions, metadata keys, groups, clips, states) made once (SmsNames), the slide collisions and the floor's surface tag read without a new wrapper a frame";
  assets/ Mario.cs, SunshineCamera.cs; assets/Sms/ SmsNames.cs (+ .uid, new), MarioRays.cs (+ .uid, new), MarioCamData.cs, MarioYCam.cs) on the 226 commit
  ("SMS 226: the King Boo REMATCH, part 71 -- the two hitches left in the sequence (his death's start, the card save's opening) were the .NET garbage collector's pauses; from the fatal hit the collector is held off (a no-GC region) through his death, the Shine, the get and the card save -- no collection, no pause inside it";
  assets/Sms/Rematch/ SmsKingBooRematch.cs) on the 225 commit
  ("SMS 225: the King Boo REMATCH, part 70 -- the hitch at the Shine Get's landing gone (the SHINE! letters built at the console's start: 73 ms -> 32-34 ms); the four other baked music tracks -- the boss and mid-boss themes, the miss and game-over jingles -- re-rendered with the game's release curves the right way round";
  assets/Sms/Console/ SmsConsoleStr.cs; assets/Sms/Sound/Music/ MSD_BGM_BOSS.wav, MSD_BGM_CHUBOSS.wav, MSD_BGM_MISS.wav, MSD_BGM_GAMEOVER.wav) on the 224 commit
  ("SMS 224: the King Boo REMATCH, part 69 -- the frame HITCH at the Shine's spawn gone: the Shine is made at the fight's start and parked hidden, so the frame of the cut only places and shows it (135 ms -> 25-29 ms)";
  assets/ ShineSprite.cs; assets/Sms/Rematch/ SmsKingBooRematch.cs) on the 223 commit
  ("SMS 223: the King Boo REMATCH, part 68 -- his death plays out in full (the burst's flash and afterglow, the ring, the puffs, the stars, the down's emitters) BEFORE the Shine comes and the camera cuts; the Shine's get chime and its two fanfares re-rendered with the game's release curves the right way round (the chime rings out, the fanfares' notes decay instead of swelling back and cutting)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs; assets/Sms/Sound/ SmsMarioSoundTable.cs; assets/Sms/Sound/Object/ MSD_SE_SY_GET_SHINE.wav; assets/Sms/Sound/Music/ MSD_BGM_GET_SHINE.wav, MSD_BGM_SHINE_APPEAR.wav) on the 222 commit
  ("SMS 222: the King Boo REMATCH, part 67 -- the death camera CUTS to the wide shot and cuts back (no pan); the LINE down the arena floor gone (the floor's stone rows re-mapped unbroken round the ring); the console hides through a Shine's appear demo and behaves the game's way at the shine get; the volley's and his attacks' signals safe after their owner is gone";
  assets/ ShineSprite.cs, Mario.cs; assets/Sms/ SmsStandardHud.cs; assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsBooVolley.cs; models/Sms/Rematch/ polta_planet.glb) on the 221 commit
  ("SMS 221: the King Boo REMATCH, part 66 -- his DEATH CAMERA pans out to a wide shot of the whole arena and holds it while the Shine comes out where he burst and jumps to the middle, then glides back; the Shine's HUM loops without its drop (each note's release kept)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs; assets/Sms/Sound/Object/ MSD_SE_SHINE_EXIST.wav) on the 220 commit
  ("SMS 220: the King Boo REMATCH, part 65 -- the SHINE's SOUNDS, the game's own: the appear's chime and fanfare, the Shine's hum while it is out, the chime at the touch, the Shine Get's fanfare (cutting the music, the effects quiet but the system's and Mario's voice) -- for every Shine in the port";
  assets/ ShineSprite.cs, Mario.cs; assets/Sms/Sound/ SmsActorSound.cs, SmsMarioSound.cs, SmsMarioSoundTable.cs, Music/MSD_BGM_SHINE_APPEAR.wav + MSD_BGM_GET_SHINE.wav, Object/MSD_SE_SHINE_APPEAR.wav + MSD_SE_SHINE_EXIST.wav + MSD_SE_SY_GET_SHINE.wav (+ .import)) on the 219 commit
  ("SMS 219: the King Boo REMATCH, part 64 -- the fight sky's FLASHES without their HARD LINES (the clouds' texture seam and the fade's seam round the sky blended away: RematchSky's flash_soften)";
  assets/Sms/Rematch/ RematchSky.gdshader) on the 218 commit
  ("SMS 218: the King Boo REMATCH, part 63 -- the fight sky's STREAKS in the game's colours (the faint ones the game's MAGENTA again, not a dark red-pink: RematchSky's colour knobs read as picked, as 217's night)";
  assets/Sms/Rematch/ RematchSky.gdshader) on the 217 commit
  ("SMS 217: the King Boo REMATCH, part 62 -- the SHINE two and a half times bigger when it spawns (its star's centre where the normal one's is, its demo camera on the bigger star's middle), and the PEACEFUL NIGHT after the defeat (from his burst the sky fades to a starry night with the northern lights, the arena's lights calm, the thunder and the embers gone)";
  assets/ ShineSprite.cs; assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsArenaLighting.cs, SmsArenaEmbers.cs, RematchSky.gdshader) on the 216 commit
  ("SMS 216: the King Boo REMATCH, part 61 -- the MISS's QUIET (the game's demoModeIn: as the miss's jingle starts every sound effect goes silent but the system's and Mario's voice), the camera's slow ZOOM onto Mario when he dies on the floor (the game's normal dead demo), and King Boo's twinkle stars without their square halo";
  assets/ Mario.cs, SunshineCamera.cs; assets/Sms/ MarioCamData.cs; assets/Sms/Sound/ SmsActorSound.cs, SmsMarioSound.cs, SmsMarioSoundTable.cs; assets/Sms/Rematch/ SmsArenaLighting.cs, KingBooRematch.tscn; assets/Sms/Fx/ kira_add.gdshader) on the 215 commit
  ("SMS 215: the King Boo REMATCH, part 60 -- the SLEEPY BOO SMASH: King Boo's Boolossus is now the game's Sleepy Boo (its own model and toon look, snoring through its one clip at the game's pace, 7 m across as the Boolossus was), and its landing sounds the Sleepy Boo's own death, MSD_SE_EN_DB_TELSA_EATEN (what the game plays as Yoshi eats it)";
  assets/Sms/Rematch/ Boolossus.tscn, SmsBoolossus.cs; assets/Sms/Sound/ SmsMarioSoundTable.cs; NEW assets/Sms/Sound/Enemy/ MSD_SE_EN_DB_TELSA_EATEN.wav (+ .import, 34 KB)) on the 214 commit
  ("SMS 214: the King Boo REMATCH, part 59 -- the IRIS's HARD EDGE (the game's Hx_Circle: black outside to a hard edge, three faint rings inside; 61's soft rim was the port's), King Boo IDLING through the split (no longer frozen mid-air in the laugh's last pose), and BOOLOSSUS's shadow ROUND with a WHITE CIRCLE where it will land (the Bomb Boo volley's mark, in white)";
  assets/Sms/Console/ Fader.gdshader, SmsFader.cs; assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsBoolossus.cs) on the 213 commit
  ("SMS 213: the King Boo REMATCH, part 58 -- the MISS's MUSIC: a miss cuts every music track at once (the boss music with it) and plays the game's own TOO BAD! jingle (MSD_BGM_MISS), with no life left the game-over jingle (MSD_BGM_GAMEOVER), and the stage waits for its end -- for every miss in the port";
  assets/ Mario.cs; assets/Sms/Sound/ SmsActorSound.cs, SmsMarioSound.cs, SmsMarioSoundTable.cs; NEW assets/Sms/Sound/Music/ MSD_BGM_MISS.wav + MSD_BGM_GAMEOVER.wav (+ .import, 1.6 MB)) on the 212 commit
  ("SMS 212: the King Boo REMATCH, part 57 -- a DEATH PLANE under the arena: a fall off it ends in the game's fall death (the cry, the camera stops and watches him fall away, the iris, TOO BAD!, a life down, the fight again from the fall-in) -- the death plane, the fall death and the camera's hell dead demo ported for any level";
  assets/ Mario.cs, SunshineCamera.cs; assets/Sms/ MarioCamData.cs, NEW SmsDeathPlane.cs + .uid; assets/Sms/Rematch/ RematchArena.tscn) on the 211b commit
  ("SMS 211b: the King Boo REMATCH, part 56b -- the THUNDER WAITS FOR THE FIGHT: none through the card, Mario's fall and the intro cutscene (the laugh from nowhere, the look-about, King Boo's rise and dive) -- it rolls from the sky's first purple flash after King Boo is up for the fight";
  assets/Sms/Rematch/ SmsArenaLighting.cs) on the 211 commit
  ("SMS 211: the King Boo REMATCH, part 56 -- SCORCH MARKS on the floor where the Bomb Boos blast and the lava balls land (fading decals: glowing orange fresh, black for eight seconds, gone over ten more) and JAYSONN'S SOUNDS for the attacks (his FireStart / FireEnd on the lava balls, Boolossus's bound and pon, the cold breath's blow, the ice's freeze / unfreeze, the lightning's crackles and zap, the wind's gust, the sky's thunder) -- and the attacks' sounds switched on at all (SmsActorSound.ActorOf never knew the attack classes: their sounds had never played)";
  assets/Sms/Rematch/ RematchArena.tscn, SmsArenaFloor.cs, SmsBombBoo.cs, SmsFireballs.cs, SmsArenaLighting.cs, SmsGhostLightning.cs, SmsGhostWind.cs, SmsGhostBreath.cs, SmsIceBlock.cs, SmsBoolossus.cs; NEW SmsScorchMarks.cs + .uid; assets/Sms/Sound/ SmsActorSound.cs; NEW assets/Sms/Sound/Rematch/ (twelve WAVs + .import, 1 MB)) on the 210 commit
  ("SMS 210: the King Boo REMATCH, part 55 -- EMBERS IN THE AIR for the final phase (a field of glowing sparks born in a ring round the arena's edge and out over the void, rising and wandering, fading in with the sky's fire) and GLOW on the arena (only what is brighter than the threshold blooms: the flames, King Boo's white, the lava balls, the embers)";
  assets/Sms/Rematch/ RematchArena.tscn; NEW SmsArenaEmbers.cs + .uid) on the 209 commit
  ("SMS 209: the King Boo REMATCH, part 54 -- the arena's LIGHTS FOLLOW THE SKY: Luigi's Mansion's lightning lights the ground (the sky's six cloud flashes added to the ambient and the sun on the same frame as in the clouds, the sky and the lights on one clock), and in the final phase the ambient, the fog and the sun blend to the fire's colours with the sky";
  assets/Sms/Rematch/ RematchArena.tscn, RematchSky.gdshader; NEW SmsArenaLighting.cs + .uid) on the 208c commit
  ("SMS 208c: the King Boo REMATCH, part 53c -- the final phase's FIRE pulled DOWN the sky for the fight's downward camera: the vrKoopa dome's elevations stretched from its top ring (final_stretch 2.5: the flames that ended 8 deg under the horizon now end ~55 deg under it, the red cap under that), the flame textures laid 1.6x down the taller band so the tongues keep their shape; final_drop to lower the whole dome besides";
  assets/Sms/Rematch/ RematchSky.gdshader) on the 208b commit
  ("SMS 208b: the King Boo REMATCH, part 53b -- the FINAL PHASE's SKY: at the second split the arena's sky cross-fades to Luigi's Mansion's FINAL-BOSS sky, the King Boo / Bowser roof fight's vrKoopa dome ported from the disc (a dark red backdrop, two layers of fire laid round the horizon, red-to-yellow streaks, a red glow below), the fade running through the frozen cutscene";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsSplitCutscene.cs, RematchSky.gdshader, RematchSky.tres; NEW Sky/lm_gaku_fire01.png + lm_ramp02.png (+ .import)) on the 208 commit
  ("SMS 208: the King Boo REMATCH, part 53 -- the arena's SKY is Luigi's Mansion's BOSS-ARENA SKY, ported from the GameCube disc (the vrball_B dome of the Bogmire, Boolossus and Chauncey fights: its three textures, its four texture scrolls, its colour flashes, drawn by a Godot sky shader on the arena's WorldEnvironment; the lighting untouched)";
  assets/Sms/Rematch/ RematchArena.tscn; NEW RematchSky.gdshader + .uid, RematchSky.tres, Sky/lm_bg04.png + lm_sin_wave.png + lm_gs001m.png (+ .import); NEW tools/lm/lm_sky_rip.py + lm_sky_desc.py) on the 207 commit
  ("SMS 207: the King Boo REMATCH, part 52 -- Mario's LOOK-ABOUT rebuilt from MOTION CURVES after the game's own idle (a glance left, a sweep right, back to the front in 2.0 s; the head leads, the chest follows, the arms swing with it, no head cock, no reaching arm) and played at its OWN PACE (LookKeepsPace: the music cue's trim comes out of the laugh first -- 197 had quickened the 3 s clip x1.5)";
  assets/Sms/Rematch/ SmsRematchIntro.cs; assets/Mario/Anims/ ma_lookabout.pose.json (generated) + ma_lookabout.tres (re-baked) + README.md; NEW tools/anim/lookabout_curves.py) on the 206b commit
  ("SMS 206b: the King Boo REMATCH, part 51b -- the Sunshine Boo in the BOMB BOO's COLOURS (the game's textures recoloured in code as the Boo loads: the body's white near-black, the eye's grey yellow and its blue iris amber; the red mouth, the orange-red tongue and the white fangs kept -- 206's white blue-eyed Boo behind the knob)";
  assets/Sms/Rematch/ SmsBombBoo.cs) on the 206 commit
  ("SMS 206: the King Boo REMATCH, part 51 -- the BOMB BOO in the SUNSHINE BOO's body (the experiment: the game's own hotel Boo -- telesa.glb, its toon materials, its blue eyes, fangs, red mouth and rigged tongue, telesa_wait playing -- in the Galaxy Boo's place, with every Bomb Boo way kept: the rise, the bob, the shock with the game's own flinch, the lasso grab in the mouth's red, the fuse's red blink, the lob; Look = Galaxy brings 158-205's black Boo back)";
  assets/Sms/Rematch/ SmsBombBoo.cs; NEW BombBooSunshine.gdshader + .uid) on the 205 commit
  ("SMS 205: the King Boo REMATCH, part 50 -- a FIREBALL GOING OUT ends in a little PUFF (dark smoke rising from where it was, a flash, a ring of light, embers flung out; 168-204's balls shrank to nothing and winked out)";
  assets/Sms/Rematch/ SmsFireballs.cs) on the 204 commit
  ("SMS 204: the King Boo REMATCH, part 49 -- the FIREBALLS TRAIL FIRE (a stream of flame left in the world behind each ball as it flies and rolls, drifting back the way it came and rising as it dies; hot streaks along the motion; the ball's own flames leaning back with its speed -- 168-203's flames rode on the ball and only embers were left behind)";
  assets/Sms/Rematch/ SmsFireballs.cs) on the 203 commit
  ("SMS 203: the King Boo REMATCH, part 48 -- the split CUTSCENES CLEAR THE FIELD (as the phase 2 / phase 3 cutscene cuts in, every attack live in the arena is gone at once: his and his copies' tornados, walls, fireballs, breath, spat Bomb Boos, the Boo circle, the Boolossi and their wave, the ice on Mario -- 188-202 froze them with the world and ran them on after), the SPIN JUMP GRABS a Bomb Boo (the stick spin and A, or A out of the X spin, takes a free Boo by the tongue in the air -- the X spin alone before), and the volley has NO DUD (VolleyDud off: every Boo he spits is lit and blasts)";
  assets/Mario.cs; assets/Sms/Rematch/ SmsKingBooRematch.cs SmsSplitCutscene.cs SmsBombBoo.cs SmsGhostLightning.cs SmsGhostWind.cs SmsFireballs.cs SmsGhostBreath.cs SmsBooVolley.cs SmsBooCircle.cs SmsBoolossus.cs SmsIceBlock.cs) on the 202 commit
  ("SMS 202: the King Boo REMATCH, part 47 -- the Bomb Boo's PUFFS are BLACK (its own smoke under a dark-violet flash: the burst in Mario's hand, a vanish, the volley's blasts -- 158-201's port burst kept its white smoke whatever the tint) and it has ONE TONGUE (the body glb's own tongue strip, baked under the port's swinging tongue since 159, taken out of the mesh as it loads)";
  assets/Sms/Rematch/ SmsBombBoo.cs) on the 201 commit
  ("SMS 201: the King Boo REMATCH, part 46 -- two new attacks: the AMBUSH from below (he sinks where he stands, his glow pool hunts Mario across the stone for 3 s, stops and flares, and he erupts there through the floor with a 5 m shock ring -- jump at the break; he is up at the pool's spot) and the BOO CIRCLE, Super Mario World's ghost-house ring (seven hotel Boos on eight places about Mario, one gap, turning once every 4 s at head height; it holds 2 s, closes on him over 8 s and fades -- out through the gap as it comes round)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs; NEW SmsBooCircle.cs + .uid) on the 200b commit
  ("SMS 200b: the King Boo REMATCH, part 45 -- the Bomb Boos BLINK RED before they blow (a spat Boo through its fall, quicker and brighter as it comes down; the floating dud through its fuse's last 2.5 s; a timed hold's last 2 s), the eyes kept yellow; the dud only slow and faint in the air";
  assets/Sms/Rematch/ SmsBombBoo.cs, BombBoo.gdshader) on the 200 commit
  ("SMS 200: the King Boo REMATCH, part 44 -- a new attack, the BOMB BOO VOLLEY: he rears back and SPITS three lit Bomb Boos out of his mouth on high lobs about Mario, a ring on the floor marking where each comes down; two BLAST where they land (the Bob-omb explosion, a 3.5 m ring of light, a standing Mario within it hit -- a jump clears it), the LAST is a DUD that thuds down and floats there as a live Bomb Boo for Mario to grab, its fuse still burning (6 s, then it blasts where it floats)";
  assets/Sms/Rematch/ SmsBombBoo.cs, SmsKingBooRematch.cs; NEW SmsBooVolley.cs + .uid) on the 199 commit
  ("SMS 199: the King Boo REMATCH, part 43 -- the BREATH leaves a WIDE MOUTH (BreathMouthWidth 4.5 x 1.4 m: the mist, its dense core and the glints born across it, the mouth's light stretched to it, and the cold that freezes in 3 lanes across it -- one 0.7 m point of contact before) and Mario's look-about WITHOUT THE SHRUG (the arms rest; the glances and the look up stay)";
  assets/Sms/Rematch/ SmsGhostBreath.cs, SmsKingBooRematch.cs; assets/Mario/Anims/ ma_lookabout.pose.json, ma_lookabout.tres) on the 198 commit
  ("SMS 198: the King Boo REMATCH, part 42 -- his clip changes CROSS-FADE (ClipBlendSeconds 0.25: the pose he is in melts into the new clip's first frames -- the wait into the laugh, the laugh into the dive, the tumble into the wait, the laugh's own loop -- instead of the game's hard cut between bcks; 0 = the cut)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs) on the 197 commit
  ("SMS 197: the King Boo REMATCH, part 41 -- the music is ANTASMA FINAL BATTLE (Jaysonn's mp3 from Ref/, now assets/Sms/Sound/Music/AntasmaFinalBattle.mp3, looping back to 7.5 s), it starts on the intro's FIRST frame (Mario looking about for the laugh), and the FIGHT STARTS ON ITS CUE: 7.5 s after the music begins (FightCueSeconds) -- the intro trims the look-about and the laugh in proportion to land there (x0.67: 3 s each -> 2 s)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsRematchIntro.cs; NEW assets/Sms/Sound/Music/AntasmaFinalBattle.mp3 + .import) on the 196 commit
  ("SMS 196: the King Boo REMATCH, part 40 -- the copies TELEPORT ABOUT and keep APART (a copy's move is a hop 0.6 of the time, 0.3 before, and waits 1.3 x his gap, 1.7 before; every spot any of him picks keeps SpotMinFromBoos 11 m from the other King Boos, where they stand or where they are going); the boss music starts at the intro's CUT to Mario in the battle stance (as King Boo surfaces), not at the fight";
  assets/Sms/Rematch/ SmsKingBooRematch.cs) on the 195 commit
  ("SMS 195: the King Boo REMATCH, part 39 -- the BOSS MUSIC (the casino King Boo's theme, MSD_BGM_BOSS, starts as the fight begins after the intro's dive and fades as the last hit takes him down) with the rematch's effects a bit UNDER it (SfxDb -4 on every SMS actor's sounds while the arena is loaded); the WIND is FIVE tornados on an even ring that CIRCLES the arena while it breathes IN to the middle and back OUT (the Spiral pattern; 169's hunt kept as a knob)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsGhostWind.cs; assets/Sms/Sound/SmsActorSound.cs) on the 194 commit
  ("SMS 194: the King Boo REMATCH, part 38 -- the split cutscene's arrivals REBUILT: each of him GATHERS out of the mist (white puffs and his twinkle stars streaming in from 10 m to his body's CENTRE while the body forms, flickering, a ring of cold light racing out over the stone under him), then POPS solid (the mist boiling back out, a flash); the effects centred on the body as measured off the wait pose; the intro laugh's climb ends LOWER (IntroLaughPitchTo 1.4 -> 1.15, From 0.7 -> 0.75)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsSplitCutscene.cs) on the 193 commit
  ("SMS 193: the King Boo REMATCH, part 37 -- the thunder walls' last wrong shock gone (192 had kept a 'gap shut' test that zapped a Mario OUTSIDE the walls as they met: he had jumped one where it stood); a Bomb Boo in Mario's hand BURSTS when he is hit (hurt, shocked, frozen or killed: the pop and gone, no drift); the split cutscene's mist WHITE (a cold white, violet before); the intro laugh SHORTER (IntroLaughSeconds 4.6 -> 3, the climb runs over it)";
  assets/Sms/Rematch/ SmsGhostLightning.cs, SmsKingBooRematch.cs; assets/Mario.cs) on the 192 commit
  ("SMS 192: the King Boo REMATCH, part 36 -- the Boolossus a sixth BIGGER and its wave of Boos lasting 3 s (bursting out, easing to a stop WaveReach out); the thunder walls' and the freeze breath's HITS put with their visuals (a wall is a SLAB about its drawn face and its shock stops where the bolts fade; the breath's mist no longer STALLS on the floor 10 m out while the cold that freezes ran on to 25 -- FloorDrag 0; ShowHitBoxes draws every hit volume in play); the intro laugh starts LOW and CLIMBS (IntroLaughPitchFrom 0.7 -> IntroLaughPitchTo 1.4)";
  assets/Sms/Rematch/ SmsBoolossus.cs, SmsGhostLightning.cs, SmsGhostBreath.cs, SmsKingBooRematch.cs; assets/Sms/Sound/SmsActorSound.cs) on the 191 commit
  ("SMS 191: the King Boo REMATCH, part 35 -- a hit on a FAKE is the MOCK: the bomb bursts, the copy does not flinch -- it LAUGHS IT OFF (the laugh clip and the short high cackle), rocks and bobs, PHASES see-through (flickering, the crown with it), then teleports off to another spot; the real one keeps the tumble -- the contrast is the tell";
  assets/Sms/Rematch/ SmsKingBooRematch.cs) on the 190 commit
  ("SMS 190: the King Boo REMATCH, part 34 -- the split cutscene's line sits HIGHER in the frame (the camera looks SplitCamLookUp 1.5 m over the hover, was 4) and the SECOND split -- THREE of him -- is verified and shot (the copy already out teleports from its spot to its place in the line, the lens pulls back to fit the three)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsSplitCutscene.cs) on the 189 commit
  ("SMS 189: the King Boo REMATCH, part 33 -- the split cutscene's CARD is off: no text on the screen (SplitLine2 / SplitLine3 empty; text in them brings the card back) -- the scene is the line of him and the chorus";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsSplitCutscene.cs) on the 188 commit
  ("SMS 188: the King Boo REMATCH, part 32 -- the SPLIT's CUTSCENE: as the hurt that earns a copy ends the world freezes and a camera cuts to a LINE across the arena -- he vanishes in a poof and comes back at his place out of violet ghost mist, laughing; his copies come in beside him one by one in a flash and the mist; all of them turn on Mario and LAUGH TOGETHER under a card line; the world runs again with every one of them attacking";
  assets/Sms/Rematch/ SmsSplitCutscene.cs (+ .uid, new), SmsKingBooRematch.cs) on the 187 commit
  ("SMS 187: the King Boo REMATCH, part 31 -- the FREEZE BREATH made fair: only the DENSE cold freezes (a puff's ball shrinks with its own fade and is gone past three quarters of its life -- the thin edge and the fading tail no longer hit; the ball 0.3 of the puff, the core's size), a jump clears it at 1 m, and the ICE is a shield (a hit or a shock through the frost shatters it and does nothing else: no knockdown, no HP)";
  assets/Sms/Rematch/ SmsGhostBreath.cs, SmsKingBooRematch.cs, assets/ Mario.cs) on the 186 commit
  ("SMS 186: the King Boo REMATCH, part 30 -- the BEGINNING LAUGH is the casino intro's CHUCKLE (MSD_SE_BS_TELESA_V_LAUGH1B, the climbing hee-hee-hee the casino plays as the roulette seats; the long V_LAUGH1 before), at IntroLaughPitch, the intro laughing 4.6 s and the chuckle cut at his dive; SmsActorSound gets Stop(se) / IsPlaying(se)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, assets/Sms/Sound/ SmsActorSound.cs) on the 185 commit
  ("SMS 185: the King Boo REMATCH, part 29 -- the pace UP (a move about every 2 s at the start, every second by the end; the first in 2 s; fewer plain hops) and the THUNDER WALLS long and LOW (18 x 2.2 m, were 10 x 5: a held jump clears them, the open ends are 9 m off -- the dodge is the jump)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsGhostLightning.cs) on the 184 commit
  ("SMS 184: the King Boo REMATCH, part 28 -- the Bomb Boos spawn in RANDOM places: the supply's Random mode is the default (158's Centre was for the playtests) and its spots are picked with rules -- on the arena's stone and EdgeMargin inside its edge, MinFromMario to MaxFromMario from Mario, MinFromLast from the last spot, MinFromBoos from any Boo out";
  assets/Sms/Rematch/ SmsBombBooSpawner.cs) on the 183 commit
  ("SMS 183: the King Boo REMATCH, part 27 -- the fight's END: his DEFEAT (the down clip's tumble with his body blinking, ghost mist and stars boiling off him, his glow swelling and his crown flaring, sinking low, then a BURST -- a flash, a shock ring, a poof, stars, a kick, an afterglow) and the SHINE (materialising in a golden flash where he burst, flying the game's way down onto its spot at the arena's middle under a demo camera with the world frozen); the Bomb Boo supply ends at the last hit";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsBombBooSpawner.cs) on the 182 commit
  ("SMS 182: the King Boo REMATCH, part 26 -- the APPEAR is clean and smooth (the appear clip's own leap is the rise: the node waits at the hover height and the body comes up out of the floor in one arc, no stall on the stone) and the CROWN SPARKLES are off (the tell's stars, the teleport-in stars, and the rise's stars shed off his body instead of a column up past his crown)";
  assets/Sms/Rematch/ SmsKingBooRematch.cs) on the 181 commit
  ("SMS 181: the King Boo REMATCH, part 25 -- effects for him COMING UP out of the ground (a glow pulsing on the stone and wisps before he breaks through, then the flash, the shock ring, the mist and twinkle stars streaming up round him) and for every TELEPORT (a poof and stars as he goes, a flash, a poof and stars where he comes in); the flashes brighter and bigger";
  assets/Sms/Rematch/ SmsKingBooRematch.cs) on the 180 commit
  ("SMS 180: the King Boo REMATCH, part 24 -- the shocked Bomb Boo WORRIES (it shakes its head, leans back, cowers, breathes, sweats), the spot's eyes a little bigger, and the intro's sink is King Boo's HIDE clip -- a leap and a DIVE through the floor in a twinkle burst, a flash, a ring of ghost mist and a shock ring";
  assets/Sms/Rematch/ SmsBombBoo.cs, SmsBombBooSpawner.cs, SmsKingBooRematch.cs) on the 179 commit
  ("SMS 179: the King Boo REMATCH, part 23 -- the Bomb Boo's EXPRESSIONS: its tongue MOVES, a Boo Mario LOOKS AT is SHOCKED (it freezes, stares back, trembles and DROOLS), and it spawns Galaxy's way (a BLACK pool with GLOWING EYES, then up through the floor in black smoke); black, not purple";
  assets/Sms/Rematch/ SmsBombBoo.cs, SmsBombBooSpawner.cs) on the 178 commit
  ("SMS 178: the King Boo REMATCH, part 22 -- the swing RISES with Mario's jump (the hit from the air), every hurt ends in a TELEPORT, copies take hits and teleport too (no damage) with a TELL, three of him by the sixth hit, and the frost breath's hit SHOWS";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsBombBoo.cs, SmsGhostBreath.cs, SmsIceBlock.cs; assets/Sms/Enemy/BossTelesa/BossTelesaToon.gdshaderinc (crown_recolor, default 0: the casino boss unchanged);
  tools/anim/probe_shader.gd NEW (+ .uid)) on the 177 commit
  ("SMS 177: the King Boo REMATCH, part 21 -- the HURT is his casino TUMBLE, and every third hit he splits off a COPY (Luigi's Mansion 3) that throws from afar and takes no damage";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsBombBoo.cs; tools/anim/probe_clip.gd NEW (+ .uid)) on the 176b commit
  ("SMS 176b: the King Boo REMATCH, part 20b -- the battle stance turns his whole BODY (as the sprites), and he HOLDS it after the fight starts until the player's first input";
  assets/Mario/Anims/ ma_battle_stance / ma_battle_stance_in .pose.json + .tres, assets/Sms/SmsHeadLook.cs, assets/Mario.cs, assets/Sms/Rematch/SmsRematchIntro.cs) on the 176 commit
  ("SMS 176: the King Boo REMATCH, part 20 -- CUSTOM ANIMATIONS: Mario's \"where am I?\" look-around and his BATTLE STANCE as real clips, baked from pose scripts";
  assets/Mario/Anims/ NEW (README.md, ma_lookabout / ma_battle_stance_in / ma_battle_stance .pose.json + .tres), tools/anim/bake_anim.gd NEW (+ .uid), assets/Player.tscn,
  assets/Mario.cs, assets/Sms/Rematch/SmsRematchIntro.cs) on the 175 commit
  ("SMS 175: the King Boo REMATCH, part 19 -- Mario LOOKS ABOUT with his HEAD (a new look-around), King Boo surfaces 12 m out";
  assets/Sms/SmsHeadLook.cs NEW (+ .uid), assets/Mario.cs, assets/Sms/Rematch/SmsRematchIntro.cs) on the 174 commit
  ("SMS 174: the King Boo REMATCH, part 18 -- the INTRO CUTSCENE: Mario looks about for the laugh, King Boo surfaces, laughs, sinks, the fight starts";
  assets/Mario.cs, assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsRematchIntro.cs) on the 173 commit
  ("SMS 173: the King Boo REMATCH, part 17 -- he sits HIGHER, the lava balls BIGGER, the electricity YELLOW and quicker";
  assets/Sms/Rematch/ SmsKingBooRematch.cs, SmsFireballs.cs, SmsGhostLightning.cs) on the 172 commit
  ("SMS 172: the King Boo REMATCH, part 16 -- the ELECTRICITY, LAVA BALLS out of the crown, a THICKER breath";
  assets/Sms/Rematch/ LavaBall.gdshader NEW, SmsFireballs.cs, SmsGhostLightning.cs, SmsGhostBreath.cs, SmsKingBooRematch.cs, three shader .uid files) on the 171 commit
  ("SMS 171: the King Boo REMATCH, part 15 -- the freeze holds a frame of the SHOCK";
  assets/Mario.cs, assets/Sms/Fx/SmsBodyOverlay.cs) on the 170 commit ("SMS 170: the King Boo REMATCH, part 14 -- the EFFECTS pass"; assets/Sms/Rematch/
  WindRibbon.gdshader NEW, GhostLightning.gdshader, IceBlock.gdshader, SmsGhostLightning.cs, SmsGhostWind.cs, SmsIceBlock.cs, SmsFireballs.cs, SmsBombBoo.cs) on the 169 commit
  ("SMS 169: the King Boo REMATCH, part 13 -- GHOST WIND, the fling"; assets/Sms/Rematch/
  SmsGhostWind.cs, GhostWind.gdshader NEW, SmsKingBooRematch.cs) on the 168 commit ("SMS 168: the King Boo REMATCH, part 12 -- FIREBALLS, the burn"; assets/Sms/Rematch/
  SmsFireballs.cs NEW, SmsKingBooRematch.cs) on the 167 commit ("SMS 167: the King Boo REMATCH, part 11 -- GHOST BREATH, the freeze"; assets/Sms/Rematch/
  SmsGhostBreath.cs, SmsIceBlock.cs, IceBlock.gdshader NEW, SmsKingBooRematch.cs; assets/Mario.cs (SmsFreezeFor, the frost), assets/Sms/Fx/SmsBodyOverlay.cs
  (SteadyFog)) on the 166 commit ("SMS 166: the King Boo REMATCH, part 10 -- GHOST LIGHTNING"; assets/Sms/Rematch/ SmsGhostLightning.cs,
  GhostLightning.gdshader NEW, SmsKingBooRematch.cs) on the 165b commit ("SMS 165b: the King Boo REMATCH, part 9b -- the Boolossus faces its dive, the Boos are the port's own, the
  crown SHINES"; assets/Sms/Rematch/ SmsBoolossus.cs, SmsKingBooRematch.cs, GhostBoo.gdshader, Boolossus.tscn; assets/Sms/Enemy/BossTelesa/BossTelesaToon.gdshaderinc;
  models/Sms/Rematch/ boo.glb + boo_teresa.png REMOVED) on the 165 commit ("SMS 165: the King Boo REMATCH, part 9 -- three Boolossi DIVE in, with effects; the Boos white; the crown
  GLOWS the attack's colour"; assets/Sms/Rematch/ SmsBoolossus.cs, SmsKingBooRematch.cs; assets/Sms/Enemy/BossTelesa/BossTelesaToon.gdshaderinc two new uniforms) on the 164 commit
  ("SMS 164: the King Boo REMATCH, part 8 -- the BOOLOSSUS SMASH"; assets/Sms/Rematch/ SmsBoolossus.cs,
  Boolossus.tscn, GhostBoo.gdshader NEW, SmsKingBooRematch.cs; models/Sms/Rematch/ boolossus.glb + boo.glb and their textures NEW) on the 163 commit ("SMS 163: the King Boo REMATCH, part 7 -- the Lick's tongue is the whip itself, a bit longer";
  assets/Sms/Rematch/ SmsTongueStretch.cs NEW, SmsTongueAim.cs REMOVED, SmsKingBooRematch.cs) on the 162 commit ("SMS 162: the King Boo REMATCH, part 6 -- the teleports and the Lick"; assets/Sms/Rematch/
  SmsTongueAim.cs NEW, SmsKingBooRematch.cs, SmsArenaFloor.cs) on the 161 commit ("SMS 161: the King Boo REMATCH, part 5 -- the fist on the tongue"; assets/Mario.cs the hand poses and the grip,
  assets/Sms/Rematch/ SmsBombBoo.cs, SmsLassoArm.cs) on the 160 commit ("SMS 160: the King Boo REMATCH, part 4 -- the Bomb Boo floats as Sunshine's Boos do"; assets/Mario.cs the
  arm's lean, assets/Sms/Rematch/ SmsBombBoo.cs, SmsBombBooSpawner.cs, SmsLassoArm.cs, BombBoo.tscn) on the 159 commit ("SMS 159: the King Boo REMATCH, part 3 -- the Bomb Boo by the tongue"; assets/Mario.cs the lasso arm hook-up,
  the throw removed; assets/Sms/Rematch/ SmsLassoArm.cs (new), SmsBombBoo.cs, BombBoo.gdshader, BombBoo.tscn, SmsKingBooRematch.cs (the hit cylinder),
  SmsRematchIntro.cs (BooHeight 4), RematchArena.tscn) on the 158 commit ("SMS 158: the King Boo REMATCH, part 2 -- the Bomb Boos and the spin"; assets/Mario.cs the spin and the carry,
  assets/Sms/Rematch/ SmsBombBoo.cs, SmsBombBooSpawner.cs, BombBoo.gdshader, BombBoo.tscn, SmsKingBooRematch.cs (the hit points), RematchArena.tscn (the
  supply), assets/Sms/Sound/SmsActorSound.cs, models/Sms/Rematch/ bomb_boo.glb + bomb_boo_tongue.glb and their PNGs / .import) on the 157 commit
  ("SMS 157: the King Boo REMATCH, part 1 -- the transition"; assets/Sms/Rematch/
  SmsRematchExit.cs, SmsFloorShatter.cs, FloorShatter.gdshader, SmsRematchIntro.cs (+ SmsRematchCard), SmsKingBooRematch.cs, KingBooRematch.tscn,
  SmsArenaFloor.cs, RematchArena.tscn and their .uid; assets/Sms/Enemy/BossTelesa/KingBooArena.tscn (RematchExit + FloorShatter children,
  Rematch on), assets/Sms/Enemy/SmsBossTelesa.cs (the knob, StartRematch), assets/GameData.cs (RematchFrom), assets/Sms/Sound/SmsActorSound.cs
  (the kingboo actor), SmsMarioSoundTable.cs (+ MSD_SE_OBJ_GLASS_BREAK), assets/Sms/Sound/Object/MSD_SE_OBJ_GLASS_BREAK.wav (+ .import),
  models/Sms/Rematch/ polta_planet.glb + polta_parts.glb, their extracted PNGs and .import files) on `feature/sounds` `7df58ad`; the branch tip is
  this file's own commit on top of them; 160 commits past `feature/sounds`, 182 since `main`. ~9 MB of new files (197's 2.8 MB mp3; 208 / 208b's five 34 KB sky textures; 211's twelve WAVs, 1 MB; 213's two jingles, 1.6 MB; 215's eaten sound, 34 KB; 220's five Shine sounds, 1.8 MB). The range carries none of Jaysonn's
  live edits (assets/Level.tscn and the rest). PLAYTESTED BY JAYSONN through 229 (2026-11-17: "so far play testing feels good", "ok lets update
  timetopush!"): push the branch AND open its PR -- the rematch is done to the design doc (the HP faces were scrapped; the way out of the arena
  after the Shine is a later pass of its own, like a normal level's). #17's sound commits are in this branch's history and come with the PR (no
  PR of their own: #17's UPDATE). ONE merge path only: this is a PR, so do NOT also merge it into local `main` (Kiro has not). It can be merged
  once it is open (squash or merge commit, his call; `main` then needs `git merge --ff-only origin/main` here -- this repo's `pull.rebase` refuses
  a pull over his unstaged work).
- To try 229: play the fight and watch the Bomb Boos near the edge -- they float no nearer the stone's edge than 3 m now (Rim Margin on the BombBoo scene's root; Keep Off The Rim off = the old free wander), a Boo you drop at the edge or a dud that lands there heads back in at once, and King Boo's spat Boos come down no nearer than 2 m to the edge. Nothing else about them changed. Press the editor's Build first: the C# changed.
- To try 228: run, spin the stick and jump, and hold the stick off to one side through the long flight -- he comes down facing the way he is moving (before, the takeoff direction, 30-45 degrees off it, until the landing clip ended). Landing Keeps Drift on Mario: off = the game's own landing, where the sideways drift is dropped and he slides along his face. Press the editor's Build first: the C# changed.
- To try 227: play the fight for a few minutes and watch for hitches -- the port now makes less than half the garbage it did (the collector's pauses come less than half as often; the ones through his death are still held off by 226). Nothing should look or feel different: the rays, the inputs and the clips are the same, only made once. Press the editor's Build first: the C# changed.
- To try 226: beat King Boo and watch the whole run from the last hit to the card save -- no hitch at his death's start or as the card opens (the .NET collector is held off from the fatal hit: Gc Quiet Through Death on KingBoo, 64 MB; the one pause it takes comes under the last blast). Press the editor's Build first: the C# changed.
- To try 225: grab the Shine -- the landing no longer hitches (the SHINE! letters are built when the console loads: Shine Letters Ahead on the ConsoleStr layer). Then listen to the boss and mid-boss themes and the miss and game-over jingles: their notes' tails now decay the game's way instead of ducking out and swelling back into a cut. Press the editor's Build first: the C# changed.
- To try 224: beat King Boo and watch the cut to the wide shot -- the Shine's appearance no longer hitches (the Shine is built when the fight loads, Shine Made Ahead on KingBoo; off = built on that frame as before). Press the editor's Build first: the C# changed.
- To try 223: beat King Boo -- his death now plays out in full on the gameplay camera (the flash, the afterglow, the ring, the puffs and stars, about 2.5 s) and only then the Shine comes out where he burst and the camera cuts to the wide shot (Shine After Death Fx on KingBoo; Shine Delay Seconds 0.5 is the pause after his effects). Then grab the Shine and listen: the chime at the touch rings out instead of stuttering, and the two fanfares (the Shine's appear, the get) have their notes decaying naturally. Press the editor's Build first: the C# changed.
- To try 222: beat King Boo -- the camera now CUTS to the wide shot when he bursts and cuts back when the Shine settles (no pan; Death Cam Pan Seconds / Back Seconds on KingBoo above 0 bring the pan back). The line down the arena floor is gone (the stones run unbroken round the ring). Through the Shine's cutscene the counters, lives and life meter leave the screen and come back after, as in the game; grab the Shine in the air or on the floor and the shine counter comes in with the count going up mid-cutscene. Press the editor's Build first: the C# changed.
- To try 221: beat King Boo -- when he bursts, the camera pulls back and up to a wide shot of the whole arena and holds it while his Shine comes out where he burst and jumps to the middle; when the Shine settles, the camera glides back behind Mario. The Shine's hum now loops smoothly (no dip once a second). Press the editor's Build first: the C# changed. The wide shot's knobs are on KingBoo in the Inspector: Death Cam Wide (off = the old Shine camera), Death Cam Distance / Height / Look Up / Fov, Death Cam Pan Seconds / Back Seconds.
- To try 220: beat King Boo -- when his Shine comes out you hear the game's chime and its Shine-appears fanfare, the Shine hums softly while it floats (louder as you walk up), and when you grab it a sparkly chime, then the game's Shine Get fanfare with every other sound quiet but Mario's voice; any music playing stops at the grab, as in the game. It is every Shine's now, not only the rematch's. Press the editor's Build first: the C# changed. (The test drive's save dialog saved the rematch Shine into your save file: it may show as already got -- ask Kiro to take it out.)
- To try 219: start the King Boo fight and watch the sky's colour flashes (the grey, the green, the lavender) -- the lit clouds no longer stop at a straight line across the sky (nor at a seam straight up it in the +Z direction); they fade out softly. The knob is on the sky's material (RematchSky.tres, Knobs): flash_soften, 0 = the game's hard lines, 1 = soft, more = wider. Only the shader changed: no Build needed.
- To try 218: start the King Boo fight and look at the sky -- the faint streaks are the game's magenta now (a soft pink-violet), not the dark red-pink they were; where two curves cross they still go cyan. Only the shader changed: no Build needed.
- To try 217: beat King Boo -- from his burst the sky fades over 6 s from the fire to a calm starry night with the northern lights (green, teal and violet curtains low over the rim), the arena's light going a cool blue, the thunder and the embers gone; and the Shine that flies out is two and a half times the size it was, centred where it was (King Boo's ShineScale; the sky's knobs are on RematchSky.tres: peace_*, aurora_*, star_gain). Press the editor's Build first: the C# changed.
- To try 216: lose a life on the floor (run out of HP) -- the camera stops and slowly zooms in on Mario as he lies there; through the miss (TOO BAD! and its jingle) every sound effect goes quiet but Mario's voice, as in the game; and King Boo's twinkle stars no longer show faint squares against the sky (his star material's alpha_floor / edge_fade; 0 = the game's). Press the editor's Build first: the C# changed.
- To try 215: wait for King Boo's smash (his crown glows white) -- the game's Sleepy Boo now falls on you instead of the Boolossus, snoring as it comes down, and its landing plays the Sleepy Boo's own death sound from the game (the one Yoshi's eat makes); its fifteen Boos burst out as before (on the smash's node: BodySize = its size in metres; GhostLook on with the Model child set back to boolossus.glb brings the Boolossus back). Press the editor's Build first: the C# changed.
- To try 214: fall off the edge -- the iris now closes with a hard black edge, as the game draws it (three faint rings just inside it); hit King Boo until he splits -- through the cutscene's laugh and after it every one of him keeps up his idle, never frozen mid-air; and when he smashes with Boolossus, a white circle pulses where it will land with its round shadow growing inside (the Boolossus's LandingMark / MarkColor knobs; ShadowOpacity / ShadowScale the shadow). Press the editor's Build first: the C# changed.
- To try 213: lose a life (fall off, or run out of HP) -- the boss music cuts out at once and the game's own TOO BAD! jingle plays; the stage moves only once it has finished (4.5 s); with no lives left the game-over jingle plays instead (8.2 s) and the stage starts over from the card with the lives reset (Mario's MissJingle / GameOverJingle slots, filled with the game's renders; SmsActorSound.MusicDb sets the music's level). Press the editor's Build first: Mario.cs and the sound scripts changed.
- To try 212: walk (or get knocked) off the arena's edge -- Mario cries out, the camera stops and watches him fall away into the void, the iris closes 2 s on, TOO BAD!, a life down, and the fight starts again from the fall-in (no card, no cutscene) (the arena's DeathPlane node at y -25: drag it up or down to move the line; Active off = he falls for ever, as before). Press the editor's Build first: Mario.cs and the camera changed.
- To try 211b: the thunder now waits for the fight -- none through the card, the fall and the intro cutscene; it rolls from the sky's first purple flash after King Boo is up for the fight (ThunderAfterIntro on the arena's ArenaLighting node; off = from the first frame, as 211).
- To try 211: a Bomb Boo's blast and a lava ball's landing leave a black scorch on the stone -- orange and glowing for a moment, black for 8 s, fading over 10 more (Darkness / BombSize / LavaSize / HoldSeconds / FadeSeconds / GlowEnergy on the arena's ScorchMarks node; Scorch off = none); and the attacks now have Jaysonn's sounds -- the lava balls' FireStart as each pops out of the crown and FireEnd as each disappears, Boolossus's bound and pon, the breath's blow, the ice's freeze / unfreeze, the lightning's crackles and zap, the wind's gust, thunder 0.4 s after the sky's purple flash (levels in dB in SmsActorSound.RematchLevels; each attack's *Se knob names its sound, empty = none; ThunderSe on the ArenaLighting node) -- and the game's own sounds on those attacks play at last (they never had).
- To try 210: from the second split, glowing embers rise all round the arena's edge and out over the void (Count / Size / Brightness / RimIn / RimOut on the arena's ArenaEmbers node; Embers off = none); and the arena now blooms -- King Boo's white halos, the fire and the lava balls read hotter, the streaks' cyan crossings glow (the Environment's Glow section: glow_intensity 0.7 = how much, glow_hdr_threshold 0.6 = how bright before it blooms; glow_enabled off = before).
- To try 209: the arena's lights follow the sky -- every few seconds the sky's flash (purple, grey, blue, green, lavender, dark red) lights the floor and King Boo on the same frame as the clouds; from the second split the floor, the fog and the sun go warm with the fire (FlashStrength / FlashOnSun / the Final colours on the arena's ArenaLighting node; FollowSky off = the fixed light of before).
- To try 208c: the final phase's fire now reaches down the sky -- the fight camera looking down over the fence sees flame to the bottom of the frame instead of flat red (final_stretch 2.5 / final_drop 0 / final_flame_tiles 1.6 on RematchSky.tres; 1 / 0 / 1 = the game's own roof sky).
- To try 208b: the final phase's sky -- at the second split the sky fades over 2.5 s to the roof fight's fire: a wall of orange-yellow flame round the horizon under dark red, red-to-yellow streaks, a red glow below the fence (FinalSky / FinalSkyAtSplit / FinalSkyBlendSeconds on KingBoo; final_blend, final_brightness, final_fire_gain on RematchSky.tres).
- To try 208: the arena's sky -- Luigi's Mansion's boss-arena sky from the disc: black-violet, magenta streaks drifting low with cyan where they cross, the clouds flashing purple / grey / blue / green / dark red on a 10 s cycle (knobs on assets/Sms/Rematch/RematchSky.tres: brightness, streak_gain, flash_amount, base_lift, speed).
- To try 207: Mario's look-about in the intro -- a glance left, a sweep right and back in 2.0 s after the game's own idle, no shrug, no head cock, at its own pace (the curves in tools/anim/lookabout_curves.py: edit, run, re-bake; LookKeepsPace / LaughMinScale on the intro node).
- To try 206b: the Bomb Boos in the game's Boo body but the Bomb Boo's colours -- black, yellow-eyed (SunshineBombColours / SunshineBodyColor / SunshineEyeColor / SunshineIrisColor on BombBoo.tscn's root; off = the white blue-eyed Boo).
- To try 206: the Bomb Boos as the game's own Boos -- white, blue-eyed, their own tongue wagging, the lasso tongue red when carried, the flinch when shocked (Look / SunshineTint / SunshineOwnTongue on BombBoo.tscn's root; Look = Galaxy for the black Boo).
- To try 205: a fireball going out -- the little puff of smoke, the flash and the embers where it was (EndPuff / EndPuffs / EndSparks / EndPuffScale on the fireballs maker's knobs).
- To try 204: the fireballs' fire TRAILING behind them as they fly and roll (Trail / TrailPuffs / TrailSeconds / TrailSize / TrailBack / TrailStreaks / TrailLean on the fireballs maker's knobs, "The look").
- To try 203: the phase 2 / 3 cutscene with an attack running -- everything gone under the cut (SplitClearsAttacks on KingBoo); a spin jump onto a floating Bomb Boo -- grabbed in the air (SpinJumpGrabs / SpinJumpGrabBelow on Mario); the volley with no dud (VolleyDud on KingBoo brings it back).
- To try 202: the Bomb Boo's puffs BLACK (a hit while swinging one, a vanish, the volley's blasts; PuffSmokePuffs / PuffSmokeScale / PuffFlash / PuffFlashColor on BombBoo.tscn's root) and its ONE tongue (HideBodyTongue).
- To try 201: the AMBUSH from below (his pool hunting you, the stop and flare, the eruption's ring -- jump at the break; AmbushHuntSeconds / AmbushHuntSpeed / AmbushTellSeconds / AmbushRingRadius / AmbushPoolSize on KingBoo) and the BOO CIRCLE (SMW's ring, one gap, turning and closing -- out through the gap; CircleCount / CircleGap / CircleRadius / CircleMinRadius / CircleHeight / CircleRevSeconds / CircleHoldSeconds / CircleSeconds / CircleDriftSpeed on KingBoo).
- To try 200b: the Bomb Boos blinking RED before they blow -- the spat ones through their fall, the dud in its last 2.5 s (FuseFlashColor / FuseFlashMax / FuseEmit / FuseBlinkHzFrom / FuseBlinkHzTo / DudWarnSeconds on BombBoo.tscn's root).
- To try 200: the Bomb Boo VOLLEY -- the yellow crown, three Boos spat on lobs with their marks on the floor, two blasts (jump them), the last a dud to grab before its 6 s fuse runs out (VolleyOn / VolleyWeight / VolleyCount / VolleyLobHeight / VolleyScatter / VolleyBlastRadius / VolleyDud / VolleyDudSeconds on KingBoo; CopyVolley to let the fakes spit too).
- To try 199: the breath leaving his whole mouth, its freeze as wide as the mist (BreathMouthWidth / BreathMouthHeight / BreathHitLanes on KingBoo: 4.5 x 1.4 m, 3 lanes; 0.7 x 0.7, 1 = the old point); Mario's look-about with the arms resting (the shrug keys out of assets/Mario/Anims/ma_lookabout.pose.json, re-bake with tools/anim/bake_anim.gd).
- To try 198: King Boo's clips melting into each other (ClipBlendSeconds on KingBoo: 0 = the old hard cut, 0.25 = one body moving, over 0.4 = mushy).
- To try 197: Antasma Final Battle from the intro's first frame, the fight landing 7.5 s in (FightCueSeconds / LookMinScale / AfterSinkSeconds on the intro node;
  BossMusicCue / BossMusicFile / BossMusicLoopOffset / BossMusicStreamDb on KingBoo); the look-about and the laugh run quicker (2 s each) to make the cue.
- To try 196: the fakes teleporting about and never nearer than 11 m to each other or to him (CopyRepositionChance / CopyGapScale / SpotMinFromBoos on KingBoo);
  the music coming in on the cut to Mario's battle stance (BossMusicAtIntro on KingBoo; off = at the fight).
- To try 195: the boss music from the fight's start (BossMusicOn / BossMusicDb / BossMusicAtIntro on KingBoo) over the effects at -4 dB (SfxDb); the wind --
  five tornados on a ring circling the arena, in to the middle by 3.8 s and out by 7 (TornadoCount / TornadoPattern / TornadoInnerRadius / TornadoSpeed /
  TornadoSeconds on KingBoo).
- To try 194: the split cutscenes -- each of him gathers out of white mist and sparkles into his centre, flickering, a ring under him, then pops solid
  (SplitGatherPuffs / SplitGatherStars / SplitGatherRadius / SplitFadeSeconds / SplitFlicker / SplitRing on KingBoo); the intro laugh's climb ends at 1.15
  (IntroLaughPitchFrom / IntroLaughPitchTo on KingBoo).
- To try 193: jump a thunder wall where it stands and wait outside it -- no shock as the walls meet; take a hit holding a Bomb Boo -- it pops and is gone
  (SmsBombBurstsOnHit on Mario); the split cutscene's white mist (SplitMistColor on KingBoo); the shorter intro laugh (IntroLaughSeconds 3).
- To try 192: the Boolossus smash -- bigger, its ring of Boos out 3 s and slowing (ModelScale, WaveSeconds, WaveReach on Boolossus.tscn); the thunder
  walls and the freeze breath with ShowHitBoxes ON (KingBoo) to see the red hit volumes against the bolts and the mist and your own green cylinder
  (WallHitTopK, BreathFloorDrag on KingBoo); the intro laugh climbing from low (IntroLaughPitchFrom / IntroLaughPitchTo on KingBoo).
- To try 191: hit a FAKE with a Bomb Boo -- it laughs it off (no tumble), phases see-through and teleports away (CopyMock, CopyMockSeconds,
  CopyMockOpacity, CopyMockFlickerHz on KingBoo); the real one still tumbles -- the contrast tells them apart.
- To try 190: the split cutscenes -- the line of him sits higher in the frame (SplitCamLookUp on KingBoo: lower = higher in the frame), and the six-hit
  split shows three of him, the first copy teleporting into the line.
- To try 189: the split cutscene with no text over it (SplitLine2 / SplitLine3 on KingBoo: text there brings the card back).
- To try 188: hit him three times -- the world freezes, the camera cuts to a line of him across the arena, his copy comes in beside him out of violet
  mist, they laugh together under "Now there are TWO of me!" (and "THREE of me!" at six hits), then all of them attack; A skips it. Knobs in The split's
  cutscene group on KingBoo: SplitCutscene (off = the old quiet split), SplitLineSpacing, SplitCopyEvery, SplitHoldSeconds, SplitCam* , SplitLine2 / SplitLine3,
  SplitMistColor / SplitMistPuffs, SplitLaughSpread, SplitSkipAllowed.
- To try 187: the freeze breath -- only the thick white core of the cold freezes you now (the thin mist and its fading tail are harmless), a held jump
  clears it, and while you are frozen the ICE takes any hit for you (it shatters, nothing else happens). Knobs: HitLifeK / HitRadiusK / HitFollowsFade /
  HitHeight on the breath (BreathHitHeight on KingBoo), SmsFrostShields on Mario.
- To try 186: the intro's laugh is the casino's hee-hee-hee chuckle, cut as he dives (IntroLaughSe / IntroLaughPitch / IntroLaughStopsAtDive /
  IntroLaughSeconds on KingBoo; IntroLaughPitch 1.2 for higher still).
- To try 185: King Boo attacks about twice as often (GapAtStart / GapAtLastHit / GapJitter / FirstAttackDelay / RepositionChance on KingBoo), and the
  thunder walls are long and low -- HOLD a jump as the wall reaches you to clear it (a short tap does not); running round the end is a 9 m dash
  (WallLength / WallHeight on KingBoo).
- To try 184: the Bomb Boos now come up in RANDOM places about the arena -- each black spot somewhere on the stone 4-14 m from you, never the same
  place twice running (SpawnMode / ArenaRadius / EdgeMargin / MinFromMario / MaxFromMario / MinFromLast / MinFromBoos on BombBooSpawner; SpawnMode Centre
  puts them back at the middle).
- To try 183: BEAT him -- on the last hit King Boo tumbles, blinking white with mist and stars pouring off him and his crown flaring, sinks low and BURSTS in
  a flash, a shock ring and a cloud of mist and stars; a second later the SHINE materialises in a golden flash where he burst and flies down onto its spot at
  the arena's middle while the world freezes and a camera watches (6 s); grab it as any Shine (the save card, then control back -- ShineExitLevel /
  ShineExitScene on KingBoo send it somewhere). Knobs in The end group on KingBoo: DeathSink, DeathFlashSize, DeathPuffs / DeathStars, ShineDelaySeconds,
  ShineDemoTicks, ShineCamOffset / ShineCamFov, ShineOverFloor, ShineSpawnPoint. The Bomb Boos are gone from the last hit on.
- To try 182: watch the cutscene's APPEAR -- after the glow's tell King Boo comes up out of the floor in one clean arc (the appear clip's own leap, no
  stall on the stone) and settles at his hover height; twinkle stars shed off his body only while he shoots up, nothing at his crown; the crown's own sparkles
  are gone from the tells and the teleport-ins (IntroAppearClipSpeed / RiseLeapDamp, CrownSparkles 0 / TeleportInStars off, Twinkle for the ambient stars on
  KingBoo).
- To try 181: watch King Boo COME UP -- in the cutscene and at the fight's start a pool of light pulses on the stone with wisps rising from it, then he bursts
  through in a flash, a shock ring and a splash of ghost mist with twinkle stars streaming up round him; every teleport now ends in a poof of mist and stars and
  begins with a flash where he comes in (RiseFx / RiseTellSeconds / RiseGlowSize / RiseWisps / RiseSparkles, TeleportFx / TeleportPuffs / TeleportSparkles /
  TeleportFxScale, FlashBoost on KingBoo).
- To try 180: FACE a Bomb Boo and watch it WORRY -- it shakes its head, leans back from you, cowers and breathes fast, sweat beads fly off its head (the
  Worry* / Sweat* knobs in The shock group on BombBoo.tscn's root); the spot's eyes are bigger (the supply's EyeSize). In the cutscene King Boo now LEAPS and
  DIVES through the floor with his hide clip -- a burst of twinkle stars at the top, then a flash on the stone, a ring of ghost mist, a shock ring and a camera
  kick as he goes under (IntroDiveClip / IntroDiveClipSpeed, DiveSparkles, DiveFlashSize, DiveRingPuffs, DiveSplashPuffs, DiveShockRing, DiveShake, DiveColor on KingBoo).
- To try 179: watch a Bomb Boo come: a BLACK pool on the floor, two yellow EYES in it looking at you, then the Boo up THROUGH the floor in black smoke; its
  tongue moves as it floats; FACE it (within 12 m) and it is SHOCKED -- it stops dead, startles, trembles, stares back at you with burning eyes and DROOLS -- until
  you look away for half a second (or spin to grab it; walking into it still hurts). Knobs on BombBoo.tscn's root (The tongue / The shock / The rise groups:
  LookShock, LookConeDeg, LookRange, ShockTremble, DroolRadius, DroolEvery, RiseFromUnder, SpawnDepth, RiseSmokePuffs...) and the supply's Fog group (FogMist,
  FogDisc, EyesGlow, EyeColor, EyeSize...). The rim glow and the burst puff are grey-black now (GlowColor, BurstColor), not violet.
- To try 178: swing a Bomb Boo and JUMP under King Boo -- the swing rises with the jump and bursts on his underside (from the ground it cannot reach him;
  JumpSwingLift on the Bomb Boo); after every hit he teleports away (TeleportAfterHit); a hit on a COPY tumbles and teleports it too but does no damage; by the
  sixth hit there are THREE of him -- the fake's crown is silver (CopyTell Crown; or Teeth: no fangs; CopyCrownColor); the frost breath's hit now flashes,
  splashes and climbs him in ice with a camera shake (the ice's SplashPuffs / RingPuffs / HitShards / FlashSize / PopSeconds, the breath's HitShake).
- To try 177: hit King Boo and he TUMBLES head over heels (HurtClip / HurtClipSpeed on him); after the third hit a COPY of him splits off and throws
  lava balls and breath from far away, slower and rarer -- hit the copy and it only laughs and fades; hit the real one for the damage. He has 9 hit points now
  (HitPoints); CopyEveryHits, MaxCopies, CopyMinFromMario, CopyGapScale, CopySpeedScale on him. NOTE: since 173 he hovers too high for the swing to reach
  from the ground -- a design call to make (he dips during attacks? the swing rises with a jump?).
- To try 176b: in the stance Mario's whole body is turned three-quarters to the camera (hips and legs too) and he KEEPS the stance after the fight starts
  until you touch the stick or a button (StanceHoldUntilInput on the intro node); his head finds King Boo over the turned chest.
- To try 176: the look-around is a real clip (ma_lookabout: a glance left, right, then up with a shrug) and on the cut to the stand-off Mario drops into
  a BATTLE STANCE (fists up, crouched, bouncing) until the fight starts. Open assets/Player.tscn -> AnimationPlayer -> the Animation panel to scrub or edit
  the three clips, or edit assets/Mario/Anims/<name>.pose.json and re-bake (README.md there). Intro knobs: LookClip, LookClipSpeed, StanceInClip, StanceClip.
- To try 175: in the cutscene Mario now looks about with his HEAD (left, right, ahead and a little up, his shoulders following, his body still), then
  looks up at King Boo as he surfaces 12 m away (he was 7). Knobs on the intro node: LookTurnDeg, LookUpDeg, LookChestShare, LookSeconds, SurfaceDistance,
  SurfaceCamSide / SurfaceCamHeight / SurfaceCamAim.
- To try 174: the intro is a CUTSCENE now -- land, hear the laugh, Mario looks about for it, King Boo surfaces in front of him, laughs, sinks, then rises
  at his spot for the fight (A skips it). Knobs on the intro node (Cutscene, CutsceneDelay, LookSeconds, LookTurnDeg, SurfaceDistance, SurfaceCamSide /
  SurfaceCamHeight, AfterSinkSeconds) and on King Boo (IntroRiseSeconds, IntroLaughSeconds, IntroSinkSeconds, IntroSinkDepth, SpawnRiseSeconds).
- To try 173: King Boo floats 4.5 m HIGHER (his tongue no longer goes into the floor; HoverLift on him), the breath still comes down to the floor 10 m
  out (BreathAimFloorAt), the lava balls are 3 m across (FireballSize), and the electricity is YELLOW and busier (SmsGhostLightning: BoltColorA / BoltColorB,
  JitterHz, Wave, ZapChance).
- To try 172: the lightning walls now CRACKLE -- bolt streaks, arcs, flashes and sparks all over them, sparks off the posts, bolts along the floor, and
  horizontal ZAPS jumping the gap as the walls close; the fireballs are now LAVA BALLS popping out of the CROWN three at a time (three pops), arcing
  down at the orange rings on the floor (one lands near you each pop), bouncing and rolling on; the breath is a THICK bank of cold. Knobs: SmsGhostLightning
  (WallStreaks, WallArcs, WallSparks, GapZaps, ZapBrightness, MeetSparks), King Boo (FireballPops, FireballPerPop, FireballPopSeconds, FireballLobHeight,
  FireballScatter, FireballAimedWithin, FireballRollSpeed), SmsFireballs (Bounce, LandingMarks, Drips, Smoke, HeatAtEnd), SmsGhostBreath (Puffs, PuffAlpha,
  CorePuffs, CoreAlpha, HitRadiusK).
- To try 171: get caught by the Ghost Breath -- the frozen Mario now holds the SHOCK pose (arms out, knees bent, eyes squeezed shut) inside the
  ice instead of whatever he was doing; and take any hit: the hurt eyes now show through the pink damage fog (they were hidden under it before).
  Knobs on Mario: SmsFrostPoseClip ("ma_shock"; "" = his own pose), SmsFrostPoseAt (0.5 = halfway through the clip).
- To try 170 (the effects pass): the lightning walls now carry HORIZONTAL arcs jumping between glowing posts, light the floor and send a
  shockwave ring out when they meet; the tornados are a GUST -- white ribbons of wind spiralling up with debris whirling in them; the ice on a frozen
  Mario is a jagged CHUNK (a different one each time); a fireball that gets you BURSTS; and a Bomb Boo swung into King Boo EXPLODES with the Bob-omb's
  blast. Knobs: SmsGhostLightning (Arcs, PostWidth, LightEnergy, MeetRing, MeetShake), SmsGhostWind (Ribbons, RibbonWidth, RibbonTurns, Debris),
  SmsIceBlock (Jag, Spikes, SpikeOut), SmsBombBoo (ExplodeOnKingBoo, ExplosionScale, ExplosionShake, ExplodeSe).
- To try 169: when the crown goes PINK, two ghost TORNADOS rise out of the floor 18 m out and come at you at 6 m/s for 7 s, turning slowly --
  run or sidestep and they never catch you; one that reaches you FLINGS you (Sunshine's Cataquack throw: 10 m up, 18 m across, the flail and the
  bounces, no HP). They outlive his move, so two winds' worth can be up at once. All six attacks are in and picked by weight. Knobs: KingBoo's
  "The fight (169)" (TornadoCount / Speed / TurnDegPerSec / Seconds / Height / Radius, FlingSpeed / FlingUpK / FlingGraceSeconds, WindCrownColor) and WindWeight.
- To try 168: when the crown goes RED-ORANGE (a flaming crown), he spits a volley of eight FIREBALLS out of his mouth that drop to the floor
  and roll across the arena at 14 m/s in a 140-deg fan, bouncing off the wall and shrinking away over 5 s. A ball reaching you is the BURN (Sunshine's
  fire hit: 1 HP, the launch, the cloud); a full JUMP clears one (feet over 1.6 m), a hop does not; watch the ones coming back off the wall. Knobs:
  KingBoo's "The fight (168)" (FireballCount / VolleySeconds / SpreadDeg / Speed / Size / Seconds / HitHeight, FireballCrownColor) and FireballWeight.
- To try 167: when the crown goes FROSTY BLUE and he turns his head to one side, a stream of cold mist comes out of his mouth and sweeps across
  the arena along the floor (1.6 s, 150 deg, 22 m/s, from your left or your right at random). JUMP as it reaches you: feet 1.2 m up are over it. Caught,
  you are FROZEN a second in a block of ice (no HP) -- the pose held, a blue frost on the body -- and it shatters as you thaw; a lick through the ice
  is the knockdown as ever. Knobs: KingBoo's "The fight (167)" (SweepDeg / SweepSeconds / SweepWay, BreathSpeed, PuffSeconds, PuffSizeStart / End,
  BreathHitHeight, FreezeSeconds, BreathCrownColor, OneFreezePerAttack), BreathWeight, SmsIceBlock's look, SmsBodyOverlay.SteadyFogFactor.
- To try 166: when the crown goes RED, two walls of lightning appear 12 m apart either side of you, dim for half a second, then close at 8 m/s
  each -- run out of the open ends (along the walls); a wall reaching you is the shock (ma_shock, 1 HP), once per attack. Three squeezes, the axis
  turning 60 deg, each re-centred on you. Knobs: KingBoo's "The fight (166)" (WallLength / WallHeight / WallGap / WallSpeed, SqueezeCount, WarnSeconds,
  SqueezePause, SqueezeTurnDeg, OneShockPerAttack, LightningCrownColor) and the attack weights (LickWeight / SmashWeight / LightningWeight).
- To try 165b: the SMASH once more: the crown flashes white-hot and then SHINES -- a sliding sheen, sparkles rising out of it, pulsing faster
  toward the launch; the Boolossi dive face first; the Boos ringing out are the hotel Boos (sleepy eyes, fangs, red tongues). Knobs: KingBoo's
  CrownShine / CrownFlashSeconds / CrownPulseQuicken / CrownSparkles / CrownFxLift (CrownGlowSize for a halo after all); Boolossus.tscn's Opacity,
  SmokeCount / SmokeColor, WaveBooScale.
- To try 165: the SMASH again: his crown now visibly GLOWS white through the tell (a light on the crown), then THREE Boolossi dive in half a second
  apart from three sides, each trailing wisps and aiming at where you stand -- their shadows show the targets, which lock 0.6 s before the landing: keep
  running and they miss. Each landing: a ring of light, smoke, a camera shake, then 15 WHITE Boos ringing outward (jump them) and vanishing in puffs.
  Knobs: KingBoo's "The fight (164)" (SmashCount, SmashStagger, SmashSpreadDeg, CrownGlowSize / Strength) and Boolossus.tscn ("The dive (165)":
  DiveHeight / DiveDistance / DiveStartSpeed / TrackSpeed / LockSeconds / DiveTilt; "The effects (165)": the trail, ring, smoke and puff colours,
  LandShake; "The wave": WaveBooOpacity, BooVertexColors).
- To try 164: half his attacks are now the SMASH: he stays put and laughs while his crown pulses white (0.6 s), then a Boolossus appears 16 m over
  you with its shadow on the floor, drifts after you for half a second and drops (1 s). Run out of the shadow; when it lands it bursts into 15 Boos
  that ring outward for a second -- jump the ring. Standing under it or in the ring is the plain hit. Knobs: KingBoo's "The fight (164)" (SmashChance,
  SmashTellSeconds, the crown colour) and Boolossus.tscn (ModelScale, DropHeight, HangSeconds, SmashGravity, WaveCount / WaveSpeed / WaveSeconds /
  WaveHeight, BooRadius, the shadow, the sounds).
- To try 163: the lick again: he fades in 8.7 m ahead and high, rears up with the tongue overhead, then the tongue whips across the front of him
  low, left to right, through where you stand -- the clip's own whirl, no re-aim; the tongue a bit longer everywhere (TongueStretch 1.15). Jump at the
  rear-up to clear it. Knobs: LickDistance / LickHeight (the tip skims the floor at 8.7 / 6.4), LickClipSpeed, LickHitFromFrame / LickHitToFrame,
  LickThickness, TongueStretch.
- To try 162: in the arena, 3 s after King Boo's laugh he vanishes and fades in 6 m in front of you, low; half a second of rearing up with the
  tongue overhead, then it snaps onto the floor where you stand -- jump at the tell to clear it, stand and it knocks you down (a point of HP). He
  hops off to another spot and comes again every 2-4 s, quicker per hit; swing a Boo into him while he waits or mid-lick (it breaks the lick off).
  Knobs: KingBooRematch.tscn's "The fight (162)" group (the gaps, the fades, LickDistance / LickHeight, the clip and its frames, the hit's radius and
  clear height, AttacksOn off = the old waiting boss).
- To try 161: grab a Boo: the arm is further out, the hand a closed FIST on the tongue, the tongue from the middle of the Boo's mouth into the
  middle of the fist, the Boo swinging wider (3.6 m). Knobs: Mario's LassoTilt / LassoForward / LassoIn, BombBoo.tscn's TongueOffset and OrbitRadius.
- To try 160: in the arena. The Boo out of the fog bobs and tilts like the hotel's Boos and wanders about the middle on its own -- it no longer
  comes to you; go to it, and mind it: a touch knocks you down like a Sunshine Boo's (jump over it and it cannot). X near it: the arm goes up and OUT
  beside his head now, the Boo swinging round over his head, no grey on it. Knobs: BombBoo.tscn's root, The float (WanderRadiusUnits, MarchSpeed /
  TurnSpeed, the FlyHeight / FlyAmplitude / FlyFrequency ranges, BaseLiftOverFloor, WaitTicks, TouchHurts off = harmless, SeeksMario on = it comes to
  you as before); Mario's SMS Rematch group (LassoTilt 1.7 / LassoForward 0.3 / LassoIn -0.4: the arm's lean, tangents of the angles).
- To try 159: in the arena. The Boo out of the fog is BLACK with yellow eyes and solid now, its pink tongue hanging. X near it: Mario's arm goes
  up and the Boo swings round him on its tongue, just over his head, leaning into the turn -- walk toward King Boo and let it swing into him: it
  bursts on him and he flips. X while holding does nothing; dive drops it; a spin jump (A out of a spin) speeds the swing. Knobs: BombBoo.tscn's root
  (OrbitRadius, OrbitOverHand, OrbitDegPerSec, OrbitLeanDeg, HoldSeconds; Lift / Ambient / Opacity / FaceGlow / Glow for the look), Mario's SMS Rematch
  group (LassoArm R / L, LassoTilt / LassoForward / LassoIn: the arm's lean), KingBoo (HitRadius / HitBelow / HitAbove), RematchIntro (BooHeight).
- To try 158: in the arena (through the casino, or F6 on assets/Sms/Rematch/RematchArena.tscn). A black foggy spot forms at the middle and a black
  Boo rises out of it and drifts your way, stopping short. X (the X key on a keyboard): Mario spins -- within reach the Boo is snatched and spins
  over his head. Jump: single jumps only; dive (B): he drops it and it drifts back. X again: he spins and THROWS it the way he faces (or the
  stick's way); aimed roughly at King Boo it curves onto him -- he flips, flashes, every Boo on the field pops, and in 6 s the next comes.
  Three hits and he goes down and vanishes (the fight's end is a later pass). Knobs: Mario's inspector, SMS Rematch (158): SpinSeconds,
  SpinTurns, BombGrabRadius, BombThrowSpeed, BombThrowAssistDeg (0 = pure aim)...; the arena's BombBooSpawner (SpawnMode Centre / Random,
  SpawnInterval, MaxOut); BombBoo.tscn's root (ModelScale, Opacity, DriftSpeed, HoverHeight, CarrySpin, ExpireSeconds); KingBoo (HitPoints).
- To try 157: King Boo's room; beat him (three hits). Where the Shine came: the table cracks from the centre out, holds a breath, and breaks
  under you -- the pieces fly off and fade -- you fall into the dark, black, the two lines, and you drop into Bouldergeist's arena with no FLUDD
  (R and X do nothing); King Boo appears, laughs, and waits -- hovering, glowing, turning to watch you. Nothing attacks yet. A skips the card.
  Knobs: KingBooArena.tscn's RematchExit (Enabled, FadeOutSeconds, TriggerDepth, ArenaScene) and its FloorShatter (CrackSeconds, HoldSeconds,
  ShardSeconds, CellAreaM2 the piece size, CrackColor / CrackGlow, Kick / Pop / Spin / Gravity, ShatterSound); RematchArena.tscn's RematchIntro (the
  two lines and their timing, FallHeight, BooHeight, FadeInSeconds, SkipAllowed, LabelSettings / FontSize), KingBoo (GhostOpacity, Glow, Twinkle,
  HoverBob, FaceMario, LaughSe), Arena (Brightness); BossTelesa's Rematch off = the game's Shine. To open the arena on its own: F6 on
  assets/Sms/Rematch/RematchArena.tscn (the same intro runs).
- PR: title "Feature: the King Boo rematch and the port's sounds (SMS passes 154-229)"; base `main`; head `feature/kingboo-rematch`; body =
  `pr157.md` at the project root (untracked -- delete it after the merge; plain ASCII, so the clipboard route cannot mojibake it as #16's did).
  Measured 2026-11-17: the body is 8.0 KB and urlencodes to 11.7 KB, so the compare URL can carry the TITLE only; the body goes by the
  clipboard (`Get-Content pr157.md -Raw | Set-Clipboard`) and is pasted into the form. The body's text, for the record (the file is the copy to use):
  > The King Boo REMATCH -- a second King Boo fight, after the casino, on Bouldergeist's planet from Super Mario Galaxy, designed in Jaysonn's `KINGBOO_REMATCH_gdoc.txt` and built pass by pass on `feature/kingboo-rematch` (SMS 157-229, 73 passes), each pass read off the game's data or the doc and logged in `PROJECT_LOG.md` with its headless drive green before the commit. The branch stacks on `feature/sounds` (SMS 154-156e, the port's first audio: its 22 commits are in this branch's history and come with this PR): 182 commits over `main` `90efb17`.
  >
  > **The sounds (SMS 154-156e, `feature/sounds`).** Mario's and FLUDD's sound effects from the game's own banks, fired the way the game fires them -- the footsteps and the pack's clanks off his animation tables, the jump voices by jump kind with the game's odds, the landing cutting the voice, the spray's and the hover's loops, the rocket and turbo launches (310 WAVs under `assets/Sms/Sound/Mario/`, baked from the disc's AudioRes by `tools/projectshine/sound154/sms_audio.py`, a port of the game's SE sequence player). Mario's sounds play flat (no distance falloff), a Sound Effects volume (the Sfx bus, -6 dB), the game's BOSS theme and intro chuckle for the casino fight. `SmsActorSound.cs`, `SmsMarioSoundTable.cs`, `Player.tscn`'s SmsSound child.
  >
  > **The way in and the arena (157, 174-176b, 197, 207-212, 217-219).** When the casino's King Boo is down (`Rematch` on `KingBooArena.tscn`; off = the game's Shine) the roulette table cracks like glass and breaks, and Mario falls through to the planet: `polta_planet.glb` (the Galaxy .dae with its vertex colours; the stone's rings re-mapped in 222), Luigi's Mansion's boss-arena sky ported from the disc with its flashes and streaks (`RematchSky`), lighting that follows the sky, embers and glow in the final phase, scorch marks under the blasts, a death plane (a fall is the game's fall death: the cry, the camera watching him fall away, the iris, TOO BAD!, a life down), and the intro cutscene -- a laugh from nowhere, Mario's look-about (a real clip), King Boo surfacing to laugh and dive, Mario dropping into a battle stance from Jaysonn's sprite refs; A skips it. The music is Jaysonn's Antasma Final Battle mp3 from the intro's first frame, the fight starting on its cue. After his defeat the sky fades to a starry night with the northern lights.
  >
  > **King Boo (`SmsKingBooRematch.cs`; the casino's btelesa model and clips).** Nine hit points. He hovers 8.5 m up and teleports between spots (the hide clip under a fade; every rise and teleport has its glow, flash, ring, mist and stars), attacking on a pace that climbs from a move every 2 s to every second, his CROWN SHINING the colour of the attack he is casting: the LICK (the roll clip's whip), the BOOLOSSUS SMASH (three dive in -- the game's Sleepy Boo -- and burst into rings of Boos), GHOST LIGHTNING (long low walls of bolts closing on Mario, crackling and zapping; a held jump clears them), GHOST BREATH (a cold stream along the floor; the dense cold FREEZES Mario inside a jagged ice block that doubles as a shield), LAVA BALLS out of the crown (rolling, trailing fire, bursting), GHOST WIND (five tornados on a ring circling in and back out; one flings him), the BOMB BOO VOLLEY (three spat on lobs, blinking red, blasting where they land), the AMBUSH from below and the BOO CIRCLE (SMW's ghost-house ring with one gap). His hurt is the casino tumble; every third hit he SPLITS off a copy (Luigi's Mansion 3) in a cutscene -- the world freezes, a camera cuts to a line of him, the copies gather out of mist and all laugh -- and the copies teleport about, keep apart and mock a hit on a fake. His DEFEAT: the down's tumble, blinking, mist and stars, the sink, then the BURST (flash, ring, poof, stars, a camera kick, an afterglow), all of it played out before the Shine comes where he burst and the camera CUTS to a wide shot of the arena (the game's Petey Shine camera), then back when the Shine settles.
  >
  > **The Bomb Boos (`SmsBombBoo.cs`, `SmsBombBooSpawner.cs`, `SmsLassoArm.cs`), Mario's only weapon.** The supply: black foggy spots at random on the stone (a black pool, low mist, two glowing eyes facing Mario) and a Boo rising up through the floor in black smoke, floating as Sunshine's Boos do (telesa.prm: the bob, the tilt, the wander between goals), hurting on touch unless he is above it; it wears the Sunshine Boo's body in the Bomb Boo's colours. X without FLUDD is Mario's grounded SPIN: it takes a Boo BY THE TONGUE, Galaxy's way -- he holds it up in one fist (a lasso arm on his skeleton) and the Boo swings round him on a long radius, rising with his jump (King Boo is out of reach from the ground: the hit comes from a jump's apex), the spin jump speeding the swing; walked into King Boo it BURSTS on him and he takes a hit. A Boo Mario LOOKS AT is shocked: it freezes where it hangs, trembles, stares back, its eyes burn, it drools and worries (a head shake, a lean, a cower, sweat) until he looks away. King Boo's hit clears the field. 229: the Boos keep off the rim -- the arena floor measures its own stone's edge (25.1 m) and a Boo's wander and body stay 3 m inside it.
  >
  > **The Shine and the ending (183, 217, 220-228).** The Shine two and a half times its size; the game's Shine sounds for EVERY Shine (the appear's chime and fanfare, the hum, the chime at the touch, the Shine Get fanfare -- re-rendered in 223 and 225 after a baker bug, the envelope curves read backwards in time); the HUD hiding through the cutscenes as the game's console does; the SHINE Get landing and the card save. The frame hitches at the Shine's spawn and the get's landing are gone (the Shine and the SHINE! letters built ahead), the .NET collector is held off from the fatal hit to control back, and the port's garbage rate is cut from about 1.4 to about 0.6 MB/s (the camera rig's and Mario's physics queries re-used, every Godot name made once: `SmsNames.cs` and `MarioRays.cs`, new). 228: a touchdown turns Mario's face onto the way he is moving.
  >
  > **To try:** play the casino fight to its end (Sirena Beach's `KingBooArena.tscn`): the table breaks and the rematch begins; or F6 on `assets/Sms/Rematch/RematchArena.tscn`. Grab a Bomb Boo with X (no FLUDD), jump and swing it into King Boo; nine hits; mind the edge. Press the editor's Build first: the C# changed.
  >
  > **Knobs:** every node's knobs are grouped with plain-English notes in the Inspector (King Boo: The fight, one group per attack, The split, The death, The Shine, Sounds; the supply: the spots and the fog; the Boo: the float, the swing, the shock, the rim; the arena: the sky, the lighting, the embers, the scorch marks, the death plane). Where the port deviates from the game's own rule, that rule is the knob's off state.
  >
  > **Drives:** the regression drive (drive162) at 68 / 68 checks plus one headless drive per pass (windowed shot runs for the looks), every one green before its commit; the numbers per pass are in `PROJECT_LOG.md`.
  >
  > **Not in it / deviations (logged):** the way out of the arena after the Shine (a later pass of its own, like a normal level's exit); King Boo's HP faces (a 217 mockup, scrapped); the fight itself is the design doc's, tuned in Jaysonn's playtests (the game has no such fight), with Sunshine's own data wherever the doc is silent (the Boo's float, the Shine Get, the fall death, the miss and game-over jingles). Also in the range: `Mario.cs` (the spin, the lasso carry, the freeze, the throw, the fall death, the landing's facing, the shared rays), `SunshineCamera.cs` (the death demos, the shared rays), `MarioCamData.cs`, `MarioYCam.cs`, `SmsStandardHud.cs`, `ShineSprite.cs`, `SmsConsoleStr.cs`, `SmsBossTelesa.cs` (the `Rematch` knob), the sound scripts and the re-rendered music WAVs. About 28 MB of new files (19 MB of 154's WAVs, 197's 2.8 MB mp3, the sky textures, the jingles and the Shine sounds); nothing near GitHub's 100 MB limit. Jaysonn's live edits (`assets/Level.tscn` and the rest) are not in it.
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/kingboo-rematch
```
git push -u origin feature/sounds
git push -u origin feature/kingboo-rematch
git rev-list --count origin/feature/kingboo-rematch..feature/kingboo-rematch
gh auth status
gh pr create --base main --head feature/kingboo-rematch --title "Feature: the King Boo rematch and the port's sounds (SMS passes 154-229)" --body-file pr157.md
```
Expect `0` from the rev-list. KEEP THE #6 ORDER: push both branches (#17's for the record; this push carries its commits anyway) -> write the
result paragraph below, commit it and push -> only THEN open the PR (`gh pr create` if `gh auth status` is logged in, else the compare URL with
the title prefilled and the body from `pr157.md` by the clipboard). ONE merge only: this is a PR, so do NOT also `git merge` it into local
`main`. Once merged (squash or merge commit, his call): `git checkout main && git merge --ff-only origin/main` here (not `git pull`:
`pull.rebase` refuses it over his unstaged work), mark #17 and #18 DONE with the PR's URL, delete `pr157.md`, tell Kiro the sha. The working
tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of `assets/Sms/SmsHiddenCoin.cs`, the gatekeeper / launcher,
BossTelesa and Mario `.tres` re-saves, the Koopa, Goop and polta `.png.import` flips, the `addons/godot_ai` churn and `KINGBOO_REMATCH_gdoc.txt`
at the root: his, leave them (none of them belong in this PR).
Result: `feature/kingboo-rematch` pushed as a NEW remote branch, 183 commits over `main` `90efb17`, 935 files / +1471489 / -480.
`git rev-list --count origin/feature/kingboo-rematch..feature/kingboo-rematch` returned `0`. `git merge-base feature/kingboo-rematch main`
is exactly `90efb17`, so the stack sits on the trunk with no merge in it, and `git merge-base --is-ancestor feature/sounds
feature/kingboo-rematch` confirms #17's 22 sound commits are in this history. The named code tips `d05ba70` (229), `79b4c87` (156e) and
`7df58ad` (#17's tip) are all ancestors. PROJECT_LOG.md gains 91 entries in this range: 11 for the sounds (154 to 156e) and 80 for the
rematch (157 to 229).
THE INSERTION COUNT IS NOT WHAT IT LOOKS LIKE, the same as #16. 1440192 of the 1471489 insertions are four `.glb.import` files under
models/Sms/Rematch/ (`polta_planet`, `polta_parts`, `bomb_boo`, `bomb_boo_tongue`), 11.67 MB each. Everything hand-written in the branch
is the other 31297 insertions. 406 of the 935 files are binary, which is the WAVs, the mp3, the sky textures and the models.
NOTE on the commit count: the entry says 182, measured 183. The extra one is `1b5c817`, this entry's own commit, which cannot count
itself. The same off-by-one as every entry with a PR so far, and always the queue file's own commit.
ONE CORRECTION TO THE PR BODY, and it is a factual one rather than formatting. Kiro's body said "About 28 MB of new files". Measured,
the branch adds 95.1 MB: 50.3 MB of real assets and 44.8 MB in those four `.glb.import` files. The undercount came from leaving out the
two baked music tracks, `MSD_BGM_BOSS.wav` at 12.1 MB and `MSD_BGM_CHUBOSS.wav` at 7.0 MB, which entry #17 names itself. The sentence now
gives both figures and says the import share is the project's standing bloat rather than anything this branch did. The largest single
file is `MSD_BGM_BOSS.wav` at 12.1 MB, so nothing is near GitHub's 100 MB per-file limit and the push went through in one go.
Hygiene: 0 of Jaysonn's live edits in the range, and `KINGBOO_REMATCH_gdoc.txt` is not in it either, still untracked at the root as he
left it. 26 tracked edits of his were in the working tree before the push and all 26 after.
PR body: `pr157.md` was already at the project root and is byte for byte identical to this entry's inline blockquote, 7986 bytes and pure
ASCII, so no extraction and no paragraph repair was needed this time, unlike #15 and #16. After the size correction and the attribution
footer it is 8336 bytes.
`gh auth status` still prints "You are not logged into any GitHub hosts", the ninth entry running, so the browser route. The compare URL
with the body prefilled measures 12416 characters, over the ~8000 ceiling #8 established, so the title went in the URL and the body went
by the clipboard, read with `[System.IO.File]::ReadAllText` under explicit UTF-8 rather than `Get-Content -Raw`, which is what mojibaked
#16's footer glyph.
ONE merge path, as the entry insists: this branch gets the PR and is NOT also merged into local `main`. `feature/sounds` gets neither.
THE TRUNK TIP AND THIS ENTRY'S PR NUMBER: read them from `git log --oneline -1 main` and from GitHub. A commit cannot state its own sha,
and this result paragraph is committed BEFORE the PR is opened, per the #6 order, so it cannot state the PR number either.
STILL OPEN, carried forward: `feature/enemy-pinna2` (entry #14, SMS 147 and 148, the Bullet Bill and the ground Electrokoopa) and
`feature/select-stages` (entry #11, SMS 135) are both still pushed and still waiting on Jaysonn's playtest, untouched since 2026-11-03,
and `feature/select-stages` holds the only copy of #11's text. Both now sit far behind the trunk and will conflict at the tails of
PROJECT_LOG.md and this file when they do land.
MERGED BY JAYSONN as a **SQUASH**: PR https://github.com/JasonAbsolute/ProjectShine/pull/15, `main` is now `9b85369`, one commit with
a single parent `90efb17`. Local `main` was advanced with `git fetch origin main:main` and then checked out, which moves the ref
without touching the working tree, and all 26 of Jaysonn's tracked edits were identical before and after.
Verified by TREE, the way a squash requires: `git diff --name-only feature/kingboo-rematch main` is EMPTY, so every one of the 935
files matches the branch, both log files and #17's 22 sound commits included. Spot-checked on the trunk: the sound scripts and the two
baked music tracks, 317 Mario WAVs, and the whole of assets/Sms/Rematch/ with its 24 scripts and 9 shaders.
THE PR WAS OPENED WITHOUT ITS BODY. PR #15 carries the title but GitHub shows "No description provided", so the prefilled compare URL's
title took and `pr157.md`'s 8336 characters never reached the form. The body is worth pasting into the PR description after the fact,
since it is the only written account of 91 passes outside PROJECT_LOG.md. The same happened to PR #16, which got the default
"Feature/sounds" title and no body. For the next entry: paste and confirm the body BEFORE pressing Create, and read it back through the
API (`/repos/.../pulls/<n>`) to check it landed, as #16's footer check did.
`pr157.md` and `pr154.md` deleted from the project root.
THE TRUNK TIP: read it from `git log --oneline -1 main`, not from a sha written here. A commit cannot state its own sha.
STILL OPEN, carried forward: `feature/enemy-pinna2` (entry #14, SMS 147 and 148) and `feature/select-stages` (entry #11, SMS 135) are
both still pushed and still waiting on Jaysonn's playtest, untouched since 2026-11-03 and now 183 commits behind the trunk.
`feature/select-stages` holds the only copy of #11's text. Three merged branches are safe to delete besides: `feature/enemy-sirena`,
`feature/kingboo-rematch` and, once PR #16 is closed, `feature/sounds`.

### #19 -- PENDING -- queued 2026-11-17 03:41 PST (Kiro) -- PUSH ONLY, no PR yet (SMS 230 + 231 + 232 + 232b + 233 + 233b + 233c + 233d + 234 + 235 + 236 + 237 + 238 + 239), updated 2026-11-20 01:16 PST -- was 2026-11-20 00:41 PST -- was 2026-11-19 03:15 PST -- was 2026-11-19 01:35 PST -- was 2026-11-19 00:10 PST -- was 2026-11-18 23:26 PST -- was 2026-11-18 22:56 PST -- was 2026-11-18 22:29 PST -- was 2026-11-17 18:25 PST
Push the fourteenth feature branch: `feature/kingboo-casino`, branched from `main` `d7887ae` (the trunk after PR #15, the rematch and the sounds, and its
"#17 and #18 done" commit). The CASINO fight's touch-ups after Jaysonn's playtest of the trunk. SMS 230: every actor sound fades by the GAME's OWN RULE for
it (MSHandle's category distances and the sound's curve bits: a placed Shine's hum is heard within 16 m and gone at 48 m -- it hummed across the whole level
at half volume -- King Boo's DOWN and his vanishes play flat anywhere), and the casino fight's sounds checked against the game's: all wired but the hover hum
(a DSP oscillator voice the baker cannot render) and the slot machine's landing thump (the game checks a frame its clip never reaches; 231 moves it) --
assets/Sms/Sound/SmsActorSound.cs. SMS 231: the slot machine comes up BOLTED to King Boo (SlotSnapsAtAppear: the hosei zeroed as the appear begins; the
game left it on an exponential tail after each spit, seconds behind him), his body goes WITH the death's burst (HideBodyAtBurst: the sixth-size body of
the down clip's last frame no longer sits through the wait to the Shine), the fanfare's CONFETTI rebuilt from ms_btls_fubuki.jpa (125 squares of 0.8 m
raining over the table for 6 s, spinning, white / yellow / red, no shadows; ConfettiHeightUnits 1000 starts it at the frame's top over his crown -- the
game's 1707 is out of frame) and the slot's landing thump at a frame the appear clip has (SlotLandFrame 56; the game's 78 never came) --
assets/Sms/Enemy/SmsBossTelesa.cs, assets/Sms/Enemy/BossTelesa/BtlsFubuki.tscn. SMS 232: the slot's MIXED result ported -- King Boo spits TWELVE HOT-SPRING
BUBBLES (TBubble, the game's dead code: its shipped slot never lands Mixed, and its own kill cylinder would have popped them where they were born): they sink
to the table, spread, rise to 4.7 m and grow to 5.5 m across, hurt and pop on touch, pop into a LARGE WATER BOTTLE when hosed, and after 8 s split into three
children each that walk at Mario (the pool 30) -- assets/Sms/Enemy/BossTelesa/SmsOsenBubble.cs, OsenBubble.tscn, OsenBubble.gdshader (new), the model and its
three textures under models/Eniemes/BossTelesa/bubble/ (new), SmsBossTelesa.cs (SlotForce Mixed, BubbleScene, the spit; the kill cylinder lets bubbles
through), BossTelesa/SmsTelesaSlot.cs (a forced Mixed holds each drum to its own face), assets/Sms/Items/SmsWaterBottle.cs (LifeTicksOverride). SMS 233:
the fanfare's CONFETTI at the jpa's own 60 Hz and look (231 read a jpa frame as 1/30 s and drew flat billboards: pixel blocks) -- 125 alpha-tested 0.5-m
squares fluttering along their fall (align_y + rotate_y), 12 m/s easing to 2.8, the colour cycling grey / yellow / red by age, 3 s, from 1125 u over the
table (BtlsFubuki.tscn regenerated by tools/projectshine/rematch157/gen_fubuki233.py; SmsBossTelesa.cs ConfettiHeightUnits 1125) -- and the REMATCH's
ENTRANCE: King Boo came up for the fight by the casino appear clip's 17-m leap under the game camera, out of the floor and off the frame's top in two frames
(a pop); 233 made it a slow EMERGENCE through the stone under the cutscene's own camera, and 233b (Jaysonn: "it's not supposed to be a cutscene, he should
just use the animation of him coming from under in the intro") put the defaults BACK -- the intro's own leap after the tell, under the game camera, 174 /
182's flow -- keeping both as knobs, off (SpawnRiseLeap on assets/Sms/Rematch/SmsKingBooRematch.cs; RiseInCutscene on assets/Sms/Rematch/SmsRematchIntro.cs);
233c (Jaysonn: "no he just appears, he's not like coming from the ground naturally" -- the leap takes him from under the floor to out of the game camera's
frame in 4 frames) CARRIES his body up through the stone on a smooth curve over the appear clip's 1.6 s while the clip plays its pose, the burst and the
stars as he breaks the surface, no overshoot, no camera hold (SpawnRise = Clip Carried on KingBooRematch; Clip Leap = 182's, Emergence = 233's); 233d (Jaysonn: "ok no just undo what i said about him coming up") takes the ENTRANCE work OUT -- both rematch scripts back
to 232b's byte for byte: the fight's rise is 182's leap after the tell under the game camera, as before 233; the confetti stays. SMS 234: the FRUIT --
every fruit scene's material MATTE (roughness 1, specular 0: the game's materials are a vertex-lit toon ramp with no specular; the port's 0.5 / 0.5 drew
a plastic highlight band), the ROLL at the game's rate (SmsRollLikeTheGame on assets/Fruit.cs: |vxz| / (the game's body radius x Size) x 2 a tick, as
calcCurrentMtx -- the port's smaller collision spheres spun them 1.3-2.2x faster, a durian 12 turns a second: a blur that read as no spin), and the red
pepper rolling like the rest (the "SMS quirk: it never spins" lock was not in the game) -- assets/Fruit.cs, fruit_banana / fruit_coconut / fruit_durian /
fruit_pineapple / fruit_red_pepper / fruitpapaya.tscn. SMS 235: the casino table's BREAK without the glass -- the shards carry the discs' own material
(MatchMaterial: metallic 1 / specular 0.5 / roughness 1, the unshaded surface, the gold's 7 x 7 tiling, the linear filter; 157's shader drew a pale glossy
non-metal the moment it swapped in), the cracks dark seams with no glow (CrackGlow 0, CrackDarken 0.6), the fade a dissolve in the opaque pass --
assets/Sms/Rematch/SmsFloorShatter.cs, FloorShatter.gdshader. SMS 236: the pause menu's selection sparks round the WHOLE circle (the shader's angle
step had stayed at the dumped rate 14 while the emitter fired the circle's division 10: a 103-degree gap; the step is the burst's own count now) --
assets/Sms/Console/PauseSparks.tscn, PauseSparks.gdshader, SmsPauseMenu.cs. SMS 237 / 238: the two SECRET COURSES for the demo laid out as the game lays
them out and under the game's own skies -- the Secret of Ricco Tower (Levels/Rico4Secrect/Skybox/Rico4Secret.tscn) and the Hillside Cave Secret
(assets/secrect_level.tscn): every piece at the disc's scene.bin position / rotation / scale with his piece scenes scaled to the game's model sizes, the
lifts and stairs on their scene.ral rails (his MovingPlatform), the umaibou and stars spinning at the game's rates, the rail blocks and EX cubes on the
new assets/Sms/SmsRailBlock.cs (the game's TRailBlock), the intro pans the game's startcamera.bck, the skies the game's sky.bmd domes riding with the
camera with their btk / brk in shaders (assets/Sms/Sky/*, models/Sms/Sky/*). SMS 239: Hillside's lift cubes orange like the game's (normal_block.tscn's mesh
is the game's NormalBlock glb with its vertex colours, models/Sms/Map/normalblock/) and its palms the game's palmNormal (assets/Sms/Map/PalmNormal.tscn,
models/Sms/Map/palmnormal/: the trunk, the stand-on quad and the leaves as collision, his PalmTreeGrab climb zone). NOT yet playtested by Jaysonn: push the branch so
it is on GitHub; do NOT open a PR until he says these touch-ups are done (more passes follow on this branch). Further
passes re-point this entry's tip.
- Branch `feature/kingboo-casino` -- code tip `077f0bc` ("SMS 239: the Hillside Cave Secret's lift cubes ORANGE like the game's and its palms the game's own
  palmNormal ..."; normal_block.tscn, assets/secrect_level.tscn, assets/Sms/Map/PalmNormal.tscn, models/Sms/Map/normalblock/*, models/Sms/Map/palmnormal/*)
  on 237 + 238's log commit (`7ade9a8`) on the 238 commit (`cde8cf0`, "SMS 238: the Hillside Cave Secret (secrect_level.tscn) laid out as the game lays it out and under
  the game's own sky ..."; assets/secrect_level.tscn, assets/Sms/Sky/SmsSkyHillsideSecret.gdshader, SkyHillsideSecret.tscn, HillsideSecretSky_*.tres,
  models/Sms/Sky/HillsideSecret/*) on the 237 commit (`cfd6c07`, "SMS 237: the Secret of Ricco Tower (Rico4Secret.tscn) laid out as the game lays it out
  and under the game's own sky ..."; Levels/Rico4Secrect/Skybox/Rico4Secret.tscn, assets/Sms/SmsRailBlock.cs, assets/Sms/Sky/SmsSkyRiccoSecret.gdshader,
  SkyRiccoSecret.tscn, RiccoSecretSky_VRsph.tres, models/Sms/Sky/RiccoSecret/*) on 235 + 236's log commit (`d47f4f4`) on the 236 commit (`afeee08`,
  "SMS 236: the pause menu's selection sparks round the WHOLE circle ..."; assets/Sms/Console/
  PauseSparks.tscn, PauseSparks.gdshader, SmsPauseMenu.cs) on the 235 commit (`cbd5bfd`, "SMS 235: the casino table's BREAK without the glass ...";
  assets/Sms/Rematch/SmsFloorShatter.cs, FloorShatter.gdshader) on 234's log commit (`0c2761d`) on the 234 commit (`25f9b25`, "SMS 234: the FRUIT -- matte
  like the game's ..., the ROLL at the game's rate ..., and the red pepper rolling like the rest ..."; assets/Fruit.cs, fruit_banana.tscn, fruit_coconut.tscn,
  fruit_durian.tscn, fruit_pineapple.tscn, fruit_red_pepper.tscn, fruitpapaya.tscn) on 233d's log commit (`0b9bc81`) on the 233d commit (`a54f214`, "SMS 233d: the rematch's ENTRANCE work UNDONE at Jaysonn's word --
  SmsKingBooRematch.cs and SmsRematchIntro.cs back to 232b's (8ef659c) ..."; assets/Sms/Rematch/SmsKingBooRematch.cs, SmsRematchIntro.cs) on 233c's log commit (`7bbb891`) on the
  233c commit (`3a7f4eb`, "SMS 233c: King Boo COMES UP THROUGH THE STONE for the fight -- the intro's appear clip plays its pose while his body is CARRIED
  up out of the floor on a smooth curve ..."; assets/Sms/Rematch/SmsKingBooRematch.cs) on 233b's log commit (`6cf8f0b`) on the 233b
  commit (`5d412b1`, "SMS 233b: the rematch's entrance back to the intro's OWN rise under the GAME camera ..."; assets/Sms/Rematch/SmsKingBooRematch.cs,
  SmsRematchIntro.cs) on 233's log commit (`3b8d7ff`) on the 233 commit (`561521a`, "SMS 233: the casino fanfare's
  CONFETTI at the jpa's own 60 Hz and look ..., and the rematch's ENTRANCE: King Boo comes up for the fight in a slow EMERGENCE under the cutscene's own
  camera ..."; assets/Sms/Enemy/BossTelesa/BtlsFubuki.tscn, assets/Sms/Enemy/SmsBossTelesa.cs, assets/Sms/Rematch/SmsKingBooRematch.cs,
  assets/Sms/Rematch/SmsRematchIntro.cs) on 232b's log commit (`8ef659c`) on
  the 232b commit (`8851d26`, "SMS 232b: the hot-spring bubble's Refraction OFF by default -- the game's own picture ... is a pale blue bubble";
  SmsOsenBubble.cs, OsenBubble.gdshader, OsenBubble.tscn) on 232's log commit (`cdaf77f`) on the 232 commit (`1331ba7`, "SMS 232: the casino
  slot's MIXED result ported -- King Boo spits TWELVE HOT-SPRING BUBBLES ..."; assets/Sms/Enemy/BossTelesa/SmsOsenBubble.cs, OsenBubble.tscn,
  OsenBubble.gdshader, models/Eniemes/BossTelesa/bubble/*, assets/Sms/Enemy/SmsBossTelesa.cs, BossTelesa/SmsTelesaSlot.cs, assets/Sms/Items/SmsWaterBottle.cs)
  on the 231 commit (`572614e`, "SMS 231: the CASINO FIGHT touched up -- the slot machine
  comes up BOLTED to King Boo ..., his body goes WITH the death's burst ..., the fanfare's CONFETTI is the game's ..., and the slot machine's landing thump
  finally fires"; assets/Sms/Enemy/SmsBossTelesa.cs, BossTelesa/BtlsFubuki.tscn) on the 230 commit (`65e5d5c`, "SMS 230: the CASINO FIGHT's sounds checked,
  and every actor sound FADES BY THE GAME'S OWN RULE for it ..."; assets/Sms/Sound/SmsActorSound.cs) and 231's log commit (`51ecd1e`) on `main` `d7887ae`;
  the branch tip is this file's own commit on top of them; 25 commits since `main`. New files (239): assets/Sms/Map/PalmNormal.tscn,
  models/Sms/Map/normalblock/normalblock.glb + normalblock.png, models/Sms/Map/palmnormal/palmnormal.glb + palmnormal.png (+ their .import). No new files in 233 / 233b / 233c / 233d / 234 / 235 / 236.
  New files (237 / 238): assets/Sms/SmsRailBlock.cs (+ .uid); assets/Sms/Sky/SmsSkyRiccoSecret.gdshader and SmsSkyHillsideSecret.gdshader (+ .uid),
  SkyRiccoSecret.tscn, SkyHillsideSecret.tscn, RiccoSecretSky_VRsph.tres, the nine HillsideSecretSky_*.tres; models/Sms/Sky/RiccoSecret/
  sky_ricco_secret.glb, P_casino_glow2mm.png, B_crasicmario.png and models/Sms/Sky/HillsideSecret/sky_hillside_secret.glb, B_sters.png, B_sunsetgrade.png,
  B_exsky_Rail.png, B_exsky_Train.png, B_exsky_Silen.png, B_exsky_Aplane.png (+ their .import). New files (232): SmsOsenBubble.cs (+ .uid), OsenBubble.tscn,
  OsenBubble.gdshader (+ .uid), models/Eniemes/BossTelesa/bubble/btelesa_osenbubbles_ind.glb, N_btelesa_bubble.png, N_btelesa_bubble_ind.png,
  N_btelesa_bubble_spe.png (+ their .import). The range carries none of Jaysonn's live edits (assets/Level.tscn, KingBooArena.tscn, RematchArena.tscn,
  SmsHiddenCoin.cs, the .tres and .import churn).
- To try 239: play assets/secrect_level.tscn -- the lift cubes are orange with pale frames (the game's colours) and the two palms on the island are the
  game's own: climb one (push into the trunk) and stand in its crown; the leaves hold you but do not dip (your PalmTree.tscn is untouched for other levels).
- To try 237 / 238: open Levels/Rico4Secrect/Skybox/Rico4Secret.tscn or assets/secrect_level.tscn and play it (press the editor's Build first: the C# changed).
  Each course is the game's: the intro is the game's start camera (16 s, skippable), you drop onto the island facing the course, and every piece is
  where the game puts it at the game's size -- Ricco: the sticks turning slowly (the game's 7.5-10.5 degrees a second), the two blue walls with the
  platform and 1-up between them, the three green rail blocks rolling over as they slide their rails, the four nails, the tilted sticks down to the
  end; Hillside: the stair blocks sliding across with their rests, the pillar, the grid of lifts shuffling, the EX cubes rolling round their loops
  along the green bridge, the stars spinning, the end platform with the tree. The skies are the game's: Ricco's blue dots with the 8-bit Mario
  pulsing on a 20-s cycle, Hillside's dark star field with the rails, the train and the plane crossing on its 4.5-minute loop. Your scripts, piece
  scenes, red-coin switch and Shine logic are untouched; the two scenes were rewritten (your old copies are in git: 6fc7a6f and f869051). Knobs: the
  sky's speed and brightness on its material (assets/Sms/Sky/*.tres), each rail block's RailSpeeds / SpeedScale, each lift's MoveSpeed / WaitTime.
  Not placed: the invisible board NPC and the 100-coin Shine; Hillside's palms are your PalmTree at the game's height; your papaya and the hidden
  marioHUD in secrect_level are gone; the Hillside Mario is now a PlayerSpawner like Ricco's (the character select works there too).
- To try 235: beat the casino fight. Where the Shine would come the table cracks and breaks as before, but now it is the TABLE breaking: no glow along
  the cracks, no colour change when the cracking starts (the pieces are the table's own gold, red, green and marble, lit as the table was), dark seams
  spreading from the centre for 1.4 s, then the pieces fly and tumble in their own colours and dissolve. Knobs on the FloorShatter node under
  KingBooArena's RematchExit: Match Material (on), Crack Darken (0.6; 0 = no visible cracks, it just breaks), Crack Glow (0; 1.6 = the old lit glass),
  the timing and the flight as before. The sound is still the glass-break sound -- say if you want it changed.
- To try 236: pause the game and watch the yellow streaks around the highlighted option: ten of them, evenly spaced, all the way round (they used to
  leave a gap over the right-hand quarter).
- To try 234: play the casino fight to a fruit round (spray his tongue; the drums land on fruit) and watch the spit: the coconuts, papayas, pineapples,
  durians and the two red peppers tumble out turning at the game's own rate -- fast out of his mouth, slowing as they roll out on the table and
  stopping where they stop -- the peppers turning like the rest; up close every fruit is matte (no plastic shine band), lit flat like Mario. A pepper at
  rest sits as the game's does: its stem end a body radius over the table and the rod lying where the roll left it (pointing up it floats, down it pokes
  through the table) -- that is the game's own placement (its pepper model has its origin at the base); say if you want it centred instead. Knob: Sms
  Roll Like The Game on a fruit (off = the old faster spin from the collision sphere's radius). The look is each fruit scene's material (roughness /
  specular in the inspector). Your sizes and collision spheres are untouched. Press the editor's Build first: the C# changed.
- To try 233 (after 233d): the rematch's entrance is as it was before 233 -- after King Boo's laugh and dive the game camera is back behind Mario
  and he comes up at his spot by the intro's own animation (the glow and the wisps on the stone, then the leap out of the floor with the burst, the
  stars and his cry); the entrance knobs of 233 / 233b / 233c are gone. In the casino fight beat a round and watch the fanfare: the confetti is now small crisp squares tumbling end over end as they fall fast and slow
  into a drift, each flickering grey / yellow / red, gone in three seconds -- Confetti Height Units on BossTelesa (1125; 1707 = the game's placement,
  mostly above the picture). Press the editor's Build first: the C# changed.
- To try 232: in the casino fight set Force Result to Mixed on BossTelesa (KingBooArena.tscn) and beat a round: the drums stop on three different faces,
  the third fanfare plays (no confetti) and he spits twelve pale blue bubbles that sink to the table, spread round it, rise to about 4.7 m and swell to 5.5 m
  across over two seconds, wobbling. Walk under one and jump into it: it hurts you and pops. Hose one (aim up with R held and the stick, or the hover):
  it pops into a LARGE water bottle on the floor under it (a full refill, there for ten seconds). Leave them eight seconds: each swells and splits into
  three smaller ones at head height that drift, then walk at you -- no more than thirty alive at once. Knobs on OsenBubble.tscn: The bubble (bubble.prm's
  numbers: Live Time, Num Division, Max Scale, Add Pos Base Units, Rate Expand, Float Height Units, Max Alive), Deviations (Spit Sink Seconds, Spit Drag,
  Face Camera -- 0 / off = the game's), The bottle, Look (Refraction: off, the game's own flat blue; on = the screen behind them refracted, red in this room; Opacity). With Force Result back on The Game the
  round never comes (the game's rates never land Mixed) -- a Hit Collect Rate under 1 on BossTelesa lets it happen on its own. Press the editor's
  Build first: the C# changed, and the new model imports on the first open.
- To try 231: play the casino fight. Beat a round (spray his tongue; three fruit) and watch the fanfare: confetti rains over the whole table from over his
  crown for six seconds (Confetti Height Units on BossTelesa, 1000; 1707 = where the game puts it, mostly above the picture). After his spit, when he hides
  and comes back, the slot machine comes up under him with him and thumps as he settles (Slot Snaps At Appear; Slot Land Frame) -- spray his tongue the
  moment he pops up and the machine is under him in the same frame as he cuts to his roll. Beat him: at the end of his tumble the burst takes his body with
  it (Hide Body At Burst; off = the game's, the tiny body held until the Shine). Press the editor's Build first: the C# changed.
- To try 230: start far from a Shine and walk up to it -- silent beyond about 48 m, rising from about 16 m to full; King Boo's defeat sound, his vanishes
  and the Shine's appear chime are heard at full from anywhere, as the game has them. Nothing else changed in how the sounds are picked or played.
```
git push -u origin feature/kingboo-casino
git rev-list --count origin/feature/kingboo-casino..feature/kingboo-casino
```
Expect `0` from the rev-list. No PR, no merge. The working tree still carries Jaysonn's uncommitted `assets/Level.tscn`, the editor's retab of
`assets/Sms/SmsHiddenCoin.cs`, his `KingBooArena.tscn` and `RematchArena.tscn` edits, the `.tres` re-saves, the `.png.import` flips, the `addons/godot_ai`
churn and `KINGBOO_REMATCH_gdoc.txt` at the root: his, leave them.
