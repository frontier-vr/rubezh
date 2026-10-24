"""Перевод моделей на атлас-палитру (YG-12; тот же приём годится для MM-13).

Каждая грань получает UV внутри ячейки атласа своего материала (tools/art/palette_atlas.py):
проекция на плоскость по главной оси нормали, с сохранением пропорций грани, с полем от края
ячейки — без заползания на соседний цвет при мипмапах. Потом у меша остаётся один материал.

Запуск в Blender (после построения моделей):
    exec(open(r"B:\\Project-1\\tools\\blender\\atlas.py", encoding="utf-8").read())
    atlasize(objects, ENV_CELLS, "ENV_Atlas")
"""
import bmesh
import bpy

_PALETTE = r"B:\Project-1\tools\art\palette_atlas.py"
_pal = {"__file__": _PALETTE}
exec(open(_PALETTE, encoding="utf-8").read().split("def rgb")[0], _pal)
ENV_CELLS = [c[0] for c in _pal["ENV_CELLS"]]
GRID = _pal["GRID"]


def cell_rect(index, pad=0.04):
    """Прямоугольник ячейки в UV (v снизу вверх; строка 0 — верх картинки) с полем pad от размера ячейки."""
    size = 1.0 / GRID
    col, row = index % GRID, index // GRID
    u0 = col * size + pad * size
    v0 = 1.0 - (row + 1) * size + pad * size
    return u0, v0, size * (1 - 2 * pad)


def atlas_material(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def atlasize(objects, cells, material_name, pad=0.04):
    """Все грани objects — в ячейки атласа по имени их материала; один материал material_name."""
    mat = atlas_material(material_name)
    missing = set()
    for ob in objects:
        if ob.type != 'MESH':
            continue
        me = ob.data
        names = [s.material.name if s.material else '' for s in ob.material_slots]
        bm = bmesh.new()
        bm.from_mesh(me)
        uv = bm.loops.layers.uv.active or bm.loops.layers.uv.new('UVMap')
        for f in bm.faces:
            name = names[f.material_index] if f.material_index < len(names) else ''
            if name not in cells:
                missing.add(name)
                index = 0
            else:
                index = cells.index(name)
            n = f.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            a, b = [(1, 2), (0, 2), (0, 1)][ax]
            pts = [(l.vert.co[a], l.vert.co[b]) for l in f.loops]
            ua = [p[0] for p in pts]
            vb = [p[1] for p in pts]
            ext = max(max(ua) - min(ua), max(vb) - min(vb), 1e-6)
            u0, v0, s = cell_rect(index, pad)
            for l, (pa, pb) in zip(f.loops, pts):
                l[uv].uv = (u0 + (pa - min(ua)) / ext * s, v0 + (pb - min(vb)) / ext * s)
            f.material_index = 0
        bm.to_mesh(me)
        bm.free()
        me.materials.clear()
        me.materials.append(mat)
    return sorted(missing)
