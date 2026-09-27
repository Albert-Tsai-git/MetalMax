"""Create two original, low-poly rigged walk-cycle character sources."""
import bpy
import math
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
PALETTE = {
    'coat': (0.18, 0.28, 0.27, 1), 'coat_light': (0.31, 0.42, 0.38, 1),
    'pants': (0.48, 0.41, 0.30, 1), 'skin': (0.60, 0.39, 0.27, 1),
    'hair': (0.12, 0.095, 0.075, 1), 'boot': (0.10, 0.12, 0.12, 1),
    'orange': (0.78, 0.25, 0.095, 1), 'metal': (0.30, 0.35, 0.34, 1),
    'glass': (0.10, 0.32, 0.39, 1), 'canvas': (0.67, 0.57, 0.39, 1),
    'dark': (0.12, 0.16, 0.15, 1), 'patch': (0.39, 0.35, 0.27, 1),
}


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)
    for action in list(bpy.data.actions):
        action.use_fake_user = False
        bpy.data.actions.remove(action)


def make_material(key):
    mat = bpy.data.materials.new(key)
    mat.diffuse_color = PALETTE[key]
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = PALETTE[key]
    bsdf.inputs['Roughness'].default_value = .82
    return mat


def make_rig(char_id):
    arm_data = bpy.data.armatures.new(f'{char_id}_Skeleton')
    arm = bpy.data.objects.new(f'{char_id}_Rig', arm_data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bones = {}
    def bone(name, head, tail, parent=None, connect=False):
        b = arm_data.edit_bones.new(name)
        b.head, b.tail = head, tail
        if parent:
            b.parent = bones[parent]
            b.use_connect = connect
        bones[name] = b
    bone('Root', (0, 0, 0), (0, .78, 0))
    bone('Hips', (0, .78, 0), (0, .98, 0), 'Root', True)
    bone('Spine', (0, .98, 0), (0, 1.16, 0), 'Hips', True)
    bone('Chest', (0, 1.16, 0), (0, 1.37, 0), 'Spine', True)
    bone('Neck', (0, 1.37, 0), (0, 1.48, 0), 'Chest', True)
    bone('Head', (0, 1.48, 0), (0, 1.76, 0), 'Neck', True)
    for side, label in [(-1, 'L'), (1, 'R')]:
        bone(f'Shoulder.{label}', (side*.16, 1.32, 0), (side*.28, 1.29, 0), 'Chest')
        bone(f'UpperArm.{label}', (side*.28, 1.29, 0), (side*.40, 1.04, 0), f'Shoulder.{label}')
        bone(f'LowerArm.{label}', (side*.40, 1.04, 0), (side*.43, .82, .015), f'UpperArm.{label}')
        bone(f'Hand.{label}', (side*.43, .82, .015), (side*.43, .74, .04), f'LowerArm.{label}')
        bone(f'UpperLeg.{label}', (side*.105, .82, 0), (side*.13, .45, 0), 'Hips')
        bone(f'LowerLeg.{label}', (side*.13, .45, 0), (side*.13, .13, 0), f'UpperLeg.{label}')
        bone(f'Foot.{label}', (side*.13, .13, 0), (side*.13, .10, .22), f'LowerLeg.{label}')
    bpy.ops.object.mode_set(mode='OBJECT')
    arm.select_set(False)
    arm.show_in_front = True
    arm.data.display_type = 'OCTAHEDRAL'
    return arm


def make_part(name, center, size, bone_name, material, rig, mats):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel = obj.modifiers.new('Large form bevel', 'BEVEL')
    bevel.width = min(size) * .10
    bevel.segments = 1
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.append(mats[material])
    group = obj.vertex_groups.new(name=bone_name)
    group.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    mod = obj.modifiers.new('Character deformation', 'ARMATURE')
    mod.object = rig
    obj.parent = rig
    return obj


def make_ico(name, center, size, bone_name, material, rig, mats, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0]/2, size[1]/2, size[2]/2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.append(mats[material])
    group = obj.vertex_groups.new(name=bone_name)
    group.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    mod = obj.modifiers.new('Character deformation', 'ARMATURE')
    mod.object = rig
    obj.parent = rig
    return obj


def build_character(char_id, mechanic=False):
    clear_scene()
    mats = {key: make_material(key) for key in PALETTE}
    rig = make_rig(char_id)
    # Core clothing blocks. Forward is +Z; the simple, large shapes read at field-camera scale.
    make_part('GEO_Pelvis', (0, .87, 0), (.37, .24, .27), 'Hips', 'pants' if mechanic else 'coat', rig, mats)
    make_part('GEO_Torso', (0, 1.19, 0), (.47, .48, .30), 'Chest', 'canvas' if mechanic else 'coat', rig, mats)
    make_part('GEO_Collar', (0, 1.43, .035), (.27, .12, .25), 'Neck', 'orange' if not mechanic else 'coat_light', rig, mats)
    make_ico('GEO_Head', (0, 1.59, .015), (.27, .31, .25), 'Head', 'skin', rig, mats, 2)
    make_part('GEO_HairCap', (0, 1.735, -.005), (.29, .12, .28), 'Head', 'hair', rig, mats)
    # Face and eye line are on the +Z/front plane.
    make_part('GEO_GoggleStrap', (0, 1.63, .128), (.28, .045, .025), 'Head', 'hair', rig, mats)
    for side, label in [(-1, 'L'), (1, 'R')]:
        make_ico(f'GEO_Goggle_{label}', (side*.065, 1.635, .151), (.075, .065, .038), 'Head', 'glass', rig, mats)
        make_part(f'GEO_UpperArm_{label}', (side*.355, 1.17, .005), (.19, .31, .22), f'UpperArm.{label}', 'canvas' if mechanic else 'coat_light', rig, mats)
        make_part(f'GEO_Forearm_{label}', (side*.415, .925, .02), (.16, .25, .19), f'LowerArm.{label}', 'coat' if mechanic else 'coat', rig, mats)
        make_ico(f'GEO_Glove_{label}', (side*.43, .785, .035), (.15, .14, .15), f'Hand.{label}', 'boot', rig, mats)
        make_part(f'GEO_Thigh_{label}', (side*.12, .64, 0), (.20, .34, .23), f'UpperLeg.{label}', 'pants' if mechanic else 'canvas', rig, mats)
        make_part(f'GEO_Shin_{label}', (side*.13, .29, .01), (.16, .31, .19), f'LowerLeg.{label}', 'pants' if mechanic else 'pants', rig, mats)
        make_part(f'GEO_Boot_{label}', (side*.13, .105, .065), (.19, .18, .32), f'Foot.{label}', 'boot', rig, mats)
    if mechanic:
        # Heavy tool roll and repair kit give the mechanic a distinct outline.
        make_part('GEO_ToolPack', (0, 1.05, -.205), (.43, .50, .24), 'Spine', 'coat', rig, mats)
        make_part('GEO_PackFlap', (0, 1.25, -.335), (.31, .18, .045), 'Chest', 'orange', rig, mats)
        make_part('GEO_ChestPocket', (-.12, 1.20, .166), (.13, .15, .035), 'Chest', 'patch', rig, mats)
        make_part('GEO_BeltTools', (.20, .92, -.02), (.10, .22, .12), 'Hips', 'metal', rig, mats)
        # Two visible wrench handles strapped across the rear pack.
        make_part('GEO_WrenchHandle', (.07, 1.07, -.35), (.055, .48, .055), 'Spine', 'metal', rig, mats)
        make_part('GEO_WrenchHead', (.07, 1.31, -.35), (.18, .07, .055), 'Spine', 'metal', rig, mats)
    else:
        # The hunter's short weather cape and orange signal scarf distinguish the silhouette.
        make_part('GEO_Cape', (0, 1.00, -.19), (.50, .48, .14), 'Spine', 'coat_light', rig, mats)
        make_part('GEO_ScarfTail', (0, 1.35, -.18), (.14, .29, .10), 'Chest', 'orange', rig, mats)
        make_part('GEO_BeltPouch_L', (-.22, .93, .02), (.12, .16, .18), 'Hips', 'patch', rig, mats)
        make_part('GEO_BeltPouch_R', (.22, .93, .02), (.12, .16, .18), 'Hips', 'orange', rig, mats)
    # A small front emblem keeps the two characters recognizable in the overhead view.
    make_part('GEO_ChestMark', (0, 1.22, .166), (.11, .13, .025), 'Chest', 'orange' if mechanic else 'canvas', rig, mats)
    create_actions(rig)
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 30
    bpy.context.scene.render.engine = 'BLENDER_EEVEE_NEXT'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / f'{char_id}.blend'))
    print(f'Created {char_id}: {sum(1 for o in bpy.data.objects if o.type == "MESH")} meshes, {len(rig.data.bones)} bones, actions={list(bpy.data.actions.keys())}')


