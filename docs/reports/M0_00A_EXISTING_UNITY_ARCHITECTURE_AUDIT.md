# M0-00A — аудит существующей Unity-архитектуры CubeGame (issue #6)

**Статус: `READY_FOR_REVIEW`** (исследование/документация). Это **не** начало M0-01; кода игры этот документ не
меняет. S05B-2 остаётся `READY_FOR_REVIEW`, M0-00 — `DRAFT`. **Исполнитель:** Claude Code, 2026-11-08.

**База аудита (зафиксированный SHA):** `721d222` — опубликованный результат D69
(`origin/claude/s05b2-d69-alpha-transfer-implementation`; он же база `review/s05b2-d69-acceptance-check`).
Аудит проведён **только чтением** этого SHA: снимок выгружен `git archive 721d222` во временную папку вне
общей рабочей копии; локальная ветка не переключалась, Unity Editor для аудита не запускался, клонов/worktree нет.
Ветка результата — отдельная `planning/m0-00a-unity-architecture-audit` (коммит через временный индекс git).

Сопутствующий документ: [M0_00A_INTEGRATION_DEPENDENCIES](../planning/M0_00A_INTEGRATION_DEPENDENCIES.md)
(схема зависимостей, безопасный порядок интеграции, предложения к M0-00C/D, вопросы продюсеру).

## 0. Что реально проверено

| Проверка | Как | Результат |
|---|---|---|
| Состав сборок и ссылки asmdef | чтение 6 `.asmdef`, `Packages/manifest.json`, `EditorBuildSettings` | 6 сборок, ацикличный граф (§1) |
| Ядро без Unity | `tools/core_check/run_core_check.ps1` на выгруженном снимке (Roslyn из Unity 6000.3.27f2, **без** редактора) | компиляция 30 файлов `Core` — OK; проверки генерации **PASS 10 / FAIL 0** |
| Границы слоёв | поиск `using`/полных имён по снимку | `Core` → UnityEngine: 0 ссылок (только комментарии); `Core` → Persistence/Presentation: 0; `Persistence` → Presentation: 0; рантайм S05 → `CubeGame.*`: 0 |
| Что подключено к сцене | `Pilot.unity`, `PilotSceneBuilder`, все `AddComponent<…>` | в сцене только `PilotBootstrap` (+ камера с `OrbitCameraController`); бой, экипировка, сохранения — **только в PlayMode-тестах** |
| Инвентарь тестов | подсчёт `[Test]/[UnityTest]` | EditMode **157**, PlayMode **17** методов (не перезапускались в M0-00A: Unity не запускается при работе другого агента; последние прогоны — в отчётах S02–S04) |
| Рантайм S05 | чтение `tools/alpha_models/transfer/unity`, `run_transfer.ps1`, `review/unity` | живёт вне `Assets/`, копируется в изолированные проекты `.scratch` (§4) |

**Не подтверждено в M0-00A** (нужен эксклюзивный Unity-прогон): EditMode/PlayMode-тесты `Assets/CubeGame` на
этом SHA, импорт рантайма S05 в основной проект, совместимость с `Pilot.unity`. Это пункты M0-01A.

## 1. Сборки и зависимости (факт)

```text
CubeGame.Core            (noEngineReferences: true, без ссылок)
  ^          ^
  |          CubeGame.Persistence   (Core; FileSaveStore, JsonSaveSerializer — UnityEngine.JsonUtility)
  |          ^
CubeGame.Presentation    (Core, Persistence; MonoBehaviour: бутстрап, мотор, камера, HUD, демо боя/экипировки)
  ^
CubeGame.Editor          (Core, Presentation; Editor-only: PilotSceneBuilder, PilotBuild, BatchMode)
CubeGame.Tests.EditMode  (Core, Persistence)      CubeGame.Tests.PlayMode (Core, Persistence, Presentation)

ВНЕ проекта: tools/alpha_models/transfer/unity/{Runtime,Editor}  — namespace CubeGame.AlphaTransfer, БЕЗ asmdef
             tools/alpha_models/review/unity/{Runtime,Editor}    — namespace CubeGame.AlphaTransfer.Review, БЕЗ asmdef
```
Слоя `Application/Features` из ADR-0001 **ещё нет**: сценарные классы сейчас — MonoBehaviour-демо в `Presentation`.
Пакеты: `test-framework 1.5.1`, `ugui 2.0.0`, встроенные модули; Input System **не** подключён (используется
`UnityEngine.Input`). Сцена сборки одна: `Assets/Scenes/Pilot.unity`.

