# Mario's custom animations

Clips of Mario's that are not from the game (SMS 176), all played by the King Boo rematch's intro:

- `ma_lookabout` -- the "where am I?" look-around after he lands (3.0 s, one-shot).
- `ma_battle_stance_in` -- the drop into his battle stance on the cut to the stand-off (0.45 s, one-shot) ...
- `ma_battle_stance` -- ... and the stance itself, bouncing, held until the fight starts (0.9 s, loops).

## Two ways to work on a clip

**In the editor.** Open `assets/Mario.tscn`, select its `AnimationPlayer`, pick the clip in the Animation panel. Move keys, re-time them,
add keys: it is a normal `Animation` saved in its own `.tres` here, so saving the scene saves the clip, and git tracks the text.

**From a pose script.** `ma_lookabout.pose.json` is the clip written as a few key POSES -- a time and, per joint, a list of turns about the
body's axes in degrees:

| axis | + | - |
|---|---|---|
| `up` | turn to HIS LEFT | to his right |
| `left` | nod / tip FORWARD | look UP / lean back |
| `forward` | roll toward his LEFT shoulder (swings the LEFT arm OUT at a shoulder) | toward his right |
| `move_up` / `move_left` / `move_forward` | MOVE the joint that many units along the axis (the rig's units: he stands 117; `center` down 10 = a crouch) | the other way |

A turn is applied AT the joint, in the order written, and the joint's children follow (turn the chest and the head and arms ride along;
then the head's own turn adds to it). Joints not named hold the base clip's pose (`base_clip` at `base_frame`: the idle `ma_wait`, frame 0).
A looping clip (`"loop": true`) should end on the pose it starts with.
Joint names are the rig's: `jnt_head`, `jnt_chest`, `jnt_sldr_L` / `_R` (shoulders), `jnt_arm_L1` (upper arm), `jnt_arm_L2` (forearm),
`jnt_hand_L`, `jnt_waist` (the legs hang from it: turning it turns the legs), `jnt_leg_L1` / `_L2`, `jnt_foot_L`, `center` (the hips / root).

Bake the script into the clip (from the project root; it overwrites the `.tres`):

    godot --headless -s tools/anim/bake_anim.gd -- res://assets/Mario/Anims/ma_lookabout.pose.json res://assets/Mario/Anims/ma_lookabout.tres

The bake keys every named joint at every key time (cubic between them), every other bone once at 0, and prints the head's yaw / pitch and the
hands' height at each key so a wrong sign shows at once. The clip is then as editable as any other: a bake is only the starting point.
Edits made in the editor are NOT written back to the script -- re-baking replaces them, so pick one source of truth per clip.

## Playing one

A cutscene plays a clip outside Mario's AnimationTree with `Mario.SmsPlayDemoClip("ma_lookabout")` -- or a one-shot and then a loop,
`SmsPlayDemoClip("ma_battle_stance_in", 1f, "ma_battle_stance")` -- and gives the tree back with `Mario.SmsEndDemoClip()`
(`SmsClipLength(name)` for timing). The rematch intro's `LookClip`, `StanceInClip` and `StanceClip` knobs name the clips it plays.

## Adding a clip

Write `<name>.pose.json` here, bake it to `<name>.tres`, then add the clip to Mario's AnimationPlayer in `assets/Mario.tscn` (the Animation
panel's library: add the `.tres` to the library the other `ma_*` clips are in, so code can play it by its bare name).
