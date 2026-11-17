# G1-00 — RUNTIME BASELINE SELECTION — REPORT

**Дата:** 2026-11-17
**Этап:** G1-00 (вход в [G1 Technical Foundation](../stages/G1_TECHNICAL_FOUNDATION_STAGE.md))
**Ветка:** `deepseek/g1-00-runtime-baseline-candidate` (опубликована в `origin`)
**Коммиты:** `c613ae6` — merge P0–P7 в candidate; `08392a9` — CR-001; `ecbcd11` — выбор baseline и отчёт
**База:** `integration/m0-all` @ `4e36f1539d787e6a03f85d1f9fc92efcb1b64a79`
**Исполнитель:** DeepSeek Harness

---

## STATUS

`PASS`

Все критерии PASS G1-00 (§20 задачи) выполнены: candidate-ветка создана, P0–P7 интегрирован, компиляция/EditMode/PlayMode/Player build/runtime smoke/save-load проверены в двух режимах (с локальным Alpha-кэшем и без него), Alpha-зависимости классифицированы, F1–F3 и P0–P7 сравнены, performance baseline снят, конфликт D01/D14/D20 закрыт записью CR-001, baseline выбран, ветка опубликована.

---

## ВЫБОР BASELINE: **OPTION C**

> **P0–P7 runtime становится основой CubeGame.** Canonical `integration/m0-all` (F1–F3) остаётся исторической базой и источником предыдущего поколения сейвов. Перед слиянием candidate в canonical выполняется **ограниченный cleanup C1–C4** (список ниже); архитектурный рефакторинг в G1-00 не выполнялся.

Почему не A: в canonical физически отсутствуют системы, которые уже написаны и работают в B — render distance/vertex budget/fog (P1), растительность (P2), стартовый flow/меню/профиль/опции (P0), лут/магазин/крафт/кошелёк (P5), UI Toolkit HUD/меню/карта/журнал (P6), миссии и Alpha-данж (P7), audio (F4). Возврат к A означал бы повторную реализацию ~29 тыс. строк.

Почему не чистый B без оговорок: B содержит Alpha-параллельные продуктовые зависимости (шрифт, WAV, атлас иконок, `.cubdec`-модели, генерированные таблицы) и build-gate, который требует Alpha-кэш, чтобы назвать сборку релизной. Это не блокер разработки (fallback есть и проверен), но это то, что нельзя оставлять в продукте.

---

## 1. Точная цепь P0–P7 (через Git, без старых summary)

| Параметр | Значение |
|---|---|
| Tip | `85ba64a` («docs(p7): record M0 transfer stop state»), ветка `codex/m0-p7-quests-cities-dungeons` |
| Merge-base с `integration/m0-all` | `05336984ea48925a957a518ae1ee059be8299768` |
| Коммитов в цепи | **104** |
| Merge-коммитов в цепи | **0** — цепь линейная |
| Файлов изменено | **278** (183 добавлено, 95 изменено) |
| Строк | **+29 197 / −2 024** |
| Состав | F4 presentation (`60f24be`) → runtime recovery R0–R7 (24 коммита) → Alpha vegetation (research + реализация) → P0 (6) → P1 → P2 → P3 (13) → P4 (8) → P5 (12) → P6 (6) → P7 (21) |
| Независимые (не «P»-блоки) изменения | входят как часть той же цепи: research/vegetation/roadmap/memory/agents-коммиты; отдельных посторонних веток в цепи нет |

Проверка линейности: `git merge-base --is-ancestor` по всей цепочке P1→P2→P3→P4→P5→P6→P7 = true.

---

## 2. Candidate-ветка и интеграция

- Ветка создана от `origin/integration/m0-all` (как требовалось), затем в неё влит `deepseek/g0-development-pivot` (fast-forward: `4e36f15` → `794e075`) — чтобы candidate нёс актуальные pivot-документы.
- Затем влит `origin/codex/m0-p7-quests-cities-dungeons` с сохранением истории: merge-коммит `c613ae6`.
- **Конфликтов в коде нет ни одного.** Конфликтов всего 8, все — документация:

