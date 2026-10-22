"""MM-11: наушники и очки для Рубежа. Модель лицом к −Y (Front), метры, пустышка HeadAnchor."""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~"


def hex_rgba(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*lin, 1.0)


def material(name, hex_color, rough=0.6):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = hex_rgba(hex_color)
    bsdf.inputs['Roughness'].default_value = rough
    m.diffuse_color = hex_rgba(hex_color)
    return m


def new_object(name, bm, mat, coll, parent=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    if parent:
        ob.parent = parent
    return ob


def cylinder_x(bm, center, radius, depth, segments, scale_z=1.0):
    """Цилиндр вдоль оси X."""
    # Крышки веером треугольников: без н-гонов (MM-14, #171).
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=segments, radius1=radius, radius2=radius, depth=depth)
    rot = Matrix.Rotation(math.radians(90), 4, 'Y')
    sc = Matrix.Diagonal((1, 1, scale_z, 1))
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ sc @ rot, verts=res['verts'])


def box(bm, center, size):
    res = bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*size, 1)), verts=res['verts'])


def ring_y(bm, center, rx, rz, width, depth, segments):
    """Плоское кольцо-оправа в плоскости XZ, толщина depth по Y."""
    loops = {}
    for side, y in (('f', -depth / 2), ('b', depth / 2)):
        o, n = [], []
        for i in range(segments):
            a = 2 * math.pi * i / segments
            ca, sa = math.cos(a), math.sin(a)
            o.append(bm.verts.new(center + Vector(((rx + width) * ca, y, (rz + width) * sa))))
            n.append(bm.verts.new(center + Vector((rx * ca, y, rz * sa))))
        loops[side] = (o, n)
    fo, fi = loops['f']
    bo, bi = loops['b']
    for i in range(segments):
        j = (i + 1) % segments
        bm.faces.new((fo[i], fo[j], fi[j], fi[i]))          # перёд
        bm.faces.new((bo[i], bi[i], bi[j], bo[j]))          # зад
        bm.faces.new((fo[i], bo[i], bo[j], fo[j]))          # наружная кромка
        bm.faces.new((fi[i], fi[j], bi[j], bi[i]))          # внутренняя кромка


def lens_y(bm, center, rx, rz, depth, segments):
    res = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=1.0, radius2=1.0, depth=depth)
    rot = Matrix.Rotation(math.radians(90), 4, 'X')
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((rx, 1, rz, 1)) @ rot, verts=res['verts'])


