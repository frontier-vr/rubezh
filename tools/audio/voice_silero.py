"""Голос инструктора (YG-13): нейросетевой синтез речи Silero TTS, модель v5_ru, диктор xenia.

Читает Assets/_Rubezh/Data/Hints/script.ru.json и пишет WAV (24 кГц, 16 бит, моно) в
Assets/_Rubezh/Audio/Voice/ — имена те же, что ждёт InstructorVoice:
  команды        — по ID озвучки (cmd_*);
  подсказки      — по ID (hint_*), звучит только действие после «кнопка:»;
  грубые нарушения — fail_<Violation> («заголовок. пояснение»).
Тексты табло (stations) не озвучиваются: их читают глазами.

Один раз:  pip install torch numpy  (модель ~140 МБ скачается сама при первом запуске)
Запуск из корня репозитория:
  python tools/audio/voice_silero.py                 все фразы
  python tools/audio/voice_silero.py --only cmd_fire  одна фраза (после правки текста или ударения)
  python tools/audio/voice_silero.py --speaker eugene другой диктор: aidar, baya, kseniya, eugene, xenia

Что править, если фраза звучит не так — docs/voice.md.
Лицензия модели — CC BY-NC-SA 4.0 (некоммерческое использование), строка в CREDITS.md.
"""
import argparse
import io
import json
import os
import wave

import numpy as np
import torch

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPT = os.path.join(ROOT, "Assets", "_Rubezh", "Data", "Hints", "script.ru.json")
OUT = os.path.join(ROOT, "Assets", "_Rubezh", "Audio", "Voice")

MODEL = "v5_ru"
SPEAKER = "xenia"
RATE = 24000
PAD_SECONDS = 0.08      # тишина в начале и конце клипа
PEAK = 0.89             # пик после нормализации, около −1 дБ

# То, что модель не читает или читает не так: цифры, сокращения, кавычки.
SPOKEN = [
    ("1,5 с", "полутора секунд"),
    ("«", ""),
    ("»", ""),
    (" — ", ", "),
]

# Ударения, которые нельзя оставлять на усмотрение модели: «+» перед ударной гласной.
# Ключ — как написано в сценарии (в нижнем регистре), значение — с ударением.
STRESS = {
    "в шкафу": "в шкаф+у",
    "к шкафу": "к шк+афу",
    "оружие": "ор+ужие",
    "затвор": "затв+ор",
    "затворе": "затв+оре",
    "на задержку": "на зад+ержку",
    "стойку": "ст+ойку",
    "рубеж": "руб+еж",
    "мишень": "миш+ень",
    "мишени": "миш+ени",
    "наушники": "на+ушники",
    "дослать": "досл+ать",
    "отвести": "отвест+и",
    "пульта": "п+ульта",
    "шахту": "ш+ахту",
    "дульный срез": "д+ульный ср+ез",
}


def word(ch):
    return ch.isalpha() or ch == "+"


def spoken(text):
    """Текст сценария → текст для модели: без кавычек и цифр, с ударениями, с точкой в конце."""
    for src, dst in SPOKEN:
        text = text.replace(src, dst)
    low = text.lower()
    for src in sorted(STRESS, key=len, reverse=True):
        start = 0
        while True:
            i = low.find(src, start)
            if i < 0:
                break
            end = i + len(src)
            # Целое слово, и ударение в нём ещё не стоит («+» — часть слова).
            whole = (i == 0 or not word(low[i - 1])) and (end == len(low) or not word(low[end]))
            if whole:
                marked = STRESS[src]
                if text[i].isupper():
                    marked = marked[0].upper() + marked[1:]
                text = text[:i] + marked + text[end:]
                low = text.lower()
                start = i + len(marked)
            else:
                start = end
    text = text.strip()
    return text if text[-1] in ".!?" else text + "."


def phrases(data):
    """(ID клипа, текст сценария) — всё, что говорит инструктор."""
    for c in data["commands"]:
        yield c["voice"], c["text"]
    for h in data["hints"]:
        text = h["text"]
        i = text.find(": ")
        if i >= 0:
            text = text[i + 2:]
        yield h["id"], text[0].upper() + text[1:]
    for f in data["failReasons"]:
        yield "fail_" + f["violation"], f["title"] + ". " + f["text"]


def save(path, audio):
    audio = audio / max(1e-6, float(np.max(np.abs(audio)))) * PEAK
    pad = np.zeros(int(RATE * PAD_SECONDS), dtype=np.float32)
    pcm = (np.concatenate([pad, audio, pad]) * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())
    return len(pcm) / RATE


def main():
    ap = argparse.ArgumentParser(description="Озвучка сценария инструктора синтезом Silero")
    ap.add_argument("--speaker", default=SPEAKER, help="диктор модели: aidar, baya, kseniya, eugene, xenia")
    ap.add_argument("--only", nargs="*", default=None, help="ID клипов, которые пересобрать (по умолчанию все)")
    ap.add_argument("--out", default=OUT, help="куда писать WAV")
    args = ap.parse_args()

    data = json.load(io.open(SCRIPT, encoding="utf-8"))
    os.makedirs(args.out, exist_ok=True)
    torch.set_num_threads(4)
    model, _ = torch.hub.load(repo_or_dir="snakers4/silero-models", model="silero_tts",
                              language="ru", speaker=MODEL, trust_repo=True)
    model.to(torch.device("cpu"))

    done = 0
    for clip, text in phrases(data):
        if args.only and clip not in args.only:
            continue
        say = spoken(text)
        audio = model.apply_tts(text=say, speaker=args.speaker, sample_rate=RATE, put_accent=True, put_yo=True)
        seconds = save(os.path.join(args.out, clip + ".wav"), audio.numpy().astype(np.float32))
        print(f"{clip}\t{seconds:.2f} c\t{say}")
        done += 1
    print(f"{done} клипов, диктор {args.speaker} -> {args.out}")


if __name__ == "__main__":
    main()
