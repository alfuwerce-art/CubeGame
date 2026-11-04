# `tools/alpha_models/character` — сборка персонажа и анимация (S05A-1)

Код изолированного Unity-пилота `S05A-1` ([D49](../../../docs/decisions/D49-s05a1-authorized.md)). Отчёт с числами и ограничениями — [CHARACTER_ASSEMBLY_ANIMATION_PILOT.md](../../../docs/research/alpha2013/CHARACTER_ASSEMBLY_ANIMATION_PILOT.md).

| Файл | Назначение |
|---|---|
| `CharacterPilot.cs` | Editor-скрипт: сборка из частей по `assembly.json`, валидаторы (иерархия, размещение, ориентация, крепление головы, гнездо оружия, движение, замыкание цикла), самотесты, снимки, отчёты |
| `CharacterPilotFixes.cs` | S05A-1-REVIEW-FIX-01: двухпроходный расчёт координат (FIX-01), размещение по исходной сетке + проверка кожных вокселей (FIX-02), независимая проверка `Transform.parent` (FIX-03), выгрузка геометрии для кросс-проверки с Python |
| `ProceduralCharacterAnimator.cs` | технический цикл `technical_walk_NOT_ALPHA` (детерминированный `Sample(t)` + `Update()` в PlayMode) |
| `PlayModeSampler.cs` | 40 реальных кадров PlayMode: движение частей + стабильность меча в кисти + (режим атаки) остаток хвата по каждому кадру; **единственный** писатель `PilotOutput/playmode.txt` / `playmode.json`; `RequiredFrames` (40) отделён от `FrameLimit`, в отчёт пишется `session` |
| `CharacterPilotPlayMode.cs` | точка входа PlayMode: предполётные проверки (сборка, 8 узлов, аниматор), жизненный цикл `OnFinished → isPlaying=false → EnteredEditMode → отложенный Exit`, журнал событий и `ValidateEventOrder`, отчёты FAIL/EXCEPTION. С S05A-2 отчёт жизненного цикла не перезаписывает отчёт сэмплера |
| `CharacterPoseAdapter.cs` | S05A-2: Unity-адаптер поз (матрицы частей 4x4 / локальные TRS / процедурные кривые `t -> TRS`) с валидацией, провенансом и запретом выдавать `TEST_DATA_NOT_ALPHA` за `ALPHA_DERIVED` |
| `CharacterPilotPoseAdapterSelfTests.cs` | S05A-2: самотесты адаптера поз (ручные числа, round trip 1e-4, отказы NaN/inf/сингулярной матрицы, охранник провенанса). Данные — только `TEST_DATA_NOT_ALPHA` |
| `CharacterPilotPlayModeReportSelfTests.cs` | S05A-2: самотесты разделения отчётов (проблема сэмплера обязана сохраняться в отчёте жизненного цикла и в итоговом вердикте) |
| `assembly.sample.json` | образец спеки сборки: имена частей, offsets в пространстве персонажа, статусы достоверности (без данных моделей) |
| `VertexColorUnlit.shader` | unlit-шейдер vertex colors (общий с S05A-0) |

## Правила, важные для повторного запуска