| Файл | Решение | Причина |
|---|---|---|
| `AGENTS.md` | canonical/G0 | сторона цепи — доpivotная версия («M0 = полный перенос») |
| `PROJECT_MEMORY.md` | canonical/G0 | то же |
| `docs/ACTIVE_STAGE.md` | canonical/G0 | то же |
| `docs/decisions/D92-…` | canonical/G0 | у canonical есть LEGACY-баннер, тело идентично |
| `docs/planning/ROADMAP_DRAFT.md` | canonical/G0 | у цепи — **старый** pre-pivot roadmap (84 строки), у canonical — pivot-карта (637 строк) |
| `docs/reports/M0_ALPHA_FULL_PORT_P0_REPORT.md` | canonical/G0 | тот же документ, но у цепи в повреждённой кодировке (CP1251-mojibake), у canonical — корректный UTF-8 |
| `docs/stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md` | canonical/G0 | у canonical LEGACY-баннер поверх того же тела |
| `docs/planning/M0_ALPHA_PARITY_BACKLOG.md` | canonical/G0 **+ добавлен блок P7-B из цепи** | блок «P7-B — данж» существовал только в цепи и является новым знанием |

Механических `ours/theirs` по коду не применялось: `Assets/` и `tools/` взяты из цепи без изменений.

**Проверка:** `git diff origin/codex/m0-p7-quests-cities-dungeons HEAD -- Assets tools` — пусто. То есть код candidate побайтово равен P7-tip.

---

## 3. Компиляция

| Проверка | Результат |
|---|---|
| Импорт/компиляция (batchmode, чистая Library) | **`error CS = 0`**, 38 warnings |
| Missing references / packages | не обнаружено; все 17 asmdef разрешаются |
| Build warnings (Player) | 19 |

---

## 4. EditMode

| Режим | total | passed | failed | skipped | Время |
|---|---|---|---|---|---|
| **A — с локальным Alpha-кэшем** | 859 | **853** | **0** | 6 | ~20 с тестов |
| **B — без Alpha-кэша** | 859 | **845** | **1** | 11 | ~20 с тестов |

- 6 skip в обоих режимах: `FullM0ZoneOracleTests.CompleteZoneMatchesLocalOracleDump(zone_*)` — нужны дампы `.scratch/m0`, которых нет в чистом checkout.
- +5 skip в режиме B: `AlphaContentCacheTests` ×4 и `CreatureSpawnTests.AllStagedKindsResolveAgainstTheCache` — корректно пропускаются без кэша.
- Единственный fail в режиме B: `AlphaZoneContentProviderTests.SettingsFromLibrarySplitPassiveObjectsFromCreatures` — это **утверждение о наличии контента** («staged passive objects feed the W-18 selection»), а не проверка graceful-режима. Классификация: тест должен быть `RequiresAlphaCache` (см. C1).

Для сравнения: canonical A давал 756/750/0/6 (с кэшем) и 756/744/1/11 (без).

---

## 5. PlayMode

| Режим | total | passed | failed | skipped | Время |
|---|---|---|---|---|---|
| **A — с кэшем** | 75 | **75** | **0** | **0** | 671–680 с |
| **B — без кэша** | 75 | **39** | **3** | **33** | ~660 с |

Режим B — три падения, все с явными сообщениями о контенте:

| Тест | Сообщение |
|---|---|
| `FullM0F2IntegrationPlayModeTests.WorldCharacterAndCombatRunTogetherThroughSaveExitLoad` | `DATA_MISSING alpha content cache: ALPHA_CONTENT_NOT_CONFIGURED` |
| `FullM0WorldStreamingPlayModeTests.HeroStartsOnAlphaTerrainAndChunksStreamWithFloatingOrigin` | «AlphaContent vegetation was installed with the chunks» — ложно без кэша |
| `FullM0WorldStreamingPlayModeTests.PlantSupportEditSurvivesChunkReloadAndSaveLoad` | `Expected: not null` (модель растения) |

33 skip — тесты, которые сами вызывают `Assert.Ignore` без кэша (визуальные, актёрские, AI-визуальные). **Ни одного crash, ни одного повреждённого сейва, ни одного зависания загрузки** — 39 тестов без контента проходят, включая базовый streaming, сохранение мира, бой и сессии.

Классификация: 3 падения — content-presence assertions, а не дефекты; переводятся в `RequiresAlphaCache` (C1).

---

## 6. Windows Player

### 6.1 Сборка

