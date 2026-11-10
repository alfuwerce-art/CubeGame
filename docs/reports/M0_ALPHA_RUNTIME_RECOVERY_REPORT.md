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

---

## 5. Осталось (следующие шаги)

1. **R4 — недостающие гейты**: `WEAPON_MODEL_VISIBLE`, `KNOWN_NPC_MODEL_VISIBLE`,
   `KNOWN_LOOT_MODEL_VISIBLE`, `NO_PRIMITIVE_PLACEHOLDER_FOR_KNOWN_ASSET` (нужна сцена с NPC и лутом).
   Уже реализованы и PASS: `ALPHA_CONTENT_READY`, `PLAYER_MODEL_VISIBLE`, `PLAYER_NOT_PRIMITIVE`,
   `KNOWN_CREATURE_MODEL_VISIBLE`, `NO_MAGENTA`, `ANIMATION_RUNNING`, `COMBAT_ACTION_ANIMATION`.
2. **R5 — полный vertical slice**: подтвердить визуально реального NPC, деревья/растительность,
   звук, HUD/инвентарь/иконку и полный цикл `start→move→NPC→enemy→attack→death→loot→pickup→inventory→save→exit→load→continue`.
   Герой (с оружием), враг и анимации уже подтверждены. Требует **визуальной приёмки продюсера**.
   - **Растительность — точный blocker**: `D3ZonePopulation.cs:41` ставит `VegetationPerZone = 0` в
     генераторе `/6`; комментарий — F1A подтвердил, что W-18 scatter `0x5F56C0` выбирает шаблоны
     существ, а не растительность. Т.е. семантика размещения растительности `PARTIAL/TRANSLATED_FROM_CODE`,
     и просто поднять флаг нельзя — это исследовательская задача, а не конфиг-флип.
3. **R6** — масштабирование каталога (после R5).
4. **R7** — структуры/город/данж (отдельный conversion gap; сейчас только footprints).

## 6. Финальный статус

`READY_FOR_PRODUCER_VISUAL_REVIEW` по R0–R3 + Player-side гейтам R4 (7/7 PASS); R5 — `PARTIAL`
(герой с оружием, реальный враг, анимации, бой, террейн и no-magenta подтверждены в Windows Player;
не подтверждены визуально NPC, растительность, звук и полный цикл). `R6`/`R7` — `NOT_STARTED`.
