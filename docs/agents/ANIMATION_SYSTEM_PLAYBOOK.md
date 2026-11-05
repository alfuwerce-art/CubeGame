# Один гуманоид: Alpha oracle → action clips → Unity controller

**S05A-2; Codex продолжает Claude от `3d4a2fb`, включая `43a1ba6`.**
**Проверено 2026-11-06:** 93 action ID / 60 states, 8 `BLOCKED`; Python 178, EditMode 29; все 93 действия PlayMode 30/60/120 Гц PASS. Подробные артефакты и границы доказательств — в отчёте.
Рабочая ветка `codex/s05a2-claude-continuation`. S05B не разрешён.
Отчёт: [S05A2_CLAUDE_CONTINUATION_REPORT](../reports/S05A2_CLAUDE_CONTINUATION_REPORT.md).
Исследование: [ALPHA_ANIMATION_SYSTEM](../research/alpha2013/ALPHA_ANIMATION_SYSTEM.md).

## 1. Источники и статусы

- `TRANSFERRED_FROM_ALPHA`: прежние клипы из живых GPU-матриц. Их исходники и метод сохранены.
- `EMULATED_ALPHA_CODE`: новые action-клипы; исполняются исходные байты resolver/drawModel существующим строгим эмулятором Claude.
- `RECONSTRUCTED_FROM_EVIDENCE`: параметрическая ходьба и отдельный слой оружия на спине.
- `BLOCKED`: неподтверждённая длительность, отсутствующая/дополнительная часть, неподдержанный вызов, статичная поза, неполное восстановление.
- `UNITY_TRANSITION`: переходы entry/recovery/exit в контроллере. Это политика Unity-пилота, а не извлечённые переходы Alpha.

Положительный `combat_getAttackTiming` задаёт **окно исследования renderer**, не доказывает игровую доступность способности, её ввод или условие завершения в игровом update. Такие правила отдельно не восстанавливались. Не переименовывать ID в догаданные способности.

## 2. Бейкинг

Из корня единственного репозитория, установленным Python 3.13:

```powershell
python -m tools.alpha_models.universal.action_baker --job tools/alpha_models/universal/jobs/human_0x02_actions.json --actions 57,58,67 --out .scratch/animsys/human_actions
```

Для реестра действий **того же** гуманоида:

```powershell
python -m tools.alpha_models.universal.action_baker --job tools/alpha_models/universal/jobs/human_0x02_actions.json --survey .scratch/animsys/survey_human.json --workers 4 --out .scratch/animsys/human_actions_extended
```

`--workers` — вычислительные процессы одного инструмента, с отдельным файлом на действие; редактор и другие агенты одновременно проект не меняют. Инструмент ограничен kind `0x02` и двуручным оружием 15/16/17; новых существ не обрабатывает.

В `actions.json` каждый ID получает state, родные timing-функции, статус, точную причину отказа либо хеш клипа и проверки. `BAKE_RESULT=PASS` означает завершённый реестр; `BLOCKED` записи остаются видимыми. Записи ключей не пропускаются. Нет подтверждённого тайминга → отказ, без прежней подстановки 1000 мс.

`job.action_manifest` и `job.clips` должны указывать на выбранный результат. Не импортировать незавершённый реестр. Поддержанные клипы обязательны **все**: отсутствие, изменение SHA256, несовпадение вида/семейства/seed/оружия приводят к FAIL. `weapon_reference.json` — локальная независимая проверка C#-слоя оружия относительно oracle.

Все выходы Alpha остаются внутри `.scratch`; запрет публикации сохраняется.

## 3. Unity 6000.3.27f2

Использовать существующий изолированный проект; сначала убедиться, что он закрыт. Не создавать копий/worktree.

```powershell
& tools/alpha_models/transfer/run_transfer.ps1 -Project D:/Projects/CubeGame/.scratch/s05a2/unity/AlphaTransfer -UniversalJob tools/alpha_models/universal/jobs/human_0x02_actions.json -CaptureController
```