| Параметр | Build A (с контентом) | Build B (bypass) |
|---|---|---|
| Метод | `CubeGame.EditorTools.ReleaseBuild.BuildWindows64` | то же + `CUBEGAME_BUILD_WITHOUT_ALPHA_CONTENT=1` |
| Результат | `result=Succeeded`, **errors=0**, warnings=19 | `result=Succeeded`, **errors=0**, warnings=1 |
| Size | 92 666 617 B | 92 666 617 B (без контента) |
| Время | 26.5 с | 5.3 с |
| Контент | `CONTENT_STAGED actors=156 actorModels=747 clips=927 items=1554 appearance=180 sounds=93 iconAtlas=1 bytes=231 993 390` → **4 225 файлов** в `CubeGame_Data/StreamingAssets/CubeGame/AlphaContent` | `StreamingAssets` **отсутствует** |
| Exit code | 0 | **1 (by design)**: `[Pilot] BATCH_FAIL: release build refused: CUBEGAME_BUILD_WITHOUT_ALPHA_CONTENT=1` — сборка помечена как «не релиз» |

### 6.2 Runtime-сценарии (Player, batchmode)

| # | Сценарий | Режим | Wall | Peak WS | Результат |
|---|---|---|---|---|---|
| A1 | новый мир + validate + save + screenshot | A | 33.0 с | 2 379 MB | `world built gen=/9 chunks=25 vertices=116108 113.9ms`, `population: 56 content, 0 recovered in 9 zones`, `saved g1a tick=177`, файлы `.sav/.session.sav/.world.sav` |
| A2 | **load → continue** | A | 21.3 с | 2 203 MB | `loaded g1a: 'Auto World' tick=177` → продолжил до tick=309, hero на той же позиции, без исключений |
| A3 | gameplay smoke (kill → loot → wallet → save) | A | 20.2 с | 2 056 MB | `SMOKE_KILL=FAIL no hostile creature` (в стартовой зоне нет враждебных), `SMOKE_SLOT2=REFUSED action 0x3B unsupported`, `SMOKE_SAVE=PASS` |
| A4 | телепорт в дикую зону + атака | A | 29.7 с | 2 482 MB | `[CombatAction] strike actor=hero action=57 … Completed`, `chunks=1398 vertices=11.66M/12.58M plants=30202` |
| A5 | UI: экран инвентаря | A | 22.5 с | 2 062 MB | `[UI] P6 ui screen 'inventory' opened`, без исключений |
| A6 | длинная сессия 2 400 кадров | A | 41.0 с | 2 540 MB | `chunks=1306 pending=0 vertices=12 584 500/12 582 912 plants=26 765 horizon=585.1` |
| B1 | новый мир + save (без контента) | B | 19.2 с | 1 301 MB | мир `/9` построен за 67.1 мс, `GUI_ICONS=ABSENT` (с подсказкой), `plants=0`, save записан, выход 0 |
| B2 | load → continue → атака | B | 10.6 с | 1 172 MB | `loaded g1nb tick=162` → 286, `strike action=57 pose=ANIMATION_PARTIAL`, без исключений |

**Вывод по §8B:** без Alpha-кэша нет crash, corrupted save, infinite loading или фатальных исключений. Есть только визуальная деградация (placeholder-модели, `plants=0`, иконки/звук отсутствуют, анимация `ANIMATION_PARTIAL`), и каждая деградация явно логируется.

### 6.3 Найденный дефект автоматизации (не блокер baseline)

`-cubeFrames <n>` устанавливает кадр скриншота (`_screenshotFrame = frames`), но **не** задаёт `_screenshotPath`. Если `-cubeScreenshot` не передан, `CaptureWorldCamera(null)` бросает `ArgumentNullException` в `Update()`, и выход по кадрам не происходит — процесс висит (наблюдалось 610 с до принудительной остановки).

- **Root cause:** `GameBootstrap.cs`, обработчик `-cubeFrames`.
- **Наследование:** та же конструкция есть в canonical (`git show 4e36f15:…/GameBootstrap.cs`), то есть дефект **не внесён** цепью P0–P7.
- **Влияние:** только dev-автоматизация Player; на игру не влияет.
- **Предлагаемый фикс (C2):** не ставить `_screenshotFrame`, если путь не задан, и/или при `string.IsNullOrEmpty(_screenshotPath)` логировать и пропускать захват.

### 6.4 Save/load целостность

