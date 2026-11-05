"""Чистка сетки перед экспортом (MM-14): н-гоны, нормали, вырожденные грани, рёбра на 3+ гранях.

Правки не трогают UV и материалы, поэтому атласы (MM-13, YG-12) остаются на месте:
- н-гоны (больше 4 вершин) — триангуляция только этих граней (BEAUTY);
- нормали — пересчёт наружу у замкнутых кусков, где нашлись грани внутрь (плоскости не трогаем);
- микротреугольники булевых операций (рёбра в сотые доли миллиметра) — вершины свариваются;
- ребро на 3+ гранях (коробки, склеенные по ребру) — куски отделяются друг от друга и остаются
  замкнутыми оболочками, вид не меняется;
- если правка открыла дыры (граничных рёбер стало больше), меш откатывается и попадает в отчёт.
После правок audit() должен вернуть нули. Запуск из Blender (MCP или Scripting):
    exec(open(r"B:/Project-1/tools/blender/mesh_hygiene.py", encoding="utf-8").read())
    clean_blend("Pistol.blend", [("Pistol", r"Weapon\\Pistol.fbx")])
"""
import os
import bmesh
import bpy

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~"
ART = r"B:\Project-1\Assets\_Rubezh\Art"


def _components(bm):
    seen, comps = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        stack, comp = [f], []
        seen.add(f)
        while stack:
            g = stack.pop()
            comp.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        comps.append(comp)
    return comps


def audit_mesh(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    ngons = sum(1 for f in bm.faces if len(f.verts) > 4)
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-10)
    nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    before = {f: f.normal.copy() for f in bm.faces}
    flipped = 0
    for comp in _components(bm):
        closed = all(len(e.link_faces) == 2 for f in comp for e in f.edges)
        if not closed:
            continue
        bmesh.ops.recalc_face_normals(bm, faces=comp)
        flipped += sum(1 for f in comp if f.normal.dot(before[f]) < 0)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    bm.free()
    return dict(tris=tris, ngons=ngons, flipped=flipped, degenerate=degenerate, nonmanifold=nonmanifold)


WELD = 2e-5   # 0,02 мм: микротреугольники булевых вырезов, на порядки мельче любой детали модели


def _groups(bm, cut):
    """Куски меша, связные через рёбра не из cut: коробка, склеенная с соседней по ребру, — свой кусок."""
    seen, groups = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        stack, group = [f], []
        seen.add(f)
        while stack:
            g = stack.pop()
            group.append(g)
            for e in g.edges:
                if e in cut:
                    continue
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        groups.append(group)
    return groups


def _boundary(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    n = sum(1 for e in bm.edges if e.is_boundary)
    bm.free()
    return n


def clean_mesh(me):
    """Чистит меш на месте. Если правка открыла дыры (граничных рёбер стало больше) — откат, False."""
    backup = me.copy()
    boundary_before = _boundary(me)
    bm = bmesh.new()
    bm.from_mesh(me)
    # Микротреугольники после булевых операций: сварить вершины ближе WELD и схлопнуть вырожденное.
    # Сварка — до разделения кусков, иначе она склеит их обратно (#172).
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=WELD)
    bmesh.ops.dissolve_degenerate(bm, dist=WELD, edges=bm.edges[:])
    # Рёбра на 3+ гранях — отделить склеенные куски друг от друга, каждый остаётся замкнутым.
    fins = {e for e in bm.edges if len(e.link_faces) > 2}
    if fins:
        for group in _groups(bm, fins)[1:]:
            edges = {e for f in group for e in f.edges}
            verts = {v for f in group for v in f.verts}
            bmesh.ops.split(bm, geom=list(group) + list(edges) + list(verts), use_only_faces=False)
    # Н-гоны.
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method='BEAUTY', ngon_method='BEAUTY')
    # Нормали наружу у замкнутых кусков.
    for comp in _components(bm):
        if all(len(e.link_faces) == 2 for f in comp for e in f.edges):
            bmesh.ops.recalc_face_normals(bm, faces=comp)
    bm.to_mesh(me)
    bm.free()
    me.update()
    if _boundary(me) > boundary_before:
        bm = bmesh.new()
        bm.from_mesh(backup)
        bm.to_mesh(me)
        bm.free()
        bpy.data.meshes.remove(backup)
        return False
    bpy.data.meshes.remove(backup)
    return True


def flatten(coll):
    """Детей детей — к корню с теми же мировыми положениями (FBX с Apply Transform, art-pipeline §3)."""
    roots = [o for o in coll.objects if o.parent is None]
    if len(roots) != 1:
        return 0
    root, moved = roots[0], 0
    for ob in list(coll.all_objects):
        if ob is root or ob.parent is root:
            continue
        world = ob.matrix_world.copy()
        ob.parent = root
        ob.matrix_parent_inverse = root.matrix_world.inverted()
        ob.matrix_world = world
        moved += 1
    return moved


def clean_blend(blend, jobs, objects=None, export=True, flat=True):
    """Чистит меши .blend (все или objects), сохраняет и переэкспортирует FBX по тем же путям."""
    exec(open(r"B:\Project-1\tools\blender\rubezh_fbx.py", encoding="utf-8").read(), globals())
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, blend))
    report = {}
    done = set()
    for ob in bpy.data.objects:
        if ob.type != 'MESH' or (objects and ob.name not in objects) or ob.data in done:
            continue
        done.add(ob.data)
        before = audit_mesh(ob.data)
        if before['ngons'] or before['flipped'] or before['degenerate'] or before['nonmanifold']:
            ok = clean_mesh(ob.data)
            report[ob.name] = dict(before=before, after=audit_mesh(ob.data), boundary=_boundary(ob.data), ok=ok)
    bpy.ops.wm.save_mainfile()
    if export:
        for coll, fbx in jobs:
            if flat:
                flatten(bpy.data.collections[coll])
            report["fbx:" + coll] = export_collection(coll, os.path.join(ART, fbx))
    return report
