# D86 M0-04B — предметный визуальный слой: модели оружия и законы крепления (партия DeepSeek, №3)

**TASK:** D86 (issue #13), M0-04B в зоне DeepSeek: подключить предметные модели к главному ригу через измеренные законы крепления Alpha (26 подтипов оружия, 10 классов левой руки), заменить модель оружия в рантайме, показать «в руке / за спиной» без имитации статов.

**STATUS: `READY_FOR_REVIEW` — не `ACCEPTED`.** `main` не тронут, ассеты Alpha не публиковались, S04 не изменялся.

**Ветка:** `deepseek/d85-visual-unity` (после `1f591c4`). **Worktree:** `D:\Projects\CubeGame-deepseek`. **Unity:** 6000.3.27f2.

---

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Законы крепления | в Python (`universal/item_attach.py`) и в исследовании M0-00B | сгенерированная C#-таблица `AlphaAnchorSetDef[26]` + `[10]` в `Assets/CubeGame/Content/Generated` (с SHA входного evidence) |
| Модель оружия | фиксировалась ригом при сборке (greatsword из job) | `AlphaWeaponLayer.TryEquip*` меняет меш оружия в рантайме по `.cubdec`, ставит измеренные якоря рука/спина, отказывает неизмеренным подтипам |
| Якоря рига | универсальное правило `center` / `+4` для всех | при экипировке риг получает точные `weaponAnchorInHand/OnBack` (лук `sy−1.5`, посох `z=10`, бумеранг, кулак и т.д.); без экипировки — прежнее поведение (регрессий нет) |
| Предметные payload’ы | не подготовлены в кэше | `tools/alpha_content/extract_item_models.py` (переиспользует принятый `cub_format`) → `.scratch/content/items`, 28 моделей |
| Тесты | — | +2 EditMode, +1 PlayMode; полный прогон **335/335** и **26/26** |

---

## 2. Файлы

| Файл | Назначение |
|---|---|
| `tools/alpha_content/build_content_tables.py` | теперь также выпускает `AlphaAnchorDef`, `AlphaAnchorSetDef`, `AlphaWeaponAnchors[26]`, `AlphaOffhandAnchors[10]`; `--check` ловит дрейф |
| `Assets/CubeGame/Content/Generated/AlphaContentTables.g.cs` | таблицы якорей: `coeff`, `const`, `scale`, имя закона, статус (`CODE_DERIVED_MEASURED`, для стрелы — `CODE_DERIVED`) |
| `Assets/CubeGame/Content/AlphaItemRules.cs` | `TryWeaponAnchors`, `TryOffhandAnchors`, `IsAnchorMeasured`, `WeaponAnchorSets/OffhandAnchorSets`; валидация 26/10 и единственного `PARTIAL` (стрела 9) |
| `Assets/CubeGame/AlphaModels/Runtime/AlphaWeaponLayer.cs` | `TryEquip(rig, bytes, subtype)`, `TryEquipFromFile`, `ExpectedMeshOffset`, `EquippableInHand`; отказы: `WEAPON_SUBTYPE_UNKNOWN`, `ANCHOR_NOT_MEASURED`, `WEAPON_MESH`, `WEAPON_MODEL_HAS_MARKERS`, `WEAPON_FILE_MISSING` |
| `Assets/CubeGame/AlphaModels/Runtime/AlphaOffhandLayer.cs` | модель левой руки: child `offhand_mesh` у `part_hand_l`, измеренный якорь (`shield y=3`, `bow sy−1.5`, `bracelet scale 0.6` и т.д.), `Clear`, `HasOffhand`, `EquippableSubtypes`; отказы `OFFHAND_SUBTYPE_UNKNOWN`, `OFFHAND_ANCHOR_NOT_MEASURED`, `OFFHAND_FILE_MISSING` |
| `Assets/CubeGame/AlphaModels/Runtime/AlphaHeldItemLayer.cs` | удерживаемый предмет: child `held_mesh` у `part_hand_r`, якорь `center` (слот 0x1e8), выбор модели по `(type, subtype)`; отказы `HELD_ITEM_NO_MODEL`, `HELD_ITEM_FILE_MISSING`, `HELD_MESH` |
| `tools/alpha_content/build_held_item_models.py` + `data/held_item_models.json` + `Content/Generated/AlphaHeldItemModels.g.cs` | дистилляция карты «предмет → модель» из исполненного `item_icon_id` (0x4EC400): 14 предметов (11 расходников, планер, лодка, лампа) |
| `tools/alpha_content/build_equipment_models.py` + `data/equipment_models.json` + `Content/Generated/AlphaEquipmentModels.g.cs` | карта брони: 4 слота × 8 подтипов (iron 1–4, iron/silk/linen/wool 5) = 32 модели |
| `Assets/CubeGame/AlphaModels/Runtime/AlphaEquipmentLayer.cs` | SWAP-слой: грудь → `body`, перчатки → `hand_l/hand_r`, сапоги → `foot_l/foot_r`; исходный рендерер скрывается, `Unequip` восстанавливает; плечо (`shoulder_item`) в гуманоидном риге отсутствует → отказ `EQUIPMENT_SHOULDER_BLOCK_NOT_IN_HUMAN_RIG` |
| `Assets/CubeGame/AlphaModels/Runtime/AlphaRig.cs` | аддитивно: `weaponAnchorInHand/OnBack`, `weaponScale`; `AnchorOf` использует измеренный закон только когда он установлен |
| `tools/alpha_content/extract_item_models.py` | выгрузка предметных `.cubdec` из `data1.db` (только чтение) в локальный кэш |
| `Assets/CubeGame/Tests/AlphaModels/EditMode/AlphaModelsEditModeTests.cs` | `AlphaItemAnchorTests`: 26/10 якорей, sword `(sx/2, sy/2, 4)`, bow `sy−1.5`, shield `y=3`, bracelet scale 0.6, отказ неизмеренного, `EquippableInHand ≥ 20` |
| `Assets/CubeGame/Tests/AlphaModels/PlayMode/AlphaItemVisualPlayModeTests.cs` | реальный человек-риг: замена greatsword → iron-sword (subtype 0) и bow (subtype 6), проверка локальных смещений меша в руке и за спиной, отказы для subtype 99 и отсутствующего файла |

---

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| M0-04B (законы слотов) | `held_item`/`special_item` = `center`; `lamp_item` = `lamp`, `pet_item` = `pet` — лампа/пет **отказываются** (`HELD_ITEM_LAW_NOT_IMPLEMENTED`), а не рисуются произвольно; EditMode `LawOf`, PlayMode: лампа → отказ, зелье/планер → успех |
| Preview | Кадры `item_actor_only` / `item_chest` / `item_potion`: крупный план груди с бронёй и зелья в правой руке; утечка preview-акторов между кадрами устранена |
| Слоты предметов | `AlphaItemSlotRules`: 11 типов → слоты по `item_layer` (held/weapon/chest/hands/feet/shoulder/neck/ring/pet/special/lamp); кольца «хранятся, но не рисуются», материалы/еда/монеты не экипируются |
| Расходники | `AlphaConsumableRules`: действия использования 80/81, кулдаун 3000 мс для life potion и 10000 мс для остальных type-1, 0 для прочих; **эффект/лечение не восстановлены** (B-U1 открыт) |
| EditMode (весь проект) | `total=354 passed=354 failed=0` (`AlphaOutput/editmode.xml`) |
| PlayMode (весь проект) | `total=42 passed=42 failed=0` (`AlphaOutput/playmode.xml`), +4: левая рука (щит/кинжал), удерживаемый предмет (зелье/лампа/планер), броня (грудь/перчатки/сапоги + отказ плеча), полный дескриптор игрока (броня+зелье) |
| Выгрузка предметных моделей | `EXTRACT_ITEM_MODELS=PASS written=28` (+14 удерживаемых +4 брони) |
| Карта «предмет → модель» | `HELD_ITEM_MODELS=PASS items=14`, `EQUIPMENT_MODELS=PASS items=32`; исходный свип `ITEM_MODELS DONE types=24 models=1565 errors=0` |
| Кэш/таблицы | `ALPHA_CONTENT_RESULT=PASS`, `ALPHA_TABLES_RESULT=PASS` |

---

## 4. ALPHA_REFERENCE + DELTA

| Элемент | Доказательство | Реализация |
|---|---|---|
| Оружие в руке (15/16/17 и одноручное) | `drawModel` 6651–6662, `CODE_DERIVED_MEASURED` | anchor = `(sx/2, sy/2, 4)` |
| Лук | 6806–6811, `fVar23 = 1.5 − sy` | anchor `y = sy − 1.5` |
| Арбалет / щит / браслет / бумеранг / посох / кулак | соответствующие ветки `drawModel`, `CODE_DERIVED_MEASURED` | таблица якорей по подтипам |
| За спиной | центральный блок, `center` | `AnchorOf` при `weaponOnBack` |
| Стрела 9 | только константы кода (`CODE_DERIVED`) | помечена `PARTIAL`, в `EquippableInHand` не входит |
| Модель предмета | `item_icon_id` (D64/D65) / `item_layer` | `.cubdec` из той же БД, мешер `VoxelMesh` |

---

## 5. PARTIAL / UNKNOWN

- **Стрела (subtype 9)** — `PARTIAL`: константы только из кода, закон не измерен.
- **Масштаб оружия** из клипов (`0.95` в job) и закон `scale` (браслет 0.6) пока не применяются к позе: масштаб задаётся клипом; `rig.weaponScale` публикуется для будущего слоя.
- **Левая рука** реализована как child-меш у `part_hand_l` с измеренным якорем (`AlphaOffhandLayer`, PlayMode-проверка щит/кинжал).
- **Удерживаемый предмет**: 11 расходников, планер, лодка и лампа имеют модель и визуальный слой (`AlphaHeldItemLayer`, якорь `center`); что предмет *делает* (лечение, кулдаун, списание) — по-прежнему в игровом слое (`B-U1` открыт).
- **Броня**: грудь/перчатки/сапоги подменяют блоки `body`/`hand_l+hand_r`/`foot_l+foot_r` по закону `center` (`AlphaEquipmentLayer`), 32 модели, `Unequip` восстанавливает исходный вид; плечо (`shoulder_item`) не отображается — в подготовленном гуманоидном риге нет такой части, это честный отказ, а не подмена.
- **Статы/редкости/прочность** — не здесь: визуальный слой не считает и не хранит статы (S04/Core).
- **Иконки UI** — M0-08, не в этой партии.

---

## 6. Handoff

API: `AlphaWeaponLayer.TryEquipFromFile(rig, path, subtype, out reason)` + `AlphaItemRules.TryWeaponAnchors/TryOffhandAnchors`. Визуальный слой не решает, можно ли экипировать предмет; игра вызывает его после своего решения. Подробности — [D86_DEEPSEEK_HANDOFF_M0_04](D86_DEEPSEEK_HANDOFF_M0_04.md).

Следующая партия DeepSeek — M0-02/M0-07 (биомы/растительность/постройки/города/данжи) и M0-08 (VFX/аудио/иконки), затем расширение предметного слоя (левая рука, расходники 80/81 в руке).
