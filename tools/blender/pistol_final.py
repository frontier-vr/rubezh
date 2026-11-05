"""MM-07: финальная геометрия пистолета поверх блок-модели (Pistol.blend).

Объекты, пустышки, имена и начала координат блок-модели сохраняются: меняются только меши.
Координаты Blender: ствол смотрит в −Y, верх +Z, левый бок пистолета +X (там предохранитель).
Запуск в открытом Pistol.blend: exec(open(r"...\\tools\\blender\\pistol_final.py").read()).
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

POLYMER = ('MM_Pistol_Polymer', '#1E2124', 0.75)
STEEL = ('MM_Pistol_Steel', '#5C636B', 0.35)
CONTROL = ('MM_Pistol_Control', '#C8691E', 0.55)


def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0])


def material(spec):
    name, color, rough = spec
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = srgb(color)
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = 0.8 if spec is STEEL else 0.0
    m.diffuse_color = srgb(color)
    return m


# ---------- примитивы bmesh в локальных координатах детали ----------

def prism_x(bm, profile_yz, x0, x1):
    """Профиль в плоскости YZ (обход против часовой, если смотреть с +X), вытянутый по X."""
    a = [bm.verts.new((x0, y, z)) for y, z in profile_yz]
    b = [bm.verts.new((x1, y, z)) for y, z in profile_yz]
    n = len(profile_yz)
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))


def prism_y(bm, profile_xz, y0, y1):
    """Сечение в плоскости XZ, вытянутое по Y (вдоль ствола)."""
    a = [bm.verts.new((x, y0, z)) for x, z in profile_xz]
    b = [bm.verts.new((x, y1, z)) for x, z in profile_xz]
    n = len(profile_xz)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[j], a[i], b[i], b[j]))


def box(bm, center, size):
    res = bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*size, 1)), verts=res['verts'])


def cyl(bm, center, radius, depth, axis, segments=16):
    res = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=depth)
    rot = {'X': Matrix.Rotation(math.radians(90), 4, 'Y'),
           'Y': Matrix.Rotation(math.radians(90), 4, 'X'),
           'Z': Matrix.Identity(4)}[axis]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ rot, verts=res['verts'])


def sweep(bm, path_yz, half_x, half_t):
    """Лента вдоль ломаной в плоскости YZ: ширина 2*half_x по X, толщина 2*half_t поперёк пути."""
    rings = []
    for i, (y, z) in enumerate(path_yz):
        a = path_yz[max(i - 1, 0)]
        b = path_yz[min(i + 1, len(path_yz) - 1)]
        dy, dz = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dy, dz) or 1.0
        ny, nz = -dz / ln, dy / ln
        ring = [bm.verts.new((sx * half_x, y + st * half_t * ny, z + st * half_t * nz))
                for sx, st in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(4):
            l = (k + 1) % 4
            bm.faces.new((r0[k], r0[l], r1[l], r1[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])


# ---------- сборка меша с булевыми вырезами и фаской ----------

def build_mesh(ob, bm, mat, cutters=(), bevel=0.0):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(ob.name + '_tmp')
    bm.to_mesh(me)
    bm.free()
    tmp = bpy.data.objects.new(ob.name + '_tmp', me)
    bpy.context.scene.collection.objects.link(tmp)
    temp_cutters = []
    for i, cbm in enumerate(cutters):
        cme = bpy.data.meshes.new('cut')
        bmesh.ops.recalc_face_normals(cbm, faces=cbm.faces)
        cbm.to_mesh(cme)
        cbm.free()
        cob = bpy.data.objects.new('cut', cme)
        bpy.context.scene.collection.objects.link(cob)
        temp_cutters.append(cob)
        mod = tmp.modifiers.new('cut%d' % i, 'BOOLEAN')
        mod.operation = 'DIFFERENCE'
        mod.solver = 'EXACT'
        mod.object = cob
    if bevel > 0:
        bv = tmp.modifiers.new('bevel', 'BEVEL')
        bv.width = bevel
        bv.segments = 1
        bv.limit_method = 'ANGLE'
        bv.angle_limit = math.radians(40)
    deps = bpy.context.evaluated_depsgraph_get()
    new_me = bpy.data.meshes.new_from_object(tmp.evaluated_get(deps))
    for cob in temp_cutters:
        bpy.data.objects.remove(cob)
    bpy.data.objects.remove(tmp)
    bpy.data.meshes.remove(me)
    tbm = bmesh.new()
    tbm.from_mesh(new_me)
    # MM-14 (#169, #170): после булевой операции — сварить микротреугольники и развернуть нормали
    # наружу, иначе пересекающиеся куски (казённик в стволе) выходят вывернутыми.
    bmesh.ops.remove_doubles(tbm, verts=tbm.verts[:], dist=2e-5)
    bmesh.ops.dissolve_degenerate(tbm, dist=2e-5, edges=tbm.edges[:])
    bmesh.ops.triangulate(tbm, faces=tbm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    bmesh.ops.recalc_face_normals(tbm, faces=tbm.faces)
    tbm.to_mesh(new_me)
    tbm.free()
    new_me.materials.clear()
    new_me.materials.append(mat)
    for p in new_me.polygons:
        p.use_smooth = False
    old = ob.data
    ob.data = new_me
    if old.users == 0:
        bpy.data.meshes.remove(old)
    new_me.name = ob.name
    return new_me


def uv_unwrap(ob):
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')


def triangles(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


# ---------- детали ----------

def frame_mesh():
    bm = bmesh.new()
    outline = [  # (y, z) профиль рамки сбоку, обход против часовой с +X
        (-0.1375, 0.017), (-0.1375, 0.002), (-0.074, 0.0), (-0.069, -0.012), (-0.066, -0.028),
        (-0.058, -0.035), (-0.030, -0.035), (-0.022, -0.030), (-0.020, -0.020),
        (-0.017, -0.036), (-0.012, -0.046), (-0.012, -0.054), (-0.007, -0.064), (-0.006, -0.072),
        (-0.001, -0.082), (0.002, -0.092), (0.004, -0.097), (0.050, -0.097), (0.053, -0.092),
        (0.026, -0.006), (0.040, 0.004), (0.043, 0.011), (0.030, 0.017),
    ]
    prism_x(bm, list(reversed(outline)), -0.014, 0.014)
    # направляющая под фонарь — три поперечных паза снизу пылезащитной крышки
    cutters = []
    hole = bmesh.new()
    guard_in = [(-0.0215, -0.004), (-0.0215, -0.024), (-0.029, -0.029), (-0.056, -0.029), (-0.062, -0.023), (-0.0625, -0.006)]
    prism_x(hole, list(reversed(guard_in)), -0.02, 0.02)
    cutters.append(hole)
    for i in range(3):
        c = bmesh.new()
        box(c, Vector((0, -0.125 + i * 0.012, 0.0)), (0.04, 0.004, 0.004))
        cutters.append(c)
    # насечка рукояти — неглубокие продольные канавки на передней и задней поверхности
    for i in range(5):
        c = bmesh.new()
        z = -0.032 - i * 0.013
        box(c, Vector((0.0145, 0.022 + (z + 0.006) * -0.32, z)), (0.003, 0.03, 0.005))
        cutters.append(c)
        c = bmesh.new()
        box(c, Vector((-0.0145, 0.022 + (z + 0.006) * -0.32, z)), (0.003, 0.03, 0.005))
        cutters.append(c)
    return bm, cutters


def slide_mesh():
    bm = bmesh.new()
    section = [(-0.013, -0.015), (0.013, -0.015), (0.013, 0.007), (0.009, 0.015), (-0.009, 0.015), (-0.013, 0.007)]
    prism_y(bm, section, -0.095, 0.095)
    # скос носа затвора
    for v in bm.verts:
        if v.co.y < -0.094 and v.co.z > 0.0:
            v.co.y += 0.006
    box(bm, Vector((0, -0.086, 0.0175)), (0.003, 0.005, 0.005))          # мушка
    box(bm, Vector((0.0045, 0.088, 0.0175)), (0.004, 0.006, 0.005))      # целик, две половинки с прорезью
    box(bm, Vector((-0.0045, 0.088, 0.0175)), (0.004, 0.006, 0.005))
    cutters = []
    port = bmesh.new()
    box(port, Vector((-0.011, 0.025, 0.011)), (0.014, 0.03, 0.014))       # окно выброса справа сверху
    cutters.append(port)
    bore = bmesh.new()
    cyl(bore, Vector((0, -0.095, 0.0)), 0.0068, 0.02, 'Y')               # отверстие под ствол в носу
    cutters.append(bore)
    for i in range(8):                                                   # насечки сзади, обе стороны
        for sx in (-1, 1):
            c = bmesh.new()
            box(c, Vector((sx * 0.0135, 0.058 + i * 0.0045, -0.002)), (0.002, 0.0018, 0.02))
            cutters.append(c)
    return bm, cutters


def barrel_mesh():
    bm = bmesh.new()
    cyl(bm, Vector((0, -0.0045, 0)), 0.0065, 0.105, 'Y', segments=16)   # срез ствола на 1 мм впереди затвора
    box(bm, Vector((0, 0.041, 0.0005)), (0.0125, 0.022, 0.0135))         # казённик, виден в окне выброса
    cutters = []
    bore = bmesh.new()
    cyl(bore, Vector((0, -0.06, 0)), 0.0045, 0.012, 'Y', segments=12)
    cutters.append(bore)
    return bm, cutters


def trigger_mesh():
    bm = bmesh.new()
    # Лезвие изогнуто вогнутой стороной вперёд (к −Y, дульному срезу): палец ложится в изгиб,
    # кончик загнут вперёд (силуэт, 22.10 — до этого изгиб был зеркальным).
    path = [(0.0, 0.0), (0.0025, -0.006), (0.0035, -0.012), (0.0025, -0.018), (-0.001, -0.0235)]
    sweep(bm, path, 0.0028, 0.0022)
    cyl(bm, Vector((0, 0, 0)), 0.0022, 0.007, 'X', segments=8)           # ось
    return bm, []


def safety_mesh():
    bm = bmesh.new()
    lever = [(0.003, 0.003), (-0.009, 0.0035), (-0.0155, 0.001), (-0.0155, -0.002), (-0.009, -0.0035), (0.003, -0.003)]
    prism_x(bm, list(reversed(lever)), -0.0012, 0.0014)
    cyl(bm, Vector((0, 0, 0)), 0.0034, 0.0032, 'X', segments=12)         # головка оси
    return bm, []


def magrelease_mesh():
    bm = bmesh.new()
    cyl(bm, Vector((0, 0, 0)), 0.0042, 0.004, 'X', segments=12)
    return bm, []


def slidestop_mesh():
    bm = bmesh.new()
    lever = [(0.001, 0.0025), (-0.022, 0.0025), (-0.029, 0.0005), (-0.029, -0.003), (-0.024, -0.003), (0.001, -0.0025)]
    prism_x(bm, list(reversed(lever)), -0.0012, 0.0013)
    box(bm, Vector((0.0004, -0.026, -0.002)), (0.0022, 0.006, 0.003))    # площадка под палец
    cyl(bm, Vector((0, 0, 0)), 0.0026, 0.0028, 'X', segments=10)
    return bm, []


PARTS = {
    'Frame': (frame_mesh, POLYMER, 0.0012),
    'Slide': (slide_mesh, STEEL, 0.0008),
    'Barrel': (barrel_mesh, STEEL, 0.0),
    'Trigger': (trigger_mesh, CONTROL, 0.0),
    'SafetyLever': (safety_mesh, CONTROL, 0.0),
    'MagRelease': (magrelease_mesh, CONTROL, 0.0),
    'SlideStop': (slidestop_mesh, CONTROL, 0.0),
}


def build():
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    tri = {}
    for name, (fn, mat_spec, bevel) in PARTS.items():
        ob = bpy.data.objects[name]
        bm, cutters = fn()
        build_mesh(ob, bm, material(mat_spec), cutters, bevel)
        uv_unwrap(ob)
        tri[name] = triangles(ob)
    for m in list(bpy.data.materials):
        if m.name.startswith('MM_Blockout') and m.users == 0:
            bpy.data.materials.remove(m)
    bpy.ops.wm.save_mainfile()
    tri['total'] = sum(tri.values())
    return tri


result = build()
