"""MM-10: мишенный механизм — направляющая, каретка с роликами, рамка, плоскость мишени, трос.

Координаты Blender: Unity (x, y, z) = (−x_b, z_b, −y_b). Перёд модели −Y (в Unity +Z).
Начала координат подвижных частей — там, откуда они движутся:
- Carriage — на оси направляющей в начальном положении (Unity Z 4,8, Y 2,8); едет вдоль −Y Blender
  (в Unity +Z, к пулеулавливателю);
- TargetFrame — на вертикальной оси поворота, в центре мишени (Unity Y 1,5); в файле лицом к −Y
  (Unity +Z), к стрелку рамку разворачивает код (180°), ребром — 90°;
- TargetPlane — в центре мишени, повёрнута на 180° внутри рамки: её локальная −Z в Unity смотрит туда
  же, куда лицо рамки (как у Quad: пробоины VP-23 и ShotResolver VP-08 не меняются).
Запуск: exec(open(r"...\\tools\\blender\\target_mechanism.py").read()).
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

exec(open(r"B:\Project-1\tools\blender\pistol_final.py", encoding="utf-8").read().split("PARTS = {")[0])

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~\TargetMechanism.blend"


def uv_unwrap(ob):
    """Smart UV Project с явным контекстом: после read_homefile у скрипта нет активного объекта."""
    scene = bpy.context.scene
    window = bpy.context.window or bpy.context.window_manager.windows[0]
    layer = scene.view_layers[0]
    layer.objects.active = ob
    with bpy.context.temp_override(window=window, view_layer=layer, active_object=ob, object=ob,
                                   selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=0.02)
        bpy.ops.object.mode_set(mode='OBJECT')
STEEL_L = ('MM_Target_Steel', '#A3AAB0', 0.4)
DARK = ('MM_Target_Dark', '#4B535B', 0.6)
PAPER = ('MM_Target_Paper', '#EDE6D3', 0.9)
CABLE = ('MM_Target_Cable', '#1E2124', 0.7)

RAIL_Y = 2.8
Z0, Z1 = 4.6, 13.3          # направляющая в Unity Z
CARRIAGE_Z = 4.8            # начальное положение каретки
TARGET_Y = 1.5


def u2b(x, y, z):
    return Vector((-x, -z, y))


def new_ob(name, bm, spec, parent=None, loc=Vector(), rot_z=0.0, cutters=(), bevel=0.0):
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.data.collections['TargetMechanism'].objects.link(ob)
    ob.parent = parent
    ob.location = loc
    ob.rotation_euler = (0, 0, rot_z)
    build_mesh(ob, bm, material(spec), cutters, bevel)
    return ob


def rail_mesh():
    bm = bmesh.new()
    # двутавр вдоль Y Blender: полки 0,08 × 0,012, стенка 0,012 × 0,076
    length = Z1 - Z0
    section = [(-0.04, -0.05), (0.04, -0.05), (0.04, -0.038), (0.006, -0.038), (0.006, 0.038), (0.04, 0.038),
               (0.04, 0.05), (-0.04, 0.05), (-0.04, 0.038), (-0.006, 0.038), (-0.006, -0.038), (-0.04, -0.038)]
    prism_y(bm, section, 0.0, -length)
    for k in range(5):                                         # подвесы к потолку
        y = -0.3 - k * (length - 0.6) / 4
        box(bm, Vector((0, y, 0.12)), (0.03, 0.03, 0.14))
        box(bm, Vector((0, y, 0.19)), (0.12, 0.08, 0.01))
    box(bm, Vector((0, -length - 0.02, 0)), (0.1, 0.04, 0.12))  # упор в конце
    box(bm, Vector((0, 0.02, 0)), (0.1, 0.04, 0.12))            # упор в начале
    return bm


def carriage_mesh():
    bm = bmesh.new()
    box(bm, Vector((0, 0, -0.1)), (0.22, 0.28, 0.08))           # корпус под направляющей
    box(bm, Vector((0, 0.05, -0.1)), (0.24, 0.1, 0.1))           # привод
    for sx in (-1, 1):
        box(bm, Vector((sx * 0.06, 0, -0.03)), (0.015, 0.24, 0.07))   # щёки к роликам
    top, bottom = -0.14, TARGET_Y + 0.45 - RAIL_Y                # от корпуса до узла поворота рамки
    cyl(bm, Vector((0, 0, (top + bottom) / 2)), 0.012, top - bottom, 'Z', segments=10)  # штанга
    return bm


def rollers_mesh():
    bm = bmesh.new()
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(bm, Vector((sx * 0.025, sy * 0.09, -0.032)), 0.018, 0.018, 'X', segments=14)
    return bm


def frame_mesh():
    bm = bmesh.new()
    s, w, d = 0.56, 0.03, 0.02                                  # рамка 0,56 м, брус 3 см
    box(bm, Vector((0, 0, s / 2 - w / 2)), (s, d, w))
    box(bm, Vector((0, 0, -s / 2 + w / 2)), (s, d, w))
    box(bm, Vector((s / 2 - w / 2, 0, 0)), (w, d, s - 2 * w))
    box(bm, Vector((-s / 2 + w / 2, 0, 0)), (w, d, s - 2 * w))
    box(bm, Vector((0, 0.01, 0)), (0.02, 0.012, s - 2 * w))      # стойка за мишенью
    box(bm, Vector((0, 0, s / 2 + 0.03)), (0.06, 0.03, 0.05))    # узел поворота
    cyl(bm, Vector((0, 0, s / 2 + 0.11)), 0.014, 0.12, 'Z', segments=10)
    for sx in (-1, 1):                                         # прижимы бумаги
        for sz in (-1, 1):
            box(bm, Vector((sx * 0.23, -0.012, sz * 0.23)), (0.04, 0.006, 0.04))
    return bm


def plane_mesh():
    """Лист 0,5 × 0,5 × 3 мм; лицевая грань в +Y локально (в рамке повёрнут на 180°), UV 0…1 на ней."""
    bm = bmesh.new()
    h = 0.25
    t = 0.0015
    vs = {}
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                vs[(sx, sy, sz)] = bm.verts.new((sx * h, sy * t, sz * h))
    uv = bm.loops.layers.uv.new('UVMap')
    faces = {
        'front': [(1, 1, -1), (-1, 1, -1), (-1, 1, 1), (1, 1, 1)],     # +Y локально
        'back': [(-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1)],
        'left': [(-1, -1, -1), (-1, -1, 1), (-1, 1, 1), (-1, 1, -1)],
        'right': [(1, 1, -1), (1, 1, 1), (1, -1, 1), (1, -1, -1)],
        'top': [(-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)],
        'bottom': [(-1, 1, -1), (1, 1, -1), (1, -1, -1), (-1, -1, -1)],
    }
    for name, keys in faces.items():
        f = bm.faces.new([vs[k] for k in keys])
        for loop in f.loops:
            co = loop.vert.co
            if name == 'front':
                # смотрим на лицо снаружи (+Y): правая сторона листа — −X локально
                loop[uv].uv = (0.5 - co.x / (2 * h), 0.5 + co.z / (2 * h))
            else:
                loop[uv].uv = (0.001, 0.001)     # кромки и оборот — в угол текстуры (белое поле)
    return bm


def cable_mesh():
    bm = bmesh.new()
    length = Z1 - Z0
    cyl(bm, Vector((0.05, -length / 2, -0.03)), 0.004, length, 'Y', segments=6)
    return bm


def build():
    bpy.ops.wm.read_homefile(use_empty=True)
    scene = bpy.context.scene
    coll = bpy.data.collections.new('TargetMechanism')
    scene.collection.children.link(coll)
    root = bpy.data.objects.new('TargetMechanism', None)
    coll.objects.link(root)

    rail = new_ob('Rail', rail_mesh(), DARK, root, u2b(0, RAIL_Y, Z0))
    carriage = new_ob('Carriage', carriage_mesh(), DARK, root, u2b(0, RAIL_Y, CARRIAGE_Z), bevel=0.004)
    new_ob('Rollers', rollers_mesh(), STEEL_L, carriage)
    frame = new_ob('TargetFrame', frame_mesh(), STEEL_L, carriage, Vector((0, 0, TARGET_Y - RAIL_Y)))
    plane = new_ob('TargetPlane', plane_mesh(), PAPER, frame, Vector((0, 0, 0)), rot_z=math.pi)
    new_ob('Cable', cable_mesh(), CABLE, rail)
    # UV листа нельзя менять Smart UV Project, у остальных — для атласа MM-13
    for ob in coll.objects:
        if ob.type == 'MESH' and ob.name != 'TargetPlane':
            uv_unwrap(ob)
    bpy.ops.wm.save_as_mainfile(filepath=SRC)
    return {ob.name: triangles(ob) for ob in coll.objects if ob.type == 'MESH'}


def flatten_for_fbx():
    """Экспорт FBX с Apply Transform ломает вложенность глубже одного уровня (дети детей приезжают
    в Unity без перевода осей). Перед экспортом все детали — прямые дети корня с теми же мировыми
    положениями; иерархию Carriage → TargetFrame → TargetPlane собирает префаб в Unity."""
    root = bpy.data.objects['TargetMechanism']
    for ob in list(bpy.data.collections['TargetMechanism'].objects):
        if ob is root or ob.parent is root:
            continue
        world = ob.matrix_world.copy()
        ob.parent = root
        ob.matrix_parent_inverse = root.matrix_world.inverted()
        ob.matrix_world = world


tri = build()
flatten_for_fbx()          # в .blend остаётся настоящая иерархия: файл сохранён до этого шага
exec(open(r"B:\Project-1\tools\blender\rubezh_fbx.py", encoding="utf-8").read())
total = export_collection('TargetMechanism', r"B:\Project-1\Assets\_Rubezh\Art\Target\TargetMechanism.fbx")
result = {"parts": tri, "total": total}
