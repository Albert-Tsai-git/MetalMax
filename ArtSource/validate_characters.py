"""Validate character rig structure, weighted meshes, and locomotion actions."""
import bpy
from pathlib import Path

base = Path(__file__).resolve().parent
required_bones = {'Root','Hips','Spine','Chest','Neck','Head','UpperArm.L','LowerArm.L','Hand.L',
                  'UpperArm.R','LowerArm.R','Hand.R','UpperLeg.L','LowerLeg.L','Foot.L',
                  'UpperLeg.R','LowerLeg.R','Foot.R'}
for char_id in ('CHR_Hunter','CHR_Mechanic'):
    bpy.ops.wm.open_mainfile(filepath=str(base / f'{char_id}.blend'))
    rigs = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    assert len(rigs) == 1, f'{char_id}: expected one rig, got {len(rigs)}'
    rig = rigs[0]
    assert rig.name == f'{char_id}_Rig'
    bones = {b.name for b in rig.data.bones}
    assert required_bones <= bones, f'{char_id}: missing bones {required_bones-bones}'
    actions = {a.name: a for a in bpy.data.actions}
    assert set(actions) == {'Idle','Walk','Run'}, f'{char_id}: wrong actions {set(actions)}'
    for name, action in actions.items():
        assert action.frame_range[1] > action.frame_range[0], f'{char_id}/{name}: empty action'
        assert action.fcurves, f'{char_id}/{name}: no keyframes'
        for curve in action.fcurves:
            keys = curve.keyframe_points
            assert len(keys) >= 2 and abs(keys[0].co.y - keys[-1].co.y) < 1e-4, f'{char_id}/{name}: loop seam in {curve.data_path}'
    assert any(m.type == 'CYCLES' for fc in actions['Walk'].fcurves for m in fc.modifiers), f'{char_id}: Walk not cyclic'
    assert any(m.type == 'CYCLES' for fc in actions['Run'].fcurves for m in fc.modifiers), f'{char_id}: Run not cyclic'
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    assert len(meshes) >= 20, f'{char_id}: too few mesh parts'
    for mesh in meshes:
        assert mesh.parent == rig, f'{char_id}/{mesh.name}: not parented to rig'
        assert any(m.type == 'ARMATURE' and m.object == rig for m in mesh.modifiers), f'{char_id}/{mesh.name}: no rig modifier'
        assert mesh.vertex_groups and len(mesh.data.vertices), f'{char_id}/{mesh.name}: no weighted vertices'
    verts = [rig.matrix_world @ mesh.matrix_world @ v.co for mesh in meshes for v in mesh.data.vertices]
    height = max(v.y for v in verts) - min(v.y for v in verts)
    width = max(v.x for v in verts) - min(v.x for v in verts)
    assert 1.5 <= height <= 2.0, f'{char_id}: implausible height {height:.2f}m'
    assert .35 <= width <= 1.1, f'{char_id}: implausible width {width:.2f}m'
    print(f'PASS {char_id}: {len(meshes)} skinned meshes, {len(bones)} bones, actions=Idle/Walk/Run, bounds={width:.2f}x{height:.2f}m')
