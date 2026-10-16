# Производительность

Цели на Quest 2: **стабильные 72 Гц**, до **150 тыс. треугольников** и до **150 draw calls** в кадре.
Замер на шлеме — OVR Metrics Tool в пресете Basic (Advanced сам нагружает шлем), собранный APK.
Оверлей:

```bash
adb shell am broadcast -n com.oculus.ovrmonitormetricsservice/.SettingsBroadcastReceiver -a com.oculus.ovrmonitormetricsservice.ENABLE_OVERLAY
```

## Замеры

| Дата | Коммит | Сцена | Где | Устройство | FPS | Треугольники | Draw calls (batches) | SetPass | Кто |
|---|---|---|---|---|---|---|---|---|---|
| 16.10 | `be5897f` | Main.unity, блокинг `NG-04` | A1, взгляд на шкаф | редактор 6000.3.16f1, XR Interaction Simulator | — (кадр 1,4 мс CPU) | 18 400 | 32 (30) | 7 | Никита |
| 16.10 | `be5897f` | Main.unity, блокинг `NG-04` | A3, взгляд в коридор | редактор 6000.3.16f1, XR Interaction Simulator | — (кадр 1,2 мс CPU) | 18 450 | 36 (34) | 7 | Никита |
| — | — | Main.unity | A3 при стрельбе | **Quest 2, APK, OVR Metrics Basic** | **ждёт** | | | | Никита |

Числа редактора — статистика Game view (`UnityStats`) в Play Mode, моно-рендер одним видом. На шлеме
стерео Instancing рисует оба глаза одним проходом, треугольников в кадре примерно столько же, но
FPS по редактору не оценивается — для этого нужна строка Quest 2.

Около 15 тыс. из 18,4 тыс. треугольников дают модели контроллеров из Starter Assets — блок-модели
окружения, оружия и снаряжения занимают меньше 3 тыс. Запас до бюджета большой: финальные модели
(до ~25 тыс. треугольников по бюджетам `docs/art-pipeline.md` §4) в него укладываются.

## Решения

- Динамическое разрешение OpenXR — `NG-21`.
- Свет запечённый, Static Batching, SRP Batcher — `NG-17`.
