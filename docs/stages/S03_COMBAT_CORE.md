# S03 — COMBAT CORE (первая партия боевой системы)

**Проект:** CubeGame (рабочее название не утверждено). **Этап:** `S03-COMBAT-CORE`.
**Статус:** **`ACCEPTED`** — решение продюсера **2026-11-03** ([D45](../decisions/D45-s03-accepted-s04-authorized.md)); архитектурное ревью ChatGPT — `PASS`, исправления `S03-REVIEW-FIX-01` приняты. Ранее: `AUTHORIZED / IN_PROGRESS` ([D44](../decisions/D44-s02-accepted-s03-authorized.md)) → `READY_FOR_REVIEW`. Приёмка ограниченная: боевое ядро как техническая основа, а не паритет с Alpha.
**Ветка:** `stage/s03-combat-core` от принятого коммита S02 (`stage/s02-core-foundation` @ `1df7413`).
**Unity:** `6000.3.27f2` (Unity 6.3 LTS), Built-in RP, без новых пакетов.
**Архитектура:** [ADR-0001](../architecture/ADR-0001-modular-monolith.md) — чистая логика в `CubeGame.Core` без UnityEngine; Unity-слой только демонстрация.
**Числа:** [D42](../decisions/D42-alpha-numbers-as-references.md) — числа Alpha как референсы со статусами достоверности.

## 1. Цель

Минимальное боевое ядро, которое умеет: получить характеристики атакующего и цели, рассчитать подтверждённую часть урона, проверить попадание, применить известные правила брони/защиты, изменить HP, определить смерть и вернуть воспроизводимый результат удара.

Полноценная боевая система (способности, комбо, AI, анимации, VFX, тайминги) в эту партию **не входит**.

## 2. Состав партии

| Модуль | Файлы | Содержание |
|---|---|---|
| A — Combat Formulas | `Core/Combat/CombatFormulas.cs` | урон оружия (база · `levelFactor` + подтверждённые флаги), разброс `1.25 − 0.5·roll/32767`, таблица множителей ID атак (изолированно), порог и бросок блока, модификатор шанса по подтипу оружия (изолированно) |
| B — Hit Resolution | `Core/Combat/HitResolver.cs`, `Core/Combat/CombatRandom.cs` | `hit = roll/32767 < chance`, `HitOutcome`, детерминированные источники RNG (`SeededCombatRandom`, `FixedCombatRandom`) |
| C — Armor / Block Mitigation | `Core/Combat/DamageMitigation.cs` | `defenseCapacity = blockFraction·armor` (диагностика), `absorbed = min(raw, defenseCapacity)`, `final = max(0, raw − absorbed)`; подробный результат снижения |
| D — Health Pool / Outcome | `Core/Combat/HealthPool.cs`, `Core/Combat/CombatOutcome.cs` | текущее/максимальное HP, урон, лечение с клампом, смерть, запрет отрицательного HP, диагностика результата |
| E — Damage Resolver | `Core/Combat/DamageResolver.cs`, `Core/Combat/CombatAttack.cs` | единый процесс `AttackInput → HitCheck → DamageCalculation → Mitigation → HealthPool → CombatOutcome` |
| F — Unity-демонстрация | `Presentation/Combat/CombatDemo.cs` | компонент с тестовой целью, одна команда `Strike()`, вывод результата в лог |

Дополнительно (замечание ревью S02): унифицирована валидация `ItemModifier` между конструктором `ItemInstance` и путём сохранений.

## 3. Границы достоверности (обязательно к соблюдению)

