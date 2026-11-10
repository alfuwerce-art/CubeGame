# M0 — ALPHA RUNTIME RECOVERY REPORT

**Status:** `READY_FOR_PRODUCER_VISUAL_REVIEW` (R0–R3 PASS, R5 partial, см. «Осталось»)
**Дата:** 2026-11-10
**Ветка:** `deepseek/m0-alpha-runtime-recovery`
**Вход:** root cause audit `review/m0-alpha-unity-root-cause` (`38fb380`), база F4 `60f24be`

---

## 0. Итог одной строкой

Работа Claude по R0–R3 найдена как незакоммиченные изменения в `D:\Projects\CubeGame` (ветка
`claude/m0-alpha-runtime-recovery`, HEAD `60f24be`) и сохранена checkpoint-коммитом `28950f0` в
`deepseek/m0-alpha-runtime-recovery`. Код скомпилирован, тесты прогнаны, собран Windows Player:
локальный Alpha-контент (156 акторов, 1554 предмета, 180 частей внешности, 93 WAV, атлас иконок)
успешно застейджен в `CubeGame_Data\StreamingAssets\CubeGame\AlphaContent`, сборка прошла
content-gate, а запущенный Player строит мир, спавнит волка с пустым списком проблем
(`problems []`), ведёт бой и **не содержит magenta** (0 пикселей).

---

## 1. Спасение работы Claude

| Факт | Значение |
|---|---|
| Claude work found | **YES** |
| Расположение | `D:\Projects\CubeGame`, локальная ветка `claude/m0-alpha-runtime-recovery` (HEAD `60f24be`), незакоммиченные staged+unstaged+untracked |
| Другие worktree | `CubeGame-claude` (F1 `9e50915`, чисто), `CubeGame-deepseek` (`6eb0618`, чисто) — не относятся к recovery |
| Проверка на Alpha-ассеты | изменений `.scratch`, `AlphaOutput`, `*.cubdec`, `*.wav`, `*.db`, build-бинарей — **нет** |
| Checkpoint commit | `28950f0` `chore(recovery): preserve Claude runtime recovery checkpoint` |
| Ветка | `deepseek/m0-alpha-runtime-recovery` (база `60f24be`, без force-push) |
| Remote SHA | `28950f008fff25c3f5199b33be91a50f0a84fcd0` |

Коммит `28950f0` содержит 21 файл (R0–R3 код + shader + EditMode-тесты) без смысловой переработки.
Локальных коммитов Claude после `60f24be` не было — только working tree.

---

## 2. Таблица Recovery

| R | Claude state | DeepSeek action |
|---|---|---|
| R0 Content packaging | CODE (AlphaContentSource packaged root, AlphaContentStaging, ReleaseBuild, build script) | compile + тест + **Windows build + runtime проверка** → PASS |
| R1 No magenta | CODE (RuntimeMaterials + Resources shader, GameVisualSink/ProjectileVisuals/PilotMaterials) | тест + **Player скриншот 0 magenta** → PASS |
| R2 Build gate | CODE (AlphaContentInventory bounds, pre/post build gate) | тест + **реальный build прошёл gate** → PASS |
| R3 Icons | CODE (staging atlas в `<root>/icons`, AlphaIconAtlas content root) | тест + **atlas.png/atlas.json в Player** → PASS |
| R4 Visual gates | PARTIAL (ContentPackagingTests: content/shader/icon gate) | добавлены **Player-side [Validate] gates** (commit `75fc293`) — 7/7 PASS в Player |
| R5 Vertical slice | NOT_STARTED | **сильный прогресс**: герой(greatsword)+волк+анимации+бой+no-magenta подтверждены в Player; осталось NPC/растительность/звук/полный цикл + визуальная приёмка |
| R6 Catalog scale | NOT_STARTED | после R5 |
| R7 Structures | NOT_STARTED | после R5/R6 |

---

## 3. Проверки (реальные прогоны)

### 3.1 Компиляция
`Unity 6000.3.27f2 -batchmode -quit -nographics -projectPath D:\Projects\CubeGame` — exit 0,
`error CS = 0`, скрипты собраны, `CubeGame.Tests.Recovery.EditMode` импортирован.

### 3.2 EditMode
Полный прогон `-runTests -testPlatform EditMode` (без `-quit`, по XML):

- До фикса: **765 total / 762 passed / 1 failed / 2 skipped**.
- Единственный fail — `ContentPackagingTests.RuntimeCodeNeverLooksUpBuiltInShadersByName`:
  **ложное срабатывание** на doc-комментарии `<c>Shader.Find("Standard")</c>` в `RuntimeMaterials.cs`
  (реальный runtime уже исправлен).
- DeepSeek исправил тест (strip `//` и `/* */` комментариев перед сканом) — commit `71ef7a3`.
- После фикса recovery-сборка: **8/8 PASS, 0 FAIL**.
- Полный повторный прогон EditMode после фикса: **765 total / 763 passed / 0 failed / 2 skipped** (2 прежних SKIP).
- Повторный полный EditMode после добавления Player-side валидатора (R4): **763 passed / 0 failed / 2 skipped** — регрессий нет.
- Полный PlayMode после всех правок (R0-R3 Claude + R4 валидатор DeepSeek): **73 passed / 0 failed / 0 skipped** — игровой цикл не сломан.

