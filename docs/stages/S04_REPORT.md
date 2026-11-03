# S04-EQUIPMENT-COMBAT — отчёт о первой партии

**Проект:** CubeGame. **Этап:** `S04-EQUIPMENT-COMBAT` ([ТЗ](S04_EQUIPMENT_COMBAT.md), [D45](../decisions/D45-s03-accepted-s04-authorized.md)).
**Ветка:** `stage/s04-equipment-combat` (от принятого S03 `stage/s03-combat-core` @ `70a0658`).
**Дата прогона:** 2026-11-03, MSK. **Unity:** `6000.3.27f2`. Тесты выполнены локально **DeepSeek**, не независимо ChatGPT.
**Статус:** `READY_FOR_REVIEW` — **не** `ACCEPTED`.

> Примечание к задаче: переданный текст ТЗ обрывался на разделе «## 12». Работа выполнена в объёме разделов 1–11 и стандартных требований проекта (D42/ADR-0001, отдельная ветка, обычный push, `READY_FOR_REVIEW`, без merge в `main`), зафиксированных в `AGENTS.md` и [D45](../decisions/D45-s03-accepted-s04-authorized.md).

## 1. Итог одной таблицей

| Проверка | Результат | Артефакт |
|---|---|---|
| Компиляция Unity | **PASS** (exit 0, без `error CS`/`warning CS`) | `.unity-install/logs/rf04_import.log` |
| EditMode (полный прогон, после REVIEW-FIX-01) | **PASS 296/296**, failed=0 | `.unity-install/results/rf04_editmode.xml` |
| PlayMode (полный прогон, после REVIEW-FIX-01) | **PASS 17/17**, failed=0 | `.unity-install/results/rf04_playmode.xml` |
| Регрессия S01 | **PASS** — EditMode 10/10 (`ChunkMeshTests`, `TerrainDeterminismTests`), PlayMode 2/2 (`PilotPlayModeTests`) | те же XML |
| Регрессия S02 | **PASS** — `ProgressionTests` 51, `CharacterStatsTests` 22, `ItemStatsTests` 65, `PersistenceTests` 28 | те же XML |
| Регрессия S03 | **PASS** — `CombatFormulaTests` 53, `CombatResolutionTests` 23, PlayMode боя 4/4 | те же XML |
| Новые тесты S04 | **+36 EditMode** (Loadout 16, Stats 10, Integration 10), **+7 PlayMode** | `s04_editmode2.xml`, `s04_playmode2.xml` |
| REVIEW-FIX-01 | **+8 EditMode** (Loadout 16→20, Stats 10→14), PlayMode без изменений | `rf04_editmode.xml` |
| `alpha_curves_reference.py --check` | **PASS**, exit 0 | — |
| `alpha_item_stats_reference.py --check` | **PASS**, exit 0 | — |
| `alpha_combat_reference.py --check` | **PASS**, exit 0 | — |
| `git diff --check` | чисто, exit 0 | — |

База на входе: 252 EditMode (после закрытия S03 был добавлен тест overkill) / 10 PlayMode. Итог: **288 EditMode / 17 PlayMode**.

## 2. Что реализовано

```
Assets/CubeGame/Core/Equipment/        (CubeGame.Core, noEngineReferences = true)
  EquipmentSlot.cs           слоты MainHand, OffHand, Chest, Gloves, Boots, Shoulders (E2 §E3)
  EquipmentLoadout.cs        Equip/Unequip/GetEquipped, замена, таблица совместимости,
                             запрет одного экземпляра в двух слотах, атомарные операции
  EquipmentStats.cs          ItemComputedStats (на предмет) + EquipmentStatSummary:
                             урон оружия, Armor, BlockValue — раздельно, без своих формул
  EquipmentCombatAdapter.cs  CharacterSheet + надетые предметы → CombatAttack;
                             ComputeTargetArmor (Σ BlockValue), ComputeMaxHealth (OQ-25)
Assets/CubeGame/Core/Combat/
  CombatAttack.cs            + ItemWeaponDamageBonus (аддитивный вклад, PARTIAL)
  DamageResolver.cs          учитывает бонус до разброса; порядок RNG не изменён
Assets/CubeGame/Presentation/Equipment/
  EquipmentCombatDemo.cs     техническая демонстрация: смена оружия/брони, удар, разница
```

**Формула адаптера (явно, `PARTIAL`):**

