# `tools/alpha_models` — конвертация моделей и сборка персонажа Cube World Alpha 2013

Собственные инструменты пилотов `S05A-0` и `S05A-1` ([D47](../../docs/decisions/D47-s04-accepted-s05a0-authorized.md), [D49](../../docs/decisions/D49-s05a1-authorized.md), [D46](../../docs/decisions/D46-asset-conversion-first.md)). Код написан с нуля для этого проекта: чужие реализации не копировались, используются только опубликованные структурные факты формата (см. [K_assets.md](../../docs/research/alpha2013/BEHAVIOR_SPECS/K_assets.md)).

**Правовая граница:** инструменты читают `D:\Games\Cube world` **только на чтение** и не содержат ассетов. Результаты конвертации (`.cubdec`, `.obj`, `.mtl`, скриншоты) — производные от закрытых ассетов Picroma: они остаются **локально** (`.scratch/`, в `.gitignore`) и не публикуются.

## Формат `.cub` (после деобфускации)

```
int32le width
int32le depth
int32le height
uint8[width * depth * height * 3]     // RGB, индекс = (h * depth + d) * width + x
                                      // (0,0,0) = пустой воксель
```

Блобы в `data1.db` обфусцированы: табличный де-шаффл (44 значения) + побитовая инверсия байтов. Проверено на всех 2857 записях: размер = `12 + w*d*h*3`, расхождений нет.

## Скрипты

| Файл | Назначение |
|---|---|
| `cub_format.py` | деобфускация, разбор заголовка, цвета, поиск маркеров-анкоров |
| `cub_probe.py` | инвентаризация `data1.db`: ключи, размеры, габариты, цвета (только метаданные) |
| `cub_to_obj.py` | `.cub` → OBJ + MTL (один материал на цвет) и `--dump-decoded` (`.cubdec`) |
| `cub_preview.py` | ортогональные превью (front/side/top) для проверки ориентации осей |
| `cub_markers.py` | измерение групп чистых R/G/B вокселей (координаты, bbox, доля на поверхности) + эвристическая метка `ANCHOR_CANDIDATE` / `VOLUME_TINT_CANDIDATE` / `UNKNOWN_DECORATIVE`; семантика не присваивается автоматически |
| `verify_export.py` | пофасетная проверка экспорта: каждая грань OBJ сопоставляется с гранью вокселя (координаты, обход/нормаль, цвет/материал, кратность), плюс габариты и единичная площадь; `--exclude-markers` учитывает скрытые маркеры |
| `tests/` | `unittest`: битые файлы, round-trip обфускации, отсечение скрытых граней, геометрия OBJ, 16 негативных кейсов порчи OBJ, 14 кейсов маркеров и их исключения |
| `unity/` | Editor-скрипт проверки S05A-0 с валидаторами и самотестами, шейдер, образцы отчётов (см. ниже) |
| `character/` | Editor-скрипт сборки персонажа S05A-1, процедурный аниматор, PlayMode-сэмплер, шейдер и образец спеки сборки (см. §«Сборка персонажа») |

## Быстрый прогон

```powershell
$py = "python"   # любой Python 3.11+; Pillow нужен только для cub_preview.py

# 1. инвентаризация базы (метаданные, без выгрузки ассетов)
& $py tools\alpha_models\cub_probe.py --db "D:\Games\Cube world\data1.db" --out .scratch\s05a0\probe.json

# 2. конвертация трёх образцов
& $py tools\alpha_models\cub_to_obj.py --db "D:\Games\Cube world\data1.db" --key "human-head-m01.cub" --out-dir .scratch\s05a0\conv --dump-decoded
& $py tools\alpha_models\cub_to_obj.py --db "D:\Games\Cube world\data1.db" --key "porcupine-body.cub" --out-dir .scratch\s05a0\conv --dump-decoded
& $py tools\alpha_models\cub_to_obj.py --db "D:\Games\Cube world\data1.db" --key "iron-greatsword.cub" --out-dir .scratch\s05a0\conv --dump-decoded

# 3. проверка целостности
& $py tools\alpha_models\verify_export.py --cubdec .scratch\s05a0\conv\iron-greatsword.cubdec `
    --obj .scratch\s05a0\conv\iron-greatsword.obj --mtl .scratch\s05a0\conv\iron-greatsword.mtl

