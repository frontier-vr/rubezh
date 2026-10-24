"""MM-08: финальный магазин поверх блок-модели (Magazine.blend).

Корпус Body и подаватель Follower заменяются, пустышки Round_01…Round_05 и начала координат
остаются. Координаты Blender: перёд −Y, верх +Z; губки магазина в z = 0, корпус вниз до −0,114.
Окна контроля — сквозные отверстия на уровне пустышек: в них видны патроны (MagazineItem).
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

exec(open(r"B:\Project-1\tools\blender\pistol_final.py", encoding="utf-8").read().split("PARTS = {")[0])

FOLLOWER = ('MM_Magazine_Follower', '#C8691E', 0.55)
BODY = ('MM_Pistol_Polymer', '#1E2124', 0.75)


def section(hx, y0, y1, c):
    """Скруглённый прямоугольник сечения корпуса в плоскости XY (фаска c)."""
    return [(-hx + c, y0), (hx - c, y0), (hx, y0 + c), (hx, y1 - c), (hx - c, y1), (-hx + c, y1), (-hx, y1 - c), (-hx, y0 + c)]


def prism_z(bm, profile_xy, z0, z1):
    a = [bm.verts.new((x, y, z0)) for x, y in profile_xy]
    b = [bm.verts.new((x, y, z1)) for x, y in profile_xy]
    n = len(profile_xy)
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))


def body_mesh():
    bm = bmesh.new()
    prism_z(bm, section(0.0095, -0.018, 0.020, 0.003), -0.106, 0.0)          # корпус
    prism_z(bm, section(0.011, -0.021, 0.023, 0.003), -0.114, -0.106)        # пятка
    cutters = []
    lips = bmesh.new()                                                        # губки: срез сверху спереди
    prism_x(lips, [(-0.03, 0.01), (-0.03, -0.004), (-0.012, -0.004), (0.0, 0.01)], -0.02, 0.02)
    cutters.append(lips)
    groove = bmesh.new()                                                      # проход патрона между губками
    box(groove, Vector((0, 0.002, 0.0)), (0.0062, 0.03, 0.004))
    cutters.append(groove)
    for k in range(5):                                                        # окна контроля на уровне Round_0k
        hole = bmesh.new()
        cyl(hole, Vector((0, 0.006, -0.006 - 0.0099 * k)), 0.0028, 0.03, 'X', segments=8)
        cutters.append(hole)
    return bm, cutters


def follower_mesh():
    bm = bmesh.new()
    box(bm, Vector((0, 0.003, 0.0)), (0.011, 0.023, 0.004))                  # площадка
    box(bm, Vector((0, -0.010, 0.001)), (0.011, 0.006, 0.006))               # передний выступ под патрон
    return bm, []


def build():
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    tri = {}
    for name, fn, spec, bevel in (('Body', body_mesh, BODY, 0.0), ('Follower', follower_mesh, FOLLOWER, 0.0)):
        ob = bpy.data.objects[name]
        bm, cutters = fn()
        build_mesh(ob, bm, material(spec), cutters, bevel)
        uv_unwrap(ob)
        tri[name] = triangles(ob)
    for m in list(bpy.data.materials):
        if m.name.startswith('MM_Blockout') and m.users == 0:
            bpy.data.materials.remove(m)
    bpy.ops.wm.save_mainfile()
    tri['total'] = tri['Body'] + tri['Follower']
    return tri


result = build()