```
WeaponBase            = CharacterSheet.WeaponBase          (base 0x180, по умолчанию 1.0)
ItemWeaponDamageBonus = Σ ItemStatsCalculator.WeaponDamage(надетое оружие)
baseDamage в бою      = WeaponBase · levelFactor(уровень персонажа) + флаги + ItemWeaponDamageBonus
TargetArmor           = Σ ItemStatsCalculator.BlockValue(броня цели)      (E3, PARTIAL)
MaxHealth             = CharacterStats.CalcMaxHealth(..., sheet.EquipmentHealthBonus)  (OQ-25)
```

Никакого двойного масштабирования: урон предмета не передаётся как `WeaponBase`, а его `levelFactor`/редкость уже учтены внутри `ItemStatsCalculator.WeaponDamage`. Проверено тестом `WeaponContributionIsNotMultipliedByCharacterLevelTwice` (уровень 11 даёт 4.0, а не 6.0).

## 3. Таблица механик и достоверности

| Механика | Статус | Проверка |
|---|---|---|
| Слоты экипировки и совместимость типов | собственная модель CubeGame (слоты по E2) | 16 тестов Loadout |
| Статы предметов (урон/броня/блок) | `REFERENCE_VERIFIED` (S02) | таблицы `alpha_item_stats_reference.py` |
| Аддитивный вклад оружия в урон | `PARTIAL` (структура E2, порядок не доказан) | Integration: base 3.0 / 9.0 / 11.0, без двойного lf |
| Защита цели из `BlockValue` брони | `PARTIAL` (E3) | `TargetArmorFromEquipmentChangesAppliedDamage` |
| `Armor` ≠ `BlockValue` | разделено | `ArmorStatIsNotUsedAsTargetArmor` |
| HP от экипировки | `UNKNOWN` (OQ-25 `OPEN`) | MaxHP не меняется; явный `EquipmentHealthBonus` = 268 работает |
| Частичный модификатор шанса по подтипу | `PARTIAL`, выключен по умолчанию | `PartialHitModifierIsOffByDefaultAndExplicitWhenEnabled` |
| Множители ID атак | `PARTIAL`, не включаются адаптером | `AdapterBuildsAttackFromSheetAndWeapon` |
| Правила двуручного оружия/щита/классов | **не реализованы** (не утверждены) | — |

## 4. Демонстрация (фактические строки лога PlayMode)

```
[Equipment] scenario: weapon=sword     main=2,00 weaponBonus=2,00 => base=3.00 variance=0.950 raw=2.85
[Equipment] scenario: weapon=greatmace main=8,00 weaponBonus=8,00 => base=9.00 variance=0.950 raw=8.55
[Equipment] scenario: weapon=none      main=0,00 weaponBonus=0,00 => base=1.00 variance=0.950 raw=0.95
[Equipment] scenario: targetArmor=0,00->2,70 => armor=2.70 (защита цели из нагрудника)
```

Один seed (`4242`) и одинаковые параметры цели → одинаковые броски (`hitRoll=16472`, `variance=0.950`); разница в уроне объясняется только экипировкой. Смена брони меняет вычисляемую защиту цели и итоговый урон.

## 5. Найденные ошибки и ограничения

1. **Ошибки в собственных ожиданиях тестов** (не в коде): при произвольном seed разброс не равен 1.25; три проверки переписаны на инварианты (`RawDamage = BaseDamage · VarianceMultiplier`, `BaseDamage` независим от seed). Числовые эталоны статов и формул не менялись.
2. **`Strike()` создаёт свежую цель на каждый удар** (честное сравнение ударов) — поэтому повторный удар по мёртвой цели проверяется в S03 `CombatDemo` и EditMode-тестах, а в S04-демо проверяется корректная фиксация смерти одного удара.
3. **Связь статов предмета с боем не доказана полностью** → адаптер `PARTIAL`, формула выписана явно; `Armor` не используется как `TargetArmor`.
4. **OQ-25 остаётся `OPEN`**: HP от экипировки не выводится; MaxHP не меняется при смене брони — это ожидаемое ограничение партии.
5. **Двуручное оружие, щит, классовые ограничения, «какая рука атакует»** — не реализованы; два оружия складываются в аддитивный вклад (как список `item_weaponDamage`), что помечено `PARTIAL` и требует решения продюсера/исследования.
6. Открытый вопрос партии: **S04-OQ-01** — правила сочетаний оружия, двуручного оружия, щита и классовых ограничений; **S04-OQ-02** — выбор атакующей руки при двух оружиях.

## 5a. S04-REVIEW-FIX-01 (2026-11-03) — защита коллекций

