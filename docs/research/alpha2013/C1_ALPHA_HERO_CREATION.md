# C1 — создание героя Alpha 2013 по байтам Cube.exe (волна 1, D88)

**Дата:** 2026-11-10. **Исполнитель:** Claude Code. **Ветка:** `integration/m0-all`. **Статус:** `READY_FOR_REVIEW`, не ACCEPTED.
**Метод:** реконструкция `.scratch/CubeWorld-Reversal` как указатель, числа сверены с байтами `Cube.exe` (capstone): таблицы переходов `switch` разобраны напрямую.

## 1. Поля редактора (виджет создания, `drawCharacterAppearancePanel` 0x428E40)

| Поле виджета | Смысл | Значения |
|---|---|---|
| `+0x18C` | раса | 0 Human, 1 Elf, 2 Dwarf, 3 Orc, 4 Goblin, 5 Lizard, 6 Undead, 7 Frogman (подписи панели) |
| `+0x194` | пол | 0 Male, 1 Female |
| `+0x190` | класс | 0 Warrior, 1 Ranger, 2 Mage, 3 Rogue → существо `+0x140 = класс + 1` |
| `+0x198` / `+0x19C` | лицо / причёска | индекс внутри диапазона моделей вида |
| `+0x1A0…0x1A2` | цвет волос (RGB) | выбранный; `+0x1A3…0x1A5` — под курсором |

Умолчания `CharCreation_reset_defaults` 0x42BD90: раса 0, пол 0, класс 0, лицо 0, причёска 0, цвет волос **FF C8 32**.

## 2. Раса + пол → вид существа (`CharCreation_refresh_model` 0x42C080)

`+0x64 = база[раса] + пол`: Human 0x02, Elf 0x00, Dwarf 0x09, Orc 0x0B, Goblin 0x04, Lizard 0x07, Undead 0x0F, Frogman 0x0D (женский = +1). Вид 0x06 редактором не используется.

## 3. Диапазоны моделей лица и причёски (таблица переходов 0x42C41C)

| Вид | Головы | Причёски |
|---|---|---|
| 0x00 ElfMale | 0x4D4…0x4D7 | 0x500…0x509 |
| 0x01 ElfFemale | 0x4D8…0x4DD | 0x50A…0x513 |
| 0x02 HumanMale (default) | 0x4DE…0x4E3 | 0x4E4…0x4F2 |
| 0x03 HumanFemale | 0x4F3…0x4F8 | 0x4F9…0x4FF |
| 0x04 GoblinMale | 0x04B…0x04F | 0x050…0x055 |
| 0x05 GoblinFemale | 0x056…0x05A | 0x05B…0x060 |
| 0x07 LizardmanMale | 0x062…0x063 | 0x064…0x069 |
| 0x08 LizardmanFemale | 0x06A…0x06E | 0x064…0x069 |
| 0x09 DwarfMale | 0x11A…0x11E | 0x11F…0x121 |
| 0x0A DwarfFemale | 0x122…0x126 | 0x127…0x12B |
| 0x0B OrcMale | 0x514…0x518 | 0x51E…0x527 |
| 0x0C OrcFemale | 0x519…0x51D | 0x528…0x52B |
| 0x0D FrogmanMale | 0x52C…0x530 | 0x531…0x535 |
| 0x0E FrogmanFemale | 0x536…0x539 | 0x53A…0x53D |
| 0x0F UndeadMale | 0x12F…0x134 | 0x135…0x13A |
| 0x10 UndeadFemale | 0x13B…0x140 | 0x141…0x146 |

Индекс по кругу: `< 0 → последний`, `≥ число → 0`; выполняется при каждой смене расы/пола/лица/причёски. Затем `creature_generateAppearance(+0x64, +0x78, appearance)` (0x43F7C0) строит внешность — это уже конвейер моделей S05/D4.

## 4. Палитра цвета волос (0x428E40)

