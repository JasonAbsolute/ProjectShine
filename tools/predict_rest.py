#!/usr/bin/env python3
"""Predict a character's rest silhouette on the SHARED skeleton, before importing.

    python tools/predict_rest.py <model.glb> [skin.tres]

Skins the mesh with Player.tscn's bone rests + the character's bind poses and
reports the resulting bounding box. A sane character is TALLEST on Y.
"""
import json, struct, re, sys, os

def player_bone_globals(tscn='assets/Player.tscn'):
    txt=open(tscn,encoding='utf-8',errors='replace').read()
    names=dict((int(i),n) for i,n in re.findall(r'bones/(\d+)/name = "([^"]*)"',txt))
    pars=dict((int(i),int(p)) for i,p in re.findall(r'bones/(\d+)/parent = (-?\d+)',txt))
    rests={int(i):[float(x) for x in b.split(',')]
           for i,b in re.findall(r'bones/(\d+)/rest = Transform3D\(([^)]*)\)',txt)}
    def mul(A,B):
        r=[]
        for i in range(3):
            for j in range(3):
                r.append(A[i*3+0]*B[0*3+j]+A[i*3+1]*B[1*3+j]+A[i*3+2]*B[2*3+j])
        o=[A[i*3+0]*B[9]+A[i*3+1]*B[10]+A[i*3+2]*B[11]+A[9+i] for i in range(3)]
        return r+o
    g={}
    for i in sorted(names):
        g[i]=rests[i] if pars[i]<0 else mul(g[pars[i]],rests[i])
    return {names[i]:g[i] for i in g}

def xf(m,v):
    return (m[0]*v[0]+m[1]*v[1]+m[2]*v[2]+m[9],
            m[3]*v[0]+m[4]*v[1]+m[5]*v[2]+m[10],
            m[6]*v[0]+m[7]*v[1]+m[8]*v[2]+m[11])

def load_glb(p):
    d=open(p,'rb').read(); total=struct.unpack_from('<I',d,8)[0]; off=12; js=binc=None
    while off<total:
        cl,ct=struct.unpack_from('<II',d,off); off+=8
        c=d[off:off+cl]; off+=cl
        if ct==0x4E4F534A: js=json.loads(c.decode('utf-8'))
        elif ct==0x004E4942: binc=c
    return js,binc

def main():
    path=sys.argv[1]
    js,binc = load_glb(path) if path.lower().endswith('.glb') else (
        json.load(open(path)), open(os.path.join(os.path.dirname(path),
        json.load(open(path))['buffers'][0]['uri']),'rb').read())
    G=player_bone_globals()

    # A mesh's JOINTS_0 indices are relative to the skin on the NODE that carries
    # it. FinModelUtility emits two skins, so resolve per mesh rather than
    # assuming skins[0] - indexing the wrong joint list overruns or silently
    # skins against the wrong bones.
    mesh_skin={}
    for nd in js['nodes']:
        if 'mesh' in nd and 'skin' in nd:
            mesh_skin.setdefault(nd['mesh'], nd['skin'])

    def skin_data(si):
        sk=js['skins'][si]
        jn=[js['nodes'][j]['name'] for j in sk['joints']]
        a=js['accessors'][sk['inverseBindMatrices']]
        base=js['bufferViews'][a['bufferView']].get('byteOffset',0)+a.get('byteOffset',0)
        b=[]
        for i in range(len(jn)):
            m=struct.unpack_from('<16f',binc,base+i*64)
            b.append([m[0],m[4],m[8], m[1],m[5],m[9], m[2],m[6],m[10], m[12],m[13],m[14]])
        return jn,b
    if len(js['skins'])>1:
        print(f"  note: {len(js['skins'])} skins - resolving per mesh")
    jn,binds = skin_data(mesh_skin.get(0,0))
    COMP={5121:('B',1),5123:('H',2),5126:('f',4)}
    def acc(i):
        A=js['accessors'][i]; n={'VEC3':3,'VEC4':4}[A['type']]; f,cs=COMP[A['componentType']]
        b=js['bufferViews'][A['bufferView']].get('byteOffset',0)+A.get('byteOffset',0)
        return [struct.unpack_from('<'+f*n,binc,b+k*n*cs) for k in range(A['count'])]
    mn=[1e30]*3; mx=[-1e30]*3; n=0; skipped=0
    for mi,me in enumerate(js['meshes']):
      jn,binds = skin_data(mesh_skin.get(mi,0))
      for pr in me['primitives']:
        P=acc(pr['attributes']['POSITION']); J=acc(pr['attributes']['JOINTS_0']); W=acc(pr['attributes']['WEIGHTS_0'])
        for v,ji,wi in zip(P,J,W):
            acc3=[0.0,0.0,0.0]; tot=0.0
            for k in range(4):
                w=wi[k]
                if w<=0: continue
                if ji[k]>=len(jn): skipped+=1; continue
                bn=jn[ji[k]]
                if bn not in G: continue
                p=xf(G[bn], xf(binds[ji[k]], v))
                for c in range(3): acc3[c]+=w*p[c]
                tot+=w
            if tot<=0: continue
            for c in range(3):
                q=acc3[c]/tot
                mn[c]=min(mn[c],q); mx[c]=max(mx[c],q)
            n+=1
    size=[mx[c]-mn[c] for c in range(3)]
    print(f"  skinned {n} verts -> rest silhouette on the shared skeleton")
    print(f"    X={size[0]:8.1f}   Y={size[1]:8.1f}   Z={size[2]:8.1f}")
    tall='XYZ'[max(range(3),key=lambda c:size[c])]
    print(f"    tallest axis: {tall}   {'OK - upright' if tall=='Y' else '<-- NOT upright'}")
main()
