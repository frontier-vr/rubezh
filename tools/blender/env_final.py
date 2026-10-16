"""YG-10: финальные модели окружения Рубежа по docs/layout.md и бюджетам задачи.

Те же корни, имена деталей, начала координат и пустышки, что у блок-моделей YG-06
(tools/blender/env_blockout.py): Никита подменяет блоки без перестановки сцены — ссылки сцены
и коллайдеры префабов держатся за имена Door, WeaponSlot, Button, Dial, CatcherZone, Slot_*, Spawn.
Рабочие поверхности на прежней высоте: столешница 0,9, стойка и полка осмотра 1,0, полка СИЗ 1,22,
полка шкафа 1,21; на них лежат контуры мест YG-11.

Детали описываются в осях Unity относительно корня (X вправо, Y вверх, Z — лицом к пользователю)
и переводятся в Blender: (x, y, z) → (−x, −z, y). Мебель — лицевой стороной к −Y Blender.
UV0 — в ячейке атласа-палитры своего материала (YG-12, tools/blender/atlas.py): у всего окружения
один материал ENV_Atlas и одна текстура 2048; второй UV для lightmap — Generate Lightmap UVs в
импорте Unity. Прозрачности нет; рассеиватель светильника светится картой эмиссии атласа.

Запуск: exec(open(r"B:\\Project-1\\tools\\blender\\env_final.py", encoding="utf-8").read())
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

exec(open(r"B:\Project-1\tools\blender\rubezh_fbx.py", encoding="utf-8").read())
exec(open(r"B:\Project-1\tools\blender\atlas.py", encoding="utf-8").read())

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~\Environment.blend"
OUT = r"B:\Project-1\Assets\_Rubezh\Art\Environment"

# Палитра docs/palette.md: имя материала = имя .mat в Unity (Art/Environment/Materials, Signage)
MATS = {
    "wall": ("ENV_ConcreteWall", "#8E8D88", 0.9, 0.0, False),
    "floor": ("ENV_ConcreteFloor", "#66675F", 0.85, 0.0, False),
    "plaster": ("ENV_Plaster", "#C9C7C0", 0.9, 0.0, False),
    "paint": ("ENV_PaintedMetal", "#4B535B", 0.6, 0.2, False),
    "steel": ("ENV_Steel", "#A3AAB0", 0.4, 0.7, False),
    "yellow": ("ENV_SignalYellow", "#F2C200", 0.6, 0.0, False),
    "white": ("ENV_SignWhite", "#ECEAE4", 0.7, 0.0, False),
    "graphite": ("ENV_Graphite", "#1D2228", 0.75, 0.0, False),
    "lamp": ("ENV_LampDiffuser", "#F2F0EA", 0.5, 0.0, True),
}

BUDGET = {"Room": 6000, "Cabinet": 2000, "LineStand": 1500, "TargetRemote": 600,
          "PrepTable": 1000, "PpeShelf": 1000, "AmmoTray": 1000, "Bench": 500}


def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0])


def material(key):
    name, color, rough, metal, emit = MATS[key]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = srgb(color)
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    if emit:
        bsdf.inputs['Emission Color'].default_value = srgb(color)
        bsdf.inputs['Emission Strength'].default_value = 2.0
    m.diffuse_color = srgb(color)
    return m


class Part:
    """Меш из примитивов в осях Unity: копит вершины, грани и материалы, в конце — один объект."""

    def __init__(self, name, pivot=(0, 0, 0)):
        self.name = name
        self.pivot = Vector(pivot)
        self.verts, self.faces, self.mats = [], [], []
        self.keys = []

    def _take(self, bm, key, matrix):
        bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts)
        if key not in self.keys:
            self.keys.append(key)
        mi = self.keys.index(key)
        base = len(self.verts)
        bm.verts.index_update()
        self.verts += [v.co.copy() for v in bm.verts]
        for f in bm.faces:
            self.faces.append([base + v.index for v in f.verts])
            self.mats.append(mi)
        bm.free()

    @staticmethod
    def _place(center, rx=0.0, ry=0.0, rz=0.0):
        rot = (Matrix.Rotation(math.radians(ry), 4, 'Y') @ Matrix.Rotation(math.radians(rx), 4, 'X')
               @ Matrix.Rotation(math.radians(rz), 4, 'Z'))
        return Matrix.Translation(Vector(center)) @ rot

    def box(self, center, size, key, bevel=0.0, rx=0.0, ry=0.0, rz=0.0):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.transform(bm, matrix=Matrix.Diagonal((*size, 1)), verts=bm.verts)
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type='OFFSET', segments=1,
                            profile=0.5, affect='EDGES', clamp_overlap=True)
        self._take(bm, key, self._place(center, rx, ry, rz))

    def cyl(self, center, radius, length, axis, key, segments=12, radius2=None):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius,
                              radius2=radius if radius2 is None else radius2, depth=length)
        # create_cone строит вдоль Z; переводим на нужную ось Unity
        turn = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'),
                'Y': Matrix.Rotation(math.radians(-90), 4, 'X')}[axis]
        bmesh.ops.transform(bm, matrix=turn, verts=bm.verts)
        self._take(bm, key, Matrix.Translation(Vector(center)))

    def wedge_x(self, profile_zy, x0, x1, key):
        """Призма вдоль X: профиль — точки (z, y) против часовой, если смотреть с +X."""
        bm = bmesh.new()
        a = [bm.verts.new((x0, y, z)) for z, y in profile_zy]
        b = [bm.verts.new((x1, y, z)) for z, y in profile_zy]
        n = len(a)
        bm.faces.new(list(reversed(a)))
        bm.faces.new(b)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        self._take(bm, key, Matrix.Identity(4))

    def build(self, coll, parent):
        me = bpy.data.meshes.new(self.name)
        bm = bmesh.new()
        bv = []
        for co in self.verts:
            p = co - self.pivot
            bv.append(bm.verts.new((-p.x, -p.z, p.y)))       # Unity → Blender
        for f, mi in zip(self.faces, self.mats):
            # Перевод осей — зеркальный (det = −1): обходим грань в обратную сторону
            face = bm.faces.new([bv[i] for i in reversed(f)])
            face.material_index = mi
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        uv = bm.loops.layers.uv.new('UVMap')
        for face in bm.faces:
            n = face.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            for loop in face.loops:
                c = loop.vert.co
                loop[uv].uv = ((c.y, c.z), (c.x, c.z), (c.x, c.y))[ax]
        bm.to_mesh(me)
        bm.free()
        for key in self.keys:
            me.materials.append(material(key))
        ob = bpy.data.objects.new(self.name, me)
        coll.objects.link(ob)
        ob.parent = parent
        p = self.pivot
        ob.location = (-p.x, -p.z, p.y)
        return ob


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = 'ARROWS'
    root.empty_display_size = 0.3
    c.objects.link(root)
    return c, root


def empty(coll, parent, name, pos, yaw_unity=0.0, size=0.08):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'PLAIN_AXES'
    e.empty_display_size = size
    coll.objects.link(e)
    e.parent = parent
    e.location = (-pos[0], -pos[2], pos[1])
    e.rotation_euler = (0, 0, math.radians(-yaw_unity))
    return e


# ---------------------------------------------------------------- комната

def room():
    c, r = collection("Room")
    p = Part("Floor")
    p.box((0, -0.05, 7), (8, 0.1, 14), "floor")
    p.build(c, r)

    p = Part("Ceiling")
    p.box((0, 3.05, 7), (8, 0.1, 14), "plaster")
    for z in (4.6, 13.4):                                             # балки над коридором
        p.box((0, 2.86, z), (8, 0.28, 0.3), "plaster", bevel=0.02)
    for x, z in ((-2.6, 2.2), (2.6, 2.2)):                           # решётки вентиляции
        p.box((x, 2.985, z), (0.6, 0.03, 0.3), "steel", bevel=0.005)
        for k in range(5):
            p.box((x, 2.965, z - 0.12 + k * 0.06), (0.56, 0.01, 0.02), "graphite")
    p.build(c, r)

    p = Part("Walls")
    p.box((-4.05, 1.5, 7), (0.1, 3, 14), "wall")
    p.box((4.05, 1.5, 7), (0.1, 3, 14), "wall")
    p.box((0, 1.5, -0.05), (8, 3, 0.1), "wall")
    p.box((0, 1.5, 14.05), (8, 3, 0.1), "wall")
    for x in (-3.9, 3.9):                                             # пилястры
        for z in (4.75, 9.0):
            p.box((x, 1.5, z), (0.2, 3, 0.4), "wall", bevel=0.015)
    # плинтус графит: боковые стены, задняя, передняя с проёмом двери
    for x in (-3.99, 3.99):
        p.box((x, 0.06, 7), (0.02, 0.12, 14), "graphite")
    p.box((0, 0.06, 13.99), (8, 0.12, 0.02), "graphite")
    p.box((-2.7, 0.06, 0.01), (2.6, 0.12, 0.02), "graphite")
    p.box((1.9, 0.06, 0.01), (4.2, 0.12, 0.02), "graphite")
    # жёлтая полоса на пилястрах у входа в коридор — граница зоны рубежа
    for x in (-3.79, 3.79):
        p.box((x, 1.2, 4.75), (0.005, 0.1, 0.4), "yellow")
    p.build(c, r)

    p = Part("EntranceDoor")                                          # O-01
    p.box((-0.8, 1.03, 0.035), (0.94, 2.04, 0.05), "paint", bevel=0.006)
    p.box((-1.3, 1.06, 0.04), (0.06, 2.12, 0.08), "steel")
    p.box((-0.3, 1.06, 0.04), (0.06, 2.12, 0.08), "steel")
    p.box((-0.8, 2.12, 0.04), (1.06, 0.06, 0.08), "steel")
    p.box((-0.8, 0.12, 0.065), (0.9, 0.2, 0.004), "steel")            # отбойник
    p.cyl((-0.8, 1.0, 0.13), 0.015, 0.62, 'X', "steel", segments=10)  # штанга «антипаника»
    for x in (-1.08, -0.52):
        p.box((x, 1.0, 0.095), (0.04, 0.06, 0.07), "graphite", bevel=0.005)
    p.build(c, r)

    for name, x, inner in (("SideShield_Left", -1.6, 1), ("SideShield_Right", 1.6, -1)):   # O-33, O-34
        p = Part(name)
        p.box((x, 1.5, 9.0), (0.1, 3, 8.8), "paint")
        for k in range(9):                                            # рёбра жёсткости внутрь коридора
            p.box((x + inner * 0.07, 1.5, 4.7 + k * 1.075), (0.04, 3, 0.08), "paint", bevel=0.008)
        p.box((x + inner * 0.055, 0.1, 9.0), (0.01, 0.2, 8.8), "graphite")
        p.box((x, 2.99, 9.0), (0.14, 0.02, 8.8), "steel")
        p.build(c, r)

    p = Part("BulletTrap")                                            # O-35
    p.box((0, 1.5, 13.85), (3.2, 3, 0.3), "paint")
    for x in (-1.55, 1.55):
        p.box((x, 1.5, 13.55), (0.1, 3, 0.32), "steel", bevel=0.01)
    for k in range(6):                                                # наклонные стальные ламели
        p.box((0, 0.42 + k * 0.47, 13.58), (3.0, 0.56, 0.03), "steel", rx=-35)
    p.box((0, 0.15, 13.45), (3.0, 0.3, 0.3), "graphite", bevel=0.02)  # лоток с песком
    p.box((0, 2.95, 13.55), (3.2, 0.1, 0.32), "steel")
    p.build(c, r)

    p = Part("CeilingBaffles")                                        # O-37
    for z in (7.0, 9.5, 12.0):
        p.box((0, 2.78, z), (3.1, 0.4, 0.02), "steel", rx=45)
        for x in (-1.4, 1.4):
            p.box((x, 2.93, z), (0.03, 0.14, 0.03), "graphite")
    p.build(c, r)

    p = Part("Lights")                                                # светильники, без коллайдера
    for x in (-2.4, 0.0, 2.4):
        for z in (1.2, 3.2):
            p.box((x, 2.97, z), (1.2, 0.06, 0.18), "steel", bevel=0.008)
            p.box((x, 2.936, z), (1.14, 0.01, 0.12), "lamp")
    for z in (5.8, 8.25, 10.75, 13.0):
        p.box((0, 2.97, z), (0.18, 0.06, 1.2), "steel", bevel=0.008)
        p.box((0, 2.936, z), (0.12, 0.01, 1.14), "lamp")
    p.build(c, r)
    return c


# ---------------------------------------------------------------- оружейный шкаф O-03

def cabinet():
    c, r = collection("Cabinet")
    W, D, H = 1.0, 0.6, 1.9
    p = Part("Body")
    p.box((0, H / 2, -D / 2 + 0.01), (W, H, 0.02), "paint")
    for x in (-W / 2 + 0.01, W / 2 - 0.01):
        p.box((x, H / 2, 0), (0.02, H, D), "paint", bevel=0.004)
    p.box((0, H - 0.01, 0), (W, 0.02, D), "paint", bevel=0.004)
    p.box((0, 0.05, -0.01), (W - 0.04, 0.1, D - 0.04), "graphite")             # цоколь
    p.box((0, 0.45, D / 2 - 0.01), (W - 0.04, 0.7, 0.02), "paint")             # нижняя панель
    for k in range(4):                                                        # прорези вентиляции
        p.box((0, 0.55 + k * 0.04, D / 2 + 0.001), (0.5, 0.012, 0.004), "graphite")
    p.box((0, 0.8, -0.01), (W - 0.04, 0.02, D - 0.06), "paint")                 # полка над нижним отсеком
    p.box((0, 1.2, -0.02), (W - 0.04, 0.02, D - 0.06), "paint")                 # полка с гнездом O-05,
    # верх 1,21 свободен: на нём зелёная метка финиша O-06 (YG-11)
    p.box((0, 1.6, -0.06), (W - 0.04, 0.02, D - 0.14), "paint")                 # верхняя полка
    p.box((-0.22, 1.69, -0.08), (0.3, 0.16, 0.22), "graphite", bevel=0.006)     # ящики для принадлежностей
    p.box((0.18, 1.67, -0.08), (0.26, 0.12, 0.22), "paint", bevel=0.006)
    for y in (0.95, 1.75):                                                    # петли дверцы
        p.cyl((W / 2, y, D / 2 + 0.01), 0.012, 0.08, 'Y', "steel", segments=8)
    p.build(c, r)

    # Дверца O-04: начало координат на петле, открыта сценой на +100°
    p = Part("Door", pivot=(W / 2, 1.35, D / 2 + 0.01))
    p.box((W / 2 - 0.45, 1.35, D / 2 + 0.01), (0.9, 1.1, 0.02), "paint", bevel=0.004)
    cx, cy, zf = W / 2 - 0.45, 1.35, D / 2 + 0.025
    p.box((cx, cy + 0.52, zf), (0.86, 0.03, 0.01), "paint")                    # рамка жёсткости
    p.box((cx, cy - 0.52, zf), (0.86, 0.03, 0.01), "paint")
    p.box((cx - 0.415, cy, zf), (0.03, 1.01, 0.01), "paint")
    p.box((cx + 0.415, cy, zf), (0.03, 1.01, 0.01), "paint")
    for k in range(3):
        p.box((cx, cy + 0.3 + k * 0.05, zf), (0.4, 0.012, 0.004), "graphite")  # прорези
    hx = cx - 0.36
    p.cyl((hx, cy, D / 2 + 0.06), 0.009, 0.18, 'Y', "steel", segments=8)        # ручка
    for dy in (-0.08, 0.08):
        p.cyl((hx, cy + dy, D / 2 + 0.04), 0.007, 0.04, 'Z', "steel", segments=6)
    p.cyl((hx, cy + 0.2, D / 2 + 0.03), 0.015, 0.02, 'Z', "steel", segments=12)  # замок
    p.box((cx, cy - 0.3, zf + 0.002), (0.24, 0.06, 0.004), "white")             # бирка
    p.build(c, r)

    p = Part("InspectionShelf")                                               # O-07, верх 1,0
    p.box((0, 0.98, D / 2 + 0.15), (0.9, 0.04, 0.3), "paint", bevel=0.004)
    p.box((0, 1.005, D / 2 + 0.295), (0.9, 0.05, 0.01), "steel")               # бортик
    for x in (-0.35, 0.35):
        p.wedge_x([(D / 2, 0.96), (D / 2, 0.72), (D / 2 + 0.26, 0.96)], x - 0.01, x + 0.01, "steel")
    p.build(c, r)
    empty(c, r, "WeaponSlot", (0, 1.3, 0.0), yaw_unity=-90)
    return c


# ---------------------------------------------------------------- полка СИЗ O-09

def ppe_shelf():
    c, r = collection("PpeShelf")
    p = Part("Shelf")
    p.box((0, 1.2, 0), (0.7, 0.04, 0.4), "paint", bevel=0.004)                 # верх 1,22
    p.box((0, 1.1, -0.19), (0.7, 0.24, 0.02), "paint", bevel=0.003)            # настенная планка
    for x in (-0.3, 0.3):
        p.wedge_x([(-0.18, 1.18), (-0.18, 0.99), (0.12, 1.18)], x - 0.01, x + 0.01, "steel")
    p.box((0, 1.2, 0.2), (0.7, 0.04, 0.008), "yellow")                         # жёлтая кромка
    p.box((0.18, 1.2225, 0), (0.22, 0.005, 0.22), "graphite")                  # коврики мест O-10, O-12
    p.box((-0.2, 1.2225, 0), (0.2, 0.005, 0.16), "graphite")
    for x in (-0.3, 0.3):                                                     # крепёж к стене
        p.cyl((x, 1.1, -0.175), 0.008, 0.01, 'Z', "steel", segments=8)
    p.build(c, r)
    empty(c, r, "Slot_Earmuffs", (0.18, 1.3, 0.0))
    empty(c, r, "Slot_Glasses", (-0.2, 1.26, 0.0))
    return c


# ---------------------------------------------------------------- стол снаряжения O-17

def prep_table():
    c, r = collection("PrepTable")
    p = Part("Table")
    p.box((0, 0.88, 0), (1.6, 0.04, 0.8), "paint", bevel=0.004)               # верх 0,9
    p.box((0, 0.88, 0.402), (1.6, 0.04, 0.006), "graphite")                    # кромка к пользователю
    for z in (-0.36, 0.36):                                                   # царга
        p.box((0, 0.82, z), (1.5, 0.08, 0.03), "steel")
    for x in (-0.76, 0.76):
        p.box((x, 0.82, 0), (0.03, 0.08, 0.7), "steel")
    for x in (-0.75, 0.75):
        for z in (-0.35, 0.35):
            p.box((x, 0.42, z), (0.05, 0.8, 0.05), "steel", bevel=0.006)       # ножки
            p.box((x, 0.01, z), (0.065, 0.02, 0.065), "graphite")              # опоры
    p.box((0, 0.2, 0), (1.46, 0.02, 0.66), "paint", bevel=0.003)               # нижняя полка
    p.build(c, r)
    return c


# ---------------------------------------------------------------- лоток с патронами O-18

def ammo_tray():
    c, r = collection("AmmoTray")
    p = Part("Tray")
    p.box((0, 0.005, 0), (0.4, 0.01, 0.3), "paint", bevel=0.002)
    p.box((0, 0.0125, 0), (0.37, 0.005, 0.27), "graphite")                     # вставка, ниже Spawn
    for z in (-0.145, 0.145):
        p.box((0, 0.03, z), (0.4, 0.05, 0.01), "paint", bevel=0.002)
    for x in (-0.195, 0.195):
        p.box((x, 0.03, 0), (0.01, 0.05, 0.3), "paint", bevel=0.002)
        p.cyl((x * 1.1, 0.045, 0), 0.006, 0.12, 'Z', "steel", segments=8)      # ручки
        for dz in (-0.05, 0.05):
            p.cyl((x * 1.05, 0.045, dz), 0.004, 0.02, 'X', "steel", segments=6)
    p.box((0, 0.0553, 0.145), (0.12, 0.0006, 0.008), "yellow")                 # метка лицевой стороны
    p.build(c, r)
    empty(c, r, "Spawn", (0, 0.02, 0), size=0.03)
    return c


# ---------------------------------------------------------------- стойка рубежа O-24, O-22, O-21

def line_stand():
    c, r = collection("LineStand")
    p = Part("Stand")
    p.box((0, 0.98, 0), (1.4, 0.04, 0.5), "paint", bevel=0.004)                # верх 1,0
    p.box((0, 0.98, 0.252), (1.4, 0.05, 0.006), "graphite")                    # кромка к стрелку
    p.box((0, 0.48, -0.2), (1.3, 0.96, 0.04), "paint")                         # дальняя панель
    for x in (-0.65, 0.65):
        p.box((x, 0.48, 0), (0.04, 0.96, 0.45), "paint", bevel=0.004)
        p.box((x, 0.02, 0), (0.06, 0.04, 0.5), "graphite")                     # опоры
    p.box((0, 0.3, -0.05), (1.26, 0.02, 0.28), "paint")                         # полка внизу
    p.box((0, 0.9, -0.17), (1.26, 0.12, 0.02), "yellow")                       # жёлтая полоса на дальней панели
    p.build(c, r)

    # Гильзоуловитель: короб с поддоном-сеткой на 0,73 — гильза ложится внутрь объёма CatcherZone
    # (CasingEjector, центр 0,85, высота 0,8) и там остаётся.
    p = Part("CasingCatcher")
    x0 = -0.95
    p.box((x0, 0.03, 0), (0.4, 0.06, 0.5), "graphite")
    p.box((x0, 0.42, 0.24), (0.4, 0.76, 0.02), "paint")
    p.box((x0, 0.42, -0.24), (0.4, 0.76, 0.02), "paint")
    p.box((x0 + 0.19, 0.42, 0), (0.02, 0.76, 0.46), "paint")
    p.box((x0 - 0.19, 0.6, 0), (0.02, 1.12, 0.46), "paint")                     # отбойник выше — ловит гильзу
    p.box((x0, 0.72, 0), (0.36, 0.02, 0.46), "graphite")                       # поддон, верх 0,73
    p.box((x0, 0.79, 0.25), (0.42, 0.02, 0.012), "yellow")                     # жёлтая кромка
    p.box((x0 + 0.2, 0.79, 0), (0.012, 0.02, 0.5), "yellow")
    p.build(c, r)

    p = Part("CasingShelf")                                                   # O-21
    p.box((-0.6, 1.005, 0.17), (0.15, 0.01, 0.1), "steel")
    for dz in (-0.045, 0.045):
        p.box((-0.6, 1.015, 0.17 + dz), (0.15, 0.02, 0.01), "steel")
    for dx in (-0.07, 0.07):
        p.box((-0.6 + dx, 1.015, 0.17), (0.01, 0.02, 0.1), "steel")
    p.build(c, r)
    empty(c, r, "CatcherZone", (-0.95, 0.85, 0))
    return c


# ---------------------------------------------------------------- скамья O-13

def bench():
    c, r = collection("Bench")
    # Имя меша — как у блок-модели («Bench.001»: корень уже зовётся Bench), за него держится коллайдер.
    p = Part("Bench.001")
    p.box((0, 0.43, 0), (0.6, 0.035, 0.36), "paint", bevel=0.006)                # сиденье, верх 0,45
    for x in (-0.25, 0.25):
        p.box((x, 0.21, 0.15), (0.035, 0.42, 0.035), "steel", bevel=0.004)      # ножки
        p.box((x, 0.21, -0.15), (0.035, 0.42, 0.035), "steel", bevel=0.004)
        p.box((x, 0.1, 0), (0.025, 0.025, 0.3), "steel")                       # перемычки
        for z in (-0.15, 0.15):
            p.box((x, 0.01, z), (0.045, 0.02, 0.045), "graphite")              # опоры
    p.box((0, 0.1, 0), (0.48, 0.025, 0.025), "steel")
    p.build(c, r)
    return c


# ---------------------------------------------------------------- пульт подачи мишени O-25

def target_remote():
    c, r = collection("TargetRemote")
    p = Part("Housing")
    p.box((0, 0.03, 0), (0.2, 0.06, 0.15), "paint", bevel=0.006)
    p.box((0, 0.0605, 0), (0.18, 0.002, 0.13), "graphite")                     # лицевая панель
    p.cyl((0.05, 0.062, 0.02), 0.025, 0.004, 'Y', "steel", segments=16)         # кольцо кнопки
    for k in range(7):                                                        # шкала регулятора
        a = math.radians(-135 + k * 45)
        p.box((-0.05 + math.sin(a) * 0.033, 0.0618, 0.02 + math.cos(a) * 0.033), (0.002, 0.001, 0.006),
              "white", ry=math.degrees(a))
    p.build(c, r)

    p = Part("Button", pivot=(0.05, 0.06, 0.02))
    p.cyl((0.05, 0.0705, 0.02), 0.018, 0.017, 'Y', "yellow", segments=16, radius2=0.016)
    p.build(c, r)

    p = Part("Dial", pivot=(-0.05, 0.06, 0.02))
    p.cyl((-0.05, 0.071, 0.02), 0.022, 0.018, 'Y', "graphite", segments=18, radius2=0.019)
    p.box((-0.05, 0.0805, 0.028), (0.004, 0.002, 0.018), "white")              # указатель вперёд
    p.build(c, r)
    return c


def build():
    bpy.ops.wm.read_homefile(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    makers = {"Room": room, "Cabinet": cabinet, "PpeShelf": ppe_shelf, "PrepTable": prep_table,
              "AmmoTray": ammo_tray, "LineStand": line_stand, "TargetRemote": target_remote, "Bench": bench}
    for f in makers.values():
        f()
    # YG-12: все модели — на один атлас-палитру 2048 (Art/Atlas/Env_Atlas.png), материал ENV_Atlas
    missing = atlasize([o for o in bpy.data.objects if o.type == 'MESH'], ENV_CELLS, "ENV_Atlas")
    assert not missing, missing
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=SRC)
    report = {}
    for name in makers:
        tris = export_collection(name, OUT + "\\Env_" + name + ".fbx")
        report[name] = (tris, BUDGET[name])
    return report


result = build()
