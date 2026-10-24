"""MM-09: звуки оружия для Рубежа, синтезированные с нуля (без чужих записей).

Запуск: python tools/audio/sfx_synth.py  →  Assets/_Rubezh/Audio/SFX/*.wav (44,1 кГц, 16 бит, моно).
Звуки — собственная работа команды, лицензия CC0 (строки в CREDITS.md). Генератор с зерном:
повторный запуск даёт те же файлы.
"""
import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Rubezh', 'Audio', 'SFX')


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def noise(rng, n):
    return [rng.uniform(-1.0, 1.0) for _ in range(n)]


def env_exp(n, tau, attack=0.0005):
    a = max(1, int(RATE * attack))
    return [min(1.0, i / a) * math.exp(-i / (RATE * tau)) for i in range(n)]


def lowpass(x, cutoff):
    k = 1.0 - math.exp(-2.0 * math.pi * cutoff / RATE)
    y, s = [], 0.0
    for v in x:
        s += k * (v - s)
        y.append(s)
    return y


def highpass(x, cutoff):
    lp = lowpass(x, cutoff)
    return [a - b for a, b in zip(x, lp)]


def resonator(x, freq, q):
    """Двухполюсный резонатор: металлический «звон» детали."""
    r = math.exp(-math.pi * freq / (q * RATE))
    c = 2.0 * r * math.cos(2.0 * math.pi * freq / RATE)
    y1 = y2 = 0.0
    out = []
    for v in x:
        y = v * (1 - r) + c * y1 - r * r * y2
        out.append(y)
        y2, y1 = y1, y
    return out


def mix(*tracks, length=None):
    n = length or max(len(t) for _, t in tracks)
    out = [0.0] * n
    for offset, t in tracks:
        start = int(RATE * offset)
        for i, v in enumerate(t):
            if 0 <= start + i < n:
                out[start + i] += v
    return out


def scale(x, g):
    return [v * g for v in x]


def metal_click(rng, freqs, tau=0.012, gain=1.0, length=0.08, q=18):
    n = int(RATE * length)
    exc = [a * b for a, b in zip(noise(rng, n), env_exp(n, 0.0015))]
    body = [0.0] * n
    for f, g in freqs:
        body = [a + g * b for a, b in zip(body, resonator(exc, f, q))]
    e = env_exp(n, tau)
    return [gain * a * b for a, b in zip(body, e)]


def thump(freq_start, freq_end, tau, length, gain):
    n = int(RATE * length)
    out, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        f = freq_end + (freq_start - freq_end) * math.exp(-t / (tau * 0.5))
        phase += 2 * math.pi * f / RATE
        out.append(gain * math.sin(phase) * math.exp(-t / tau))
    return out


def normalize(x, peak=0.89):
    m = max(abs(v) for v in x) or 1.0
    y = [math.tanh(1.3 * v / m) for v in x]       # мягкое ограничение
    m2 = max(abs(v) for v in y) or 1.0
    return [peak * v / m2 for v in y]


def fade_out(x, seconds=0.01):
    n = int(RATE * seconds)
    for i in range(min(n, len(x))):
        x[-1 - i] *= i / n
    return x


def write(name, x):
    os.makedirs(OUT, exist_ok=True)
    x = fade_out(normalize(x))
    path = os.path.join(OUT, name + '.wav')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v)) * 32767)) for v in x))
    return path


def shot(rng):
    n = int(RATE * 0.7)
    crack = [a * b for a, b in zip(highpass(noise(rng, n), 1800), env_exp(n, 0.006, 0.0002))]
    blast = [a * b for a, b in zip(lowpass(noise(rng, n), 2500), env_exp(n, 0.035, 0.0005))]
    body = thump(160, 55, 0.09, 0.7, 1.0)
    tail = [a * b for a, b in zip(lowpass(noise(rng, n), 700), env_exp(n, 0.22, 0.01))]
    mech = metal_click(rng, [(2900, 1.0), (5300, 0.6)], tau=0.01, gain=0.25, length=0.1)
    return mix((0, scale(crack, 1.2)), (0, scale(blast, 1.0)), (0, scale(body, 0.9)), (0.004, scale(tail, 0.45)),
               (0.035, mech), length=n)


