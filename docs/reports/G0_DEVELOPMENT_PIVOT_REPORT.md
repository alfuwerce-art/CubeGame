# G0 — DEVELOPMENT PIVOT / BASELINE FREEZE — REPORT

**Дата:** 2026-11-17
**Этап:** G0
**Ветка:** `deepseek/g0-development-pivot`
**База:** `origin/integration/m0-all` @ `4e36f1539d787e6a03f85d1f9fc92efcb1b64a79`
**Исполнитель:** DeepSeek Harness

---

## STATUS

`PASS`

Основание: все критерии PASS G0 выполнены и подтверждены проверками в этом отчёте. Единственные открытые пункты — решения, которые по правилам проекта принимает продюсер (базовая линия G1, формальный `CHANGE_REQUEST` к D01/D14/D20, правовой режим Alpha-производных данных); они вынесены в §7 RISKS и не блокируют pivot.

---

## 1. BASELINE

| Параметр | Значение |
|---|---|
| Branch | `deepseek/g0-development-pivot` (рабочая), база `integration/m0-all` |
| Commit (база) | `4e36f1539d787e6a03f85d1f9fc92efcb1b64a79` — «docs: pivot roadmap from Alpha port to CubeGame production» |
| Unity | `6000.3.27f2` (`D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe`), URP не используется (Built-in RP) |
| Компиляция | `error CS = 0`, 38 warnings (batchmode import, чистый checkout) |
| EditMode (с локальным Alpha-кэшем) | **756 total / 750 passed / 0 failed / 6 skipped** |
| EditMode (без Alpha-кэша) | 756 total / 744 passed / **1 failed** / 11 skipped |
| PlayMode (с Alpha-кэшем) | **70 total / 69 passed / 0 failed / 1 skipped** (1 321 с) |
| Windows Player build | **SUCCEEDED**, `errors=0 warnings=19`, 91.7 MB, `CubeGamePilot.exe` (44.8 с) |
| Windows Player smoke run | **PASS** — запуск, вход в мир, население, сущность, чистый выход (`exit 0`) |
| Объём кода | 274 `.cs` / 55 267 строк в `Assets/` |
| Сцена | одна: `Assets/Scenes/Pilot.unity` |

### Разбор единственного EditMode-fail без кэша

`AlphaZoneContentProviderTests.SettingsFromLibrarySplitPassiveObjectsFromCreatures` — «staged passive objects feed the W-18 selection: Expected not empty, But was empty». Тест читает `AlphaActorLibrary.Shared`, то есть локальный Alpha-кэш. С `CUBEGAME_ALPHA_CONTENT=D:\Projects\CubeGame\.scratch\content` он проходит. Это **environmental**, не регрессия.

### Разбор пропусков

- 6 skip (всегда): `FullM0ZoneOracleTests.CompleteZoneMatchesLocalOracleDump(zone_canyon/lake/river/village/roadriver/carves)` — нужен локальный дамп `.scratch/m0`.
- +5 skip без кэша: `AlphaContentCacheTests` ×4, `CreatureSpawnTests.AllStagedKindsResolveAgainstTheCache`.
- 1 skip PlayMode: `AlphaVisualPlayModeTests.IconAtlasLoadsForUi` — нужен `<project>/AlphaOutput/icons/atlas.png`.

### Smoke-прогон Player (фактический лог)

```
[Game] boot -> MainMenu; saves=...\smoke\saves
[Game] world built seed=666666 gen=cubegame.alpha_terrain/6 chunks=25 origin=(8396928, 8396928)
       vertices=115160 139,0ms hero=(8396928.50, 153.20, 8396928.50) region=region(512,512) level=1 base=15
[Game] population: 0 content, 18 recovered in 9 zones
[Game] new world 'Auto World' seed=666666 in smoke
[Game] training target Wolf entity:3000000000000001 hp 100,0/100,0 ...
       problems [DATA_MISSING alpha content cache: ALPHA_CONTENT_NOT_CONFIGURED ...]
[Game] screenshot -> ...\smoke.png state=InGame tick=16
Failed to capture screen shot.
[Game] quit
```

Вывод: платформа **запускается и строит мир без Alpha-контента**, отсутствие кэша деградирует в placeholder с честной диагностикой. Скриншот в `-batchmode` не снимается (`Failed to capture screen shot`), это ограничение режима, а не игры.

