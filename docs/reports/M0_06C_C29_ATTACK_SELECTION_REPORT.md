# M0-06C / C-29 — выбор атаки существ по Alpha (перенос, пакет DeepSeek)

**Дата:** 2026-11-09. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content`
(база `1e1920c`, каталог `D:\Projects\CubeGame-deepseek`, D88).
**STATUS: `READY_FOR_REVIEW`.** Не `ACCEPTED`: приёмка — только у продюсера.

## 1. Задача

HANDOFF Claude (журнал `docs/agents/AGENT_COORDINATION.txt`, запись 2026-11-09 22:25, п. 4b): «C-29 выбор
атаки + атаки NPC с оружием R-1/R-2». До этого игра брала **первую атаку библиотеки** вида — в
[M0_05A_COMBAT_SLICE_REPORT](M0_05A_COMBAT_SLICE_REPORT.md) это помечено `PLACEHOLDER_NOT_ALPHA`
(`CombatBehavior_chooseAttack` C-29 не перенесён).

Сделана первая, доказанная часть C-29 — **таблица выбора варианта атаки**
(`combat_selectAttackVariant`, `Cube.exe` `0x4452A0`). Часть «окно/тайминг атаки» (`chooseAttack`,
`combat_getAttackTiming`) не переносилась: она дублирует уже используемые клиповые тайминги Alpha.

## 2. Было → стало

| Что | Было | Стало |
|---|---|---|
| Выбор атаки существ | первая атака из `AvailableActions` (порядок библиотеки), `PLACEHOLDER_NOT_ALPHA` | `AlphaAttackSelection.SelectAttackId(...)` — таблица Alpha по оружию (тип/подтип), классу, специализации и биту чётности; библиотека только запасной путь с явной причиной `ACTION_C29_NOT_IN_LIBRARY` |
| Числа таблицы | не перенесены | генерируются из самого `Cube.exe` и байт-проверяются (`RESULT=PASS`, 31 проверка), коммитится только результат — адреса и числа, без ассетов Alpha |
| Состояние существа | не хранило оружие/класс/специализацию | 4 новых поля (`WeaponItemType/WeaponItemSubtype/ClassId/SpecialisationId`), **не входят в блоб `M0NPC|1`** — формат сейва не менялся |
| Проверки | — | 11 новых EditMode-тестов + два реальных дефекта, найденных ими (см. §5) |

## 3. Файлы

| Файл | Назначение |
|---|---|
| `tools/alpha_content/attack_selection.py` | разбор `Cube.exe 0x4452A0` (capstone), байт-проверки, генерация JSON-доказательства и C#-таблицы; `--check` — контроль дрейфа |
| `tools/alpha_content/data/attack_selection_evidence.json` | доказательство: sha256 бинарника, sha256 268 байт функции, 31 проверка байтов, декодированные правила, `partial`/`unknown` |
| `Assets/CubeGame/Content/Generated/AlphaAttackSelection.g.cs` | сгенерированные числа таблицы (в Git, как и остальные `Generated/*`) |
| `Assets/CubeGame/Features/Creatures/AlphaAttackSelection.cs` | чистый порт управляющего потока Alpha (без урона/HP/ресурсов) |
| `Assets/CubeGame/Features/Creatures/CreatureContracts.cs` | +4 несериализуемых поля `CreatureState` (C-29 входы) |
| `Assets/CubeGame/Features/AI/CreatureAi.cs` | C-29 вместо «первой атаки»; `LastSelectedAttack`, `TargetPointerParity` |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaAttackSelectionTests.cs` | 8 тестов таблицы + 3 теста интеграции ИИ |
| `docs/research/alpha2013/SPAWN_POPULATION_EVIDENCE.md` | факты по спавн-точкам мира и N-19 (что доказано, чего нет) |

## 4. Что доказывает таблица (кратко)

`Cube.exe` sha256 `84a7a132…30bf`, функция `0x4452A0`, 268 байт, sha256 `81822ebc…17bc`:

1. оружие (тип 3, подтип 6) → действие `0x17` (проверяется **до** класса и вида);
2. класс `3` или вид `0x75`/`0x56` → ветка класса/специализации: подтип `0x0B` → `0x2D`/`0x2E`,
   `0x0A` → `0x1F`/`0x21`, иначе `0x25`/`0x2B` (различие — `[creature+0x141] == 1`);
3. оружие (3, 5) → действие `5`;
4. оружие с подтипами `0x0F,0x10,0x11,0x05,0x0A,0x0B,0x12,0x08,0x06,0x07` → `0x42 − бит`;
   подтипы `0x05`, `0x06`, `0x0A`, `0x0B` до этой строки не доходят (порядок веток Alpha сохранён);
5. всё остальное (нет оружия, неизвестный подтип) → безоружная атака `3 + бит`.

## 5. Фактические прогоны

| Прогон | Команда | Результат |
|---|---|---|
| Доказательство/генерация | `python -m tools.alpha_content.attack_selection` | `ATTACK_SELECTION_RESULT=PASS checks=31 problems=[]` |
| Контроль дрейфа | `python -m tools.alpha_content.attack_selection --check` | `PASS … drift=[]` |
| Unity EditMode, адресный (новые + существующие тесты существ) | `Unity.exe -batchmode -projectPath D:\Projects\CubeGame-deepseek -runTests -testPlatform EditMode -testFilter CubeGame.Tests.Creatures.EditMode` | `total=46 passed=46 failed=0` — `AlphaOutput/c29_editmode3.xml` |
| Unity EditMode, весь проект | то же без фильтра | `total=446 passed=440 failed=0 skipped=6` (`Skipped:Ignored`) — `AlphaOutput/c29_full_editmode.xml` |
| Unity PlayMode, весь проект | `-testPlatform PlayMode` | `total=57 passed=57 failed=0` — `AlphaOutput/c29_full_playmode.xml` |
| Компиляция | логи обоих прогонов | ни одной строки `error CS` |

Первые два прогона были **красными** и это записано честно: 46/46 → 44/46 → 45/46 → 46/46.
Найденные тестами дефекты:
1. `CreatureAi` мог запросить у игры действие, которого нет в доказанном наборе вида (запрос
   «желаемой» атаки мимо `AvailableActions`) — исправлено: желаемая атака запрашивается только если
   она есть в наборе; иначе библиотека + причина `ACTION_C29_NOT_IN_LIBRARY <id>`.
2. Тест ожидал чётность для подтипов, которые в Alpha до этой ветки не доходят (`0x05`, `0x06`,
   `0x0A`, `0x0B`) — исправлено ожидание, порядок веток Alpha сохранён в коде и описании.

## 6. PARTIAL / UNKNOWN (не выдумывать)

- **Бит чётности** — `bit0` поля `[creature+0x70]`, того же поля, которое `chooseAttack` проверяет как
  «нет цели». Что именно переключает бит, **не установлено** (`PARTIAL`); игра передаёт `false`
  (чётная ветка: `3` без оружия, `0x42` с оружием) — это воспроизводит все наблюдавшиеся запуски.
- **Класс/специализация/оружие NPC** приходят из записи спавна мира (`R-1`), которая **не исполнена**.
  Пока у существа нет этих полей, таблица честно даёт безоружную атаку `3`.
- **Окно/тайминг** (`CombatBehavior_chooseAttack`, `combat_getAttackTiming`, порог `timing − windup`,
  recover 50…5000 мс) **не переносились**: игра уже берёт windup/actionEnd/recovery из клиповых
  таймингов Alpha (`AlphaActionTiming`). Если понадобится гейт ровно как в Alpha — отдельный пакет.
- **Ресурсы/стан/каст** (`[+0x128]`, `ability_getResourceCost`) — вне C-29-таблицы, остаются за Claude.
- **N-19** (таблица «уровень/биом → вид») по-прежнему **не закрыт** — см. §7.

## 7. Что это значит для пакета (a) «спавн по биомам»

Проверено по декомпилированному коду клиента (подробно — [SPAWN_POPULATION_EVIDENCE](../research/alpha2013/SPAWN_POPULATION_EVIDENCE.md)):
мир ставит спавн-точки типов `0x80…0x84` (`Chunk_generateObjects 0x5AF170`), типы 2/3/5 вызывают
`World_generateMonsterSpawnList`/`World_generateNpcSpawnList`; строки уровней и звёзды совпадают с N-18.
Но **смысл кодов в дескрипторах 0x118 и связь с биомом не установлены**: таблицу «биом → вид»
сейчас нельзя заполнить без исполнения генератора или рантайм-дампа дескрипторов.
Поэтому `IWorldPopulationSource` в этом пакете не реализован: пустая зона честнее выдуманного биома.

## 8. Handoff Claude

1. `CreatureState.WeaponItemType/WeaponItemSubtype/ClassId/SpecialisationId` — заполнять при появлении
   записи спавна (`SpawnDefinition`/R-1). `-1` = неизвестно (безоружная ветка).
2. `CreatureAi.LastSelectedAttack` — какой id выбрала таблица; причина успеха/фолбэка в
   `LastReason`: `ACTION_C29 <id>` / `ACTION_C29_NOT_IN_LIBRARY <desired> <id>`.
3. `TargetPointerParity` (по умолчанию `false`) — единственный `PARTIAL` вход; менять только с
   доказательством.
4. Блоб `M0NPC|1` и сейв не менялись: новые поля не сериализуются.
