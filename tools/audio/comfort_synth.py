"""YG-16: звуки комфорта перемещения — мягкий «вжух» телепорта и тихий щелчок поворота.

Запуск: python tools/audio/comfort_synth.py → Assets/_Rubezh/Audio/SFX/Locomotion_*.wav
(44,1 кГц, 16 бит, моно). Собственный синтез, CC0.
"""
import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Rubezh', 'Audio', 'SFX')


def write(name, data):
    path = os.path.join(OUT, name)
    peak = max(abs(v) for v in data) or 1.0
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(v / peak * 0.5 * 32767)) for v in data))
    print(os.path.normpath(path))


def teleport():
    """Шум с полосовым фильтром, частота плавно поднимается: короткий мягкий «вжух», 0,35 с."""
    rng = random.Random(7)
    n = int(RATE * 0.35)
    out, lp, bp = [], 0.0, 0.0
    for i in range(n):
        t = i / n
        f = 300 + 1700 * t * t                      # полоса уходит вверх
        k = 1 - math.exp(-2 * math.pi * f / RATE)
        x = rng.uniform(-1, 1)
        lp += k * (x - lp)
        bp += k * (lp - bp)
        env = math.sin(math.pi * t) ** 1.5           # плавно вошёл и вышел
        out.append((lp - bp) * env)
    return out


def snap_turn():
    """Тихий щелчок: затухающий тон 1,2 кГц с призвуком, 0,06 с."""
    n = int(RATE * 0.06)
    out = []
    for i in range(n):
        t = i / RATE
        env = math.exp(-t * 70)
        out.append(env * (math.sin(2 * math.pi * 1200 * t) + 0.4 * math.sin(2 * math.pi * 2650 * t)))
    return out


if __name__ == '__main__':
    write('Locomotion_Teleport.wav', teleport())
    write('Locomotion_SnapTurn.wav', snap_turn())
