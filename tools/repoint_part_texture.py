"""Point an SMS part's material at the texture its UVs actually sample.

FinModelUtility routinely binds an accessory to a small intensity mask from its
own BMD, when the UVs index into the CHARACTER's body atlas instead. The part
then imports grey or white -- this is what made Piantissimo's helmet white and
Wario's cap flat yellow.

Diagnose by sampling: read the part's TEXCOORD_0, look those coordinates up in
the body atlas, and see whether sensible colours come back.

    python tools/repoint_part_texture.py <part.glb> <atlas.png>
"""
import json, os, shutil, struct, sys


def repoint(part, atlas):
    base = os.path.dirname(part)
    name = os.path.basename(atlas)
    dest = os.path.join(base, name)
    if os.path.abspath(atlas) != os.path.abspath(dest):
        shutil.copy(atlas, dest)

    raw = open(part, 'rb').read()
    jlen, = struct.unpack_from('<I', raw, 12)
    doc = json.loads(raw[20:20 + jlen])
    rest = raw[20 + jlen:]

    doc['images'] = [{'uri': name}]
    doc['textures'] = [{'source': 0, **({'sampler': 0} if doc.get('samplers') else {})}]
    for mat in doc.get('materials', []):
        pbr = mat.setdefault('pbrMetallicRoughness', {})
        pbr['baseColorTexture'] = {'index': 0}
        # The atlas carries the real colours, so drop any tint standing in for them.
        pbr.pop('baseColorFactor', None)
        pbr['metallicFactor'] = 0.0
        mat['alphaMode'] = 'OPAQUE'

    blob = json.dumps(doc, separators=(',', ':')).encode('utf-8')
    blob += b' ' * (-len(blob) % 4)
    out = raw[:12] + struct.pack('<I', len(blob)) + b'JSON' + blob + rest
    out = out[:8] + struct.pack('<I', len(out)) + out[12:]
    open(part, 'wb').write(out)
    print(f"  {os.path.basename(part)} -> {name}")


if __name__ == '__main__':
    repoint(sys.argv[1], sys.argv[2])
