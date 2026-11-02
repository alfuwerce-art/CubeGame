# S00-R02 / пакет E — бой и поведение врагов (спецификация поведения Alpha 2013)

**Проект:** CubeGame. **Этап:** S00-R02 (D41), партия 1.
**Источники:** `SRC-01` (локальный `Cube.exe`), `SRC-02` (Reversal @ `e6e906e4b2644806ef19c2e1f12ebc636c347f7c`), `SRC-04` (Unity 6000.3).
**Метод:** статическая реконструкция + сверка имён по `RENAMES.tsv`/`attribution.tsv`; динамических прогонов в этой партии **не было** (см. §9).
**Граница документа:** только собственные описания алгоритмов и констант. Тела декомпилированных функций не переносятся. Адреса даны как `005xxxxx` (ImageBase-relative, как в Reversal), чтобы факт оставался проверяемым.

## 0. Сводка

| # | Механика | Статус | Ключевой источник |
|---|---|---|---|
| E1 | Модель полей бойца: HP, ресурс, блок-энергия, базовый урон/броня, класс/спец, звёзды | `RECONSTRUCTED_SOURCE` / `STRONGLY_SUPPORTED` | `cube_types.h` (enum offsets), `Player_resetState` `004465c0`, боевой код |
| E2 | Максимальное HP (функция `stat_calcAttackDamage` — **имя в реконструкции неверно**) и урон оружия | `STRONGLY_SUPPORTED` (константы читаются однозначно; семантика подтверждена двумя независимыми контекстами) | `stat_calcAttackDamage` `00444db0`, `stat_calcWeaponDamage` `0043dc60`, `CombatBehavior::vfunc_0` (порог зелья) |
| E3 | Броня/защита и её вклад в урон | `PARTIAL` | `stat_calcArmor` `0043cff0`, `item_computeBlockValue` |
| E4 | Скорость атаки (haste), тайминги windup/recover, стоимость ресурса | `STRONGLY_SUPPORTED` | `combat_getEffectiveHaste` `00447700`, `combat_getAttackTiming` `0043d1a0`, `combat_getReloadFrames` `00447310`, `combat_getStaminaCost` `00444270` |
| E5 | Резолв попадания: попадание, блок, броня, элементальные проки | `PARTIAL` (часть ветвей деградирована) | `CombatBehavior_applyMeleeHit` `00595a60`, `combat_rollHitChance` `004441a0`, `combat_rollBlockSuccess` `0043e420` |
| E6 | Шанс попадания и модификаторы оружия | `STRONGLY_SUPPORTED` | `combat_rollHitChance` `004441a0` |
| E7 | HP-пул и смерть, дроп опыта, эффекты смерти | `PARTIAL` | `applyMeleeHit`, `Creature_spawnDeathEffects` `0059b180` |
| E8 | XP и уровни | `STRONGLY_SUPPORTED` | `xp_getNextLevelCost` `00444d60`, `xp_applyLevelUps` `00447b00`, `curve_level05` `0043ca60` |
| E9 | Кривые (diminishing-модель Alpha) | `STRONGLY_SUPPORTED` | `curve_diminishingx3p1` `0043e2c0`, `curve_diminishingN1` `0043e660`, `curve_diminishingx3p15` `0043f770`, `curve_diminishingHalf` `004476a0` |
| E10 | Смерть/возрождение игрока | `PARTIAL` / `UNKNOWN` | `Player_resetFull` `00446330`, `Player_resetState` `004465c0` |

---

## E1. Модель полей бойца

`Creature` — общий класс игрока, моба и NPC (RTTI `cube::Creature`, размер объекта ≥ `0x1d5xx`; конструктор `0043b690` инициализирует десятки контейнеров STL). Подтверждённые смещения (источник — экспорт структур Reversal + собственные проверки чтения в функциях боя):