### Важное уточнение о канонической ветке

Canonical содержит runtime уровня F1–F3. Работа P0–P7 находится в отдельной линейной цепи review-веток и **не влита**:

| Ветка | Коммит | Впереди canonical | Объём |
|---|---|---|---|
| `codex/m0-p1-world-streaming` | `0763c83` | 54 | 122 файла, +10 887 / −1 274 |
| `codex/m0-p3-population-ai` | `f265108` | 62 | 148 файлов, +12 493 / −1 347 |
| `codex/m0-p4-action-combat` | `f0d2b71` | 65 | 166 файлов, +13 331 / −1 784 |
| `codex/m0-p5-character-items-progression` | `01930ff` | 77 | 192 файла, +16 717 / −1 889 |
| `codex/m0-p6-alpha-menu-hud` | `fb4e0d5` | 83 | 203 файла, +19 324 / −1 889 |
| `codex/m0-p7-quests-cities-dungeons` | `85ba64a` | 104 | 278 файлов, +29 197 / −2 024 |

`merge-base canonical ↔ P7` = `0533698`; canonical не является предком P7 (merge будет не fast-forward).

**Проверка P7-ветки (выполнена в G0, worktree `.scratch/g0-p7` @ `85ba64a`):**

- 328 `.cs` / 72 400 строк (против 274 / 55 267 в canonical);
- EditMode: **859 total / 851 passed / 1 failed / 6 skipped**;
- единственный fail — `ContentPackagingTests.LocalCacheOfThisMachineIsReleaseComplete`: `ICON_ATLAS 0 < 1`, то есть отсутствует `<project>/AlphaOutput/icons`, а не дефект кода. Кэш на машине при этом полный: `actors=156 actorModels=747 clips=927 items=1554 appearance=180 sounds=93`;
- P7 PlayMode — см. §1.1.

### 1.1 P7 PlayMode (выполнено)

- PlayMode: **75 total / 74 passed / 0 failed / 1 skipped** (672 с).
- Skip: `AlphaVisualPlayModeTests.IconAtlasLoadsForUi` — отсутствует `<project>/AlphaOutput/icons/atlas.png`.

**Вывод:** цепь P0–P7 компилируется, и её тесты фактически зелёные — единственные два отличия от «всё зелёное» носят environmental характер (атлас иконок в EditMode-тесте упаковки и в этом PlayMode-тесте). Цепь является реальным кандидатом на базовую линию G1, но merge в canonical не fast-forward и требует отдельного решения (G1-00).

---

## 2. Что изменено в G0

Документы (все — в ветке `deepseek/g0-development-pivot`):

| Файл | Изменение |
|---|---|
| `docs/ACTIVE_STAGE.md` | **переписан**: активный этап = G0; CubeGame = собственная игра; старый M0 = LEGACY; порядок G0…G15; запреты; baseline; research loop |
| `docs/planning/README.md` | **переписан**: актуальный статус вместо «реализация ещё не начата»; новая карта документов; роль legacy M0; оговорка о методе D01/D14/D20 |
| `docs/planning/G0_EXISTING_WORK_REUSE_AUDIT.md` | **создан**: reuse map по 10 областям с вердиктами KEEP/REWORK/RESEARCH/RETIRE |
| `docs/planning/G2_WORLD_FOUNDATION_AUDIT.md` | **создан**: фактический аудит генерации мира |
| `docs/stages/G1_TECHNICAL_FOUNDATION_STAGE.md` | **создан**: исполнимый план G1 (W0–W11, тесты, DoD, blockers) |
| `docs/reports/G0_DEVELOPMENT_PIVOT_REPORT.md` | **создан**: этот отчёт |
| `docs/decisions/D93-development-pivot-own-game.md` | **создан**: запись решения о pivot + `PROPOSED CHANGE_REQUEST` к D01/D14/D20 |
| `AGENTS.md` | **переписан раздел текущей цели/метода**: агент больше не получает команду «полный перенос Alpha»; добавлены reading list, research loop, правило «что не является blocker» |
| `PROJECT_MEMORY.md` | новый раздел §0 «G0 DEVELOPMENT PIVOT»; D92/M0-цель/parity backlog помечены как исторические |
| `README.md` | устаревший статус эпохи D40 («Unity отсутствует», «S00-G0 не завершён») заменён актуальной картой документов |
| `docs/decisions/D92-m0-full-alpha-unity-port.md` | добавлен баннер `LEGACY — superseded …`, текст решения сохранён |
| `docs/stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md` | добавлен баннер `LEGACY` |
| `docs/stages/M0_F4_PRESENTATION_STAGE.md` | добавлен баннер `LEGACY` |
| `docs/planning/M0_ALPHA_PARITY_BACKLOG.md` | добавлен баннер `LEGACY`; реестр объявлен исследовательским, не blocking |
| `docs/agents/CLAUDE_ONBOARDING.md` | §12 (сводка S05A-эпохи) помечен `LEGACY` со ссылкой на ACTIVE_STAGE |