- Форматы **не изменились** относительно A: `CUBEGAME_SAVE` v1, `CUBEGAME_SESSION` v1, `CUBEGAME_WORLD` v1; добавлен только слой профиля (`characters.index`/`worlds.index` + `MigrateLegacySlots`).
- Нагрузка/загрузка проверена в обоих режимах: позиция героя, tick, население (56 в 9 зонах и до, и после загрузки — дублирования нет), открытые сундуки/дельты покрыты PlayMode-тестами.
- Генератор: canonical-миры `/6` **читаются** candidate'ом (`WorldState.TryCreateGenerator` принимает `/4`, `/5`, `/6`, `/7`, `/8`, `/9`); новые миры пишутся как `/9`. Потери старых миров при переходе на C нет.
- Не сохраняется (by design, как и в A): позиции/HP живых NPC, лут на земле, состояние действий и снарядов, ассортимент торговцев.

---

## 7. Alpha-зависимости

Полная таблица «где / зачем / заменимо / сложность» — в [G1_RUNTIME_REUSE_MATRIX](../planning/G1_RUNTIME_REUSE_MATRIX.md) §3. Итоги:

| Класс | Что входит |
|---|---|
| `RESEARCH_ONLY` | `.scratch/m0` зонные дампы, oracle-фикстуры, `tools/alpha_content/*` (SIF-парсинг, SHA `Cube.exe`), `AlphaContentInventory`, `ContentPackagingTests`, `Editor/AlphaModelsImport`, оригинальная установка `D:\Games\Cube world` |
| `TEMPORARY_RUNTIME` | локальный кэш `.scratch/content` (232 МБ), `AlphaOutput/icons/atlas.png`, `90+` WAV, `Resources/AlphaFont.ttf` (стадируется скриптом) |
| `PRODUCT_BLOCKER` | `.cubdec` модели/риги/клипы, атлас иконок из Alpha GUI, Alpha-WAV, Alpha-шрифт, 14 таблиц `Content/Generated/*.g.cs` (~7 060 строк), `AlphaContentStaging` build-gate |
| `REMOVE` | demo/showcase-классы (`CombatDemo`, `EquipmentCombatDemo`, `AlphaContentShowcase`, `PilotBootstrap`) — не в игровом пути |

