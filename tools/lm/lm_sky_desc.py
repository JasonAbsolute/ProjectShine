"""lm_sky_desc.py -- SMS 208: Luigi's Mansion's SKY DESCRIPTOR tables out of the disc's main.dol: the per-map table lbl_80300C7C[map] of pointers to
descriptors (archive path, model, animations) -- which sky each boss map draws and with which .btk / .brk. Stdlib only.
    python lm_sky_desc.py <disc.iso>"""
import sys, struct, re

f = open(sys.argv[1], "rb")
def rd(off, n): f.seek(off); return f.read(n)
def u32(b, o=0): return struct.unpack(">I", b[o:o + 4])[0]
dol_off = u32(rd(0x420, 4))
hdr = rd(dol_off, 0x100)
secs = []   # (file offset, virtual address, size)
for i in range(18):
    o, a, s = u32(hdr, i * 4), u32(hdr, 0x48 + i * 4), u32(hdr, 0x90 + i * 4)
    if o and s: secs.append((o, a, s))
dol = rd(dol_off, max(o + s for o, a, s in secs))
def va2off(va):
    for o, a, s in secs:
        if a <= va < a + s: return o + (va - a)
    return None
def off2va(off):
    for o, a, s in secs:
        if o <= off < o + s: return a + (off - o)
    return None
def cstr(va):
    o = va2off(va)
    if o is None: return None
    e = dol.index(b"\0", o)
    s = dol[o:e]
    return s.decode("ascii") if all(0x20 <= c < 0x7f for c in s) and len(s) < 80 else None

# every string that names a sky file
names = {}
for m in re.finditer(rb"[\x20-\x7e]{4,}", dol):
    s = m.group(0).decode("ascii")
    if any(k in s.lower() for k in ("vrb", "vrball", "vrkoopa", "iwamoto/vr", "k_vrbx", ".btk", ".brk", ".bck")) and "/" in s:
        names[off2va(m.start())] = s
for va in sorted(names): print(f"  string {va:08x}: {names[va]}")

# the per-map table: 0x80300C7C in the USA DOL (the port's notes) -- 14 maps (0..13); each entry points at a descriptor
TABLE = 0x80300C7C
print(f"\nmap table {TABLE:08x}:")
descs = {}
for mp in range(14):
    p = u32(dol, va2off(TABLE + mp * 4))
    descs.setdefault(p, []).append(mp)
    print(f"  map {mp:2d} -> descriptor {p:08x}")
# each descriptor: dump 24 words, naming the ones that point at strings or at other pointers
print()
for p in sorted(descs):
    print(f"descriptor {p:08x} (maps {descs[p]}):")
    o = va2off(p)
    if o is None: print("   (not in the DOL)"); continue
    for k in range(24):
        w = u32(dol, o + k * 4)
        s = cstr(w) if 0x80000000 <= w < 0x80400000 else None
        note = f'"{s}"' if s else ""
        if not s and 0x80000000 <= w < 0x80400000:
            oo = va2off(w)
            if oo is not None:
                w2 = u32(dol, oo); s2 = cstr(w2) if 0x80000000 <= w2 < 0x80400000 else None
                if s2: note = f'-> {w2:08x} "{s2}"'
                else:
                    # a list of string pointers?
                    lst = []
                    for j in range(8):
                        wj = u32(dol, oo + j * 4); sj = cstr(wj) if 0x80000000 <= wj < 0x80400000 else None
                        if sj: lst.append(sj)
                        else: break
                    if lst: note = "-> [" + ", ".join(lst) + "]"
        print(f"   +{k * 4:02x}: {w:08x}  {note}")