1. **Пивот части — в суставе.** Мировая точка пивота = `frameOffset + joint`; вложенная часть позиционируется относительно **пивота** родителя. Ошибка ловится валидатором `world min != frameOffset + sourceMin`.
2. **Размещение — по исходной воксельной сетке (FIX-02).** Вершины меша остаются в координатах сетки; заполненная область сохраняет смещение `sourceMin` внутри неё; `frameOffset` — начало сетки в пространстве персонажа. Голова и волосы имеют один и тот же `frameOffset`, поэтому их взаимное положение берётся из данных, а не из совмещения bounding boxes. Проверяется `ValidateSharedFrame` и `ValidateSkinAlignment` (skin-воксели волос обязаны попадать в кожу головы, ≥ 99%).
3. **Координаты считаются в два прохода (FIX-01).** Сначала границы видимых вокселей (с маской маркеров), потом вершины. Копия старого однопроходного алгоритма оставлена только в самотестах, чтобы дефект оставался воспроизводимым.
4. **Иерархия проверяется по сцене (FIX-03).** `ValidateHierarchyFromScene` читает реальные `Transform.parent`, игнорирует технический узел `mesh` и сравнивает набор/подчинение узлов `part_*` со спекой.
5. **Маркеры скрываются только по явному указанию цветов**: `cub_to_obj.py --exclude-markers green,blue`. По умолчанию геометрия не меняется; семантика маркеров не присваивается автоматически (см. `cub_markers.py`).
6. **Ориентация:** собственный `Mesh` строится напрямую из вокселей (без `ModelImporter`), поэтому зеркалирование OBJ по X из S05A-0 здесь не возникает; проверка `mesh min == grid min` и `>= 0` это фиксирует.
7. **PlayMode:** в проекте отключается domain reload (`EnterPlayModeOptions.DisableDomainReload`), иначе статические колбэки сэмплера теряются при входе в Play.
8. **Данные пилота** (`*.cubdec`, `markers.json`, PNG, отчёты) остаются локально в `.scratch/` и в Git не попадают. Единственное исключение — фикстура кросс-проверки `tests/fixtures/unity_geometry_dump.json`: она собрана из **синтетических** моделей и ассетов Alpha не содержит.
9. **Позы — только через адаптер и только с явным провенансом (S05A-2).** Матрицы частей (как у костей Alpha), локальные TRS и процедурные кривые входят через `CharacterPoseAdapter` (раздельные методы), поза пишется только в свои части, некорректный ввод (NaN/inf, сингулярный родитель, неправдоподобный масштаб, сдвиг/shear, зеркальный масштаб) сообщается именованной проблемой и не применяется. `TEST_DATA_NOT_ALPHA` нельзя выдать за `ALPHA_DERIVED`; снимок самотестов `PilotOutput/pose_adapter_selftest.json` помечен `TEST_DATA_NOT_ALPHA`. Реальные матрицы Alpha в пилот пока не перенесены (см. `ANIMATION_EXTRACTION_RESEARCH.md`).

## Запуск

```powershell
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit -projectPath <проект> -executeMethod CharacterPilotVerifier.Run -logFile run.log
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit -projectPath <проект> -executeMethod CharacterPilotVerifier.RunSelfTests -logFile selftest.log
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -projectPath <проект> -executeMethod CharacterPilotVerifier.RunPlayMode -logFile playmode.log
```

Ожидаемые маркеры: `CHARACTER_RESULT=PASS`, `SELFTEST_RESULT=PASS`, `PLAYMODE_RESULT=PASS`, `PLAYMODE_LIFECYCLE_RESULT=PASS` и для PlayMode — код завершения процесса `0`. Самотесты на 2026-11-05: `cases=116` (`base_cases=23`, `fix_cases=59`, `pose_adapter_cases=22`, `report_cases=12`); до S05A-2 было `cases=82`.

Тестовые хуки командной строки (только для негативных прогонов, в обычной работе не используются):

| Хук | Действие |
|---|---|
| `-pilotFrames N` | сэмплер соберёт `N` кадров вместо 40; требование остаётся 40, поэтому `N < 40` даёт `FAIL` |
| `-pilotSkipAnimator` | `BuildCharacter` не добавляет `ProceduralCharacterAnimator`; PlayMode обязан отказаться |
| `-pilotUnbindHair` | после сборки аниматор привязывается без волос: `IsBound == false`, предполётная проверка даёт `FAIL` |
| `-pilotDoubleFinish` | сэмплер сообщает о завершении дважды: должен сработать `SAMPLER_FINISHED_DUPLICATE` |
| `-pilotDoubleRegister` | обработчик `playModeStateChanged` регистрируется дважды: `STATE_HANDLER_DUPLICATE` |
| `-pilotExitBeforeEditMode` | попытка выйти до `EnteredEditMode`: отказ и `FAIL` |
| `-pilotForceExitEarly` | PlayMode останавливается до окончания сэмплирования: `FAIL` с диагностикой |
| `-pilotAttack` | PlayMode проверяет двуручную атаку (`two_handed_attack_NOT_ALPHA`) вместо цикла ходьбы |

