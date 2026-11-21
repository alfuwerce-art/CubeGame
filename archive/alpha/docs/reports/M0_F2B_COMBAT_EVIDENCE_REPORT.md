# M0-F2B-SUPPORT — снаряды, способности и VFX: доказательная и data-часть

**Дата:** 2026-11-10 · **Исполнитель:** DeepSeek · **Ветка:** `deepseek/m0-f2b-combat-evidence`
· **База:** F2A `5f197f9` (F1 `748d650`) · **STATUS: `READY_FOR_REVIEW`**

Роль — support для Claude: собрана **проверяемая** таблица combat-данных и генератор к ней.
Runtime боя, `GameBootstrap`, `CombatSystem`, PlayMode-тесты Claude и `GameVisualSink` не
редактировались; интеграция в runtime — handoff (§9). `ACCEPTED` не ставится.

## 0. Главный результат в двух строках

1. **В Alpha 2013 нет летящего снаряда-сущности.** Дальний удар считается мгновенно: `projectile_getMuzzlePosition`
   (VA `0x446BB0`) → нормализация направления → `World_raycastVoxels` (`0x5A35D0`, максимум **200 блоков**) →
   перебор существ вдоль луча → `stat_calcWeaponDamage` с разбросом `1.25 − 0.5·rand/32767`. Видимый «снаряд» —
   **сметаемый меш по двум точкам** (`render_build_projectile_mesh`, VA `0x4C04C0`, ровно **1** место вызова `0x4B6A47`),
   который перестраивается каждый кадр. Поэтому `Projectiles.Launch/Step` (полёт, гравитация, время жизни) — это
   `CUBEGAME_CONTRACT`, а не Alpha.
2. **Таблица «оружие → действие → числа» сгенерирована из байтов `Cube.exe`**, а не из декомпиляции:
   4 switch-функции декодированы через их собственные таблицы переходов, второй switch `combat_getAttackTiming`
   сверен с `combat_getReloadFrames` (105 id, **0 расхождений**), 42 байт-проверки, детерминированный вывод,
   `--check` падает при дрейфе.

## 1. Что читалось и что проверялось