| Механика | Статус | Реализация |
|---|---|---|
| Урон оружия `base·levelFactor` (+ флаги `0x8`/`0x10`) | `REFERENCE_VERIFIED` | `CombatFormulas.WeaponDamage` (использует S02) |
| Разброс `1.25 − 0.5·roll/32767`, диапазон 0.75…1.25 | `STRONGLY_SUPPORTED` | `CombatFormulas.DamageVariance` |
| Попадание `roll/32767 < chance` | `REFERENCE_VERIFIED` | `HitResolver` |
| Порог блока `int((1−1/((L−1)·0.05+1))·100+1)` | `REFERENCE_VERIFIED` | `CombatFormulas.BlockThreshold` |
| Множители ID атак `0x57 ×50`, `0x5B ×2`, `0x5D ×5`, `0x60 ×3` | `PARTIAL` | `CombatFormulas` + **только** при `CombatAttack.UseAttackIdMultiplier = true` |
| Модификаторы шанса по подтипу оружия (`×0.5`, `×0.3`) | `PARTIAL` | `ApplyWeaponSubtypeHitModifier` + только при явно заданном типе слота оружия |
| Снижение урона: `absorbed = min(raw, blockFraction·armor)`, `final = max(0, raw − absorbed)` | `PARTIAL` | `DamageMitigation`; контрольный случай 30/10/0.5 → absorbed 5, final 25; `DefenseCapacity` — диагностическая величина |
| Базовая формула `chance` персонажа | `UNKNOWN` (OQ-01) | шанс передаёт вызывающий код, значения по умолчанию нет |
| Автоматическая доля блока из боевой серии (`+0x70`) | `PARTIAL` (OQ-24) | `blockFraction` передаётся явно, не вычисляется |
| Порядок «блок → броня → HP» | `PARTIAL` (OQ-02) | реализован один порядок, отмечен как частичный |

## 4. Обязательные тесты

**EditMode:** урон по референсам; множители ID атак; границы разброса; попадание на границах (`chance=0/1`, `roll=0/32767`); применение брони; частичная модель блока; урон не отрицателен; HP не ниже нуля; лечение не выше MaxHP; повторный удар по мёртвой цели; некорректные входы; повторяемость RNG по seed; разные последовательности при разных seed; отсутствие изменения HP при ошибке; регрессия S02.

**PlayMode:** цель получает урон, HP уменьшается на рассчитанную величину, смерть фиксируется, повторный удар игнорируется; регрессия сцены S01 и модулей S02.

**Прочее:** полный прогон EditMode/PlayMode (не только новые тесты), `alpha_curves_reference.py --check`, `alpha_item_stats_reference.py --check`, `alpha_combat_reference.py --check`, `git diff --check`.

## 5. Критерии приёмки партии (для ревью, не для самоприёмки)

- [ ] Компиляция Unity без ошибок и предупреждений.
- [ ] Полный EditMode/PlayMode прогон зелёный, включая регрессии S01/S02.
- [ ] Боевое ядро не зависит от UnityEngine (`CubeGame.Core`).
- [ ] Подтверждённые формулы воспроизводятся эталонами.
- [ ] Частичные механики изолированы и не применяются по умолчанию.
- [ ] Неизвестные величины (`chance`, `blockFraction`) передаются явно, а не выдумываются.
- [ ] Результат удара воспроизводим при одинаковом seed.
- [ ] Код опубликован в ветке `stage/s03-combat-core`.

## 6. Запрещено в S03

AI/NPC, полная система навыков, новые классы и баланс, анимации/VFX, переделка генерации мира, URP, инвентарь/лут, запуск Alpha без разрешения, копирование закрытого кода/ассетов, merge в `main`, force push, самостоятельное присвоение статуса `ACCEPTED`, закрытие OQ-01/OQ-02/OQ-24/OQ-25/OQ-26 без новых доказательств.

## 7. Ссылки

- [D44 — S02 принят, S03 авторизован](../decisions/D44-s02-accepted-s03-authorized.md)
- [E_combat.md](../research/alpha2013/BEHAVIOR_SPECS/E_combat.md) (§E2, §E3, §E5, §E6)
- [alpha_combat_reference.py](../research/alpha2013/tools/alpha_combat_reference.py)
- [S03_REPORT.md](S03_REPORT.md) — фактические результаты прогона
