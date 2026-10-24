# Темы курса в плане проекта

Как темы занятий ложатся на задачи Рубежа. На разделы ссылаются Issues.

## 1. VR-пакет: XR Interaction Toolkit

Рубеж построен на XRI 3.6.1 и сэмпле Starter Assets.

- **XR Origin** из Starter Assets (`XR Origin (XR Rig)`): Locomotion Mediator, Teleportation
  Provider, Snap Turn Provider. Остальные провайдеры рига выключены: Move, Grab Move, Climb,
  Climb Teleport, Gravity, Jump. Плавного хода нет (FR-02).
- **Телепорт только на якоря**: три `TeleportationAnchor` (A1–A3) с `teleportAnchorTransform`.
  `Teleportation Area` из демо-префаба `Teleportation Environment` в сцену не переносим.
- **Левый `ControllerInputActionManager`**: снять Smooth Motion Enabled, иначе телепорт с левого
  стика выключен (`SetEnabled(m_TeleportMode, !m_SmoothMotionEnabled)` в сэмпле).
- Разворот на 180° стиком назад оставляем; телепорт при ближнем хвате оставляем
  (Near-Far: enable teleport during near interaction). Jump на A выключен: A занята под магазин.
- **XR Interaction Simulator** включается в Project Settings → XR Plug-in Management → XR
  Interaction Toolkit → «Use XR Interaction Simulator in scenes» с «Instantiate In Editor Only».
  Префаб симулятора в `Main.unity` не кладём. Всё, что не про ощущения, сначала проверяем в
  симуляторе: гарнитур в аудитории две на все команды.
- **Взаимодействие**: `XRGrabInteractable` с Select Mode Multiple и `XRGeneralGrabTransformer`
  для пистолета, `XRSocketInteractor` для мест и магазина, `XRSimpleInteractable` +
  `XRPokeFilter` для кнопки пульта, собственный `IXRSelectFilter` для шкафа.

## 2. Ввод и жесты

- Управление контроллерами Oculus Touch через профиль OpenXR. Hand tracking не используем:
  касание спускового крючка (ёмкостный датчик `triggerTouched`) — ключевой сигнал детектора
  пальца, у рук его нет.
- Чтение ввода через `XRInputValueReader` и `TryReadValue` (`NG-05`).
- Кнопки: A/X — выпустить магазин, B/Y — предохранитель, Grip — взять, Trigger — спуск.
  Левая рука зеркально (FR-05).

## 3. Passthrough и MR

Не используем: упражнение идёт в полностью виртуальном тире, а пакет Unity OpenXR: Meta,
нужный для passthrough, тянет AR Foundation и Composition Layers. Решение записано в README.

## 4. Графика, эффекты и комфорт под Quest 2

### 4.1 Оптимизация

- URP, HDR и тени реального времени выключены, свет запечённый: lightmaps для статики,
  light probes для подвижных объектов.
- Окружение Static, Static Batching включён, SRP Batcher включён. GPU Instancing не используем:
  в URP он работает только при выключенном SRP Batcher.
- Атласы: оружие 1024×1024 (`MM-13`), окружение 2048×2048 (`YG-12`). Материалы Unlit или
  Simple Lit, прозрачности нет (ТЗ §7.3).
- Бюджет: стабильные 72 Гц, до 150 тыс. треугольников и до 150 draw calls в кадре. Замеры —
  [`qa/perf.md`](qa/perf.md), OVR Metrics Tool в пресете Basic.

### 4.2 Постпроцессинг

Unity 6.3 даёт on-tile post-processing для автономных XR-шлемов: Renderer Feature «On Tile
Post Processing (Untethered XR)» при выключенном встроенном постпроцессинге на Renderer.
Поддерживает только Color grading, Vignette, Tonemapping, Dithering, Film Grain.
В Рубеже это кандидат для оттенка стёкол при надетых очках (Color Adjustments → Color Filter
по `IWearables.GlassesOn`). Задача COULD: оставляем, только если по замеру держатся 72 Гц
(`NG-30`). Opaque Texture и Depth Texture в URP Asset выключены.

### 4.3 Эффекты

Вспышка выстрела — непрозрачный меш до 100 треугольников с Unlit-материалом на пустышке
`Muzzle` (`MM-17`), а не частицы с альфой и не Bloom: Meta для Quest советует избегать
перекрывающейся альфа-геометрии и alpha test. Вспышку на 2–3 кадра включает та же подписка на
`Fired`, что даёт отдачу (`NG-15`). Пробоины — непрозрачные меши из пула (`VP-23`).

### 4.4 Комфорт перемещения

Телепорт и snap turn дают дезориентацию; Meta предлагает пространственный звук, «моргание»
и виньетку. Порядок проверки (`YG-16`): сначала без средств, затем короткий звук телепорта
(вешается событием `teleporting` у `TeleportationAnchor`), виньетка `TunnelingVignette` из
Starter Assets — только если звука мало: она рисует полупрозрачную геометрию на весь экран и
требует исключения из ТЗ §7.3 и замера 72 Гц. Итог — `docs/comfort.md`.

## 5. Звук

Микшер с группами SFX, Voice, Ambience. Наушники — переход на снапшот «Наушники» (фильтр низких
частот и ниже громкость на SFX), не exposed parameter из кода: такой параметр выпадает из
переходов снапшотов. Голос инструктора в Voice, наушники на него не действуют (`NG-10`, `VP-14`).

## 6. Тестирование

- EditMode-тесты на чистый C# в `Rubezh.Core`: автомат оружия, директор, осечка, балл.
  Время через `IClock`, случайность через генератор с зерном — тесты мгновенные и воспроизводимые.
- PlayMode дымовой тест главной сцены (`NG-18`).
- Ручной протокол в шлеме (`NG-19`), плейтест с другими командами (`NG-22`).
