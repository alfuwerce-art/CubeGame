# G1 — RUNTIME REUSE MATRIX (F1–F3 vs P0–P7)

**Дата:** 2026-11-17
**Этап:** [G1-00 — Runtime Baseline Selection](../reports/G1_00_RUNTIME_BASELINE_SELECTION_REPORT.md)
**Сравниваемые деревья:**

- **A (canonical F1–F3):** `integration/m0-all` @ `4e36f15` — 274 `.cs` / 55 267 строк.
- **B (P0–P7):** цепь `0533698..85ba64a` — 104 линейных коммита, 278 файлов, +29 197 / −2 024; 328 `.cs` / 72 400 строк. Включает F4 presentation, runtime recovery, vegetation и P0–P7.
- **C (candidate):** ветка `deepseek/g1-00-runtime-baseline-candidate` = A + pivot-документы + B. Код `Assets/` и `tools/` **идентичен B** (проверено `git diff`, пусто); отличается только документация.

**Правило оценки:** больше строк ≠ лучше. Оцениваются: функциональность, стабильность, связанность (coupling), поддерживаемость, тестовое покрытие, полезность для CubeGame.

---

## 1. Сравнение по системам

| Система | A: Canonical F1–F3 | B: P0–P7 | Recommendation | Почему |
|---|---|---|---|---|
| **Bootstrap** | `GameBootstrap` 1 784 + `GameplayLoop` 337 (один god-class) | 9 partial-файлов: `GameBootstrap` 1 784 + `Interface` 824 + `AlphaMenus` 871 + `AlphaHud` 350 + `Options` 299 + `Diagnostic` 232 + `Preview` 326 + `MenusAndMap` 161 + `AlphaFont` 47 = **5 184** | **B → `KEEP_BUT_REWORK`** | Функционально больше (меню, опции, HUD, диагностика), но класс вырос вдвое; разбиение по UI-слою уже сделано, разбиение по *ответственности* — нет. G1 W1 работает на B. |
| **Player / движение** | `PlayerMotor` 186 + `AlphaMovement` 503 | те же + отделение прыжка от UI-ввода (P0), preview-камера | **B → `KEEP`** | Поведение то же, добавлено корректное разделение ввода/UI. |
| **Input** | 19 прямых `Input.*`, захардкоженные клавиши | P0: контексты стартового экрана и `jump` отдельно от UI; клавиши по-прежнему в коде | **B → `KEEP_BUT_REWORK`** | Стало лучше (нет протекания ввода в UI), но abstraction-слоя всё ещё нет — это G1 W2. |
| **World generation** | `/6` (`WorldRuntimeId`), без легаси-id | `/9` + `PreviousWorldRuntimeId /6`, `EnvironmentRuntimeId /7`, `PopulationRuntimeId /8` | **B → `KEEP`** | Альфа-данжи и патрули требуют `/9`. Важно: смена id = старые миры не читаются (версионная стоимость). |
| **Streaming** | `StreamingWorldView` 414, view 6 / keep 8, `BuildsPerFrame 4`, **нет render distance/бюджета/LOD/тумана** | P1: render distance → vertex budget, туман по ближайшему отсутствующему чанку, горизонт | **B → `KEEP`** | В A этой системы физически нет; B даёт измеримый горизонт и бюджет. |
| **Environment** | растительности нет, `VegetationPerZone = 0` | P2/P7: деревья (вид 5 — рост), наземный декор, surface decorations, меши растительности, Alpha-селектор | **B → `KEEP_BUT_REWORK`** | Есть реальная геометрия и правила размещения. REWORK — потому что правила/плотность Alpha-производные, а не CubeGame-баланс. |
| **NPC / population** | каналы зон, `CreatureAi`, pathfinding **нет**, расписания пустые | то же + патрули ориентиров, свита вожака, хозяева миссий, `AlphaMissionBossPopulation` | **B → `KEEP_BUT_REWORK`** | Больше контента поведения, но по-прежнему нет pathfinding и фракционной матрицы. |
| **Combat** | `CombatSystem` (фазы/hit/damage/death), `ProjectileSystem` | то же + `AlphaClassAbilities`, расширенное покрытие таймингов диспетчера | **B → `KEEP_BUT_REWORK`** | Способности появились. REWORK: action-id-space Alpha и формулы надо сделать своими (G4). |
| **Items** | инвентарь/экипировка, крафт только Life Potion | + `AlphaLoot`, `AlphaShop`, `AlphaCrafting`, `AlphaWallet`, `AlphaItemRecord` | **B → `KEEP_BUT_REWORK`** | Полный цикл kill→loot→equip→sell→craft существует. REWORK — баланс/таблицы под CubeGame (G6). |
| **Loot** | `LootRules` — placeholder, `System.Random`, деньги не падают | loot по Alpha `0x59B180`: монеты, классовый предмет 1/10, формула 1/50, материалы, экипировка 1/4 | **B → `KEEP_BUT_REWORK`** | Механика доказана; числа — Alpha (пере-тюнинг в G6). |
| **Progression** | stats/XP/level, ранги навыков без эффектов | + профиль-стор, опции, эффекты способностей | **B → `KEEP`** | Навыки стали применяться в бою. |
| **UI** | IMGUI напрямую из bootstrap; Alpha-parity отсутствует | UI Toolkit: HUD, меню, инвентарь, карта, журнал, options, диагностика; USS-тема; Alpha-шрифт с fallback | **B → `KEEP_BUT_REWORK`** | Это единственный существующий UI-слой. REWORK: Alpha-параллельная компоновка не нужна, а шрифт — закрытый ассет (G12). |
| **Quests / content** | журнал охот, один тип миссии | генератор миссий (регион, веса зон), миссии типов 1–5, данж с хозяином, зачёт по убийству хозяина | **B → `KEEP_BUT_REWORK`** | Появился каркас контента. REWORK: 5 типов = Alpha-набор (G11). |
| **Persistence** | 3 файла, checksums, атомарная запись, **нет версионирования** | + `AlphaProfileStore` (списки персонажей/миров, миграция слотов), те же форматы | **B → `KEEP`** (версионирование — G1 W5) | В B добавлен слой профиля, которого в A нет. Миграция *формата* по-прежнему отсутствует в обоих. |
| **Tests** | 756 EditMode / 70 PlayMode | **859 EditMode / 75 PlayMode** (+103/+5), включая P7-данжи, миссии, лут, магазин, крафт, способности, упаковку контента, стартовый flow, F4 presentation | **B** | Больше покрытия по всем новым системам. |
| **Player build** | `PilotBuild` → `.unity-install/build/Pilot/…` | + `ReleaseBuild` (release-сборка) + `AlphaContentStaging` (стадирование кэша в `StreamingAssets` + build gate + bypass-флаг) | **B → `KEEP_BUT_REWORK`** | В B есть реальный конвейер сборки. REWORK: гейт требует Alpha-кэш → для CubeGame должен требовать *свой* контент. |
| **Alpha-шрифт** | нет | `Resources/AlphaFont.ttf` (не коммитится), `Resources.Load`, `ALPHA_FONT=ABSENT` при отсутствии | **B → `KEEP_BUT_REWORK`** | Механика graceful fallback хорошая; сам шрифт — `PRODUCT_BLOCKER` (G12). |

