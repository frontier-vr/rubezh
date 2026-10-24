"""Тексты для кода из документов UX:
  docs/script.md → Assets/_Rubezh/Data/Hints/script.ru.json (команды, подсказки, причины стопа);
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
    part = text[text.index(heading):]
    nxt = part.find("\n## ", 5)
    part = part if nxt < 0 else part[:nxt]
    rows = [l for l in part.splitlines() if l.startswith("|") and not l.startswith("|---")][1:]
    return [[c.strip() for c in r.strip().strip("|").split("|")] for r in rows]


def clean(s):
    return s.strip().strip("«»").replace("`", "")


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
    print(f"{len(data['commands'])} команд, {len(data['hints'])} подсказок, {len(data['failReasons'])} причин -> {DST}")


if __name__ == "__main__":
    main()
