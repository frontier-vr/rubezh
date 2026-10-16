"""MM-13: оружие, магазин, патрон, гильза, СИЗ и мишенный механизм — на один атлас 1024.

Открывает исходники Art/Source~/*.blend, раскладывает грани по ячейкам Weapon_Atlas.png
(tools/blender/atlas.py, палитра docs/palette.md), сохраняет .blend и переэкспортирует FBX по
тем же путям (GUID в Unity не меняются). Мишенный лист (MM_Target_Paper, печатная текстура MM-12)
остаётся своим материалом; вспышка MM-17 — своя эмиссия, в атлас не входит.
Вложенность глубже одного уровня FBX с Apply Transform ломает — перед экспортом детали
становятся прямыми детьми корня (как в target_mechanism.py), .blend сохраняется до этого.

Запуск: exec(open(r"B:\\Project-1\\tools\\blender\\weapon_atlas.py", encoding="utf-8").read())
"""
import os
import bpy

exec(open(r"B:\Project-1\tools\blender\rubezh_fbx.py", encoding="utf-8").read())
exec(open(r"B:\Project-1\tools\blender\atlas.py", encoding="utf-8").read())

SRC = r"B:\Project-1\Assets\_Rubezh\Art\Source~"
ART = r"B:\Project-1\Assets\_Rubezh\Art"
KEEP = ("MM_Target_Paper",)

# .blend → [(коллекция, FBX)]
JOBS = {
    "Pistol.blend": [("Pistol", r"Weapon\Pistol.fbx")],
    "Magazine.blend": [("Magazine", r"Weapon\Magazine.fbx"), ("Round", r"Weapon\Round.fbx"), ("Casing", r"Weapon\Casing.fbx")],
    "Earmuffs.blend": [("Earmuffs", r"Equipment\Earmuffs.fbx")],
    "Glasses.blend": [("Glasses", r"Equipment\Glasses.fbx")],
    "TargetMechanism.blend": [("TargetMechanism", r"Target\TargetMechanism.fbx")],
}


def flatten(coll):
    roots = [o for o in coll.objects if o.parent is None]
    if len(roots) != 1:
        return 0
    root = roots[0]
    moved = 0
    for ob in list(coll.all_objects):
        if ob is root or ob.parent is root:
            continue
        world = ob.matrix_world.copy()
        ob.parent = root
        ob.matrix_parent_inverse = root.matrix_world.inverted()
        ob.matrix_world = world
        moved += 1
    return moved


def run():
    report = {}
    for blend, jobs in JOBS.items():
        bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, blend))
        objs = [o for c, _ in jobs for o in bpy.data.collections[c].all_objects if o.type == 'MESH']
        missing = atlasize(objs, WEAPON_CELLS, "MM_Atlas", keep=KEEP)
        assert not missing, (blend, missing)
        bpy.ops.wm.save_mainfile()
        for coll, fbx in jobs:
            flat = flatten(bpy.data.collections[coll])
            tris = export_collection(coll, os.path.join(ART, fbx))
            report[coll] = (tris, flat)
    return report


result = run()
