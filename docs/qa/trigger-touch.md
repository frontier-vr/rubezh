# Касание спускового крючка (NG-05)

Детектор пальца на спуске (`VP-06`) читает `ITriggerInput`: `PressValue` (0..1) и `IsTouched`.
Реализация — `Rubezh.XR.TriggerInput`, по компоненту на руку.

## Источники

| Что | Привязка | Тип |
|---|---|---|
| нажатие | `<XRController>{RightHand}/trigger` (левая — `{LeftHand}`) | ось 0..1 |
| касание | `<OculusTouchController>{RightHand}/triggerTouched` | кнопка, OpenXR `/input/trigger/touch` |

Контрол `triggerTouched` есть в профиле Oculus Touch плагина OpenXR 1.18.0
(`OculusTouchControllerProfile`, проверено по исходнику пакета). Читается через
`XRInputValueReader` / `XRInputButtonReader` (`TryReadValue`, `ReadIsPerformed`).

## Варианты и что выбрано

`TriggerInput.touchSource`:

| Режим | `IsTouched` |
|---|---|
| `Auto` (по умолчанию) | если датчик привязался к устройству — касание по датчику **или** нажатие выше порога; если датчика нет — только по порогу |
| `Sensor` | только датчик |
| `Threshold` | только порог: `PressValue > RulesConfig.touchThreshold` (0,05, ниже порога выстрела) |

«Датчик есть» = действие касания привязалось хотя бы к одному контролу устройства.
Нажатие выше порога в режиме `Auto` тоже считается касанием: нажать спуск, не коснувшись его,
нельзя, а датчик может моргнуть.

## Проверки

| Где | Дата | Сборка | Результат |
|---|---|---|---|
| EditMode, `TriggerTouchTests` | 16.10 | `dev` | 10 случаев правила касания зелёные |
| XR Interaction Simulator, Play Mode | 16.10 | `dev` | нажатие привязалось к `/XRSimulatedController1/trigger`, датчика у симулятора нет → `Auto` работает по порогу |
| Аудиторный Quest 2, Input Debugger | — | — | **не проверено**: нужен шлем |

## Как проверить на Quest 2

1. Собрать APK из `dev`, запустить на шлеме с подключением к редактору
   (Window → Analysis → Input Debugger, Remote Devices).
2. В Input Debugger открыть `OculusTouchControllerOpenXR` правой руки, найти `triggerTouched`.
3. Положить палец на спуск, не нажимая: значение должно стать 1. Снять палец — 0.
4. Записать результат в таблицу выше. Если значение не приходит, перевести `TriggerInput` на
   обеих руках в режим `Threshold` и подобрать `touchThreshold` в `RulesConfig`.
