"""Unpack a Nintendo RARC archive (.arc / uncompressed .szs).

SMS character archives are RARC containers holding the BMD model, its BCK
animations and textures. FinModelUtility's `convert` wants the BMD directly,
so the archive has to come apart first.

    python tools/unpack_rarc.py Wario.arc out_dir [name_filter]
"""
import os, struct, sys


def unpack(path, out, keep=None):
    d = open(path, 'rb').read()
    if d[:4] == b'Yaz0':
        sys.exit(f"{path} is Yaz0-compressed; decompress it first")
    if d[:4] != b'RARC':
        sys.exit(f"{path} is not RARC (magic {d[:4]!r})")

    data_off = struct.unpack_from('>I', d, 0x0C)[0] + 0x20
    n_nodes, node_off, n_files, file_off, _, str_off = struct.unpack_from('>IIIIII', d, 0x20)
    node_off += 0x20; file_off += 0x20; str_off += 0x20

    def name_at(o):
        e = d.index(b'\0', str_off + o)
        return d[str_off + o:e].decode('shift_jis', 'replace')

    written = 0
    for n in range(n_nodes):
        b = node_off + n * 0x10
        count, first = struct.unpack_from('>HI', d, b + 0x0A)
        dir_name = name_at(struct.unpack_from('>I', d, b + 0x04)[0])
        for i in range(count):
            e = file_off + (first + i) * 0x14
            fid, _, packed, off, size = struct.unpack_from('>HHIII', d, e)
            flags = (packed >> 24) & 0xFF
            nm = name_at(packed & 0x00FFFFFF)
            if fid == 0xFFFF or not (flags & 0x01) or nm in ('.', '..'):
                continue
            if keep and keep.lower() not in nm.lower():
                continue
            dest = os.path.join(out, dir_name, nm)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest, 'wb') as f:
                f.write(d[data_off + off: data_off + off + size])
            written += 1
    print(f"  {os.path.basename(path)}: wrote {written} file(s) to {out}")


if __name__ == '__main__':
    unpack(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
