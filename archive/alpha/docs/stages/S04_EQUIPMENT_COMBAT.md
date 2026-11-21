# S04 — EQUIPMENT & COMBAT INTEGRATION (первая партия)

**Проект:** CubeGame (рабочее название не утверждено). **Этап:** `S04-EQUIPMENT-COMBAT`.
**Статус:** `AUTHORIZED / IN_PROGRESS` (решение продюсера 2026-11-03, [D45](../decisions/D45-s03-accepted-s04-authorized.md)); по завершении — `READY_FOR_REVIEW`, **не** `ACCEPTED`.
**Ветка:** `stage/s04-equipment-combat` от принятого коммита S03 (`stage/s03-combat-core` @ `70a0658`).
**Unity:** `6000.3.27f2` (Unity 6.3 LTS), Built-in RP, без новых пакетов.
**Архитектура:** [ADR-0001](../architecture/ADR-0001-modular-monolith.md) — чистая логика в `CubeGame.Core`; Unity-слой только демонстрация.
**Числа:** [D42](../decisions/D42-alpha-numbers-as-references.md) — статусы достоверности обязательны.

## 1. Цель

Соединить уже существующие системы — `ItemInstance`, `ItemStatsCalculator`, `CharacterSheet`, `CharacterStats`, `CombatAttack`, `DamageResolver`, `HealthPool` — так, чтобы экипированные предметы влияли на боевой расчёт через **существующие** вычислители. Параллельная система статов запрещена.

## 2. Состав партии

| Модуль | Файлы | Содержание |
|---|---|---|
| A — Equipment Slots | `Core/Equipment/EquipmentSlot.cs`, `EquipmentLoadout.cs` | слоты `MainHand`, `OffHand`, `Chest`, `Gloves`, `Boots`, `Shoulders`; `Equip`/`Unequip`/`GetEquipped`, замена, проверка типа, запрет дублирования экземпляра, атомарность |
| B — Stat Aggregation | `Core/Equipment/EquipmentStats.cs` | `ItemComputedStats` на предмет и `EquipmentStatSummary`; урон оружия, `Armor`, `BlockValue` — раздельно |
| C — Combat Adapter | `Core/Equipment/EquipmentCombatAdapter.cs` | `CharacterSheet + надетые предметы → CombatAttack`; аддитивный вклад оружия без двойного `levelFactor`; защита цели из `BlockValue` |
| D — Character Stats Integration | тот же адаптер (`ComputeMaxHealth`) | уровень, базовая база, флаги апгрейдов, MaxHP; OQ-25 не подменяется |
| E — Unity Demo | `Presentation/Equipment/EquipmentCombatDemo.cs` | смена оружия/брони, расчёт характеристики, удар, различие результатов при одном seed |

Дополнительно: в `CombatAttack` добавлено поле `ItemWeaponDamageBonus` (аддитивный вклад `Σ item_weaponDamage`, `PARTIAL`); `DamageResolver` учитывает его до разброса, порядок RNG не изменён.

## 3. Обязательные ограничения

| Правило | Почему |
|---|---|
| Никакого двойного `levelFactor` | `ItemStatsCalculator.WeaponDamage` уже содержит уровень предмета; `CharacterStats.CalcWeaponDamage` умножает на уровень персонажа |
| Вклад предмета аддитивен (`E2`: `base(0x180)·levelFactor + флаги + Σ item_weaponDamage`) | восстановленная структура формулы |
| `Armor` (`item_computeStat_armor`) ≠ `BlockValue` (`item_computeBlockValue`) | разные величины; в защиту существа Alpha добавляет блок (E3) |
| OQ-25 `OPEN` | `+8 HP` из экипировки не объяснён; HP от предметов не «додумывается» |
| Частичные механики выключены по умолчанию | модификатор шанса по подтипу оружия, множители ID атак (`PARTIAL`) |
| `HitChance` и `BlockFraction` передаёт вызывающий код | OQ-01 `UNKNOWN`, OQ-24 `PARTIAL` |
| Формат сохранений не меняется | слоты требуют отдельного контракта и миграции (см. S04_REPORT §6) |
| Правила двуручного оружия/щита/классов не реализуются | не утверждены исследованиями |

## 4. Тесты

**EditMode:** совместимость слотов, замена, снятие, пустой слот, запрет дублирования экземпляра, атомарность при ошибке, влияние параметров предмета, два оружия, отсутствие двойного `levelFactor`, агрегация брони, разделение урона/брони, OQ-25, безопасное поведение без оружия, частичные модификаторы выключены, повторяемость seed, регрессия S02/S03.

**PlayMode:** экипировка меняет результат боя, замена обновляет характеристики без пересоздания сцены, снятие возвращает базовое состояние, броня меняет защиту и результат, смерть цели работает, сцена/генерация S01 цела.

**Прочее:** полный прогон (не только новые тесты), `alpha_curves/items/combat --check`, `git diff --check`.

## 5. Запрещено

Инвентарь и drag-and-drop, модели, анимации, VFX, AI, сложные хитбоксы, URP, запуск Alpha, изменение баланса, merge в `main`, force push, самостоятельное присвоение `ACCEPTED`.

## 6. Ссылки

- [D45 — S03 принят, S04 авторизован](../decisions/D45-s03-accepted-s04-authorized.md)
- [E_combat.md](../research/alpha2013/BEHAVIOR_SPECS/E_combat.md) (§E2 слоты и урон, §E3 броня/блок)
- [F_items.md](../research/alpha2013/BEHAVIOR_SPECS/F_items.md) (статы предметов)
- [S04_REPORT.md](S04_REPORT.md) — результаты и контракт сохранения экипировки