def slide_back(rng):
    scrape_n = int(RATE * 0.07)
    scrape = [a * b * 0.18 for a, b in zip(resonator(highpass(noise(rng, scrape_n), 1500), 3800, 4), [math.sin(math.pi * i / scrape_n) for i in range(scrape_n)])]
    return mix((0, metal_click(rng, [(2300, 1.0), (4100, 0.7), (6700, 0.4)], gain=0.7)),
               (0.02, scrape),
               (0.1, metal_click(rng, [(1900, 1.0), (3600, 0.8)], tau=0.02, gain=1.0)), length=int(RATE * 0.25))


def slide_forward(rng):
    return mix((0, metal_click(rng, [(2600, 0.8), (4800, 0.5)], gain=0.5, tau=0.008)),
               (0.045, metal_click(rng, [(1500, 1.0), (2900, 0.9), (5200, 0.5)], tau=0.03, gain=1.0)),
               (0.045, thump(320, 180, 0.025, 0.08, 0.5)), length=int(RATE * 0.22))


def mag_insert(rng):
    return mix((0, thump(260, 140, 0.02, 0.06, 0.6)),
               (0.0, metal_click(rng, [(1800, 1.0), (3200, 0.6)], tau=0.01, gain=0.8)),
               (0.016, metal_click(rng, [(2400, 1.0), (4300, 0.6)], tau=0.012, gain=1.0)), length=int(RATE * 0.16))


def mag_drop(rng):
    tracks = [(0, metal_click(rng, [(2100, 1.0), (3900, 0.5)], tau=0.008, gain=0.6))]
    t, g = 0.03, 0.5
    for _ in range(4):
        tracks.append((t, metal_click(rng, [(1400 + rng.uniform(-150, 150), 1.0), (2600, 0.4)], tau=0.006, gain=g, q=10)))
        t += rng.uniform(0.018, 0.035)
        g *= 0.7
    return mix(*tracks, length=int(RATE * 0.22))


def dry_fire(rng):
    return mix((0, metal_click(rng, [(3500, 1.0), (5100, 0.7), (7900, 0.3)], tau=0.006, gain=1.0, q=25)),
               (0.012, metal_click(rng, [(2100, 0.6)], tau=0.005, gain=0.3)), length=int(RATE * 0.09))


def safety(rng):
    return mix((0, metal_click(rng, [(2500, 1.0), (4500, 0.6)], tau=0.005, gain=1.0, q=22)), length=int(RATE * 0.06))


def casing_concrete(rng):
    tracks = []
    t, g = 0.0, 1.0
    for bounce in range(4):
        ping = metal_click(rng, [(3100, 1.0), (5900, 0.7), (8400, 0.45), (11200, 0.2)], tau=0.07 * g + 0.02, gain=g, length=0.25, q=60)
        hit_n = int(RATE * 0.01)
        hit = [a * b * 0.3 * g for a, b in zip(highpass(noise(rng, hit_n), 2500), env_exp(hit_n, 0.002))]
        tracks += [(t, ping), (t, hit)]
        t += 0.12 * (0.62 ** bounce)
        g *= 0.55
    return mix(*tracks, length=int(RATE * 0.6))


def round_insert(rng):
    spring_n = int(RATE * 0.05)
    spring = [a * b * 0.25 for a, b in zip(resonator(noise(rng, spring_n), 1200, 30), env_exp(spring_n, 0.015))]
    return mix((0, metal_click(rng, [(2700, 1.0), (4600, 0.5)], tau=0.008, gain=1.0)), (0.004, spring), length=int(RATE * 0.1))


SOUNDS = {
    'Pistol_Shot': shot,
    'Pistol_SlideBack': slide_back,
    'Pistol_SlideForward': slide_forward,
    'Magazine_Insert': mag_insert,
    'Magazine_Drop': mag_drop,
    'Pistol_DryFire': dry_fire,
    'Pistol_Safety': safety,
    'Casing_Concrete': casing_concrete,
    'Round_Insert': round_insert,
}

if __name__ == '__main__':
    for i, (name, fn) in enumerate(SOUNDS.items()):
        path = write(name, fn(random.Random(1000 + i)))
        print(os.path.normpath(path), os.path.getsize(path))