Если Python отсутствует в PATH, передать `-Python <полный путь python.exe>`. Версию Unity не менять.
Скрипт сохраняет предыдущий `TransferOutput` в историю; PASS требует нового отчёта и exit 0. Ошибки компиляции/самотестов/сборки прерывают зависимые шаги.

| Артефакт `TransferOutput/` | Обязательное доказательство |
|---|---|
| `selftest.txt` | EditMode-самотесты; среди них отказы контроллера и часы 30/60/120 Гц |
| `transfer_report.txt` | геометрия, все ключи/части roundtrip ≤ 0.001, seam, хват трёх двуручных атак |
| `playmode.txt` | отдельная регрессия ходьбы; отсутствие старого имени атаки здесь не подтверждает action-контроллер |
| `paramwalk.txt` | Python↔C# golden, живой capture ходьбы, отдельная `WEAPON_ORACLE` проверка |
| `locomotion_playmode.txt` | реальные кадры driver/boost/amplitude |
| `controller_playmode.txt` | **все** required actions: idle→move→stop→entry→action→recovery→exit→idle, движение, таймаут, busy/repeat, позы и стыки |
| `controller_frames/`, `.tsv` | при `-CaptureController`: реальные кадры первого полного цикла контроллера, не только прямые сэмплы клипа |

`AlphaAnimationController` — единственный владелец часов: driver и clip player выключены как автономные компоненты, контроллер вызывает их явно. При запросе действия на ходу сначала останавливается locomotion. Амплитуда входа/остановки — политика пилота `PLACEHOLDER_NOT_ALPHA`.

Для локальной визуальной передачи (Windows PowerShell, без установки библиотек):

```powershell
powershell.exe -NoProfile -STA -File tools/alpha_models/transfer/controller_preview.ps1
```

Выход — `.scratch/animsys/visuals/controller_preview.gif` и `controller_contact_sheet.png`; источник только реальные `-CaptureController` кадры, статусы подписаны. Публикация изображений/клипов Alpha в Git запрещена.

Сами action-клипы не редактируются ради стыков. Их единый якорь оружия — центр модели; переход рука↔спина целиком присутствует в J. Проверка хвата использует **реальную точку сетки Z=4**, а не ошибочно центр модели/представительский якорь.

Вращающийся меч интерполируется вокруг этого хвата, чтобы не открывать ложный зазор между точными ключами. Дополнительно проверяется конечный отрезок рукояти, полученный из геометрии, а не бесконечная ось. Для fixed-back action-клипов размеры оружия должны совпадать с размерами бейкинга; иной размер требует явного адаптера, импорт откажет. Тело по-прежнему использует прежний независимый от размеров контракт.

## 4. Независимая проверка исходного механизма

```powershell
python -m tools.alpha_models.universal.validate_oracle --capture .scratch/animsys/cap/survey_actions.jsonl --capture .scratch/animsys/full_npc.jsonl --out .scratch/animsys/oracle_validation.json
python -m pytest tools/alpha_models/tests -q -p no:cacheprovider
& tools/alpha_models/transfer/run_transfer.ps1 -Test
```

`validate_oracle` воспроизводит полные **pre-draw** входы и родной dt каждого живого кадра, сравнивает resolver, набор draw sites и исходные upload-матрицы после удаления placement. Порог `max_abs ≤ 0.0001`. Подтверждены только реально встреченные state/action, не весь набор из 101 ID.

Для дальних NPC подаётся исходное fixed16 смещение камеры, восстановленное из placement и координат существа. Иначе искусственное центрирование к нулю меняет округления f32. Ключи поз в oracle не подставляются. Финальный replay — 3115 кадров, max_abs 0; наблюдались action 0/57/59/60. Статус новых клипов остаётся `EMULATED_ALPHA_CODE`.

## 5. Продолжение DeepSeek

Изучать `BLOCKED` записи по конкретной причине. Смена/скрытие экипировки требует отдельного контракта видимости/наборов частей, а не удаления проверок. Игровые lifetimes и условия прерывания требуют исследования update-кода. Новые персонажи/массовая конвертация — только после решения продюсера о S05B. Художественную приёмку не объявлять.
