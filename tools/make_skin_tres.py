"""Build a Godot Skin .tres from a glTF's inverse bind matrices.

Godot's importer can emit these, but only through Advanced Import Settings by
hand — and `save_to_file` can't bootstrap them (dead-uid loop). The data is
right there in the glTF, so generate it instead.

Matrix convention, verified against a known-good pair rather than assumed:
glTF stores mat4 column-major, Godot's 12-float Transform3D text form is
row-major, so the basis transposes. Translation carries straight over.

    python tools/make_skin_tres.py <model.gltf|.glb> <out.tres>
"""
import base64, json, os, struct, sys


def _load(path):
    raw = open(path, 'rb').read()
    if raw[:4] == b'glTF':
        jlen = struct.unpack_from('<I', raw, 12)[0]
        doc = json.loads(raw[20:20 + jlen])
        blen = struct.unpack_from('<I', raw, 20 + jlen)[0]
        return doc, raw[28 + jlen:28 + jlen + blen], os.path.dirname(path)
    return json.load(open(path)), None, os.path.dirname(path)


def _buffer(doc, embedded, base, index):
    buf = doc['buffers'][index]
    uri = buf.get('uri')
    if uri is None:
        return embedded
    if uri.startswith('data:'):
        return base64.b64decode(uri.split(',', 1)[1])
    return open(os.path.join(base, uri), 'rb').read()


def build(src, dst):
    doc, embedded, base = _load(src)
    skin = doc['skins'][0]
    acc = doc['accessors'][skin['inverseBindMatrices']]
    view = doc['bufferViews'][acc['bufferView']]
    data = _buffer(doc, embedded, base, view['buffer'])
    start = view.get('byteOffset', 0) + acc.get('byteOffset', 0)

    lines = ['[gd_resource type="Skin" format=3]', '', '[resource]',
             'resource_name = "Skin"', f"bind_count = {acc['count']}"]

    for i in range(acc['count']):
        m = struct.unpack_from('<16f', data, start + i * 64)
        name = doc['nodes'][skin['joints'][i]].get('name', f'bone{i}')
        pose = [m[0], m[4], m[8], m[1], m[5], m[9], m[2], m[6], m[10],
                m[12], m[13], m[14]]
        lines.append(f'bind/{i}/name = &"{name}"')
        lines.append(f'bind/{i}/bone = -1')
        lines.append('bind/%d/pose = Transform3D(%s)'
                     % (i, ', '.join(repr(round(v, 7)) for v in pose)))

    open(dst, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print(f"  {dst}  {acc['count']} binds")


if __name__ == '__main__':
    build(sys.argv[1], sys.argv[2])
