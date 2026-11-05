# Параметрическая локомоция Alpha → Unity: техническая спецификация и интерфейсы

**Статус: DRAFT** (предложение архитектора, не утверждено продюсером). **Дата:** 2026-11-05. **Автор:** Claude Code.
**Основание:** [PARAMETRIC_WALK_SYSTEM.md](../research/alpha2013/PARAMETRIC_WALK_SYSTEM.md) (формулы, точность, покрытие). **Расширяет:** [UNIVERSAL_ASSET_ANIMATION_PIPELINE.md](UNIVERSAL_ASSET_ANIMATION_PIPELINE.md) §4 (подход B теперь реализован для ходьбы). **Методичка исполнителя:** [PARAMETRIC_WALK_PLAYBOOK.md](../agents/PARAMETRIC_WALK_PLAYBOOK.md).
**Границы:** локальный исследовательский прототип (D54). Не S05B, не production-Unity; данные Alpha (внешности, захваты, модели) — только `.scratch`. Перед релизом — собственные/лицензированные ассеты.

---

## 1. Назначение

Ходьба любого существа Alpha вычисляется одной формулой от **параметров вида** (статически извлекаемых из `Cube.exe`) и **рантайм-состояния** (фаза, амплитуда, ускорение). Поэтому для подключения нового вида к Unity не нужны ни захват, ни клип, ни правка кода — нужны только данные вида и задание. Захват нужен лишь тогда, когда вид использует член формулы, который ещё не проверен ни на одном захвате.

## 2. Компоненты и границы ядра

```
 alpha_static.json ──► classify / terms_used ──► FORMULA_VERIFIED | FORMULA_CODE_ONLY_TERMS | NEEDS_RESEARCH
 (вид → внешность)          (param_walk)                         │
                                                                 ▼
 job.json ──► pipeline prepare-unity ──► Data/locomotion.json ──► AlphaLocomotionDriver (рантайм, без клипов)
                     │                   Data/clips/walk.json  ──► AlphaClipPlayer (клип формулы, 72 ключа)
                     │                   Data/paramwalk_golden.json, Data/paramwalk_reference.json (опц.)
                     ▼
               AlphaParamWalkCheck (EditMode)  +  AlphaLocomotionProbe (PlayMode)  →  маркеры PASS/FAIL
```

| Слой | Файл | Кто меняет |
|---|---|---|
| **Ядро формулы** (эталон) | `tools/alpha_models/universal/param_walk.py`: `core`, `joints`, `head_lean_target`, `lean_step`, `phase_rate`, `TERMS`, `terms_used`, `classify` | только архитектор (ревью) |
| **Ядро формулы** (порт) | `transfer/unity/Runtime/AlphaParamWalk.cs` | только вместе с эталоном, в одном коммите; GOLDEN обязателен |
| Экспорт/валидация | `param_walk.py`: `generate_walk_clip`, `locomotion_spec`, `golden_cases`, `reference_frames`, `validate_capture`, `coverage`; `pipeline.py`: `param-walk-validate`, `param-walk-coverage`, `prepare-unity` (`_prepare_locomotion`) | архитектор; исполнитель — только исправления ошибок с тестом |
| Рантайм Unity | `AlphaLocomotionDriver.cs`, `AlphaLocomotionProbe.cs` | исполнитель может добавлять входы игры (скорость, состояния), не меняя `Joint` |
| Проверки Unity | `Editor/AlphaParamWalkCheck.cs`, `Editor/AlphaParamWalkPlayMode.cs` | пороги не ослаблять |
| **Данные подключения** | `tools/alpha_models/universal/jobs/*_formula.json` | исполнитель (DeepSeek), без правок кода |

**Правило «ядро не меняется»:** подключение вида = новый `job.json` + прогон. Если вид отклонён (`NEEDS_RESEARCH`, `PART_NOT_MODELLED`, `UNKNOWN`-член), исполнитель не правит формулу, а оформляет исследовательскую задачу (§7).

## 3. Python API (эталон)

