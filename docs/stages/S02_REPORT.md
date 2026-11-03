# S02-CORE-FOUNDATION — отчёт о выполнении первой партии

**Проект:** CubeGame (рабочее название). **Этап:** `S02-CORE-FOUNDATION` ([ТЗ](S02_CORE_FOUNDATION.md), [D43](../decisions/D43-close-s00-s02-core-foundation.md)).
**Ветка:** `stage/s02-core-foundation` (от `stage/s01-unity-pilot` @ `a8842783378bf2621e6a39a50e1f9ee2398cf245`).
**Дата прогона:** 2026-11-03, MSK. **Android-устройств, облачных сервисов и платных API не использовалось.**
**Статус:** `READY_FOR_REVIEW` — **не** `ACCEPTED`.
**Актуальные результаты (после REVIEW-FIX-01, 2026-11-03):** компиляция PASS; EditMode **170/170**, PlayMode **6/6**, failed=0. Подробности — §5a; таблица §1 ниже описывает первый прогон партии (132/132 и 5/5) и оставлена как история.

## 1. Итог первой партии до REVIEW-FIX-01 (история)

| # | Проверка | Результат | Артефакт |
|---|---|---|---|
| 0 | Unity Pilot S01 @ `a884278`: импорт/компиляция | **PASS** (exit 0, `Initialize engine version: 6000.3.27f2`) | `.unity-install/logs/1_project_import.log` |
| 0 | S01: EditMode | **PASS 10/10**, failed=0 | `.unity-install/results/editmode.xml` |
| 0 | S01: PlayMode | **PASS 2/2**, failed=0 | `.unity-install/results/playmode_s01.xml` |
| 0 | S01: сборка Windows-плеера и запуск со скриншотом | `NOT_RUN` в этом прогоне (`-SkipPlayerRun`); ранее — `REPORTED` агентом | — |
| 1 | Компиляция проекта с модулями S02 | **PASS** (exit 0, нет `error CS`/`warning CS`) | `.unity-install/logs/s02_import3.log` |
| 2 | EditMode: прогрессия | **PASS** (`ProgressionTests`, 45 кейсов) | `.unity-install/results/s02_editmode2.xml` |
| 3 | EditMode: характеристики персонажа | **PASS** (`CharacterStatsTests`, 22 кейса) | там же |
| 4 | EditMode: структура и статы предметов | **PASS** (`ItemStatsTests`, 42 кейса) | там же |
| 5 | Round-trip сохранения/загрузки | **PASS** (`PersistenceTests`, 13 кейсов, включая `SaveSurvivesRestartWithNewServiceInstance`) | там же |
| 6 | Повреждённое сохранение | **PASS** (текст, обрезка, подмена payload, версия из будущего) | там же |
| 7 | PlayMode smoke (сцена + новые модули) | **PASS 5/5**, failed=0 | `.unity-install/results/s02_playmode.xml` |
| 8 | Регрессия пилота | **PASS** — EditMode 10/10 (`ChunkMeshTests`, `TerrainDeterminismTests`), PlayMode 2/2 (`PilotPlayModeTests`) | оба XML выше |
| — | Пересборка базовой сцены `Assets/Scenes/Pilot.unity` | **PASS** — `[PilotScene] SCENE_SAVED` | `.unity-install/logs/s02_scene.log` |

**Всего тестов в прогоне S02:** EditMode **132/132 passed**, PlayMode **5/5 passed**, failed=0, skipped=0.

Логи и XML лежат в локальном `.unity-install/` (в `.gitignore`); в репозиторий не коммитятся, поэтому в отчёт выписаны фактические числа и пути.

## 2. Проверка существующего Unity Pilot (шаг 1 задания)

| Пункт задания | Фактический результат |
|---|---|
| Состояние Unity-проекта и структура каталогов | Проект в корне репозитория (`Assets/`, `Packages/`, `ProjectSettings/`); ассембли: `CubeGame.Core` (`noEngineReferences: true`), `CubeGame.Presentation`, `CubeGame.Editor`, `CubeGame.Tests.EditMode/PlayMode` — соответствует ADR-0001 |
| Совместимость с Unity `6000.3.27f2` | PASS: `ProjectVersion.txt` = `6000.3.27f2`, в логе импорта `Initialize engine version: 6000.3.27f2 (22188f149405)`; Unity 6.6 не открывалась |
| Компиляция | PASS, ошибок и предупреждений CS нет |
| EditMode-тесты | PASS **10/10** (совпадает с заявлением пилота) |
| PlayMode-тесты | PASS **2/2** (совпадает с заявлением пилота) |
| Базовая сцена | `Assets/Scenes/Pilot.unity` существует; пересборка через `PilotSceneBuilder.Build` → `SCENE_SAVED` |
| Существующая генерация | Регрессия подтверждает: детерминизм высот/мешей, 2×2 и 3×3 чанка с `MeshRenderer`+`MeshCollider`, персонаж встаёт на террейн |
| Соответствие ADR-0001 | Да: игровая логика без ссылок на UnityEngine, представление отдельно |

