"""Check editable source hierarchy and coordinate contracts before FBX export."""
import bpy
from pathlib import Path

base = Path(__file__).resolve().parent
mounts = {
    'TNK_Chassis_Light': {'Mount_Main', 'Mount_Sub'},
    'TNK_Chassis_Heavy': {'Mount_Main', 'Mount_Sub', 'Mount_SE'},
    'WPN_Cannon_75': set(),
    'WPN_MG_77': set(),
    'WPN_SE_Missile': set(),
}

for name, required in mounts.items():
    bpy.ops.wm.open_mainfile(filepath=str(base / f'{name}.blend'))
    root = bpy.data.objects[name]
    assert root.type == 'EMPTY', f'{name}: root is not empty'
    assert tuple(root.location) == (0.0, 0.0, 0.0), f'{name}: shifted root'
    assert tuple(root.rotation_euler) == (0.0, 0.0, 0.0), f'{name}: rotated root'
    assert tuple(root.scale) == (1.0, 1.0, 1.0), f'{name}: scaled root'
    actual = {obj.name for obj in bpy.data.objects if obj.type == 'EMPTY'} - {name}
    assert actual == required, f'{name}: mounts {actual}, expected {required}'
    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    assert meshes, f'{name}: no mesh'
    assert all(obj.parent == root for obj in meshes), f'{name}: detached mesh'
    assert all(obj.parent == root for obj in bpy.data.objects if obj.name in required), f'{name}: detached mount'
    assert all(tuple(obj.scale) == (1.0, 1.0, 1.0) for obj in meshes), f'{name}: unapplied scale'
    if name.startswith('WPN_'):
        assert max(obj.location.z for obj in meshes) > 0, f'{name}: weapon has no +Z forward geometry'
    print(f'PASS {name}: {len(meshes)} meshes, mounts={sorted(actual)}')
