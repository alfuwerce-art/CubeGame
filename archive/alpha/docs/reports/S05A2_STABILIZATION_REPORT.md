# S05A-2 — стабилизация незавершённой работы Claude (отчёт DeepSeek)

**Дата:** 2026-11-05. **Ветка:** `deepseek/s05a2-stabilization` от `43a1ba6`.
**Итог:** `READY_FOR_REVIEW` только для выполненной стабилизации; исследование анимаций остаётся `IN_PROGRESS`.

---

## TASK / STATUS

| # | Задача | STATUS |
|---|---|---|
| 1 | Своя ветка от `43a1ba6`, незакоммиченные изменения сохранены | **PASS** |
| 2 | Воспроизвести и исправить 8 падающих Python-тестов | **PASS** (161/161) |
| 3 | Проверить результаты фоновых вычислений `.scratch/animsys` | **PASS** (3/3 готовы) |
| 4 | Unity-тесты нового контроллера в изолированном проекте | **PASS** (новые C#-термы) / **BLOCKED** (`AlphaControllerPlayMode`) |
| 5 | Отчёт PASS/FAIL/BLOCKED | **PASS** (этот документ) |
| 6 | Перечень незавершённых задач для Claude | **PASS** (§6) |

---

## 1. Ветка

- Создана `deepseek/s05a2-stabilization` от `43a1ba6` (HEAD совпадает).
- Незакоммиченные изменения Claude сохранены в рабочем дереве, в коммит **не включены**:
  - `tools/alpha_models/character/CharacterPilotGripAttack.cs` — только line-ending (LF→CRLF, содержимое без изменений);
  - `Assets/CubeGame/Core/Domain.meta` — untracked, `.meta` пустой папки `Domain`.
- Открытый редактор Unity (PID 25408) работает на `.scratch/s05a2/unity/AlphaTransfer`; он **не затрагивался** (тесты гнались на отдельном `AlphaTransferDsh`).

## 2. Восемь падающих Python-тестов

Воспроизведено: `python -m pytest tools/alpha_models/tests -q` → **153 passed, 8 failed**.

Все 8 — устаревшие ожидания (Claude сам пометил в коммите `43a1ba6`: «8 older Python tests still assert the previous statuses (to update)»). Причина: в WIP-коммите термины формулы ходьбы переведены из `CODE_ONLY`/`UNKNOWN` в `VERIFIED` (по эмуляции `drawModel`), а блоки `slot9a`(крылья)/`slot98`(локти) — из `UNVERIFIED` в `VERIFIED`; тесты не обновлены. Реальных ошибок в коде не обнаружено.

Исправлены **только ожидания в тестах** (исходники `param_walk.py`/`creature_rig.py`/`pipeline.py`/эмулятор/диспетчер **не тронуты**), без ослабления проверок:

- `tools/alpha_models/tests/test_param_walk.py`:
  - `test_airborne_bit1_is_code_only` → `test_airborne_bit1_is_verified` (`FORMULA_VERIFIED`, `code_only_terms=[]`, `airborne_bit1 in terms`);
  - `test_unknown_terms_and_parts_need_research` → `test_unmodelled_parts_and_kind_exceptions_need_research` (NEEDS_RESEARCH теперь только через `PART_NOT_MODELLED`/`kind_exceptions`; бывшие UNKNOWN-термины проверяются как VERIFIED; сохранена проверка «hand_rest_angles только при кистях» через `terms`);
  - `test_angle_58_is_code_only_with_feet` → `test_angle_58_is_verified_with_feet`;
  - `test_generate_clip_refuses_research_and_marks_status` — отказ теперь через немоделируемую часть `["body", "weapon"]`;
  - `test_coverage_on_synthetic_table` — `0x92`(крылья) заменён на `0x57`(kind-specific code) для сохранения NEEDS_RESEARCH-кейса.
- `tools/alpha_models/tests/test_universal.py`:
  - `test_unverified_block_and_kind_branch_are_exceptions` — UNVERIFIED-блок теперь через `equipment=("offhand",)`;
  - `test_wrong_slot_unknown_and_unverified_sites_are_rejected` — `0x4238C5`(slot9a_b, теперь VERIFIED) → `0x421DFA`(offhand_a, ещё UNVERIFIED);
  - `test_foreign_family_clip_is_skipped_and_no_clip_fails` — `0x37`(ворона, крылья теперь VERIFIED) → `NO_FAMILY_CLIPS`; добавлен `0x57` для сохранения проверки `UNSUPPORTED`.

**Результат:** `python -m pytest tools/alpha_models/tests -q` → **161 passed, 0 failed**.

## 3. Фоновые вычисления `.scratch/animsys`

Все три результата на месте и **завершены** (процессы `survey_run.py human/cat` доработали до `DONE` во время проверки; перезапуск не выполнялся):

| Файл | Результат |
|---|---|
| `formula_all.json` | **DONE** — 156 видов, 0 ошибок |
| `survey_human.json` | **DONE** — 101 action, 0 ошибок (22:55) |
| `survey_cat.json` | **DONE** — 101 action, 0 ошибок (22:57) |

Дополнительно: `state_table.json` — 101 action с ненулевым состоянием (min 1, max 110).

## 4. Unity-тесты нового контроллера (изолированный проект)

Открытый редактор занят на `AlphaTransfer`, поэтому тесты гнались на отдельном проекте `AlphaTransferDsh` (данные перегенерированы `prepare-unity`, оригинальный `data1.db` не менялся — LastWriteTime 14.07.2013).

Команда: `run_transfer.ps1 -Project .scratch/s05a2/unity/AlphaTransferDsh -UniversalJob tools/alpha_models/universal/jobs/human_0x02_formula.json`.

**Результат `RESULT=PASS` (exit 0):**

| Шаг | Результат |
|---|---|
| `SELFTEST` | **PASS 22/22** |
| `TRANSFER` (сборка, геометрия, roundtrip, seam) | **PASS** (7/7 частей, roundtrip ≤1e-6, seam CONTINUOUS) |
| `PLAYMODE` (ходьба) | **PASS** (90 кадров, pose err 0.0) |
| `PARAMWALK` (новые C#-термы) | **PASS** — GOLDEN 900 кейсов `max_abs=0.0000014`; ALPHA 446 кадров `max_pos=0.000292 vox / max_rot=0.0°` |
| `LOCOMOTION_PLAYMODE` | **PASS** (211 кадров, head_lean 9.9995° = ожидание) |

Тем самым подтверждено: обновлённый C#-порт (`AlphaParamWalk.cs` с `root_angle_48`, `hand_rest_angles`, `foot_angle_58`, `airborne_bit1`, `slot9a`/`slot98`) **компилируется** и **совпадает с Python** в пределах порогов.

**BLOCKED:** `AlphaControllerPlayMode.Run` (полный контроллер `AlphaAnimationController` + `AlphaControllerProbe`: idle→move→stop→action→recovery→idle) **не запускался** — в проекте нет клипов `action_*` (бейкинг экшенов оракулом не выполнен, отдельного скрипта нет). Это исследовательская работа, см. §6.

## 5. Итог PASS / FAIL / BLOCKED по инструментам

| Инструмент | STATUS |
|---|---|
| Python-тесты `tools/alpha_models/tests` | **PASS 161/161** |
| `pipeline prepare-unity` (human 0x02 + formula) | **PASS** |
| Unity `AlphaTransferSelfTests` | **PASS 22/22** |
| Unity `AlphaTransferEditor.Run` (transfer) | **PASS** |
| Unity `AlphaTransferPlayMode` | **PASS** |
| Unity `AlphaParamWalkCheck` (новые термы) | **PASS** |
| Unity `AlphaParamWalkPlayMode` | **PASS** |
| `formula_all.json` / `survey_human.json` / `survey_cat.json` | **PASS (DONE)** |
| Unity `AlphaControllerPlayMode` | **BLOCKED** (нет `action_*` клипов) |
| Отчёты/playbook/память аним-системы | **NOT_VERIFIED** (не обновлены — §6) |

## 6. Незавершённые задачи для Claude

1. **`AlphaControllerPlayMode` (приоритет).** `tools/alpha_models/transfer/unity/Editor/AlphaControllerPlayMode.cs` + `Runtime/AlphaAnimationController.cs` + `Runtime/AlphaControllerProbe.cs` написаны, но не запускались: нужны клипы `action_*` (статус `EMULATED_ALPHA_CODE`). Функции бейкинга есть — `anim_states.bake_action()` / `baked_clip()` (`tools/alpha_models/universal/anim_states.py`), оракул `alpha_oracle.Oracle`; отдельного скрипта бейкинга нет. Для атаки потребуется слой оружия (rig с `weapon`, в отличие от текущего no-weapon job). Нужно: выбрать экшены, испечь клипы, повторно `prepare-unity` (с equipment), затем `AlphaControllerPlayMode.Run`.

2. **Документация аним-системы.** Коммит `43a1ba6` не обновил: отчёт исследования (drawModel-оракул, state resolver, closed walk terms), playbook (`docs/agents/*_PLAYBOOK.md`) и `PROJECT_MEMORY.md`/`docs/ACTIVE_STAGE.md`. Числа для фиксации: emu ≤8e-7 (420 player + 86 NPC кадров), 101 action → 67 drawModel-состояний, закрытые термы `hand_rest_angles`/`root_angle_48`/`foot_angle_58`/`airborne_bit1`/`wings`/`elbows_slot98` (≤1e-5° на Koala/Skeleton/Crab/PlantCreature/Imp/Penguin/Owl/Hornet/Crow/Troll/Minotaur/Golem), anchors `wing=(sx/2,0,sz/2)`, `slot_98=center`.

3. **Интерпретация готовых фоновых результатов.** `.scratch/animsys/formula_all.json` (156 видов), `survey_human.json`/`survey_cat.json` (по 101 action) теперь завершены и годятся для анализа «action→state, что переносить».

4. **Незакоммиченное, не перезаписывать.** `tools/alpha_models/character/CharacterPilotGripAttack.cs` (line-ending) и `Assets/CubeGame/Core/Domain.meta` (`.meta` пустой папки) — решить Claude.

5. **Мелочь в доках.** `tools/alpha_models/transfer/README.md` пишет «19 самотестов», фактически `AlphaTransferSelfTests` даёт **22**.

## 7. Неподтверждённое

- Художественная приёмка любых визуальных результатов — за продюсером (не выполнялась).
- `AlphaControllerPlayMode` не прогонялся (см. §6.1) — корректность `AlphaAnimationController`/`AlphaControllerProbe` в рантайме не подтверждена.
- Привязка «канал → часть тела» и `TRANSFERRED_FROM_ALPHA` для оригинальных поз — вне этой задачи (остаётся за исследованием).
- Результаты `run_transfer.ps1`/`prepare-unity` — в `.scratch/` (git-ignored), в Git не попали.

---

**Ветка/коммит:** `deepseek/s05a2-stabilization` (коммит — см. историю ветки). Изменения в Git: только 2 тестовых файла + этот отчёт.
