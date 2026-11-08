# M0-00B — доказательная матрица механик Alpha 2013 и пробелов интеграции Unity

**Статус: `RESEARCH_DRAFT / NOT_APPROVED`.** Это исследовательский документ к issue #5, а **не** ТЗ, **не** разрешение на
M0-01 и **не** приёмка S05B-2. Черновик ТЗ M0-00 (`docs/stages/M0_00_FULL_ALPHA_GAME_TZ.md`) не переписывается:
расхождения и предложения — в `docs/reports/M0_00B_RESEARCH_GAPS_REPORT.md`.

**Входной SHA (фиксировано):** ветка `review/s05b2-d69-acceptance-check` @ `27a0184`
(база результата S05B-2/D69 — `claude/s05b2-d69-alpha-transfer-implementation` @ `721d222`).
**Дата:** 2026-11-08. **Исполнитель:** DeepSeek Harness. **Метод:** только чтение документов, кода и данных;
Unity, Python-прогоны и оригинальная Alpha не запускались; `.scratch` не изменялся.

---

## 0. Как читать матрицу

**confidence** (достоверность Alpha-факта): `DIRECT` — прочитано в исполняемом коде/данных Alpha с адресом или
константой; `PARTIAL` — каркас есть, детали не доказаны; `UNKNOWN` — не восстановлено.
`DIRECT` **не означает** «механика готова к переносу»: например, константы движения `DIRECT`, а их масштаб — `UNKNOWN`.

**status** (что есть в CubeGame сейчас):

| status | Значение |
|---|---|
| `IMPLEMENTED_CORE` | чистая логика в `Assets/CubeGame/Core/**` без UnityEngine (`CubeGame.Core`, `noEngineReferences: true`); в сцене не используется |
| `IMPLEMENTED_PILOT` | работает только в пилотной сцене `Assets/Scenes/Pilot.unity` (или в изолированном проекте `.scratch`) |
| `TEST_ONLY` | MonoBehaviour/класс существует, но ни в одной сцене нет; единственный вызывающий — PlayMode-тест |
| `PARTIAL` | есть часть механики (формула/данные/адаптер), игровой сценарий собрать нельзя |
| `MISSING` | файлов нет (проверено отсутствием и нулевыми grep-совпадениями) |
| `N/A` | механика в Alpha отсутствует или осознанно не переносится |

**S05 dep** — зависимость от S05: `PIPE` (конвейер `tools/alpha_models/**` и его данные), `RUNTIME` (рантайм
`tools/alpha_models/transfer/unity/Runtime/**` — он **вне** `Assets/`, копируется в изолированные проекты), `—`.

**verification scenario** — как проверять после реализации (`GS-*` из M0-00 §6 или собственный сценарий).

**Факты о состоянии проекта (проверено чтением, 2026-11-08):** 60 `.cs` в `Assets/CubeGame` (15 тестовых), 6 `.asmdef`,
1 сцена; в `Pilot.unity` ровно два MonoBehaviour — `PilotBootstrap` и `OrbitCameraController`; `Packages/manifest.json`
не содержит Input System, AI Navigation (NavMesh), URP, Cinemachine, Addressables; сейв `CUBEGAME_SAVE` v1 хранит
только персонажа и список предметов (экипировка не сохраняется).

---

## 1. Сводка

| Домен | Строк | `IMPLEMENTED_CORE` | `IMPLEMENTED_PILOT` | `TEST_ONLY` | `PARTIAL` | `MISSING` | `N/A` |
|---|---|---|---|---|---|---|---|
| 1. Мир и генерация | 20 | 0 | 2 | 0 | 2 | 16 | 0 |
| 2. Герой и перемещение | 27 | 3 | 6 | 0 | 4 | 14 | 0 |
| 3. Бой | 37 | 9 | 0 | 0 | 13 | 14 | 1 |
| 4. Существа, NPC, AI | 30 | 0 | 0 | 0 | 6 | 23 | 1 |
| 5. Предметы и экономика | 18 | 2 | 0 | 0 | 7 | 9 | 0 |
| 6. Контент, UI, сохранения | 27 | 1 | 1 | 1 | 1 | 21 | 2 |
| **Итого** | 159 | 15 | 9 | 1 | 33 | 97 | 4 |

**Ключевой вывод по сводке:** весь существующий C# — библиотека расчётов и демонстрационный пилот. Ни одна строка с
`IMPLEMENTED_CORE` не вызывается из игрового цикла; строки `IMPLEMENTED_PILOT` относятся к сцене `Pilot.unity`
(фиксированная сетка 8×8 чанков, без стриминга, без NPC, без сохранения мира).

---

## 2. Домен 1 — Мир: seed, регионы, чанки, вода/пещеры, постройки, стриминг, сохранение изменений

