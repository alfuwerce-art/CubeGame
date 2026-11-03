# `tools/alpha_models/character` — сборка персонажа и анимация (S05A-1)

Код изолированного Unity-пилота `S05A-1` ([D49](../../../docs/decisions/D49-s05a1-authorized.md)). Отчёт с числами и ограничениями — [CHARACTER_ASSEMBLY_ANIMATION_PILOT.md](../../../docs/research/alpha2013/CHARACTER_ASSEMBLY_ANIMATION_PILOT.md).

| Файл | Назначение |
|---|---|
| `CharacterPilot.cs` | Editor-скрипт: сборка из частей по `assembly.json`, валидаторы (иерархия, размещение, ориентация, крепление головы, гнездо оружия, движение, замыкание цикла), 22 самотеста, снимки, отчёты |
| `ProceduralCharacterAnimator.cs` | технический цикл `technical_walk_NOT_ALPHA` (детерминированный `Sample(t)` + `Update()` в PlayMode) |
| `PlayModeSampler.cs` | 40 реальных кадров PlayMode: движение частей + стабильность меча в кисти |
| `assembly.sample.json` | образец спеки сборки: имена частей, offsets в пространстве персонажа, статусы достоверности (без данных моделей) |
| `VertexColorUnlit.shader` | unlit-шейдер vertex colors (общий с S05A-0) |

## Правила, важные для повторного запуска

1. **Пивот части — в суставе.** Абсолютное начало части = `offset + joint`; вложенная часть позиционируется относительно **пивота** родителя, а не его `offset`. Ошибка здесь ловится валидатором `world min != spec offset`.
2. **Маркеры скрываются только по явному указанию цветов**: `cub_to_obj.py --exclude-markers green,blue`. По умолчанию геометрия не меняется; семантика маркеров не присваивается автоматически (см. `cub_markers.py`).
3. **Ориентация:** собственный `Mesh` строится напрямую из вокселей (без `ModelImporter`), поэтому зеркалирование OBJ по X из S05A-0 здесь не возникает; проверка `mesh min >= 0` это фиксирует.
4. **PlayMode:** в проекте отключается domain reload (`EnterPlayModeOptions.DisableDomainReload`), иначе статические колбэки сэмплера теряются при входе в Play.
5. **Данные пилота** (`*.cubdec`, `markers.json`, PNG, отчёты) остаются локально в `.scratch/` и в Git не попадают.

## Запуск

```powershell
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit -projectPath <проект> -executeMethod CharacterPilotVerifier.Run -logFile run.log
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit -projectPath <проект> -executeMethod CharacterPilotVerifier.RunSelfTests -logFile selftest.log
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -projectPath <проект> -executeMethod CharacterPilotVerifier.RunPlayMode -logFile playmode.log
```

Ожидаемые маркеры: `CHARACTER_RESULT=PASS`, `SELFTEST_RESULT=PASS`, `PLAYMODE_RESULT=PASS`.
