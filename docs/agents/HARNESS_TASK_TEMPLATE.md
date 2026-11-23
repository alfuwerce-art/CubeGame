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