```python
from tools.alpha_models.universal import param_walk as pw

app   # dict из alpha_static.decode_appearance(): flags, size, scale_*, pitch_tail, angle_*, offset_*
state = pw.WalkState(phase, amp, boost=False, head_lean_deg=None, on_ground=True, accelerating=False)
J     = pw.joints(app, state, parts)        # {part: [16 float]} — Alpha row-vector, без якоря
k     = pw.core(app, state)                 # общие величины: c, w, d0, e4, bob, foot_z, lift_l/r, hand_l/r
pw.head_lean_target(app, boost, accelerating) -> deg;   pw.lean_step(cur, target, dt_ms) -> deg
pw.phase_rate(app, speed_xy) -> rad/s       # INFERRED
pw.terms_used(app, parts) -> [term]         # какие члены формулы задействует вид
pw.classify(app, parts, kind_exceptions=()) -> {status, terms, code_only_terms, unknown_terms, reasons}
pw.locomotion_spec(app, kind, family_id, parts) -> dict (schema §5.1)
pw.generate_walk_clip(app, kind, family_id, parts, amp, boost=False, keys=72, accelerating=False, speed_xy=None)
pw.validate_capture(frames) -> {frames, parts{part: max_pos_vox, max_rot_deg, p95_rot_deg}, head_lean_one_step}
pw.coverage(static_table, captured_kinds=(), captured_families=()) -> schema §5.4
pw.golden_cases() / pw.reference_frames(frames, capture_path)
```

Части (имена блоков отрисовки `creature_rig`): `body, head, hair, tail, hand_l, hand_r, foot_l, foot_r` (`MODELLED_PARTS`). Любая другая часть → `PART_NOT_MODELLED`.

CLI:

```powershell
python -m tools.alpha_models.universal.pipeline param-walk-validate --capture CAP.jsonl --kind 0x20   # PARAMWALK_VALIDATE=PASS|FAIL
python -m tools.alpha_models.universal.pipeline param-walk-coverage --static alpha_static.json --out coverage.json
python -m tools.alpha_models.universal.pipeline prepare-unity --job jobs\<kind>_formula.json --project <P>
```

## 4. Unity API

```csharp
// Runtime/AlphaParamWalk.cs
[Serializable] class AlphaLocomotionParams { int flags; float[] size; float scale_head, scale_body, scale_hand,
    scale_foot, scale_tail, pitch_tail, angle_58; float[] offset_body, offset_head, offset_hand, offset_foot, offset_tail; }
[Serializable] class AlphaLocomotionSpec { string format, kind, rig, status; string[] parts, terms, code_only_terms,
    unknown_terms; AlphaLocomotionParams @params; static AlphaLocomotionSpec Parse(string json, List<string> problems); }
struct AlphaWalkInput { float phase, amp; bool boost; float headLeanDeg; bool onGround; }
static class AlphaParamWalk {
    float[] Joint(AlphaLocomotionParams p, AlphaWalkInput s, string part);   // J (Alpha row-vector)
    float HeadLeanTarget(AlphaLocomotionParams p, bool boost, bool accelerating);
    float LeanStep(float current, float target, float dtMs);
    float PhaseRate(AlphaLocomotionParams p, float speedXY);
    bool IsModelled(string part);
}
// Runtime/AlphaLocomotionDriver.cs  [RequireComponent(AlphaRig)]
TextAsset specAsset; float speedXY, amplitude, amplitudeTarget, phase, headLeanDeg; bool followAmplitude, boost,
accelerating, onGround;  bool Load(); void Step(float dt); void Apply(); AlphaWalkInput Input { get; }
```

Применение позы: `J → AlphaSpace.ToUnity → AlphaSpace.Decompose → part.localPosition/Rotation/Scale` (тот же путь, что у клипов; меш-потомок части сдвинут на `−anchor(size)` риггером `AlphaRig`). Драйвер вычисляет формулу каждый кадр (интерполяции нет). Игровой код подаёт только `speedXY`, `amplitude` (или `amplitudeTarget` + повторитель `PLACEHOLDER_NOT_ALPHA`), `boost`, `accelerating`.

## 5. Схемы данных

### 5.1 `locomotion.json` (`cubegame.param_walk/1`)
`{format, kind:"0xNN", rig:"alpha_creature/<hash>", parts[], status, terms[], code_only_terms[], unknown_terms[], params{flags, size[3], scale_head, scale_body, scale_hand, scale_foot, scale_tail, pitch_tail, angle_58, offset_body[3], offset_head[3], offset_hand[3], offset_foot[3], offset_tail[3]}}`. Без геометрии и поз: только параметры вида. Unity отклоняет `status = NEEDS_RESEARCH` и непустой `unknown_terms`.

