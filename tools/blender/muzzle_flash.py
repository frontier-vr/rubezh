"""MM-17: вспышка выстрела — непрозрачный меш из звезды и скрещённых «языков пламени».

Начало координат — на срезе ствола (пустышка Muzzle), вспышка вытянута вдоль −Y Blender (в Unity +Z).
Без прозрачности (ТЗ §7.3): материал Unlit Opaque, обе стороны рисуются (Render Face: Both).
Запуск: exec(open(r"...\\tools\\blender\\muzzle_flash.py").read()).
"""
import math
import bmesh
import bpy
from mathutils import Vector

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~\MuzzleFlash.blend"
OUT = r"B:\Project-1\Assets\_Rubezh\Art\Weapon\MuzzleFlash.fbx"


def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0])


def build():
    bpy.ops.wm.read_homefile(use_empty=True)
    scene = bpy.context.scene
    coll = bpy.data.collections.new('MuzzleFlash')
    scene.collection.children.link(coll)
    mat = bpy.data.materials.new('MM_MuzzleFlash')
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = srgb('#FFE9A8')
    mat.diffuse_color = srgb('#FFE9A8')

    bm = bmesh.new()
    # звезда на 6 лучей поперёк ствола, чуть впереди среза
    rays, r_out, r_in, y0 = 6, 0.035, 0.011, -0.004
    center = bm.verts.new((0, y0, 0))
    ring = []
    for i in range(rays * 2):
        a = math.pi * i / rays
        r = r_out if i % 2 == 0 else r_in
        ring.append(bm.verts.new((r * math.cos(a), y0, r * math.sin(a))))
    for i in range(len(ring)):
        bm.faces.new((center, ring[i], ring[(i + 1) % len(ring)]))
    # три скрещённых «языка» вдоль оси ствола
    profile = [(0.0, 0.0), (0.013, -0.018), (0.007, -0.055), (0.0, -0.085), (-0.007, -0.055), (-0.013, -0.018)]
    for k in range(3):
        a = math.pi * k / 3
        ca, sa = math.cos(a), math.sin(a)
        vs = [bm.verts.new((w * ca, y, w * sa)) for w, y in profile]
        bm.faces.new(vs)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    me = bpy.data.meshes.new('MuzzleFlash')
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new('MuzzleFlash', me)
    coll.objects.link(ob)
    bpy.ops.wm.save_as_mainfile(filepath=SRC)
    return sum(len(p.vertices) - 2 for p in me.polygons)


tri = build()
exec(open(r"B:\Project-1\tools\blender\rubezh_fbx.py", encoding="utf-8").read())
result = {"tri": tri, "exported": export_collection('MuzzleFlash', OUT)}