def new_file():
    bpy.ops.wm.read_homefile(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    return scene


def build_earmuffs():
    scene = new_file()
    body = material('PPE_Earmuffs', '#2B2F33', 0.7)
    cushion = material('PPE_Cushion', '#1E2124', 0.9)

    # --- Наушники: начало в центре головы между ушами, чашки по оси X ---
    ce = bpy.data.collections.new('Earmuffs')
    scene.collection.children.link(ce)
    root_e = bpy.data.objects.new('Earmuffs', None)
    ce.objects.link(root_e)
    anchor_e = bpy.data.objects.new('HeadAnchor', None)
    anchor_e.empty_display_size = 0.03
    ce.objects.link(anchor_e)
    anchor_e.parent = root_e

    bm = bmesh.new()
    for s in (-1, 1):
        cylinder_x(bm, Vector((s * 0.088, 0, 0)), 0.042, 0.032, 14, scale_z=1.15)   # чашка
    seg = 16
    r_in, r_out, half_w = 0.094, 0.102, 0.013
    verts = []
    for i in range(seg + 1):
        a = math.pi * i / seg
        ring = []
        for r, y in ((r_in, -half_w), (r_out, -half_w), (r_out, half_w), (r_in, half_w)):
            ring.append(bm.verts.new((r * math.cos(a), y, 0.035 + r * math.sin(a))))
        verts.append(ring)
    for i in range(seg):
        a, b = verts[i], verts[i + 1]
        for k in range(4):
            l = (k + 1) % 4
            bm.faces.new((a[k], a[l], b[l], b[k]))
    for end in (verts[0], verts[-1]):
        bm.faces.new(end if end is verts[-1] else list(reversed(end)))
    for s in (-1, 1):
        box(bm, Vector((s * 0.098, 0, 0.042)), (0.008, 0.02, 0.03))                 # вилка чашки
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    shell = new_object('Earmuffs_Shell', bm, body, ce, root_e)

    bm = bmesh.new()
    for s in (-1, 1):
        cylinder_x(bm, Vector((s * 0.067, 0, 0)), 0.036, 0.012, 14, scale_z=1.15)   # амбушюры к голове
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    pads = new_object('Earmuffs_Cushions', bm, cushion, ce, root_e)
    bpy.ops.wm.save_as_mainfile(filepath=SRC + r"\Earmuffs.blend")
    return [shell, pads]


def curve(x):
    """Изгиб линзы: к краям уходит назад (+Y)."""
    return 9.0 * x * x


def curved_strip(bm, x0, x1, segments, z0, z1, thickness, y_offset):
    """Гнутая полоса по кривой curve(x) от z0 до z1, толщина по Y."""
    cols = []
    for i in range(segments + 1):
        x = x0 + (x1 - x0) * i / segments
        y = curve(x) + y_offset
        cols.append([bm.verts.new((x, y + dy, z)) for dy, z in ((0, z0), (0, z1), (thickness, z1), (thickness, z0))])
    for c0, c1 in zip(cols, cols[1:]):
        for k in range(4):
            l = (k + 1) % 4
            bm.faces.new((c0[k], c1[k], c1[l], c0[l]))
    bm.faces.new(cols[0])
    bm.faces.new(list(reversed(cols[-1])))


def build_glasses():
    scene = new_file()
    frame_m = material('PPE_GlassesFrame', '#23272B', 0.5)
    lens_m = material('PPE_GlassesLens', '#6E7E86', 0.2)

    # --- Очки: начало в середине переносицы в плоскости стёкол, дужки назад (+Y) ---
    cg = bpy.data.collections.new('Glasses')
    scene.collection.children.link(cg)
    root_g = bpy.data.objects.new('Glasses', None)
    cg.objects.link(root_g)
    anchor_g = bpy.data.objects.new('HeadAnchor', None)
    anchor_g.empty_display_size = 0.02
    cg.objects.link(anchor_g)
    anchor_g.parent = root_g

    # Стрелковые очки: цельная гнутая линза с боковой защитой (docs/refs/weapon.md).
    bm = bmesh.new()
    curved_strip(bm, -0.068, 0.068, 12, 0.0185, 0.025, 0.004, 0.0035)             # верхняя планка оправы
    for s in (-1, 1):
        x = s * 0.068
        y = curve(x)
        box(bm, Vector((s * 0.0695, y + 0.012, -0.002)), (0.003, 0.026, 0.042))    # боковой щиток
        box(bm, Vector((s * 0.0695, y + 0.085, 0.017)), (0.004, 0.12, 0.006))      # дужки
        box(bm, Vector((s * 0.0695, y + 0.15, 0.004)), (0.004, 0.018, 0.024))      # заушники
    box(bm, Vector((0, 0.008, -0.012)), (0.014, 0.008, 0.006))                      # носоупор за линзой
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    frame = new_object('Glasses_Frame', bm, frame_m, cg, root_g)

    bm = bmesh.new()
    curved_strip(bm, -0.068, 0.068, 12, -0.024, 0.0185, 0.002, 0.0)               # цельная линза
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    lenses = new_object('Glasses_Lenses', bm, lens_m, cg, root_g)

    bpy.ops.wm.save_as_mainfile(filepath=SRC + r"\Glasses.blend")
    return [frame, lenses]


# Каждая модель — свой .blend (имя пустышки HeadAnchor в файле уникально) и свой FBX.
exec(open(r"B:\Project-1\tools\blender\rubezh_fbx.py", encoding="utf-8").read())
result = {}
ONLY_GLASSES = globals().get("ONLY_GLASSES", False)
for build, name in ((build_glasses, "Glasses"),) if ONLY_GLASSES else ((build_earmuffs, "Earmuffs"), (build_glasses, "Glasses")):
    build()
    result[name] = export_collection(name, "//../Equipment/%s.fbx" % name)
