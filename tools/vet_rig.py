#!/usr/bin/env python3
"""
Step 1 of the ma_mdl1 runbook: vet a character export BEFORE importing it.

    python tools/vet_rig.py <model.glb|model.gltf>

Reports every way the file can be quietly unusable. Gates only on things that
actually break a character; per-bone bind differences between characters are
normal and are NOT a failure. Exit code 0 = eligible, 1 = do not import.
"""
import json, struct, os, sys, math

MARIO_RIG = {
 'mdl1','center','jnt_waist','chn_chest','jnt_chest','jnt_head','M_head','M_head_cap1',
 'jnt_leg_R1','jnt_leg_R2','chn_foot_R','jnt_foot_R','M_foot_R',
 'jnt_leg_L1','jnt_leg_L2','chn_foot_L','jnt_foot_L','M_foot_L',
 'jnt_sldr_R','jnt_arm_R1','jnt_arm_R2','jnt_hand_R','M_hand1_R',
 'jnt_sldr_L','eff_sldr_L','jnt_arm_L1','jnt_arm_L2','jnt_hand_L','M_hand1_L'}

def load(path):
    if path.lower().endswith('.glb'):
        d = open(path,'rb').read()
        total = struct.unpack_from('<I', d, 8)[0]
        off, js, binc = 12, None, b''
        while off < total:
            clen, ctype = struct.unpack_from('<II', d, off); off += 8
            chunk = d[off:off+clen]; off += clen
            if ctype == 0x4E4F534A: js = json.loads(chunk.decode('utf-8'))
            elif ctype == 0x004E4942: binc = chunk
        return js, binc
    js = json.load(open(path))
    uri = js['buffers'][0].get('uri','')
    binc = open(os.path.join(os.path.dirname(path), uri),'rb').read() if uri else b''
    return js, binc

def bind_origins(js, binc):
    """Bone name -> inverse-bind translation."""
    sk = js['skins'][0]
    if 'inverseBindMatrices' not in sk: return {}
    a = js['accessors'][sk['inverseBindMatrices']]
    base = js['bufferViews'][a['bufferView']].get('byteOffset',0) + a.get('byteOffset',0)
    out = {}
    for i, j in enumerate(sk['joints']):
        m = struct.unpack_from('<16f', binc, base + i*64)
        out[js['nodes'][j].get('name')] = (m[12], m[13], m[14])
    return out

def mesh_size(js):
    mn, mx = [1e30]*3, [-1e30]*3
    for pr in js['meshes'][0]['primitives']:
        a = js['accessors'][pr['attributes']['POSITION']]
        for k in range(3):
            mn[k] = min(mn[k], a['min'][k]); mx[k] = max(mx[k], a['max'][k])
    return [mx[k]-mn[k] for k in range(3)], mn, mx

# The four orientations an SMS rip realistically shows up in.
ORIENTS = {
    'as-is (Y-up)'      : lambda v: (v[0],  v[1],  v[2]),
    'Z-up (x,-z,y)'     : lambda v: (v[0], -v[2],  v[1]),
    'Z-up (x,z,-y)'     : lambda v: (v[0],  v[2], -v[1]),
    '180 about X'       : lambda v: (v[0], -v[1], -v[2]),
}

def main():
    path = sys.argv[1]
    ref  = 'models/Luigi/Godot/Luigi.gltf'
    if '--ref' in sys.argv: ref = sys.argv[sys.argv.index('--ref')+1]

    js, binc = load(path)
    fails, warns = [], []
    print(f"\n=== {path} ===")

    nsk = len(js.get('skins', []))
    print(f"  skins={nsk}  meshes={len(js.get('meshes',[]))}  anims={len(js.get('animations',[]))}")
    if nsk == 0: print("  FAIL: no skin"); sys.exit(1)
    if nsk > 1: fails.append(f"{nsk} skins (expect 1) - broken or doubled export")

    names = [js['nodes'][j].get('name') for j in js['skins'][0]['joints']]
    s = set(names)
    print(f"  joints={len(names)}  rig name match: {s == MARIO_RIG}")
    if s != MARIO_RIG:
        if s - MARIO_RIG: fails.append(f"extra bones: {sorted(s-MARIO_RIG)}")
        if MARIO_RIG - s: fails.append(f"missing bones: {sorted(MARIO_RIG-s)}")

    P = bind_origins(js, binc)
    if not P:
        fails.append("no inverseBindMatrices")
    elif all(max(abs(c) for c in v) < 1e-4 for v in P.values()):
        fails.append("DEGENERATE: every bind origin is (0,0,0) - rest pose lost")
    else:
        # Up-axis, read off `center` only. Mario and Luigi both put the -58 on Y.
        # Do NOT diff every bone against a reference: characters legitimately
        # differ in per-bone axis conventions (Luigi's head sits 99.9 units from
        # Mario's and animates perfectly), because each mesh ships with its own
        # matching Skin. Only whole-model orientation is worth flagging.
        c = P.get('center')
        if c:
            axis = max(range(3), key=lambda k: abs(c[k]))
            print(f"  center bind origin: ({c[0]:.2f}, {c[1]:.2f}, {c[2]:.2f})  -> up axis is {'XYZ'[axis]}")
            if axis != 1:
                warns.append(f"up axis is {'XYZ'[axis]}, not Y - re-export with +Y Up or it lands on its back")

    size, mn, mx = mesh_size(js)
    print(f"  mesh size  X={size[0]:.1f}  Y={size[1]:.1f}  Z={size[2]:.1f}   (Luigi ref 150.2 x 122.3 x 77.5)")
    tall = max(size)
    if not (0.6 <= tall/150.0 <= 1.6):
        warns.append(f"largest mesh axis {tall:.1f} is far from the ~150 the rig implies")

    imgs = [i.get('uri','<embedded>') for i in js.get('images',[])]
    print(f"  images: {imgs if imgs else 'NONE'}")
    if not imgs: fails.append("no textures - materials are likely vertex-color only")

    mats = js.get('materials', [])
    absent = sum(1 for m in mats if 'metallicFactor' not in m.get('pbrMetallicRoughness',{}))
    print(f"  materials={len(mats)}  missing metallicFactor: {absent}")
    if absent: warns.append(f"{absent} materials omit metallicFactor (glTF default 1.0 = chrome); set to 0")

    print()
    for w in warns: print(f"  WARN  {w}")
    for f in fails: print(f"  FAIL  {f}")
    if not fails:
        print("\n  ELIGIBLE - proceed to Step 2" + ("  (fix the warnings first)" if warns else ""))
    else:
        print("\n  NOT ELIGIBLE - do not import")
    sys.exit(1 if fails else 0)

main()
