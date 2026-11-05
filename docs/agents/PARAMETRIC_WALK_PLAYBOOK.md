# Методичка: подключение существ через параметрическую ходьбу Alpha (для DeepSeek)

**Статус технологии:** прототип, `READY_FOR_REVIEW` (не ACCEPTED). Формула ходьбы проверена на трёх планах тела (человек, кот, бегун) по всем кадрам захватов; Unity-прогоны PASS. **S05B не разрешён**: методичка описывает, как подключать виды, но массовая обработка начинается только по решению продюсера.
**Читать вместе с:** [исследование и точность](../research/alpha2013/PARAMETRIC_WALK_SYSTEM.md), [спецификация и интерфейсы](../architecture/PARAMETRIC_LOCOMOTION_SPEC.md), [универсальный конвейер](UNIVERSAL_TRANSFER_PLAYBOOK.md) (захват, блоки, модели — в силе).

---

## 0. Что уже решено — не исследовать заново

| Вопрос | Ответ |
|---|---|
| Есть ли в Alpha анимация ходьбы у каждого вида? | Нет. Ходьба = одна формула `drawModel` от фазы, амплитуды и параметров внешности вида |
| Нужен ли захват, чтобы получить ходьбу нового вида? | Только если вид использует непроверенный член формулы (`classify` → `code_only_terms` / `unknown_terms`) |
| Чем различаются виды? | Значениями внешности; кодом — только флаг `flags&1` («четвероногое» качание), обе ветви проверены |
| Точность формулы | ≤ 0.00034 вокселя / ≤ 0.0004° на 4101 установившемся кадре Alpha (игрок, NPC-люди, кот, бегун; все амплитуды) |
| Статус поз из формулы | всегда `RECONSTRUCTED_FROM_EVIDENCE`, никогда `TRANSFERRED_FROM_ALPHA` |
| Можно ли менять формулу? | Нет. Ядро (`param_walk.joints`, `TERMS`, `AlphaParamWalk.Joint`) меняет только архитектор |

## 1. Порядок работы для одного вида

### Шаг 1. Покрытие (без Alpha, секунды)

```powershell
python -m tools.alpha_models.universal.pipeline param-walk-coverage --static .scratch\universal\static\alpha_static.json --out .scratch\universal\static\param_walk_coverage.json
```

В `param_walk_coverage.json → kinds[]` найти вид: `status`, `terms`, `code_only_terms`, `unknown_terms`, `reasons`.

| `status` | Что делать |
|---|---|
| `FORMULA_VERIFIED` | шаг 2 — подключать |
| `FORMULA_CODE_ONLY_TERMS` | подключать можно; в отчёте перечислить `code_only_terms`. Сейчас открыт только `airborne_bit1` (2 вида). Если член открыт у многих видов, предложить архитектору один проверочный захват (шаг 4) |
| `NEEDS_RESEARCH` | не подключать; записать в отчёт `unknown_terms`/`reasons`, задача архитектору |

### Шаг 2. Задание (единственный создаваемый файл, в Git)

`tools/alpha_models/universal/jobs/<имя>_<kind>_formula.json`:

```json
{
 "kind": "0x1c",
 "seed_variant": [0, 0],
 "variant": "wolf_0x1c_formula_only",
 "locomotion": "formula",
 "amp": 0.19,
 "speed_xy": 3.7,
 "note": "FORMULA_VERIFIED, no capture; RECONSTRUCTED_FROM_EVIDENCE"
}
```

Правила: без `equipment` (оружие — не часть формулы); `accelerating: true` для ИИ-существ (наклон головы +5°, VERIFIED на NPC-людях); если у семейства **есть** захват — добавить `"reference_capture": {"path": "D:/Projects/CubeGame/.scratch/<…>.jsonl", "kind": "0xNN"}` (только прямые слэши: обратные ломают JSON).

### Шаг 3. Прогон Unity

```powershell
& tools\alpha_models\transfer\run_transfer.ps1 -Project D:\Projects\CubeGame\.scratch\paramwalk\unity\<Имя> -UniversalJob tools\alpha_models\universal\jobs\<задание>.json
```

Не использовать проект, открытый в интерактивном Unity (проверить `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"`). Итог — только по файлам `<Project>\TransferOutput` и кодам выхода:

