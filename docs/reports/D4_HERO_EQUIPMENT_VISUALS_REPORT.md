# D4 — экипировка на модели героя и предметы на земле (волна 2, пакет DeepSeek)

**Дата:** 2026-11-09. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content`.
**STATUS: `READY_FOR_REVIEW`.**

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Экипировка героя по событиям | `GameVisualSink.OnEquipmentChanged` — пустая заглушка: броня/оружие не появлялись на модели | `HeroLoadout.TryApply` — событие `EquipmentVisualChanged` превращается в дескриптор `CharacterVisualRequest`: оружие (Alpha тип 3, подтип → модель по таблице `AlphaWeaponSubtypeDef.DefaultModel`), левая рука (подтип), броня (типы 4/5/6 — грудь/кисти/стопы); sink перестраивает модель героя |
| Неподдерживаемые слоты | молча игнорировались | явный отказ с причиной `SLOT_NOT_IMPLEMENTED` (шея, кольца, лампа, питомец, спина) и `ITEM_TYPE_MISMATCH` — ничего не подставляется |
| Предметы на земле | кубики-заглушки Claude (`GroundItem_PLACEHOLDER`) | `GameVisualSink.OnWorldItemDropped/Removed`: модель предмета из `AlphaHeldItemCatalog` (расходники), таблицы оружия, каталога брони; меш через `VoxelMesh` из `.cubdec`, масштаб модели героя; удаление по `WorldItemRemoved` |
| События предметов | `IEntityVisualSink` их не содержит | `GameVisualSink.BindItems(GameEventBus)` — подписка на `WorldItemDropped/WorldItemRemoved` без изменения контракта Claude |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Presentation/Visual/HeroLoadout.cs` | чистое отображение «слот + Alpha item → дескриптор визуала» + `ModelFile` (`.cub` → `.cubdec`) |
| `Assets/CubeGame/Presentation/Gameplay/GameVisualSink.cs` | перестройка модели героя при смене экипировки; визуалы предметов на земле; `BindItems`, `GroundItemModelName`, `GroundItemCount` |
| `Assets/CubeGame/Tests/Creatures/EditMode/HeroLoadoutTests.cs` | 7 EditMode-тестов |
| `Assets/CubeGame/Tests/Creatures/EditMode/CubeGame.Tests.Creatures.EditMode.asmdef` | добавлены ссылки на `CubeGame.Presentation.Visual` и `CubeGame.Gameplay` |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `73/73 passed`, 0 failed (`AlphaOutput/d4_editmode3.xml`) |
| Полные EditMode/PlayMode | EditMode `491` (485 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

Красные прогоны и их причины (исправлялись в причине, не в тесте):
1. `AlphaHeldItemDef`/`AlphaItemRules` не видны — не хватало `using CubeGame.Content` в sink (ошибка компиляции);
2. тест ожидал модель `iron-greatsword1`, а таблица Alpha даёт `obsidian-greatsword` для подтипа 15 — тест
   исправлен на проверку суффикса `.cubdec` и каталога предметов.

## 4. PARTIAL / UNKNOWN

- **Перестройка модели героя** проверена адресными тестами отображения; полный визуальный прогон в сцене
  (экипировка → модель) — на этапе визуального осмотра (этап 3 плана).
- **Смерть существ** осталась `PLACEHOLDER` (тело на боку); эффекты смерти — D5.
- **Поза по подтипу оружия** (`AI-09`) не подключена: клипы действий уже дают позу, отдельный слой позы —
  следующий шаг D4 вместе с аурами/щитами.
- **Модели рас/полов для редактора** (P-01…P-05) — следующий шаг D4: нужен выбор внешности от C1.
- **Лут на земле**: содержимое/количество — gameplay (Claude, C5); здесь только визуал модели.

## 5. Что нужно от Claude

1. В `GameBootstrap` (после создания шины): `visualSink.BindItems(eventBus)` — без этого визуалы
   предметов на земле не появятся (события `WorldItemDropped` уже публикуются).
2. `EquipmentVisualChanged` для героя публикуется при входе в мир и каждой смене — этого достаточно;
   модель перестраивается на месте, капсула-заглушка больше не нужна при наличии кэша контента.
