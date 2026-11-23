# Prepares an image-to-3D character (GLB) for Mixamo: 1.80 m tall, feet at the origin, about 30k triangles,
# textures saved as PNGs and embedded in an FBX. Run headless:
#
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
#     -P scripts/blender/prep-character.py -- <in.glb> <out-dir> [height-m] [triangles]
#
# Writes <out-dir>/<name>-for-mixamo.fbx, <out-dir>/textures/*.png and <out-dir>/<name>-prep.blend.
# Decimating changes the UVs, so do it before any texture painting, never after.

import bpy, sys, os
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = ARGS[0], ARGS[1]
TARGET_HEIGHT = float(ARGS[2]) if len(ARGS) > 2 else 1.80
TARGET_TRIS = int(ARGS[3]) if len(ARGS) > 3 else 30000
NAME = os.path.splitext(os.path.basename(SRC))[0].split('-')[0]
os.makedirs(os.path.join(OUT, 'textures'), exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
obj.name = obj.data.name = NAME.capitalize()
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Scale to height (glTF's +Z forward arrives as Blender's -Y, which Mixamo treats as the front), feet on the ground, centred on X/Y.
bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
minz = min(v.z for v in bb); maxz = max(v.z for v in bb)
s = TARGET_HEIGHT / (maxz - minz)
obj.scale = (s, s, s); bpy.ops.object.transform_apply(scale=True)
bb = [Vector(c) for c in obj.bound_box]
cx = (min(v.x for v in bb)+max(v.x for v in bb))/2; cy = (min(v.y for v in bb)+max(v.y for v in bb))/2
mz = min(v.z for v in bb)
obj.data.transform(Matrix.Translation((-cx, -cy, -mz)))
obj.location = (0,0,0)

# Clean: merge coincident verts, then decimate to the triangle budget.
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0001); bpy.ops.object.mode_set(mode='OBJECT')
tris = sum(len(p.vertices)-2 for p in obj.data.polygons)
mod = obj.modifiers.new('dec', 'DECIMATE'); mod.ratio = TARGET_TRIS / tris
mod.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier='dec')
tris2 = sum(len(p.vertices)-2 for p in obj.data.polygons)

# Save the embedded textures as files so the FBX can embed them (and Unity can use them).
for img in bpy.data.images:
    if img.source != 'FILE' and img.packed_file is None: continue
    name = img.name.replace(' ', '_').replace('-', '_')
    path = os.path.join(OUT, 'textures', f'{NAME}_{name}.png')
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()
    print('TEX', path, tuple(img.size))

dims = tuple(round(d,3) for d in obj.dimensions)
print('RESULT tris', tris, '->', tris2, 'dims', dims)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f'{NAME}-for-mixamo.fbx'), use_selection=True,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    path_mode='COPY', embed_textures=True, mesh_smooth_type='FACE', add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, f'{NAME}-prep.blend'))