| Смещение | Тип | Смысл | Где подтверждено |
|---|---|---|---|
| `0x060` | u8 | **роль/фракция**: `0` = игрок (локальный), `1` = враждебный монстр, `3` = союзник/питомец, `5` = дружелюбный NPC, `6` = пассивный. Управляет ветками формул и ИИ | `stat_calcAttackDamage`, `combat_getEffectiveHaste`, тик ИИ, `CombatBehavior` (фракция 5), призыв питомца (3) |
| `0x068` | u8 | текущий id атаки/способности (определяет windup/recover) | `combat_getAttackTiming`, `CombatBehavior_chooseAttack` |
| `0x06c` | i32 | накопленный таймер атаки (мс), сравнивается с порогом | `CombatBehavior_chooseAttack`, `combat_advanceAttackTimer` |
| `0x070` | i32 | текущая цель (указатель/индекс), `0` = нет цели | `CombatBehavior_chooseAttack`, блок-код |
| `0x0a0` | u8 | ? ресурс/стамина-флаг (используется в ветке силы) | `applyMeleeHit` |
| `0x128` | i32 | стан/оглушение (мс); блокирует атаку, если `>0` | `combat_advanceAttackTimer`, `CombatBehavior_chooseAttack` |
| `0x130` | i32 | «прерывание атаки» (мс) — выставляется уроном | `applyMeleeHit` |
| `0x134` | i32 | счётчик «ярости/комбо» (накопитель, потолок 5000) | `applyMeleeHit`, элементные эффекты |
| `0x138` | i32 | аналогичный накопитель у цели | `applyMeleeHit` |
| `0x140` | u8 | класс: 1 Warrior, 2 Ranger, 3 Mage, 4 Rogue | `stat_calcAttackDamage` switch |
| `0x141` | u8 | специализация (номер ветки) | `stat_calcAttackDamage`, `combat_selectAttackVariant` |
| `0x144` | f32 | «второй» ресурс (перелив ресурса при блоке) | блок-код `00598xxx` |
| `0x14c..0x15c` | f32 | скорость/импульс (обнуляется при ударе) | `applyMeleeHit` |
| `0x164`, `0x168` | f32 | ? (в формулах пока не подтверждены) | — `UNKNOWN` |
| `0x16c` | f32 | **текущее здоровье**; `<=0` → смерть; при ресете = `1.0`, при спавне = вычисленное `MaxHealth` | `applyMeleeHit` (вычитание, кламп в 0), `Player_resetState`, спавн игрока `22322` |
| `0x170` | f32 | **ресурс** (мана/стамина) в диапазоне `0..1`; тратится на способности | `ability_isUsable`, блок-код |
| `0x174` | f32 | **энергия блока/парирования**; может уходить в минус до `-1.0` | блок-код `00598xxx` |
| `0x178` | f32 | **база максимального HP** (входит в `stat_calcAttackDamage`) | `stat_calcAttackDamage` `00444db0` |
| `0x180` | f32 | базовый урон оружия; при ресете = `1.0` | `stat_calcWeaponDamage` `0043dc60`, `Player_resetState` |
| `0x184` | f32 | базовая броня; при ресете = `0.0` | `stat_calcArmor` `0043cff0`, `Player_resetState` |
| `0x190` | i32 | уровень | `xp_*`, `stat_*`, `ability_getPowerFactor` |
| `0x194` | i32 | текущий опыт | `xp_applyLevelUps` |
| `0x1a8` | u8 | «звёздность» монстра (0..~5) | `stat_calcAttackDamage`, `combat_getEffectiveHaste` |
| `0x1b0`, `0x1b4` | i32 | мировые координаты существа (fixed-point 16.16) | `applyMeleeHit` (поиск ячейки региона), region lookup |
| `0x1d8`? / `0x11d0`, `0x11d4` | i32 | координаты источника последнего урона (для «агро») | `applyMeleeHit` |
| `0x7e` | u16 | битовые флаги существа (0x8, 0x10, 0x20, 0x200, 0x800, 0x2000, 0x4000…) | `stat_calcArmor`, `stat_calcWeaponDamage`, `applyMeleeHit` |
| `0xaa8`, `0xaa9` | u8 | «материал/руна» оружия: класс эффекта (3 = руна) и подтип | `weapon_isSpecialRune`, `combat_selectAttackVariant` |
| `0x990`, `0x530`, `0x648`, `0x760`, `0x878`, `0xcd8` | u8 с шагом слота | слоты экипировки (тип предмета в слоте) | `stat_calcArmor`, `stat_calcAttackDamage` |
| `0x1138` | i32[11] | ранги навыков: `[0]` PetTaming, `[1]` PetRiding, `[2]` Climbing, `[3]` HangGliding, `[4]` Swimming, `[5]` BoatDriving, `[6..10]` SkillAbility1…5. **Уточнение:** в первой редакции слоты 1–5 были названы «профессиями» — это ошибка; по таблице имён `World.cpp:3945-3988` это транспортно-двигательные навыки | `ability_getPowerFactor` (чтение `+0x1138+slot*4`), `World.cpp:3945-3988` |
| `0x1158` | i32 | ранг «форменной» способности (ярость/форма) | `combat_getEffectiveHaste` |
| `0x1178` | ptr | голова интрузивного списка баффов (`BuffNode`: next/prev/type u8/magnitude f32/duration i32) | `stat_calcManaRegen`, `combat_rollElementProc`, `combat_getEffectiveHaste` |
| `0x1190` | f32 | «равновесие/stagger» (сбивается уроном, восстанавливается реген-кривой) | `Creature_compute_scale_factor`, `applyMeleeHit` |
| `0x139c` | std::map | кулдауны способностей (`id → мс`) | `ability_isUsable` |
| `0x13a4` | std::map | таблица «кто сколько урона нанёс» (threat) | `applyMeleeHit`, `combat_findTopThreat` |

