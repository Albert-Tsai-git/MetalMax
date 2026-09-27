"""Export Y-up source assets to Unity FBX with 1 unit = 1 metre."""
import bpy
from pathlib import Path

source = Path(__file__).resolve().parent
output = source.parent / 'UNITY' / 'Assets' / 'Models'
output.mkdir(parents=True, exist_ok=True)

for name in ('WPN_Flamethrower', 'WPN_ShockCannon'):
    bpy.ops.wm.open_mainfile(filepath=str(source / f'{name}.blend'))
    path = output / f'{name}.fbx'
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=False,
        global_scale=1.0,
        apply_unit_scale=True,
        axis_forward='Z',
        axis_up='Y',
        bake_space_transform=False,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        object_types={'EMPTY', 'MESH'},
        path_mode='AUTO',
    )
    print(f'Exported {path}')
