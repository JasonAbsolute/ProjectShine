"""lm_sky_rip.py -- SMS 208: pull Luigi's Mansion's SKY archives out of the GameCube disc image (no game data leaves the PC except the few sky files).
Reads the GC file system table, extracts the /Iwamoto/vr*.szp archives (Yaz0-compressed RARC), decompresses and unpacks them into <out>/<archive>/,
and lists the DOL's strings near 'vrball' / 'vrKoopa' (the sky descriptor tables). Stdlib only.
    python lm_sky_rip.py <disc.iso> <out dir>"""
import sys, os, struct

iso_path, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
f = open(iso_path, "rb")
def rd(off, n): f.seek(off); return f.read(n)
def u32(b, o=0): return struct.unpack(">I", b[o:o + 4])[0]

game = rd(0, 6).decode("ascii", "replace"); magic = u32(rd(0x1C, 4)); title = rd(0x20, 64).split(b"\0")[0].decode("ascii", "replace")
print(f"disc {game} magic {magic:08x} ({'GameCube' if magic == 0xC2339F3D else 'NOT a GC disc'}) title {title}")
dol_off, fst_off, fst_size = u32(rd(0x420, 4)), u32(rd(0x424, 4)), u32(rd(0x428, 4))
fst = rd(fst_off, fst_size)
n_entries = u32(fst, 8); names = fst[n_entries * 12:]
def name_at(o): e = names.index(b"\0", o); return names[o:e].decode("ascii", "replace")
# walk the tree
files = {}   # path -> (offset, size)
def walk(i, end, prefix):
    while i < end:
        e = fst[i * 12:(i + 1) * 12]
        typ = e[0]; name_off = int.from_bytes(e[1:4], "big"); a = u32(e, 4); b = u32(e, 8)
        nm = name_at(name_off)
        if typ == 1:
            walk(i + 1, b, prefix + nm + "/"); i = b
        else:
            files[prefix + nm] = (a, b); i += 1
walk(1, n_entries, "/")
print(f"FST: {n_entries} entries, {len(files)} files; DOL at {dol_off:#x}")
sky = sorted(p for p in files if "/vr" in p.lower() or "sky" in p.lower())
for p in sky: print(f"  {p}  {files[p][1]} bytes")

def yay0(data):
    if data[:4] != b"Yay0": return data
    size, link_off, chunk_off = u32(data, 4), u32(data, 8), u32(data, 12)
    mask_off = 16; dst = bytearray(); mask = 0; bits = 0
    while len(dst) < size:
        if bits == 0: mask = u32(data, mask_off); mask_off += 4; bits = 32
        if mask & 0x80000000:
            dst.append(data[chunk_off]); chunk_off += 1
        else:
            link = struct.unpack(">H", data[link_off:link_off + 2])[0]; link_off += 2
            dist = (link & 0x0FFF) + 1; n = link >> 12
            if n == 0: n = data[chunk_off] + 0x12; chunk_off += 1
            else: n += 2
            start = len(dst) - dist
            for k in range(n): dst.append(dst[start + k])
        mask = (mask << 1) & 0xFFFFFFFF; bits -= 1
    return bytes(dst)

def yaz0(data):
    if data[:4] == b"Yay0": return yay0(data)
    if data[:4] != b"Yaz0": return data
    size = u32(data, 4); src = 16; dst = bytearray()
    code = 0; bits = 0
    while len(dst) < size:
        if bits == 0: code = data[src]; src += 1; bits = 8
        if code & 0x80:
            dst.append(data[src]); src += 1
        else:
            b1, b2 = data[src], data[src + 1]; src += 2
            dist = ((b1 & 0x0F) << 8 | b2) + 1; n = b1 >> 4
            if n == 0: n = data[src] + 0x12; src += 1
            else: n += 2
            start = len(dst) - dist
            for k in range(n): dst.append(dst[start + k])
        code <<= 1; bits -= 1
    return bytes(dst)

def rarc_unpack(data, dest):
    assert data[:4] == b"RARC", data[:4]
    data_start = 0x20 + u32(data, 0x0C)
    n_nodes, node_off, n_files, file_off, str_off = u32(data, 0x20), 0x20 + u32(data, 0x24), u32(data, 0x28), 0x20 + u32(data, 0x2C), 0x20 + u32(data, 0x34)
    def sname(o): e = data.index(b"\0", str_off + o); return data[str_off + o:e].decode("ascii", "replace")
    nodes = []
    for k in range(n_nodes):
        nb = data[node_off + k * 16:node_off + (k + 1) * 16]
        nodes.append((nb[:4].decode("ascii", "replace"), sname(u32(nb, 4)), struct.unpack(">H", nb[10:12])[0], u32(nb, 12)))
    def unpack_node(k, prefix):
        typ, nm, cnt, first = nodes[k]
        for j in range(first, first + cnt):
            fe = data[file_off + j * 20:file_off + (j + 1) * 20]
            fid = struct.unpack(">H", fe[0:2])[0]; ftype = fe[4]; noff = int.from_bytes(fe[5:8], "big"); a = u32(fe, 8); b = u32(fe, 12)
            fnm = sname(noff)
            if fnm in (".", ".."): continue
            if ftype & 0x02:
                os.makedirs(os.path.join(dest, prefix, fnm), exist_ok=True); unpack_node(a, os.path.join(prefix, fnm))
            else:
                blob = data[data_start + a:data_start + a + b]
                if blob[:4] in (b"Yaz0", b"Yay0"): blob = yaz0(blob)
                path = os.path.join(dest, prefix, fnm); os.makedirs(os.path.dirname(path), exist_ok=True)
                open(path, "wb").write(blob); print(f"    {os.path.join(prefix, fnm)}  {len(blob)} bytes  {blob[:8]!r}")
    unpack_node(0, "")

for p in sky:
    off, size = files[p]
    raw = rd(off, size); dec = yaz0(raw)
    base = os.path.splitext(os.path.basename(p))[0]
    open(os.path.join(out, base + ".arc"), "wb").write(dec)
    print(f"{p}: {size} -> {len(dec)} bytes {dec[:4]!r}")
    if dec[:4] == b"RARC": rarc_unpack(dec, os.path.join(out, base))

# the DOL's sky strings: every NUL-terminated string mentioning vrball / vrKoopa / Iwamoto/vr, with its file offset
dol_size = files.get("/sys/main.dol", (0, 0))[1]
dol_hdr = rd(dol_off, 0x100)
# the DOL's section table: 18 sections (7 text + 11 data): offsets at 0x00, sizes at 0x90 -- the file's total is the furthest section end
ends = [u32(dol_hdr, i * 4) + u32(dol_hdr, 0x90 + i * 4) for i in range(18) if u32(dol_hdr, i * 4)]
dol = rd(dol_off, max(ends))
print(f"DOL {len(dol)} bytes; strings:")
import re
for m in re.finditer(rb"[\x20-\x7e]{4,}", dol):
    s = m.group(0)
    if b"vrball" in s.lower() or b"vrkoopa" in s.lower() or b"iwamoto/vr" in s.lower() or b"vrbox" in s.lower():
        print(f"  {m.start():#08x}: {s.decode('ascii')}")
