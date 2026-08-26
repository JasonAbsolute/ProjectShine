"""Give an SMS accessory part its colour back.

Run headless:
    blender --background --python tools/tint_part_glb.py -- <in.glb> <out.glb> <#RRGGBB>

Caps and similar parts ship an I4 (intensity-only) texture -- shading, no hue.
The actual colour came from the TEV pipeline, which BMD exporters drop, so the
part imports grey. Multiplying a base colour through the intensity map restores
it. metallicFactor is forced to 0 at the same time; glTF defaults it to 1.0 and
the part comes out chrome otherwise.
"""
import sys

import bpy

argv = sys.argv[sys.argv.index('--') + 1:]
src, dst, hexcol = argv[0], argv[1], argv[2].lstrip('#')
rgb = tuple(int(hexcol[i:i + 2], 16) / 255.0 for i in (0, 2, 4))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    for node in mat.node_tree.nodes:
        if node.type != 'BSDF_PRINCIPLED':
            continue
        node.inputs['Metallic'].default_value = 0.0
        if 'Roughness' in node.inputs:
            node.inputs['Roughness'].default_value = 1.0
        base = node.inputs['Base Color']
        if base.is_linked:
            # Texture already drives base colour, so fold the tint in with a mix
            # rather than replacing it and losing the shading.
            tex = base.links[0].from_node
            mix = mat.node_tree.nodes.new('ShaderNodeMixRGB')
            mix.blend_type = 'MULTIPLY'
            mix.inputs['Fac'].default_value = 1.0
            mix.inputs['Color2'].default_value = (*rgb, 1.0)
            mat.node_tree.links.new(mix.inputs['Color1'], tex.outputs['Color'])
            mat.node_tree.links.new(base, mix.outputs['Color'])
        else:
            base.default_value = (*rgb, 1.0)

bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB',
                          export_animations=False, export_yup=True)
print(f"[tint] {dst} <- #{hexcol}")