def create_actions(rig):
    animated = ['Hips', 'Chest', 'UpperArm.L', 'LowerArm.L', 'UpperArm.R', 'LowerArm.R',
                'UpperLeg.L', 'LowerLeg.L', 'UpperLeg.R', 'LowerLeg.R']
    for action_name, frames, amount in [('Idle', [1, 8, 16, 23, 30], .035),
                                        ('Walk', [1, 6, 11, 16, 21, 26, 31], .55),
                                        ('Run', [1, 5, 9, 13, 17, 21], .88),
                                        ('Attack', [1, 5, 9, 14, 19], 1.0),
                                        ('Hit', [1, 4, 8, 12], 1.0),
                                        ('Defeat', [1, 6, 12, 18], 1.0)]:
        action = bpy.data.actions.new(action_name)
        rig.animation_data_create()
        rig.animation_data.action = action
        for frame in frames:
            phase = 2 * math.pi * (frame - frames[0]) / (frames[-1] - frames[0])
            wave = math.sin(phase)
            for bone_name in animated:
                pb = rig.pose.bones[bone_name]
                pb.rotation_mode = 'XYZ'
                pb.rotation_euler = (0, 0, 0)
            if action_name == 'Idle':
                breath = math.sin(phase) * amount
                rig.pose.bones['Chest'].rotation_euler.x = breath
                rig.pose.bones['Hips'].location.y = abs(math.sin(phase)) * .012
                for side in ('L', 'R'):
                    rig.pose.bones[f'UpperArm.{side}'].rotation_euler.x = math.sin(phase + (0 if side == 'L' else math.pi)) * .018
            elif action_name in ('Walk', 'Run'):
                stride = amount * wave
                rig.pose.bones['UpperLeg.L'].rotation_euler.x = stride
                rig.pose.bones['UpperLeg.R'].rotation_euler.x = -stride
                rig.pose.bones['LowerLeg.L'].rotation_euler.x = max(0, -wave) * (.30 if action_name == 'Walk' else .68)
                rig.pose.bones['LowerLeg.R'].rotation_euler.x = max(0, wave) * (.30 if action_name == 'Walk' else .68)
                rig.pose.bones['UpperArm.L'].rotation_euler.x = -stride * .92
                rig.pose.bones['UpperArm.R'].rotation_euler.x = stride * .92
                elbow = .12 if action_name == 'Walk' else .38
                rig.pose.bones['LowerArm.L'].rotation_euler.x = elbow
                rig.pose.bones['LowerArm.R'].rotation_euler.x = elbow
                rig.pose.bones['Hips'].location.y = (1 - math.cos(2 * phase)) * (.006 if action_name == 'Walk' else .018)
                rig.pose.bones['Chest'].rotation_euler.x = .025 * wave
            elif action_name == 'Attack':
                # A readable two-handed forward strike: wind-up, lunge, hold, recoil.
                t = (frame - frames[0]) / (frames[-1] - frames[0])
                reach = {1: 0.0, 5: -0.16, 9: -0.05, 14: 0.0, 19: 0.0}[frame]
                rig.pose.bones['Chest'].rotation_euler.x = {1: 0.0, 5: -0.12, 9: 0.22, 14: 0.04, 19: 0.0}[frame]
                rig.pose.bones['Chest'].location.z = reach
                rig.pose.bones['UpperArm.R'].rotation_euler.x = {1: 0.0, 5: -0.7, 9: 0.9, 14: 0.3, 19: 0.0}[frame]
                rig.pose.bones['LowerArm.R'].rotation_euler.x = {1: 0.1, 5: -0.9, 9: 0.25, 14: 0.4, 19: 0.1}[frame]
                rig.pose.bones['UpperArm.L'].rotation_euler.x = {1: 0.0, 5: -0.35, 9: 0.65, 14: 0.18, 19: 0.0}[frame]
                rig.pose.bones['LowerArm.L'].rotation_euler.x = {1: 0.1, 5: -0.45, 9: 0.2, 14: 0.3, 19: 0.1}[frame]
                rig.pose.bones['UpperLeg.R'].rotation_euler.x = {1: 0.0, 5: -0.2, 9: 0.25, 14: 0.0, 19: 0.0}[frame]
                rig.pose.bones['UpperLeg.L'].rotation_euler.x = {1: 0.0, 5: 0.15, 9: -0.2, 14: 0.0, 19: 0.0}[frame]
                rig.pose.bones['Hips'].location.y = 0.01 if t < .7 else 0.0
            elif action_name == 'Hit':
                rig.pose.bones['Chest'].rotation_euler.x = {1: 0.0, 4: -0.48, 8: -0.30, 12: 0.0}[frame]
                rig.pose.bones['Chest'].rotation_euler.z = {1: 0.0, 4: 0.22, 8: 0.1, 12: 0.0}[frame]
                rig.pose.bones['Head'].rotation_euler.x = {1: 0.0, 4: 0.18, 8: 0.06, 12: 0.0}[frame]
                for side in ('L', 'R'):
                    rig.pose.bones[f'UpperArm.{side}'].rotation_euler.x = {1: 0.0, 4: -0.35, 8: -0.18, 12: 0.0}[frame]
                rig.pose.bones['Hips'].location.y = {1: 0.0, 4: -0.04, 8: -0.02, 12: 0.0}[frame]
            elif action_name == 'Defeat':
                # Cumulative collapse to one side, with head and legs trailing.
                t = (frame - frames[0]) / (frames[-1] - frames[0])
                rig.pose.bones['Hips'].rotation_euler.x = -0.22 * t
                rig.pose.bones['Hips'].rotation_euler.z = 1.05 * t
                rig.pose.bones['Hips'].location.y = -0.18 * t
                rig.pose.bones['Chest'].rotation_euler.x = -0.18 * t
                rig.pose.bones['Chest'].rotation_euler.z = -0.28 * t
                rig.pose.bones['Head'].rotation_euler.x = 0.18 * t
                for side in ('L', 'R'):
                    rig.pose.bones[f'UpperArm.{side}'].rotation_euler.x = -0.55 * t
                    rig.pose.bones[f'UpperArm.{side}'].rotation_euler.z = (0.55 if side == 'L' else -0.55) * t
                    rig.pose.bones[f'UpperLeg.{side}'].rotation_euler.x = 0.24 * t
                    rig.pose.bones[f'LowerLeg.{side}'].rotation_euler.x = -0.25 * t
            for bone_name in animated + ['Head']:
                pb = rig.pose.bones[bone_name]
                pb.keyframe_insert(data_path='rotation_euler', frame=frame, group=bone_name)
            for bone_name in ('Hips', 'Chest'):
                rig.pose.bones[bone_name].keyframe_insert(data_path='location', frame=frame, group=bone_name)
        for fcurve in action.fcurves:
            if action_name in ('Idle', 'Walk', 'Run'):
                fcurve.modifiers.new('CYCLES')
            for key in fcurve.keyframe_points:
                key.interpolation = 'BEZIER'
        action.use_fake_user = True
        action.frame_range = (frames[0], frames[-1])
    rig.animation_data.action = bpy.data.actions.get('Idle')


for char_id, mechanic in [('CHR_Hunter', False), ('CHR_Mechanic', True)]:
    build_character(char_id, mechanic)
