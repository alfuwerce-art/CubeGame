# tools/alpha_models/universal — универсальный конвейер Alpha → Unity (прототип)

Исследование: [UNIVERSAL_ANIMATION_SYSTEM_RESEARCH.md](../../../docs/research/alpha2013/UNIVERSAL_ANIMATION_SYSTEM_RESEARCH.md) ·
архитектура (DRAFT): [UNIVERSAL_ASSET_ANIMATION_PIPELINE.md](../../../docs/architecture/UNIVERSAL_ASSET_ANIMATION_PIPELINE.md) ·
методичка: [UNIVERSAL_TRANSFER_PLAYBOOK.md](../../../docs/agents/UNIVERSAL_TRANSFER_PLAYBOOK.md) ·
результаты: [UNIVERSAL_TRANSFER_PROTOTYPE_REPORT.md](../../../docs/research/alpha2013/UNIVERSAL_TRANSFER_PROTOTYPE_REPORT.md).

| Модуль | Назначение |
|---|---|
| `x86emu.py` | строгий интерпретатор x86-32 для небольших чистых функций `Cube.exe` (без угадывания) |
| `alpha_static.py` | вид → внешность (эмуляция), реестр индекс → `.cub`, размеры моделей, имена-подсказки; закреплённый SHA256 сборки |
| `creature_rig.py` | блоки отрисовки `drawModel`, правила якорей, разрешение рига, семейства движения, исключения |
| `uclip.py` | захват → клипы любого проверенного плана тела (ходьба, покой), проверки |
| `pipeline.py` | CLI: `scan`, `build-clips`, `prepare-unity`, `batch` |
| `jobs/*.json` | задания Unity (вид, вариант моделей, экипировка) — без данных Alpha |

Только стандартная библиотека Python 3.13. Все данные Alpha (таблицы, захваты, клипы, проекты Unity) — в `.scratch/universal` (не в Git). Тесты: `tools/alpha_models/tests/test_universal.py`.