**Вывод:** критических ошибок в пилоте не обнаружено; новые модули построены на его проверенной основе. S01 остаётся `READY_FOR_REVIEW` — использование кода не означает приёмку пилота.

**Инструментальное наблюдение:** `tools/unity/run_pilot.ps1` (шаг тестов через Unity CLI `unity test`) после успешного завершения EditMode-теста не завершил процесс и ждал собственного таймаута, хотя Unity записала `editmode.xml` с `result=Passed`. Поэтому тесты S02 запускались напрямую `Unity.exe -batchmode -runTests -testPlatform <mode> -testResults <xml>` (тот же механизм, который CLI использует внутри — это видно в `2_tests_editmode_cli.err.log`). Скрипт не изменялся: это отдельная задача для следующей партии.

## 3. Архитектура и созданные модули

```
Assets/CubeGame/Core/            CubeGame.Core (noEngineReferences = true)
  Progression/AlphaMath.cs              кривые Alpha: curve_level05, levelFactor, starFactor,
                                        formHaste, powerFactor, cost(L), killXp
  Progression/ProgressionState.cs        уровень/XP, повышение уровней while-циклом, без капа
  Characters/CharacterClass.cs           классы 0…3, специализации, преобразование id
  Characters/CharacterStats.cs           calcMaxHealth, calcWeaponDamage (подтверждённая часть),
                                        флаги 0x7e&0x8 / 0x7e&0x10, HP существ со звёздами
  Characters/CharacterSheet.cs           чистое состояние персонажа: прогрессия + экипировка
  Items/ItemEnums.cs                     типы/подтипы/модификаторы по таблице Alpha
  Items/ItemInstance.cs                  экземпляр предмета + идентичность (как Item_equals)
  Items/ItemStatsCalculator.cs           урон/броня/блок/стоимость/редкость, math_pow2Mul
  Persistence/SaveGame.cs                модель сохранения (версия, персонаж, предметы) + валидатор
  Persistence/SaveContracts.cs           ISaveStore / ISaveSerializer / статусы
  Persistence/SaveService.cs             кадр файла, FNV-1a, версии, миграционная ветка
  Persistence/CharacterSaveMapper.cs     модель ↔ плоские структуры сохранения
Assets/CubeGame/Persistence/     CubeGame.Persistence (Unity-слой ввода-вывода)
  FileSaveStore.cs                       Application.persistentDataPath, атомарная запись (.tmp/.bak)
  JsonSaveSerializer.cs                  штатный JsonUtility
Assets/CubeGame/Presentation/Character/
  CharacterSheetPresenter.cs             минимальная связка со сценой; без геймплея
Assets/CubeGame/Tests/EditMode/         ProgressionTests, CharacterStatsTests, ItemStatsTests, PersistenceTests
Assets/CubeGame/Tests/PlayMode/         CoreFoundationPlayModeTests
```

**Соблюдение ADR-0001:** чистая логика (`Core`, без UnityEngine) → данные/модели → сохранения (Core-логика + Unity-IO) → Unity-представление → тесты. Новых пакетов не добавлено; сериализация — `JsonUtility`, путь — `persistentDataPath`, запись — `File.Replace`.

**Формат сохранения CubeGame (собственный, не Alpha v11):**

```
CUBEGAME_SAVE|<version>|<fnv1a64 hex>|<payload byte length>
<payload: JSON>
```

Проверяются: заголовок, длина полезной нагрузки, контрольная сумма, версия (новее — `UnsupportedVersion`, старее — `MigrationFailed`), поля через `SaveValidator`. Исключения не выходят в игровой код: возвращается `LoadStatus`.

## 4. Числовые референсы, на которых стоят тесты

| Что | Значения в тестах | Статус (D42) |
|---|---|---|
| `cost(L)` | 1→50, 2→97, 3→140, 4→180, 5→216, 10→360, 20→537, 50→760, 100→881, предел 1049 | `REFERENCE_VERIFIED` |
| `levelFactor(L)` | 1→1.0, 5→1.4142, 11→2.0, 21→2.8284, 51→4.4160, 101→5.6569, предел 8.0 | `REFERENCE_VERIFIED` |
| `starFactor(S)` | 0→1, 1→1.1892, 4→2, 8→4 | `REFERENCE_VERIFIED` |
| `maxHP` Warrior L1 + 8 экипировки | **268.0** (проверенный сейв Alpha) | `REFERENCE_VERIFIED` |
| HP по классам при L1 | 260 / 220 / 200 / 240 | `REFERENCE_VERIFIED` |
| Warrior+Guardian | ×1.25 | `PARTIAL` (вывод из `+0x140 = classIdx+1`) |
| Урон оружия | `base·levelFactor`, флаги `0x8`→`+lf·sf·2`, `0x10`→`+lf·sf·8` | `REFERENCE_VERIFIED` частично |
| Статы предметов | 6 контрольных точек level 1/10/50 × rarity 0/2/4 | `REFERENCE_VERIFIED` |

