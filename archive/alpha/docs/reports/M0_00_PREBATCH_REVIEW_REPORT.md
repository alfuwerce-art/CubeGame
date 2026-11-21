# M0-00 — предмассовое ревью и исправление конвейера (Claude)

**Дата:** 2026-11-09. **Исполнитель:** Claude Code. **Ветка:** `claude/m0-00-prebatch-review`.
**База:** `30f88a1` (D83, `dragon_0x65_v1`, утверждён продюсером) + merge `review/m0-00b-final-two-questions` @ `d07564c` (DeepSeek), без конфликтов.
**Статус:** `READY_FOR_REVIEW`, не `ACCEPTED`. Merge в `main` нет, M0-01+ не открыт, S02–S04 и `Assets/` не менялись.

## Итог

**`PREBATCH_REVIEW_PASS` (ограниченный)**. Конвейер готов к массовым партиям **B0–B3** (раздел 5): геометрия каталога, ходьба и покой, действия существ. После П1–П6 все проверки проходят повторным запуском.
Партии **B4–B5** (слой предметов в Unity, перенос таблиц систем в Core) — **`PREBATCH_BLOCKED`**: для них нужен новый исполнитель и решения продюсера (раздел 6). Это не дефекты кода, а отсутствующие этапы.

Порядок работ не меняется: SCAN → PLAN → VALIDATE → PILOT → BATCH → REVIEW, затем проверка игры в Unity. Формулировка D72 «сразу Unity-first» как разрешение пропустить BATCH **не используется**.

## 1. Проверка пакета DeepSeek `d07564c`

| Вопрос | Как проверено | Результат |
|---|---|---|
| `WeaponSubtype 0 = Sword` | 43 исходные проверки DeepSeek запущены повторно (`Cube.exe` SHA `84a7a132…30bf`, тот же в `D:\Games\Cube world`); отдельно прочитаны `item_weaponDamage 0x4c7f60` и item-layer | **Подтверждено.** Тип 3 / подтип 0 — обычное одноручное оружие: множитель ×4 (×2 только у 3/4/0xd, ×8 у дальнобойных/двуручных), атака 3/4, модель 899 `iron-sword1.cub`. Покупка игроком — PARTIAL |
| Зелья NPC 25% / 50% | Собственная дизассемблировка `0x42cb20…0x42cee7` (capstone) | **Подтверждено:** обе ветки берут собственный MaxHP NPC (`ecx=edi` перед `call 0x444db0`), константы 0.25/0.5. **Найдены пропущенные условия** — см. П4 |
| 43 проверки | `verify_subtype0_and_potion.py --source <скрипт DeepSeek>` | `VERIFY_TWO_QUESTIONS_RESULT=PASS checks=43 failed=0`, `REPLAY=PASS` |
| Качество 43 проверок | Чтение скрипта | 7 из 43 — синтетическая арифметика над локальной лямбдой, ещё 1 — тождество (268). Это не доказательства Alpha. Доказательная часть — 35 проверок + моя дизассемблировка |
| Пилоты M0 | Свежий прогон | 467/467 на `d07564c`; **478/478** после П4. Историческое 462 недействительно |

## 2. Найденные дефекты и исправления (согласованы продюсером, «было → стало»)

| № | Было | Стало | Файлы |
|---|---|---|---|
| **П1** | `d07564c` не содержит D83 (`dragon_0x65_v1`): ветки разошлись на `653b992`. Массовый перенос с этой базы перенёс бы дракона без утверждённой правки. На объединённом дереве падал `test_item_layer::test_committed_evidence_matches_the_executed_bytes` (пин `alpha_static.py`) | Ветка = D83 + `d07564c`. Evidence перегенерирован: `verification` и `contract` **идентичны** прежним, изменились только пины кода | merge-коммит, `tools/alpha_models/catalog/item_layer_evidence.json` |
| **П2** | `alpha_confirmed_tables.json`: подтип 0 = `Unknown0x00 / UNKNOWN` — противоречит выводу Sword=0 той же ветки | `{0, "Sword", DIRECT, iron-sword1.cub}` с адресами; `AlphaTables.g.cs` перегенерирован. Перечисления S04 (enum) **не тронуты** — вопрос M0-00D | `tools/m0_transfer/data/alpha_confirmed_tables.json`, `tools/m0_pilots/Generated/AlphaTables.g.cs` |
| **П3** | `build_transfer_registry.py --check` = FAIL: матрицу правили, реестр не перегенерировали. `sourceSha256` по сырым байтам — при `core.autocrlf=true` проверка всегда «устаревшая» | Хеш по тексту с LF (совпадает с blob в Git); реестр перегенерирован; `--check` PASS | `tools/m0_transfer/build_transfer_registry.py`, `registry/transfer_registry.json` |
| **П4** | Контракт зелий без доказанных гейтов | См. ниже; 11 новых проверок, NPC 87 → 98 | `tools/m0_pilots/Npc/NpcContracts.cs`, `NpcPilot.cs`, матрица C-24, карта §1.4 |
| **П5** | 3 скрипта `tools/alpha_research/` по умолчанию читали из `D:\Projects\CubeGame-deepseek\.scratch` и писали туда же | Пути от корня текущего репозитория; позиционные аргументы сохранены | `extract_model_load_table.py`, `extract_world_ctor_tables.py`, `verify_item_type_model_consistency.py` |
| **П6** | `item_layer.py --verify` («re-run and compare») не перезапускал ничего, только сверял SHA-пины | `--verify` перезапускает эмуляцию и сравнивает `verification` + `contract`; `--check-pins` — быстрая сверка пинов | `tools/alpha_models/catalog/item_layer.py`, `catalog/README.md` |

