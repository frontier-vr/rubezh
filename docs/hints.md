# Правила подсказок

По этим правилам директор упражнения (`VP-12`) решает, что и когда подсказать: подсветка объекта
(`VP-21`) и фраза у контроллера (`NG-32`). Фразы — в [`script.md`](script.md) §3 по ID.
Машиночитаемая копия — `Assets/_Rubezh/Data/Hints/hint_rules.ru.json` (генерируется из этого файла:
`python tools/docs/script_to_json.py`).

## 1. Общие правила

- Подсказка включается только после **5 с бездействия** (`RulesConfig`): взрослый новичок не терпит
  навязчивой опеки. О нарушениях заранее не предупреждаем (design-doc §3.1).
- Таймер бездействия сбрасывают: захват, отпускание, событие сокета, любая `WeaponCommand`,
  `PhaseChanged`, телепорт.
- Подсказка снимается действием пользователя: следующим тем же сбросом таймера.
- В фазе берётся **первая сверху строка, чьё условие выполнено**. Если нужный объект у другого
  якоря, сначала срабатывает строка с телепортом.
- В `Fire` подсказка активна только до первого выстрела. После `DryFire` — пауза 5 с, затем
  подсказка на затвор (`hint_clear_jam`).
- В `Done` и `Failed` подсказок нет.
- **Цвет подсветки один на всё приложение** — `#1FB6C9` ([`palette.md`](palette.md)): не жёлтый
  разметки, не красный стопа, не белый контуров мест и не зелёный метки финиша. Подсветка пульсирует,
  метка финиша и контуры — нет.

## 2. Таблица правил

Условия читаются по снимку мира директора (`ExerciseFacts`) и якорю пользователя.
Подсветка — ключ объекта в сцене (§3).

| Шаг | Фаза | Условие | Код условия | Подсветка | Фраза (ID) |
|---|---|---|---|---|---|
| 1. Получение | `Receive` | не у A1 | `not_at:A1` | `Anchor_A1` | `hint_teleport_a1` |
| 1. Получение | `Receive` | у A1 | `at:A1` | `Pistol` | `hint_take_pistol` |
| 2. Осмотр | `Inspect` | магазин в оружии | `mag_in_weapon` | `MagRelease` | `hint_eject_magazine` |
| 2. Осмотр | `Inspect` | магазин не на полке осмотра | `mag_not_on:InspectShelf` | `InspectShelf` | `hint_put_magazine_shelf` |
| 2. Осмотр | `Inspect` | затвор не на задержке | `slide_not_locked` | `Slide` | `hint_slide_lock` |
| 3. СИЗ | `Ppe` | не у A1 | `not_at:A1` | `Anchor_A1` | `hint_teleport_a1` |
| 3. СИЗ | `Ppe` | наушники не надеты | `no_earmuffs` | `Earmuffs` | `hint_take_earmuffs` |
| 3. СИЗ | `Ppe` | очки не надеты | `no_glasses` | `Glasses` | `hint_take_glasses` |
| 4. Снаряжение | `LoadMagazine` | не у A2 | `not_at:A2` | `Anchor_A2` | `hint_teleport_a2` |
| 4. Снаряжение | `LoadMagazine` | магазин не на столе | `mag_not_on:PrepTable` | `PlaceMagazineTable` | `hint_magazine_table` |
| 4. Снаряжение | `LoadMagazine` | патрона в руке нет | `no_round_in_hand` | `AmmoTray` | `hint_take_round` |
| 4. Снаряжение | `LoadMagazine` | патрон в руке | `round_in_hand` | `Magazine` | `hint_round_to_magazine` |
| 5. Рубеж | `ToLine` | не у A3 | `not_at:A3` | `Anchor_A3` | `hint_teleport_a3` |
| 5. Рубеж | `ToLine` | пистолет не на стойке | `pistol_not_on:LineStand` | `PlacePistolStand` | `hint_pistol_to_stand` |
| 5. Рубеж | `ToLine` | магазин не на стойке | `mag_not_on:LineStand` | `PlaceMagazineStand` | `hint_magazine_to_stand` |
| 5. Рубеж | `ToLine` | мишень не подана | `target_not_supplied` | `TargetButton` | `hint_target_button` |
| 6. Стрельба | `Load` | магазин не в оружии | `mag_not_in_weapon` | `Magazine` | `hint_insert_magazine` |
| 6. Стрельба | `Fire` | патронник пуст и задержки нет | `chamber_empty_no_jam` | `Slide` | `hint_chamber` |
| 6. Стрельба | `Fire` | предохранитель включён | `safety_on` | `SafetyLever` | `hint_safety_off` |
| 6. Стрельба | `Fire` | не было ни одного выстрела | `no_shots_yet` | `TargetPlane` | `hint_fire` |
| 7. Задержка | `Fire` | осечка, прошло 5 с после `DryFire` | `jam_wait_elapsed` | `Slide` | `hint_clear_jam` |
| 8. Разряжание | `Cease` | предохранитель выключен | `safety_off` | `SafetyLever` | `hint_safety_on` |
| 8. Разряжание | `Unload` | магазин в оружии | `mag_in_weapon` | `MagRelease` | `hint_unload_magazine` |
| 8. Разряжание | `Unload` | затвор не на задержке или патронник не пуст | `not_clear` | `Slide` | `hint_unload_slide` |
| 8. Разряжание | `ShowClear` | пистолет или магазин не на стойке | `pistol_or_mag_not_on:LineStand` | `PlacePistolStand` | `hint_show_clear` |
| 9. Сдача | `Return` | не у A1 | `not_at:A1` | `Anchor_A1` | `hint_teleport_a1` |
| 9. Сдача | `Return` | у A1 | `at:A1` | `WeaponSlot` | `hint_return_cabinet` |