| FIX | Что исправлено | Доказательство |
|---|---|---|
| **FIX-01** — `EquipmentLoadout.Slots` | Свойство возвращало внутренний статический массив `AllSlots`; через приведение `IReadOnlyList → EquipmentSlot[]` его можно было изменить извне. Теперь публикуется `ReadOnlyCollection<EquipmentSlot>` (`Array.AsReadOnly`): приведение к массиву невозможно, запись/добавление/удаление через `IList<EquipmentSlot>` бросает `NotSupportedException`. Публичный контракт `IReadOnlyList<EquipmentSlot>` и порядок шести слотов сохранены; `Equip`/`Unequip`/`GetEquipped`/`EquippedCount`/`IsSlotCompatible` не менялись | `SlotsCannotBeCastToArray`, `SlotsCannotBeModifiedThroughIList`, `SlotsKeepOrderAndNumericValues`, `LoadoutWorksAfterMutationAttempt` (EquipmentLoadoutTests 16 → 20) |
| **FIX-02** — `EquipmentStatSummary.PerItem` | Резюме хранило переданную коллекцию как есть: изменение исходного списка после создания (или приведение к изменяемому интерфейсу) могло поменять результат агрегирования. Теперь конструктор делает защитную копию и публикует `ReadOnlyCollection`: изменение источника не влияет, `IList<ItemComputedStats>` бросает `NotSupportedException`. Формулы, агрегирование и порядок элементов не изменялись | `PerItemIsDefensiveCopyOfInputCollection`, `PerItemCannotBeModifiedThroughIList`, `PerItemKeepsOrderAndValues`, `RepeatedComputationProducesStableResult`, плюс `new EquipmentStatSummary(null, …)` → `ArgumentNullException` (EquipmentStatsTests 10 → 14) |

**Что не менялось:** формулы урона и `levelFactor`, `DamageResolver` и порядок RNG, баланс и характеристики предметов, правила двуручного оружия/щитов/классов, формат сохранений, модели/анимации/графика, исследовательский контур Alpha. Статусы OQ сохранены. D46 и ветка `stage/s05-asset-conversion-plan` не затрагивались.

**Итог прогона REVIEW-FIX-01 (Unity 6000.3.27f2, DeepSeek):** компиляция PASS без `error CS`/`warning CS`; EditMode **296/296 passed** (+8), PlayMode **17/17 passed**, failed=0; регрессии S01/S02/S03 внутри прогона; Python-эталоны `alpha_curves`/`alpha_items`/`alpha_combat --check` — PASS (exit 0); `git diff --check` — чисто. Артефакты: `.unity-install/results/rf04_editmode.xml`, `.unity-install/results/rf04_playmode.xml`, логи `rf04_*.log`.

## 6. Контракт будущего сохранения экипировки (не реализован)

Формат S02 (`CUBEGAME_SAVE` v1: персонаж + список предметов) **не менялся**; все save/load тесты S02 зелёные. Слоты в сохранение не пишутся.

Предлагаемый контракт следующей партии (требует отдельного решения и миграции версии):

| Требование | Значение |
|---|---|
| Идентификатор экземпляра | стабильный `ItemInstanceId` (int64 или GUID) на каждый предмет; сейчас идентичность — набор полей, для слотов этого мало |
| Запись слота | `slotId` (enum `EquipmentSlot`) + `itemId`; слот без предмета не пишется |
| Целостность | каждый `itemId` слота обязан присутствовать в списке предметов; один предмет — не более одного слота |
| Совместимость | слоты проверяются той же таблицей `EquipmentLoadout.IsSlotCompatible` |
| Версия | повышение формата до v2 (или опциональный блок `equipment` в v1 с дефолтом «пусто») и миграция v1→v2 |
| Ошибки | неизвестный слот/предмет → `Corrupted`, состояние не меняется (как в S02 REVIEW-FIX-01) |

## 7. Критерии завершения партии

- [x] Компиляция без ошибок и предупреждений.
- [x] Полный EditMode/PlayMode прогон зелёный, регрессии S01/S02/S03 внутри.
- [x] Экипировка использует существующие вычислители, параллельной системы статов нет.
- [x] Двойное масштабирование уровня исключено и проверено тестом.
- [x] Частичные механики выключены по умолчанию; OQ-25 не подменяется догадкой.
- [x] Формат сохранений не изменён, контракт зафиксирован.
- [x] Код опубликован в ветке `stage/s04-equipment-combat`.

## 8. Рекомендация по следующей партии

1. Ревью S04 (контракты `EquipmentLoadout`, `EquipmentCombatAdapter`, поле `ItemWeaponDamageBonus`).
2. Следующая партия (решение продюсера): стабильный `ItemInstanceId` + сохранение слотов (v2) — естественный следующий шаг, либо `S04-OQ-01/OQ-02` (правила сочетаний оружия) через статический разбор.
3. Не начинать способности/AI/анимации без отдельного решения.