**Итог сравнения:** B строго богаче A по всем системам, кроме «чистоты»: B больше (72 400 против 55 267 строк), связаннее и содержит Alpha-параллельные слои. Ни одной системы, которая в A есть, а в B отсутствует, не найдено.

---

## 2. Классификация блоков P0–P7

| Блок | Вердикт | Комментарий |
|---|---|---|
| F4 presentation (UI Toolkit, audio) | `KEEP_BUT_REWORK` | Работает; Alpha-параллельная компоновка и Alpha-WAV под замену (G12) |
| Runtime recovery (свет, тени, небо, материалы) | `KEEP_BUT_REWORK` | Дало рабочую визуальную базу; финальный рендер — G9/S06 |
| Vegetation (P2/P7 деревья, декор) | `KEEP_BUT_REWORK` | Геометрия и размещение есть; плотность/биомы — G2/G9 |
| P0 startup/menu/profile/options | `KEEP` | Реальный стартовый flow, профиль-стор, опции; нужен только input-слой (G1 W2) |
| P1 render distance/vertex budget/fog | `KEEP` | Измеримый горизонт; бюджеты уточняются в G2 |
| P3 population/AI | `KEEP_BUT_REWORK` | Патрули и свиты есть; pathfinding/расписания/фракции — G5 |
| P4 combat/abilities | `KEEP_BUT_REWORK` | Ядро сохраняется; action-id-space и формулы — G4 |
| P5 items/loot/shop/crafting/wallet | `KEEP_BUT_REWORK` | Полный цикл есть; таблицы/цены — G6 |
| P6 UI Toolkit HUD/menus/map/journal | `KEEP_BUT_REWORK` | Единственный UI-слой; pixel-parity не нужен (G12) |
| P7 missions/dungeons/patrols | `KEEP_BUT_REWORK` | Каркас контента; 5 Alpha-типов миссий — G11 |
| `AlphaContentStaging` + build gate | `REMOVE_FROM_PRODUCT` | Стадирует Alpha-производный контент в Player; для продукта нужен собственный контент-конвейер |
| `AlphaContentInventory` (release completeness) | `RESEARCH_ONLY` | Проверка полноты локального кэша; полезна только для dev |
| `ContentPackagingTests` | `RESEARCH_ONLY` | Проверяет «кэш этой машины полон» — не продуктовый тест |
| `Editor/AlphaModelsImport` | `RESEARCH_ONLY` | Dev-инструмент; останется локальным |
| `Content/Generated/*.g.cs` (14 таблиц, ~7 060 строк) | `RESEARCH_ONLY` → заменить | Alpha-числа; решение по замене — G2/G3/G6 |
| Alpha-шрифт `Resources/AlphaFont.ttf` | `REMOVE_FROM_PRODUCT` | Закрытый ассет; fallback уже есть |
| WAV-каталог Alpha (93 файла) | `REMOVE_FROM_PRODUCT` | Закрытые ассеты; собственный аудио-контент — G12 |
| Alpha icon atlas | `REMOVE_FROM_PRODUCT` | Производный от Alpha GUI; замена — G12 |
| `.cubdec` модели/риги/клипы | `REMOVE_FROM_PRODUCT` | Закрытые ассеты; замена — G3/G10 |
| F4/recovery демо и showcase-классы (`CombatDemo`, `AlphaContentShowcase`, `PilotBootstrap`) | `REMOVE_FROM_PRODUCT` | Не в игровом пути; чистятся по решению, не в G1-00 |

