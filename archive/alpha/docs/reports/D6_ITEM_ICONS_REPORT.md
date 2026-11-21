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

## 6. Codex (D89) — иконки в инвентаре C10

При первом открытии инвентаря `GameBootstrap` загружает локальный атлас. Строки
сумки и дополнительных слотов рисуют область атласа рядом с названием, если
`AlphaItemIcons.TryEntry` нашёл подтверждённую модель и в атласе есть её ключ.
Сами строки, операции с предметами и сохранения используют прежние данные.

Локальный импорт из `.scratch/content/items` создал 42 иконки в `AlphaOutput/icons`,
512×384. Метаданные и ненулевой альфа-канал проверены для `life-potion`, `mana-potion`,
`lantern02`, `glider`, `boat2`. Каталог исключён из Git. Сценовая проверка P22 отложена.

Первая сборка записала атлас, после чего Unity аварийно завершился в обёртке
`RunBuildIconAtlas`. Повтор с `-executeMethod CubeGame.AlphaModels.Import.AlphaModelsImport.BuildIconAtlas -quit`
завершился с кодом `0` и дал те же 42 иконки. Полный Unity EditMode: 625 прошли,
2 пропущены, ошибок нет.
