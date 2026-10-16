# Экспорт доски дизайн-документа

`design-board.html` собирает доску из `docs/design-doc.md` (карточки по разделам `##`) и картинок
`docs/user-flow.png`, `docs/layout.svg`, мудборда `docs/moodboard/`. Экспорт — `docs/design-doc.png`.

```bash
cd docs && python -m http.server 8765        # страница читает файлы относительными путями
# скопировать design-board.html в docs/ на время экспорта или открыть с тем же сервером, затем:
playwright-cli open http://localhost:8765/design-board.html
playwright-cli resize 1800 1200
playwright-cli screenshot --full-page --filename design-doc.png
```

Шрифт — Segoe UI, рендер — Chromium из Playwright. Доску в Miro можно собрать из этой картинки
и разделов `docs/design-doc.md`.