**Важно (риск):** смещения `0x164`, `0x168`, `0x1d8` не подтверждены формулами; в спецификацию CubeGame не переносятся.

### FEATURE_ID: E1.buff-model
SUBSYSTEM: E
ORIGINAL_OBSERVATION: баффы — двусвязный интрузивный список из узлов фиксированного размера; узел содержит тип (u8, значения 0x06, 0x09, 0x0b, 0x0c замечены в разных формулах), величину (f32) и остаток (i32, мс).
RECONSTRUCTION_REF: `cube_types.h` enum `cube_BuffNode_offsets` (next 0x00 / prev 0x04 / type 0x08 / magnitude 0x0c / durationMs 0x10); потребители `00447700`, `0043ea40`, `004444a0`.
LOCAL_BINARY_REF: `Cube.exe` `84A7A132…6DC30BF`.
SAVE_OR_ASSET_EVIDENCE: в блобе персонажа списка баффов не найдено (`UNKNOWN`, см. `D_character`).
CONTRADICTIONS: смысл конкретных кодов типа (6 = «щит», 9 = «ярость», 11 = «равновесие», 12 = «скорость») — вывод по контексту вызовов, не документирован.
CONFIDENCE: PARTIAL
CUBEGAME_DECISION: PROPOSED (типизированные эффекты с длительностью; коды Alpha не копировать)
UNITY_MAPPING: own-game-logic (структуры эффектов), отображение — builtin (VFX/UI)
GAPS: нужен перечень всех кодов типов; проверка длительности/стака.

---

## E2. Максимальное HP (`stat_calcAttackDamage`, `00444db0`) и урон оружия (`stat_calcWeaponDamage`, `0043dc60`)

> **Исправление (важно).** В первой редакции этой партии функция `stat_calcAttackDamage` трактовалась как «урон атаки» (по имени из `RENAMES.tsv`). Независимая проверка контекстов показала, что это **максимальное (и потолочное) HP**:
> 1. `CombatBehavior::vfunc_0` (`0042cb20`, строки 200–204): `HP(0x16c) < MaxHP·0.25` → существо пьёт лечебное зелье. Порог «25 % здоровья» имеет смысл только если это потолок HP.
> 2. Спавн игрока (`GameController.cpp:22322`): `HP(0x16c) = stat_calcAttackDamage()`.
> 3. `applyMeleeHit`: после лечения (`HP −= (−dmg)`) значение клампится сверху именно этой величиной.
> 4. Спавн питомца: HP = эта же величина.
> Имя в `RENAMES.tsv` (`stat_calcAttackDamage`) — ошибочная аттрибуция; каноническое имя для наших спецификаций: **`calcMaxHealth`**.

Алгоритм `calcMaxHealth` (собственное описание):

1. `levelFactor(L) = 2^((1 − 1/((L−1)·0.05 + 1)) · 3)`, где `L` — уровень (`0x190`). Это `curve_level05`, умноженная на 3 и возведённая в степень 2 — **экспоненциальный прирост с насыщением**. Пересчитано (Python, 2026-11-03): `L=1 → 1.000`; `L=5 → 1.414`; `L=11 → 2.000`; `L=21 → 2.828`; `L=51 → 4.416`; `L=101 → 5.657`; предел при `L→∞` — `2^3 = 8`.
2. `starFactor(S) = 2^(S · 0.25)`, где `S` — звёзды (`0x1a8`). `S=1 → 1.189`, `S=2 → 1.414`, `S=4 → 2.0`, `S=8 → 4.0`.
3. `maxHealth = base(0x178) · starFactor · levelFactor`.
4. Если `stateFlag(0x060) == 0` (обычная форма), `starFactor` заменяется на `2^1 = 2.0` — для обычной формы звёздный множитель фиксирован и не зависит от `0x1a8`. **Резолюция гипотез (партия 1):** `0x060 == 0` — это **игрок**, а не «обычная форма»: (а) ИИ-тик пропускается для существ с `0x60 != 0` (локальный контроллер игрока тикает иначе); (б) питомцы призываются с `0x60 = 3`, монстры — 1/5/6; (в) `ability_getPowerFactor` использует ветку `level/2 + star + 1` только при `0x60 != 0`, т.е. «effectivePower» — свойство мобов, не игрока. Следствие: у игрока звёздный множитель жёстко 2.0, у мобов — `2^(0.25·S)`; 4-звёздный моб равен игроку того же уровня.
5. Множители класса (`0x140`): Warrior `×1.3`, Ranger `×1.1`, Rogue `×1.2`, Mage — **без множителя** (ветки `case 3` нет). То есть в Alpha класс меняет **запас HP**, а не урон.
6. Warrior со специализацией `1` (`0x141 == 1`) — дополнительный `×1.25`.
7. К результату прибавляются **статы надетых предметов**: для слотов `0x990` (вторая рука, тип 3), `0xaa8` (основное оружие, тип 3), `0x530` (нагрудник, тип 4), `0x648` (перчатки, тип 6), `0x760` (обувь, тип 5), `0x878` (наплечники, тип 7) вызывается общий расчёт стата предмета (в псевдокоде назван `item_computeStat_armor`). **Уточнение партии 2:** это не «руны», а слоты экипировки; сравнение идёт с **типом** предмета в слоте (3 = оружие, 4/5/6/7 = броня), см. `F_items.md` §F1.

