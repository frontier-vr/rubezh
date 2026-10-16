# Заимствованное

Всё, что сделано не командой и лежит в проекте: модели, текстуры, звуки, шрифты, код.
У каждой строки — автор, источник, лицензия и где лежит в репозитории. Берём только CC0 и
CC-BY (или лицензии, разрешающие такое использование). Референсы, которые в проект не
попадают, сюда не пишем: они в `docs/refs/`.

## Код и пакеты

| Что | Автор | Лицензия | Где |
|---|---|---|---|
| XR Interaction Toolkit Starter Assets, XR Interaction Simulator (сэмплы) | Unity Technologies | Unity Companion License | `Assets/Samples/XR Interaction Toolkit/` |
| TextMesh Pro Essentials | Unity Technologies | Unity Companion License | `Assets/TextMesh Pro/` |
| MCP for Unity | CoplayDev | MIT | `Packages/manifest.json` (только редактор, в APK не попадает) |

## Шрифты

| Что | Автор | Лицензия | Где |
|---|---|---|---|
| Liberation Sans | Red Hat / Liberation Fonts | SIL Open Font License 1.1 | `Assets/TextMesh Pro/Fonts/` |

## Модели

| Что | Автор | Источник | Лицензия | Где |
|---|---|---|---|---|

## Текстуры

| Что | Автор | Источник | Лицензия | Где |
|---|---|---|---|---|
| мудборд: 22 превью материалов, HDRI и моделей (`docs/moodboard/ph_*.png`) | Poly Haven | [polyhaven.com](https://polyhaven.com), ссылки в `moodboard.json` | CC0 | `docs/moodboard/` |
| мудборд: галерея тира: скамьи, ограждения, верхний свет (`docs/moodboard/wc_01.jpg`) | Jakub Sochor | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:25m_shooting_range_Pilsen.jpg) | CC BY 3.0 | `docs/moodboard/` |
| мудборд: спортивный тир 10 м: жёлтая линия огня на полу (`docs/moodboard/wc_02.jpg`) | Nishantp10 | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:10_metre_indoor_range_by_Gun_For_Glory_Shooting_Academy_Jabalpur.jpg) | CC BY-SA 4.0 | `docs/moodboard/` |
| мудборд: рубеж за перегородкой, табло результатов (`docs/moodboard/wc_04.jpg`) | Uri Tours | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Meari_Shooting_Range_(13204953155).jpg) | CC BY-SA 2.0 | `docs/moodboard/` |
| мудборд: табличка безопасности на перфорированном металле (`docs/moodboard/wc_06.jpg`) | NIOSH | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Warning_sign_posted_indoor_firing_range_to_prevent_Lead_exposure.jpg) | Public domain | `docs/moodboard/` |
| атлас оружия и снаряжения 1024 (`Weapon_Atlas.png`) | команда «Рубеж» (Маша) — генерация по палитре, без чужих изображений | `tools/art/palette_atlas.py` | CC0 | `Assets/_Rubezh/Art/Atlas/` |
| атлас окружения 2048 и карта эмиссии (`Env_Atlas*.png`) | команда «Рубеж» (Юля) — генерация по палитре, без чужих изображений | `tools/art/palette_atlas.py` | CC0 | `Assets/_Rubezh/Art/Atlas/` |

## Звуки

| Что | Автор | Источник | Лицензия | Где |
|---|---|---|---|---|
| выстрел (`Pistol_Shot.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| отвод затвора (`Pistol_SlideBack.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| возврат затвора (`Pistol_SlideForward.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| щелчок вставки магазина (`Magazine_Insert.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| выпадение магазина (`Magazine_Drop.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| сухой щелчок при осечке (`Pistol_DryFire.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| щелчок предохранителя (`Pistol_Safety.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| падение гильзы на бетон (`Casing_Concrete.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| щелчок патрона в магазине (`Round_Insert.wav`) | команда «Рубеж» (Маша) — синтез, без чужих записей | `tools/audio/sfx_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
| голос инструктора: 13 команд (`cmd_*.wav`), 25 подсказок (`hint_*.wav`), 5 причин стопа (`fail_*.wav`) | команда «Рубеж» (Юля) — синтез речи Windows, голос Microsoft Irina (ru-RU), без чужих записей | `tools/audio/voice_tts.ps1` из `script.ru.json` | временная озвучка: синтез средствами Windows; перед публичным релизом заменить живой записью (те же имена файлов) или проверить условия лицензии Windows на голос | `Assets/_Rubezh/Audio/Voice/` |
| фон тира — гул вентиляции и сети, петля 20 с (`Range_RoomTone.wav`) | команда «Рубеж» (Юля) — синтез, без чужих записей | `tools/audio/ambience_synth.py` | CC0 | `Assets/_Rubezh/Audio/Ambience/` |
| звуки перемещения: телепорт (`Locomotion_Teleport.wav`) и поворот щелчком (`Locomotion_SnapTurn.wav`) | команда «Рубеж» (Юля) — синтез, без чужих записей | `tools/audio/comfort_synth.py` | CC0 | `Assets/_Rubezh/Audio/SFX/` |
