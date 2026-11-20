"""Тексты для кода из документов UX:
  docs/script.md → Assets/_Rubezh/Data/Hints/script.ru.json (команды, подсказки, причины грубых нарушений,
                   табло у столов и стоек);
  docs/hints.md  → Assets/_Rubezh/Data/Hints/hint_rules.ru.json (правила подсказок, онбординг, названия нарушений).

Запуск из корня репозитория: python tools/docs/script_to_json.py
Таблицы документов — источник, JSON генерируется из них.
"""
import io
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "docs", "script.md")
DST = os.path.join(ROOT, "Assets", "_Rubezh", "Data", "Hints", "script.ru.json")
HINTS_SRC = os.path.join(ROOT, "docs", "hints.md")
HINTS_DST = os.path.join(ROOT, "Assets", "_Rubezh", "Data", "Hints", "hint_rules.ru.json")


def table(text, heading):
    """Строки первой таблицы после заголовка (или начиная с её шапки), без шапки."""
    part = text[text.index(heading):]
    nxt = part.find("\n## ", 5)
    part = part if nxt < 0 else part[:nxt]
    rows = []
    for l in part.splitlines():
        if l.startswith("|"):
            rows.append(l)
        elif rows:
            break               # таблица кончилась: дальше в разделе могут идти другие
    rows = [l for l in rows if not l.startswith("|---")][1:]
    return [[c.strip() for c in r.strip().strip("|").split("|")] for r in rows]


def clean(s):
    return s.strip().strip("«»").replace("`", "")


def lines(cell):
    """Пункты в ячейке таблицы разделены <br>."""
    return [p.strip() for p in cell.split("<br>") if p.strip()]


def main():
    text = io.open(SRC, encoding="utf-8").read()
    data = {
        "language": "ru",
        "source": "docs/script.md",
        "commands": [{"phase": clean(r[0]), "text": clean(r[2]), "voice": clean(r[3])}
                     for r in table(text, "## 2. Команды")],
        "hints": [{"id": clean(r[1]), "text": clean(r[2])} for r in table(text, "## 3. Подсказки")],
        "failReasons": [{"violation": clean(r[0]), "title": r[1], "text": r[2]}
                        for r in table(text, "## 4. Причины")],
    }
    # §6: первая таблица — места, вторая — табло по фазам, третья — служебные строки.
    boards = text[text.index("## 6. Табло"):]
    places = table(boards, "| Место `Station`")
    cards = table(boards, "| Фаза `Phase`")
    service = {clean(r[0]): "\n".join(lines(r[1])) for r in table(boards, "| Ключ")}
    data["stationNames"] = [{"station": clean(r[0]), "where": r[1]} for r in places]
    data["stations"] = [{"phase": clean(r[0]), "station": clean(r[1]), "title": r[2], "steps": lines(r[3])}
                        for r in cards]
    data["boardNow"] = service["now"]
    data["boardElsewhere"] = service["elsewhere"]
    known_places = {p["station"] for p in data["stationNames"]} | {"Any"}
    stray = [c["phase"] for c in data["stations"] if c["station"] not in known_places]
    if stray:
        raise SystemExit(f"в script.md §6 неизвестное место у фаз: {stray}")
    io.open(DST, "w", encoding="utf-8", newline="\n").write(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    hints = io.open(HINTS_SRC, encoding="utf-8").read()
    rules = {
        "language": "ru",
        "source": "docs/hints.md",
        "idleSecondsKey": "RulesConfig.hintIdleSeconds",
        "highlightColor": "#1FB6C9",
        "rules": [{"step": r[0], "phase": clean(r[1]), "condition": clean(r[3]), "target": clean(r[4]), "hint": clean(r[5])}
                  for r in table(hints, "## 2. Таблица правил")],
        "onboarding": [{"id": clean(r[0]), "when": r[1], "text": r[2]} for r in table(hints, "## 4. Онбординг")],
        "violationNames": [{"violation": clean(r[0]), "name": r[1]} for r in table(hints, "## 5. Названия нарушений")],
    }
    known = {h["id"] for h in data["hints"]}
    missing = [r["hint"] for r in rules["rules"] if r["hint"] not in known]
    if missing:
        raise SystemExit(f"в script.md нет подсказок: {missing}")
    io.open(HINTS_DST, "w", encoding="utf-8", newline="\n").write(json.dumps(rules, ensure_ascii=False, indent=2) + "\n")
    print(f"{len(rules['rules'])} rules, {len(rules['onboarding'])} onboarding, {len(rules['violationNames'])} names -> {HINTS_DST}")
    print(f"{len(data['commands'])} команд, {len(data['hints'])} подсказок, {len(data['failReasons'])} причин, "
          f"{len(data['stations'])} табло -> {DST}")


if __name__ == "__main__":
    main()
