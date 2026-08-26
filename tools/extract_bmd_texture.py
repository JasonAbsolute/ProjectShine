"""Decode a texture out of a BMD's TEX1 section.

FinModelUtility mis-decodes RGB565 (Waluigi's eyes come out as dither noise),
so pull those directly. GameCube textures are stored in 4x4 tiles, which is the
part naive decoders get wrong.

    python tools/extract_bmd_texture.py <model.bmd> <index> <out.png>
"""
import struct, sys

from PIL import Image

FORMATS = {0: 'I4', 1: 'I8', 2: 'IA4', 3: 'IA8', 4: 'RGB565',
           5: 'RGB5A3', 6: 'RGBA32', 8: 'C4', 9: 'C8', 10: 'C14X2', 14: 'CMPR'}


def _tex1(data):
    off = 0x20
    while off < len(data) - 8:
        magic = data[off:off + 4]
        size = struct.unpack_from('>I', data, off + 4)[0]
        if magic == b'TEX1':
            return off, struct.unpack_from('>H', data, off + 8)[0], \
                   struct.unpack_from('>I', data, off + 0x0C)[0]
        if size <= 0:
            break
        off += size
    raise SystemExit('no TEX1 section')


def _rgb565(buf, w, h):
    px = [(0, 0, 0, 255)] * (w * h)
    i = 0
    for ty in range(0, h, 4):
        for tx in range(0, w, 4):
            for y in range(4):
                for x in range(4):
                    v = struct.unpack_from('>H', buf, i)[0]
                    i += 2
                    if ty + y < h and tx + x < w:
                        px[(ty + y) * w + tx + x] = (
                            ((v >> 11) & 0x1F) * 255 // 31,
                            ((v >> 5) & 0x3F) * 255 // 63,
                            (v & 0x1F) * 255 // 31,
                            255,
                        )
    return px


def extract(path, index, out):
    d = open(path, 'rb').read()
    base, count, hdr = _tex1(d)
    if index >= count:
        raise SystemExit(f'only {count} textures')
    b = base + hdr + index * 0x20
    fmt, w, h = d[b], *struct.unpack_from('>HH', d, b + 2)
    data_off = b + struct.unpack_from('>I', d, b + 0x1C)[0]
    name = FORMATS.get(fmt, f'?{fmt}')
    if name != 'RGB565':
        raise SystemExit(f'tex{index} is {name}; only RGB565 handled here')
    img = Image.new('RGBA', (w, h))
    img.putdata(_rgb565(d[data_off:], w, h))
    img.save(out)
    print(f"  tex{index} {name} {w}x{h} -> {out}")


if __name__ == '__main__':
    extract(sys.argv[1], int(sys.argv[2]), sys.argv[3])
