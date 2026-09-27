"""Round-trip exported character FBX files through Blender and verify clips, rig, scale and facing axis."""
import bpy
from mathutils import Vector
from pathlib import Path
base = Path(__file__).resolve().parent
models = base.parent / 'UNITY' / 'Assets' / 'Models'
for char_id in ('CHR_Hunter','CHR_Mechanic'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(models / f'{char_id}.fbx'), automatic_bone_orientation=False, use_anim=True)
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    actions = {action.name for action in bpy.data.actions}
    assert len(rigs) == 1, f'{char_id}: expected one armature, got {len(rigs)}'
    assert len(meshes) >= 20, f'{char_id}: lost mesh parts ({len(meshes)})'
    normalized_actions = {action.split('|')[-1] for action in actions}
    assert {'Idle','Walk','Run'} <= normalized_actions, f'{char_id}: actions lost on FBX roundtrip: {actions}'
    imported = {action.name.split('|')[-1]: action for action in bpy.data.actions}
    for name in ('Idle', 'Walk', 'Run'):
        action = imported[name]
        for curve in action.fcurves:
            keys = curve.keyframe_points
            assert len(keys) >= 2, f'{char_id}/{name}: imported curve has fewer than two keys'
            assert abs(keys[0].co.y - keys[-1].co.y) < 1e-3, (
                f'{char_id}/{name}: FBX loop seam in {curve.data_path}[{curve.array_index}] '
                f'({keys[0].co.y:.5f} != {keys[-1].co.y:.5f})'
            )
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    height = max(p.y for p in points) - min(p.y for p in points)
    depth = max(p.z for p in points) - min(p.z for p in points)
    assert 1.5 <= height <= 2.1, f'{char_id}: FBX height {height:.2f}m'
    assert depth > .2, f'{char_id}: forward/depth axis absent'
    print(f'PASS FBX {char_id}: {len(meshes)} meshes, bones={len(rigs[0].data.bones)}, clips={sorted(actions)}, bounds-height={height:.2f}m depth={depth:.2f}m')
