"""strip_import_defaults.py -- strip Godot's inherited scene-import defaults (the project's [importer_defaults] scene block: Mario's ~200 animation
slices, ~11 MB of text) from .import files of models that carry no animations. Godot copies the dock's last 'Set as Default' into every new
scene import; the nozzle items' six .import files came in at 11.4 MB each (2026-10-25). Keeps everything else in the file.
usage: python strip_import_defaults.py <file.import> [...]"""
import pathlib
import sys

for arg in sys.argv[1:]:
    f = pathlib.Path(arg)
    s = f.read_text(encoding="utf-8")
    before = len(s)
    a = s.find("_subresources={")
    if a < 0:
        print(f"{f.name}: no _subresources block ({before} B)")
        continue
    depth = 0
    i = a + len("_subresources=")
    while True:
        c = s[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                break
        i += 1
    s = s[:a] + "_subresources={}" + s[i + 1:]
    f.write_text(s, encoding="utf-8")
    print(f"{f.name}: {before} -> {len(s)} B")