| mechanic_id | Alpha source (файл/символ/адрес) | conf | CubeGame implementation | status | S05 dep | M0 substage | verification scenario | открытый вопрос / блокер |
|---|---|---|---|---|---|---|---|---|
| W-01 | мастер-сид → поля мира: `crt_stl.cpp:11431-11459` (`srand`, `rand()%100000` в `+0x800188`/`+0x800168..174`/таблицы) | DIRECT | NONE | MISSING | — | M0-02A | `GEN-DET-01`: один seed → одинаковый хеш чанка | полный список ~20 таблиц не выгружен |
| W-02 | три независимых RNG-потока: `gc:87971`, `WorldInfo.cpp:1531`, `gc:87676` | DIRECT | NONE | MISSING | — | M0-02A | `GEN-DET-01` | порядок вызовов в кадре не доказан; **B_world §B12: бит-в-бит не требуется** |
| W-03 | шум `perlinNoise2D_cosInterp` `004c0ef0` (value-noise + косинусная интерполяция) | DIRECT | `Core/Generation/PilotHeightField.cs` (4 октавы, `PLACEHOLDER_NOT_ALPHA`) | PARTIAL | — | M0-02A | `GEN-NOISE-01`: порт функции на эталонных узлах | C#-порт бит-совместимости не проверялся |
| W-04 | форма рельефа `generateBiomeContent` `005e4850` (`WorldInfo.cpp:1639-1729`), константы 432/4234/423432/54352/34432/8992/985/98584/234/60/8/0.4/1.25 | PARTIAL (порядок потерян) | `Core/Generation/PilotHeightField.cs` (4 октавы — **другая** формула) | IMPLEMENTED_PILOT | — | M0-02A | `GEN-SIL-01`: силуэт против кадров Alpha | порядок операций деградирован (потеряны `ftol`/float) |
| W-05 | биомы: `biome = FUN_005a4710(rx,rz)`; `variant = rand()%5` при `biome>4`; `orientation = rand()%4` | UNKNOWN (таблица) | NONE | MISSING | — | M0-02A | `GEN-BIOME-01`: набор биомов и правила перехода | **функция классификации биома не расшифрована; перечисления биомов нет ни в одном документе** |
| W-06 | климат-клетка `0x1C` (`+4` температура, `+8` влажность, `+0xC` высота, `+0x10` поверхность, плотность растительности 0.3…0.7, каменистость 0.4…1.0) | PARTIAL | NONE | MISSING | — | M0-02A | `GEN-CLIMATE-01` | семантика полей плотности/каменистости |
| W-07 | вода: per-256-клетка (`cell+0x10` поверхность, `+0x1C` мощность, `+0x18` материалы), `y<+0x10` → вода | PARTIAL (запись `+0x10` — UNKNOWN) | NONE | MISSING | — | M0-02C | `GEN-WATER-01` | **где вычисляется уровень — U3/OQ-14; глобального sea level нет → нужно решение продюсера (§B12.3)** |
| W-08 | пещеры: `roof = base+(noise(x*0.01+435,z*0.01+847)+1)*20`, маска `1−abs(n)⁴`, материал 6 при крутизне `>0.5` | PARTIAL | NONE | MISSING | — | M0-02C | `GEN-CAVE-01` | точные границы вырезания |
| W-09 | реки/дороги `World_generateWaterOrPathFeature` `005df960` | UNKNOWN (только вызов) | NONE | MISSING | — | M0-02C | `GEN-RIVER-01` | ширина/слияние/привязка к воде не описаны |
| W-10 | регион 64×64 блока, `Region` `0x15A28`, сетка 1024×1024, мир 65 536×65 536, «voxel column 8» | DIRECT | `Core/Generation/PilotWorldLayout.cs` (`ChunkSize = 32`, `ViewChunks = 8`, мин/макс высоты — `PLACEHOLDER_NOT_ALPHA`) | PARTIAL | — | M0-02B | `GEN-CHUNK-01`: границы чанков/регионов | размер чанка CubeGame ещё не утверждён (32 блока — пилотное решение) |
| W-11 | стриминг: держать 8 зон / освобождать плитки за 10 зон (≈512/640 блоков), `lib_fn_602160`, `unloadZone` `00602160` | PARTIAL | NONE (`PilotWorldView` строит фиксированную сетку один раз) | MISSING | — | M0-02B | `GS-02`, `GEN-STREAM-01` | **документы противоречат друг другу: `B_world` §B10 — порог 3/5 регионов, `C_streaming` §C2 — 8/10 зон** |
| W-12 | выгрузка зоны пишет blob `"discovered"` (`unloadZone` шаг 2) | DIRECT | NONE | MISSING | — | M0-02D | `GS-02` | формат `discovered` (OQ-06) |
| W-13 | дельта изменений вокселя: байт 3 `&0x80` «изменено игроком»; чтение региона `deserializeRegionChunk` `0044cdb0` | DIRECT (флаг) / PARTIAL (формат) | NONE | MISSING | — | M0-02D | `GS-02` | семантика записи региона не восстановлена |
| W-14 | карта 1024×1024 (тип тайла / «открыто»), `map-tile-*.cub`, ключ `"discovered"`, метки `(x,y)→wstring` | DIRECT (структура) / UNKNOWN (формат) | NONE | MISSING | — | M0-02D (данные) / M0-08A (UI) | `MAP-01` | формат `reg`/`tile`/`land` не расшифрован (OQ-06, L-U1) |
| W-15 | процедурные имена регионов (слоговые таблицы) | UNKNOWN (только упоминание) | NONE | MISSING | — | M0-08A | `MAP-02` | таблиц слогов в документах нет |
| W-16 | размещение структур `WorldInfo_placeStructure` `005f0ce0` (swap осей, footprint против 256-клетки, 4 поворота, скан уровня, RB-tree) | DIRECT (алгоритм) | NONE | MISSING | — | M0-07A | `GEN-PLACE-01` | правила «что и где ставить» не восстановлены |
| W-17 | дом: объект `0x74` Б, всегда 3×3×4, стиль `+0x60` 1…5, клетки по 12 Б, мебель `0x88d…0x8ef`, генератор `Chunk_generateObjects` `005af170` | DIRECT | NONE | MISSING | — | M0-07A | `CITY-01` | разложение 12-байтной клетки — U4 |
| W-18 | «что растёт» `scatterObjectsInArea` `005f56c0` — таблица «климат → объекты» ~660 строк | UNKNOWN (U6) | NONE | MISSING | — | M0-02C / M0-06B | `GEN-SCATTER-01` | **таблица не выгружена; без неё растительность и биомный спавн не воспроизводимы** |
| W-19 | меш/AO/sunlight: `ChunkBuffer::ctor_0` `0049d910`, AO `004c1510`, `VoxelChunk_propagateSunlight` `0059a0e0` | PARTIAL | `Core/Rendering/ChunkMeshBuilder.cs`, `Presentation/Rendering/PilotWorldView.cs` | IMPLEMENTED_PILOT | — | M0-02B | `GEN-MESH-01` | в Unity AO/свет заменяются штатными средствами (не переносить) |
| W-20 | позиция персонажа в сейве: тройка `int64` из `Creature+0x10/+0x18/+0x20` | PARTIAL (OQ-22: x/z и «позиция vs спавн» не доказаны) | NONE | MISSING | — | M0-01D | `SAVE-POS-01` | OQ-22 `PARTIAL / RUNTIME_BLOCKED` |

---

## 3. Домен 2 — Герой: создание, внешность, ввод/камера, перемещение, прогрессия

