# D6 шаг 3 — иконки предметов из предметного слоя Alpha

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content` @ `3b63659`.
**STATUS: `READY_FOR_REVIEW`.**

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Иконки предметов в интерфейсе | есть только атлас (`AlphaIconAtlas`, ключи = имена моделей), но не было отображения «предмет → иконка» | `AlphaItemIcons`: запись атласа = имя модели Alpha из предметного слоя (`AlphaHeldItemDef.Model`, селектор `item_icon_id 0x004EC400`), напр. LifePotion → `life-potion`; оружие/броня **отказывают с причиной**, потому что соответствие «материал → иконка» не восстановлено |
| Честность | — | `ALPHA_DATA: model/icon names of held items …; weapon/armour icon by material UNKNOWN` |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Presentation/Visual/AlphaItemIcons.cs` | `EntryOf(model)`, `TryEntry(item)`, `TryGetSprite(atlas, item, out reason)` |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaItemIconsTests.cs` | 3 теста: запись из модели, отказ для оружия/без атласа, честный статус |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `137/137 passed`, 0 failed (`AlphaOutput/d6c_editmode2.xml`) |
| Полные EditMode/PlayMode | EditMode `555` (549 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

Ошибка компиляции по ходу: поле в таблице называется `ModelIndex`, а не `IconId` — исправлено в причине
(и заодно убран придуманный ключ атласа: ключ — имя модели, как в самом атласе).

## 4. PARTIAL / UNKNOWN

1. **Иконки оружия и брони** — по материалу/редкости (`item_icon_id` per material) не восстановлены:
   интерфейс получит явный отказ, а не «похожую» иконку.
2. **Ключи атласа**: используются имена моделей; если Codex увидит другую схему ключей в сгенерированном
   атласе, менять нужно одну функцию `EntryOf` — контракт данных не затронут.

## 5. Что нужно от Codex (C10)

1. Взять `AlphaItemIcons.TryGetSprite(atlas, rawItem, out reason)` для слотов инвентаря; при отказе
   показывать текстовую заглушку с причиной — это честнее выдуманной иконки.
2. Если у оружия появится доказанное соответствие материала иконке — добавить ветку в `TryEntry`.