# 4. тесты
& $py -m unittest discover -s tools/alpha_models/tests -t tools/alpha_models
```

## Проверка в Unity (изолированный проект)

`unity/` содержит только код проверки, проект создаётся заново (в `.scratch`, не в production-проекте CubeGame):

1. Создать пустой проект Unity 6000.3.27f2 (`Packages/manifest.json` = `{"dependencies": {}}`, `ProjectSettings/ProjectVersion.txt` = `m_EditorVersion: 6000.3.27f2`).
2. Положить `unity/ModelPilot.cs` в `Assets/Editor/`, `unity/VertexColorUnlit.shader` в `Assets/PilotShaders/`.
3. Положить результаты шага 2 в `Assets/PilotData/*.cubdec` и `Assets/PilotModels/*.obj|*.mtl`.
4. Запустить:

```powershell
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit `
    -projectPath <путь проекта> -executeMethod ModelPilot.Run -logFile batch.log

# самотесты валидаторов (негативные кейсы, без ассетов)
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit `
    -projectPath <путь проекта> -executeMethod ModelPilot.RunSelfTests -logFile selftest.log
```

Результат `Run`: `PilotOutput/report.json` (числа по двум независимым путям — собственный mesh с vertex colors и импорт OBJ через `ModelImporter`, плюс `verdict` и `setProblems`), `PilotOutput/report.txt` с маркером `PILOT_RESULT=PASS|FAIL|EXCEPTION` и PNG-скриншоты. Любая проблема (нет пары `.cubdec`/`.obj`, лишний файл, пустой набор, несовпадение треугольников/габаритов, упавший импорт, исключение) даёт `FAIL` и ненулевой код возврата; папка `PilotOutput` очищается в начале прогона, чтобы старый `PASS` не пережил неудачу. Результат `RunSelfTests`: `PilotOutput/selftest.json` и маркер `SELFTEST_RESULT`.

**Важно про зеркалирование:** Unity импортирует OBJ как правосторонний формат и переводит его в своё пространство через `x' = -x` (импортированные `bounds.min.x` отрицательны). Проверка это учитывает (`mirroredX`) и сравнивает с зеркально ожидаемыми `min`/размером; при необходимости точного совпадения с файлом используйте `Bake Axis Conversion` или предварительно инвертируйте X. Путь «собственный `Mesh` + vertex colors» от этого не зависит.

Образцы вывода (только числа, без ассетов): `unity/report.sample.json`, `unity/report.sample.txt`, `unity/selftest.sample.json`.

## Сборка персонажа и анимация (S05A-1)

`character/` содержит код изолированного пилота сборки одного гуманоида:

| Файл | Назначение |
|---|---|
| `CharacterPilot.cs` | Editor-скрипт: сборка из частей по `assembly.json`, валидаторы (иерархия, размещение, ориентация, крепление головы, гнездо оружия, движение, замыкание цикла), 21 самотест, рендер снимков, отчёты |
| `ProceduralCharacterAnimator.cs` | технический цикл `technical_walk_NOT_ALPHA` (детерминированный `Sample(t)` + `Update()` в PlayMode) |
| `PlayModeSampler.cs` | снимает 40 реальных кадров PlayMode и проверяет движение и стабильность меча в кисти |
| `assembly.sample.json` | образец спеки сборки (имена частей, offsets, статусы достоверности — без данных моделей) |
| `VertexColorUnlit.shader` | unlit-шейдер vertex colors (общий с S05A-0) |

Запуск (изолированный проект, данные — только локально в `.scratch`):

```powershell
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit -projectPath <проект> -executeMethod CharacterPilotVerifier.Run -logFile run.log
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -quit -projectPath <проект> -executeMethod CharacterPilotVerifier.RunSelfTests -logFile selftest.log
& "D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe" -batchmode -projectPath <проект> -executeMethod CharacterPilotVerifier.RunPlayMode -logFile playmode.log
```

Требуется `Assets/CharacterPilot/data/`: `*.cubdec` (по одной на часть), `markers.json` (скрытые маркеры) и `assembly.json`. Подготовка данных — `prepare_assets` из отчёта S05A-1 (в `.scratch`, не публикуется). Пивот части стоит в суставе: абсолютное начало = `offset + joint`, вложенная часть позиционируется относительно **пивота** родителя, а не его offset.

Маркеры и тинт: `cub_to_obj.py --markers <json> --exclude-markers red,green,blue` скрывает **только явно названные** цвета; по умолчанию геометрия не меняется. Семантика маркеров (`PARTIAL`/`UNKNOWN`) и открытый вопрос тинта волос описаны в [отчёте S05A-1](../../docs/research/alpha2013/CHARACTER_ASSEMBLY_ANIMATION_PILOT.md).