| mechanic_id | Alpha source | conf | CubeGame implementation | status | S05 dep | M0 substage | verification scenario | открытый вопрос / блокер |
|---|---|---|---|---|---|---|---|---|
| P-01 | расы: 8 (Human, Elf, Dwarf, Orc, Goblin, Lizard, Undead, Frogman) — `drawCharacterAppearancePanel` `00428e40` | DIRECT | NONE (`CharacterClass.cs` описывает только 4 класса) | MISSING | — | M0-03A | `GS-11` | — |
| P-02 | классы: 4 (Warrior/Ranger/Mage/Rogue), `+0x140 = classIdx+1` | DIRECT | `Core/Characters/CharacterClass.cs` | IMPLEMENTED_CORE | — | M0-03A | `GS-11` | — |
| P-03 | пол Male/Female (`+0x04` appearance) | DIRECT | NONE | MISSING | — | M0-03A | `GS-11` | — |
| P-04 | Face/Haircut + `styleId = baseStyle[race]+gender` (16 значений); диапазоны зависят от style id | DIRECT (правило) | NONE | MISSING | — | M0-03A | `GS-11` | полные таблицы id моделей head/hair (`0x4d4…0x535`) |
| P-05 | цвет волос: палитра 16 RGB, дефолт `FFC832` (`00428e40`, `CharCreation_reset_defaults`) | DIRECT | NONE (механизм tint — только в S05-пилоте) | PARTIAL | RUNTIME | M0-03A | `GS-11` | перенос tint-правила в игру не выполнен |
| P-06 | имя персонажа `NameInput_handle_key` `0043a0d0`, ≤255 байт | DIRECT | NONE | MISSING | — | M0-03A | `GS-11` | — |
| P-07 | применение выбора: `+0x64 = styleId`, `creature_generateAppearance(styleId, +0x78, appearance)` | DIRECT | `tools/alpha_models/universal/pipeline.py` (вне `Assets/`) | IMPLEMENTED_PILOT | PIPE/RUNTIME | M0-03A / M0-03C | `GEN-APPEAR-01` | смысл `+0x78` и 11 узлов appearance |
| P-08 | стартовый набор по классу (`start_game_mode` `0x47742e`) | DIRECT | NONE | MISSING | — | M0-04A | `GS-07` | — |
| P-09 | XP: `cost(L)=int((1−1/((L−1)·0.05+1))·1000+50)`, асимптота 1050 | DIRECT | `Core/Progression/AlphaMath.cs`, `ProgressionState.cs` | IMPLEMENTED_CORE | — | M0-03D | `GS-01` | кап уровня не доказан (D-U1) |
| P-10 | maxHP = `base(0x178)·levelFactor·starFactorOr2·classMult·specMult + equipmentBonuses` | DIRECT | `Core/Characters/CharacterStats.cs` | IMPLEMENTED_CORE | — | M0-03D | `STATS-01` | OQ-25 OPEN: HP от экипировки — отдельное явное поле |
| P-11 | `starFactor` игрока = 2.0; мобов `2^(0.25S)` | DIRECT | `CharacterStats` (игрок), звёзды мобов — NONE | PARTIAL | — | M0-03D | `STATS-02` | звёздная система мобов отсутствует |
| P-12 | специализации: Berserker/Guardian/Sniper/Scout/Fire/Water/Assassin/Ninja | DIRECT | `CharacterClass`/`CharacterSheet` (поле спец.), эффектов спец. нет | PARTIAL | — | M0-03D | `GS-11` | чем спец. меняет игру, кроме `specMult 1.25`, не восстановлено |
| P-13 | ранги навыков `i32[11] @0x1138` (Taming, Riding, Climbing, HangGliding, Swimming, Boat, Ability1…5) | DIRECT | NONE | MISSING | — | M0-03D | `SKILL-01` | — |
| P-14 | покупка/респек рангов за деньги: `computeQuestScore` `004df9c0`, `GC+0x160`, поле `+0x1304` | PARTIAL | NONE | MISSING | — | M0-03D | `SKILL-02` | источник права на прокачку — OQ-04 OPEN |
| P-15 | способности: 16 известных id (0x15,0x22,0x30,0x31,0x32,0x36,0x4f,0x56,0x58,0x60,0x61,0x63,0x64,0x65,0x66,0x67) | PARTIAL | NONE | MISSING | — | M0-05B | `ABILITY-01` | полная таблица id — OQ-03 / D-U3 |
| P-16 | ввод: 4 цифровых направления `GC+0x0b…0x0e`, две оси `+0x124/+0x128` (15.16, ÷32768), масштаб добавки 80 (8 при жёстком приземлении) | DIRECT (адреса) / UNKNOWN (клавиши) | `Presentation/Player/PlayerMotor.cs` (legacy `Input.GetKey`, WASD) | IMPLEMENTED_PILOT | — | M0-03B | `GS-01` | клавиши по умолчанию — U6; Input System не подключён |
| P-17 | камера: FOV 50°/30°, near/far 0.1/2000, pitch 0…180, yaw старт 180°, мышь `cameraSpeed·0.005·dt`, зум 0…14 (дефолт 5.0), сглаживание `1−0.99^dt_ms` | DIRECT | `Presentation/Camera/OrbitCameraController.cs` (Orbit, параметры не сверены) | IMPLEMENTED_PILOT | — | M0-03B | `CAM-01` | дистанция камеры от зума — U3/OQ-21 |
| P-18 | спринт ×2.0 (`+0x124&0x40`), стамина `+0x1194` (−0.0002/мс, реген +0.0001/мс), вторая шкала `+0x1190` | DIRECT | NONE | MISSING | — | M0-03B | `MOVE-STAM-01` | смысл второй шкалы (равновесие) |
| P-19 | рывок: импульс 40 + 5 вверх, 600 мс, цена 0.25 стамины | DIRECT | NONE | MISSING | — | M0-03B | `MOVE-DASH-01` | — |
| P-20 | прыжок | UNKNOWN | `PlayerMotor.JumpSpeed = 7f` (`PLACEHOLDER_NOT_ALPHA`) | IMPLEMENTED_PILOT | — | M0-03B | `MOVE-JUMP-01` | **констант прыжка нет ни в `I_movement`, ни в RESEARCH_COVERAGE — механика не исследована** |
| P-21 | шкала скоростей: база 6.0 → босс 8.0 → роль 5: 12.0 → игрок 20.0 → спринт ×2, кламп | DIRECT (константы) / UNKNOWN (масштаб) | `PlayerMotor` 5/8 (`PLACEHOLDER_NOT_ALPHA`) | IMPLEMENTED_PILOT | — | M0-03B | `MOVE-SPEED-01` | **OQ-23 OPEN: 1 ед. ≠ доказанные блоки/с** |
| P-22 | гравитация 30.0, планирование 3.0 | DIRECT | `PlayerMotor.Gravity = -24f` | IMPLEMENTED_PILOT | — | M0-03B | `MOVE-GRAV-01` | — |
| P-23 | коллизия: воксельная swept, шаг ≤1 ед., твёрдость `(blockType&0x1F)!=0 && !=2` | DIRECT (структура) | `CharacterController` (пилот) | PARTIAL | — | M0-03B | `MOVE-COLL-01` | step-up/slide/высота капсулы в Alpha не найдены; модель CubeGame — отдельное решение |
| P-24 | плавание: `SkillSwimming`=4, `@swimmingspeed`, `curve_diminishingHalf`, подъёмная сила 30·погружение | PARTIAL | NONE | MISSING | — | M0-03B | `MOVE-SWIM-01` | вход/выход из воды, дыхание не исследованы |
| P-25 | лазание: `SkillClimbing`=2, `@climbingpower`, расход стамины | PARTIAL | NONE | MISSING | — | M0-03B | `MOVE-CLIMB-01` | срыв/поверхности не исследованы |
| P-26 | дельтаплан: `SkillHangGliding`=3, предмет `HangGlider` (0x17/0), гравитация 3.0 | PARTIAL | NONE | MISSING | — | M0-03B | `MOVE-GLIDE-01` | числа кривой |
| P-27 | лодка (`SkillBoatDriving`=5, `Boat` 0x17/1) и езда (`SkillPetRiding`=1) | UNKNOWN | NONE | MISSING | — | M0-03B / M0-06E | `MOVE-BOAT-01` | механика не найдена (U7/OQ-08) |

---

## 4. Домен 3 — Бой: урон, митигация, попадания, тайминги, смерть, эффекты