### Урон оружия (`stat_calcWeaponDamage`, `0043dc60`)

1. `weaponPart = base(0x180) · 2^0 · levelFactor` (звёздный множитель в базе не участвует: `2^0 = 1`).
2. Если у оружия нет списка «апгрейдов»: флаг `0x7e & 0x8` даёт `+= levelFactor · starFactor · 2.0`; флаг `0x7e & 0x10` — `+= levelFactor · starFactor · 8.0`.
3. Если апгрейды есть — к сумме прибавляется `item_weaponDamage(...)` по каждому элементу списка.
4. Финальные множители по id текущей атаки (`0x68`): `0x57 → ×50`, `0x5b → ×2`, `0x5d → ×5`, `0x60 → ×3`; остальные — без множителя.

**Вывод модели (важно для CubeGame):** в Alpha HP и урон — **разные** базы (`0x178` — HP, `0x180` — оружие), но **одна шкала множителей** (`levelFactor`, `starFactor`) и одна кривая. Класс влияет на HP-множитель; оружие и руны — на урон.

### FEATURE_ID: E2.max-health-and-weapon-damage
SUBSYSTEM: E
ORIGINAL_OBSERVATION: запас HP растёт экспоненциально от уровня с насыщением и от звёзд; класс даёт постоянный множитель к HP; урон оружия масштабируется тем же levelFactor, но своей базой и множителями по типу атаки.
RECONSTRUCTION_REF: `game_misc.cpp` → `00444db0`, `0043dc60`; `control/GameController.cpp` → `0043ca60` (`curve_level05`).
SAVE_OR_ASSET_EVIDENCE: уровень/звёзды/базы присутствуют в блобе персонажа (см. `D_character.md`).
CONTRADICTIONS: имя `stat_calcAttackDamage` в аттрибуции Reversal противоречит семантике (см. врезку). Это пример того, почему `RECONSTRUCTED_SOURCE` нельзя принимать без перекрёстной проверки.
CONFIDENCE: STRONGLY_SUPPORTED (формулы и семантика), PARTIAL (смысл `stateFlag`)
CUBEGAME_DECISION: PROPOSED — разделить `calcMaxHealth` и `calcWeaponDamage` как отдельные функции одной шкалы; числа — предмет решения продюсера.
UNITY_MAPPING: own-game-logic (`CombatFormulas`), тесты — NUnit EditMode; UI-полоса HP — builtin.
ALPHA_TEST_STEPS: см. `VALIDATION_CASES.md` `VA-CMB-01`, `VA-CMB-02`.
UNITY_TEST_STEPS: табличный тест `MaxHealthTests` (6 контрольных точек) + `WeaponDamageTests`.
GAPS: смысл `stateFlag`; различие «звёздный множитель 2.0 vs `2^(0.25S)`»; куда именно попадает результат `stat_calcWeaponDamage` в hit-запись (какая из ветвей `applyMeleeHit` использует его напрямую).

---

## E3. Броня (`stat_calcArmor`, `0043cff0`)

1. Та же пара множителей `starFactor · levelFactor`, но от `armorBase(0x184)`.
2. Если `stateFlag == 0` — снова `starFactor := 2.0` (симметрично E2).
3. Флаг `0x7e & 0x20` добавляет `weaponBase = starFactor·levelFactor` (одна «безразмерная» единица брони).
4. Слоты `0x530` (тип 4 — нагрудник), `0x648` (6 — перчатки), `0x760` (5 — обувь), `0x878` (7 — наплечники) добавляют `item_computeBlockValue()` — стат брони слота. **Уточнение партии 2:** это слоты брони, а не руны (`F_items.md`).

