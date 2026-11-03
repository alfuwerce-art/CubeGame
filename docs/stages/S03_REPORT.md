# S03-COMBAT-CORE — отчёт о выполнении первой партии боевой системы

**Проект:** CubeGame. **Этап:** `S03-COMBAT-CORE` ([ТЗ](S03_COMBAT_CORE.md), [D44](../decisions/D44-s02-accepted-s03-authorized.md)).
**Ветка:** `stage/s03-combat-core` (от принятого S02 `stage/s02-core-foundation` @ `1df7413`).
**Дата прогона:** 2026-11-03, MSK. **Unity:** `6000.3.27f2`. Тесты выполнены локально **DeepSeek**, не независимо ChatGPT.
**Статус:** `READY_FOR_REVIEW` — **не** `ACCEPTED`.

## 1. Итог одной таблицей

| Проверка | Результат | Артефакт |
|---|---|---|
| Компиляция проекта с боевым ядром | **PASS** (exit 0, без `error CS`/`warning CS`) | `.unity-install/logs/s03_import.log` |
| EditMode (полный прогон) | **PASS 232/232**, failed=0 | `.unity-install/results/s03_editmode3.xml` |
| PlayMode (полный прогон) | **PASS 10/10**, failed=0 | `.unity-install/results/s03_playmode.xml` |
| Регрессия S01 (сцена/генерация) | **PASS** — EditMode 10/10 (`ChunkMeshTests`, `TerrainDeterminismTests`), PlayMode 2/2 (`PilotPlayModeTests`) | те же XML |
| Регрессия S02 | **PASS** — `ProgressionTests` 51, `CharacterStatsTests` 22, `ItemStatsTests` 65, `PersistenceTests` 28; PlayMode S02 4/4 | те же XML |
| `alpha_curves_reference.py --check` | **PASS**, exit 0 | — |
| `alpha_item_stats_reference.py --check` | **PASS**, exit 0 | — |
| `alpha_combat_reference.py --check` | **PASS**, exit 0 | — |
| `git diff --check` | чисто, exit 0 | — |

**Новых тестов:** +62 EditMode (170 → 232), +4 PlayMode (6 → 10).
Разбивка EditMode по фикстурам: `CombatFormulaTests` 39, `CombatResolutionTests` 17, `ItemStatsTests` 65 (+6 к S02), `ProgressionTests` 51, `PersistenceTests` 28, `CharacterStatsTests` 22, `ChunkMeshTests` 5, `TerrainDeterminismTests` 5.

## 2. Что реализовано

```
Assets/CubeGame/Core/Combat/           (CubeGame.Core, noEngineReferences = true)
  CombatRandom.cs        ICombatRandom, SeededCombatRandom (xorshift32 → 0..32767), FixedCombatRandom
  CombatFormulas.cs      урон оружия, разброс 1.25−0.5·roll/32767, таблица ID атак (PARTIAL, изолирована),
                         порог/бросок блока, модификатор шанса по подтипу оружия (PARTIAL, изолирован)
  HitResolver.cs         hit = roll/32767 < chance, HitOutcome (Hit/Miss), диагностика
  DamageMitigation.cs    final = max(0, rawDamage − blockFraction·armor), MitigationResult
  HealthPool.cs          текущее/максимальное HP, урон, лечение с клампом, смерть, валидация
  CombatOutcome.cs       неизменяемый результат: статус, броски, урон, защита, HP до/после, смерть
  CombatAttack.cs        вход: база оружия, уровень, шанс, флаги, ID атаки (опционально), броня/доля блока
  DamageResolver.cs      AttackInput → HitCheck → DamageCalculation → Mitigation → HealthPool → Outcome
Assets/CubeGame/Presentation/Combat/
  CombatDemo.cs          тестовая цель (примитив), одна команда Strike(), лог результата
```

**Порядок и потребление RNG (зафиксировано и проверено тестами):** мёртвая цель — 0 бросков; промах — 1 бросок; попадание — 2 броска (попадание + разброс); HP меняется последним.

