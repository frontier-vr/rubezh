"""Блок-модели окружения Рубежа (YG-06) по docs/layout.md.

Комната строится сразу в координатах зала. Мебель — каждая в своём начале координат: центр
опоры на полу, лицевая сторона к −Y Blender (в Unity +Z); в сцене её разворачивает Никита (NG-04).
Перевод осей Blender → Unity: Unity (X, Y, Z) = (−x, z, −y), поворот по Unity Y на β = поворот
по Blender Z на −β.
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector


def material(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.8
    m.diffuse_color = (*rgb, 1)
    return m


def u2b(x, y, z):
    """Точка из координат Unity в координаты Blender."""
    return Vector((-x, -z, y))


class Builder:
    def __init__(self):
        sc = bpy.context.scene
        sc.unit_settings.system = 'METRIC'
        sc.unit_settings.scale_length = 1.0
        self.scene = sc
        self.mat = {
            "concrete": material("Blockout_Concrete", (0.55, 0.55, 0.53)),
            "metal": material("Blockout_Metal", (0.30, 0.32, 0.35)),
            "yellow": material("Blockout_Yellow", (0.95, 0.75, 0.10)),
            "paper": material("Blockout_Paper", (0.90, 0.88, 0.82)),
        }

    def collection(self, name):
        c = bpy.data.collections.new(name)
        self.scene.collection.children.link(c)
        return c

    def root(self, coll, name):
        e = bpy.data.objects.new(name, None)
        e.empty_display_type = 'ARROWS'
        e.empty_display_size = 0.3
        coll.objects.link(e)
        return e

    def mesh(self, coll, parent, name, boxes, mat, pivot=(0, 0, 0)):
        """boxes: [(центр Unity-local, размер Unity-local X×Y×Z)] — в осях Unity относительно корня."""
        me = bpy.data.meshes.new(name)
        bm = bmesh.new()
        pv = u2b(*pivot)
        for c, s in boxes:
            g = bmesh.ops.create_cube(bm, size=1.0)
            cb = u2b(*c)
            for v in g["verts"]:
                v.co = Vector((v.co.x * s[0], v.co.y * s[2], v.co.z * s[1])) + cb - pv
        bm.to_mesh(me)
        bm.free()
        me.materials.append(self.mat[mat])
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        ob.parent = parent
        ob.location = pv
        return ob

    def empty(self, coll, parent, name, pos, yaw_unity=0.0, size=0.08):
        e = bpy.data.objects.new(name, None)
        e.empty_display_type = 'PLAIN_AXES'
        e.empty_display_size = size
        coll.objects.link(e)
        e.parent = parent
        e.location = u2b(*pos)
        e.rotation_euler = (0, 0, math.radians(-yaw_unity))
        return e


def build():
    bpy.ops.wm.read_homefile(use_empty=True)
    B = Builder()

    # ---- Комната: в координатах зала (docs/layout.md §1, §3)
    c = B.collection("Room")
    r = B.root(c, "Room")
    B.mesh(c, r, "Floor", [((0, -0.05, 7), (8, 0.1, 14))], "concrete")
    B.mesh(c, r, "Ceiling", [((0, 3.05, 7), (8, 0.1, 14))], "concrete")
    B.mesh(c, r, "Walls", [((-4.05, 1.5, 7), (0.1, 3, 14)), ((4.05, 1.5, 7), (0.1, 3, 14)),
                           ((0, 1.5, -0.05), (8, 3, 0.1)), ((0, 1.5, 14.05), (8, 3, 0.1))], "concrete")
    B.mesh(c, r, "EntranceDoor", [((-0.8, 1.05, 0.03), (1.0, 2.1, 0.06))], "metal")            # O-01
    B.mesh(c, r, "SideShield_Left", [((-1.6, 1.5, 9.0), (0.1, 3, 8.8))], "metal")             # O-33
    B.mesh(c, r, "SideShield_Right", [((1.6, 1.5, 9.0), (0.1, 3, 8.8))], "metal")             # O-34
    B.mesh(c, r, "BulletTrap", [((0, 1.5, 13.7), (3, 3, 0.6))], "metal")                      # O-35
    B.mesh(c, r, "CeilingBaffles", [((0, 2.85, z), (3.1, 0.3, 0.1)) for z in (7.0, 9.5, 12.0)], "metal")  # O-37

    # ---- Оружейный шкаф O-03: лицом к +Z, ширина 1,0, глубина 0,6, высота 1,9
    c = B.collection("Cabinet")
    r = B.root(c, "Cabinet")
    W, D, H = 1.0, 0.6, 1.9
    B.mesh(c, r, "Body", [((0, H / 2, -D / 2 + 0.01), (W, H, 0.02)),            # задняя стенка
                          ((-W / 2 + 0.01, H / 2, 0), (0.02, H, D)), ((W / 2 - 0.01, H / 2, 0), (0.02, H, D)),
                          ((0, H - 0.01, 0), (W, 0.02, D)), ((0, 0.05, 0), (W, 0.1, D)),
                          ((0, 1.2, -0.02), (W - 0.04, 0.02, D - 0.06))], "metal")   # полка с гнездом
    B.mesh(c, r, "InspectionShelf", [((0, 0.98, D / 2 + 0.15), (0.9, 0.04, 0.3))], "metal")     # O-07, верх 1,0
    # Дверца O-04: начало координат на петле — правый передний край шкафа (в зале это край у входа,
    # Z 1,0), поворот вокруг локальной Y. Открыта на +100°: не закрывает полку СИЗ от A1.
    B.mesh(c, r, "Door", [((W / 2 - 0.45, 1.35, D / 2 + 0.01), (0.9, 1.1, 0.02))], "metal", pivot=(W / 2, 1.35, D / 2 + 0.01))
    # WeaponSlot O-05: пистолет ложится стволом к мишеням. Шкаф в зале развёрнут лицом к +X
    # (поворот Unity Y = 90°), мишени по +Z зала = −X шкафа, поэтому гнездо повёрнуто на −90°.
    B.empty(c, r, "WeaponSlot", (0, 1.3, 0.0), yaw_unity=-90)

    # ---- Полка СИЗ O-09: на стене, ширина 0,7, глубина 0,4, верх 1,22
    c = B.collection("PpeShelf")
    r = B.root(c, "PpeShelf")
    B.mesh(c, r, "Shelf", [((0, 1.2, 0), (0.7, 0.04, 0.4)),
                           ((-0.3, 1.1, -0.15), (0.03, 0.16, 0.1)), ((0.3, 1.1, -0.15), (0.03, 0.16, 0.1))], "metal")
    # В зале полка развёрнута лицом к +X: локальная +X полки = −Z зала.
    B.empty(c, r, "Slot_Earmuffs", (0.18, 1.3, 0.0))     # O-10
    B.empty(c, r, "Slot_Glasses", (-0.2, 1.26, 0.0))     # O-12

    # ---- Скамья O-13
    c = B.collection("Bench")
    r = B.root(c, "Bench")
    B.mesh(c, r, "Bench", [((0, 0.43, 0), (0.6, 0.04, 0.4)), ((-0.25, 0.21, 0), (0.05, 0.42, 0.35)),
                           ((0.25, 0.21, 0), (0.05, 0.42, 0.35))], "metal")

    # ---- Стол снаряжения O-17: ширина 1,6, глубина 0,8, верх 0,9
    c = B.collection("PrepTable")
    r = B.root(c, "PrepTable")
    B.mesh(c, r, "Table", [((0, 0.88, 0), (1.6, 0.04, 0.8))] +
           [((x, 0.43, z), (0.05, 0.86, 0.05)) for x in (-0.75, 0.75) for z in (-0.35, 0.35)], "metal")

    # ---- Лоток с патронами O-18
    c = B.collection("AmmoTray")
    r = B.root(c, "AmmoTray")
    B.mesh(c, r, "Tray", [((0, 0.005, 0), (0.4, 0.01, 0.3)), ((0, 0.03, -0.145), (0.4, 0.05, 0.01)),
                          ((0, 0.03, 0.145), (0.4, 0.05, 0.01)), ((-0.195, 0.03, 0), (0.01, 0.05, 0.3)),
                          ((0.195, 0.03, 0), (0.01, 0.05, 0.3))], "metal")
    B.empty(c, r, "Spawn", (0, 0.02, 0), size=0.03)

    # ---- Стойка рубежа O-24 с гильзоуловителем O-22 и полочкой гильз O-21.
    # Лицом к стрелку (+Z локально); в зале развёрнута на 180°, поэтому правая для стрелка
    # сторона зала (+X) — это −X стойки.
    c = B.collection("LineStand")
    r = B.root(c, "LineStand")
    B.mesh(c, r, "Stand", [((0, 0.98, 0), (1.4, 0.04, 0.5)), ((0, 0.48, -0.2), (1.3, 0.96, 0.04)),
                           ((-0.65, 0.48, 0), (0.04, 0.96, 0.45)), ((0.65, 0.48, 0), (0.04, 0.96, 0.45))], "concrete")
    B.mesh(c, r, "CasingCatcher", [((-0.95, 0.4, 0), (0.4, 0.8, 0.5))], "metal")
    B.mesh(c, r, "CasingShelf", [((-0.6, 1.01, 0.17), (0.15, 0.02, 0.1))], "metal")
    B.empty(c, r, "CatcherZone", (-0.95, 0.85, 0))

    # ---- Пульт подачи мишени O-25: кнопка и регулятор отдельно
    c = B.collection("TargetRemote")
    r = B.root(c, "TargetRemote")
    B.mesh(c, r, "Housing", [((0, 0.03, 0), (0.2, 0.06, 0.15))], "metal")
    B.mesh(c, r, "Button", [((0.05, 0.07, 0.02), (0.04, 0.02, 0.04))], "yellow", pivot=(0.05, 0.06, 0.02))
    B.mesh(c, r, "Dial", [((-0.05, 0.07, 0.02), (0.05, 0.02, 0.05))], "paper", pivot=(-0.05, 0.06, 0.02))

    bpy.context.view_layer.update()
    return B