`RunPlayMode` не запускает PlayMode, если сборка неполная, отсутствует часть или аниматор: пишет `PLAYMODE_LIFECYCLE_RESULT=FAIL` (и `PLAYMODE_VERDICT=FAIL`) с причинами, **не создавая** `playmode.txt`, и завершается ненулевым кодом.

### Отчёты PlayMode (S05A-2)

| Файл | Владелец | Содержимое |
|---|---|---|
| `playmode.txt` / `playmode.json` | **только сэмплер** | вердикт сэмплера, `session`, `frames`, режим (`walk`/`attack`), проблемы сэмплера; в режиме атаки дополнительно `bladeTipTravel`, `worstGripError`, `gripSamples` |
| `playmode_lifecycle.txt` / `.json` | жизненный цикл | вердикт жизненного цикла, `sampler_report=present/not-written/not-run`, `sampler_result`, **проблемы сэмплера дословно** и причины жизненного цикла |
| `playmode_verdict.txt` / `.json` | жизненный цикл | то же, что lifecycle, но как итоговый объединённый вердикт (`PLAYMODE_VERDICT=...`) |
| `playmode_events.txt` / `.json` | жизненный цикл | журнал последовательности событий PlayMode |

Правило: отчёт сэмплера не перезаписывается никогда. Если сэмплер отчёт не написал, lifecycle пишет `sampler_report=not-written` (или `not-run`, если PlayMode вообще не начинался) и перечисляет свои причины — отчёт «только жизненного цикла» вместо отчёта сэмплера не подставляется. Причины понижения вердикта (`SAMPLER_FINISHED_DUPLICATE`, нарушение порядка событий, запрещённый выход) остаются в отчёте жизненного цикла и не стирают список проблем сэмплера.

### Режим `-pilotAttack`

Двуручная атака судится по тому, что она делает: клинок проходит (`bladeTipTravel` ≥ 0.35), правая рука движется, левая ладонь удерживается на `secondaryGrip` (остаток ≤ 0.02 мировых единиц = 2% вокселя), оружие не смещается в кисти. Хват передаётся аниматору через `BindGrip` и восстанавливается после перезагрузки сцены в PlayMode (`GripBound`), иначе решатель молча ничего не делал и остаток был `NaN`.

Фаза атаки в этом режиме задаётся сэмплером детерминированно: 40 реальных `Update()` кадров batch-прогона покрывают неконтролируемый кусок клипа (первый замер дал 0.26 из требуемых 0.35), поэтому метрика определена на клипе — за прогон проходится ровно один цикл атаки.

Жизненный цикл (S05A-1-REVIEW-FIX-03): сэмплер сообщает о завершении → `EditorApplication.isPlaying = false` → по `EnteredEditMode` планируется выход → тик `EditorApplication.update` подтверждает, что редактор не в PlayMode → `EditorApplication.Exit(code)`. Журнал `PilotOutput/playmode_events.txt` содержит последовательность `PLAYMODE_SESSION_STARTED → PLAYMODE_ENTERED → SAMPLER_FINISHED → PLAYMODE_EXIT_REQUESTED → PLAYMODE_EXITING → EDITMODE_ENTERED → EDITOR_EXIT_SCHEDULED → EDITOR_EXIT_EXECUTED`; нарушение порядка даёт `FAIL`. Синхронный `Exit()` из `playModeStateChanged` вызывал `0xC0000005` и больше не используется.