| mechanic_id | Alpha source | conf | CubeGame implementation | status | S05 dep | M0 substage | verification scenario | открытый вопрос / блокер |
|---|---|---|---|---|---|---|---|---|
| C-01 | поля `Creature`: `+0x060` роль, `+0x068` id действия, `+0x16c` HP, `+0x170` ресурс, `+0x174` энергия блока, `+0x178/0x180/0x184` базы, `+0x190` уровень, `+0x1a8` звёзды | DIRECT | `Core/Characters/CharacterSheet.cs` (часть полей) | PARTIAL | — | M0-05A | `GS-05` | `+0x164/+0x168/+0x1d8` не подтверждены — не переносить |
| C-02 | роль/фракция `+0x060`: 0 игрок, 1 монстр, 3 союзник, 5 NPC, 6 пассивный | DIRECT (CQ-02) | NONE | MISSING | — | M0-06B | `GS-04` | — |
| C-03 | максимум HP = `stat_calcAttackDamage` `00444db0` | DIRECT | `CharacterStats` | IMPLEMENTED_CORE | — | M0-05A | `STATS-01` | имя функции в аттрибуции ошибочно (это HP, не урон) |
| C-04 | `levelFactor(L)=2^(curve_level05·3)` | DIRECT | `AlphaMath.LevelFactor` | IMPLEMENTED_CORE | — | M0-05A | `STATS-01` | — |
| C-05 | classMult 1.3/1.1/1.0/1.2, specMult 1.25 (Warrior+spec1), `starFactor` | DIRECT | `CharacterStats` | IMPLEMENTED_CORE | — | M0-05A | `STATS-02` | — |
| C-06 | урон оружия `stat_calcWeaponDamage` `0043dc60`: `base(0x180)·levelFactor` + апгрейды `0x7e&0x8/0x10`; множители id `0x57 ×50`, `0x5b ×2`, `0x5d ×5`, `0x60 ×3` | DIRECT (формула) / PARTIAL (точки применения) | `Core/Items/ItemStatsCalculator.cs`, `Core/Combat/CombatFormulas.cs` | PARTIAL | — | M0-05A / M0-05B | `DMG-01` | какая ветвь `applyMeleeHit` берёт величину напрямую — не доказано |
| C-07 | разброс урона `v = 1.25 − 0.5·rand()/32767` (0.75…1.25) | DIRECT | `CombatFormulas.Variance` (`MinVariance 0.75`, `MaxVariance 1.25`) | IMPLEMENTED_CORE | — | M0-05A | `DMG-02` | — |
| C-08 | сборка урона: обычная атака `abilityFactor·weaponDamage·(ratio²·5+base)`; исключения id `0x57/0x58/0x1e/0x1f/0x20/0x21`; элементный прок ×2 | DIRECT / PARTIAL (таблицы) | `CombatAttack` (есть `ItemWeaponDamageBonus`), `DamageResolver` | PARTIAL | — | M0-05B | `DMG-03` | табличные константы по id отсутствуют |
| C-09 | броня `stat_calcArmor` `0043cff0` (`base(0x184)·star·level` + флаг `0x7e&0x20` + слоты) | DIRECT | `EquipmentStats`, `EquipmentCombatAdapter.ComputeTargetArmor` | IMPLEMENTED_CORE | — | M0-05A | `ARMOR-01` | — |
| C-10 | митигация: `dmg = raw − blockFraction·armor`, `blockFraction = 1 − (+0x70/blockDuration)` | PARTIAL | `DamageMitigation.Apply` (proxy `max(0, raw − bf·armor)`) | IMPLEMENTED_CORE | — | M0-05A | `MIT-01` | **OQ-02 OPEN: порядок «блок→броня→HP» — гипотеза; `blockFraction` передаётся извне** |
| C-11 | энергия блока `+0x174` (может уходить в `−1.0`) | PARTIAL | NONE | MISSING | — | M0-05B | `BLOCK-01` | роль в отрицательной зоне |
| C-12 | шанс попадания `combat_rollHitChance` `004441a0`: `hit = rand()/32767 < chance`; штрафы материала оружия ×0.5/×0.3 | DIRECT (механизм) / UNKNOWN (база) | `Combat/HitResolver.cs` (`chance` — параметр вызывающего) | IMPLEMENTED_CORE | — | M0-05B | `HIT-01` | **OQ-01: базовая `chance` не восстановлена → в игре источник шанса отсутствует** |
| C-13 | блок: `combat_rollBlockSuccess` `0043e420`, порог `(1−1/((L−1)·0.05+1))·100+1`, `item_rarityScaled` 0…100, тип щита | PARTIAL | `CombatFormulas.BlockThreshold/RollBlock` | IMPLEMENTED_CORE | — | M0-05B | `BLOCK-02` | смысл `item_rarityScaled` не установлен |
| C-14 | критические удары | UNKNOWN (не найдено) | NONE | N/A | — | M0-05B | — | **в восстановленных формулах крита нет (0 совпадений в docs); если M0-05B планирует крит — это отклонение, требует решения** |
| C-15 | смерть: HP→0, `Creature_spawnDeathEffects` `0059b180`, сброс таймеров, обнуление HP пета | DIRECT / PARTIAL (эффекты) | `Core/Combat/HealthPool.cs` (только пул HP) | PARTIAL | — | M0-05D | `GS-06` | содержимое эффектов смерти не разобрано |
| C-16 | опыт за убийство `max(1, curve_level05(level+star)·20)`, ×20 босс, ×10 флаг `0x200`, 0 при `0x800` | PARTIAL (E_combat) / STRONGLY_SUPPORTED (J_quests) | `AlphaMath.KillExperience` (вызывается только из EditMode-теста) | PARTIAL | — | M0-05D | `GS-05` | **статус формулы расходится между E_combat §E7 и J_quests §J7 — нужен один** |
| C-17 | `xp_getNextLevelCost` `00444d60` | DIRECT | `AlphaMath.ExperienceCostForNextLevel` | IMPLEMENTED_CORE | — | M0-03D | `GS-01` | — |
| C-18 | кривые `curve_level05/diminishingN1/x3p1/x3p15/Half`, `ability_getPowerFactor` `0043ed60` | DIRECT / PARTIAL (`Half`) | `AlphaMath` (часть кривых) | PARTIAL | — | M0-05B | `CURVE-01` | `curve_diminishingHalf` прочитана не до конца |
| C-19 | ресурс `+0x170` (0…1) и цена `combat_getStaminaCost` `00444270` (100/600/0) | DIRECT / PARTIAL (таблицы) | NONE | MISSING | — | M0-05B | `RES-01` | полной таблицы id→стоимость нет |
| C-20 | доступность способности `ability_isUsable` `0x43E5A0` (ресурс ≥ цены, нет кулдауна `+0x139c`) | DIRECT (исполнено) | NONE | MISSING | — | M0-05B | `ABILITY-02` | GUI-гейты — PARTIAL |
| C-21 | серия/финишер `+0x70` (<5 замах, ≥5 финишер тип 8 / 20 000 мс), таймер `+0x74`, сброс >4000 мс | PARTIAL (OQ-24) | NONE | MISSING | — | M0-05B | `COMBO-01` | **OQ-24: то же поле делится на `blockDuration` — единая семантика не установлена** |
| C-22 | стан/равновесие: `+0x128`, `+0x130`, poise `+0x1190` (`damage·0.25 / 2^((L−1+star)·0.25)`), `0x68=='T'` → 3000 мс | PARTIAL | NONE | MISSING | — | M0-05B | `STUN-01` | флаги `0x7e`, роль `0x2000/0x4000` |
| C-23 | threat-map `+0x13a4` («атакующий, цель» → счётчик) + `0x11d0/0x11d4` | DIRECT (модель) | NONE | MISSING | — | M0-06C | `AI-AGGRO-01` | — |
| C-24 | ИИ-зелье при `HP < 0.25·MaxHP`, `state='P'`, прыжок в сторону | DIRECT | NONE | MISSING | — | M0-06C | `AI-POTION-01` | таблица расходников |
| C-25 | смерть/возрождение игрока: `Player_resetFull` `00446330`, `Player_resetState` `004465c0` | UNKNOWN | NONE | MISSING | — | M0-05D | `GS-06` | **правило возрождения (штраф, точка, таймер) не восстановлено → GS-06 в текущей формулировке невыполним** |
| C-26 | haste `combat_getEffectiveHaste` `00447700` (+`stat_calcHasteBonus` `004478b0`, бафф `0x0c`) | DIRECT / PARTIAL | `AlphaMath.FormHaste` (вызывается только из EditMode-теста) | PARTIAL | — | M0-05B | `HASTE-01` | `+0x70` внутри haste — OQ-24 |
| C-27 | фазы `base_ms/(haste·speed(+0x17c))`: `combat_getAttackWindup` `0x43caa0`, `Timing` `0043d1a0`, `ReloadFrames` `00447310` | DIRECT (формула) / PARTIAL (таблицы, `speed`) | S05: тайминги `RECONSTRUCTED_FROM_EVIDENCE` (`tools/alpha_models/universal/*`) | PARTIAL | PIPE | M0-05C | `TIMING-01` | `speed(+0x17c)` неизвестен → абсолютные мс не «игровые» |
| C-28 | окна удара используются позой: `Creature_drawModel` `0x4128f0` вызывает windup 106 / reload 46 / timing 45 | DIRECT (исполнено) | S05: клипы действий `EMULATED_ALPHA_CODE` (вне `Assets/`) | PARTIAL | RUNTIME | M0-05C | `ANIM-HIT-01` | тип оружия → тип способности не найдено |
| C-29 | `CombatBehavior_chooseAttack` `004456a0` (порог `timing−windup`, recover 50…5000 мс) | DIRECT | NONE | MISSING | — | M0-06C | `AI-CHOOSE-01` | полная таблица констант |
| C-30 | сглаживание позы (коэффициенты 0.01/0.04, первые ~50 мс зависят от предыдущей позы) | PARTIAL | S05 (`AlphaAnimationController`, вне `Assets/`) | PARTIAL | RUNTIME | M0-03C | `ANIM-SMOOTH-01` | в игре статус `UNITY_TRANSITION`, не Alpha |
| C-31 | lifecycle действия игрока: 57→67→58, `+0x128!=0` блокирует primary, idle-таймаут 10 000 мс, strike window ≠ конец (1382/492 мс) | DIRECT (helper/input-tail) | S05A-2 (`TEST_ADAPTER_ACTIVATION`), вне `Assets/` | PARTIAL | RUNTIME | M0-05B | `COMBO-02` | полный lifecycle 101 ID — PARTIAL |
| C-32 | dynamic parts: 22@271 мс, 23@53 мс, 26 hide/return 296/1579, 71 200/3200, 72 200/5200 | DIRECT (для входов) | S05: тайминги, но часть рига отсутствует | PARTIAL | RUNTIME | M0-06E | `DYN-01` | R-8 |
| C-33 | действия 51 (эффект дракона), 79 (`HELD_STATE`), 80 (consumable, cooldown 3000/10000) | 51 `BLOCKED` / 79 `HELD_STATE` / 80 `BLOCKED_CONTEXT` | S05: 51/80 не перенесены, 79 — статичная поза | PARTIAL | RUNTIME | M0-06E | `ACT-51-80` | R-3 / R-8 |
| C-34 | эффекты и снаряды (огонь 51, стрелы, камни, снаряды магов) — R-3 | UNKNOWN (отдельные рендереры `spawnEffectMesh`, `render_build_projectile_mesh`) | NONE | MISSING | — | M0-05C | `GS-05` | **BLOCKED: нет адаптера эффектов; в ТЗ M0-05C планируются заглушки** |
| C-35 | динамическая часть рига (камень 108 у големов/рокилинга, черепаха 72) — R-8 | BLOCKED | NONE | MISSING | RUNTIME | M0-06E | `DYN-02` | нужна скрытая часть рига + проигрыватель |
| C-36 | `RuneGiant 0x76` — ветка атаки управляет другим существом — R-9 | BLOCKED | NONE | MISSING | RUNTIME | M0-06E | `ACT-0x76` | нужен вход «список существ мира» |
| C-37 | оружейные действия NPC (23 кода: 1,5,14,19,22–27,30–33,38,44–46,55,57,59–61) — R-1/R-2 | DEFERRED_TO_M0 | NONE | MISSING | RUNTIME | M0-06B | `NPC-WEAPON-01` | значения класса/оружия NPC задаёт генерация мира — не исполнена (R-1) |

