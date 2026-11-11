"""lookabout_curves.py -- SMS 207: Mario's "where am I?" look-about (the rematch intro's ma_lookabout) written as MOTION CURVES, not a few key poses.

The curves below are the clip. Edit the numbers, run this file from the project root (python tools/anim/lookabout_curves.py), then bake:
    godot --headless -s tools/anim/bake_anim.gd -- res://assets/Mario/Anims/ma_lookabout.pose.json res://assets/Mario/Anims/ma_lookabout.tres
It writes assets/Mario/Anims/ma_lookabout.pose.json as a pose script with a key every frame (30 a second), so the baker's cubic has nothing to
overshoot and what is written here is what plays. The shape is the game's own idle (ma_wait: the head leads, the chest follows a beat later with
less of the turn, the arms swing with it, nothing stops dead) made alert: bigger, quicker, and done in 2.0 s -- the look-about's slot before King
Boo surfaces. No shrug, no head cock (199 / 207, Jaysonn), the hands hang at the idle.

Each curve is a list of (second, value) points joined by a monotone cubic (Fritsch-Carlson: no overshoot, the speed continuous, at rest at both
ends). Angles in degrees. "+ left" = toward HIS left. The head's yaw is the head's WORLD yaw (what the camera sees), the chest's turn is taken out
of it for the head's own key.
"""
import json, math, os, sys

LENGTH = 2.0       # s; the intro plays the clip at its own pace (SmsRematchIntro.LookKeepsPace) and takes the music cue's trim out of the laugh first
FPS = 30

# the head's world yaw: a beat while the laugh registers, a quick glance to his LEFT (60 in a third of a second), a drift while he scans, the sweep
# over to his RIGHT (a little further: the idle does the same), a drift, and back to the front for the cut to the stand-off
HEAD_YAW = [(0.00, 0), (0.07, 0), (0.40, 60), (0.65, 55), (1.28, -62), (1.52, -58), (1.92, 0), (2.00, 0)]
# the head's pitch, + = UP: the chin comes up a little through the glances (the laugh seems to come from everywhere), dips as the head sweeps
# through the front, and stays a touch up at the end -- alert
HEAD_UP = [(0.00, 0), (0.40, 4), (0.65, 5), (1.00, 2), (1.28, 5), (1.52, 6), (1.92, 4), (2.00, 4)]
# the chest's yaw: follows the head about 0.15 s later with 40% of the turn, and has not quite come home when the clip ends (the cut hides it)
CHEST_YAW = [(0.00, 0), (0.15, 0), (0.58, 24), (0.80, 22), (1.42, -24), (1.65, -22), (2.00, -3)]
# the upper arms swing with the chest's turn, this share of its yaw (the idle's hands travel further than their shoulders): the right arm forward and
# the left back as the chest turns left, the other way round on the right glance; ARM_LAG seconds behind the chest
ARM_SWING = 0.2
ARM_LAG = 0.05
# one breath over the clip: the chest leans back this far (degrees) at its fullest, half way through, and settles
BREATH = 1.5
# roll: none. The head cock of 176 / 199 is gone (Jaysonn: "he still tilts his head"); the pitch is taken about the head's OWN lateral axis after
# its yaw, so the turn adds no roll of its own.

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "assets", "Mario", "Anims", "ma_lookabout.pose.json"))


def pchip(points):
    """A monotone cubic through (x, y) points -- Fritsch-Carlson tangents, zero tangents at the ends (the motion starts and stops at rest). Returns f(x)."""
    xs = [float(p[0]) for p in points]; ys = [float(p[1]) for p in points]; n = len(xs)
    h = [xs[i + 1] - xs[i] for i in range(n - 1)]; d = [(ys[i + 1] - ys[i]) / h[i] for i in range(n - 1)]
    m = [0.0] * n
    for i in range(1, n - 1):
        if d[i - 1] * d[i] <= 0: m[i] = 0.0
        else:
            w1 = 2 * h[i] + h[i - 1]; w2 = h[i] + 2 * h[i - 1]
            m[i] = (w1 + w2) / (w1 / d[i - 1] + w2 / d[i])
    def f(x):
        if x <= xs[0]: return ys[0]
        if x >= xs[-1]: return ys[-1]
        i = 0
        while x > xs[i + 1]: i += 1
        t = (x - xs[i]) / h[i]; t2 = t * t; t3 = t2 * t
        return (2 * t3 - 3 * t2 + 1) * ys[i] + (t3 - 2 * t2 + t) * h[i] * m[i] + (-2 * t3 + 3 * t2) * ys[i + 1] + (t3 - t2) * h[i] * m[i + 1]
    return f


