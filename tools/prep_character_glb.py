"""Clean a raw FinModelUtility SMS export into something Godot can import.

Run headless:
    blender --background --python tools/prep_character_glb.py -- <in.glb> <out.gltf>

The raw conversion has two problems every time, both fixed by a Blender round
trip plus a material pass:

  * two skins where there should be one, which vet_rig.py rejects
  * no metallicFactor, and glTF's default of 1.0 renders the character chrome
"""
import sys

import bpy

argv = sys.argv[sys.argv.index('--') + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

fixed = 0
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    for node in mat.node_tree.nodes:
        if node.type != 'BSDF_PRINCIPLED':
            continue
        node.inputs['Metallic'].default_value = 0.0
        # Flat SMS art has no business being glossy either.
        if 'Roughness' in node.inputs:
            node.inputs['Roughness'].default_value = 1.0
        fixed += 1

arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print(f"[prep] armatures={len(arms)} meshes={len(meshes)} materials fixed={fixed}")

bpy.ops.export_scene.gltf(
    filepath=dst,
    export_format='GLTF_SEPARATE',
    export_animations=False,
    export_skins=True,
    export_yup=True,
    use_selection=False,
)
print(f"[prep] wrote {dst}")
