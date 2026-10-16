"""Атласы-палитры Рубежа (YG-12: окружение 2048; MM-13: оружие и снаряжение 1024).

Атлас — сетка ячеек, в каждой цвет палитры (docs/palette.md) с лёгкой фактурой: бетон — крапом,
сталь — шлифовкой, краска — едва заметным шумом. Грани моделей получают UV внутри «своей»
ячейки (tools/blender/atlas.py), поэтому у всего окружения один материал и одна текстура.
Прозрачности нет; блик на очках — нарисован в ячейке стекла.

Запуск: python tools/art/palette_atlas.py → Assets/_Rubezh/Art/Atlas/*.png. Только стандартная
библиотека: PNG пишется через zlib. Генератор детерминированный (зерно).
"""
import math
import os
import random
import struct
import zlib

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Rubezh', 'Art', 'Atlas')

# Сетка 4 × 4; номер ячейки = строка * 4 + столбец, строка 0 — верх картинки.
GRID = 4

# (ключ, цвет, фактура). Порядок = номер ячейки; он же в tools/blender/atlas.py.
ENV_CELLS = [
    ('ENV_ConcreteWall', '#8E8D88', 'concrete'),
    ('ENV_ConcreteFloor', '#66675F', 'concrete'),
    ('ENV_Plaster', '#C9C7C0', 'plaster'),
    ('ENV_PaintedMetal', '#4B535B', 'paint'),
    ('ENV_Steel', '#A3AAB0', 'brushed'),
    ('ENV_SignalYellow', '#F2C200', 'paint'),
    ('ENV_SignWhite', '#ECEAE4', 'paint'),
    ('ENV_Graphite', '#1D2228', 'paint'),
    ('ENV_LampDiffuser', '#F2F0EA', 'flat'),
]
# MM-13: оружие и снаряжение, 1024. Мишенный лист и вспышка — свои материалы (печать и эмиссия).
WEAPON_CELLS = [
    ('MM_Pistol_Polymer', '#1E2124', 'paint'),
    ('MM_Pistol_Steel', '#5C636B', 'brushed'),
    ('MM_Pistol_Control', '#C8691E', 'paint'),
    ('MM_Brass', '#C79A44', 'brushed'),
    ('MM_Copper', '#B5653A', 'brushed'),
    ('MM_Magazine_Follower', '#C8691E', 'paint'),
    ('PPE_Earmuffs', '#2B2F33', 'paint'),
    ('PPE_Cushion', '#1E2124', 'plaster'),
    ('PPE_GlassesFrame', '#23272B', 'paint'),
    ('PPE_GlassesLens', '#6E7E86', 'lens'),
    ('MM_Target_Steel', '#A3AAB0', 'brushed'),
    ('MM_Target_Dark', '#4B535B', 'paint'),
    ('MM_Target_Cable', '#1E2124', 'paint'),
]


def rgb(h):
    h = h.lstrip('#')
    return [int(h[i:i + 2], 16) for i in (0, 2, 4)]


def texel(kind, base, x, y, n, rng_cache):
    """Цвет точки ячейки (x, y в 0..n-1)."""
    if kind == 'flat':
        k = 1.0
    elif kind == 'concrete':
        k = 1.0 + rng_cache[(x * 7 + y * 13) % len(rng_cache)] * 0.07 \
            + 0.04 * math.sin(x * 0.031 + 1.7 * math.sin(y * 0.023)) * math.cos(y * 0.027)
    elif kind == 'plaster':
        k = 1.0 + rng_cache[(x * 11 + y * 5) % len(rng_cache)] * 0.03
    elif kind == 'brushed':
        k = 1.0 + rng_cache[(y * 31) % len(rng_cache)] * 0.06 + rng_cache[(x * 3 + y * 17) % len(rng_cache)] * 0.015
    elif kind == 'lens':
        # Непрозрачное стекло с бликом (YG-10: блик на текстуре вместо прозрачности): горизонтальная
        # светлая полоса в верхней трети — на гнутой линзе из узких граней она идёт сплошной лентой.
        d = y / float(n)
        k = 0.85 + 0.55 * max(0.0, 1.0 - abs(d - 0.3) * 9) + 0.15 * (1.0 - d)
    else:   # paint
        k = 1.0 + rng_cache[(x * 5 + y * 9) % len(rng_cache)] * 0.02
    return [max(0, min(255, int(c * k))) for c in base]


def write_png(path, w, h, rows):
    raw = b''.join(b'\x00' + bytes(r) for r in rows)
    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) \
        + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(png)


def atlas(cells, size, seed):
    rng = random.Random(seed)
    noise = [rng.uniform(-1, 1) for _ in range(4099)]
    n = size // GRID
    rows = [bytearray(size * 3) for _ in range(size)]
    for i, (name, color, kind) in enumerate(cells):
        base = rgb(color)
        cx, cy = (i % GRID) * n, (i // GRID) * n
        for y in range(n):
            row = rows[cy + y]
            for x in range(n):
                c = texel(kind, base, x, y, n, noise)
                o = (cx + x) * 3
                row[o:o + 3] = bytes(c)
    # Пустые ячейки — нейтральный серый (их не использует ни одна грань).
    for i in range(len(cells), GRID * GRID):
        cx, cy = (i % GRID) * n, (i // GRID) * n
        for y in range(n):
            rows[cy + y][cx * 3:(cx + n) * 3] = bytes([128, 128, 128] * n)
    return rows


def emission(cells, size, lit):
    n = size // GRID
    rows = [bytearray(size * 3) for _ in range(size)]
    for i, (name, color, kind) in enumerate(cells):
        if name not in lit:
            continue
        cx, cy = (i % GRID) * n, (i // GRID) * n
        for y in range(n):
            rows[cy + y][cx * 3:(cx + n) * 3] = bytes(rgb(color) * n)
    return rows


def main():
    write_png(os.path.join(ROOT, 'Env_Atlas.png'), 2048, 2048, atlas(ENV_CELLS, 2048, 12))
    write_png(os.path.join(ROOT, 'Env_Atlas_Emission.png'), 256, 256, emission(ENV_CELLS, 256, {'ENV_LampDiffuser'}))
    write_png(os.path.join(ROOT, 'Weapon_Atlas.png'), 1024, 1024, atlas(WEAPON_CELLS, 1024, 13))
    print('ok')


if __name__ == '__main__':
    main()