Прочитано: `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `AGENTS.md`, `docs/planning/M0_REMAINING_PLAN_D90.md`,
`docs/reports/CODEX_D3_D6_INDEPENDENT_AUDIT.md`, `docs/reports/M0_F2A_CHARACTER_RUNTIME_REPORT.md`,
`docs/reports/D5_PROJECTILES_REPORT.md`, `docs/reports/M0_06C_C29_ATTACK_SELECTION_REPORT.md`,
`docs/research/alpha2013/BEHAVIOR_SPECS/E_combat.md`, `docs/research/alpha2013/M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX.md` (AI-15),
`docs/research/alpha2013/ITEM_MODEL_ANIMATION_TRANSFER.md`, `tools/alpha_models/catalog/item_layer_evidence.json`,
декомпиляция `.scratch/CubeWorld-Reversal/cube` (коммит `e6e906e`, только чтение) и байты `Cube.exe`
(sha256 `84a7a132…6dc30bf`, 3 885 568 байт, копия `.scratch/alphatest2/Cube.exe` идентична `D:\Games\Cube world\Cube.exe`).

Клиент Alpha не запускался. `D:\Games\Cube world` не изменялся. Ассеты Alpha не копировались.

## 2. Projectile catalog (доказано)

Источник модели оружия — исполненный `item_icon_id` (`0x4EC400`), `item_layer_evidence.json`
(`VERIFIED_EMULATION`, 26 классов оружия). Источник id атаки — `combat_selectAttackVariant` (`0x4452A0`,
268 байт, sha256 `81822ebc…17bc`, 31 байт-проверка) + 11 проверок гейтов снаряда в этом же генераторе.

| subtype | оружие | модель Alpha | action/attack (чёт/нечет) | ветка Alpha | muzzle/trail | статус |
|---|---|---|---|---|---|---|
| 6 | bow | `bow.cub` | `0x17`/`0x17` | `weapon_6_before_class` | нет | `PARTIAL` |
| 7 | crossbow | `wood-crossbow1.cub` | `0x42`/`0x41` | `parity` | нет | `PARTIAL` |
| 8 | boomerang | `wood-boomerang1.cub` | `0x42`/`0x41` | `parity` | нет | `PARTIAL` |
| 9 | arrow | `arrow.cub` | `0x03`/`0x04` | `unarmed` | нет | `PARTIAL` |
| 10 | staff | `wood-staff1.cub` | `0x1F`/`0x1F` | `class_branch_subtype` | нет | `PARTIAL` |
| 11 | wand | `wood-wand1.cub` | `0x2D`/`0x2D` | `class_branch_subtype` | нет | `PARTIAL` |
| 12 | bracelet | `silver-bracelet1.cub` | `0x25`/`0x25` | `class_branch_subtype` | **да** | `ALPHA_CODE_STATIC` |
| 14 | quiver | `quiver.cub` | `0x03`/`0x04` | `unarmed` | нет | `ALPHA_CODE_STATIC` |
| 5 | longsword | `iron-longsword1.cub` | `0x05`/`0x05` | `weapon_5` | нет | `ALPHA_CODE_STATIC` |
| 15/16/17/18 | greatsword/greataxe/greatmace/pitchfork | `iron-great*.cub`, `pitchfork.cub` | `0x42`/`0x41` | `parity` | нет | `ALPHA_CODE_STATIC` |
| 0–4, 13, 19, 20 | sword/axe/mace/dagger/fist/shield/pickaxe/torch | свои `.cub` | `0x03`/`0x04` | `unarmed` | нет | `ALPHA_CODE_STATIC` |

Полная таблица (26 строк, включая alias-строки 21–25 самого item-слоя) — в
`tools/alpha_content/data/combat_runtime_evidence.json` → `weapons` и в
`Assets/CubeGame/Content/Generated/AlphaCombatRuntimeData.g.cs` → `Weapons`.

**Что означает `muzzle/trail = нет`:** это «не доказано», а не «доказано, что нет». Ни один байт-анкер не
помещает action id лука/арбалета/посоха/жезла/бумеранга в гейт снаряда. Поэтому модель и id действия у них
`ALPHA_CODE_STATIC`, а весь путь снаряда — `PARTIAL`.

### 2.1 Гейты снаряда (байт-проверки, 11 штук)

| VA | байты | смысл |
|---|---|---|
| `0x4B630D` | `8a4668` | `mov al, [esi+0x68]` — текущий action id (гейт A) |
| `0x4B6310/14/18/1C` | `3c25` `3c26` `3c2b` `3c2c` | `0x25`, `0x26`, `0x2B`, `0x2C` → muzzle |
| `0x4B467B` | `8a4668` | `mov al, [esi+0x68]` — гейт B |
| `0x4B467E/93/97` | `3c1c` `3c5f` `3c5e` | `0x1C`, `0x5F`, `0x5E` → muzzle |
| `0x4B6495` | `807e6826` | `cmp byte [esi+0x68], 0x26` — особая длина ствола |
| `0x615FE9` | `80786868` | `cmp byte [eax+0x68], 0x68` — бомба: вместо рейкаста фиксированная дальность (`[0x745E74]`) |

Вызовы: `projectile_getMuzzlePosition` (`0x446BB0`) — **4** места (`0x42DA82`, `0x4B471B`, `0x4B64CA`, `0x615FAB`);
`render_build_projectile_mesh` (`0x4C04C0`) — **1** место (`0x4B6A47`). Оба факта проверяются генератором;
если в бинарнике появится второй вызов trail-меша или третий/пятый muzzle — `--check` упадёт (`MUZZLE_CALL_SITES`/`TRAIL_CALL_SITES`).

### 2.2 Разрешение дальнего удара (декомпиляция + байты, `0x615F62…0x615FDD`)

```
0x615F62  edi = creature + 0x160          ; вектор прицела
0x615F7A  call 0x424860                   ; длина²
0x615F96  call 0x4240F0                   ; нормализация
0x615FAB  call 0x446BB0                   ; muzzle-позиция
0x615FBB  push 0x43480000                 ; 200.0f — предел луча
0x615FC5  call 0x427870                   ; нормализация направления
0x615FD8  call 0x5A35D0                   ; World_raycastVoxels
0x615FDD  fstp [ebp-0x2B1C]               ; дистанция попадания
0x615FE9  cmp byte [eax+0x68], 0x68       ; бомба → константа вместо дистанции
```

Далее (декомпиляция `GameController.cpp:98638-98720`): перебор существ, проекция на луч, условие
`|вдоль| < размер·0.5 + 1`, `combat_rollElementProc`, урон `pow · 1.5 · (1.0 + 0.05·rand/32767)`, для
`action 0x68` — `ratio²·stat_calcWeaponDamage·10`, разброс `1.25 − 0.5·rand/32767`, `×2` при элементарном проке.
Отсюда: **скорость, гравитация и время жизни полёта не существуют как данные Alpha** — их неоткуда взять,
и в генераторе их нет (тест `GeneratedDataInventsNoFlightNumbers`).

### 2.3 Что именно не доказано по снарядам

| Вопрос | Статус | Что проверено |
|---|---|---|
| модель летящего снаряда | `UNKNOWN` | `render_build_projectile_mesh` строит геометрию из двух точек (sin/cos), `.cub` не грузит; `drawModelAtProjectedPos` (`0x476660`) вызывается только из UI/карты |
| скорость / гравитация / lifetime | `UNKNOWN` | нет сущности-снаряда: нет списка с обновлением позиции, нет константы гравитации рядом с muzzle-путём |
| возврат бумеранга | `UNKNOWN` | ни одного обращения к «возврату» не найдено; `0x471D50` (`drawOrientedModelOrTrail`) — рендер, не симуляция |
| кто именно держит action id в момент гейта | `PARTIAL` | `+0x68` несёт и wind-up action, и release attack id; `combat_selectAttackAnim` (`0x445020`) даёт anim-id, `combat_selectAttackVariant` — attack-id |
| jitter muzzle-позиции | `PARTIAL` | `0x446BB0` умножает смещение на `cos/sin(yaw[+0x30])`, добавляет `ftol2`-джиттер; конкретные коэффициенты не восстановлены (декомпилятор теряет `self`) |

## 3. Ability catalog (доказано)

`combat_getAttackTiming` (`0x43D1A0`) = **cast** (свой switch, id `0..0x69`) + **recover** (второй switch `0x43D510`,
id `0..0x68`). Значение: `fix N` (Alpha возвращает N как есть) или `B/(haste·speed[+0x17c])`.
`combat_getReloadFrames` (`0x447310`) — та же recover-таблица; сверка двух независимых декодирований дала
**0 расхождений на 105 id** (`RECOVER_CONFLICT` — механизм проверки реально срабатывал в процессе, см. §7).
`combat_getStaminaCost` (`0x444270`) — база стамины.

| attack | cast | recover | stamina | muzzle | bomb |
|---|---|---|---|---|---|
| `0x17` (bow) | 50 | 50 | 10 | нет | нет |
| `0x1F`/`0x21` (staff) | 1600 | fix 1200 | 100 | нет | нет |
| `0x25`/`0x2B` (bracelet) | 600 при subtype `0x0C`, иначе 1200 | 100 | 100 | **да** | нет |
| `0x26`/`0x2C` (wand anim) | 500 | 300 | 100 | **да** | нет |
| `0x2D`/`0x2E` (wand attack) | 1200 | 500 | 100 | нет | нет |
| `0x41`/`0x42` (crossbow/boomerang/great*) | 300 | 400 | 200 | нет | нет |
| `0x1C` | fix 0 | 300 | 500 | **да** | нет |
| `0x5E` | 500 | 500 | 100 | **да** | нет |
| `0x5F` | 1000 | fix 2000 | 100 | **да** | нет |
| `0x68` (bomb) | fix 0 | 300 | 100 | нет | **да** |
| `0x69` (последний id) | fix 5000 | 300 (**default**) | 500 (**default**) | нет | нет |

Все числа — `ScaledByHasteAndSpeed`, если не помечено `fix`. Для `0x69` recover/stamina приходят из ветки
`default:` и это **явно помечено** (`RecoverFromDefault`/`StaminaFromDefault`), а не выдано за значение кейса.

Дополнительно (декомпиляция, `ALPHA_DECOMPILED`, в generated-данные **не** включено):
`combat_selectAttackAnim` (`0x445020`) — какой action проигрывать: bow/crossbow `0x16`; staff `0x1E`/`0x20`
(spec 1); wand `0x26`/`0x2C`; bracelet `0x28`/`0x2A` (spec 1) / `0x27`; boomerang + руна `0x1A`;
dagger `0x13`/`0x12`; longsword `0x0E`/`0x0D`; прочее `0x06`/`0x07`; без оружия `0x01`/`0x02`.
`ability_getResourceCost` (`0x444AE0`) — доля ресурса: `0.1` для id `3/4`, отдельная ветка (fallback `0.3`) для
`0x1F,0x21,0x25,0x2B,0x2D,0x2E,0x5F`, `(1 − power·0.75)·0.125` для `0x22`, `1.0` для `0x31`. Таблицу ресурса
в генератор не включал: структура блоков (fall-through + общий хвост с buff-сканом) не декодируется тем же
строгим способом, поэтому числа остались бы догадкой.

Именованные способности (16 узлов, `AbilityRangerKick` 21 … `AbilityManaShield` 103) уже лежат в
`AlphaContentTables.Abilities`; в новом генераторе **не дублировались**. Расхождение по стамине для `48` и `54`
с этой таблицей — не конфликт: `AlphaContentTables` берёт значения из другого reviewed-источника, а мой
`combat_getStaminaCost` даёт `0` и `400`; сверять их как одно поле нельзя (разные функции).

## 4. VFX evidence (R-3 / R-8)

Проверялись существующие исследования (`D65_FULL_CATALOG_RUN_REPORT`, `S05B2_*`, `M0_00B_*`, `D4_COMBAT_FEEDBACK_REPORT`)
и код клиента. Новая VFX не создавалась.

| Рендерер / функция | VA | вызовы | что доказано |
|---|---|---|---|
| `CombatBehavior_spawnProjectileHit` | `0x596D30` | 7 (`0x60DD18`, `0x60F45D`, `0x61476B`, `0x6166C8`, `0x6183DA`, `0x621E8C`, `0x622AB8`) | резолв попадания + постановка effect-записей; урон/HP/блок/элементы — здесь же |
| `CombatBehavior_spawnHitParticles` | `0x59A800` | 1 (`0x597670`) | вызывается **на каждый элемент** активного оружия (`equipment_getActiveElement`) — связь «элемент → частицы попадания» доказана |
| `Creature_spawnDeathEffects` | `0x59B180` | 1 (`0x59622E`) | вызывается из `applyMeleeHit` при смерти |
| `GameController_spawnEffectMesh` | `0x4BD160` | 2 (`0x4B2801`, `0x4BA4FE`) | отдельный рендерер эффекта, не поза `drawModel` |
| `GameController_spawnRibbonEffect` | `0x4BBD80` | 5 | ленты; вызывается рядом с trail-путём |
| `GameController_spawnRibbonEffect2` | `0x4BC760` | 2 (`0x4B4430`, `0x4B4549`) | вторые ленты |
| `render_build_projectile_mesh` | `0x4C04C0` | 1 (`0x4B6A47`) | сметаемый меш «снаряда» |
| `drawOrientedModelOrTrail` | `0x471D50` | 5 | ориентация модели/сегмента по направлению |
| `World_emitDecalOrParticle`, `World_buildParticleBatch` | `0x5F6D50`, `0x5F7A60` | — | источник декалей/частиц мира |

**Что это значит:** участие в уроне доказано только у `spawnProjectileHit` (он и есть резолвер). Остальные —
визуал. Параметры визуала (что именно рисуется, размеры, цвета, время жизни частиц) — `UNKNOWN`/`BLOCKED`:
декомпилятор теряет базу `self` у effect-структур (`undefined4`-блоки), а аргументы
`render_build_projectile_mesh` (14 параметров) статически не восстанавливаются. В generated-данные VFX не попал.

Практический вывод для runtime: **VFX-паритет не нужен, чтобы F2B работал** — нужен только muzzle/рейкаст и
(опционально) сметаемый след для 7 доказанных action ids.

## 5. Generated data

| Файл | Назначение |
|---|---|
| `tools/alpha_content/gen_combat_runtime_data.py` | генератор + `--check`; capstone, stdlib; только чтение `Cube.exe` |
| `tools/alpha_content/data/combat_runtime_evidence.json` | доказательство: sha256 бинарника, sha256 каждой функции, 42 байт-проверки, таблицы, `unknown`, `problems`, `RESULT` |
| `Assets/CubeGame/Content/Generated/AlphaCombatRuntimeData.g.cs` | числа, enum-like id, имена моделей, action id, проверенные флаги — и ничего больше |

В generated-файле нет ни геометрии, ни ассетов, ни путей к Alpha: только `0x…` id, `.cub`-имена (они уже
закоммичены в `AlphaContentTables.g.cs`) и числовые константы. Runtime-кода в этом пакете нет: класс
`AlphaCombatRuntimeData` — чистая таблица + два поиска (`TryGetAttack`, `TryGetWeapon`).

## 6. Проверки генератора

Выполнено (реальные прогоны):

| Команда | Результат |
|---|---|
| `python -m tools.alpha_content.gen_combat_runtime_data` | `COMBAT_RUNTIME_RESULT=PASS attacks=106 weapons=26 checks=42 problems=[]` |
| `python -m tools.alpha_content.gen_combat_runtime_data --check` | `PASS … drift=[]`, exit 0 |
| повторная сборка внутри одного прогона | вывод байт-в-байт совпал (`NONDETERMINISTIC_OUTPUT` не сработал) |

Защиты, встроенные в генератор: `DUPLICATE_WEAPON_SUBTYPE`/`DUPLICATE_ATTACK_ROW`, `MISSING_MODEL_MAPPING`,
`MISSING_PROJECTILE_SUBTYPE`, `IMPOSSIBLE_ATTACK_ID`, `IMPOSSIBLE_<CAST/RECOVER/STAMINA>`,
`WEAPON_REFERS_TO_UNKNOWN_ATTACK`, `RECOVER_CONFLICT`, `MUZZLE_CALL_SITES`/`TRAIL_CALL_SITES`,
`UNDECODED_*`, `NONDETERMINISTIC_OUTPUT`.

Негативные проверки (порча входа в `.scratch/f2b/`, репозиторий не менялся):

| Порча | Результат |
|---|---|
| удалена строка bracelet (subtype 12) | `FAIL`, `MISSING_PROJECTILE_SUBTYPE 12`, drift по обоим файлам, exit 1 |
| продублирован bow (subtype 6) | `FAIL`, `DUPLICATE_WEAPON_SUBTYPE 6`, exit 1 |
| у bow убрана модель | `FAIL`, `MISSING_MODEL_MAPPING subtype=6 label=bow`, exit 1 |
| 21 ложный `RECOVER_CONFLICT` при первом (неверном) декодере | проверка сработала; после исправления — 0 конфликтов |

## 7. EditMode tests

`Assets/CubeGame/Tests/Creatures/EditMode/AlphaCombatRuntimeDataTests.cs` — 10 тестов, только data-слой:

`WeaponProjectileMappingMatchesAlphaItemLayer`, `EveryWeaponRowCarriesItsAlphaModel`,
`AttackRowsAreUniqueAndCoverTheProvenRange`, `UnknownWeaponSubtypeIsNotSilentlyUnarmed`,
`MuzzleTrailPathIsProvenForExactlyTheByteVerifiedActions`, `MuzzlePathIsNotClaimedForWeaponsWithoutProof`,
`AttackNumbersMatchTheDecodedSwitches`, `DefaultBranchValuesAreFlaggedInsteadOfFabricated`,
`EscapeArrowsAndQuiversAreNotClaimedAsProjectileAttackActions`, `GeneratedDataInventsNoFlightNumbers`.

| Прогон | Результат |
|---|---|
| `Unity.exe -batchmode -projectPath D:\Projects\CubeGame -runTests -testPlatform EditMode -testFilter CubeGame.Tests.Creatures.EditMode.AlphaCombatRuntimeDataTests -testResults …` | **10 total / 10 passed / 0 failed / 0 skipped**, `error CS = 0` |
| Компиляция по логу | 0 `error CS`, 0 `Scripts have compiler errors`; 4 `warning CS0675` — существующие в `Ids.cs`, не из этого пакета |
| Первый прогон (с `-quit`) | тесты не успели запуститься, XML нет — повторено без `-quit` |

Unity-слот: `CLAIM 15:13` → `RELEASE 15:14` в `docs/agents/AGENT_COORDINATION.txt`
(файл не коммитился: в нём есть незакоммиченные правки сессии Claude).

## 8. UNKNOWN / PARTIAL (сводка)

| Пункт | Статус | Чем закрывать |
|---|---|---|
| полёт/гравитация/lifetime снаряда | `UNKNOWN` | нет сущности в клиенте; либо решение продюсера о CubeGame-контракте, либо отказ от полёта |
| модель летящего снаряда | `UNKNOWN` | R-3: нужен перенос `render_build_projectile_mesh`/`spawnEffectMesh` |
| muzzle-джиттер и смещения | `PARTIAL` | декомпилятор теряет `self`; нужен эмулятор или рантайм-дамп `0x446BB0` |
| какой action id держит существо в момент гейта | `PARTIAL` | нужен дамп `+0x68` в бою (или исполнение `selectAttackAnim`+`selectAttackVariant` в связке) |
| resource cost способностей | `PARTIAL` | отдельный разбор `0x444AE0` (fall-through блоки) |
| VFX-параметры (цвета, размеры, жизнь частиц) | `BLOCKED` (R‑3/R‑8) | как в D65/S05B2: нужен перенос рендереров |
| `+0x70` (чётность/серия) | `PARTIAL` (OQ‑24) | без изменений, см. C‑29 |
| alias-строки item-слоя 21–25 | `PARTIAL` | это запасные ветки селектора `item_icon_id` (модель = индекс 899), а не отдельные виды оружия |

## 9. Handoff Claude (точный список)

1. **Подключать `AlphaCombatRuntimeData`** (namespace `CubeGame.Content`) вместо повторного разбора:
   `TryGetWeapon(subtype)` → `AttackEven/AttackOdd`, `TryGetAttack(id)` → `Cast/Recover/Stamina` + режимы.
   Имена: `MuzzlePathProven` = «доказано», `false` = «не доказано».
2. **Дальний бой = hitscan.** Порядок Alpha: вектор прицела (`+0x160`) → нормализация → muzzle → нормализация →
   рейкаст до **200** блоков → существа вдоль луча с допуском `size·0.5 + 1` → урон. Полёт снаряда не нужен.
3. **`Projectiles.Launch/Step` не выдавать за Alpha.** Если продюсер хочет видимый полёт — это отдельное решение
   и статус `CUBEGAME_CONTRACT`; текущая модель (стрела/бумеранг как модель `.cub`) Alpha не подтверждена.
4. **Trail рисовать только для `MuzzleAttackIds`** (`0x1C,0x25,0x26,0x2B,0x2C,0x5E,0x5F`). Для лука/арбалета/
   посоха/жезла Alpha-подтверждённого следа нет — не подменять его выдуманной стрелой.
5. **Бомба (`0x68`)** — отдельная ветка: cast 0, фиксированная дальность (`[0x745E74]`) вместо рейкаста, фитиль `+0x170`.
6. **Не менять** `TwoHandedSubtype = 6` в `AlphaAttackSelectionTable` без сверки: по item-слою subtype 6 — это
   **лук** (`bow.cub`), а не двуручное оружие; число верное, имя опасно.
7. **`AlphaContentTables.Abilities` не переиспользовать как стамину**: `0`/`400` против `500` для id 48/54 —
   разные функции (`ability_getResourceCost` vs `combat_getStaminaCost`).
8. **VFX не выдумывать**: R‑3/R‑8 остаются `BLOCKED`; в этом пакете никаких эффектов не создано.
9. Runtime-файлы (`GameBootstrap`, `CombatSystem`, projectile manager, PlayerMotor, PlayMode-тесты,
   `GameVisualSink`) **не менялись** — всё подключение за Claude.

## 10. Блокеры и попытки решения

**Блокер 1 — «нет данных о снарядах» (R-3).**
Попытка 1: декомпиляция `CombatBehavior_spawnProjectileHit` (`0x596D30`) → effect-записи строятся в
`undefined4`-локалях, база `self` потеряна; смысл полей не восстановим. Неудача для «что рисуется».
Попытка 2: байтовый скан вызовов (`E8 rel32`) к `0x446BB0`, `0x4C04C0`, `0x4BD160`, `0x4BBD80`, `0x4BC760`,
`0x59A800`, `0x59B180` → получены точные списки мест вызова и гейты `+0x68`; это дало **структуру** пути
(4 muzzle → 1 trail) без рендереров. Результат: структура закрыта, рендер остаётся `BLOCKED`.
Попытка 3: поиск «летящей сущности» (гравитация/скорость/lifetime) по всему коду и константам → не найдено.
Вывод: `UNKNOWN`, и это доказательный минус, а не пропуск.

**Блокер 2 — сопоставление «оружие → эффект».**
Попытка 1: `combat_selectAttackVariant` (attack id) vs гейты muzzle → пересечение только `0x25/0x2B`.
Попытка 2: `combat_selectAttackAnim` (`0x445020`, anim id) → `0x26/0x2C` попадают в гейт A. Итог: `PARTIAL`,
гипотеза «wind-up id vs release id в `+0x68`» записана как гипотеза, не как факт.

**Блокер 3 — числа способностей только «на глаз» из switch.**
Попытка 1: ручное чтение switch-функций → 106 строк, риск опечатки.
Попытка 2: декодер таблиц переходов (capstone) + чтение float-констант из `.rdata` → числа берутся из байтов;
первый декодер ошибался (регистр результата, проскок в соседний блок) — поймано самопроверкой
`RECOVER_CONFLICT` (21 конфликт), исправлено, теперь 0 конфликтов на 105 id.

## 11. Файлы и коммит

| Файл | Статус |
|---|---|
| `tools/alpha_content/gen_combat_runtime_data.py` | новый |
| `tools/alpha_content/data/combat_runtime_evidence.json` | новый (сгенерирован) |
| `Assets/CubeGame/Content/Generated/AlphaCombatRuntimeData.g.cs` | новый (сгенерирован) |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaCombatRuntimeDataTests.cs` | новый |
| `docs/reports/M0_F2B_COMBAT_EVIDENCE_REPORT.md` | этот отчёт |

Ветка `deepseek/m0-f2b-combat-evidence`, база `5f197f9`. `push` не выполнялся.
Не тронуты: `GameBootstrap.cs`, `CombatSystem.cs`, projectile manager, PlayerMotor/movement,
FullM0 PlayMode tests, `GameVisualSink.cs`, `Projectiles.cs`, `AlphaAttackSelection.*`.

**Статус: `READY_FOR_REVIEW`** (не `ACCEPTED`).
