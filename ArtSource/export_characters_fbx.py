"""Export approved character IDs and all three looping actions to Unity FBX."""
import bpy
from pathlib import Path
source = Path(__file__).resolve().parent
output = source.parent / 'UNITY' / 'Assets' / 'Models'
output.mkdir(parents=True, exist_ok=True)
for char_id in ('CHR_Hunter', 'CHR_Mechanic'):
    bpy.ops.wm.open_mainfile(filepath=str(source / f'{char_id}.blend'))
    path = output / f'{char_id}.fbx'
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=False, global_scale=1.0, apply_unit_scale=True,
        axis_forward='Z', axis_up='Y', bake_space_transform=False, use_mesh_modifiers=True,
        add_leaf_bones=False, object_types={'ARMATURE','MESH'}, path_mode='AUTO',
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    )
    print(f'Exported {path}: {list(bpy.data.actions.keys())}')