Применение (из `CombatBehavior_applyMeleeHit` и вызывающего кода `00598xxx`): итоговый урон = `rawDamage − blockFraction · armor`, где
`blockFraction = 1 − (blockMeter / blockDuration)`, ограничено `[0,1]`;
`blockMeter` — `0x174`, `blockDuration` — `shield_getBlockDuration()` (`00444cb0`);
результат клампится в `0` (урон не уходит в отрицательные значения); остаток `local_334` после вычитания используется как «пробивший» урон.

⚠️ `PARTIAL`: в деградированном коде `local_32c` переиспользуется и как «доля блока», и как «доля блокированного урона»; порядок операций (блок → броня → урон) восстанавливается по последовательности вызовов, но точные границы — гипотеза.

### FEATURE_ID: E3.armor-mitigation
CONFIDENCE: PARTIAL
CUBEGAME_DECISION: PROPOSED — броня как **доля снижения**, зависящая от «готовности блока», не как плоское вычитание.
UNITY_MAPPING: own-game-logic.
GAPS: точная формула доли; роль `0x174` в отрицательной зоне; взаимодействие с флагом `0x7e&0x20`.

---

## E4. Скорость атаки, тайминги, стоимость ресурса

### E4.1 `combat_getEffectiveHaste` (`00447700`)

`mult = 1.0`
1. Если `stateFlag != 0` → `mult = 0.75 + 0.0625·star` (S=0 → 0.75; S=4 → 1.0; S=8 → 1.25). Иначе `mult` остаётся `1.0`.
2. Warrior без выбранной спецухи (`0x140==1 && 0x141==0`) → `mult += 0.5 · (blockMeter/blockDuration)` (щит ускоряет).
3. Mage-ветка `0x140==3 && 0x141==1` → `mult += (blockMeter/blockDuration)` (полный, без 0.5).
4. `mult += stat_calcHasteBonus()` (`004478b0`): базовая часть `levelFactor/8 · 0.1` + бонусы слотов (`item_computeStat_variant3`).
5. Если в списке баффов есть тип `0x0c` (ускорение): `mult *= 1 + (1 − 1/(rank·0.1 + 1))`, где `rank` = `0x1158`, а если он 0 и форма ненулевая — `rank = level/2 + star + 1`.

### E4.2 Тайминги (`combat_getAttackTiming` `0043d1a0`, `combat_getReloadFrames` `00447310`, `combat_getStaminaCost` `00444270`)

Все три функции — гигантские `switch` по id способности/оружия (`0x68`), различающиеся **только базовыми константами**; формула одна:

`value = base / (haste · attackSpeedStat(0x17c))`, где
- `combat_getAttackTiming` — полный цикл «замах + восстановление» (для оружия: 50…400 мс; для способностей до 1200–1600 мс);
- `combat_getReloadFrames` — только восстановление (0…1200 мс; особые значения: `0` для id 0/0x31, `200` для 10, `600` для 0x1e/0x20/0x49/0x5d, `1200` для 0x1f/0x21);
- `combat_getStaminaCost` — стоимость ресурса (для оружия/способностей 10…800; константы `100` — «базовая атака», `600` — тяжёлая, `0` — бесплатная).

`CombatBehavior_chooseAttack` (`004456a0`) использует `attackTiming − windup` как порог готовности и дополнительно пересчитывает recover для конкретной выбранной атаки (таблица констант 50…5000 мс; напр. `W` = 5000 мс, `i` = 5000 мс фикс.).

### FEATURE_ID: E4.timing
CONFIDENCE: STRONGLY_SUPPORTED (формула), PARTIAL (полнота таблиц id)
CUBEGAME_DECISION: PROPOSED — единая формула `base/(haste·speed)` с таблицей констант на способность.
UNITY_MAPPING: own-game-logic + `Animator`/Animation Events для визуальных окон; тайминги — собственные (мс), не `Time.deltaTime`-зависимые константы.
GAPS: полный список id способностей и их базовых констант (в этой партии выписаны только срезы); связь id с именами способностей — `UNKNOWN` (см. F).

---

## E5. Резолв попадания (`CombatBehavior_applyMeleeHit`, `00595a60`)

**Сборка величины урона (новое, партия 2).** Урон конкретного удара формируется в вызывающем коде перед записью попадания (`GameController.cpp:98177-98205`):

