# M0-00 — отчёт о подготовке полного игрового функционала (READY_FOR_REVIEW)

**TASK:** M0-00 PREPARATION FULL GAME SYSTEMS — SCAN → PLAN → VALIDATE → PILOT → BATCH → REVIEW.
**STATUS:** `READY_FOR_REVIEW`. **Не** `ACCEPTED`, **не** приёмка S05B-2, **не** разрешение M0-01+.
**Исполнитель:** DeepSeek Harness. **Worktree:** `D:\Projects\CubeGame-deepseek` (D71).
**Ветка:** `deepseek/m0-00-full-systems-preparation` от `0b62ccc`.
**Дата:** 2026-11-09.

---

## 1. Что сделано

| Направление | Результат | Доказательство |
|---|---|---|
| SCAN → PLAN | Карта переноса полного функционала, семь систем, связи, блокеры, очередность M0-01…09 | [M0_00_FULL_TRANSFER_MAP](../planning/M0_00_FULL_TRANSFER_MAP.md) |
| Матрица Alpha → Unity | Отдельная матрица по семи системам + ссылка на подробные 176 строк | [M0_00_ALPHA_UNITY_SYSTEM_MATRIX](M0_00_ALPHA_UNITY_SYSTEM_MATRIX.md) |
| PILOT-WORLD | Детерминированный rand, формула региона, координаты, чанк-хеши, дельта-хранилище и восстановление | `tools/m0_pilots/World/**`, 58 проверок |
| PILOT-MOVEMENT | 55 значений кривых навыков, гравитация/планирование, стамина, рывок, флаги/режимы, честные `UNKNOWN` | `tools/m0_pilots/Movement/**`, 102 проверки |
| PILOT-COMBAT | Таблицы способностей, атомарный расход ресурсов, hit/block, интеграция S03, детерминированный бой, награды | `tools/m0_pilots/Combat/**`, 81 проверка |
| PILOT-NPC | 31 мс тик/стан, угроза, зелье/додж/реген, боссы, спавн, стабильные ID, сохранение состояния | `tools/m0_pilots/Npc/**`, 82 проверки |
| PILOT-ITEMS | 24 типа, 13 слотов, инвентарь, расходники, стартовые наборы, визуальный статус, аудит Core | `tools/m0_pilots/Items/**`, 85 проверок |
| ECONOMY / QUESTS / SAVES | Контракты и сценарии: продажа/покупка, wallet, состояния задания, гейт, S02 round-trip, повреждение/версии, пробел экипировки | `tools/m0_pilots/Economy/**`, 54 проверки |
| BATCH-инструменты | Подтверждённые таблицы → C# (SHA-256), матрица → машинный реестр (176 строк), `--check` идемпотентность | `tools/m0_transfer/**` |
| Отчёт/handoff | Этот отчёт и контрактные замечания для Claude | `docs/reports/M0_00_*` |

**Не изменялись:** `Assets/`, `Packages/`, `ProjectSettings/`, S02–S05, `main`, чужие ветки.
**Не запускались:** оригинальная Alpha, Unity Editor, тяжёлые прогоны.

---

## 2. Фактические проверки (команды и результаты)