**Замечание ревью S02 исправлено:** конструктор `ItemInstance` теперь валидирует `ItemModifier` через ту же функцию, что и загрузчик сохранений (`TryValidateValues`); добавлены негативные тесты и проверка «конструктор и путь сохранений согласованы».

## 3. Таблица механик и достоверности

| Механика | Статус | Где | Как проверено |
|---|---|---|---|
| Урон оружия `base(0x180)·levelFactor` | `REFERENCE_VERIFIED` | `CombatFormulas.WeaponDamage` | таблица баз 1/2, уровни 1/5/11 |
| Флаги апгрейдов `0x7e & 0x8` / `0x10` | `REFERENCE_VERIFIED` (частично) | `CharacterStats.CalcWeaponDamage` | case-таблица 1.0/3.0/9.0/11.0 |
| Разброс урона `1.25 − 0.5·roll/32767` | `STRONGLY_SUPPORTED` | `CombatFormulas.DamageVariance` | границы 1.25/0.75, середина ≈1.0, монотонность |
| Проверка попадания `roll/32767 < chance` | `REFERENCE_VERIFIED` | `HitResolver` | границы `chance` 0/1 и `roll` 0/32767 |
| Модификатор шанса по подтипу оружия | `PARTIAL` | `ApplyWeaponSubtypeHitModifier` | таблица ×0.5/×0.3 + «только при явном типе слота» |
| Множители по ID атак 0x57/0x5B/0x5D/0x60 | `PARTIAL` | `TryGetAttackIdMultiplier`, `ApplyAttackIdMultiplier` | таблица + «только по явному флагу» |
| Порог блока `int(curve05·100+1)` | `REFERENCE_VERIFIED` | `BlockThreshold` | 1→1, 2→5, 5→17, 10→32, 20→49, 50→72 |
| Бросок блока `roll ≤ порог` | `PARTIAL` (полный алгоритм не восстановлен) | `RollBlock` | границы порога |
| Снижение урона `raw − blockFraction·armor`, кламп 0 | `PARTIAL` | `DamageMitigation` | контроль 30/10/0.5 → 25; кламп в 0 |
| Пул здоровья, смерть, лечение с клампом | собственная модель CubeGame | `HealthPool` | overkill, лечение сверх максимума, мёртвая цель |
| Базовая формула `chance` | `UNKNOWN` (OQ-01) | — | передаётся явно; значения по умолчанию нет |
| Автоматическая доля блока (`+0x70`) | `PARTIAL` (OQ-24) | — | передаётся явно, не вычисляется |
| Порядок «блок → броня → HP» | `PARTIAL` (OQ-02) | `DamageResolver` | один порядок, отмечен как частичный |

## 4. Демонстрация расчёта урона

**Разбор вручную (совпадает с тестом `ControlCaseFromResearchReachesHealthPool`):**

```
weaponBase = 24, level = 1        → baseDamage = 24 · levelFactor(1) = 24
variance roll = 0                → variance = 1.25 − 0.5·0/32767 = 1.25
rawDamage = 24 · 1.25 = 30
armor = 10, blockFraction = 0.5  → absorbed = 5, final = max(0, 30 − 5) = 25
HP 100 → 75, death = false
```

**Фактические строки лога PlayMode-прогона (боевая демонстрация):**

```
[Combat] status=Hit chance=1.000 hitRoll=20 base=1.00 variance=1.172 raw=1.17 armor=0.00
         blockFraction=0.000 absorbed=0.00 applied=1.17 hp=100.00->98.83 dead=False
[Combat] status=Hit chance=1.000 hitRoll=28 base=1.00 variance=1.140 raw=1.14 armor=0.00
         blockFraction=0.000 absorbed=0.00 applied=0.50 hp=0.50->0.00 dead=True
[Combat] status=TargetAlreadyDead chance=0.000 hitRoll=-1 ... applied=0.00 hp=0.00->0.00 dead=True
```

