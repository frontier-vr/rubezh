"""YG-13: фон тира — ровный гул вентиляции и сети, без выстрелов и голосов, бесшовная петля.

Запуск: python tools/audio/ambience_synth.py → Assets/_Rubezh/Audio/Ambience/Range_RoomTone.wav
(44,1 кГц, 16 бит, моно, 20 с). Собственный синтез команды, CC0.
"""
import math
import os
import random
import struct
import wave

RATE = 44100
SECONDS = 20.0
FADE = 2.0          # перекрытие конца и начала для бесшовной петли
OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Rubezh', 'Audio', 'Ambience', 'Range_RoomTone.wav')


def lowpass(x, cutoff):
    k = 1.0 - math.exp(-2.0 * math.pi * cutoff / RATE)
    s, y = 0.0, []
    for v in x:
        s += k * (v - s)
        y.append(s)
    return y


def main():
    rng = random.Random(42)
    n = int(RATE * (SECONDS + FADE))
    noise = [rng.uniform(-1, 1) for _ in range(n)]
    vent = lowpass(lowpass(noise, 600), 900)                     # шум вентиляции
    rumble = lowpass(lowpass([rng.uniform(-1, 1) for _ in range(n)], 90), 120)   # низкий гул помещения
    out = []
    for i in range(n):
        t = i / RATE
        hum = 0.05 * math.sin(2 * math.pi * 50 * t) + 0.025 * math.sin(2 * math.pi * 100 * t) + 0.012 * math.sin(2 * math.pi * 150 * t)
        swell = 0.85 + 0.15 * math.sin(2 * math.pi * t / 7.3)    # медленное «дыхание» вентиляции
        out.append(hum + 2.2 * vent[i] * swell + 3.5 * rumble[i])
    body = int(RATE * SECONDS)
    fade = int(RATE * FADE)
    loop = out[:body]
    for i in range(fade):                                        # хвост плавно переходит в начало
        a = i / fade
        loop[i] = loop[i] * a + out[body + i] * (1 - a)
    peak = max(abs(v) for v in loop)
    loop = [0.35 * v / peak for v in loop]
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(v * 32767)) for v in loop))
    print(os.path.normpath(OUT), os.path.getsize(OUT))


if __name__ == '__main__':
    main()
