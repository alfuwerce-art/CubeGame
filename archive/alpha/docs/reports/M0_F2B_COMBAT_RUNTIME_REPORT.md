# M0-F2B COMBAT & MOVEMENT RUNTIME

10 ноября 2026 · `claude/m0-f2b-combat-runtime` · база F2A `5f197f9` (содержит F1 `748d650`) · Claude

**`READY_FOR_REVIEW`** — не `ACCEPTED`.

## 0. База и ветки

`origin/integration/m0-all` = `628a80b` (документы M1) не содержит ни F1, ни F2A, поэтому этап начат от F2A `5f197f9`, как требует ТЗ. В ветку первым коммитом сведён локальный коммит DeepSeek `08ae4e3` (combat evidence, автор сохранён, новые файлы, без конфликтов). `main` и формат сейва не менялись. Alpha-ассеты не коммитились.

## 1. Состояние до (аудит)

Колонки: **CODE** — код есть; **RUNTIME** — подключён в игровой сцене (`GameBootstrap`); **TESTED** — есть тест; **ALPHA** — происхождение.

| Система | CODE | RUNTIME | TESTED | ALPHA PROVENANCE | Дефект |
|---|---|---|---|---|---|
| melee attack (выбор действия C4) | да | да | да | `ALPHA_CODE_STATIC` (0x445020) | — |
| melee hit (сектор, один удар на strike) | да | да | да | сектор/дальность `PLACEHOLDER_NOT_ALPHA` | — |
| ranged / bow / crossbow / boomerang / staff / wand | нет | **нет** | нет | — | `SelectMelee` → null, `HeroAttack` подставлял `HeroAttackAction` 57: **ranged/magic падали в melee** |
| projectile model / spawn / velocity / gravity / lifetime | `Projectiles.cs` (D5) | **нет** | синтетика | имена моделей Alpha; полёт — контракт D5 | `DATA_IMPLEMENTED`, нет вызывающих |
| collision → hit, despawn | нет | нет | нет | — | `ResolveHit` вызывался только из melee |
| target selection (ranged) | нет | нет | нет | — | — |
| attack timing | да (S05 клипы) | да | да | `EMULATED_ALPHA_CODE` / `PLACEHOLDER` | — |
| dodge / roll | нет | нет | нет | `UNKNOWN` | — |
| movement: ground/run/jump/swim/climb/glider | да (`AlphaMovement`, C2) | да (`PlayerMotor`) | да (`FullM0MovementTests`) | `ALPHA_CODE_STATIC` (байты C2) | — |
| abilities (16 именованных) | таблица `AlphaContentTables.Abilities` | нет | данные | `ALPHA_CODE_STATIC` (таблица) | нет ввода/рантайма |
| combat VFX (R-3/R-8) | кривые D4 | вспышка/труп | да | `CUBEGAME_CONTRACT`; Alpha `BLOCKED` | — |
| species scale | нет | **нет** | нет | — | `ModelScale` подгонялся под тело **текущего** героя; размер вида не применялся ни к кому |

## 2. Alpha-доказательства (байты `Cube.exe`, capstone)

Исследование вёл по байтам, декомпиляция — только указатель. Скрипты — в scratchpad сессии (дизассемблер `adis.py` поверх `tools/alpha_models/anim/pe_bytes.py`), `Cube.exe` sha256 `84a7a132…` не запускался.

### 2.1 Летящий снаряд существует (0x16/0x17/0x1A/0x25…0x2E)

