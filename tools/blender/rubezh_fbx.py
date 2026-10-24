"""Экспорт FBX для Рубежа с настройками из docs/art-pipeline.md.

Запуск в Blender: Scripting → Open → этот файл → Run Script, затем в консоли Python
    export_collection("Pistol", "//../Weapon/Pistol.fbx")
или из MCP/командной строки через exec(open(path).read()).
Путь с «//» считается от .blend-файла.
"""
import bpy

# Проверенная связка Blender 5.1.2 + Unity 6000.3.24f1 (MM-04): в Unity rotation (0,0,0),
# scale (1,1,1), 1 единица = 1 м, перёд модели (−Y в Blender) смотрит в +Z.
FBX_SETTINGS = dict(
    apply_scale_options='FBX_SCALE_ALL',   # Apply Scalings: FBX All
    axis_forward='-Z',                     # Forward: -Z
    axis_up='Y',                           # Up: Y
    bake_space_transform=True,             # Apply Transform
    object_types={'MESH', 'EMPTY'},
    use_mesh_modifiers=True,
    mesh_smooth_type='FACE',
    use_tspace=False,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode='AUTO',
    embed_textures=False,
)


def triangles(objects):
    """Число треугольников после триангуляции (как оверлей Statistics)."""
    deps = bpy.context.evaluated_depsgraph_get()
    total = 0
    for ob in objects:
        if ob.type != 'MESH':
            continue
        me = ob.evaluated_get(deps).to_mesh()
        total += sum(len(p.vertices) - 2 for p in me.polygons)
        ob.evaluated_get(deps).to_mesh_clear()
    return total


def export_collection(name, filepath):
    """Экспортирует все объекты коллекции name (с детьми) в filepath. Возвращает число треугольников."""
    coll = bpy.data.collections[name]
    objs = list(coll.all_objects)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:
        ob.hide_set(False)
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = bpy.path.abspath(filepath)
    # Сразу после открытия файла из скрипта у контекста нет окна: даём его явно.
    window = bpy.context.window or bpy.context.window_manager.windows[0]
    with bpy.context.temp_override(window=window, view_layer=bpy.context.view_layer):
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, **FBX_SETTINGS)
    return triangles(objs)
