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
- Если подтип неизвестен или закон не измерен, модель **не** подменяется: возвращается причина. Это осознанный отказ, не ошибка.
- `.cubdec` предметов лежат в локальном кэше `<content>/items/` (не в Git): `python -m tools.alpha_content.extract_item_models`.

## 3. Что нужно от игры

1. Путь к `.cubdec` предмета: либо из локального кэша, либо из вашего item-визуального реестра; `subtype` — Alpha-подтип оружия (0…25), не Core-enum.
2. Вызов `TryEquipFromFile` при смене экипировки (main hand), после собственной проверки правил.
3. Не менять `Assets/CubeGame/AlphaModels/*`, `Content/*` — это наши модули; расширение законов (левая рука, расходники) — через handoff.

## 4. Ограничения

- Подмена модели левой руки (щит/факел/браслет) ещё не сделана — таблица якорей готова, вызов появится в следующей партии.
- Стрела 9 — `PARTIAL` (только константы кода).
- `rig.weaponScale` публикуется, но поза масштаба остаётся за клипами.
- Иконки инвентаря — M0-08.

## 5. Проверки

`AlphaOutput/editmode.xml` — 335/335 (+2 теста якорей); `AlphaOutput/playmode.xml` — 26/26 (+1 PlayMode с реальной заменой greatsword → sword/bow на человеке). Выгрузка моделей: `EXTRACT_ITEM_MODELS=PASS written=28`.