| Адрес | Что делают байты |
|---|---|
| `0x617184…0x6171DB` | case `0x16/0x17/0x1A`: выпуск, когда время действия пересекает windup (для `0x1A` с зарядом — windup+300) |
| `0x6171ED…0x617208` | `v = normalize(aim +0x160) · 100.0` (`push 0x42C80000`, `vec3_scale`) |
| `0x617227…0x61740B` | структура: `+0x60` тип (0 стрела, 2 бумеранг), `+0x68` возраст = 0, `+0x4C` размер 0.5, `+0x3C` скорость, `+0x48` урон, `+0x18` позиция = позиция существа |
| `0x6174F2…0x617526` | `list_pushBack` в **`[world]+0x14`** |
| `0x617C88…0x617CCB` | группа `0x25…0x2E` (жезл): тип 1, `v = normalize(aim) · 50`, при браслете (`+0xAA9 == 0x0C`) `· 100`; размер `0.5 + 4·charge` (`0x617CEA`) |
| `0x620A7B…0x620AB8` | интегратор обходит **тот же** список `[world]+0x14` |
| `0x620C6F…0x620C99` | для типов ≠1/2: `vz −= dt · 30 · 0.25` (7.5 блок/с²); магия без гравитации |
| `0x620C9D…0x620CC1` | `pos += v·dt`, `age += ms` (`dt = ms·0.001`, `0x60C852`, как в C2) |
| `0x620CD3` | тип ≠2 и `age > 5000` → удаление |
| `0x620AD9…0x620BF6` | бумеранг: до 800 мс полёт (подруливание к точке прицела — lerp 0.05, **не перенесено**), 800–1100 `v = (owner − pos)·10`, 1100–1200 позиция тянется к владельцу, >1200 удаление |
| `0x621590…0x621912` | попадание в существо: по каждой оси `|Δ| < size/2 + (+0x4C)`, враждебность, `+0x128 == 0` |
| `0x621A1F` | после попадания удаляется всё, кроме бумеранга |
| `0x6220D1`, `0x622293` | терраин: твёрдый воксель в новой позиции или луч вдоль `v·dt` → стрела/магия удаляются, бумеранг останавливается (`v = 0`, `0x622382`) |

**Поправка к отчёту DeepSeek `08ae4e3` §0/§2.3** («в Alpha нет летящего снаряда-сущности; скорость/гравитация/lifetime не существуют»): для действий `0x16/0x17/0x1A/0x25…0x2E/0x6C` это неверно — запись снаряда и интегратор работают с одним списком `[world]+0x14`, все поля совпадают по смещениям. Его мгновенный луч на 200 блоков (`0x615F62…0x615FDD`) верен, но относится к **другой** группе действий: `0x1C/0x5E/0x5F` и бомбе `0x68`. Остальные его данные (таблицы timing/stamina, muzzle-гейты, VFX-вызовы) подтверждены и использованы.

Решение продюсера по расхождению (ответ на вопрос в сессии): «выбрать более удобный и рабочий вариант, который не ломает игру и сохраняет визуал». Поэтому в рантайме оба пути, каждый для своих действий: снаряд для лука/бумеранга/жезла/браслета и луч для `0x1C/0x5E/0x5F/0x68`.

### 2.2 Посох (0x1E…0x21)

Действие посоха находится в группе ближнего боя (`0x6142A0`): объём сдвигается вперёд от атакующего, урон `lookup · stat_pow`, повтор через `reload/6`. Снаряда нет. Сдвиг и размер объёма в декомпиляции потеряны (`UNKNOWN`), поэтому значения — `CUBEGAME_CONTRACT`.

### 2.3 Масштаб вида (species scale)

`Creature_drawModel` исполнен в `drawmodel_emu.py` для всех 156 видов. Равномерный масштаб матрицы размещения (`local_14e0`) зависит от вида: 0x02 — 0.0857143, 0x04 — 0.0714286 (0.833 = 1.8/2.16), 0x0C — 0.0929, волк 0x1C — 0.1357143. Захват S05 вычитает размещение (`alpha_clip.joint_frame`: `· placement⁻¹`), поэтому у всех актёров Unity, и героя, и NPC, этот множитель отсутствовал. Вывод F2A «`creature_getScaleVec4` = 1» остаётся верным, но размер вида применяется через матрицу размещения. Масштаб не зависит от кадра (две разные последовательности кадров) и равномерен по осям; генератор проверяет оба условия.

## 3. Архитектура после