1. Множитель разброса: `v = 1.25 − 0.5·rand()/32767` → диапазон **0.75…1.25** (±25 %).
2. Обычная атака: `damage = abilityFactor · stat_calcWeaponDamage() · (ratio²·5.0 + base)`, где `ratio` — нормированная дистанция/размер, `base` — табличная величина по id.
3. Способности-исключения (id `0x57`, `0x58`, `0x1e`, `0x1f`, `0x20`, `0x21`): `damage = tableValue · stat_pow · v`.
4. Если сработал элементный прок (`combat_rollElementProc`) — **урон ×2**.
5. Итог передаётся в `CombatBehavior_spawnProjectileHit`/`applyMeleeHit`.

Это закрывает «дыру» между формулой статов (`stat_calcWeaponDamage`) и реальным уроном.

Восстановленная последовательность резолва (собственное описание; часть ветвей деградирована):

1. **Поиск цели** в таблице попаданий по ключу позиции (`rbtree_lowerBound_pairKey`), проверка «цель существует и жива» (`0x16c > 0`).
2. **Threat-таблица**: если урон `>0`, в map `0x13a4` по паре «атакующий, цель» инкрементируется счётчик; запоминаются координаты источника (`0x11d0/0x11d4`) — используется для «кто меня ударил».
3. **Сбивание равновесия**: `poise(0x1190) −= damage·0.25 / 2^((L_target − 1 + star_target)·0.25)`, кламп в 0; если цель в специальном состоянии `0x68 == 'T'` — сброс состояния и `0x130 = 3000` мс (оглушение).
4. **Выбор цели атаки**: атакующий вызывает `CombatController_acquireNearbyTargets` и накапливает «ярость» (`0x134`): обычная атака `+2·dmg`, «огненная» аура `+1·dmg`, оружие id `0x19` `+10·dmg`, специальная руна со специализацией `+20·dmg`; после — `+0.01`.
5. **Клампы**: обнуление импульса (`0x4c..0x54`) при определённых условиях; `0x130 = max(0x130, param)` (прерывание); `0x128 = max(0x128, param)` (стан) — если удар несёт стан.
6. **Спецэффекты удара** по типу (`param_1[0x45]`): `1` = лечение/бафф цели (`0x170 += 0.25`, кламп 1.0), `4` = обновление фитиля бомбы, `5` = снятие/расход «щита» из списка баффов (тип `6`), `3` = ветка «без прямого урона».
7. **Прямой урон/лечение**: при `damage < 0` — лечение: `HP(0x16c) −= damage` (растёт), затем кламп сверху `min(HP, calcMaxHealth())`; при `damage > 0` — `HP −= damage`. Это и есть механизм «не перелечить» и он же — доказательство, что `calcMaxHealth` = максимум HP (см. E7).
8. **Смерть**: HP клампится в 0, создаются «эффекты смерти» (`Creature_spawnDeathEffects` `0059b180`), обнуляются таймеры, сбрасывается `0x6c`, вычисляется награда опыта (E8), обнуляется HP у связанной сущности (пет/маунт) в map `0x11c8`.
9. **Прок элементных эффектов**: в вызывающем коде (`00598xxx`) для рун с кодами `0x80..0x83` применяются эффекты по `curve_level05(level руны)`: `0x80` — доп. урон, `0x81` — урон + обратный эффект (`local_33c`, отрицательная величина — «отхил»/«вампиризм»), `0x82` — урон с накоплением `0x134`, `0x83` — урон с накоплением `0x138`.

### FEATURE_ID: E5.melee-resolution
CONFIDENCE: PARTIAL
CUBEGAME_DECISION: PROPOSED — разделить на слои: hit detection → threat → mitigation → HP/death → rewards; эффекты — data-driven.
UNITY_MAPPING: `Collider`/`Physics.OverlapSphere` (хитбоксы), own-game-logic для слоёв; `Animator` Animation Events для окон.
ALPHA_TEST_STEPS: удары по одному и тому же мобу на разных уровнях; запись HP до/после; проверка «блок щитом» и «стан».
UNITY_TEST_STEPS: pure-C# тест слоя `DamageResolver.Resolve(attack, defense, rng) → outcome` с seeded RNG.
GAPS: точные условия ветвей `param_1[6] <1` (knockback?), роль флагов `0x2000`, `0x4000`; порядок «блок → броня» (E3).

---

## E6. Шанс попадания (`combat_rollHitChance`, `004441a0`)

`roll = rand()` (0..32767); `hit = roll/32767.0 < chance`.
Модификаторы от «материала оружия» (`0xaa8==3`): подтипы `0,1,2,0xc` → `chance × 0.5`; подтипы `8,10` → `chance × 0.3`. Остальные подтипы — без изменений.
`chance` приходит из вызывающего кода (база + бонусы уровня/звёзд) — точная формула базы `UNKNOWN` в этой партии (см. `CombatBehavior::vfunc_0`).