def build():
    head_yaw = pchip(HEAD_YAW); head_up = pchip(HEAD_UP); chest_yaw = pchip(CHEST_YAW)
    keys = []
    n = int(round(LENGTH * FPS))
    for i in range(n + 1):
        t = i / FPS
        c = chest_yaw(t); y = head_yaw(t); p = head_up(t)
        breath = -BREATH * math.sin(math.pi * min(1.0, t / LENGTH))              # "left" negative = lean back: in to half way, out to the end
        arm = ARM_SWING * chest_yaw(t - ARM_LAG)
        pose = {
            # the chest: the breath about the body's lateral axis, then its turn about the vertical (it starts square, so both are pure)
            "jnt_chest": [["left", round(breath, 2)], ["up", round(c, 2)]],
            # the head rides on the chest (already at yaw c): undo that yaw, pitch about its own lateral axis, then yaw to its world heading y
            "jnt_head": [["up", round(-c, 2)], ["left", round(-p, 2)], ["up", round(y, 2)]],
            # the upper arms: "left" + swings an arm BACK, - forward
            "jnt_arm_R1": [["left", round(-arm, 2)]],
            "jnt_arm_L1": [["left", round(arm, 2)]],
        }
        if i == 0: pose = {}                                                        # the first frame IS the idle's pose
        keys.append({"t": round(t, 4), "pose": pose})
    return {
        "name": "ma_lookabout",
        "comment": "SMS 207 -- GENERATED by tools/anim/lookabout_curves.py (edit the curves THERE, run it, re-bake): Mario's 'where am I?' look-about for the rematch intro, built from motion curves after the game's own idle (ma_wait: the head leads, the chest follows later with less of the turn, the arms swing with it, nothing stops dead) made alert and done in 2.0 s. A beat, a glance to his left (head 60 world yaw, the chest to 24), the sweep over to his right (-62 / -24), back to the front; the chin a little up through it, one breath, no shrug and no head cock (199 / 207, Jaysonn). A key every frame so the baker's cubic has nothing to overshoot. Axes: up (+ = his left), left (+ = nod down / tip forward, - = look up / lean back), forward (+ = roll to his left shoulder). Degrees.",
        "base_clip": "ma_wait",
        "base_frame": 0,
        "fps": FPS,
        "length": LENGTH,
        "loop": False,
        "keys": keys,
    }


def write(script, path):
    # one key a line: 60-odd lines a reader can scan, and a diff that shows which frames moved
    lines = ["{"]
    for k in ("name", "comment", "base_clip", "base_frame", "fps", "length", "loop"):
        lines.append(f' "{k}": {json.dumps(script[k])},')
    lines.append(' "keys": [')
    ks = script["keys"]
    for i, k in enumerate(ks):
        lines.append("  " + json.dumps(k, separators=(", ", ": ")) + ("," if i < len(ks) - 1 else ""))
    lines.append(" ]"); lines.append("}")
    with open(path, "w", newline="\n") as f: f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else OUT
    script = build()
    write(script, out)
    f_y = pchip(HEAD_YAW); f_c = pchip(CHEST_YAW)
    peak = max(abs(f_y(t / 300) - f_y((t - 1) / 300)) * 300 for t in range(1, int(LENGTH * 300) + 1))
    print(f"wrote {out}: {len(script['keys'])} keys over {LENGTH} s at {FPS} fps; the head's world yaw {min(f_y(i / 100) for i in range(201)):+.0f}..{max(f_y(i / 100) for i in range(201)):+.0f}, "
          f"its quickest {peak:.0f} deg/s; the chest {min(f_c(i / 100) for i in range(201)):+.0f}..{max(f_c(i / 100) for i in range(201)):+.0f}")