Множители типа атаки (`0x57 ×50`, `0x5b ×2`, `0x5d ×5`, `0x60 ×3`) **не реализованы**: их привязка к типам удара не восстановлена (OQ). Тест `AttackTypeMultipliersAreNotApplied` фиксирует это явно, чтобы никто не «додумал» формулу.

## 5. Найденные расхождения в референсах (исправлены в REVIEW-FIX-01)

1. **`alpha_curves_reference.py`, таблица `EXPECT_LEVEL_FACTOR_4DP`:** `L=101` содержало `5.6574` вместо `5.6569`, а `L=51` — `4.4160` вместо `4.4164`. Обе строки проходили только за счёт допуска `5e-4`: для `L=101` разность в float64 равна `4.999999999999723e-4 ≤ 5e-4`, то есть проверка стояла ровно на границе и не ловила ошибку округления. Исправлено в REVIEW-FIX-01: значения точные, допуск ужесточён до `1e-9`.
2. **`F_items.md §F2`, фраза «материальный множитель … в пределах ±5 %»:** формула `1 + (1 − (seed·8 mod 21)/20)` даёт 21 дискретное значение в диапазоне **1.0…2.0** (seed 0 → 2.0, seed 1 → 1.6). Формулировка исправлена, математика не менялась.

Оба пункта касались текста исследований и эталонных констант; код Alpha они не затрагивают.

## 5a. REVIEW-FIX-01 (2026-11-03) — исправления после ревью

| FIX | Что исправлено | Доказательство |
|---|---|---|
| **FIX-01** (CRITICAL) | Валидация сохранений проверяет полный набор идентификаторов предмета: тип (`ItemInstance.IsSupportedType`), диапазон подтипа (`int → byte` без потери данных), подтип оружия `1…0x14`, «подтип только у оружия», известные модификаторы, маску флагов `0x7e` (`0x8|0x10`), конечность `baseHealth`/`weaponBase`/`equipmentHealthBonus`, инвариант «опыт < стоимости уровня». Ошибка любого поля → `LoadStatus.Corrupted`, без исключений (`SaveValidator.TryValidate` ловит внутренние ошибки). `CharacterSaveMapper` отказывается работать с непроверенными данными и не теряет точность при приведении подтипа. `CharacterSheetPresenter.ApplySaveGame` сначала строит модель в локальных переменных и только потом заменяет состояние | EditMode: `PersistenceTests` 28 кейсов (было 13), включая `UnknownItemTypeIsRejectedAsCorrupted`, `SubtypeBeyondByteRangeIsRejectedInsteadOfTruncated`, `InvalidWeaponUpgradeFlagsAreRejected`, `NonFiniteCharacterValuesAreRejectedByValidator`, `EveryLoadedSaveCanBeMappedWithoutExceptions`; PlayMode: `InvalidSaveDataLeavesPresenterUnchanged` |
| **FIX-02** (HIGH) | `ProgressionState.AddExperience`: проверка переполнения `long`, предел одного начисления `MaxExperiencePerCall = 1e9` (не игровой кап), оценка числа повышений до цикла (защита от зависания на восстановленном абсурдном опыте), отказ при `level == int.MaxValue`, изменения применяются только в конце — при любой ошибке уровень и XP прежние | EditMode: `ProgressionTests` 51 кейс (было 45): `ExperienceNearLongMaxIsRejectedWithoutStateChange`, `HugeRestoredExperienceCannotHangOrPartiallyApply`, `LevelOverflowIsRejectedWithoutStateChange`, `MaximumLevelStillAcceptsExperienceBelowCost`, `LargeButValidExperienceIsAppliedQuickly` |
| **FIX-03** (HIGH) | Арифметика seed: расширение до `long` **до** умножения (`SeedMaterialIndex`), floor-mod как в Python-эталоне; прежний `(long)(seed << 3)` переполнялся в `int` и для `int.MaxValue` давал материал 2.4 вместо 1.6 | EditMode: `ItemStatsTests` 59 кейсов (было 42): `SeedMaterialIndexMatchesPythonReference` (8 seed-значений), `ArmorMaterialMultiplierMatchesPythonReference` (7 значений), `SeedMaterialIndexIsPeriodicAndNeverNegative`, `LargeSeedDoesNotOverflowIntShift`; сохранение/загрузка крайних seed — `ValidSaveWithExtremeSeedStillLoadsAndMaps` |
| **FIX-04** | Эталонная таблица и текст спецификации: `levelFactor(101) → 5.6569`, `levelFactor(51) → 4.4164`, допуск `_close` ужесточён до `1e-9` с объяснением прежнего «пограничного» PASS; `F_items.md`: «±5 %» → «21 значение в диапазоне 1.0…2.0». C# и Python используют одинаковые эталоны | `alpha_curves_reference.py --check` PASS, `alpha_item_stats_reference.py --check` PASS, `alpha_combat_reference.py --check` PASS |