---

## 5. Домен 4 — Существа, NPC, AI, питомцы, спавн, поведение вне экрана

| mechanic_id | Alpha source | conf | CubeGame implementation | status | S05 dep | M0 substage | verification scenario | открытый вопрос / блокер |
|---|---|---|---|---|---|---|---|---|
| N-01 | один класс `Creature` (`0x1e60`, `ctor` `0043b690`) | DIRECT | `CharacterSheet` (только игрок) | PARTIAL | — | M0-06A | `ENTITY-01` | — |
| N-02 | роль/фракция `+0x060` (0/1/3/5/6) | DIRECT | NONE | MISSING | — | M0-06B | `GS-04` | — |
| N-03 | дерево поведений: 3 слота vtable (тик/клон/деструктор), `SequentialBehavior` = «ИЛИ», `RandomInteractionBehavior` — лист; сборки `[Combat, LookAtPlayer, WalkPath, RandomWalk]` | DIRECT | NONE | MISSING | — | M0-06C | `AI-TREE-01` | смысл служебных состояний `'O','j','I','V','W','Y','H','l','_'` |
| N-04 | тик ИИ: аккумулятор `+0x13c4`, порог 31 мс; при стане `+0x12C≥1` тик пропускается | DIRECT | NONE | MISSING | — | M0-06C | `AI-TICK-01` | — |
| N-05 | `RandomWalkBehavior`: создаётся при спавне, «поводок» `+0x1c0…0x1d4`; у фракции 6 не создаётся (`0x5360AE`) | DIRECT | NONE | MISSING | — | M0-06C | `AI-WALK-01` | частота/радиус RandomWalk не разобраны |
| N-06 | `CombatBehavior`: сброс `+0x6c>10000`, ранний выход при `+0x128!=0`, порог зелья 25 %, выбор цели/сканирование, дистанция `2r_self+r_target`, скорости 80/40, ≤10 итераций и ≤50 узлов пути | DIRECT (числа) | NONE | MISSING | — | M0-06C | `AI-COMBAT-01` | единицы 80/40 не переведены в блоки/с (OQ-23) |
| N-07 | затухание угрозы `×0.9`/тик, сброс агро >20 с, пересканирование 3 с | DIRECT | NONE | MISSING | — | M0-06C | `AI-AGGRO-02` | живой проверки нет |
| N-08 | AI-додж `rand()%10==0`, не босс, радиус <2, `+0x128=600` мс | DIRECT | NONE | MISSING | — | M0-06C | `AI-DODGE-01` | — |
| N-09 | питомцы: `CompanionBehavior` (владелец `+8/+0xc`), скорость 5/30, **телепорта нет**, призыв `0x5c` (фракция 3, лимит 2, HP=MaxHP) | DIRECT | NONE | MISSING | — | M0-06E | `PET-01` | — |
| N-10 | приручение (еда/предмет к существу) | UNKNOWN (OQ-08) | NONE | MISSING | — | M0-06E | `PET-02` | **функций tame/food нет; нужно решение продюсера** |
| N-11 | езда (состояние `'R'`, `+0x1020==0x14`, `+0x1021`) | UNKNOWN | NONE | MISSING | — | M0-06E | `PET-03` | функций «посадить/спешиться» нет |
| N-12 | реген фракции 5: `HP += MaxHP·0.10/с` | DIRECT | NONE | MISSING | — | M0-06C | `NPC-REGEN-01` | — |
| N-13 | расписание города: `SpawnLocationBehavior` (записи `0x20` с временем), `LookAtPlayerBehavior` (радиус 8) | DIRECT | NONE | MISSING | — | M0-07A | `CITY-02` | семантика списков `obj+0x48/0x54` |
| N-14 | патруль `WalkPathBehavior` (пауза `rand()%4000+2000`, ≤21 попытки) | DIRECT | NONE | MISSING | — | M0-07A | `CITY-03` | — |
| N-15 | день/ночь: `worldTime` вне `[21 600 000, 82 799 999]` (6:00–23:00) — другое расписание | DIRECT | NONE | MISSING | — | M0-07A | `CITY-04` | типы мебели (16/18/68) — гипотеза |
| N-16 | сон/отдых у объектов: `'S'/'T'`, реген 5 %/с | PARTIAL | NONE | MISSING | — | M0-07C | `NPC-REST-01` | условия входа не доказаны |
| N-17 | диалог NPC `interactNpc`, «innkeeper» тип `0x84`, гейт квеста: `abs(cellLevel − playerLevel) > 2` | DIRECT | NONE | MISSING | — | M0-07C / M0-07D | `GS-08` | гейт ±2 — кандидат, требует решения продюсера |
| N-18 | спавн: `World_generateNpcSpawnList` — 30 строк (`curve_inverse20p1`), запись `0x118` Б; звёзды 1 %/0.1 %/0.01 %, кламп 4 | DIRECT (вероятности) | NONE | MISSING | — | M0-06B | `SPAWN-01` | — |
| N-19 | связь спавн-таблицы с биомом/уровнем региона | PARTIAL (GAP) | NONE | MISSING | — | M0-06B | `SPAWN-02` | **не доказана; зависит от W-05/W-18** |
| N-20 | лимит живых мобов в регионе | UNKNOWN (не найден) | NONE | MISSING | — | M0-06D | `POP-01` | — |
| N-21 | поведение существ вне экрана: деспавн/активация по дистанции | UNKNOWN (**не исследовано**) | NONE | MISSING | — | M0-06D | `POP-02` | **в документах нет вообще: `C_streaming` описывает только зоны/плитки** |
| N-22 | запись спавна: kind/faction (`Server.exe` `0x535DEB`/`0x535DEE`); класс/спец копируются из записи (`0x535DF7`: `record+0x30/+0x31`); оружие читается из `creature+0xaa8/+0xaa9`, off-hand `+0x990/+0x991`; per-kind overrides `0x522719..0x52273C` | DIRECT (смещения) / PARTIAL (значения из генерации мира) | `tools/alpha_models/catalog/spawn_role_evidence.json` (role/faction), `trigger_catalog.py` (условия) | PARTIAL | PIPE | M0-06B | `SPAWN-03` | **R-1: какие значения класс/оружие назначает генерация мира — не исполнено; 23 оружейных действия заблокированы** |
| N-23 | пассивные объекты: 26 видов (`0x18`, `0x78…0x90`), фракция 6, без `RandomWalkBehavior` (`0x5360AE`) и таймера действий (`0x53E2A8`); режим `STATIC_OBJECT` | DIRECT (22 кодом / 4 правилом) | S05: `contract.py`, `AlphaEntityContract` (вне `Assets/`) | PARTIAL | RUNTIME | M0-06A | `GS-04` | 4 из 26 — правило продюсера/класс, не байты спавна |
| N-24 | растительные монстры `0x45–0x49` — обычные существа, действия сохранены | DIRECT | S05: `ACTIONS`-профиль 0x45 проверен | PARTIAL | RUNTIME | M0-06A | `GS-03` | живого наблюдения нет |
| N-25 | действия видов и их запуск: `TRIGGER_EMULATED_CONDITIONAL` (15 видов S05B-1), 37/40/42/43 у 14 видов (D69) | DIRECT (эмуляция) | S05: библиотеки клипов (вне `Assets/`) | PARTIAL | RUNTIME | M0-06C | `GS-03` | **игровой триггер `DEFERRED_TO_M0`: клип ≠ работающий ИИ** |
| N-26 | боссы: `creature_isBossType` `004445d0` → 10 видов (`0x65,0x6b,0x6c,0x6d,0x72,0x73,0x74,0x75,0x76,0x77`) | DIRECT | NONE | MISSING | — | M0-06E | `BOSS-01` | — |
| N-27 | «хозяин подземелья» `spawn+0x10E8 → creature+0x1d38` | PARTIAL | NONE | MISSING | — | M0-06E | `BOSS-02` | сколько «хозяев» и как выбирается — J-U2 |
| N-28 | фазы/спец-атаки боссов | UNKNOWN (фаз не найдено) | NONE | MISSING | — | M0-06E | `BOSS-03` | OQ-13 PARTIAL |
| N-29 | крылатые/водные виды (13 winged, водные ветки `creature_hasWaterAffinity` `0x444610`) | PARTIAL | S05: риги/клипы видов (вне `Assets/`) | PARTIAL | RUNTIME | M0-06A | `ENTITY-02` | поведение водных/летающих не восстановлено |
| N-30 | живая перепись: 6 видов (Арулон), 2 прежних захвата; остальные 148 — `LIVE_UNOBSERVED` | DIRECT (перепись) | — | N/A | PIPE | M0-06 приёмка | `LIVE-01` | **«есть в каталоге» ≠ «видели в игре»** |