### 2.1 Изолированный стенд (реальный Core, без Unity Editor)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\m0_pilots\run_pilots.ps1
```

Фактический итог (2026-11-09, лог `.unity-install\m0_pilots\m0_pilots.log`):

```
PILOT_WORLD_RESULT=PASS checks=58 failed=0
PILOT_MOVEMENT_RESULT=PASS checks=102 failed=0
PILOT_COMBAT_RESULT=PASS checks=81 failed=0
PILOT_NPC_RESULT=PASS checks=82 failed=0
PILOT_ITEMS_RESULT=PASS checks=85 failed=0
CONTRACTS_ECONOMY_QUESTS_SAVES_RESULT=PASS checks=54 failed=0
M0_PILOTS_RESULT=PASS checks=462 failed=0
```

Стенд компилирует **реальные** `Assets/CubeGame/Core/**` (44 источника вместе с пилотами) и
запускается под Mono из установленного Unity 6.3; редактор не стартует. Пилот использует S03
`DamageResolver`/`HitResolver`/`DamageMitigation` и S04 `ItemStatsCalculator` — формулы S03/S04
не дублируются.

> Техническая деталь: прямой запуск `.exe` на .NET Framework упирается в отсутствующий
> `String.TrimEnd(char)` внутри принятого `SaveService`; стенд запускает тот же бинарник под
> Mono Unity, где метод есть. Это ограничение среды проверки, не дефект S02.

### 2.2 Генератор подтверждённых таблиц

```powershell
python tools/m0_transfer/gen_alpha_tables.py
python tools/m0_transfer/gen_alpha_tables.py --check
```

```
ALPHA_TABLES_GENERATED=PASS items=24 weapons=20 abilities=16 hash=7a65ea7650497cf5
ALPHA_TABLES_CHECK=PASS items=24 weapons=20 abilities=16 slots=13 hash=7a65ea7650497cf5
```

### 2.3 Реестр матрицы

```powershell
python tools/m0_transfer/build_transfer_registry.py
python tools/m0_transfer/build_transfer_registry.py --check
```

```
TRANSFER_REGISTRY_GENERATED=PASS rows=176 hash=552a12f30465ced1
  domains: world=20 movement=20 combat=40 npc=32 items=28 economy_quests=9
           progression_persistence=12 content_ui=15
  confidence: BLOCKED=2 DEFERRED_TO_M0=1 DIRECT=118 PARTIAL=30 UNKNOWN=25
  status: IMPLEMENTED_CORE=15 IMPLEMENTED_PILOT=9 MISSING=110 N/A=4 PARTIAL=37 TEST_ONLY=1
```

### 2.4 Регрессия существующего Core-стенда

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\core_check\run_core_check.ps1
```

Фактический итог: `PASS 10 FAIL 0`, exit 0. S02-код не менялся.

---

## 3. Пилоты: что именно проверено

### PILOT-WORLD (58)

- CRT `rand()`: известный вектор `41, 18467, 6334, 26500, 19169` для seed 1; диапазон 0…32767.
- Регион: `(512,512)=1`, `(0,0)=545`, `(513,512)=2`, `(1023,0)=544`; сид
  `masterSeed + z·0x400 + x`; вариант только при типе > 4; ориентация после варианта; 4096
  уникальных сидов; имена только для доказанных ключей, `Ruins` — `(0,5)`.
- Координаты: мир 0…65535 → регион 0…1023 (по 64 блока) → клетка 8×8; границы 63/64,
  65535/65536; отрицательные/внешние отвергаются.
- Чанки: одинаковый seed → одинаковый хеш; разные seed → разный; правка меняет только свой
  чанк; граница 527/528 относится к разным чанкам.
- Данные: дельта-файл `M0DELTA|1|seed|count|fnv`, детерминированный порядок, round-trip,
  восстановление всех правок, отказ при изменённой полезной нагрузке/версии/количестве,
  пустая дельта, границы блока.

### PILOT-MOVEMENT (102)

- 55 табличных значений (ride/climb/fly/swim/boat, ранги 0…10) с допуском 0.05 п.п.;
  guard `n<1` для P1/x3p15/x3p1, floor 50 % для Half, отсутствие guard у N1.
- Гравитация 30/1 с = −30; планирование 3/1 с = −3; 10×; начальная скорость 20 → 5 за 500 мс.
- Стамина: 5000 мс спринта → 0; 10000 мс регена → 1; клампы; рывок ровно 0.25, отказ при 0.2,
  таймер 600 мс, нельзя начать второй.
- Флаги контакта/действий (1/2/4/0x10; 0x1/0x10/0x40/0x200/0x400); режимы — `CUBEGAME_CONTRACT`.
- Явно: `OQ-23` масштаб не доказан, множители навыков не применяются к физике, прыжка нет.

### PILOT-COMBAT (81)

- Кулдауны: 6/haste, 3/haste, flat 4/5/10, `0x15 = powerFactor·30+5`, default 2/haste,
  роли 3/5 → 0. Мана 0x15/0x30/0x56/0x48/default; стамина 100/200/300/400/500/600 и /haste;
  32 канала и гейт `0x1a00`; melee-диапазоны.
- Активация: успех списывает ману/стамину и ставит кулдаун; повтор/нехватка ресурсов не
  списывают ничего; тик кулдауна; отрицательное время отвергается.
- Попадание: строгая граница `roll/32767 < chance` (при chance=1 максимальный бросок — промах);
  модификаторы подтипов 0/0x0c ×0.5, 8/10 ×0.3, не-оружие без изменений; блок-порог 1/17/32.
- S03: hit/variance 1.25, митигация, HP before/after, miss, dead target без бросков, множитель
  0x57 только по флагу, неизвестный ID не умножается; 5000 ударов при 0.5 → 2400…2600 попаданий,
  повтор seed идентичен, другой seed отличается.
- Награды: босс ×20, флаг `0x200` ×10, `0x800` → 0, деньги за убийство 0; отображение
  «оружие → действие» намеренно отсутствует.

### PILOT-NPC (82)

- Фракции 0/1/3/5/6; AI-часы 30/31/62/100 мс и остаток; стан блокирует решение и тикает.
- Угроза: ×0.9/тик, накопление, смена цели; сброс строго >20 с (19 999 и 20 000 сохраняют),
  рескан 3 с; отрицательная угроза отвергается.
- Зелье r3: `HP < 0.5·входящий урон`, только фракция 3; действие 0x50, таймер 0. Додж:
  `roll%10==0`, не босс, дистанция <2, lock 600; реген фракции 5 +10 %/с с клампом.
- Боссы: 10 id; спавн 30 строк, запись 0x118, звёзды 1 % и условные 0.1 %/0.01 %, кламп 4;
  стабильные ID регион:клетка:слот; 200k статистических бросков.
- Сохранение состояния: все поля, позиция, действие, угроза; повторное сохранение не дублирует;
  неизвестный ID не выдумывается; несогласованный HP отвергается; `ALPHA` (блоб по близости) и
  `CUBEGAME_EXTENSION` (живая симуляция вне экрана) разделены.

### PILOT-ITEMS (85)

- 24 типа; r3-исправления `5 Gloves`, `6 Boots`, `0x11 Vase`, `0x15 QuestItem`, `0x17`, `0x18`,
  `0x19`; 20 подтипов оружия (Axe/Bow/Quiver/Greatmace и др.).
- 13 слотов хранения, 7 базовых отрисовок, кольца/лампа/`+0x300`; 4×50 инвентарь, стек ≤49.
- Расходники: 3000/10000/0 мс; событие 1/2 → 0x50, иначе 0x51; списание/лечение не выдумано.
- Стартовые наборы: Warrior Greatmace + 2 Axe + Shield; Ranger Bow + Mace/Crossbow; Mage Staff +
  Bracelet/Wand; Rogue два Dagger (dual wield) + Fist/Longsword; всем 2 кольца, лампа, 5 зелий.
- Визуал: модель расходника в руке рисуется Alpha; клипы 0x50/0x51 — `BLOCKED_NOT_TRANSFERRED`.
- **Аудит Core:** 10 расхождений (8 подтипов оружия + Boots/Gloves); `ItemInstance` не
  поддерживает типы 1/0x0b/0x15/0x18 и др.; переиспользованы S04 значения (Longsword 4.0,
  Chest 10.0, Shield 2.0, стоимость ≥1).

### CONTRACTS-ECONOMY-QUESTS-SAVES (54)

- Экономика: sell = `ItemStatsCalculator.Value`; buy — отказ с причиной `F-U5`; wallet без
  отрицательного баланса; деньги за убийство 0.
- Задания: типы 1…13; 1…5/13 — мгновенно, 7…12 — счётчик, 6 — без цели; состояния 0→1→2,
  повторная активация запрещена, прогресс клампится; гейт ±2 (2 разрешён, 3 нет); XP-награда.
- Сохранения: S02 round-trip класса/специализации/уровня/опыта/HP/предметов; frame/CRC; порча →
  `Corrupted`; версия 99 → `UnsupportedVersion`; версия 0 → `MigrationFailed`; предмет типа 1
  отвергается текущим валидатором; отсутствие слотов экипировки в `SaveGame` подтверждено
  рефлексией (13 слотов Alpha не сохраняются).

---

## 4. Найденные несовместимости (переданы Claude, не исправлены самостоятельно)

1. **`CORE-ENUM-MISMATCH`.** `Assets/CubeGame/Core/Items/ItemEnums.cs`: `ItemType.Boots = 5`,
   `Gloves = 6`, а r3-доказательство — `5 = Gloves`, `6 = Boots`; подтипы оружия сдвинуты
   (`1 = Mace` вместо `Axe`, `2 = Fist` вместо `Mace`, `3 = Sword` вместо `Dagger`,
   `4 = Dagger` вместо `Fist`, `6/7 = Reserved` вместо `Bow/Crossbow`, `8 = Crossbow` вместо
   `Boomerang`, `0xe = Boomerang` вместо `Quiver`). Итог: 10 расхождений. **Принятый S04 не
   изменялся**; нужно решение M0-04A.
2. **`ITEMINSTANCE-TYPE-LIMIT`.** `ItemInstance.IsSupportedType` принимает 8 типов; Alpha —
   24. Расходники, материалы, квестовые предметы, лампа, мана-куб не могут быть сохранены.
3. **`SAVE-EQUIPMENT-GAP`.** `SaveGame` v1 не содержит экипировки; 13 слотов Alpha теряются.
   Нужна версия формата и миграция в M0-01D/M0-04D.
4. **Единицы стамины.** Кулдаун/мана прочитаны в 1/100000; для стамины нормализация к шкале
   0…1 не доказана — в пилоте `unitsPerResource` явный вход, не «игровое» число.
5. **Смерть игрока.** Есть полный сброс состояния и функция выбора точки, но правило штрафа,
   точки и таймера — `UNKNOWN`; это продуктовое решение, не константа.
6. **Торговля.** Наценки/ассортимент/ресток `F-U5` не восстановлены; покупка в контракте
   отсутствует намеренно.
7. **Мир.** Таблица климат→объекты W-18, уровень воды W-07, порог стриминга W-11 (3/5 vs 8/10),
   `reg/tile/land` — открыты.

Подробный текст для issue #9/нового комментария — [M0_00_CONTRACT_HANDOFF](M0_00_CONTRACT_HANDOFF.md).

---

## 5. Что осталось неподтверждённым

- Ни один пилот не подключён к Unity-сцене: это изолированные контракты/тесты, а не игра.
- Не проверены визуальные совпадения, анимации, физика Unity, ввод, камера, сохранение файла
  через `Application.persistentDataPath`.
- Не запускались Alpha/Unity; `LIVE_UNOBSERVED` для 148 видов остаётся.
- S05B-2 визуально не принят; M0-00D не утверждён; M0-01+ не открыт.
- В рабочем дереве во время сессии появились **чужие** untracked-файлы
  `tools/alpha_models/catalog/item_layer.py`, `verify_catalog_import.py`,
  `tools/alpha_models/tests/test_item_layer.py`, `tools/alpha_models/universal/item_attach.py`.
  Они **не** созданы этой задачей и **не** включены в коммиты; перед публикацией проверен
  список добавленных файлов.

---

## 6. Следующие шаги

1. Ревью этого пакета (ChatGPT/Claude) и решение по `CORE-ENUM-MISMATCH`,
   `ITEMINSTANCE-TYPE-LIMIT`, `SAVE-EQUIPMENT-GAP`.
2. Продюсер: приёмка S05B-2 и M0-00D; только после этого — M0-01.
3. При открытии M0-01 первыми переносятся `SaveService`/frame/CRC, `WorldDeltaStore`,
   `RegionDescriptor`, стабильные ID, `MovementFormulas`, `AbilityRules`, `SpawnTable`.
4. Автоматизировать выгрузку W-18, лут-таблиц, торговых цен и `mission` — без этого
   соответствующие механики остаются `UNKNOWN`.

**Финальный статус: `READY_FOR_REVIEW`, не `ACCEPTED`.**
