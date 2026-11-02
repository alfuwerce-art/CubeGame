# S00-R02 / пакет D — создание героя, классы, специализации, прогрессия (Alpha 2013)

**Проект:** CubeGame. **Этап:** S00-R02 (D41), партия 1.
**Источники:** `SRC-01` (сейв `characters.db` персонажа `S00G0`), `SRC-02` (Reversal @ `e6e906e4`).
**Проверено лично:** таблица имён навыков `World.cpp:3945-3988`, поля `Player_resetState` (`0x16c`, `0x168`, `0x170`, `0x180`, `0x184`), значение `0x16c = 268.0f` в реальном сейве (совместно с субагентом D).
**Граница:** собственные описания; тела функций и блобы сейвов не публикуются.

## D1. Один класс на всех: `Creature`

Игрок, моб и NPC — один класс (аллокация ≈ `0x1e60`; `Creature::ctor_0` `0043b690`). Различия — в полях: класс `0x140` (только у игрока 1…4), ранги навыков `0x1138`, имя `0x1168`, appearance `0x1d28`, роль `0x60` (0 = игрок), звёзды `0x1a8` (только у мобов), инвентарь `0x11dc…` (у игрока 4×50).

## D2. Создание героя (экраны и поля)

| Опция | Значения | Где | Уверенность |
|---|---|---|---|
| Раса | 8: Human, Elf, Dwarf, Orc, Goblin, Lizard, Undead, Frogman | `GameController::drawCharacterAppearancePanel` `00428e40` (строки 8332-8359) | DIRECT |
| Класс | 4: Warrior, Ranger, Mage, Rogue | там же (8701-8713); в существо пишется `+0x140 = classIdx + 1` | DIRECT |
| Пол | Male / Female | там же (8531-8536) | DIRECT |
| Face / Haircut | индексы; **диапазон зависит от style id** | `CharCreation_refresh_model` `0042c080` (клампы 9673-9688) | DIRECT |
| Цвет волос | палитра 16 цветов; RGB; по умолчанию `FFC832` | `00428e40` (9386-9417), `CharCreation_reset_defaults` `0042bd90` | DIRECT |
| Имя | свободный ввод, ≤255 байт в существе | `NameInput_handle_key` `0043a0d0`, десериализация `Creature.cpp:870` | DIRECT |

Style id **не равен** расе: `styleId = baseStyle[race] + gender`, где `baseStyle = {0→2, 1→0, 2→9, 3→11, 4→4, 5→7, 6→15, 7→13}`; достижимо 16 значений из 0…16. Для каждого style id задан свой набор допустимых Face/Haircut (например, style 0: head `0x4d4..0x4d7`, hair `0x500..0x509`; style 9: `0x11a..0x11e` / `0x11f..0x121`).

После подтверждения выбора:
1. `Creature+0x64 = styleId` (внешность/модель);
2. `Creature+0x140 = класс 1…4`;
3. в appearance-объект `+0x1d28`: `+0x00` раса, `+0x04` пол, `+0x08` Face, `+0x0c` Haircut, `+0x10..0x12` RGB;
4. `creature_generateAppearance(styleId, +0x78, appearance)` — генерация модели.

### FEATURE_ID: D2.char-creation
CONFIDENCE: STRONGLY_SUPPORTED
CUBEGAME_DECISION: PROPOSED — каталог вариантов + правила диапазонов (зависят от модели, а не константы) в данных, не в коде.
UNITY_MAPPING: `ScriptableObject`-каталог + собственные правила валидации; рендер внешности — `SkinnedMeshRenderer`/blend shapes (без Plasma-скелетов).
GAPS: конкретные id моделей (0x4d4…0x535) требуют пакета моделей; смысл 11 узлов appearance — `UNKNOWN`.

## D3. Стартовый набор (по классу)

`GameController_start_game_mode` (`0x47742e`+): инвентарь 4×50; далее по классу:

| Класс | Основное оружие `(+0xaa8/+0xaa9)` | Дополнительно |
|---|---|---|
| Warrior | (3, 0x11) — Greatmace | 2× (3,0) + (3,0x0d) Shield |
| Ranger | (3, 6) — Bow | (3,7) + (3,0x0b) Wand |
| Mage | (3, 0x0a) — Staff | 2× (3,0x0c) Bracelet + (3,0x0b) Wand |
| Rogue | (3, 3) — Sword | 2× (3,4) Dagger; второе оружие (3,3) |
| все | — | Ring ×2 (9,0), Lamp (0x18,0), 5× (1,1) расходник |

## D4. Прогрессия

### D4.1 Опыт (DIRECT, воспроизведено скриптом)