`combat_rollBlockSuccess` (`0043e420`): `roll = item_rarityScaled()` (0..100), порог `= (1 − 1/((L−1)·0.05+1))·100 + 1`; если `roll > порог` → блок не удался; иначе — доп. проверка типа щита `item_classifyRecursive(0x140)`.
То есть **шанс блока растёт с уровнем** по той же насыщающейся кривой и ограничен типом щита.

### FEATURE_ID: E6.hit-and-block
CONFIDENCE: STRONGLY_SUPPORTED (hit), PARTIAL (block: смысл `item_rarityScaled`)
CUBEGAME_DECISION: PROPOSED — hit/block как отдельные проверки с seeded RNG и логированием.
UNITY_MAPPING: own-game-logic.
GAPS: базовая `chance`, роль «редкости» в блоке, `item_classifyRecursive` (тип щита).

---

## E7. HP, смерть и награда

- HP хранится как `f32` в `0x16c`; в `Player_resetState` инициализируется `1.0`, при спавне игрока и при level-up — значением `calcMaxHealth()` (`stat_calcAttackDamage`, `00444db0`).
  → **Резолюция (партия 1):** отдельный пул HP **есть**, но его максимум вычисляется той же функцией-«power», что раньше ошибочно считалась уроном. Урон приходит из `stat_calcWeaponDamage` (база `0x180`). Класс влияет на HP, оружие/руны — на урон.
- Смерть: HP→0, `Creature_spawnDeathEffects`, обнуление таймеров, поиск ячейки региона для регистрации события смерти (`world_getRegionCell64` + запись в список сущностей), обнуление HP связанного пета.
- **Награда опыта** (в `applyMeleeHit`): `xp = max(1.0, curve_level05(level_target + star_target) · 20)`;
  `×20` если `creature_isBossType()`; `×10` если флаг `0x7e & 0x200`; `= 0` если флаг `0x7e & 0x800`.
  ⚠️ формула прочитана из смешанного блока (часть регистров переиспользована) — `PARTIAL`.
- Дроп предметов в этой партии не разобран (`UNKNOWN`; пакет F).

### FEATURE_ID: E7.hp-death
CONFIDENCE: STRONGLY_SUPPORTED (модель HP), PARTIAL (награда/флаги)
CUBEGAME_DECISION: PROPOSED — отдельный пул HP, максимум по формуле `calcMaxHealth` (собственная кривая, числа утверждает продюсер).
UNITY_MAPPING: own-game-logic.
ALPHA_TEST_STEPS: (1) на уровне 1 и 2 ударить моба и записать изменение HP; (2) снять скриншот полосы HP до/после level-up; (3) сравнить HP-шкалу с расчётным `calcMaxHealth`.
UNITY_TEST_STEPS: `HealthPoolTests` + тест формулы награды.
GAPS: полный список флагов `0x7e`; какая именно величина попадает в `param_1[4]` записи удара (`stat_calcWeaponDamage` — гипотеза).

---

## E8. Опыт и уровни

- `xp_getNextLevelCost(L) = floor((1 − 1/((L−1)·0.05 + 1))·1000 + 50)` (`00444d60`).
  Пересчитано точно (Python, 2026-11-03), значения — целая часть: `L=1 → 50`; `L=2 → 97`; `L=5 → 216`; `L=10 → 360`; `L=20 → 537`; `L=50 → 760`; `L=100 → 881`. Асимптота при `L→∞` — `1050`.
- `xp_applyLevelUps` (`00447b00`): пока `xp(0x194) >= cost`, вычитает cost, `level++`, пересчитывает «power»-значение. Опыт хранится накопительно в `0x194`.
- Опыт за убийство — см. E7.
- `computeLevelDiffBonus` (`0040f4f0`): если уровень цели ниже уровня игрока, добавляет `Σ 2·2^((star_target + level_target − 1)·0.25)` по разнице уровней — то есть **добивание слабых мобов даёт бонусный опыт по той же звёздной шкале**. `PARTIAL` (часть регистров потеряна).

### FEATURE_ID: E8.xp-levels
CONFIDENCE: STRONGLY_SUPPORTED
CUBEGAME_DECISION: PROPOSED — повторить форму стоимости; бонус за разницу уровней — обсудить (D30: предел уровня не ограничен).
UNITY_MAPPING: own-game-logic; UI — builtin.
ALPHA_TEST_STEPS: убить N мобов на уровне 1, зафиксировать XP-бар и момент level-up (сверить с таблицей).
UNITY_TEST_STEPS: `XpCurveTests` (табличный тест 1..100).
GAPS: точный вклад `computeLevelDiffBonus`; откуда берётся XP за квесты (пакет J).