Вторая строка показывает кламп урона текущим HP (raw 1.14 → снято 0.50) и фиксацию смерти; третья — повторный удар по мёртвой цели не меняет HP.

## 5. Найденные ошибки и исправления по ходу партии

1. **Дефолт `CombatAttack.EquippedItemType = 3` включал частичный модификатор шанса.** Статистический тест (chance = 0.5 на 10 000 ударов) дал 2 545 попаданий вместо ожидаемых ~5 000: подтип оружия по умолчанию (0) попадал в группу `×0.5`, эффективный шанс становился 0.25. Исправлено: тип слота по умолчанию 0 («не оружие»), частичный модификатор применяется только по явной настройке; добавлен тест `WeaponSubtypeModifierIsAppliedOnlyWhenExplicitlyConfigured`. Это ровно тот случай, когда частичная механика не должна работать «по умолчанию».
2. **Тест на потребление бросков был некорректен** (`FixedCombatRandom` исчерпывался, потому что бросок 16000 при chance 0.5 давал попадание). Исправлено на бросок 30000 (промах в обоих случаях); сам резолвер не менялся.
3. **Единая валидация модификатора** (замечание ревью S02): конструктор `ItemInstance` раньше не проверял `ItemModifier`. Исправлено без изменения известных значений модификаторов, добавлены негативные тесты.

## 6. Ограничения и что осталось неподтверждённым

| Ограничение | Статус |
|---|---|
| Базовая формула `chance` (уровень/звёзды/оружие) | `UNKNOWN` — **OQ-01**, передаётся явно |
| Порядок митигации (блок → броня → HP) и роль `+0x174` | `PARTIAL` — **OQ-02** |
| Автоматическая доля блока из боевой серии (`+0x70`) | `PARTIAL` — **OQ-24**, передаётся явно |
| Семантика модификаторов шанса по подтипу оружия | `PARTIAL`, изолировано |
| Место применения множителей ID атак, названия способностей | `PARTIAL`, только по явному флагу |
| Пул здоровья/смерть | собственная модель CubeGame (не паритет Alpha): без анимаций, дропа, опыта, респавна |
| Способности, комбо, тайминги, элементальные проки, threat, poise | вне S03 |
| Множитель `abilityFactor · (ratio²·5.0 + base)` | вне S03 (нет подтверждённых входов `ratio`/`base`) |
| HP после level-up, OQ-22/OQ-23 | по-прежнему `NOT_VERIFIED/BLOCKED` из S00/S02 |
| Статус S03 | `READY_FOR_REVIEW`, приёмку делает продюсер/ChatGPT |

**Осознанно не реализовано** (чтобы не выдавать догадку за Alpha): элементальные проки, «ярость»/threat-таблица, poise/стан, лечение отрицательным уроном по формуле Alpha, авто-блок, комбо-счётчик `+0x70`, любые тайминги атак.

## 7. Критерии завершения партии

- [x] Компиляция без ошибок и предупреждений CS.
- [x] Полный EditMode/PlayMode прогон зелёный, регрессии S01/S02 внутри.
- [x] Боевое ядро не зависит от UnityEngine (`CubeGame.Core`, `noEngineReferences: true`).
- [x] Подтверждённые формулы сверены эталонными скриптами.
- [x] Частичные механики изолированы и выключены по умолчанию.
- [x] Неизвестные величины передаются явно.
- [x] Результат удара воспроизводим при одинаковом seed.
- [x] Код опубликован в ветке `stage/s03-combat-core`.

## 8. Рекомендация по следующей партии

1. Ревью S03 (в первую очередь контракты `DamageResolver`/`HealthPool` и границы `PARTIAL`).
2. Следующая партия (требует решения продюсера): подключение реального оружия/экипировки к `CombatAttack` (бонусы предметов уже есть в S02), затем — тайминги атак из `E4` (нужны таблицы ID способностей) **или** threat/poise-слой, но по одному слою за партию.
3. OQ-01/OQ-02/OQ-24 закрывать только статическим разбором с доказательствами; новых запусков Alpha нет.