**Статус исправлений:** FIX-01 — `DONE`; FIX-02 — `DONE`; FIX-03 — `DONE` по реализации и тестам, но **`PARTIAL` по достоверности** (точная разрядность операции `seed*8 mod 21` в Alpha для отрицательных/огромных seed не доказана; воспроизводится единственный воспроизводимый эталон — Python-скрипт); FIX-04 — `DONE`.

**Итог прогона REVIEW-FIX-01 (Unity 6000.3.27f2):** компиляция PASS (exit 0, без `error CS`/`warning CS`); EditMode **170/170 passed**, failed=0; PlayMode **6/6 passed**, failed=0; регрессия S01 внутри прогона — EditMode 10/10 (`ChunkMeshTests`, `TerrainDeterminismTests`), PlayMode 2/2 (`PilotPlayModeTests`). Артефакты: `.unity-install/results/rf01_editmode.xml`, `.unity-install/results/rf01_playmode.xml`, логи `.unity-install/logs/rf01_*.log`. Новых тестов: **+38 EditMode, +1 PlayMode**.

## 6. Ограничения и что осталось неподтверждённым

| Ограничение | Статус |
|---|---|
| Полноценный бой (попадания, блок, комбо, способности, урон по цели) | **не реализовано** — запрещено ТЗ партии |
| Множители типа атаки в уроне оружия | `UNKNOWN`, сознательно отсутствуют |
| Какая характеристика предмета даёт `+8` к HP в проверенном сейве | **OQ-25 `OPEN`**; `equipmentHealthBonus` передаётся явно, догадкой не заменяется |
| Семантика флагов `0x7e & 0x8` / `0x7e & 0x10` | **OQ-26 `OPEN`** |
| `specMult` 1.25 именно для Warrior+Secondary | `PARTIAL`, отдельного прогона Alpha не было |
| Движение/скорости | вне S02; до OQ-23 любые скорости — `PLACEHOLDER_NOT_ALPHA` |
| URP, Pipeline | не подключались |
| Сборка Windows-плеера в этом прогоне | `NOT_RUN` (в проверке S01 использован `-SkipPlayerRun`) |
| Приёмка S01 / S02 | `READY_FOR_REVIEW`, приёмку делает продюсер/ChatGPT |

## 7. Критерии завершения партии

- [x] Unity-проект компилируется без ошибок.
- [x] Базовые характеристики персонажа вычисляются по референсам (268 HP воспроизведён).
- [x] XP и уровень изменяются корректно (включая 4 уровня за один вызов).
- [x] Предметы представлены типизированными данными с вычисляемыми статами.
- [x] Сохранение переживает перезапуск (новый экземпляр сервиса и новый компонент).
- [x] Все обязательные автоматизированные тесты выполнены, результаты зафиксированы.
- [x] Существующая сцена Unity не сломана (регрессия + пересборка сцены).
- [x] Нет механик, выданных за точную Alpha (неизвестное помечено `UNKNOWN`/`PARTIAL`/`OPEN`).
- [x] Код опубликован в отдельной рабочей ветке GitHub.

## 8. Рекомендация по следующей партии

1. **Приёмка/ревью S02** по этому отчёту; затем отдельное решение о слиянии `stage/s02-core-foundation` (merge в `main` не выполнялся).
2. **Следующая партия (предложение, требует решения продюсера):** боевые формулы уровня S02+ — `calcWeaponDamage` полностью (типы атаки, OQ), митигация (броня/блок, OQ-02), но без AI и без визуальных эффектов; плюс закрытие OQ-25/OQ-26 статическим разбором (без новых запусков Alpha).
3. **Отдельно:** починить тестовый шаг `tools/unity/run_pilot.ps1` (CLI-обёртка не завершается после тестов) — чтобы прогоны не требовали ручного вмешательства.
4. **Отдельно:** поправить две неточности в тексте исследований (§5 этого отчёта).