Исторические отчёты, исследования и решения **не переписывались**: у них изменены только статусные баннеры (где нужно), тексты сохранены.

GDD не изменялся: `docs/planning/DECISIONS_001-036.md` не тронут.

---

## 3. REUSE SUMMARY

Детально — [G0_EXISTING_WORK_REUSE_AUDIT.md](../planning/G0_EXISTING_WORK_REUSE_AUDIT.md).

| Категория | Кол-во | Что |
|---|---|---|
| `KEEP` | 22 | сеанс/состояния, стабильные ID и контракты, событийная шина, checkpoint-сейв, `WorldDeltaStore`, комбат-ядро (фазы/hit/damage/смерть), снаряды, инвентарь/экипировка, stats/XP, seed/terrain/zones/chunk/streaming/floating origin, placement foundation, oracle-фикстуры, presentation-контракты, `MapTileBuilder`, `CombatFeedback` |
| `REWORK` | 19 | `GameBootstrap` (god class), input abstraction, climate-семантика, biome rules, кэши мира, render distance/LOD, структуры/города/данжи, NPC AI и расписания, фракции, action-id routing, респавн героя, лут, торговля, навыки, миссии, версионирование сейва, NPC-state persistence, иконки, `AlphaSoundLibrary`, камера |
| `RESEARCH` | 11 | растительность/трава, pathfinding, способности/block/dodge, крафт, Alpha animation timing, M1-спецификация, `tools/alpha_models` pipeline, `AlphaModels/**`, ComfyUI, hero-editor правила, P6 UI Toolkit |
| `RETIRE` | 9 | IMGUI-UI как архитектура, второй AI-стек (`CreatureAgent`/`CreatureSpawner`/`WorldContentStreamer`), старая библиотека снарядов, `AlphaSpawnTable`/`AlphaPopulationRules`/`AlphaWorldPopulation`/`FallbackPopulationPolicy`, pilot-генератор (`PilotHeightField`/`ChunkMeshBuilder`/`PilotWorldView`), `Content/Generated/*.g.cs` как продуктовые данные, текущий lighting/shader-стек, audio-parity путь, dead demo/showcase |

Главный вывод: **инженерная работа переиспользуема в основном объёме**. Не переиспользуются Alpha-параллельные артефакты (UI parity, audio/VFX parity, второй AI-стек, pilot) и Alpha-числа как продуктовые данные.

---

## 4. OLD M0 — что произошло и как пользоваться дальше

- `M0 ALPHA FULL UNITY PORT` → `LEGACY / RESEARCH BASE / NOT A RELEASE TARGET`.
- P7/P8/P9/P10 **не продолжаются**.
- Ничего не удалено: код в ветках, отчёты в `docs/reports`, исследования в `docs/research/alpha2013`, фикстуры в тестах, decompilation evidence и таблицы на месте.
- [M0_ALPHA_PARITY_BACKLOG.md](../planning/M0_ALPHA_PARITY_BACKLOG.md) остаётся полезным **реестром знаний**: он показывает, что именно в Alpha было понято не до конца. Он больше не является blocker-списком.
- Как пользоваться evidence: когда CubeGame нужен конкретный механизм, сначала проверяется существующая реализация, затем — backlog/research; решение принимается в формате research loop (`ACTIVE_STAGE` §6).

---

## 5. G1 — первый implementation work package

[G1_TECHNICAL_FOUNDATION_STAGE.md](../stages/G1_TECHNICAL_FOUNDATION_STAGE.md)