## 3. Объекты подсветки

| Ключ | Что | Где |
|---|---|---|
| `Anchor_A1`, `Anchor_A2`, `Anchor_A3` | метки якорей | `layout.md` §4 |
| `Pistol`, `Slide`, `SafetyLever`, `MagRelease` | пистолет и его части | §5.2 ТЗ, `MM-05` |
| `Magazine` | магазин | `MM-06` |
| `InspectShelf` | места осмотра O-08 | `layout.md` §3 |
| `PlaceMagazineTable` | место магазина на столе O-19 | |
| `AmmoTray` | лоток с патронами O-18 | |
| `Earmuffs`, `Glasses` | СИЗ на полке O-10, O-12 | |
| `PlacePistolStand`, `PlaceMagazineStand` | места на стойке O-27, O-28 | |
| `TargetButton` | кнопка пульта O-25 | |
| `TargetPlane` | мишень | |
| `WeaponSlot` | гнездо шкафа O-05 | |

## 4. Онбординг

Четыре выноски у контроллера, каждая один раз за сессию (`docs/user-flow.md` §3, `NG-32`).

| ID | Когда | Текст |
|---|---|---|
| `onb_teleport` | старт сцены у A1 | Стик вперёд: телепорт на жёлтый круг |
| `onb_grab` | первая наводка руки на предмет | Grip: взять предмет |
| `onb_pistol_buttons` | пистолет впервые в руке | {Eject}: выпустить магазин · {Safety}: предохранитель |
| `onb_slide` | впервые нужна работа затвором | Grip на затворе: отвести затвор |

## 5. Названия нарушений

Короткие нейтральные названия для замечания у контроллера и экрана результата — все 13 `Violation`.

| `Violation` | Название |
|---|---|
| `MuzzleOutOfSector` | Ствол вне сектора |
| `FingerOnTrigger` | Палец на спуске |
| `LookIntoMuzzle` | Взгляд в ствол |
| `NoEarmuffs` | Без наушников на рубеже |
| `NoGlasses` | Без очков на рубеже |
| `StepOrder` | Нарушен порядок шагов |
| `IncompleteInspection` | Осмотр не завершён |
| `ShotBeforeFire` | Выстрел до команды «Огонь» |
| `ShotAfterCease` | Выстрел после команды «Стой» |
| `ShotOutOfSector` | Выстрел вне сектора |
| `ReturnedLoaded` | Сдача неразряженного оружия |
| `CasingLeft` | Гильзы вне гильзоуловителя |
| `JamMishandled` | Задержка устранена раньше 5 с |