---

## 6. Домен 5 — Предметы, инвентарь, экипировка, лут, крафт, торговля

| mechanic_id | Alpha source | conf | CubeGame implementation | status | S05 dep | M0 substage | verification scenario | открытый вопрос / блокер |
|---|---|---|---|---|---|---|---|---|
| I-01 | модель предмета: `+0x00` тип, `+0x01` подтип, `+0x04` seed, `+0x08` вариант, `+0x0c` редкость, `+0x0d` модификатор, `+0x0e` флаг, `+0x10` уровень, `+0x114` powerBonus, `+0x118` стек, `+0x11c` ингредиенты | DIRECT (PARTIAL `+0x08/+0x0e`) | `Core/Items/ItemInstance.cs` | IMPLEMENTED_CORE | — | M0-04A | `ITEM-01` | смысл `+0x08/+0x0e` |
| I-02 | статы `2^(curve_level05)·2^(rarity·0.25)·M_type·M_modifier`, `L = level+0.1·powerBonus` | DIRECT | `ItemStatsCalculator.cs` | IMPLEMENTED_CORE | — | M0-04A | `ITEM-02` | — |
| I-03 | редкость: 1 % → +1, 0.1 % → +1, 0.01 % → +1, кламп 4 | DIRECT | `ItemStatsCalculator.RollRarity` (вызывается только из EditMode-теста) | PARTIAL | — | M0-04A | `ITEM-03` | — |
| I-04 | типы предметов: 24 типа в Alpha (по D64, 1565 моделей) | PARTIAL | `Core/Items/ItemEnums.cs` — 8 типов | PARTIAL | PIPE | M0-04A | `ITEM-04` | полная таблица `(type,subtype)→имя` — F-U1 |
| I-05 | модификаторы `+0x0d` (0x01…0x1b) | UNKNOWN (OQ-09) | NONE | MISSING | — | M0-04A | `ITEM-05` | **смысл модификаторов не установлен** |
| I-06 | инвентарь: 4 контейнера × 50 слотов (шаг `0x11c`) | DIRECT | NONE | MISSING | — | M0-04B | `GS-07` | — |
| I-07 | стаки: лимит стака ≤49 (`quest_checkItemThreshold`) | PARTIAL | `ItemInstance.StackCount` (без лимита/правил) | PARTIAL | — | M0-04B | `ITEM-06` | правила Special/приманки |
| I-08 | экипировка: 13 слотов по `0x118` (`0x300…0x1020`) | DIRECT (слоты) / PARTIAL (UI↔смещение) | `Core/Equipment/EquipmentSlot.cs` — 6 слотов (MainHand, OffHand, Chest, Gloves, Boots, Shoulders) | PARTIAL | — | M0-04B | `GS-07` | **13 слотов Alpha против 6 в коде; кольца/амулет/лампа/Special отсутствуют (S04 это прямо исключил)** |
| I-09 | тип предмета → слот (8→амулет, 4→нагрудник, 6→перчатки, 5→обувь, 7→наплечники, 3→оружие/вторая рука, 9→кольца, `0x18`→лампа, `0x13`→Special) | DIRECT | `EquipmentLoadout` (6 типов) | PARTIAL | — | M0-04B | `ITEM-07` | слоты 0 и 11 не идентифицированы (D-U5) |
| I-10 | модель оружия/брони в руке: законы якорей всех 10 слотов `drawModel` (D64), слой оружия гуманоида реализован | DIRECT | S05: `AlphaRig`, `AlphaWeaponLocomotion` (вне `Assets/`) | PARTIAL | RUNTIME | M0-04B | `ITEM-08` | NPC-оружие — R-1/R-2 |
| I-11 | экипировка в сохранении: 13 слотов в блобе v11 | DIRECT | `Core/Persistence/SaveGame.cs` — **поля экипировки отсутствуют** (v1: персонаж + список предметов) | MISSING | — | M0-04D | `GS-13` | **прямое расхождение с M0-04D и GS-07** |
| I-12 | лут-таблицы `loot_generateItemTable` `005c8390`, `drop_getCountRange1` `00443f40`, `spawnItemAffixDrops` `0047fae0` | PARTIAL (OQ-11 OPEN) | NONE | MISSING | — | M0-04C | `GS-05` | **полных таблиц и шансов по мобам нет** |
| I-13 | сундуки: `rand()&7 == 0` → вид 6 / id `0x8f`, ровно 10 сгенерированных предметов | DIRECT | NONE | MISSING | — | M0-04C | `GS-16` | контейнер как объект (вместимость/открытие) не исследован |
| I-14 | крафт: рецепт = предмет типа 2 с `+0x11c…0x120`; `computeCraftPrices` `004a19d0` = «число комплектов» | PARTIAL | NONE (только тип `Formula` в enum) | MISSING | — | M0-04C | `GS-17` | **таблицы рецептов нет; правила профессий нет** |
| I-15 | верстак кастомизации оружия `drawWeaponCustomization` `00588500` | UNKNOWN (OQ-10) | NONE | MISSING | — | M0-04C | `GS-17` | **правила модификаторов не восстановлены** |
| I-16 | деньги `+0x1304`; за убийство не начисляются, только продажа (`item_valueByType` `004c76e0`) | DIRECT | NONE | MISSING | — | M0-04A | `GS-08` | — |
| I-17 | торговля: вкладка `Vendor`, цены `item_valueByType`/`Item_compute_value` | PARTIAL (наценки неизвестны) | NONE | MISSING | — | M0-07C | `GS-08` | **F-U5: buy/sell-наценки, ассортимент, ресток не восстановлены** |
| I-18 | предмет → модель/иконка: `item_icon_id` (24 типа, 1565 моделей, D64) | DIRECT | S05: каталог (вне `Assets/`) | PARTIAL | PIPE | M0-04B | `ITEM-09` | иконки UI — своя графика (K_assets §K4) |