### П4 подробно — зелья ИИ (`CombatBehavior::vfunc_0`)

Порядок одного тика в Alpha и контракт `NpcRules.EvaluatePotion`:

1. **Гейты всего тика** (`0x42cbac…0x42cbc6`, DIRECT): `+0x128 ≠ 0` (поле стана, C-22) → выход; `action == 0x50` и таймер `+0x6c < 3000` мс → выход (защита от повторного питья).
2. **Ветка A** (`0x42cbdc`, DIRECT): `HP < 0.25·self.maxHP`, фракция **не проверяется**, текущее действие завершено (`+0x6c > combat_getAttackTiming 0x43d1a0`, строго), в сумке есть `(1,1)` → `action 0x50`, предмет копируется в руку (`0x42c5e0`), счётчик −1.
3. Выбор цели по угрозе.
4. **Ветка B** (`0x42ce8c`): выполняется **только при нулевом указателе цели** (`0x42ce36 test ebx / je`), `HP < 0.5·self.maxHP`, фракция 3 → `action 0x50`, предмет `0x0401 (1,4)`, уровень предмета = `+0x190`. Ветвление DIRECT; то, что «нет цели» = «вне боя», — PARTIAL.

Старая спецификация `BEHAVIOR_SPECS/G_H_ai_npc_pets.md` («`HP < 0.25·MaxHP` и таймер атаки истёк») описывала ветку A верно. Ветка B — дополнительная.

### Инцидент при проверке (раскрытие)