---

## E9. Кривые (общая модель Alpha)

Все прогрессии в Alpha строятся из 3–4 «убывающих» кривых:

| Функция | Формула | Где используется |
|---|---|---|
| `curve_level05(n)` | `1 − 1/((n−1)·0.05+1)` | XP-стоимость, бонусы уровня, награда за убийство |
| `curve_diminishingN1(n)` | `1 − 1/((n+1)·0.1+1)` | ранги способностей (мощность) |
| `curve_diminishingx3p1(n)` | `(1 − 1/(n·0.1+1))·3 + 1`, `0` при `n<1` | множители силы/эффекта |
| `curve_diminishingx3p15(n)` | `(1 − 1/(n·0.1+1))·3 + 1.5`, `0` при `n<1` | вариант для «сильных» эффектов |
| `curve_diminishingHalf(n)` | см. `004476a0` | половина убывания (мелкие бонусы) |

`ability_getPowerFactor(itemType, level)` (`0043ed60`): `1 − 1/(level·0.1 + 1)`; если `level < 0`, берётся ранг из `0x1138[slot]`; если ранг `0` и форма ненулевая — `level/2 + star + 1` («эффективный Power»). Это же выражение повторяется в `combat_getEffectiveHaste` (buff 0x0c) — единая формула «power-rank».

### FEATURE_ID: E9.curves
CONFIDENCE: STRONGLY_SUPPORTED
CUBEGAME_DECISION: PROPOSED — реализовать как один модуль `ProgressionCurves` с именованными кривыми, чтобы числа можно было менять без правки механик.
UNITY_MAPPING: own-game-logic (чистые функции, покрываются тестами).
GAPS: `curve_diminishingHalf` точная форма (в этой партии не прочитана до конца).

---

## E10. Смерть/возрождение игрока — `UNKNOWN`

`Player_resetFull` (`00446330`) и `Player_resetState` (`004465c0`) восстанавливают контроллер, но **условия возрождения** (штрафы, точка респавна, таймер) в этой партии не восстановлены. Пометка `UNKNOWN`; кандидат на партию I/J.

---

## 9. Что не подтверждено и как проверять (Alpha ↔ CubeGame)

| # | Вопрос | Способ проверки | Риск, если угадать |
|---|---|---|---|
| Q-E1 | `stateFlag(0x060)` = «обычная форма» или «игрок»? | ударить мобом с 2★ и игроком одного уровня, сравнить урон | неверный звёздный множитель |
| Q-E2 | HP = attack-power (H-E7)? | полоса HP до/после level-up | неверная модель здоровья |
| Q-E3 | Порядок «блок → броня → HP» и доли | бой со щитом при разной «готовности» блока | неверная митигация |
| Q-E4 | Базовая `chance` попадания | серия ударов, статистика промахов | неверная точность |
| Q-E5 | Полные таблицы констант по id способностей | сопоставить id с именами (пакет F) + замер | неверные тайминги |

**Что можно делать без динамики:** формулы E2/E4/E8/E9 и модель полей E1 уже достаточны для написания **чистых функций** и их тестов в CubeGame — но только как `PROPOSED`, не как `APPROVED_Dxx`.

## 10. Unity-маппинг (без переписывания Plasma)

| Требование Alpha | Unity 6000.3 builtin | Собственный код CubeGame |
|---|---|---|
| Хитбоксы/зоны удара | `Collider`, `Physics.OverlapSphereNonAlloc`/`OverlapBox` | выбор формы зоны, слои, «дружественный огонь» |
| Окна анимации удара | `Animator`, Animation Events, State Machine Behaviours | связь событий с hit-window и уроном |
| Тайминги/кулдауны | `Time.time`, корутины; для массовых — `Job System` | формулы `base/(haste·speed)`, таблицы констант |
| Урон/броня/HP | — | `CombatFormulas`, `DamageResolver` (простые C#-классы, покрываются NUnit) |
| Эффекты/частицы | VFX Graph / Particle System | правила наложения баффов |
| Звук удара | `AudioSource` + `AudioMixer` | выбор клипа по событию |
| AI-поведение в бою | `NavMeshAgent` (после проверки на стриминговых чанках) | дерево решений, threat, фазы |
| Детерминированная проверка формул | — | тесты EditMode с seeded RNG (`System.Random`), без `UnityEngine.Random` |

**Не переносить:** D3D9-рендер, собственную анимационную систему Plasma, XAudio2, оконный слой. Числовые таблицы Alpha — материал для решения продюсера, а не готовый баланс CubeGame.