---

## 3. Alpha-зависимости: где, зачем, заменяемость

| Зависимость | Где используется | Зачем | Заменимо? | Сложность замены | Класс |
|---|---|---|---|---|---|
| Локальный кэш `.scratch/content` (156 actors, 747 моделей, 927 клипов, 1 554 предмета, 180 appearance, 93 звука, ~232 МБ) | `AlphaContentSource` → `AlphaActorLibrary.Shared` → `GameVisualSink`, `CreatureSpawner`, `ZoneContentPlanner` | визуал существ/NPC/предметов, иконки | да, собственным контентом | высокая (нужен G3/G10 pipeline) | `TEMPORARY_RUNTIME` + `PRODUCT_BLOCKER` |
| `<project>/AlphaOutput/icons/atlas.png|json` | `AlphaIconAtlas` → UI-иконки | иконки предметов/карты | да | средняя | `TEMPORARY_RUNTIME` + `PRODUCT_BLOCKER` |
| `<root>/sounds/*.wav` (93) | `AlphaSoundLibrary` → `GameAudio` | звук событий | да | средняя | `TEMPORARY_RUNTIME` + `PRODUCT_BLOCKER` |
| `Resources/AlphaFont.ttf` (не коммитится, стадируется скриптом) | `GameBootstrap.AlphaFont` | Alpha-шрифт UI | да (свой шрифт) | низкая | `TEMPORARY_RUNTIME` + `PRODUCT_BLOCKER` |
| `.cubdec` модели предметов/appearance | `GameVisualSink`, `HeroLoadout`, слои экипировки | модели на герое и в мире | да | высокая | `PRODUCT_BLOCKER` |
| 14 таблиц `Content/Generated/*.g.cs` | `AlphaContentDatabase`, вид/класс существ, предметы, звуки, тайминги | идентификация и правила | да, своими таблицами | средняя | `PRODUCT_BLOCKER` (по данным), `RESEARCH_ONLY` (как evidence) |
| `.scratch/m0` зонные дампы | `FullM0ZoneOracleTests` | oracle-сверка | не нужна в продукте | — | `RESEARCH_ONLY` |
| `tools/alpha_content/*` (SIF-парсинг `data1/2.db`, `Cube.exe` SHA) | build-time генерация таблиц | извлечение данных | не нужна в продукте | — | `RESEARCH_ONLY` |
| `D:\Games\Cube world` | только локальные скрипты (`stage_alpha_ui_font.ps1`, extractors) | источник закрытых ассетов | нет (read-only) | — | `RESEARCH_ONLY` |
| Абсолютные локальные пути (`D:\`, `C:\`) в runtime-коде | — | — | — | — | **не найдено** (0 совпадений в `Assets/**/*.cs`) |
| `AlphaContentStaging` build gate | Editor hook сборки | гарантирует «полный» Player | заменяется своим конвейером | средняя | `PRODUCT_BLOCKER` (механизм) |

**Проверено:** ни один runtime-класс не читает `Cube.exe`, `data1/2.db`, `resource1.dat` и не содержит абсолютных путей. Вся Alpha-зависимость сводится к четырём видам локального кэша + сгенерированным таблицам.

---

## 4. Что это значит для выбора baseline

1. В B нет ни одной системы, которая хуже A по функциональности; A — это подмножество B по коду.
2. Связанность B выше (bootstrap 5 184 строки, Alpha-параллельные слои UI/audio/шрифт), но это ровно тот технический долг, который уже расписан в [G1 ТЗ](../stages/G1_TECHNICAL_FOUNDATION_STAGE.md) (W1–W12) и в G0 reuse audit.
3. Alpha-зависимости B локализованы в 4 точках кэша и таблицах; ни одна не является «жёсткой» (есть fallback или точка замены).
4. Поэтому инженерная рекомендация — **B как основа, плюс ограниченный cleanup** (см. отчёт G1-00 §итог).