---

## 7. Домен 6 — Контент, взаимодействия, UI, звук, сохранения

| mechanic_id | Alpha source | conf | CubeGame implementation | status | S05 dep | M0 substage | verification scenario | открытый вопрос / блокер |
|---|---|---|---|---|---|---|---|---|
| Q-01 | создание героя: экраны/поля `drawCharacterAppearancePanel` `00428e40`, `CharCreation_refresh_model` `0042c080` | DIRECT (поля) / UNKNOWN (раскладка UI) | NONE | MISSING | — | M0-03A (UI — M0-08A) | `GS-11` | Plasma UI не переносится (K_assets §K2) |
| Q-02 | меню/новый мир/загрузка/выход | нет спецификации (только структура `worlds.db`) | NONE | MISSING | — | M0-01B | `GS-01` | **UI-сценарии Alpha не каталогизированы (`VISUAL_CATALOG` — `DEFERRED`)** |
| Q-03 | смена сцен/жизненный цикл мира | — | NONE (`SceneManager` в рантайме отсутствует) | MISSING | — | M0-01B | `GS-01` | — |
| Q-04 | HUD (HP/XP/ресурс/подсказки) | нет спецификации | `Presentation/Hud/PilotDebugHud.cs` (IMGUI seed/координаты/FPS) | IMPLEMENTED_PILOT | — | M0-08A | `GS-05` | это отладочный оверлей, не HUD |
| Q-05 | инвентарь UI: вкладки Equipment/Weapons/Armor/Amulets/Cooking/Alchemy/Formulas/Vendor | DIRECT (перечень вкладок) | NONE | MISSING | — | M0-08A | `GS-07` | раскладка не каталогизирована |
| Q-06 | окно навыков (11 рангов, 16 id способностей) | PARTIAL | NONE | MISSING | — | M0-08A | `SKILL-01` | — |
| Q-07 | карта/мини-карта, маркеры, имена регионов | DIRECT (структура) / UNKNOWN (формат, имена) | NONE | MISSING | — | M0-08A | `MAP-01` | W-14/W-15 |
| Q-08 | журнал заданий | нет спецификации (модель целей — J_quests §J4) | NONE | MISSING | — | M0-08A | `QUEST-01` | — |
| Q-09 | звук: `data2.db` 93 `.wav`; известен один игровой звук `missioncomplete.wav` (id 0x1e) | DIRECT (инвентарь) / UNKNOWN (события) | NONE (`AudioSource` в коде 0) | MISSING | — | M0-08B | `AUDIO-01` | **маппинга «событие → звук» нет; звуки удара/смерти не привязаны** |
| Q-10 | UI-ресурсы `.plx` (5 файлов, тег `PlasmaGraphics`) | UNKNOWN (теги) | N/A — пересоздаётся в Unity | N/A | — | — | — | решение: `REPLACED_BY_UNITY` (K_assets §K2) |
| Q-11 | сохранение персонажа: блоб v11 63 753 Б (23 поля) | DIRECT | `Core/Persistence/SaveGame.cs` (`CUBEGAME_SAVE` v1), `CharacterSaveMapper`, `SaveService`, `Persistence/FileSaveStore`, `JsonSaveSerializer` | IMPLEMENTED_CORE | — | M0-01D / M0-03D | `GS-01` | формат CubeGame свой, не Alpha — отклонение зафиксировать |
| Q-12 | экипировка в сейве | DIRECT (13 слотов в блобе) | NONE (см. I-11) | MISSING | — | M0-04D | `GS-13` | — |
| Q-13 | сохранение мира: `world_<name>.db` (`time`, 64×`mission`, 64×`monster`), `map_<name>.db` (`reg`/`tile`/`land`/`discovered`) | DIRECT (ключи/размеры) / UNKNOWN (форматы `reg`/`tile`/`land`) | NONE | MISSING | — | M0-01D / M0-02D | `GS-01`, `GS-02` | **форматы не расшифрованы (OQ-06, L-U1); TZ проектирует свой формат — нужно явно записать отклонение** |
| Q-14 | миграции сохранений | нет в Alpha (тоже нет транзакций/CRC) | `SaveService` → `LoadStatus.MigrationFailed` (миграций нет) | PARTIAL | — | M0-01D | `GS-20` | — |
| Q-15 | город/дома: `Chunk_generateObjects` `005af170`, дома 3×3×4, мебель `0x88d…0x8ef` | DIRECT (габарит/генератор) / UNKNOWN (правила расстановки) | NONE | MISSING | — | M0-07A | `CITY-01` | W-16/W-17 |
| Q-16 | подземелья: комнаты 3×3×3 (шаг 7), L-коридоры `005c52a0`, клетка 2 Б, входы `overground/underground-dungeon01` | PARTIAL (структура) | NONE | MISSING | — | M0-07B | `GS-09` | размеры объёма (J-U1), свет/факелы (J-U8) |
| Q-17 | сундуки в подземелье (см. I-13) | DIRECT | NONE | MISSING | — | M0-07B | `GS-09` | — |
| Q-18 | задания: запись на клетку региона (`0x68` Б): тип `+0x34`, состояние `+0x41` (0/1/2), прогресс `+0x44`, требуемое `+0x48`; 13 типов | DIRECT (модель) / PARTIAL (условия) | NONE | MISSING | — | M0-07D | `GS-18` | **радиус активации, условия scenery/explored (J-U7), награда за цель (J-U6) — UNKNOWN** |
| Q-19 | хранение задания `mission<x>_<z>` 38 Б | PARTIAL (раскладка — гипотеза; найден только читатель) | NONE | MISSING | — | M0-07D | `GS-18` | **J-U4/J-U5; сохранение прогресса заданий не специфицировано** |
| Q-20 | invasion/gang цели (типы 7…13, счётчик до `+0x48`) | DIRECT (типы) / UNKNOWN (события) | NONE | MISSING | — | M0-07D | `EVENT-01` | «вторжений» как контента нет в документах |
| Q-21 | квестовые тексты: `Speech`/`dict_<lang>.xml`, шаблонизатор `QuestText::parseTemplate`, `srand(point.seed)` | DIRECT (грамматика) / PARTIAL (подстановки) | NONE | MISSING | — | M0-08A | `QUEST-02` | что подставляется в `#`/`$` |
| Q-22 | смерть/возрождение игрока | UNKNOWN (E10) | NONE | MISSING | — | M0-05D | `GS-06` | **C-25: правило неизвестно** |
| Q-23 | взаимодействия с объектами (мебель/кровати 16/18/68, «innkeeper» 0x84, сундук 0x8f) | PARTIAL (типы) / UNKNOWN (правила) | NONE | MISSING | — | M0-07C | `INTERACT-01` | смысл списков точек и типов объектов |
| Q-24 | время мира/сутки (`worldTime` границы 21 600 000…82 799 999) | DIRECT (числа) | NONE | MISSING | — | M0-01C / M0-07A | `TIME-01` | визуал суток/погоды не исследован |
| Q-25 | лагеря (camps) как контент | **не найдено** | NONE | N/A | — | M0-07B | — | **в Alpha описаны только типы целей invasion/gang; «лагерей» как контента в документах нет** |
| Q-26 | гейт квеста по уровню ±2 | DIRECT | NONE | MISSING | — | M0-07D | `QUEST-03` | кандидат на нашу механику — решение продюсера |
| Q-27 | (не Alpha-механика, а состояние CubeGame) игровая проводка боя/экипировки/листа персонажа | — | `Presentation/Combat/CombatDemo.cs`, `Presentation/Equipment/EquipmentCombatDemo.cs`, `Presentation/Character/CharacterSheetPresenter.cs` | TEST_ONLY | — | M0-05A / M0-04B | `GS-05`, `GS-07` | **эти MonoBehaviour не входят ни в одну сцену; единственный вызывающий — PlayMode-тест, поэтому `PlayMode 17/17` не доказывает наличие боя/экипировки в игре** |