- **G1-00 (блокирующая, до кода):** решение о базовой линии — canonical / цепь P0–P7 / гибрид. Рекомендация: гибрид или цепь P0–P7 при условии зелёного прогона; P7 уже проверен: EditMode 851/859 (fail — отсутствие `AlphaOutput/icons`, не код), PlayMode 74/75 (skip — тот же атлас).
- **W1:** композиционный корень и разбиение `GameBootstrap` (1 876 + 360 строк).
- **W2:** input abstraction (19 прямых `Input.*`).
- **W3:** границы симуляции/рендера (тик сейчас `PLACEHOLDER`).
- **W4:** данные и стабильные ID без Alpha-смещений в публичном API.
- **W5:** версионирование сейва + миграции (сейчас только отказ).
- **W6:** независимость от Alpha-контента (штатный путь «контента нет»).
- **W7:** границы событий. **W8:** диагностика. **W9:** тестовая инфраструктура + multi-seed gate.
- **W10:** воспроизводимый Windows Player pipeline (сейчас два scene builder'а пишут один `Pilot.unity`).
- **W11:** профилирование CPU/RAM/GC.

DoD и обязательный Player-сценарий — в ТЗ §8–§10.

---

## 6. G2 — состояние генерации мира

[G2_WORLD_FOUNDATION_AUDIT.md](../planning/G2_WORLD_FOUNDATION_AUDIT.md)

**Готово и можно считать production foundation:**

- deterministic seed (таблица seeds, oracle-проверка);
- высоты/базовый рельеф/горный массив с отклонением ≤1e-3 вокселя от исполненной Alpha;
- voxel-модель, сетка регион→зона→чанк→воксель; 36 864 записи зон и 704 landmark-колонки совпали точно;
- реки/дороги/пещеры как часть проверенного зонного прохода;
- chunk streaming с реальной выгрузкой и floating origin (169 чанков, освобождение подтверждено тестом);
- `WorldDeltaStore` + сохранение правок игрока через save/exit/load;
- placement foundation со стабильными ID.

**Чего нет:** render/visibility distance, vertex budget, LOD, туман (эти работы остались в P1-ветке), растительность и её плотность (P2-ветка), production-структуры, настоящие города и данжи, именованные климатические оси, data-driven биомы, бюджеты кэшей, multi-seed gate, debug-инструменты, миграция версий мира.

---

## 7. RISKS

1. **Выбор базовой линии G1 не сделан продюсером.** Цепь P0–P7 — это +29 тыс. строк уже написанного runtime, но merge в canonical не fast-forward (278 файлов, конфликты в `GameBootstrap`, `AlphaContentSource`, шейдерах). Если G1 начнётся от canonical без решения, часть работы будет сделана повторно.
2. **Реестр решений формально не согласован с pivot.** D01/D14/D20 формулируют parity как критерий первой версии; D93 фиксирует `PROPOSED CHANGE_REQUEST`. До решения продюсера возможна трактовка «parity всё ещё обязателен» при чтении только `DECISIONS_001-036.md`. Митигация: оговорка в `docs/planning/README.md` §3 + D93.
3. **Правовой режим Alpha-производных данных не решён.** В репозитории остаются 14 таблиц `Content/Generated/*.g.cs` (~7 060 строк Alpha-чисел), каталоги имён существ/предметов/звуков; локально — `.cubdec`-модели, 93 WAV, атлас иконок. Это блокер продуктового использования, но не блокер разработки.
4. **Доказательная база частично невоспроизводима в чистом checkout.** 6 zonе-oracle-тестов требуют `.scratch/m0`; 1 PlayMode — `AlphaOutput/icons`. Это ограничение evidence, а не работоспособности.
5. **Два scene builder'а пишут один `Assets/Scenes/Pilot.unity`** (`PilotSceneBuilder` и `GameSceneBuilder`), а `tools/unity/run_pilot.ps1` вызывает первый — документированный pilot-пайплайн молча заменяет игровую сцену. Риск потери работы; устраняется в G1 W10.
6. **`ProjectSettings.asset` перезаписывается Unity при сборке** (только line endings). В G0 изменение откатано, дерево чистое; при следующих сборках это будет повторяться.

---

## 8. NEXT ACTION

**G1-00 — принять решение о базовой линии G1** (canonical / цепь P0–P7 / гибрид) и зафиксировать её коммит.

Вход для решения уже собран в этом отчёте §1; если решение — «идти от P7», следующая задача — merge `codex/m0-p7-quests-cities-dungeons` в отдельную ветку от `integration/m0-all`, полный прогон EditMode+PlayMode+Player, и только после этого старт W1.

---

## 9. Что реально выполнено (команды и проверки)

| Проверка | Команда | Результат |
|---|---|---|
| Состояние репозитория | `git branch -a -vv`, `git status`, `git worktree list`, `git log --all` | найдена чужая незакоммиченная работа, 8 worktree, база отставала от origin |
| Синхронизация базы | `git fetch origin --prune` | `integration/m0-all` 1b75b27 → 4e36f15 |
| Импорт/компиляция | `Unity.exe -batchmode -nographics -quit -projectPath …` | exit 0, `error CS = 0`, 38 warnings |
| EditMode без кэша | `-runTests -testPlatform EditMode` | 744/1/11 |
| EditMode с кэшем | то же + `CUBEGAME_ALPHA_CONTENT` | 750/0/6 |
| PlayMode с кэшем | `-runTests -testPlatform PlayMode` | 69/0/1 (1 321 с) |
| Windows build | `-executeMethod CubeGame.EditorTools.PilotBuild.BuildStandalone` | `result=Succeeded errors=0 warnings=19` |
| Player smoke | `CubeGamePilot.exe -batchmode -cubeAutoNew smoke -cubeSeed 666666 -cubeScreenshot … -cubeFrames 240` | exit 0, мир построен, население 18 в 9 зонах, честная деградация без кэша; скриншот в batchmode не снят |
| P7-ветка EditMode | `-runTests -testPlatform EditMode` в `.scratch/g0-p7` | 851/1/6 |
| P7-ветка PlayMode | `-runTests -testPlatform PlayMode` | 74/0/1 (672 с) |

Логи: `D:\Projects\CubeGame\.scratch\g0-logs\` (gitignored, локально).

---

## 10. Что осталось неподтверждённым

1. P7-ветка: проверены EditMode и PlayMode; Windows Player из P7-ветки **не собирался**, merge в canonical **не выполнялся** (это G1-00).
2. Полные зонные oracle-дампы (6 тестов) и атлас иконок (1 PlayMode-тест) — требуют локальных данных вне репозитория.
3. Производительность текущего дерева (чанки/вершины/память/GC при длительном движении) не измерялась — это G1 W11.
4. Скриншот Player в batchmode не снимается; визуальная проверка кадра в G0 не выполнялась.
5. Правовой статус Alpha-производных данных — вопрос продюсера, не техники.

---

## 11. Сохранность чужой работы

На момент начала работы головная рабочая копия `D:\Projects\CubeGame` была на ветке `review/m1-creature-family-research` (на 1 коммит впереди `origin/review/m1-factory-research-pipeline`) и содержала **незакоммиченные файлы другого агента**:

- `Assets/CubeGame/Features/World/AlphaLandmarkCampCompositionCatalog.cs` (+`.meta`);
- `Assets/CubeGame/Tests/Creatures/EditMode/AlphaLandmarkCampCompositionCatalogTests.cs` (+`.meta`);
- `Assets/CubeGame/Presentation/Gameplay/Resources/AlphaFont.ttf` (+`.meta`);
- `Assets/InitTestScene719c2da4-….unity` (+`.meta`);
- `docs/reports/M0_ALPHA_FULL_PORT_P3_POPULATION_AI_REPORT.md` (локальная копия в другой кодировке, отличается от версии в canonical).

Решение: **не трогать эту рабочую копию вообще**. G0 выполнен в отдельном worktree `.scratch/g0-pivot` на новой ветке от `origin/integration/m0-all`. Ни одна команда не изменяла и не удаляла чужие файлы; `git reset --hard`, `clean`, `stash` к чужой работе не применялись. Ветка `review/m1-creature-family-research` осталась на месте со всеми изменениями.

Дополнительно: `.scratch/g0-p7` — detached worktree на `85ba64a` только для проверки, без изменений.

---

## 12. Ссылки

- [ACTIVE_STAGE](../ACTIVE_STAGE.md) · [Roadmap](../planning/ROADMAP_DRAFT.md) · [D93](../decisions/D93-development-pivot-own-game.md)
- [G0 reuse audit](../planning/G0_EXISTING_WORK_REUSE_AUDIT.md) · [G2 audit](../planning/G2_WORLD_FOUNDATION_AUDIT.md) · [G1 ТЗ](../stages/G1_TECHNICAL_FOUNDATION_STAGE.md)
