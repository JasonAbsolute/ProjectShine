"""Generate a stroke-order map from brush paths instead of drawn frames.

Companion to bake_stroke_order.py. That one recovers order from an existing
frame sequence; this one synthesises a letter from scratch, which is what you
want for a character the source art never had.

A letter is a list of strokes, each a list of normalised (x, y) control points
in the order the brush travels. Because a pixel's order value is simply *when
the brush reached it*, shape and timing come out of the same pass -- so a new
letter costs a few control points rather than a full frame sequence.

    python tools/make_stroke.py L Font/CharacterSelect/StrokeL.png
"""
import math, random, sys
from PIL import Image, ImageFilter

N = 512

# Normalised control points, in brush order. Tuned against the extracted M:
# strokes run ~9% of the canvas wide and taper at both ends.
LETTERS = {
    'L': {
        'strokes': [
            [(0.368, 0.19), (0.342, 0.39), (0.322, 0.59), (0.318, 0.795)],
            [(0.318, 0.815), (0.46, 0.845), (0.61, 0.815), (0.745, 0.735)],
        ],
        'accents': [[(0.815, 0.145), (0.775, 0.315)]],
    },
    'P': {
        'strokes': [
            [(0.355, 0.19), (0.335, 0.40), (0.322, 0.62), (0.315, 0.845)],
            [(0.345, 0.215), (0.545, 0.172), (0.668, 0.292), (0.572, 0.420),
             (0.362, 0.458)],
        ],
        'accents': [[(0.815, 0.20), (0.782, 0.372)]],
    },
    'W': {
        'strokes': [
            [(0.175, 0.20), (0.245, 0.50), (0.315, 0.815)],
            [(0.315, 0.825), (0.395, 0.545), (0.455, 0.295)],
            [(0.455, 0.305), (0.525, 0.565), (0.595, 0.825)],
            [(0.595, 0.835), (0.675, 0.545), (0.745, 0.245)],
        ],
        'accents': [],
    },

    'Y': {
        'strokes': [
            [(0.275, 0.19), (0.375, 0.375), (0.475, 0.525)],
            [(0.675, 0.185), (0.585, 0.355), (0.485, 0.525)],
            [(0.478, 0.520), (0.482, 0.685), (0.475, 0.845)],
        ],
        'accents': [[(0.815, 0.20), (0.782, 0.372)]],
    },

    'K': {
        'strokes': [
            [(0.335, 0.19), (0.320, 0.40), (0.310, 0.62), (0.305, 0.845)],
            [(0.640, 0.205), (0.505, 0.355), (0.358, 0.500)],
            [(0.382, 0.498), (0.545, 0.668), (0.685, 0.842)],
        ],
        'accents': [[(0.845, 0.20), (0.812, 0.372)]],
    },
}


def spline(pts, samples=420):
    """Catmull-Rom through the control points, so a few points give a smooth path."""
    if len(pts) == 2:
        (x0, y0), (x1, y1) = pts
        return [(x0 + (x1 - x0) * i / samples, y0 + (y1 - y0) * i / samples)
                for i in range(samples + 1)]
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(len(p) - 3):
        p0, p1, p2, p3 = p[i:i + 4]
        for s in range(samples // (len(p) - 3)):
            t = s / (samples / (len(p) - 3))
            t2, t3 = t * t, t * t * t
            out.append((
                0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t
                       + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2
                       + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3),
                0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t
                       + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2
                       + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3),
            ))
    return out


# A real brush tip is a flat chisel, not a dot. Held at a fixed angle, it lays
# down a wide mark travelling across the flat and a narrow one travelling along
# it -- which is what separates a painted stroke from an extruded tube.
PEN_ANGLE = -0.60      # radians the nib is held at
ROUGHNESS = 40.0       # outline wander; pairs with GRAIN below

# The source M is a hard italic -- its centroid drifts 0.69 x-units per y-unit.
# Letters are authored upright and sheared here, so the lean is one number
# rather than something baked into every control point.
GRAIN = 14.0           # wander wavelength; fine grain reads as a bad scan, not paint
SLANT = 0.45
BASE_Y = 0.82          # height the shear pivots about, roughly the baseline
NIB_RATIO = 0.34       # short axis / long axis; lower is a flatter chisel


