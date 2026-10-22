"""VP-17: призраки повтора эпизода нарушения — голова со шлемом VR и кисти рук.

Модели лицом к −Y (Front), метры, корень-пустышка в начале координат (docs/art-pipeline.md).
Только квады и треугольники: крышки цилиндров веером треугольников, н-гонов нет; нормали наружу
(после построения пересчитываются и проверяются). Запуск в Blender 5.1 (из MCP или Scripting):
    exec(open(r"B:/Project-1/tools/blender/replay_ghost.py", encoding="utf-8").read())
    build_and_export()
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~"
OUT = r"B:\Project-1\Assets\_Rubezh\Art\Replay\ReplayGhost.fbx"
GHOST = "#B9A8F0"     # призрак повтора (docs/palette.md)
VISOR = "#1D2228"     # графит: лицевая панель шлема, чтобы читалось направление взгляда


def hex_rgba(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*lin, 1.0)


def material(name, hex_color, rough=0.7):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = hex_rgba(hex_color)
    bsdf.inputs['Roughness'].default_value = rough
    m.diffuse_color = hex_rgba(hex_color)
    return m


def quad_ellipsoid(bm, center, radii, cuts=3, squash_front=0.0):
    """Квад-сфера: куб с подразбиением, вершины на эллипсоиде. Только квады."""
    bm.verts.ensure_lookup_table()
    first = len(bm.verts)
    res = bmesh.ops.create_cube(bm, size=2.0)
    edges = list({e for v in res['verts'] for e in v.link_edges})
    bmesh.ops.subdivide_edges(bm, edges=edges, cuts=cuts, use_grid_fill=True)
    bm.verts.ensure_lookup_table()
    allv = list(bm.verts[first:])
    for v in allv:
        d = v.co.normalized()
        if squash_front and d.y < 0:
            d.y *= (1.0 - squash_front)
        v.co = center + Vector((d.x * radii[0], d.y * radii[1], d.z * radii[2]))
    return allv


def rounded_box(bm, center, size, bevel):
    """Коробка со скруглёнными рёбрами: квады на рёбрах, треугольники в углах."""
    res = bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*size, 1)), verts=res['verts'])
    edges = list({e for v in res['verts'] for e in v.link_edges})
    bmesh.ops.bevel(bm, geom=res['verts'] + edges, offset=bevel, segments=1, affect='EDGES', profile=0.5)


def capsule(bm, start, end, radius, segments=8):
    """Фаланга: цилиндр с крышками-веерами треугольников (без н-гонов) и скруглённым кончиком."""
    axis = end - start
    length = axis.length
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=segments,
                                radius1=radius, radius2=radius * 0.85, depth=length)
    rot = Vector((0, 0, 1)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
    mid = (start + end) * 0.5
    bmesh.ops.transform(bm, matrix=Matrix.Translation(mid) @ rot, verts=res['verts'])


def band(bm, center, rx, ry, height, thick, segments, start_deg, end_deg):
    """Ремешок шлема: дуга-лента в плоскости XY (вокруг головы), сечение — прямоугольник, квады."""
    rings = []
    n = segments
    for i in range(n + 1):
        a = math.radians(start_deg + (end_deg - start_deg) * i / n)
        ca, sa = math.cos(a), math.sin(a)
        ring = []
        for (dr, dz) in ((0, -height / 2), (thick, -height / 2), (thick, height / 2), (0, height / 2)):
            ring.append(bm.verts.new(center + Vector(((rx + dr) * ca, (ry + dr) * sa, dz))))
        rings.append(ring)
    for i in range(n):
        a, b = rings[i], rings[i + 1]
        for k in range(4):
            bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
    for ring in (rings[0], rings[-1]):
        bm.faces.new(ring)    # торцы — квады


def finish(name, bm, mats, coll, parent, material_of_face=None):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    if material_of_face:
        for f in bm.faces:
            f.material_index = material_of_face(f)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    ob.parent = parent
    return ob


def build_head(coll, root, ghost, visor):
    bm = bmesh.new()
    # Череп: ширина 0,16, глубина 0,2, высота 0,23; начало координат — точка между глаз (камера HMD).
    quad_ellipsoid(bm, Vector((0, 0.07, 0.02)), (0.08, 0.10, 0.115), cuts=3)
    # Шея: короткий цилиндр под черепом.
    capsule(bm, Vector((0, 0.08, -0.17)), Vector((0, 0.08, -0.07)), 0.05, segments=10)
    # Шлем VR перед глазами: корпус-коробка лицом к −Y.
    rounded_box(bm, Vector((0, -0.045, 0.0)), (0.19, 0.09, 0.11), 0.018)
    # Ремешок вокруг затылка.
    band(bm, Vector((0, 0.03, 0.01)), 0.088, 0.105, 0.035, 0.01, 12, 0, 180)
    visor_y = -0.088

    def mat_of(f):
        c = f.calc_center_median()
        return 1 if (c.y < visor_y + 0.004 and f.normal.y < -0.7) else 0
    return finish("Ghost_Head", bm, [ghost, visor], coll, root, mat_of)


def build_hand(coll, root, ghost, side):
    """Кисть, держащая контроллер: ладонь, сжатые три пальца, указательный и большой.
    side = 1 — правая (большой палец слева от стрелка, +X Blender), −1 — левая (зеркально)."""
    bm = bmesh.new()
    rounded_box(bm, Vector((0, 0.01, 0)), (0.085, 0.10, 0.032), 0.012)           # ладонь
    rounded_box(bm, Vector((-0.004 * side, -0.035, -0.022)), (0.07, 0.035, 0.04), 0.012)  # сжатые пальцы
    capsule(bm, Vector((0.03 * side, -0.035, 0.0)), Vector((0.03 * side, -0.085, -0.005)), 0.0095)  # указательный
    capsule(bm, Vector((0.045 * side, 0.015, 0.0)), Vector((0.05 * side, -0.035, 0.012)), 0.011)    # большой
    # Запястье.
    capsule(bm, Vector((0, 0.055, 0)), Vector((0, 0.11, 0)), 0.026, segments=10)
    name = "Ghost_Hand_R" if side > 0 else "Ghost_Hand_L"
    return finish(name, bm, [ghost], coll, root)


def build():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0
    coll = bpy.data.collections.new("ReplayGhost")
    bpy.context.scene.collection.children.link(coll)
    root = bpy.data.objects.new("ReplayGhost", None)
    root.empty_display_size = 0.05
    coll.objects.link(root)
    ghost = material("MM_Ghost", GHOST)
    visor = material("MM_GhostVisor", VISOR, rough=0.4)
    head = build_head(coll, root, ghost, visor)
    hand_r = build_hand(coll, root, ghost, 1)
    hand_l = build_hand(coll, root, ghost, -1)
    # Раскладка для просмотра в .blend; в Unity каждая деталь — отдельный призрак, положение задаёт запись.
    head.location = (0, 0, 0.35)
    hand_l.location = (0.15, 0, 0)
    hand_r.location = (-0.15, 0, 0)
    return coll


def check(coll):
    """Н-гоны, перевёрнутые нормали, несвязная геометрия — для отчёта в PR."""
    out = {}
    for ob in coll.all_objects:
        if ob.type != 'MESH':
            continue
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        ngons = sum(1 for f in bm.faces if len(f.verts) > 4)
        before = [f.normal.copy() for f in bm.faces]
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        flipped = sum(1 for f, n in zip(bm.faces, before) if f.normal.dot(n) < 0)
        tris = sum(len(f.verts) - 2 for f in bm.faces)
        bm.free()
        out[ob.name] = dict(tris=tris, ngons=ngons, flipped=flipped)
    return out


def build_and_export():
    import os
    coll = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC, "ReplayGhost.blend"))
    exec(open(r"B:/Project-1/tools/blender/rubezh_fbx.py", encoding="utf-8").read(), globals())
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    # В FBX детали стоят в начале координат: раскладка только для просмотра.
    saved = {ob.name: tuple(ob.location) for ob in coll.all_objects}
    for ob in coll.all_objects:
        if ob.parent:
            ob.location = (0, 0, 0)
    tris = export_collection("ReplayGhost", OUT)
    for ob in coll.all_objects:
        ob.location = saved[ob.name]
    return dict(tris=tris, check=check(coll))
