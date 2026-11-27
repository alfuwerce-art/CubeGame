# CubeGame — контракт задачи (создаёт исполнитель)
Владелец пишет «исправь женский торс по этому референсу» или «добавь атаку», а исполнитель сам составляет минимальный контракт. Не требовать от владельца заполнения JSON. Сначала читать контекст и skills, затем записать контракт до изменения проекта. Для маленькой задачи достаточно коротких полей. Пример структуры (заменить все значения реальными; не копировать выдуманные пути):

```json
{
  "task_id": "G3-FEMALE-TORSO",
  "goal": "Изменить только форму женского торса по указанному референсу",
  "source": "Прямое поручение владельца + паспорт исходного ассета",
  "kind": "art",
  "asset_type": "character",
  "base_sha": "FULL_GIT_SHA",
  "branch": "codex/example",
  "engine": "Unreal (actual version)",
  "project_path": "actual authorized project path",
  "allowed_paths": ["docs/reports/example.md"],
  "context_files": ["docs/art/ART_DIRECTION.md", "docs/art/CHARACTER_STYLE_STANDARD.md", "docs/art/REFERENCE_PROTOCOL.md", "docs/art/ASSET_ACCEPTANCE.md", ".agents/skills/cubegame-voxel-asset/SKILL.md", ".agents/skills/cubegame-art-validation/SKILL.md"],
  "skills": ["cubegame-voxel-asset", "cubegame-art-validation"],
  "protected_files": [{"path": "actual existing file", "root": "external"}],
  "external_root": "actual authorized isolated root",
  "external_allowed_paths": ["Content/actual editable asset.uasset"],
  "references": ["local reference identifier + view + region; not public URL"],
  "acceptance_checks": ["Силуэт соответствует референсу", "Не изменены защищённые элементы"]
}
```

kind: code / art / animation / docs / research. Для персонажей asset_type: character. Для явно порученной правки документов kind: docs и editable_context_files — только изменяемые контекстные файлы, уже перечисленные в allowed_paths; прочий контекст защищён. engine/project_path — фактическая среда; для docs допустимо none. allowed_paths и external_allowed_paths — точные относительные файлы или префиксы каталогов с завершающим slash. external_root необязателен, только явно разрешённый отдельный проект. protected_files: root repo (default) или external, конкретные существующие файлы; для разделов одной сетки дополнительно сохранить измерения/хеши геометрических регионов средствами редактора и проверить вручную. Для арта/анимации обязательны references и профильные skills; для нового ассета без защищённых файлов явно обосновать в отчёте. AGENTS, Memory, Active Stage входят в gate автоматически.

Result JSON: `{"status":"READY_FOR_OWNER_REVIEW","checks":[{"name":"точный критерий из контракта","status":"PASS","evidence":["relative/path/to/evidence"]}],"technical_status":"PASS","visual_status":"PASS","evidence":["relative/path/to/report"],"external_changed_paths":[]}`. Для неполного результата status BLOCKED или PARTIAL; не выдавать PASS за мнение владельца. Evidence: путь относительно repo или объект {"root":"external","path":"..."}. Для code/docs visual_status может быть NOT_RUN; для art/animation READY требует PASS обоих (самопроверка, не acceptance). Для остальных обязательных проверок NOT_RUN/FAIL/BLOCKED несовместимы с READY. Если внешние операции не проверяемы автоматически, указать ограничения в отчёте. Передача: TASK/SHA/FILES/TESTS/PLAYER/EVIDENCE/BLOCKERS, фактические логи, до/после. Не публиковать приватные ссылки/файлы без разрешения.

## Animation extension (required for kind: animation)
Only cubegame-rig-animation and required subordinate cubegame-art-validation in skills. Do not declare unrelated skills. SEARCH is a valid requirement mode for selection tasks that include real preview/animation validation; a read-only catalog shortlist without preview is kind research with same primary skill and honest NOT_RUN for untested target motion. New/replacement motion requires documented SEARCH before adaptation, or exact owner-specified source. Do not author without explicit permission. For staged direction review create a scoped task for Walk/Run; do not include dependent transition changes until owner direction approval. Attach actual gate invocation logs, contract, receipt and result. Follow [selection](animation/SOURCE_SELECTION.md) and [editor recovery](animation/EDITOR_RECOVERY.md).
Include docs/agents/animation/QUALITY_PROTOCOL.md, WORK_MODES.md, TOOL_SELECTION.md and REVIEW_LAB.md (each with full relative path) in context_files, in addition to existing art/skill context. Add contract animation object: {"requirements":[{"id":"ANIM-001","owner_request":"exact owner wording/source","mode":"EDIT","target":"actual clip / phase","check":"exact existing acceptance_checks entry","preserve":"protected elements"}],"gameplay_required":true,"checkpoint":"local baseline revision + restoration recipe + active asset inventory","snapshots":[{"id":"idle","baseline":"relative/protected/export.json","allowed_keys":["actualAsset|actualBone|rotation"]}]}.
Each scoped requirement has a unique ID/check, and all checks have IDs; carry unresolved out-of-scope remarks in checkpoint. For EDIT require protected track export pair(s), except runtime-only edit with empty snapshots and explicit nonempty runtime_only_reason (no clip track edits). REVIEW has no asset mutation; protected files still required where relevant. Baseline export reference must be in protected_files (same string/object). Export JSON: {"tracks":{"asset|bone|channel":[...],"asset|metadata":{...}}}; full normalized saved/reloaded track/metadata map; allowed keys exact, no glob. Baseline immutable; candidate export same extractor. Do not upload private evidence. 
Result animation: {"requirements":[{"id":"ANIM-001","status":"PASS","observation":"actual time/frame, camera, observed result","evidence":["relative/path"]}],"domains":{"technical":{"status":"PASS","observation":"actual finding","evidence":["relative/path"]},"motion":{},"visual":{},"gameplay":{},"regression":{}},"snapshots":[{"id":"idle","candidate":"relative/candidate/export.json"}]}.
Every domain uses same status/observation/evidence structure; SKIP gameplay only if gameplay_required false, explicit observation reason. Technical and visual domain statuses match top-level statuses. Result per-ID status matches corresponding generic check. PASS requires evidence; READY requires all applicable domains and IDs PASS plus exact protected-track comparison. PARTIAL/BLOCKED may report missing candidate snapshots explicitly by omitting entries; cannot claim regression PASS without all pairs. Gate checks evidence paths and track export equality; does not prove exports complete or observations truthful. See [protocol](animation/QUALITY_PROTOCOL.md).