def stamp(order, alpha, path, t0, t1, w, rng):
    """Drag the nib along a path, recording when each pixel is first touched."""
    n = len(path)
    ph = rng.uniform(0.0, 6.28)
    # A hand doesn't lay every stroke at the same weight, and a loaded brush
    # swells or thins across a single stroke. Without these the letter reads as
    # uniform pipe-work however good the outline is.
    base = rng.uniform(0.86, 1.14)
    grad = rng.uniform(-0.30, 0.30)
    ca, sa = math.cos(PEN_ANGLE), math.sin(PEN_ANGLE)
    for i, (x, y) in enumerate(path):
        f = i / max(n - 1, 1)
        # Barely any taper: the M runs at full width and stops dead, letting the
        # nib's own flat footprint form the angled end. A long taper reads as a
        # felt tip instead.
        e = 0.05
        taper = min(1.0, f / e, (1 - f) / e)
        taper = taper * taper * (3 - 2 * taper)
        # Smooth low-frequency swell rather than per-stamp jitter, which would
        # roughen the width at a much finer scale than a loaded brush does.
        wob = (base * (1.0 + grad * (f - 0.5))
               + 0.040 * math.sin(f * 4.1 + ph)
               + 0.022 * math.sin(f * 9.3 + ph * 2))
        r = w * N * (0.88 + 0.12 * taper) * wob
        a_ax, b_ax = r, r * NIB_RATIO
        t = round((t0 + (t1 - t0) * f) * 255)
        cx, cy, ri = x * N, y * N, int(r) + 2
        for py in range(max(0, int(cy - ri)), min(N, int(cy + ri) + 1)):
            for px in range(max(0, int(cx - ri)), min(N, int(cx + ri) + 1)):
                dx, dy = px - cx, py - cy
                u, v = dx * ca + dy * sa, -dx * sa + dy * ca
                q = (u / a_ax) ** 2 + (v / b_ax) ** 2
                if q > 1.62:
                    continue
                j = py * N + px
                # Order is recorded on a slightly fatter footprint than alpha, so
                # that roughening can push the edge outwards without exposing
                # pixels that were never assigned a time.
                if order[j] == 256:
                    order[j] = t
                if q <= 1.0:
                    alpha[j] = 255


def roughen(alpha, amount=0.0, scale=3.2, grain=6.0, fine=0.55):
    """Displace the outline with coherent noise, for a ragged painted edge.

    Off by default. The extracted M scores as "rough" only because of its sharp
    corners and flat cut ends -- its actual outline is smooth, so adding noise
    here reads as a crumbly scan rather than as paint. Kept for tuning; past
    about 25 it visibly degrades.

    Blurring the shape turns its edge into a ramp, so adding noise before
    re-thresholding shifts the outline in and out. The noise is normalised to
    unit deviation first because blurring white noise crushes its amplitude by
    an amount that depends on the radius -- without that, `amount` would mean
    something different every time `grain` changed.
    """
    al = Image.new('L', (N, N)); al.putdata(alpha)
    soft = list(al.filter(ImageFilter.GaussianBlur(scale)).getdata())

    # Two octaves: a coarse one wanders the outline the way a loaded brush does,
    # a finer one puts nicks in it. Coarse alone looks melted, fine alone looks
    # like sandpaper -- the M's edge has both.
    def octave(radius):
        o = list(Image.effect_noise((N, N), 128)
                 .filter(ImageFilter.GaussianBlur(radius)).getdata())
        mu = sum(o) / len(o)
        s_ = (sum((v - mu) ** 2 for v in o) / len(o)) ** 0.5 or 1.0
        return [(v - mu) / s_ for v in o]

    coarse, fn = octave(grain), octave(grain / 3.0)
    noise = [128.0 + coarse[i] + fn[i] * fine for i in range(N * N)]
    m, sd = 128.0, 1.0
    # Perturb only the blurred edge band. Applied everywhere, the noise would
    # also flip isolated pixels out in the empty margin and speckle the canvas.
    out = [0] * (N * N)
    for i in range(N * N):
        v = soft[i]
        if v >= 248:
            out[i] = 255
        elif v > 6:
            out[i] = 255 if v + (noise[i] - m) / sd * amount > 128 else 0
    return out


def shear_all(segs):
    """Lean the letter over, then recentre so it still sits in the frame."""
    out = [[(x + SLANT * (BASE_Y - y), y) for x, y in pts] for pts in segs]
    xs = [x for pts in out for x, _ in pts]
    shift = 0.5 - (min(xs) + max(xs)) / 2.0
    return [[(x + shift, y) for x, y in pts] for pts in out]


def build(name, out, width=0.086, seed=7):
    spec = LETTERS[name]
    rng = random.Random(seed)
    order = [256] * (N * N)
    alpha = [0] * (N * N)

    segs = [(s, False) for s in spec['strokes']] + [(a, True) for a in spec['accents']]
    leaned = shear_all([pts for pts, _ in segs])
    segs = [(leaned[i], segs[i][1]) for i in range(len(segs))]
    # Accents land last, matching the flicks the M finishes on.
    total = len(segs)
    for i, (pts, is_accent) in enumerate(segs):
        t0, t1 = i / total, (i + 1) / total
        stamp(order, alpha, spline(pts), t0, t1,
              width * (0.62 if is_accent else 1.0), rng)

    if ROUGHNESS > 0.0:
        alpha = roughen(alpha, amount=ROUGHNESS, grain=GRAIN, fine=0.0, scale=3.6)
    order = [255 if v == 256 else v for v in order]
    om = Image.new('L', (N, N)); om.putdata(order)
    al = Image.new('L', (N, N)); al.putdata(alpha)
    al = al.filter(ImageFilter.GaussianBlur(0.8))       # soften the stamped edge
    om = om.filter(ImageFilter.GaussianBlur(1.4))
    Image.merge('RGBA', (om, om, om, al)).save(out)
    print(f"  {out}  {N}x{N}  {len(segs)} strokes")


if __name__ == '__main__':
    build(sys.argv[1], sys.argv[2])
