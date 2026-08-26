"""Collapse a progressive-draw frame sequence into a single stroke-order map.

The SMS/Eclipse char-select paint frames are strictly additive: ink is only ever
added between frames, never erased. That means every pixel has a well-defined
moment it got painted, so all N frames can be flattened into one texture:

    R = when this pixel was painted   (0 = first stroke, 255 = last)
    A = the finished shape's alpha

A shader then reveals it with a single 0-1 `progress` uniform, which is both
smaller and smoother than flipping between the original frames.

    python tools/bake_stroke_order.py <frame_dir> <out.png> [size]
"""
import glob, sys, os
from PIL import Image, ImageFilter

src, out = sys.argv[1], sys.argv[2]
N = int(sys.argv[3]) if len(sys.argv) > 3 else 512
THRESH = 96

files = glob.glob(os.path.join(src, '*.png'))
if not files:
    sys.exit(f"no frames in {src}")

# Frames are hash-named and carry no order, so recover it from ink coverage:
# a strictly additive sequence is sorted by how much paint is down.
frames = []
for f in files:
    a = Image.open(f).convert('RGBA').getchannel('A').resize((N, N), Image.BILINEAR)
    d = list(a.getdata())
    frames.append((sum(1 for p in d if p > THRESH), d))
frames.sort(key=lambda t: t[0])
seq = [d for _, d in frames]

# First frame in which each pixel crosses the threshold.
last = len(seq) - 1
order = [255] * (N * N)
for i, d in enumerate(seq):
    v = round(i / last * 255)
    for p in range(N * N):
        if order[p] == 255 and d[p] > THRESH:
            order[p] = v

final = seq[-1]
alpha = Image.new('L', (N, N)); alpha.putdata(final)
omap  = Image.new('L', (N, N)); omap.putdata(order)

# Light blur softens the 16 discrete bands into a continuous ramp so the reveal
# doesn't step. Masked to the shape so it can't bleed order values outward into
# transparent pixels, which would make edges reveal at the wrong time.
blur = omap.filter(ImageFilter.GaussianBlur(N / 340))
omap = Image.composite(blur, omap, alpha.point(lambda p: 255 if p > THRESH else 0))

img = Image.merge('RGBA', (omap, omap, omap, alpha))
img.save(out)
print(f"  {out}  {N}x{N}  {os.path.getsize(out)/1024:.0f}KB  from {len(files)} frames")