| Файл | Маркер | Обязательно |
|---|---|---|
| `selftest.txt` | `SELFTEST_RESULT=PASS cases=22` | |
| `transfer_report.txt` | `TRANSFER_RESULT=PASS` | GEOMETRY N/N, ROUNDTRIP ≤ 1e-3, SEAM CONTINUOUS для `walk` |
| `playmode.txt` | `PLAYMODE_RESULT=PASS` | `runtime_pose_max_pos_err=0` |
| `paramwalk.txt` | `PARAMWALK_RESULT=PASS` | `golden=480`, при `reference_capture` — строка `ALPHA … max_pos_vox ≤ 0.001` |
| `locomotion_playmode.txt` | `LOCOMOTION_PLAYMODE_RESULT=PASS` | `runtime_pose_max_pos_err=0`, `phase_travel_rad > 0`, `max_part_rot_deg ≥ 10` или `max_part_move_vox ≥ 1` |
| вывод скрипта | `RESULT=PASS` | |

### Шаг 4. Проверочный захват открытого члена (только с согласия архитектора)

Нужен один раз на член, не на вид. Взять любой вид, у которого член задействован (`coverage → families`, поле `code_only_terms`/`unknown_terms`), снять его по [UNIVERSAL_TRANSFER_PLAYBOOK §2 шаг 2](UNIVERSAL_TRANSFER_PLAYBOOK.md), затем:

```powershell
python -m tools.alpha_models.universal.pipeline param-walk-validate --capture .scratch\universal\cap\<имя>.jsonl --kind 0xNN
```

`PARAMWALK_VALIDATE=PASS` → передать архитектору: член можно перевести в `VERIFIED` (правку `TERMS` делает архитектор). `FAIL` → **не подгонять**: приложить `parts{…}` с ошибками и кадры; это исследовательская задача.

### Шаг 5. Регрессия и отчёт

* `python -m unittest discover -s tools/alpha_models/tests -t .` — всё OK (на момент написания 161 тест);
* `run_transfer.ps1 -Test` — 10/10;
* регрессия формулы против захватов: `jobs/whitecat_0x20_formula.json` (+ `plainrunner_0x3f`, `human_0x02`, `human_0x02_npc`) — `PARAMWALK_RESULT=PASS` с `ALPHA` ≤ 0.001;
* отчёт: вид, семейство, `status`, `code_only_terms`, маркеры Unity, пути к `TransferOutput`; статус клипа — `RECONSTRUCTED_FROM_EVIDENCE`; `git status` — ничего из `.scratch`.

## 2. Что нельзя

* менять формулу, константы, `TERMS`, пороги, `AlphaParamWalk.Joint` — даже если «почти совпадает»;
* присваивать `TRANSFERRED_FROM_ALPHA` позам из формулы;
* подключать `NEEDS_RESEARCH`-виды, обходя отказ (`LOCOMOTION_NEEDS_RESEARCH`, `PART_NOT_MODELLED`);
* добавлять оружие/экипировку в задания формулы;
* коммитить `.scratch` (внешности, захваты, модели, эталоны `paramwalk_reference.json`);
* запускать пакетную обработку всех видов (S05B) без решения продюсера.

## 3. Типичные ошибки

| Симптом | Причина | Что делать |
|---|---|---|
| `json.decoder.JSONDecodeError: Invalid \escape` | путь в задании с `\` | прямые слэши `D:/…` |
| `LOCOMOTION_NEEDS_RESEARCH unknown_terms=[…]` | вид использует UNKNOWN-член | не подключать; задача архитектору |
| `LOCOMOTION_PARTS_NOT_MODELLED [slot9a_a…]` | крылья/`slot_98`/экипировка | не подключать; исследование блока (UNIVERSAL §3) |
| `REFERENCE_FAMILY_MISMATCH` | захват другого семейства | указать захват вида того же `family_id` или убрать `reference_capture` |
| `GOLDEN_MISMATCH` | C# и Python разошлись | правка ядра без синхронизации — вернуть, сообщить архитектору |
| `ALPHA_MISMATCH` | формула не совпала с захватом | не ослаблять порог; приложить `paramwalk.txt`; исследование |
| `HEAD_LEAN_SMOOTHING` | изменён шаг сглаживания | вернуть `LeanStep` к `1 − 0.99^dt_мс` |
| Unity exit 1 сразу, нет маркеров | проект открыт в интерактивном Unity | другой `-Project` |

## 4. Приоритет (когда продюсер разрешит масштабирование)

1. 66 семейств `FORMULA_VERIFIED` (сначала 22 гуманоидных и 16 с конечностями, затем 28 «только тело»); `head_lean_ai` уже проверен захватом NPC-людей (`.scratch\paramwalk\cap_npc_human.jsonl`, задание `jobs/human_0x02_npc_formula.json`);
2. `airborne_bit1` (2 вида) — захват в прыжке/падении, по согласию архитектора;
3. захват одного вида с `hand_rest_angles` (12 семейств) — исследование архитектора;
4. крылья и `slot_98` — по [UNIVERSAL_TRANSFER_PLAYBOOK §3](UNIVERSAL_TRANSFER_PLAYBOOK.md).