16 базовых оттенков (`setVec4`): красный, (1,¼,0), (1,½,0), жёлтый, (½,1,0), зелёный, (0,1,½), циан, (0,½,1), (0,¼,1), синий, (¼,0,1), (½,0,1), (¾,0,1), пурпурный, серый (½,½,½). 7 строк: строки 0–2 — `c × (0.2 + 0.25·i)`, строка 3 — сам оттенок, строки 4–6 — `c + (1 − c)·0.25·(i − 3)`; байт = `(int)(c·255) & 0xFF`. Итого 112 цветов.

## 5. Имя

Поле имени существа `+0x1168` — 16 байт (копирование `0x60E62D` отказывает при длине ≥ 0x10) → не больше **15** символов. Прежняя запись «≤255 байт» (P-06) относилась к общему текстовому полю ввода.

## 6. Стартовый набор (`GameController_start_game_mode` 0x4772B0, таблица 0x4777D8 по `+0x140 − 1`)

Байты предмета: тип, подтип, материал `+0x0D`, уровень `+0x10` = 1 (инициализация 0x42F3E0), редкость 0.

| Класс | В руках (слоты Alpha) | В сумку |
|---|---|---|
| Warrior | правая `0xAA8`: Greatmace (3, 0x11), материал 2 | 2× Sword (3, 0) мат. 1, Shield (3, 0x0D) мат. 1 |
| Ranger | правая: Bow (3, 6), мат. 2 | Crossbow (3, 7) мат. 2, Wand (3, 0x0B) мат. 2 |
| Mage | правая: Staff (3, 0x0A), мат. 2 | 2× Bracelet (3, 0x0C) мат. 0x0B, Wand (3, 0x0B) мат. 2 |
| Rogue | правая и левая `0x990`: Dagger (3, 3), мат. 1 | 2× Fist (3, 4) мат. 1, Longsword (3, 5) мат. 1 |
| все | кольца в слотах `0xCD8` (9, 0, мат. 0x0B) и `0xBC0` (9, 0, мат. 0x0C), лампа в `0xDF0` (0x18, 0, мат. 1) | 5× (1, 1) — зелье жизни |

Затем HP = максимум (`stat_calcAttackDamage`). Прежняя таблица D_character §D3 расходилась в нескольких строках (Ranger, Rogue) — она исправлена этой.

## 7. Что сделано в Unity

| Файл | Что |
|---|---|
| [AlphaHeroCreation.cs](../../../Assets/CubeGame/Features/Session/AlphaHeroCreation.cs) | расы, вид, диапазоны, круговой индекс, палитра, имя, стартовые наборы |
| [HeroAppearance.cs](../../../Assets/CubeGame/Features/Contracts/HeroAppearance.cs), `EntitySpawned.Appearance` | выбор редактора для моделей (D4) |
| [GameSession.cs](../../../Assets/CubeGame/Features/Session/GameSession.cs) | `HeroState.Appearance/Kind`, `GiveStarterKit`; новый мир берёт выбор и набор Alpha вместо временного двуручного меча |
| `SessionSaveData` / `GameSlotStore` | необязательные поля пола/лица/причёски/цвета; старые сейвы → Human, умолчания Alpha |
| [GameBootstrap.cs](../../../Assets/CubeGame/Presentation/Gameplay/GameBootstrap.cs) | редактор в меню нового мира (IMGUI): раса, пол, лицо, причёска, палитра 16×7, класс, имя |
| [FullM0HeroCreationTests.cs](../../../Assets/CubeGame/Tests/FullM0/EditMode/FullM0HeroCreationTests.cs) | 11 тестов |

## 8. Ограничения

- **Материал 2** (деревянное оружие набора) Core S04 не принимает (тест S04 `UnsupportedModifierIsRejectedByPublicConstructor(2)`); пока предмет получает материал `None`, байт Alpha хранится в `StarterKitItem.AlphaMaterial`. Урон оружия материал не читает — бой не меняется. Нужно решение продюсера: разрешить значение 2 в Core.
- Кольца и лампа лежат в сумке, пока нет слотов колец/лампы (C5).
- Вид героя: модели рас, лиц, причёсок и цвет волос рисует D4 (DeepSeek); сейчас любой герой выглядит как HumanMale с мечом. Тайминги атаки героя — с HumanMale (атака 57); лучник/маг пока бьют ближней атакой — C4.
- Специализация в редакторе Alpha не выбирается (Primary).