Технически важно: **ни один runtime-класс не читает `Cube.exe`, `data1/2.db`, `resource1.dat` и не содержит абсолютных локальных путей** (проверено grep: 0 совпадений по `D:\`, `C:\` в `Assets/**/*.cs`). Вся Alpha-зависимость сведена к четырём точкам кэша и сгенерированным таблицам, каждая из которых имеет точку замены.

---

## 8. Сравнение A (F1–F3) и B (P0–P7)

Полная матрица — [G1_RUNTIME_REUSE_MATRIX](../planning/G1_RUNTIME_REUSE_MATRIX.md) §1. Кратко:

| | A canonical | B candidate |
|---|---|---|
| Код | 274 `.cs` / 55 267 строк | 328 `.cs` / 72 400 строк |
| Bootstrap | 2 236 строк (2 файла), IMGUI UI | 5 184 строки (9 partial), UI Toolkit + HUD/меню/опции |
| Мир | `/6`, без render distance/LOD/fog | `/9` (+ чтение `/4…/8`), vertex budget 12.58M, горизонт, туман |
| Растительность | нет (`VegetationPerZone = 0`) | деревья/декор/меши (26.8 тыс. растений в длинной сессии) |
| Бой | фазы/hit/damage, без способностей | + `AlphaClassAbilities`, расширенные тайминги |
| Предметы | инвентарь, крафт только Life Potion | + лут/магазин/крафт/кошелёк по Alpha-формулам |
| Миссии | журнал охот | генератор миссий 1–5, данж с хозяином, патрули |
| Сейв | 3 файла, без версионирования | + профиль-слой и миграция слотов; формат v1 сохранён |
| Тесты | 756 / 70 | **859 / 75** |
| Сборка | `PilotBuild` | + `ReleaseBuild` + контент-гейт + bypass |

Ни одной системы, которая есть в A и отсутствует в B, не найдено.

---

## 9. Классификация P0–P7

| Блок | Класс |
|---|---|
| F4 presentation | `KEEP_BUT_REWORK` (G12: заменить Alpha-WAV/компоновку) |
| Runtime recovery (свет, тени, материалы) | `KEEP_BUT_REWORK` (G9/S06) |
| Vegetation (P2/P7) | `KEEP_BUT_REWORK` (G2/G9: плотность и биомы) |
| P0 startup/menu/profile/options | `KEEP` |
| P1 render distance/vertex budget/fog | `KEEP` |
| P3 population/AI/patrols | `KEEP_BUT_REWORK` (G5: pathfinding, расписания, фракции) |
| P4 combat/abilities | `KEEP_BUT_REWORK` (G4) |
| P5 loot/shop/crafting/wallet | `KEEP_BUT_REWORK` (G6) |
| P6 UI Toolkit HUD/menus/map/journal | `KEEP_BUT_REWORK` (G12) |
| P7 missions/dungeons | `KEEP_BUT_REWORK` (G11) |
| `AlphaContentStaging`, Alpha-шрифт, WAV, атлас, `.cubdec`, `Content/Generated` | `RESEARCH_ONLY` / `REMOVE_FROM_PRODUCT` |

---

## 10. Performance baseline (точка отсчёта G1, без оптимизации)

Измерено на Player (batchmode, 1280×720, RTX 4060 Laptop, seed 666666):

| Метрика | Значение | Источник |
|---|---|---|
| Startup до входа в мир (wall, batchmode) | 19–33 с (A1 33.0 с; B1 19.2 с без контента) | runner |
| Построение стартового мира | **67–357 мс** (`world built … 113.9 ms` / `67.1 ms` / `325–357 ms` при повторной загрузке) | Player log |
| Стартовые чанки | 25 (2×2 зоны + запас) | Player log |
| Стриминг до насыщения | 1 095–1 398 чанков за 240–900 кадров | Player log |
| Vertex budget | **12 582 912**; факт 10.2M → 12 584 500 (переполнение на 1 588, бюджет «мягкий») | Player log |
| Растительность | 22 303 → 30 202 → 26 765 растений в зависимости от зоны | Player log |
| Горизонт (render distance) | 332–585 (растёт со стримингом) | Player log |
| Активные NPC | **56 content-акторов в 9 зонах** (canonical: 0 content / 18 recovered) | Player log |
| Peak working set | **2 379–2 540 MB** с контентом; **1 172–1 301 MB** без контента | runner (сэмплинг 200 мс) |
| Frames/sec (batchmode, без vsync) | ~58 FPS средняя (2 400 кадров за 41 с) | wall clock |
| GC / main-thread spikes | **не инструментировано** — требует G1 W11 | — |
| Unload (released chunks) | в Player-прогонах `released=0` (телепорт не выгружает); выгрузка подтверждена PlayMode-тестом `HeroStartsOnAlphaTerrainAndChunksStreamWithFloatingOrigin` | PlayMode |

Внимание: 12.58M вершин и 2.5 ГБ RSS — это цена Alpha-подобного render distance; для CubeGame это вход в G2/G9 (бюджеты и quality presets), а не приговор.

---

## 11. D01 / D14 / D20 — конфликт закрыт

Продюсер подтвердил: полный Cube World Alpha parity **не является** целью первой версии CubeGame.

Сделано в реестре решений ([DECISIONS_001-036.md](../planning/DECISIONS_001-036.md)):

- добавлена запись **CR-001** с ID, причиной, затронутыми решениями, последствиями и решением продюсера;
- в строках D01, D14, D20 текст решений **не изменён**, в колонку «граница/примечание» добавлена пометка `superseded: CR-001`;
- остальные 33 решения не тронуты; GDD-содержание, не относящееся к parity, сохранено;
- [D93](../decisions/D93-development-pivot-own-game.md) переведён из `PROPOSED CHANGE_REQUEST` в `APPROVED` со ссылкой на CR-001.

История не переписана: старые формулировки видны в файле и в git history.

---

## 12. Ограниченный cleanup (C1–C4) перед слиянием candidate в canonical

Это **не** сделано в G1-00 (по условию задачи — без массового рефакторинга) и является входом G1:

| # | Что | Где | Почему |
|---|---|---|---|
| **C1** | Пометить 4 content-presence теста как `RequiresAlphaCache` (1 EditMode, 3 PlayMode) | `AlphaZoneContentProviderTests`, `FullM0F2IntegrationPlayModeTests`, `FullM0WorldStreamingPlayModeTests` (2 теста) | сейчас они падают без кэша вместо явного skip → ломают «зелёный» прогон на чистой машине (G1 W9) |
| **C2** | Не ставить `_screenshotFrame` без `_screenshotPath`; при пустом пути — лог и пропуск захвата | `GameBootstrap.cs` (обработчик `-cubeFrames`) | NRE + зависание автоматизации (G1 W10) |
| **C3** | Сделать build-gate независимым от Alpha-кэша: требовать собственный контент-манифест CubeGame, а Alpha-кэш — опционально | `Editor/AlphaContentStaging.cs`, `Editor/ReleaseBuild.cs` | сейчас релизная сборка без Alpha-кэша невозможна (G1 W10) |
| **C4** | Зафиксировать шрифт: либо собственный TTF, либо оставить placeholder как штатный | `GameBootstrap.AlphaFont.cs`, `Resources/AlphaFont` | Alpha-шрифт — `PRODUCT_BLOCKER`, но fallback уже работает (G12) |

После C1–C4 candidate становится канонической базой: `integration/m0-all` ← `deepseek/g1-00-runtime-baseline-candidate`.

---

## 13. Изменённые и созданные файлы G1-00

| Файл | Изменение |
|---|---|
| `docs/reports/G1_00_RUNTIME_BASELINE_SELECTION_REPORT.md` | создан (этот отчёт) |
| `docs/planning/G1_RUNTIME_REUSE_MATRIX.md` | создан (матрица сравнения и классификация) |
| `docs/planning/DECISIONS_001-036.md` | добавлена запись CR-001, пометки в D01/D14/D20 |
| `docs/decisions/D93-development-pivot-own-game.md` | статус → `APPROVED`, §4 переписан под CR-001 |
| `docs/planning/README.md` | ссылка на CR-001 вместо «открытого пункта» |
| `docs/reports/G0_DEVELOPMENT_PIVOT_REPORT.md` | риск №2 помечен закрытым |
| `docs/ACTIVE_STAGE.md` | активный этап → G1, baseline = candidate |
| `PROJECT_MEMORY.md` | запись о baseline G1-00 |
| весь код `Assets/` + `tools/` | **из цепи P0–P7 без изменений** |

---

## 14. Выполненные команды (сокращённо)

```
git worktree add .scratch/g1-00 -b deepseek/g1-00-runtime-baseline-candidate origin/integration/m0-all
git merge deepseek/g0-development-pivot                     # fast-forward → 794e075
git merge --no-commit --no-ff origin/codex/m0-p7-quests-cities-dungeons   # 8 doc conflicts, resolved; commit c613ae6
Unity.exe -batchmode -nographics -runTests -testPlatform EditMode   # ×2 (с кэшем / без)
Unity.exe -batchmode -runTests -testPlatform PlayMode              # ×2
Unity.exe -batchmode -nographics -executeMethod CubeGame.EditorTools.ReleaseBuild.BuildWindows64   # ×2 (A: staging, B: bypass)
CubeGame.exe -batchmode -cubeAutoNew … / -cubeAutoLoad … / -cubeP5Smoke fresh / -cubeTeleport / -cubeAttackFrame / -cubeUiScreen
```

Логи и скриншоты: `D:\Projects\CubeGame\.scratch\g0-logs\`, `.scratch\g1-run-A\`, `.scratch\g1-run-B\` (gitignored).

---

## 15. Что осталось неподтверждённым

1. GC и main-thread spikes не измерены — нет инструментации (G1 W11).
2. Выгрузка чанков в Player не наблюдалась (`released=0`) — телепорт не моделирует непрерывное движение; выгрузка доказана PlayMode-тестом.
3. 6 oracle-тестов полных зон по-прежнему требуют `.scratch/m0`.
4. Alpha-данж и миссии типа 5 проверены тестами EditMode/PlayMode, но боевой зачёт хозяина в Player не проверялся.
5. Визуальное качество кадров оценивалось по логам и скриншотам, не продюсером.

---

## 16. Git-безопасность

- Чужая рабочая копия `D:\Projects\CubeGame` (ветка `review/m1-creature-family-research` + 8 незакоммиченных файлов другого агента) **не тронута**; G1-00 выполнен в отдельном worktree `.scratch/g1-00`.
- `reset --hard`, `clean`, `force-push` не применялись.
- Слияние выполнено с сохранением истории (merge-коммит), без squash.