## 2. Матрица подсистем

Легенда «пилот/интегрировано»: **СЦЕНА** — работает в `Pilot.unity`; **ТЕСТ** — только в тестах/демо-компонентах;
**СТЕНД** — только в изолированном проекте `.scratch`; **—** — нет.

| Подсистема | Путь / класс | Реально реализовано | Тест / доказательство | Пилот / интегрировано | Пробел | Риск | Подэтап M0 |
|---|---|---|---|---|---|---|---|
| Точка входа | `Presentation/PilotBootstrap.cs`, `Editor/PilotSceneBuilder.cs`, `Scenes/Pilot.unity` | код-сборка сцены: мир 8×8 чанков, капсула-игрок, камера, свет, HUD; аргументы `-cubeSeed/-cubeChunks/-cubeScreenshot` | `PilotPlayModeTests` (2); `tools/unity/run_pilot.ps1` | СЦЕНА | нет меню, слотов, загрузки/выхода, жизненного цикла мира | бутстрап — «бог-объект»: при росте станет точкой связности всех систем | M0-01A/B/C |
| Генерация мира | `Core/Generation/PilotHeightField`, `PilotWorldLayout`, `DeterministicHash` | детерминированная карта высот (4 октавы), чанк 32 блока (= Alpha ZoneTile); остальное `PLACEHOLDER_NOT_ALPHA` | `TerrainDeterminismTests` (5); core_check 10/10 PASS (M0-00A) | СЦЕНА | не генератор Alpha; нет биомов, воды, пещер, стриминга, версии генератора | замена генератора меняет мир у старых сейвов → версия генератора в сейве обязательна | M0-02A/B/D |
| Меш/рендер мира | `Core/Rendering/ChunkMeshBuilder`, `MeshData`, `PilotPalette`; `Presentation/Rendering/PilotWorldView`, `UnityMeshFactory`, `PilotMaterials`, шейдер | меш чанка без Unity-типов, отсечение скрытых граней; построение всей области сразу | `ChunkMeshTests` (5); core_check | СЦЕНА | нет выгрузки/дозагрузки, коллизии — `MeshCollider` всей области | производительность при стриминге; палитра отладочная | M0-02B |
| Персонаж/прогрессия | `Core/Characters/CharacterClass`, `CharacterSheet`, `CharacterStats`; `Core/Progression/ProgressionState`, `AlphaMath` | классы/спецы, HP по формуле, XP/уровни (S02, D44) | `CharacterStatsTests` (10), `ProgressionTests` (19); `CoreFoundationPlayModeTests` (4) | ТЕСТ (`CharacterSheetPresenter`) | нет создания героя, расы, внешности; персонаж не связан с капсулой в сцене | **ID класса**: Core — 0-based `classIdx`, данные S05/D64 — сырой байт `+0x140` (= `classIdx+1`), см. §5 R-ID-1 | M0-03A/D |
| Бой | `Core/Combat/*` (`CombatFormulas`, `HitResolver`, `DamageResolver`, `DamageMitigation`, `HealthPool`, `CombatOutcome`, `CombatRandom`, `CombatAttack`) | расчёт попадания/урона/защиты, HP, детерминированный ГСЧ (S03, D45) | `CombatFormulaTests` (17), `CombatResolutionTests` (21); `CombatCorePlayModeTests` (4) | ТЕСТ (`CombatDemo`) | нет команд атаки, целей, хитбоксов, связи с окнами анимаций | тайминг удара (S05 `combat_get*`) и расчёт урона не связаны — риск двух «часов» | M0-05A/C |
| Предметы/экипировка | `Core/Items/*` (`ItemInstance`, `ItemStatsCalculator`, `ItemEnums`), `Core/Equipment/*` (`EquipmentLoadout`, `EquipmentStats`, `EquipmentCombatAdapter`, `EquipmentSlot`) | статы предметов, 6 слотов, сумма статов, адаптер в бой без двойного `levelFactor` (S04, D47) | `ItemStatsTests` (18), `EquipmentLoadoutTests` (14), `EquipmentStatsTests` (14), `EquipmentCombatIntegrationTests` (10); `EquipmentCombatPlayModeTests` (7) | ТЕСТ (`EquipmentCombatDemo`) | нет инвентаря/подбора/лута; модели предметов не связаны | `EquipmentSlot` (6) ≠ слоты отрисовки Alpha `drawModel` (10, D64); `ItemType` покрывает 2–9, маршрут предмет→модель D64 (`item_icon_id`, 24 типа) шире | M0-04A/B |
| Сохранения | `Core/Persistence/SaveService`, `SaveGame`, `SaveContracts`, `CharacterSaveMapper`; `Persistence/FileSaveStore`, `JsonSaveSerializer` | рамка `CUBEGAME_SAVE|версия|FNV-1a64|длина`, атомарная запись `.tmp`→`File.Replace` с `.bak`, валидатор, `CurrentVersion = 1` | `PersistenceTests` (24); `CoreFoundationPlayModeTests` | ТЕСТ | сохраняются только персонаж и список предметов; **назначение слотов `EquipmentLoadout` не сохраняется**; мира/сущностей нет; миграций нет (`MigrationFailed`) | первое же расширение схемы ломает старые сейвы без механизма миграций | M0-01D, M0-04D |
| Ввод/движение | `Presentation/Player/PlayerMotor` | `CharacterController`, ходьба/бег/прыжок от камеры, `UnityEngine.Input` напрямую | `PilotPlayModeTests` | СЦЕНА | нет слоя команд; плавание/лазание нет | скорости `PLACEHOLDER_NOT_ALPHA` (OQ-23); ввод зашит в MonoBehaviour | M0-01C, M0-03B |
| Камера | `Presentation/Camera/OrbitCameraController`; дубль `review/unity/Runtime/ReviewOrbitCamera` | орбита за целью, блокировка курсора | — | СЦЕНА / СТЕНД | — | две камеры с похожей логикой (вторая — инструмент просмотра, в игру не переносить) | M0-03B |
| UI/HUD | `Presentation/Hud/PilotDebugHud` (IMGUI), `CharacterSheetPresenter`; просмотрщик `ReviewViewer` (IMGUI, 918 строк) | отладочные надписи | — | СЦЕНА / СТЕНД | нет меню, инвентаря, HUD боя | IMGUI-просмотрщик не основа игрового UI | M0-01B → M0-08A |
| Модели/риг S05 | `tools/alpha_models/transfer/unity/Runtime/AlphaRig`, `VoxelMesh`, `AlphaSpace`, `AlphaEntityContract` | сборка сущности из `.cub`-частей, якоря (вкл. `head_0x65`), маркеры/tint, контракт `cubegame.entity_contract/1` | Python 332→337 (D69/#4), Unity `D69_VERIFY` 29/29 + 14/14, D65 статика 2856/2856 | СТЕНД | не в `Assets/` основного проекта; без asmdef | **масштаб**: меш в единицах сетки вокселей (1 воксель = 1 ед. Unity), мир пилота — 1 блок = 1 ед., капсула 1.8 ед.; соотношение блок↔воксель в коде не задано | M0-03C, M0-06A |
| Анимации S05 | `AlphaClip`, `AlphaClipPlayer`, `AlphaParamWalk`, `AlphaLocomotionDriver`, `AlphaAnimationController`, `AlphaNative*`, `AlphaWeaponLocomotion` | формула ходьбы/покоя (156/156 = `drawModel`), клипы действий, контроллер с фазами входа/выхода, режимы `ACTIONS/LOCOMOTION_ONLY/STATIC_OBJECT` | Unity-прогоны пакетов D59–D69; регрессия HumanMale 98/98 | СТЕНД | игровые триггеры: класс/оружие NPC из спавна мира — `DEFERRED_TO_M0`; эффекты R-3, динамические части R-8, RuneGiant R-9 — `BLOCKED` | контроллер тикает от `Time.deltaTime` сам (`externalClock=false` по умолчанию) — нужно единое игровое время | M0-03C, M0-05C, M0-06A/C |
| Проверочные компоненты S05 | `Alpha*Probe` (5 шт.), `Editor/*PlayMode`, `AlphaTransferSelfTests` | пишут кадры/вердикты в `TransferOutput` | — | СТЕНД | — | смешаны с рантаймом в одной папке `Runtime` → попадут в игровую сборку | M0-01A |
| Данные S05 | `Assets/AlphaTransfer/Data/*.json` стенда; `StreamingAssets/Review/*` просмотрщика | JSON-контракт, риг, клипы, локомоция, действия | — | СТЕНД | путь данных — `Application.dataPath`/`File.ReadAllText` (только редактор) | в сборке игрока данных нет; ассеты Alpha не должны попасть в Git | M0-01A, M0-06A |
| NPC/AI, данжи, задания, торговля, крафт, звук | — | — | — | — | отсутствуют | — | M0-05…M0-08 |

## 3. Что работает в настоящей сцене, а что только в тестах

* **В сцене `Pilot.unity`:** генерация и меш области, капсула с `CharacterController`, орбитальная камера, HUD.
* **Только в тестах / демо-компонентах:** персонаж/прогрессия (`CharacterSheetPresenter`), сохранение и
  загрузка с диска, бой (`CombatDemo`), экипировка в бою (`EquipmentCombatDemo`). Ни один из них не добавляется
  `PilotBootstrap`-ом — интеграции в игровой цикл нет.
* **Только в изолированных проектах `.scratch`:** весь рантайм моделей/анимаций S05 и сцена просмотра.

## 4. Рантайм S05: где живёт и как подключается

* Исходник: `tools/alpha_models/transfer/unity/{Runtime,Editor}` (17 + 8 файлов C#), namespace
  `CubeGame.AlphaTransfer`, **без asmdef** — в стенде попадает в `Assembly-CSharp`.
* Подключение: `run_transfer.ps1` копирует `Runtime\*` → `<проект>\Assets\AlphaTransfer\Scripts\Runtime`,
  `Editor\*` → `...\Scripts\Editor`; Python-конвейер (`pipeline.prepare_unity`) пишет данные в
  `Assets\AlphaTransfer\Data`; просмотрщик (`review/unity`) копируется `setup_review.ps1` и читает
  `StreamingAssets/Review/<id>`.
* Реестр видов/контроллеров/клипов: Python `universal/contract.py` (`ACTION_PROFILES`, `D69_MERGED_LAUNCHES`,
  режимы контроллера), `catalog/spawn_roles.py` (`STATIC_OBJECT`), `catalog/npc_trigger_evidence.json`; в Unity —
  `AlphaEntityContract.Parse` + `AlphaAnimationController` (`locomotionOnly`, `staticObject`, `actionsBlockedReason`).
* Ссылок на `CubeGame.Core/Presentation` нет — перенос в основной проект не создаст цикла, если рантайм станет
  отдельной сборкой уровня Presentation (см. план зависимостей).
* Заблокировано/отложено (реестр D69): R-1/R-2 оружейные действия NPC и HumanMale-NPC — `DEFERRED_TO_M0`;
  R-3 эффекты/снаряды, R-8 динамические части (камень 108, черепаха 72), R-9 RuneGiant — `BLOCKED`;
  скорости OQ-23 — `PLACEHOLDER_NOT_ALPHA`.

## 5. Риски интеграции

| ID | Риск | Факт | Что предлагается (M0-00C решает) |
|---|---|---|---|
| R-ID-1 | Несовпадение ID класса | `Core.Characters.CharacterClass` = 0-based `classIdx` (комментарий: Alpha `+0x140 = classIdx + 1`); условия запуска действий D64/D69 (`launch_conditions`) записаны **сырым** байтом `+0x140`. Пример: действия 37/40/42/43 «class 3» = `classIdx 2` = **`Mage`** в терминах Core, не `Rogue` | один конвертер `AlphaIds.ClassFromByte/ToByte` в слое импорта + тест; сырые байты не должны попадать в Core |
| R-ID-2 | Слоты экипировки | `EquipmentSlot` 6 значений (MainHand/OffHand/Chest/Gloves/Boots/Shoulders); у Alpha 10 слотов отрисовки (D64) | таблица соответствия слот Core ↔ слот отрисовки, расширение enum только с миграцией сейва |
| R-ID-3 | Типы предметов | `ItemType` 2–9 (+ подтипы оружия до 0x14); маршрут предмет→модель D64 — 24 типа `item_icon_id` | реестр `ItemType → модель` в данных, неподдержанные типы — явный `UNSUPPORTED`, не молчаливый пропуск |
| R-ID-4 | ID сущностей | ID видов S05 — `kind` (`+0x64`, 0x00–0x90); сущностей в мире пока нет | `EntityId` из seed + индекса спавна (как в ТЗ M0-00 §4.3); `kind` — только тип |
| R-SCALE | Масштаб моделей | меш S05 в единицах вокселя, мир — 1 блок = 1 ед. | доказать коэффициент блок↔воксель по коду Alpha (не подбирать на глаз); до доказательства — `PLACEHOLDER_NOT_ALPHA` в одном месте |
| R-TIME | Два источника времени | `PlayerMotor`, `AlphaClipPlayer`, `AlphaLocomotionDriver`, `AlphaAnimationController` тикают от `Time.deltaTime`; у Core игровых часов нет | игровые часы в Features; контроллер S05 в режиме `externalClock` |
| R-LIFE | Жизненный цикл | вся сцена строится в `Awake` бутстрапа; нет состояний «меню/загрузка/игра/выход» | машина состояний игры в M0-01C до подключения систем |
| R-SAVE | Схема сейва | v1 без миграций; не сохраняется назначение слотов; нет мира/сущностей | механизм `vN→vN+1` + тест «старый сейв» **до** первого расширения схемы (M0-01D) |
| R-DATA | Данные Alpha в сборке | рантайм читает `Application.dataPath`; ассеты Alpha нельзя коммитить | локальный импорт в кэш вне Git + один загрузчик; сборка без кэша выдаёт понятную ошибку |
| R-PROBE | Проверочный код в рантайме | `Alpha*Probe` пишут в `TransferOutput` | вынести в отдельную сборку `…AlphaModels.Verification` (не в игровую) |
| R-INPUT | Ввод | `UnityEngine.Input` в `PlayerMotor`, `PilotBootstrap`, `OrbitCameraController` | слой команд (M0-01C); выбор Input System — вопрос продюсеру |
| R-REG | Регрессии S02–S04 | приняты, 157+17 тестов | при переносе S05 в основной проект прогнать полный набор EditMode/PlayMode (эксклюзивный запуск) |
| R-STATIC | Статическое состояние | `PilotMaterials._fallback`, `AlphaRig._mpb` (кэши) | допустимо; проверить сброс при перезапуске мира без перезапуска процесса |

Дублей игровых систем не обнаружено (кроме второй орбитальной камеры просмотрщика, которая остаётся инструментом).
Циклических зависимостей нет.
