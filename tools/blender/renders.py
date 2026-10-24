"""MM-15: рендеры моделей сложной формы для презентации — пистолет и мишенный механизм.

На модель два кадра в docs/renders/:
- <имя>.png — чистый вид: EEVEE, три источника света, атлас MM-13 на материале;
- <имя>_wire.png — снимок окна 3D-вида с сеткой и оверлеем Statistics (число треугольников).
Исходники .blend не сохраняются — скрипт только смотрит.

Запуск в Blender с открытым окном:
    exec(open(r"B:\\Project-1\\tools\\blender\\renders.py", encoding="utf-8").read())
"""
import math
import os
import bpy
from mathutils import Vector

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~"
OUT = r"B:\Project-1\docs\renders"
ATLAS = r"B:\Project-1\Assets\_Rubezh\Art\Atlas\Weapon_Atlas.png"
PAPER = r"B:\Project-1\Assets\_Rubezh\Art\Target\Target_Precision.png"

# (имя, файл, коллекция, направление на камеру, детали в кадре — None: вся модель).
# У мишенного механизма направляющая 8,8 м: в кадре каретка с рамкой и листом, направляющая уходит вдаль.
MODELS = [
    ("pistol", "Pistol.blend", "Pistol", (1.0, -1.2, 0.45), None),
    ("target_mechanism", "TargetMechanism.blend", "TargetMechanism", (1.3, -1.6, 0.35),
     ("Carriage", "Rollers", "TargetFrame", "TargetPlane")),
]


def textured(mat_name, image_path):
    m = bpy.data.materials.get(mat_name)
    if m is None:
        return
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(image_path, check_existing=True)
    tex.interpolation = 'Closest' if 'Atlas' in image_path else 'Linear'
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.55


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == 'MESH' for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return (lo + hi) / 2, (hi - lo).length


def light(name, kind, loc, energy, size=1.0):
    d = bpy.data.lights.new(name, kind)
    d.energy = energy
    if kind == 'AREA':
        d.size = size
    o = bpy.data.objects.new(name, d)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    return o


def aim(ob, target):
    ob.rotation_euler = (target - ob.location).to_track_quat('-Z', 'Y').to_euler()


def render_clean(name, objs, direction):
    sc = bpy.context.scene
    center, size = bounds(objs)
    cam_data = bpy.data.cameras.new("RenderCam")
    cam_data.lens = 70
    cam = bpy.data.objects.new("RenderCam", cam_data)
    sc.collection.objects.link(cam)
    d = Vector(direction).normalized()
    cam.location = center + d * size * 2.1
    aim(cam, center)
    sc.camera = cam
    # Точная рамка: камера с тем же направлением придвигается так, чтобы модель вошла целиком.
    window = bpy.context.window_manager.windows[0]
    area = max((a for a in window.screen.areas if a.type == 'VIEW_3D'), key=lambda a: a.width * a.height)
    region = next(r for r in area.regions if r.type == 'WINDOW')
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    for o in bpy.context.view_layer.objects:
        o.select_set(o in objs)
    with bpy.context.temp_override(window=window, area=area, region=region):
        bpy.ops.view3d.camera_to_view_selected()
    cam.location = center + (cam.location - center) * 1.15     # поле вокруг модели
    for o in objs:
        o.select_set(False)
    for nm, loc, e in (("Key", d.cross(Vector((0, 0, 1))) * size * 1.5 + Vector((0, 0, size * 1.5)), 70),
                       ("Fill", -d.cross(Vector((0, 0, 1))) * size * 1.5 + Vector((0, 0, size * 0.5)), 25),
                       ("Rim", -d * size * 1.5 + Vector((0, 0, size)), 50)):
        lt = light(nm, 'AREA', center + loc, e * size * size, size)
        aim(lt, center)
    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = (0.11, 0.12, 0.13, 1)
    bg.inputs['Strength'].default_value = 0.6
    try:
        sc.render.engine = 'BLENDER_EEVEE_NEXT'
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = 'PNG'
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    return cam


def setup_wire_view(objs, direction):
    """Окно 3D-вида: сплошная заливка с сеткой и оверлей Statistics, модель в кадре с того же ракурса.
    Снимок окна делается после скрипта: Blender MCP get_screenshot_of_area (VIEW_3D) или вручную
    View → Area → Screenshot Area — screenshot_area из скрипта снимает пустой кадр."""
    window = bpy.context.window_manager.windows[0]
    area = max((a for a in window.screen.areas if a.type == 'VIEW_3D'), key=lambda a: a.width * a.height)
    space = area.spaces.active
    region = next(r for r in area.regions if r.type == 'WINDOW')
    space.shading.type = 'SOLID'
    space.overlay.show_wireframes = True
    space.overlay.wireframe_threshold = 1.0
    space.overlay.show_stats = True
    space.overlay.show_floor = False
    space.overlay.show_axis_x = space.overlay.show_axis_y = False
    space.overlay.show_extras = False
    for o in bpy.context.view_layer.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    space.region_3d.view_perspective = 'PERSP'
    space.region_3d.view_rotation = (-Vector(direction).normalized()).to_track_quat('-Z', 'Y')
    with bpy.context.temp_override(window=window, area=area, region=region):
        bpy.ops.view3d.view_selected()
    for o in objs:
        o.select_set(False)          # без оранжевого выделения: серая сетка читается лучше


def run():
    os.makedirs(OUT, exist_ok=True)
    report = {}
    for name, blend, coll, direction, focus in MODELS:
        bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, blend))
        textured("MM_Atlas", ATLAS)
        textured("MM_Target_Paper", PAPER)
        objs = [o for o in bpy.data.collections[coll].all_objects if o.type == 'MESH']
        render_clean(name, [o for o in objs if focus is None or o.name in focus], direction)
        deps = bpy.context.evaluated_depsgraph_get()
        tris = 0
        for o in objs:
            me = o.evaluated_get(deps).to_mesh()
            tris += sum(len(p.vertices) - 2 for p in me.polygons)
            o.evaluated_get(deps).to_mesh_clear()
        report[name] = tris
    return report


def wire(name):
    """Открыть модель name и подготовить окно для снимка сетки (см. setup_wire_view)."""
    for n, blend, coll, direction, focus in MODELS:
        if n == name:
            bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, blend))
            objs = [o for o in bpy.data.collections[coll].all_objects if o.type == 'MESH']
            setup_wire_view([o for o in objs if focus is None or o.name in focus], direction)


result = run()