### 5.2 Задание (`jobs/<имя>_formula.json`, в Git, без данных Alpha)
| Ключ | Обязателен | Смысл |
|---|---|---|
| `kind` | да | вид `"0xNN"` |
| `seed_variant` | нет (`[0,0]`) | вариант моделей вида |
| `locomotion` | да | `"formula"` |
| `amp` | нет (0.19) | амплитуда клипа формулы (устойчивая ходьба в захватах 0.19–0.193) |
| `boost`, `accelerating` | нет (false) | ×1.25 / ускорение ИИ (наклон головы +5°, CODE_ONLY) |
| `speed_xy` | нет (3.7) | скорость для скорости фазы клипа; 3.7 — медиана ИИ кота/бегуна (`PLACEHOLDER`, не параметр вида) |
| `reference_capture` | нет | `{"path": "D:/…/cap.jsonl", "kind": "0xNN"}` — захват **этого же семейства** для проверки ALPHA; путь с прямыми слэшами |
| `equipment` | **запрещено** | оружие — не часть формулы (`LOCOMOTION_PARTS_NOT_MODELLED`) |

### 5.3 Клип формулы
`cubegame.alpha_clip/2` как у захватов, но `status = RECONSTRUCTED_FROM_EVIDENCE`, `source.generator = cubegame.param_walk/1`, `source.terms/code_only_terms`, `phase_rate_status = INFERRED`. Имя `walk` (или `walk_formula`, если у семейства есть захваченный `walk`).

### 5.4 Покрытие (`cubegame.param_walk_coverage/1`)
`metrics{kinds, families, kinds_by_status, families_by_status, families_captured, uncaptured_families_by_status, uncaptured_families_per_open_term, uncaptured_families_per_blocking_reason}`, `terms{term: {status, evidence}}`, `families{id: {kinds[], parts[], status, code_only_terms[], unknown_terms[], reasons[], captured}}`, `kinds[]`.

### 5.5 Эталоны Unity
`paramwalk_golden.json` — `{status: TEST_DATA_NOT_ALPHA, cases[{name, p, phase, amp, boost, lean, onGround, part, m[16]}]}` (синтетика; 480 случаев). `paramwalk_reference.json` — `{status: ALPHA_LOCAL_ONLY, capture, capture_sha256, kind, rig, p, frames[{index, phase, amp, boost, lean, parts[], m[16·n]}]}` — только в `.scratch`.

## 6. Проверки и пороги (не ослаблять)

| Проверка | Где | Порог |
|---|---|---|
| формула vs захват (все части, кроме сглаживаемой головы) | `param-walk-validate`, `test_param_walk` | ≤ 1e-3 вокс., ≤ 0.01° |
| сглаживание головы (одношаговый прогноз) | то же | p95 < 0.05° |
| C# = Python (GOLDEN) | `AlphaParamWalkCheck` | max |Δ| ≤ 2e-4 |
| C# vs захват (ALPHA) | `AlphaParamWalkCheck` | ≤ 1e-3 вокс., ≤ 0.05° |
| рантайм = формула; ходьба не застыла; развёртка амплитуды; рост наклона | `AlphaLocomotionProbe` | ≤ 1e-3 / 0.05°; ≥ 10° или ≥ 1 вокс.; |Δlean| ≤ 1e-3 |
| GEOMETRY / ROUNDTRIP / SEAM клипа формулы | `AlphaTransferEditor` | как в S05A-2 |

Маркеры: `PARAMWALK_RESULT`, `LOCOMOTION_PLAYMODE_RESULT` (+ прежние `SELFTEST/TRANSFER/PLAYMODE_RESULT`). Итог `RESULT=PASS` только если все PASS и коды выхода 0.

## 7. Расширение ядра (только через архитектора)

Новый член формулы (например, `hand_rest_angles`, `root_angle_48`, плавание):
1. захват любого вида с ненулевым значением члена (`pose_capture --kinds`);
2. чтение цепочки в коде; реализация в `param_walk.joints` **и** `AlphaParamWalk.Joint`; член в `TERMS` со статусом `VERIFIED` только после того, как `param-walk-validate` на этом захвате даёт пороги §6;
3. `terms_used` — когда член задействован; синтетический вид в `SYNTHETIC_APPEARANCES` (GOLDEN покрывает ветку);
4. тест в `test_param_walk.py` (синтетика + локальное доказательство); перегенерировать покрытие.

Новая часть (крылья `slot_9a`, `slot_98`) — сначала правило якоря в `creature_rig` (методичка UNIVERSAL §3), затем член формулы.

## 8. Риски

* Адреса и константы — из одной сборки `Cube.exe` (SHA256 закреплён в `alpha_static`).
* CODE_ONLY-члены могут отличаться от рантайма (знак/порядок); статус клипов при них — `RECONSTRUCTED_FROM_EVIDENCE`, в отчёте указывать открытые члены.
* Формула — только локомоция состояния 0; переходы в атаки и действия требуют захвата (S05A-2).
* Скорость фазы INFERRED; динамика амплитуды не восстановлена — ступни могут слегка скользить при нестандартной скорости.
