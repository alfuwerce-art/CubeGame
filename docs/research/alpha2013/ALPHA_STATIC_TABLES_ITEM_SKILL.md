# Статические таблицы Alpha 2013: предметы, объекты домов, навыки и способности

**Статус: `RESEARCH_DRAFT / NOT_APPROVED`.** Доказательная выписка из декомпилированного кода клиента
`Cube.exe` (Alpha 2013). Это **не** ТЗ, **не** приёмка S05B-2 и **не** разрешение на M0-01+.

**Источник:** `cube::World::ctor_1 @ 0x0058eb00`, файл `cube/world/World.cpp` декомпилированного дерева
(Ghidra 12.1.2 headless; дерево получено независимо от CubeGame и хранится только в `.scratch`).
**SHA256 источника:** `FEFC5E47A06A0DF79329F0E3D19756FBD138114C09CC2421901B9CF57999A9D9` (177 526 Б).
**Тело функции:** строки 2152…4674 файла; таблицы строятся повторяющимися вызовами
`World_MapInsertChunk` (ключ — пара dword) и `World_map_find_or_insert_int` (ключ — int).
**Извлечение:** [tools/alpha_research/extract_world_ctor_tables.py](../../../tools/alpha_research/README.md) —
детерминированный разбор текста, без запуска игры и без запуска `Cube.exe`.
**Дата:** 2026-11-08. **Исполнитель:** DeepSeek Harness (issue #8).

---

## 0. Правило восстановления значения (доказано)

В декомпиляции вставка ведёт себя как `std::map::operator[]`: вызов возвращает слот значения, а
**присваивание широкой строки, идущее сразу после вызова**, заполняет этот слот. Отсюда:

```
local_3c = TYPE; local_38 = SUBTYPE;   →  ключ (TYPE, SUBTYPE)
World_MapInsertChunk(&local_3c);       →  вставка
u16string_assignPtrLen(L"Name", n);    →  значение ЭТОГО ключа
```

**Проверки правила (три независимых):**

1. `(2,0) = Formula` — совпадает с уже установленным фактом: рецепт крафта это предмет типа 2
   (`F_items.md`, `GameController_computeCraftPrices @ 0x4a19d0`).
2. `(8,0) = Amulet`, `(9,0) = Ring` — совпадает с проверками типов в слотах экипировки
   (`Equipment_sum_slot_values @ 0x446150`: слот `+0x418` требует тип `8`, слоты `+0xbc0/+0xcd8` — тип `9`).
3. Таблица навыков/способностей, восстановленная тем же правилом, даёт **16/16** идентификаторов,
   совпадающих с независимо известным списком способностей в `M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX`
   (P-15: `0x15,0x22,0x30,0x31,0x32,0x36,0x4f,0x56,0x58,0x60,0x61,0x63,0x64,0x65,0x66,0x67`).

**Остаточная неоднозначность (не скрывается).** В двух блоках декомпилятор печатает значение без
переменной-посредника (`assignPtrLen(L"...",n)`): это блок экипировки (строки 2815…2847) и «голова»
блока оружия (строки 2847…2879). Альтернативное чтение «значение относится к СЛЕДУЮЩЕМУ ключу»
воспроизводит прежние опубликованные списки (`F_items.md`: 1 mace, 2 fist, 3 sword, 4 dagger;
`M0-00B` I-09: 5 обувь, 6 перчатки), но противоречит фактам (2,0)/(8,0)/(9,0) в том же блоке.
Оба чтения одновременно верными быть не могут. Такие строки помечены `AMBIGUOUS` и **не считаются
доказанными**; решающий тест указан в §5.

---

## 1. Предметы: (тип, подтип) → имя

Извлечено 133 пары. Строка `(1,0) = Village` **исключена** как артефакт: она принадлежит другому
(региональному) словарю, ключ собран из устаревших локальных переменных.

| тип | шт. | имена по подтипам |
|---|---|---|
| `0x01` | 9 | 1 LifePotion, 2 CactusPotion, 3 ManaPotion, 4 GinsengSoup, 5 SnowBerryMash, 6 MushroomSpit, 7 **Bomb**, 8 PineappleSlice, 9 PumpkinMuffin |
| `0x02` | 1 | 0 Formula (рецепт) |
| `0x03` | 19 | 0 Sword⁰, 1 `DAT_0071b3d0`(3 симв.), 2 Mace⁰, 3 Dagger⁰, 4 Fist⁰, 5 Longsword, 6 `DAT_0071b3d8`(3 симв.), 7 Crossbow⁰, 8 Boomerang⁰, 9 Arrow, 0xa Staff, 0xb Wand, 0xc Bracelet, 0xd Shield, 0xe Arrows, 0xf Greatsword, 0x10 Greataxe, 0x11 Greatmace, 0x14 **Torch** |
| `0x04` | 1 | 0 ChestArmor |
| `0x05` | 1 | 0 Gloves⁰ |
| `0x06` | 1 | 0 Boots⁰ |
| `0x07` | 1 | 0 ShoulderArmor |
| `0x08` | 1 | 0 Amulet |
| `0x09` | 1 | 0 Ring |
| `0x0b` | 28 | 0 Nugget, 1 `DAT_0071bad8`, 2 Feather, 3 Horn, 4 Claw, 5 Fiber, 6 Cobweb, 7 Hair, 8 Crystal, 9 Yarn, 0xa Cube, 0xb Capsule, 0xc **Flask**, 0xd `DAT_0071bb5c`, 0xe Spirit, 0xf Mushroom, 0x10 Pumpkin, 0x11 Pineapple, 0x12 RadishSlice, 0x13 ShimmerMushroom, 0x14 GinsengRoot, 0x15 OnionSlice, 0x16 Heartflower, 0x17 PricklyPear, 0x18 FrozenHeartflower, 0x19 Soulflower, 0x1a **WaterFlask**, 0x1b SnowBerry |
| `0x0c` | 1 | 0 Coin |
| `0x0d` | 1 | 0 PlatinumCoin |
| `0x0e` | 1 | 0 Leftovers |
| `0x0f` | 1 | 0 Beak |
| `0x10` | 1 | 0 Painting |
| `0x12` | 2 | 0 **Candle**, 1 **Candle** (два варианта модели/предмета под одним именем) |
| `0x13` | 1 | 0 `DAT_00700a30`(3 симв.) — имя не восстановлено (надпись из пула строк, не литерал) |
| `0x14` | 47 | 0 **Bait** (заполняется циклом для подтипов 0…0x9b), далее еда: 0x13 BubbleGum, 0x16 VanillaCupcake, 0x17 ChocolateCupcake, 0x19 CinnamonRole, 0x1a Waffle, 0x1b Croissant, 0x1e Candy, 0x21 PumpkinMash, 0x22 CottonCandy, 0x23 Carrot, 0x24 BlackberryMarmelade, 0x25 GreenJelly, 0x26 PinkJelly, 0x27 YellowJelly, 0x28 BlueJelly, 0x32 BananaSplit, 0x35 Popcorn, 0x37 LicoriceCandy, 0x38 CerealBar, 0x39 SaltedCaramel, 0x3a GingerTartlet, 0x3b MangoJuice, 0x3c FruitBasket, 0x3d MelonIceCream, 0x3e BloodOrangeJuice, 0x3f MilkChocolateBar, 0x40 MintChocolateBar, 0x41 WhiteChocolateBar, 0x42 CaramelChocolateBar, 0x4a SugarCandy, 0x4b AppleRing, 0x56 WaterIce, 0x57 ChocolateDonut, 0x58 Pancakes, 0x5a StrawberryCake, 0x5b ChocolateCake, 0x5c Lollipop, 0x5d Softice, 0x62 CandiedApple, 0x63 DateCookie, 0x66 Bread, 0x67 Curry, 0x68 Lolly, 0x69 ChocolateCookie, 0x6a StrawberryCocktail, 0x97 BiscuitRole |
| `0x15` | 10 | 0 Amulet1, 1 Amulet2, 2 JewelCase, 3 `DAT_0071bcf8`, 4 Medicine, 5 Antivenom, 6 BandAid, 7 Crutch, 8 Bandage, 9 Salve |
| `0x17` | 2 | 0 **HangGlider**, 1 **Boat** |
| `0x18` | 1 | 0 **Lamp** |
| `0x19` | 1 | 0 **ManaCube** |

`⁰` — тип подтверждён, подтип `AMBIGUOUS` (см. §0 и §5).

**Что доказано этими данными:**

* **Фляга и вода в фляге — это предметы типа `0x0b` (материалы), подтипы `0x0c` и `0x1a`**, а не зелья.
  Питьевые зелья — тип `0x01`; бомба — тип `0x01`, подтип `7`.
* **Дельтаплан и лодка — предметы одного типа `0x17`** (подтипы 0 и 1), лампа — `0x18`, мана-куб — `0x19`,
  свеча — `0x12` (два подтипа). Это единственные «носимые» предметы, у которых есть собственный тип,
  отдельный от экипировки (`0x03…0x09`).
* «Спец-слот» `0x13` в `M0-00B` I-09 назван по смещению слота; в таблице имён тип `0x13` существует, но
  имя не восстановлено.

---

## 2. Объекты домов и мебель: тип → имя (68 записей)

`World_map_find_or_insert_int`, строки 3670…3944. Это словарь **объектов интерьера/города**
(ранее в `M0-00B` W-17 фигурировали только диапазоны id `0x88d…0x8ef` без семантики).

| id | имя | id | имя | id | имя |
|---|---|---|---|---|---|
| 0x00 | Statue | 0x17 | MarketStand3 | 0x31 | FlowerBox3 |
| 0x01 | Door | 0x18 | Barrel | 0x32 | StreetLight |
| 0x02 | BigDoor | 0x19 | Crate | 0x33 | FireStreetLight |
| 0x03 | Window | 0x1a | OpenCrate | 0x34 | Fence1 |
| 0x04 | CastleWindow | 0x1b | Sack | 0x35 | Fence2 |
| 0x05 | Gate | 0x1c | Shelter | 0x36 | Fence3 |
| 0x06 | FireTrap | 0x1d | Cupboard | 0x37 | Fence4 |
| 0x07 | SpikeTrap | 0x1e | Desktop | 0x38 | Vase1 |
| 0x08 | StompTrap | 0x1f | Counter | 0x39 | Vase2 |
| 0x09 | Lever | 0x20 | Shelf1 | 0x3a | Vase3 |
| 0x0a | Chest | 0x21 | Shelf2 | 0x3b | Vase4 |
| 0x0c | Table | 0x22 | Shelf3 | 0x3c | Vase5 |
| 0x0d | Table | 0x2c | Corpse | 0x3d | Vase6 |
| 0x0e | Table | 0x2d | RuneStone | 0x3e | Vase7 |
| 0x0f | Stool | 0x2e | Artifact | 0x3f | Vase8 |
| 0x10 | Stool | 0x2f | FlowerBox1 | 0x40 | Vase9 |
| 0x11 | Stool | 0x30 | FlowerBox2 | 0x41 | Campfire |
| 0x12 | Bench | | | 0x42 | Tent |
| 0x13 | `DAT_0071be5c` | 0x43 | BeachUmbrella | 0x4b | SawBench |
| 0x14 | BedTable | 0x44 | BeachTowel | 0x4c | Workbench |
| 0x15 | MarketStand1 | 0x45 | SleepingMat | 0x4d | **CustomizationBench** |
| 0x16 | MarketStand2 | 0x47 | Furnace | | |
| | | 0x48 | Anvil | | |
| | | 0x49 | SpinningWheel | | |
| | | 0x4a | Loom | | |

**Следствия:** появляется точная семантика для интерактивных объектов города: `Lever` (0x09) и три вида
ловушек (`FireTrap` 0x06, `SpikeTrap` 0x07, `StompTrap` 0x08), `Chest` (0x0a, сундук), `RuneStone` (0x2d),
`Artifact` (0x2e), три типа рыночных прилавков (0x15…0x17), кровати/спальные места (`BedTable` 0x14,
`SleepingMat` 0x45), профессии-верстаки (`Furnace` 0x47, `Anvil` 0x48, `SpinningWheel` 0x49, `Loom` 0x4a,
`SawBench` 0x4b, `Workbench` 0x4c, `CustomizationBench` 0x4d). Три «Table» и три «Stool» — разные id с
одинаковым именем (модельные варианты). Пустые id в таблице — записи в источнике отсутствуют.

---

## 3. Навыки и способности: id → имя (27 записей)

`World_map_find_or_insert_int`, строки 3945…4052. Это узлы дерева навыков.

**Ранги (11 значений, массив `i32[11]` у существа — см. `+0x1138`, копирование `memcpy 0x2c` в
`0x43…`):**

| id | имя | смысл |
|---|---|---|
| 0 | SkillPetTaming | приручение питомцев |
| 1 | SkillPetRiding | езда на питомце |
| 2 | SkillClimbing | лазание |
| 3 | SkillHangGliding | дельтаплан |
| 4 | SkillSwimming | плавание |
| 5 | SkillBoatDriving | лодка |
| 6…10 | SkillAbility1…SkillAbility5 | пять слотов способностей |

**Способности (16 узлов, совпадают с P-15):**

| id | имя | id | имя |
|---|---|---|---|
| 0x15 | AbilityRangerKick | 0x56 | AbilityCyclone |
| 0x22 | AbilityHealingStream | 0x58 | AbilityFireExplosion |
| 0x30 | AbilityIntercept | 0x60 | AbilityShuriken |
| 0x31 | AbilityTeleport | 0x61 | AbilityCamouflage |
| 0x32 | AbilityRetreat | 0x63 | AbilityAim |
| 0x36 | AbilitySmash | 0x64 | AbilitySwiftness |
| 0x4f | AbilitySneak | 0x65 | AbilityBulwark |
| | | 0x66 | AbilityWarFrenzy |
| | | 0x67 | AbilityManaShield |

**Классовая принадлежность по именам (гипотеза, требует подтверждения по коду классов):**
Warrior — Smash 0x36, Cyclone 0x56, Bulwark 0x65, WarFrenzy 0x66; Ranger — RangerKick 0x15, Retreat 0x32,
Aim 0x63, Swiftness 0x64; Mage — FireExplosion 0x58, ManaShield 0x67, Teleport 0x31, HealingStream 0x22(?);
Rogue — Sneak 0x4f, Camouflage 0x61, Shuriken 0x60, Intercept 0x30(?). Пометка `ГИПОТЕЗА`; в
`drawItemTooltip @ 0x4a28c0` класс читается из `Creature+0x140` (1…4 = Warrior/Ranger/Mage/Rogue), но
прямой таблицы «класс → набор id» в этой выписке нет.

---

## 4. Стоимость и кулдауны способностей (прямые формулы)

| Что | Адрес | Формула/значения |
|---|---|---|
| Кулдаун | `Ability_get_cooldown @ 0x444860` | Только для роли не 3 и не 5 (не союзник/NPC). Секунды: id 1,2,9 → `6/haste`; 3,4,0x3e → `3/haste`; 5 → 4; 6,7,0x12,0x13 → `3/haste`; 0xb,0x57 → 10; 0xd,0xe,0xf,0x3c,0x3d → `6/haste`; 0x11,0x14 → 6; 0x15 → `powerFactor·30+5`; 0x36 → 5; прочее → `2/haste` |
| Мана | `ability_getManaCost @ 0x43e6a0` | `base − powerFactor·scale` в единицах 1/100000: 0x15 8000/8000; 0x30 20000/12000; 0x31,0x32 16000/10000; 0x36 20000/14000; 0x48 = 15000 (const); 0x56 60000/40000; 0x58 8000/8000; 0x60 20000/14000; 0x61 60000/30000; 0x63(99) 12000/10000; 0x64(100),0x65,0x66 60000/30000; 0x67 20000/10000; иначе 0 |
| Стамина | `combat_getStaminaCost @ 0x444270` | `base/haste`: 100 для id 0,0x32,0x60; 300 для 3,4,5,0x3e; 200 для 7,0xe,0x12; 100 для 0xb,0x3c,0x3d,0x68; 400 для 0xf; 500 — по умолчанию; 600 для 10 |
| Канал | `ability_isChanneled @ 0x444680` | Канал, если `(itemFlags+0x7e & 0x1a00) == 0` и id ∈ {0x5c,0x4a,0x22,0x19,0x37,0x35,0x57,0x44,0x43,0x5d,0x6a,0x6b,0x23,0x3a,0x39,0x93,0x91,0x92,0x16,0x17,0x62,0x38,0x1e,0x1f,0x20,0x13,0x1a,0x1b,0x21,0x64,0x14,0x59} |
| Мили-тип | `weapon_isMeleeType @ 0x444760` | Набор id (0x13…0x28, 0x3f…0x43, 0x4a, 0x4b, 0x62, 0x63, 0x64, 0x66…0x69, 0x97) — это классификация **способности**, а не подтипа оружия |

Замечание к реестру: `ability_isChanneled` использует id > 0x80 (`0x91,0x92,0x93`), то есть пространство
action-id шире, чем «73 состояния/101 id», зафиксированные в S05. Это не противоречие (S05 считал
состояния отрисовки), но при планировании M0-05B нельзя опираться на «id ≤ 0x77».

---

## 5. Что осталось недоказанным и как это закрыть

| # | Вопрос | Почему не закрыт | Решающий тест |
|---|---|---|---|
| A-1 | Подтипы оружия 0,1,2,3,4,6,7,8 (`AMBIGUOUS`) | Два чтения одного блока декомпиляции | Исполнить `GameController_item_icon_id @ 0x4ec400` для (3,0)/(3,3) и сравнить модель с моделью живого меча (метод уже применялся в D64), либо снять в игре имя предмета первого оружия Warrior |
| A-2 | Типы `5`/`6` = Gloves/Boots или наоборот | Тот же блок; альтернативное чтение воспроизводит I-09 | Тот же приём: модель перчаток/сапог через `item_icon_id` + сверка с `.cub`-именем |
| A-3 | Имена `DAT_0071b3d0`, `DAT_0071b3d8`, `DAT_0071bad8`, `DAT_0071bb5c`, `DAT_0071bcf8`, `DAT_00700a30`, `DAT_0071be5c` | В пуле строк лежат не литералы, а указатели длиной 3 символа | Чтение wide-строки по адресу из `Cube.exe` (только чтение), либо поиск тех же 3-символьных строк в `data1.db` |
| A-4 | Класс → набор способностей | В таблице имён класса нет | Найти switch по `Creature+0x140` в UI дерева навыков (`AdaptionWidget`) и выписать список узлов по классу |
| A-5 | Тип `0x15` (Amulet1/Amulet2/JewelCase/Medicine/…) | Семантика типа не определена | Проверить, участвует ли тип в слотах/луте: поиск проверок `== 0x15` по коду |

---

**Ограничения.** Живая игра не запускалась; Unity не запускалась; числа не сверялись с игровым
экраном. Все имена — из статической декомпиляции и являются внутренними именами Alpha (не финальный
текст будущей игры). Оригинальные ассеты, бинарники и `data*.db` в репозиторий не попадают.