---

## 8. Что `PASS_ORACLE_VERIFIED` **не** доказывает

Матрица выше намеренно отделяет «клип/поза существуют» от «механика работает в игровом мире». По действующим отчётам:

1. **851 клип действий D65** и **56 клипов 14 видов D69** — `EMULATED_ALPHA_CODE`, `live_check = NOT_CAPTURED`;
   игровой триггер в игре (`DEFERRED_TO_M0`) отсутствует: клип воспроизводится по запросу, ИИ его не запускает.
2. **15 видов S05B-1** — запуск доказан исполнением байтов ИИ (`TRIGGER_EMULATED_CONDITIONAL`), но в живом мире
   встречались только фракции 3/5, а фракция 5 никогда не враждебна игроку (`0x596CA0`) → боевого наблюдения нет.
3. **Пассивные 26 видов** — доказано отсутствие `RandomWalkBehavior`/таймера действий, но 4 из 26
   (`0x18` Egg, `0x81` FirTree — правило продюсера; `0x86` Sandstone, `0x8b` IceCrystal — класс объекта) не имеют
   прямых байтов спавна.
4. **Ходьба 156/156** — формула сверена с `drawModel`, но это формула отрисовки, а не «существо идёт по миру»:
   нет ни навигации, ни столкновений, ни спавна.
5. **`D69_VERIFY_RESULT=PASS 29/29 + 14/14`** относится к изолированному проекту `.scratch/d69/unity/D69Verify`,
   а не к игровому `Assets/`: в игре нет ни `AlphaRig`, ни `Animator` (проверено: в `Assets` 0 совпадений).

---

## 9. Источники (точные)

**Alpha-спецификации:** `docs/research/alpha2013/BEHAVIOR_SPECS/{B_world,C_streaming,D_character,E_combat,F_items,G_H_ai_npc_pets,I_movement,J_quests_dungeons,K_assets}.md`;
`docs/research/alpha2013/{OPEN_QUESTIONS,RESEARCH_COVERAGE,REFERENCE_ATLAS,DATA_AND_SAVE_SCHEMAS,NPC_ACTION_TRIGGERS,ALPHA_ACTION_LIFECYCLE,ANIMATION_EXTRACTION_RESEARCH}.md`.
**S05-результаты:** `docs/research/alpha2013/S05B2_ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG.md`,
`docs/research/alpha2013/S05B2_UNITY_TRANSFER_DISPOSITION_MATRIX.md`, `docs/reports/S05B2_TRANSFER_IMPLEMENTATION_REPORT.md`,
`docs/reports/S05B2_UNITY_TRANSFER_GAP_AUDIT.md`, `docs/reports/D65_FULL_CATALOG_RUN_REPORT.md`.
**Код CubeGame:** `Assets/CubeGame/**` (60 `.cs`, 6 `.asmdef`, сцена `Assets/Scenes/Pilot.unity`), `Packages/manifest.json`.
**Конвейер/рантайм S05:** `tools/alpha_models/**` (в частности `universal/contract.py`, `catalog/spawn_roles.py`,
`transfer/unity/Runtime/*.cs`). **Сохранности:** `Assets/CubeGame/Core/Persistence/SaveGame.cs` (`CurrentVersion = 1`).

---

**Ограничения этого документа.** Числа и статусы — по документам и коду на входном SHA; живой прогон Alpha не
выполнялся; Unity/Python не запускались. Строки `UNKNOWN` — это не «нет в игре», а «не доказано»; строки
`LIVE_UNOBSERVED` — не «отсутствует». Приёмку и приоритеты определяет продюсер; M0-01+ не открыт.
