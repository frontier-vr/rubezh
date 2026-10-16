"""YG-11: таблички безопасности, линия огня, контуры мест, метка финиша.

Каждая деталь — свой объект в начале координат, лицом к Unity +Z (в Blender −Y); на места
по docs/layout.md §3 их ставит префаб Prefabs/Art/Env_Signage. Табличка: жёлтая рамка
#F2C200 и белое поле #ECEAE4, текст — TextMesh Pro в Unity. Контуры мест — белые рамки
шириной 8 мм на столешнице, метка финиша — зелёная #3E9E4F. Прозрачности нет.
Запуск: exec(open(r"...\\tools\\blender\\signage.py").read()) → Art/Source~/Signage.blend и
Art/Environment/Signage/Env_Signage.fbx.
"""
import bmesh
import bpy
from mathutils import Matrix, Vector

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~\Signage.blend"

YELLOW = ('ENV_SignalYellow', '#F2C200')
WHITE = ('ENV_SignWhite', '#ECEAE4')
GREEN = ('ENV_FinishGreen', '#3E9E4F')

# (имя, ширина, высота) табличек, м
PLATES = [('Sign_T1_O-15', 0.40, 0.50), ('Sign_T2_O-30', 0.30, 0.20), ('Sign_PPE_O-11', 0.50, 0.25),
          ('Label_Return_O-03', 0.50, 0.10)]
# (имя, ширина по X Unity, глубина по Z Unity, материал) контуров и разметки
RINGS = [('Contour_O-08_Pistol', 0.21, 0.265, WHITE), ('Contour_O-08_Magazine', 0.17, 0.10, WHITE),
         ('Contour_O-19_Magazine', 0.10, 0.17, WHITE), ('Contour_O-20_Pistol', 0.265, 0.21, WHITE),
         ('Contour_O-27_Pistol', 0.21, 0.265, WHITE), ('Contour_O-28_Magazine', 0.17, 0.10, WHITE),
         ('Finish_O-06', 0.10, 0.27, GREEN)]
LINE = 0.008      # ширина линии контура
FLAT = 0.002      # толщина разметки над поверхностью


def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0])


def material(spec):
    name, color = spec
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = srgb(color)
    bsdf.inputs['Roughness'].default_value = 0.8
    m.diffuse_color = srgb(color)
    return m


def box(bm, center, size, mat_index):
    res = bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*size, 1)), verts=res['verts'])
    for f in {f for v in res['verts'] for f in v.link_faces}:
        f.material_index = mat_index


def obj(name, bm, mats, coll):
    me = bpy.data.meshes.new(name)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    return ob


def build():
    bpy.ops.wm.read_homefile(use_empty=True)
    scene = bpy.context.scene
    coll = bpy.data.collections.new('Signage')
    scene.collection.children.link(coll)
    yellow, white, green = material(YELLOW), material(WHITE), material(GREEN)
    tri = {}
    for name, w, h in PLATES:
        bm = bmesh.new()
        frame_mat = green if name.startswith('Label_Return') else yellow
        box(bm, Vector((0, 0.006, 0)), (w, 0.012, h), 0)                     # рамка, задняя плоскость у стены
        box(bm, Vector((0, -0.001, 0)), (w - 0.02, 0.004, h - 0.02), 1)      # поле таблички чуть впереди
        ob = obj(name, bm, [frame_mat, white], coll)
        tri[name] = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    for name, wx, dz, spec in RINGS:
        bm = bmesh.new()
        hx, hy = wx / 2, dz / 2
        z = FLAT / 2
        box(bm, Vector((0, -hy + LINE / 2, z)), (wx, LINE, FLAT), 0)
        box(bm, Vector((0, hy - LINE / 2, z)), (wx, LINE, FLAT), 0)
        box(bm, Vector((-hx + LINE / 2, 0, z)), (LINE, dz - 2 * LINE, FLAT), 0)
        box(bm, Vector((hx - LINE / 2, 0, z)), (LINE, dz - 2 * LINE, FLAT), 0)
        ob = obj(name, bm, [material(spec)], coll)
        tri[name] = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    bm = bmesh.new()
    box(bm, Vector((0, 0, FLAT / 2)), (3.1, 0.1, FLAT), 0)                     # линия огня O-29
    ob = obj('FireLine_O-29', bm, [yellow], coll)
    tri['FireLine_O-29'] = 12
    window = bpy.context.window or bpy.context.window_manager.windows[0]
    layer = scene.view_layers[0]
    for ob in coll.objects:                                                    # UV под атлас YG-12
        layer.objects.active = ob
        with bpy.context.temp_override(window=window, view_layer=layer, active_object=ob, object=ob,
                                       selected_objects=[ob], selected_editable_objects=[ob]):
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.uv.smart_project(island_margin=0.02)
            bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.wm.save_as_mainfile(filepath=SRC)
    return tri


tri = build()
exec(open(r"B:\Project-1\tools\blender\rubezh_fbx.py", encoding="utf-8").read())
import os
os.makedirs(r"B:\Project-1\Assets\_Rubezh\Art\Environment\Signage", exist_ok=True)
total = export_collection('Signage', r"B:\Project-1\Assets\_Rubezh\Art\Environment\Signage\Env_Signage.fbx")
result = {"parts": tri, "total": total}