`cost(L) = int((1 − 1/((L−1)·0.05 + 1))·1000 + 50)`; асимптота `1050`.
`L=1→50`, `2→97`, `5→216`, `10→360`, `20→537`, `50→760`, `100→881`. Сумма 1→100 — 68 695 XP.
Повышение уровня: `while XP(0x194) >= cost(L): XP -= cost; L += 1; HP_max = calcMaxHealth()`. Верхнего капа в коде нет (`UNKNOWN`, см. D7).

### D4.2 Максимальное HP и урон

**Резолюция противоречия.** Два независимых чтения дали разные трактовки функции `stat_calcAttackDamage` (`00444db0`): «кэш урона» и «максимальное HP». Разрешено в пользу **максимального HP** по четырём свидетельствам:
1. HP `+0x16c` уменьшается от урона и клампится в 0 → смерть (`applyMeleeHit` `00595a60`);
2. после лечения HP клампится **сверху** значением этой функции (`HP = min(HP, f())`);
3. ИИ пьёт зелье при `HP < 0.25·f()` (порог «25 % здоровья»);
4. в реальном сейве `+0x16c = 268.0f` у Warrior 1 уровня без урона: `100 (база 0x178) · 2.0 (звёздный множитель игрока) · 1.0 (уровень) · 1.3 (Warrior) + 2·4 (статы предметов) = 268` — сходится точно.

Формула максимального HP (собственное описание):
`maxHP = base(0x178) · levelFactor(L) · starFactorOr2 · classMult · specMult + equipmentBonuses`,
где `levelFactor(L) = 2^(curve_level05(L)·3)`; `starFactor = 2^(0.25·S)` для роли `0x60 != 0` (моб/NPC/питомец), иначе для игрока **2.0**; `classMult`: Warrior 1.3, Ranger 1.1, Mage 1.0, Rogue 1.2; `specMult` 1.25 для `class==1 && spec==1`. `equipmentBonuses` — сумма статов надетых предметов (не «руны»).

Урон оружия — отдельная функция `stat_calcWeaponDamage` (`0043dc60`): `base(0x180) · levelFactor` + флаги апгрейдов (`0x7e&0x8` → `+levelFactor·starFactor·2`, `0x7e&0x10` → `·8`) или список `item_weaponDamage`, затем множители типа атаки (`0x57 ×50`, `0x5b ×2`, `0x5d ×5`, `0x60 ×3`).
Подробности и таблица — `E_combat.md` §E2.

### D4.3 «Power» способностей (DIRECT)

`powerFactor(rank) = 1 − 1/(rank·0.1 + 1)`; если ранг слота равен 0 и роль `0x60 != 0` (не игрок), то
`rank = level/2 + star + 1` — это и есть **effectivePower**. То же выражение используется в haste-баффе `0x0c`.

### D4.4 Разница уровней

`computeLevelDiffBonus` (`0040f4f0`): при `targetLevel < playerLevel` сумма
`Σ_{i=1..Δ} 2·2^((targetStar − 1 + targetLevel)·0.25)`; используется при приручении/улучшении существа как «цена». `PARTIAL`.

## D5. Специализации и навыки

### D5.1 Специализации (DIRECT)

| Класс | spec = 0 | spec = 1 |
|---|---|---|
| Warrior | Berserker | Guardian |
| Ranger | Sniper | Scout |
| Mage | Fire | Water |
| Rogue | Assassin | Ninja |

### D5.2 Ранги навыков — `i32[11] @ 0x1138` (DIRECT)

| idx | имя в данных | смысл |
|---|---|---|
| 0 | `SkillPetTaming` | приручение |
| 1 | `SkillPetRiding` | езда |
| 2 | `SkillClimbing` | лазание |
| 3 | `SkillHangGliding` | дельтаплан |
| 4 | `SkillSwimming` | плавание |
| 5 | `SkillBoatDriving` | лодка |
| 6…10 | `SkillAbility1…5` | пять активных способностей |

> **Исправление к первой редакции `E_combat.md`.** Слоты 1–5 были названы «профессиями»; на самом деле это транспортно-двигательные навыки. Профессии (крафт) — отдельная система (пакет F).

### D5.3 Как тратятся ранги (PARTIAL)

В `xp_applyLevelUps` очков навыков **нет**. Найден только механизм **покупки**: `GameController_computeQuestScore` (`004df9c0`) считает цену изменения рангов (`Σ по 11 слотам 2^(level)·Δrank·10` + бонус за смену специализации), затем обработчик сверяет цену с полем `+0x1304` (это **деньги** — доказано buy/sell-кодом) и копирует «ожидающие» ранги из `GC+0x160` в существо. Кто наполняет `GC+0x160` и как игрок «зарабатывает» право прокачки — `UNKNOWN`.