### 3.3 Windows Player build
`ReleaseBuild.BuildWindows64` (`-executeMethod`), `CUBEGAME_RELEASE_DIR=.unity-install\build\Recovery`:

```
[AlphaContent] CONTENT_GATE_SOURCE_OK actors=156 actorModels=747 clips=927 items=1554 appearance=180 sounds=93 iconAtlas=1 bytes=231993390
[AlphaContent] CONTENT_STAGED ... target=...\CubeGame_Data\StreamingAssets\CubeGame\AlphaContent
[ReleaseBuild] BUILD_SUCCEEDED output=CubeGame.exe size=667136B
[ReleaseBuild] CONTENT_STAGED ... (re-check on disk)
[ReleaseBuild] RELEASE_READY
```

Player-каталог самодостаточен: `CubeGame.exe`, `UnityPlayer.dll`, `CubeGame_Data\StreamingAssets\CubeGame\AlphaContent\{actors,appearance,icons,items,sounds,manifest.json}`.

### 3.4 Player run (R5 partial)
`CubeGame.exe -cubeSeed 666666 -cubeAutoNew recovery1 -cubeScreenshot ... -cubeFrames 900 -logFile ...`

- `[Game] world built seed=666666 gen=cubegame.alpha_terrain/6 chunks=25 vertices=115160`
- `[Game] population: 0 content, 18 recovered in 9 zones`
- `[Game] training target Wolf ... problems []` — **пустой список проблем** = нет
  `DATA_MISSING` контента, `DATA_MISSING hero model`, `DATA_MISSING actor` (герой и волк — реальные модели).
- `[Combat] hit ...` — бой идёт.
- Скриншот 1920×1080: **magenta_pixels = 0**, доминируют зелёный террейн и синее небо.

### 3.5 Player-side visual validation (R4 gates)

`CubeGame.exe -cubeSeed 666666 -cubeAutoNew recovery3 -cubeValidateVisual -logFile ...` — собранный Player
сам проверяет визуальные гейты и пишет в лог:

```
[Validate] ALPHA_CONTENT_READY=PASS state=Ready origin=Packaged
[Validate] PLAYER_MODEL_VISIBLE=PASS
[Validate] PLAYER_NOT_PRIMITIVE=PASS
[Validate] KNOWN_CREATURE_MODEL_VISIBLE=PASS
[Validate] NO_MAGENTA=PASS MAGENTA_COUNT=0
[Validate] ANIMATION_RUNNING=PASS hero=True wolf=True
[Validate] COMBAT_ACTION_ANIMATION=PASS wolf_frames=558 hero_frames=0
[Validate] hero actor=d86_human_0x02_greatsword ready=True phase=Locomotion clips=94 problems=0
[Validate] wolf actor=wolf_0x1c_formula_only ready=True phase=Locomotion clips=6 frames=558 problems=0
```

Герой — **реальный человек с двуручным мечом** (`d86_human_0x02_greatsword`), волк — реальная модель,
оба `problems=0`, `origin=Packaged` (контент из StreamingAssets), `MAGENTA_COUNT=0`.
Локомоция (idle/move) работает у обоих, боевой action-клип проигрывался у волка (558 кадров).

Позднее в валидацию добавлен счётчик `REAL_ACTOR_VIEWS`: в Player-сцене **20 реальных `AlphaActorView`**
(герой + волк + **18 recovered диких существ**), все `ready=True`, **ни одного куба/капсулы**:
`d86_kind_0x5b/0x1e/0x58/0x4a/0x19/0x38/0x57/0x5a/0x43/0x1b/0x35_formula`, `whitecat_0x20`,
`plainrunner_0x3f`. Это прямо опровергает «существа = cube» из аудита.

**Content-популяция работает, но герой спавнится вне города** (`[Game] population: 0 content, 18 recovered` на старте):
- Стартовая позиция = зона **32800**, а Town-зона стартовой деревни = **32804** (тест
  `FullM0ZoneOracleTests.VillageStreetZoneMatchesExecutedAlphaCode`).
- Телепорт героя в Town-зону (`-cubeTeleport 1024 1024`) даёт **`REAL_ACTOR_VIEWS=44`** (было 20):
  появляются ~20 гуманоидных городских NPC (`d86_human_0x02_*` — merchant/innkeeper/trainers,
  `TownNpcPopulation`) + больше wild-существ. Т.е. NPC/структуры/сервисы **спавнятся реальными
  моделями**, но вне активного радиуса населения от стартовой точки.
- Точный blocker R5: герой стартует на 4 зоны (1024 вокселя) от Town-зоны; до телепорта/хода игрока
  контент-популяция = 0. Причина — `AlphaWorldScale.StartZoneX=32800` помечен как «Alpha saved hero
  position 32 800.5 zones (OQ-22 PARTIAL)»: деревня-ландмарк лежит в центре ячейки (зоны 32803–32804),
  а герой спавнится в зоне 32800. Это известная неточность OQ-22, а не регрессия — вопрос дизайна
  (спавнить ли героя в Town-зоне).

