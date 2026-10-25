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

### #2 -- PENDING -- 2026-10-25 03:55 PDT (Kiro)
Push the first feature branch and open its PR: the card save ("Save?" after a shine, a blue coin, the pause menu's SAVE). Not merged yet --
Jaysonn playtests from the PR, then merges there (or `git merge --no-ff feature/save-prompt` into main here).
- Branch `feature/save-prompt` -- tip `3ab4893` ("SMS pass 124b: card save -- centred contents, WindowColor knob (darker default), digit
  pitch, count mode"; pass 124 is `a804f34`), 3 commits since main (`7e448b9`) plus this file's commits.
- PR: title "Feature: the card save (SMS pass 124)"; base `main`; body:
  > TCardSave (GC2D/CardSave.cpp) on the game's own `save.blo` + `savemessage.bmg`: the shine-get window (Save and Continue / Save and
  > Quit / Continue, the saved count or "New", the pointing hand), the blue-coin window (every coin, as the game), the pause menu's SAVE
  > ("Save?" YES/NO with the breathing green-to-yellow pick, then "Save complete. Continue playing?"), the 300-frame saving window with the
  > live count at frame 200. The save write moved from the pickup to the dialog's "Save and ..." (Continue leaves it unsaved, as the game).
  > New: SmsCardSave.cs, SmsTextBox.cs (a J2DTextBox on the system font), CardSave.tscn (under Mario in Player.tscn), 8 textures.
  > Drive 124 34/34 through the real input path (across the stage move); 123 32/32, 118 35/35, 119 25/25; boot ALL OK.
  > Playtest: get a shine (the window after the white), pick up a blue coin, pause -> SAVE. Knobs on CardSave.tscn: WindowColor (any colour;
  > the game's is (0, 0.733, 0.725, 0.533)), CenterContents (off = the game's left-weighted layout), DigitPitch, CountShown (Saved = the game /
  > Live), HeadingMessage (33 = the game's "File created." quirk, 41 = "SHINE! Save data?"), TitleScene, Size, SavingFrames, the six sound
  > slots (empty).
  > Not ported: memory-card states, the YES/NO sparks, the dialog's mask (never shown for these purposes).
  compare URL: https://github.com/JasonAbsolute/ProjectShine/compare/main...feature/save-prompt
```
git push -u origin feature/save-prompt
git rev-list --count origin/feature/save-prompt..feature/save-prompt
```
Expect `0`. Then open the PR from the compare URL with the title and body above. Do not merge it: Jaysonn decides after the playtest.

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