⚠️ Расхождение с внешними описаниями: wiki (SRC-03e) говорит о «2 очках навыка за уровень» в Alpha. В коде такого нет; возможно, очки выдавались на другом этапе или wiki описывает 2019. Помечаем `SOURCE_VERSION_MIXED`, факт не подтверждён.

### FEATURE_ID: D5.skill-ranks
CONFIDENCE: STRONGLY_SUPPORTED (таблица и формула), UNKNOWN (источник очков)
CUBEGAME_DECISION: DEFERRED — модель прокачки решает продюсер (D30: дерево пассивных навыков может быть полностью открыто); копировать «покупку за деньги» не обязательно.
UNITY_MAPPING: own-game-logic; UI — Unity UI Toolkit.
GAPS: источник очков, полная таблица способностей (16 известных id — см. ниже).

### D5.4 Известные id способностей (PARTIAL, 16 из ~…)

`AbilityRangerKick` 0x15, `AbilityHealingStream` 0x22, `AbilityIntercept` 0x30, `AbilityTeleport` 0x31, `AbilityRetreat` 0x32, `AbilitySmash` 0x36, `AbilitySneak` 0x4f, `AbilityCyclone` 0x56, `AbilityFireExplosion` 0x58, `AbilityShuriken` 0x60, `AbilityCamouflage` 0x61, `AbilityAim` 0x63, `AbilitySwiftness` 0x64, `AbilityBulwark` 0x65, `AbilityWarFrenzy` 0x66, `AbilityManaShield` 0x67. Полная таблица — `UNKNOWN`.

## D6. Экипировка: 13 слотов по `0x118`

| # | Смещение | Тип (byte0) | Слот |
|---|---|---|---|
| 0 | `0x300` | — | не адресуется в коде (`UNKNOWN`) |
| 1 | `0x418` | 8 | Neck/Amulet |
| 2 | `0x530` | 4 | Chest |
| 3 | `0x648` | 6 | Hands (Gloves) |
| 4 | `0x760` | 5 | Feet (Boots) |
| 5 | `0x878` | 7 | Shoulder |
| 6 | `0x990` | 3 | второе оружие / левая рука |
| 7 | `0xaa8` | 3 | **основное оружие** |
| 8 | `0xbc0` | 9 | Ring |
| 9 | `0xcd8` | 9 | Ring |
| 10 | `0xdf0` | 0x18 | Light (лампа/факел) |
| 11 | `0xf08` | — | не идентифицирован |
| 12 | `0x1020` | 0x13 | «Special» |

Порядок UI-подписей (`Left Weapon`, `Right Weapon`, `Left Ring`, `Right Ring`, `Neck`, `Shoulder`, `Chest`, `Hands`, `Feet`, `Light`, `Special`) не совпадает с порядком смещений → соответствие «UI-индекс ↔ смещение» `PARTIAL`.

## D7. UNKNOWN (пакет D)

| # | Вопрос | Как снять |
|---|---|---|
| D-U1 | Жёсткий кап уровня | прогон с правкой уровня в сейве; xref-поиск сравнений `[creature+0x190]` с константой |
| D-U2 | Источник очков/права прокачки | trace UI `SkillWidget`/`AdaptionWidget`, watchpoint на `GC+0x160` |
| D-U3 | Полная таблица способностей | де-обфускация `dict_en.xml` из `data4.db` + таблица в `0x4e…` |
| D-U4 | Смысл `+0x1198`, `+0x119c`, `+0x114`, `+0x12` | watchpoint при уроне/получении опыта |
| D-U5 | Слоты экипировки 0 (`0x300`) и 11 (`0xf08`) | игра: надеть предмет в каждый слот, сравнить блоб |
| D-U6 | 11 узлов appearance | сменить Face/Haircut/цвет в игре, сравнить блоб |

## D8. Unity-маппинг (вывод)

| Правило Alpha | Unity 6.3 builtin | Собственный код |
|---|---|---|
| Каталог рас/классов/внешности | `ScriptableObject`, `Localization` (если разрешат) | правила диапазонов, валидация, применение к модели |
| XP/уровень/HP-формулы | — | чистые C#-функции + NUnit-таблицы |
| Экипировка 13 слотов | — | модель слотов + правила «тип → слот» |
| Ранги навыков | — | массив + маппинг категория→индекс (собственная таблица) |
| Сохранение персонажа | `Application.persistentDataPath`, `JsonUtility` для простого | версионированный бинарный формат с миграциями (см. `DATA_AND_SAVE_SCHEMAS.md`) |
| Внешний вид | `SkinnedMeshRenderer`, blend shapes | данные вариантов, собственные модели S05 |
