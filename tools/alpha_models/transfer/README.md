# `tools/alpha_models/transfer` — перенос оригинальных анимаций Alpha в Unity (S05A-2)

Методичка (порядок работы, форматы, пороги, типичные ошибки): [ANIMATION_TRANSFER_PLAYBOOK.md](../../../docs/agents/ANIMATION_TRANSFER_PLAYBOOK.md).
Отчёт с фактическими числами: [ALPHA_ANIMATION_TRANSFER.md](../../../docs/research/alpha2013/ALPHA_ANIMATION_TRANSFER.md).

| Файл | Назначение |
|---|---|
| `prepare_unity.py` | `.cub` из `data1.db` (только чтение) → `models/*.cubdec`, `rig.json`; клипы `*.alphaclip.json` → плоский формат для Unity; проверка совместимости моделей |
| `run_transfer.ps1` | одна команда: изолированный проект 6000.3.27f2 → копия кода → данные → самотесты → перенос → PlayMode; вердикт по файлам отчётов |
| `models.s05a1_greatsword.json` | гуманоид S05A-1 с двуручным мечом `iron-greatsword` (подтип 15) — основной пример |
| `models.captured_greatmace.json` | тот же гуманоид с оружием из захвата (`wood-greatmace02`, подтип 17) — сверка один-к-одному |
| `unity/Runtime/AlphaSpace.cs` | единственное место преобразования Alpha (строки-векторы, Z вверх) ↔ Unity |
| `unity/Runtime/AlphaClip.cs` | клип: разбор с проверками, сэмплирование (lerp/slerp) |
| `unity/Runtime/AlphaRig.cs` | плоский риг `root → part_* → mesh`, правила якорей, чтение матрицы обратно в пространство Alpha |
| `unity/Runtime/AlphaClipPlayer.cs` | проигрыватель (время в мс / фаза ходьбы), слой наклона взгляда |
| `unity/Runtime/AlphaParamWalk.cs` | параметрическая ходьба Alpha (порт `universal/param_walk.py`): кадры суставов по фазе/амплитуде/параметрам вида |
| `unity/Runtime/AlphaLocomotionDriver.cs` | позирует риг формулой каждый кадр (скорость, амплитуда, ×1.25, наклон головы) — без клипов |
| `unity/Runtime/AlphaLocomotionProbe.cs` | PlayMode-проверка драйвера: поза = формула, развёртка амплитуды, сглаживание наклона |
| `unity/Editor/AlphaParamWalkCheck.cs` | SPEC / синтетика / GOLDEN (C# = Python) / ALPHA (C# против захвата) → `paramwalk.txt` |
| `unity/Editor/AlphaParamWalkPlayMode.cs` | безопасный вход/выход PlayMode для драйвера → `locomotion_playmode.txt` |
| `unity/Runtime/VoxelMesh.cs` | меш из `.cubdec` в координатах сетки (те же грани, что у Alpha) |
| `unity/Runtime/AlphaPlayModeProbe.cs` | проверка в реальных кадрах PlayMode |
| `unity/Runtime/VoxelShaded.shader` | вершинные цвета + постоянный свет для снимков |
| `unity/Editor/AlphaTransferEditor.cs` | `Run` (сборка + GEOMETRY/ROUNDTRIP/SEAM/GRIP/SHOTS), `RenderCompare` |
| `unity/Editor/AlphaTransferSelfTests.cs` | 19 синтетических самотестов (`TEST_DATA_NOT_ALPHA`) |
| `unity/Editor/AlphaTransferPlayMode.cs` | вход/выход PlayMode без аварии (выход только после `EnteredEditMode`) |

Связанные инструменты: захват — [`../runtime`](../runtime/README.md) (`pose_capture.py`, `alpha_*.ps1`), клипы — [`../anim`](../anim/README.md) (`alpha_clip.py`, `humanoid_rig.py`, `mat4.py`). Тесты: `python -m unittest discover -s tools/alpha_models/tests -t .`

Данные Alpha (захваты, клипы, `.cubdec`, Unity-проекты, PNG) живут только в `.scratch/` и в Git не попадают.