```
ЛКМ → GameBootstrap.HeroAttack
  → SyncHeroCombatant: hit box = размер вида Alpha; aim = взгляд камеры (CUBEGAME_CONTRACT)
  → AlphaAttackRoute.Select(class, main, off) → AttackPlan {action, path, kind}   — без fallback
  → CombatSystem.TryStartAttack(plan)  (unsupported → отказ с причиной UNKNOWN, без действия)
  → фазы на игровых часах → Strike (один раз):
       Melee      → ResolveMelee (сектор)                       → ResolveHit
       Projectile → ProjectileSystem.Launch → шаги по часам → бокс/терраин → ResolveHit → удаление ×1
       AreaEffect → объём посоха (тот же ProjectileSystem, стоит на месте, 100 мс)
       Hitscan    → ProjectileSystem.FireRay: луч ≤200 блоков до стены (бомба: 10) → все враги на луче ×1 → ResolveHit
  → события ProjectileSpawned / ProjectileRemoved / HitscanFired / HitResolved(AttackSerial)
  → ProjectileVisuals (единственный владелец визуала снарядов и следов)
```

- Выбор пути: оружие → действие (C4/0x445020) → путь по группе обработчика (`AlphaAttackRoute.ForAction`).
- `ProjectileSystem` — геймплей (Features, без Unity). Позиции в игровом пространстве. Смена начала координат его не касается: визуал пересчитывает сцену из игровых координат сразу после шага часов и после rebase.
- Снаряды и лучи не сохраняются. При выходе/загрузке — `Clear` (одно событие `Cleared` на снаряд), после загрузки бой в idle.
- `ResolveHit` по мёртвой цели ничего не публикует (`TargetAlreadyDead`).

## 4. Melee

Путь `HeroAttack → TryStartAttack(plan) → Strike → ResolveMelee → ResolveHit → смерть` без изменений по смыслу. Добавлено: стабильный `AttackSerial` в `HitResolved`, `ActionSerialOf`, одна резолюция на strike, отказ во время действия, отсутствие повторных эффектов на мёртвой цели. Ранее подставлявшееся действие 57 для дальнего боя убрано.

## 5. Ranged / magic — по оружию

| Оружие | Действие | Путь | Снаряд/эффект | Модель | Тайминг | Спаун | Попадание | Статус |
|---|---|---|---|---|---|---|---|---|
| Bow / Crossbow (main или off-hand) | 22 (0x16) | Projectile | стрела: 100 бл/с, g 7.5, 5000 мс, размер 0.5 | `arrow.cub` (модель предмета) | клип S05/fallback | центр тела (CUBEGAME) | бокс Alpha, удаление после попадания | движение/попадание `ALPHA_CODE_STATIC`; вид в полёте `PARTIAL` |
| Boomerang | 26 (0x1A) | Projectile | 100 бл/с, без g, возврат 800/1100/1200 | деревянный бумеранг (селектор) | то же | центр тела | один раз на цель за бросок (CUBEGAME), не удаляется | `PARTIAL` (подруливание до 800 мс) |
| Wand | 38 (0x26) | Projectile | магия: 50 бл/с (×2 с браслетом в off-hand), без g | плейсхолдер-сфера (`render_build_projectile_mesh` R-3) | то же | центр тела | бокс Alpha | `ALPHA_CODE_STATIC`; визуал `PLACEHOLDER_NOT_ALPHA` |
| Bracelet (main hand) | 39 (0x27) | Projectile | магия | плейсхолдер | то же | центр тела | бокс Alpha | `PARTIAL`: действие по декомпиляции (DeepSeek §3), не по эмуляции C4 |
| Staff | 30 (0x1E) | AreaEffect | объём 1.5 бл. впереди, +1.0 к полуразмеру цели, 100 мс | плейсхолдер | то же | перед героем | один раз на цель | путь `ALPHA_DECOMPILED`, числа `CUBEGAME_CONTRACT` |
| — (действия 0x1C/0x5E/0x5F) | — | Hitscan | луч ≤200 блоков, стоп у стены | след-линия, только для muzzle-id | — | центр тела | все враги на луче, `size/2 + 1` | `ALPHA_CODE_STATIC`; допуск `PARTIAL`. Основная атака героя их не выбирает |
| Bomb | 0x68 | Hitscan | фиксированная дальность 10 (`[0x745E74]`) | — | — | — | — | `PARTIAL`: ввод использования бомбы не подключён |
| Mage без оружия / Mage с мечом | — | **отказ** | — | — | — | — | — | `UNKNOWN` (правило Alpha не восстановлено) |

