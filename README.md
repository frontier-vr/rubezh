# Рубеж

VR-тренажёр безопасного обращения с оружием в тире для Meta Quest 2.

Пользователь проходит полный цикл упражнения из девяти шагов: получение оружия, контрольный
осмотр, наушники и очки, снаряжение магазина, выход на рубеж, стрельба, устранение задержки,
разряжание и сдача. Параллельно всем шагам система следит за безопасностью: куда смотрит ствол,
лежит ли палец на спуске, не заглядывает ли пользователь в ствол. Грубое нарушение
останавливает упражнение, в конце — балл, вердикт и разбор нарушений.

Мишень только спортивная круговая, людей в сцене нет: нарушение считается по сектору, а не
по наведению на человека.

## Стек

| Что | Версия |
|---|---|
| Unity | **6000.3.24f1** с модулем Android Build Support (SDK, NDK, OpenJDK) |
| Рендер | URP 17.3.0, HDR и тени основного света выключены |
| XR | XR Interaction Toolkit 3.6.1, OpenXR 1.18.0 (Meta Quest Support, профиль Oculus Touch) |
| Ввод | Input System |
| Модели | Blender 5.1.2, экспорт FBX по `docs/art-pipeline.md` |
| Шлем | Meta Quest 2, цель 72 Гц |

Пакет Unity OpenXR: Meta не ставим: он тянет AR Foundation и Composition Layers, нужные только для MR.

## Как начать

1. Установить Unity **6000.3.24f1** через Unity Hub с модулем Android Build Support
   (вместе с Android SDK & NDK Tools и OpenJDK). Если Unity предлагает обновить проект
   до другой версии, отказываемся и ставим нужную.
2. Один раз на компьютере: `git lfs install`. Бинарники (FBX, `.blend`, текстуры, звук)
   лежат в Git LFS (`.gitattributes`).
3. Склонировать репозиторий и задать почту из GitHub → Settings → Emails, иначе коммиты не
   попадут в график вклада:
   ```bash
   git clone https://github.com/frontier-vr/rubezh.git
   cd rubezh
   git config user.name "Имя Фамилия"
   git config user.email "<почта из GitHub>"
   ```
4. Открыть папку в Unity Hub → Add project from disk. Главная сцена:
   `Assets/_Rubezh/Scenes/Main.unity`. В редакторе без шлема работает XR Interaction Simulator.
5. Тесты: Window → General → Test Runner → EditMode → Run All.

## Как мы работаем

- **Задачи** — Issues с ID по роли (`NG`, `VP`, `MM`, `YG`), метки приоритета `P0`–`P3`,
  вехи M1–M7. Доски: [Код и сборка](https://github.com/orgs/frontier-vr/projects/1),
  [Модели и UX](https://github.com/orgs/frontier-vr/projects/2).
- **Спринты** по две недели, поле Sprint на досках.
- **Одна задача — одна ветка — один PR.** Имя ветки указано в issue:
  `<имя>/<ID>-<кратко>`, например `nikita/NG-03-teleport`.
- **PR идут в `dev`**, в конце спринта `dev` сливается в `main` (`docs/decisions.md`, Р-02).
- **Только merge-коммит**: личные коммиты попадают в историю со своими авторами (Р-01).
- **Коммиты** по Conventional Commits с ID задачи в конце:
  `feat(weapon): автомат состояний IWeapon (VP-03)`, `docs(team): добавить Влада (VP-01)`.
- **Ревью** по `.github/CODEOWNERS`: у каждого пути два владельца, свой PR не одобряешь сам.
- **`Main.unity` правит только Никита** (Р-03). Остальные работают в своих песочницах
  `Scenes/Sandbox/Sandbox_<Имя>.unity` и отдают префабы.
- Перед PR пройтись по чек-листу «Перед PR» из issue (он же в шаблоне PR).

## Правила проекта, которые легко нарушить

- Метры: 1 единица Unity = 1 м, у моделей в Unity scale (1,1,1).
- Прозрачности в материалах нет, экранных эффектов нет (ТЗ §7.3): Quest 2 их не тянет.
- Весь код в asmdef: `Rubezh.Core` (чистый C#, No Engine References) ←
  `Rubezh.Gameplay` ← `Rubezh.XR` / `Rubezh.UI`. Тестовые сборки не видят `Assembly-CSharp`.
- Все числа правил (пороги, штрафы, таймеры) — в `RulesConfig`, не в коде.
- Ни `FindObjectOfType`, ни `Debug.Log` в `Update`.
- Заимствованные модели, текстуры и звуки — строка в `CREDITS.md`.

## Структура

```
Assets/_Rubezh/
├─ Art/            Source (*.blend), Weapon, Equipment, Target, Environment, Atlas
├─ Audio/          Mixer, SFX, Voice, Ambience
├─ Code/           Core, Gameplay, XR, UI (по asmdef на папку)
├─ Data/           Rules (RulesConfig), Hints (фразы и подсказки)
├─ Prefabs/        Art (визуальные), Interactive (с XRI)
├─ Scenes/         Main.unity, Sandbox/
├─ Settings/       URP Asset и Renderer
└─ Tests/          EditMode, PlayMode
docs/              план проекта, путь пользователя, планировка, QA
```

## Документация

| Файл | О чём |
|---|---|
| [`docs/team.md`](docs/team.md) | кто в команде и за что отвечает |
| [`docs/decisions.md`](docs/decisions.md) | решения команды по репозиторию и процессу |
| [`docs/mcp.md`](docs/mcp.md) | MCP для Claude Code: Unity и Blender |

## Команда

Никита Григорьев, Владислав Пахомов, Мария Махмудова, Юлия — подробности в [`docs/team.md`](docs/team.md).
