"""Generate editable low-poly Y-up vehicle and weapon .blend sources."""
import bpy
import math
from pathlib import Path

OUT = Path(__file__).resolve().parent

PALETTE = {
    'body': (0.29, 0.39, 0.34, 1),
    'heavy': (0.24, 0.30, 0.27, 1),
    'patch': (0.64, 0.58, 0.43, 1),
    'rubber': (0.075, 0.08, 0.08, 1),
    'metal': (0.27, 0.29, 0.29, 1),
    'glass': (0.30, 0.47, 0.51, 1),
    'orange': (0.74, 0.30, 0.13, 1),
    'dark': (0.12, 0.14, 0.14, 1),
}


def reset(name):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = 'CUBE'
    root.empty_display_size = 0.35
    return root


def mat(name):
    material = bpy.data.materials.get(name)
    if material is None:
        material = bpy.data.materials.new(name)
        material.diffuse_color = PALETTE[name]
    return material


def finish(obj, name, material, root, bevel=0.0):
    obj.name = name
    obj.data.materials.append(mat(material))
    if bevel:
        modifier = obj.modifiers.new('Soft edges', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 1
        modifier.affect = 'EDGES'
        normal = obj.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.parent = root
    return obj


def box(root, name, loc, size, material, bevel=0.04):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.dimensions = size
    return finish(obj, name, material, root, bevel)


def cylinder(root, name, loc, radius, depth, material, vertices=12, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    return finish(bpy.context.object, name, material, root)


def point(root, name, loc):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = 'ARROWS'
    obj.empty_display_size = 0.25
    obj.location = loc
    obj.parent = root
    return obj


def chassis(name, heavy=False):
    root = reset(name)
    width = 2.85 if heavy else 2.18
    length = 5.45 if heavy else 4.55
    box(root, 'GEO_Frame', (0, .72, 0), (width, .57, length), 'heavy' if heavy else 'body')
    box(root, 'GEO_Nose', (0, .95, length*.34), (width*.84, .38, length*.30), 'patch')
    box(root, 'GEO_Cab', (0, 1.25, length*.16), (width*.66, .58, length*.28), 'body')
    box(root, 'GEO_Windscreen', (0, 1.56, length*.26), (width*.51, .035, .39), 'glass', .015)
    box(root, 'GEO_EngineHood', (0, 1.09, -length*.33), (width*.60, .28, length*.25), 'dark')
    box(root, 'GEO_EnginePanel', (0, 1.25, -length*.34), (width*.40, .045, length*.16), 'metal', .01)
    for side, tag in [(-1, 'L'), (1, 'R')]:
        x = side*(width*.53)
        for index, z in enumerate([length*.32, 0, -length*.32]):
            cylinder(root, f'GEO_Wheel_{tag}{index+1}', (x, .51, z), .49 if heavy else .43,
                     .29 if heavy else .25, 'rubber', 16, (0, math.pi/2, 0))
            cylinder(root, f'GEO_Hub_{tag}{index+1}', (x+side*.16, .51, z), .19, .025,
                     'metal', 10, (0, math.pi/2, 0))
        box(root, f'GEO_SideArmor_{tag}', (side*width*.41, 1.02, -.17),
            (.19, .42, length*.52), 'patch' if heavy else 'body')
        box(root, f'GEO_RearLamp_{tag}', (side*width*.33, 1.05, -length*.49),
            (.17, .09, .06), 'orange', .01)
    box(root, 'GEO_FrontMarker', (0, 1.02, length*.49), (width*.42, .055, .04), 'orange', .01)
    point(root, 'Mount_Main', (0, 1.63 if heavy else 1.55, -.22))
    point(root, 'Mount_Sub', (width*.24, 1.51 if heavy else 1.38, length*.28))
    if heavy:
        point(root, 'Mount_SE', (-width*.27, 1.35, -length*.32))
    save(name)


def weapon(name):
    root = reset(name)
    if name == 'WPN_Cannon_75':
        cylinder(root, 'GEO_TurretBase', (0, .05, 0), .55, .16, 'heavy', 12, (math.pi/2, 0, 0))
        box(root, 'GEO_Breech', (0, .22, .22), (.72, .46, .70), 'body')
        cylinder(root, 'GEO_Barrel', (0, .20, .96), .15, 1.45, 'metal', 12)
        cylinder(root, 'GEO_Muzzle', (0, .20, 1.70), .22, .18, 'dark', 12)
        cylinder(root, 'GEO_MuzzleInner', (0, .20, 1.80), .10, .012, 'rubber', 12)
    elif name == 'WPN_MG_77':
        box(root, 'GEO_Swivel', (0, .12, 0), (.38, .24, .40), 'heavy')
        box(root, 'GEO_Receiver', (0, .23, .31), (.32, .28, .55), 'metal')
        cylinder(root, 'GEO_Barrel', (0, .25, .89), .055, .78, 'dark', 10)
        box(root, 'GEO_AmmoBox', (.24, .09, .29), (.24, .31, .34), 'patch')
    elif name == 'WPN_Flamethrower':
        cylinder(root, 'GEO_TurretBase', (0, .04, 0), .40, .12, 'heavy', 12, (math.pi/2, 0, 0))
        box(root, 'GEO_Receiver', (0, .22, .18), (.48, .34, .52), 'body')
        cylinder(root, 'GEO_Nozzle', (0, .22, .73), .105, .72, 'metal', 10)
        cylinder(root, 'GEO_FlareGuard', (0, .22, 1.08), .17, .16, 'orange', 10)
        for side, tag in [(-1, 'L'), (1, 'R')]:
            cylinder(root, f'GEO_FuelCanister_{tag}', (side*.31, .24, .08), .16, .60, 'patch', 10, (math.pi/2, 0, 0))
            cylinder(root, f'GEO_HoseCoupler_{tag}', (side*.31, .24, .40), .07, .12, 'orange', 8)
    elif name == 'WPN_ShockCannon':
        cylinder(root, 'GEO_TurretBase', (0, .05, 0), .52, .14, 'heavy', 12, (math.pi/2, 0, 0))
        box(root, 'GEO_Breech', (0, .24, .20), (.64, .46, .62), 'body')
        cylinder(root, 'GEO_Emitter', (0, .22, .83), .13, 1.12, 'metal', 10)
        for z in (.45, .73, 1.01):
            cylinder(root, f'GEO_Coil_{int(z*100)}', (0, .22, z), .21, .08, 'orange', 10)
        box(root, 'GEO_Capacitor_L', (-.38, .21, .31), (.16, .34, .45), 'patch')
        box(root, 'GEO_Capacitor_R', (.38, .21, .31), (.16, .34, .45), 'patch')
    else:
        box(root, 'GEO_Rack', (0, .12, .20), (.82, .25, .73), 'heavy')
        for side, tag in [(-1, 'L'), (1, 'R')]:
            x = side*.25
            cylinder(root, f'GEO_Missile_{tag}', (x, .28, .52), .13, 1.15, 'patch', 10)
            cylinder(root, f'GEO_Nose_{tag}', (x, .28, 1.13), .095, .13, 'orange', 10)
            box(root, f'GEO_Fin_{tag}', (x, .14, .10), (.36, .055, .24), 'metal', .01)
    save(name)


def save(name):
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / f'{name}.blend'))


for weapon_id in ('WPN_Flamethrower', 'WPN_ShockCannon'):
    weapon(weapon_id)