След рисуется только для байт-доказанных muzzle-действий `0x1C,0x25,0x26,0x2B,0x2C,0x5E,0x5F` (тест сверяет список с `AlphaCombatRuntimeData.MuzzleAttackIds`). Сам след — линия `PLACEHOLDER_NOT_ALPHA`, 0.15 с.

Урон всех путей идёт через `CombatSystem.ResolveHit` (Core S03/S04). Формулы урона Alpha для снарядов (`stat_pow·1.5`, `charge²·5`) не перенесены — `PARTIAL`, F3.

## 6. Movement

Аудит: `AlphaMovement` (C2, байты) уже покрывает землю, бег, прыжок, плавание, лазание и дельтаплан; подключён через `PlayerMotor`; покрыт `FullM0MovementTests`. Для боевого цикла M0 новых режимов не нужно, поэтому код движения не менялся.

| Режим | Статус |
|---|---|
| ground / run / jump / swim / climb / glider | `ALPHA_CODE_STATIC`, RUNTIME, TESTED (C2) |
| dodge / roll / dash | `UNKNOWN`: в восстановленных данных нет (C2: «dash (C4)» не перенесён). Не реализовано, выдумывать не стал |
| boat, ветер дельтаплана, «жёсткое приземление» | `PARTIAL` (C2), вне F2B |

## 7. Species scale — одно правило

- **Визуал** (`GameVisualSink.Fit`, герой и все существа): `ModelScale × placement(kind)/placement(0x02)` (`EMULATED_ALPHA_CODE`). `ModelScale` подгоняется один раз на мир по эталону HumanMale 0x02 (тело 1.8 — прежний `PLACEHOLDER_NOT_ALPHA`) и больше не зависит от расы героя.
- **Бой**: hit box = `size` вида Alpha (appearance +0x08, эмулировано для 156 видов) для героя и NPC.
- Таблица: `tools/alpha_content/gen_creature_sizes.py` → `AlphaCreatureSizes.g.cs` (только числа).
- Следствия: волк 0x1C теперь в 1.583× крупнее прежнего, герой расы 4 (0x04) — 0.833 от человека. `CharacterController` героя остаётся 1.8 для всех рас (`PARTIAL`, физика героя — F3).

## 8. Abilities / VFX

| Пункт | Статус |
|---|---|
| 16 именованных способностей, тайминги/стамина (DeepSeek) | данные `ALPHA_CODE_STATIC`; ввода и рантайма способностей нет — `PARTIAL`, F3 |
| `spawnProjectileHit` (резолвер), `spawnHitParticles`, `spawnDeathEffects`, `spawnEffectMesh`, ленты | места вызова доказаны; параметры визуала `BLOCKED` (R-3/R-8). Новых VFX не создано |
| вспышка попадания / труп (D4) | `CUBEGAME_CONTRACT`, без изменений |

## 9. CUBEGAME_CONTRACT (явно)

- прицел героя = направление камеры (писатель `+0x160` у игрока не декодирован);
- точка выпуска = центр тела (позиция ног + половина высоты); `projectile_getMuzzlePosition` не перенесён;
- центр существа для бокса = ноги + половина высоты;
- подшаги ≤0.25 блока (Alpha шагает раз в кадр, CubeGame — 30 Гц);
- удаление снаряда в незагруженном чанке и при пропаже владельца;
- бумеранг поражает цель не более одного раза за бросок;
- объём посоха (смещение 1.5, допуск 1.0, 100 мс);
- шаг луча 0.1 блока (вместо `World_raycastVoxels`);
- плейсхолдеры магии/посоха и след-линия; ориентация модели стрелы по длинной оси.