Из-за дефекта П5 мой запуск `extract_model_load_table.py` и `extract_world_ctor_tables.py` (2026-11-09 13:09) **перезаписал** в `D:\Projects\CubeGame-deepseek\.scratch\research\` файлы `model_load_table.json/.tsv` и `world_ctor_tables.json/.item.tsv/.object.tsv/.skill.tsv`. Это детерминированные выгрузки из тех же read-only источников. После П5 я сгенерировал их в своём `.scratch`: все 4 TSV **побайтно совпадают** с файлами DeepSeek; JSON отличаются только записанным путём источника. Других записей в папку DeepSeek не было.

## 3. Полные результаты тестов (ветка `claude/m0-00-prebatch-review`, всё перезапущено)

Логи: `.scratch/m0prebatch/logs/` (локально, не в Git).

| Набор | Результат |
|---|---|
| Python `pytest tools` | **375 passed**, 109 subtests, 0 failed |
| M0-пилоты (`run_pilots.ps1`, реальный Core + Unity Mono) | **478/478**: World 58, Movement 102, Combat 81, NPC 98, Items 85, Economy/Quests/Saves 54 |
| Unity 6.3 EditMode (S02–S04 + адаптеры S05) | **296/296**, exit 0 |
| `gen_alpha_tables.py --check` | PASS, hash `b2cafaad5e6c8c02` |
| `build_transfer_registry.py --check` | PASS, 176 строк |
| 43 проверки DeepSeek (replay) | PASS 43/0 |
| `verify_item_type_model_consistency.py` | PASS 34/0 |
| `verify_catalog_import` (D65) | PASS 2856/2856 |
| `item_layer --verify` (с перезапуском) | данные MATCH, пины OK; оружие 26/26, офхенд 7/7, предикаты 46/46, двуручная цепочка 9/9 |
| Негативная проверка П6 | изменённый `modelIndex` в evidence → `MISMATCH ["contract"]`, FAIL; файл восстановлен побайтно |
| `transfer_planner validate` | PASS, 18 проверок, 0 проблем |
| `transfer_planner plan` | 156 видов, 106 семейств, действия у 155, 1 `NO_VERIFIED_ACTION_TRIGGER` (RuneGiant) |

PlayMode и регрессия HumanMale 98/98 в этом ревью не запускались: код S05-адаптеров и `Assets/` не менялись. Их запуск обязателен в B0 (раздел 5).

## 4. Реально готовые к BATCH механизмы

| Механизм | Инструмент | Доказательство готовности |
|---|---|---|
| Геометрия каталога (2856 моделей) | D65-импорт + `verify_catalog_import` | 2856/2856 совпадают с байтами Alpha; повторная конвертация запрещена D66/D71 |
| Дракон 0x65 | `dragon_0x65_v1` (D83, опция) | утверждено продюсером; в базе этой ветки |
| Ходьба и покой 156 видов | `transfer_planner`/`batch_runner`, scope `d63_full` | VALIDATE PASS; формула сверена с захватами (D64) |
| Действия существ (155 видов) | scope `d63_actions` | план: доступны 155, RuneGiant — BLOCKED |
| Законы креплений оружия/офхенда, предикат «спина/рука», расходник 80/81 | `item_layer` / `item_attach` | `--verify` с перезапуском PASS — **данные готовы, исполнителя для Unity нет** (B4) |
| Подтверждённые таблицы (24 типа, 20 подтипов, 16 способностей, 13 слотов) | `gen_alpha_tables.py` | `--check` PASS — **готовы как данные**, перенос в Core ждёт M0-00D (B5) |
| Контракты систем (мир, движение, бой, NPC, предметы, экономика, сейвы) | `tools/m0_pilots` | 478/478 — контрактные проверки, **не** интеграция в Unity-сцену и не Alpha parity |

## 5. План последовательных массовых партий для DeepSeek

Общие правила каждой партии: база — SHA этой ветки; перед партией `git status` чистый; одна партия = один коммит/отчёт; после партии — регрессия. Статусы клипов как раньше (`EMULATED_ALPHA_CODE` / `TRANSFERRED_FROM_ALPHA` / `BLOCKED`); недоказанное не заполнять. Ассеты Alpha — только `.scratch`.

| Партия | Содержание | Команды / scope | Критерий PASS | Разрешение |
|---|---|---|---|---|
| **B0** Сверка базы | Воспроизвести раздел 3 на своей машине | `pytest tools`; `run_pilots.ps1`; оба `--check`; `item_layer --verify --workers 8`; `verify_catalog_import`; Unity EditMode + PlayMode walk/attack; регрессия HumanMale 98/98 (30/60/120 Гц) | Те же числа, что в разделе 3 | Не нужно (чтение и тесты) |
| **B1** Каталог + дракон | Подтвердить, что импорт 2856 моделей актуален; включить `dragon_0x65_v1` в сцену проверки | `verify_catalog_import`; сборка review-сцены с коррекцией D83 | 2856/2856; дракон совпадает с утверждённым D83 | D65 + D83 |
| **B2** Ходьба/покой 156 видов | Финальный прогон локомоции на базе с D83 | `transfer_planner batch --authorization D63 --scope d63_full --producer-approved-d64` (сначала `--dry-run`) | Все виды PASS или явный BLOCKED с причиной; регрессия 98/98 | D63/D65 — **продюсер подтверждает, что база этой ветки разрешена** |
| **B3** Действия существ | Действия 155 видов; RuneGiant — BLOCKED | `--scope d63_actions --producer-approved-d64` | Все действия вида выполнены, отклонение 0; RuneGiant в отчёте как BLOCKED | как B2 |
| **B4** Слой предметов | Оружие/офхенд/расходник на гуманоидах в Unity | **нет исполнителя** | — | `PREBATCH_BLOCKED`: см. 6.1 |
| **B5** Таблицы систем → Core | Типы/подтипы/слоты/способности в Core S04+ | **нет решения** | — | `PREBATCH_BLOCKED`: M0-00D |

После B0–B3 — фактическая проверка в Unity (D72, M0-00U): модели + анимации в сцене, исправления в Unity, повторная проверка.

## 6. Оставшиеся блокеры

### 6.1 Блокируют партии
- **ITEM-LAYER-NO-UNITY-EXECUTOR** (B4): контракт `cubegame.item_layer/1` не потребляется ни одним Unity-адаптером; в `batch_runner.SCOPES` нет scope для предметов. Нужны: решение продюсера (новый Dxx + scope), Unity-адаптер `item_attach`, пилот на HumanMale (одноручное, двуручное, парное, дальнобойное, щит, расходник 80/81), затем партия.
- **M0-00D решения** (B5): переименование членов `ItemType`/`WeaponSubtype` по r3 без изменения чисел (вариант A′, включая `None=0 → Sword=0`), расширение `ItemInstance` с 8 до 24 типов, сохранение 13 слотов (сейв v2, миграция v1→v2).

### 6.2 Не блокируют B0–B3, но открыты для M0
`OQ-01` (база шанса попадания), `OQ-24` (`+0x70`: серия/блок), `OQ-23` (масштаб скоростей), W-18 (климат→объекты), I-12/OQ-11 (лут-таблицы), F-U5 (цены покупки), J-U4/U5/U7 (задания), `DEATH-RULE` (решение продюсера), R-1/R-2 (оружейные действия NPC), R-3/R-8 (эффекты, динамические части), R-9/RuneGiant, слот `+0x300`, подтип 0x12. Зелья: лечение и списание в ветке B не доказаны; смысл «нет цели» — PARTIAL.

## 7. Что осталось без изменений
S02–S04, `Assets/`, `Packages/`, `ProjectSettings/`, `main`, ветки и рабочая папка DeepSeek (кроме инцидента раздела 2), перечисления и формат сейвов S04, `BATCH_AUTHORIZATIONS` планировщика.
