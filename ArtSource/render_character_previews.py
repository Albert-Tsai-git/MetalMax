"""Render clean idle and walk previews for field-camera model review."""
import bpy
import math
from mathutils import Matrix, Vector
from pathlib import Path
base = Path(__file__).resolve().parent
out = base / 'Previews'
out.mkdir(exist_ok=True)
for char_id in ('CHR_Hunter','CHR_Mechanic'):
    bpy.ops.wm.open_mainfile(filepath=str(base / f'{char_id}.blend'))
    rig = bpy.data.objects[f'{char_id}_Rig']
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = True
    scene.world.color = (.19,.19,.19)
    bpy.ops.object.camera_add(location=(2.5, 2.15, 4.2))
    camera = bpy.context.object
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 2.55
    target = Vector((0, .91, 0))
    forward = (target - camera.location).normalized()
    right = forward.cross(Vector((0, 1, 0))).normalized()
    up = (-forward).cross(right).normalized()
    rotation = Matrix((right, up, -forward)).transposed().to_quaternion()
    camera.rotation_mode = 'QUATERNION'
    camera.rotation_quaternion = rotation
    scene.camera = camera
    bpy.ops.object.light_add(type='AREA', location=(-3.2, 5.0, 4.0))
    key = bpy.context.object
    key.data.energy = 850
    key.data.shape = 'DISK'
    key.data.size = 4
    key.rotation_euler = (Vector((0,1,0)) - key.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.light_add(type='AREA', location=(3.5, 2.8, -2.5))
    fill = bpy.context.object
    fill.data.energy = 500
    fill.data.size = 3
    fill.rotation_euler = (Vector((0,1,0)) - fill.location).to_track_quat('-Z','Y').to_euler()
    for action, frame in [('Idle',1),('Walk',6),('Run',5)]:
        rig.animation_data.action = bpy.data.actions[action]
        scene.frame_set(frame)
        scene.render.filepath = str(out / f'{char_id}_{action}.png')
        bpy.ops.render.render(write_still=True)
        print(f'Rendered {char_id}_{action}')
    if char_id == 'CHR_Mechanic':
        camera.location = (-2.5, 2.15, -4.2)
        forward = (target - camera.location).normalized()
        right = forward.cross(Vector((0, 1, 0))).normalized()
        up = (-forward).cross(right).normalized()
        camera.rotation_quaternion = Matrix((right, up, -forward)).transposed().to_quaternion()
        rig.animation_data.action = bpy.data.actions['Idle']
        scene.frame_set(1)
        scene.render.filepath = str(out / 'CHR_Mechanic_Back.png')
        bpy.ops.render.render(write_still=True)
        print('Rendered CHR_Mechanic_Back')
