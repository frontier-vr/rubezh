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

- **Динамическое разрешение OpenXR — включено** (`NG-21`, 16.10). Подробности ниже.

### Динамическое разрешение (`NG-21`)

**Проверено, что есть в нашей связке.** В пакете Unity OpenXR 1.18 (Unity 6000.3, URP 17, Vulkan)
есть своя функция **Automatic Viewport Dynamic Resolution** (`AutomaticDynamicResolutionFeature`,
группа Android, `All Features`). Она живёт в самом пакете OpenXR: сама запрашивает у рантайма
расширение `XR_META_recommended_layer_resolution` (Quest) или `XR_ANDROID_recommended_resolution`
и меняет область вывода каждый кадр по рекомендации рантайма. Пакет Unity OpenXR: Meta для неё не
нужен — его в проект не ставим (тянет AR Foundation и Composition Layers, ТЗ). Раньше, до
Unity 6.3, динамическое разрешение на OpenXR давали только Meta XR SDK (`OVRManager`) или пакет
OpenXR: Meta — обе дороги нам закрыты.

**Решение: включить сейчас**, как советует Meta, а не перед сдачей.
- `Project Settings → XR Plug-in Management → OpenXR → Android → All Features →
  Automatic Viewport Dynamic Resolution` включена; Min Resolution Scalar **0,7**, Max **1,0**.
  Ниже 0,7 текст табличек и табло на 2 м мылится; 0,5 — нижний предел движка.
- На `Main Camera` включён `Allow Dynamic Resolution` (рекомендация Unity для URP).
- Режим совместимости URP не используем — в нём функция не работает.

**Что остаётся проверить на шлеме** (строка замера выше, ждёт APK `NG-01`): что рантайм Quest 2
сообщает поддержку расширения (`AutomaticDynamicResolutionFeature.IsAutomaticDynamicResolutionScalingSupported()`)
и что в OVR Metrics виден масштаб ниже 1,0 под нагрузкой при стрельбе. Если расширения нет,
функция ничего не делает и кадр рендерится в полном разрешении — сборка не ломается. Запасной путь
без расширения — `XRSettings.renderViewportScale` из своего скрипта по времени кадра; его
делаем, только если замер покажет просадку ниже 72 Гц.
- Свет запечённый, Static Batching, SRP Batcher — `NG-17`.
