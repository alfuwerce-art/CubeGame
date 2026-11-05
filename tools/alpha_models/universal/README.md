# tools/alpha_models/universal — универсальный конвейер Alpha → Unity (прототип)

Продолжение системы одного гуманоида (Codex, 2026-11-06): [ANIMATION_SYSTEM_PLAYBOOK](../../../docs/agents/ANIMATION_SYSTEM_PLAYBOOK.md), [отчёт](../../../docs/reports/S05A2_CLAUDE_CONTINUATION_REPORT.md).
`action_baker.py` автоматизирует существующий oracle и ведёт полный реестр отказов; `validate_oracle.py` проверяет его по живым pre-draw захватам; `weapon_locomotion.py` — отдельный слой оружия. S05B не разрешён.

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
| `param_walk.py` | **параметрическая ходьба**: формулы `drawModel` состояния 0 (эталон для C#), члены формулы и их доказательность, классификатор `classify`/`coverage`, клип/спецификация для Unity, проверка против захвата ([исследование](../../../docs/research/alpha2013/PARAMETRIC_WALK_SYSTEM.md), [спецификация](../../../docs/architecture/PARAMETRIC_LOCOMOTION_SPEC.md), [методичка](../../../docs/agents/PARAMETRIC_WALK_PLAYBOOK.md)) |
| `pipeline.py` | CLI: `scan`, `build-clips`, `prepare-unity` (+ `"locomotion": "formula"`), `batch`, `param-walk-validate`, `param-walk-coverage` |
| `jobs/*.json` | задания Unity (вид, вариант моделей, экипировка) — без данных Alpha |

Только стандартная библиотека Python 3.13. Все данные Alpha (таблицы, захваты, клипы, проекты Unity) — в `.scratch/universal` (не в Git). Тесты: `tools/alpha_models/tests/test_universal.py`, `tools/alpha_models/tests/test_param_walk.py`.
