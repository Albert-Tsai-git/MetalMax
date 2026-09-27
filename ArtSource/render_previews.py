"""Render review images from the five editable Blender sources without Unity."""
import bpy
from mathutils import Matrix, Vector
from pathlib import Path

base = Path(__file__).resolve().parent
out = base / 'Previews'
out.mkdir(exist_ok=True)

for name in ('TNK_Chassis_Light', 'TNK_Chassis_Heavy', 'WPN_Cannon_75', 'WPN_MG_77', 'WPN_SE_Missile'):
    bpy.ops.wm.open_mainfile(filepath=str(base / f'{name}.blend'))
    root = bpy.data.objects[name]
    bpy.ops.object.camera_add(location=(6.6, 6.1, 7.8) if name.startswith('TNK') else (2.4, 2.5, 3.3))
    camera = bpy.context.object
    target = Vector((0, .8, 0) if name.startswith('TNK') else (0, .2, .6))
    forward = (target - camera.location).normalized()
    right = forward.cross(Vector((0, 1, 0))).normalized()
    up = (-forward).cross(right).normalized()
    orientation = Matrix((right, up, -forward)).transposed()
    camera.rotation_euler = orientation.to_euler()
    bpy.context.scene.camera = camera
    bpy.context.scene.render.engine = 'BLENDER_EEVEE_NEXT'
    bpy.context.scene.render.resolution_x = 960
    bpy.context.scene.render.resolution_y = 720
    bpy.context.scene.render.resolution_percentage = 100
    bpy.context.scene.render.image_settings.file_format = 'PNG'
    bpy.context.scene.render.film_transparent = True
    bpy.context.scene.world.color = (.16, .16, .16)
    bpy.ops.object.light_add(type='AREA', location=(0, 9, 4))
    bpy.context.object.data.energy = 1500
    bpy.context.object.data.shape = 'DISK'
    bpy.context.object.data.size = 8
    bpy.context.scene.render.filepath = str(out / f'{name}.png')
    bpy.ops.render.render(write_still=True)
    print(f'Rendered {name}')
