"""Repair an SMS accessory .glb exported by FinModelUtility.

Three faults, all from the same source, all fixed here:

  * I4 (intensity-only) textures come out with the intensity copied into the
    ALPHA channel as well. Combined with the next fault the part renders
    semi-transparent everywhere -- Wario's cap you could see straight through.
  * alphaMode is BLEND even when nothing is meant to be see-through. Alpha
    blending also skips the depth write, so parts sort wrongly against
    themselves.
  * metallicFactor is absent, and glTF defaults it to 1.0, i.e. chrome.

Optionally applies a baseColorFactor too, since I4 textures carry no hue -- the
colour lived in TEV data the exporter drops.

    python tools/fix_sms_part.py <file.glb> [#RRGGBB]
"""
import json, os, struct, sys

from PIL import Image


def fix(path, hexcol=None):
    raw = open(path, 'rb').read()
    jlen, = struct.unpack_from('<I', raw, 12)
    doc = json.loads(raw[20:20 + jlen])
    rest = raw[20 + jlen:]
    base = os.path.dirname(path)

    for mat in doc.get('materials', []):
        mat['alphaMode'] = 'OPAQUE'
        mat.pop('alphaCutoff', None)
        pbr = mat.setdefault('pbrMetallicRoughness', {})
        pbr['metallicFactor'] = 0.0
        pbr.setdefault('roughnessFactor', 1.0)
        if hexcol:
            h = hexcol.lstrip('#')
            pbr['baseColorFactor'] = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)] + [1.0]

    cleaned = []
    for img in doc.get('images', []):
        uri = img.get('uri')
        if not uri:
            continue
        p = os.path.join(base, uri)
        if not os.path.exists(p):
            continue
        im = Image.open(p).convert('RGBA')
        r = list(im.getchannel('R').getdata())
        a = list(im.getchannel('A').getdata())
        # Only touch the artifact: alpha that merely mirrors intensity. A real
        # cut-out mask won't match its own red channel on every pixel.
        if all(x == y for x, y in zip(r, a)):
            im.putalpha(255)
            im.save(p)
            cleaned.append(uri)

    blob = json.dumps(doc, separators=(',', ':')).encode('utf-8')
    blob += b' ' * (-len(blob) % 4)
    out = raw[:12] + struct.pack('<I', len(blob)) + b'JSON' + blob + rest
    out = out[:8] + struct.pack('<I', len(out)) + out[12:]
    open(path, 'wb').write(out)
    print(f"  {os.path.basename(path)}: OPAQUE, metallic 0"
          + (f", tint {hexcol}" if hexcol else "")
          + (f", alpha reset on {len(cleaned)} texture(s)" if cleaned else ""))


if __name__ == '__main__':
    fix(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
