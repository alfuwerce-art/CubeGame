# D86 — handoff DeepSeek → Claude: предметный визуальный слой (M0-04B)

**Дата:** 2026-11-09. **Ветка:** `deepseek/d85-visual-unity`. **Отчёт:** [M0_04B_DEEPSEEK_ITEM_VISUAL_REPORT](M0_04B_DEEPSEEK_ITEM_VISUAL_REPORT.md). **Статус:** `READY_FOR_REVIEW`.

## 1. Что теперь можно вызвать

```csharp
// После решения игры «этот предмет экипирован»:
if (!AlphaWeaponLayer.TryEquipFromFile(view.Rig, cubdecPath, weaponSubtype, out var reason))
    Debug.Log(reason);   // WEAPON_SUBTYPE_UNKNOWN / ANCHOR_NOT_MEASURED / WEAPON_FILE_MISSING / WEAPON_MESH

// Законы крепления (чистые данные, без UnityEngine):
AlphaItemRules.TryWeaponAnchors(subtype, out var set);   // 26 подтипов
AlphaItemRules.TryOffhandAnchors(subtype, out var set);  // 10 классов левой руки
AlphaItemRules.IsAnchorMeasured(set.InHand);             // стрела 9 = PARTIAL, остальные измерены
```

Визуальный слой **не** проверяет право экипировать предмет, не считает статы и не трогает инвентарь: он только рисует модель и ставит её по измеренному закону. Какие клипы показывают оружие — решают клипы (`weaponOnBack`) и игровое состояние.

## 2. Как это устроено

- `AlphaWeaponLayer.TryEquip` заменяет меш части `weapon` (мешер `VoxelMesh`, маркерное правило D61), записывает размеры, устанавливает `weaponAnchorInHand/OnBack` из таблицы и пересчитывает локальное смещение меша (`-GridToUnity(anchor)`); «в руке / за спиной» переключается `AlphaRig.SetWeaponOnBack`, как и раньше.
- `AlphaOffhandLayer.TryEquipFromFile` вешает модель на `part_hand_l` с измеренным якорем левой руки.
- `AlphaHeldItemLayer.TryEquipFromItem(rig, itemsDir, type, subtype)` вешает удерживаемый предмет (расходники 1/0…1/10, планер 23/0, лодка 23/1, лампа 24) на `part_hand_r` с якорем `center`; модель берётся из таблицы `AlphaHeldItemModels` (исполненный `item_icon_id` 0x4EC400), файл — `.cubdec` из локального кэша.
- `AlphaEquipmentLayer.TryEquipFromItem(rig, itemsDir, type, subtype)` подменяет блоки брони: грудь → `body`, перчатки → `hand_l/hand_r`, сапоги → `foot_l/foot_r` (SWAP + `center`), исходный рендерер скрывается; `Unequip`/`UnequipSlot` восстанавливают. Плечо (тип 7) в подготовленном гуманоидном риге не отображается — возвращается `EQUIPMENT_SHOULDER_BLOCK_NOT_IN_HUMAN_RIG`.
- Если подтип неизвестен, якорь не измерен или файла нет, модель **не** подменяется: возвращается причина. Это осознанный отказ, не ошибка.
- `AlphaItemSlotRules.TrySlot(type, out slot)` / `LawOf(slot)` / `IsDrawn(slot)` — карта «тип предмета → слот» по `item_layer` (held/weapon/chest/hands/feet/shoulder/neck/ring/pet/special/lamp); кольца «хранятся, но не рисуются», материалы/еда/монеты — `None`. Используйте для инвентаря/экипировки вместо догадок.
- `.cubdec` предметов лежат в локальном кэше `<content>/items/` (не в Git): `python -m tools.alpha_content.extract_item_models`.

## 3. Что нужно от игры

1. Путь к `.cubdec` предмета: либо из локального кэша, либо из вашего item-визуального реестра; `subtype` — Alpha-подтип (оружие 0…25, левая рука — класс 3/4/6/7/12/13…), не Core-enum.
2. Вызов `TryEquipFromFile` (оружие), `AlphaOffhandLayer.TryEquipFromFile` (левая рука) и `AlphaHeldItemLayer.TryEquipFromItem` (расходник/лампа/планер/лодка) после собственной проверки правил инвентаря.
3. Не менять `Assets/CubeGame/AlphaModels/*`, `Content/*` — это наши модули; расширение законов — через handoff.

## 4. Ограничения

- Левая рука и удерживаемые предметы реализованы (PlayMode-проверки щит/кинжал, зелье/лампа/планер); броня грудь/перчатки/сапоги — SWAP, плечо не отображается (нет `shoulder_item` в риге); «передний» слот и квестовые предметы в руке пока не визуализируются.
- Стрела 9 — `PARTIAL` (только константы кода).
- `rig.weaponScale` публикуется, но поза масштаба остаётся за клипами; `AlphaHeldItemLayer` не масштабирует модель (закон масштаба для расходников не найден).
- Иконки инвентаря — M0-08 (рантайм и пакетная выгрузка 28 моделей).

## 5. Проверки

`AlphaOutput/editmode.xml` — 349/349; `AlphaOutput/playmode.xml` — 41/41 (замена оружия, левая рука, удерживаемый предмет, броня с Unequip, отказы без подмены). Выгрузка моделей: `EXTRACT_ITEM_MODELS=PASS written=28` + 14 удерживаемых + 4 брони; карты `HELD_ITEM_MODELS=PASS items=14`, `EQUIPMENT_MODELS=PASS items=32`.