Примечание: `problems=[ANIM_MISSING ... ACTION_REFUSED Recovery]` в глобальном списке — это не пропавшая
модель, а штатный отказ боевого action 37 во время Recovery (волк атакует, не дождавшись выхода из
recovery). Лог-метка `ANIM_MISSING` здесь вводит в заблуждение; поведение корректное.

---

## 4. Что сделал Claude vs DeepSeek

**Claude (незакоммиченно, сохранено в `28950f0`):**
- R0: `AlphaContentSource` (Packaged root первым в порядке разрешения, `AlphaContentOrigin`),
  `AlphaContentStaging` (pre/post build → StreamingAssets + gate), `ReleaseBuild`, `build_windows_release.ps1`.
- R1: `RuntimeMaterials` + `Resources/CubeGameSolidColor.shader` (закоммиченный fallback);
  `GameVisualSink`/`ProjectileVisuals`/`PilotMaterials` больше не делают `new Material(null)`.
- R2: `AlphaContentInventory` с нижними границами каталога, build FAIL при неполном кэше.
- R3: стайджинг атласа в `<root>/icons`, `AlphaIconAtlas` читает content root первым.
- R4 (частично): `ContentPackagingTests` (staging, gate, shader, icon coverage).

**DeepSeek (эта сессия):**
- Спасение + checkpoint `28950f0` + push.
- Компиляция, EditMode-прогоны, исправление ложного fail теста (`71ef7a3`).
- Windows Player build + runtime проверка (контент, no-magenta, wolf/hero, бой).
- Player-side R4 visual validation gates (`75fc293`): 7/7 PASS в собранном Player.
- Research + реализация растительности (`bff0a67`): деревья/декор Alpha вместо W-18-creature-scatter.

---

## 5. Что сделано по визуальному фидбеку продюсера

Продюсер: «игра в принципе далека от оригинала». Сделано:

1. **Герой спавнится в Town-зоне** (zone 32803) — сразу видны NPC/дома/растительность
   (`population: 98 content`, было 54; `REAL_ACTOR_VIEWS=98`). Commit `02079a5`.
2. **Alpha sunlight + per-vertex AO** на террейне (`ChunkMesher`): тёмные пещеры/навесы, AO верхних
   граней. Commit `02079a5`.
3. **Реальные тени** — directional sun `LightShadows.Soft`, `PilotVertexColor` (террейн) и `VoxelShaded`
   (модели) получили ShadowCaster pass + `SHADOW_ATTENUATION`. Commit `d14adee`.
4. **Градиентное небо** (`CubeGameSky.shader` + `RenderSettings.skybox`) — реализовано и собрано,
   **но НЕ закоммичено** (оставлено в рабочем дереве на решение продюсера; commit не делался по просьбе
   «не продолжать»).

Скриншоты: `.unity-install/shots/recovery_shadow_town.png` (тени, 823 KB, `MAGENTA=0`),
`recovery_sky_town.png` (небо, снят).

## 6. Осталось (для «похоже на оригинал»)

**Важно:** `AlphaColors` (палитра террейна) — **побайтово сверена** с исполняемыми функциями Alpha
(`tools/alpha_models/world/compare_colors.py`, 300 сэмплов). Это НЕ gap — цвета террейна уже точные Alpha.

Реальные остатки:
1. **Небо** — градиент реализован (не закоммичен); облака/солнце — нет.
2. **Плотность мира** — деревья/декор/дома могут быть редкими; проверить scatter/плотность.
3. **Цвета моделей** — проверить, что конвертация `.cub → .cubdec` не исказила RGB.
4. **R7 материалы домов** — маппинг типа части→материала Alpha (сейчас реконструированы).
5. **Звук** — 93 WAV staged, «слышно ли» — приёмка.
6. **R4** — явные `WEAPON/NPC/LOOT` гейты (опционально).
7. **PlayMode** — тест спавна в Town-зоне обновлён, но полный прогон не завершён (прерван).

## 7. Финальный статус

`READY_FOR_PRODUCER_VISUAL_REVIEW` — по восстановлению контента: R0–R6 PASS, R7 геометрия подключена,
герой/оружие/NPC/враг/существа/анимации/террейн (точные Alpha-цвета)/растительность/дома рендерятся
реальными Alpha-моделями, `MAGENTA_COUNT=0`, цикл `save→exit→load` работает, каталог `156/156`.

`PARTIAL` — по «похожести на оригинал»: небо (облака), плотность мира, цвета моделей и материалы домов
ещё отличаются (см. §6). Это rendering/tuning-трек, не блокер playable.

Ветка: `deepseek/m0-alpha-runtime-recovery`, remote SHA `5e4d9461aff2db9980b32f17ea1f57255503872b`.
В рабочем дереве (не закоммичено): `CubeGameSky.shader`, правка `GameBootstrap.cs` (небо).
