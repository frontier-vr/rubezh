# Релиз: APK, Unity Package, видео (NG-20)

Что выкладываем в конце спринта и с вехой M6, где это лежит и как собрать заново.

## Состав

| Файл | Что | Где |
|---|---|---|
| `Rubezh.apk` | сборка для Meta Quest 2 (Android, Vulkan, IL2CPP ARM64, OpenXR) | GitHub Releases, релиз спринта |
| `Rubezh.unitypackage` | всё `Assets/` проекта: сцена, код, модели, звук, настройки URP и XR | GitHub Releases |
| `rubezh-gameplay-editor.mp4` | видео сквозного прогона упражнения в редакторе, 42 с | GitHub Releases |
| `rubezh-MN-sprint-0K.zip` | весь проект с развёрнутым Git LFS (как в `stage/`) | GitHub Releases, `stage/` |

## APK

Сборка — README, шаг 6; настройки — `docs/decisions.md`, Р-06. Установка на шлем в режиме разработчика:

```bash
adb install -r Rubezh.apk
```

или Meta Quest Developer Hub → Device Manager → Apps → перетащить APK. В шлеме: Библиотека →
Неизвестные источники → «Рубеж».

## Unity Package

Assets → Export Package → вся папка `Assets/` с вложенными. Пакеты из `Packages/manifest.json` в него
не входят: проект, в который импортируют, должен быть на Unity 6000.3.24f1 с URP 17.3, XR Interaction
Toolkit 3.6.1, OpenXR 1.18.0 и Input System (README, таблица версий).

## Видео

Задача просит запись из шлема через Meta Quest Developer Hub (трансляция и запись, звук игры без
микрофона). Шлема у команды пока нет, поэтому в релизе — **видео сквозного прогона в редакторе**:
тот же `FullRunTests`, который проходит всё упражнение руками XRI от шкафа до шкафа, снят камерой на
высоте глаз над XR Origin, 1280×720, 30 кадров в секунду игрового времени, без звука. Показывает весь
путь: шкаф, осмотр, СИЗ, снаряжение магазина, рубеж, стрельбу с задержкой, разряжание, сдачу и экран
результата «Зачёт 100».

Как переснять:

1. Rubezh → Видео прогона → Записывать кадры в Temp/video.
2. Test Runner → PlayMode → `FullRunTests` → Run.
3. Rubezh → Видео прогона → Не записывать.
4. `.\tools\video\encode.ps1 -Frames Temp\video -Out Builds\rubezh-gameplay.mp4` (нужен ffmpeg).

Запись из шлема через MQDH добавляется в тот же релиз, когда будет шлем: Device Manager → Cast →
Record, файлы — в папке Videos на шлеме, комментарий накладывается отдельно.

## Папка курса

Ссылки на папку курса у команды нет в репозитории: файлы из релиза загружает Никита вручную,
ссылку на релиз — в отчёт по вехе.
