# MCP для Claude Code: Unity + Blender

`.mcp.json` лежит в корне репо. Claude Code, запущенный из этой папки, сам подхватит три сервера и при первом запуске спросит разрешение.

| Сервер | Что даёт |
|---|---|
| `unity` | MCP for Unity 10.3.0: сцены, префабы, компоненты, материалы, ProBuilder, свет и запекание, UI, VFX, анимация, тесты, сборка, профайлер, C# через Roslyn |
| `blender` | Официальный Blender Lab MCP 1.0.3: любой `bpy`-код, скриншоты вьюпорта, разбор `.blend`, встроенные документация API и мануал. Порт **9877** |
| `blender-assets` | MCP for Blender 2.1.9: Poly Haven, Sketchfab, Poly Pizza, генерация моделей. Порт **9876**, на него же ходит мост Blender → Unity |

## Один раз на каждом компе

1. **uv**
   - macOS: `brew install uv`
   - Windows: `winget install astral-sh.uv`, затем перезапустить терминал, чтобы `uvx` появился в PATH

   Проверка: `uvx --version`. Ещё нужен git, он уже стоит для работы с репо.
2. **Blender 5.1.2**: https://download.blender.org/release/Blender5.1/
3. **Официальный аддон.** Скачать [mcp-1.0.3.zip](https://projects.blender.org/lab/blender_mcp/releases/download/v1.0.3/mcp-1.0.3.zip) и перетащить в окно Blender. Затем Edit → Preferences → Add-ons → **MCP** → поле **Port: 9877**.
4. **Аддон ассетов.** Скачать [addon.py](https://raw.githubusercontent.com/ahujasid/blender-mcp/7a0373ec9199183cb460068c4f96aed9c579fb4f/addon.py) (правый клик → «Сохранить как»). Затем Preferences → Add-ons → ⌄ → Install from Disk → `addon.py` → включить **MCP for Blender**. Порт не трогать, остаётся 9876.
5. **Unity.** Пакет приезжает из `Packages/manifest.json`, ставить ничего не надо. В Window → MCP for Unity:
   - если не подключается, выставить транспорт **stdio**;
   - во вкладке Tools включить все группы.

## Работа

Сначала открыть Unity и Blender, потом запустить `claude` из корня репо. Проверить подключение можно командой `claude mcp list`.

Необязательно: чтобы работали инструменты Blender `*_for_cli`, которые сами запускают Blender в фоне, задайте переменную `BLENDER_PATH`:
- macOS: `/Applications/Blender.app/Contents/MacOS/Blender`;
- Windows: `C:\Program Files\Blender Foundation\Blender 5.1\blender.exe`.

## Для Никиты: одна строка в `Packages/manifest.json`

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.3.0"
```

Пакет работает только в редакторе и в APK не попадает.

> Серверы выполняют сгенерированный ИИ код в Blender и Unity без песочницы. Перед крупными изменениями делайте коммит.