## 10. PARTIAL / UNKNOWN

| Пункт | Статус |
|---|---|
| подруливание бумеранга до 800 мс (`0x620BF6`) | `PARTIAL` |
| формулы урона снарядов/луча Alpha | `PARTIAL` — урон через Core |
| точный горизонтальный допуск луча (`AStar_coordExceedsBound`) | `PARTIAL` |
| Mage без оружия / с ближним оружием | `UNKNOWN`, атака отклоняется |
| вид летящей стрелы/магии в Alpha | `UNKNOWN`/R-3 |
| dodge/roll | `UNKNOWN` |
| ввод бомбы, способности | `PARTIAL`, F3 |
| визуальный осмотр человеком (размер волка, стрела в полёте) | не проводился; проверены меши, позиции, масштабы |

## 11. Blockers

Блокеров нет. Расхождение «снаряд или hitscan» разобрано по байтам (§2.1) и решено продюсером (§2.1).

Расхождение «масштаб вида не доказан» (F2A) закрыто эмуляцией `Creature_drawModel` (§2.3).

## 12. Изменённые файлы

| Файл | Что |
|---|---|
| `Assets/CubeGame/Features/Combat/AttackRoute.cs` (новый) | `AttackPath`, `ProjectileKind`, `AttackPlan`, `AlphaAttackRoute.Select/ForAction` |
| `Assets/CubeGame/Features/Combat/ProjectileSystem.cs` (новый) | `ProjectileRules` (байты), `Projectile`, `ProjectileSystem` (Launch/Update/FireRay/Clear) |
| `Assets/CubeGame/Features/Combat/CombatSystem.cs` | `TryStartAttack(AttackPlan)`, `Strike` по пути, `Projectiles`, `ActionSerialOf`, мёртвая цель без событий, `AttackSerial` |
| `Assets/CubeGame/Features/Combat/Combatant.cs` | hit box, прицел, браслет в off-hand |
| `Assets/CubeGame/Features/Contracts/GameplayEvents.cs` | `ProjectileSpawned`, `ProjectileRemoved`, `HitscanFired`, `HitResolved.AttackSerial` |
| `Assets/CubeGame/Content/AlphaCreatureSizes.cs`, `Generated/AlphaCreatureSizes.g.cs` (новые) | размер и масштаб размещения 156 видов |
| `tools/alpha_content/gen_creature_sizes.py` (новый) | генератор (alpha_static + эмуляция drawModel, проверки) |
| `Assets/CubeGame/Presentation/Gameplay/ProjectileVisuals.cs` (новый) | единственный владелец визуала снарядов/следов, уничтожение мешей |
| `Assets/CubeGame/Presentation/Gameplay/GameBootstrap.cs` | маршрут атаки без fallback, синхронизация героя, hit box видов, терраин снарядов, очистка, `ShiftOrigin`, `SpawnTrainingTargetAt` |
| `Assets/CubeGame/Presentation/Gameplay/GameVisualSink.cs` | эталон `ModelScale` = HumanMale, `SpeciesScale` для героя и NPC |
| `Assets/CubeGame/Presentation/Visual/Projectiles.cs` | пометка: не рантайм-путь |
| `Assets/CubeGame/Tests/FullM0/EditMode/FullM0ProjectileTests.cs` (новый) | 60+ EditMode случаев |
| `Assets/CubeGame/Tests/Creatures/EditMode/AttackRouteGripTests.cs` (новый) | хват F2A ↔ путь атаки |
| `Assets/CubeGame/Tests/FullM0/PlayMode/FullM0RangedCombatPlayModeTests.cs` (новый) | 6 PlayMode сценариев |
| коммит DeepSeek `08ae4e3` | `AlphaCombatRuntimeData.g.cs`, генератор, evidence JSON, 10 тестов, его отчёт |

## 13. EditMode

Unity 6000.3.27f2, полный `-runTests -testPlatform EditMode`, `AlphaOutput/f2b_editmode.xml`:

| Прогон | total | passed | failed | skipped | error CS |
|---|---|---|---|---|---|
| база F2A | 679 | 677 | 0 | 2 | 0 |
| **F2B** | **746** | **744** | **0** | 2 | **0** |

Skip — прежние два `FullM0ZoneOracleTests` (локальные дампы). Новое: путь по состоянию оружия (10 случаев) и по id действия (14), отказ без melee-fallback (3), ranged бьёт только снарядом (цель в секторе melee не задета), состояние стрелы по байтам, конфигурация магии/бумеранга, lifetime 5000 ровно один раз, hit-once, владелец/союзник/труп не поражаются, убитая цель без повторов, независимость от длины тика, терраин/незагруженный чанк, возврат бумеранга, объём посоха, бокс Alpha, `Clear`, пропавший владелец, melee hit-once + стабильный `AttackSerial`, hitscan (все на луче один раз, не владелец, 200, стена, бомба 10), след только для muzzle-id, таблица размеров (4), species scale (4), эталон, хват ↔ путь, плюс 10 тестов DeepSeek.

## 14. PlayMode

`FullM0RangedCombatPlayModeTests` в реальной сцене (`GameBootstrap`):

| Сценарий | Проверено |
|---|---|
| `MeleeSpawnAttackHitDamageDeath` | спаун → удары → урон → одна смерть; новый attack id на каждый взмах, один hit на взмах, ни одного снаряда |
| `BowArrowAppearsFliesCollidesHitsAndDisappears` | стартовый лук → стрела видна 7 кадров, визуал совпадает с игровой позицией, бокс → `ResolveHit` → удаление (`HitTarget`), визуал и меш уничтожены |
| `StaffVolumeAndWandMagicDamageTargets` | посох: объём ранит цель; жезл из сумки → магический снаряд ранит цель |
| `OriginShiftDuringFlightKeepsProjectileAndTargetConsistent` | смена начала координат в полёте; 34 кадра снаряд и цель совпадают с `ToScene`; попадание есть |
| `SaveWithProjectileInFlightReloadsIdle` | сейв в полёте → выход (`Cleared` ×1) → загрузка: 0 снарядов, бой idle, жезл на месте, новая атака работает |
| `SpeciesScaleIsOneRuleForHeroAndCreatures` | `ModelScale` одинаков в мирах человека и 0x04; герой 0x04 = 0.833 человека; волк = `ModelScale × 1.583` в обоих |

### 14.1 Полный PlayMode

Unity 6000.3.27f2, полный `-runTests -testPlatform PlayMode`, `AlphaOutput/f2b_playmode.xml`:

| Прогон | total | passed | failed | skipped | error CS |
|---|---|---|---|---|---|
| база F2A | 62 | 62 | 0 | 0 | 0 |
| **F2B** | **68** | **68** | **0** | **0** | **0** |

Лог: стрела видна 7 кадров, 1 меш уничтожен, проблем визуала нет; origin shift проверен на 16 кадрах; `ModelScale` 0.7726, тело человека 1.739, героя 0x04 — 1.449 (0.833), масштаб волка 1.2233 = 0.7726 × 1.583. Прежние `FullM0CombatPlayModeTests` (рост героя 1.8, действие 57, убийство волка, save/load) прошли без изменений.

## 15. Handoff в F3

1. Формулы урона Alpha для снарядов/луча/посоха (`stat_pow`, `charge`) вместо общего Core-резолва.
2. Ввод способностей и бомбы (`0x68`); таблицы DeepSeek готовы (`AlphaCombatRuntimeData`).
3. `CharacterController` героя по размеру вида (сейчас 1.8 у всех рас).
4. Подруливание бумеранга, muzzle-позиция `0x446BB0`, точный допуск луча.
5. R-3/R-8: настоящий меш следа/снаряда и эффекты попадания (если появится адаптер рендереров).
6. Визуальный осмотр: волк ×1.583 и стрела в полёте (метрики проверены, глаз — нет).
