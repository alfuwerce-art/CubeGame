# `tools/alpha_models/review` — сцена визуальной проверки S05B (15 видов)

Инструкция для продюсера и список дефектов: [S05B_VISUAL_REVIEW_GUIDE.md](../../../docs/reports/S05B_VISUAL_REVIEW_GUIDE.md).

| Файл | Назначение |
|---|---|
| `build_review.py` | данные просмотра: для каждого вида × seed охвата B вызывает существующий `pipeline.prepare_unity` с заданиями пакетов S05B-1/S05B-A (+ библиотека игрока HumanMale, + снаряжение живого захвата) и копирует `Data` в `Assets/StreamingAssets/Review/<id>`; `index.json` с моделями и происхождением клипов |
| `setup_review.ps1` | изолированный проект Unity 6000.3.27f2 в `.scratch/review/unity/S05B_VisualReview`: код → данные → `Build` → `Check` → `RunPlayMode`; вердикт = маркер + exit 0 |
| `open_review.ps1` | открыть проект в редакторе со сценой (`-Play` — сразу PlayMode) |
| `check_ps51.ps1` | проверка всех `.ps1` парсером Windows PowerShell 5.1 + запрет не-ASCII без BOM (D61) |
| `unity/Runtime/ReviewSpecimen.cs` | сборка образца тем же рантаймом переноса (`AlphaRig`, `VoxelMesh`, `AlphaClipPlayer`, `AlphaLocomotionDriver`) |
| `unity/Runtime/ReviewViewer.cs` | интерфейс просмотра (IMGUI): вид/вариант, Idle/Walk/Action, пауза, кадры, скорость, ряд из 15, проверки |
| `unity/Runtime/ReviewOrbitCamera.cs` | орбитальная камера |
| `unity/Editor/ReviewSceneBuilder.cs` | сборка сцены, EditMode-проверка всех источников, контактные листы, PlayMode-обход |

D61: правило маркеров Alpha (M — до/после), цвет волос из живых наблюдений (C), адаптивная раскладка — [отчёт](../../../docs/reports/D61_TINT_REVIEW_FIX_REPORT.md).

Данные Alpha и проект — только в `.scratch` (D54/D59/D60), в Git не попадают.
