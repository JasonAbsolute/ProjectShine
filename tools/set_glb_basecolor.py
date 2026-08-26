"""Set baseColorFactor on every material in a .glb.

Blender's glTF exporter drops a MixRGB tint rather than folding it into
baseColorFactor, so set it on the file directly. glTF multiplies the factor by
baseColorTexture, which is exactly what an intensity-only SMS texture needs to
get its colour back.

    python tools/set_glb_basecolor.py <file.glb> <#RRGGBB>
"""
import json, struct, sys


def set_color(path, hexcol):
    rgb = [int(hexcol[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    raw = open(path, 'rb').read()
    jlen, = struct.unpack_from('<I', raw, 12)
    doc = json.loads(raw[20:20 + jlen])
    rest = raw[20 + jlen:]

    for mat in doc.get('materials', []):
        pbr = mat.setdefault('pbrMetallicRoughness', {})
        pbr['baseColorFactor'] = rgb + [1.0]
        pbr.setdefault('metallicFactor', 0.0)

    blob = json.dumps(doc, separators=(',', ':')).encode('utf-8')
    blob += b' ' * (-len(blob) % 4)          # JSON chunk pads with spaces
    out = (raw[:12]
           + struct.pack('<I', len(blob)) + b'JSON' + blob
           + rest)
    out = out[:8] + struct.pack('<I', len(out)) + out[12:]
    open(path, 'wb').write(out)
    print(f"  {path} <- #{hexcol}")


if __name__ == '__main__':
    set_color(sys.argv[1], sys.argv[2].lstrip('#'))
