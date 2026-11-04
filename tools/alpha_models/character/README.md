# `tools/alpha_models/character` — сборка персонажа и анимация (S05A-1)

Код изолированного Unity-пилота `S05A-1` ([D49](../../../docs/decisions/D49-s05a1-authorized.md)). Отчёт с числами и ограничениями — [CHARACTER_ASSEMBLY_ANIMATION_PILOT.md](../../../docs/research/alpha2013/CHARACTER_ASSEMBLY_ANIMATION_PILOT.md).

| Файл | Назначение |
|---|---|
| `CharacterPilot.cs` | Editor-скрипт: сборка из частей по `assembly.json`, валидаторы (иерархия, размещение, ориентация, крепление головы, гнездо оружия, движение, замыкание цикла), 49 самотестов, снимки, отчёты |
| `CharacterPilotFixes.cs` | S05A-1-REVIEW-FIX-01: двухпроходный расчёт координат (FIX-01), размещение по исходной сетке + проверка кожных вокселей (FIX-02), независимая проверка `Transform.parent` (FIX-03), выгрузка геометрии для кросс-проверки с Python |
| `ProceduralCharacterAnimator.cs` | технический цикл `technical_walk_NOT_ALPHA` (детерминированный `Sample(t)` + `Update()` в PlayMode) |
| `PlayModeSampler.cs` | 40 реальных кадров PlayMode: движение частей + стабильность меча в кисти; `RequiredFrames` (40) отделён от `FrameLimit`, в отчёт пишется `session` |
| `CharacterPilotPlayMode.cs` | точка входа PlayMode: предполётные проверки (сборка, 8 узлов, аниматор), жизненный цикл `OnFinished → isPlaying=false → EnteredEditMode → отложенный Exit`, журнал событий и `ValidateEventOrder`, отчёты FAIL/EXCEPTION |
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

## Запуск

```powershell
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit -projectPath <проект> -executeMethod CharacterPilotVerifier.Run -logFile run.log
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit -projectPath <проект> -executeMethod CharacterPilotVerifier.RunSelfTests -logFile selftest.log
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -projectPath <проект> -executeMethod CharacterPilotVerifier.RunPlayMode -logFile playmode.log
```

Ожидаемые маркеры: `CHARACTER_RESULT=PASS`, `SELFTEST_RESULT=PASS`, `PLAYMODE_RESULT=PASS`, и для PlayMode — код завершения процесса `0`.

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

`RunPlayMode` не запускает PlayMode, если сборка неполная, отсутствует часть или аниматор: пишет `PLAYMODE_RESULT=FAIL` с причинами и завершается ненулевым кодом.

Жизненный цикл (S05A-1-REVIEW-FIX-03): сэмплер сообщает о завершении → `EditorApplication.isPlaying = false` → по `EnteredEditMode` планируется выход → тик `EditorApplication.update` подтверждает, что редактор не в PlayMode → `EditorApplication.Exit(code)`. Журнал `PilotOutput/playmode_events.txt` содержит последовательность `PLAYMODE_SESSION_STARTED → PLAYMODE_ENTERED → SAMPLER_FINISHED → PLAYMODE_EXIT_REQUESTED → PLAYMODE_EXITING → EDITMODE_ENTERED → EDITOR_EXIT_SCHEDULED → EDITOR_EXIT_EXECUTED`; нарушение порядка даёт `FAIL`. Синхронный `Exit()` из `playModeStateChanged` вызывал `0xC0000005` и больше не используется.
